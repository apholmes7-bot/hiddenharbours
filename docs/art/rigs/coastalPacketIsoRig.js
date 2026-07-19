/* Hidden Harbours — parametric ISO Coastal Packet (M2 bake recipe, ADR-0006 — same pipeline as
   sternTrawlerMk2IsoRig.js / sideDraggerIsoRig.js). Tier 6, the first merchant hull: ~60 m LOA
   COASTAL PACKET, built to the reference photos — a small blue coastal freighter with the whole
   white HOUSE AFT (three levels: accommodation, boat deck, glassy wheelhouse with full-beam bridge
   wings), one LONG CARGO HOLD forward under mint-green folding hatch covers on a white coaming,
   open white rails along the sheer the length of the hold, a raised foc'sle with windlass and a
   tall white foremast at the hold's forward end, a small blue deck crane at the house front, a
   black exhaust scoop on the bridge deck, and below the water a red-brown antifouling bottom with
   a LIME boot line and a lime BULBOUS BOW poking forward at the stem. Palette sampled from the
   reference photos (the fleet slice CoastalPacket.png is the green SLICE PLACEHOLDER; per
   ADR-0006 the bake supersedes it). Fixed 3/4 turntable camera (elev 40deg default, adjustable),
   45deg steps, flat-facet shading from a fixed upper-left key, z-buffered, ordered dither, 1px
   keyline post-pass, NO AA. 32 px = 1 m.

   Single cell 2112x1760, pivot (1056,976) = boat origin (amidships, keel bottom, centreline),
   pinned every heading. Deck anchors baked from day one: helmSeat(dir,opts) -> wheelhouse skipper;
   craneMounts(dir,opts) -> [pedestal top, jib head]; hatchMount(dir,opts) -> hold centre (laden /
   cargo overlays); tubMounts(dir,opts) -> walkway + foredeck anchors; navMounts(dir,opts) ->
   {port,star,stern,mast} for the night bake (sidelights ride the bridge-wing ends). Pass the
   hull's rock(i) so overlays ride the wave. Exposes globalThis.CoastalPacketIso = { W,H,PX,DIRS,
   pivot,order,ROCK,rock(i),render(dir,opts), helmSeat,HELM, craneMounts,CRANE, hatchMount,HOLD,
   tubMounts,TUBS, navMounts, HULL,BOOT,LIME,WHITE,DECKF,MINT,GLAS,STEEL,IRON,ORNG,KEY }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 2112, H = 1760, cx = 1056, cy = 976;   // cell + pivot (projection of boat origin)
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const ROCK = { frames: 8, rollA: 1.3, pitchA: 0.65, heaveA: 1.2, period: 6.0 };  // 60 m laden coaster — long, slow, stately
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 60.0, TH = 0.10, DECK = 5.0;
  const NSEG = 24;
  const RAKE = 1.60;   // raked merchant stem

  // ---- palette ramps dark->light (sampled from the reference photos, KTC-clamped) ----
  const HULL  = ['#0e1626','#152547','#1c356a','#25478e','#3159ae','#4b77c6','#7095d6'];  // royal-blue topsides
  const BOOT  = ['#23100b','#341811','#482017','#5c2b1c','#713a24'];                       // red-brown antifouling bottom
  const LIME  = ['#3f6b28','#5c8f35','#7fb24a'];                                           // lime boot line + bulb
  const WHITE = ['#878b85','#a2a7a0','#bcc2ba','#cfd4cc','#dfe3dc','#eff2ec'];             // house + coaming + rails
  const DECKF = ['#233028','#2e3d31','#3c4d3e','#4c5f4c','#5f735e','#748672','#8a9a86'];  // green steel deck / bulwark liner
  const MINT  = ['#48685c','#5f8272','#7a9d88','#96b8a0','#b0ceb6','#c6dfca'];             // mint hatch covers
  const GLAS  = ['#131c21','#213039','#33434e','#48657a','#6b91a1'];                       // window glass (sea-grey)
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2'];                       // rails, masts, winch drums
  const IRON  = ['#0e1114','#171b21','#232a32','#333c46'];                                 // dark fittings, exhaust, doors
  const ORNG  = ['#833c14','#ad541c','#d4732b','#ee9a4a'];                                 // life rings
  const KEY   = '#0d1013';
  const MATS = { hull:{ramp:HULL,off:0}, boot:{ramp:BOOT,off:0}, lime:{ramp:LIME,off:0},
                 white:{ramp:WHITE,off:0}, deck:{ramp:DECKF,off:0}, mint:{ramp:MINT,off:0},
                 glas:{ramp:GLAS,off:0}, steel:{ramp:STEEL,off:0}, iron:{ramp:IRON,off:0},
                 orng:{ramp:ORNG,off:0}, blk:{ramp:IRON,off:-1}, dark:{ramp:IRON,off:-2} };
  const RINDEX = {}; [HULL,BOOT,LIME,WHITE,DECKF,MINT,GLAS,STEEL,IRON,ORNG].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m) ----
  // flat transom, long full midbody (box holds), marked sheer to a high flared bow.
  const T = [
    [3.60,2.60,4.90,0.40],   // 0 transom
    [4.60,3.90,4.95,0.12],   // 1
    [5.00,4.60,5.00,0.02],   // 2
    [5.20,4.90,5.00,0.00],   // 3
    [5.20,4.90,5.00,0.00],   // 4 amidships (max beam 10.4 m)
    [5.20,4.80,5.05,0.00],   // 5
    [5.00,4.20,5.25,0.00],   // 6
    [4.20,1.80,5.90,0.10],   // 7 bow shoulder — full in plan, heavy flare
    [0.22,0.05,6.90,0.50],   // 8 stem
  ];
  const lerp = (a,b,t)=>a+(b-a)*t;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), wb:lerp(A[1],B[1],fr), dep:lerp(A[2],B[2],fr),
             kz:lerp(A[3],B[3],fr), y:-L/2+u*L };
  }
  function bowRake(u,frac){ const t=Math.max(0,(u-0.62)/0.38), s=t*t*(3-2*t); return RAKE*s*(0.30+0.70*frac); }
  function flareExp(u){ const t=Math.max(0,(u-0.52)/0.48), s=t*t*(3-2*t); return 1+1.9*s; }
  function skin(side,u,frac,inset){
    const st=station(u);
    const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
    return [ side*lerp(wb,ws,Math.pow(frac,flareExp(u))), st.y+bowRake(u,frac), st.kz+lerp(0,dep,frac) ];
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

  // paint, frac 0(keel)->1(sheer): red-brown bottom, LIME boot line, royal blue to the rail
  const OB = [ [0,0.29,'boot',-0.2,0], [0.29,0.345,'lime',0.1,0.01], [0.345,1,'hull',0,0] ];

  // house envelope (AFT): three white levels + wheelhouse + full-beam bridge wings
  const H1X=4.20, H1A=-28.6, H1F=-17.8, H1T=7.60;      // level 1: accommodation on the main deck
  const H2X=3.85, H2A=-28.2, H2F=-18.3, H2T=9.95;      // level 2
  const WHX=3.25, WHA=-27.8, WHF=-19.8, WHZ=10.0, WHT=12.50, ROOFZ=12.56;  // wheelhouse
  const WGY0=-21.6, WGY1=-19.8, WGX=5.00;              // bridge wings (full beam, at the WH front)
  // cargo hold trunk
  const CY0=-15.5, CY1=18.5, CXW=3.55, COAMT=6.10;

  (function build(){
    // ---- hull shell ----
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG;
        for(const [f0,f1,mat,b,db] of OB)
          face([skin(side,u0,f0),skin(side,u1,f0),skin(side,u1,f1),skin(side,u0,f1)],mat,b,db);
        // inner bulwark liner (green steel), deck line -> sheer, stop before the bow flare
        const sa=station(u0), sb=station(u1), fa=dfrac(sa), fb=dfrac(sb);
        if(sa.y <= 19.2){
          const LT=0.95;
          for(let k=0;k<2;k++){
            const g0a=fa+(LT-fa)*k/2, g1a=fa+(LT-fa)*(k+1)/2;
            const g0b=fb+(LT-fb)*k/2, g1b=fb+(LT-fb)*(k+1)/2;
            face([skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'deck',-1.5,-0.03);
          }
        }
        // bottom (antifouling)
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'boot',-1.0);
        // covering board — white rail cap both sides, full sheer
        const oa=skin(side,u0,1),ob=skin(side,u1,1),ia=skin(side,u0,1,1),ib=skin(side,u1,1,1);
        const inb=(p)=>[p[0]-side*0.36,p[1],p[2]-0.004];
        face([oa,ob,inb(ib),inb(ia)],'white',-0.9,0.03);
      }
    }
    // ---- lime bulbous bow at the stem ----
    tubeF([0,28.9,0.85],[0,31.35,0.85],0.80,'lime',-0.3);
    // ---- main deck (green steel), stern -> foc'sle break ----
    const SOLE_U = 0.825, U0 = 0.012;
    const dw=(u)=>{ const st=station(u); return (lerp(st.wb,st.ws,Math.pow(dfrac(st),flareExp(u)))-TH)*0.97; };
    const BSEG=20;
    for(let i=0;i<BSEG;i++){
      const u0=U0+(SOLE_U-U0)*i/BSEG, u1=U0+(SOLE_U-U0)*(i+1)/BSEG;
      face([[-dw(u0),station(u0).y,DECK],[dw(u0),station(u0).y,DECK],[dw(u1),station(u1).y,DECK],[-dw(u1),station(u1).y,DECK]],'deck',-0.35);
    }
    // ---- transom: paint bands + white cap ----
    (function(){
      const st0=station(0), sy=st0.y, xw=(f)=>lerp(st0.wb,st0.ws,f), zf=(f)=>st0.kz+f*st0.dep;
      for(const [f0,f1,mat,b] of OB)
        F.push(faceO([[-xw(f0),sy,zf(f0)],[xw(f0),sy,zf(f0)],[xw(f1),sy,zf(f1)],[-xw(f1),sy,zf(f1)]],[0,-1,0],mat,(b||0)-0.8,0.005));
      face([[-(st0.ws-TH),sy,zf(1)],[st0.ws-TH,sy,zf(1)],[st0.ws-TH,sy+0.30,zf(1)-0.004],[-(st0.ws-TH),sy+0.30,zf(1)-0.004]],'white',-0.9,0.03);
    })();
    // quarter bollards on the poop
    for(const s of [-1,1]) boxF([s*3.0,-29.3,DECK+0.11],[0.07,0.09,0.11],'iron',0.1,-0.02);
    boxF([0,-29.35,DECK+0.10],[0.55,0.28,0.10],'iron',0.05,-0.02);   // mooring winch aft
    // stern light box on the taffrail
    boxF([0,-29.55,5.55],[0.09,0.09,0.12],'iron',0.1,-0.02);

    // ---- CARGO HOLD: white coaming + mint folding hatch covers (7 panels) ----
    boxF([0,(CY0+CY1)/2,(DECK+COAMT)/2],[CXW,(CY1-CY0)/2,(COAMT-DECK)/2],'white',-0.35,-0.01);
    (function(){
      const NP=7, y0=CY0+0.20, y1=CY1-0.20, seg=(y1-y0)/NP;
      for(let i=0;i<NP;i++){
        const ya=y0+i*seg+0.10, yb=y0+(i+1)*seg-0.10;
        boxF([0,(ya+yb)/2,COAMT+0.13],[CXW-0.18,(yb-ya)/2,0.13],'mint',(i&1)?0.12:-0.12,-0.01);
      }
    })();
    // ---- open white rails along the sheer, house -> foc'sle break (the reference's white lattice) ----
    (function(){
      const P=(s,u,h)=>{ const st=station(u); return [s*(st.ws-0.20), st.y+bowRake(u,1), st.kz+st.dep+h]; };
      const RU0=0.208, RU1=0.822, NS2=12, du=(RU1-RU0)/NS2;
      for(const s of [-1,1]){
        const b=s<0?0.15:-0.4;
        for(let i=0;i<NS2;i++){ const u0=RU0+i*du, u1=u0+du;
          tubeF(P(s,u0,1.00),P(s,u1,1.00),0.032,'white',b);
          tubeF(P(s,u0,0.52),P(s,u1,0.52),0.022,'white',b); }
        for(let i=0;i<=NS2;i++){ const u=RU0+i*du; tubeF(P(s,u,0.02),P(s,u,1.00),0.022,'white',b); }
      }
      // foc'sle rail to the stem
      for(const s of [-1,1]){
        const b=s<0?0.15:-0.4;
        for(let i=0;i<5;i++){ const u0=0.835+i*0.028, u1=u0+0.028;
          tubeF(P(s,u0,0.85),P(s,u1,0.85),0.03,'white',b); tubeF(P(s,u0,0.42),P(s,u1,0.42),0.02,'white',b); }
        for(let i=0;i<=5;i++){ const u=0.835+i*0.028; tubeF(P(s,u,-0.06),P(s,u,0.85),0.02,'white',b); }
      }
    })();

    // ---- HOUSE level 1 (accommodation) ----
    F.push(faceO([[-H1X,H1F,H1T],[H1X,H1F,H1T],[H1X,H1F,DECK],[-H1X,H1F,DECK]],[0,1,0],'white',0.3,0));
    F.push(faceO([[-H1X,H1A,H1T],[H1X,H1A,H1T],[H1X,H1A,DECK],[-H1X,H1A,DECK]],[0,-1,0],'white',-0.6,0));
    F.push(faceO([[-H1X,H1A,H1T],[-H1X,H1F,H1T],[-H1X,H1F,DECK],[-H1X,H1A,DECK]],[-1,0,0],'white',-0.1,0));
    F.push(faceO([[H1X,H1A,H1T],[H1X,H1F,H1T],[H1X,H1F,DECK],[H1X,H1A,DECK]],[1,0,0],'white',-1.0,0));
    F.push(frontPanel(H1F+0.03,-1.65,-0.85,DECK+0.05,6.95,'dark',-0.5));            // deck door at the house front
    for(const s of [-1,1]){                                                          // portholes ×4 + side door
      const P = s<0 ? leftPanel : rightPanel;
      for(const yy of [-27.0,-25.4,-23.8,-22.2]){
        F.push(P(s*(H1X+0.03), yy-0.28, yy+0.28, 5.95, 6.55, 'iron', -0.15));
        F.push(P(s*(H1X+0.065), yy-0.21, yy+0.21, 6.02, 6.48, 'glas', s<0?-0.15:-1.05));
      }
      F.push(P(s*(H1X+0.03), -19.4, -18.7, DECK+0.05, 6.95, 'dark', -0.5));
    }
    F.push(backPanel(H1A-0.03,-0.5,0.5,DECK+0.05,6.95,'dark',-0.5));                 // aft door
    for(const xx of [-1.9,1.9]){                                                      // aft portholes
      F.push(backPanel(H1A-0.03, xx-0.28, xx+0.28, 5.95, 6.55, 'iron', -0.15));
      F.push(backPanel(H1A-0.065, xx-0.21, xx+0.21, 6.02, 6.48, 'glas', -0.25));
    }
    boxF([0,(H1A+H1F)/2,H1T+0.03],[H1X+0.10,(H1F-H1A)/2+0.08,0.06],'white',0.45,-0.01); // boat-deck slab
    // boat-deck stern rail
    (function(){ const rz=8.60, xr=4.05;
      tubeF([-xr,-28.5,rz],[xr,-28.5,rz],0.032,'white',0.1);
      for(const s of [-1,1]){ tubeF([s*xr,-28.5,rz],[s*xr,-26.4,rz],0.032,'white',s<0?0.15:-0.3);
        for(const yy of [-28.45,-27.4,-26.4]) tubeF([s*xr,yy,H1T+0.09],[s*xr,yy,rz],0.022,'white',-0.1); }
      for(const xx of [-2.7,-1.35,0,1.35,2.7]) tubeF([xx,-28.5,H1T+0.09],[xx,-28.5,rz],0.022,'white',-0.1);
    })();
    // liferaft canisters on the boat deck sides
    for(const s of [-1,1]){
      boxF([s*3.55,-18.7,7.85],[0.10,0.55,0.10],'iron',-0.1,-0.02);
      tubeF([s*3.55,-19.25,8.02],[s*3.55,-18.15,8.02],0.26,'white',s<0?0.3:-0.2);
    }

    // ---- HOUSE level 2 ----
    F.push(faceO([[-H2X,H2F,H2T],[H2X,H2F,H2T],[H2X,H2F,H1T],[-H2X,H2F,H1T]],[0,1,0],'white',0.3,0));
    F.push(faceO([[-H2X,H2A,H2T],[H2X,H2A,H2T],[H2X,H2A,H1T],[-H2X,H2A,H1T]],[0,-1,0],'white',-0.6,0));
    F.push(faceO([[-H2X,H2A,H2T],[-H2X,H2F,H2T],[-H2X,H2F,H1T],[-H2X,H2A,H1T]],[-1,0,0],'white',-0.1,0));
    F.push(faceO([[H2X,H2A,H2T],[H2X,H2F,H2T],[H2X,H2F,H1T],[H2X,H2A,H1T]],[1,0,0],'white',-1.0,0));
    for(const s of [-1,1]){                                                          // cabin windows ×3 + life rings
      const P = s<0 ? leftPanel : rightPanel;
      for(const yy of [-26.5,-24.7,-22.9]){
        F.push(P(s*(H2X+0.03), yy-0.42, yy+0.42, 8.30, 9.10, 'iron', -0.15));
        F.push(P(s*(H2X+0.065), yy-0.34, yy+0.34, 8.38, 9.02, 'glas', s<0?-0.15:-1.05));
      }
      F.push(P(s*(H2X+0.04), -21.4, -20.8, 8.35, 8.95, 'orng', 0.3));
    }
    for(const [xa,xb] of [[-2.4,-1.2],[1.2,2.4]]){                                    // level-2 front windows
      F.push(frontPanel(H2F+0.03, xa-0.06, xb+0.06, 8.24, 9.16, 'iron', -0.15));
      F.push(frontPanel(H2F+0.065, xa, xb, 8.30, 9.10, 'glas', -0.05));
    }
    boxF([0,(H2A+H2F)/2,H2T+0.03],[H2X+0.10,(H2F-H2A)/2+0.08,0.06],'white',0.5,-0.01); // bridge-deck slab
    // bridge-deck rails around the wheelhouse walkway
    (function(){ const rz=10.90, xr=3.90;
      for(const s of [-1,1]){
        tubeF([s*xr,-28.1,rz],[s*xr,-19.95,rz],0.03,'white',s<0?0.15:-0.3);
        for(const yy of [-28.05,-26.0,-24.0,-22.0,-20.0]) tubeF([s*xr,yy,H2T+0.09],[s*xr,yy,rz],0.02,'white',-0.1);
      }
      tubeF([-3.9,-28.1,rz],[3.9,-28.1,rz],0.03,'white',0.1);
      for(const xx of [-2.6,-1.3,0,1.3,2.6]) tubeF([xx,-28.1,H2T+0.09],[xx,-28.1,rz],0.02,'white',-0.1);
    })();
    // black exhaust scoop, bridge deck aft-centre (ref photo 2)
    boxF([0,-27.5,H2T+0.75],[0.42,0.38,0.72],'blk',-0.2,-0.02);
    boxF([0,-27.75,H2T+1.62],[0.34,0.52,0.18],'blk',-0.4,-0.02);

    // ---- WHEELHOUSE ----
    F.push(faceO([[-WHX,WHF,WHT],[WHX,WHF,WHT],[WHX,WHF,WHZ],[-WHX,WHF,WHZ]],[0,1,0],'white',0.4,0));
    F.push(faceO([[-WHX,WHA,WHT],[WHX,WHA,WHT],[WHX,WHA,WHZ],[-WHX,WHA,WHZ]],[0,-1,0],'white',-0.7,0));
    F.push(faceO([[-WHX,WHA,WHT],[-WHX,WHF,WHT],[-WHX,WHF,WHZ],[-WHX,WHA,WHZ]],[-1,0,0],'white',-0.1,0));
    F.push(faceO([[WHX,WHA,WHT],[WHX,WHF,WHT],[WHX,WHF,WHZ],[WHX,WHA,WHZ]],[1,0,0],'white',-1.0,0));
    for(const [xa,xb] of [[-3.0,-1.95],[-1.75,-0.7],[-0.5,0.5],[0.7,1.75],[1.95,3.0]]){  // 5-pane windscreen
      F.push(frontPanel(WHF+0.03, xa-0.06, xb+0.06, 10.84, 12.11, 'iron', -0.15));
      F.push(frontPanel(WHF+0.065, xa, xb, 10.90, 12.05, 'glas', 0.5));
    }
    for(const s of [-1,1]){                                                          // side windows ×3
      const P = s<0 ? leftPanel : rightPanel;
      for(const [ya,yb] of [[-21.0,-20.1],[-22.6,-21.7],[-24.2,-23.3]]){
        F.push(P(s*(WHX+0.03), ya-0.06, yb+0.06, 10.84, 12.05, 'iron', -0.15));
        F.push(P(s*(WHX+0.065), ya, yb, 10.90, 11.99, 'glas', s<0?-0.15:-1.05));
      }
    }
    for(const [xa,xb] of [[-2.5,-0.7],[0.7,2.5]]){                                    // aft windows
      F.push(backPanel(WHA-0.03, xa-0.06, xb+0.06, 10.84, 11.95, 'iron', -0.15));
      F.push(backPanel(WHA-0.065, xa, xb, 10.90, 11.89, 'glas', -0.25));
    }
    boxF([0,(WHA+WHF)/2-0.05,ROOFZ],[WHX+0.15,(WHF-WHA)/2+0.35,0.06],'white',0.6,-0.01); // roof, brow over the screen
    // ---- bridge wings: full-beam slab + white wing bulwarks + struts + life rings ----
    boxF([0,(WGY0+WGY1)/2,WHZ],[WGX,(WGY1-WGY0)/2,0.08],'white',0.35,-0.01);
    for(const s of [-1,1]){
      const P = s<0 ? leftPanel : rightPanel;
      F.push(P(s*WGX, WGY0, WGY1, WHZ+0.08, 11.0, 'white', s<0?-0.1:-1.0));
      F.push(frontPanel(WGY1, s*WHX, s*WGX, WHZ+0.08, 11.0, 'white', 0.25));
      F.push(backPanel(WGY0, s*WHX, s*WGX, WHZ+0.08, 11.0, 'white', -0.55));
      F.push(frontPanel(WGY1+0.03, s*4.7-0.26, s*4.7+0.26, 10.30, 10.85, 'orng', 0.3));
      tubeF([s*4.75,-20.1,WHZ-0.04],[s*H2X,-20.6,8.7],0.05,'white',-0.2);            // strut down to level 2
    }
    // roof gear: radar mast + scanner, whips, horn, stays
    tubeF([0,-23.5,ROOFZ+0.04],[0,-23.5,15.80],0.09,'steel',0.1);
    boxF([0,-23.5,15.10],[0.50,0.13,0.08],'white',0.5);
    boxF([0,-23.5,15.86],[0.08,0.08,0.08],'iron',0.2,-0.02);
    for(const s of [-1,1]) tubeF([s*2.1,-26.6,ROOFZ+0.04],[s*2.5,-27.2,16.4],0.028,'steel',s<0?0.25:-0.2);
    boxF([0.9,-20.4,ROOFZ+0.12],[0.10,0.22,0.08],'steel',0.2);

    // ---- deck crane at the house front (blue, ref photo 2) ----
    tubeF([1.2,-16.6,DECK],[1.2,-16.6,7.00],0.40,'white',-0.15);
    boxF([1.2,-16.6,7.25],[0.52,0.62,0.32],'hull',0.15,-0.02);
    tubeF([1.2,-16.35,7.35],[0.6,-12.55,8.55],0.20,'hull',0.25);
    tubeF([0.6,-12.6,8.50],[0.6,-12.6,6.55],0.02,'steel',-0.3);                       // hook wire to the hatch
    boxF([0.6,-12.6,6.45],[0.09,0.09,0.10],'iron',0.1,-0.02);

    // ---- FOREMAST at the hold's forward end + derrick boom + stays ----
    (function(){
      const MY=19.3, MZ0=5.45, MZT=19.0;
      tubeF([0,MY,MZ0],[0,MY,MZT],0.13,'white',0.05);
      tubeF([-2.0,MY,16.2],[2.0,MY,16.2],0.05,'steel',0.15);                          // crosstree
      boxF([0,MY,MZT+0.06],[0.07,0.07,0.07],'iron',0.2,-0.02);                        // masthead light box
      tubeF([0,MY,18.55],[0,30.6,7.25],0.022,'steel',-0.1);                           // forestay to the stem head
      for(const s of [-1,1]) tubeF([s*0.1,MY,17.6],[s*3.35,12.0,6.15],0.02,'steel',s<0?-0.05:-0.3);  // shrouds to the coaming
      tubeF([0.35,MY-0.25,7.10],[0.9,10.2,6.62],0.09,'white',-0.2);                   // derrick boom, stowed over the covers
      tubeF([0,MY,17.0],[0.9,10.4,6.72],0.018,'steel',-0.25);                         // topping lift
    })();

    // ---- foc'sle: raised whaleback deck following the sheer, white break bulkhead ----
    const FSEG=6, FCAP=0.985;
    const fz=(u)=>{ const st=station(u); return st.kz+st.dep-0.12; };
    const fw=(u)=>{ const st=station(u); return Math.max(0.02, st.ws-0.32); };
    const fy=(u)=> station(u).y + bowRake(u,1);
    for(let i=0;i<FSEG;i++){
      const u0=SOLE_U+(FCAP-SOLE_U)*i/FSEG, u1=SOLE_U+(FCAP-SOLE_U)*(i+1)/FSEG;
      face([[-fw(u0),fy(u0),fz(u0)],[fw(u0),fy(u0),fz(u0)],[fw(u1),fy(u1),fz(u1)],[-fw(u1),fy(u1),fz(u1)]],'deck',0.4,-0.02);
    }
    (function(){ const u=SOLE_U, wv=fw(u), z=fz(u), y=station(u).y, yF=fy(u), st=station(u);
      const hw=(zz)=>{ const fr=Math.max(0,Math.min(1,(zz-st.kz)/st.dep)); return lerp(st.wb,st.ws,Math.pow(fr,flareExp(u)))-TH; };
      const wTop=Math.min(wv,hw(z)), wDeck=hw(DECK);
      face([[wTop,y,z],[-wTop,y,z],[-wDeck,y,DECK],[wDeck,y,DECK]],'white',-0.6,-0.03);   // break bulkhead (faces aft)
      face([[-wv,y-0.36,z],[wv,y-0.36,z],[wv,yF,z],[-wv,yF,z]],'deck',0.4,-0.03);
    })();
    // foc'sle furniture: windlass + bow bollards + anchor pockets
    (function(){ const zf=fz(0.88)+0.02;
      boxF([0,25.0,zf+0.18],[0.60,0.34,0.18],'iron',0.1,-0.02);
      tubeF([-0.62,25.0,zf+0.42],[0.62,25.0,zf+0.42],0.24,'steel',-0.1);
      for(const s of [-1,1]) boxF([s*1.0,26.8,zf+0.12],[0.07,0.07,0.12],'iron',0.1,-0.02);
      for(const s of [-1,1]) boxF([s*3.5,21.5,5.25],[0.14,0.45,0.38],'blk',s<0?0.1:-0.4,-0.02);  // anchors in their pockets
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
  const HELM = { x:0.30, y:-20.4, z:10.60 };
  function helmSeat(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HELM.x, HELM.y, HELM.z, B);
    return { x:p.sx, y:p.sy };
  }
  const CRANE = [ {x:1.2,y:-16.6,z:7.10}, {x:0.6,y:-12.6,z:8.55} ];   // pedestal top + jib head
  function craneMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return CRANE.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  const HOLD = { x:0, y:1.5, z:6.40 };   // hold centre — laden / cargo overlays
  function hatchMount(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HOLD.x, HOLD.y, HOLD.z, B);
    return { x:p.sx, y:p.sy };
  }
  // walkway + foredeck anchors (crew / lashings), clear of the coaming, crane and masts
  const TUBS = [ {x:-4.25,y:-6,z:DECK}, {x:4.25,y:-6,z:DECK},
                 {x:-4.25,y:8,z:DECK}, {x:4.25,y:8,z:DECK}, {x:-1.5,y:-16.6,z:DECK} ];
  function tubMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  // nav lights: sidelights on the wing ends, stern light on the taffrail, masthead on the foremast
  function navMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    const pt=(x,y,z)=>{ const p=projVert(x,y,z,B); return {x:p.sx,y:p.sy}; };
    return {
      port:  pt(-5.05,-20.6,11.10),
      star:  pt( 5.05,-20.6,11.10),
      stern: pt(0,-29.55,5.75),
      mast:  pt(0,19.3,19.15),
    };
  }

  root.CoastalPacketIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], HULL, BOOT, LIME, WHITE, DECKF, MINT, GLAS, STEEL, IRON, ORNG, KEY,
    render, ROCK, rock:rockMotion, helmSeat, HELM, craneMounts, CRANE, hatchMount, HOLD, tubMounts, TUBS, navMounts };
})(typeof globalThis!=='undefined'?globalThis:window);
