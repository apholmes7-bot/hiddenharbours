/* Hidden Harbours — parametric ISO TANKER (M2 bake recipe, ADR-0006 — same pipeline as
   coastalPacketIsoRig.js / sternTrawlerMk2IsoRig.js). Tier 7, THE FINAL HULL: ~110 m LOA gas
   tanker built to the reference photos — international-orange topsides over a salmon antifouling
   bottom with a WHITE boot line, an elevated flared bow and elevated poop with the midship sheer
   dropping to a LOW BULWARK (the raked sheer ramps at both ends are baked into the shell), the
   whole WHITE HOUSE AFT (big two-level block with the NO SMOKING wall down to the weather deck,
   a set-back third level, and a RAKED-GLASS wheelhouse with full-beam bridge wings), a navy
   raked funnel, two huge GREY tank covers amidships buried in pipework — side pipe runs, a
   centre catwalk over the crowns, a compressor cabin with a riser cluster, a transverse
   manifold with yellow valve gear, vent masts, a hose crane — a raised foc'sle with windlass
   and white foremast, "LPG" in white on both sides, and a salmon bulbous bow at the stem.
   Fixed 3/4 turntable camera (elev 40deg default), 45deg steps, flat-facet shading from a fixed
   upper-left key, z-buffered, ordered dither, 1px keyline post-pass, NO AA.
   WORKING RESOLUTION 16 px = 1 m (half the fleet standard — at 32 the sprite would be 3,500 px
   long; scale x2 in-engine, PX is exposed).

   Single cell 1920x1600, pivot (960,880) = boat origin (amidships, keel bottom, centreline),
   pinned every heading. Deck anchors baked from day one: helmSeat(dir,opts) -> wheelhouse;
   craneMounts(dir,opts) -> [hose-crane pedestal, jib head]; manifoldMount(dir,opts) -> midship
   manifold (loading-arm / hose overlays); funnelMount(dir,opts) -> funnel top (engine smoke);
   tubMounts(dir,opts) -> walkway + poop anchors; navMounts(dir,opts) -> {port,star,stern,mast,
   range}. Pass the hull's rock(i) so overlays ride the wave — the slowest, longest loop in the
   fleet. Exposes globalThis.TankerIso = { W,H,PX,DIRS,pivot,order,ROCK,rock(i),render(dir,opts),
   helmSeat,HELM, craneMounts,CRANE, manifoldMount,MANIF, funnelMount,FUNL, tubMounts,TUBS,
   navMounts, HULL,BOOT,WHITE,GREY,DECKG,GLAS,STEEL,IRON,NAVY,ORNG,YELL,KEY }. */
