/* Hidden Harbours — parametric ISO Side Dragger (M2 bake recipe, ADR-0006 — same pipeline as
   lobsterBoatIsoRig.js / capeIslanderIsoRig.js). PASS 1: full hull + aft deckhouse + wheelhouse +
   funnel + ochre foremast with twin derricks + starboard trawl gallows. Tier 4, the first offshore
   hull: ~25 m LOA riveted-steel SIDE TRAWLER ("works The Banks"). The classic dragger arrangement,
   structured from the WD-3 reference set: a long open WORKING DECK forward with fish hatches, a raised
   whaleback foc'sle at the raked stem, the trawl WINCH athwartships in front of the house, TWO GALLOWS
   frames on the starboard rail (the working side) with the otter boards hung and the net piled along
   the bulwark, and the whole house AFT — cream lower house, glassy wheelhouse looking over the deck,
   buff funnel with a black cap behind it, mizzen pole, cruiser stern. Rust-red oxide topsides + black
   boot, cream cove line + house, taupe steel deck, ochre mast/derricks/gallows, net green — palette-
   clamped (KTC) to the fleet slice SideDragger.png / Roster/SideDragger.png. Fixed 3/4 turntable camera
   (elev 40deg default, adjustable), 45deg steps, flat-facet shading from a fixed upper-left key,
   z-buffered, ordered dither, 1px keyline post-pass, NO AA. 32 px = 1 m.

   Single cell 896x792, pivot (448,450) = boat origin (amidships, keel bottom, centreline), pinned every
   heading so a direction swap never shifts placement. Deck anchors baked from day one:
   helmSeat(dir,opts) -> wheelhouse skipper; gallowsMounts(dir,opts) -> the two starboard gallows block
   points (haulerMount = the forward one, API-compatible); tubMounts(dir,opts) -> working-deck anchors;
   navMounts(dir,opts) -> {port,star,stern,mast} for the night bake. Pass the hull's rock(i) so overlays
   ride the wave. Exposes globalThis.SideDraggerIso = { W,H,PX,DIRS,pivot,order,ROCK,rock(i),
   render(dir,opts), helmSeat,HELM, haulerMount,HAULER, gallowsMounts,GALLOWS, tubMounts,TUBS, navMounts,
   HULL,BOOT,CREAM,DECKF,WOOD,GLAS,BUFF,NET,STEEL,IRON,KEY }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 896, H = 792, cx = 448, cy = 450;   // cell + pivot (projection of boat origin)
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const ROCK = { frames: 8, rollA: 2.0, pitchA: 1.1, heaveA: 1.0, period: 4.2 };  // 25 m of steel — slow, stiff offshore roll
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 25.0, TH = 0.07, DECK = 2.05;
  const NSEG = 24;
  const RAKE = 0.90;   // raked trawler stem

  // ---- palette ramps dark->light (KTC: sampled from Art/Boats/SideDragger.png + Art/UI/Roster/SideDragger.png) ----
  const HULL  = ['#3a1a14','#582a20','#7a382c','#9c4a3c','#b5604e','#c97a62','#d99678'];  // rust-red oxide topsides
  const BOOT  = ['#0e1114','#16191e','#20242a','#2b3338','#3a4450'];                       // near-black boot / bottom
  const CREAM = ['#8f8672','#a89c80','#c2b79c','#d8cdb4','#eae0c8','#f4ecd8'];             // house + cove line
  const DECKF = ['#3a3630','#46443a','#5a5248','#645c52','#7e756a','#8a8175','#9c937f'];  // taupe steel deck / bulwark liner
  const WOOD  = ['#3a2c20','#5a4634','#6f5840','#8c6a45','#a5825a'];                       // hatch coamings, otter boards
  const GLAS  = ['#131c21','#213039','#33434e','#48657a','#6b91a1'];                       // window glass (sea-grey)
  const BUFF  = ['#6b4a22','#8c5e2c','#b06f32','#cf7a35','#e29a55','#f0b878'];             // ochre mast / derricks / funnel / gallows
  const NET   = ['#16241d','#22362b','#2c4438','#3f5e4f','#5c7d6b'];                       // trawl net pile
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2'];                       // winch drums, rails, aerials
  const IRON  = ['#0e1114','#171b21','#232a32','#333c46'];                                 // dark fittings
  const KEY   = '#140f0c';
  const MATS = { hull:{ramp:HULL,off:0}, boot:{ramp:BOOT,off:0}, cream:{ramp:CREAM,off:0},
                 deck:{ramp:DECKF,off:0}, wood:{ramp:WOOD,off:0}, glas:{ramp:GLAS,off:0}, buff:{ramp:BUFF,off:0},
                 net:{ramp:NET,off:0}, steel:{ramp:STEEL,off:0}, iron:{ramp:IRON,off:0},
                 blk:{ramp:BOOT,off:-1}, dark:{ramp:BOOT,off:-2} };
  const RINDEX = {}; [HULL,BOOT,CREAM,DECKF,WOOD,GLAS,BUFF,NET,STEEL,IRON].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m) ----
  // full midbody, cruiser stern (keel rises aft), marked sheer sweeping to a high raked bow.
  // sheerZ = kz+depth: 3.20, 2.95, 2.77, 2.62, 2.60, 2.75, 3.12, 3.80, 4.70
  const T = [
    [2.30,1.30,2.90,0.30],   // 0 cruiser stern
    [3.00,2.30,2.85,0.10],   // 1
    [3.35,2.80,2.75,0.02],   // 2
    [3.50,3.00,2.62,0.00],   // 3
    [3.50,3.00,2.60,0.00],   // 4 amidships (max beam 7 m)
    [3.42,2.80,2.75,0.00],   // 5
    [3.10,2.20,3.10,0.02],   // 6
    [2.30,1.10,3.65,0.15],   // 7 bow shoulder (flare)
    [0.14,0.06,4.30,0.40],   // 8 stem
  ];
  const lerp = (a,b,t)=>a+(b-a)*t;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), wb:lerp(A[1],B[1],fr), dep:lerp(A[2],B[2],fr),
             kz:lerp(A[3],B[3],fr), y:-L/2+u*L };
  }
  function bowRake(u,frac){ const t=Math.max(0,(u-0.62)/0.38), s=t*t*(3-2*t); return RAKE*s*(0.30+0.70*frac); }
  function skin(side,u,frac,inset){
    const st=station(u);
    const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
    return [ side*lerp(wb,ws,frac), st.y+bowRake(u,frac), st.kz+lerp(0,dep,frac) ];
  }
  function dfrac(st){ return Math.max(0.04, Math.min(0.98, (DECK - st.kz)/st.dep)); }

  // ---- generic solids ----
  const ID=(p)=>p;
  const v_sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]], v_add=(a,b)=>[a[0]+b[0],a[1]+b[1],a[2]+b[2]];
  const v_mul=(a,s)=>[a[0]*s,a[1]*s,a[2]*s], v_norm=(a)=>{const m=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/m,a[1]/m,a[2]/m];};
  const v_cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  function box(c,h,mat,b,db,xf){
    xf=xf||ID;
    const P=(sx,sy,sz)=>xf([c[0]+sx*h[0], c[1]+sy*h[1], c[2]+sz*h[2]]);
    const f=(v)=>({v,mat,b:b||0,db:db||0});
    return [
      f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]),
      f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]),
      f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]),
      f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]),
      f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]),
      f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]),
    ];
  }
  function tube(A,B2,rad,mat,b,xf){
    xf=xf||ID;
    const P0=xf(A), P1=xf(B2);
    const ax=v_norm(v_sub(P1,P0)); let up=[0,0,1]; if(Math.abs(ax[2])>0.9) up=[0,1,0];
    const r=v_norm(v_cross(ax,up)), u=v_cross(r,ax);
    const ring=(P)=>[ v_add(v_add(P,v_mul(r,rad)),v_mul(u,rad)), v_add(v_add(P,v_mul(r,-rad)),v_mul(u,rad)),
                      v_add(v_add(P,v_mul(r,-rad)),v_mul(u,-rad)), v_add(v_add(P,v_mul(r,rad)),v_mul(u,-rad)) ];
    const r0=ring(P0), r1=ring(P1), out=[];
    for(let k=0;k<4;k++){ const k2=(k+1)%4; out.push({v:[r0[k],r0[k2],r1[k2],r1[k]],mat,b:b||0,db:-0.15}); }
    return out;
  }
  const DBP = 0.05;
  const frontPanel=(y,xa,xb,za,zb,mat,b)=>({v:[[xa,y,zb],[xb,y,zb],[xb,y,za],[xa,y,za]],mat,b:b||0,db:DBP});
  const backPanel =(y,xa,xb,za,zb,mat,b)=>({v:[[xb,y,zb],[xa,y,zb],[xa,y,za],[xb,y,za]],mat,b:b||0,db:DBP});
  const rightPanel=(x,ya,yb,za,zb,mat,b)=>({v:[[x,yb,zb],[x,ya,zb],[x,ya,za],[x,yb,za]],mat,b:b||0,db:DBP});
  const leftPanel =(x,ya,yb,za,zb,mat,b)=>({v:[[x,ya,zb],[x,yb,zb],[x,yb,za],[x,ya,za]],mat,b:b||0,db:DBP});

  const F = [];
  const face=(v,mat,b,db)=>F.push({v,mat:mat||'hull',b:b||0,db:db||0});
  const boxF=(c,h,mat,b,db)=>{ F.push.apply(F, box(c,h,mat,b,db)); };
  const tubeF=(A,B2,rad,mat,b)=>{ F.push.apply(F, tube(A,B2,rad,mat,b)); };
  function objNormal(a,b,c){ const ux=b[0]-a[0],uy=b[1]-a[1],uz=b[2]-a[2], vx=c[0]-a[0],vy=c[1]-a[1],vz=c[2]-a[2];
    return [uy*vz-uz*vy, uz*vx-ux*vz, ux*vy-uy*vx]; }
  function faceO(v, outward, mat, b, db){ const n=objNormal(v[0],v[1],v[2]);
    if(n[0]*outward[0]+n[1]*outward[1]+n[2]*outward[2] < 0) v=v.slice().reverse();
    return {v, mat, b:b||0, db:(db==null?DBP:db)}; }
  function rrect(ua,ub,va,vb,c){ return [[ua+c,va],[ub-c,va],[ub,va+c],[ub,vb-c],[ub-c,vb],[ua+c,vb],[ua,vb-c],[ua,va+c]]; }
  function winRR(mapUV, outward, ua,ub,va,vb, cut, mat, b){
    return faceO(rrect(ua,ub,va,vb,cut).map(([u,v])=>mapUV(u,v)), outward, mat, b); }
  function glaze(mk, outward, ua,ub,va,vb, glassB, cut){ cut=cut||0.10;
    F.push(winRR(mk(0.03),  outward, ua-0.06,ub+0.06, va-0.055,vb+0.055, cut+0.03, 'iron', -0.15));
    F.push(winRR(mk(0.065), outward, ua,ub, va,vb, cut, 'glas', glassB));
  }
  // paint, frac 0(keel)->1(sheer): black boot, rust topsides, cream cove line, rust sheer strake
  const OB = [ [0,0.30,'boot',-0.2,0], [0.30,0.90,'hull',0,0], [0.90,0.938,'cream',0.25,0.01], [0.938,1,'hull',0.12,0] ];

  // deckhouse envelope (aft): lower block + wheelhouse looking over the working deck
  const HXl = 2.35, HYa = -10.6, HYf = -4.6, HZ1l = 4.50;          // lower house
  const HXw = 2.05, WYa = -8.0,  FYb = -4.5, FYt = -4.95;          // wheelhouse (raked front)
  const WZ0 = 4.56, WZ1 = 6.60, ROOFZ = 6.66;

  (function build(){
    // ---- hull shell ----
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],mat,b,db);
        // inner bulwark liner (taupe), deck line -> sheer — open deck only (fwd of the foc'sle break
        // the liner inset collapses against the bow flare and z-fights, so stop at y ~ +7.4)
        const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb);
        if(sa.y <= 7.4){
          const LT=0.95;
          for(let k=0;k<2;k++){
            const g0a=fa+(LT-fa)*k/2, g1a=fa+(LT-fa)*(k+1)/2;
            const g0b=fb+(LT-fb)*k/2, g1b=fb+(LT-fb)*(k+1)/2;
            face([skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'deck',-1.5,-0.03);
          }
        }
        // bottom (boot)
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'boot',-1.0);
        // covering board — rail cap both sides, full sheer
        const oa=skin(side,u0,1),ob=skin(side,u1,1),ia=skin(side,u0,1,1),ib=skin(side,u1,1,1);
        const inb=(p)=>[p[0]-side*0.34,p[1],p[2]-0.004];
        face([oa,ob,inb(ib),inb(ia)],'deck',-1.2,0.03);
      }
    }
    // ---- main working deck (taupe steel), stern quarter -> foc'sle break ----
    const SOLE_U = 0.80, U0 = 0.016, DSEG = 20;
    const dw=(u)=>{ const st=station(u); return (lerp(st.wb,st.ws,dfrac(st))-TH)*0.97; };
    for(let i=0;i<DSEG;i++){
      const u0=U0+(SOLE_U-U0)*i/DSEG, u1=U0+(SOLE_U-U0)*(i+1)/DSEG;
      face([[-dw(u0),station(u0).y,DECK],[dw(u0),station(u0).y,DECK],[dw(u1),station(u1).y,DECK],[-dw(u1),station(u1).y,DECK]],'deck',-0.35);
    }
    // ---- fish hatches: wood coaming + dark tarpaulin top ----
    const hatch=(y,hw,hl)=>{
      boxF([0,y,DECK+0.14],[hw,hl,0.14],'wood',-0.2);
      face([[-hw+0.06,y-hl+0.06,DECK+0.30],[hw-0.06,y-hl+0.06,DECK+0.30],[hw-0.06,y+hl-0.06,DECK+0.30],[-hw+0.06,y+hl-0.06,DECK+0.30]],'dark',-0.3,0.02);
    };
    hatch(4.9,1.15,0.95); hatch(1.7,1.15,0.95); hatch(-0.9,1.0,0.8);
    // ---- foc'sle: raised whaleback deck following the sheer, break bulkhead + crew door ----
    const FSEG=6, FCAP=0.985;
    const fz=(u)=>{ const st=station(u); return st.kz+st.dep-0.10; };
    const fw=(u)=>{ const st=station(u); return Math.max(0.02, st.ws-0.32); };
    const fy=(u)=> station(u).y + bowRake(u,1);
    for(let i=0;i<FSEG;i++){
      const u0=SOLE_U+(FCAP-SOLE_U)*i/FSEG, u1=SOLE_U+(FCAP-SOLE_U)*(i+1)/FSEG;
      face([[-fw(u0),fy(u0),fz(u0)],[fw(u0),fy(u0),fz(u0)],[fw(u1),fy(u1),fz(u1)],[-fw(u1),fy(u1),fz(u1)]],'deck',0.4,-0.02);
    }
    (function(){ const u=SOLE_U, wv=fw(u), z=fz(u), y=station(u).y, yF=fy(u), st=station(u);
      const hw=(zz)=>{ const fr=Math.max(0,Math.min(1,(zz-st.kz)/st.dep)); return lerp(st.wb,st.ws,fr)-TH; };
      const wTop=Math.min(wv,hw(z)), wDeck=hw(DECK);
      face([[wTop,y,z],[-wTop,y,z],[-wDeck,y,DECK],[wDeck,y,DECK]],'cream',-0.6,-0.03);   // break bulkhead (faces aft)
      F.push(backPanel(y-0.03,-0.45,0.45,DECK+0.05,z-0.14,'dark',-0.5));                  // crew doorway
      face([[-wv,y-0.36,z],[wv,y-0.36,z],[wv,yF,z],[-wv,yF,z]],'deck',0.4,-0.03);         // flat bridge to the raked deck edge
    })();
    // foc'sle furniture: anchor windlass + samson post + bow bollards
    (function(){ const zf=fz(0.884)+0.02;
      boxF([0,9.6,zf+0.16],[0.52,0.30,0.16],'iron',0.1,-0.02);
      tubeF([-0.55,9.6,zf+0.38],[0.55,9.6,zf+0.38],0.22,'steel',-0.1);
      boxF([0,fy(0.952),fz(0.952)+0.16],[0.05,0.06,0.16],'iron',0.15,-0.02);
      for(const s of [-1,1]) boxF([s*0.85,8.6,zf+0.10],[0.06,0.06,0.10],'iron',0.1,-0.02);
    })();
    // ---- transom bands + covering board across the cruiser stern ----
    const tp=(s,f)=>skin(s,0,f);
    for(const [f0,f1,mat,b] of OB)
      face([tp(-1,f1),tp(1,f1),tp(1,f0),tp(-1,f0)], mat, (b||0)-0.8, 0.005);
    (function(){ const s0=station(0), zt=s0.kz+s0.dep, wsx=s0.ws-TH;
      face([[-wsx,s0.y,zt],[wsx,s0.y,zt],[wsx,s0.y+0.30,zt-0.004],[-wsx,s0.y+0.30,zt-0.004]],'deck',-0.9,0.03); })();
    // stern capstan + quarter bollards
    tubeF([0,-12.0,DECK],[0,-12.0,DECK+0.50],0.18,'steel',-0.1);
    boxF([0,-12.0,DECK+0.54],[0.24,0.24,0.04],'steel',0.2);
    for(const s of [-1,1]) boxF([s*1.75,-11.8,DECK+0.10],[0.06,0.09,0.10],'iron',0.1,-0.02);

    // ---- DECKHOUSE lower block (cream) ----
    face([[-HXl,HYf,HZ1l],[HXl,HYf,HZ1l],[HXl,HYf,DECK],[-HXl,HYf,DECK]],'cream',0.4);      // front (faces the deck)
    face([[HXl,HYa,HZ1l],[-HXl,HYa,HZ1l],[-HXl,HYa,DECK],[HXl,HYa,DECK]],'cream',-0.7);     // aft
    face([[-HXl,HYa,HZ1l],[-HXl,HYf,HZ1l],[-HXl,HYf,DECK],[-HXl,HYa,DECK]],'cream',-0.1);   // port
    face([[HXl,HYf,HZ1l],[HXl,HYa,HZ1l],[HXl,HYa,DECK],[HXl,HYf,DECK]],'cream',-1.0);       // starboard
    F.push(frontPanel(HYf+0.03,-0.50,0.40,DECK+0.05,4.05,'dark',-0.5));                     // deck door in the front wall
    for(const side of [-1,1]){                                                               // portholes ×3 + side door
      const P = side<0 ? leftPanel : rightPanel, xw=side*(HXl);
      for(const yy of [-9.9,-8.9,-7.9]){
        F.push(P(side*(HXl+0.03), yy-0.27, yy+0.27, 3.12, 3.78, 'iron', -0.15));
        F.push(P(side*(HXl+0.065), yy-0.20, yy+0.20, 3.20, 3.70, 'glas', side<0?-0.15:-1.05));
      }
      F.push(P(side*(HXl+0.03), -5.65, -4.95, DECK+0.05, 4.05, 'dark', -0.5));
    }
    boxF([0,(HYa+HYf)/2,HZ1l+0.03],[HXl+0.10,(HYf-HYa)/2+0.08,0.06],'cream',0.5,-0.01);      // lower roof slab
    // boat-deck railing around the aft roof
    (function(){ const rz=5.35, rx=2.15, ya=-10.45, yf=-8.15;
      tubeF([-rx,ya,rz],[rx,ya,rz],0.035,'steel',0.15);
      for(const s of [-1,1]){ tubeF([s*rx,ya,rz],[s*rx,yf,rz],0.035,'steel',s<0?0.15:-0.3);
        for(const yy of [ya+0.05,-9.3,yf]) tubeF([s*rx,yy,HZ1l+0.09],[s*rx,yy,rz],0.022,'steel',-0.1); }
    })();

    // ---- WHEELHOUSE (raked front, looks over the working deck) ----
    face([[-HXw,WYa,WZ0],[-HXw,WYa,WZ1],[-HXw,FYt,WZ1],[-HXw,FYb,WZ0]],'cream',-0.1);        // port
    face([[HXw,WYa,WZ0],[HXw,FYb,WZ0],[HXw,FYt,WZ1],[HXw,WYa,WZ1]],'cream',-1.0);            // starboard
    face([[-HXw,FYt,WZ1],[HXw,FYt,WZ1],[HXw,FYb,WZ0],[-HXw,FYb,WZ0]],'cream',0.4);           // raked front
    face([[HXw,WYa,WZ1],[-HXw,WYa,WZ1],[-HXw,WYa,WZ0],[HXw,WYa,WZ0]],'cream',-0.7);          // aft
    const _rny=(WZ1-WZ0), _rnz=(FYb-FYt), _rn=Math.hypot(_rny,_rnz), nY=_rny/_rn, nZ=_rnz/_rn;
    const yFront=(z)=> FYb + (FYt-FYb)*(z-WZ0)/(WZ1-WZ0);
    for(const [xa,xb] of [[-1.72,-0.94],[-0.78,-0.04],[0.04,0.78],[0.94,1.72]])              // 4-pane windscreen
      glaze((pr)=>((x,z)=>[x, yFront(z)+nY*pr, z+nZ*pr]), [0,nY,nZ], xa,xb, 5.40,6.20, 0.5, 0.05);
    const sideWin=(side, pts, glassB)=>{
      const cyg=pts.reduce((s,p)=>s+p[0],0)/pts.length, czg=pts.reduce((s,p)=>s+p[1],0)/pts.length;
      const trimPts=pts.map(([y,z])=>[y+(y-cyg)*0.12, z+(z-czg)*0.12]);
      F.push(faceO(trimPts.map(([y,z])=>[side*(HXw+0.03), y, z]), [side,0,0], 'iron', -0.15, DBP));
      F.push(faceO(pts.map(([y,z])=>[side*(HXw+0.065), y, z]),    [side,0,0], 'glas', glassB, DBP));
    };
    for(const side of [-1,1]){
      const b0=side<0?-0.15:-1.05;
      sideWin(side, [[-5.75,5.40],[-5.05,5.40],[-5.05,6.20],[-5.75,6.20]], b0);
      sideWin(side, [[-6.65,5.40],[-5.95,5.40],[-5.95,6.20],[-6.65,6.20]], b0);
    }
    // aft trawl-watching windows (skipper watches the gear astern)
    for(const [xa,xb] of [[-1.55,-0.55],[0.55,1.55]]){
      F.push(backPanel(WYa-0.03, xa-0.06, xb+0.06, 5.34, 6.16, 'iron', -0.15));
      F.push(backPanel(WYa-0.065, xa, xb, 5.40, 6.10, 'glas', -0.25));
    }
    boxF([0,-6.25,ROOFZ],[2.20,1.90,0.06],'cream',0.6,-0.01);                                // roof (brow overhangs the screen)
    // roof gear: radar, whip aerials, horn
    tubeF([0,-6.6,ROOFZ+0.05],[0,-6.6,7.30],0.08,'steel',0.1);
    tubeF([0,-6.6,7.30],[0,-6.6,7.52],0.34,'cream',0.15);
    boxF([0,-6.6,7.54],[0.34,0.26,0.03],'cream',0.5);
    for(const s of [-1,1]) tubeF([s*1.5,-7.5,ROOFZ+0.05],[s*1.85,-8.2,9.4],0.03,'steel',s<0?0.25:-0.2);
    boxF([-0.9,-5.4,ROOFZ+0.14],[0.10,0.22,0.08],'steel',0.2);

    // ---- FUNNEL (buff, black cap, raked aft) behind the wheelhouse ----
    tubeF([0,-9.65,HZ1l+0.04],[0,-9.95,6.35],0.58,'buff',-0.1);
    tubeF([0,-9.95,6.35],[0,-9.99,6.62],0.585,'hull',0.1);
    tubeF([0,-9.99,6.62],[0,-10.05,6.98],0.57,'blk',-0.3);
    // mizzen pole aft of the funnel + stay to the stern
    tubeF([0,-10.75,HZ1l+0.06],[0,-11.05,9.3],0.09,'buff',0.1);
    tubeF([0,-11.0,9.1],[0,-12.2,3.45],0.025,'steel',-0.2);

    // ---- FOREMAST (ochre) with crosstree + twin derrick booms, forward of the hatches ----
    const MY=3.3;
    boxF([0,MY,DECK+0.14],[0.30,0.30,0.14],'iron',0.0,-0.02);                                // mast step
    tubeF([0,MY,DECK+0.2],[0,MY-0.15,11.2],0.15,'buff',0.15);                                // pole (slight aft rake)
    tubeF([-1.15,MY-0.08,8.5],[1.15,MY-0.08,8.5],0.06,'buff',0.2);                           // crosstree
    boxF([0,MY-0.14,10.35],[0.09,0.09,0.10],'iron',0.2,-0.02);                               // masthead light box
    for(const s of [-1,1]){
      tubeF([s*0.16,MY,2.90],[s*3.4,6.9,6.3],0.10,'buff',s<0?0.15:-0.35);                    // derrick boom
      boxF([s*3.4,6.9,6.32],[0.09,0.09,0.12],'iron',-0.2,-0.02);                             // tip block
      tubeF([s*0.12,MY-0.05,9.7],[s*3.32,6.82,6.42],0.028,'steel',-0.15);                    // topping lift
    }
    tubeF([0,MY-0.12,10.7],[0,12.6,4.50],0.028,'steel',-0.1);                                // forestay to the stem head
    for(const s of [-1,1]) tubeF([s*0.10,MY-0.05,10.0],[s*1.6,-4.45,6.70],0.028,'steel',-0.2); // backstays to the brow

    // ---- STARBOARD TRAWL GALLOWS ×2 (the working side) + otter boards + warps ----
    const gallows=(yc,xb)=>{
      for(const dy of [-0.45,0.45]) tubeF([xb,yc+dy,DECK],[xb+0.22,yc+dy,4.35],0.085,'buff',-0.35);
      tubeF([xb+0.22,yc-0.45,4.35],[xb+0.22,yc+0.45,4.35],0.085,'buff',-0.2);
      boxF([xb+0.20,yc,4.05],[0.07,0.07,0.12],'iron',-0.2,-0.02);
      tubeF([xb+0.13,yc,3.90],[xb+0.27,yc,3.90],0.06,'steel',-0.1);
    };
    gallows(6.2,2.40); gallows(-1.6,2.90);
    boxF([2.10,6.15,3.10],[0.07,0.55,0.45],'wood',-0.35);                                    // fwd otter board (hung inboard)
    boxF([2.60,-1.65,2.95],[0.07,0.55,0.45],'wood',-0.55);                                   // aft otter board
    tubeF([1.05,-3.52,2.90],[2.55,6.15,4.00],0.02,'steel',-0.5);                             // fwd warp
    tubeF([1.40,-3.50,2.75],[3.05,-1.60,4.00],0.02,'steel',-0.5);                            // aft warp

    // ---- TRAWL WINCH athwartships in front of the house ----
    boxF([0,-3.6,DECK+0.18],[1.70,0.50,0.18],'iron',0.0,-0.02);
    for(const s of [-1,1]) tubeF([s*0.45,-3.6,DECK+0.68],[s*1.55,-3.6,DECK+0.68],0.42,'steel',s<0?-0.1:-0.45);
    boxF([0,-3.6,DECK+0.62],[0.34,0.30,0.30],'iron',0.1,-0.02);

    // ---- NET piled along the starboard bulwark between the gallows ----
    boxF([2.65,1.2,DECK+0.30],[0.55,1.80,0.30],'net',-0.35,-0.01);
    boxF([2.55,3.3,DECK+0.26],[0.50,0.95,0.26],'net',-0.05,-0.01);
    boxF([2.75,-0.4,DECK+0.24],[0.48,0.85,0.24],'net',-0.7,-0.01);
    for(const [fx,fy2] of [[2.5,0.4],[2.85,1.9],[2.6,2.9]]) boxF([fx,fy2,DECK+0.62],[0.10,0.10,0.08],'buff',0.3,-0.03); // floats
  })();

  // ---- rasterizer (shared recipe) ----
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){
    return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2];
  }
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) };
  }
  function projVert(x,y,z,B,G){
    const gx=G?G.cx:cx, gy=G?G.cy:cy;
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:gx+xr*S, sy:gy-(yr*B.se+zr*B.ce)*S - B.heave, d:(yr*B.ce-zr*B.se) };
  }
  function _paint(faces, opts, doEdge, G){
    const PW=G?G.W:W, PH=G?G.H:H;
    const B=camBasis(opts);
    const zbuf=new Float32Array(PW*PH).fill(Infinity);
    const col=new Array(PW*PH).fill(null);
    const dep=new Float32Array(PW*PH);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B,G));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce); if(sh<0 && ((f.b||0)<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + (f.b||0);
      const M = MATS[f.mat] || MATS.hull;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1]);
      function fillTri(a,b,c){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(PW-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(PH-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) return;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          let w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          let w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          let w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-(f.db||0);
          const i=y*PW+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d;
            let base=Math.floor(fidx);
            let idx=base+((fidx-base)>BAYER[x&3][y&3]?1:0)+M.off;
            col[i]=M.ramp[Math.max(0,Math.min(M.ramp.length-1,idx))];
          }
        }
      }
    }
    const out=new Array(PW*PH).fill(null);
    for(let i=0;i<PW*PH;i++) out[i]=col[i];
    if(doEdge){
      for(let y=0;y<PH;y++) for(let x=0;x<PW;x++){
        const i=y*PW+x; if(!col[i]) continue;
        for(const [dx,dy] of [[1,0],[0,1]]){
          const nx=x+dx, ny=y+dy; if(nx>=PW||ny>=PH) continue;
          const j=ny*PW+nx; if(!col[j]) continue;
          if(Math.abs(dep[i]-dep[j])>0.30){
            const far=dep[i]>dep[j]?i:j, e=RINDEX[col[far]];
            if(e && e.i>0) out[far]=e.r[Math.max(0,e.i-2)];
          }
        }
      }
    }
    for(let y=0;y<PH;y++) for(let x=0;x<PW;x++){
      const i=y*PW+x; if(out[i]) continue;
      let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
        const nx=x+dx, ny=y+dy;
        if(nx>=0&&nx<PW&&ny>=0&&ny<PH&&col[ny*PW+nx]){ touch=true; break; }
      }
      if(touch) out[i]=KEY;
    }
    return out;
  }
  function _toRGBA(out, PW, PH){
    PW=PW||W; PH=PH||H;
    const rgba=new Uint8ClampedArray(PW*PH*4);
    for(let i=0;i<PW*PH;i++){
      const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      rgba[i*4]=parseInt(c.slice(1,3),16); rgba[i*4+1]=parseInt(c.slice(3,5),16);
      rgba[i*4+2]=parseInt(c.slice(5,7),16); rgba[i*4+3]=255;
    }
    return rgba;
  }
  function render(dir, opts){
    opts = (typeof opts==='number') ? {elev:opts} : (opts||{});
    return _toRGBA(_paint(F, Object.assign({}, opts, {dir}), true));
  }

  // ---- deck anchors (cell coords; pass rock(i) so overlays ride the wave) ----
  const HELM = { x:0.30, y:-5.15, z:4.60 };
  function helmSeat(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HELM.x, HELM.y, HELM.z, B);
    return { x:p.sx, y:p.sy };
  }
  const GALLOWS = [ {x:2.60,y:6.2,z:4.05}, {x:3.10,y:-1.6,z:4.05} ];   // fwd + aft block points
  function gallowsMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return GALLOWS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  const HAULER = GALLOWS[0];
  function haulerMount(dir, opts){ return gallowsMounts(dir, opts)[0]; }
  // working-deck anchors (crew / tubs / catch piles), clear of hatches, winch and the net
  const TUBS = [ {x:-1.8,y:0.9,z:DECK}, {x:1.6,y:0.9,z:DECK},
                 {x:-1.8,y:-1.8,z:DECK}, {x:1.6,y:-1.8,z:DECK}, {x:-1.8,y:6.0,z:DECK} ];
  function tubMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  // nav lights: sidelights on the wheelhouse sides, stern light, masthead
  function navMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    const pt=(x,y,z)=>{ const p=projVert(x,y,z,B); return {x:p.sx,y:p.sy}; };
    return {
      port:  pt(-2.12,-5.0,6.30),
      star:  pt( 2.12,-5.0,6.30),
      stern: pt(0,-12.35,3.30),
      mast:  pt(0, 3.16,11.20),
    };
  }

  root.SideDraggerIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], HULL, BOOT, CREAM, DECKF, WOOD, GLAS, BUFF, NET, STEEL, IRON, KEY,
    render, ROCK, rock:rockMotion, helmSeat, HELM, haulerMount, HAULER, gallowsMounts, GALLOWS, tubMounts, TUBS, navMounts };
})(typeof globalThis!=='undefined'?globalThis:window);
