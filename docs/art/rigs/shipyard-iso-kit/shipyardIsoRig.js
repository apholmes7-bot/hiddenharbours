/* Hidden Harbours — parametric ISO SHIPYARD rig (ADR-0006 bake pipeline, SAME turntable + camera +
   shading as wharfIsoRig / wharfBuildingRig / utilityIsoRig / the fleet). Two layers in one file:

     PARTS   the yard's vocabulary, each a real object in metres — davit, cradle, keel blocks,
             marine railway, winch house, travel-lift, workshop gantry, clear-span workshop, yard
             office, wet basin + gate + roof, hardstand, chain-link fence, staging, timber stack.
     SITES   named whole yards that ASSEMBLE those parts around a hull: backyardSlip · smallYard ·
             workingYard · largeYard · industrialYard. A site is sized BY THE BOAT IT SERVES, so
             the 4.9 m dory yard and the 110 m tanker yard are the same code with a different hull.

   REUSED FROM THE EXISTING RIGS, DELIBERATELY (this is a member of the same family, not a new look):
     · 32 px = 1 m, 3/4 camera, 45deg steps, elev 40deg, flat-facet shading from the fixed upper-LEFT
       key, z-buffered, ordered dither, per-face uv texture, depth-edge darkening, 1 px keyline, no AA
     · the wharf palettes (POLE / WOOD / PLANK / CONCRETE / GALV / IRON / STEEL / RUST / ROCK / ...)
     · the TIDAL FRAME and its growth bands — matAtZ / zSplit / bandedPile / bandedWall / weedFringe.
       A slipway, a railway and a basin wall all run through the tide, so they band exactly like a
       quay face does, and they hold station while the water moves.
     · the WATER CONTRACT (ADR 0010/0012/0023): ZERO water pixels are baked, even in the wet basin.
       The rig publishes `wet` — the mask of yard structure currently below the waterline — and the
       shader owns everything else. opts.clipBelowWater drops those pixels outright.
     · the fleet's hull table, extended to the two big hulls the yards are built around.

   NEW HERE
     · pxPerM is a parameter. Parts bake at the native 32 px = 1 m. A whole site is a planning bake:
       fitScale() drops to 16 / 12 / 8 px per metre so a 110 m tanker yard fits one sheet, and the
       result reports the scale it used. Detail below ~0.15 m is dropped when the scale can't hold it.
     · textures may return null for a HOLE — real see-through chain-link mesh and open truss webs,
       instead of an opaque grey panel standing in for a fence.
     · NO HULLS ARE BAKED. Every cradle, davit, blocking stack, sling and basin berth publishes a
       hullMount anchor — position, heading, keel height, support type and the hull class it takes —
       and the compositor drops the existing fleet sprites on top.

   Hulls: the yard bakes without them and publishes a mount per berth. Pass { hulls:true } and the rig
   composites the FLEET'S OWN bakes onto those mounts — same camera, same elevation, z-tested against the
   yard's depth buffer, so a shed wall or a cradle upright in front of a hull still covers it. SHIPS names
   the rig global per class; a hull whose rig is not on the page is simply skipped.

   Exposes globalThis.ShipyardIso = { PX, DIRS, defaultElev, order, PARTS, SITES, SHIPS, MATS,
     list(), sites(), resolve(), plan(), frame(), fitScale(), hullRigs(), hullDir(), render(what,dir,opts),
     gameplay(what,opts), anchors(what,dir,opts,cell), project() }. */
