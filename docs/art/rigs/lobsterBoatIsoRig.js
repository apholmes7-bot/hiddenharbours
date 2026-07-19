/* Hidden Harbours — parametric ISO Lobster Boat (M2 bake recipe, ADR-0006 — same pipeline as
   capeIslanderIsoRig.js / sportSkiffIsoRig.js / consoleIsoRig.js). PASS 1: full hull + aft wheelhouse +
   extended hardtop + radar arch. Tier 3, the shellfish specialist: ~12.0 m LOA modern Northumberland-Strait
   lobster boat ("Knuckles & Claws"). The NEW-generation lines, deliberately more modern than the Cape
   Islander: a beamy semi-displacement hull with a SLIGHTLY RAKED bow (stemhead carried forward of the
   forefoot), a springy CURVED sheer with a low hauling freeboard aft, an aft-set glassy wheelhouse with a
   near-vertical windscreen, and — the signatures — a CABIN ROOF EXTENDED AFT over the open working deck on
   two posts (a hardtop), and a STAINLESS ROOF ARCH carrying the radar dome, GPS pods, whip aerials and a
   light bar. White gelcoat topsides + near-black boot, twin blue stripes (waterline + cove), black rubrail,
   sea-grey glass, grey non-skid deck, stainless hardware — palette-clamped (KTC) to the Knuckles & Claws slice.
   Fixed 3/4 turntable camera (elev 40deg default, adjustable), 45deg steps, flat-facet shading from a fixed
   upper-left key, z-buffered, ordered dither, 1px keyline post-pass, NO AA. 32 px = 1 m.

   Single cell 456x420, pivot (228,258) = boat origin (amidships, keel bottom, centreline), pinned every
   heading so a direction swap never shifts placement. No outboard (inboard diesel). Deck anchors baked from
   day one: helmSeat(dir,opts) -> wheelhouse operator; haulerMount(dir,opts) -> starboard hauling block;
   tubMounts(dir,opts) -> cockpit crate anchors; navMounts(dir,opts) -> {port,star,stern,mast} nav-light
   points for the night bake. Pass the hull's rock(i) so overlays ride the wave.
   Exposes globalThis.LobsterBoatIso = { W,H,PX,DIRS,pivot,order,ROCK,rock(i),render(dir,opts),
   helmSeat,HELM, haulerMount,HAULER, tubMounts,TUBS, navMounts,
   HULL,BOOT,CREAM,DECKF,GLAS,BLUE,STEEL,IRON,KEY }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 456, H = 420, cx = 228, cy = 258;   // cell + pivot (projection of boat origin)
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const ROCK = { frames: 8, rollA: 2.8, pitchA: 1.6, heaveA: 1.2, period: 3.2 };  // beamy semi-displacement — livelier than the Cape but still weighty
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 12.0, TH = 0.05, DECK = 0.50;   // lower sole -> deeper cockpit / taller washboards
  const NSEG = 24;
  const RAKE = 0.50;   // forward rake of the bow (stemhead carried forward of the forefoot)

  // ---- palette ramps dark->light (KTC: sampled from the Knuckles & Claws slice) ----
  const HULL  = ['#7c848a','#9aa2a6','#b7bfbf','#d0d7d4','#e4e9e3','#f0f3ed','#f9fbf5'];  // white gelcoat topsides
  const BOOT  = ['#0a0d12','#10141b','#171d27','#212836','#2c3444'];                       // near-black boot / bottom
  const CREAM = ['#868e93','#a2aaae','#bfc6c6','#d6dbd7','#e8ebe5','#f2f4ee','#fbfcf6'];  // wheelhouse gelcoat (cooler white)
  const DECKF = ['#5f655f','#767c73','#8d9289','#a2a79d','#b6bbb0','#c8ccc2'];             // grey non-skid deck / washboards
  const GRIP  = ['#33372f','#3f4339','#4b4f44','#575b4f','#63675a','#6f7365'];             // darker grippy cockpit sole
  const GLAS  = ['#131c21','#213039','#33434e','#48657a','#6b91a1'];                       // window glass (sea-grey)
  const BLUE  = ['#0f2f57','#194d84','#2668a9','#3a81c6','#579ad9'];                       // waterline + cove stripes (accent)
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2','#e6edee'];             // stainless: arch, rails, aerials
  const IRON  = ['#0e1114','#171b21','#232a32','#333c46'];                                 // dark fittings
  const KEY   = '#0d1418';
  const MATS = { hull:{ramp:HULL,off:0}, boot:{ramp:BOOT,off:0}, cream:{ramp:CREAM,off:0},
                 deck:{ramp:DECKF,off:0}, grip:{ramp:GRIP,off:0}, glas:{ramp:GLAS,off:0}, blue:{ramp:BLUE,off:0},
                 steel:{ramp:STEEL,off:0}, iron:{ramp:IRON,off:0},
                 blk:{ramp:BOOT,off:-1}, dark:{ramp:BOOT,off:-2} };
  const RINDEX = {}; [HULL,BOOT,CREAM,DECKF,GRIP,GLAS,BLUE,STEEL,IRON].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m) ----
  // beamy hull, broad flat-ish transom aft; springy CURVED sheer (dips just fwd of the transom, sweeps up
  // to a high raked bow). sheerZ = keelZ+depth: 1.19,1.14,1.19,1.30,1.45,1.67,2.01,2.42,2.84
  const T = [
    [1.80,1.48,1.14,0.05],   // 0 transom (broad, near-flat run)
    [2.06,1.62,1.13,0.01],   // 1 (lowest freeboard — springy sheer)
    [2.18,1.66,1.19,0.00],   // 2
    [2.22,1.62,1.30,0.00],   // 3
    [2.20,1.50,1.45,0.00],   // 4 amidships (max beam)
    [2.04,1.24,1.66,0.01],   // 5
    [1.74,0.88,1.95,0.06],   // 6
    [1.20,0.44,2.24,0.18],   // 7 bow shoulder (flare)
    [0.12,0.05,2.44,0.40],   // 8 stem
  ];
  const lerp = (a,b,t)=>a+(b-a)*t;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), wb:lerp(A[1],B[1],fr), dep:lerp(A[2],B[2],fr),
             kz:lerp(A[3],B[3],fr), y:-L/2+u*L };
  }
  // forward bow rake: shifts the forward hull forward (positive y), more near the top (frac->1) so the
  // stemhead leans out over the forefoot. Zero aft of u~0.60, so house/cockpit/transom are untouched.
  function bowRake(u,frac){ const t=Math.max(0,(u-0.60)/0.40), s=t*t*(3-2*t); return RAKE*s*(0.30+0.70*frac); }
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
      f([P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1)]),      // +z top
      f([P(-1,1,-1),P(1,1,-1),P(1,-1,-1),P(-1,-1,-1)]),  // -z bottom
      f([P(-1,1,1),P(1,1,1),P(1,1,-1),P(-1,1,-1)]),      // +y front
      f([P(1,-1,1),P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1)]),  // -y back
      f([P(1,1,1),P(1,-1,1),P(1,-1,-1),P(1,1,-1)]),      // +x right
      f([P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1)]),  // -x left
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
  // proud axis-aligned panels (outward normal along the named axis) for house glazing/joinery
  const DBP = 0.05;
  const frontPanel=(y,xa,xb,za,zb,mat,b)=>({v:[[xa,y,zb],[xb,y,zb],[xb,y,za],[xa,y,za]],mat,b:b||0,db:DBP});
  const backPanel =(y,xa,xb,za,zb,mat,b)=>({v:[[xb,y,zb],[xa,y,zb],[xa,y,za],[xb,y,za]],mat,b:b||0,db:DBP});
  const rightPanel=(x,ya,yb,za,zb,mat,b)=>({v:[[x,yb,zb],[x,ya,zb],[x,ya,za],[x,yb,za]],mat,b:b||0,db:DBP});
  const leftPanel =(x,ya,yb,za,zb,mat,b)=>({v:[[x,ya,zb],[x,yb,zb],[x,yb,za],[x,ya,za]],mat,b:b||0,db:DBP});

  // ---- face list ----
  const F = [];
  const face=(v,mat,b,db)=>F.push({v,mat:mat||'hull',b:b||0,db:db||0});
  const boxF=(c,h,mat,b,db)=>{ F.push.apply(F, box(c,h,mat,b,db)); };
  const tubeF=(A,B2,rad,mat,b)=>{ F.push.apply(F, tube(A,B2,rad,mat,b)); };
  // ---- rounded-corner glazing: octagon glass + dark trim frame, auto-oriented to any face ----
  function objNormal(a,b,c){ const ux=b[0]-a[0],uy=b[1]-a[1],uz=b[2]-a[2], vx=c[0]-a[0],vy=c[1]-a[1],vz=c[2]-a[2];
    return [uy*vz-uz*vy, uz*vx-ux*vz, ux*vy-uy*vx]; }
  function faceO(v, outward, mat, b, db){ const n=objNormal(v[0],v[1],v[2]);
    if(n[0]*outward[0]+n[1]*outward[1]+n[2]*outward[2] < 0) v=v.slice().reverse();
    return {v, mat, b:b||0, db:(db==null?DBP:db)}; }
  function rrect(ua,ub,va,vb,c){ return [[ua+c,va],[ub-c,va],[ub,va+c],[ub,vb-c],[ub-c,vb],[ua+c,vb],[ua,vb-c],[ua,va+c]]; }
  function winRR(mapUV, outward, ua,ub,va,vb, cut, mat, b){
    return faceO(rrect(ua,ub,va,vb,cut).map(([u,v])=>mapUV(u,v)), outward, mat, b); }
  function glaze(mk, outward, ua,ub,va,vb, glassB, cut){ cut=cut||0.10;
    F.push(winRR(mk(0.03),  outward, ua-0.06,ub+0.06, va-0.055,vb+0.055, cut+0.03, 'iron', -0.15));  // dark trim frame
    F.push(winRR(mk(0.065), outward, ua,ub, va,vb, cut, 'glas', glassB));                            // rounded glass
  }
  // outer paint scheme, frac 0(keel)->1(sheer): near-black boot, blue waterline stripe, white topsides,
  // blue cove stripe, black rubrail at the sheer
  const OB = [ [0,0.27,'boot',-0.2,0], [0.27,0.315,'blue',0.2,0.01], [0.315,0.90,'hull',0,0],
               [0.90,0.945,'blue',0.28,0.01], [0.945,1,'dark',-0.25,0.006] ];

  // wheelhouse envelope: set well FORWARD, tapered inboard toward the bow, taller so windows clear the sheer
  const HYaft = 0.55, HYfwd = 3.60, HZ0 = DECK, HZ1 = 2.90;    // aft y, fwd y, floor, eaves (widths in build)
  const ROOFZ = 2.96;
  const EXT_AFT = -1.55;   // hardtop extends aft to here over the cockpit

  (function build(){
    // house plan taper (up-front so the side decks can meet the house wall)
    const HXa=1.50, HXf=1.08;
    const HXat=(y)=>{ const t=Math.max(0,Math.min(1,(y-HYaft)/(HYfwd-HYaft))); return HXa+(HXf-HXa)*t; };
    // ---- hull shell ----
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],mat,b,db);
        // inner bulwark liner (white), deck line -> sheer, cockpit only (aft of the house front)
        const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb);
        if(sa.y <= HYaft+0.2){
          const LT=0.95;
          for(let k=0;k<2;k++){
            const g0a=fa+(LT-fa)*k/2, g1a=fa+(LT-fa)*(k+1)/2;
            const g0b=fb+(LT-fb)*k/2, g1b=fb+(LT-fb)*(k+1)/2;
            face([skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'cream',-1.5,-0.03);
          }
        }
        // bottom (boot)
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'boot',-1.0);
        // covering board — light grey rail cap, runs the full sheer both sides
        const oa=skin(side,u0,1),ob=skin(side,u1,1),ia=skin(side,u0,1,1),ib=skin(side,u1,1,1);
        const inb=(p)=>[p[0]-side*0.30,p[1],p[2]-0.004];
        face([oa,ob,inb(ib),inb(ia)],'deck',-1.2,0.03);
      }
    }
    // ---- cockpit sole: white margin (full width) + darker grippy non-skid panel inset from the walls ----
    const SOLE_U = 0.80;
    const DSEG=20, BORD=0.24;
    const dw=(u)=>{ const st=station(u); return (lerp(st.wb,st.ws,dfrac(st))-TH)*0.96; };
    for(let i=0;i<DSEG;i++){
      const u0=SOLE_U*i/DSEG, u1=SOLE_U*(i+1)/DSEG;
      face([[-dw(u0),station(u0).y,DECK],[dw(u0),station(u0).y,DECK],[dw(u1),station(u1).y,DECK],[-dw(u1),station(u1).y,DECK]],'cream',-0.3);   // white border base
    }
    const gA=station(0).y+0.34, gF=HYaft-0.12;                 // grippy panel: transom -> house, inset for a white border
    for(let i=0;i<DSEG;i++){
      const u0=SOLE_U*i/DSEG, u1=SOLE_U*(i+1)/DSEG, y0=station(u0).y, y1=station(u1).y;
      if(y1<gA || y0>gF) continue;
      const w0=Math.max(0,dw(u0)-BORD), w1=Math.max(0,dw(u1)-BORD);
      face([[-w0,y0,DECK+0.006],[w0,y0,DECK+0.006],[w1,y1,DECK+0.006],[-w1,y1,DECK+0.006]],'grip',-0.25,0.02);
    }
    // flush in-floor stainless deck hatches: dark deck-mounted frame + two-tone brushed lid + lift handle
    const hatch=(x,y,w,l,handle)=>{
      const z=DECK;
      face([[x-w,y-l,z+0.008],[x+w,y-l,z+0.008],[x+w,y+l,z+0.008],[x-w,y+l,z+0.008]],'iron',0.0,0.03);   // frame ring mounted to the deck
      const iw=w-0.05, il=l-0.05;
      face([[x-iw,y-il,z+0.014],[x,y-il,z+0.014],[x,y+il,z+0.014],[x-iw,y+il,z+0.014]],'steel',-3.5,0.05); // lid — darker brushed stainless, half A
      face([[x,y-il,z+0.014],[x+iw,y-il,z+0.014],[x+iw,y+il,z+0.014],[x,y+il,z+0.014]],'steel',-2.7,0.05);  // lid — darker brushed stainless, half B
      if(handle) boxF([x, y-il+0.07, z+0.03],[0.12,0.022,0.022],'steel',-1.7,0.07);                         // stainless lift handle (catch light)
    };
    hatch(0,-1.15,0.56,0.44,true); hatch(0,-2.55,0.56,0.44,true); hatch(0,-3.95,0.50,0.42,true);
    hatch(-1.00,-1.85,0.30,0.32,false); hatch(1.00,-1.85,0.30,0.32,false);
    // side decks: continuous the full length — washboards around the cockpit, narrowing to the house wall
    // alongside the house, then meeting the foredeck so there is no gap at the wheelhouse front
    const WB=0.44;
    const innerX=(st)=>{ if(st.y > HYaft-0.05) return Math.min(st.ws-TH-0.10, HXat(st.y)); return st.ws-TH-WB; };
    for(const side of [-1,1]){
      for(let i=0;i<DSEG;i++){
        const u0=SOLE_U*i/DSEG, u1=SOLE_U*(i+1)/DSEG;
        const sa=station(u0), sb=station(u1);
        const xo0=side*(sa.ws-TH), xi0=side*innerX(sa), z0=sa.kz+sa.dep-0.02;
        const xo1=side*(sb.ws-TH), xi1=side*innerX(sb), z1=sb.kz+sb.dep-0.02;
        const q = side>0 ? [[xi0,sa.y,z0],[xo0,sa.y,z0],[xo1,sb.y,z1],[xi1,sb.y,z1]]
                         : [[xo0,sa.y,z0],[xi0,sa.y,z0],[xi1,sb.y,z1],[xo1,sb.y,z1]];
        face(q,'hull',-0.6);
      }
    }
    // ---- transom: paint bands carried across ----
    const tp=(s,f)=>skin(s,0,f);
    for(const [f0,f1,mat,b] of OB)
      face([tp(-1,f1),tp(1,f1),tp(1,f0),tp(-1,f0)], mat, (b||0)-0.8, 0.005);
    (function(){ const s0=station(0), zt=s0.kz+s0.dep, wsx=s0.ws-TH;   // covering board across the transom top
      face([[-wsx,s0.y,zt],[wsx,s0.y,zt],[wsx,s0.y+0.26,zt-0.004],[-wsx,s0.y+0.26,zt-0.004]],'deck',-0.9,0.03); })();
    // ---- foredeck (white gelcoat), following the raked sheer, forward of the house ----
    const FSEG=8, DROP=0.05, FCAP=0.985;
    const fz=(u)=>{ const st=station(u); return st.kz+st.dep-DROP; };
    const fw=(u)=>{ const st=station(u); return Math.max(0.02, st.ws-0.30); };   // full-width flush foredeck (to the covering board)
    const fy=(u)=> station(u).y + bowRake(u,1);
    for(let i=0;i<FSEG;i++){
      const u0=SOLE_U+(FCAP-SOLE_U)*i/FSEG, u1=SOLE_U+(FCAP-SOLE_U)*(i+1)/FSEG;
      face([[-fw(u0),fy(u0),fz(u0)],[fw(u0),fy(u0),fz(u0)],[fw(u1),fy(u1),fz(u1)],[-fw(u1),fy(u1),fz(u1)]],'hull',0.5,-0.02);
    }
    (function(){ const u=SOLE_U, wv=fw(u), z=fz(u), y=station(u).y, yF=fy(u), st=station(u);   // bulkhead riser + flat bridge — closes the gap between the raked foredeck aft edge and the windscreen base
      const hw=(zz)=>{ const fr=Math.max(0,Math.min(1,(zz-st.kz)/st.dep)); return lerp(st.wb,st.ws,fr)-TH; };  // hull half-width at this station, per height
      const wTop=Math.min(wv,hw(z)), wDeck=hw(DECK);   // V-taper: narrow at the sole, wider at the sheer — stays inside the hull
      face([[-wTop,y,z],[wTop,y,z],[wDeck,y,DECK],[-wDeck,y,DECK]],'cream',-1.4,-0.03);   // V bulkhead face at the house front
      face([[-wv,y-0.36,z],[wv,y-0.36,z],[wv,yF,z],[-wv,yF,z]],'hull',0.5,-0.03); // flat foredeck bridge — reaches aft under the windscreen base
    })();
    // ---- small samson post at the stem (no pulpit rail — clean Novi foredeck) ----
    (function(){ const u=0.93, y=fy(u), z=fz(u);
      boxF([0, y, z+0.09],[0.035,0.05,0.09],'iron',0.15,-0.02); })();   // samson post / bow bitt
    // ---- (engine box removed — flat working deck with the flush hatches above) ----
    // ---- stern cleats ----
    for(const s of [-1,1]) boxF([s*(station(0).ws-0.22),-5.72,station(0).kz+station(0).dep+0.03],[0.05,0.09,0.05],'iron',0.15,-0.02);

    // ---- WHEELHOUSE: tapered inboard (HXat, defined at the top of build), reclined windscreen, raised ----
    const FYb=HYfwd, FYt=HYfwd-0.52;                           // front wall bottom vs top y (reclined)
    const SWZa=1.38, SWZf=2.02;                                // wall bottom line: tucked just under the side decks / foredeck so nothing hangs below the sheer
    const FYf=FYb+(FYt-FYb)*(SWZf-HZ0)/(HZ1-HZ0);              // front-bottom y kept on the windscreen recline plane
    const AL=[-HXa,HYaft,HZ0], AR=[HXa,HYaft,HZ0], ALt=[-HXa,HYaft,HZ1], ARt=[HXa,HYaft,HZ1];
    const ALs=[-HXa,HYaft,SWZa], ARs=[HXa,HYaft,SWZa];
    const FLb=[-HXf,FYf,SWZf], FRb=[HXf,FYf,SWZf], FLt=[-HXf,FYt,HZ1], FRt=[HXf,FYt,HZ1];
    face([ALs,ALt,FLt,FLb],'cream',-0.1);                      // port wall (tapered, bottom rides the deck line)
    face([ARs,FRb,FRt,ARt],'cream',-1.0);                      // starboard wall (tapered, shaded)
    face([FLt,FRt,FRb,FLb],'cream',0.4);                       // front wall — windscreen band standing on the foredeck
    face([AL,AR,ARt,ALt],'cream',-0.7);                        // aft wall (into cockpit, full height to the sole)
    const _rny=(HZ1-HZ0), _rnz=(FYb-FYt), _rn=Math.hypot(_rny,_rnz), nY=_rny/_rn, nZ=_rnz/_rn;
    const yFront=(z)=> FYb + (FYt-FYb)*(z-HZ0)/(HZ1-HZ0);
    // three-pane reclined windscreen (sits above the raised foredeck it butts onto)
    for(const [xa,xb] of [[-0.98,-0.48],[-0.32,0.32],[0.48,0.98]])
      glaze((pr)=>((x,z)=>[x, yFront(z)+nY*pr, z+nZ*pr]), [0,nY,nZ], xa,xb, 2.16,2.74, 0.5, 0.05);
    // side windows — FORWARD trapezoid (leading edge sloped parallel to the windscreen, straight top/bottom,
    // vertical trailing edge) + two RECTANGULAR aft lights; sills sit above the sheer so the hull never crops
    // them. pts=[y,z] corners on the tapered wall (x from HXat(y)); glass sits proud of a dark trim frame.
    const sideWin=(side, pts, glassB)=>{
      const cyg=pts.reduce((s,p)=>s+p[0],0)/pts.length, czg=pts.reduce((s,p)=>s+p[1],0)/pts.length;
      const trimPts=pts.map(([y,z])=>[y+(y-cyg)*0.12, z+(z-czg)*0.12]);
      F.push(faceO(trimPts.map(([y,z])=>[side*(HXat(y)+0.03), y, z]), [side,0,0], 'iron', -0.15, DBP));
      F.push(faceO(pts.map(([y,z])=>[side*(HXat(y)+0.065), y, z]),    [side,0,0], 'glas', glassB, DBP));
    };
    for(const side of [-1,1]){
      const b0=side<0?-0.15:-1.05;
      sideWin(side, [[3.16,2.14],[3.05,2.66],[2.36,2.66],[2.36,2.14]], b0);   // fwd trapezoid (sloped front)
      sideWin(side, [[2.20,2.14],[1.66,2.14],[1.66,2.66],[2.20,2.66]], b0);   // aft light 1 — tall, top level with the canopy line
      sideWin(side, [[1.52,2.14],[0.98,2.14],[0.98,2.66],[1.52,2.66]], b0);   // aft light 2 — equal size, same band
    }
    // aft face: door opening (dark) + small stbd light, on the cockpit side (proud)
    const AY=HYaft-0.03;
    F.push(backPanel(AY,-0.40,0.34, HZ0+0.02,2.36,'dark',-0.5));         // doorway (open, dark)
    glaze((pr)=>((x,z)=>[x, AY-pr, z]), [0,-1,0], 0.56,HXa-0.14, 1.95,2.55, -0.25, 0.05);  // small aft light

    // ---- EXTENDED HARDTOP: roof slab reaches aft over the cockpit, on two posts ----
    const RHX = HXa+0.28;                                      // roof half-width (overhangs the gunwale to cover the posts)
    const RYf = FYt+0.16, RYa = EXT_AFT;                        // roof fwd lip (fwd of the reclined brow) -> aft over cockpit
    boxF([0,(RYf+RYa)/2,ROOFZ+0.045],[RHX,(RYf-RYa)/2,0.05],'cream',0.6,-0.01);   // main roof slab (extends aft)
    boxF([0,RYf-0.02,ROOFZ-0.02],[RHX-0.04,0.05,0.05],'cream',0.2);               // front visor lip
    // two VERTICAL support posts at the aft corners of the hardtop — feet planted on the washboard cap
    (function(){ const pst=station((EXT_AFT+0.12+L/2)/L); const capZ=pst.kz+pst.dep-0.02;
      const footX=(pst.ws-TH)-0.40;   // inner washboard, tucked under the roof overhang
      for(const s of [-1,1]) tubeF([s*footX, EXT_AFT+0.12, capZ],[s*footX, EXT_AFT+0.12, ROOFZ],0.055,'steel',0.1); })();
    // aft edge grab rail under the hardtop
    tubeF([-(RHX-0.12), EXT_AFT+0.10, ROOFZ-0.06],[ (RHX-0.12), EXT_AFT+0.10, ROOFZ-0.06],0.035,'steel',0.2);

    // ---- STAINLESS ROOF ARCH (radar + aerials + nav gear) mounted at the aft of the house roof ----
    const ARY = HYaft+1.30;                                    // arch fore-aft station — centred over the wheelhouse cabin roof
    const ALX = HXa-0.06, ATX = 1.02, ATZ = 3.92;              // leg foot x, top x, top z
    tubeF([-ALX, ARY, ROOFZ+0.02],[-ATX, ARY-0.05, ATZ],0.06,'steel',0.15);   // port leg (rakes in + slightly aft)
    tubeF([ ALX, ARY, ROOFZ+0.02],[ ATX, ARY-0.05, ATZ],0.06,'steel',-0.4);   // starboard leg (shaded)
    tubeF([-ATX, ARY-0.05, ATZ],[ ATX, ARY-0.05, ATZ],0.055,'steel',0.25);    // top crossbar
    tubeF([-ATX+0.04, ARY-0.05, ATZ-0.02],[-ATX+0.04, ARY+0.55, ATZ-0.24],0.04,'steel',0.1);  // port fore stay
    tubeF([ ATX-0.04, ARY-0.05, ATZ-0.02],[ ATX-0.04, ARY+0.55, ATZ-0.24],0.04,'steel',-0.3); // stbd fore stay
    // light bar across the front of the arch, a little lower
    tubeF([-0.94, ARY+0.16, 3.52],[0.94, ARY+0.16, 3.52],0.045,'steel',0.2);
    for(const x of [-0.6,-0.2,0.2,0.6]) boxF([x, ARY+0.20, 3.52],[0.07,0.03,0.05],'glas',0.4,-0.05);  // flood lenses
    // radar dome (squat drum) centred on the crossbar
    tubeF([0, ARY-0.05, ATZ+0.03],[0, ARY-0.05, ATZ+0.22],0.33,'cream',0.15);
    boxF([0, ARY-0.05, ATZ+0.24],[0.33,0.26,0.03],'cream',0.5);
    // GPS / satcom pods flanking the dome
    for(const x of [-0.66,0.66]) tubeF([x, ARY-0.05, ATZ+0.02],[x, ARY-0.05, ATZ+0.16],0.10,'cream',0.3);
    // whip aerials — tall thin stainless rods, splayed slightly out and aft
    tubeF([-0.80, ARY-0.02, ATZ],[-1.00, ARY-0.34, 5.95],0.033,'steel',0.3);
    tubeF([ 0.80, ARY-0.02, ATZ],[ 1.00, ARY-0.34, 5.95],0.033,'steel',0.3);
    boxF([0, ARY-0.05, ATZ+0.02],[0.05,0.05,0.05],'iron',0.2,-0.02);   // masthead anchor light base

    // ---- small stainless side exhaust at the starboard aft corner of the house ----
    tubeF([HXa-0.02, HYaft+0.20, 1.10],[HXa+0.16, HYaft+0.20, 1.10],0.055,'steel',0.2);
    // ---- hauling block on the starboard washboard, just aft of the house ----
    (function(){ const st=station((-1.5+L/2)/L), z=st.kz+st.dep; boxF([1.34,st.y,z+0.10],[0.10,0.12,0.10],'iron',0.2);
      tubeF([1.22,st.y,z+0.14],[1.46,st.y,z+0.14],0.05,'steel',0.3); })();  // roller
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

  // ---- deck anchors (cell coords; pass rock(i) so they ride the wave) ----
  const HELM = { x:0, y:1.05, z:DECK+0.02 };                  // skipper at the wheel, forward in the house
  function helmSeat(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HELM.x, HELM.y, HELM.z, B);
    return { x:p.sx, y:p.sy };
  }
  const HAULER = (function(){ const st=station((-1.5+L/2)/L); return { x:1.34, y:st.y, z:st.kz+st.dep+0.14 }; })();
  function haulerMount(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HAULER.x, HAULER.y, HAULER.z, B);
    return { x:p.sx, y:p.sy };
  }
  // cockpit crate anchors (boat-local; two rows aft of the house + one at the transom quarter)
  const TUBS = [ {x:-0.82,y:-2.4,z:DECK}, {x:0.82,y:-2.4,z:DECK},
                 {x:-0.82,y:-3.7,z:DECK}, {x:0.82,y:-3.7,z:DECK}, {x:0,y:-5.0,z:DECK} ];
  function tubMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  // nav-light points for the night bake: bow port/star sidelights, stern light, masthead (top of arch)
  function navMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), s7=station(0.95), s0=station(0);
    const pt=(x,y,z)=>{ const p=projVert(x,y,z,B); return {x:p.sx,y:p.sy}; };
    return {
      port:  pt(-(s7.ws-0.10), s7.y+bowRake(0.95,1)-0.2, s7.kz+s7.dep+0.06),
      star:  pt( (s7.ws-0.10), s7.y+bowRake(0.95,1)-0.2, s7.kz+s7.dep+0.06),
      stern: pt(0, s0.y+0.05, s0.kz+s0.dep+0.10),
      mast:  pt(0, HYaft+1.30, 3.92),
    };
  }

  root.LobsterBoatIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], HULL, BOOT, CREAM, DECKF, GLAS, BLUE, STEEL, IRON, KEY,
    render, ROCK, rock:rockMotion, helmSeat, HELM, haulerMount, HAULER, tubMounts, TUBS, navMounts };
})(typeof globalThis!=='undefined'?globalThis:window);
