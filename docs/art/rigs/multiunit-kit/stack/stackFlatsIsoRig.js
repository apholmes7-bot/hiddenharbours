/* Hidden Harbours — parametric ISO STACKED-FLATS rig (ADR-0006 bake pipeline, SAME turntable +
   camera + shading as houseIsoRig.js / rowhouseIsoRig.js / walkupIsoRig.js / the fleet). MULTI-UNIT
   CLASS 3. ONE parametric 3D TIMBER-FRAMED BLOCK — a narrow, deep plot filled front to back, one or
   two full-depth flats per storey stacked on a single common stair — baked to pixel sheets through
   the SHARED 3/4 camera: 45deg steps, elev 40deg default, flat-facet shading from the fixed
   upper-LEFT key, z-buffered, ordered dither, per-face uv texture, depth-edge darkening, NO AA.
   32 px = 1 m. All 8 facings fall out of one model.

   WHY IT IS NOT THE TERRACE AND NOT THE WALK-UP. The other two classes are masonry and horizontal:
   the terrace is a run of narrow bays along a street, the walk-up a deep flat-roofed slab either
   side of an internal corridor. This class is TIMBER, VERTICAL and PORCHED — the plot is one flat
   wide and one flat deep, the flats stack, and every flat runs the whole depth of the building so
   it sees the street, the side and the yard. THERE IS NO CORRIDOR AND NO LIFT: circulation is one
   common stair at the party side with the flat doors on its landings, plus an open service stair in
   the stacked rear porch. The stacked porches are the silhouette — the thing that reads at 20 px.

   THE TWO STYLES (both new; neither shares a vocabulary with class 1 or class 2):
     porch     THREE-DECKER — painted clapboard on a rubble plinth, heavy white corner boards and
               window casings, STACKED OPEN PORCHES across the whole street face (square posts,
               top rail, square balusters, plank decks), a lattice skirt, granite steps, a low
               HIPPED asphalt roof with a stub brick chimney, storm sashes, laundry lines strung
               across the rear porch, an oil tank and bins down the side.
     gambrel   CAPTAIN'S FLATS — weathered cedar shingle over a granite plinth, a GAMBREL roof with
               shed dormers and a widow's-walk rail, GLAZED sun porches (small-pane) instead of open
               ones, a projecting two-storey canted bay on the side, copper-flashed eaves, a
               corbelled chimney, a boarded skirt and an iron-railed stoop.

   BUILDER SURFACE (every axis resolved per render, no re-modelling):
     tier:'porch'|'gambrel'   storeys:2|3   perFloor:1|2   beds:1|2|3   mix:'uniform'|'mixed'
     body: BODY ramp keys     roof:'hip'|'gambrel'   porch:'open'|'glazed'|'none'
     skirt:'lattice'|'board'|'none'   plinth:'rubble'|'granite'
     rearPorch:bool  dryingYard:bool  bay:bool  dormers:bool  widowWalk:bool
     lit:0..1 (night occupancy)  weather:0..1  night:bool  elev:deg
     detail:'lived'|'plain' and individually: dressing stormSash windowBoxes rainwater entryLamps
     signage meters laundryLines roofKit oilTank binStore cellarDoor kerb
     outline:bool (ADR-0031 A/B, default OFF)

   REGISTRATION CONTRACT for Art/stackUnitIsoRig.js (station precedent — the interior rig measures
   nothing, it reads the shell): shell(opts) publishes wall and party thicknesses, floor and ceiling
   heights per storey, every flat's interior footprint, the band depths its room program is measured
   from, which of its edges are glazed, the common stair bay sliced front to back, both porch decks,
   and every opening in the SAME metres this bake uses. units(opts) publishes the gameplay unit
   table. anchors(dir,opts) reports street door / mail / stair / rear stair / laundry / porch /
   window points in cell px per facing.

   LIGHT: matches the shipped neighbours — upper-left key, LN = normalize([-0.42,0.72,0.52]), so a
   three-decker composites into the same street as a terrace and a walk-up.
   KEYLINE: ringless by default (ADR-0031); {outline:true} kept as a live A/B.

   Exposes globalThis.StackFlatsIso = { W,H,PX,DIRS,pivot,order,defaultElev, TIERS,BODY,RUBBLE,
     GRANITE,TRIM,IRON,BRICK,ASPH,SLATE,COPPER,DECKW,PLANT,ROOFS,PORCHES,SKIRTS,PLINTHS,PRESETS,
     dims(opts), shell(opts), unitBox(b,u), units(opts), render(dir,opts), anchors(dir,opts),
     project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1280, H = 1320, cx = 640, groundY = 960;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;

  // ---- palettes, dark -> light (KTC master ramps; the painted ones are the harbour paint box) ----
  const BODY = {
    paintedWhite:  ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'],
    paintedSage:   ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'],
    seaGlass:      ['#143a38','#1f4d4a','#2c625e','#3b7872','#4d8f88','#66a69d'],
    harbourBlue:   ['#16283a','#20384d','#2b4a63','#385d78','#47728d','#5b88a3'],
    oxblood:       ['#331a1a','#472423','#5c2f2c','#713d37','#874d44','#9c6053'],
    ochre:         ['#4a3a19','#5f4b22','#75602e','#8b763d','#a18c4f','#b7a366'],
    silverShingle: ['#43443f','#54554f','#676861','#7b7c74','#908f87','#a5a49b'],
    cedarStain:    ['#3f2c1e','#543b28','#684b33','#7d5c40','#93704f','#a9855f'],
    slateGrey:     ['#33383c','#42474b','#54595d','#666c70','#7a8084','#8e9498'],
    sandRender:    ['#6f6350','#877a63','#a2947a','#bcae92','#d3c6a9','#e7dcc2'],
    charTimber:    ['#17171a','#212126','#2c2d33','#393a42','#484a53','#595c66'],
    galv:          ['#464d51','#5a6267','#727c81','#8c979c','#a6b1b5','#c2cccf'],
  };
  const TRIM    = ['#9aa09a','#b4b8b0','#ccd0c7','#e0e2da','#eef0e8','#f8f9f2'];
  const RUBBLE  = ['#3b3d3a','#4a4d49','#5c605b','#70746e','#858982','#9a9e96'];
  const GRANITE = ['#4a4d51','#5a5e62','#6d7175','#818589','#95999d','#a9adb1'];
  const BRICK   = ['#3a201a','#552b20','#6e3728','#874634','#a05743','#b96b55'];
  const ASPH    = ['#22252a','#2c3036','#383d44','#464c54','#565d66','#666e78'];
  const SLATE   = ['#282d33','#333940','#40474f','#4f5760','#606872','#727b85'];
  const COPPER  = ['#1e3a33','#2b5045','#3a6659','#4b7d6d','#5d9482','#71ab97'];
  const DECKW   = ['#4b4238','#5c5245','#6f6455','#847765','#998b77','#ae9f8a'];
  const IRON    = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970'];
  const PLANT   = ['#1b2a1c','#263a25','#334c30','#42603c','#537449','#668a58'];
  const LINEN   = ['#8a8a80','#a2a297','#b9b9ad','#cfcfc2','#e2e2d5','#f1f1e5'];
  const GLASSD  = ['#1e2c31','#26383d','#33474d','#40585f','#547078'];
  const GLASSN  = ['#7a4f18','#b98a2f','#eed07a'];
  const GLASS_HI = '#cfe6e8';
  const KEY = '#1a1c22';

  const ROOFS   = ['hip','mansard'];
  const PORCHES = ['open','glazed','none'];
  const SKIRTS  = ['lattice','board','none'];
  const PLINTHS = ['rubble','granite'];

  const TIERS = {
    porch: {
      body:'seaGlass', accent:'paintedWhite', roof:'hip', porch:'open', skirt:'lattice',
      plinth:'rubble', roofMat:'asphalt', bay:false, dormers:false, widowWalk:false,
      rearPorch:true, dryingYard:true,
      storeyH:2.95, fH:0.86, eaveH:0.46, roofRise:1.72, ov:0.46, panes:2,
      stairW:2.80, flatD:12.20, porchDep:2.10, rearDep:1.85,
      flatW:{1:6.20,2:7.40,3:8.60}, weather:0.36,
    },
    gambrel: {
      body:'silverShingle', accent:'paintedWhite', roof:'mansard', porch:'glazed', skirt:'board',
      plinth:'granite', roofMat:'slate', bay:true, dormers:true, widowWalk:true,
      rearPorch:true, dryingYard:false,
      storeyH:3.20, fH:1.06, eaveH:0.56, roofRise:2.85, ov:0.40, panes:3,
      stairW:3.05, flatD:12.90, porchDep:2.40, rearDep:1.95,
      flatW:{1:6.60,2:7.90,3:9.10}, weather:0.10,
    },
  };

  // presets are NAMED BLOCKS — the building the quarter knows by sight
  const PRESETS = {
    decker6:     { tier:'porch',   storeys:3, perFloor:2, beds:2, body:'seaGlass',      weather:0.34 },
    decker3:     { tier:'porch',   storeys:3, perFloor:1, beds:3, body:'oxblood',       weather:0.42 },
    decker4:     { tier:'porch',   storeys:2, perFloor:2, beds:2, body:'paintedWhite',  weather:0.30 },
    deckerMixed: { tier:'porch',   storeys:3, perFloor:2, beds:2, body:'ochre',         weather:0.46, mix:'mixed' },
    captains6:   { tier:'gambrel', storeys:3, perFloor:2, beds:2, body:'silverShingle', weather:0.10 },
    captains2:   { tier:'gambrel', storeys:2, perFloor:1, beds:3, body:'cedarStain',    weather:0.07 },
    captains4:   { tier:'gambrel', storeys:2, perFloor:2, beds:2, body:'harbourBlue',   weather:0.12 },
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
  function clampI(v,a,b){ return Math.max(a, Math.min(b, Math.round(v))); }
  function r3(v){ return Math.round(v*1000)/1000; }

  // ---- camera / projection (identical to the terrace and the walk-up) ----
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
  function tri(out, p0,p1,p2, mat, b, tex){
    const uv = tex ? [[0,0],[Math.hypot(p1[0]-p0[0],p1[1]-p0[1],p1[2]-p0[2]),0],
                      [Math.hypot(p2[0]-p0[0],p2[1]-p0[1],p2[2]-p0[2]),0.9]] : null;
    out.push(F([p0,p1,p2], mat, b||0, 0, uv, tex));
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
  // a gable / dormer cheek in the plane x = xv, points as [y,z] and CCW seen from +x
  function polyX(out, xv, ptsYZ, nx, mat, b, tex){
    const P = ptsYZ.map(p=>[xv, p[0], p[1]]);
    const V = nx>0 ? P : P.slice().reverse();
    out.push(F(V, mat, b||0, 0, tex?V.map(p=>[p[1],p[2]]):null, tex||null));
  }
  function polyY(out, yv, ptsXZ, ny, mat, b, tex){
    const P = ptsXZ.map(p=>[p[0], yv, p[1]]);
    const V = ny>0 ? P.slice().reverse() : P;
    out.push(F(V, mat, b||0, 0, tex?V.map(p=>[p[0],p[2]]):null, tex||null));
  }
  // a wall plane with rectangular holes punched in it (porch entries, loggia voids)
  function wallMinusY(out, x0,x1, yv, ny, z0,z1, mat, tex, b, holes){
    let rects=[[x0,x1,z0,z1]];
    for(const h of (holes||[])){
      const next=[];
      for(const q of rects){
        const [a0,a1,b0,b1]=q;
        if(h.x1<=a0+0.001||h.x0>=a1-0.001||h.z1<=b0+0.001||h.z0>=b1-0.001){ next.push(q); continue; }
        const cx0=Math.max(a0,h.x0), cx1=Math.min(a1,h.x1), cz0=Math.max(b0,h.z0), cz1=Math.min(b1,h.z1);
        if(b0<cz0-0.001) next.push([a0,a1,b0,cz0]);
        if(cz1<b1-0.001) next.push([a0,a1,cz1,b1]);
        if(a0<cx0-0.001) next.push([a0,cx0,cz0,cz1]);
        if(cx1<a1-0.001) next.push([cx1,a1,cz0,cz1]);
      }
      rects=next;
    }
    for(const [a0,a1,b0,b1] of rects) if(a1-a0>0.02 && b1-b0>0.02){
      if(ny>0) wall(out, a1,yv, a0,yv, b0,b1, mat, tex, b);
      else     wall(out, a0,yv, a1,yv, b0,b1, mat, tex, b);
    }
  }
  function wallMinusX(out, y0,y1, xv, nx, z0,z1, mat, tex, b, holes){
    let rects=[[y0,y1,z0,z1]];
    for(const h of (holes||[])){
      const next=[];
      for(const q of rects){
        const [a0,a1,b0,b1]=q;
        if(h.y1<=a0+0.001||h.y0>=a1-0.001||h.z1<=b0+0.001||h.z0>=b1-0.001){ next.push(q); continue; }
        const cy0=Math.max(a0,h.y0), cy1=Math.min(a1,h.y1), cz0=Math.max(b0,h.z0), cz1=Math.min(b1,h.z1);
        if(b0<cz0-0.001) next.push([a0,a1,b0,cz0]);
        if(cz1<b1-0.001) next.push([a0,a1,cz1,b1]);
        if(a0<cy0-0.001) next.push([a0,cy0,cz0,cz1]);
        if(cy1<a1-0.001) next.push([cy1,a1,cz0,cz1]);
      }
      rects=next;
    }
    for(const [a0,a1,b0,b1] of rects) if(a1-a0>0.02 && b1-b0>0.02){
      if(nx>0) wall(out, xv,a0, xv,a1, b0,b1, mat, tex, b);
      else     wall(out, xv,a1, xv,a0, b0,b1, mat, tex, b);
    }
  }
  function decalY(out, yv, ny, xs,xe, z0,z1, mat, b, tex, flat, db){
    const e=0.02*ny, uw=xe-xs, uh=z1-z0;
    const P = ny>0
      ? [[xe,yv+e,z0],[xs,yv+e,z0],[xs,yv+e,z1],[xe,yv+e,z1]]
      : [[xs,yv+e,z0],[xe,yv+e,z0],[xe,yv+e,z1],[xs,yv+e,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.06, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat));
  }
  function decalX(out, xv, nx, ys,ye, z0,z1, mat, b, tex, flat, db){
    const e=0.02*nx, uw=ye-ys, uh=z1-z0;
    const P = nx>0
      ? [[xv+e,ys,z0],[xv+e,ye,z0],[xv+e,ye,z1],[xv+e,ys,z1]]
      : [[xv+e,ye,z0],[xv+e,ys,z0],[xv+e,ys,z1],[xv+e,ye,z1]];
    out.push(F(P, mat, b||0, db!=null?db:0.06, tex?[[0,0],[uw,0],[uw,uh],[0,uh]]:null, tex||null, flat));
  }
  const dec = (out, ax, plane, nrm, a0,a1, z0,z1, mat, bias, tex, flat, db) =>
    ax==='y' ? decalY(out, plane,nrm, a0,a1, z0,z1, mat, bias, tex||null, flat!==false, db)
             : decalX(out, plane,nrm, a0,a1, z0,z1, mat, bias, tex||null, flat!==false, db);

  // ---- textures: integer ramp deltas ----------------------------------------
  function clapTex(){ const SP=0.155;
    return (u,v)=>{ const f=((v%SP)+SP)%SP;
      if(f<0.026) return -2;                                   // shadow under each lap
      if(f>SP-0.020) return 1;                                 // lit board edge
      const t=hash2(Math.floor(u*3.2), Math.floor(v/SP)); return t<0.12?-1:(t>0.94?1:0); }; }
  function shingleTex(){ const CH=0.185, SW=0.30;
    return (u,v)=>{ const row=Math.floor(v/CH), f=((v%CH)+CH)%CH;
      const off=(row&1)*SW*0.5 + hash2(row,11)*0.06, su=(((u+off)%SW)+SW)%SW;
      if(f<0.024) return -2;
      if(su<0.020) return -1;
      const t=hash2(Math.floor((u+off)/SW), row); return t<0.24?-1:(t>0.88?1:0); }; }
  function rubbleTex(){ const CH=0.30;
    return (u,v)=>{ const row=Math.floor(v/CH), f=((v%CH)+CH)%CH;
      const jw=0.42+0.18*hash2(row,7), off=hash2(row,3)*0.4;
      const su=(((u+off)%jw)+jw)%jw;
      if(f<0.036||su<0.034) return -2;
      const t=hash2(Math.floor((u+off)/jw), row); return t<0.30?-1:(t>0.86?1:0); }; }
  function graniteTex(){ const CH=0.46;
    return (u,v)=>{ const f=((v%CH)+CH)%CH; if(f<0.030) return -2;
      const t=hash2(Math.floor(u*9), Math.floor(v*9)); return t<0.20?-1:(t>0.90?1:0); }; }
  function asphaltTex(){ const CH=0.30;
    return (u,v)=>{ const row=Math.floor(v/CH), f=((v%CH)+CH)%CH;
      if(f<0.030) return -1;
      const su=(((u+(row&1)*0.45)%0.90)+0.90)%0.90;
      if(su<0.024) return -1;
      const t=hash2(Math.floor(u*4), row); return t<0.18?-1:0; }; }
  function slateTex(){ const CH=0.26, SW=0.36;
    return (u,v)=>{ const row=Math.floor(v/CH), f=((v%CH)+CH)%CH, off=(row&1)*SW*0.5;
      const su=(((u+off)%SW)+SW)%SW;
      if(f<0.026||su<0.022) return -2;
      const t=hash2(Math.floor((u+off)/SW), row); return t<0.22?-1:(t>0.90?1:0); }; }
  function plankTex(){ const SP=0.16;
    return (u,v)=>{ const su=((v%SP)+SP)%SP; if(su<0.020) return -1;
      const t=hash2(Math.floor(u*3), Math.floor(v/SP)); return t<0.12?-1:(t>0.94?1:0); }; }
  function latticeTex(){ const SP=0.115;
    return (u,v)=>{ const a=(((u+v)%SP)+SP)%SP, c=(((u-v)%SP)+SP)%SP;
      return (a<0.030||c<0.030) ? 0 : -3; }; }
  function brickTex(){ const CO=0.086, BR=0.235;
    return (u,v)=>{ const row=Math.floor(v/CO), f=((v%CO)+CO)%CO;
      const off=(row&1)*BR*0.5, su=(((u+off)%BR)+BR)%BR;
      if(f<0.024) return -2; if(su<0.022) return -1;
      const t=hash2(Math.floor((u+off)/BR), row); return t<0.20?-1:(t>0.90?1:0); }; }
  function boardTex(){ const SP=0.24;
    return (u,v)=>{ const su=((u%SP)+SP)%SP; if(su<0.024) return -1;
      const t=hash2(Math.floor(u/SP), Math.floor(v*5)); return t<0.14?-1:(t>0.93?1:0); }; }
  function linenTex(){ return (u,v)=>{ const t=hash2(Math.floor(u*8), Math.floor(v*8));
      return t<0.18?-1:(t>0.88?1:0); }; }

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

  // ---- resolve: the block becomes a STAIR BAY + A STACK OF FULL-DEPTH FLATS ----
  function resolve(opts){
    opts = opts||{};
    const tierKey = (opts.tier==='gambrel') ? 'gambrel' : 'porch';
    const T = TIERS[tierKey];
    const g = (k,d)=> opts[k]!=null ? opts[k] : (T[k]!=null ? T[k] : d);
    const storeys  = clampI(opts.storeys!=null?opts.storeys:3, 2, 3);
    const perFloor = clampI(opts.perFloor!=null?opts.perFloor:2, 1, 2);
    const beds     = clampI(opts.beds!=null?opts.beds:2, 1, 3);
    const mix      = opts.mix || 'uniform';
    const n = storeys*perFloor;

    const storeyH=g('storeyH',3.0), fH=g('fH',0.9), eaveH=g('eaveH',0.5);
    const stairW=g('stairW',2.8), flatD=g('flatD',12.2);
    // A stacked-flats plot has a sensible maximum frontage — past about 20 m it stops being a
    // three-decker and becomes a slab, so wide flats squeeze rather than the plot growing.
    let flatW = T.flatW[beds]||T.flatW[2];
    const MAXW=20.5;
    if(stairW + perFloor*flatW > MAXW) flatW = (MAXW-stairW)/perFloor;

    const Wd = stairW + perFloor*flatW, Ln = flatD;
    const x0=-Wd/2, x1=Wd/2, yR=-Ln/2, yF=Ln/2;
    // one flat per floor: the stair bay takes the high-x edge. Two: it sits between them.
    const stairX1 = perFloor===2 ? stairW/2 : x1;
    const stairX0 = stairX1 - stairW;
    const bayX = perFloor===2 ? [[x0,stairX0],[stairX1,x1]] : [[x0,stairX0]];

    const bays=[]; let id=0;
    for(let s=0;s<storeys;s++) for(let c=0;c<perFloor;c++){
      const bx=bayX[c];
      const bd = mix==='mixed' ? (s===storeys-1 ? Math.max(1,beds-1) : beds) : beds;
      bays.push({ i:id, unit:'unit_'+(id+1), storey:s, col:c,
        side: perFloor===1 ? 'full' : (c===0?'west':'east'),
        x0:bx[0], x1:bx[1], w:bx[1]-bx[0], xc:(bx[0]+bx[1])/2, beds:bd,
        hallSide: (perFloor===1 || c===0) ? 1 : -1,
        outerX: (perFloor===1 || c===0) ? bx[0] : bx[1],
        outerN: (perFloor===1 || c===0) ? -1 : 1,
        y0:yR, y1:yF });
      id++;
    }

    const det = (opts.detail||'lived') !== 'plain';
    // THE ROOF IS SIZED BY THE PLOT, not by a constant. A hip over an 18 m frontage with a fixed
    // 1.7 m rise reads as a flat slab; the pitch is what makes the class read as timber housing, so
    // the rise falls out of the hip run at a fixed pitch and only then can a caller override it.
    const roofKind = opts.roof || T.roof || 'hip';
    const ovv = opts.ov!=null ? opts.ov : (T.ov!=null?T.ov:0.45);
    const hipRun = Math.min((Wd+2*ovv)/2, (Ln+2*ovv)/2);
    const pitch = roofKind==='mansard' ? 0.42 : 0.34;
    const rise = opts.roofRise!=null ? opts.roofRise
      : clamp(hipRun*pitch, 1.5, storeyH*(roofKind==='mansard'?1.02:0.80));
    const b = {
      tier:tierKey, n, storeys, perFloor, beds, mix, flatW, flatD, stairW, Wd, Ln,
      x0, x1, yR, yF, stairX0, stairX1, bayX, bays, storeyH, fH, eaveH,
      body: opts.body || T.body, accent: opts.accent || T.accent,
      roof: roofKind, porch: g('porch','open'), skirt: g('skirt','lattice'),
      plinth: g('plinth','rubble'), roofMat: g('roofMat','asphalt'),
      roofRise: rise, hipRun: r3(hipRun), ov: ovv, panes: g('panes',2),
      porchDep: g('porchDep',2.1), rearDep: g('rearDep',1.85),
      bay: opts.bay!=null ? !!opts.bay : T.bay,
      dormers: opts.dormers!=null ? !!opts.dormers : T.dormers,
      widowWalk: opts.widowWalk!=null ? !!opts.widowWalk : T.widowWalk,
      rearPorch: opts.rearPorch!=null ? !!opts.rearPorch : T.rearPorch,
      dryingYard: opts.dryingYard!=null ? !!opts.dryingYard : T.dryingYard,
      // THE LIVED-IN PASS. Individually switchable; detail:'plain' clears the lot, so the block can
      // be baked bare for a background silhouette or dressed for a foreground hero.
      detail: det ? 'lived' : 'plain',
      dressing:     opts.dressing!=null     ? !!opts.dressing     : det,
      stormSash:    opts.stormSash!=null    ? !!opts.stormSash    : (det && tierKey==='porch'),
      windowBoxes:  opts.windowBoxes!=null  ? !!opts.windowBoxes  : det,
      rainwater:    opts.rainwater!=null    ? !!opts.rainwater    : det,
      entryLamps:   opts.entryLamps!=null   ? !!opts.entryLamps   : det,
      signage:      opts.signage!=null      ? !!opts.signage      : det,
      meters:       opts.meters!=null       ? !!opts.meters       : (det && tierKey==='porch'),
      laundryLines: opts.laundryLines!=null ? !!opts.laundryLines : (det && tierKey==='porch'),
      roofKit:      opts.roofKit!=null      ? !!opts.roofKit      : det,
      oilTank:      opts.oilTank!=null      ? !!opts.oilTank      : (det && tierKey==='porch'),
      binStore:     opts.binStore!=null     ? !!opts.binStore     : det,
      cellarDoor:   opts.cellarDoor!=null   ? !!opts.cellarDoor   : det,
      kerb:         opts.kerb!=null         ? !!opts.kerb         : det,
      lit: opts.lit!=null ? opts.lit : 0.5,
      weather: opts.weather!=null ? opts.weather : T.weather,
      night: !!opts.night,
      outline: opts.outline!=null ? !!opts.outline : KEYLINE_DEFAULT,
      wallT: 0.24, partyT: 0.20, floorT: 0.30,
    };
    b.topZ = fH + storeys*storeyH;          // top of the wall plate
    b.eaveZ = b.topZ + eaveH;               // top of the frieze / fascia band
    b.ridgeZ = b.eaveZ + b.roofRise;
    b.storeyZ = []; for(let s=0;s<storeys;s++) b.storeyZ.push(fH + s*storeyH);
    return b;
  }

  // ---- materials -----------------------------------------------------------
  function makeMats(b){
    const wx=b.weather, night=b.night;
    const wthBody=(ramp)=>ramp.map(c=>{ let x=desat(c, wx*0.44); x=mix(x,'#6b675e',wx*0.26); if(night)x=mix(x,'#1b2733',0.44); return x; });
    const wthHard=(ramp)=>ramp.map(c=>{ let x=desat(c, wx*0.30); x=mix(x,'#6f6a5f',wx*0.18); if(night)x=mix(x,'#1b2733',0.42); return x; });
    const nightOnly=(ramp)=>ramp.map(c=> night?mix(c,'#1b2733',0.38):c );
    const bodyR = BODY[b.body]||BODY.seaGlass, accR = BODY[b.accent]||BODY.paintedWhite;
    const plinthR = b.plinth==='granite' ? GRANITE : RUBBLE;
    const roofR = b.roofMat==='slate' ? SLATE : ASPH;
    return {
      body:    { ramp: wthBody(bodyR) },
      accent:  { ramp: wthBody(accR) },
      trim:    { ramp: wthHard(TRIM) },
      plinth:  { ramp: wthHard(plinthR) },
      roof:    { ramp: wthHard(roofR) },
      brick:   { ramp: wthBody(BRICK) },
      deck:    { ramp: wthHard(DECKW) },
      iron:    { ramp: nightOnly(IRON) },
      metal:   { ramp: wthHard(BODY.galv) },
      copper:  { ramp: wthHard(COPPER) },
      plant:   { ramp: nightOnly(PLANT) },
      linen:   { ramp: nightOnly(LINEN) },
      blind:   { ramp: wthHard(['#6e6a5c','#847f6e','#9a9482','#b0a897','#c5bdab','#d9d1bf']) },
      hall:    { ramp: night?['#8a5c1e','#b98a2f','#dcb055','#f0d283','#f8e6b4']
                            : ['#6d7c6f','#8b9a86','#a9b69e','#c6d0b6','#dee5cd'] },
      glass:   { ramp: night?GLASSN:GLASSD },
      glassOff:{ ramp: night?nightOnly(GLASSD):GLASSD },
      glassHi: { ramp: [night?'#f6dfa6':GLASS_HI] },
      cavity:  { ramp: ['#0d1013','#141a1e','#1b2328','#232c32'] },
    };
  }

  // ---- openings ------------------------------------------------------------
  // A double-hung sash in a heavy painted casing: the class-3 signature opening. `panes` sets the
  // muntin grid (2 = 2-over-2 clapboard tier, 3 = 6-over-6 shingle tier).
  function sash(out, b, ax, plane, nrm, c, wdt, z0, hgt, o){
    o=o||{};
    const a0=c-wdt/2, a1=c+wdt/2, z1=z0+hgt, mid=(z0+z1)/2;
    const panes = o.panes!=null?o.panes:b.panes;
    dec(out, ax, plane, nrm, a0-0.10, a1+0.10, z0-0.09, z1+0.14, 'trim', 0.34, null, true, 0.035);
    dec(out, ax, plane, nrm, a0, a1, z0, z1, o.lit?'glass':'glassOff', 0.0, null, true, 0.075);
    if(o.lit) dec(out, ax, plane, nrm, a0+0.05, a0+0.16, mid+0.12, z1-0.08, 'glassHi', 0, null, true, 0.09);
    dec(out, ax, plane, nrm, a0, a1, mid-0.035, mid+0.035, 'trim', 0.36, null, true, 0.10);
    for(let q=1;q<panes;q++){ const t=a0+(a1-a0)*q/panes;
      dec(out, ax, plane, nrm, t-0.026, t+0.026, z0, z1, 'trim', 0.36, null, true, 0.10); }
    if(panes>2){ for(const zz of [z0+(mid-z0)*0.5, mid+(z1-mid)*0.5])
      dec(out, ax, plane, nrm, a0, a1, zz-0.024, zz+0.024, 'trim', 0.36, null, true, 0.10); }
    if(o.blind>0.03) dec(out, ax, plane, nrm, a0+0.04, a1-0.04, z1-hgt*o.blind, z1-0.02, 'blind', 0.18, linenTex(), true, 0.115);
    // storm sash: an outer frame set a hand's width off the casing — the porch tier wears them
    if(o.storm) dec(out, ax, plane, nrm, a0-0.06, a1+0.06, z0-0.05, z1+0.05, 'trim', 0.20, null, true, 0.125);
    // sill + apron
    if(ax==='y'){ const y0=nrm>0?plane:plane-0.09, y1=nrm>0?plane+0.09:plane;
      boxSolid(out, a0-0.14,a1+0.14, y0,y1, z0-0.10,z0-0.02, 'trim', null, 0.22, 0.40); }
    else { const xx0=nrm>0?plane:plane-0.09, xx1=nrm>0?plane+0.09:plane;
      boxSolid(out, xx0,xx1, a0-0.14,a1+0.14, z0-0.10,z0-0.02, 'trim', null, 0.22, 0.40); }
  }
  function panelDoor(out, b, ax, plane, nrm, c, wdt, z0, hgt, o){
    o=o||{};
    const a0=c-wdt/2, a1=c+wdt/2, z1=z0+hgt;
    dec(out, ax, plane, nrm, a0-0.11, a1+0.11, z0, z1+0.15, 'trim', 0.36, null, true, 0.035);
    dec(out, ax, plane, nrm, a0, a1, z0, z1, 'accent', -0.12, null, true, 0.07);
    // upper light + two lower panels
    dec(out, ax, plane, nrm, a0+0.10, a1-0.10, z1-hgt*0.42, z1-0.12, o.lit?'glass':'glassOff', 0, null, true, 0.10);
    dec(out, ax, plane, nrm, a0+0.10, a1-0.10, z0+0.14, z0+hgt*0.44, 'accent', 0.16, null, true, 0.10);
    // knob
    dec(out, ax, plane, nrm, a1-0.20, a1-0.12, z0+0.98, z0+1.06, 'metal', 0.42, null, true, 0.12);
    if(o.transom) dec(out, ax, plane, nrm, a0-0.02, a1+0.02, z1+0.16, z1+0.52, o.lit?'glass':'glassOff', 0, null, true, 0.08);
  }

  // ================= the shell =================
  function buildShell(out, b, bodyTex){
    const t=b.wallT, x0=b.x0, x1=b.x1, yR=b.yR, yF=b.yF;
    const plinthTex = b.plinth==='granite'?graniteTex():rubbleTex();
    // plinth: the block sits on stone, and the stone steps out
    boxSolid(out, x0-0.09, x1+0.09, yR-0.09, yF+0.09, 0, b.fH, 'plinth', plinthTex, 0.0, 0.30);
    // the body: one box per storey band so the water table and the storey bands read
    for(let s=0;s<b.storeys;s++){
      const z0=b.storeyZ[s], z1=z0+b.storeyH;
      boxSolid(out, x0, x1, yR, yF, z0, z1, 'body', bodyTex, 0.0, 0.24);
    }
    // corner boards — the heavy white trim that says timber frame, not masonry
    for(const cxn of [[x0,yR],[x0,yF],[x1,yR],[x1,yF]]){
      const sx = cxn[0]<0?1:-1, sy = cxn[1]<0?1:-1;
      boxSolid(out, cxn[0], cxn[0]+sx*0.17, cxn[1]-0.035, cxn[1]+0.035, b.fH, b.topZ, 'trim', null, 0.20, 0.30);
      boxSolid(out, cxn[0]-0.035, cxn[0]+0.035, cxn[1], cxn[1]+sy*0.17, b.fH, b.topZ, 'trim', null, 0.20, 0.30);
    }
    // water table over the plinth, and a storey band between floors
    for(const yv of [yR,yF]) decalY(out, yv, yv<0?-1:1, x0,x1, b.fH-0.02, b.fH+0.10, 'trim', 0.26, null, true, 0.03);
    for(const xv of [x0,x1]) decalX(out, xv, xv<0?-1:1, yR,yF, b.fH-0.02, b.fH+0.10, 'trim', 0.26, null, true, 0.03);
    for(let s=1;s<b.storeys;s++){
      const z=b.storeyZ[s]-0.06;
      for(const yv of [yR,yF]) decalY(out, yv, yv<0?-1:1, x0,x1, z, z+0.11, 'trim', 0.22, null, true, 0.03);
      for(const xv of [x0,x1]) decalX(out, xv, xv<0?-1:1, yR,yF, z, z+0.11, 'trim', 0.22, null, true, 0.03);
    }
    // frieze + fascia: a boxed cornice, copper-flashed on the shingle tier
    boxSolid(out, x0-0.06, x1+0.06, yR-0.06, yF+0.06, b.topZ, b.eaveZ, 'trim', null, 0.16, 0.34);
    if(b.tier==='gambrel')
      boxSolid(out, x0-0.10, x1+0.10, yR-0.10, yF+0.10, b.eaveZ-0.09, b.eaveZ, 'copper', null, 0.22, 0.40);
  }

  // A SHED DORMER on one slope of the gambrel: face at the low end, roof running back up into the
  // main slope, cheeks dying into it. Given the slope's eave and knuckle in one axis, everything
  // else falls out — so the same code serves both ridge orientations and both sides.
  function shedDormer(out, b, axis, aEave, aKnee, zEave, zKnee, cAt, dw, rTex){
    const span=aKnee-aEave, zsp=zKnee-zEave;
    if(Math.abs(span)<0.5 || zsp<0.6) return;
    const t0=0.26, dh=1.04;
    const aF=aEave+span*t0, zF=zEave+zsp*t0, head=zF+dh;
    const tB=clamp((head+0.28-zEave)/zsp, t0+0.18, 0.97);
    const aB=aEave+span*tB, zB=zEave+zsp*tB;
    if(zB<=head+0.06) return;
    const nrm = span>0 ? -1 : 1;                    // the face looks back down the slope
    const c0 = span>0 ? cAt-dw/2 : cAt+dw/2, c1 = span>0 ? cAt+dw/2 : cAt-dw/2;
    const e0 = span>0 ? -0.12 : 0.12;
    if(axis==='y'){
      wall(out, c0,aF, c1,aF, zF, head, 'body', shingleTex(), 0.05);
      quad(out, [c0-e0,aF,head],[c1+e0,aF,head],[c1+e0,aB,zB],[c0-e0,aB,zB], 'roof', 0.24, rTex);
      for(const cx of [cAt-dw/2, cAt+dw/2])
        polyX(out, cx, [[aF,zF],[aB,zB],[aF,head]], cx<cAt?-1:1, 'body', 0.03, shingleTex());
      sash(out, b, 'y', aF, nrm, cAt, Math.min(1.45, dw-0.44), zF+0.16, dh-0.34, {panes:3});
    } else {
      wall(out, aF,c1, aF,c0, zF, head, 'body', shingleTex(), 0.05);
      quad(out, [aF,c1+e0,head],[aF,c0-e0,head],[aB,c0-e0,zB],[aB,c1+e0,zB], 'roof', 0.24, rTex);
      for(const cy of [cAt-dw/2, cAt+dw/2])
        polyY(out, cy, [[aF,zF],[aB,zB],[aF,head]], cy<cAt?-1:1, 'body', 0.03, shingleTex());
      sash(out, b, 'x', aF, nrm, cAt, Math.min(1.45, dw-0.44), zF+0.16, dh-0.34, {panes:3});
    }
  }

  // ---- roofs. Ridge runs along the LONGER axis, so a one-flat plot and a two-flat plot both get a
  // roof that closes. The hip is four planes; the gambrel is two knuckled planes plus gable ends.
  function buildRoof(out, b){
    const rTex = b.roofMat==='slate'?slateTex():asphaltTex();
    const ov=b.ov, ex0=b.x0-ov, ex1=b.x1+ov, ey0=b.yR-ov, ey1=b.yF+ov;
    const z0=b.eaveZ, zr=b.ridgeZ;
    const alongX = (ex1-ex0) >= (ey1-ey0);
    const hw=(ex1-ex0)/2, hd=(ey1-ey0)/2, mx=(ex0+ex1)/2, my=(ey0+ey1)/2;
    // eave soffit: the four sides only. A top slab here sits at exactly the height the slopes
    // spring from and wins the z-buffer over the whole roof — a white plate where the roof should be.
    wall(out, ex0,ey0, ex1,ey0, z0-0.11, z0, 'trim', null, -0.10);
    wall(out, ex1,ey1, ex0,ey1, z0-0.11, z0, 'trim', null, -0.10);
    wall(out, ex1,ey0, ex1,ey1, z0-0.11, z0, 'trim', null, -0.10);
    wall(out, ex0,ey1, ex0,ey0, z0-0.11, z0, 'trim', null, -0.10);
    if(b.roof==='mansard'){
      // A MANSARD: a steep face on all four sides, truncated to a DECK. A true gambrel needs close
      // to 6 m of rise over a 13 m span, which would swallow the building; the truncated form is
      // what the type actually does at this footprint — and the deck is where the widow's walk goes.
      const kn=0.30, iw=hw*kn, id=hd*kn, zk=zr;
      const dx0=ex0+iw, dx1=ex1-iw, dy0=ey0+id, dy1=ey1-id;
      quad(out, [ex0,ey0,z0],[ex1,ey0,z0],[dx1,dy0,zk],[dx0,dy0,zk], 'roof', 0.06, rTex);
      quad(out, [ex1,ey1,z0],[ex0,ey1,z0],[dx0,dy1,zk],[dx1,dy1,zk], 'roof', 0.20, rTex);
      quad(out, [ex0,ey1,z0],[ex0,ey0,z0],[dx0,dy0,zk],[dx0,dy1,zk], 'roof', 0.02, rTex);
      quad(out, [ex1,ey0,z0],[ex1,ey1,z0],[dx1,dy1,zk],[dx1,dy0,zk], 'roof', 0.22, rTex);
      slab(out, [[dx0,dy0],[dx1,dy0],[dx1,dy1],[dx0,dy1]], zk, 'roof', 0.34, plankTex());
      // a bead of copper along the deck EDGE — four strips, not one box: a box's top slab would
      // plate the whole deck over in copper
      for(const st of [[dx0-0.10,dx1+0.10,dy0-0.10,dy0+0.06],[dx0-0.10,dx1+0.10,dy1-0.06,dy1+0.10],
                       [dx0-0.10,dx0+0.06,dy0-0.10,dy1+0.10],[dx1-0.06,dx1+0.10,dy0-0.10,dy1+0.10]])
        boxSolid(out, st[0],st[1], st[2],st[3], zk-0.09, zk+0.03, 'copper', null, 0.26, 0.42);
      if(b.widowWalk) railRing(out, dx0+0.14, dx1-0.14, dy0+0.14, dy1-0.14, zk+0.02, 0.78, 'iron', 0.055, 4.4);
      // shed dormers in the steep street and yard faces, one per flat bay
      if(b.dormers) for(const u of b.bays.filter(v=>v.storey===b.storeys-1)){
        const dw=Math.min(2.70, u.w*0.50);
        shedDormer(out, b, 'y', ey0, dy0, z0, zk, u.xc, dw, rTex);
        shedDormer(out, b, 'y', ey1, dy1, z0, zk, u.xc, dw, rTex);
      }
    } else {
      const run = Math.min(hw,hd);
      if(alongX){
        const rx0=ex0+run, rx1=ex1-run;
        quad(out, [ex0,ey0,z0],[ex1,ey0,z0],[rx1,my,zr],[rx0,my,zr], 'roof', 0.12, rTex);
        quad(out, [ex1,ey1,z0],[ex0,ey1,z0],[rx0,my,zr],[rx1,my,zr], 'roof', 0.12, rTex);
        tri(out, [ex0,ey0,z0],[rx0,my,zr],[ex0,ey1,z0], 'roof', 0.02, rTex);
        tri(out, [ex1,ey1,z0],[rx1,my,zr],[ex1,ey0,z0], 'roof', 0.22, rTex);
        boxSolid(out, rx0-0.10, rx1+0.10, my-0.10, my+0.10, zr-0.10, zr+0.03, 'roof', null, 0.30, 0.44);
      } else {
        const ry0=ey0+run, ry1=ey1-run;
        quad(out, [ex0,ey1,z0],[ex0,ey0,z0],[mx,ry0,zr],[mx,ry1,zr], 'roof', 0.12, rTex);
        quad(out, [ex1,ey0,z0],[ex1,ey1,z0],[mx,ry1,zr],[mx,ry0,zr], 'roof', 0.12, rTex);
        tri(out, [ex1,ey0,z0],[mx,ry0,zr],[ex0,ey0,z0], 'roof', 0.02, rTex);
        tri(out, [ex0,ey1,z0],[mx,ry1,zr],[ex1,ey1,z0], 'roof', 0.22, rTex);
        boxSolid(out, mx-0.10, mx+0.10, ry0-0.10, ry1+0.10, zr-0.10, zr+0.03, 'roof', null, 0.30, 0.44);
      }
    }
    // chimney: brick, at the rear third over the stair bay, corbelled on the shingle tier
    const chx = (b.stairX0+b.stairX1)/2, chy = b.yR + b.Ln*0.30;
    const cw = b.tier==='gambrel'?0.86:0.72;
    boxSolid(out, chx-cw/2, chx+cw/2, chy-cw/2, chy+cw/2, b.topZ-0.4, b.ridgeZ+0.92, 'brick', brickTex(), 0.04, 0.30);
    if(b.tier==='gambrel'){      boxSolid(out, chx-cw/2-0.10, chx+cw/2+0.10, chy-cw/2-0.10, chy+cw/2+0.10,
               b.ridgeZ+0.62, b.ridgeZ+0.78, 'brick', null, 0.16, 0.38);
      boxSolid(out, chx-cw/2-0.14, chx+cw/2+0.14, chy-cw/2-0.14, chy+cw/2+0.14,
               b.ridgeZ+0.92, b.ridgeZ+1.02, 'plinth', null, 0.20, 0.42);
    } else {
      boxSolid(out, chx-0.13, chx+0.13, chy-0.13, chy+0.13, b.ridgeZ+0.92, b.ridgeZ+1.22, 'metal', null, 0.24, 0.42);
    }
  }

  function stepW(b){ return b.tier==='gambrel'?1.55:1.85; }

  // a post-and-rail balustrade round a rectangle: posts, bottom rail, top rail, balusters
  function railRing(out, x0,x1, y0,y1, z, h, mat, t, per){
    railRunFull(out, 'x', x0, x1, y0, z, h, mat, t, per);
    railRunFull(out, 'x', x0, x1, y1, z, h, mat, t, per);
    railRunFull(out, 'y', y0, y1, x0, z, h, mat, t, per);
    railRunFull(out, 'y', y0, y1, x1, z, h, mat, t, per);
  }
  function railRunFull(out, ax, a0, a1, plane, z, h, mat, t, per){
    const L=a1-a0; if(L<0.12) return;
    const n=Math.max(2, Math.round(L*per));
    const put=(p0,p1,q0,q1,zz0,zz1,bb)=>boxSolid(out, p0,p1, q0,q1, zz0,zz1, mat, null, bb, 0.34);
    if(ax==='x'){
      put(a0,a1, plane-t/2, plane+t/2, z+h-0.075, z+h, 0.34);        // top rail
      put(a0,a1, plane-t*0.4, plane+t*0.4, z+0.10, z+0.16, 0.24);    // bottom rail
      for(let i=0;i<=n;i++){ const p=a0+L*i/n;
        put(p-t*0.30, p+t*0.30, plane-t*0.30, plane+t*0.30, z+0.14, z+h-0.07, 0.16); }
    } else {
      put(plane-t/2, plane+t/2, a0,a1, z+h-0.075, z+h, 0.34);
      put(plane-t*0.4, plane+t*0.4, a0,a1, z+0.10, z+0.16, 0.24);
      for(let i=0;i<=n;i++){ const p=a0+L*i/n;
        put(plane-t*0.30, plane+t*0.30, p-t*0.30, p+t*0.30, z+0.14, z+h-0.07, 0.16); }
    }
  }
  function railRun(out, ax, a0, a1, plane, z, h, mat, t, per){
    railRunFull(out, ax, a0, a1, plane, z, h, mat, t, per);
  }

  // ---- the stacked STREET PORCH: the silhouette of the class ----------------
  function buildFrontPorch(out, b, litFn){
    if(b.porch==='none') return;
    const glazed = b.porch==='glazed';
    const x0=b.x0+0.10, x1=b.x1-0.10, y0=b.yF, y1=b.yF+b.porchDep;
    const postT=b.tier==='gambrel'?0.20:0.17;
    const entryX = (b.stairX0+b.stairX1)/2;
    for(let s=0;s<b.storeys;s++){
      const zf=b.storeyZ[s], zc=zf+b.storeyH;
      // deck + fascia
      boxSolid(out, x0-0.10, x1+0.10, y0, y1, zf-0.26, zf, 'deck', plankTex(), 0.02, 0.30);
      decalY(out, y1, 1, x0-0.10, x1+0.10, zf-0.28, zf-0.02, 'trim', 0.22, null, true, 0.04);
      // posts at the bay lines
      const posts=[x0, x1, b.stairX0, b.stairX1].concat(b.bays.filter(u=>u.storey===0).map(u=>u.xc));
      for(const px of posts){
        const p=clamp(px, x0, x1);
        boxSolid(out, p-postT/2, p+postT/2, y1-postT, y1, zf, zc-0.16, 'trim', null, 0.22, 0.34);
        // capital + base blocks: what makes a post read as joinery, not a stick
        boxSolid(out, p-postT*0.8, p+postT*0.8, y1-postT*1.4, y1+0.03, zc-0.28, zc-0.16, 'trim', null, 0.30, 0.40);
        boxSolid(out, p-postT*0.8, p+postT*0.8, y1-postT*1.4, y1+0.03, zf, zf+0.14, 'trim', null, 0.26, 0.40);
      }
      // beam over the posts
      boxSolid(out, x0-0.12, x1+0.12, y1-postT-0.04, y1+0.05, zc-0.16, zc, 'trim', null, 0.24, 0.36);
      if(glazed){
        // an enclosed SUN PORCH: a shingled apron, then a BAND of small-pane sashes. The glazing is
        // a band, not a wall — a full-storey light stacks into one glass tower and loses the porch.
        const sillZ = zf+1.06, headZ = zc-0.92;
        boxSolid(out, x0, x1, y1-0.14, y1, zf, sillZ, 'body', shingleTex(), 0.04, 0.30);
        decalY(out, y1, 1, x0, x1, sillZ-0.10, sillZ+0.04, 'trim', 0.28, null, true, 0.05);
        for(const u of b.bays.filter(v=>v.storey===s)){
          const n=3, span=(u.w-0.5)/n;
          for(let q=0;q<n;q++)
            sash(out, b, 'y', y1, 1, u.x0+0.25+span*(q+0.5), span-0.10, sillZ+0.06, headZ-sillZ,
                 { panes:3, lit:litFn(u.i,s,'porch') });
        }
        // the head band under the beam, and the sides of the sun porch
        decalY(out, y1, 1, x0, x1, headZ+0.04, zc-0.18, 'body', 0.06, shingleTex(), false, 0.05);
        for(const xv of [x0,x1]){ const nx=xv<0?-1:1;
          boxSolid(out, xv-(nx<0?0.14:0), xv+(nx>0?0.14:0), y0, y1, zf, sillZ, 'body', shingleTex(), 0.04, 0.30);
          boxSolid(out, xv-(nx<0?0.14:0), xv+(nx>0?0.14:0), y0, y1, headZ+0.04, zc-0.18, 'body', shingleTex(), 0.04, 0.30);
          sash(out, b, 'x', xv+nx*0.02, nx, (y0+y1)/2, b.porchDep-0.8, sillZ+0.06, headZ-sillZ, {panes:3}); }
      } else {
        // an OPEN porch: top rail and square balusters between the posts, the stair bay left open
        for(const seg of (b.perFloor===1 ? [[x0, entryX-stepW(b)/2],[entryX+stepW(b)/2, x1]]
                                         : [[x0, entryX-stepW(b)/2],[entryX+stepW(b)/2, x1]]))
          if(seg[1]-seg[0]>0.3) railRunFull(out, 'x', seg[0], seg[1], y1-postT/2, zf, 1.02, 'trim', 0.085, 4.4);
        for(const xv of [x0,x1]) railRunFull(out, 'y', y0+0.2, y1, xv, zf, 1.02, 'trim', 0.085, 4.4);
        // the ceiling of the porch below, boarded
        slab(out, [[x0-0.10,y0],[x1+0.10,y0],[x1+0.10,y1],[x0-0.10,y1]], zc-0.17, 'trim', -0.16, plankTex());
      }
    }
    // the porch roof over the top deck
    const zt=b.storeyZ[b.storeys-1]+b.storeyH;
    if(glazed){
      boxSolid(out, x0-0.16, x1+0.16, y0, y1+0.10, zt-0.16, zt+0.06, 'roof', plankTex(), 0.20, 0.38);
      railRing(out, x0-0.10, x1+0.10, y0, y1+0.06, zt+0.06, 0.62, 'iron', 0.05, 4.0);
    } else {
      quad(out, [x0-0.26,y1+0.34,zt-0.10],[x1+0.26,y1+0.34,zt-0.10],
                [x1+0.26,y0,zt+0.62],[x0-0.26,y0,zt+0.62], 'roof', 0.18, asphaltTex());
      boxSolid(out, x0-0.26, x1+0.26, y1+0.30, y1+0.40, zt-0.16, zt-0.02, 'trim', null, 0.24, 0.36);
    }
    // steps up to the street door, and the skirt below the lowest deck
    const stW = stepW(b), sz=b.storeyZ[0]-0.26;
    const nst=Math.max(2, Math.round(sz/0.19));
    for(let i=0;i<nst;i++){
      const zz=sz*(i+1)/nst, dy=0.30*(nst-i);
      boxSolid(out, entryX-stW/2, entryX+stW/2, y1+dy-0.30, y1+dy, 0, zz, 'plinth',
               b.plinth==='granite'?graniteTex():rubbleTex(), 0.04, 0.34);
    }
    if(b.skirt!=='none'){
      const sk = b.skirt==='lattice'?latticeTex():boardTex();
      const mat = b.skirt==='lattice'?'trim':'body';
      decalY(out, y1+0.01, 1, x0-0.10, x1+0.10, 0.02, sz, mat, -0.10, sk, false, 0.03);
      for(const xv of [x0-0.10,x1+0.10]) decalX(out, xv, xv<0?-1:1, y0, y1, 0.02, sz, mat, -0.10, sk, false, 0.03);
    }
  }

  // ---- the stacked REAR SERVICE PORCH, with the open back stair -------------
  function buildRearPorch(out, b){
    if(!b.rearPorch) return;
    const x0=b.x0+0.20, x1=b.x1-0.20, y1=b.yR, y0=b.yR-b.rearDep;
    const stairBay = [Math.max(x0,b.stairX0), Math.min(x1,b.stairX1)];
    const postT=0.15;
    for(let s=0;s<b.storeys;s++){
      const zf=b.storeyZ[s], zc=zf+b.storeyH;
      boxSolid(out, x0-0.08, x1+0.08, y0, y1, zf-0.24, zf, 'deck', plankTex(), 0.02, 0.28);
      for(const px of [x0, x1, stairBay[0], stairBay[1]]){
        boxSolid(out, px-postT/2, px+postT/2, y0, y0+postT, zf, zc-0.14, 'trim', null, 0.20, 0.32);
      }
      boxSolid(out, x0-0.10, x1+0.10, y0-0.02, y0+postT+0.02, zc-0.14, zc, 'trim', null, 0.22, 0.34);
      // rails, with the stair bay left open
      for(const seg of [[x0,stairBay[0]],[stairBay[1],x1]])
        if(seg[1]-seg[0]>0.3) railRunFull(out, 'x', seg[0], seg[1], y0+postT/2, zf, 0.98, 'trim', 0.075, 4.0);
      for(const xv of [x0,x1]) railRunFull(out, 'y', y0+0.15, y1, xv, zf, 0.98, 'trim', 0.075, 4.0);
      // slat screen at the party line between the two flats' halves of the porch
      if(b.perFloor===2) for(const xv of [stairBay[0],stairBay[1]])
        decalX(out, xv, xv<0?-1:1, y0+0.1, y1, zf+0.02, zf+1.86, 'trim', -0.06, boardTex(), false, 0.03);
      // the flight down from this landing, in the stair bay
      if(s>0){
        const fx0=stairBay[0]+0.14, fx1=stairBay[1]-0.14;
        const n=Math.max(6, Math.round(b.storeyH/0.19)), run=(b.rearDep-0.34)/n;
        for(let i=0;i<n;i++){
          const zz=zf-(b.storeyH)*(i+1)/n;
          boxSolid(out, fx0,fx1, y0+0.17+run*i, y0+0.17+run*(i+1), zz, zz+0.055, 'deck', plankTex(), 0.06, 0.36);
        }
        railRunFull(out, 'y', y0+0.17, y1-0.1, fx0-0.05, zf-b.storeyH*0.55, 1.0, 'trim', 0.07, 3.0);
      }
    }
    const zt=b.storeyZ[b.storeys-1]+b.storeyH;
    quad(out, [x0-0.24,y0-0.28,zt-0.06],[x1+0.24,y0-0.28,zt-0.06],
              [x1+0.24,y1,zt+0.54],[x0-0.24,y1,zt+0.54], 'roof', 0.16, asphaltTex());
    // ground steps down to the yard
    const sz=b.storeyZ[0]-0.24, nst=Math.max(2, Math.round(sz/0.19));
    for(let i=0;i<nst;i++){
      const zz=sz*(i+1)/nst, dy=0.28*(nst-i);
      boxSolid(out, stairBay[0]+0.1, stairBay[1]-0.1, y0-dy, y0-dy+0.28, 0, zz, 'plinth',
               b.plinth==='granite'?graniteTex():rubbleTex(), 0.04, 0.34);
    }
  }

  // ---- the openings on every elevation, per flat ---------------------------
  function buildFacades(out, b, litFn){
    const yF=b.yF, yR=b.yR;
    const wh = b.tier==='gambrel'?1.52:1.42, ww = b.tier==='gambrel'?1.02:0.94;
    for(const u of b.bays){
      const zf=b.storeyZ[u.storey], sill=zf+0.92;
      const lit=(q)=>litFn(u.i,u.storey,q);
      const hs=u.hallSide, sw=Math.max(1.7, Math.min(2.1, u.w*0.24));
      const hallX = hs>0 ? u.x1-sw/2 : u.x0+sw/2;
      const livC  = hs>0 ? (u.x0 + (u.w-sw)*0.5) : (u.x1 - (u.w-sw)*0.5);
      // STREET: the parlour's two sashes and its porch door, the hall's single sash
      sash(out, b, 'y', yF, 1, livC-(u.w-sw)*0.26, ww, sill, wh, {lit:lit('liv'), blind:0.18+0.5*hash2(u.i,3), storm:b.stormSash});
      sash(out, b, 'y', yF, 1, livC+(u.w-sw)*0.26, ww, sill, wh, {lit:lit('liv'), blind:0.12+0.5*hash2(u.i,5), storm:b.stormSash});
      panelDoor(out, b, 'y', yF, 1, hallX, 0.94, zf, 2.10, {lit:lit('hall')});
      // SIDE (the flat's outer wall): parlour, bed1 x2, bed2
      const ox=u.outerX, on=u.outerN;
      const ys=[yF-2.0, yF-5.2, yF-7.0, yR+1.5];
      const tags=['liv','bed1','bed1','bed2'];
      for(let q=0;q<ys.length;q++)
        sash(out, b, 'x', ox, on, ys[q], ww, sill, wh,
             {lit:lit(tags[q]), blind:0.10+0.6*hash2(u.i*7+q,11), storm:b.stormSash});
      // REAR: kitchen, bath, bed2
      const kx = hs>0 ? u.x1-sw*0.5-0.3 : u.x0+sw*0.5+0.3;
      sash(out, b, 'y', yR, -1, kx, ww, sill, wh*0.86, {lit:lit('kit')});
      sash(out, b, 'y', yR, -1, u.xc, 0.72, sill+0.30, wh*0.62, {lit:lit('bath')});
      sash(out, b, 'y', yR, -1, hs>0?u.x0+u.w*0.22:u.x1-u.w*0.22, ww, sill, wh*0.86, {lit:lit('bed2')});
      // the kitchen's back door onto the service porch
      if(b.rearPorch) panelDoor(out, b, 'y', yR, -1, kx+ (hs>0?-1:1)*0.9, 0.88, zf, 2.04, {});
      // a two-storey canted bay on the shingle tier, on the parlour's side wall
      if(b.bay && u.storey<2 && u.storey<b.storeys-0){
        const by=yF-3.2, bd=0.66, bw=2.55;
        if(u.storey===0){
          const zz0=b.fH-0.06, zz1=b.storeyZ[Math.min(1,b.storeys-1)]+b.storeyH-0.5;
          boxSolid(out, ox+(on>0?0:-bd), ox+(on>0?bd:0), by-bw/2, by+bw/2, zz0, zz1, 'body', shingleTex(), 0.05, 0.28);
          quad(out, [ox+(on>0?bd+0.16:-bd-0.16), by-bw/2-0.16, zz1-0.06],
                    [ox+(on>0?bd+0.16:-bd-0.16), by+bw/2+0.16, zz1-0.06],
                    [ox, by+bw/2+0.16, zz1+0.40],[ox, by-bw/2-0.16, zz1+0.40], 'roof', 0.20, slateTex());
          for(let st=0; st<Math.min(2,b.storeys); st++){
            const zs=b.storeyZ[st]+0.92;
            sash(out, b, 'x', ox+on*bd, on, by, 1.22, zs, wh, {panes:3, lit:lit('liv')});
            for(const sg of [-1,1]) sash(out, b, 'y', by+sg*bw/2, sg, ox+on*bd*0.5, 0.58, zs, wh, {panes:2});
          }
        }
      }
    }
    // the COMMON BAY: the street door with its transom, a landing sash above, a rear service door
    const ex=(b.stairX0+b.stairX1)/2;
    panelDoor(out, b, 'y', yF, 1, ex, b.tier==='gambrel'?1.16:1.06, b.storeyZ[0], 2.24, {transom:true, lit:true});
    for(let s=1;s<b.storeys;s++)
      sash(out, b, 'y', yF, 1, ex, b.tier==='gambrel'?1.12:1.02, b.storeyZ[s]+0.98, wh, {lit:true, panes:3});
    for(let s=0;s<b.storeys;s++){
      sash(out, b, 'y', yR, -1, ex, 0.86, b.storeyZ[s]+1.06, wh*0.72, {lit:s===0});
      if(b.perFloor===1) sash(out, b, 'x', b.x1, 1, 0.0, 0.72, b.storeyZ[s]+1.30, wh*0.6, {lit:false});
    }
    if(b.rearPorch) panelDoor(out, b, 'y', yR, -1, ex-1.0, 0.90, b.storeyZ[0], 2.04, {});
  }

  // ---- the lived-in pass ---------------------------------------------------
  function buildDetail(out, b){
    if(b.detail==='plain') return;
    const rnd=mulberry32(4211 + b.n*17 + Math.round(b.Wd*10));
    const yF=b.yF, yR=b.yR, x0=b.x0, x1=b.x1;
    // rainwater: a downpipe at every corner with a hopper head and bracket lines
    if(b.rainwater) for(const c of [[x0,yR,-1,-1],[x1,yR,1,-1],[x0,yF,-1,1],[x1,yF,1,1]]){
      const px=c[0]+c[2]*0.14, py=c[1]+c[3]*0.14;
      boxSolid(out, px-0.055, px+0.055, py-0.055, py+0.055, 0.06, b.eaveZ-0.06, 'metal', null, 0.14, 0.30);
      boxSolid(out, px-0.13, px+0.13, py-0.13, py+0.13, b.eaveZ-0.34, b.eaveZ-0.10, 'metal', null, 0.20, 0.34);
      boxSolid(out, px-0.10, px+0.10, py-0.10, py+0.10, 0.02, 0.16, 'metal', null, 0.10, 0.30);
    }
    // window boxes under some street and side sashes
    if(b.windowBoxes) for(const u of b.bays){
      if(hash2(u.i,29)<0.42) continue;
      const zf=b.storeyZ[u.storey], sw=Math.max(1.7, Math.min(2.1, u.w*0.24));
      const livC = u.hallSide>0 ? (u.x0 + (u.w-sw)*0.5) : (u.x1 - (u.w-sw)*0.5);
      const bx = livC + (hash2(u.i,31)<0.5?-1:1)*(u.w-sw)*0.26;
      boxSolid(out, bx-0.62, bx+0.62, yF+0.02, yF+0.26, zf+0.72, zf+0.94, 'trim', boardTex(), 0.16, 0.34);
      boxSolid(out, bx-0.56, bx+0.56, yF+0.05, yF+0.24, zf+0.92, zf+1.10, 'plant', null, 0.06, 0.30);
    }
    // entry lamps either side of the street door, and the number plate
    const ex=(b.stairX0+b.stairX1)/2, ez=b.storeyZ[0];
    if(b.entryLamps) for(const sg of [-1,1]){
      const lx=ex+sg*(b.tier==='gambrel'?0.92:0.84);
      boxSolid(out, lx-0.05, lx+0.05, yF+0.02, yF+0.16, ez+2.02, ez+2.34, 'iron', null, 0.18, 0.34);
      boxSolid(out, lx-0.09, lx+0.09, yF+0.10, yF+0.28, ez+2.34, ez+2.58, b.night?'glassHi':'glass', null, 0.30, 0.42);
      boxSolid(out, lx-0.11, lx+0.11, yF+0.08, yF+0.30, ez+2.58, ez+2.66, 'iron', null, 0.22, 0.38);
    }
    if(b.signage){
      decalY(out, yF, 1, ex+0.62, ex+1.02, ez+2.28, ez+2.54, 'trim', 0.40, null, true, 0.14);
      decalY(out, yF, 1, ex+0.70, ex+0.94, ez+2.34, ez+2.48, 'iron', 0.30, null, true, 0.16);
    }
    // meter cabinet + conduit riser: the porch tier wears its services outside
    if(b.meters){
      const mx=b.perFloor===1 ? b.stairX0-0.5 : x1-0.9;
      boxSolid(out, mx-0.30, mx+0.30, yR-0.16, yR, b.fH+0.30, b.fH+1.10, 'metal', null, 0.16, 0.32);
      boxSolid(out, mx-0.045, mx+0.045, yR-0.10, yR, b.fH+1.10, b.storeyZ[b.storeys-1]+1.2, 'metal', null, 0.12, 0.28);
    }
    // laundry lines strung across the rear porch, with a couple of sheets
    if(b.laundryLines && b.rearPorch) for(let s=1;s<b.storeys;s++){
      const zf=b.storeyZ[s]+1.62, py=yR-b.rearDep+0.42;
      for(const yy of [py, py+0.30]){
        boxSolid(out, x0+0.3, x1-0.3, yy-0.022, yy+0.022, zf, zf+0.03, 'iron', null, 0.24, 0.36);
      }
      if(hash2(s,13)<0.75){
        const lx=x0+0.7+(x1-x0-2.0)*hash2(s,17);
        decalY(out, py+0.31, -1, lx, lx+0.92, zf-0.86, zf-0.02, 'linen', 0.22, linenTex(), false, 0.05);
        decalY(out, py+0.31, -1, lx+1.10, lx+1.62, zf-0.62, zf-0.02, 'linen', 0.18, linenTex(), false, 0.05);
      }
    }
    // roof kit: an aerial and vents on the porch tier, a weathervane on the shingle tier
    if(b.roofKit){
      const rx=(b.stairX0+b.stairX1)/2 - 1.4, ry=b.yF-b.Ln*0.28;
      if(b.tier==='porch'){
        boxSolid(out, rx-0.04, rx+0.04, ry-0.04, ry+0.04, b.ridgeZ-0.4, b.ridgeZ+1.35, 'metal', null, 0.16, 0.30);
        for(let i=0;i<4;i++){ const zz=b.ridgeZ+0.55+i*0.20;
          boxSolid(out, rx-0.42+i*0.03, rx+0.42-i*0.03, ry-0.025, ry+0.025, zz, zz+0.04, 'metal', null, 0.22, 0.34); }
        for(const u of b.bays.filter(v=>v.storey===0)){
          const vx=u.xc+0.8;
          boxSolid(out, vx-0.11, vx+0.11, -0.11, 0.11, b.eaveZ+0.1, b.eaveZ+0.52, 'metal', null, 0.18, 0.34);
        }
      } else {
        boxSolid(out, rx-0.035, rx+0.035, ry-0.035, ry+0.035, b.ridgeZ, b.ridgeZ+1.05, 'iron', null, 0.16, 0.30);
        boxSolid(out, rx-0.44, rx+0.20, ry-0.02, ry+0.02, b.ridgeZ+0.86, b.ridgeZ+1.00, 'iron', null, 0.26, 0.36);
        boxSolid(out, rx-0.03, rx+0.03, ry-0.32, ry+0.32, b.ridgeZ+0.62, b.ridgeZ+0.70, 'iron', null, 0.22, 0.34);
      }
    }
    // an oil tank on legs, bins, a cellar bulkhead, a kerb and a drying yard
    const sideX = b.perFloor===1 ? b.stairX1+0.9 : x1+0.9;
    if(b.oilTank){
      boxSolid(out, sideX-0.42, sideX+0.42, yR+1.2, yR+2.7, 0.48, 1.62, 'metal', null, 0.06, 0.28);
      for(const yy of [yR+1.34, yR+2.54]) for(const sg of [-1,1])
        boxSolid(out, sideX+sg*0.32, sideX+sg*0.38, yy-0.05, yy+0.05, 0, 0.50, 'iron', null, 0.10, 0.30);
    }
    if(b.binStore){
      const bx=b.perFloor===1 ? b.x0-0.8 : x0-0.85;
      for(let i=0;i<2;i++){ const yy=yR+0.7+i*0.82;
        boxSolid(out, bx-0.33, bx+0.33, yy-0.33, yy+0.33, 0, 0.94, 'body', boardTex(), 0.02, 0.26);
        boxSolid(out, bx-0.36, bx+0.36, yy-0.36, yy+0.36, 0.94, 1.02, 'iron', null, 0.18, 0.34); }
    }
    if(b.cellarDoor){
      const cx2=b.perFloor===1 ? b.stairX0-1.2 : x0-0.0;
      quad(out, [cx2-0.55, yR-0.02, 0.10],[cx2+0.55, yR-0.02, 0.10],
                [cx2+0.55, yR-1.05, 0.60],[cx2-0.55, yR-1.05, 0.60], 'metal', 0.16, boardTex());
    }
    if(b.dryingYard){
      for(const sg of [-1,1]){
        const px=(b.x0+b.x1)/2 + sg*(b.Wd*0.28);
        boxSolid(out, px-0.06, px+0.06, yR-b.rearDep-2.4, yR-b.rearDep-2.28, 0, 2.05, 'trim', null, 0.14, 0.30);
        boxSolid(out, px-0.42, px+0.42, yR-b.rearDep-2.40, yR-b.rearDep-2.30, 1.88, 1.96, 'trim', null, 0.22, 0.34);
      }
      boxSolid(out, (b.x0+b.x1)/2-b.Wd*0.28, (b.x0+b.x1)/2+b.Wd*0.28,
               yR-b.rearDep-2.36, yR-b.rearDep-2.32, 1.90, 1.93, 'iron', null, 0.24, 0.34);
    }
    if(b.kerb){
      const ky=b.yF+b.porchDep+2.6;
      boxSolid(out, x0-2.2, x1+2.2, ky, ky+0.34, 0, 0.13, 'plinth', graniteTex(), 0.06, 0.30);
      boxSolid(out, x0-2.2, x1+2.2, ky-1.10, ky, 0, 0.05, 'plant', null, 0.02, 0.24);
    }
    // porch furniture: a chair and a stack of crates on one deck, so a porch reads as used
    if(b.dressing){
      const u=b.bays[0], zf=b.storeyZ[u.storey], py=b.yF+b.porchDep*0.55;
      const px=u.xc-0.6;
      boxSolid(out, px-0.24, px+0.24, py-0.24, py+0.24, zf, zf+0.44, 'trim', boardTex(), 0.08, 0.30);
      boxSolid(out, px-0.24, px+0.24, py+0.16, py+0.24, zf+0.44, zf+0.92, 'trim', boardTex(), 0.14, 0.32);
      const qx=u.xc+1.1;
      for(let i=0;i<2;i++) boxSolid(out, qx-0.27, qx+0.27, py-0.27, py+0.27,
        zf+i*0.34, zf+0.34+i*0.34, 'deck', boardTex(), 0.04+i*0.04, 0.28);
      if(rnd()<0.8){
        const hx=b.perFloor===1?b.stairX0-0.6:b.x0+0.6;
        boxSolid(out, hx-0.20, hx+0.20, yR+0.02, yR+0.16, b.fH+0.36, b.fH+0.62, 'iron', null, 0.14, 0.30);
      }
    }
  }

  function build(b){
    const bodyTex = b.tier==='gambrel' ? shingleTex() : clapTex();
    const out=[];
    const rnd = mulberry32(9137 + b.n*31 + Math.round(b.lit*100));
    const litTable={};
    const litFn=(i,s,q)=>{ if(!b.night) return false; const k=i+'|'+s+'|'+q;
      if(litTable[k]==null) litTable[k] = rnd() < b.lit; return litTable[k]; };
    buildShell(out, b, bodyTex);
    buildFacades(out, b, litFn);
    buildRoof(out, b);
    buildRearPorch(out, b);
    buildFrontPorch(out, b, litFn);
    buildDetail(out, b);
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
          out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; }
      }
    }
    const wx=b.weather;
    if(wx>0.02){
      const rnd=mulberry32(7717|((b.Wd*13)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='body'||m==='accent') && rnd()<wx*0.07) out[i]=rbuf[i][Math.max(0,Math.min(rbuf[i].length-1,ibuf[i]-1))];
        if((m==='trim') && rnd()<wx*0.05) out[i]=mix(out[i], '#6b6656', 0.18+rnd()*0.16);
        if((m==='plinth') && rnd()<wx*0.06) out[i]=mix(out[i], '#4d5646', 0.22+rnd()*0.16);   // moss
        if((m==='roof'||m==='deck') && rnd()<wx*0.05) out[i]=mix(out[i], '#47543c', 0.20+rnd()*0.16);
        if(m==='metal' && rnd()<wx*0.05) out[i]=mix(out[i], '#6a4a2c', 0.20+rnd()*0.18);      // rust
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

  // ================= published metrics =================
  function dims(opts){
    const b=resolve(opts||{});
    return { tier:b.tier, units:b.n, storeys:b.storeys, perFloor:b.perFloor,
      Wd:r3(b.Wd), Ln:r3(b.Ln), topZ:r3(b.topZ), eaveZ:r3(b.eaveZ), ridgeZ:r3(b.ridgeZ),
      fH:b.fH, storeyH:b.storeyH, storeyZ:b.storeyZ.map(r3),
      flatW:r3(b.flatW), flatD:r3(b.flatD), stairW:r3(b.stairW),
      porchDep:r3(b.porchDep), rearDep:r3(b.rearDep), wallT:b.wallT, floorT:b.floorT,
      bays:b.bays.map(u=>({ unit:u.unit, storey:u.storey, col:u.col, side:u.side, beds:u.beds,
        x0:r3(u.x0), x1:r3(u.x1) })) };
  }

  // the interior rig measures NOTHING — it reads this
  function unitBox(b, u){
    const t=b.wallT, party=b.partyT, hs=u.hallSide;
    const ix0 = hs>0 ? u.x0 + t      : u.x0 + party/2;
    const ix1 = hs>0 ? u.x1 - party/2 : u.x1 - t;
    const yS  = b.yF - t;                 // inner face of the street wall
    const yRr = b.yR + t;                 // inner face of the rear wall
    const w = ix1-ix0, d = yS-yRr;
    // THE BAND TABLE the flat plan is measured from. A stacked flat is a hall spine down the party
    // side with a full-width parlour at the street, two rooms off the spine, a cross hall, and a
    // three-room service band at the yard.
    const sw    = clamp(w*0.24, 1.70, 2.10);              // the hall spine
    const pD    = clamp(d*0.335, 3.70, 4.35);             // parlour band
    const mD    = clamp(d*0.275, 3.00, 3.60);             // bedroom band
    const cD    = b.tier==='gambrel' ? 1.45 : 1.35;       // cross hall
    const rD    = d - pD - mD - cD;                       // service band at the yard
    const kitW  = clamp(w*0.37, 2.45, 3.25);
    const bathW = clamp(w*0.29, 1.95, 2.45);
    return { ix0, ix1, w, d, yS, yRr, hs, sw, pD, mD, cD, rD, kitW, bathW,
             bed2W: w - kitW - bathW,
             hallX: hs>0 ? ix1-sw/2 : ix0+sw/2,
             entryX: hs>0 ? ix1 : ix0,
             entryY: yS - 2.05 };
  }
  function shell(opts){
    const b=resolve(opts||{});
    const t=b.wallT, party=b.partyT;
    const sx0=b.stairX0+ (b.perFloor===1 ? party/2 : party/2), sx1=b.stairX1 - (b.perFloor===1 ? t : party/2);
    const yS=b.yF-t, yR=b.yR+t;
    return {
      contract:'32 px = 1 m · origin ground-centre of the plot · +y street/front · −y yard · flats run the full depth',
      tier:b.tier, wallT:t, partyWallT:party, floorT:b.floorT, ceilH:r3(b.storeyH-b.floorT),
      fH:b.fH, storeyH:b.storeyH, storeyZ:b.storeyZ.map(r3), roofZ:r3(b.topZ), eaveZ:r3(b.eaveZ),
      storeys:b.storeys, perFloor:b.perFloor,
      block:{ x0:r3(b.x0), x1:r3(b.x1), yRear:r3(b.yR), yFront:r3(b.yF) },
      // NO CORRIDOR. The common bay is a stair sliced front to back: entry at the street, the
      // flight in the middle, the shared laundry and the back door at the yard.
      core:{ x0:r3(sx0), x1:r3(sx1), yStreet:r3(yS), yRear:r3(yR),
        // the vestibule is 2.95 m deep and the flat doors sit at its BACK: a shallow one with a
        // door in every wall leaves the mail bank nowhere to go, which the placer duly reported
        vestibule:{ y0:r3(yS-2.95), y1:r3(yS), rooms:['mailboxes','bench'] },
        stair:{ x0:r3(sx0), x1:r3(sx1), y0:r3(yS-2.95-4.30), y1:r3(yS-2.95) },
        backhall:{ y0:r3(yR+2.55), y1:r3(yS-2.95-4.30) },
        laundry:{ y0:r3(yR), y1:r3(yR+2.55), shared:true },
        frontDoor:{ x:r3((b.stairX0+b.stairX1)/2), y:r3(b.yF), clearW:b.tier==='gambrel'?1.16:1.06, clearH:2.24 },
        rearDoor:{ x:r3((b.stairX0+b.stairX1)/2-1.0), y:r3(b.yR), clearW:0.90, clearH:2.04 },
        lift:false, rearStair:b.rearPorch },
      porches:{
        front: b.porch==='none' ? null : { kind:b.porch, y0:r3(b.yF), y1:r3(b.yF+b.porchDep),
                x0:r3(b.x0+0.10), x1:r3(b.x1-0.10), perStorey:true },
        rear: b.rearPorch ? { kind:'service', y0:r3(b.yR-b.rearDep), y1:r3(b.yR),
                x0:r3(b.x0+0.20), x1:r3(b.x1-0.20), perStorey:true, stair:true } : null,
      },
      units: b.bays.map(u=>{
        const K=unitBox(b,u);
        return { id:u.unit, storey:u.storey, col:u.col, side:u.side, beds:u.beds,
          interior:{ x0:r3(K.ix0), x1:r3(K.ix1), yStreet:r3(K.yS), yRear:r3(K.yRr),
                     w:r3(K.w), d:r3(K.d), hallSide:K.hs },
          bands:{ hallW:r3(K.sw), parlourD:r3(K.pD), bedD:r3(K.mD), crossD:r3(K.cD),
                  serviceD:r3(K.rD), kitW:r3(K.kitW), bathW:r3(K.bathW), bed2W:r3(K.bed2W) },
          // WHICH EDGES SEE DAYLIGHT — the interior rig needs this to keep headboards off glazing
          glazed:{ street:true, rear:true, outer:true, party:false, stair:false },
          openings:{
            entry:{ x:r3(K.entryX), y:r3(K.entryY), clearW:b.tier==='gambrel'?0.98:0.92, clearH:2.08,
                    kind:'flat_entry_door', fromStairLanding:true },
            // the porch is reached from the hall, which is where the street door of the flat sits
            porchDoor: b.porch==='none' ? null
              : { x:r3(K.hallX), y:r3(K.yS), clearW:0.94, clearH:2.10,
                  kind:'porch_door', room:'hall' },
            rearDoor: b.rearPorch
              ? { x:r3(K.hs>0 ? K.ix1-K.kitW*0.5 : K.ix0+K.kitW*0.5), y:r3(K.yRr),
                  clearW:0.88, clearH:2.04, kind:'kitchen_door', room:'kitchen' } : null,
            streetWindows:3, sideWindows:4, rearWindows:3,
          } };
      }),
    };
  }

  // the gameplay unit table the occupancy/schedule pass consumes
  function units(opts){
    const b=resolve(opts||{});
    return b.bays.map(u=>{
      const K=unitBox(b,u);
      const outdoor = [];
      if(b.porch!=='none') outdoor.push(b.porch==='glazed' ? 'sun_porch' : 'front_porch');
      if(b.rearPorch) outdoor.push('rear_porch');
      return { id:u.unit, storey:u.storey, col:u.col, side:u.side, tier:b.tier, beds:u.beds,
        // the room program — baths, bedrooms actually built — is published by
        // Art/stackUnitIsoRig.js. Every flat here is through-plan, so it always has a rear door.
        ensuite:false,
        floorArea_m2: r3(K.w*K.d), sleeps: u.beds===1?2:(u.beds===2?4:6),
        entry:{ from:(u.storey===0?'vestibule':'landing'), storey:u.storey,
                x:r3(K.entryX), y:r3(K.entryY) },
        lift:false, stairs:true, walkUpFloors:u.storey, throughPlan:true,
        privateOutdoor: outdoor.length?outdoor.join('+'):'none',
        sharedAccess: ['vestibule','stair','landing','laundry']
                        .concat(b.rearPorch?['rear_stair','rear_porch']:[])
                        .concat(b.dryingYard?['drying_yard']:[]) };
    });
  }

  function anchors(dir, opts){
    opts=opts||{}; const b=resolve(opts), B=camBasis({dir,elev:opts.elev});
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return {x:Math.round(v.sx),y:Math.round(v.sy)}; };
    const ex=(b.stairX0+b.stairX1)/2, yF=b.yF, yR=b.yR;
    const unitsA=b.bays.map(u=>{
      const K=unitBox(b,u), base=b.storeyZ[u.storey], wins=[];
      const livC = K.hs>0 ? (K.ix0 + (K.w-K.sw)*0.5) : (K.ix1 - (K.w-K.sw)*0.5);
      for(const sg of [-1,1]) wins.push(Object.assign({storey:u.storey, face:'street'},
        pj(livC+sg*(K.w-K.sw)*0.26, yF, base+1.6)));
      for(const yy of [yF-2.0, yF-5.2, yR+1.5]) wins.push(Object.assign({storey:u.storey, face:'side'},
        pj(u.outerX, yy, base+1.6)));
      return { unit:u.unit, storey:u.storey, beds:u.beds,
        entry: pj(K.entryX, K.entryY, base+1.05),
        porch: b.porch==='none' ? null : pj(u.xc, yF+b.porchDep*0.5, base+0.05),
        rearPorch: b.rearPorch ? pj(u.xc, yR-b.rearDep*0.5, base+0.05) : null,
        windows: wins };
    });
    return {
      units: unitsA,
      entry:   pj(ex, yF+b.porchDep+0.35, 0.05),
      stoop:   pj(ex, yF+b.porchDep*0.45, b.storeyZ[0]+0.02),
      mail:    pj(ex-0.9, yF-0.9, b.storeyZ[0]+1.35),
      stair:   pj(ex, yF-4.3, b.storeyZ[0]),
      rearStair: b.rearPorch ? pj(ex, yR-b.rearDep*0.55, b.storeyZ[0]) : null,
      laundry: pj(ex, yR+1.3, b.storeyZ[0]),
      dryingYard: b.dryingYard ? pj(0, yR-b.rearDep-2.35, 0.04) : null,
      bins:    b.binStore ? pj(b.x0-0.85, yR+1.1, 0.04) : null,
      oilTank: b.oilTank ? pj((b.perFloor===1?b.stairX1:b.x1)+0.9, yR+1.95, 1.05) : null,
      chimney: pj(ex, yR+b.Ln*0.30, b.ridgeZ+1.0),
      roofDeck: b.widowWalk ? pj(0, 0, b.ridgeZ+0.06) : null,
      Wd:r3(b.Wd), Ln:r3(b.Ln), topZ:r3(b.topZ), ridgeZ:r3(b.ridgeZ),
    };
  }
  // ---- GAMEPLAY SIDECAR -----------------------------------------------------
  //   StackFlatsIso.gameplayAll(opts)  -> the whole sidecar, interiors merged when the room rig loads
  //   StackFlatsIso.gameplay(opts)     -> the exterior sections alone
  //
  // Generated, never edited. Three hashes are stamped by Art/_sidecarExport.js, one per renderer
  // that can drift (shell, room, props). If any moves, re-run the generator, never patch a number.
  //
  // WHAT A THREE-DECKER IS, IN SECTIONS. It is not a small walk-up: there is no corridor and no
  // lift, the stair bay IS the shared space, and the porches are stacked outdoor rooms that every
  // flat reaches from its own hall. PORCH is present here and nowhere else in the folder.
  function gameplay(opts){
    const o=opts||{}, b=resolve(o), sh=shell(o), us=units(o), lux=b.tier==='gambrel';
    const C=sh.core, yF=b.yF, yR=b.yR, exC=(b.stairX0+b.stairX1)/2;

    const UNITS = us.map((u,i)=>{
      const si=sh.units[i];
      return { id:u.id, storey:u.storey, col:u.col, side:u.side, tier:u.tier,
        beds:u.beds, baths:null, ensuite:u.ensuite, storeys:1,
        floor_area_m2:u.floorArea_m2, resident_slots:u.sleeps,
        interior_box:si.interior, bands:si.bands, glazed:si.glazed,
        // a flat runs the FULL DEPTH of the house, street to yard, and is one level
        single_level:true, through_plan:true,
        level:'storey_'+u.storey, level_z:sh.storeyZ[u.storey],
        entry:{ x:si.openings.entry.x, y:si.openings.entry.y, z:sh.storeyZ[u.storey],
                from:(u.storey===0?'vestibule':'landing'), kind:si.openings.entry.kind },
        party_walls:(b.perFloor===2?1:0),
        private_outdoor:u.privateOutdoor,
        porch: si.openings.porchDoor ? { front:true, rear:!!si.openings.rearDoor } : null,
        shared_access:u.sharedAccess,
        walk_up_floors:u.walkUpFloors, lift_served:false };
    });

    const THRESHOLD = [];
    us.forEach((u,i)=>{
      const si=sh.units[i], op=si.openings.entry;
      THRESHOLD.push({ id:u.id+'.entry', unit:u.id, kind:'flat_entry', storey:u.storey,
        level:'storey_'+u.storey,
        from:(u.storey===0?'vestibule_s0':'landing_s'+u.storey), to:u.id+'_hall',
        axis:'y', plane:op.x, centre:op.y,
        clear_width_m:op.clearW, clear_height_m:op.clearH, sill_z:sh.storeyZ[u.storey],
        mechanism:'hinged_single',
        hinge_axis:{ x:op.x, y:r3(op.y-op.clearW/2), vertical:true },
        swing:{ outward:false, into:'flat', degrees:90,
          keep_clear:[[r3(op.x-op.clearW*(si.interior.hallSide>0?-1:1)),r3(op.y-op.clearW/2)],
                      [r3(op.x),r3(op.y+op.clearW/2)]] },
        default_state:'shut', self_closing:false, leaf:op.kind });
      if(si.openings.porchDoor){ const p=si.openings.porchDoor;
        THRESHOLD.push({ id:u.id+'.porch_door', unit:u.id, kind:'porch_door', storey:u.storey,
          level:'storey_'+u.storey, from:u.id+'_hall', to:u.id+'_porch',
          axis:'x', plane:p.y, centre:p.x, clear_width_m:p.clearW, clear_height_m:p.clearH,
          sill_z:sh.storeyZ[u.storey], mechanism:'hinged_single',
          hinge_axis:{ x:r3(p.x-p.clearW/2), y:p.y, vertical:true },
          swing:{ outward:false, into:'hall', degrees:90 },
          default_state:'shut', leaf:'half_glazed' }); }
      if(si.openings.rearDoor){ const p=si.openings.rearDoor;
        THRESHOLD.push({ id:u.id+'.rear_door', unit:u.id, kind:'kitchen_door', storey:u.storey,
          level:'storey_'+u.storey, from:u.id+'_kitchen', to:u.id+'_rear_porch',
          axis:'x', plane:p.y, centre:p.x, clear_width_m:p.clearW, clear_height_m:p.clearH,
          sill_z:sh.storeyZ[u.storey], mechanism:'hinged_single',
          hinge_axis:{ x:r3(p.x-p.clearW/2), y:p.y, vertical:true },
          swing:{ outward:true, into:'rear_porch', degrees:95 },
          default_state:'shut', leaf:'half_glazed',
          note:'every flat is through-plan, so every flat has its own back door' }); }
    });
    THRESHOLD.push({ id:'block.front_door', unit:null, kind:'street_entry', storey:0,
      level:'storey_0', from:'outside', to:'vestibule_s0', axis:'x',
      plane:C.frontDoor.y, centre:C.frontDoor.x,
      clear_width_m:C.frontDoor.clearW, clear_height_m:C.frontDoor.clearH, sill_z:b.fH,
      mechanism:'hinged_single',
      hinge_axis:{ x:r3(C.frontDoor.x-C.frontDoor.clearW/2), y:C.frontDoor.y, vertical:true },
      swing:{ outward:true, degrees:95,
        keep_clear:[[r3(C.frontDoor.x-C.frontDoor.clearW/2),r3(C.frontDoor.y)],
                    [r3(C.frontDoor.x+C.frontDoor.clearW/2),r3(C.frontDoor.y+C.frontDoor.clearW)]] },
      default_state:'shut', secured:false, leaf:'half_glazed',
      note:'opens onto the front porch, not onto grade — the porch is the landing' });
    THRESHOLD.push({ id:'block.rear_door', unit:null, kind:'service_entry', storey:0,
      level:'storey_0', from:'outside', to:'laundry_s0', axis:'x',
      plane:C.rearDoor.y, centre:C.rearDoor.x,
      clear_width_m:C.rearDoor.clearW, clear_height_m:C.rearDoor.clearH, sill_z:b.fH,
      mechanism:'hinged_single',
      hinge_axis:{ x:r3(C.rearDoor.x-C.rearDoor.clearW/2), y:C.rearDoor.y, vertical:true },
      swing:{ outward:true, degrees:95 }, default_state:'shut', leaf:'boarded' });

    // ---- the shared rooms: a stair bay, not a corridor
    const SHARED = [];
    for(let s=0;s<b.storeys;s++){
      SHARED.push({ id:'core.'+(s===0?'vestibule':'landing')+'_s'+s,
        kind:(s===0?'vestibule':'landing'), level:'storey_'+s, storey:s,
        polygon:[[C.x0,C.vestibule.y0],[C.x1,C.vestibule.y0],[C.x1,C.vestibule.y1],[C.x0,C.vestibule.y1]],
        z:sh.storeyZ[s], serves:'storey',
        note:s===0 ? 'the mail bank and the flat doors off it' : 'the flat doors on this plate open off it' });
      SHARED.push({ id:'core.stairhall_s'+s, kind:'stairhall', level:'storey_'+s, storey:s,
        polygon:[[C.x0,C.stair.y0],[C.x1,C.stair.y0],[C.x1,C.stair.y1],[C.x0,C.stair.y1]],
        z:sh.storeyZ[s], serves:'all', means_of_escape:true,
        note:'the only stair in the building; the flight takes 1.10 m of it and the rest is landing' });
    }
    SHARED.push({ id:'core.laundry', kind:'shared_laundry', level:'storey_0', storey:0,
      polygon:[[C.x0,C.laundry.y0],[C.x1,C.laundry.y0],[C.x1,C.laundry.y1],[C.x0,C.laundry.y1]],
      z:sh.storeyZ[0], serves:'all',
      note:'the porch tier keeps no machine in the flat, so this room is on a route every day' });

    // ---- PORCH: the stacked outdoor rooms that are the silhouette of the class
    const PORCH = [];
    if(sh.porches.front) for(let s=0;s<b.storeys;s++) PORCH.push({
      id:'porch_front_s'+s, kind:b.porch==='glazed'?'sun_porch':'open_porch',
      level:'storey_'+s, storey:s, z:r3(sh.storeyZ[s]),
      polygon:[[sh.porches.front.x0,sh.porches.front.y0],[sh.porches.front.x1,sh.porches.front.y0],
               [sh.porches.front.x1,sh.porches.front.y1],[sh.porches.front.x0,sh.porches.front.y1]],
      depth_m:r3(b.porchDep), enclosed:b.porch==='glazed',
      balustrade: b.porch==='glazed' ? null : { height_m:0.92, infill:'square_baluster', cap:'timber' },
      shared_with: b.perFloor===2 ? 'both flats on this storey' : 'the flat on this storey',
      roofed:true, walkable:true,
      note:'a real room you can stand on, one per storey, stacked. The ground one is also the '+
           'landing the front door opens onto.' });
    if(sh.porches.rear) for(let s=0;s<b.storeys;s++) PORCH.push({
      id:'porch_rear_s'+s, kind:'service_porch', level:'storey_'+s, storey:s,
      z:r3(sh.storeyZ[s]),
      polygon:[[sh.porches.rear.x0,sh.porches.rear.y0],[sh.porches.rear.x1,sh.porches.rear.y0],
               [sh.porches.rear.x1,sh.porches.rear.y1],[sh.porches.rear.x0,sh.porches.rear.y1]],
      depth_m:r3(b.rearDep), enclosed:false,
      balustrade:{ height_m:0.92, infill:'square_baluster', cap:'timber' },
      roofed:s<b.storeys-1, walkable:true,
      note:'the kitchen door lands here; the rear stair connects every one of them to the yard' });

    // ---- the exterior flights: the front steps and the rear stair
    const EXT_STAIRS = [];
    // Exact here too: the rise published is grade-to-first-floor divided by the tread count, NOT a
    // rounded figure. r3() on the gambrel tier's 1.06 / 6 gave 0.177, and 6 x 0.177 = 1.062 — the
    // top tread 2 mm above the porch it lands on. Same defect as the manor's rounded flight.
    const frontRise = b.fH;
    const frontN = Math.max(2, Math.round(frontRise/0.17));
    EXT_STAIRS.push({ id:'block.front_steps', kind:'entry_steps', shared:true,
      from_level:'grade', to_level:'storey_0',
      steps:frontN, rise_m:frontRise/frontN, run_m:0.31, floor_rise_m:r3(frontRise),
      exact:{ rule:'steps x rise_m = floor_rise_m', product:r3(frontN*(frontRise/frontN)) },
      footprint:[[r3(exC-1.05),r3((sh.porches.front?sh.porches.front.y1:yF))],
                 [r3(exC+1.05),r3((sh.porches.front?sh.porches.front.y1:yF)+1.00)]],
      lands_on: sh.porches.front ? 'porch_front_s0' : 'vestibule_s0',
      handrail:{ sides:'both', height_m:0.88, material:'timber' } });
    if(b.rearPorch){
      // One run per storey, and the first is NOT like the rest: grade to the first floor is fH,
      // every flight above it a full storey rise. Published per flight, each with its own exact
      // rise so steps x rise = that flight's rise to the bit — the rule the core stair obeys. One
      // averaged figure here would put a landing out by most of a tread.
      const flights=[];
      for(let s=0;s<b.storeys;s++){
        const rise = s===0 ? b.fH : r3(sh.storeyZ[s]-sh.storeyZ[s-1]);
        const n = Math.max(2, Math.round(rise/0.19));
        flights.push({ index:s, from_level:s===0?'grade':'storey_'+(s-1), to_level:'storey_'+s,
          steps:n, rise_m:rise/n, run_m:0.27, floor_rise_m:r3(rise),
          exact:{ rule:'steps x rise_m = floor_rise_m', product:r3(n*(rise/n)) },
          lands_on:'porch_rear_s'+s });
      }
      EXT_STAIRS.push({ id:'block.rear_stair', kind:'service_stair', shared:true,
        from_level:'grade', to_level:'storey_'+(b.storeys-1),
        flight_count:flights.length, flights, run_m:0.27,
        total_rise_m:r3(sh.storeyZ[b.storeys-1]),
        footprint:[[r3(exC-1.20),r3(yR-b.rearDep)],[r3(exC+0.20),r3(yR)]],
        handrail:{ sides:'open_side', height_m:0.90, material:'timber' },
        note:'the back stair: every rear porch, every storey, straight down to the yard. It is the '+
             'second means of escape and the route the laundry actually uses.' });
    }

    const BLOCKERS = [{ what:'building', level:'grade', treatment:'wall',
      footprint:[[r3(b.x0),r3(yR)],[r3(b.x1),r3(yF)]], height_above_grade_m:r3(b.eaveZ),
      note:'the house shell; interior plates are walkable via SOLE, porches via PORCH' }];
    if(b.skirt!=='none') BLOCKERS.push({ what:'skirt', level:'grade', treatment:'wall',
      footprint:[[r3(b.x0),r3(yR)],[r3(b.x1),r3(yF)]], height_above_grade_m:r3(b.fH),
      note:b.skirt+' skirt closing the crawl space under the first floor' });
    if(b.oilTank) BLOCKERS.push({ what:'oil_tank', level:'grade', treatment:'wall',
      footprint:[[r3((b.perFloor===1?b.stairX1:b.x1)+0.35),r3(yR+1.35)],
                 [r3((b.perFloor===1?b.stairX1:b.x1)+1.45),r3(yR+2.55)]],
      height_above_grade_m:1.55 });
    if(b.binStore) BLOCKERS.push({ what:'bins', level:'grade', treatment:'waist_block',
      footprint:[[r3(b.x0-1.25),r3(yR+0.55)],[r3(b.x0-0.45),r3(yR+1.65)]],
      height_above_grade_m:1.05 });
    if(b.dryingYard) BLOCKERS.push({ what:'drying_yard', level:'grade', treatment:'flat',
      footprint:[[r3(b.x0),r3(yR-b.rearDep-4.2)],[r3(b.x1),r3(yR-b.rearDep)]],
      height_above_grade_m:0.01,
      note:'walkable ground with two line posts; the lines are overhead at 2.05 m' });

    const ROOF = { deck_z:r3(b.topZ), ridge_z:r3(b.ridgeZ), eave_z:r3(b.eaveZ),
      form:b.roof, material:b.roofMat, walkable:!!b.widowWalk,
      access: b.widowWalk ? { kind:'roof_hatch', from:'stairhall_s'+(b.storeys-1),
        footprint:[[r3(exC-0.55),r3(-0.55)],[r3(exC+0.55),r3(0.55)]], height_m:0.0,
        note:'a hatch and a ship\'s ladder off the top landing' } : null,
      deck: b.widowWalk ? { id:'block.widows_walk', kind:'widows_walk', z:r3(b.ridgeZ+0.06),
        polygon:[[r3(-2.10),r3(-2.10)],[r3(2.10),r3(-2.10)],[r3(2.10),r3(2.10)],[r3(-2.10),r3(2.10)]],
        balustrade:{ height_m:0.94, infill:'turned_baluster', cap:'timber' } } : null,
      note: b.widowWalk ? 'a captain\'s walk on the ridge: small, railed, and genuinely standable'
                        : 'pitched roof, no deck and no hatch — nothing up here is walkable' };

    return { UNITS, THRESHOLD, SHARED, PORCH, EXT_STAIRS, BLOCKERS, ROOF };
  }

  function gameplayAll(opts){
    const o=opts||{}, b=resolve(o), dm=dims(o), sh=shell(o);
    const ext=gameplay(o);
    const IN=root.StackUnitIso;
    const inner=(IN&&IN.gameplaySections) ? IN.gameplaySections(Object.assign({},o,{focus:'all',explode:0})) : null;
    const out={
      schema:'hidden-harbours/building-gameplay@1',
      rig:'Art/stackFlatsIsoRig.js',
      exportSymbol:'globalThis.StackFlatsIso',
      interiorRig:'Art/stackUnitIsoRig.js',
      propRig:'Art/interiorPropRig.js',
      variant:b.tier+'_'+b.n+'u_'+b.beds+'bed_'+b.storeys+'st_'+b.perFloor+'pf',
      generator:'StackFlatsIso.gameplayAll(opts)',
      phase:'multi-unit phase 3 — the three-decker stack',
      build:{ tier:b.tier, units:b.n, perFloor:b.perFloor, beds:b.beds, mix:b.mix,
        storeys:b.storeys, body:b.body, accent:b.accent, roof:b.roof, roofMat:b.roofMat,
        porch:b.porch, skirt:b.skirt, plinth:b.plinth, bay:b.bay, dormers:b.dormers,
        widowWalk:b.widowWalk, rearPorch:b.rearPorch, dryingYard:b.dryingYard },
      frame:{ units:'metres', scale_px_per_m:PX, origin:'ground centre of the plot footprint',
        axes:'+x across the front, +y street, −y yard, +z up', heading_independent:true,
        cell:{ w:W, h:H, pivot:{ x:cx, y:groundY } },
        note:'the interior rig paints into this same cell and pivot, so a flat registers inside its storey to the pixel' },
      building:{ width_m:dm.Wd, depth_m:dm.Ln, storeys:b.storeys, unit_count:b.n,
        units_per_floor:b.perFloor, wall_thickness_m:sh.wallT,
        party_wall_thickness_m:sh.partyWallT, floor_thickness_m:sh.floorT,
        storey_height_m:b.storeyH, ceiling_height_m:sh.ceilH, ground_floor_z:sh.fH,
        storey_z:sh.storeyZ, roof_z:sh.roofZ, eave_z:sh.eaveZ,
        core:sh.core, porches:sh.porches },
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
      out.STAIRS=(inner.STAIRS||[]).concat(out.EXT_STAIRS||[]);
      delete out.EXT_STAIRS;
      out.INTERACT=inner.INTERACT;
      out.BLOCKERS=out.BLOCKERS.concat(inner.BLOCKERS);
      out.reach_audit=inner.REACH_AUDIT;
      for(const u of out.UNITS){
        const sleeps=inner.INTERACT.filter(x=>x.unit===u.id && x.verb==='sleep');
        const baths=inner.SOLE.filter(x=>x.unit===u.id && x.kind==='bath');
        u.nominal_occupancy=u.resident_slots;
        u.resident_slots=sleeps.length;
        u.bedrooms_built=new Set(sleeps.map(x=>x.room)).size;
        u.baths=baths.length;
        u.sleep_anchors=sleeps.map(x=>x.id);
      }
      if(inner._unplaced&&inner._unplaced.length) out._unplaced=inner._unplaced;
      out._interiorNote=inner._interiorNote;
    } else { out.STAIRS=out.EXT_STAIRS; delete out.EXT_STAIRS; }
    out._excluded={
      WASHBOARD:'Not a hull.', CLEATS:'Not a hull.',
      ELEVATOR:'A three-decker is walk-up by definition. There is no shaft, no pit and no overrun '+
        'in the model. The walk-up block rig is the phase that carries ELEVATOR.',
      CORRIDOR:'There is no corridor. Every flat door opens off the vestibule or the landing '+
        'directly, which is what makes the plan a stair bay rather than a block.',
      LOBBY:'A vestibule is not a lobby: 2.95 m deep, mail on one wall, flat doors at the back. '+
        'It is published in SHARED as a vestibule so nothing reads it as a lounge.',
      lockers:'No basement and no cages. Storage is the flat\'s own store room and the back porch.',
      private_stair:'A flat is single-level. The front stair and, where fitted, the rear stair are '+
        'the only vertical routes and both are shared.',
      walkable_roof: b.widowWalk ? undefined : 'A pitched roof with no deck, no hatch and no ladder.',
      site:'The rig bakes the house, not the lot. The drying yard is published because the rig '+
        'draws its posts; fences, paths and planting are the scene\'s.',
    };
    out._confirm={
      interiors: inner ? undefined : 'Art/stackUnitIsoRig.js was not loaded, so SOLE, STAIRS and INTERACT are ABSENT. Load the room rig and regenerate.',
      resident_slots:'UNITS[].resident_slots is the count of SLEEP anchors the interior actually built. nominal_occupancy is the figure units() derives from the bed request. Trust resident_slots.',
      porch_occupancy:'A two-flats-per-storey build gives BOTH households the same porch. Whether that is shared ground or split down the middle is a gameplay ruling, not a rig one.',
      rear_stair:'The rear stair is published as one run per storey with a declared going. The bake draws the flights; the landing-by-landing geometry is not measured off pixels.',
      widows_walk:'The deck is given as a 4.2 m square on the ridge. The bake draws its rail, not its framing, so the polygon is declared.',
      flat_door_swing:'Flat doors are published opening INTO the flat. The bake parks every interior leaf flat against its wall, so this is a rule, not a measurement.',
    };
    return out;
  }

  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.StackFlatsIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    TIERS, BODY, RUBBLE, GRANITE, TRIM, IRON, BRICK, ASPH, SLATE, COPPER, DECKW, PLANT, LINEN,
    ROOFS, PORCHES, SKIRTS, PLINTHS, PRESETS, KEY, KEYLINE_DEFAULT,
    dims, shell, unitBox, units, render, anchors, project,
    gameplay, gameplayAll };
})(typeof globalThis!=='undefined'?globalThis:window);
