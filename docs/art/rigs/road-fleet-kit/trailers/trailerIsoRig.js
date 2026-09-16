/* Hidden Harbours — parametric ISO TRAILER rig, FOUR TOWED BODIES (same turntable + camera +
   shading as aeroSemiIsoRig.js / classicSemiIsoRig.js / the fleet). Bodies: flatbed28, flatbed53,
   reefer28, reefer53 — the coupling MATES of the two semi tractors. Trailers are their own sprites:
   the GAME hinges anchors().kingpin to a tractor's anchors().fifthWheel and articulates the pair.
   45deg steps, elev 40deg, flat-facet shading from the fixed upper-LEFT key, z-buffered, ordered
   dither, NO AA, 32 px = 1 m, ringless from birth (ADR 0031).

   THE HANDSHAKE (locked to both tractors, asserted in every harness):
     width 2.44 m (hw 1.22)          — wider breaks the tractors' 4 mm jackknife margin
     kingpin set 0.90 m aft of nose  — sqrt(1.22^2 + 0.90^2) = 1.516 m nose swing < 1.52 gap
     coupled deck plane z 1.18       — the fifth-wheel plate top; the trailers BAKE at ride height
     reefer unit swing r 1.30 m      — the nose box stays inside the 1.516 m corner swing

   CELLS — two, both ground-row-true:
     flatbed28 / reefer28   384 x 320 @ 192,214  (the road cell — pups park like the vans)
     flatbed53 / reefer53   640 x 480 @ 320,300  (16.15 m needs width AND depth; own ground row)

   ARTICULATION — pose params on render(dir,opts), 0..1 unless noted:
     gear              landing gear, 0 legs up (COUPLED) -> 1 legs down with sand shoes grounded
                       (PARKED, the bake default). Couple the trailer, set gear 0.
     barnL barnR       reefer rear barn doors, hinged at their OUTER edges, 0 -> 255deg — they
                       swing out, back and nearly flat against the sides for dock work.
                       Flatbeds clamp these to 0.
     roll              master wheel roll, REVOLUTIONS (cyclic); wL wR per-side offsets
                       (each side's axles share the roll, like the tractors' tandems)
     sus               suspension, -1..1: the BODY drops over the axle group, pivoting from the
                       kingpin (a coupled nose rides the tractor). Wheels stay on the ground.
     yaw               heading off the 45deg grid, DEGREES (-45..45), rebaked under the fixed key.
   Parts (not poses): mudflaps, headboard (flatbeds: the front bulkhead; default true).

   ORIGIN / PIVOT: ground-centre of the BODY footprint (reefer unit overhang excluded).
   +x curb side, +y NOSE (kingpin end), +z up. dir 0 (N) shows the REAR (doors/ICC bar).

   Exposes globalThis.TrailerIso = { W,H,PX,DIRS,pivot,order,defaultElev, CELLS,cellFor,pivotFor,
     BODY,TRIM,IRON,GALV,RUBBER,CHROME,WOOD,GLASSD,GLASSN,KEY, BODIES,PRESETS,CUES,G,travel,
     list(), dims(opts), resolve(opts), render(dir,opts), frames(dir,n,opts,cue),
     anchors(dir,opts), project(dir,p,elev,yaw,body) }. */
