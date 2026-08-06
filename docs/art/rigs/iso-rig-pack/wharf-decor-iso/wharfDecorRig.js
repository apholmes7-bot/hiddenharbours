/* Hidden Harbours — parametric ISO WHARF ACCESSORIES & DECOR rig (ADR-0006 bake pipeline, SAME
   turntable + camera + shading as utilityIsoRig.js / wharfBuildingRig.js / interiorPropRig.js / the
   fleet). This is the DRESSING of a working wharf: the gear, the handling kit, the lifting tackle,
   the safety board, the drying flakes and the odds that pile up against a shed wall. The wharf tile
   kit gives the deck, cleats, bollards, rails, ladders, fenders and gangways; the services rig gives
   poles, lamps, hydrants and tanks; this rig gives everything a fisherman leaves lying about.

   Each piece is one parametric 3D model baked through the SHARED 3/4 camera: 45deg steps, elev 40deg,
   flat-facet shading from the fixed upper-LEFT key, z-buffered, ordered dither, per-face uv texture,
   depth-edge darkening, 1px keyline, NO AA. 32 px = 1 m. All 8 facings fall out of one model.

   PROP ORIGIN: ground-centre of the footprint (== the utility / house / room pivot), so
   WharfDecor.render(name,dir) drops straight onto a wharf deck tile with no offset maths. At the
   default SE view +Y is the near side, so the wall-hung pieces (buoy wall, rope hanks, ring station,
   fire cabinet, notice board, tide staff, harbour sign) carry their backboard toward -Y — authored
   front-to--Y and mirrored — which keeps the shed wall behind them and the face readable.

   MOUNTS: mounts(name,opts) returns attach points in METRES, anchors(name,dir,opts) bakes the same
   points to cell pixels:
     deck   ground contact points   — where the piece touches the planking
     rail   rail-clamp points       — pieces that hang off the pipe/wood railing of the wharf kit
     wall   backboard points        — the plane that must sit against a shed wall
     post   post/pile attach points — pieces bolted to a pile head or a lamp post
     hook   lift points             — the block, hook or scale eye a load hangs from
   so the compositor can snap a ring station to a rail, hang a rope hank on a shed and sling a tote
   from a davit without per-instance nudging.

   Each piece reads a resolved spec:
     paint:BODY key|null   metal:'galv'|'alum'|'iron'   variant:int   len:0..1 (stack / run scale)
     loaded:bool (totes with fish, barrels with bait, crates with rope, racks full)
     tarp:bool (canvas thrown over)   weather:0..1 (grime + rust + sun-silvered timber)   night:bool
   Exposes globalThis.WharfDecor = { W,H,PX,pivot,order,defaultElev, palettes..., PROPS,CATS, list(),
     footprint(), height(), mounts(), anchors(), render(name,dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 420, H = 520, cx = 210, groundY = 420;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;

  // ---- palettes (harbour master ramps, shared with the houses, services & fleet) ----
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
  const WOOD     = ['#553d23','#6b4e30','#7f603b','#957749','#ab8d5f','#c1a578']; // sawn timber
  const PLANK    = ['#454037','#565045','#676054','#7a7265','#8d8477','#a1988a']; // sun-silvered deck plank
  const POLE     = ['#33291f','#42342a','#523f33','#63503f','#75604c','#8a7460']; // creosoted pile
  const IRON     = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970'];
  const GALV     = ['#565b5f','#6d7276','#868b8f','#a0a5a8','#bbbfc1','#d6d9da'];
  const ALUM     = ['#3f4448','#525a5e','#687175','#7f888c','#98a0a3','#b2b9bb'];
  const CONCRETE = ['#55564f','#686962','#7c7d74','#919287','#a6a79b','#bbbcae'];
  const COPPER   = ['#4a2413','#68361c','#8a4c26','#a86538','#c4834f','#dba86f'];
  const BRASS    = ['#59410f','#795819','#977326','#b28e3b','#c8a757','#dcc17d'];
  const ROPE     = ['#5a4526','#71592f','#8a6f3d','#a2874e','#b89f66','#cdb686']; // manila
  const POLYR    = ['#1b3550','#26496a','#345f85','#45789e','#5c92b4','#7aadc9']; // blue poly line
  const NET      = ['#1d2a1e','#2a3a29','#374b34','#465e41','#57724f','#6a875f']; // green twine / vinyl wire
  const CANVAS   = ['#3d4038','#4b4f45','#5b6053','#6d7263','#7f8474','#929686']; // tarpaulin
  const FISH     = ['#5f5748','#776c5a','#8f8270','#a69887','#bcae9d','#d0c3b3']; // split & salted
  const ICE      = ['#4c5f66','#638089','#7fa0a8','#9dbcc2','#bcd6d9','#dcecee'];
  const RUBBER   = ['#141517','#1d1f22','#282b2f','#34383d','#43484d','#54595f'];
  const FOAM     = ['#5c3a12','#7d5019','#9c6a24','#b8853a','#cda058','#e0bd80']; // orange trawl float
  const SLATE    = ['#181d1e','#222829','#2d3436','#3a4244','#48524f','#5a635f']; // chalkboard
  const PAPER    = ['#8a8577','#a09a89','#b5ae9c','#c8c1ad','#d9d2bd','#e8e1cb'];
  const GRAVEL   = ['#3b3a33','#4b493f','#5c5a4d','#6f6c5c','#83806e','#979382'];
  const ASPHALT  = ['#1e2124','#272b2f','#33383c','#41474b','#51585c','#646b6f'];
  const SALT     = ['#7d7f78','#93958c','#a8aa9f','#bcbeb2','#cfd1c4','#e2e4d6'];
  const GLASSD   = ['#7d9ea6','#a1c2c6','#cfe6e8'], GLASSN = ['#141d2b','#233247','#3d5570'];
  const GLOW     = ['#7a5a18','#c09a2c','#efd06a','#fdf0b6'];
  const KEY      = '#1a1c22';
  const BUOYSET  = ['red','white','gold','teal','blue'];   // the mixed wall of a buoy rack

  // ---- shading (identical recipe to utilityIsoRig / interiorPropRig) ----
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
  // generic faceted TUBE between two 3D points — pipes, poles, rope runs, drums, arms, guys
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
  // torus about the z axis — ring buoys, rope coils, tyres, wire hoops
  function torusZ(out, x,y,z, R, r, n, m, mat, b){
    const P=(i,j)=>{ const th=i/n*Math.PI*2, ph=j/m*Math.PI*2, rr=R+r*Math.cos(ph);
      return [x+Math.cos(th)*rr, y+Math.sin(th)*rr, z+r*Math.sin(ph)]; };
    for(let i=0;i<n;i++) for(let j=0;j<m;j++){ const i2=(i+1)%n, j2=(j+1)%m;
      out.push(F([P(i,j),P(i2,j),P(i2,j2),P(i,j2)], mat, (b||0)+Math.sin(j/m*Math.PI*2)*0.22)); }
  }
  function decalY(out, yv, ny, xs,xe, z0,z1, mat, b, tex, flat, db){ const e=0.02*ny, uw=xe-xs, uh=z1-z0;
    const P = ny>0 ? [[xs,yv+e,z0],[xe,yv+e,z0],[xe,yv+e,z1],[xs,yv+e,z1]] : [[xe,yv+e,z0],[xs,yv+e,z0],[xs,yv+e,z1],[xe,yv+e,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat)); }
  function decalX(out, xv, nx, ys,ye, z0,z1, mat, b, tex, flat, db){ const e=0.02*nx, uw=ye-ys, uh=z1-z0;
    const P = nx>0 ? [[xv+e,ye,z0],[xv+e,ys,z0],[xv+e,ys,z1],[xv+e,ye,z1]] : [[xv+e,ys,z0],[xv+e,ye,z0],[xv+e,ye,z1],[xv+e,ys,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat)); }
  const dY=(out,y,n,a,b2,z0,z1,mat,bias,db)=>decalY(out,y,n,a,b2,z0,z1,mat,bias,null,true,db==null?0.06:db);
  const dX=(out,x,n,a,b2,z0,z1,mat,bias,db)=>decalX(out,x,n,a,b2,z0,z1,mat,bias,null,true,db==null?0.06:db);
  function decalZ(out, zv, xs,xe, ys,ye, mat, b, tex, flat, db){
    out.push(F([[xs,ys,zv],[xe,ys,zv],[xe,ye,zv],[xs,ye,zv]], mat, b||0, db!=null?db:0.05,
      tex?[[0,0],[xe-xs,0],[xe-xs,ye-ys],[0,ye-ys]]:null, tex||null, flat)); }
  function pad(out, w,d,h, mat, b){ const m=Math.min(0.05,h*0.4);
    box(out,-w/2,w/2,-d/2,d/2, 0,h-m, mat, b||0, null, true);
    box(out,-w/2+m,w/2-m,-d/2+m,d/2-m, h-m,h, mat, (b||0)+0.2); }

  // ---- textures ----
  function gravelTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*13)|0, Math.floor(v*13)|0); return h<0.3?-1:(h>0.86?1:0); }; }
  function corrTex(p){ p=p||0.13; return (u,v)=>{ const f=((v%p)+p)%p; return f<p*0.42?-1:(f>p*0.86?1:0); }; }
  function grainTex(p){ p=p||0.3; return (u,v)=>{ const f=((v%p)+p)%p; if(f<0.03) return -2;
    return hash2(Math.floor(v/p)|0, Math.floor(u*3)|0)<0.5?0:-1; }; }
  function plankTex(p){ p=p||0.22; return (u,v)=>{ const f=((u%p)+p)%p; if(f<0.026) return -2;
    return hash2(Math.floor(u/p)|0, Math.floor(v*2.4)|0)<0.42?-1:0; }; }
  function meshTex(c){ c=c||0.062; return (u,v)=>{ const fu=((u%c)+c)%c, fv=((v%c)+c)%c;
    return (fu<0.019||fv<0.019)?1:-3; }; }
  function netTex(c){ c=c||0.034; return (u,v)=>{ const fu=((u%c)+c)%c, fv=((v%c)+c)%c;
    return (fu<0.013||fv<0.013)?0:-2; }; }
  function slatTex(p){ p=p||0.085; return (u,v)=>{ const f=((u%p)+p)%p; return f<p*0.22?-2:(f>p*0.8?0:-1); }; }
  function canvasTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*24)|0, Math.floor(v*24)|0); return h<0.24?-1:(h>0.9?1:0); }; }
  function wickerTex(){ const c=0.055; return (u,v)=>{ const fv=((v%c)+c)%c;
    return fv<c*0.3?-2:(hash2(Math.floor(u*22)|0, Math.floor(v/c)|0)<0.45?-1:0); }; }
  function scaleTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*30)|0, Math.floor(v*30)|0); return h<0.3?-1:(h>0.82?1:0); }; }
  function ropeTex(){ const p=0.055; return (u,v)=>{ const f=(((u+v*0.6)%p)+p)%p; return f<p*0.3?-1:(f>p*0.82?1:0); }; }
  function rustTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*9)|0, Math.floor(v*9)|0); return h<0.22?-2:(h>0.88?1:0); }; }

  // ---- shared sub-assemblies ----
  // scattered heap filling a volume — fish in a tote, ice, salt, coal, tangled net, firewood
  function heap(out, x,y,z, w,d,h, mat, seed, n, tex, b){
    const rnd=mulberry32(seed|0);
    for(let i=0;i<n;i++){ const a=rnd(), c=rnd(), e=rnd();
      const px=x+(a*2-1)*w*0.5, py=y+(c*2-1)*d*0.5;
      const fall=1-Math.min(1,Math.hypot((px-x)/(w*0.5+1e-5),(py-y)/(d*0.5+1e-5)));
      const rr=h*(0.3+e*0.42), top=z+h*0.42*fall+rr*0.5;
      prismT(out, px,py, rr*0.7, z, Math.max(z+0.035, top), 6, mat, (b||0)-0.3+rnd()*0.7, tex||null); }
  }
  // spindle buoy, lying or standing: shaft + swelled body + a painted band
  function buoy(out, p0, p1, r, mat, band, b){
    tube(out, p0, p1, r, 8, mat, b||0);
    const t=(k)=>[p0[0]+(p1[0]-p0[0])*k, p0[1]+(p1[1]-p0[1])*k, p0[2]+(p1[2]-p0[2])*k];
    tube(out, t(0.3), t(0.68), r*1.5, 8, mat, (b||0)+0.12);
    if(band) tube(out, t(0.44), t(0.56), r*1.58, 8, band, (b||0)+0.3);
    tube(out, t(0.94), t(1.16), r*0.42, 6, 'iron', -0.2);
  }
  // one lobster trap — wire (vinyl-coated mesh) or wood (bowed lath)
  function trapUnit(out, x,y,z, w,d,h, wood, b, rnd){
    const hw=w/2, hd=d/2, sk=rnd?(rnd()-0.5)*0.03:0;
    if(wood){
      box(out, x-hw,x+hw, y-hd+sk,y+hd+sk, z,z+h*0.62, 'wood', b||0, slatTex(0.075), true);
      tube(out, [x-hw,y+sk,z+h*0.62],[x+hw,y+sk,z+h*0.62], hd*0.98, 7, 'wood', (b||0)+0.16, slatTex(0.09), false);
      for(const sx of [-hw+0.06, 0, hw-0.06]) beam(out,[x+sx,y-hd+sk,z+h*0.6],[x+sx,y+hd+sk,z+h*0.6],0.016,'wood',(b||0)+0.3);
      dY(out, y-hd+sk, -1, x-hw*0.34, x+hw*0.34, z+h*0.12, z+h*0.46, 'net', -1.0, 0.05);
    } else {
      box(out, x-hw,x+hw, y-hd+sk,y+hd+sk, z+0.03,z+h, 'net', b||0, meshTex(), false);
      // galvanised frame: bottom + top rails all round, standing slightly proud so a stacked
      // trap reads as a separate trap rather than one tall mesh block
      for(const zz of [z+0.05, z+h-0.03]){
        for(const sy of [-hd+sk, hd+sk]) beam(out,[x-hw-0.02,y+sy,zz],[x+hw+0.02,y+sy,zz],0.028,'galv',(b||0)+0.45);
        for(const sx of [-hw, hw]) beam(out,[x+sx,y-hd+sk-0.02,zz],[x+sx,y+hd+sk+0.02,zz],0.026,'galv',(b||0)+0.3); }
      for(const sx of [-hw,hw]) for(const sy of [-hd+sk,hd+sk]) beam(out,[x+sx,y+sy,z+0.04],[x+sx,y+sy,z+h-0.02],0.024,'galv',(b||0)+0.2);
      dY(out, y-hd+sk, -1, x-hw*0.3, x+hw*0.3, z+h*0.28, z+h*0.76, 'net', -1.2, 0.05);
    }
    for(const sx of [-hw*0.6, hw*0.6]) beam(out,[x+sx,y-hd+sk-0.03,z+0.02],[x+sx,y+hd+sk+0.03,z+0.02],0.03,'wood',(b||0)-0.2); // runners
  }
  // draped canvas over a footprint — sags on a 3x3 grid, ties at the corners
  function tarpOver(out, x0,x1,y0,y1, z, sag, b){
    const nx=3, ny=3;
    const Z=(u,v)=> z - Math.sin(u*Math.PI)*Math.sin(v*Math.PI)*sag*0.5 - (u*(1-u)+v*(1-v))*sag*0.3;
    const X=(u)=>x0+(x1-x0)*u, Y=(v)=>y0+(y1-y0)*v;
    for(let i=0;i<nx;i++) for(let j=0;j<ny;j++){
      const u0=i/nx,u1=(i+1)/nx, v0=j/ny,v1=(j+1)/ny;
      out.push(F([[X(u0),Y(v0),Z(u0,v0)],[X(u1),Y(v0),Z(u1,v0)],[X(u1),Y(v1),Z(u1,v1)],[X(u0),Y(v1),Z(u0,v1)]],
        'canvas', (b||0)+0.1+j*0.06, 0, [[X(u0),Y(v0)],[X(u1),Y(v0)],[X(u1),Y(v1)],[X(u0),Y(v1)]], canvasTex())); }
    for(const sx of [x0,x1]) for(const sy of [y0,y1]){
      const u=sx===x0?0:1, v=sy===y0?0:1;
      out.push(F([[sx,sy,Z(u,v)],[sx+(u?-0.1:0.1),sy,Z(u,v)-0.16],[sx+(u?-0.1:0.1),sy+(v?-0.06:0.06),Z(u,v)-0.16],[sx,sy+(v?-0.06:0.06),Z(u,v)]],
        'canvas', (b||0)-0.4)); }
  }
  // polyline / catenary rope run
  function ropeRun(out, pts, r, mat, b, n){ for(let i=0;i+1<pts.length;i++) tube(out, pts[i], pts[i+1], r, n||6, mat, b, null, false); }
  function catPts(p0,p1,sag,steps){ const o=[]; steps=steps||7;
    for(let i=0;i<=steps;i++){ const t=i/steps;
      o.push([p0[0]+(p1[0]-p0[0])*t, p0[1]+(p1[1]-p0[1])*t, p0[2]+(p1[2]-p0[2])*t - Math.sin(Math.PI*t)*sag]); }
    return o; }
  // flaked rope coil on the deck: concentric rings
  function ropeCoilAt(out, x,y,z, R, turns, r, mat, b){
    for(let k=0;k<turns;k++) torusZ(out, x,y, z+r*0.86, Math.max(r*1.2, R-k*r*2.05), r, 14, 6, mat, (b||0)+k*0.05);
  }
  // shed-wall backboard for the wall-hung pieces (built at -Y, then mirrored to +Y by mirrorY)
  function backBoard(out, w,h, y, z0, mat, b, tex){
    box(out, -w/2,w/2, y,y+0.055, z0,z0+h, mat, b||0, tex||plankTex(0.2), true);
    for(const sx of [-w/2+0.05, w/2-0.05]) beam(out,[sx,y+0.03,z0],[sx,y+0.03,z0+h],0.028,mat,(b||0)+0.2);
  }
  // mirror a face list through y=0 (winding reversed so normals stay outward). Wall-hung pieces are
  // authored front-to--Y for readable maths, then flipped so the face reads at the default SE view.
  function mirrorY(out, faces){ for(const f of faces){
    const v=f.v.map(p=>[p[0],-p[1],p[2]]).slice().reverse();
    const uv=f.uv?f.uv.slice().reverse():null;
    out.push({ v, mat:f.mat, b:f.b, db:f.db, uv, tex:f.tex, flat:f.flat }); } }
  // a slatted pallet, used on its own and under half the stacks
  function palletAt(out, x,y,z, w,d, mat, b){
    const hw=w/2, hd=d/2;
    for(const sy of [-hd+0.07, 0, hd-0.07]) box(out, x-hw,x+hw, y+sy-0.05,y+sy+0.05, z,z+0.075, mat, (b||0)-0.1, null, true);
    for(let i=0;i<5;i++){ const px=x-hw+ (i/4)*(w-0.13)+0.065;
      box(out, px-0.055,px+0.055, y-hd,y+hd, z+0.075,z+0.11, mat, (b||0)+0.2, grainTex(0.18)); }
  }
  // open-topped tote / crate shell with a rolled lip
  function toteShell(out, x,y,z, w,d,h, mat, b, tex, lip){
    const hw=w/2, hd=d/2, t=0.035;
    box(out, x-hw,x+hw, y-hd,y+hd, z,z+h, mat, b||0, tex||null, true);
    box(out, x-hw+t,x+hw-t, y-hd+t,y+hd-t, z+h*0.14,z+h*0.2, mat, (b||0)-0.9, null); // inner floor
    for(const sy of [-1,1]) dY(out, y+sy*hd, sy, x-hw+t, x+hw-t, z+h*0.2, z+h-t, mat, (b||0)-1.1, 0.04);
    for(const sx of [-1,1]) dX(out, x+sx*hw, sx, y-hd+t, y+hd-t, z+h*0.2, z+h-t, mat, (b||0)-1.1, 0.04);
    if(lip!==false){ box(out, x-hw-0.02,x+hw+0.02, y-hd-0.02,y+hd+0.02, z+h-0.045,z+h, mat, (b||0)+0.34, null, true);
      slab(out, [[x-hw-0.02,y-hd-0.02],[x+hw+0.02,y-hd-0.02],[x+hw+0.02,y+hd+0.02],[x-hw-0.02,y+hd+0.02]], z+h, mat, (b||0)+0.42); }
  }
  // hooped barrel, standing
  function barrelAt(out, x,y,z, r, h, mat, b, hoop){
    tube(out, [x,y,z],[x,y,z+h*0.16], r*0.94, 12, mat, (b||0)-0.05, grainTex(0.24));
    tube(out, [x,y,z+h*0.16],[x,y,z+h*0.84], r, 12, mat, b||0, grainTex(0.24));
    tube(out, [x,y,z+h*0.84],[x,y,z+h], r*0.94, 12, mat, (b||0)-0.05, grainTex(0.24));
    for(const zz of [z+h*0.12, z+h*0.44, z+h*0.86]) tube(out, [x,y,zz],[x,y,zz+0.035], r*1.03, 12, hoop||'iron', (b||0)+0.5, null, false);
    const p=[]; for(let i=0;i<12;i++){ const t=(i/12)*Math.PI*2; p.push([x+Math.cos(t)*r*0.94, y-Math.sin(t)*r*0.94]); }
    slab(out, p, z+h, mat, (b||0)+0.3, grainTex(0.2));
  }

  // ================= CATALOGUE =================
  const CATS = ['gear','handling','lifting','safety','drying','decor','odds'];

  // ---------------- FISHING GEAR ----------------
  function pTrapStack(out,s){
    const wood = s.variant===1, n = 2 + Math.round((s.len||0)*4);
    const tw=0.92, td=0.56, th=wood?0.42:0.34, rnd=mulberry32(1471);
    for(let i=0;i<n;i++) trapUnit(out, 0,0, i*(th+0.02), tw, td, th, wood, i*0.05, rnd);
    const top = n*(th+0.02);
    if(s.loaded){
      ropeCoilAt(out, -0.16,0.04, top, 0.19, 3, 0.028, 'rope', 0.2);
      buoy(out, [0.24,-0.1,top+0.05],[0.4,0.12,top+0.05], 0.055, 'buoy1', 'buoy2', 0.15);
      heap(out, 0.1,0.1, top, 0.4,0.3, 0.09, 'net', 5501, 4, netTex(), -0.3);
    }
    if(s.tarp) tarpOver(out, -tw/2-0.07, tw/2+0.07, -td/2-0.07, td/2+0.07, top+0.05, 0.3, 0);
  }
  function pTrapSingle(out,s){
    const wood = s.variant===1, tw=0.94, td=0.58, th=wood?0.44:0.36, rnd=mulberry32(77);
    trapUnit(out, 0,0, 0, tw, td, th, wood, 0, rnd);
    if(s.loaded){
      ropeRun(out, catPts([tw*0.42,-td*0.3,th],[tw*0.42+0.5,-td*0.3-0.32,0.03], 0.1, 6), 0.022, 'rope', 0.1);
      ropeCoilAt(out, tw*0.42+0.58,-td*0.3-0.4, 0, 0.15, 2, 0.024, 'rope', 0.05);
      buoy(out, [-tw*0.2,-td*0.62,0.05],[tw*0.16,-td*0.72,0.05], 0.05, 'buoy1', 'buoy2', 0.1);
      prismT(out, -0.1,0.02, 0.07, th*0.2, th*0.5, 6, 'net', -0.8);   // bait bag inside
    }
  }
  function pBuoyRack(out,s){
    const w = 1.5 + (s.len||0)*1.4, legs=2;
    for(const sx of [-1,1]){ const x=sx*(w/2-0.1);
      beam(out,[x,-0.34,0],[x,0,1.62], 0.055,'wood',0.1);
      beam(out,[x,0.34,0],[x,0,1.62], 0.055,'wood',-0.1);
      beam(out,[x,-0.3,0.5],[x,0.3,0.5], 0.035,'wood',0.15); }
    box(out, -w/2,w/2, -0.045,0.045, 1.56,1.62, 'wood', 0.35, grainTex(0.2));
    box(out, -w/2,w/2, -0.04,0.04, 0.96,1.02, 'wood', 0.3, grainTex(0.2));
    if(s.loaded!==false){ const n=Math.max(3, Math.round(w/0.3)), rnd=mulberry32(913);
      for(let i=0;i<n;i++){ const x=-w/2+0.14+ i*((w-0.28)/(n-1)), k=BUOYSET[i%BUOYSET.length];
        const z0=1.5-rnd()*0.1, L=0.62+rnd()*0.2, lean=(rnd()-0.5)*0.16;
        buoy(out, [x,0.02,z0],[x+lean,0.06,z0-L], 0.058, 'buoy'+((i%5)+1), i%2?'buoy'+(((i+2)%5)+1):null, 0.1);
        beam(out,[x,0.02,1.58],[x,0.02,z0],0.012,'rope',0.3); }
    }
  }
  function pBuoyWall(out,s){
    const q=[], w = 1.7 + (s.len||0)*1.3;
    backBoard(q, w, 1.9, 0.16, 0.05, 'plank', -0.2);
    for(const z of [1.6, 0.98]) box(q, -w/2+0.04,w/2-0.04, 0.1,0.16, z,z+0.06, 'wood', 0.3, grainTex(0.18));
    const rows=[1.6,0.98], rnd=mulberry32(2287);
    rows.forEach((rz,ri)=>{ const n=Math.max(3, Math.round(w/0.34));
      for(let i=0;i<n;i++){ const x=-w/2+0.16+ i*((w-0.32)/(n-1)) + (ri?0.08:0);
        const L=0.5+rnd()*0.22, lean=(rnd()-0.5)*0.2, k=(i+ri*2)%5;
        buoy(q, [x,0.06,rz-0.02],[x+lean,0.02,rz-0.04-L], 0.055, 'buoy'+(k+1), (i+ri)%2?'buoy'+((k+3)%5+1):null, 0.05);
        beam(q,[x,0.1,rz+0.06],[x,0.06,rz-0.02],0.011,'rope',0.3); } });
    mirrorY(out,q);
  }
  function pNetReel(out,s){
    const w = 1.15 + (s.len||0)*0.7, R=0.62;
    for(const sx of [-1,1]){ const x=sx*(w/2+0.09);
      beam(out,[x-0.16,-0.42,0],[x,0,R+0.28], 0.06,'wood',0.05);
      beam(out,[x-0.16,0.42,0],[x,0,R+0.28], 0.06,'wood',-0.1);
      box(out, x-0.2,x+0.2, -0.5,0.5, 0,0.1, 'wood', -0.2, null, true); }
    for(const sx of [-1,1]) tube(out, [sx*(w/2)-sx*0.03,0,R+0.28],[sx*(w/2)+sx*0.03,0,R+0.28], R, 14, 'wood', 0.2, grainTex(0.26));
    tube(out, [-w/2-0.16,0,R+0.28],[w/2+0.16,0,R+0.28], 0.05, 8, 'galv', 0.3);
    for(const sx of [-1,1]) for(let i=0;i<6;i++){ const a=i/6*Math.PI*2;
      beam(out,[sx*(w/2-0.02), Math.cos(a)*R*0.72, R+0.28+Math.sin(a)*R*0.72],[sx*(w/2-0.02),0,R+0.28], 0.02,'wood',0.2); }
    if(s.loaded!==false) tube(out, [-w/2+0.04,0,R+0.28],[w/2-0.04,0,R+0.28], R*0.82, 14, 'net', -0.1, netTex(0.045));
    beam(out,[w/2+0.12,0,R+0.28],[w/2+0.34,0,R+0.28], 0.028,'iron',0.2);
    beam(out,[w/2+0.34,0,R+0.28],[w/2+0.34,-0.2,R+0.16], 0.024,'iron',0.1);   // crank handle
    if(s.tarp) tarpOver(out, -w/2-0.1, w/2+0.1, -R*0.9, R*0.9, R+0.9, 0.42, 0.1);
  }
  function pNetPile(out,s){
    const w = 1.5 + (s.len||0)*1.1, d = 1.0 + (s.len||0)*0.6;
    heap(out, 0,0, 0, w,d, 0.52, 'net', 3307, 26, netTex(), 0);
    const rnd=mulberry32(881);
    for(let i=0;i<5;i++){ const a=rnd()*Math.PI*2, r=rnd()*w*0.42;
      prismT(out, Math.cos(a)*r, Math.sin(a)*r*0.7, 0.075, 0.3+rnd()*0.2, 0.45+rnd()*0.22, 8, 'foam', 0.2); }
    for(let i=0;i<3;i++){ const a=rnd()*Math.PI*2, r=w*0.3+rnd()*w*0.2;
      prismT(out, Math.cos(a)*r, Math.sin(a)*r*0.66, 0.055, 0.02, 0.1, 6, 'iron', -0.2); } // lead line weights
    ropeRun(out, catPts([-w*0.44,-d*0.3,0.2],[w*0.4,d*0.22,0.34], 0.12, 6), 0.024, 'rope', 0.2);
  }
  function pRopeCoil(out,s){
    const n = s.variant===1?3:1, R=0.34+(s.len||0)*0.16;
    const spots = n===1?[[0,0]]:[[-0.3,-0.1],[0.28,0.06],[0.02,0.32]];
    spots.forEach((p,i)=>{ ropeCoilAt(out, p[0],p[1], 0, R-(i?0.06:0), 4, 0.03, i===1?'poly':'rope', i*0.08);
      const a=(i*2.1); ropeRun(out, catPts([p[0]+Math.cos(a)*R, p[1]+Math.sin(a)*R*0.8, 0.05],
        [p[0]+Math.cos(a)*(R+0.32), p[1]+Math.sin(a)*(R+0.26)*0.8, 0.02], 0.03, 4), 0.028, i===1?'poly':'rope', 0.1); });
  }
  function pRopeHanks(out,s){
    const q=[], w = 1.2 + (s.len||0)*0.8;
    backBoard(q, w, 1.5, 0.14, 0.35, 'plank', -0.2);
    const n=Math.max(3, Math.round(w/0.36)), rnd=mulberry32(6161);
    for(let i=0;i<n;i++){ const x=-w/2+0.2+ i*((w-0.4)/(n-1)), zt=1.62;
      beam(q,[x,0.14,zt],[x,-0.04,zt+0.02], 0.022,'iron',0.4);               // peg
      const R=0.15+rnd()*0.06, mat=i%3===1?'poly':'rope';
      for(let k=0;k<3;k++){ const rr=R-k*0.032;
        q.push(F([[x-rr,0.06,zt-0.02],[x+rr,0.06,zt-0.02],[x+rr,0.06,zt-0.02-rr*2.1],[x-rr,0.06,zt-0.02-rr*2.1]],
          mat, -0.2+k*0.14, 0.05, [[0,0],[rr*2,0],[rr*2,rr*2.1],[0,rr*2.1]], ropeTex(), false)); }
      tube(q,[x-R,0.04,zt-0.02-R*2.1],[x+R,0.04,zt-0.02-R*2.1], 0.03, 6, mat, 0.1, null, false); }
    mirrorY(out,q);
  }
  function pFloatBags(out,s){
    const w=1.1+(s.len||0)*0.7, rnd=mulberry32(4021);
    const n=6+Math.round((s.len||0)*7);
    for(let i=0;i<n;i++){ const a=rnd(), c=rnd(), e=rnd();
      const x=(a*2-1)*w*0.44, y=(c*2-1)*w*0.3, r=0.12+e*0.07;
      const z=r + (i>n*0.6? r*1.4:0);
      prismT(out, x,y, r, Math.max(0,z-r), z+r*0.8, 8, i%4===0?'buoy2':'foam', -0.2+rnd()*0.6);
      pipe(out, x,y, r*0.2, z+r*0.8, z+r*1.05, 'iron', 0.2, 6); }
    if(s.loaded) box(out, -w*0.5,w*0.5, -w*0.34,w*0.34, 0,0.05, 'net', -0.6, netTex(), true);
  }
  function pTrawlDoor(out,s){
    const w = 1.5 + (s.len||0)*0.7, h = 0.9 + (s.len||0)*0.3, lean=0.34;
    const A=[[-w/2,0.02,0],[w/2,0.02,0],[w/2,-lean,h],[-w/2,-lean,h]];
    out.push(F(A, 'wood', 0.1, 0, [[0,0],[w,0],[w,h],[0,h]], slatTex(0.14)));
    out.push(F([A[3],A[2],A[1],A[0]], 'wood', -0.9));
    for(const zz of [h*0.2, h*0.78]){ const t=zz/h;
      box(out, -w/2-0.02,w/2+0.02, 0.02-lean*t-0.05,0.02-lean*t+0.01, zz-0.05,zz+0.05, 'galv', 0.4); }
    for(const sx of [-1,1]) beam(out,[sx*(w/2-0.05),0.02,0],[sx*(w/2-0.05),-lean,h], 0.04,'galv',0.3);
    box(out, -w/2-0.03,w/2+0.03, 0.0,0.08, 0,0.09, 'iron', 0.1);              // shoe
    for(const sx of [-1,1]) tube(out,[sx*(w*0.3),-lean*0.6,h*0.58],[sx*(w*0.3),-lean*0.6-0.04,h*0.58], 0.06, 8,'iron',0.3,null,false);
    if(s.loaded) ropeRun(out, catPts([-w*0.3,-lean*0.62,h*0.58],[-w*0.3-0.5,-0.5,0.04], 0.1, 6), 0.026, 'rope', 0.1);
  }

  // ---------------- HANDLING & STORAGE ----------------
  function pToteStack(out,s){
    const n = 2 + Math.round((s.len||0)*4), w=0.72, d=0.5, h=0.34;
    for(let i=0;i<n;i++){ const z=i*(h-0.045), sk=(i%2?0.015:-0.015);
      toteShell(out, sk,sk*0.6, z, w,d,h, 'body', i*0.04, null, true);
      for(const sy of [-1,1]) dY(out, sk*0.6+sy*d/2, sy, sk-0.1, sk+0.1, z+h*0.34, z+h*0.62, 'body', (i*0.04)+0.7, 0.06); }
    if(s.loaded){ const z=(n-1)*(h-0.045);
      heap(out, 0,0, z+h*0.2, w*0.78, d*0.72, 0.3, 'fish', 7717, 12, scaleTex(), 0.1); }
    if(s.tarp) tarpOver(out, -w/2-0.06, w/2+0.06, -d/2-0.06, d/2+0.06, (n-1)*(h-0.045)+h+0.03, 0.26, 0);
  }
  function pToteSingle(out,s){
    const w=0.74, d=0.52, h=0.36;
    toteShell(out, 0,0, 0, w,d,h, 'body', 0, null, true);
    for(const sy of [-1,1]) dY(out, sy*d/2, sy, -0.11, 0.11, h*0.34, h*0.64, 'body', 0.7, 0.06);
    dY(out, -d/2, -1, -0.2, -0.05, h*0.72, h*0.84, 'paper', 0.5, 0.06);      // stencil
    if(s.loaded){
      if(s.variant===1){ heap(out, 0,0, h*0.22, w*0.8,d*0.74, 0.22, 'ice', 8813, 14, null, 0.4);
        heap(out, 0.02,-0.02, h*0.34, w*0.5,d*0.4, 0.16, 'fish', 8823, 5, scaleTex(), 0.1); }
      else heap(out, 0,0, h*0.2, w*0.8,d*0.74, 0.3, 'fish', 8801, 13, scaleTex(), 0.1); }
  }
  function pCrateStack(out,s){
    const n = 1 + Math.round((s.len||0)*3), w=0.66, d=0.62, h=0.42, rnd=mulberry32(551);
    for(let i=0;i<n;i++){ const z=i*(h+0.012), sk=(rnd()-0.5)*0.07;
      box(out, sk-w/2,sk+w/2, sk*0.5-d/2,sk*0.5+d/2, z,z+h, 'wood', i*0.05, slatTex(0.1), i===n-1&&s.loaded?true:false);
      for(const sx of [-1,1]) beam(out,[sk+sx*(w/2-0.03), sk*0.5-d/2, z],[sk+sx*(w/2-0.03), sk*0.5-d/2, z+h], 0.032,'wood',(i*0.05)+0.3);
      dY(out, sk*0.5-d/2, -1, sk-0.14, sk+0.14, z+h*0.36, z+h*0.6, 'paper', 0.4, 0.05); }
    if(s.loaded){ const z=(n-1)*(h+0.012)+h;
      ropeCoilAt(out, 0,0, z-0.02, 0.22, 3, 0.028, 'rope', 0.2); }
    if(s.tarp) tarpOver(out, -w/2-0.06,w/2+0.06, -d/2-0.06,d/2+0.06, (n-1)*(h+0.012)+h+0.04, 0.28, 0);
  }
  function pPallet(out,s){
    const n = 1 + Math.round((s.len||0)*5), w=1.1, d=0.85;
    for(let i=0;i<n;i++) palletAt(out, (i%2?0.02:-0.02),0, i*0.115, w,d, 'wood', i*0.04);
    if(s.loaded){ const z=n*0.115;
      box(out, -w*0.4,w*0.4, -d*0.4,d*0.4, z,z+0.34, 'body', 0.1, slatTex(0.1));
      ropeRun(out, [[-w*0.42,-d*0.2,z+0.34],[w*0.42,-d*0.2,z+0.34]], 0.02,'poly',0.3,6);
      ropeRun(out, [[-w*0.42,d*0.2,z+0.34],[w*0.42,d*0.2,z+0.34]], 0.02,'poly',0.3,6); }
  }
  function pSaltBin(out,s){
    const w = 1.3 + (s.len||0)*0.9, d=0.86, h=0.78;
    box(out, -w/2,w/2, -d/2,d/2, 0,h, 'wood', 0, slatTex(0.11), true);
    for(const sx of [-1,1]) for(const sy of [-1,1]) beam(out,[sx*(w/2-0.04),sy*(d/2-0.04),0],[sx*(w/2-0.04),sy*(d/2-0.04),h], 0.045,'wood',0.25);
    for(const sy of [-1,1]) dY(out, sy*d/2, sy, -w/2+0.06, w/2-0.06, h*0.44, h*0.52, 'wood', 0.35, 0.04);
    if(s.variant===1){        // lid propped open
      out.push(F([[-w/2-0.03,-d/2-0.03,h+0.02],[w/2+0.03,-d/2-0.03,h+0.02],[w/2+0.03,d/2+0.03,h+0.28],[-w/2-0.03,d/2+0.03,h+0.28]],
        'wood', 0.36, 0, [[0,0],[w,0],[w,d],[0,d]], plankTex(0.2)));
      if(s.loaded) heap(out, 0,0, h*0.62, w*0.82,d*0.78, 0.2, 'salt', 4411, 12, gravelTex(), 0.5);
    } else {
      out.push(F([[-w/2-0.04,-d/2-0.04,h+0.06],[w/2+0.04,-d/2-0.04,h+0.06],[w/2+0.04,d/2+0.04,h+0.16],[-w/2-0.04,d/2+0.04,h+0.16]],
        'wood', 0.4, 0, [[0,0],[w,0],[w,d],[0,d]], plankTex(0.2)));
      for(const sx of [-1,1]) beam(out,[sx*w*0.3,d/2+0.02,h+0.1],[sx*w*0.3,d/2+0.02,h+0.16], 0.03,'iron',0.4);
      if(s.loaded) heap(out, 0,-d*0.1, 0.02, w*0.5,d*0.4, 0.18, 'salt', 4412, 7, gravelTex(), 0.4);
    }
  }
  function pIceChest(out,s){
    const w = 1.15 + (s.len||0)*0.75, d=0.72, h=0.66;
    box(out, -w/2,w/2, -d/2,d/2, 0.07,h, 'body', 0, null, true);
    box(out, -w/2+0.04,w/2-0.04, -d/2+0.04,d/2-0.04, 0,0.07, 'iron', -0.3);
    for(const sy of [-1,1]) dY(out, sy*d/2, sy, -w/2+0.05, w/2-0.05, 0.12, h-0.08, 'body', sy>0?-0.6:-0.9, 0.04);
    if(s.variant===1){        // lid open, ice inside
      out.push(F([[-w/2,d/2,h],[w/2,d/2,h],[w/2,d/2+0.12,h+0.4],[-w/2,d/2+0.12,h+0.4]], 'body', 0.44, 0, [[0,0],[w,0],[w,0.42],[0,0.42]], null));
      box(out, -w/2+0.05,w/2-0.05, -d/2+0.05,d/2-0.05, h-0.28,h-0.24, 'ice', -0.4);
      heap(out, 0,0, h-0.26, w*0.8,d*0.76, 0.2, 'ice', 9101, 12, null, 0.5);
      if(s.loaded) heap(out, 0,-0.04, h-0.2, w*0.5,d*0.4, 0.14, 'fish', 9102, 5, scaleTex(), 0.1);
    } else {
      box(out, -w/2-0.025,w/2+0.025, -d/2-0.025,d/2+0.025, h,h+0.075, 'body', 0.4);
      for(const sx of [-1,1]) dX(out, sx*(w/2+0.025), sx, -0.1,0.1, h-0.2,h-0.1, 'galv', 0.6, 0.05);
      if(s.loaded) heap(out, -w*0.36,-d*0.44, 0, 0.3,0.22, 0.1, 'ice', 9105, 5, null, 0.6);
    }
    dY(out, -d/2, -1, -0.22, 0.06, h*0.5, h*0.68, 'paper', 0.45, 0.06);
  }
  function pWeighScale(out,s){
    const ht = 1.9 + (s.len||0)*0.5, sp=0.5;
    for(const sx of [-1,1]){ beam(out,[sx*sp,-0.3,0],[sx*0.06,0,ht], 0.05,'galv',0.1);
      beam(out,[sx*sp,0.3,0],[sx*0.06,0,ht], 0.05,'galv',-0.05);
      box(out, sx*sp-0.09,sx*sp+0.09, -0.36,0.36, 0,0.05, 'iron', -0.1, null, true); }
    box(out, -0.12,0.12, -0.05,0.05, ht-0.04,ht+0.03, 'iron', 0.3);
    prismT(out, 0,0, 0.2, ht-0.42, ht-0.06, 14, 'body', 0.1);                  // dial head
    decalY(out, -0.2, -1, -0.15,0.15, ht-0.36,ht-0.12, 'paper', 0.6, null, true, 0.07);
    beam(out,[0,-0.2,ht-0.24],[0.07,-0.24,ht-0.18], 0.014,'iron',0.7);         // needle
    tube(out,[0,0,ht-0.46],[0,0,ht-0.42], 0.05, 8,'iron',0.2,null,false);
    ropeRun(out, [[0,0,ht-0.46],[0,0,1.1]], 0.016,'galv',0.2,6);
    tube(out,[0,0,1.06],[0,0,1.1], 0.055, 8,'iron',0.3,null,false);            // hook block
    beam(out,[0,0,1.06],[0,-0.03,0.96], 0.018,'iron',0.4);
    if(s.loaded){ toteShell(out, 0,0, 0.62, 0.7,0.48,0.32, 'body', 0.05, null, true);
      heap(out, 0,0, 0.78, 0.54,0.36, 0.22, 'fish', 6161, 9, scaleTex(), 0.1);
      for(const sx of [-1,1]) ropeRun(out, [[sx*0.3,0,0.94],[0,0,1.06]], 0.014,'rope',0.2,5); }
  }
  function pGuttingTable(out,s){
    const w = 1.7 + (s.len||0)*1.1, d=0.78, h=0.9;
    for(const sx of [-1,1]) for(const sy of [-1,1])
      beam(out,[sx*(w/2-0.08),sy*(d/2-0.07),0],[sx*(w/2-0.08),sy*(d/2-0.07),h], 0.045,'wood',0.15);
    box(out, -w/2,w/2, -d/2,d/2, h-0.06,h, 'plank', 0.3, plankTex(0.16));
    box(out, -w/2,w/2, d/2-0.06,d/2, h,h+0.14, 'plank', 0.2);                  // splash board
    for(const sy of [-1,1]) box(out, -w/2+0.06,w/2-0.06, sy*(d/2-0.1)-0.03,sy*(d/2-0.1)+0.03, h*0.42,h*0.48, 'wood', 0.25);
    box(out, w/2-0.44,w/2-0.06, -0.16,0.2, h-0.1,h-0.05, 'iron', -0.4);        // gut chute hole
    pipe(out, w/2-0.25,0.02, 0.09, h*0.3, h-0.1, 'galv', 0.05, 8);
    tube(out,[-w/2+0.3,d/2-0.04,h+0.1],[-w/2+0.3,d/2-0.22,h+0.1], 0.022, 8,'galv',0.3);  // tap
    tube(out,[-w/2+0.3,d/2-0.22,h+0.1],[-w/2+0.3,d/2-0.22,h+0.02], 0.02, 8,'galv',0.25);
    ropeRun(out, catPts([-w/2+0.3,d/2-0.02,h+0.02],[-w/2-0.1,d/2+0.16,0.04], 0.12, 6), 0.022,'poly',0.1);
    if(s.loaded){ heap(out, w*0.06,-0.04, h, w*0.44,d*0.4, 0.16, 'fish', 3131, 7, scaleTex(), 0.2);
      for(const k of [[-0.2,0.08],[0.06,0.16]]) beam(out,[w*0.06+k[0],k[1],h+0.01],[w*0.06+k[0]+0.16,k[1]-0.05,h+0.015], 0.012,'galv',0.8); }
    toteShell(out, -w/2+0.22,-d/2-0.28, 0, 0.6,0.42,0.3, 'body', 0.05, null, true);
  }
  function pBaitBarrel(out,s){
    const n = s.variant===1?2:1;
    const spots = n===1?[[0,0]]:[[-0.32,0.06],[0.3,-0.06]];
    spots.forEach((p,i)=>{ barrelAt(out, p[0],p[1], 0, 0.31, 0.86, i&&s.variant===1?'body':'wood', i*0.06, 'iron');
      if(s.loaded && i===0){ heap(out, p[0],p[1], 0.8, 0.5,0.5, 0.16, 'fish', 2121, 8, scaleTex(), 0.2);
        prismT(out, p[0]+0.1,p[1]-0.1, 0.055, 0.86, 1.02, 6, 'wood', 0.3); } });   // scoop handle
    if(s.tarp) tarpOver(out, spots[0][0]-0.38, (spots[n-1][0])+0.38, -0.4,0.4, 0.95, 0.3, 0);
  }
  function pBushelBaskets(out,s){
    const n = 2 + Math.round((s.len||0)*3), rnd=mulberry32(1919);
    for(let i=0;i<n;i++){ const a=rnd()*Math.PI*2, r=(i?0.34+rnd()*0.16:0);
      const x=Math.cos(a)*r, y=Math.sin(a)*r*0.7, stack=(i%3===2)?1:0;
      for(let k=0;k<=stack;k++){ const z=k*0.3;
        tube(out, [x,y,z],[x,y,z+0.34], 0.24, 12, 'wood', 0.05+k*0.06, wickerTex());
        tube(out, [x,y,z+0.32],[x,y,z+0.37], 0.255, 12, 'wood', 0.4, null, false);
        const p=[]; for(let j=0;j<12;j++){ const t=(j/12)*Math.PI*2; p.push([x+Math.cos(t)*0.23, y-Math.sin(t)*0.23]); }
        if(k===stack && s.loaded) { slab(out,p, z+0.16,'wood',-1.0); heap(out, x,y, z+0.16, 0.34,0.34, 0.2, 'fish', 1313+i, 6, scaleTex(), 0.1); }
        else slab(out, p, z+0.06, 'wood', -1.1);
        for(const sx of [-1,1]) beam(out,[x+sx*0.245,y,z+0.2],[x+sx*0.2,y,z+0.3], 0.02,'wood',0.4); } }
  }
  function pSortingTrough(out,s){
    const w = 2.0 + (s.len||0)*1.4, d=0.6, h=0.82, t=0.05;
    for(const sx of [-1,1]) for(const sy of [-1,1])
      beam(out,[sx*(w/2-0.1),sy*(d/2-0.05),0],[sx*(w/2-0.1),sy*(d/2-0.05),h-0.16], 0.042,'wood',0.15);
    for(const sx of [-1,1]) beam(out,[sx*(w/2-0.1),-d/2+0.05,h*0.4],[sx*(w/2-0.1),d/2-0.05,h*0.4], 0.03,'wood',0.2);
    box(out, -w/2,w/2, -d/2,d/2, h-0.16,h-0.11, 'plank', 0.1, plankTex(0.15));  // trough floor
    for(const sy of [-1,1]) box(out, -w/2,w/2, sy*d/2-sy*t,sy*d/2, h-0.16,h+0.02, 'plank', sy>0?0.15:0.3, plankTex(0.15));
    for(const sx of [-1,1]) box(out, sx*w/2-sx*t,sx*w/2, -d/2,d/2, h-0.16,h+0.02, 'plank', 0.22);
    box(out, -w/2+0.3,-w/2+0.34, -d/2+t,d/2-t, h-0.11,h+0.0, 'plank', 0.35);   // grading divider
    box(out,  w/2-0.62, w/2-0.58, -d/2+t,d/2-t, h-0.11,h+0.0, 'plank', 0.35);
    tube(out,[w/2-0.02,0,h-0.14],[w/2+0.18,0,h-0.22], 0.05, 8,'galv',0.2);      // drain spout
    if(s.loaded){ heap(out, -w*0.32,0, h-0.11, w*0.2,d*0.62, 0.14, 'fish', 5151, 6, scaleTex(), 0.2);
      heap(out,  w*0.05,0, h-0.11, w*0.3,d*0.62, 0.12, 'fish', 5152, 7, scaleTex(), 0.2); }
    if(s.variant===1) toteShell(out, w/2+0.42,0, 0, 0.68,0.46,0.32, 'body', 0.05, null, true);
  }

  // torus on an arbitrary plane (ax MUST equal e1 x e2 for outward winding) — wheels, hoops
  function torusA(out, c, R, r, n, m, mat, b, e1, e2, ax){
    const P=(i,j)=>{ const th=i/n*Math.PI*2, ph=j/m*Math.PI*2, rr=R+r*Math.cos(ph), h=r*Math.sin(ph);
      return [c[0]+e1[0]*Math.cos(th)*rr+e2[0]*Math.sin(th)*rr+ax[0]*h,
              c[1]+e1[1]*Math.cos(th)*rr+e2[1]*Math.sin(th)*rr+ax[1]*h,
              c[2]+e1[2]*Math.cos(th)*rr+e2[2]*Math.sin(th)*rr+ax[2]*h]; };
    for(let i=0;i<n;i++) for(let j=0;j<m;j++){ const i2=(i+1)%n, j2=(j+1)%m;
      out.push(F([P(i,j),P(i2,j),P(i2,j2),P(i,j2)], mat, (b||0)+Math.sin(j/m*Math.PI*2)*0.22)); }
  }
  const EX=[1,0,0], EY=[0,1,0], EZ=[0,0,1], NEY=[0,-1,0];

  // ---------------- LIFTING & MOVING ----------------
  function pDavitHoist(out,s){
    const ht = 2.3 + (s.len||0)*1.2, reach = 1.2 + (s.len||0)*0.6;
    box(out, -0.24,0.24, -0.22,0.22, 0,0.1, 'iron', -0.1, null, true);
    for(const sx of [-1,1]) for(const sy of [-1,1]) prismT(out, sx*0.17,sy*0.15, 0.035, 0,0.06, 6,'iron',0.3);
    pipe(out, 0,0, 0.075, 0.08, ht, 'galv', 0.05, 10);
    tube(out, [0,0,ht-0.12],[0,-reach*0.32,ht+0.22], 0.062, 8, 'galv', 0.2);
    tube(out, [0,-reach*0.32,ht+0.22],[0,-reach,ht+0.16], 0.055, 8, 'galv', 0.25);
    beam(out,[0,-reach*0.55,ht+0.19],[0,-0.05,ht-0.62], 0.03,'galv',-0.1);      // knee brace
    tube(out,[0,-reach,ht+0.16],[0,-reach,ht+0.02], 0.03, 6,'iron',0.3);
    torusA(out, [0,-reach,ht-0.05], 0.07, 0.022, 10, 5, 'iron', 0.2, EX, EZ, NEY);
    const dz = s.loaded? 0.72 : ht-0.7;
    ropeRun(out, [[0,-reach,ht-0.08],[0,-reach,dz+0.12]], 0.016,'galv',0.15,6);
    tube(out,[0,-reach,dz+0.12],[0,-reach,dz+0.02], 0.045, 8,'iron',0.35);       // hook block
    beam(out,[0,-reach,dz+0.03],[0,-reach-0.04,dz-0.07], 0.02,'iron',0.45);
    if(s.loaded){ toteShell(out, 0,-reach, 0, 0.72,0.5,0.34, 'body', 0.05, null, true);
      heap(out, 0,-reach, 0.2, 0.56,0.36, 0.22, 'fish', 4141, 9, scaleTex(), 0.1);
      for(const sx of [-1,1]) ropeRun(out, [[sx*0.32,-reach,0.36],[0,-reach,dz+0.02]], 0.013,'rope',0.2,5); }
    box(out, -0.16,0.16, 0.1,0.28, 0.5,0.9, 'body', 0.1);                        // gear case
    tube(out,[0.16,0.2,0.7],[0.34,0.2,0.7], 0.028, 8,'iron',0.3);
    torusA(out, [0.36,0.2,0.7], 0.11, 0.02, 10, 5, 'iron', 0.2, EY, EZ, EX);     // crank wheel
    ropeCoilAt(out, 0,0.19, 0.9, 0.11, 2, 0.03, 'galv', 0.3);
  }
  function pCapstan(out,s){
    const ht = 0.62 + (s.len||0)*0.2;
    prismT(out, 0,0, 0.46, 0,0.08, 14, 'iron', -0.1, gravelTex());
    for(let i=0;i<4;i++){ const a=i/4*Math.PI*2+0.4; prismT(out, Math.cos(a)*0.36, Math.sin(a)*0.36, 0.035, 0,0.05, 6,'iron',0.3); }
    tube(out, [0,0,0.06],[0,0,ht*0.34], 0.3, 14, 'iron', 0.05);
    tube(out, [0,0,ht*0.34],[0,0,ht*0.82], 0.22, 14, 'iron', 0.1, corrTex(0.09));  // waisted drum
    tube(out, [0,0,ht*0.82],[0,0,ht], 0.3, 14, 'iron', 0.16);
    prismT(out, 0,0, 0.3, ht,ht+0.03, 14, 'iron', 0.4);
    for(let i=0;i<6;i++){ const a=i/6*Math.PI*2; prismT(out, Math.cos(a)*0.22, Math.sin(a)*0.22, 0.028, ht+0.02, ht+0.09, 6,'iron',0.35); } // whelps
    if(s.variant===1){ tube(out,[0,0,ht+0.04],[0,-0.62,ht+0.14], 0.045, 8,'wood',0.3); }  // hand bar
    if(s.loaded){ for(let k=0;k<3;k++) torusZ(out, 0,0, ht*0.4+k*0.07, 0.235, 0.026, 14, 5, 'rope', 0.2+k*0.06);
      ropeRun(out, catPts([0.24,-0.1,ht*0.5],[0.9,-0.7,0.04], 0.1, 6), 0.026,'rope',0.15); }
  }
  function pBlockTackle(out,s){
    const ht = 2.4 + (s.len||0)*0.9;
    box(out, -0.11,0.11, -0.11,0.11, 0,0.09, 'iron', -0.1, null, true);
    pipe(out, 0,0, 0.08, 0.06, ht, 'pole', 0.05, 10);
    box(out, -0.09,0.09, -1.05,0.06, ht-0.16, ht-0.02, 'wood', 0.3, grainTex(0.2));   // cantilever beam
    beam(out,[0,-0.72,ht-0.16],[0,-0.05,ht-0.78], 0.036,'wood',0.05);
    for(const [yy,zz,rr] of [[-0.9,ht-0.34,0.1],[-0.9,ht-0.9,0.09]]){
      box(out, -0.05,0.05, yy-rr-0.03,yy+rr+0.03, zz-rr-0.04, zz+rr+0.04, 'wood', 0.2, null, true);
      torusA(out, [0,yy,zz], rr*0.72, 0.026, 10, 5, 'iron', 0.3, EY, EZ, EX);
      for(const sx of [-1,1]) dX(out, sx*0.05, sx, yy-rr*0.7, yy+rr*0.7, zz-rr*0.7, zz+rr*0.7, 'iron', 0.5, 0.05); }
    ropeRun(out, [[0.0,-0.9,ht-0.24],[0.0,-0.9,ht-0.8]], 0.017,'rope',0.2,6);
    ropeRun(out, [[0.06,-0.9,ht-0.44],[0.06,-0.9,ht-1.0]], 0.017,'rope',0.2,6);
    ropeRun(out, catPts([-0.06,-0.9,ht-0.44],[-0.3,-0.3,0.06], 0.14, 7), 0.017,'rope',0.15);
    ropeCoilAt(out, -0.34,-0.28, 0, 0.17, 3, 0.026, 'rope', 0.1);
    tube(out,[0,-0.9,ht-1.02],[0,-0.9,ht-1.12], 0.04, 8,'iron',0.3);
    beam(out,[0,-0.9,ht-1.1],[0,-0.94,ht-1.2], 0.019,'iron',0.45);
    if(s.loaded){ barrelAt(out, 0,-0.9, ht-2.06, 0.3, 0.84, 'wood', 0.05, 'iron');
      for(const sx of [-1,1]) ropeRun(out, [[sx*0.28,-0.9,ht-1.3],[0,-0.9,ht-1.14]], 0.013,'rope',0.2,5); }
  }
  function pWheelbarrow(out,s){
    const R=0.19;
    torusA(out, [0,-0.52,R], R, 0.055, 12, 6, 'rubber', 0.05, EX, EZ, NEY);
    tube(out,[0,-0.56,R],[0,-0.48,R], 0.06, 10,'galv',0.2);
    for(const sx of [-1,1]){ beam(out,[sx*0.05,-0.5,R],[sx*0.26,0.56,0.66], 0.032,'wood',0.15);
      beam(out,[sx*0.24,0.2,0.1],[sx*0.24,0.34,0.5], 0.028,'galv',0.1);
      prismT(out, sx*0.24,0.2, 0.035, 0,0.05, 6,'galv',0.3);
      tube(out,[sx*0.26,0.5,0.62],[sx*0.26,0.66,0.68], 0.036, 8,'wood',0.3); }
    const y0=-0.36, y1=0.4, z0=0.3;
    out.push(F([[-0.34,y0,z0+0.1],[0.34,y0,z0+0.1],[0.3,y1,z0+0.02],[-0.3,y1,z0+0.02]], 'body', 0.35, 0,
      [[0,0],[0.68,0],[0.68,0.78],[0,0.78]], null));                              // tray floor
    for(const sx of [-1,1]) out.push(F([[sx*0.34,y0,z0+0.1],[sx*0.3,y1,z0+0.02],[sx*0.42,y1,z0+0.3],[sx*0.46,y0,z0+0.42]],
      'body', sx>0?0.2:-0.5));
    out.push(F([[-0.46,y0,z0+0.42],[0.46,y0,z0+0.42],[0.34,y0,z0+0.1],[-0.34,y0,z0+0.1]], 'body', -1.0));
    if(s.loaded){ heap(out, 0,0.0, z0+0.16, 0.6,0.6, 0.26, s.variant===1?'net':'fish', 7373, 11, s.variant===1?netTex():scaleTex(), 0.1); }
  }
  function pHandTruck(out,s){
    const lean=0.2, ht=1.35;
    for(const sx of [-1,1]){ beam(out,[sx*0.19,0.02,0.06],[sx*0.19,-lean,ht], 0.032,'galv',0.15);
      torusA(out, [sx*0.26,0.04,0.14], 0.13, 0.045, 10, 5, 'rubber', 0.05, EX, EZ, NEY);
      tube(out,[sx*0.2,0.04,0.14],[sx*0.3,0.04,0.14], 0.035, 8,'iron',0.2); }
    for(const zz of [0.42, 0.86, ht-0.04]){ const t=(zz-0.06)/(ht-0.06);
      box(out, -0.19,0.19, 0.02-lean*t-0.02,0.02-lean*t+0.02, zz-0.025,zz+0.025, 'galv', 0.3); }
    box(out, -0.21,0.21, -0.02,0.2, 0.06,0.11, 'galv', 0.25, null, true);          // toe plate
    tube(out,[-0.19,-lean,ht],[0.19,-lean,ht], 0.03, 8,'galv',0.35);
    for(const sx of [-1,1]) tube(out,[sx*0.19,-lean,ht-0.02],[sx*0.19,-lean-0.06,ht-0.12], 0.028, 8,'rubber',0.2);
    if(s.loaded){ for(let i=0;i<2;i++) box(out, -0.19,0.19, -0.1-i*0.02,0.28-i*0.02, 0.11+i*0.34,0.44+i*0.34, 'wood', 0.05+i*0.06, slatTex(0.1));
      ropeRun(out, [[-0.2,-0.1,0.7],[0.2,-0.1,0.7]], 0.016,'poly',0.3,6); }
  }
  function pDockCart(out,s){
    const w = 1.1 + (s.len||0)*0.6, d=0.72, h=0.34;
    for(const sx of [-1,1]) for(const sy of [-1,1])
      torusA(out, [sx*(w/2-0.14), sy*(d/2-0.08), 0.11], 0.1, 0.04, 10, 5, 'rubber', 0.05, EX, EZ, NEY);
    box(out, -w/2,w/2, -d/2,d/2, h-0.07,h, 'plank', 0.25, plankTex(0.15));
    for(const sy of [-1,1]) box(out, -w/2,w/2, sy*(d/2)-sy*0.05,sy*d/2, h-0.07,h+0.02, 'plank', 0.15);
    for(const sx of [-1,1]) box(out, sx*(w/2-0.1)-0.03,sx*(w/2-0.1)+0.03, -d/2+0.06,d/2-0.06, 0.11,h-0.07, 'galv', 0.1);
    box(out, -w/2+0.1,w/2-0.1, -0.04,0.04, 0.16,0.22, 'galv', 0.2);
    tube(out,[0,-d/2,h-0.04],[0,-d/2-0.34,h+0.16], 0.026, 8,'galv',0.25);         // draw handle
    tube(out,[-0.16,-d/2-0.34,h+0.16],[0.16,-d/2-0.34,h+0.16], 0.03, 8,'galv',0.35);
    if(s.loaded){ for(let i=0;i<2;i++) toteShell(out, -w*0.2+i*w*0.42,0, h, 0.66,0.46,0.32, 'body', 0.05+i*0.04, null, true);
      heap(out, -w*0.2,0, h+0.2, 0.5,0.34, 0.2, 'fish', 8181, 7, scaleTex(), 0.1); }
    if(s.tarp) tarpOver(out, -w/2-0.05,w/2+0.05, -d/2-0.05,d/2+0.05, h+0.4, 0.3, 0);
  }
  function pGantryFrame(out,s){
    const ht = 2.6 + (s.len||0)*1.1, sp = 1.15 + (s.len||0)*0.5;
    for(const sx of [-1,1]){ beam(out,[sx*sp,-0.42,0],[sx*0.12,0,ht], 0.06,'pole',0.1);
      beam(out,[sx*sp,0.42,0],[sx*0.12,0,ht], 0.06,'pole',-0.05);
      beam(out,[sx*sp*0.62,-0.28,ht*0.5],[sx*sp*0.62,0.28,ht*0.5], 0.032,'wood',0.2);
      box(out, sx*sp-0.1,sx*sp+0.1, -0.5,0.5, 0,0.07, 'wood', -0.15, null, true); }
    box(out, -sp*0.4,sp*0.4, -0.06,0.06, ht-0.06,ht+0.04, 'pole', 0.34, grainTex(0.22));
    for(const sx of [-1,1]) beam(out,[sx*0.12,0,ht-0.05],[sx*sp*0.45,0,ht-0.05], 0.045,'pole',0.3);
    tube(out,[0,0,ht-0.08],[0,0,ht-0.2], 0.038, 8,'iron',0.3);
    torusA(out, [0,0,ht-0.28], 0.09, 0.024, 10, 5, 'iron', 0.25, EY, EZ, EX);
    const dz = s.loaded? 0.9 : ht-1.1;
    ropeRun(out, [[0,0,ht-0.3],[0,0,dz+0.1]], 0.016,'rope',0.2,6);
    tube(out,[0,0,dz+0.1],[0,0,dz], 0.04, 8,'iron',0.3);
    beam(out,[0,0,dz+0.02],[0,-0.04,dz-0.09], 0.019,'iron',0.45);
    if(s.loaded){ barrelAt(out, 0,0, 0.02, 0.3, 0.84, 'wood', 0.05, 'iron');
      for(const sx of [-1,1]) ropeRun(out, [[sx*0.28,0,0.86],[0,0,dz]], 0.013,'rope',0.2,5); }
  }
  function pPotHauler(out,s){
    const ht = 0.98 + (s.len||0)*0.3;
    box(out, -0.3,0.3, -0.24,0.24, 0,0.09, 'iron', -0.1, null, true);
    pipe(out, 0,0, 0.06, 0.06, ht, 'galv', 0.05, 10);
    box(out, -0.2,0.2, -0.18,0.18, ht-0.34, ht-0.02, 'body', 0.05);               // hydraulic body
    dY(out, -0.18, -1, -0.14,0.14, ht-0.28, ht-0.08, 'body', -0.9, 0.05);
    for(const sy of [-1,1]) tube(out,[0,sy*0.19,ht-0.16],[0,sy*0.3,ht-0.16], 0.05, 8,'iron',0.25);
    torusA(out, [0,-0.34,ht-0.16], 0.19, 0.05, 12, 6, 'iron', 0.15, EX, EZ, NEY);  // sheave
    torusA(out, [0,-0.34,ht-0.16], 0.13, 0.05, 12, 6, 'rubber', -0.1, EX, EZ, NEY);
    tube(out,[0.2,0.06,ht-0.2],[0.4,0.06,ht-0.2], 0.032, 8,'galv',0.2);
    tube(out,[0.4,0.06,ht-0.2],[0.4,0.06,ht-0.5], 0.026, 8,'galv',0.15);          // lever
    tube(out,[0.4,0.06,ht-0.5],[0.34,-0.06,ht-0.62], 0.03, 8,'rubber',0.2);
    for(const sy of [-1,1]) tube(out,[0.1,sy*0.1,0.3],[0.1,sy*0.1,ht-0.34], 0.03, 8,'iron',-0.1);  // hoses
    if(s.loaded) ropeRun(out, catPts([0,-0.5,ht-0.1],[0.2,-1.1,0.05], 0.1, 6), 0.026,'rope',0.15);
  }

  // ---------------- SAFETY & SIGNAGE ----------------
  function ringBuoyAt(out, x,y,z, R, e1,e2,ax, b){
    torusA(out, [x,y,z], R, R*0.28, 14, 6, 'body', b||0, e1,e2,ax);
    for(let i=0;i<4;i++){ const a=i/4*Math.PI*2+0.4;
      const p=[x+e1[0]*Math.cos(a)*R+e2[0]*Math.sin(a)*R, y+e1[1]*Math.cos(a)*R+e2[1]*Math.sin(a)*R, z+e1[2]*Math.cos(a)*R+e2[2]*Math.sin(a)*R];
      torusA(out, p, R*0.3, R*0.07, 8, 4, 'rope', 0.3, e1,e2,ax); }
  }
  function pRingStation(out,s){
    const q=[], w = 0.92, ht=1.72;
    for(const sx of [-1,1]) beam(q,[sx*(w/2-0.06),0.1,0],[sx*(w/2-0.06),0.1,ht], 0.05,'wood',0.1);
    backBoard(q, w, 1.0, 0.1, 0.62, 'plank', -0.15);
    box(q, -w/2,w/2, 0.06,0.14, ht-0.1,ht, 'wood', 0.35, grainTex(0.2));
    ringBuoyAt(q, 0,0.02, 1.12, 0.31, EX,EZ,NEY, 0.1);
    for(let k=0;k<4;k++) torusA(q, [w*0.3,0.06,0.78], 0.13-k*0.02, 0.022, 10, 4, 'rope', 0.1+k*0.06, EX,EZ,NEY);
    dY(q, 0.1, -1, -w/2+0.06, w/2-0.06, 0.66, 0.8, 'paper', 0.5, 0.06);
    beam(q,[-w*0.34,0.08,0.72],[-w*0.34,-0.02,0.72], 0.02,'iron',0.4);
    mirrorY(out,q);
  }
  function pRingPost(out,s){
    const ht = 1.05 + (s.len||0)*0.3;
    prismT(out, 0,0, 0.16, 0,0.09, 8, 'iron', -0.1);
    pipe(out, 0,0, 0.055, 0.07, ht, 'galv', 0.05, 10);
    prismT(out, 0,0, 0.07, ht,ht+0.04, 10, 'galv', 0.3);
    ringBuoyAt(out, 0,0.04, ht-0.32, 0.29, EX,EZ,NEY, 0.1);
    beam(out,[0,0.03,ht-0.02],[0,0.03,ht-0.05], 0.02,'iron',0.4);
    ropeCoilAt(out, 0.02,0.3, 0, 0.19, 3, 0.024, 'rope', 0.1);
    ropeRun(out, catPts([0.24,0.06,ht-0.34],[0.14,0.3,0.06], 0.06, 5), 0.022,'rope',0.15);
  }
  function pFireCabinet(out,s){
    const q=[], w=0.44, h=0.7;
    backBoard(q, w+0.24, h+0.5, 0.14, 0.55, 'plank', -0.2);
    box(q, -w/2,w/2, 0.02,0.14, 0.72,0.72+h, 'body', 0.05);
    dY(q, 0.02, -1, -w/2+0.04, w/2-0.04, 0.78, 0.72+h-0.06, 'body', -1.0, 0.05);
    dY(q, 0.02, -1, -w/2+0.09, w/2-0.09, 0.9, 1.2, 'glass', 0.4, 0.07);
    dY(q, 0.02, -1, -0.05, 0.05, 0.84, 0.88, 'iron', 0.8, 0.08);
    dY(q, 0.02, -1, -0.14, 0.14, 0.72+h-0.05, 0.72+h-0.01, 'paper', 0.5, 0.06);
    pipe(q, 0,0.09, 0.075, 0.94, 1.24, 'red', 0.2, 10);                        // extinguisher behind glass
    prismT(q, 0,0.09, 0.05, 1.24, 1.32, 8, 'iron', 0.3);
    if(s.variant===1){ box(q, -0.2,0.2, 0.02,0.14, 0.3,0.66, 'body', 0.02);      // sand bucket below
      prismT(q, 0,0.06, 0.13, 0.06, 0.3, 10, 'red', 0.05);
      heap(q, 0,0.06, 0.24, 0.2,0.2, 0.09, 'salt', 3311, 5, gravelTex(), 0.4); }
    mirrorY(out,q);
  }
  function pNoticeBoard(out,s){
    const q=[], w = 1.05 + (s.len||0)*0.7, h=0.82, ht=1.72;
    for(const sx of [-1,1]) beam(q,[sx*(w/2-0.08),0.06,0],[sx*(w/2-0.08),0.06,ht-0.02], 0.05,'wood',0.1);
    box(q, -w/2,w/2, 0.02,0.09, ht-h-0.06,ht-0.06, 'plank', -0.1, plankTex(0.18));
    box(q, -w/2-0.03,w/2+0.03, 0.02,0.12, ht-0.1,ht+0.04, 'wood', 0.35, grainTex(0.2));
    q.push(F([[-w/2-0.08,-0.16,ht+0.16],[w/2+0.08,-0.16,ht+0.16],[w/2+0.08,0.13,ht+0.02],[-w/2-0.08,0.13,ht+0.02]],
      'plank', 0.42, 0, [[0,0],[w+0.16,0],[w+0.16,0.34],[0,0.34]], plankTex(0.2)));  // little roof
    const rnd=mulberry32(2929);
    for(let i=0;i<6;i++){ const pw=0.14+rnd()*0.1, ph=0.18+rnd()*0.1;
      const px=-w/2+0.1+rnd()*(w-0.3), pz=ht-h+0.06+rnd()*(h-ph-0.14);
      dY(q, 0.02, -1, px,px+pw, pz,pz+ph, 'paper', 0.4+rnd()*0.3, 0.05);
      dY(q, 0.02, -1, px+pw*0.4,px+pw*0.5, pz+ph-0.03,pz+ph, 'iron', 0.8, 0.07); }
    if(s.variant===1) dY(q, 0.02, -1, -w/2+0.06, w/2-0.06, ht-0.24, ht-0.1, 'body', 0.5, 0.06);
    mirrorY(out,q);
  }
  function pChalkSign(out,s){
    const w = 0.66, h = 0.94, lean = 0.24;
    for(const sy of [-1,1]) for(const sx of [-1,1])
      beam(out,[sx*(w/2-0.03), sy*lean, 0],[sx*(w/2-0.06), sy*0.02, h], 0.032,'wood',sy>0?0.05:0.15);
    for(const sy of [-1,1]){
      out.push(F(sy<0
        ? [[-w/2,-lean*0.9,0.1],[w/2,-lean*0.9,0.1],[w/2,-0.03,h-0.08],[-w/2,-0.03,h-0.08]]
        : [[w/2,lean*0.9,0.1],[-w/2,lean*0.9,0.1],[-w/2,0.03,h-0.08],[w/2,0.03,h-0.08]],
        'slate', sy<0?0.3:-0.6, 0, [[0,0],[w,0],[w,h],[0,h]], null));
      for(let i=0;i<4;i++){ const t=0.2+i*0.19, zz=0.1+(h-0.18)*t, yy=(-lean*0.9+(0.03-(-lean*0.9))*t)*sy*(sy<0?1:1);
        const yv = sy<0 ? -lean*0.9+(lean*0.9-0.03)*t : lean*0.9-(lean*0.9-0.03)*t;
        const lw = (i%2? 0.36: 0.5)*w;
        decalY(out, yv, sy, -lw/2, lw/2, zz, zz+0.035, 'paper', sy<0?1.0:0.2, null, true, 0.06); } }
    box(out, -w/2-0.02,w/2+0.02, -0.05,0.05, h-0.1,h-0.02, 'wood', 0.4);
    for(const sx of [-1,1]) beam(out,[sx*(w/2-0.04),-lean*0.5,h*0.42],[sx*(w/2-0.04),lean*0.5,h*0.42], 0.02,'rope',0.2);
  }
  function pTideStaff(out,s){
    const q=[], ht = 2.4 + (s.len||0)*1.6;
    pipe(q, 0,0, 0.13, 0, ht*0.28, 'pole', 0.0, 10, plankTex(0.14));
    box(q, -0.11,0.11, -0.06,0.06, ht*0.26, ht, 'plank', 0.1, plankTex(0.16));
    for(let i=0;i<=10;i++){ const z=ht*0.3+ i*((ht-ht*0.34)/10);
      const lw = (i%5===0)?0.1:0.06;
      decalY(q, -0.06, -1, -lw, lw, z, z+0.03, i%5===0?'paper':'iron', i%5===0?0.9:0.4, null, true, 0.06);
      if(i%5===0) decalY(q, -0.06, -1, -0.045, 0.045, z+0.045, z+0.085, 'paper', 0.8, null, true, 0.06); }
    box(q, -0.15,0.15, -0.07,0.05, ht,ht+0.16, 'body', 0.3);
    dY(q, -0.07, -1, -0.11,0.11, ht+0.03, ht+0.13, 'paper', 0.6, 0.06);
    for(const sx of [-1,1]) beam(q,[sx*0.12,0.04,ht*0.3],[sx*0.02,0.04,ht*0.26], 0.02,'galv',0.2);
    if(s.variant===1){ prismT(q, 0.24,0.1, 0.06, 0,0.06, 8,'iron',0.2);        // float tube alongside
      pipe(q, 0.24,0.1, 0.05, 0.04, ht*0.72, 'galv', 0.1, 8); }
    mirrorY(out,q);
  }
  function pHarbourSign(out,s){
    const q=[], w = 1.9 + (s.len||0)*1.2, h = 0.56, ht = 1.9 + (s.len||0)*0.3;
    for(const sx of [-1,1]){ box(q, sx*(w/2-0.06)-0.06, sx*(w/2-0.06)+0.06, -0.07,0.07, 0,ht, 'pole', 0.1, plankTex(0.14));
      prismT(q, sx*(w/2-0.06),0, 0.1, ht,ht+0.07, 6, 'pole', 0.35); }
    box(q, -w/2,w/2, -0.04,0.04, ht-h-0.1, ht-0.1, 'plank', 0.05, plankTex(0.2));
    for(const zz of [ht-h-0.1, ht-0.14]) box(q, -w/2-0.03,w/2+0.03, -0.055,0.055, zz,zz+0.045, 'wood', 0.32);
    decalY(q, -0.04, -1, -w*0.4, w*0.4, ht-h+0.14, ht-h+0.3, 'body', 0.9, null, true, 0.07);
    decalY(q, -0.04, -1, -w*0.26, w*0.26, ht-h+0.36, ht-h+0.46, 'body', 0.7, null, true, 0.07);
    if(s.variant===1){ q.push(F([[-w/2-0.06,-0.2,ht-0.02],[w/2+0.06,-0.2,ht-0.02],[w/2+0.06,0.1,ht-0.14],[-w/2-0.06,0.1,ht-0.14]],
      'plank', 0.44, 0, [[0,0],[w,0],[w,0.34],[0,0.34]], plankTex(0.2))); }
    if(s.loaded){ buoy(q, [w/2-0.3,-0.06,ht-h-0.16],[w/2-0.2,-0.1,ht-h-0.66], 0.055,'buoy1','buoy2',0.1);
      beam(q,[w/2-0.3,-0.05,ht-h-0.1],[w/2-0.3,-0.06,ht-h-0.16], 0.012,'rope',0.3); }
    mirrorY(out,q);
  }
  function pRescueLadder(out,s){
    const drop = 1.5 + (s.len||0)*1.1, w=0.42;
    box(out, -w/2-0.09,w/2+0.09, -0.16,0.05, 0,0.07, 'iron', -0.1, null, true);   // rail-clamp shoe
    for(const sx of [-1,1]){ beam(out,[sx*w/2,0,0.05],[sx*w/2,0,1.0], 0.035,'galv',0.15);
      beam(out,[sx*w/2,0,1.0],[sx*w/2,-0.26,1.06], 0.035,'galv',0.2);
      beam(out,[sx*w/2,-0.26,1.06],[sx*w/2,-0.3,1.06-drop], 0.032,'galv',0.1); }
    for(let i=0;i<7;i++){ const t=i/6, zz=1.02-drop*t*0.98, yy=-0.27-0.04*t;
      if(zz>0.98) continue;
      tube(out,[-w/2,yy,zz],[w/2,yy,zz], 0.026, 8,'galv',0.3); }
    for(const zz of [0.34, 0.86]) tube(out,[-w/2,0,zz],[w/2,0,zz], 0.026, 8,'galv',0.3);
    tube(out,[-w/2-0.02,-0.13,1.03],[w/2+0.02,-0.13,1.03], 0.03, 8,'iron',0.35);
    dY(out, -0.16, -1, -0.1,0.1, 0.02,0.06, 'body', 0.6, 0.06);
    if(s.loaded) ropeCoilAt(out, 0,0.24, 0, 0.16, 3, 0.024, 'poly', 0.1);
  }

  // ---------------- DRYING & CURING ----------------
  function pCodFlake(out,s){
    const w = 2.0 + (s.len||0)*1.6, d = 1.35 + (s.len||0)*0.7, ht=0.92;
    const nx = Math.max(3, Math.round(w/0.9));
    for(let i=0;i<nx;i++){ const x=-w/2+0.12+ i*((w-0.24)/(nx-1));
      for(const sy of [-1,1]) beam(out,[x,sy*(d/2-0.1),0],[x,sy*(d/2-0.1),ht], 0.05,'pole',0.1); }
    for(const sy of [-1,1]) box(out, -w/2,w/2, sy*(d/2-0.1)-0.045,sy*(d/2-0.1)+0.045, ht-0.09,ht-0.02, 'wood', 0.25, grainTex(0.2));
    const nb = Math.max(6, Math.round(d/0.17));
    for(let j=0;j<nb;j++){ const y=-d/2+0.1+ j*((d-0.2)/(nb-1));
      box(out, -w/2,w/2, y-0.028,y+0.028, ht-0.02,ht+0.025, 'wood', 0.32, grainTex(0.3)); }
    if(s.loaded!==false){ const rnd=mulberry32(6767);
      const rows=Math.max(2, Math.round(d/0.34)), cols=Math.max(3, Math.round(w/0.42));
      for(let r=0;r<rows;r++) for(let c=0;c<cols;c++){
        if(rnd()<0.14) continue;
        const x=-w/2+0.24+ c*((w-0.48)/(cols-1)) + (rnd()-0.5)*0.05;
        const y=-d/2+0.26+ r*((d-0.52)/Math.max(1,rows-1)) + (rnd()-0.5)*0.05;
        const L=0.34+rnd()*0.08, Wf=0.19+rnd()*0.05, z0=ht+0.035;
        box(out, x-Wf/2,x+Wf/2, y-L/2,y+L/2, z0, z0+0.045+rnd()*0.02, 'fish', 0.15+rnd()*0.4, scaleTex());
        box(out, x-Wf*0.2,x+Wf*0.2, y-L*0.46,y+L*0.2, z0+0.04, z0+0.065, 'fish', 0.5, scaleTex()); } }
    if(s.tarp) tarpOver(out, -w/2-0.08,w/2+0.08, -d/2-0.08,d/2+0.08, ht+0.32, 0.34, 0.05);
  }
  function pFishRack(out,s){
    const w = 1.6 + (s.len||0)*1.2, ht = 2.0 + (s.len||0)*0.4;
    for(const sx of [-1,1]){ beam(out,[sx*(w/2-0.08),-0.36,0],[sx*(w/2-0.08),0,ht], 0.055,'pole',0.1);
      beam(out,[sx*(w/2-0.08),0.36,0],[sx*(w/2-0.08),0,ht], 0.055,'pole',-0.05);
      beam(out,[sx*(w/2-0.08),-0.3,0.8],[sx*(w/2-0.08),0.3,0.8], 0.032,'wood',0.2); }
    for(const zz of [ht-0.04, ht-0.5]) box(out, -w/2,w/2, -0.04,0.04, zz-0.04,zz+0.02, 'wood', 0.3, grainTex(0.24));
    if(s.loaded!==false){ const rnd=mulberry32(4343);
      [ht-0.06, ht-0.52].forEach((rz,ri)=>{ const n=Math.max(4, Math.round(w/0.24));
        for(let i=0;i<n;i++){ if(rnd()<0.12) continue;
          const x=-w/2+0.14+ i*((w-0.28)/(n-1)), L=0.42+rnd()*0.14, tw=0.13+rnd()*0.05;
          const yy = (ri?0.03:-0.03)+(rnd()-0.5)*0.05;
          beam(out,[x,yy,rz],[x,yy,rz-0.06], 0.008,'rope',0.3);
          out.push(F([[x-tw/2,yy-0.012,rz-0.06],[x+tw/2,yy-0.012,rz-0.06],[x+tw/2,yy-0.012,rz-0.06-L],[x-tw/2,yy-0.012,rz-0.06-L]],
            'fish', 0.2+rnd()*0.5, 0.05, [[0,0],[tw,0],[tw,L],[0,L]], scaleTex(), false));
          out.push(F([[x+tw/2,yy+0.012,rz-0.06],[x-tw/2,yy+0.012,rz-0.06],[x-tw/2,yy+0.012,rz-0.06-L],[x+tw/2,yy+0.012,rz-0.06-L]],
            'fish', -0.7, 0.05)); } }); }
  }
  function pNetFrame(out,s){
    const w = 2.1 + (s.len||0)*1.5, ht = 1.9 + (s.len||0)*0.5;
    for(const sx of [-1,1]){ beam(out,[sx*(w/2-0.06),0.02,0],[sx*(w/2-0.06),0.02,ht], 0.055,'pole',0.1);
      beam(out,[sx*(w/2-0.06),0.02,ht*0.5],[sx*(w/2-0.06)-sx*0.5,-0.42,0], 0.036,'wood',0.05); }
    box(out, -w/2,w/2, -0.04,0.06, ht-0.06,ht, 'wood', 0.3, grainTex(0.24));
    if(s.loaded!==false){ const drop = ht*0.78;
      out.push(F([[-w/2+0.08,0.0,ht-0.06],[w/2-0.08,0.0,ht-0.06],[w/2-0.08,0.06,ht-0.06-drop],[-w/2+0.08,0.06,ht-0.06-drop]],
        'net', 0.1, 0.04, [[0,0],[w,0],[w,drop],[0,drop]], netTex(0.04), false));
      const rnd=mulberry32(1717);
      for(let i=0;i<5;i++){ const x=-w/2+0.3+rnd()*(w-0.6);
        prismT(out, x, 0.04, 0.07, ht-0.06-drop*0.9, ht-0.06-drop*0.9+0.12, 8,'foam',0.2); }
      for(let i=0;i<4;i++){ const x=-w/2+0.24+rnd()*(w-0.48);
        ropeRun(out, catPts([x,0.02,ht-0.08],[x+0.16,0.05,ht-0.4], 0.06, 4), 0.014,'rope',0.2); } }
    heap(out, w*0.28,-0.3, 0, 0.6,0.4, 0.2, 'net', 1751, 8, netTex(), -0.1);
  }
  function pOilskinLine(out,s){
    const w = 1.9 + (s.len||0)*1.4, ht = 1.86;
    for(const sx of [-1,1]) beam(out,[sx*(w/2),0.04,0],[sx*(w/2),0.04,ht], 0.05,'pole',0.1);
    ropeRun(out, catPts([-w/2,0.04,ht-0.06],[w/2,0.04,ht-0.06], 0.07, 8), 0.016,'rope',0.25);
    const items=[['gold',0.78,0.42],['sage',0.62,0.36],['gold',0.56,0.46],['greyShingle',0.7,0.38]];
    const n=Math.min(items.length, Math.max(2, Math.round(w/0.62)));
    for(let i=0;i<n;i++){ const t=(i+0.7)/(n+0.4), x=-w/2+w*t;
      const sag=Math.sin(Math.PI*t)*0.07, zt=ht-0.08-sag, it=items[i], L=it[1], Wg=it[2];
      box(out, x-Wg/2,x+Wg/2, -0.03,0.07, zt-L, zt, it[0], 0.1, canvasTex(), true);
      slab(out, [[x-Wg/2,-0.03],[x+Wg/2,-0.03],[x+Wg/2,0.07],[x-Wg/2,0.07]], zt, it[0], 0.44, canvasTex());
      for(const sx of [-1,1]) box(out, x+sx*Wg/2-sx*0.02, x+sx*Wg/2+sx*0.09, -0.02,0.05, zt-L*0.62, zt-L*0.22, it[0], sx>0?0.2:-0.15, canvasTex()); // sleeves
      for(const sx of [-1,1]) beam(out,[x+sx*Wg*0.36,0.02,zt+0.03],[x+sx*Wg*0.36,0.02,zt-0.04], 0.016,'galv',0.5);
      if(i%2===0){ for(const sx of [-1,1]) tube(out, [x+sx*0.1,-0.24,0],[x+sx*0.1,-0.24,0.3], 0.07, 8, 'rubber', 0.1+sx*0.06);
        for(const sx of [-1,1]) box(out, x+sx*0.1-0.06,x+sx*0.1+0.06, -0.38,-0.24, 0,0.07, 'rubber', 0.3); } }
  }
  function pHerringSticks(out,s){
    const w = 1.5 + (s.len||0)*1.0, ht = 1.65, rows = 3;
    for(const sx of [-1,1]) beam(out,[sx*(w/2),0.02,0],[sx*(w/2),0.02,ht], 0.05,'pole',0.1);
    for(let r=0;r<rows;r++){ const zz=ht-0.1-r*0.44;
      tube(out,[-w/2,0.02,zz],[w/2,0.02,zz], 0.028, 8,'wood',0.28);
      const n=Math.max(4, Math.round(w/0.2)), rnd=mulberry32(929+r*13);
      for(let i=0;i<n;i++){ if(rnd()<0.1) continue;
        const x=-w/2+0.1+ i*((w-0.2)/(n-1));
        beam(out,[x,-0.16,zz+0.01],[x,0.16,zz-0.02], 0.012,'wood',0.35);         // the stick
        for(const yy of [-0.09, 0.02, 0.11]){ const L=0.2+rnd()*0.07, tw=0.055+rnd()*0.02;
          out.push(F([[x-tw,yy-0.01,zz-0.03],[x+tw,yy-0.01,zz-0.03],[x+tw,yy-0.01,zz-0.03-L],[x-tw,yy-0.01,zz-0.03-L]],
            'fish', 0.1+rnd()*0.5, 0.05, [[0,0],[tw*2,0],[tw*2,L],[0,L]], scaleTex(), false)); } } }
    if(s.tarp) tarpOver(out, -w/2-0.06,w/2+0.06, -0.3,0.3, ht+0.1, 0.3, 0.05);
  }

  // ---------------- DECOR (working register) ----------------
  function pBench(out,s){
    const w = 1.5 + (s.len||0)*0.9, d=0.42, sh=0.44;
    for(const sx of [-1,1]){ box(out, sx*(w/2-0.12)-0.04, sx*(w/2-0.12)+0.04, -d/2+0.02,-d/2+0.1, 0,sh, 'wood', 0.1);
      box(out, sx*(w/2-0.12)-0.04, sx*(w/2-0.12)+0.04, d/2-0.1,d/2-0.02, 0,sh+0.42, 'wood', 0.05);
      beam(out,[sx*(w/2-0.12),-d/2+0.06,0.1],[sx*(w/2-0.12),d/2-0.06,0.1], 0.026,'wood',0.2); }
    for(let i=0;i<3;i++){ const y=-d/2+0.06+ i*((d-0.14)/2);
      box(out, -w/2,w/2, y-0.055,y+0.055, sh-0.05,sh, 'plank', 0.28, plankTex(0.2)); }
    for(let i=0;i<2;i++){ const z=sh+0.2+i*0.18;
      box(out, -w/2,w/2, d/2-0.09,d/2-0.03, z,z+0.11, 'plank', 0.15, plankTex(0.2)); }
    if(s.loaded){ ropeCoilAt(out, w*0.28,-0.02, sh, 0.14, 2, 0.024, 'rope', 0.2);
      prismT(out, -w*0.3,0.0, 0.07, sh, sh+0.2, 8, 'body', 0.2); }              // a tin mug
  }
  function pPicnicTable(out,s){
    const w = 1.9 + (s.len||0)*0.9, d=0.78, th=0.74, sh=0.44;
    for(const sx of [-1,1]){ for(const sy of [-1,1])
      beam(out,[sx*(w/2-0.22), sy*(d/2+0.34), 0],[sx*(w/2-0.22), sy*(d/2-0.24), th-0.06], 0.05,'wood',0.1);
      beam(out,[sx*(w/2-0.22),-d/2-0.3,sh-0.06],[sx*(w/2-0.22),d/2+0.3,sh-0.06], 0.036,'wood',0.2); }
    for(let i=0;i<4;i++){ const y=-d/2+0.08+ i*((d-0.16)/3);
      box(out, -w/2,w/2, y-0.085,y+0.085, th-0.055,th, 'plank', 0.3, plankTex(0.22)); }
    for(const sy of [-1,1]) for(let i=0;i<2;i++){ const y=sy*(d/2+0.2+i*0.19);
      box(out, -w/2+0.06,w/2-0.06, y-0.085,y+0.085, sh-0.05,sh, 'plank', 0.24, plankTex(0.2)); }
    if(s.loaded){ toteShell(out, w*0.24,0, th, 0.42,0.3,0.2, 'body', 0.1, null, true);
      barrelAt(out, -w*0.3,0.04, th, 0.09, 0.22, 'wood', 0.2, 'iron');
      heap(out, 0,0, th, 0.4,0.3, 0.08, 'net', 8383, 5, netTex(), 0.1); }
  }
  function pDeckChair(out,s){
    const w=0.62, sh=0.34;
    for(const sx of [-1,1]){
      out.push(F([[sx*w/2-0.03,-0.34,0],[sx*w/2+0.03,-0.34,0],[sx*w/2+0.03,0.34,0.06],[sx*w/2-0.03,0.34,0.06]],'wood',sx>0?0.2:-0.4));
      beam(out,[sx*(w/2-0.02),-0.3,0.02],[sx*(w/2-0.02),-0.06,sh], 0.03,'wood',0.15);
      beam(out,[sx*(w/2-0.02),0.3,0.04],[sx*(w/2-0.02),0.16,sh+0.02], 0.03,'wood',0.1);
      beam(out,[sx*(w/2-0.02),0.2,sh],[sx*(w/2-0.02),0.34,sh+0.62], 0.032,'wood',0.05);
      out.push(F([[sx*w/2-0.03,-0.34,sh],[sx*w/2+0.03,-0.34,sh],[sx*w/2+0.03,-0.02,sh+0.24],[sx*w/2-0.03,-0.02,sh+0.24]],'wood',sx>0?0.3:-0.3)); }
    for(let i=0;i<5;i++){ const t=i/4, y=-0.3+t*0.46, z=sh+t*0.02;
      box(out, -w/2,w/2, y-0.045,y+0.045, z-0.03,z, 'plank', 0.3-t*0.05, plankTex(0.16)); }
    for(let i=0;i<5;i++){ const t=i/4, z=sh+0.06+t*0.56, y=0.18+t*0.16;
      box(out, -w/2+0.02,w/2-0.02, y-0.03,y+0.03, z,z+0.08, 'plank', 0.18, plankTex(0.16)); }
    box(out, -w/2-0.04,w/2+0.04, 0.28,0.4, sh+0.66,sh+0.74, 'plank', 0.35);
    if(s.loaded){ out.push(F([[-w/2,-0.28,sh+0.02],[w/2,-0.28,sh+0.02],[w/2-0.04,0.08,sh+0.2],[-w/2+0.04,0.08,sh+0.2]],
      'canvas', 0.4, 0.05, [[0,0],[w,0],[w,0.4],[0,0.4]], canvasTex(), false)); }
  }
  function pDrumPlanter(out,s){
    const n = s.variant===1?2:1, spots = n===1?[[0,0]]:[[-0.34,0.05],[0.32,-0.05]];
    spots.forEach((p,i)=>{ const h=0.56;
      tube(out, [p[0],p[1],0],[p[0],p[1],h], 0.3, 14, 'body', i*0.05, rustTex());
      for(const zz of [h*0.36, h*0.72]) tube(out, [p[0],p[1],zz],[p[0],p[1],zz+0.035], 0.312, 14, 'body', (i*0.05)+0.4, null, false);
      tube(out, [p[0],p[1],h-0.05],[p[0],p[1],h], 0.315, 14, 'iron', 0.4, null, false);
      const q=[]; for(let j=0;j<14;j++){ const t=(j/14)*Math.PI*2; q.push([p[0]+Math.cos(t)*0.29, p[1]-Math.sin(t)*0.29]); }
      slab(out, q, h-0.14, 'wood', -0.9, gravelTex());
      if(s.loaded!==false){ heap(out, p[0],p[1], h-0.16, 0.44,0.44, 0.2, 'net', 5959+i*7, 9, netTex(0.03), 0.3);
        const rnd=mulberry32(717+i);
        for(let k=0;k<4;k++){ const a=rnd()*Math.PI*2, r=rnd()*0.18;
          prismT(out, p[0]+Math.cos(a)*r, p[1]+Math.sin(a)*r, 0.035, h-0.06, h+0.06+rnd()*0.1, 6, 'body', 0.6); } } });
  }
  function pPlanterBox(out,s){
    const w = 1.1 + (s.len||0)*0.8, d=0.46, h=0.4;
    box(out, -w/2,w/2, -d/2,d/2, 0.06,h, 'wood', 0, slatTex(0.1), true);
    for(const sx of [-1,1]) for(const sy of [-1,1]) beam(out,[sx*(w/2-0.04),sy*(d/2-0.04),0],[sx*(w/2-0.04),sy*(d/2-0.04),h+0.03], 0.045,'wood',0.25);
    box(out, -w/2-0.03,w/2+0.03, -d/2-0.03,d/2+0.03, h,h+0.05, 'wood', 0.35, null, true);
    slab(out, [[-w/2+0.05,-d/2+0.05],[w/2-0.05,-d/2+0.05],[w/2-0.05,d/2-0.05],[-w/2+0.05,d/2-0.05]], h-0.1, 'gravel', -0.8, gravelTex());
    if(s.loaded!==false){ heap(out, 0,0, h-0.12, w*0.82,d*0.7, 0.24, 'net', 6363, 12, netTex(0.03), 0.25);
      const rnd=mulberry32(6364);
      for(let k=0;k<6;k++) prismT(out, (rnd()*2-1)*w*0.36, (rnd()*2-1)*d*0.28, 0.035, h+0.02, h+0.12+rnd()*0.12, 6, 'body', 0.6); }
  }
  function pFlagpole(out,s){
    const ht = 5.4 + (s.len||0)*3.2;
    prismT(out, 0,0, 0.28, 0,0.14, 10, 'concrete', 0, gravelTex());
    for(let i=0;i<4;i++){ const a=i/4*Math.PI*2+0.4; prismT(out, Math.cos(a)*0.19, Math.sin(a)*0.19, 0.03, 0.1,0.2, 6,'galv',0.3); }
    pipe(out, 0,0, 0.075, 0.12, ht*0.5, 'galv', 0.05, 10);
    pipe(out, 0,0, 0.058, ht*0.5, ht, 'galv', 0.08, 10);
    prismT(out, 0,0, 0.075, ht, ht+0.09, 8, 'brass', 0.35);
    beam(out,[0,0,ht-0.06],[0.14,0,ht-0.06], 0.02,'galv',0.3);
    torusA(out, [0.15,0,ht-0.08], 0.035, 0.012, 8, 4, 'iron', 0.3, EY, EZ, EX);
    ropeRun(out, [[0.14,0.01,ht-0.1],[0.06,0.01,1.3]], 0.012,'rope',0.2,6);
    ropeRun(out, [[0.1,-0.01,ht-0.1],[0.05,-0.01,1.3]], 0.012,'rope',0.2,6);
    box(out, -0.09,0.09, -0.09,0.09, 1.16,1.34, 'galv', 0.2);                     // cleat block
    for(const sx of [-1,1]) tube(out,[sx*0.02,-0.09,1.26],[sx*0.09,-0.16,1.26], 0.02, 6,'iron',0.4);
    if(s.loaded!==false){ const L=1.05, hh=0.44, z=ht-0.24;
      out.push(F([[0.1,0.0,z],[0.1,0.0,z-hh],[0.1-0.02,-L,z-hh*0.62],[0.1-0.02,-L,z-hh*0.24]], 'red', 0.4, 0.05,
        [[0,0],[0,hh],[L,hh*0.5],[L,0]], canvasTex(), false));
      out.push(F([[0.14,0.0,z-hh],[0.14,0.0,z],[0.14-0.02,-L,z-hh*0.24],[0.14-0.02,-L,z-hh*0.62]], 'red', -0.7, 0.05)); }
  }
  function pBunting(out,s){
    const w = 2.6 + (s.len||0)*1.8, ht = 2.2 + (s.len||0)*0.4;
    for(const sx of [-1,1]){ pipe(out, sx*w/2, 0, 0.075, 0, ht, 'pole', 0.05, 10, plankTex(0.14));
      prismT(out, sx*w/2,0, 0.095, ht, ht+0.05, 8, 'pole', 0.35);
      beam(out,[sx*w/2,0,ht*0.42],[sx*(w/2-0.44),-0.3,0.04], 0.03,'rope',0.1); }
    const line=catPts([-w/2,0.02,ht-0.1],[w/2,0.02,ht-0.1], 0.26, 12);
    ropeRun(out, line, 0.014,'rope',0.25,6);
    const cols=['red','cream','teal','gold','blue'];
    for(let i=0;i+1<line.length;i++){ const a=line[i], b=line[i+1];
      const mx=(a[0]+b[0])/2, my=(a[1]+b[1])/2, mz=(a[2]+b[2])/2, L=0.26;
      out.push(F([[a[0],my,a[2]-0.01],[b[0],my,b[2]-0.01],[mx,my,mz-L]], cols[i%cols.length], 0.4, 0.05,
        [[0,0],[b[0]-a[0],0],[(b[0]-a[0])/2,L]], canvasTex(), false));
      out.push(F([[b[0],my+0.035,b[2]-0.01],[a[0],my+0.035,a[2]-0.01],[mx,my+0.035,mz-L]], cols[i%cols.length], -0.75, 0.05)); }
  }
  function pLanternPost(out,s){
    const ht = 1.9 + (s.len||0)*1.0;
    prismT(out, 0,0, 0.2, 0,0.1, 8, 'concrete', 0, gravelTex());
    box(out, -0.07,0.07, -0.07,0.07, 0.08,ht, 'pole', 0.05, plankTex(0.13));
    box(out, -0.1,0.1, -0.1,0.1, ht,ht+0.05, 'pole', 0.35);
    beam(out,[0,0,ht+0.02],[0,-0.3,ht+0.06], 0.025,'iron',0.3);
    torusA(out, [0,-0.31,ht-0.02], 0.05, 0.014, 8, 4, 'iron', 0.3, EX, EZ, NEY);
    const lz = ht-0.34;
    box(out, -0.09,0.09, -0.4,-0.22, lz,lz+0.05, 'iron', 0.2);                    // lantern base
    for(const sx of [-1,1]) for(const sy of [-1,1]) beam(out,[sx*0.075,-0.31+sy*0.075,lz],[sx*0.075,-0.31+sy*0.075,lz+0.3], 0.012,'iron',0.4);
    box(out, -0.07,0.07, -0.38,-0.24, lz+0.02,lz+0.28, 'glass', 0.3);
    prismT(out, 0,-0.31, 0.035, lz+0.06, lz+0.2, 6, 'glow', 0.3);
    out.push(F([[-0.11,-0.42,lz+0.3],[0.11,-0.42,lz+0.3],[0,-0.31,lz+0.42]], 'iron', 0.45));
    out.push(F([[0.11,-0.2,lz+0.3],[-0.11,-0.2,lz+0.3],[0,-0.31,lz+0.42]], 'iron', 0.15));
    out.push(F([[0.11,-0.42,lz+0.3],[0.11,-0.2,lz+0.3],[0,-0.31,lz+0.42]], 'iron', 0.3));
    out.push(F([[-0.11,-0.2,lz+0.3],[-0.11,-0.42,lz+0.3],[0,-0.31,lz+0.42]], 'iron', 0.05));
    prismT(out, 0,-0.31, 0.02, lz+0.42, lz+0.5, 6, 'iron', 0.4);
    if(s.loaded){ dY(out, -0.07, -1, -0.06,0.06, 1.0,1.26, 'paper', 0.5, 0.06); } // nailed notice
  }
  function pAnchorProp(out,s){
    const L = 1.5 + (s.len||0)*0.7, lean=0.42;
    const top=[0,-lean,L], bot=[0,0.06,0.14];
    tube(out, bot, top, 0.075, 8, 'iron', 0.1, rustTex());
    torusA(out, [0,-lean*1.06,L+0.11], 0.1, 0.032, 10, 5, 'iron', 0.25, EX, EZ, NEY);   // ring
    tube(out, [-0.4,-lean*0.86,L-0.16],[0.4,-lean*0.86,L-0.16], 0.05, 8, 'iron', 0.3);  // stock
    for(const sx of [-1,1]) prismT(out, sx*0.42,-lean*0.86, 0.065, L-0.24,L-0.09, 6,'iron',0.35);
    const cz=0.26, cy=0.04;
    prismT(out, 0,cy, 0.11, 0.06, cz+0.06, 8, 'iron', 0.05);                            // crown
    for(const sx of [-1,1]){ const tip=[sx*0.5,cy+0.2,cz+0.2];
      tube(out, [sx*0.05,cy,cz],[tip[0]*0.8,tip[1]*0.8,tip[2]*0.86], 0.055, 7,'iron',sx>0?0.2:0.05);
      tube(out, [tip[0]*0.8,tip[1]*0.8,tip[2]*0.86], tip, 0.045, 7,'iron',sx>0?0.25:0.1);
      // solid fluke blade
      box(out, sx>0?tip[0]-0.02:tip[0]-0.2, sx>0?tip[0]+0.2:tip[0]+0.02, tip[1]-0.16,tip[1]+0.1, tip[2]-0.06, tip[2]+0.22, 'iron', sx>0?0.3:-0.05, rustTex());
      prismT(out, tip[0]+sx*0.2, tip[1]-0.03, 0.07, tip[2]+0.2, tip[2]+0.3, 6, 'iron', 0.34); }
    if(s.loaded) ropeCoilAt(out, 0.36,-0.4, 0, 0.18, 3, 0.028, 'rope', 0.1);
    if(s.variant===1) box(out, -0.62,0.62, -0.16,0.42, 0,0.14, 'plank', -0.2, plankTex(0.14)); // on a skid
  }
  function pRopeFence(out,s){
    const w = 2.4 + (s.len||0)*1.8, ht = 0.92, n = 3;
    for(let i=0;i<n;i++){ const x=-w/2+ i*(w/(n-1));
      pipe(out, x,0, 0.075, 0, ht, 'pole', 0.05, 10, plankTex(0.13));
      prismT(out, x,0, 0.09, ht,ht+0.05, 8, 'pole', 0.35);
      for(const sy of [-1,1]) tube(out,[x,0,ht-0.14],[x,sy*0.09,ht-0.14], 0.022, 6,'iron',0.3); }
    for(let i=0;i+1<n;i++){ const x0=-w/2+ i*(w/(n-1)), x1=-w/2+(i+1)*(w/(n-1));
      ropeRun(out, catPts([x0,0.0,ht-0.15],[x1,0.0,ht-0.15], 0.16, 8), 0.026, i%2?'poly':'rope', 0.2);
      if(s.variant===1) ropeRun(out, catPts([x0,0.0,ht-0.44],[x1,0.0,ht-0.44], 0.13, 8), 0.024, 'rope', 0.15); }
    if(s.loaded){ const x=-w/2+w*0.5;
      buoy(out, [x+0.1,0.02,ht-0.2],[x+0.16,0.05,ht-0.7], 0.055,'buoy1','buoy3',0.1);
      beam(out,[x+0.1,0.02,ht-0.15],[x+0.1,0.02,ht-0.2], 0.012,'rope',0.3); }
  }

  // ---------------- WASTE & ODDS ----------------
  function pTrashBarrel(out,s){
    const h = 0.86, r=0.31;
    tube(out, [0,0,0],[0,0,h], r, 14, 'galv', 0.05, corrTex(0.1));
    for(const zz of [h*0.24, h*0.62]) tube(out, [0,0,zz],[0,0,zz+0.03], r*1.04, 14, 'galv', 0.45, null, false);
    tube(out, [0,0,h-0.05],[0,0,h], r*1.03, 14, 'galv', 0.4, null, false);
    if(s.variant===1){        // lid on
      const q=[]; for(let j=0;j<14;j++){ const t=(j/14)*Math.PI*2; q.push([Math.cos(t)*r*1.05, -Math.sin(t)*r*1.05]); }
      slab(out, q, h+0.04, 'galv', 0.4, corrTex(0.12));
      tube(out,[0,0,h+0.04],[0,0,h+0.1], 0.06, 8,'galv',0.5);
    } else {
      const q=[]; for(let j=0;j<14;j++){ const t=(j/14)*Math.PI*2; q.push([Math.cos(t)*r*0.94, -Math.sin(t)*r*0.94]); }
      slab(out, q, h*0.2, 'galv', -1.0);
      if(s.loaded){ heap(out, 0,0, h*0.2, r*1.5, r*1.5, 0.5, 'net', 2727, 10, netTex(), -0.1);
        heap(out, 0.04,-0.02, h*0.6, r*1.2, r*1.1, 0.34, 'paper', 2728, 6, canvasTex(), 0.2); }
    }
    for(const sx of [-1,1]) tube(out,[sx*r,0,h*0.62],[sx*(r+0.07),0,h*0.55], 0.022, 6,'iron',0.3);
  }
  function pOilDrum(out,s){
    const n = s.variant===1?2:(s.variant===2?3:1);
    const spots = n===1?[[0,0,0]] : n===2?[[-0.34,0.04,0],[0.32,-0.04,0]] : [[-0.34,0.06,0],[0.32,-0.02,0],[0.0,0.3,0.88]];
    spots.forEach((p,i)=>{ const h=0.88, r=0.29;
      if(p[2]>0){ // one lying on its side across the pair
        tube(out, [-0.42,p[1],r+0.86],[0.42,p[1],r+0.86], r, 14, 'body', i*0.05, corrTex(0.11));
        for(const xx of [-0.24,0.24]) tube(out, [xx,p[1],r+0.86],[xx+0.04,p[1],r+0.86], r*1.04, 14, 'body', 0.45, null, false);
        return; }
      tube(out, [p[0],p[1],0],[p[0],p[1],h], r, 14, 'body', i*0.05, corrTex(0.11));
      for(const zz of [h*0.3, h*0.66]) tube(out, [p[0],p[1],zz],[p[0],p[1],zz+0.035], r*1.05, 14, 'body', (i*0.05)+0.45, null, false);
      const q=[]; for(let j=0;j<14;j++){ const t=(j/14)*Math.PI*2; q.push([p[0]+Math.cos(t)*r, p[1]-Math.sin(t)*r]); }
      slab(out, q, h, 'body', 0.34, rustTex());
      prismT(out, p[0]+r*0.5, p[1]-r*0.2, 0.05, h, h+0.03, 8, 'iron', 0.5);
      if(s.loaded) dY(out, p[1]-r, -1, p[0]-0.14, p[0]+0.14, h*0.42, h*0.62, 'paper', 0.5, 0.06); });
    if(s.tarp) tarpOver(out, -0.7,0.7, -0.4,0.5, 1.0, 0.34, 0);
  }
  function pSaltBox(out,s){
    const w = 1.0 + (s.len||0)*0.7, d=0.66, h=0.58;
    box(out, -w/2,w/2, -d/2,d/2, 0,h, 'body', 0, slatTex(0.11), true);
    for(const sx of [-1,1]) for(const sy of [-1,1]) beam(out,[sx*(w/2-0.04),sy*(d/2-0.04),0],[sx*(w/2-0.04),sy*(d/2-0.04),h], 0.04,'wood',0.25);
    out.push(F([[-w/2-0.04,-d/2-0.04,h+0.16],[w/2+0.04,-d/2-0.04,h+0.16],[w/2+0.04,d/2+0.04,h+0.3],[-w/2-0.04,d/2+0.04,h+0.3]],
      'plank', 0.42, 0, [[0,0],[w,0],[w,d],[0,d]], plankTex(0.2)));
    for(const sx of [-1,1]) beam(out,[sx*(w/2-0.06),d/2+0.02,h],[sx*(w/2-0.06),d/2+0.02,h+0.28], 0.026,'wood',0.2);
    for(const sx of [-1,1]) beam(out,[sx*(w/2-0.06),-d/2-0.02,h],[sx*(w/2-0.06),-d/2-0.02,h+0.15], 0.026,'wood',0.15);
    dY(out, -d/2, -1, -w*0.24, w*0.24, h*0.4, h*0.6, 'paper', 0.5, 0.06);
    if(s.loaded){ heap(out, 0,0, h*0.5, w*0.7,d*0.6, 0.22, 'salt', 3737, 9, gravelTex(), 0.5);
      prismT(out, w*0.3,-d*0.2, 0.06, h*0.5, h+0.24, 6, 'wood', 0.3); }
    if(s.variant===1) heap(out, -w*0.5-0.2,-d*0.2, 0, 0.4,0.3, 0.09, 'salt', 3738, 5, gravelTex(), 0.4);
  }
  function pBootRack(out,s){
    const w = 0.86 + (s.len||0)*0.5;
    box(out, -w/2,w/2, -0.16,0.16, 0.04,0.1, 'wood', -0.1, null, true);
    for(const sx of [-1,1]) beam(out,[sx*(w/2-0.05),0,0.06],[sx*(w/2-0.05),0,0.52], 0.032,'wood',0.15);
    box(out, -w/2,w/2, -0.03,0.03, 0.46,0.52, 'wood', 0.3, grainTex(0.2));
    const n = 2 + Math.round((s.len||0)*2), rnd=mulberry32(4747);
    for(let i=0;i<n;i++){ const x=-w/2+0.16+ i*((w-0.32)/Math.max(1,n-1)), lean=(rnd()-0.5)*0.06;
      for(const sy of [-1,1]){ const y=sy*0.075;
        tube(out, [x+lean,y,0.44],[x,y,0.12], 0.062, 8, 'rubber', 0.1+sy*0.05);
        out.push(F([[x-0.06,y-0.06,0.12],[x+0.06,y-0.06,0.12],[x+0.06,y-0.2,0.06],[x-0.06,y-0.2,0.06]],'rubber',0.3));
        tube(out, [x,y,0.44],[x,y,0.46], 0.066, 8, 'rubber', 0.35, null, false); } }
    if(s.loaded){ for(const sx of [-1,1]) tube(out,[sx*(w/2-0.05),0.0,0.5],[sx*(w/2-0.05)+sx*0.02,-0.14,0.28], 0.05, 8,'canvas',0.2); }
  }
  function pBicycle(out,s){
    const R=0.33, wb=1.02, lean=0.1;
    const yc=lean;
    torusA(out, [-wb/2,yc,R], R, 0.028, 14, 5, 'rubber', 0.05, EX, EZ, NEY);
    torusA(out, [ wb/2,yc,R], R, 0.028, 14, 5, 'rubber', 0.05, EX, EZ, NEY);
    for(const sx of [-1,1]) for(let i=0;i<6;i++){ const a=i/6*Math.PI;
      beam(out,[sx*wb/2+Math.cos(a)*R*0.94, yc, R+Math.sin(a)*R*0.94],[sx*wb/2-Math.cos(a)*R*0.94, yc, R-Math.sin(a)*R*0.94], 0.006,'galv',0.3,3); }
    for(const sx of [-1,1]) tube(out,[sx*wb/2,yc-0.05,R],[sx*wb/2,yc+0.05,R], 0.03, 8,'galv',0.3);
    const bb=[-0.02,yc,0.28], seat=[-0.3,yc,0.86], bar=[0.22,yc,0.84];
    beam(out, bb, seat, 0.022,'body',0.2); beam(out, bb, bar, 0.022,'body',0.15);
    beam(out, seat, bar, 0.02,'body',0.25);
    beam(out, [-wb/2,yc,R], bb, 0.02,'body',0.1); beam(out, [-wb/2,yc,R], seat, 0.02,'body',0.05);
    beam(out, bar, [wb/2,yc,R], 0.022,'body',0.2);
    beam(out, [0.24,yc,0.9],[wb/2,yc,R+0.04], 0.02,'body',0.15);
    tube(out,[0.24,yc-0.2,0.94],[0.24,yc+0.2,0.94], 0.022, 8,'galv',0.35);        // bars
    for(const sy of [-1,1]) tube(out,[0.24,yc+sy*0.2,0.94],[0.28,yc+sy*0.22,0.9], 0.026, 6,'rubber',0.3);
    out.push(F([[-0.36,yc-0.06,0.88],[-0.24,yc-0.06,0.9],[-0.24,yc+0.06,0.9],[-0.36,yc+0.06,0.88]],'rubber',0.45));
    torusA(out, [-0.02,yc-0.06,0.28], 0.11, 0.014, 12, 4, 'galv', 0.3, EX, EZ, NEY);  // chainring
    for(const sy of [-1,1]) tube(out,[-0.02,yc+sy*0.07,0.28],[-0.02+sy*0.14,yc+sy*0.09,0.28+sy*0.1], 0.014, 6,'galv',0.3);
    if(s.loaded){ box(out, 0.14,0.38, yc-0.12,yc+0.12, 0.62,0.78, 'wood', 0.1, slatTex(0.08));
      heap(out, 0.26,yc, 0.72, 0.2,0.18, 0.12, 'fish', 5253, 4, scaleTex(), 0.2); }
    if(s.variant===1){ beam(out,[wb/2+0.06,yc,R+0.3],[wb/2+0.06,yc,0.06], 0.02,'galv',0.2); }  // kickstand-lean post
  }
  function pDogBowl(out,s){
    const n = s.variant===1?2:1, spots = n===1?[[0,0]]:[[-0.16,0.02],[0.16,-0.02]];
    spots.forEach((p,i)=>{ const r=0.13, h=0.08;
      tube(out, [p[0],p[1],0],[p[0],p[1],h], r, 12, i?'galv':'body', i*0.06, null);
      const q=[]; for(let j=0;j<12;j++){ const t=(j/12)*Math.PI*2; q.push([p[0]+Math.cos(t)*r*0.86, p[1]-Math.sin(t)*r*0.86]); }
      slab(out, q, h*0.36, i?'galv':'body', -1.0);
      tube(out, [p[0],p[1],h-0.02],[p[0],p[1],h], r*1.06, 12, i?'galv':'body', 0.4, null, false);
      if(s.loaded && i===0) slab(out, q, h*0.7, 'glass', 0.5); });
    if(s.loaded){ heap(out, 0.24,0.2, 0, 0.18,0.14, 0.06, 'net', 8585, 4, netTex(), 0.1); }  // chewed rope end
  }
  function pTyrePile(out,s){
    const n = 3 + Math.round((s.len||0)*4), rnd=mulberry32(3939);
    if(s.variant===1){ // leaning against a stack, a couple standing
      for(let i=0;i<n;i++){ const z=0.1+i*0.15, R=0.31;
        torusZ(out, (rnd()-0.5)*0.06,(rnd()-0.5)*0.06, z, R, 0.1, 14, 6, 'rubber', 0.05+i*0.05); }
      torusA(out, [0.5,0.1,0.34], 0.32, 0.1, 14, 6, 'rubber', 0.1, EX, EZ, NEY);
    } else {
      for(let i=0;i<n;i++){ const z=0.1+i*0.15;
        torusZ(out, (rnd()-0.5)*0.07,(rnd()-0.5)*0.07, z, 0.31, 0.1, 14, 6, 'rubber', 0.05+i*0.05); }
    }
    if(s.loaded) heap(out, 0,0, 0.1+n*0.15, 0.34,0.34, 0.14, 'net', 3940, 5, netTex(), 0.1);
  }
  function pJerryCans(out,s){
    const n = 2 + Math.round((s.len||0)*2), rnd=mulberry32(5555);
    for(let i=0;i<n;i++){ const x=-((n-1)*0.19)/2 + i*0.19, y=(rnd()-0.5)*0.12, h=0.42, w=0.16, d=0.32;
      box(out, x-w/2,x+w/2, y-d/2,y+d/2, 0,h, i%2?'body':'galv', i*0.05, null, true);
      for(const sx of [-1,1]) dX(out, x+sx*w/2, sx, y-d/2+0.04, y+d/2-0.04, 0.06, h-0.06, i%2?'body':'galv', -0.9, 0.04);
      box(out, x-w/2+0.02,x+w/2-0.02, y-0.05,y+0.05, h,h+0.05, i%2?'body':'galv', 0.4);
      for(const sy of [-1,1]) tube(out,[x,y+sy*0.05,h+0.05],[x,y+sy*0.11,h+0.02], 0.018, 6,'iron',0.35);
      prismT(out, x, y-d*0.3, 0.035, h, h+0.05, 8, 'iron', 0.45); }
    if(s.loaded){ const x=((n-1)*0.19)/2+0.28;
      barrelAt(out, x, 0.06, 0, 0.13, 0.36, 'galv', 0.1, 'iron');
      tube(out,[x,0.06,0.36],[x+0.1,-0.06,0.42], 0.02, 6,'iron',0.3); }
  }
  function pWoodStack(out,s){
    const w = 1.5 + (s.len||0)*1.2, d=0.5, rows = 4 + Math.round((s.len||0)*3);
    const rnd=mulberry32(6969);
    for(const sx of [-1,1]) beam(out,[sx*(w/2+0.05),-d/2+0.04,0],[sx*(w/2+0.05),-d/2+0.04,rows*0.13+0.1], 0.04,'pole',0.1);
    for(const sx of [-1,1]) beam(out,[sx*(w/2+0.05),d/2-0.04,0],[sx*(w/2+0.05),d/2-0.04,rows*0.13+0.1], 0.04,'pole',0.05);
    for(let r=0;r<rows;r++){ const z=0.06+r*0.13, cross=(r%2===1);
      const n = cross? Math.max(3, Math.round(w/0.16)) : Math.max(3, Math.round(d/0.15));
      for(let i=0;i<n;i++){
        const rr=0.055+rnd()*0.016, jitter=(rnd()-0.5)*0.02;
        if(cross){ const x=-w/2+0.08+ i*((w-0.16)/(n-1));
          tube(out, [x+jitter,-d/2+0.06,z],[x+jitter,d/2-0.06,z], rr, 7, 'wood', -0.1+rnd()*0.5, grainTex(0.2)); }
        else { const y=-d/2+0.08+ i*((d-0.16)/(n-1));
          tube(out, [-w/2+0.04,y+jitter,z],[w/2-0.04,y+jitter,z], rr, 7, 'wood', -0.1+rnd()*0.5, grainTex(0.6)); } } }
    if(s.tarp) tarpOver(out, -w/2-0.08,w/2+0.08, -d/2-0.08,d/2+0.08, rows*0.13+0.16, 0.34, 0.05);
  }

  // ================= PROP TABLE =================
  // foot:[w,d] metres at len 0 · k: extra footprint scale per unit len · h(s): metres · run: HEIGHT/RUN axis
  // mounts(s): { deck, rail, wall, post, hook } attach points in metres
  const T=true;
  const PROPS = {
    trapStack:   { label:'Trap stack',   cat:'gear', foot:[0.98,0.62], run:T, k:0.06, variants:['wire','wood'], def:{}, build:pTrapStack,
                   h:(s)=>(2+Math.round(s.len*4))*((s.variant===1?0.42:0.34)+0.02),
                   mounts:(s)=>({ deck:[[0,0,0]] }) },
    trapSingle:  { label:'Single trap',  cat:'gear', foot:[1.0,0.64],  variants:['wire','wood'], def:{}, build:pTrapSingle,
                   h:(s)=>(s.variant===1?0.44:0.36) },
    buoyRack:    { label:'Buoy rack',    cat:'gear', foot:[1.7,0.78],  run:T, k:0.82, variants:['A-frame'], def:{loaded:T}, build:pBuoyRack, h:()=>1.62 },
    buoyWall:    { label:'Buoy wall',    cat:'gear', foot:[1.9,0.24],  run:T, k:0.68, variants:['two rails'], def:{loaded:T}, build:pBuoyWall, h:()=>1.95,
                   mounts:(s)=>({ deck:[], wall:[[-(1.7+s.len*1.3)/2,-0.16,0.05],[(1.7+s.len*1.3)/2,-0.16,1.95]] }) },
    netReel:     { label:'Net reel',     cat:'gear', foot:[1.5,1.3],   run:T, k:0.4, variants:['timber drum'], def:{loaded:T}, build:pNetReel, h:()=>1.52 },
    netPile:     { label:'Net pile',     cat:'gear', foot:[1.6,1.1],   run:T, k:0.6, variants:['heaped'], def:{}, build:pNetPile, h:()=>0.55 },
    ropeCoil:    { label:'Rope coils',   cat:'gear', foot:[0.8,0.8],   run:T, k:0.4, variants:['single','three coils'], def:{}, build:pRopeCoil, h:()=>0.1 },
    ropeHanks:   { label:'Rope hanks',   cat:'gear', foot:[1.4,0.2],   run:T, k:0.55, variants:['pegged board'], def:{}, build:pRopeHanks, h:()=>1.85,
                   mounts:(s)=>({ deck:[], wall:[[-(1.2+s.len*0.8)/2,-0.14,0.35],[(1.2+s.len*0.8)/2,-0.14,1.85]] }) },
    floatBags:   { label:'Trawl floats', cat:'gear', foot:[1.2,0.8],   run:T, k:0.5, variants:['loose pile'], def:{}, build:pFloatBags, h:()=>0.52 },
    trawlDoor:   { label:'Trawl door',   cat:'gear', foot:[1.6,0.5],   run:T, k:0.4, variants:['otter board'], def:{}, build:pTrawlDoor, h:(s)=>0.9+s.len*0.3 },

    toteStack:   { label:'Tote stack',   cat:'handling', foot:[0.78,0.56], run:T, k:0.05, variants:['stacked'], def:{paint:'blue'}, build:pToteStack,
                   h:(s)=>{ const n=2+Math.round(s.len*4); return (n-1)*0.295+0.34; },
                   mounts:(s)=>({ deck:[[0,0,0]], hook:[[0,0,(2+Math.round(s.len*4)-1)*0.295+0.34]] }) },
    toteSingle:  { label:'Fish tote',    cat:'handling', foot:[0.78,0.56], variants:['fish','iced'], def:{paint:'blue'}, build:pToteSingle, h:()=>0.36,
                   mounts:()=>({ deck:[[0,0,0]], hook:[[0,0,0.36]] }) },
    crateStack:  { label:'Crate stack',  cat:'handling', foot:[0.72,0.68], run:T, k:0.06, variants:['slat crate'], def:{}, build:pCrateStack,
                   h:(s)=>(1+Math.round(s.len*3))*0.432 },
    pallet:      { label:'Pallets',      cat:'handling', foot:[1.16,0.9],  run:T, k:0.04, variants:['stacked'], def:{paint:'greyShingle'}, build:pPallet,
                   h:(s)=>(1+Math.round(s.len*5))*0.115 },
    saltBin:     { label:'Salt bin',     cat:'handling', foot:[1.4,0.9],   run:T, k:0.55, variants:['lid shut','lid propped'], def:{}, build:pSaltBin,
                   h:(s)=>0.78+(s.variant===1?0.28:0.16) },
    iceChest:    { label:'Ice chest',    cat:'handling', foot:[1.25,0.8],  run:T, k:0.5, variants:['shut','open'], def:{paint:'white'}, build:pIceChest,
                   h:(s)=>s.variant===1?1.06:0.735, mounts:()=>({ deck:[[0,0,0]] }) },
    weighScale:  { label:'Weigh scale',  cat:'handling', foot:[1.2,0.8],   run:T, k:0.2, variants:['dial head'], def:{paint:'red'}, build:pWeighScale,
                   h:(s)=>1.93+s.len*0.5, mounts:(s)=>({ deck:[[-0.5,0,0],[0.5,0,0]], hook:[[0,0,1.06]] }) },
    guttingTable:{ label:'Gutting table',cat:'handling', foot:[1.8,0.9],   run:T, k:0.62, variants:['hosed'], def:{}, build:pGuttingTable, h:()=>1.04 },
    baitBarrel:  { label:'Bait barrel',  cat:'handling', foot:[0.68,0.68], variants:['single','pair'], def:{}, build:pBaitBarrel, h:()=>0.86,
                   k:0, mounts:()=>({ deck:[[0,0,0]], hook:[[0,0,0.86]] }) },
    bushelBaskets:{label:'Baskets',      cat:'handling', foot:[0.9,0.72],  run:T, k:0.5, variants:['bushel'], def:{}, build:pBushelBaskets, h:()=>0.67 },
    sortingTrough:{label:'Sorting trough',cat:'handling',foot:[2.1,0.7],   run:T, k:0.66, variants:['graded','with tote'], def:{}, build:pSortingTrough, h:()=>0.84 },

    davitHoist:  { label:'Davit hoist',  cat:'lifting', foot:[0.6,1.6],  run:T, k:0.4, variants:['swing arm'], def:{paint:'sage'}, build:pDavitHoist,
                   h:(s)=>2.52+s.len*1.2,
                   mounts:(s)=>({ deck:[[0,0,0]], hook:[[0,-(1.2+s.len*0.6), s.loaded?0.74:2.3+s.len*1.2-0.7]] }) },
    capstan:     { label:'Capstan',      cat:'lifting', foot:[0.92,0.92], run:T, k:0.1, variants:['powered','hand bar'], def:{}, build:pCapstan,
                   h:(s)=>0.71+s.len*0.2 },
    blockTackle: { label:'Block & tackle',cat:'lifting',foot:[0.5,1.3],   run:T, k:0.2, variants:['two block'], def:{}, build:pBlockTackle,
                   h:(s)=>2.4+s.len*0.9,
                   mounts:(s)=>({ deck:[[0,0,0]], hook:[[0,-0.9,(2.4+s.len*0.9)-1.12]] }) },
    wheelbarrow: { label:'Wheelbarrow',  cat:'lifting', foot:[0.95,1.35], variants:['catch','net'], def:{paint:'rustOrange'}, build:pWheelbarrow, h:()=>0.72 },
    handTruck:   { label:'Hand truck',   cat:'lifting', foot:[0.62,0.62], variants:['upright'], def:{}, build:pHandTruck, h:()=>1.35 },
    dockCart:    { label:'Dock cart',    cat:'lifting', foot:[1.2,1.5],  run:T, k:0.34, variants:['flat deck'], def:{}, build:pDockCart, h:()=>0.5 },
    gantryFrame: { label:'Gantry frame', cat:'lifting', foot:[2.6,1.1],  run:T, k:0.4, variants:['A-frame'], def:{}, build:pGantryFrame,
                   h:(s)=>2.64+s.len*1.1,
                   mounts:(s)=>({ deck:[[-(1.15+s.len*0.5),0,0],[(1.15+s.len*0.5),0,0]], hook:[[0,0,s.loaded?0.9:(2.6+s.len*1.1)-1.1]] }) },
    potHauler:   { label:'Pot hauler',   cat:'lifting', foot:[0.66,0.9],  run:T, k:0.15, variants:['sheave head'], def:{paint:'gold'}, build:pPotHauler,
                   h:(s)=>0.98+s.len*0.3,
                   mounts:(s)=>({ deck:[[0,0,0]], rail:[[0,0,0.05]], hook:[[0,-0.34,0.98+s.len*0.3-0.16]] }) },

    ringStation: { label:'Ring station', cat:'safety', foot:[0.98,0.3],  variants:['board & line'], def:{paint:'red'}, build:pRingStation, h:()=>1.72,
                   mounts:()=>({ deck:[[-0.4,-0.1,0],[0.4,-0.1,0]], wall:[[-0.46,-0.14,0.62],[0.46,-0.14,1.62]], rail:[[0,-0.1,0.98]] }) },
    ringPost:    { label:'Ring post',    cat:'safety', foot:[0.4,0.4],   run:T, k:0.1, variants:['post & coil'], def:{paint:'red'}, build:pRingPost,
                   h:(s)=>1.09+s.len*0.3, mounts:(s)=>({ deck:[[0,0,0]], rail:[[0,0,0.07]], post:[[0,0,1.05+s.len*0.3]] }) },
    fireCabinet: { label:'Fire cabinet', cat:'safety', foot:[0.7,0.26],  variants:['glass front','with sand'], def:{paint:'red'}, build:pFireCabinet, h:()=>1.75,
                   mounts:()=>({ deck:[], wall:[[-0.34,-0.14,0.55],[0.34,-0.14,1.75]] }) },
    noticeBoard: { label:'Notice board', cat:'safety', foot:[1.2,0.36],  run:T, k:0.6, variants:['open','headed'], def:{}, build:pNoticeBoard, h:()=>1.88 },
    chalkSign:   { label:'Chalk board',  cat:'safety', foot:[0.7,0.56],  variants:['A-frame'], def:{}, build:pChalkSign, h:()=>0.94 },
    tideStaff:   { label:'Tide staff',   cat:'safety', foot:[0.34,0.3],  run:T, k:0.1, variants:['board','with float tube'], def:{paint:'white'}, build:pTideStaff,
                   h:(s)=>2.56+s.len*1.6, mounts:(s)=>({ deck:[[0,0,0]], post:[[0,-0.06,(2.4+s.len*1.6)*0.3]] }) },
    harbourSign: { label:'Harbour sign', cat:'safety', foot:[2.1,0.24],  run:T, k:0.6, variants:['plain','headed'], def:{paint:'white'}, build:pHarbourSign,
                   h:(s)=>1.97+s.len*0.3 },
    rescueLadder:{ label:'Rescue ladder',cat:'safety', foot:[0.6,0.5],   run:T, k:0.1, variants:['rail hung'], def:{paint:'gold'}, build:pRescueLadder, h:()=>1.06,
                   mounts:(s)=>({ deck:[[0,-0.06,0]], rail:[[0,-0.13,1.03]] }) },

    codFlake:    { label:'Cod flake',    cat:'drying', foot:[2.1,1.45], run:T, k:0.7, variants:['drying'], def:{loaded:T}, build:pCodFlake, h:()=>0.97 },
    fishRack:    { label:'Fish rack',    cat:'drying', foot:[1.7,0.8],  run:T, k:0.6, variants:['two rails'], def:{loaded:T}, build:pFishRack, h:(s)=>2.0+s.len*0.4 },
    netFrame:    { label:'Net frame',    cat:'drying', foot:[2.2,1.0],  run:T, k:0.6, variants:['hung net'], def:{loaded:T}, build:pNetFrame, h:(s)=>1.9+s.len*0.5 },
    oilskinLine: { label:'Oilskin line', cat:'drying', foot:[2.0,0.5],  run:T, k:0.66, variants:['gear line'], def:{}, build:pOilskinLine, h:()=>1.86 },
    herringSticks:{label:'Herring sticks',cat:'drying',foot:[1.6,0.44], run:T, k:0.6, variants:['three rails'], def:{}, build:pHerringSticks, h:()=>1.65 },

    bench:       { label:'Bench',        cat:'decor', foot:[1.6,0.46],  run:T, k:0.55, variants:['plank back'], def:{}, build:pBench, h:()=>0.97 },
    picnicTable: { label:'Picnic table', cat:'decor', foot:[2.0,1.9],   run:T, k:0.45, variants:['trestle'], def:{}, build:pPicnicTable, h:()=>0.74 },
    deckChair:   { label:'Deck chair',   cat:'decor', foot:[0.68,0.78], variants:['slat back'], def:{}, build:pDeckChair, h:()=>1.08 },
    drumPlanter: { label:'Drum planter', cat:'decor', foot:[0.64,0.64], variants:['single','pair'], def:{paint:'sage',loaded:T}, build:pDrumPlanter, h:()=>0.72 },
    planterBox:  { label:'Planter box',  cat:'decor', foot:[1.2,0.52],  run:T, k:0.6, variants:['plank'], def:{loaded:T}, build:pPlanterBox, h:()=>0.6 },
    flagpole:    { label:'Flagpole',     cat:'decor', foot:[0.6,0.6],   run:T, k:0.1, variants:['halyard'], def:{loaded:T}, build:pFlagpole, h:(s)=>5.49+s.len*3.2,
                   mounts:(s)=>({ deck:[[0,0,0]], post:[[0.1,0,5.4+s.len*3.2-0.24]] }) },
    bunting:     { label:'Bunting line', cat:'decor', foot:[2.8,0.3],   run:T, k:0.7, variants:['pennants'], def:{}, build:pBunting, h:(s)=>2.25+s.len*0.4,
                   mounts:(s)=>({ deck:[[-(2.6+s.len*1.8)/2,0,0],[(2.6+s.len*1.8)/2,0,0]],
                                  post:[[-(2.6+s.len*1.8)/2,0,2.2+s.len*0.4-0.1],[(2.6+s.len*1.8)/2,0,2.2+s.len*0.4-0.1]] }) },
    lanternPost: { label:'Lantern post', cat:'decor', foot:[0.44,0.7],  run:T, k:0.1, variants:['hurricane'], def:{}, build:pLanternPost, h:(s)=>2.06+s.len*1.0,
                   mounts:(s)=>({ deck:[[0,0,0]], post:[[0,-0.31,(1.9+s.len*1.0)-0.34]] }) },
    anchorProp:  { label:'Old anchor',   cat:'decor', foot:[0.95,0.75], run:T, k:0.3, variants:['leaning','on a skid'], def:{}, build:pAnchorProp, h:(s)=>1.59+s.len*0.7 },
    ropeFence:   { label:'Rope fence',   cat:'decor', foot:[2.6,0.24],  run:T, k:0.75, variants:['one rail','two rail'], def:{}, build:pRopeFence, h:()=>0.97,
                   mounts:(s)=>({ deck:[[-(2.4+s.len*1.8)/2,0,0],[0,0,0],[(2.4+s.len*1.8)/2,0,0]] }) },

    trashBarrel: { label:'Trash barrel', cat:'odds', foot:[0.66,0.66], variants:['open','lidded'], def:{}, build:pTrashBarrel, h:(s)=>s.variant===1?0.96:0.86 },
    oilDrum:     { label:'Oil drum',     cat:'odds', foot:[0.62,0.62], variants:['single','pair','stacked three'], def:{paint:'rustOrange'}, build:pOilDrum,
                   h:(s)=>s.variant===2?1.44:0.91,
                   k:(s)=>1, mounts:()=>({ deck:[[0,0,0]] }) },
    saltBox:     { label:'Sand box',     cat:'odds', foot:[1.1,0.74],  run:T, k:0.55, variants:['closed','spilled'], def:{paint:'gold'}, build:pSaltBox, h:()=>0.88 },
    bootRack:    { label:'Boot rack',    cat:'odds', foot:[0.95,0.36], run:T, k:0.45, variants:['pegged'], def:{}, build:pBootRack, h:()=>0.52 },
    bicycle:     { label:'Bicycle',      cat:'odds', foot:[1.4,0.55],  variants:['leaning','propped'], def:{paint:'teal'}, build:pBicycle, h:()=>0.98 },
    dogBowl:     { label:'Dog bowls',    cat:'odds', foot:[0.34,0.3],  variants:['single','pair'], def:{paint:'red'}, build:pDogBowl, h:()=>0.09 },
    tyrePile:    { label:'Tyre pile',    cat:'odds', foot:[0.84,0.84], run:T, k:0.4, variants:['stacked','with leaner'], def:{}, build:pTyrePile,
                   h:(s)=>0.2+(3+Math.round(s.len*4))*0.15 },
    jerryCans:   { label:'Jerry cans',   cat:'odds', foot:[0.72,0.44], run:T, k:0.5, variants:['row'], def:{paint:'red'}, build:pJerryCans, h:()=>0.47 },
    woodStack:   { label:'Wood stack',   cat:'odds', foot:[1.7,0.6],   run:T, k:0.7, variants:['cross-piled'], def:{}, build:pWoodStack,
                   h:(s)=>(4+Math.round(s.len*3))*0.13+0.1 },
  };

  function resolve(name, opts){ opts=opts||{}; const P=PROPS[name]||PROPS.trapStack;
    const g=(k,d)=> opts[k]!=null?opts[k] : (P.def[k]!=null?P.def[k]:d);
    return { name, paint: opts.paint!==undefined?opts.paint:(P.def.paint||null), metal:g('metal','galv'),
      variant: opts.variant!=null?opts.variant:0, len: opts.len!=null?opts.len:(P.run?0.4:0),
      loaded: g('loaded',false)?true:false, tarp: g('tarp',false)?true:false,
      weather: opts.weather!=null?opts.weather:0.45, night:!!opts.night }; }

  function makeMats(s){ const wx=s.weather, night=s.night;
    const grime=r=>r.map(c=>mix(desat(c,wx*0.24),'#3a3128',wx*0.14));
    const rust =r=>r.map(c=>mix(c,'#6d3417',wx*0.28));
    const silver=r=>r.map(c=>mix(desat(c,wx*0.4),'#8d8a80',wx*0.16));
    const cool =r=>night?r.map(c=>mix(desat(c,0.28),'#1b2740',0.38)):r;
    const t=r=>cool(grime(r)), tm=r=>cool(rust(grime(r))), tw=r=>cool(silver(grime(r)));
    const paint = s.paint?(BODY[s.paint]||BODY.sage):ALUM;
    const metal = s.metal==='alum'?ALUM : s.metal==='iron'?IRON : GALV;
    const M = { body:{ramp:t(paint)}, wood:{ramp:tw(WOOD)}, plank:{ramp:tw(PLANK)}, pole:{ramp:t(POLE)},
      iron:{ramp:tm(IRON)}, galv:{ramp:tm(metal)}, alum:{ramp:t(ALUM)}, concrete:{ramp:t(CONCRETE)},
      copper:{ramp:tm(COPPER)}, brass:{ramp:tm(BRASS)}, rope:{ramp:tw(ROPE)}, poly:{ramp:t(POLYR)},
      net:{ramp:t(NET)}, canvas:{ramp:tw(CANVAS)}, fish:{ramp:t(FISH)}, ice:{ramp:cool(ICE)},
      rubber:{ramp:t(RUBBER)}, foam:{ramp:tw(FOAM)}, slate:{ramp:t(SLATE)}, paper:{ramp:tw(PAPER)},
      gravel:{ramp:t(GRAVEL)}, asphalt:{ramp:t(ASPHALT)}, salt:{ramp:t(SALT)},
      glass:{ramp: night?GLASSN:GLASSD}, glow:{ramp: night?GLOW:['#5f6a5e','#8d9a8b','#b6c2b0','#d3ddcb']} };
    Object.keys(BODY).forEach(k=>{ M[k]={ramp:t(BODY[k])}; });
    BUOYSET.forEach((k,i)=>{ M['buoy'+(i+1)]={ramp:t(BODY[k])}; });
    return M;
  }

  // ---- rasteriser (identical recipe; single object → always keyline) ----
  function paintFaces(faces, B, MATS){ const N=W*H;
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
    if(s.weather>0.02){ const rnd=mulberry32(3181|((s.variant*97)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='pole'||m==='wood'||m==='plank'||m==='body'||m==='concrete'||m==='galv'||m==='canvas'||m==='rope') && rnd()<s.weather*0.06)
          out[i]=rbuf[i][Math.max(0,ibuf[i]-1)]; } }
    if(s.night){ for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x; if(nbuf[i]!=='glow') continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,1]]){ const j=(y+dy)*W+(x+dx); if(out[j]&&nbuf[j]!=='glow') out[j]=mix(out[j],'#f2c25e',0.3); } } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
      if(touch) out[i]=KEY; }
    return out;
  }
  function toRGBA(cols){ const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; } const [r,g,bl]=hex2rgb(c);
      rgba[i*4]=r;rgba[i*4+1]=g;rgba[i*4+2]=bl;rgba[i*4+3]=255; }
    return rgba;
  }

  function render(name, dir, opts){ opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const s=resolve(name,opts), B=camBasis({dir,elev:opts.elev}), MATS=makeMats(s), out=[];
    (PROPS[name]||PROPS.trapStack).build(out, s);
    return toRGBA(post(paintFaces(out,B,MATS), s));
  }
  function footprint(name, opts){ const s=resolve(name,opts), P=PROPS[name]||PROPS.trapStack;
    const k = P.run ? (1 + s.len*(typeof P.k==='function'?P.k(s):(P.k!=null?P.k:0.2))) : 1;
    return { w:P.foot[0]*k, d:P.foot[1]*k }; }
  function height(name, opts){ const s=resolve(name,opts), P=PROPS[name]||PROPS.trapStack;
    return P.h?P.h(s):1; }
  function mounts(name, opts){ const s=resolve(name,opts), P=PROPS[name]||PROPS.trapStack;
    const M=P.mounts?P.mounts(s):null, f=footprint(name,opts);
    const deck = M&&M.deck ? M.deck : [[0,0,0]];
    return { deck, rail:(M&&M.rail)||[], wall:(M&&M.wall)||[], post:(M&&M.post)||[], hook:(M&&M.hook)||[], foot:f }; }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }
  function anchors(name, dir, opts){ opts=opts||{}; const M=mounts(name,opts), e=opts.elev;
    const P=(a)=>a.map(p=>{ const q=project(dir,p,e); return { x:q.x, y:q.y, m:p }; });
    return { deck:P(M.deck), rail:P(M.rail), wall:P(M.wall), post:P(M.post), hook:P(M.hook),
      foot:M.foot, height:height(name,opts) }; }
  function list(){ return Object.keys(PROPS); }

  root.WharfDecor = { W, H, PX, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    WOOD, PLANK, POLE, IRON, GALV, ALUM, CONCRETE, COPPER, BRASS, ROPE, POLYR, NET, CANVAS, FISH, ICE,
    RUBBER, FOAM, SLATE, PAPER, GRAVEL, ASPHALT, SALT, GLASSD, GLOW, BODY, BUOYSET, KEY,
    PROPS, CATS, list, footprint, height, mounts, anchors, render, project };
})(typeof globalThis!=='undefined'?globalThis:window);
