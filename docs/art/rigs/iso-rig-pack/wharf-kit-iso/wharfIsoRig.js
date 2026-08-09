/* Hidden Harbours — parametric ISO WHARF STRUCTURE rig (ADR-0006 bake pipeline, SAME turntable +
   camera + shading as houseIsoRig / wharfBuildingRig / wharfDecorRig / the fleet / characterIsoRig).
   THIS IS THE ISO REPLACEMENT FOR Art/wharfKitRig.js. The old kit was a hand-painted near-plan
   32x32 auto-tile atlas: its deck cells were never projected through the shared camera, so nothing
   on it was in scale with anything else in the game. Measured off WharfOverlays.json:

     fitting     old baked sprite      true size        error
     ladder      9 x 20 px = 0.28 x 0.63 m   0.45 x 3.0 m+   ~4.7x short
     tyre        11 x 14 px = 0.34 x 0.44 m  1.00 m OD       ~2.5x small
     bollard     11 x 12 px = 0.34 x 0.37 m  0.75 m tall     ~2.0x short
     gangway     16 x 40 px = 0.50 x 1.25 m  0.95 x 6.0 m    ~4.8x short
     tallpier    19 px of face = 0.59 m      1.6 m stated    ~2.7x short

   Here every dimension is METRES in one 3D model, baked at 32 px = 1 m through the shared 3/4
   camera (45deg steps, elev 40deg, flat-facet shading from the fixed upper-LEFT key, z-buffered,
   ordered dither, per-face uv texture, depth-edge darkening, NO AA; the 1px keyline is RETIRED by
   default per ADR 0031 and reachable with `{outline:true}`). A 0.45 m ladder
   is 14 px wide because it is 0.45 m, and it reads correctly beside a 1.75 m fisher and a 4.9 m
   dory because all three go through the same projection.

   MODULAR STRUCTURES, NOT TILES. render(family, dir, opts) bakes ONE structure whose dimensions are
   parameters: bays x bayLen along +X, width across Y, deckZ above datum. Cells are sized from the
   projected bbox and carry their own pivot, so a 3-bay pier and a 9-bay pier are the same call.
   snap points in gameplay() let modules chain end-to-end into a whole harbour.

   TIDE IS A PARAMETER, AND THE TIDAL FRAME IS FIXED.
     z = 0        chart datum (lowest water)
     tideRange    metres from datum to highest water — 0.5 m to 12 m (Fundy) all work
     tide         current water level in metres above datum, ANY value, continuous
     clearance    how far a FIXED deck stands above highest water: deckZ = tideRange + clearance.
                  The same number sets a float's gangway abutment, so a ramp meets the deck beside
                  it rather than a step above it.
   The pack BAKES at tideRange 4.4 / clearance 0.8 — Hillsborough Bay, the home world (#466) — so a
   sheet's face is 5.2 m of quay. See DEFAULTS for why, and regenerate the contract if you move it.
   Growth zones are pinned to the tidal FRAME, not to the instantaneous water level, which is what
   makes a falling tide read: the bands do not move, the water uncovers them.
     dark tide stain   up to HHW + 0.10          barnacle band   0.40R .. 0.80R
     ice scour scar    0.90R .. min(R+0.14,      rockweed fringe 0.06R .. 0.40R (hangs, droops)
                       1.04R+0.10)
     wet-sheen split   everything below tide + 0.05 (splash), as a ramp transform
   (Those four are frame() verbatim. The figures here read 0.34R..0.86R / -0.35..0.46R before this
   commit, which is a stale draft of the same table — worth knowing, because the bands are exactly
   what the re-parameterisation moves and they are the natural thing to check it against.)
   Fixed structures (quay / pier / crib / slipway / riprap) hold station and expose pile, bracing,
   growth and marine hardware as the tide drops. FLOATS RIDE IT: deck z = tide + freeboard, guide
   hoops slide up the guide piles, mooring chain straightens, and the gangway re-solves its slope
   from the same two numbers. Nothing needs re-authoring per tide state.

   WATER CONTRACT (ADR 0010/0012/0023): ZERO water pixels are baked. No foam, no reflection, no sea.
   The shader owns everything wet. What the rig gives the shader instead is `wet` — a per-pixel mask
   of the structure that is currently below the waterline — so the shader can tint, refract or cut
   it with no knowledge of the geometry. opts.clipBelowWater drops those pixels outright.

   FAMILIES  quay · pier · crib · float · gangway · slipway · riprap
   FITTINGS  ladder · tyre · foam · cleat · bollard · ring · rail(wood|pipe) · dolphin · pileCap
             all sized in metres, all placed by rule from the structure's own dimensions.

   GAMEPLAY: gameplay(family, opts) returns metres-space data the game needs and pixels can't carry —
     walk      walkable deck polygons + surface z (and the float's rock amplitude)
     blockers  solid volumes: bollards, rail runs, guide piles, dolphin
     mooring   cleats / bollards / rings / dolphin with line capacity + which side they serve
     board     boarding points, the deck-to-water drop AT THIS TIDE, and needsLadder when it is > 1.2 m
     ladders   reach, and whether the bottom rung is currently submerged / dry
     fenders   contact circles a hull pushes off
     snap      +X / -X module sockets for chaining, at deck height
   Exposes globalThis.WharfIso = { PX, DIRS, defaultElev, order, FAMILIES, MATS palettes, list(),
     resolve(), frame(), render(family,dir,opts), gameplay(family,opts), anchors(), project() }. */
