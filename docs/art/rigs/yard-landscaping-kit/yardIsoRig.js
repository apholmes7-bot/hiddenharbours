/* Hidden Harbours — parametric ISO DOORYARD & LANDSCAPING rig (ADR-0006 bake pipeline, SAME
   turntable + camera + shading as houseIsoRig.js / utilityIsoRig.js / wharfDecorRig.js / the fleet).
   The house rig gives the building, the road rig gives the street, the tree / shrub / flower rigs give
   the wild plants. This rig gives everything BETWEEN them: the fence, the hedge, the bed, the barrel
   planter, the clothesline, the woodpile, the buoy post and the trap bench — the stuff a townsperson
   puts on their own lawn. It is what makes one house on a street read as lived-in and the next as
   let-go.

   Each piece is one parametric 3D model baked through the SHARED 3/4 camera: 45deg steps, elev 40deg,
   flat-facet shading from the fixed upper-LEFT key, z-buffered, ordered dither, per-face uv texture,
   depth-edge darkening, 1px keyline, NO AA. 32 px = 1 m. All 8 facings fall out of one model.
   PROP ORIGIN: ground-centre of the footprint (== the house / utility / wharf pivot), so a piece drops
   onto a lawn tile with no offset maths. Wall-backed pieces (window box, rain barrel, foundation bed)
   carry their back plane toward -Y, so the house stands behind them and the face reads.

   TWO AXES THIS RIG OWNS THAT THE OTHER PROP RIGS DON'T:

   1. KEPT — 0..1 upkeep, and it is not a paint fade. One number moves SEVEN things at once, because
      that is what "kept" actually looks like: paint chalks and peels (kept 0 loses whole pickets),
      posts lean off plumb, lines sag, tools and pots get left where they were used, mown edges give
      way to a weed skirt at every base, mulch beds go to grass, and the hedge stops being clipped —
      its top profile roughness is a direct function of (1-kept). A row of houses at 0.15 / 0.55 / 0.95
      reads as three different families, not three different colour grades.

   2. PHASE — the shrubIsoRig 8-station calendar, verbatim (dormant · catkin · bloom · leaf · green ·
      fruit · turn · bare), so a hedge, a bed and a wild alder behind the fence can never disagree
      about what month it is. Foliage fullness, colour, bloom and berry all derive from ONE table.
      `snow` defaults off the phase (deep in dormant, a dusting in bare) and caps horizontal surfaces —
      a fence rail, a hedge top, a bench seat, a woodpile.

   BEDS COMPOSE, HEDGES ARE NATIVE. A planted bed asks globalThis.Flowers / globalThis.Shrubs for real
   sprites and blits them back-to-front over its own soil (same PPU, same 40deg elevation, so they land
   true); if those rigs are not loaded the bed falls back to native foliage mounds and still reads. A
   hedge is native — a clipped hedge is a MASS with a machined top, not a row of shrubs, and the two
   are drawn by different rules.

   Each piece reads a resolved spec:
     paint:BODY key|null   variant:int   len:0..1 (panel / run length)   kept:0..1
     phase:PHASE key   snow:0..1   weather:0..1 (grime + sun-silvered timber)   night:bool
     planted:bool (beds and planters carry plants)   seed:int
   Exposes globalThis.YardIso = { W,H,PX,pivot,order,defaultElev, PROPS,CATS,CAT_LABEL,PHASES,
     BODY,..., list(), footprint(), height(), mounts(), anchors(), render(name,dir,opts),
     project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 470, H = 540, cx = 235, groundY = 442;
  const DEG = Math.PI / 180, DEFAULT_ELEV = 40;

  // ---- palettes (harbour master ramps, shared with the houses, services & wharf) ----
  const BODY = {
    white:       ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    cream:       ['#8a6f3c','#a6884b','#c2a35f','#d8bd7c','#e9d59d','#f5e7c1'],
    red:         ['#4a130f','#671b14','#88271c','#a33124','#bd4230','#d25a42'],
    sage:        ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'],
    blue:        ['#33454a','#43585d','#556d72','#6a848a','#849ea3','#a3b9bd'],
    greyShingle: ['#4c463f','#5d564c','#6f665a','#82786a','#968b7b','#a99d8c'],
    gold:        ['#5e4a12','#7c6119','#987a26','#b39440','#c8ab5e','#dbc182'],
    teal:        ['#123a3a','#1b4d4b','#26635e','#357b73','#4d968b','#6cb1a4'],
    rustOrange:  ['#4a2410','#6a3514','#8c481a','#a85f27','#c07a3a','#d49657'],
    plum:        ['#2e2333','#3f3047','#523f5d','#664f73','#7d648b','#9079a1'],
  };
  const WOOD   = ['#553d23','#6b4e30','#7f603b','#957749','#ab8d5f','#c1a578']; // sawn timber
  const PLANK  = ['#454037','#565045','#676054','#7a7265','#8d8477','#a1988a']; // sun-silvered board
  const POLE   = ['#33291f','#42342a','#523f33','#63503f','#75604c','#8a7460']; // cedar post / creosote
  const IRON   = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970'];
  const GALV   = ['#565b5f','#6d7276','#868b8f','#a0a5a8','#bbbfc1','#d6d9da'];
  const TIN    = ['#4a5054','#5f676b','#767e82','#8d9599','#a5adb0','#bec5c7']; // mailbox sheet
  const CONCRETE=['#55564f','#686962','#7c7d74','#919287','#a6a79b','#bbbcae'];
  const STONE  = ['#3b3d3a','#4c4f4a','#5f625b','#74776d','#8a8d81','#a1a396']; // fieldstone
  const SLATEF = ['#33383b','#434a4d','#555d60','#6a7376','#808a8c','#98a1a3']; // flagstone
  const BRICK  = ['#3a201a','#552b20','#6e3728','#874634','#a05743','#b96b55'];
  const CLAY   = ['#4f2517','#6b3620','#88492b','#a35f3b','#bb7852','#d1936d']; // terracotta pot
  const GRAVEL = ['#3b3a33','#4b493f','#5c5a4d','#6f6c5c','#83806e','#979382'];
  const SHELL  = ['#6b675c','#827d70','#9a9484','#b1ab99','#c7c1ae','#dcd6c3']; // crushed oyster shell
  const SOIL   = ['#241a12','#31241a','#3f2f22','#4e3b2b','#5e4936','#6f5843'];
  const MULCH  = ['#2c1d14','#3c281a','#4c3423','#5e422d','#71533a','#846649'];
  const TURF   = ['#26361f','#33472a','#425a35','#526e42','#648451','#789b63']; // mown lawn
  const ROPE   = ['#5a4526','#71592f','#8a6f3d','#a2874e','#b89f66','#cdb686'];
  const CANVAS = ['#3d4038','#4b4f45','#5b6053','#6d7263','#7f8474','#929686'];
  const RUBBER = ['#141517','#1d1f22','#282b2f','#34383d','#43484d','#54595f'];
  const SNOWR  = ['#7d8a95','#98a5ae','#b3bfc6','#ccd6db','#e0e8ec','#f2f7f9'];
  const LINEN  = ['#8a8f8a','#a3a79f','#bcbfb4','#d1d3c8','#e3e5da','#f2f3ea']; // laundry
  const GLASSD = ['#7d9ea6','#a1c2c6','#cfe6e8'], GLASSN = ['#141d2b','#233247','#3d5570'];
  const GLOW   = ['#7a5a18','#c09a2c','#efd06a','#fdf0b6'];
  const KEY    = '#1a1c22';

  // ---- foliage: ONE table, shared by hedges, beds, planters and the weed skirt ----
  const FOL = {
    green:  ['#1e2f1c','#2b4326','#395832','#4a6f3f','#5c874e','#71a061'],
    fresh:  ['#243620','#344c2a','#456237','#587c46','#6d9757','#84b06c'],   // spring flush
    turn:   ['#38240f','#513718','#6b4c20','#87652c','#a3813e','#bd9d56'],
    winter: ['#2b2620','#3a342b','#4a4237','#5b5245','#6d6354','#807565'],   // bare twig
    ever:   ['#17281f','#213629','#2c4634','#3a5941','#496d4f','#5b8460'],   // cedar / spruce hedge
  };
  const BLOOM = {
    lilac:'#9b7fc4', rose:'#d97f9c', white:'#e8e4d6', gold:'#e0b23a',
    red:'#c04234', blue:'#6e86c4', hip:'#b8442c', berry:'#5f7ba8',
  };
  // the shrubIsoRig calendar, verbatim — one clock for every plant in the world
  const PHASES = [
    { key:'dormant', label:'DORMANT', when:'Feb' },
    { key:'catkin',  label:'CATKIN',  when:'early Apr' },
    { key:'bloom',   label:'BLOOM',   when:'late May' },
    { key:'leaf',    label:'LEAF',    when:'Jun' },
    { key:'green',   label:'GREEN',   when:'Jul' },
    { key:'fruit',   label:'FRUIT',   when:'Aug' },
    { key:'turn',    label:'TURN',    when:'early Oct' },
    { key:'bare',    label:'BARE',    when:'Nov' },
  ];
  const PHASE_KEYS = PHASES.map(p => p.key);
  const LEAFING   = { dormant:0, catkin:0.02, bloom:0.55, leaf:0.88, green:1, fruit:1, turn:0.94, bare:0.06 };
  const COLOURING = { dormant:'winter', catkin:'winter', bloom:'fresh', leaf:'fresh', green:'green', fruit:'green', turn:'turn', bare:'turn' };
  const SNOW_BY_PHASE = { dormant:0.85, catkin:0.12, bloom:0, leaf:0, green:0, fruit:0, turn:0, bare:0.18 };
  // lawn colour follows the calendar too — a dooryard in Feb is not July green
  const TURF_BY_PHASE = { dormant:0.9, catkin:0.6, bloom:0.15, leaf:0.05, green:0, fruit:0.12, turn:0.35, bare:0.6 };

  // ---- shading (identical recipe to utilityIsoRig / wharfDecorRig) ----
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));
  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }
  function rampFrom(hex){ // build a 6-step ramp around one colour, value structure of the master ramps
    return [mix(hex,'#141a18',0.62), mix(hex,'#141a18',0.38), mix(hex,'#141a18',0.14),
            hex, mix(hex,'#f4f1e4',0.2), mix(hex,'#f4f1e4',0.42)];
  }

  function camBasis(opts){ const dir=opts.dir||0, th=dir*Math.PI/4, e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { th, ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) }; }
  function projVert(x,y,z,B){ const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:groundY-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) }; }
  function normal(a,b,c){ const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx; const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m]; }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }

  // ---- face builders (outward-normal winding, wharfDecor recipe) ----
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
    const e1=nrm(crs(a,u)), e2=nrm(crs(u,e1)); n=n||10; b=b||0;
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
  const beam = (out,p0,p1,r,mat,b,n)=> tube(out,p0,p1,r,n||4,mat,b);
  const pipe = (out,x,y,r,z0,z1,mat,b,n,tex)=> tube(out,[x,y,z0],[x,y,z1],r,n||10,mat,b,tex);
  function prismT(out,x,y,r,z0,z1,n,mat,b,texTop){ tube(out,[x,y,z0],[x,y,z1],r,n,mat,b,null,false);
    const p=[]; for(let i=0;i<n;i++){ const t=(i/n)*Math.PI*2; p.push([x+Math.cos(t)*r, y-Math.sin(t)*r]); }
    slab(out, p, z1, mat, (b||0)+0.24, texTop||null); }
  function decalY(out, yv, ny, xs,xe, z0,z1, mat, b, tex, flat, db){ const e=0.02*ny, uw=xe-xs, uh=z1-z0;
    const P = ny>0 ? [[xs,yv+e,z0],[xe,yv+e,z0],[xe,yv+e,z1],[xs,yv+e,z1]] : [[xe,yv+e,z0],[xs,yv+e,z0],[xs,yv+e,z1],[xe,yv+e,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat)); }
  function decalX(out, xv, nx, ys,ye, z0,z1, mat, b, tex, flat, db){ const e=0.02*nx, uw=ye-ys, uh=z1-z0;
    const P = nx>0 ? [[xv+e,ye,z0],[xv+e,ys,z0],[xv+e,ys,z1],[xv+e,ye,z1]] : [[xv+e,ys,z0],[xv+e,ye,z0],[xv+e,ye,z1],[xv+e,ys,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat)); }
  const dY=(out,y,n,a,b2,z0,z1,mat,bias,db)=>decalY(out,y,n,a,b2,z0,z1,mat,bias,null,true,db==null?0.06:db);
  const dX=(out,x,n,a,b2,z0,z1,mat,bias,db)=>decalX(out,x,n,a,b2,z0,z1,mat,bias,null,true,db==null?0.06:db);
  function decalZ(out, zv, xs,xe, ys,ye, mat, b, tex, db){
    out.push(F([[xs,ys,zv],[xe,ys,zv],[xe,ye,zv],[xs,ye,zv]], mat, b||0, db!=null?db:0.05,
      tex?[[0,0],[xe-xs,0],[xe-xs,ye-ys],[0,ye-ys]]:null, tex||null, false)); }

  // ---- textures ----
  function grainTex(p){ p=p||0.3; return (u,v)=>{ const f=((v%p)+p)%p; if(f<0.03) return -2;
    return hash2(Math.floor(v/p)|0, Math.floor(u*3)|0)<0.5?0:-1; }; }
  function plankTex(p){ p=p||0.22; return (u,v)=>{ const f=((u%p)+p)%p; if(f<0.026) return -2;
    return hash2(Math.floor(u/p)|0, Math.floor(v*2.4)|0)<0.42?-1:0; }; }
  function slatTex(p){ p=p||0.085; return (u,v)=>{ const f=((u%p)+p)%p; return f<p*0.22?-2:(f>p*0.8?0:-1); }; }
  function meshTex(c){ c=c||0.062; return (u,v)=>{ const fu=((u%c)+c)%c, fv=((v%c)+c)%c;
    return (fu<0.019||fv<0.019)?1:-3; }; }
  function gravelTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*13)|0, Math.floor(v*13)|0); return h<0.3?-1:(h>0.86?1:0); }; }
  function stoneTex(){ return (u,v)=>{ // dry-laid fieldstone: irregular blocks with dark joints
    const row=Math.floor(v/0.24), off=(row&1)?0.16:0, cu=Math.floor((u+off)/0.34);
    const fu=(((u+off)%0.34)+0.34)%0.34, fv=((v%0.24)+0.24)%0.24;
    if(fu<0.035||fv<0.032) return -3;
    const h=hash2(cu,row); return h<0.3?-1:(h>0.74?1:0); }; }
  function soilTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*9)|0, Math.floor(v*9)|0); return h<0.28?-1:(h>0.9?1:0); }; }
  function leafTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*17)|0, Math.floor(v*17)|0);
    return h<0.3?-1:(h>0.82?1:0); }; }
  function canvasTex(){ return (u,v)=>{ const h=hash2(Math.floor(u*24)|0, Math.floor(v*24)|0); return h<0.24?-1:(h>0.9?1:0); }; }
  function knitTex(){ const c=0.05; return (u,v)=>{ const fu=((u%c)+c)%c; return fu<c*0.3?-1:0; }; }

  // ================= SHARED SUB-ASSEMBLIES =================
  // A round or square post, optionally out of plumb. `lean` is in radians at the base.
  function post(out, x,y, z0,z1, r, mat, b, lean, leanDir, square, tex){
    const h=z1-z0, dx=Math.sin(lean||0)*h*Math.cos(leanDir||0), dy=Math.sin(lean||0)*h*Math.sin(leanDir||0);
    if(square){ // square post as a 4-sided tube so the lean comes free
      tube(out,[x,y,z0],[x+dx,y+dy,z1], r*1.28, 4, mat, b, tex);
    } else tube(out,[x,y,z0],[x+dx,y+dy,z1], r, 8, mat, b, tex);
    return [x+dx, y+dy, z1];
  }
  // a pointed picket / paling — square post with a chamfered cap
  function picket(out, x,y, z0,z1, hw, thick, mat, b, pointed, lean){
    const dx=Math.sin(lean||0)*(z1-z0);
    box(out, x-hw+dx*0.0, x+hw, y-thick, y+thick, z0, pointed?z1-hw*1.25:z1, mat, b, null, !pointed?false:true);
    if(pointed){ const zt=z1-hw*1.25;
      out.push(F([[x-hw,y-thick,zt],[x+hw,y-thick,zt],[x+dx,y-thick,z1]], mat, (b||0)+0.1));
      out.push(F([[x+hw,y+thick,zt],[x-hw,y+thick,zt],[x+dx,y+thick,z1]], mat, (b||0)+0.1));
      out.push(F([[x+hw,y-thick,zt],[x+hw,y+thick,zt],[x+dx,y+thick,z1],[x+dx,y-thick,z1]], mat, (b||0)+0.35));
      out.push(F([[x-hw,y+thick,zt],[x-hw,y-thick,zt],[x+dx,y-thick,z1],[x+dx,y+thick,z1]], mat, (b||0)-0.15));
    }
  }
  // WEED SKIRT — the single strongest "kept" tell. Blades of unmown grass at the foot of a thing.
  function weeds(out, x0,x1, y0,y1, amt, seed, mat, tall){
    if(amt<=0.02) return;
    const rnd=mulberry32((seed|0)||7), n=Math.max(2, Math.round(amt*((x1-x0)+(y1-y0))*11));
    for(let i=0;i<n;i++){
      const edge=rnd(); let px,py;
      if(edge<0.5){ px=x0+rnd()*(x1-x0); py=(rnd()<0.5?y0:y1)+(rnd()-0.5)*0.09; }
      else { px=(rnd()<0.5?x0:x1)+(rnd()-0.5)*0.09; py=y0+rnd()*(y1-y0); }
      const h=(0.10+rnd()*0.20)*(tall?1.7:1)*(0.5+amt);
      const lean=(rnd()-0.5)*0.5, la=rnd()*Math.PI*2;
      beam(out,[px,py,0],[px+Math.sin(lean)*h*Math.cos(la), py+Math.sin(lean)*h*Math.sin(la), h],
        0.014+rnd()*0.012, mat||'weed', -0.2+rnd()*0.9, 3);
    }
  }
  // snow cap over a horizontal rectangle
  function snowCap(out, x0,x1,y0,y1, z, amt, b){
    if(amt<=0.03) return; const d=0.02+amt*0.075;
    box(out, x0-0.012,x1+0.012, y0-0.012,y1+0.012, z, z+d, 'snow', (b||0)+0.2, null, false);
  }
  // ---- FOLIAGE ---------------------------------------------------------------
  // CLUMP — the foliage unit, borrowed from treeIsoRig's blob() and rebuilt in face space: a lobed
  // ellipsoid whose radius carries two LOW harmonics plus an under-weighted tooth term, so the
  // silhouette scallops into leaf ends and breaks into teeth on the underside, where leaf mass
  // actually hangs. Faces carry real normals, so the rig's own shading rounds them — which is the
  // thing a field of vertical columns could never do, however carefully it was toned.
  function clump(out, cx,cy,cz, rx,ry,rz, mat, matDark, b, seed, teeth, lod){
    const p1=(seed||0)*1.7, p2=(seed||0)*3.1, p3=(seed||0)*5.3;
    // LOW POLY ON PURPOSE: the biggest clump in this rig is ~6 px of radius, so 14 facets already
    // give more silhouette than the pixel grid can hold. The lobes come from the radius harmonics,
    // not from tessellation — and every extra facet is a face through the software rasteriser.
    const nt = lod ? 5 : 7, nv = 2;
    const phB=-0.62, phT=Math.PI/2, T=(teeth==null?0.16:teeth);
    const K=(th,ph)=>{ const under=0.5-0.5*Math.sin(ph);
      return 1 + 0.15*Math.sin(3*th+p1) + 0.085*Math.sin(5*th+p2)
               + T*(0.3+0.7*under)*Math.sin(7*th+p3); };
    const V=(i,j)=>{ const th=(i/nt)*Math.PI*2, ph=phB+(phT-phB)*(j/nv), k=K(th,ph);
      const c=Math.cos(ph), sn=Math.sin(ph);
      return [cx+Math.cos(th)*rx*k*c, cy+Math.sin(th)*ry*k*c, cz+sn*rz*(1+0.1*Math.sin(2*th+p2))]; };
    // no per-pixel texture: at 5 px a clump is carried by its facet tone, and a uv callback per
    // pixel is the most expensive thing this rasteriser can be asked to do.
    for(let j=0;j<nv;j++) for(let i=0;i<nt;i++){
      const q0=V(i,j), q1=V(i+1,j), q2=V(i+1,j+1), q3=V(i,j+1);
      const ph=phB+(phT-phB)*((j+0.5)/nv), low = ph < -0.1;
      // tone from WORLD position, so the mottle never lines up with the build lattice
      const hh=hash2(Math.floor((q0[0]+40)/0.08)*7 + Math.floor((q0[1]+40)/0.08),
                     Math.floor(q0[2]/0.08)*13 + ((seed|0)&255));
      out.push(F([q0,q1,q2,q3], low?matDark:mat, (b||0)+(hh-0.5)*0.7+(low?-0.3:0)));
    }
  }
  // FOLIAGE MASS — hedge, shrub mound, bed mound, planter spill. TWO parts, and the split is the
  // whole trick: CROWNS are one jittered clump per cell of the top surface, and the FLANK is a
  // brick-staggered ring walked around the plan silhouette by arc length. Stacking flank clumps per
  // cell (or extruding columns) prints vertical files of bumps down the sides — the corduroy this
  // rig had before. Staggered courses, jittered radii and a taper toward the base read as leaf mass.
  // kept high = tight clip, small teeth, flat shoulders; kept low = shaggy, toothed, breakout shoots.
  // o.z0 lifts the whole mass (a planter's soil is not the ground); o.taper narrows the base
  // (negative batters it wider, which is what a cedar screen does).
  function foliageMass(out, cxx,cyy, w,d,h, o){
    o=o||{}; const rough=o.rough==null?0.05:o.rough, full=o.full==null?1:o.full;
    const seed=o.seed||11, mat=o.mat||'leaf', matDark=o.matDark||mat, snow=o.snow||0, z0=o.z0||0;
    const planU=o.plan||(w>d*1.6?6:2.4), planV=o.planV||2.3;
    const domeK=o.flatTop?0.20:0.52;              // a clipped hedge has shoulders, not a dome
    const teeth=Math.max(0.05, Math.min(0.32, 0.06+rough*0.55));
    const taper=o.taper!=null?o.taper:(planU>3?0.06:0.26);
    // how far off the clip the mass has grown. A shears-kept hedge wants EVEN courses and one clump
    // size; a let-go one wants every clump a different size and off its line. Same primitive, and
    // this is most of what makes kept legible at 32 ppu.
    const shag=Math.min(1, 0.25+rough*2.2);
    // a leaf clump is 5-7 px at 32 ppu and does NOT scale with the hedge: a bigger mass carries MORE
    // clumps, never fatter ones (treeIsoRig's MIN_R rule, stated in metres).
    const fs=full>=0.9?1:(0.58+0.42*full);      // a half-leafed clump is a smaller clump
    const cr=Math.max(0.07, Math.min(0.185, (o.cell||0.115)*1.5, Math.min(w,d)*0.6, h*0.62))*fs;
    const step=cr*1.22;
    const nx=Math.max(1,Math.round(w/step)), ny=Math.max(1,Math.round(d/step));
    const dome=(rad)=>Math.pow(Math.max(0.05, 1-Math.pow(Math.min(1,rad),1.5)*domeK), 0.55);
    const live=[], cells=[];
    for(let i=0;i<nx;i++){ live.push([]);
      for(let j=0;j<ny;j++){
        const u=nx===1?0:((i+0.5)/nx*2-1), v=ny===1?0:((j+0.5)/ny*2-1);
        const rad=Math.pow(Math.abs(u),planU)+Math.pow(Math.abs(v),planV);
        const jit=(hash2(i*3+seed, j*5+seed)-0.5)*(0.2+rough*0.5);
        // leaf-off is a REAL loss, not a thinning: dormant carries no mass at all (the twig cage and
        // the snow drift are the silhouette then), and what survives in bare/catkin also shrinks.
        const on = rad<=1+jit && (full>=0.97 || (full>0.03 && hash2(i*13+seed, j*17+seed) <= 0.08+full*0.9));
        live[i].push(on);
        if(!on) continue;
        const n1=hash2(i+seed, j*3+seed)-0.5, n2=hash2(i*5+seed, j+seed*3)-0.5;
        let top=h*dome(rad)+n1*rough*h*0.7+n2*rough*h*0.3;
        if(rough>0.22 && hash2(i*7+seed, j*11)>0.93) top+=h*rough*0.7;   // a breakout shoot
        cells.push({ x:cxx-w/2+(i+0.5)*w/nx, y:cyy-d/2+(j+0.5)*d/ny,
          top:Math.max(cr*1.15, top), rad, i, j });
      }
    }
    if(!cells.length) return;
    // ---- crowns ---------------------------------------------------------------
    for(const c of cells){
      const sd=(c.i*7+c.j*13+seed)|0;
      const jx=(hash2(c.i*9+seed, c.j*3+1)-0.5)*step*0.34*shag, jy=(hash2(c.i*5+2, c.j*11+seed)-0.5)*step*0.34*shag;
      const rr=cr*(1-0.17*shag+hash2(c.i*21+seed, c.j*7+5)*0.32*shag);
      const crownZ=z0+c.top-rr*0.55;
      // interior fill: never seen, but it stops a shell of clumps from being see-through
      const inner = c.i>0&&c.j>0&&c.i<nx-1&&c.j<ny-1 &&
        live[c.i-1][c.j]&&live[c.i+1][c.j]&&live[c.i][c.j-1]&&live[c.i][c.j+1];
      if(inner && crownZ-z0>cr*0.9)
        box(out, c.x-(w/nx)*0.7, c.x+(w/nx)*0.7, c.y-(d/ny)*0.7, c.y+(d/ny)*0.7,
          z0, crownZ-cr*0.2, matDark, -0.55, leafTex(), true);
      clump(out, c.x+jx, c.y+jy, crownZ, rr, rr*0.95, rr*0.88, mat, matDark, 0, sd, teeth, false);
      if(snow>0.05 && hash2(c.i+seed*2, c.j+seed*5) < snow*0.85)
        clump(out, c.x+jx, c.y+jy, z0+c.top-rr*0.22, rr*0.94, rr*0.9, rr*0.42,
          'snow','snowDark', 0.3, sd+91, teeth*0.5, true);
    }
    // ---- flank: a staggered ring walked round the silhouette by arc length -----
    if(h < cr*1.7) return;                       // a flat mound is all crown, no sides to build
    const SE=(t)=>{ const c=Math.cos(t), sn=Math.sin(t);
      const u=(c<0?-1:1)*Math.pow(Math.abs(c), 2/planU), v=(sn<0?-1:1)*Math.pow(Math.abs(sn), 2/planV);
      return [cxx+u*w/2, cyy+v*d/2]; };
    const NS=96, pts=[], cum=[0];
    for(let k=0;k<=NS;k++) pts.push(SE(k/NS*Math.PI*2));
    for(let k=1;k<=NS;k++) cum.push(cum[k-1]+Math.hypot(pts[k][0]-pts[k-1][0], pts[k][1]-pts[k-1][1]));
    const PER=cum[NS], nP=Math.max(4, Math.round(PER/(cr*1.12))), rimH=h*dome(1);
    const posAt=(arc)=>{ const t=((arc%PER)+PER)%PER; let s=1; while(s<NS && cum[s]<t) s++;
      const f=(t-cum[s-1])/Math.max(1e-6, cum[s]-cum[s-1]);
      return [pts[s-1][0]+(pts[s][0]-pts[s-1][0])*f, pts[s-1][1]+(pts[s][1]-pts[s-1][1])*f]; };
    for(let k=0;k<nP;k++){
      if(full<0.96 && (full<=0.03 || hash2(k*29+seed, 7) > 0.11+full*0.89)) continue;
      const arcK=PER*k/nP, hj=hash2(k*17+seed, k*5+3);
      const rimZ=Math.max(cr*1.3, rimH*(1-0.1*shag+hj*0.2*shag) + (hj-0.5)*rough*h*0.55);
      const base=cr*0.55, stag=(k%2)?cr*0.42:0;
      let z=rimZ-cr*0.5-stag, m=0;
      while(z>base*0.75 && m<40){
        const f2=Math.max(0, Math.min(1, (z-base)/Math.max(0.001, rimZ-base)));
        const sc=1-taper*(1-f2);
        const rr=cr*(1-0.2*shag+hash2(k*13+m*7+seed, m*3+1)*0.34*shag);
        // BRICK BOND: every other course steps half a clump along the perimeter. Without it the
        // courses line up into vertical files and the flank goes back to reading as columns.
        const p2=posAt(arcK + ((m%2)?cr*0.52:0) + (hash2(k*3+m, seed+m)-0.5)*cr*0.34*shag);
        if(m<2 || shag<0.5 || hash2(k*7+m, seed*3+m)>0.16)
          clump(out, cxx+(p2[0]-cxx)*sc, cyy+(p2[1]-cyy)*sc,
                     z0+z, rr, rr*0.95, rr*0.9, mat, matDark, m?-0.08:0, (k*31+m*7+seed)|0, teeth, m>1);
        z -= cr*(m<2?0.86:1.02);
        m++;
      }
    }
  }
  // a drift of pack against a bare plant — in February the snow IS the silhouette
  function snowDrift(out, w,d,h, amt, seed){
    if(amt<=0.15) return;
    foliageMass(out, 0,0.03, w*(0.7+amt*0.3), d*(0.8+amt*0.3), h*amt, { cell:0.13, rough:0.16, full:1,
      seed:(seed|0)+91, mat:'snow', matDark:'snowDark', plan:3 });
  }
  // a heap (firewood ends, gravel, produce) filling a footprint
  function heap(out, x,y,z, w,d,h, mat, seed, n, tex, b){
    const rnd=mulberry32(seed|0);
    for(let i=0;i<n;i++){ const a=rnd(), c=rnd(), e=rnd();
      const px=x+(a*2-1)*w*0.5, py=y+(c*2-1)*d*0.5;
      const fall=1-Math.min(1,Math.hypot((px-x)/(w*0.5+1e-5),(py-y)/(d*0.5+1e-5)));
      const rr=h*(0.3+e*0.42), top=z+h*0.42*fall+rr*0.5;
      prismT(out, px,py, rr*0.7, z, Math.max(z+0.035, top), 6, mat, (b||0)-0.3+rnd()*0.7, tex||null); }
  }
  // hooped barrel (rain barrel, half-barrel planter). half=true cuts it at the belt.
  function barrel(out, x,y,z, r, h, mat, b, hoop){
    tube(out, [x,y,z],[x,y,z+h*0.16], r*0.94, 12, mat, (b||0)-0.05, grainTex(0.24));
    tube(out, [x,y,z+h*0.16],[x,y,z+h*0.84], r, 12, mat, b||0, grainTex(0.24));
    tube(out, [x,y,z+h*0.84],[x,y,z+h], r*0.94, 12, mat, (b||0)-0.05, grainTex(0.24));
    for(const zz of [z+h*0.12, z+h*0.5, z+h*0.88]) tube(out,[x,y,zz],[x,y,zz+0.03], r*1.03, 12, hoop||'iron', (b||0)+0.5, null, false);
  }
  const capDisc=(out,x,y,z,r,n,mat,b,tex)=>{ const p=[]; for(let i=0;i<n;i++){ const t=(i/n)*Math.PI*2; p.push([x+Math.cos(t)*r, y-Math.sin(t)*r]); } slab(out,p,z,mat,b,tex||null); };
  // slat seat / table top made of boards along X
  function boards(out, x0,x1,y0,y1,z,t, n, mat, b, gapT){
    const g=gapT==null?0.014:gapT, d=(y1-y0-g*(n-1))/n;
    for(let i=0;i<n;i++){ const yy=y0+i*(d+g); box(out, x0,x1, yy,yy+d, z,z+t, mat, (b||0)+(i%2?0.05:0), grainTex(0.26)); }
  }
  // rotate a built face list about a vertical axis at (ox,oy) — hinged leaves
  function rotFaces(faces, ox,oy, th){ const c=Math.cos(th), s=Math.sin(th);
    for(const fc of faces){ fc.v = fc.v.map(([x,y,z])=>{ const dx=x-ox, dy=y-oy;
      return [ox+dx*c-dy*s, oy+dx*s+dy*c, z]; }); } return faces; }
  // a plant slot the compositor fills from Flowers / Shrubs (or the native fallback)
  function plant(ctx, kind, key, x,y,z, o){ if(ctx&&ctx.plants) ctx.plants.push({ kind, key, x, y, z, o:o||{} }); }
  // NATIVE fallback bloom mound, used when the plant rigs are not loaded
  function bloomMound(out, x,y,z, r,h, s, seed, bloomKey){
    const F2=folState(s), rnd=mulberry32(seed|0);
    foliageMass(out, x,y, r*2, r*1.7, h, { cell:0.1, rough:0.3, full:F2.full, seed:seed, mat:'leaf', matDark:'leafDark' });
    if(F2.bloom>0.1 && bloomKey){ const n=Math.round(4+rnd()*5);
      for(let i=0;i<n;i++){ const a=rnd()*Math.PI*2, rr=rnd()*r*0.8;
        prismT(out, x+Math.cos(a)*rr, y+Math.sin(a)*rr*0.8, 0.035+rnd()*0.02, h*0.72, h*0.72+0.05, 5, 'bloomA', 0.7); } }
  }

  // ---- phase → foliage state -------------------------------------------------
  function folState(s, evergreen){
    const ph = PHASE_KEYS.indexOf(s.phase)>=0 ? s.phase : 'green';
    const full = evergreen ? 1 : LEAFING[ph];
    const col  = evergreen ? 'ever' : COLOURING[ph];
    const bloom = (ph==='bloom')?1:(ph==='leaf'?0.35:0);
    const fruit = (ph==='fruit'||ph==='turn')?1:0;
    return { ph, full, col, bloom, fruit };
  }

  // ================= CATALOGUE =================
  const CATS = ['fence','hedge','bed','planter','fixture','sitting','play','ornament','ground','sign'];
  const CAT_LABEL = { fence:'Fences & gates', hedge:'Hedges & shrubs', bed:'Beds & gardens', planter:'Planters',
    fixture:'Dooryard fixtures', sitting:'Sitting & gathering', play:'Play & work', ornament:'Lawn ornaments',
    ground:'Paths & ground', sign:'Signs & poles' };

  // ---------------- FENCES ----------------
  function runLen(s, base, add){ return base + s.len*add; }
  function pPicket(out,s,ctx){
    const L=runLen(s,1.7,1.6), un=1-s.kept, rnd=mulberry32(31+s.variant*7);
    const h=s.variant===1?1.16:0.92, hw=0.045, gap=0.075;
    const n=Math.max(4, Math.round(L/(hw*2+gap)));
    const step=L/n;
    for(const px of [-L/2, L/2]) post(out, px,0, 0, h+0.14, 0.055, 'body', 0.08, un*0.09*(rnd()-0.5)*2, rnd()*6.28, true, grainTex(0.3));
    for(const zr of (s.variant===1?[0.24,0.66,1.02]:[0.22,0.7])){
      box(out, -L/2,L/2, -0.028,0.028, zr-0.045, zr+0.045, 'body', -0.1, null, true);
    }
    for(let i=0;i<n;i++){
      if(un>0.55 && rnd()<un*0.22) continue;                    // a paling gone missing
      const x=-L/2+step*(i+0.5), lean=(rnd()-0.5)*un*0.16;
      picket(out, x, 0, 0.06, h+(rnd()-0.5)*un*0.09, hw, 0.026, 'body', -0.05+ (i%2?0.06:0), true, lean);
    }
    snowCap(out, -L/2,L/2, -0.03,0.03, s.variant===1?1.065:0.745, s.snowAmt);
    weeds(out, -L/2,L/2, -0.1,0.1, un*0.9, 41+s.variant, 'weed');
  }
  function pPicketGate(out,s,ctx){
    const un=1-s.kept, open=s.variant===1, Lg=1.02, hp=1.3, hg=1.0;
    for(const px of [-Lg/2-0.08, Lg/2+0.08]){
      post(out, px,0, 0, hp, 0.065, 'body', 0.1, un*0.06, 1.2, true, grainTex(0.3));
      capDisc(out, px,0, hp+0.005, 0.085, 4, 'body', 0.55);
      out.push(F([[px-0.085,-0.085,hp],[px+0.085,-0.085,hp],[px,0,hp+0.09]],'body',0.3));
      out.push(F([[px+0.085,0.085,hp],[px-0.085,0.085,hp],[px,0,hp+0.09]],'body',0.05));
    }
    // the leaf, authored flat in the x-z plane about the LEFT hinge, then swung
    const lf=[], n=5, x0=-Lg/2, span=Lg;
    for(let i=0;i<n;i++){
      const x=x0+0.06+ (i+0.5)*(span-0.12)/n;
      const arch=1-0.13*Math.pow(Math.abs((i+0.5)/n*2-1),1.8);
      picket(lf, x, 0, 0.14, 0.2+hg*arch, 0.042, 0.024, 'body', -0.05+(i%2?0.07:0), true, 0);
    }
    for(const zr of [0.34, 0.94]) box(lf, x0+0.04, x0+span-0.04, -0.032,0.032, zr-0.045,zr+0.045, 'body', 0.12, null, true);
    beam(lf,[x0+0.06,0,0.32],[x0+span-0.06,0,0.98],0.03,'body',-0.05,4);
    box(lf, x0+span-0.16, x0+span-0.06, -0.05,0.05, 0.72,0.78, 'galv', 0.5, null, false);
    if(open) rotFaces(lf, x0, 0, -0.95);
    for(const fc of lf) out.push(fc);
    weeds(out, -Lg/2-0.1,Lg/2+0.1, -0.14,0.14, un*0.8, 57, 'weed');
  }
  function pPostRail(out,s,ctx){
    const L=runLen(s,2.0,1.8), un=1-s.kept, rnd=mulberry32(71);
    for(const px of [-L/2,0,L/2]) post(out, px,0, 0, 1.14, 0.062, 'body', 0.05, un*0.1*(rnd()-0.5)*2, rnd()*6.28, true, grainTex(0.3));
    for(const zr of [0.42,0.76,1.04]) box(out, -L/2-0.03,L/2+0.03, -0.032,0.032, zr-0.05,zr+0.05, 'body', 0, grainTex(0.4), false);
    snowCap(out, -L/2,L/2, -0.035,0.035, 1.09, s.snowAmt);
    weeds(out, -L/2,L/2, -0.11,0.11, un*0.95, 83, 'weed');
  }
  function pSplitRail(out,s,ctx){
    const L=runLen(s,2.2,1.8), un=1-s.kept, rnd=mulberry32(97);
    const zig=0.26;
    for(let i=0;i<3;i++){ const y=(i%2?zig:-zig);
      for(let k=0;k<3;k++){ const z=0.2+k*0.32;
        const x0=-L/2+i*L/3, x1=x0+L/3+0.16;
        beam(out,[x0,y+(rnd()-0.5)*0.05, z],[x1, (i%2?-zig:zig)+(rnd()-0.5)*0.05, z+0.03], 0.045+ (k===0?0.012:0), 'pole', -0.05+k*0.08, 5); } }
    for(let i=0;i<=3;i++){ const x=-L/2+i*L/3, y=(i%2?zig:-zig);
      post(out, x,y, 0, 1.05, 0.055, 'pole', 0.05, un*0.12, rnd()*6.28, false); }
    weeds(out, -L/2,L/2, -0.32,0.32, 0.35+un*0.75, 113, 'weed', true);
  }
  function pWireFence(out,s,ctx){
    const L=runLen(s,2.2,2.0), un=1-s.kept, rnd=mulberry32(131), h=1.06;
    for(const px of [-L/2,-L/6,L/6,L/2]) post(out, px,0, 0, h+0.1, 0.05, 'pole', 0, un*0.14*(rnd()-0.5)*2, rnd()*6.28, false, grainTex(0.28));
    // page wire: verticals + graded horizontals, drawn as a decal grid so it stays 1 px at 32 ppu
    for(let k=0;k<6;k++){ const z=0.14+Math.pow(k/5,1.25)*(h-0.2) - un*0.03*Math.sin(k);
      const sag=un*0.05;
      for(let i=0;i<8;i++){ const x0=-L/2+i*L/8, x1=x0+L/8;
        const s0=Math.sin(Math.PI*(i/8))*sag, s1=Math.sin(Math.PI*((i+1)/8))*sag;
        beam(out,[x0,0,z-s0],[x1,0,z-s1],0.013,'galv',0.5,3); } }
    for(let i=1;i<10;i++){ const x=-L/2+i*L/10; beam(out,[x,0,0.13],[x,0,h-0.05],0.011,'galv',0.35,3); }
    weeds(out, -L/2,L/2, -0.1,0.1, 0.3+un*0.85, 149, 'weed', true);
  }
  function pSnowFence(out,s,ctx){
    const L=runLen(s,2.0,1.6), un=1-s.kept, rnd=mulberry32(167), h=0.92, tilt=0.06+un*0.14;
    for(const px of [-L/2,0,L/2]) post(out, px,0, 0, h+0.12, 0.032, 'galv', 0.1, tilt, Math.PI/2, false);
    const n=Math.round(L/0.11);
    for(let i=0;i<n;i++){ const x=-L/2+ (i+0.5)*L/n, lean=tilt+(rnd()-0.5)*un*0.2;
      const dx=Math.sin(lean)*h;
      beam(out,[x,0,0.03],[x,dx*0.9,h+ (rnd()-0.5)*0.05], 0.019, 'body', -0.1+ (i%2?0.15:0), 4); }
    for(const zr of [0.2,0.78]) beam(out,[-L/2,Math.sin(tilt)*zr,zr],[L/2,Math.sin(tilt)*zr,zr],0.014,'galv',0.4,3);
    weeds(out, -L/2,L/2, -0.14,0.24, 0.4+un*0.7, 181, 'weed', true);
  }
  function pLattice(out,s,ctx){
    const L=runLen(s,1.5,1.1), un=1-s.kept, h=0.74;
    box(out, -L/2,L/2, -0.05,0.05, 0,0.07, 'body', -0.2, null, true);
    for(const sgn of [1,-1]) for(let i=-8;i<=8;i++){
      const x=i*0.19;
      const x0=x-0.5*sgn*h*0.5, x1=x+0.5*sgn*h*0.5;
      if(x0>L/2+0.3 || x1<-L/2-0.3) continue;
      const a=[Math.max(-L/2,Math.min(L/2,x0)),0,0.07], b=[Math.max(-L/2,Math.min(L/2,x1)),0,h];
      if(Math.abs(a[0]-b[0])<0.02) continue;
      beam(out,a,b,0.019,'body', sgn>0?0.15:-0.05, 3);
    }
    box(out, -L/2-0.03,L/2+0.03, -0.055,0.055, h,h+0.075, 'body', 0.15, null, false);
    for(const px of [-L/2,L/2]) box(out, px-0.045,px+0.045, -0.055,0.055, 0,h+0.075, 'body', 0.1, null, false);
    weeds(out, -L/2,L/2, -0.09,0.09, un*0.8, 199, 'weed');
  }
  function pStoneWall(out,s,ctx){
    const L=runLen(s,1.8,1.6), un=1-s.kept, rnd=mulberry32(211+s.variant*3);
    const h=s.variant===1?0.86:0.62, d=0.46;
    // dry-laid: a battered core plus real cap stones, so the silhouette is lumpy not extruded
    const nx=Math.max(5,Math.round(L/0.34));
    for(let i=0;i<nx;i++){
      const x0=-L/2+i*L/nx, x1=x0+L/nx, jitter=(rnd()-0.5)*0.05;
      const hh=h*(0.9+rnd()*0.12)+jitter - un*0.03;
      const dd=d*(0.86+rnd()*0.2);
      box(out, x0+0.008,x1-0.008, -dd/2,dd/2, 0, hh, 'stone', -0.05+rnd()*0.35, stoneTex(), false);
      if(rnd()<0.7){ const cw=(x1-x0)*(0.5+rnd()*0.4);
        box(out, x0+cw*0.2, x0+cw*1.0, -dd*0.42,dd*0.42, hh, hh+0.075+rnd()*0.05, 'stone', 0.35+rnd()*0.3, stoneTex(), false); }
      if(un>0.4 && rnd()<un*0.5) box(out, x0+0.1,x0+0.24, -dd*0.3,dd*0.1, hh+0.05, hh+0.09, 'leaf', 0.2, leafTex(), false); // moss / weed in the joint
    }
    snowCap(out, -L/2,L/2, -d/2*0.85,d/2*0.85, h+0.1, s.snowAmt*0.8);
    weeds(out, -L/2,L/2, -d/2-0.05,d/2+0.05, 0.25+un*0.8, 223, 'weed');
  }
  function pStonePillar(out,s,ctx){
    const un=1-s.kept, rnd=mulberry32(227), r=0.28;
    for(let k=0;k<5;k++){ const z0=k*0.24, z1=z0+0.24, w=r*2*(1-k*0.035);
      box(out, -w/2,w/2, -w/2,w/2, z0,z1, 'stone', -0.05+ (k%2?0.15:0), stoneTex(), true); }
    box(out, -r*1.15,r*1.15, -r*1.15,r*1.15, 1.2,1.31, 'stone', 0.45, stoneTex(), false);
    if(s.variant===1) prismT(out, 0,0, 0.1, 1.31, 1.46, 8, 'body', 0.3);  // a lamp / finial ball
    weeds(out, -r,r, -r,r, un*0.85, 229, 'weed');
  }
  function pFenceCorner(out,s,ctx){
    const un=1-s.kept, rnd=mulberry32(233), h=0.92, arm=0.62;
    post(out, 0,0, 0, h+0.2, 0.07, 'body', 0.1, un*0.08, 0.7, true, grainTex(0.3));
    for(const [ax,ay] of [[arm,0],[0,arm]]){
      for(const zr of [0.22,0.7]) beam(out,[0,0,zr],[ax,ay,zr],0.035,'body',0,4);
      const nP=3; for(let i=0;i<nP;i++){ const t=(i+0.7)/nP;
        picket(out, ax*t, ay*t, 0.06, h, 0.045, 0.026, 'body', -0.05+(i%2?0.06:0), true, (rnd()-0.5)*un*0.14); }
    }
    weeds(out, -0.12,arm, -0.12,arm, un*0.7, 239, 'weed');
  }

  // ---------------- HEDGES & SHRUBS ----------------
  function pHedgeRun(out,s,ctx){
    const L=runLen(s,1.6,1.9), un=1-s.kept, Fs=folState(s,false);
    const h=(s.variant===1?1.35:0.85), d=0.62+ (s.variant===1?0.1:0);
    foliageMass(out, 0,0, L, d, h, { cell:0.115, rough:0.035+un*0.42, full:Fs.full, seed:17+s.variant,
      mat:'leaf', matDark:'leafDark', snow:s.snowAmt, flatTop:s.kept>0.72 });
    if(s.snowAmt>0.2 && Fs.full<0.5) snowDrift(out, L*0.95, d*1.5, h*0.5, s.snowAmt, 17);
    if(Fs.full<0.4){ const rnd=mulberry32(19); // the twig cage shows when the leaf is off
      for(let i=0;i<Math.round(L*7);i++){ const x=-L/2+ (i+0.5)/Math.round(L*7)*L;
        beam(out,[x+(rnd()-0.5)*0.1,(rnd()-0.5)*0.2,0],[x+(rnd()-0.5)*0.24,(rnd()-0.5)*0.28,h*(0.6+rnd()*0.42)],0.017,'twig',-0.1,3); } }
    weeds(out, -L/2,L/2, -d/2,d/2, un*0.7, 251, 'weed');
  }
  function pHedgeBall(out,s,ctx){
    const un=1-s.kept, Fs=folState(s, s.variant===1), r=0.42+s.len*0.22;
    beam(out,[0,0,0],[0,0,r*0.7],0.05,'twig',-0.1,5);
    foliageMass(out, 0,0, r*2, r*1.9, r*1.9, { cell:0.1, rough:0.03+un*0.5, full:Fs.full, seed:29, taper:0.36,
      mat:'leaf', matDark:'leafDark', snow:s.snowAmt, plan:2 });
    weeds(out, -r,r, -r,r, un*0.7, 257, 'weed');
  }
  function pCedarHedge(out,s,ctx){
    const L=runLen(s,1.5,1.7), h=1.8+s.len*0.5, un=1-s.kept;
    foliageMass(out, 0,0, L, 0.66, h, { cell:0.12, rough:0.03+un*0.3, full:1, seed:37, taper:-0.13,
      mat:'ever', matDark:'everDark', snow:s.snowAmt, flatTop:s.kept>0.6 });
    weeds(out, -L/2,L/2, -0.34,0.34, un*0.6, 263, 'weed');
  }
  function pLilac(out,s,ctx){
    const un=1-s.kept, Fs=folState(s,false), rnd=mulberry32(269), h=1.9+s.len*0.6, r=0.62+s.len*0.2;
    for(let i=0;i<6;i++){ const a=(i/6)*Math.PI*2+0.4;
      beam(out,[Math.cos(a)*0.06,Math.sin(a)*0.05,0],[Math.cos(a)*r*0.5,Math.sin(a)*r*0.4,h*0.62],0.036,'twig',-0.1,4); }
    foliageMass(out, 0,0, r*2, r*1.6, h, { cell:0.12, rough:0.28+un*0.3, full:Fs.full, seed:41, taper:0.46,
      mat:'leaf', matDark:'leafDark', snow:s.snowAmt });
    if(s.snowAmt>0.2 && Fs.full<0.5) snowDrift(out, r*1.9, r*1.6, 0.5, s.snowAmt, 41);
    if(Fs.ph==='bloom'||Fs.ph==='leaf'){ const n=Fs.ph==='bloom'?11:5;
      for(let i=0;i<n;i++){ const a=rnd()*Math.PI*2, rr=r*(0.25+rnd()*0.6), z=h*(0.62+rnd()*0.34);
        const x=Math.cos(a)*rr, y=Math.sin(a)*rr*0.8;
        tube(out,[x,y,z],[x+0.03,y,z+0.2+rnd()*0.09], 0.055, 6, 'bloomA', 0.5+rnd()*0.4); } }
    weeds(out, -r*0.8,r*0.8, -r*0.6,r*0.6, un*0.8, 271, 'weed');
  }
  function pRoseBush(out,s,ctx){
    const un=1-s.kept, Fs=folState(s,false), rnd=mulberry32(277), r=0.52+s.len*0.2, h=0.78+s.len*0.25;
    foliageMass(out, 0,0, r*2, r*1.8, h, { cell:0.1, rough:0.3+un*0.35, full:Fs.full, seed:43, taper:0.3,
      mat:'leaf', matDark:'leafDark', snow:s.snowAmt });
    if(s.snowAmt>0.2 && Fs.full<0.5) snowDrift(out, r*1.9, r*1.7, 0.44, s.snowAmt, 43);
    const showBloom = Fs.ph==='bloom'||Fs.ph==='leaf'||Fs.ph==='green';
    const showHip   = Fs.ph==='fruit'||Fs.ph==='turn'||Fs.ph==='bare';
    if(showBloom||showHip){ const n=showBloom?(Fs.ph==='bloom'?9:5):7;
      for(let i=0;i<n;i++){ const a=rnd()*Math.PI*2, rr=r*(0.3+rnd()*0.62);
        prismT(out, Math.cos(a)*rr, Math.sin(a)*rr*0.8, showHip?0.045:0.07, h*(0.5+rnd()*0.5), h*(0.5+rnd()*0.5)+0.05,
          6, showHip?'bloomB':'bloomA', 0.75); } }
    weeds(out, -r,r, -r*0.8,r*0.8, un*0.9, 281, 'weed');
  }

  // ---------------- BEDS & GARDENS ----------------
  function bedSoil(out, s, w,d, edge, seed){
    const un=1-s.kept, rnd=mulberry32(seed|0);
    const mound=0.07+ (1-un)*0.03;
    // edging first, soil inside it
    if(edge==='stone'){ const n=Math.round((w+d)*2/0.3);
      for(let i=0;i<n;i++){ const t=i/n*4; let x,y;
        if(t<1){ x=-w/2+t*w; y=-d/2; } else if(t<2){ x=w/2; y=-d/2+(t-1)*d; }
        else if(t<3){ x=w/2-(t-2)*w; y=d/2; } else { x=-w/2; y=d/2-(t-3)*d; }
        const r=0.09+rnd()*0.05;
        prismT(out, x,y, r, 0, 0.09+rnd()*0.06, 6, 'stone', -0.1+rnd()*0.5, stoneTex()); } }
    else if(edge==='timber'){
      box(out, -w/2,w/2, -d/2,-d/2+0.09, 0,0.12, 'pole', 0, grainTex(0.3), false);
      box(out, -w/2,w/2, d/2-0.09,d/2, 0,0.12, 'pole', 0, grainTex(0.3), false);
      box(out, -w/2,-w/2+0.09, -d/2,d/2, 0,0.12, 'pole', -0.1, grainTex(0.3), false);
      box(out, w/2-0.09,w/2, -d/2,d/2, 0,0.12, 'pole', 0.1, grainTex(0.3), false); }
    else if(edge==='shell'){ const n=Math.round((w+d)*2/0.16);
      for(let i=0;i<n;i++){ const t=i/n*4; let x,y;
        if(t<1){ x=-w/2+t*w; y=-d/2; } else if(t<2){ x=w/2; y=-d/2+(t-1)*d; }
        else if(t<3){ x=w/2-(t-2)*w; y=d/2; } else { x=-w/2; y=d/2-(t-3)*d; }
        prismT(out, x,y, 0.055+rnd()*0.02, 0, 0.05+rnd()*0.03, 5, 'shell', 0.2+rnd()*0.5); } }
    const inset = edge==='timber'?0.09:0.05;
    const mat = s.kept>0.45 ? 'mulch' : 'soil';
    box(out, -w/2+inset, w/2-inset, -d/2+inset, d/2-inset, 0, mound, mat, 0.1, soilTex(), false);
    return mound;
  }
  function bedPlant(out, ctx, s, w,d,z, dense, seed){
    const rnd=mulberry32(seed|0), un=1-s.kept, Fs=folState(s,false);
    if(dense<=0 || Fs.full<0.2){ if(un>0.3) weeds(out, -w/2+0.1,w/2-0.1, -d/2+0.08,d/2-0.08, un*1.0, seed+3, 'weed', true); return; }
    const back=Math.max(1, Math.round(dense*w*1.15)), front=Math.max(2, Math.round(dense*w*2.1));
    for(let i=0;i<back;i++){
      const x=-w/2+ (i+0.5+ (rnd()-0.5)*0.4)*w/back, y=-d/2+0.1+rnd()*0.06;
      const pick=BACKMIX[Math.floor(rnd()*BACKMIX.length)];
      plant(ctx, pick.kind, pick.key, x,y,z, { variant:Math.floor(rnd()*4), tier:pick.tier });
      if(!ctx || !ctx.external) bloomMound(out, x,y,z, 0.1+rnd()*0.05, 0.34+rnd()*0.2, s, seed+i*7, 'lilac');
    }
    for(let i=0;i<front;i++){
      const x=-w/2+ (i+0.4+ (rnd()-0.5)*0.5)*w/front, y=d/2-0.1-rnd()*0.14;
      const pick=FRONTMIX[Math.floor(rnd()*FRONTMIX.length)];
      plant(ctx, pick.kind, pick.key, x,y,z, { variant:Math.floor(rnd()*4), tier:pick.tier });
      if(!ctx || !ctx.external) bloomMound(out, x,y,z, 0.1+rnd()*0.05, 0.16+rnd()*0.1, s, seed+i*11, 'white');
    }
    if(un>0.35) weeds(out, -w/2+0.1,w/2-0.1, -d/2+0.08,d/2-0.08, un*0.9, seed+3, 'weed', true);
  }
  // a bed is planted like a bed: tall at the back, short at the front. Two lists, two rows.
  const BACKMIX  = [ {kind:'flower',key:'Lupin Purple'}, {kind:'flower',key:'Lupin Pink'},
                     {kind:'flower',key:'Goldenrod Gold'}, {kind:'flower',key:'BlueFlag'} ];
  const FRONTMIX = [ {kind:'flower',key:'OxeyeDaisy'}, {kind:'flower',key:'WildRose'},
                     {kind:'flower',key:'QueenAnne'}, {kind:'flower',key:'Buttercup',tier:'patch'} ];
  function pFoundationBed(out,s,ctx){
    const w=runLen(s,1.6,1.4), d=0.62;
    const z=bedSoil(out, s, w,d, s.variant===1?'stone':'shell', 307);
    bedPlant(out, ctx, s, w,d,z, s.planted?1:0, 311);
  }
  function pIslandBed(out,s,ctx){
    const w=runLen(s,1.1,0.9), d=w*0.72;
    const z=bedSoil(out, s, w,d, 'stone', 313);
    bedPlant(out, ctx, s, w,d,z, s.planted?1.1:0, 317);
    if(s.planted) plant(ctx,'shrub','WildRose',0,0.02,z,{ size:0.35 });
  }
  function pBorderBed(out,s,ctx){
    const w=runLen(s,1.8,1.6), d=0.45;
    const z=bedSoil(out, s, w,d, 'timber', 331);
    bedPlant(out, ctx, s, w,d,z, s.planted?0.9:0, 337);
  }
  function pVegRows(out,s,ctx){
    const w=runLen(s,1.5,1.3), d=1.1, un=1-s.kept, rnd=mulberry32(347), Fs=folState(s,false);
    box(out, -w/2,w/2, -d/2,d/2, 0,0.05, 'soil', -0.05, soilTex(), false);
    const rows=4;
    for(let r=0;r<rows;r++){ const y=-d/2+ (r+0.5)*d/rows;
      // hilled drill
      for(let i=0;i<Math.round(w/0.16);i++){ const x=-w/2+ (i+0.5)*0.16;
        prismT(out, x,y, 0.085, 0.04, 0.12+rnd()*0.02, 6, 'soil', -0.1+rnd()*0.4, soilTex()); }
      if(Fs.full>0.25){ const n=Math.round(w/0.22);
        for(let i=0;i<n;i++){ const x=-w/2+ (i+0.6)*w/n;
          foliageMass(out, x,y, 0.19, 0.19, 0.16+ Fs.full*0.16, { cell:0.07, rough:0.3, full:Fs.full, seed:353+i+r*13, mat:'leaf', matDark:'leafDark' }); } }
    }
    if(s.variant===1){ // bean poles
      for(let i=0;i<3;i++){ const x=-w/2+0.3+i*(w-0.6)/2;
        for(let k=0;k<3;k++){ const a=k/3*Math.PI*2;
          beam(out,[x+Math.cos(a)*0.16, d/2-0.2+Math.sin(a)*0.16, 0],[x,d/2-0.2,1.05],0.02,'pole',0.1,4); } } }
    weeds(out, -w/2,w/2, -d/2,d/2, un*1.1, 359, 'weed', true);
  }
  function pPotatoDrills(out,s,ctx){
    const w=runLen(s,1.7,1.5), d=1.2, rnd=mulberry32(367), Fs=folState(s,false), un=1-s.kept;
    const rows=5;
    for(let r=0;r<rows;r++){ const y=-d/2+ (r+0.5)*d/rows;
      for(let i=0;i<Math.round(w/0.13);i++){ const x=-w/2+ (i+0.5)*0.13;
        prismT(out, x,y, 0.078, 0, 0.15+rnd()*0.025, 6, 'soil', -0.15+rnd()*0.45, soilTex()); }
      if(Fs.full>0.4){ for(let i=0;i<Math.round(w/0.24);i++){ const x=-w/2+ (i+0.6)*0.24;
        foliageMass(out, x,y, 0.22, 0.2, 0.2, { cell:0.075, rough:0.26, full:Fs.full, seed:373+i+r*7, mat:'leaf', matDark:'leafDark' });
        if(Fs.ph==='bloom'||Fs.ph==='leaf') prismT(out, x,y, 0.03, 0.22, 0.26, 5, 'bloomC', 0.8); } }
    }
    weeds(out, -w/2,w/2, -d/2,d/2, un*0.7, 379, 'weed');
  }
  function pRhubarb(out,s,ctx){
    const Fs=folState(s,false), rnd=mulberry32(383), un=1-s.kept;
    const n=5+Math.round(s.len*3);
    for(let i=0;i<n;i++){ const a=(i/n)*Math.PI*2+0.3, rr=0.16+rnd()*0.12;
      const x=Math.cos(a)*rr, y=Math.sin(a)*rr*0.8;
      beam(out,[0,0,0.02],[x,y,0.2+rnd()*0.1],0.026,'stalk',0.3,4);
      if(Fs.full>0.3) foliageMass(out, x*1.5,y*1.5, 0.34, 0.3, 0.16+Fs.full*0.14,
        { cell:0.09, rough:0.22, full:Fs.full, seed:389+i, mat:'leaf', matDark:'leafDark' });
    }
    weeds(out, -0.4,0.4, -0.34,0.34, un*0.8, 397, 'weed');
  }

  // ---------------- PLANTERS ----------------
  function pBarrelPlanter(out,s,ctx){
    const Fs=folState(s,false), r=0.31, h=0.44;
    barrel(out, 0,0,0, r, h, 'pole', 0, 'iron');
    capDisc(out, 0,0, h-0.06, r*0.93, 12, 'soil', -0.4, soilTex());
    if(s.planted){
      foliageMass(out, 0,0, r*1.75, r*1.55, 0.3, { z0:h-0.09, cell:0.08, rough:0.3, full:Fs.full, seed:401, mat:'leaf', matDark:'leafDark' });
      if(Fs.full>0.4){ const rnd=mulberry32(403);
        for(let i=0;i<7;i++){ const a=i/7*6.28+rnd()*0.4, rr=r*(0.2+rnd()*0.55);
          prismT(out, Math.cos(a)*rr, Math.sin(a)*rr*0.85, 0.04, h+0.24+rnd()*0.06, h+0.3+rnd()*0.06, 5, i%3?'bloomA':'bloomC', 0.7); }
        for(let i=0;i<4;i++){ const a=i/4*6.28+0.7;                      // a trail over the rim
          beam(out,[Math.cos(a)*r*0.8, Math.sin(a)*r*0.7, h+0.06],[Math.cos(a)*r*1.15, Math.sin(a)*r*1.0, h-0.16],0.026,'leaf',0.1,4); } } }
    snowCap(out, -r*0.9,r*0.9, -r*0.9,r*0.9, h-0.04, s.snowAmt);
  }
  function pWindowBox(out,s,ctx){
    const L=runLen(s,0.9,0.7), Fs=folState(s,false), h=0.22;
    box(out, -L/2,L/2, -0.14,0.14, 0,h, 'body', 0, plankTex(0.18), true);
    box(out, -L/2+0.03,L/2-0.03, -0.11,0.11, h-0.06,h-0.02, 'soil', -0.5, soilTex(), false);
    for(const sx of [-L/2+0.08, 0, L/2-0.08]) box(out, sx-0.03,sx+0.03, -0.155,-0.135, 0.02,h-0.02, 'body', 0.25, null, true);
    if(s.planted){
      foliageMass(out, 0,0, L-0.06, 0.2, 0.17, { z0:h-0.05, cell:0.075, rough:0.34, full:Fs.full, seed:409, mat:'leaf', matDark:'leafDark' });
      if(Fs.full>0.4){ const n=Math.round(L*7);
        for(let i=0;i<n;i++){ const x=-L/2+ (i+0.5)*L/n;
          prismT(out, x, (hash2(i,3)-0.5)*0.14, 0.033, 0.24+hash2(i,7)*0.05, 0.29+hash2(i,7)*0.05, 5, i%3?'bloomA':'bloomC', 0.7); } }
      // trailing growth over the front lip
      for(let i=0;i<Math.round(L*5);i++){ const x=-L/2+ (i+0.4)*L/Math.round(L*5);
        beam(out,[x,0.13,h-0.05],[x+(hash2(i,11)-0.5)*0.06, 0.17, h-0.18-hash2(i,13)*0.1],0.022,'leaf',0.1,4); }
    }
  }
  function pPotCluster(out,s,ctx){
    const Fs=folState(s,false), rnd=mulberry32(419);
    const pots=[[0,-0.06,0.19,0.24],[0.26,0.1,0.14,0.18],[-0.24,0.08,0.11,0.15]];
    pots.forEach((p,i)=>{
      const [x,y,r,h]=p;
      tube(out,[x,y,0],[x,y,h], r*0.72, 10, 'clay', -0.05, null, false);
      tube(out,[x,y,h*0.86],[x,y,h], r, 10, 'clay', 0.15, null, false);
      capDisc(out, x,y, h-0.02, r*0.88, 10, 'soil', -0.45, soilTex());
      if(s.planted){ foliageMass(out, x,y, r*1.9, r*1.7, r*1.5, { z0:h-0.04, cell:0.07, rough:0.3, full:Fs.full, seed:421+i*5, mat:'leaf', matDark:'leafDark' });
        if(Fs.full>0.4) for(let k=0;k<3;k++){ const a=rnd()*6.28;
          prismT(out, x+Math.cos(a)*r*0.5, y+Math.sin(a)*r*0.4, 0.032, h*0.95, h*0.95+0.05, 5, k%2?'bloomA':'bloomC', 0.7); } }
    });
  }
  function pTirePlanter(out,s,ctx){
    const Fs=folState(s,false), R=0.36, r=0.13;
    for(let i=0;i<14;i++){ const a=i/14*6.28, a2=(i+1)/14*6.28;
      const P=(t,ph,rr)=>[Math.cos(t)*(R+rr*Math.cos(ph)), Math.sin(t)*(R+rr*Math.cos(ph))*0.94, r+rr*Math.sin(ph)];
      for(let j=0;j<6;j++){ const p1=j/6*Math.PI*2, p2=(j+1)/6*Math.PI*2;
        out.push(F([P(a,p1,r),P(a2,p1,r),P(a2,p2,r),P(a,p2,r)], s.variant===1?'body':'rubber', 0.1+Math.sin(p1)*0.3)); } }
    capDisc(out, 0,0, r*1.15, R*0.82, 12, 'soil', -0.4, soilTex());
    if(s.planted){ foliageMass(out, 0,0, R*1.3, R*1.2, 0.22, { z0:r*1.05, cell:0.08, rough:0.3, full:Fs.full, seed:431, mat:'leaf', matDark:'leafDark' });
      if(Fs.full>0.4) for(let i=0;i<6;i++){ const a=i/6*6.28;
        prismT(out, Math.cos(a)*R*0.4, Math.sin(a)*R*0.36, 0.04, 0.3, 0.36, 5, i%2?'bloomC':'bloomA', 0.75); } }
  }

  // ---------------- DOORYARD FIXTURES ----------------
  function pMailbox(out,s,ctx){
    const un=1-s.kept, lean=un*0.12, h=1.04;
    const top=post(out, 0,0, 0, h, 0.055, 'pole', 0, lean, 2.2, true, grainTex(0.3));
    const px=top[0], py=top[1];
    box(out, px-0.16,px+0.16, py-0.12,py+0.12, h,h+0.05, 'pole', 0.2, grainTex(0.3), false);   // the platform
    const bz=h+0.05, bw=0.15, bd=0.24;
    box(out, px-bw,px+bw, py-bd,py+bd, bz, bz+0.12, 'tin', 0.05, null, true);
    for(let i=0;i<9;i++){ const a1=Math.PI*(i/9), a2=Math.PI*((i+1)/9);
      const P=(a,yy)=>[px+Math.cos(a)*bw, yy, bz+0.12+Math.sin(a)*bw*1.02];
      out.push(F([P(a1,py-bd),P(a2,py-bd),P(a2,py+bd),P(a1,py+bd)], 'tin', -0.15+Math.sin(a1)*0.95)); }
    // the door end, facing the road (+Y)
    out.push(F([[px-bw,py+bd,bz],[px+bw,py+bd,bz],[px+bw,py+bd,bz+0.12],[px-bw,py+bd,bz+0.12]], 'tin', -0.25));
    for(let i=0;i<9;i++){ const a1=Math.PI*(i/9), a2=Math.PI*((i+1)/9);
      out.push(F([[px+Math.cos(a1)*bw,py+bd,bz+0.12+Math.sin(a1)*bw*1.02],
                  [px+Math.cos(a2)*bw,py+bd,bz+0.12+Math.sin(a2)*bw*1.02],
                  [px,py+bd,bz+0.12]], 'tin', -0.3)); }
    dY(out, py+bd+0.006, 1, px-bw*0.72, px+bw*0.72, bz+0.03, bz+0.16, 'tin', -0.85, 0.07);   // the door seam
    box(out, px-0.035,px+0.035, py+bd-0.01,py+bd+0.01, bz+0.055,bz+0.075, 'galv', 0.6, null, false); // latch
    // the flag: a red blade on a bracket, up or down
    const fx=px+bw+0.015;
    box(out, fx-0.012,fx+0.012, py-0.02,py+0.02, bz+0.02,bz+0.05, 'galv', 0.4, null, false);
    if(s.variant===1) box(out, fx-0.012,fx+0.012, py-0.02,py+0.02, bz+0.05,bz+0.24, 'flagred', 0.55, null, false);
    else box(out, fx-0.012,fx+0.012, py-0.2,py+0.02, bz+0.03,bz+0.055, 'flagred', 0.5, null, false);
    dY(out, py-bd-0.006, -1, px-0.07, px+0.07, bz+0.03, bz+0.09, 'trimw', 0.3, 0.07);        // the number plate
    weeds(out, -0.2,0.2, -0.28,0.28, un*0.9, 439, 'weed', true);
  }
  function pClothesline(out,s,ctx){
    const un=1-s.kept, span=2.6+s.len*1.6, h=1.92, arm=0.56, rnd=mulberry32(443);
    for(const sx of [-span/2, span/2]){
      post(out, sx,0, 0, h, 0.075, 'pole', 0, un*0.05, 1.0, true, grainTex(0.3));
      box(out, sx-0.055,sx+0.055, -arm/2,arm/2, h-0.1,h, 'pole', 0.3, grainTex(0.3), false);   // the T head
      for(const sy of [-1,1]) beam(out,[sx,sy*(arm/2-0.04),h-0.1],[sx,0,h-0.46],0.03,'pole',sy<0?0.05:-0.15,4);
      for(const sy of [-1,1]) capDisc(out, sx, sy*(arm/2-0.03), h+0.01, 0.05, 6, 'galv', 0.5);
    }
    const lines=[-arm/2+0.07, arm/2-0.07], hung = s.variant!==1;
    for(const ly of lines){
      const sag=0.05+un*0.1+(hung?0.09:0);
      for(let i=0;i<12;i++){ const t0=i/12, t1=(i+1)/12;
        const x0=-span/2+span*t0, x1=-span/2+span*t1;
        const z0=h-0.06-Math.sin(Math.PI*t0)*sag, z1=h-0.06-Math.sin(Math.PI*t1)*sag;
        beam(out,[x0,ly,z0],[x1,ly,z1],0.024,'rope',0.55,4); }
    }
    if(hung){
      const n=4+Math.round(s.len*3), frozen = s.phase==='dormant';
      for(let i=0;i<n;i++){
        const t=(i+0.75)/(n+0.5), x=-span/2+span*t, ly=lines[i%2];
        const z=h-0.07-Math.sin(Math.PI*t)*(0.14+un*0.1);
        const wq=0.30+rnd()*0.12, hq=0.42+rnd()*0.26, mat=(i%3===0)?'body':'linen';
        const blow=frozen?0.02:0.10+rnd()*0.1;                    // stiff in February, bellied out in July
        // a hung sheet: two panels meeting at a soft fold, so it has a lit face and a shaded one
        const yF=ly+blow, yB=ly-blow*0.5;
        out.push(F([[x-wq/2,ly,z],[x,yF,z-0.01],[x,yF,z-hq],[x-wq/2,ly+0.01,z-hq*0.94]], mat, 0.3, 0,
          [[0,0],[wq/2,0],[wq/2,hq],[0,hq]], knitTex()));
        out.push(F([[x,yF,z-0.01],[x+wq/2,ly,z],[x+wq/2,ly+0.01,z-hq*0.94],[x,yF,z-hq]], mat, -0.05, 0,
          [[0,0],[wq/2,0],[wq/2,hq],[0,hq]], knitTex()));
        out.push(F([[x+wq/2,ly,z],[x-wq/2,ly,z],[x-wq/2,yB,z-hq*0.9],[x+wq/2,yB,z-hq*0.9]], mat, -0.8, 0,
          [[0,0],[wq,0],[wq,hq],[0,hq]], knitTex()));
        out.push(F([[x-wq/2,ly,z-hq*0.94],[x,yF,z-hq],[x+wq/2,ly,z-hq*0.94],[x,yB,z-hq*0.9]], mat, -0.55));
        for(const cx2 of [x-wq/2+0.035, x+wq/2-0.035])
          box(out, cx2-0.014,cx2+0.014, ly-0.018,ly+0.018, z-0.015,z+0.055, 'pole', 0.45, null, false);
      }
      if(s.kept<0.5){ // the basket left out on the grass
        const bx=-span/2+0.4;
        tube(out,[bx,0.34,0],[bx,0.34,0.22], 0.24, 10, 'basket', 0.1, null, false);
        capDisc(out, bx,0.34, 0.2, 0.21, 10, 'linen', -0.3);
      }
    }
    weeds(out, -span/2,span/2, -0.12,0.12, un*0.55, 449, 'weed');
  }
  function pWoodpile(out,s,ctx){
    const L=runLen(s,1.1,1.1), un=1-s.kept, rnd=mulberry32(457);
    const rows=4+Math.round(s.len*3), perRow=Math.max(3,Math.round(L/0.2));
    const stacked = s.kept>0.4;
    if(stacked){
      for(let r=0;r<rows;r++){ const z=r*0.2, jitterR=(rnd()-0.5)*0.04;
        for(let i=0;i<perRow;i++){
          const x=-L/2+ (i+0.5)*L/perRow + jitterR;
          const rr=0.085+rnd()*0.022, len=0.42+rnd()*0.05;
          tube(out,[x,-len/2,z+rr],[x,len/2,z+rr], rr, 7, 'firewood', -0.2+rnd()*0.7, grainTex(0.2), true); } }
      if(s.variant===1){ // tarp over the top
        const zt=rows*0.2+0.02;
        for(let i=0;i<3;i++) for(let j=0;j<3;j++){
          const x0=-L/2-0.06+ (L+0.12)*i/3, x1=-L/2-0.06+(L+0.12)*(i+1)/3;
          const y0=-0.3+0.6*j/3, y1=-0.3+0.6*(j+1)/3;
          const sagf=(u)=>zt-0.05*Math.sin(u*Math.PI);
          out.push(F([[x0,y0,sagf(i/3)],[x1,y0,sagf((i+1)/3)],[x1,y1,sagf((i+1)/3)],[x0,y1,sagf(i/3)]], 'canvas', 0.2+j*0.08, 0,
            [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], canvasTex())); } }
      else snowCap(out, -L/2,L/2, -0.22,0.22, rows*0.2+0.02, s.snowAmt);
    } else { // a tumbled heap, not a rick — the low-kept read is loose logs, every one lying its own way
      const r2=mulberry32(461);
      for(let i=0;i<22;i++){
        const px=(r2()-0.5)*L*0.95, py=(r2()-0.5)*0.62, a=r2()*Math.PI*2;
        const len=0.36+r2()*0.14, rr=0.075+r2()*0.025, z=0.06+r2()*0.22;
        tube(out,[px-Math.cos(a)*len/2, py-Math.sin(a)*len/2, z],
                 [px+Math.cos(a)*len/2, py+Math.sin(a)*len/2, z+ (r2()-0.5)*0.09],
                 rr, 7, 'firewood', -0.25+r2()*0.8, grainTex(0.2), true);
      }
    }
    weeds(out, -L/2,L/2, -0.3,0.3, un*0.9, 463, 'weed', true);
  }
  function pRainBarrel(out,s,ctx){
    const un=1-s.kept, r=0.31, h=0.86;
    // stands on a couple of blocks, backs to -Y (the house wall)
    for(const sx of [-0.18,0.18]) box(out, sx-0.1,sx+0.1, -0.16,0.16, 0,0.12, 'stone', 0, stoneTex(), false);
    barrel(out, 0,0,0.12, r, h-0.12, s.variant===1?'body':'pole', 0, 'iron');
    capDisc(out, 0,0, h+0.005, r*0.9, 12, s.variant===1?'body':'pole', 0.3, grainTex(0.2));
    // downspout coming down the wall into the lid
    box(out, -0.05,0.05, -0.34,-0.26, 0.55,2.2, 'tin', 0.1, null, true);
    out.push(F([[-0.05,-0.26,0.55],[0.05,-0.26,0.55],[0.05,-0.06,h+0.03],[-0.05,-0.06,h+0.03]], 'tin', 0.35));
    beam(out,[0,-0.02,0.28],[0.02,0.3,0.2],0.03,'rubber',-0.1,5);   // overflow hose
    weeds(out, -0.34,0.34, -0.3,0.3, un*0.8, 467, 'weed');
  }
  function pHoseReel(out,s,ctx){
    const un=1-s.kept;
    pipe(out, -0.02,-0.24, 0.045, 0, 0.66, 'galv', 0.2, 8);                  // standpipe
    box(out, -0.085,0.085, -0.32,-0.16, 0.58,0.74, 'brassy', 0.4, null, false); // the bib body
    beam(out,[0,-0.24,0.68],[0,-0.1,0.6],0.026,'brassy',0.45,6);             // spout
    box(out, -0.055,0.055, -0.33,-0.29, 0.66,0.7, 'brassy', 0.6, null, false); // handle
    // hose flaked in three coils on the grass
    for(let k=0;k<3;k++){ const R=0.3-k*0.055, z=0.03+k*0.045;
      for(let i=0;i<16;i++){ const a1=i/16*6.28, a2=(i+1)/16*6.28;
        beam(out,[Math.cos(a1)*R, 0.14+Math.sin(a1)*R*0.72, z],
                 [Math.cos(a2)*R, 0.14+Math.sin(a2)*R*0.72, z], 0.03, 'hose', 0.15+(i%2?0.5:0), 4); } }
    beam(out,[0,-0.16,0.58],[0.02,-0.04,0.14],0.03,'hose',0.35,4);         // the run down from the bib
    beam(out,[0.24,0.34,0.06],[0.42,0.44,0.05],0.026,'brassy',0.5,5);        // nozzle lying off the coil
    if(s.variant===1) box(out, -0.36,0.36, -0.12,0.44, 0,0.05, 'gravel', -0.1, gravelTex(), false);
    weeds(out, -0.36,0.36, -0.32,0.44, un*0.9, 479, 'weed', true);
  }
  function pCompostBin(out,s,ctx){
    const un=1-s.kept, w=0.9, d=0.8, h=0.86;
    for(const sx of [-w/2,w/2]) for(const sy of [-d/2,d/2]) box(out, sx-0.045,sx+0.045, sy-0.045,sy+0.045, 0,h, 'pole', 0.1, null, false);
    for(let k=0;k<5;k++){ const z=0.08+k*0.17;
      box(out, -w/2,w/2, -d/2-0.02,-d/2+0.03, z,z+0.1, 'plank', 0, grainTex(0.3), false);
      box(out, -w/2,w/2, d/2-0.03,d/2+0.02, z,z+0.1, 'plank', 0.05, grainTex(0.3), false);
      box(out, -w/2-0.02,-w/2+0.03, -d/2,d/2, z,z+0.1, 'plank', -0.1, grainTex(0.3), false);
      box(out, w/2-0.03,w/2+0.02, -d/2,d/2, z,z+0.1, 'plank', 0.1, grainTex(0.3), false); }
    heap(out, 0,0,0.1, w-0.16,d-0.16, 0.55, s.kept>0.5?'mulch':'compost', 487, 16, soilTex(), 0);
    if(s.kept<0.5) for(let i=0;i<5;i++) foliageMass(out, (hash2(i,2)-0.5)*(w-0.3), (hash2(i,5)-0.5)*(d-0.3), 0.2,0.18, 0.24,
      { z0:0.34, cell:0.08, rough:0.34, full:0.9, seed:491+i, mat:'leaf', matDark:'leafDark' });
    weeds(out, -w/2,w/2, -d/2,d/2, 0.3+un*0.8, 499, 'weed', true);
  }
  function pOilTank(out,s,ctx){
    const un=1-s.kept, R=0.34, L=1.05, z0=0.42;
    for(const sx of [-L/2+0.16, L/2-0.16]){
      beam(out,[sx-0.1,-0.24,0],[sx-0.1,-0.24,z0],0.028,'iron',0.1,4);
      beam(out,[sx+0.1,0.24,0],[sx+0.1,0.24,z0],0.028,'iron',0.1,4);
      beam(out,[sx-0.1,-0.24,z0-0.02],[sx+0.1,0.24,z0-0.02],0.026,'iron',0.2,4); }
    tube(out,[-L/2,0,z0+R],[L/2,0,z0+R], R, 14, 'body', 0, null, true);
    for(const sx of [-L/2+0.03, L/2-0.03]) tube(out,[sx-0.02,0,z0+R],[sx+0.02,0,z0+R], R*1.02, 14, 'body', 0.2, null, true);
    beam(out,[L/2-0.2,0,z0+R*2],[L/2-0.2,0,z0+R*2+0.13],0.03,'galv',0.5,5);
    beam(out,[-L/2+0.1,0.1,z0+R],[-L/2-0.14,0.22,z0+R*1.2],0.026,'galv',0.4,5);
    weeds(out, -L/2,L/2, -0.3,0.3, 0.2+un*0.9, 503, 'weed', true);
  }

  // ---------------- SITTING ----------------
  function pBench(out,s,ctx){
    const L=1.5+s.len*0.5, un=1-s.kept, sz=0.44;
    for(const sx of [-L/2+0.12, L/2-0.12]){
      box(out, sx-0.04,sx+0.04, -0.19,-0.11, 0,sz, 'body', 0.05, null, false);
      box(out, sx-0.04,sx+0.04, 0.11,0.19, 0,sz+0.02, 'body', 0.05, null, false);
      box(out, sx-0.035,sx+0.035, 0.12,0.18, sz,0.86, 'body', 0.1, null, false); }
    boards(out, -L/2,L/2, -0.22,0.2, sz, 0.035, 3, 'body', 0.1);
    for(let k=0;k<3;k++){ const z=0.58+k*0.1; box(out, -L/2,L/2, 0.15,0.19, z,z+0.07, 'body', 0.08, grainTex(0.28), false); }
    snowCap(out, -L/2,L/2, -0.22,0.2, sz+0.035, s.snowAmt);
    weeds(out, -L/2,L/2, -0.22,0.22, un*0.95, 509, 'weed', true);
  }
  function pPicnicTable(out,s,ctx){
    const L=1.85, un=1-s.kept, tz=0.74, bz=0.44;
    for(const sx of [-L/2+0.22, L/2-0.22]){
      for(const sgn of [-1,1]) beam(out,[sx,sgn*0.72,0],[sx,sgn*0.16,tz],0.045,'plank',0.05,4);
      beam(out,[sx,-0.6,bz],[sx,0.6,bz],0.04,'plank',0.1,4); }
    boards(out, -L/2,L/2, -0.38,0.38, tz, 0.04, 5, 'plank', 0.15);
    for(const sgn of [-1,1]) boards(out, -L/2+0.05,L/2-0.05, sgn*0.72-0.14, sgn*0.72+0.14, bz, 0.035, 2, 'plank', 0.05);
    snowCap(out, -L/2,L/2, -0.38,0.38, tz+0.04, s.snowAmt);
    weeds(out, -L/2,L/2, -0.86,0.86, un*0.9, 521, 'weed', true);
  }
  function pAdirondack(out,s,ctx){
    const un=1-s.kept, pair=s.variant!==1;
    const chair=(ox, rot)=>{
      const ct=Math.cos(rot), st=Math.sin(rot);
      const T=(x,y,z)=>[ox + x*ct - y*st, x*st + y*ct, z];
      const seatBack=0.34, seatFront=0.42;
      for(const sx of [-0.28,0.28]){
        out.push(F([T(sx-0.03,-0.34,0),T(sx+0.03,-0.34,0),T(sx+0.03,0.3,0.16),T(sx-0.03,0.3,0.16)],'body',0.05));
        out.push(F([T(sx-0.03,-0.34,0),T(sx-0.03,0.3,0.16),T(sx-0.03,0.3,0.2),T(sx-0.03,-0.34,0.04)],'body',-0.1));
        out.push(F([T(sx+0.03,0.3,0.16),T(sx+0.03,-0.34,0),T(sx+0.03,-0.34,0.04),T(sx+0.03,0.3,0.2)],'body',0.25)); }
      for(let i=0;i<5;i++){ const y=-0.3+i*0.14;
        out.push(F([T(-0.3,y,0.18+ (y+0.3)*0.12),T(0.3,y,0.18+(y+0.3)*0.12),T(0.3,y+0.1,0.19+(y+0.3)*0.12),T(-0.3,y+0.1,0.19+(y+0.3)*0.12)],'body',0.28+ (i%2?0.06:0))); }
      for(let i=0;i<6;i++){ const x=-0.26+i*0.104, hh=0.86-Math.abs(i-2.5)*0.03;
        out.push(F([T(x,-0.34,0.16),T(x+0.075,-0.34,0.16),T(x+0.075,-0.46,hh),T(x,-0.46,hh)],'body',0.1+ (i%2?0.08:0))); }
      for(const sx of [-0.34,0.34]){ out.push(F([T(sx-0.06,-0.2,0.62),T(sx+0.06,-0.2,0.62),T(sx+0.06,0.16,0.6),T(sx-0.06,0.16,0.6)],'body',0.4));
        beam(out,T(sx,0.14,0.6),T(sx,0.2,0.2),0.028,'body',0.1,4); }
    };
    chair(pair?-0.42:0, pair?0.16:0);
    if(pair) chair(0.46, -0.2);
    weeds(out, -0.9,0.9, -0.6,0.5, un*0.9, 523, 'weed', true);
  }
  function pFirePit(out,s,ctx){
    const un=1-s.kept, R=0.56, rnd=mulberry32(541);
    for(let i=0;i<12;i++){ const a=i/12*6.28, rr=0.09+rnd()*0.04;
      prismT(out, Math.cos(a)*R, Math.sin(a)*R*0.92, rr, 0, 0.16+rnd()*0.07, 6, 'stone', -0.1+rnd()*0.5, stoneTex()); }
    capDisc(out, 0,0, R*0.78, 0.03, 12, 'ash', -0.55, soilTex());
    if(s.variant!==1){ for(let i=0;i<4;i++){ const a=i/4*6.28+0.5;
      beam(out,[Math.cos(a)*R*0.5,Math.sin(a)*R*0.45,0.03],[Math.cos(a+2.6)*0.1,Math.sin(a+2.6)*0.09,0.3],0.05,'firewood',-0.05+i*0.12,5); } }
    for(let i=0;i<(s.len>0.4?3:2);i++){ const a=i/3*6.28+1.1, d=R+0.5;
      prismT(out, Math.cos(a)*d, Math.sin(a)*d*0.9, 0.19, 0, 0.4, 8, 'pole', 0.05, grainTex(0.16)); }
    weeds(out, -R-0.7,R+0.7, -R-0.6,R+0.6, un*0.7, 547, 'weed');
  }

  // ---------------- PLAY & WORK ----------------
  function pSwingSet(out,s,ctx){
    const un=1-s.kept, span=2.1, h=1.95, sp=0.62;
    for(const sx of [-span/2, span/2]) for(const sy of [-sp/2, sp/2]){
      beam(out,[sx+ (sx<0?0.12:-0.12)*0,sy,0],[sx*0.86,0,h],0.05,'pole',sy<0?0.15:-0.05,4); }
    beam(out,[-span/2*0.86-0.1,0,h],[span/2*0.86+0.1,0,h],0.055,'pole',0.3,5);
    const seats=[-0.44, 0.44];
    for(const sx of seats){ const drop=h-0.5, sway=un*0.1;
      for(const off of [-0.16,0.16]){
        beam(out,[sx+off,0,h-0.03],[sx+off+sway,0.04,h-drop],0.014,'galv',0.4,3); }
      box(out, sx-0.17+sway,sx+0.17+sway, -0.07,0.07, h-drop-0.03, h-drop, 'body', 0.2, null, false); }
    if(s.variant===1){ // a tyre swing on the near end instead of one plank seat
      const sx=0.44; for(let i=0;i<10;i++){ const a=i/10*6.28, a2=(i+1)/10*6.28, R=0.22;
        out.push(F([[sx+Math.cos(a)*R, Math.sin(a)*R*0.4, h-1.34+Math.abs(Math.sin(a))*0.06],
                    [sx+Math.cos(a2)*R, Math.sin(a2)*R*0.4, h-1.34+Math.abs(Math.sin(a2))*0.06],
                    [sx+Math.cos(a2)*(R*0.66), Math.sin(a2)*R*0.3, h-1.36],
                    [sx+Math.cos(a)*(R*0.66), Math.sin(a)*R*0.3, h-1.36]],'rubber',0.1+ (i%2?0.3:0))); } }
    weeds(out, -span/2,span/2, -sp/2,sp/2, 0.2+un*1.0, 557, 'weed', true);
  }
  function pSandbox(out,s,ctx){
    const un=1-s.kept, w=1.15, d=1.0;
    for(const [x0,x1,y0,y1] of [[-w/2,w/2,-d/2,-d/2+0.11],[-w/2,w/2,d/2-0.11,d/2],[-w/2,-w/2+0.11,-d/2,d/2],[w/2-0.11,w/2,-d/2,d/2]])
      box(out, x0,x1,y0,y1, 0,0.22, 'pole', 0.05, grainTex(0.3), false);
    for(const [cx2,cy2] of [[-w/2+0.055,-d/2+0.055],[w/2-0.055,-d/2+0.055],[-w/2+0.055,d/2-0.055],[w/2-0.055,d/2-0.055]])
      box(out, cx2-0.075,cx2+0.075, cy2-0.075,cy2+0.075, 0,0.3, 'pole', 0.25, null, false);
    box(out, -w/2+0.11,w/2-0.11, -d/2+0.11,d/2-0.11, 0.02,0.15, 'sand', 0.2, gravelTex(), false);
    if(s.kept>0.35){ const rnd=mulberry32(563);
      for(let i=0;i<3;i++) prismT(out, (rnd()-0.5)*0.6, (rnd()-0.5)*0.5, 0.07, 0.15, 0.24, 7, i%2?'body':'flagred', 0.3); }
    else { heap(out, 0,0,0.15, w-0.3,d-0.3, 0.1, 'sand', 569, 8, gravelTex(), 0);
      weeds(out, -w/2+0.14,w/2-0.14, -d/2+0.14,d/2-0.14, un*0.9, 571, 'weed', true); }
    weeds(out, -w/2,w/2, -d/2,d/2, un*0.6, 577, 'weed');
  }
  function pDoghouse(out,s,ctx){
    const un=1-s.kept, w=0.78, d=0.94, wallZ=0.6;
    box(out, -w/2,w/2, -d/2,d/2, 0.05,wallZ, 'body', 0, plankTex(0.16), true);
    box(out, -w/2-0.03,w/2+0.03, -d/2-0.03,d/2+0.03, 0,0.06, 'pole', -0.1, null, false);
    const rz=wallZ+0.34;
    out.push(F([[-w/2-0.06,-d/2-0.05,wallZ],[0,-d/2-0.05,rz],[0,d/2+0.05,rz],[-w/2-0.06,d/2+0.05,wallZ]], s.variant===1?'shingle':'body', 0.35, 0, [[0,0],[0.55,0],[0.55,d+0.1],[0,d+0.1]], slatTex(0.12)));
    out.push(F([[0,-d/2-0.05,rz],[w/2+0.06,-d/2-0.05,wallZ],[w/2+0.06,d/2+0.05,wallZ],[0,d/2+0.05,rz]], s.variant===1?'shingle':'body', -0.15, 0, [[0,0],[0.55,0],[0.55,d+0.1],[0,d+0.1]], slatTex(0.12)));
    out.push(F([[-w/2,-d/2,wallZ],[w/2,-d/2,wallZ],[0,-d/2,rz]], 'body', 0.1));
    out.push(F([[w/2,d/2,wallZ],[-w/2,d/2,wallZ],[0,d/2,rz]], 'body', -0.4));
    dY(out, d/2+0.001, 1, -0.15,0.15, 0.08, 0.44, 'doorhole', -1.6, 0.03);
    if(s.variant!==1) box(out, 0.34,0.5, d/2+0.16,d/2+0.32, 0,0.06, 'galv', 0.3, null, false);  // the bowl
    weeds(out, -w/2,w/2, -d/2,d/2, un*0.85, 587, 'weed', true);
  }
  function pSawhorses(out,s,ctx){
    const un=1-s.kept;
    const horse=(ox)=>{ const hz=0.62;
      box(out, ox-0.32,ox+0.32, -0.05,0.05, hz,hz+0.075, 'pole', 0.15, grainTex(0.26), false);
      for(const sx of [-0.24,0.24]) for(const sy of [-1,1])
        beam(out,[ox+sx+sy*0.02, sy*0.3, 0],[ox+sx, sy*0.03, hz],0.028,'pole',sy<0?0.1:-0.1,4); };
    horse(-0.55); horse(0.55);
    if(s.variant!==1){ boards(out, -1.0,1.0, -0.16,0.16, 0.695, 0.035, 2, 'plank', 0.2);
      if(s.kept<0.5){ const rnd=mulberry32(593);
        for(let i=0;i<4;i++) prismT(out, -0.8+rnd()*1.6, (rnd()-0.5)*0.3, 0.06, 0, 0.05, 6, 'plank', 0.1); } }
    weeds(out, -1.0,1.0, -0.32,0.32, un*0.9, 599, 'weed', true);
  }
  function pToolLean(out,s,ctx){
    const un=1-s.kept, rnd=mulberry32(601);
    post(out, 0,0, 0, 1.32, 0.06, 'pole', 0.05, 0, 0, true, grainTex(0.3));
    capDisc(out, 0,0, 1.325, 0.075, 4, 'pole', 0.45);
    // spade: blade on the ground, shaft to the post, D-grip at the top
    { const bx=-0.42, by=0.2, top=[0.0,0.07,1.16];
      box(out, bx-0.085,bx+0.085, by-0.035,by+0.035, 0,0.26, 'galv', 0.35, null, false);
      out.push(F([[bx-0.085,by-0.035,0.02],[bx+0.085,by-0.035,0.02],[bx,by-0.035,-0.0]],'galv',0.5));
      beam(out,[bx,by,0.24],[top[0],top[1],top[2]],0.026,'pole',0.25,5);
      box(out, top[0]-0.055,top[0]+0.055, top[1]-0.02,top[1]+0.02, top[2],top[2]+0.11, 'pole', 0.4, null, false); }
    // rake: head across the ground with visible tines
    { const bx=0.44, by=0.26;
      box(out, bx-0.19,bx+0.19, by-0.025,by+0.025, 0.09,0.13, 'galv', 0.4, null, false);
      for(let k=0;k<9;k++){ const tx=bx-0.17+k*0.0425;
        beam(out,[tx,by,0.1],[tx,by+0.035,0.0],0.012,'galv',0.45,3); }
      beam(out,[bx,by,0.12],[0.03,0.06,1.22],0.024,'pole',0.2,5); }
    if(s.kept<0.45){ for(let i=0;i<3;i++){ const px=(rnd()-0.5)*0.9, py=0.3+rnd()*0.3;
      tube(out,[px,py,0],[px,py,0.14], 0.09, 9, 'clay', 0.1, null, false);
      tube(out,[px,py,0.12],[px,py,0.15], 0.105, 9, 'clay', 0.3, null, false); } }
    weeds(out, -0.5,0.6, -0.16,0.5, un*1.0, 607, 'weed', true);
  }

  // ---------------- LAWN ORNAMENTS ----------------
  function pBuoyPost(out,s,ctx){
    const un=1-s.kept, h=1.6+s.len*0.5, rnd=mulberry32(613);
    post(out, 0,0, 0, h, 0.06, 'pole', 0, un*0.09, 1.4, true, grainTex(0.3));
    capDisc(out, 0,0, h+0.005, 0.075, 4, 'pole', 0.4);
    const cols=['buoyA','buoyB','buoyC','buoyD'];
    const n=3+Math.round(s.len*3);
    for(let i=0;i<n;i++){
      const side=(i%2)?1:-1, z=h-0.2-i*0.19, ox=side*(0.1+ (i%3)*0.02);
      const tilt=(rnd()-0.5)*0.4;
      const p0=[ox,0.02+ (rnd()-0.5)*0.05, z], p1=[ox+Math.sin(tilt)*0.34, 0.02, z-0.34];
      tube(out, p0,p1, 0.05, 8, cols[i%cols.length], 0.05);
      tube(out, [p0[0],p0[1],p0[2]+0.04],[p1[0]*0.35,p1[1],p1[2]+0.24], 0.075, 8, cols[i%cols.length], 0.2);
      beam(out,[0,0.02,z+0.05],[p0[0],p0[1],p0[2]],0.012,'rope',0.4,3);
    }
    if(s.variant===1) box(out, -0.22,0.22, -0.02,0.02, h-0.06,h+0.04, 'pole', 0.3, null, false);
    weeds(out, -0.22,0.22, -0.18,0.18, un*0.9, 617, 'weed', true);
  }
  function pDoryPlanter(out,s,ctx){
    const Fs=folState(s,false), L=1.7+s.len*0.5, un=1-s.kept;
    // half a dory, cut on its centreline, planted — the classic dooryard boat
    const nb=9, hw=0.34, hz=0.5;
    for(let i=0;i<nb;i++){
      const t0=i/nb*2-1, t1=(i+1)/nb*2-1;
      const b0=hw*Math.sqrt(Math.max(0,1-t0*t0*0.86)), b1=hw*Math.sqrt(Math.max(0,1-t1*t1*0.86));
      const z0=hz*(0.7+0.3*Math.pow(Math.abs(t0),2.2)), z1=hz*(0.7+0.3*Math.pow(Math.abs(t1),2.2));
      const x0=t0*L/2, x1=t1*L/2;
      out.push(F([[x0,-b0,0.06],[x1,-b1,0.06],[x1,-b1,z1],[x0,-b0,z0]], 'body', -0.15, 0, [[0,0],[0.2,0],[0.2,z1],[0,z1]], plankTex(0.1)));
      out.push(F([[x1,b1,0.06],[x0,b0,0.06],[x0,b0,z0],[x1,b1,z1]], 'body', 0.25, 0, [[0,0],[0.2,0],[0.2,z0],[0,z0]], plankTex(0.1)));
      out.push(F([[x0,-b0,0.06],[x0,b0,0.06],[x1,b1,0.06],[x1,-b1,0.06]], 'body', -0.6));
      // gunwale strake
      out.push(F([[x0,-b0-0.02,z0],[x1,-b1-0.02,z1],[x1,-b1+0.03,z1],[x0,-b0+0.03,z0]], 'trimw', 0.5));
      out.push(F([[x1,b1+0.02,z1],[x0,b0+0.02,z0],[x0,b0-0.03,z0],[x1,b1-0.03,z1]], 'trimw', 0.5));
      // soil inside
      out.push(F([[x0,-b0+0.04,z0-0.14],[x1,-b1+0.04,z1-0.14],[x1,b1-0.04,z1-0.14],[x0,b0-0.04,z0-0.14]], 'soil', -0.5, 0, [[x0,-b0],[x1,-b1],[x1,b1],[x0,b0]], soilTex()));
    }
    if(s.planted){ const n=Math.round(4+s.len*3);
      for(let i=0;i<n;i++){ const x=-L/2+ (i+0.6)*L/(n+0.2);
        const bw=hw*Math.sqrt(Math.max(0.1,1-Math.pow(x/(L/2),2)*0.86));
        plant(ctx,'flower', i%2?'OxeyeDaisy':'Lupin Purple', x, 0, 0.4, {});
        if(!ctx||!ctx.external){ foliageMass(out, x,0, 0.22, bw*1.4, 0.22, { z0:0.36, cell:0.075, rough:0.3, full:Fs.full, seed:619+i, mat:'leaf', matDark:'leafDark' });
          if(Fs.full>0.4) prismT(out, x, 0, 0.04, 0.62, 0.7, 5, i%2?'bloomA':'bloomC', 0.7); } } }
    weeds(out, -L/2,L/2, -hw,hw, un*0.8, 631, 'weed');
  }
  function pAnchorSet(out,s,ctx){
    const un=1-s.kept, sc=0.95+s.len*0.35, rnd=mulberry32(641);
    for(let i=0;i<9;i++){ const a=i/9*6.28, rr=0.3+rnd()*0.06;   // a ring of beach stone it stands in
      prismT(out, Math.cos(a)*rr, Math.sin(a)*rr*0.85, 0.07+rnd()*0.03, 0, 0.08+rnd()*0.04, 6, 'stone', -0.1+rnd()*0.5, stoneTex()); }
    const H=1.15*sc, lean=0.1;
    const SX=(z)=>Math.sin(lean)*z;                                 // the whole anchor cants back a little
    pipe(out, 0,0, 0.052, 0.04, H*0.42, 'iron', 0.1, 8);            // shank, lower
    tube(out,[SX(H*0.42),0,H*0.42],[SX(H),0,H], 0.048, 8, 'iron', 0.15);
    tube(out,[SX(H)-0.02,0,H],[SX(H)+0.02,0,H+0.07], 0.075, 10, 'iron', 0.45);   // the ring boss
    for(let i=0;i<10;i++){ const a1=i/10*6.28, a2=(i+1)/10*6.28, R=0.085;
      beam(out,[SX(H)+Math.cos(a1)*R*0.3, Math.sin(a1)*R, H+0.14+Math.cos(a1)*R],
               [SX(H)+Math.cos(a2)*R*0.3, Math.sin(a2)*R, H+0.14+Math.cos(a2)*R], 0.019, 'iron', 0.3+(i%2?0.35:0), 4); }
    beam(out,[SX(H*0.92)-0.42*sc,0.02,H*0.92],[SX(H*0.92)+0.42*sc,0.02,H*0.9],0.042,'iron',0.4,5);  // stock
    for(const sgn of [-1,1]){ const tipX=sgn*0.46*sc, tipZ=0.5*sc;
      for(let k=0;k<4;k++){ const t0=k/4, t1=(k+1)/4;
        const P=(t)=>[sgn*0.46*sc*Math.pow(t,1.3), 0, 0.1+ (0.4*sc)*Math.sin(t*1.35)];
        beam(out,P(t0),P(t1),0.046-k*0.004,'iron',0.05+k*0.08,5); }
      const bx=tipX, bz=tipZ;                                        // the palm / fluke
      out.push(F([[bx-sgn*0.06,-0.055,bz-0.16],[bx+sgn*0.17,-0.055,bz+0.16],[bx-sgn*0.02,-0.055,bz+0.2]],'iron',0.45));
      out.push(F([[bx+sgn*0.17,0.055,bz+0.16],[bx-sgn*0.06,0.055,bz-0.16],[bx-sgn*0.02,0.055,bz+0.2]],'iron',0.05));
      out.push(F([[bx-sgn*0.06,-0.055,bz-0.16],[bx+sgn*0.17,-0.055,bz+0.16],[bx+sgn*0.17,0.055,bz+0.16],[bx-sgn*0.06,0.055,bz-0.16]],'iron',0.3)); }
    for(let k=0;k<3;k++){ const R=0.24-k*0.055;                      // the coil of rope beside it
      for(let i=0;i<12;i++){ const a1=i/12*6.28, a2=(i+1)/12*6.28;
        beam(out,[0.52+Math.cos(a1)*R, 0.42+Math.sin(a1)*R*0.62, 0.03+k*0.035],
                 [0.52+Math.cos(a2)*R, 0.42+Math.sin(a2)*R*0.62, 0.03+k*0.035], 0.024, 'rope', 0.15+(i%2?0.4:0), 4); } }
    weeds(out, -0.5,0.8, -0.35,0.6, un*0.9, 641, 'weed', true);
  }
  function pTrapBench(out,s,ctx){
    const un=1-s.kept, L=1.4, seatZ=0.46;
    for(const sx of [-L/2+0.28, L/2-0.28]){  // two wire traps as the legs
      box(out, sx-0.3,sx+0.3, -0.24,0.24, 0.02,seatZ-0.04, 'trapnet', 0, meshTex(), false);
      for(const zz of [0.05, seatZ-0.07]) for(const sy of [-0.24,0.24])
        beam(out,[sx-0.31,sy,zz],[sx+0.31,sy,zz],0.024,'galv',0.45,4);
      for(const sx2 of [-0.3,0.3]) for(const sy of [-0.24,0.24])
        beam(out,[sx+sx2,sy,0.04],[sx+sx2,sy,seatZ-0.06],0.022,'galv',0.3,4); }
    boards(out, -L/2,L/2, -0.24,0.24, seatZ-0.04, 0.04, 3, 'plank', 0.15);
    if(s.variant===1){ for(let i=0;i<3;i++) tube(out,[-L/2+0.2+i*0.4, -0.3, seatZ+0.02],[-L/2+0.24+i*0.4,-0.34,seatZ+0.18],0.055,8, i%2?'buoyA':'buoyB', 0.3); }
    snowCap(out, -L/2,L/2, -0.24,0.24, seatZ, s.snowAmt);
    weeds(out, -L/2,L/2, -0.3,0.3, un*0.95, 643, 'weed', true);
  }
  function pBirdbath(out,s,ctx){
    const un=1-s.kept, h=0.82, R=0.34;
    prismT(out, 0,0, 0.24, 0, 0.09, 10, 'concrete', 0.05);
    tube(out,[0,0,0.09],[0,0,h-0.1], 0.1, 10, 'concrete', 0.1, null, false);
    tube(out,[0,0,h-0.16],[0,0,h], R, 14, 'concrete', 0.2, null, false);
    capDisc(out, 0,0, h, R*0.98, 14, 'concrete', 0.45);
    capDisc(out, 0,0, h-0.035, R*0.82, 14, s.variant===1?'ice':'water', -0.2);
    weeds(out, -0.28,0.28, -0.26,0.26, un*0.9, 647, 'weed', true);
  }
  function pBirdhouse(out,s,ctx){
    const un=1-s.kept, h=2.1+s.len*0.5;
    post(out, 0,0, 0, h, 0.045, 'pole', 0, un*0.1, 0.9, true, grainTex(0.3));
    const w=0.26, d=0.24;
    box(out, -w/2,w/2, -d/2,d/2, h,h+0.3, 'body', 0.05, plankTex(0.12), true);
    out.push(F([[-w/2-0.05,-d/2-0.04,h+0.3],[0,-d/2-0.04,h+0.46],[0,d/2+0.04,h+0.46],[-w/2-0.05,d/2+0.04,h+0.3]],'trimw',0.35));
    out.push(F([[0,-d/2-0.04,h+0.46],[w/2+0.05,-d/2-0.04,h+0.3],[w/2+0.05,d/2+0.04,h+0.3],[0,d/2+0.04,h+0.46]],'trimw',-0.1));
    out.push(F([[-w/2,-d/2,h+0.3],[w/2,-d/2,h+0.3],[0,-d/2,h+0.46]],'body',0.15));
    out.push(F([[w/2,d/2,h+0.3],[-w/2,d/2,h+0.3],[0,d/2,h+0.46]],'body',-0.4));
    dY(out, d/2+0.001, 1, -0.045,0.045, h+0.13, h+0.22, 'doorhole', -1.8, 0.03);
    beam(out,[0,d/2,h+0.1],[0,d/2+0.08,h+0.1],0.014,'pole',0.4,4);
    weeds(out, -0.16,0.16, -0.16,0.16, un*0.8, 653, 'weed');
  }

  // ---------------- PATHS & GROUND ----------------
  function pSteppingStones(out,s,ctx){
    const L=runLen(s,1.2,1.4), rnd=mulberry32(659), un=1-s.kept;
    const n=Math.max(3, Math.round(L/0.42));
    for(let i=0;i<n;i++){
      const x=-L/2+ (i+0.5)*L/n, y=(rnd()-0.5)*0.16;
      const r=0.17+rnd()*0.06, sides=5+Math.floor(rnd()*3);
      const p=[]; for(let k=0;k<sides;k++){ const a=k/sides*6.28+rnd()*0.2; const rr=r*(0.82+rnd()*0.3);
        p.push([x+Math.cos(a)*rr, y-Math.sin(a)*rr*0.9]); }
      const z=0.035+rnd()*0.02;
      slab(out, p, z, s.variant===1?'concrete':'flag', 0.15+rnd()*0.3, stoneTex());
      for(let k=0;k<p.length;k++){ const q=p[k], q2=p[(k+1)%p.length];
        wall(out, q[0],q[1], q2[0],q2[1], 0, z, s.variant===1?'concrete':'flag', null, -0.1); }
      if(un>0.3) weeds(out, x-r,x+r, y-r*0.9,y+r*0.9, un*0.6, 661+i, 'weed');
    }
  }
  function pGravelApron(out,s,ctx){
    const w=runLen(s,1.6,1.8), d=1.3+s.len*0.5, rnd=mulberry32(673);
    const shell=s.variant===1;
    box(out, -w/2,w/2, -d/2,d/2, 0,0.05, shell?'shell':'gravel', 0.1, gravelTex(), false);
    for(let i=0;i<Math.round(w*d*26);i++){
      const x=(rnd()-0.5)*w*0.98, y=(rnd()-0.5)*d*0.98;
      prismT(out, x,y, 0.02+rnd()*0.03, 0.05, 0.06+rnd()*0.025, 5, shell?'shell':'gravel', -0.4+rnd()*1.2); }
    if(s.kept<0.5){ // ruts and a weed stripe up the middle, which is what a soft drive does
      for(const yy of [-d*0.22, d*0.22]) box(out, -w/2,w/2, yy-0.14,yy+0.14, 0.03,0.045, 'soil', -0.3, soilTex(), false);
      weeds(out, -w/2+0.1,w/2-0.1, -0.1,0.1, (1-s.kept)*0.9, 677, 'weed', true); }
    weeds(out, -w/2,w/2, -d/2,d/2, (1-s.kept)*0.35, 683, 'weed');
  }
  function pLawnEdge(out,s,ctx){
    const L=runLen(s,1.4,1.4), un=1-s.kept, rnd=mulberry32(691);
    // the mown strip: turf slab, a cut edge, and a soil lip. Reads as "someone owns a mower".
    box(out, -L/2,L/2, -0.5,0.5, 0,0.055, 'turf', 0.1, null, false);
    const n=Math.round(L/0.09);
    for(let i=0;i<n;i++){ const x=-L/2+ (i+0.5)*L/n;
      const hh=0.055+ (0.01+un*0.13)*(0.5+hash2(i,3));
      box(out, x-0.045,x+0.045, 0.34+ (hash2(i,7)-0.5)*un*0.1, 0.5, 0.03, hh, 'turf', -0.1+hash2(i,11)*0.6, null, false); }
    box(out, -L/2,L/2, 0.5,0.62, 0,0.04, 'soil', -0.2, soilTex(), false);
    if(un>0.4) weeds(out, -L/2,L/2, -0.45,0.45, un*0.7, 701, 'weed', true);
  }

  // ---------------- SIGNS & POLES ----------------
  function pFlagpole(out,s,ctx){
    const h=5.2+s.len*1.8, un=1-s.kept;
    prismT(out, 0,0, 0.2, 0, 0.12, 10, 'concrete', 0.1);
    tube(out,[0,0,0.1],[0,0,h], 0.045, 8, 'alum', 0.25, null, false);
    prismT(out, 0,0, 0.06, h, h+0.09, 6, 'brassy', 0.6);
    const fl=0.95, fh=0.5, z=h-0.18;
    for(let i=0;i<5;i++){ const t0=i/5, t1=(i+1)/5;
      const wv=(t)=>Math.sin(t*3.2+0.6)*0.1*(0.35+t);
      out.push(F([[0.05+fl*t0, wv(t0), z],[0.05+fl*t1, wv(t1), z],
                  [0.05+fl*t1, wv(t1)+0.02, z-fh],[0.05+fl*t0, wv(t0)+0.02, z-fh]],
        (i===0||i===4)?'flagred':'flagwhite', 0.25+ (i%2?0.12:0))); }
    beam(out,[0.04,0,z],[0.04,0,z-fh],0.012,'rope',0.4,3);
    weeds(out, -0.24,0.24, -0.24,0.24, un*0.7, 709, 'weed');
  }
  function pNameSign(out,s,ctx){
    const un=1-s.kept, h=1.46;
    post(out, -0.34,0, 0, h, 0.055, 'pole', 0.05, un*0.07, 1.1, true, grainTex(0.3));
    capDisc(out, -0.34,0, h+0.005, 0.07, 4, 'pole', 0.45);
    box(out, -0.4,0.44, -0.035,0.035, h-0.1,h-0.02, 'pole', 0.3, grainTex(0.3), false);   // the arm
    beam(out,[-0.34,0,h-0.42],[-0.05,0,h-0.1],0.024,'pole',0.15,4);                       // knee brace
    const bw=0.66, bh=0.34, bz=h-0.5, bx0=-0.2;
    box(out, bx0,bx0+bw, -0.028,0.028, bz,bz+bh, 'body', 0.15, null, true);               // painted board
    dY(out, 0.03, 1, bx0+0.04, bx0+bw-0.04, bz+0.05, bz+bh-0.05, 'slate', -0.2);          // the panel
    for(let i=0;i<5;i++) dY(out, 0.034, 1, bx0+0.09+i*0.105, bx0+0.15+i*0.105, bz+0.13, bz+bh-0.11, 'trimw', 0.5, 0.08); // lettering
    for(const sx of [bx0+0.07, bx0+bw-0.07]) beam(out,[sx,0,h-0.09],[sx,0,bz+bh],0.013,'galv',0.45,3);
    if(s.variant===1) tube(out,[bx0+bw*0.5,0.04,bz-0.13],[bx0+bw*0.5,0.06,bz-0.13],0.075,8,'buoyA',0.3);
    weeds(out, -0.44,0.5, -0.14,0.14, un*0.85, 719, 'weed', true);
  }
  function pNumberPost(out,s,ctx){
    const un=1-s.kept, h=1.0;
    post(out, 0,0, 0, h, 0.045, 'pole', 0.05, un*0.1, 0.6, true, grainTex(0.3));
    box(out, -0.13,0.13, -0.028,0.028, h-0.3,h+0.02, 'body', 0.15, null, true);
    for(let i=0;i<3;i++) dY(out, 0.03, 1, -0.09+i*0.065, -0.05+i*0.065, h-0.24, h-0.08, 'trimw', -0.4);
    capDisc(out, 0,0, h+0.055, 0.055, 4, 'pole', 0.4);
    weeds(out, -0.16,0.16, -0.14,0.14, un*0.9, 727, 'weed', true);
  }
  function pFarmStand(out,s,ctx){
    const un=1-s.kept, w=1.3, d=0.72, tz=0.8;
    for(const sx of [-w/2+0.08, w/2-0.08]) for(const sy of [-d/2+0.06, d/2-0.06])
      box(out, sx-0.042,sx+0.042, sy-0.042,sy+0.042, 0,tz, 'pole', 0.05, null, false);
    boards(out, -w/2,w/2, -d/2,d/2, tz, 0.038, 4, 'plank', 0.15);
    box(out, -w/2,w/2, -d/2,-d/2+0.05, tz-0.34,tz-0.02, 'plank', -0.15, plankTex(0.16), false);  // back skirt
    const rz=tz+0.62;
    for(const sx of [-w/2+0.08, w/2-0.08]) box(out, sx-0.033,sx+0.033, -d/2+0.02,-d/2+0.09, tz,rz, 'pole', 0.1, null, false);
    out.push(F([[-w/2-0.04,-d/2-0.02,rz],[w/2+0.04,-d/2-0.02,rz],[w/2+0.04,d/2+0.02,rz-0.06],[-w/2-0.04,d/2+0.02,rz-0.06]], 'shingle', 0.3, 0,
      [[0,0],[w+0.08,0],[w+0.08,d+0.04],[0,d+0.04]], slatTex(0.13)));
    out.push(F([[-w/2-0.04,d/2+0.02,rz-0.06],[w/2+0.04,d/2+0.02,rz-0.06],[w/2+0.04,d/2+0.02,rz-0.11],[-w/2-0.04,d/2+0.02,rz-0.11]], 'shingle', -0.3));
    dY(out, -d/2+0.012, -1, -w/2+0.14, w/2-0.14, tz+0.02, tz+0.24, 'slate', -0.25);                // the price board
    for(let i=0;i<4;i++) dY(out, -d/2+0.008, -1, -w/2+0.24+i*0.2, -w/2+0.34+i*0.2, tz+0.08, tz+0.19, 'trimw', 0.5, 0.08);
    if(s.variant!==1){ const rnd=mulberry32(733);
      for(let i=0;i<3;i++){ const x=-w/2+0.3+i*0.35, y=0.06+ (i%2)*0.1;
        tube(out,[x,y,tz+0.038],[x,y,tz+0.2], 0.125, 9, 'basket', 0.05, wickerish(), false);
        capDisc(out, x,y, tz+0.2, 0.13, 9, 'basket', 0.4, wickerish());
        heap(out, x,y, tz+0.19, 0.2,0.2, 0.13, i===1?'bloomC':'produce', 739+i, 7, null, 0.35); }
      box(out, w/2-0.24,w/2-0.06, -0.06,0.1, tz+0.038,tz+0.16, 'tin', 0.3, null, false); }     // the honour box
    weeds(out, -w/2,w/2, -d/2,d/2, un*0.8, 751, 'weed', true);
  }
  function wickerish(){ const c=0.055; return (u,v)=>{ const fv=((v%c)+c)%c;
    return fv<c*0.3?-2:(hash2(Math.floor(u*22)|0, Math.floor(v/c)|0)<0.45?-1:0); }; }

  const T=true;
  // ---------------- MORE GREENERY (second pass) ----------------
  function pHedgeCorner(out,s,ctx){
    const un=1-s.kept, Fs=folState(s,false), arm=runLen(s,0.95,0.45);
    const h=(s.variant===1?1.32:0.88), d=0.6, off=-arm/2+d/2;
    foliageMass(out, 0,off, arm, d, h, { cell:0.115, rough:0.035+un*0.42, full:Fs.full, seed:71,
      mat:'leaf', matDark:'leafDark', snow:s.snowAmt, flatTop:s.kept>0.72 });
    foliageMass(out, off,d/2, d, arm-d, h*0.99, { cell:0.115, rough:0.04+un*0.42, full:Fs.full, seed:73,
      plan:2.3, planV:6, mat:'leaf', matDark:'leafDark', snow:s.snowAmt, flatTop:s.kept>0.72 });
    if(Fs.full<0.4){ const rnd=mulberry32(77);
      for(let i=0;i<12;i++){ const along=-arm/2+rnd()*arm, xArm=rnd()<0.5;
        const x=xArm?along:off, y=xArm?off:along;
        beam(out,[x,y,0],[x+(rnd()-0.5)*0.2, y+(rnd()-0.5)*0.2, h*(0.55+rnd()*0.45)],0.017,'twig',-0.1,3); } }
    weeds(out, -arm/2,arm/2, -arm/2,off+d/2, un*0.7, 79, 'weed');
  }
  function pHydrangea(out,s,ctx){
    const un=1-s.kept, Fs=folState(s,false), rnd=mulberry32(1103);
    const r=0.55+s.len*0.2, h=0.85+s.len*0.3;
    for(let i=0;i<5;i++){ const a=i/5*6.28+0.5;
      beam(out,[0,0,0],[Math.cos(a)*r*0.42, Math.sin(a)*r*0.36, h*0.55],0.03,'twig',-0.1,4); }
    foliageMass(out, 0,0, r*2, r*1.75, h, { cell:0.115, rough:0.14+un*0.32, full:Fs.full, seed:53, taper:0.36,
      mat:'leaf', matDark:'leafDark', snow:s.snowAmt });
    if(s.snowAmt>0.2 && Fs.full<0.5) snowDrift(out, r*1.9, r*1.6, 0.5, s.snowAmt, 53);
    const mop = Fs.ph==='bloom'||Fs.ph==='leaf'||Fs.ph==='green'||Fs.ph==='fruit'||Fs.ph==='turn';
    if(mop){
      const m = (Fs.ph==='turn') ? 'bloomB' : (s.variant===1 ? 'bloomC' : 'bloomD');
      const n = Fs.ph==='bloom'?8:(Fs.ph==='leaf'||Fs.ph==='green'?6:4);
      for(let i=0;i<n;i++){ const a=rnd()*6.28, rr=r*(0.2+rnd()*0.62), rz=0.085+rnd()*0.03;
        clump(out, Math.cos(a)*rr, Math.sin(a)*rr*0.82, h*(0.72+rnd()*0.26)+rz*0.4,
          rz, rz*0.95, rz*0.85, m, m, 0.4, 611+i*3, 0.09, false); } }
    weeds(out, -r,r, -r*0.85,r*0.85, un*0.8, 1107, 'weed');
  }
  function pTrellisVine(out,s,ctx){
    const L=runLen(s,1.0,0.6), un=1-s.kept, Fs=folState(s,false), rnd=mulberry32(1139);
    const h=1.7+s.len*0.3;
    for(const px of [-L/2+0.04, L/2-0.04]) box(out, px-0.035,px+0.035, -0.032,0.032, 0,h, 'body', 0.05, grainTex(0.3), false);
    for(let k=0;k<=5;k++){ const z=0.16+k*(h-0.24)/5;
      box(out, -L/2,L/2, -0.018,0.018, z-0.014,z+0.014, 'body', 0.05, null, true); }
    for(let i=-2;i<=2;i++){ const x=i*L/5;
      box(out, x-0.015,x+0.015, -0.016,0.016, 0.1,h-0.05, 'body', -0.1, null, true); }
    for(let i=0;i<3;i++){ const bx=(i-1)*L*0.3;
      beam(out,[bx,0.02,0],[bx+(rnd()-0.5)*0.22, 0.05, h*(0.75+rnd()*0.25)],0.021,'twig',-0.1,4); }
    if(Fs.full>0.15){ const n=Math.round(18+Fs.full*20);
      for(let i=0;i<n;i++){ const x=(rnd()*2-1)*L*0.47, z=0.13+(i/n)*(h-0.2)+(rnd()-0.5)*0.16;
        const r2=0.095+rnd()*0.03;
        clump(out, x, 0.045+rnd()*0.07, z, r2, r2*0.72, r2*0.95, 'leaf','leafDark', 0, 901+i*5, 0.27, i%3>0); } }
    if(Fs.ph==='bloom'||Fs.ph==='leaf'){ const m=s.variant===1?'bloomA':'bloomC';
      for(let i=0;i<7;i++){ const zz=0.35+rnd()*(h-0.6);
        prismT(out, (rnd()*2-1)*L*0.42, 0.09, 0.034, zz, zz+0.07, 5, m, 0.72); } }
    if(Fs.ph==='fruit' && s.variant===0){ for(let i=0;i<6;i++){ const zz=0.4+rnd()*(h-0.7);
      beam(out,[(rnd()*2-1)*L*0.4, 0.08, zz],[(rnd()*2-1)*L*0.4, 0.09, zz-0.12],0.02,'leafDark',0.2,4); } }
    weeds(out, -L/2,L/2, -0.14,0.14, un*0.7, 1141, 'weed');
  }
  function pFernBed(out,s,ctx){
    const un=1-s.kept, Fs=folState(s,false), rnd=mulberry32(1117), R=0.42+s.len*0.2;
    const n = Fs.full<0.25 ? 4 : (9+Math.round(s.len*6));
    for(let i=0;i<n;i++){
      const a=(i/n)*6.28+rnd()*0.55, rr=R*(0.5+rnd()*0.6);
      const bx=Math.cos(a)*R*0.12, by=Math.sin(a)*R*0.1;
      const tx=Math.cos(a)*rr, ty=Math.sin(a)*rr*0.8;
      const tz=(0.44+rnd()*0.28)*(0.4+Fs.full*0.6), apex=tz*1.3;
      beam(out,[bx,by,0.02],[(bx+tx)*0.5,(by+ty)*0.5,apex],0.02,'stalk',0.25,4);
      beam(out,[(bx+tx)*0.5,(by+ty)*0.5,apex],[tx,ty,tz*0.55],0.016,'stalk',0.2,4);
      // leaflets: flattened clumps down the frond, so it arches instead of standing as a stick
      if(Fs.full>0.2) for(let m=0;m<5;m++){ const t=(m+0.55)/5.2;
        const px=bx+(tx-bx)*t, py=by+(ty-by)*t;
        const pz=(t<0.5 ? 0.03+(apex-0.03)*(t/0.5)*0.94 : apex-(apex-tz*0.55)*((t-0.5)/0.5));
        const r2=0.1*(1.05-t*0.4);
        clump(out, px,py,pz, r2, r2*0.72, r2*0.5, 'leaf','leafDark', -0.02, 701+i*7+m, 0.26, m>2); }
    }
    if(s.kept>0.5) for(let i=0;i<11;i++){ const a=i/11*6.28;      // a mulch ring, not a slab
      prismT(out, Math.cos(a)*R*0.97, Math.sin(a)*R*0.8, 0.055, 0, 0.05, 5, 'mulch', -0.2, soilTex()); }
    weeds(out, -R,R, -R*0.85,R*0.85, un*0.7, 1121, 'weed');
  }
  function pHostaEdge(out,s,ctx){
    const L=runLen(s,1.2,1.0), un=1-s.kept, Fs=folState(s,false);
    const n=Math.max(2, Math.round(L/0.44));
    for(let i=0;i<n;i++){
      const x=-L/2+(i+0.5)*L/n, y=(hash2(i,5)-0.5)*0.11;
      foliageMass(out, x,y, 0.5, 0.44, 0.27+Fs.full*0.13, { cell:0.085, rough:0.1+un*0.26,
        full:Fs.full, seed:59+i*3, plan:2, taper:0.4, mat:'leaf', matDark:'leafDark', snow:s.snowAmt });
      if(Fs.full>0.5 && (Fs.ph==='bloom'||Fs.ph==='leaf')){         // one scape, not a picket
        const zz=0.3+Fs.full*0.1;
        beam(out,[x,y,zz*0.7],[x+0.015,y,zz+0.24],0.013,'stalk',0.2,4);
        for(let k=0;k<3;k++) prismT(out, x+0.015, y, 0.021, zz+0.12+k*0.07, zz+0.16+k*0.07, 5,
          s.variant===1?'bloomA':'bloomC', 0.7); }
    }
    weeds(out, -L/2,L/2, -0.28,0.28, un*0.8, 1129, 'weed');
  }
  function pRaspberryRow(out,s,ctx){
    const L=runLen(s,1.4,1.2), un=1-s.kept, Fs=folState(s,false), rnd=mulberry32(1131);
    const h=1.15+s.len*0.25;
    for(const px of [-L/2,0,L/2]) post(out, px,0, 0, h, 0.038, 'pole', 0, un*0.1, rnd()*6.28, false, grainTex(0.3));
    for(const z of [h*0.45, h*0.85]) beam(out,[-L/2,0,z],[L/2,0,z],0.012,'galv',0.45,3);
    const n=Math.max(5, Math.round(L/0.2));
    for(let i=0;i<n;i++){
      const x=-L/2+(i+0.5)*L/n, lean=(rnd()-0.5)*0.6, tz=h*(0.7+rnd()*0.36);
      beam(out,[x,(rnd()-0.5)*0.1,0],[x+lean*0.24,(rnd()-0.5)*0.18,tz],0.014,'stalk',-0.15,4);
      if(Fs.full>0.2) for(let m=0;m<5;m++){ const t=(m+0.6)/5.2, r2=0.1-t*0.02;
        clump(out, x+lean*0.24*t+(hash2(i,m+21)-0.5)*0.1, (hash2(i,m)-0.5)*0.24, 0.14+tz*t*0.86,
          r2, r2*0.85, r2*0.8, 'leaf','leafDark', 0, 801+i*3+m, 0.26, m%2>0); }
      if(Fs.ph==='fruit'||Fs.ph==='turn') for(let m=0;m<2;m++){ const zz=tz*(0.45+m*0.26);
        prismT(out, x+(hash2(i,m+9)-0.5)*0.16, (hash2(i,m+3)-0.5)*0.18, 0.027, zz, zz+0.05, 5, 'bloomB', 0.8); }
      if(Fs.ph==='bloom') prismT(out, x, (hash2(i,7)-0.5)*0.14, 0.022, tz*0.7, tz*0.7+0.04, 5, 'bloomC', 0.8);
    }
    weeds(out, -L/2,L/2, -0.3,0.3, un*0.9, 1133, 'weed', true);
  }
  function pHangingBasket(out,s,ctx){
    const Fs=folState(s,false), un=1-s.kept, rnd=mulberry32(1151);
    const H0=1.5+s.len*0.25, bx=0.26, bz=H0-0.34;
    post(out, 0,0, 0, H0, 0.026, 'iron', 0.05, un*0.08, 1.1, false);
    const arc=[[0,0,H0-0.02],[0.08,0,H0+0.09],[0.2,0,H0+0.1],[bx,0,H0-0.03]];
    for(let i=0;i<arc.length-1;i++) beam(out,arc[i],arc[i+1],0.02,'iron',0.3,5);
    for(const a of [0.3,2.4,4.5]) beam(out,[bx,0,H0-0.05],[bx+Math.cos(a)*0.13, Math.sin(a)*0.11, bz+0.16],0.009,'galv',0.45,3);
    tube(out,[bx,0,bz],[bx,0,bz+0.09], 0.15, 10, 'basket', -0.05, null, false);
    tube(out,[bx,0,bz+0.09],[bx,0,bz+0.21], 0.19, 10, 'basket', 0.08, null, false);
    capDisc(out, bx,0, bz+0.19, 0.18, 10, 'soil', -0.4, soilTex());
    if(s.planted){
      foliageMass(out, bx,0, 0.42,0.36, 0.24, { z0:bz+0.16, cell:0.08, rough:0.3, full:Fs.full,
        seed:83, taper:0.3, mat:'leaf', matDark:'leafDark' });
      if(Fs.full>0.35){
        for(let i=0;i<7;i++){ const a=i/7*6.28+0.4;
          beam(out,[bx+Math.cos(a)*0.12, Math.sin(a)*0.1, bz+0.14],
                   [bx+Math.cos(a)*0.19, Math.sin(a)*0.16, bz-0.2-rnd()*0.18],0.026,'leaf',0.1,4); }
        for(let i=0;i<6;i++){ const a=rnd()*6.28, rr=0.05+rnd()*0.1, zz=bz+0.21+rnd()*0.08;
          prismT(out, bx+Math.cos(a)*rr, Math.sin(a)*rr*0.85, 0.031, zz, zz+0.05, 5,
            i%2?'bloomA':'bloomC', 0.72); } }
    }
    weeds(out, -0.14,0.14, -0.12,0.12, un*0.6, 1153, 'weed');
  }
  function pGroundCover(out,s,ctx){
    const L=runLen(s,1.0,1.0), d=0.85, un=1-s.kept, Fs=folState(s,false), rnd=mulberry32(1157);
    const mossy=s.variant===1;
    box(out, -L/2,L/2, -d/2,d/2, 0,0.03, mossy?'soil':'mulch', -0.35, soilTex(), false);
    // a mat, not a sprinkle: two courses of low clumps, the under-course dark, so the ground reads
    // as covered rather than as bare mulch with weeds in it
    const dens=0.45+Fs.full*0.55, n=Math.round(L*d*58*dens);
    for(let i=0;i<n;i++){ const x=(rnd()*2-1)*L*0.48, y=(rnd()*2-1)*d*0.46, r=0.062+rnd()*0.03;
      clump(out, x,y, 0.02, r, r*0.9, mossy?0.03:0.042, 'leafDark','leafDark', -0.3, 1001+i*3, 0.14, true); }
    for(let i=0;i<n;i++){ const x=(rnd()*2-1)*L*0.47, y=(rnd()*2-1)*d*0.45, r=0.058+rnd()*0.03;
      clump(out, x,y, 0.045+rnd()*0.03, r, r*0.88, mossy?0.032:0.05, 'leaf','leafDark',
        mossy?-0.1:0.02, 1301+i*3, mossy?0.12:0.3, true); }
    if(!mossy && (Fs.ph==='bloom'||Fs.ph==='leaf')) for(let i=0;i<Math.round(n*0.16);i++)
      prismT(out, (rnd()*2-1)*L*0.44, (rnd()*2-1)*d*0.42, 0.022, 0.09, 0.13, 5, 'bloomA', 0.75);
    weeds(out, -L/2,L/2, -d/2,d/2, un*0.5, 1159, 'weed');
  }

  const PROPS = {
    picketPanel:  { label:'Picket fence',   cat:'fence', foot:[1.7,0.1], run:T, k:0.94, variants:['low','tall'], def:{paint:'white'}, build:pPicket, h:(s)=>(s.variant===1?1.3:1.06) },
    picketGate:   { label:'Picket gate',    cat:'fence', foot:[1.3,0.3], variants:['shut','open'], def:{paint:'white'}, build:pPicketGate, h:()=>1.3 },
    postRail:     { label:'Post & rail',    cat:'fence', foot:[2.0,0.14], run:T, k:0.9, variants:['three rail'], def:{paint:'white'}, build:pPostRail, h:()=>1.14 },
    splitRail:    { label:'Split rail',     cat:'fence', foot:[2.2,0.6], run:T, k:0.82, variants:['zigzag'], def:{}, build:pSplitRail, h:()=>1.05 },
    wireFence:    { label:'Page wire',      cat:'fence', foot:[2.2,0.1], run:T, k:0.91, variants:['farm wire'], def:{}, build:pWireFence, h:()=>1.16 },
    snowFence:    { label:'Snow fence',     cat:'fence', foot:[2.0,0.2], run:T, k:0.8, variants:['slat'], def:{paint:'rustOrange'}, build:pSnowFence, h:()=>1.04 },
    lattice:      { label:'Lattice skirt',  cat:'fence', foot:[1.5,0.12], run:T, k:0.73, variants:['diamond'], def:{paint:'white'}, build:pLattice, h:()=>0.82 },
    stoneWall:    { label:'Fieldstone wall',cat:'fence', foot:[1.8,0.46], run:T, k:0.89, variants:['low','high'], def:{}, build:pStoneWall, h:(s)=>(s.variant===1?0.96:0.72) },
    stonePillar:  { label:'Gate pillar',    cat:'fence', foot:[0.64,0.64], variants:['capped','finial'], def:{}, build:pStonePillar, h:(s)=>s.variant===1?1.46:1.31 },
    fenceCorner:  { label:'Fence corner',   cat:'fence', foot:[0.7,0.7], variants:['picket'], def:{paint:'white'}, build:pFenceCorner, h:()=>1.12 },

    hedgeRun:     { label:'Clipped hedge',  cat:'hedge', foot:[1.6,0.62], run:T, k:1.19, variants:['low','tall'], def:{}, build:pHedgeRun, h:(s)=>(s.variant===1?1.35:0.85) },
    hedgeBall:    { label:'Clipped ball',   cat:'hedge', foot:[0.9,0.86], variants:['deciduous','cedar'], def:{}, build:pHedgeBall, h:(s)=>1.9*(0.42+s.len*0.22)/0.42*0.42 },
    cedarHedge:   { label:'Cedar hedge',    cat:'hedge', foot:[1.5,0.66], run:T, k:1.13, variants:['screen'], def:{}, build:pCedarHedge, h:(s)=>1.8+s.len*0.5 },
    lilac:        { label:'Dooryard lilac', cat:'hedge', foot:[1.3,1.1], variants:['bush'], def:{}, build:pLilac, h:(s)=>1.9+s.len*0.6 },
    roseBush:     { label:'Rugosa rose',    cat:'hedge', foot:[1.1,1.0], variants:['mound'], def:{}, build:pRoseBush, h:(s)=>0.78+s.len*0.25 },
    hedgeCorner:  { label:'Hedge corner',   cat:'hedge', foot:[0.95,0.95], run:T, k:0.47, variants:['low','tall'], def:{}, build:pHedgeCorner, h:(s)=>(s.variant===1?1.32:0.88) },
    hydrangea:    { label:'Hydrangea',      cat:'hedge', foot:[1.15,1.0], variants:['blue','white'], def:{}, build:pHydrangea, h:(s)=>0.85+s.len*0.3 },
    trellisVine:  { label:'Trellis & vine', cat:'hedge', foot:[1.0,0.16], run:T, k:0.6, variants:['sweet pea','clematis'], def:{paint:'white'}, build:pTrellisVine, h:(s)=>1.7+s.len*0.3 },

    foundationBed:{ label:'Foundation bed', cat:'bed', foot:[1.6,0.62], run:T, k:0.87, variants:['shell edge','stone edge'], def:{planted:T}, build:pFoundationBed, h:()=>0.5 },
    islandBed:    { label:'Island bed',     cat:'bed', foot:[1.1,0.8], run:T, k:0.82, variants:['oval'], def:{planted:T}, build:pIslandBed, h:()=>0.6 },
    borderBed:    { label:'Border bed',     cat:'bed', foot:[1.8,0.45], run:T, k:0.89, variants:['timber edge'], def:{planted:T}, build:pBorderBed, h:()=>0.45 },
    vegRows:      { label:'Vegetable rows', cat:'bed', foot:[1.5,1.1], run:T, k:0.87, variants:['open','bean poles'], def:{}, build:pVegRows, h:(s)=>s.variant===1?1.05:0.34 },
    potatoDrills: { label:'Potato drills',  cat:'bed', foot:[1.7,1.2], run:T, k:0.88, variants:['hilled'], def:{}, build:pPotatoDrills, h:()=>0.42 },
    rhubarb:      { label:'Rhubarb patch',  cat:'bed', foot:[0.9,0.8], variants:['clump'], def:{}, build:pRhubarb, h:()=>0.42 },
    fernBed:      { label:'Fern clump',     cat:'bed', foot:[0.9,0.8], run:T, k:0.42, variants:['shade'], def:{}, build:pFernBed, h:()=>0.6 },
    hostaEdge:    { label:'Hosta edging',   cat:'bed', foot:[1.2,0.5], run:T, k:0.83, variants:['white spike','pink spike'], def:{}, build:pHostaEdge, h:()=>0.7 },
    raspberryRow: { label:'Raspberry canes',cat:'bed', foot:[1.4,0.6], run:T, k:0.86, variants:['wired'], def:{}, build:pRaspberryRow, h:(s)=>1.15+s.len*0.25 },

    barrelPlanter:{ label:'Half barrel',    cat:'planter', foot:[0.62,0.62], variants:['oak'], def:{planted:T}, build:pBarrelPlanter, h:()=>0.72 },
    windowBox:    { label:'Window box',     cat:'planter', foot:[0.9,0.3], run:T, k:0.78, variants:['painted'], def:{paint:'white',planted:T}, build:pWindowBox, h:()=>0.5 },
    potCluster:   { label:'Clay pots',      cat:'planter', foot:[0.7,0.5], variants:['three'], def:{planted:T}, build:pPotCluster, h:()=>0.45 },
    tirePlanter:  { label:'Tyre planter',   cat:'planter', foot:[0.74,0.7], variants:['black','whitewashed'], def:{planted:T,paint:'white'}, build:pTirePlanter, h:()=>0.4 },
    hangingBasket:{ label:'Hanging basket', cat:'planter', foot:[0.52,0.4], variants:['crook'], def:{planted:T}, build:pHangingBasket, h:(s)=>1.62+s.len*0.25 },

    mailbox:      { label:'Mailbox',        cat:'fixture', foot:[0.3,0.44], variants:['flag down','flag up'], def:{}, build:pMailbox, h:()=>1.35 },
    clothesline:  { label:'Clothesline',    cat:'fixture', foot:[2.4,0.55], run:T, k:0.67, variants:['hung','empty'], def:{paint:'white'}, build:pClothesline, h:()=>1.95 },
    woodpile:     { label:'Woodpile',       cat:'fixture', foot:[1.1,0.5], run:T, k:1.0, variants:['open','tarped'], def:{}, build:pWoodpile, h:(s)=>(4+Math.round(s.len*3))*0.2 },
    rainBarrel:   { label:'Rain barrel',    cat:'fixture', foot:[0.66,0.7], variants:['oak','painted'], def:{paint:'sage'}, build:pRainBarrel, h:()=>0.98 },
    hoseReel:     { label:'Hose & tap',     cat:'fixture', foot:[0.6,0.6], variants:['grass','gravel pad'], def:{}, build:pHoseReel, h:()=>0.7 },
    compostBin:   { label:'Compost bin',    cat:'fixture', foot:[0.9,0.8], variants:['slat'], def:{}, build:pCompostBin, h:()=>0.86 },
    oilTank:      { label:'Oil tank',       cat:'fixture', foot:[1.1,0.7], variants:['on legs'], def:{paint:'cream'}, build:pOilTank, h:()=>1.1 },

    bench:        { label:'Garden bench',   cat:'sitting', foot:[1.5,0.45], run:T, k:0.33, variants:['slat back'], def:{paint:'white'}, build:pBench, h:()=>0.86 },
    picnicTable:  { label:'Picnic table',   cat:'sitting', foot:[1.85,1.6], variants:['A-frame'], def:{}, build:pPicnicTable, h:()=>0.78 },
    adirondack:   { label:'Muskoka chairs', cat:'sitting', foot:[1.6,0.9], variants:['pair','single'], def:{paint:'teal'}, build:pAdirondack, h:()=>0.9 },
    firePit:      { label:'Fire pit',       cat:'sitting', foot:[1.9,1.7], variants:['laid','cold'], def:{}, build:pFirePit, h:()=>0.4 },

    swingSet:     { label:'Swing set',      cat:'play', foot:[2.1,0.7], variants:['two seats','tyre'], def:{}, build:pSwingSet, h:()=>1.95 },
    sandbox:      { label:'Sandbox',        cat:'play', foot:[1.15,1.0], variants:['open'], def:{}, build:pSandbox, h:()=>0.3 },
    doghouse:     { label:'Doghouse',       cat:'play', foot:[0.84,1.0], variants:['board roof','shingle'], def:{paint:'red'}, build:pDoghouse, h:()=>0.94 },
    sawhorses:    { label:'Sawhorses',      cat:'play', foot:[1.5,0.66], variants:['with plank','bare'], def:{}, build:pSawhorses, h:()=>0.73 },
    toolLean:     { label:'Tools & post',   cat:'play', foot:[0.9,0.8], variants:['leaning'], def:{}, build:pToolLean, h:()=>1.35 },

    buoyPost:     { label:'Buoy post',      cat:'ornament', foot:[0.5,0.4], variants:['plain','cross arm'], def:{}, build:pBuoyPost, h:(s)=>1.6+s.len*0.5 },
    doryPlanter:  { label:'Dory planter',   cat:'ornament', foot:[1.7,0.7], run:T, k:0.3, variants:['painted'], def:{paint:'white',planted:T}, build:pDoryPlanter, h:()=>0.7 },
    anchorSet:    { label:'Anchor & coil',  cat:'ornament', foot:[0.9,0.7], variants:['fisherman'], def:{}, build:pAnchorSet, h:(s)=>1.05*(0.9+s.len*0.4) },
    trapBench:    { label:'Trap bench',     cat:'ornament', foot:[1.4,0.5], variants:['plain','with buoys'], def:{}, build:pTrapBench, h:()=>0.5 },
    birdbath:     { label:'Birdbath',       cat:'ornament', foot:[0.68,0.68], variants:['water','iced'], def:{}, build:pBirdbath, h:()=>0.82 },
    birdhouse:    { label:'Birdhouse pole', cat:'ornament', foot:[0.3,0.3], variants:['single'], def:{paint:'white'}, build:pBirdhouse, h:(s)=>2.56+s.len*0.5 },

    steppingStones:{label:'Stepping stones',cat:'ground', foot:[1.2,0.5], run:T, k:1.17, variants:['flagstone','cast'], def:{}, build:pSteppingStones, h:()=>0.06 },
    gravelApron:  { label:'Gravel apron',   cat:'ground', foot:[1.6,1.3], run:T, k:1.12, variants:['gravel','crushed shell'], def:{}, build:pGravelApron, h:()=>0.08 },
    lawnEdge:     { label:'Mown edge',      cat:'ground', foot:[1.4,1.1], run:T, k:1.0, variants:['cut edge'], def:{}, build:pLawnEdge, h:()=>0.2 },
    groundCover:  { label:'Creeping cover', cat:'ground', foot:[1.0,0.85], run:T, k:1.0, variants:['thyme','moss'], def:{}, build:pGroundCover, h:()=>0.14 },

    flagpole:     { label:'Flagpole',       cat:'sign', foot:[0.4,0.4], run:T, k:0.1, variants:['flying'], def:{}, build:pFlagpole, h:(s)=>5.2+s.len*1.8 },
    nameSign:     { label:'Name board',     cat:'sign', foot:[0.9,0.2], variants:['plain','buoy'], def:{paint:'white'}, build:pNameSign, h:()=>1.52 },
    numberPost:   { label:'Number post',    cat:'sign', foot:[0.3,0.2], variants:['post'], def:{paint:'white'}, build:pNumberPost, h:()=>1.06 },
    farmStand:    { label:'Roadside stand', cat:'sign', foot:[1.25,0.8], variants:['stocked','empty'], def:{paint:'white'}, build:pFarmStand, h:()=>1.46 },
  };

  // ---- spec resolution -------------------------------------------------------
  function resolve(name, opts){ opts=opts||{}; const P=PROPS[name]||PROPS.picketPanel;
    const g=(k,d)=> opts[k]!=null?opts[k] : (P.def[k]!=null?P.def[k]:d);
    const phase = PHASE_KEYS.indexOf(opts.phase)>=0 ? opts.phase : 'green';
    return { name, paint: opts.paint!==undefined?opts.paint:(P.def.paint||null),
      variant: opts.variant!=null?opts.variant:0,
      len: opts.len!=null?opts.len:(P.run?0.4:0),
      kept: opts.kept!=null?Math.max(0,Math.min(1,opts.kept)):0.62,
      phase, snowAmt: opts.snow!=null?Math.max(0,Math.min(1,opts.snow)):SNOW_BY_PHASE[phase],
      planted: g('planted',false)?true:false,
      weather: opts.weather!=null?opts.weather:0.4, night:!!opts.night, seed:opts.seed||0 }; }

  function makeMats(s){ const wx=s.weather, night=s.night, un=1-s.kept;
    const grime=r=>r.map(c=>mix(desat(c,wx*0.22),'#3a3128',wx*0.12));
    const silver=r=>r.map(c=>mix(desat(c,wx*0.34+un*0.22),'#8d8a80',wx*0.14+un*0.1));
    const chalk =r=>r.map(c=>mix(desat(c,un*0.42),'#a89e8c',un*0.22));      // paint gone chalky
    const cool  =r=>night?r.map(c=>mix(desat(c,0.28),'#1b2740',0.4)):r;
    const t=r=>cool(grime(r)), tw=r=>cool(silver(grime(r))), tp=r=>cool(chalk(grime(r)));
    const Fs=folState(s,false);
    const folRamp = FOL[Fs.col]||FOL.green;
    const dry = TURF_BY_PHASE[Fs.ph]||0;
    const turf = TURF.map(c=>mix(c, '#8a7c52', dry*0.55));
    const weedRamp = FOL[Fs.col==='winter'?'winter':(Fs.col==='turn'?'turn':'green')].map(c=>mix(c,'#7e7248',dry*0.5+un*0.12));
    const paint = s.paint?(BODY[s.paint]||BODY.white):BODY.white;
    const M = {
      body:{ramp:tp(paint)}, trimw:{ramp:tp(BODY.white)}, wood:{ramp:tw(WOOD)}, plank:{ramp:tw(PLANK)},
      pole:{ramp:tw(POLE)}, firewood:{ramp:tw(WOOD)}, iron:{ramp:t(IRON)}, galv:{ramp:t(GALV)},
      alum:{ramp:t(GALV)}, tin:{ramp:t(TIN)}, brassy:{ramp:t(['#59410f','#795819','#977326','#b28e3b','#c8a757','#dcc17d'])},
      concrete:{ramp:t(CONCRETE)}, stone:{ramp:t(STONE)}, flag:{ramp:t(SLATEF)}, brick:{ramp:t(BRICK)},
      clay:{ramp:t(CLAY)}, gravel:{ramp:t(GRAVEL)}, shell:{ramp:t(SHELL)}, sand:{ramp:t(SHELL.map(c=>mix(c,'#c8a86a',0.35)))},
      soil:{ramp:t(SOIL)}, mulch:{ramp:t(MULCH)}, compost:{ramp:t(SOIL.map(c=>mix(c,'#3f3a1e',0.3)))},
      turf:{ramp:t(turf)}, weed:{ramp:t(weedRamp)}, stalk:{ramp:t(['#5c2320','#74332a','#8c4736','#a35d45','#b87759','#cc9273'])},
      leaf:{ramp:t(folRamp)}, leafDark:{ramp:t(folRamp.map(c=>mix(c,'#101a14',0.34)))},
      ever:{ramp:t(FOL.ever)}, everDark:{ramp:t(FOL.ever.map(c=>mix(c,'#0d1512',0.34)))},
      twig:{ramp:tw(FOL.winter)},
      bloomA:{ramp:t(rampFrom(BLOOM[s.variant%2?'rose':'lilac']))},
      bloomB:{ramp:t(rampFrom(BLOOM.hip))}, bloomC:{ramp:t(rampFrom(BLOOM.white))},
      bloomD:{ramp:t(rampFrom(BLOOM.blue))}, bloomE:{ramp:t(rampFrom(BLOOM.gold))},
      rope:{ramp:tw(ROPE)}, canvas:{ramp:tw(CANVAS)}, rubber:{ramp:t(RUBBER)},
      hose:{ramp:t(['#123528','#1b4a35','#256045','#317a58','#40946c','#54ae83'])}, linen:{ramp:tp(LINEN)},
      snow:{ramp:cool(SNOWR)}, snowDark:{ramp:cool(SNOWR.map(c=>mix(c,'#3d5566',0.34)))}, ice:{ramp:cool(['#5b7078','#79949c','#96b3ba','#b3ccd1','#cfe2e5','#e6f2f4'])},
      water:{ramp:cool(['#28454f','#356070','#437c8e','#5798a8','#71b2bf','#93cbd4'])},
      slate:{ramp:t(['#181d1e','#222829','#2d3436','#3a4244','#48524f','#5a635f'])},
      ash:{ramp:t(['#232323','#333230','#43423e','#55534d','#68655d','#7d796f'])},
      trapnet:{ramp:t(['#1d2a1e','#2a3a29','#374b34','#465e41','#57724f','#6a875f'])},
      basket:{ramp:tw(ROPE)}, produce:{ramp:t(rampFrom('#b8622c'))},
      shingle:{ramp:tw(BODY.greyShingle)}, doorhole:{ramp:['#0e1114','#151a1e','#1d2429','#252d33','#2e373e','#38434b']},
      flagred:{ramp:t(BODY.red)}, flagwhite:{ramp:tp(BODY.white)},
      glass:{ramp: night?GLASSN:GLASSD}, glow:{ramp: night?GLOW:['#5f6a5e','#8d9a8b','#b6c2b0','#d3ddcb']},
    };
    ['buoyA','buoyB','buoyC','buoyD'].forEach((k,i)=>{ M[k]={ramp:t(BODY[['red','white','gold','teal'][i]])}; });
    Object.keys(BODY).forEach(k=>{ M[k]={ramp:t(BODY[k])}; });
    return M;
  }

  // ---- rasteriser (wharfDecor recipe, verbatim) ----
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
  function post2(bufs, s){ const { rbuf, ibuf, nbuf, dep }=bufs, N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx,ny=y+dy; if(nx>=W||ny>=H) continue; const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j; out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; } } }
    const un=1-s.kept;
    if(s.weather>0.02||un>0.3){ const rnd=mulberry32(3181|((s.variant*97)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='pole'||m==='plank'||m==='body'||m==='trimw'||m==='concrete'||m==='stone'||m==='tin'||m==='canvas') && rnd()<s.weather*0.05+un*0.05)
          out[i]=rbuf[i][Math.max(0,ibuf[i]-1)]; } }
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

  // ---- plant compositing: the bed asks the flower / shrub rigs for real sprites -------------
  function plantsAvailable(){ return !!(root.Flowers || root.Shrubs); }
  function renderPlant(p, s){
    try {
      if(p.kind==='shrub' && root.Shrubs){
        const r=root.Shrubs.render(p.key, { phase:s.phase, size:(p.o.size!=null?p.o.size:0.3), variant:p.o.variant||0,
          snow:s.snowAmt*0.6 });
        return { w:r.w, h:r.h, rgba:r.rgba, px:r.pivot.x, py:r.pivot.y };
      }
      if(root.Flowers){
        const tier=p.o.tier||'single';
        const stage = (s.phase==='catkin') ? 0 : (s.phase==='turn'||s.phase==='bare'||s.phase==='dormant') ? 2 : 1;
        const key = root.Flowers.byKey[p.key] ? p.key : (root.Flowers.SPECIES[0]||{}).key;
        if(!key) return null;
        const r = tier==='patch' ? root.Flowers.renderPatch(key, p.o.variant||0, 0)
                : tier==='clump' ? root.Flowers.renderClump(key, 0)
                : root.Flowers.renderSingle(key, stage, 0);
        return { w:r.w, h:r.h, rgba:r.rgba, px:Math.floor(r.w/2), py: tier==='patch'?Math.floor(r.h/2):r.h-1 };
      }
    } catch(e){ /* a plant rig mid-edit must never take the yard down */ }
    return null;
  }
  function blitPlants(cols, plants, B, s){
    const items=[];
    for(const p of plants){ const v=projVert(p.x,p.y,p.z,B); items.push({ p, v }); }
    items.sort((a,b)=> b.v.d - a.v.d);                       // far first
    for(const it of items){
      const spr=renderPlant(it.p, s); if(!spr) continue;
      const ox=Math.round(it.v.sx - spr.px), oy=Math.round(it.v.sy - spr.py);
      for(let y=0;y<spr.h;y++){ const ty=oy+y; if(ty<0||ty>=H) continue;
        for(let x=0;x<spr.w;x++){ const tx=ox+x; if(tx<0||tx>=W) continue;
          const a=spr.rgba[(y*spr.w+x)*4+3]; if(a<128) continue;
          cols[ty*W+tx]=rgb2hex(spr.rgba[(y*spr.w+x)*4], spr.rgba[(y*spr.w+x)*4+1], spr.rgba[(y*spr.w+x)*4+2]); } }
    }
  }

  // ---- public ----------------------------------------------------------------
  function render(name, dir, opts){ opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const s=resolve(name,opts), B=camBasis({dir,elev:opts.elev}), MATS=makeMats(s), out=[];
    const ctx={ plants:[], external: opts.compose===false?false:plantsAvailable() };
    (PROPS[name]||PROPS.picketPanel).build(out, s, ctx);
    const cols = post2(paintFaces(out,B,MATS), s);
    if(ctx.external && ctx.plants.length) blitPlants(cols, ctx.plants, B, s);
    return toRGBA(cols);
  }
  function footprint(name, opts){ const s=resolve(name,opts), P=PROPS[name]||PROPS.picketPanel;
    const k = P.run ? (1 + s.len*(P.k!=null?P.k:0.2)) : 1;
    return { w:P.foot[0]*k, d:P.foot[1] }; }
  function height(name, opts){ const s=resolve(name,opts), P=PROPS[name]||PROPS.picketPanel;
    return P.h?P.h(s):1; }
  function mounts(name, opts){ const s=resolve(name,opts), P=PROPS[name]||PROPS.picketPanel;
    const f=footprint(name,opts), M=P.mounts?P.mounts(s):null;
    return { ground:(M&&M.ground)||[[0,0,0]], ends:(M&&M.ends)||(P.run?[[-f.w/2,0,0],[f.w/2,0,0]]:[]),
      wall:(M&&M.wall)||[], foot:f }; }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }
  function anchors(name, dir, opts){ opts=opts||{}; const M=mounts(name,opts), e=opts.elev;
    const P=(a)=>a.map(p=>{ const q=project(dir,p,e); return { x:q.x, y:q.y, m:p }; });
    return { ground:P(M.ground), ends:P(M.ends), wall:P(M.wall), foot:M.foot, height:height(name,opts) }; }
  function list(){ return Object.keys(PROPS); }
  function byCat(){ const o={}; CATS.forEach(c=>o[c]=[]); Object.keys(PROPS).forEach(k=>{ o[PROPS[k].cat].push(k); }); return o; }

  root.YardIso = { W, H, PX, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    BODY, WOOD, PLANK, POLE, IRON, GALV, TIN, CONCRETE, STONE, SLATEF, CLAY, GRAVEL, SHELL, SOIL, MULCH,
    TURF, ROPE, CANVAS, SNOWR, FOL, BLOOM, KEY,
    PHASES, PHASE_KEYS, LEAFING, SNOW_BY_PHASE, PROPS, CATS, CAT_LABEL,
    list, byCat, footprint, height, mounts, anchors, render, project, resolve, plantsAvailable };
})(typeof globalThis!=='undefined'?globalThis:window);
