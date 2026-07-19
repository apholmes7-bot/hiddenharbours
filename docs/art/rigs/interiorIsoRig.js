/* Hidden Harbours — parametric ISO INTERIOR rig (ADR-0006 bake pipeline, SAME turntable + camera +
   shading as houseIsoRig.js / wharfBuildingRig.js / the fleet / characterIsoRig.js). One parametric
   3D ROOM, built from a footprint (the SAME Wd/Ln/wallH surface the building rigs resolve) baked to
   pixel sheets through the SHARED 3/4 camera: 45deg steps, elev 40deg default, flat-facet shading
   from the fixed upper-LEFT key, z-buffered, ordered dither, per-face uv texture, depth-edge
   darkening, 1px keyline, NO AA. 32 px = 1 m. All 8 facings fall out of one model.

   THE INTERIOR IS THE BUILDING SEEN FROM INSIDE. The footprint that drives the exterior shell
   (houseIsoRig / wharfBuildingRig: Wd, Ln, wallH, size) drives the interior's OUTER walls 1:1, so
   a "cottage" interior registers under a "cottage" exterior. OPEN-DOLLHOUSE cutaway: the two walls
   whose outward face points at the camera are dropped, so you look in over them; which two drop
   swaps per facing (a clean back V at the diagonals, a back-U at the orthogonals). NO ceiling.

   THE BUILDER SURFACE (every axis resolved per render, no re-modelling):
     size:   0..1   cottage(~6x7m) -> farmhouse(~8x11m)   — mirrors houseIsoRig exactly
     floor:  'plank'|'wideBoard'|'checker'|'stoneFlag'|'painted'   floorTone: FLOOR key / BODY key
     wall:   'plaster'|'wainscot'|'wallpaper'|'board'|'stud'|'stone'|'brick'   (interior finish)
     paper:  BODY key (wallpaper/paint hue)   wainscot:bool (beadboard dado on any finish)
     windows:'sixOverSix'|'twoOverTwo'|...   winDensity:0..1   door:bool (front doorway)
     dividers:0..2 (interior partition walls w/ a door gap — multiple rooms from one footprint)
     hearth:bool (chimney breast + firebox on the back wall, tied to the exterior chimney)
     weather:0..1 (grime + scuffed floor)   night:bool (warm lamplit; windows go cool/night)
   ANIM: rooms are static; lamp flicker + hearth fire are runtime overlays — anchors(dir,opts) ->
     { floor:{x,y}, door:{x,y}, hearth:{x,y}|null, lamps:[{x,y}], Wd, Ln } in cell px.
   Exposes globalThis.InteriorIso = { W,H,PX,DIRS,pivot,order,defaultElev, FLOORS,WALLS,WINDOWS,
     BODY,TRIM,FLOORWOOD,STONE,BRICK,TILE,PLASTER,PRESETS, render(dir,opts), anchors(dir,opts),
     project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1180, H = 900, cx = 590, groundY = 560;
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
  };
  const TRIM = ['#9aa09a','#b4b8b0','#ccd0c7','#e0e2da','#eef0e8','#f8f9f2'];
  const PLASTER  = ['#6f6350','#877a63','#a2947a','#bcae92','#d3c6a9','#e7dcc2'];
  const WOOD     = ['#4f3a24','#63492d','#785a39','#8f7049','#a6875d','#bd9f74'];
  const FLOORWOOD= ['#563820','#6c472a','#855a36','#9e7247','#b78c5e','#cea679'];
  const STONE    = ['#3a3d43','#494d54','#5b6067','#70757c','#868b91','#9ca1a6'];
  const BRICK    = ['#3a201a','#552b20','#6e3728','#874634','#a05743','#b96b55'];
  const TILE     = ['#20262b','#2b333a','#3a444c','#a9b0af','#c4cbc7','#dee3de']; // dark 0-2, light 3-5
  const CAVITY   = ['#201811','#291f15','#33271a','#3e2f20','#493827','#54432f'];
  const DOORDK   = ['#100c0f','#171216','#20191e','#2a212700'.slice(0,7),'#332830'];
  const GLASSD   = ['#7d9ea6','#a1c2c6','#cfe6e8'];        // day: bright exterior through the pane
  const GLASSN   = ['#141d2b','#233247','#3d5570'];        // night: cool dark sky
  const GLASS_HI = '#dff0f1';
  const KEY = '#1a1c22';
  const FIRE = ['#7a2a10','#b5541a','#e59433','#f6cf6a'];

  const FLOORS = ['plank','wideBoard','checker','stoneFlag','painted'];
  const WALLS  = ['plaster','wainscot','wallpaper','board','stud','stone','brick'];
  const STAIRS = ['none','straight'];
  const WINDOWS= ['sixOverSix','twoOverTwo','fourOverFour','oneOverOne','arched'];
  const WINSTYLES = {
    sixOverSix:   { v:2, r:[0.25,0.5,0.75] },
    fourOverFour: { v:1, r:[0.25,0.5,0.75] },
    twoOverTwo:   { v:1, r:[0.5] },
    oneOverOne:   { v:0, r:[0.5] },
    arched:       { v:1, r:[0.5], arch:true },
  };

  // presets MIRROR the building rigs (size => same metric footprint as houseIsoRig)
  const PRESETS = {
    keeperKitchen:  { size:0.20, floor:'checker',   floorTone:'slate',  wall:'plaster',   paper:'cream', wainscot:true,  windows:'sixOverSix', dividers:1, hearth:false, weather:0.30 },
    cottageBedroom: { size:0.20, floor:'plank',     wall:'wallpaper', paper:'red',   wainscot:false, windows:'twoOverTwo', dividers:0, hearth:true,  weather:0.28 },
    seasideParlor:  { size:0.42, floor:'wideBoard', wall:'wallpaper', paper:'sage',  wainscot:true,  windows:'sixOverSix', dividers:0, hearth:true,  weather:0.24 },
    netLoft:        { size:0.55, floor:'wideBoard', wall:'board',     paper:'greyShingle', wainscot:false, windows:'twoOverTwo', dividers:0, hearth:false, weather:0.55 },
    stoneCellar:    { size:0.30, floor:'stoneFlag', wall:'stone',     paper:'greyShingle', wainscot:false, windows:'oneOverOne', dividers:0, hearth:false, weather:0.6 },
    framingRoom:    { size:0.35, floor:'plank',     wall:'stud',      paper:'cream', wainscot:false, windows:'twoOverTwo', dividers:1, hearth:false, weather:0.4 },
    farmhouseHall:  { size:0.72, floor:'plank',     wall:'plaster',   paper:'blue',  wainscot:true,  windows:'sixOverSix', dividers:2, hearth:true,  weather:0.18 },
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

  // ---- camera / projection (identical to houseIsoRig, so interiors composite with the fleet) ----
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

  // ---- face builders (F = {v, mat, b, db, uv, tex, flat}) ---------------------
  let CUR_LAYER='base';
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat, layer:CUR_LAYER }; }
  function wall(out, x0,y0,x1,y1, z0,z1, mat, tex, b){
    const L=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]], mat, b||0, 0,
      [[0,z0],[L,z0],[L,z1],[0,z1]], tex));
  }
  function slab(out, pts, z, mat, b, tex){
    const uv = tex ? pts.map(p=>[p[0],p[1]]) : null;
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0, uv, tex));
  }
  function boxSolid(out, x0,x1, y0,y1, z0,z1, mat, tex, b){
    wall(out, x0,y0, x1,y0, z0,z1, mat, tex, b);
    wall(out, x1,y1, x0,y1, z0,z1, mat, tex, b);
    wall(out, x1,y0, x1,y1, z0,z1, mat, tex, b);
    wall(out, x0,y1, x0,y0, z0,z1, mat, tex, b);
    slab(out, [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, (b||0)+0.25);
  }

  // ---- decals on wall planes (finish bands, casings, glass) -------------------
  function decalY(out, yv, ny, xs,xe, z0,z1, mat, b, tex, flat, db){
    const e=0.02*ny, uw=xe-xs, uh=z1-z0;
    const P = ny>0
      ? [[xs,yv+e,z0],[xe,yv+e,z0],[xe,yv+e,z1],[xs,yv+e,z1]]
      : [[xe,yv+e,z0],[xs,yv+e,z0],[xs,yv+e,z1],[xe,yv+e,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat));
  }
  function decalX(out, xv, nx, ys,ye, z0,z1, mat, b, tex, flat, db){
    const e=0.02*nx, uw=ye-ys, uh=z1-z0;
    const P = nx>0
      ? [[xv+e,ye,z0],[xv+e,ys,z0],[xv+e,ys,z1],[xv+e,ye,z1]]
      : [[xv+e,ys,z0],[xv+e,ye,z0],[xv+e,ye,z1],[xv+e,ys,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.05, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat));
  }
  // put(axis, plane, nrm, a0,a1, z0,z1, mat, bias, db) — a decal on an ±X or ±Y interior plane
  const putOn=(axis)=>(out,plane,nrm,a0,a1,z0,z1,mat,bias,db,tex,flat)=> axis==='y'
      ? decalY(out,plane,nrm,a0,a1,z0,z1,mat,bias,tex||null,flat!==false&&!tex?true:!!flat,db)
      : decalX(out,plane,nrm,a0,a1,z0,z1,mat,bias,tex||null,flat!==false&&!tex?true:!!flat,db);

  // ---- textures (return integer ramp delta) ----------------------------------
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }

  function floorTex(kind){
    if(kind==='checker'){ const c=0.52;
      return (u,v)=>{ const cxx=Math.floor(u/c), cyy=Math.floor(v/c);
        const light=((cxx+cyy)&1)?3:0;
        const fu=((u%c)+c)%c, fv=((v%c)+c)%c;
        const grout=(fu<0.045||fv<0.045)?-1:0;
        const sheen=(!light && fu>c*0.55 && fv<c*0.4)?1:0;   // faint catch on dark tiles
        return light+grout+sheen; }; }
    if(kind==='stoneFlag'){ const c=0.62;
      return (u,v)=>{ const cxx=Math.floor(u/c), cyy=Math.floor(v/c);
        const fu=((u%c)+c)%c, fv=((v%c)+c)%c, r=hash2(cxx|0,cyy|0);
        if(fu<0.06||fv<0.06) return -2;                     // mortar
        return (r<0.33?-1:(r>0.72?1:0)) + ((fu<0.12||fv<0.12)?0:(hash2(cxx*3|0,cyy*7|0)<0.08?-1:0)); }; }
    const PW = kind==='wideBoard'?0.82:0.42;                // plank / wideBoard: run along +Y
    return (u,v)=>{ const p=Math.floor(u/PW); const fu=((u%PW)+PW)%PW;
      const tone = (hash2(p|0,0)<0.5?0:1) - (hash2(p|0,3)<0.3?1:0);
      if(fu<0.035) return -2;                               // board seam
      const butt=(((v% (2.4))+2.4)%2.4); if(butt<0.05 && (Math.floor(v/2.4)+p)&1) return -2; // staggered butt joint
      const knot = hash2(p*5|0, Math.floor(v/1.7)|0)<0.04 ? -1 : 0;
      return tone+knot; };
  }
  function wallTex(kind){
    if(kind==='plaster') return (u,v)=>{ const m=Math.sin(u*2.3+v*0.7)*Math.sin(v*1.9-u*0.4);
      return m>0.86?-1:(m<-0.9?1:0); };
    if(kind==='wallpaper') return (u,v)=>{ const st=0.16, f=((u%st)+st)%st;
      const stripe=(f<st*0.5)?0:-1;
      const motif=(hash2(Math.round(u/0.24), Math.round(v/0.24))<0.10)?1:0;
      return stripe+motif; };
    if(kind==='board') return (u,v)=>{ const bw=0.24, f=((u%bw)+bw)%bw;   // vertical shiplap
      if(f<0.035) return -2; if(f>bw-0.03) return 1; return 0; };
    if(kind==='stone'){ const c=0.34, bl=0.62;
      return (u,v)=>{ const row=Math.floor(v/c), off=(row&1)*0.5*bl;
        const fv=((v%c)+c)%c, su=(((u+off)%bl)+bl)%bl, r=hash2((Math.floor((u+off)/bl))|0,row|0);
        if(fv<0.05) return -2; if(su<0.05) return -1; if(fv>c-0.05) return 1; return r<0.3?-1:(r>0.8?1:0); }; }
    if(kind==='brick'){ const c=0.16, bl=0.40;
      return (u,v)=>{ const row=Math.floor(v/c), off=(row&1)*0.5*bl;
        const fv=((v%c)+c)%c, su=(((u+off)%bl)+bl)%bl;
        if(fv<0.03) return -2; if(su<0.035) return -2; if(fv>c-0.03) return 1; return 0; }; }
    return null;
  }
  function beadTex(){ const bw=0.095; return (u,v)=>{ const f=((u%bw)+bw)%bw;
    if(f<0.02) return -2; if(f>bw-0.02) return 1; return 0; }; }

  // ---- window / door on an interior wall plane -------------------------------
  function windowOn(out, axis, plane, nrm, c, z, ww, wh, style){
    const put=putOn(axis), st=WINSTYLES[style]||WINSTYLES.sixOverSix, ct=0.09, topZ=z+wh;
    put(out,plane,nrm, c-ww/2-ct-0.05, c+ww/2+ct+0.05, z-0.14, z-0.02, 'trim', 0.9, 0.05);      // sill
    put(out,plane,nrm, c-ww/2-ct, c+ww/2+ct, z-0.02, topZ+ct, 'trim', 0.4, 0.06);               // casing
    put(out,plane,nrm, c-ww/2-ct-0.04, c+ww/2+ct+0.04, topZ+ct, topZ+ct+0.08, 'trim', 0.8, 0.05);// header
    put(out,plane,nrm, c-ww/2, c+ww/2, z, topZ, 'glass', 0.0, 0.10);                            // glass
    put(out,plane,nrm, c-ww/2+0.02, c-ww/2+ww*0.34, z+wh*0.5, topZ-0.05, 'glassHi', 0.0, 0.12);
    const mb=0.055;
    if(st.v>0){ const cols=st.v+1; for(let i=1;i<=st.v;i++){ const cc=c-ww/2+ww*(i/cols);
      put(out,plane,nrm, cc-mb/2, cc+mb/2, z, topZ, 'trim', 0.55, 0.13); } }
    for(const r of st.r){ const rz=z+wh*r; put(out,plane,nrm, c-ww/2, c+ww/2, rz-mb/2, rz+mb/2, 'trim', 0.55, 0.13); }
    if(st.arch){ put(out,plane,nrm, c-ww/2-ct, c+ww/2+ct, topZ+ct, topZ+ct+ww*0.28, 'trim', 0.5, 0.05);
      put(out,plane,nrm, c-ww*0.34, c+ww*0.34, topZ, topZ+ww*0.22, 'glass', 0.0, 0.11); }
  }
  function doorwayOn(out, axis, plane, nrm, c, z0, dw, dh, opt){
    const put=putOn(axis), ct=0.11, topZ=z0+dh;
    put(out,plane,nrm, c-dw/2-ct, c+dw/2+ct, z0, topZ+ct, 'trim', 0.5, 0.06);                    // jamb/casing
    put(out,plane,nrm, c-dw/2-ct-0.04, c+dw/2+ct+0.04, topZ+ct, topZ+ct+0.08, 'trim', 0.8, 0.05);// head casing
    if(opt && opt.slab){                                                                         // closed plank door
      put(out,plane,nrm, c-dw/2, c+dw/2, z0, topZ, 'wood', 0.05, 0.10);
      const planks=3; for(let i=1;i<planks;i++){ const px=c-dw/2+dw*(i/planks);
        put(out,plane,nrm, px-0.016,px+0.016, z0+0.05, topZ-0.05,'wood',-1.6,0.12); }
      put(out,plane,nrm, c-dw/2+0.05,c+dw/2-0.05, z0+0.28, z0+0.4,'wood',-1.0,0.12);
      put(out,plane,nrm, c+dw/2-0.18,c+dw/2-0.1, z0+dh*0.48, z0+dh*0.48+0.14,'trim',0.9,0.13);   // latch
    } else {
      put(out,plane,nrm, c-dw/2, c+dw/2, z0, topZ, 'doordk', 0.0, 0.10);                          // open dark
      put(out,plane,nrm, c-dw/2, c-dw/2+0.06, z0, topZ, 'trim', 0.3, 0.12);                       // jamb reveal L
      put(out,plane,nrm, c+dw/2-0.06, c+dw/2, z0, topZ, 'doordk', -1.2, 0.12);                    // jamb reveal R (shadow)
    }
  }

  // ---- interior wall (kept side): cap slab + finished inner face -------------
  function finishBands(out, axis, plane, nrm, a0, a1, fZ, ceilZ, b){
    const put=putOn(axis), F0=b.finish, tex=wallTex(F0), bt=beadTex();
    const baseH=0.16, dadoTop=fZ+1.0;
    // baseboard (painted white board)
    put(out,plane,nrm, a0,a1, fZ, fZ+baseH, 'trim', 0.35, 0.045);
    let mainZ0=fZ+baseH;
    if(b.wainscot){
      put(out,plane,nrm, a0,a1, fZ+baseH, dadoTop, 'wood', 0.0, 0.05, bt, false);      // beadboard dado
      put(out,plane,nrm, a0,a1, dadoTop, dadoTop+0.09, 'trim', 0.7, 0.055);            // chair rail
      mainZ0=dadoTop+0.09;
    }
    const crownZ=ceilZ-0.1;
    if(F0==='stud'){
      put(out,plane,nrm, a0,a1, mainZ0, crownZ, 'cavity', -0.2, 0.05);                 // cavity
      put(out,plane,nrm, a0,a1, mainZ0, mainZ0+0.12, 'wood', 0.1, 0.06);               // bottom plate
      put(out,plane,nrm, a0,a1, crownZ-0.14, crownZ, 'wood', 0.1, 0.06);               // top plate
      const step=0.42; for(let a=a0+0.18; a<a1-0.05; a+=step)
        put(out,plane,nrm, a-0.045,a+0.045, mainZ0+0.1, crownZ-0.12, 'wood', 0.2, 0.07); // studs
    } else {
      const mat = F0==='wallpaper'?'paper' : F0==='board'?'wood' : F0==='stone'?'stone' : F0==='brick'?'brick' : 'plaster';
      put(out,plane,nrm, a0,a1, mainZ0, crownZ, mat, 0.0, 0.05, tex, false);
    }
    // crown / cornice
    put(out,plane,nrm, a0,a1, crownZ, ceilZ, 'trim', 0.6, 0.05);
  }

  // ---- geometry resolve (mirrors houseIsoRig footprint) ----------------------
  function resolve(opts){
    opts = opts||{};
    const P = opts.preset && PRESETS[opts.preset] ? PRESETS[opts.preset] : {};
    const g=(k,d)=> opts[k]!=null ? opts[k] : (P[k]!=null ? P[k] : d);
    const size = g('size', 0.3);
    const b = {
      size,
      floor:  g('floor','plank'),
      floorTone: g('floorTone', null),
      finish: g('wall','plaster'),
      paper:  g('paper','cream'),
      wainscot: g('wainscot', false),
      windows: g('windows','sixOverSix'),
      winD:   g('winDensity', 0.6),
      door:   g('door', true),
      dividers: g('dividers', 0)|0,
      hearth: g('hearth', false),
      stairs: g('stairs','none'),
      beams:  g('beams', false),
      weather: g('weather', 0.3),
      night:  !!opts.night,
    };
    b.Wd = 6 + size*2.4;             // == houseIsoRig
    b.Ln = 7 + size*4.2;
    b.wallH = 3.6 + size*2.0;
    b.roomH = Math.min(2.55 + size*0.85, b.wallH - 0.6);   // single-storey interior height
    b.wt = 0.16;
    b.fZ = 0;
    b.ceilZ = b.fZ + b.roomH;
    return b;
  }

  function makeMats(b){
    const wx=b.weather, night=b.night;
    const grime=(ramp)=>ramp.map(c=>{ let x=desat(c, wx*0.28); x=mix(x,'#4a4034',wx*0.14); return x; });
    const warm=(ramp,k)=> night ? ramp.map(c=> mix(mix(c,'#c98b3f',0.12), '#241a10', 0.20+ (k||0)) ) : ramp;
    const bodyPaper = Array.isArray(b.paper)?b.paper:(BODY[b.paper]||BODY.cream);
    const floorRamp = b.floor==='checker' ? TILE
                    : b.floor==='stoneFlag' ? STONE
                    : (b.floorTone && BODY[b.floorTone]) ? BODY[b.floorTone] : FLOORWOOD;
    return {
      plaster:{ ramp: warm(grime(PLASTER)) },
      paper:  { ramp: warm(grime(bodyPaper)) },
      wood:   { ramp: warm(grime(WOOD)) },
      floor:  { ramp: warm(grime(floorRamp), 0.02) },
      trim:   { ramp: warm(grime(TRIM)) },
      stone:  { ramp: warm(grime(STONE)) },
      brick:  { ramp: warm(grime(BRICK)) },
      cavity: { ramp: warm(CAVITY) },
      doordk: { ramp: night?DOORDK.map(c=>mix(c,'#3a2a14',0.25)):DOORDK },
      glass:  { ramp: night?GLASSN:GLASSD },
      glassHi:{ ramp:[ night?'#4a6a86':GLASS_HI ] },
      fire:   { ramp: FIRE },
      dark:   { ramp:[KEY] },
    };
  }

  // floor face bias so the (always top-lit) plane lands mid-ramp instead of saturating
  const FLOOR_BIAS = { checker:-3.75, stoneFlag:-3.35, plank:-2.7, wideBoard:-2.7, painted:-3.1 };

  // ---- assemble the room -----------------------------------------------------
  function keepWall(nx,ny,B){ const nyr = nx*B.stt + ny*B.ct; return nyr >= -0.35; }

  // straight flight of stacked closed-string treads rising along +Y against the W wall
  function buildStairs(out, b, x0i, y0i){
    const n=7, depth=0.30, w=1.15, top=Math.min(1.7, b.ceilZ-1.0), rise=top/n, x0=x0i, x1=x0i+w, y0=y0i+0.35;
    for(let i=0;i<n;i++){ const zt=(i+1)*rise, ya=y0+i*depth;
      boxSolid(out, x0,x1, ya,ya+depth+0.02, 0, zt, 'wood', null, 0.05);
      decalX(out, x1,1, ya,ya+depth, zt-0.05,zt, 'wood', 0.6, null, true, 0.05); }   // tread nosing
    const lz=n*rise, ly=y0+n*depth;
    boxSolid(out, x0,x1+0.5, ly,ly+0.7, 0, lz, 'wood', null, 0.05);                   // landing
    for(const py of [y0, ly]){ const z0=(py===y0?0:lz); boxSolid(out, x1-0.08,x1+0.04, py-0.05,py+0.05, z0, z0+0.85, 'wood', null, 0.15); } // newels
    out.push(F([[x1,y0,0.85],[x1,ly,lz+0.85],[x1,ly,lz+0.72],[x1,y0,0.72]], 'wood', 0.4, 0.06, null, null));  // handrail
  }
  // hinged plank door leaf, swung ~90deg open into the +Y room, hinged at (hingeX, yy)
  function buildDoorLeaf(out, hingeX, yy, leafW, fZ, dh){
    const th=0.05, y2=yy+leafW;
    boxSolid(out, hingeX-th,hingeX+th, yy,y2, fZ, fZ+dh, 'wood', null, 0.05);
    for(let k=1;k<3;k++){ const zz=fZ+dh*(k/3); decalX(out, hingeX+th,1, yy+0.05,y2-0.05, zz-0.016,zz+0.016, 'wood', -1.4, null, true, 0.06); }
    decalX(out, hingeX+th,1, y2-0.15,y2-0.07, fZ+dh*0.47,fZ+dh*0.47+0.09, 'trim', 0.9, null, true, 0.07);   // knob
  }

  function build(b, B){
    const out=[];
    const hw=b.Wd/2, hl=b.Ln/2, wt=b.wt, fZ=b.fZ, ceilZ=b.ceilZ;
    const x0i=-hw+wt, x1i=hw-wt, y0i=-hl+wt, y1i=hl-wt;
    const ftex=floorTex(b.floor), fb=FLOOR_BIAS[b.floor]!=null?FLOOR_BIAS[b.floor]:-3;
    const flatFloor = (b.floor==='checker');
    const b2 = Object.assign({}, b);

    // FLOOR (full footprint, under the walls) — uv in metres from a room corner
    CUR_LAYER='floor';
    out.push(F([[-hw,-hl,fZ],[hw,-hl,fZ],[hw,hl,fZ],[-hw,hl,fZ]], 'floor', fb, 0,
      [[0,0],[b.Wd,0],[b.Wd,b.Ln],[0,b.Ln]], ftex, flatFloor));

    // PERIMETER WALLS + their openings — each on its OWN layer (engine fades one at a time).
    // Drop the walls whose outward face points at the camera (open dollhouse); the L/V swaps per facing.
    const sillG = fZ + 1.0, ww=0.82, wh=1.12;
    const nLong = Math.max(1, Math.round((b.Ln/2.6)*(0.5+b.winD)));
    const sides=[
      { id:'N', nx:0, ny:1,  axis:'y', plane:y1i, nrm:-1, a0:x0i, a1:x1i, cap:['y',y1i,hl]  },
      { id:'S', nx:0, ny:-1, axis:'y', plane:y0i, nrm:1,  a0:x0i, a1:x1i, cap:['y',-hl,y0i] },
      { id:'E', nx:1, ny:0,  axis:'x', plane:x1i, nrm:-1, a0:y0i, a1:y1i, cap:['x',x1i,hw]  },
      { id:'W', nx:-1,ny:0,  axis:'x', plane:x0i, nrm:1,  a0:y0i, a1:y1i, cap:['x',-hw,x0i] },
    ];
    for(const s of sides){
      if(!keepWall(s.nx,s.ny,B)) continue;
      CUR_LAYER='w'+s.id;
      if(s.cap[0]==='y') slab(out, [[-hw,s.cap[1]],[hw,s.cap[1]],[hw,s.cap[2]],[-hw,s.cap[2]]], ceilZ, 'plaster', 0.55);
      else               slab(out, [[s.cap[1],-hl],[s.cap[2],-hl],[s.cap[2],hl],[s.cap[1],hl]], ceilZ, 'plaster', 0.55);
      finishBands(out, s.axis, s.plane, s.nrm, s.a0, s.a1, fZ, ceilZ, b2);
      if(s.id==='E') for(let i=0;i<nLong;i++){ const c=y0i+(y1i-y0i)*((i+0.5)/nLong); windowOn(out,'x',x1i,-1,c,sillG,ww,wh,b.windows); }
      if(s.id==='W') for(let i=0;i<nLong;i++){ const c=y0i+(y1i-y0i)*((i+0.5)/nLong); windowOn(out,'x',x0i, 1,c,sillG,ww,wh,b.windows); }
      if(s.id==='N'){ windowOn(out,'y',y1i,-1,-hw*0.42,sillG,ww,wh,b.windows); windowOn(out,'y',y1i,-1,hw*0.42,sillG,ww,wh,b.windows); }
      if(s.id==='S'){ if(b.door) doorwayOn(out,'y',y0i,1, 0, fZ, 1.05, 2.15, {slab:true});
        windowOn(out,'y',y0i,1,-hw*0.62,sillG,ww,wh,b.windows); windowOn(out,'y',y0i,1, hw*0.62,sillG,ww,wh,b.windows); }
    }

    // CEILING BEAMS — exposed joists across the short span + a summer beam down the ridge (overhead)
    if(b.beams){ CUR_LAYER='beam'; const bz=ceilZ-0.2, nb=Math.max(3,Math.round(b.Ln/1.8));
      for(let i=1;i<nb;i++){ const yy=-hl+b.Ln*(i/nb); boxSolid(out, x0i,x1i, yy-0.06,yy+0.06, bz, ceilZ-0.02,'wood',null,0.15); }
      boxSolid(out, -0.1,0.1, y0i,y1i, bz-0.05, ceilZ-0.02,'wood',null,0.22); }

    // STAIRS — straight flight against the W wall; rides that wall's visibility (dropped when W is a near wall)
    if(b.stairs && b.stairs!=='none' && keepWall(-1,0,B)){ CUR_LAYER='stair'; buildStairs(out, b, x0i, y0i); }

    // INTERIOR DIVIDERS (partition walls w/ a hinged door leaf → multiple rooms from one footprint)
    const nd=Math.min(2,b.dividers|0);
    for(let i=0;i<nd;i++){
      CUR_LAYER='dv'+i;
      const yy = y0i + (y1i-y0i)*((i+1)/(nd+1));           // run in X, at staggered y
      const gapC = (i%2? -1:1) * hw*0.34, gapW=1.15;       // doorway gap
      const segs=[[x0i, gapC-gapW/2],[gapC+gapW/2, x1i]];
      for(const [sa,sb] of segs){ if(sb-sa<0.2) continue;
        boxSolid(out, sa,sb, yy-wt/2, yy+wt/2, fZ, ceilZ, 'plaster', null, 0.0);
        finishBands(out,'y', yy-wt/2, -1, sa, sb, fZ, ceilZ, b2);
        finishBands(out,'y', yy+wt/2,  1, sa, sb, fZ, ceilZ, b2);
      }
      decalY(out, yy-wt/2, -1, gapC-gapW/2-0.1, gapC+gapW/2+0.1, fZ+2.15, fZ+2.32, 'trim', 0.6, null, true, 0.06);
      decalY(out, yy+wt/2,  1, gapC-gapW/2-0.1, gapC+gapW/2+0.1, fZ+2.15, fZ+2.32, 'trim', 0.6, null, true, 0.06);
      buildDoorLeaf(out, gapC-gapW/2, yy, gapW*0.9, fZ, 2.05);
    }

    // HEARTH / chimney breast on the back (+Y / N) wall, tied to the exterior chimney
    if(b.hearth && keepWall(0,1,B)){
      CUR_LAYER='hearth';
      const bw=1.7, bd=0.7, cx0=-bw/2, cx1=bw/2, by0=y1i-bd, by1=y1i;
      boxSolid(out, cx0,cx1, by0,by1, fZ, ceilZ*0.86, 'stone', wallTex('stone'), 0.0);            // stone surround
      boxSolid(out, cx0+0.12,cx1-0.12, by0-0.02,by1, ceilZ*0.86, ceilZ, 'stone', wallTex('stone'), 0.05); // flue
      decalY(out, by0, -1, cx0+0.22, cx1-0.22, fZ+0.06, fZ+1.12, 'brick', 0.0, wallTex('brick'), false, 0.055);
      decalY(out, by0, -1, cx0+0.3, cx1-0.3, fZ+0.1, fZ+0.98, 'dark', 0.0, null, true, 0.07);
      decalY(out, by0, -1, cx0+0.36, cx1-0.36, fZ+0.12, fZ+0.62, 'fire', 0.0, null, true, 0.09);
      decalY(out, by0-0.06, -1, cx0-0.1, cx1+0.1, fZ+1.2, fZ+1.38, 'wood', 0.4, null, true, 0.05);
    }

    CUR_LAYER='base';
    return out;
  }

  // ---- rasterizer (fleet recipe) ---------------------------------------------
  function paint(faces, B, MATS){
    const N=W*H;
    const zbuf=new Float32Array(N).fill(Infinity);
    const dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null);
    const ibuf=new Int16Array(N);
    const nbuf=new Array(N).fill(null);
    const lbuf=new Array(N).fill(null);
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce);
      if(sh<0 && (f.b<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + f.b;
      const M = MATS[f.mat] || MATS.plaster;
      const ramp=M.ramp, off=M.off||0, tex=f.tex, uv=f.uv, flat=f.flat;
      for(let t=1;t+1<rv.length;t++) fillTri(rv[0],rv[t],rv[t+1], 0,t,t+1);
      function fillTri(a,b,c, ia,ib,ic){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx)));
        const maxX=Math.min(W-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy)));
        const maxY=Math.min(H-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy);
        if(Math.abs(area)<1e-6) return;
        const ua=uv?uv[ia]:null, ub=uv?uv[ib]:null, uc=uv?uv[ic]:null;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){
          const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area;
          const w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area;
          const w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-f.db;
          const i=y*W+x;
          if(deff<zbuf[i]){
            zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat; lbuf[i]=f.layer;
            let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi += tex(uu,vv); }
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
    const { rbuf, ibuf, nbuf, dep } = bufs;
    const N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    // depth-edge darkening (surface separation)
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j;
          const idx=Math.max(0,ibuf[far]-2); out[far]=rbuf[far][idx]; }
      }
    }
    // grime speckle
    const wx=b.weather;
    if(wx>0.02){ const rnd=mulberry32(913|((b.size*131)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='floor'||m==='plaster'||m==='paper'||m==='wood'||m==='stone'||m==='brick') && rnd()<wx*0.06)
          out[i]=rbuf[i][Math.max(0,Math.min(rbuf[i].length-1,ibuf[i]-1))]; } }
    // night: warm glow bleed off the firebox + window cool rim
    if(b.night){
      for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x;
        if(nbuf[i]==='fire'){ for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,1]]){ const j=(y+dy)*W+(x+dx);
          if(out[j]&&nbuf[j]!=='fire') out[j]=mix(out[j],'#f0b45a',0.22); } } }
    }
    // despeckle isolated islands
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; } }
    // 1px keyline around the whole cutaway (skipped for the layered path — each layer keylines itself)
    if(!noKey){ for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
      if(touch) out[i]=KEY; } }
    return out;
  }
  function toRGBA(cols){
    const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; }
      const [r,g,bl]=hex2rgb(c); rgba[i*4]=r; rgba[i*4+1]=g; rgba[i*4+2]=bl; rgba[i*4+3]=255; }
    return rgba;
  }

  // 1px keyline drawn INTO an rgba buffer (per-layer), decided off the original alpha mask
  function addKeyline(rgba){
    const [kr,kg,kb]=hex2rgb(KEY), N=W*H, op=new Uint8Array(N);
    for(let i=0;i<N;i++) op[i]=rgba[i*4+3]!==0?1:0;
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(op[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&op[ny*W+nx]){ touch=true; break; } }
      if(touch){ const j=i*4; rgba[j]=kr; rgba[j+1]=kg; rgba[j+2]=kb; rgba[j+3]=255; } }
  }
  function layerKind(id){ return id==='floor'?'floor' : id[0]==='w'?'wall' : id.slice(0,2)==='dv'?'divider' : 'fixture'; }

  // engine primitive: the room split into independently compositable/fade-able sprites, so a wall or
  // divider can be dropped/ghosted when the player walks behind it. Same pivot as render().
  function renderLayers(dir, opts){
    opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const b=resolve(opts), B=camBasis({dir,elev:opts.elev}), MATS=makeMats(b);
    const bufs=paint(build(b,B), B, MATS);
    const cols=post(bufs,b,true), lbuf=bufs.lbuf, groups={};
    for(let i=0;i<W*H;i++){ const l=lbuf[i]; if(cols[i]==null||l==null) continue; (groups[l]||(groups[l]=[])).push(i); }
    const rank=(id)=> id==='floor'?0 : (layerKind(id)==='fixture'?1 : layerKind(id)==='divider'?2 : 3);
    const order=Object.keys(groups).sort((a,b)=> rank(a)-rank(b));
    const layers=order.map(id=>{ const rgba=new Uint8ClampedArray(W*H*4);
      for(const i of groups[id]){ const [r,g,bl]=hex2rgb(cols[i]), j=i*4; rgba[j]=r;rgba[j+1]=g;rgba[j+2]=bl;rgba[j+3]=255; }
      addKeyline(rgba); return { id, kind:layerKind(id), rgba }; });
    return { W, H, pivot:{x:cx,y:groundY}, layers, anchors:anchors(dir,opts) };
  }
  // ordered-dither "ghost" of a layer rgba — the see-through state when a player is behind it.
  // keep = fraction of pixels retained (lower = more transparent). No-AA stipple, matches the look.
  function ghost(rgba, keep){ keep=keep==null?0.18:keep; const out=new Uint8ClampedArray(rgba);
    for(let y=0;y<H;y++) for(let x=0;x<W;x++) if(BAYER[x&3][y&3] >= keep) out[(y*W+x)*4+3]=0;
    return out; }

  function render(dir, opts){
    opts = (typeof opts==='number')?{elev:opts}:(opts||{});
    const b=resolve(opts);
    const B=camBasis({dir, elev:opts.elev});
    const MATS=makeMats(b);
    const faces=build(b, B);
    return toRGBA(post(paint(faces, B, MATS), b));
  }
  function anchors(dir, opts){
    opts=opts||{}; const b=resolve(opts), B=camBasis({dir,elev:opts.elev});
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return {x:v.sx,y:v.sy}; };
    const hl=b.Ln/2, hw=b.Wd/2, wt=b.wt, x0i=-hw+wt,x1i=hw-wt,y0i=-hl+wt,y1i=hl-wt;
    // occluders: each wall/divider's floor base line + its worldY + top screen-y, so the engine can
    // test 'player behind this' (nearer camera than the player) and ghost that layer.
    const occ=[];
    const pushW=(id,ax,plane,a0,a1,wy)=>{ const p0=ax==='y'?pj(a0,plane,b.fZ):pj(plane,a0,b.fZ);
      const p1=ax==='y'?pj(a1,plane,b.fZ):pj(plane,a1,b.fZ);
      const t=ax==='y'?pj((a0+a1)/2,plane,b.ceilZ):pj(plane,(a0+a1)/2,b.ceilZ);
      occ.push({id, kind:layerKind(id), base:[p0,p1], worldY:wy, topY:t.y}); };
    if(keepWall(0,1,B))  pushW('wN','y',y1i,-hw,hw, y1i);
    if(keepWall(0,-1,B)) pushW('wS','y',y0i,-hw,hw, y0i);
    if(keepWall(1,0,B))  pushW('wE','x',x1i,-hl,hl, 0);
    if(keepWall(-1,0,B)) pushW('wW','x',x0i,-hl,hl, 0);
    const nd=Math.min(2,b.dividers|0);
    for(let i=0;i<nd;i++){ const yy=y0i+(y1i-y0i)*((i+1)/(nd+1)); pushW('dv'+i,'y',yy,x0i,x1i, yy); }
    return { floor:pj(0,0,b.fZ), door:pj(0,-hl,b.fZ),
      hearth: b.hearth?pj(0,hl-0.35,b.fZ+0.5):null,
      lamps:[pj(-hw*0.5,hl*0.4,b.ceilZ*0.7), pj(hw*0.5,-hl*0.2,b.ceilZ*0.7)],
      occluders:occ, Wd:b.Wd, Ln:b.Ln };
  }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.InteriorIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    FLOORS, WALLS, WINDOWS, STAIRS, BODY, TRIM, FLOORWOOD, STONE, BRICK, TILE, PLASTER, WOOD, PRESETS, KEY,
    render, renderLayers, ghost, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
