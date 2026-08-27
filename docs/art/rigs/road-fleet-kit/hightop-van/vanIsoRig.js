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
  function grilleTex(){ const p=0.082; return (u,v)=>{ const f=((v%p)+p)%p; return f<0.034?2:0; }; }
  function ribTex(){ const p=0.15; return (u,v)=>{ const f=((u%p)+p)%p; return f<0.055?-1:0; }; }
  function treadTex(phase){ const c=0.095; return (u,v)=>{ const f=(((u+phase)%c)+c)%c; return f<c*0.42?-1:0; }; }

  // ================= GEOMETRY — HIGHTOP VAN =================
  const TF=0.08, TR=0.10;                          // suspension travel per axle, metres
  const G = {
    noseY:2.90, bumpF:[2.88,3.00], tailY:-2.92, bumpR:[-3.04,-2.90],
    cowlY:1.72, axF:2.20, axR:-1.76, wheelR:0.36, tireW:0.24,
    frontWX:0.82, rearWX:0.82,
    hwSide:1.01, hwArch:1.09, hwRoof:0.90,
    doorZ0:0.52, beltZ:1.42, glassTop:2.06,
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
    boxAt(out, -1.04,1.04, G.bumpF[0], G.bumpF[1], 0.44, 0.80, 'dash', 0.02);   // front bumper (dark cladding)
    boxAt(out, -1.02,1.02, G.bumpR[0], G.bumpR[1], 0.42, 0.58, 'galv', 0.0);    // rear step bumper
    if(s.hitch){ boxAt(out,-0.07,0.07,-3.12,-2.92,0.30,0.42,'iron',-0.1);
      tube(out,[0,-3.08,0.42],[0,-3.08,0.52],0.030,8,'chrome',0.4); }
    tube(out,[0.55,-2.62,0.22],[0.55,-3.00,0.21],0.042,8,'chrome',0.15);     // exhaust tip, curb aft
  }

  // ---- front clip: arches, stub hood, grille, lamps, engine bay ----
  function buildFront(out,s){
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

  function buildHood(out,s){
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
    const wb={y:1.70,z:1.32}, wt={y:1.34,z:2.06};
    const wsQ=(x0b,x1b,x0t,x1t,mat,b)=> quad(out,[x1b,wb.y,wb.z],[x0b,wb.y,wb.z],[x0t,wt.y,wt.z],[x1t,wt.y,wt.z],mat,b);
    wsQ(-0.82,0.82,-0.74,0.74,'glass',-0.18);
    wsQ(0.82,0.92,0.74,0.86,'paint',0.10); wsQ(-0.92,-0.82,-0.86,-0.74,'paint',0.10);
    wallY(out, wt.y-0.01, -0.86, 0.86, wt.z, wt.z+0.04, 'paint', 0.1, +1);
    // front roof cap rising off the header — the hightop signature
    quad(out, [0.86,1.34,2.10],[-0.86,1.34,2.10],[-G.hwRoof,yRoofF,roofZ],[G.hwRoof,yRoofF,roofZ], 'paint', 0.28, 0, null, wear);
    // roof slab
    boxAt(out, -G.hwRoof, G.hwRoof, -2.86, yRoofF, roofZ-0.03, roofZ, 'paint', 0.06, false, wear);
    // side cants (full length, near-vertical tumblehome)
    for(const sx of [-1,1]){
      if(sx>0) quad(out, [gx(G.glassTop),-2.90,G.glassTop],[gx(G.glassTop),1.34,G.glassTop],[G.hwRoof,1.34,roofZ-0.02],[G.hwRoof,-2.90,roofZ-0.02], 'paint', 0.12, 0, null, wear);
      else     quad(out, [-gx(G.glassTop),1.34,G.glassTop],[-gx(G.glassTop),-2.90,G.glassTop],[-G.hwRoof,-2.90,roofZ-0.02],[-G.hwRoof,1.34,roofZ-0.02], 'paint', -0.46, 0, null, wear);
    }
    // rear cap + header over the barn doors
    quad(out, [G.hwRoof,-2.86,roofZ],[-G.hwRoof,-2.86,roofZ],[-G.hwRoof,G.tailY,roofZ-0.10],[G.hwRoof,G.tailY,roofZ-0.10], 'paint', 0.20);
    wallY(out, G.tailY, -0.94, 0.94, barnTop, roofZ-0.10, 'paint', -0.30, -1, null, wear);
    // rear corner columns beside the barn opening + tail lamps
    for(const sx of [-1,1]){
      wallY(out, G.tailY, Math.min(sx*G.barnHW,sx*1.01), Math.max(sx*G.barnHW,sx*1.01), 0.50, barnTop, 'paint', -0.30, -1);
      boxAt(out, Math.min(sx*0.94,sx*1.00), Math.max(sx*0.94,sx*1.00), G.tailY-0.03, G.tailY, 0.66, 1.30, 'lensR', 0.25, false);
    }
    // interior liner + cargo shell (what open doors and windows reveal)
    wallX(out, 0.96, -2.84, 1.66, G.floorZ, G.glassTop, 'shade', -0.9, -1);
    wallX(out,-0.96, -2.84, 1.66, G.floorZ, G.glassTop, 'shade', -0.9, +1);
    wallY(out, -2.84, -0.96, 0.96, G.floorZ, barnTop, 'shade', -0.9, +1);
    slab(out, [[-0.96,-2.84],[0.96,-2.84],[0.96,1.70],[-0.96,1.70]], roofZ-0.14, 'shade', -1.0);  // headliner
  }

  // ---- static side panels ----
  function buildSides(out,s){
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
      boxAt(out, sx*0.26, sx*0.72, 0.70, 1.20, 0.92, 1.10, 'cloth', -0.45);
      boxAt(out, sx*0.26, sx*0.72, 0.60, 0.74, 1.10, 1.56, 'cloth', -0.55);
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
  function buildDoorsF(out,s){
    const wear=wearTex(s.weather);
    for(const d of [ {side:+1, pose:s.dFR}, {side:-1, pose:s.dFL} ]){
      const sx=d.side, y0=G.doorF[0], y1=G.doorF[1];
      const a=sx*d.pose*62*DEG, ca=Math.cos(a), sa=Math.sin(a);
      wallY(out, y1, Math.min(sx*0.90,sx*1.02), Math.max(sx*0.90,sx*1.02), G.doorZ0+0.02, G.glassTop, 'leaf', -0.5, -1);
      wallY(out, y0, Math.min(sx*0.90,sx*1.02), Math.max(sx*0.90,sx*1.02), G.doorZ0+0.02, G.glassTop, 'leaf', -0.6, +1);
      slab(out, sx>0?[[0.90,y0],[1.02,y0],[1.02,y1],[0.90,y1]]:[[-1.02,y0],[-0.90,y0],[-0.90,y1],[-1.02,y1]], G.doorZ0, 'leaf', -0.55);
      part(out,(T)=>{
        wallX(T, sx*G.hwSide, y0, y1, G.doorZ0, G.beltZ, 'paint', sx>0?0.18:-0.42, sx, [[y0,0],[y1,0],[y1,1],[y0,1]], wear);
        const gq=(zA,zB,mat,b,off)=>{ const xa=gx(zA)+(off||0), xb=gx(zB)+(off||0);
          quad(T, sx>0?[xa,y0+0.04,zA]:[-xa,y1-0.04,zA],
                  sx>0?[xa,y1-0.04,zA]:[-xa,y0+0.04,zA],
                  sx>0?[xb,y1-0.04,zB]:[-xb,y0+0.04,zB],
                  sx>0?[xb,y0+0.04,zB]:[-xb,y1-0.04,zB], mat, b); };
        gq(G.beltZ, G.glassTop, 'paint', sx>0?0.10:-0.48);
        gq(1.48, 2.02, 'glass', sx>0?-0.15:-0.55, 0.012);
        wallX(T, sx*0.92, y0+0.02, y1-0.02, G.doorZ0+0.04, G.beltZ, 'leaf', -0.7, -sx);
        boxAt(T, Math.min(sx*1.02,sx*1.05), Math.max(sx*1.02,sx*1.05), y0+0.10, y0+0.28, 1.20, 1.26, 'trim', 0.3);
        if(s.mirrors){
          bar(T,[sx*1.01,y1-0.10,1.50],[sx*1.20,y1-0.02,1.58],0.020,'iron',-0.1);
          boxAt(T, Math.min(sx*1.16,sx*1.26), Math.max(sx*1.16,sx*1.26), y1-0.10, y1+0.06, 1.46, 1.74, 'paint', -0.05);
          wallY(T, y1-0.10, Math.min(sx*1.17,sx*1.25), Math.max(sx*1.17,sx*1.25), 1.50, 1.70, 'glass', -0.2, -1);
        }
      }, (p)=>hingeZ(p, sx*0.98, y1, ca, sa));
    }
  }

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
  function buildBarn(out,s){
    const wear=wearTex(s.weather);
    const roofZ=s.roofZ, top = roofZ - G.barnDrop;
    for(const d of [ {sx:+1, pose:s.barnR}, {sx:-1, pose:s.barnL} ]){
      const sx=d.sx, a=sx*d.pose*96*DEG, ca=Math.cos(a), sa=Math.sin(a);
      part(out,(T)=>{
        const x0=Math.min(sx*0.005,sx*G.barnHW), x1=Math.max(sx*0.005,sx*G.barnHW);
        boxAt(T, x0, x1, G.tailY, G.tailY+0.05, G.barnZ0, top, 'paint', -0.12, false, wear);
        if(s.windows) wallY(T, G.tailY-0.006, Math.min(sx*0.10,sx*(G.barnHW-0.10)), Math.max(sx*0.10,sx*(G.barnHW-0.10)), 1.52, 1.98, 'glass', -0.32, -1);
        boxAt(T, Math.min(sx*0.06,sx*0.10), Math.max(sx*0.06,sx*0.10), G.tailY-0.018, G.tailY, 1.06, 1.34, 'trim', 0.3, false);
      }, (p)=>hingeZ(p, sx*0.98, G.tailY, ca, sa));
    }
  }

  // ---- wheels & axles ----
  function wheelAt(out, xc, yc, sxOut, roll, yawDeg){
    if(yawDeg){ const a=yawDeg*DEG, ca=Math.cos(a), sa=Math.sin(a);
      part(out,(T)=>wheelAt(T,xc,yc,sxOut,roll,0),(p)=>hingeZ(p,xc,yc,ca,sa)); return; }
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

  function build(s){
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

  root.VanIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    BODY, TRIM, IRON, GALV, RUBBER, CHROME, CLOTH, GLASSD, GLASSN, KEY,
    BODIES, PRESETS, CUES, G, travel:{F:TF,R:TR},
    steer:{ maxInnerDeg:STEER_MAX, maxOuterDeg:+(steerAngles(1).R.toFixed(2)), angles:steerAngles },
    list, dims, resolve, render, frames, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