(function (root) {
  const PX = 32, S = 32, DEG = Math.PI / 180, DEFAULT_ELEV = 40, PAD = 3, MAXDIM = 2048;

  // ---- palettes, dark -> light (harbour master ramps, shared with the buildings / decor / fleet) ----
  const WOOD     = ['#553d23','#6b4e30','#7f603b','#957749','#ab8d5f','#c1a578']; // sawn timber
  const PLANK    = ['#454037','#565045','#676054','#7a7265','#8d8477','#a1988a']; // sun-silvered deck plank
  const POLE     = ['#33291f','#42342a','#523f33','#63503f','#75604c','#8a7460']; // creosoted pile
  const IRON     = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970'];
  const GALV     = ['#565b5f','#6d7276','#868b8f','#a0a5a8','#bbbfc1','#d6d9da'];
  const ALUM     = ['#3f4448','#525a5e','#687175','#7f888c','#98a0a3','#b2b9bb'];
  const CONCRETE = ['#55564f','#686962','#7c7d74','#919287','#a6a79b','#bbbcae'];
  const ROCK     = ['#252b31','#3a434c','#525a64','#6a727b','#848c94','#9aa2a9']; // armour stone
  const RUBBER   = ['#141517','#1d1f22','#282b2f','#34383d','#43484d','#54595f'];
  const FOAM     = ['#5c3a12','#7d5019','#9c6a24','#b8853a','#cda058','#e0bd80']; // orange trawl float
  const ROPE     = ['#5a4526','#71592f','#8a6f3d','#a2874e','#b89f66','#cdb686'];
  const NET      = ['#1d2a1e','#2a3a29','#374b34','#465e41','#57724f','#6a875f'];
  const YEL      = ['#6a4d17','#8e6a20','#b8923c','#cfa945','#e0bd58','#eed37e']; // safety curb
  const POLY     = ['#1b3550','#26496a','#345f85','#45789e','#5c92b4','#7aadc9']; // poly float drum
  const BARN     = ['#6b6a5f','#807f72','#989685','#b0ad9a','#c6c3af','#dcd9c4']; // barnacle crust
  const WEED     = ['#2b2a15','#3b3a1c','#4d4a22','#605c2b','#756f36','#8a8244']; // rockweed
  const ALG      = ['#20301f','#2c4029','#3a5233','#4a663f','#5d7c4d','#72925e']; // green algae film
  const SANDST   = ['#3a231a','#50301f','#6b3f28','#845234','#9c6a46','#b3835d']; // PEI red sandstone armour
  const RUST     = ['#3a1c10','#4f2614','#6a3620','#84462b','#9c5b3a','#b2724c']; // weathered steel sheet pile
  const STEEL    = ['#2a2f33','#3c454b','#525c63','#6d777e','#889298','#a2acb1']; // painted / mill steel
  const HDPE     = ['#1b1f22','#262b2f','#33393e','#434a50','#555d64','#6a737a']; // black poly dock cube
  const HDPEDECK = ['#3f4348','#4e545a','#5f666d','#727a81','#868f96','#9aa4ab']; // grey poly walking grid
  const KEY      = '#1a1c22';
  // ADR 0031 — the outline is retired from world art; the silhouette is carried by the form's own
  // dark side. The ring is not deleted: it is gated OFF by default and reachable with
  // `{outline:true}`, mirroring the engine's own `GameConfig.HullKeylineFlood` so the owner keeps a
  // one-flag A/B. Depth-edge darkening (the EDGE pass above the ring) is the separate INTERIOR rule
  // ADR 0031 §2 keeps untouched — do not confuse the two.
  const KEYLINE_DEFAULT = false;

  // ---- shading (identical recipe to wharfDecorRig / utilityIsoRig) ----
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
    return { th, ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) }; }
  // screen coords are RELATIVE to the projection of the model origin; the cell offset is applied at bake
  function projRaw(x,y,z,B){ const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr, yr, zr, sx:xr*S, sy:-(yr*B.se + zr*B.ce)*S, d:(yr*B.ce - zr*B.se) }; }
  function normal(a,b,c){ const ux=b.xr-a.xr, uy=b.yr-a.yr, uz=b.zr-a.zr, vx=c.xr-a.xr, vy=c.yr-a.yr, vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx; const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m]; }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se + n[2]*ce)*LN[1] + (-n[1]*ce + n[2]*se)*LN[2]; }

  // ---- face builders (outward-normal winding; signatures match wharfDecorRig) ----
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
    const e1=nrm(crs(a,u)), e2=nrm(crs(u,e1)); n=n||12; b=b||0;
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
  const pipe = (out,x,y,r,z0,z1,mat,b,n,tex)=> tube(out,[x,y,z0],[x,y,z1],r,n||12,mat,b,tex);
  const beam = (out,p0,p1,r,mat,b,n)=> tube(out,p0,p1,r,n||4,mat,b);
  function torusZ(out, x,y,z, R, r, n, m, mat, b){
    const P=(i,j)=>{ const th=i/n*Math.PI*2, ph=j/m*Math.PI*2, rr=R+r*Math.cos(ph);
      return [x+Math.cos(th)*rr, y+Math.sin(th)*rr, z+r*Math.sin(ph)]; };
    for(let i=0;i<n;i++) for(let j=0;j<m;j++){ const i2=(i+1)%n, j2=(j+1)%m;
      out.push(F([P(i,j),P(i2,j),P(i2,j2),P(i,j2)], mat, (b||0)+Math.sin(j/m*Math.PI*2)*0.22)); }
  }
  // torus lying in the X/Z plane (a tyre hanging flat against a face that runs along X)
  function torusY(out, x,y,z, R, r, n, m, mat, b){
    const P=(i,j)=>{ const th=i/n*Math.PI*2, ph=j/m*Math.PI*2, rr=R+r*Math.cos(ph);
      return [x+Math.cos(th)*rr, y+r*Math.sin(ph), z+Math.sin(th)*rr]; };
    for(let i=0;i<n;i++) for(let j=0;j<m;j++){ const i2=(i+1)%n, j2=(j+1)%m;
      out.push(F([P(i,j),P(i2,j),P(i2,j2),P(i,j2)], mat, (b||0)+Math.sin(j/m*Math.PI*2)*0.22)); }
  }
  function decalY(out, yv, ny, xs,xe, z0,z1, mat, b, tex, flat, db){ const e=0.02*ny, uw=xe-xs, uh=z1-z0;
    const P = ny>0 ? [[xs,yv+e,z0],[xe,yv+e,z0],[xe,yv+e,z1],[xs,yv+e,z1]] : [[xe,yv+e,z0],[xs,yv+e,z0],[xs,yv+e,z1],[xe,yv+e,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat)); }
  function decalX(out, xv, nx, ys,ye, z0,z1, mat, b, tex, flat, db){ const e=0.02*nx, uw=ye-ys, uh=z1-z0;
    const P = nx>0 ? [[xv+e,ye,z0],[xv+e,ys,z0],[xv+e,ys,z1],[xv+e,ye,z1]] : [[xv+e,ys,z0],[xv+e,ye,z0],[xv+e,ye,z1],[xv+e,ys,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat)); }
  function decalZ(out, zv, xs,xe, ys,ye, mat, b, tex, flat, db){
    out.push(F([[xs,ys,zv],[xe,ys,zv],[xe,ye,zv],[xs,ye,zv]], mat, b||0, db!=null?db:0.05,
      tex?[[0,0],[xe-xs,0],[xe-xs,ye-ys],[0,ye-ys]]:null, tex||null, flat)); }

  // ---- textures ----
  function plankTex(p){ p=p||0.20; return (u,v)=>{ const f=((u%p)+p)%p; if(f<0.028) return -2;
    return hash2(Math.floor(u/p)|0, Math.floor(v*2.4)|0)<0.42?-1:0; }; }
  function grainTex(p){ p=p||0.28; return (u,v)=>{ const f=((v%p)+p)%p; if(f<0.03) return -2;
    return hash2(Math.floor(v/p)|0, Math.floor(u*3)|0)<0.5?0:-1; }; }
  function sawnTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*7)|0, Math.floor(v*22)|0); return h<0.3?-1:(h>0.9?1:0); }; }
  function formTex(p){ p=p||0.62; return (u,v)=>{ const f=((v%p)+p)%p;              // board-form concrete
    if(f<0.035) return -2; const g=((u%1.22)+1.22)%1.22; if(g<0.03) return -1;
    return hash2(Math.floor(u*5)|0, Math.floor(v*5)|0)<0.24?-1:0; }; }
  function ribTex(p){ p=p||0.30; return (u,v)=>{ const f=((u%p)+p)%p; return f<p*0.30?-2:(f>p*0.72?1:0); }; }
  function rockTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*6)|0, Math.floor(v*6)|0); return h<0.26?-1:(h>0.84?1:0); }; }
  function crustTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*26)|0, Math.floor(v*26)|0);
    return h<0.30?-2:(h>0.70?1:0); }; }                                             // barnacle stipple
  function weedTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*9)|0, Math.floor(v*17)|0); return h<0.34?-2:(h>0.8?1:0); }; }
  function treadTex(p){ p=p||0.28; return (u,v)=>{ const f=((v%p)+p)%p; return f<0.045?-2:(f<0.09?1:0); }; }
  function rustTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*9)|0, Math.floor(v*9)|0); return h<0.22?-2:(h>0.88?1:0); }; }
  // vertical sheet-pile pans: deep U profile, one interlock line per pan
  function sheetTex(p){ p=p||0.62; return (u,v)=>{ const f=(((u%p)+p)%p)/p;
    if(f<0.05) return -2; if(f<0.16) return -1; if(f<0.50) return 0; if(f<0.66) return 1;
    return hash2(Math.floor(u/p)|0, Math.floor(v*2.2)|0)<0.35 ? -1 : 0; }; }
  // vertical timber sheeting / close-driven piles: one board per 300 mm, grain per board
  function sheetWoodTex(p){ p=p||0.30; return (u,v)=>{ const f=(((u%p)+p)%p)/p;
    if(f<0.10) return -2; if(f>0.90) return -1;
    return hash2(Math.floor(u/p)|0, Math.floor(v*1.7)|0)<0.42 ? -1 : 0; }; }
  function cubeTex(p){ p=p||0.50; return (u,v)=>{ const fu=(((u%p)+p)%p)/p, fv=(((v%p)+p)%p)/p;
    if(fu<0.07||fv<0.07) return -2; if(fu>0.90||fv>0.90) return -1; return 0; }; }

  // ============================ THE TIDAL FRAME ==============================================
  // z = 0 is chart datum (lowest water). Bands are pinned here, NOT to the current water level.
  function frame(s){
    const R = Math.max(0.3, s.tideRange), w = s.tide;
    return { R, w, hhw:R, mid:R*0.5,
      stainTop : R + 0.10,
      iceBot   : R * 0.90, iceTop: Math.min(R + 0.14, R * 1.04 + 0.10),
      barnTop  : R * 0.80, barnBot: R * 0.40,
      weedTop  : R * 0.40, weedBot: R * 0.06,
      splash   : w + 0.05 };
  }
  const GROWTH_ALL = { stain:true, barn:true, weed:true, ice:true, wet:true };
  // Growth is a TRANSFORM OF THE HOST MATERIAL, not its own poster colour: 'concB' is barnacled
  // concrete, 'poleG' is weeded creosote. Keeps material identity and stops the face banding out.
  function matAtZ(z, base, T, s){
    const g = s.growth; let k = base;
    if(g.stain && z <= T.stainTop) k = base + 'S';
    if(g.barn  && z <= T.barnTop && z >= T.barnBot) k = base + 'B';
    if(g.weed  && z <= T.weedTop && z >= T.weedBot) k = base + 'G';
    if(g.ice   && z <= T.iceTop  && z >= T.iceBot)  k = base + 'I';
    if(g.wet   && z <  T.splash) k += 'W';
    return k;
  }
  // split a vertical run at every band boundary it crosses -> [{a,b,mat}]
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

  // a pile / post carrying its tide bands, plus a rockweed fringe where the weed zone tops out
  function bandedPile(out, x, y, r, z0, z1, base, T, s, n, capMat){
    n = n || 10;
    for(const g of zSplit(z0, z1, base, T, s)){
      const rr = isBarn(g.mat) ? r*1.07 : isWeed(g.mat) ? r*1.04 : r;
      tube(out, [x,y,g.a], [x,y,g.b], rr, n, g.mat, isBarn(g.mat)?0.22:isWeed(g.mat)?-0.35:0, texFor(g.mat, base), false);
    }
    // top cap face + optional iron pile cap
    const topPts = []; for(let i=0;i<n;i++){ const t=(i/n)*Math.PI*2; topPts.push([x+Math.cos(t)*r, y-Math.sin(t)*r]); }
    slab(out, topPts, z1, matAtZ(z1, base, T, s), 0.30, base==='conc'?formTex():grainTex(0.22));
    if(capMat){ torusZ(out, x, y, z1+0.02, r*0.98, 0.035, 10, 5, capMat, 0.2);
      pipe(out, x, y, r*0.34, z1+0.02, z1+0.09, capMat, 0.3, 8); }
    if(s.growth.weed && T.weedTop > z0 && T.weedTop < z1) weedFringe(out, x, y, r, T.weedTop, s);
  }
  // rockweed: droopy tapered fronds hanging off the top of the weed zone
  function weedFringe(out, x, y, r, zTop, s){
    const rnd = mulberry32(((x*911 + y*577 + s.variant*31)|0) >>> 0);
    const n = 5 + ((rnd()*3)|0);
    for(let i=0;i<n;i++){
      const th = rnd()*Math.PI*2, len = 0.22 + rnd()*0.34;
      const px = x + Math.cos(th)*r*0.94, py = y + Math.sin(th)*r*0.94;
      const bx = px + Math.cos(th)*0.05, by = py + Math.sin(th)*0.05;
      tube(out, [px,py,zTop+0.02], [bx,by,zTop-len*0.55], 0.032, 5, 'weed', -0.3, null, false);
      tube(out, [bx,by,zTop-len*0.55], [bx+Math.cos(th)*0.03, by+Math.sin(th)*0.03, zTop-len], 0.022, 5, 'weed', -0.7, null, false);
    }
  }
  // a vertical wall carrying its tide bands (quay face, crib face, float fascia)
  function bandedWall(out, x0,y0,x1,y1, z0,z1, base, T, s, b, texOv){
    for(const g of zSplit(z0, z1, base, T, s))
      wall(out, x0,y0, x1,y1, g.a, g.b, g.mat, (isBarn(g.mat)||isWeed(g.mat)) ? texFor(g.mat, base) : (texOv || texFor(g.mat, base)),
        (b||0) + (isBarn(g.mat)?0.20:isWeed(g.mat)?-0.32:0));
  }
  // a driven sheet-pile wall: steel pans or close-driven timber, with a cap beam and tie-rod heads
  function sheetWall(out, x0,y0,x1,y1, z0, top, kind, T, s, b){
    const base = kind === 'steel' ? 'rust' : 'pole';
    bandedWall(out, x0,y0, x1,y1, z0, top - 0.20, base, T, s, b||0, kind === 'steel' ? sheetTex(0.62) : sheetWoodTex(0.30));
    if(kind === 'steel'){                                   // waler + tie-rod heads on the exposed face
      const wz = Math.max(z0 + 0.3, top - 0.75);
      for(const g of zSplit(wz - 0.09, wz + 0.09, 'steel', T, s))
        wall(out, x0,y0 + 0.05, x1,y1 + 0.05, g.a, g.b, g.mat, null, (b||0) + 0.25);
      const L = Math.hypot(x1-x0, y1-y0), n = Math.max(2, Math.round(L/1.9));
      for(let i=0;i<n;i++){ const t=(i+0.5)/n, px=x0+(x1-x0)*t, py=y0+(y1-y0)*t;
        pipe(out, px, py + 0.10, 0.055, wz - 0.055, wz + 0.055, matAtZ(wz,'steel',T,s), 0.35, 6); }
    } else {                                                 // pile heads standing proud of the sheeting
      const L = Math.hypot(x1-x0, y1-y0), n = Math.max(2, Math.round(L/1.2));
      for(let i=0;i<=n;i++){ const t=i/n, px=x0+(x1-x0)*t, py=y0+(y1-y0)*t;
        bandedPile(out, px, py, 0.15, top - 1.4, top - 0.06, 'pole', T, s, 8, s.capIron?'iron':null); }
    }
  }

  // ============================ FITTINGS, IN METRES ==========================================
  // side: +1 = the water edge at y = +Wd/2, -1 = shore edge. Every number here is a real dimension.
  const FIT = {
    ladder : { w:0.45, rung:0.30, railT:0.05, below:1.00 },  // 0.45 m wide, rungs at 300 mm, 1 m below LLW
    tyre   : { od:1.00, sec:0.28 },                          // truck tyre fender
    foam   : { od:0.55, len:1.10 },                          // poly foam fender
    cleat  : { len:0.50, h:0.22 },                           // galvanised horn cleat (heavy wharf pattern)
    bollard: { h:0.75, rBase:0.16, rTop:0.11, flange:0.24 }, // cast bollard w/ safety band
    ring   : { od:0.30, plate:0.42 },
    rail   : { h:1.05, mid:0.55, post:0.10, span:1.80, pipeR:0.024 },
    dolphin: { r:0.175, spread:0.55, above:1.20 },           // 3-pile cluster
  };
  function ladderAt(out, x, yFace, side, deckZ, T, s){
    const L = FIT.ladder, botZ = -L.below, hw = L.w/2, y = yFace + side*0.06;
    for(const sx of [-1, 1]){
      const px = x + sx*hw;
      for(const g of zSplit(botZ, deckZ - 0.06, 'galv', T, s)){
        const m = isWeed(g.mat) || isBarn(g.mat) ? g.mat : g.mat;
        box(out, px-L.railT/2, px+L.railT/2, y-0.035, y+0.035, g.a, g.b, m, 0.1, null, true);
      }
      // top bend over the curb onto the deck
      beam(out, [px, y, deckZ-0.06], [px, yFace - side*0.10, deckZ+0.02], 0.026, matAtZ(deckZ,'galv',T,s), 0.25, 6);
    }
    let z = botZ + 0.10;
    while(z < deckZ - 0.10){ const m = matAtZ(z, 'galv', T, s);
      beam(out, [x-hw, y+0.02, z], [x+hw, y+0.02, z], 0.021, m, 0.3, 6); z += L.rung; }
    for(const zz of [deckZ - 0.55, Math.max(botZ+0.4, T.hhw*0.5)])          // standoff brackets
      if(zz < deckZ && zz > botZ) box(out, x-hw-0.02, x+hw+0.02, yFace-side*0.02, y+side*0.03, zz-0.04, zz+0.04, matAtZ(zz,'galv',T,s), 0.05, null, true);
  }
  function tyreAt(out, x, yFace, side, deckZ, hangTop, T, s){
    const t = FIT.tyre, R = (t.od - t.sec)/2, r = t.sec/2;
    const cz = hangTop - t.od/2, y = yFace + side*(r + 0.03);
    const mat = cz < T.splash ? 'rubberW' : 'rubber';
    torusY(out, x, y, cz, R, r, 12, 6, mat, 0);
    for(const sx of [-0.10, 0.10]){                                        // chain to a bolt under the curb
      const zTop = deckZ - 0.16, links = Math.max(1, Math.round((zTop - (cz + R)) / 0.09));
      for(let i=0;i<links;i++){ const z0 = zTop - i*0.09;
        torusZ(out, x + sx, yFace + side*0.05, z0, 0.035, 0.013, 6, 4, z0 < T.splash?'galvW':'galv', 0.1); }
    }
  }
  function foamAt(out, x, yFace, side, hangTop, T, s){
    const f = FIT.foam, r = f.od/2, y = yFace + side*(r + 0.02);
    const zTop = hangTop, zBot = hangTop - f.len;
    for(const g of zSplit(zBot, zTop, 'foam', T, s)){
      const m = g.mat.indexOf('foam')===0 ? g.mat : (isWeed(g.mat)||isBarn(g.mat) ? g.mat : g.mat);
      tube(out, [x,y,g.a], [x,y,g.b], r*(g.b>zTop-0.06?0.86:1), 10, m, 0, null, false);
    }
    const top=[]; for(let i=0;i<10;i++){ const t=(i/10)*Math.PI*2; top.push([x+Math.cos(t)*r*0.86, y-Math.sin(t)*r*0.86]); }
    slab(out, top, zTop, 'foam', 0.3);
    pipe(out, x, y, 0.030, zTop, zTop+0.16, 'galv', 0.2, 8);
    beam(out, [x, y, zTop+0.14], [x, yFace + side*0.04, zTop+0.30], 0.024, 'rope', 0.1, 5);
  }
  function cleatAt(out, x, y, deckZ, ang){
    const c = FIT.cleat, hl = c.len/2, ca = Math.cos(ang||0), sa = Math.sin(ang||0);
    const P = (dx,dy)=>[x + dx*ca - dy*sa, y + dx*sa + dy*ca];
    const bp = P(0,0);
    box(out, bp[0]-hl*0.62, bp[0]+hl*0.62, bp[1]-0.085, bp[1]+0.085, deckZ, deckZ+0.045, 'iron', -0.55);
    for(const sx of [-1,1]){ const p = P(sx*hl*0.46, 0);
      pipe(out, p[0], p[1], 0.038, deckZ+0.03, deckZ+c.h-0.045, 'galv', 0.15, 8); }
    const a = P(-hl,0), b = P(hl,0);
    tube(out, [a[0],a[1],deckZ+c.h-0.025], [b[0],b[1],deckZ+c.h-0.025], 0.048, 8, 'galv', 0.3, null, true);
  }
  function bollardAt(out, x, y, deckZ){
    const B = FIT.bollard;
    pipe(out, x, y, B.flange, deckZ, deckZ+0.05, 'iron', 0.05, 12);
    for(let i=0;i<4;i++){ const z0 = deckZ+0.05 + i*(B.h-0.16)/4, z1 = z0 + (B.h-0.16)/4;
      const r0 = B.rBase + (B.rTop-B.rBase)*(i/4);
      tube(out, [x,y,z0], [x,y,z1], r0, 12, i===2?'galv':'iron', i===2?0.3:0, null, false); }
    tube(out, [x,y,deckZ+B.h-0.16], [x,y,deckZ+B.h-0.05], B.rTop*1.24, 12, 'iron', 0.15, null, false);
    pipe(out, x, y, B.rTop*1.24, deckZ+B.h-0.05, deckZ+B.h, 'iron', 0.35, 12);
  }
  function ringAt(out, x, y, deckZ){
    const R = FIT.ring;
    decalZ(out, deckZ+0.004, x-R.plate/2, x+R.plate/2, y-R.plate*0.34, y+R.plate*0.34, 'iron', -0.45, rustTex(), false, 0.04);
    torusZ(out, x, y, deckZ+0.03, R.od/2 - 0.022, 0.022, 12, 5, 'iron', 0.05);
  }
  function railRun(out, x0,y0, x1,y1, deckZ, kind, s){
    const R = FIT.rail, L = Math.hypot(x1-x0, y1-y0), n = Math.max(2, Math.round(L / R.span) + 1);
    for(let i=0;i<n;i++){ const t = i/(n-1), px = x0 + (x1-x0)*t, py = y0 + (y1-y0)*t;
      if(kind === 'pipe') pipe(out, px, py, R.pipeR*1.5, deckZ, deckZ+R.h, 'galv', 0.05, 8);
      else box(out, px-R.post/2, px+R.post/2, py-R.post/2, py+R.post/2, deckZ, deckZ+R.h, 'wood', 0.05, sawnTex()); }
    const ux = (x1-x0)/L, uy = (y1-y0)/L, nx2 = -uy*0.035, ny2 = ux*0.035;
    for(const z of [R.h, R.mid]){
      if(kind === 'pipe') tube(out, [x0,y0,deckZ+z], [x1,y1,deckZ+z], R.pipeR, 8, 'galv', 0.2, null, true);
      else box(out, Math.min(x0,x1)-0.02+nx2, Math.max(x0,x1)+0.02+nx2, Math.min(y0,y1)-0.055+ny2, Math.max(y0,y1)+0.055+ny2,
        deckZ+z-0.045, deckZ+z+0.045, 'wood', 0.22, sawnTex());
    }
  }
  function dolphinAt(out, x, y, deckZ, T, s){
    const D = FIT.dolphin, topZ = deckZ + D.above;
    for(let i=0;i<3;i++){ const th = i*(Math.PI*2/3) - Math.PI/2;
      const bx = x + Math.cos(th)*D.spread, by = y + Math.sin(th)*D.spread;
      const tx = x + Math.cos(th)*0.16, ty = y + Math.sin(th)*0.16;
      // splayed pile: two banded segments so the lean reads without a full skewed band solver
      for(const g of zSplit(s.mudZ, topZ, 'pole', T, s)){
        const f0 = (g.a - s.mudZ)/(topZ - s.mudZ), f1 = (g.b - s.mudZ)/(topZ - s.mudZ);
        const p0 = [bx + (tx-bx)*f0, by + (ty-by)*f0, g.a], p1 = [bx + (tx-bx)*f1, by + (ty-by)*f1, g.b];
        const rr = isBarn(g.mat) ? D.r*1.08 : D.r;
        tube(out, p0, p1, rr, 9, g.mat, isBarn(g.mat)?0.3:0, texFor(g.mat,'pole'), false);
      }
    }
    for(const z of [deckZ + 0.55, deckZ - 0.25, Math.max(s.mudZ+0.5, T.mid)])       // wire binding
      if(z > s.mudZ && z < topZ) torusZ(out, x, y, z, D.spread*0.62, 0.045, 12, 5, z < T.splash?'galvW':'galv', 0.15);
    pipe(out, x, y, D.spread*0.52, topZ, topZ+0.10, 'wood', 0.3, 10);
  }

  // One armour stone: a jittered polyhedron whose top is a FAN OF FACETS to a raised apex, so the
  // crown reads as broken rock rather than a smooth cap. Returns its crown z.
  function rubbleStone(out, x, y, zBase, r, mat, rnd, bias, tex){
    const n = 5 + ((rnd()*3)|0), rot = rnd()*Math.PI*2, sq = 0.74 + rnd()*0.46;
    // armour stone is a BLOCK, not a spire: low crown, broad faceted top, sharp arrises
    const h = r*(0.40 + rnd()*0.34), zb = zBase - r*(0.26 + rnd()*0.22);   // seated, not buried
    const base = [], top = [];
    for(let i=0;i<n;i++){
      const a = rot + (i/n)*Math.PI*2 + (rnd()-0.5)*0.30, rr = r*(0.78 + rnd()*0.30);
      base.push([x + Math.cos(a)*rr, y - Math.sin(a)*rr*sq]);
      const rt = rr*(0.62 + rnd()*0.28);
      top.push([x + Math.cos(a)*rt + (rnd()-0.5)*r*0.16, y - Math.sin(a)*rt*sq + (rnd()-0.5)*r*0.16,
        zBase + h*(0.74 + rnd()*0.34)]);
    }
    const tx = tex || rockTex();
    for(let i=0;i<n;i++){ const j=(i+1)%n;
      out.push(F([[base[i][0],base[i][1],zb],[base[j][0],base[j][1],zb],
                  [top[j][0],top[j][1],top[j][2]],[top[i][0],top[i][1],top[i][2]]],
        mat, (bias||0) - 0.35 + rnd()*0.8, 0, [[0,0],[r*1.6,0],[r*1.6,h],[0,h]], tx)); }
    const apex = [x + (rnd()-0.5)*r*0.42, y + (rnd()-0.5)*r*0.42, zBase + h*(0.94 + rnd()*0.20)];
    for(let i=0;i<n;i++){ const j=(i+1)%n;
      out.push(F([[top[i][0],top[i][1],top[i][2]],[top[j][0],top[j][1],top[j][2]], apex],
        mat, (bias||0) + 0.05 + rnd()*0.55, 0, [[0,0],[r,0],[r*0.5,r]], tx)); }
    return apex[2];
  }

  // ============================ DECK ASSEMBLIES ==============================================
  const DECK = { plank:0.055, stringer:[0.10,0.30], cap:[0.30,0.25], curb:[0.16,0.20] };
  // planked deck surface + perimeter fascia. Deck planks run ACROSS the wharf (seams along +X).
  function plankDeck(out, x0,x1, y0,y1, topZ, s, curbSide){
    const t = DECK.plank;
    slab(out, [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], topZ, 'plank', 0.12, plankTex(0.20));
    wall(out, x0,y1, x1,y1, topZ-t, topZ, 'plank', grainTex(0.20), -0.1);
    wall(out, x1,y0, x0,y0, topZ-t, topZ, 'plank', grainTex(0.20), -0.35);
    wall(out, x1,y1, x1,y0, topZ-t, topZ, 'plank', grainTex(0.20), 0.0);
    wall(out, x0,y0, x0,y1, topZ-t, topZ, 'plank', grainTex(0.20), -0.2);
    if(curbSide){ const c = DECK.curb, y = curbSide > 0 ? y1 - c[0]/2 : y0 + c[0]/2;
      box(out, x0, x1, y-c[0]/2, y+c[0]/2, topZ, topZ+c[1], s.curb==='yellow'?'yel':'wood', 0.18, sawnTex()); }
  }
  function underFrame(out, x0,x1, y0,y1, topZ, s, T){
    const st = DECK.stringer, z1 = topZ - DECK.plank, z0 = z1 - st[1];
    const n = Math.max(2, Math.round((y1-y0)/0.75) + 1);
    for(let i=0;i<n;i++){ const t = i/(n-1), y = y0 + 0.09 + (y1-y0-0.18)*t;
      for(const g of zSplit(z0, z1, 'wood', T, s))
        box(out, x0, x1, y-st[0]/2, y+st[0]/2, g.a, g.b, g.mat, -0.55, sawnTex(), true); }
  }

  // ============================ BERTHING ====================================================
  // A LADDER BELONGS TO A BERTH. The face is divided into berths sized by the biggest hull that
  // fits, each berth gets its own ladder at mid-berth and its fenders out at the quarters — which
  // is also what keeps a fender from ever landing on a ladder.
  const BOATS = [
    { id:'dory',    label:'dory',           loa:4.9,  beam:1.60, freeboard:0.55 },
    { id:'punt',    label:'punt',           loa:5.5,  beam:1.80, freeboard:0.60 },
    { id:'skiff',   label:'console skiff',  loa:6.5,  beam:2.20, freeboard:0.75 },
    { id:'lobster', label:'lobster boat',   loa:10.7, beam:4.30, freeboard:1.15 },
    { id:'packet',  label:'coastal packet', loa:14.5, beam:4.60, freeboard:1.60 },
    { id:'dragger', label:'side dragger',   loa:18.0, beam:5.60, freeboard:1.90 },
  ];
  const BERTH_CLR = 1.4;                                    // metres of water between hulls
  // Pack the face with the best MIX of hulls, not just repeats of one: a 24 m quay takes a packet
  // plus a skiff, a 32 m one a dragger plus a lobster boat. Utilisation first, then biggest hull
  // served (within 0.6 m), because a wharf is sized for the largest boat in the fleet.
  function packBerths(rem, only, depth, memo){
    if(depth > 6) return [];
    const key = Math.round(rem*10) + '|' + depth;
    if(memo[key]) return memo[key];
    let best = [], bestUsed = 0, bestMax = 0;
    for(const b of (only ? [only] : BOATS)){
      const slot = b.loa + BERTH_CLR;
      if(slot > rem + 1e-6) continue;
      const sub = packBerths(rem - slot, only, depth + 1, memo);
      const used = slot + sub.reduce((a,c)=> a + c.loa + BERTH_CLR, 0);
      const mx = Math.max(b.loa, sub.reduce((a,c)=> Math.max(a, c.loa), 0));
      if(used > bestUsed + 0.6 || (used > bestUsed - 0.6 && mx > bestMax)){ bestUsed = Math.max(bestUsed, used); bestMax = mx; best = [b].concat(sub); }
    }
    memo[key] = best;
    return best;
  }
  function berths(s){
    const L = (s.family === 'gangway' || s.family === 'slipway') ? s.run : s.bays * s.bayLen;
    if(s.family === 'gangway' || s.family === 'slipway' || s.family === 'riprap')
      return { list: [], cls: null, faceLen: L, clearance: BERTH_CLR };
    const only = BOATS.find(b => b.id === s.berth) || null;
    let pack = packBerths(L, only, 0, {}).slice().sort((a,b)=> b.loa - a.loa);   // big boat at the deep end
    if(!pack.length) pack = [only || BOATS[0]];                                  // a short face still berths something
    const used = pack.reduce((a,c)=> a + c.loa + BERTH_CLR, 0);
    const gap = Math.max(0, (L - used) / (pack.length + 1));
    let cursor = -L/2 + gap, list = [];
    pack.forEach((cls, i)=>{
      const slot = Math.min(cls.loa + BERTH_CLR, L), c = cursor + slot/2;
      const q = Math.min(cls.loa*0.32, slot*0.42);
      list.push({ id:'berth'+i, cls:cls.id, label:cls.label, loa:cls.loa, beam:cls.beam,
        freeboard:cls.freeboard, x:+c.toFixed(2), x0:+(c - cls.loa/2).toFixed(2), x1:+(c + cls.loa/2).toFixed(2),
        ladderX:+c.toFixed(2), fenderX:[+(c - q).toFixed(2), +(c + q).toFixed(2)],
        edgeX:[+(cursor).toFixed(2), +(cursor + slot).toFixed(2)] });
      cursor += slot + gap;
    });
    return { list, cls: pack[0], faceLen: L, clearance: BERTH_CLR, utilisation: +(used/L).toFixed(2) };
  }

  // ONE ramp body, used by the gangway family and by a float that carries its own gangway. Runs +X
  // from (cx-run/2, topZ) down to (cx+run/2, botZ): trusses, treads, plumb stanchions, hinge, rollers.
  function rampBody(out, cx, cy, run, wid, topZ, botZ, T, s){
    const hw = wid/2, x0 = cx - run/2, x1 = cx + run/2, dz = topZ - botZ, trussD = 0.30;
    const zAt = (x)=> topZ - dz*((x - x0)/run);
    for(const sy of [-1, 1]){ const y = cy + sy*(hw - 0.03);
      for(const off of [0, -trussD])
        tube(out, [x0, y, zAt(x0)+off-0.04], [x1, y, zAt(x1)+off-0.04], 0.035, 6, 'alum', off?-0.9:-0.4, null, true);
      const nd = Math.max(4, Math.round(run/0.9));
      for(let i=0;i<nd;i++){ const xa = x0 + run*(i/nd), xb = x0 + run*((i+1)/nd), up = i % 2 === 0;
        tube(out, [xa, y, zAt(xa) - (up?trussD:0) - 0.04], [xb, y, zAt(xb) - (up?0:trussD) - 0.04], 0.022, 5, 'alum', -1.1, null, false); }
    }
    const nt = Math.max(6, Math.round(run/0.28));
    for(let i=0;i<nt;i++){ const xa = x0 + run*(i/nt), xb = x0 + run*((i+0.86)/nt);
      out.push(F([[xa,cy-hw+0.04,zAt(xa)],[xb,cy-hw+0.04,zAt(xb)],[xb,cy+hw-0.04,zAt(xb)],[xa,cy+hw-0.04,zAt(xa)]],
        'alum', -0.85, 0, [[0,0],[0.24,0],[0.24,wid],[0,wid]], treadTex(0.28))); }
    out.push(F([[x0,cy-hw+0.04,zAt(x0)-0.03],[x1,cy-hw+0.04,zAt(x1)-0.03],[x1,cy+hw-0.04,zAt(x1)-0.03],[x0,cy+hw-0.04,zAt(x0)-0.03]], 'alum', -1.3));
    const ns = Math.max(3, Math.round(run/1.6)) + 1;
    for(const sy of [-1,1]){ const y = cy + sy*(hw-0.03);
      for(let i=0;i<ns;i++){ const x = x0 + run*(i/(ns-1)); pipe(out, x, y, 0.026, zAt(x), zAt(x)+0.92, 'alum', -0.5, 8); }
      for(const h of [0.92, 0.48]) tube(out, [x0, y, zAt(x0)+h], [x1, y, zAt(x1)+h], 0.024, 8, 'alum', -0.3, null, true);
    }
    box(out, x0-0.22, x0+0.06, cy-hw, cy+hw, topZ-0.06, topZ+0.02, 'galv', 0.1, rustTex());
    pipe(out, x0, cy, 0.05, topZ-0.02, topZ+0.02, 'iron', 0.2, 8);
    for(const sy of [-1,1]) tube(out, [x1-0.10, cy+sy*(hw-0.10), botZ+0.06], [x1-0.10, cy+sy*(hw-0.02), botZ+0.06], 0.075, 10, 'iron', 0.05, null, true);
    return Math.atan2(dz, run);
  }

  // ============================ FAMILIES =====================================================
  const FAMILIES = {
    quay: { label:'concrete quay', note:'solid mass-concrete face, no piles — the deep-water berth',
      dims:{ bays:[2,6,4], bayLen:[2.4,4.0,3.0], width:[4,12,7], deckZ:[1.2,4.5,2.6] },
      build(out, s, T){
        const L = s.bays*s.bayLen, hx = L/2, hy = s.width/2, top = s.deckZ, base = Math.min(s.mudZ, -0.4);
        const sheet = s.face === 'steelSheet' ? 'steel' : s.face === 'timberSheet' ? 'timber' : null;
        if(sheet){
          // driven wall of pans, fill behind it, concrete cap beam over the lot
          sheetWall(out, -hx, hy, hx, hy, base, top, sheet, T, s, 0.05);
          sheetWall(out, hx, -hy, -hx, -hy, base, top, sheet, T, s, -0.55);
          sheetWall(out, hx, hy, hx, -hy, base, top, sheet, T, s, -0.15);
          sheetWall(out, -hx, -hy, -hx, hy, base, top, sheet, T, s, -0.3);
          box(out, -hx-0.04, hx+0.04, -hy-0.04, hy+0.04, top-0.22, top, 'conc', 0.10, formTex(0.5));
          slab(out, [[-hx-0.04,-hy-0.04],[hx+0.04,-hy-0.04],[hx+0.04,hy+0.04],[-hx-0.04,hy+0.04]], top, 'conc', 0.06, formTex(0.9));
          if(s.curb !== 'none'){ const c = DECK.curb;      // Torbay's yellow bull rail rides the cap
            box(out, -hx, hx, hy-c[0]-0.02, hy-0.02, top, top+c[1], s.curb==='yellow'?'yel':'wood', 0.20, sawnTex()); }
          return;
        }
        // mass body, band-split so the tide frame reads on every wetted face
        bandedWall(out, -hx, hy,  hx, hy, base, top-0.24, 'conc', T, s, 0);      // water face (+Y)
        bandedWall(out,  hx,-hy, -hx,-hy, base, top-0.24, 'conc', T, s, -0.55);  // shore face
        bandedWall(out,  hx, hy,  hx,-hy, base, top-0.24, 'conc', T, s, -0.15);
        bandedWall(out, -hx,-hy, -hx, hy, base, top-0.24, 'conc', T, s, -0.3);
        // coping: cast slab standing 0.24 proud of the body, oversailing 60 mm
        box(out, -hx-0.06, hx+0.06, -hy-0.06, hy+0.06, top-0.24, top, 'conc', 0.10, formTex(0.5));
        slab(out, [[-hx-0.06,-hy-0.06],[hx+0.06,-hy-0.06],[hx+0.06,hy+0.06],[-hx-0.06,hy+0.06]], top, 'conc', 0.06, formTex(0.9));
        // timber rubbing strake + vertical fender piles on the working face
        if(s.fenderPiles) for(let i=0;i<=s.bays;i++){ const x = -hx + i*s.bayLen;
          bandedPile(out, x, hy+0.16, 0.14, Math.max(base, -0.9), top-0.30, 'pole', T, s, 9); }
        const wz = top - 0.62;
        for(const g of zSplit(wz-0.10, wz+0.10, 'wood', T, s))
          box(out, -hx, hx, hy+0.02, hy+0.14, g.a, g.b, g.mat, 0.1, sawnTex(), true);
        // weep holes + a service ring of stain runs
        for(let i=0;i<s.bays;i++){ const x = -hx + (i+0.5)*s.bayLen, z = Math.max(base+0.2, T.mid + 0.35);
          decalY(out, hy, 1, x-0.08, x+0.08, z, z+0.14, 'concS', -1.1, null, true, 0.04); }
      } },

    pier: { label:'timber pile pier', note:'pile-driven timber wharf — piles, caps, stringers, bracing',
      dims:{ bays:[2,9,4], bayLen:[2.2,3.6,2.8], width:[2.4,8,4.2], deckZ:[1.2,6,2.8] },
      build(out, s, T){
        const L = s.bays*s.bayLen, hx = L/2, hy = s.width/2, top = s.deckZ;
        const capD = DECK.cap[1], capW = DECK.cap[0];
        const capTop = top - DECK.plank - DECK.stringer[1], pileTop = capTop - capD;
        const steelP = s.struct === 'steelPile';
        const pileMat = steelP ? 'steel' : 'pole';
        const pileR = steelP ? s.pileR * 0.78 : s.pileR;
        const rows = s.bays + 1, across = s.width > 4.6 ? 3 : 2;
        const pileY = across === 3 ? [-hy+0.42, 0, hy-0.42] : [-hy+0.42, hy-0.42];
        for(let i=0;i<rows;i++){
          const x = -hx + i*s.bayLen;
          for(const y of pileY) bandedPile(out, x, y, pileR, s.mudZ, pileTop, pileMat, T, s, steelP?12:10, s.capIron?'iron':null);
          // pile cap (header) across the row
          for(const g of zSplit(pileTop, capTop, 'wood', T, s))
            box(out, x-capW/2, x+capW/2, -hy+0.18, hy-0.18, g.a, g.b, g.mat, 0.02, sawnTex(), true);
          // transverse X-bracing between the outer piles, below the cap
          if(s.brace && across >= 2){
            const zA = Math.max(s.mudZ + 0.35, Math.min(pileTop - 0.55, T.mid + 0.25)), zB = pileTop - 0.35;
            const y0 = pileY[0], y1 = pileY[pileY.length-1];
            for(const [pa, pb] of [[[y0,zA],[y1,zB]], [[y1,zA],[y0,zB]]]){
              const mm = matAtZ((pa[1]+pb[1])/2, 'wood', T, s);
              box2(out, [x-0.05, pa[0], pa[1]], [x+0.05, pb[0], pb[1]], 0.05, mm);
            }
          }
        }
        // longitudinal walers bolted along the outer pile lines
        if(s.brace) for(const y of [pileY[0], pileY[pileY.length-1]]){
          const wz = Math.max(s.mudZ + 0.5, Math.min(pileTop - 0.9, T.mid + 0.55));
          for(const g of zSplit(wz-0.09, wz+0.09, 'wood', T, s))
            box(out, -hx-0.05, hx+0.05, y - (y>0?0.02:0.16) , y + (y>0?0.16:0.02), g.a, g.b, g.mat, -0.2, sawnTex(), true);
        }
        // sheeted variant: close the working face below the deck so it reads as a solid wharf
        if(s.struct === 'sheeted'){
          sheetWall(out, -hx, hy - 0.02, hx, hy - 0.02, s.mudZ, top - DECK.plank - 0.02, steelP ? 'steel' : 'timber', T, s, 0.1);
        }
        underFrame(out, -hx, hx, -hy, hy, top, s, T);
        plankDeck(out, -hx, hx, -hy, hy, top, s, s.curb === 'none' ? 0 : 1);
      } },

    crib: { label:'stone-filled crib wharf', note:'drift-pinned log crib, stone ballast — Atlantic Canada vernacular',
      dims:{ bays:[2,6,3], bayLen:[2.6,4.2,3.2], width:[3.5,9,5], deckZ:[1.2,4.5,2.4] },
      build(out, s, T){
        const L = s.bays*s.bayLen, hx = L/2, hy = s.width/2, top = s.deckZ;
        const t = 0.28, cribTop = top - DECK.plank - 0.30, base = s.mudZ;
        const courses = Math.max(3, Math.round((cribTop - base) / t));
        const rnd = mulberry32(s.variant*7717 + 13);
        for(let c=0;c<courses;c++){
          const z0 = base + c*t, z1 = Math.min(cribTop, z0 + t*0.94);
          if(z0 >= cribTop) break;
          const m = matAtZ((z0+z1)/2, 'wood', T, s);
          const bump = isBarn(m) ? 0.02 : 0;
          if(c % 2 === 0){                                  // stretchers: logs along X
            for(const y of [-hy + t/2, hy - t/2])
              tube(out, [-hx, y, (z0+z1)/2], [hx, y, (z0+z1)/2], t*0.48 + bump, 8, m, isBarn(m)?0.3:0, grainTex(0.3), true);
          } else {                                          // headers: logs across Y, ends proud
            const n = Math.max(2, Math.round(L / s.bayLen) + 1);
            for(let i=0;i<n;i++){ const x = -hx + (L)*(i/(n-1));
              tube(out, [x, -hy - 0.10, (z0+z1)/2], [x, hy + 0.10, (z0+z1)/2], t*0.46 + bump, 8, m, isBarn(m)?0.3:0, grainTex(0.3), true); }
            for(const y of [-hy + t/2, hy - t/2]) {          // short fillers keep the wall closed
              tube(out, [-hx, y, (z0+z1)/2], [hx, y, (z0+z1)/2], t*0.34, 6, m, isBarn(m)?0.2:-0.3, null, false); }
          }
          if(c % 2 === 1 && c > 1){                          // drift pins
            for(let i=0;i<=s.bays;i++) pipe(out, -hx + i*s.bayLen, hy - t/2, 0.022, z1-0.02, z1+0.10, matAtZ(z1,'galv',T,s), 0.2, 6);
          }
          if(s.growth.weed && Math.abs((z0+z1)/2 - T.weedTop) < t) weedFringe(out, hx*0.55, hy - t/2, t*0.5, T.weedTop, s);
        }
        // stone ballast heaped inside, visible over the last course
        const fillZ = cribTop - 0.10;
        for(let i=0;i<Math.max(10, s.bays*7);i++){
          const px = -hx + 0.5 + rnd()*(L-1), py = -hy + 0.55 + rnd()*(s.width-1.1), rr = 0.16 + rnd()*0.22;
          const m = matAtZ(fillZ, 'stone', T, s);
          pipe(out, px, py, rr, fillZ - rr*0.6, fillZ + rr*0.45*(0.4+rnd()), m, -0.2 + rnd()*0.5, 6, rockTex());
          const cap=[]; for(let k=0;k<6;k++){ const a=(k/6)*Math.PI*2; cap.push([px+Math.cos(a)*rr, py-Math.sin(a)*rr]); }
          slab(out, cap, fillZ + rr*0.45*0.7, m, 0.25, rockTex());
        }
        // deck bearers over the crib, then planking
        for(let i=0;i<=s.bays;i++){ const x = -hx + i*s.bayLen;
          box(out, x-0.14, x+0.14, -hy+0.1, hy-0.1, cribTop, top - DECK.plank, matAtZ(cribTop,'wood',T,s), 0.0, sawnTex(), true); }
        if(s.cap === 'concrete'){
          box(out, -hx-0.05, hx+0.05, -hy-0.05, hy+0.05, top - 0.26, top, 'conc', 0.08, formTex(0.5));
          slab(out, [[-hx-0.05,-hy-0.05],[hx+0.05,-hy-0.05],[hx+0.05,hy+0.05],[-hx-0.05,hy+0.05]], top, 'conc', 0.06, formTex(0.9));
          if(s.curb !== 'none'){ const c = DECK.curb;
            box(out, -hx, hx, hy-c[0], hy, top, top+c[1], s.curb==='yellow'?'yel':'wood', 0.18, sawnTex()); }
        } else plankDeck(out, -hx, hx, -hy, hy, top, s, s.curb === 'none' ? 0 : 1);
      } },

    float: { label:'floating dock', note:'rides the tide on guide piles — deck z tracks water level',
      dims:{ bays:[1,5,2], bayLen:[2.4,3.6,3.0], width:[1.8,3.6,2.4], deckZ:[0.30,0.60,0.40] },
      build(out, s, T){
        const L = s.bays*s.bayLen, hx = L/2, hy = s.width/2;
        const top = s.floatDeckZ, t = 0.05, frameD = 0.26, fz1 = top - t, fz0 = fz1 - frameD;
        if(s.hull === 'plastic'){
          // modular HDPE cube raft: the cubes ARE the buoyancy, the frame and the deck
          const cz1 = top, cz0 = top - 0.42;
          for(const g of zSplit(cz0, cz1, 'hdpe', T, s)){
            wall(out, -hx, hy,  hx, hy, g.a, g.b, g.mat, cubeTex(0.5), 0.05);
            wall(out,  hx,-hy, -hx,-hy, g.a, g.b, g.mat, cubeTex(0.5), -0.5);
            wall(out,  hx, hy,  hx,-hy, g.a, g.b, g.mat, cubeTex(0.5), -0.15);
            wall(out, -hx,-hy, -hx, hy, g.a, g.b, g.mat, cubeTex(0.5), -0.3);
          }
          slab(out, [[-hx,-hy],[hx,-hy],[hx,hy],[-hx,hy]], cz0, matAtZ(cz0,'hdpe',T,s), -0.95);
          slab(out, [[-hx,-hy],[hx,-hy],[hx,hy],[-hx,hy]], top, 'hdpeDeck', 0.12, cubeTex(0.5));
          const cu = 0.5;                                          // coupling-pin bosses on the seams
          for(let x = -hx + cu; x < hx - 0.01; x += cu) for(const y of [-hy + cu*0.5, hy - cu*0.5])
            pipe(out, x, y, 0.045, top, top + 0.035, 'hdpeDeck', 0.3, 6);
          if(s.curb !== 'none' && s.curb === 'yellow')
            box(out, -hx, hx, hy-0.14, hy, top, top+0.14, 'yel', 0.2, null, true);
        } else {
        // perimeter frame + cross joists
        box(out, -hx, hx, hy-0.10, hy, fz0, fz1, 'wood', 0.05, sawnTex(), true);
        box(out, -hx, hx, -hy, -hy+0.10, fz0, fz1, 'wood', -0.4, sawnTex(), true);
        box(out, -hx, -hx+0.10, -hy, hy, fz0, fz1, 'wood', -0.2, sawnTex(), true);
        box(out, hx-0.10, hx, -hy, hy, fz0, fz1, 'wood', 0.0, sawnTex(), true);
        const nj = Math.max(2, Math.round(L/0.8));
        for(let i=1;i<nj;i++){ const x = -hx + L*(i/nj);
          box(out, x-0.045, x+0.045, -hy+0.1, hy-0.1, fz0+0.04, fz1, 'wood', -0.5, null, true); }
        // flotation: poly-shelled foam billets slung under the frame, waterline across them
        const nb = Math.max(2, Math.round(L / 2.4)), bw = Math.min(2.1, L/nb - 0.22), bd = Math.min(0.62, s.width*0.55), bh = 0.42;
        for(let i=0;i<nb;i++){
          const cxx = -hx + (L/nb)*(i+0.5);
          const z1 = fz0 + 0.02, z0 = z1 - bh;
          for(const g of zSplit(z0, z1, 'poly', T, s))
            box(out, cxx-bw/2, cxx+bw/2, -bd/2, bd/2, g.a, g.b, g.mat, -0.15, null, true);
          slab(out, [[cxx-bw/2,-bd/2],[cxx+bw/2,-bd/2],[cxx+bw/2,bd/2],[cxx-bw/2,bd/2]], z0, matAtZ(z0,'poly',T,s), -0.9);
        }
        slab(out, [[-hx,-hy],[hx,-hy],[hx,hy],[-hx,hy]], top, 'plank', 0.12, plankTex(0.20));
        if(s.curb !== 'none'){ const c = DECK.curb;
          box(out, -hx, hx, hy-c[0], hy, top, top+c[1]*0.7, s.curb==='yellow'?'yel':'wood', 0.2, sawnTex()); }
        }
        // A gangway hung off this float: the hinge is on the fixed abutment, the landing rides the
        // float — so it is solved from the float's ROCKED deck height and then held out of the rock.
        const gFrom = out.length;
        if(s.gangway){
          const gRun = s.gangRun != null ? clampF(s.gangRun, 3, 14)
            : Math.max(3, Math.min(14, (T.R + s.freeboard + 0.9) * 2.4));
          // Same quantity as the fixed families' deck: clearance above HIGHEST water. It must read
          // s.clearance, not a second literal — at the shipped defaults the two were EQUAL (both
          // 2.8 m), and a hard-coded 1.0 here would hinge this ramp 0.20 m ABOVE the quay deck it
          // is supposed to meet, which draws as a step and reads as a bug in the float, not in the
          // number.
          const abut = s.abutZ != null ? s.abutZ : T.R + s.clearance;
          const land = s._rockPt ? s._rockPt([-hx + 0.4, 0, top])[2] : top;
          const hingeX = -hx - gRun + 0.30, aw = s.gangWidth/2 + 0.35;
          // the abutment it hinges off: without something under the hinge the ramp reads as floating
          bandedWall(out, hingeX, aw, hingeX, -aw, s.mudZ, abut - 0.22, 'conc', T, s, -0.2);
          bandedWall(out, hingeX - 1.5, aw, hingeX, aw, s.mudZ, abut - 0.22, 'conc', T, s, 0.05);
          bandedWall(out, hingeX, -aw, hingeX - 1.5, -aw, s.mudZ, abut - 0.22, 'conc', T, s, -0.55);
          box(out, hingeX - 1.55, hingeX + 0.05, -aw - 0.05, aw + 0.05, abut - 0.22, abut, 'conc', 0.08, formTex(0.5));
          rampBody(out, -hx - gRun/2 + 0.30, 0, gRun, s.gangWidth, abut, land + 0.06, T, s);
        }
        // ANYTHING DRIVEN INTO THE SEABED MUST NOT ROCK WITH THE FLOAT: guide piles, chain, anchor
        // block and the already-solved gangway are tagged fixed and skipped by the rock transform.
        const fixedFrom = gFrom;
        if(s.guidePiles) for(const gx of (s.bays > 1 ? [-hx+0.4, hx-0.4] : [hx-0.4])){
          const py = hy + 0.34;
          bandedPile(out, gx, py, s.pileR, s.mudZ, T.hhw + s.guideAbove, 'pole', T, s, 10, s.capIron?'iron':null);
          torusZ(out, gx, py, top + 0.22, s.pileR + 0.075, 0.045, 12, 5, 'galv', 0.25);
          box(out, gx-0.05, gx+0.05, hy-0.02, py-s.pileR-0.04, top+0.16, top+0.28, 'galv', 0.1, null, true);
        }
        // mooring chain to a seabed block — straightens as the tide falls
        if(s.chain){
          const ax = -hx - 0.9, ay = -hy - 0.7;
          box(out, ax-0.34, ax+0.34, ay-0.30, ay+0.30, s.mudZ, s.mudZ+0.34, matAtZ(s.mudZ+0.17,'conc',T,s), -0.3, formTex(0.4));
          const n = 9, p0 = [-hx+0.14, -hy+0.06, fz0+0.06], p1 = [ax, ay, s.mudZ+0.34];
          const slack = Math.max(0, 0.55 - (T.w - s.mudZ) * 0.06);
          for(let i=0;i<n;i++){ const t0=i/n, t1=(i+1)/n;
            const cz = (a)=>p0[2] + (p1[2]-p0[2])*a - Math.sin(a*Math.PI)*slack;
            const A = [p0[0]+(p1[0]-p0[0])*t0, p0[1]+(p1[1]-p0[1])*t0, cz(t0)];
            const Bp= [p0[0]+(p1[0]-p0[0])*t1, p0[1]+(p1[1]-p0[1])*t1, cz(t1)];
            tube(out, A, Bp, 0.030, 5, ((A[2]+Bp[2])/2 < T.splash)?'galvW':'galv', 0.05, null, false); }
        }
        for(let i = fixedFrom; i < out.length; i++) out[i].fixed = true;
      } },

    gangway: { label:'gangway ramp', note:'aluminium ramp, slope re-solved from deck height and tide',
      dims:{ bays:[1,1,1], bayLen:[3,14,6], width:[0.8,1.4,0.95], deckZ:[1.2,8,2.8] },
      build(out, s, T){
        s._gangAng = rampBody(out, 0, 0, s.run, s.width, s.deckZ, s.floatDeckZ + 0.06, T, s);
      } },

    slipway: { label:'slipway', note:'ramp into the water — the tide decides how much is usable',
      dims:{ bays:[1,1,1], bayLen:[6,16,10], width:[2.5,6,3.6], deckZ:[0.8,3,1.6] },
      build(out, s, T){
        const L = s.run, hw = s.width/2, topZ = s.deckZ, toeZ = s.toeZ;
        const x0 = -L/2, x1 = L/2, dz = topZ - toeZ;
        const zAt = (x)=> topZ - dz*((x - x0)/L);
        const n = Math.max(8, Math.round(L/0.9));
        const kerb = 0.26, kh = 0.22;                                   // side kerbs follow the slope
        for(let i=0;i<n;i++){
          const xa = x0 + L*(i/n), xb = x0 + L*((i+1)/n);
          const za = zAt(xa), zb = zAt(xb), zm = (za+zb)/2;
          const m = matAtZ(zm, 'conc', T, s);
          out.push(F([[xa,-hw+kerb,za],[xb,-hw+kerb,zb],[xb,hw-kerb,zb],[xa,hw-kerb,za]], m, 0.05, 0,
            [[xa,-hw],[xb,-hw],[xb,hw],[xa,hw]], ribTex(0.30)));
          // sloped kerbs: a top strip + an inner cheek, both parallel to the ramp
          for(const sy of [-1,1]){ const yo = sy*(hw - kerb/2), yi = sy*(hw - kerb), ye = sy*hw;
            out.push(F([[xa,yi,za+kh],[xb,yi,zb+kh],[xb,ye,zb+kh],[xa,ye,za+kh]], m, 0.30, 0, [[xa,0],[xb,0],[xb,kerb],[xa,kerb]], ribTex(0.9)));
            out.push(F(sy>0 ? [[xa,yi,za],[xb,yi,zb],[xb,yi,zb+kh],[xa,yi,za+kh]] : [[xb,yi,zb],[xa,yi,za],[xa,yi,za+kh],[xb,yi,zb+kh]], m, -0.55));
            // sloped outer cheek — wall() holds z constant across a segment, which stair-stepped the kerb
            out.push(F(sy>0 ? [[xa,ye,za+kh],[xb,ye,zb+kh],[xb,ye,zb-0.46],[xa,ye,za-0.46]]
                            : [[xb,ye,zb+kh],[xa,ye,za+kh],[xa,ye,za-0.46],[xb,ye,zb-0.46]],
              matAtZ(zb - 0.2,'conc',T,s), sy>0?-0.15:-0.7, 0,
              [[0,0],[xb-xa,0],[xb-xa,kh+0.46],[0,kh+0.46]], formTex()));
          }
          if(s.slipRails) for(const sy of [-1,1]){ const y = sy*hw*0.40;   // timber cradle rails, on the slope
            const mw = matAtZ(zm, 'wood', T, s);
            out.push(F([[xa,y-0.09,za+0.13],[xb,y-0.09,zb+0.13],[xb,y+0.09,zb+0.13],[xa,y+0.09,za+0.13]], mw, 0.22, 0, [[xa,0],[xb,0],[xb,0.18],[xa,0.18]], grainTex(0.3)));
            out.push(F([[xb,y+0.09,zb+0.13],[xb,y+0.09,zb],[xa,y+0.09,za],[xa,y+0.09,za+0.13]], mw, -0.5));
          }
        }
        // shore-end head wall, only as deep as the ramp needs
        box(out, x0-0.28, x0, -hw, hw, Math.max(s.mudZ, topZ-1.4), topZ+0.10, matAtZ(topZ,'conc',T,s), -0.1, formTex(0.5));
      } },

    riprap: { label:'armour-stone edge', note:'graded rock — revetment, breakwater mound or sheet-pile cell',
      dims:{ bays:[2,8,4], bayLen:[2,4,3], width:[2.5,7,4], deckZ:[0.8,4,2.2] },
      build(out, s, T){
        const L = s.bays*s.bayLen, hx = L/2, crest = s.deckZ, toe = s.mudZ, hy = s.width/2;
        const rnd = mulberry32(s.variant*4441 + 907);
        const SM = s.stone === 'sandstone' ? 'sandst' : 'stone';      // PEI red, or grey granite
        const cell = s.mound === 'sheetCell', bw = s.mound === 'breakwater';
        // the face: a revetment slopes one way, a breakwater mounds both ways off a centre crest
        const zPlane = bw
          ? (y)=> crest - (Math.abs(y) / hy) * (crest - toe) * 0.92
          : (y)=> crest - ((y + hy) / s.width) * (crest - toe);
        if(cell){
          // driven sheet-pile cell, rock-filled and capped — the harbour breakwater arm
          const capZ = crest - 0.55;
          sheetWall(out, -hx, hy, hx, hy, toe, capZ + 0.20, 'steel', T, s, 0.05);
          sheetWall(out, hx, -hy, -hx, -hy, toe, capZ + 0.20, 'steel', T, s, -0.55);
          sheetWall(out, hx, hy, hx, -hy, toe, capZ + 0.20, 'steel', T, s, -0.15);
          sheetWall(out, -hx, -hy, -hx, hy, toe, capZ + 0.20, 'steel', T, s, -0.3);
          slab(out, [[-hx,-hy],[hx,-hy],[hx,hy],[-hx,hy]], capZ, matAtZ(capZ,'conc',T,s), -0.15, formTex(0.6));
          const gstep = 0.52, gnx = Math.max(3, Math.round(L/gstep)), gny = Math.max(3, Math.round(s.width/gstep));
          for(let j=0;j<gny;j++) for(let i=0;i<gnx;i++){
            const x = -hx + (i+0.5)*(L/gnx) + (rnd()-0.5)*gstep*0.9;
            const y = -hy + (j+0.5)*(s.width/gny) + (rnd()-0.5)*gstep*0.8;
            if(x < -hx || x > hx || y < -hy - 0.2 || y > hy + 0.2) continue;
            const rr = 0.26 + Math.pow(rnd(), 1.5)*0.36;
            const heap = 1 - Math.abs(y) / (hy + 0.4);                 // heaped along the crest line
            rubbleStone(out, x, y, capZ + 0.10 + heap*0.55 + rnd()*0.14, rr, matAtZ(capZ + 0.4, SM, T, s), rnd, 0);
          }
          return;
        }
        // core behind / under the armour, so the mound is never see-through
        const steps = Math.max(6, Math.round(L/0.9));
        for(let i=0;i<steps;i++){
          const xa = -hx + L*(i/steps), xb = -hx + L*((i+1)/steps);
          const ya = -hy + 0.15, yb = hy - 0.15;
          const m = matAtZ((zPlane(ya)+zPlane(yb))/2, SM, T, s);
          out.push(F([[xa,ya,zPlane(ya)-0.16],[xb,ya,zPlane(ya)-0.16],[xb,yb,zPlane(yb)-0.16],[xa,yb,zPlane(yb)-0.16]], m, -0.5, 0,
            [[xa,ya],[xb,ya],[xb,yb],[xa,yb]], rockTex()));
        }
        // two grades on a jittered lattice: filter stone first, armour over it, all faceted
        for(const g of [{ r:[0.14,0.22], step:0.30, lift:-0.05, b:-0.95 },
                        { r:[0.34,0.58], step:0.72, lift:0.08, b:0.0 }]){
          const nx = Math.max(2, Math.round(L / g.step)), ny = Math.max(2, Math.round(s.width / g.step));
          for(let j=0;j<ny;j++) for(let i=0;i<nx;i++){
            const jx = (rnd()-0.5)*g.step*1.05, jy = (rnd()-0.5)*g.step*0.85;
            const x = -hx + (i+0.5)*(L/nx) + jx, y = -hy + (j+0.5)*(s.width/ny) + jy;
            if(x < -hx || x > hx || y < -hy || y > hy) continue;
            const rr = g.r[0] + Math.pow(rnd(), 1.6)*(g.r[1]-g.r[0]);
            const zs = zPlane(y) + g.lift + rr*0.30 + (rnd()-0.5)*0.16;
            if(zs > crest + 0.14) continue;
            const crown = rubbleStone(out, x, y, zs, rr, matAtZ(zs, SM, T, s), rnd, g.b);
            if(s.growth.weed && Math.abs(zs - T.weedTop) < 0.28 && rnd() < 0.45) weedFringe(out, x, y, rr*0.8, crown - rr*0.3, s);
          }
        }
        // crest beam: a low concrete kerb so the revetment can meet a deck (revetment only)
        if(!bw && s.crestBeam !== false) box(out, -hx, hx, -hy - 0.35, -hy + 0.20, crest - 0.42, crest, matAtZ(crest,'conc',T,s), -0.05, formTex(0.5));
      } },
  };
  // an oriented box between two corner points (used for skewed braces)
  function box2(out, a, b, t, mat){
    const dx = b[0]-a[0], dy = b[1]-a[1], dz = b[2]-a[2], L = Math.hypot(dx,dy,dz);
    tube(out, a, b, t*0.62, 4, mat, -0.35, null, true);
  }

  // ============================ SPEC RESOLUTION ==============================================
  // ---- THE BAKE PARAMETERS -------------------------------------------------------------------
  // `tideRange` and `clearance` are what the PACK IS BAKED AT: the baker renders every preset with
  // `{}`, so these two numbers are the coast the 17 committed sheets depict. They are Hillsborough
  // Bay's — 4.4 m from chart datum to highest water, and a deck standing 0.8 m above that — not a
  // generic coast, because the game has one home world (#466) and Nine Mile Creek is the first
  // wharf the player walks onto.
  //
  // They were 1.8 / 1.0 and the face baked 2.80 m against the 5.2 m NMC authors. #471 measured the
  // 2.40 m shortfall and decomposed it: +2.60 m of tide, less 0.20 m of freeboard — this wharf sits
  // closer to its own high water than a generic coast assumes, a wharf you step DOWN onto. Changing
  // these re-pins the growth bands too (barnacle 0.40R..0.80R, rockweed 0.06R..0.40R), which is the
  // half no vertical stack of cells could ever have fixed.
  //
  // ⚠️ THE CELL IS PARAMETRIC. These two numbers move 14 of the 17 committed cells — every fixed
  // structure grows by exactly 2.40 m of deck, which is 59 px through this camera — so
  // wharfIsoRig.contract.json must be regenerated in the SAME COMMIT or every bake refuses against
  // its own oracle. The three that do not move (lowPier, tallPier, sheetCell) are exactly the
  // presets that pin `deckZ` themselves and so never read the auto below.
  const DEFAULTS = {
    tide: null, tideRange: 4.4, clearance: 0.8, mudZ: -1.4, deckZ: null, width: null, bays: null, bayLen: null,
    pileR: 0.16, brace: true, capIron: true, curb: 'wood', fenderPiles: true,
    rail: 'none', railSides: ['shore','ends'], guidePiles: true, guideAbove: 1.6, chain: true,
    slipRails: true, run: null, toeZ: null, freeboard: 0.40,
    fittings: null, weather: 0.35, variant: 0, frame: 0, growth: null, clipBelowWater: false,
    // ---- style axes (every default is the original look; these only ADD variants) ----
    berth: null,          // null = auto-size berths to the longest hull that fits; or a BOATS id
    face: 'concrete',     // quay:  concrete | steelSheet | timberSheet
    struct: 'open',       // pier:  open | sheeted | steelPile
    cap: 'plank',         // crib:  plank | concrete
    hull: 'timber',       // float: timber | plastic
    stone: 'granite',     // riprap: granite | sandstone
    mound: 'revetment',   // riprap: revetment | breakwater | sheetCell
    gangway: false,       // float: hang a gangway off it, solved from the same tide
    gangRun: null, gangWidth: 0.95, abutZ: null,
  };
  const FIT_DEFAULT = {
    quay:    { ladder:'auto', tyre:'auto', foam:0, cleat:'auto', bollard:2, ring:2, dolphin:false },
    pier:    { ladder:'auto', tyre:'auto', foam:'auto', cleat:'auto', bollard:1, ring:2, dolphin:false },
    crib:    { ladder:'auto', tyre:'auto', foam:1, cleat:'auto', bollard:1, ring:1, dolphin:false },
    float:   { ladder:'auto', tyre:'auto', foam:1, cleat:4, bollard:0, ring:0, dolphin:false },
    gangway: { ladder:0, tyre:0, foam:0, cleat:0, bollard:0, ring:0, dolphin:false },
    slipway: { ladder:0, tyre:0, foam:0, cleat:1, bollard:0, ring:1, dolphin:false },
    riprap:  { ladder:0, tyre:0, foam:0, cleat:0, bollard:0, ring:0, dolphin:false },
  };
  // Named looks, including the four the original near-plan kit shipped (float / lowpier / tallpier /
  // quay) and four read off the reference photos. render('torbayQuay', dir) works like a family.
  const PRESETS = {
    lowPier:      { family:'pier',   deckZ:1.3, bays:4, bayLen:2.6, width:3.0, struct:'open', brace:false, capIron:false },
    tallPier:     { family:'pier',   deckZ:2.8, bays:5, bayLen:2.8, width:4.2, struct:'open', brace:true },
    sheetedPier:  { family:'pier',   struct:'sheeted', bays:5, width:4.6, curb:'wood' },
    steelPier:    { family:'pier',   struct:'steelPile', bays:5, width:4.6, capIron:true },
    concreteQuay: { family:'quay',   face:'concrete', bays:5, bayLen:3.2, width:8 },
    torbayQuay:   { family:'quay',   face:'steelSheet', curb:'yellow', bays:5, bayLen:3.2, width:7, rail:'none' },
    timberQuay:   { family:'quay',   face:'timberSheet', curb:'wood', bays:5, bayLen:3.0, width:6.5 },
    logCrib:      { family:'crib',   cap:'plank' },
    cappedCrib:   { family:'crib',   cap:'concrete', curb:'yellow' },
    timberFloat:  { family:'float',  hull:'timber' },
    plasticFloat: { family:'float',  hull:'plastic', curb:'yellow' },
    floatSet:     { family:'float',  hull:'timber', gangway:true, bays:2 },
    plasticSet:   { family:'float',  hull:'plastic', gangway:true, bays:2, curb:'yellow' },
    graniteEdge:  { family:'riprap', stone:'granite',   mound:'revetment' },
    redEdge:      { family:'riprap', stone:'sandstone', mound:'revetment' },
    breakwater:   { family:'riprap', stone:'granite',   mound:'breakwater', width:6, bays:6 },
    sheetCell:    { family:'riprap', stone:'granite',   mound:'sheetCell', width:5, bays:5, deckZ:2.6 },
  };
  function resolve(family, opts){
    opts = opts || {};
    if(PRESETS[family]){ opts = Object.assign({}, PRESETS[family], opts); family = PRESETS[family].family; }
    family = FAMILIES[family] ? family : 'pier';
    const Fm = FAMILIES[family], D = Fm.dims;
    const s = Object.assign({}, DEFAULTS, opts);
    s.family = family;
    s.bays   = clampI(s.bays   != null ? s.bays   : D.bays[2],   D.bays[0],   D.bays[1]);
    s.bayLen = clampF(s.bayLen != null ? s.bayLen : D.bayLen[2], D.bayLen[0], D.bayLen[1]);
    s.width  = clampF(s.width  != null ? s.width  : D.width[2],  D.width[0],  D.width[1]);
    s.tideRange = clampF(s.tideRange, 0.3, 14);
    s.tide   = s.tide != null ? s.tide : s.tideRange * 0.55;
    s.growth = Object.assign({}, GROWTH_ALL, opts.growth || {});
    s.fittings = Object.assign({}, FIT_DEFAULT[family], opts.fittings || {});
    // deck height: quoted as clearance above HIGHEST water so a big range lifts the whole wharf
    if(family === 'float'){
      s.freeboard = clampF(s.freeboard, 0.25, 0.8);
      s.floatDeckZ = s.tide + s.freeboard;
      s.deckZ = s.floatDeckZ;
    } else {
      const auto = s.tideRange + s.clearance;
      s.deckZ = clampF(s.deckZ != null ? s.deckZ : auto, D.deckZ[0], Math.max(D.deckZ[1], s.tideRange + 3));
    }
    if(family === 'gangway'){
      s.run = clampF(s.run != null ? s.run : D.bayLen[2], D.bayLen[0], D.bayLen[1]);
      s.floatDeckZ = opts.floatDeckZ != null ? opts.floatDeckZ : s.tide + 0.40;
      s.deckZ = clampF(s.deckZ, s.floatDeckZ + 0.15, s.floatDeckZ + s.run*0.92);
    }
    if(family === 'slipway'){
      s.run = clampF(s.run != null ? s.run : D.bayLen[2], D.bayLen[0], D.bayLen[1]);
      s.toeZ = s.toeZ != null ? s.toeZ : -0.6;
      s.deckZ = clampF(s.deckZ, s.toeZ + 0.6, s.toeZ + s.run*0.35);
    }
    s.mudZ = Math.min(s.mudZ, -0.35);
    return s;
  }
  const clampF = (v,a,b)=> Math.max(a, Math.min(b, +v || 0));
  const clampI = (v,a,b)=> Math.max(a, Math.min(b, Math.round(+v || 0)));

  // Hardware is laid out FROM THE BERTHS. Every berth gets a ladder at mid-berth and its fenders out
  // at the quarters, so a fender can never land on a ladder head; cleats go on the berth divisions,
  // bollards on the ends. Deck hardware is then allocated on reserved stations as before.
  function placements(s, T){
    const L = (s.family === 'gangway' || s.family === 'slipway') ? s.run : s.bays * s.bayLen;
    const hx = L/2, hy = s.width/2, top = s.family === 'float' ? s.floatDeckZ : s.deckZ;
    const f = s.fittings, P = { ladders:[], tyres:[], foams:[], cleats:[], bollards:[], rings:[], dolphin:null, rails:[] };
    const B = berths(s), n = (v, avail)=> v === 'auto' ? avail.length : Math.max(0, Math.min(avail.length, v|0));
    const spread = (cnt, pad)=>{ const a=[]; if(cnt<=0) return a; pad = pad==null?0.7:pad;
      for(let i=0;i<cnt;i++) a.push(-hx + pad + (L - pad*2) * (cnt===1 ? 0.5 : i/(cnt-1))); return a; };
    const fenderTop = (h)=> s.fenderZ === 'water' ? Math.min(top - h, T.w + (h < 0.25 ? 0.55 : 0.75)) : top - h;

    if(B.list.length){
      const lad = B.list.map(b => b.ladderX);
      for(const x of lad.slice(0, n(f.ladder, lad))) P.ladders.push({ x, y:hy, side:1, topZ:top, botZ:-FIT.ladder.below });
      const ty = [], fo = [];
      for(const b of B.list){ ty.push(b.fenderX[0], b.fenderX[1]); fo.push(b.fenderX[1]); }
      for(const x of ty.slice(0, n(f.tyre, ty))) P.tyres.push({ x, y:hy, side:1, top: fenderTop(0.28) });
      for(const x of fo.slice(0, n(f.foam, fo))) P.foams.push({ x, y:hy, side:1, top: fenderTop(0.20) });
    } else {
      for(const x of spread(f.ladder === 'auto' ? 1 : f.ladder, 1.0)) P.ladders.push({ x, y:hy, side:1, topZ:top, botZ:-FIT.ladder.below });
      for(const x of spread(f.tyre === 'auto' ? 2 : f.tyre, 0.9)) P.tyres.push({ x, y:hy, side:1, top: fenderTop(0.28) });
      for(const x of spread(f.foam === 'auto' ? 1 : f.foam, 1.4)) P.foams.push({ x, y:hy, side:1, top: fenderTop(0.20) });
    }
    // deck hardware: stations every ~1.3 m, ladders AND fenders reserved first
    const nSt = Math.max(2, Math.round((L - 1.2) / 1.3) + 1), st = [];
    for(let i=0;i<nSt;i++) st.push({ x: -hx + 0.6 + (L - 1.2) * (nSt === 1 ? 0.5 : i/(nSt-1)), free: true });
    const reserve = (x, gap)=>{ for(const p of st) if(Math.abs(p.x - x) < gap) p.free = false; };
    for(const l of P.ladders) reserve(l.x, 0.85);
    const take = (cnt, fromEnds, prefer)=>{ const out = [];
      if(prefer) for(const px of prefer){ if(out.length >= cnt) break;
        const p = st.reduce((best, q)=> (q.free && (!best || Math.abs(q.x - px) < Math.abs(best.x - px))) ? q : best, null);
        if(p){ p.free = false; out.push(p.x); reserve(p.x, 1.15); } }
      const order = fromEnds ? st.map((p,i)=>[p, Math.min(i, st.length-1-i)]).sort((a,b)=>a[1]-b[1]).map(a=>a[0])
                             : st.map((p,i)=>[p, Math.abs(i - (st.length-1)/2)]).sort((a,b)=>a[1]-b[1]).map(a=>a[0]);
      for(const p of order){ if(out.length >= cnt) break; if(!p.free) continue; p.free = false; out.push(p.x); reserve(p.x, 1.15); }
      return out.sort((a,b)=>a-b); };
    const divisions = B.list.length ? B.list.map(b => b.edgeX[1]).slice(0, -1).concat(B.list.map(b => b.edgeX[0])) : null;
    const cleatN = f.cleat === 'auto' ? (B.list.length ? B.list.length + 1 : 2) : f.cleat;
    for(const x of take(f.bollard === 'auto' ? 2 : f.bollard, true)) P.bollards.push({ x, y:hy - 0.52, z:top });
    for(const x of take(cleatN, false, divisions)) P.cleats.push({ x, y:hy - 0.42, z:top });
    for(const x of take(f.ring === 'auto' ? 2 : f.ring, false)) P.rings.push({ x, y:hy - 0.30, z:top });
    if(f.dolphin) P.dolphin = { x: hx + 1.1, y: hy + 0.9, z: top };
    if(s.rail && s.rail !== 'none'){
      const R = s.railSides || [];
      if(R.indexOf('shore') >= 0) P.rails.push([-hx+0.1, -hy+0.08, hx-0.1, -hy+0.08]);
      if(R.indexOf('water') >= 0) P.rails.push([-hx+0.1,  hy-0.08, hx-0.1,  hy-0.08]);
      if(R.indexOf('ends')  >= 0){ P.rails.push([-hx+0.08, -hy+0.1, -hx+0.08, hy-0.1]); P.rails.push([hx-0.08, -hy+0.1, hx-0.08, hy-0.1]); }
    }
    return Object.assign(P, { L, hx, hy, top, berths:B });
  }
  function addFittings(out, s, T){
    const P = placements(s, T);
    for(const l of P.ladders) ladderAt(out, l.x, l.y, l.side, l.topZ, T, s);
    for(const t of P.tyres)   tyreAt(out, t.x, t.y, t.side, P.top, t.top, T, s);
    for(const g of P.foams)   foamAt(out, g.x, g.y, g.side, g.top, T, s);
    for(const c of P.cleats)  cleatAt(out, c.x, c.y, c.z, 0);
    for(const b of P.bollards)bollardAt(out, b.x, b.y, b.z);
    for(const r of P.rings)   ringAt(out, r.x, r.y, r.z);
    for(const r of P.rails)   railRun(out, r[0], r[1], r[2], r[3], P.top, s.rail, s);
    if(P.dolphin) dolphinAt(out, P.dolphin.x, P.dolphin.y, P.dolphin.z, T, s);
    return P;
  }

  // float rock: locked in place, so it heaves and rolls about its own centre — no translation
  function rockXform(s, T){
    if(s.family !== 'float' || s.rock === false) return null;
    const ph = ((s.frame || 0) % 8) / 8 * Math.PI * 2;
    const roll = Math.sin(ph) * 0.85 * DEG, pitch = Math.sin(ph + 1.9) * 0.55 * DEG, heave = Math.sin(ph + 0.6) * 0.022;
    const cr = Math.cos(roll), sr = Math.sin(roll), cp = Math.cos(pitch), sp = Math.sin(pitch), z0 = T.w;
    return (p)=>{ const x=p[0], y=p[1], z=p[2]-z0;
      const x1 = x*cr + z*sr, z1 = -x*sr + z*cr;
      const y2 = y*cp - z1*sp, z2 = y*sp + z1*cp;
      return [x1, y2, z2 + z0 + heave]; };
  }

  // ============================ MATERIALS ====================================================
  function makeMats(s){
    const M = {};
    const add = (k, ramp)=>{ M[k] = { ramp }; M[k+'W'] = { ramp: ramp.map(c => mix(c, '#08161c', 0.40)) }; };
    const wthr = (ramp)=> s.weather > 0 ? ramp.map(c => desat(mix(c, '#6b6558', s.weather*0.10), s.weather*0.20)) : ramp;
    const BASES = { pole:wthr(POLE), wood:wthr(WOOD), plank:wthr(PLANK), conc:wthr(CONCRETE), stone:ROCK,
      iron:IRON, galv:GALV, alum:ALUM, rubber:RUBBER, foam:FOAM, rope:ROPE, net:NET,
      yel:wthr(YEL), poly:POLY, barn:BARN, weed:WEED, alg:ALG,
      sandst:SANDST, rust:wthr(RUST), steel:wthr(STEEL), hdpe:HDPE, hdpeDeck:HDPEDECK };
    for(const k in BASES) add(k, BASES[k]);
    // every base carries its tide-frame transforms, so matAtZ can band ANY material it meets
    for(const k in BASES){ const r = BASES[k];
      add(k+'S', r.map(c => mix(desat(c, 0.34), '#1b241f', 0.42)));               // dark tide stain / creosote bleed
      add(k+'I', r.map(c => mix(desat(c, 0.48), '#c9cbbf', 0.18)));               // ice scour scar
      add(k+'B', r.map(c => mix(desat(c, 0.42), '#a8a290', 0.46)));               // barnacle crust
      add(k+'G', r.map(c => mix(desat(c, 0.26), '#3a4020', 0.56)));               // rockweed / algae film
    }
    M.body = M.wood;
    return M;
  }

  // ============================ BAKE =========================================================
  function bake(faces, B, MATS, s, T){
    let minX=1e9, minY=1e9, maxX=-1e9, maxY=-1e9;
    const pf = [];
    for(const f of faces){ const rv = f.v.map(([x,y,z]) => projRaw(x,y,z,B));
      for(const v of rv){ if(v.sx<minX)minX=v.sx; if(v.sx>maxX)maxX=v.sx; if(v.sy<minY)minY=v.sy; if(v.sy>maxY)maxY=v.sy; }
      pf.push({ f, rv }); }
    if(!pf.length) return { data:new Uint8ClampedArray(4), w:1, h:1, px:0, py:0, wet:new Uint8Array(1) };
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
          if(deff < zbuf[i]){ zbuf[i] = deff; dep[i] = d; nbuf[i] = f.mat; let fi = fidx;
            if(tex && uv){ const uu = w0*ua[0] + w1*ub[0] + w2*uc[0], vv = w0*ua[1] + w1*ub[1] + w2*uc[1]; fi += tex(uu, vv); }
            let idx; if(flat) idx = Math.round(fi); else { const base = Math.floor(fi); idx = base + ((fi-base) > BAYER[x&3][y&3] ? 1 : 0); }
            idx = Math.max(0, Math.min(ramp.length-1, idx)); rbuf[i] = ramp; ibuf[i] = idx; } }
      }
    }
    // resolve, depth-edge darkening, weather grit, isolated-pixel cull, 1px keyline
    const out = new Array(N).fill(null);
    for(let i=0;i<N;i++) if(rbuf[i]) out[i] = rbuf[i][ibuf[i]];
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue; const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j]) > EDGE){ const far = dep[i] > dep[j] ? i : j; out[far] = rbuf[far][Math.max(0, ibuf[far]-2)]; } } }
    if(s.weather > 0.02){ const rnd = mulberry32(3181 | ((s.variant*97)|0));
      for(let i=0;i<N;i++){ const m = nbuf[i]; if(!m || !rbuf[i]) continue;
        if(/^(pole|wood|plank|conc|galv|yel|alum)/.test(m) && rnd() < s.weather*0.055) out[i] = rbuf[i][Math.max(0, ibuf[i]-1)]; } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx, ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; nbuf[i]=null; } }
    const wet = new Uint8Array(N);
    for(let i=0;i<N;i++) if(nbuf[i] && /W$/.test(nbuf[i])) wet[i] = 1;
    // KEYLINE — RETIRED BY DEFAULT (ADR 0031). Pure ring deletion: every pixel this pass writes is
    // an EMPTY neighbour of the silhouette, so switching it off changes no painted pixel of the
    // structure. What holds the edge instead is the deck/face's own dark side.
    if(s.outline === undefined ? KEYLINE_DEFAULT : s.outline !== false){
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let touch=false;
        for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx, ny=y+dy; if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
        if(touch) out[i] = KEY; }
    }
    const data = new Uint8ClampedArray(N*4);
    for(let i=0;i<N;i++){ const c = out[i];
      if(!c || (s.clipBelowWater && wet[i])){ data[i*4+3] = 0; continue; }
      const [r,g,b] = hex2rgb(c); data[i*4]=r; data[i*4+1]=g; data[i*4+2]=b; data[i*4+3]=255; }
    return { data, w:W, h:H, px:ox, py:oy, wet };
  }

  function render(family, dir, opts){
    opts = (typeof opts === 'number') ? { elev:opts } : (opts || {});
    const s = resolve(family, opts), T = frame(s), B = camBasis({ dir, elev:opts.elev });
    const out = [];
    s._rockPt = rockXform(s, T);
    FAMILIES[s.family].build(out, s, T);
    if(s.family !== 'riprap' && s.family !== 'gangway') addFittings(out, s, T);
    const rk = rockXform(s, T);
    if(rk) for(const f of out) if(!f.fixed) f.v = f.v.map(rk);
    return bake(out, B, makeMats(s), s, T);
  }

  // ============================ GAMEPLAY =====================================================
  function gameplay(family, opts){
    const s = resolve(family, opts), T = frame(s), P = placements(s, T);
    const { L, hx, hy, top } = P, walkZ = top;
    const drop = top - T.w;
    const mooring = [], blockers = [], fenders = [], ladders = [], board = [];
    P.cleats.forEach((c,i)=> mooring.push({ id:'cleat'+i, type:'cleat', x:+c.x.toFixed(2), y:+c.y.toFixed(2), z:+c.z.toFixed(2), side:'water', maxLine:'22 mm', holds:'skiff–lobster boat' }));
    P.bollards.forEach((b,i)=>{ mooring.push({ id:'bollard'+i, type:'bollard', x:+b.x.toFixed(2), y:+b.y.toFixed(2), z:+b.z.toFixed(2), side:'water', maxLine:'32 mm', holds:'packet–dragger' });
      blockers.push({ shape:'circle', x:+b.x.toFixed(2), y:+b.y.toFixed(2), r:FIT.bollard.rBase+0.04, h:FIT.bollard.h, kind:'bollard' }); });
    P.rings.forEach((r,i)=> mooring.push({ id:'ring'+i, type:'ring', x:+r.x.toFixed(2), y:+r.y.toFixed(2), z:+r.z.toFixed(2), side:'water', maxLine:'16 mm', holds:'dory–punt', flush:true }));
    if(P.dolphin) mooring.push({ id:'dolphin', type:'dolphin', x:+P.dolphin.x.toFixed(2), y:+P.dolphin.y.toFixed(2), z:+(P.dolphin.z).toFixed(2), side:'outboard', maxLine:'40 mm', holds:'anything afloat' });
    P.ladders.forEach((l,i)=> ladders.push({ id:'ladder'+i, x:+l.x.toFixed(2), y:+l.y.toFixed(2), side:'water',
      topZ:+l.topZ.toFixed(2), botZ:+l.botZ.toFixed(2), rung:FIT.ladder.rung,
      rungsDry:Math.max(0, Math.floor((top - Math.max(T.w, l.botZ)) / FIT.ladder.rung)),
      bottomSubmerged: T.w > l.botZ, reachesWater: T.w >= l.botZ }));
    P.tyres.forEach((t,i)=> fenders.push({ id:'tyre'+i, type:'tyre', x:+t.x.toFixed(2), y:+(t.y+FIT.tyre.sec/2).toFixed(2), z:+(t.top - FIT.tyre.od/2).toFixed(2), r:FIT.tyre.od/2, contacts: Math.abs((t.top - FIT.tyre.od/2) - T.w) < FIT.tyre.od*0.8 }));
    P.foams.forEach((g,i)=> fenders.push({ id:'foam'+i, type:'foam', x:+g.x.toFixed(2), y:+(g.y+FIT.foam.od/2).toFixed(2), z:+(g.top - FIT.foam.len/2).toFixed(2), r:FIT.foam.od/2, contacts: g.top - FIT.foam.len < T.w && g.top > T.w }));
    for(const r of P.rails) blockers.push({ shape:'wall', x0:+r[0].toFixed(2), y0:+r[1].toFixed(2), x1:+r[2].toFixed(2), y1:+r[3].toFixed(2), h:FIT.rail.h, kind:'rail' });
    // boarding: ONE PER BERTH, at the berth's own ladder, so every place a hull can lie has a way up.
    const ramp = s.family === 'gangway' || s.family === 'slipway';
    if(!ramp) for(const bt of P.berths.list){
      const nearLadder = P.ladders.some(l => Math.abs(l.x - bt.x) < 1.6);
      board.push({ id:'board' + bt.id.slice(5), berth:bt.id, cls:bt.cls, x:bt.x, y:+hy.toFixed(2), z:+top.toFixed(2),
        side:'water', drop:+drop.toFixed(2), gunwale:+(drop - bt.freeboard).toFixed(2),
        needsLadder: drop - bt.freeboard > 0.55 && !nearLadder, ladderInReach: nearLadder }); }
    const piles = [];
    if(s.family === 'pier'){ const across = s.width > 4.6 ? [-hy+0.42, 0, hy-0.42] : [-hy+0.42, hy-0.42];
      for(let i=0;i<=s.bays;i++) for(const y of across) piles.push({ x:+(-hx + i*s.bayLen).toFixed(2), y:+y.toFixed(2), r:s.pileR, topZ:+(top - DECK.plank - DECK.stringer[1] - DECK.cap[1]).toFixed(2) }); }
    if(s.family === 'float' && s.guidePiles) for(const gx of (s.bays > 1 ? [-hx+0.4, hx-0.4] : [hx-0.4])){
      piles.push({ x:+gx.toFixed(2), y:+(hy+0.34).toFixed(2), r:s.pileR, topZ:+(T.hhw + s.guideAbove).toFixed(2), guide:true });
      blockers.push({ shape:'circle', x:+gx.toFixed(2), y:+(hy+0.34).toFixed(2), r:s.pileR+0.10, h:1.6, kind:'guidePile' }); }
    // The walkable surface. A ramp's surface is SLOPED, so it publishes the z at each end and the
    // law between them — a flat rectangle at the top hinge would float the collision in mid-air.
    const rz0 = ramp ? s.deckZ : top;                                     // at x = -L/2 (shore / hinge)
    const rz1 = s.family === 'gangway' ? s.floatDeckZ + 0.06 : s.family === 'slipway' ? s.toeZ : top;
    const walk = [{ poly:[[-hx,-hy],[hx,-hy],[hx,hy],[-hx,hy]].map(p=>[+p[0].toFixed(2),+p[1].toFixed(2)]),
      z:+((rz0 + rz1)/2).toFixed(2),
      surface: s.family === 'quay' ? 'concrete' : s.family === 'gangway' ? 'alloy-tread'
             : s.family === 'slipway' ? 'concrete-ribbed' : s.family === 'riprap' ? 'loose-stone' : 'plank',
      slope: +((rz0 - rz1) / L).toFixed(3),
      wet: s.family === 'slipway' ? 'below mid tide' : 'spray only',
      rides: s.family === 'float' }];
    if(ramp){ Object.assign(walk[0], { axis:'x', z0:+rz0.toFixed(2), z1:+rz1.toFixed(2),
      angleDeg:+(Math.atan2(rz0 - rz1, L) * 180/Math.PI).toFixed(1),
      zAtX:'z0 + (z1 - z0) * ((x + L/2) / L)',
      submergedFrom: rz1 < T.w ? +(-hx + L * ((rz0 - T.w)/(rz0 - rz1))).toFixed(2) : null }); }
    if(s.family === 'float') walk[0].rock = { rollDeg:0.85, pitchDeg:0.55, heaveM:0.022, frames:8, locked:true };
    // where a hull meets a ramp: the gangway's landing, the slipway's water edge at THIS tide
    const ends = ramp ? (s.family === 'gangway'
      ? { hinge:{ x:+(-hx).toFixed(2), z:+rz0.toFixed(2), fixedTo:'deck' },
          landing:{ x:+hx.toFixed(2), z:+rz1.toFixed(2), fixedTo:'float', rollers:true } }
      : { crest:{ x:+(-hx).toFixed(2), z:+rz0.toFixed(2) },
          waterEdge: walk[0].submergedFrom, toe:{ x:+hx.toFixed(2), z:+rz1.toFixed(2) },
          launchable: T.w > rz1 + 0.35 }) : null;
    return {
      family: s.family, label: FAMILIES[s.family].label,
      footprint: { w:+L.toFixed(2), d:+s.width.toFixed(2) },
      deckZ:+top.toFixed(2), waterZ:+T.w.toFixed(2), tideRange:+T.R.toFixed(2),
      freeboard:+drop.toFixed(2), mudZ:s.mudZ,
      zones: { stainTop:+T.stainTop.toFixed(2), barn:[+T.barnBot.toFixed(2), +T.barnTop.toFixed(2)],
      weed:[+T.weedBot.toFixed(2), +T.weedTop.toFixed(2)], ice:[+T.iceBot.toFixed(2), +T.iceTop.toFixed(2)] },
      walk, blockers, mooring, board, ladders, fenders, piles, ends,
      styles: { face:s.face, struct:s.struct, cap:s.cap, hull:s.hull, stone:s.stone, mound:s.mound, curb:s.curb },
      berths: P.berths.list.map(bt => Object.assign({}, bt, {
        drop:+drop.toFixed(2), clear:+(drop - bt.freeboard).toFixed(2),
        ladder: P.ladders.some(l => Math.abs(l.x - bt.x) < 1.6) })),
      berthClass: P.berths.cls ? P.berths.cls.id : null, berthClearance: BERTH_CLR,
      berthUtilisation: P.berths.utilisation != null ? P.berths.utilisation : null,
      gangway: (s.family === 'float' && s.gangway) ? {
        run: +(s.gangRun != null ? clampF(s.gangRun, 3, 14) : Math.max(3, Math.min(14, (T.R + s.freeboard + 0.9) * 2.4))).toFixed(2),
        width: s.gangWidth, abutZ: +(s.abutZ != null ? s.abutZ : T.R + s.clearance).toFixed(2),
        landZ: +top.toFixed(2), ridesFloat: true, hingeFixed: true,
        angleDeg: +(Math.atan2((s.abutZ != null ? s.abutZ : T.R + s.clearance) - top,
          (s.gangRun != null ? clampF(s.gangRun, 3, 14) : Math.max(3, Math.min(14, (T.R + s.freeboard + 0.9) * 2.4)))) * 180/Math.PI).toFixed(1),
      } : null,
      snap: { px:{ x:+hx.toFixed(2), y:0, z:+top.toFixed(2), face:'+x' }, nx:{ x:+(-hx).toFixed(2), y:0, z:+top.toFixed(2), face:'-x' } },
      exposure: {
        pileExposed: +Math.max(0, T.w - s.mudZ).toFixed(2) === 0 ? 0 : +Math.max(0, (top - T.w)).toFixed(2),
        barnacleShowing: T.w < T.barnTop, weedShowing: T.w < T.weedTop, iceScarShowing: T.w < T.iceBot,
      },
    };
  }
  function project(dir, p, elev){ const v = projRaw(p[0], p[1], p[2], camBasis({ dir, elev })); return { x:v.sx, y:v.sy }; }
  // anchors in CELL pixels: pass the render result so the pivot lines up with the baked cell
  function anchors(family, dir, opts, cell){
    const g = gameplay(family, opts), e = (opts||{}).elev;
    const ox = cell ? cell.px : 0, oy = cell ? cell.py : 0;
    const P = (p)=>{ const q = project(dir, p, e); return { x:+(q.x+ox).toFixed(1), y:+(q.y+oy).toFixed(1) }; };
    return {
      mooring: g.mooring.map(m => Object.assign({}, m, P([m.x, m.y, m.z]))),
      ladders: g.ladders.map(l => Object.assign({}, l, { top:P([l.x,l.y,l.topZ]), bot:P([l.x,l.y,l.botZ]) })),
      board:   g.board.map(b => Object.assign({}, b, P([b.x, b.y, b.z]))),
      fenders: g.fenders.map(f => Object.assign({}, f, P([f.x, f.y, f.z]))),
      piles:   g.piles.map(p => Object.assign({}, p, P([p.x, p.y, p.topZ]))),
      snap:    { px:P([g.snap.px.x,0,g.snap.px.z]), nx:P([g.snap.nx.x,0,g.snap.nx.z]) },
      deckZ: g.deckZ, waterZ: g.waterZ,
    };
  }
  function list(){ return Object.keys(FAMILIES); }

  root.WharfIso = { PX, S, DIRS:8, defaultElev:DEFAULT_ELEV, order:['N','NE','E','SE','S','SW','W','NW'],
    FAMILIES, PRESETS, BOATS, BERTH_CLR, FIT, DECK, WOOD, PLANK, POLE, IRON, GALV, ALUM, CONCRETE, ROCK,
    SANDST, RUST, STEEL, HDPE, HDPEDECK, RUBBER, FOAM, ROPE, NET, YEL, POLY, BARN, WEED, ALG, KEY,
    KEYLINE_DEFAULT,
    GROWTH_ALL, FIT_DEFAULT,
    STYLES: { face:['concrete','steelSheet','timberSheet'], struct:['open','sheeted','steelPile'],
      cap:['plank','concrete'], hull:['timber','plastic'], stone:['granite','sandstone'],
      mound:['revetment','breakwater','sheetCell'], curb:['wood','yellow','none'] },
    list, presets: ()=>Object.keys(PRESETS), resolve, frame, placements, berths, makeMats,
    render, gameplay, anchors, project };
})(typeof globalThis !== 'undefined' ? globalThis : window);
