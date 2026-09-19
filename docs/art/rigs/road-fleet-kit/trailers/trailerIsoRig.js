/* PASS 2 — sculpted body assemblies and mechanical finish. See PASS-2.md. */
/* REVISED COPY — 2026-09-13. See ART-CHANGES.md. Original inputs preserved separately. */
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
  const CHROME = ['#27343c','#465862','#788e97','#a6bac0','#d5e1df','#f2f4e9'];
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
  function wearTex(w){return(u,v)=>{const low=v<.95, edge=((u%1.14)+1.14)%1.14<.06; return w>.03&&(low||edge)&&hash2(Math.floor(u*19),Math.floor(v*21))<w*.035?-.65:0;};}
  function seamWearTex(w){return(u,v)=>{const q=((u%1.14)+1.14)%1.14; if(q<.012)return -.42; return v<1.48&&hash2(Math.floor(u*21),Math.floor(v*27))<w*.045?-.6:0;};}
  function plankTex(w){return(u,v)=>{const p=.305,q=((u%p)+p)%p; if(q<.012)return -.65;const board=Math.floor(u/p);return(hash2(board,17)-.5)*.5+(w>.1&&hash2(Math.floor(u*28),Math.floor(v*11))<w*.012?-.5:0);};}
  function ribTex(){ const p=0.15; return (u,v)=>{ const f=((u%p)+p)%p; return f<0.055?-1:0; }; }
  function grilleTex(){ const p=0.082; return (u,v)=>{ const f=((v%p)+p)%p; return f<0.034?2:0; }; }
  function treadTex(phase){const c=2*Math.PI*G.wheelR/28;return(u,v)=>{const f=(((u+phase)%c)+c)%c;return f<c*.42?-1:0;};}
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
  // ---- the roof roll (see artFinish) ----
  // Wall tops tuck under the rolled roof edge so a flat panel cannot poke through it. The roll is a
  // shape of the FIXED BODY, so it is applied in the frame the geometry was BUILT in — never to a
  // posed part in world space. A part that carries a transform is rolled HERE, before its hinge, and
  // tagged so artFinish leaves it alone: the leaf that bakes closed is then the same rigid leaf that
  // bakes open, one rotation about its pin. See README "The roof roll and the barn leaves".
  function rollTop(p){
    const roof=G.boxRoofZ||(G.rf&&G.rf.roofZ), hw=G.hwBox||G.hw;
    if(!roof||!hw) return p;
    return [p[0],p[1],p[2]-.065*Math.pow(Math.min(1,Math.abs(p[0])/hw),10)*Math.max(0,Math.min(1,(p[2]-roof+.16)/.16))];
  }
  function part(out, fn, xf){
    const T=[]; fn(T);
    if(xf) for(const f of T){ f.posed=true; f.v=f.v.map(p=>xf(rollTop(p))); }
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
  function originalFlatbed(out,s){
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
  function originalReefer(out,s){
    const S=s.S, hl=S.L/2, R=G.rf, sw=seamWearTex(s.weather);
    for(const sx of [-1,1]){
      texWallX(out, sx*G.hw, -hl+0.06, hl, G.deckZ, R.roofZ, 'paint', sx>0?0.18:-0.42, sx, sw);
      wallX(out, sx*G.hw, -hl+0.06, hl, 1.06, G.deckZ, 'galv', sx>0?0.02:-0.5, sx);          // bottom rail
    }
    wallY(out, hl, -G.hw, G.hw, G.deckZ, R.roofZ, 'paint', 0.10, +1);                         // nose wall
    p2CargoRoof(out,G.hw,-hl+.04,hl,R.roofZ);
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
        for(const zz of [1.57,2.35,3.47]){
          boxAt(T,Math.min(sx*.87,sx*1.16),Math.max(sx*.87,sx*1.16),-hl-.035,-hl-.012,zz,zz+.055,'galv',.16);
          tube(T,[sx*1.15,-hl-.035,zz-.035],[sx*1.15,-hl-.035,zz+.10],.025,8,'chrome',.15);
        }
        for(const rx of [0.38,0.80]){ const px=sx*rx;
          bar(T,[px,-hl-0.025,R.doorZ[0]+0.10],[px,-hl-0.025,R.doorZ[1]-0.10],0.027,'chrome',0.25);
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
  function originalWheelAt(out, xc, yc, sxOut, roll){
    roll=((roll%1)+1)%1;
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

  function build(s){ return artFinish(buildRaw(s)); }
  function buildRaw(s){
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
  function p1Mats(s){
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
            if(f.mat==='paint'||f.mat==='trim')fi=Math.round(fi)+(fi-Math.round(fi))*ART.bodyDither;
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
        if((m==='paint'||m==='galv'||m==='iron'||m==='rubber'||m==='wood') && rnd()<0)
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
  function originalAnchors(dir, opts){ opts=opts||{}; const s=resolve(opts), e=opts.elev;
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


  // Art revision 2026-09-13: construction geometry is evaluated before rasterization.
  // Values are authored metres; export scale, articulation and coupling anchors are retained.
  const ART={glassSeal:.027, rimSegments:24, bodyDither:.24};
  const artLerp=(a,b,t)=>a.map((v,i)=>v+(b[i]-v)*t);
  function artPatch(f,u,v){return artLerp(artLerp(f.v[0],f.v[1],u),artLerp(f.v[3],f.v[2],u),v);}
  function p1Finish(faces){
    const out=[];
    for(const f of faces){
      if(f.mat==='glass'&&f.v.length===4){
        // A dark gasket, a separate pane and broad reflection bands, all riding the source face.
        const n=nrm(crs(sub(f.v[1],f.v[0]),sub(f.v[2],f.v[0])));
        const w=Math.hypot(...sub(f.v[1],f.v[0])),h=Math.hypot(...sub(f.v[3],f.v[0]));
        if(Math.min(w,h)<.08){out.push(f);continue;}
        const iu=Math.min(.15,ART.glassSeal/w),iv=Math.min(.15,ART.glassSeal/h);
        out.push({...f,mat:'rubber',b:-.3,tex:null,flat:true});
        const patch=(u0,u1,v0,v1,mat,b,depth)=>{
          const pts=[[u0,v0],[u1,v0],[u1,v1],[u0,v1]].map(([u,v])=>artPatch(f,u,v).map((c,i)=>c+n[i]*depth));
          out.push(F(pts,mat,b,.009,null,null,true));
        };
        patch(iu,1-iu,iv,1-iv,'glass',f.b-.32,.005);
        patch(iu,1-iu,.64,.77,'glass',f.b+.75,.007);
        patch(iu,1-iu,.78,.815,'glass',f.b+1.15,.008);
        if(w>.65&&h>.45&&Math.abs(n[1])>.5){
          const a=artPatch(f,.16,.08),b=artPatch(f,.43,.19);
          bar(out,a.map((c,i)=>c+n[i]*.014),b.map((c,i)=>c+n[i]*.014),.012,'rubber',-.1);
        }
      }else{
        out.push(f);
        if(f.mat==='grille'&&f.v.length===4){
          for(let k=0;k<4;k++)bar(out,f.v[k],f.v[(k+1)%4],.019,'chrome',.15);
        }
      }
    }
    return out;
  }
  function artSurface(out, rows, mat='paint', bias=.08){
    for(let j=0;j<rows.length-1;j++)for(let i=0;i<rows[j].length-1;i++)
      out.push(F([rows[j][i],rows[j][i+1],rows[j+1][i+1],rows[j+1][i]],mat,bias));
  }
  function artWarp(faces,fn){
    const out=[];
    for(const f of faces){
      if(f.v.length===4&&f.mat==='paint'){
        for(let v=0;v<4;v++)for(let u=0;u<4;u++){
          const q=[[u/4,v/4],[(u+1)/4,v/4],[(u+1)/4,(v+1)/4],[u/4,(v+1)/4]];
          out.push({...f,v:q.map(([a,b])=>fn(artPatch(f,a,b))),uv:null,tex:null});
        }
      }else out.push({...f,v:f.v.map(fn)});
    }
    return out;
  }
  function artRing(out,x,y,z,ro,ri,mat,b){
    for(let i=0;i<ART.rimSegments;i++){
      const a=i*2*Math.PI/ART.rimSegments,c=(i+1)*2*Math.PI/ART.rimSegments;
      out.push(F([[x,y+Math.cos(a)*ro,z+Math.sin(a)*ro],[x,y+Math.cos(c)*ro,z+Math.sin(c)*ro],
        [x,y+Math.cos(c)*ri,z+Math.sin(c)*ri],[x,y+Math.cos(a)*ri,z+Math.sin(a)*ri]],mat,b,.003));
    }
  }
  function artWheels(out,xc,yc,sx,roll,yaw){
    const T=[]; originalWheelAt(T,xc,yc,sx,roll,0);
    const r=G.wheelR,w=G.tireW,x=xc+sx*(w/2+.020);
    artRing(T,x,yc,r,r*.70,r*.58,'chrome',.45);
    artRing(T,x+sx*.003,yc,r,r*.58,r*.48,'iron',-.15);
    artRing(T,x+sx*.006,yc,r,r*.48,r*.40,'alloy',.32);
    for(let i=0;i<5;i++){
      const a=((roll%1)+i/5)*2*Math.PI,py=yc+Math.cos(a)*r*.46,pz=r+Math.sin(a)*r*.46;
      tube(T,[x,py,pz],[x+sx*.01,py,pz],r*.080,8,'rubber',-.2);
    }
    if(yaw){const a=yaw*DEG;for(const f of T)f.v=f.v.map(p=>hingeZ(p,xc,yc,Math.cos(a),Math.sin(a)));}
    out.push(...T);
  }
  function wheelAt(out,xc,yc,sx,roll,yaw){artWheels(out,xc,yc,sx,roll,yaw);}
  function anchors(dir,opts){
    const A=originalAnchors(dir,opts),s=resolve(opts||{});
    for(const key of Object.keys(A)){
      const a=A[key];if(!a||!a.m||/^wheel/i.test(key))continue;
      const p=a.m.slice();let dz;
      if(s.S){const kp=kpY(s.S);dz=-s.sus*TRAV*Math.max(0,(kp-p[1])/(kp-axC(s.S)));}
      else{const t=(p[1]-G.axR)/(G.axF-G.axR);dz=-s.susF*TF*t-s.susR*TR*(1-t);}
      p[2]+=dz;const q=s.S?project(dir,p,opts&&opts.elev,s.yaw,s.body):project(dir,p,opts&&opts.elev,s.yaw);
      A[key]={x:q.x,y:q.y,m:p};
    }
    return A;
  }

  function p1Flatbed(out,s){originalFlatbed(out,s);const hl=s.S.L/2;
    for(const sx of [-1,1]){
      // Outboard structural web and flange, visibly distinct beneath the deck edge.
      const cuts=[-hl+.15,...s.S.axles.flatMap(y=>[y-.58,y+.58]),hl-.15].sort((a,b)=>a-b);
      for(let i=0;i<cuts.length-1;i++)if(!s.S.axles.some(y=>Math.abs((cuts[i]+cuts[i+1])/2-y)<.58)){
        boxAt(out,Math.min(sx*.99,sx*1.07),Math.max(sx*.99,sx*1.07),cuts[i],cuts[i+1],.78,1.055,'iron',.15);
        boxAt(out,Math.min(sx*.94,sx*1.12),Math.max(sx*.94,sx*1.12),cuts[i],cuts[i+1],.78,.835,'galv',-.10);
      }
      for(let y=-hl+.85;y<hl-.2;y+=1.35){
        wallX(out,sx*1.237,y-.065,y+.065,1.015,1.09,'iron',-.25,sx);
        boxAt(out,Math.min(sx*1.18,sx*1.23),Math.max(sx*1.18,sx*1.23),y-.08,y+.08,1.095,1.13,'galv',.28);
      }
      for(let y=-hl+.5;y<hl;y+=1.3)boxAt(out,Math.min(sx*1.216,sx*1.224),Math.max(sx*1.216,sx*1.224),y,y+.25,1.12,1.165,'lensR',.10);
    }
    for(let x=-.915;x<1.1;x+=.305)bar(out,[x,-hl+.015,G.deckZ+.003],[x,hl-.015,G.deckZ+.003],.011,'wood',-.55);
    for(let y=-hl+.4;y<hl-.2;y+=1.1){
      boxAt(out,-1.10,1.10,y-.03,y+.03,.92,1.07,'galv',-.12);
      for(const x of [-.91,-.30,.30,.91])boxAt(out,x-.012,x+.012,y-.012,y+.012,G.deckZ+.001,G.deckZ+.004,'iron',-.15);
    }
    if(s.headboard)for(const x of [-.98,.98])boxAt(out,x-.025,x+.025,hl-.13,hl-.105,G.deckZ,G.fl.headZ,'galv',.1);
  }
  function p1Reefer(out,s){originalReefer(out,s);const hl=s.S.L/2,R=G.rf,U=R.unit;
    artCargoRails(out,G.hw,-hl,hl,G.deckZ,R.roofZ);
    // The unit keeps its existing swing envelope. Its grille is now broken into serviceable modules.
    boxAt(out,-U.hw+.04,U.hw-.04,hl+U.out-.005,hl+U.out+.003,2.02,2.09,'trim',.1);
    boxAt(out,-U.hw+.04,U.hw-.04,hl+U.out-.005,hl+U.out+.003,2.69,2.76,'trim',.1);
    tube(out,[0,hl+U.out+.004,2.40],[0,hl+U.out+.010,2.40],.24,20,'iron',-.1);
    tube(out,[0,hl+U.out+.011,2.40],[0,hl+U.out+.015,2.40],.075,12,'galv',.2);
    for(const x of [-.32,.32])bar(out,[x,hl+U.out+.006,2.14],[x,hl+U.out+.006,2.65],.015,'galv',.15);
    wallY(out,hl+U.out+.004,-.28,.28,1.73,1.93,'iron',-.2,+1);
    wallY(out,hl+U.out+.006,-.24,-.04,1.80,1.87,'glass',.4,+1);
  }

  function artCargoRails(out,hw,yr,yf,z0,z1){
    for(const sx of [-1,1]){
      const x=sx*(hw+.003);
      wallX(out,x,yr+.015,yf-.015,z1-.065,z1-.01,'galv',.05,sx);
      wallX(out,x,yr+.015,yf-.015,z0,z0+.065,'galv',.08,sx);
      for(let y=yr+1.14;y<yf-.15;y+=1.14)wallX(out,x,y-.011,y+.011,z0+.09,z1-.09,'paint',-.48,sx);
      for(const y of [yr+.035,yf-.035]){
        wallX(out,x,y-.035,y+.035,z0,z1-.025,'galv',.05,sx);
        for(let z=z0+.2;z<z1-.1;z+=.4)boxAt(out,Math.min(x,x+sx*.012),Math.max(x,x+sx*.012),y-.012,y+.012,z,z+.024,'chrome',.1);
      }
      for(let y=yr+.25;y<yf-.15;y+=1.25){
        wallX(out,x+sx*.002,y,y+.28,z0+.02,z0+.07,'lensR',.05,sx);
        wallX(out,x+sx*.002,y+.28,y+.52,z0+.02,z0+.07,'trim',.3,sx);
      }
    }
  }


  // Sculpted section, recessed fitting, and stamped-panel helpers. All measurements are metres.
  function p2Face(out,v,mat='paint',b=0,n){if(n){const c=crs(sub(v[1],v[0]),sub(v[2],v[0]));if(c[0]*n[0]+c[1]*n[1]+c[2]*n[2]<0)v=v.slice().reverse();}out.push(F(v,mat,b));}
  function p2Rect(x0,x1,z0,z1,r){r=Math.min(r,(x1-x0)/2,(z1-z0)/2);const pts=[];for(const [x,z,a]of [[x0+r,z0+r,Math.PI],[x1-r,z0+r,1.5*Math.PI],[x1-r,z1-r,0],[x0+r,z1-r,.5*Math.PI]])for(let i=0;i<=4;i++){const t=a+i*Math.PI/8;pts.push([x+r*Math.cos(t),z+r*Math.sin(t)]);}return pts;}
  function p2Y(out,y,pts,mat,b=0,sgn=1){p2Face(out,pts.map(p=>[p[0],y,p[1]]),mat,b,[0,sgn,0]);}
  function p2Ring(out,outer,inner,mat,b,n){for(let i=0;i<outer.length;i++){const j=(i+1)%outer.length;p2Face(out,[outer[i],outer[j],inner[j],inner[i]],mat,b,n);}}
  function p2Box(out,sx,x0,x1,y0,y1,z0,z1,mat,b=0){boxAt(out,Math.min(sx*x0,sx*x1),Math.max(sx*x0,sx*x1),y0,y1,z0,z1,mat,b);}
  function p2Intake(out,y,w,z0,z1,chrome=false,vertical=false){
    const o=p2Rect(-w,w,z0,z1,.09),i=p2Rect(-w+.05,w-.05,z0+.05,z1-.05,.06);
    p2Y(out,y-.025,o,'shade',-.35);
    p2Ring(out,o.map(p=>[p[0],y+.015,p[1]]),i.map(p=>[p[0],y-.010,p[1]]),chrome?'p2Bright':'p2Pressed',.05,[0,1,0]);
    const h=z1-z0;if(vertical){for(let x=-w+.085;x<w-.06;x+=.075)boxAt(out,x,x+.025,y-.012,y+.008,z0+.065,z1-.065,'p2Bright',.08);}
    else for(let z=z0+.085;z<z1-.06;z+=.09)boxAt(out,-w+.075,w-.075,y-.018,y-.009,z,z+.019,'galv',-.24);
    if(chrome&&!vertical){for(const z of [z0+h*.28,z0+h*.72])boxAt(out,-w+.065,w-.065,y+.017,y+.030,z,z+.025,'p2Bright',.2);}
  }
  function p2Lamp(out,sx,y,x0,x1,z0,z1,classic=false,night=false){
    const o=p2Rect(x0,x1,z0,z1,.05),i=p2Rect(x0+.026,x1-.026,z0+.025,z1-.025,.028);
    const M=(p,dy)=>[sx*p[0],y+dy,p[1]];
    p2Face(out,o.map(p=>M(p,0)),'rubber',-.2,[0,1,0]);
    p2Ring(out,o.map(p=>M(p,.010)),i.map(p=>M(p,.003)),classic?'p2Bright':'p2Pressed',.15,[0,1,0]);
    p2Face(out,i.map(p=>M(p,.004)),'shade',-.3,[0,1,0]);
    const z=(z0+z1)/2,r=Math.min((x1-x0)/5,(z1-z0)*.29);
    for(const x of [x0+(x1-x0)*.30,x0+(x1-x0)*.72]){
      tube(out,[sx*x,y+.012,z],[sx*x,y+.022,z],r,12,'p2Bright',.1);
      tube(out,[sx*x,y+.023,z],[sx*x,y+.026,z],r*.69,12,night?'glow':'p2Lens',.2);
    }
    if(!classic){p2Box(out,sx,x0+.04,x1-.04,y+.024,y+.030,z1-.055,z1-.025,night?'glow':'p2Lens',.2);}
    p2Box(out,sx,x1-.048,x1-.022,y+.024,y+.030,z0+.04,z0+.09,'lensA',.15);
  }
  function p2Glaze(out,f){
    if(f.v.length!==4){out.push(f);return;}
    const n=nrm(crs(sub(f.v[1],f.v[0]),sub(f.v[2],f.v[0]))),P=(uv,off)=>artPatch(f,uv[0],uv[1]).map((v,i)=>v+n[i]*off);
    const O=[[.025,0],[.975,0],[1,.04],[1,.96],[.975,1],[.025,1],[0,.96],[0,.04]];
    const I=O.map(([u,v])=>[.035+u*.93,.035+v*.93]);
    p2Face(out,O.map(uv=>P(uv,0)),'rubber',-.25,n);
    p2Ring(out,O.map(uv=>P(uv,.005)),I.map(uv=>P(uv,.007)),'rubber',-.1,n);
    p2Face(out,I.map(uv=>P(uv,.008)),'p2Glass',-.1,n);
    p2Face(out,[[.05,.83],[.81,.83],[.96,.66],[.20,.66]].map(uv=>P(uv,.011)),'p2Reflection',-.10,n);
    p2Face(out,[[.05,.86],[.76,.86],[.81,.83],[.05,.83]].map(uv=>P(uv,.012)),'p2Reflection',.28,n);
    if(Math.hypot(...sub(f.v[1],f.v[0]))>.6&&Math.abs(n[1])>.6){
      bar(out,P([.14,.10],.016),P([.45,.21],.016),.011,'rubber',-.1);
      bar(out,P([.55,.10],.016),P([.86,.21],.016),.011,'rubber',-.1);
    }
  }
  function artFinish(faces){
    // The wall tops follow the rolled roof edge, so flat side panels cannot poke through it.
    // POSED parts (the barn leaves) are already rolled in their own build frame by part()/rollTop —
    // re-rolling them here, in world space AFTER the hinge, is what bent them out of shape.
    for(const f of faces) if(!f.posed) f.v=f.v.map(rollTop);
    const out=[],rest=[];for(const f of faces)if(f.mat==='glass')p2Glaze(out,f);else rest.push(f);return p1Finish(rest).concat(out);
  }
  function makeMats(s){const m=p1Mats(s),base=m.paint.ramp,cool=r=>s.night?r.map(c=>mix(c,'#1b2740',.4)):r;
    return {...m,p2Pressed:{ramp:base.map(c=>mix(c,'#30424a',.075))},p2Hood:{ramp:base.map(c=>mix(c,'#263e49',.12))},
      p2Bright:{ramp:cool(['#263640','#506774','#8ca3ab','#bfd0d2','#e3e9e1','#fffbed'])},
      p2Glass:{ramp:cool(['#142029','#23343e','#354b55','#4d6972','#718c94','#a2b8bb'])},
      p2Reflection:{ramp:cool(['#354e5d','#4e6c7b','#71909a','#8eaab1','#b3c8cb','#d2dddb'])},
      p2Lens:{ramp:cool(['#425960','#748d94','#a1b9bf','#c4d5d6','#e0e8e3','#fffbee'])},
      p2Plate:{ramp:cool(['#24343b','#43545a','#798b8e','#a9b7b4','#d0d8ce','#e9edde'])},
      p2Liner:{ramp:cool(['#26313a','#38444c','#515e63','#6a7577','#83908e','#a1aaa2'])}};
  }
  function p2Roof(out,hw,yr,yf,z){
    const rows=[];for(let j=0;j<=8;j++){const t=j/8,y=yr+(yf-yr)*t,end=Math.pow(Math.abs(2*t-1),8);
      rows.push([-1,-.96,-.85,-.6,0,.6,.85,.96,1].map(u=>[u*(hw-.025*end),y,z-.065*Math.pow(Math.abs(u),4)-.025*end]));}
    artSurface(out,rows,'paint',.06);
    for(const sx of [-1,1])bar(out,[sx*(hw-.01),yr+.05,z-.08],[sx*(hw-.01),yf-.05,z-.08],.012,'p2Bright',-.12);
  }
  function p2DoorLeaf(out,sx,ys,x,z0,belt,head,s,van=false){
    const [ya,yb]=ys,Y0=ya+.018,Y1=yb-.018;
    const outer=p2Rect(Y0,Y1,z0+.014,belt,.035),inner=p2Rect(Y0+.10,Y1-.10,z0+.16,belt-.13,.075);
    const P=(p,dx)=>[sx*(x+dx),p[0],p[1]];
    p2Face(out,outer.map(p=>P(p,-.078)),'p2Liner',-.15,[sx,0,0]);
    p2Ring(out,outer.map(p=>P(p,0)),inner.map(p=>P(p,-.022)),'paint',.05,[sx,0,0]);
    p2Face(out,inner.map(p=>P(p,-.022)),'p2Pressed',-.03,[sx,0,0]);
    for(const y of [Y0,Y1])bar(out,[sx*(x+.003),y,z0+.03],[sx*(x+.003),y,belt],.008,'rubber',-.35);
    const shell=[[ya,belt],[yb,belt],[yb-.08,head],[ya+.012,head]],glass=[[ya+.095,belt+.075],[yb-.075,belt+.075],[yb-.16,head-.075],[ya+.10,head-.075]];
    const Q=(p,dx)=>[sx*(x-.038*(p[1]-belt)/(head-belt)+dx),p[0],p[1]];
    p2Ring(out,shell.map(p=>Q(p,0)),glass.map(p=>Q(p,0)),'paint',.03,[sx,0,0]);
    const gf=F(glass.map(p=>Q(p,.003)),'glass',0);if(sx<0)gf.v.reverse();p2Glaze(out,gf);
    bar(out,[sx*(x+.004),ya+.025,belt+.025],[sx*(x+.004),yb-.025,belt+.025],.013,'p2Bright',.02);
    const hy=ya+.23,hz=belt-.09;
    p2Face(out,p2Rect(hy-.14,hy+.14,hz-.05,hz+.05,.035).map(p=>[sx*(x+.005),p[0],p[1]]),'rubber',-.15,[sx,0,0]);
    bar(out,[sx*(x+.038),hy-.105,hz+.006],[sx*(x+.038),hy+.09,hz+.006],.021,'p2Bright',.12);
    p2Box(out,sx,x-.078,x-.064,ya+.12,yb-.12,z0+.25,belt-.08,'p2Liner',-.2);
    bar(out,[sx*(x-.10),hy-.08,hz-.08],[sx*(x-.10),hy+.10,hz-.08],.022,'rubber',-.1);
    if(van&&s.mirrors){
      bar(out,[sx*x,yb-.12,belt+.14],[sx*1.20,yb-.04,belt+.22],.025,'rubber',-.05);
      p2Box(out,sx,1.15,1.26,yb-.13,yb+.04,1.47,1.75,'rubber',-.1);
      wallY(out,yb-.135,Math.min(sx*1.175,sx*1.24),Math.max(sx*1.175,sx*1.24),1.51,1.70,'p2Glass',.2,-1);
      wallY(out,yb-.14,Math.min(sx*1.17,sx*1.245),Math.max(sx*1.17,sx*1.245),1.505,1.53,'p2Reflection',.1,-1);
    }
  }
  function p2Front(out,s,kind,bonnet=true){
    const van=kind==='van',classic=kind==='classic',aero=kind==='aero';
    const y0=van?1.74:G.hoodY0,y1=van?G.hoodY1:G.hoodY1,L=y1-y0,hw=van?G.hwArch:G.hwFender,ih=G.hwHood;
    const zc=G.hoodZc,zn=G.hoodZn,ar=G.archF.r,az=G.archF.zc,ay=G.axF;
    const ease=t=>t*t*(3-2*t),tAt=y=>Math.max(0,Math.min(1,(y-y0)/L));
    const edge=y=>hw-(van?.08:.13)*Math.pow(Math.max(0,(tAt(y)-.64)/.36),2)-.045*Math.pow(Math.max(0,(.18-tAt(y))/.18),2);
    const sweep=(x,y)=>y-(aero?.17:van?.105:.11)*Math.pow(Math.abs(x)/hw,4)*Math.pow(tAt(y),5);
    const hz=y=>zc+(zn-zc)*ease(tAt(y));
    const shoulder=y=>Math.max(az+ar+.08,(van?1.055:G.fenderTopZ)+.025*Math.sin(tAt(y)*Math.PI)-.05*Math.pow(tAt(y),4));
    // Front wheel skin and rolled lip: cut out the opening rather than putting a tire behind a box.
    for(const sx of [-1,1]){
      const points=[];for(let j=0;j<=28;j++)points.push(y0+L*j/28);points.push(Math.max(y0,ay-ar),Math.min(y1,ay+ar));points.sort((a,b)=>a-b);
      const low=y=>Math.max(.48,Math.abs(y-ay)<ar?az+Math.sqrt(Math.max(0,ar*ar-(y-ay)**2)):.48);
      for(let j=0;j<points.length-1;j++){const ya=points[j],yb=points[j+1];if(yb-ya<1e-6)continue;
        const P=(y,z)=>[sx*edge(y),sweep(edge(y),y),z];
        p2Face(out,[P(ya,low(ya)),P(yb,low(yb)),P(yb,shoulder(yb)),P(ya,shoulder(ya))],'paint',.02,[sx,0,0]);
        if(classic){
          p2Face(out,[[sx*ih,ya,shoulder(ya)+.015],[sx*edge(ya),sweep(edge(ya),ya),shoulder(ya)],[sx*edge(yb),sweep(edge(yb),yb),shoulder(yb)],[sx*ih,yb,shoulder(yb)+.015]],'paint',.10,[0,0,1]);
          p2Face(out,[[sx*ih,ya,shoulder(ya)+.015],[sx*ih,yb,shoulder(yb)+.015],[sx*ih,yb,hz(yb)],[sx*ih,ya,hz(ya)]],'paint',-.08,[sx,0,0]);
        }else for(let k=0;k<6;k++){
          const P=(y,t)=>{const x=ih+(edge(y)-ih)*t,z=hz(y)+(shoulder(y)-hz(y))*ease(t);return[sx*x,sweep(x,y),z];};
          p2Face(out,[P(ya,k/6),P(yb,k/6),P(yb,(k+1)/6),P(ya,(k+1)/6)],'paint',.04,[0,0,1]);
        }
      }
      for(let j=0;j<24;j++){const a=j*Math.PI/24,b=(j+1)*Math.PI/24;
        const P=(t,r,dx)=>{const y=ay+Math.cos(t)*r;return[sx*(edge(y)+dx),sweep(edge(y),y),az+Math.sin(t)*r];};
        p2Face(out,[P(a,ar,.003),P(b,ar,.003),P(b,ar+.032,.002),P(a,ar+.032,.002)],classic?'p2Bright':'p2Pressed',.14,[sx,0,0]);
        p2Face(out,[P(a,ar,-.025),P(b,ar,-.025),P(b,ar,.002),P(a,ar,.002)],'rubber',-.15,[sx,0,0]);
      }
      p2Box(out,sx,edge(ay-.3)+.004,edge(ay-.3)+.012,ay-.38,ay-.20,shoulder(ay-.3)-.07,shoulder(ay-.3)-.04,'p2Bright',.1);
    }
    if(bonnet)p2Bonnet(out,s,kind);
    // A curved nose panel behind sockets and grille. Its outer corners sweep into the fenders.
    const nosestart=out.length,faceTop=classic?zn:Math.min(zn,shoulder(y1)+.04),faceWidth=edge(y1);
    p2Y(out,y1,p2Rect(-faceWidth,faceWidth,.50,faceTop,.12),'paint',.02);
    const gw=classic?.61:van?.54:.60,g0=van?.75:.69,g1=classic?zn-.06:van?1.075:Math.min(1.28,zn-.045);
    p2Intake(out,y1+.035,gw,g0,g1,classic||kind==='conventional',classic);
    const lz=classic?1.29:van?.87:.97,lt=classic?1.56:van?1.105:1.235;
    for(const sx of [-1,1])p2Lamp(out,sx,y1+.04,gw+.025,Math.min(faceWidth-.025,gw+.44),lz,lt,classic,s.night);
    p2Y(out,y1+.072,p2Rect(-.075,.075,g1-.13,g1-.075,.018),'p2Bright',.2);
    for(let i=nosestart;i<out.length;i++)out[i].v=out[i].v.map(([x,y,z])=>[x,sweep(x,y),z]);
    if(classic)for(const sx of [-1,1])for(let y=y0+.18;y<y0+.86;y+=.085)wallX(out,sx*(ih+.006),y,y+.024,1.40,1.62,'iron',-.15,sx);
  }
  function p2Bonnet(out,s,kind){
    const van=kind==='van',y0=van?1.74:G.hoodY0,y1=G.hoodY1,L=y1-y0,hw=G.hwHood;
    const rows=[];const xs=[-1,-.92,-.72,-.46,0,.46,.72,.92,1];
    for(let j=0;j<=12;j++){const t=j/12,e=t*t*(3-2*t),y=y0+L*t;rows.push(xs.map(u=>{
      const x=u*hw,z=G.hoodZc+(G.hoodZn-G.hoodZc)*e+.052*(1-u*u)*Math.sin(Math.PI*(.18+.64*t));
      return[x,y-(kind==='aero'?.17:van?.105:.11)*Math.pow(Math.abs(x)/(van?G.hwArch:G.hwFender),4)*Math.pow(t,5),z];}));}
    for(let j=0;j<rows.length-1;j++)for(let i=0;i<xs.length-1;i++){
      const v=[rows[j][i],rows[j][i+1],rows[j+1][i+1],rows[j+1][i]];
      p2Face(out,v,i===2||i===5?'p2Hood':'paint',.06,[0,0,1]);
      p2Face(out,v.map(p=>[p[0],p[1],p[2]-.025]),'p2Liner',-.4,[0,0,-1]);
    }
    for(const sx of [-1,1]){const i=sx<0?0:xs.length-1;for(let j=0;j<rows.length-1;j++)bar(out,rows[j][i],rows[j+1][i],.009,'p2Pressed',-.14);}
  }
  function p2Bumper(out,s,kind){
    const van=kind==='van',classic=kind==='classic',hw=van?1.015:classic?1.24:kind==='aero'?1.24:1.06,y=G.bumpF[1],z0=van?.31:.30,z1=van?.55:kind==='aero'?.68:.62;
    const start=out.length,mat=classic||kind==='conventional'?'p2Bright':'p2Pressed';
    const O=p2Rect(-hw,hw,z0,z1,.075);p2Y(out,y,O,mat,.12);
    boxAt(out,-hw+.08,hw-.08,G.bumpF[0],y-.01,z1-.045,z1,'p2Bright',.1);
    p2Y(out,y+.004,p2Rect(-.44,.44,z0+.06,z1-.045,.025),'shade',-.3);
    for(const sx of [-1,1]){
      p2Y(out,y+.009,p2Rect(Math.min(sx*.66,sx*.90),Math.max(sx*.66,sx*.90),z0+.07,z1-.06,.03),'rubber',-.12);
      p2Y(out,y+.014,p2Rect(Math.min(sx*.70,sx*.86),Math.max(sx*.70,sx*.86),z0+.10,z1-.095,.017),s.night?'glow':'p2Lens',.12);
    }
    p2Y(out,y+.016,p2Rect(-.16,.16,z0+.075,z0+.21,.01),'p2Plate',.2);
    for(const x of [-.11,-.045,.035,.10])boxAt(out,x-.008,x+.008,y+.018,y+.021,z0+.11,z0+.16,'iron',-.15);
    for(let i=start;i<out.length;i++)out[i].v=out[i].v.map(([x,yy,z])=>[x,yy-.09*Math.pow(Math.abs(x)/hw,4),z]);
  }
  function p2ServicePanel(out,sx,x,y0,y1,z0,z1){
    const o=p2Rect(y0,y1,z0,z1,.055),i=p2Rect(y0+.025,y1-.025,z0+.025,z1-.025,.035),P=(p,dx)=>[sx*(x+dx),p[0],p[1]];
    p2Face(out,o.map(p=>P(p,.004)),'p2Pressed',-.12,[sx,0,0]);
    p2Ring(out,o.map(p=>P(p,.006)),i.map(p=>P(p,.009)),'paint',.12,[sx,0,0]);
    p2Face(out,i.map(p=>P(p,.010)),'paint',-.07,[sx,0,0]);
    p2Box(out,sx,x+.012,x+.025,y0+.085,y0+.20,z1-.12,z1-.08,'rubber',-.05);
    for(const y of [y0+.055,y1-.055])for(const z of [z0+.055,z1-.055])tube(out,[sx*(x+.011),y,z],[sx*(x+.022),y,z],.012,6,'p2Bright',.1);
  }
  function p2RunningGear(out,s,classic=false){
    const fw=G.fw;if(fw){
      for(let j=0;j<30;j++){const a=(-75+j*11)*DEG,b=(-75+(j+1)*11)*DEG,P=(t,inner,z)=>[(inner?.085:.44)*Math.cos(t),fw.y+(inner?.085:.40)*Math.sin(t),z];
        p2Face(out,[P(a,false,fw.topZ),P(b,false,fw.topZ),P(b,true,fw.topZ),P(a,true,fw.topZ)],'iron',.14,[0,0,1]);
        p2Face(out,[P(a,false,fw.topZ-.025),P(b,false,fw.topZ-.025),P(b,false,fw.topZ),P(a,false,fw.topZ)],'galv',-.08);
        p2Face(out,[P(a,true,fw.topZ),P(b,true,fw.topZ),P(b,true,fw.topZ-.025),P(a,true,fw.topZ-.025)],'shade',-.2);
      }
      // Flexible air-line coils and their distinct fittings on the existing rear-of-cab route.
      for(const sx of [-1,1]){let prev=null;for(let j=0;j<=36;j++){const t=j/36,p=[sx*.20+.045*Math.sin(t*12*Math.PI),G.cabBackY-.08-.55*t,1.92-.64*t+.045*Math.cos(t*12*Math.PI)];if(prev)bar(out,prev,p,.014,sx<0?'lensR':'p2Reflection',-.05);prev=p;}}
    }
    for(const sx of [-1,1]){
      const y0=G.doorY[0]-.08,y1=G.doorY[1]-.10,x=G.hwCab;
      for(const z of [.49,.73]){
        p2Box(out,sx,x-.07,x+.045,y0,y1,z,z+.045,'p2Bright',.04);
        for(let y=y0+.04;y<y1-.03;y+=.09)p2Box(out,sx,x-.045,x+.025,y,y+.035,z+.046,z+.052,'rubber',-.1);
      }
      if(classic&&G.tank)for(const yy of [G.tank.y[0]+.14,G.tank.y[1]-.14])tube(out,[sx*G.tank.x,yy-.022,G.tank.z],[sx*G.tank.x,yy+.022,G.tank.z],G.tank.r+.014,20,'p2Bright',.25,false);
    }
    if(classic){
      for(const sx of [-1,1]){
        tube(out,[sx*G.stacks.x,G.stacks.y,G.stacks.z1-.07],[sx*G.stacks.x,G.stacks.y,G.stacks.z1+.001],G.stacks.r*.73,12,'shade',-.3);
        for(let z=1.22;z<2.17;z+=.10)for(const yy of [-.045,.045])tube(out,[sx*(G.stacks.x+.082),G.stacks.y+yy,z],[sx*(G.stacks.x+.097),G.stacks.y+yy,z],.014,6,'iron',-.1);
      }
      for(const x of [-.34,.34]){
        tube(out,[x,.48,G.cabRoofZ+.055],[x,1.28,G.cabRoofZ+.055],.035,12,'p2Bright',.16);
        tube(out,[x,1.18,G.cabRoofZ+.055],[x,1.36,G.cabRoofZ+.055],.068,16,'p2Bright',.20);
        tube(out,[x,1.362,G.cabRoofZ+.055],[x,1.365,G.cabRoofZ+.055],.05,12,'shade',-.2);
      }
    }
  }
  function p2CargoRoof(out,hw,yr,yf,z){
    const xs=[-1,-.995,-.98,-.95,-.90,0,.90,.95,.98,.995,1],rows=[];
    for(const y of [yr,yr+.08,yf-.08,yf])rows.push(xs.map(u=>[hw*u,y,z]));
    artSurface(out,rows,'trim',.02);
    for(const sx of [-1,1])bar(out,[sx*(hw-.01),yr+.04,z-.07],[sx*(hw-.01),yf-.04,z-.07],.012,'p2Bright',.05);
  }

  function buildFlatbed(out,s){p1Flatbed(out,s);const hl=s.S.L/2;
    // Chamfered end caps, recessed winch drums and visible tie-down fittings.
    for(const sx of [-1,1]){
      for(let y=-hl+1.1;y<hl-.4;y+=1.6){
        const x=sx*1.22;bar(out,[x,y-.075,1.12],[x,y+.075,1.12],.018,'p2Bright',.1);
        bar(out,[x,y-.075,1.12],[sx*1.205,y-.075,1.17],.018,'p2Bright',.1);
        if(sx<0){tube(out,[-1.22,y,.90],[-1.15,y,.90],.075,12,'iron',.05);tube(out,[-1.225,y,.90],[-1.215,y,.90],.035,10,'p2Bright',.1);}
      }
      for(const ay of s.S.axles)for(const dy of [-.3,.3])p2Box(out,sx,.49,.61,ay+dy-.07,ay+dy+.07,.53,.78,'iron',-.05);
    }
    for(let x=-.91;x<1.1;x+=.305){const y=-hl+1.1+((Math.round((x+.91)/.305)%3)*1.5);bar(out,[x-.14,y,G.deckZ+.006],[x+.14,y,G.deckZ+.006],.009,'wood',-.55);}
  }
  function buildReefer(out,s){p1Reefer(out,s);const hl=s.S.L/2,R=G.rf,U=R.unit,y=hl+U.out;
    // Fan guard, removable service cover, control inset, hinge and latch detail.
    for(let i=0;i<16;i++){const a=i*2*Math.PI/16;bar(out,[0,y+.017,2.40],[.218*Math.cos(a),y+.017,2.40+.218*Math.sin(a)],.007,'galv',.02);}
    for(const x of [-.42,.42])for(const z of [1.72,2.82])tube(out,[x,y+.007,z],[x,y+.019,z],.016,8,'p2Bright',.1);
    boxAt(out,-.49,-.43,y+.005,y+.020,2.09,2.70,'p2Bright',-.1);
    p2Y(out,y+.009,p2Rect(.09,.35,1.77,1.94,.025),'p2Plate',.02);
    for(let z=1.8;z<1.93;z+=.035)wallY(out,y+.012,.12,.30,z,z+.013,'iron',-.1,1);
    for(const sx of [-1,1]){
      wallX(out,sx*1.224,-hl+.13,hl-.1,1.23,1.36,'p2Bright',-.05,sx);
      for(let yy=-hl+.3;yy<hl-.2;yy+=.57){p2Box(out,sx,1.224,1.231,yy-.012,yy+.012,R.roofZ-.14,R.roofZ-.115,'p2Bright',.04);}
    }
  }

  root.TrailerIso = { W:CELLS.long.W, H:CELLS.long.H, PX, DIRS:8,
    pivot:{x:CELLS.long.cx,y:CELLS.long.gy}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    CELLS, cellFor, pivotFor,
    BODY, TRIM, IRON, GALV, RUBBER, CHROME, WOOD, GLASSD, GLASSN, KEY,
    BODIES, PRESETS, CUES, G, travel:{group:TRAV},
    mesh:(opts)=>build(resolve(opts||{})), list, dims, resolve, render, frames, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
