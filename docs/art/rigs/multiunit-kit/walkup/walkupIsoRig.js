/* Hidden Harbours — parametric ISO WALK-UP APARTMENT BLOCK rig (ADR-0006 bake pipeline, SAME
   turntable + camera + shading as houseIsoRig.js / rowhouseIsoRig.js / interiorIsoRig.js / the
   fleet). ONE parametric 3D SLAB BLOCK — a double-loaded corridor with a stair-and-lift core at one
   end — baked to pixel sheets through the SHARED 3/4 camera: 45deg steps, elev 40deg default,
   flat-facet shading, z-buffered, ordered dither, per-face uv texture, depth-edge darkening, NO AA.
   32 px = 1 m. All 8 facings fall out of one model.

   THE BLOCK IS A CORE PLUS A GRID OF UNITS. Units are a list, as in the terrace, but arranged on a
   double-loaded corridor instead of a street: perFloor = ceil(units/storeys), split into a front row
   and a rear row, columns = the wider of the two. Nothing in the rig knows "twelve" — corridor,
   plate, windows, loggias and roof furniture are generated per unit, so a 4-unit block and a 12-unit
   block are the same model with a different list.

   WHY IT DOES NOT LOOK LIKE THE TERRACE. Same two tiers, deliberately different vocabulary — the
   silhouette does most of it (a deep slab with a projecting core, not a run of narrow bays):
     basic    CONCRETE-BANDED BRICK WALK-UP — the floor slabs are expressed as continuous concrete
              bands wrapping the block, brick infill panels sit between them, and the openings are
              wide horizontal aluminium sliders. Circulation is external: an open galvanised-steel
              stair tower bolted to the end of the core, mesh balustrades, concrete treads. Thin
              metal fascia instead of a cornice, no parapet coping, no chimneys. Reads horizontal
              and utilitarian where the terrace reads vertical and domestic.
     luxury   CANNERY LOFT BLOCK — dark reclaimed brick in deep reveals, tall steel-framed
              industrial glazing (a grid of small panes) as the signature, blackened metal, and
              loggias CARVED INTO the mass rather than balconies projecting off it. Bronze portal at
              the entry, brick piers expressed vertically, planted parapet and a pergola over the
              roof deck. Masonry-and-steel where the terrace was standing seam, cedar and glass.

   BUILDER SURFACE (every axis resolved per render, no re-modelling):
     tier:'basic'|'luxury'   units:4|6|8|12   storeys:2|3   beds:1|2|3   mix:'uniform'|'mixed'
     body / accent: BODY ramp keys   band:'concrete'|'render'|'painted'
     glazing:'slider'|'industrial'|'casement'   loggia:'none'|'recessed'|'inset'
     stairTower:'external'|'internal'   roofDeck:bool   lockers:bool   parking:bool
     mailwall:bool   lit:0..1 (night occupancy)   weather:0..1   night:bool   elev:deg
     detail:'lived'|'plain'  and individually: dressing acUnits windowBoxes rainwater entryLamps
     signage meters tiePlates roofKit hoist binStore kerb
     outline:bool (ADR-0031 A/B, default OFF)

   REGISTRATION CONTRACT for Art/walkupUnitIsoRig.js (station precedent — the interior rig measures
   nothing, it reads the shell): shell(opts) publishes wall thickness, floor and ceiling heights per
   storey, every unit's interior footprint AND which of its edges are glazed, the corridor box, the
   core's lobby / stair / lift / laundry / locker boxes, and every opening in the SAME metres this
   bake uses. units(opts) publishes the gameplay unit table. anchors(dir,opts) reports entry / mail /
   lobby / stair / lift / window / loggia / vent points in cell px per facing.

   THE LIVED-IN PASS (detail:'lived', on by default). The shell above is the architecture; this is
   the evidence of occupation, and it is what stops a 12-flat elevation reading as a spreadsheet.
   Per FLAT, from a seed fixed to that flat so nothing shimmers between bakes: blinds pulled to
   different heights, a window air conditioner on some, a planted window box on others. Per BLOCK:
   rainwater downpipes with hopper heads and bracket lines at every corner, entry lamps that light
   at night, a relief number plate, a meter cabinet and conduit riser (basic wears its services
   outside), cast-iron pattress plates on the brick (luxury — the plates that tie a loft's floors to
   its walls), a fire ladder to the roof and the satellite dishes every walk-up accumulates (basic),
   a riveted water tank on legs (luxury), a blocked-up loading bay with its hoist beam still bolted
   above it (luxury — the relic that says cannery), relief lettering on the parapet, a kerb and
   planting strip, and a bin enclosure by the parking. detail:'plain' clears all of it, for a
   background silhouette.

   LIGHT: matches the shipped neighbours — upper-left key, LN = normalize([-0.42,0.72,0.52]). The art
   bible §1 rules top-of-frame is canonical and asks upper-left rigs be corrected before their next
   bake; this block stands in the same street as the terrace and the houses, so matching the shipped
   set was chosen over matching the doc. One constant, whole-set decision.
   KEYLINE: ringless by default (ADR-0031); {outline:true} kept as a live A/B.

   Exposes globalThis.WalkupIso = { W,H,PX,DIRS,pivot,order,defaultElev, TIERS,BODY,CONC,TRIM,IRON,
     BRONZE,GLAZINGS,LOGGIAS,BANDS,PRESETS, dims(opts), shell(opts), units(opts), render(dir,opts),
     anchors(dir,opts), project(dir,p,elev), gameplay(opts), gameplayAll(opts) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1600, H = 1560, cx = 800, groundY = 940;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;

  // ---- palettes, dark -> light (KTC master ramps, shared with the fleet) ----
  const BODY = {
    buffBrick:      ['#4a3f2a','#61533a','#7a6a4c','#94845f','#ad9d77','#c5b795'],
    brownBrick:     ['#2f231d','#453329','#5b4636','#715a46','#876f58','#9d876e'],
    greyBrick:      ['#33343a','#42444b','#54575d','#666a70','#7a7e84','#8f9399'],
    redBrick:       ['#3a201a','#552b20','#6e3728','#874634','#a05743','#b96b55'],
    reclaimedBrick: ['#24181a','#3a2523','#50332d','#664239','#7c5345','#936854'],
    inkBrick:       ['#1a1a1f','#26262d','#34353d','#43454e','#54565f','#666971'],
    sandRender:     ['#6f6350','#877a63','#a2947a','#bcae92','#d3c6a9','#e7dcc2'],
    paintedWhite:   ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    paintedSage:    ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'],
    seaGlass:       ['#143a38','#1f4d4a','#2c625e','#3b7872','#4d8f88','#66a69d'],
    galv:           ['#464d51','#5a6267','#727c81','#8c979c','#a6b1b5','#c2cccf'],
    charTimber:     ['#17171a','#212126','#2c2d33','#393a42','#484a53','#595c66'],
  };
  const CONC   = ['#3d4144','#4c5154','#5e6366','#727679','#878b8e','#9ba0a2'];
  const BOARD  = ['#43464a','#53575b','#666a6e','#7b8084','#8f9498','#a3a8ac'];
  const TRIM   = ['#9aa09a','#b4b8b0','#ccd0c7','#e0e2da','#eef0e8','#f8f9f2'];
  const IRON   = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970'];
  const BRONZE = ['#2b1e10','#40301a','#573f22','#6e522d','#87673b','#a0804f'];
  const MEMB   = ['#20242a','#292e35','#343a42','#41474f','#4e555e','#5c646d'];
  const PLANT  = ['#1b2a1c','#263a25','#334c30','#42603c','#537449','#668a58'];
  const GLASSD = ['#1e2c31','#26383d','#33474d','#40585f','#547078'];
  const GLASSN = ['#7a4f18','#b98a2f','#eed07a'];
  const GLASS_HI = '#cfe6e8';
  const KEY = '#1a1c22';

  const GLAZINGS = ['slider','industrial','casement'];
  const LOGGIAS  = ['none','recessed','inset'];
  const BANDS    = ['concrete','render','painted'];

  const TIERS = {
    basic: {
      body:'buffBrick', accent:'greyBrick', band:'concrete', glazing:'slider', loggia:'none',
      stairTower:'external', roofDeck:false, lockers:true, parking:true, mailwall:true,
      storeyH:2.95, fH:0.55, fasciaH:0.42, coreW:5.40, corridorW:1.70, unitD:8.60,
      unitW:{1:6.30,2:8.60,3:11.40}, weather:0.32,
    },
    luxury: {
      body:'reclaimedBrick', accent:'charTimber', band:'render', glazing:'industrial', loggia:'recessed',
      stairTower:'internal', roofDeck:true, lockers:true, parking:true, mailwall:true,
      storeyH:3.45, fH:0.30, fasciaH:0.78, coreW:5.90, corridorW:2.10, unitD:9.40,
      unitW:{1:7.10,2:9.40,3:12.20}, weather:0.07,
    },
  };

  // presets are NAMED BLOCKS — the building the quarter knows by sight
  const PRESETS = {
    walkup8:      { tier:'basic',  units:8,  storeys:2, beds:2, body:'buffBrick',      weather:0.34 },
    walkup12:     { tier:'basic',  units:12, storeys:3, beds:2, body:'brownBrick',     weather:0.30 },
    walkup12Fam:  { tier:'basic',  units:12, storeys:3, beds:3, body:'greyBrick',      weather:0.38, mix:'mixed' },
    walkup4:      { tier:'basic',  units:4,  storeys:2, beds:1, body:'redBrick',       weather:0.40 },
    cannery12:    { tier:'luxury', units:12, storeys:3, beds:2, body:'reclaimedBrick', weather:0.06 },
    cannery8Loft: { tier:'luxury', units:8,  storeys:2, beds:3, body:'inkBrick',       weather:0.09, mix:'mixed' },
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
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }
  function clamp(v,a,b){ return v<a?a:(v>b?b:v); }
  function r3(v){ return Math.round(v*1000)/1000; }

  // ---- camera / projection (identical to the terrace, so the two composite in one street) ----
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

  // ---- face builders. F = { v, mat, b, db, uv, tex, flat } -------------------
  function F(v,mat,b,db,uv,tex,flat){ return { v, mat, b:b||0, db:db||0, uv:uv||null, tex:tex||null, flat:!!flat }; }
  function wall(out, x0,y0,x1,y1, z0,z1, mat, tex, b){
    const L=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]], mat, b||0, 0,
      [[0,z0],[L,z0],[L,z1],[0,z1]], tex));
  }
  function slab(out, pts, z, mat, b, tex){
    const uv = tex ? pts.map(p=>[p[0],p[1]]) : null;
    out.push(F(pts.map(p=>[p[0],p[1],z]), mat, b||0, 0, uv, tex));
  }
  function quad(out, p0,p1,p2,p3, mat, b, tex){
    const u=Math.hypot(p1[0]-p0[0],p1[1]-p0[1],p1[2]-p0[2]);
    const v=Math.hypot(p3[0]-p0[0],p3[1]-p0[1],p3[2]-p0[2]);
    out.push(F([p0,p1,p2,p3], mat, b||0, 0, tex?[[0,0],[u,0],[u,v],[0,v]]:null, tex||null));
  }
  function boxSolid(out, x0,x1, y0,y1, z0,z1, mat, tex, b, topB){
    wall(out, x0,y0, x1,y0, z0,z1, mat, tex, b);
    wall(out, x1,y1, x0,y1, z0,z1, mat, tex, b);
    wall(out, x1,y0, x1,y1, z0,z1, mat, tex, b);
    wall(out, x0,y1, x0,y0, z0,z1, mat, tex, b);
    slab(out, [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, (b||0)+(topB!=null?topB:0.25), tex);
  }
  // hollow band — four bars round the edge, so a roof or deck inside stays visible. skipX omits a
  // span of the FRONT bar: a band that runs across the entry is visible through the entry hole,
  // which reads as a bar nailed across the doorway.
  function bandRing(out, x0,x1, y0,y1, t, z0,z1, mat, tex, b, topB, skipX){
    if(skipX && skipX[1]>x0 && skipX[0]<x1){
      if(skipX[0]>x0) boxSolid(out, x0, skipX[0], y1-t, y1, z0,z1, mat, tex, b, topB);
      if(skipX[1]<x1) boxSolid(out, skipX[1], x1, y1-t, y1, z0,z1, mat, tex, b, topB);
    } else boxSolid(out, x0, x1, y1-t, y1, z0,z1, mat, tex, b, topB);
    boxSolid(out, x0, x1, y0, y0+t, z0,z1, mat, tex, b, topB);
    boxSolid(out, x0, x0+t, y0+t, y1-t, z0,z1, mat, tex, b, topB);
    boxSolid(out, x1-t, x1, y0+t, y1-t, z0,z1, mat, tex, b, topB);
  }
  // a wall plane with rectangular holes punched in it (loggia voids)
  function wallMinusY(out, x0,x1, yv, ny, z0,z1, mat, tex, b, holes){
    let rects=[[x0,x1,z0,z1]];
    for(const h of (holes||[])){
      const next=[];
      for(const q of rects){
        const a0=q[0],a1=q[1],c0=q[2],c1=q[3];
        if(h.x1<=a0+0.001||h.x0>=a1-0.001||h.z1<=c0+0.001||h.z0>=c1-0.001){ next.push(q); continue; }
        const cx0=Math.max(a0,h.x0), cx1=Math.min(a1,h.x1), cz0=Math.max(c0,h.z0), cz1=Math.min(c1,h.z1);
        if(c0<cz0-0.001) next.push([a0,a1,c0,cz0]);
        if(cz1<c1-0.001) next.push([a0,a1,cz1,c1]);
        if(a0<cx0-0.001) next.push([a0,cx0,cz0,cz1]);
        if(cx1<a1-0.001) next.push([cx1,a1,cz0,cz1]);
      }
      rects=next;
    }
    for(const q of rects){
      if(q[1]-q[0]<0.02 || q[3]-q[2]<0.02) continue;
      if(ny>0) wall(out, q[0],yv, q[1],yv, q[2],q[3], mat, tex, b);
      else      wall(out, q[1],yv, q[0],yv, q[2],q[3], mat, tex, b);
    }
  }
  function decalY(out, yv, ny, xs,xe, z0,z1, mat, b, tex, flat, db){
    const e=0.02*ny, uw=xe-xs, uh=z1-z0;
    const P = ny>0
      ? [[xs,yv+e,z0],[xe,yv+e,z0],[xe,yv+e,z1],[xs,yv+e,z1]]
      : [[xe,yv+e,z0],[xs,yv+e,z0],[xs,yv+e,z1],[xe,yv+e,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.06, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat));
  }
  function decalX(out, xv, nx, ys,ye, z0,z1, mat, b, tex, flat, db){
    const e=0.02*nx, uw=ye-ys, uh=z1-z0;
    const P = nx>0
      ? [[xv+e,ye,z0],[xv+e,ys,z0],[xv+e,ys,z1],[xv+e,ye,z1]]
      : [[xv+e,ys,z0],[xv+e,ye,z0],[xv+e,ye,z1],[xv+e,ys,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.06, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat));
  }
  const putter=(axis,plane,nrm)=>(out,a0,a1,z0,z1,mat,bias,db,tex,flat)=> axis==='y'
    ? decalY(out, plane,nrm, a0,a1, z0,z1, mat, bias, tex||null, flat!==false, db)
    : decalX(out, plane,nrm, a0,a1, z0,z1, mat, bias, tex||null, flat!==false, db);

  // ---- textures: integer ramp deltas ----------------------------------------
  function brickTex(kind){
    const CO=0.086, BR=0.235, deep=(kind==='reclaimed');
    return (u,v)=>{ const row=Math.floor(v/CO), f=((v%CO)+CO)%CO;
      const off=(row&1)*BR*0.5, su=(((u+off)%BR)+BR)%BR;
      if(f < 0.026) return deep?-3:-2;                        // bed joint, raked deeper on reclaimed
      if(su < 0.024) return deep?-2:-1;
      const t=hash2(Math.floor((u+off)/BR), row);
      if(deep){ if(t<0.20) return -1; if(t>0.90) return 1; return 0; }   // salvage brick is patchy
      if(t<0.14) return -1; if(t>0.93) return 1;
      return 0; };
  }
  function boardTex(){                                        // board-formed concrete, vertical marks
    const SP=0.20;
    return (u,v)=>{ const su=((u%SP)+SP)%SP;
      if(su < 0.018) return -1;
      const t=hash2(Math.floor(u/SP), Math.floor(v*6));
      return t<0.10?-1:(t>0.95?1:0); };
  }
  function renderTex(){
    return (u,v)=>{ const t=hash2(Math.floor(u*7), Math.floor(v*7)); return t<0.12?-1:(t>0.94?1:0); };
  }
  function meshTex(){                                         // stair-tower balustrade mesh
    const SP=0.075;
    return (u,v)=>{ const su=((u%SP)+SP)%SP, sv=((v%SP)+SP)%SP;
      return (su<0.028||sv<0.028) ? 0 : -3; };
  }
  function membraneTex(){
    return (u,v)=>{ const t=hash2(Math.floor(u*5), Math.floor(v*5)); return t<0.18?-1:0; };
  }
  function plantTex(){
    return (u,v)=>{ const t=hash2(Math.floor(u*9), Math.floor(v*9)); return t<0.3?-1:(t>0.78?1:0); };
  }
  function treadTex(){
    return (u,v)=>{ const t=hash2(Math.floor(u*6), Math.floor(v*6)); return t<0.15?-1:0; };
  }

  // ---- rasterizer (fleet recipe) -------------------------------------------
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

  // ---- resolve: the block becomes a CORE + a LIST OF UNITS -------------------
  function bedsFor(mix, i, beds){
    if(mix==='mixed') return (i%3===0) ? Math.max(1,beds-1) : beds;
    return beds;
  }
  function resolve(opts){
    opts = opts||{};
    const tierKey = (opts.tier==='luxury') ? 'luxury' : 'basic';
    const T = TIERS[tierKey];
    const g = (k,d)=> opts[k]!=null ? opts[k] : (T[k]!=null ? T[k] : d);
    const ALLOWED=[4,6,8,12];
    let n = Math.round(opts.units!=null?opts.units:8);
    n = ALLOWED.reduce((a,b)=>Math.abs(b-n)<Math.abs(a-n)?b:a, ALLOWED[0]);
    const storeys = Math.max(2, Math.min(3, Math.round(opts.storeys!=null?opts.storeys:(n===12?3:2))));
    const beds = Math.max(1, Math.min(3, Math.round(opts.beds!=null?opts.beds:2)));
    const mix = opts.mix || 'uniform';
    const storeyH = g('storeyH',3.0), fH = g('fH',0.5), fasciaH = g('fasciaH',0.5);
    const coreW = g('coreW',5.4), corridorW = g('corridorW',1.7), unitD = g('unitD',8.2);
    const uwTab = T.unitW;

    const det = (opts.detail||'lived') !== 'plain';
    const perFloor = Math.ceil(n/storeys);
    const frontCount = Math.ceil(perFloor/2), rearCount = perFloor - frontCount;
    const columns = Math.max(frontCount, rearCount);
    let unitW = uwTab[beds]||uwTab[2];
    // A double-loaded walk-up has a sensible maximum corridor length — past about 32 m it stops
    // being a walk-up and becomes a slab. Wide units squeeze to fit rather than the block growing:
    // a 3-bed at 9 m x 9 m is still a generous flat.
    const MAXW = 32.0;
    if(coreW + columns*unitW > MAXW) unitW = (MAXW - coreW)/columns;

    const Wd = coreW + columns*unitW;
    const Ln = 2*unitD + corridorW;
    const x0 = -Wd/2, coreX1 = x0 + coreW;
    const cyF = corridorW/2, cyR = -corridorW/2;

    // units: fill floor by floor, front row left to right, then rear row
    const bays=[]; let id=0;
    for(let s=0;s<storeys;s++){
      for(let side=0; side<2; side++){
        const cnt = side===0?frontCount:rearCount;
        for(let c=0;c<cnt;c++){
          if(id>=n) break;
          const ux0 = coreX1 + c*unitW, ux1 = ux0 + unitW;
          const bd = bedsFor(mix, id, beds);
          bays.push({ i:id, unit:'unit_'+(id+1), storey:s, side: side===0?'front':'rear', col:c,
            x0:ux0, x1:ux1, w:unitW, xc:(ux0+ux1)/2, beds:bd,
            y0: side===0? cyF : -Ln/2, y1: side===0? Ln/2 : cyR,
            facadeY: side===0? Ln/2 : -Ln/2, facadeN: side===0? 1 : -1,
            endWall: (c===cnt-1) ? 1 : 0 });
          id++;
        }
      }
    }
    const b = {
      tier:tierKey, n, storeys, beds, mix, perFloor, frontCount, rearCount, columns,
      unitW, unitD, corridorW, coreW, Wd, Ln, x0, coreX1, cyF, cyR, storeyH, fH, fasciaH, bays,
      body: opts.body || T.body, accent: opts.accent || T.accent, band: g('band','concrete'),
      glazing: g('glazing','slider'), loggia: g('loggia','none'),
      stairTower: g('stairTower','external'), roofDeck: g('roofDeck',false),
      lockers: g('lockers',true), parking: g('parking',true), mailwall: g('mailwall',true),
      // THE LIVED-IN PASS. Each piece is individually switchable and detail:'plain' clears the lot,
      // so the block can be baked bare for a background silhouette or dressed for a foreground hero.
      detail: det ? 'lived' : 'plain',
      dressing:    opts.dressing!=null    ? !!opts.dressing    : det,
      acUnits:     opts.acUnits!=null     ? !!opts.acUnits     : (det && tierKey==='basic'),
      windowBoxes: opts.windowBoxes!=null ? !!opts.windowBoxes : det,
      rainwater:   opts.rainwater!=null   ? !!opts.rainwater   : det,
      entryLamps:  opts.entryLamps!=null  ? !!opts.entryLamps  : det,
      signage:     opts.signage!=null     ? !!opts.signage     : det,
      meters:      opts.meters!=null      ? !!opts.meters      : (det && tierKey==='basic'),
      tiePlates:   opts.tiePlates!=null   ? !!opts.tiePlates   : (det && tierKey==='luxury'),
      roofKit:     opts.roofKit!=null     ? !!opts.roofKit     : det,
      hoist:       opts.hoist!=null       ? !!opts.hoist       : (det && tierKey==='luxury'),
      binStore:    opts.binStore!=null    ? !!opts.binStore    : det,
      kerb:        opts.kerb!=null        ? !!opts.kerb        : det,
      lit: opts.lit!=null ? opts.lit : 0.5,
      weather: opts.weather!=null ? opts.weather : T.weather,
      night: !!opts.night,
      outline: opts.outline!=null ? !!opts.outline : KEYLINE_DEFAULT,
      wallT: 0.32, partyT: 0.24, floorT: 0.28,
    };
    b.topZ = fH + storeys*storeyH;
    b.parapetZ = b.topZ + fasciaH;
    b.storeyZ = []; for(let s=0;s<storeys;s++) b.storeyZ.push(fH + s*storeyH);
    return b;
  }

  // ---- materials -----------------------------------------------------------
  function makeMats(b){
    const wx=b.weather, night=b.night;
    const wthBody=(ramp)=>ramp.map(c=>{ let x=desat(c, wx*0.46); x=mix(x,'#6b675e',wx*0.28); if(night)x=mix(x,'#1b2733',0.44); return x; });
    const wthHard=(ramp)=>ramp.map(c=>{ let x=desat(c, wx*0.30); x=mix(x,'#6f6a5f',wx*0.18); if(night)x=mix(x,'#1b2733',0.42); return x; });
    const nightOnly=(ramp)=>ramp.map(c=> night?mix(c,'#1b2733',0.38):c );
    const bodyR = BODY[b.body]||BODY.buffBrick, accR = BODY[b.accent]||BODY.galv;
    const bandR = b.band==='concrete'?CONC : (b.band==='render'?BODY.sandRender : BODY.paintedWhite);
    return {
      body:      { ramp: wthBody(bodyR) },
      accent:    { ramp: wthBody(accR) },
      band:      { ramp: wthHard(bandR) },
      conc:      { ramp: wthHard(CONC) },
      board:     { ramp: wthHard(BOARD) },
      trim:      { ramp: wthHard(TRIM) },
      metal:     { ramp: wthHard(BODY.galv) },
      iron:      { ramp: nightOnly(IRON) },
      bronze:    { ramp: wthHard(BRONZE) },
      roof:      { ramp: wthHard(MEMB) },
      plant:     { ramp: nightOnly(PLANT) },
      lobby:     { ramp: night?['#8a5c1e','#b98a2f','#dcb055','#f0d283','#f8e6b4']
                             : ['#6d7c6f','#8b9a86','#a9b69e','#c6d0b6','#dee5cd'] },
      glass:     { ramp: night?GLASSN:GLASSD },
      glassOff:  { ramp: night?nightOnly(GLASSD):GLASSD },
      glassHi:   { ramp: [night?'#f6dfa6':GLASS_HI] },
      cavity:    { ramp: ['#0d1013','#141a1e','#1b2328','#232c32'] },
    };
  }

  // ---- openings ------------------------------------------------------------
  // wide horizontal aluminium slider: the basic tier's signature opening
  // A blind or curtain pulled part way down. Nothing says "people live here" as cheaply as an
  // elevation where no two windows are dressed alike.
  function blindOver(out, put, c, ww, z, wh, frac, mat, b){
    if(!(frac>0.04)) return;
    const fz = z + wh*(1 - Math.min(0.62, frac));
    put(out, c-ww/2+0.012, c+ww/2-0.012, fz, z+wh, mat, b.night?-0.30:0.30, 0.078);
    put(out, c-ww/2+0.012, c+ww/2-0.012, fz, fz+0.035, mat, -0.55, 0.080);
  }
  function slider(out, axis, plane, nrm, c, z, ww, wh, b, lit, bl){
    const put=putter(axis,plane,nrm);
    const fr=0.07, gm = lit?'glass':(b.night?'glassOff':'glass');
    put(out, c-ww/2-fr, c+ww/2+fr, z-fr, z+wh+fr, 'metal', 0.30, 0.05);
    put(out, c-ww/2, c+ww/2, z, z+wh, gm, b.night?1.6:0.0, 0.07);
    put(out, c-0.035, c+0.035, z, z+wh, 'metal', 0.34, 0.085);          // meeting stile
    put(out, c-ww/2, c+ww/2, z+wh*0.5-0.02, z+wh*0.5+0.02, 'metal', 0.18, 0.082);
    blindOver(out, put, c, ww, z, wh, bl||0, 'trim', b);
    put(out, c-ww/2+0.06, c-ww/2+ww*0.24, z+wh*0.60, z+wh*0.86, 'glassHi', 0, 0.09);
    put(out, c-ww/2-0.14, c+ww/2+0.14, z-0.13, z-0.02, 'band', 0.22, 0.02, null, false);  // sill
    put(out, c-ww/2-0.08, c-ww/2-0.01, z-0.02, z+wh+0.05, 'body', -0.80, 0.04, null, false);   // reveal
    put(out, c-ww/2-0.15, c+ww/2+0.15, z+wh+0.07, z+wh+0.19, 'band', 0.26, 0.02, null, false); // lintel
  }
  // tall steel-framed industrial sash: the luxury tier's signature opening
  function industrial(out, axis, plane, nrm, c, z, ww, wh, b, lit, bl){
    const put=putter(axis,plane,nrm);
    const gm = lit?'glass':(b.night?'glassOff':'glass');
    put(out, c-ww/2-0.10, c+ww/2+0.10, z-0.10, z+wh+0.10, 'iron', 0.20, 0.04);   // steel frame
    put(out, c-ww/2, c+ww/2, z, z+wh, gm, b.night?1.6:0.0, 0.07);
    // reveal: a steel window in brick is set BACK, and without the shadow on its head and one
    // jamb the whole opening reads as a panel stuck on the wall. NB a bias at or below -1 trips
    // the rasteriser's back-face flip and LIGHTENS the facet, so the shadow has to stay above it.
    put(out, c-ww/2-0.10, c+ww/2+0.10, z+wh+0.02, z+wh+0.12, 'body', -0.80, 0.045, null, false);
    put(out, c-ww/2-0.10, c-ww/2-0.02, z-0.06, z+wh+0.06, 'body', -0.80, 0.045, null, false);
    put(out, c-ww/2-0.17, c+ww/2+0.17, z+wh+0.12, z+wh+0.27, 'band', 0.30, 0.03, null, false);   // concrete lintel
    const cols=3, rows=Math.max(4, Math.round(wh/0.52));
    for(let i=1;i<cols;i++){ const u=c-ww/2+ww*i/cols;
      put(out, u-0.022, u+0.022, z, z+wh, 'iron', 0.30, 0.085); }
    for(let j=1;j<rows;j++){ const v=z+wh*j/rows;
      put(out, c-ww/2, c+ww/2, v-0.020, v+0.020, 'iron', 0.26, 0.085); }
    put(out, c-ww/2, c+ww/2, z+wh*0.62-0.045, z+wh*0.62+0.045, 'iron', 0.5, 0.088); // opening light
    blindOver(out, put, c, ww, z, wh, bl||0, 'band', b);
    put(out, c-ww/2+0.05, c-ww/2+ww*0.26, z+wh*0.70, z+wh*0.92, 'glassHi', 0, 0.09);
    put(out, c-ww/2-0.12, c+ww/2+0.12, z-0.16, z-0.04, 'band', 0.18, 0.02, null, false);
  }
  function casement(out, axis, plane, nrm, c, z, ww, wh, b, lit, bl){
    const put=putter(axis,plane,nrm);
    const gm = lit?'glass':(b.night?'glassOff':'glass');
    put(out, c-ww/2-0.08, c+ww/2+0.08, z-0.08, z+wh+0.08, 'trim', 0.30, 0.05);
    put(out, c-ww/2, c+ww/2, z, z+wh, gm, b.night?1.6:0.0, 0.07);
    put(out, c-0.025, c+0.025, z, z+wh, 'trim', 0.28, 0.085);
    blindOver(out, put, c, ww, z, wh, bl||0, 'trim', b);
    put(out, c-ww/2+0.05, c-ww/2+ww*0.28, z+wh*0.64, z+wh*0.88, 'glassHi', 0, 0.09);
    put(out, c-ww/2-0.13, c+ww/2+0.13, z-0.13, z-0.02, 'band', 0.20, 0.02, null, false);
  }
  function openingFor(b){ return b.glazing==='industrial'?industrial:(b.glazing==='casement'?casement:slider); }

  function flatDoor(out, axis, plane, nrm, c, z0, dw, dh, mat, b, glazed){
    const put=putter(axis,plane,nrm);
    put(out, c-dw/2-0.09, c+dw/2+0.09, z0, z0+dh+0.09, mat, 0.34, 0.05);
    put(out, c-dw/2, c+dw/2, z0, z0+dh, mat, 0.05, 0.07);
    if(glazed){ put(out, c-dw/2+0.14, c+dw/2-0.14, z0+dh*0.42, z0+dh-0.14, b.night?'glass':'glass', b.night?1.5:0.0, 0.085);
      put(out, c-dw/2+0.18, c-dw/2+dw*0.36, z0+dh*0.66, z0+dh*0.84, 'glassHi', 0, 0.09); }
    put(out, c+dw/2-0.19, c+dw/2-0.11, z0+dh*0.44, z0+dh*0.50, 'metal', 1.1, 0.10);
  }

  // every loggia void in the block, so the facade holes and the recesses cannot disagree
  function loggiaOf(b, u){
    if(!(b.tier==='luxury' && b.loggia!=='none' && u.storey>0)) return null;
    const base=b.fH + u.storey*b.storeyH;
    const logW=Math.min(2.9, u.w*0.36), logX=u.x0 + u.w*0.24;
    return { side:u.side, y:u.facadeY, nrm:u.facadeN, logX, logW, base,
      top: base + b.storeyH - 0.62, dep: b.loggia==='inset' ? 1.9 : 1.45,
      x0: logX-logW/2, x1: logX+logW/2, z0: base, z1: base + b.storeyH - 0.62 };
  }
  function loggiaRects(b){ const out=[]; for(const u of b.bays){ const L=loggiaOf(b,u); if(L) out.push(L); } return out; }

  // the entry void, so the core's front face, the block's front wall and the recess all agree
  function entryHole(b){
    const lux=b.tier==='luxury', proj=lux?0.42:0.30, ex=(b.x0+b.coreX1)/2;
    const pw = lux?3.40:2.90;
    const ph = lux ? Math.min(b.fH+b.storeyH*1.42, b.topZ-0.6) : 2.62;
    return { side:'front', ex, pw, ph, proj, dep: lux?1.05:0.72,
             x0:ex-pw/2, x1:ex+pw/2, z0:0, z1:ph };
  }

  // ---- one unit's facade ---------------------------------------------------
  function buildUnitFacade(out, b, u, litFn){
    const open=openingFor(b), yv=u.facadeY, nrm=u.facadeN, lux=b.tier==='luxury';
    const base=b.fH + u.storey*b.storeyH;
    const wins = u.beds>=3 ? 4 : (u.beds===2 ? 3 : 2);
    // one RNG per flat, so a household's blinds, its air conditioner and its window box stay put
    // between bakes instead of shimmering from render to render
    const rnd = mulberry32(1013 + u.i*7919 + u.storey*131);
    const blinds=[]; for(let q=0;q<wins+2;q++){ const t=rnd();
      blinds.push(b.dressing ? (t<0.34 ? 0 : 0.18+rnd()*0.62) : 0); }
    const acAt  = (b.acUnits     && rnd()<0.62) ? Math.floor(rnd()*wins) : -1;
    const boxAt = (b.windowBoxes && rnd()<0.34) ? Math.floor(rnd()*wins) : -1;
    // the blocked-up loading bay REPLACES a ground-floor window rather than sharing its slot —
    // otherwise the window is drawn over the bay and the hoist beam above explains nothing
    const bay = (lux && b.hoist && u.storey===0 && u.side==='front')
      ? { c: b.coreX1 + Math.min(b.unitW*0.5, 3.4), w: 2.5 } : null;
    const L = loggiaOf(b, u), loggiaHere = !!L;
    // the loggia is CARVED OUT of the mass: a shadowed void with the slab as its floor, a
    // blackened metal rail set back in the reveal, and the glazed door inside it
    let logW=0, logX=0;
    if(loggiaHere){
      logW = L.logW; logX = L.logX;
      const dep = L.dep, top = L.top, by = yv - nrm*dep;
      // The facade already has a HOLE here (wallMinusY), so this builds the void itself. A flat
      // cavity decal on the facade plane fills that hole again and kills the depth — the recess
      // needs its own back wall, floor, soffit and reveal jambs to read as carved out of the mass.
      decalY(out, by, nrm, logX-logW/2, logX+logW/2, base, top, 'body', -0.30, brickTex('reclaimed'), false, 0.0);
      for(const sgn of [-1,1]){
        const jx = logX + sgn*logW/2;
        quad(out, [jx, yv, base],[jx, by, base],[jx, by, top],[jx, yv, top], 'body', sgn<0?0.05:-0.55, brickTex('reclaimed'));
      }
      quad(out, [logX-logW/2, yv, top],[logX+logW/2, yv, top],
                [logX+logW/2, by, top],[logX-logW/2, by, top], 'band', -0.70);
      slab(out, [[logX-logW/2, by],[logX+logW/2, by],[logX+logW/2, yv],[logX-logW/2, yv]], base+0.02, 'band', -0.10);
      flatDoor(out,'y', by, nrm, logX-logW*0.22, base+0.05, 1.05, 2.28, 'iron', b, true);
      open(out,'y', by, nrm, logX+logW*0.26, base+0.72, Math.min(1.2,logW*0.42), 1.62, b, litFn(u.i,u.storey,9), blinds[wins+1]);
      // planting on the loggia rail — the one place a loft flat gardens
      if(b.windowBoxes){
        const ry2 = yv - nrm*0.30;
        boxSolid(out, logX-logW/2+0.10, logX-logW/2+0.92, ry2-0.16, ry2+0.16, base+1.02, base+1.24, 'band', null, 0.12, 0.2);
        boxSolid(out, logX-logW/2+0.14, logX-logW/2+0.88, ry2-0.12, ry2+0.12, base+1.24, base+1.44, 'plant', plantTex(), 0.05, 0.15);
      }
      // rail set back in the reveal
      const ry = yv - nrm*0.22;
      boxSolid(out, logX-logW/2+0.04, logX+logW/2-0.04, ry-0.035, ry+0.035, base+1.02, base+1.09, 'iron', null, 0.45);
      for(let q=0;q<=5;q++){ const px=logX-logW/2+0.06+(logW-0.12)*q/5;
        boxSolid(out, px-0.022, px+0.022, ry-0.022, ry+0.022, base+0.06, base+1.04, 'iron', null, 0.2); }
    }
    // the run of openings, skipping the loggia's slot
    for(let q=0;q<wins;q++){
      const c = u.x0 + u.w*(q+0.5)/wins;
      if(loggiaHere && Math.abs(c-logX) < logW/2+0.55) continue;
      if(bay && Math.abs(c-bay.c) < bay.w/2+0.45) continue;
      const ww = lux ? Math.min(1.55, u.w/wins*0.60) : Math.min(1.85, u.w/wins*0.72);
      const sz = lux ? base+0.62 : base+0.95, wh = lux ? b.storeyH-1.42 : 1.35;
      open(out,'y', yv, nrm, c, sz, ww, wh, b, litFn(u.i,u.storey,q), blinds[q]);
      // a window air conditioner — the walk-up's most honest ornament
      if(q===acAt){
        boxSolid(out, c-0.33, c+0.33, Math.min(yv, yv+nrm*0.36), Math.max(yv, yv+nrm*0.36),
                 sz-0.04, sz+0.40, 'metal', null, 0.06);
        for(let g=0;g<4;g++) decalY(out, yv+nrm*0.36, nrm, c-0.25, c+0.25,
          sz+0.04+g*0.085, sz+0.075+g*0.085, 'iron', -0.35, null, true, 0.02);
      }
      // a planted window box
      if(q===boxAt){
        boxSolid(out, c-ww/2+0.05, c+ww/2-0.05, Math.min(yv+nrm*0.03, yv+nrm*0.27), Math.max(yv+nrm*0.03, yv+nrm*0.27),
                 sz-0.30, sz-0.06, lux?'band':'metal', null, 0.14, 0.2);
        boxSolid(out, c-ww/2+0.09, c+ww/2-0.09, Math.min(yv+nrm*0.06, yv+nrm*0.24), Math.max(yv+nrm*0.06, yv+nrm*0.24),
                 sz-0.08, sz+0.10, 'plant', plantTex(), 0.05, 0.15);
      }
    }
    // end-wall opening on the last column
    if(u.endWall){
      const xv=b.x0+b.Wd, mid=(u.y0+u.y1)/2;
      if(lux) industrial(out,'x', xv, 1, mid, base+0.62, 1.35, b.storeyH-1.42, b, litFn(u.i,u.storey,7), blinds[wins]);
      else    slider(out,'x', xv, 1, mid, base+0.95, 1.55, 1.35, b, litFn(u.i,u.storey,7), blinds[wins]);
    }
  }

  // ---- the shell: slab bands (basic) or brick piers (luxury) ----------------
  function buildShell(out, b, tex, logs){
    const x0=b.x0, x1=b.x0+b.Wd, yR=-b.Ln/2, yF=b.Ln/2, lux=b.tier==='luxury';
    // any band whose height crosses the entry opening must step round the core, or it shows
    // through the hole as a bar across the doorway
    const EH=entryHole(b), skip=[x0-0.20, b.coreX1];
    const crossesEntry=(z0,z1)=> !(z1<=EH.z0 || z0>=EH.z1);
    wallMinusY(out, x0,x1, yF,  1, 0, b.topZ, 'body', tex, 0, (logs||[]).filter(L=>L.side==='front'));
    wallMinusY(out, x0,x1, yR, -1, 0, b.topZ, 'body', tex, 0, (logs||[]).filter(L=>L.side==='rear'));
    wall(out, x0,yF, x0,yR, 0, b.topZ, 'body', tex);
    wall(out, x1,yR, x1,yF, 0, b.topZ, 'body', tex);

    // plinth
    if(lux) boxSolid(out, x0-0.06,x1+0.06, yR-0.06, yF+0.06, 0, b.fH, 'board', boardTex(), -0.12, 0.2);
    else    boxSolid(out, x0-0.08,x1+0.08, yR-0.08, yF+0.08, 0, b.fH, 'conc', null, -0.15, 0.2);

    if(!lux){
      // BASIC: every floor slab is expressed as a continuous concrete band wrapping the block,
      // brick infill sitting between. This is the horizontal read that separates the walk-up from
      // the terrace's vertical bays.
      for(let s=0;s<=b.storeys;s++){
        const z = b.fH + s*b.storeyH;
        if(s===b.storeys) continue;
        bandRing(out, x0-0.11,x1+0.11, yR-0.11, yF+0.11, 0.55, z-0.30, z+0.06, 'band', null, 0.30, 0.35,
                 crossesEntry(z-0.30, z+0.06) ? skip : null);
      }
      // thin metal fascia at the head — no cornice, no coping
      bandRing(out, x0-0.13,x1+0.13, yR-0.13, yF+0.13, 0.5, b.topZ-0.10, b.topZ+b.fasciaH, 'metal', null, 0.34, 0.4);
      slab(out, [[x0,yF],[x1,yF],[x1,yR],[x0,yR]], b.topZ-0.03, 'roof', 0.72, membraneTex());
    } else {
      // LUXURY: brick piers on the unit party lines, a deep datum band at the head of the ground
      // storey, and a planted parapet
      for(const u of b.bays){ if(u.storey!==0) continue;
        for(const px of [u.x0]){
          if(px<=x0+0.02) continue;
          boxSolid(out, px-0.17, px+0.17, yF-0.10, yF+0.14, b.fH, b.topZ-0.20, 'body', tex, 0.30);
          boxSolid(out, px-0.17, px+0.17, yR-0.14, yR+0.10, b.fH, b.topZ-0.20, 'body', tex, 0.30);
        }
      }
      const dz = b.fH + b.storeyH;
      bandRing(out, x0-0.12,x1+0.12, yR-0.12, yF+0.12, 0.52, dz-0.34, dz-0.04, 'band', renderTex(), 0.26, 0.3,
               crossesEntry(dz-0.34, dz-0.04) ? skip : null);
      bandRing(out, x0-0.06,x1+0.06, yR-0.06, yF+0.06, 0.44, b.topZ, b.topZ+b.fasciaH, 'body', tex, 0.12, 0.2);
      bandRing(out, x0-0.10,x1+0.10, yR-0.10, yF+0.10, 0.50, b.topZ+b.fasciaH, b.topZ+b.fasciaH+0.10, 'band', null, 0.30, 0.4);
      slab(out, [[x0,yF],[x1,yF],[x1,yR],[x0,yR]], b.topZ-0.03, 'roof', 0.72, membraneTex());
      // planting troughs along the parapet
      for(let q=0;q<Math.max(3,Math.round(b.Wd/3.2));q++){
        const pw=Math.min(2.4, b.Wd/Math.max(3,Math.round(b.Wd/3.2))-0.5);
        const pcx = x0+0.9 + q*(b.Wd-1.8)/Math.max(1,Math.round(b.Wd/3.2)-1);
        if(pcx+pw/2 > x1-0.5) continue;
        boxSolid(out, pcx-pw/2, pcx+pw/2, yF-1.14, yF-0.52, b.topZ, b.topZ+0.56, 'band', null, 0.16, 0.3);
        boxSolid(out, pcx-pw/2+0.07, pcx+pw/2-0.07, yF-1.07, yF-0.59, b.topZ+0.56, b.topZ+0.86, 'plant', plantTex(), 0.05, 0.2);
      }
    }
  }

  // ---- the core: lobby, stair, lift, laundry, lockers ----------------------
  function buildCore(out, b, tex, litFn){
    const lux=b.tier==='luxury', x0=b.x0, cx1=b.coreX1, yR=-b.Ln/2, yF=b.Ln/2;
    // The core is a solid forward mass and the entry is COMPOSED ON its front plane. A real recess
    // is geometrically honest, but a 1 m reveal at 45deg hides its own back wall, so the doorway's
    // read came and went with the facing. Layered decals plus a shadow reveal read from all eight.
    const proj = lux?0.42:0.30;
    const coreTop = b.topZ + (lux?b.fasciaH:0);
    const cbias = lux?0.05:0.18, ctex = lux?tex:null;
    const EH = entryHole(b), ex=EH.ex, pw=EH.pw, ph=EH.ph;
    boxSolid(out, x0-proj, cx1, yF, yF+proj, 0, coreTop, 'accent', ctex, cbias);
    const by = yF + proj;                                   // the plane the entry is composed on
    decalY(out, by, 1, ex-pw/2-0.12, ex+pw/2+0.12, 0, ph+0.14, 'accent', -0.95, null, false, 0.02);

    if(lux){
      // a bronze portal FRAME round the opening — four bars, not a filled panel, or the surround
      // vanishes under whatever is drawn on top of it
      const fw=0.20;
      for(const [a0,a1,z0b,z1b] of [
        [ex-pw/2-fw, ex+pw/2+fw, ph, ph+fw],
        [ex-pw/2-fw, ex-pw/2,    0,   ph+fw],
        [ex+pw/2,    ex+pw/2+fw, 0,   ph+fw]])
        decalY(out, yF+proj, 1, a0, a1, z0b, z1b, 'bronze', 0.55, null, false, 0.03);
      const dcx = ex-pw/2+0.95;
      decalY(out, by, 1, dcx-0.66, dcx+0.66, 0.06, 2.52, 'lobby', 0.9, null, true, 0.07);       // lit lobby beyond
      decalY(out, by, 1, dcx-0.62, dcx+0.62, 0.09, 2.48, 'bronze', 0.30, null, true, 0.075);
      decalY(out, by, 1, dcx-0.50, dcx+0.50, 0.42, 2.34, 'lobby', 1.6, null, true, 0.08);       // the glazed leaf
      decalY(out, by, 1, dcx+0.44, dcx+0.52, 0.98, 1.62, 'bronze', 1.0, null, true, 0.09);      // the pull
      decalY(out, by, 1, ex+0.28, ex+pw/2-0.22, 0.10, 2.56, 'lobby', 0.7, null, true, 0.07);    // sidelight
      decalY(out, by, 1, ex-pw/2+0.22, ex+pw/2-0.22, 2.74, ph-0.22, 'lobby', 0.5, null, true, 0.07);
      for(let q=1;q<4;q++){ const mx=ex-pw/2+0.22+(pw-0.44)*q/4;
        decalY(out, by, 1, mx-0.03, mx+0.03, 2.74, ph-0.22, 'bronze', 0.45, null, true, 0.08); }
      boxSolid(out, ex-pw/2-0.20, ex+pw/2+0.20, yF+proj, yF+proj+0.30, ph+fw, ph+fw+0.16, 'bronze', null, 0.42);
      boxSolid(out, ex-2.2, ex+2.2, yF+proj+0.30, yF+proj+1.5, 0, Math.max(0.10,b.fH), 'board', boardTex(), -0.05, 0.15);
    } else {
      decalY(out, by, 1, ex-1.24, ex+0.02, 0.06, 2.40, 'lobby', 0.8, null, true, 0.07);        // lit lobby beyond
      decalY(out, by, 1, ex-1.20, ex-0.02, 0.09, 2.36, 'metal', 0.35, null, true, 0.075);
      decalY(out, by, 1, ex-1.08, ex-0.14, 0.38, 2.22, 'lobby', 1.5, null, true, 0.08);        // the glazed leaf
      decalY(out, by, 1, ex-0.28, ex-0.20, 0.94, 1.54, 'metal', 1.1, null, true, 0.09);        // the pull
      decalY(out, by, 1, ex+0.16, ex+pw/2-0.14, 0.12, 2.42, 'lobby', 0.6, null, true, 0.07);
      if(b.mailwall) decalY(out, by, 1, ex-pw/2+0.16, ex-1.26, 0.85, 1.95, 'metal', 0.55, null, true, 0.06);
      boxSolid(out, ex-pw/2-0.42, ex+pw/2+0.42, yF+proj, yF+proj+1.35, ph, ph+0.16, 'metal', null, 0.40);
      for(const sgn of [-1,1]) boxSolid(out, ex+sgn*(pw/2+0.30)-0.05, ex+sgn*(pw/2+0.30)+0.05, yF+proj+1.16, yF+proj+1.26, 0, ph, 'metal', null, 0.25);
      boxSolid(out, ex-1.9, ex+1.9, yF+proj+0.10, yF+proj+1.30, 0, Math.max(0.08,b.fH), 'conc', null, -0.05, 0.15);
    }
    // lift overrun on the roof — the honest sign the block has an elevator
    const lz = b.topZ + (lux?b.fasciaH+0.10:b.fasciaH);
    boxSolid(out, x0+0.55, x0+2.95, -0.9, 1.5, lz, lz+1.85, lux?'body':'band', lux?tex:null, 0.08);
    boxSolid(out, x0+0.46, x0+3.04, -1.0, 1.6, lz+1.85, lz+2.00, 'metal', null, 0.42);
    // stair bulkhead / head of the internal flight
    if(lux){
      boxSolid(out, x0+3.20, x0+5.30, -1.6, 0.9, lz, lz+2.30, 'accent', tex, 0.05);
      boxSolid(out, x0+3.12, x0+5.38, -1.7, 1.0, lz+2.30, lz+2.44, 'metal', null, 0.42);
      // glazed stair slot on the core's street face
      const sx=(x0+cx1)/2;
      for(let s=0;s<b.storeys;s++){
        const z=b.fH+s*b.storeyH;
        decalY(out, yF+proj, 1, x0+0.55, x0+1.95, z+0.35, z+b.storeyH-0.30, 'glass', b.night?1.5:0.0, null, true, 0.07);
        decalY(out, yF+proj, 1, x0+0.55, x0+1.95, z+0.35, z+b.storeyH-0.30, 'iron', 0.3, null, true, 0.075);
        decalY(out, yF+proj, 1, x0+1.20, x0+1.28, z+0.35, z+b.storeyH-0.30, 'iron', 0.4, null, true, 0.08);
      }
    }
    // roof deck (luxury): pergola over the deck behind the parapet
    if(lux && b.roofDeck){
      const px0=x0+6.2, px1=Math.min(x0+b.Wd-1.2, px0+6.4);
      if(px1>px0+2.0){
        slab(out, [[px0,yF-1.0],[px1,yF-1.0],[px1,yF-5.2],[px0,yF-5.2]], b.topZ+0.02, 'accent', 0.35, boardTex());
        for(const qx of [px0+0.2, px1-0.2]) for(const qy of [yF-1.3, yF-4.9]){
          boxSolid(out, qx-0.08, qx+0.08, qy-0.08, qy+0.08, b.topZ, b.topZ+2.35, 'accent', null, 0.15); }
        for(let q=0;q<=7;q++){ const yy=yF-1.3-(3.6)*q/7;
          boxSolid(out, px0+0.16, px1-0.16, yy-0.05, yy+0.05, b.topZ+2.28, b.topZ+2.38, 'accent', null, 0.3); }
      }
    }
    // roof furniture (basic): vent stacks + condenser units, the walk-up's honest kit
    if(!lux){
      const yR2=-b.Ln/2, yF2=b.Ln/2, rx0=b.x0, rx1=b.x0+b.Wd;
      bandRing(out, rx0+0.55, rx1-0.55, yR2+0.55, yF2-0.55, 0.14, b.topZ-0.03, b.topZ+0.14, 'band', null, 0.24, 0.4);
      const dy=yR2+b.Ln*0.32;                                    // capped duct run along the block
      boxSolid(out, rx0+1.5, rx1-1.5, dy-0.30, dy+0.30, b.topZ, b.topZ+0.44, 'metal', null, 0.10);
      boxSolid(out, rx0+1.4, rx1-1.4, dy-0.36, dy+0.36, b.topZ+0.44, b.topZ+0.50, 'metal', null, 0.40);
      for(let q=0;q<Math.max(2,Math.round(b.Wd/5.2));q++){        // duct supports
        const sx=rx0+2.0+q*(b.Wd-4.0)/Math.max(1,Math.round(b.Wd/5.2)-1);
        boxSolid(out, sx-0.07, sx+0.07, dy-0.24, dy-0.16, b.topZ-0.02, b.topZ, 'iron', null, 0.1); }
      boxSolid(out, rx1-3.1, rx1-2.0, yF2-2.2, yF2-1.1, b.topZ, b.topZ+0.26, 'band', null, 0.18, 0.35);
      // one condenser per couple of flats, which is what a walk-up roof actually carries
      const nCond = Math.max(2, Math.min(6, Math.ceil(b.n/2)));
      for(let q=0;q<nCond; q++){
        const vx = b.coreX1 + 1.2 + q*(b.Wd-b.coreW-2.4)/Math.max(1,nCond-1);
        boxSolid(out, vx-0.17, vx+0.17, -0.5, -0.16, b.topZ, b.topZ+0.62, 'iron', null, 0.28);
        boxSolid(out, vx+0.5, vx+1.45, 0.6, 1.55, b.topZ, b.topZ+0.55, 'metal', null, 0.12);
        boxSolid(out, vx+0.56, vx+1.39, 0.66, 1.49, b.topZ+0.55, b.topZ+0.60, 'iron', null, -0.3);
      }
    }
    // external steel stair tower (basic): the circulation is outside, and it shows
    if(!lux && b.stairTower==='external'){
      const tx1=x0-proj, tx0=tx1-2.65, n=b.storeys;
      boxSolid(out, tx0-0.06, tx0+0.06, -1.85, -1.73, 0, b.topZ, 'metal', null, 0.2);   // corner posts
      boxSolid(out, tx0-0.06, tx0+0.06,  1.73,  1.85, 0, b.topZ, 'metal', null, 0.2);
      for(let s=0;s<n;s++){
        const z0=b.fH+s*b.storeyH, z1=z0+b.storeyH;
        // landing at each floor, then a straight flight up the tower
        boxSolid(out, tx0, tx1, 0.55, 1.85, z0-0.10, z0, 'conc', treadTex(), 0.05, 0.35);
        const steps=12, run=2.30/steps, rise=b.storeyH/steps;
        for(let i=0;i<steps;i++){
          const yy=0.50-i*run, zz=z0+rise*(i+1);
          boxSolid(out, tx0+0.20, tx1-0.20, yy-run, yy, zz-0.08, zz, 'conc', treadTex(), 0.05, 0.4);
        }
        if(s===n-1) boxSolid(out, tx0, tx1, -1.85, -0.60, z1-0.10, z1, 'conc', treadTex(), 0.05, 0.35);
        // mesh balustrades on the two long sides
        for(const xv of [tx0, tx1]){
          decalX(out, xv, xv===tx0?-1:1, -1.85, 1.85, z0+0.10, z0+1.14, 'metal', 0.1, meshTex(), false, 0.03);
          boxSolid(out, xv-0.04, xv+0.04, -1.85, 1.85, z0+1.14, z0+1.21, 'metal', null, 0.42);
        }
        decalY(out, -1.85, -1, tx0, tx1, z0+0.10, z0+1.14, 'metal', 0.1, meshTex(), false, 0.03);
      }
      // the bridge from each landing into the corridor
      for(let s=0;s<n;s++){
        const z=b.fH+s*b.storeyH;
        boxSolid(out, tx1, x0+0.05, 0.55, 1.85, z-0.10, z, 'conc', treadTex(), 0.05, 0.35);
        if(s>0) flatDoor(out,'x', x0, -1, 1.20, z, 1.05, 2.20, 'metal', b, true);
      }
    }
    // parking bay + bike rack on the entry side
    if(b.parking){
      const py=yF+proj+ (lux?1.9:1.7);
      slab(out, [[x0-1.2,py],[x0+b.Wd*0.42,py],[x0+b.Wd*0.42,py+2.4],[x0-1.2,py+2.4]], 0.02, 'conc', -0.28);
      for(let q=0;q<3;q++){ const lx=x0-0.9+q*(b.Wd*0.42+0.3)/3;
        boxSolid(out, lx-0.03, lx+0.03, py+0.15, py+2.25, 0.02, 0.05, 'trim', null, 0.5); }
      const rx=x0+b.Wd*0.52;
      for(let q=0;q<3;q++){ const bx=rx+q*0.55;
        boxSolid(out, bx-0.03, bx+0.03, py+0.5, py+0.56, 0.02, 0.72, 'metal', null, 0.3);
        boxSolid(out, bx-0.03, bx+0.03, py+1.1, py+1.16, 0.02, 0.72, 'metal', null, 0.3);
        boxSolid(out, bx-0.035, bx+0.035, py+0.5, py+1.16, 0.68, 0.74, 'metal', null, 0.45); }
    }
  }

  // ---- the lived-in pass: the kit that makes a block look occupied rather than modelled ----
  function buildDetail(out, b, tex){
    const lux=b.tier==='luxury', x0=b.x0, x1=b.x0+b.Wd, yR=-b.Ln/2, yF=b.Ln/2;
    const proj=lux?0.42:0.30, ex=(x0+b.coreX1)/2, mat=lux?'iron':'metal';

    // --- rainwater goods. A four-storey wall with no downpipe reads as a model, not a building.
    if(b.rainwater){
      for(const [px,py,sy] of [[x0+0.26,yF,1],[x1-0.26,yF,1],[x0+0.26,yR,-1],[x1-0.26,yR,-1]]){
        const a=py+(sy>0?0.02:-0.17), c2=py+(sy>0?0.17:-0.02);
        boxSolid(out, px-0.095, px+0.095, a, c2, 0.20, b.topZ-0.30, mat, null, lux?0.55:-0.40);
        boxSolid(out, px-0.14, px+0.14, py+(sy>0?0.0:-0.26), py+(sy>0?0.26:0.0), b.topZ-0.34, b.topZ-0.06, mat, null, 0.32); // hopper head
        boxSolid(out, px-0.105, px+0.105, py+(sy>0?0.0:-0.24), py+(sy>0?0.24:0.0), 0.02, 0.22, mat, null, 0.18);            // shoe
        for(let s=1;s<b.storeys;s++){ const z=b.fH+s*b.storeyH;    // pipe brackets on the band lines
          boxSolid(out, px-0.11, px+0.11, py+(sy>0?0.0:-0.20), py+(sy>0?0.20:0.0), z-0.06, z+0.02, mat, null, 0.36); }
      }
    }

    // --- entry: a pair of lamps that actually light at night, and a number plate
    if(b.entryLamps){
      for(const sgn of [-1,1]){
        // a compact wall lantern. A gooseneck arm's silhouette is not legible at 32 px/m — it read
        // as a stray bracket beside the door. The glow is what carries.
        const lx=ex+sgn*(lux?2.35:2.05), fy=yF+proj;
        boxSolid(out, lx-0.13, lx+0.13, fy, fy+0.16, 2.24, 2.66, lux?'bronze':'metal', null, 0.24);
        decalY(out, fy+0.16, 1, lx-0.10, lx+0.10, 2.30, 2.58, b.night?'lobby':'trim', b.night?1.9:0.70, null, true, 0.05);
        boxSolid(out, lx-0.17, lx+0.17, fy-0.02, fy+0.20, 2.66, 2.74, lux?'bronze':'metal', null, 0.42);
      }
      // relief number plate beside the door
      if(b.signage) decalY(out, yF+proj, 1, ex+(lux?1.15:1.05), ex+(lux?1.62:1.48), 2.05, 2.36,
        lux?'bronze':'trim', lux?0.85:1.0, null, true, 0.05);
    }

    // --- meter cabinet and conduit riser: the basic tier wears its services outside
    if(b.meters){
      const mx=x0+0.62, fy=yF+proj;
      boxSolid(out, mx, mx+0.86, fy, fy+0.17, 0.72, 1.78, 'metal', null, 0.14);
      decalY(out, fy+0.17, 1, mx+0.06, mx+0.80, 0.80, 1.70, 'metal', -0.85, null, true, 0.03);
      boxSolid(out, mx+0.94, mx+1.02, fy, fy+0.09, 0.30, b.fH+b.storeyH*0.55, 'iron', null, 0.18);
      boxSolid(out, mx+0.34, mx+0.44, fy, fy+0.09, 0.14, 0.72, 'iron', null, 0.18);
    }

    // --- cannery tie plates: the pattress plates that hold a loft's floors to its walls
    if(b.tiePlates){
      for(let s=1;s<=b.storeys;s++){
        const z=b.fH + s*b.storeyH - 0.42;
        for(const [py,nr] of [[yF,1],[yR,-1]]){
          for(let q=0;q<Math.max(3, Math.round(b.Wd/2.6)); q++){
            const px = x0+1.1 + q*(b.Wd-2.2)/Math.max(1, Math.round(b.Wd/2.6)-1);
            if(px>x1-0.8) continue;
            decalY(out, py, nr, px-0.12, px+0.12, z-0.12, z+0.12, 'metal', 0.65, null, true, 0.05);
            decalY(out, py, nr, px-0.05, px+0.05, z-0.05, z+0.05, 'iron', 0.20, null, true, 0.06);
          }
        }
      }
    }

    // --- a blocked-up loading bay: the relic that tells you this was a cannery. The infill has to
    // read LIGHTER than the surrounding wall — newer brick in an old opening — or the bay vanishes
    // and the hoist beam above it looks like a bracket bolted to nothing.
    if(lux && b.hoist){
      const bx = b.coreX1 + Math.min(b.unitW*0.5, 3.4), bw=2.5, bh=3.1, fy=yF;
      decalY(out, fy, 1, bx-bw/2-0.10, bx+bw/2+0.10, 0.02, bh+0.30, 'body', -0.80, null, false, 0.02);
      // parged infill: a half-step-lighter brick is indistinguishable from the wall it sits in, so
      // the filled opening is rendered, the way a blocked-up bay actually gets closed
      decalY(out, fy, 1, bx-bw/2, bx+bw/2, 0.16, bh, 'band', 0.10, renderTex(), false, 0.03);
      decalY(out, fy, 1, bx-bw/2-0.14, bx+bw/2+0.14, bh, bh+0.24, 'band', 0.30, null, false, 0.035);  // lintel
      decalY(out, fy, 1, bx-bw/2-0.12, bx+bw/2+0.12, 0.02, 0.18, 'band', 0.24, null, false, 0.035);   // threshold
      for(const sg of [-1,1]) decalY(out, fy, 1, bx+sg*bw/2-0.05, bx+sg*bw/2+0.05, 0.16, bh, 'iron', 0.30, null, false, 0.04);
      // the hoist beam sits directly over the bay it served. Three storeys up it read as a dark
      // wedge bolted to nothing — the beam and the blocked opening have to compose as one motif.
      const hz = bh + 0.95;
      boxSolid(out, bx-0.14, bx+0.14, fy, fy+1.35, hz, hz+0.26, 'iron', null, 0.22);
      boxSolid(out, bx-0.22, bx+0.22, fy+1.20, fy+1.42, hz-0.10, hz+0.32, 'iron', null, 0.30);
      boxSolid(out, bx-0.05, bx+0.05, fy+1.26, fy+1.36, hz-0.52, hz-0.10, 'iron', null, 0.14);
      boxSolid(out, bx-0.13, bx+0.13, fy+1.22, fy+1.40, hz-0.76, hz-0.52, 'iron', null, 0.26);
      // and the pair of bolted plates that carried it
      for(const sg of [-1,1]) decalY(out, fy, 1, bx+sg*0.55-0.10, bx+sg*0.55+0.10, hz-0.16, hz+0.04, 'metal', 0.60, null, true, 0.05);
    }

    // --- roof kit
    if(b.roofKit){
      if(!lux){
        // a fire ladder up to the roof, and the dishes every walk-up accumulates
        const lx = x0 + 1.35, tz = b.fH + (b.storeys-1)*b.storeyH, ly = yR - 0.18;
        for(const sg of [-1,1]) boxSolid(out, lx+sg*0.24-0.04, lx+sg*0.24+0.04, ly-0.10, ly, tz, b.topZ+1.00, 'metal', null, 0.24);
        for(let r=0;r<Math.round((b.topZ+0.95-tz)/0.34); r++)
          boxSolid(out, lx-0.24, lx+0.24, ly-0.08, ly-0.02, tz+0.20+r*0.34, tz+0.25+r*0.34, 'metal', null, 0.34);
        boxSolid(out, lx-0.30, lx+0.30, ly-0.16, ly+0.04, b.topZ+1.00, b.topZ+1.08, 'metal', null, 0.40);
        for(let q=0;q<3;q++){
          const dx = x0 + b.Wd*(0.28+q*0.24), dy = yF-1.25;
          boxSolid(out, dx-0.05, dx+0.05, dy, dy+0.10, b.topZ+0.06, b.topZ+0.50, 'metal', null, 0.20);
          // a squat drum reads as a dish at 32 px/m; an edge-on parabola reads as a bird
          boxSolid(out, dx-0.29, dx+0.29, dy+0.06, dy+0.22, b.topZ+0.44, b.topZ+1.00, 'trim', null, 0.30);
          boxSolid(out, dx-0.24, dx+0.24, dy+0.22, dy+0.30, b.topZ+0.50, b.topZ+0.94, 'trim', null, -0.35);
          boxSolid(out, dx-0.04, dx+0.04, dy+0.28, dy+0.52, b.topZ+0.66, b.topZ+0.76, 'iron', null, 0.35);
        }
      } else {
        // a riveted water tank on legs: the loft roof's landmark
        const tx = x0 + b.Wd*0.72, ty = yR + b.Ln*0.30, tw=2.3, td=2.0, th=2.1;
        for(const sx of [-1,1]) for(const sy of [-1,1])
          boxSolid(out, tx+sx*(tw/2-0.16)-0.075, tx+sx*(tw/2-0.16)+0.075,
                        ty+sy*(td/2-0.16)-0.075, ty+sy*(td/2-0.16)+0.075, b.topZ, b.topZ+0.95, 'iron', null, 0.18);
        boxSolid(out, tx-tw/2, tx+tw/2, ty-td/2, ty+td/2, b.topZ+0.95, b.topZ+0.95+th, 'metal', null, 0.10);
        boxSolid(out, tx-tw/2-0.05, tx+tw/2+0.05, ty-td/2-0.05, ty+td/2+0.05, b.topZ+0.95+th, b.topZ+1.06+th, 'iron', null, 0.36);
        for(let r=0;r<3;r++) decalY(out, ty-td/2, -1, tx-tw/2+0.05, tx+tw/2-0.05,
          b.topZ+1.25+r*0.60, b.topZ+1.33+r*0.60, 'iron', 0.30, null, true, 0.03);   // hoop bands
      }
      // relief lettering on the parapet, the block's name
      if(b.signage){
        const lz = b.topZ + (lux? b.fasciaH*0.30 : b.fasciaH*0.22);
        const n = Math.max(5, Math.min(11, Math.round(b.Wd/2.2)));
        for(let q=0;q<n;q++){
          const cx2 = x0 + b.Wd*0.30 + q*(b.Wd*0.44)/(n-1);
          decalY(out, yF+0.15, 1, cx2-0.105, cx2+0.105, lz, lz+(lux?0.34:0.26), lux?'bronze':'iron', lux?0.85:0.10, null, true, 0.04);
        }
      }
    }

    // --- ground: a kerb and planting strip along the facade, and a bin enclosure by the parking
    if(b.kerb){
      const ky=yF+proj+0.10;
      boxSolid(out, x1-b.Wd*0.46, x1-0.20, ky+0.62, ky+0.74, 0.02, 0.16, 'conc', null, 0.20);
      boxSolid(out, x1-b.Wd*0.46, x1-0.20, ky, ky+0.62, 0.02, 0.13, 'plant', plantTex(), 0.0, 0.15);
      for(let q=0;q<3;q++){   // a few clipped shrubs so the strip is not a flat green bar
        const sx = x1-b.Wd*0.42 + q*(b.Wd*0.34)/2;
        boxSolid(out, sx-0.26, sx+0.26, ky+0.10, ky+0.54, 0.13, 0.58, 'plant', plantTex(), 0.10, 0.22);
      }
    }
    if(b.binStore && b.parking){
      const bx=x0+b.Wd*0.44, by=yF+proj+(lux?2.6:2.4);
      boxSolid(out, bx, bx+2.7, by, by+1.45, 0.02, 1.35, lux?'accent':'metal', lux?tex:null, 0.06);
      boxSolid(out, bx-0.05, bx+2.75, by-0.05, by+1.50, 1.35, 1.44, mat, null, 0.34);
      decalY(out, by+1.45, 1, bx+0.14, bx+1.26, 0.10, 1.22, mat, -0.55, null, true, 0.04);   // the two gates
      decalY(out, by+1.45, 1, bx+1.42, bx+2.54, 0.10, 1.22, mat, -0.55, null, true, 0.04);
      for(const gx of [bx+0.70, bx+1.98]) decalY(out, by+1.45, 1, gx-0.05, gx+0.05, 0.58, 0.74, mat, 0.95, null, true, 0.06);
    }
  }

  function build(b){
    const out=[];
    const tex = brickTex(b.tier==='luxury'?'reclaimed':'std');
    const rnd = mulberry32(7717 + b.n*29 + Math.round(b.lit*100));
    const litTable={};
    const litFn=(i,s,q)=>{ if(!b.night) return false; const k=i+'|'+s+'|'+q;
      if(litTable[k]==null) litTable[k] = rnd() < b.lit; return litTable[k]; };
    const logs = loggiaRects(b);
    buildShell(out, b, tex, logs);
    for(const u of b.bays) buildUnitFacade(out, b, u, litFn);
    buildCore(out, b, tex, litFn);
    buildDetail(out, b, tex);
    return out;
  }

  // ---- post pass + RGBA ----------------------------------------------------
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
      const rnd=mulberry32(3319|((b.Wd*13)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='body'||m==='accent') && rnd()<wx*0.06){ out[i]=rbuf[i][Math.max(0,Math.min(rbuf[i].length-1,ibuf[i]-1))]; }
        if((m==='band'||m==='conc'||m==='board') && rnd()<wx*0.06){ out[i]=mix(out[i], '#4d5646', 0.20+rnd()*0.16); }
        if(m==='roof' && rnd()<wx*0.03){ out[i]=mix(out[i], '#47543c', 0.24+rnd()*0.14); }
        if(m==='metal' && rnd()<wx*0.04){ out[i]=mix(out[i], '#6a4a2c', 0.18+rnd()*0.18); }   // rust streaks
      }
    }
    if(b.night){
      for(let y=1;y<H-1;y++) for(let x=1;x<W-1;x++){ const i=y*W+x;
        if(nbuf[i]==='glass'){ for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*W+(x+dx);
          if(out[j] && nbuf[j]!=='glass' && nbuf[j]!=='glassHi') out[j]=mix(out[j],'#f0c66a',0.26); } } }
    }
    for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(!out[i]) continue;
      let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
        if(nx>=0&&nx<W&&ny>=0&&ny<H&&out[ny*W+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; }
    }
    if(b.outline){
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const i=y*W+x; if(out[i]) continue;
        let touch=false;
        for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy;
          if(nx>=0&&nx<W&&ny>=0&&ny<H&&rbuf[ny*W+nx]){ touch=true; break; } }
        if(touch) out[i]=KEY;
      }
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
    return toRGBA(post(paint(build(b), {dir, elev:opts.elev}, makeMats(b)), b));
  }

  // ---- published metrics ---------------------------------------------------
  function dims(opts){
    const b=resolve(opts||{});
    return { tier:b.tier, units:b.n, storeys:b.storeys, perFloor:b.perFloor, columns:b.columns,
      Wd:r3(b.Wd), Ln:r3(b.Ln), topZ:r3(b.topZ), parapetZ:r3(b.parapetZ), fH:b.fH, storeyH:b.storeyH,
      storeyZ:b.storeyZ.map(r3), unitW:r3(b.unitW), unitD:r3(b.unitD), corridorW:r3(b.corridorW),
      coreW:r3(b.coreW), wallT:b.wallT, floorT:b.floorT,
      bays:b.bays.map(u=>({ unit:u.unit, storey:u.storey, side:u.side, col:u.col, beds:u.beds,
        x0:r3(u.x0), x1:r3(u.x1), y0:r3(u.y0), y1:r3(u.y1) })) };
  }

  // the interior rig measures NOTHING — it reads this
  function unitBox(b, u){
    const t=b.wallT, party=b.partyT;
    const isLast = (u.side==='front' ? u.col===b.frontCount-1 : u.col===b.rearCount-1);
    const sgn = u.side==='front' ? 1 : -1;
    const ix0 = u.x0 + party/2, ix1 = u.x1 - (isLast ? t : party/2);
    const yC  = (u.side==='front' ? b.cyF : b.cyR) + sgn*t/2;    // corridor face of the unit
    const yFa = (u.side==='front' ? b.Ln/2 - t : -b.Ln/2 + t);   // inside face of the facade
    const hallW = b.tier==='luxury' ? 1.85 : 1.70;
    return { ix0, ix1, w:ix1-ix0, yC, yFa, d:Math.abs(yFa-yC), sgn, isLast,
             serviceD: b.tier==='luxury' ? 3.90 : 3.50,
             corridorD: b.tier==='luxury' ? 1.30 : 1.15,
             hallW, entryX: ix1 - hallW/2 };
  }
  function shell(opts){
    const b=resolve(opts||{});
    const t=b.wallT, party=b.partyT, x0=b.x0, x1=b.x0+b.Wd, yR=-b.Ln/2, yF=b.Ln/2;
    const lastFront = b.frontCount-1, lastRear = b.rearCount-1;
    return {
      contract:'32 px = 1 m · origin ground-centre of the block footprint · +y street/front · +x along the corridor',
      tier:b.tier, wallT:t, partyWallT:party, floorT:b.floorT, ceilH:r3(b.storeyH-b.floorT),
      fH:b.fH, storeyH:b.storeyH, storeyZ:b.storeyZ.map(r3), roofZ:r3(b.topZ), parapetZ:r3(b.parapetZ),
      storeys:b.storeys, block:{ x0:r3(x0), x1:r3(x1), yRear:r3(yR), yFront:r3(yF) },
      // the corridor: the spine every unit door opens off, identical on every storey
      corridor:{ x0:r3(b.coreX1), x1:r3(x1-t), y0:r3(b.cyR), y1:r3(b.cyF), w:r3(b.corridorW) },
      // the core, sliced front to back: lobby at the street, stair + lift in the middle, laundry
      // and lockers at the rear. Upper storeys: landing, stair + lift, store.
      core:{ x0:r3(x0+t), x1:r3(b.coreX1-party/2),
        lobby:  { y0:r3(yF-4.6), y1:r3(yF-t), rooms:['mailboxes','seating'] },
        stair:  { x0:r3(x0+t+2.35), x1:r3(b.coreX1-party/2), y0:r3(-1.55), y1:r3(1.55) },
        lift:   { x0:r3(x0+t), x1:r3(x0+t+2.20), y0:r3(-1.15), y1:r3(1.25), doorSide:'east' },
        laundry:{ y0:r3(yR+t), y1:r3(yR+t+3.30) },
        lockers:{ y0:r3(yR+t+3.30), y1:r3(-1.75), present:b.lockers },
      },
      units: b.bays.map(u=>{
        const K=unitBox(b,u), L=loggiaOf(b,u);
        return { id:u.unit, storey:u.storey, side:u.side, col:u.col, beds:u.beds,
          interior:{ x0:r3(K.ix0), x1:r3(K.ix1), yCorridor:r3(K.yC), yFacade:r3(K.yFa),
                     w:r3(K.w), d:r3(K.d), inward:K.sgn },
          // the band dimensions the flat plan is measured from — the interior rig computes the room
          // program, never the geometry it sits in
          bands:{ serviceD:K.serviceD, corridorD:K.corridorD, hallW:K.hallW },
          // WHICH EDGES SEE DAYLIGHT — the interior rig needs this to keep headboards off glazing
          glazed:{ facade:true, end:K.isLast, corridor:false, party:false },
          openings:{
            entry:{ x:r3(K.entryX), y:r3(u.side==='front'? b.cyF : b.cyR),
                    clearW:b.tier==='luxury'?1.02:0.94, clearH:2.10,
                    kind:'flat_entry_door', fromCorridor:true },
            facadeWindows: u.beds>=3?4:(u.beds===2?3:2),
            endWindows: K.isLast?1:0,
            loggia: L?1:0,
            loggiaBox: L ? { x0:r3(L.x0), x1:r3(L.x1), depth:r3(L.dep),
                             y:r3(L.y), inward:-L.nrm, z0:r3(L.z0) } : null,
          } };
      }),
    };
  }

  // the gameplay unit table the occupancy/schedule pass consumes
  function units(opts){
    const b=resolve(opts||{}), t=b.wallT, party=b.partyT;
    return b.bays.map(u=>{
      const iw=u.w-party, id=b.unitD-t-party/2;
      return { id:u.unit, storey:u.storey, side:u.side, tier:b.tier, beds:u.beds,
        // the room program — bath count, bedrooms actually built — is published by
        // Art/walkupUnitIsoRig.js. A corridor flat's second bathroom opens off its internal
        // corridor, not off a bedroom, so this rig makes no ensuite claim.
        ensuite: false,
        floorArea_m2: r3(iw*id), sleeps: u.beds===1?2:(u.beds===2?3:5),
        entry:{ from:'corridor', storey:u.storey, x:r3(unitBox(b,u).entryX),
                y:r3(u.side==='front'? b.cyF : b.cyR) },
        lift:true, stairs:true, walkUpFloors:u.storey,
        privateOutdoor: (b.tier==='luxury' && b.loggia!=='none' && u.storey>0) ? 'loggia' : 'none',
        sharedAccess: ['lobby','corridor','stair','lift'].concat(b.lockers?['laundry','lockers']:['laundry'])
                       .concat(b.tier==='luxury'&&b.roofDeck?['roof_deck']:[]) };
    });
  }

  function anchors(dir, opts){
    opts=opts||{}; const b=resolve(opts), B=camBasis({dir,elev:opts.elev});
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return {x:Math.round(v.sx),y:Math.round(v.sy)}; };
    const lux=b.tier==='luxury', proj=lux?0.42:0.30, ex=(b.x0+b.coreX1)/2, yF=b.Ln/2;
    const unitsA=b.bays.map(u=>{
      const base=b.fH+u.storey*b.storeyH, wins=u.beds>=3?4:(u.beds===2?3:2), out=[];
      for(let q=0;q<wins;q++) out.push(Object.assign({storey:u.storey, side:u.side},
        pj(u.x0+u.w*(q+0.5)/wins, u.facadeY, base+(lux?1.7:1.6))));
      const logW=Math.min(2.9,u.w*0.36), logX=u.x0+u.w*0.24;
      return { unit:u.unit, storey:u.storey, side:u.side, beds:u.beds,
        entry: pj(unitBox(b,u).entryX, u.side==='front'?b.cyF:b.cyR, base+1.05),
        windows: out,
        loggia: (lux && b.loggia!=='none' && u.storey>0) ? pj(logX, u.facadeY-u.facadeN*0.9, base+0.05) : null };
    });
    return {
      units: unitsA,
      entry:   pj(ex, yF+proj, lux?1.2:1.1),
      lobby:   pj(ex, yF-2.4, b.fH),
      mail:    b.mailwall ? pj(ex-1.3, yF+proj-0.58, b.fH+1.4) : null,
      stair:   pj(b.x0+3.9, 0, b.fH),
      lift:    pj(b.x0+1.4, 0.05, b.fH+1.1),
      liftOverrun: pj(b.x0+1.75, 0.3, b.topZ+(lux?b.fasciaH+2.0:b.fasciaH+1.85)),
      laundry: pj(b.x0+2.6, -b.Ln/2+1.9, b.fH),
      lockers: b.lockers ? pj(b.x0+2.6, -b.Ln/2+4.4, b.fH) : null,
      stairTower: (!lux && b.stairTower==='external') ? pj(b.x0-proj-1.35, 0, b.fH) : null,
      roofDeck: (lux && b.roofDeck) ? pj(b.x0+9.0, yF-3.1, b.topZ+0.02) : null,
      parking: b.parking ? pj(b.x0+1.0, yF+proj+(lux?3.1:2.9), 0.02) : null,
      vents: !lux ? b.bays.filter(u=>u.storey===0).map(u=>pj(u.xc, -0.33, b.topZ+0.62)) : [],
      Wd:r3(b.Wd), Ln:r3(b.Ln), topZ:r3(b.topZ),
    };
  }
  // ---- GAMEPLAY SIDECAR -----------------------------------------------------
  //   WalkupIso.gameplayAll(opts)   -> the whole sidecar, interiors merged when the room rig is loaded
  //   WalkupIso.gameplay(opts)      -> the exterior sections alone
  //
  // Generated, never edited. Three hashes are stamped by Art/_sidecarExport.js, one per renderer
  // that can drift (shell, room, props). If any moves, re-run the generator, never patch a number.
  //
  // THIS IS THE FILE THE TERRACE'S `_excluded` POINTED AT. The rowhouse sidecar says: elevator,
  // lobby, corridor, shared laundry and basement lockers belong to the walk-up block rig. They are
  // all PRESENT here, and ELEVATOR is the first one in the folder with real kinematics.
  function gameplay(opts){
    const o=opts||{}, b=resolve(o), sh=shell(o), us=units(o), lux=b.tier==='luxury';
    const C=sh.core, lift=C.lift, yF=b.Ln/2, yR=-b.Ln/2;
    const levelsAll=Array.from({length:b.storeys},(_,s)=>'storey_'+s);

    const UNITS = us.map((u,i)=>{
      const si=sh.units[i], L=loggiaOf(b, b.bays[i]);
      return { id:u.id, storey:u.storey, side:u.side, col:si.col, tier:u.tier,
        beds:u.beds, baths:null, ensuite:u.ensuite, storeys:1,
        floor_area_m2:u.floorArea_m2, resident_slots:u.sleeps,
        interior_box:si.interior, bands:si.bands, glazed:si.glazed,
        // a walk-up flat is SINGLE LEVEL. The terrace's per-unit private stair does not exist here:
        // a household's only vertical route is the shared core, which is why ELEVATOR matters.
        single_level:true, level:'storey_'+u.storey, level_z:sh.storeyZ[u.storey],
        entry:{ x:si.openings.entry.x, y:si.openings.entry.y, z:sh.storeyZ[u.storey],
                from:'corridor', kind:si.openings.entry.kind },
        party_walls:(si.col>0?1:0)+(si.glazed.end?0:1),
        private_outdoor:u.privateOutdoor,
        loggia: L ? { x0:r3(L.x0), x1:r3(L.x1), depth:r3(L.dep), y:r3(L.y), z:r3(L.z0) } : null,
        shared_access:u.sharedAccess,
        walk_up_floors:u.walkUpFloors, lift_served:true };
    });

    // every flat door off the corridor, plus the one street door into the lobby
    const THRESHOLD = us.map((u,i)=>{
      const op=sh.units[i].openings.entry;
      return { id:u.id+'.entry', unit:u.id, kind:'flat_entry', storey:u.storey,
        level:'storey_'+u.storey, from:'corridor_s'+u.storey, to:u.id+'_hall',
        axis:'x', plane:op.y, centre:op.x,
        clear_width_m:op.clearW, clear_height_m:op.clearH, sill_z:sh.storeyZ[u.storey],
        mechanism:'hinged_single',
        hinge_axis:{ x:r3(op.x-op.clearW/2), y:op.y, vertical:true },
        // a flat door opens INTO the flat, never into the corridor: a leaf swung into a 1.7 m
        // means of escape is a door that cannot legally exist and an NPC route that jams
        swing:{ outward:false, into:'flat', degrees:90,
          keep_clear:[[r3(op.x-op.clearW/2),r3(op.y-op.clearW*(u.side==='front'?1:-1))],
                      [r3(op.x+op.clearW/2),r3(op.y)]] },
        default_state:'shut', self_closing:true, leaf:op.kind };
    });
    THRESHOLD.push({ id:'block.street_entry', unit:null, kind:'street_entry', storey:0,
      level:'storey_0', from:'outside', to:'lobby_s0', axis:'x',
      plane:r3(yF), centre:r3((C.x0+C.x1)/2),
      clear_width_m:lux?1.80:1.55, clear_height_m:2.30, sill_z:b.fH,
      mechanism:lux?'bipart_slider':'hinged_pair',
      // a slider's collider is the leaf, which travels inside the wall line: no arc to sweep
      keep_clear: lux ? null : { rule:'leaf_swings_outward', arc:[[r3((C.x0+C.x1)/2-0.78),r3(yF)],[r3((C.x0+C.x1)/2+0.78),r3(yF+0.86)]] },
      swing: lux ? null : { outward:true, degrees:95 },
      default_state:'shut', secured:true, leaf:lux?'glazed_slider':'glazed_pair' });

    // ---- ELEVATOR. Present at last, and the reason this rig exists as its own phase.
    const carH = Math.min(2.30, sh.ceilH-0.12);
    const travel = r3(sh.storeyZ[b.storeys-1]-sh.storeyZ[0]);
    const ELEVATOR = [{ id:'core.lift', kind:'passenger', shared:true,
      shaft:{ polygon:[[lift.x0,lift.y0],[lift.x1,lift.y0],[lift.x1,lift.y1],[lift.x0,lift.y1]],
              base_z:r3(b.fH-1.10), head_z:r3(b.topZ+(lux?b.fasciaH+2.0:b.fasciaH+1.85)),
              pit_depth_m:1.10, overrun:'roof_mounted_motor_room' },
      car:{ inside:[[r3(lift.x0+0.12),r3(lift.y0+0.12)],[r3(lift.x1-0.12),r3(lift.y1-0.12)]],
            height_m:r3(carH), capacity_persons:lux?8:6,
            floor_z_at:levelsAll.map((lv,s)=>({ level:lv, z:sh.storeyZ[s] })) },
      doors:{ side:lift.doorSide, mechanism:'bipart_slider', clear_width_m:1.10,
              clear_height_m:2.05, opens_onto:'stairhall',
              // a lift door is not a hinged leaf: the collider is the panel inside the jamb, and
              // there is no arc. keep_clear is null for the same reason the station's slider is.
              keep_clear:null, default_state:'closed_until_called',
              dwell_s:4.0, open_s:1.6, close_s:2.0 },
      levels_served:levelsAll, travel_m:travel,
      speed_mps:0.63, accel_mps2:0.50,
      call_points:levelsAll.map((lv,s)=>({ level:lv, x:r3(lift.x1+0.62),
        y:r3((lift.y0+lift.y1)/2), z:sh.storeyZ[s],
        stand:[r3(lift.x1+0.90), r3((lift.y0+lift.y1)/2)] })),
      note:'One car, one shaft, every storey. A flat is single-level, so this and the core stair '+
           'are the only vertical routes in the building.' }];

    // ---- the shared rooms a terrace does not have
    const SHARED = [
      { id:'core.lobby', kind:'lobby', level:'storey_0', storey:0,
        polygon:[[C.x0,C.lobby.y0],[C.x1,C.lobby.y0],[C.x1,C.lobby.y1],[C.x0,C.lobby.y1]],
        z:sh.storeyZ[0], serves:'all', note:'street door, mail wall, the way to the core' },
      { id:'core.laundry', kind:'shared_laundry', level:'storey_0', storey:0,
        polygon:[[C.x0,C.laundry.y0],[C.x1,C.laundry.y0],[C.x1,C.laundry.y1],[C.x0,C.laundry.y1]],
        z:sh.storeyZ[0], serves:'all',
        note:lux?'in-suite machines as well, so this room is overflow':'the only machines in the building' },
    ];
    if(C.lockers.present) SHARED.push({ id:'core.lockers', kind:'lockers', level:'storey_0', storey:0,
      polygon:[[C.x0,C.lockers.y0],[C.x1,C.lockers.y0],[C.x1,C.lockers.y1],[C.x0,C.lockers.y1]],
      z:sh.storeyZ[0], serves:'all', note:'one cage per flat' });
    for(let s=0;s<b.storeys;s++) SHARED.push({ id:'core.corridor_s'+s, kind:'corridor',
      level:'storey_'+s, storey:s,
      polygon:[[sh.corridor.x0,sh.corridor.y0],[sh.corridor.x1,sh.corridor.y0],
               [sh.corridor.x1,sh.corridor.y1],[sh.corridor.x0,sh.corridor.y1]],
      z:sh.storeyZ[s], serves:'storey', width_m:sh.corridor.w,
      means_of_escape:true, note:'every flat door on this plate opens off it' });

    // ---- WALK: the entry steps, the loggias, the roof deck
    const WALK = [{ id:'block.entry_steps', kind:'entry_landing', z:r3(b.fH),
      polygon:[[r3((C.x0+C.x1)/2-1.45),r3(yF)],[r3((C.x0+C.x1)/2+1.45),r3(yF)],
               [r3((C.x0+C.x1)/2+1.45),r3(yF+1.30)],[r3((C.x0+C.x1)/2-1.45),r3(yF+1.30)]],
      steps:{ count:Math.max(1,Math.round(b.fH/0.17)), rise_m:r3(b.fH/Math.max(1,Math.round(b.fH/0.17))),
              run_m:0.32, handrail:{ sides:'both', height_m:0.90, material:'iron' } },
      treatment:'step_up', note:'the one way in at grade; the whole building shares it' }];
    for(const u of UNITS) if(u.loggia) WALK.push({ id:u.id+'.loggia', unit:u.id, kind:'loggia',
      level:u.level, z:r3(u.loggia.z),
      polygon:[[u.loggia.x0,r3(u.loggia.y)],[u.loggia.x1,r3(u.loggia.y)],
               [u.loggia.x1,r3(u.loggia.y-u.loggia.depth)],[u.loggia.x0,r3(u.loggia.y-u.loggia.depth)]],
      balustrade:{ height_m:1.05, infill:'steel_rod', cap:'timber' }, private_to:u.id,
      note:'recessed into the facade, so it is inside the collider box, not a projection' });
    if(lux && b.roofDeck) WALK.push({ id:'block.roof_deck', kind:'roof_deck', z:r3(b.topZ+0.02),
      polygon:[[r3(b.x0+1.2),r3(yR+1.2)],[r3(b.x0+b.Wd-1.2),r3(yR+1.2)],
               [r3(b.x0+b.Wd-1.2),r3(yF-1.2)],[r3(b.x0+1.2),r3(yF-1.2)]],
      balustrade:{ height_m:1.10, infill:'mesh', cap:'metal' }, shared:true,
      note:'reached from the core: the stair bulkhead and the lift both land up here' });

    const BLOCKERS = [{ what:'building', level:'grade', treatment:'wall',
      footprint:[[r3(b.x0),r3(yR)],[r3(b.x0+b.Wd),r3(yF)]],
      height_above_grade_m:r3(b.parapetZ),
      note:'the block shell; interior plates are walkable via SOLE' }];
    if(!lux && b.stairTower==='external') BLOCKERS.push({ what:'stair_tower', level:'grade',
      treatment:'wall', footprint:[[r3(b.x0-1.95),r3(-2.05)],[r3(b.x0),r3(2.05)]],
      height_above_grade_m:r3(b.topZ) });
    if(b.parking) BLOCKERS.push({ what:'parking_apron', level:'grade', treatment:'flat',
      footprint:[[r3(b.x0),r3(yF+1.4)],[r3(b.x0+b.Wd),r3(yF+(lux?5.6:5.2))]],
      height_above_grade_m:0.02, note:'surface parking, not a collider — drivable' });

    const ROOF = { deck_z:r3(b.topZ), parapet_z:r3(b.parapetZ), walkable:!!(lux&&b.roofDeck),
      access:(lux&&b.roofDeck) ? { kind:'core_bulkhead_and_lift', from:'core',
        footprint:[[r3(C.x0),r3(C.stair.y0)],[r3(C.x1),r3(C.stair.y1)]], height_m:2.45 } : null,
      plant:[{ what:'lift_overrun',
        footprint:[[r3(lift.x0),r3(lift.y0)],[r3(lift.x1),r3(lift.y1)]],
        height_m:r3(lux?b.fasciaH+2.0:b.fasciaH+1.85) }],
      note:(lux&&b.roofDeck) ? 'membrane deck behind a parapet; the core is the way up'
                             : 'flat membrane behind the parapet; no ladder, no hatch, not walkable' };

    return { UNITS, THRESHOLD, ELEVATOR, SHARED, WALK, BLOCKERS, ROOF,
      MAIL: b.mailwall ? [{ id:'core.mail_wall', kind:'mail_bank', level:'storey_0', storey:0,
        boxes:b.n, pos:[r3((C.x0+C.x1)/2-1.30), r3(C.lobby.y1-0.10), r3(b.fH+1.40)],
        stand:[r3((C.x0+C.x1)/2-1.30), r3(C.lobby.y1-0.95), r3(b.fH)],
        accepts:['letter','small_parcel'],
        note:'one bank in the lobby, one box per flat — a block does not put a box on every door' }] : undefined };
  }

  function gameplayAll(opts){
    const o=opts||{}, b=resolve(o), dm=dims(o), sh=shell(o), lux=b.tier==='luxury';
    const ext=gameplay(o);
    const IN=root.WalkupUnitIso;
    const inner=(IN&&IN.gameplaySections) ? IN.gameplaySections(Object.assign({},o,{focus:'all',explode:0})) : null;
    const out={
      schema:'hidden-harbours/building-gameplay@1',
      rig:'Art/walkupIsoRig.js',
      exportSymbol:'globalThis.WalkupIso',
      interiorRig:'Art/walkupUnitIsoRig.js',
      propRig:'Art/interiorPropRig.js',
      variant:b.tier+'_'+b.n+'u_'+b.beds+'bed_'+b.storeys+'st',
      generator:'WalkupIso.gameplayAll(opts)',
      phase:'multi-unit phase 2 — the walk-up block',
      build:{ tier:b.tier, units:b.n, perFloor:b.perFloor, columns:b.columns, beds:b.beds,
        mix:b.mix, storeys:b.storeys, body:b.body, accent:b.accent, band:b.band,
        glazing:b.glazing, loggia:b.loggia, stairTower:b.stairTower, roofDeck:b.roofDeck,
        lockers:b.lockers, parking:b.parking, mailwall:b.mailwall },
      frame:{ units:'metres', scale_px_per_m:PX, origin:'ground centre of the block footprint',
        axes:'+x along the corridor, +y street/front, +z up', heading_independent:true,
        cell:{ w:W, h:H, pivot:{ x:cx, y:groundY } },
        note:'the interior rig paints into this same cell and pivot, so a flat registers inside its bay to the pixel' },
      building:{ width_m:dm.Wd, depth_m:dm.Ln, storeys:b.storeys, unit_count:b.n,
        units_per_floor:b.perFloor, wall_thickness_m:sh.wallT,
        party_wall_thickness_m:sh.partyWallT, floor_thickness_m:sh.floorT,
        storey_height_m:b.storeyH, ceiling_height_m:sh.ceilH, ground_floor_z:sh.fH,
        storey_z:sh.storeyZ, roof_z:sh.roofZ, parapet_z:sh.parapetZ,
        corridor:sh.corridor, core:sh.core },
      // the cottage-room contract, at building scale: one rise and one clear height per storey, so
      // the baker never has to guess a plate it did not draw
      heights:{ storey_rise_m:Array.from({length:b.storeys},(_,s)=>
                  s<b.storeys-1 ? r3(sh.storeyZ[s+1]-sh.storeyZ[s]) : null),
                room_height_m:Array.from({length:b.storeys},()=>r3(sh.ceilH)),
                slab_thickness_m:sh.floorT,
                rule:'room_height = storey_rise - slab_thickness; the top storey has no rise' },
    };
    Object.assign(out, ext);
    if(inner){
      out.SOLE=inner.SOLE;
      out.THRESHOLD=out.THRESHOLD.concat(inner.THRESHOLD);
      out.STAIRS=inner.STAIRS;
      out.INTERACT=inner.INTERACT;
      out.BLOCKERS=out.BLOCKERS.concat(inner.BLOCKERS);
      out.reach_audit=inner.REACH_AUDIT;
      for(const u of out.UNITS){
        const sleeps=inner.INTERACT.filter(x=>x.unit===u.id && x.verb==='sleep');
        const baths=inner.SOLE.filter(x=>x.unit===u.id && (x.kind==='bath'||x.kind==='ensuite'));
        u.nominal_occupancy=u.resident_slots;
        u.resident_slots=sleeps.length;
        u.bedrooms_built=new Set(sleeps.map(x=>x.room)).size;
        u.baths=baths.length;
        u.sleep_anchors=sleeps.map(x=>x.id);
      }
      if(inner._unplaced&&inner._unplaced.length) out._unplaced=inner._unplaced;
      out._interiorNote=inner._interiorNote;
    }
    out._excluded={
      WASHBOARD:'Not a hull.', CLEATS:'Not a hull.',
      private_stair:'A flat is single-level. The terrace gives every household its own stair; a '+
        'walk-up gives every household the same one. Vertical routes are STAIRS (shared core) and ELEVATOR.',
      walkable_roof:(lux&&b.roofDeck) ? undefined
        : 'Flat membrane behind a parapet. The basic tier has no deck, no bulkhead and no hatch.',
      refuse_store:'Bins are site dressing placed by the scene, not building fabric.',
      site:'The rig bakes the block, not its plot. Kerbs, bike racks and planting are the scene\'s.',
    };
    out._confirm={
      interiors: inner ? undefined : 'Art/walkupUnitIsoRig.js was not loaded, so SOLE, STAIRS and INTERACT are ABSENT. Load the room rig and regenerate.',
      resident_slots:'UNITS[].resident_slots is the count of SLEEP anchors the interior actually built (one per approachable bed side). nominal_occupancy is the figure units() derives from the bed request. Trust resident_slots — it is the one backed by a bed in a room.',
      elevator_timings:'Car speed, acceleration, dwell and door times are DECLARED to give the game a starting point, not measured off the bake — the rig draws a car at a storey, not a journey.',
      lift_pit:'The 1.10 m pit and the motor overrun are declared from the drawn overrun box; nothing below grade is modelled.',
      entry_swing:'The street door is given its mechanism and, on the basic tier, a 95 degree outward swing. The bake draws it shut, so the arc is declared rather than measured off pixels.',
      flat_door_swing:'Flat doors are published opening INTO the flat. The bake parks every interior leaf flat against its wall, so this is a rule, not a measurement.',
    };
    return out;
  }

  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.WalkupIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    TIERS, BODY, CONC, TRIM, IRON, BRONZE, PLANT, GLAZINGS, LOGGIAS, BANDS, PRESETS, KEY,
    KEYLINE_DEFAULT, dims, shell, unitBox, units, render, anchors, project,
    gameplay, gameplayAll };
})(typeof globalThis!=='undefined'?globalThis:window);
