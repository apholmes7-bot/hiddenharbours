/* Hidden Harbours — parametric ISO ROAD-VEHICLE rig, CONVENTIONAL BOX TRUCK (same turntable +
   camera + shading as vehicleIsoRig.js / boxIsoRig.js / the fleet). Body: CONVENTIONAL BOX — the
   cabover's big sibling (F-650/MV class): hooded day cab, 6.16 m dry box, dual rear wheels, cab
   roof fairing, roll-up rear door, tuck-under liftgate. 45deg steps, elev 40deg, flat-facet
   shading from the fixed upper-LEFT key, z-buffered, ordered dither, NO AA, 32 px = 1 m, ringless
   from birth (ADR 0031).

   CELL: 448 x 352 @ 224,214 — NOT the 384 road cell. The 9.60 m LOA plus a liftgate that reaches
   6.52 m aft when grounded cannot fit 384 px at fleet scale; the ground row (214) is kept so this
   body still parks on the same road plane as the rest of the fleet.

   ARTICULATION — pose params on render(dir,opts), 0..1 unless noted:
     dL dR             cab doors, hinged on their FORWARD edge, 0 -> 65deg
     rollup            rear roll-up door: 15 slats ride a track 2.30 m up the opening, then bend
                       forward and stack flat under the roof. The door NEVER leaves the body.
     gate              tuck-under liftgate, one param, three phases:
                       0..0.45 swing out from under the tail to dock height (folded),
                       0.45..0.70 the flip half unfolds aft to the full 1.26 m platform,
                       0.70..1 the arms lower the platform to the ground.
     hood              the whole FRONT CLIP — hood, cheeks, fenders, arches, grille, headlamps —
                       tilts forward 0 -> 70deg about a hinge at the bumper line and bares the
                       engine. The cab does not move: this class has a hood, not a tilt cab.
     roll              master wheel roll, REVOLUTIONS (cyclic); wFL wFR wRL wRR per-wheel offsets
     susF susR         suspension travel per axle, -1..1 (the BODY moves; wheels stay down)
     steer             front pair yaw, Ackermann-split, -1..1; +1 is full LEFT lock. Inner 35deg —
                       the biggest lock in the pack, but the 6.10 m wheelbase still turns wider
                       than the cabover. The painted fenders (half-width 1.25 m) are the envelope.
     yaw               heading off the 45deg grid, DEGREES (-45..45), rebaked under the fixed key.
   Parts (not poses): mirrors, mudflaps, liftgate (true|false — plain tail), fairing (true|false —
   the cab roof fairing; false moves the five ID lamps down onto the bare cab roof).

   ORIGIN / PIVOT: ground-centre of the body footprint. +x curb side, +y nose, +z up.

   Exposes globalThis.ConvBoxIso = { W,H,PX,DIRS,pivot,order,defaultElev, BODY,TRIM,IRON,GALV,
     RUBBER,CHROME,CLOTH,GLASSD,GLASSN,KEY, BODIES,PRESETS,CUES,G,travel,steer, list(), dims(opts),
     resolve(opts), render(dir,opts), frames(dir,n,opts,cue), anchors(dir,opts), project(dir,p,elev),
     gatePose, rollPath }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 448, H = 352, cx = 224, groundY = 214;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;                    // ADR 0031

  // ---- ramps (harbour master ramps, shared with the fleet — nothing invented) ----
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

  // ---- shading (identical recipe to vehicleIsoRig / the fleet) ----
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
    return { th, ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) }; }
  function projVert(x,y,z,B){ const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:groundY-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) }; }
  function normal(a,b,c){ const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx; const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m]; }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }

  // ---- face builders (outward-normal winding, fleet conventions) ----
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
  function seamWearTex(w){ const p=0.57; return (u,v)=>{ const f=((u%p)+p)%p; if(f<0.035) return -1;
    if(w>0.03 && hash2(Math.floor(u*6.5)|0, Math.floor(v*6.5)|0) < w*0.10) return -1; return 0; }; }
  function grilleTex(){ const p=0.082; return (u,v)=>{ const f=((v%p)+p)%p; return f<0.034?2:0; }; }
  function ribTex(){ const p=0.15; return (u,v)=>{ const f=((u%p)+p)%p; return f<0.055?-1:0; }; }
  function treadTex(phase){ const c=0.1047; return (u,v)=>{ const f=(((u+phase)%c)+c)%c; return f<c*0.42?-1:0; }; }
  // c = 0.1047 m puts 27.001 stripe periods on the 2.827 m circumference: one revolution closes to a
  // sub-pixel phase, while a HALF revolution lands mid-stripe (13.5) — the loop seam is invisible but
  // the roll cue still visibly moves.

  // ================= GEOMETRY — CONVENTIONAL BOX =================
  const TF=0.10, TR=0.14;                          // suspension travel per axle, metres
  const G = {
    noseY:4.16, bumpF:[4.18,4.30], bumpR:[-5.30,-5.12],
    cowlY:2.42, cabBackY:1.30, axF:3.20, axR:-2.90, wheelR:0.45, tireW:0.28,
    frontWX:0.86, dualXi:0.62, dualXo:0.92,
    hwCab:1.02, hwCabRoof:0.96, cabRoofZ:2.86, cabFloorZ:0.98,
    hwHood:0.82, hoodZc:1.60, hoodZn:1.44, hoodY0:2.50, hoodY1:4.12,
    hwFender:1.25, fenderTopZ:1.30, hoodHinge:{y:4.08,z:0.60}, hoodDeg:70,
    wsB:{y:2.40,z:1.66}, wsT:{y:2.16,z:2.58},
    doorY:[1.42,2.38], doorZ0:0.80, doorHead:2.34,
    hwBox:1.22, boxF:1.10, boxR:-5.06, boxFloorZ:1.10, boxRoofZ:3.68, ceilZ:3.60,
    openHW:1.10, sillZ:1.10, headZ:3.40, slatH:0.16, slats:15, vTrack:2.30, stackZ:3.52,
    archF:{r:0.62, zc:0.40}, archFy:[2.46,4.10],
    fairing:{y0:1.32, y1:2.02, hw:0.92, zTop:3.38},
    gate:{ pivotY:-4.60, pivotZ:0.64, mainD:0.66, flipD:0.60, hwPlat:1.10, th:0.055,
           stowY:-4.54, stowZ:0.76, stowDeg:24, deployY:-5.26, dockZ:1.10, groundZ:0.03 },
  };

  const BODIES = {
    convBox: { key:'convBox', label:'Conventional Box Truck', kind:'box_truck',
      loa:+(G.bumpF[1]-G.bumpR[0]).toFixed(2), bodyL:+(G.noseY-G.boxR).toFixed(2),
      width:+(G.hwFender*2).toFixed(2), bodyW:+(G.hwBox*2).toFixed(2),
      height:+(G.boxRoofZ+0.04).toFixed(2), wheelbase:+(G.axF-G.axR).toFixed(2),
      boxLen:+(G.boxF-G.boxR).toFixed(2), boxFloor:G.boxFloorZ, wheels:6 },
  };

  // ---- steering: front pair yaw about their own vertical axes, Ackermann-split ----
  const STEER_MAX = 35;                                   // inner wheel, degrees, at full lock
  function steerAngles(v){
    if(!v) return { L:0, R:0 };
    const inner = Math.abs(v)*STEER_MAX*DEG;
    const outer = Math.atan(1/(1/Math.tan(inner) + (G.frontWX*2)/(G.axF-G.axR)));
    const i=inner/DEG, o=outer/DEG;
    return v>0 ? { L:+i, R:+o } : { L:-o, R:-i };
  }

  const PRESETS = {
    showroom:     { paint:'white', weather:0.05 },
    workhorse:    { paint:'white', weather:0.50 },
    mover:        { paint:'gold',  weather:0.32 },
    coldChain:    { paint:'teal',  weather:0.30 },
    nightFreight: { paint:'blue',  weather:0.28, night:true },
  };
  const CUES = {
    doors: (t)=>({ dL:t, dR:t }),
    rollup:(t)=>({ rollup:t }),
    gate:  (t)=>({ gate:t }),
    hood:  (t)=>({ hood:t }),
    roll:  (t)=>({ roll:t }),                             // one revolution, cyclic
    steer: (t)=>({ steer:t*2-1 }),                        // full right lock -> full left lock
    turn:  (t)=>({ steer:Math.sin(t*Math.PI*2), yaw:Math.sin(t*Math.PI*2)*8, roll:t }),  // cyclic
    bounce:(t)=>({ susF:Math.sin(t*Math.PI*2)*0.7, susR:Math.sin(t*Math.PI*2+1.3)*0.7, roll:t }),
  };

  function resolve(opts){
    opts=opts||{};
    const g=(k,d)=> opts[k]!=null?opts[k]:d;
    const c01=(v)=>Math.max(0,Math.min(1,v)), c11=(v)=>Math.max(-1,Math.min(1,v));
    return {
      body:'convBox', B:BODIES.convBox,
      paint: opts.paint||'white', weather:g('weather',0.32),
      dL:c01(g('dL',0)), dR:c01(g('dR',0)), rollup:c01(g('rollup',0)),
      gate:c01(g('gate',0)), hood:c01(g('hood',0)),
      roll:g('roll',0), wFL:g('wFL',0), wFR:g('wFR',0), wRL:g('wRL',0), wRR:g('wRR',0),
      susF:c11(g('susF',0)), susR:c11(g('susR',0)),
      steer:c11(g('steer',0)), yaw:Math.max(-45,Math.min(45,g('yaw',0))),
      mirrors:g('mirrors',true), mudflaps:g('mudflaps',true), liftgate:g('liftgate',true), fairing:g('fairing',true),
      night:!!opts.night, outline: opts.outline!=null?!!opts.outline:KEYLINE_DEFAULT };
  }
  function dims(opts){ const s=resolve(opts);
    return Object.assign({ travelF:TF, travelR:TR }, s.B, { liftgate:s.liftgate, fairing:s.fairing }); }

  const hingeZ=(p,hx,hy,ca,sa)=>{ const dx=p[0]-hx, dy=p[1]-hy; return [hx+dx*ca-dy*sa, hy+dx*sa+dy*ca, p[2]]; };
  const rotX=(p,hy,hz,ca,sa)=>{ const dy=p[1]-hy, dz=p[2]-hz; return [p[0], hy+dy*ca-dz*sa, hz+dy*sa+dz*ca]; };
  function part(out, fn, xf){
    const T=[]; fn(T);
    if(xf) for(const f of T) f.v=f.v.map(xf);
    for(const f of T) out.push(f);
  }
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

  // ---- chassis: frame, tanks, steps, bumpers, ICC bar, engine (bared by the hood) ----
  function buildFrame(out,s){
    for(const sx of [-1,1]) boxAt(out, sx*0.50-0.045, sx*0.50+0.045, -5.26, 4.00, 0.50, 0.66, 'iron', -0.15);
    for(const y of [-4.90,-3.90,-2.60,-1.30,0.00,1.30,2.60,3.60]) boxAt(out,-0.50,0.50,y-0.05,y+0.05,0.50,0.60,'iron',-0.3);
    tube(out,[0.90,0.90,0.62],[0.90,2.05,0.62],0.30,12,'galv',-0.1,true);     // cylindrical fuel tank, curb
    boxAt(out, -1.14,-0.84, 0.30, 0.90, 0.44, 0.86, 'rubber', -0.25);         // battery box, street
    boxAt(out, -1.02,-0.64, -1.20, -0.40, 0.42, 0.76, 'galv', -0.3);          // muffler/DPF, street
    tube(out,[-0.62,-3.40,0.24],[-0.62,-3.58,0.23],0.045,8,'chrome',0.15);    // exhaust tip, street aft
    for(const sx of [-1,1]){                                                   // two fixed treads per side
      boxAt(out, Math.min(sx*0.84,sx*1.16), Math.max(sx*0.84,sx*1.16), 1.55, 1.85, 0.32, 0.38, 'iron', -0.2);
      boxAt(out, Math.min(sx*0.86,sx*1.16), Math.max(sx*0.86,sx*1.16), 1.55, 1.85, 0.64, 0.70, 'iron', -0.15);
      bar(out, [sx*1.00,1.70,0.38],[sx*1.00,1.70,0.64],0.022,'iron',-0.4);
    }
    boxAt(out, -1.06,1.06, G.bumpF[0], G.bumpF[1], 0.30, 0.62, 'galv', 0.02); // front bumper (stays put)
    for(const sx of [-1,1])                                                    // rear bumperettes
      boxAt(out, Math.min(sx*0.90,sx*1.18), Math.max(sx*0.90,sx*1.18), -5.30, -5.12, 0.44, 0.62, 'galv', 0.0);
    boxAt(out, -0.94,0.94, -5.26, -5.20, 0.40, 0.48, 'iron', -0.1);           // ICC underride bar
    for(const sx of [-1,1]) bar(out,[sx*0.48,-5.06,0.50],[sx*0.48,-5.23,0.48],0.032,'iron',-0.3);
    for(const sx of [-1,1])                                                    // tail lamps under the floor
      boxAt(out, Math.min(sx*0.98,sx*1.18), Math.max(sx*0.98,sx*1.18), -5.14, -5.08, 0.80, 0.98, 'lensR', 0.25);
    if(s.mudflaps) for(const sx of [-1,1]){
      wallY(out, -3.62, Math.min(sx*0.50,sx*1.06), Math.max(sx*0.50,sx*1.06), 0.06, 0.48, 'rubber', -0.4, -1);
      wallY(out, -3.615, Math.min(sx*0.50,sx*1.06), Math.max(sx*0.50,sx*1.06), 0.06, 0.48, 'rubber', -0.8, +1);
    }
    // engine bay (bared by the tilted clip): walls, block, valve cover, radiator, air cleaner
    wallY(out, 2.44, -0.84, 0.84, 0.60, 1.55, 'shade', -0.8, +1);             // firewall
    wallX(out, 0.80, 2.46, 4.02, 0.62, 1.36, 'shade', -0.8, -1);
    wallX(out,-0.80, 2.46, 4.02, 0.62, 1.36, 'shade', -0.8, +1);
    boxAt(out, -0.64,0.64, 4.02, 4.08, 0.55, 1.30, 'shade', -0.6);            // radiator wall
    boxAt(out, -0.36,0.36, 2.62, 3.62, 0.55, 1.15, 'iron', -0.3);             // block
    boxAt(out, -0.28,0.28, 2.70, 3.55, 1.15, 1.26, 'galv', -0.1);             // valve cover
    tube(out,[0.56,2.90,1.05],[0.56,3.30,1.05],0.15,10,'rubber',-0.2);        // air cleaner, curb
    for(const sx of [-1,1]) boxAt(out, Math.min(sx*0.86,sx*0.94), Math.max(sx*0.86,sx*0.94), 2.44, 2.50, 1.46, 1.54, 'trim', 0.3);  // hood latches
  }

  // ---- the front clip: hood, cheeks, fenders, arches, grille, headlamps — one hinged group ----
  function buildHoodClip(out,s){
    const a = -s.hood*G.hoodDeg*DEG, ca=Math.cos(a), sa=Math.sin(a);
    part(out,(T)=>{
      const wear=wearTex(s.weather);
      const y0=G.hoodY0, y1=G.hoodY1, zc=G.hoodZc, zn=G.hoodZn;
      // hood top, crowned centre, + underside (seen tilted)
      quad(T, [0,y0,zc+0.02],[G.hwHood,y0,zc-0.02],[G.hwHood,y1,zn-0.02],[0,y1,zn+0.02], 'paint', 0.34, 0,
        [[0,y0],[G.hwHood,y0],[G.hwHood,y1],[0,y1]], wear);
      quad(T, [-G.hwHood,y0,zc-0.02],[0,y0,zc+0.02],[0,y1,zn+0.02],[-G.hwHood,y1,zn-0.02], 'paint', 0.34, 0,
        [[-G.hwHood,y0],[0,y0],[0,y1],[-G.hwHood,y1]], wear);
      quad(T, [G.hwHood,y0,zc-0.03],[0,y0,zc+0.01],[0,y1,zn+0.01],[G.hwHood,y1,zn-0.03], 'leaf', -0.9);
      quad(T, [0,y0,zc+0.01],[-G.hwHood,y0,zc-0.03],[-G.hwHood,y1,zn-0.03],[0,y1,zn+0.01], 'leaf', -0.9);
      wallY(T, y1, -G.hwHood, G.hwHood, 1.38, zn-0.02, 'paint', 0.0, +1);      // front edge drop
      for(const sx of [-1,1]){
        // cheek: hood edge down-out to the fender ledge
        if(sx>0) quad(T, [0.82,y0,1.58],[1.04,y0,1.30],[1.04,y1,1.26],[0.82,y1,1.42], 'paint', 0.24);
        else     quad(T, [-1.04,y0,1.30],[-0.82,y0,1.58],[-0.82,y1,1.42],[-1.04,y1,1.26], 'paint', 0.24);
        // fender top ledge
        quad(T, sx>0?[1.04,y0,G.fenderTopZ]:[-G.hwFender,y0,G.fenderTopZ],
                sx>0?[G.hwFender,y0,G.fenderTopZ]:[-1.04,y0,G.fenderTopZ],
                sx>0?[G.hwFender,y1-0.02,1.26]:[-1.04,y1-0.02,1.26],
                sx>0?[1.04,y1-0.02,1.26]:[-G.hwFender,y1-0.02,1.26], 'paint', 0.30, 0, null, wear);
        // fender outer panel with the arch cutout — the widest point of the truck
        archPanel(T, sx*G.hwFender, G.archFy[0], G.archFy[1], 0.46, 1.28, 'paint', sx>0?0.18:-0.42, sx,
          [{yc:G.axF, r:G.archF.r, zc:G.archF.zc}], wear);
        // arch liner (above the tire top)
        wallX(T, sx*1.00, G.axF-G.archF.r, G.axF+G.archF.r, 0.86, 1.05, 'shade', -0.85, sx);
        // fender front face: lamp block flanking the grille, painted surrounds
        boxAt(T, Math.min(sx*0.62,sx*1.02), Math.max(sx*0.62,sx*1.02), 4.10, 4.15, 1.00, 1.22, s.night?'glow':'head', 0.35);
        wallY(T, y1, Math.min(sx*0.62,sx*1.02), Math.max(sx*0.62,sx*1.02), 0.46, 1.00, 'paint', 0.05, +1, null, wear);
        wallY(T, y1, Math.min(sx*0.62,sx*1.02), Math.max(sx*0.62,sx*1.02), 1.22, 1.38, 'paint', 0.05, +1);
        wallY(T, y1, Math.min(sx*0.58,sx*0.62), Math.max(sx*0.58,sx*0.62), 0.46, 1.38, 'paint', 0.05, +1);
        wallY(T, y1, Math.min(sx*1.02,sx*G.hwFender), Math.max(sx*1.02,sx*G.hwFender), 0.46, 1.30, 'paint', 0.05, +1, null, wear);
      }
      // centre grille, proud, + painted sill and header
      T.push(F([[0.58,4.15,0.66],[-0.58,4.15,0.66],[-0.58,4.15,1.34],[0.58,4.15,1.34]],'grille',-0.1,0,
        [[0.58,0.66],[-0.58,0.66],[-0.58,1.34],[0.58,1.34]], grilleTex()));
      wallY(T, y1, -0.58, 0.58, 0.46, 0.66, 'paint', 0.05, +1);
      wallY(T, y1, -0.58, 0.58, 1.34, 1.38, 'paint', 0.05, +1);
    }, s.hood>0 ? (p)=>rotX(p, G.hoodHinge.y, G.hoodHinge.z, ca, sa) : null);
  }

  // ---- cab: static — shell, glass, doors, mirrors, interior ----
  function buildDoors(T,s){
    const wear=wearTex(s.weather);
    for(const d of [ {sx:+1, pose:s.dR}, {sx:-1, pose:s.dL} ]){
      const sx=d.sx, y0=G.doorY[0], y1=G.doorY[1];
      const a=sx*d.pose*65*DEG, ca=Math.cos(a), sa=Math.sin(a);
      wallY(T, y1, Math.min(sx*0.92,sx*1.02), Math.max(sx*0.92,sx*1.02), G.doorZ0+0.02, G.doorHead, 'leaf', -0.5, -1);
      wallY(T, y0, Math.min(sx*0.92,sx*1.02), Math.max(sx*0.92,sx*1.02), G.doorZ0+0.02, G.doorHead, 'leaf', -0.6, +1);
      slab(T, sx>0?[[0.92,y0],[1.02,y0],[1.02,y1],[0.92,y1]]:[[-1.02,y0],[-0.92,y0],[-0.92,y1],[-1.02,y1]], G.doorZ0, 'leaf', -0.55);
      part(T,(D)=>{
        texWallX(D, sx*G.hwCab, y0, y1, G.doorZ0, 1.46, 'paint', sx>0?0.18:-0.42, sx, wear);
        wallX(D, sx*G.hwCab, y0, y1, 2.26, G.doorHead, 'paint', sx>0?0.10:-0.48, sx);
        wallX(D, sx*1.032, y0+0.06, y1-0.06, 1.46, 2.26, 'glass', sx>0?-0.15:-0.55, sx);
        wallX(D, sx*0.94, y0+0.02, y1-0.02, G.doorZ0+0.04, G.doorHead-0.04, 'leaf', -0.7, -sx);
        boxAt(D, Math.min(sx*1.03,sx*1.06), Math.max(sx*1.03,sx*1.06), y0+0.10, y0+0.30, 1.28, 1.34, 'trim', 0.3);
      }, (p)=>hingeZ(p, sx*0.98, y1, ca, sa));
    }
  }
  function buildCab(out,s){
    const wear=wearTex(s.weather);
    // windshield + pillars + header to the roof
    quad(out, [0.84,G.wsB.y,G.wsB.z],[-0.84,G.wsB.y,G.wsB.z],[-0.78,G.wsT.y,G.wsT.z],[0.78,G.wsT.y,G.wsT.z], 'glass', -0.18);
    quad(out, [0.98,G.wsB.y,G.wsB.z],[0.84,G.wsB.y,G.wsB.z],[0.78,G.wsT.y,G.wsT.z],[0.90,G.wsT.y,G.wsT.z], 'paint', 0.10);
    quad(out, [-0.84,G.wsB.y,G.wsB.z],[-0.98,G.wsB.y,G.wsB.z],[-0.90,G.wsT.y,G.wsT.z],[-0.78,G.wsT.y,G.wsT.z], 'paint', 0.10);
    quad(out, [0.90,G.wsT.y,G.wsT.z],[-0.90,G.wsT.y,G.wsT.z],[-G.hwCabRoof,2.06,2.84],[G.hwCabRoof,2.06,2.84], 'paint', 0.28);
    slab(out, [[-0.98,2.40],[0.98,2.40],[0.98,2.50],[-0.98,2.50]], 1.60, 'paint', 0.12);       // cowl strip
    wallY(out, G.cowlY, -1.02, 1.02, 0.80, 1.60, 'paint', 0.08, +1);                            // cowl face
    boxAt(out, -G.hwCabRoof, G.hwCabRoof, G.cabBackY, 2.06, 2.80, G.cabRoofZ, 'paint', 0.06, false, wear);
    for(const sx of [-1,1]){
      // side skirt, door posts, cant to the roof
      wallX(out, sx*G.hwCab, G.cabBackY, G.cowlY, 0.50, G.doorZ0, 'paint', sx>0?0.16:-0.44, sx, null, wear);
      wallX(out, sx*G.hwCab, G.doorY[1], G.cowlY, G.doorZ0, G.doorHead, 'paint', sx>0?0.16:-0.44, sx, null, wear);
      wallX(out, sx*G.hwCab, G.cabBackY, G.doorY[0], G.doorZ0, G.doorHead, 'paint', sx>0?0.16:-0.44, sx, null, wear);
      if(sx>0) quad(out, [1.02,1.30,G.doorHead],[1.02,2.42,G.doorHead],[0.96,2.42,2.82],[0.96,1.30,2.82], 'paint', 0.12);
      else     quad(out, [-1.02,2.42,G.doorHead],[-1.02,1.30,G.doorHead],[-0.96,1.30,2.82],[-0.96,2.42,2.82], 'paint', -0.46);
      // mirrors on A-pillar arms (they stay with the cab; the hood tilts alone)
      if(s.mirrors){
        bar(out,[sx*0.99,2.34,2.28],[sx*1.28,2.50,2.20],0.020,'iron',-0.1);
        bar(out,[sx*0.99,2.34,1.92],[sx*1.28,2.50,1.98],0.018,'iron',-0.15);
        boxAt(out, Math.min(sx*1.24,sx*1.34), Math.max(sx*1.24,sx*1.34), 2.44, 2.54, 1.58, 1.98, 'dash', -0.05);
        wallY(out, 2.44, Math.min(sx*1.25,sx*1.33), Math.max(sx*1.25,sx*1.33), 1.62, 1.94, 'glass', -0.25, -1);
      }
    }
    wallY(out, G.cabBackY, -1.02, 1.02, 0.62, G.doorHead, 'paint', -0.30, -1, null, wear);      // back wall
    wallY(out, G.cabBackY, -G.hwCabRoof, G.hwCabRoof, G.doorHead, 2.82, 'paint', -0.32, -1);
    // interior: FLAT floor (the engine is ahead of the firewall), dash, wheel, two buckets
    slab(out, [[-0.94,1.34],[0.94,1.34],[0.94,2.40],[-0.94,2.40]], G.cabFloorZ, 'rubber', -0.35, ribTex());
    boxAt(out, -0.92,0.92, 2.22, 2.40, 1.22, 1.42, 'dash', -0.4);
    tube(out, [-0.56,2.16,1.30],[-0.56,2.08,1.37], 0.17, 10, 'dash', -0.2);
    bar(out, [-0.56,2.20,1.14],[-0.56,2.10,1.30], 0.024, 'iron', -0.4);
    for(const sx of [-1,1]){
      boxAt(out, Math.min(sx*0.32,sx*0.80), Math.max(sx*0.32,sx*0.80), 1.58, 2.02, 1.20, 1.36, 'cloth', -0.45);
      boxAt(out, Math.min(sx*0.32,sx*0.80), Math.max(sx*0.32,sx*0.80), 1.46, 1.60, 1.36, 1.90, 'cloth', -0.55);
    }
    slab(out, [[-0.92,1.34],[0.92,1.34],[0.92,2.12],[-0.92,2.12]], 2.74, 'shade', -1.0);        // headliner
    buildDoors(out,s);
  }

  // ---- cab roof fairing (part): the big tier's aero cap, carrying the five ID lamps ----
  function buildFairing(out,s){
    const idLamps=(y0,y1,z0,z1)=>{ for(const mx of [-0.56,-0.28,0,0.28,0.56])
      boxAt(out, mx-0.045, mx+0.045, y0, y1, z0, z1, 'lensA', 0.3); };
    if(!s.fairing){ idLamps(1.96, 2.02, 2.86, 2.92); return; }                 // lamps on the bare roof
    const fw=G.fairing.hw, y0=G.fairing.y0, y1=G.fairing.y1, zT=G.fairing.zTop;
    const wear=wearTex(s.weather);
    quad(out, [fw,y1,2.86],[-fw,y1,2.86],[-fw,1.46,zT],[fw,1.46,zT], 'paint', 0.30, 0, null, wear);   // slope
    slab(out, [[-fw,y0],[fw,y0],[fw,1.46],[-fw,1.46]], zT, 'paint', 0.34);                            // top
    quad(out, [fw,y1,2.86],[fw,y0,2.86],[fw,y0,zT],[fw,1.46,zT], 'paint', 0.12);                      // curb side
    quad(out, [-fw,y0,2.86],[-fw,y1,2.86],[-fw,1.46,zT],[-fw,y0,zT], 'paint', -0.46);                 // street side
    wallY(out, y0, -fw, fw, 2.86, zT, 'shade', -0.6, -1);                                             // open aft face
    idLamps(1.66, 1.74, 3.08, 3.14);                                                                  // lamps ride the slope
  }

  // ---- the box: painted walls with panel seams, white roof, marker lamps, lined bay ----
  function buildBox(out,s){
    const sw=seamWearTex(s.weather);
    for(const sx of [-1,1]){
      texWallX(out, sx*G.hwBox, G.boxR, G.boxF, 1.08, 3.66, 'paint', sx>0?0.18:-0.42, sx, sw);
      wallX(out, sx*G.hwBox, G.boxR, G.boxF, 0.98, 1.08, 'galv', sx>0?0.02:-0.5, sx);          // skirt band
    }
    boxAt(out, -G.hwBox, G.hwBox, G.boxR, G.boxF, 3.64, G.boxRoofZ, 'trim', 0.10, false, wearTex(s.weather));
    wallY(out, G.boxF, -G.hwBox, G.hwBox, 1.08, 3.66, 'paint', 0.10, +1);
    for(const mx of [-0.28,0,0.28])                                                             // front clearance lamps
      boxAt(out, mx-0.045, mx+0.045, G.boxF, G.boxF+0.03, 3.52, 3.58, 'lensA', 0.3);
    // rear frame: posts + header around the roll-up opening, sill plate, corner markers
    for(const sx of [-1,1])
      wallY(out, G.boxR, Math.min(sx*G.openHW,sx*G.hwBox), Math.max(sx*G.openHW,sx*G.hwBox), 1.08, 3.66, 'paint', -0.30, -1);
    wallY(out, G.boxR, -G.openHW, G.openHW, G.headZ, 3.66, 'paint', -0.30, -1);
    boxAt(out, -G.openHW, G.openHW, G.boxR-0.02, G.boxR+0.04, 1.02, G.sillZ, 'galv', -0.05);
    for(const sx of [-1,1])
      boxAt(out, Math.min(sx*1.12,sx*1.20), Math.max(sx*1.12,sx*1.20), G.boxR-0.03, G.boxR, 3.52, 3.58, 'lensA', 0.3);
    // bay: rubber floor, lined walls, ceiling — flat nose to tail, no wheel tubs
    slab(out, [[-1.14,G.boxR+0.02],[1.14,G.boxR+0.02],[1.14,G.boxF-0.04],[-1.14,G.boxF-0.04]], G.boxFloorZ, 'rubber', -0.35, ribTex());
    wallX(out, 1.16, G.boxR+0.02, G.boxF-0.04, G.boxFloorZ, G.ceilZ, 'shade', -0.9, -1);
    wallX(out,-1.16, G.boxR+0.02, G.boxF-0.04, G.boxFloorZ, G.ceilZ, 'shade', -0.9, +1);
    wallY(out, G.boxF-0.04, -1.16, 1.16, G.boxFloorZ, G.ceilZ, 'shade', -0.9, -1);
    slab(out, [[-1.14,G.boxR+0.02],[1.14,G.boxR+0.02],[1.14,G.boxF-0.04],[-1.14,G.boxF-0.04]], G.ceilZ, 'shade', -1.0);
  }

  // ---- roll-up door: 15 slats on a corner track — the door never leaves the body ----
  function rollPath(u){
    if(u<=G.vTrack) return { y:G.boxR+0.03, z:G.sillZ+u };
    return { y:G.boxR+0.03+(u-G.vTrack)+0.06, z:G.stackZ };
  }
  function buildRollup(out,s){
    for(const sx of [-1,1]){
      boxAt(out, Math.min(sx*1.115,sx*1.145), Math.max(sx*1.115,sx*1.145), G.boxR-0.01, G.boxR+0.05, G.sillZ, G.sillZ+G.vTrack, 'galv', -0.3);
      boxAt(out, Math.min(sx*1.115,sx*1.145), Math.max(sx*1.115,sx*1.145), G.boxR+0.07, -2.40, G.stackZ-0.03, G.stackZ+0.03, 'galv', -0.35);
    }
    const shift = s.rollup*G.slats*G.slatH;
    for(let i=0;i<G.slats;i++){
      const Pa=rollPath(i*G.slatH+shift), Pb=rollPath((i+1)*G.slatH+shift);
      const bs = (i%2 ? 0.02 : -0.10);
      out.push(F([[-1.095,Pa.y,Pa.z],[1.095,Pa.y,Pa.z],[1.095,Pb.y,Pb.z],[-1.095,Pb.y,Pb.z]], 'galv', bs));
    }
    const P0=rollPath(shift);
    if(shift < 1.8){                                                     // bottom rail + handle
      const P1=rollPath(Math.max(0,shift-0.045));
      out.push(F([[-1.095,P1.y-0.012,P1.z],[1.095,P1.y-0.012,P1.z],[1.095,P0.y-0.012,P0.z],[-1.095,P0.y-0.012,P0.z]], 'iron', -0.2));
      boxAt(out, -0.16,0.16, P0.y-0.045, P0.y-0.012, P0.z+0.05, P0.z+0.11, 'trim', 0.3);
    }
  }

  // ---- tuck-under liftgate: swing out (folded) -> unfold -> lower to the ground ----
  function platePrism(out, fy, fz, d, n, len, x0, x1, th, mat, bT, tex){
    const a=[fy+d[0]*len, fz+d[1]*len];
    const V=(x,p,drop)=>[x, p[0]-(drop?n[0]*th:0), p[1]-(drop?n[1]*th:0)];
    const f=[fy,fz];
    const uv=tex?[[x1,0],[x0,0],[x0,len],[x1,len]]:null;
    out.push(F([V(x1,f),V(x0,f),V(x0,a),V(x1,a)], mat, bT, 0, uv, tex));                    // top
    out.push(F([V(x0,f,1),V(x1,f,1),V(x1,a,1),V(x0,a,1)], mat, bT-0.75));                   // bottom
    out.push(F([V(x0,f),V(x1,f),V(x1,f,1),V(x0,f,1)], mat, bT-0.25));                       // fwd edge
    out.push(F([V(x1,a),V(x0,a),V(x0,a,1),V(x1,a,1)], mat, bT-0.35));                       // aft edge
    out.push(F([V(x1,f),V(x1,a),V(x1,a,1),V(x1,f,1)], mat, bT-0.15));
    out.push(F([V(x0,a),V(x0,f),V(x0,f,1),V(x0,a,1)], mat, bT-0.45));
  }
  function gatePose(t){
    const gp=G.gate;
    const tA=Math.min(1,t/0.45), tB=Math.max(0,Math.min(1,(t-0.45)/0.25)), tC=Math.max(0,Math.min(1,(t-0.70)/0.30));
    const th=gp.stowDeg*DEG*(1-tA);
    const yF=gp.stowY+(gp.deployY-gp.stowY)*tA;
    const zF=(tC>0)? gp.dockZ+(gp.groundZ-gp.dockZ)*tC : gp.stowZ+(gp.dockZ-gp.stowZ)*tA;
    const phi=Math.PI*(1-tB);
    return { th, yF, zF, phi };
  }
  function buildGate(out,s){
    if(!s.liftgate) return;
    const gp=G.gate, q=gatePose(s.gate);
    const d=[-Math.cos(q.th), -Math.sin(q.th)], n=[-Math.sin(q.th), Math.cos(q.th)];
    const rib=ribTex();
    platePrism(out, q.yF, q.zF, d, n, gp.mainD, -gp.hwPlat, gp.hwPlat, gp.th, 'galv', 0.15, rib);
    const ay=q.yF+d[0]*gp.mainD, az=q.zF+d[1]*gp.mainD;
    const lift=0.055*(1-Math.cos(q.phi));
    const f2=[ d[0]*Math.cos(q.phi)+n[0]*Math.sin(q.phi), d[1]*Math.cos(q.phi)+n[1]*Math.sin(q.phi) ];
    const n2=[ n[0]*Math.cos(q.phi)-d[0]*Math.sin(q.phi), n[1]*Math.cos(q.phi)-d[1]*Math.sin(q.phi) ];
    platePrism(out, ay+n[0]*lift, az+n[1]*lift, f2, n2, gp.flipD, -gp.hwPlat+0.02, gp.hwPlat-0.02, gp.th, 'galv', 0.10, rib);
    for(const sx of [-1,1]){
      bar(out, [sx*0.80, gp.pivotY, gp.pivotZ], [sx*0.80, q.yF+d[0]*0.06, q.zF+d[1]*0.06-0.02], 0.038, 'iron', -0.25);
      bar(out, [sx*0.72, gp.pivotY, gp.pivotZ-0.08], [sx*0.72, q.yF+d[0]*0.10, q.zF+d[1]*0.10-0.06], 0.026, 'galv', -0.2);
      boxAt(out, sx*0.80-0.055, sx*0.80+0.055, gp.pivotY-0.09, gp.pivotY+0.09, gp.pivotZ-0.09, gp.pivotZ+0.11, 'iron', -0.3);
    }
  }

  // ---- wheels & axles: steered singles up front, duals aft, 8-lug heavy hubs ----
  function wheelAt(out, xc, yc, sxOut, roll, yawDeg){
    if(yawDeg){ const a=yawDeg*DEG, ca=Math.cos(a), sa=Math.sin(a);
      part(out,(T)=>wheelAt(T,xc,yc,sxOut,roll,0),(p)=>hingeZ(p,xc,yc,ca,sa)); return; }
    const r=G.wheelR, w=G.tireW, ph=roll*2*Math.PI;
    tube(out,[xc-w/2,yc,r],[xc+w/2,yc,r], r, 14, 'rubber', -0.05, true, treadTex(roll*2*Math.PI*r));
    const xf=xc+sxOut*(w/2+0.012);
    tube(out,[xc+sxOut*(w/2-0.02),yc,r],[xf,yc,r], r*0.56, 12, 'alloy', 0.25);
    for(let k=0;k<8;k++){ const th=ph+k*Math.PI/4, py=yc+Math.cos(th)*0.16, pz=r+Math.sin(th)*0.16;
      tube(out,[xf-sxOut*0.005,py,pz],[xf+sxOut*0.028,py,pz],0.028,6,'galv',0.35); }
    for(let k=0;k<4;k++){ const th=ph+Math.PI/8+k*Math.PI/2, py=yc+Math.cos(th)*0.27, pz=r+Math.sin(th)*0.27;
      tube(out,[xf,py,pz],[xf+sxOut*0.012,py,pz],0.048,6,'rubber',-0.6); }
    { const th=ph+Math.PI/3, py=yc+Math.cos(th)*0.31, pz=r+Math.sin(th)*0.31;
      tube(out,[xf,py,pz],[xf+sxOut*0.02,py,pz],0.022,5,'iron',-0.4); }
    tube(out,[xf,yc,r],[xf+sxOut*0.05,yc,r],0.066,8,'chrome',0.4);
  }
  function dualAt(out, sxOut, roll){
    const r=G.wheelR, w=G.tireW, yc=G.axR, ci=sxOut*G.dualXi;
    tube(out,[ci-w/2,yc,r],[ci+w/2,yc,r], r, 14, 'rubber', -0.20, true, treadTex(roll*2*Math.PI*r));
    wheelAt(out, sxOut*G.dualXo, yc, sxOut, roll, 0);
  }
  function buildWheels(out,s){
    const st=steerAngles(s.steer);
    tube(out,[-0.72,G.axF,G.wheelR],[0.72,G.axF,G.wheelR],0.050,8,'iron',-0.25);
    tube(out,[-0.90,G.axR,G.wheelR],[0.90,G.axR,G.wheelR],0.065,8,'iron',-0.25);
    tube(out,[-0.03,G.axR,G.wheelR],[0.29,G.axR,G.wheelR],0.16,10,'iron',-0.2);   // diff
    bar(out,[0.12,1.60,0.46],[0.12,G.axR+0.35,0.44],0.050,'iron',-0.3);           // driveshaft
    wheelAt(out,  G.frontWX, G.axF, +1, s.roll+s.wFR, st.R);
    wheelAt(out, -G.frontWX, G.axF, -1, s.roll+s.wFL, st.L);
    dualAt(out, +1, s.roll+s.wRR);
    dualAt(out, -1, s.roll+s.wRL);
  }

  function build(s){
    const body=[], rolling=[];
    buildFrame(body,s); buildHoodClip(body,s); buildCab(body,s); buildFairing(body,s);
    buildBox(body,s); buildRollup(body,s); buildGate(body,s);
    buildWheels(rolling,s);
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
    const s=resolve(opts), B=camBasis({dir,elev:opts.elev,yaw:s.yaw});
    return toRGBA(post(paint(build(s), B, makeMats(s), s), s));
  }
  function frames(dir, n, opts, cue){ n=n||8; const fn=CUES[cue||'rollup']||CUES.rollup, out=[];
    const cyclic = (cue==='roll'||cue==='bounce'||cue==='turn');
    for(let i=0;i<n;i++){ const t = cyclic ? i/n : i/(n-1);
      out.push(render(dir, Object.assign({}, opts, fn(t)))); }
    return out;
  }
  function project(dir, p, elev, yaw){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev,yaw})); return {x:v.sx,y:v.sy}; }
  function anchors(dir, opts){ opts=opts||{}; const s=resolve(opts), e=opts.elev;
    const P=(p)=>{ const q=project(dir,p,e,s.yaw); return { x:q.x, y:q.y, m:p }; };
    return {
      rollup:P([0,G.boxR+0.03,1.22]), sill:P([0,G.boxR,G.sillZ]), gate:P([0,-5.89,G.gate.dockZ]),
      cargo:P([0,-2.0,G.boxFloorZ]), hoodLatch:P([0,2.46,1.58]),
      doorL:P([-G.hwCab,1.90,1.45]), doorR:P([G.hwCab,1.90,1.45]),
      roof:P([0,-2.0,G.boxRoofZ]), fairing:P([0,1.67,3.16]),
      wheelFL:P([-G.frontWX,G.axF,G.wheelR]), wheelFR:P([G.frontWX,G.axF,G.wheelR]),
      wheelRL:P([-G.dualXo,G.axR,G.wheelR]), wheelRR:P([G.dualXo,G.axR,G.wheelR]),
      exhaust:P([-0.62,-3.58,0.23]),
      bodyL:s.B.bodyL, loa:s.B.loa, width:s.B.width, height:s.B.height, wheelbase:s.B.wheelbase,
    };
  }
  function list(){ return Object.keys(BODIES); }

  root.ConvBoxIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    BODY, TRIM, IRON, GALV, RUBBER, CHROME, CLOTH, GLASSD, GLASSN, KEY,
    BODIES, PRESETS, CUES, G, travel:{F:TF,R:TR},
    steer:{ maxInnerDeg:STEER_MAX, maxOuterDeg:+(steerAngles(1).R.toFixed(2)), angles:steerAngles },
    list, dims, resolve, render, frames, anchors, project, gatePose, rollPath };
})(typeof globalThis!=='undefined'?globalThis:window);
