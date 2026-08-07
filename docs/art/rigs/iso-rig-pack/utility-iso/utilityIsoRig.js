/* Hidden Harbours — parametric ISO OUTSIDE-UTILITY rig (ADR-0006 bake pipeline, SAME turntable +
   camera + shading as interiorPropRig.js / houseIsoRig.js / the fleet). The SERVICES that hang off a
   village: power, light, water, sewer, fuel & telecom. Each piece is one parametric 3D model baked to
   pixel sheets through the SHARED 3/4 camera: 45deg steps, elev 40deg, flat-facet shading from the
   fixed upper-LEFT key, z-buffered, ordered dither, per-face uv texture, depth-edge darkening,
   NO AA. The 1px keyline is RETIRED by default per ADR 0031 and reachable with `{outline:true}`.
   32 px = 1 m. All 8 facings fall out of one model, so a pole line rotates in lockstep
   with the wharf, the houses and the villagers walking under it.

   PROP ORIGIN: ground-centre of the footprint (== the house/room-rig pivot), so UtilityIso.render(name,dir)
   drops straight onto a terrain point with no offset maths. Front face is -Y; wall-mounted pieces
   (service mast, fill pipes) sit with their backboard toward +Y so the wall is behind them.

   CATALOGUE (name -> spec):
     power   powerPole · hFrame · padTransformer · switchgear · pedestal · genset · serviceMast · meterBank · guyAnchor
     light   yardLight · streetLamp · floodMast · beacon
     water   hydrant · yardHydrant · standpipe · handPump · wellCap · curbStop · valveVault · waterTank · cistern
     sewer   manhole · cleanout · ventStack · septicLids · catchBasin · trenchDrain · liftStation · outfall · culvert
     fuel    oilTank · fillPipes · propaneTank · bulkTank · fuelPump · drumRack · coalBin
     telecom telecomPed · crossBox · alarmBox · radioMast
   Each reads a resolved spec:
     paint:BODY key|null   metal:'galv'|'alum'|'iron'   variant:int   len:0..1 (height/length runs)
     weather:0..1 (grime + rust on metals)   gravel:bool (apron/pad skirt)   night:bool (cool moonlight)
   TIES: ties(name,opts) returns wire/lamp/drop attach points in METRES; anchors(name,dir,opts) returns
   the same points baked to cell pixels, so the compositor can sling catenary spans pole → pole → house.
   Exposes globalThis.UtilityIso = { W,H,PX,pivot,order,defaultElev, POLE,IRON,GALV,ALUM,CONCRETE,COPPER,
     BRASS,GLASSINS,PORC,GRAVEL,ASPHALT,BODY,KEY, PROPS,CATS, list(), footprint(), ties(), anchors(),
     render(name,dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 440, H = 620, cx = 220, groundY = 520;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;

  // ---- palettes (harbour master ramps, shared with the houses & fleet) ----
  const BODY = {
    greyShingle: ['#4c463f','#5d564c','#6f665a','#82786a','#968b7b','#a99d8c'],
    white:       ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    cream:       ['#8a6f3c','#a6884b','#c2a35f','#d8bd7c','#e9d59d','#f5e7c1'],
    red:         ['#4a130f','#671b14','#88271c','#a33124','#bd4230','#d25a42'],
    sage:        ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'],
    blue:        ['#33454a','#43585d','#556d72','#6a848a','#849ea3','#a3b9bd'],
    gold:        ['#5e4a12','#7c6119','#987a26','#b39440','#c8ab5e','#dbc182'],
    teal:        ['#123a3a','#1b4d4b','#26635e','#357b73','#4d968b','#6cb1a4'],
    rustOrange:  ['#4a2410','#6a3514','#8c481a','#a85f27','#c07a3a','#d49657'],
    plum:        ['#2e2333','#3f3047','#523f5d','#664f73','#7d648b','#9079a1'],
  };
  const POLE     = ['#33291f','#42342a','#523f33','#63503f','#75604c','#8a7460']; // creosoted fir pole
  const WOOD     = ['#553d23','#6b4e30','#7f603b','#957749','#ab8d5f','#c1a578']; // sawn timber
  const IRON     = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970']; // cast iron
  const GALV     = ['#565b5f','#6d7276','#868b8f','#a0a5a8','#bbbfc1','#d6d9da']; // galvanised
  const ALUM     = ['#3f4448','#525a5e','#687175','#7f888c','#98a0a3','#b2b9bb']; // painted steel / alum
  const CONCRETE = ['#55564f','#686962','#7c7d74','#919287','#a6a79b','#bbbcae'];
  const COPPER   = ['#4a2413','#68361c','#8a4c26','#a86538','#c4834f','#dba86f'];
  const BRASS    = ['#59410f','#795819','#977326','#b28e3b','#c8a757','#dcc17d'];
  const GLASSINS = ['#1d3c42','#2b585c','#3c7576','#529b92','#77bcaf','#a2d9cc']; // aqua glass insulator
  const PORC     = ['#3a251a','#4e3222','#65422c','#7d573b','#96704f','#ad8a66']; // brown porcelain
  const GRAVEL   = ['#3b3a33','#4b493f','#5c5a4d','#6f6c5c','#83806e','#979382'];
  const ASPHALT  = ['#1e2124','#272b2f','#33383c','#41474b','#51585c','#646b6f'];
  const GLASSD   = ['#7d9ea6','#a1c2c6','#cfe6e8'], GLASSN = ['#141d2b','#233247','#3d5570'];
  const GLOW     = ['#7a5a18','#c09a2c','#efd06a','#fdf0b6'];
  const KEY      = '#1a1c22';
  // ADR 0031 — the outline is retired from world art; the silhouette is carried by the form's own
  // dark side. The ring is not deleted: it is gated OFF by default and reachable with
  // `{outline:true}`, mirroring the engine's own `GameConfig.HullKeylineFlood` so the owner keeps a
  // one-flag A/B. Depth-edge darkening (the EDGE pass in post()) is the separate INTERIOR rule
  // ADR 0031 §2 keeps untouched — do not confuse the two. This family pays the most for the ring
  // of any in the pack: it is 25.6 % of every ink pixel on a `powerPole` (perimeter-cost law,
  // ../../outline-interaction-language.md, landing on filamentary subjects).
  const KEYLINE_DEFAULT = false;

  // ---- shading (identical recipe to interiorPropRig) ----
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

  // ---- face builders (outward-normal winding) ----
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }
  function wall(out, x0,y0,x1,y1, z0,z1, mat, tex, b){ const L=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]], mat, b||0, 0, [[0,z0],[L,z0],[L,z1],[0,z1]], tex)); }
  function slab(out, pts, z, mat, b, tex){ const uv=tex?pts.map(p=>[p[0],p[1]]):null;
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0, uv, tex)); }
  function box(out, x0,x1,y0,y1,z0,z1, mat, b, tex, noTop){
    wall(out, x0,y0, x1,y0, z0,z1, mat, tex, b);
    wall(out, x1,y1, x0,y1, z0,z1, mat, tex, b);
    wall(out, x1,y0, x1,y1, z0,z1, mat, tex, b);
    wall(out, x0,y1, x0,y0, z0,z1, mat, tex, b);
    if(!noTop) slab(out, [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, (b||0)+0.28, tex);
  }
  const sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]];
  const crs=(a,b)=>[a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]];
  const nrm=(v)=>{ const m=Math.hypot(v[0],v[1],v[2])||1; return [v[0]/m,v[1]/m,v[2]/m]; };
  // generic round/faceted TUBE between two 3D points — pipes, poles, tanks, arms, guys, bottles.
  // ring basis is right-handed with the axis (e1 x e2 = axis) so every side face normal points outward.
  function tube(out, p0, p1, r, n, mat, b, tex, caps){
    const u=nrm(sub(p1,p0)); const a=Math.abs(u[2])>0.9?[1,0,0]:[0,0,1];
    const e1=nrm(crs(a,u)), e2=nrm(crs(u,e1)); n=n||12; b=b||0;
    const L=Math.hypot(p1[0]-p0[0],p1[1]-p0[1],p1[2]-p0[2]);
    const P=(i,end)=>{ const th=(i/n)*Math.PI*2, c=Math.cos(th)*r, s=Math.sin(th)*r, q=end?p1:p0;
      return [q[0]+e1[0]*c+e2[0]*s, q[1]+e1[1]*c+e2[1]*s, q[2]+e1[2]*c+e2[2]*s]; };
    const arc=(2*Math.PI*r)/n;
    for(let i=0;i<n;i++){ const j=(i+1)%n;
      out.push(F([P(i,0),P(j,0),P(j,1),P(i,1)], mat, b, 0, tex?[[i*arc,0],[(i+1)*arc,0],[(i+1)*arc,L],[i*arc,L]]:null, tex)); }
    if(caps!==false){ const top=[],bot=[];
      for(let i=0;i<n;i++){ top.push(P(i,1)); bot.push(P(n-1-i,0)); }
      out.push(F(top, mat, b+0.26, 0, null, null));
      out.push(F(bot, mat, b-0.5, 0, null, null)); }
  }
  const pipe  = (out,x,y,r,z0,z1,mat,b,n,tex)=> tube(out,[x,y,z0],[x,y,z1],r,n||12,mat,b,tex);
  const prismT= (out,x,y,r,z0,z1,n,mat,b,texTop)=>{ tube(out,[x,y,z0],[x,y,z1],r,n,mat,b,null,false);
    const p=[]; for(let i=0;i<n;i++){ const t=(i/n)*Math.PI*2; p.push([x+Math.cos(t)*r, y-Math.sin(t)*r]); }
    slab(out, p, z1, mat, (b||0)+0.24, texTop||null); };
  const beam  = (out,p0,p1,r,mat,b,n)=> tube(out,p0,p1,r,n||4,mat,b);
  function decalY(out, yv, ny, xs,xe, z0,z1, mat, b, tex, flat, db){ const e=0.02*ny, uw=xe-xs, uh=z1-z0;
    const P = ny>0 ? [[xs,yv+e,z0],[xe,yv+e,z0],[xe,yv+e,z1],[xs,yv+e,z1]] : [[xe,yv+e,z0],[xs,yv+e,z0],[xs,yv+e,z1],[xe,yv+e,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat)); }
  function decalX(out, xv, nx, ys,ye, z0,z1, mat, b, tex, flat, db){ const e=0.02*nx, uw=ye-ys, uh=z1-z0;
    const P = nx>0 ? [[xv+e,ye,z0],[xv+e,ys,z0],[xv+e,ys,z1],[xv+e,ye,z1]] : [[xv+e,ys,z0],[xv+e,ye,z0],[xv+e,ye,z1],[xv+e,ys,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat)); }
  const dY=(out,y,n,a,b2,z0,z1,mat,bias,db)=>decalY(out,y,n,a,b2,z0,z1,mat,bias,null,true,db==null?0.06:db);
  const dX=(out,x,n,a,b2,z0,z1,mat,bias,db)=>decalX(out,x,n,a,b2,z0,z1,mat,bias,null,true,db==null?0.06:db);
  // concrete pad with a chamfered lip, the base of half this catalogue
  function pad(out, w,d,h, mat, b){ const m=Math.min(0.05,h*0.4);
    box(out,-w/2,w/2,-d/2,d/2, 0,h-m, mat, b||0, null, true);
    box(out,-w/2+m,w/2-m,-d/2+m,d/2-m, h-m,h, mat, (b||0)+0.2); }
  function apron(out, r, mat){ prismT(out,0,0,r, 0,0.035, 16, mat, -0.1, gravelTex()); }
  function chamferTop(out, x0,x1,y0,y1, z, ins, drop, mat, b){ b=b||0;
    slab(out, [[x0+ins,y0+ins],[x1-ins,y0+ins],[x1-ins,y1-ins],[x0+ins,y1-ins]], z, mat, b+0.34);
    out.push(F([[x0,y0,z-drop],[x1,y0,z-drop],[x1-ins,y0+ins,z],[x0+ins,y0+ins,z]], mat, b+0.06));
    out.push(F([[x1,y1,z-drop],[x0,y1,z-drop],[x0+ins,y1-ins,z],[x1-ins,y1-ins,z]], mat, b+0.18));
    out.push(F([[x1,y0,z-drop],[x1,y1,z-drop],[x1-ins,y1-ins,z],[x1-ins,y0+ins,z]], mat, b+0.12));
    out.push(F([[x0,y1,z-drop],[x0,y0,z-drop],[x0+ins,y0+ins,z],[x0+ins,y1-ins,z]], mat, b+0.02)); }

  // ---- textures ----
  function gravelTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*13)|0, Math.floor(v*13)|0); return h<0.3?-1:(h>0.86?1:0); }; }
  function corrTex(p){ p=p||0.13; return (u,v)=>{ const f=((v%p)+p)%p; return f<p*0.42?-1:(f>p*0.86?1:0); }; }
  function waffleTex(){ const c=0.085; return (u,v)=>{ const fu=((u%c)+c)%c, fv=((v%c)+c)%c;
    return (fu<0.018||fv<0.018)?-1:1; }; }
  function grainTex(p){ p=p||0.3; return (u,v)=>{ const f=((v%p)+p)%p; if(f<0.03) return -2;
    return hash2(Math.floor(v/p)|0, Math.floor(u*3)|0)<0.5?0:-1; }; }
  function poleTex(){ const p=0.105; return (u,v)=>{ const f=((u%p)+p)%p;
    if(f<0.022) return -1;
    return hash2(Math.floor(u/p)|0, Math.floor(v/1.9)|0)<0.34?-1:0; }; }
  function louvreTex(){ const c=0.09; return (u,v)=>{ const f=((v%c)+c)%c; return f<c*0.45?-2:0; }; }

  // ================= CATALOGUE =================
  const CATS = ['power','light','water','sewer','fuel','telecom'];

  // ---------------- POWER ----------------
  function insulator(out, x,y, z, mat){                     // pin + skirted bell
    pipe(out, x,y, 0.026, z, z+0.085, 'iron', 0.1, 8);
    prismT(out, x,y, 0.088, z+0.085, z+0.135, 10, mat, 0.1);
    prismT(out, x,y, 0.062, z+0.135, z+0.20, 10, mat, 0.25);
  }
  function pPowerPole(out,s){
    const ht = 7.4 + (s.len||0)*4.2, tr = s.variant===1, lamp = s.variant===2;
    pipe(out, 0,0, 0.155, 0, ht*0.34, 'pole', 0, 12, poleTex());          // butt
    pipe(out, 0,0, 0.135, ht*0.33, ht, 'pole', 0.05, 12, poleTex());      // upper shaft
    prismT(out, 0,0, 0.145, ht, ht+0.03, 12, 'pole', 0.3);                // sawn top
    const armZ = ht-0.62;
    box(out, -1.24,1.24, -0.08,0.08, armZ, armZ+0.15, 'pole', 0.42, poleTex());        // crossarm
    for(const sx of [-1,1]) beam(out,[sx*0.86,0.0,armZ],[sx*0.14,0,armZ-0.62], 0.034,'iron',-0.5);
    for(const x of [-1.02,0,1.02]) insulator(out, x,0, armZ+0.15, 'glassins');
    // secondary rack + a spool of service wire lower down (−Y, road side)
    box(out, -0.34,0.34, -0.22,-0.14, ht*0.62, ht*0.62+0.09, 'alum', 0.1);
    for(const x of [-0.24,0,0.24]) prismT(out, x,-0.18, 0.055, ht*0.62+0.09, ht*0.62+0.17, 8, 'porc', 0.2);
    for(let i=0;i<2;i++){ const z=1.9+i*0.42; dY(out,-0.135,-1, -0.05,0.05, z,z+0.028,'iron',0.5); } // climbing bolts
    dY(out,-0.135,-1, -0.07,0.07, 2.9,3.06,'galv',0.6);                    // pole tag
    beam(out,[0,-0.13,0.02],[0,-0.13,armZ-0.9], 0.014,'copper',-0.3);      // ground wire down the face
    if(tr){                                                                 // pole-can transformer, +Y
      const tz = armZ-2.35;
      pipe(out, 0,0.46, 0.33, tz, tz+1.0, 'alum', 0.05, 14);
      prismT(out, 0,0.46, 0.35, tz+1.0, tz+1.06, 14, 'alum', 0.3);
      for(const x of [-0.15,0.15]) prismT(out, x,0.46, 0.075, tz+1.06, tz+1.26, 8, 'porc', 0.25);
      prismT(out, 0,0.20, 0.05, tz+1.06, tz+1.30, 8, 'porc', 0.2);          // arrester
      for(const z of [tz+0.15, tz+0.82]) box(out, -0.16,0.16, 0.10,0.16, z,z+0.07,'iron',0.1);
      for(const z of [tz+0.24, tz+0.58]) tube(out,[0,0.13,z],[0,0.13,z+0.02],0.36,14,'alum',-0.2,null,false);
      beam(out,[-0.05,0.46,tz],[-0.05,0.46,tz-0.5], 0.012,'copper',-0.3);
    }
    if(lamp){                                                               // gooseneck yard lamp on the pole
      const lz = ht-1.5;
      beam(out,[0.1,0,lz],[1.05,0,lz+0.42], 0.045,'alum',0.1);
      beam(out,[1.05,0,lz+0.42],[1.62,0,lz+0.34], 0.045,'alum',0.1);
      box(out, 1.44,2.0, -0.16,0.16, lz+0.2,lz+0.36, 'alum', 0.15);
      slab(out, [[1.5,-0.13],[1.94,-0.13],[1.94,0.13],[1.5,0.13]], lz+0.2, 'glow', 0, null);
    }
    if(s.guy!==false){                                                      // down guy, +Y so spans stay clear
      beam(out,[0.06,0.12,ht-1.15],[0.04,2.5,0.06], 0.026,'iron',-0.55);
      pipe(out, 0,2.55, 0.045, 0, 0.3, 'galv', 0.1, 8);
    }
  }
  function pServiceMast(out,s){                                             // meter + weatherhead on a wall board
    const mh = 4.0 + (s.len||0)*1.4, by = 0.24;
    box(out, -0.46,0.46, by-0.06,by, 0.12,1.72, 'wood', 0.05, grainTex(0.26)); // backboard
    pad(out, 0.9,0.5, 0.12, 'concrete', 0);
    pipe(out, 0,0.06, 0.145, 1.02, 1.34, 'alum', 0.05, 14);                 // meter socket
    prismT(out, 0,0.02, 0.125, 1.34, 1.50, 14, 'glass', 0.1);               // meter glass
    prismT(out, 0,0.02, 0.135, 1.50, 1.55, 14, 'alum', 0.3);
    box(out, -0.17,0.17, by-0.22,by-0.06, 0.46,0.92, 'alum', 0.02);         // disconnect
    dY(out,by-0.22,-1, -0.12,0.12, 0.54,0.84,'alum',-1.2);
    dY(out,by-0.22,-1, -0.02,0.06, 0.62,0.70,'brass',0.8, 0.07);            // handle
    pipe(out, 0,0.10, 0.05, 1.55, mh-0.2, 'galv', 0.08, 10);                // service riser
    box(out, -0.11,0.11, -0.01,0.21, mh-0.2,mh-0.04, 'galv', 0.12);         // weatherhead
    out.push(F([[-0.11,-0.01,mh-0.04],[0.11,-0.01,mh-0.04],[0.11,0.21,mh+0.06],[-0.11,0.21,mh+0.06]],'galv',0.3));
    for(const z of [1.9,2.9]) box(out, -0.07,0.07, 0.06,0.15, z,z+0.05,'galv',0.2);  // pipe straps
    beam(out,[0.30,0.14,1.02],[0.30,0.14,0.0], 0.012,'copper',-0.2);        // ground conductor
    pipe(out, 0.30,0.14, 0.022, 0, 0.06, 'copper', 0.1, 8);                 // rod clamp
    if(s.gravel) slab(out, [[-0.5,-0.34],[0.5,-0.34],[0.5,by],[-0.5,by]], 0.008,'gravel',-0.15, gravelTex());
  }
  function pPedestal(out,s){
    pad(out, 0.78,0.62, 0.14, 'concrete', 0);
    box(out, -0.19,0.19, -0.14,0.14, 0.14,0.86, 'body', 0);
    out.push(F([[-0.21,-0.16,0.86],[0.21,-0.16,0.86],[0.21,0.16,0.99],[-0.21,0.16,0.99]],'body',0.3));  // sloped hood
    box(out, -0.21,0.21, -0.17,-0.14, 0.82,0.88, 'body', 0.2);
    dY(out,-0.14,-1, -0.14,0.14, 0.22,0.74,'body',-1.3);                    // door reveal
    dY(out,-0.14,-1, -0.03,0.03, 0.44,0.52,'brass',0.9, 0.08);              // latch
    dY(out,-0.14,-1, -0.10,0.10, 0.76,0.82,'gold',0.7, 0.07);               // warning plate
    dX(out, 0.19,1, -0.10,0.10, 0.24,0.62,'body',-0.9);                     // vent panel
    if(s.gravel) slab(out, [[-0.62,-0.5],[0.62,-0.5],[0.62,0.5],[-0.62,0.5]], 0.01,'gravel',-0.15, gravelTex());
  }
  function pPadTransformer(out,s){
    const w = 1.1 + (s.len||0)*0.5;
    pad(out, w+0.75, 1.55, 0.2, 'concrete', 0);
    box(out, -w/2,w/2, -0.44,0.44, 0.2,1.16, 'body', 0);
    chamferTop(out, -w/2-0.03,w/2+0.03, -0.47,0.47, 1.28, 0.07, 0.1, 'body', 0.2);
    for(const nx of [-1,1]) for(let i=0;i<5;i++){ const y=-0.32+i*0.16;      // cooling fins
      dX(out, nx*w/2, nx, y,y+0.10, 0.34,1.0,'body', nx>0?0.55:-0.75, 0.05); }
    dY(out,-0.44,-1, -w/2+0.07,-0.02, 0.28,1.06,'body',-1.2);                // twin doors
    dY(out,-0.44,-1, 0.02,w/2-0.07, 0.28,1.06,'body',-1.2);
    dY(out,-0.44,-1, -0.05,0.05, 0.6,0.74,'iron',0.8, 0.08);                 // hasp + padlock
    dY(out,-0.44,-1, w/2-0.34,w/2-0.12, 1.1,1.24,'gold',0.75, 0.07);         // warning plate
    for(const x of [-w*0.26, w*0.26]) prismT(out, x,0.16, 0.115, 1.28,1.46, 10, 'porc', 0.25);
    prismT(out, 0,-0.2, 0.05, 1.28,1.4, 8, 'copper', 0.1);
    if(s.gravel) slab(out, [[-(w/2+0.62),-0.94],[w/2+0.62,-0.94],[w/2+0.62,0.94],[-(w/2+0.62),0.94]], 0.012,'gravel',-0.15, gravelTex());
  }
  function pGenset(out,s){
    const w = 1.35 + (s.len||0)*0.7;
    pad(out, w+0.4, 1.05, 0.16, 'concrete', 0);
    box(out, -w/2,w/2, -0.36,0.36, 0.16,0.94, 'body', 0);
    chamferTop(out, -w/2-0.02,w/2+0.02, -0.38,0.38, 1.02, 0.06, 0.08, 'body', 0.2);
    for(const nx of [-1,1]) dX(out, nx*w/2, nx, -0.28,0.28, 0.3,0.8,'body', nx>0?0.4:-0.9, 0.05, louvreTex());
    dY(out,-0.36,-1, -w/2+0.08,-0.06, 0.26,0.86,'body',-1.1);                // access door
    dY(out,-0.36,-1, 0.1,w/2-0.1, 0.5,0.84,'body',-0.6);                     // control panel
    dY(out,-0.36,-1, 0.14,0.2, 0.72,0.78,'gold',0.9, 0.08);                  // pilot lamp
    pipe(out, w/2-0.22,0.28, 0.055, 1.02,1.34, 'iron', 0.1, 10);             // exhaust
    prismT(out, w/2-0.22,0.28, 0.075, 1.34,1.4, 10,'iron',0.25);
    beam(out,[-w/2,0.3,0.34],[-w/2-0.34,0.3,0.34], 0.022,'copper',0.1);      // fuel line stub
    if(s.gravel) slab(out, [[-(w/2+0.4),-0.72],[w/2+0.4,-0.72],[w/2+0.4,0.72],[-(w/2+0.4),0.72]], 0.012,'gravel',-0.15, gravelTex());
  }
  function pHFrame(out,s){                                                  // two-pole line structure
    const ht = 6.8 + (s.len||0)*3.6, sp = 1.15, a1 = ht-0.55, a2 = ht-1.62;
    for(const sx of [-1,1]){
      pipe(out, sx*sp,0, 0.15, 0, ht*0.36, 'pole', 0, 12, poleTex());
      pipe(out, sx*sp,0, 0.13, ht*0.35, ht, 'pole', 0.05, 12, poleTex());
      prismT(out, sx*sp,0, 0.14, ht, ht+0.03, 12, 'pole', 0.3);
    }
    for(const az of [a1,a2]) box(out, -sp-0.46,sp+0.46, -0.09,0.09, az,az+0.16, 'pole', 0.42, poleTex());
    beam(out,[-sp+0.06,0.13,a2+0.02],[sp-0.06,0.13,a1+0.02], 0.042,'pole',0.08);   // sway brace
    for(const sx of [-1,1]) beam(out,[sx*(sp+0.42),0,a1],[sx*sp,0,a1-0.66], 0.032,'iron',-0.5);
    for(const x of [-sp-0.26,-0.4,0.4,sp+0.26]) insulator(out, x,0, a1+0.16, 'glassins');
    for(const x of [-sp+0.3,0,sp-0.3]) insulator(out, x,0, a2+0.16, 'glassins');
    box(out, -sp-0.3,-sp+0.3, -0.26,-0.16, a2-0.52,a2-0.44, 'alum', 0.15);        // cutout rack
    for(const x of [-sp-0.2,-sp,-sp+0.2]){ prismT(out, x,-0.21, 0.05, a2-0.9,a2-0.52, 8, 'porc', 0.2);
      prismT(out, x,-0.21, 0.062, a2-0.52,a2-0.44, 8, 'iron', 0.3); }
    beam(out,[sp,-0.13,0.02],[sp,-0.13,a2-0.3], 0.014,'copper',-0.3);
    for(let i=0;i<2;i++){ const z=1.9+i*0.42; dY(out,-0.13,-1, -sp-0.05,-sp+0.05, z,z+0.028,'iron',0.5); }
    if(s.guy!==false) for(const sx of [-1,1]){
      beam(out,[sx*sp,0.12,ht-1.2],[sx*(sp+0.5),2.4,0.06], 0.026,'iron',-0.55);
      pipe(out, sx*(sp+0.5),2.45, 0.045, 0, 0.3, 'galv', 0.1, 8); }
    if(s.gravel) slab(out, [[-sp-0.7,-0.7],[sp+0.7,-0.7],[sp+0.7,0.7],[-sp-0.7,0.7]], 0.012,'gravel',-0.15, gravelTex());
  }
  function pSwitchgear(out,s){                                              // pad-mounted switch cabinet
    const w = 1.5 + (s.len||0)*0.7;
    pad(out, w+0.7, 1.7, 0.22, 'concrete', 0);
    box(out, -w/2,w/2, -0.5,0.5, 0.22,1.42, 'body', 0);
    chamferTop(out, -w/2-0.03,w/2+0.03, -0.53,0.53, 1.54, 0.08, 0.11, 'body', 0.2);
    for(const sy of [-1,1]){
      dY(out, sy*0.5, sy, -w/2+0.08,-0.03, 0.34,1.3,'body', sy>0?-0.7:-1.2);
      dY(out, sy*0.5, sy, 0.03,w/2-0.08, 0.34,1.3,'body', sy>0?-0.7:-1.2); }
    dY(out,-0.5,-1, -0.06,0.06, 0.7,0.88,'iron',0.8, 0.08);                 // hasp
    dY(out,-0.5,-1, w/2-0.42,w/2-0.14, 1.34,1.48,'gold',0.75, 0.07);        // warning plate
    for(const sx of [-1,1]) decalX(out, sx*w/2, sx, -0.36,0.36, 0.5,1.2,'body', sx>0?0.45:-0.85, louvreTex(), true, 0.05);
    for(const x of [-w*0.28, 0, w*0.28]) prismT(out, x,0.2, 0.1, 1.54,1.7, 10, 'porc', 0.25);
    for(const sx of [-1,1]) prismT(out, sx*(w/2+0.2), 0.4, 0.05, 0.22,0.46, 8, 'galv', 0.1);
    if(s.gravel) slab(out, [[-(w/2+0.6),-1.0],[w/2+0.6,-1.0],[w/2+0.6,1.0],[-(w/2+0.6),1.0]], 0.012,'gravel',-0.15, gravelTex());
  }
  function pMeterBank(out,s){                                               // gang of meters on a strut rack
    const n = s.variant===1?5:3, bw = 0.48+(n-1)*0.43;
    pad(out, bw+0.3, 0.46, 0.12, 'concrete', 0);
    for(const sx of [-1,1]) pipe(out, sx*(bw/2-0.05), 0.09, 0.055, 0.1, 1.94, 'galv', 0.05, 10);
    for(const z of [0.9, 1.66]) box(out, -bw/2,bw/2, 0.03,0.11, z,z+0.09, 'galv', 0.2);
    box(out, -bw/2+0.02,bw/2-0.02, -0.08,0.06, 0.98,1.68, 'alum', 0.02);
    for(let i=0;i<n;i++){ const x=-bw/2+0.24+i*0.43;
      pipe(out, x,-0.02, 0.135, 1.12, 1.44, 'alum', 0.06, 14);
      prismT(out, x,-0.06, 0.115, 1.44,1.58, 14, 'glass', 0.12);
      prismT(out, x,-0.06, 0.125, 1.58,1.63, 14, 'alum', 0.3);
      pipe(out, x,0.02, 0.036, 0.14, 1.1, 'galv', 0.05, 8); }
    box(out, -bw/2+0.06,bw/2-0.06, -0.06,0.04, 0.5,0.68,'alum',0.05);       // gutter trough
    dY(out,-0.06,-1, -bw/2+0.14,bw/2-0.14, 0.54,0.64,'alum',-0.9);
    beam(out,[bw/2+0.02,0.09,1.9],[bw/2+0.02,0.09,0.02], 0.012,'copper',-0.2);
    if(s.gravel) slab(out, [[-(bw/2+0.24),-0.34],[bw/2+0.24,-0.34],[bw/2+0.24,0.3],[-(bw/2+0.24),0.3]], 0.01,'gravel',-0.15, gravelTex());
  }
  function pGuyAnchor(out,s){                                               // anchor rod, turnbuckle & guard
    const g = 2.2 + (s.len||0)*1.6;
    prismT(out, 0,0, 0.34, 0,0.05, 12, 'gravel', -0.1, gravelTex());
    const p0=[0,0.06,0.02], p1=[-g*0.34,-g*0.3,g];
    const L=(t)=>[p0[0]+(p1[0]-p0[0])*t, p0[1]+(p1[1]-p0[1])*t, p0[2]+(p1[2]-p0[2])*t];
    tube(out, p0, p1, 0.026, 6, 'iron', 0.0);
    tube(out, L(0.36), L(0.5), 0.055, 8, 'iron', 0.2);                      // turnbuckle
    tube(out, p0, L(0.3), 0.085, 8, 'wood', 0.1, grainTex(0.2));            // guy guard
    prismT(out, 0,0.06, 0.07, 0.02,0.2, 8, 'iron', 0.25);                   // anchor eye
    box(out, -0.05,0.05, -0.32,-0.26, 0.6,0.86, 'gold', 0.55);              // marker plate
    beam(out,[0,-0.29,0.0],[0,-0.29,0.72], 0.024,'wood',0.1);               // plate stake
  }
  // ---------------- LIGHT ----------------
  function pYardLight(out,s){
    const ht = 5.6 + (s.len||0)*3.4;
    prismT(out, 0,0, 0.24, 0,0.13, 12, 'concrete', 0);                       // footing
    box(out, -0.16,0.16, -0.16,0.16, 0.13,0.2, 'iron', 0.1);                 // base plate
    pipe(out, 0,0, 0.105, 0.18, ht*0.5, 'alum', 0.05, 12);
    pipe(out, 0,0, 0.088, ht*0.5-0.02, ht, 'alum', 0.08, 12);
    beam(out,[0,0,ht-0.1],[-0.62,0,ht+0.30], 0.058,'alum',0.1);              // gooseneck
    beam(out,[-0.62,0,ht+0.30],[-1.44,0,ht+0.22], 0.058,'alum',0.1);
    box(out, -1.86,-1.2, -0.19,0.19, ht+0.06,ht+0.24, 'alum', 0.15);         // head
    out.push(F([[-1.9,-0.15,ht+0.24],[-1.16,-0.15,ht+0.24],[-1.16,0.15,ht+0.3],[-1.9,0.15,ht+0.3]],'alum',0.34));
    slab(out, [[-1.82,-0.16],[-1.24,-0.16],[-1.24,0.16],[-1.82,0.16]], ht+0.06, 'glow', 0);
    dY(out,-0.105,-1, -0.06,0.06, 1.3,1.62,'alum',-0.9);                     // hand hole
    if(s.gravel) apron(out, 0.5, 'gravel');
  }
  function pStreetLamp(out,s){
    const ht = 3.6 + (s.len||0)*1.8, acorn = s.variant===1;
    prismT(out, 0,0, 0.28, 0,0.1, 12, 'concrete', 0);
    prismT(out, 0,0, 0.22, 0.1,0.36, 12, 'iron', 0.05);                      // cast base
    prismT(out, 0,0, 0.16, 0.36,0.44, 12, 'iron', 0.2);
    pipe(out, 0,0, 0.075, 0.42, ht, 'iron', 0.02, 12);
    for(const z of [ht*0.5, ht*0.78]) tube(out,[0,0,z],[0,0,z+0.05], 0.095, 12,'iron',0.2,null,false);
    dY(out,-0.075,-1, -0.06,0.06, 1.24,1.56,'iron',-0.9);                    // hand hole
    if(acorn){
      prismT(out, 0,0, 0.13, ht,ht+0.09, 10, 'iron', 0.2);
      prismT(out, 0,0, 0.22, ht+0.09,ht+0.42, 10, 'glow', 0.05);             // acorn globe
      prismT(out, 0,0, 0.13, ht+0.42,ht+0.54, 10, 'glow', 0.2);
      prismT(out, 0,0, 0.07, ht+0.54,ht+0.66, 8, 'iron', 0.3);
    } else {
      beam(out,[0,0,ht-0.12],[-0.34,0,ht+0.16], 0.04,'iron',0.1);            // scroll bracket
      beam(out,[-0.34,0,ht+0.16],[-0.8,0,ht+0.16], 0.04,'iron',0.1);
      beam(out,[-0.16,0,ht-0.5],[-0.6,0,ht+0.12], 0.024,'iron',-0.2);
      prismT(out, -0.8,0, 0.1, ht+0.02,ht+0.16, 8, 'iron', 0.2);
      prismT(out, -0.8,0, 0.2, ht-0.28,ht+0.02, 8, 'glow', 0.05);            // lantern lens
      prismT(out, -0.8,0, 0.11, ht-0.4,ht-0.28, 8, 'iron', 0.15);
    }
    if(s.gravel) apron(out, 0.46, 'gravel');
  }
  function pFloodMast(out,s){
    const ht = 6.0 + (s.len||0)*4.0;
    prismT(out, 0,0, 0.34, 0,0.16, 12, 'concrete', 0);
    box(out, -0.2,0.2, -0.2,0.2, 0.16,0.24, 'iron', 0.1);
    pipe(out, 0,0, 0.12, 0.22, ht*0.55, 'galv', 0.04, 12);
    pipe(out, 0,0, 0.1, ht*0.54, ht, 'galv', 0.07, 12);
    box(out, -0.9,0.9, -0.07,0.07, ht,ht+0.1, 'galv', 0.3);                  // head bar
    for(const x of [-0.62,0,0.62]){
      beam(out,[x,0.02,ht+0.1],[x,0.02,ht+0.2], 0.03,'galv',0.1);            // yoke
      tube(out, [x,0.06,ht+0.2],[x,-0.26,ht+0.04], 0.145, 12,'alum',0.1);    // flood can
      tube(out, [x,-0.24,ht+0.05],[x,-0.32,ht+0.01], 0.125, 12,'glow',0.15); // lens
    }
    box(out, -0.16,0.16, -0.18,-0.08, ht*0.3, ht*0.3+0.34,'alum',0.05);      // junction box
    dY(out,-0.18,-1, -0.11,0.11, ht*0.3+0.06, ht*0.3+0.28,'alum',-0.9);
    beam(out,[0.12,-0.1,ht*0.3],[0.12,-0.1,0.3], 0.02,'galv',0.1);
    if(s.gravel) apron(out, 0.56, 'gravel');
  }
  function pBeacon(out,s){                                                   // harbour light on piles
    const ht = 2.6 + (s.len||0)*2.2, sp = 0.62;
    const A=(i)=>i/3*Math.PI*2-Math.PI/2;
    for(let i=0;i<3;i++){ const a=A(i), x=Math.cos(a)*sp, y=Math.sin(a)*sp;
      prismT(out, x,y, 0.15, 0,0.12, 8, 'concrete', 0, gravelTex());
      tube(out, [x,y,0.1], [x*0.32,y*0.32,ht], 0.06, 8, 'galv', 0.05); }
    for(const t of [0.36,0.7]) for(let i=0;i<3;i++){ const j=(i+1)%3, k=sp*(1-0.68*t);
      beam(out,[Math.cos(A(i))*k,Math.sin(A(i))*k,ht*t],[Math.cos(A(j))*k,Math.sin(A(j))*k,ht*t], 0.026,'galv',0.1); }
    prismT(out, 0,0, 0.34, ht,ht+0.06, 8, 'iron', 0.2);                      // platform
    box(out, -0.2,0.2, -0.2,0.2, ht+0.06,ht+0.34, 'body', 0.05);             // lantern housing
    prismT(out, 0,0, 0.2, ht+0.34,ht+0.62, 10, 'glow', 0.1);                 // lens drum
    prismT(out, 0,0, 0.23, ht+0.62,ht+0.7, 10, 'iron', 0.3);
    prismT(out, 0,0, 0.045, ht+0.7,ht+0.86, 6, 'iron', 0.35);
    dY(out,-0.2,-1, -0.14,0.14, ht+0.12,ht+0.3,'gold',0.6, 0.06);            // daymark plate
    for(let i=0;i<5;i++){ const z=0.5+i*0.34; if(z<ht-0.2)
      beam(out,[-0.14,-sp*0.9,z],[0.14,-sp*0.9,z], 0.018,'galv',0.2); }      // ladder rungs
    for(const sx of [-1,1]) beam(out,[sx*0.14,-sp*0.9,0.4],[sx*0.14*0.4,-sp*0.34,ht-0.1], 0.02,'galv',0.15);
  }
  // ---------------- WATER ----------------
  function pHydrant(out,s){
    prismT(out, 0,0, 0.25, 0,0.09, 12, 'iron', -0.1);                        // ground flange
    prismT(out, 0,0, 0.19, 0.09,0.17, 12, 'body', 0.05);
    pipe(out, 0,0, 0.115, 0.15, 0.62, 'body', 0, 12);                        // barrel
    prismT(out, 0,0, 0.145, 0.6,0.68, 12, 'body', 0.2);                      // flange
    prismT(out, 0,0, 0.165, 0.68,0.80, 12, 'body', 0.1);                     // bonnet
    prismT(out, 0,0, 0.10, 0.80,0.89, 10, 'body', 0.25);
    box(out, -0.035,0.035, -0.035,0.035, 0.89,0.96, 'iron', 0.3);            // operating nut
    tube(out,[0,-0.1,0.44],[0,-0.30,0.44], 0.085, 10,'galv',0.12);           // pumper nozzle (−Y front)
    prismT(out, 0,-0.30, 0.10, 0.44,0.44, 10,'galv',0.3);
    for(const sx of [-1,1]){ tube(out,[sx*0.09,0,0.5],[sx*0.24,0,0.5], 0.062, 8,'galv',0.12);
      box(out, sx*0.24,sx*0.30, -0.055,0.055, 0.445,0.555,'galv',0.2); }     // hose nozzle caps
    for(let i=0;i<6;i++){ const a=i/6*Math.PI*2, r=0.15;                     // bonnet bolts
      prismT(out, Math.cos(a)*r,Math.sin(a)*r, 0.022, 0.68,0.71, 6,'iron',0.5); }
    if(s.gravel) apron(out, 0.46, 'gravel');
  }
  function pYardHydrant(out,s){
    const ht = 0.9 + (s.len||0)*0.4;
    pipe(out, 0,0, 0.045, 0, ht, 'galv', 0.05, 10);
    box(out, -0.075,0.075, -0.075,0.075, ht,ht+0.16, 'iron', 0.05);          // head casting
    beam(out,[0,0.02,ht+0.14],[0,-0.34,ht+0.30], 0.022,'iron',0.15);         // lever
    box(out, -0.03,0.03, -0.37,-0.31, ht+0.27,ht+0.33,'iron',0.3);
    tube(out,[0,-0.05,ht-0.02],[0,-0.19,ht-0.06], 0.032, 8,'galv',0.15);     // spout
    prismT(out, 0,0, 0.075, 0,0.05, 10, 'concrete', 0.1);
    apron(out, 0.34, 'gravel');
  }
  function pWellCap(out,s){
    if(s.variant===1){                                                       // dug well · concrete lid
      prismT(out, 0,0, 0.66, 0,0.22, 14, 'concrete', 0, gravelTex());
      prismT(out, 0,0, 0.60, 0.22,0.30, 14, 'concrete', 0.22);
      for(const sx of [-1,1]) box(out, sx*0.2-0.05,sx*0.2+0.05, -0.05,0.05, 0.30,0.34,'iron',0.4);
      pipe(out, 0.34,0.12, 0.055, 0.3, 0.66, 'galv', 0.1, 8);
      prismT(out, 0.34,0.12, 0.07, 0.66,0.71, 8,'iron',0.3);
    } else {                                                                 // drilled well · steel casing
      pipe(out, 0,0, 0.155, 0, 0.52, 'galv', 0.02, 12);
      prismT(out, 0,0, 0.185, 0.52,0.60, 12, 'iron', 0.15);
      for(let i=0;i<4;i++){ const a=i/4*Math.PI*2+0.4; prismT(out, Math.cos(a)*0.15,Math.sin(a)*0.15, 0.022, 0.6,0.63, 6,'iron',0.5); }
      beam(out,[0.06,0,0.6],[0.06,0,0.72], 0.026,'galv',0.2);                // vent
      beam(out,[0.06,0,0.72],[0.06,-0.16,0.72], 0.026,'galv',0.2);
      box(out, -0.06,0.06, -0.20,-0.12, 0.28,0.40,'alum',0.05);              // wire conduit box
      beam(out,[0,-0.16,0.28],[0,-0.16,0.0], 0.018,'galv',0.05);
    }
    apron(out, 0.86, 'gravel');
  }
  function pCurbStop(out,s){
    prismT(out, 0,0, 0.23, 0,0.06, 14, 'asphalt', -0.1, gravelTex());
    prismT(out, 0,0, 0.155, 0.04,0.085, 12, 'iron', 0.1);
    prismT(out, 0,0, 0.125, 0.085,0.105, 12, 'iron', 0.3, waffleTex());
    prismT(out, 0,0, 0.035, 0.105,0.12, 8, 'iron', 0.5);
  }
  function pWaterTank(out,s){
    const stand = 1.9 + (s.len||0)*1.7, r = 0.86, th = 1.5, sp = 0.82;
    for(const sx of [-1,1]) for(const sy of [-1,1]){
      beam(out,[sx*sp,sy*sp,0],[sx*sp*0.72,sy*sp*0.72,stand], 0.06,'wood',0.05);   // battered legs
    }
    for(const sy of [-1,1]) for(let i=0;i<2;i++){ const z0=stand*(0.24+i*0.38), z1=stand*(0.62+i*0.38);
      beam(out,[-sp*0.9,sy*sp*0.86,z0],[sp*0.9,sy*sp*0.86,Math.min(z1,stand)], 0.032,'wood',0.1);
      beam(out,[sp*0.9,sy*sp*0.86,z0],[-sp*0.9,sy*sp*0.86,Math.min(z1,stand)], 0.032,'wood',0.1); }
    for(const sx of [-1,1]) beam(out,[sx*sp*0.74,-sp*0.74,stand],[sx*sp*0.74,sp*0.74,stand], 0.05,'wood',0.15);
    box(out, -r*0.8,r*0.8, -r*0.8,r*0.8, stand,stand+0.07, 'wood', 0.1, grainTex(0.24));  // deck
    pipe(out, 0,0, r, stand+0.07, stand+0.07+th, 'galv', 0, 16, corrTex(0.3));            // tank
    for(const z of [stand+0.4, stand+1.06]) tube(out,[0,0,z],[0,0,z+0.07], r+0.02, 16,'iron',0.1,null,false);
    prismT(out, 0,0, r+0.02, stand+0.07+th, stand+0.14+th, 16, 'galv', 0.3);
    prismT(out, 0.24,0.1, 0.2, stand+0.14+th, stand+0.2+th, 12, 'galv', 0.4);             // hatch
    pipe(out, -0.5,0.34, 0.05, 0, stand+0.2, 'galv', 0.08, 8);                            // downpipe
    beam(out,[-0.5,0.34,0.5],[-0.9,0.34,0.5], 0.05,'galv',0.1);
    pipe(out, 0.62,-0.3, 0.032, stand+0.1, stand+th, 'galv', 0.05, 8);                    // overflow
    if(s.gravel) slab(out, [[-1.3,-1.3],[1.3,-1.3],[1.3,1.3],[-1.3,1.3]], 0.012,'gravel',-0.15, gravelTex());
  }
  function pStandpipe(out,s){                                                // public tap & trough
    pad(out, 0.92,0.5, 0.12, 'concrete', 0);
    pipe(out, 0,0.14, 0.06, 0.12, 1.24, 'galv', 0.04, 10);
    beam(out,[0,0.14,1.24],[0,-0.04,1.24], 0.06,'galv',0.1, 10);
    prismT(out, 0,-0.04, 0.075, 1.08,1.24, 10, 'brass', 0.2);                // bib tap
    prismT(out, 0,-0.04, 0.04, 0.98,1.08, 8, 'brass', 0.3);
    box(out, -0.06,0.06, -0.14,-0.02, 1.16,1.21, 'brass', 0.35);             // handle
    box(out, -0.44,0.44, -0.66,-0.1, 0.06,0.46, 'concrete', 0.02, gravelTex(), true);
    slab(out, [[-0.44,-0.66],[0.44,-0.66],[0.44,-0.1],[-0.44,-0.1]], 0.46,'concrete',0.28, gravelTex());
    slab(out, [[-0.34,-0.58],[0.34,-0.58],[0.34,-0.18],[-0.34,-0.18]], 0.36,'glass',-0.5);   // water
    box(out, -0.06,0.06, -0.7,-0.64, 0.2,0.3, 'iron', 0.1);                  // overflow
    if(s.gravel) slab(out, [[-0.6,-0.86],[0.6,-0.86],[0.6,0.34],[-0.6,0.34]], 0.01,'gravel',-0.15, gravelTex());
  }
  function pHandPump(out,s){
    box(out, -0.44,0.44, -0.4,0.4, 0,0.14, 'wood', 0.02, grainTex(0.2));     // plank platform
    prismT(out, 0,0, 0.1, 0.14,0.3, 10, 'iron', 0.05);                       // base flange
    pipe(out, 0,0, 0.072, 0.28, 0.88, 'iron', 0.02, 10);
    prismT(out, 0,0, 0.1, 0.88,1.0, 10, 'iron', 0.15);                       // head casting
    tube(out,[0,-0.04,0.9],[0,-0.34,0.78], 0.05, 8, 'iron', 0.12);           // spout
    beam(out,[0,0.04,1.0],[0,0.06,1.06], 0.032,'iron',0.2);                  // pivot lug
    beam(out,[0,0.06,1.06],[0,0.44,1.24], 0.034,'iron',0.15);                // handle
    box(out, -0.032,0.032, 0.42,0.54, 1.21,1.28, 'wood', 0.3);               // grip
    prismT(out, 0.26,0.22, 0.03, 0.14,0.44, 6, 'iron', 0.2);                 // bucket hook
    beam(out,[0.26,0.22,0.44],[0.26,0.1,0.44], 0.022,'iron',0.25);
    if(s.gravel) apron(out, 0.6, 'gravel');
  }
  function pValveVault(out,s){
    box(out, -0.56,0.56, -0.5,0.5, 0,0.12, 'concrete', 0.02, gravelTex(), true);
    slab(out, [[-0.56,-0.5],[0.56,-0.5],[0.56,0.5],[-0.56,0.5]], 0.12,'concrete',0.2, gravelTex());
    box(out, -0.42,0.42, -0.34,0.34, 0.1,0.16, 'iron', 0.08, null, true);    // frame
    for(const sx of [-1,1]) box(out, sx*0.2-0.17, sx*0.2+0.17, -0.3,0.3, 0.13,0.17, 'iron', 0.2, waffleTex());
    dX(out, 0.0,1, -0.06,0.06, 0.15,0.17,'iron',-1.4, 0.02);                 // pick slot
    prismT(out, 0.46,0.34, 0.055, 0.12,0.44, 10, 'iron', 0.1);               // air valve
    prismT(out, 0.46,0.34, 0.078, 0.44,0.52, 10, 'iron', 0.25);
    beam(out,[-0.64,-0.42,0],[-0.64,-0.42,0.9], 0.03,'wood',0.1);            // marker stake
    dY(out,-0.45,-1, -0.72,-0.56, 0.64,0.86,'gold',0.6, 0.05);
    if(s.gravel) apron(out, 0.78, 'gravel');
  }
  function pCistern(out,s){
    const w = 1.5 + (s.len||0)*0.9;
    box(out, -w/2,w/2, -0.7,0.7, 0,0.42, 'concrete', 0.02, gravelTex(), true);
    chamferTop(out, -w/2,w/2, -0.7,0.7, 0.5, 0.08, 0.08, 'concrete', 0.18);
    prismT(out, -w*0.24,0, 0.3, 0.5,0.62, 14, 'concrete', 0.1, gravelTex()); // hatch riser
    prismT(out, -w*0.24,0, 0.27, 0.62,0.68, 14, 'iron', 0.22, waffleTex());
    for(let i=0;i<4;i++){ const a=i/4*Math.PI*2+0.5;
      prismT(out, -w*0.24+Math.cos(a)*0.2, Math.sin(a)*0.2, 0.024, 0.68,0.705, 6,'iron',0.45); }
    pipe(out, w*0.28,-0.24, 0.055, 0.5, 1.0, 'galv', 0.05, 10);              // vent
    beam(out,[w*0.28,-0.24,1.0],[w*0.28,-0.4,1.0], 0.055,'galv',0.1, 10);
    pipe(out, w*0.36,0.44, 0.06, 0.5, 1.86, 'galv', 0.04, 8);                // downpipe off the eaves
    beam(out,[w*0.36,0.44,1.86],[w*0.36,0.62,1.94], 0.06,'galv',0.1, 8);
    for(const z of [0.9,1.5]) box(out, w*0.36-0.09,w*0.36+0.09, 0.4,0.5, z,z+0.05,'galv',0.2);
    box(out, -0.1,0.1, -0.73,-0.66, 0.16,0.32, 'iron', 0.1);                 // overflow scupper
    if(s.gravel) slab(out, [[-(w/2+0.3),-0.94],[w/2+0.3,-0.94],[w/2+0.3,0.94],[-(w/2+0.3),0.94]], 0.012,'gravel',-0.15, gravelTex());
  }
  // ---------------- SEWER ----------------
  function pManhole(out,s){
    const road = s.variant===1;
    prismT(out, 0,0, 0.72, 0,0.07, 16, road?'asphalt':'concrete', -0.05, gravelTex());
    prismT(out, 0,0, 0.47, 0.05,0.105, 14, 'iron', 0.05);                    // frame
    prismT(out, 0,0, 0.40, 0.09,0.135, 18, 'iron', 0.2, waffleTex());        // cover
    prismT(out, 0,0, 0.30, 0.135,0.142, 18, 'iron', 0.3);                    // raised ring
    dX(out, 0.06,1, -0.06,0.06, 0.132,0.142, 'iron', -1.4, 0.02);            // pick hole
    if(road){ for(let i=0;i<3;i++){ const a=i*2.1;                            // shim ring + patch crumbs
      prismT(out, Math.cos(a)*0.62,Math.sin(a)*0.62, 0.06, 0,0.045, 6,'asphalt',0.1); } }
  }
  function pCleanout(out,s){
    pad(out, 0.44,0.44, 0.1, 'concrete', 0);
    pipe(out, 0,0, 0.075, 0.1, 0.4, 'galv', 0.05, 10);
    prismT(out, 0,0, 0.088, 0.4,0.46, 10, 'brass', 0.2);
    box(out, -0.035,0.035, -0.035,0.035, 0.46,0.5, 'brass', 0.35);           // square nut
    tube(out,[0,0,0.22],[0,-0.26,0.14], 0.062, 8,'galv',0.05);               // wye stub
    prismT(out, 0,-0.26, 0.075, 0.14,0.14, 8,'galv',0.2);
    if(s.gravel) apron(out, 0.4, 'gravel');
  }
  function pVentStack(out,s){
    const ht = 2.0 + (s.len||0)*1.3;
    prismT(out, 0,0, 0.16, 0,0.12, 12, 'concrete', 0);
    pipe(out, 0,0, 0.062, 0.1, ht, 'galv', 0.02, 10);
    beam(out,[0,0,ht],[0.12,0,ht+0.17], 0.062,'galv',0.08, 10);              // candy-cane bend
    beam(out,[0.12,0,ht+0.17],[0.36,0,ht+0.17], 0.062,'galv',0.08, 10);
    beam(out,[0.36,0,ht+0.17],[0.48,0,ht], 0.062,'galv',0.08, 10);
    pipe(out, 0.48,0, 0.062, ht-0.22, ht+0.02, 'galv', 0.05, 10);
    tube(out,[0.48,0,ht-0.22],[0.48,0,ht-0.26], 0.075, 10,'galv',-0.4);      // screened mouth
    box(out, -0.02,0.5, -0.05,0.05, ht*0.62,ht*0.62+0.06,'galv',0.15);       // spacer bar
    for(const z of [0.5, ht*0.78]) tube(out,[0,0,z],[0,0,z+0.045], 0.078, 10,'iron',0.2,null,false);
    if(s.gravel) apron(out, 0.36, 'gravel');
  }
  function pSepticLids(out,s){
    const sp = 0.72 + (s.len||0)*0.6;
    slab(out, [[-sp-0.6,-0.62],[sp+0.6,-0.62],[sp+0.6,0.62],[-sp-0.6,0.62]], 0.012, 'gravel', -0.12, gravelTex());
    for(const sx of [-1,1]){
      prismT(out, sx*sp,0, 0.38, 0,0.13, 14, 'concrete', 0, gravelTex());     // riser
      prismT(out, sx*sp,0, 0.34, 0.13,0.19, 14, 'body', 0.15);                // lid
      for(let i=0;i<4;i++){ const a=i/4*Math.PI*2+0.6;
        prismT(out, sx*sp+Math.cos(a)*0.26, Math.sin(a)*0.26, 0.026, 0.19,0.215, 6,'iron',0.45); }
      prismT(out, sx*sp,0, 0.06, 0.19,0.235, 8,'body',0.35);                  // lifting boss
    }
    pipe(out, 0,0.1, 0.055, 0, 0.42, 'galv', 0.05, 8);                        // inspection riser
    prismT(out, 0,0.1, 0.07, 0.42,0.47, 8,'iron',0.25);
    beam(out,[-sp-0.34,-0.42,0],[-sp-0.34,-0.42,0.62], 0.028,'wood',0.1);     // marker stake
    dY(out,-0.42-0.028,-1, -sp-0.4,-sp-0.28, 0.46,0.6,'gold',0.6, 0.05);
  }
  function pCatchBasin(out,s){
    box(out, -0.62,0.62, 0.28,0.62, 0,0.34, 'concrete', 0.05, gravelTex());   // curb block
    box(out, -0.52,0.52, -0.36,0.3, 0,0.15, 'concrete', 0, gravelTex(), true);
    slab(out, [[-0.52,-0.36],[0.52,-0.36],[0.52,0.3],[-0.52,0.3]], 0.15, 'concrete', 0.24, gravelTex());
    slab(out, [[-0.4,-0.26],[0.4,-0.26],[0.4,0.2],[-0.4,0.2]], 0.03, 'iron', -2.2);        // throat void
    box(out, -0.42,0.42, -0.28,0.22, 0.11,0.16, 'iron', 0.1, null, true);     // frame
    for(let i=0;i<7;i++){ const y=-0.24+i*0.07;                              // grate bars
      box(out, -0.4,0.4, y,y+0.035, 0.13,0.16, 'iron', 0.15); }
    dY(out, 0.28,-1, -0.28,0.28, 0.06,0.2,'iron',-1.8, 0.04);                 // curb inlet mouth
    if(s.variant===1) slab(out, [[-0.8,-0.62],[0.8,-0.62],[0.8,-0.3],[-0.8,-0.3]], 0.014,'asphalt',-0.1, gravelTex());
  }
  function pCulvert(out,s){
    const L = 1.9 + (s.len||0)*2.4, r = 0.34;
    tube(out, [0,-L/2,r+0.06],[0,L/2,r+0.06], r, 14,'galv',0.02, corrTex(0.14), false);
    const ring=(y)=>{ const p=[]; for(let i=0;i<14;i++){ const t=(i/14)*Math.PI*2;
        p.push([Math.cos(t)*r, y, r+0.06+Math.sin(t)*r]); }
      out.push(F(y<0?p:p.slice().reverse(), 'galv', y<0?0.1:-0.4)); };
    ring(-L/2); ring(L/2);
    tube(out, [0,-L/2+0.02,r+0.06],[0,-L/2+0.12,r+0.06], r*0.82, 14,'iron',-1.9,null,false);  // dark bore
    for(const sx of [-1,1]){                                                  // earth embankment wedges
      const xi=sx>0?(r+0.02):-1.5, xo=sx>0?1.5:-(r+0.02);                     // x-ascending, then y — up-facing
      out.push(F([[xi,-L/2,0],[xo,-L/2,0],[xo,L/2,0],[xi,L/2,0]],'gravel',-0.2,0,[[0,0],[1.4,0],[1.4,L],[0,L]],gravelTex()));
      const w=[[sx*(r+0.02),0.02],[sx*1.2,0.02],[sx*(r+0.02),r*1.5]];         // end triangles, −Y then +Y
      out.push(F(sx>0?[[w[0][0],-L/2,w[0][1]],[w[1][0],-L/2,w[1][1]],[w[2][0],-L/2,w[2][1]]]
                     :[[w[1][0],-L/2,w[1][1]],[w[0][0],-L/2,w[0][1]],[w[2][0],-L/2,w[2][1]]],'gravel',0.1));
      out.push(F(sx>0?[[w[1][0],L/2,w[1][1]],[w[0][0],L/2,w[0][1]],[w[2][0],L/2,w[2][1]]]
                     :[[w[0][0],L/2,w[0][1]],[w[1][0],L/2,w[1][1]],[w[2][0],L/2,w[2][1]]],'gravel',-0.1));
    }
    const rnd=mulberry32(4711);                                               // rip-rap at the mouth
    for(let i=0;i<9;i++){ const a=(rnd()*1.6-0.8), rr=0.42+rnd()*0.7, sz=0.09+rnd()*0.09;
      prismT(out, Math.sin(a)*rr*1.4, -L/2-0.1-rnd()*0.5, sz, 0, sz*1.1, 7, 'concrete', -0.1+rnd()*0.3, gravelTex()); }
  }
  function pTrenchDrain(out,s){
    const L = 1.6 + (s.len||0)*2.2, n=Math.round(L/0.15);
    box(out, -0.34,0.34, -L/2,L/2, 0,0.16, 'concrete', 0.02, gravelTex(), true);
    slab(out, [[-0.34,-L/2],[-0.2,-L/2],[-0.2,L/2],[-0.34,L/2]], 0.16,'concrete',0.2, gravelTex());
    slab(out, [[0.2,-L/2],[0.34,-L/2],[0.34,L/2],[0.2,L/2]], 0.16,'concrete',0.2, gravelTex());
    slab(out, [[-0.2,-L/2],[0.2,-L/2],[0.2,L/2],[-0.2,L/2]], 0.05,'iron',-2.2);        // channel bed
    for(const sx of [-1,1]) box(out, sx*0.21-0.03, sx*0.21+0.03, -L/2+0.03,L/2-0.03, 0.11,0.16,'iron',0.1);
    for(let i=0;i<n;i++){ const y=-L/2+0.09+i*0.15; if(y+0.055>L/2-0.05) break;
      box(out, -0.2,0.2, y,y+0.055, 0.12,0.16,'iron',0.15); }
    if(s.variant===1) slab(out, [[-0.8,-L/2-0.1],[-0.34,-L/2-0.1],[-0.34,L/2+0.1],[-0.8,L/2+0.1]], 0.014,'asphalt',-0.1, gravelTex());
  }
  function pLiftStation(out,s){
    prismT(out, 0,0, 0.72, 0,0.18, 16, 'concrete', 0, gravelTex());                   // wet-well slab
    prismT(out, 0,0, 0.44, 0.16,0.24, 14, 'iron', 0.08);
    box(out, -0.32,0.32, -0.28,0.28, 0.2,0.28, 'iron', 0.2, waffleTex());             // hatch
    dY(out,-0.28,-1, -0.07,0.07, 0.24,0.28,'iron',-1.2, 0.02);
    pipe(out, 0.5,0.4, 0.055, 0.16, 1.5, 'galv', 0.05, 10);                           // vent
    beam(out,[0.5,0.4,1.5],[0.5,0.22,1.62], 0.055,'galv',0.1, 10);
    box(out, -1.54,-0.86, -0.34,0.34, 0,0.14,'concrete',0, gravelTex());              // panel plinth
    box(out, -1.44,-0.96, -0.26,0.26, 0.14,1.16,'body',0);
    out.push(F([[-1.46,-0.28,1.16],[-0.94,-0.28,1.16],[-0.94,0.28,1.28],[-1.46,0.28,1.28]],'body',0.3));
    dY(out,-0.26,-1, -1.38,-1.02, 0.3,1.1,'body',-1.2);                               // door
    dY(out,-0.26,-1, -1.23,-1.17, 0.66,0.76,'iron',0.8, 0.07);
    dY(out,-0.26,-1, -1.32,-1.18, 1.02,1.1,'gold',0.7, 0.06);
    beam(out,[-0.96,0.22,0.9],[-0.4,0.22,0.42], 0.03,'galv',0.1);                     // conduit to the well
    beam(out,[-0.4,0.22,0.42],[-0.16,0.22,0.24], 0.03,'galv',0.1);
    if(s.gravel) slab(out, [[-1.76,-0.86],[0.94,-0.86],[0.94,0.86],[-1.76,0.86]], 0.012,'gravel',-0.15, gravelTex());
  }
  function pOutfall(out,s){
    const r = 0.32, wz = 1.0;
    box(out, -0.92,0.92, 0.1,0.36, 0,wz, 'concrete', 0.02, gravelTex());              // headwall
    tube(out, [0,0.36,r+0.1],[0,-0.44,r+0.1], r, 14,'concrete',0.05,null,false);      // pipe
    const ring=(y,b)=>{ const p=[]; for(let i=0;i<14;i++){ const t=(i/14)*Math.PI*2;
        p.push([Math.cos(t)*r, y, r+0.1+Math.sin(t)*r]); }
      out.push(F(y<0?p:p.slice().reverse(), 'concrete', b)); };
    ring(-0.44, 0.1);
    tube(out, [0,-0.42,r+0.1],[0,-0.3,r+0.1], r*0.8, 14,'iron',-1.9,null,false);      // dark bore
    tube(out, [0,-0.52,r+0.08],[0,-0.56,r+0.04], r*0.94, 12,'iron',0.15);             // flap gate
    beam(out,[0,-0.42,r+0.44],[0,-0.54,r+0.3], 0.028,'iron',0.2);                     // hinge arm
    for(const sx of [-1,1]) box(out, sx*0.78-0.14, sx*0.78+0.14, -0.32,0.12, 0,0.68,'concrete',0.05, gravelTex());
    const rnd=mulberry32(881);
    for(let i=0;i<11;i++){ const x=(rnd()*2-1)*1.15, y=-0.5-rnd()*0.8, sz=0.09+rnd()*0.1;
      prismT(out, x,y, sz, 0, sz*1.1, 7, 'concrete', -0.1+rnd()*0.3, gravelTex()); }
  }
  // ---------------- FUEL ----------------
  function pOilTank(out,s){
    const w = 1.12, d = 0.66, base = 0.34, top = 1.58;
    for(const sx of [-1,1]) for(const sy of [-1,1]){
      const x=sx*(w/2-0.09), y=sy*(d/2-0.07);
      beam(out,[x,y,0],[x,y,base+0.06], 0.038,'iron',0.05);
      dY(out, y, sy, x-0.05,x+0.05, 0.02,base,'iron',0.2, 0.05); }
    for(const sy of [-1,1]) beam(out,[-(w/2-0.09),sy*(d/2-0.07),0.08],[w/2-0.09,sy*(d/2-0.07),0.24], 0.02,'iron',0.1);
    box(out, -w/2,w/2, -d/2,d/2, base,top, 'body', 0, null, true);            // tank body
    chamferTop(out, -w/2,w/2, -d/2,d/2, top+0.1, 0.1, 0.14, 'body', 0.12);
    for(const sy of [-1,1]) dY(out, sy*d/2, sy, -w/2+0.06,w/2-0.06, base+0.06,base+0.12,'body', sy>0?-0.5:0.45, 0.05); // seam
    prismT(out, -0.3,0.0, 0.075, top+0.1,top+0.2, 10, 'galv', 0.2);           // fill bung
    prismT(out, 0.06,0.0, 0.055, top+0.1,top+0.18, 10, 'galv', 0.2);          // vent bung
    prismT(out, 0.34,0.0, 0.05, top+0.1,top+0.26, 10, 'brass', 0.25);         // gauge body
    prismT(out, 0.34,0.0, 0.032, top+0.26,top+0.40, 8, 'glass', 0.3);         // gauge tube
    tube(out,[-w/2,0.16,base+0.16],[-w/2-0.16,0.16,base+0.16], 0.055, 8,'copper',0.1);   // filter + valve
    prismT(out, -w/2-0.16,0.16, 0.085, base+0.02,base+0.2, 10, 'iron', 0.05);
    beam(out,[-w/2-0.16,0.16,base+0.02],[-w/2-0.16,0.16,0.06], 0.018,'copper',0.05);
    beam(out,[-w/2-0.16,0.16,0.06],[-w/2-0.5,0.16,0.06], 0.018,'copper',0.05);
    dY(out,-d/2,-1, -0.14,0.14, base+0.5,base+0.68,'gold',0.55, 0.05);        // maker plate
    if(s.variant===1){                                                        // twin tank behind
      const off=0.86;
      box(out, -w/2,w/2, -d/2+off,d/2+off, base,top, 'body', -0.15, null, true);
      chamferTop(out, -w/2,w/2, -d/2+off,d/2+off, top+0.1, 0.1, 0.14, 'body', 0);
      for(const sx of [-1,1]) for(const sy of [-1,1]){ const x=sx*(w/2-0.09), y=sy*(d/2-0.07)+off;
        beam(out,[x,y,0],[x,y,base+0.06], 0.038,'iron',0.05); }
      beam(out,[0.34,0.0,top+0.14],[0.34,off-d/2,top+0.14], 0.028,'copper',0.1);
    }
    if(s.gravel) slab(out, [[-1.0,-0.6],[1.0,-0.6],[1.0,0.6+(s.variant===1?0.9:0)],[-1.0,0.6+(s.variant===1?0.9:0)]], 0.01,'gravel',-0.15, gravelTex());
  }
  function pFillPipes(out,s){
    box(out, -0.32,0.32, 0.14,0.2, 0.06,0.98, 'wood', 0.05, grainTex(0.22));  // wall board
    pad(out, 0.7,0.42, 0.08, 'concrete', 0);
    pipe(out, -0.14,0.04, 0.05, 0.08, 0.7, 'galv', 0.04, 10);                 // fill
    prismT(out, -0.14,0.04, 0.075, 0.7,0.78, 10, 'brass', 0.25);
    prismT(out, -0.14,0.04, 0.045, 0.78,0.81, 8, 'brass', 0.4);
    pipe(out, 0.14,0.04, 0.038, 0.08, 0.84, 'galv', 0.04, 10);                // vent + whistle
    beam(out,[0.14,0.04,0.84],[0.14,-0.14,0.84], 0.038,'galv',0.1, 8);
    prismT(out, 0.14,-0.14, 0.062, 0.78,0.86, 10, 'galv', 0.2);
    for(const z of [0.3,0.62]) box(out, -0.2,0.2, 0.1,0.15, z,z+0.045,'galv',0.2);  // straps
    dY(out, 0.14,-1, -0.3,-0.18, 0.72,0.86,'gold',0.6, 0.05);                 // tag
    if(s.gravel) slab(out, [[-0.44,-0.3],[0.44,-0.3],[0.44,0.2],[-0.44,0.2]], 0.01,'gravel',-0.15, gravelTex());
  }
  function pPropaneTank(out,s){
    if(s.variant===1){                                                        // 100 lb bottle pair
      pad(out, 0.96,0.56, 0.1, 'concrete', 0);
      for(const sx of [-1,1]){ const x=sx*0.24;
        pipe(out, x,0, 0.185, 0.1, 1.05, 'body', 0, 14);
        prismT(out, x,0, 0.15, 1.05,1.16, 14, 'body', 0.2);
        tube(out,[x,0,1.16],[x,0,1.3], 0.185, 14,'iron',0.1,null,false);       // collar
        prismT(out, x,0, 0.045, 1.14,1.26, 8, 'brass', 0.3);                   // valve
        beam(out,[x,0,1.2],[x*0.35,0.0,1.24], 0.018,'copper',0.1); }
      box(out, -0.1,0.1, -0.06,0.06, 1.18,1.34,'alum',0.1);                    // regulator
      prismT(out, 0,0, 0.09, 1.34,1.4, 10,'alum',0.3);
      beam(out,[0,0,1.18],[0,0.2,0.4], 0.016,'copper',0.05);
      beam(out,[0,0.2,0.4],[0,0.24,0.16], 0.016,'copper',0.05);
      box(out, -0.3,0.3, 0.2,0.26, 0.06,1.4,'wood',-0.1, grainTex(0.2));       // wall board
      return;
    }
    const L = 1.1 + (s.len||0)*0.7, r = 0.42;                                  // 500 gal horizontal
    for(const sx of [-1,1]) box(out, sx*L*0.56-0.16,sx*L*0.56+0.16, -0.3,0.3, 0,0.3,'concrete',0, gravelTex());
    tube(out, [-L,0,r+0.3],[L,0,r+0.3], r, 16,'body',0, null, false);
    for(const sx of [-1,1]){                                                   // dished heads
      tube(out, [sx*L,0,r+0.3],[sx*(L+0.1),0,r+0.3], r*0.94, 16,'body',0.05, null, false);
      tube(out, [sx*(L+0.1),0,r+0.3],[sx*(L+0.16),0,r+0.3], r*0.7, 16,'body',0.1, null, false);
      const p=[]; for(let i=0;i<16;i++){ const t=(i/16)*Math.PI*2; p.push([sx*(L+0.16), Math.cos(t)*r*0.7, r+0.3+Math.sin(t)*r*0.7]); }
      out.push(F(sx>0?p:p.slice().reverse(),'body',0.18)); }
    for(const sx of [-1,1]) tube(out,[sx*L*0.56,0,r+0.3],[sx*L*0.56+0.04,0,r+0.3], r+0.02, 16,'iron',0.05,null,false); // saddles
    box(out, -0.24,0.24, -0.2,0.2, r+0.62,r+0.78, 'body', 0.2);                // dome cover
    out.push(F([[-0.26,-0.22,r+0.78],[0.26,-0.22,r+0.78],[0.26,0.22,r+0.86],[-0.26,0.22,r+0.86]],'body',0.32));
    prismT(out, -0.08,0, 0.05, r+0.72,r+0.84, 8, 'brass', 0.25);
    box(out, 0.06,0.2, -0.08,0.08, r+0.74,r+0.9,'alum',0.15);                  // regulator
    beam(out,[0.13,0,r+0.74],[0.13,0.3,0.5], 0.016,'copper',0.05);
    beam(out,[0.13,0.3,0.5],[0.13,0.34,0.14], 0.016,'copper',0.05);
    dY(out, -0.42,-1, -0.5,-0.2, r+0.34,r+0.5,'gold',0.5, 0.05);               // data plate
    if(s.gravel) slab(out, [[-(L+0.5),-0.6],[L+0.5,-0.6],[L+0.5,0.6],[-(L+0.5),0.6]], 0.012,'gravel',-0.15, gravelTex());
  }
  function pBulkTank(out,s){
    const ht = 2.4 + (s.len||0)*2.0, r = 0.95;
    if(s.gravel){ for(const sy of [-1,1]) box(out, -1.52,1.52, sy*1.42-0.1, sy*1.42+0.1, 0,0.4,'concrete',0.05, gravelTex());
      for(const sx of [-1,1]) box(out, sx*1.42-0.1, sx*1.42+0.1, -1.32,1.32, 0,0.4,'concrete',0.02, gravelTex()); }
    prismT(out, 0,0, r+0.16, 0,0.22, 16, 'concrete', 0, gravelTex());                 // ring plinth
    pipe(out, 0,0, r, 0.2, ht, 'body', 0, 16, corrTex(0.36));                         // shell courses
    for(const t of [0.4,0.75]){ const z=0.2+(ht-0.2)*t; tube(out,[0,0,z],[0,0,z+0.05], r+0.02, 16,'iron',0.08,null,false); }
    prismT(out, 0,0, r+0.03, ht, ht+0.1, 16, 'body', 0.3);                            // roof
    prismT(out, 0.3,0.12, 0.18, ht+0.1, ht+0.2, 12, 'galv', 0.35);                    // manway
    prismT(out, -0.3,-0.12, 0.055, ht+0.1, ht+0.34, 10, 'galv', 0.3);                 // vent
    for(const sx of [-1,1]) beam(out,[sx*0.16,-r-0.1,0.28],[sx*0.16,-r-0.1,ht+0.1], 0.022,'galv',0.15);
    for(let i=0;i<Math.floor((ht-0.4)/0.3);i++){ const z=0.44+i*0.3;
      beam(out,[-0.16,-r-0.1,z],[0.16,-r-0.1,z], 0.016,'galv',0.2); }
    tube(out,[0,-r+0.04,0.52],[0,-r-0.34,0.52], 0.055, 8,'galv',0.1);                 // outlet
    prismT(out, 0,-r-0.34, 0.075, 0.42,0.64, 10, 'iron', 0.05);                       // valve
    beam(out,[0,-r-0.34,0.42],[0,-r-0.34,0.06], 0.024,'galv',0.05);
    if(s.gravel) dY(out,-1.52,-1, -0.34,0.0, 0.14,0.3,'gold',0.6, 0.05);
  }
  function pFuelPump(out,s){
    pad(out, 0.68,0.58, 0.16, 'concrete', 0);
    box(out, -0.22,0.22, -0.18,0.18, 0.16,1.32, 'body', 0);
    chamferTop(out, -0.235,0.235, -0.195,0.195, 1.42, 0.05, 0.08, 'body', 0.2);
    dY(out,-0.18,-1, -0.16,0.16, 0.36,0.9,'body',-1.1);                              // cabinet door
    dY(out,-0.18,-1, -0.13,0.13, 0.98,1.28,'glass',0.5, 0.06);                        // dial window
    dY(out,-0.18,-1, -0.05,0.05, 0.6,0.68,'brass',0.9, 0.07);                         // handle
    prismT(out, 0,0, 0.13, 1.42,1.74, 12, 'glass', 0.15);                             // sight column
    prismT(out, 0,0, 0.15, 1.74,1.82, 12, 'iron', 0.3);
    prismT(out, 0,0, 0.05, 1.82,1.94, 8, 'iron', 0.35);
    beam(out,[0.2,0.08,1.22],[0.5,0.08,1.02], 0.03,'iron',0.05);                      // hose arch
    beam(out,[0.5,0.08,1.02],[0.52,0.08,0.66], 0.03,'iron',0.05);
    box(out, 0.44,0.6, 0.0,0.16, 0.5,0.68, 'iron', 0.1);                              // nozzle boot
    if(s.gravel) apron(out, 0.5, 'gravel');
  }
  function pDrumRack(out,s){
    const n = s.variant===1?3:2, sp = 0.68, half = ((n-1)/2)*sp;
    for(const sy of [-1,1]) for(const sx of [-1,1])
      beam(out,[sx*(half+0.34),sy*0.34,0],[sx*(half+0.34),sy*0.34,0.66], 0.05,'wood',0.05);
    for(const sy of [-1,1]) beam(out,[-(half+0.34),sy*0.34,0.62],[half+0.34,sy*0.34,0.62], 0.05,'wood',0.12);
    for(let i=0;i<n;i++){ const x=-half+i*sp;
      box(out, x-0.3,x+0.3, -0.32,0.32, 0.56,0.66, 'wood', 0.15, grainTex(0.2));       // cradle
      tube(out, [x,-0.36,0.94],[x,0.36,0.94], 0.29, 14, 'body', 0, corrTex(0.26));     // drum
      for(const sy of [-1,1]) tube(out,[x,sy*0.22,0.94],[x,sy*0.27,0.94], 0.305, 14,'iron',0.1,null,false);
      tube(out,[x-0.1,-0.36,0.86],[x-0.1,-0.54,0.86], 0.034, 8,'brass',0.2);           // tap
      tube(out,[x-0.1,-0.52,0.86],[x-0.1,-0.52,0.72], 0.024, 6,'brass',0.25);
      prismT(out, x+0.11,-0.4, 0.038, 0.98,1.06, 8, 'galv', 0.25); }                   // bung
    box(out, -(half+0.4),half+0.4, -0.64,-0.3, 0,0.1, 'galv', 0.05);                   // drip tray
    dY(out,-0.64,-1, -0.14,0.14, 0.02,0.08,'galv',-0.6);
    if(s.gravel) slab(out, [[-(half+0.6),-0.8],[half+0.6,-0.8],[half+0.6,0.56],[-(half+0.6),0.56]], 0.012,'gravel',-0.15, gravelTex());
  }
  function pCoalBin(out,s){
    const w = 1.3 + (s.len||0)*0.8;
    box(out, -w/2-0.06,w/2+0.06, -0.66,0.66, 0,0.16, 'concrete', 0, gravelTex(), true);
    box(out, -w/2,w/2, -0.6,0.6, 0.14,0.96, 'wood', 0, grainTex(0.22), true);
    out.push(F([[-w/2-0.05,-0.66,1.0],[w/2+0.05,-0.66,1.0],[w/2+0.05,0.66,1.2],[-w/2-0.05,0.66,1.2]],
      'wood', 0.34, 0, [[0,0],[w+0.1,0],[w+0.1,1.34],[0,1.34]], grainTex(0.24)));       // sloped lid
    for(const sx of [-1,1]) beam(out,[sx*(w/2-0.06),-0.56,0.14],[sx*(w/2-0.06),-0.56,0.98], 0.055,'wood',0.15);
    dY(out,-0.6,-1, -0.3,0.3, 0.3,0.66,'wood',-1.1);                                   // chute door
    dY(out,-0.6,-1, -0.05,0.05, 0.44,0.54,'iron',0.8, 0.07);
    dY(out,-0.6,-1, -0.3,-0.24, 0.3,0.66,'iron',0.5, 0.06);
    for(const z of [0.34,0.8]) dY(out,-0.6,-1, -w/2+0.08,w/2-0.08, z,z+0.05,'wood',0.35, 0.04);
    const rnd=mulberry32(3301);
    for(let i=0;i<8;i++){ const x=(rnd()*2-1)*(w*0.42), y=-0.76-rnd()*0.34, sz=0.055+rnd()*0.07;
      prismT(out, x,y, sz, 0, sz*0.9, 6, 'asphalt', -0.2+rnd()*0.4); }                  // spilled coal
    if(s.gravel) slab(out, [[-(w/2+0.4),-1.14],[w/2+0.4,-1.14],[w/2+0.4,0.86],[-(w/2+0.4),0.86]], 0.01,'gravel',-0.15, gravelTex());
  }
  // ---------------- TELECOM ----------------
  function pTelecomPed(out,s){
    pad(out, 0.56,0.44, 0.1, 'concrete', 0);
    pipe(out, 0,0.02, 0.05, 0.1, 0.5, 'galv', 0.05, 10);
    box(out, -0.17,0.17, -0.11,0.11, 0.46,0.92, 'body', 0);
    out.push(F([[-0.19,-0.13,0.92],[0.19,-0.13,0.92],[0.19,0.13,1.0],[-0.19,0.13,1.0]],'body',0.3));
    dY(out,-0.11,-1, -0.13,0.13, 0.54,0.86,'body',-1.2);
    dY(out,-0.11,-1, -0.02,0.02, 0.66,0.72,'iron',0.8, 0.07);
    dY(out,-0.11,-1, -0.12,0.0, 0.87,0.91,'gold',0.6, 0.06);                   // bell plate
    tube(out,[0.2,0.06,0.62],[0.2,0.06,0.66], 0.14, 12,'iron',-0.2,null,false); // slack coil
    tube(out,[0.2,0.06,0.66],[0.2,0.06,0.70], 0.12, 12,'iron',-0.1,null,false);
    beam(out,[0.2,0.06,0.62],[0.06,0.06,0.2], 0.016,'iron',-0.1);
    if(s.gravel) apron(out, 0.4, 'gravel');
  }

  function pCrossBox(out,s){                                                 // cross-connect cabinet
    const w = 0.9 + (s.len||0)*0.5;
    pad(out, w+0.4, 0.82, 0.16, 'concrete', 0);
    box(out, -w/2,w/2, -0.26,0.26, 0.16,1.26, 'body', 0);
    out.push(F([[-w/2-0.03,-0.29,1.26],[w/2+0.03,-0.29,1.26],[w/2+0.03,0.29,1.38],[-w/2-0.03,0.29,1.38]],'body',0.3));
    for(const sy of [-1,1]) dY(out, sy*0.26, sy, -w/2+0.07,w/2-0.07, 0.3,1.18,'body', sy>0?-0.7:-1.2);
    dY(out,-0.26,-1, -0.05,0.05, 0.64,0.8,'iron',0.8, 0.08);
    dY(out,-0.26,-1, w/2-0.32,w/2-0.1, 1.06,1.16,'gold',0.6, 0.06);
    for(const sx of [-1,1]) pipe(out, sx*(w/2-0.18),0.1, 0.05, 0.02,0.2, 'galv', 0.05, 8);
    tube(out,[w/2+0.02,0.14,0.9],[w/2+0.06,0.14,0.9], 0.14, 12,'iron',-0.2,null,false);  // slack coil
    if(s.gravel) slab(out, [[-(w/2+0.34),-0.56],[w/2+0.34,-0.56],[w/2+0.34,0.56],[-(w/2+0.34),0.56]], 0.012,'gravel',-0.15, gravelTex());
  }
  function pAlarmBox(out,s){
    const ht = 1.05 + (s.len||0)*0.35;
    prismT(out, 0,0, 0.15, 0,0.12, 10, 'concrete', 0);
    pipe(out, 0,0, 0.055, 0.1, ht, 'iron', 0.02, 10);
    for(const z of [0.16, ht-0.12]) tube(out,[0,0,z],[0,0,z+0.05], 0.072, 10,'iron',0.2,null,false);
    box(out, -0.16,0.16, -0.12,0.12, ht, ht+0.42, 'body', 0);
    out.push(F([[-0.18,-0.14,ht+0.42],[0.18,-0.14,ht+0.42],[0.18,0.14,ht+0.5],[-0.18,0.14,ht+0.5]],'body',0.32));
    dY(out,-0.12,-1, -0.12,0.12, ht+0.06,ht+0.34,'body',-1.2);                // door
    dY(out,-0.12,-1, -0.06,0.06, ht+0.14,ht+0.28,'glass',0.5, 0.07);          // glass panel
    dY(out,-0.12,-1, -0.04,0.04, ht+0.36,ht+0.41,'gold',0.7, 0.06);
    tube(out,[0,-0.12,ht+0.2],[0,-0.22,ht+0.2], 0.02, 6,'brass',0.3);         // pull lever
    beam(out,[0.06,0.05,ht],[0.06,0.05,0.14], 0.014,'galv',0.1);
    if(s.gravel) apron(out, 0.32, 'gravel');
  }
  function pRadioMast(out,s){
    const ht = 7.0 + (s.len||0)*5.0, sp = 0.36, R=5;
    const A=(i)=>i/3*Math.PI*2-Math.PI/2;
    const P=(i,t)=>{ const a=A(i), k=sp*(1-0.45*t); return [Math.cos(a)*k, Math.sin(a)*k, 0.14+(ht-0.14)*t]; };
    for(let i=0;i<3;i++){ const a=A(i), x=Math.cos(a)*sp, y=Math.sin(a)*sp;
      prismT(out, x,y, 0.14, 0,0.16, 8, 'concrete', 0);
      tube(out, [x,y,0.14],[Math.cos(a)*sp*0.55, Math.sin(a)*sp*0.55, ht], 0.042, 8, 'galv', 0.05); }
    for(let r=1;r<=R;r++){ const t0=(r-1)/R, t1=r/R;
      for(let i=0;i<3;i++){ const j=(i+1)%3;
        beam(out, P(i,t1), P(j,t1), 0.018,'galv',0.15);
        beam(out, P(i,t0), P(j,t1), 0.015,'galv',0.02); } }
    pipe(out, 0,0, 0.028, ht, ht+1.1, 'galv', 0.1, 8);                        // whip
    box(out, -0.1,0.1, -0.1,0.1, ht+0.02,ht+0.14, 'iron', 0.1);
    prismT(out, 0,0, 0.085, ht+0.14,ht+0.3, 8, 'glow', 0.2);                  // obstruction lamp
    box(out, -0.62,0.62, -0.05,0.05, ht*0.76, ht*0.76+0.06, 'galv', 0.2);     // dipole bar
    for(const sx of [-1,1]) prismT(out, sx*0.58,0, 0.028, ht*0.76+0.06, ht*0.76+0.5, 6, 'galv', 0.15);
    box(out, -0.3,0.3, -0.56,-0.26, 0.16,0.9, 'body', 0.02);                  // equipment box
    out.push(F([[-0.32,-0.58,0.9],[0.32,-0.58,0.9],[0.32,-0.24,0.98],[-0.32,-0.24,0.98]],'body',0.3));
    dY(out,-0.56,-1, -0.24,0.24, 0.3,0.82,'body',-1.2);
    dY(out,-0.56,-1, -0.03,0.03, 0.52,0.6,'iron',0.8, 0.07);
    if(s.guy!==false) for(let i=0;i<3;i++){ const a=A(i)+0.52, x=Math.cos(a)*2.7, y=Math.sin(a)*2.7;
      beam(out,[Math.cos(a)*sp*0.6, Math.sin(a)*sp*0.6, ht*0.72],[x,y,0.12], 0.02,'iron',-0.5);
      prismT(out, x,y, 0.05, 0,0.24, 6, 'galv', 0.1); }
    if(s.gravel) apron(out, 0.72, 'gravel');
  }

  const PROPS = {
    powerPole:      { label:'Power pole',    cat:'power',  foot:[0.5,0.5],  run:true,  variants:['crossarm','transformer','lamp arm'], def:{metal:'galv'},          build:pPowerPole,
                      ties:(s)=>{ const ht=7.4+(s.len||0)*4.2, z=ht-0.62+0.35;
                        return { wires:[[-1.02,0,z],[0,0,z],[1.02,0,z]], secondary:[[-0.24,-0.2,ht*0.62+0.2],[0,-0.2,ht*0.62+0.2],[0.24,-0.2,ht*0.62+0.2]],
                                 lamp: s.variant===2 ? [[1.72,0,ht-1.32]] : [] }; } },
    hFrame:         { label:'H-frame',       cat:'power',  foot:[2.9,0.6],  run:true,  variants:['twin circuit'],                     def:{metal:'galv'},          build:pHFrame,
                      ties:(s)=>{ const ht=6.8+(s.len||0)*3.6, z1=ht-0.55+0.36, z2=ht-1.62+0.36, sp=1.15;
                        return { wires:[[-sp-0.26,0,z1],[-0.4,0,z1],[0.4,0,z1],[sp+0.26,0,z1]],
                                 secondary:[[-sp+0.3,0,z2],[0,0,z2],[sp-0.3,0,z2]] }; } },
    padTransformer: { label:'Pad transformer',cat:'power', foot:[1.9,1.55], run:true,  variants:['single'],                          def:{paint:'sage'},          build:pPadTransformer },
    switchgear:     { label:'Switch cabinet',cat:'power',  foot:[2.2,1.7],  run:true,  variants:['four-way'],                         def:{paint:'sage'},          build:pSwitchgear },
    pedestal:       { label:'Junction ped.', cat:'power',  foot:[0.78,0.62],           variants:['hooded'],                          def:{paint:'sage'},          build:pPedestal },
    genset:         { label:'Standby set',   cat:'power',  foot:[1.8,1.05], run:true,  variants:['enclosed'],                        def:{paint:'cream'},         build:pGenset },
    serviceMast:    { label:'Service mast',  cat:'power',  foot:[0.9,0.5],  run:true,  variants:['meter + head'],                    def:{},                      build:pServiceMast,
                      ties:(s)=>({ drop:[[0,0.10,4.0+(s.len||0)*1.4]] }) },
    meterBank:      { label:'Meter bank',    cat:'power',  foot:[1.64,0.5],            variants:['three-gang','five-gang'],          def:{},                      build:pMeterBank,
                      ties:(s)=>({ drop:[[0,0.09,1.92]] }) },
    guyAnchor:      { label:'Guy anchor',    cat:'power',  foot:[1.4,1.3],  run:true,  variants:['rod & guard'],                     def:{},                      build:pGuyAnchor,
                      ties:(s)=>{ const g=2.2+(s.len||0)*1.6; return { wires:[[-g*0.34,-g*0.3,g]] }; } },
    yardLight:      { label:'Yard light',    cat:'light',  foot:[0.5,0.5],  run:true,  variants:['cobra head'],                      def:{},                      build:pYardLight,
                      ties:(s)=>{ const ht=5.6+(s.len||0)*3.4; return { lamp:[[-1.53,0,ht+0.06]] }; } },
    streetLamp:     { label:'Street lamp',   cat:'light',  foot:[1.0,0.5],  run:true,  variants:['pendant lantern','acorn globe'],    def:{},                      build:pStreetLamp,
                      ties:(s)=>{ const ht=3.6+(s.len||0)*1.8;
                        return { lamp: s.variant===1?[[0,0,ht+0.26]]:[[-0.8,0,ht-0.13]], drop:[[0,0.075,ht-0.5]] }; } },
    floodMast:      { label:'Flood mast',    cat:'light',  foot:[1.9,0.7],  run:true,  variants:['three-head'],                      def:{},                      build:pFloodMast,
                      ties:(s)=>{ const ht=6.0+(s.len||0)*4.0;
                        return { lamp:[[-0.62,-0.3,ht+0.03],[0,-0.3,ht+0.03],[0.62,-0.3,ht+0.03]] }; } },
    beacon:         { label:'Harbour beacon',cat:'light',  foot:[1.4,1.4],  run:true,  variants:['tripod pile'],                     def:{paint:'white'},         build:pBeacon,
                      ties:(s)=>{ const ht=2.6+(s.len||0)*2.2; return { lamp:[[0,0,ht+0.48]] }; } },
    hydrant:        { label:'Fire hydrant',  cat:'water',  foot:[0.62,0.62],           variants:['two-way'],                         def:{paint:'red'},           build:pHydrant },
    yardHydrant:    { label:'Yard hydrant',  cat:'water',  foot:[0.42,0.42], run:true, variants:['frost-free'],                      def:{},                      build:pYardHydrant },
    standpipe:      { label:'Standpipe',     cat:'water',  foot:[1.2,1.7],             variants:['tap & trough'],                    def:{},                      build:pStandpipe },
    handPump:       { label:'Hand pump',     cat:'water',  foot:[0.88,0.8],            variants:['pitcher pump'],                    def:{},                      build:pHandPump },
    wellCap:        { label:'Well head',     cat:'water',  foot:[0.86,0.86],           variants:['drilled casing','dug well lid'],   def:{},                      build:pWellCap },
    curbStop:       { label:'Curb stop',     cat:'water',  foot:[0.46,0.46],           variants:['road box'],                        def:{},                      build:pCurbStop },
    valveVault:     { label:'Valve vault',   cat:'water',  foot:[1.3,1.1],             variants:['twin lid'],                        def:{},                      build:pValveVault },
    waterTank:      { label:'Water tank',    cat:'water',  foot:[2.2,2.2],  run:true,  variants:['timber stand'],                    def:{},                      build:pWaterTank },
    cistern:        { label:'Cistern',       cat:'water',  foot:[2.4,1.9],  run:true,  variants:['buried box'],                      def:{},                      build:pCistern },
    manhole:        { label:'Manhole',       cat:'sewer',  foot:[1.44,1.44],           variants:['sanitary','road patch'],           def:{},                      build:pManhole },
    cleanout:       { label:'Cleanout',      cat:'sewer',  foot:[0.44,0.5],            variants:['capped'],                          def:{},                      build:pCleanout },
    ventStack:      { label:'Vent stack',    cat:'sewer',  foot:[0.6,0.34], run:true,  variants:['candy cane'],                      def:{},                      build:pVentStack },
    septicLids:     { label:'Septic lids',   cat:'sewer',  foot:[2.6,1.24], run:true,  variants:['twin riser'],                      def:{paint:'sage'},          build:pSepticLids },
    catchBasin:     { label:'Catch basin',   cat:'sewer',  foot:[1.24,1.24],           variants:['curb inlet','with apron'],         def:{},                      build:pCatchBasin },
    trenchDrain:    { label:'Trench drain',  cat:'sewer',  foot:[0.7,3.8],  run:true,  variants:['grated channel','with apron'],     def:{},                      build:pTrenchDrain },
    liftStation:    { label:'Lift station',  cat:'sewer',  foot:[2.7,1.7],             variants:['well + panel'],                    def:{paint:'sage'},          build:pLiftStation },
    outfall:        { label:'Outfall',       cat:'sewer',  foot:[1.9,1.7],             variants:['headwall & flap'],                 def:{},                      build:pOutfall },
    culvert:        { label:'Culvert end',   cat:'sewer',  foot:[3.0,4.3],  run:true,  variants:['corrugated'],                      def:{},                      build:pCulvert },
    oilTank:        { label:'Oil tank',      cat:'fuel',   foot:[1.12,0.66],           variants:['275 gal','twin'],                  def:{paint:'greyShingle'},   build:pOilTank },
    fillPipes:      { label:'Fill & vent',   cat:'fuel',   foot:[0.7,0.42],            variants:['pair'],                            def:{},                      build:pFillPipes },
    propaneTank:    { label:'Propane tank',  cat:'fuel',   foot:[2.6,0.9],  run:true,  variants:['500 gal','bottle pair'],           def:{paint:'white'},         build:pPropaneTank },
    bulkTank:       { label:'Bulk tank',     cat:'fuel',   foot:[3.1,2.9],  run:true,  variants:['vertical shell'],                  def:{paint:'white'},         build:pBulkTank },
    fuelPump:       { label:'Fuel pump',     cat:'fuel',   foot:[0.68,0.58],           variants:['dockside'],                        def:{paint:'red'},           build:pFuelPump },
    drumRack:       { label:'Drum rack',     cat:'fuel',   foot:[1.9,1.5],             variants:['two drum','three drum'],           def:{paint:'rustOrange'},    build:pDrumRack },
    coalBin:        { label:'Coal bin',      cat:'fuel',   foot:[2.1,2.0],  run:true,  variants:['plank bin'],                       def:{},                      build:pCoalBin },
    telecomPed:     { label:'Telecom ped.',  cat:'telecom',foot:[0.56,0.44],           variants:['hooded'],                          def:{paint:'greyShingle'},   build:pTelecomPed,
                      ties:(s)=>({ drop:[[0,0.06,0.98]] }) },
    crossBox:       { label:'Cross-connect', cat:'telecom',foot:[1.3,0.8],  run:true,  variants:['cross-connect'],                   def:{paint:'greyShingle'},   build:pCrossBox,
                      ties:(s)=>({ drop:[[0,0.28,1.32]] }) },
    alarmBox:       { label:'Call box',      cat:'telecom',foot:[0.42,0.42], run:true, variants:['pull box'],                        def:{paint:'red'},           build:pAlarmBox,
                      ties:(s)=>{ const ht=1.05+(s.len||0)*0.35; return { drop:[[0,0.12,ht+0.4]] }; } },
    radioMast:      { label:'Radio mast',    cat:'telecom',foot:[5.4,5.4],  run:true,  variants:['guyed lattice'],                   def:{paint:'cream'},         build:pRadioMast,
                      ties:(s)=>{ const ht=7.0+(s.len||0)*5.0;
                        return { lamp:[[0,0,ht+0.23]], drop:[[0,-0.5,0.9]] }; } },
  };

  function resolve(name, opts){ opts=opts||{}; const P=PROPS[name]||PROPS.powerPole;
    const g=(k,d)=> opts[k]!=null?opts[k] : (P.def[k]!=null?P.def[k]:d);
    return { name, paint: opts.paint!==undefined?opts.paint:(P.def.paint||null), metal:g('metal','galv'),
      variant: opts.variant!=null?opts.variant:0, len: opts.len!=null?opts.len:(P.run?0.4:0),
      weather: opts.weather!=null?opts.weather:0.35, gravel: opts.gravel!=null?!!opts.gravel:true,
      guy: opts.guy!=null?!!opts.guy:true, night:!!opts.night,
      // ADR 0031 A/B arm: carried through UNNORMALISED so `undefined` still means "use
      // KEYLINE_DEFAULT" at the ring pass. Do not coerce it to a bool here.
      outline: opts.outline }; }

  function makeMats(s){ const wx=s.weather, night=s.night;
    const grime=r=>r.map(c=>mix(desat(c,wx*0.22),'#3a3128',wx*0.13));
    const rust =r=>r.map(c=>mix(c,'#6d3417',wx*0.26));
    const cool =r=>night?r.map(c=>mix(desat(c,0.28),'#1b2740',0.38)):r;
    const t=r=>cool(grime(r)), tm=r=>cool(rust(grime(r)));
    const paint = s.paint?(BODY[s.paint]||BODY.sage):ALUM;
    const metal = s.metal==='alum'?ALUM : s.metal==='iron'?IRON : GALV;
    return { body:{ramp:t(paint)}, pole:{ramp:t(POLE)}, wood:{ramp:t(WOOD)},
      iron:{ramp:tm(IRON)}, galv:{ramp:tm(metal)}, alum:{ramp:t(ALUM)},
      concrete:{ramp:t(CONCRETE)}, copper:{ramp:tm(COPPER)}, brass:{ramp:tm(BRASS)},
      glassins:{ramp:t(GLASSINS)}, porc:{ramp:t(PORC)}, gravel:{ramp:t(GRAVEL)}, asphalt:{ramp:t(ASPHALT)},
      glass:{ramp: night?GLASSN:GLASSD}, glow:{ramp: night?GLOW:['#5f6a5e','#8d9a8b','#b6c2b0','#d3ddcb']} };
  }

  // ---- rasteriser (identical recipe; single object → always keyline) ----
  function paint(faces, B, MATS){ const N=W*H;
    const zbuf=new Float32Array(N).fill(Infinity), dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null), ibuf=new Int16Array(N), nbuf=new Array(N).fill(null);
    for(const f of faces){ const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]); let sh=shadeOf(n, B.se, B.ce);
      if(sh<0 && (f.b<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx=sh*GAIN+BIAS+f.b; const M=MATS[f.mat]||MATS.body, ramp=M.ramp, tex=f.tex, uv=f.uv, flat=f.flat;
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
          if(deff<zbuf[i]){ zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat; let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi+=tex(uu,vv); }
            let idx; if(flat){ idx=Math.round(fi); } else { const base=Math.floor(fi); idx=base+((fi-base)>BAYER[x&3][y&3]?1:0); }
            idx=Math.max(0,Math.min(ramp.length-1,idx)); rbuf[i]=ramp; ibuf[i]=idx; } }
      }
    }
    return { rbuf, ibuf, nbuf, dep };
  }
  function post(bufs, s){ const { rbuf, ibuf, nbuf, dep }=bufs, N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx,ny=y+dy; if(nx>=W||ny>=H) continue; const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j; out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; } } }
    if(s.weather>0.02){ const rnd=mulberry32(2207|((s.variant*97)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='pole'||m==='wood'||m==='body'||m==='concrete'||m==='asphalt'||m==='galv') && rnd()<s.weather*0.055)
          out[i]=rbuf[i][Math.max(0,ibuf[i]-1)]; } }
    if(s.night){ for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x; if(nbuf[i]!=='glow') continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,1]]){ const j=(y+dy)*W+(x+dx); if(out[j]&&nbuf[j]!=='glow') out[j]=mix(out[j],'#f2c25e',0.3); } } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; } }
    // KEYLINE — RETIRED BY DEFAULT (ADR 0031). Pure ring deletion: every pixel this pass writes is
    // an EMPTY neighbour of the silhouette, so switching it off changes no painted pixel of any
    // prop. What holds the edge instead is the pole/box's own dark side.
    if(s.outline === undefined ? KEYLINE_DEFAULT : s.outline !== false){
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let touch=false;
        for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
        if(touch) out[i]=KEY; }
    }
    return out;
  }
  function toRGBA(cols){ const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; } const [r,g,bl]=hex2rgb(c); rgba[i*4]=r;rgba[i*4+1]=g;rgba[i*4+2]=bl;rgba[i*4+3]=255; }
    return rgba;
  }

  function render(name, dir, opts){ opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const s=resolve(name,opts), B=camBasis({dir,elev:opts.elev}), MATS=makeMats(s), out=[];
    (PROPS[name]||PROPS.powerPole).build(out, s);
    return toRGBA(post(paint(out,B,MATS), s));
  }
  function footprint(name, opts){ const s=resolve(name,opts), P=PROPS[name]||PROPS.powerPole;
    const k = P.run ? (1 + s.len*(name==='culvert'?0.55:name==='trenchDrain'?0.6:name==='propaneTank'?0.32:name==='bulkTank'?0.3:
      name==='radioMast'?0.24:name==='coalBin'?0.3:name==='guyAnchor'?0.5:name==='waterTank'?0.1:name==='septicLids'?0.42:0.2)) : 1;
    return { w:P.foot[0]*k, d:P.foot[1]*k }; }
  function height(name, opts){ const s=resolve(name,opts);
    switch(name){ case 'powerPole': return 7.4+s.len*4.2; case 'yardLight': return 5.6+s.len*3.4+0.3;
      case 'hFrame': return 6.8+s.len*3.6+0.35; case 'switchgear': return 1.7;
      case 'meterBank': return 1.94; case 'guyAnchor': return 2.2+s.len*1.6;
      case 'streetLamp': return 3.6+s.len*1.8+(s.variant===1?0.66:0.16);
      case 'floodMast': return 6.0+s.len*4.0+0.2; case 'beacon': return 2.6+s.len*2.2+0.86;
      case 'serviceMast': return 4.0+s.len*1.4; case 'waterTank': return 1.9+s.len*1.7+1.7;
      case 'standpipe': return 1.24; case 'handPump': return 1.28; case 'valveVault': return 0.9;
      case 'cistern': return 1.94; case 'trenchDrain': return 0.16; case 'liftStation': return 1.62;
      case 'outfall': return 1.0; case 'bulkTank': return 2.4+s.len*2.0+0.44;
      case 'fuelPump': return 1.94; case 'drumRack': return 1.24; case 'coalBin': return 1.2;
      case 'crossBox': return 1.38; case 'alarmBox': return 1.05+s.len*0.35+0.5;
      case 'radioMast': return 7.0+s.len*5.0+1.1;
      case 'ventStack': return 2.0+s.len*1.3+0.2; case 'oilTank': return 2.0;
      case 'propaneTank': return s.variant===1?1.4:1.2; case 'hydrant': return 0.96;
      case 'padTransformer': return 1.3; case 'genset': return 1.34; case 'pedestal': return 1.0;
      case 'telecomPed': return 1.0; case 'manhole': return 0.14; case 'culvert': return 0.75;
      case 'catchBasin': return 0.34; case 'septicLids': return 0.47; case 'cleanout': return 0.5;
      case 'curbStop': return 0.12; case 'wellCap': return s.variant===1?0.71:0.72;
      case 'yardHydrant': return 0.9+s.len*0.4+0.33; case 'fillPipes': return 0.98; default: return 1; } }
  function ties(name, opts){ const s=resolve(name,opts), P=PROPS[name]||PROPS.powerPole;
    const T=P.ties?P.ties(s):{}; return { wires:T.wires||[], secondary:T.secondary||[], lamp:T.lamp||[], drop:T.drop||[] }; }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }
  function anchors(name, dir, opts){ opts=opts||{}; const T=ties(name,opts), e=opts.elev;
    const P=(a)=>a.map(p=>{ const q=project(dir,p,e); return { x:q.x, y:q.y, m:p }; });
    return { wires:P(T.wires), secondary:P(T.secondary), lamp:P(T.lamp), drop:P(T.drop),
      foot:footprint(name,opts), height:height(name,opts) }; }
  function list(){ return Object.keys(PROPS); }

  root.UtilityIso = { W, H, PX, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    POLE, WOOD, IRON, GALV, ALUM, CONCRETE, COPPER, BRASS, GLASSINS, PORC, GRAVEL, ASPHALT, BODY, GLOW, KEY,
    KEYLINE_DEFAULT,
    PROPS, CATS, list, footprint, height, ties, anchors, render, project };
})(typeof globalThis!=='undefined'?globalThis:window);
