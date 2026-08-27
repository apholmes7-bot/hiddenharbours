/* Hidden Harbours — parametric ISO ROAD-VEHICLE rig, CLASSIC LONG-NOSE SEMI TRACTOR (same
   turntable + camera + shading as aeroSemiIsoRig.js / the fleet). Body: CLASSIC SEMI — a square-hood
   owner-operator tractor (W900/389 class): 2.3 m level hood, chrome grille and bumper, fender-pod
   headlamps, drop visor, flat-top 1.43 m sleeper, TWIN CHROME STACKS behind the cab, chrome tanks,
   bare frame (no skirts on this tier), tandem duals, and the SAME FIFTH-WHEEL HANDSHAKE as the aero:
   plate top z 1.18, slot aft, kingpin-to-cab-back 1.52 m — the pack's trailers couple to either
   tractor interchangeably. 45deg steps, elev 40deg, fixed upper-LEFT key, z-buffered, ordered
   dither, NO AA, 32 px = 1 m, ringless from birth (ADR 0031). Cell 384 x 320 @ 192,214.

   ARTICULATION — pose params on render(dir,opts), 0..1 unless noted:
     dL dR             cab doors, hinged on their FORWARD edge, 0 -> 65deg
     hood              the whole FRONT CLIP — hood, fenders, pods, grille — tilts forward
                       0 -> 70deg about a hinge at the bumper line and bares the engine.
     roll              master wheel roll, REVOLUTIONS (cyclic); wFL wFR wRL wRR per-CORNER offsets
     susF susR         suspension travel per axle group, -1..1 (the BODY moves; wheels stay down)
     steer             front pair yaw, Ackermann-split, -1..1; +1 is full LEFT lock. Inner 30deg —
                       the long hood buys presence and pays for it in lock.
     yaw               heading off the 45deg grid, DEGREES (-45..45), rebaked under the fixed key.
   Parts (not poses): mirrors (west-coast bars), mudflaps, visor (drop visor over the windshield).

   ORIGIN / PIVOT: ground-centre of the body footprint. +x curb side, +y nose, +z up.

   Exposes globalThis.ClassicSemiIso = { W,H,PX,DIRS,pivot,order,defaultElev, BODY,TRIM,IRON,GALV,
     RUBBER,CHROME,CLOTH,GLASSD,GLASSN,KEY, BODIES,PRESETS,CUES,G,travel,steer, list(), dims(opts),
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
  function grilleTex(){ const p=0.082; return (u,v)=>{ const f=((u%p)+p)%p; return f<0.034?2:0; }; }   // VERTICAL bars — the classic grille
  function ribTex(){ const p=0.15; return (u,v)=>{ const f=((u%p)+p)%p; return f<0.055?-1:0; }; }
  function gridTex(){ const p=0.12; return (u,v)=>{ const fu=((u%p)+p)%p, fv=((v%p)+p)%p; return (fu<0.03||fv<0.03)?-1:0; }; }
  function treadTex(phase){ const c=0.1083; return (u,v)=>{ const f=(((u+phase)%c)+c)%c; return f<c*0.42?-1:0; }; }
  // c = 0.1083 m puts 29.008 stripe periods on the 3.1416 m circumference — invisible seam, visible motion.

  // ================= GEOMETRY — CLASSIC LONG-NOSE SEMI =================
  const TF=0.09, TR=0.11;                          // suspension travel per axle group, metres
  const G = {
    noseY:4.35, bumpF:[4.37,4.50], frameR:-3.35,
    cowlY:1.95, cabBackY:-0.78, axF:3.45, tandA:-1.60, tandB:-2.80, axR:-2.20,  // axR = tandem centre
    wheelR:0.50, tireW:0.30, frontWX:0.84, dualXi:0.60, dualXo:0.90,
    hwCab:1.22, hwCabRoof:1.10, cabRoofZ:2.92, cabFloorZ:1.15,
    hwHood:0.80, hoodZc:1.78, hoodZn:1.62, hoodY0:2.01, hoodY1:4.32,
    hwFender:1.27, fenderTopZ:1.38, hoodHinge:{y:4.42,z:0.55}, hoodDeg:70,
    wsB:{y:1.93,z:1.82}, wsT:{y:1.79,z:2.62},
    doorY:[0.65,1.67], doorZ0:1.00, doorHead:2.46,
    archF:{r:0.72, zc:0.44}, archFy:[2.68,4.22],
    stacks:{x:1.08, y:-0.92, r:0.072, z0:0.98, z1:3.55, shield:[1.10,2.20]},
    visor:{y0:1.66, y1:1.98, zBack:2.88, zFront:2.70, hw:1.08},
    fw:{y:-2.30, plate:[-2.70,-1.90], hw:0.45, topZ:1.18, rampTo:{y:-2.92,z:1.00}},
    tank:{x:0.88, r:0.30, y:[-0.30,1.10], z:0.62},
  };

  const BODIES = {
    classicSemi: { key:'classicSemi', label:'Classic Long-Nose Semi', kind:'semi_tractor',
      loa:+(G.bumpF[1]-G.frameR).toFixed(2), bodyL:+(G.noseY-G.frameR).toFixed(2),
      width:+(G.hwFender*2).toFixed(2), bodyW:+(G.hwCab*2).toFixed(2),
      height:+(G.stacks.z1+0.04).toFixed(2), cabRoof:G.cabRoofZ, wheelbase:+(G.axF-G.axR).toFixed(2),
      sleeper:+(G.doorY[0]-G.cabBackY).toFixed(2), fwZ:G.fw.topZ, fwY:G.fw.y, wheels:10 },
  };

  // ---- steering: front pair yaw about their own vertical axes, Ackermann-split ----
  const STEER_MAX = 30;                                   // inner wheel, degrees, at full lock
  function steerAngles(v){
    if(!v) return { L:0, R:0 };
    const inner = Math.abs(v)*STEER_MAX*DEG;
    const outer = Math.atan(1/(1/Math.tan(inner) + (G.frontWX*2)/(G.axF-G.axR)));
    const i=inner/DEG, o=outer/DEG;
    return v>0 ? { L:+i, R:+o } : { L:-o, R:-i };
  }

  const PRESETS = {
    showroom:    { paint:'white', weather:0.05 },
    ownerOp:     { paint:'red',   weather:0.30 },
    blackline:   { paint:'greyShingle', weather:0.40 },
    harbourLine: { paint:'teal',  weather:0.32 },
    midnight:    { paint:'plum',  weather:0.28, night:true },
  };
  const CUES = {
    doors: (t)=>({ dL:t, dR:t }),
    hood:  (t)=>({ hood:t }),
    roll:  (t)=>({ roll:t }),                             // one revolution, cyclic
    steer: (t)=>({ steer:t*2-1 }),                        // full right lock -> full left lock
    turn:  (t)=>({ steer:Math.sin(t*Math.PI*2), yaw:Math.sin(t*Math.PI*2)*7, roll:t }),  // cyclic
    bounce:(t)=>({ susF:Math.sin(t*Math.PI*2)*0.7, susR:Math.sin(t*Math.PI*2+1.3)*0.7, roll:t }),
  };

  function resolve(opts){
    opts=opts||{};
    const g=(k,d)=> opts[k]!=null?opts[k]:d;
    const c01=(v)=>Math.max(0,Math.min(1,v)), c11=(v)=>Math.max(-1,Math.min(1,v));
    return {
      body:'classicSemi', B:BODIES.classicSemi,
      paint: opts.paint||'white', weather:g('weather',0.32),
      dL:c01(g('dL',0)), dR:c01(g('dR',0)), hood:c01(g('hood',0)),
      roll:g('roll',0), wFL:g('wFL',0), wFR:g('wFR',0), wRL:g('wRL',0), wRR:g('wRR',0),
      susF:c11(g('susF',0)), susR:c11(g('susR',0)),
      steer:c11(g('steer',0)), yaw:Math.max(-45,Math.min(45,g('yaw',0))),
      mirrors:g('mirrors',true), mudflaps:g('mudflaps',true), visor:g('visor',true),
      night:!!opts.night, outline: opts.outline!=null?!!opts.outline:KEYLINE_DEFAULT };
  }
  function dims(opts){ const s=resolve(opts);
    return Object.assign({ travelF:TF, travelR:TR }, s.B, { visor:s.visor }); }

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

  // ---- chassis: frame, chrome tanks, steps, deck, fifth wheel, stacks, engine ----
  function buildFrame(out,s){
    for(const sx of [-1,1]) boxAt(out, sx*0.47-0.045, sx*0.47+0.045, G.frameR, 4.20, 0.55, 0.95, 'iron', -0.15);
    for(const y of [-3.20,-2.20,-1.10,-0.10,1.00,2.10,3.20,4.00]) boxAt(out,-0.47,0.47,y-0.05,y+0.05,0.55,0.68,'iron',-0.3);
    for(const sx of [-1,1]){                                                   // CHROME tanks both sides
      tube(out,[sx*G.tank.x,G.tank.y[0],G.tank.z],[sx*G.tank.x,G.tank.y[1],G.tank.z],G.tank.r,12,'chrome',-0.05,true);
      boxAt(out, Math.min(sx*0.84,sx*1.16), Math.max(sx*0.84,sx*1.16), 0.75, 1.15, 0.44, 0.50, 'iron', -0.2);   // treads
      boxAt(out, Math.min(sx*0.86,sx*1.16), Math.max(sx*0.86,sx*1.16), 0.75, 1.15, 0.80, 0.86, 'iron', -0.15);
      bar(out, [sx*1.00,0.95,0.50],[sx*1.00,0.95,0.80],0.022,'iron',-0.4);
    }
    boxAt(out, -1.12,-0.82, -0.70, -0.10, 0.44, 0.86, 'rubber', -0.25);        // battery box, street
    boxAt(out, -1.24,1.24, G.bumpF[0], G.bumpF[1], 0.32, 0.62, 'chrome', 0.05);// CHROME bumper (stays put)
    for(const sx of [-1,1]) bar(out,[sx*0.30,4.44,0.44],[sx*0.30,4.52,0.44],0.035,'iron',-0.2);  // tow pins
    for(const sx of [-1,1])                                                    // frame-end lamp bar
      boxAt(out, Math.min(sx*0.30,sx*0.46), Math.max(sx*0.30,sx*0.46), -3.35, -3.30, 0.75, 0.88, 'lensR', 0.25);
    if(s.mudflaps) for(const sx of [-1,1]){
      wallY(out, -3.20, Math.min(sx*0.52,sx*1.08), Math.max(sx*0.52,sx*1.08), 0.06, 0.50, 'rubber', -0.4, -1);
      wallY(out, -3.195, Math.min(sx*0.52,sx*1.08), Math.max(sx*0.52,sx*1.08), 0.06, 0.50, 'rubber', -0.8, +1);
    }
    // deck plates (catwalk) + glad hands
    slab(out, [[-0.55,-1.85],[0.55,-1.85],[0.55,-0.82],[-0.55,-0.82]], 1.10, 'galv', -0.1, gridTex());
    for(const sx of [-1,1]) bar(out,[sx*0.22,-0.80,1.98],[sx*0.30,-1.45,1.30],0.020,'rubber',-0.35);
    boxAt(out, -0.30,0.30, -0.82,-0.78, 1.88, 2.06, 'iron', -0.2);
    // fifth wheel: pedestal, plate with aft slot, approach ramps, release handle
    boxAt(out, -0.35,0.35, -2.45,-2.15, 0.95, 1.13, 'iron', -0.3);
    slab(out, [[-G.fw.hw,G.fw.plate[0]],[G.fw.hw,G.fw.plate[0]],[G.fw.hw,G.fw.plate[1]],[-G.fw.hw,G.fw.plate[1]]], G.fw.topZ, 'galv', 0.15);
    slab(out, [[-0.06,G.fw.plate[0]-0.001],[0.06,G.fw.plate[0]-0.001],[0.06,G.fw.y],[-0.06,G.fw.y]], G.fw.topZ+0.004, 'shade', -0.6);
    wallY(out, G.fw.plate[1], -G.fw.hw, G.fw.hw, 1.06, G.fw.topZ, 'galv', -0.2, +1);
    for(const sx of [-1,1])
      quad(out, sx>0?[G.fw.hw,G.fw.plate[0],G.fw.topZ]:[-0.06,G.fw.plate[0],G.fw.topZ],
                sx>0?[0.06,G.fw.plate[0],G.fw.topZ]:[-G.fw.hw,G.fw.plate[0],G.fw.topZ],
                sx>0?[0.06,G.fw.rampTo.y,G.fw.rampTo.z]:[-G.fw.hw,G.fw.rampTo.y,G.fw.rampTo.z],
                sx>0?[G.fw.hw,G.fw.rampTo.y,G.fw.rampTo.z]:[-0.06,G.fw.rampTo.y,G.fw.rampTo.z], 'galv', -0.05);
    bar(out, [-0.45,-2.30,1.10],[-0.78,-2.30,1.08],0.022,'iron',-0.25);
    // TWIN CHROME STACKS on cab-back brackets — the tier's cue
    for(const sx of [-1,1]){
      tube(out,[sx*G.stacks.x,G.stacks.y,G.stacks.z0],[sx*G.stacks.x,G.stacks.y,G.stacks.z1],G.stacks.r,10,'chrome',0.30,true);
      tube(out,[sx*G.stacks.x,G.stacks.y,G.stacks.shield[0]],[sx*G.stacks.x,G.stacks.y,G.stacks.shield[1]],G.stacks.r+0.024,10,'galv',-0.15,false);
      bar(out,[sx*1.10,G.cabBackY-0.02,1.60],[sx*G.stacks.x,G.stacks.y,1.60],0.020,'iron',-0.3);
      bar(out,[sx*1.10,G.cabBackY-0.02,2.60],[sx*G.stacks.x,G.stacks.y,2.60],0.020,'iron',-0.3);
      bar(out,[sx*0.44,-0.95,0.70],[sx*G.stacks.x,G.stacks.y,G.stacks.z0+0.02],0.055,'iron',-0.35);  // elbow up from the frame
    }
    // engine bay (bared by the tilted clip)
    wallY(out, 1.97, -0.84, 0.84, 0.60, 1.70, 'shade', -0.8, +1);              // firewall
    wallX(out, 0.78, 1.99, 4.24, 0.62, 1.42, 'shade', -0.8, -1);
    wallX(out,-0.78, 1.99, 4.24, 0.62, 1.42, 'shade', -0.8, +1);
    boxAt(out, -0.64,0.64, 4.24, 4.30, 0.55, 1.40, 'shade', -0.6);             // radiator wall
    boxAt(out, -0.38,0.38, 2.40, 3.80, 0.55, 1.28, 'iron', -0.3);              // the big block
    boxAt(out, -0.30,0.30, 2.50, 3.70, 1.28, 1.40, 'galv', -0.1);              // valve cover
    tube(out,[0.58,2.65,1.15],[0.58,3.15,1.15],0.16,10,'rubber',-0.2);         // air cleaner, curb
    for(const sx of [-1,1]) boxAt(out, Math.min(sx*0.86,sx*0.94), Math.max(sx*0.86,sx*0.94), 1.97, 2.03, 1.60, 1.68, 'trim', 0.3);  // hood latches
  }

  // ---- the front clip: level square hood, pontoon fenders, chrome grille, lamp pods ----
  function buildHoodClip(out,s){
    const a = -s.hood*G.hoodDeg*DEG, ca=Math.cos(a), sa=Math.sin(a);
    part(out,(T)=>{
      const wear=wearTex(s.weather);
      const y0=G.hoodY0, y1=G.hoodY1, zc=G.hoodZc, zn=G.hoodZn;
      quad(T, [0,y0,zc+0.02],[G.hwHood,y0,zc-0.02],[G.hwHood,y1,zn-0.02],[0,y1,zn+0.02], 'paint', 0.34, 0,
        [[0,y0],[G.hwHood,y0],[G.hwHood,y1],[0,y1]], wear);
      quad(T, [-G.hwHood,y0,zc-0.02],[0,y0,zc+0.02],[0,y1,zn+0.02],[-G.hwHood,y1,zn-0.02], 'paint', 0.34, 0,
        [[-G.hwHood,y0],[0,y0],[0,y1],[-G.hwHood,y1]], wear);
      quad(T, [G.hwHood,y0,zc-0.03],[0,y0,zc+0.01],[0,y1,zn+0.01],[G.hwHood,y1,zn-0.03], 'leaf', -0.9);
      quad(T, [0,y0,zc+0.01],[-G.hwHood,y0,zc-0.03],[-G.hwHood,y1,zn-0.03],[0,y1,zn+0.01], 'leaf', -0.9);
      // hood sides: tall vertical cheeks down to the frame line — the square-hood look
      for(const sx of [-1,1]){
        wallX(T, sx*G.hwHood, y0, y1, 0.90, (sx,zc-0.03,zn-0.03, true)?zc-0.03:zc, 'paint', sx>0?0.16:-0.44, sx, null, wear);
        // pontoon fender: outer arch panel + flat top ledge, separate from the hood cheek
        archPanel(T, sx*G.hwFender, G.archFy[0], G.archFy[1], 0.48, 1.36, 'paint', sx>0?0.18:-0.42, sx,
          [{yc:G.axF, r:G.archF.r, zc:G.archF.zc}], wear);
        quad(T, sx>0?[G.hwHood,G.archFy[0],G.fenderTopZ]:[-G.hwFender,G.archFy[0],G.fenderTopZ],
                sx>0?[G.hwFender,G.archFy[0],G.fenderTopZ]:[-G.hwHood,G.archFy[0],G.fenderTopZ],
                sx>0?[G.hwFender,G.archFy[1],G.fenderTopZ]:[-G.hwHood,G.archFy[1],G.fenderTopZ],
                sx>0?[G.hwHood,G.archFy[1],G.fenderTopZ]:[-G.hwFender,G.archFy[1],G.fenderTopZ], 'paint', 0.30, 0, null, wear);
        wallX(T, sx*1.02, G.axF-G.archF.r, G.axF+G.archF.r, 1.00, 1.20, 'shade', -0.85, sx);
        // headlamp pods on the fender tops
        boxAt(T, Math.min(sx*0.98,sx*1.14), Math.max(sx*0.98,sx*1.14), 3.95, 4.15, G.fenderTopZ, 1.56, 'chrome', 0.1);
        wallY(T, 4.15, Math.min(sx*0.99,sx*1.13), Math.max(sx*1.13,sx*0.99), 1.42, 1.54, s.night?'glow':'head', 0.35, +1);
      }
      // nose: chrome surround + tall VERTICAL-bar grille
      wallY(T, y1, -G.hwHood, G.hwHood, 0.62, 0.74, 'chrome', 0.1, +1);
      T.push(F([[0.56,y1+0.03,0.74],[-0.56,y1+0.03,0.74],[-0.56,y1+0.03,zn-0.06],[0.56,y1+0.03,zn-0.06]],'grille',-0.1,0,
        [[0.56,0.74],[-0.56,0.74],[-0.56,zn-0.06],[0.56,zn-0.06]], grilleTex()));
      for(const sx of [-1,1]) wallY(T, y1, Math.min(sx*0.56,sx*G.hwHood), Math.max(sx*0.56,sx*G.hwHood), 0.74, zn-0.02, 'chrome', 0.05, +1);
      wallY(T, y1, -0.56, 0.56, zn-0.06, zn-0.02, 'chrome', 0.05, +1);
    }, s.hood>0 ? (p)=>rotX(p, G.hoodHinge.y, G.hoodHinge.z, ca, sa) : null);
  }

  // ---- cab + flat-top sleeper: static — shell, glass, doors, mirrors, visor, interior ----
  function buildDoors(T,s){
    const wear=wearTex(s.weather);
    for(const d of [ {sx:+1, pose:s.dR}, {sx:-1, pose:s.dL} ]){
      const sx=d.sx, y0=G.doorY[0], y1=G.doorY[1];
      const a=sx*d.pose*65*DEG, ca=Math.cos(a), sa=Math.sin(a);
      wallY(T, y1, Math.min(sx*1.12,sx*1.22), Math.max(sx*1.12,sx*1.22), G.doorZ0+0.02, G.doorHead, 'leaf', -0.5, -1);
      wallY(T, y0, Math.min(sx*1.12,sx*1.22), Math.max(sx*1.12,sx*1.22), G.doorZ0+0.02, G.doorHead, 'leaf', -0.6, +1);
      slab(T, sx>0?[[1.12,y0],[1.22,y0],[1.22,y1],[1.12,y1]]:[[-1.22,y0],[-1.12,y0],[-1.12,y1],[-1.22,y1]], G.doorZ0, 'leaf', -0.55);
      part(T,(D)=>{
        texWallX(D, sx*G.hwCab, y0, y1, G.doorZ0, 1.66, 'paint', sx>0?0.18:-0.42, sx, wear);
        wallX(D, sx*G.hwCab, y0, y1, 2.38, G.doorHead, 'paint', sx>0?0.10:-0.48, sx);
        wallX(D, sx*1.232, y0+0.06, y1-0.06, 1.66, 2.38, 'glass', sx>0?-0.15:-0.55, sx);
        wallX(D, sx*1.14, y0+0.02, y1-0.02, G.doorZ0+0.04, G.doorHead-0.04, 'leaf', -0.7, -sx);
        boxAt(D, Math.min(sx*1.23,sx*1.26), Math.max(sx*1.23,sx*1.26), y0+0.10, y0+0.30, 1.46, 1.52, 'trim', 0.3);
      }, (p)=>hingeZ(p, sx*1.18, y1, ca, sa));
    }
  }
  function buildCab(out,s){
    const wear=wearTex(s.weather);
    // upright two-piece windshield + centre post
    quad(out, [1.04,G.wsB.y,G.wsB.z],[0.05,G.wsB.y,G.wsB.z],[0.05,G.wsT.y,G.wsT.z],[0.96,G.wsT.y,G.wsT.z], 'glass', -0.18);
    quad(out, [-0.05,G.wsB.y,G.wsB.z],[-1.04,G.wsB.y,G.wsB.z],[-0.96,G.wsT.y,G.wsT.z],[-0.05,G.wsT.y,G.wsT.z], 'glass', -0.18);
    quad(out, [0.05,G.wsB.y,G.wsB.z],[-0.05,G.wsB.y,G.wsB.z],[-0.05,G.wsT.y,G.wsT.z],[0.05,G.wsT.y,G.wsT.z], 'paint', 0.05);
    quad(out, [1.18,G.wsB.y,G.wsB.z],[1.04,G.wsB.y,G.wsB.z],[0.96,G.wsT.y,G.wsT.z],[1.08,G.wsT.y,G.wsT.z], 'paint', 0.10);
    quad(out, [-1.04,G.wsB.y,G.wsB.z],[-1.18,G.wsB.y,G.wsB.z],[-1.08,G.wsT.y,G.wsT.z],[-0.96,G.wsT.y,G.wsT.z], 'paint', 0.10);
    quad(out, [1.08,G.wsT.y,G.wsT.z],[-1.08,G.wsT.y,G.wsT.z],[-G.hwCabRoof,1.66,2.90],[G.hwCabRoof,1.66,2.90], 'paint', 0.28);
    slab(out, [[-1.18,1.93],[1.18,1.93],[1.18,2.01],[-1.18,2.01]], 1.76, 'paint', 0.12);        // cowl strip
    wallY(out, G.cowlY, -1.22, 1.22, 1.00, 1.76, 'paint', 0.08, +1);                             // cowl face
    boxAt(out, -G.hwCabRoof, G.hwCabRoof, G.cabBackY, 1.66, 2.86, G.cabRoofZ, 'paint', 0.06, false, wear);
    for(const mx of [-0.56,-0.28,0,0.28,0.56])                                                   // five roof markers
      boxAt(out, mx-0.045, mx+0.045, 1.54, 1.62, G.cabRoofZ, G.cabRoofZ+0.06, 'lensA', 0.3);
    if(s.visor)                                                                                  // drop visor
      quad(out, [G.visor.hw,G.visor.y0,G.visor.zBack],[-G.visor.hw,G.visor.y0,G.visor.zBack],
                [-G.visor.hw,G.visor.y1,G.visor.zFront],[G.visor.hw,G.visor.y1,G.visor.zFront], 'paint', 0.26);
    for(const sx of [-1,1]){
      wallX(out, sx*G.hwCab, G.cabBackY, G.cowlY, 0.62, G.doorZ0, 'paint', sx>0?0.16:-0.44, sx, null, wear);
      wallX(out, sx*G.hwCab, G.doorY[1], G.cowlY, G.doorZ0, G.doorHead, 'paint', sx>0?0.16:-0.44, sx, null, wear);
      wallX(out, sx*G.hwCab, G.cabBackY, G.doorY[0], G.doorZ0, G.doorHead, 'paint', sx>0?0.16:-0.44, sx, null, wear);
      if(sx>0) quad(out, [1.22,G.cabBackY,G.doorHead],[1.22,1.95,G.doorHead],[1.10,1.95,2.88],[1.10,G.cabBackY,2.88], 'paint', 0.12);
      else     quad(out, [-1.22,1.95,G.doorHead],[-1.22,G.cabBackY,G.doorHead],[-1.10,G.cabBackY,2.88],[-1.10,1.95,2.88], 'paint', -0.46);
      wallX(out, sx*1.232, -0.55, -0.05, 1.75, 2.20, 'glass', sx>0?-0.12:-0.50, sx);             // sleeper window
      if(s.mirrors){                                                                             // west-coast bars
        bar(out,[sx*1.20,1.62,2.40],[sx*1.44,1.70,2.36],0.018,'iron',-0.1);
        bar(out,[sx*1.20,1.62,1.70],[sx*1.44,1.70,1.74],0.018,'iron',-0.15);
        bar(out,[sx*1.44,1.70,1.70],[sx*1.44,1.70,2.38],0.018,'iron',-0.12);
        boxAt(out, Math.min(sx*1.40,sx*1.48), Math.max(sx*1.40,sx*1.48), 1.62, 1.72, 1.76, 2.32, 'dash', -0.05);
        wallY(out, 1.62, Math.min(sx*1.41,sx*1.47), Math.max(sx*1.41,sx*1.47), 1.80, 2.28, 'glass', -0.25, -1);
      }
    }
    wallY(out, G.cabBackY, -1.22, 1.22, 0.95, 2.88, 'paint', -0.30, -1, null, wear);             // back wall
    // interior: high flat floor, dash, wheel, two buckets, bunk
    slab(out, [[-1.14,-0.74],[1.14,-0.74],[1.14,1.93],[-1.14,1.93]], G.cabFloorZ, 'rubber', -0.35, ribTex());
    boxAt(out, -1.10,1.10, 1.72, 1.92, 1.40, 1.62, 'dash', -0.4);
    tube(out, [-0.62,1.66,1.50],[-0.62,1.58,1.57], 0.18, 10, 'dash', -0.2);
    bar(out, [-0.62,1.70,1.32],[-0.62,1.60,1.50], 0.024, 'iron', -0.4);
    for(const sx of [-1,1]){
      boxAt(out, Math.min(sx*0.36,sx*0.88), Math.max(sx*0.36,sx*0.88), 0.98, 1.44, 1.36, 1.52, 'cloth', -0.45);
      boxAt(out, Math.min(sx*0.36,sx*0.88), Math.max(sx*0.36,sx*0.88), 0.86, 1.00, 1.52, 2.06, 'cloth', -0.55);
    }
    boxAt(out, -0.92,0.92, -0.68, 0.50, 1.30, 1.48, 'cloth', -0.5);            // bunk
    slab(out, [[-1.06,-0.70],[1.06,-0.70],[1.06,1.62],[-1.06,1.62]], 2.78, 'shade', -1.0);      // headliner
    buildDoors(out,s);
  }

  // ---- wheels & axles: steered singles up front, tandem duals aft, 10-lug hubs ----
  function wheelAt(out, xc, yc, sxOut, roll, yawDeg){
    if(yawDeg){ const a=yawDeg*DEG, ca=Math.cos(a), sa=Math.sin(a);
      part(out,(T)=>wheelAt(T,xc,yc,sxOut,roll,0),(p)=>hingeZ(p,xc,yc,ca,sa)); return; }
    const r=G.wheelR, w=G.tireW, ph=roll*2*Math.PI;
    tube(out,[xc-w/2,yc,r],[xc+w/2,yc,r], r, 14, 'rubber', -0.05, true, treadTex(roll*2*Math.PI*r));
    const xf=xc+sxOut*(w/2+0.012);
    tube(out,[xc+sxOut*(w/2-0.02),yc,r],[xf,yc,r], r*0.56, 12, 'alloy', 0.25);
    for(let k=0;k<10;k++){ const th=ph+k*Math.PI/5, py=yc+Math.cos(th)*0.175, pz=r+Math.sin(th)*0.175;
      tube(out,[xf-sxOut*0.005,py,pz],[xf+sxOut*0.028,py,pz],0.026,6,'galv',0.35); }
    for(let k=0;k<4;k++){ const th=ph+Math.PI/8+k*Math.PI/2, py=yc+Math.cos(th)*0.30, pz=r+Math.sin(th)*0.30;
      tube(out,[xf,py,pz],[xf+sxOut*0.012,py,pz],0.050,6,'rubber',-0.6); }
    { const th=ph+Math.PI/3, py=yc+Math.cos(th)*0.345, pz=r+Math.sin(th)*0.345;
      tube(out,[xf,py,pz],[xf+sxOut*0.02,py,pz],0.022,5,'iron',-0.4); }
    tube(out,[xf,yc,r],[xf+sxOut*0.05,yc,r],0.070,8,'chrome',0.4);
  }
  function dualAt(out, sxOut, yc, roll){
    const r=G.wheelR, w=G.tireW, ci=sxOut*G.dualXi;
    tube(out,[ci-w/2,yc,r],[ci+w/2,yc,r], r, 14, 'rubber', -0.20, true, treadTex(roll*2*Math.PI*r));
    wheelAt(out, sxOut*G.dualXo, yc, sxOut, roll, 0);
  }
  function buildWheels(out,s){
    const st=steerAngles(s.steer);
    tube(out,[-0.70,G.axF,G.wheelR],[0.70,G.axF,G.wheelR],0.052,8,'iron',-0.25);
    for(const ay of [G.tandA,G.tandB]){
      tube(out,[-0.88,ay,G.wheelR],[0.88,ay,G.wheelR],0.068,8,'iron',-0.25);
      tube(out,[-0.03,ay,G.wheelR],[0.30,ay,G.wheelR],0.17,10,'iron',-0.2);
    }
    bar(out,[0.12,1.20,0.50],[0.12,G.tandA+0.35,0.48],0.052,'iron',-0.3);
    bar(out,[0.12,G.tandA-0.35,0.48],[0.12,G.tandB+0.35,0.48],0.048,'iron',-0.3);
    wheelAt(out,  G.frontWX, G.axF, +1, s.roll+s.wFR, st.R);
    wheelAt(out, -G.frontWX, G.axF, -1, s.roll+s.wFL, st.L);
    for(const ay of [G.tandA,G.tandB]){
      dualAt(out, +1, ay, s.roll+s.wRR);
      dualAt(out, -1, ay, s.roll+s.wRL);
    }
  }

  function build(s){
    const body=[], rolling=[];
    buildFrame(body,s); buildHoodClip(body,s); buildCab(body,s);
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
      chrome:{ ramp:t(CHROME), polish:0.3 }, alloy:{ ramp:t(CHROME.map(c=>desat(c,0.15))) },
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
  function frames(dir, n, opts, cue){ n=n||8; const fn=CUES[cue||'doors']||CUES.doors, out=[];
    const cyclic = (cue==='roll'||cue==='bounce'||cue==='turn');
    for(let i=0;i<n;i++){ const t = cyclic ? i/n : i/(n-1);
      out.push(render(dir, Object.assign({}, opts, fn(t)))); }
    return out;
  }
  function project(dir, p, elev, yaw){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev,yaw})); return {x:v.sx,y:v.sy}; }
  function anchors(dir, opts){ opts=opts||{}; const s=resolve(opts), e=opts.elev;
    const P=(p)=>{ const q=project(dir,p,e,s.yaw); return { x:q.x, y:q.y, m:p }; };
    return {
      fifthWheel:P([0,G.fw.y,G.fw.topZ]), deck:P([0,-1.30,1.10]), gladHands:P([0,-0.77,1.98]),
      hoodLatch:P([0,2.00,1.64]),
      doorL:P([-G.hwCab,0.95,1.70]), doorR:P([G.hwCab,0.95,1.70]),
      roof:P([0,0.45,G.cabRoofZ]),
      stackL:P([-G.stacks.x,G.stacks.y,G.stacks.z1]), stackR:P([G.stacks.x,G.stacks.y,G.stacks.z1]),
      wheelFL:P([-G.frontWX,G.axF,G.wheelR]), wheelFR:P([G.frontWX,G.axF,G.wheelR]),
      wheelML:P([-G.dualXo,G.tandA,G.wheelR]), wheelMR:P([G.dualXo,G.tandA,G.wheelR]),
      wheelRL:P([-G.dualXo,G.tandB,G.wheelR]), wheelRR:P([G.dualXo,G.tandB,G.wheelR]),
      bodyL:s.B.bodyL, loa:s.B.loa, width:s.B.width, height:s.B.height, wheelbase:s.B.wheelbase,
    };
  }
  function list(){ return Object.keys(BODIES); }

  root.ClassicSemiIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    BODY, TRIM, IRON, GALV, RUBBER, CHROME, CLOTH, GLASSD, GLASSN, KEY,
    BODIES, PRESETS, CUES, G, travel:{F:TF,R:TR},
    steer:{ maxInnerDeg:STEER_MAX, maxOuterDeg:+(steerAngles(1).R.toFixed(2)), angles:steerAngles },
    list, dims, resolve, render, frames, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
