/* Hidden Harbours — parametric ISO HOUSE rig (ADR-0006 bake pipeline, same turntable as the
   fleet + characterIsoRig.js). One parametric 3D house, built from walls / roof planes / gable
   prisms / decals, baked to pixel sheets through the SHARED 3/4 camera: 45deg steps, elev 40deg
   default (sits true beside the fleet + fisher), flat-facet shading from the fixed upper-LEFT key,
   z-buffered, ordered dither, per-face uv texture (siding), depth-edge darkening, 1px keyline,
   NO AA. 32 px = 1 m. All 8 facings fall out of one model by construction.

   THE HOUSE-CREATOR SURFACE (every axis resolved per render, no re-modelling):
     shape:  'gable'|'ell'|'gambrel'|'saltbox'|'cape'      (massing / roofline)
     size:   0..1  cottage(~6x7m, 1.5-storey) -> farmhouse(~8x11m, 2-storey)
     siding: 'shingle'|'clapboard'|'twotone'|'fishscale'   (wall texture; twotone splits the body)
     body:   'greyShingle'|'white'|'cream'|'red'|'sage'|'blue' | custom ramp   (trim stays white)
     lower:  body key for the twotone lower band
     roof:   'asphaltGrey'|'asphaltBrown'|'metal'
     dormers:0..3   bargeboard:bool (gingerbread)   chimneys:0..2
     windows:'sixOverSix'|'twoOverTwo'   winDensity:0..1   attic:'none'|'gable'|'round'|'gothic'
     bay:bool (sunroom projection)      porch:'none'|'front'|'wrap'
     era:    style preset (plain / colonial / gothic / modern) — seeds the axes above
     weather:0..1 (paint fade + shingle greying + roof moss + patchy)   night:bool (warm-lit)
   ANIM: houses are static; chimney smoke + lit windows are runtime overlays — anchors(dir,opts)
   -> { chimneys:[{x,y}], door:{x,y}, ridge:{x,y} } in cell px for the smoke / glow / label layers.
   Exposes globalThis.HouseIso = { W,H,PX,DIRS,pivot,order,defaultElev, SHAPES,SIDINGS,ROOFS,
   BODY,TRIM,ERAS,PRESETS,WINDOWS, render(dir,opts), anchors(dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 992, H = 1060, cx = 496, groundY = 676;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;

  // ---- palettes, dark -> light (KTC master ramps, shared with the fleet / cottage) ----
  const BODY = {
    greyShingle: ['#4c463f','#5d564c','#6f665a','#82786a','#968b7b','#a99d8c'],
    white:       ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    cream:       ['#8a6f3c','#a6884b','#c2a35f','#d8bd7c','#e9d59d','#f5e7c1'],
    red:         ['#4a130f','#671b14','#88271c','#a33124','#bd4230','#d25a42'],
    sage:        ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'],
    blue:        ['#33454a','#43585d','#556d72','#6a848a','#849ea3','#a3b9bd'],
    // phase 2b: more siding colours
    yellow:      ['#6e5316','#8c6b1f','#a9852c','#c49f3e','#d9b85a','#e8cd7e'],
    green:       ['#1f3a2c','#29493a','#35594a','#436b5a','#56806c','#6d9582'],
    grey:        ['#4d5256','#5f6569','#737a7e','#8a9195','#a2a9ac','#b9bfc1'],
    teal:        ['#23474a','#2d5a5c','#3a6e6f','#4b8584','#629b98','#7db2ad'],
    charcoal:    ['#26292c','#303438','#3c4145','#4a5055','#5a6166','#6c7378'],
  };
  const TRIM = ['#9aa09a','#b4b8b0','#ccd0c7','#e0e2da','#eef0e8','#f8f9f2'];
  const ROOFS = {
    asphaltGrey:  ['#23262b','#2e333a','#3c424a','#4c535c','#5d6570','#6f7883'],
    asphaltBrown: ['#2a211a','#3a2e23','#4c3d2e','#5f4d3a','#736046','#877254'],
    metal:        ['#424d52','#556065','#6c7c81','#88999e','#a4babe','#c0d4d7'],
  };
  const STONE  = ['#33343a','#42444b','#54575d','#666a70','#7a7e84'];
  const BRICK  = ['#3a201a','#552b20','#6e3728','#874634','#a05743','#b96b55'];
  const WOOD   = ['#4f3a24','#63492d','#785a39','#8f7049','#a6875d','#bd9f74'];
  const DOORC  = ['#20343a','#2c464d','#3a5c64','#4a747d','#5c8f99'];
  const GLASSD = ['#33474d','#40585f','#54707800'.slice(0,7)];          // day glass (cool)
  const GLASS_HI = '#cfe6e8';
  const GLASSN = ['#7a4f18','#b98a2f','#eed07a'];                       // night glass (warm glow)
  const KEY = '#1a1c22';
  let LIVE = false;                                   // camera-first pass: build() sets it for the faces it emits
  let CUR = null;                                     // phase 2: the build being emitted (era / shape-dependent joinery)

  const ERAS = {
    plain:    { shape:'gable',   roof:'asphaltGrey',  siding:'shingle',   pitch:0.95, bargeboard:false, windows:'twoOverTwo', attic:'gable',  trimW:0.10 },
    colonial: { shape:'gable',   roof:'asphaltGrey',  siding:'clapboard', pitch:0.85, bargeboard:false, windows:'sixOverSix', attic:'gable',  trimW:0.12 },
    gothic:   { shape:'gable',    roof:'asphaltGrey',  siding:'twotone',   pitch:1.4,  bargeboard:true,  windows:'sixOverSix', attic:'gothic', crossGable:1, trimW:0.14 },
    seaside:  { shape:'cape',    roof:'asphaltBrown', siding:'shingle',   pitch:1.05, bargeboard:false, windows:'sixOverSix', attic:'gable',  trimW:0.11 },
    modern:   { shape:'saltbox', roof:'metal',        siding:'clapboard', pitch:0.80, bargeboard:false, windows:'twoOverTwo', attic:'none',   trimW:0.09 },
  };
  const PRESETS = {
    shingleCottage: { era:'plain',    body:'greyShingle', roof:'asphaltBrown', shape:'gable',   siding:'shingle',   size:0.15, porch:'front', dormers:0, chimneys:1, bay:false, attic:'gable',  weather:0.35 },
    whiteFarmhouse: { era:'colonial', body:'white',       roof:'metal',        shape:'ell',     siding:'clapboard', size:0.7,  porch:'wrap',  dormers:1, chimneys:1, bay:true,  attic:'gable',  weather:0.10 },
    redSaltbox:     { era:'modern',   body:'red',         roof:'asphaltGrey',  shape:'saltbox', siding:'clapboard', size:0.4,  porch:'none',  dormers:0, chimneys:1, bay:false, attic:'none',   weather:0.2 },
    gothicRevival:  { era:'gothic',   body:'cream',       lower:'red', roof:'slate', shape:'gable', siding:'twotone', size:0.8, porch:'front', dormers:0, chimneys:2, bay:false, attic:'gothic', crossGable:1, bargeboard:true, weather:0.15 },
    dormerCape:     { era:'seaside',  body:'greyShingle', roof:'slate',        shape:'cape',    siding:'shingle',   size:0.5,  porch:'front', dormers:3, chimneys:1, bay:false, attic:'gable',  bargeboard:true, weather:0.45 },
    gambrelColonial:{ era:'colonial', body:'blue',        roof:'asphaltGrey',  shape:'gambrel', siding:'shingle',   size:0.2,  porch:'none',  dormers:2, chimneys:1, bay:false, attic:'gable',  weather:0.3 },   // phase 2
  };
  const SHAPES = ['gable','ell','gambrel','saltbox','cape'];
  const SIDINGS = ['shingle','clapboard','twotone','fishscale'];
  const WINDOWS = ['sixOverSix','twoOverTwo','fourOverFour','twoOverOne','oneOverOne','arched'];
  // phase 2b: the live look's roofs (classic falls back to its own asphalt / metal ramps), shutter and door paints
  const ROOF_OPTIONS = ['slate','asphaltGrey','asphaltBrown','metal','metalRed','metalGreen'];
  const PAINTS = ['join','red','blue','green','ivory','iron','oak'];
  /* St Peters (village plan, session 1): each house its own shape, colour, roof and joinery. facing = the plan's facing;
     door = where the door faces there. Shops (general store, post office) come with the shop kit. */
  const CAST = {
    sage_cottage:    { who:'Junior and Basil', facing:4, door:'S', opts:{ beds:2, era:'plain', shape:'gable', siding:'clapboard', body:'sage', roof:'asphaltBrown', size:0.25, windows:'twoOverTwo', winDensity:0.5, attic:'gable', porch:'front', dormers:0, chimneys:1, shutters:'ivory', doorPaint:'red', weather:0.55 } },
    school:          { who:'Eileen teaches here', facing:4, door:'S', opts:{ beds:0, era:'colonial', shape:'gable', siding:'clapboard', body:'white', roof:'slate', size:0.15, windows:'sixOverSix', winDensity:0.85, attic:'gable', porch:'front', dormers:0, chimneys:1, belfry:true, shutters:'blue', weather:0.3 } },
    harbour_gable:   { who:'two cannery hands', facing:6, door:'S', opts:{ beds:2, era:'gothic', shape:'gable', siding:'shingle', body:'teal', roof:'metalRed', size:0.3, windows:'twoOverTwo', winDensity:0.5, attic:'gothic', porch:'none', crossGable:1, gableFull:true, bargeboard:true, dormers:0, chimneys:1, shutters:'none', doorPaint:'ivory', weather:0.45 } },
    cream_saltbox:   { who:'two cannery hands', facing:6, door:'S', opts:{ beds:2, era:'modern', shape:'saltbox', siding:'clapboard', body:'cream', roof:'metalGreen', size:0.3, windows:'sixOverSix', winDensity:0.6, attic:'none', porch:'none', dormers:0, chimneys:1, shutters:'red', doorPaint:'blue', weather:0.15 } },
    white_gable:     { who:'a family, two children', facing:4, door:'S', opts:{ beds:3, era:'colonial', shape:'gable', siding:'clapboard', body:'white', roof:'asphaltGrey', size:0.7, windows:'twoOverOne', winDensity:0.6, attic:'round', porch:'wrap', dormers:2, chimneys:2, mirror:true, shutters:'iron', doorPaint:'red', weather:0.1 } },
    red_saltbox:     { who:'Rose', facing:4, door:'W', opts:{ beds:1, era:'modern', shape:'saltbox', siding:'clapboard', body:'red', roof:'metal', size:0.4, windows:'twoOverTwo', winDensity:0.6, attic:'none', porch:'front', entry:'left', dormers:0, chimneys:1, shutters:'none', weather:0.2 } },
    white_farmhouse: { who:'Eileen', facing:4, door:'W', opts:{ beds:1, era:'colonial', shape:'ell', siding:'clapboard', body:'white', roof:'slate', size:0.7, windows:'sixOverSix', winDensity:0.6, attic:'gable', porch:'wrap', entry:'right', mirror:true, dormers:1, chimneys:1, bay:true, shutters:'join', weather:0.1 } },
  };
  const WINSTYLES = {
    sixOverSix:   { v:2, r:[0.25,0.5,0.75] },
    fourOverFour: { v:1, r:[0.25,0.5,0.75] },
    twoOverTwo:   { v:1, r:[0.5] },
    twoOverOne:   { v:1, r:[0.5], vTop:true },
    oneOverOne:   { v:0, r:[0.5] },
    arched:       { v:1, r:[0.5], arch:true },
    knee:         { v:2, r:[] },                   // phase 2: the squat three-light sash under a storey-and-a-half eave
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

  // ---- camera / projection (identical to characterIsoRig, so houses composite with the fleet) ----
  function camBasis(opts){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:DEFAULT_ELEV)*DEG;
    return { ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e) };
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

  // ---- face builders -------------------------------------------------------
  // Every face: { v:[[x,y,z]..], mat, b, db, uv:[[u,v]..]|null, tex:fn|null }
  //   uv u = along-surface metres, v = height/up metres (for siding lap alignment)
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }
  function tagFrom(out,t0,tag){ for(let i=t0;i<out.length;i++) if(!out[i].tag) out[i].tag=tag; }
  const STONE_TEX=(u,v)=>{ const CO=0.19, row=Math.floor(v/CO), f=v-row*CO, su=(((u+(row&1)*0.23)%0.46)+0.46)%0.46; return (f<0.03||su<0.035)?-1:0; };

  // vertical wall from (x0,y0)->(x1,y1), z0..z1. facing = outward for CCW-from-outside.
  function wall(out, x0,y0,x1,y1, z0,z1, mat, tex, b){
    const L=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]], mat, b||0, 0,
      [[0,z0],[L,z0],[L,z1],[0,z1]], tex));
  }
  // horizontal quad (roof-flat / deck / foundation top) at given z, corners in xy
  function slab(out, pts, z, mat, b){ out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0)); }
  // arbitrary quad (roof slope) with uv from two edge lengths
  function quad(out, p0,p1,p2,p3, mat, b, tex, uvScale){
    const u=Math.hypot(p1[0]-p0[0],p1[1]-p0[1],p1[2]-p0[2]);
    const v=Math.hypot(p3[0]-p0[0],p3[1]-p0[1],p3[2]-p0[2]);
    out.push(F([p0,p1,p2,p3], mat, b||0, 0, tex?[[0,0],[u,0],[u,v],[0,v]]:null, tex||null));
  }
  function tri(out, p0,p1,p2, mat, b, uv, tex){ out.push(F([p0,p1,p2], mat, b||0, 0, uv||null, tex||null)); }
  // solid axis box (chimney, dormer body, posts) — 6 faces, optional side tex
  function boxSolid(out, x0,x1, y0,y1, z0,z1, mat, tex, b){
    wall(out, x0,y0, x1,y0, z0,z1, mat, tex, b);     // -Y
    wall(out, x1,y1, x0,y1, z0,z1, mat, tex, b);     // +Y
    wall(out, x1,y0, x1,y1, z0,z1, mat, tex, b);     // +X
    wall(out, x0,y1, x0,y0, z0,z1, mat, tex, b);     // -X
    slab(out, [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, (b||0)+0.25);   // top (lit)
  }

  // ---- siding textures: return an integer ramp delta (negative = darker seam) -------------
  function sidingTex(kind){
    if(kind==='clapboard' || kind==='twotone'){
      const LAP=0.30;
      return (u,v)=>{ const f=((v% LAP)+LAP)%LAP; return f < 0.055 ? -2 : (f>LAP-0.04? 1 : 0); };
    }
    if(kind==='shingle'){
      const CO=0.34, SW=0.24;
      return (u,v)=>{ const row=Math.floor(v/CO); const f=((v%CO)+CO)%CO;
        const off=(row&1)*0.5*SW; const su=(((u+off)%SW)+SW)%SW;
        if(f < 0.05) return -2;                 // course shadow
        if(su < 0.035) return -1;               // butt gap
        if(f > CO-0.05) return 1;               // lit lower lip
        return 0; };
    }
    if(kind==='fishscale'){
      const CO=0.26, SW=0.26;
      return (u,v)=>{ const row=Math.floor(v/CO); const off=(row&1)*0.5*SW;
        const su=(((u+off)%SW)+SW)%SW - SW/2; const fv=(((v%CO)+CO)%CO);
        const r=Math.hypot(su/(SW*0.52), (fv-CO*0.5)/(CO*0.6));
        if(r>0.9) return -2;                    // scallop gap
        if(fv > CO*0.72) return -1;             // rounded bottom shade
        if(su<-SW*0.18 && fv<CO*0.5) return 1;  // upper-left catch light
        return 0; };
    }
    return null;
  }
  // window mullion grid baked into glass via uv (u,v in 0..1 across the pane)
  function muntinTex(cols,rows){
    return (u,v)=>{ // u,v are metres along pane; caller sets uvScale = pane size so 0..paneW
      return 0; };
  }

  // ---- rasterizer (fleet recipe + uv interpolation + per-face tex) ----------
  function paint(faces, opts, MATS){
    const B=camBasis(opts);
    const N=W*H;
    const zbuf=new Float32Array(N).fill(Infinity);
    const dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null);   // ramp array per px
    const ibuf=new Int16Array(N);         // index into ramp
    const nbuf=new Array(N).fill(null);   // material name (weathering/night)
    for(const f of faces){
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B));
      let n=normal(rv[0],rv[1],rv[2]);
      let sh=shadeOf(n, B.se, B.ce);
      if(sh<0 && (f.b<=-1)) sh=shadeOf([-n[0],-n[1],-n[2]], B.se, B.ce)*0.9;
      const fidx = sh*GAIN + BIAS + f.b;
      const M = MATS[f.mat] || MATS.body;
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
            zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat;
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
    return { rbuf, ibuf, nbuf, dep };
  }

  // ---- geometry assembly ----------------------------------------------------
  function resolve(opts){
    opts = opts||{};
    const era = ERAS[opts.era] || ERAS.plain;
    const g = (k,d)=> opts[k]!=null ? opts[k] : (era[k]!=null ? era[k] : d);
    const size = opts.size!=null ? opts.size : 0.4;
    const b = {
      shape:  g('shape','gable'),
      size,
      siding: g('siding','shingle'),
      body:   opts.body || 'greyShingle',
      lower:  opts.lower || 'red',
      roof:   g('roof','asphaltGrey'),
      pitch:  g('pitch',0.95),
      dormers: opts.dormers!=null ? opts.dormers : (opts.shape==='cape'||g('shape')==='cape'?3:0),
      bargeboard: g('bargeboard',false),
      chimneys: opts.chimneys!=null ? opts.chimneys : 1,
      windows: g('windows','sixOverSix'),
      winDensity: opts.winDensity!=null ? opts.winDensity : 0.6,
      attic:  g('attic','gable'),
      bay:    opts.bay!=null ? opts.bay : false,
      porch:  opts.porch || 'none',
      crossGable: g('crossGable',0),
      gableFull: opts.gableFull!=null ? opts.gableFull : false,
      garage: opts.garage || 'none',
      weather: opts.weather!=null ? opts.weather : 0.2,
      night:  !!opts.night && !!opts.classic,         // the live look takes its night from the sky
      era: opts.era||'plain',
      classic: !!opts.classic,
      live: !opts.classic,
      entry: (opts.entry==='left'||opts.entry==='right') ? opts.entry : 'front',
    };
    // dimensions (metres)
    b.Wd = 6 + size*2.4;              // gable span (x)
    b.Ln = 7 + size*4.2;              // ridge length (y)
    b.fH = 0.55;                      // stone foundation
    b.wallH = 3.6 + size*2.0;         // eave height above foundation
    b.eaveZ = b.fH + b.wallH;
    b.rise  = (b.Wd/2) * b.pitch;
    b.ridgeZ = b.eaveZ + b.rise;
    b.ov = 0.32;                      // eave overhang
    if(b.live && b.shape==='saltbox') b.eaveL = b.eaveZ - 1.4;   // phase 2: the saltbox's low rear eave (gutter and downpipes follow it)
    b.belfry = !!opts.belfry && b.live;
    b.mirror = !!opts.mirror && b.live;                               // phase 2b: the same house flipped across its ridge
    b.shutters = b.live && PAINTS.concat(['none']).includes(opts.shutters) ? opts.shutters : null;
    b.doorPaint = b.live && PAINTS.includes(opts.doorPaint) ? opts.doorPaint : null;
    return b;
  }

  // build MATS for a resolved build (+ weathering/night ramp transforms)
  function makeMats(b){
    const wx=b.weather, night=b.night;
    const wthBody=(ramp)=>ramp.map(c=>{ let x=desat(c, wx*0.55); x=mix(x,'#6f6a5f',wx*0.28); if(night)x=mix(x,'#1b2733',0.42); return x; });
    const wthRoof=(ramp)=>ramp.map(c=>{ let x=mix(c,'#5f6a52',wx*0.18); if(night)x=mix(x,'#141d27',0.45); return x; });
    const wthWood=(ramp)=>ramp.map(c=>{ let x=mix(c,'#8a8172',wx*0.4); if(night)x=mix(x,'#1b2230',0.4); return x; });
    const trimR = TRIM.map(c=>{ let x=desat(c,wx*0.3); if(night)x=mix(x,'#24303c',0.4); return x; });
    const bodyRamp = Array.isArray(b.body)?b.body:(BODY[b.body]||BODY.greyShingle);
    const lowerRamp= BODY[b.lower]||BODY.red;
    const glass = night ? GLASSN : GLASSD;
    return {
      body:  { ramp: wthBody(bodyRamp) },
      lower: { ramp: wthBody(lowerRamp) },
      trim:  { ramp: trimR },
      roof:  { ramp: wthRoof(ROOFS[b.roof]||ROOFS.asphaltGrey) },
      stone: { ramp: wthBody(STONE) },
      brick: { ramp: wthRoof(BRICK) },
      wood:  { ramp: wthWood(WOOD) },
      door:  { ramp: night?wthRoof(DOORC):DOORC },
      glass: { ramp: glass, off: night?1:0 },
      glassHi:{ ramp:[ night?'#ffe6a6':GLASS_HI ] },
      dark:  { ramp:[KEY] },
      ...(b.live ? { iron:{ ramp:['#1d2427','#2a3337','#3a4549','#4d595d','#627074'] }, lamp:{ ramp:['#6d5a33','#8f7640','#b39552','#cfb46c','#e6d08d'] },
        lite:{ ramp: glass, off: night?1:0 }, pot:{ ramp: wthRoof(['#4a281c','#653624','#80452e','#99573b','#b06c4c']) } } : {}),
    };
  }

  // decal on a ±Y wall (normal along Y). yv = wall plane, xs..xe span, z0..z1.
  function decalY(out, yv, ny, xs,xe, z0,z1, mat, b, tex, flat, db){
    const e=0.02*ny, uw=xe-xs, uh=z1-z0;
    const P = ny>0
      ? [[xs,yv+e,z0],[xe,yv+e,z0],[xe,yv+e,z1],[xs,yv+e,z1]]
      : [[xe,yv+e,z0],[xs,yv+e,z0],[xs,yv+e,z1],[xe,yv+e,z1]];
    out.push(F(P, mat, b||0.3, db!=null?db:0.06, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat));
  }
  function decalX(out, xv, nx, ys,ye, z0,z1, mat, b, tex, flat, db){
    const e=0.02*nx, uw=ye-ys, uh=z1-z0;
    const P = nx>0
      ? [[xv+e,ye,z0],[xv+e,ys,z0],[xv+e,ys,z1],[xv+e,ye,z1]]
      : [[xv+e,ys,z0],[xv+e,ye,z0],[xv+e,ye,z1],[xv+e,ys,z1]];
    out.push(F(P, mat, b||0.3, db!=null?db:0.06, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat));
  }
  // muntin grid tex for a pane of given metre size
  function paneTex(pw,ph,cols,rows){
    const cw=pw/cols, rh=ph/rows, t=0.028;
    return (u,v)=>{ const fu=((u%cw)+cw)%cw, fv=((v%rh)+rh)%rh;
      if(u<t||u>pw-t||v<t||v>ph-t) return -3;                  // frame edge dark
      if(fu<t||fv<t) return -2;                                // muntin
      if(u<pw*0.42 && v>ph*0.5) return 1;                      // gla
      return 0; };
  }

  // a framed double-hung window centred at along-coord c, sill at z, on a ±Y or ±X wall.
  // built as crisp FLAT quads (no dither / no sub-pixel texture) so panes + muntins stay legible.
  function windowOn(out, axis, plane, nrm, c, z, ww, wh, b){
    const put=(a0,a1,z0,z1,mat,bias,db)=>{ if(axis==='y') decalY(out, plane,nrm, a0,a1, z0,z1, mat, bias, null, true, db);
                                            else            decalX(out, plane,nrm, a0,a1, z0,z1, mat, bias, null, true, db); };
    const st = WINSTYLES[b.windows] || WINSTYLES.sixOverSix;
    const ct=0.09, topZ=z+wh, archH = st.arch ? ww*0.52 : 0;
    // sill (protruding board below the opening)
    put(c-ww/2-ct-0.05, c+ww/2+ct+0.05, z-0.13, z-0.03, 'trim', 0.9, 0.05);
    // casing frame (filled white board, glass sits proud of it)
    put(c-ww/2-ct, c+ww/2+ct, z-0.03, topZ+ct, 'trim', 0.45, 0.06);
    // header cap (rectangular styles only; arched replaces it with a peak)
    if(!st.arch) put(c-ww/2-ct-0.04, c+ww/2+ct+0.04, topZ+ct, topZ+ct+0.07, 'trim', 0.8, 0.05);
    if(LIVE && CUR && CUR.era==='gothic' && !st.arch && wh>0.8){ const z1=topZ+ct+0.07, a0=c-ww/2-ct-0.07, a1=c+ww/2+ct+0.07, e=0.035*nrm;   // phase 2: a peaked hood over each gothic sash
      const pt=(al,zz)=> axis==='y' ? [al,plane+e,zz] : [plane+e,al,zz]; out.push(F([pt(a0,z1),pt(a1,z1),pt(c,z1+0.22)],'trim',0.65,0.07,null,null,true)); }
    // glass pane
    put(c-ww/2, c+ww/2, z, topZ, 'glass', 0.0, 0.10);
    // upper-left catch light
    put(c-ww/2+0.02, c-ww/2+ww*0.34, z+wh*0.54, topZ-0.05, 'glassHi', 0.0, 0.12);
    // muntins over the glass
    const mb=0.055;
    if(st.v>0){ const cols=st.v+1; for(let i=1;i<=st.v;i++){ const cx=c-ww/2+ww*(i/cols);
      put(cx-mb/2, cx+mb/2, (st.vTop? z+wh*0.5 : z), topZ, 'trim', 0.6, 0.14); } }
    for(const r of st.r){ const rz=z+wh*r; put(c-ww/2, c+ww/2, rz-mb/2, rz+mb/2, 'trim', 0.6, 0.14); }
    // arched (pointed gothic) top
    if(st.arch){
      const apex=topZ+archH, e=0.02*nrm;
      const pt=(al,zz)=> axis==='y' ? [al,plane+e,zz] : [plane+e,al,zz];
      const tri=(aL,aR,zz,mat,bias,db)=>{ const ap=apex-(zz-topZ);
        const P = (axis==='y')? (nrm>0?[pt(aL,zz),pt(c,ap),pt(aR,zz)]:[pt(aR,zz),pt(c,ap),pt(aL,zz)])
                              : (nrm>0?[pt(aR,zz),pt(c,ap),pt(aL,zz)]:[pt(aL,zz),pt(c,ap),pt(aR,zz)]);
        out.push(F(P,mat,bias,db,null,null,true)); };
      tri(c-ww/2-ct, c+ww/2+ct, topZ, 'trim', 0.5, 0.05);
      tri(c-ww/2+0.03, c+ww/2-0.03, topZ+0.03, 'glass', 0.0, 0.12);
    }
  }

  function doorOn(out, axis, plane, nrm, c, z0, dw, dh){
    const put=(a0,a1,zz0,zz1,mat,bias,db)=>{ if(axis==='y') decalY(out, plane,nrm, a0,a1, zz0,zz1, mat, bias, null, true, db);
                                             else            decalX(out, plane,nrm, a0,a1, zz0,zz1, mat, bias, null, true, db); };
    const ct=0.1, t0=out.length;
    if(LIVE){ const sl=sideLights(), tr=0.34, hw2=dw/2+sl+ct, top=z0+dh+tr+ct;   // phase 2: a transom over every door, sidelights where the era has them
      put(c-hw2, c+hw2, z0, top, 'trim', 0.55, 0.06);
      put(c-hw2-0.08, c+hw2+0.08, top, top+0.1, 'trim', 0.9, 0.05);
      put(c-hw2-0.03, c+hw2+0.03, top+0.1, top+0.15, 'trim', 0.35, 0.05);
      const g0=out.length, side=(s)=>{ const a0=c+s*(dw/2+0.05), a1=c+s*(dw/2+sl-0.02); return [Math.min(a0,a1), Math.max(a0,a1)]; };
      put(c-dw/2, c+dw/2, z0+dh+0.06, z0+dh+tr-0.03, 'lite', 0.0, 0.10);
      if(sl) for(const s of [-1,1]){ const [lo,hi]=side(s); put(lo, hi, z0+0.42, z0+dh-0.04, 'lite', 0.0, 0.10); }
      for(let i=g0;i<out.length;i++) out[i].em='door';
      for(const k of [1,2]){ const mx=c-dw/2+dw*k/3; put(mx-0.022, mx+0.022, z0+dh+0.06, z0+dh+tr-0.03, 'trim', 0.6, 0.14); }
      if(sl) for(const s of [-1,1]){ const [lo,hi]=side(s);
        for(const f of [0.36,0.68]){ const mz=z0+0.42+(dh-0.46)*f; put(lo, hi, mz-0.022, mz+0.022, 'trim', 0.6, 0.14); }
        put(lo, hi, z0+0.06, z0+0.36, 'door', -0.4, 0.12); }
    } else {
    put(c-dw/2-ct, c+dw/2+ct, z0, z0+dh+ct, 'trim', 0.55, 0.06);            // casing
    put(c-dw/2-ct-0.04, c+dw/2+ct+0.04, z0+dh+ct, z0+dh+ct+0.07, 'trim', 0.8, 0.05); // header cap
    }
    for(let i=t0;i<out.length;i++) out[i].tag='entry.frame';
    const t1=out.length;
    put(c-dw/2, c+dw/2, z0, z0+dh, 'door', 0.15, 0.10);                     // slab
    put(c-dw/2+0.13, c+dw/2-0.13, z0+0.22, z0+dh*0.46, 'door', -0.7, 0.12); // lower panel (recessed)
    if(LIVE){                                                                // live: four lights in the upper leaf; the hall lamp shows through at night
      const gx0=c-dw/2+0.15, gx1=c+dw/2-0.15, gz0=z0+dh*0.56, gz1=z0+dh-0.17, gm=(gz0+gz1)/2;
      put(gx0, gx1, gz0, gz1, 'glass', 0.0, 0.12); out[out.length-1].em='door';
      put(c-0.025, c+0.025, gz0, gz1, 'door', 0.3, 0.14); put(gx0, gx1, gm-0.025, gm+0.025, 'door', 0.3, 0.14);
    } else put(c-dw/2+0.13, c+dw/2-0.13, z0+dh*0.54, z0+dh-0.15, 'door', -0.7, 0.12); // upper panel
    put(c+dw/2-0.18, c+dw/2-0.11, z0+dh*0.48, z0+dh*0.48+0.07, 'trim', 0.95, 0.14); // knob
    for(let i=t1;i<out.length;i++) out[i].tag='door.leaf';
  }

  // small wooden stoop / steps under a ground-level door on a wall
  function stoop(out, axis, plane, nrm, c, fZ){
    const w=1.35, n=3, run=0.3, t0=out.length;
    for(let s=0;s<n;s++){
      const z1=fZ*(s+1)/n, outer=(n-s)*run, a=plane, bnd=plane+nrm*outer;
      const lo=Math.min(a,bnd), hi=Math.max(a,bnd);
      if(axis==='y') boxSolid(out, c-w/2,c+w/2, lo,hi, 0, z1, 'wood', null, 0.04);
      else           boxSolid(out, lo,hi, c-w/2,c+w/2, 0, z1, 'wood', null, 0.04);
    }
    tagFrom(out,t0,'entry.steps');
  }
  /* camera-first entry porch (live): a deck on its own footing, steps down along the door's normal, two posts and a
     gabled hood, and a lantern on a bracket off the post nearest the show face. For an eave door it is the front porch;
     for a side door it is what the camera sees of the way in at the square-on facing (the signpost). Tagged entry.* */
  function portico(out, b, axis, plane, nrm, c){
    const dz=b.fH-0.03, W2=0.82, D=1.05, run=0.3, n=3, rz=b.fH+2.82, hwc=Math.max(0.78, 0.6+sideLights()+0.08);   // phase 2: the hood clears the transom and spans the sidelights
    const P=(a,o,z)=> axis==='x' ? [plane+nrm*o, a, z] : [a, plane+nrm*o, z];
    const bx=(a0,a1,o0,o1,z0,z1,mat,tg,bb)=>{ const p=P(a0,o0,0), q=P(a1,o1,0), t=out.length;
      boxSolid(out, Math.min(p[0],q[0]),Math.max(p[0],q[0]), Math.min(p[1],q[1]),Math.max(p[1],q[1]), z0,z1, mat, null, bb||0); tagFrom(out,t,tg); };
    bx(c-W2,c+W2, 0,D, 0,dz, 'wood','entry.porch',0.1);
    for(let k=0;k<n;k++) bx(c-W2+0.1,c+W2-0.1, D+k*run, D+(k+1)*run, 0, dz*(n-k)/(n+1), 'wood','entry.steps',0.05);
    // the hood rides on knee brackets, no posts: at the diagonal facing the camera looks past the corner at the door
    const t0=out.length, ov=0.1, A=c-hwc-ov, B=c+hwc+ov, o1=0.95, ridge=rz+0.5;
    quad(out, P(A,-0.02,rz), P(A,o1,rz), P(c,o1,ridge), P(c,-0.02,ridge), 'roof', 0.1, null);
    quad(out, P(B,o1,rz), P(B,-0.02,rz), P(c,-0.02,ridge), P(c,o1,ridge), 'roof', 0.2, null);
    out.push(F([P(A,o1,rz),P(B,o1,rz),P(c,o1,ridge)],'trim',0.35,0.02,null,null));
    out.push(F([P(A,o1,rz-0.15),P(B,o1,rz-0.15),P(B,o1,rz),P(A,o1,rz)],'trim',0.45,0.03,null,null));
    for(const s of [-1,1]){ const k=hwc-0.08; bx(c+s*k-0.04,c+s*k+0.04, 0,0.09, rz-0.7,rz-0.02,'trim','entry.hood',0.15); bx(c+s*k-0.04,c+s*k+0.04, 0,0.62, rz-0.16,rz-0.04,'trim','entry.hood',0.25); }
    tagFrom(out,t0,'entry.hood');
    // a lamp post at the foot of the steps on the show face's side: out past the corner at the square-on facing
    const la=c+W2+0.42, lo=D+n*run+0.75, lt=out.length;
    bx(la-0.06,la+0.06, lo-0.06,lo+0.06, 0,1.72,'iron','entry.lantern');
    bx(la-0.13,la+0.13, lo-0.13,lo+0.13, 1.72,1.78,'iron','entry.lantern');
    const g0=out.length; bx(la-0.1,la+0.1, lo-0.1,lo+0.1, 1.78,2.02,'lamp','entry.lantern'); for(let i=g0;i<out.length;i++) out[i].em='lantern';
    bx(la-0.16,la+0.16, lo-0.16,lo+0.16, 2.02,2.09,'iron','entry.lantern');
    tagFrom(out,lt,'entry.lantern');
  }

  // gable-end wall (triangle) at y=yv facing ny, apex at x=xa z=ridgeZ, eaves x=±hw
  function gableEnd(out, yv, ny, hw, eaveZ, ridgeZ, xa, mat, tex){
    const e=0;
    const A=[-hw,yv,eaveZ], B2=[hw,yv,eaveZ], C=[xa,yv,ridgeZ];
    // uv: u = x+hw (along), v = z
    const uv = ny>0 ? [[0,eaveZ],[2*hw,eaveZ],[hw+xa,ridgeZ]] : [[2*hw,eaveZ],[0,eaveZ],[hw-xa,ridgeZ]];
    const P = ny>0 ? [A,B2,C] : [B2,A,C];
    out.push(F(P, mat, 0, 0, uv, tex));
  }

  // one gabled block: footprint x∈[-Wd/2,Wd/2] y∈[y0,y1], ridge along Y at x=xr
  function gableBlock(out, Wd, y0,y1, fZ, eaveZ, ridgeZ, xr, mats, siTex, opt){
    opt=opt||{};
    const hw=Wd/2, ov=opt.ov!=null?opt.ov:0.32;
    // walls (long eave walls + gable-end walls)
    wall(out, -hw,y0, -hw,y1, fZ, eaveZ, 'body', siTex);   // -X eave wall
    wall(out,  hw,y1,  hw,y0, fZ, eaveZ, 'body', siTex);   // +X eave wall
    if(!opt.openY0) wall(out,  hw,y0, -hw,y0, fZ, eaveZ, 'body', siTex);  // -Y gable wall
    if(!opt.openY1) wall(out, -hw,y1,  hw,y1, fZ, eaveZ, 'body', siTex);  // +Y gable wall
    // gable triangles
    if(!opt.openY0) gableEnd(out, y0,-1, hw, eaveZ, ridgeZ, xr, opt.gableMat||'body', opt.gableTex!==undefined?opt.gableTex:siTex);
    if(!opt.openY1) gableEnd(out, y1, 1, hw, eaveZ, ridgeZ, xr, opt.gableMat||'body', opt.gableTex!==undefined?opt.gableTex:siTex);
    // roof slopes with overhang
    const rTex = opt.roofTex||null;
    const yA=y0-ov, yB=y1+ov;
    // left slope (-X): eave (-hw-ov, eaveZ-slope) up to ridge (xr, ridgeZ)
    const eZ = eaveZ - (ov* (ridgeZ-eaveZ)/(hw+ (xr - (-hw)) ) ); // small drop at overhang
    quad(out, [-hw-ov,yA,eaveZ],[-hw-ov,yB,eaveZ],[xr,yB,ridgeZ],[xr,yA,ridgeZ], 'roof', -0.05, rTex);
    quad(out, [hw+ov,yB,eaveZ],[hw+ov,yA,eaveZ],[xr,yA,ridgeZ],[xr,yB,ridgeZ], 'roof', 0.15, rTex);
    if(LIVE) boxSolid(out, xr-0.1,xr+0.1, yA,yB, ridgeZ-0.07,ridgeZ+0.06, 'roof', null, 0.6);   // ridge cap
    // fascia boards + rake trim (defined white eaves)
    wall(out, -hw-ov,yB, -hw-ov,yA, eaveZ-0.22, eaveZ, 'trim', null, 0.35);
    wall(out,  hw+ov,yA,  hw+ov,yB, eaveZ-0.22, eaveZ, 'trim', null, 0.35);
    for(const yv of [yA,yB]){
      for(const sgn of [-1,1]){
        const ex=sgn*(hw+ov);
        out.push(F([[ex,yv,eaveZ],[xr,yv,ridgeZ],[xr,yv,ridgeZ-0.2],[ex,yv,eaveZ-0.2]],'trim',0.5,0.05,null,null));
      }
    }
    return { hw, yA, yB, ov };
  }

  function gambrelBlock(out, Wd, y0,y1, fZ, eaveZ, topZ, mats, siTex, opt){
    opt=opt||{};
    const hw=Wd/2, ov=0.3;
    let brk=hw*0.5, midZ=eaveZ+(topZ-eaveZ)*0.55;
    if(LIVE){ const rise=topZ-0.3-eaveZ; brk=hw*0.62; midZ=eaveZ+rise*0.66; topZ=eaveZ+rise+0.22; }   // phase 2: the interior rig's gambrel (steep lower, shallow upper), so the two register
    wall(out, -hw,y0, -hw,y1, fZ, eaveZ, 'body', siTex);
    wall(out,  hw,y1,  hw,y0, fZ, eaveZ, 'body', siTex);
    if(!opt.openY0) wall(out,  hw,y0, -hw,y0, fZ, eaveZ, 'body', siTex);
    if(!opt.openY1) wall(out, -hw,y1,  hw,y1, fZ, eaveZ, 'body', siTex);
    // gambrel gable = pentagon (both ends)
    for(const [yv,ny] of [[y0,-1],[y1,1]]){
      if((ny<0&&opt.openY0)||(ny>0&&opt.openY1)) continue;
      const pts = ny>0
        ? [[-hw,yv,eaveZ],[hw,yv,eaveZ],[brk,yv,midZ],[0,yv,topZ],[-brk,yv,midZ]]
        : [[hw,yv,eaveZ],[-hw,yv,eaveZ],[-brk,yv,midZ],[0,yv,topZ],[brk,yv,midZ]];
      out.push(F(pts,'body',0,0,LIVE?pts.map(p=>[ny>0?p[0]+hw:hw-p[0],p[2]]):null,LIVE?siTex:null));
    }
    const yA=y0-ov,yB=y1+ov, rTex=opt.roofTex||null;
    // lower steep slopes
    quad(out, [-hw-ov,yA,eaveZ],[-hw-ov,yB,eaveZ],[-brk,yB,midZ],[-brk,yA,midZ],'roof',-0.05,rTex);
    quad(out, [hw+ov,yB,eaveZ],[hw+ov,yA,eaveZ],[brk,yA,midZ],[brk,yB,midZ],'roof',0.15,rTex);
    // upper shallow slopes
    quad(out, [-brk,yA,midZ],[-brk,yB,midZ],[0,yB,topZ],[0,yA,topZ],'roof',0.0,rTex);
    quad(out, [brk,yB,midZ],[brk,yA,midZ],[0,yA,topZ],[0,yB,topZ],'roof',0.2,rTex);
    wall(out, -hw-ov,yB, -hw-ov,yA, eaveZ-0.22, eaveZ,'trim',null,0.35);
    wall(out,  hw+ov,yA,  hw+ov,yB, eaveZ-0.22, eaveZ,'trim',null,0.35);
    for(const yv of [yA,yB]){
      for(const sgn of [-1,1]){
        const segs=[[sgn*(hw+ov),eaveZ, sgn*brk,midZ],[sgn*brk,midZ, 0,topZ]];
        for(const [xa,za,xb2,zb] of segs)
          out.push(F([[xa,yv,za],[xb2,yv,zb],[xb2,yv,zb-0.2],[xa,yv,za-0.2]],'trim',0.5,0.05,null,null));
      }
    }
    return { hw, yA, yB, ov, midZ, brk, topZ };
  }

  // saltbox: ridge offset toward +X front; long rear slope down to a low eave at -X
  function saltboxBlock(out, Wd, y0,y1, fZ, eaveZ, ridgeZ, mats, siTex, opt){
    opt=opt||{};
    const hw=Wd/2, ov=0.3, xr=hw*0.25, rearEave=eaveZ-1.4;
    wall(out, -hw,y0, -hw,y1, fZ, rearEave, 'body', siTex);
    wall(out,  hw,y1,  hw,y0, fZ, eaveZ, 'body', siTex);
    for(const [yv,ny] of [[y0,-1],[y1,1]]){
      if((ny<0&&opt.openY0)||(ny>0&&opt.openY1)) continue;
      const pts = ny>0
        ? [[-hw,yv,rearEave],[hw,yv,eaveZ],[xr,yv,ridgeZ]]
        : [[hw,yv,eaveZ],[-hw,yv,rearEave],[xr,yv,ridgeZ]];
      const uvS=(P)=>P.map(p=>[ny>0?p[0]+hw:hw-p[0],p[2]]);            // phase 2: real (u, z), so the laps run across the gable
      out.push(F(pts,'body',0,0,LIVE?uvS(pts):null,LIVE?siTex:null));
      // fill the wall step under the sloped top of gable end
      const low=ny>0?[[-hw,yv,rearEave],[hw,yv,eaveZ],[hw,yv,fZ],[-hw,yv,fZ]]:[[hw,yv,eaveZ],[-hw,yv,rearEave],[-hw,yv,fZ],[hw,yv,fZ]];
      out.push(F(low,'body',0,0, LIVE?uvS(low):(ny>0?[[0,fZ],[2*hw,fZ],[2*hw,fZ],[0,fZ]]:[[0,fZ],[2*hw,fZ],[2*hw,fZ],[0,fZ]]), siTex));
    }
    const yA=y0-ov,yB=y1+ov, rTex=opt.roofTex||null;
    quad(out, [-hw-ov,yA,rearEave],[-hw-ov,yB,rearEave],[xr,yB,ridgeZ],[xr,yA,ridgeZ],'roof',-0.05,rTex);
    quad(out, [hw+ov,yB,eaveZ],[hw+ov,yA,eaveZ],[xr,yA,ridgeZ],[xr,yB,ridgeZ],'roof',0.15,rTex);
    if(LIVE) boxSolid(out, xr-0.1,xr+0.1, yA,yB, ridgeZ-0.07,ridgeZ+0.06, 'roof', null, 0.6);   // ridge cap
    wall(out, -hw-ov,yB, -hw-ov,yA, rearEave-0.22, rearEave,'trim',null,0.35);
    wall(out,  hw+ov,yA,  hw+ov,yB, eaveZ-0.22, eaveZ,'trim',null,0.35);
    for(const yv of [yA,yB]){
      out.push(F([[hw+ov,yv,eaveZ],[xr,yv,ridgeZ],[xr,yv,ridgeZ-0.2],[hw+ov,yv,eaveZ-0.2]],'trim',0.5,0.05,null,null));
      out.push(F([[-hw-ov,yv,rearEave],[xr,yv,ridgeZ],[xr,yv,ridgeZ-0.2],[-hw-ov,yv,rearEave-0.2]],'trim',0.5,0.05,null,null));
    }
    return { hw, yA, yB, ov, xr, rearEave };
  }

  function dormer(out, hw, eaveZ, ridgeZ, y, mats, siTex, roofTex){
    // proper gabled dormer sitting ON the +X roof slope, front face +X, window in it
    const dw=1.35, ov=0.16;
    const roofZ=(x)=> eaveZ + (ridgeZ-eaveZ)*(hw-x)/hw;
    const invRoof=(z)=> hw - (z-eaveZ)*hw/(ridgeZ-eaveZ);   // x where the main roof is at height z
    const xf=hw*0.56, zf=roofZ(xf), wallTop=zf+1.05, apex=wallTop+0.6;
    const xbE=Math.max(hw*0.05, invRoof(wallTop));   // eave line dies into the main roof
    const xbR=Math.max(hw*0.02, invRoof(apex));      // ridge dies into the main roof (further up-slope)
    const yl=y-dw/2, yr=y+dw/2, ylo=yl-ov, yro=yr+ov;
    // front vertical wall + gable triangle
    wall(out, xf,yl, xf,yr, zf, wallTop, 'body', siTex, 0.12);
    out.push(F([[xf,yl,wallTop],[xf,yr,wallTop],[xf,y,apex]],'body',0.12,0,
      [[0,wallTop],[dw,wallTop],[dw/2,apex]], siTex));
    // side eave walls: flat top at wallTop, bottom rides the main roof \u2014 never poke above the roof
    out.push(F([[xf,yl,zf],[xf,yl,wallTop],[xbE,yl,wallTop],[xbE,yl,roofZ(xbE)]],'body',-0.14,0,null,null));
    out.push(F([[xf,yr,wallTop],[xf,yr,zf],[xbE,yr,roofZ(xbE)],[xbE,yr,wallTop]],'body',0.04,0,null,null));
    // roof slopes: trapezoids from the front down to eaves, dying into the main roof at the back
    out.push(F([[xf,y,apex],[xbR,y,apex],[xbE,ylo,wallTop],[xf,ylo,wallTop]],'roof',0.0,0,null,null));
    out.push(F([[xf,yro,wallTop],[xbE,yro,wallTop],[xbR,y,apex],[xf,y,apex]],'roof',0.3,0,null,null));
    // rear closer (ties the ridge tail into the roof)
    out.push(F([[xbE,yl,wallTop],[xbE,yr,wallTop],[xbR,y,apex]],'roof',0.1,0,null,null));
    // rake trim along the two front slopes
    out.push(F([[xf,y,apex],[xf,ylo,wallTop],[xf,ylo,wallTop-0.16],[xf,y,apex-0.16]],'trim',0.55,0.05,null,null));
    out.push(F([[xf,yro,wallTop],[xf,y,apex],[xf,y,apex-0.16],[xf,yro,wallTop-0.16]],'trim',0.55,0.05,null,null));
    // window
    windowOn(out,'x', xf, 1, y, zf+0.26, 0.62, 0.6, mats);
  }

  // white cornerboard: slim vertical trim post straddling a wall corner
  function cornerboard(out, x, y, fZ, topZ){ const t=0.085; boxSolid(out, x-t,x+t, y-t,y+t, fZ, topZ, 'trim', null, 0.2); }

  // projecting front cross-gable on the +X eave wall (Gothic Revival centre gable / gablet).
  // A steep gabled bay reaching the ground; optional entry door; peak window; bargeboard.
  function crossGable(out, b, cy, siTex, roofTex, doorHere){
    const full=!!b.gableFull;
    const hw=b.Wd/2, proj=0.6, dw=full?3.5:2.9, fZ=b.fH, eaveZ=b.eaveZ, ridgeZ=b.ridgeZ;
    const roofZ=(x)=> eaveZ + (ridgeZ-eaveZ)*(hw-x)/hw;
    const xf=hw+proj, wallTop=eaveZ+0.15, steep=(b.pitch||1)+0.4;
    const apex=full ? ridgeZ-0.1 : Math.min(wallTop+(dw/2)*steep, ridgeZ-0.2);
    const xb=hw - hw*(apex-eaveZ)/(ridgeZ-eaveZ);
    const yl=cy-dw/2, yr=cy+dw/2;
    // projecting front wall + gable triangle (match main +X wall winding)
    wall(out, xf,yr, xf,yl, fZ, wallTop, 'body', siTex);
    out.push(F([[xf,yr,wallTop],[xf,yl,wallTop],[xf,cy,apex]],'body',0,0,[[0,wallTop],[dw,wallTop],[dw/2,apex]],siTex));
    // cheek side walls hw->xf
    out.push(F([[hw,yl,fZ],[xf,yl,fZ],[xf,yl,wallTop],[hw,yl,wallTop]],'body',-0.12,0,null,null));
    out.push(F([[xf,yr,fZ],[hw,yr,fZ],[hw,yr,wallTop],[xf,yr,wallTop]],'body',0.04,0,null,null));
    // twotone: carry the lower band + beltcourse across the projecting front
    if(b.siding==='twotone'){
      const bandZ=b.fH + b.wallH*0.52, bt=sidingTex('clapboard');
      decalX(out, xf,1, yl,yr, fZ, bandZ, 'lower', 0.05, bt);
      decalX(out, xf,1, yl,yr, bandZ, bandZ+0.12, 'trim', 0.5);
    }
    // roof slopes: horizontal ridge xf->xb at apex, down to eaves at wallTop
    quad(out, [xf,cy,apex],[xb,cy,apex],[xb,yl,wallTop],[xf,yl,wallTop],'roof',0.0,roofTex);
    quad(out, [xf,yr,wallTop],[xb,yr,wallTop],[xb,cy,apex],[xf,cy,apex],'roof',0.3,roofTex);
    // eave fascia + rake trim
    wall(out, xf,yl, xf,yr, wallTop-0.16, wallTop, 'trim', null, 0.35);
    out.push(F([[xf,cy,apex],[xf,yl,wallTop],[xf,yl,wallTop-0.18],[xf,cy,apex-0.18]],'trim',0.55,0.05,null,null));
    out.push(F([[xf,yr,wallTop],[xf,cy,apex],[xf,cy,apex-0.18],[xf,yr,wallTop-0.18]],'trim',0.55,0.05,null,null));
    cornerboard(out, xf, yl, fZ, wallTop); cornerboard(out, xf, yr, fZ, wallTop);
    // gingerbread teeth along the rake
    if(b.bargeboard && LIVE){ boxSolid(out, xf-0.045,xf+0.045, cy-0.045,cy+0.045, apex-0.05, apex+0.78, 'trim', null, 0.3); boxSolid(out, xf-0.09,xf+0.09, cy-0.09,cy+0.09, apex+0.42, apex+0.58, 'trim', null, 0.4); }   // phase 2: finial
    if(b.bargeboard){ const teeth=6;
      for(const s of [[yl,wallTop,cy,apex],[cy,apex,yr,wallTop]]){
        for(let i=0;i<teeth;i++){ const t0=i/teeth,t1=(i+1)/teeth;
          const ya=s[0]+(s[2]-s[0])*t0, za=s[1]+(s[3]-s[1])*t0, yb=s[0]+(s[2]-s[0])*t1, zb=s[1]+(s[3]-s[1])*t1;
          out.push(F([[xf,ya,za],[xf,yb,zb],[xf,(ya+yb)/2,(za+zb)/2-0.2]],'trim',0.6,0.06,null,null)); } }
    }
    // peak window (arched for gothic) + ground door or windows
    const peakStyle=(b.attic==='gothic')?'arched':b.windows, peakZ=wallTop+0.2;
    windowOn(out,'x', xf,1, cy, peakZ, 0.72, Math.min(full?1.5:0.95, apex-peakZ-0.35), {windows:peakStyle});
    if(doorHere){ doorOn(out,'x', xf,1, cy, fZ, 1.05, 2.15); stoop(out,'x', xf,1, cy, fZ); }
    else { windowOn(out,'x', xf,1, cy-dw*0.26, fZ+1.0, 0.6,1.05, {windows:b.windows});
           windowOn(out,'x', xf,1, cy+dw*0.26, fZ+1.0, 0.6,1.05, {windows:b.windows}); }
  }

  function chimney(out, x,y, topZ, mats){
    if(LIVE){ const big=CUR&&(CUR.shape==='cape'||CUR.shape==='saltbox'), hx=big?0.46:0.3, hy=big?0.36:0.26;   // phase 2: the big stack for a centre-chimney house; a clay pot on every flue
      boxSolid(out, x-hx,x+hx, y-hy,y+hy, topZ-2.0, topZ+1.3, 'brick', null, 0);
      boxSolid(out, x-hx-0.05,x+hx+0.05, y-hy-0.05,y+hy+0.05, topZ+0.98, topZ+1.1, 'brick', null, 0.3);
      boxSolid(out, x-hx-0.07,x+hx+0.07, y-hy-0.07,y+hy+0.07, topZ+1.3, topZ+1.42, 'stone', null, 0.2);
      for(const px of (big?[-hx*0.5,hx*0.5]:[0])){ boxSolid(out, x+px-0.1,x+px+0.1, y-0.1,y+0.1, topZ+1.42, topZ+1.72, 'pot', null, 0.1); boxSolid(out, x+px-0.12,x+px+0.12, y-0.12,y+0.12, topZ+1.66, topZ+1.75, 'pot', null, 0.3); }
      return; }
    boxSolid(out, x-0.28,x+0.28, y-0.24,y+0.24, topZ-0.3, topZ+1.3, 'brick', null, 0);
    // cap
    slab(out, [[x-0.34,y-0.3],[x+0.34,y-0.3],[x+0.34,y+0.3],[x+0.34,y+0.3]], topZ+1.32, 'stone', 0.3);
    boxSolid(out, x-0.34,x+0.34, y-0.3,y+0.3, topZ+1.3, topZ+1.42, 'stone', null, 0.2);
    if(LIVE) boxSolid(out, x-0.32,x+0.32, y-0.28,y+0.28, topZ+1.0, topZ+1.1, 'brick', null, 0.3);   // corbel course
  }

  // porch: deck + posts + railing + roof along +Y front (and +X side if wrap)
  function porch(out, b, roofTex, wrap){
    const hw=b.Wd/2, y1=b.Ln/2, deckZ=b.fH-0.05, depth=LIVE?1.6:1.9, postTop=b.eaveZ-(LIVE?0.25:0.5), pov=LIVE?0.08:0.2;
    const roofT=roofTex||null;
    const runs=[];
    runs.push({ax:'y', plane:y1+depth, x0:-hw, x1:hw, front:true});
    if(wrap) runs.push({ax:'x', plane:hw+depth, y0:y1, y1:y1-b.Ln*0.6, side:true});
    // deck slab (live: boards)
    if(LIVE) out.push(F([[-hw,y1,deckZ],[hw,y1,deckZ],[hw,y1+depth,deckZ],[-hw,y1+depth,deckZ]],'wood',0.2,0,[[-hw,y1],[hw,y1],[hw,y1+depth],[-hw,y1+depth]],(u,v)=>((((v%0.15)+0.15)%0.15)<0.02?-1:0)));
    else slab(out, [[-hw,y1],[hw,y1],[hw,y1+depth],[-hw,y1+depth]], deckZ, 'wood', 0.2);
    // deck fascia (phase 2, live: a skirt board over a lattice down to the ground, round the open sides too)
    const skirt=(x0,y0,x1,y1)=>{ wall(out, x0,y0, x1,y1, deckZ-0.16, deckZ, 'trim', null, 0.1); wall(out, x0,y0, x1,y1, 0, deckZ-0.16, 'wood', LATTICE, -0.2); };
    if(LIVE){ skirt(-hw,y1+depth, hw,y1+depth); skirt(-hw,y1, -hw,y1+depth); if(!wrap) skirt(hw,y1+depth, hw,y1); }
    else wall(out, -hw,y1+depth, hw,y1+depth, deckZ-0.35, deckZ, 'wood', null, -0.1);
    if(wrap){ slab(out, [[hw,y1-b.Ln*0.6],[hw+depth,y1-b.Ln*0.6],[hw+depth,y1],[hw,y1]], deckZ, 'wood', 0.2);
      if(LIVE) skirt(hw+depth,y1, hw+depth,y1-b.Ln*0.6); else wall(out, hw+depth,y1, hw+depth,y1-b.Ln*0.6, deckZ-0.35, deckZ, 'wood', null, 0.05); }
    // posts + rail (front)
    const nP=Math.max(2,Math.round(b.Wd/1.6));
    // live: posts flank the steps and the corners only, so nothing stands between the camera and the door
    const posts=LIVE?[-hw,-0.85,0.85,hw]:Array.from({length:nP+1},(_,i)=>-hw+ (b.Wd)*(i/nP));
    for(const px of posts){ const pw=LIVE?0.075:0.06;
      boxSolid(out, px-pw,px+pw, y1+depth-0.1,y1+depth+0.02, deckZ, postTop, 'trim', null, 0.1); }
    if(LIVE){ for(const [a0,a1] of [[-hw,-0.8],[0.8,hw]]) wall(out, a0,y1+depth-0.05, a1,y1+depth-0.05, deckZ+0.5, deckZ+0.62, 'trim', null, 0.2); }   // the rail opens at the steps
    if(LIVE){ const yp=y1+depth-0.04;                                     // phase 2: post bases and capitals, a bottom rail and square balusters; brackets with gingerbread
      for(const px of posts){ boxSolid(out, px-0.11,px+0.11, yp-0.1,yp+0.1, deckZ, deckZ+0.2, 'trim', null, 0.15); boxSolid(out, px-0.12,px+0.12, yp-0.1,yp+0.1, postTop-0.48, postTop-0.36, 'trim', null, 0.35); }
      for(const [a0,a1] of [[-hw+0.1,-0.85],[0.85,hw-0.1]]){ wall(out, a0,yp, a1,yp, deckZ+0.08, deckZ+0.14, 'trim', null, 0.1);
        for(let a=a0+0.1; a<a1-0.06; a+=0.16) boxSolid(out, a-0.026,a+0.026, yp-0.026,yp+0.026, deckZ+0.14, deckZ+0.5, 'trim', null, 0.05); }
      if(b.bargeboard) for(const px of posts) for(const s of [-1,1]){ if(Math.abs(px+s*0.42)>hw || (Math.abs(px)<1 && Math.sign(px)!==s)) continue;
        out.push(F([[px+s*0.08,yp+0.1,postTop-0.36],[px+s*0.42,yp+0.1,postTop-0.36],[px+s*0.08,yp+0.1,postTop-0.74]],'trim',0.5,0.04,null,null)); } }
    else wall(out, -hw,y1+depth-0.05, hw,y1+depth-0.05, deckZ+0.5, deckZ+0.62, 'trim', null, 0.2); // top rail
    // porch shed roof (wound so the top face catches the key light) + white fascia
    quad(out, [-hw-0.2,y1-0.1,b.eaveZ+0.05],[hw+0.2,y1-0.1,b.eaveZ+0.05],[hw+0.2,y1+depth+pov,postTop-0.15],[-hw-0.2,y1+depth+pov,postTop-0.15],'roof',0.3,roofT);
    wall(out, -hw-0.2,y1+depth+pov, hw+0.2,y1+depth+pov, postTop-0.3, postTop-0.12, 'trim', null, 0.4);
    if(LIVE){ for(const sx of (wrap?[-1]:[-1,1])){ const xx=sx*(hw+0.2);                   // phase 2: fascia up the open ends of the porch roof
        out.push(F([[xx,y1-0.1,b.eaveZ+0.05],[xx,y1+depth+pov,postTop-0.15],[xx,y1+depth+pov,postTop-0.33],[xx,y1-0.1,b.eaveZ-0.13]],'trim',0.4,0.02,null,null)); }
      if(b.shape!=='ell'){ const yf=y1+depth+pov, zb=postTop-0.15, A=1.05, za=zb+0.74;         // a pediment over the steps marks the way in
        out.push(F([[-A,yf+0.01,zb],[A,yf+0.01,zb],[0,yf+0.01,za]],'body',0.1,0.03,[[0,zb],[2*A,zb],[A,za]],sidingTex(b.siding)));
        for(const s of [-1,1]) out.push(F([[s*(A+0.1),yf+0.04,zb-0.06],[0,yf+0.04,za+0.08],[0,yf+0.04,za-0.12],[s*(A-0.1),yf+0.04,zb-0.06]],'trim',0.5,0.05,null,null));
        wall(out, -A-0.1,yf+0.04, A+0.1,yf+0.04, zb-0.12, zb+0.04, 'trim', null, 0.45);
        quad(out, [-A-0.12,yf+0.06,zb-0.03],[-A-0.12,y1,zb-0.03],[0,y1,za+0.06],[0,yf+0.06,za+0.06],'roof',0.1,roofT);
        quad(out, [A+0.12,y1,zb-0.03],[A+0.12,yf+0.06,zb-0.03],[0,yf+0.06,za+0.06],[0,y1,za+0.06],'roof',0.25,roofT); } }
    if(wrap){
      const ys=y1-b.Ln*0.6;
      for(let i=0;i<=Math.round(b.Ln*0.6/1.6);i++){ const py=y1-(b.Ln*0.6)*(i/Math.round(b.Ln*0.6/1.6));
        boxSolid(out, hw+depth-0.1,hw+depth+0.02, py-0.06,py+0.06, deckZ, postTop, 'trim', null, 0.1); }
      wall(out, hw+depth-0.05,y1, hw+depth-0.05,ys, deckZ+0.5, deckZ+0.62, 'trim', null, 0.2);
      quad(out, [hw-0.1,y1+0.2,b.eaveZ+0.05],[hw-0.1,ys-0.2,b.eaveZ+0.05],[hw+depth+0.2,ys-0.2,postTop-0.15],[hw+depth+0.2,y1+0.2,postTop-0.15],'roof',0.2,roofT);
      wall(out, hw+depth+0.2,y1+0.2, hw+depth+0.2,ys-0.2, postTop-0.3, postTop-0.12, 'trim', null, 0.4);
      if(LIVE){ const xp=hw+depth-0.04, nW=Math.round(b.Ln*0.6/1.6);
        for(let i=0;i<=nW;i++){ const py=y1-(b.Ln*0.6)*(i/nW); boxSolid(out, xp-0.1,xp+0.1, py-0.11,py+0.11, deckZ, deckZ+0.2, 'trim', null, 0.15); boxSolid(out, xp-0.1,xp+0.1, py-0.12,py+0.12, postTop-0.48, postTop-0.36, 'trim', null, 0.35); }
        wall(out, xp,y1, xp,ys, deckZ+0.08, deckZ+0.14, 'trim', null, 0.1);
        for(let a=ys+0.1; a<y1-0.06; a+=0.16) boxSolid(out, xp-0.026,xp+0.026, a-0.026,a+0.026, deckZ+0.14, deckZ+0.5, 'trim', null, 0.05);
        out.push(F([[hw-0.1,ys-0.2,b.eaveZ+0.05],[hw+depth+0.2,ys-0.2,postTop-0.15],[hw+depth+0.2,ys-0.2,postTop-0.33],[hw-0.1,ys-0.2,b.eaveZ-0.13]],'trim',0.4,0.02,null,null)); }
    }
    // steps (front centre; live: solid to the ground)
    const ts=out.length;
    for(let s=0;s<3;s++){ const sz=deckZ-0.12*(s+1), sy=y1+depth+0.12*s;
      boxSolid(out, -0.7,0.7, sy,sy+0.14, LIVE?0:sz-0.12, sz, 'wood', null, 0.05); }
    for(let i=ts;i<out.length;i++) out[i].tag='entry.steps';
    // live: a work bench against the wall at the porch's +X end (character v9.2 'bench', top 0.80 above the deck)
    if(LIVE){ const tb=out.length, bx0=hw-1.55, bx1=hw-0.35, by0=y1+0.14, by1=y1+0.62, tz=deckZ+0.80;
      boxSolid(out, bx0,bx1, by0,by1, tz-0.06,tz, 'wood', null, 0.25);
      for(const lx of [bx0+0.06,bx1-0.1]) for(const ly of [by0+0.04,by1-0.1]) boxSolid(out, lx,lx+0.06, ly,ly+0.06, deckZ,tz-0.06, 'wood', null, -0.1);
      boxSolid(out, bx0+0.1,bx1-0.1, by0+0.06,by1-0.06, deckZ+0.18,deckZ+0.22, 'wood', null, 0);
      tagFrom(out,tb,'yard.bench'); }
  }

  // bargeboard: white sawtooth along the +Y gable rake (gingerbread)
  function bargeboard(out, b, sgnY){
    const hw=b.Wd/2, yv=(sgnY||1)*(b.Ln/2+0.34), xr=0, n=8;
    for(let side=0;side<2;side++){
      const sgn=side?1:-1;
      for(let i=0;i<n;i++){
        const t0=i/n, t1=(i+1)/n;
        const x0=sgn*(hw*(1-t0)), z0=b.eaveZ+(b.ridgeZ-b.eaveZ)*t0;
        const x1=sgn*(hw*(1-t1)), z1=b.eaveZ+(b.ridgeZ-b.eaveZ)*t1;
        out.push(F([[x0,yv,z0],[x1,yv,z1],[x1+sgn*0.0,yv,z1-0.22]],'trim',0.6,0.08,null,null));
      }
    }
    // apex pendant
    out.push(F([[-0.08,yv,b.ridgeZ],[0.08,yv,b.ridgeZ],[0,yv,b.ridgeZ-0.5]],'trim',0.6,0.08,null,null));
    if(LIVE){ boxSolid(out, -0.045,0.045, yv-0.045,yv+0.045, b.ridgeZ-0.05, b.ridgeZ+0.78, 'trim', null, 0.3); boxSolid(out, -0.09,0.09, yv-0.09,yv+0.09, b.ridgeZ+0.42, b.ridgeZ+0.58, 'trim', null, 0.4); }   // phase 2: finial
  }

  function foundation(out, b, xhw, y0, y1){
    boxSolid(out, -xhw,xhw, y0,y1, 0, b.fH, 'stone', LIVE?STONE_TEX:null, -0.1);
  }

  // projecting front bay window on the +Y front: canted 'trapezoid' or sharper 'hex' half-bay.
  // one storey, flat front window, hipped cap rising back to the wall.
  function bayFront(out, b, siTex, kind){
    const y1=b.Ln/2, bz=b.fH, bTop=b.fH + b.wallH*0.64;
    const Wopen=3.0, Wfront = kind==='hex'?1.35:2.0, depth = kind==='hex'?1.25:0.9;
    const xoL=-Wopen/2, xoR=Wopen/2, xfL=-Wfront/2, xfR=Wfront/2, yf=y1+depth;
    boxSolid(out, xoL,xoR, y1,yf+0.02, 0,bz,'stone',null,-0.1);
    wall(out, xfL,yf, xoL,y1, bz,bTop,'body',siTex);   // left cheek
    wall(out, xfR,yf, xfL,yf, bz,bTop,'body',siTex);   // front face
    wall(out, xoR,y1, xfR,yf, bz,bTop,'body',siTex);   // right cheek
    cornerboard(out, xfL,yf, bz,bTop); cornerboard(out, xfR,yf, bz,bTop);
    const sill=bz+0.7, wh=Math.max(0.85, bTop-sill-0.35);
    windowOn(out,'y', yf,1, 0, sill, Math.min(1.25,Wfront-0.35), wh, b);
    // hipped roof rising to the wall
    const rBack=bTop+0.5, wov=0.18;
    quad(out,[xfL-wov,yf+wov,bTop],[xfR+wov,yf+wov,bTop],[xfR,y1,rBack],[xfL,y1,rBack],'roof',0.2,null);
    quad(out,[xoL-wov,y1,bTop],[xfL-wov,yf+wov,bTop],[xfL,y1,rBack],[xoL,y1,rBack],'roof',0.0,null);
    quad(out,[xfR+wov,yf+wov,bTop],[xoR+wov,y1,bTop],[xoR,y1,rBack],[xfR,y1,rBack],'roof',0.35,null);
    wall(out, xfL,yf, xfR,yf, bTop-0.14, bTop,'trim',null,0.35);
  }

  // paneled overhead garage door on a +Y wall
  function garageDoor(out, plane, nrm, c, z0, dw, dh){
    const put=(a0,a1,zz0,zz1,mat,bias,db)=> decalY(out, plane,nrm, a0,a1, zz0,zz1, mat, bias, null, true, db);
    put(c-dw/2-0.1, c+dw/2+0.1, z0, z0+dh+0.1, 'trim', 0.55, 0.06);               // casing
    put(c-dw/2-0.14, c+dw/2+0.14, z0+dh+0.1, z0+dh+0.17, 'trim', 0.8, 0.05);       // header
    put(c-dw/2, c+dw/2, z0, z0+dh, 'door', 0.1, 0.10);                             // slab
    for(let i=1;i<4;i++){ const pz=z0+dh*(i/4); put(c-dw/2+0.06, c+dw/2-0.06, pz-0.03, pz+0.03, 'door', -0.6, 0.12); }
    for(const gx of [-dw*0.25, dw*0.25]) put(c+gx-0.03, c+gx+0.03, z0+0.1, z0+dh-0.05, 'door', -0.5, 0.12);
  }
  // attached garage on the -X side: a lower forward-projecting gable, gable + door(s) face +Y
  function garage(out, b, siTex, roofTex, kind, x0,x1,yb,yf){
    const fZ=b.fH, gcx=(x0+x1)/2, gW=x1-x0, ov=0.25;
    const eave=fZ+2.95, ridge=Math.min(eave+(gW/2)*0.6, b.eaveZ-0.4);
    boxSolid(out, x0,x1, yb,yf, 0,fZ,'stone',null,-0.1);
    wall(out, x0,yb, x0,yf, fZ, eave,'body',siTex);   // -X
    wall(out, x1,yf, x1,yb, fZ, eave,'body',siTex);   // +X
    wall(out, x0,yf, x1,yf, fZ, eave,'body',siTex);   // +Y gable front
    wall(out, x1,yb, x0,yb, fZ, eave,'body',siTex);   // -Y back wall
    // rear gable triangle + a window
    out.push(F([[x1,yb,eave],[x0,yb,eave],[gcx,yb,ridge]],'body',0,0,[[0,eave],[gW,eave],[gW/2,ridge]],siTex));
    windowOn(out,'y', yb,-1, gcx, fZ+1.0, 0.9,1.0, {windows:b.windows});
    windowOn(out,'x', x0,-1, (yb+yf)/2, fZ+1.0, 0.7,1.0, {windows:b.windows});
    out.push(F([[x0,yf,eave],[x1,yf,eave],[gcx,yf,ridge]],'body',0,0,[[0,eave],[gW,eave],[gW/2,ridge]],siTex));
    quad(out,[x0-ov,yb,eave],[x0-ov,yf+ov,eave],[gcx,yf+ov,ridge],[gcx,yb,ridge],'roof',-0.05,roofTex);
    quad(out,[x1+ov,yf+ov,eave],[x1+ov,yb,eave],[gcx,yb,ridge],[gcx,yf+ov,ridge],'roof',0.15,roofTex);
    wall(out, x0-ov,yf+ov, x0-ov,yb, eave-0.2, eave,'trim',null,0.35);
    wall(out, x1+ov,yb, x1+ov,yf+ov, eave-0.2, eave,'trim',null,0.35);
    out.push(F([[x0-ov,yf,eave],[gcx,yf,ridge],[gcx,yf,ridge-0.18],[x0-ov,yf,eave-0.18]],'trim',0.5,0.05,null,null));
    out.push(F([[gcx,yf,ridge],[x1+ov,yf,eave],[x1+ov,yf,eave-0.18],[gcx,yf,ridge-0.18]],'trim',0.5,0.05,null,null));
    cornerboard(out, x0,yf,fZ,eave); cornerboard(out, x1,yf,fZ,eave);
    const dh=2.35;
    if(kind==='double'){ garageDoor(out, yf,1, gcx-gW*0.23, fZ, gW*0.4, dh); garageDoor(out, yf,1, gcx+gW*0.23, fZ, gW*0.4, dh); }
    else garageDoor(out, yf,1, gcx, fZ, Math.min(gW*0.66,2.4), dh);
  }

  // ======================= PHASE 2 · HOUSE STYLES (2026-09-26, live look only; classic:true untouched) =======================
  const hash2=(a,c)=>{ let h=Math.imul((a|0)^0x9e3779b9,0x85ebca6b)^Math.imul((c|0)+0x632be5ab,0xc2b2ae35); h^=h>>>13; h=Math.imul(h,0x27d4eb2d); h^=h>>>15; return (h>>>0)/4294967296; };
  // slate: 0.30 m courses of 0.30 m slates, staggered half a slate, with a darker or paler slate here and there
  function slateTex(u,v){ const CO=0.30, SW=0.30, row=Math.floor(v/CO), f=v-row*CO, uu=u+(row&1)*SW*0.5, col=Math.floor(uu/SW), su=uu-col*SW;
    if(f<0.045) return -2; if(f>CO-0.04) return 1; if(su<0.03) return -1; const h=hash2(row,col); return h<0.1?-1:(h>0.975?1:0); }
  // standing seam: the seam's shadow and the rib's lit edge
  function seamTex(u,v){ const SE=0.42, su=((u%SE)+SE)%SE; return su<0.05?-2:(su<0.085?1:0); }
  // asphalt: 0.2 m courses of three-tab strips, the slots staggered
  function asphaltTex(u,v){ const CO=0.2, row=Math.floor(v/CO), f=v-row*CO, uu=u+(row&1)*0.17, su=((uu%0.34)+0.34)%0.34;
    if(f<0.035) return -2; if(su<0.022 && f<CO*0.62) return -2; return hash2(row,Math.floor(uu/0.34))<0.08?-1:0; }
  // cedar: 0.22 m courses of shingles of random width, each its own weathered tone
  function cedarTex(u,v){ const CO=0.22, row=Math.floor(v/CO), f=v-row*CO; let x=u+hash2(row,7)*0.3, k=0, w=0;
    for(;;){ w=0.12+hash2(row,k)*0.16; if(x<w) break; x-=w; k++; if(k>400) break; }
    if(f<0.04) return -2; if(x<0.025) return -1; const h=hash2(row*31+k,3); return h<0.22?-1:(h>0.9?1:0); }
  const LATTICE=(u,v)=>{ const P=0.3, a=((u+v)%P+P)%P, c=((u-v)%P+P)%P; return (a<0.08||c<0.08)?0:-2; };
  const sideLights=()=> (CUR && (CUR.era==='colonial'||CUR.era==='seaside'||CUR.era==='gothic')) ? 0.26 : 0;
  // eave return: the cornice turns the corner onto the gable for 0.6 m, tucked under the rake
  function eaveReturn(out, xc, sx, yv, ny, hw, ov, ez){
    const xo=xc+sx*(hw+ov-0.03), xi=xc+sx*(hw-0.6), yo=yv+ny*(ov-0.05);
    boxSolid(out, Math.min(xo,xi),Math.max(xo,xi), Math.min(yv,yo),Math.max(yv,yo), ez-0.44, ez-0.03, 'trim', null, 0.25);
  }
  // gambrel shed dormer (Dutch colonial): one long dormer across the steep lower slope of the +X side, sashes in a row
  function shedDormer(out, b, g, n, siTex, roofTex){
    const hw=g.hw, brk=g.brk, mz=g.midZ, tz=g.topZ, ez=b.eaveZ;
    const zl=(x)=> ez+(hw-x)/(hw-brk)*(mz-ez), su=(tz-mz)/brk, ss=0.22;
    const xf=hw-(hw-brk)*0.3, zt=mz+0.12, xo=xf+0.14, xm=(brk*su-0.12-xo*ss)/(su-ss), zm=mz+(brk-xm)*su;
    const L=Math.min(b.Ln-2.0, n*1.75+0.5), ya=-L/2, yb=L/2;
    wall(out, xf,ya, xf,yb, zl(xf), zt, 'body', siTex);
    for(const [yy,bb] of [[ya,-0.14],[yb,0.04]]){
      out.push(F([[xf,yy,zl(xf)],[xf,yy,zt],[brk,yy,mz]],'body',bb,0,[[0,zl(xf)],[0,zt],[xf-brk,mz]],siTex));
      out.push(F([[xf,yy,zt],[xm,yy,zm],[brk,yy,mz]],'body',bb,0,[[0,zt],[xf-xm,zm],[xf-brk,mz]],siTex)); }
    quad(out, [xo,ya-0.14,zt],[xo,yb+0.14,zt],[xm,yb+0.14,zm+0.02],[xm,ya-0.14,zm+0.02],'roof',0.3,roofTex);
    wall(out, xo,ya-0.14, xo,yb+0.14, zt-0.18, zt, 'trim', null, 0.35);
    for(const yy of [ya-0.14,yb+0.14]) out.push(F([[xo,yy,zt],[xm,yy,zm+0.02],[xm,yy,zm-0.14],[xo,yy,zt-0.18]],'trim',0.4,0.02,null,null));
    cornerboard(out, xf, ya, zl(xf), zt); cornerboard(out, xf, yb, zl(xf), zt);
    const wz=zl(xf)+0.28, wh2=Math.min(0.95, zt-wz-0.3);
    for(let i=0;i<n;i++) windowOn(out,'x', xf,1, ya+L*((i+0.5)/n), wz, 0.72, wh2, {windows:b.windows});
  }
  // schoolhouse belfry on the ridge near the front gable (opts.belfry): sided base, open bell stage, pyramid cap
  function belfry(out, b, yc){
    const s=0.62, p=b.pitch||0.95, z0=b.ridgeZ-s*p-0.12, z1=b.ridgeZ+0.7, z2=z1+0.95, z3=z2+1.1, e=s+0.18;
    boxSolid(out, -s,s, yc-s,yc+s, z0, z1, 'body', sidingTex(b.siding), 0.1);
    boxSolid(out, -s-0.07,s+0.07, yc-s-0.07,yc+s+0.07, z1, z1+0.12, 'trim', null, 0.3);
    for(const [px,py] of [[-1,-1],[1,-1],[1,1],[-1,1]]) boxSolid(out, px*(s-0.02)-0.07,px*(s-0.02)+0.07, yc+py*(s-0.02)-0.07,yc+py*(s-0.02)+0.07, z1+0.12, z2, 'trim', null, 0.2);
    boxSolid(out, -0.2,0.2, yc-0.2,yc+0.2, z1+0.38, z1+0.8, 'iron', null, 0.1);
    boxSolid(out, -s-0.1,s+0.1, yc-s-0.1,yc+s+0.1, z2, z2+0.14, 'trim', null, 0.3);
    const zt=z2+0.14, A=[-e,yc-e,zt], B=[e,yc-e,zt], C=[e,yc+e,zt], D=[-e,yc+e,zt], T=[0,yc,z3];
    tri(out, A,B,T,'roof',0.0); tri(out, B,C,T,'roof',0.2); tri(out, C,D,T,'roof',0.3); tri(out, D,A,T,'roof',-0.1);
    boxSolid(out, -0.04,0.04, yc-0.04,yc+0.04, z3-0.1, z3+0.55, 'iron', null, 0.2);
  }

  function build(b){
    LIVE=!!b.live; CUR=b;
    const out=[];
    const sideDoor = layoutOf(b).door;                    // camera-first side entry (live only), or null
    const siTex = sidingTex(b.siding);
    const roofTex = LIVE ? (/^metal/.test(b.roof)?seamTex:b.roof==='asphaltGrey'?asphaltTex:b.roof==='asphaltBrown'?cedarTex:slateTex) : b.roof==='metal'
      ? (u,v)=>{ const seam=0.42; return (((u%seam)+seam)%seam)<0.05? -2 : 0; }
      : (u,v)=>{ const CO=0.34; const f=((v%CO)+CO)%CO; return f<0.05?-2:(f>CO-0.05?1:0); };
    const hw=b.Wd/2, y0=-b.Ln/2, y1=b.Ln/2;
    const twotone = b.siding==='twotone';
    const bandZ = b.fH + (b.wallH)*0.52;   // beltcourse height for twotone

    // FOUNDATION
    foundation(out, b, hw+0.05, y0, y1);
    if(LIVE) boxSolid(out, -hw-0.035,hw+0.035, y0-0.035,y1+0.035, b.fH-0.02,b.fH+0.15, 'trim', null, 0.1);   // water table: the siding stops on a board

    // MAIN MASS by shape
    let blk;
    if(b.shape==='gambrel'){
      blk=gambrelBlock(out, b.Wd, y0,y1, b.fH, b.eaveZ, b.ridgeZ+0.3, null, siTex, {roofTex});
    } else if(b.shape==='saltbox'){
      blk=saltboxBlock(out, b.Wd, y0,y1, b.fH, b.eaveZ, b.ridgeZ, null, siTex, {roofTex});
    } else {
      // gable / ell / cape share the gable core
      const opt={roofTex, ov:b.ov};
      if(b.shape==='ell') opt.openY1=false;
      blk=gableBlock(out, b.Wd, y0,y1, b.fH, b.eaveZ, b.ridgeZ, 0, null, siTex, opt);
    }

    // TWO-TONE overlay: lower band re-skinned + beltcourse (drawn as decals in front of walls)
    if(twotone){
      const bt=sidingTex('clapboard');
      for(const [xv,nx] of [[-hw,-1],[hw,1]])
        decalX(out, xv,nx, y0,y1, b.fH, bandZ, 'lower', 0.05, bt);
      for(const [yv,ny] of [[y0,-1],[y1,1]])
        decalY(out, yv,ny, -hw,hw, b.fH, bandZ, 'lower', 0.05, bt);
      // beltcourse trim
      for(const [xv,nx] of [[-hw,-1],[hw,1]]) decalX(out, xv,nx, y0,y1, bandZ, bandZ+0.12,'trim',0.5);
      for(const [yv,ny] of [[y0,-1],[y1,1]]) decalY(out, yv,ny, -hw,hw, bandZ, bandZ+0.12,'trim',0.5);
    }

    // CORNERBOARDS (white vertical trim at the wall corners)
    if(b.shape==='saltbox'){
      const rear=blk.rearEave!=null?blk.rearEave:b.eaveZ;
      cornerboard(out, hw,y0,b.fH,b.eaveZ); cornerboard(out, hw,y1,b.fH,b.eaveZ);
      cornerboard(out,-hw,y0,b.fH,rear);    cornerboard(out,-hw,y1,b.fH,rear);
    } else {
      cornerboard(out,-hw,y0,b.fH,b.eaveZ); cornerboard(out, hw,y0,b.fH,b.eaveZ);
      if(b.shape!=='ell'){ cornerboard(out,-hw,y1,b.fH,b.eaveZ); cornerboard(out, hw,y1,b.fH,b.eaveZ); }
    }

    // phase 2 (live): eave returns at the gable corners and a frieze board under every eave
    if(LIVE){ const t0=out.length;
      if(b.shape==='gable'||b.shape==='cape'||b.shape==='ell'||b.shape==='saltbox')
        for(const sx of [-1,1]) for(const [yv,ny] of [[y0,-1],[y1,1]]){ if(b.shape==='ell' && ny>0 && sx<0) continue;   // the wing covers that corner
          eaveReturn(out, 0, sx, yv, ny, hw, blk.ov, (sx<0&&b.eaveL!=null)?b.eaveL:b.eaveZ); }
      for(const sx of [-1,1]){ const ez=(sx<0&&b.eaveL!=null)?b.eaveL:b.eaveZ; decalX(out, sx*hw, sx, y0, y1, ez-0.3, ez, 'trim', 0.3, null, true, 0.04); }
      tagFrom(out,t0,'trim.cornice'); }

    // ELL forward wing: a lower cross-gable projecting off the +Y front (gable faces +Y)
    if(b.shape==='ell'){
      const tW=out.length;
      const wingW=b.Wd*0.62, wingProj=b.Ln*0.42;
      const wx0=-hw+0.25, wx1=wx0+wingW, wcx=(wx0+wx1)/2;
      const wyb=y1-0.5, wy1=y1+wingProj, wmy=(wyb+wy1)/2;
      const wingEave=b.fH + b.wallH - 0.5;                                 // eave a touch below main
      const apexZ=Math.min(wingEave + (wingW/2)*b.pitch, b.ridgeZ-0.6);    // ridge clearly below main ridge
      const wov=0.28;
      boxSolid(out, wx0,wx1, wyb,wy1, 0,b.fH,'stone',null,-0.1);
      wall(out, wx0,wyb, wx0,wy1, b.fH, wingEave,'body',siTex);            // -X wall
      wall(out, wx1,wy1, wx1,wyb, b.fH, wingEave,'body',siTex);            // +X wall
      wall(out, wx0,wy1, wx1,wy1, b.fH, wingEave,'body',siTex);            // +Y gable wall
      out.push(F([[wx0,wy1,wingEave],[wx1,wy1,wingEave],[wcx,wy1,apexZ]],'body',0,0,
        [[0,wingEave],[wingW,wingEave],[wingW/2,apexZ]],siTex));
      // roof slopes (ridge along Y at x=wcx)
      quad(out,[wx0-wov,wyb,wingEave],[wx0-wov,wy1+wov,wingEave],[wcx,wy1+wov,apexZ],[wcx,wyb,apexZ],'roof',-0.05,roofTex);
      quad(out,[wx1+wov,wy1+wov,wingEave],[wx1+wov,wyb,wingEave],[wcx,wyb,apexZ],[wcx,wy1+wov,apexZ],'roof',0.15,roofTex);
      // fascia + rake + cornerboards
      wall(out, wx0-wov,wy1+wov, wx0-wov,wyb, wingEave-0.22, wingEave,'trim',null,0.35);
      wall(out, wx1+wov,wyb, wx1+wov,wy1+wov, wingEave-0.22, wingEave,'trim',null,0.35);
      out.push(F([[wx0-wov,wy1,wingEave],[wcx,wy1,apexZ],[wcx,wy1,apexZ-0.2],[wx0-wov,wy1,wingEave-0.2]],'trim',0.5,0.05,null,null));
      out.push(F([[wcx,wy1,apexZ],[wx1+wov,wy1,wingEave],[wx1+wov,wy1,wingEave-0.2],[wcx,wy1,apexZ-0.2]],'trim',0.5,0.05,null,null));
      cornerboard(out, wx0,wy1,b.fH,wingEave); cornerboard(out, wx1,wy1,b.fH,wingEave);
      if(LIVE){ for(const sx of [-1,1]) eaveReturn(out, wcx, sx, wy1, 1, wingW/2, wov, wingEave);                       // phase 2: the wing's returns and frieze
        for(const [xv,nx] of [[wx0,-1],[wx1,1]]) decalX(out, xv,nx, wyb,wy1, wingEave-0.3, wingEave, 'trim', 0.3, null, true, 0.04); }
      // entry door + stoop on the wing front; attic window in the gable; a side window
      if(sideDoor) windowOn(out,'y', wy1,1, wcx, b.fH+1.0, 0.7,1.1, {windows:b.windows});   // a side entry: the wing keeps a window where its door was
      else { doorOn(out,'y', wy1,1, wcx, b.fH, 0.95, 2.05); stoop(out,'y', wy1,1, wcx, b.fH); }
      if(b.attic!=='none') windowOn(out,'y', wy1,1, wcx, wingEave-0.5, 0.62,0.72, {windows:b.windows});
      windowOn(out,'x', wx1,1, wmy, b.fH+1.0, 0.7,1.1, {windows:b.windows});
      tagFrom(out,tW,'show.wing');
    }

    // WINDOWS on main mass ------------------------------------------------
    const sillG = b.fH+1.0, sillU = b.fH + b.wallH*0.55 + 0.5;
    const ww=0.82, wh=1.15;
    const nLong = Math.max(1, Math.round((b.Ln/2.4) * (0.5+b.winDensity)));
    const bayKind = (b.bay==='trapezoid'||b.bay==='hex') ? b.bay : (b.bay===true?'trapezoid':null);
    const bayFrontOn = !!bayKind && b.shape!=='ell';
    const isCape = b.shape==='cape';
    const hasPorch = ((b.porch==='front'||b.porch==='wrap')) && !bayFrontOn && !isCape;
    const eaveDoor = isCape || (!hasPorch && b.shape!=='ell');   // cape entry always centres on the long +X face
    const eaveDoorDraw = eaveDoor && !sideDoor;
    const ncg = Math.min(3, b.crossGable|0);
    const cgYs=[]; if(ncg===1) cgYs.push(0); else for(let i=0;i<ncg;i++) cgYs.push(-b.Ln*0.3 + b.Ln*0.6*(i/Math.max(1,ncg-1)));
    const cgDoorIdx = (eaveDoorDraw && ncg%2===1) ? (ncg-1)/2 : -1;
    { const t0=out.length; cgYs.forEach((cy,i)=> crossGable(out, b, cy, siTex, roofTex, i===cgDoorIdx)); tagFrom(out,t0,'show.crossGable'); }
    const inCG=(c)=> cgYs.some(cy=>Math.abs(c-cy)<1.6);
    const wrapSide = (b.porch==='wrap') && ncg===0 && !bayFrontOn && !isCape;
    const garageOn = (b.garage==='single'||b.garage==='double') && b.shape!=='ell';
    const gW = b.garage==='double'?5.6:3.2, gx1=-hw+0.15, gx0=gx1-gW, gyf=b.Ln/2+0.2, gyb=gyf-5.4;
    if(garageOn) garage(out, b, siTex, roofTex, b.garage, gx0,gx1,gyb,gyf);
    for(const [xv,nx] of [[-hw,-1],[hw,1]]){
      for(let i=0;i<nLong;i++){ const c=y0+ b.Ln*((i+0.5)/nLong);
        if(nx>0 && inCG(c)) continue;                                          // wall hidden behind a cross-gable
        if(nx>0 && eaveDoorDraw && cgDoorIdx<0 && Math.abs(c)<1.1) continue;   // leave room for the door
        if(nx<0 && garageOn && c>gyb-0.3 && c<gyf) continue;                   // wall hidden behind the garage
        const underWrap = nx>0 && wrapSide && c > (b.Ln/2 - b.Ln*0.6);         // covered by the wrap porch
        const nearSide = !!sideDoor && sideDoor.axis==='x' && nx===sideDoor.nrm && Math.abs(c-sideDoor.y)<1.2;   // room for a side door
        if(!underWrap && !nearSide) windowOn(out,'x', xv,nx, c, sillG, ww,wh, b);
        if(LIVE && isCape){ if(b.wallH>4.2) windowOn(out,'x', xv,nx, c, b.eaveZ-1.08, ww*1.1, 0.56, {windows:'knee'}); }    // phase 2: a storey-and-a-half's knee windows under the frieze
        else if(LIVE && b.shape==='saltbox'){ if(nx>0 && b.wallH>4.1) windowOn(out,'x', xv,nx, c, sillU, ww,wh*0.92, b); }  // the saltbox front is two full storeys, its back one
        else if(b.wallH>4.4) windowOn(out,'x', xv,nx, c, sillU, ww,wh*0.92, b);   // 2nd storey
      }
    }
    if(eaveDoorDraw && cgDoorIdx<0){ doorOn(out,'x', hw,1, 0, b.fH, 1.0, 2.1); if(LIVE) portico(out, b, 'x', hw, 1, 0); else stoop(out,'x', hw,1, 0, b.fH); }
    if(sideDoor){ const sc=sideDoor.axis==='x'?sideDoor.y:sideDoor.x; doorOn(out, sideDoor.axis, sideDoor.plane, sideDoor.nrm, sc, b.fH, 1.0, 2.1); portico(out, b, sideDoor.axis, sideDoor.plane, sideDoor.nrm, sc); }
    // gable ends: door on +Y under the porch; else windows flanking
    const gableDoor = hasPorch && b.shape!=='ell';
    for(const [yv,ny] of [[y0,-1],[y1,1]]){
      if(ny>0 && gableDoor){
        if(sideDoor) windowOn(out,'y', yv,ny, 0, sillG, ww,wh, b); else doorOn(out,'y', yv,ny, 0, b.fH, 1.0, 2.1);
        windowOn(out,'y', yv,ny, -hw*0.55, sillG, ww,wh, b);
        windowOn(out,'y', yv,ny,  hw*0.55, sillG, ww,wh, b);
      } else {
        const frontGround = ny>0;
        const skipL = frontGround && ((bayFrontOn && hw*0.42<1.7) || b.shape==='ell');
        const skipR = frontGround && (bayFrontOn && hw*0.42<1.7);
        const sd=(a)=> !!sideDoor && sideDoor.axis==='y' && ny===sideDoor.nrm && Math.abs(a-sideDoor.x)<1.2;
        if(!skipL && !sd(-hw*0.42)) windowOn(out,'y', yv,ny, -hw*0.42, sillG, ww,wh, b);
        if(!skipR && !sd( hw*0.42)) windowOn(out,'y', yv,ny,  hw*0.42, sillG, ww,wh, b);
      }
      if(b.wallH>4.4 || (LIVE && b.shape==='saltbox' && b.wallH>4.1)){ windowOn(out,'y', yv,ny, -hw*0.42, sillU, ww,wh*0.9, b);
                       windowOn(out,'y', yv,ny,  hw*0.42, sillU, ww,wh*0.9, b); }
      // attic window in the peak
      if(LIVE && b.shape==='gambrel'){ for(const s of [-1,1]) windowOn(out,'y', yv,ny, s*hw*0.34, b.eaveZ+0.55, ww*0.9, 1.0, b); }   // phase 2: the gambrel storey's pair
      else if(LIVE && b.attic==='gothic'){ const az=b.eaveZ+(b.ridgeZ-b.eaveZ)*0.36; windowOn(out,'y', yv,ny, 0, az-0.45, 0.66, 0.8, {windows:'arched'}); }   // a pointed-arch sash in the gothic peak
      else if(b.attic!=='none'){
        const az=b.eaveZ+ (b.ridgeZ-b.eaveZ)*0.42;
        if(b.attic==='round' || b.attic==='gothic'){
          const s=0.42; // diamond
          const P = ny>0
            ? [[0,yv+0.02,az-s],[s,yv+0.02,az],[0,yv+0.02,az+s],[-s,yv+0.02,az]]
            : [[0,yv-0.02,az-s],[-s,yv-0.02,az],[0,yv-0.02,az+s],[s,yv-0.02,az]];
          out.push(F(P,'trim',0.55,0.06,null,null,true));
          const P2=P.map(p=>[p[0]*0.62,p[1]+0.001*ny,az+(p[2]-az)*0.62]);
          out.push(F(P2,'glass',0.2,0.07,null,null,true));
        } else {
          windowOn(out,'y', yv,ny, 0, az-0.5, 0.7, 0.9, b);
        }
      }
    }

    // BAY WINDOW projection on the +Y front (canted trapezoid / sharper hex)
    if(bayFrontOn){ const t0=out.length; bayFront(out, b, siTex, bayKind); tagFrom(out,t0,'show.bay'); }

    // DORMERS on the +X slope
    const nd=Math.min(3,b.dormers|0), tD=out.length;
    if(LIVE && b.shape==='gambrel'){ if(nd>0) shedDormer(out, b, blk, nd+1, siTex, roofTex); }   // phase 2: the gambrel's long shed dormer
    else for(let i=0;i<nd;i++){
      const dy = y0 + b.Ln*((i+0.5)/Math.max(1,nd));
      dormer(out, hw, b.eaveZ, b.ridgeZ, dy, {windows:b.windows}, siTex, roofTex);
    }
    tagFrom(out,tD,'show.dormers');

    // CHIMNEYS (near ridge, on gable sides)
    const nc=Math.min(2,b.chimneys|0);
    if(nc>=1) chimney(out, 0.0, y0+b.Ln*0.22, b.ridgeZ);
    if(nc>=2) chimney(out, 0.0, y1-b.Ln*0.18, b.ridgeZ);
    if(b.belfry && (b.shape==='gable'||b.shape==='ell'||b.shape==='cape')){ const t0=out.length; belfry(out, b, y1-1.25); tagFrom(out,t0,'show.belfry'); }

    // PORCH (suppressed when a front bay or cape long-face entry occupies the front)
    if(!bayFrontOn && !isCape){ const t0=out.length;
      if(b.porch==='front') porch(out, b, roofTex, false);
      if(b.porch==='wrap')  porch(out, b, roofTex, ncg===0);   // drop the +X wrap side when cross-gables occupy it
      tagFrom(out,t0,'show.porch');
    }

    // BARGEBOARD gingerbread
    if(b.bargeboard){ const t0=out.length; bargeboard(out, b); if(LIVE) bargeboard(out, b, -1); tagFrom(out,t0,'show.bargeboard'); }   // live: both gables, so every listed facing shows one

    return out;
  }

  // ---- weathering / night post pass + RGBA ----------------------------------
  function post(bufs, b){
    const { rbuf, ibuf, nbuf, dep } = bufs;
    const N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    // depth-edge darkening (mass separation)
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j;
          const idx=Math.max(0,ibuf[far]-2); out[far]=rbuf[far][idx]; }
      }
    }
    // weather + moss speckle
    const wx=b.weather;
    if(wx>0.02){
      const rnd=mulberry32(1234|((b.size*97)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='body'||m==='lower') && rnd()<wx*0.07){ out[i]=rbuf[i][Math.max(0,Math.min(rbuf[i].length-1,ibuf[i]-1))]; }
        if(m==='roof' && rnd()<wx*0.035){ out[i]=mix(out[i], '#47543c', 0.28+rnd()*0.16); }
      }
    }
    // night: warm glow halo around lit glass
    if(b.night){
      for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x;
        if(nbuf[i]==='glass'){ for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*W+(x+dx);
          if(out[j] && nbuf[j]!=='glass' && nbuf[j]!=='glassHi') out[j]=mix(out[j],'#f0c66a',0.28); } } }
    }
    // despeckle: drop isolated 1px islands (stray rake/trim specks)
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue;
      let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; }
    }
    // 1px keyline
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue;
      let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
      if(touch) out[i]=KEY;
    }
    return out;
  }
  function toRGBA(cols){
    const rgba=new Uint8ClampedArray(W*H*4);
    for(let i=0;i<W*H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; }
      const [r,g,bl]=hex2rgb(c); rgba[i*4]=r; rgba[i*4+1]=g; rgba[i*4+2]=bl; rgba[i*4+3]=255; }
    return rgba;
  }

  function render(dir, opts){
    opts = (typeof opts==='number')?{elev:opts}:(opts||{});
    if(!opts.classic && root.CoastalPass && root.CoastalPass.light) return renderLive(dir, opts);
    let b=resolve(opts);
    const MATS=makeMats(b);
    let faces=build(b);
    const LC=root.BuildingLifecycle;                       // construction phase / dereliction pass
    if(LC && LC.active(opts)){ const r=LC.apply(faces, MATS, b, opts); faces=r.faces; b=r.b; }
    if(root.CoastalPass) faces=root.CoastalPass.apply('house',faces,MATS,b,opts);
    const bufs=paint(faces, {dir, elev:opts.elev}, MATS);
    return toRGBA(post(bufs, b));
  }
  // Entrance selection mirrors build(): ell wing, porch, long wall, or central cross-gable.
  // Metric records are additive; door remains the existing screen-space label/glow anchor.
  function entrance(opts){
    const b=resolve(opts||{}), hw=b.Wd/2, y1=b.Ln/2, LY=layoutOf(b);
    if(LY.door){ const d=LY.door; return {axis:d.axis, facing:d.facing, x:d.x, y:d.y, z:b.fH, width:1.0, height:2.1, entry:b.entry, show:LY.show}; }
    const bayOn=!!b.bay && b.shape!=='ell';
    const hasPorch=(b.porch==='front'||b.porch==='wrap')&&!bayOn&&b.shape!=='cape';
    let x=0, y=y1, axis='y', width=1.0, height=2.1;
    if(b.shape==='ell'){
      x=-hw+0.25+b.Wd*0.62/2; y=y1+b.Ln*0.42; width=0.95; height=2.05;
    } else if(b.shape==='cape'||!hasPorch){
      const ncg=Math.min(3,b.crossGable|0), cross=ncg>0&&ncg%2===1;
      x=hw+(cross?0.6:0); y=0; axis='x'; width=cross?1.05:1.0; height=cross?2.15:2.1;
    }
    return {axis, facing:axis==='x'?'+X':'+Y', x,y,z:b.fH,width,height, entry:'front', show:LY.show};
  }
  function anchors(dir, opts){
    opts=opts||{}; const b=resolve(opts), B=camBasis({dir,elev:opts.elev});
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return {x:v.sx,y:v.sy}; };
    const nc=Math.min(2,b.chimneys|0), ch=[];
    const cz=b.ridgeZ+(b.live?1.76:1.45);                   // phase 2: the live stacks carry clay pots
    if(nc>=1) ch.push(pj(0.0, -b.Ln/2+b.Ln*0.22, cz));
    if(nc>=2) ch.push(pj(0.0,  b.Ln/2-b.Ln*0.18, cz));
    const e0=entrance(opts), mx=b.mirror, mp=(p)=>mx?[-p[0],p[1]]:p, e=mx?mirE(e0):e0;
    const base={ chimneys:ch, door:pj(e.x,e.y,e.z+1.0), threshold:pj(e.x,e.y,e.z), entrance:e, ridge:pj(0,0,b.ridgeZ), Wd:b.Wd, Ln:b.Ln };
    if(!b.live) return base;
    // camera-first anchors: model metres (m), world metres at this facing ([east, north] from the pivot, w) and screen px
    const LY=layoutOf(b), ap=approachOf(b,e0), path=entryPathOf(b,e0,ap), n=WALLN[e.facing];
    const at=(p0)=>{ const p=mp(p0), s=pj(p[0],p[1],0), w=worldOf(p,dir); return { x:s.x, y:s.y, m:[p[0],p[1]], w:[+w[0].toFixed(3),+w[1].toFixed(3)] }; };
    return Object.assign(base, { show:mx?FLIPW[LY.show]:LY.show, doorWall:e.facing, doorFaces:compass(worldOf(n,dir)), approach:at(ap), entryPath:path.map(at), mirror:mx });
  }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }
  // ---- phase 2b: mirror (the same house flipped across its ridge, x -> -x; live only) ----
  const FLIPW={'+X':'-X','-X':'+X','+Y':'+Y','-Y':'-Y'};
  const noMir=(o)=>{ if(!o||!o.mirror) return o; const c=Object.assign({},o); delete c.mirror; return c; };
  function flipFaces(faces){ for(const f of faces){ f.v=f.v.map(p=>[-p[0],p[1],p[2]]).reverse(); if(f.uv) f.uv=f.uv.slice().reverse(); } return faces; }
  const mirE=(e)=>Object.assign({},e,{x:-e.x, facing:FLIPW[e.facing], show:FLIPW[e.show]||e.show});
  function entranceM(opts){ const e=entrance(opts); return resolve(opts||{}).mirror ? mirE(e) : e; }

  // ======================= CAMERA-FIRST PASS (2026-09-25) =======================
  /* The camera is fixed (orthographic, 40 deg, looking north: down the screen is world south) and a house turns by using
     another facing's picture. Every house has one SHOW FACE, the wall it is designed to be seen from: +Y where the door
     sits under a front porch or on an ell wing, +X for an eave-door house. Its way in is on the show face (entry 'front')
     or on the side wall to the left or right as you look at it (entry 'left' | 'right'), near the show face's corner, with
     a portico whose steps, hood and lantern stand out past the corner. placement() lists the facings the house is designed
     to stand at and the world direction its door faces there, measured by projection; doors face S, SE, SW, E or W only.
     The live look is the default; classic:true draws the 2026-09-16 picture byte for byte. */
  const COMPASS=['N','NE','E','SE','S','SW','W','NW'], WALLN={'+X':[1,0],'-X':[-1,0],'+Y':[0,1],'-Y':[0,-1]};
  function worldOf(v, dir){ const th=dir*Math.PI/4, c=Math.cos(th), s=Math.sin(th); return [v[0]*c-v[1]*s, v[0]*s+v[1]*c]; }
  function compass(w){ const a=(Math.atan2(w[0],w[1])*180/Math.PI+360)%360; return COMPASS[Math.round(a/45)%8]; }
  function layoutOf(b){
    const hw=b.Wd/2, hl=b.Ln/2;
    const bayKind=(b.bay==='trapezoid'||b.bay==='hex')?b.bay:(b.bay===true?'trapezoid':null), bayFrontOn=!!bayKind&&b.shape!=='ell', isCape=b.shape==='cape';
    const hasPorch=(b.porch==='front'||b.porch==='wrap')&&!bayFrontOn&&!isCape, eaveDoor=isCape||(!hasPorch&&b.shape!=='ell');
    const show=eaveDoor?'+X':'+Y', side=!!b.live&&b.entry!=='front';
    let door=null;
    if(side){ const s=b.entry==='left'?1:-1;          // left / right as you stand looking at the show face
      door = show==='+Y' ? {axis:'x',plane:s*hw,nrm:s,facing:s>0?'+X':'-X',x:s*hw,y:hl-1.35}
                         : {axis:'y',plane:-s*hl,nrm:-s,facing:s>0?'-Y':'+Y',x:hw-1.35,y:-s*hl}; }
    return {bayKind,bayFrontOn,isCape,hasPorch,eaveDoor,show,side,door};
  }
  /* where the camera-first goods stand (live): a woodpile on a rack (top 0.80, v9.2 lift/place) and a rain barrel under a
     downpipe, on the side wall opposite a side door (else -X, or +Y for an eave-door house), near the show face's corner */
  function dooryardOf(b){
    if(!b.live) return null;
    const L=layoutOf(b), hw=b.Wd/2, hl=b.Ln/2, top=0.80, wrap=b.porch==='wrap'&&!L.bayFrontOn&&!L.isCape&&Math.min(3,b.crossGable|0)===0;
    const garage=(b.garage==='single'||b.garage==='double')&&b.shape!=='ell';
    let wall = L.show==='+Y' ? ((L.door&&L.door.facing==='-X')||garage ? '+X' : '-X') : ((L.door&&L.door.facing==='+Y') ? '-Y' : '+Y');
    if(wall==='+X' && wrap) wall=(L.door&&L.door.facing==='-X')?'-Y':'-X';
    let wp, barrel, pipe, backPipe;
    if(wall==='-X'||wall==='+X'){ const s=wall==='+X'?1:-1;
      wp={x0:Math.min(s*(hw+0.08),s*(hw+0.64)),x1:Math.max(s*(hw+0.08),s*(hw+0.64)),y0:hl-2.75,y1:hl-1.05,top};
      barrel={x:s*(hw+0.42),y:hl-0.42}; pipe={x:s*(hw+0.34),y:hl-0.42}; backPipe={x:-s*(hw+0.14),y:-hl-0.12}; }
    else { const s=wall==='+Y'?1:-1;
      wp={x0:hw-2.75,x1:hw-1.05,y0:Math.min(s*(hl+0.08),s*(hl+0.64)),y1:Math.max(s*(hl+0.08),s*(hl+0.64)),top};
      barrel={x:hw+0.42,y:s*(hl-0.42)}; pipe={x:hw+0.34,y:s*(hl-0.42)}; backPipe={x:-hw-0.14,y:-s*(hl+0.12)}; }
    return {wall, woodpile:wp, barrel, pipe, backPipe};
  }
  // the building on the ground, as rectangles in model metres (walk: a deck or steps a person climbs)
  function footprint(b){
    const hw=b.Wd/2, hl=b.Ln/2, L=layoutOf(b), R=[], ncg=Math.min(3,b.crossGable|0);
    const add=(id,x0,x1,y0,y1,walk)=>R.push({id,x0:+Math.min(x0,x1).toFixed(3),x1:+Math.max(x0,x1).toFixed(3),y0:+Math.min(y0,y1).toFixed(3),y1:+Math.max(y0,y1).toFixed(3),walk:!!walk});
    add('house',-hw-0.06,hw+0.06,-hl-0.06,hl+0.06);
    if(b.shape==='ell'){ const x0=-hw+0.25, x1=x0+b.Wd*0.62, wy1=hl+b.Ln*0.42, wc=(x0+x1)/2; add('wing',x0,x1,hl-0.5,wy1); if(!L.side) add('wingStoop',wc-0.68,wc+0.68,wy1,wy1+0.9,true); }
    if(L.bayFrontOn) add('bay',-1.5,1.5,hl,hl+(L.bayKind==='hex'?1.25:0.9)+0.02);
    if(!L.bayFrontOn&&!L.isCape&&(b.porch==='front'||b.porch==='wrap')){ const pd=b.live?1.6:1.9; add('porch',-hw,hw,hl,hl+pd,true); add('porchSteps',-0.7,0.7,hl+pd,hl+pd+0.36,true);
      if(b.porch==='wrap'&&ncg===0) add('porchWrap',hw,hw+pd,hl-b.Ln*0.6,hl,true); }
    if(ncg){ const dw=b.gableFull?3.5:2.9; const ys=ncg===1?[0]:Array.from({length:ncg},(_,i)=>-b.Ln*0.3+b.Ln*0.6*(i/Math.max(1,ncg-1))); for(const cy of ys) add('crossGable',hw,hw+0.6,cy-dw/2,cy+dw/2); }
    if((b.garage==='single'||b.garage==='double')&&b.shape!=='ell'){ const gW=b.garage==='double'?5.6:3.2, gx1=-hw+0.15; add('garage',gx1-gW,gx1,hl+0.2-5.4,hl+0.2); }
    if(L.eaveDoor&&!L.side&&!(ncg>0&&ncg%2===1)){ if(b.live){ add('portico',hw,hw+1.95,-0.82,0.82,true); add('lampPost',hw+2.57,hw+2.89,1.1,1.42); } else add('stoop',hw,hw+0.9,-0.68,0.68,true); }
    if(L.side){ const d=L.door, c=d.axis==='x'?d.y:d.x; if(d.axis==='x') add('portico',d.plane,d.plane+d.nrm*1.95,c-0.82,c+0.82,true); else add('portico',c-0.82,c+0.82,d.plane,d.plane+d.nrm*1.95,true);
      if(d.axis==='x') add('lampPost',d.plane+d.nrm*2.57,d.plane+d.nrm*2.89,c+1.08,c+1.40); else add('lampPost',c+1.08,c+1.40,d.plane+d.nrm*2.57,d.plane+d.nrm*2.89); }
    const Y=dooryardOf(b); if(Y){ const w=Y.woodpile; add('woodpile',w.x0,w.x1,w.y0,w.y1); add('barrel',Y.barrel.x-0.34,Y.barrel.x+0.34,Y.barrel.y-0.34,Y.barrel.y+0.34); }
    return R;
  }
  const inRect=(q,x,y,r)=> x+r>q.x0 && x-r<q.x1 && y+r>q.y0 && y-r<q.y1;
  // where a person stands to walk in: out along the door's normal, at least 1.5 m, past every deck and step
  function approachOf(b,e){ const n=WALLN[e.facing], R=footprint(b), r=0.25;
    for(let t=1.5;t<10;t+=0.05){ const x=e.x+n[0]*t, y=e.y+n[1]*t; if(!R.some(q=>inRect(q,x,y,r))) return [+x.toFixed(3),+y.toFixed(3)]; }
    return [e.x+n[0]*1.5, e.y+n[1]*1.5]; }
  /* the start of the path to the door, model metres from the approach: straight out for a front entry; for a side entry
     along the side wall to the show face's side and round the corner, so the lane in front meets it */
  function entryPathOf(b,e,ap){ const n=WALLN[e.facing], L=layoutOf(b), s=WALLN[L.show];
    if(!L.side) return [ap,[+(ap[0]+n[0]*2).toFixed(3),+(ap[1]+n[1]*2).toFixed(3)]];
    let far=-1e9; for(const q of footprint(b)){ const v=s[0]?(s[0]>0?q.x1:-q.x0):(s[1]>0?q.y1:-q.y0); if(v>far) far=v; }
    const lead=+(far+0.9).toFixed(3), p1=s[0]?[s[0]*lead,ap[1]]:[ap[0],s[1]*lead], p2=s[0]?[s[0]*lead,+(ap[1]*0.3).toFixed(3)]:[+(ap[0]*0.3).toFixed(3),s[1]*lead];
    return [ap,p1,p2]; }
  /* the facings the house is designed to stand at: via 'door' where the door itself faces the camera (S, SE or SW), via
     'signpost' where a side door is edge-on (E or W) and its portico, steps, hood and lantern show the way in */
  function placement(opts){
    opts=opts||{}; const b=resolve(opts), e=entrance(opts), L=layoutOf(b), mx=b.mirror, fl=(k)=>mx?FLIPW[k]:k, dn=WALLN[fl(e.facing)], sn=WALLN[fl(L.show)], list=[];
    for(let d=0;d<8;d++){ const dw=worldOf(dn,d), sw=worldOf(sn,d), dS=-dw[1], sS=-sw[1]; let via=null;
      if(!L.side){ if(dS>0.5) via='door'; }
      else if(dS>0.5&&sS>0.5) via='door'; else if(sS>0.99&&Math.abs(dw[1])<0.01) via='signpost';
      if(via) list.push({dir:d, doorFaces:compass(dw), showFaces:compass(sw), via}); }
    if(L.side) list.sort((p,q)=>(p.via==='door'?0:1)-(q.via==='door'?0:1));
    // an ell carries its porch on the +X side of the wing: it is designed for the facings where +X does not face north, and a
    // side door under its wrap porch has no signpost at the square-on facing (the brief: list it only if it passes)
    const xFeat = b.shape==='ell' || Math.min(3,b.crossGable|0)>0 || (b.live && L.show==='+Y' && (b.dormers|0)>0);   // a wing, a cross gable or dormers on the +X side
    let keep = !xFeat ? list : list.filter(f=>{ const xw=worldOf([mx?-1:1,0],f.dir); if(xw[1]>0.5) return false;
      return !(L.side && b.porch==='wrap' && L.door.facing==='+X' && f.via==='signpost'); });
    if(b.live){ const other=L.show==='+Y'?[mx?-1:1,0]:[0,1], seen=(n,d)=>-worldOf(n,d)[1]>0.3;   // phase 2: the diagonal that shows the show face and its designed neighbour comes first
      keep=keep.map((f,i)=>({f,i,s:(f.dir%2?2:0)+(seen(other,f.dir)?1:0)})).sort((p,q)=>((p.f.via==='door'?0:1)-(q.f.via==='door'?0:1))||(q.s-p.s)||(p.i-q.i)).map(o=>o.f); }
    return {show:fl(L.show), entry:fl(e.facing), entryKind:L.side?b.entry:'front', mirror:mx, facings:keep};
  }

  // ---- character v9.2: the verbs a house offers outside, each at a fixture height every creator body fits ----
  const CHAR92={ rig:'characterIsoRig9.js', exportSymbol:'CharacterIso9', revision:'9.2', radiusMax:0.229, footprintMax:[0.458,0.370],
    fits:{ bench:[0.643,0.853], knife:[0.598,0.760], load:[0.718,0.978], rest:[0,1.012], rail:[0.549,0.724], rung:[0.211,0.273] }, bedZ:0.30,
    source:'export/character-v9.2-import-kit/reports/worldfit.txt, every creator body (375)' };
  function stations(opts){
    opts=opts||{}; const b=resolve(opts), e=entrance(opts), L=layoutOf(b), n=WALLN[e.facing], ap=approachOf(b,e), Y=dooryardOf(b), R=footprint(b), r=CHAR92.radiusMax;
    const yaw=(v)=>+(Math.atan2(v[0],v[1])*180/Math.PI).toFixed(1), P=root.PropIso, S=[];
    const fit=(k,z)=>{ const f=CHAR92.fits[k]; return z>=f[0]-1e-9&&z<=f[1]+1e-9; };
    S.push({ id:'door', verb:'enter', clip:'walk', at:[e.x,e.y,e.z], stand:ap, standZ:0, faceYaw:yaw([-n[0],-n[1]]), clearWidth:e.width, fits:e.width>=CHAR92.footprintMax[0]+0.3,
      note:'walk in over the threshold; the doorway is wider than the largest creator body with 0.15 m a side' });
    const onDeck=R.find(q=>q.walk&&inRect(q,e.x+n[0]*0.5,e.y+n[1]*0.5,0.01));
    S.push({ id:'doorstep', verb:'handOver', clip:'reach', fixture:'rest', fixtureZ:0.95, fit:CHAR92.fits.rest.slice(), fits:fit('rest',0.95),
      at:[e.x+n[0]*0.2,e.y+n[1]*0.2,e.z+0.95], stand:[+(e.x+n[0]*0.62).toFixed(3),+(e.y+n[1]*0.62).toFixed(3)], standZ:onDeck?+(b.fH-0.03).toFixed(3):0, standOn:onDeck?onDeck.id:'ground',
      faceYaw:yaw([-n[0],-n[1]]), note:'a villager in the doorway hands over or takes a thing at the named rest stowV (0.95)' });
    if(Y){ const w=Y.woodpile, cxw=(w.x0+w.x1)/2, cyw=(w.y0+w.y1)/2, sx=Y.wall==='-X'?-1:Y.wall==='+X'?1:0, sy=Y.wall==='-Y'?-1:Y.wall==='+Y'?1:0;
      const st=[+(sx? (sx>0?w.x1:w.x0)+sx*0.5 : cxw).toFixed(3), +(sy? (sy>0?w.y1:w.y0)+sy*0.5 : cyw).toFixed(3)];
      S.push({ id:'woodpile', verb:'takeWood', clip:'lift', also:['place'], fixture:'load', fixtureZ:w.top, fit:CHAR92.fits.load.slice(), fits:fit('load',w.top),
        at:[cxw,cyw,w.top], stand:st, standZ:0, faceYaw:yaw([-sx,-sy]), note:'split wood stacked on a rack so the top course is at a v9.2 load height' });
      const rim=P&&P.height?+P.height('barrel',{wood:'driftwood',weather:.5}).toFixed(3):0.9, bxs=Y.barrel.x+(sx||0)*0.62, bys=Y.barrel.y+(sy?sy*0.62:(L.show==='+Y'?0.62:0));
      S.push({ id:'rainBarrel', verb:'drawWater', clip:'lift', fixture:'load', fixtureZ:rim, fit:CHAR92.fits.load.slice(), fits:fit('load',rim),
        at:[Y.barrel.x,Y.barrel.y,rim], stand:[+bxs.toFixed(3),+bys.toFixed(3)], standZ:0, note:'dip a pail at the rim; the downpipe feeds it from the gutter' }); }
    const porch=R.find(q=>q.id==='porch');
    if(porch&&b.live){ const hl=b.Ln/2, hw=b.Wd/2;
      S.push({ id:'porchBench', verb:'mendNets', clip:'bench', fixture:'bench', fixtureZ:0.80, fit:CHAR92.fits.bench.slice(), fits:fit('bench',0.80),
        at:[hw-0.95,hl+0.38,b.fH-0.03+0.80], stand:[+(hw-0.95).toFixed(3),+(hl+1.1).toFixed(3)], standZ:+(b.fH-0.03).toFixed(3), standOn:'porch', faceYaw:180,
        note:'a work bench against the wall at the porch end; 0.80 above the deck' }); }
    // reachability on the ground: from the approach, round the building, to every ground standing point
    const box0=R.reduce((a,q)=>[Math.min(a[0],q.x0),Math.max(a[1],q.x1),Math.min(a[2],q.y0),Math.max(a[3],q.y1)],[1e9,-1e9,1e9,-1e9]), st=0.1, pad=3;
    const X0=box0[0]-pad, Y0=box0[2]-pad, nx=Math.ceil((box0[1]-box0[0]+2*pad)/st)+1, ny=Math.ceil((box0[3]-box0[2]+2*pad)/st)+1, seen=new Uint8Array(nx*ny);
    const free=(x,y)=>!R.some(q=>inRect(q,x,y,r)), cell=(p)=>[Math.round((p[0]-X0)/st),Math.round((p[1]-Y0)/st)];
    const [si,sj]=cell(ap), Q=[]; if(free(ap[0],ap[1])){ seen[sj*nx+si]=1; Q.push(sj*nx+si); }
    for(let q=0;q<Q.length;q++){ const k=Q[q], i=k%nx, j=(k-i)/nx; for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const ii=i+dx, jj=j+dy; if(ii<0||jj<0||ii>=nx||jj>=ny) continue; const kk=jj*nx+ii; if(seen[kk]) continue;
      if(!free(X0+ii*st,Y0+jj*st)) continue; seen[kk]=1; Q.push(kk); } }
    const reach=(p)=>{ const [i,j]=cell(p); for(let dj=-2;dj<=2;dj++) for(let di=-2;di<=2;di++){ const ii=i+di, jj=j+dj; if(ii>=0&&jj>=0&&ii<nx&&jj<ny&&seen[jj*nx+ii]) return true; } return false; };
    for(const s of S){ if(s.standOn&&s.standOn!=='ground'){ const steps=R.find(q=>q.id===(s.standOn==='porch'?'porchSteps':s.standOn)); s.reachable=!!steps; s.via=s.standOn==='porch'?'porch steps (the rail opens at them)':'the portico steps'; }
      else s.reachable=reach(s.stand); }
    if(b.mirror) for(const s of S){ if(s.at) s.at=[-s.at[0],s.at[1],s.at[2]]; if(s.stand) s.stand=[-s.stand[0],s.stand[1]]; if(s.faceYaw!=null) s.faceYaw=-s.faceYaw; }
    return { character:CHAR92, stations:S, requests:[{ verb:'sit', note:'v9.2 has no seated clip but drive; a porch chair or bench seat stays decor until one lands' }] };
  }

  // ---- occupancy and time: who is home and awake decides which rooms burn a lamp once it is dark ----
  const SCHEDULES={
    family:{ label:'family household', wake:6.5, out:[], upstairs:21.25, bed:22.0 },
    fisher:{ label:'fishing household: up before the boats, home by mid-afternoon', wake:4.25, out:[[5.25,15.5]], upstairs:20.75, bed:21.25 },
    elder:{ label:'elder living alone', wake:6.75, out:[[10,11.5]], upstairs:20.25, bed:20.75 },
    school:{ label:'schoolhouse: lamps while a class or an evening meeting is in', resident:false, open:[[8,15.75],[18.5,20.25]] },
    empty:{ label:'empty house', resident:false, open:[] },
  };
  const ROOMS0=()=>({kitchen:0,parlour:0,upper:0,hall:0});
  function lightsOn(opts, sky){
    opts=opts||{}; const CPL=root.CoastalPass&&root.CoastalPass.light; sky=sky||(CPL?CPL.skyOf(opts):null);
    const need=CPL?CPL.lampNeed(sky):0, t=opts.time!=null?+opts.time:(sky&&sky.time!=null?sky.time:14), key=SCHEDULES[opts.schedule]?opts.schedule:'family', S=SCHEDULES[key];
    const inR=(r)=>(r||[]).some(([a,c])=>t>=a&&t<c);
    let home, awake, rooms=ROOMS0(), lantern=0, why;
    if(S.resident===false){ const open=inR(S.open); home=awake=open; if(open) rooms={kitchen:0.8,parlour:1,upper:0,hall:1}; lantern=open?1:0; why=open?'open':'closed'; }
    else { awake=t>=S.wake&&t<S.bed; home=!awake||!inR(S.out);
      if(awake&&home){ if(t>=S.upstairs) rooms={kitchen:0,parlour:0.35,upper:1,hall:0.5}; else if(t<S.wake+1.25) rooms={kitchen:1,parlour:0,upper:0.3,hall:0.6}; else rooms={kitchen:0.85,parlour:1,upper:0,hall:0.8}; }
      lantern=awake?1:0; why=!awake?'asleep':home?'home':'out'; }
    const occ=opts.occupancy;
    if(occ==='away'||occ==='out'){ home=false; rooms=ROOMS0(); lantern=S.resident===false?0:1; why='out (game)'; }
    else if(occ==='asleep'){ home=true; awake=false; rooms=ROOMS0(); lantern=0; why='asleep (game)'; }
    else if(occ==='home'||occ==='up'){ home=awake=true; if(!rooms.kitchen&&!rooms.parlour&&!rooms.upper) rooms={kitchen:0.85,parlour:1,upper:0,hall:0.8}; lantern=1; why='home (game)'; }
    else if(occ&&typeof occ==='object'){ if(occ.rooms) rooms=Object.assign(ROOMS0(),occ.rooms); if(occ.lantern!=null) lantern=+occ.lantern; why='game'; }
    const lv={}; for(const k in rooms) lv[k]=+(rooms[k]*need).toFixed(3);
    return { time:+(+t).toFixed(2), need:+need.toFixed(3), schedule:key, home, awake, why, rooms:lv, lantern:+(lantern*need).toFixed(3) };
  }
  const vary=(i)=>0.74+0.26*((((i+1)*2654435761)>>>0)%1000)/1000;
  // group the glass into windows (id, wall, room); rooms by position: upper above the ground-floor heads, kitchen behind
  function windowsOf(faces,b){
    const Wn=[];
    for(const f of faces){ if(f.em||(f.mat!=='glass'&&f.mat!=='glassHi')) continue;
      const xs=f.v.map(p=>p[0]), ys=f.v.map(p=>p[1]), zs=f.v.map(p=>p[2]), x0=Math.min(...xs), x1=Math.max(...xs), y0=Math.min(...ys), y1=Math.max(...ys), z0=Math.min(...zs), z1=Math.max(...zs);
      const ax=(x1-x0)<0.01?'x':(y1-y0)<0.01?'y':'z', mx=(x0+x1)/2, my=(y0+y1)/2;
      let w=Wn.find(q=>q.ax===ax&&Math.abs(q.cx-mx)<0.7&&Math.abs(q.cy-my)<0.7&&z0<q.z1+0.12&&z1>q.z0-0.12);
      if(!w){ w={id:Wn.length,ax,cx:mx,cy:my,z0,z1}; Wn.push(w); } else { w.z0=Math.min(w.z0,z0); w.z1=Math.max(w.z1,z1); }
      f.em='win:'+w.id; }
    for(const w of Wn){ w.n=w.ax==='x'?[Math.sign(w.cx)||1,0]:w.ax==='y'?[0,Math.sign(w.cy)||1]:[0,0]; w.room=w.z0>b.fH+1.35?'upper':(w.cy<-0.3?'kitchen':'parlour'); }
    return Wn;
  }
  const MODELS=new Map(), LIVE_SKIP={sky:1,time:1,cloud:1,rain:1,fog:1,snow:1,wind:1,occupancy:1,schedule:1,outline:1,elev:1,night:1};
  function liveModel(opts){
    const CP=root.CoastalPass; if(!CP||!CP.light) return null;
    const o={}; for(const k of Object.keys(opts).sort()) if(!LIVE_SKIP[k]&&typeof opts[k]!=='function') o[k]=opts[k];
    const key=JSON.stringify(o)+'|'+(CP.enabled?1:0); let m=MODELS.get(key); if(m) return m;
    let b=resolve(opts); const MATS=makeMats(b); let faces=build(b);
    const LC=root.BuildingLifecycle; if(LC&&LC.active(opts)){ const r=LC.apply(faces,MATS,b,opts); faces=r.faces; b=r.b; }
    faces=CP.apply('house',faces,MATS,b,noMir(opts));
    if(b.mirror) flipFaces(faces);                                     // phase 2b: mirror after the pass has dressed the house
    const wins=windowsOf(faces,b), lamps=[];
    for(const f of faces) if(f.em==='lantern'){ const c=f.v.reduce((a,p)=>[a[0]+p[0]/f.v.length,a[1]+p[1]/f.v.length,a[2]+p[2]/f.v.length],[0,0,0]);
      let l=lamps.find(q=>Math.hypot(q.p[0]-c[0],q.p[1]-c[1])<0.5); if(!l){ l={p:c,n:1}; lamps.push(l); } else { l.p=l.p.map((v,i)=>(v*l.n+c[i])/(l.n+1)); l.n++; } }
    const door=faces.filter(f=>f.em==='door').map(f=>f.v[0]);
    m=CP.light.model(faces,MATS); m.b=b; m.wins=wins; m.lamps=lamps; m.doorGlass=door.length?door[0]:null; m.entrance=entranceM(opts);
    if(MODELS.size>32) MODELS.delete(MODELS.keys().next().value); MODELS.set(key,m); return m;
  }
  function frame(dir, opts, fo){ opts=opts||{}; const m=liveModel(opts); if(!m) return null; const B=camBasis({dir,elev:opts.elev});
    const fr=root.CoastalPass.light.frame(m,{ct:B.ct,st:B.stt,se:B.se,ce:B.ce,S,ox:cx,oy:groundY},W,H,fo); fr.dir=dir; fr.b=m.b; fr.opts=opts; return fr; }
  // every light the house gives at this hour: windows by room, the lantern(s), the door's lights; model metres + a ground pool
  function lampSet(m, st){
    const L=[], e=m.entrance, n=WALLN[e.facing];
    for(const w of m.wins){ const lv=+((st.rooms[w.room]||0)*vary(w.id)).toFixed(3); if(w.ax==='z') continue;
      L.push({kind:'window',id:'win:'+w.id,room:w.room,level:lv,p:[w.cx+w.n[0]*0.4,w.cy+w.n[1]*0.4,(w.z0+w.z1)/2],pool:[w.cx+w.n[0]*(w.room==='upper'?1.6:1.2),w.cy+w.n[1]*(w.room==='upper'?1.6:1.2),w.room==='upper'?1.0:1.3]}); }
    for(const l of m.lamps) L.push({kind:'lantern',id:'lantern',room:'porch',level:st.lantern,p:l.p.slice(),pool:[l.p[0],l.p[1],2.3]});
    if(m.doorGlass) L.push({kind:'door',id:'door',room:'hall',level:st.rooms.hall,p:[e.x+n[0]*0.4,e.y+n[1]*0.4,e.z+1.5],pool:[e.x+n[0]*1.0,e.y+n[1]*1.0,0.9]});
    return L;
  }
  function lightRig(fr, opts, sky){
    const m=fr.mdl, st=lightsOn(opts, sky), set=lampSet(m, st), byWin={};
    for(const l of set) if(l.kind==='window') byWin[l.id]=l.level;
    const emit=(name)=> name==='lantern'?st.lantern : name==='door'?st.rooms.hall : name.startsWith('win:')?(byWin[name]||0) : 0;
    const lights=set.filter(l=>l.level>0.05).map(l=>({p:l.p, c:l.kind==='lantern'?'#ffd08a':'#ffc673', I:(l.kind==='lantern'?0.95:l.kind==='door'?0.4:0.42)*l.level, r:l.kind==='lantern'?3.4:1.9}));
    return {st, set, emit, lights};
  }
  function renderLive(dir, opts){ opts=opts||{}; const fr=frame(dir,opts), CPL=root.CoastalPass.light, sky=CPL.skyOf(opts), L=lightRig(fr,opts,sky);
    return CPL.relight(fr, sky, {emit:L.emit, lights:L.lights, outline:!!opts.outline}); }
  // relight a frame from any sky; o.occupancy / o.schedule as for render(), o.outline for the keyline A/B
  function relight(fr, sky, o){ o=o||{}; const CPL=root.CoastalPass.light; sky=sky||CPL.REF_SKY; const L=lightRig(fr, Object.assign({}, fr.opts, o), sky);
    return CPL.relight(fr, sky, Object.assign({emit:L.emit, lights:L.lights}, o)); }
  function castShadow(fr, sky, o){ return root.CoastalPass.light.castShadow(fr, sky||root.CoastalPass.light.REF_SKY, o); }
  function view(fr, ch, sky, o){ return (!ch||ch==='lit') ? relight(fr, sky, o) : root.CoastalPass.light.view(fr, ch, sky, o); }
  // the lights as the game places its glow sprites and light pools: screen px at this facing + model metres
  function lights(dir, opts){ opts=opts||{}; const m=liveModel(opts); if(!m) return null; const CPL=root.CoastalPass.light, sky=CPL.skyOf(opts), st=lightsOn(opts, sky), B=camBasis({dir,elev:opts.elev});
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return {x:+v.sx.toFixed(2),y:+v.sy.toFixed(2)}; };
    return { state:st, lamps:lampSet(m, st).map(l=>({kind:l.kind,id:l.id,room:l.room,level:l.level,m:l.p.map(v=>+v.toFixed(3)),screen:pj(l.p[0],l.p[1],l.p[2]),
      pool:{m:[+l.pool[0].toFixed(3),+l.pool[1].toFixed(3)],r:l.pool[2],screen:pj(l.pool[0],l.pool[1],0)}})) }; }

  root.HouseIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    SHAPES, SIDINGS, ROOFS, BODY, TRIM, ERAS, PRESETS, WINDOWS, KEY, ROOF_OPTIONS, PAINTS, CAST,
    render, anchors, project, entrance:entranceM,
    ENTRIES:['front','left','right'], SCHEDULES, CHAR:CHAR92, layout:(o)=>{ const b=resolve(o||{}), L=layoutOf(b); if(!b.mirror) return L; const d=L.door;
      return Object.assign({},L,{show:FLIPW[L.show], door:d?Object.assign({},d,{x:-d.x, facing:FLIPW[d.facing], plane:d.axis==='x'?-d.plane:d.plane, nrm:d.axis==='x'?-d.nrm:d.nrm}):null}); },
    placement, footprint:(o)=>{ const b=resolve(o||{}), R=footprint(b); return b.mirror?R.map(q=>Object.assign({},q,{x0:-q.x1,x1:-q.x0})):R; },
    dooryard:(o)=>{ const b=resolve(o||{}), Y=dooryardOf(b); if(!Y||!b.mirror) return Y; const w=Y.woodpile, fx=(p)=>Object.assign({},p,{x:-p.x});
      return {wall:FLIPW[Y.wall], woodpile:Object.assign({},w,{x0:-w.x1,x1:-w.x0}), barrel:fx(Y.barrel), pipe:fx(Y.pipe), backPipe:fx(Y.backPipe)}; },
    stations, lightsOn, lights, frame, relight, castShadow, view, renderLive };
})(typeof globalThis!=='undefined'?globalThis:window);
