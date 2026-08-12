/* Hidden Harbours — parametric ISO SHOP INTERIOR rig (ADR-0006 bake pipeline, SAME turntable +
   camera + shading as interiorIsoRig.js / shopfrontRig.js / houseIsoRig.js / the fleet). One
   parametric 3D commercial ROOM plus a placeable FIXTURE CATALOG, baked to pixel sheets through the
   SHARED 3/4 camera: 45deg steps, elev 40deg default, flat-facet shading from the fixed upper-LEFT
   key, z-buffered, ordered dither, per-face uv texture, depth-edge darkening, 1px keyline, NO AA.
   32 px = 1 m. All 8 facings fall out of one model.

   THE SHOP IS THE SHOPFRONT SEEN FROM INSIDE. Footprint, wall height, floor height and roofline are
   read from shopfrontRig.js's TYPES by the SAME formula, so a 'bakery' room registers under a
   'bakery' shell. OPEN-DOLLHOUSE cutaway: the two walls whose outward face points at the camera are
   dropped. The +Y wall is the street elevation from the inside — its storefront glazing, bulkhead and
   shop door are the very ones the exterior rig builds.

   BUILT-INS ARE BAKED, LOOSE PROPS ARE SPRITES. Every fixture is geometry in the same 3D pass (so
   shading, z-order, keyline and occlusion are exact), but each one also lands on its OWN layer, and
   renderItem(name,dir,opts) bakes any single fixture as a standalone sprite — so the loose props
   (tables, chairs, crates, barrels) can ship as individual sheets while the built-ins (counter,
   shelving, bar, cases) stay part of the room.

   PLACEMENT IS MANUAL. grid(opts) hands back the floor cell grid; items are [{k,gx,gy,rot}] in cell
   coordinates. defaultItems(opts) seeds a full, trade-correct layout to start from.

   THE BUILDER SURFACE (every axis resolved per render, no re-modelling):
     type:   'generalStore'|'fishMarket'|'chandlery'|'bakery'|'restaurant'|'tavern'|'postOffice'|
             'takeoutStand'|'giftShop'   — names the exterior it is inside; seeds every axis
     room:   per-type room (sales floor / bar room / bakehouse / sorting room / …) — reseeds the layout
     storey: 'ground'|'upper'  ('upper' = the flat above the shop: knee walls, sloped ceiling)
     shape:  'gable'|'shed'|'gambrel'|'falseFront'  pitch:0..2   size:0..1
     floor:  'plank'|'wideBoard'|'checker'|'stoneFlag'|'painted'|'sawdust'
     wall:   'plaster'|'wainscot'|'board'|'stud'|'brick'|'block'|'tile'|'panel'
     paper:  BODY key   wainscot:bool   trimTone: BODY key|null (painted joinery)
     windows: sash style   winDensity:0..1   storefront: matches the shell   dividers:0..2
     stock:  0..1 (bare fixtures → packed shelves)   counterTone: BODY key
     beams:bool   stove:bool   weather:0..1   night:bool   seed:int
     items:  [{k,gx,gy,rot}] | null (null = defaultItems for this type+room)
     shell:  bool (default true; false renders ONLY the placed fixtures, at their true positions and
             on the same pivot — how the engine gets a complete per-fixture sprite to y-sort and ghost)
   ANIM: rooms are static; lamplight, the stove fire and heat-lamp glow are runtime overlays —
     anchors(dir,opts) -> { floor, door, till, queue, browse:[], stools:[], pass, stove, lamps:[],
     occluders:[], fixtures:[{k,x,y,rot,px,py}], Wd, Ln, fH, storeyZ, plate, ext } in cell px.
   Exposes globalThis.ShopInterior = { W,H,PX,DIRS,pivot,order,defaultElev, TYPES,ROOMS,FLOORS,WALLS,
     WINDOWS,SHAPES,STOREYS,FIXTURES,CATS,BODY,TRIM,PRESETS,KEY, dims,grid,defaultItems,fixtureFoot,
     list, render, renderLayers, renderItem, ghost, anchors, project }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1180, H = 900, cx = 590, groundY = 560;
  // The raster VIEW is mutable so the same rasterizer can bake a bigger, re-pivoted sheet.
  // shopBuildingRig.js bakes whole multi-room PLANS through it; every render*() here resets it
  // first, so the single-room sheet size is unchanged.
  let VW = W, VH = H, VCX = cx, VGY = groundY;
  function setView(w,h,ccx,cgy){ VW=w|0; VH=h|0; VCX=ccx; VGY=cgy; }
  function resetView(){ VW=W; VH=H; VCX=cx; VGY=groundY; }
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;

  // ---- palettes, dark -> light (KTC master ramps, shared with every other rig) ----
  const BODY = {
    greyShingle: ['#4c463f','#5d564c','#6f665a','#82786a','#968b7b','#a99d8c'],
    white:       ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    cream:       ['#8a6f3c','#a6884b','#c2a35f','#d8bd7c','#e9d59d','#f5e7c1'],
    red:         ['#4a130f','#671b14','#88271c','#a33124','#bd4230','#d25a42'],
    sage:        ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'],
    blue:        ['#33454a','#43585d','#556d72','#6a848a','#849ea3','#a3b9bd'],
    gold:        ['#5e4a12','#7c6119','#987a26','#b39440','#c8ab5e','#dbc182'],
    plum:        ['#2e2333','#3f3047','#523f5d','#664f73','#7d648b','#9079a1'],
    rustOrange:  ['#5c2a10','#78380f','#95491a','#b05c27','#c67338','#d98d4f'],
    mustard:     ['#5e4a12','#7c6119','#987a26','#b39440','#c8ab5e','#dbc182'],
    teal:        ['#143a38','#1f4d4a','#2c625e','#3b7872','#4d8f88','#66a69d'],
    galv:        ['#464d51','#5a6267','#727c81','#8c979c','#a6b1b5','#c2cccf'],
    rustMetal:   ['#3a1c10','#552a17','#6e3a22','#8a4e2f','#a5643f','#bd7d52'],
  };
  const TRIM     = ['#9aa09a','#b4b8b0','#ccd0c7','#e0e2da','#eef0e8','#f8f9f2'];
  const PLASTER  = ['#6f6350','#877a63','#a2947a','#bcae92','#d3c6a9','#e7dcc2'];
  const WOOD     = ['#4f3a24','#63492d','#785a39','#8f7049','#a6875d','#bd9f74'];
  const OAK      = ['#3c2a17','#4e381f','#63482a','#795a36','#8f6f45','#a68758'];
  const FLOORWOOD= ['#563820','#6c472a','#855a36','#9e7247','#b78c5e','#cea679'];
  const STONE    = ['#3a3d43','#494d54','#5b6067','#70757c','#868b91','#9ca1a6'];
  const BRICK    = ['#3a201a','#552b20','#6e3728','#874634','#a05743','#b96b55'];
  const TILE     = ['#20262b','#2b333a','#3a444c','#a9b0af','#c4cbc7','#dee3de'];
  const CAVITY   = ['#201811','#291f15','#33271a','#3e2f20','#493827','#54432f'];
  const DOORDK   = ['#100c0f','#171216','#20191e','#2a2127','#332830'];
  const CINDER   = ['#4a4842','#5f5d55','#77746a','#8f8b7f','#a4a094','#b8b3a6'];
  const STEEL    = ['#2a2f33','#3c454b','#525c63','#6d777e','#889298','#a2acb1'];
  const IRON     = ['#141619','#1e2226','#2b3035','#3a4046','#4a5158','#5b636a'];
  const BRASS    = ['#4a3410','#66491a','#856228','#a37e3c','#bd9a55','#d4b774'];
  const ZINC     = ['#4b545a','#5e696f','#747f86','#8c979d','#a3aeb3','#bcc6ca'];
  const MARBLE   = ['#5c5f63','#71757a','#888d92','#a2a7ac','#bec2c6','#dadde0'];
  const SLATE    = ['#2b2f34','#383d43','#484e55','#5b6169','#70767e','#868d94'];
  const CERAMIC  = ['#8e8f89','#a5a69f','#bcbdb5','#d1d2ca','#e3e4dc','#f2f3eb'];
  const CRATE    = ['#6a4f2c','#7e5f36','#937143','#a98653','#bd9b67','#cfb180'];
  const SACK     = ['#6e6249','#7f7255','#928463','#a69773','#b9aa85','#cbbd98'];
  const LEATHER  = ['#2c1c14','#3e281a','#523522','#66442c','#7a5537','#8e6845'];
  const CHALKBD  = ['#15181a','#1d2124','#272c30','#31373c','#3d4348','#4b5257'];
  const ICE      = ['#5c7580','#728e98','#8aa8b0','#a5c2c8','#c1dade','#dcefef'];
  const FISHA    = ['#3a4a52','#4c6069','#617781','#7a9099','#94a9b1','#b0c3c9'];
  const FISHB    = ['#5c3a3a','#74494a','#8c5a5c','#a26f71','#b78688','#c99fa1'];
  const GLASSD   = ['#7d9ea6','#a1c2c6','#cfe6e8'];
  const GLASSN   = ['#141d2b','#233247','#3d5570'];
  const GLASS_HI = '#dff0f1';
  const CASEG    = ['#93b0b6','#b2ccd0','#d8ecee'];
  const FIRE     = ['#7a2a10','#b5541a','#e59433','#f6cf6a'];
  const KEY      = '#1a1c22';

  const FLOORS  = ['plank','wideBoard','checker','stoneFlag','painted','sawdust'];
  const WALLS   = ['plaster','wainscot','board','stud','brick','block','tile','panel'];
  const WINDOWS = ['sixOverSix','twoOverTwo','oneOverOne','industrial'];
  const SHAPES  = ['gable','shed','gambrel','falseFront'];
  const STOREYS = ['ground','upper'];
  const WINSTYLES = {
    sixOverSix: { v:2, r:[0.25,0.5,0.75] },
    twoOverTwo: { v:1, r:[0.5] },
    oneOverOne: { v:0, r:[0.5] },
    industrial: { v:2, r:[0.2,0.4,0.6,0.8] },
  };

  // ---- the nine trades. Wd/Ln/wallH/fH ranges are IDENTICAL to shopfrontRig.js — one contract. ----
  const TYPES = {
    generalStore:{ label:'general store', ext:'generalStore', shape:'falseFront', pitch:0.62, size:0.35, flat:true,
      Wd:[7.0,2.6], Ln:[8.5,4.0], wallH:[3.9,1.2], fH:0.55, storefront:'bay',
      floor:'plank', wall:'wainscot', paper:'cream', trimTone:'white', wainscot:true, windows:'twoOverTwo', winD:0.50,
      counterTone:'red', goods:['red','blue','cream','gold'], wares:['box','tin','sack','bolt','jar'], beams:false, stove:true },
    fishMarket:{ label:'fish market', ext:'fishMarket', shape:'shed', pitch:0.50, size:0.40, flat:false,
      Wd:[6.5,2.4], Ln:[7.0,3.5], wallH:[3.5,1.0], fH:0.45, storefront:'hatch',
      floor:'painted', wall:'tile', paper:'white', trimTone:'white', wainscot:false, windows:'industrial', winD:0.45,
      counterTone:'galv', goods:['galv','white','blue','teal'], wares:['crateB','tin','box'], beams:true, stove:false },
    chandlery:{ label:'chandlery', ext:'chandlery', shape:'gable', pitch:0.95, size:0.45, flat:true,
      Wd:[6.5,2.4], Ln:[8.0,4.0], wallH:[3.8,1.2], fH:0.50, storefront:'smallPane',
      floor:'wideBoard', wall:'board', paper:'greyShingle', trimTone:null, wainscot:false, windows:'sixOverSix', winD:0.50,
      counterTone:'blue', goods:['galv','rustMetal','blue','cream'], wares:['coil','box','tin','curio'], beams:true, stove:true },
    bakery:{ label:'café / bakery', ext:'bakery', shape:'falseFront', pitch:0.60, size:0.30, flat:true,
      Wd:[6.0,2.2], Ln:[7.0,3.2], wallH:[3.6,1.0], fH:0.50, storefront:'plate',
      floor:'checker', wall:'tile', paper:'cream', trimTone:'white', wainscot:true, windows:'twoOverTwo', winD:0.50,
      counterTone:'sage', goods:['cream','gold','red','white'], wares:['loaf','tray','jar','box'], beams:false, stove:false },
    restaurant:{ label:'restaurant', ext:'restaurant', shape:'gable', pitch:0.85, size:0.50, flat:true,
      Wd:[7.5,2.8], Ln:[9.0,4.5], wallH:[4.0,1.2], fH:0.50, storefront:'plate',
      floor:'wideBoard', wall:'wainscot', paper:'sage', trimTone:'white', wainscot:true, windows:'twoOverTwo', winD:0.60,
      counterTone:'sage', goods:['cream','white','red','gold'], wares:['crock','jar','box','tray'], beams:true, stove:false },
    tavern:{ label:'tavern', ext:'tavern', shape:'gable', pitch:1.00, size:0.45, flat:true,
      Wd:[7.0,2.6], Ln:[8.5,4.0], wallH:[3.9,1.4], fH:0.50, storefront:'narrow',
      floor:'plank', wall:'panel', paper:'red', trimTone:null, wainscot:true, windows:'sixOverSix', winD:0.45,
      counterTone:'gold', goods:['gold','red','plum','cream'], wares:['bottle','mug','crock','box'], beams:true, stove:true },
    postOffice:{ label:'post office', ext:'postOffice', shape:'falseFront', pitch:0.60, size:0.35, flat:true,
      Wd:[6.5,2.4], Ln:[7.5,3.5], wallH:[3.9,1.0], fH:0.60, storefront:'narrow',
      floor:'stoneFlag', wall:'wainscot', paper:'blue', trimTone:'white', wainscot:true, windows:'sixOverSix', winD:0.50,
      counterTone:'blue', goods:['cream','white','red','blue'], wares:['parcel','box','ledger'], beams:false, stove:true },
    takeoutStand:{ label:'takeout stand', ext:'takeoutStand', shape:'shed', pitch:0.45, size:0.30, flat:false,
      Wd:[4.2,1.6], Ln:[4.0,2.0], wallH:[2.9,0.8], fH:0.35, storefront:'hatch',
      floor:'painted', wall:'board', paper:'mustard', trimTone:'white', wainscot:false, windows:'oneOverOne', winD:0.30,
      counterTone:'red', goods:['red','mustard','white','cream'], wares:['box','tin','bottle'], beams:true, stove:false },
    giftShop:{ label:'gift shop', ext:'giftShop', shape:'gable', pitch:0.90, size:0.30, flat:true,
      Wd:[5.5,2.0], Ln:[6.5,3.0], wallH:[3.6,1.0], fH:0.50, storefront:'bay',
      floor:'wideBoard', wall:'plaster', paper:'teal', trimTone:'white', wainscot:true, windows:'twoOverTwo', winD:0.55,
      counterTone:'teal', goods:['teal','cream','gold','plum'], wares:['curio','jar','box','bolt'], beams:false, stove:false },
  };

  // ---- rooms per trade. IDs are the SHARED room vocabulary: shopBuildingRig.js names the same rooms
  // in its plans, so the engine can ask either rig for 'chandlery/loft' and get the same room. -----
  const ROOMS = {
    generalStore:['salesFloor','stockroom','flat'],
    fishMarket:  ['counter','cutting','ice'],
    chandlery:   ['floor','stockroom','loft','flat'],
    bakery:      ['front','bakehouse','flat'],
    restaurant:  ['dining','bar','kitchen','stock','flat'],
    tavern:      ['barroom','snug','kitchen','flat'],
    postOffice:  ['lobby','sorting','flat'],
    takeoutStand:['servery'],
    giftShop:    ['floor','stockroom','flat'],
  };
  const ROOM_LABEL = {
    salesFloor:'sales floor', stockroom:'stock room', counter:'fish counter', cutting:'cutting room',
    ice:'ice house', stock:'stock room',
    floor:'shop floor', loft:'rigging loft', front:'shop front', bakehouse:'bakehouse',
    dining:'dining room', bar:'bar', kitchen:'back kitchen', barroom:'bar room', snug:'snug',
    lobby:'lobby', sorting:'sorting room', servery:'servery', flat:'flat above',
  };

  // ---- FIXTURE CATALOG ------------------------------------------------------
  // Local coords: x across the WIDTH, y across the DEPTH, +Y is the fixture's FRONT (customer side),
  // z up from the floor. rot turns it in 90deg steps. foot:[w,d] in metres.
  const CATS = ['service','shelving','cold','kitchen','seating','stock','decor'];

  // ---- shading constants (fleet recipe) ----
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }

  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { th, ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) };
  }
  function projVert(x,y,z,B){
    const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:VCX+xr*S, sy:VGY-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) };
  }
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }

  // ---- face builders --------------------------------------------------------
  let CUR_LAYER='base';
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat, layer:CUR_LAYER }; }
  function wall(out, x0,y0,x1,y1, z0,z1, mat, tex, b){
    const L=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]], mat, b||0, 0, [[0,z0],[L,z0],[L,z1],[0,z1]], tex));
  }
  function slab(out, pts, z, mat, b, tex){
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0, tex?pts.map(p=>[p[0],p[1]]):null, tex));
  }
  function box(out, x0,x1, y0,y1, z0,z1, mat, tex, b){
    wall(out, x0,y0, x1,y0, z0,z1, mat, tex, b);
    wall(out, x1,y1, x0,y1, z0,z1, mat, tex, b);
    wall(out, x1,y0, x1,y1, z0,z1, mat, tex, b);
    wall(out, x0,y1, x0,y0, z0,z1, mat, tex, b);
    slab(out, [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, (b||0)+0.25);
  }
  // a downstand beam: no lit top face, so ceiling joists read as beams under an implied ceiling
  function joist(out, x0,x1, y0,y1, z0,z1, mat){
    wall(out, x0,y0, x1,y0, z0,z1, mat, null, 0.1);
    wall(out, x1,y1, x0,y1, z0,z1, mat, null, 0.1);
    wall(out, x1,y0, x1,y1, z0,z1, mat, null, 0.1);
    wall(out, x0,y1, x0,y0, z0,z1, mat, null, 0.1);
    slab(out, [[x0,y1],[x1,y1],[x1,y0],[x0,y0]], z0, mat, -1.3);
  }
  function decalY(out, yv, ny, xs,xe, z0,z1, mat, b, tex, flat, db){
    const e=0.02*ny, uw=xe-xs, uh=z1-z0;
    const P = ny>0 ? [[xs,yv+e,z0],[xe,yv+e,z0],[xe,yv+e,z1],[xs,yv+e,z1]]
                   : [[xe,yv+e,z0],[xs,yv+e,z0],[xs,yv+e,z1],[xe,yv+e,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat));
  }
  function decalX(out, xv, nx, ys,ye, z0,z1, mat, b, tex, flat, db){
    const e=0.02*nx, uw=ye-ys, uh=z1-z0;
    const P = nx>0 ? [[xv+e,ye,z0],[xv+e,ys,z0],[xv+e,ys,z1],[xv+e,ye,z1]]
                   : [[xv+e,ys,z0],[xv+e,ye,z0],[xv+e,ye,z1],[xv+e,ys,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat));
  }
  const putOn=(axis)=>(out,plane,nrm,a0,a1,z0,z1,mat,bias,db,tex,flat)=> axis==='y'
      ? decalY(out,plane,nrm,a0,a1,z0,z1,mat,bias,tex||null,flat!==false&&!tex?true:!!flat,db)
      : decalX(out,plane,nrm,a0,a1,z0,z1,mat,bias,tex||null,flat!==false&&!tex?true:!!flat,db);
  function polyY(out, yv, ny, pts, mat, bias, db, tex){
    let a=0; for(let i=0;i<pts.length;i++){ const p=pts[i], q=pts[(i+1)%pts.length]; a += p[0]*q[1]-q[0]*p[1]; }
    const pl=((a>0)===(ny>0))?pts:pts.slice().reverse(), e=0.02*ny;
    out.push(F(pl.map(p=>[p[0], yv+e, p[1]]), mat, bias||0, db==null?0.05:db, tex?pl.map(p=>[p[0],p[1]]):null, tex||null, !tex));
  }

  // ---- textures ------------------------------------------------------------
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }
  function floorTex(kind){
    if(kind==='checker'){ const c=0.52;
      return (u,v)=>{ const cxx=Math.floor(u/c), cyy=Math.floor(v/c), light=((cxx+cyy)&1)?3:0;
        const fu=((u%c)+c)%c, fv=((v%c)+c)%c;
        return light + ((fu<0.045||fv<0.045)?-1:0) + ((!light && fu>c*0.55 && fv<c*0.4)?1:0); }; }
    if(kind==='stoneFlag'){ const c=0.62;
      return (u,v)=>{ const cxx=Math.floor(u/c), cyy=Math.floor(v/c), fu=((u%c)+c)%c, fv=((v%c)+c)%c, r=hash2(cxx|0,cyy|0);
        if(fu<0.06||fv<0.06) return -2; return (r<0.33?-1:(r>0.72?1:0)); }; }
    if(kind==='sawdust'){ return (u,v)=>{ const r=hash2(Math.round(u*7)|0, Math.round(v*7)|0);
      return r<0.20?1:(r<0.34?-1:0); }; }
    if(kind==='painted'){ return (u,v)=>{ const r=hash2(Math.round(u*3)|0, Math.round(v*3)|0);
      const p=Math.floor(u/0.9); return (r<0.07?-1:0) + (((u%0.9)+0.9)%0.9 < 0.03 ? -1 : 0) + (hash2(p|0,11)<0.3?0:0); }; }
    const PW = kind==='wideBoard'?0.82:0.42;
    return (u,v)=>{ const p=Math.floor(u/PW), fu=((u%PW)+PW)%PW;
      const tone=(hash2(p|0,0)<0.5?0:1)-(hash2(p|0,3)<0.3?1:0);
      if(fu<0.035) return -2;
      const butt=(((v%2.4)+2.4)%2.4); if(butt<0.05 && (Math.floor(v/2.4)+p)&1) return -2;
      return tone + (hash2(p*5|0, Math.floor(v/1.7)|0)<0.04?-1:0); };
  }
  function wallTex(kind){
    if(kind==='plaster') return (u,v)=>{ const m=Math.sin(u*2.3+v*0.7)*Math.sin(v*1.9-u*0.4); return m>0.86?-1:(m<-0.9?1:0); };
    if(kind==='board'||kind==='wainscot') return (u,v)=>{ const bw=0.24, f=((u%bw)+bw)%bw;
      if(f<0.035) return -2; if(f>bw-0.03) return 1; return 0; };
    if(kind==='panel'){ const pw=0.72;
      return (u,v)=>{ const f=((u%pw)+pw)%pw;
        if(f<0.05||f>pw-0.05) return -1;
        if(f<0.10||f>pw-0.10) return 1;
        const fv=((v%1.05)+1.05)%1.05; if(fv<0.05) return -1; if(fv<0.10) return 1; return 0; }; }
    if(kind==='brick'){ const c=0.16, bl=0.40;
      return (u,v)=>{ const row=Math.floor(v/c), off=(row&1)*0.5*bl, fv=((v%c)+c)%c, su=(((u+off)%bl)+bl)%bl;
        if(fv<0.03) return -2; if(su<0.035) return -2; if(fv>c-0.03) return 1; return 0; }; }
    if(kind==='block'){ const c=0.20, bl=0.40;
      return (u,v)=>{ const row=Math.floor(v/c), off=(row&1)*0.5*bl, fv=((v%c)+c)%c, su=(((u+off)%bl)+bl)%bl, r=hash2(Math.floor((u+off)/bl)|0,row|0);
        if(fv<0.04) return -2; if(su<0.04) return -2; if(fv>c-0.035) return 1; return r<0.22?-1:0; }; }
    if(kind==='tile'){ const c=0.155;
      return (u,v)=>{ const fu=((u%c)+c)%c, fv=((v%c)+c)%c;
        if(fu<0.022||fv<0.022) return -2; if(fu>c-0.03&&fv<c*0.4) return 1; return 0; }; }
    return null;
  }
  function beadTex(){ const bw=0.095; return (u,v)=>{ const f=((u%bw)+bw)%bw; return f<0.02?-2:(f>bw-0.02?1:0); }; }
  function plankTex(pw){ const PW=pw||0.26;
    return (u,v)=>{ const p=Math.floor(u/PW), f=((u%PW)+PW)%PW;
      if(f<0.03) return -2; return (hash2(p|0,7)<0.42?0:1)-(hash2(p|0,3)<0.22?1:0); }; }

  // ---- WARES: the goods that fill shelves, counters and windows -------------
  // shapes are 1-3 boxes each; the ramp is one of four per-shop goods ramps.
  function ware(out, kind, x, y, z, w, d, hMax, rnd){
    const m='goods'+(1+((rnd()*4)|0));
    if(kind==='bottle'){ const r=Math.min(w,d)*0.34, h=hMax*(0.78+rnd()*0.18);
      box(out, x-r,x+r, y-r,y+r, z, z+h*0.68,'glassC',null,0.0);
      box(out, x-r*0.44,x+r*0.44, y-r*0.44,y+r*0.44, z+h*0.68, z+h,'glassC',null,0.2);
      box(out, x-r*0.5,x+r*0.5, y-r*0.5,y+r*0.5, z+h-0.03, z+h+0.02, m,null,0.3);
      decalY(out, y+r, 1, x-r*0.8, x+r*0.8, z+h*0.24, z+h*0.5, m, 0.3, null, true, 0.06); return h; }
    if(kind==='mug'){ const r=Math.min(w,d)*0.32, h=hMax*0.34;
      box(out, x-r,x+r, y-r,y+r, z, z+h,'ceramic',null,0.0);
      box(out, x+r,x+r*1.3, y-r*0.3,y+r*0.3, z+h*0.3, z+h*0.75,'ceramic',null,0.1); return h; }
    if(kind==='crock'||kind==='jar'){ const r=Math.min(w,d)*0.38, h=hMax*(0.42+rnd()*0.2);
      box(out, x-r,x+r, y-r,y+r, z, z+h,'ceramic',null,0.0);
      box(out, x-r*0.82,x+r*0.82, y-r*0.82,y+r*0.82, z+h, z+h+0.035, m,null,0.35);
      decalY(out, y+r,1, x-r*0.7,x+r*0.7, z+h*0.30, z+h*0.62, m, 0.25, null, true, 0.06); return h+0.04; }
    if(kind==='loaf'){ const h=hMax*0.36;
      box(out, x-w*0.42,x+w*0.42, y-d*0.32,y+d*0.32, z, z+h,'crust',null,0.0);
      for(let k=0;k<3;k++) decalY(out, y+d*0.32,1, x-w*0.3+k*w*0.22, x-w*0.3+k*w*0.22+w*0.06, z+h*0.4, z+h,'crust',-1.4,null,true,0.06);
      return h; }
    if(kind==='tray'){ const h=hMax*0.16;
      box(out, x-w*0.46,x+w*0.46, y-d*0.4,y+d*0.4, z, z+h*0.5,'tin',null,0.1);
      for(let k=0;k<3;k++){ const px=x-w*0.28+k*w*0.28;
        box(out, px-w*0.10,px+w*0.10, y-d*0.24,y+d*0.24, z+h*0.5, z+h,'crust',null,0.15); } return h; }
    if(kind==='sack'){ const h=hMax*(0.52+rnd()*0.2);
      box(out, x-w*0.40,x+w*0.40, y-d*0.36,y+d*0.36, z, z+h*0.78,'sack',null,0.0);
      box(out, x-w*0.24,x+w*0.24, y-d*0.22,y+d*0.22, z+h*0.78, z+h,'sack',null,0.2);
      decalY(out, y+d*0.36,1, x-w*0.24,x+w*0.24, z+h*0.30, z+h*0.52, m, 0.3, null, true, 0.06); return h; }
    if(kind==='bolt'){ const h=hMax*(0.86+rnd()*0.12);
      box(out, x-w*0.34,x+w*0.34, y-d*0.34,y+d*0.34, z, z+h, m,null,0.0);
      for(let k=0;k<3;k++) decalY(out, y+d*0.34,1, x-w*0.3,x+w*0.3, z+h*(0.25+k*0.24), z+h*(0.25+k*0.24)+0.02, m,-1.5,null,true,0.06);
      return h; }
    if(kind==='parcel'){ const h=hMax*(0.34+rnd()*0.22);
      box(out, x-w*0.44,x+w*0.44, y-d*0.4,y+d*0.4, z, z+h,'sack',null,0.0);
      decalY(out, y+d*0.4,1, x-w*0.07,x+w*0.07, z, z+h,'rope',0.4,null,true,0.06);
      slab(out, [[x-w*0.07,y-d*0.4],[x+w*0.07,y-d*0.4],[x+w*0.07,y+d*0.4],[x-w*0.07,y+d*0.4]], z+h+0.005,'rope',0.4);
      decalY(out, y+d*0.4,1, x-w*0.30,x-w*0.12, z+h*0.5, z+h*0.8,'trim',0.7,null,true,0.07); return h; }
    if(kind==='ledger'){ const h=hMax*(0.5+rnd()*0.2);
      for(let k=0;k<3;k++) box(out, x-w*0.32+k*0.035, x+w*0.32-k*0.035, y-d*0.3,y+d*0.3, z+k*h/3, z+(k+1)*h/3, k%2?'goods2':'goods3',null,0.05);
      return h; }
    if(kind==='coil'){ const r=Math.min(w,d)*0.42, h=hMax*0.30;
      for(let k=0;k<2;k++){ const rr=r*(1-k*0.22);
        box(out, x-rr,x+rr, y-rr*0.9,y+rr*0.9, z+k*h*0.5, z+(k+1)*h*0.5,'rope',null,0.0);
        box(out, x-rr*0.9,x+rr*0.9, y-rr,y+rr, z+k*h*0.5, z+(k+1)*h*0.5,'rope',null,0.0); }
      box(out, x-r*0.32,x+r*0.32, y-r*0.30,y+r*0.30, z, z+h+0.01,'dark',null,-0.6); return h; }
    if(kind==='curio'){ const r=Math.min(w,d)*0.3, h=hMax*(0.4+rnd()*0.35);
      box(out, x-r,x+r, y-r,y+r, z, z+h*0.3, m,null,0.0);
      box(out, x-r*0.6,x+r*0.6, y-r*0.6,y+r*0.6, z+h*0.3, z+h, m,null,0.2); return h; }
    if(kind==='tin'){ const r=Math.min(w,d)*0.34, h=hMax*(0.36+rnd()*0.18);
      box(out, x-r,x+r, y-r,y+r, z, z+h,'tin',null,0.0);
      decalY(out, y+r,1, x-r*0.86,x+r*0.86, z+h*0.26, z+h*0.72, m, 0.25, null, true, 0.06); return h; }
    if(kind==='crateB'){ const h=hMax*(0.4+rnd()*0.18);
      box(out, x-w*0.44,x+w*0.44, y-d*0.4,y+d*0.4, z, z+h,'crate',null,0.0);
      decalY(out, y+d*0.4,1, x-w*0.4,x+w*0.4, z+h*0.48, z+h*0.54,'crate',-1.5,null,true,0.06); return h; }
    // 'box' — the default carton
    const h=hMax*(0.42+rnd()*0.4);
    box(out, x-w*0.42,x+w*0.42, y-d*0.38,y+d*0.38, z, z+h, m,null,0.0);
    decalY(out, y+d*0.38,1, x-w*0.30,x+w*0.30, z+h*0.42, z+h*0.74,'trim',0.45,null,true,0.06);
    return h;
  }
  // fill a band with wares, left to right
  function waresRow(out, x0,x1, yc, d, z, hMax, s, dens){
    const stock=(dens==null?s.stock:dens); if(stock<=0.01) return;
    const rnd=s.rnd, kinds=s.wares; let x=x0+0.06;
    let guard=0;
    while(x < x1-0.10 && guard++<40){
      const kind=kinds[(rnd()*kinds.length)|0];
      const w=Math.min(0.34, Math.max(0.16, 0.18+rnd()*0.16));
      if(x+w > x1-0.04) break;
      if(rnd() < stock) ware(out, kind, x+w/2, yc, z, w, d, hMax, rnd);
      x += w + 0.025 + rnd()*0.06*(1-stock*0.7);
    }
  }

  // ---- fixture builders (local coords, +Y = front) --------------------------
  function fCounter(out,s){
    const w=s.w, d=s.d, h=1.05;
    box(out, -w/2,w/2, -d/2,d/2-0.06, 0, h-0.06,'paint', wallTex('panel'), 0.0);      // carcase
    box(out, -w/2-0.05,w/2+0.05, -d/2-0.03,d/2+0.05, h-0.06, h,'worktop', null, 0.35); // top w/ overhang
    decalY(out, d/2-0.06, 1, -w/2+0.04, w/2-0.04, 0.02, 0.14,'trim',0.45,null,true,0.06); // kick
    const np=Math.max(2, Math.round(w/0.78));
    for(let i=0;i<np;i++){ const c=-w/2+w*((i+0.5)/np), pw=w/np-0.14;
      decalY(out, d/2-0.06, 1, c-pw/2, c+pw/2, 0.24, h-0.22,'paint',-0.9,null,true,0.07);
      decalY(out, d/2-0.06, 1, c-pw/2+0.05, c+pw/2-0.05, 0.29, h-0.27,'paint',0.5,null,true,0.08); }
    // till + counter scale on top
    box(out, w/2-0.52, w/2-0.10, -0.12, 0.20, h, h+0.34,'brass', null, 0.15);
    box(out, w/2-0.46, w/2-0.16, -0.06, 0.02, h+0.34, h+0.44,'brass', null, 0.35);
    decalY(out, 0.20, 1, w/2-0.46, w/2-0.16, h+0.10, h+0.28,'dark',0.0,null,true,0.07);
    box(out, -w/2+0.20, -w/2+0.52, -0.14, 0.16, h, h+0.10,'tin', null, 0.2);
    box(out, -w/2+0.30, -w/2+0.42, -0.05, 0.05, h+0.10, h+0.40,'iron', null, 0.1);
    box(out, -w/2+0.18, -w/2+0.54, -0.10, 0.10, h+0.40, h+0.46,'tin', null, 0.35);
    // back shelf inside the counter with stock
    waresRow(out, -w/2+0.10, w/2-0.66, -d/2+0.18, 0.24, h, 0.22, s, Math.min(1,s.stock*0.8));
  }
  function fDisplayCase(out,s){
    const w=s.w, d=s.d, h=1.14;
    box(out, -w/2,w/2, -d/2,d/2, 0, 0.42,'paint', wallTex('panel'), 0.0);
    box(out, -w/2,w/2, -d/2,d/2, 0.42, h-0.06,'caseGlass', null, 0.0);
    box(out, -w/2-0.04,w/2+0.04, -d/2-0.03,d/2+0.03, h-0.06, h,'worktop', null, 0.35);
    for(const xv of [-w/2,w/2]) box(out, xv-0.03,xv+0.03, -d/2,d/2, 0.42, h-0.06,'brass',null,0.2);
    decalY(out, d/2, 1, -w/2, w/2, 0.42, 0.47,'brass',0.5,null,true,0.09);
    for(const zz of [0.60, 0.86]){
      slab(out, [[-w/2+0.05,-d/2+0.05],[w/2-0.05,-d/2+0.05],[w/2-0.05,d/2-0.05],[-w/2+0.05,d/2-0.05]], zz,'glassSh',0.3);
      waresRow(out, -w/2+0.08, w/2-0.08, 0, d-0.16, zz, 0.22, s, null); }
    waresRow(out, -w/2+0.08, w/2-0.08, 0, d-0.16, 0.44, 0.14, s, null);
  }
  function fColdTable(out,s){
    const w=s.w, d=s.d, h=0.92;
    for(const xv of [-w/2+0.10, w/2-0.10]) for(const yv of [-d/2+0.10, d/2-0.10])
      box(out, xv-0.05,xv+0.05, yv-0.05,yv+0.05, 0, h-0.14,'steel',null,0.1);
    box(out, -w/2,w/2, -d/2,d/2, h-0.14, h,'zinc', null, 0.15);
    slab(out, [[-w/2+0.06,-d/2+0.06],[w/2-0.06,-d/2+0.06],[w/2-0.06,d/2-0.06],[-w/2+0.06,d/2-0.06]], h+0.005,'ice',-0.7);
    const rnd=s.rnd, n=Math.max(4, Math.round(w*3.2));
    for(let i=0;i<n;i++){ if(rnd()>s.stock) continue;
      const px=-w/2+0.16+(w-0.32)*rnd(), py=-d/2+0.14+(d-0.28)*rnd(), L=0.13+rnd()*0.10;
      box(out, px-L,px+L, py-0.055,py+0.055, h+0.01, h+0.075, (i%3)?'fishA':'fishB', null, 0.1);
      box(out, px-L-0.05,px-L, py-0.03,py+0.03, h+0.02, h+0.07, (i%3)?'fishA':'fishB', null, -0.3); }
    decalY(out, d/2, 1, -w/2+0.10, w/2-0.10, h-0.12, h-0.02,'zinc',0.6,null,true,0.07);
    box(out, -w/2,w/2, -d/2+0.02,-d/2+0.10, h, h+0.24,'zinc',null,0.3);      // back splash
    for(let k=0;k<3;k++){ const px=-w/2+w*((k+0.5)/3);                        // price cards
      decalY(out, d/2+0.02, 1, px-0.10, px+0.10, h+0.02, h+0.16,'trim',0.8,null,true,0.06); }
  }
  function fBar(out,s){
    const w=s.w, d=s.d, h=1.13;
    box(out, -w/2,w/2, -d/2,d/2-0.10, 0, h-0.07,'oak', wallTex('panel'), 0.0);
    box(out, -w/2-0.06,w/2+0.06, -d/2-0.04,d/2+0.10, h-0.07, h,'oak', null, 0.4);
    decalY(out, d/2-0.10, 1, -w/2, w/2, h-0.20, h-0.07,'brass',0.7,null,true,0.08);       // brass nosing
    box(out, -w/2+0.04,w/2-0.04, d/2+0.02,d/2+0.08, 0.16, 0.22,'brass', null, 0.5);       // foot rail
    for(const xv of [-w/2+0.24, 0, w/2-0.24]) box(out, xv-0.04,xv+0.04, d/2+0.01,d/2+0.09, 0, 0.19,'brass',null,0.2);
    const np=Math.max(3, Math.round(w/0.72));
    for(let i=0;i<np;i++){ const c=-w/2+w*((i+0.5)/np), pw=w/np-0.13;
      decalY(out, d/2-0.10, 1, c-pw/2, c+pw/2, 0.28, h-0.30,'oak',-1.0,null,true,0.09);
      decalY(out, d/2-0.10, 1, c-pw/2+0.05, c+pw/2-0.05, 0.33, h-0.35,'oak',0.55,null,true,0.10); }
    // beer engine pulls + glasses on the top
    for(let k=0;k<3;k++){ const px=-w/2+0.42+k*0.34;
      box(out, px-0.05,px+0.05, -0.12,-0.02, h, h+0.30,'brass',null,0.2);
      box(out, px-0.03,px+0.03, -0.02, 0.16, h+0.24, h+0.30,'brass',null,0.4);
      box(out, px-0.04,px+0.04, 0.14, 0.20, h+0.10, h+0.26,'iron',null,0.1); }
    waresRow(out, w/2-1.05, w/2-0.12, 0.02, 0.22, h, 0.20, s, Math.min(1,s.stock*0.9));
  }
  function fBackBar(out,s){
    const w=s.w, d=s.d, h=2.38;
    box(out, -w/2,w/2, -d/2,d/2, 0, 0.92,'oak', wallTex('panel'), 0.0);
    box(out, -w/2-0.04,w/2+0.04, -d/2,d/2+0.05, 0.92, 0.99,'worktop', null, 0.35);
    for(const xv of [-w/2+0.06, w/2-0.06]) box(out, xv-0.06,xv+0.06, -d/2,d/2-0.06, 0.99, h,'oak',null,0.1);
    decalY(out, d/2-0.08, 1, -w/2+0.14, w/2-0.14, 1.20, h-0.34,'mirror',0.25,null,true,0.06);
    decalY(out, d/2-0.06, 1, -w/2+0.20, -w/2+0.52, 1.40, h-0.60,'trim',0.8,null,true,0.08);
    for(const zz of [1.16, 1.56, 1.96]){
      box(out, -w/2+0.10, w/2-0.10, -d/2+0.04, d/2-0.06, zz-0.05, zz,'oak', null, 0.3);
      waresRow(out, -w/2+0.14, w/2-0.14, 0.02, d-0.16, zz, 0.34, s, null); }
    box(out, -w/2-0.06,w/2+0.06, -d/2,d/2-0.02, h, h+0.14,'oak', null, 0.5);    // cornice
    box(out, -w/2+0.10,w/2-0.10, -d/2+0.02,d/2-0.06, h+0.14, h+0.24,'oak', null, 0.55);
  }
  function fWallShelf(out,s){
    const w=s.w, d=s.d, h=2.12;
    for(const xv of [-w/2+0.05, w/2-0.05]) box(out, xv-0.05,xv+0.05, -d/2,d/2-0.04, 0, h,'wood',null,0.1);
    box(out, -w/2,w/2, -d/2,-d/2+0.05, 0, h,'wood', plankTex(0.22), -0.2);
    const nb=4;
    for(let i=0;i<nb;i++){ const zz=0.42+i*((h-0.58)/nb);
      box(out, -w/2+0.05, w/2-0.05, -d/2+0.02, d/2-0.02, zz-0.045, zz,'wood', null, 0.3);
      decalY(out, d/2-0.02, 1, -w/2+0.05, w/2-0.05, zz-0.05, zz-0.005,'wood',0.55,null,true,0.07);
      waresRow(out, -w/2+0.08, w/2-0.08, 0, d-0.10, zz, ((h-0.58)/nb)-0.10, s, null); }
    box(out, -w/2, w/2, -d/2, d/2-0.02, 0.30, 0.38,'wood', null, 0.3);
    waresRow(out, -w/2+0.08, w/2-0.08, 0, d-0.10, 0.38, 0.26, s, null);
  }
  function fGondola(out,s){
    const w=s.w, d=s.d, h=1.52;
    box(out, -0.05,0.05, -d/2+0.06,d/2-0.06, 0, h,'paint',null,0.0);                       // spine
    for(const xv of [-w/2+0.06, 0, w/2-0.06]) box(out, xv-0.04,xv+0.04, -0.04,0.04, 0, h,'paint',null,0.05);
    box(out, -w/2,w/2, -d/2,d/2, 0, 0.16,'paint',null,0.05);                               // plinth
    for(const sgn of [-1,1]){
      for(let i=0;i<3;i++){ const zz=0.42+i*0.38, yb=sgn*(d/2);
        const y0=Math.min(sgn*0.05, yb), y1=Math.max(sgn*0.05, yb);
        box(out, -w/2+0.04, w/2-0.04, y0, y1, zz-0.04, zz,'paint', null, 0.3);
        decalY(out, yb, sgn, -w/2+0.04, w/2-0.04, zz-0.05, zz+0.02,'trim',0.6,null,true,0.07);
        waresRow(out, -w/2+0.08, w/2-0.08, sgn*(d/2-0.16), d/2-0.10, zz, 0.30, s, null); }
      waresRow(out, -w/2+0.08, w/2-0.08, sgn*(d/2-0.16), d/2-0.10, 0.16, 0.24, s, null);
    }
  }
  function fBinRack(out,s){
    const w=s.w, d=s.d, h=1.62;
    box(out, -w/2,w/2, -d/2,-d/2+0.05, 0, h,'wood', plankTex(0.22), -0.2);
    for(const xv of [-w/2+0.05, w/2-0.05]) box(out, xv-0.05,xv+0.05, -d/2,d/2, 0, h,'wood',null,0.1);
    const nr=4, nc=Math.max(3, Math.round(w/0.4));
    for(let r=0;r<nr;r++){ const z0=0.14+r*((h-0.22)/nr), z1=z0+((h-0.22)/nr)-0.06;
      box(out, -w/2+0.05, w/2-0.05, -d/2+0.04, d/2-0.02, z0-0.04, z0,'wood',null,0.3);
      for(let c=0;c<nc;c++){ const px=-w/2+0.08+((w-0.16)/nc)*(c+0.5), bw=(w-0.16)/nc-0.05;
        // angled open bin
        out.push(F([[px-bw/2, d/2-0.02, z0],[px+bw/2, d/2-0.02, z0],[px+bw/2, -d/2+0.06, z1-0.04],[px-bw/2, -d/2+0.06, z1-0.04]],'wood',0.15,0.06,null,null));
        decalY(out, d/2-0.02, 1, px-bw/2, px+bw/2, z0, z0+0.10,'wood',0.4,null,true,0.07);
        if(s.rnd()<s.stock){ const m='goods'+(1+((s.rnd()*4)|0));
          box(out, px-bw/2+0.03, px+bw/2-0.03, -d/2+0.10, d/2-0.08, z0+0.03, z0+0.11, m, null, 0.05); }
      } }
  }
  function fPigeonholes(out,s){
    const w=s.w, d=s.d, h=2.18;
    box(out, -w/2,w/2, -d/2,d/2, 0, h,'wood', null, -0.3);
    const nc=Math.max(5, Math.round(w/0.28)), nr=6;
    for(let r=0;r<nr;r++) for(let c=0;c<nc;c++){
      const px=-w/2+0.06+((w-0.12)/nc)*c, pw=(w-0.12)/nc-0.035;
      const pz=0.52+((h-0.66)/nr)*r, ph=((h-0.66)/nr)-0.035;
      decalY(out, d/2, 1, px, px+pw, pz, pz+ph,'dark',0.0,null,true,0.08);
      if(s.rnd()<s.stock*0.85){ const m='goods'+(1+((s.rnd()*4)|0));
        decalY(out, d/2-0.03, 1, px+0.01, px+pw-0.01, pz+0.01, pz+ph*0.62, m, 0.1, null, true, 0.10); }
    }
    box(out, -w/2,w/2, -d/2,d/2-0.02, 0.36, 0.50,'wood', null, 0.3);
    waresRow(out, -w/2+0.06, w/2-0.06, 0, d-0.08, 0.50, 0.22, s, null);
    box(out, -w/2-0.04,w/2+0.04, -d/2,d/2+0.02, h, h+0.10,'wood', null, 0.5);
  }
  function fWicket(out,s){
    const w=s.w, d=s.d, h=2.34;
    box(out, -w/2,w/2, -d/2,d/2-0.08, 0, 1.06,'paint', wallTex('panel'), 0.0);
    box(out, -w/2-0.05,w/2+0.05, -d/2-0.03,d/2+0.06, 1.06, 1.14,'worktop', null, 0.35);
    for(const xv of [-w/2+0.05, w/2-0.05]) box(out, xv-0.05,xv+0.05, -d/2,d/2-0.08, 1.14, h,'paint',null,0.1);
    box(out, -w/2,w/2, -d/2,d/2-0.08, h, h+0.10,'paint', null, 0.5);
    // grille bars over the opening, with a serving gap in the middle
    const gapW=Math.min(0.52, w*0.26);
    for(const [a0,a1] of [[-w/2+0.10, -gapW/2],[gapW/2, w/2-0.10]]){
      decalY(out, d/2-0.08, 1, a0, a1, 1.16, h-0.08,'dark',0.0,null,true,0.08);
      const nb=Math.max(3, Math.round((a1-a0)/0.11));
      for(let i=0;i<=nb;i++){ const px=a0+(a1-a0)*(i/nb);
        decalY(out, d/2-0.08, 1, px-0.018, px+0.018, 1.16, h-0.08,'brass',0.5,null,true,0.10); }
      for(const zz of [1.52, 1.94]) decalY(out, d/2-0.08, 1, a0, a1, zz-0.018, zz+0.018,'brass',0.5,null,true,0.10);
    }
    decalY(out, d/2-0.08, 1, -gapW/2-0.05, gapW/2+0.05, 1.16, 1.20,'brass',0.6,null,true,0.09);
    decalY(out, d/2-0.08, 1, -gapW/2, gapW/2, 1.20, 1.66,'dark',0.0,null,true,0.08);
    waresRow(out, -w/2+0.12, w/2-0.12, -0.02, 0.22, 1.14, 0.18, s, Math.min(1,s.stock*0.6));
  }
  function fPass(out,s){
    const w=s.w, d=s.d, h=1.34;
    box(out, -w/2,w/2, -d/2,d/2, 0, h-0.08,'steel', null, 0.0);
    box(out, -w/2-0.04,w/2+0.04, -d/2-0.03,d/2+0.03, h-0.08, h,'zinc', null, 0.35);
    decalY(out, d/2, 1, -w/2+0.06, w/2-0.06, 0.14, h-0.20,'steel',-0.6,null,true,0.07);
    for(const xv of [-w/2+0.08, w/2-0.08]) box(out, xv-0.05,xv+0.05, -d/2+0.06,d/2-0.06, h, h+0.86,'steel',null,0.1);
    box(out, -w/2,w/2, -d/2+0.06,d/2-0.06, h+0.62, h+0.70,'zinc', null, 0.3);       // pass shelf
    box(out, -w/2+0.06,w/2-0.06, -0.06,0.06, h+0.80, h+0.86,'iron', null, 0.2);     // lamp bar
    const nl=Math.max(2, Math.round(w/0.9));
    for(let i=0;i<nl;i++){ const px=-w/2+w*((i+0.5)/nl);
      box(out, px-0.11,px+0.11, -0.09,0.09, h+0.70, h+0.80,'iron',null,0.1);
      slab(out, [[px-0.09,-0.07],[px+0.09,-0.07],[px+0.09,0.07],[px-0.09,0.07]], h+0.695,'heat',0.0); }
    waresRow(out, -w/2+0.10, w/2-0.10, 0, d-0.14, h, 0.16, s, Math.min(1,s.stock*0.8));
  }
  function fOvenBank(out,s){
    const w=s.w, d=s.d, h=1.58;
    box(out, -w/2,w/2, -d/2,d/2, 0, h,'brick', wallTex('brick'), 0.0);
    box(out, -w/2-0.05,w/2+0.05, -d/2-0.04,d/2+0.04, h, h+0.12,'stone', null, 0.4);
    const nd=Math.max(1, Math.round(w/0.8));
    for(let i=0;i<nd;i++){ const px=-w/2+w*((i+0.5)/nd), dw=Math.min(0.62, w/nd-0.16);
      decalY(out, d/2, 1, px-dw/2-0.05, px+dw/2+0.05, 0.42, 1.16,'iron',0.35,null,true,0.06);
      decalY(out, d/2, 1, px-dw/2, px+dw/2, 0.48, 1.10,'iron',-0.9,null,true,0.08);
      decalY(out, d/2, 1, px-dw/2+0.06, px+dw/2-0.06, 0.54, 0.86,'fire',0.0,null,true,0.10);
      decalY(out, d/2, 1, px-0.05, px+0.05, 1.16, 1.26,'brass',0.8,null,true,0.09); }
    box(out, w/2-0.44, w/2-0.06, -d/2+0.06, -d/2+0.36, h+0.12, h+1.6,'iron', null, 0.1);   // flue
    waresRow(out, -w/2+0.08, w/2-0.08, 0, d-0.20, h+0.12, 0.20, s, Math.min(1,s.stock*0.7));
  }
  function fPrepTable(out,s){
    const w=s.w, d=s.d, h=0.90;
    for(const xv of [-w/2+0.08, w/2-0.08]) for(const yv of [-d/2+0.08, d/2-0.08])
      box(out, xv-0.04,xv+0.04, yv-0.04,yv+0.04, 0, h-0.05,'steel',null,0.1);
    box(out, -w/2,w/2, -d/2,d/2, h-0.05, h,'zinc', null, 0.35);
    box(out, -w/2+0.06,w/2-0.06, -d/2+0.06,d/2-0.06, 0.26, 0.31,'steel', null, 0.25);
    waresRow(out, -w/2+0.10, w/2-0.10, 0.02, d-0.24, h, 0.22, s, Math.min(1,s.stock*0.85));
    waresRow(out, -w/2+0.12, w/2-0.12, -0.02, d-0.28, 0.31, 0.18, s, Math.min(1,s.stock*0.6));
  }
  function fSinkRun(out,s){
    const w=s.w, d=s.d, h=0.90;
    box(out, -w/2,w/2, -d/2,d/2, 0, h-0.06,'paint', wallTex('panel'), 0.0);
    box(out, -w/2-0.04,w/2+0.04, -d/2-0.03,d/2+0.03, h-0.06, h,'zinc', null, 0.35);
    for(const sgn of [-1,1]){ const c=sgn*w*0.22;
      slab(out, [[c-w*0.16,-d*0.28],[c+w*0.16,-d*0.28],[c+w*0.16,d*0.28],[c-w*0.16,d*0.28]], h-0.02,'zinc',-1.2); }
    box(out, -0.03,0.03, -d/2+0.10,-d/2+0.16, h, h+0.42,'brass',null,0.2);
    box(out, -0.03,0.03, -d/2+0.16,-d/2+0.34, h+0.36, h+0.42,'brass',null,0.4);
    decalY(out, d/2, 1, -w/2+0.06, w/2-0.06, 0.16, h-0.16,'paint',-0.8,null,true,0.07);
  }
  function fLarder(out,s){
    const w=s.w, d=s.d, h=2.02;
    box(out, -w/2,w/2, -d/2,d/2, 0, h,'paint', null, 0.0);
    for(const sgn of [-1,1]){ const c=sgn*w*0.24;
      for(const [z0,z1] of [[0.10,0.92],[1.02,h-0.08]]){
        decalY(out, d/2, 1, c-w*0.21, c+w*0.21, z0, z1,'paint',-0.8,null,true,0.07);
        decalY(out, d/2, 1, c-w*0.16, c+w*0.16, z0+0.06, z1-0.06,'paint',0.5,null,true,0.09); }
      decalY(out, d/2, 1, c+sgn*w*0.16, c+sgn*w*0.19, 0.86, 0.96,'brass',0.9,null,true,0.10); }
    box(out, -w/2-0.04,w/2+0.04, -d/2,d/2+0.03, h, h+0.10,'paint', null, 0.5);
  }
  function fIceChest(out,s){
    const w=s.w, d=s.d, h=0.94;
    box(out, -w/2,w/2, -d/2,d/2, 0, h-0.08,'paint', plankTex(0.24), 0.0);
    box(out, -w/2-0.03,w/2+0.03, -d/2-0.03,d/2+0.03, h-0.08, h,'zinc', null, 0.35);
    for(const zz of [0.16, 0.62]){ decalY(out, d/2,1, -w/2+0.04, w/2-0.04, zz-0.03, zz+0.03,'iron',0.4,null,true,0.07);
      decalX(out, w/2,1, -d/2+0.04, d/2-0.04, zz-0.03, zz+0.03,'iron',0.4,null,true,0.07); }
    for(const sgn of [-1,1]) box(out, sgn*w*0.24-w*0.20, sgn*w*0.24+w*0.20, -d/2+0.04, d/2-0.04, h, h+0.04,'zinc',null,0.5);
    for(const sgn of [-1,1]) box(out, sgn*w*0.24-0.06, sgn*w*0.24+0.06, -0.04, 0.04, h+0.04, h+0.10,'brass',null,0.3);
  }
  function fTable(out,s){
    const w=s.w, d=s.d, h=0.76;
    for(const xv of [-w/2+0.12, w/2-0.12]) for(const yv of [-d/2+0.12, d/2-0.12])
      box(out, xv-0.045,xv+0.045, yv-0.045,yv+0.045, 0, h-0.06,'wood',null,0.05);
    box(out, -w/2+0.08,w/2-0.08, -d/2+0.10,d/2-0.10, h-0.24, h-0.18,'wood',null,0.15);
    box(out, -w/2,w/2, -d/2,d/2, h-0.06, h,'wood', plankTex(0.28), 0.35);
    if(s.cloth){ box(out, -w/2-0.04,w/2+0.04, -d/2-0.04,d/2+0.04, h-0.02, h+0.02,'linen',null,0.45);
      for(const sgn of [-1,1]){ decalY(out, sgn*(d/2+0.04), sgn, -w/2-0.04, w/2+0.04, h-0.22, h+0.02,'linen',0.1,null,true,0.06);
        decalX(out, sgn*(w/2+0.04), sgn, -d/2-0.04, d/2+0.04, h-0.22, h+0.02,'linen',0.1,null,true,0.06); } }
    const z=h+(s.cloth?0.02:0);
    if(s.stock>0.1){ ware(out,'crock', -w*0.12, 0, z, 0.16, 0.16, 0.22, s.rnd);
      ware(out,'bottle', w*0.16, 0.06, z, 0.13, 0.13, 0.26, s.rnd); }
  }
  function fRoundTable(out,s){
    const r=Math.min(s.w,s.d)/2, h=0.74;
    box(out, -0.07,0.07, -0.07,0.07, 0, h-0.05,'wood',null,0.1);
    box(out, -r*0.62,r*0.62, -0.06,0.06, 0, 0.06,'wood',null,0.2);
    box(out, -0.06,0.06, -r*0.62,r*0.62, 0, 0.06,'wood',null,0.2);
    box(out, -r,r, -r*0.84,r*0.84, h-0.05, h,'wood', plankTex(0.3), 0.35);
    box(out, -r*0.84,r*0.84, -r,r, h-0.05, h,'wood', plankTex(0.3), 0.35);
    if(s.stock>0.1) ware(out,'jar', 0, 0, h, 0.16, 0.16, 0.24, s.rnd);
  }
  function fChair(out,s){
    const w=0.44, d=0.42, sh=0.45;
    for(const [px,py] of [[-0.17,-0.16],[0.17,-0.16],[-0.17,0.16],[0.17,0.16]])
      box(out, px-0.028,px+0.028, py-0.028,py+0.028, 0, sh-0.04,'wood',null,0.05);
    box(out, -w/2,w/2, -d/2,d/2, sh-0.04, sh,'wood',null,0.3);
    box(out, -w/2+0.03,w/2-0.03, -d/2,-d/2+0.05, sh, sh+0.52,'wood',null,0.15);
    for(const zz of [sh+0.20, sh+0.38]) box(out, -w/2+0.05,w/2-0.05, -d/2+0.005,-d/2+0.045, zz-0.025, zz+0.025,'wood',null,0.25);
  }
  function fStool(out,s){
    const r=0.19, sh=0.74;
    for(let i=0;i<3;i++){ const a=i*2.094, px=Math.cos(a)*r*0.72, py=Math.sin(a)*r*0.72;
      box(out, px-0.028,px+0.028, py-0.028,py+0.028, 0, sh-0.05,'wood',null,0.05); }
    box(out, -r*0.7,r*0.7, -r*0.7,r*0.7, sh*0.36, sh*0.40,'wood',null,0.2);
    box(out, -r,r, -r*0.84,r*0.84, sh-0.05, sh,'leather',null,0.35);
    box(out, -r*0.84,r*0.84, -r,r, sh-0.05, sh,'leather',null,0.35);
  }
  function fBooth(out,s){
    const w=s.w, d=s.d, bh=1.22, sh=0.46;
    for(const sgn of [-1,1]){ const yb=sgn*(d/2-0.24);
      box(out, -w/2,w/2, yb-0.24,yb+0.24, 0, sh,'wood',null,0.0);
      box(out, -w/2,w/2, yb-0.22,yb+0.22, sh, sh+0.06,'leather',null,0.35);
      const back=sgn*(d/2-0.05);
      box(out, -w/2,w/2, back-0.07,back+0.07, 0, bh,'wood', wallTex('panel'), 0.05);
      decalY(out, back-sgn*0.08, -sgn, -w/2+0.05, w/2-0.05, sh+0.06, bh-0.14,'leather',0.1,null,true,0.06);
      for(let k=1;k<4;k++){ const px=-w/2+w*(k/4);
        decalY(out, back-sgn*0.09, -sgn, px-0.02, px+0.02, sh+0.10, bh-0.18,'leather',-1.3,null,true,0.08); }
      box(out, -w/2,w/2, back-0.09,back+0.09, bh, bh+0.07,'wood',null,0.45); }
    const th=0.74;
    box(out, -0.06,0.06, -0.06,0.06, 0, th-0.05,'iron',null,0.1);
    box(out, -w*0.42,w*0.42, -d*0.16,d*0.16, th-0.05, th,'worktop', null, 0.35);
    if(s.stock>0.1){ ware(out,'crock', -w*0.14, 0, th, 0.15,0.15, 0.20, s.rnd);
      ware(out,'mug', w*0.12, 0.05, th, 0.14,0.14, 0.24, s.rnd); }
  }
  function fBench(out,s){
    const w=s.w, d=0.38, sh=0.46;
    for(const xv of [-w/2+0.12, w/2-0.12]){ box(out, xv-0.05,xv+0.05, -d/2,d/2, 0, sh-0.05,'wood',null,0.05);
      box(out, xv-0.16,xv+0.16, -d/2+0.02,d/2-0.02, 0, 0.05,'wood',null,0.2); }
    box(out, -w/2,w/2, -d/2,d/2, sh-0.05, sh,'wood', plankTex(0.20), 0.3);
    box(out, -w/2+0.06,w/2-0.06, -0.03,0.03, sh*0.42, sh*0.48,'wood',null,0.15);
  }
  function fCrateStack(out,s){
    const rnd=s.rnd; let z=0;
    const n=Math.max(1, Math.round(1+s.stock*2.4));
    for(let i=0;i<n;i++){ const sw=0.52-i*0.03, jx=(rnd()-0.5)*0.10, jy=(rnd()-0.5)*0.10;
      box(out, -sw/2+jx, sw/2+jx, -sw/2+jy, sw/2+jy, z, z+sw*0.74,'crate', plankTex(0.18), 0.05);
      for(const zz of [z+sw*0.22, z+sw*0.54]){
        decalY(out, sw/2+jy, 1, -sw/2+jx+0.03, sw/2+jx-0.03, zz-0.026, zz+0.026,'crate',-1.4,null,true,0.06);
        decalX(out, sw/2+jx, 1, -sw/2+jy+0.03, sw/2+jy-0.03, zz-0.026, zz+0.026,'crate',-1.4,null,true,0.06); }
      z += sw*0.74; }
  }
  function fBarrelStack(out,s){
    const r=0.30, h=0.82;
    for(const px of [-0.34, 0.34]){
      box(out, px-r,px+r, -r*0.82,r*0.82, 0, h,'oak',null,0.0);
      box(out, px-r*0.82,px+r*0.82, -r,r, 0, h,'oak',null,0.0);
      for(const zz of [h*0.16, h*0.5, h*0.86]){
        decalY(out, r, 1, px-r*0.78, px+r*0.78, zz-0.033, zz+0.033,'iron',0.3,null,true,0.06);
        decalY(out, -r,-1, px-r*0.78, px+r*0.78, zz-0.033, zz+0.033,'iron',0.3,null,true,0.06);
        decalX(out, px+r, 1, -r*0.78, r*0.78, zz-0.033, zz+0.033,'iron',0.3,null,true,0.06); }
      slab(out, [[px-r*0.8,-r*0.66],[px+r*0.8,-r*0.66],[px+r*0.8,r*0.66],[px-r*0.8,r*0.66]], h+0.01,'oak',0.5); }
    if(s.stock>0.5){ box(out, -0.28,0.28, -0.22,0.22, h, h+0.52,'oak',null,0.0);
      for(const zz of [h+0.10, h+0.42]) decalY(out, 0.22,1, -0.24,0.24, zz-0.03, zz+0.03,'iron',0.3,null,true,0.06); }
  }
  function fSackPile(out,s){
    const rnd=s.rnd;
    for(let i=0;i<3;i++){ const px=-0.26+i*0.26, py=(i%2?0.06:-0.06), h=0.34+rnd()*0.10;
      box(out, px-0.20,px+0.20, py-0.17,py+0.17, 0, h*0.78,'sack',null,0.0);
      box(out, px-0.12,px+0.12, py-0.10,py+0.10, h*0.78, h,'sack',null,0.2);
      decalY(out, py+0.17,1, px-0.11,px+0.11, h*0.30, h*0.52,'goods2',0.3,null,true,0.06); }
    if(s.stock>0.6){ box(out, -0.16,0.24, -0.14,0.14, 0.34, 0.60,'sack',null,0.1); }
  }
  function fRopeRack(out,s){
    const w=s.w, d=s.d, h=1.92;
    for(const xv of [-w/2+0.06, w/2-0.06]) box(out, xv-0.05,xv+0.05, -d/2,d/2-0.06, 0, h,'wood',null,0.1);
    for(const zz of [0.58, 1.10, 1.62]){
      box(out, -w/2,w/2, -0.035,0.035, zz-0.035, zz+0.035,'iron',null,0.2);
      const nc=Math.max(2, Math.round(w/0.46));
      for(let i=0;i<nc;i++){ if(s.rnd()>s.stock) continue;
        const px=-w/2+w*((i+0.5)/nc), r=0.15;
        box(out, px-r,px+r, -0.05,0.05, zz-0.34, zz,'rope',null,0.0);
        box(out, px-r*0.62,px+r*0.62, -0.06,0.06, zz-0.42, zz-0.30,'rope',null,0.15); } }
    for(const zz of [0.28, 0.86, 1.40]){ const nc=Math.max(2, Math.round(w/0.52));
      for(let i=0;i<nc;i++){ if(s.rnd()>s.stock*0.7) continue;
        const px=-w/2+0.14+((w-0.28)/nc)*(i+0.5);
        box(out, px-0.06,px+0.06, -0.05,0.05, zz-0.20, zz,'iron',null,0.1); } }
  }
  function fPotbelly(out,s){
    const r=0.30, h=1.02;
    box(out, -r*0.7,r*0.7, -r*0.7,r*0.7, 0, 0.16,'iron',null,0.1);
    box(out, -r,r, -r*0.86,r*0.86, 0.16, h*0.66,'iron',null,0.0);
    box(out, -r*0.86,r*0.86, -r,r, 0.16, h*0.66,'iron',null,0.0);
    box(out, -r*0.72,r*0.72, -r*0.62,r*0.62, h*0.66, h,'iron',null,0.05);
    box(out, -r*0.82,r*0.82, -r*0.72,r*0.72, h, h+0.06,'iron',null,0.4);
    box(out, -0.10,0.10, -0.09,0.09, h+0.06, h+1.68,'iron',null,0.1);
    decalY(out, r*0.86, 1, -r*0.5, r*0.5, 0.30, 0.56,'iron',-1.0,null,true,0.07);
    decalY(out, r*0.86, 1, -r*0.38, r*0.38, 0.34, 0.50,'fire',0.0,null,true,0.09);
    box(out, -0.04,0.04, r*0.86, r*0.86+0.14, 0.40, 0.46,'brass',null,0.3);
    box(out, -r*1.5,-r*0.95, -r*0.6,r*0.6, 0, 0.30,'crate', plankTex(0.16), 0.05);
  }
  function fRug(out,s){
    const w=s.w, d=s.d;
    slab(out, [[-w/2,-d/2],[w/2,-d/2],[w/2,d/2],[-w/2,d/2]], 0.012,'cloth',-1.9);
    slab(out, [[-w/2+0.14,-d/2+0.14],[w/2-0.14,-d/2+0.14],[w/2-0.14,d/2-0.14],[-w/2+0.14,d/2-0.14]], 0.016,'cloth2',-1.7);
    slab(out, [[-w/2+0.26,-d/2+0.26],[w/2-0.26,-d/2+0.26],[w/2-0.26,d/2-0.26],[-w/2+0.26,d/2-0.26]], 0.02,'cloth',-1.6);
  }
  function fChalkMenu(out,s){
    const w=s.w, h=1.02, d=0.09;
    box(out, -w/2,w/2, -d/2,d/2, 1.14, 1.14+h,'wood',null,0.15);
    decalY(out, d/2, 1, -w/2+0.07, w/2-0.07, 1.21, 1.14+h-0.07,'chalkbd',0.0,null,true,0.08);
    const rnd=s.rnd;
    for(let k=0;k<5;k++){ const zz=1.14+h-0.22-k*0.14;
      decalY(out, d/2, 1, -w/2+0.14, -w/2+0.14+(w-0.34)*(0.4+rnd()*0.5), zz, zz+0.035,'trim',0.75,null,true,0.10); }
  }
  function fPlantTub(out,s){
    const r=0.24, h=0.36;
    box(out, -r,r, -r*0.9,r*0.9, 0, h,'crate', plankTex(0.14), 0.05);
    box(out, -r*0.9,r*0.9, -r,r, 0, h,'crate', plankTex(0.14), 0.05);
    for(const zz of [h*0.28, h*0.76]) decalY(out, r*0.9,1, -r*0.8,r*0.8, zz-0.025, zz+0.025,'iron',0.3,null,true,0.06);
    slab(out, [[-r*0.8,-r*0.72],[r*0.8,-r*0.72],[r*0.8,r*0.72],[-r*0.8,r*0.72]], h+0.01,'soil',-0.5);
    for(let i=0;i<8;i++){ const a=i*0.9, rr=r*0.62*(0.35+((i%3)*0.3)), px=Math.cos(a)*rr, py=Math.sin(a)*rr, hh=0.20+((i%4)*0.08);
      box(out, px-0.055,px+0.055, py-0.055,py+0.055, h, h+hh,'leaf',null,0.1); }
  }
  function fStockShelf(out,s){
    const w=s.w, d=s.d, h=2.06;
    for(const xv of [-w/2+0.05, w/2-0.05]) for(const yv of [-d/2+0.05, d/2-0.05])
      box(out, xv-0.05,xv+0.05, yv-0.05,yv+0.05, 0, h,'wood',null,0.1);
    for(let i=0;i<4;i++){ const zz=0.34+i*((h-0.44)/4);
      box(out, -w/2+0.04, w/2-0.04, -d/2+0.02, d/2-0.02, zz-0.045, zz,'wood', plankTex(0.2), 0.3);
      waresRow(out, -w/2+0.08, w/2-0.08, 0, d-0.12, zz, ((h-0.44)/4)-0.10, s, null); }
  }

  const FIXTURES = {
    counter:    { label:'Shop counter',  cat:'service',  foot:[2.8,0.70], builtin:true,  tall:false, build:fCounter },
    counterShort:{label:'Counter · short',cat:'service', foot:[1.5,0.70], builtin:true,  tall:false, build:fCounter },
    displayCase:{ label:'Display case',  cat:'service',  foot:[1.9,0.72], builtin:true,  tall:false, build:fDisplayCase },
    coldTable:  { label:'Iced fish table',cat:'cold',    foot:[2.2,0.90], builtin:true,  tall:false, build:fColdTable },
    bar:        { label:'Bar counter',   cat:'service',  foot:[3.2,0.82], builtin:true,  tall:false, build:fBar },
    backBar:    { label:'Back bar',      cat:'service',  foot:[2.8,0.52], builtin:true,  tall:true,  build:fBackBar },
    pass:       { label:'Kitchen pass',  cat:'kitchen',  foot:[2.0,0.60], builtin:true,  tall:true,  build:fPass },
    wicket:     { label:'Post wicket',   cat:'service',  foot:[2.2,0.46], builtin:true,  tall:true,  build:fWicket },
    wallShelf:  { label:'Wall shelving', cat:'shelving', foot:[2.0,0.34], builtin:true,  tall:true,  build:fWallShelf },
    gondola:    { label:'Gondola aisle', cat:'shelving', foot:[2.0,0.72], builtin:true,  tall:true,  build:fGondola },
    stockShelf: { label:'Stock rack',    cat:'shelving', foot:[1.8,0.56], builtin:true,  tall:true,  build:fStockShelf },
    binRack:    { label:'Bin rack',      cat:'shelving', foot:[1.6,0.50], builtin:true,  tall:true,  build:fBinRack },
    pigeonholes:{ label:'Sorting wall',  cat:'shelving', foot:[1.8,0.36], builtin:true,  tall:true,  build:fPigeonholes },
    ropeRack:   { label:'Rope & chain',  cat:'shelving', foot:[1.6,0.42], builtin:true,  tall:true,  build:fRopeRack },
    larder:     { label:'Larder press',  cat:'shelving', foot:[1.0,0.55], builtin:true,  tall:true,  build:fLarder },
    iceChest:   { label:'Ice chest',     cat:'cold',     foot:[1.3,0.70], builtin:true,  tall:false, build:fIceChest },
    ovenBank:   { label:'Bake oven',     cat:'kitchen',  foot:[1.5,0.80], builtin:true,  tall:true,  build:fOvenBank },
    prepTable:  { label:'Prep table',    cat:'kitchen',  foot:[1.8,0.70], builtin:true,  tall:false, build:fPrepTable },
    sinkRun:    { label:'Sink run',      cat:'kitchen',  foot:[1.5,0.65], builtin:true,  tall:false, build:fSinkRun },
    table:      { label:'Table',         cat:'seating',  foot:[1.3,0.90], builtin:false, tall:false, build:fTable },
    roundTable: { label:'Round table',   cat:'seating',  foot:[1.0,1.00], builtin:false, tall:false, build:fRoundTable },
    chair:      { label:'Chair',         cat:'seating',  foot:[0.46,0.46],builtin:false, tall:false, build:fChair },
    stool:      { label:'Bar stool',     cat:'seating',  foot:[0.42,0.42],builtin:false, tall:false, build:fStool },
    booth:      { label:'Booth',         cat:'seating',  foot:[1.7,1.60], builtin:false, tall:true,  build:fBooth },
    bench:      { label:'Bench',         cat:'seating',  foot:[1.6,0.40], builtin:false, tall:false, build:fBench },
    crateStack: { label:'Crate stack',   cat:'stock',    foot:[0.60,0.60],builtin:false, tall:false, build:fCrateStack },
    barrelStack:{ label:'Barrels',       cat:'stock',    foot:[1.3,0.64], builtin:false, tall:false, build:fBarrelStack },
    sackPile:   { label:'Sacks',         cat:'stock',    foot:[1.0,0.44], builtin:false, tall:false, build:fSackPile },
    potbelly:   { label:'Pot-belly stove',cat:'decor',   foot:[0.90,0.62],builtin:false, tall:true,  build:fPotbelly },
    rug:        { label:'Rug',           cat:'decor',    foot:[2.2,1.30], builtin:false, tall:false, build:fRug },
    chalkMenu:  { label:'Chalk menu',    cat:'decor',    foot:[1.2,0.12], builtin:false, tall:true,  build:fChalkMenu },
    plantTub:   { label:'Plant tub',     cat:'decor',    foot:[0.50,0.50],builtin:false, tall:false, build:fPlantTub },
  };
  function list(){ return Object.keys(FIXTURES); }
  function fixtureFoot(name, rot){ const P=FIXTURES[name]; if(!P) return {w:0.6,d:0.6};
    return (rot&1) ? { w:P.foot[1], d:P.foot[0] } : { w:P.foot[0], d:P.foot[1] }; }

  // ---- DEFAULT LAYOUTS -----------------------------------------------------
  // fx/fy are fractions of the inner half-extent (-1 .. 1); rot in 90deg steps (0 = facing +Y).
  const LAYOUTS = {
    'generalStore/salesFloor':[
      {k:'counter',fx:0.30,fy:-0.55,rot:0},{k:'counterShort',fx:0.86,fy:-0.10,rot:3},
      {k:'wallShelf',fx:-0.62,fy:-0.92,rot:0},{k:'wallShelf',fx:0.30,fy:-0.92,rot:0},
      {k:'wallShelf',fx:-0.92,fy:-0.30,rot:1},{k:'wallShelf',fx:-0.92,fy:0.42,rot:1},
      {k:'gondola',fx:-0.28,fy:0.06,rot:0},{k:'gondola',fx:-0.28,fy:0.52,rot:0},
      {k:'potbelly',fx:0.62,fy:0.34,rot:0},{k:'barrelStack',fx:0.86,fy:0.76,rot:3},
      {k:'sackPile',fx:-0.86,fy:0.86,rot:0},{k:'crateStack',fx:0.10,fy:0.86,rot:0},
    ],
    'generalStore/stockroom':[
      {k:'stockShelf',fx:-0.90,fy:-0.60,rot:1},{k:'stockShelf',fx:-0.90,fy:0.10,rot:1},
      {k:'stockShelf',fx:0.90,fy:-0.60,rot:3},{k:'stockShelf',fx:0.90,fy:0.10,rot:3},
      {k:'crateStack',fx:-0.20,fy:-0.80,rot:0},{k:'crateStack',fx:0.24,fy:-0.80,rot:0},
      {k:'barrelStack',fx:0.0,fy:0.24,rot:0},{k:'sackPile',fx:-0.40,fy:0.62,rot:0},
      {k:'prepTable',fx:0.34,fy:0.70,rot:2},
    ],
    'fishMarket/counter':[
      {k:'coldTable',fx:-0.30,fy:0.20,rot:2},{k:'coldTable',fx:0.44,fy:0.20,rot:2},
      {k:'counter',fx:0.06,fy:-0.42,rot:0},
      {k:'iceChest',fx:-0.84,fy:-0.62,rot:1},{k:'prepTable',fx:0.78,fy:-0.66,rot:3},
      {k:'wallShelf',fx:-0.30,fy:-0.94,rot:0},{k:'sinkRun',fx:0.52,fy:-0.94,rot:0},
      {k:'crateStack',fx:-0.86,fy:0.72,rot:0},{k:'crateStack',fx:0.86,fy:0.78,rot:0},
      {k:'chalkMenu',fx:-0.70,fy:0.96,rot:2},
    ],
    'fishMarket/cutting':[
      {k:'prepTable',fx:-0.40,fy:-0.30,rot:0},{k:'prepTable',fx:0.40,fy:-0.30,rot:0},
      {k:'sinkRun',fx:-0.60,fy:-0.92,rot:0},{k:'iceChest',fx:0.56,fy:-0.92,rot:0},
      {k:'wallShelf',fx:-0.92,fy:0.10,rot:1},{k:'stockShelf',fx:0.92,fy:0.20,rot:3},
      {k:'crateStack',fx:0.0,fy:0.70,rot:0},{k:'crateStack',fx:0.44,fy:0.76,rot:0},
    ],
    'fishMarket/ice':[
      {k:'iceChest',fx:-0.88,fy:-0.40,rot:1},{k:'iceChest',fx:-0.88,fy:0.30,rot:1},
      {k:'iceChest',fx:0.88,fy:-0.40,rot:3},{k:'iceChest',fx:0.88,fy:0.30,rot:3},
      {k:'crateStack',fx:0.0,fy:-0.55,rot:0},{k:'crateStack',fx:0.0,fy:0.30,rot:0},
      {k:'stockShelf',fx:-0.34,fy:0.90,rot:2},
    ],
    'chandlery/floor':[
      {k:'counter',fx:0.44,fy:-0.50,rot:0},
      {k:'binRack',fx:-0.86,fy:-0.62,rot:1},{k:'binRack',fx:-0.86,fy:0.02,rot:1},
      {k:'ropeRack',fx:-0.40,fy:-0.94,rot:0},{k:'ropeRack',fx:0.30,fy:-0.94,rot:0},
      {k:'stockShelf',fx:0.90,fy:0.24,rot:3},{k:'gondola',fx:-0.10,fy:0.24,rot:0},
      {k:'barrelStack',fx:-0.80,fy:0.80,rot:0},{k:'crateStack',fx:0.30,fy:0.82,rot:0},
      {k:'potbelly',fx:0.80,fy:0.82,rot:0},
    ],
    'chandlery/loft':[
      {k:'ropeRack',fx:-0.88,fy:-0.40,rot:1},{k:'ropeRack',fx:-0.88,fy:0.30,rot:1},
      {k:'stockShelf',fx:0.88,fy:-0.40,rot:3},{k:'stockShelf',fx:0.88,fy:0.30,rot:3},
      {k:'crateStack',fx:0.0,fy:-0.60,rot:0},{k:'barrelStack',fx:0.0,fy:0.30,rot:0},
      {k:'sackPile',fx:-0.36,fy:0.78,rot:0},
    ],
    'chandlery/stockroom':[
      {k:'stockShelf',fx:-0.90,fy:-0.50,rot:1},{k:'stockShelf',fx:-0.90,fy:0.20,rot:1},
      {k:'stockShelf',fx:0.90,fy:-0.50,rot:3},{k:'ropeRack',fx:0.90,fy:0.24,rot:3},
      {k:'barrelStack',fx:-0.20,fy:-0.80,rot:0},{k:'crateStack',fx:0.26,fy:-0.80,rot:0},
      {k:'sackPile',fx:0.0,fy:0.40,rot:0},{k:'crateStack',fx:0.46,fy:0.76,rot:0},
    ],
    'giftShop/stockroom':[
      {k:'stockShelf',fx:-0.90,fy:-0.50,rot:1},{k:'stockShelf',fx:-0.90,fy:0.24,rot:1},
      {k:'stockShelf',fx:0.90,fy:-0.20,rot:3},
      {k:'crateStack',fx:-0.10,fy:-0.78,rot:0},{k:'crateStack',fx:0.34,fy:-0.78,rot:0},
      {k:'prepTable',fx:0.20,fy:0.34,rot:0},{k:'sackPile',fx:-0.40,fy:0.74,rot:0},
    ],
    'restaurant/stock':[
      {k:'stockShelf',fx:-0.90,fy:-0.40,rot:1},{k:'stockShelf',fx:-0.90,fy:0.34,rot:1},
      {k:'stockShelf',fx:0.90,fy:-0.40,rot:3},{k:'iceChest',fx:0.86,fy:0.44,rot:3},
      {k:'barrelStack',fx:0.0,fy:-0.72,rot:0},{k:'crateStack',fx:0.0,fy:0.10,rot:0},
      {k:'sackPile',fx:-0.32,fy:0.78,rot:0},
    ],
    'tavern/kitchen':[
      {k:'ovenBank',fx:-0.36,fy:-0.90,rot:0},{k:'sinkRun',fx:0.52,fy:-0.90,rot:0},
      {k:'prepTable',fx:-0.24,fy:-0.10,rot:0},{k:'prepTable',fx:0.44,fy:-0.10,rot:0},
      {k:'wallShelf',fx:-0.90,fy:0.18,rot:1},{k:'stockShelf',fx:0.90,fy:0.24,rot:3},
      {k:'pass',fx:-0.10,fy:0.94,rot:0},{k:'crateStack',fx:0.48,fy:0.78,rot:0},
      {k:'barrelStack',fx:-0.52,fy:0.74,rot:0},
    ],
    'bakery/front':[
      {k:'displayCase',fx:-0.34,fy:-0.32,rot:0},{k:'displayCase',fx:0.34,fy:-0.32,rot:0},
      {k:'counterShort',fx:0.86,fy:-0.36,rot:3},
      {k:'wallShelf',fx:-0.42,fy:-0.94,rot:0},{k:'wallShelf',fx:0.36,fy:-0.94,rot:0},
      {k:'roundTable',fx:-0.58,fy:0.52,rot:0},{k:'chair',fx:-0.58,fy:0.16,rot:0},{k:'chair',fx:-0.58,fy:0.88,rot:2},
      {k:'roundTable',fx:0.28,fy:0.56,rot:0},{k:'chair',fx:0.28,fy:0.20,rot:0},{k:'chair',fx:0.28,fy:0.92,rot:2},
      {k:'chalkMenu',fx:0.90,fy:0.30,rot:3},{k:'plantTub',fx:0.92,fy:0.90,rot:0},
    ],
    'bakery/bakehouse':[
      {k:'ovenBank',fx:-0.44,fy:-0.90,rot:0},{k:'ovenBank',fx:0.34,fy:-0.90,rot:0},
      {k:'prepTable',fx:-0.30,fy:-0.10,rot:0},{k:'prepTable',fx:0.42,fy:-0.10,rot:0},
      {k:'sinkRun',fx:-0.88,fy:0.24,rot:1},{k:'stockShelf',fx:0.90,fy:0.30,rot:3},
      {k:'sackPile',fx:-0.42,fy:0.72,rot:0},{k:'crateStack',fx:0.20,fy:0.78,rot:0},
    ],
    'restaurant/dining':[
      {k:'pass',fx:-0.14,fy:-0.94,rot:0},{k:'counterShort',fx:0.72,fy:-0.90,rot:0},
      {k:'booth',fx:-0.66,fy:-0.34,rot:0},{k:'booth',fx:-0.66,fy:0.30,rot:0},
      {k:'table',fx:0.34,fy:-0.36,rot:0},{k:'chair',fx:0.34,fy:-0.66,rot:0},{k:'chair',fx:0.34,fy:-0.06,rot:2},
      {k:'table',fx:0.34,fy:0.26,rot:0},{k:'chair',fx:0.34,fy:-0.04,rot:0},{k:'chair',fx:0.34,fy:0.56,rot:2},
      {k:'table',fx:0.34,fy:0.86,rot:0},{k:'chair',fx:0.34,fy:0.56,rot:0},
      {k:'roundTable',fx:-0.60,fy:0.86,rot:0},{k:'chair',fx:-0.88,fy:0.86,rot:1},
      {k:'plantTub',fx:0.94,fy:0.94,rot:0},
    ],
    'restaurant/bar':[
      {k:'backBar',fx:-0.10,fy:-0.94,rot:0},{k:'bar',fx:-0.10,fy:-0.42,rot:0},
      {k:'stool',fx:-0.62,fy:-0.10,rot:0},{k:'stool',fx:-0.30,fy:-0.10,rot:0},
      {k:'stool',fx:0.02,fy:-0.10,rot:0},{k:'stool',fx:0.34,fy:-0.10,rot:0},
      {k:'booth',fx:-0.62,fy:0.52,rot:0},{k:'booth',fx:0.34,fy:0.52,rot:0},
      {k:'wallShelf',fx:0.90,fy:-0.60,rot:3},{k:'plantTub',fx:0.94,fy:0.96,rot:0},
    ],
    'restaurant/kitchen':[
      {k:'pass',fx:0.10,fy:0.94,rot:2},
      {k:'ovenBank',fx:-0.48,fy:-0.92,rot:0},{k:'ovenBank',fx:0.30,fy:-0.92,rot:0},
      {k:'prepTable',fx:-0.34,fy:-0.24,rot:0},{k:'prepTable',fx:0.40,fy:-0.24,rot:0},
      {k:'sinkRun',fx:-0.88,fy:0.30,rot:1},{k:'stockShelf',fx:0.90,fy:0.34,rot:3},
      {k:'iceChest',fx:-0.40,fy:0.44,rot:0},{k:'crateStack',fx:0.22,fy:0.50,rot:0},
    ],
    'tavern/barroom':[
      {k:'backBar',fx:-0.24,fy:-0.94,rot:0},{k:'bar',fx:-0.24,fy:-0.44,rot:0},
      {k:'stool',fx:-0.74,fy:-0.12,rot:0},{k:'stool',fx:-0.44,fy:-0.12,rot:0},
      {k:'stool',fx:-0.14,fy:-0.12,rot:0},{k:'stool',fx:0.16,fy:-0.12,rot:0},
      {k:'table',fx:0.62,fy:-0.52,rot:1},{k:'chair',fx:0.62,fy:-0.86,rot:0},
      {k:'roundTable',fx:-0.58,fy:0.44,rot:0},{k:'chair',fx:-0.86,fy:0.44,rot:1},{k:'chair',fx:-0.30,fy:0.44,rot:3},
      {k:'roundTable',fx:0.26,fy:0.52,rot:0},{k:'chair',fx:0.26,fy:0.86,rot:2},{k:'chair',fx:0.54,fy:0.52,rot:3},
      {k:'potbelly',fx:0.86,fy:0.20,rot:0},{k:'bench',fx:0.10,fy:0.94,rot:2},
    ],
    'tavern/snug':[
      {k:'booth',fx:-0.56,fy:-0.44,rot:0},{k:'booth',fx:0.44,fy:-0.44,rot:0},
      {k:'roundTable',fx:-0.30,fy:0.42,rot:0},{k:'chair',fx:-0.62,fy:0.42,rot:1},{k:'chair',fx:0.02,fy:0.42,rot:3},
      {k:'potbelly',fx:0.72,fy:0.30,rot:0},{k:'bench',fx:-0.20,fy:0.94,rot:2},
      {k:'wallShelf',fx:0.90,fy:-0.62,rot:3},{k:'rug',fx:-0.20,fy:0.42,rot:0},
    ],
    'postOffice/lobby':[
      {k:'wicket',fx:-0.16,fy:-0.94,rot:0},{k:'counterShort',fx:0.82,fy:-0.92,rot:0},
      {k:'bench',fx:-0.84,fy:0.10,rot:1},{k:'bench',fx:0.84,fy:0.10,rot:3},
      {k:'table',fx:-0.10,fy:0.10,rot:0},
      {k:'chalkMenu',fx:-0.90,fy:-0.44,rot:1},{k:'potbelly',fx:0.80,fy:0.72,rot:0},
      {k:'plantTub',fx:-0.86,fy:0.86,rot:0},
    ],
    'postOffice/sorting':[
      {k:'pigeonholes',fx:-0.60,fy:-0.94,rot:0},{k:'pigeonholes',fx:0.24,fy:-0.94,rot:0},
      {k:'pigeonholes',fx:-0.92,fy:-0.20,rot:1},
      {k:'prepTable',fx:-0.16,fy:-0.20,rot:0},{k:'prepTable',fx:0.52,fy:-0.20,rot:0},
      {k:'stockShelf',fx:0.90,fy:0.32,rot:3},{k:'crateStack',fx:-0.40,fy:0.60,rot:0},
      {k:'sackPile',fx:0.16,fy:0.66,rot:0},
    ],
    'takeoutStand/servery':[
      {k:'counter',fx:0.0,fy:0.72,rot:2},{k:'prepTable',fx:-0.32,fy:-0.44,rot:0},
      {k:'iceChest',fx:0.60,fy:-0.50,rot:0},{k:'wallShelf',fx:-0.24,fy:-0.96,rot:0},
      {k:'crateStack',fx:0.86,fy:0.20,rot:0},
    ],
    'giftShop/floor':[
      {k:'counter',fx:0.40,fy:-0.56,rot:0},
      {k:'wallShelf',fx:-0.48,fy:-0.94,rot:0},{k:'wallShelf',fx:-0.92,fy:-0.30,rot:1},
      {k:'wallShelf',fx:-0.92,fy:0.40,rot:1},{k:'displayCase',fx:-0.14,fy:0.10,rot:0},
      {k:'gondola',fx:0.34,fy:0.48,rot:1},{k:'plantTub',fx:0.90,fy:0.88,rot:0},
      {k:'rug',fx:-0.20,fy:0.72,rot:0},{k:'crateStack',fx:-0.86,fy:0.90,rot:0},
    ],
    'flat':[
      {k:'table',fx:-0.30,fy:-0.30,rot:0},{k:'chair',fx:-0.30,fy:-0.66,rot:0},{k:'chair',fx:-0.30,fy:0.04,rot:2},
      {k:'larder',fx:-0.88,fy:-0.86,rot:0},{k:'sinkRun',fx:0.24,fy:-0.92,rot:0},
      {k:'potbelly',fx:0.84,fy:-0.72,rot:0},{k:'bench',fx:0.52,fy:0.30,rot:0},
      {k:'rug',fx:-0.20,fy:0.44,rot:0},{k:'crateStack',fx:0.86,fy:0.86,rot:0},
    ],
  };

  // ---- roofline (the shell's own roof, as this storey's ceiling) ------------
  function roofProfile(shape, hw, k, rise){
    switch(shape){
      case 'shed':    return [[-hw,k],[hw,k+rise*1.05]];
      case 'gambrel': { const brk=hw*0.62; return [[-hw,k],[-brk,k+rise*0.66],[0,k+rise+0.22],[brk,k+rise*0.66],[hw,k]]; }
      default:        return [[-hw,k],[0,k+rise],[hw,k]];
    }
  }
  function profZ(prof,x){
    for(let i=0;i+1<prof.length;i++){ const a=prof[i], b=prof[i+1];
      if(x>=a[0]-1e-6 && x<=b[0]+1e-6) return a[1]+(b[1]-a[1])*((x-a[0])/((b[0]-a[0])||1)); }
    return x<prof[0][0]?prof[0][1]:prof[prof.length-1][1];
  }
  function ridgeX(prof){ let bx=prof[0][0], bz=prof[0][1]; for(const p of prof) if(p[1]>bz){ bz=p[1]; bx=p[0]; } return bx; }

  // ---- resolve -------------------------------------------------------------
  // Ask shopBuildingRig for this room's rect when it is on the page. Re-entrancy guard: that rig
  // resolves its plan through dims() back into here, and a nested lookup would spin forever.
  let _planBusy=false;
  function planRoom(type, room, size){
    const SB = root.ShopBuilding;
    if(_planBusy || !room || !SB || !SB.roomBox) return null;
    _planBusy=true;
    try{ return SB.roomBox(type, room, size); }catch(e){ return null; } finally { _planBusy=false; }
  }

  function resolve(opts){
    opts=opts||{};
    const tk=opts.type||'generalStore', T=TYPES[tk]||TYPES.generalStore;
    const g=(k,d)=> opts[k]!=null ? opts[k] : (T[k]!=null ? T[k] : d);
    const rooms=ROOMS[tk]||['floor'];
    const named = !!(opts.room && rooms.indexOf(opts.room)>=0);
    const room = named ? opts.room : rooms[0];
    const size = opts.size!=null?opts.size:T.size;
    const b={
      type:tk, label:T.label, ext:T.ext, room, size,
      shape: g('shape', T.shape), pitch: g('pitch', T.pitch),
      storey: opts.storey || (room==='loft'||room==='flat' ? 'upper' : 'ground'),
      floor: g('floor', T.floor), wall: g('wall', T.wall), paper: g('paper', T.paper),
      trimTone: opts.trimTone!==undefined?opts.trimTone:T.trimTone,
      wainscot: g('wainscot', T.wainscot),
      windows: g('windows', T.windows), winD: g('winDensity', T.winD),
      storefront: g('storefront', T.storefront),
      counterTone: g('counterTone', T.counterTone),
      dividers: (opts.dividers!=null?opts.dividers:0)|0,
      beams: g('beams', T.beams),
      stock: opts.stock!=null?opts.stock:0.95,
      weather: opts.weather!=null?opts.weather:0.3,
      night: !!opts.night,
      seed: (opts.seed!=null?opts.seed:7)|0,
      shell: opts.shell!==false,
      goods: T.goods, wares: T.wares, flat: T.flat,
    };
    b.Wd = T.Wd[0] + size*T.Wd[1];
    b.Ln = T.Ln[0] + size*T.Ln[1];
    // SNAP THE SHELL TO THE CELL GRID. The plan rasterises the shell to Math.round(Wd/CELL) cells;
    // unless the shell IS a whole number of cells, the plan's envelope and the exterior wall stand a
    // few centimetres apart and rooms poke through the shell. Round once, here — shopBuildingRig and
    // shopfrontRig both read these numbers back, so all three draw the same rectangle.
    b.Wd = Math.max(4, Math.round(b.Wd/CELL))*CELL;
    b.Ln = Math.max(4, Math.round(b.Ln/CELL))*CELL;
    b.bWd = b.Wd; b.bLn = b.Ln;                    // the whole shell, before the room is cut out of it
    // THE ROOM'S OWN BOX. shopBuildingRig owns the plan; when it is loaded and the caller NAMES a
    // room we render that room at the size it has in the plan instead of the whole shell, so a
    // layout authored here drops straight into the building (same clear rect, same cell grid).
    // No room named means the whole shell — which is what shopBuildingRig itself asks for.
    b.rb = named ? planRoom(tk, b.room, size) : null;
    if(b.rb){ b.Wd = b.rb.Wd; b.Ln = b.rb.Ln; }
    b.street = b.rb ? !!b.rb.street : true;
    b.wallH = T.wallH[0] + size*T.wallH[1];
    b.fH = T.fH;
    b.shopH = Math.min(3.10, Math.max(2.55, b.wallH - b.fH - 0.55));
    if(T.flat) b.wallH = Math.max(b.wallH, b.fH + b.shopH + 0.34 + 2.35);   // == shopfrontRig
    b.rise = (b.Wd/2) * b.pitch;
    b.wt = 0.16; b.fZ = 0;
    const upper = b.storey==='upper';
    b.openRoof = upper;
    if(upper){
      b.storeyZ = b.fH + b.shopH + 0.34;
      b.plate = Math.max(0.85, Math.min(1.55, b.fH + b.wallH - b.storeyZ));
    } else {
      b.storeyZ = b.fH;
      b.plate = b.shopH;
    }
    b.ceilZ = b.fZ + b.plate;
    b.prof = b.openRoof ? roofProfile(b.shape==='falseFront'?'gable':b.shape, b.Wd/2, b.ceilZ, b.rise*0.84) : null;
    b.peakZ = b.prof ? b.prof.reduce((m,p)=>Math.max(m,p[1]),0) : b.ceilZ;
    return b;
  }
  function dims(opts){ const b=resolve(opts||{});
    return { Wd:b.Wd, Ln:b.Ln, wallH:b.wallH, fH:b.fH, plate:b.plate, ceilZ:b.ceilZ, peakZ:b.peakZ,
      storeyZ:b.storeyZ, shopH:b.shopH, ext:b.ext, type:b.type, room:b.room, label:b.label,
      bWd:b.bWd, bLn:b.bLn, planned:!!b.rb, sides:b.rb?b.rb.sides:null, street:b.rb?!!b.rb.street:true,
      roomLabel:ROOM_LABEL[b.room]||b.room }; }

  // ---- floor cell grid + items --------------------------------------------
  const CELL = 0.5;
  function grid(opts){ const b=resolve(opts||{});
    // nx/ny come from the plan when there is one, so (type, room) yields the SAME grid in both rigs
    // and a fixture's {gx,gy} means the same cell here and in the building. Origin is re-centred:
    // the single-room sheet puts the room on the pivot, the building puts it where the plan says.
    if(b.rb) return { cell:CELL, nx:b.rb.nx, ny:b.rb.ny, ox:-b.rb.nx*CELL/2, oy:-b.rb.ny*CELL/2,
      Wd:b.Wd, Ln:b.Ln, wt:b.wt, room:b.room };
    const wIn=b.Wd-2*b.wt, lIn=b.Ln-2*b.wt;
    const nx=Math.max(2, Math.floor(wIn/CELL)), ny=Math.max(2, Math.floor(lIn/CELL));
    return { cell:CELL, nx, ny, ox:-nx*CELL/2, oy:-ny*CELL/2, Wd:b.Wd, Ln:b.Ln, wt:b.wt };
  }
  function cellWorld(G, gx, gy, cw, ch){
    return { x: G.ox + (gx + cw/2)*G.cell, y: G.oy + (gy + ch/2)*G.cell };
  }
  function itemCells(name, rot){ const f=fixtureFoot(name, rot);
    return { cw: Math.max(1, Math.round(f.w/CELL)), ch: Math.max(1, Math.round(f.d/CELL)) }; }
  function defaultItems(opts){
    const b=resolve(opts||{}), G=grid(opts||{});
    const key = (b.room==='flat') ? 'flat' : (b.type+'/'+b.room);
    const L = LAYOUTS[key] || LAYOUTS[b.type+'/'+(ROOMS[b.type]||['floor'])[0]] || [];
    const out=[];
    for(const it of L){
      const c=itemCells(it.k, it.rot);
      const wx = (it.fx) * ((G.nx*CELL)/2 - c.cw*CELL/2);
      const wy = (it.fy) * ((G.ny*CELL)/2 - c.ch*CELL/2);
      const gx = Math.max(0, Math.min(G.nx-c.cw, Math.round((wx - G.ox)/CELL - c.cw/2)));
      const gy = Math.max(0, Math.min(G.ny-c.ch, Math.round((wy - G.oy)/CELL - c.ch/2)));
      out.push({ k:it.k, gx, gy, rot:it.rot|0 });
    }
    return out;
  }

  // ---- materials ----------------------------------------------------------
  function makeMats(b){
    const wx=b.weather, night=b.night;
    const grime=(r)=>r.map(c=>mix(desat(c, wx*0.26),'#4a4034', wx*0.13));
    const warm=(r,k)=> night ? r.map(c=>mix(mix(c,'#c98b3f',0.12),'#241a10',0.20+(k||0))) : r;
    const t=(r,k)=>warm(grime(r),k);
    const paperRamp = Array.isArray(b.paper)?b.paper:(BODY[b.paper]||BODY.cream);
    const floorRamp = b.floor==='checker'?TILE : b.floor==='stoneFlag'?STONE
                    : b.floor==='painted'?CINDER : b.floor==='sawdust'?SACK : FLOORWOOD;
    const trimRamp = b.trimTone ? (BODY[b.trimTone]||TRIM) : TRIM;
    const paintRamp = BODY[b.counterTone]||BODY.blue;
    const rnd=mulberry32(6101 + b.seed*131);
    const gk=b.goods||['red','blue','cream','gold'];
    const goods={}; for(let i=1;i<=4;i++){ const k=gk[i-1]!=null?gk[i-1]:gk[(rnd()*gk.length)|0]; goods['goods'+i]={ramp:t(BODY[k]||BODY.cream)}; }
    return Object.assign({
      plaster:{ ramp:t(PLASTER) },
      paper:  { ramp:t(paperRamp) },
      wood:   { ramp:t(WOOD) },
      oak:    { ramp:t(OAK) },
      floor:  { ramp:t(floorRamp, 0.02) },
      trim:   { ramp:t(trimRamp) },
      paint:  { ramp:t(paintRamp) },
      stone:  { ramp:t(STONE) },
      brick:  { ramp:t(BRICK) },
      cavity: { ramp:warm(CAVITY) },
      cinder: { ramp:t(CINDER) },
      steel:  { ramp:t(STEEL) },
      iron:   { ramp:t(IRON) },
      brass:  { ramp:t(BRASS) },
      zinc:   { ramp:t(ZINC) },
      tin:    { ramp:t(ZINC) },
      worktop:{ ramp:t(b.wall==='tile'?MARBLE:SLATE) },
      marble: { ramp:t(MARBLE) },
      ceramic:{ ramp:t(CERAMIC) },
      crate:  { ramp:t(CRATE) },
      sack:   { ramp:t(SACK) },
      rope:   { ramp:t(['#6a5b3c','#7d6c48','#907e55','#a49163','#b7a472','#c9b783']) },
      leather:{ ramp:t(LEATHER) },
      cloth:  { ramp:t(BODY[b.counterTone]||BODY.red) },
      cloth2: { ramp:t(BODY.cream) },
      linen:  { ramp:t(['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec']) },
      crust:  { ramp:t(['#5e3f1c','#754f24','#8c632f','#a3793f','#b89152','#cbaa6d']) },
      chalkbd:{ ramp:t(CHALKBD) },
      mirror: { ramp: night?['#2a2620','#3b342a','#4d4436']:['#6e7d80','#8a999c','#a9b9bb'] },
      soil:   { ramp:t(['#221a12','#2d2318','#3a2d1f','#473827','#54432f','#614e38']) },
      leaf:   { ramp:t(['#22331f','#2e4429','#3c5734','#4c6b42','#5e8052','#729665']) },
      ice:    { ramp:t(ICE) },
      fishA:  { ramp:t(FISHA) },
      fishB:  { ramp:t(FISHB) },
      doordk: { ramp: night?DOORDK.map(c=>mix(c,'#3a2a14',0.25)):DOORDK },
      glass:  { ramp: night?GLASSN:GLASSD },
      glassHi:{ ramp:[ night?'#4a6a86':GLASS_HI ] },
      caseGlass:{ ramp: night?['#2c3a44','#3d4f5c','#546a78']:CASEG },
      glassSh:{ ramp: night?['#3d4f5c','#546a78','#6d8794']:CASEG },
      glassC: { ramp: night?['#3a4a52','#4c6069','#617781']:['#7f9ea3','#9db9bd','#c0d6d8'] },
      heat:   { ramp:['#8a3d10','#c26a18','#e8a333'] },
      fire:   { ramp:FIRE },
      dark:   { ramp:['#0e1013','#141719','#1c2023'] },
    }, goods);
  }
  const FLOOR_BIAS = { checker:-3.75, stoneFlag:-3.35, plank:-2.7, wideBoard:-2.7, painted:-3.1, sawdust:-3.0 };

  // ---- room shell ---------------------------------------------------------
  function keepWall(nx,ny,B){ return (nx*B.stt + ny*B.ct) >= -0.35; }

  function windowOn(out, axis, plane, nrm, c, z, ww, wh, style){
    const put=putOn(axis), st=WINSTYLES[style]||WINSTYLES.twoOverTwo, ct=0.09, topZ=z+wh;
    put(out,plane,nrm, c-ww/2-ct-0.05, c+ww/2+ct+0.05, z-0.14, z-0.02,'trim',0.9,0.05);
    put(out,plane,nrm, c-ww/2-ct, c+ww/2+ct, z-0.02, topZ+ct,'trim',0.4,0.06);
    put(out,plane,nrm, c-ww/2-ct-0.04, c+ww/2+ct+0.04, topZ+ct, topZ+ct+0.08,'trim',0.8,0.05);
    put(out,plane,nrm, c-ww/2, c+ww/2, z, topZ,'glass',0.0,0.10);
    put(out,plane,nrm, c-ww/2+0.02, c-ww/2+ww*0.34, z+wh*0.5, topZ-0.05,'glassHi',0.0,0.12);
    const mb=0.055;
    if(st.v>0){ const cols=st.v+1; for(let i=1;i<=st.v;i++){ const cc=c-ww/2+ww*(i/cols);
      put(out,plane,nrm, cc-mb/2, cc+mb/2, z, topZ,'trim',0.55,0.13); } }
    for(const r of st.r){ const rz=z+wh*r; put(out,plane,nrm, c-ww/2, c+ww/2, rz-mb/2, rz+mb/2,'trim',0.55,0.13); }
  }
  function glazedBay(out, axis, plane, nrm, c, z0, w, h, cols, rows){
    const put=putOn(axis);
    put(out,plane,nrm, c-w/2-0.11, c+w/2+0.11, z0-0.11, z0+h+0.12,'wood',0.35,0.05);
    put(out,plane,nrm, c-w/2, c+w/2, z0, z0+h,'glass',0.0,0.10);
    put(out,plane,nrm, c-w/2+0.04, c-w/2+w*0.26, z0+h*0.42, z0+h-0.06,'glassHi',0.0,0.13);
    const mb=0.05;
    for(let i=1;i<cols;i++){ const cc=c-w/2+w*(i/cols); put(out,plane,nrm, cc-mb, cc+mb, z0, z0+h,'wood',0.5,0.14); }
    for(let j=1;j<rows;j++){ const zz=z0+h*(j/rows); put(out,plane,nrm, c-w/2, c+w/2, zz-mb, zz+mb,'wood',0.5,0.14); }
  }
  function doorwayOn(out, axis, plane, nrm, c, z0, dw, dh, slabDoor, glassTop){
    const put=putOn(axis), ct=0.11, topZ=z0+dh;
    put(out,plane,nrm, c-dw/2-ct, c+dw/2+ct, z0, topZ+ct,'trim',0.5,0.06);
    put(out,plane,nrm, c-dw/2-ct-0.04, c+dw/2+ct+0.04, topZ+ct, topZ+ct+0.08,'trim',0.8,0.05);
    if(slabDoor){
      put(out,plane,nrm, c-dw/2, c+dw/2, z0, topZ,'wood',0.05,0.10);
      if(glassTop){ const gz0=z0+dh*0.52, gz1=topZ-0.14;
        put(out,plane,nrm, c-dw/2+0.10, c+dw/2-0.10, gz0, gz1,'glass',0.0,0.12);
        put(out,plane,nrm, c-0.03, c+0.03, gz0, gz1,'trim',0.5,0.14); }
      for(const k of [0,1]){ const pz0=z0+0.12+k*(dh*0.19), pz1=pz0+dh*0.15;
        put(out,plane,nrm, c-dw/2+0.10, c+dw/2-0.10, pz0, pz1,'wood',-1.4,0.12); }
      put(out,plane,nrm, c+dw/2-0.20, c+dw/2-0.11, z0+dh*0.46, z0+dh*0.46+0.13,'brass',0.9,0.13);
    } else {
      put(out,plane,nrm, c-dw/2, c+dw/2, z0, topZ,'doordk',0.0,0.10);
      put(out,plane,nrm, c-dw/2, c-dw/2+0.06, z0, topZ,'trim',0.3,0.12);
      put(out,plane,nrm, c+dw/2-0.06, c+dw/2, z0, topZ,'doordk',-1.2,0.12);
    }
  }
  function finishBands(out, axis, plane, nrm, a0, a1, fZ, ceilZ, b){
    const put=putOn(axis), F0=b.wall, tex=wallTex(F0), bt=beadTex();
    const baseH=0.17, dadoTop=fZ+1.05;
    put(out,plane,nrm, a0,a1, fZ, fZ+baseH,'trim',0.35,0.045);
    let z0=fZ+baseH;
    if(b.wainscot){
      const wm = F0==='panel' ? 'oak' : 'wood';
      put(out,plane,nrm, a0,a1, fZ+baseH, dadoTop, wm, 0.0, 0.05, F0==='panel'?wallTex('panel'):bt, false);
      put(out,plane,nrm, a0,a1, dadoTop, dadoTop+0.09,'trim',0.7,0.055);
      z0=dadoTop+0.09;
    }
    const crownZ=ceilZ-0.10;
    if(F0==='stud'){
      put(out,plane,nrm, a0,a1, z0, crownZ,'cavity',-0.2,0.05);
      put(out,plane,nrm, a0,a1, z0, z0+0.12,'wood',0.1,0.06);
      put(out,plane,nrm, a0,a1, crownZ-0.14, crownZ,'wood',0.1,0.06);
      for(let a=a0+0.18; a<a1-0.05; a+=0.42) put(out,plane,nrm, a-0.045,a+0.045, z0+0.1, crownZ-0.12,'wood',0.2,0.07);
    } else {
      const mat = F0==='board'?'wood' : F0==='brick'?'brick' : F0==='block'?'cinder'
                : F0==='tile'?'trim' : 'paper';
      if(F0==='tile'){                                   // glazed tile dado + painted wall above
        const tileTop=Math.min(fZ+1.85, crownZ-0.3);
        put(out,plane,nrm, a0,a1, z0, tileTop,'trim',0.15,0.05, wallTex('tile'), false);
        put(out,plane,nrm, a0,a1, tileTop, tileTop+0.07,'trim',0.75,0.055);
        put(out,plane,nrm, a0,a1, tileTop+0.07, crownZ,'paper',0.0,0.05, wallTex('plaster'), false);
      } else {
        put(out,plane,nrm, a0,a1, z0, crownZ, mat, 0.0, 0.05, tex, false);
      }
    }
    put(out,plane,nrm, a0,a1, crownZ, ceilZ,'trim',0.6,0.05);
  }
  function gableField(out, b, yv, ny, mat, tex, noRake){
    const x0=-b.Wd/2+b.wt, x1=b.Wd/2-b.wt, k=b.ceilZ, prof=b.prof;
    const line=[[x0, profZ(prof,x0)]];
    for(const p of prof) if(p[0]>x0+0.03 && p[0]<x1-0.03) line.push([p[0],p[1]]);
    line.push([x1, profZ(prof,x1)]);
    const pts=[[x0,k],[x1,k]];
    for(let i=line.length-1;i>=0;i--) pts.push(line[i]);
    polyY(out, yv, ny, pts, mat, 0, 0.05, tex);
    if(noRake) return;
    for(let i=0;i+1<line.length;i++){ const a=line[i], c=line[i+1];
      polyY(out, yv+0.012*ny, ny, [[a[0],a[1]-0.17],[c[0],c[1]-0.17],[c[0],c[1]],[a[0],a[1]]],'trim',0.5,0.07,null); }
  }
  // the +Y wall IS the street elevation from inside: bulkhead, big glass, shop door
  function storefrontInside(out, b, plane, nrm){
    const hw=b.Wd/2, sf=b.storefront, fZ=b.fZ;
    const sillZ=fZ+0.62, headZ=Math.min(b.ceilZ-0.34, fZ+2.62);
    const doorH=Math.min(2.24, headZ-fZ-0.05);
    const bulk=(a,bx)=>{ decalY(out, plane, nrm, a, bx, fZ+0.17, sillZ,'wood',0.05, wallTex('board'), false, 0.045);
      decalY(out, plane, nrm, a, bx, sillZ, sillZ+0.10,'trim',0.7,null,true,0.06); };
    let doorX=0;
    if(sf==='bay'){
      const dw=1.12, panW=(b.Wd-dw-1.2)/2;
      for(const sgn of [-1,1]){ const c=sgn*(dw/2+0.30+panW/2);
        bulk(c-panW/2, c+panW/2);
        glazedBay(out,'y', plane, nrm, c, sillZ+0.14, panW, headZ-sillZ-0.28, 2, 1); }
      doorwayOn(out,'y', plane, nrm, 0, fZ, dw, doorH, true, true);
    } else if(sf==='plate'){
      doorX=-b.Wd*0.28;
      const gA=doorX+0.87, gB=hw-0.44;
      bulk(gA,gB);
      glazedBay(out,'y', plane, nrm, (gA+gB)/2, sillZ+0.12, gB-gA, headZ-sillZ-0.26, Math.max(2,Math.round((gB-gA)/1.35)), 1);
      doorwayOn(out,'y', plane, nrm, doorX, fZ, 1.14, doorH, true, true);
      if(doorX-0.87 > -hw+0.42){ bulk(-hw+0.42, doorX-0.87);
        glazedBay(out,'y', plane, nrm, (-hw+0.42+doorX-0.87)/2, sillZ+0.12, (doorX-0.87)-(-hw+0.42), headZ-sillZ-0.26, 1, 1); }
    } else if(sf==='smallPane'){
      doorX=b.Wd*0.26;
      const gA=-hw+0.42, gB=doorX-0.85;
      bulk(gA,gB);
      glazedBay(out,'y', plane, nrm, (gA+gB)/2, sillZ+0.14, gB-gA, Math.min(1.62, headZ-sillZ-0.30), Math.max(3,Math.round((gB-gA)/0.62)), 3);
      doorwayOn(out,'y', plane, nrm, doorX, fZ, 1.05, doorH, true, true);
    } else if(sf==='hatch'){
      doorX=-b.Wd*0.30;
      const hwid=Math.min(b.Wd*0.52, 2.9), hc=b.Wd*0.10, z0=fZ+0.98;
      decalY(out, plane, nrm, hc-hwid/2-0.12, hc+hwid/2+0.12, z0-0.12, z0+1.24,'trim',0.4,null,true,0.05);
      decalY(out, plane, nrm, hc-hwid/2, hc+hwid/2, z0, z0+1.12,'glass',-0.2,null,true,0.10);
      decalY(out, plane, nrm, hc-hwid/2, hc+hwid/2, z0-0.10, z0,'worktop',0.55,null,true,0.09);
      doorwayOn(out,'y', plane, nrm, doorX, fZ, 0.98, Math.min(2.1,doorH), true, false);
    } else {
      doorX=-b.Wd*0.24;
      doorwayOn(out,'y', plane, nrm, doorX, fZ, 1.08, doorH, true, true);
      const gA=doorX+0.88, gB=hw-0.46;
      if(gB-gA>0.9){ bulk(gA,gB);
        glazedBay(out,'y', plane, nrm, (gA+gB)/2, sillZ+0.18, gB-gA, Math.min(1.5, headZ-sillZ-0.38), Math.max(2,Math.round((gB-gA)/0.72)), 2); }
    }
    return doorX;
  }

  function placeItem(out, name, s, wx, wy, rot){
    const P=FIXTURES[name]; if(!P) return;
    const f=fixtureFoot(name, 0);
    const tmp=[]; P.build(tmp, Object.assign({}, s, { w:f.w, d:f.d }));
    const r=rot&3, c=[1,0,-1,0][r], sn=[0,1,0,-1][r];
    for(const fc of tmp){
      fc.v = fc.v.map(([x,y,z])=>[wx + x*c - y*sn, wy + x*sn + y*c, z]);
      out.push(fc);
    }
  }

  function build(b, B, items){
    const out=[];
    const hw=b.Wd/2, hl=b.Ln/2, wt=b.wt, fZ=b.fZ, ceilZ=b.ceilZ;
    const x0i=-hw+wt, x1i=hw-wt, y0i=-hl+wt, y1i=hl-wt;
    const ftex=floorTex(b.floor), fb=FLOOR_BIAS[b.floor]!=null?FLOOR_BIAS[b.floor]:-3;
    const upper=b.storey==='upper', prof=b.prof;
    const keepN=keepWall(0,1,B), keepS=keepWall(0,-1,B), keepE=keepWall(1,0,B), keepW=keepWall(-1,0,B);
    const EXT=(id)=> !b.rb || !!b.rb.sides[id];    // is this wall on the building envelope?
    const cMat = (b.wall==='plaster'||b.wall==='wainscot'||b.wall==='tile') ? 'plaster' : (b.wall==='block'?'cinder':'wood');
    const cTex = cMat==='plaster' ? wallTex('plaster') : cMat==='cinder' ? wallTex('block') : wallTex('board');

    CUR_LAYER='floor';
    if(b.shell) out.push(F([[-hw,-hl,fZ],[hw,-hl,fZ],[hw,hl,fZ],[-hw,hl,fZ]],'floor', fb, 0,
      [[0,0],[b.Wd,0],[b.Wd,b.Ln],[0,b.Ln]], ftex, b.floor==='checker'));

    const sillG=fZ+1.0, ww=0.82, wh=1.15;
    const nLong=Math.max(1, Math.round((b.Ln/2.8)*(0.5+b.winD)));
    const sides=[
      { id:'N', nx:0, ny:1,  axis:'y', plane:y1i, nrm:-1, a0:x0i, a1:x1i, cap:['y',y1i,hl]  },
      { id:'S', nx:0, ny:-1, axis:'y', plane:y0i, nrm:1,  a0:x0i, a1:x1i, cap:['y',-hl,y0i] },
      { id:'E', nx:1, ny:0,  axis:'x', plane:x1i, nrm:-1, a0:y0i, a1:y1i, cap:['x',x1i,hw]  },
      { id:'W', nx:-1,ny:0,  axis:'x', plane:x0i, nrm:1,  a0:y0i, a1:y1i, cap:['x',-hw,x0i] },
    ];
    for(const s of sides){
      if(!b.shell) break;
      if(!keepWall(s.nx,s.ny,B)) continue;
      const gable = s.axis==='y';
      CUR_LAYER='w'+s.id;
      if(!(b.openRoof && gable)){
        if(s.cap[0]==='y') slab(out, [[-hw,s.cap[1]],[hw,s.cap[1]],[hw,s.cap[2]],[-hw,s.cap[2]]], ceilZ,'plaster',0.55);
        else               slab(out, [[s.cap[1],-hl],[s.cap[2],-hl],[s.cap[2],hl],[s.cap[1],hl]], ceilZ,'plaster',0.55);
      }
      finishBands(out, s.axis, s.plane, s.nrm, s.a0, s.a1, fZ, ceilZ, b);
      if(b.openRoof && gable){
        const gm = b.wall==='board'||b.wall==='stud'?'wood' : b.wall==='block'?'cinder' : 'paper';
        gableField(out, b, s.plane, s.nrm, gm, wallTex(b.wall==='stud'?'board':b.wall));
      }
      // a wall the plan says is a PARTY wall carries no glazing — it gets the door to the next room
      if(s.axis==='x' && EXT(s.id) && !upper) for(let i=0;i<nLong;i++){ const c=y0i+(y1i-y0i)*((i+0.5)/nLong);
        windowOn(out,'x', s.plane, s.nrm, c, sillG, ww, wh, b.windows); }
      if(s.axis==='x' && EXT(s.id) && upper) for(let i=0;i<Math.max(1,nLong-1);i++){ const c=y0i+(y1i-y0i)*((i+0.5)/Math.max(1,nLong-1));
        if(ceilZ-fZ>1.5) windowOn(out,'x', s.plane, s.nrm, c, fZ+0.42, ww, Math.min(wh, ceilZ-fZ-0.62), b.windows); }
      if(s.axis==='x' && !EXT(s.id))
        doorwayOn(out,'x', s.plane, s.nrm, hl*0.30, fZ, 1.04, Math.min(2.10, ceilZ-0.2), true, false);
      if(gable){
        if(s.id==='N'){                                     // the STREET elevation, from inside
          if(!EXT('N')) doorwayOn(out,'y', s.plane, s.nrm, hw*0.16, fZ, 1.06, Math.min(2.12, ceilZ-0.2), true, false);
          else if(!upper && b.street) storefrontInside(out, b, s.plane, s.nrm);
          else { const nU=Math.max(2, Math.round(b.Wd/2.3));
            for(let i=0;i<nU;i++){ const c=x0i+(x1i-x0i)*((i+0.5)/nU);
              if(ceilZ-fZ>1.5) windowOn(out,'y', s.plane, s.nrm, c, fZ+0.42, ww, Math.min(wh, ceilZ-fZ-0.62), b.windows); } }
        } else {                                             // back elevation: back door + sash
          if(!EXT('S')) doorwayOn(out,'y', s.plane, s.nrm, -hw*0.16, fZ, 1.06, Math.min(2.12, ceilZ-0.2), true, false);
          else {
            if(!upper) doorwayOn(out,'y', s.plane, s.nrm, -hw*0.34, fZ, 1.02, Math.min(2.12, ceilZ-0.2), true, false);
            for(const c of [hw*0.34]) windowOn(out,'y', s.plane, s.nrm, c, sillG, ww, Math.min(wh, ceilZ-sillG-0.28), b.windows);
          }
        }
        if(b.openRoof && EXT(s.id)){
          const pz=ceilZ+(b.peakZ-ceilZ)*0.42;
          if(pz-ceilZ>0.5) windowOn(out,'y', s.plane, s.nrm, 0, pz-0.35, 0.72, 0.80, b.windows);
        }
      }
    }

    // ROOF UNDERSIDE for the flat / loft above the shop
    if(b.shell && b.openRoof && prof){
      CUR_LAYER='roof';
      const xR=ridgeX(prof), xLo=keepW?-hw:xR, xHi=keepE?hw:xR;
      const inset=(keepE&&keepW)?b.Ln*0.42:0;
      const yLo=keepS?-hl:y0i+inset, yHi=keepN?hl:y1i-inset;
      for(let i=0;i+1<prof.length;i++){
        let ax=prof[i][0], az=prof[i][1], bx=prof[i+1][0], bz=prof[i+1][1];
        if(bx<=xLo+0.01 || ax>=xHi-0.01) continue;
        if(ax<xLo){ az=profZ(prof,xLo); ax=xLo; }
        if(bx>xHi){ bz=profZ(prof,xHi); bx=xHi; }
        const L=Math.hypot(bx-ax, bz-az);
        out.push(F([[ax,yLo,az],[bx,yLo,bz],[bx,yHi,bz],[ax,yHi,az]], cMat, bz<az?0.30:-0.30, 0,
          [[0,0],[L,0],[L,yHi-yLo],[0,yHi-yLo]], cTex));
      }
      if(b.beams){
        CUR_LAYER='beam';
        const nR=Math.max(2, Math.round((yHi-yLo)/1.45));
        for(let r=0;r<=nR;r++){ const yy=yLo+(yHi-yLo)*(r/nR);
          for(let i=0;i+1<prof.length;i++){
            let ax=prof[i][0], az=prof[i][1], bx=prof[i+1][0], bz=prof[i+1][1];
            if(bx<=xLo+0.01 || ax>=xHi-0.01) continue;
            if(ax<xLo){ az=profZ(prof,xLo); ax=xLo; }
            if(bx>xHi){ bz=profZ(prof,xHi); bx=xHi; }
            out.push(F([[ax,yy-0.055,az-0.13],[bx,yy-0.055,bz-0.13],[bx,yy+0.055,bz-0.13],[ax,yy+0.055,az-0.13]],'wood',0.25,0.07));
            out.push(F([[ax,yy+0.055,az-0.13],[bx,yy+0.055,bz-0.13],[bx,yy+0.055,bz-0.02],[ax,yy+0.055,az-0.02]],'wood',-0.35,0.08));
          } }
        if(b.shape!=='shed'){ joist(out, xR-0.06, xR+0.06, yLo, yHi, b.peakZ-0.24, b.peakZ-0.03,'wood'); }
      }
    } else if(b.shell && b.beams){
      CUR_LAYER='beam';
      const bz=ceilZ-0.19, nb=Math.max(3, Math.round(b.Ln/1.9));
      for(let i=1;i<nb;i++){ const yy=-hl+b.Ln*(i/nb); joist(out, x0i,x1i, yy-0.065,yy+0.065, bz, ceilZ-0.03,'wood'); }
    }

    // INTERIOR DIVIDERS
    const nd=b.shell?Math.min(2, b.dividers|0):0, dvTop=ceilZ, dvDoor=Math.min(2.06, dvTop-0.14);
    for(let i=0;i<nd;i++){
      CUR_LAYER='dv'+i;
      const yy=y0i+(y1i-y0i)*((i+1)/(nd+1));
      const gapC=(i%2?-1:1)*hw*0.34, gapW=Math.min(1.15, b.Wd*0.30);
      for(const [sa,sb] of [[x0i, gapC-gapW/2],[gapC+gapW/2, x1i]]){ if(sb-sa<0.2) continue;
        box(out, sa,sb, yy-wt/2, yy+wt/2, fZ, dvTop,'plaster',null,0.0);
        finishBands(out,'y', yy-wt/2, -1, sa, sb, fZ, dvTop, b);
        finishBands(out,'y', yy+wt/2,  1, sa, sb, fZ, dvTop, b);
      }
      if(dvTop>dvDoor+0.2){ for(const sgn of [-1,1])
        decalY(out, yy+sgn*wt/2, sgn, gapC-gapW/2-0.1, gapC+gapW/2+0.1, dvDoor+0.1, dvDoor+0.27,'trim',0.6,null,true,0.06); }
    }

    // FIXTURES — each on its own layer, so the engine can drop or ghost any one of them
    const G=grid({type:b.type, room:b.room, size:b.size, storey:b.storey});
    const s0={ stock:b.stock, wares:b.wares, cloth:(b.type==='restaurant') };
    items.forEach((it, idx)=>{
      const P=FIXTURES[it.k]; if(!P) return;
      CUR_LAYER='it'+idx;
      const c=itemCells(it.k, it.rot), w=cellWorld(G, it.gx, it.gy, c.cw, c.ch);
      const s=Object.assign({}, s0, { rnd: mulberry32(3301 + b.seed*97 + idx*7919) });
      placeItem(out, it.k, s, w.x, w.y, it.rot);
    });

    CUR_LAYER='base';
    return out;
  }

  // ---- rasterizer --------------------------------------------------------
  function paint(faces, B, MATS){
    const W=VW, H=VH, N=W*H;
    const zbuf=new Float32Array(N).fill(Infinity), dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null), ibuf=new Int16Array(N), nbuf=new Array(N).fill(null), lbuf=new Array(N).fill(null);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce);
      if(sh<0 && (f.b<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx=sh*GAIN + BIAS + f.b;
      const M=MATS[f.mat]||MATS.plaster, ramp=M.ramp, off=M.off||0, tex=f.tex, uv=f.uv, flat=f.flat;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1],0,t,t+1);
      function fillTri(a,b,c, ia,ib,ic){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) return;
        const ua=uv?uv[ia]:null, ub=uv?uv[ib]:null, uc=uv?uv[ic]:null;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-f.db, i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat; lbuf[i]=f.layer;
            let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi+=tex(uu,vv); }
            let idx;
            if(flat){ idx=Math.round(fi)+off; }
            else { const base=Math.floor(fi); idx=base+((fi-base)>BAYER[x&3][y&3]?1:0)+off; }
            idx=Math.max(0,Math.min(ramp.length-1,idx));
            rbuf[i]=ramp; ibuf[i]=idx;
          }
        }
      }
    }
    return { rbuf, ibuf, nbuf, dep, lbuf };
  }
  function post(bufs, b, noKey){
    const W=VW, H=VH, { rbuf, ibuf, nbuf, dep }=bufs, N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j; out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; } } }
    const wx=b.weather;
    if(wx>0.02){ const rnd=mulberry32(913|((b.size*131)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='floor'||m==='plaster'||m==='paper'||m==='wood'||m==='oak'||m==='stone'||m==='brick'||m==='crate'||m==='sack') && rnd()<wx*0.06)
          out[i]=rbuf[i][Math.max(0,Math.min(rbuf[i].length-1,ibuf[i]-1))]; } }
    if(b.night){
      for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x;
        if(nbuf[i]==='fire'||nbuf[i]==='heat'){ for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,1]]){ const j=(y+dy)*W+(x+dx);
          if(out[j]&&nbuf[j]!=='fire'&&nbuf[j]!=='heat') out[j]=mix(out[j],'#f0b45a',0.22); } } }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; } }
    if(!noKey){ for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
      if(touch) out[i]=KEY; } }
    return out;
  }
  function toRGBA(cols){
    const W=VW, H=VH, rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; }
      const [r,g,bl]=hex2rgb(c); rgba[i*4]=r; rgba[i*4+1]=g; rgba[i*4+2]=bl; rgba[i*4+3]=255; }
    return rgba;
  }
  function addKeyline(rgba){
    const W=VW, H=VH, N=W*H, op=new Uint8Array(N), [kr,kg,kb]=hex2rgb(KEY);
    for(let i=0;i<N;i++) op[i]=rgba[i*4+3]!==0?1:0;
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(op[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&op[ny*W+nx]){ touch=true; break; } }
      if(touch){ const j=i*4; rgba[j]=kr; rgba[j+1]=kg; rgba[j+2]=kb; rgba[j+3]=255; } }
  }
  function layerKind(id){
    if(id==='floor') return 'floor';
    if(id==='roof' || id[0]==='w') return 'wall';
    if(id.slice(0,2)==='dv') return 'divider';
    if(id==='beam') return 'beam';
    return 'fixture';
  }
  // A layout authored against a different room (or a stale saved one) is clamped into this room's
  // grid rather than allowed to hang outside the walls.
  function itemsOf(b, opts){
    const raw = (opts && opts.items) ? opts.items : defaultItems(opts||{});
    const G = grid(opts||{});
    return raw.map(it=>{ const c=itemCells(it.k, it.rot);
      if(c.cw>G.nx || c.ch>G.ny) return null;
      return { k:it.k, rot:it.rot|0,
        gx: Math.max(0, Math.min(G.nx-c.cw, it.gx|0)),
        gy: Math.max(0, Math.min(G.ny-c.ch, it.gy|0)) };
    }).filter(Boolean);
  }

  function render(dir, opts){
    opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    resetView();
    const b=resolve(opts), B=camBasis({dir,elev:opts.elev}), MATS=makeMats(b);
    return toRGBA(post(paint(build(b,B,itemsOf(b,opts)), B, MATS), b));
  }
  // a single fixture as its own sprite (the loose props ship as individual sheets)
  function renderItem(name, dir, opts){
    opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    resetView();
    const b=resolve(opts), B=camBasis({dir,elev:opts.elev}), MATS=makeMats(b);
    const out=[]; CUR_LAYER='it0';
    placeItem(out, name, { stock:b.stock, wares:b.wares, cloth:(b.type==='restaurant'),
      rnd: mulberry32(3301 + b.seed*97) }, 0, 0, (opts.rot|0)||0);
    CUR_LAYER='base';
    return toRGBA(post(paint(out, B, MATS), b));
  }
  function renderLayers(dir, opts){
    opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    resetView();
    const b=resolve(opts), B=camBasis({dir,elev:opts.elev}), MATS=makeMats(b);
    const items=itemsOf(b,opts);
    const bufs=paint(build(b,B,items), B, MATS);
    const cols=post(bufs,b,true), lbuf=bufs.lbuf, groups={};
    for(let i=0;i<W*H;i++){ const l=lbuf[i]; if(cols[i]==null||l==null) continue; (groups[l]||(groups[l]=[])).push(i); }
    const rank=(id)=>{ const k=layerKind(id); return k==='floor'?0 : k==='fixture'?1 : k==='divider'?2 : k==='beam'?4 : 3; };
    const order=Object.keys(groups).sort((a,b2)=>rank(a)-rank(b2));
    const layers=order.map(id=>{ const rgba=new Uint8ClampedArray(W*H*4);
      for(const i of groups[id]){ const [r,g,bl]=hex2rgb(cols[i]), j=i*4; rgba[j]=r;rgba[j+1]=g;rgba[j+2]=bl;rgba[j+3]=255; }
      addKeyline(rgba);
      const m=/^it(\d+)$/.exec(id);
      return { id, kind:layerKind(id), item: m?items[+m[1]]:null, rgba }; });
    return { W, H, pivot:{x:cx,y:groundY}, layers, anchors:anchors(dir,opts) };
  }
  function ghost(rgba, keep, w, h){ keep=keep==null?0.18:keep;
    const gw=w||W, gh=h||H, out=new Uint8ClampedArray(rgba);
    for(let y=0;y<gh;y++) for(let x=0;x<gw;x++) if(BAYER[x&3][y&3] >= keep) out[(y*gw+x)*4+3]=0;
    return out; }

  function anchors(dir, opts){
    opts=opts||{}; resetView();
    const b=resolve(opts), B=camBasis({dir,elev:opts.elev});
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return {x:v.sx,y:v.sy}; };
    const hl=b.Ln/2, hw=b.Wd/2, wt=b.wt, x0i=-hw+wt, x1i=hw-wt, y0i=-hl+wt, y1i=hl-wt;
    const items=itemsOf(b,opts), G=grid(opts);
    const occ=[];
    const pushW=(id,ax,plane,a0,a1,wy)=>{
      const p0=ax==='y'?pj(a0,plane,b.fZ):pj(plane,a0,b.fZ);
      const p1=ax==='y'?pj(a1,plane,b.fZ):pj(plane,a1,b.fZ);
      const t=ax==='y'?pj((a0+a1)/2,plane,b.ceilZ):pj(plane,(a0+a1)/2,b.ceilZ);
      occ.push({id, kind:layerKind(id), base:[p0,p1], worldY:wy, topY:t.y}); };
    if(keepWall(0,1,B))  pushW('wN','y',y1i,-hw,hw, y1i);
    if(keepWall(0,-1,B)) pushW('wS','y',y0i,-hw,hw, y0i);
    if(keepWall(1,0,B))  pushW('wE','x',x1i,-hl,hl, 0);
    if(keepWall(-1,0,B)) pushW('wW','x',x0i,-hl,hl, 0);
    const nd=Math.min(2,b.dividers|0);
    for(let i=0;i<nd;i++){ const yy=y0i+(y1i-y0i)*((i+1)/(nd+1)); pushW('dv'+i,'y',yy,x0i,x1i, yy); }
    const fixtures=[], browse=[], stools=[];
    let till=null, pass=null, stove=null;
    items.forEach((it, idx)=>{
      const P=FIXTURES[it.k]; if(!P) return;
      const c=itemCells(it.k, it.rot), wc=cellWorld(G, it.gx, it.gy, c.cw, c.ch);
      const f=fixtureFoot(it.k, it.rot);
      const rec={ k:it.k, idx, x:wc.x, y:wc.y, rot:it.rot|0, cat:P.cat, builtin:!!P.builtin,
        px:pj(wc.x, wc.y, b.fZ), top:pj(wc.x, wc.y, b.fZ+(P.tall?1.9:0.9)) };
      fixtures.push(rec);
      // customer-side standing spot, 0.55 m off the fixture's front
      const dv=[[0,1],[1,0],[0,-1],[-1,0]][it.rot&3];
      const sx=wc.x + dv[0]*(f.d/2+0.55), sy=wc.y + dv[1]*(f.d/2+0.55);
      if(it.k==='counter'||it.k==='counterShort'||it.k==='wicket'||it.k==='displayCase'||it.k==='coldTable'){
        if(!till) till={ station:pj(wc.x - dv[0]*(f.d/2+0.45), wc.y - dv[1]*(f.d/2+0.45)), queue:pj(sx,sy) };
      }
      if(it.k==='pass') pass=pj(wc.x, wc.y, b.fZ+1.34);
      if(it.k==='potbelly'||it.k==='ovenBank') stove=pj(wc.x, wc.y, b.fZ+0.5);
      if(it.k==='stool') stools.push(pj(wc.x, wc.y, b.fZ+0.74));
      if(P.cat==='shelving') browse.push(pj(sx, sy, b.fZ));
      if(P.tall) occ.push({ id:'it'+idx, kind:'fixture', item:it.k,
        base:[pj(wc.x-f.w/2, wc.y, b.fZ), pj(wc.x+f.w/2, wc.y, b.fZ)], worldY:wc.y, topY:pj(wc.x, wc.y, b.fZ+1.9).y });
    });
    const doorX = b.storefront==='plate'?-b.Wd*0.28 : b.storefront==='smallPane'?b.Wd*0.26
                : b.storefront==='hatch'?-b.Wd*0.30 : b.storefront==='narrow'?-b.Wd*0.24 : 0;
    return {
      floor: pj(0,0,b.fZ),
      door:  pj(doorX, hl, b.fZ),
      backDoor: pj(-hw*0.34, -hl, b.fZ),
      till: till?till.station:null, queue: till?till.queue:null,
      browse, stools, pass, stove,
      lamps:[ pj(-hw*0.45, hl*0.35, b.ceilZ*0.82), pj(hw*0.45, -hl*0.25, b.ceilZ*0.82) ],
      occluders: occ, fixtures,
      Wd:b.Wd, Ln:b.Ln, fH:b.fH, storeyZ:b.storeyZ, plate:b.plate, peakZ:b.peakZ,
      type:b.type, room:b.room, ext:b.ext, storey:b.storey, shape:b.shape,
    };
  }
  function project(dir, p, elev){ resetView();
    const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.ShopInterior = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'], CELL,
    TYPES, ROOMS, ROOM_LABEL, FLOORS, WALLS, WINDOWS, SHAPES, STOREYS, FIXTURES, CATS, LAYOUTS,
    BODY, TRIM, WOOD, OAK, STONE, BRICK, TILE, STEEL, IRON, BRASS, ZINC, MARBLE, SLATE, CRATE, ICE, KEY,
    dims, grid, defaultItems, fixtureFoot, itemCells, cellWorld, list,
    render, renderItem, renderLayers, ghost, anchors, project,
    // INTERNALS — shopBuildingRig.js composes multi-room plans out of these exact builders, so a
    // plan bake is the same rasterizer, palette, texture and keyline as a single room. Not public API.
    _i:{ S, DEG, BODY, TRIM, PLASTER, WOOD, OAK, FLOORWOOD, STONE, BRICK, TILE, CAVITY, DOORDK,
      CINDER, STEEL, IRON, BRASS, ZINC, MARBLE, SLATE, CERAMIC, CRATE, SACK, LEATHER, CHALKBD, KEY,
      BAYER, WINSTYLES, FLOOR_BIAS,
      setView, resetView, camBasis, projVert, mulberry32, mix, desat,
      F, wall, slab, box, joist, decalY, decalX, putOn, polyY, setLayer:(l)=>{ CUR_LAYER=l; },
      floorTex, wallTex, beadTex, plankTex, hash2, ware, waresRow,
      keepWall, windowOn, doorwayOn, finishBands, gableField, storefrontInside,
      roofProfile, profZ, ridgeX, placeItem, makeMats, paint, post, toRGBA, addKeyline, resolve } };
})(typeof globalThis!=='undefined'?globalThis:window);
