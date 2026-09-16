/* FINAL FIT PASS — shared cab/door/windshield envelope. See FINAL-PASS.md. */
/* PASS 2 — sculpted body assemblies and mechanical finish. See PASS-2.md. */
/* REVISED COPY — 2026-09-13. See ART-CHANGES.md. Original inputs preserved separately. */
/* Hidden Harbours — parametric ISO ROAD-VEHICLE rig, VAN body (same turntable + camera + shading
   as vehicleIsoRig.js / camperIsoRig.js / the fleet). Body: HIGHTOP VAN — a Euro-style forward-cab
   cargo van. TWO variants live in the one body: roof 'high'|'low', windows false (panel) | true
   (passenger glass + benches, no bulkhead). 45deg steps, elev 40deg, flat-facet shading from the
   fixed upper-LEFT key, z-buffered, ordered dither, depth-edge darkening, NO AA, 32 px = 1 m,
   ringless from birth (ADR 0031).

   ARTICULATION — pose params on render(dir,opts), 0..1 unless noted:
     dFL dFR           front doors, hinged on their FORWARD edge, 0 -> 62deg
     slide             curb-side sliding door: pops out 0.085 m then runs 1.16 m AFT on its track
     barnL barnR       rear barn doors, hinged at their OUTER edges, 0 -> 96deg (swing out + aft)
     hood              stub clamshell hood, 0 -> 42deg (engine bay modelled underneath)
     roll              master wheel roll, REVOLUTIONS (cyclic); wFL wFR wRL wRR per-wheel offsets
     susF susR         suspension travel per axle, -1..1 (the BODY moves; wheels stay down)
     steer             front pair yaw, Ackermann-split, -1..1; +1 is full LEFT lock. Inner 24deg —
                       the front arches bulge to 1.09 m half-width and the tire corner must stay
                       inside them (the van has no flares to hide behind).
     yaw               heading off the 45deg grid, DEGREES (-45..45), rebaked under the fixed key.
   Variants (not poses): roof:'high'|'low', windows:true|false.

   ORIGIN / PIVOT: ground-centre of the body footprint. +x curb side, +y nose, +z up.
   The slide door is CURB side (+x) — kerb loading, correctly for the class.

   Exposes globalThis.VanIso = { W,H,PX,DIRS,pivot,order,defaultElev, BODY,TRIM,IRON,GALV,RUBBER,
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
  const CHROME = ['#27343c','#465862','#788e97','#a6bac0','#d5e1df','#f2f4e9'];
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
  function grilleTex(){ const p=0.082; return (u,v)=>{ const f=((v%p)+p)%p; return f<0.034?2:0; }; }
  function ribTex(){ const p=0.15; return (u,v)=>{ const f=((u%p)+p)%p; return f<0.055?-1:0; }; }
  function treadTex(phase){const c=2*Math.PI*G.wheelR/28;return(u,v)=>{const f=(((u+phase)%c)+c)%c;return f<c*.42?-1:0;};}

  // ================= GEOMETRY — HIGHTOP VAN =================
  const TF=0.08, TR=0.10;                          // suspension travel per axle, metres
  const G = {
    noseY:2.90, bumpF:[2.88,3.00], tailY:-2.92, bumpR:[-3.04,-2.90],
    cowlY:1.72, axF:2.20, axR:-1.76, wheelR:0.36, tireW:0.24,
    frontWX:0.82, rearWX:0.82,
    hwSide:1.01, hwArch:1.09, hwRoof:0.90,
    doorZ0:0.52, beltZ:1.42, glassTop:2.22,
    roofHigh:2.72, roofLow:2.42,
    hoodZc:1.28, hoodZn:1.12, hwHood:0.86, hoodY1:2.86,
    floorZ:0.55, bulkY:0.58,
    doorF:[0.62,1.66], slideY:[-0.62,0.58], slideZ0:0.50,
    barnHW:0.92, barnZ0:0.50, barnDrop:0.42,
    archF:{r:0.50, zc:0.40}, archR:{r:0.52, zc:0.40},
    archFy:[1.66,2.76],
  };
  // near-vertical upper-side cant: half-width at height z (belt -> glass top)
  const gx=(z)=> G.hwSide - 0.055*(z-G.beltZ);

  const BODIES = {
    hightopVan: { key:'hightopVan', label:'Hightop Van', kind:'van',
      loa:+(G.bumpF[1]-G.bumpR[0]).toFixed(2), bodyL:+(G.noseY-G.tailY).toFixed(2),
      width:+(G.hwArch*2).toFixed(2), bodyW:+(G.hwSide*2).toFixed(2),
      height:+(G.roofHigh+0.04).toFixed(2), heightLow:+(G.roofLow+0.04).toFixed(2),
      wheelbase:+(G.axF-G.axR).toFixed(2),
      cargoLen:+( -(-2.82) + 0.56 ).toFixed(2), wheels:4 },
  };

  // ---- steering: front pair yaw about their own vertical axes, Ackermann-split ----
  const STEER_MAX = 24;                                   // inner wheel, degrees, at full lock
  function steerAngles(v){
    if(!v) return { L:0, R:0 };
    const inner = Math.abs(v)*STEER_MAX*DEG;
    const outer = Math.atan(1/(1/Math.tan(inner) + (G.frontWX*2)/(G.axF-G.axR)));
    const i=inner/DEG, o=outer/DEG;
    return v>0 ? { L:+i, R:+o } : { L:-o, R:-i };
  }

  const PRESETS = {
    showroom:  { paint:'white', weather:0.05 },
    courier:   { paint:'white', weather:0.45 },
    tradesman: { paint:'sage',  weather:0.38 },
    airporter: { paint:'blue',  weather:0.25, windows:true },
    beachBus:  { paint:'teal',  weather:0.30, windows:true, roof:'low' },
  };
  const CUES = {
    doors: (t)=>({ dFL:t, dFR:t }),
    slide: (t)=>({ slide:t }),
    barn:  (t)=>({ barnL:t, barnR:t }),
    hood:  (t)=>({ hood:t }),
    roll:  (t)=>({ roll:t }),                             // one revolution, cyclic
    steer: (t)=>({ steer:t*2-1 }),                        // full right lock -> full left lock
    turn:  (t)=>({ steer:Math.sin(t*Math.PI*2), yaw:Math.sin(t*Math.PI*2)*12, roll:t }),  // cyclic
    bounce:(t)=>({ susF:Math.sin(t*Math.PI*2)*0.7, susR:Math.sin(t*Math.PI*2+1.3)*0.7, roll:t }),
  };

  function resolve(opts){
    opts=opts||{};
    const g=(k,d)=> opts[k]!=null?opts[k]:d;
    const c01=(v)=>Math.max(0,Math.min(1,v)), c11=(v)=>Math.max(-1,Math.min(1,v));
    const roof = opts.roof==='low' ? 'low' : 'high';
    return {
      body:'hightopVan', B:BODIES.hightopVan,
      paint: opts.paint||'white', weather:g('weather',0.32),
      roof, roofZ: roof==='low'?G.roofLow:G.roofHigh, windows:!!opts.windows,
      hood:c01(g('hood',0)), slide:c01(g('slide',0)),
      dFL:c01(g('dFL',0)), dFR:c01(g('dFR',0)), barnL:c01(g('barnL',0)), barnR:c01(g('barnR',0)),
      roll:g('roll',0), wFL:g('wFL',0), wFR:g('wFR',0), wRL:g('wRL',0), wRR:g('wRR',0),
      susF:c11(g('susF',0)), susR:c11(g('susR',0)),
      steer:c11(g('steer',0)), yaw:Math.max(-45,Math.min(45,g('yaw',0))),
      mirrors:g('mirrors',true), mudflaps:g('mudflaps',true), hitch:g('hitch',true),
      night:!!opts.night, outline: opts.outline!=null?!!opts.outline:KEYLINE_DEFAULT };
  }
  function dims(opts){ const s=resolve(opts);
    return Object.assign({ travelF:TF, travelR:TR }, s.B,
      { roof:s.roof, windows:s.windows, height: s.roof==='low'?s.B.heightLow:s.B.height, roofZ:s.roofZ }); }

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

  // ---- frame, tank, bumpers, hitch ----
  function buildFrame(out,s){
    for(const sx of [-1,1]) boxAt(out, sx*0.46-0.04, sx*0.46+0.04, -2.98, 2.60, 0.32, 0.44, 'iron', -0.15);
    for(const y of [-2.60,-1.20,0.00,1.20,2.20]) boxAt(out,-0.46,0.46,y-0.045,y+0.045,0.32,0.40,'iron',-0.3);
    boxAt(out, -0.90,-0.50, -0.50, 0.50, 0.30, 0.50, 'galv', -0.2);          // fuel tank, street side
    p2Bumper(out,s,'van');
    boxAt(out, -1.02,1.02, G.bumpR[0], G.bumpR[1], 0.42, 0.58, 'galv', 0.0);    // rear step bumper
    if(s.hitch){ boxAt(out,-0.07,0.07,-3.12,-2.92,0.30,0.42,'iron',-0.1);
      tube(out,[0,-3.08,0.42],[0,-3.08,0.52],0.030,8,'chrome',0.4); }
    tube(out,[0.55,-2.62,0.22],[0.55,-3.00,0.21],0.042,8,'chrome',0.15);     // exhaust tip, curb aft
  }

  // ---- front clip: arches, stub hood, grille, lamps, engine bay ----
  function originalFront(out,s){
    const wear=wearTex(s.weather);
    const yN=G.noseY;
    for(const sx of [-1,1]){
      // bulged front arch panel (widest point of the van)
      archPanel(out, sx*G.hwArch, G.archFy[0], G.archFy[1], 0.46, 1.10, 'paint', sx>0?0.18:-0.42, sx,
        [{yc:G.axF, r:G.archF.r, zc:G.archF.zc}], wear);
      // arch cap strip back to the body plane
      slab(out, sx>0?[[G.hwSide,G.archFy[0]],[G.hwArch,G.archFy[0]],[G.hwArch,G.archFy[1]],[G.hwSide,G.archFy[1]]]
                    :[[-G.hwArch,G.archFy[0]],[-G.hwSide,G.archFy[0]],[-G.hwSide,G.archFy[1]],[-G.hwArch,G.archFy[1]]],
           1.10, 'paint', 0.26);
      // nose corner strip ahead of the bulge
      wallX(out, sx*1.03, G.archFy[1], yN, 0.46, 1.12, 'paint', sx>0?0.18:-0.42, sx, null, wear);
      // fender top: sloped filler between hood edge and body side
      if(sx>0) quad(out, [G.hwHood,1.72,1.26],[1.05,1.72,1.12],[1.05,G.hoodY1,1.06],[G.hwHood,G.hoodY1,1.10], 'paint', 0.30);
      else     quad(out, [-1.05,1.72,1.12],[-G.hwHood,1.72,1.26],[-G.hwHood,G.hoodY1,1.10],[-1.05,G.hoodY1,1.06], 'paint', 0.30);
      // arch liner (above tire tops only)
      wallX(out, sx*1.00, G.axF-G.archF.r, G.axF+G.archF.r, 0.78, 0.96, 'shade', -0.85, sx);
      // headlamps wrap the nose corners
      boxAt(out, Math.min(sx*0.66,sx*1.00), Math.max(sx*0.66,sx*1.00), yN-0.02, yN+0.03, 0.86, 1.10, s.night?'glow':'head', 0.35, false);
      wallY(out, yN, Math.min(sx*0.66,sx*1.00), Math.max(sx*0.66,sx*1.00), 0.80, 0.86, 'paint', -0.05, +1);
      wallY(out, yN, Math.min(sx*0.62,sx*0.66), Math.max(sx*0.62,sx*0.66), 0.80, 1.08, 'paint', -0.05, +1);
    }
    // nose face: grille + header
    out.push(F([[0.62,yN,0.80],[-0.62,yN,0.80],[-0.62,yN,1.08],[0.62,yN,1.08]],'grille',-0.1,0,
      [[0.62,0.80],[-0.62,0.80],[-0.62,1.08],[0.62,1.08]], grilleTex()));
    wallY(out, yN, -1.00, 1.00, 1.08, 1.12, 'paint', 0.05, +1);              // header lip under the hood edge
    // engine bay (seen when the hood is up)
    wallX(out, 0.80, 1.74, yN-0.06, 0.70, 1.20, 'shade', -0.8, -1);
    wallX(out,-0.80, 1.74, yN-0.06, 0.70, 1.20, 'shade', -0.8, +1);
    wallY(out, yN-0.08, -0.80, 0.80, 0.70, 1.06, 'shade', -0.8, -1);         // radiator wall
    wallY(out, 1.74, -0.80, 0.80, 0.70, 1.24, 'shade', -0.8, +1);            // firewall
    boxAt(out, -0.34,0.34, 1.95, 2.60, 0.75, 1.05, 'iron', -0.3);            // engine block
    boxAt(out, -0.74,-0.44, 2.35, 2.65, 0.85, 1.06, 'rubber', -0.2);         // battery
    // cowl slab between hood hinge and windshield base
    slab(out, [[-0.98,1.66],[0.98,1.66],[0.98,1.74],[-0.98,1.74]], 1.30, 'paint', 0.12);
  }

  function originalHood(out,s){
    const a=s.hood*42*DEG, ca=Math.cos(a), sa=Math.sin(a);
    part(out,(T)=>{
      const wear=wearTex(s.weather);
      const zc=G.hoodZc, zn=G.hoodZn, y0=1.74, y1=G.hoodY1;
      quad(T, [0,y0,zc+0.02],[G.hwHood,y0,zc-0.02],[G.hwHood,y1,zn-0.02],[0,y1,zn+0.02], 'paint', 0.34, 0,
        [[0,y0],[G.hwHood,y0],[G.hwHood,y1],[0,y1]], wear);
      quad(T, [-G.hwHood,y0,zc-0.02],[0,y0,zc+0.02],[0,y1,zn+0.02],[-G.hwHood,y1,zn-0.02], 'paint', 0.34, 0,
        [[-G.hwHood,y0],[0,y0],[0,y1],[-G.hwHood,y1]], wear);
      wallY(T, y1, -G.hwHood, G.hwHood, zn-0.06, zn, 'paint', 0.0, +1);      // front edge drop
      quad(T, [G.hwHood,y0,zc-0.03],[0,y0,zc+0.01],[0,y1,zn+0.01],[G.hwHood,y1,zn-0.03], 'leaf', -0.9);
      quad(T, [0,y0,zc+0.01],[-G.hwHood,y0,zc-0.03],[-G.hwHood,y1,zn-0.03],[0,y1,zn+0.01], 'leaf', -0.9);
    }, (p)=>rotX(p, 1.74, G.hoodZc, ca, sa));
  }

  // ---- greenhouse shell: windshield, roof, cants, rear header ----
  function buildCab(out,s){
    const wear=wearTex(s.weather);
    const roofZ=s.roofZ, yRoofF = s.roof==='low' ? 1.15 : 0.95, barnTop = roofZ - G.barnDrop;
    // windshield (one raked plane) + pillars + header
    const wb={y:1.70,z:1.32}, wt={y:1.34,z:2.22};
    const wsQ=(x0b,x1b,x0t,x1t,mat,b)=> quad(out,[x1b,wb.y,wb.z],[x0b,wb.y,wb.z],[x0t,wt.y,wt.z],[x1t,wt.y,wt.z],mat,b);
    wsQ(-0.82,0.82,-0.74,0.74,'glass',-0.18);
    wsQ(0.82,0.92,0.74,0.86,'paint',0.10); wsQ(-0.92,-0.82,-0.86,-0.74,'paint',0.10);
    wallY(out, wt.y-0.01, -0.86, 0.86, wt.z, wt.z+0.04, 'paint', 0.1, +1);
    // Crowned roof: continuous header rise, rolled shoulders and a quiet central panel.
    const stations=[[-2.86,roofZ-.035],[-2.55,roofZ],[-.35,roofZ],[.38,roofZ-.07],[.83,roofZ-.23],[1.15,2.40],[1.34,2.335]];
    const xs=[-1,-.91,-.72,0,.72,.91,1];
    const rows=stations.map(([y,z])=>xs.map(x=>[x*G.hwRoof,y,z-.075*Math.pow(Math.abs(x),4)]));
    artSurface(out,rows);
    for(let i=0;i<xs.length-1;i++){const a=rows[rows.length-1][i],b=rows[rows.length-1][i+1];out.push(F([[b[0],1.34,2.26],[a[0],1.34,2.26],a,b],'paint',.02));}
    for(const sx of [-1,1])for(let i=0;i<stations.length-1;i++){
      const [ya,za]=stations[i],[yb,zb]=stations[i+1];
      const pts=[[sx*gx(G.glassTop),ya,G.glassTop],[sx*G.hwRoof,ya,za-.075],[sx*G.hwRoof,yb,zb-.075],[sx*gx(G.glassTop),yb,G.glassTop]];
      if(sx>0)pts.reverse();out.push(F(pts,'paint',.02));
    }
    for(const sx of [-1,1])bar(out,[sx*G.hwRoof,-2.82,roofZ-.10],[sx*G.hwRoof,.35,roofZ-.10],.012,'paint',.25);
    // rear cap + header over the barn doors
    quad(out, [G.hwRoof,-2.86,roofZ],[-G.hwRoof,-2.86,roofZ],[-G.hwRoof,G.tailY,roofZ-0.10],[G.hwRoof,G.tailY,roofZ-0.10], 'paint', 0.20);
    wallY(out, G.tailY, -0.94, 0.94, barnTop, roofZ-0.10, 'paint', -0.30, -1, null, wear);
    // rear corner columns beside the barn opening + tail lamps
    for(const sx of [-1,1]){
      wallY(out, G.tailY, Math.min(sx*G.barnHW,sx*1.01), Math.max(sx*G.barnHW,sx*1.01), 0.50, barnTop, 'paint', -0.30, -1);
      boxAt(out, Math.min(sx*0.94,sx*1.00), Math.max(sx*0.94,sx*1.00), G.tailY-0.03, G.tailY, 0.66, 1.30, 'lensR', 0.25, false);
    }
    // interior liner + cargo shell (what open doors and windows reveal)
    for(const [ya,yb] of [[-2.84,G.slideY[0]],[G.slideY[1],G.doorF[0]]])wallX(out,.96,ya,yb,G.floorZ,G.glassTop,'shade',-.9,-1);
    wallX(out,-.96,-2.84,G.doorF[0],G.floorZ,G.glassTop,'shade',-.9,1);
    // Rear opening is bounded by its two moving barn leaves, not a fixed liner wall.
    const inner=[];artSurface(inner,rows.map(row=>row.map(p=>[p[0]*.97,p[1],p[2]-.10])),'shade',-.9);
    for(const f of inner){f.v.reverse();out.push(f);}
      for(const sx of [-1,1])fitSide(out,s,sx,true);
  }

  // ---- static side panels ----
  function p1Sides(out,s){
    const wear=wearTex(s.weather);
    const lower=[ {sx:-1, runs:[[-2.92,0.62]]}, {sx:+1, runs:[[-2.92,-0.62],[G.slideY[1],0.62]]} ];
    for(const side of lower) for(const run of side.runs){
      archPanel(out, side.sx*G.hwSide, run[0], run[1], 0.46, G.beltZ, 'paint', side.sx>0?0.18:-0.42, side.sx,
        [{yc:G.axR, r:G.archR.r, zc:G.archR.zc}], wear);
    }
    // upper shell strips (canted plane belt -> glassTop)
    const upper=[ {sx:-1, runs:[[-2.90,0.62]]}, {sx:+1, runs:[[-2.90,-0.62],[G.slideY[1],0.62]]} ];
    for(const side of upper) for(const run of side.runs){
      const sx=side.sx, y0=run[0], y1=run[1];
      quad(out, sx>0?[gx(G.beltZ),y0,G.beltZ]:[-gx(G.beltZ),y1,G.beltZ],
                sx>0?[gx(G.beltZ),y1,G.beltZ]:[-gx(G.beltZ),y0,G.beltZ],
                sx>0?[gx(G.glassTop),y1,G.glassTop]:[-gx(G.glassTop),y0,G.glassTop],
                sx>0?[gx(G.glassTop),y0,G.glassTop]:[-gx(G.glassTop),y1,G.glassTop], 'paint', sx>0?0.12:-0.46, 0, null, wear);
    }
    // passenger glass: proud panes set into the shell
    if(s.windows){
      const panes={ '-1':[[-2.55,-0.75],[-0.60,0.45]], '1':[[-2.55,-0.75]] };
      for(const k of ['-1','1']) for(const p of panes[k]){
        const sx=+k, z0=1.50, z1=2.00, xa=gx(z0)+0.012, xb=gx(z1)+0.012;
        quad(out, sx>0?[xa,p[0],z0]:[-xa,p[1],z0], sx>0?[xa,p[1],z0]:[-xa,p[0],z0],
                  sx>0?[xb,p[1],z1]:[-xb,p[0],z1], sx>0?[xb,p[0],z1]:[-xb,p[1],z1], 'glass', sx>0?-0.12:-0.50);
      }
    }
    if(!s.windows)for(const sx of [-1,1]){
      const ya=-2.52,yb=sx<0?.40:-.80;
      const q=[[sx*(gx(1.54)+.004),ya,1.54],[sx*(gx(1.54)+.004),yb,1.54],[sx*(gx(1.94)+.004),yb,1.94],[sx*(gx(1.94)+.004),ya,1.94]];
      if(sx<0)q.reverse();out.push(F(q,'paint',-.30,.004));
      bar(out,[sx*1.013,-2.53,1.04],[sx*1.013,-.82,1.04],.021,'rubber',-.1);
    }
    // slide-door track rail, curb side, aft of the opening
    boxAt(out, 1.005, 1.045, -2.35, -0.75, 1.30, 1.36, 'galv', -0.05);
    // rear arch liners + mudflaps
    for(const sx of [-1,1]){
      wallX(out, sx*0.98, G.axR-G.archR.r, G.axR+G.archR.r, 0.78, 0.94, 'shade', -0.85, sx);
      if(s.mudflaps){
        wallY(out, -2.36, Math.min(sx*0.70,sx*0.94), Math.max(sx*0.70,sx*0.94), 0.08, 0.44, 'rubber', -0.4, -1);
        wallY(out, -2.355, Math.min(sx*0.70,sx*0.94), Math.max(sx*0.70,sx*0.94), 0.08, 0.44, 'rubber', -0.8, +1);
      }
    }
  }

  // ---- interior ----
  function buildInterior(out,s){
    boxAt(out, -0.90,0.90, 1.40, 1.68, 0.98, 1.34, 'dash', -0.4);
    tube(out, [-0.48,1.38,1.18],[-0.48,1.30,1.24], 0.16, 10, 'dash', -0.2);
    bar(out, [-0.48,1.42,1.04],[-0.48,1.32,1.20], 0.024, 'iron', -0.4);
    for(const sx of [-1,1]){
      boxAt(out, Math.min(sx*.26,sx*.72), Math.max(sx*.26,sx*.72), 0.70, 1.20, 0.92, 1.10, 'cloth', -0.45);
      boxAt(out, Math.min(sx*.26,sx*.72), Math.max(sx*.26,sx*.72), 0.60, 0.74, 1.10, 1.56, 'cloth', -0.55);
    }
    slab(out, [[-0.94,-2.82],[0.94,-2.82],[0.94,0.56],[-0.94,0.56]], G.floorZ, 'rubber', -0.35, ribTex());
    for(const sx of [-1,1]) boxAt(out, Math.min(sx*0.62,sx*0.94), Math.max(sx*0.62,sx*0.94), G.axR-0.50, G.axR+0.50, G.floorZ, 0.82, 'rubber', -0.35);
    if(!s.windows){
      wallY(out, G.bulkY, -0.94, 0.94, G.floorZ, 2.02, 'shade', -0.7, +1);   // full bulkhead behind the seats
      wallY(out, G.bulkY-0.005, -0.94, 0.94, G.floorZ, 2.02, 'shade', -0.85, -1);
    } else {
      for(const ry of [-0.05,-1.05]){                                        // two bench rows
        boxAt(out, -0.80,0.80, ry-0.25, ry+0.25, 0.78, 0.96, 'cloth', -0.5);
        boxAt(out, -0.80,0.80, ry-0.39, ry-0.25, 0.96, 1.42, 'cloth', -0.6);
      }
    }
  }

  // ---- front doors (hinged on their forward edge; mirrors ride them) ----
  function buildDoorsF(out,s){for(const [sx,pose]of [[-1,s.dFL],[1,s.dFR]]){const a=sx*pose*62*DEG;part(out,T=>p2DoorLeaf(T,sx,G.doorF,G.hwSide,G.doorZ0,G.beltZ,G.glassTop,s,true),p=>hingeZ(p,sx*.98,G.doorF[1],Math.cos(a),Math.sin(a)));}}

  // ---- curb-side sliding door ----
  function buildSlide(out,s){
    const wear=wearTex(s.weather);
    const y0=G.slideY[0], y1=G.slideY[1];
    // jambs + sill + opening reveal
    wallY(out, y1, 0.90, 1.01, G.slideZ0+0.02, G.glassTop, 'leaf', -0.5, -1);
    wallY(out, y0, 0.90, 1.01, G.slideZ0+0.02, G.glassTop, 'leaf', -0.6, +1);
    slab(out, [[0.90,y0],[1.01,y0],[1.01,y1],[0.90,y1]], G.slideZ0, 'leaf', -0.55);
    const outT = 0.085*Math.min(1, s.slide*6), aft = 1.16*Math.max(0, (s.slide-0.10)/0.90);
    part(out,(T)=>{
      wallX(T, G.hwSide, y0, y1, G.slideZ0, G.beltZ, 'paint', 0.18, +1, [[y0,0],[y1,0],[y1,1],[y0,1]], wear);
      const gq=(zA,zB,mat,b,off)=>{ const xa=gx(zA)+(off||0), xb=gx(zB)+(off||0);
        quad(T, [xa,y0+0.03,zA],[xa,y1-0.03,zA],[xb,y1-0.03,zB],[xb,y0+0.03,zB], mat, b); };
      gq(G.beltZ, G.glassTop, 'paint', 0.10);
      if(s.windows) gq(1.50, 2.00, 'glass', -0.12, 0.012);
      wallX(T, 0.94, y0+0.02, y1-0.02, G.slideZ0+0.04, G.glassTop-0.04, 'leaf', -0.7, -1);
      boxAt(T, 1.025, 1.055, y0+0.10, y0+0.32, 1.16, 1.22, 'trim', 0.3);      // handle
      boxAt(T, 1.02, 1.05, y0-0.02, y0+0.10, 1.30, 1.36, 'galv', -0.05);      // roller arm on the track
    }, (p)=>[p[0]+outT, p[1]-aft, p[2]]);
  }

  // ---- rear barn doors (hinged at their outer edges, swing out + aft) ----
  function buildBarn(out,s){const top=s.roofZ-G.barnDrop;
    for(const [sx,pose]of [[-1,s.barnL],[1,s.barnR]]){const a=sx*pose*96*DEG;part(out,T=>{
      const x0=Math.min(sx*.008,sx*G.barnHW),x1=Math.max(sx*.008,sx*G.barnHW),y=G.tailY;
      boxAt(T,x0,x1,y,y+.05,G.barnZ0,top,'paint',-.06);
      for(const [za,zb]of [[.65,1.30],[1.45,top-.12]])if(zb>za+.12){
        const O=p2Rect(x0+.07,x1-.07,za,zb,.065),I=p2Rect(x0+.095,x1-.095,za+.025,zb-.025,.045);
        p2Ring(T,O.map(p=>[p[0],y-.005,p[1]]),I.map(p=>[p[0],y-.010,p[1]]),'p2Pressed',-.05,[0,-1,0]);
        p2Y(T,y-.011,I,s.windows&&za>1?'p2Glass':'paint',-.07,-1);
      }
      for(const z of [.73,1.72])boxAt(T,sx<0?x0:x1-.12,sx<0?x0+.12:x1,y-.022,y-.002,z,z+.10,'p2Bright',-.08);
      boxAt(T,Math.min(sx*.08,sx*.26),Math.max(sx*.08,sx*.26),y-.031,y-.009,1.30,1.36,'rubber',-.1);
    },p=>hingeZ(p,sx*.98,G.tailY,Math.cos(a),Math.sin(a)));}
  }

  // ---- wheels & axles ----
  function originalWheelAt(out, xc, yc, sxOut, roll, yawDeg){
    if(yawDeg){ const a=yawDeg*DEG, ca=Math.cos(a), sa=Math.sin(a);
      part(out,(T)=>wheelAt(T,xc,yc,sxOut,roll,0),(p)=>hingeZ(p,xc,yc,ca,sa)); return; }
    roll=((roll%1)+1)%1;
    const r=G.wheelR, w=G.tireW, ph=roll*2*Math.PI;
    tube(out,[xc-w/2,yc,r],[xc+w/2,yc,r], r, 14, 'rubber', -0.05, true, treadTex(roll*2*Math.PI*r));
    const xf=xc+sxOut*(w/2+0.012);
    tube(out,[xc+sxOut*(w/2-0.02),yc,r],[xf,yc,r], r*0.56, 12, 'alloy', 0.25);
    for(let k=0;k<6;k++){ const th=ph+k*Math.PI/3, py=yc+Math.cos(th)*0.125, pz=r+Math.sin(th)*0.125;
      tube(out,[xf-sxOut*0.005,py,pz],[xf+sxOut*0.026,py,pz],0.026,6,'galv',0.35); }
    for(let k=0;k<4;k++){ const th=ph+Math.PI/8+k*Math.PI/2, py=yc+Math.cos(th)*0.21, pz=r+Math.sin(th)*0.21;
      tube(out,[xf,py,pz],[xf+sxOut*0.012,py,pz],0.044,6,'rubber',-0.6); }
    { const th=ph+Math.PI/3, py=yc+Math.cos(th)*0.25, pz=r+Math.sin(th)*0.25;
      tube(out,[xf,py,pz],[xf+sxOut*0.02,py,pz],0.020,5,'iron',-0.4); }
    tube(out,[xf,yc,r],[xf+sxOut*0.04,yc,r],0.062,8,'chrome',0.4);
  }
  function buildWheels(out,s){
    const st=steerAngles(s.steer);
    tube(out,[-0.70,G.axF,G.wheelR],[0.70,G.axF,G.wheelR],0.045,8,'iron',-0.25);
    tube(out,[-0.76,G.axR,G.wheelR],[0.76,G.axR,G.wheelR],0.055,8,'iron',-0.25);
    tube(out,[-0.05,G.axR,G.wheelR],[0.23,G.axR,G.wheelR],0.13,10,'iron',-0.2);   // diff
    bar(out,[0.09,0.60,0.40],[0.09,G.axR+0.30,0.38],0.040,'iron',-0.3);           // driveshaft
    wheelAt(out,  G.frontWX, G.axF, +1, s.roll+s.wFR, st.R);
    wheelAt(out, -G.frontWX, G.axF, -1, s.roll+s.wFL, st.L);
    wheelAt(out,  G.rearWX,  G.axR, +1, s.roll+s.wRR, 0);
    wheelAt(out, -G.rearWX,  G.axR, -1, s.roll+s.wRL, 0);
  }

  /* JOB 1 STEP 3 — ramp fold, inside build() so the game bakes exactly what the sheets show.
     Faces that used the key ramp now use the value ramp. No faces, material names or vertices change. */
  const FOLD = { alloy:'chrome', p2Hood:'p2Pressed', p2Plate:'chrome', dash:'rubber', cloth:'iron' };
  function foldRamps(faces){ for(const f of faces){ const t=FOLD[f.mat]; if(t) f.mat=t; } return faces; }
  function build(s){ return foldRamps(artFinish(buildRaw(s))); }
  function buildRaw(s){
    const body=[], rolling=[];
    buildFrame(body,s); buildFront(body,s); buildHood(body,s);
    buildCab(body,s); buildSides(body,s); buildInterior(body,s);
    buildDoorsF(body,s); buildSlide(body,s); buildBarn(body,s);
    buildWheels(rolling,s);
    const dz=(y)=>{ const t=(y-G.axR)/(G.axF-G.axR); return -(s.susF*TF)*t - (s.susR*TR)*(1-t); };
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
            if(f.mat==='paint'||f.mat==='trim')fi=Math.round(fi)+(fi-Math.round(fi))*ART.bodyDither;
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
        if((m==='paint'||m==='galv'||m==='iron'||m==='rubber') && rnd()<0)
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
  function originalAnchors(dir, opts){ opts=opts||{}; const s=resolve(opts), e=opts.elev;
    const P=(p)=>{ const q=project(dir,p,e,s.yaw); return { x:q.x, y:q.y, m:p }; };
    return {
      hitch:P([0,-3.08,0.52]), barnL:P([-0.10,G.tailY,1.22]), barnR:P([0.10,G.tailY,1.22]),
      slide:P([G.hwSide,-0.02,1.20]), cargo:P([0,-1.15,G.floorZ]),
      hoodLatch:P([0,G.hoodY1-0.06,G.hoodZn]),
      doorFL:P([-G.hwSide,G.doorF[0]+0.18,1.25]), doorFR:P([G.hwSide,G.doorF[0]+0.18,1.25]),
      roof:P([0,-0.75,s.roofZ]),
      wheelFL:P([-G.frontWX,G.axF,G.wheelR]), wheelFR:P([G.frontWX,G.axF,G.wheelR]),
      wheelRL:P([-G.rearWX,G.axR,G.wheelR]), wheelRR:P([G.rearWX,G.axR,G.wheelR]),
      exhaust:P([0.55,-3.00,0.21]),
      bodyL:s.B.bodyL, loa:s.B.loa, width:s.B.width, height:(s.roof==='low'?s.B.heightLow:s.B.height), wheelbase:s.B.wheelbase,
    };
  }
  function list(){ return Object.keys(BODIES); }


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

  function artVanNose(p){const [x,y,z]=p,t=Math.max(0,Math.min(1,(y-1.74)/1.2));return[x*(1-.035*t*t),y-.13*t*t*Math.pow(Math.abs(x),3),z-.035*t*Math.pow(Math.abs(x),2)];}
  function buildFront(out,s){const T=[];originalFront(T,s);out.push(...T.filter(f=>!['paint','head','glow','grille'].includes(f.mat)));p2Front(out,s,'van',false);}
  function buildHood(out,s){part(out,T=>p2Bonnet(T,s,'van'),p=>rotX(p,1.74,G.hoodZc,Math.cos(s.hood*42*DEG),Math.sin(s.hood*42*DEG)));}

  function artDoorSkin(out,sx,y0,y1,x,z0,z1){
    const ya=y0+.09,yb=y1-.09,za=z0+.13,zb=z1-.10;
    wallX(out,sx*(x+.008),ya,yb,za,zb,'paint',-.17,sx);
    bar(out,[sx*(x+.012),ya,za],[sx*(x+.012),yb,za],.012,'paint',.25);
    bar(out,[sx*(x+.018),y0+.04,z1-.015],[sx*(x+.018),y1-.04,z1-.015],.015,'chrome',.1);
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
      tube(out,[sx*x,y+.023,z],[sx*x,y+.026,z],r*.69,12,night?'glow':'head',.2);
    }
    if(!classic){p2Box(out,sx,x0+.04,x1-.04,y+.024,y+.030,z1-.055,z1-.025,night?'glow':'head',.2);}
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
    const roof=G.boxRoofZ||(G.rf&&G.rf.roofZ),hw=G.hwBox||G.hw;
    if(roof&&hw)for(const f of faces)f.v=f.v.map(([x,y,z])=>[x,y,z-.065*Math.pow(Math.min(1,Math.abs(x)/hw),10)*Math.max(0,Math.min(1,(z-roof+.16)/.16))]);
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
  function pass2DoorLeaf(out,sx,ys,x,z0,belt,head,s,van=false){
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
      p2Y(out,y+.014,p2Rect(Math.min(sx*.70,sx*.86),Math.max(sx*.70,sx*.86),z0+.10,z1-.095,.017),s.night?'glow':'head',.12);
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

  function buildSides(out,s){p1Sides(out,s);
    for(const sx of [-1,1]){
      for(const [a,b]of (sx<0?[[-2.60,-.80],[-.59,.42]]:[[-2.60,-.80]]))if(!s.windows)p2ServicePanel(out,sx,gx(1.65)+.009,a,b,1.53,2.03);
      const x=sx*1.016;bar(out,[x,-2.57,1.06],[x,-.79,1.06],.026,'rubber',-.05);
    }
    // Subtle roof pressings stop the long top plane reading as an unformed lid.
    for(const x of [-.48,.48])bar(out,[x,-2.47,s.roofZ-.075*Math.pow(Math.abs(x)/.9,4)+.004],[x,-.43,s.roofZ-.075*Math.pow(Math.abs(x)/.9,4)+.004],.010,'p2Pressed',-.18);
  }


  // A single metric envelope defines the cab skin, door, jamb, and roof junction.
  const FIT={"belt":1.42,"rake":0.35,"front":1.72,"roofY":1.34,"roofZ":2.335,"drop":0.075,"xb":0.92,"xt":0.86,"hx":0.98,"wb":{"y":1.7,"z":1.32},"wt":{"y":1.34,"z":2.22},"hingeSetback":0.065};
  const fitClamp=t=>Math.max(0,Math.min(1,t));
  function fitLerp(a,b,t){return a+(b-a)*t;}
  function fitSample(z,rows){if(z<=rows[0][0])return rows[0][1];for(let i=1;i<rows.length;i++)if(z<=rows[i][0])return fitLerp(rows[i-1][1],rows[i][1],(z-rows[i-1][0])/(rows[i][0]-rows[i-1][0]));return rows[rows.length-1][1];}
  function fitFront(z){const b=FIT.wb||G.wsB,t=FIT.wt||G.wsT;return fitSample(z,[[G.doorZ0||.52,FIT.front],[b.z,b.y],[t.z,t.y],[FIT.roofZ-FIT.drop,FIT.roofY]]);}
  function fitWidth(y,z){
    const van=!!FIT.wb,b=FIT.wb||G.wsB,t=FIT.wt||G.wsT,hw=van?G.hwSide:G.hwCab,rw=van?G.hwRoof:G.hwCabRoof;
    const side=van?(z<=FIT.belt?hw:gx(z)):fitSample(z,[[FIT.belt,hw],[FIT.roofZ-FIT.drop,rw]]);
    const edge=fitSample(z,[[G.doorZ0||.52,hw],[b.z,FIT.xb],[t.z,FIT.xt],[FIT.roofZ-FIT.drop,rw]]);
    const a=fitClamp((y-fitFront(z)+.25)/.25),e=a*a*(3-2*a);return fitLerp(side,edge,e);
  }
  function fitPoint(sx,y,z,off=0){return[sx*(fitWidth(y,z)+off),y,z];}
  function fitDoorFront(z){const ys=G.doorY||G.doorF,head=G.doorHead||G.glassTop;return Math.min(ys[1]-FIT.hingeSetback-FIT.rake*fitClamp((z-FIT.belt)/(head-FIT.belt)),fitFront(z)-.065);}
  function fitDoorOutline(){const ys=G.doorY||G.doorF,z0=G.doorZ0,head=G.doorHead||G.glassTop;return[
    [ys[0]+.018,z0+.014],[fitDoorFront(z0+.014),z0+.014],[fitDoorFront(FIT.belt),FIT.belt],
    [fitDoorFront(head-.075),head-.075],[fitDoorFront(head-.014)-.035,head-.014],
    [ys[0]+.055,head-.014],[ys[0]+.018,head-.075],[ys[0]+.018,FIT.belt]];}
  function fitGlass(out,sx,poly){
    p2Face(out,poly.map(p=>fitPoint(sx,p[0],p[1],.002)),'rubber',-.22,[sx,0,0]);
    const center=poly.reduce((a,p)=>[a[0]+p[0]/poly.length,a[1]+p[1]/poly.length],[0,0]);
    const inner=poly.map(p=>[fitLerp(p[0],center[0],.065),fitLerp(p[1],center[1],.065)]);
    p2Face(out,inner.map(p=>fitPoint(sx,p[0],p[1],.006)),'p2Glass',-.1,[sx,0,0]);
    // Clip two diagonal reflection bands to the actual six-sided pane.
    function clip(poly,a,b,c){const out=[];for(let i=0;i<poly.length;i++){const p=poly[i],q=poly[(i+1)%poly.length],dp=a*p[0]+b*p[1]-c,dq=a*q[0]+b*q[1]-c;if(dp>=0)out.push(p);if((dp>=0)!==(dq>=0)){const t=dp/(dp-dq);out.push([fitLerp(p[0],q[0],t),fitLerp(p[1],q[1],t)]);}}return out;}
    const top=Math.max(...inner.map(p=>p[1]));for(const [offset,w,bias]of [[.17,.095,-.15],[.065,.028,.12]]){const c=top-offset+.18*center[0];let band=clip(inner,.18,1,c-w);band=clip(band,-.18,-1,-c);if(band.length>2)p2Face(out,band.map(p=>fitPoint(sx,p[0],p[1],.010)),'p2Reflection',bias,[sx,0,0]);}
    p2Face(out,inner.map(p=>fitPoint(sx,p[0],p[1],-.008)).reverse(),'p2Glass',-.25,[-sx,0,0]);
  }
  function fitMirror(out,sx,s){if(!s.mirrors)return;const start=out.length,ys=G.doorY||G.doorF,van=!!FIT.wb,classic=FIT.rake===.13,hw=van?G.hwSide:G.hwCab;
    const y=fitDoorFront(FIT.belt+.11)-.035,z=FIT.belt+.13,mx=hw+(van?.22:.25),my=y+.055;
    bar(out,fitPoint(sx,y-.055,z,.008),[sx*mx,my,z+.02],.025,'rubber',-.08);
    const z0=z-.09,z1=z+(classic?.42:.29),x0=mx-.04,x1=mx+.07;
    const outer=p2Rect(x0,x1,z0,z1,.035);p2Face(out,outer.map(p=>[sx*p[0],my+.085,p[1]]),'rubber',-.12,[0,1,0]);
    p2Face(out,outer.map(p=>[sx*p[0],my-.055,p[1]]),'rubber',-.12,[0,-1,0]);
    p2Ring(out,outer.map(p=>[sx*p[0],my+.085,p[1]]),outer.map(p=>[sx*p[0],my-.055,p[1]]),'rubber',-.04);
    p2Y(out,my-.058,p2Rect(Math.min(sx*(x0+.014),sx*(x1-.014)),Math.max(sx*(x0+.014),sx*(x1-.014)),z0+.02,z1-.025,.022),'p2Glass',.12,-1);
    p2Y(out,my-.060,p2Rect(Math.min(sx*(x0+.014),sx*(x1-.014)),Math.max(sx*(x0+.014),sx*(x1-.014)),z0+.023,z0+.08,.012),'p2Reflection',.08,-1);
    if(classic)bar(out,fitPoint(sx,y-.11,z1-.02,.007),[sx*mx,my,z1-.02],.017,'p2Bright',.1);
    for(let i=start;i<out.length;i++)out[i].fitPart='mirror';
  }
  function p2DoorLeaf(out,sx,ys,x,z0,belt,head,s,van=false){
    // Keep the pressed lower construction; fit every vertex to the shared envelope before hinging it.
    const lower=[];pass2DoorLeaf(lower,sx,ys,x,z0,belt,head,{...s,mirrors:false},van);
    for(const f of lower)if(f.v.every(p=>p[2]<=belt+.035)){
      f.v=f.v.map(([xx,y,z])=>{const ya=ys[0]+.018,yy=ya+(y-ya)/(ys[1]-ys[0]-.036)*(fitDoorFront(z)-ya);return[sx*(fitWidth(yy,z)+Math.abs(xx)-x),yy,z];});f.fitGroup='door';f.fitSide=sx;out.push(f);
    }
    const rear=ys[0]+.018,top=head-.014;
    const outer=[[rear,belt],[fitDoorFront(belt),belt],[fitDoorFront(head-.075),head-.075],[fitDoorFront(top)-.035,top],[rear+.037,top],[rear,head-.075]];
    const pane=[[rear+.065,belt+.064],[fitDoorFront(belt+.064)-.070,belt+.064],[fitDoorFront(head-.125)-.07,head-.125],[fitDoorFront(head-.085)-.087,head-.085],[rear+.105,head-.085],[rear+.065,head-.13]];
    const start=out.length;
    p2Ring(out,outer.map(p=>fitPoint(sx,p[0],p[1])),pane.map(p=>fitPoint(sx,p[0],p[1])),'paint',.04,[sx,0,0]);
    p2Ring(out,outer.map(p=>fitPoint(sx,p[0],p[1],-.034)),pane.map(p=>fitPoint(sx,p[0],p[1],-.034)),'p2Liner',-.22,[-sx,0,0]);
    p2Ring(out,outer.map(p=>fitPoint(sx,p[0],p[1])),outer.map(p=>fitPoint(sx,p[0],p[1],-.034)),'p2Pressed',-.08);
    p2Ring(out,pane.map(p=>fitPoint(sx,p[0],p[1])),pane.map(p=>fitPoint(sx,p[0],p[1],-.034)),'rubber',-.12);
    fitGlass(out,sx,pane);
    for(let i=1;i<outer.length-1;i++)bar(out,fitPoint(sx,outer[i][0],outer[i][1],.003),fitPoint(sx,outer[i+1][0],outer[i+1][1],.003),.008,'p2Pressed',-.1);
    fitMirror(out,sx,s);for(let i=start;i<out.length;i++){out[i].fitGroup='door';out[i].fitSide=sx;}
  }
  function fitSide(out,s,sx,van=false){
    const ys=G.doorY||G.doorF,z0=G.doorZ0,head=G.doorHead||G.glassTop,yr=van?ys[0]:G.cabBackY,roof=van?head:FIT.roofZ-FIT.drop;
    const levels=[z0,FIT.belt,(FIT.wb||G.wsB).z,head-.075,head-.014,head,(FIT.wt||G.wsT).z,roof].filter(z=>z>=z0&&z<=roof).sort((a,b)=>a-b);
    const start=out.length;
    function strip(a,b,rear){const ya=z=>rear?yr:fitDoorFront(z)+.012,yb=z=>rear?ys[0]+.006:fitFront(z);
      if(yb(a)-ya(a)<.00001&&yb(b)-ya(b)<.00001)return;
      for(let j=0;j<5;j++){const P=(z,t)=>fitPoint(sx,fitLerp(ya(z),yb(z),t),z);p2Face(out,[P(a,j/5),P(a,(j+1)/5),P(b,(j+1)/5),P(b,j/5)],'paint',.02,[sx,0,0]);}}
    for(let i=0;i<levels.length-1;i++){const a=levels[i],b=levels[i+1];if(b-a<1e-6)continue;
      if(a<head-.014){if(!van)strip(a,b,true);strip(a,b,false);}
      else for(let j=0;j<14;j++){const P=(z,t)=>fitPoint(sx,fitLerp(yr,fitFront(z),t),z);p2Face(out,[P(a,j/14),P(a,(j+1)/14),P(b,(j+1)/14),P(b,j/14)],'paint',.02,[sx,0,0]);}
    }
    // Solid jamb returns and a dark compression seal reveal a real opening with the leaf open.
    const outline=fitDoorOutline();p2Ring(out,outline.map(p=>fitPoint(sx,p[0],p[1],-.009)),outline.map(p=>fitPoint(sx,p[0],p[1],-.075)),'p2Liner',-.23);
    for(let i=0;i<outline.length;i++){const a=outline[i],b=outline[(i+1)%outline.length];bar(out,fitPoint(sx,a[0],a[1],-.014),fitPoint(sx,b[0],b[1],-.014),.011,'rubber',-.2);}
    for(const z of [z0+.2,FIT.belt-.09]){const y=fitDoorFront(z)+.018;p2Box(out,sx,fitWidth(y,z)-.025,fitWidth(y,z)+.007,y-.03,y+.03,z-.04,z+.04,'p2Bright',-.04);}
    for(let i=start;i<out.length;i++)out[i].fitGroup='cab-fit';
  }
  function fitHeader(out){const t=FIT.wt||G.wsT,hw=G.hwCabRoof||G.hwRoof;
    for(let i=0;i<12;i++){const u=-1+i/6,v=u+1/6,P=(a,top)=>top?[a*hw,FIT.roofY,FIT.roofZ-FIT.drop*Math.pow(Math.abs(a),FIT.drop<.04?5:4)]:[a*FIT.xt,t.y,t.z];p2Face(out,[P(u,false),P(v,false),P(v,true),P(u,true)],'paint',.10,[0,1,1]);}
  }
  function fitMounted(faces){for(const f of faces){
    if(f.fitGroup)continue;
    if(f.v.every(p=>Math.abs(p[1]-G.cabBackY)<1e-6))f.v=f.v.map(([x,y,z])=>[x/(G.hwCab||1)*fitWidth(y,z),y,z]);
    else if(f.v.every(p=>Math.abs(p[0])>G.hwCab-.003&&p[1]<G.doorY[0]&&p[2]>FIT.belt&&p[2]<G.doorHead+.001))f.v=f.v.map(([x,y,z])=>[Math.sign(x)*(fitWidth(y,z)+Math.abs(x)-G.hwCab),y,z]);
  }return faces;}
  const cabFit={front:fitFront,width:fitWidth,doorOutline:fitDoorOutline,profile:FIT,
    doorMesh:(sx=1,pose=0,opts={})=>{const out=[],ys=G.doorY||G.doorF,s=resolve(opts);p2DoorLeaf(out,sx,ys,G.hwCab||G.hwSide,G.doorZ0,FIT.belt,G.doorHead||G.glassTop,s,!!FIT.wb);const a=sx*pose*(FIT.wb?62:65)*DEG;for(const f of out)f.v=f.v.map(p=>hingeZ(p,sx*FIT.hx,ys[1],Math.cos(a),Math.sin(a)));return out;},
    skinMesh:()=>{const out=[];for(const sx of [-1,1])fitSide(out,resolve({}),sx,!!FIT.wb);return out;}};

  root.VanIso = { cabFit, W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    BODY, TRIM, IRON, GALV, RUBBER, CHROME, CLOTH, GLASSD, GLASSN, KEY,
    BODIES, PRESETS, CUES, G, travel:{F:TF,R:TR},
    steer:{ maxInnerDeg:STEER_MAX, maxOuterDeg:+(steerAngles(1).R.toFixed(2)), angles:steerAngles },
    mesh:(opts)=>build(resolve(opts||{})), list, dims, resolve, render, frames, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
