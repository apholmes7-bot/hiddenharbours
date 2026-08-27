/* Hidden Harbours — parametric ISO ROAD-VEHICLE rig, CABOVER BOX TRUCK (same turntable + camera +
   shading as vehicleIsoRig.js / vanIsoRig.js / the fleet). Body: CABOVER BOX — a low-cab-forward
   city truck (NPR class): flat-face tilt cab, 4.56 m dry box behind it, dual rear wheels, roll-up
   rear door, tuck-under liftgate. 45deg steps, elev 40deg, flat-facet shading from the fixed
   upper-LEFT key, z-buffered, ordered dither, depth-edge darkening, NO AA, 32 px = 1 m, ringless
   from birth (ADR 0031). Cell 384 x 320 @ 192,214 — the same road cell as the dually and the van.

   ARTICULATION — pose params on render(dir,opts), 0..1 unless noted:
     dL dR             cab doors, hinged on their FORWARD edge, 0 -> 65deg
     rollup            rear roll-up door: 12 slats ride a track 1.88 m up the opening, then bend
                       forward and stack flat under the roof. The door NEVER leaves the body.
     gate              tuck-under liftgate, one param, three phases:
                       0..0.45 swing out from under the tail to dock height (folded),
                       0.45..0.70 the flip half unfolds aft to the full 1.20 m platform,
                       0.70..1 the arms lower the platform to the ground.
     tilt              cab tilt, 0 -> 38deg about the front hinge — the whole cab (doors, mirrors,
                       steps, interior) noses over and bares the engine. This class has no hood.
     roll              master wheel roll, REVOLUTIONS (cyclic); wFL wFR wRL wRR per-wheel offsets
     susF susR         suspension travel per axle, -1..1 (the BODY moves; wheels stay down)
     steer             front pair yaw, Ackermann-split, -1..1; +1 is full LEFT lock. Inner 33deg —
                       cabovers steer tight because the wheels sit at the corner; the black arch
                       flares (half-width 1.07 m) are what the tire corner must stay inside.
     yaw               heading off the 45deg grid, DEGREES (-45..45), rebaked under the fixed key.
   Parts (not poses): mirrors, mudflaps, liftgate (true|false — false is the plain-tail build).

   ORIGIN / PIVOT: ground-centre of the body footprint. +x curb side, +y nose, +z up.

   Exposes globalThis.BoxIso = { W,H,PX,DIRS,pivot,order,defaultElev, BODY,TRIM,IRON,GALV,RUBBER,
     CHROME,CLOTH,GLASSD,GLASSN,KEY, BODIES,PRESETS,CUES,G,travel,steer, list(), dims(opts),
     resolve(opts), render(dir,opts), frames(dir,n,opts,cue), anchors(dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 384, H = 320, cx = 192, groundY = 214;
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
  function treadTex(phase){ const c=0.0999; return (u,v)=>{ const f=(((u+phase)%c)+c)%c; return f<c*0.42?-1:0; }; }
  // c = 0.0999 m puts 21.007 stripe periods on the 2.099 m circumference: one revolution closes to a
  // 0.7 mm phase (sub-pixel), while a HALF revolution lands mid-stripe — so the loop seam is invisible
  // but the roll cue still visibly moves (the 0.095 m fleet period is half-rev symmetric on this tire).

  // ================= GEOMETRY — CABOVER BOX =================
  const TF=0.09, TR=0.12;                          // suspension travel per axle, metres
  const G = {
    noseY:3.28, bumpF:[3.30,3.42], bumpR:[-3.30,-3.12],
    cabBackY:1.72, axF:2.62, axR:-1.50, wheelR:0.334, tireW:0.24,
    frontWX:0.78, dualXi:0.58, dualXo:0.84,
    hwCab:0.98, hwFlare:1.07, hwCabRoof:0.92, cabRoofZ:2.30,
    wsB:{y:3.26,z:1.28}, wsT:{y:3.16,z:2.14},
    doorY:[1.84,2.96], doorZ0:0.82, doorHead:2.14, skirtZ0:0.42,
    cabFloorZ:0.85, tiltHinge:{y:3.20,z:0.50}, tiltDeg:38,
    hwBox:1.06, boxF:1.50, boxR:-3.06, boxFloorZ:0.92, boxRoofZ:3.04, ceilZ:2.96,
    openHW:0.95, sillZ:0.92, headZ:2.84, slatH:0.16, slats:12, vTrack:1.88, stackZ:2.88,
    archF:{r:0.44, zc:0.34}, flareArch:{r:0.50, zc:0.34}, archFy:[2.10,3.14],
    gate:{ pivotY:-2.62, pivotZ:0.56, mainD:0.62, flipD:0.58, hwPlat:0.95, th:0.05,
           stowY:-2.56, stowZ:0.66, stowDeg:26, deployY:-3.10, dockZ:0.92, groundZ:0.03 },
  };

  const BODIES = {
    caboverBox: { key:'caboverBox', label:'Cabover Box Truck', kind:'box_truck',
      loa:+(G.bumpF[1]-G.bumpR[0]).toFixed(2), bodyL:+(G.noseY-G.boxR).toFixed(2),
      width:+(G.hwFlare*2).toFixed(2), bodyW:+(G.hwBox*2).toFixed(2),
      height:+(G.boxRoofZ+0.04).toFixed(2), wheelbase:+(G.axF-G.axR).toFixed(2),
      boxLen:+(G.boxF-G.boxR).toFixed(2), boxFloor:G.boxFloorZ, wheels:6 },
  };

  // ---- steering: front pair yaw about their own vertical axes, Ackermann-split ----
  const STEER_MAX = 33;                                   // inner wheel, degrees, at full lock
  function steerAngles(v){
    if(!v) return { L:0, R:0 };
    const inner = Math.abs(v)*STEER_MAX*DEG;
    const outer = Math.atan(1/(1/Math.tan(inner) + (G.frontWX*2)/(G.axF-G.axR)));
    const i=inner/DEG, o=outer/DEG;
    return v>0 ? { L:+i, R:+o } : { L:-o, R:-i };
  }

  const PRESETS = {
    showroom:   { paint:'white', weather:0.05 },
    rental:     { paint:'white', weather:0.50 },
    fishmonger: { paint:'teal',  weather:0.42 },
    produce:    { paint:'sage',  weather:0.30 },
    nightHaul:  { paint:'blue',  weather:0.28, night:true },
  };
  const CUES = {
    doors: (t)=>({ dL:t, dR:t }),
    rollup:(t)=>({ rollup:t }),
    gate:  (t)=>({ gate:t }),
    tilt:  (t)=>({ tilt:t }),
    roll:  (t)=>({ roll:t }),                             // one revolution, cyclic
    steer: (t)=>({ steer:t*2-1 }),                        // full right lock -> full left lock
    turn:  (t)=>({ steer:Math.sin(t*Math.PI*2), yaw:Math.sin(t*Math.PI*2)*10, roll:t }),  // cyclic
    bounce:(t)=>({ susF:Math.sin(t*Math.PI*2)*0.7, susR:Math.sin(t*Math.PI*2+1.3)*0.7, roll:t }),
  };

  function resolve(opts){
    opts=opts||{};
    const g=(k,d)=> opts[k]!=null?opts[k]:d;
    const c01=(v)=>Math.max(0,Math.min(1,v)), c11=(v)=>Math.max(-1,Math.min(1,v));
    return {
      body:'caboverBox', B:BODIES.caboverBox,
      paint: opts.paint||'white', weather:g('weather',0.32),
      dL:c01(g('dL',0)), dR:c01(g('dR',0)), rollup:c01(g('rollup',0)),
      gate:c01(g('gate',0)), tilt:c01(g('tilt',0)),
      roll:g('roll',0), wFL:g('wFL',0), wFR:g('wFR',0), wRL:g('wRL',0), wRR:g('wRR',0),
      susF:c11(g('susF',0)), susR:c11(g('susR',0)),
      steer:c11(g('steer',0)), yaw:Math.max(-45,Math.min(45,g('yaw',0))),
      mirrors:g('mirrors',true), mudflaps:g('mudflaps',true), liftgate:g('liftgate',true),
      night:!!opts.night, outline: opts.outline!=null?!!opts.outline:KEYLINE_DEFAULT };
  }
  function dims(opts){ const s=resolve(opts);
    return Object.assign({ travelF:TF, travelR:TR }, s.B, { liftgate:s.liftgate }); }

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

  // ---- chassis: frame, tanks, bumpers, ICC bar, engine (bared by the tilt) ----
  function buildFrame(out,s){
    for(const sx of [-1,1]) boxAt(out, sx*0.48-0.04, sx*0.48+0.04, -3.26, 3.10, 0.44, 0.58, 'iron', -0.15);
    for(const y of [-2.90,-1.90,-0.70,0.50,1.60,2.90]) boxAt(out,-0.48,0.48,y-0.045,y+0.045,0.44,0.52,'iron',-0.3);
    boxAt(out, 0.60,1.00, 0.20, 1.20, 0.34, 0.74, 'galv', -0.2);              // fuel tank, curb side
    boxAt(out, -0.98,-0.60, 0.35, 0.85, 0.40, 0.74, 'rubber', -0.25);         // battery box, street
    boxAt(out, -1.00,-0.62, -1.00, -0.20, 0.36, 0.70, 'galv', -0.3);          // muffler/DPF, street
    tube(out,[-0.62,-2.58,0.24],[-0.62,-2.96,0.23],0.040,8,'chrome',0.15);    // exhaust tip, street aft
    boxAt(out, -1.04,1.04, G.bumpF[0], G.bumpF[1], 0.30, 0.62, 'galv', 0.02); // front bumper (steel)
    for(const sx of [-1,1])                                                    // rear bumperettes
      boxAt(out, Math.min(sx*0.55,sx*0.95), Math.max(sx*0.55,sx*0.95), -3.30, -3.12, 0.44, 0.60, 'galv', 0.0);
    boxAt(out, -0.92,0.92, -3.20, -3.14, 0.40, 0.48, 'iron', -0.1);           // ICC underride bar
    for(const sx of [-1,1]) bar(out,[sx*0.46,-3.00,0.46],[sx*0.46,-3.17,0.44],0.03,'iron',-0.3);
    for(const sx of [-1,1])                                                    // tail lamps under the floor
      boxAt(out, Math.min(sx*0.80,sx*0.99), Math.max(sx*0.80,sx*0.99), -3.12, -3.06, 0.60, 0.78, 'lensR', 0.25);
    if(s.mudflaps) for(const sx of [-1,1]){
      wallY(out, -2.10, Math.min(sx*0.46,sx*0.98), Math.max(sx*0.46,sx*0.98), 0.06, 0.42, 'rubber', -0.4, -1);
      wallY(out, -2.095, Math.min(sx*0.46,sx*0.98), Math.max(sx*0.46,sx*0.98), 0.06, 0.42, 'rubber', -0.8, +1);
    }
    // engine, radiator, air cleaner — what the tilted cab bares
    boxAt(out, -0.34,0.34, 2.15, 2.90, 0.50, 1.00, 'iron', -0.3);
    boxAt(out, -0.26,0.26, 2.20, 2.85, 1.00, 1.10, 'galv', -0.1);
    boxAt(out, -0.60,0.60, 3.12, 3.22, 0.50, 1.10, 'shade', -0.6);
    tube(out,[0.52,2.10,0.88],[0.52,2.46,0.88],0.13,10,'rubber',-0.2);
    boxAt(out, -0.10,0.10, 1.76, 1.90, 0.40, 0.56, 'iron', -0.2);             // cab tilt latch
  }

  // ---- cab doors (hinged forward; built INSIDE the tilting cab group) ----
  function buildDoors(T,s){
    const wear=wearTex(s.weather);
    for(const d of [ {sx:+1, pose:s.dR}, {sx:-1, pose:s.dL} ]){
      const sx=d.sx, y0=G.doorY[0], y1=G.doorY[1];
      const a=sx*d.pose*65*DEG, ca=Math.cos(a), sa=Math.sin(a);
      wallY(T, y1, Math.min(sx*0.88,sx*0.98), Math.max(sx*0.88,sx*0.98), G.doorZ0+0.02, G.doorHead, 'leaf', -0.5, -1);
      wallY(T, y0, Math.min(sx*0.88,sx*0.98), Math.max(sx*0.88,sx*0.98), G.doorZ0+0.02, G.doorHead, 'leaf', -0.6, +1);
      slab(T, sx>0?[[0.88,y0],[0.98,y0],[0.98,y1],[0.88,y1]]:[[-0.98,y0],[-0.88,y0],[-0.88,y1],[-0.98,y1]], G.doorZ0, 'leaf', -0.55);
      part(T,(D)=>{
        texWallX(D, sx*G.hwCab, y0, y1, G.doorZ0, 1.38, 'paint', sx>0?0.18:-0.42, sx, wear);
        wallX(D, sx*G.hwCab, y0, y1, 1.38, G.doorHead, 'paint', sx>0?0.10:-0.48, sx);
        wallX(D, sx*0.992, y0+0.06, y1-0.06, 1.44, 2.06, 'glass', sx>0?-0.15:-0.55, sx);
        wallX(D, sx*0.90, y0+0.02, y1-0.02, G.doorZ0+0.04, G.doorHead-0.04, 'leaf', -0.7, -sx);
        boxAt(D, Math.min(sx*0.99,sx*1.02), Math.max(sx*0.99,sx*1.02), y0+0.10, y0+0.30, 1.28, 1.34, 'trim', 0.3);
      }, (p)=>hingeZ(p, sx*0.94, y1, ca, sa));
    }
  }

  // ---- the tilt cab: shell, glass, flares, steps, mirrors, interior — one hinged group ----
  function buildCab(out,s){
    const a = -s.tilt*G.tiltDeg*DEG, ca=Math.cos(a), sa=Math.sin(a);
    part(out,(T)=>{
      const wear=wearTex(s.weather);
      // face: skirt to windshield base, grille band proud, headlamps at the belt
      wallY(T, G.noseY, -G.hwCab, G.hwCab, G.skirtZ0, G.wsB.z, 'paint', 0.10, +1, null, wear);
      T.push(F([[0.84,3.286,0.64],[-0.84,3.286,0.64],[-0.84,3.286,0.94],[0.84,3.286,0.94]],'grille',-0.1,0,
        [[0.84,0.64],[-0.84,0.64],[-0.84,0.94],[0.84,0.94]], grilleTex()));
      for(const sx of [-1,1])
        boxAt(T, Math.min(sx*0.60,sx*0.94), Math.max(sx*0.60,sx*0.94), G.noseY-0.01, G.noseY+0.04, 0.98, 1.16, s.night?'glow':'head', 0.35);
      // windshield (one raked plane) + pillars + header to the roof edge
      quad(T, [0.84,G.wsB.y,G.wsB.z],[-0.84,G.wsB.y,G.wsB.z],[-0.80,G.wsT.y,G.wsT.z],[0.80,G.wsT.y,G.wsT.z], 'glass', -0.18);
      quad(T, [0.98,G.wsB.y,G.wsB.z],[0.84,G.wsB.y,G.wsB.z],[0.80,G.wsT.y,G.wsT.z],[0.92,G.wsT.y,G.wsT.z], 'paint', 0.10);
      quad(T, [-0.84,G.wsB.y,G.wsB.z],[-0.98,G.wsB.y,G.wsB.z],[-0.92,G.wsT.y,G.wsT.z],[-0.80,G.wsT.y,G.wsT.z], 'paint', 0.10);
      quad(T, [0.92,G.wsT.y,G.wsT.z],[-0.92,G.wsT.y,G.wsT.z],[-G.hwCabRoof,3.10,G.cabRoofZ],[G.hwCabRoof,3.10,G.cabRoofZ], 'paint', 0.28);
      slab(T, [[-0.98,3.20],[0.98,3.20],[0.98,3.28],[-0.98,3.28]], G.wsB.z, 'paint', 0.12);   // cowl strip
      boxAt(T, -G.hwCabRoof, G.hwCabRoof, G.cabBackY+0.02, 3.10, G.cabRoofZ-0.04, G.cabRoofZ, 'paint', 0.06, false, wear);
      // sides: skirt with arch cutout, fixed panels fore/aft of the door, band above the door
      for(const sx of [-1,1]){
        archPanel(T, sx*G.hwCab, G.cabBackY, G.noseY, G.skirtZ0, G.doorZ0, 'paint', sx>0?0.18:-0.42, sx,
          [{yc:G.axF, r:G.archF.r, zc:G.archF.zc}], wear);
        wallX(T, sx*G.hwCab, G.doorY[1], G.noseY, G.doorZ0, G.doorHead, 'paint', sx>0?0.16:-0.44, sx, null, wear);
        wallX(T, sx*G.hwCab, G.cabBackY, G.doorY[0], G.doorZ0, G.doorHead, 'paint', sx>0?0.16:-0.44, sx, null, wear);
        wallX(T, sx*G.hwCab, G.cabBackY, G.noseY, G.doorHead, 2.26, 'paint', sx>0?0.12:-0.46, sx);
        // black arch flare — the widest point of the truck, and the steering envelope
        archPanel(T, sx*G.hwFlare, G.archFy[0], G.archFy[1], 0.44, 0.80, 'dash', sx>0?0.10:-0.45, sx,
          [{yc:G.axF, r:G.flareArch.r, zc:G.flareArch.zc}]);
        slab(T, sx>0?[[G.hwCab,G.archFy[0]],[G.hwFlare,G.archFy[0]],[G.hwFlare,G.archFy[1]],[G.hwCab,G.archFy[1]]]
                   :[[-G.hwFlare,G.archFy[0]],[-G.hwCab,G.archFy[0]],[-G.hwCab,G.archFy[1]],[-G.hwFlare,G.archFy[1]]],
             0.80, 'dash', 0.18);
        wallX(T, sx*0.95, G.axF-G.archF.r, G.axF+G.archF.r, 0.70, 0.86, 'shade', -0.85, sx);   // arch liner
        // steps under the door's aft half
        boxAt(T, Math.min(sx*0.86,sx*1.02), Math.max(sx*0.86,sx*1.02), 1.88, 2.14, 0.24, 0.30, 'iron', -0.2);
        boxAt(T, Math.min(sx*0.88,sx*1.02), Math.max(sx*0.88,sx*1.02), 1.88, 2.14, 0.52, 0.58, 'iron', -0.15);
        bar(T, [sx*0.94,2.01,0.30],[sx*0.94,2.01,0.52],0.020,'iron',-0.4);
        // mirrors on A-pillar arms (they ride the cab, not the doors)
        if(s.mirrors){
          bar(T,[sx*0.97,3.06,2.08],[sx*1.24,3.26,2.02],0.020,'iron',-0.1);
          bar(T,[sx*0.97,3.06,1.70],[sx*1.24,3.26,1.76],0.018,'iron',-0.15);
          boxAt(T, Math.min(sx*1.20,sx*1.30), Math.max(sx*1.20,sx*1.30), 3.20, 3.30, 1.68, 2.04, 'dash', -0.05);
          wallY(T, 3.20, Math.min(sx*1.21,sx*1.29), Math.max(sx*1.21,sx*1.29), 1.72, 2.00, 'glass', -0.25, -1);
        }
      }
      wallY(T, G.cabBackY, -G.hwCab, G.hwCab, 0.60, 2.26, 'paint', -0.30, -1, null, wear);     // back wall
      // interior: flat high floor, engine hump, dash, wheel, bucket + two-man bench
      slab(T, [[-0.94,1.78],[0.94,1.78],[0.94,3.16],[-0.94,3.16]], G.cabFloorZ, 'rubber', -0.35, ribTex());
      boxAt(T, -0.28,0.28, 1.90, 3.10, G.cabFloorZ, 1.24, 'dash', -0.45);
      boxAt(T, -0.90,0.90, 3.02, 3.24, 1.20, 1.32, 'dash', -0.4);
      tube(T, [-0.52,3.00,1.24],[-0.52,2.92,1.31], 0.17, 10, 'dash', -0.2);
      bar(T, [-0.52,3.04,1.10],[-0.52,2.94,1.26], 0.024, 'iron', -0.4);
      boxAt(T, -0.72,-0.30, 2.16, 2.62, G.cabFloorZ+0.07, 1.22, 'cloth', -0.45);
      boxAt(T, -0.72,-0.30, 2.06, 2.20, 1.22, 1.72, 'cloth', -0.55);
      boxAt(T, 0.14,0.90, 2.16, 2.62, G.cabFloorZ+0.07, 1.22, 'cloth', -0.45);
      boxAt(T, 0.14,0.90, 2.06, 2.20, 1.22, 1.72, 'cloth', -0.55);
      slab(T, [[-0.92,1.80],[0.92,1.80],[0.92,3.12],[-0.92,3.12]], 2.22, 'shade', -1.0);       // headliner
      buildDoors(T,s);
    }, s.tilt>0 ? (p)=>rotX(p, G.tiltHinge.y, G.tiltHinge.z, ca, sa) : null);
  }

  // ---- the box: painted walls with panel seams, white roof, marker lamps, lined bay ----
  function buildBox(out,s){
    const sw=seamWearTex(s.weather);
    for(const sx of [-1,1]){
      texWallX(out, sx*G.hwBox, G.boxR, G.boxF, 0.90, 3.02, 'paint', sx>0?0.18:-0.42, sx, sw);
      wallX(out, sx*G.hwBox, G.boxR, G.boxF, 0.82, 0.90, 'galv', sx>0?0.02:-0.5, sx);          // skirt band
    }
    boxAt(out, -G.hwBox, G.hwBox, G.boxR, G.boxF, 3.00, G.boxRoofZ, 'trim', 0.10, false, wearTex(s.weather));
    wallY(out, G.boxF, -G.hwBox, G.hwBox, 0.90, 3.02, 'paint', 0.10, +1);
    for(const mx of [-0.24,0,0.24])                                                             // clearance lamps
      boxAt(out, mx-0.04, mx+0.04, G.boxF, G.boxF+0.03, 2.90, 2.96, 'lensA', 0.3);
    // rear frame: posts + header around the roll-up opening, sill plate, corner markers
    for(const sx of [-1,1])
      wallY(out, G.boxR, Math.min(sx*G.openHW,sx*G.hwBox), Math.max(sx*G.openHW,sx*G.hwBox), 0.90, 3.02, 'paint', -0.30, -1);
    wallY(out, G.boxR, -G.openHW, G.openHW, G.headZ, 3.02, 'paint', -0.30, -1);
    boxAt(out, -G.openHW, G.openHW, G.boxR-0.02, G.boxR+0.04, 0.86, G.sillZ, 'galv', -0.05);
    for(const sx of [-1,1])
      boxAt(out, Math.min(sx*0.98,sx*1.05), Math.max(sx*0.98,sx*1.05), G.boxR-0.03, G.boxR, 2.88, 2.94, 'lensA', 0.3);
    // bay: rubber floor, lined walls, ceiling — flat nose to tail, no wheel tubs
    slab(out, [[-1.00,G.boxR+0.02],[1.00,G.boxR+0.02],[1.00,G.boxF-0.04],[-1.00,G.boxF-0.04]], G.boxFloorZ, 'rubber', -0.35, ribTex());
    wallX(out, 1.00, G.boxR+0.02, G.boxF-0.04, G.boxFloorZ, G.ceilZ, 'shade', -0.9, -1);
    wallX(out,-1.00, G.boxR+0.02, G.boxF-0.04, G.boxFloorZ, G.ceilZ, 'shade', -0.9, +1);
    wallY(out, G.boxF-0.04, -1.00, 1.00, G.boxFloorZ, G.ceilZ, 'shade', -0.9, -1);
    slab(out, [[-1.00,G.boxR+0.02],[1.00,G.boxR+0.02],[1.00,G.boxF-0.04],[-1.00,G.boxF-0.04]], G.ceilZ, 'shade', -1.0);
  }

  // ---- roll-up door: 12 slats on a corner track — the door never leaves the body ----
  function rollPath(u){
    if(u<=G.vTrack) return { y:G.boxR+0.03, z:G.sillZ+u };
    return { y:G.boxR+0.03+(u-G.vTrack)+0.06, z:G.stackZ };
  }
  function buildRollup(out,s){
    for(const sx of [-1,1]){
      boxAt(out, Math.min(sx*0.955,sx*0.985), Math.max(sx*0.955,sx*0.985), G.boxR-0.01, G.boxR+0.05, G.sillZ, G.sillZ+G.vTrack, 'galv', -0.3);
      boxAt(out, Math.min(sx*0.955,sx*0.985), Math.max(sx*0.955,sx*0.985), G.boxR+0.07, -1.02, G.stackZ-0.03, G.stackZ+0.03, 'galv', -0.35);
    }
    const shift = s.rollup*G.slats*G.slatH;
    for(let i=0;i<G.slats;i++){
      const Pa=rollPath(i*G.slatH+shift), Pb=rollPath((i+1)*G.slatH+shift);
      const bs = (i%2 ? 0.02 : -0.10);
      out.push(F([[-0.945,Pa.y,Pa.z],[0.945,Pa.y,Pa.z],[0.945,Pb.y,Pb.z],[-0.945,Pb.y,Pb.z]], 'galv', bs));
    }
    const P0=rollPath(shift);
    if(shift < 1.5){                                                     // bottom rail + handle
      const P1=rollPath(Math.max(0,shift-0.045));
      out.push(F([[-0.945,P1.y-0.012,P1.z],[0.945,P1.y-0.012,P1.z],[0.945,P0.y-0.012,P0.z],[-0.945,P0.y-0.012,P0.z]], 'iron', -0.2));
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
      bar(out, [sx*0.72, gp.pivotY, gp.pivotZ], [sx*0.72, q.yF+d[0]*0.06, q.zF+d[1]*0.06-0.02], 0.035, 'iron', -0.25);
      bar(out, [sx*0.64, gp.pivotY, gp.pivotZ-0.08], [sx*0.64, q.yF+d[0]*0.10, q.zF+d[1]*0.10-0.06], 0.024, 'galv', -0.2);
      boxAt(out, sx*0.72-0.05, sx*0.72+0.05, gp.pivotY-0.08, gp.pivotY+0.08, gp.pivotZ-0.08, gp.pivotZ+0.10, 'iron', -0.3);
    }
  }

  // ---- wheels & axles: steered singles up front, duals aft ----
  function wheelAt(out, xc, yc, sxOut, roll, yawDeg){
    if(yawDeg){ const a=yawDeg*DEG, ca=Math.cos(a), sa=Math.sin(a);
      part(out,(T)=>wheelAt(T,xc,yc,sxOut,roll,0),(p)=>hingeZ(p,xc,yc,ca,sa)); return; }
    const r=G.wheelR, w=G.tireW, ph=roll*2*Math.PI;
    tube(out,[xc-w/2,yc,r],[xc+w/2,yc,r], r, 14, 'rubber', -0.05, true, treadTex(roll*2*Math.PI*r));
    const xf=xc+sxOut*(w/2+0.012);
    tube(out,[xc+sxOut*(w/2-0.02),yc,r],[xf,yc,r], r*0.56, 12, 'alloy', 0.25);
    for(let k=0;k<6;k++){ const th=ph+k*Math.PI/3, py=yc+Math.cos(th)*0.118, pz=r+Math.sin(th)*0.118;
      tube(out,[xf-sxOut*0.005,py,pz],[xf+sxOut*0.026,py,pz],0.026,6,'galv',0.35); }
    for(let k=0;k<4;k++){ const th=ph+Math.PI/8+k*Math.PI/2, py=yc+Math.cos(th)*0.20, pz=r+Math.sin(th)*0.20;
      tube(out,[xf,py,pz],[xf+sxOut*0.012,py,pz],0.042,6,'rubber',-0.6); }
    { const th=ph+Math.PI/3, py=yc+Math.cos(th)*0.24, pz=r+Math.sin(th)*0.24;
      tube(out,[xf,py,pz],[xf+sxOut*0.02,py,pz],0.020,5,'iron',-0.4); }
    tube(out,[xf,yc,r],[xf+sxOut*0.04,yc,r],0.060,8,'chrome',0.4);
  }
  function dualAt(out, sxOut, roll){
    const r=G.wheelR, w=G.tireW, yc=G.axR, ci=sxOut*G.dualXi;
    tube(out,[ci-w/2,yc,r],[ci+w/2,yc,r], r, 14, 'rubber', -0.20, true, treadTex(roll*2*Math.PI*r));
    wheelAt(out, sxOut*G.dualXo, yc, sxOut, roll, 0);
  }
  function buildWheels(out,s){
    const st=steerAngles(s.steer);
    tube(out,[-0.66,G.axF,G.wheelR],[0.66,G.axF,G.wheelR],0.048,8,'iron',-0.25);
    tube(out,[-0.84,G.axR,G.wheelR],[0.84,G.axR,G.wheelR],0.060,8,'iron',-0.25);
    tube(out,[-0.02,G.axR,G.wheelR],[0.26,G.axR,G.wheelR],0.145,10,'iron',-0.2);   // diff
    bar(out,[0.10,1.40,0.42],[0.10,G.axR+0.32,0.40],0.045,'iron',-0.3);            // driveshaft
    wheelAt(out,  G.frontWX, G.axF, +1, s.roll+s.wFR, st.R);
    wheelAt(out, -G.frontWX, G.axF, -1, s.roll+s.wFL, st.L);
    dualAt(out, +1, s.roll+s.wRR);
    dualAt(out, -1, s.roll+s.wRL);
  }

  function build(s){
    const body=[], rolling=[];
    buildFrame(body,s); buildCab(body,s); buildBox(body,s);
    buildRollup(body,s); buildGate(body,s);
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
      rollup:P([0,G.boxR+0.03,0.98]), sill:P([0,G.boxR,G.sillZ]), gate:P([0,-3.70,G.gate.dockZ]),
      cargo:P([0,-0.78,G.boxFloorZ]), tiltLatch:P([0,1.83,0.48]),
      doorL:P([-G.hwCab,2.02,1.30]), doorR:P([G.hwCab,2.02,1.30]),
      roof:P([0,-0.78,G.boxRoofZ]),
      wheelFL:P([-G.frontWX,G.axF,G.wheelR]), wheelFR:P([G.frontWX,G.axF,G.wheelR]),
      wheelRL:P([-G.dualXo,G.axR,G.wheelR]), wheelRR:P([G.dualXo,G.axR,G.wheelR]),
      exhaust:P([-0.62,-2.96,0.23]),
      bodyL:s.B.bodyL, loa:s.B.loa, width:s.B.width, height:s.B.height, wheelbase:s.B.wheelbase,
    };
  }
  function list(){ return Object.keys(BODIES); }

  root.BoxIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    BODY, TRIM, IRON, GALV, RUBBER, CHROME, CLOTH, GLASSD, GLASSN, KEY,
    BODIES, PRESETS, CUES, G, travel:{F:TF,R:TR},
    steer:{ maxInnerDeg:STEER_MAX, maxOuterDeg:+(steerAngles(1).R.toFixed(2)), angles:steerAngles },
    list, dims, resolve, render, frames, anchors, project, gatePose, rollPath };
})(typeof globalThis!=='undefined'?globalThis:window);
