/* Hidden Harbours — parametric ISO SHOPFRONT rig (ADR-0006 bake pipeline, SAME turntable + camera +
   shading as houseIsoRig.js / wharfBuildingRig.js / interiorIsoRig.js / the fleet). One parametric 3D
   commercial building baked to pixel sheets through the SHARED 3/4 camera: 45deg steps, elev 40deg
   default, flat-facet shading from the fixed upper-LEFT key, z-buffered, ordered dither, per-face uv
   texture, depth-edge darkening, 1px keyline, NO AA. 32 px = 1 m. All 8 facings from one model.

   THIS IS THE COMMERCIAL FAMILY — the third building rig beside houseIsoRig (dwellings) and
   wharfBuildingRig (sheds/plants). Nine businesses, each seeding massing, cladding, storefront type,
   awning, signage + the street furniture that belongs to that trade.

   REGISTRATION CONTRACT with shopInteriorRig.js — every number below is read from ONE formula, so a
   'bakery' room registers under a 'bakery' shell:
     footprint  Wd/Ln come from the TYPE's ranges;  wallH is grade -> EAVE;  fH is grade -> shop floor
     roofline   shape + pitch;  rise = (Wd/2)*pitch;  ridgeZ = wallH + rise
     door       shop door on the +Y street elevation at x = doorX (storefront-dependent)
     stack      bake-oven / kitchen flue on the -Y gable at x=0
     sash       upper-storey + long-wall sash ww 0.82 x wh 1.15, sill at its storey floor + 1.0
     grade      z 0 is the pavement; the shell's own floor plane sits at fH

   THE BUILDER SURFACE (every axis resolved per render, no re-modelling):
     type:      'generalStore'|'fishMarket'|'chandlery'|'bakery'|'restaurant'|'tavern'|'postOffice'|
                'takeoutStand'|'giftShop'   — seeds every axis below
     shape:     'gable'|'shed'|'gambrel'|'falseFront'   pitch:0..2   size:0..1
     siding:    'clapboard'|'shingle'|'boardBatten'|'corrugated'   body: BODY key   roof: ROOF key
     storefront:'bay'|'plate'|'smallPane'|'hatch'|'narrow'   winDensity:0..1  windows: sash style
     awning:    'none'|'straight'|'scallop'   awnExtend:0..1   awnCols: AWNINGS key
     fascia:bool (painted signboard band over the storefront)   bracket:bool (hanging bracket sign)
     sign:      'board'|'oval'|'shield'|'pennant'  signTone: BODY key   — ABSTRACT blanks, no lettering
     flat:bool (flat above the shop: upper sash + laundry line)   stall:bool (trestle + crates on the walk)
     patio:bool (tables + umbrellas)   sandwich:bool (chalkboard A-frame)   planters:bool
     load:bool (loading door on +X)   scale:bool (platform scale by the loading door)   stacks:0..2
     weather:0..1   night:bool (lit glass + lamp)   elev:30..50
   ANIM: shells are static; stack smoke, lit glass and the door lamp are runtime overlays —
     anchors(dir,opts) -> { door, sign, bracket, awning, stall, patio, stacks:[], lamp, ridge, Wd, Ln,
     fH, wallH, ext } in cell px.
   Exposes globalThis.Shopfront = { W,H,PX,DIRS,pivot,order,defaultElev, TYPES,SHAPES,SIDINGS,ROOFS,
     STOREFRONTS,SIGNS,AWNINGS,BODY,TRIM,WINDOWS,PRESETS, dims(opts), render(dir,opts),
     anchors(dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1320, H = 1180, cx = 660, groundY = 800;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;

  // ---- palettes, dark -> light (KTC master ramps, shared with the fleet / house / wharf) ----
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
  const TRIM   = ['#9aa09a','#b4b8b0','#ccd0c7','#e0e2da','#eef0e8','#f8f9f2'];
  const ROOFS  = {
    asphaltGrey:  ['#23262b','#2e333a','#3c424a','#4c535c','#5d6570','#6f7883'],
    asphaltBrown: ['#2a211a','#3a2e23','#4c3d2e','#5f4d3a','#736046','#877254'],
    metal:        ['#424d52','#556065','#6c7c81','#88999e','#a4babe','#c0d4d7'],
  };
  const ROOF_KEYS = ['asphaltGrey','asphaltBrown','metalSeam','corrugated','rusted'];
  const GALV   = BODY.galv;
  const RUST   = ['#3a1c10','#4f2614','#6a3620','#84462b','#9c5b3a','#b2724c'];
  const STEEL  = ['#2a2f33','#3c454b','#525c63','#6d777e','#889298','#a2acb1'];
  const IRON   = ['#141619','#1e2226','#2b3035','#3a4046','#4a5158','#5b636a'];
  const STONE  = ['#33343a','#42444b','#54575d','#666a70','#7a7e84','#8e9299'];
  const WOOD   = ['#4f3a24','#63492d','#785a39','#8f7049','#a6875d','#bd9f74'];
  const CRATE  = ['#6a4f2c','#7e5f36','#937143','#a98653','#bd9b67','#cfb180'];
  const SIGN   = ['#7d7566','#8f8878','#a89e88','#c0b69e','#d4cbb4','#e4dcc6'];
  const BRASS  = ['#4a3410','#66491a','#856228','#a37e3c','#bd9a55','#d4b774'];
  const CHALK  = ['#15181a','#1d2124','#272c30','#31373c','#3d4348','#4b5257'];
  const CANVAS = ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'];
  const GLASSD = ['#33474d','#40585f','#547078','#6b898f','#83a2a7'];   // day: dark interior behind glass
  const GLASSN = ['#7a4f18','#b98a2f','#eed07a'];                        // night: lamplight from inside
  const GLASS_HI = '#cfe6e8';
  const KEY = '#1a1c22';
  const ICE  = ['#5c7580','#728e98','#8aa8b0','#a5c2c8','#c1dade','#dcefef'];

  const SIDINGS    = ['clapboard','shingle','boardBatten','corrugated'];
  const SHAPES     = ['gable','shed','gambrel','falseFront'];
  const STOREFRONTS= ['bay','plate','smallPane','hatch','narrow'];
  const SIGNS      = ['board','oval','shield','pennant'];
  const WINDOWS    = ['sixOverSix','twoOverTwo','oneOverOne','industrial'];
  const WINSTYLES  = {
    sixOverSix: { v:2, r:[0.25,0.5,0.75] },
    twoOverTwo: { v:1, r:[0.5] },
    oneOverOne: { v:0, r:[0.5] },
    industrial: { v:2, r:[0.2,0.4,0.6,0.8] },
  };
  // striped awning duck: [ramp A, ramp B] as BODY keys
  const AWNINGS = {
    redCream:   ['red','white'],
    greenCream: ['sage','cream'],
    blueCream:  ['blue','white'],
    goldCream:  ['mustard','cream'],
    tealCream:  ['teal','white'],
    plain:      ['white','white'],
  };

  // ---- the nine businesses. Wd/Ln/wallH ranges are [base, size*k] and are the CONTRACT the
  // interior rig reads back, so shell and room share one footprint. ------------------------------
  const TYPES = {
    generalStore:{ label:'general store', shape:'falseFront', pitch:0.62, siding:'clapboard', body:'cream',
      roof:'asphaltGrey', storefront:'bay',       windows:'twoOverTwo', winD:0.50,
      awning:'straight', awnCols:'redCream',  fascia:true,  bracket:true,  sign:'board',   signTone:'red',
      stall:true,  patio:false, sandwich:true,  flat:true,  load:false, scale:false, stacks:0, planters:true,
      Wd:[7.0,2.6], Ln:[8.5,4.0], wallH:[3.9,1.2], fH:0.55, size:0.35 },
    fishMarket:{ label:'fish market', shape:'shed', pitch:0.50, siding:'boardBatten', body:'blue',
      roof:'corrugated', storefront:'hatch',      windows:'industrial', winD:0.45,
      awning:'straight', awnCols:'blueCream', fascia:true,  bracket:false, sign:'board',   signTone:'white',
      stall:true,  patio:false, sandwich:true,  flat:false, load:true,  scale:true,  stacks:0, planters:false,
      Wd:[6.5,2.4], Ln:[7.0,3.5], wallH:[3.5,1.0], fH:0.45, size:0.40 },
    chandlery:{ label:'chandlery', shape:'gable', pitch:0.95, siding:'shingle', body:'greyShingle',
      roof:'asphaltGrey', storefront:'smallPane', windows:'sixOverSix', winD:0.50,
      awning:'none',     awnCols:'goldCream', fascia:true,  bracket:true,  sign:'pennant', signTone:'gold',
      stall:true,  patio:false, sandwich:false, flat:true,  load:true,  scale:false, stacks:0, planters:false,
      Wd:[6.5,2.4], Ln:[8.0,4.0], wallH:[3.8,1.2], fH:0.50, size:0.45 },
    bakery:{ label:'café / bakery', shape:'falseFront', pitch:0.60, siding:'clapboard', body:'sage',
      roof:'asphaltBrown', storefront:'plate',    windows:'twoOverTwo', winD:0.50,
      awning:'scallop',  awnCols:'greenCream',fascia:true,  bracket:true,  sign:'oval',    signTone:'cream',
      stall:false, patio:true,  sandwich:true,  flat:true,  load:false, scale:false, stacks:1, planters:true,
      Wd:[6.0,2.2], Ln:[7.0,3.2], wallH:[3.6,1.0], fH:0.50, size:0.30 },
    restaurant:{ label:'restaurant', shape:'gable', pitch:0.85, siding:'clapboard', body:'white',
      roof:'asphaltGrey', storefront:'plate',     windows:'twoOverTwo', winD:0.60,
      awning:'straight', awnCols:'redCream',  fascia:true,  bracket:false, sign:'board',   signTone:'red',
      stall:false, patio:true,  sandwich:true,  flat:true,  load:false, scale:false, stacks:1, planters:true,
      Wd:[7.5,2.8], Ln:[9.0,4.5], wallH:[4.0,1.2], fH:0.50, size:0.50 },
    tavern:{ label:'tavern', shape:'gable', pitch:1.00, siding:'shingle', body:'red',
      roof:'asphaltGrey', storefront:'narrow',    windows:'sixOverSix', winD:0.45,
      awning:'none',     awnCols:'goldCream', fascia:false, bracket:true,  sign:'shield',  signTone:'gold',
      stall:false, patio:true,  sandwich:false, flat:true,  load:false, scale:false, stacks:1, planters:false,
      Wd:[7.0,2.6], Ln:[8.5,4.0], wallH:[3.9,1.4], fH:0.50, size:0.45 },
    postOffice:{ label:'post office', shape:'falseFront', pitch:0.60, siding:'clapboard', body:'white',
      roof:'asphaltGrey', storefront:'narrow',    windows:'sixOverSix', winD:0.50,
      awning:'none',     awnCols:'blueCream', fascia:true,  bracket:false, sign:'board',   signTone:'blue',
      stall:false, patio:false, sandwich:false, flat:true,  load:false, scale:false, stacks:0, planters:false,
      Wd:[6.5,2.4], Ln:[7.5,3.5], wallH:[3.9,1.0], fH:0.60, size:0.35 },
    takeoutStand:{ label:'takeout stand', shape:'shed', pitch:0.45, siding:'boardBatten', body:'mustard',
      roof:'metalSeam', storefront:'hatch',       windows:'oneOverOne', winD:0.30,
      awning:'straight', awnCols:'redCream',  fascia:true,  bracket:false, sign:'board',   signTone:'red',
      stall:false, patio:true,  sandwich:true,  flat:false, load:false, scale:false, stacks:1, planters:false,
      Wd:[4.2,1.6], Ln:[4.0,2.0], wallH:[2.9,0.8], fH:0.35, size:0.30 },
    giftShop:{ label:'gift shop', shape:'gable', pitch:0.90, siding:'clapboard', body:'teal',
      roof:'asphaltBrown', storefront:'bay',      windows:'twoOverTwo', winD:0.55,
      awning:'scallop',  awnCols:'tealCream', fascia:true,  bracket:true,  sign:'oval',    signTone:'cream',
      stall:true,  patio:false, sandwich:false, flat:true,  load:false, scale:false, stacks:0, planters:true,
      Wd:[5.5,2.0], Ln:[6.5,3.0], wallH:[3.6,1.0], fH:0.50, size:0.30 },
  };
  const PRESETS = {
    harbourStore:  { type:'generalStore', size:0.35, weather:0.32 },
    coopFishHouse: { type:'fishMarket',   size:0.45, weather:0.52 },
    shipChandler:  { type:'chandlery',    size:0.45, weather:0.40 },
    quaysideBakery:{ type:'bakery',       size:0.28, weather:0.20 },
    wharfDiner:    { type:'restaurant',   size:0.50, weather:0.26 },
    theAnchorInn:  { type:'tavern',       size:0.48, weather:0.38 },
    villagePost:   { type:'postOffice',   size:0.30, weather:0.24 },
    chipStand:     { type:'takeoutStand', size:0.30, weather:0.34 },
    lighthouseGift:{ type:'giftShop',     size:0.28, weather:0.22 },
  };

  // ---- shading constants (fleet recipe) ----
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }

  // ---- camera / projection (identical to every other iso rig) ----
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { th, ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) };
  }
  function projVert(x,y,z,B){
    const xr=x*B.ct - y*B.stt, yr=x*B.stt + y*B.ct, zr=z;
    return { xr,yr,zr, sx:cx+xr*S, sy:groundY-(yr*B.se+zr*B.ce)*S, d:(yr*B.ce-zr*B.se) };
  }
  function normal(a,b,c){
    const ux=b.xr-a.xr,uy=b.yr-a.yr,uz=b.zr-a.zr, vx=c.xr-a.xr,vy=c.yr-a.yr,vz=c.zr-a.zr;
    let nx=uy*vz-uz*vy, ny=uz*vx-ux*vz, nz=ux*vy-uy*vx;
    const m=Math.hypot(nx,ny,nz)||1; return [nx/m,ny/m,nz/m];
  }
  function shadeOf(n, se, ce){ return n[0]*LN[0] + (n[1]*se+n[2]*ce)*LN[1] + (-n[1]*ce+n[2]*se)*LN[2]; }

  // ---- face builders ---------------------------------------------------------
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }
  function wall(out, x0,y0,x1,y1, z0,z1, mat, tex, b){
    const L=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]], mat, b||0, 0, [[0,z0],[L,z0],[L,z1],[0,z1]], tex));
  }
  function slab(out, pts, z, mat, b, tex){
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0, tex?pts.map(p=>[p[0],p[1]]):null, tex));
  }
  function boxSolid(out, x0,x1, y0,y1, z0,z1, mat, tex, b){
    wall(out, x0,y0, x1,y0, z0,z1, mat, tex, b);
    wall(out, x1,y1, x0,y1, z0,z1, mat, tex, b);
    wall(out, x1,y0, x1,y1, z0,z1, mat, tex, b);
    wall(out, x0,y1, x0,y0, z0,z1, mat, tex, b);
    slab(out, [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, (b||0)+0.25);
  }
  function tri(out,a,b,c,mat,bias,uv,tex){ out.push(F([a,b,c],mat,bias||0,0,uv||null,tex||null)); }
  function quad(out,a,b,c,d,mat,bias,tex){
    let uv=null;
    if(tex){ const L=Math.hypot(b[0]-a[0],b[1]-a[1],b[2]-a[2]), M=Math.hypot(d[0]-a[0],d[1]-a[1],d[2]-a[2]);
      uv=[[0,0],[L,0],[L,M],[0,M]]; }
    out.push(F([a,b,c,d],mat,bias||0,0,uv,tex||null));
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

  // ---- textures (integer ramp delta) ----------------------------------------
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }
  function sidingTex(kind){
    if(kind==='clapboard'){ const LAP=0.30;
      return (u,v)=>{ const f=((v%LAP)+LAP)%LAP; return f<0.055?-2:(f>LAP-0.04?1:0); }; }
    if(kind==='shingle'){ const CO=0.34, SW=0.24;
      return (u,v)=>{ const row=Math.floor(v/CO), f=((v%CO)+CO)%CO, off=(row&1)*0.5*SW, su=(((u+off)%SW)+SW)%SW;
        if(f<0.05) return -2; if(su<0.035) return -1; if(f>CO-0.05) return 1; return 0; }; }
    if(kind==='boardBatten'){ const BAT=0.30, bw=0.075;
      return (u,v)=>{ const f=((u%BAT)+BAT)%BAT;
        if(f<bw) return 1; if(f<bw+0.035) return -1; if(f>BAT-0.035) return -2; return 0; }; }
    if(kind==='corrugated'){ const R=0.19;
      return (u,v)=>{ const f=(((u%R)+R)%R)/R;
        if(f<0.10) return -2; if(f<0.24) return -1; if(f<0.46) return 0; if(f<0.62) return 1; if(f<0.80) return 0; return -1; }; }
    return null;
  }
  function roofTexFor(roof){
    if(roof==='metalSeam') return (u,v)=>{ const s=0.42; return (((u%s)+s)%s)<0.05?-2:0; };
    if(roof==='corrugated'||roof==='rusted'){ const R=0.24;
      return (u,v)=>{ const f=(((u%R)+R)%R)/R;
        if(f<0.12) return -2; if(f<0.30) return -1; if(f<0.55) return 0; if(f<0.75) return 1; return -1; }; }
    return (u,v)=>{ const CO=0.34, f=((v%CO)+CO)%CO; return f<0.05?-2:(f>CO-0.05?1:0); };
  }
  function plankTex(pw){ const PW=pw||0.26;
    return (u,v)=>{ const p=Math.floor(u/PW), f=((u%PW)+PW)%PW;
      if(f<0.03) return -2; return (hash2(p|0,7)<0.42?0:1) - (hash2(p|0,3)<0.22?1:0); }; }
  function stoneTex(){ const c=0.30, bl=0.56;
    return (u,v)=>{ const row=Math.floor(v/c), off=(row&1)*0.5*bl, fv=((v%c)+c)%c,
        su=(((u+off)%bl)+bl)%bl, r=hash2(Math.floor((u+off)/bl)|0,row|0);
      if(fv<0.05) return -2; if(su<0.05) return -1; if(fv>c-0.05) return 1; return r<0.3?-1:(r>0.8?1:0); }; }
  function boardTex(){ const bw=0.22; return (u,v)=>{ const f=((u%bw)+bw)%bw; return f<0.03?-2:(f>bw-0.03?1:0); }; }

  // ---- fittings -------------------------------------------------------------
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
  // multi-light shop glazing: one big opening gridded into panes
  function glazedBay(out, axis, plane, nrm, c, z0, w, h, cols, rows, frameMat){
    const put=putOn(axis), fm=frameMat||'trim';
    put(out,plane,nrm, c-w/2-0.11, c+w/2+0.11, z0-0.11, z0+h+0.12, fm, 0.35, 0.05);      // outer frame
    put(out,plane,nrm, c-w/2, c+w/2, z0, z0+h,'glass',0.0,0.10);                          // glass field
    put(out,plane,nrm, c-w/2+0.04, c-w/2+w*0.26, z0+h*0.42, z0+h-0.06,'glassHi',0.0,0.13);// sheen
    const mb=0.05;
    for(let i=1;i<cols;i++){ const cc=c-w/2+w*(i/cols); put(out,plane,nrm, cc-mb, cc+mb, z0, z0+h, fm, 0.5, 0.14); }
    for(let j=1;j<rows;j++){ const zz=z0+h*(j/rows); put(out,plane,nrm, c-w/2, c+w/2, zz-mb, zz+mb, fm, 0.5, 0.14); }
  }
  // panelled shop door: glass upper light + two lower panels
  function shopDoor(out, axis, plane, nrm, c, z0, dw, dh, glassTop){
    const put=putOn(axis), ct=0.11, topZ=z0+dh;
    put(out,plane,nrm, c-dw/2-ct, c+dw/2+ct, z0, topZ+ct,'trim',0.5,0.06);
    put(out,plane,nrm, c-dw/2-ct-0.05, c+dw/2+ct+0.05, topZ+ct, topZ+ct+0.09,'trim',0.85,0.05);
    put(out,plane,nrm, c-dw/2, c+dw/2, z0, topZ,'door',0.05,0.10);
    if(glassTop!==false){
      const gz0=z0+dh*0.52, gz1=topZ-0.14;
      put(out,plane,nrm, c-dw/2+0.10, c+dw/2-0.10, gz0, gz1,'glass',0.0,0.12);
      put(out,plane,nrm, c-dw/2+0.14, c-dw/2+dw*0.4, gz0+ (gz1-gz0)*0.4, gz1-0.04,'glassHi',0.0,0.14);
      put(out,plane,nrm, c-0.03, c+0.03, gz0, gz1,'trim',0.5,0.14);
    }
    for(const k of [0,1]){ const pz0=z0+0.12+k*(dh*0.2), pz1=pz0+dh*0.16;
      put(out,plane,nrm, c-dw/2+0.10, c+dw/2-0.10, pz0, pz1,'door',-1.4,0.12); }
    put(out,plane,nrm, c+dw/2-0.20, c+dw/2-0.11, z0+dh*0.46, z0+dh*0.46+0.13,'brass',0.9,0.13);  // knob
  }
  // drop-down service hatch: counter shelf, opening, and the shutter propped up as a canopy
  function serviceHatch(out, plane, c, z0, w, h, shutter){
    decalY(out, plane, 1, c-w/2-0.12, c+w/2+0.12, z0-0.12, z0+h+0.12,'trim',0.4,null,true,0.05);
    decalY(out, plane, 1, c-w/2, c+w/2, z0, z0+h,'dark',0.0,null,true,0.10);
    decalY(out, plane, 1, c-w/2+0.05, c+w/2-0.05, z0+h*0.34, z0+h*0.62,'glass',0.0,null,true,0.11);
    boxSolid(out, c-w/2-0.16, c+w/2+0.16, plane, plane+0.40, z0-0.10, z0,'worktop',null,0.35);   // counter shelf
    for(const x of [c-w/2+0.12, c+w/2-0.12]) boxSolid(out, x-0.05,x+0.05, plane+0.24,plane+0.34, z0-0.55, z0-0.10,'iron',null,0.1);
    if(shutter!==false){                                                                          // propped shutter
      const top=z0+h+0.06, out2=plane+1.15, dz=top+0.34;
      quad(out, [c-w/2-0.2, plane+0.02, top],[c+w/2+0.2, plane+0.02, top],[c+w/2+0.2, out2, dz],[c-w/2-0.2, out2, dz],'wood',0.25, boardTex());
      for(const x of [c-w/2-0.14, c+w/2+0.14]) boxSolid(out, x-0.035,x+0.035, plane+0.5,plane+0.56, z0+0.2, dz-0.1,'iron',null,0.1);
    }
  }
  // ABSTRACT sign blank — shaped board, painted field, border band, no lettering
  function signBlank(out, axis, plane, nrm, c, z0, w, h, shape){
    const put=putOn(axis);
    if(shape==='oval'){
      put(out,plane,nrm, c-w/2, c+w/2, z0+h*0.16, z0+h*0.84,'signB',0.3,0.06);
      put(out,plane,nrm, c-w*0.40, c+w*0.40, z0, z0+h,'signB',0.3,0.06);
      put(out,plane,nrm, c-w/2+0.09, c+w/2-0.09, z0+h*0.16+0.07, z0+h*0.84-0.07,'signF',0.15,0.08);
      put(out,plane,nrm, c-w*0.40+0.09, c+w*0.40-0.09, z0+0.07, z0+h-0.07,'signF',0.15,0.08);
      put(out,plane,nrm, c-w*0.22, c+w*0.22, z0+h*0.44, z0+h*0.56,'signB',0.5,0.10);
    } else if(shape==='shield'){
      put(out,plane,nrm, c-w/2, c+w/2, z0+h*0.30, z0+h,'signB',0.3,0.06);
      put(out,plane,nrm, c-w*0.36, c+w*0.36, z0+h*0.12, z0+h*0.34,'signB',0.3,0.06);
      put(out,plane,nrm, c-w*0.16, c+w*0.16, z0, z0+h*0.16,'signB',0.3,0.06);
      put(out,plane,nrm, c-w/2+0.08, c+w/2-0.08, z0+h*0.38, z0+h-0.08,'signF',0.15,0.08);
      put(out,plane,nrm, c-w*0.28, c+w*0.28, z0+h*0.18, z0+h*0.40,'signF',0.15,0.08);
    } else if(shape==='pennant'){
      put(out,plane,nrm, c-w/2, c+w/2, z0+h*0.52, z0+h,'signB',0.3,0.06);
      put(out,plane,nrm, c-w*0.34, c+w*0.34, z0+h*0.26, z0+h*0.54,'signB',0.3,0.06);
      put(out,plane,nrm, c-w*0.12, c+w*0.12, z0, z0+h*0.28,'signB',0.3,0.06);
      put(out,plane,nrm, c-w/2+0.08, c+w/2-0.08, z0+h*0.58, z0+h-0.07,'signF',0.15,0.08);
    } else {
      put(out,plane,nrm, c-w/2-0.09, c+w/2+0.09, z0-0.09, z0+h+0.09,'signB',0.3,0.06);      // frame
      put(out,plane,nrm, c-w/2, c+w/2, z0, z0+h,'signF',0.15,0.08);                          // field
      put(out,plane,nrm, c-w/2+0.10, c+w/2-0.10, z0+h*0.16, z0+h*0.24,'signB',0.55,0.10);    // upper band
      put(out,plane,nrm, c-w/2+0.10, c+w/2-0.10, z0+h*0.74, z0+h*0.82,'signB',0.55,0.10);    // lower band
    }
  }
  // signboard fascia band spanning the storefront, with a moulded cap
  function fasciaBand(out, plane, hw, z0, h, inset){
    const a=-hw+inset, b=hw-inset;
    decalY(out, plane, 1, a, b, z0, z0+h,'signF',0.15,null,true,0.05);
    decalY(out, plane, 1, a, b, z0+h*0.12, z0+h*0.2,'signB',0.55,null,true,0.07);
    decalY(out, plane, 1, a, b, z0+h*0.80, z0+h*0.88,'signB',0.55,null,true,0.07);
    boxSolid(out, a-0.08, b+0.08, plane, plane+0.16, z0+h, z0+h+0.13,'trim',null,0.55);       // cap moulding
    boxSolid(out, a-0.08, b+0.08, plane, plane+0.12, z0-0.12, z0,'trim',null,0.45);           // sill moulding
  }
  // striped fabric awning: alternating duck panels on a slope, with a scalloped or straight valance
  function awning(out, plane, hw, z0, ext, kind, inset){
    const a=-hw+inset, b=hw-inset, span=b-a, front=plane+ext, dz=z0-ext*0.36;
    const n=Math.max(6, Math.round(span/0.42)), step=span/n;
    for(let i=0;i<n;i++){ const x0=a+i*step, x1=x0+step, m=(i&1)?'cvB':'cvA';
      quad(out, [x0,plane,z0],[x1,plane,z0],[x1,front,dz],[x0,front,dz], m, 0.20);
      quad(out, [x1,plane,z0-0.07],[x0,plane,z0-0.07],[x0,front,dz-0.07],[x1,front,dz-0.07], m, -0.75);  // underside
      if(kind==='scallop'){ const cxm=(x0+x1)/2, r=step*0.46;
        quad(out, [cxm-r,front,dz],[cxm+r,front,dz],[cxm+r,front,dz-0.20],[cxm-r,front,dz-0.20], m, 0.05);
        quad(out, [cxm-r*0.6,front,dz-0.20],[cxm+r*0.6,front,dz-0.20],[cxm+r*0.6,front,dz-0.32],[cxm-r*0.6,front,dz-0.32], m, 0.05);
      } else {
        quad(out, [x0,front,dz],[x1,front,dz],[x1,front,dz-0.26],[x0,front,dz-0.26], m, 0.05);
      }
    }
    boxSolid(out, a-0.04, b+0.04, front-0.05, front+0.05, dz-0.02, dz+0.05,'iron',null,0.2);            // front rail
    for(const x of [a+0.16, b-0.16]){                                                                   // side arms
      boxSolid(out, x-0.035,x+0.035, plane, front, dz+0.02, dz+0.08,'iron',null,0.15);
      boxSolid(out, x-0.03,x+0.03, plane+0.04, plane+0.10, dz, z0+0.06,'iron',null,0.15);
    }
  }
  // wrought bracket + hanging sign off the street elevation
  function bracketSign(out, plane, x, z, w, h, shape){
    const armY=plane+0.10, tip=plane+w*0.62+0.34;
    boxSolid(out, x-0.05,x+0.05, plane-0.02, armY+0.06, z-0.06, z+0.62,'iron',null,0.15);          // wall plate
    boxSolid(out, x-0.045,x+0.045, armY, tip, z+0.50, z+0.58,'iron',null,0.2);                     // arm
    for(let i=0;i<3;i++){ const t=(i+1)/4, yy=armY+(tip-armY)*t, zz=z+0.50-0.34*t;                 // scrolled brace
      boxSolid(out, x-0.03,x+0.03, yy-0.03,yy+0.03, zz-0.03, zz+0.03,'iron',null,0.15); }
    const cY=(armY+tip)/2;
    for(const yy of [cY-w*0.30, cY+w*0.30]) boxSolid(out, x-0.02,x+0.02, yy-0.02,yy+0.02, z+0.32, z+0.50,'iron',null,0.1);
    signBlank(out,'x', x-0.05, -1, cY, z+0.32-h, w, h, shape);                                     // board, both faces
    signBlank(out,'x', x+0.05,  1, cY, z+0.32-h, w, h, shape);
  }
  // A-frame chalkboard on the pavement
  function sandwichBoard(out, x, y, rot){
    const w=0.62, h=0.95, s=0.24;
    for(const sgn of [-1,1]){
      quad(out, [x-w/2, y+sgn*0.03, 0],[x+w/2, y+sgn*0.03, 0],[x+w/2, y+sgn*s, h],[x-w/2, y+sgn*s, h],'wood', sgn>0?0.15:-0.4, boardTex());
      decalY(out, y+sgn*(s*0.55), sgn, x-w/2+0.07, x+w/2-0.07, h*0.18, h*0.86,'chalk',0.0,null,true,0.08);
      for(let k=0;k<3;k++){ const zz=h*0.30+k*0.16;
        decalY(out, y+sgn*(s*0.55), sgn, x-w/2+0.14, x+w/2-0.20-k*0.06, zz, zz+0.045,'trim',0.7,null,true,0.10); }
    }
    boxSolid(out, x-w/2-0.02, x+w/2+0.02, y-0.03, y+0.03, h, h+0.06,'wood',null,0.4);
  }
  // trestle stall: table, sloped display board, and stock in crates + baskets
  function stall(out, x, y, w, stock, rnd, kinds){
    const d=0.78, ht=0.86;
    for(const sx of [x-w/2+0.14, x+w/2-0.14]){
      boxSolid(out, sx-0.05,sx+0.05, y-d/2+0.06, y-d/2+0.16, 0, ht,'wood',null,0.1);
      boxSolid(out, sx-0.05,sx+0.05, y+d/2-0.16, y+d/2-0.06, 0, ht,'wood',null,0.1);
      quad(out, [sx-0.04, y-d/2+0.10, ht*0.42],[sx-0.04, y+d/2-0.10, ht*0.42],[sx-0.04, y+d/2-0.10, ht*0.36],[sx-0.04, y-d/2+0.10, ht*0.36],'wood',0.1);
    }
    boxSolid(out, x-w/2, x+w/2, y-d/2, y+d/2, ht, ht+0.07,'wood', plankTex(0.3), 0.3);
    quad(out, [x-w/2, y-d/2+0.05, ht+0.60],[x+w/2, y-d/2+0.05, ht+0.60],[x+w/2, y+d/2-0.16, ht+0.08],[x-w/2, y+d/2-0.16, ht+0.08],'wood',0.3, plankTex(0.3));
    for(const sx of [x-w/2+0.1, x+w/2-0.1]) boxSolid(out, sx-0.04,sx+0.04, y-d/2+0.06,y-d/2+0.12, ht, ht+0.62,'wood',null,0.1);
    goodsRow(out, x-w/2+0.10, x+w/2-0.10, y+d/2-0.44, y+d/2-0.10, ht+0.07, 0.26, rnd, stock, kinds);
    goodsRow(out, x-w/2+0.14, x+w/2-0.14, y-d/2+0.16, y-d/2+0.44, ht+0.14, 0.20, rnd, stock, kinds);
    crateStack(out, x-w/2-0.52, y+0.1, rnd, 2);
    barrel(out, x+w/2+0.44, y-0.12, 0.62);
    basket(out, x+w/2+0.42, y+0.42, 0.34);
  }
  function crateStack(out, x, y, rnd, n){
    let z=0;
    for(let i=0;i<n;i++){ const s=0.46-i*0.03, jx=(rnd()-0.5)*0.10, jy=(rnd()-0.5)*0.10;
      boxSolid(out, x-s/2+jx, x+s/2+jx, y-s/2+jy, y+s/2+jy, z, z+s*0.78,'crate', boardTex(), 0.05);
      for(const zz of [z+s*0.22, z+s*0.56]){
        decalY(out, y+s/2+jy, 1, x-s/2+jx+0.03, x+s/2+jx-0.03, zz-0.028, zz+0.028,'crate',-1.4,null,true,0.06);
        decalX(out, x+s/2+jx, 1, y-s/2+jy+0.03, y+s/2+jy-0.03, zz-0.028, zz+0.028,'crate',-1.4,null,true,0.06); }
      z += s*0.78;
    }
  }
  function barrel(out, x, y, h){
    const r=0.30;
    boxSolid(out, x-r,x+r, y-r*0.82,y+r*0.82, 0, h,'wood', null, 0.0);
    boxSolid(out, x-r*0.82,x+r*0.82, y-r,y+r, 0, h,'wood', null, 0.0);
    for(const zz of [h*0.16, h*0.5, h*0.86]){
      decalY(out, y+r, 1, x-r*0.78, x+r*0.78, zz-0.035, zz+0.035,'iron',0.3,null,true,0.06);
      decalY(out, y-r,-1, x-r*0.78, x+r*0.78, zz-0.035, zz+0.035,'iron',0.3,null,true,0.06);
      decalX(out, x+r, 1, y-r*0.78, y+r*0.78, zz-0.035, zz+0.035,'iron',0.3,null,true,0.06);
      decalX(out, x-r,-1, y-r*0.78, y+r*0.78, zz-0.035, zz+0.035,'iron',0.3,null,true,0.06); }
    slab(out, [[x-r*0.8,y-r*0.66],[x+r*0.8,y-r*0.66],[x+r*0.8,y+r*0.66],[x-r*0.8,y+r*0.66]], h+0.01,'wood',0.5);
  }
  function basket(out, x, y, r){
    boxSolid(out, x-r,x+r, y-r*0.86,y+r*0.86, 0, r*1.0,'crate',null,0.0);
    boxSolid(out, x-r*0.86,x+r*0.86, y-r,y+r, 0, r*1.0,'crate',null,0.0);
    for(let k=0;k<3;k++){ const zz=r*(0.2+k*0.28);
      decalY(out, y+r,1, x-r*0.8,x+r*0.8, zz-0.02, zz+0.02,'crate',-1.3,null,true,0.06); }
    slab(out, [[x-r*0.7,y-r*0.6],[x+r*0.7,y-r*0.6],[x+r*0.7,y+r*0.6],[x-r*0.7,y+r*0.6]], r*1.0+0.01,'goods1',0.2);
  }
  // deterministic goods run — small cartons / tins / bottles filling a shelf or table band
  function goodsRow(out, x0,x1, y0,y1, z, hMax, rnd, stock, kinds){
    if(stock<=0.01) return;
    const span=x1-x0; let x=x0+0.02;
    while(x < x1-0.08){
      const kind=kinds[(rnd()*kinds.length)|0], mat='goods'+(1+((rnd()*4)|0));
      let w, h, d;
      if(kind==='bottle'){ w=0.13; h=hMax*(0.82+rnd()*0.2); d=0.13; }
      else if(kind==='tin'){ w=0.15; h=hMax*(0.4+rnd()*0.2); d=0.15; }
      else if(kind==='sack'){ w=0.26; h=hMax*(0.6+rnd()*0.25); d=(y1-y0)*0.8; }
      else if(kind==='bolt'){ w=0.20; h=hMax*(0.9+rnd()*0.1); d=(y1-y0)*0.7; }
      else if(kind==='parcel'){ w=0.24+rnd()*0.14; h=hMax*(0.35+rnd()*0.25); d=(y1-y0)*0.72; }
      else { w=0.18+rnd()*0.14; h=hMax*(0.5+rnd()*0.45); d=(y1-y0)*0.76; }
      if(x+w > x1-0.02) break;
      if(rnd() < stock){
        const yc=(y0+y1)/2, ya=yc-d/2, yb=yc+d/2;
        boxSolid(out, x, x+w, ya, yb, z, z+h, mat, null, 0.0);
        if(kind==='bottle'){ boxSolid(out, x+w*0.3, x+w*0.7, ya+d*0.3, yb-d*0.3, z+h, z+h+hMax*0.22, mat, null, 0.2); }
        else if(kind==='tin'){ decalY(out, yb, 1, x+0.02, x+w-0.02, z+h*0.34, z+h*0.62,'trim',0.6,null,true,0.06); }
        else if(kind==='parcel'){ decalY(out, yb, 1, x+w*0.42, x+w*0.58, z, z+h,'trim',0.5,null,true,0.06);
          slab(out, [[x+w*0.42,ya],[x+w*0.58,ya],[x+w*0.58,yb],[x+w*0.42,yb]], z+h+0.005,'trim',0.5); }
        else if(kind==='crateB'){ decalY(out, yb, 1, x+0.02, x+w-0.02, z+h*0.5-0.02, z+h*0.5+0.02,'crate',-1.4,null,true,0.06); }
        else { decalY(out, yb, 1, x+0.03, x+w-0.03, z+h*0.52, z+h*0.78,'trim',0.45,null,true,0.06); }
      }
      x += w + 0.03 + rnd()*0.05*(1-stock*0.6);
      if(span<0.3) break;
    }
  }
  // café table + chair pair + parasol
  function patioSet(out, x, y, umbrella, rnd){
    const r=0.36, ht=0.74;
    boxSolid(out, x-0.06,x+0.06, y-0.06,y+0.06, 0, ht,'iron',null,0.1);
    boxSolid(out, x-0.22,x+0.22, y-0.05,y+0.05, 0, 0.05,'iron',null,0.2);
    boxSolid(out, x-0.05,x+0.05, y-0.22,y+0.22, 0, 0.05,'iron',null,0.2);
    boxSolid(out, x-r,x+r, y-r*0.84,y+r*0.84, ht, ht+0.06,'worktop',null,0.35);
    boxSolid(out, x-r*0.84,x+r*0.84, y-r,y+r, ht, ht+0.06,'worktop',null,0.35);
    for(const [ox,oy] of [[-r-0.32, 0.06],[r+0.32,-0.06]]){
      const cx2=x+ox, cy2=y+oy;
      boxSolid(out, cx2-0.20,cx2+0.20, cy2-0.19,cy2+0.19, 0.42, 0.47,'wood',null,0.25);
      for(const [px,py] of [[-0.16,-0.15],[0.16,-0.15],[-0.16,0.15],[0.16,0.15]])
        boxSolid(out, cx2+px-0.03,cx2+px+0.03, cy2+py-0.03,cy2+py+0.03, 0, 0.42,'iron',null,0.05);
      boxSolid(out, cx2-0.19,cx2+0.19, cy2+(ox<0?0.14:-0.19), cy2+(ox<0?0.19:-0.14), 0.47, 0.86,'wood',null,0.15);
    }
    if(umbrella){
      const mz=2.18; boxSolid(out, x-0.045,x+0.045, y-0.045,y+0.045, ht, mz,'wood',null,0.1);
      const rr=1.05, zz=mz-0.34, n=8;
      for(let i=0;i<n;i++){ const a0=(i/n)*Math.PI*2, a1=((i+1)/n)*Math.PI*2, m=(i&1)?'cvB':'cvA';
        tri(out, [x,y,mz],[x+Math.cos(a0)*rr, y+Math.sin(a0)*rr, zz],[x+Math.cos(a1)*rr, y+Math.sin(a1)*rr, zz], m, 0.15);
        quad(out, [x+Math.cos(a0)*rr, y+Math.sin(a0)*rr, zz],[x+Math.cos(a1)*rr, y+Math.sin(a1)*rr, zz],
                  [x+Math.cos(a1)*rr*0.99, y+Math.sin(a1)*rr*0.99, zz-0.18],[x+Math.cos(a0)*rr*0.99, y+Math.sin(a0)*rr*0.99, zz-0.18], m, 0.0); }
      boxSolid(out, x-0.03,x+0.03, y-0.03,y+0.03, mz, mz+0.16,'wood',null,0.3);
    }
  }
  function planter(out, x, y){
    const r=0.30, h=0.42;
    boxSolid(out, x-r,x+r, y-r,y+r, 0, h,'crate', boardTex(), 0.05);
    decalY(out, y+r,1, x-r+0.04, x+r-0.04, h*0.3, h*0.42,'crate',-1.3,null,true,0.06);
    slab(out, [[x-r+0.05,y-r+0.05],[x+r-0.05,y-r+0.05],[x+r-0.05,y+r-0.05],[x-r+0.05,y+r-0.05]], h+0.01,'soil',-0.4);
    for(let i=0;i<7;i++){ const a=i*1.12, rr=r*0.62*(0.4+((i%3)*0.3)), px=x+Math.cos(a)*rr, py=y+Math.sin(a)*rr, hh=0.16+((i%4)*0.06);
      boxSolid(out, px-0.06,px+0.06, py-0.06,py+0.06, h, h+hh,'leaf',null,0.1);
      if(i%2) boxSolid(out, px-0.035,px+0.035, py-0.035,py+0.035, h+hh, h+hh+0.09,'bloom',null,0.3); }
  }
  function platformScale(out, x, y){
    boxSolid(out, x-0.44,x+0.44, y-0.34,y+0.34, 0, 0.16,'iron',null,0.05);
    boxSolid(out, x-0.40,x+0.40, y-0.30,y+0.30, 0.16, 0.20,'steel', plankTex(0.2), 0.35);
    boxSolid(out, x+0.30-0.05,x+0.30+0.05, y-0.30,y-0.20, 0.20, 1.26,'iron',null,0.1);
    boxSolid(out, x+0.16,x+0.46, y-0.34,y-0.16, 1.26, 1.58,'steel',null,0.2);
    decalY(out, y-0.34,-1, x+0.20, x+0.42, 1.32, 1.54,'dial',0.5,null,true,0.07);
    decalY(out, y-0.34,-1, x+0.30-0.015, x+0.30+0.015, 1.36, 1.50,'iron',0.2,null,true,0.09);
  }
  function iceTray(out, x0,x1, y0,y1, z){
    boxSolid(out, x0,x1, y0,y1, z, z+0.14,'steel',null,0.2);
    slab(out, [[x0+0.04,y0+0.04],[x1-0.04,y0+0.04],[x1-0.04,y1-0.04],[x0+0.04,y1-0.04]], z+0.15,'ice',-0.6);
    for(let i=0;i<9;i++){ const px=x0+0.10+((x1-x0-0.24)*((i*0.37)%1)), py=y0+0.08+((y1-y0-0.20)*((i*0.61)%1));
      boxSolid(out, px-0.13,px+0.13, py-0.05,py+0.05, z+0.15, z+0.21, (i%3)?'fishA':'fishB', null, 0.1); }
  }
  function loadingDoor(out, plane, c, z0, w, h){
    decalX(out, plane, 1, c-w/2-0.12, c+w/2+0.12, z0-0.12, z0+h+0.14,'steel',0.35,null,true,0.05);
    decalX(out, plane, 1, c-w/2, c+w/2, z0, z0+h,'galv',-0.1,null,true,0.10);
    const rib=0.28; for(let z=z0+rib; z<z0+h-0.04; z+=rib) decalX(out, plane,1, c-w/2,c+w/2, z-0.03,z+0.03,'galv',-1.6,null,true,0.12);
    decalX(out, plane, 1, c-w/2, c+w/2, z0, z0+0.16,'steel',0.4,null,true,0.11);
  }
  function laundryLine(out, hw, y0, y1, z, rnd){
    const px=hw+2.35;
    boxSolid(out, px-0.07,px+0.07, y0-0.07,y0+0.07, 0, z+0.42,'wood',null,0.1);
    boxSolid(out, px-0.34,px+0.34, y0-0.05,y0+0.05, z+0.24, z+0.32,'wood',null,0.3);
    for(const zz of [z, z-0.34]){
      boxSolid(out, hw+0.04, px, y0-0.015, y0+0.015, zz, zz+0.03,'rope',null,0.2);
      let t=0.12; while(t<0.86){ const xx=hw+0.04+(px-hw-0.04)*t, w=0.16+rnd()*0.14, hh=0.30+rnd()*0.22;
        quad(out, [xx, y0-0.02, zz],[xx+w, y0-0.02, zz],[xx+w, y0-0.02, zz-hh],[xx, y0-0.02, zz-hh], 'goods'+(1+((rnd()*4)|0)), 0.25);
        quad(out, [xx+w, y0+0.02, zz],[xx, y0+0.02, zz],[xx, y0+0.02, zz-hh],[xx+w, y0+0.02, zz-hh], 'goods'+(1+((rnd()*4)|0)), -0.3);
        t += (w/(px-hw))+0.06+rnd()*0.05; }
    }
  }
  function flue(out, x, y, baseZ, topZ, brickTex){
    boxSolid(out, x-0.34,x+0.34, y-0.30,y+0.30, baseZ, topZ,'brick', brickTex, 0.0);
    boxSolid(out, x-0.40,x+0.40, y-0.36,y+0.36, topZ, topZ+0.16,'brick', null, 0.35);
    boxSolid(out, x-0.13,x+0.13, y-0.11,y+0.11, topZ+0.16, topZ+0.30,'dark', null, -0.4);
  }
  function stoop(out, c, y, w, fH){
    const n=Math.max(1, Math.round(fH/0.19)), d=0.34;
    for(let i=0;i<n;i++){ const z=fH*(1-(i)/n), yy=y+i*d;
      boxSolid(out, c-w/2-i*0.06, c+w/2+i*0.06, yy, yy+d+0.02, 0, Math.max(0.04,z),'stone', stoneTex(), -0.05); }
    for(const sx of [c-w/2-0.10, c+w/2+0.10]){
      boxSolid(out, sx-0.04,sx+0.04, y+0.05, y+0.11, fH, fH+0.92,'iron',null,0.1);
      boxSolid(out, sx-0.04,sx+0.04, y+n*d-0.10, y+n*d-0.04, 0.05, 0.95,'iron',null,0.1);
      quad(out, [sx-0.03, y+0.08, fH+0.92],[sx-0.03, y+n*d-0.07, 0.95],[sx-0.03, y+n*d-0.07, 0.88],[sx-0.03, y+0.08, fH+0.85],'iron',0.3); }
  }
  function wallLamp(out, plane, x, z){
    boxSolid(out, x-0.03,x+0.03, plane, plane+0.26, z, z+0.04,'iron',null,0.15);
    boxSolid(out, x-0.10,x+0.10, plane+0.16, plane+0.36, z-0.30, z,'lampGlass',null,0.1);
    boxSolid(out, x-0.13,x+0.13, plane+0.13, plane+0.39, z, z+0.10,'iron',null,0.35);
  }

  // ---- geometry resolve ------------------------------------------------------
  function resolve(opts){
    opts=opts||{};
    const P = opts.preset && PRESETS[opts.preset] ? PRESETS[opts.preset] : {};
    const g=(k,d)=> opts[k]!=null ? opts[k] : (P[k]!=null ? P[k] : d);
    const tk=g('type','generalStore'), T=TYPES[tk]||TYPES.generalStore;
    const size=g('size', T.size!=null?T.size:0.35);
    const b={
      type:tk, label:T.label, size,
      shape:  g('shape',  T.shape),
      pitch:  g('pitch',  T.pitch),
      siding: g('siding', T.siding),
      body:   g('body',   T.body),
      roof:   g('roof',   T.roof),
      storefront: g('storefront', T.storefront),
      windows: g('windows', T.windows),
      winD:   g('winDensity', T.winD),
      awning: g('awning', T.awning),
      awnExtend: g('awnExtend', 0.5),
      awnCols: g('awnCols', T.awnCols),
      fascia: g('fascia', T.fascia),
      bracket:g('bracket', T.bracket),
      sign:   g('sign',   T.sign),
      signTone: g('signTone', T.signTone),
      flat:   g('flat',   T.flat),
      stall:  g('stall',  T.stall),
      patio:  g('patio',  T.patio),
      sandwich:g('sandwich', T.sandwich),
      planters:g('planters', T.planters),
      load:   g('load',   T.load),
      scale:  g('scale',  T.scale),
      stacks: g('stacks', T.stacks)|0,
      lamp:   g('lamp', true),
      weather:g('weather', 0.3),
      night:  !!opts.night,
      seed:   g('seed', 7)|0,
    };
    b.Wd = T.Wd[0] + size*T.Wd[1];
    b.Ln = T.Ln[0] + size*T.Ln[1];
    b.wallH = T.wallH[0] + size*T.wallH[1];        // grade -> eave  (== interior contract)
    b.fH = T.fH;                                   // grade -> shop floor
    // ONE FOOTPRINT, NOT TWO THAT AGREE. shopInteriorRig snaps the shell to the plan's cell grid;
    // take its numbers verbatim when it is loaded, so the shell and the rooms inside it are the same
    // rectangle. The ranges above are the standalone fallback when this rig is used on its own.
    { const SI=root.ShopInterior;
      if(SI && SI.dims && SI.TYPES && SI.TYPES[tk]){
        try{ const D=SI.dims({ type:tk, size });
          b.Wd=D.bWd||D.Wd; b.Ln=D.bLn||D.Ln; b.wallH=D.wallH; b.fH=D.fH; b.shopH=D.shopH; b.si=true; }catch(e){}
      } }
    // shopH and the flat-above eave lift are already baked into the numbers above when they came
    // from the interior rig — running them again would compound the lift and raise the eave 0.25 m
    // over the rooms it is supposed to cover.
    if(!b.si){
      b.shopH = Math.min(3.10, Math.max(2.55, b.wallH - b.fH - 0.55));   // shop ceiling over its floor
      // a flat above the shop RAISES the eave — the shell has to be two storeys tall for the upper
      // sash to exist, and the interior rig reads wallH back to place its 'upper' room.
      if(b.flat) b.wallH = Math.max(b.wallH, b.fH + b.shopH + 0.34 + 2.35);
    }
    // REAR WING (kitchen / ice-house ell) — the mass a real back kitchen needs. The numbers are NOT
    // invented here: shopBuildingRig owns the plan, and wingOf(type,size) hands back the same
    // depth/width/offset/eave its wing rooms occupy, so the interior registers inside the shell.
    // Pass wing:false to suppress, or your own {depth,width,offset,wallH,pitch} to override.
    // Resolved BEFORE the roofline, because a shed has to be tall enough to cover its own ell.
    if(opts.wing===false) b.wing=null;
    else if(opts.wing) b.wing=opts.wing;
    else { b.wing=null;
      if(root.ShopBuilding && root.ShopBuilding.wingOf){ try{ b.wing=root.ShopBuilding.wingOf(tk,size); }catch(e){ b.wing=null; } } }
    b.rise = (b.Wd/2) * b.pitch;
    // A SHED FALLS TO THE BACK, NOT ACROSS THE STREET. Sloping in X put half the street elevation
    // under a low eave while the storefront head, fascia, awning and sash were all set off the HIGH
    // eave — so the glazing and the awning stood proud of the wall they hang on and read as clipping
    // through the roof. Falling in Y keeps the whole shop front at full height, and the drop is
    // bounded (and the wall raised to suit) so the back wall still carries its sash and covers the ell.
    if(b.shape==='shed'){
      b.drop = Math.max(0.55, Math.min(2.00, b.Ln * b.pitch * 0.30));
      b.wallH = Math.max(b.wallH, b.fH + (b.wing ? 3.10 : 2.83) + b.drop);
    }
    b.eaveZ = b.wallH;
    b.ridgeZ = b.shape==='shed' ? b.eaveZ : b.eaveZ + b.rise;
    b.ov = 0.34;
    b.storeyZ = b.fH + b.shopH + 0.34;             // flat-above floor plane
    return b;
  }
  function dims(opts){ const b=resolve(opts||{});
    return { Wd:b.Wd, Ln:b.Ln, wallH:b.wallH, fH:b.fH, shopH:b.shopH, eaveZ:b.eaveZ, ridgeZ:b.ridgeZ, storeyZ:b.storeyZ,
      shape:b.shape, pitch:b.pitch, type:b.type, label:b.label, wing:b.wing }; }

  function makeMats(b){
    const wx=b.weather, night=b.night;
    const grime=(r)=>r.map(c=>mix(desat(c, wx*0.26),'#4a4034', wx*0.13));
    const warm=(r,k)=> night ? r.map(c=>mix(mix(c,'#c98b3f',0.10),'#22190f',0.22+(k||0))) : r;
    const t=(r,k)=>warm(grime(r),k);
    const bodyRamp = Array.isArray(b.body)?b.body:(BODY[b.body]||BODY.cream);
    const roofRamp = b.roof==='metalSeam'?ROOFS.metal : b.roof==='corrugated'?GALV
                   : b.roof==='rusted'?RUST : (ROOFS[b.roof]||ROOFS.asphaltGrey);
    const aw = AWNINGS[b.awnCols]||AWNINGS.redCream;
    const signRamp = BODY[b.signTone]||SIGN;
    const rnd=mulberry32(4801 + b.seed*97);
    const goodsKeys=['red','blue','sage','cream','gold','teal','rustOrange','plum','white','mustard'];
    const goods={}; for(let i=1;i<=4;i++){ const k=goodsKeys[(rnd()*goodsKeys.length)|0]; goods['goods'+i]={ramp:t(BODY[k])}; }
    return Object.assign({
      body:   { ramp:t(bodyRamp) },
      roof:   { ramp:t(roofRamp) },
      trim:   { ramp:t(TRIM) },
      wood:   { ramp:t(WOOD) },
      crate:  { ramp:t(CRATE) },
      door:   { ramp:t(BODY[b.signTone]||WOOD) },
      stone:  { ramp:t(STONE) },
      brick:  { ramp:t(['#3a201a','#552b20','#6e3728','#874634','#a05743','#b96b55']) },
      cinder: { ramp:t(['#4a4842','#5f5d55','#77746a','#8f8b7f','#a4a094','#b8b3a6']) },
      steel:  { ramp:t(STEEL) },
      iron:   { ramp:t(IRON) },
      galv:   { ramp:t(GALV) },
      brass:  { ramp:t(BRASS) },
      worktop:{ ramp:t(['#3a3d43','#494d54','#5b6067','#70757c','#868b91','#9ca1a6']) },
      signF:  { ramp:t(signRamp) },
      signB:  { ramp:t(SIGN) },
      cvA:    { ramp:t(BODY[aw[0]]||BODY.red) },
      cvB:    { ramp:t(BODY[aw[1]]||CANVAS) },
      chalk:  { ramp:t(CHALK) },
      rope:   { ramp:t(['#6a5b3c','#7d6c48','#907e55','#a49163','#b7a472','#c9b783']) },
      ice:    { ramp:t(ICE) },
      fishA:  { ramp:t(['#3a4a52','#4c6069','#617781','#7a9099','#94a9b1','#b0c3c9']) },
      fishB:  { ramp:t(['#5c3a3a','#74494a','#8c5a5c','#a26f71','#b78688','#c99fa1']) },
      soil:   { ramp:t(['#221a12','#2d2318','#3a2d1f','#473827','#54432f','#614e38']) },
      leaf:   { ramp:t(['#22331f','#2e4429','#3c5734','#4c6b42','#5e8052','#729665']) },
      bloom:  { ramp:t(BODY.gold) },
      dial:   { ramp:t(TRIM) },
      lampGlass:{ ramp: night?['#8a5c1c','#c99433','#f2d689']:['#8fa8ae','#a9c0c4','#c6d9dc'] },
      glass:  { ramp: night?GLASSN:GLASSD },
      glassHi:{ ramp:[ night?'#f4dc9a':GLASS_HI ] },
      dark:   { ramp:['#0e1013','#141719','#1c2023'] },
    }, goods);
  }

  // ---- massing --------------------------------------------------------------
  function gableEnd(out, yv, ny, hw, eaveZ, ridgeZ, mat, tex){
    const A=[-hw,yv,eaveZ], B2=[hw,yv,eaveZ], C=[0,yv,ridgeZ];
    const uv = ny>0 ? [[0,eaveZ],[2*hw,eaveZ],[hw,ridgeZ]] : [[2*hw,eaveZ],[0,eaveZ],[hw,ridgeZ]];
    out.push(F(ny>0?[A,B2,C]:[B2,A,C], mat, 0, 0, uv, tex));
  }
  // A REAR WING: the ell behind the shop that a back kitchen actually needs. Gable with its ridge
  // running in Y, butting the main mass's back wall, eave held below the main eave so the two
  // masses read as one building. Geometry comes from wingOf() — the interior plan's own numbers.
  function massWing(out, b, siTex, rTex, m){
    const W=b.wing; if(!W || !(W.depth>0.8) || !(W.width>1.2)) return null;
    const hw=b.Wd/2, y0=-b.Ln/2, ov=0.26;
    // the plan's width, taken as given — clamping it narrower is what used to leave the wing rooms
    // poking out through the ell's side walls
    const half=Math.min(W.width, b.Wd)/2;
    const cxw=Math.max(-hw+half, Math.min(hw-half, W.offset||0));
    const x0=cxw-half, x1=cxw+half, yB=y0-W.depth;
    // the ell butts the BACK wall, so its ridge has to die under the main roof at that wall — an
    // unclamped ridge is what used to burst up through the shed plane
    const cap=(m&&m.wingCap!=null?m.wingCap:b.ridgeZ)-0.22;
    let eave=Math.max(b.fH+2.30, Math.min(b.eaveZ-0.40, W.wallH));
    let rise=Math.max(0.55, half*(W.pitch!=null?W.pitch:0.62));
    if(eave+rise>cap){
      eave=Math.max(b.fH+2.10, Math.min(eave, cap-0.55));
      rise=Math.max(0.45, Math.min(rise, cap-eave));
    }
    const ridge=eave+rise;
    boxSolid(out, x0-0.05, x1+0.05, yB, y0, 0, b.fH,'stone', stoneTex(), -0.1);
    wall(out, x0,yB, x0,y0, 0, eave,'body',siTex);
    wall(out, x1,y0, x1,yB, 0, eave,'body',siTex);
    wall(out, x1,yB, x0,yB, 0, eave,'body',siTex);
    out.push(F([[x1,yB,eave],[x0,yB,eave],[cxw,yB,ridge]],'body',0,0,
      [[2*half,eave],[0,eave],[half,ridge]],siTex));
    const yA=yB-ov;
    quad(out, [x0-ov,yA,eave],[x0-ov,y0,eave],[cxw,y0,ridge],[cxw,yA,ridge],'roof',-0.05,rTex);
    quad(out, [x1+ov,y0,eave],[x1+ov,yA,eave],[cxw,yA,ridge],[cxw,y0,ridge],'roof',0.15,rTex);
    wall(out, x0-ov,y0, x0-ov,yA, eave-0.20, eave,'trim',null,0.35);
    wall(out, x1+ov,yA, x1+ov,y0, eave-0.20, eave,'trim',null,0.35);
    for(const sgn of [-1,1]){ const ex=cxw+sgn*(half+ov);
      out.push(F([[ex,yA,eave],[cxw,yA,ridge],[cxw,yA,ridge-0.18],[ex,yA,eave-0.18]],'trim',0.5,0.05,null,null)); }
    for(const xv of [x0,x1]){ const t=0.08; boxSolid(out, xv-t,xv+t, yB-t,yB+t, b.fH*0.5, eave,'trim',null,0.2); }
    shopDoor(out,'y', yB, -1, cxw+half*0.34, b.fH, 1.02, 2.06, false);   // service entrance
    const sill=b.fH+1.15, wh=Math.min(1.05, eave-sill-0.30);
    if(wh>0.45){
      windowOn(out,'y', yB, -1, cxw-half*0.42, sill, 0.78, wh, b.windows);
      const n=Math.max(1, Math.round(W.depth/2.6));
      for(let i=0;i<n;i++){ const c=yB+W.depth*((i+0.6)/(n+0.2));
        windowOn(out,'x', x1, 1, c, sill, 0.78, wh, b.windows);
        if(2*half>3.2) windowOn(out,'x', x0, -1, c, sill, 0.78, wh, b.windows); }
    }
    return { x0, x1, yB, cx:cxw, eave, ridge, depth:W.depth, width:2*half };
  }

  function massGable(out, b, siTex, rTex){
    const hw=b.Wd/2, y0=-b.Ln/2, y1=b.Ln/2, ov=b.ov, eaveZ=b.eaveZ, ridgeZ=b.ridgeZ;
    wall(out, -hw,y0, -hw,y1, b.fH*0, eaveZ,'body',siTex);
    wall(out,  hw,y1,  hw,y0, 0, eaveZ,'body',siTex);
    wall(out,  hw,y0, -hw,y0, 0, eaveZ,'body',siTex);
    wall(out, -hw,y1,  hw,y1, 0, eaveZ,'body',siTex);
    gableEnd(out, y0,-1, hw, eaveZ, ridgeZ,'body',siTex);
    gableEnd(out, y1, 1, hw, eaveZ, ridgeZ,'body',siTex);
    const yA=y0-ov, yB=y1+ov;
    quad(out, [-hw-ov,yA,eaveZ],[-hw-ov,yB,eaveZ],[0,yB,ridgeZ],[0,yA,ridgeZ],'roof',-0.05,rTex);
    quad(out, [hw+ov,yB,eaveZ],[hw+ov,yA,eaveZ],[0,yA,ridgeZ],[0,yB,ridgeZ],'roof',0.15,rTex);
    wall(out, -hw-ov,yB, -hw-ov,yA, eaveZ-0.22, eaveZ,'trim',null,0.35);
    wall(out,  hw+ov,yA,  hw+ov,yB, eaveZ-0.22, eaveZ,'trim',null,0.35);
    for(const yv of [yA,yB]) for(const sgn of [-1,1]){ const ex=sgn*(hw+ov);
      out.push(F([[ex,yv,eaveZ],[0,yv,ridgeZ],[0,yv,ridgeZ-0.2],[ex,yv,eaveZ-0.2]],'trim',0.5,0.05,null,null)); }
    return { eaveZ, ridgeZ, topFront:eaveZ, frontTop:eaveZ, backTop:eaveZ,
      sideTopAt:()=>eaveZ, wingCap:eaveZ+(ridgeZ-eaveZ)*0.62 };
  }
  // a wall whose top edge climbs from zA at the first corner to zB at the second
  function wallRake(out, x0,y0, x1,y1, z0, zA, zB, mat, tex, b){
    const L=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,zB],[x0,y0,zA]], mat, b||0, 0, [[0,z0],[L,z0],[L,zB],[0,zA]], tex));
  }
  // SHED / skillion: HIGH AT THE STREET, falling to the back alley. The long walls carry the rake,
  // the street and back elevations are square, so every opening on the shop front sits under a
  // full-height wall.
  function massShed(out, b, siTex, rTex){
    const hw=b.Wd/2, y0=-b.Ln/2, y1=b.Ln/2, ov=b.ov;
    const eaveHi=b.eaveZ, eaveLo=b.eaveZ - (b.drop!=null?b.drop:Math.max(0.42,Math.min(1.60,b.Ln*b.pitch*0.22)));
    wallRake(out, -hw,y0, -hw,y1, 0, eaveLo, eaveHi,'body',siTex);
    wallRake(out,  hw,y1,  hw,y0, 0, eaveHi, eaveLo,'body',siTex);
    wall(out,  hw,y0, -hw,y0, 0, eaveLo,'body',siTex);
    wall(out, -hw,y1,  hw,y1, 0, eaveHi,'body',siTex);
    const yA=y0-ov, yB=y1+ov;
    quad(out, [hw+ov,yA,eaveLo],[-hw-ov,yA,eaveLo],[-hw-ov,yB,eaveHi],[hw+ov,yB,eaveHi],'roof',0.15,rTex);
    wall(out,  hw+ov,yA, -hw-ov,yA, eaveLo-0.22, eaveLo,'trim',null,0.35);
    wall(out, -hw-ov,yB,  hw+ov,yB, eaveHi-0.22, eaveHi,'trim',null,0.35);
    for(const xv of [-hw-ov, hw+ov])
      out.push(F([[xv,yA,eaveLo],[xv,yB,eaveHi],[xv,yB,eaveHi-0.2],[xv,yA,eaveLo-0.2]],'trim',0.5,0.05,null,null));
    const span=y1-y0;
    return { eaveZ:eaveHi, ridgeZ:eaveHi, topFront:eaveHi, eaveLo, eaveHi,
      frontTop:eaveHi, backTop:eaveLo, wingCap:eaveLo,
      sideTopAt:(y)=>eaveLo+(eaveHi-eaveLo)*Math.max(0,Math.min(1,(y-y0)/span)) };
  }
  function massGambrel(out, b, siTex, rTex){
    const hw=b.Wd/2, y0=-b.Ln/2, y1=b.Ln/2, ov=0.3, eaveZ=b.eaveZ, topZ=b.eaveZ+b.rise+0.3, brk=hw*0.5, midZ=eaveZ+(topZ-eaveZ)*0.55;
    wall(out, -hw,y0, -hw,y1, 0, eaveZ,'body',siTex);
    wall(out,  hw,y1,  hw,y0, 0, eaveZ,'body',siTex);
    wall(out,  hw,y0, -hw,y0, 0, eaveZ,'body',siTex);
    wall(out, -hw,y1,  hw,y1, 0, eaveZ,'body',siTex);
    for(const [yv,ny] of [[y0,-1],[y1,1]]){
      const pts = ny>0 ? [[-hw,yv,eaveZ],[hw,yv,eaveZ],[brk,yv,midZ],[0,yv,topZ],[-brk,yv,midZ]]
                       : [[hw,yv,eaveZ],[-hw,yv,eaveZ],[-brk,yv,midZ],[0,yv,topZ],[brk,yv,midZ]];
      out.push(F(pts,'body',0,0,null,null));
    }
    const yA=y0-ov, yB=y1+ov;
    quad(out, [-hw-ov,yA,eaveZ],[-hw-ov,yB,eaveZ],[-brk,yB,midZ],[-brk,yA,midZ],'roof',-0.05,rTex);
    quad(out, [hw+ov,yB,eaveZ],[hw+ov,yA,eaveZ],[brk,yA,midZ],[brk,yB,midZ],'roof',0.15,rTex);
    quad(out, [-brk,yA,midZ],[-brk,yB,midZ],[0,yB,topZ],[0,yA,topZ],'roof',0.0,rTex);
    quad(out, [brk,yB,midZ],[brk,yA,midZ],[0,yA,topZ],[0,yB,topZ],'roof',0.2,rTex);
    wall(out, -hw-ov,yB, -hw-ov,yA, eaveZ-0.22, eaveZ,'trim',null,0.35);
    wall(out,  hw+ov,yA,  hw+ov,yB, eaveZ-0.22, eaveZ,'trim',null,0.35);
    return { eaveZ, ridgeZ:topZ, topFront:eaveZ, midZ, frontTop:eaveZ, backTop:eaveZ,
      sideTopAt:()=>eaveZ, wingCap:midZ };
  }
  // gable mass behind a tall flat parapet on the street elevation — the maritime store front
  function massFalseFront(out, b, siTex, rTex){
    const m=massGable(out, b, siTex, rTex);
    const hw=b.Wd/2, y1=b.Ln/2, ov=0.14, fw=hw+ov;
    // the false front is a BOARD ABOVE THE EAVE, flush with the street elevation — it squares off the
    // roofline without covering the storefront below it.
    const baseZ=b.eaveZ-0.34, pz=b.eaveZ + 0.72 + b.rise*0.30;
    out.push(F([[-fw,y1+0.22,baseZ],[fw,y1+0.22,baseZ],[fw,y1+0.22,pz],[-fw,y1+0.22,pz]],'body',0,0,
      [[0,baseZ],[2*fw,baseZ],[2*fw,pz],[0,pz]], siTex));                            // parapet face
    out.push(F([[fw,y1+0.06,baseZ],[-fw,y1+0.06,baseZ],[-fw,y1+0.06,pz],[fw,y1+0.06,pz]],'body',-0.5,0,null,null));
    for(const sgn of [-1,1]) wall(out, sgn*fw, y1+0.06, sgn*fw, y1+0.22, baseZ, pz,'body',siTex);
    slab(out, [[-fw,y1+0.06],[fw,y1+0.06],[fw,y1+0.22],[-fw,y1+0.22]], pz,'trim',0.55);
    boxSolid(out, -fw-0.14, fw+0.14, y1+0.02, y1+0.32, pz, pz+0.20,'trim',null,0.55);   // cornice
    boxSolid(out, -fw-0.08, fw+0.08, y1+0.08, y1+0.28, pz-0.44, pz-0.30,'trim',null,0.5);// frieze band
    return Object.assign({}, m, { eaveZ:m.eaveZ, ridgeZ:pz+0.20, topFront:pz-0.55, parapet:pz });
  }

  // ---- storefront on the +Y street elevation -------------------------------
  function storefront(out, b, m, rnd){
    const hw=b.Wd/2, y1=b.Ln/2, fH=b.fH, sf=b.storefront;
    const sillZ = fH + 0.62;                                        // bulkhead height
    const headZ = Math.min(m.eaveZ-0.55, fH + 2.62);
    const doorH = Math.min(2.24, headZ-fH-0.05);
    let doorX = 0, glassTop = headZ;
    // bulkhead / stall riser under the glass
    const bulk=(a,bx)=>{ decalY(out, y1,1, a, bx, fH, sillZ,'wood',0.1, boardTex(), false, 0.04);
      decalY(out, y1,1, a, bx, sillZ, sillZ+0.10,'trim',0.7,null,true,0.06);
      decalY(out, y1,1, a, bx, fH-0.02, fH+0.12,'trim',0.4,null,true,0.06); };

    if(sf==='bay'){
      doorX = 0;
      const dw=1.12, panW=(b.Wd-dw-1.0)/2;
      for(const sgn of [-1,1]){ const c=sgn*(dw/2+0.28+panW/2);
        bulk(c-panW/2, c+panW/2);
        glazedBay(out,'y', y1, 1, c, sillZ+0.14, panW, glassTop-sillZ-0.28, 2, 1,'wood');
        decalY(out, y1,1, c-panW/2-0.06, c+panW/2+0.06, glassTop-0.14, glassTop,'trim',0.75,null,true,0.06);
        // stepped goods display behind the glass
        for(let k=0;k<2;k++){ const zz=sillZ+0.16+k*0.30, ya=y1-0.52+k*0.16;
          goodsRow(out, c-panW/2+0.10, c+panW/2-0.10, ya, ya+0.26, zz, 0.26, rnd, 0.9, ['box','tin','crateB']); }
      }
      shopDoor(out,'y', y1, 1, doorX, fH, dw, doorH, true);
    } else if(sf==='plate'){
      doorX = -b.Wd*0.28;
      const dw=1.14, gA=-hw+0.42, gB=hw-0.42;
      const gLeft=doorX+dw/2+0.30;
      bulk(gLeft, gB);
      glazedBay(out,'y', y1, 1, (gLeft+gB)/2, sillZ+0.12, gB-gLeft, glassTop-sillZ-0.26, Math.max(2,Math.round((gB-gLeft)/1.35)), 1,'wood');
      decalY(out, y1,1, gLeft-0.06, gB+0.06, glassTop-0.16, glassTop,'trim',0.75,null,true,0.06);
      for(let k=0;k<2;k++){ const zz=sillZ+0.16+k*0.34, ya=y1-0.60+k*0.18;
        goodsRow(out, gLeft+0.14, gB-0.14, ya, ya+0.30, zz, 0.30, rnd, 0.9, ['box','tin','bottle']); }
      if(doorX-dw/2-0.30 > gA){ bulk(gA, doorX-dw/2-0.30);
        glazedBay(out,'y', y1, 1, (gA+doorX-dw/2-0.30)/2, sillZ+0.12, (doorX-dw/2-0.30)-gA, glassTop-sillZ-0.26, 1, 1,'wood'); }
      shopDoor(out,'y', y1, 1, doorX, fH, dw, doorH, true);
    } else if(sf==='smallPane'){
      doorX = b.Wd*0.26;
      const dw=1.05, gA=-hw+0.40, gB=doorX-dw/2-0.32;
      bulk(gA, gB);
      glazedBay(out,'y', y1, 1, (gA+gB)/2, sillZ+0.14, gB-gA, Math.min(1.62, glassTop-sillZ-0.3), Math.max(3,Math.round((gB-gA)/0.62)), 3,'wood');
      goodsRow(out, gA+0.12, gB-0.12, y1-0.52, y1-0.20, sillZ+0.16, 0.28, rnd, 0.9, ['box','tin','bolt','crateB']);
      shopDoor(out,'y', y1, 1, doorX, fH, dw, doorH, true);
      windowOn(out,'y', y1, 1, doorX+ (hw-doorX)*0.5 + 0.1, fH+1.05, 0.82, 1.15, b.windows);
    } else if(sf==='hatch'){
      doorX = -b.Wd*0.30;
      const hwid=Math.min(b.Wd*0.52, 2.9), hc=b.Wd*0.10;
      serviceHatch(out, y1, hc, fH+0.98, hwid, 1.12, !(b.awning && b.awning!=='none'));
      shopDoor(out,'y', y1, 1, doorX, fH, 0.98, Math.min(2.1, doorH), false);
    } else { // narrow
      doorX = -b.Wd*0.24;
      shopDoor(out,'y', y1, 1, doorX, fH, 1.08, doorH, true);
      const gA=doorX+0.86, gB=hw-0.44;
      if(gB-gA>0.9){ bulk(gA,gB);
        glazedBay(out,'y', y1, 1, (gA+gB)/2, sillZ+0.18, gB-gA, Math.min(1.5, glassTop-sillZ-0.36), Math.max(2,Math.round((gB-gA)/0.72)), 2,'wood');
        goodsRow(out, gA+0.12, gB-0.12, y1-0.48, y1-0.18, sillZ+0.20, 0.24, rnd, 0.85, ['box','bottle','tin']); }
    }
    stoop(out, doorX, y1+0.02, 1.42, fH);
    return { doorX, headZ, sillZ };
  }

  // ---- assemble -------------------------------------------------------------
  function build(b){
    const out=[];
    const siTex=sidingTex(b.siding), rTex=roofTexFor(b.roof), brickTex=(u,v)=>{
      const c=0.16, bl=0.40, row=Math.floor(v/c), off=(row&1)*0.5*bl, fv=((v%c)+c)%c, su=(((u+off)%bl)+bl)%bl;
      if(fv<0.03) return -2; if(su<0.035) return -2; if(fv>c-0.03) return 1; return 0; };
    const rnd=mulberry32(9137 + b.seed*613 + ((b.size*191)|0));
    const hw=b.Wd/2, y0=-b.Ln/2, y1=b.Ln/2;

    // pavement pad + granite kerb along the street
    const pad = 2.35 + (b.patio?0.85:0);
    slab(out, [[-hw-0.8, y1],[hw+0.8, y1],[hw+0.8, y1+pad],[-hw-0.8, y1+pad]], 0.03,'cinder',-3.6, stoneTex());
    boxSolid(out, -hw-0.8, hw+0.8, y1+pad-0.18, y1+pad, 0, 0.13,'stone', null, -0.5);

    // FOUNDATION
    boxSolid(out, -hw-0.05, hw+0.05, y0, y1, 0, b.fH,'stone', stoneTex(), -0.1);

    // MASS
    const m = b.shape==='shed' ? massShed(out,b,siTex,rTex)
            : b.shape==='gambrel' ? massGambrel(out,b,siTex,rTex)
            : b.shape==='falseFront' ? massFalseFront(out,b,siTex,rTex)
            : massGable(out,b,siTex,rTex);
    const wg = massWing(out,b,siTex,rTex,m);
    const sideTop = m.sideTopAt || (()=>m.eaveZ);
    const backTop = m.backTop!=null?m.backTop:m.eaveZ;
    const frontTop = m.frontTop!=null?m.frontTop:m.eaveZ;
    // NOTHING PUNCHES THROUGH A WALL TOP. Every opening is fitted under the wall it sits in — shrunk
    // to the headroom that is actually there, dropped entirely when there isn't enough.
    const fitWin=(axis,plane,nrm,c,sill,ww,wh,top)=>{
      const h=Math.min(wh, top-0.26-sill);
      if(h<0.52) return false;
      windowOn(out,axis,plane,nrm,c,sill,ww,h,b.windows); return true; };

    // CORNERBOARDS
    const cMat = b.siding==='corrugated' ? 'steel' : 'trim';
    for(const [xv,yv,tz] of [[-hw,y0,backTop],[hw,y0,backTop],[-hw,y1,frontTop],[hw,y1,frontTop]]){
      const t=0.085; boxSolid(out, xv-t,xv+t, yv-t,yv+t, b.fH*0.5, tz, cMat, null, 0.2);
    }

    // STOREFRONT
    const sfr = storefront(out, b, m, rnd);

    // FASCIA SIGNBOARD over the storefront — the awning tucks in just below it
    const fascZ = Math.min(m.topFront-0.30, sfr.headZ+0.18);
    const fascH = Math.min(0.80, Math.max(0.42, (m.topFront-fascZ)-0.12));
    if(b.fascia) fasciaBand(out, y1, hw, fascZ, fascH, 0.20);

    // FLAT ABOVE — upper sash on the street + long walls, laundry line off the back corner
    const upSill = b.storeyZ + 1.0;
    if(b.flat && upSill+1.15 < frontTop-0.12){
      const nU=Math.max(2, Math.round(b.Wd/2.3));
      for(let i=0;i<nU;i++){ const c=-hw+b.Wd*((i+0.5)/nU); fitWin('y', y1,1, c, upSill, 0.82, 1.15, frontTop); }
      for(const [xv,nx] of [[-hw,-1],[hw,1]]){
        const nS=Math.max(1, Math.round(b.Ln/3.0));
        for(let i=0;i<nS;i++){ const c=y0+b.Ln*((i+0.5)/nS); fitWin('x', xv,nx, c, upSill, 0.82, 1.15, sideTop(c)); }
      }
      laundryLine(out, hw, y0+0.9, y1, b.storeyZ+0.85, rnd);
    }

    // LONG-WALL + BACK sash on the shop storey
    const sillG=b.fH+1.0, ww=0.82, wh=1.15;
    const nW=Math.max(1, Math.round(b.Ln/2.8*(0.5+b.winD)));
    for(const [xv,nx] of [[-hw,-1],[hw,1]]){
      for(let i=0;i<nW;i++){ const c=y0+b.Ln*((i+0.5)/nW);
        if(nx>0 && b.load && Math.abs(c-(y0+b.Ln*0.30))<1.6) continue;
        fitWin('x', xv,nx, c, sillG, ww,wh, sideTop(c)); }
    }
    for(const c of [-hw*0.44, hw*0.44]) fitWin('y', y0,-1, c, sillG, ww,wh, backTop);

    // LOADING DOOR + platform scale on +X
    if(b.load){
      const c=y0+b.Ln*0.30, dw=Math.min(2.5, b.Ln*0.34), dh=Math.min(sideTop(c)-b.fH-0.5, 2.7);
      loadingDoor(out, hw, c, b.fH, dw, dh);
      boxSolid(out, hw+0.02, hw+1.55, c-dw/2-0.3, c+dw/2+0.3, 0, b.fH,'cinder',null,-0.05);   // dock apron
      if(b.scale) platformScale(out, hw+0.95, c+dw/2+1.05);
      iceTray(out, hw+0.30, hw+1.30, c-dw/2-0.2, c+dw/2-0.6, b.fH);
    }

    // AWNING over the storefront, hung just under the sign band
    if(b.awning && b.awning!=='none'){
      const ext=0.9 + b.awnExtend*1.5;
      const az=Math.min((b.fascia?fascZ-0.05:sfr.headZ+0.12), m.topFront-0.14);
      awning(out, y1+0.03, hw, az, ext, b.awning, 0.16);
    }

    // BRACKET SIGN off the street elevation
    if(b.bracket){
      const bx=hw-0.55, bz=Math.min(m.topFront-0.35, b.fH+2.95);
      bracketSign(out, y1+0.04, bx, bz, 1.02, 0.74, b.sign);
    }

    // DOOR LAMP
    if(b.lamp) wallLamp(out, y1+0.04, sfr.doorX + 0.86, b.fH+2.30);

    // STREET FURNITURE on the walk
    if(b.stall) stall(out, -hw*0.34, y1+ (b.awning&&b.awning!=='none' ? 1.62+b.awnExtend*0.5 : 1.05), Math.min(2.4, b.Wd*0.46), 0.95,
      mulberry32(311+b.seed*13), b.type==='fishMarket'?['crateB','box','tin']:b.type==='chandlery'?['bolt','box','tin','crateB']:['box','tin','sack','crateB']);
    if(b.patio){
      const py = y1 + (b.awning&&b.awning!=='none' ? 1.35+b.awnExtend*1.5 : 1.35);
      patioSet(out, -hw*0.46, py+0.28, true, rnd);
      patioSet(out,  hw*0.42, py-0.10, b.type!=='takeoutStand', rnd);
    }
    if(b.sandwich) sandwichBoard(out, hw*0.62, y1+0.92, 0);
    if(b.planters){ planter(out, -hw+0.42, y1+0.52); planter(out, hw-0.42, y1+0.52); }
    if(b.type==='chandlery' || b.type==='generalStore'){ barrel(out, -hw+0.55, y1+0.50, 0.78); }

    // FLUES near the ridge (bake oven / kitchen)
    const ns=Math.min(2, b.stacks|0);
    for(let i=0;i<ns;i++){ const sy=y0 + b.Ln*((i+1)/(ns+1)); flue(out, -hw*0.42, sy, m.eaveZ-1.0, m.ridgeZ+1.15, brickTex); }

    return out;
  }

  // ---- rasterizer ----------------------------------------------------------
  function paint(faces, opts, MATS){
    const B=camBasis(opts), N=W*H;
    const zbuf=new Float32Array(N).fill(Infinity), dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null), ibuf=new Int16Array(N), nbuf=new Array(N).fill(null);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce);
      if(sh<0 && (f.b<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx=sh*GAIN + BIAS + f.b;
      const M=MATS[f.mat]||MATS.body, ramp=M.ramp, off=M.off||0, tex=f.tex, uv=f.uv, flat=f.flat;
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
            zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat;
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
    return { rbuf, ibuf, nbuf, dep };
  }
  function post(bufs, b){
    const { rbuf, ibuf, nbuf, dep }=bufs, N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j; out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; } } }
    const wx=b.weather;
    if(wx>0.02){ const rnd=mulberry32(1451|((b.size*97)|0));
      const rustRoof=(b.roof==='rusted');
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='body'||m==='wood'||m==='crate'||m==='signF'||m==='signB'||m==='cinder'||m==='galv') && rnd()<wx*0.06)
          out[i]=rbuf[i][Math.max(0,Math.min(rbuf[i].length-1,ibuf[i]-1))];
        if(m==='roof'){ if(rustRoof){ if(rnd()<wx*0.05) out[i]=mix(out[i],'#7a4a2c',0.3+rnd()*0.2); }
          else if(rnd()<wx*0.035) out[i]=mix(out[i],'#47543c',0.26+rnd()*0.16); }
        if((m==='cvA'||m==='cvB') && rnd()<wx*0.05) out[i]=rbuf[i][Math.max(0,ibuf[i]-1)];
      } }
    if(b.night){ for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x;
      if(nbuf[i]==='glass'||nbuf[i]==='lampGlass'){ for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*W+(x+dx);
        if(out[j] && nbuf[j]!=='glass' && nbuf[j]!=='glassHi' && nbuf[j]!=='lampGlass') out[j]=mix(out[j],'#f0c66a',0.26); } } } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; } }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
      if(touch) out[i]=KEY; }
    return out;
  }
  function toRGBA(cols){
    const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; }
      const [r,g,bl]=hex2rgb(c); rgba[i*4]=r; rgba[i*4+1]=g; rgba[i*4+2]=bl; rgba[i*4+3]=255; }
    return rgba;
  }

  function render(dir, opts){
    opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    let b=resolve(opts); const MATS=makeMats(b); let faces=build(b);
    const LC=root.BuildingLifecycle;                       // construction phase / dereliction pass
    if(LC && LC.active(opts)){ const r=LC.apply(faces, MATS, b, opts); faces=r.faces; b=r.b; }
    return toRGBA(post(paint(faces, {dir, elev:opts.elev}, MATS), b));
  }
  function anchors(dir, opts){
    opts=opts||{}; const b=resolve(opts), B=camBasis({dir,elev:opts.elev});
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return {x:v.sx,y:v.sy}; };
    const hw=b.Wd/2, y0=-b.Ln/2, y1=b.Ln/2;
    const dx = b.storefront==='plate' ? -b.Wd*0.28 : b.storefront==='smallPane' ? b.Wd*0.26
             : b.storefront==='hatch' ? -b.Wd*0.30 : b.storefront==='narrow' ? -b.Wd*0.24 : 0;
    const ns=Math.min(2,b.stacks|0), st=[];
    for(let i=0;i<ns;i++){ const sy=y0+b.Ln*((i+1)/(ns+1)); st.push(pj(-hw*0.42, sy, b.ridgeZ+1.45)); }
    return {
      door:  pj(dx, y1, b.fH),
      queue: pj(dx, y1+1.05, 0),
      hatch: b.storefront==='hatch' ? pj(b.Wd*0.10, y1+0.4, b.fH+0.98) : null,
      sign:  pj(0, y1+0.30, Math.min(b.ridgeZ-0.4, b.fH+3.0)),
      bracket: b.bracket ? pj(hw-0.55, y1+1.0, b.fH+2.6) : null,
      awning: (b.awning&&b.awning!=='none') ? pj(0, y1+0.9+b.awnExtend*1.5, b.fH+2.4) : null,
      stall: b.stall ? pj(-hw*0.34, y1+(b.awning&&b.awning!=='none'?1.62+b.awnExtend*0.5:1.05), 0.9) : null,
      patio: b.patio ? [pj(-hw*0.46, y1+1.63+b.awnExtend*1.5, 0), pj(hw*0.42, y1+1.25+b.awnExtend*1.5, 0)] : [],
      loadDoor: b.load ? pj(hw, y0+b.Ln*0.30, b.fH) : null,
      lamp:  b.lamp ? pj(dx+0.86, y1+0.30, b.fH+2.06) : null,
      stacks: st, ridge: pj(0,0,b.ridgeZ),
      wing: b.wing ? { depth:b.wing.depth, width:b.wing.width, offset:b.wing.offset, eave:b.wing.wallH,
        serviceDoor: pj(Math.max(-hw,Math.min(hw,(b.wing.offset||0)+b.wing.width*0.17)), y0-b.wing.depth, b.fH),
        back: pj(b.wing.offset||0, y0-b.wing.depth, 0) } : null,
      Wd:b.Wd, Ln:b.Ln, fH:b.fH, wallH:b.wallH, storeyZ:b.storeyZ, type:b.type, shape:b.shape,
    };
  }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.Shopfront = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    TYPES, SHAPES, SIDINGS, ROOFS:ROOF_KEYS, STOREFRONTS, SIGNS, AWNINGS, WINDOWS, BODY, TRIM, SIGN,
    STONE, WOOD, STEEL, IRON, GALV, RUST, BRASS, CANVAS, ICE, KEY, PRESETS,
    dims, render, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
