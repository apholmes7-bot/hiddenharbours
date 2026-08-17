/* Hidden Harbours — parametric ISO ROAD-VEHICLE rig (SAME turntable + camera + shading as
   camperIsoRig.js / houseIsoRig.js / the fleet). First body: DUALLY 3500 — a crew-cab one-tonne
   dually pickup (long box, flared rear fenders, six wheels). The catalogue is built to grow:
   cars, SUVs, cube vans, semis all land as BODIES entries rendered through this same bake.
   45deg steps, elev 40deg, flat-facet shading from the fixed key, z-buffered, ordered dither,
   depth-edge darkening, NO AA. 32 px = 1 m, true to the fleet. Ringless from birth (ADR 0031).

   ARTICULATION — every moving part is a pose param on render(dir,opts), 0..1 unless noted:
     dFL dFR dRL dRR   four doors, hinged on their FORWARD edge, 0 -> 62deg
     hood              hinged at the cowl, 0 -> 46deg (engine bay modelled underneath)
     gate              tailgate, hinged at its bottom edge, 0 -> 92deg (drops level)
     roll              master wheel roll, in REVOLUTIONS (cyclic)
     wFL wFR wRL wRR   per-wheel roll offsets, revolutions — wheels rotate INDEPENDENTLY
     susF susR         suspension travel per axle, -1..1 (+ compresses, body drops & pitches;
                       wheels stay on the ground — the BODY moves, which is what reads)

   ORIGIN / PIVOT: ground-centre of the body footprint. +x curb side, +y nose, +z up.
   Doors: FL/RL are street side (-x, the driver's), FR/RR curb side (+x).

   PAINT: harbour BODY ramps only, polish 0.22 (painted steel). Weather greys, grimes, rusts
   the running gear. No colours invented.

   Exposes globalThis.VehicleIso = { W,H,PX,DIRS,pivot,order,defaultElev, BODY,TRIM,IRON,GALV,
     RUBBER,CHROME,CLOTH,GLASSD,GLASSN,KEY, BODIES,PRESETS,CUES, list(), dims(opts), resolve(opts),
     render(dir,opts), frames(dir,n,opts,cue), anchors(dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 384, H = 320, cx = 192, groundY = 214;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;                    // ADR 0031

  // ---- ramps (harbour master ramps, shared with the houses & fleet) ----
  const BODY = {
    white:       ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    cream:       ['#8a6f3c','#a6884b','#c2a35f','#d8bd7c','#e9d59d','#f5e7c1'],
    red:         ['#4a130f','#671b14','#88271c','#a33124','#bd4230','#d25a42'],
    sage:        ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'],
    blue:        ['#33454a','#43585d','#556d72','#6a848a','#849ea3','#a3b9bd'],
    teal:        ['#123a3a','#1b4d4b','#26635e','#357b73','#4d968b','#6cb1a4'],
    gold:        ['#5e4a12','#7c6119','#987a26','#b39440','#c8ab5e','#dbc182'],
    rustOrange:  ['#4a2410','#6a3514','#8c481a','#a85f27','#c07a3a','#d49657'],
    greyShingle: ['#4c463f','#5d564c','#6f665a','#82786a','#968b7b','#a99d8c'],
    plum:        ['#2e2333','#3f3047','#523f5d','#664f73','#7d648b','#9079a1'],
  };
  const TRIM   = ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'];
  const IRON   = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970'];
  const GALV   = ['#565b5f','#6d7276','#868b8f','#a0a5a8','#bbbfc1','#d6d9da'];
  const RUBBER = ['#121417','#191c20','#22262b','#2c3137','#383e45','#464d55'];
  const CHROME = ['#4a5157','#5f696f','#7b858c','#98a2a8','#b6bec2','#d6dbdd'];
  const CLOTH  = ['#23262b','#2e3238','#3a3f46','#484e56','#575e67','#686f79'];
  const SHADE  = ['#0b0e11','#0f1418','#141a1f','#1a2128','#212a31','#28323a'];
  const GLASSD = ['#1b262b','#243238','#2f4149','#3d545c','#5d7b82','#96b6ba'];
  const GLASSN = ['#141d2b','#1d2a3d','#2a3c53','#3d5570','#6b7f9c','#95a8c0'];
  const GLOW   = ['#7a5a18','#c09a2c','#efd06a','#fdf0b6'];
  const LENSR  = ['#3a0c0a','#5a120e','#7d1c14','#a52a1d','#c93c2a','#e4573f'];
  const LENSA  = ['#4a2c07','#6d420b','#8f5a12','#b0771f','#cc9633','#e5b455'];
  const KEY    = '#1a1c22';

  // ---- shading (identical recipe to camperIsoRig / the fleet) ----
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }

  function camBasis(opts){ const dir=opts.dir||0, th=dir*Math.PI/4, e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { th, ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) }; }
  function projVert(x,y,z,B){ const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:groundY-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) }; }
  function normal(a,b,c){ const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx; const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m]; }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }

  // ---- face builders (outward-normal winding, camper conventions) ----
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }
  function quad(out,a,b,c,d,mat,bi,db,uv,tex,flat){ out.push(F([a,b,c,d],mat,bi,db,uv,tex,flat)); }
  function slab(out, pts, z, mat, b, tex){ const uv=tex?pts.map(p=>[p[0],p[1]]):null;
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0, uv, tex)); }
  // wall at constant x; sgn=+1 faces +x  (needs y increasing first for +x)
  function wallX(out,x,y0,y1,z0,z1,mat,b,sgn,uv,tex){
    if(sgn>0) quad(out,[x,y0,z0],[x,y1,z0],[x,y1,z1],[x,y0,z1],mat,b,0,uv,tex);
    else      quad(out,[x,y1,z0],[x,y0,z0],[x,y0,z1],[x,y1,z1],mat,b,0,uv,tex);
  }
  // wall at constant y; sgn=+1 faces +y
  function wallY(out,y,x0,x1,z0,z1,mat,b,sgn,uv,tex){
    if(sgn>0) quad(out,[x1,y,z0],[x0,y,z0],[x0,y,z1],[x1,y,z1],mat,b,0,uv,tex);
    else      quad(out,[x0,y,z0],[x1,y,z0],[x1,y,z1],[x0,y,z1],mat,b,0,uv,tex);
  }
  function boxAt(out, x0,x1,y0,y1,z0,z1, mat, b, noTop, tex){
    b=b||0;
    wallY(out,y0,x0,x1,z0,z1,mat,b-0.30,-1,null,tex);
    wallY(out,y1,x0,x1,z0,z1,mat,b+0.10,+1,null,tex);
    wallX(out,x1,y0,y1,z0,z1,mat,b+0.18,+1,null,tex);
    wallX(out,x0,y0,y1,z0,z1,mat,b-0.42,-1,null,tex);
    if(!noTop) slab(out,[[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, b+0.34, tex);
  }
  const sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]];
  const crs=(a,b)=>[a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]];
  const nrm=(v)=>{ const m=Math.hypot(v[0],v[1],v[2])||1; return [v[0]/m,v[1]/m,v[2]/m]; };
  function tube(out, p0, p1, r, n, mat, b, caps, tex){
    const u=nrm(sub(p1,p0)); const a=Math.abs(u[2])>0.9?[1,0,0]:[0,0,1];
    const e1=nrm(crs(a,u)), e2=nrm(crs(u,e1)); n=n||12; b=b||0;
    const L=Math.hypot(p1[0]-p0[0],p1[1]-p0[1],p1[2]-p0[2]), arc=(2*Math.PI*r)/n;
    const P=(i,end)=>{ const th=(i/n)*Math.PI*2, c=Math.cos(th)*r, s=Math.sin(th)*r, q=end?p1:p0;
      return [q[0]+e1[0]*c+e2[0]*s, q[1]+e1[1]*c+e2[1]*s, q[2]+e1[2]*c+e2[2]*s]; };
    for(let i=0;i<n;i++){ const j=(i+1)%n;
      out.push(F([P(i,0),P(j,0),P(j,1),P(i,1)], mat, b, 0,
        tex?[[i*arc,0],[(i+1)*arc,0],[(i+1)*arc,L],[i*arc,L]]:null, tex)); }
    if(caps!==false){ const top=[],bot=[];
      for(let i=0;i<n;i++){ top.push(P(i,1)); bot.push(P(n-1-i,0)); }
      out.push(F(top, mat, b+0.26)); out.push(F(bot, mat, b-0.5)); }
  }
  const bar = (out,p0,p1,r,mat,b)=> tube(out,p0,p1,r,4,mat,b);

  // ---- textures ----
  function wearTex(w){ return (u,v)=>{ if(w>0.03 && hash2(Math.floor(u*6.5)|0, Math.floor(v*6.5)|0) < w*0.10) return -1; return 0; }; }
  function grilleTex(){ const p=0.088; return (u,v)=>{ const f=((v%p)+p)%p; return f<0.036?2:0; }; }
  function ribTex(){ const p=0.15; return (u,v)=>{ const f=((u%p)+p)%p; return f<0.055?-1:0; }; }
  function treadTex(phase){ const c=0.105; return (u,v)=>{ const f=(((u+phase)%c)+c)%c; return f<c*0.42?-1:0; }; }

  // ================= GEOMETRY — DUALLY 3500 =================
  const TF=0.09, TR=0.11;                          // suspension travel per axle, metres
  const G = {
    noseY:3.06, bumpF:[3.04,3.30], tailY:-3.08, bumpR:[-3.34,-3.10],
    cowlY:1.82, cabRear:-1.02, bedFront:-1.10,
    axF:2.18, axR:-2.12, wheelR:0.42, tireW:0.26,
    frontWX:0.90, rearWXin:0.72, rearWXout:1.05,
    hwSide:1.03, hwFender:1.05, hwRocker:0.95, beltZ:1.38,
    glassTop:1.90, roofZ:2.00, hwRoof:0.82,
    hoodZc:1.50, hoodZn:1.38, hwHood:0.88,
    bedFloorZ:0.95, railZ:1.46, flareHW:1.24, flareTop:1.10,
    doorF:[0.54,1.74], doorR:[-0.76,0.46], doorZ0:0.68,
    archF:{r:0.56, zc:0.45}, archR:{r:0.60, zc:0.45},
  };
  // tapered greenhouse plane: half-width at height z (belt -> glass top)
  const gx=(z)=> 1.00 - 0.3036*(z-G.beltZ);

  const BODIES = {
    dually3500: { key:'dually3500', label:'Dually 3500', kind:'pickup',
      loa:+(G.bumpF[1]-G.bumpR[0]).toFixed(2), bodyL:+(G.noseY-G.tailY).toFixed(2),
      width:+(G.flareHW*2).toFixed(2), bodyW:+(G.hwSide*2).toFixed(2),
      height:G.roofZ+0.05, wheelbase:+(G.axF-G.axR).toFixed(2),
      bedLen:+((G.bedFront-0.06)-G.tailY).toFixed(2), wheels:6 },
  };

  const PRESETS = {
    showroom:  { paint:'white', weather:0.05 },
    workhorse: { paint:'white', weather:0.45 },
    farmGate:  { paint:'red',   weather:0.62, gate:1 },
    serviceBay:{ paint:'blue',  weather:0.30, hood:1 },
    crewCall:  { paint:'sage',  weather:0.35, dFL:1, dRL:1 },
  };
  // named pose sweeps for frames(): t runs 0..1 over the strip
  const CUES = {
    doors: (t)=>({ dFL:t, dFR:t, dRL:t, dRR:t }),
    hood:  (t)=>({ hood:t }),
    gate:  (t)=>({ gate:t }),
    roll:  (t)=>({ roll:t }),                             // one revolution, cyclic
    bounce:(t)=>({ susF:Math.sin(t*Math.PI*2)*0.7, susR:Math.sin(t*Math.PI*2+1.3)*0.7, roll:t }),
  };

  function resolve(opts){
    opts=opts||{};
    const g=(k,d)=> opts[k]!=null?opts[k]:d;
    const c01=(v)=>Math.max(0,Math.min(1,v)), c11=(v)=>Math.max(-1,Math.min(1,v));
    return {
      body:'dually3500', B:BODIES.dually3500,
      paint: opts.paint||'white', weather:g('weather',0.32),
      hood:c01(g('hood',0)), gate:c01(g('gate',0)),
      dFL:c01(g('dFL',0)), dFR:c01(g('dFR',0)), dRL:c01(g('dRL',0)), dRR:c01(g('dRR',0)),
      roll:g('roll',0), wFL:g('wFL',0), wFR:g('wFR',0), wRL:g('wRL',0), wRR:g('wRR',0),
      susF:c11(g('susF',0)), susR:c11(g('susR',0)),
      mirrors:g('mirrors',true), mudflaps:g('mudflaps',true), steps:g('steps',false), hitch:g('hitch',true),
      night:!!opts.night, outline: opts.outline!=null?!!opts.outline:KEYLINE_DEFAULT };
  }
  function dims(opts){ const s=resolve(opts); return Object.assign({ travelF:TF, travelR:TR }, s.B); }

  // rotations: vertical hinge (doors) and x-axis hinge (hood, tailgate)
  const hingeZ=(p,hx,hy,ca,sa)=>{ const dx=p[0]-hx, dy=p[1]-hy; return [hx+dx*ca-dy*sa, hy+dx*sa+dy*ca, p[2]]; };
  const rotX=(p,hy,hz,ca,sa)=>{ const dy=p[1]-hy, dz=p[2]-hz; return [p[0], hy+dy*ca-dz*sa, hz+dy*sa+dz*ca]; };

  // build into a temp array, transform every vertex, then merge — how every hinged part moves
  function part(out, fn, xf){
    const T=[]; fn(T);
    if(xf) for(const f of T) f.v=f.v.map(xf);
    for(const f of T) out.push(f);
  }

  // vertical side panel with wheel-arch openings cut out (strips along y)
  function archPanel(out, x, y0,y1, z0,z1, mat, b, sgn, arches, tex){
    const step=0.075;
    for(let ya=y0; ya<y1-1e-6; ya+=step){
      const yb=Math.min(y1, ya+step);
      const top=(y)=>{ let zb=z0;
        for(const A of arches){ const d=Math.abs(y-A.yc); if(d<A.r){ zb=Math.max(zb, A.zc+Math.sqrt(A.r*A.r-d*d)); } }
        return Math.min(zb, z1); };
      const za=top(ya), zb2=top(yb);
      if(za>=z1-1e-4 && zb2>=z1-1e-4) continue;
      const uv=tex?[[ya,za],[yb,zb2],[yb,z1],[ya,z1]]:null;
      if(sgn>0) out.push(F([[x,ya,za],[x,yb,zb2],[x,yb,z1],[x,ya,z1]],mat,b,0,uv,tex));
      else      out.push(F([[x,yb,zb2],[x,ya,za],[x,ya,z1],[x,yb,z1]],mat,b,0,tex?[[yb,zb2],[ya,za],[ya,z1],[yb,z1]]:null,tex));
    }
  }

  // ---- frame, tank, bumpers ----
  function buildFrame(out,s){
    for(const sx of [-1,1]){
      boxAt(out, sx*0.50-0.045, sx*0.50+0.045, -3.28, 2.98, 0.50, 0.64, 'iron', -0.15);
    }
    for(const y of [-2.70,-1.10,0.30,1.60,2.60]) boxAt(out,-0.50,0.50,y-0.05,y+0.05,0.50,0.60,'iron',-0.3);
    boxAt(out, -0.92,-0.46, -0.90, 0.20, 0.40, 0.62, 'galv', -0.2);          // fuel tank, street side
    // bumpers
    boxAt(out, -1.08,1.08, G.bumpF[0], G.bumpF[1], 0.50, 0.80, 'chrome', 0.05);
    boxAt(out, -1.06,1.06, G.bumpR[0], G.bumpR[1], 0.50, 0.78, 'chrome', 0.05);
    for(const sx of [-1,1]) boxAt(out, sx*0.40-0.04, sx*0.40+0.04, 3.28, 3.36, 0.42, 0.52, 'iron', -0.1); // tow hooks
    if(s.hitch){ boxAt(out,-0.07,0.07,-3.40,-3.20,0.36,0.50,'iron',-0.1);
      tube(out,[0,-3.36,0.50],[0,-3.36,0.60],0.032,8,'chrome',0.4); }
    tube(out,[0.62,-2.90,0.28],[0.62,-3.30,0.27],0.048,8,'chrome',0.15);     // exhaust tip
  }

  // ---- front clip: fenders, nose, grille, lamps ----
  function buildFront(out,s){
    const wear=wearTex(s.weather);
    for(const sx of [-1,1]){                                                 // fender sides w/ arch
      archPanel(out, sx*G.hwFender, G.cowlY, G.noseY, 0.66, 1.36, 'paint', sx>0?0.18:-0.42, sx,
        [{yc:G.axF, r:G.archF.r, zc:G.archF.zc}], wear);
      // fender top strip (static — the hood opens inside it)
      if(sx>0) quad(out, [0.86,G.cowlY,1.40],[1.05,G.cowlY,1.36],[1.05,G.noseY,1.36],[0.86,G.noseY,1.40], 'paint', 0.30);
      else     quad(out, [-1.05,G.cowlY,1.36],[-0.86,G.cowlY,1.40],[-0.86,G.noseY,1.40],[-1.05,G.noseY,1.36], 'paint', 0.30);
      // arch liner (above the tire tops only — must never hide the wheels)
      wallX(out, sx*1.00, G.axF-G.archF.r, G.axF+G.archF.r, 0.84, 1.02, 'shade', -0.85, sx);
      // side marker lamp
      boxAt(out, Math.min(sx*1.035,sx*1.075), Math.max(sx*1.035,sx*1.075), 2.84, 2.96, 0.92, 1.00, 'lensA', 0.3, false);
    }
    // nose face: grille + headlamps + paint strips
    const yN=G.noseY;
    out.push(F([[0.60,yN,0.88],[-0.60,yN,0.88],[-0.60,yN,1.30],[0.60,yN,1.30]],'grille',-0.1,0,
      [[0.60,0.88],[-0.60,0.88],[-0.60,1.30],[0.60,1.30]], grilleTex()));
    for(const sx of [-1,1]){
      boxAt(out, Math.min(sx*0.64,sx*0.98), Math.max(sx*0.64,sx*0.98), yN-0.02, yN+0.03, 1.06, 1.30, s.night?'glow':'head', 0.35, false);
      wallY(out, yN, Math.min(sx*0.64,sx*0.98), Math.max(sx*0.64,sx*0.98), 0.88, 1.06, 'paint', -0.05, +1);
      wallY(out, yN, Math.min(sx*0.60,sx*0.64), Math.max(sx*0.60,sx*0.64), 0.88, 1.30, 'paint', -0.05, +1);
    }
    wallY(out, yN, -1.05, 1.05, 1.30, 1.40, 'paint', 0.05, +1);              // header lip
    wallY(out, yN, -1.05, 1.05, 0.80, 0.88, 'paint', -0.25, +1);             // valance
    wallX(out, 1.05, 2.96, yN, 0.80, 1.40, 'paint', 0.18, +1);               // nose corners
    wallX(out,-1.05, 2.96, yN, 0.80, 1.40, 'paint', -0.42, -1);
    // engine bay (seen when the hood is up; walls rise to the hood shutline)
    wallX(out, 0.86, G.cowlY, yN-0.06, 0.72, 1.44, 'shade', -0.8, -1);
    wallX(out,-0.86, G.cowlY, yN-0.06, 0.72, 1.44, 'shade', -0.8, +1);
    wallY(out, yN-0.08, -0.86, 0.86, 0.72, 1.34, 'shade', -0.8, -1);         // radiator wall
    wallY(out, G.cowlY+0.02, -0.86, 0.86, 0.72, 1.40, 'shade', -0.8, +1);    // firewall
    boxAt(out, -0.38,0.38, 2.02, 2.86, 0.82, 1.22, 'iron', -0.3);            // engine block
    boxAt(out, -0.20,0.20, 2.18, 2.66, 1.22, 1.32, 'galv', -0.1);            // intake
    boxAt(out, -0.80,-0.48, 2.56, 2.90, 1.02, 1.24, 'rubber', -0.2);         // battery
    // cowl panel between hood hinge and windshield
    slab(out, [[-1.00,1.76],[1.00,1.76],[1.00,1.84],[-1.00,1.84]], 1.48, 'paint', 0.12);
  }

  function buildHood(out,s){
    const a=s.hood*46*DEG, ca=Math.cos(a), sa=Math.sin(a);
    part(out,(T)=>{
      const wear=wearTex(s.weather);
      // crowned top: two half-slabs meeting on a centre ridge
      const zc=G.hoodZc, zn=G.hoodZn, y0=1.84, y1=3.02;
      quad(T, [0,y0,zc+0.03],[G.hwHood,y0,zc],[G.hwHood,y1,zn],[0,y1,zn+0.03], 'paint', 0.34, 0,
        [[0,y0],[G.hwHood,y0],[G.hwHood,y1],[0,y1]], wear);
      quad(T, [-G.hwHood,y0,zc],[0,y0,zc+0.03],[0,y1,zn+0.03],[-G.hwHood,y1,zn], 'paint', 0.34, 0,
        [[-G.hwHood,y0],[0,y0],[0,y1],[-G.hwHood,y1]], wear);
      wallY(T, y1, -G.hwHood, G.hwHood, zn-0.06, zn, 'paint', 0.0, +1);      // front edge
      // underside (visible open)
      quad(T, [G.hwHood,y0,zc-0.01],[0,y0,zc+0.02],[0,y1,zn+0.02],[G.hwHood,y1,zn-0.01], 'leaf', -0.9);
      quad(T, [0,y0,zc+0.02],[-G.hwHood,y0,zc-0.01],[-G.hwHood,y1,zn-0.01],[0,y1,zn+0.02], 'leaf', -0.9);
    }, (p)=>rotX(p, 1.84, G.hoodZc, ca, sa));
  }

  // ---- cab shell (static parts: pillars, roof, rear wall, rocker, glass) ----
  function buildCab(out,s){
    const wear=wearTex(s.weather);
    // rocker
    for(const sx of [-1,1]) wallX(out, sx*G.hwRocker, G.cabRear, G.cowlY, 0.58, G.doorZ0, 'paint', sx>0?-0.1:-0.5, sx);
    // A-pillar zone + B/C pillars: lower body-colour strips at the two planes
    const strips=[ [G.doorF[1], G.cowlY], [G.doorR[1]-0.0, G.doorF[0]], [G.cabRear, G.doorR[0]] ];
    for(const sx of [-1,1]) for(const st of strips){
      const y0=Math.min(st[0],st[1]), y1=Math.max(st[0],st[1]);
      wallX(out, sx*G.hwSide, y0, y1, G.doorZ0, G.beltZ, 'paint', sx>0?0.18:-0.42, sx, [[y0,0],[y1,0],[y1,1],[y0,1]], wear);
      // upper (canted) pillar strip
      quad(out, sx>0?[gx(G.beltZ),y0,G.beltZ]:[-gx(G.beltZ),y1,G.beltZ],
                sx>0?[gx(G.beltZ),y1,G.beltZ]:[-gx(G.beltZ),y0,G.beltZ],
                sx>0?[gx(G.glassTop),y1,G.glassTop]:[-gx(G.glassTop),y0,G.glassTop],
                sx>0?[gx(G.glassTop),y0,G.glassTop]:[-gx(G.glassTop),y1,G.glassTop], 'paint', sx>0?0.12:-0.46);
    }
    // windshield + pillars + header (one raked plane)
    const wb={y:1.78,z:1.52}, wt={y:1.50,z:1.94};
    const wsQ=(x0b,x1b,x0t,x1t,mat,b)=> quad(out,[x1b,wb.y,wb.z],[x0b,wb.y,wb.z],[x0t,wt.y,wt.z],[x1t,wt.y,wt.z],mat,b);
    wsQ(-0.84,0.84,-0.76,0.76,'glass',-0.18);
    wsQ(0.84,0.92,0.76,0.84,'paint',0.10); wsQ(-0.92,-0.84,-0.84,-0.76,'paint',0.10);
    wallY(out, wt.y-0.01, -0.82, 0.82, wt.z, wt.z+0.02, 'paint', 0.1, +1);
    // roof
    boxAt(out, -G.hwRoof, G.hwRoof, G.cabRear, 1.50, G.glassTop, G.roofZ, 'paint', 0.05, false, wear);
    // clearance lamps
    for(let i=-2;i<=2;i++) boxAt(out, i*0.28-0.05, i*0.28+0.05, 1.42, 1.50, G.roofZ, G.roofZ+0.05, 'lensA', 0.35);
    // rear wall + window
    wallY(out, G.cabRear, -1.00, 1.00, G.doorZ0, G.beltZ, 'paint', -0.30, -1);
    wallY(out, G.cabRear, -0.83, 0.83, G.beltZ, G.glassTop, 'paint', -0.30, -1);
    wallY(out, G.cabRear-0.008, -0.66, 0.66, 1.46, 1.84, 'glass', -0.35, -1);
    // interior liner walls & floor (what an open door reveals)
    wallX(out, 0.90, G.cabRear+0.04, G.cowlY-0.06, 0.78, G.glassTop, 'shade', -0.9, -1);
    wallX(out,-0.90, G.cabRear+0.04, G.cowlY-0.06, 0.78, G.glassTop, 'shade', -0.9, +1);
    wallY(out, G.cabRear+0.04, -0.90, 0.90, 0.78, G.glassTop, 'shade', -0.9, +1);
    slab(out, [[-0.92,G.cabRear+0.04],[0.92,G.cabRear+0.04],[0.92,1.78],[-0.92,1.78]], 0.80, 'shade', -0.6);
  }

  function buildInterior(out,s){
    // dash + wheel
    boxAt(out, -0.90,0.90, 1.46, 1.76, 1.02, 1.40, 'dash', -0.4);
    tube(out, [-0.50,1.40,1.24],[-0.50,1.32,1.30], 0.17, 10, 'dash', -0.2);
    bar(out, [-0.50,1.44,1.10],[-0.50,1.34,1.26], 0.025, 'iron', -0.4);
    // seats: two buckets + rear bench
    for(const sx of [-1,1]){
      boxAt(out, sx*0.24, sx*0.74, 0.62, 1.12, 0.96, 1.14, 'cloth', -0.45);
      boxAt(out, sx*0.24, sx*0.74, 0.52, 0.68, 1.14, 1.60, 'cloth', -0.55);
    }
    boxAt(out, -0.82,0.82, -0.72,-0.22, 0.96, 1.14, 'cloth', -0.5);
    boxAt(out, -0.82,0.82, -0.86,-0.70, 1.14, 1.58, 'cloth', -0.6);
    boxAt(out, -0.14,0.14, 0.70, 1.24, 0.96, 1.22, 'dash', -0.5);            // console
  }

  // ---- doors (each hinged on its forward edge; mirrors ride the front doors) ----
  function buildDoors(out,s){
    const defs=[
      { y:G.doorF, side:+1, pose:s.dFR, mirror:true }, { y:G.doorF, side:-1, pose:s.dFL, mirror:true },
      { y:G.doorR, side:+1, pose:s.dRR, mirror:false }, { y:G.doorR, side:-1, pose:s.dRL, mirror:false },
    ];
    const wear=wearTex(s.weather);
    for(const d of defs){
      const sx=d.side, y0=d.y[0], y1=d.y[1];
      const a=sx*d.pose*62*DEG, ca=Math.cos(a), sa=Math.sin(a);
      const hx=sx*1.00, hy=y1;
      // jambs + sill (static, seen when open)
      wallY(out, y1, Math.min(sx*0.90,sx*1.02), Math.max(sx*0.90,sx*1.02), 0.72, G.glassTop, 'leaf', -0.5, -1);
      wallY(out, y0, Math.min(sx*0.90,sx*1.02), Math.max(sx*0.90,sx*1.02), 0.72, G.glassTop, 'leaf', -0.6, +1);
      slab(out, sx>0?[[0.90,y0],[1.02,y0],[1.02,y1],[0.90,y1]]:[[-1.02,y0],[-0.90,y0],[-0.90,y1],[-1.02,y1]], 0.70, 'leaf', -0.55);
      part(out,(T)=>{
        // lower skin: vertical band + belt tuck
        wallX(T, sx*G.hwSide, y0, y1, G.doorZ0, 1.12, 'paint', sx>0?0.18:-0.42, sx, [[y0,0],[y1,0],[y1,1.12],[y0,1.12]], wear);
        quad(T, sx>0?[G.hwSide,y0,1.12]:[-G.hwSide,y1,1.12],
                sx>0?[G.hwSide,y1,1.12]:[-G.hwSide,y0,1.12],
                sx>0?[gx(G.beltZ),y1,G.beltZ]:[-gx(G.beltZ),y0,G.beltZ],
                sx>0?[gx(G.beltZ),y0,G.beltZ]:[-gx(G.beltZ),y1,G.beltZ], 'paint', sx>0?0.22:-0.40);
        // glass band (frame strips proud)
        const g0=G.beltZ+0.06, g1=G.glassTop-0.04;
        const gq=(zA,zB,mat,b,off)=>{ const xa=gx(zA)+ (off||0), xb=gx(zB)+(off||0);
          quad(T, sx>0?[xa,y0+0.05,zA]:[-xa,y1-0.05,zA],
                  sx>0?[xa,y1-0.05,zA]:[-xa,y0+0.05,zA],
                  sx>0?[xb,y1-0.05,zB]:[-xb,y0+0.05,zB],
                  sx>0?[xb,y0+0.05,zB]:[-xb,y1-0.05,zB], mat, b); };
        gq(G.beltZ, G.glassTop, 'paint', sx>0?0.10:-0.48);                    // door upper shell
        gq(g0, g1, 'glass', sx>0?-0.15:-0.55, 0.012);                         // pane proud of shell
        // inner panel
        wallX(T, sx*0.92, y0+0.02, y1-0.02, 0.74, G.beltZ, 'leaf', -0.7, -sx);
        // handle
        boxAt(T, Math.min(sx*1.03,sx*1.065), Math.max(sx*1.03,sx*1.065), y0+0.10, y0+0.28, 1.26, 1.32, 'trim', 0.3);
        // tow mirror on the front doors
        if(d.mirror && s.mirrors){
          bar(T,[sx*1.02,y1-0.12,1.56],[sx*1.30,y1-0.16,1.66],0.022,'iron',-0.1);
          bar(T,[sx*1.02,y1-0.12,1.34],[sx*1.28,y1-0.18,1.44],0.020,'iron',-0.2);
          boxAt(T, Math.min(sx*1.24,sx*1.36), Math.max(sx*1.24,sx*1.36), y1-0.26, y1-0.06, 1.40, 1.78, 'paint', -0.05);
          wallY(T, y1-0.26, Math.min(sx*1.25,sx*1.35), Math.max(sx*1.25,sx*1.35), 1.44, 1.74, 'glass', -0.2, -1);
        }
      }, (p)=>hingeZ(p, hx, hy, ca, sa));
    }
  }

  // ---- bed, tailgate, flares ----
  function buildBed(out,s){
    const wear=wearTex(s.weather);
    for(const sx of [-1,1]){
      wallX(out, sx*G.hwSide, G.tailY, G.bedFront, 0.66, G.railZ, 'paint', sx>0?0.18:-0.42, sx, [[G.tailY,0],[G.bedFront,0],[G.bedFront,1],[G.tailY,1]], wear);
      boxAt(out, Math.min(sx*0.88,sx*G.hwSide), Math.max(sx*0.88,sx*G.hwSide), G.tailY, G.bedFront, G.railZ, G.railZ+0.05, 'paint', 0.12, false);
      wallX(out, sx*0.88, G.tailY+0.02, G.bedFront-0.06, G.bedFloorZ, G.railZ, 'rubber', -0.55, -sx);   // inner walls
      boxAt(out, Math.min(sx*0.56,sx*0.88), Math.max(sx*0.56,sx*0.88), G.axR-0.55, G.axR+0.55, G.bedFloorZ, 1.24, 'rubber', -0.35); // wheel tubs
    }
    wallY(out, G.bedFront, -G.hwSide, G.hwSide, 0.66, G.railZ, 'paint', 0.05, +1);
    boxAt(out, -G.hwSide, G.hwSide, G.bedFront-0.06, G.bedFront, G.railZ, G.railZ+0.05, 'paint', 0.12, false);
    wallY(out, G.bedFront-0.06, -0.88, 0.88, G.bedFloorZ, G.railZ, 'rubber', -0.55, -1);
    slab(out, [[-0.88,G.tailY+0.02],[0.88,G.tailY+0.02],[0.88,G.bedFront-0.06],[-0.88,G.bedFront-0.06]], G.bedFloorZ, 'rubber', -0.35, ribTex());
    // tail below the gate + corner caps with lamps
    wallY(out, G.tailY, -G.hwSide, G.hwSide, 0.66, 0.98, 'paint', -0.30, -1);
    for(const sx of [-1,1]){
      wallY(out, G.tailY, Math.min(sx*1.00,sx*G.hwSide), Math.max(sx*1.00,sx*G.hwSide), 0.98, G.railZ+0.05, 'paint', -0.30, -1);
      boxAt(out, Math.min(sx*0.90,sx*1.00), Math.max(sx*0.90,sx*1.00), G.tailY-0.03, G.tailY, 1.04, 1.42, 'lensR', 0.25, false);
    }
  }
  function buildGate(out,s){
    const a=s.gate*92*DEG, ca=Math.cos(a), sa=Math.sin(a);
    part(out,(T)=>{
      boxAt(T, -1.00, 1.00, G.tailY, G.tailY+0.05, 0.98, G.railZ, 'paint', 0.0, false, wearTex(s.weather));
      boxAt(T, -0.16, 0.16, G.tailY-0.015, G.tailY, 1.28, 1.38, 'trim', 0.3, false);  // handle
    }, (p)=>rotX(p, G.tailY+0.05, 1.00, ca, sa));
  }
  function buildFlares(out,s){
    const y0=-2.98, y1=-1.26;
    for(const sx of [-1,1]){
      archPanel(out, sx*G.flareHW, y0, y1, 0.62, G.flareTop, 'paint', sx>0?0.18:-0.42, sx,
        [{yc:G.axR, r:G.archR.r, zc:G.archR.zc}]);
      // top slope band (outward-up)
      if(sx>0) quad(out, [G.flareHW,y0,G.flareTop],[G.flareHW,y1,G.flareTop],[G.hwSide,y1,1.22],[G.hwSide,y0,1.22], 'paint', 0.30);
      else     quad(out, [-G.hwSide,y0,1.22],[-G.hwSide,y1,1.22],[-G.flareHW,y1,G.flareTop],[-G.flareHW,y0,G.flareTop], 'paint', 0.30);
      // front end cap (ramp toward the cab)
      if(sx>0) quad(out, [G.flareHW,y1,0.62],[G.hwSide,-1.12,0.62],[G.hwSide,-1.12,1.22],[G.flareHW,y1,G.flareTop], 'paint', 0.10);
      else     quad(out, [-G.hwSide,-1.12,0.62],[-G.flareHW,y1,0.62],[-G.flareHW,y1,G.flareTop],[-G.hwSide,-1.12,1.22], 'paint', -0.20);
      wallY(out, y0, Math.min(sx*G.hwSide,sx*G.flareHW), Math.max(sx*G.hwSide,sx*G.flareHW), 0.62, G.flareTop, 'paint', -0.30, -1);
      // arch liner (above the tire tops only) + markers
      wallX(out, sx*1.19, G.axR-G.archR.r, G.axR+G.archR.r, 0.84, 1.08, 'shade', -0.85, sx);
      boxAt(out, Math.min(sx*1.22,sx*1.25), Math.max(sx*1.22,sx*1.25), -1.44, -1.34, 0.88, 0.94, 'lensA', 0.3);
      boxAt(out, Math.min(sx*1.22,sx*1.25), Math.max(sx*1.22,sx*1.25), -2.90, -2.80, 0.88, 0.94, 'lensR', 0.3);
      if(s.mudflaps){
        wallY(out, -2.78, Math.min(sx*1.00,sx*1.22), Math.max(sx*1.00,sx*1.22), 0.10, 0.58, 'rubber', -0.4, -1);
        wallY(out, -2.775, Math.min(sx*1.00,sx*1.22), Math.max(sx*1.00,sx*1.22), 0.10, 0.58, 'rubber', -0.8, +1);
      }
    }
  }

  function buildSteps(out,s){
    if(!s.steps) return;
    for(const sx of [-1,1]){
      boxAt(out, Math.min(sx*1.04,sx*1.16), Math.max(sx*1.04,sx*1.16), -0.60, 1.60, 0.50, 0.55, 'galv', -0.1);
      for(const y of [-0.4,0.5,1.4]) bar(out,[sx*0.95,y,0.60],[sx*1.10,y,0.53],0.02,'iron',-0.3);
    }
  }

  // ---- wheels & axles (the rolling group — untouched by suspension) ----
  function wheelAt(out, xc, yc, sxOut, roll, hub){
    const r=G.wheelR, w=G.tireW, ph=roll*2*Math.PI;
    tube(out,[xc-w/2,yc,r],[xc+w/2,yc,r], r, 14, 'rubber', -0.05, true, treadTex(roll*2*Math.PI*r));
    if(!hub) return;
    const xf=xc+sxOut*(w/2+0.012);
    tube(out,[xc+sxOut*(w/2-0.02),yc,r],[xf,yc,r], r*0.58, 12, 'alloy', 0.25);
    // rotating detail: 8 lugs, 4 hand-holes, one index notch — the notch gives a full-rev period
    for(let k=0;k<8;k++){ const th=ph+k*Math.PI/4, py=yc+Math.cos(th)*0.155, pz=r+Math.sin(th)*0.155;
      tube(out,[xf-sxOut*0.005,py,pz],[xf+sxOut*0.028,py,pz],0.028,6,'galv',0.35); }
    for(let k=0;k<4;k++){ const th=ph+Math.PI/8+k*Math.PI/2, py=yc+Math.cos(th)*0.26, pz=r+Math.sin(th)*0.26;
      tube(out,[xf,py,pz],[xf+sxOut*0.012,py,pz],0.05,6,'rubber',-0.6); }
    { const th=ph+Math.PI/3, py=yc+Math.cos(th)*0.30, pz=r+Math.sin(th)*0.30;
      tube(out,[xf,py,pz],[xf+sxOut*0.02,py,pz],0.022,5,'iron',-0.4); }
    tube(out,[xf,yc,r],[xf+sxOut*0.045,yc,r],0.07,8,'chrome',0.4);           // centre cap
  }
  function buildWheels(out,s){
    tube(out,[-0.78,G.axF,G.wheelR],[0.78,G.axF,G.wheelR],0.055,8,'iron',-0.25);
    tube(out,[-0.98,G.axR,G.wheelR],[0.98,G.axR,G.wheelR],0.065,8,'iron',-0.25);
    tube(out,[-0.06,G.axR,G.wheelR],[0.26,G.axR,G.wheelR],0.16,10,'iron',-0.2);   // diff
    bar(out,[0.08,0.40,0.48],[0.08,G.axR+0.34,0.44],0.045,'iron',-0.3);           // driveshaft
    wheelAt(out,  G.frontWX, G.axF, +1, s.roll+s.wFR, true);
    wheelAt(out, -G.frontWX, G.axF, -1, s.roll+s.wFL, true);
    wheelAt(out,  G.rearWXin,  G.axR, +1, s.roll+s.wRR, false);
    wheelAt(out,  G.rearWXout, G.axR, +1, s.roll+s.wRR, true);
    wheelAt(out, -G.rearWXin,  G.axR, -1, s.roll+s.wRL, false);
    wheelAt(out, -G.rearWXout, G.axR, -1, s.roll+s.wRL, true);
  }

  function build(s){
    const body=[], rolling=[];
    buildFrame(body,s); buildFront(body,s); buildHood(body,s);
    buildCab(body,s); buildInterior(body,s); buildDoors(body,s);
    buildBed(body,s); buildGate(body,s); buildFlares(body,s); buildSteps(body,s);
    buildWheels(rolling,s);
    // suspension: the body drops toward whichever axle is compressed; extrapolates past the
    // axles so the bumpers pitch, which is what sells the travel.
    const dz=(y)=>{ const t=(y-G.axR)/(G.axF-G.axR); return -(s.susF*TF)*t - (s.susR*TR)*(1-t); };
    for(const f of body) f.v=f.v.map(p=>[p[0],p[1],p[2]+dz(p[1])]);
    return body.concat(rolling);
  }

  // ---- materials ----
  function makeMats(s){
    const wx=s.weather, night=s.night;
    const grime=r=>r.map(c=>mix(desat(c,wx*0.24),'#3a3128',wx*0.12));
    const rust =r=>r.map(c=>mix(c,'#6d3417',wx*0.26));
    const cool =r=>night?r.map(c=>mix(desat(c,0.30),'#1b2740',0.40)):r;
    const t=r=>cool(grime(r)), tm=r=>cool(rust(grime(r)));
    const paintRamp=BODY[s.paint]||BODY.white;
    return {
      paint :{ ramp:t(paintRamp), polish:0.22 },
      trim  :{ ramp:t(TRIM) },
      leaf  :{ ramp:cool(TRIM.map(c=>mix(desat(c,0.34),'#4a4f52',0.40))) },
      dash  :{ ramp:cool(RUBBER.map(c=>mix(c,'#4a4f52',0.20))) },
      cloth :{ ramp:cool(CLOTH) }, shade:{ ramp:cool(SHADE) },
      iron  :{ ramp:tm(IRON) }, galv:{ ramp:tm(GALV) },
      chrome:{ ramp:t(CHROME) }, alloy:{ ramp:t(CHROME.map(c=>desat(c,0.15))) },
      rubber:{ ramp:t(RUBBER) }, grille:{ ramp:t(IRON) },
      lensR :{ ramp:LENSR }, lensA:{ ramp:LENSA },
      head  :{ ramp:GLASSD.map(c=>mix(c,'#dfe6e2',0.35)) },
      glass :{ ramp:night?GLASSN:GLASSD },
      glow  :{ ramp:night?GLOW:['#5f6a5e','#8d9a8b','#b6c2b0','#d3ddcb'] },
    };
  }

  // ---- rasteriser (fleet recipe, verbatim) ----
  function paint(faces, B, MATS, s){
    const N=W*H, zbuf=new Float32Array(N).fill(Infinity), dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null), ibuf=new Int16Array(N), nbuf=new Array(N).fill(null);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]); let sh=shadeOf(n, B.se, B.ce);
      if(sh<0 && f.b<=-0.8) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const M=MATS[f.mat]||MATS.paint, ramp=M.ramp, tex=f.tex, uv=f.uv, flat=f.flat;
      const shIdx=(nn,sh0)=>{ let v=sh0*GAIN+BIAS+f.b;
        if(M.polish) v += M.polish*(1.55*nn[2] + 0.50*Math.max(0,1-Math.abs(nn[2])/0.30)*Math.max(0,Math.min(1,sh0*2)));
        return v; };
      let fidx=shIdx(n,sh);
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1],0,t,t+1);
      function fillTri(a,b,c,ia,ib,ic){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy); if(Math.abs(area)<1e-6) return;
        const ua=uv?uv[ia]:null, ub=uv?uv[ib]:null, uc=uv?uv[ic]:null;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){ const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area, w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area, w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-f.db, i=y*W+x;
          if(deff<zbuf[i]){ zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat;
            let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi+=tex(uu,vv); }
            let idx; if(flat){ idx=Math.round(fi); } else { const base=Math.floor(fi); idx=base+((fi-base)>BAYER[x&3][y&3]?1:0); }
            idx=Math.max(0,Math.min(ramp.length-1,idx)); rbuf[i]=ramp; ibuf[i]=idx; } }
      }
    }
    return { rbuf, ibuf, nbuf, dep };
  }
  function post(bufs, s){
    const { rbuf, ibuf, nbuf, dep }=bufs, N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx,ny=y+dy; if(nx>=W||ny>=H) continue; const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j; out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; } } }
    if(s.weather>0.02){ const rnd=mulberry32(9021);
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='paint'||m==='galv'||m==='iron'||m==='rubber') && rnd()<s.weather*0.05)
          out[i]=rbuf[i][Math.max(0,ibuf[i]-1)]; } }
    if(s.night){ for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x;
      if(nbuf[i]!=='glow' && nbuf[i]!=='glass') continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*W+(x+dx);
        if(out[j] && nbuf[j]!=='glow' && nbuf[j]!=='glass') out[j]=mix(out[j],'#f2c25e',nbuf[i]==='glow'?0.30:0.14); } } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; } }
    if(s.outline){ for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
      if(touch) out[i]=KEY; } }
    return out;
  }
  function toRGBA(cols){ const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; }
      const [r,g,b]=hex2rgb(c); rgba[i*4]=r;rgba[i*4+1]=g;rgba[i*4+2]=b;rgba[i*4+3]=255; }
    return rgba;
  }

  function render(dir, opts){ opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const s=resolve(opts), B=camBasis({dir,elev:opts.elev});
    return toRGBA(post(paint(build(s), B, makeMats(s), s), s));
  }
  function frames(dir, n, opts, cue){ n=n||8; const fn=CUES[cue||'doors']||CUES.doors, out=[];
    const cyclic = (cue==='roll'||cue==='bounce');
    for(let i=0;i<n;i++){ const t = cyclic ? i/n : i/(n-1);
      out.push(render(dir, Object.assign({}, opts, fn(t)))); }
    return out;
  }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }
  function anchors(dir, opts){ opts=opts||{}; const s=resolve(opts), e=opts.elev;
    const P=(p)=>{ const q=project(dir,p,e); return { x:q.x, y:q.y, m:p }; };
    return {
      hitch:P([0,-3.36,0.55]), gate:P([0,G.tailY,1.42]), bed:P([0,(G.tailY+G.bedFront)/2,G.bedFloorZ]),
      hoodLatch:P([0,G.noseY-0.10,G.hoodZn]),
      doorFL:P([-1.05,G.doorF[0]+0.18,1.29]), doorFR:P([1.05,G.doorF[0]+0.18,1.29]),
      doorRL:P([-1.05,G.doorR[0]+0.18,1.29]), doorRR:P([1.05,G.doorR[0]+0.18,1.29]),
      wheelFL:P([-G.frontWX,G.axF,G.wheelR]), wheelFR:P([G.frontWX,G.axF,G.wheelR]),
      wheelRL:P([-G.rearWXout,G.axR,G.wheelR]), wheelRR:P([G.rearWXout,G.axR,G.wheelR]),
      exhaust:P([0.62,-3.30,0.27]),
      bodyL:s.B.bodyL, loa:s.B.loa, width:s.B.width, height:s.B.height, wheelbase:s.B.wheelbase,
    };
  }
  function list(){ return Object.keys(BODIES); }

  root.VehicleIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    BODY, TRIM, IRON, GALV, RUBBER, CHROME, CLOTH, GLASSD, GLASSN, KEY,
    BODIES, PRESETS, CUES, G, travel:{F:TF,R:TR},
    list, dims, resolve, render, frames, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