(function (root) {
  const PX = 32, S = 32;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;                    // ADR 0031

  const CELLS = { road:{W:384,H:320,cx:192,gy:214}, long:{W:640,H:480,cx:320,gy:300} };
  const SPECS = {
    flatbed28: { kind:'flatbed', L:8.53,  axles:[-2.90],        cell:CELLS.road, label:'28 ft Flatbed Pup' },
    flatbed53: { kind:'flatbed', L:16.15, axles:[-5.50,-6.70],  cell:CELLS.long, label:'53 ft Flatbed' },
    reefer28:  { kind:'reefer',  L:8.53,  axles:[-2.90],        cell:CELLS.road, label:'28 ft Reefer Pup' },
    reefer53:  { kind:'reefer',  L:16.15, axles:[-5.50,-6.70],  cell:CELLS.long, label:'53 ft Reefer' },
  };

  // ---- ramps (harbour master ramps, shared with the fleet) + deck lumber ----
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
  const SHADE  = ['#0b0e11','#0f1418','#141a1f','#1a2128','#212a31','#28323a'];
  const WOOD   = ['#33261a','#473424','#5c442e','#71543a','#866647','#9b7856'];
  const GLASSD = ['#1b262b','#243238','#2f4149','#3d545c','#5d7b82','#96b6ba'];
  const GLASSN = ['#141d2b','#1d2a3d','#2a3c53','#3d5570','#6b7f9c','#95a8c0'];
  const GLOW   = ['#7a5a18','#c09a2c','#efd06a','#fdf0b6'];
  const LENSR  = ['#3a0c0a','#5a120e','#7d1c14','#a52a1d','#c93c2a','#e4573f'];
  const LENSA  = ['#4a2c07','#6d420b','#8f5a12','#b0771f','#cc9633','#e5b455'];
  const KEY    = '#1a1c22';

  // ---- shading (identical recipe to the fleet) ----
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }

  function camBasis(opts){ const dir=opts.dir||0, th=dir*Math.PI/4 + (opts.yaw||0)*DEG, e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { th, ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e), cell:opts.cell||CELLS.long }; }
  function projVert(x,y,z,B){ const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:B.cell.cx+xr*S, sy:B.cell.gy-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) }; }
  function normal(a,b,c){ const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx; const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m]; }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }

  // ---- face builders (fleet conventions) ----
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }
  function quad(out,a,b,c,d,mat,bi,db,uv,tex,flat){ out.push(F([a,b,c,d],mat,bi,db,uv,tex,flat)); }
  function slab(out, pts, z, mat, b, tex){ const uv=tex?pts.map(p=>[p[0],p[1]]):null;
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0, uv, tex)); }
  function wallX(out,x,y0,y1,z0,z1,mat,b,sgn,uv,tex){
    if(sgn>0) quad(out,[x,y0,z0],[x,y1,z0],[x,y1,z1],[x,y0,z1],mat,b,0,uv,tex);
    else      quad(out,[x,y1,z0],[x,y0,z0],[x,y0,z1],[x,y1,z1],mat,b,0,uv,tex);
  }
  function texWallX(out,x,y0,y1,z0,z1,mat,b,sgn,tex){
    const uv = sgn>0 ? [[y0,z0],[y1,z0],[y1,z1],[y0,z1]] : [[y1,z0],[y0,z0],[y0,z1],[y1,z1]];
    wallX(out,x,y0,y1,z0,z1,mat,b,sgn,uv,tex);
  }
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
  function seamWearTex(w){ const p=1.14; return (u,v)=>{ const f=((u%p)+p)%p; if(f<0.04) return -1;
    if(w>0.03 && hash2(Math.floor(u*6.5)|0, Math.floor(v*6.5)|0) < w*0.10) return -1; return 0; }; }
  function plankTex(w){ const p=0.305; return (u,v)=>{ const f=((u%p)+p)%p; if(f<0.035) return -1;
    if(w>0.03 && hash2(Math.floor(u*3.3)|0, Math.floor(v*3.3)|0) < w*0.14) return -1; return 0; }; }
  function ribTex(){ const p=0.15; return (u,v)=>{ const f=((u%p)+p)%p; return f<0.055?-1:0; }; }
  function grilleTex(){ const p=0.082; return (u,v)=>{ const f=((v%p)+p)%p; return f<0.034?2:0; }; }
  function treadTex(phase){ const c=0.1083; return (u,v)=>{ const f=(((u+phase)%c)+c)%c; return f<c*0.42?-1:0; }; }
  // c = 0.1083 m puts 29.008 stripe periods on the 3.1416 m circumference — invisible loop seam.

  // ================= GEOMETRY =================
  const TRAV=0.10;                                  // suspension travel over the axle group, metres
  const G = {
    hw:1.22, deckZ:1.18, kpSet:0.90, kpR:0.045, kpZ:[1.04,1.16],
    wheelR:0.50, tireW:0.30, dualXi:0.60, dualXo:0.90,
    gearX:0.95, gearAft:2.00,
    fl:{ railZ:[0.78,1.10], woodZ:[1.12,1.18], rubZ:[1.00,1.12], headZ:2.35 },
    rf:{ floorZ:1.30, roofZ:4.06, ceilZ:3.98, doorZ:[1.30,3.92], headerZ:3.92, sillZ:[1.18,1.30],
         unit:{hw:0.55, out:0.28, z:[1.62,2.95]}, doorDeg:255 },
  };
  const kpY=(S)=> S.L/2 - G.kpSet;
  const axC=(S)=> S.axles.reduce((a,b)=>a+b,0)/S.axles.length;

  const BODIES = {};
  for(const k of Object.keys(SPECS)){ const S=SPECS[k], fb=S.kind==='flatbed';
    BODIES[k] = { key:k, label:S.label, kind:'trailer_'+S.kind,
      loa:+(S.L + (fb?0:G.rf.unit.out)).toFixed(2), bodyL:S.L, width:+(G.hw*2).toFixed(2),
      height: fb? G.fl.headZ : +(G.rf.roofZ+0.04).toFixed(2),
      deck: fb? G.deckZ : G.rf.floorZ,
      kingpinY:+kpY(S).toFixed(3), kingpinSet:G.kpSet, coupledDeckZ:G.deckZ,
      noseSwingR:+Math.hypot(G.hw,G.kpSet).toFixed(3),
      tailSwingR:+(S.L-G.kpSet).toFixed(2),
      kingpinToAxleCentre:+(kpY(S)-axC(S)).toFixed(3),
      axles:S.axles.slice(), wheels:S.axles.length*4 };
  }

  const PRESETS = {
    parked:    { gear:1, weather:0.45 },
    coupled:   { gear:0, weather:0.45 },
    dockOpen:  { gear:1, barnL:1, barnR:1, weather:0.40 },
    harbourLine:{ gear:1, paint:'teal', weather:0.30 },
    workedHard:{ gear:1, paint:'rustOrange', weather:0.85 },
  };
  const CUES = {
    gear:  (t)=>({ gear:t }),
    doors: (t)=>({ barnL:t, barnR:t }),
    roll:  (t)=>({ roll:t, gear:0 }),                       // rolling => coupled, gear up
    bounce:(t)=>({ sus:Math.sin(t*Math.PI*2)*0.7, roll:t, gear:0 }),
    turn:  (t)=>({ yaw:Math.sin(t*Math.PI*2)*10, roll:t, gear:0 }),
  };

  function resolve(opts){
    opts=opts||{};
    const g=(k,d)=> opts[k]!=null?opts[k]:d;
    const c01=(v)=>Math.max(0,Math.min(1,v)), c11=(v)=>Math.max(-1,Math.min(1,v));
    const body = SPECS[opts.body] ? opts.body : 'reefer53';
    const S=SPECS[body];
    return {
      body, S, B:BODIES[body], cell:S.cell, kind:S.kind,
      paint: opts.paint||'white', weather:g('weather',0.35),
      gear:c01(g('gear',1)),
      barnL: S.kind==='reefer'?c01(g('barnL',0)):0, barnR: S.kind==='reefer'?c01(g('barnR',0)):0,
      roll:g('roll',0), wL:g('wL',0), wR:g('wR',0),
      sus:c11(g('sus',0)), yaw:Math.max(-45,Math.min(45,g('yaw',0))),
      mudflaps:g('mudflaps',true), headboard: S.kind==='flatbed'?g('headboard',true):false,
      night:!!opts.night, outline: opts.outline!=null?!!opts.outline:KEYLINE_DEFAULT };
  }
  function dims(opts){ const s=resolve(opts);
    return Object.assign({ travel:TRAV }, s.B, { gearDown:s.gear>0.5, headboard:s.headboard }); }

  const hingeZ=(p,hx,hy,ca,sa)=>{ const dx=p[0]-hx, dy=p[1]-hy; return [hx+dx*ca-dy*sa, hy+dx*sa+dy*ca, p[2]]; };
  function part(out, fn, xf){
    const T=[]; fn(T);
    if(xf) for(const f of T) f.v=f.v.map(xf);
    for(const f of T) out.push(f);
  }

  // ---- chassis shared by all four: rails, coupler, kingpin, gear, rear frame, lamps ----
  function buildChassis(out,s){
    const S=s.S, hl=S.L/2, yKp=kpY(S);
    for(const sx of [-1,1]) boxAt(out, sx*0.47-0.045, sx*0.47+0.045, -hl+0.10, hl-0.06, G.fl.railZ[0], G.fl.railZ[1], 'iron', -0.15);
    const nX=Math.ceil(S.L/1.45);
    for(let i=0;i<=nX;i++){ const y=-hl+0.30+(S.L-0.60)*i/nX; boxAt(out,-0.47,0.47,y-0.045,y+0.045,G.fl.railZ[0],G.fl.railZ[0]+0.10,'iron',-0.3); }
    // upper coupler apron + kingpin (the coupling half)
    boxAt(out, -1.00,1.00, hl-1.80, hl-0.04, 1.10, G.deckZ-0.005, 'galv', -0.15);
    tube(out,[0,yKp,G.kpZ[0]],[0,yKp,G.kpZ[1]],G.kpR,8,'chrome',0.2,true);
    tube(out,[0,yKp,G.kpZ[0]],[0,yKp,G.kpZ[0]+0.035],G.kpR+0.02,8,'iron',-0.2,false);   // pin flange
    // landing gear: legs, sand shoes, crossbrace, crank (street side)
    const yG=yKp-G.gearAft, drop=0.25+s.gear*0.78, foot=1.10-drop;
    for(const sx of [-1,1]){
      boxAt(out, sx*G.gearX-0.05, sx*G.gearX+0.05, yG-0.05, yG+0.05, foot+0.06, 1.12, 'galv', -0.2);
      boxAt(out, sx*G.gearX-0.07, sx*G.gearX+0.07, yG-0.09, yG+0.09, foot, foot+0.07, 'iron', -0.25);  // shoe
      bar(out, [sx*G.gearX, yG-0.04, 1.10],[sx*0.47, yG+0.55, G.fl.railZ[0]+0.02], 0.022,'iron',-0.35);
    }
    bar(out, [-G.gearX+0.06,yG,foot+0.30],[G.gearX-0.06,yG,foot+0.30],0.022,'iron',-0.3);
    bar(out, [-G.gearX-0.02,yG+0.02,1.02],[-G.gearX-0.16,yG+0.02,1.02],0.018,'galv',-0.1);            // crank
    tube(out, [-G.gearX-0.16,yG-0.05,1.02],[-G.gearX-0.16,yG+0.09,1.02],0.030,6,'galv',-0.15);
    // glad-hand stub + line on the nose face
    boxAt(out, -0.22,0.22, hl-0.06, hl-0.02, 1.42, 1.56, 'iron', -0.2);
    bar(out, [0.10,hl-0.05,1.42],[0.16,hl-0.30,1.22],0.018,'rubber',-0.35);
    // rear: ICC bar, verticals, tail lamps, conspicuity strip
    boxAt(out, -0.94,0.94, -hl+0.02, -hl+0.08, 0.40, 0.48, 'iron', -0.1);
    for(const sx of [-1,1]) bar(out,[sx*0.48,-hl+0.30,G.fl.railZ[0]],[sx*0.48,-hl+0.05,0.46],0.032,'iron',-0.3);
    for(const sx of [-1,1])
      boxAt(out, Math.min(sx*0.98,sx*1.18), Math.max(sx*0.98,sx*1.18), -hl-0.005, -hl+0.05, 0.78, 0.96, 'lensR', 0.25);
    if(s.mudflaps){ const yM=S.axles[S.axles.length-1]-0.72;
      for(const sx of [-1,1]){
        wallY(out, yM, Math.min(sx*0.52,sx*1.08), Math.max(sx*0.52,sx*1.08), 0.06, 0.50, 'rubber', -0.4, -1);
        wallY(out, yM-0.005, Math.min(sx*0.52,sx*1.08), Math.max(sx*0.52,sx*1.08), 0.06, 0.50, 'rubber', -0.8, +1);
      }
    }
    // suspension hangers over each axle
    for(const ay of S.axles) boxAt(out, -0.60,0.60, ay-0.28, ay+0.28, 0.62, G.fl.railZ[0]+0.02, 'iron', -0.35);
  }

  // ---- flatbed: plank deck, rub rails, stake pockets, winches, headboard ----
  function buildFlatbed(out,s){
    const S=s.S, hl=S.L/2, wear=wearTex(s.weather);
    slab(out, [[-G.hw,-hl],[G.hw,-hl],[G.hw,hl],[-G.hw,hl]], G.deckZ, 'wood', 0.10, plankTex(s.weather));
    wallY(out, hl, -G.hw, G.hw, G.fl.rubZ[0], G.deckZ, 'paint', 0.08, +1, null, wear);       // nose band
    wallY(out, -hl, -G.hw, G.hw, G.fl.rubZ[0], G.deckZ, 'paint', -0.30, -1, null, wear);     // tail band
    for(const sx of [-1,1]){
      texWallX(out, sx*G.hw, -hl, hl, G.fl.rubZ[0], G.deckZ, 'paint', sx>0?0.18:-0.42, sx, wear);   // rub rail
      const nP=Math.floor(S.L/1.35);
      for(let i=0;i<nP;i++){ const y=-hl+0.85+i*1.35;
        boxAt(out, Math.min(sx*(G.hw-0.01),sx*(G.hw+0.015)), Math.max(sx*(G.hw-0.01),sx*(G.hw+0.015)), y-0.09, y+0.09, G.fl.rubZ[0]+0.01, G.fl.rubZ[1]-0.01, 'galv', -0.1); }
    }
    for(let i=0;i<Math.floor(S.L/2.6);i++){ const y=-hl+1.5+i*2.6;                            // winches, street
      boxAt(out, -G.hw-0.02, -G.hw+0.06, y-0.10, y+0.10, 0.88, 1.00, 'galv', -0.25); }
    if(s.headboard){
      boxAt(out, -1.10,1.10, hl-0.10, hl-0.02, G.deckZ, G.fl.headZ, 'paint', 0.0, false, wear);
      for(const mx of [-0.55,0,0.55]) boxAt(out, mx-0.03, mx+0.03, hl-0.115, hl-0.10, G.deckZ+0.05, G.fl.headZ-0.05, 'paint', -0.25);
    }
  }

  // ---- reefer: insulated box, rear frame + barn doors, nose unit, markers ----
  function buildReefer(out,s){
    const S=s.S, hl=S.L/2, R=G.rf, sw=seamWearTex(s.weather);
    for(const sx of [-1,1]){
      texWallX(out, sx*G.hw, -hl+0.06, hl, G.deckZ, R.roofZ, 'paint', sx>0?0.18:-0.42, sx, sw);
      wallX(out, sx*G.hw, -hl+0.06, hl, 1.06, G.deckZ, 'galv', sx>0?0.02:-0.5, sx);          // bottom rail
    }
    wallY(out, hl, -G.hw, G.hw, G.deckZ, R.roofZ, 'paint', 0.10, +1);                         // nose wall
    boxAt(out, -G.hw, G.hw, -hl+0.04, hl, R.roofZ-0.02, R.roofZ, 'trim', 0.10, false, wearTex(s.weather));
    // bay interior (seen through open doors): T-floor, lined walls, ceiling
    slab(out, [[-1.16,-hl+0.04],[1.16,-hl+0.04],[1.16,hl-0.06],[-1.16,hl-0.06]], R.floorZ, 'galv', -0.35, ribTex());
    wallX(out, 1.17, -hl+0.04, hl-0.06, R.floorZ, R.ceilZ, 'shade', -0.9, -1);
    wallX(out,-1.17, -hl+0.04, hl-0.06, R.floorZ, R.ceilZ, 'shade', -0.9, +1);
    wallY(out, hl-0.06, -1.17, 1.17, R.floorZ, R.ceilZ, 'shade', -0.9, -1);
    slab(out, [[-1.16,-hl+0.04],[1.16,-hl+0.04],[1.16,hl-0.06],[-1.16,hl-0.06]], R.ceilZ, 'shade', -1.0);
    // rear frame: posts, header, sill
    for(const sx of [-1,1])
      wallY(out, -hl+0.04, Math.min(sx*1.16,sx*G.hw), Math.max(sx*1.16,sx*G.hw), G.deckZ, R.roofZ, 'paint', -0.30, -1);
    wallY(out, -hl+0.04, -1.16, 1.16, R.headerZ, R.roofZ, 'paint', -0.30, -1);
    boxAt(out, -1.16, 1.16, -hl+0.015, -hl+0.06, R.sillZ[0], R.sillZ[1], 'galv', -0.05);
    // barn doors: hinged at outer edges, 0 -> 255deg, lock rods + handles
    for(const d of [ {sx:+1, pose:s.barnR}, {sx:-1, pose:s.barnL} ]){
      const sx=d.sx, a=sx*d.pose*R.doorDeg*DEG, ca=Math.cos(a), sa=Math.sin(a);
      part(out,(T)=>{
        const x0=Math.min(sx*0.015,sx*1.16), x1=Math.max(sx*0.015,sx*1.16);
        boxAt(T, x0, x1, -hl-0.01, -hl+0.04, R.doorZ[0], R.doorZ[1], 'paint', -0.12, false, wearTex(s.weather));
        for(const rx of [0.38,0.80]){ const px=sx*rx;
          bar(T,[px,-hl-0.025,R.doorZ[0]+0.10],[px,-hl-0.025,R.doorZ[1]-0.10],0.020,'chrome',0.05);
          boxAt(T, px-0.05, px+0.05, -hl-0.045, -hl-0.02, 1.90, 2.02, 'galv', -0.1); }
      }, (p)=>hingeZ(p, sx*1.19, -hl+0.02, ca, sa));
    }
    // nose refrigeration unit — inside the 1.516 m swing (r = 1.30 m at the corners)
    const U=R.unit;
    boxAt(out, -U.hw, U.hw, hl, hl+U.out, U.z[0], U.z[1], 'trim', 0.05, false, wearTex(s.weather));
    out.push(F([[U.hw-0.06,hl+U.out+0.001,U.z[0]+0.12],[-U.hw+0.06,hl+U.out+0.001,U.z[0]+0.12],
                [-U.hw+0.06,hl+U.out+0.001,U.z[1]-0.12],[U.hw-0.06,hl+U.out+0.001,U.z[1]-0.12]],
      s.night?'glow':'grille', -0.05, 0,
      [[U.hw-0.06,U.z[0]+0.12],[-U.hw+0.06,U.z[0]+0.12],[-U.hw+0.06,U.z[1]-0.12],[U.hw-0.06,U.z[1]-0.12]], grilleTex()));
    boxAt(out, -0.30,0.30, hl+0.02, hl+0.20, 1.30, U.z[0], 'iron', -0.3);                     // fuel/battery pack under unit
    // marker lamps: three amber front top, red rear corners
    for(const mx of [-0.85,0,0.85]) boxAt(out, mx-0.045, mx+0.045, hl-0.005, hl+0.03, R.roofZ-0.20, R.roofZ-0.12, 'lensA', 0.3);
    for(const sx of [-1,1]) boxAt(out, Math.min(sx*1.10,sx*1.18), Math.max(sx*1.10,sx*1.18), -hl+0.02, -hl+0.07, R.roofZ-0.20, R.roofZ-0.12, 'lensR', 0.25);
  }

  // ---- wheels: duals both sides of every axle, 10-lug hubs ----
  function wheelAt(out, xc, yc, sxOut, roll){
    const r=G.wheelR, w=G.tireW, ph=roll*2*Math.PI;
    tube(out,[xc-w/2,yc,r],[xc+w/2,yc,r], r, 14, 'rubber', -0.05, true, treadTex(roll*2*Math.PI*r));
    const xf=xc+sxOut*(w/2+0.012);
    tube(out,[xc+sxOut*(w/2-0.02),yc,r],[xf,yc,r], r*0.56, 12, 'alloy', 0.25);
    for(let k=0;k<10;k++){ const th=ph+k*Math.PI/5, py=yc+Math.cos(th)*0.175, pz=r+Math.sin(th)*0.175;
      tube(out,[xf-sxOut*0.005,py,pz],[xf+sxOut*0.028,py,pz],0.026,6,'galv',0.35); }
    for(let k=0;k<5;k++){ const th=ph+Math.PI/10+k*2*Math.PI/5, py=yc+Math.cos(th)*0.30, pz=r+Math.sin(th)*0.30;
      tube(out,[xf,py,pz],[xf+sxOut*0.012,py,pz],0.050,6,'rubber',-0.6); }
    { const th=ph+Math.PI/3, py=yc+Math.cos(th)*0.345, pz=r+Math.sin(th)*0.345;
      tube(out,[xf,py,pz],[xf+sxOut*0.02,py,pz],0.022,5,'iron',-0.4); }
    tube(out,[xf,yc,r],[xf+sxOut*0.05,yc,r],0.070,8,'chrome',0.4);
  }
  function buildWheels(out,s){
    const S=s.S;
    for(const ay of S.axles){
      tube(out,[-0.88,ay,G.wheelR],[0.88,ay,G.wheelR],0.068,8,'iron',-0.25);
      const ci=G.dualXi, w=G.tireW;
      for(const sx of [-1,1]){
        tube(out,[sx*ci-w/2,ay,G.wheelR],[sx*ci+w/2,ay,G.wheelR], G.wheelR, 14, 'rubber', -0.20, true,
          treadTex((s.roll+(sx>0?s.wR:s.wL))*2*Math.PI*G.wheelR));
        wheelAt(out, sx*G.dualXo, ay, sx, s.roll+(sx>0?s.wR:s.wL));
      }
    }
  }

  function build(s){
    const body=[], rolling=[];
    buildChassis(body,s);
    if(s.kind==='flatbed') buildFlatbed(body,s); else buildReefer(body,s);
    buildWheels(rolling,s);
    const S=s.S, yKp=kpY(S), yAx=axC(S);
    const dz=(y)=>{ const t=Math.max(0,(yKp-y)/(yKp-yAx)); return -(s.sus*TRAV)*t; };
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
      wood  :{ ramp:cool(rust(WOOD)) },
      iron  :{ ramp:tm(IRON) }, galv:{ ramp:tm(GALV) },
      chrome:{ ramp:t(CHROME), polish:0.3 }, alloy:{ ramp:t(CHROME.map(c=>desat(c,0.15))) },
      rubber:{ ramp:t(RUBBER) }, shade:{ ramp:cool(SHADE) }, grille:{ ramp:t(IRON) },
      lensR :{ ramp:LENSR }, lensA:{ ramp:LENSA },
      glass :{ ramp:night?GLASSN:GLASSD },
      glow  :{ ramp:night?GLOW:['#5f6a5e','#8d9a8b','#b6c2b0','#d3ddcb'] },
    };
  }

  // ---- rasteriser (fleet recipe, per-body cell) ----
  function paint(faces, B, MATS, s){
    const W=B.cell.W, H=B.cell.H;
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
    return { rbuf, ibuf, nbuf, dep, W, H };
  }
  function post(bufs, s){
    const { rbuf, ibuf, nbuf, dep, W, H }=bufs, N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx,ny=y+dy; if(nx>=W||ny>=H) continue; const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j; out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; } } }
    if(s.weather>0.02){ const rnd=mulberry32(9021);
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='paint'||m==='galv'||m==='iron'||m==='rubber'||m==='wood') && rnd()<s.weather*0.05)
          out[i]=rbuf[i][Math.max(0,ibuf[i]-1)]; } }
    if(s.night){ for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x;
      if(nbuf[i]!=='glow') continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*W+(x+dx);
        if(out[j] && nbuf[j]!=='glow') out[j]=mix(out[j],'#f2c25e',0.30); } } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; } }
    if(s.outline){ for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
      if(touch) out[i]=KEY; } }
    return { out, W, H };
  }
  function toRGBA(res){ const {out,W,H}=res; const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=out[i]; if(!c){ rgba[i*4+3]=0; continue; }
      const [r,g,b]=hex2rgb(c); rgba[i*4]=r;rgba[i*4+1]=g;rgba[i*4+2]=b;rgba[i*4+3]=255; }
    return rgba;
  }

  function render(dir, opts){ opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const s=resolve(opts), B=camBasis({dir,elev:opts.elev,yaw:s.yaw,cell:s.cell});
    return toRGBA(post(paint(build(s), B, makeMats(s), s), s));
  }
  function frames(dir, n, opts, cue){ n=n||8; const fn=CUES[cue||'gear']||CUES.gear, out=[];
    const cyclic = (cue==='roll'||cue==='bounce'||cue==='turn');
    for(let i=0;i<n;i++){ const t = cyclic ? i/n : i/(n-1);
      out.push(render(dir, Object.assign({}, opts, fn(t)))); }
    return out;
  }
  function project(dir, p, elev, yaw, body){ const S=SPECS[body]||SPECS.reefer53;
    const v=projVert(p[0],p[1],p[2],camBasis({dir,elev,yaw,cell:S.cell})); return {x:v.sx,y:v.sy}; }
  function anchors(dir, opts){ opts=opts||{}; const s=resolve(opts), e=opts.elev;
    const S=s.S, hl=S.L/2, yKp=kpY(S), lastAx=S.axles[S.axles.length-1];
    const P=(p)=>{ const q=project(dir,p,e,s.yaw,s.body); return { x:q.x, y:q.y, m:p }; };
    const A={
      kingpin:P([0,yKp,G.deckZ]), gladHands:P([0,hl-0.04,1.49]),
      gearCrank:P([-G.gearX-0.16,yKp-G.gearAft,1.02]),
      rear:P([0,-hl,s.kind==='flatbed'?G.deckZ:G.rf.floorZ]), icc:P([0,-hl+0.05,0.44]),
      wheelL:P([-G.dualXo,lastAx,G.wheelR]), wheelR:P([G.dualXo,lastAx,G.wheelR]),
      bodyL:S.L, loa:s.B.loa, width:s.B.width, height:s.B.height, kingpinY:yKp,
    };
    if(s.kind==='reefer'){
      A.unit=P([0,hl+G.rf.unit.out,2.28]); A.roof=P([0,0,G.rf.roofZ]);
      A.doorL=P([-0.80,-hl-0.03,1.96]); A.doorR=P([0.80,-hl-0.03,1.96]);
    } else {
      A.deck=P([0,0,G.deckZ]); A.headboard=P([0,hl-0.06,G.fl.headZ]);
    }
    return A;
  }
  function list(){ return Object.keys(BODIES); }
  const cellFor=(body)=> (SPECS[body]||SPECS.reefer53).cell;
  const pivotFor=(body)=>{ const c=cellFor(body); return {x:c.cx,y:c.gy}; };

  root.TrailerIso = { W:CELLS.long.W, H:CELLS.long.H, PX, DIRS:8,
    pivot:{x:CELLS.long.cx,y:CELLS.long.gy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    CELLS, cellFor, pivotFor,
    BODY, TRIM, IRON, GALV, RUBBER, CHROME, WOOD, GLASSD, GLASSN, KEY,
    BODIES, PRESETS, CUES, G, travel:{group:TRAV},
    list, dims, resolve, render, frames, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
