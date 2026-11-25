/* St Peters landing fittings, v1. New immutable source; metres, PPU 32, 40 degree camera.
   The established dock-props primitives, palette, dither and depth law are retained.
   Tyres: top hanger pivot; .60 m outside diameter and .20 m authored axial section.
   Coil: 25 mm line, three layers and its loose tail. No baked water or scene placement. */
(function(root){
  'use strict';
  const PX = 32, S = 32;
  const W = 420, H = 760, cx = 210, groundY = 160;
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
  // ADR 0031 — the outline is retired from world art; the silhouette is carried by the form's own
  // dark side. The ring is not deleted: it is gated OFF by default and reachable with
  // `{outline:true}`, mirroring the engine's own `GameConfig.HullKeylineFlood` so the owner keeps a
  // one-flag A/B. Depth-edge darkening (the EDGE pass in post()) is the separate INTERIOR rule
  // ADR 0031 §2 keeps untouched — do not confuse the two.
  const KEYLINE_DEFAULT = false;
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
    M.wet={ramp:['#152326','#203237','#2f4448','#435b5e','#5c7474','#7c9290']};
    M.weed={ramp:['#1d281b','#293822','#37482a','#475a32','#596d3b','#6e8047']};
    M.rubberWet={ramp:cool(RUBBER.map(c=>mix(c,'#10262a',.32)))};
    M.rubberWeed={ramp:cool(M.weed.ramp.map(c=>mix(c,'#172018',.2)))};
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
      if(n===0 && s.name!=='ropeCoil'){ out[i]=null; rbuf[i]=null; } }
    // KEYLINE — RETIRED BY DEFAULT (ADR 0031). Pure ring deletion: every pixel this pass writes is
    // an EMPTY neighbour of the silhouette, so switching it off changes no painted pixel of any
    // prop. What holds the edge instead is the prop's own dark side.
    if(s.outline === undefined ? KEYLINE_DEFAULT : s.outline !== false){
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let touch=false;
        for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
        if(touch) out[i]=KEY; }
    }
    return out;
  }
  function toRGBA(cols){ const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; } const [r,g,bl]=hex2rgb(c);
      rgba[i*4]=r;rgba[i*4+1]=g;rgba[i*4+2]=bl;rgba[i*4+3]=255; }
    return rgba;
  }



  const DEFAULTS={top:5.354004,lowWater:-2.2,highWater:2.2,diameter:.60,sectionDepth:.20,
    lineDiameter:.025,layers:3,variant:0,len:.4,weather:.35,outline:false,metal:'galv',night:false};
  function spec(name,o){
    if(name!=='tyre'&&name!=='ropeCoil')throw new Error('Unknown landing fitting '+name);
    const s=Object.assign({name},DEFAULTS,o||{});
    for(const k of ['top','lowWater','highWater','diameter','sectionDepth','lineDiameter','layers','len'])
      if(!Number.isFinite(s[k]))throw new Error('Non-finite '+k);
    if(s.diameter!==.60||s.sectionDepth!==.20)throw new Error('Tyre body must be .60 diameter and .20 deep');
    if(s.lineDiameter<.020||s.lineDiameter>.030||s.layers!==3||s.variant!==0)
      throw new Error('One three-layer coil of 20-30 mm line is required');
    if(s.lowWater>s.highWater)throw new Error('Reversed water bounds');
    return s;
  }
  function clipZ(poly,z,above){
    const out=[];
    for(let i=0;i<poly.length;i++){
      const a=poly[i],b=poly[(i+1)%poly.length],aa=above?a[2]>=z:a[2]<=z,bb=above?b[2]>=z:b[2]<=z;
      if(aa)out.push(a);
      if(aa!==bb){const t=(z-a[2])/(b[2]-a[2]);out.push(a.map((v,j)=>v+(b[j]-v)*t));}
    }return out;
  }
  function tyreFace(out,vertices,s,brightness,texture){
    const wet=s.highWater-s.top,weed=Math.min(s.highWater,s.lowWater+.70)-s.top;
    for(const band of [{low:-Infinity,high:weed,mat:'rubberWeed'},{low:weed,high:wet,mat:'rubberWet'},{low:wet,high:Infinity,mat:'rubber'}]){
      let v=vertices;
      if(Number.isFinite(band.low))v=clipZ(v,band.low,true);
      if(v.length&&Number.isFinite(band.high))v=clipZ(v,band.high,false);
      if(v.length>=3)out.push(F(v,band.mat,brightness,0,v.map(p=>[Math.atan2(p[2]+.32,p[0])*.23,p[1]]),texture));
    }
  }
  function pTyre(out,s){
    // Elliptical section: radial .07, axial .10; outside .60, opening .32, authored depth .20.
    const major=.23,radial=.07,axial=s.sectionDepth/2,z0=-.32,around=32,section=12;
    const at=(a,b)=>[(major+radial*Math.cos(b))*Math.cos(a),axial+axial*Math.sin(b),z0+(major+radial*Math.cos(b))*Math.sin(a)];
    const tread=(u,v)=>{
      const phase=((u+Math.abs(v-axial)*.6)%.06+.06)%.06;
      return phase<.012?-1.4:phase>.048?.25:0;
    };
    for(let i=0;i<around;i++)for(let j=0;j<section;j++){
      const a=i*Math.PI*2/around,b=j*Math.PI*2/section,da=Math.PI*2/around,db=Math.PI*2/section;
      const v=[at(a,b),at(a,b+db),at(a+da,b+db),at(a+da,b)];
      tyreFace(out,v,s,.12+(j%3===0?.18:0),Math.cos(b)>.5?tread:null);
    }
    // Two sidewall beads, facet highlights and a short real line suspension. No raised origin.
    for(const y of [.002,.198])for(const R of [.174,.205,.26]){
      const q=[];torusA(q,[0,y,z0],R,.004,32,6,'rubber',.28,EX,EZ,NEY);
      for(const face of q)tyreFace(out,face.v,s,face.b,null);
    }
    const rope=s.lineDiameter/2;
    for(const x of [-.10,.10])ropeRun(out,[[x,0,-rope],[x,.06,-.032],[x,.12,-.10]],rope,'rope',.1,8);
  }
  function coilTail(s){
    const r=s.lineDiameter/2,R=.34+s.len*.16,centre=(s.layers-1)*s.lineDiameter+r*Math.sqrt(3)/2;
    return catPts([R,0,centre],[R+.32,0,r],s.lineDiameter*.2,8).map(q=>[q[0],q[1],Math.max(r,q[2])]);
  }
  function pCoil(out,s){
    const r=s.lineDiameter/2,R=.34+s.len*.16;
    // Preserve the outer coil and loose tail; three honest layers replace the oversized tube.
    for(let layer=0;layer<s.layers;layer++)
      ropeCoilAt(out,0,0,layer*s.lineDiameter+r*(Math.sqrt(3)/2-.86),R,4,r,'rope',layer*.06);
    ropeRun(out,coilTail(s),r,'rope',.1,8);
  }
  function geometry(name,o){const s=spec(name,o),faces=[];(name==='tyre'?pTyre:pCoil)(faces,s);return{s,faces};}
  function bounds(name,o){const b={x:[Infinity,-Infinity],y:[Infinity,-Infinity],z:[Infinity,-Infinity]};
    for(const f of geometry(name,o).faces)for(const p of f.v)for(let i=0;i<3;i++){
      const a=b[['x','y','z'][i]];a[0]=Math.min(a[0],p[i]);a[1]=Math.max(a[1],p[i]);}return b;}
  function contract(name,o){const s=spec(name,o);return name==='tyre'?{
    kind:'hung tyre fender',outsideDiameter:s.diameter,openingDiameter:.32,sectionDepth:s.sectionDepth,
    hangerPivot:[0,0,0],bodyCentre:[0,.10,-.32],top:s.top,lowWater:s.lowWater,highWater:s.highWater,
    wetBelow:s.highWater,weedBelow:Math.min(s.highWater,s.lowWater+.70),bounds:bounds(name,o)
  }:{kind:'coiled line',lineDiameter:s.lineDiameter,layers:s.layers,concentricTurns:4,
    looseTail:coilTail(s),tailDiameter:s.lineDiameter,top:s.top,bounds:bounds(name,o)};}
  function frame(name,dir,o){const g=geometry(name,o),B=camBasis({dir,elev:(o||{}).elev}),buf=paintFaces(g.faces,B,makeMats(g.s));
    const localZ=new Float32Array(W*H), rgba=toRGBA(post(buf,g.s));
    // Invert projected screen-y/depth back to z for each painted pixel.
    for(let i=0;i<W*H;i++) if(rgba[i*4+3]){
      const screen=(groundY-(Math.floor(i/W)+.5))/S;
      localZ[i]=screen*B.ce-buf.dep[i]*B.se;
    }
    return {w:W,h:H,pivot:{x:cx,y:groundY},rgba,localZ};}
  function render(name,dir,o){return frame(name,dir,o).rgba;}
  function project(dir,p,elev){const q=projVert(p[0],p[1],p[2],camBasis({dir,elev}));return{x:q.sx,y:q.sy};}

  const API={VERSION:'1.0.0',W,H,PX,pivot:{x:cx,y:groundY},defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],DEFAULTS,spec,geometry,bounds,frame,render,project,contract};
  root.StPetersLandingFittings=API;
  if(typeof module!=='undefined'&&module.exports)module.exports=API;
})(typeof globalThis!=='undefined'?globalThis:this);
