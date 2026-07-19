/* Hidden Harbours — parametric ISO Stern Trawler (M2 bake recipe, ADR-0006 — same pipeline as
   sideDraggerIsoRig.js / lobsterBoatIsoRig.js / capeIslanderIsoRig.js). PASS 1: full hull + forward
   house + trawl deck + STERN RAMP + gantry + net drum + split winches. Tier 5, the big steel ship:
   ~38 m LOA STERN TRAWLER — the arrangement flips from the side dragger: the whole house stands
   FORWARD (cream block + glassy wheelhouse with aft trawl-watching windows and a buff raked funnel
   behind it), and the long working deck runs AFT — split trawl winches at the house, two wood-coamed
   fish hatches, the big ochre NET DRUM wound with navy net, an ochre portal GANTRY straddling the
   deck, and a RAMP cut clean through the transom to the waterline so the trawl comes up the stern.
   Grey riveted-steel topsides + black boot, cream cove line + house, taupe steel deck, ochre
   gantry/drum/funnel, navy net — palette-clamped (KTC) to the fleet slice SternTrawler.png /
   Roster/SternTrawler.png. Fixed 3/4 turntable camera (elev 40deg default, adjustable), 45deg steps,
   flat-facet shading from a fixed upper-left key, z-buffered, ordered dither, 1px keyline post-pass,
   NO AA. 32 px = 1 m.

   Single cell 1344x1152, pivot (672,672) = boat origin (amidships, keel bottom, centreline), pinned
   every heading. Deck anchors baked from day one: helmSeat(dir,opts) -> wheelhouse skipper;
   gantryMounts(dir,opts) -> the two gantry block points over the ramp (gallowsMounts/haulerMount kept
   API-compatible); drumMount(dir,opts) -> net-drum axis; tubMounts(dir,opts) -> working-deck anchors;
   navMounts(dir,opts) -> {port,star,stern,mast} for the night bake. Pass the hull's rock(i) so
   overlays ride the wave. Exposes globalThis.SternTrawlerIso = { W,H,PX,DIRS,pivot,order,ROCK,rock(i),
   render(dir,opts), helmSeat,HELM, gantryMounts,GANTRY, gallowsMounts,GALLOWS, haulerMount,HAULER,
   drumMount,DRUM, tubMounts,TUBS, navMounts, HULL,BOOT,CREAM,DECKF,WOOD,GLAS,BUFF,NET,STEEL,IRON,KEY }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1344, H = 1152, cx = 672, cy = 672;   // cell + pivot (projection of boat origin)
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const ROCK = { frames: 8, rollA: 1.6, pitchA: 0.9, heaveA: 1.0, period: 5.0 };  // 38 m of steel — the slowest, stiffest roll in the fleet
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 38.0, TH = 0.09, DECK = 3.5;
  const NSEG = 24;
  const RAKE = 1.30;   // raked trawler stem

  // ---- palette ramps dark->light (KTC: sampled from Art/Boats/SternTrawler.png + Art/UI/Roster/SternTrawler.png) ----
  const HULL  = ['#15191c','#2b3338','#454e54','#565c5f','#6f767a','#868d90','#a3adb2'];  // grey steel topsides
  const BOOT  = ['#0a0c0e','#111417','#181c20','#1d2226','#262b2e'];                       // near-black boot / bottom
  const CREAM = ['#8f8a7a','#a8a294','#c2bcae','#cdc7b6','#d8d2c6','#eae6da'];             // house + cove line
  const DECKF = ['#2e2b26','#3a3630','#46443a','#5c5852','#74706a','#828884','#93907f'];  // taupe steel deck / bulwark liner
  const WOOD  = ['#3a2c20','#5a4634','#6f5840','#8c6a45','#a5825a'];                       // hatch coamings
  const GLAS  = ['#131c21','#213039','#33434e','#48657a','#6b91a1'];                       // window glass (sea-grey)
  const BUFF  = ['#6b4a22','#8c5e2c','#b06f32','#cf7a35','#e29a55','#f0b878'];             // ochre gantry / net drum / funnel
  const NET   = ['#050516','#0d0d2b','#1a1a44','#2b2b5e','#414178'];                       // navy trawl net
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2'];                       // winch drums, rails, aerials
  const IRON  = ['#0e1114','#171b21','#232a32','#333c46'];                                 // dark fittings, ramp plating
  const KEY   = '#0d1013';
  const MATS = { hull:{ramp:HULL,off:0}, boot:{ramp:BOOT,off:0}, cream:{ramp:CREAM,off:0},
                 deck:{ramp:DECKF,off:0}, wood:{ramp:WOOD,off:0}, glas:{ramp:GLAS,off:0}, buff:{ramp:BUFF,off:0},
                 net:{ramp:NET,off:0}, steel:{ramp:STEEL,off:0}, iron:{ramp:IRON,off:0},
                 blk:{ramp:BOOT,off:-1}, dark:{ramp:BOOT,off:-2} };
  const RINDEX = {}; [HULL,BOOT,CREAM,DECKF,WOOD,GLAS,BUFF,NET,STEEL,IRON].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m) ----
  // broad transom stern (ramp cut through it), full midbody, marked sheer to a high raked bow.
  // sheerZ = kz+depth: 4.75, 4.53, 4.44, 4.45, 4.50, 4.65, 5.08, 6.07, 7.15
  const T = [
    [3.40,2.40,4.30,0.45],   // 0 transom (ramp)
    [4.10,3.30,4.35,0.18],   // 1
    [4.40,3.80,4.40,0.04],   // 2
    [4.50,4.00,4.45,0.00],   // 3
    [4.50,4.00,4.50,0.00],   // 4 amidships (max beam 9 m)
    [4.40,3.80,4.65,0.00],   // 5
    [4.00,3.00,5.05,0.03],   // 6
    [3.00,1.50,5.85,0.22],   // 7 bow shoulder (flare)
    [0.16,0.07,6.60,0.55],   // 8 stem
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
  // paint, frac 0(keel)->1(sheer): black boot, grey steel topsides, cream cove line, grey sheer strake
  const OB = [ [0,0.34,'boot',-0.2,0], [0.34,0.915,'hull',0,0], [0.915,0.952,'cream',0.25,0.01], [0.952,1,'hull',0.12,0] ];

  // stern ramp
  const RW = 1.70, RYT = -13.6, RZ0 = 1.75;
  const rampZ=(y)=> RZ0 + (DECK-0.02-RZ0)*((y+L/2)/(RYT+L/2));
  // forward house envelope: lower block + wheelhouse looking AFT over the trawl deck
  const HXl = 3.45, HYa = 1.5, HYf = 9.55, HZ1l = 6.50;           // lower house
  const HXw = 2.90, WYa = 3.4,  FYb = 9.20, FYt = 8.75;           // wheelhouse (raked front toward the bow)
  const WZ0 = 6.56, WZ1 = 8.95, ROOFZ = 9.00;

  (function build(){
    // ---- hull shell ----
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],mat,b,db);
        // inner bulwark liner (taupe), deck line -> sheer — the open trawl deck + side decks, stop
        // before the bow flare (the inset collapses against the flare and z-fights)
        const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb);
        if(sa.y <= 9.4){
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
        const inb=(p)=>[p[0]-side*0.36,p[1],p[2]-0.004];
        face([oa,ob,inb(ib),inb(ia)],'deck',-1.2,0.03);
      }
    }
    // ---- trawl deck (taupe steel), stern -> foc'sle break; split around the ramp slot aft ----
    const SOLE_U = 0.755, U0 = 0.016, U_RAMP = (RYT+L/2)/L;
    const dw=(u)=>{ const st=station(u); return (lerp(st.wb,st.ws,dfrac(st))-TH)*0.97; };
    const ASEG=5;
    for(let i=0;i<ASEG;i++){   // beside the ramp: two side strips
      const u0=U0+(U_RAMP-U0)*i/ASEG, u1=U0+(U_RAMP-U0)*(i+1)/ASEG;
      const y0=station(u0).y, y1=station(u1).y;
      face([[-dw(u0),y0,DECK],[-RW,y0,DECK],[-RW,y1,DECK],[-dw(u1),y1,DECK]],'deck',-0.35);
      face([[RW,y0,DECK],[dw(u0),y0,DECK],[dw(u1),y1,DECK],[RW,y1,DECK]],'deck',-0.35);
    }
    const BSEG=15;
    for(let i=0;i<BSEG;i++){   // full-width deck forward of the ramp
      const u0=U_RAMP+(SOLE_U-U_RAMP)*i/BSEG, u1=U_RAMP+(SOLE_U-U_RAMP)*(i+1)/BSEG;
      face([[-dw(u0),station(u0).y,DECK],[dw(u0),station(u0).y,DECK],[dw(u1),station(u1).y,DECK],[-dw(u1),station(u1).y,DECK]],'deck',-0.35);
    }
    // ---- STERN RAMP: sloped wet-steel floor, side cheeks, lip roller ----
    (function(){
      const yA=-L/2, seg=[[yA,-17.2,-0.45],[-17.2,-15.4,-0.25],[-15.4,RYT,-0.45]];
      for(const [y0,y1,b] of seg)
        face([[-RW+0.06,y0,rampZ(y0)],[RW-0.06,y0,rampZ(y0)],[RW-0.06,y1,rampZ(y1)],[-RW+0.06,y1,rampZ(y1)]],'iron',b,-0.02);
      for(const s of [-1,1]){   // slot walls, floor -> deck
        const pts=[[s*RW,yA,rampZ(yA)],[s*RW,RYT,rampZ(RYT)],[s*RW,RYT,DECK+0.02],[s*RW,yA,DECK+0.02]];
        F.push(faceO(pts,[-s,0,0],'deck',s<0?-0.9:-0.6, -0.02));
        // cheek pieces up through the bulwark at the very stern
        F.push(faceO([[s*RW,yA,DECK],[s*RW,-17.4,DECK],[s*RW,yA,4.72]],[-s,0,0],'deck',s<0?-1.0:-0.7,-0.02));
      }
      tubeF([-1.55,-18.85,RZ0+0.12],[1.55,-18.85,RZ0+0.12],0.12,'steel',-0.2);   // lip roller
    })();
    // ---- transom: paint bands split around the ramp mouth ----
    (function(){
      const st0=station(0), sy=st0.y, xw=(f)=>lerp(st0.wb,st0.ws,f), zf=(f)=>st0.kz+f*st0.dep;
      const RF=(RZ0-st0.kz)/st0.dep;
      const band=(f0,f1,mat,b)=>{
        const z0=zf(f0), z1=zf(f1);
        if(f1<=RF){ F.push(faceO([[-xw(f0),sy,z0],[xw(f0),sy,z0],[xw(f1),sy,z1],[-xw(f1),sy,z1]],[0,-1,0],mat,b,0.005)); return; }
        if(f0<RF){ band(f0,RF,mat,b); f0=RF; }
        const za=zf(f0), zb=zf(f1);
        for(const s of [-1,1])
          F.push(faceO([[s*RW,sy,za],[s*xw(f0),sy,za],[s*xw(f1),sy,zb],[s*RW,sy,zb]],[0,-1,0],mat,b,0.005));
      };
      for(const [f0,f1,mat,b] of OB) band(f0,f1,mat,(b||0)-0.8);
      // covering board across the transom top, split at the slot
      for(const s of [-1,1])
        face([[s*RW,sy,4.75],[s*(st0.ws-TH),sy,4.75],[s*(st0.ws-TH),sy+0.30,4.746],[s*RW,sy+0.30,4.746]],'deck',-0.9,0.03);
    })();
    // quarter bollards
    for(const s of [-1,1]) boxF([s*2.6,-18.0,DECK+0.11],[0.07,0.09,0.11],'iron',0.1,-0.02);

    // ---- fish hatches: wood coaming + dark tarpaulin top ----
    const hatch=(y,hw,hl)=>{
      boxF([0,y,DECK+0.14],[hw,hl,0.14],'wood',-0.2);
      face([[-hw+0.06,y-hl+0.06,DECK+0.30],[hw-0.06,y-hl+0.06,DECK+0.30],[hw-0.06,y+hl-0.06,DECK+0.30],[-hw+0.06,y+hl-0.06,DECK+0.30]],'dark',-0.3,0.02);
    };
    hatch(-3.2,1.2,1.0); hatch(-6.4,1.2,1.0);

    // ---- SPLIT TRAWL WINCHES at the house, warps aft through the gantry down the ramp ----
    for(const s of [-1,1]){
      boxF([s*1.9,0.5,DECK+0.18],[0.95,0.50,0.18],'iron',0.0,-0.02);
      tubeF([s*0.85,0.5,DECK+0.70],[s*2.85,0.5,DECK+0.70],0.42,'steel',s<0?-0.1:-0.45);
      boxF([s*1.05,0.5,DECK+0.66],[0.16,0.30,0.28],'iron',0.1,-0.02);
      tubeF([s*1.35,0.5,4.35],[s*1.25,-14.3,7.28],0.022,'steel',-0.4);      // warp to the gantry block
      tubeF([s*1.25,-14.5,7.24],[s*0.95,-18.4,2.10],0.022,'steel',-0.5);    // warp down the ramp
    }

    // ---- NET DRUM (ochre, wound with navy net) + net tail heaped to the ramp head ----
    (function(){
      const DY=-11.3, DZ=4.75;
      for(const s of [-1,1]) boxF([s*2.35,DY,DECK+0.55],[0.16,0.44,0.55],'iron',-0.15,-0.02);   // stands
      tubeF([-2.35,DY,DZ],[2.35,DY,DZ],0.14,'steel',-0.2);                                      // axle
      tubeF([-2.20,DY,DZ],[-1.92,DY,DZ],0.75,'buff',-0.1);                                      // port flange
      tubeF([1.92,DY,DZ],[2.20,DY,DZ],0.75,'buff',-0.35);                                       // star flange
      tubeF([-1.90,DY,DZ],[1.90,DY,DZ],0.62,'net',-0.25);                                       // wound net
      boxF([-2.05,DY+0.62,DECK+0.42],[0.22,0.20,0.42],'iron',0.05,-0.02);                       // drive motor
      face([[-1.5,DY-0.65,4.45],[1.5,DY-0.65,4.45],[1.3,-13.35,DECK+0.16],[-1.3,-13.35,DECK+0.16]],'net',-0.3,-0.01);  // net tail
      boxF([0,-13.1,DECK+0.22],[1.30,0.62,0.22],'net',-0.45,-0.01);                             // heap at the ramp head
      for(const [fx,fy2] of [[-0.7,-12.9],[0.4,-13.3],[0.9,-12.8]]) boxF([fx,fy2,DECK+0.52],[0.10,0.10,0.08],'buff',0.3,-0.03); // floats
    })();

    // ---- PORTAL GANTRY straddling the ramp head ----
    (function(){
      const GY=-14.4;
      for(const s of [-1,1]){
        tubeF([s*3.00,GY,DECK],[s*2.55,GY,7.70],0.14,'buff',s<0?0.15:-0.35);        // legs (lean in)
        tubeF([s*2.90,-12.6,DECK],[s*2.58,GY+0.08,7.40],0.07,'buff',s<0?0.10:-0.30); // raked braces
        boxF([s*1.25,GY,7.30],[0.09,0.09,0.16],'iron',-0.2,-0.02);                   // hanging blocks
      }
      tubeF([-2.55,GY,7.70],[2.55,GY,7.70],0.14,'buff',-0.1);                        // cross beam
      boxF([0,GY,7.95],[0.10,0.10,0.10],'iron',0.1,-0.02);                           // stern light box
    })();

    // ---- DECKHOUSE lower block (cream), FORWARD ----
    F.push(faceO([[-HXl,HYf,HZ1l],[HXl,HYf,HZ1l],[HXl,HYf,DECK],[-HXl,HYf,DECK]],[0,1,0],'cream',0.3,0));
    F.push(faceO([[-HXl,HYa,HZ1l],[HXl,HYa,HZ1l],[HXl,HYa,DECK],[-HXl,HYa,DECK]],[0,-1,0],'cream',-0.6,0));
    F.push(faceO([[-HXl,HYa,HZ1l],[-HXl,HYf,HZ1l],[-HXl,HYf,DECK],[-HXl,HYa,DECK]],[-1,0,0],'cream',-0.1,0));
    F.push(faceO([[HXl,HYa,HZ1l],[HXl,HYf,HZ1l],[HXl,HYf,DECK],[HXl,HYa,DECK]],[1,0,0],'cream',-1.0,0));
    F.push(backPanel(HYa-0.03,-0.55,0.45,DECK+0.05,5.35,'dark',-0.5));               // deck door in the aft wall
    for(const xx of [-1.9,1.9]){                                                      // aft-wall portholes
      F.push(backPanel(HYa-0.03, xx-0.30, xx+0.30, 4.72, 5.38, 'iron', -0.15));
      F.push(backPanel(HYa-0.065, xx-0.22, xx+0.22, 4.80, 5.30, 'glas', -0.25));
    }
    for(const side of [-1,1]){                                                        // portholes ×4 + side door
      const P = side<0 ? leftPanel : rightPanel;
      for(const yy of [3.0,4.6,6.2,7.8]){
        F.push(P(side*(HXl+0.03), yy-0.28, yy+0.28, 4.62, 5.28, 'iron', -0.15));
        F.push(P(side*(HXl+0.065), yy-0.21, yy+0.21, 4.70, 5.20, 'glas', side<0?-0.15:-1.05));
      }
      F.push(P(side*(HXl+0.03), 1.95, 2.65, DECK+0.05, 5.35, 'dark', -0.5));
    }
    boxF([0,(HYa+HYf)/2,HZ1l+0.03],[HXl+0.10,(HYf-HYa)/2+0.08,0.06],'cream',0.5,-0.01); // boat-deck slab
    // boat-deck railing across the aft roof (around the funnel)
    (function(){ const rz=7.35, rx=3.30, ya=1.70, yf=3.25;
      tubeF([-rx,ya,rz],[rx,ya,rz],0.035,'steel',0.15);
      for(const s of [-1,1]){ tubeF([s*rx,ya,rz],[s*rx,yf,rz],0.035,'steel',s<0?0.15:-0.3);
        for(const yy of [ya+0.05,2.5,yf]) tubeF([s*rx,yy,HZ1l+0.09],[s*rx,yy,rz],0.022,'steel',-0.1); }
    })();
    // liferaft canisters on the boat deck sides
    for(const s of [-1,1]){
      boxF([s*3.05,4.85,6.70],[0.10,0.55,0.10],'iron',-0.1,-0.02);
      tubeF([s*3.05,4.30,6.88],[s*3.05,5.40,6.88],0.26,'cream',s<0?0.3:-0.2);
    }

    // ---- WHEELHOUSE (raked front toward the bow; aft trawl-watching windows over the deck) ----
    face([[-HXw,WYa,WZ0],[-HXw,WYa,WZ1],[-HXw,FYt,WZ1],[-HXw,FYb,WZ0]],'cream',-0.1);        // port
    face([[HXw,WYa,WZ0],[HXw,FYb,WZ0],[HXw,FYt,WZ1],[HXw,WYa,WZ1]],'cream',-1.0);            // starboard
    face([[-HXw,FYt,WZ1],[HXw,FYt,WZ1],[HXw,FYb,WZ0],[-HXw,FYb,WZ0]],'cream',0.4);           // raked front
    face([[HXw,WYa,WZ1],[-HXw,WYa,WZ1],[-HXw,WYa,WZ0],[HXw,WYa,WZ0]],'cream',-0.7);          // aft
    const _rny=(WZ1-WZ0), _rnz=(FYb-FYt), _rn=Math.hypot(_rny,_rnz), nY=_rny/_rn, nZ=_rnz/_rn;
    const yFront=(z)=> FYb + (FYt-FYb)*(z-WZ0)/(WZ1-WZ0);
    for(const [xa,xb] of [[-2.52,-1.72],[-1.52,-0.72],[-0.50,0.50],[0.72,1.52],[1.72,2.52]]) // 5-pane windscreen
      glaze((pr)=>((x,z)=>[x, yFront(z)+nY*pr, z+nZ*pr]), [0,nY,nZ], xa,xb, 7.45,8.45, 0.5, 0.05);
    const sideWin=(side, pts, glassB)=>{
      const cyg=pts.reduce((s,p)=>s+p[0],0)/pts.length, czg=pts.reduce((s,p)=>s+p[1],0)/pts.length;
      const trimPts=pts.map(([y,z])=>[y+(y-cyg)*0.12, z+(z-czg)*0.12]);
      F.push(faceO(trimPts.map(([y,z])=>[side*(HXw+0.03), y, z]), [side,0,0], 'iron', -0.15, DBP));
      F.push(faceO(pts.map(([y,z])=>[side*(HXw+0.065), y, z]),    [side,0,0], 'glas', glassB, DBP));
    };
    for(const side of [-1,1]){
      const b0=side<0?-0.15:-1.05;
      sideWin(side, [[7.70,7.45],[8.40,7.45],[8.40,8.35],[7.70,8.35]], b0);
      sideWin(side, [[6.70,7.45],[7.40,7.45],[7.40,8.35],[6.70,8.35]], b0);
      sideWin(side, [[5.70,7.45],[6.40,7.45],[6.40,8.35],[5.70,8.35]], b0);
    }
    // aft trawl-watching windows (the skipper works the ramp from here)
    for(const [xa,xb] of [[-2.35,-0.65],[0.65,2.35]]){
      F.push(backPanel(WYa-0.03, xa-0.06, xb+0.06, 7.39, 8.41, 'iron', -0.15));
      F.push(backPanel(WYa-0.065, xa, xb, 7.45, 8.35, 'glas', -0.25));
    }
    boxF([0,(WYa+FYt)/2-0.05,ROOFZ],[HXw+0.15,(FYt-WYa)/2+0.35,0.06],'cream',0.6,-0.01);     // roof (brow overhangs the screen)
    // roof gear: radar mast + scanner, whip aerials, horn, stays
    tubeF([0,5.40,ROOFZ+0.04],[0,5.30,11.90],0.10,'steel',0.1);
    tubeF([-0.90,5.35,10.90],[0.90,5.35,10.90],0.05,'steel',0.15);
    boxF([0,5.32,11.35],[0.42,0.14,0.09],'cream',0.5);
    boxF([0,5.30,11.97],[0.09,0.09,0.09],'iron',0.2,-0.02);
    for(const s of [-1,1]) tubeF([s*2.2,4.0,ROOFZ+0.04],[s*2.6,3.4,12.6],0.03,'steel',s<0?0.25:-0.2);
    boxF([0.9,8.55,ROOFZ+0.12],[0.10,0.22,0.08],'steel',0.2);
    tubeF([0,5.30,11.70],[0,19.9,7.10],0.022,'steel',-0.1);                                  // forestay to the stem head
    tubeF([0,5.35,11.60],[0,-14.3,7.90],0.022,'steel',-0.2);                                 // aft stay to the gantry

    // ---- FUNNEL (buff, black cap, raked aft) behind the wheelhouse ----
    tubeF([0,2.45,6.52],[0,2.15,8.55],0.60,'buff',-0.1);
    tubeF([0,2.15,8.55],[0,2.11,8.80],0.605,'hull',0.1);
    tubeF([0,2.11,8.78],[0,2.05,9.10],0.585,'blk',-0.3);

    // ---- foc'sle: raised whaleback deck following the sheer, break bulkhead at the house ----
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
      face([[-wv,y-0.36,z],[wv,y-0.36,z],[wv,yF,z],[-wv,yF,z]],'deck',0.4,-0.03);         // flat bridge to the raked deck edge
    })();
    // foc'sle furniture: anchor windlass + samson post + bow bollards
    (function(){ const zf=fz(0.868)+0.02;
      boxF([0,14.0,zf+0.18],[0.60,0.34,0.18],'iron',0.1,-0.02);
      tubeF([-0.65,14.0,zf+0.42],[0.65,14.0,zf+0.42],0.24,'steel',-0.1);
      boxF([0,fy(0.952),fz(0.952)+0.18],[0.06,0.07,0.18],'iron',0.15,-0.02);
      for(const s of [-1,1]) boxF([s*1.1,12.6,zf+0.12],[0.07,0.07,0.12],'iron',0.1,-0.02);
    })();
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
  const HELM = { x:0.30, y:7.60, z:6.62 };
  function helmSeat(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HELM.x, HELM.y, HELM.z, B);
    return { x:p.sx, y:p.sy };
  }
  const GANTRY = [ {x:-1.25,y:-14.4,z:7.30}, {x:1.25,y:-14.4,z:7.30} ];   // port + star block points over the ramp
  function gantryMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return GANTRY.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  const GALLOWS = GANTRY, HAULER = GANTRY[0];
  function gallowsMounts(dir, opts){ return gantryMounts(dir, opts); }
  function haulerMount(dir, opts){ return gantryMounts(dir, opts)[0]; }
  const DRUM = { x:0, y:-11.3, z:4.75 };
  function drumMount(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(DRUM.x, DRUM.y, DRUM.z, B);
    return { x:p.sx, y:p.sy };
  }
  // working-deck anchors (crew / tubs / catch piles), clear of hatches, winches, drum and the net
  const TUBS = [ {x:-2.4,y:-1.2,z:DECK}, {x:2.4,y:-1.2,z:DECK},
                 {x:-2.4,y:-8.8,z:DECK}, {x:2.4,y:-8.8,z:DECK}, {x:0,y:-9.4,z:DECK} ];
  function tubMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  // nav lights: sidelights on the wheelhouse sides, stern light on the gantry, masthead on the radar mast
  function navMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    const pt=(x,y,z)=>{ const p=projVert(x,y,z,B); return {x:p.sx,y:p.sy}; };
    return {
      port:  pt(-2.95,6.4,8.60),
      star:  pt( 2.95,6.4,8.60),
      stern: pt(0,-14.4,8.00),
      mast:  pt(0,5.30,12.00),
    };
  }

  root.SternTrawlerIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], HULL, BOOT, CREAM, DECKF, WOOD, GLAS, BUFF, NET, STEEL, IRON, KEY,
    render, ROCK, rock:rockMotion, helmSeat, HELM, gantryMounts, GANTRY, gallowsMounts, GALLOWS, haulerMount, HAULER,
    drumMount, DRUM, tubMounts, TUBS, navMounts };
})(typeof globalThis!=='undefined'?globalThis:window);
