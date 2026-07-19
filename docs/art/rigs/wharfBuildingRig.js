/* Hidden Harbours — parametric ISO WHARF-BUILDING rig (ADR-0006 bake pipeline, SAME turntable +
   camera + shading as houseIsoRig.js / the fleet / characterIsoRig.js). One parametric 3D building,
   built from walls / roof planes / gable prisms / decals, baked to pixel sheets through the SHARED
   3/4 camera: 45deg steps, elev 40deg default, flat-facet shading from the fixed upper-LEFT key,
   z-buffered, ordered dither, per-face uv texture (siding), depth-edge darkening, 1px keyline, NO AA.
   32 px = 1 m. All 8 facings fall out of one model. Buildings sit true on the Wharf tile kit.

   THIS RIG IS THE WHARF-BUILDINGS SIBLING OF houseIsoRig.js — the vernacular net-shed / storage barn /
   fish-processing plant family, PLUS industrial cladding (corrugated / board-&-batten / cinderblock)
   the house rig doesn't carry. Same face/paint/post code; different massing, materials + fittings.

   THE BUILDER SURFACE (every axis resolved per render, no re-modelling):
     type:   'shack'|'storage'|'processing'   — net shed / storage barn / fish plant; seeds every axis
     shape:  'gable'|'gambrel'|'shed'          — massing / roofline
     size:   0..1   small -> large (per-type range)
     siding: 'shingle'|'clapboard'|'boardBatten'|'corrugated'   (wall texture; corrugated reads raw
             galvanised when body='galv'/'rustMetal', painted otherwise)
     base:   'none'|'block'                    — cinderblock wainscot on the lower wall
     body:   greyShingle|white|cream|red|sage|blue|rustOrange|mustard|teal|galv|rustMetal | custom ramp
     roof:   'asphaltGrey'|'asphaltBrown'|'metalSeam'|'corrugated'|'rusted'
     door:   'doubleBarn'|'slidingBarn'|'plank'|'rollUp'|'personnel'   (main door on the +Y gable)
     windows:'twoOverTwo'|'sixOverSix'|'oneOverOne'|'industrial'   winDensity:0..1
     cupola: 'none'|'cupola'|'monitor'   (barn vent cupola / long roof monitor clerestory)
     dock:bool (raised loading dock + roll-up bays on +X)   hvac:bool   stacks:0..3   vents:bool
     sign:bool (blank gable sign board — letter it separately)   boom:bool (roof hoist boom/davit)
     weather:0..1 (paint fade + shingle greying + roof moss/rust + patchy)   night:bool (warm-lit)
   ANIM: buildings are static; exhaust-stack smoke + lit windows are runtime overlays — anchors(dir,opts)
   -> { stacks:[{x,y}], door:{x,y}, ridge:{x,y}, Wd, Ln } in cell px for the smoke / glow / label layers.
   Exposes globalThis.WharfBuilding = { W,H,PX,DIRS,pivot,order,defaultElev, TYPES,SHAPES,SIDINGS,ROOFS,
   BODY,TRIM,DOORS,WINDOWS,CUPOLAS,PRESETS, render(dir,opts), anchors(dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1200, H = 1160, cx = 600, groundY = 780;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;

  // ---- palettes, dark -> light (KTC master ramps, shared with the fleet / house / wharf kit) ----
  const BODY = {
    greyShingle: ['#4c463f','#5d564c','#6f665a','#82786a','#968b7b','#a99d8c'],
    white:       ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    cream:       ['#8a6f3c','#a6884b','#c2a35f','#d8bd7c','#e9d59d','#f5e7c1'],
    red:         ['#4a130f','#671b14','#88271c','#a33124','#bd4230','#d25a42'],
    sage:        ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'],
    blue:        ['#33454a','#43585d','#556d72','#6a848a','#849ea3','#a3b9bd'],
    rustOrange:  ['#5c2a10','#78380f','#95491a','#b05c27','#c67338','#d98d4f'],
    mustard:     ['#5e4a12','#7c6119','#987a26','#b39440','#c8ab5e','#dbc182'],
    teal:        ['#143a38','#1f4d4a','#2c625e','#3b7872','#4d8f88','#66a69d'],
    galv:        ['#464d51','#5a6267','#727c81','#8c979c','#a6b1b5','#c2cccf'],
    rustMetal:   ['#3a1c10','#552a17','#6e3a22','#8a4e2f','#a5643f','#bd7d52'],
  };
  const TRIM = ['#9aa09a','#b4b8b0','#ccd0c7','#e0e2da','#eef0e8','#f8f9f2'];
  const ROOFS = {
    asphaltGrey:  ['#23262b','#2e333a','#3c424a','#4c535c','#5d6570','#6f7883'],
    asphaltBrown: ['#2a211a','#3a2e23','#4c3d2e','#5f4d3a','#736046','#877254'],
    metal:        ['#424d52','#556065','#6c7c81','#88999e','#a4babe','#c0d4d7'],
  };
  const GALV   = BODY.galv;
  const RUST   = ['#3a1c10','#4f2614','#6a3620','#84462b','#9c5b3a','#b2724c'];
  const CINDER = ['#4a4842','#5f5d55','#77746a','#8f8b7f','#a4a094','#b8b3a6'];
  const STEEL  = ['#2a2f33','#3c454b','#525c63','#6d777e','#889298','#a2acb1'];
  const STONE  = ['#33343a','#42444b','#54575d','#666a70','#7a7e84'];
  const WOOD   = ['#4f3a24','#63492d','#785a39','#8f7049','#a6875d','#bd9f74'];
  const DOORC  = ['#20343a','#2c464d','#3a5c64','#4a747d','#5c8f99'];
  const SIGN   = ['#7d7566','#8f8878','#a89e88','#c0b69e','#d4cbb4','#e4dcc6'];
  const GLASSD = ['#33474d','#40585f','#547078'];
  const GLASS_HI = '#cfe6e8';
  const GLASSN = ['#7a4f18','#b98a2f','#eed07a'];
  const KEY = '#1a1c22';

  // per-type defaults (seed massing + every axis; opts override). ranges: [base, size*k]
  const TYPES = {
    shack: {
      shape:'gable', pitch:1.3, siding:'shingle', body:'greyShingle', door:'doubleBarn',
      windows:'twoOverTwo', base:'none', cupola:'none', roof:'asphaltGrey',
      Wd:[3.6,1.4], Ln:[4.5,3.0], wallH:[2.9,1.1], fH:0.4,
      loft:'window', dock:false, hvac:false, stacks:0, vents:false, sign:false, boom:false, winD:0.35,
    },
    storage: {
      shape:'gambrel', pitch:1.0, siding:'boardBatten', body:'red', door:'slidingBarn',
      windows:'twoOverTwo', base:'none', cupola:'cupola', roof:'asphaltGrey',
      Wd:[5.2,2.2], Ln:[6.5,4.5], wallH:[3.6,1.6], fH:0.45,
      loft:'door', dock:false, hvac:false, stacks:0, vents:true, sign:false, boom:false, winD:0.3,
    },
    processing: {
      shape:'gable', pitch:0.72, siding:'corrugated', body:'galv', door:'rollUp',
      windows:'industrial', base:'block', cupola:'monitor', roof:'corrugated',
      Wd:[7.2,2.6], Ln:[10,6], wallH:[4.0,1.6], fH:0.6,
      loft:'none', dock:true, hvac:true, stacks:2, vents:true, sign:true, boom:true, winD:0.5,
    },
  };
  const PRESETS = {
    netShed:     { type:'shack',      body:'greyShingle', siding:'shingle',    roof:'asphaltGrey',  door:'doubleBarn', size:0.2,  weather:0.6  },
    redShed:     { type:'shack',      body:'red',         siding:'boardBatten', roof:'asphaltBrown', door:'plank',      size:0.35, weather:0.4  },
    tealShack:   { type:'shack',      body:'teal',        siding:'clapboard',  roof:'metalSeam',    door:'slidingBarn', size:0.3,  weather:0.45 },
    gambrelBarn: { type:'storage',    body:'blue',        siding:'boardBatten', roof:'asphaltGrey',  door:'slidingBarn', cupola:'cupola', size:0.6,  weather:0.35 },
    iceHouse:    { type:'storage',    body:'white',       siding:'clapboard',  roof:'metalSeam',    door:'doubleBarn', size:0.5,  weather:0.25 },
    fishPlant:   { type:'processing', body:'galv',        siding:'corrugated', roof:'corrugated',   size:0.7,  weather:0.5  },
    cannery:     { type:'processing', body:'rustMetal',   siding:'corrugated', roof:'rusted',       size:0.9,  weather:0.72, boom:true },
  };
  const SHAPES = ['gable','gambrel','shed'];
  const SIDINGS = ['shingle','clapboard','boardBatten','corrugated'];
  const DOORS = ['doubleBarn','slidingBarn','plank','rollUp','personnel'];
  const ROOF_KEYS = ['asphaltGrey','asphaltBrown','metalSeam','corrugated','rusted'];
  const CUPOLAS = ['none','cupola','monitor'];
  const WINDOWS = ['twoOverTwo','sixOverSix','oneOverOne','industrial'];
  const WINSTYLES = {
    sixOverSix:   { v:2, r:[0.25,0.5,0.75] },
    twoOverTwo:   { v:1, r:[0.5] },
    oneOverOne:   { v:0, r:[0.5] },
    industrial:   { v:2, r:[0.2,0.4,0.6,0.8] },
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

  // ---- camera / projection (identical to houseIsoRig, so wharf buildings composite with the fleet) ----
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

  // ---- face builders (F = {v, mat, b, db, uv, tex, flat}) ---------------------
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }
  function wall(out, x0,y0,x1,y1, z0,z1, mat, tex, b){
    const L=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]], mat, b||0, 0,
      [[0,z0],[L,z0],[L,z1],[0,z1]], tex));
  }
  function slab(out, pts, z, mat, b){ out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0)); }
  function quad(out, p0,p1,p2,p3, mat, b, tex){
    const u=Math.hypot(p1[0]-p0[0],p1[1]-p0[1],p1[2]-p0[2]);
    const v=Math.hypot(p3[0]-p0[0],p3[1]-p0[1],p3[2]-p0[2]);
    out.push(F([p0,p1,p2,p3], mat, b||0, 0, tex?[[0,0],[u,0],[u,v],[0,v]]:null, tex||null));
  }
  function tri(out, p0,p1,p2, mat, b, uv, tex){ out.push(F([p0,p1,p2], mat, b||0, 0, uv||null, tex||null)); }
  function boxSolid(out, x0,x1, y0,y1, z0,z1, mat, tex, b){
    wall(out, x0,y0, x1,y0, z0,z1, mat, tex, b);
    wall(out, x1,y1, x0,y1, z0,z1, mat, tex, b);
    wall(out, x1,y0, x1,y1, z0,z1, mat, tex, b);
    wall(out, x0,y1, x0,y0, z0,z1, mat, tex, b);
    slab(out, [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, (b||0)+0.25);
  }

  // ---- siding textures: return an integer ramp delta (negative = darker seam) -------------
  function sidingTex(kind){
    if(kind==='clapboard'){
      const LAP=0.30;
      return (u,v)=>{ const f=((v% LAP)+LAP)%LAP; return f < 0.055 ? -2 : (f>LAP-0.04? 1 : 0); };
    }
    if(kind==='shingle'){
      const CO=0.34, SW=0.24;
      return (u,v)=>{ const row=Math.floor(v/CO); const f=((v%CO)+CO)%CO;
        const off=(row&1)*0.5*SW; const su=(((u+off)%SW)+SW)%SW;
        if(f < 0.05) return -2;
        if(su < 0.035) return -1;
        if(f > CO-0.05) return 1;
        return 0; };
    }
    if(kind==='boardBatten'){
      const BAT=0.30, bw=0.075;
      return (u,v)=>{ const f=((u%BAT)+BAT)%BAT;
        if(f < bw) return 1;                 // raised batten (lit)
        if(f < bw+0.035) return -1;          // shadow beside batten
        if(f > BAT-0.035) return -2;         // recess gap before next batten
        return 0; };
    }
    if(kind==='corrugated'){
      const R=0.19;
      return (u,v)=>{ const f=(((u%R)+R)%R)/R;   // 0..1 across one rib
        if(f < 0.10) return -2;
        if(f < 0.24) return -1;
        if(f < 0.46) return 0;
        if(f < 0.62) return 1;
        if(f < 0.80) return 0;
        return -1; };
    }
    return null;
  }
  function blockTex(){
    const CO=0.22, BL=0.44;
    return (u,v)=>{ const row=Math.floor(v/CO); const off=(row&1)*0.5*BL;
      const fv=((v%CO)+CO)%CO; const su=(((u+off)%BL)+BL)%BL;
      if(fv < 0.035) return -2;          // horizontal mortar
      if(su < 0.04) return -1;           // vertical joint
      if(fv > CO-0.04) return 1;         // block lower catch
      return 0; };
  }
  function roofTexFor(roof){
    if(roof==='metalSeam') return (u,v)=>{ const s=0.42; return (((u%s)+s)%s)<0.05? -2 : 0; };
    if(roof==='corrugated' || roof==='rusted'){ const R=0.24;
      return (u,v)=>{ const f=(((u%R)+R)%R)/R;
        if(f < 0.12) return -2; if(f < 0.30) return -1; if(f < 0.55) return 0; if(f < 0.75) return 1; return -1; }; }
    return (u,v)=>{ const CO=0.34; const f=((v%CO)+CO)%CO; return f<0.05?-2:(f>CO-0.05?1:0); };  // asphalt courses
  }

  // ---- rasterizer (fleet recipe + uv interpolation + per-face tex) ----------
  function paint(faces, opts, MATS){
    const B=camBasis(opts);
    const N=W*H;
    const zbuf=new Float32Array(N).fill(Infinity);
    const dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null);
    const ibuf=new Int16Array(N);
    const nbuf=new Array(N).fill(null);
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

  // ---- geometry resolve -----------------------------------------------------
  function resolve(opts){
    opts = opts||{};
    const T = TYPES[opts.type] || TYPES.shack;
    const g = (k,d)=> opts[k]!=null ? opts[k] : (T[k]!=null ? T[k] : d);
    const size = opts.size!=null ? opts.size : 0.4;
    const b = {
      type:   opts.type || 'shack',
      shape:  g('shape','gable'),
      size,
      siding: g('siding','shingle'),
      base:   g('base','none'),
      body:   opts.body || T.body || 'greyShingle',
      roof:   g('roof','asphaltGrey'),
      door:   g('door','doubleBarn'),
      windows:g('windows','twoOverTwo'),
      winD:   opts.winDensity!=null ? opts.winDensity : T.winD,
      cupola: g('cupola','none'),
      pitch:  g('pitch',1.0),
      loft:   g('loft','none'),
      dock:   g('dock',false),
      hvac:   g('hvac',false),
      stacks: opts.stacks!=null ? opts.stacks : T.stacks,
      vents:  g('vents',false),
      sign:   g('sign',false),
      boom:   g('boom',false),
      weather:opts.weather!=null ? opts.weather : 0.55,
      night:  !!opts.night,
    };
    b.Wd = T.Wd[0] + size*T.Wd[1];
    b.Ln = T.Ln[0] + size*T.Ln[1];
    b.fH = T.fH;
    b.wallH = T.wallH[0] + size*T.wallH[1];
    b.eaveZ = b.fH + b.wallH;
    b.rise  = (b.Wd/2) * b.pitch;
    b.ridgeZ = b.eaveZ + b.rise;
    b.ov = 0.32;
    return b;
  }

  function makeMats(b){
    const wx=b.weather, night=b.night;
    const wthBody=(ramp)=>ramp.map(c=>{ let x=desat(c, wx*0.55); x=mix(x,'#6f6a5f',wx*0.28); if(night)x=mix(x,'#1b2733',0.42); return x; });
    const wthMetal=(ramp)=>ramp.map(c=>{ let x=mix(c,'#6b6156',wx*0.22); if(night)x=mix(x,'#1b2530',0.42); return x; });
    const wthRoof=(ramp)=>ramp.map(c=>{ let x=mix(c,'#5f6a52',wx*0.18); if(night)x=mix(x,'#141d27',0.45); return x; });
    const wthWood=(ramp)=>ramp.map(c=>{ let x=mix(c,'#8a8172',wx*0.4); if(night)x=mix(x,'#1b2230',0.4); return x; });
    const trimR = TRIM.map(c=>{ let x=desat(c,wx*0.3); if(night)x=mix(x,'#24303c',0.4); return x; });
    const bodyRamp = Array.isArray(b.body)?b.body:(BODY[b.body]||BODY.greyShingle);
    const isMetalBody = b.body==='galv'||b.body==='rustMetal';
    const glass = night ? GLASSN : GLASSD;
    const ROOFRAMP = { asphaltGrey:ROOFS.asphaltGrey, asphaltBrown:ROOFS.asphaltBrown, metalSeam:ROOFS.metal, corrugated:GALV, rusted:RUST };
    return {
      body:  { ramp: (isMetalBody?wthMetal:wthBody)(bodyRamp) },
      trim:  { ramp: trimR },
      roof:  { ramp: wthRoof(ROOFRAMP[b.roof]||ROOFS.asphaltGrey) },
      stone: { ramp: wthBody(STONE) },
      cinder:{ ramp: wthBody(CINDER) },
      steel: { ramp: wthMetal(STEEL) },
      galv:  { ramp: wthMetal(GALV) },
      rust:  { ramp: wthMetal(RUST) },
      wood:  { ramp: wthWood(WOOD) },
      sign:  { ramp: wthBody(SIGN) },
      door:  { ramp: night?wthRoof(DOORC):DOORC },
      glass: { ramp: glass, off: night?1:0 },
      glassHi:{ ramp:[ night?'#ffe6a6':GLASS_HI ] },
      dark:  { ramp:[KEY] },
    };
  }

  // ---- decals on a wall plane ----------------------------------------------
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
  const putOn=(axis)=>(out,plane,nrm,a0,a1,z0,z1,mat,bias,db)=> axis==='y'
      ? decalY(out,plane,nrm,a0,a1,z0,z1,mat,bias,null,true,db)
      : decalX(out,plane,nrm,a0,a1,z0,z1,mat,bias,null,true,db);

  // framed double-hung / multilite window (crisp flat quads)
  function windowOn(out, axis, plane, nrm, c, z, ww, wh, b){
    const put=putOn(axis);
    const st = WINSTYLES[b.windows] || WINSTYLES.twoOverTwo;
    const ct=0.09, topZ=z+wh;
    put(out,plane,nrm, c-ww/2-ct-0.05, c+ww/2+ct+0.05, z-0.13, z-0.03, 'trim', 0.9, 0.05);  // sill
    put(out,plane,nrm, c-ww/2-ct, c+ww/2+ct, z-0.03, topZ+ct, 'trim', 0.45, 0.06);           // casing
    put(out,plane,nrm, c-ww/2-ct-0.04, c+ww/2+ct+0.04, topZ+ct, topZ+ct+0.07, 'trim', 0.8, 0.05); // header
    put(out,plane,nrm, c-ww/2, c+ww/2, z, topZ, 'glass', 0.0, 0.10);                          // glass
    put(out,plane,nrm, c-ww/2+0.02, c-ww/2+ww*0.34, z+wh*0.54, topZ-0.05, 'glassHi', 0.0, 0.12);
    const mb=0.055;
    if(st.v>0){ const cols=st.v+1; for(let i=1;i<=st.v;i++){ const cc=c-ww/2+ww*(i/cols);
      put(out,plane,nrm, cc-mb/2, cc+mb/2, z, topZ, 'trim', 0.6, 0.14); } }
    for(const r of st.r){ const rz=z+wh*r; put(out,plane,nrm, c-ww/2, c+ww/2, rz-mb/2, rz+mb/2, 'trim', 0.6, 0.14); }
  }

  function doorOn(out, axis, plane, nrm, c, z0, dw, dh){
    const put=putOn(axis), ct=0.1;
    put(out,plane,nrm, c-dw/2-ct, c+dw/2+ct, z0, z0+dh+ct, 'trim', 0.55, 0.06);
    put(out,plane,nrm, c-dw/2-ct-0.04, c+dw/2+ct+0.04, z0+dh+ct, z0+dh+ct+0.07, 'trim', 0.8, 0.05);
    put(out,plane,nrm, c-dw/2, c+dw/2, z0, z0+dh, 'door', 0.15, 0.10);
    put(out,plane,nrm, c-dw/2+0.13, c+dw/2-0.13, z0+0.22, z0+dh*0.46, 'door', -0.7, 0.12);
    put(out,plane,nrm, c-dw/2+0.13, c+dw/2-0.13, z0+dh*0.54, z0+dh-0.15, 'door', -0.7, 0.12);
    put(out,plane,nrm, c+dw/2-0.18, c+dw/2-0.11, z0+dh*0.48, z0+dh*0.48+0.07, 'trim', 0.95, 0.14);
  }

  // stepped diagonal brace (Z-brace) laid as small squares along a wall plane
  function diagBrace(out, axis, plane, nrm, a0,z0, a1,z1, wdt, mat, bias){
    const n=8; for(let i=0;i<n;i++){ const t=i/(n-1); const a=a0+(a1-a0)*t, z=z0+(z1-z0)*t;
      putOn(axis)(out,plane,nrm, a-wdt/2,a+wdt/2, z-wdt/2,z+wdt/2, mat, bias, 0.15); } }

  // big hinged double doors (white plank, Z-braced) — the classic net-shed door
  function barnDoors(out, axis, plane, nrm, c, z0, dw, dh){
    const put=putOn(axis), ct=0.1, topZ=z0+dh, gap=0.045;
    put(out,plane,nrm, c-dw/2-ct-0.06, c+dw/2+ct+0.06, topZ+ct, topZ+ct+0.13, 'wood', 0.5, 0.05); // header beam
    put(out,plane,nrm, c-dw/2-ct, c+dw/2+ct, z0, topZ+ct, 'trim', 0.5, 0.06);                     // casing
    for(const s of [-1,1]){
      const lx0 = s<0 ? c-dw/2 : c+gap, lx1 = s<0 ? c-gap : c+dw/2;
      put(out,plane,nrm, lx0, lx1, z0, topZ, 'trim', 0.05, 0.10);                                 // leaf
      const planks=3; for(let i=1;i<planks;i++){ const px=lx0+(lx1-lx0)*(i/planks);
        put(out,plane,nrm, px-0.018,px+0.018, z0+0.05, topZ-0.05,'trim',-1.5,0.12); }             // plank seams
      put(out,plane,nrm, lx0+0.05,lx1-0.05, z0+0.12, z0+0.26,'trim',-1.1,0.12);                   // low ledger
      put(out,plane,nrm, lx0+0.05,lx1-0.05, topZ-0.3, topZ-0.16,'trim',-1.1,0.12);                // high ledger
      diagBrace(out,axis,plane,nrm, lx0+0.1,z0+0.24, lx1-0.1,topZ-0.28, 0.09,'trim',-0.9);        // diagonal
      const hx = s<0 ? lx0+0.06 : lx1-0.06;
      put(out,plane,nrm, hx-0.03,hx+0.03, z0+0.18, z0+0.42,'door',-2,0.13);                       // strap hinge lo
      put(out,plane,nrm, hx-0.03,hx+0.03, topZ-0.55, topZ-0.31,'door',-2,0.13);                   // strap hinge hi
    }
    put(out,plane,nrm, c-0.02,c+0.02, z0+dh*0.42, z0+dh*0.42+0.34,'door',-2,0.13);                // centre handle
  }

  // sliding barn door on an overhead rail (single leaf, offset to one side)
  function slidingDoor(out, axis, plane, nrm, c, z0, dw, dh, b){
    const put=putOn(axis), topZ=z0+dh, off=dw*0.14;
    put(out,plane,nrm, c-dw/2-off-0.12, c+dw/2, topZ+0.06, topZ+0.16,'steel',0.7,0.05);           // rail/track
    const lx0=c-dw/2-off, lx1=c+dw/2-off;                                                          // leaf slid to one side
    for(const hx of [lx0+dw*0.24, lx1-dw*0.24]) put(out,plane,nrm, hx-0.05,hx+0.05, topZ-0.02, topZ+0.13,'steel',0.3,0.06); // hangers
    put(out,plane,nrm, lx0-0.06, lx1+0.06, z0, topZ, 'trim', 0.3, 0.10);                           // frame
    put(out,plane,nrm, lx0, lx1, z0+0.03, topZ-0.03, 'trim', 0.05, 0.11);                          // leaf
    const planks=5; for(let i=1;i<planks;i++){ const px=lx0+(lx1-lx0)*(i/planks);
      put(out,plane,nrm, px-0.016,px+0.016, z0+0.06, topZ-0.06,'trim',-1.4,0.12); }
    put(out,plane,nrm, lx0+0.05,lx1-0.05, z0+0.15, z0+0.28,'trim',-1.0,0.12);
    put(out,plane,nrm, lx0+0.05,lx1-0.05, topZ-0.34, topZ-0.21,'trim',-1.0,0.12);
    diagBrace(out,axis,plane,nrm, lx0+0.1,z0+0.26, lx1-0.1,topZ-0.32, 0.085,'trim',-0.85);
    put(out,plane,nrm, lx1-0.16,lx1-0.05, z0+dh*0.46, z0+dh*0.46+0.3,'door',-2,0.13);              // handle
  }

  // tall single vertical-plank door
  function plankDoor(out, axis, plane, nrm, c, z0, dw, dh){
    const put=putOn(axis), ct=0.09, topZ=z0+dh;
    put(out,plane,nrm, c-dw/2-ct, c+dw/2+ct, z0, topZ+ct, 'trim', 0.5, 0.06);
    put(out,plane,nrm, c-dw/2-ct-0.04, c+dw/2+ct+0.04, topZ+ct, topZ+ct+0.06, 'trim', 0.8, 0.05);
    put(out,plane,nrm, c-dw/2, c+dw/2, z0, topZ, 'wood', 0.1, 0.10);
    const planks=3; for(let i=1;i<planks;i++){ const px=c-dw/2+dw*(i/planks);
      put(out,plane,nrm, px-0.016,px+0.016, z0+0.04, topZ-0.04,'wood',-1.6,0.12); }
    put(out,plane,nrm, c-dw/2+0.04,c+dw/2-0.04, z0+0.3, z0+0.42,'wood',-1.0,0.12);
    put(out,plane,nrm, c-dw/2+0.04,c+dw/2-0.04, topZ-0.5, topZ-0.38,'wood',-1.0,0.12);
    put(out,plane,nrm, c+dw/2-0.16,c+dw/2-0.09, z0+dh*0.48, z0+dh*0.48+0.16,'steel',0.4,0.13);     // latch
  }

  // roll-up steel dock door (horizontal slats, side guides, bottom bar)
  function rollUpDoor(out, axis, plane, nrm, c, z0, dw, dh){
    const put=putOn(axis), topZ=z0+dh;
    put(out,plane,nrm, c-dw/2-0.12, c+dw/2+0.12, z0, topZ+0.16, 'steel', 0.35, 0.05);              // frame
    put(out,plane,nrm, c-dw/2-0.12, c-dw/2-0.04, z0, topZ+0.16, 'steel', -1.0, 0.11);              // L guide
    put(out,plane,nrm, c+dw/2+0.04, c+dw/2+0.12, z0, topZ+0.16, 'steel', -1.0, 0.11);              // R guide
    put(out,plane,nrm, c-dw/2, c+dw/2, z0, topZ, 'galv', -0.1, 0.10);                              // curtain
    const rib=0.28; for(let z=z0+rib; z<topZ-0.04; z+=rib) put(out,plane,nrm, c-dw/2,c+dw/2, z-0.03,z+0.03,'galv',-1.6,0.12); // slat lines
    put(out,plane,nrm, c-dw/2, c+dw/2, z0, z0+0.16, 'steel', 0.4, 0.11);                           // bottom bar
    put(out,plane,nrm, c-0.16,c+0.16, z0+0.5, z0+0.62,'steel',0.6,0.13);                           // lift handle
  }

  function placeDoor(out, kind, axis, plane, nrm, c, z0, dw, dh, b){
    if(kind==='doubleBarn') barnDoors(out,axis,plane,nrm,c,z0,dw,dh);
    else if(kind==='slidingBarn') slidingDoor(out,axis,plane,nrm,c,z0,dw,dh,b);
    else if(kind==='plank') plankDoor(out,axis,plane,nrm,c,z0,dw,dh);
    else if(kind==='rollUp') rollUpDoor(out,axis,plane,nrm,c,z0,dw,dh);
    else doorOn(out,axis,plane,nrm,c,z0,dw,dh);
  }

  // louvred vent (horizontal slats in a frame) for gable peaks
  function ventLouvre(out, axis, plane, nrm, c, z0, w, h){
    const put=putOn(axis);
    put(out,plane,nrm, c-w/2-0.06, c+w/2+0.06, z0-0.05, z0+h+0.05,'trim',0.5,0.06);   // frame
    put(out,plane,nrm, c-w/2, c+w/2, z0, z0+h,'dark',0.0,0.10);                        // recess
    const n=Math.max(3,Math.round(h/0.14)); for(let i=0;i<n;i++){ const zz=z0+h*((i+0.5)/n);
      put(out,plane,nrm, c-w/2+0.03,c+w/2-0.03, zz-0.02, zz+0.03,'trim',0.2,0.12); }   // slats
  }

  // blank framed sign board (letter separately)
  function signBoard(out, axis, plane, nrm, c, z0, w, h){
    const put=putOn(axis);
    put(out,plane,nrm, c-w/2-0.1, c+w/2+0.1, z0-0.1, z0+h+0.1,'wood',0.3,0.06);        // frame
    put(out,plane,nrm, c-w/2, c+w/2, z0, z0+h,'sign',0.35,0.10);                       // blank face
  }

  // exterior HVAC / condenser unit standing beside a wall
  function hvacUnit(out, x0,x1, y0,y1, z0,z1){
    boxSolid(out, x0,x1, y0,y1, z0, z1,'galv',null,0.05);
    const put=putOn('x');
    for(let z=z0+0.18; z<z1-0.1; z+=0.14) put(out, x1,1, y0+0.06,y1-0.06, z-0.02,z+0.02,'galv',-1.4,0.06); // side louvres
    // top fan grille
    const fcx=(x0+x1)/2, fcy=(y0+y1)/2, r=Math.min(x1-x0,y1-y0)*0.36;
    slab(out, [[fcx-r,fcy-r],[fcx+r,fcy-r],[fcx+r,fcy+r],[fcx-r,fcy+r]], z1+0.01,'dark',0.0);
    slab(out, [[fcx-0.03,fcy-r],[fcx+0.03,fcy-r],[fcx+0.03,fcy+r],[fcx-0.03,fcy+r]], z1+0.02,'steel',0.3);
    slab(out, [[fcx-r,fcy-0.03],[fcx+r,fcy-0.03],[fcx+r,fcy+0.03],[fcx-r,fcy+0.03]], z1+0.02,'steel',0.3);
  }

  // vertical exhaust stack / metal flue with a rain cap (smoke anchored here)
  function exhaustStack(out, x, y, baseZ, topZ){
    boxSolid(out, x-0.13,x+0.13, y-0.13,y+0.13, baseZ, topZ,'steel',null,0.1);
    boxSolid(out, x-0.17,x+0.17, y-0.17,y+0.17, topZ, topZ+0.1,'steel',null,0.3);   // collar
    boxSolid(out, x-0.19,x+0.19, y-0.19,y+0.19, topZ+0.22, topZ+0.3,'steel',null,0.4); // rain cap
    boxSolid(out, x-0.03,x+0.03, y-0.03,y+0.03, topZ+0.1, topZ+0.22,'dark',null,0);   // cap post
  }

  // raised concrete loading dock along the +X wall, with bumpers + an end ramp
  function loadingDock(out, b, bays){
    const hw=b.Wd/2, y0=-b.Ln/2, y1=b.Ln/2, h=1.05, depth=1.9;
    const x0=hw+0.05, x1=hw+depth, dy0=y0+0.5, dy1=y1-0.5;
    boxSolid(out, x0,x1, dy0,dy1, 0, h,'cinder',null,0);
    wall(out, x1,dy0, x1,dy1, h-0.13, h,'steel',null,0.25);                          // steel edge nosing
    for(const by of bays){ decalX(out, x1,1, by-0.5,by+0.5, h-0.55,h-0.02,'dark',0.0,null,true,0.05); } // rubber bumpers
    for(let s=0;s<3;s++){ const sz=h*(1-(s+1)/3); const yy=dy1+s*0.42;               // end steps
      boxSolid(out, x0,x1, yy,yy+0.42, 0, Math.max(0.02,sz),'cinder',null,0); }
  }

  // roof-mounted boom / davit (hoist gear over the +Y front)
  function boomDavit(out, b){
    const x=b.Wd*0.3, y1=b.Ln/2, base=b.eaveZ-0.3, top=b.ridgeZ+0.7;
    boxSolid(out, x-0.08,x+0.08, y1-0.08,y1+0.08, base, top,'steel',null,0.2);        // mast
    const n=7, ty=y1+2.6, tz=top-1.0;
    for(let i=0;i<n;i++){ const t=i/(n-1); const yy=y1+(ty-y1)*t, zz=top+(tz-top)*t;
      boxSolid(out, x-0.07,x+0.07, yy-0.07,yy+0.07, zz-0.07, zz+0.07,'steel',null,0.2); } // boom arm
    boxSolid(out, x-0.02,x+0.02, ty-0.02,ty+0.02, x*0+base+0.4, tz-0.05,'dark',null,0); // hoist line
    boxSolid(out, x-0.1,x+0.1, ty-0.07,ty+0.07, tz-0.24, tz-0.04,'steel',null,0);       // block
  }

  // small louvred barn cupola on the ridge
  function cupolaVent(out, b){
    const s=0.42, z0=b.ridgeZ-0.05, z1=z0+0.85, put=putOn;
    boxSolid(out, -s,s, -s,s, z0, z1,'trim',null,0.1);
    for(let k=1;k<4;k++){ const zz=z0+(z1-z0)*(k/4);
      decalY(out, s,1, -s+0.05,s-0.05, zz-0.03,zz+0.03,'dark',0.2,null,true,0.06);
      decalX(out, s,1, -s+0.05,s-0.05, zz-0.03,zz+0.03,'dark',0.2,null,true,0.06); }
    const cs=s+0.12, apex=z1+0.5;
    tri(out,[-cs,cs,z1],[cs,cs,z1],[0,0,apex],'roof',0.35);
    tri(out,[cs,-cs,z1],[cs,cs,z1],[0,0,apex],'roof',0.45);
    tri(out,[-cs,-cs,z1],[-cs,cs,z1],[0,0,apex],'roof',-0.1);
    tri(out,[-cs,-cs,z1],[cs,-cs,z1],[0,0,apex],'roof',-0.2);
    boxSolid(out, -0.05,0.05, -0.05,0.05, apex, apex+0.28,'steel',null,0.3);          // finial
  }

  // long raised roof monitor / clerestory along the ridge (fish-plant ventilator)
  function roofMonitor(out, b, siTex, roofTex){
    const ml=b.Ln*0.62, my0=-ml/2, my1=ml/2;
    const mhw=b.Wd*0.21, baseZ=b.ridgeZ-0.12, wallTop=baseZ+1.0, ov=0.18;
    const mridge=wallTop + mhw*0.7;
    // walls
    wall(out, -mhw,my0, -mhw,my1, baseZ, wallTop,'body',siTex);
    wall(out,  mhw,my1,  mhw,my0, baseZ, wallTop,'body',siTex);
    wall(out,  mhw,my0, -mhw,my0, baseZ, wallTop,'body',siTex);
    wall(out, -mhw,my1,  mhw,my1, baseZ, wallTop,'body',siTex);
    // gable triangles
    tri(out,[-mhw,my0,wallTop],[mhw,my0,wallTop],[0,my0,mridge],'body',0,[[0,wallTop],[2*mhw,wallTop],[mhw,mridge]],siTex);
    tri(out,[mhw,my1,wallTop],[-mhw,my1,wallTop],[0,my1,mridge],'body',0,[[0,wallTop],[2*mhw,wallTop],[mhw,mridge]],siTex);
    // continuous glazing + louvre band on the long sides
    for(const [xv,nx] of [[-mhw,-1],[mhw,1]]){
      decalX(out, xv,nx, my0+0.15,my1-0.15, baseZ+0.18, wallTop-0.12,'glass',0.0,null,true,0.10);
      for(let k=0;k<3;k++){ const zz=baseZ+0.32+k*0.24; decalX(out, xv,nx, my0+0.2,my1-0.2, zz-0.025,zz+0.025,'trim',0.4,null,true,0.12); }
    }
    // little gable roof
    quad(out,[-mhw-ov,my0-ov,wallTop],[-mhw-ov,my1+ov,wallTop],[0,my1+ov,mridge],[0,my0-ov,mridge],'roof',-0.05,roofTex);
    quad(out,[mhw+ov,my1+ov,wallTop],[mhw+ov,my0-ov,wallTop],[0,my0-ov,mridge],[0,my1+ov,mridge],'roof',0.2,roofTex);
  }

  // ---- massing blocks -------------------------------------------------------
  function gableEnd(out, yv, ny, hw, eaveZ, ridgeZ, xa, mat, tex){
    const A=[-hw,yv,eaveZ], B2=[hw,yv,eaveZ], C=[xa,yv,ridgeZ];
    const uv = ny>0 ? [[0,eaveZ],[2*hw,eaveZ],[hw+xa,ridgeZ]] : [[2*hw,eaveZ],[0,eaveZ],[hw-xa,ridgeZ]];
    const P = ny>0 ? [A,B2,C] : [B2,A,C];
    out.push(F(P, mat, 0, 0, uv, tex));
  }
  function gableBlock(out, Wd, y0,y1, fZ, eaveZ, ridgeZ, xr, siTex, opt){
    opt=opt||{};
    const hw=Wd/2, ov=opt.ov!=null?opt.ov:0.32;
    wall(out, -hw,y0, -hw,y1, fZ, eaveZ, 'body', siTex);
    wall(out,  hw,y1,  hw,y0, fZ, eaveZ, 'body', siTex);
    wall(out,  hw,y0, -hw,y0, fZ, eaveZ, 'body', siTex);
    wall(out, -hw,y1,  hw,y1, fZ, eaveZ, 'body', siTex);
    gableEnd(out, y0,-1, hw, eaveZ, ridgeZ, xr, 'body', siTex);
    gableEnd(out, y1, 1, hw, eaveZ, ridgeZ, xr, 'body', siTex);
    const rTex=opt.roofTex||null, yA=y0-ov, yB=y1+ov;
    quad(out, [-hw-ov,yA,eaveZ],[-hw-ov,yB,eaveZ],[xr,yB,ridgeZ],[xr,yA,ridgeZ], 'roof', -0.05, rTex);
    quad(out, [hw+ov,yB,eaveZ],[hw+ov,yA,eaveZ],[xr,yA,ridgeZ],[xr,yB,ridgeZ], 'roof', 0.15, rTex);
    wall(out, -hw-ov,yB, -hw-ov,yA, eaveZ-0.22, eaveZ, 'trim', null, 0.35);
    wall(out,  hw+ov,yA,  hw+ov,yB, eaveZ-0.22, eaveZ, 'trim', null, 0.35);
    for(const yv of [yA,yB]) for(const sgn of [-1,1]){ const ex=sgn*(hw+ov);
      out.push(F([[ex,yv,eaveZ],[xr,yv,ridgeZ],[xr,yv,ridgeZ-0.2],[ex,yv,eaveZ-0.2]],'trim',0.5,0.05,null,null)); }
    return { hw, yA, yB, ov };
  }
  function gambrelBlock(out, Wd, y0,y1, fZ, eaveZ, topZ, siTex, opt){
    opt=opt||{};
    const hw=Wd/2, ov=0.3, brk=hw*0.5, midZ=eaveZ+(topZ-eaveZ)*0.55;
    wall(out, -hw,y0, -hw,y1, fZ, eaveZ, 'body', siTex);
    wall(out,  hw,y1,  hw,y0, fZ, eaveZ, 'body', siTex);
    wall(out,  hw,y0, -hw,y0, fZ, eaveZ, 'body', siTex);
    wall(out, -hw,y1,  hw,y1, fZ, eaveZ, 'body', siTex);
    for(const [yv,ny] of [[y0,-1],[y1,1]]){
      const pts = ny>0
        ? [[-hw,yv,eaveZ],[hw,yv,eaveZ],[brk,yv,midZ],[0,yv,topZ],[-brk,yv,midZ]]
        : [[hw,yv,eaveZ],[-hw,yv,eaveZ],[-brk,yv,midZ],[0,yv,topZ],[brk,yv,midZ]];
      out.push(F(pts,'body',0,0,null,null));
    }
    const yA=y0-ov,yB=y1+ov, rTex=opt.roofTex||null;
    quad(out, [-hw-ov,yA,eaveZ],[-hw-ov,yB,eaveZ],[-brk,yB,midZ],[-brk,yA,midZ],'roof',-0.05,rTex);
    quad(out, [hw+ov,yB,eaveZ],[hw+ov,yA,eaveZ],[brk,yA,midZ],[brk,yB,midZ],'roof',0.15,rTex);
    quad(out, [-brk,yA,midZ],[-brk,yB,midZ],[0,yB,topZ],[0,yA,topZ],'roof',0.0,rTex);
    quad(out, [brk,yB,midZ],[brk,yA,midZ],[0,yA,topZ],[0,yB,topZ],'roof',0.2,rTex);
    wall(out, -hw-ov,yB, -hw-ov,yA, eaveZ-0.22, eaveZ,'trim',null,0.35);
    wall(out,  hw+ov,yA,  hw+ov,yB, eaveZ-0.22, eaveZ,'trim',null,0.35);
    for(const yv of [yA,yB]) for(const sgn of [-1,1]){
      const segs=[[sgn*(hw+ov),eaveZ, sgn*brk,midZ],[sgn*brk,midZ, 0,topZ]];
      for(const [xa,za,xb2,zb] of segs)
        out.push(F([[xa,yv,za],[xb2,yv,zb],[xb2,yv,zb-0.2],[xa,yv,za-0.2]],'trim',0.5,0.05,null,null)); }
    return { hw, yA, yB, ov, midZ };
  }
  // mono-pitch shed: -X wall low, +X wall high, single roof plane, trapezoid end walls
  function shedBlock(out, Wd, y0,y1, fZ, eaveLo, eaveHi, siTex, opt){
    opt=opt||{};
    const hw=Wd/2, ov=0.3;
    wall(out, -hw,y0, -hw,y1, fZ, eaveLo, 'body', siTex);
    wall(out,  hw,y1,  hw,y0, fZ, eaveHi, 'body', siTex);
    out.push(F([[hw,y0,fZ],[-hw,y0,fZ],[-hw,y0,eaveLo],[hw,y0,eaveHi]],'body',0,0,
      [[0,fZ],[2*hw,fZ],[2*hw,eaveLo],[0,eaveHi]], siTex));   // -Y wall
    out.push(F([[-hw,y1,fZ],[hw,y1,fZ],[hw,y1,eaveHi],[-hw,y1,eaveLo]],'body',0,0,
      [[0,fZ],[2*hw,fZ],[2*hw,eaveHi],[0,eaveLo]], siTex));   // +Y wall
    const yA=y0-ov,yB=y1+ov, rTex=opt.roofTex||null;
    quad(out, [-hw-ov,yA,eaveLo],[-hw-ov,yB,eaveLo],[hw+ov,yB,eaveHi],[hw+ov,yA,eaveHi],'roof',0.15,rTex);
    wall(out, -hw-ov,yB, -hw-ov,yA, eaveLo-0.22, eaveLo,'trim',null,0.35);           // low fascia
    wall(out,  hw+ov,yA,  hw+ov,yB, eaveHi-0.22, eaveHi,'trim',null,0.35);           // high fascia
    for(const yv of [yA,yB]) out.push(F([[-hw-ov,yv,eaveLo],[hw+ov,yv,eaveHi],[hw+ov,yv,eaveHi-0.2],[-hw-ov,yv,eaveLo-0.2]],'trim',0.5,0.05,null,null));
    return { hw, yA, yB, ov, eaveLo, eaveHi };
  }

  function cornerboard(out, x, y, fZ, topZ, mat){ const t=0.085; boxSolid(out, x-t,x+t, y-t,y+t, fZ, topZ, mat||'trim', null, 0.2); }
  function foundation(out, b, xhw, y0, y1){ boxSolid(out, -xhw,xhw, y0,y1, 0, b.fH, 'stone', null, -0.1); }

  // ---- assemble the building ------------------------------------------------
  function build(b){
    const out=[];
    const siTex = sidingTex(b.siding);
    const roofTex = roofTexFor(b.roof);
    const hw=b.Wd/2, y0=-b.Ln/2, y1=b.Ln/2;
    const cornerMat = (b.siding==='corrugated') ? 'steel' : 'trim';

    foundation(out, b, hw+0.05, y0, y1);

    // MAIN MASS
    let eaveZ=b.eaveZ, ridgeZ=b.ridgeZ, blk;
    if(b.shape==='gambrel'){
      ridgeZ=b.eaveZ + b.rise + 0.3;
      blk=gambrelBlock(out, b.Wd, y0,y1, b.fH, b.eaveZ, ridgeZ, siTex, {roofTex});
    } else if(b.shape==='shed'){
      const eaveLo=b.eaveZ-0.2, eaveHi=b.eaveZ + b.rise*1.1;
      eaveZ=eaveHi; ridgeZ=eaveHi;
      blk=shedBlock(out, b.Wd, y0,y1, b.fH, eaveLo, eaveHi, siTex, {roofTex});
    } else {
      blk=gableBlock(out, b.Wd, y0,y1, b.fH, b.eaveZ, ridgeZ, 0, siTex, {roofTex, ov:b.ov});
    }

    // CINDERBLOCK wainscot (lower wall band)
    if(b.base==='block'){
      const bt=blockTex(), bandZ=b.fH + Math.min(1.35, b.wallH*0.34);
      for(const [xv,nx] of [[-hw,-1],[hw,1]]) decalX(out, xv,nx, y0,y1, b.fH, bandZ,'cinder',0.05,bt,false,0.03);
      for(const [yv,ny] of [[y0,-1],[y1,1]]) decalY(out, yv,ny, -hw,hw, b.fH, bandZ,'cinder',0.05,bt,false,0.03);
      for(const [xv,nx] of [[-hw,-1],[hw,1]]) decalX(out, xv,nx, y0,y1, bandZ, bandZ+0.08,'steel',0.4);
      for(const [yv,ny] of [[y0,-1],[y1,1]]) decalY(out, yv,ny, -hw,hw, bandZ, bandZ+0.08,'steel',0.4);
    }

    // CORNERBOARDS / corner flashing
    const cTop = b.shape==='shed' ? blk.eaveHi : (b.shape==='gambrel'? b.eaveZ : b.eaveZ);
    cornerboard(out,-hw,y0,b.fH,b.shape==='shed'?blk.eaveLo:cTop,cornerMat);
    cornerboard(out, hw,y0,b.fH,cTop,cornerMat);
    cornerboard(out,-hw,y1,b.fH,b.shape==='shed'?blk.eaveLo:cTop,cornerMat);
    cornerboard(out, hw,y1,b.fH,cTop,cornerMat);

    // MAIN DOOR on +Y gable (front)
    const dw = { doubleBarn:Math.min(b.Wd*0.62,3.2), slidingBarn:Math.min(b.Wd*0.56,3.0),
                 plank:1.1, rollUp:Math.min(b.Wd*0.6,3.4), personnel:1.0 }[b.door] || 2.2;
    const dh = { doubleBarn:Math.min(b.wallH*0.84,2.8), slidingBarn:Math.min(b.wallH*0.84,2.8),
                 plank:2.15, rollUp:Math.min(b.wallH*0.86,3.4), personnel:2.1 }[b.door] || 2.2;
    placeDoor(out, b.door, 'y', y1, 1, 0, b.fH, dw, dh, b);

    // +Y PEAK: sign > loft > vent
    const peakZ = b.eaveZ + (ridgeZ-b.eaveZ)*0.42;
    if(b.sign){
      signBoard(out,'y', y1,1, 0, b.eaveZ-1.25, Math.min(b.Wd*0.74, 4.4), 1.0);
    } else if(b.loft==='door'){
      plankDoor(out,'y', y1,1, 0, b.eaveZ+0.05, 0.95, 1.25);
      decalY(out, y1,1, -0.55,0.55, b.eaveZ+1.5, b.eaveZ+1.62,'wood',0.5,null,true,0.05);   // hood beam
      boxSolid(out, -0.06,0.06, y1+0.02,y1+0.5, b.eaveZ+1.5, b.eaveZ+1.62,'wood',null,0.3);  // beam out
    } else if(b.loft==='window'){
      windowOn(out,'y', y1,1, 0, peakZ-0.35, 0.72, 0.8, b);
    } else if(b.vents){
      ventLouvre(out,'y', y1,1, 0, peakZ-0.2, 0.9, Math.min(1.0, ridgeZ-peakZ+0.1));
    }

    // -Y PEAK + back windows
    if(b.vents) ventLouvre(out,'y', y0,-1, 0, peakZ-0.2, 0.9, Math.min(1.0, ridgeZ-peakZ+0.1));
    else if(b.shape!=='shed') windowOn(out,'y', y0,-1, 0, peakZ-0.35, 0.72, 0.8, b);
    const ww=0.82, wh=1.12, sillG=b.fH+1.0;
    windowOn(out,'y', y0,-1, -hw*0.42, sillG, ww,wh, b);
    windowOn(out,'y', y0,-1,  hw*0.42, sillG, ww,wh, b);

    // LOADING DOCK + roll-up bays on +X (processing)
    const bays=[];
    if(b.dock){
      const nBay = b.Wd>8 ? 3 : 2, bw=Math.min(2.6, (b.Ln*0.7)/nBay);
      for(let i=0;i<nBay;i++){ const c=y0 + b.Ln*((i+0.6)/(nBay+0.2)); bays.push(c);
        rollUpDoor(out,'x', hw,1, c, b.fH, bw, Math.min(b.wallH*0.7,2.9)); }
      loadingDock(out, b, bays);
    }

    // LONG-WALL WINDOWS (+X visible, -X back)
    const nW=Math.max(1, Math.round(b.Ln/2.6*(0.5+b.winD)));
    for(const [xv,nx] of [[-hw,-1],[hw,1]]){
      for(let i=0;i<nW;i++){ const c=y0+ b.Ln*((i+0.5)/nW);
        if(nx>0 && bays.some(by=>Math.abs(c-by)<1.8)) continue;
        windowOn(out,'x', xv,nx, c, sillG, ww,wh, b);
      }
      if(b.type==='processing'){                                        // clerestory industrial band
        const bz=b.eaveZ-1.35, nH=Math.max(2,Math.round(b.Ln/2.4));
        for(let i=0;i<nH;i++){ const c=y0+ b.Ln*((i+0.5)/nH);
          windowOn(out,'x', xv,nx, c, bz, 0.72, 0.92, {windows:'industrial'}); }
      }
    }

    // HVAC beside -X wall
    if(b.hvac){ const hy=y0+b.Ln*0.32; hvacUnit(out, -hw-1.4,-hw-0.2, hy-0.7,hy+0.7, 0, 1.15); }

    // EXHAUST STACKS near the ridge
    const ns=Math.min(3,b.stacks|0);
    for(let i=0;i<ns;i++){ const sy=y0 + b.Ln*((i+1)/(ns+1)); exhaustStack(out, hw*0.3, sy, ridgeZ-0.4, ridgeZ+2.0); }

    // ROOF MONITOR / CUPOLA
    if(b.cupola==='monitor' && b.shape!=='shed') roofMonitor(out, b, siTex, roofTex);
    else if(b.cupola==='cupola' && b.shape!=='shed') cupolaVent(out, b);

    // ROOF-MOUNTED BOOM
    if(b.boom) boomDavit(out, b);

    return out;
  }

  // ---- weathering / night post pass + RGBA ----------------------------------
  function post(bufs, b){
    const { rbuf, ibuf, nbuf, dep } = bufs;
    const N=W*H, out=new Array(N).fill(null);
    for(let i=0;i<N;i++){ if(rbuf[i]) out[i]=rbuf[i][ibuf[i]]; }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){
      const i=y*W+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){
        const nx=x+dx, ny=y+dy; if(nx>=W||ny>=H) continue;
        const j=ny*W+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j;
          const idx=Math.max(0,ibuf[far]-2); out[far]=rbuf[far][idx]; }
      }
    }
    const wx=b.weather;
    if(wx>0.02){
      const rnd=mulberry32(1234|((b.size*97)|0));
      const rustRoof = (b.roof==='rusted');
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='body'||m==='cinder'||m==='galv'||m==='rust') && rnd()<wx*0.07){ out[i]=rbuf[i][Math.max(0,Math.min(rbuf[i].length-1,ibuf[i]-1))]; }
        if(m==='roof'){ if(rustRoof){ if(rnd()<wx*0.05) out[i]=mix(out[i],'#7a4a2c',0.3+rnd()*0.2); }
          else if(rnd()<wx*0.035){ out[i]=mix(out[i], '#47543c', 0.28+rnd()*0.16); } }
      }
    }
    if(b.night){
      for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x;
        if(nbuf[i]==='glass'){ for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*W+(x+dx);
          if(out[j] && nbuf[j]!=='glass' && nbuf[j]!=='glassHi') out[j]=mix(out[j],'#f0c66a',0.28); } } }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue;
      let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; }
    }
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
    let ridgeZ=b.ridgeZ; if(b.shape==='gambrel') ridgeZ+=0.3; if(b.shape==='shed') ridgeZ=b.eaveZ+b.rise*1.1;
    const hw=b.Wd/2, y0=-b.Ln/2;
    const ns=Math.min(3,b.stacks|0), st=[];
    for(let i=0;i<ns;i++){ const sy=y0 + b.Ln*((i+1)/(ns+1)); st.push(pj(hw*0.3, sy, ridgeZ+2.15)); }
    return { stacks:st, door:pj(0,b.Ln/2,b.fH+1.0), ridge:pj(0,0,ridgeZ), Wd:b.Wd, Ln:b.Ln };
  }
  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.WharfBuilding = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    TYPES, SHAPES, SIDINGS, ROOFS:ROOF_KEYS, BODY, TRIM, DOORS, WINDOWS, CUPOLAS, PRESETS, KEY,
    ROOF_RAMPS:{ asphaltGrey:ROOFS.asphaltGrey, asphaltBrown:ROOFS.asphaltBrown, metalSeam:ROOFS.metal, corrugated:GALV, rusted:RUST },
    GALV, RUST, CINDER, STEEL, SIGN, STONE, WOOD,
    render, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