(function (root) {
  const PX = 32, DEG = Math.PI / 180, DEFAULT_ELEV = 40, PAD = 3, MAXDIM = 3000;

  // ---- palettes, dark -> light (shared harbour-master ramps) ----
  const WOOD     = ['#553d23','#6b4e30','#7f603b','#957749','#ab8d5f','#c1a578'];
  const PLANK    = ['#454037','#565045','#676054','#7a7265','#8d8477','#a1988a'];
  const POLE     = ['#33291f','#42342a','#523f33','#63503f','#75604c','#8a7460'];
  const IRON     = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970'];
  const GALV     = ['#565b5f','#6d7276','#868b8f','#a0a5a8','#bbbfc1','#d6d9da'];
  const ALUM     = ['#3f4448','#525a5e','#687175','#7f888c','#98a0a3','#b2b9bb'];
  const CONCRETE = ['#55564f','#686962','#7c7d74','#919287','#a6a79b','#bbbcae'];
  const ROCK     = ['#252b31','#3a434c','#525a64','#6a727b','#848c94','#9aa2a9'];
  const RUBBER   = ['#141517','#1d1f22','#282b2f','#34383d','#43484d','#54595f'];
  const ROPE     = ['#5a4526','#71592f','#8a6f3d','#a2874e','#b89f66','#cdb686'];
  const NET      = ['#1d2a1e','#2a3a29','#374b34','#465e41','#57724f','#6a875f'];
  const YEL      = ['#6a4d17','#8e6a20','#b8923c','#cfa945','#e0bd58','#eed37e'];
  const BARN     = ['#6b6a5f','#807f72','#989685','#b0ad9a','#c6c3af','#dcd9c4'];
  const WEED     = ['#2b2a15','#3b3a1c','#4d4a22','#605c2b','#756f36','#8a8244'];
  const RUST     = ['#3a1c10','#4f2614','#6a3620','#84462b','#9c5b3a','#b2724c'];
  const STEEL    = ['#2a2f33','#3c454b','#525c63','#6d777e','#889298','#a2acb1'];
  // ---- yard ground + fabric ----
  const GRASS    = ['#1f2a18','#2a3820','#35462a','#425536','#516644','#627955'];
  const GRAVEL   = ['#3c3a34','#4b4941','#5a584e','#6b685c','#7c796b','#8d8a7b'];
  const DIRT     = ['#2f2419','#3d2f21','#4b3b2a','#5a4934','#6b5941','#7c6a50'];
  const SLEEPER  = ['#2b2118','#3a2c20','#493829','#584534','#685440','#79654f'];
  const CORR     = ['#4a5155','#5d666b','#727d83','#8a959b','#a2adb2','#bcc5c9']; // galvanised sheet
  const REDPAINT = ['#3a1512','#511d17','#6b2a1f','#84392a','#9c4d3a','#b3654e'];
  const GRNPAINT = ['#16281f','#1f3729','#2a4735','#375943','#466c53','#588066'];
  const BUFF     = ['#4d4433','#63583f','#7a6d4e','#91845f','#a89c74','#bfb48d'];
  const WHITEP   = ['#4f5452','#666b68','#7f847f','#989c96','#b1b5ae','#cbcec6'];
  const GLASS    = ['#12191d','#1a2429','#243138','#2f414a','#3d545e','#5a7581'];
  const MESH     = ['#4b5155','#5d6367','#6f7579','#81878b','#93999d','#a5abaf'];
  const TARP     = ['#2c3038','#3a3f49','#49505b','#5a626e','#6c7581','#7f8894'];
  const SHADE    = ['#0d1114','#11161a','#161c21','#1b2228','#20282f','#252e36']; // shed interior
  const KEY      = '#1a1c22';

  // ---- shading (identical recipe to wharfIsoRig) ----
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v = [-0.42, 0.72, 0.52]; const m = Math.hypot(...v); return v.map(c => c / m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r => r.map(v => (v + 0.5) / 16));
  function mulberry32(a){ return function(){ a|=0; a=a+0x6D2B79F5|0; let t=Math.imul(a^a>>>15,1|a); t=t+Math.imul(t^t>>>7,61|t)^t; return ((t^t>>>14)>>>0)/4294967296; }; }
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16), parseInt(h.slice(3,5),16), parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a), B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t, A[1]+(B[1]-A[1])*t, A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t, g+(l-g)*t, b+(l-b)*t); }
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }

  function camBasis(opts){ const dir=opts.dir||0, th=dir*Math.PI/4, e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { th, ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e), S:opts.S||PX }; }
  function projRaw(x,y,z,B){ const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr, yr, zr, sx:xr*B.S, sy:-(yr*B.se + zr*B.ce)*B.S, d:(yr*B.ce - zr*B.se) }; }
  function normal(a,b,c){ const ux=b.xr-a.xr, uy=b.yr-a.yr, uz=b.zr-a.zr, vx=c.xr-a.xr, vy=c.yr-a.yr, vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx; const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m]; }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se + n[2]*ce)*LN[1] + (-n[1]*ce + n[2]*se)*LN[2]; }

  // ---- face builders ----
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
  function tube(out, p0, p1, r, n, mat, b, tex, caps){
    const u=nrm(sub(p1,p0)); const a=Math.abs(u[2])>0.9?[1,0,0]:[0,0,1];
    const e1=nrm(crs(a,u)), e2=nrm(crs(u,e1)); n=n||8; b=b||0;
    const L=Math.hypot(p1[0]-p0[0],p1[1]-p0[1],p1[2]-p0[2]);
    const P=(i,end)=>{ const th=(i/n)*Math.PI*2, c=Math.cos(th)*r, s=Math.sin(th)*r, q=end?p1:p0;
      return [q[0]+e1[0]*c+e2[0]*s, q[1]+e1[1]*c+e2[1]*s, q[2]+e1[2]*c+e2[2]*s]; };
    const arc=(2*Math.PI*r)/n;
    for(let i=0;i<n;i++){ const j=(i+1)%n;
      out.push(F([P(i,0),P(j,0),P(j,1),P(i,1)], mat, b, 0, tex?[[i*arc,0],[(i+1)*arc,0],[(i+1)*arc,L],[i*arc,L]]:null, tex)); }
    if(caps!==false){ const top=[], bot=[];
      for(let i=0;i<n;i++){ top.push(P(i,1)); bot.push(P(n-1-i,0)); }
      out.push(F(top, mat, b+0.26)); out.push(F(bot, mat, b-0.5)); }
  }
  const pipe = (out,x,y,r,z0,z1,mat,b,n,tex)=> tube(out,[x,y,z0],[x,y,z1],r,n||10,mat,b,tex);
  const beam = (out,p0,p1,r,mat,b,n)=> tube(out,p0,p1,r,n||4,mat,b,null,true);
  function torusZ(out, x,y,z, R, r, n, m, mat, b){
    const P=(i,j)=>{ const th=i/n*Math.PI*2, ph=j/m*Math.PI*2, rr=R+r*Math.cos(ph);
      return [x+Math.cos(th)*rr, y+Math.sin(th)*rr, z+r*Math.sin(ph)]; };
    for(let i=0;i<n;i++) for(let j=0;j<m;j++){ const i2=(i+1)%n, j2=(j+1)%m;
      out.push(F([P(i,j),P(i2,j),P(i2,j2),P(i,j2)], mat, (b||0)+Math.sin(j/m*Math.PI*2)*0.22)); }
  }
  // a wheel / sheave standing in the X-Z plane (axle along Y)
  function discY(out, x,y,z, R, w, n, mat, b){
    const P=(i,e)=>{ const th=i/n*Math.PI*2; return [x+Math.cos(th)*R, y+(e?w/2:-w/2), z+Math.sin(th)*R]; };
    for(let i=0;i<n;i++){ const j=(i+1)%n; out.push(F([P(i,0),P(j,0),P(j,1),P(i,1)], mat, (b||0)+Math.sin(i/n*Math.PI*2)*0.3)); }
    const a=[], c=[]; for(let i=0;i<n;i++){ a.push(P(i,1)); c.push(P(n-1-i,0)); }
    out.push(F(a, mat, (b||0)+0.15)); out.push(F(c, mat, (b||0)-0.35));
  }
  function decalZ(out, zv, xs,xe, ys,ye, mat, b, tex, flat, db){
    out.push(F([[xs,ys,zv],[xe,ys,zv],[xe,ye,zv],[xs,ye,zv]], mat, b||0, db!=null?db:0.05,
      tex?[[0,0],[xe-xs,0],[xe-xs,ye-ys],[0,ye-ys]]:null, tex||null, flat)); }
  // build a part at the origin, then drop it into the yard at (x,y,z) rotated by ang radians
  function group(out, x, y, ang, fn, z){
    const tmp = []; fn(tmp); const c = Math.cos(ang||0), sn = Math.sin(ang||0), dz = z || 0;
    for(const f of tmp){ f.v = f.v.map(([vx,vy,vz]) => [x + vx*c - vy*sn, y + vx*sn + vy*c, vz + dz]); out.push(f); }
  }

  // ---- textures (a texture may return null: that pixel is a HOLE — see-through mesh / open web) ----
  function plankTex(p){ p=p||0.20; return (u,v)=>{ const f=((u%p)+p)%p; if(f<0.028) return -2;
    return hash2(Math.floor(u/p)|0, Math.floor(v*2.4)|0)<0.42?-1:0; }; }
  function grainTex(p){ p=p||0.28; return (u,v)=>{ const f=((v%p)+p)%p; if(f<0.03) return -2;
    return hash2(Math.floor(v/p)|0, Math.floor(u*3)|0)<0.5?0:-1; }; }
  function sawnTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*7)|0, Math.floor(v*22)|0); return h<0.3?-1:(h>0.9?1:0); }; }
  function formTex(p){ p=p||0.62; return (u,v)=>{ const f=((v%p)+p)%p;
    if(f<0.035) return -2; const g=((u%1.22)+1.22)%1.22; if(g<0.03) return -1;
    return hash2(Math.floor(u*5)|0, Math.floor(v*5)|0)<0.24?-1:0; }; }
  function rockTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*6)|0, Math.floor(v*6)|0); return h<0.26?-1:(h>0.84?1:0); }; }
  function crustTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*26)|0, Math.floor(v*26)|0); return h<0.30?-2:(h>0.70?1:0); }; }
  function weedTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*9)|0, Math.floor(v*17)|0); return h<0.34?-2:(h>0.8?1:0); }; }
  function rustTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*9)|0, Math.floor(v*9)|0); return h<0.22?-2:(h>0.88?1:0); }; }
  function ribTex(p){ p=p||0.30; return (u,v)=>{ const f=((u%p)+p)%p; return f<p*0.30?-2:(f>p*0.72?1:0); }; }
  function corrTex(p){ p=p||0.26; return (u,v)=>{ const f=(((u%p)+p)%p)/p;                  // corrugated sheet
    const s = f<0.18 ? -2 : f<0.42 ? -1 : f<0.72 ? 0 : 1;
    return s + (hash2(Math.floor(u*3)|0, Math.floor(v*2.2)|0)<0.16 ? -1 : 0); }; }
  function seamTex(p){ p=p||0.55; return (u,v)=>{ const f=(((u%p)+p)%p)/p; return f<0.07?-2:(f>0.93?1:0); }; }
  function apronTex(p){ p=p||3.6; return (u,v)=>{ const a=((u%p)+p)%p, b=((v%p)+p)%p;   // concrete apron: sawn joints
    if(a<0.10||b<0.10) return -2;
    const h=hash2(Math.floor(u*2.2)|0, Math.floor(v*2.2)|0);
    return h<0.16?-1:h>0.93?1:0; }; }
  function gravelTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*4.4)|0, Math.floor(v*4.4)|0);
    return h<0.20?-2:h<0.44?-1:h>0.90?1:0; }; }
  function grassTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*5.5)|0, Math.floor(v*5.5)|0);
    const g=hash2(Math.floor(u*1.7)|0, Math.floor(v*1.7)|0);
    return (h<0.30?-1:h>0.88?1:0) + (g<0.14?-1:0); }; }
  function dirtTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*3.1)|0, Math.floor(v*3.1)|0); return h<0.30?-1:h>0.86?1:0; }; }
  function sleeperTex(gap, w){ gap=gap||1.15; w=w||0.26; return (u,v)=>{ const f=((v%gap)+gap)%gap;   // ways: timber across
    if(f>w) return null; if(f<0.03||f>w-0.03) return -2;
    return hash2(Math.floor(v/gap)|0, Math.floor(u*2.6)|0)<0.44?-1:0; }; }
  function meshTex(m){ m=m||0.13; return (u,v)=>{ const w=0.030;                          // chain-link diamonds
    const a=(((u+v)%m)+m)%m, b=(((u-v)%m)+m)%m;
    if(a<w) return 0; if(b<w) return -1; return null; }; }
  function paneTex(pw, ph){ pw=pw||0.62; ph=ph||0.72; return (u,v)=>{ const a=((u%pw)+pw)%pw, b=((v%ph)+ph)%ph;
    if(a<0.05||b<0.05) return 2; return (a/pw + b/ph) < 0.55 ? 1 : -1; }; }
  function webTex(p){ p=p||0.9; return (u,v)=>{ const a=(((u+v*1.4)%p)+p)%p;              // open lattice web
    if(a<0.10) return 0; const b=(((u-v*1.4)%p)+p)%p; if(b<0.10) return -1; return null; }; }

  // ============================ THE TIDAL FRAME ==============================================
  function frame(s){
    const R = Math.max(0.3, s.tideRange), w = s.tide;
    return { R, w, hhw:R, mid:R*0.5, stainTop:R + 0.10,
      iceBot:R*0.90, iceTop:Math.min(R + 0.14, R*1.04 + 0.10),
      barnTop:R*0.80, barnBot:R*0.40, weedTop:R*0.40, weedBot:R*0.06, splash:w + 0.05 };
  }
  const GROWTH_ALL = { stain:true, barn:true, weed:true, ice:true, wet:true };
  function matAtZ(z, base, T, s){
    const g = s.growth; let k = base;
    if(g.stain && z <= T.stainTop) k = base + 'S';
    if(g.barn  && z <= T.barnTop && z >= T.barnBot) k = base + 'B';
    if(g.weed  && z <= T.weedTop && z >= T.weedBot) k = base + 'G';
    if(g.ice   && z <= T.iceTop  && z >= T.iceBot)  k = base + 'I';
    if(g.wet   && z <  T.splash) k += 'W';
    return k;
  }
  function zSplit(z0, z1, base, T, s){
    const cuts = [z0, z1];
    for(const c of [T.stainTop, T.barnTop, T.barnBot, T.weedTop, T.weedBot, T.iceBot, T.iceTop, T.splash])
      if(c > z0 + 0.01 && c < z1 - 0.01) cuts.push(c);
    cuts.sort((a,b)=>a-b);
    const segs = [];
    for(let i=0;i<cuts.length-1;i++){ const a=cuts[i], b=cuts[i+1]; if(b-a < 0.008) continue;
      segs.push({ a, b, mat: matAtZ((a+b)/2, base, T, s) }); }
    return segs.length ? segs : [{ a:z0, b:z1, mat: matAtZ((z0+z1)/2, base, T, s) }];
  }
  const baseKey = k => k.replace(/W$/, '');
  const isBarn = k => /B$/.test(baseKey(k));
  const isWeed = k => /G$/.test(baseKey(k)) || baseKey(k) === 'weed';
  function texFor(k, base){ return isBarn(k) ? crustTex() : isWeed(k) ? weedTex()
    : (base==='conc' ? formTex() : base==='stone' ? rockTex() : grainTex(0.30)); }
  function bandedPile(out, x, y, r, z0, z1, base, T, s, n){
    n = n || 8;
    for(const g of zSplit(z0, z1, base, T, s)){
      const rr = isBarn(g.mat) ? r*1.07 : isWeed(g.mat) ? r*1.04 : r;
      tube(out, [x,y,g.a], [x,y,g.b], rr, n, g.mat, isBarn(g.mat)?0.22:isWeed(g.mat)?-0.35:0, texFor(g.mat, base), false);
    }
    const topPts = []; for(let i=0;i<n;i++){ const t=(i/n)*Math.PI*2; topPts.push([x+Math.cos(t)*r, y-Math.sin(t)*r]); }
    slab(out, topPts, z1, matAtZ(z1, base, T, s), 0.30, grainTex(0.22));
    if(s.growth.weed && T.weedTop > z0 && T.weedTop < z1) weedFringe(out, x, y, r, T.weedTop, s);
  }
  function weedFringe(out, x, y, r, zTop, s){
    if(s.lod < 1) return;
    const rnd = mulberry32(((x*911 + y*577 + s.variant*31)|0) >>> 0);
    const n = 4 + ((rnd()*3)|0);
    for(let i=0;i<n;i++){
      const th = rnd()*Math.PI*2, len = 0.22 + rnd()*0.34;
      const px = x + Math.cos(th)*r*0.94, py = y + Math.sin(th)*r*0.94;
      const bx = px + Math.cos(th)*0.05, by = py + Math.sin(th)*0.05;
      tube(out, [px,py,zTop+0.02], [bx,by,zTop-len*0.55], 0.032, 5, 'weed', -0.3, null, false);
      tube(out, [bx,by,zTop-len*0.55], [bx+Math.cos(th)*0.03, by+Math.sin(th)*0.03, zTop-len], 0.022, 5, 'weed', -0.7, null, false);
    }
  }
  function bandedWall(out, x0,y0,x1,y1, z0,z1, base, T, s, b, texOv){
    for(const g of zSplit(z0, z1, base, T, s))
      wall(out, x0,y0, x1,y1, g.a, g.b, g.mat, (isBarn(g.mat)||isWeed(g.mat)) ? texFor(g.mat, base) : (texOv || texFor(g.mat, base)),
        (b||0) + (isBarn(g.mat)?0.20:isWeed(g.mat)?-0.32:0));
  }

  // ============================ THE FLEET THE YARDS ARE BUILT AROUND =========================
  // loa/beam from the hull rigs; draft + depth (keel to sheer) are what a yard actually needs —
  // they set blocking height, cradle depth, gantry clearance and how deep the basin has to be.
  // loa / beam / depth are READ OFF THE FLEET RIGS themselves (offsets tables), not guessed, so a
  // cradle cut for `lobster` is cut for the boat LobsterBoatIso actually bakes. `rig` names the global
  // the compositor loads to drop a real hull on the mount.
  const SHIPS = [
    { id:'dory',    label:'dory',                loa:4.5,   beam:1.50,  draft:0.26, depth:0.60,  tier:0, rig:'DoryIso' },
    { id:'punt',    label:'punt',                loa:5.2,   beam:1.58,  draft:0.28, depth:0.56,  tier:0, rig:'PuntIso' },
    { id:'skiff',   label:'console skiff',       loa:7.0,   beam:2.30,  draft:0.42, depth:0.82,  tier:1, rig:'ConsoleIso' },
    { id:'lobster', label:'lobster boat',        loa:12.0,  beam:4.44,  draft:1.35, depth:2.84,  tier:2, rig:'LobsterBoatIso' },
    { id:'cape',    label:'Cape Islander',       loa:12.8,  beam:4.40,  draft:1.40, depth:2.90,  tier:2, rig:'CapeIslanderIso' },
    { id:'dragger', label:'side dragger',        loa:25.0,  beam:7.00,  draft:2.55, depth:3.50,  tier:4, rig:'SideDraggerIso' },
    { id:'trawler', label:'stern trawler Mk II', loa:38.0,  beam:9.00,  draft:3.90, depth:5.10,  tier:5, rig:'SternTrawlerMk2Iso' },
    { id:'packet',  label:'coastal packet',      loa:60.0,  beam:10.40, draft:4.20, depth:5.25,  tier:6, rig:'CoastalPacketIso' },
    { id:'tanker',  label:'gas tanker',          loa:110.0, beam:17.40, draft:6.20, depth:12.30, tier:7, rig:'TankerIso' },
  ];
  const ship = (id)=> SHIPS.find(b => b.id === id) || SHIPS[3];
  // which fleet rigs are actually on the page — the yards still bake without them
  function hullRigs(){ return SHIPS.map(b => ({ id:b.id, rig:b.rig, loaded: !!(root[b.rig] && root[b.rig].render) })); }

  // ============================ PARTS ========================================================
  // Every part is modelled at the origin in metres: +X along its own length, +Y to its own left,
  // z=0 at the ground it stands on. group() puts it in the yard. Sizes come from the hull served.

  // --- ground: a graded pad with its own surface ---
  const SURF = {
    gravel: { mat:'gravel', tex:gravelTex(),  label:'crushed gravel hardstand' },
    conc:   { mat:'conc',   tex:apronTex(3.6),  label:'concrete apron' },
    dirt:   { mat:'dirt',   tex:dirtTex(),    label:'packed dirt' },
    grass:  { mat:'grass',  tex:grassTex(),   label:'grass verge' },
    ways:   { mat:'sleeper',tex:sleeperTex(), label:'timber ways & sleepers' },
  };
  // The pad is drawn around its HOLES — the slipway trough, the dock basin, the lift slot — by a
  // sweep in x then y, so the yard surface never covers the thing that was cut into it.
  function pad(out, x0,x1, y0,y1, z, surf, s, edge, holes){
    const S = SURF[surf] || SURF.gravel;
    holes = (holes || []).filter(h => h && h.x1 > x0 && h.x0 < x1 && h.y1 > y0 && h.y0 < y1);
    const xs = [x0, x1];
    for(const h of holes){ if(h.x0 > x0 && h.x0 < x1) xs.push(h.x0); if(h.x1 > x0 && h.x1 < x1) xs.push(h.x1); }
    xs.sort((a,b)=>a-b);
    for(let i=0;i<xs.length-1;i++){
      const a = xs[i], b = xs[i+1]; if(b - a < 0.05) continue; const mx = (a+b)/2;
      const cuts = [y0, y1];
      for(const h of holes) if(mx > h.x0 && mx < h.x1){
        if(h.y0 > y0 && h.y0 < y1) cuts.push(h.y0); if(h.y1 > y0 && h.y1 < y1) cuts.push(h.y1); }
      cuts.sort((p,q)=>p-q);
      for(let j=0;j<cuts.length-1;j++){
        const c = cuts[j], d = cuts[j+1]; if(d - c < 0.05) continue; const my = (c+d)/2;
        if(holes.some(h => mx > h.x0 && mx < h.x1 && my > h.y0 && my < h.y1)) continue;
        slab(out, [[a,c],[b,c],[b,d],[a,d]], z, S.mat, surf==='grass'?0.05:0.12, S.tex);
      }
    }
    if(edge !== false){ const t = 0.34;
      wall(out, x1,y0, x0,y0, z-t, z, S.mat, gravelTex(), -0.40);
      wall(out, x0,y0, x0,y1, z-t, z, S.mat, gravelTex(), -0.22);
      wall(out, x1,y1, x1,y0, z-t, z, S.mat, gravelTex(), 0.02);
    }
  }
  // scattered stone / puddle / oil grime so a big apron is never a flat rectangle
  function litter(out, x0,x1,y0,y1, z, s, dens, seed){
    if(s.lod < 1) return;
    const rnd = mulberry32(((seed||7)*7919 + s.variant*131) >>> 0);
    const n = Math.round((x1-x0)*(y1-y0) * (dens || 0.03));
    for(let i=0;i<n;i++){
      const x = x0 + rnd()*(x1-x0), y = y0 + rnd()*(y1-y0), r = 0.16 + rnd()*0.42;
      const kind = rnd();
      if(kind < 0.42) decalZ(out, z+0.01, x, x+r*2.2, y, y+r*1.5, rnd()<0.5?'dirt':'gravel', -0.55, dirtTex(), false, 0.04);
      else if(kind < 0.72) box(out, x, x+r*0.9, y, y+r*0.7, z, z+0.10+rnd()*0.10, 'stone', -0.2, rockTex());
      else decalZ(out, z+0.01, x, x+r*1.4, y, y+r*1.2, 'sleeper', -0.7, dirtTex(), false, 0.04);
    }
  }

  // --- the water edge: how the yard meets the sea ---
  function shoreEdge(out, x0, x1, yEdge, z, kind, s, T, notch){
    const spans = notch ? [[x0, Math.max(x0, notch.x0)], [Math.min(x1, notch.x1), x1]].filter(a => a[1] - a[0] > 0.4) : [[x0, x1]];
    for(const [a, b] of spans) shoreSpan(out, a, b, yEdge, z, kind, s, T);
  }
  function shoreSpan(out, x0, x1, yEdge, z, kind, s, T){
    if(kind === 'quay'){
      bandedWall(out, x1, yEdge, x0, yEdge, s.mudZ, z - 0.30, 'conc', T, s, 0.05, formTex(0.8));
      box(out, x0, x1, yEdge - 0.55, yEdge, z - 0.30, z, matAtZ(z,'conc',T,s), 0.22, formTex(0.6));
      for(let x = x0 + 3; x < x1 - 2; x += 7.5){                       // bollards along the coping
        pipe(out, x, yEdge - 0.65, 0.15, z, z + 0.62, 'iron', 0.1, 8);
        torusZ(out, x, yEdge - 0.65, z + 0.66, 0.13, 0.05, 8, 5, 'iron', 0.25);
      }
      return;
    }
    if(kind === 'sheet'){
      bandedWall(out, x1, yEdge, x0, yEdge, s.mudZ, z - 0.22, 'rust', T, s, 0.05, ribTex(0.62));
      box(out, x0, x1, yEdge - 0.42, yEdge, z - 0.22, z, matAtZ(z,'steel',T,s), 0.2, null);
      return;
    }
    // rubble bank / beach: stones tumbling from the pad down past the water. The run is solved from
    // the drop and capped, so a 3 m grade reads as a beach and never as a grey apron.
    const rnd = mulberry32((311 + s.variant*77 + Math.round(x0*13)) >>> 0);
    const drop = z - Math.min(-0.6, s.mudZ*0.55);
    const run = Math.max(2.2, Math.min(drop * (kind === 'beach' ? 1.5 : 1.2), kind === 'beach' ? 3.4 : 2.8));
    const zAt = (y)=> z - drop * Math.min(1, Math.max(0, (y - yEdge) / run));
    const steps = Math.max(4, Math.round((x1-x0)/1.4));
    for(let i=0;i<steps;i++){
      const xa = x0 + (x1-x0)*(i/steps), xb = x0 + (x1-x0)*((i+1)/steps);
      const m = matAtZ((z + zAt(yEdge+run))/2, kind === 'beach' ? 'gravel' : 'stone', T, s);
      out.push(F([[xa,yEdge,z-0.05],[xb,yEdge,z-0.05],[xb,yEdge+run,zAt(yEdge+run)],[xa,yEdge+run,zAt(yEdge+run)]],
        m, -0.25, 0, [[xa,0],[xb,0],[xb,run],[xa,run]], kind === 'beach' ? gravelTex() : rockTex()));
    }
    if(s.lod < 1) return;
    const nst = Math.round((x1-x0) * run * (kind === 'beach' ? 0.75 : 1.10));    for(let i=0;i<nst;i++){
      const x = x0 + rnd()*(x1-x0), yy = yEdge + rnd()*run, r = (kind==='beach'?0.12:0.24) + rnd()*(kind==='beach'?0.14:0.30);
      const zz = zAt(yy) + r*0.4;
      box(out, x, x+r*1.9, yy, yy+r*1.6, zz-r, zz, matAtZ(zz, kind==='beach'?'gravel':'stone', T, s), -0.3 + rnd()*0.7, rockTex());
      if(s.growth.weed && Math.abs(zz - T.weedTop) < 0.3 && rnd() < 0.4) weedFringe(out, x, yy, r*0.7, zz, s);
    }
  }

  // --- SLIPWAY: the concrete/timber ramp the railway runs down ---
  function slipway(out, p, s, T){
    const hw = p.width/2, run = p.run, z0 = p.crestZ, z1 = p.toeZ;
    const zAt = (y)=> z0 + (z1 - z0) * Math.min(1, Math.max(0, y / run));
    const steps = Math.max(6, Math.round(run/1.1));
    for(let i=0;i<steps;i++){
      const ya = run*(i/steps), yb = run*((i+1)/steps), za = zAt(ya), zb = zAt(yb);
      const m = matAtZ((za+zb)/2, p.deck === 'timber' ? 'pole' : 'conc', T, s);
      out.push(F([[-hw,ya,za],[hw,ya,za],[hw,yb,zb],[-hw,yb,zb]], m, 0.06, 0,
        [[0,ya],[p.width,ya],[p.width,yb],[0,yb]], p.deck === 'timber' ? plankTex(0.28) : ribTex(0.34)));
    }
    for(const sx of [-1, 1]){                                            // side walls keep the fill in
      const xw = sx*hw;
      for(let i=0;i<steps;i++){
        const ya = run*(i/steps), yb = run*((i+1)/steps), za = zAt(ya), zb = zAt(yb);
        const m = matAtZ((za+zb)/2, 'conc', T, s);
        const a = sx>0 ? [[xw,ya,za-0.55],[xw,yb,zb-0.55],[xw,yb,zb+0.14],[xw,ya,za+0.14]]
                       : [[xw,yb,zb-0.55],[xw,ya,za-0.55],[xw,ya,za+0.14],[xw,yb,zb+0.14]];
        out.push(F(a, m, sx>0?0.1:-0.5, 0, [[ya,0],[yb,0],[yb,0.7],[ya,0.7]], formTex(0.7)));
        out.push(F([[xw - sx*0.28, ya, za+0.14],[xw - sx*0.28, yb, zb+0.14],[xw, yb, zb+0.14],[xw, ya, za+0.14]]
          .slice(sx>0?0:0), m, 0.3, 0, [[0,0],[yb-ya,0],[yb-ya,0.28],[0,0.28]], ribTex(0.3)));
      }
    }
    if(p.rails !== false) railway(out, { gauge:p.gauge, run:run + 3.0, y0:-3.0, zAt:(y)=> zAt(y) + 0.10, sleepers:true }, s, T);
  }
  // --- MARINE RAILWAY: rails on sleepers, running from the winch head down under water ---
  function railway(out, p, s, T){
    const g = p.gauge/2, y0 = p.y0 != null ? p.y0 : 0, y1 = y0 + p.run, zAt = p.zAt;
    if(p.sleepers !== false){
      const step = Math.max(0.9, 1.15 * (s.lod < 1 ? 1.9 : 1));
      for(let y = y0; y <= y1 - 0.3; y += step){
        const z = zAt(y), m = matAtZ(z, 'pole', T, s);
        box(out, -g-0.34, g+0.34, y, y+0.26, z-0.16, z, m, -0.15, grainTex(0.24));
      }
    }
    for(const sx of [-1,1]){                                            // rail head: two low bars
      const steps = Math.max(3, Math.round((y1-y0)/2.2));
      for(let i=0;i<steps;i++){
        const ya = y0 + (y1-y0)*(i/steps), yb = y0 + (y1-y0)*((i+1)/steps);
        const za = zAt(ya), zb = zAt(yb), m = matAtZ((za+zb)/2, 'rust', T, s);
        out.push(F([[sx*g-0.05,ya,za],[sx*g+0.05,ya,za],[sx*g+0.05,yb,zb],[sx*g-0.05,yb,zb]], m, 0.55, 0.02));
        out.push(F([[sx*g+0.05,ya,za],[sx*g+0.05,ya,za-0.10],[sx*g+0.05,yb,zb-0.10],[sx*g+0.05,yb,zb]], m, sx>0?0.2:-0.5, 0.02));
        out.push(F([[sx*g-0.05,ya,za-0.10],[sx*g-0.05,ya,za],[sx*g-0.05,yb,zb],[sx*g-0.05,yb,zb-0.10]], m, sx>0?-0.5:0.2, 0.02));
      }
    }
  }
  // --- CRADLE: the wheeled carriage a hull rides on, sized to the hull ---
  function cradle(out, p, s){
    const H = p.hull, len = Math.min(H.loa*0.78, p.maxLen || 1e9), hw = (H.beam*0.62)/2, deck = p.deckZ || 0.55;
    const bw = Math.max(0.14, H.beam*0.030), mat = p.steel ? 'steel' : 'wood', tex = p.steel ? null : sawnTex();
    for(const sx of [-1,1])                                              // main longitudinal beams
      box(out, -len/2, len/2, sx*hw - bw/2, sx*hw + bw/2, deck - bw*1.5, deck, mat, 0.05, tex);
    const nx = Math.max(3, Math.round(len/2.6));
    for(let i=0;i<=nx;i++){ const x = -len/2 + len*(i/nx);               // cross members + bilge pads
      box(out, x - bw*0.45, x + bw*0.45, -hw, hw, deck - bw*1.2, deck - bw*0.2, mat, -0.2, tex);
      if(i%1===0) for(const sx of [-1,1]){
        const px = x, py = sx*hw*0.82, ph = deck + Math.max(0.35, H.draft*0.28);
        beam(out, [px, py, deck], [px, py*0.86, ph], bw*0.55, mat, -0.1, 4);
        box(out, px - bw*0.5, px + bw*0.5, py*0.86 - bw*0.7, py*0.86 + bw*0.7, ph, ph + 0.10, 'pole', 0.25, grainTex(0.2));
      }
    }
    const kz = deck + Math.max(0.35, H.draft*0.16);                       // keel way down the centre
    box(out, -len/2, len/2, -bw*0.8, bw*0.8, deck, kz, 'pole', 0.18, grainTex(0.24));
    if(p.wheels !== false){ const wr = Math.min(0.22 + H.beam*0.012, Math.max(0.12, (deck - bw*1.5)*0.9));
      for(let i=0;i<=nx;i+=Math.max(1, Math.round(nx/3))) for(const sx of [-1,1]){
        const x = -len/2 + len*(i/nx);
        discY(out, x, sx*(p.gauge ? p.gauge/2 : hw), wr, wr, wr*0.5, 10, 'iron', 0.0);
      } }
    return { keelZ: kz, len };
  }
  // --- KEEL BLOCKS + BILGE POPPETS: a hull sat dry on blocking, no machine ---
  function keelBlocks(out, p, s){
    const H = p.hull, h = p.blockZ, len = H.loa*0.62, n = Math.max(2, Math.round(len/Math.max(2.0, H.loa*0.13)));
    const bw = Math.max(0.30, H.beam*0.09), bl = Math.max(0.55, H.beam*0.16);
    for(let i=0;i<n;i++){
      const x = -len/2 + len*(i/(n-1 || 1));
      const layers = Math.max(2, Math.round(h/0.32));
      for(let k=0;k<layers;k++){ const z0 = h*(k/layers), z1 = h*((k+1)/layers), across = k%2===0;
        if(across) box(out, x - bl/2, x + bl/2, -bw/2, bw/2, z0, z1, 'pole', 0.05 + (k%2)*0.1, grainTex(0.22));
        else       box(out, x - bw/2, x + bw/2, -bl/2, bl/2, z0, z1, 'wood', 0.0 + (k%2)*0.1, sawnTex());
      }
    }
    if(p.shores !== false){                                              // side shores out to the ground
      const ns = Math.max(2, Math.round(n/1.4));
      for(let i=0;i<ns;i++){ const x = -len/2 + len*((i+0.5)/ns);
        for(const sx of [-1,1]){
          const footY = sx*(H.beam*0.62 + 0.7), headY = sx*H.beam*0.40, headZ = h + H.draft*0.60;
          beam(out, [x, footY, 0.02], [x, headY, headZ], Math.max(0.06, H.beam*0.016), 'pole', -0.15, 4);
          box(out, x-0.22, x+0.22, footY - 0.26, footY + 0.26, 0, 0.14, 'wood', 0.1, sawnTex());
        } }
    }
    return { keelZ: h };
  }
  // --- DAVITS: pairs of posts + falls, the boat slung between them ---
  function davitPair(out, p, s){
    const H = p.hull, spread = H.loa*0.52, span = H.beam + Math.max(1.1, H.beam*0.30);
    const hz = p.slingZ + Math.max(2.0, H.depth*0.95), pr = Math.max(0.16, H.beam*0.038);
    const timber = p.style !== 'steel', mat = timber ? 'pole' : 'steel';
    for(const sx of [-1,1]){ const x = sx*spread/2;
      for(const sy of [-1,1]){ const y = sy*span/2;
        if(timber) tube(out, [x,y,0], [x,y,hz], pr, 8, mat, 0.0, grainTex(0.3), false);
        else { box(out, x-pr, x+pr, y-pr, y+pr, 0, hz, mat, 0.05, null, true);
               box(out, x-pr*1.7, x+pr*1.7, y-pr*1.7, y+pr*1.7, 0, 0.16, 'conc', 0.15, formTex(0.4)); }
        beam(out, [x, y, hz*0.62], [x + sx*Math.min(1.3, spread*0.16), y, hz*0.98], pr*0.62, mat, -0.2, 4);  // knee brace
        beam(out, [x, y, hz*0.55], [x, y - sy*span*0.22, hz*0.96], pr*0.55, mat, -0.3, 4);                   // cross-head knee
      }
      box(out, x-pr*1.2, x+pr*1.2, -span/2 - 0.22, span/2 + 0.22, hz, hz + pr*2.4, timber?'wood':'steel', 0.24, timber?sawnTex():null);
      // falls: a block hanging at each end of the cross-head, wire down to a sling under the hull
      for(const sy of [-1,1]){ const y = sy*span*0.34;
        tube(out, [x, y, hz], [x, y, p.slingZ + 0.62], 0.028, 6, 'iron', 0.1, null, true);
        box(out, x-0.13, x+0.13, y-0.09, y+0.09, p.slingZ + 0.28, p.slingZ + 0.66, 'iron', 0.15);
        discY(out, x, y, p.slingZ + 0.50, 0.11, 0.06, 8, 'galv', 0.25);
      }
      out.push(F([[x-0.06, -span*0.34, p.slingZ+0.30],[x+0.06, -span*0.34, p.slingZ+0.30],
                  [x+0.06, span*0.34, p.slingZ+0.30],[x-0.06, span*0.34, p.slingZ+0.30]], 'rope', 0.1, 0.02)); // strop across
    }
    return { spread, span, headZ: hz };
  }
  // --- TRAVEL-LIFT: rubber-tyred gantry straddling the boat in slings ---
  function travelLift(out, p, s){
    const H = p.hull, straddle = H.beam + Math.max(2.4, H.beam*0.36), len = Math.min(H.loa*0.70, 26);
    const clear = p.slingZ + H.depth + 1.4, lr = Math.max(0.16, H.beam*0.030);
    for(const sx of [-1,1]) for(const sy of [-1,1]){
      const x = sx*len/2, y = sy*straddle/2;
      box(out, x-lr, x+lr, y-lr, y+lr, 0.62, clear, 'steel', 0.05, seamTex(1.1), true);   // leg
      beam(out, [x, y, clear*0.55], [x - sx*len*0.16, y, clear*0.97], lr*0.5, 'steel', -0.25, 4);
      for(const w of [-1,1]){                                                             // twin tyres
        discY(out, x, y + w*lr*2.2, 0.62, 0.62, 0.30, 12, 'rubber', 0.0);
        discY(out, x, y + w*lr*2.2, 0.62, 0.24, 0.34, 8, 'galv', 0.3);
      }
      box(out, x-lr*1.5, x+lr*1.5, y-lr*3.1, y+lr*3.1, 0.55, 0.95, 'steel', -0.1);        // axle bogie
    }
    for(const sy of [-1,1]){ const y = sy*straddle/2;                                      // side girders
      box(out, -len/2 - lr, len/2 + lr, y - lr*1.2, y + lr*1.2, clear, clear + lr*2.4, 'steel', 0.12, seamTex(1.4));
    }
    const nb = 2;
    for(let i=0;i<nb;i++){ const x = -len*0.26 + len*0.52*i;                               // hoist cross-beams + slings
      box(out, x - lr, x + lr, -straddle/2, straddle/2, clear + lr*0.4, clear + lr*2.0, 'steel', 0.18);
      box(out, x - lr*1.6, x + lr*1.6, -0.55, 0.55, clear + lr*2.0, clear + lr*3.4, 'yel', 0.25);
      for(const sy of [-1,1]){ const y = sy*(H.beam*0.44);
        tube(out, [x, sy*straddle*0.46, clear + lr*0.6], [x, y, p.slingZ + H.draft*0.25], 0.035, 5, 'iron', 0.05, null, true);
      }
      out.push(F([[x-0.16, -H.beam*0.46, p.slingZ + H.draft*0.22],[x+0.16, -H.beam*0.46, p.slingZ + H.draft*0.22],
                  [x+0.16, H.beam*0.46, p.slingZ + H.draft*0.22],[x-0.16, H.beam*0.46, p.slingZ + H.draft*0.22]], 'net', -0.1, 0.02));
    }
    box(out, len/2 - 1.5, len/2 - 0.2, straddle/2 - 0.2, straddle/2 + 0.9, 1.0, 2.5, 'yel', 0.1);   // operator cab
    box(out, len/2 - 1.4, len/2 - 0.3, straddle/2 - 0.1, straddle/2 + 0.85, 1.6, 2.3, 'glass', -0.2, paneTex(0.5,0.5));
    return { straddle, len, clear };
  }
  // --- LIFT DOCK: the two finger piers a travel-lift walks out over. Built from the shore (y=0)
  //     seaward along +Y, so the slot always meets the yard edge instead of floating off it.
  function liftDock(out, p, s, T){
    const slot = p.slot, run = p.run, z = p.deckZ, wid = p.width;
    for(const sx of [-1,1]){
      const x0 = sx*slot/2, x1 = x0 + sx*wid;
      const xa = Math.min(x0,x1), xb = Math.max(x0,x1);
      for(let y = 1.0; y <= run - 0.6; y += 3.0) for(const xx of [xa + 0.6, xb - 0.6])
        bandedPile(out, xx, y, 0.20, s.mudZ, z - 0.35, 'pole', T, s, 8);
      box(out, xa, xb, -1.0, run, z - 0.35, z, matAtZ(z,'conc',T,s), 0.1, apronTex(2.4));
      bandedWall(out, x0, run, x0, -1.0, z - 1.6, z - 0.35, 'conc', T, s, sx>0?0.06:-0.3, formTex(0.7));
      for(let y = 1.5; y < run - 1; y += 4.5){                              // fender timbers down the slot
        const m = matAtZ(z - 1.0, 'pole', T, s);
        box(out, x0 - sx*0.16, x0, y, y + 0.30, s.mudZ*0.5, z - 0.30, m, 0.15, grainTex(0.3));
      }
      for(let y = 3.0; y < run - 1; y += 6.0){                              // bollards on the finger
        pipe(out, x0 + sx*1.2, y, 0.13, z, z + 0.55, 'iron', 0.1, 8);
      }
    }
  }
  // --- WINCH HOUSE: the drum, the wire and the shed over it at the head of the railway ---
  function winchHouse(out, p, s){
    const L = p.L || 4.2, W = p.W || 3.4, h = p.h || 2.5;
    box(out, -L/2, L/2, -W/2, W/2, 0, h, 'corr', 0.05, corrTex(0.26), true);
    const ridge = h + W*0.20;
    out.push(F([[-L/2-0.2,-W/2-0.2,h],[L/2+0.2,-W/2-0.2,h],[L/2+0.2,0,ridge],[-L/2-0.2,0,ridge]], 'rust', 0.35, 0, [[0,0],[L,0],[L,W*0.6],[0,W*0.6]], corrTex(0.3)));
    out.push(F([[L/2+0.2,W/2+0.2,h],[-L/2-0.2,W/2+0.2,h],[-L/2-0.2,0,ridge],[L/2+0.2,0,ridge]], 'rust', -0.15, 0, [[0,0],[L,0],[L,W*0.6],[0,W*0.6]], corrTex(0.3)));
    for(const sx of [-1,1]) out.push(F([[sx*L/2,-W/2,h],[sx*L/2,W/2,h],[sx*L/2,0,ridge]], 'corr', sx>0?0.2:-0.4));
    box(out, -L*0.18, L*0.18, W/2 - 0.05, W/2 + 0.06, 0, 1.95, 'wood', -0.35, grainTex(0.2)); // door
    tube(out, [-L*0.30, -W*0.30, 0.55], [L*0.30, -W*0.30, 0.55], 0.34, 10, 'iron', 0.0, seamTex(0.5), true); // drum
    discY(out, -L*0.30, -W*0.30, 0.55, 0.46, 0.09, 12, 'rust', 0.2);
    discY(out,  L*0.30, -W*0.30, 0.55, 0.46, 0.09, 12, 'rust', 0.2);
  }
  // --- STAGING: pipe scaffold + planks up the side of a hull under repair ---
  function staging(out, p, s){
    if(s.lod < 1) return;
    const len = p.len, h = p.h, lift = Math.max(1.6, h/Math.max(1, Math.round(h/1.9)));
    const nb = Math.max(2, Math.round(len/2.2));
    for(let i=0;i<=nb;i++){ const x = -len/2 + len*(i/nb);
      for(const sy of [0, 1]){ const y = sy*0.9;
        pipe(out, x, y, 0.03, 0, h, 'galv', 0.05, 6);
      }
      for(let z = lift; z <= h + 0.01; z += lift) beam(out, [x, 0, z], [x, 0.9, z], 0.026, 'galv', 0.1, 4);
    }
    for(let z = lift; z <= h + 0.01; z += lift){
      for(const sy of [0,1]) beam(out, [-len/2, sy*0.9, z], [len/2, sy*0.9, z], 0.026, 'galv', 0.15, 4);
      slab(out, [[-len/2, 0.06],[len/2, 0.06],[len/2, 0.84],[-len/2, 0.84]], z + 0.03, 'plank', 0.2, plankTex(0.24));
      for(const sy of [0,1]) beam(out, [-len/2, sy*0.9, z + 1.0], [len/2, sy*0.9, z + 1.0], 0.022, 'galv', 0.05, 4);
    }
  }
  // --- yard clutter, all real objects: timber stack, drums, sawhorse, spar rack ---
  function timberStack(out, p, s){
    const L = p.L || 4.0, W = p.W || 1.1, layers = p.layers || 6;
    for(let k=0;k<layers;k++){ const z0 = k*0.14, across = k%2===1;
      if(across){ const n = Math.max(2, Math.round(L/0.34));
        for(let i=0;i<n;i++){ const y0 = -W/2, x = -L/2 + (i+0.08)*(L/n);
          box(out, x, x + (L/n)*0.84, y0, y0 + W, z0, z0 + 0.13, 'wood', 0.02 + (i%2)*0.12, sawnTex()); } }
      else { const n = Math.max(2, Math.round(W/0.30));
        for(let i=0;i<n;i++){ const y = -W/2 + (i+0.08)*(W/n);
          box(out, -L/2, L/2, y, y + (W/n)*0.84, z0, z0 + 0.13, 'plank', 0.02 + (i%2)*0.12, plankTex(0.5)); } }
    }
  }
  function drums(out, p, s){
    const rnd = mulberry32(((p.seed||3)*104729) >>> 0), n = p.n || 3;
    for(let i=0;i<n;i++){ const x = (rnd()-0.5)*1.5, y = (rnd()-0.5)*1.2, tipped = rnd() < 0.22;
      const m = rnd() < 0.5 ? 'rust' : (rnd() < 0.5 ? 'grn' : 'red');
      if(tipped) tube(out, [x-0.44,y,0.29], [x+0.44,y,0.29], 0.29, 10, m, 0.0, seamTex(0.36), true);
      else { pipe(out, x, y, 0.29, 0, 0.88, m, 0.0, 10, seamTex(0.36));
        torusZ(out, x, y, 0.30, 0.30, 0.035, 10, 5, m, 0.2); torusZ(out, x, y, 0.58, 0.30, 0.035, 10, 5, m, 0.2); }
    }
  }
  function sawhorse(out, p, s){
    const L = 1.35, h = 0.78, sp = 0.42;
    box(out, -L/2, L/2, -0.07, 0.07, h - 0.10, h, 'wood', 0.15, sawnTex());
    for(const sx of [-1,1]) for(const sy of [-1,1])
      beam(out, [sx*(L/2 - 0.16), 0, h - 0.10], [sx*(L/2 - 0.10), sy*sp, 0], 0.045, 'wood', -0.1, 4);
  }
  function sparRack(out, p, s){
    const L = p.L || 7.0;
    for(const sx of [-1,1]){ const x = sx*L*0.32;
      for(const sy of [-1,1]) beam(out, [x, sy*0.55, 0], [x, sy*0.16, 1.35], 0.07, 'pole', 0.0, 4);
      box(out, x - 0.08, x + 0.08, -0.34, 0.34, 1.32, 1.44, 'wood', 0.2, sawnTex());
    }
    const rnd = mulberry32(((p.seed||5)*7717)>>>0);
    for(let i=0;i<3;i++){ const y = -0.26 + i*0.24, r = 0.06 + rnd()*0.05;
      tube(out, [-L/2, y, 1.44 + r], [L/2 - rnd()*1.4, y, 1.44 + r], r, 6, 'pole', 0.1, grainTex(0.4), true); }
  }
  // --- CHAIN-LINK FENCE: real mesh, holes and all ---
  function fenceRun(out, x0,y0, x1,y1, s, opts){
    opts = opts || {};
    const h = opts.h || 2.1, L = Math.hypot(x1-x0, y1-y0), n = Math.max(2, Math.round(L/3.0));
    const gate = opts.gate ? { a: L*0.5 - (opts.gateW||6)/2, b: L*0.5 + (opts.gateW||6)/2 } : null;
    const ux = (x1-x0)/L, uy = (y1-y0)/L;
    const P = (t, z)=> [x0 + ux*t, y0 + uy*t, z];
    for(let i=0;i<=n;i++){ const t = L*(i/n);
      if(gate && t > gate.a + 0.3 && t < gate.b - 0.3) continue;
      const p0 = P(t, 0), p1 = P(t, h);
      tube(out, p0, p1, 0.045, 6, 'galv', 0.05, null, true);
    }
    const segs = gate ? [[0, gate.a], [gate.b, L]] : [[0, L]];
    for(const [a,b] of segs){
      if(b - a < 0.2) continue;
      const A = P(a, 0), B = P(b, 0);
      out.push(F([[A[0],A[1],0.06],[B[0],B[1],0.06],[B[0],B[1],h-0.10],[A[0],A[1],h-0.10]], 'mesh', 0.0, 0.03,
        [[0,0],[b-a,0],[b-a,h-0.16],[0,h-0.16]], meshTex(0.13), true));
      tube(out, P(a, h-0.04), P(b, h-0.04), 0.032, 5, 'galv', 0.2, null, true);
      if(opts.barb !== false) for(const zz of [h + 0.12, h + 0.26])
        tube(out, P(a, zz), P(b, zz), 0.012, 4, 'galv', 0.25, null, true);
    }
    if(gate){                                                            // two leaves swung back
      for(const [t, dir] of [[gate.a, 1], [gate.b, -1]]){
        const w = (gate.b - gate.a)/2 - 0.15;
        const A = P(t, 0), Bx = A[0] + (ux*0.30 - uy*w*dir)*1, By = A[1] + (uy*0.30 + ux*w*dir)*1;
        out.push(F([[A[0],A[1],0.10],[Bx,By,0.10],[Bx,By,h-0.06],[A[0],A[1],h-0.06]], 'mesh', 0.1, 0.03,
          [[0,0],[w,0],[w,h-0.16],[0,h-0.16]], meshTex(0.13), true));
        tube(out, [Bx,By,0], [Bx,By,h], 0.040, 6, 'galv', 0.1, null, true);
        tube(out, [A[0],A[1],h-0.05], [Bx,By,h-0.05], 0.030, 5, 'galv', 0.2, null, true);
      }
    }
  }
  // --- WORKSHOP: a clear-span shed that actually fits the hull, portal frames or timber trusses ---
  function shed(out, p, s){
    const L = p.L, W = p.W, eave = p.eave, pitch = (p.pitch || 16)*DEG;
    const ridge = eave + (W/2)*Math.tan(pitch), body = p.body || 'corr', ov = 0.28;
    const bodyTex = body === 'corr' ? corrTex(0.26) : body === 'board' ? plankTex(0.24) : seamTex(0.9);
    const hw = W/2, hl = L/2;
    const dEnd = p.doorEnd || 1, dW = Math.min(p.doorW || W*0.62, W - 1.0), dH = Math.min(p.doorH || eave - 0.4, eave - 0.25);
    // long walls
    for(const sy of [-1,1]){ const y = sy*hw;
      if(sy > 0) wall(out, -hl, y, hl, y, 0, eave, body, bodyTex, 0.14);
      else wall(out, hl, y, -hl, y, 0, eave, body, bodyTex, -0.42);
    }
    // gable ends, with the door opening cut into the working end
    for(const sx of [-1,1]){ const x = sx*hl, isDoor = sx === dEnd;
      const gable = (yy)=> eave + (hw - Math.abs(yy))*Math.tan(pitch);
      if(isDoor){
        const a = [[x,-hw,0],[x,-dW/2,0],[x,-dW/2,dH],[x,dW/2,dH],[x,dW/2,0],[x,hw,0],[x,hw,eave],[x,0,gable(0)],[x,-hw,eave]];
        out.push(F(sx>0?a:a.slice().reverse(), body, sx>0?0.05:-0.5, 0, a.map(v=>[v[1],v[2]]), bodyTex));
        // recessed opening + a door leaf slid aside
        out.push(F([[x - sx*0.30, -dW/2, 0],[x - sx*0.30, dW/2, 0],[x - sx*0.30, dW/2, dH],[x - sx*0.30, -dW/2, dH]].map(v=>sx>0?v:[v[0],-v[1],v[2]]), 'shade', -1.2, 0.02));
        if(p.doorOpen !== false){
          const lw = dW*0.42;
          out.push(F([[x + sx*0.06, dW/2 - 0.1, 0],[x + sx*0.06, dW/2 + lw, 0],[x + sx*0.06, dW/2 + lw, dH],[x + sx*0.06, dW/2 - 0.1, dH]], 'corr', sx>0?0.3:-0.4, 0.03, [[0,0],[lw,0],[lw,dH],[0,dH]], corrTex(0.24)));
          tube(out, [x + sx*0.10, -dW/2, dH + 0.16], [x + sx*0.10, hw + 0.2, dH + 0.16], 0.05, 5, 'steel', 0.2, null, true);
        }
        box(out, x - sx*0.34, x + sx*0.06, -dW/2 - 0.16, -dW/2, 0, dH + 0.18, 'steel', 0.1);
        box(out, x - sx*0.34, x + sx*0.06, dW/2, dW/2 + 0.16, 0, dH + 0.18, 'steel', 0.1);
      } else {
        const a = [[x,-hw,0],[x,hw,0],[x,hw,eave],[x,0,gable(0)],[x,-hw,eave]];
        out.push(F(sx>0?a:a.slice().reverse(), body, sx>0?0.05:-0.5, 0, a.map(v=>[v[1],v[2]]), bodyTex));
        if(p.window !== false){ const wz = eave*0.52;
          out.push(F([[x + sx*0.03, -W*0.20, wz],[x + sx*0.03, W*0.20, wz],[x + sx*0.03, W*0.20, wz + 1.1],[x + sx*0.03, -W*0.20, wz + 1.1]].map(v=>sx>0?v:[v[0],-v[1],v[2]]),
            'glass', 0.0, 0.03, [[0,0],[W*0.40,0],[W*0.40,1.1],[0,1.1]], paneTex(0.62,0.55))); }
      }
    }
    // roof planes + fascia + ridge
    for(const sy of [-1,1]){
      const a = [[-hl-ov, sy*(hw+ov), eave - ov*Math.tan(pitch)],[hl+ov, sy*(hw+ov), eave - ov*Math.tan(pitch)],[hl+ov, 0, ridge],[-hl-ov, 0, ridge]];
      out.push(F(sy>0?a:a.slice().reverse(), p.roof || 'rust', sy>0?0.42:-0.18, 0,
        [[0,0],[L+ov*2,0],[L+ov*2, hw/Math.cos(pitch)],[0, hw/Math.cos(pitch)]], corrTex(0.30)));
    }
    box(out, -hl-ov, hl+ov, -0.10, 0.10, ridge - 0.04, ridge + 0.12, 'galv', 0.4, seamTex(1.2));
    if(p.monitor && W > 9){                                              // ridge monitor / clerestory
      const ml = L*0.58, mw = W*0.17, mz = ridge - 0.10, mt = mz + 1.0;
      box(out, -ml/2, ml/2, -mw/2, mw/2, mz, mt, body, 0.2, bodyTex, true);
      for(const sy of [-1,1]) out.push(F([[-ml/2, sy*mw/2*1.01, mz+0.2],[ml/2, sy*mw/2*1.01, mz+0.2],[ml/2, sy*mw/2*1.01, mt-0.12],[-ml/2, sy*mw/2*1.01, mt-0.12]].map(v=>sy>0?v:[v[0],v[1],v[2]]), 'glass', sy>0?0.1:-0.4, 0.03, [[0,0],[ml,0],[ml,0.7],[0,0.7]], paneTex(0.7,0.45)));
      for(const sy of [-1,1]) out.push(F(sy>0?[[-ml/2-0.16, sy*(mw/2+0.22), mt],[ml/2+0.16, sy*(mw/2+0.22), mt],[ml/2+0.16, 0, mt+0.34],[-ml/2-0.16, 0, mt+0.34]]
        : [[ml/2+0.16, sy*(mw/2+0.22), mt],[-ml/2-0.16, sy*(mw/2+0.22), mt],[-ml/2-0.16, 0, mt+0.34],[ml/2+0.16, 0, mt+0.34]], p.roof || 'rust', sy>0?0.4:-0.2, 0, [[0,0],[ml,0],[ml,mw*0.7],[0,mw*0.7]], corrTex(0.3)));
    }
    if(p.gantry) gantryCrane(out, { L, W, railZ: eave - Math.max(1.1, eave*0.13), hull:p.hull, hookZ: p.hookZ }, s);
    if(p.stack !== false){ const sxp = -hl*0.55;                        // stove pipe
      pipe(out, sxp, hw*0.55, 0.10, eave + 0.2, ridge + 1.1, 'iron', 0.1, 8);
      torusZ(out, sxp, hw*0.55, ridge + 1.1, 0.13, 0.04, 8, 4, 'iron', 0.2); }
    return { ridge, eave, L, W, doorEnd: dEnd, doorW: dW, doorH: dH };
  }
  // --- OVERHEAD GANTRY: rails on corbels, bridge girder, trolley, hook block ---
  function gantryCrane(out, p, s){
    const hw = p.W/2 - 0.35, z = p.railZ, x = p.x != null ? p.x : p.L*0.12;
    for(const sy of [-1,1]){
      box(out, -p.L/2 + 0.4, p.L/2 - 0.4, sy*hw - 0.12, sy*hw + 0.12, z - 0.34, z, 'steel', 0.05, seamTex(1.1));
      box(out, -p.L/2 + 0.4, p.L/2 - 0.4, sy*hw - 0.05, sy*hw + 0.05, z, z + 0.10, 'rust', 0.4);
    }
    box(out, x - 0.34, x + 0.34, -hw - 0.20, hw + 0.20, z + 0.10, z + 0.86, 'yel', 0.18, seamTex(1.6));  // bridge girder
    for(const sy of [-1,1]) box(out, x - 0.48, x + 0.48, sy*hw - 0.22, sy*hw + 0.22, z + 0.04, z + 0.30, 'steel', 0.1);
    const ty = p.trolleyY != null ? p.trolleyY : 0;
    box(out, x - 0.44, x + 0.44, ty - 0.42, ty + 0.42, z + 0.86, z + 1.30, 'steel', 0.22);              // trolley
    const hookZ = p.hookZ != null ? p.hookZ : z - 2.4;
    for(const sx of [-0.22, 0.22]) tube(out, [x + sx, ty, z + 0.86], [x + sx, ty, hookZ + 0.42], 0.022, 5, 'iron', 0.1, null, true);
    box(out, x - 0.26, x + 0.26, ty - 0.16, ty + 0.16, hookZ + 0.12, hookZ + 0.48, 'iron', 0.15);       // hook block
    tube(out, [x, ty, hookZ - 0.16], [x, ty, hookZ + 0.14], 0.07, 6, 'galv', 0.2, null, true);
    return { hookZ, x };
  }
  // --- YARD OFFICE: small, and it reads as an office, not a net shed ---
  function office(out, p, s){
    const L = p.L || 8.0, W = p.W || 5.4, eave = p.eave || (p.storeys === 2 ? 5.4 : 2.9);
    const pitch = 26*DEG, hw = W/2, hl = L/2, ridge = eave + hw*Math.tan(pitch);
    const body = p.body || 'white', tex = p.siding === 'shingle' ? gravelTex() : plankTex(0.19);
    for(const sy of [-1,1]){ const y = sy*hw;
      if(sy>0) wall(out, -hl, y, hl, y, 0, eave, body, tex, 0.12); else wall(out, hl, y, -hl, y, 0, eave, body, tex, -0.44); }
    for(const sx of [-1,1]){ const x = sx*hl;
      const a = [[x,-hw,0],[x,hw,0],[x,hw,eave],[x,0,ridge],[x,-hw,eave]];
      out.push(F(sx>0?a:a.slice().reverse(), body, sx>0?0.05:-0.5, 0, a.map(v=>[v[1],v[2]]), tex)); }
    for(const sy of [-1,1]){ const ov = 0.34;
      const a = [[-hl-ov, sy*(hw+ov), eave - ov*Math.tan(pitch)],[hl+ov, sy*(hw+ov), eave - ov*Math.tan(pitch)],[hl+ov, 0, ridge],[-hl-ov, 0, ridge]];
      out.push(F(sy>0?a:a.slice().reverse(), p.roof || 'shingleR', sy>0?0.36:-0.22, 0, [[0,0],[L,0],[L,hw/Math.cos(pitch)],[0,hw/Math.cos(pitch)]], seamTex(0.42))); }
    box(out, -hl-0.34, hl+0.34, -0.09, 0.09, ridge - 0.03, ridge + 0.10, 'wood', 0.35, sawnTex());
    // door + windows on the +Y face, a sign board over the door
    const dx = -L*0.24;
    box(out, dx - 0.50, dx + 0.50, hw - 0.04, hw + 0.05, 0, 2.05, 'wood', -0.3, grainTex(0.18));
    out.push(F([[dx-0.42, hw+0.06, 0.12],[dx+0.42, hw+0.06, 0.12],[dx+0.42, hw+0.06, 1.95],[dx-0.42, hw+0.06, 1.95]], 'glass', -0.1, 0.03, [[0,0],[0.84,0],[0.84,1.83],[0,1.83]], paneTex(0.42,0.9)));
    const nW = p.storeys === 2 ? 3 : 2;
    for(let i=0;i<nW;i++){ const x = L*0.06 + i*Math.min(2.0, (L*0.42)/nW);
      for(const zz of p.storeys === 2 ? [1.05, 3.6] : [1.05]){
        box(out, x - 0.62, x + 0.62, hw - 0.02, hw + 0.07, zz - 0.08, zz + 1.30, 'trim', 0.25, null, true);
        out.push(F([[x-0.55, hw+0.08, zz],[x+0.55, hw+0.08, zz],[x+0.55, hw+0.08, zz+1.16],[x-0.55, hw+0.08, zz+1.16]], 'glass', 0.0, 0.03, [[0,0],[1.1,0],[1.1,1.16],[0,1.16]], paneTex(0.55,0.58)));
      } }
    if(p.sign !== false){
      box(out, dx - 1.5, dx + 1.5, hw + 0.05, hw + 0.14, 2.22, 3.0, 'wood', 0.3, sawnTex());
      decalZ(out, 2.61, dx - 1.2, dx + 1.2, hw + 0.14, hw + 0.15, 'trim', 0.4, null, true, 0.04);
    }
    box(out, -hl - 1.1, -hl + 0.2, -hw*0.5, hw*0.5, 0, 0.16, 'conc', 0.15, formTex(0.4));   // step
    pipe(out, hl*0.55, -hw*0.5, 0.09, eave, ridge + 0.9, 'iron', 0.1, 8);
    return { L, W, eave, ridge };
  }
  // --- WET BASIN: walls, floor, gate, and the roof over it. wet:false pumps it out. ---
  function basin(out, p, s, T){
    const hl = p.L/2, hw = p.W/2, floor = p.floorZ, cope = p.copeZ;
    // floor
    slab(out, [[-hl,-hw],[hl,-hw],[hl,hw],[-hl,hw]], floor, matAtZ(floor,'conc',T,s), -0.35, formTex(1.6));
    // four walls, banded through the tide
    bandedWall(out, -hl,-hw, hl,-hw, floor, cope, 'conc', T, s, -0.42, formTex(0.9));
    bandedWall(out, hl,hw, -hl,hw, floor, cope, 'conc', T, s, 0.14, formTex(0.9));
    bandedWall(out, hl,-hw, hl,hw, floor, cope, 'conc', T, s, 0.06, formTex(0.9));
    bandedWall(out, -hl,hw, -hl,-hw, floor, cope, 'conc', T, s, -0.30, formTex(0.9));
    if(!p.wet && p.altars !== false){                                     // graving-dock steps, dry only
      for(const sy of [-1,1]){ let z = floor + 0.9, inset = 0.9;
        while(z < cope - 1.2){
          box(out, -hl + 0.2, hl - 0.2, sy*(hw - inset) - 0.45, sy*(hw - inset) + 0.45, z - 0.9, z, matAtZ(z,'conc',T,s), sy>0?0.16:-0.3, formTex(0.7));
          z += 1.5; inset += 0.9;
        } }
    }
    // coping + curb all round
    for(const [x0,y0,x1,y1,b] of [[-hl,-hw,hl,-hw,-0.2],[hl,hw,-hl,hw,0.2],[hl,-hw,hl,hw,0.05],[-hl,hw,-hl,-hw,-0.1]]){
      const dx = Math.sign(x1-x0) || 0, dy = Math.sign(y1-y0) || 0;
      const nx = -dy, ny = dx;
      const w = 1.1;
      out.push(F([[x0,y0,cope],[x1,y1,cope],[x1 + nx*w, y1 + ny*w, cope],[x0 + nx*w, y0 + ny*w, cope]], matAtZ(cope,'conc',T,s), 0.2 + b, 0,
        [[0,0],[Math.hypot(x1-x0,y1-y0),0],[Math.hypot(x1-x0,y1-y0),w],[0,w]], formTex(1.2)));
    }
    // the gate at the +X (seaward) end: a steel caisson with a walkway on top
    if(p.gate !== false){
      const gz = p.gateTopZ != null ? p.gateTopZ : cope - 0.35;
      const closed = p.gateClosed !== false;
      const gx = closed ? hl - 0.9 : hl - 0.9;
      const gw = closed ? hw : hw*0.30;
      const yOff = closed ? 0 : hw*0.72;
      for(const sy of closed ? [0] : [-1,1]){
        const ya = sy === 0 ? -hw : (sy>0 ? yOff - gw : -yOff - gw), yb = sy === 0 ? hw : (sy>0 ? yOff + gw : -yOff + gw);
        for(const g of zSplit(floor + 0.1, gz, 'rust', T, s))
          box(out, gx - 0.85, gx + 0.85, ya, yb, g.a, g.b, g.mat, 0.05, ribTex(0.75), true);
        box(out, gx - 1.05, gx + 1.05, ya, yb, gz, gz + 0.14, matAtZ(gz,'steel',T,s), 0.3, seamTex(1.3));
        for(let y = ya + 1.4; y < yb - 0.8; y += 2.6){                     // handrail across the gate top
          pipe(out, gx, y, 0.035, gz + 0.14, gz + 1.1, 'galv', 0.1, 6);
        }
        for(const zz of [gz + 0.62, gz + 1.06]) tube(out, [gx, ya + 0.6, zz], [gx, yb - 0.6, zz], 0.028, 5, 'galv', 0.15, null, true);
      }
    }
    // capstans / bollards on the coping
    for(let x = -hl + 4; x < hl - 3; x += 9){ for(const sy of [-1,1]){
      pipe(out, x, sy*(hw + 0.55), 0.19, cope, cope + 0.78, 'iron', 0.05, 8);
      torusZ(out, x, sy*(hw + 0.55), cope + 0.82, 0.17, 0.06, 8, 5, 'iron', 0.25);
    } }
  }
  // --- BASIN ROOF: portal frames striding the basin, corrugated roof, open sides ---
  function basinRoof(out, p, s){
    const cx0 = p.cx || 0, hl = p.L/2, span = p.span, hs = span/2, eave = p.eaveZ, pitch = (p.pitch || 11)*DEG;
    const ridge = eave + hs*Math.tan(pitch), cr = Math.max(0.24, span*0.010), roofMat = p.roof || 'rust';
    const n = Math.max(3, Math.round(p.L/ (p.bay || 9)));
    for(let i=0;i<=n;i++){ const x = cx0 - hl + p.L*(i/n);
      for(const sy of [-1,1]){ const y = sy*hs;
        box(out, x - cr, x + cr, y - cr, y + cr, p.baseZ, eave, 'steel', 0.05, seamTex(1.2), true);
        beam(out, [x, y, eave - span*0.13], [x, y - sy*span*0.13, eave], cr*0.6, 'steel', -0.2, 4);
      }
      // rafters up to the ridge
      for(const sy of [-1,1]) beam(out, [x, sy*hs, eave], [x, 0, ridge], cr*0.8, 'steel', sy>0?0.1:-0.3, 4);
    }
    for(const sy of [-1,1]){                                              // roof planes + eave gutter
      const ov = 0.5, ez = eave - ov*Math.tan(pitch);
      const a = [[cx0-hl-ov, sy*(hs+ov), ez],[cx0+hl+ov, sy*(hs+ov), ez],[cx0+hl+ov, 0, ridge],[cx0-hl-ov, 0, ridge]];
      out.push(F(sy>0?a:a.slice().reverse(), roofMat, sy>0?0.40:-0.20, 0,
        [[0,0],[p.L,0],[p.L, hs/Math.cos(pitch)],[0, hs/Math.cos(pitch)]], corrTex(0.34)));
      box(out, cx0-hl-ov, cx0+hl+ov, sy*(hs+ov) - 0.15, sy*(hs+ov) + 0.15, ez - 0.34, ez, 'galv', 0.12, seamTex(1.6));
    }
    box(out, cx0-hl-0.5, cx0+hl+0.5, -0.14, 0.14, ridge - 0.05, ridge + 0.16, 'galv', 0.42, seamTex(1.4));
    // gable ends: clad down to the coping, with a full-height opening at the seaward end
    for(const sx of [-1,1]){ const x = cx0 + sx*hl, open = sx > 0 && p.doorSpan;
      const a = [[x,-hs,eave],[x,hs,eave],[x,0,ridge]];
      out.push(F(sx>0?a:a.slice().reverse(), 'corr', sx>0?0.1:-0.45, 0, [[0,0],[span,0],[span/2,ridge-eave]], corrTex(0.28)));
      if(open){ const dw = Math.min(p.doorSpan.w, span - 3)/2, dz = Math.min(p.doorSpan.h, eave - p.baseZ - 1.0);
        const b = [[x,-hs,p.baseZ],[x,-dw,p.baseZ],[x,-dw,p.baseZ+dz],[x,dw,p.baseZ+dz],[x,dw,p.baseZ],[x,hs,p.baseZ],[x,hs,eave],[x,-hs,eave]];
        out.push(F(sx>0?b:b.slice().reverse(), 'corr', sx>0?0.05:-0.5, 0, b.map(v=>[v[1],v[2]]), corrTex(0.28)));
        out.push(F([[x - sx*0.45, -dw, p.baseZ],[x - sx*0.45, dw, p.baseZ],[x - sx*0.45, dw, p.baseZ+dz],[x - sx*0.45, -dw, p.baseZ+dz]], 'shade', -1.3, 0.02));
        for(const sy of [-1,1]) box(out, x - sx*0.5, x + sx*0.1, sy*dw, sy*dw + sy*0.4, p.baseZ, p.baseZ + dz + 0.5, 'steel', 0.1);
      } else {
        const b = [[x,-hs,p.baseZ],[x,hs,p.baseZ],[x,hs,eave],[x,-hs,eave]];
        out.push(F(sx>0?b:b.slice().reverse(), 'corr', sx>0?0.05:-0.5, 0, [[0,0],[span,0],[span,eave-p.baseZ],[0,eave-p.baseZ]], corrTex(0.28)));
      }
    }
    if(p.crane !== false){                                                // travelling jib on the eave rail
      for(const sy of [-1,1]) box(out, cx0-hl + 1, cx0+hl - 1, sy*hs - 0.3, sy*hs + 0.3, eave - 1.5, eave - 1.15, 'rust', 0.1, seamTex(1.0));
      const cx = p.craneX != null ? p.craneX : cx0 - hl*0.28;
      box(out, cx - 0.5, cx + 0.5, -hs, hs, eave - 1.15, eave - 0.35, 'yel', 0.2, seamTex(1.5));
      const ty = p.craneY != null ? p.craneY : hs*0.35;
      box(out, cx - 0.6, cx + 0.6, ty - 0.5, ty + 0.5, eave - 0.35, eave + 0.25, 'steel', 0.25);
      const hz = p.craneHookZ != null ? p.craneHookZ : eave - 9;
      for(const sx of [-0.26,0.26]) tube(out, [cx + sx, ty, eave - 0.35], [cx + sx, ty, hz], 0.03, 5, 'iron', 0.1, null, true);
      box(out, cx - 0.3, cx + 0.3, ty - 0.2, ty + 0.2, hz - 0.45, hz, 'iron', 0.15);
    }
  }
  // --- JIB CRANE: a fixed slewing crane on the quay, for the industrial yard ---
  function jibCrane(out, p, s){
    const h = p.h || 14, reach = p.reach || 12, ang = p.slew || 0.6;
    box(out, -1.1, 1.1, -1.1, 1.1, 0, 0.7, 'conc', 0.1, formTex(0.5));
    tube(out, [0,0,0.7], [0,0,h*0.55], 0.62, 8, 'yel', 0.05, seamTex(1.6), true);
    box(out, -1.5, 1.5, -1.6, 1.6, h*0.55, h*0.55 + 2.2, 'yel', 0.15, seamTex(1.4));   // machinery house
    const jx = Math.cos(ang)*reach, jy = Math.sin(ang)*reach, jz = h;
    beam(out, [0, 0, h*0.55 + 2.0], [jx, jy, jz], 0.34, 'yel', 0.2, 4);                 // jib
    beam(out, [0, 0, h*0.55 + 2.0], [-Math.cos(ang)*reach*0.28, -Math.sin(ang)*reach*0.28, jz*0.92], 0.28, 'yel', -0.1, 4);
    tube(out, [jx, jy, jz], [jx, jy, p.hookZ != null ? p.hookZ : 3.5], 0.03, 5, 'iron', 0.1, null, true);
    box(out, jx - 0.3, jx + 0.3, jy - 0.2, jy + 0.2, (p.hookZ != null ? p.hookZ : 3.5) - 0.5, (p.hookZ != null ? p.hookZ : 3.5), 'iron', 0.2);
  }

  const PARTS = {
    keelBlocks:  { label:'keel blocks & poppets', kind:'haulout' },
    cradle:      { label:'wheeled cradle',        kind:'haulout' },
    railway:     { label:'marine railway',        kind:'haulout' },
    slipway:     { label:'slipway',               kind:'haulout' },
    davitPair:   { label:'davit pair',            kind:'haulout' },
    travelLift:  { label:'travel-lift gantry',    kind:'haulout' },
    liftDock:    { label:'lift dock piers',       kind:'haulout' },
    gantryCrane: { label:'overhead gantry crane', kind:'machine' },
    jibCrane:    { label:'quay jib crane',        kind:'machine' },
    winchHouse:  { label:'winch house',           kind:'building' },
    shed:        { label:'clear-span workshop',   kind:'building' },
    office:      { label:'yard office',           kind:'building' },
    basin:       { label:'wet basin + gate',      kind:'dock' },
    basinRoof:   { label:'basin roof',            kind:'dock' },
    fenceRun:    { label:'chain-link fence',      kind:'yard' },
    staging:     { label:'staging',               kind:'yard' },
    timberStack: { label:'timber stack',          kind:'yard' },
    sparRack:    { label:'spar rack',             kind:'yard' },
    drums:       { label:'oil drums',             kind:'yard' },
    sawhorse:    { label:'sawhorse',              kind:'yard' },
  };
  // standalone part bakes: each part on its own patch of ground, sized to the hull it serves
  const PART_BUILD = {
    keelBlocks: (out,s,T,H)=>{ pad(out, -H.loa*0.45, H.loa*0.45, -H.beam*1.1, H.beam*1.1, 0, s.surface, s);
      const r = keelBlocks(out, { hull:H, blockZ:blockH(H) }, s);
      s._mounts.push(mount('blocks', 0, 0, r.keelZ, 0, H, 'keel blocks & poppets')); },
    cradle: (out,s,T,H)=>{ pad(out, -H.loa*0.45, H.loa*0.45, -H.beam*1.1, H.beam*1.1, 0, s.surface, s);
      group(out, 0,0, Math.PI/2, (g)=> railway(g, { gauge:gauge(H), run:H.loa*0.86, y0:-H.loa*0.43, zAt:()=>0.14 }, s, T));
      const r = cradle(out, { hull:H, deckZ:0.55, gauge:gauge(H), steel:H.loa > 16 }, s);
      s._mounts.push(mount('cradle', 0, 0, r.keelZ, 0, H, 'wheeled cradle')); },
    davitPair: (out,s,T,H)=>{ pad(out, -H.loa*0.45, H.loa*0.45, -H.beam*1.4, H.beam*1.4, 0, s.surface, s);
      const slingZ = Math.max(1.2, H.draft + 0.9);
      davitPair(out, { hull:H, slingZ, style: H.loa > 14 ? 'steel' : 'timber' }, s);
      s._mounts.push(mount('davit', 0, 0, slingZ, 0, H, 'slung in davit falls')); },
    travelLift: (out,s,T,H)=>{ pad(out, -H.loa*0.5, H.loa*0.5, -H.beam*1.5, H.beam*1.5, 0, s.surface, s);
      const slingZ = 2.1; travelLift(out, { hull:H, slingZ }, s);
      s._mounts.push(mount('slings', 0, 0, slingZ, 0, H, 'travel-lift slings')); },
    railway: (out,s,T,H)=>{ pad(out, -6, 6, -H.loa*0.5, H.loa*0.2, 0, s.surface, s);
      group(out, 0, -H.loa*0.5 + 1, 0, (g)=> winchHouse(g, {}, s));
      railway(out, { gauge:gauge(H), run:H.loa*0.9, y0:-H.loa*0.42, zAt:()=>0.12 }, s, T);
      const r = cradle(out, { hull:H, deckZ:0.55, gauge:gauge(H), steel:H.loa > 16 }, s);
      s._mounts.push(mount('cradle', 0, 0, r.keelZ, 90, H, 'railway cradle')); },
    slipway: (out,s,T,H)=>{ const w = H.beam + 3.4;
      pad(out, -w, w, -8, 0, s.gradeZ, s.surface, s);
      slipway(out, { width:w, run:Math.max(10, H.draft*7), crestZ:s.gradeZ, toeZ:-Math.max(1.0, H.draft*0.9), gauge:gauge(H), deck:'conc' }, s, T);
      s._mounts.push(mount('cradle', 0, 3, s.gradeZ - 0.4, 90, H, 'on the ways')); },
    shed: (out,s,T,H)=>{ const L = H.loa*1.25 + 6, W = H.beam*1.8 + 5, e = H.depth + 3.2;
      pad(out, -L*0.62, L*0.62, -W*0.75, W*0.75, 0, s.surface, s);
      shed(out, { L, W, eave:e, hull:H, gantry:true, monitor:true, hookZ:e - 3.5, doorEnd:1, doorW:Math.min(W*0.66, H.beam + 2.6), roof:'rust' }, s);
      s._mounts.push(mount('blocks', -1.5, 0, blockH(H), 0, H, 'inside the workshop')); },
    office: (out,s,T,H)=>{ pad(out, -8, 8, -6, 6, 0, s.surface, s); office(out, { storeys:1 }, s); },
    winchHouse: (out,s,T,H)=>{ pad(out, -5, 5, -4, 4, 0, s.surface, s); winchHouse(out, {}, s); },
    liftDock: (out,s,T,H)=>{ liftDock(out, { slot:H.beam + 3.0, run:H.loa*0.8, width:4.5, deckZ:s.gradeZ }, s, T);
      s._mounts.push(Object.assign(mount('afloat', 0, 0, s.tide, 90, H, 'in the lift slot'), { waterlineZ:+s.tide.toFixed(2) })); },
    gantryCrane: (out,s,T,H)=>{ pad(out, -H.loa*0.5, H.loa*0.5, -H.beam*1.2, H.beam*1.2, 0, s.surface, s);
      gantryCrane(out, { L:H.loa*0.9, W:H.beam*2.0, railZ:H.depth + 3.0, hookZ:blockH(H) + H.depth*0.6 }, s); },
    jibCrane: (out,s,T,H)=>{ pad(out, -10, 10, -10, 10, 0, 'conc', s); jibCrane(out, { h:16, reach:13, hookZ:4 }, s); },
    basin: (out,s,T,H)=>{ const L = H.loa*1.18, W = H.beam*1.9;
      pad(out, -L*0.62, L*0.62, -W*1.1, W*1.1, s.gradeZ, 'conc', s);
      basin(out, { L, W, floorZ:-(H.draft + 1.6), copeZ:s.gradeZ, wet:s.wet !== false, gateClosed:s.gateClosed !== false }, s, T);
      s._mounts.push(s.wet !== false ? Object.assign(mount('afloat', 0, 0, s.tide, 0, H, 'afloat in the basin'), { waterlineZ:+s.tide.toFixed(2) })
        : mount('blocks', 0, 0, -(H.draft + 1.6) + blockH(H), 0, H, 'blocked on the dock floor')); },
    basinRoof: (out,s,T,H)=>{ const L = H.loa*1.18, W = H.beam*1.9;
      basinRoof(out, { L, span:W + 12, eaveZ:s.gradeZ + H.depth + 9, baseZ:s.gradeZ, bay:9 }, s); },
    fenceRun: (out,s,T,H)=>{ pad(out, -12, 12, -4, 4, 0, s.surface, s); fenceRun(out, -11, 0, 11, 0, s, { gate:true, gateW:6 }); },
    staging: (out,s,T,H)=>{ pad(out, -H.loa*0.4, H.loa*0.4, -3, 3, 0, s.surface, s); staging(out, { len:Math.min(H.loa*0.7, 14), h:Math.max(3, H.depth + 1.2) }, s); },
    timberStack: (out,s,T,H)=>{ pad(out, -4, 4, -3, 3, 0, s.surface, s); timberStack(out, {}, s); },
    sparRack: (out,s,T,H)=>{ pad(out, -6, 6, -3, 3, 0, s.surface, s); sparRack(out, {}, s); },
    drums: (out,s,T,H)=>{ pad(out, -3, 3, -3, 3, 0, s.surface, s); drums(out, { n:4 }, s); },
    sawhorse: (out,s,T,H)=>{ pad(out, -2.5, 2.5, -2.5, 2.5, 0, s.surface, s); sawhorse(out, {}, s); },
  };
  const blockH = (H)=> H.loa < 8 ? 0.55 : H.loa < 14 ? 1.05 : H.loa < 22 ? 1.35 : H.loa < 50 ? 1.85 : 2.30;
  const gauge  = (H)=> Math.max(1.5, Math.min(H.beam*0.72, 9.0));
  function mount(support, x, y, keelZ, ang, H, note){
    return { id:'mount', support, cls:H.id, label:H.label, loa:H.loa, beam:H.beam, draft:H.draft, depth:H.depth,
      x:+x.toFixed(2), y:+y.toFixed(2), keelZ:+keelZ.toFixed(2), heading:ang, note };
  }

  // ============================ SITES ========================================================
  // A yard is sized BY ITS BOAT. Each site returns a plan in metres; build() walks the plan and
  // calls the parts. Water is at +Y, the road / back fence at -Y — same axis convention as the
  // wharf rig, so a yard and a wharf module can sit on the same shoreline.
  const SITES = {
    backyardSlip: {
      label:'backyard slip', tier:1, hull:'dory', surface:'grass', edge:'beach', freeboard:0.35,
      blurb:'One punt, a pair of timber davits and a set of ways run down the grass. No fence, no yard — a shed door and a beach.',
      plan(H, s){
        const W = 17, D = 14, gz = s.gradeZ, edgeY = D/2 - 3.4;
        const toe = -(H.draft*0.9 + 0.5), run = (gz - toe)*3.0;
        return { W, D, gradeZ:gz, surface:'grass', patch:{ x0:-6.0, x1:1.2, y0:-4.6, y1:2.4, surf:'gravel' },
          edge:'beach', edgeY,
          slip:{ x:4.4, width:2.6, run, overhang:run*0.22, crestZ:gz, toeZ:toe, gauge:1.6, deck:'timber', rails:false },
          cells:[
            { kind:'davit',  hull:'dory', x:-2.8, y:0.6, ang:0 },
            { kind:'blocks', hull:'punt', x:-3.4, y:-3.4, ang:8 },
          ],
          buildings:[ { kind:'shed', x:5.4, y:-4.2, ang:0, L:5.0, W:3.4, eave:2.5, body:'board', roof:'rust',
                        doorEnd:1, doorW:2.2, doorH:2.1, monitor:false, stack:false, window:false } ],
          props:[ { kind:'sawhorse', x:0.2, y:-1.9, ang:0.4 }, { kind:'sawhorse', x:1.5, y:-1.1, ang:0.1 },
                  { kind:'timberStack', x:-7.0, y:-1.0, ang:0.2, L:3.0, W:0.9, layers:4 },
                  { kind:'drums', x:-6.8, y:-4.6, n:2, seed:2 } ],
          fence:null };
      } },
    smallYard: {
      label:'small land yard', tier:2, hull:'lobster', surface:'gravel', edge:'beach', freeboard:0.9,
      blurb:'Fully land based. Gravel hardstand, an office, one shed and a marine railway with a cradle — the lobster-boat yard.',
      plan(H, s){
        const W = 30, D = 24, gz = s.gradeZ, edgeY = D/2 - 4.0;
        const toe = -(H.draft*0.9 + 0.5), run = (gz - toe)*3.2;
        return { W, D, gradeZ:gz, surface:'gravel', edge:'beach', edgeY,
          slip:{ x:7.5, width:H.beam + 2.2, run, overhang:run*0.22, crestZ:gz, toeZ:toe, gauge:gauge(H), deck:'conc', rails:true },
          railway:{ x:7.5, y0:-9.8, run:6.9, gauge:gauge(H), zAt:()=> gz + 0.14 },
          winch:{ x:7.5, y:-10.4, ang:0 },
          cells:[
            { kind:'cradle', hull:'lobster', x:7.5, y:-3.4, ang:90 },
            { kind:'davit',  hull:'lobster', x:-5.5, y:2.2, ang:0, staging:true },
            { kind:'blocks', hull:'skiff',   x:-10.5, y:-4.6, ang:6 },
          ],
          buildings:[
            { kind:'shed',   x:-6.5, y:-7.8, ang:0, L:13.0, W:6.6, eave:4.4, body:'corr', roof:'rust', doorEnd:1, doorW:5.0, monitor:false, gantry:false },
            { kind:'office', x:12.0, y:-8.0, ang:0, L:6.5, W:4.6, storeys:1, body:'white', siding:'clap' },
          ],
          props:[ { kind:'timberStack', x:-13.5, y:-0.6, ang:0.05, L:4.5, W:1.2, layers:7 },
                  { kind:'sparRack', x:-13.0, y:4.2, ang:0.02, L:7.5, seed:4 },
                  { kind:'drums', x:1.4, y:-8.6, n:3, seed:6 },
                  { kind:'sawhorse', x:-1.6, y:-4.2, ang:0.5 }, { kind:'sawhorse', x:-0.4, y:-3.2, ang:1.1 } ],
          fence:{ y:-D/2 + 1.0, gate:true, gateW:6.5 } };
      } },
    workingYard: {
      label:'working yard', tier:3, hull:'dragger', surface:'gravel', edge:'quay', freeboard:1.2,
      blurb:'The dragger yard: concrete apron over gravel, heavy railway and cradle, davits and staging, a proper workshop and a fenced road gate.',
      plan(H, s){
        const W = 48, D = 36, gz = s.gradeZ, edgeY = D/2 - 3.0;
        const toe = -(H.draft*0.9 + 0.6), run = (gz - toe)*3.2;
        return { W, D, gradeZ:gz, surface:'gravel', patch:{ x0:-14, x1:19, y0:-6.5, y1:13, surf:'conc' },
          edge:'quay', edgeY,
          slip:{ x:13.0, width:H.beam + 3.0, run, overhang:run*0.22, crestZ:gz, toeZ:toe, gauge:gauge(H), deck:'conc', rails:true },
          railway:{ x:13.0, y0:-13.5, run:14.9, gauge:gauge(H), zAt:()=> gz + 0.16 },
          winch:{ x:13.0, y:-14.2, ang:0 },
          cells:[
            { kind:'cradle', hull:'dragger', x:13.0, y:-4.5, ang:90, steel:true },
            { kind:'davit',  hull:'cape',    x:-6.0, y:6.2, ang:0, style:'steel', staging:true },
            { kind:'blocks', hull:'lobster', x:-5.0, y:-2.4, ang:0, staging:true },
            { kind:'blocks', hull:'skiff',   x:-18.0, y:-7.0, ang:15 },
          ],
          buildings:[
            { kind:'shed',   x:-9.0, y:-11.6, ang:0, L:20.0, W:9.0, eave:6.2, body:'corr', roof:'rust', doorEnd:1, doorW:6.8, monitor:true, gantry:true, hookZ:3.4 },
            { kind:'office', x:5.5,  y:-13.0, ang:0, L:8.0, W:5.4, storeys:1, body:'red', siding:'clap' },
            { kind:'shed',   x:20.5, y:-7.0, ang:Math.PI/2, L:8.0, W:5.0, eave:3.2, body:'board', roof:'rust', doorEnd:1, doorW:3.0, doorH:2.8, monitor:false, stack:false },
          ],
          props:[ { kind:'timberStack', x:-20.5, y:-1.5, ang:0.03, L:5.5, W:1.3, layers:8 },
                  { kind:'timberStack', x:-20.5, y:-4.6, ang:0.03, L:4.0, W:1.1, layers:5 },
                  { kind:'sparRack', x:-20.0, y:3.4, ang:0.0, L:9.0, seed:8 },
                  { kind:'drums', x:2.5, y:-8.0, n:4, seed:9 }, { kind:'drums', x:-14.5, y:8.5, n:3, seed:11 },
                  { kind:'sawhorse', x:-1.2, y:1.6, ang:0.3 }, { kind:'sawhorse', x:0.2, y:0.6, ang:0.9 } ],
          fence:{ y:-D/2 + 1.2, gate:true, gateW:8.0 } };
      } },
    largeYard: {
      label:'large yard', tier:4, hull:'trawler', surface:'conc', edge:'quay', freeboard:1.4,
      blurb:'Sized for the stern trawler Mk II: a clear-span workshop that swallows the whole ship, a travel-lift walking out over the dock slot, and blocked hulls on the apron.',
      plan(H, s){
        const W = 80, D = 56, gz = s.gradeZ, edgeY = D/2 - 4.0;
        return { W, D, gradeZ:gz, surface:'conc', edge:'quay', edgeY,
          liftDock:{ x:19.0, slot:H.beam + 3.2, run:26.0, width:5.0, deckZ:gz },
          cells:[
            { kind:'slings', hull:'trawler', x:19.0, y:edgeY - 6.0, ang:90 },
            { kind:'shopBlocks', hull:'trawler', x:-15.0, y:-7.0, ang:0, staging:true },
            { kind:'blocks', hull:'dragger', x:15.0, y:-13.0, ang:0, staging:true },
            { kind:'blocks', hull:'cape',    x:31.0, y:-5.0, ang:0 },
            { kind:'davit',  hull:'lobster', x:32.0, y:6.0, ang:0, style:'steel' },
          ],
          buildings:[
            { kind:'shed',   x:-15.0, y:-7.0, ang:0, L:H.loa + 9, W:H.beam*2.2 + 3, eave:H.depth + 7.5, body:'corr', roof:'rust',
              doorEnd:1, doorW:H.beam + 3.2, doorH:H.depth + 5.5, monitor:true, gantry:true, hookZ:H.depth + 3.0 },
            { kind:'office', x:27.0, y:-21.0, ang:0, L:11.0, W:7.0, storeys:2, body:'white', siding:'clap' },
            { kind:'shed',   x:4.0,  y:-22.0, ang:0, L:14.0, W:8.0, eave:4.4, body:'corr', roof:'rust', doorEnd:1, doorW:5.0, monitor:false },
          ],
          props:[ { kind:'timberStack', x:-36.0, y:9.0, ang:0.02, L:6.0, W:1.4, layers:9 },
                  { kind:'timberStack', x:-36.0, y:5.0, ang:0.02, L:5.0, W:1.2, layers:6 },
                  { kind:'sparRack', x:-35.0, y:14.0, ang:0, L:11.0, seed:14 },
                  { kind:'drums', x:-2.0, y:-15.0, n:5, seed:15 }, { kind:'drums', x:26.0, y:-11.0, n:3, seed:16 },
                  { kind:'jib', x:35.0, y:18.0, h:15, reach:12, slew:2.3, hookZ:3 } ],
          fence:{ y:-D/2 + 1.4, gate:true, gateW:10.0 } };
      } },
    industrialYard: {
      label:'industrial wet yard', tier:5, hull:'tanker', surface:'conc', edge:'quay', wet:true, freeboard:1.6,
      blurb:'The covered dock. A gated basin cut back from the shore, long enough for the tanker and roofed on portal frames with the travelling crane on the eave rails. Pump it out and the same dock is a graving dock on blocks.',
      plan(H, s){
        const W = 78, D = 156, gz = s.gradeZ, edgeY = D/2 - 8.0;
        const bL = H.loa*1.12, bW = H.beam*1.62, floor = -(H.draft + 2.0);
        return { W, D, gradeZ:gz, surface:'conc', edge:'quay', edgeY,
          basin:{ x:0, y:4.0, ang:Math.PI/2, L:bL, W:bW, floorZ:floor, copeZ:gz, wet:s.wet, gateClosed:s.gateClosed !== false,
                  roof:{ L:bL*0.54, cx:-bL*0.21, span:bW + 12, eaveZ:gz + 16.5, bay:9, craneY:bW*0.18, craneHookZ:gz + 4,
                         doorSpan:{ w:bW - 2, h:14.5 } } },
          cells:[
            { kind:'basinBerth', hull:'tanker',  x:0, y:4.0, ang:90 },
            { kind:'blocks',     hull:'trawler', x:27.0, y:-28.0, ang:90, staging:true },
            { kind:'blocks',     hull:'dragger', x:-27.0, y:-30.0, ang:90 },
          ],
          buildings:[
            { kind:'shed',   x:28.0,  y:14.0, ang:Math.PI/2, L:40.0, W:16.0, eave:11.0, body:'corr', roof:'rust', doorEnd:1, doorW:10.0, monitor:true, gantry:true, hookZ:6.5 },
            { kind:'shed',   x:-28.0, y:6.0,  ang:Math.PI/2, L:26.0, W:12.0, eave:7.0,  body:'corr', roof:'rust', doorEnd:-1, doorW:7.0, monitor:false },
            { kind:'office', x:-26.0, y:-56.0, ang:0, L:14.0, W:8.0, storeys:2, body:'white', siding:'clap' },
          ],
          props:[ { kind:'jib', x:19.0, y:58.0, h:18, reach:15, slew:2.4, hookZ:4 },
                  { kind:'jib', x:-19.0, y:58.0, h:18, reach:15, slew:0.7, hookZ:4 },
                  { kind:'timberStack', x:30.0, y:-56.0, ang:0, L:7.0, W:1.6, layers:10 },
                  { kind:'timberStack', x:30.0, y:-50.0, ang:0, L:6.0, W:1.4, layers:7 },
                  { kind:'drums', x:8.0, y:-58.0, n:6, seed:21 }, { kind:'drums', x:-34.0, y:-20.0, n:4, seed:22 } ],
          fence:{ y:-D/2 + 2.0, gate:true, gateW:12.0 } };
      } },
  };

  // ---- assemble a site plan into faces, collecting hull mounts as it goes ----
  function buildSite(out, key, s, T){
    const site = SITES[key], H = ship(s.hull || site.hull), P = site.plan(H, s);
    s._plan = P;
    const hw = P.W/2, hd = P.D/2, gz = P.gradeZ, edgeY = P.edgeY != null ? P.edgeY : hd;
    const holes = [];
    if(P.slip) holes.push({ x0:P.slip.x - P.slip.width/2 - 0.02, x1:P.slip.x + P.slip.width/2 + 0.02,
                            y0:edgeY + (P.slip.overhang || 0) - P.slip.run, y1:edgeY + 1 });
    if(P.basin){ const across = Math.abs(Math.cos(P.basin.ang || 0)) > 0.5;
      const bl = across ? P.basin.L : P.basin.W, bw = across ? P.basin.W : P.basin.L;
      holes.push({ x0:P.basin.x - bl/2, x1:P.basin.x + bl/2, y0:P.basin.y - bw/2, y1:P.basin.y + bw/2 }); }
    if(P.liftDock) holes.push({ x0:P.liftDock.x - P.liftDock.slot/2, x1:P.liftDock.x + P.liftDock.slot/2,
                                y0:edgeY - 3.0, y1:edgeY + 1 });
    const notch = holes[0];
    pad(out, -hw, hw, -hd, edgeY, gz, s.surface || P.surface, s, true, holes);
    if(P.patch) pad(out, P.patch.x0, P.patch.x1, P.patch.y0, Math.min(P.patch.y1, edgeY), gz + 0.02, P.patch.surf, s, false, holes);
    litter(out, -hw + 1, hw - 1, -hd + 1, edgeY - 1, gz + 0.02, s, 0.012, 3);
    shoreEdge(out, -hw, hw, edgeY, gz, P.edge, s, T, notch);

    if(P.slip) group(out, P.slip.x, edgeY + (P.slip.overhang || 0) - P.slip.run, 0, (g)=> slipway(g, P.slip, s, T));
    if(P.railway) railway(out, { gauge:P.railway.gauge, run:P.railway.run, y0:P.railway.y0, zAt:P.railway.zAt, sleepers:true }, s, T);
    if(P.winch) group(out, P.winch.x, P.winch.y, P.winch.ang || 0, (g)=> winchHouse(g, {}, s), gz);
    if(P.liftDock) group(out, P.liftDock.x, edgeY - 2.0, 0, (g)=> liftDock(g, { slot:P.liftDock.slot, run:P.liftDock.run, width:P.liftDock.width, deckZ:gz }, s, T));
    if(P.basin){
      group(out, P.basin.x, P.basin.y, P.basin.ang || 0, (g)=> basin(g, P.basin, s, T));
      if(P.basin.roof) group(out, P.basin.x, P.basin.y, P.basin.ang || 0, (g)=> basinRoof(g, Object.assign({ L:P.basin.L + 6, baseZ:gz }, P.basin.roof), s));    }
    for(const b of P.buildings || []){
      if(b.kind === 'shed') group(out, b.x, b.y, b.ang || 0, (g)=> shed(g, Object.assign({ hull:H }, b, { L:b.L, W:b.W }), s), gz);
      if(b.kind === 'office') group(out, b.x, b.y, b.ang || 0, (g)=> office(g, b, s), gz);
    }
    for(const c of P.cells || []){
      const CH = ship(c.hull), ang = (c.ang || 0)*DEG;
      if(c.kind === 'blocks' || c.kind === 'shopBlocks'){
        const bz = blockH(CH);
        group(out, c.x, c.y, ang, (g)=> keelBlocks(g, { hull:CH, blockZ:bz, shores:c.kind !== 'shopBlocks' }, s), gz);
        s._mounts.push(Object.assign(mount('blocks', c.x, c.y, gz + bz, c.ang || 0, CH,
          c.kind === 'shopBlocks' ? 'blocked inside the workshop, under the gantry' : 'blocked on the hardstand'), { indoor: c.kind === 'shopBlocks' }));
        if(c.staging) group(out, c.x, c.y + CH.beam*0.62 + 0.5, ang, (g)=> staging(g, { len:CH.loa*0.68, h:bz + CH.depth*0.9 }, s), gz);
      }
      if(c.kind === 'cradle'){
        group(out, c.x, c.y, ang, (g)=>{ const r = cradle(g, { hull:CH, deckZ:0.62, gauge:gauge(CH), steel:c.steel || CH.loa > 16 }, s);
          s._mounts.push(mount('cradle', c.x, c.y, gz + r.keelZ, c.ang || 0, CH, 'on the railway cradle')); }, gz + 0.14);
      }
      if(c.kind === 'davit'){
        const slingZ = Math.max(1.3, CH.draft + 1.0);
        group(out, c.x, c.y, ang, (g)=> davitPair(g, { hull:CH, slingZ, style:c.style || (CH.loa > 14 ? 'steel' : 'timber') }, s), gz);
        s._mounts.push(mount('davit', c.x, c.y, gz + slingZ, c.ang || 0, CH, 'slung in the davit falls'));
        if(c.staging) group(out, c.x, c.y - CH.beam*0.75, ang, (g)=> staging(g, { len:CH.loa*0.6, h:slingZ + CH.depth*0.8 }, s), gz);
      }
      if(c.kind === 'slings'){
        const slingZ = 2.4;
        group(out, c.x, c.y, ang, (g)=> travelLift(g, { hull:CH, slingZ }, s), gz);
        s._mounts.push(mount('slings', c.x, c.y, gz + slingZ, c.ang || 0, CH, 'in the travel-lift slings'));
      }
      if(c.kind === 'basinBerth'){
        const wet = P.basin.wet !== false;
        s._mounts.push(Object.assign(
          wet ? mount('afloat', c.x, c.y, +(T.w - CH.draft).toFixed(2), c.ang || 0, CH, 'afloat under the roof')
              : mount('blocks', c.x, c.y, P.basin.floorZ + blockH(CH), c.ang || 0, CH, 'blocked on the dock floor'),
          { waterlineZ: wet ? +T.w.toFixed(2) : null, indoor:true }));
        if(!wet){ group(out, c.x, c.y, (c.ang||0)*DEG, (g)=> keelBlocks(g, { hull:CH, blockZ:blockH(CH), shores:true }, s), P.basin.floorZ); }
      }
    }
    for(const pr of P.props || []){
      const ang = pr.ang || 0;
      if(pr.kind === 'timberStack') group(out, pr.x, pr.y, ang, (g)=> timberStack(g, pr, s), gz);
      if(pr.kind === 'sparRack')    group(out, pr.x, pr.y, ang, (g)=> sparRack(g, pr, s), gz);
      if(pr.kind === 'drums')       group(out, pr.x, pr.y, ang, (g)=> drums(g, pr, s), gz);
      if(pr.kind === 'sawhorse')    group(out, pr.x, pr.y, ang, (g)=> sawhorse(g, pr, s), gz);
      if(pr.kind === 'jib')         group(out, pr.x, pr.y, 0, (g)=> jibCrane(g, pr, s), gz);
    }
    if(P.fence && s.fence !== false){
      group(out, 0, 0, 0, (g)=>{
        fenceRun(g, -hw + 0.8, P.fence.y, hw - 0.8, P.fence.y, s, { gate:P.fence.gate, gateW:P.fence.gateW });
        for(const sx of [-1,1]) fenceRun(g, sx*(hw - 0.8), P.fence.y, sx*(hw - 0.8), Math.min(edgeY - 4.5, hd - 6), s, { gate:false, barb:true });
      }, gz);
    }
    if(s.ghost) for(const m of s._mounts){                                  // footprint the hull will occupy
      const c = Math.cos(m.heading*DEG), sn = Math.sin(m.heading*DEG), hl = m.loa/2, hb = m.beam/2;
      const pts = [[-hl,-hb],[hl,-hb],[hl,hb],[-hl,hb]].map(([px,py])=>[m.x + px*c - py*sn, m.y + px*sn + py*c]);
      slab(out, pts, m.keelZ + 0.04, 'ghost', 0.4, meshTex(0.55));
    }
    return { plan:P, hull:H };
  }

  // ============================ MATERIALS ====================================================
  function makeMats(s){
    const M = {};
    const add = (k, ramp)=>{ M[k] = { ramp }; M[k+'W'] = { ramp: ramp.map(c => mix(c, '#08161c', 0.40)) }; };
    const wthr = (ramp)=> s.weather > 0 ? ramp.map(c => desat(mix(c, '#6b6558', s.weather*0.10), s.weather*0.20)) : ramp;
    const BODY = { white:WHITEP, red:REDPAINT, grn:GRNPAINT, buff:BUFF, corr:CORR, galv:GALV }[s.shedColour] || CORR;
    const BASES = { pole:wthr(POLE), wood:wthr(WOOD), plank:wthr(PLANK), conc:wthr(CONCRETE), stone:ROCK,
      iron:IRON, galv:GALV, alum:ALUM, rubber:RUBBER, rope:ROPE, net:NET, yel:wthr(YEL),
      barn:BARN, weed:WEED, rust:wthr(RUST), steel:wthr(STEEL), sleeper:wthr(SLEEPER),
      grass:GRASS, gravel:GRAVEL, dirt:DIRT, corr:wthr(BODY), mesh:MESH, tarp:TARP, shade:SHADE,
      glass:GLASS, white:wthr(WHITEP), red:wthr(REDPAINT), grn:wthr(GRNPAINT), buff:wthr(BUFF),
      trim:wthr(WHITEP), shingleR:wthr(PLANK), ghost:['#2b3a3f','#35474d','#40565d','#4c666e','#5a7780','#6a8892'] };
    for(const k in BASES) add(k, BASES[k]);
    for(const k in BASES){ const r = BASES[k];
      add(k+'S', r.map(c => mix(desat(c, 0.34), '#1b241f', 0.42)));
      add(k+'I', r.map(c => mix(desat(c, 0.48), '#c9cbbf', 0.18)));
      add(k+'B', r.map(c => mix(desat(c, 0.42), '#a8a290', 0.46)));
      add(k+'G', r.map(c => mix(desat(c, 0.26), '#3a4020', 0.56)));
    }
    M.body = M.wood;
    return M;
  }

  // ============================ BAKE =========================================================
  function bake(faces, B, MATS, s, T){
    let minX=1e9, minY=1e9, maxX=-1e9, maxY=-1e9;
    const pf = [];
    for(const f of faces){ if(!f.v || f.v.length < 3) continue; const rv = f.v.map(([x,y,z]) => projRaw(x,y,z,B));
      for(const v of rv){ if(v.sx<minX)minX=v.sx; if(v.sx>maxX)maxX=v.sx; if(v.sy<minY)minY=v.sy; if(v.sy>maxY)maxY=v.sy; }
      pf.push({ f, rv }); }
    if(!pf.length) return { data:new Uint8ClampedArray(4), w:1, h:1, px:0, py:0, wet:new Uint8Array(1), dep:new Float32Array([Infinity]) };
    const W = Math.min(MAXDIM, Math.ceil(maxX - minX) + PAD*2 + 1), H = Math.min(MAXDIM, Math.ceil(maxY - minY) + PAD*2 + 1);
    const ox = PAD - minX, oy = PAD - minY, N = W*H;
    const zbuf = new Float32Array(N).fill(Infinity), dep = new Float32Array(N);
    const rbuf = new Array(N).fill(null), ibuf = new Int16Array(N), nbuf = new Array(N).fill(null);
    for(const { f, rv } of pf){
      let n = normal(rv[0], rv[1], rv[2]); let sh = shadeOf(n, B.se, B.ce);
      if(sh < 0 && f.b <= -1) sh = shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce) * 0.9;
      const fidx = sh*GAIN + BIAS + f.b, M = MATS[f.mat] || MATS.body, ramp = M.ramp, tex = f.tex, uv = f.uv, flat = f.flat;
      for(let t=1; t+1<rv.length; t++) fillTri(rv[0], rv[t], rv[t+1], 0, t, t+1);
      function fillTri(a,b,c, ia,ib,ic){
        const minPX = Math.max(0, Math.floor(Math.min(a.sx,b.sx,c.sx) + ox)), maxPX = Math.min(W-1, Math.ceil(Math.max(a.sx,b.sx,c.sx) + ox));
        const minPY = Math.max(0, Math.floor(Math.min(a.sy,b.sy,c.sy) + oy)), maxPY = Math.min(H-1, Math.ceil(Math.max(a.sy,b.sy,c.sy) + oy));
        const ax=a.sx+ox, ay=a.sy+oy, bx=b.sx+ox, by=b.sy+oy, cxx=c.sx+ox, cy2=c.sy+oy;
        const area = (bx-ax)*(cy2-ay) - (cxx-ax)*(by-ay); if(Math.abs(area) < 1e-6) return;
        const ua = uv?uv[ia]:null, ub = uv?uv[ib]:null, uc = uv?uv[ic]:null;
        for(let y=minPY; y<=maxPY; y++) for(let x=minPX; x<=maxPX; x++){
          const px = x+0.5, py = y+0.5;
          const w0 = ((bx-px)*(cy2-py) - (cxx-px)*(by-py))/area, w1 = ((cxx-px)*(ay-py) - (ax-px)*(cy2-py))/area, w2 = 1-w0-w1;
          if(w0 < -0.001 || w1 < -0.001 || w2 < -0.001) continue;
          const d = w0*a.d + w1*b.d + w2*c.d, deff = d - f.db, i = y*W + x;
          if(deff < zbuf[i]){
            let fi = fidx;
            if(tex && uv){ const uu = w0*ua[0] + w1*ub[0] + w2*uc[0], vv = w0*ua[1] + w1*ub[1] + w2*uc[1];
              const tv = tex(uu, vv); if(tv === null) continue;               // HOLE: mesh / open web
              fi += tv; }
            zbuf[i] = deff; dep[i] = d; nbuf[i] = f.mat;
            let idx; if(flat) idx = Math.round(fi); else { const base = Math.floor(fi); idx = base + ((fi-base) > BAYER[x&3][y&3] ? 1 : 0); }
            idx = Math.max(0, Math.min(ramp.length-1, idx)); rbuf[i] = ramp; ibuf[i] = idx; } }
      }
    }
    const out = new Array(N).fill(null);
    for(let i=0;i<N;i++) if(rbuf[i]) out[i] = rbuf[i][ibuf[i]];
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue; const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j]) > EDGE){ const far = dep[i] > dep[j] ? i : j; out[far] = rbuf[far][Math.max(0, ibuf[far]-2)]; } } }
    if(s.weather > 0.02){ const rnd = mulberry32(3181 | ((s.variant*97)|0));
      for(let i=0;i<N;i++){ const m = nbuf[i]; if(!m || !rbuf[i]) continue;
        if(/^(pole|wood|plank|conc|galv|yel|alum|corr|sleeper)/.test(m) && rnd() < s.weather*0.055) out[i] = rbuf[i][Math.max(0, ibuf[i]-1)]; } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx, ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; nbuf[i]=null; } }
    const wet = new Uint8Array(N);
    for(let i=0;i<N;i++) if(nbuf[i] && /W$/.test(nbuf[i])) wet[i] = 1;
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let best=Infinity;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx, ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]&&dep[ny*W+nx]<best) best=dep[ny*W+nx]; }
      if(best<Infinity){ out[i] = KEY; dep[i] = best; } }        // keyline inherits its neighbour's depth
    const data = new Uint8ClampedArray(N*4), dp = new Float32Array(N);
    for(let i=0;i<N;i++){ const c = out[i];
      if(!c || (s.clipBelowWater && wet[i])){ data[i*4+3] = 0; dp[i] = Infinity; continue; }
      const [r,g,b] = hex2rgb(c); data[i*4]=r; data[i*4+1]=g; data[i*4+2]=b; data[i*4+3]=255; dp[i] = dep[i]; }
    return { data, w:W, h:H, px:ox, py:oy, wet, dep:dp };
  }

  // ============================ HULLS ========================================================
  // The yard publishes mounts; this drops the FLEET'S OWN BAKES on them. Each hull rig bakes at its
  // own px/m with its pivot on (amidships, keel bottom, centreline) — exactly the point a mount names —
  // so placement is a projection, a scale and a depth test against the yard's z-buffer. Nothing is
  // redrawn: the shed wall, cradle uprights and staging in front of a hull still cover it.
  const HULL_CACHE = new Map();
  function hullSprite(rigName, dir, elev){
    const rig = root[rigName];
    if(!rig || typeof rig.render !== 'function') return null;
    const e = Math.round(elev != null ? elev : DEFAULT_ELEV);
    const key = rigName + '|' + dir + '|' + e;
    if(HULL_CACHE.has(key)) return HULL_CACHE.get(key);
    let sp = null;
    try {
      const data = rig.render(dir, { elev:e });
      if(data && data.length === rig.W*rig.H*4)
        sp = { data, w:rig.W, h:rig.H, px:rig.pivot.x, py:rig.pivot.y, S:rig.PX || 32 };
    } catch(err){ sp = null; }
    if(HULL_CACHE.size > 24) HULL_CACHE.clear();
    HULL_CACHE.set(key, sp);
    return sp;
  }
  // heading in yard degrees -> the hull rig's own turntable index. A hull rig lays its length on
  // local +y, a mount lays it on +x at heading 0, hence the two-step (90 deg) offset.
  const hullDir = (dir, heading)=> ((dir + Math.round((heading || 0)/45) - 2) % 8 + 8) % 8;
  function compositeHulls(cell, s, dir, elev, B){
    const jobs = [];
    for(const m of cell.mounts || []){
      const spec = SHIPS.find(b => b.id === m.cls);
      if(!spec || !spec.rig) continue;
      const sp = hullSprite(spec.rig, hullDir(dir, m.heading), elev);
      if(!sp) continue;
      const q = projRaw(m.x, m.y, m.keelZ, B);
      jobs.push({ sp, k:cell.pxPerM/sp.S, X:q.sx + cell.px, Y:q.sy + cell.py, d:q.d, z0:(spec.depth || 2)*0.55 });
    }
    if(!jobs.length) return cell;
    let x0 = 0, y0 = 0, x1 = cell.w, y1 = cell.h;
    for(const j of jobs){
      const bx = j.X - j.sp.px*j.k, by = j.Y - j.sp.py*j.k;
      x0 = Math.min(x0, Math.floor(bx) - 1); y0 = Math.min(y0, Math.floor(by) - 1);
      x1 = Math.max(x1, Math.ceil(bx + j.sp.w*j.k) + 1); y1 = Math.max(y1, Math.ceil(by + j.sp.h*j.k) + 1);
    }
    const W = Math.min(MAXDIM, x1 - x0), H = Math.min(MAXDIM, y1 - y0);
    const ox = -x0, oy = -y0, N = W*H;
    const data = new Uint8ClampedArray(N*4), dep = new Float32Array(N).fill(Infinity), wet = new Uint8Array(N);
    for(let y=0;y<cell.h;y++){ const ty = y + oy; if(ty < 0 || ty >= H) continue;
      for(let x=0;x<cell.w;x++){ const tx = x + ox; if(tx < 0 || tx >= W) continue;
        const si = y*cell.w + x, ti = ty*W + tx;
        if(!cell.data[si*4+3]) continue;
        data[ti*4]=cell.data[si*4]; data[ti*4+1]=cell.data[si*4+1]; data[ti*4+2]=cell.data[si*4+2]; data[ti*4+3]=255;
        dep[ti] = cell.dep ? cell.dep[si] : 0; wet[ti] = cell.wet[si];
      } }
    jobs.sort((a,b)=> b.d - a.d);
    // Per-pixel depth, not one depth per hull: in an axonometric view the screen-vertical offset from
    // the mount IS a depth offset (d = d0 - ey*cot(elev) - z/sin(elev)), so a 110 m ship ramps in depth
    // along its own length and the dock roof over her bow still covers her. z0 = mid-hull height, the
    // one approximation left — gear standing well above the sheer can be occluded a touch early.
    const A = B.ce/B.se, IZ = 1/B.se;
    for(const j of jobs){
      const sp = j.sp, k = j.k, bx = j.X - sp.px*k + ox, by = j.Y - sp.py*k + oy;
      const tx0 = Math.max(0, Math.floor(bx)), tx1 = Math.min(W - 1, Math.ceil(bx + sp.w*k));
      const ty0 = Math.max(0, Math.floor(by)), ty1 = Math.min(H - 1, Math.ceil(by + sp.h*k));
      for(let ty=ty0; ty<=ty1; ty++){
        const dz = j.d - A*((ty + 0.5 - oy - j.Y)/cell.pxPerM) - j.z0*IZ - 0.06;
        for(let tx=tx0; tx<=tx1; tx++){
          const sx = Math.floor((tx + 0.5 - bx)/k), sy = Math.floor((ty + 0.5 - by)/k);
          if(sx < 0 || sy < 0 || sx >= sp.w || sy >= sp.h) continue;
          const si = (sy*sp.w + sx)*4;
          if(sp.data[si+3] < 128) continue;
          const ti = ty*W + tx;
          if(dep[ti] < dz) continue;                          // yard structure stands in front
          data[ti*4]=sp.data[si]; data[ti*4+1]=sp.data[si+1]; data[ti*4+2]=sp.data[si+2]; data[ti*4+3]=255;
          dep[ti] = dz; wet[ti] = 0;
        }
      }
    }
    cell.data = data; cell.w = W; cell.h = H; cell.px += ox; cell.py += oy; cell.wet = wet; cell.dep = dep;
    cell.hulls = jobs.length;
    return cell;
  }

  // ============================ SPEC =========================================================
  const DEFAULTS = {
    tideRange: 1.8, tide: null, mudZ: -2.2, gradeZ: null, hull: null,
    surface: null, shedColour: 'corr', weather: 0.4, variant: 0, fence: true,
    wet: true, gateClosed: true, ghost: false, clipBelowWater: false, growth: null, hulls: false,
    pxPerM: null, lod: 1,
  };
  const clampF = (v,a,b)=> Math.max(a, Math.min(b, +v || 0));
  function resolve(what, opts){
    opts = opts || {};
    const s = Object.assign({}, DEFAULTS, opts);
    s.what = SITES[what] ? what : (PART_BUILD[what] ? what : 'smallYard');
    s.isSite = !!SITES[s.what];
    const site = s.isSite ? SITES[s.what] : null;
    s.hull = s.hull || (site ? site.hull : 'lobster');
    s.tideRange = clampF(s.tideRange, 0.3, 14);
    s.tide = s.tide != null ? s.tide : s.tideRange*0.55;
    s.gradeZ = s.gradeZ != null ? s.gradeZ : +(s.tideRange + (site && site.freeboard != null ? site.freeboard : 1.15)).toFixed(2);
    s.mudZ = Math.min(s.mudZ, -0.6);
    s.surface = s.surface || (site ? site.surface : 'gravel');
    s.growth = Object.assign({}, GROWTH_ALL, opts.growth || {});
    if(site && site.wet != null && opts.wet == null) s.wet = site.wet;
    s._mounts = [];
    return s;
  }
  // A whole yard is a PLANNING bake: drop px/m until the sheet fits, and report what was used.
  function fitScale(what, opts){
    const s = resolve(what, opts);
    const H = ship(s.hull);
    let W = 30, D = 24, Z = 8;
    if(s.isSite){ const P = SITES[s.what].plan(H, s); W = P.W; D = P.D; Z = s.gradeZ + H.depth + 16; }
    else { W = Math.max(14, H.loa*1.4); D = Math.max(12, H.beam*3.2); Z = s.gradeZ + H.depth + 8; }
    for(const px of [32, 24, 16, 12, 8, 6, 4]){
      const w = (W + D)*0.7071*px + 24, h = ((W + D)*0.7071*Math.sin(DEFAULT_ELEV*DEG) + Z*Math.cos(DEFAULT_ELEV*DEG))*px + 24;
      if(w <= MAXDIM && h <= MAXDIM) return px;
    }
    return 4;
  }
  function plan(what, opts){ const s = resolve(what, opts); return s.isSite ? SITES[s.what].plan(ship(s.hull), s) : null; }

  // ============================ RENDER =======================================================
  function render(what, dir, opts){
    opts = (typeof opts === 'number') ? { elev:opts } : (opts || {});
    const s = resolve(what, opts), T = frame(s);
    const pxPerM = s.pxPerM || fitScale(what, opts);
    s.lod = pxPerM >= 16 ? 1 : pxPerM >= 10 ? 0.5 : 0;
    const B = camBasis({ dir, elev:opts.elev, S:pxPerM });
    const out = [], H = ship(s.hull);
    if(s.isSite) buildSite(out, s.what, s, T);
    else PART_BUILD[s.what](out, s, T, H);
    const cell = bake(out, B, makeMats(s), s, T);
    cell.pxPerM = pxPerM; cell.mounts = s._mounts; cell.hulls = 0;
    if(s.hulls) compositeHulls(cell, s, dir, opts.elev, B);
    return cell;
  }

  // ============================ GAMEPLAY =====================================================
  function gameplay(what, opts){
    const s = resolve(what, opts), T = frame(s), H = ship(s.hull);
    const faces = [];                                   // build for its side effects: mounts + plan
    if(s.isSite) buildSite(faces, s.what, s, T); else PART_BUILD[s.what](faces, s, T, H);
    const P = s._plan, site = s.isSite ? SITES[s.what] : null;
    const mounts = s._mounts.map((m,i)=> Object.assign({}, m, { id:'mount'+i }));
    const haulout = [];
    if(P && P.slip) haulout.push({ type:'slipway', x:P.slip.x, run:P.slip.run, crestZ:P.slip.crestZ, toeZ:P.slip.toeZ,
      width:P.slip.width, launchable: T.w > P.slip.toeZ + 0.35, waterEdgeY: null });
    if(P && P.railway) haulout.push({ type:'railway', x:P.railway.x, gauge:P.railway.gauge, run:P.railway.run, winch:!!P.winch });
    if(P && P.liftDock) haulout.push({ type:'travelLift', x:P.liftDock.x, slot:P.liftDock.slot, run:P.liftDock.run,
      maxBeam:+(P.liftDock.slot - 0.6).toFixed(2) });
    if(P && P.basin) haulout.push({ type:'basin', x:P.basin.x, y:P.basin.y, L:P.basin.L, W:P.basin.W,
      floorZ:P.basin.floorZ, copeZ:P.basin.copeZ, wet:P.basin.wet !== false, gateClosed:P.basin.gateClosed !== false,
      depthAtTide:+(T.w - P.basin.floorZ).toFixed(2), roofed:!!P.basin.roof,
      maxDraft:+(T.w - P.basin.floorZ - 0.8).toFixed(2), maxLoa:+(P.basin.L - 6).toFixed(1) });
    for(const c of (P && P.cells) || []) if(c.kind === 'davit') haulout.push({ type:'davit', x:c.x, y:c.y, hull:c.hull });
    const blockers = [], walk = [];
    if(P){
      walk.push({ poly:[[-P.W/2, -P.D/2],[P.W/2, -P.D/2],[P.W/2, P.edgeY],[-P.W/2, P.edgeY]], z:+P.gradeZ.toFixed(2),
        surface: (SURF[s.surface] || SURF.gravel).label, wet:'spray only' });
      for(const b of P.buildings || []) blockers.push({ shape:'box', x:b.x, y:b.y, w:b.L, d:b.W, h:b.eave || 3, kind:b.kind, ang:(b.ang||0) });
      if(P.fence) blockers.push({ shape:'wall', x0:-P.W/2 + 0.8, y0:P.fence.y, x1:P.W/2 - 0.8, y1:P.fence.y, h:2.1, kind:'fence', gate:!!P.fence.gate });
      if(P.basin) blockers.push({ shape:'box', x:P.basin.x, y:P.basin.y, w:P.basin.L, d:P.basin.W, h:0, kind:'basin', hazard:'open water' });
    }
    return {
      id: s.what, label: site ? site.label : (PARTS[s.what] ? PARTS[s.what].label : s.what),
      kind: s.isSite ? 'site' : 'part', tier: site ? site.tier : null, blurb: site ? site.blurb : null,
      servesHull: { id:H.id, label:H.label, loa:H.loa, beam:H.beam, draft:H.draft, depth:H.depth },
      footprint: P ? { w:+P.W.toFixed(1), d:+P.D.toFixed(1) } : null,
      gradeZ:+s.gradeZ.toFixed(2), waterZ:+T.w.toFixed(2), tideRange:+T.R.toFixed(2), mudZ:s.mudZ,
      surface: s.surface, edge: P ? P.edge : null,
      capacity: { hulls:mounts.length, biggest: mounts.reduce((a,m)=> m.loa > a ? m.loa : a, 0) },
      hullMounts: mounts, haulout, walk, blockers,
      buildings: (P ? P.buildings || [] : []).map(b => ({ kind:b.kind, x:b.x, y:b.y, L:b.L, W:b.W, eave:b.eave, gantry:!!b.gantry,
        clearSpan: b.kind === 'shed' ? +b.W.toFixed(1) : null, doorW: b.doorW || null })),
      zones: { stainTop:+T.stainTop.toFixed(2), barn:[+T.barnBot.toFixed(2), +T.barnTop.toFixed(2)],
        weed:[+T.weedBot.toFixed(2), +T.weedTop.toFixed(2)], ice:[+T.iceBot.toFixed(2), +T.iceTop.toFixed(2)] },
      snap: P ? { px:{ x:+(P.W/2).toFixed(2), y:0, z:+s.gradeZ.toFixed(2), face:'+x' },
                  nx:{ x:+(-P.W/2).toFixed(2), y:0, z:+s.gradeZ.toFixed(2), face:'-x' } } : null,
    };
  }
  function project(dir, p, elev, pxPerM){ const v = projRaw(p[0], p[1], p[2], camBasis({ dir, elev, S:pxPerM || PX })); return { x:v.sx, y:v.sy }; }
  // hull mounts in CELL pixels — pass the render result so the pivot lines up with the baked cell
  function anchors(what, dir, opts, cell){
    const g = gameplay(what, opts), e = (opts||{}).elev, sc = cell ? (cell.pxPerM || PX) : ((opts||{}).pxPerM || fitScale(what, opts));
    const ox = cell ? cell.px : 0, oy = cell ? cell.py : 0;
    const P = (p)=>{ const q = project(dir, p, e, sc); return { x:+(q.x+ox).toFixed(1), y:+(q.y+oy).toFixed(1) }; };
    return {
      pxPerM: sc,
      hullMounts: g.hullMounts.map(m => {
        const c = Math.cos(m.heading*DEG), sn = Math.sin(m.heading*DEG), hl = m.loa/2, hb = m.beam/2;
        return Object.assign({}, m, P([m.x, m.y, m.keelZ]), {
          footprint: [[-hl,-hb],[hl,-hb],[hl,hb],[-hl,hb]].map(([px,py]) => P([m.x + px*c - py*sn, m.y + px*sn + py*c, m.keelZ])),
          bow: P([m.x + hl*c, m.y + hl*sn, m.keelZ]), stern: P([m.x - hl*c, m.y - hl*sn, m.keelZ]),
        });
      }),
      haulout: g.haulout.map(h => Object.assign({}, h, h.x != null ? P([h.x, h.y != null ? h.y : 0, g.gradeZ]) : {})),
      buildings: g.buildings.map(b => Object.assign({}, b, P([b.x, b.y, g.gradeZ]))),
      gradeZ: g.gradeZ, waterZ: g.waterZ,
    };
  }
  function list(){ return Object.keys(PART_BUILD); }
  function sites(){ return Object.keys(SITES); }

  root.ShipyardIso = { PX, DIRS:8, defaultElev:DEFAULT_ELEV, order:['N','NE','E','SE','S','SW','W','NW'],
    PARTS, SITES, SHIPS, SURF, KEY, GROWTH_ALL,
    MATS: { WOOD, PLANK, POLE, IRON, GALV, ALUM, CONCRETE, ROCK, RUST, STEEL, GRASS, GRAVEL, DIRT, CORR, MESH, YEL },
    SHED_COLOURS: ['corr','galv','red','grn','buff','white'],
    SURFACES: ['gravel','conc','dirt','grass','ways'],
    list, sites, resolve, plan, frame, fitScale, ship, blockH, hullRigs, hullDir,
    render, gameplay, anchors, project };
})(typeof globalThis !== 'undefined' ? globalThis : window);
