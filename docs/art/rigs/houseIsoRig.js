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
    gothicRevival:  { era:'gothic',   body:'cream',       lower:'red', roof:'asphaltGrey', shape:'gable', siding:'twotone', size:0.8, porch:'front', dormers:0, chimneys:2, bay:false, attic:'gothic', crossGable:1, bargeboard:true, weather:0.15 },
    dormerCape:     { era:'seaside',  body:'greyShingle', roof:'asphaltGrey',  shape:'cape',    siding:'shingle',   size:0.5,  porch:'front', dormers:3, chimneys:1, bay:false, attic:'gable',  bargeboard:true, weather:0.45 },
  };
  const SHAPES = ['gable','ell','gambrel','saltbox','cape'];
  const SIDINGS = ['shingle','clapboard','twotone','fishscale'];
  const WINDOWS = ['sixOverSix','twoOverTwo','fourOverFour','twoOverOne','oneOverOne','arched'];
  const WINSTYLES = {
    sixOverSix:   { v:2, r:[0.25,0.5,0.75] },
    fourOverFour: { v:1, r:[0.25,0.5,0.75] },
    twoOverTwo:   { v:1, r:[0.5] },
    twoOverOne:   { v:1, r:[0.5], vTop:true },
    oneOverOne:   { v:0, r:[0.5] },
    arched:       { v:1, r:[0.5], arch:true },
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
      night:  !!opts.night,
      era: opts.era||'plain',
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
    const ct=0.1;
    put(c-dw/2-ct, c+dw/2+ct, z0, z0+dh+ct, 'trim', 0.55, 0.06);            // casing
    put(c-dw/2-ct-0.04, c+dw/2+ct+0.04, z0+dh+ct, z0+dh+ct+0.07, 'trim', 0.8, 0.05); // header cap
    put(c-dw/2, c+dw/2, z0, z0+dh, 'door', 0.15, 0.10);                     // slab
    put(c-dw/2+0.13, c+dw/2-0.13, z0+0.22, z0+dh*0.46, 'door', -0.7, 0.12); // lower panel (recessed)
    put(c-dw/2+0.13, c+dw/2-0.13, z0+dh*0.54, z0+dh-0.15, 'door', -0.7, 0.12); // upper panel
    put(c+dw/2-0.18, c+dw/2-0.11, z0+dh*0.48, z0+dh*0.48+0.07, 'trim', 0.95, 0.14); // knob
  }

  // small wooden stoop / steps under a ground-level door on a wall
  function stoop(out, axis, plane, nrm, c, fZ){
    const w=1.35, n=3, run=0.3;
    for(let s=0;s<n;s++){
      const z1=fZ*(s+1)/n, outer=(n-s)*run, a=plane, bnd=plane+nrm*outer;
      const lo=Math.min(a,bnd), hi=Math.max(a,bnd);
      if(axis==='y') boxSolid(out, c-w/2,c+w/2, lo,hi, 0, z1, 'wood', null, 0.04);
      else           boxSolid(out, lo,hi, c-w/2,c+w/2, 0, z1, 'wood', null, 0.04);
    }
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
    const hw=Wd/2, ov=0.3, brk=hw*0.5, midZ=eaveZ+(topZ-eaveZ)*0.55;
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
      out.push(F(pts,'body',0,0,null,null));
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
    return { hw, yA, yB, ov, midZ };
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
      out.push(F(pts,'body',0,0,null,null));
      // fill the wall step under the sloped top of gable end
      out.push(F(ny>0?[[-hw,yv,rearEave],[hw,yv,eaveZ],[hw,yv,fZ],[-hw,yv,fZ]]:[[hw,yv,eaveZ],[-hw,yv,rearEave],[-hw,yv,fZ],[hw,yv,fZ]],'body',0,0,
        ny>0?[[0,fZ],[2*hw,fZ],[2*hw,fZ],[0,fZ]]:[[0,fZ],[2*hw,fZ],[2*hw,fZ],[0,fZ]], siTex));
    }
    const yA=y0-ov,yB=y1+ov, rTex=opt.roofTex||null;
    quad(out, [-hw-ov,yA,rearEave],[-hw-ov,yB,rearEave],[xr,yB,ridgeZ],[xr,yA,ridgeZ],'roof',-0.05,rTex);
    quad(out, [hw+ov,yB,eaveZ],[hw+ov,yA,eaveZ],[xr,yA,ridgeZ],[xr,yB,ridgeZ],'roof',0.15,rTex);
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
    boxSolid(out, x-0.28,x+0.28, y-0.24,y+0.24, topZ-0.3, topZ+1.3, 'brick', null, 0);
    // cap
    slab(out, [[x-0.34,y-0.3],[x+0.34,y-0.3],[x+0.34,y+0.3],[x+0.34,y+0.3]], topZ+1.32, 'stone', 0.3);
    boxSolid(out, x-0.34,x+0.34, y-0.3,y+0.3, topZ+1.3, topZ+1.42, 'stone', null, 0.2);
  }

  // porch: deck + posts + railing + roof along +Y front (and +X side if wrap)
  function porch(out, b, roofTex, wrap){
    const hw=b.Wd/2, y1=b.Ln/2, deckZ=b.fH-0.05, depth=1.9, postTop=b.eaveZ-0.5;
    const roofT=roofTex||null;
    const runs=[];
    runs.push({ax:'y', plane:y1+depth, x0:-hw, x1:hw, front:true});
    if(wrap) runs.push({ax:'x', plane:hw+depth, y0:y1, y1:y1-b.Ln*0.6, side:true});
    // deck slab
    slab(out, [[-hw,y1],[hw,y1],[hw,y1+depth],[-hw,y1+depth]], deckZ, 'wood', 0.2);
    // deck fascia
    wall(out, -hw,y1+depth, hw,y1+depth, deckZ-0.35, deckZ, 'wood', null, -0.1);
    if(wrap){ slab(out, [[hw,y1-b.Ln*0.6],[hw+depth,y1-b.Ln*0.6],[hw+depth,y1],[hw,y1]], deckZ, 'wood', 0.2);
      wall(out, hw+depth,y1, hw+depth,y1-b.Ln*0.6, deckZ-0.35, deckZ, 'wood', null, 0.05); }
    // posts + rail (front)
    const nP=Math.max(2,Math.round(b.Wd/1.6));
    for(let i=0;i<=nP;i++){ const px=-hw+ (b.Wd)*(i/nP);
      boxSolid(out, px-0.06,px+0.06, y1+depth-0.1,y1+depth+0.02, deckZ, postTop, 'trim', null, 0.1); }
    wall(out, -hw,y1+depth-0.05, hw,y1+depth-0.05, deckZ+0.5, deckZ+0.62, 'trim', null, 0.2); // top rail
    // porch shed roof (wound so the top face catches the key light) + white fascia
    quad(out, [-hw-0.2,y1-0.1,b.eaveZ+0.05],[hw+0.2,y1-0.1,b.eaveZ+0.05],[hw+0.2,y1+depth+0.2,postTop-0.15],[-hw-0.2,y1+depth+0.2,postTop-0.15],'roof',0.3,roofT);
    wall(out, -hw-0.2,y1+depth+0.2, hw+0.2,y1+depth+0.2, postTop-0.3, postTop-0.12, 'trim', null, 0.4);
    if(wrap){
      const ys=y1-b.Ln*0.6;
      for(let i=0;i<=Math.round(b.Ln*0.6/1.6);i++){ const py=y1-(b.Ln*0.6)*(i/Math.round(b.Ln*0.6/1.6));
        boxSolid(out, hw+depth-0.1,hw+depth+0.02, py-0.06,py+0.06, deckZ, postTop, 'trim', null, 0.1); }
      wall(out, hw+depth-0.05,y1, hw+depth-0.05,ys, deckZ+0.5, deckZ+0.62, 'trim', null, 0.2);
      quad(out, [hw-0.1,y1+0.2,b.eaveZ+0.05],[hw-0.1,ys-0.2,b.eaveZ+0.05],[hw+depth+0.2,ys-0.2,postTop-0.15],[hw+depth+0.2,y1+0.2,postTop-0.15],'roof',0.2,roofT);
      wall(out, hw+depth+0.2,y1+0.2, hw+depth+0.2,ys-0.2, postTop-0.3, postTop-0.12, 'trim', null, 0.4);
    }
    // steps (front centre)
    for(let s=0;s<3;s++){ const sz=deckZ-0.12*(s+1), sy=y1+depth+0.12*s;
      boxSolid(out, -0.7,0.7, sy,sy+0.14, sz-0.12, sz, 'wood', null, 0.05); }
  }

  // bargeboard: white sawtooth along the +Y gable rake (gingerbread)
  function bargeboard(out, b){
    const hw=b.Wd/2, yv=b.Ln/2+0.34, xr=0, n=8;
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
  }

  function foundation(out, b, xhw, y0, y1){
    boxSolid(out, -xhw,xhw, y0,y1, 0, b.fH, 'stone', null, -0.1);
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

  function build(b){
    const out=[];
    const siTex = sidingTex(b.siding);
    const roofTex = b.roof==='metal'
      ? (u,v)=>{ const seam=0.42; return (((u%seam)+seam)%seam)<0.05? -2 : 0; }
      : (u,v)=>{ const CO=0.34; const f=((v%CO)+CO)%CO; return f<0.05?-2:(f>CO-0.05?1:0); };
    const hw=b.Wd/2, y0=-b.Ln/2, y1=b.Ln/2;
    const twotone = b.siding==='twotone';
    const bandZ = b.fH + (b.wallH)*0.52;   // beltcourse height for twotone

    // FOUNDATION
    foundation(out, b, hw+0.05, y0, y1);

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

    // ELL forward wing: a lower cross-gable projecting off the +Y front (gable faces +Y)
    if(b.shape==='ell'){
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
      // entry door + stoop on the wing front; attic window in the gable; a side window
      doorOn(out,'y', wy1,1, wcx, b.fH, 0.95, 2.05); stoop(out,'y', wy1,1, wcx, b.fH);
      if(b.attic!=='none') windowOn(out,'y', wy1,1, wcx, wingEave-0.5, 0.62,0.72, {windows:b.windows});
      windowOn(out,'x', wx1,1, wmy, b.fH+1.0, 0.7,1.1, {windows:b.windows});
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
    const ncg = Math.min(3, b.crossGable|0);
    const cgYs=[]; if(ncg===1) cgYs.push(0); else for(let i=0;i<ncg;i++) cgYs.push(-b.Ln*0.3 + b.Ln*0.6*(i/Math.max(1,ncg-1)));
    const cgDoorIdx = (eaveDoor && ncg%2===1) ? (ncg-1)/2 : -1;
    cgYs.forEach((cy,i)=> crossGable(out, b, cy, siTex, roofTex, i===cgDoorIdx));
    const inCG=(c)=> cgYs.some(cy=>Math.abs(c-cy)<1.6);
    const wrapSide = (b.porch==='wrap') && ncg===0 && !bayFrontOn && !isCape;
    const garageOn = (b.garage==='single'||b.garage==='double') && b.shape!=='ell';
    const gW = b.garage==='double'?5.6:3.2, gx1=-hw+0.15, gx0=gx1-gW, gyf=b.Ln/2+0.2, gyb=gyf-5.4;
    if(garageOn) garage(out, b, siTex, roofTex, b.garage, gx0,gx1,gyb,gyf);
    for(const [xv,nx] of [[-hw,-1],[hw,1]]){
      for(let i=0;i<nLong;i++){ const c=y0+ b.Ln*((i+0.5)/nLong);
        if(nx>0 && inCG(c)) continue;                                          // wall hidden behind a cross-gable
        if(nx>0 && eaveDoor && cgDoorIdx<0 && Math.abs(c)<1.1) continue;       // leave room for the door
        if(nx<0 && garageOn && c>gyb-0.3 && c<gyf) continue;                   // wall hidden behind the garage
        const underWrap = nx>0 && wrapSide && c > (b.Ln/2 - b.Ln*0.6);         // covered by the wrap porch
        if(!underWrap) windowOn(out,'x', xv,nx, c, sillG, ww,wh, b);
        if(b.wallH>4.4) windowOn(out,'x', xv,nx, c, sillU, ww,wh*0.92, b);   // 2nd storey
      }
    }
    if(eaveDoor && cgDoorIdx<0){ doorOn(out,'x', hw,1, 0, b.fH, 1.0, 2.1); stoop(out,'x', hw,1, 0, b.fH); }
    // gable ends: door on +Y under the porch; else windows flanking
    const gableDoor = hasPorch && b.shape!=='ell';
    for(const [yv,ny] of [[y0,-1],[y1,1]]){
      if(ny>0 && gableDoor){
        doorOn(out,'y', yv,ny, 0, b.fH, 1.0, 2.1);
        windowOn(out,'y', yv,ny, -hw*0.55, sillG, ww,wh, b);
        windowOn(out,'y', yv,ny,  hw*0.55, sillG, ww,wh, b);
      } else {
        const frontGround = ny>0;
        const skipL = frontGround && ((bayFrontOn && hw*0.42<1.7) || b.shape==='ell');
        const skipR = frontGround && (bayFrontOn && hw*0.42<1.7);
        if(!skipL) windowOn(out,'y', yv,ny, -hw*0.42, sillG, ww,wh, b);
        if(!skipR) windowOn(out,'y', yv,ny,  hw*0.42, sillG, ww,wh, b);
      }
      if(b.wallH>4.4){ windowOn(out,'y', yv,ny, -hw*0.42, sillU, ww,wh*0.9, b);
                       windowOn(out,'y', yv,ny,  hw*0.42, sillU, ww,wh*0.9, b); }
      // attic window in the peak
      if(b.attic!=='none'){
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
    if(bayFrontOn) bayFront(out, b, siTex, bayKind);

    // DORMERS on the +X slope
    const nd=Math.min(3,b.dormers|0);
    for(let i=0;i<nd;i++){
      const dy = y0 + b.Ln*((i+0.5)/Math.max(1,nd));
      dormer(out, hw, b.eaveZ, b.ridgeZ, dy, {windows:b.windows}, siTex, roofTex);
    }

    // CHIMNEYS (near ridge, on gable sides)
    const nc=Math.min(2,b.chimneys|0);
    if(nc>=1) chimney(out, 0.0, y0+b.Ln*0.22, b.ridgeZ);
    if(nc>=2) chimney(out, 0.0, y1-b.Ln*0.18, b.ridgeZ);

    // PORCH (suppressed when a front bay or cape long-face entry occupies the front)
    if(!bayFrontOn && !isCape){
      if(b.porch==='front') porch(out, b, roofTex, false);
      if(b.porch==='wrap')  porch(out, b, roofTex, ncg===0);   // drop the +X wrap side when cross-gables occupy it
    }

    // BARGEBOARD gingerbread
    if(b.bargeboard) bargeboard(out, b);

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
    const b=resolve(opts);
    const MATS=makeMats(b);
    const faces=build(b);
    const bufs=paint(faces, {dir, elev:opts.elev}, MATS);
    return toRGBA(post(bufs, b));
  }
  function anchors(dir, opts){
    opts=opts||{}; const b=resolve(opts), B=camBasis({dir,elev:opts.elev});
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return {x:v.sx,y:v.sy}; };
    const nc=Math.min(2,b.chimneys|0), ch=[];
    if(nc>=1) ch.push(pj(0.0, -b.Ln/2+b.Ln*0.22, b.ridgeZ+1.45));
    if(nc>=2) ch.push(pj(0.0,  b.Ln/2-b.Ln*0.18, b.ridgeZ+1.45));
    return { chimneys:ch, door:pj(0,b.Ln/2,b.fH+1.0), ridge:pj(0,0,b.ridgeZ), Wd:b.Wd, Ln:b.Ln };
  }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.HouseIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    SHAPES, SIDINGS, ROOFS, BODY, TRIM, ERAS, PRESETS, WINDOWS, KEY,
    render, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