(function (root) {
  const PX = 16, S = 16;
  const W = 1920, H = 1600, cx = 960, cy = 880;   // cell + pivot (projection of boat origin)
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const ROCK = { frames: 8, rollA: 0.85, pitchA: 0.40, heaveA: 1.0, period: 7.5 };  // 110 m laden — the slowest roll in the fleet
  function rockMotion(i, frames){
    frames = frames || ROCK.frames;
    const a = 2*Math.PI*(((i%frames)+frames)%frames)/frames;
    return { roll: ROCK.rollA*Math.sin(a), pitch: ROCK.pitchA*Math.sin(a+Math.PI/2), heave: ROCK.heaveA*Math.sin(a) };
  }
  const L = 110.0, TH = 0.14;
  const DECK = 8.6, POOP = 11.6;      // main weather deck / poop deck
  const NSEG = 26;
  const RAKE = 2.3;                    // raked tanker stem

  // ---- palette ramps dark->light (sampled from the reference photos, KTC-clamped) ----
  const HULL  = ['#4a1005','#6e1e07','#93300b','#b64312','#d5581d','#ec7635','#f99a5c'];  // international-orange topsides
  const BOOT  = ['#5e2013','#7d301c','#9c4327','#b85835','#d07048'];                       // salmon antifouling bottom + bulb
  const WHITE = ['#878b85','#a2a7a0','#bcc2ba','#cfd4cc','#dfe3dc','#eff2ec'];             // house, boot line, rails, text
  const GREY  = ['#454e53','#59646a','#707c82','#8a969b','#a5b1b4','#bfc9cb'];             // tank covers + big pipe gear
  const DECKG = ['#27312c','#333f37','#414e43','#505e50','#616f60','#748172'];             // green-grey weather deck / bulwark liner
  const GLAS  = ['#131c21','#213039','#33434e','#48657a','#6b91a1'];                       // window glass (sea-grey)
  const STEEL = ['#3a4148','#565f66','#7a858c','#9fabb1','#c3ced2'];                       // rails, masts, pipes
  const IRON  = ['#0e1114','#171b21','#232a32','#333c46'];                                 // dark fittings, winches, doors
  const NAVY  = ['#0e1526','#172644','#223764','#2f4c88','#4166ac'];                       // funnel casing
  const ORNG  = ['#833c14','#ad541c','#d4732b','#ee9a4a'];                                 // lifeboats + life rings
  const YELL  = ['#7d5a10','#a67a18','#cf9d24','#efbe3a'];                                 // crane jib + manifold valves
  const KEY   = '#0d1013';
  const MATS = { hull:{ramp:HULL,off:0}, boot:{ramp:BOOT,off:0}, white:{ramp:WHITE,off:0},
                 grey:{ramp:GREY,off:0}, deck:{ramp:DECKG,off:0}, glas:{ramp:GLAS,off:0},
                 steel:{ramp:STEEL,off:0}, iron:{ramp:IRON,off:0}, navy:{ramp:NAVY,off:0},
                 orng:{ramp:ORNG,off:0}, yell:{ramp:YELL,off:0}, blk:{ramp:IRON,off:-1}, dark:{ramp:IRON,off:-2} };
  const RINDEX = {}; [HULL,BOOT,WHITE,GREY,DECKG,GLAS,STEEL,IRON,NAVY,ORNG,YELL].forEach(r=>r.forEach((c,i)=>{ RINDEX[c]={r,i}; }));
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  // ---- offsets table: stern(0) -> bow(8). [sheerHalf, bottomHalf, depth, keelZ] (m) ----
  // elevated poop aft, LOW midship walls, elevated flared bow — the raked sheer ramps between
  // stations 2-3 and 6-7 are the reference's diagonal bulwark transitions.
  const T = [
    [6.60,5.20,12.05,0.45],   // 0 transom (poop level 12.5)
    [8.30,7.90,12.50,0.05],   // 1 poop
    [8.68,8.50,12.30,0.00],   // 2 poop, ramp begins at the house front
    [8.70,8.50, 9.60,0.00],   // 3 midship low sheer 9.6
    [8.70,8.50, 9.60,0.00],   // 4 amidships (max beam 17.4 m)
    [8.70,8.45, 9.65,0.00],   // 5
    [8.50,7.60,10.40,0.00],   // 6 rising to the foc'sle
    [7.20,3.20,12.90,0.15],   // 7 bow shoulder — heavy flare
    [0.28,0.06,14.35,0.85],   // 8 stem (head 15.2)
  ];
  const lerp = (a,b,t)=>a+(b-a)*t;
  function station(u){
    const f=Math.max(0,Math.min(8,u*8)), i=Math.min(7,Math.floor(f)), fr=f-i;
    const A=T[i], B=T[i+1];
    return { ws:lerp(A[0],B[0],fr), wb:lerp(A[1],B[1],fr), dep:lerp(A[2],B[2],fr),
             kz:lerp(A[3],B[3],fr), y:-L/2+u*L };
  }
  function bowRake(u,frac){ const t=Math.max(0,(u-0.66)/0.34), s=t*t*(3-2*t); return RAKE*s*(0.25+0.75*frac); }
  function flareExp(u){ const t=Math.max(0,(u-0.60)/0.40), s=t*t*(3-2*t); return 1+2.2*s; }
  function skin(side,u,frac,inset){
    const st=station(u);
    const ws=st.ws-(inset?TH:0), wb=st.wb-(inset?TH*0.6:0), dep=st.dep-(inset?0.02:0);
    return [ side*lerp(wb,ws,Math.pow(frac,flareExp(u))), st.y+bowRake(u,frac), st.kz+lerp(0,dep,frac) ];
  }
  function zfrac(st,z){ return Math.max(0.02, Math.min(0.995, (z-st.kz)/st.dep)); }
  function sheerZ(u){ const st=station(u); return st.kz+st.dep; }

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
  const boxF=(c,h,mat,b,db,xf)=>{ F.push.apply(F, box(c,h,mat,b,db,xf)); };
  const tubeF=(A,B2,rad,mat,b)=>{ F.push.apply(F, tube(A,B2,rad,mat,b)); };
  function objNormal(a,b,c){ const ux=b[0]-a[0],uy=b[1]-a[1],uz=b[2]-a[2], vx=c[0]-a[0],vy=c[1]-a[1],vz=c[2]-a[2];
    return [uy*vz-uz*vy, uz*vx-ux*vz, ux*vy-uy*vx]; }
  function faceO(v, outward, mat, b, db){ const n=objNormal(v[0],v[1],v[2]);
    if(n[0]*outward[0]+n[1]*outward[1]+n[2]*outward[2] < 0) v=v.slice().reverse();
    return {v, mat, b:b||0, db:(db==null?DBP:db)}; }

  // paint by ABSOLUTE z (the sheer varies): salmon bottom -> white boot line -> orange to the rail
  const ZBANDS = [ [-9, 3.25,'boot',-0.2,0], [3.25,3.95,'white',-0.45,0.01], [3.95,99,'hull',0,0] ];

  // 3x5 pixel font for the baked lettering
  const FONT={ N:['101','111','111','101','101'], O:['111','101','101','101','111'], S:['111','100','111','001','111'],
    M:['101','111','101','101','101'], K:['101','101','110','101','101'], I:['111','010','010','010','111'],
    G:['110','100','101','101','111'], L:['100','100','100','100','111'], P:['111','101','111','100','100'] };
  function frontText(str, FY, xCenter, zTop, cell, mat, b){       // on a +y-facing wall (screen-right = -x)
    const slotW=4*cell, total=str.length*slotW-cell;
    let x = xCenter + total/2;
    for(const ch of str){
      const g=FONT[ch];
      if(g) for(let r=0;r<5;r++) for(let c=0;c<3;c++) if(g[r][c]==='1')
        F.push(frontPanel(FY, x-(c+1)*cell, x-c*cell, zTop-(r+1)*cell, zTop-r*cell, mat, b));
      x -= slotW;
    }
  }
  function sideText(str, s, X, yCenter, zTop, cell, mat, b){      // reads correctly from either side
    const slotW=4*cell, total=str.length*slotW-cell;
    let y0 = yCenter - s*total/2;
    for(const ch of str){
      const g=FONT[ch];
      if(g) for(let r=0;r<5;r++) for(let c=0;c<3;c++) if(g[r][c]==='1'){
        const ya=y0+s*c*cell, yb=y0+s*(c+1)*cell;
        F.push((s>0?rightPanel:leftPanel)(s*X, Math.min(ya,yb), Math.max(ya,yb), zTop-(r+1)*cell, zTop-r*cell, mat, b));
      }
      y0 += s*slotW;
    }
  }

  // house envelope (AFT)
  const HF=-26.5, HA=-48.6, HX=7.8, H2T=17.1;                 // main block: deck -> level-2 top (NO SMOKING wall)
  const F3=-29.5, A3=HA, X3=6.8, H3T=19.7;                    // level 3, set back
  const WX=5.9, WA=-46.6, WFB=-33.3, WFT=-32.55, WZ0=19.7, WZT=22.5, ROOFZ=22.55;  // wheelhouse, RAKED front
  const WGY0=-35.6, WGY1=-33.5, WGX=8.7;                      // full-beam bridge wings
  // tank covers
  const RX=6.3, ZB=8.8, HZ=4.85;
  const TANKS=[ {y0:6.2,y1:24.8}, {y0:-16.8,y1:1.8} ];

  (function build(){
    // ---- hull shell (paint bands by absolute z so the waterline stays level under the sheer) ----
    for(const side of [-1,1]){
      for(let i=0;i<NSEG;i++){
        const u0=i/NSEG, u1=(i+1)/NSEG, sa=station(u0), sb=station(u1);
        for(const [z0,z1,mat,b,db] of ZBANDS){
          const f0a=zfrac(sa,z0), f1a=zfrac(sa,z1), f0b=zfrac(sb,z0), f1b=zfrac(sb,z1);
          if(f1a-f0a<0.004 && f1b-f0b<0.004) continue;
          face([skin(side,u0,f0a),skin(side,u1,f0b),skin(side,u1,f1b),skin(side,u0,f1a)],mat,b,db);
        }
        // inner bulwark liner (green-grey), deck line -> sheer
        const lin=(zdeck,uA,uB)=>{
          if(u0<uA-0.001||u1>uB+0.001) return;
          const fa=zfrac(sa,zdeck), fb=zfrac(sb,zdeck), LT=0.965;
          for(let k=0;k<2;k++){
            const g0a=fa+(LT-fa)*k/2, g1a=fa+(LT-fa)*(k+1)/2;
            const g0b=fb+(LT-fb)*k/2, g1b=fb+(LT-fb)*(k+1)/2;
            face([skin(side,u1,g0b,1),skin(side,u0,g0a,1),skin(side,u0,g1a,1),skin(side,u1,g1b,1)],'deck',-1.5,-0.03);
          }
        };
        lin(DECK, 0.262, 0.795);            // midship low walls
        lin(POOP, 0.012, 0.245);            // poop bulwark
        if(u0>=0.80) lin(sheerZ((u0+u1)/2)-0.35, 0.80, 0.975);   // foc'sle bulwark
        // bottom (antifouling)
        face([skin(-1,u0,0),skin(-1,u1,0),skin(1,u1,0),skin(1,u0,0)],'boot',-1.0);
        // covering board — orange rail cap both sides, full sheer
        const oa=skin(side,u0,1),ob=skin(side,u1,1),ia=skin(side,u0,1,1),ib=skin(side,u1,1,1);
        const inb=(p)=>[p[0]-side*0.42,p[1],p[2]-0.004];
        face([oa,ob,inb(ib),inb(ia)],'hull',0.5,0.03);
      }
    }
    // ---- salmon bulbous bow at the stem ----
    tubeF([0,53.4,1.7],[0,57.2,1.7],1.05,'boot',-0.2);
    // ---- decks: main weather deck / poop / foc'sle ----
    const dw=(u,z)=>{ const st=station(u); return (lerp(st.wb,st.ws,Math.pow(zfrac(st,z),flareExp(u)))-TH)*0.975; };
    const strip=(uA,uB,zf2,mat,b,N2)=>{
      for(let i=0;i<N2;i++){
        const u0=uA+(uB-uA)*i/N2, u1=uA+(uB-uA)*(i+1)/N2, z0=zf2(u0), z1=zf2(u1);
        const y0=station(u0).y+ (zf2===null?0:0), y1=station(u1).y;
        face([[-dw(u0,z0),station(u0).y,z0],[dw(u0,z0),station(u0).y,z0],[dw(u1,z1),y1,z1],[-dw(u1,z1),y1,z1]],mat,b);
      }
    };
    strip(0.258,0.795,(u)=>DECK,'deck',-0.35,18);
    strip(0.012,0.258,(u)=>POOP,'deck',-0.30,6);
    const fz=(u)=>sheerZ(u)-0.35;
    (function(){ const FS=6, U0=0.795, U1=0.982;
      for(let i=0;i<FS;i++){
        const u0=U0+(U1-U0)*i/FS, u1=U0+(U1-U0)*(i+1)/FS;
        const fy=(u)=>station(u).y+bowRake(u,1), fw=(u)=>Math.max(0.02,station(u).ws-0.34);
        face([[-fw(u0),fy(u0),fz(u0)],[fw(u0),fy(u0),fz(u0)],[fw(u1),fy(u1),fz(u1)],[-fw(u1),fy(u1),fz(u1)]],'deck',0.35,-0.02);
      }
      // foc'sle break bulkhead (faces aft, white like the refs' stores front)
      const u=U0, st=station(u), z=fz(u), y=st.y;
      const hw=(zz)=>{ const fr=zfrac(st,zz); return lerp(st.wb,st.ws,Math.pow(fr,flareExp(u)))-TH; };
      face([[hw(z),y,z],[-hw(z),y,z],[-hw(DECK),y,DECK],[hw(DECK),y,DECK]],'white',-0.6,-0.03);
      F.push(backPanel(y-0.03,-0.6,0.6,DECK+0.05,10.4,'dark',-0.5));       // stores door
    })();
    // ---- transom: paint bands + poop cap ----
    (function(){
      const st0=station(0), sy=st0.y, xw=(f)=>lerp(st0.wb,st0.ws,f), zf2=(z)=>zfrac(st0,z);
      for(const [z0,z1,mat,b] of ZBANDS){
        const f0=zf2(z0), f1=zf2(z1); if(f1-f0<0.004) continue;
        F.push(faceO([[-xw(f0),sy,st0.kz+f0*st0.dep],[xw(f0),sy,st0.kz+f0*st0.dep],[xw(f1),sy,st0.kz+f1*st0.dep],[-xw(f1),sy,st0.kz+f1*st0.dep]],[0,-1,0],mat,(b||0)-0.8,0.005));
      }
    })();

    // ---- TANK COVERS: two grey half-cylinders with seam bands + dome hatches ----
    for(const tk of TANKS){
      const NA=7, yc=(tk.y0+tk.y1)/2;
      const pt=(a,y)=>[RX*Math.cos(a), y, ZB+HZ*Math.sin(a)];
      for(let k=0;k<NA;k++){
        const a0=Math.PI*k/NA, a1=Math.PI*(k+1)/NA, am=(a0+a1)/2;
        F.push(faceO([pt(a0,tk.y0),pt(a1,tk.y0),pt(a1,tk.y1),pt(a0,tk.y1)],[Math.cos(am),0,Math.sin(am)],'grey',0,0));
        for(const ye of [tk.y0,tk.y1])
          F.push(faceO([pt(a0,ye),pt(a1,ye),[0,ye,ZB+0.1]], [0, ye<yc?-1:1, 0],'grey',ye<yc?-0.7:0.1,0.01));
        // raised seam bands
        for(const yr of [yc-4.6, yc+4.6]){
          const p2=(a,y)=>[(RX+0.07)*Math.cos(a), y, ZB+(HZ+0.07)*Math.sin(a)];
          F.push(faceO([p2(a0,yr-0.18),p2(a1,yr-0.18),p2(a1,yr+0.18),p2(a0,yr+0.18)],[Math.cos(am),0,Math.sin(am)],'grey',-0.8,-0.02));
        }
      }
      boxF([0,yc,ZB+HZ+0.28],[0.85,1.25,0.30],'grey',0.35,-0.01);          // dome hatch on the crown
      for(const s of [-1,1]) tubeF([s*0.45,yc+0.7,ZB+HZ+0.5],[s*0.45,yc+0.7,ZB+HZ+1.35],0.09,'steel',0.1);
    }
    // ---- centre catwalk over the crowns, house front -> foc'sle ----
    (function(){
      const Y0=-21.3, Y1=30.3, ZW=13.82;
      boxF([0,(Y0+Y1)/2,ZW],[0.78,(Y1-Y0)/2,0.055],'steel',-0.35,-0.01);
      for(const yy of [-21.0,-19.2,27.6,29.9]) tubeF([0,yy,DECK+0.05],[0,yy,ZW-0.05],0.07,'steel',-0.3);
      for(const yy of [3.0,5.2]) tubeF([0,yy,12.45],[0,yy,ZW-0.05],0.07,'steel',-0.3);
      for(const s of [-1,1]){
        const b=s<0?0.15:-0.4;
        tubeF([s*0.72,Y0,14.70],[s*0.72,Y1,14.70],0.045,'steel',b);
        tubeF([s*0.72,Y0,14.28],[s*0.72,Y1,14.28],0.035,'steel',b);
        for(let yy=Y0;yy<=Y1;yy+=3.44) tubeF([s*0.72,yy,ZW+0.03],[s*0.72,yy,14.70],0.035,'steel',b);
      }
    })();
    // ---- compressor cabin + riser cluster between the tanks ----
    boxF([0,4.0,10.5],[3.4,1.8,1.9],'white',-0.1,-0.01);
    boxF([0,4.0,12.47],[3.5,1.9,0.07],'grey',0.3,-0.01);
    F.push(frontPanel(5.83,-0.55,0.55,DECK+0.05,10.7,'dark',-0.5));
    for(const s of [-1,1]) F.push((s>0?rightPanel:leftPanel)(s*3.43,3.2,4.8,9.6,10.4,'iron',-0.2));
    for(const [xx,yy,zt] of [[-1.2,4.5,18.4],[1.2,4.5,18.6],[0,3.4,19.4]]) tubeF([xx,yy,12.5],[xx,yy,zt],0.13,'grey',-0.1);
    // ---- side pipe runs the length of the tank deck + yellow valve pods ----
    for(const s of [-1,1]){
      const b=s<0?0.1:-0.5;
      tubeF([s*5.85,-21,9.35],[s*5.85,31,9.35],0.14,'steel',b);
      tubeF([s*5.15,-21,9.12],[s*5.15,31,9.12],0.10,'steel',b);
      for(const yy of [-10,3,16]) boxF([s*5.85,yy,9.62],[0.16,0.24,0.14],'yell',0.2,-0.02);
    }
    // ---- transverse MANIFOLD amidship-aft: crossing pipes, yellow valve gear, drip tray ----
    (function(){
      const MY=-20.0;
      boxF([0,MY,8.95],[3.3,1.0,0.13],'iron',-0.3,-0.01);
      for(const [zz,b2] of [[9.7,-0.2],[10.15,0.05],[10.6,0.3]]) tubeF([-7.55,MY,zz],[7.55,MY,zz],0.17,'steel',b2);
      for(const s of [-1,1]) for(const zz of [9.7,10.15,10.6]) boxF([s*5.7,MY,zz],[0.22,0.28,0.22],'yell',0.25,-0.03);
      for(const s of [-1,1]) tubeF([s*0.55,MY,10.6],[s*0.55,MY,18.8],0.12,'grey',s<0?0:-0.35);
      tubeF([0,MY,10.6],[0,MY,20.2],0.13,'grey',-0.1);
    })();
    // ---- vent masts fore + aft of the tank deck ----
    for(const s of [-1,1]){
      tubeF([s*1.5,30.8,DECK],[s*1.5,30.8,21.0],0.09,'steel',s<0?0.1:-0.35);
      tubeF([s*1.5,30.45,20.7],[s*1.5,31.15,20.7],0.09,'steel',0);
      tubeF([s*2.2,-22.6,DECK],[s*2.2,-22.6,19.4],0.09,'steel',s<0?0.1:-0.35);
      tubeF([s*2.2,-22.95,19.1],[s*2.2,-22.25,19.1],0.09,'steel',0);
    }
    // ---- hose crane, starboard, between the house and the tanks ----
    tubeF([4.8,-22.6,DECK],[4.8,-22.6,12.8],0.38,'white',-0.15);
    boxF([4.8,-22.6,13.1],[0.55,0.68,0.38],'white',0.1,-0.02);
    tubeF([4.75,-22.3,13.25],[2.0,-14.8,16.6],0.22,'yell',0.25);
    tubeF([2.0,-14.85,16.5],[2.0,-14.85,11.0],0.03,'steel',-0.3);
    boxF([2.0,-14.85,10.88],[0.10,0.10,0.11],'iron',0.1,-0.02);

    // ---- "LPG" in white on both sides of the low midship hull ----
    sideText('LPG',  1, 8.72, -6.0, 7.6, 0.6, 'white', -0.25);
    sideText('LPG', -1, 8.72, -6.0, 7.6, 0.6, 'white', -0.25);

    // ---- HOUSE main block (levels 1+2): the big white wall down to the weather deck ----
    F.push(faceO([[-HX,HF,H2T],[HX,HF,H2T],[HX,HF,DECK],[-HX,HF,DECK]],[0,1,0],'white',0.3,0));
    F.push(faceO([[-HX,HA,H2T],[HX,HA,H2T],[HX,HA,POOP-0.4],[-HX,HA,POOP-0.4]],[0,-1,0],'white',-0.6,0));
    F.push(faceO([[-HX,HA,H2T],[-HX,HF,H2T],[-HX,HF,DECK],[-HX,HA,DECK]],[-1,0,0],'white',-0.1,0));
    F.push(faceO([[HX,HA,H2T],[HX,HF,H2T],[HX,HF,DECK],[HX,HA,DECK]],[1,0,0],'white',-1.0,0));
    frontText('NO SMOKING', HF+0.04, 0, 15.95, 0.22, 'hull', -0.55);
    F.push(frontPanel(HF+0.03,-5.7,-4.85,DECK+0.05,10.75,'dark',-0.5));    // deck door
    F.push(frontPanel(HF+0.03, 4.85,5.7,DECK+0.05,10.75,'dark',-0.5));
    F.push(frontPanel(HF+0.04,-7.0,-6.1,16.2,16.85,'navy',-0.2));          // house mark
    for(const s of [-1,1]){
      const P = s<0 ? leftPanel : rightPanel;
      for(const yy of [-45.2,-43.5,-41.8,-40.1,-38.4]){                     // L1 portholes
        F.push(P(s*(HX+0.03), yy-0.26, yy+0.26, 12.55, 13.15, 'iron', -0.15));
        F.push(P(s*(HX+0.065), yy-0.19, yy+0.19, 12.62, 13.08, 'glas', s<0?-0.15:-1.05));
      }
      for(const yy of [-46,-44,-42,-40,-38,-36]){                           // L2 cabin windows
        F.push(P(s*(HX+0.03), yy-0.40, yy+0.40, 15.20, 16.00, 'iron', -0.15));
        F.push(P(s*(HX+0.065), yy-0.32, yy+0.32, 15.28, 15.92, 'glas', s<0?-0.15:-1.05));
      }
      F.push(P(s*(HX+0.03), -29.3, -28.5, POOP+0.05, 13.85, 'dark', -0.5)); // side doors
      F.push(P(s*(HX+0.04), -34.4, -33.8, 15.35, 15.95, 'orng', 0.3));      // life rings
    }
    F.push(backPanel(HA-0.03,-0.65,0.65,POOP+0.05,13.85,'dark',-0.5));      // aft door
    for(const xx of [-4.2,-1.6,1.6,4.2]){
      F.push(backPanel(HA-0.03, xx-0.28, xx+0.28, 15.3, 15.9, 'iron', -0.15));
      F.push(backPanel(HA-0.065, xx-0.21, xx+0.21, 15.37, 15.83, 'glas', -0.25));
    }
    boxF([0,(HA+HF)/2,H2T+0.03],[HX+0.10,(HF-HA)/2+0.10,0.06],'white',0.45,-0.01);  // L2 roof / boat deck
    // boat-deck rails
    (function(){ const rz=18.15, xr=7.86;
      for(const s of [-1,1]){
        const b=s<0?0.15:-0.35;
        tubeF([s*xr,HA+0.1,rz],[s*xr,HF-0.1,rz],0.032,'white',b);
        for(let yy=HA+0.4;yy<=HF-0.2;yy+=2.2) tubeF([s*xr,yy,H2T+0.09],[s*xr,yy,rz],0.024,'white',b);
      }
      tubeF([-7.86,HA+0.1,rz],[7.86,HA+0.1,rz],0.032,'white',0.1);
      for(const xx of [-6,-3,0,3,6]) tubeF([xx,HA+0.1,H2T+0.09],[xx,HA+0.1,rz],0.024,'white',-0.1);
      tubeF([-7.86,HF-0.1,rz],[7.86,HF-0.1,rz],0.032,'white',0.25);
      for(const xx of [-6.6,-4.4,-2.2,0,2.2,4.4,6.6]) tubeF([xx,HF-0.1,H2T+0.09],[xx,HF-0.1,rz],0.024,'white',0.1);
    })();
    // lifeboats (orange, both sides) + liferaft canisters on the boat deck
    for(const s of [-1,1]){
      boxF([s*7.0,-44,17.9],[0.55,1.9,0.34],'orng',s<0?0.2:-0.5,-0.02);
      boxF([s*7.0,-44,18.35],[0.42,1.25,0.22],'orng',s<0?0.35:-0.35,-0.02);
      tubeF([s*6.6,-45.6,H2T+0.06],[s*7.3,-45.6,18.75],0.05,'steel',-0.2);
      tubeF([s*6.6,-42.4,H2T+0.06],[s*7.3,-42.4,18.75],0.05,'steel',-0.2);
      tubeF([s*7.35,-31.6,17.65],[s*7.35,-30.4,17.65],0.26,'white',s<0?0.3:-0.2);
      boxF([s*7.35,-31.0,17.32],[0.10,0.5,0.09],'iron',-0.1,-0.02);
    }
    // ---- LEVEL 3, set back ----
    F.push(faceO([[-X3,F3,H3T],[X3,F3,H3T],[X3,F3,H2T],[-X3,F3,H2T]],[0,1,0],'white',0.3,0));
    F.push(faceO([[-X3,A3,H3T],[X3,A3,H3T],[X3,A3,H2T],[-X3,A3,H2T]],[0,-1,0],'white',-0.6,0));
    F.push(faceO([[-X3,A3,H3T],[-X3,F3,H3T],[-X3,F3,H2T],[-X3,A3,H2T]],[-1,0,0],'white',-0.1,0));
    F.push(faceO([[X3,A3,H3T],[X3,F3,H3T],[X3,F3,H2T],[X3,A3,H2T]],[1,0,0],'white',-1.0,0));
    for(const [xa,xb] of [[-5.8,-3.9],[-2.9,-1.0],[1.0,2.9],[3.9,5.8]]){    // L3 front windows
      F.push(frontPanel(F3+0.03, xa-0.06, xb+0.06, 18.10, 19.05, 'iron', -0.15));
      F.push(frontPanel(F3+0.065, xa, xb, 18.17, 18.98, 'glas', -0.05));
    }
    for(const s of [-1,1]){
      const P = s<0 ? leftPanel : rightPanel;
      for(const yy of [-45,-43,-41,-39,-37]){
        F.push(P(s*(X3+0.03), yy-0.40, yy+0.40, 18.15, 18.95, 'iron', -0.15));
        F.push(P(s*(X3+0.065), yy-0.32, yy+0.32, 18.23, 18.87, 'glas', s<0?-0.15:-1.05));
      }
    }
    boxF([0,(A3+F3)/2,H3T+0.03],[X3+0.10,(F3-A3)/2+0.08,0.06],'white',0.5,-0.01);   // L3 roof
    // L3-front walkway rail on the L2 roof
    (function(){ const rz=18.15;
      tubeF([-6.7,F3-0.35,rz],[6.7,F3-0.35,rz],0.03,'white',0.2);
    })();
    // ---- WHEELHOUSE with the RAKED windscreen (top leans forward, like the refs) ----
    F.push(faceO([[-WX,WFB,WZ0],[WX,WFB,WZ0],[WX,WFT,WZT],[-WX,WFT,WZT]],[0,1,0.25],'white',0.45,0));
    F.push(faceO([[-WX,WA,WZT],[WX,WA,WZT],[WX,WA,WZ0],[-WX,WA,WZ0]],[0,-1,0],'white',-0.7,0));
    F.push(faceO([[-WX,WFB,WZ0],[-WX,WA,WZ0],[-WX,WA,WZT],[-WX,WFT,WZT]],[-1,0,0],'white',-0.1,0));
    F.push(faceO([[WX,WFB,WZ0],[WX,WA,WZ0],[WX,WA,WZT],[WX,WFT,WZT]],[1,0,0],'white',-1.0,0));
    (function(){                                                            // 6 raked panes
      const yAt=(z)=>WFB+(z-WZ0)*((WFT-WFB)/(WZT-WZ0));
      const zA=20.45, zB=22.02, zA2=20.30, zB2=22.16;
      const pane=(xa,xb,z0,z1,off,mat,b)=>F.push(faceO([[xa,yAt(z0)+off,z0],[xb,yAt(z0)+off,z0],[xb,yAt(z1)+off,z1],[xa,yAt(z1)+off,z1]],[0,1,0.25],mat,b,DBP));
      const XW2=5.35, NP=6, gap=0.16, w=(2*XW2-(NP-1)*gap)/NP;
      pane(-XW2-0.12, XW2+0.12, zA2, zB2, 0.05, 'iron', -0.15);
      for(let i=0;i<NP;i++){ const xa=-XW2+i*(w+gap); pane(xa, xa+w, zA, zB, 0.09, 'glas', 0.55); }
    })();
    for(const s of [-1,1]){                                                 // WH side windows x3
      const P = s<0 ? leftPanel : rightPanel;
      for(const [ya,yb] of [[-35.4,-34.0],[-37.8,-36.4],[-40.2,-38.8]]){
        F.push(P(s*(WX+0.03), ya-0.08, yb+0.08, 20.55, 21.95, 'iron', -0.15));
        F.push(P(s*(WX+0.065), ya, yb, 20.65, 21.85, 'glas', s<0?-0.15:-1.05));
      }
    }
    for(const [xa,xb] of [[-4.6,-1.2],[1.2,4.6]]){                          // aft windows
      F.push(backPanel(WA-0.03, xa-0.06, xb+0.06, 20.6, 21.85, 'iron', -0.15));
      F.push(backPanel(WA-0.065, xa, xb, 20.68, 21.77, 'glas', -0.25));
    }
    boxF([0,(WA+WFT)/2-0.05,ROOFZ],[WX+0.15,(WFT-WA)/2+0.42,0.06],'white',0.6,-0.01);  // roof + brow
    // ---- bridge wings: full-beam slab + white wing bulwarks + struts + life rings ----
    boxF([0,(WGY0+WGY1)/2,WZ0],[WGX,(WGY1-WGY0)/2,0.08],'white',0.35,-0.01);
    for(const s of [-1,1]){
      const P = s<0 ? leftPanel : rightPanel;
      F.push(P(s*WGX, WGY0, WGY1, WZ0+0.08, 20.75, 'white', s<0?-0.1:-1.0));
      F.push(frontPanel(WGY1, s*WX, s*WGX, WZ0+0.08, 20.75, 'white', 0.25));
      F.push(backPanel(WGY0, s*WX, s*WGX, WZ0+0.08, 20.75, 'white', -0.55));
      F.push(frontPanel(WGY1+0.03, s*7.9-0.26, s*7.9+0.26, 20.0, 20.55, 'orng', 0.3));
      tubeF([s*8.35,-33.9,WZ0-0.04],[s*X3,-34.6,18.4],0.05,'white',-0.2);
      boxF([s*8.5,-34.5,20.95],[0.09,0.09,0.13],'iron',0.1,-0.02);          // sidelight boxes
    }
    // ---- NAVY funnel, raked aft, on the boat deck ----
    (function(){
      const shear=(p)=>[p[0], p[1]-(p[2]-H2T)*0.18, p[2]];
      boxF([0,-45.3,21.4],[2.0,1.5,4.3],'navy',-0.1,-0.01,shear);
      boxF([0,-45.3,25.95],[2.06,1.56,0.26],'blk',-0.3,-0.02,shear);
      F.push(faceO([[-2.04,-46.55,23.2],[-2.04,-45.05,23.2],[-2.04,-45.32,21.7],[-2.04,-46.82,21.7]],[-1,0,0],'white',0.1,0.06));  // funnel mark
      F.push(faceO([[2.04,-45.05,23.2],[2.04,-46.55,23.2],[2.04,-46.82,21.7],[2.04,-45.32,21.7]],[1,0,0],'white',-0.7,0.06));
      for(const s of [-1,1]) tubeF([s*0.7,-46.9,25.9],[s*0.7,-47.1,27.1],0.17,'iron',-0.2);
    })();
    // ---- radar mast + satdome + whips on the wheelhouse roof ----
    tubeF([0,-37.5,ROOFZ+0.04],[0,-37.5,30.6],0.12,'white',0.05);
    tubeF([-2.0,-37.5,28.6],[2.0,-37.5,28.6],0.05,'steel',0.15);
    boxF([0,-37.5,29.35],[0.72,0.15,0.10],'white',0.5);
    boxF([0,-37.5,30.68],[0.08,0.08,0.09],'iron',0.2,-0.02);
    tubeF([0,-35.4,ROOFZ+0.04],[0,-35.4,26.2],0.08,'white',0.0);
    boxF([0,-35.4,26.35],[0.5,0.11,0.08],'white',0.4);
    tubeF([0,-43.5,ROOFZ+0.04],[0,-43.5,23.35],0.28,'white',-0.1);
    boxF([0,-43.5,23.85],[0.55,0.55,0.5],'white',0.6,-0.01);
    for(const s of [-1,1]) tubeF([s*4.8,-45.5,ROOFZ+0.04],[s*5.3,-46.1,25.6],0.03,'steel',s<0?0.25:-0.2);
    // ---- FOREMAST on the foc'sle + stays ----
    (function(){
      const MY=37.0, MZ0=11.85, MZT=27.0;
      tubeF([0,MY,MZ0],[0,MY,MZT],0.14,'white',0.05);
      tubeF([-1.9,MY,24.6],[1.9,MY,24.6],0.05,'steel',0.15);
      boxF([0,MY,MZT+0.07],[0.08,0.08,0.08],'iron',0.2,-0.02);
      tubeF([0,MY,26.5],[0,56.4,15.3],0.028,'steel',-0.1);                  // forestay to the stem head
      for(const s of [-1,1]) tubeF([s*0.12,MY,25.4],[s*5.4,32.2,10.6],0.024,'steel',s<0?-0.05:-0.3);
    })();
    // ---- foc'sle furniture: windlass drums, bollards, anchors in the flare ----
    (function(){
      const zf2=fz(0.9)+0.02;
      for(const s of [-1,1]){
        boxF([s*2.2,47.6,zf2+0.22],[0.62,0.40,0.22],'iron',0.1,-0.02);
        tubeF([s*1.55,47.6,zf2+0.52],[s*2.85,47.6,zf2+0.52],0.28,'steel',s<0?0:-0.25);
        boxF([s*1.7,43.8,zf2+0.14],[0.08,0.08,0.14],'iron',0.1,-0.02);
      }
      boxF([0,51.2,zf2+0.16],[0.09,0.09,0.16],'iron',0.1,-0.02);
      for(const s of [-1,1]){ const p=skin(s,0.918,0.70); boxF([p[0]*1.02,p[1],p[2]],[0.16,0.5,0.45],'blk',s<0?0.1:-0.4,-0.02); }
      // foc'sle rails to the stem
      const P=(s,u,h)=>{ const st=station(u); return [s*(st.ws-0.24), st.y+bowRake(u,1), sheerZ(u)+h]; };
      const RU0=0.803, RU1=0.972, NS2=7, du=(RU1-RU0)/NS2;
      for(const s of [-1,1]){
        const b=s<0?0.15:-0.4;
        for(let i=0;i<NS2;i++){ const u0=RU0+i*du, u1=u0+du;
          tubeF(P(s,u0,0.95),P(s,u1,0.95),0.035,'white',b);
          tubeF(P(s,u0,0.48),P(s,u1,0.48),0.025,'white',b); }
        for(let i=0;i<=NS2;i++){ const u=RU0+i*du; tubeF(P(s,u,0.0),P(s,u,0.95),0.025,'white',b); }
      }
    })();
    // ---- poop aft: mooring winches, bollards, stern light ----
    for(const s of [-1,1]){
      boxF([s*2.6,-51.6,POOP+0.25],[0.75,0.45,0.25],'iron',0.05,-0.02);
      tubeF([s*1.85,-51.6,POOP+0.62],[s*3.35,-51.6,POOP+0.62],0.30,'steel',s<0?0:-0.25);
      boxF([s*6.1,-53.5,POOP+0.14],[0.08,0.08,0.14],'iron',0.1,-0.02);
      boxF([s*4.0,-49.8,POOP+0.13],[0.08,0.08,0.13],'iron',0.1,-0.02);
    }
    boxF([0,-54.45,12.62],[0.09,0.09,0.13],'iron',0.1,-0.02);
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
  const HELM = { x:0.35, y:-34.6, z:20.9 };
  function helmSeat(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(HELM.x, HELM.y, HELM.z, B);
    return { x:p.sx, y:p.sy };
  }
  const CRANE = [ {x:4.8,y:-22.6,z:13.3}, {x:2.0,y:-14.8,z:16.6} ];   // hose-crane pedestal top + jib head
  function craneMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return CRANE.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  const MANIF = { x:0, y:-20.0, z:10.9 };   // manifold centre — loading-arm / hose overlays
  function manifoldMount(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(MANIF.x, MANIF.y, MANIF.z, B);
    return { x:p.sx, y:p.sy };
  }
  const FUNL = { x:0, y:-47.0, z:26.4 };    // funnel top — engine-smoke overlay
  function funnelMount(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts), p=projVert(FUNL.x, FUNL.y, FUNL.z, B);
    return { x:p.sx, y:p.sy };
  }
  // walkway + poop anchors (crew / lashings), clear of the covers, pipes and manifold
  const TUBS = [ {x:-7.1,y:-14,z:DECK}, {x:7.1,y:-14,z:DECK},
                 {x:-7.1,y:20,z:DECK}, {x:7.1,y:20,z:DECK}, {x:0,y:-52.5,z:POOP} ];
  function tubMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    return TUBS.map(m=>{ const p=projVert(m.x,m.y,m.z,B); return {x:p.sx, y:p.sy}; });
  }
  // nav lights: sidelights on the wing ends, stern light, foremast light, range light on the radar mast
  function navMounts(dir, opts){
    opts = Object.assign({}, (typeof opts==='number'?{elev:opts}:opts||{}), {dir});
    const B=camBasis(opts);
    const pt=(x,y,z)=>{ const p=projVert(x,y,z,B); return {x:p.sx,y:p.sy}; };
    return {
      port:  pt(-8.5,-34.5,21.15),
      star:  pt( 8.5,-34.5,21.15),
      stern: pt(0,-54.45,12.85),
      mast:  pt(0,37.0,27.15),
      range: pt(0,-37.5,30.85),
    };
  }

  root.TankerIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:cy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], HULL, BOOT, WHITE, GREY, DECKG, GLAS, STEEL, IRON, NAVY, ORNG, YELL, KEY,
    render, ROCK, rock:rockMotion, helmSeat, HELM, craneMounts, CRANE, manifoldMount, MANIF, funnelMount, FUNL, tubMounts, TUBS, navMounts };
})(typeof globalThis!=='undefined'?globalThis:window);
