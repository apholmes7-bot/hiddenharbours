/* Hidden Harbours — parametric ISO SADDLE-VEHICLE rig, THREE BODIES (same turntable + camera +
   shading as vehicleIsoRig.js / amphibIsoRig.js / the fleet): DIRTBIKE (Enduro 250), THREE-WHEELER
   (Trike 200) and QUAD (Utility 4x4). Everything you sit astride, nothing you sit inside: no cab,
   no doors, no bed — a saddle, a bar, pegs or boards, and racks where the class carries them.
   45deg steps, elev 40deg, flat-facet shading from the fixed upper-LEFT key, z-buffered, ordered
   dither, depth-edge darkening, NO AA. 32 px = 1 m. Ringless (ADR 0031).

   THE BAKE CARRIES NO RIDER. The character rig mounts at anchors(): seat, gripL/gripR, pegL/pegR.
   Grips ride the bars (they turn with steer); everything rides the suspension and, on the bike,
   the lean — anchors() applies the same transforms the bake does, so a rider placed on them sits
   right at any pose.

   CELL 256 x 192 @ pivot 128,128 (the Otter's cell) — the three are 1.9-2.2 m long.

   ARTICULATION — pose params on render(dir,opts):
     roll              master wheel roll, REVOLUTIONS of the REAR wheel (cyclic). The front scales
                       by rR/rF on the bike and trike so both tread the same road.
     rollF rollR       per-axle offsets, revolutions.
     steer  -1..1      handlebar. Bike/trike: the whole front assembly (wheel, fork, bars, fender,
                       lamp) turns about the RAKED steering axis through the head. Quad: the bars
                       turn about the stem and the front pair yaw about their own kingpins,
                       Ackermann-split. +1 is full LEFT (the nose swings toward -x) — the dually's
                       convention.
     susF susR -1..1   suspension per axle: the BODY drops toward the compressed end and pitches;
                       wheels stay on the ground. The trike's rear is RIGID (TR 0): its tires are
                       its springs.
     lean   -1..1      DIRTBIKE ONLY. Roll of the whole machine about the tyre contact line (world
                       z=0, x=0); +1 leans 28deg to the curb (+x). Cornering leans INTO the turn.
     stand  0..1       DIRTBIKE ONLY. Side stand on the street side (-x), 0 up -> 1 down, and the
                       bike settles 12deg onto it. DEFAULT 1: she bakes PARKED. A dirtbike cannot
                       stand upright unridden — the rolling cues set stand 0; a rider owns the rest.
     yaw               heading off the 45deg facing grid, DEGREES (-45..45), rebaked under the key.
   FITTED (parts, not poses): lamp (bike: true = enduro headlamp, false = MX number plate),
     rackF rackR (quad; rackR also on the trike), hitch, winch (quad).

   ORIGIN / PIVOT: ground-centre of the wheelbase. +x curb side, +y nose, +z up. Facing 0 (N) shows
   the TAIL; facing 4 (S) the nose. The exhaust is on the curb side (+x), the side stand on the
   street side (-x): a bike is mounted from the street side, over the stand.

   RAMP BUDGET: 11 ramps at most on any build — well under the fleet's 16.

   Exposes globalThis.AtvIso = { W,H,PX,DIRS,pivot,order,defaultElev, BODY,TRIM,IRON,GALV,RUBBER,
     CHROME,CLOTH,GLASSD,GLASSN,KEY, SPECS,BODIES,PRESETS,CUES,cuesFor,fittingsFor,steer, list(),
     dims(opts), resolve(opts), render(dir,opts), frames(dir,n,opts,cue), anchors(dir,opts),
     project(dir,p,elev,yaw), gameplayGeometry({body}), gameplayAll(), RIG_URL }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 256, H = 192, cx = 128, groundY = 128;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;                    // ADR 0031
  const ORDER = ['N','NE','E','SE','S','SW','W','NW'];

  // ---- ramps (harbour master ramps, shared with the houses, the fleet, the trucks) ----
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
  const CLOTH  = ['#1b1d21','#232629','#2c3034','#373c41','#44494f','#53595f'];
  const SHADE  = ['#0b0e11','#0f1418','#141a1f','#1a2128','#212a31','#28323a'];
  const GLASSD = ['#1b262b','#243238','#2f4149','#3d545c','#5d7b82','#96b6ba'];
  const GLASSN = ['#141d2b','#1d2a3d','#2a3c53','#3d5570','#6b7f9c','#95a8c0'];
  const GLOW   = ['#7a5a18','#c09a2c','#efd06a','#fdf0b6'];
  const LENSR  = ['#3a0c0a','#5a120e','#7d1c14','#a52a1d','#c93c2a','#e4573f'];
  const KEY    = '#1a1c22';

  // ---- shading (identical recipe to vehicleIsoRig / amphibIsoRig / the fleet) ----
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
  const lerp3=(a,b,t)=>[a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t, a[2]+(b[2]-a[2])*t];
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
  // annulus in the plane x, centred (yc,zc), from r0 out to r1; sgn = which way it faces
  function annulus(out, x, yc, zc, r0, r1, n, mat, b, sgn){
    for(let i=0;i<n;i++){ const a0=(i/n)*Math.PI*2, a1=((i+1)/n)*Math.PI*2;
      const O0=[x,yc+r1*Math.cos(a0),zc+r1*Math.sin(a0)], O1=[x,yc+r1*Math.cos(a1),zc+r1*Math.sin(a1)];
      const I0=[x,yc+r0*Math.cos(a0),zc+r0*Math.sin(a0)], I1=[x,yc+r0*Math.cos(a1),zc+r0*Math.sin(a1)];
      out.push(F(sgn>0?[O0,O1,I1,I0]:[O1,O0,I0,I1], mat, b)); }
  }
  // two-sided sheet strip between y0 and y1 (either order), half-width hw, sloping z0 -> z1
  function sheet(out, hw, y0, z0, y1, z1, mat, b, tex){
    if(y0>y1){ const ty=y0; y0=y1; y1=ty; const tz=z0; z0=z1; z1=tz; }
    const uv=tex?[[-hw,y0],[hw,y0],[hw,y1],[-hw,y1]]:null;
    out.push(F([[-hw,y0,z0],[hw,y0,z0],[hw,y1,z1],[-hw,y1,z1]], mat, b, 0, uv, tex));
    out.push(F([[hw,y0,z0-0.012],[-hw,y0,z0-0.012],[-hw,y1,z1-0.012],[hw,y1,z1-0.012]], mat, -0.85));
  }
  // side panel: pts for the +x side as [bottom-rear, bottom-front, top-front, top-rear]; sx<0 mirrors
  function sidePanel(out, sx, pts, mat, b, tex){
    const P = sx>0 ? pts : pts.map(p=>[-p[0],p[1],p[2]]).reverse();
    out.push(F(P, mat, b, 0, tex?P.map(p=>[p[1],p[2]]):null, tex));
  }
  // box whose top slopes from zBack (at y0) to zFront (at y1)
  function wedgeBox(out, x0,x1, y0,y1, z0, zBack, zFront, mat, b, tex){
    b=b||0;
    quad(out,[x0,y0,z0],[x1,y0,z0],[x1,y0,zBack],[x0,y0,zBack],mat,b-0.30);
    quad(out,[x1,y1,z0],[x0,y1,z0],[x0,y1,zFront],[x1,y1,zFront],mat,b+0.10);
    out.push(F([[x1,y0,z0],[x1,y1,z0],[x1,y1,zFront],[x1,y0,zBack]],mat,b+0.18));
    out.push(F([[x0,y0,zBack],[x0,y1,zFront],[x0,y1,z0],[x0,y0,z0]],mat,b-0.42));
    out.push(F([[x0,y0,zBack],[x1,y0,zBack],[x1,y1,zFront],[x0,y1,zFront]],mat,b+0.34,0,tex?[[x0,y0],[x1,y0],[x1,y1],[x0,y1]]:null,tex));
  }
  // plastic fender shell: flat top at zT (half-width hwT), aprons flaring out to hwB at zB, closed ends
  function fenderShell(out, y0, y1, hwT, zT, hwB, zB, mat, b, tex){
    b=b||0;
    out.push(F([[-hwT,y0,zT],[hwT,y0,zT],[hwT,y1,zT],[-hwT,y1,zT]],mat,b+0.34,0,tex?[[-hwT,y0],[hwT,y0],[hwT,y1],[-hwT,y1]]:null,tex));
    out.push(F([[hwB,y0,zB],[hwB,y1,zB],[hwT,y1,zT],[hwT,y0,zT]],mat,b+0.18,0,tex?[[y0,zB],[y1,zB],[y1,zT],[y0,zT]]:null,tex));
    out.push(F([[-hwT,y0,zT],[-hwT,y1,zT],[-hwB,y1,zB],[-hwB,y0,zB]],mat,b-0.42,0,tex?[[y0,zT],[y1,zT],[y1,zB],[y0,zB]]:null,tex));
    out.push(F([[hwB,y1,zB],[-hwB,y1,zB],[-hwT,y1,zT],[hwT,y1,zT]],mat,b+0.10));
    out.push(F([[-hwB,y0,zB],[hwB,y0,zB],[hwT,y0,zT],[-hwT,y0,zT]],mat,b-0.30));
  }
  // arc of sheet over a wheel: angles in degrees from +y (0) over the top (90) toward -y (180)
  function fenderArc(out, yc, zc, R, hw, a0, a1, n, mat, b, tex){
    for(let i=0;i<n;i++){ const p=a0+(a1-a0)*i/n, q=a0+(a1-a0)*(i+1)/n;
      sheet(out, hw, yc+R*Math.cos(p*DEG), zc+R*Math.sin(p*DEG), yc+R*Math.cos(q*DEG), zc+R*Math.sin(q*DEG), mat, b, tex); }
  }
  // rotations: Rodrigues about an arbitrary axis (the raked steering head), vertical hinge (kingpins, stem)
  function rotAxis(p, o, u, th){ if(!th) return p; const c=Math.cos(th), s=Math.sin(th);
    const v=[p[0]-o[0],p[1]-o[1],p[2]-o[2]], d=u[0]*v[0]+u[1]*v[1]+u[2]*v[2], k=crs(u,v);
    return [ o[0]+v[0]*c+k[0]*s+u[0]*d*(1-c), o[1]+v[1]*c+k[1]*s+u[1]*d*(1-c), o[2]+v[2]*c+k[2]*s+u[2]*d*(1-c) ]; }
  const hingeZ=(p,hx,hy,ca,sa)=>{ const dx=p[0]-hx, dy=p[1]-hy; return [hx+dx*ca-dy*sa, hy+dx*sa+dy*ca, p[2]]; };
  function part(out, fn, xf){ const T=[]; fn(T); if(xf) for(const f of T) f.v=f.v.map(xf); for(const f of T) out.push(f); }

  // ---- textures ----
  function wearTex(w){ return (u,v)=>{ if(w>0.03 && hash2(Math.floor(u*6.5)|0, Math.floor(v*6.5)|0) < w*0.10) return -1; return 0; }; }
  // knobby tread: c is the lug pitch, fitted to a whole number of lugs per revolution so the loop seam closes
  function lugTex(phase, c){ c=c||0.155; return (u,v)=>{ const f=(((u+phase)%c)+c)%c; return f<c*0.46?-1:0; }; }

  // ================= GEOMETRY — THE THREE SADDLES =================
  const SPECS = {
    dirtbike: { key:'dirtbike', label:'Enduro 250', kind:'dirtbike', wheels:2,
      axF:0.74, axR:-0.74, rF:0.35, rR:0.33, wF:0.08, wR:0.11, rimF:0.26, rimR:0.23,
      rake:27, forkX:0.09, forkL:0.60, lowerL:0.33,
      barY:0.42, barZ:1.04, barHW:0.40, seatZ:0.94, seatRef:[0,-0.30,0.94],
      grips:[[-0.33,0.42,1.04],[0.33,0.42,1.04]], pegs:[[-0.27,-0.06,0.42],[0.27,-0.06,0.42]],
      standPivot:[-0.12,-0.12,0.36], standTip:[-0.31,-0.02,0.066], standStow:[-0.14,-0.50,0.40],
      standLean:12, leanMax:28, steerMax:35, TF:0.14, TR:0.12, swingPivot:[-0.22,0.44],
      clearance:0.32, mass:112, yMin:-1.07, yMax:1.16, width:0.86, height:1.07 },
    trike: { key:'trike', label:'Trike 200', kind:'three_wheeler', wheels:3,
      axF:0.60, axR:-0.60, rF:0.29, wF:0.20, rimF:0.14, rR:0.28, wR:0.28, rimR:0.11, rearX:0.42,
      rake:25, forkX:0.11, forkL:0.52, lowerL:0.28,
      barY:0.36, barZ:0.92, barHW:0.38, seatZ:0.76, seatRef:[0,-0.34,0.76],
      grips:[[-0.31,0.36,0.92],[0.31,0.36,0.92]], pegs:[[-0.32,-0.08,0.34],[0.32,-0.08,0.34]],
      steerMax:30, TF:0.08, TR:0, rackR:{x:0.26,y:[-0.90,-0.60],z:0.70},
      fenderR:{y:[-0.92,-0.22],z:0.62,hwT:0.50,hwB:0.60,zB:0.48},
      clearance:0.20, mass:132, yMin:-0.935, yMax:0.92, width:1.20, height:0.95 },
    quad: { key:'quad', label:'Utility Quad 4x4', kind:'quad', wheels:4,
      axF:0.64, axR:-0.64, r:0.315, wFt:0.20, wRr:0.25, rim:0.15, wheelX:0.48,
      stem:[0,0.40], barY:0.36, barZ:1.04, barHW:0.38, seatZ:0.90, seatRef:[0,-0.30,0.90],
      grips:[[-0.31,0.36,1.04],[0.31,0.36,1.04]], boards:[[-0.33,-0.04,0.365],[0.33,-0.04,0.365]],
      steerMax:30, barsMax:28, TF:0.10, TR:0.09,
      fenderF:{y:[0.24,0.98],z:0.72,hw:0.60}, fenderR:{y:[-1.00,-0.24],z:0.72,hw:0.60},
      rackF:{x:0.40,y:[0.44,0.94],z:0.80}, rackR:{x:0.46,y:[-0.98,-0.70],z:0.82},
      hitch:[0,-1.06,0.47], winch:[0,0.95,0.43],
      clearance:0.22, mass:300, yMin:-1.10, yMax:1.058, width:1.28, height:1.07 },
  };
  const axisOf=(P)=>[0,-Math.sin(P.rake*DEG),Math.cos(P.rake*DEG)];
  const alongFork=(P,x,t)=>{ const u=axisOf(P); return [x, P.axF+u[1]*t, P.rF+u[2]*t]; };
  const headOf=(P)=>alongFork(P,0,P.forkL);

  const BODIES = {};
  for(const k of Object.keys(SPECS)){ const P=SPECS[k];
    BODIES[k]={ key:k, label:P.label, kind:P.kind, loa:+(P.yMax-P.yMin).toFixed(2), width:P.width, height:P.height,
      wheelbase:+(P.axF-P.axR).toFixed(2), seatZ:P.seatZ, clearance:P.clearance, wheels:P.wheels,
      rearWheelR: P.rR!=null?P.rR:P.r, distancePerRev:+(2*Math.PI*(P.rR!=null?P.rR:P.r)).toFixed(3) }; }

  // ---- steering: the quad's front pair yaw about their own kingpins, Ackermann-split ----
  function steerAngles(v, P){ if(!v) return { L:0, R:0 };
    const inner=Math.abs(v)*P.steerMax*DEG, outer=Math.atan(1/(1/Math.tan(inner) + (P.wheelX*2)/(P.axF-P.axR)));
    const i=inner/DEG, o=outer/DEG; return v>0 ? { L:+i, R:+o } : { L:-o, R:-i }; }

  const PRESETS = {
    showroom:  { paint:'red', weather:0.05 },
    trailWorn: { paint:'blue', weather:0.55 },
    enduro:    { body:'dirtbike', paint:'teal', weather:0.30, lamp:true, stand:1 },
    mxPlate:   { body:'dirtbike', paint:'rustOrange', weather:0.22, lamp:false, stand:1 },
    beachTrike:{ body:'trike', paint:'gold', weather:0.38, rackR:true },
    wharfQuad: { body:'quad', paint:'sage', weather:0.42, rackF:true, rackR:true, hitch:true, winch:true },
  };
  // named pose sweeps for frames(): t runs 0..1 over the strip
  const CUES = {
    roll:  (t)=>({ roll:t, stand:0 }),                                                         // cyclic
    turn:  (t)=>({ steer:Math.sin(t*Math.PI*2), yaw:Math.sin(t*Math.PI*2)*14, lean:-Math.sin(t*Math.PI*2)*0.55, roll:t, stand:0 }),  // cyclic
    bounce:(t)=>({ susF:Math.sin(t*Math.PI*2)*0.8, susR:Math.sin(t*Math.PI*2+1.3)*0.8, roll:t, stand:0 }),  // cyclic
    steer: (t)=>({ steer:t*2-1 }),                                                             // full right lock -> full left lock
    park:  (t)=>({ stand:t, lean:0 }),                                                         // dirtbike: upright -> down on the stand
  };
  const CYCLIC = { roll:true, turn:true, bounce:true };
  function cuesFor(body){ return body==='dirtbike' ? ['roll','turn','bounce','steer','park'] : ['roll','turn','bounce','steer']; }
  function fittingsFor(body){
    if(body==='dirtbike') return [{key:'lamp',label:'HEADLAMP',off:'MX PLATE'}];
    if(body==='trike') return [{key:'rackR',label:'REAR RACK'}];
    return [{key:'rackF',label:'FRONT RACK'},{key:'rackR',label:'REAR RACK'},{key:'hitch',label:'HITCH'},{key:'winch',label:'WINCH'}];
  }

  function resolve(opts){
    opts=opts||{};
    const g=(k,d)=> opts[k]!=null?opts[k]:d;
    const c01=(v)=>Math.max(0,Math.min(1,v)), c11=(v)=>Math.max(-1,Math.min(1,v));
    const body = SPECS[opts.body] ? opts.body : 'quad', P=SPECS[body], bike=body==='dirtbike';
    const stand = bike ? c01(g('stand',1)) : 0, lean = bike ? c11(g('lean',0)) : 0;
    return {
      body, P, B:BODIES[body],
      paint: opts.paint||'red', weather:g('weather',0.32),
      roll:g('roll',0), rollF:g('rollF',0), rollR:g('rollR',0),
      steer:c11(g('steer',0)), susF:c11(g('susF',0)), susR: P.TR>0 ? c11(g('susR',0)) : 0,
      yaw:Math.max(-45,Math.min(45,g('yaw',0))),
      lean, stand, leanDeg: bike ? (lean*P.leanMax - stand*P.standLean) : 0,
      lamp: bike ? !!g('lamp',true) : true,
      rackF: body==='quad' ? !!g('rackF',true) : false,
      rackR: body!=='dirtbike' ? !!g('rackR',true) : false,
      hitch: body==='quad' ? !!g('hitch',true) : false,
      winch: body==='quad' ? !!g('winch',false) : false,
      night:!!opts.night, outline: opts.outline!=null?!!opts.outline:KEYLINE_DEFAULT };
  }
  function dims(opts){ const s=resolve(opts); return Object.assign({ travelF:s.P.TF, travelR:s.P.TR, leanDeg:s.leanDeg }, s.B); }
  function dzFn(s){ const P=s.P; return (y)=>{ const t=(y-P.axR)/(P.axF-P.axR); return -(s.susF*P.TF)*t - (s.susR*P.TR)*(1-t); }; }

  // ---- wheels ----
  // spoked (the bike): open tread band + sidewall annuli + rim band, hub, six spokes, brake disc
  function wheelSpoked(out, xc, yc, r, w, rimR, roll, brakeSide){
    const n=14, ph=roll*2*Math.PI, c=2*Math.PI*r/Math.round(2*Math.PI*r/0.155);
    tube(out,[xc-w/2,yc,r],[xc+w/2,yc,r], r, n, 'rubber', -0.05, false, lugTex(roll*2*Math.PI*r, c));
    annulus(out, xc+w/2, yc, r, rimR, r, n, 'rubber', 0.02, +1);
    annulus(out, xc-w/2, yc, r, rimR, r, n, 'rubber', -0.40, -1);
    tube(out,[xc-w*0.34,yc,r],[xc+w*0.34,yc,r], rimR, 12, 'chrome', 0.10, false);
    tube(out,[xc-0.035,yc,r],[xc+0.035,yc,r], 0.065, 8, 'iron', -0.15, true);
    for(let k=0;k<6;k++){ const th=ph+k*Math.PI/3, sx=(k&1)?1:-1, cy=Math.cos(th), sz=Math.sin(th);
      bar(out,[xc+sx*0.022,yc+cy*0.065,r+sz*0.065],[xc+sx*0.010,yc+cy*(rimR-0.01),r+sz*(rimR-0.01)],0.014,'galv',0.25); }
    if(brakeSide) tube(out,[xc+brakeSide*(w/2+0.005),yc,r],[xc+brakeSide*(w/2+0.015),yc,r],0.11,10,'galv',0.05,true);
  }
  // balloon (trike, quad): solid tire with a steel wheel proud of the outboard face (both faces when centred)
  function wheelBalloon(out, xc, yc, r, w, rimR, roll, sxOut, rimMat, yawDeg){
    if(yawDeg){ const a=yawDeg*DEG, ca=Math.cos(a), sa=Math.sin(a);
      part(out,(T)=>wheelBalloon(T,xc,yc,r,w,rimR,roll,sxOut,rimMat,0),(p)=>hingeZ(p,xc,yc,ca,sa)); return; }
    const ph=roll*2*Math.PI, c=2*Math.PI*r/Math.round(2*Math.PI*r/0.155);
    tube(out,[xc-w/2,yc,r],[xc+w/2,yc,r], r, 14, 'rubber', -0.05, true, lugTex(roll*2*Math.PI*r, c));
    const sides = sxOut ? [sxOut] : [-1,1];
    for(const sx of sides){
      const xf=xc+sx*(w/2+0.012);
      tube(out,[xc+sx*(w/2-0.02),yc,r],[xf,yc,r], rimR, 12, rimMat, sx>0?0.22:-0.10);
      for(let k=0;k<4;k++){ const th=ph+k*Math.PI/2, py=yc+Math.cos(th)*rimR*0.55, pz=r+Math.sin(th)*rimR*0.55;
        tube(out,[xf-sx*0.004,py,pz],[xf+sx*0.02,py,pz],0.022,6,'iron',0.1); }
      tube(out,[xf,yc,r],[xf+sx*0.022,yc,r],0.04,8,'iron',-0.2);
    }
  }
  function rackTubes(out, R, zBase){ const z=R.z;
    for(const sx of [-1,1]){ bar(out,[sx*R.x,R.y[0],z],[sx*R.x,R.y[1],z],0.014,'iron',sx>0?0.1:-0.2);
      for(const y of R.y) bar(out,[sx*R.x,y,zBase],[sx*R.x,y,z],0.014,'iron',0); }
    for(const y of [R.y[0],(R.y[0]+R.y[1])/2,R.y[1]]) bar(out,[-R.x,y,z],[R.x,y,z],0.014,'iron',0.15);
    for(const x of [-R.x*0.5,0,R.x*0.5]) bar(out,[x,R.y[0],z],[x,R.y[1],z],0.012,'iron',0.12);
  }

  // ---- DIRTBIKE ----
  function buildDirtbike(body, rolling, s, dz){
    const D=SPECS.dirtbike, u=axisOf(D), Hd=headOf(D), th=s.steer*D.steerMax*DEG;
    const xf=(p)=>rotAxis(p,Hd,u,th);
    // front assembly, body half: stanchions, clamps, bars, lamp or plate, high fender — turns, then rides the forks
    part(body,(T)=>{
      for(const sx of [-1,1]) tube(T, alongFork(D,sx*D.forkX,0.30), alongFork(D,sx*D.forkX,0.66), 0.020, 6, 'chrome', sx>0?0.1:-0.3, true);
      const c1=alongFork(D,0,0.52), c2=alongFork(D,0,0.63);
      boxAt(T,-0.12,0.12,c1[1]-0.045,c1[1]+0.045,c1[2]-0.035,c1[2]+0.035,'alloy',0);
      boxAt(T,-0.12,0.12,c2[1]-0.045,c2[1]+0.045,c2[2]-0.03,c2[2]+0.03,'alloy',0.05);
      for(const sx of [-1,1]) bar(T,[sx*0.05,c2[1],c2[2]+0.03],[sx*0.05,D.barY,D.barZ-0.01],0.02,'alloy',0);
      tube(T,[-D.barHW,D.barY,D.barZ],[D.barHW,D.barY,D.barZ],0.014,6,'galv',0.15,true);
      for(const sx of [-1,1]) tube(T,[sx*0.26,D.barY,D.barZ],[sx*D.barHW,D.barY,D.barZ],0.03,8,'rubber',-0.1,true);
      if(s.lamp){ boxAt(T,-0.10,0.10,0.50,0.585,0.79,0.98,'paint',0.05);
        wallY(T,0.59,-0.08,0.08,0.83,0.95,s.night?'glow':'head',0.35,+1); }
      else boxAt(T,-0.13,0.13,0.545,0.565,0.76,1.00,'trim',0.1);
      sheet(T,0.085,0.44,0.775,0.86,0.78,'paint',0.30);
      sheet(T,0.085,0.86,0.78,1.16,0.70,'paint',0.28);
    }, xf);
    // front assembly, rolling half: wheel, fork guards on the lowers, axle — turns with the bars, stays on the ground
    part(rolling,(T)=>{
      wheelSpoked(T,0,D.axF,D.rF,D.wF,D.rimF,s.roll*(D.rR/D.rF)+s.rollF,-1);
      for(const sx of [-1,1]) tube(T, alongFork(D,sx*D.forkX,0.03), alongFork(D,sx*D.forkX,D.lowerL), 0.030, 6, 'paint', sx>0?0.05:-0.35, true);
      tube(T,[-D.forkX,D.axF,D.rF],[D.forkX,D.axF,D.rF],0.018,6,'iron',-0.2,true);
    }, xf);
    // frame
    bar(body,[0,Hd[1]-0.01,Hd[2]-0.06],[0,0.40,0.36],0.030,'galv',-0.1);                    // down tube
    bar(body,[0,Hd[1]-0.03,Hd[2]-0.02],[0,-0.16,0.76],0.028,'galv',0.05);                   // backbone
    for(const sx of [-1,1]){
      bar(body,[sx*0.12,0.40,0.34],[sx*0.12,-0.18,0.34],0.02,'galv',sx>0?0:-0.3);           // cradle rails
      bar(body,[sx*0.10,-0.20,0.50],[sx*0.10,-0.74,0.86],0.02,'galv',sx>0?0:-0.3);          // subframe
      bar(body,[sx*0.10,-0.20,0.50],[sx*0.10,-0.20,0.76],0.02,'galv',sx>0?0:-0.3);          // rear upright
      bar(body,[sx*0.17,-0.06,0.42],[sx*0.27,-0.06,0.42],0.016,'iron',sx>0?0.1:-0.2);      // pegs
    }
    // engine
    boxAt(body,-0.15,0.15,-0.10,0.30,0.34,0.62,'iron',-0.15);
    boxAt(body,-0.09,0.09,0.10,0.30,0.62,0.80,'alloy',0.0);
    boxAt(body,0.15,0.19,-0.06,0.14,0.38,0.54,'alloy',0.05);
    boxAt(body,-0.19,-0.15,-0.06,0.14,0.38,0.54,'alloy',-0.2);
    // plastics: tank, shrouds, side plates, seat, rear fender, tail lamp
    wedgeBox(body,-0.16,0.16,0.06,0.42,0.80,0.99,0.93,'paint',0.1);
    for(const sx of [-1,1]){
      sidePanel(body,sx,[[0.18,0.18,0.66],[0.14,0.50,0.66],[0.15,0.44,0.96],[0.19,0.12,0.96]],'paint',sx>0?0.18:-0.42);
      sidePanel(body,sx,[[0.17,-0.70,0.60],[0.17,-0.30,0.60],[0.15,-0.30,0.86],[0.15,-0.70,0.86]],'paint',sx>0?0.18:-0.42);
    }
    boxAt(body,-0.13,0.13,-0.74,0.08,0.86,0.94,'cloth',-0.25);
    sheet(body,0.14,-0.70,0.94,-1.06,0.89,'paint',0.30);
    boxAt(body,-0.04,0.04,-1.07,-1.03,0.86,0.90,'lensR',0.2);
    // exhaust: header down the curb side into the muffler under the side plate
    tube(body,[0.06,0.32,0.72],[0.17,0.46,0.56],0.026,6,'galv',0.1,false);
    tube(body,[0.17,0.46,0.56],[0.21,0.30,0.44],0.026,6,'galv',0.05,false);
    tube(body,[0.21,0.30,0.44],[0.20,-0.30,0.58],0.026,6,'galv',0.05,false);
    tube(body,[0.20,-0.30,0.60],[0.19,-0.98,0.66],0.05,8,'chrome',0.15,true);
    bar(body,[0,-0.36,0.42],[0,-0.20,0.84],0.028,'galv',0.0);                                // shock
    { const tip=lerp3(D.standStow,D.standTip,s.stand);                                        // side stand
      bar(body,D.standPivot,tip,0.02,'galv',-0.2);
      if(s.stand>0.02) boxAt(body,tip[0]-0.035,tip[0]+0.025,tip[1]-0.03,tip[1]+0.03,tip[2]-0.005,tip[2]+0.025,'galv',-0.3); }
    // rear wheel, sprocket (street side), swingarm from the sprung pivot down to the grounded axle
    wheelSpoked(rolling,0,D.axR,D.rR,D.wR,D.rimR,s.roll+s.rollR,+1);
    tube(rolling,[-0.105,D.axR,D.rR],[-0.085,D.axR,D.rR],0.105,10,'iron',-0.2,true);
    const pz=D.swingPivot[1]+dz(D.swingPivot[0]);
    for(const sx of [-1,1]) bar(rolling,[sx*0.10,D.swingPivot[0],pz],[sx*0.10,D.axR,D.rR],0.022,'alloy',sx>0?0.1:-0.3);
    tube(rolling,[-0.11,D.axR,D.rR],[0.11,D.axR,D.rR],0.018,6,'iron',-0.2,true);
  }

  // ---- THREE-WHEELER ----
  function buildTrike(body, rolling, s, dz){
    const T=SPECS.trike, u=axisOf(T), Hd=headOf(T), th=s.steer*T.steerMax*DEG, xf=(p)=>rotAxis(p,Hd,u,th), wear=wearTex(s.weather);
    part(body,(Q)=>{
      for(const sx of [-1,1]) tube(Q, alongFork(T,sx*T.forkX,0.26), alongFork(T,sx*T.forkX,T.forkL+0.05), 0.020, 6, 'chrome', sx>0?0.1:-0.3, true);
      const c1=alongFork(T,0,T.forkL-0.07), c2=alongFork(T,0,T.forkL+0.03);
      boxAt(Q,-0.14,0.14,c1[1]-0.045,c1[1]+0.045,c1[2]-0.03,c1[2]+0.03,'alloy',0);
      boxAt(Q,-0.14,0.14,c2[1]-0.045,c2[1]+0.045,c2[2]-0.03,c2[2]+0.03,'alloy',0.05);
      for(const sx of [-1,1]) bar(Q,[sx*0.06,c2[1],c2[2]+0.03],[sx*0.06,T.barY,T.barZ-0.01],0.02,'alloy',0);
      tube(Q,[-T.barHW,T.barY,T.barZ],[T.barHW,T.barY,T.barZ],0.014,6,'galv',0.15,true);
      for(const sx of [-1,1]) tube(Q,[sx*0.24,T.barY,T.barZ],[sx*T.barHW,T.barY,T.barZ],0.03,8,'rubber',-0.1,true);
      tube(Q,[0,Hd[1]+0.03,Hd[2]+0.05],[0,Hd[1]+0.13,Hd[2]+0.05],0.08,10,'paint',0.05,false);     // lamp shell
      tube(Q,[0,Hd[1]+0.13,Hd[2]+0.05],[0,Hd[1]+0.14,Hd[2]+0.05],0.072,10,s.night?'glow':'head',0.3,true);
      fenderArc(Q,T.axF,T.rF,0.37,0.16,30,130,5,'paint',0.28,wear);
    }, xf);
    part(rolling,(Q)=>{
      wheelBalloon(Q,0,T.axF,T.rF,T.wF,T.rimF,s.roll*(T.rR/T.rF)+s.rollF,0,'galv');
      for(const sx of [-1,1]) tube(Q, alongFork(T,sx*T.forkX,0.03), alongFork(T,sx*T.forkX,T.lowerL), 0.026, 6, 'galv', sx>0?0:-0.35, true);
      tube(Q,[-T.forkX,T.axF,T.rF],[T.forkX,T.axF,T.rF],0.02,6,'iron',-0.2,true);
    }, xf);
    // frame, rigid rear axle (unsprung AND unsuspended — it is the body's), final drive
    bar(body,[0,Hd[1]-0.02,Hd[2]-0.06],[0,-0.30,0.56],0.03,'galv',0.05);
    bar(body,[0,Hd[1]-0.02,Hd[2]-0.10],[0,0.28,0.32],0.03,'galv',-0.1);
    for(const sx of [-1,1]){ bar(body,[sx*0.13,0.28,0.30],[sx*0.13,-0.62,0.30],0.02,'galv',sx>0?0:-0.3);
      bar(body,[sx*0.18,-0.08,0.34],[sx*0.32,-0.08,0.34],0.016,'iron',sx>0?0.1:-0.2); }
    tube(body,[-T.rearX,T.axR,T.rR],[T.rearX,T.axR,T.rR],0.035,8,'iron',-0.2,true);
    boxAt(body,-0.10,0.10,-0.72,-0.50,0.20,0.38,'iron',-0.2);
    // engine, airbox, tank, seat
    boxAt(body,-0.16,0.16,-0.14,0.22,0.30,0.56,'iron',-0.15);
    boxAt(body,-0.10,0.10,0.04,0.22,0.56,0.72,'alloy',0);
    boxAt(body,-0.14,0.14,-0.24,0.02,0.44,0.64,'iron',-0.3);
    wedgeBox(body,-0.16,0.16,0.02,0.36,0.62,0.82,0.76,'paint',0.1);
    boxAt(body,-0.17,0.17,-0.72,0.02,0.64,0.76,'cloth',-0.25);
    // one-piece rear fender over both balloon tires, rack on it
    const R=T.fenderR;
    fenderShell(body,R.y[0],R.y[1],R.hwT,R.z,R.hwB,R.zB,'paint',0.0,wear);
    if(s.rackR) rackTubes(body,T.rackR,R.z);
    tube(body,[0.06,0.24,0.68],[0.24,0.40,0.52],0.026,6,'galv',0.1,false);
    tube(body,[0.24,0.40,0.52],[0.30,-0.10,0.66],0.026,6,'galv',0.05,false);
    tube(body,[0.30,-0.10,0.67],[0.30,-0.72,0.70],0.055,8,'chrome',0.15,true);                 // muffler, along the fender
    boxAt(body,-0.05,0.05,-0.935,-0.90,0.55,0.60,'lensR',0.2);
    for(const sx of [-1,1]) wheelBalloon(rolling,sx*T.rearX,T.axR,T.rR,T.wR,T.rimR,s.roll+s.rollR,sx,'galv');
  }

  // ---- QUAD ----
  function buildQuad(body, rolling, s, dz){
    const Q=SPECS.quad, wear=wearTex(s.weather), st=steerAngles(s.steer,Q), FF=Q.fenderF, FR=Q.fenderR;
    for(const sx of [-1,1]){
      boxAt(body, sx*0.20-0.025, sx*0.20+0.025, -0.84, 0.84, 0.30, 0.36, 'iron', -0.2);                        // frame rails
      boxAt(body, Math.min(sx*0.20,sx*0.46), Math.max(sx*0.20,sx*0.46), -0.34, 0.26, 0.32, 0.365, 'rubber', -0.15);  // footboards
    }
    boxAt(body,-0.16,0.16,-0.26,0.22,0.30,0.44,'iron',-0.15);                                   // engine, what shows of it
    boxAt(body,-0.20,0.20,-0.30,0.26,0.40,0.80,'paint',-0.12,false,wear);                       // centre body panel
    wedgeBox(body,-0.17,0.17,0.02,0.36,0.72,0.98,0.90,'paint',0.1);                             // tank
    wedgeBox(body,-0.17,0.17,-0.66,0.02,0.80,0.94,0.90,'cloth',-0.25);                          // seat, rising aft
    fenderShell(body,FF.y[0],FF.y[1],FF.hw,FF.z,FF.hw+0.04,FF.z-0.12,'paint',0,wear);
    fenderShell(body,FR.y[0],FR.y[1],FR.hw,FR.z,FR.hw+0.04,FR.z-0.12,'paint',0,wear);
    wallY(body,FF.y[1],-FF.hw-0.04,FF.hw+0.04,0.44,FF.z-0.12,'paint',0.05,+1);                  // nose below the shell
    wallY(body,FF.y[0],-FF.hw-0.04,FF.hw+0.04,0.36,FF.z-0.12,'paint',-0.30,-1);                 // fender back, down to the boards
    wallY(body,FR.y[0],-FR.hw-0.04,FR.hw+0.04,0.44,FR.z-0.12,'paint',-0.30,-1);                 // tail below the shell
    wallY(body,FR.y[1],-FR.hw-0.04,FR.hw+0.04,0.36,FR.z-0.12,'paint',0.05,+1);
    for(const sx of [-1,1]) boxAt(body,Math.min(sx*0.22,sx*0.36),Math.max(sx*0.22,sx*0.36),FF.y[1]-0.005,FF.y[1]+0.025,0.54,0.64,s.night?'glow':'head',0.3,false);
    const yB=1.04;                                                                              // brush guard
    for(const sx of [-1,1]){ bar(body,[sx*0.34,yB,0.30],[sx*0.34,yB,0.66],0.018,'iron',sx>0?0.05:-0.2);
      bar(body,[sx*0.34,yB,0.34],[sx*0.20,0.86,0.33],0.016,'iron',-0.1); }
    for(const z of [0.42,0.66]) bar(body,[-0.34,yB,z],[0.34,yB,z],0.018,'iron',0.1);
    if(s.winch){ for(const sx of [-1,1]) boxAt(body,Math.min(sx*0.13,sx*0.16),Math.max(sx*0.13,sx*0.16),0.90,1.00,0.36,0.50,'iron',-0.1);
      tube(body,[-0.13,Q.winch[1],Q.winch[2]],[0.13,Q.winch[1],Q.winch[2]],0.045,8,'galv',0.05,true); }
    if(s.rackF) rackTubes(body,Q.rackF,FF.z);
    if(s.rackR) rackTubes(body,Q.rackR,FR.z);
    { const a=s.steer*Q.barsMax*DEG, ca=Math.cos(a), sa=Math.sin(a);                           // stem + bars turn about the stem
      part(body,(P)=>{
        bar(P,[0,0.40,0.72],[0,0.38,0.98],0.024,'galv',0.05);
        bar(P,[0,0.38,0.98],[0,Q.barY,Q.barZ-0.01],0.02,'alloy',0);
        tube(P,[-Q.barHW,Q.barY,Q.barZ],[Q.barHW,Q.barY,Q.barZ],0.014,6,'galv',0.15,true);
        for(const sx of [-1,1]) tube(P,[sx*0.24,Q.barY,Q.barZ],[sx*Q.barHW,Q.barY,Q.barZ],0.03,8,'rubber',-0.1,true);
        boxAt(P,-0.09,0.09,0.33,0.42,0.98,1.06,'paint',0.05);
      },(p)=>hingeZ(p,Q.stem[0],Q.stem[1],ca,sa)); }
    tube(body,[0.28,-0.56,0.50],[0.30,-1.04,0.52],0.05,8,'galv',0.05,true);                    // muffler, tip out past the tail
    if(s.hitch){ boxAt(body,-0.04,0.04,-1.10,-1.00,0.36,0.44,'iron',-0.1); tube(body,[0,Q.hitch[1],0.44],[0,Q.hitch[1],0.50],0.026,8,'chrome',0.3,true); }
    for(const sx of [-1,1]) boxAt(body,Math.min(sx*0.30,sx*0.42),Math.max(sx*0.30,sx*0.42),FR.y[0]-0.025,FR.y[0]+0.005,0.56,0.64,'lensR',0.2,false);
    // unsprung: solid rear axle + diff, swingarm and A-arms from the sprung frame down to the grounded hubs
    tube(rolling,[-0.40,Q.axR,Q.r],[0.40,Q.axR,Q.r],0.04,8,'iron',-0.2,true);
    boxAt(rolling,-0.10,0.10,Q.axR-0.10,Q.axR+0.10,0.22,0.40,'iron',-0.2);
    for(const sx of [-1,1]){
      bar(rolling,[sx*0.16,-0.20,0.34+dz(-0.20)],[sx*0.16,Q.axR,Q.r],0.02,'iron',sx>0?0:-0.3);
      for(const y of [0.54,0.74]) bar(rolling,[sx*0.20,y,0.33+dz(y)],[sx*0.36,Q.axF,0.30],0.014,'iron',sx>0?0:-0.3);
    }
    wheelBalloon(rolling, Q.wheelX,Q.axF,Q.r,Q.wFt,Q.rim,s.roll+s.rollF,+1,'galv',st.R);
    wheelBalloon(rolling,-Q.wheelX,Q.axF,Q.r,Q.wFt,Q.rim,s.roll+s.rollF,-1,'galv',st.L);
    wheelBalloon(rolling, Q.wheelX,Q.axR,Q.r,Q.wRr,Q.rim,s.roll+s.rollR,+1,'galv');
    wheelBalloon(rolling,-Q.wheelX,Q.axR,Q.r,Q.wRr,Q.rim,s.roll+s.rollR,-1,'galv');
  }

  function build(s){
    const body=[], rolling=[], dz=dzFn(s);
    if(s.body==='dirtbike') buildDirtbike(body,rolling,s,dz);
    else if(s.body==='trike') buildTrike(body,rolling,s,dz);
    else buildQuad(body,rolling,s,dz);
    // suspension: the body drops toward whichever axle is compressed and pitches; wheels stay grounded
    for(const f of body) f.v=f.v.map(p=>[p[0],p[1],p[2]+dz(p[1])]);
    const all=body.concat(rolling);
    // lean (dirtbike): the whole machine rolls about the tyre contact line; +x is toward the curb
    if(s.leanDeg){ const a=s.leanDeg*DEG, ca=Math.cos(a), sa=Math.sin(a);
      for(const f of all) f.v=f.v.map(p=>[p[0]*ca+p[2]*sa, p[1], -p[0]*sa+p[2]*ca]); }
    return all;
  }

  // ---- materials ----
  function makeMats(s){
    const wx=s.weather, night=s.night;
    const grime=r=>r.map(c=>mix(desat(c,wx*0.24),'#3a3128',wx*0.12));
    const rust =r=>r.map(c=>mix(c,'#6d3417',wx*0.26));
    const cool =r=>night?r.map(c=>mix(desat(c,0.30),'#1b2740',0.40)):r;
    const t=r=>cool(grime(r)), tm=r=>cool(rust(grime(r)));
    const paintRamp=BODY[s.paint]||BODY.red;
    return {
      paint :{ ramp:t(paintRamp), polish:0.12 },                       // plastics, not painted steel
      trim  :{ ramp:t(TRIM) },
      cloth :{ ramp:cool(CLOTH) }, shade:{ ramp:cool(SHADE) },
      iron  :{ ramp:tm(IRON) }, galv:{ ramp:tm(GALV) },
      chrome:{ ramp:t(CHROME) }, alloy:{ ramp:t(CHROME.map(c=>desat(c,0.15))) },
      rubber:{ ramp:t(RUBBER) },
      lensR :{ ramp:LENSR },
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
          if(deff<zbuf[i]){
            let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi+=tex(uu,vv); }
            zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat;
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
  function frames(dir, n, opts, cue){ n=n||8; const fn=CUES[cue||'roll']||CUES.roll, out=[];
    const cyclic = !!CYCLIC[cue||'roll'];
    for(let i=0;i<n;i++){ const t = cyclic ? i/n : i/(n-1);
      out.push(render(dir, Object.assign({}, opts, fn(t)))); }
    return out;
  }
  function project(dir, p, elev, yaw){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev,yaw})); return {x:v.sx,y:v.sy}; }

  // the pose, as functions on model points — what anchors() applies so a rider lands where the bake put the saddle
  function poseFns(s){ const P=s.P, dz=dzFn(s);
    const lean=(p)=>{ if(!s.leanDeg) return p; const a=s.leanDeg*DEG, ca=Math.cos(a), sa=Math.sin(a); return [p[0]*ca+p[2]*sa, p[1], -p[0]*sa+p[2]*ca]; };
    let steerXf;
    if(s.body==='quad'){ const a=s.steer*P.barsMax*DEG, ca=Math.cos(a), sa=Math.sin(a); steerXf=(p)=>hingeZ(p,P.stem[0],P.stem[1],ca,sa); }
    else { const u=axisOf(P), Hd=headOf(P), th=s.steer*P.steerMax*DEG; steerXf=(p)=>rotAxis(p,Hd,u,th); }
    const bodyPt=(p)=>lean([p[0],p[1],p[2]+dz(p[1])]);
    return { dz, lean, steerXf, bodyPt, barPt:(p)=>bodyPt(steerXf(p)), wheelPt:lean };
  }
  function anchors(dir, opts){ opts=opts||{}; const s=resolve(opts), e=opts.elev, P=s.P, X=poseFns(s);
    const Pj=(p)=>{ const q=project(dir,p,e,s.yaw); return { x:q.x, y:q.y, m:p.map(v=>+v.toFixed(3)) }; };
    const pegs=P.pegs||P.boards;
    const A={ seat:Pj(X.bodyPt(P.seatRef)), bars:Pj(X.barPt([0,P.barY,P.barZ])),
      gripL:Pj(X.barPt(P.grips[0])), gripR:Pj(X.barPt(P.grips[1])), pegL:Pj(X.bodyPt(pegs[0])), pegR:Pj(X.bodyPt(pegs[1])) };
    if(s.body==='dirtbike'){
      A.wheelF=Pj(X.wheelPt([0,P.axF,P.rF])); A.wheelR=Pj(X.wheelPt([0,P.axR,P.rR]));
      A.standTip=Pj(X.lean(lerp3(P.standStow,P.standTip,s.stand)));
      A.tail=Pj(X.bodyPt([0,-1.06,0.89])); A.lamp=Pj(X.barPt([0,0.59,0.89]));
    } else if(s.body==='trike'){ const Hd=headOf(P);
      A.wheelF=Pj(X.wheelPt([0,P.axF,P.rF])); A.wheelRL=Pj(X.wheelPt([-P.rearX,P.axR,P.rR])); A.wheelRR=Pj(X.wheelPt([P.rearX,P.axR,P.rR]));
      A.tail=Pj(X.bodyPt([0,-0.92,0.58])); A.lamp=Pj(X.barPt([0,Hd[1]+0.14,Hd[2]+0.05]));
      if(s.rackR) A.rackR=Pj(X.bodyPt([0,(P.rackR.y[0]+P.rackR.y[1])/2,P.rackR.z]));
    } else {
      A.wheelFL=Pj(X.wheelPt([-P.wheelX,P.axF,P.r])); A.wheelFR=Pj(X.wheelPt([P.wheelX,P.axF,P.r]));
      A.wheelRL=Pj(X.wheelPt([-P.wheelX,P.axR,P.r])); A.wheelRR=Pj(X.wheelPt([P.wheelX,P.axR,P.r]));
      A.tail=Pj(X.bodyPt([0,-1.00,0.60])); A.lampL=Pj(X.bodyPt([-0.29,0.99,0.59])); A.lampR=Pj(X.bodyPt([0.29,0.99,0.59]));
      if(s.rackF) A.rackF=Pj(X.bodyPt([0,(P.rackF.y[0]+P.rackF.y[1])/2,P.rackF.z]));
      if(s.rackR) A.rackR=Pj(X.bodyPt([0,(P.rackR.y[0]+P.rackR.y[1])/2,P.rackR.z]));
      if(s.hitch) A.hitch=Pj(X.bodyPt(P.hitch)); if(s.winch) A.winch=Pj(X.bodyPt(P.winch));
    }
    return Object.assign(A, { loa:s.B.loa, width:s.B.width, height:s.B.height, wheelbase:s.B.wheelbase, wheels:P.wheels, leanDeg:s.leanDeg });
  }
  function list(){ return Object.keys(BODIES); }

  // ================= GAMEPLAY SIDECAR — generated off SPECS, never typed =================
  // visible_facings: a feature with outward normal n reads at facing d when nx*sin(45d)+ny*cos(45d) < -0.3
  // (the near-side test the rasteriser uses, edge-on excluded). Top faces read at all eight.
  function visibleFacings(n){ const out=[]; for(let d=0;d<8;d++){ const a=d*45*DEG; if(n[0]*Math.sin(a)+n[1]*Math.cos(a) < -0.3) out.push(ORDER[d]); } return out; }
  const ALL8 = ORDER.slice();
  const r3=(v)=>+v.toFixed(3), P3=(p)=>p.map(r3);

  function gameplayGeometry(opts){
    const s=resolve(Object.assign({}, opts||{}, { steer:0, susF:0, susR:0, lean:0, yaw:0, roll:0, stand: (opts&&opts.body)==='dirtbike'?1:0 }));
    const P=s.P, B=s.B, body=s.body, bike=body==='dirtbike', trike=body==='trike', quad=body==='quad';
    const pegs=P.pegs||P.boards, hw=B.width/2;
    const reachL=[r3(-(hw+0.55)), r3(P.seatRef[1]), 0], reachR=[r3(hw+0.55), r3(P.seatRef[1]), 0];
    const G = { label:B.label, kind:B.kind };
    G.BODY = {
      loa_m:B.loa, width_m:B.width, height_m:B.height, wheelbase_m:B.wheelbase, seat_height_m:P.seatZ,
      ground_clearance_m:P.clearance, wheels:P.wheels,
      collider_bbox:{ x:[-hw,hw], y:[P.yMin,P.yMax], z:[0,B.height],
        _note: bike ? 'width is over the GRIPS (0.86); the body itself is 0.38 wide at the shrouds. Baked parked she leans 12deg to the street side and the collider should too — see STAND.world_at_rest.'
             : trike ? 'width is over the one-piece rear fender (1.20); the balloon tires stop at 1.12.'
             : 'width is over the fender aprons (1.28); tires 1.21, racks 0.92. y -1.10 is the hitch ball; -1.025 the tail lamps without it.' },
      mass_kg_estimate:{ value:P.mass, basis:'NOT measured off the rig — no rig geometry carries mass. A gameplay knob, quoted so a load model has a number to start from.' },
      provenance:'SPECS.'+body+' + BODIES (loa = yMax - yMin, wheelbase = axF - axR)' };
    G.SADDLE = {
      _what:'THE BAKE CARRIES NO RIDER. These are the mount points the character rig sits on. Posture is ASTRIDE — legs either side of the machine, hands on the grips, feet on the '+(quad?'boards':'pegs')+'.',
      seat_ref:P3(P.seatRef), seat:{ x:[-(bike?0.13:0.17),(bike?0.13:0.17)], y: bike?[-0.74,0.08] : trike?[-0.72,0.02] : [-0.66,0.02], top_z:P.seatZ,
        _note: bike ? 'one 0.82 m cushion. A pillion is possible in the rig (nothing divides it) and unruled in the game.' : trike ? 'a long flat tractor seat; two up is the class habit and the rig would not object.' : 'rises 0.04 toward the tail. One rider by design; a pillion sits on the rear rack, which is a gameplay call.' },
      grips:{ L:P3(P.grips[0]), R:P3(P.grips[1]), span_m:r3(P.grips[1][0]-P.grips[0][0]), turn_with:'steer', _note:'anchors() applies the steer transform — read grips from there, not from these rest values, when the bars are turned.' },
      [quad?'boards':'pegs']:{ L:P3(pegs[0]), R:P3(pegs[1]), _note: quad ? 'flat rubber footboards 0.26 x 0.60 m at z 0.365 — standing on them is legal geometry.' : 'a 0.10 m peg each side; nothing to stand on beside them.' },
      rider_frame:'every point above rides the suspension (dz(y)) and, on the bike, the lean. anchors(dir,opts) returns them posed; the character rig should take leanDeg from dims() and roll the rider with the machine.',
      mount:{ sides:['street','curb'], preferred: bike?'street':'either', _note: bike ? 'the stand is on the street side (-x); a rider mounts over it and kicks it up. Curb-side mounting is possible and not the habit.' : 'no stand, no preference; the seat is reached from either side.',
        reach_points:{ street:reachL, curb:reachR }, visible_facings:{ street:visibleFacings([-1,0,0]), curb:visibleFacings([1,0,0]) } },
      provenance:'SPECS.'+body+' seatRef / grips / '+(quad?'boards':'pegs')+' — exact; reach points are width/2 + 0.55 m outboard at the seat ref y' };
    G.STEERING = quad ? {
      type:'handlebar over a vertical stem; front pair yaw about their own kingpins', param:'steer', range:[-1,1], sense:'+1 = full LEFT (nose swings toward -x)',
      bars:{ axis:'vertical through the stem at [0, 0.40]', max_deg:P.barsMax },
      wheels:{ geometry:'Ackermann', inner_max_deg:P.steerMax, outer_max_deg:r3(steerAngles(1,P).R), kingpins:[[-P.wheelX,P.axF],[P.wheelX,P.axF]], axis:'vertical through each front wheel centre' },
      turning:{ centre:'on the rear axle line', inner_wheel_radius_m:r3(B.wheelbase/Math.tan(P.steerMax*DEG)), centreline_radius_m:r3(B.wheelbase/Math.tan(P.steerMax*DEG)+P.wheelX), _note:'geometric, at full lock, no slip' },
      provenance:'steerAngles() + SPECS.quad.barsMax/steerMax/wheelX' } : {
      type:'handlebar; the WHOLE front assembly (wheel, fork, fender, lamp, bars) turns about the raked steering head', param:'steer', range:[-1,1], sense:'+1 = full LEFT (nose swings toward -x)',
      axis:{ through:P3(headOf(P)), rake_deg:P.rake, direction:P3(axisOf(P)), _note:'a real raked axis — the front wheel\'s contact point moves with steer, which is why the bars swing over the tank a little' },
      max_deg:P.steerMax, turning:{ centre:'on the rear axle line', radius_m:r3(B.wheelbase/Math.tan(P.steerMax*DEG)), _note:'geometric, at full lock, no lean and no slip'+(bike?' — a real bike turns tighter by leaning, which is LEAN\'s job':'') },
      provenance:'axisOf()/headOf() off SPECS.'+body+'.rake/forkL' };
    G.SUSPENSION = { params:['susF','susR'], range:[-1,1], travel_m:{ front:P.TF, rear:P.TR },
      rule:'the BODY moves, the wheels stay on the ground: dz(y) = -(susF*TF)*t - (susR*TR)*(1-t), t = (y - axR)/(axF - axR), applied to every sprung polygon and to every SADDLE point',
      unsprung:[ 'wheels', bike?'fork guards, swingarm, sprocket':(trike?'fork lowers, front axle':'rear axle, diff, swingarm, A-arms') ],
      _note: trike ? 'REAR TRAVEL IS ZERO. The class has a rigid rear axle — the balloon tires are the springs and no param flexes them. susR is accepted and ignored.' : 'fork travel on the real class is 2-3x this; the rig keeps what reads at 32 px/m.' };
    const wl=[]; const wheelRole=(r,role,pos,w)=>({ id:role, pos:P3(pos), radius_m:r, width_m:w, role:role.replace(/[LR]$/,'') });
    if(bike){ wl.push({ id:'F', pos:P3([0,P.axF,P.rF]), radius_m:P.rF, width_m:P.wF, rim_r_m:P.rimF, role:'steer', spoked:true });
              wl.push({ id:'R', pos:P3([0,P.axR,P.rR]), radius_m:P.rR, width_m:P.wR, rim_r_m:P.rimR, role:'drive', spoked:true, sprocket:'street side, r 0.105' }); }
    else if(trike){ wl.push({ id:'F', pos:P3([0,P.axF,P.rF]), radius_m:P.rF, width_m:P.wF, role:'steer' });
              for(const sx of [-1,1]) wl.push({ id:sx<0?'RL':'RR', pos:P3([sx*P.rearX,P.axR,P.rR]), radius_m:P.rR, width_m:P.wR, role:'drive', side:sx<0?'street':'curb' }); }
    else { for(const sx of [-1,1]) wl.push({ id:sx<0?'FL':'FR', pos:P3([sx*P.wheelX,P.axF,P.r]), radius_m:P.r, width_m:P.wFt, role:'steer+drive', side:sx<0?'street':'curb' });
           for(const sx of [-1,1]) wl.push({ id:sx<0?'RL':'RR', pos:P3([sx*P.wheelX,P.axR,P.r]), radius_m:P.r, width_m:P.wRr, role:'drive', side:sx<0?'street':'curb' }); }
    G.WHEELS = { count:P.wheels, axles_y:[P.axF,P.axR], track_m: bike?0:r3(2*(trike?P.rearX:P.wheelX)), contact_z:0, list:wl,
      roll:{ params:['roll','rollF','rollR'], units:'revolutions of the REAR wheel', distance_per_rev_m:B.distancePerRev,
        _note: bike||trike ? 'the front wheel is a different radius and turns rR/rF times as fast; the rig scales it so both tread the same road.' : 'all four the same radius; a 4x4 with no differential lock modelled — rollF/rollR are offsets, not a drivetrain.' },
      tread:'knobby — the lug pitch is fitted to a whole number of lugs per revolution, so the roll loop closes exactly',
      provenance:'SPECS.'+body+' axF/axR/r*/w* — exact' };
    if(bike){
      G.STAND = { param:'stand', range:[0,1], default:1, side:'street (-x)', hinge:P3(P.standPivot), tip_upright_frame:P3(P.standTip), tip_stowed:P3(P.standStow),
        lean_deg_at_1:P.standLean, world_at_rest:{ lean_deg:-P.standLean, tip:P3(poseFns(s).lean(P.standTip)), seat_ref:P3(poseFns(s).bodyPt(P.seatRef)), _note:'at stand 1 the tip sits on z=0 to 1 mm; the machine is rotated about its contact line so the tires still touch at x=0' },
        rule:'she BAKES PARKED. A dirtbike does not stand upright without a rider: the roll/turn/bounce cues set stand 0, and a game that shows her upright with nobody aboard is showing a bug the rig will not catch.',
        cue:'park — 8 frames, stand 0 -> 1, run reversed to kick it up before riding off', provenance:'SPECS.dirtbike.stand* — exact; world tip = lean(standTip) at 12deg' };
      G.LEAN = { param:'lean', range:[-1,1], max_deg:P.leanMax, sense:'+1 leans 28deg toward the CURB (+x)', axis:'the tyre contact line — world y-axis at x=0, z=0 — so the contact patch never leaves the ground',
        cornering:'lean INTO the turn: steer +1 (left) pairs with lean -1 (street side). The turn cue couples lean = -0.55 * steer and yaw = 14 * steer; the rig does not enforce the coupling.',
        stacks_with:'stand — total = lean*28 - stand*12 degrees; dims().leanDeg publishes the sum', rider:'the rider leans WITH the machine — anchors() already applies it to the saddle points' };
    }
    if(quad||trike){ const racks={};
      if(quad) racks.front={ present_when:{rackF:true}, default:true, platform:{ x:[-P.rackF.x,P.rackF.x], y:P.rackF.y, z:P.rackF.z }, size_m:[r3(2*P.rackF.x), r3(P.rackF.y[1]-P.rackF.y[0])], load_height_m:P.rackF.z, construction:'tube — perimeter, three longitudinals, one cross rail; open between the bars, small items fall through', visible_facings:ALL8 };
      const RR=P.rackR; racks.rear={ present_when:{rackR:true}, default:true, platform:{ x:[-RR.x,RR.x], y:RR.y, z:RR.z }, size_m:[r3(2*RR.x), r3(RR.y[1]-RR.y[0])], load_height_m:RR.z, construction: quad ? 'tube, as the front; the bigger of the two' : 'a small tube frame on the fender — a cooler, a coil of rope, one tote', visible_facings:ALL8 };
      G.CARGO = Object.assign({ _what:'racks only — no bed, no box, no tailgate. Anything not on a rack rides a lap or a hitch-borne trailer.'+(quad?' The seat tail doubles as a pillion in the class; unruled here.':'') }, racks); }
    else G.CARGO = { _no_rack:'nothing on a dirtbike carries cargo. The pillion half of the seat is the only place a second thing goes, and that is a rider.' };
    if(quad) G.TOW = { hitch:{ present_when:{hitch:true}, default:true, ball:P3(P.hitch), ball_r_m:0.026, receiver:{ x:[-0.04,0.04], y:[-1.10,-1.00], z:[0.36,0.44] }, _note:'a 50 mm ball on a receiver stub. The trailer to hang on it is not in this rig.' },
      winch:{ present_when:{winch:true}, default:false, drum_axis:{ from:[-0.13,0.95,0.43], to:[0.13,0.95,0.43] }, drum_r_m:0.045, line_exit:'forward, under the fender nose, through the brush guard', _note:'a recovery point. There is no rope in the rig — the line is the game\'s to draw.', visible_facings:visibleFacings([0,1,0]) } };
    const lamps=[];
    if(bike) lamps.push({ id:'headlamp', present_when:{lamp:true}, default:true, pos:[0,0.59,0.89], aim:'+y, turns with the bars', size_m:[0.16,0.12], alt:'lamp:false swaps it for a white MX number plate at the same station — no light at night', visible_facings:visibleFacings([0,1,0]) });
    else if(trike){ const Hd=headOf(P); lamps.push({ id:'headlamp', pos:P3([0,Hd[1]+0.14,Hd[2]+0.05]), r_m:0.072, aim:'+y, turns with the bars', visible_facings:visibleFacings([0,1,0]) }); }
    else for(const sx of [-1,1]) lamps.push({ id:sx<0?'headlamp_L':'headlamp_R', pos:P3([sx*0.29,0.99,0.59]), size_m:[0.14,0.10], aim:'+y, fixed in the fender nose', visible_facings:visibleFacings([0,1,0]) });
    G.ATTACH = { headlamps:lamps,
      tail_lamp: bike ? { x:[-0.04,0.04], y:-1.07, z:[0.86,0.90] } : trike ? { x:[-0.05,0.05], y:-0.935, z:[0.55,0.60] } : [{ x:[-0.42,-0.30], y:-1.025, z:[0.56,0.64] },{ x:[0.30,0.42], y:-1.025, z:[0.56,0.64] }],
      exhaust_tip: bike ? [0.19,-0.98,0.66] : trike ? [0.30,-0.72,0.70] : [0.30,-1.04,0.52], exhaust_side:'curb (+x)',
      night:'headlamps swap to the glow ramp and spill one pixel onto their neighbours; the tail lamps do not glow' };
    const inter=[ { id:'ride', action:'enter_ride', at:'SADDLE.seat_ref', reach_point:reachL, alt_reach_point:reachR, visible_facings:visibleFacings([-1,0,0]).concat(visibleFacings([1,0,0])), _note: bike?'mount from the street side over the stand, then stand 1 -> 0':'either side' } ];
    if(bike) inter.push({ id:'stand', action:'kick_stand', param:'stand', reach_point:reachL, visible_facings:visibleFacings([-1,0,0]), _note:'a rider does this as part of mounting; published separately so a game can leave her on the stand while someone sits' });
    if(quad) inter.push({ id:'rack_front', action:'cargo', present_when:{rackF:true}, reach_point:[0,P.yMax+0.60,0], visible_facings:visibleFacings([0,1,0]) });
    if(quad||trike) inter.push({ id:'rack_rear', action:'cargo', present_when:{rackR:true}, reach_point:[0,P.yMin-0.60,0], visible_facings:visibleFacings([0,-1,0]) });
    if(quad){ inter.push({ id:'hitch', action:'couple', present_when:{hitch:true}, reach_point:[0,P.yMin-0.60,0], visible_facings:visibleFacings([0,-1,0]) });
      inter.push({ id:'winch', action:'recover', present_when:{winch:true}, reach_point:[0,P.yMax+0.60,0], visible_facings:visibleFacings([0,1,0]) }); }
    G.INTERACT = inter;
    G.YAW = { param:'yaw', units:'degrees', range:[-45,45], what:'heading between the eight facings. A RENDER param: the model turns about z under the fixed key before projection, so the shading is rebaked, not rotated.',
      frame:'every metre in this file is in the machine frame and does not move with yaw', pivot:'unaffected; the origin projects to the published pivot at any yaw' };
    G._excluded = Object.assign({
      THRESHOLD:'no doors, no sill, no step. The way in is a leg over the saddle — SADDLE.mount carries the reach points.',
      cab_roof_bed:'none. Nothing on these three is sat inside or stood on except '+(quad?'the footboards':'the pegs')+'.',
      levers_pedals:'clutch and brake levers, shifter and brake pedal are 1-2 px at this scale and left out. The bars carry grips only.',
      mirrors_plate_decals:'none. A number plate is the MX plate on the bike (lamp:false), not a registration.',
      float:'these do not swim. No FLOAT section; the Otter is the amphibian.',
      rider:'not in the bake, by design — see SADDLE.' },
      bike ? { chain:'the sprocket is modelled, the chain is not — at 32 px/m it is a one-pixel line the swingarm already draws.', kickstart:'not modelled; the stand is the only lever on her.' } :
      trike ? { rear_suspension:'none on the class — recorded in SUSPENSION, not an omission.', reverse:'the class has none; nothing in the rig implies one either way.' } :
      { differential_lock:'not a rig concept. rollF/rollR are offsets for a game to drive.', cargo_box:'no box, no bed — racks only, see CARGO.' });
    G._confirm = Object.assign({
      reach_points:'ground-level spots derived from the seat ref, width/2 + 0.55 m outboard; NOT tested against terrain, a wall or another vehicle.',
      two_up:'every seat here is one undivided cushion. How many ride is a gameplay call the rig cannot make.',
      ramp_budget:'11 ramps at most on any build of any body — the fleet cap is 16.' },
      bike ? { parked_state:'the bake default is stand 1 (parked, leaning). A game that pools sprites should bake the ridden state from the roll cue, not from the default.' } :
      quad ? { footboard_standing:'0.26 x 0.60 m of flat rubber per side at z 0.365. Standing on the boards to ride is legal geometry and unruled.' } :
      { fender_load:'the one-piece rear fender is 1.20 m wide plastic. Whether a crate rides on it beside the rack is unruled; the rack is the only published load surface.' });
    return G;
  }
  function gameplayAll(){
    const bodies={}; for(const k of Object.keys(SPECS)) bodies[k]=gameplayGeometry({ body:k });
    return {
      rig:'atvIsoRig.js', exportSymbol:'AtvIso', variant:'atvPack-x3', kind:'saddle_vehicles',
      label:'ATV Set: Enduro 250, Trike 200, Utility Quad 4x4',
      generated:'generated by AtvIso.gameplayAll() off SPECS — the same table the bake reads. Regenerate from ATV Pack Iso.dc.html; never edit a number here.',
      frame:{ units:'metres', scale_px_per_m:PX, origin:'ground-centre of the wheelbase (z=0 is the road)', axes:'+x curb side, +y nose, +z up', heading_independent:true,
        at_rest:'every value is at steer 0, susF/susR 0, lean 0, yaw 0. The dirtbike\'s at-rest is stand 1 (parked): see its STAND.world_at_rest for the leaned points.',
        pivot_px:{ x:cx, y:groundY, cell:[W,H], _note:'one cell for all three bodies; model z=0 projects to the pivot row in all 8 facings at every pose' },
        facings:{ order:ORDER.slice(), _note:'dir 0 (N) shows the TAIL: the machine points away. dir 4 (S) shows the nose. Street side (-x) reads at NE E SE; curb side (+x) at SW W NW.' } },
      bodies };
  }

  root.AtvIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV, order:ORDER.slice(),
    BODY, TRIM, IRON, GALV, RUBBER, CHROME, CLOTH, GLASSD, GLASSN, KEY,
    SPECS, BODIES, PRESETS, CUES, CYCLIC, cuesFor, fittingsFor,
    steer:{ quad:{ barsMaxDeg:SPECS.quad.barsMax, innerMaxDeg:SPECS.quad.steerMax, outerMaxDeg:+steerAngles(1,SPECS.quad).R.toFixed(2) }, dirtbike:{ maxDeg:SPECS.dirtbike.steerMax, rakeDeg:SPECS.dirtbike.rake }, trike:{ maxDeg:SPECS.trike.steerMax, rakeDeg:SPECS.trike.rake }, angles:steerAngles },
    list, dims, resolve, render, frames, anchors, project, gameplayGeometry, gameplayAll, RIG_URL:'Art/atvIsoRig.js' };
})(typeof globalThis!=='undefined'?globalThis:window);
