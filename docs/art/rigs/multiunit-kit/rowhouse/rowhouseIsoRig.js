/* Hidden Harbours — parametric ISO ROWHOUSE / TOWNHOUSE-TERRACE rig (ADR-0006 bake pipeline, SAME
   turntable + camera + shading as houseIsoRig.js / interiorIsoRig.js / wharfBuildingRig.js / the
   fleet). ONE parametric 3D TERRACE — a run of 2..6 attached units built as bays off one loft — baked
   to pixel sheets through the SHARED 3/4 camera: 45deg steps, elev 40deg default, flat-facet shading,
   z-buffered, ordered dither, per-face uv texture, depth-edge darkening, NO AA. 32 px = 1 m.
   All 8 facings fall out of one model.

   THE ROW IS A LIST OF BAYS. Every unit is one bay: its own width (set by its bed count), its own
   storey count, its own front-wall Y (the stagger), its own door paint, its own windows. Nothing in
   the rig knows "four" — cornice, parapet, coping, chimneys, downpipes, stoops and balconies are all
   generated per bay, so a 2-unit duplex-terrace and a 6-unit row are the same model with a different
   list. This is the gas-station rule restated for housing: a terrace is a LIST OF UNITS.

   TWO TIERS, one loft:
     PLAN DRIVES THE SHELL. Bay widths and unit depth are sized from what a real unit plan needs —
   a stair spine wide enough for a flight plus a landing that a door can clear, a front room deep
   enough for a bed, and a rear room deep enough for a kitchen and a table. See rowhouseUnitIsoRig.

   basic    brick mill-town terrace — flat roof behind a corbelled cornice + parapet with stone
              coping, granite plinth/water table, stone sills, segmental brick arches, panelled doors
              with transom lights on stone stoops with iron rails, party-wall pilasters + chimney
              stacks on the party lines, cast-iron downpipes at the joints, areaway sash at grade.
     luxury   contemporary coastal block — standing-seam metal + cedar screen bays over a render base,
              deep recessed entries under metal canopies, wide aluminium sash, glass balconies with
              metal caps on every upper storey, slim fascia + shallow parapet, optional roof deck with
              a stair bulkhead. Bigger bays, taller storeys, no stacks.

   BUILDER SURFACE (every axis resolved per render, no re-modelling):
     tier:'basic'|'luxury'   units:2..6   beds:1|2|3   mix:'uniform'|'mixed'|'stepped'
     storeys:2|3   clad:'brick'|'paintedBrick'|'render'|'standingSeam'|'cedar'
     body / accent: BODY ramp keys   roofForm:'parapet'|'mono'   entry:'stoop'|'recessed'
     balcony:'none'|'juliet'|'glass'   stagger:0..1   chimneys:0..5   downpipes:bool
     ends:'blank'|'window'   areaway:bool   roofDeck:bool   doorPaint:'varied'|'uniform'
     winStyle:'twoOverTwo'|'sixOverSix'|'oneOverOne'|'wide'   lit:0..1 (night occupancy)
     weather:0..1   night:bool   elev:deg   outline:bool (ADR-0031 A/B, default OFF)

   REGISTRATION CONTRACT for the interior pass (per the station precedent — the interior rig measures
   nothing, it reads the shell): shell(opts) publishes wall thickness, floor and ceiling heights per
   storey, every unit's interior footprint, and every opening (door / window / balcony door) in the
   SAME metres this bake uses, so a unit interior registers under its bay to the pixel.
   units(opts) publishes the gameplay unit table — id, beds, storeys, floor area, entry, party walls.
   anchors(dir,opts) reports door / mail / stoop / window / balcony / chimney / vent / roof points in
   cell px per facing, so lit windows, smoke and NPC entry cues are runtime overlays on baked points.

   LIGHT: matches the shipped neighbours (houseIsoRig / interiorIsoRig / the fleet) — upper-left key,
   LN = normalize([-0.42,0.72,0.52]). NOTE FOR THE OWNER: the art bible §1 rules the canonical key is
   top-of-frame, not upper-left, and asks that upper-left rigs be corrected before their next bake. A
   terrace stands in the same street as houseIsoRig, so matching the shipped houses was chosen over
   matching the doc; correcting it is a one-constant change and a whole-set decision, not this rig's.
   KEYLINE: ringless by default (ADR-0031); {outline:true} is kept as a live A/B.

   PALETTE: every ramp is either lifted VERBATIM from the rig that owns it (houseIsoRig, vanIsoRig,
   wharfBuildingRig, plazaRig, wharfIsoRig/roadPathRig concrete) or derived in-file through
   yardIsoRig's rampFrom()/mix() from a canonical step, with the source named on the line. No ramp is
   forked and no hue is invented — the road-path v1->v3 correction, restated for housing.

   Exposes globalThis.RowhouseIso = { W,H,PX,DIRS,pivot,order,defaultElev, TIERS,CLADS,BODY,STONE,
     TRIM,IRON,DOORPAINTS,WINDOWS,ROOFFORMS,ENTRIES,BALCONIES,PRESETS, dims(opts), shell(opts),
     units(opts), render(dir,opts), anchors(dir,opts), project(dir,p,elev) }. */
(function (root) {
  const PX = 32, S = 32;
  const W = 1320, H = 1160, cx = 660, groundY = 745;
  const DEG = Math.PI / 180;
  const DEFAULT_ELEV = 40;
  const KEYLINE_DEFAULT = false;

  // ---- colour helpers, declared before the palettes so derived ramps can use them --------------
  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const h=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+h(r)+h(g)+h(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }
  // yardIsoRig.js:127 — build a 6-step ramp around one colour, value structure of the master ramps.
  // The sanctioned way to add a body colour; every ramp below that is not verbatim goes through it,
  // seeded from a step of a ramp the harbour already owns.
  function rampFrom(hex){
    return [mix(hex,'#141a18',0.62), mix(hex,'#141a18',0.38), mix(hex,'#141a18',0.14),
            hex, mix(hex,'#f4f1e4',0.2), mix(hex,'#f4f1e4',0.42)];
  }

  // ---- palettes, dark -> light. VERBATIM from the rig that already owns each ramp; anything else
  //      is derived here through rampFrom()/mix() from a canonical step, with its source named.
  //      Nothing is forked and no hue is invented (the road-path v1->v3 correction, restated).
  const CREAM  = ['#8a6f3c','#a6884b','#c2a35f','#d8bd7c','#e9d59d','#f5e7c1']; // houseIsoRig · vanIsoRig BODY.cream
  const TEAL   = ['#123a3a','#1b4d4b','#26635e','#357b73','#4d968b','#6cb1a4']; // vanIsoRig BODY.teal
  const ASPHB  = ['#2a211a','#3a2e23','#4c3d2e','#5f4d3a','#736046','#877254']; // houseIsoRig ROOFS.asphaltBrown
  const STONE  = ['#33343a','#42444b','#54575d','#666a70','#7a7e84'];           // houseIsoRig · wharfBuildingRig
  const BODY = {
    redBrick:     ['#3a201a','#552b20','#6e3728','#874634','#a05743','#b96b55'], // houseIsoRig · interiorIsoRig BRICK
    paintedWhite: ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'], // houseIsoRig BODY.white
    paintedCream: CREAM,
    paintedSage:  ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'], // houseIsoRig BODY.sage
    asphaltGrey:  ['#23262b','#2e333a','#3c424a','#4c535c','#5d6570','#6f7883'], // houseIsoRig ROOFS.asphaltGrey
    galv:         ['#464d51','#5a6267','#727c81','#8c979c','#a6b1b5','#c2cccf'], // plazaRig · wharfBuildingRig GALV
    cedar:        ['#4f3a24','#63492d','#785a39','#8f7049','#a6875d','#bd9f74'], // houseIsoRig · wharfBuildingRig WOOD
    buffBrick:    rampFrom(CREAM[1]),   // derived · cream step 1
    brownBrick:   rampFrom(ASPHB[3]),   // derived · asphaltBrown step 3
    greyBrick:    rampFrom(STONE[2]),   // derived · stone step 2
    sandRender:   rampFrom(CREAM[2]),   // derived · cream step 2
    seaGlass:     rampFrom(TEAL[2]),    // derived · teal step 2
  };
  const TRIM   = ['#9aa09a','#b4b8b0','#ccd0c7','#e0e2da','#eef0e8','#f8f9f2']; // houseIsoRig TRIM
  const CONC   = ['#55564f','#686962','#7c7d74','#919287','#a6a79b','#bbbcae']; // wharfIsoRig · shipyardIsoRig · yardIsoRig · roadPathRig CONCRETE
  const IRON   = ['#111216','#1c1e23','#2a2d33','#3a3e46','#4d525a','#636970']; // vanIsoRig IRON
  const MEMB   = BODY.asphaltGrey;                                              // flat-roof membrane = asphalt grey
  const GLASSD = ['#33474d','#40585f','#547078'];                               // wharfBuildingRig day glass
  const GLASSN = ['#7a4f18','#b98a2f','#eed07a'];                               // wharfBuildingRig night glass
  const GLASS_HI = '#cfe6e8';                                                   // houseIsoRig catch-light
  const BALGLASS = TEAL.map(c=>mix(c, GLASS_HI, 0.42));  // derived · canonical teal lifted toward the catch-light
  const KEY = '#1a1c22';

  // door paints: the harbour paint set under its existing names (vanIsoRig BODY), plus wharf STEEL
  const DOORPAINTS = {
    teal:  TEAL,
    red:   ['#4a130f','#671b14','#88271c','#a33124','#bd4230','#d25a42'], // houseIsoRig · vanIsoRig BODY.red
    gold:  ['#5e4a12','#7c6119','#987a26','#b39440','#c8ab5e','#dbc182'], // vanIsoRig BODY.gold
    plum:  ['#2e2333','#3f3047','#523f5d','#664f73','#7d648b','#9079a1'], // vanIsoRig BODY.plum
    sage:  ['#3a4636','#4a5843','#5c6b52','#718063','#889777','#a1ae90'], // houseIsoRig BODY.sage
    blue:  ['#33454a','#43585d','#556d72','#6a848a','#849ea3','#a3b9bd'], // houseIsoRig · vanIsoRig BODY.blue
    steel: ['#2a2f33','#3c454b','#525c63','#6d777e','#889298','#a2acb1'], // wharfBuildingRig · plazaRig STEEL
  };
  const DOORKEYS = Object.keys(DOORPAINTS);

  const CLADS = ['brick','paintedBrick','render','standingSeam','cedar'];
  const WINDOWS = ['twoOverTwo','sixOverSix','oneOverOne','wide'];
  const ROOFFORMS = ['parapet','mono'];
  const ENTRIES = ['stoop','recessed'];
  const BALCONIES = ['none','juliet','glass'];
  const TIERS = {
    basic: {
      clad:'brick', body:'redBrick', accent:'greyBrick', roofForm:'parapet', entry:'stoop',
      balcony:'none', winStyle:'twoOverTwo', storeyH:3.00, fH:0.62, parapetH:0.95, Ln:10.6,
      bayW:{1:5.0,2:5.6,3:6.2}, chimneys:3, downpipes:true, areaway:true, roofDeck:false,
      doorPaint:'varied', weather:0.34,
    },
    luxury: {
      clad:'standingSeam', body:'asphaltGrey', accent:'cedar', roofForm:'mono', entry:'recessed',
      balcony:'glass', winStyle:'wide', storeyH:3.35, fH:0.32, parapetH:0.58, Ln:11.6,
      bayW:{1:5.6,2:6.3,3:7.0}, chimneys:0, downpipes:false, areaway:false, roofDeck:true,
      doorPaint:'uniform', weather:0.08,
    },
  };

  // presets are NAMED TERRACES — the row the street knows by sight
  const PRESETS = {
    millRow4:      { tier:'basic',  units:4, beds:2, mix:'uniform', storeys:2, body:'redBrick',     clad:'brick',        stagger:0,    weather:0.38 },
    millRow4Tall:  { tier:'basic',  units:4, beds:3, mix:'uniform', storeys:3, body:'buffBrick',    clad:'brick',        stagger:0,    weather:0.30, chimneys:3 },
    paintedRow4:   { tier:'basic',  units:4, beds:2, mix:'mixed',   storeys:2, body:'paintedCream', clad:'paintedBrick', stagger:0.55, weather:0.26 },
    duplexPair:    { tier:'basic',  units:2, beds:3, mix:'uniform', storeys:2, body:'greyBrick',    clad:'brick',        stagger:0,    weather:0.42, chimneys:1 },
    coastalRow4:   { tier:'luxury', units:4, beds:3, mix:'uniform', storeys:3, body:'asphaltGrey',  clad:'standingSeam', stagger:0.35, weather:0.06, accent:'cedar' },
    coastalRow4Low:{ tier:'luxury', units:4, beds:2, mix:'mixed',   storeys:2, body:'seaGlass',     clad:'standingSeam', stagger:0.7,  weather:0.10, accent:'cedar' },
  };

  // ---- shading constants (fleet recipe) ----
  const GAIN = 3.1, BIAS = 2.55, EDGE = 0.16;
  const LN = (() => { const v=[-0.42,0.72,0.52]; const m=Math.hypot(...v); return v.map(c=>c/m); })();
  const BAYER = [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]].map(r=>r.map(v=>(v+0.5)/16));

  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}
  function r3(v){ return Math.round(v*1000)/1000; }
  function hash2(a,b){ let h=(a*374761393 + b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }

  // ---- camera / projection (identical to houseIsoRig, so terraces composite with the street) ----
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
  // solid axis box — 5 faces (no underside), optional side tex
  function boxSolid(out, x0,x1, y0,y1, z0,z1, mat, tex, b, topB){
    wall(out, x0,y0, x1,y0, z0,z1, mat, tex, b);   // -Y
    wall(out, x1,y1, x0,y1, z0,z1, mat, tex, b);   // +Y
    wall(out, x1,y0, x1,y1, z0,z1, mat, tex, b);   // +X
    wall(out, x0,y1, x0,y0, z0,z1, mat, tex, b);   // -X
    slab(out, [[x0,y0],[x1,y0],[x1,y1],[x0,y1]], z1, mat, (b||0)+(topB!=null?topB:0.25), tex);
  }
  // hollow band — cornice / parapet / coping. Four bars round the edge instead of one filled box,
  // so the top reads as a coping strip and the roof deck inside stays visible. Internal bars at a
  // party line are skipped (the neighbour's own band covers them).
  function bandRing(out, x0,x1, y0,y1, t, z0,z1, mat, tex, b, topB, left, right){
    boxSolid(out, x0, x1, y1-t, y1, z0,z1, mat, tex, b, topB);
    boxSolid(out, x0, x1, y0, y0+t, z0,z1, mat, tex, b, topB);
    if(left)  boxSolid(out, x0, x0+t, y0+t, y1-t, z0,z1, mat, tex, b, topB);
    if(right) boxSolid(out, x1-t, x1, y0+t, y1-t, z0,z1, mat, tex, b, topB);
  }
  // ---- decals on wall planes (sills, casings, glass, doors) ------------------
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

  // ---- cladding textures: return an integer ramp delta ------------------------
  function cladTex(kind){
    if(kind==='brick' || kind==='paintedBrick'){
      const CO=0.086, BR=0.235, soft=(kind==='paintedBrick');
      return (u,v)=>{ const row=Math.floor(v/CO), f=((v%CO)+CO)%CO;
        const off=(row&1)*BR*0.5, su=(((u+off)%BR)+BR)%BR;
        if(f < 0.026) return soft?-1:-2;                       // bed joint
        if(su < 0.024) return soft?0:-1;                       // perp joint
        if(soft) return 0;
        const t=hash2(Math.floor((u+off)/BR), row);            // mill brick is mottled
        if(t<0.14) return -1; if(t>0.93) return 1;
        return 0; };
    }
    if(kind==='render'){
      return (u,v)=>{ const t=hash2(Math.floor(u*7), Math.floor(v*7)); return t<0.12?-1:(t>0.94?1:0); };
    }
    if(kind==='standingSeam'){
      const SP=0.44;
      return (u,v)=>{ const su=((u%SP)+SP)%SP;
        if(su < 0.035) return -2;                              // seam shadow
        if(su < 0.075) return 1;                               // raised rib catching the key
        return 0; };
    }
    if(kind==='cedar'){
      const SP=0.095;
      return (u,v)=>{ const su=((u%SP)+SP)%SP;
        if(su < 0.028) return -3;                              // shadow gap between battens
        const t=hash2(Math.floor(u/SP), Math.floor(v*5));      // grain
        return t<0.16?-1:(t>0.9?1:0); };
    }
    return null;
  }
  function stoneTex(){
    const BW=0.62, BH=0.31;
    return (u,v)=>{ const row=Math.floor(v/BH), off=(row&1)*BW*0.5;
      const f=((v%BH)+BH)%BH, su=(((u+off)%BW)+BW)%BW;
      if(f<0.03 || su<0.03) return -2;
      const t=hash2(Math.floor((u+off)/BW), row);
      return t<0.2?-1:(t>0.88?1:0); };
  }
  function membraneTex(){
    return (u,v)=>{ const t=hash2(Math.floor(u*5), Math.floor(v*5)); return t<0.18?-1:0; };
  }
  function treadTex(){
    return (u,v)=>{ const t=hash2(Math.floor(u*6), Math.floor(v*6)); return t<0.15?-1:0; };
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

  // ---- resolve: the row becomes a LIST OF BAYS -------------------------------
  function bedsFor(mix, i, n, beds){
    if(mix==='mixed')   return (i%2===0) ? beds : Math.max(1, beds-1);
    if(mix==='stepped') return Math.max(1, Math.min(3, beds - Math.floor(i/2)));
    return beds;
  }
  function resolve(opts){
    opts = opts||{};
    const tierKey = (opts.tier==='luxury') ? 'luxury' : 'basic';
    const T = TIERS[tierKey];
    const g = (k,d)=> opts[k]!=null ? opts[k] : (T[k]!=null ? T[k] : d);
    const n = Math.max(2, Math.min(6, Math.round(opts.units!=null?opts.units:4)));
    const beds = Math.max(1, Math.min(3, Math.round(opts.beds!=null?opts.beds:2)));
    const mix = opts.mix || 'uniform';
    const storeys = Math.max(2, Math.min(3, Math.round(g('storeys',2))));
    const storeyH = g('storeyH',3.0);
    const fH = g('fH',0.6);
    const parapetH = g('parapetH',0.95);
    const Ln = g('Ln',8.8);
    const stagger = opts.stagger!=null ? opts.stagger : 0;
    const doorPaint = g('doorPaint','varied');
    const rnd = mulberry32(9137 + n*31 + beds*7 + (tierKey==='luxury'?911:0));

    // bay widths from bed counts, then a global squeeze so the widest row still fits the cell
    const raw=[]; let sum=0;
    for(let i=0;i<n;i++){ const bd=bedsFor(mix,i,n,beds); const w=T.bayW[bd]||5.2; raw.push({bd,w}); sum+=w; }
    const MAXW = 38.0;
    const k = sum>MAXW ? MAXW/sum : 1;
    const Wd = sum*k;
    const bays=[]; let x=-Wd/2;
    for(let i=0;i<n;i++){
      const w=raw[i].w*k, bd=raw[i].bd;
      const st = (mix==='stepped' && storeys===3 && i>=n-1) ? 2 : storeys;
      const yOff = (i%2===1) ? stagger*0.75 : 0;
      const topZ = fH + st*storeyH;
      const dk = doorPaint==='uniform' ? DOORKEYS[0] : DOORKEYS[Math.floor(rnd()*DOORKEYS.length)];
      bays.push({ i, x0:x, x1:x+w, w, xc:x+w/2, beds:bd, storeys:st, yOff,
                  yf:Ln/2+yOff, topZ, parapetZ:topZ+parapetH, door:dk,
                  doorX: x + (tierKey==='luxury' ? Math.min(1.9, w*0.30)*0.5 + 0.12 : w*0.30) });
      x+=w;
    }
    const b = {
      tier:tierKey, n, beds, mix, storeys, storeyH, fH, parapetH, Ln, Wd, bays, stagger,
      clad: g('clad','brick'), body: opts.body || T.body, accent: opts.accent || T.accent,
      roofForm: g('roofForm','parapet'), entry: g('entry','stoop'),
      balcony: g('balcony','none'), winStyle: g('winStyle','twoOverTwo'),
      chimneys: opts.chimneys!=null ? Math.max(0,Math.min(5,Math.round(opts.chimneys))) : T.chimneys,
      downpipes: g('downpipes',true), areaway: g('areaway',false), roofDeck: g('roofDeck',false),
      ends: opts.ends || 'window', doorPaint,
      lit: opts.lit!=null ? opts.lit : 0.55,
      weather: opts.weather!=null ? opts.weather : T.weather,
      night: !!opts.night,
      outline: opts.outline!=null ? !!opts.outline : KEYLINE_DEFAULT,
      wallT: 0.30,
    };
    b.wallH = fH + storeys*storeyH;
    b.topZ = Math.max(...bays.map(v=>v.topZ));
    b.parapetZ = b.topZ + parapetH;
    b.storeyZ = []; for(let s=0;s<storeys;s++) b.storeyZ.push(fH + s*storeyH);
    return b;
  }

  // ---- materials (+ weathering / night ramp transforms) ----------------------
  function makeMats(b){
    const wx=b.weather, night=b.night;
    const wthBody=(ramp)=>ramp.map(c=>{ let x=desat(c, wx*0.48); x=mix(x,'#6b675e',wx*0.30); if(night)x=mix(x,'#1b2733',0.44); return x; });
    const wthHard=(ramp)=>ramp.map(c=>{ let x=desat(c, wx*0.30); x=mix(x,'#6f6a5f',wx*0.16); if(night)x=mix(x,'#1b2733',0.42); return x; });
    const nightOnly=(ramp)=>ramp.map(c=> night?mix(c,'#1b2733',0.38):c );
    const bodyR = BODY[b.body]||BODY.redBrick, accR = BODY[b.accent]||BODY.cedar;
    const M = {
      body:      { ramp: wthBody(bodyR) },
      accent:    { ramp: wthBody(accR) },
      stone:     { ramp: wthHard(STONE) },
      conc:      { ramp: wthHard(CONC) },
      trim:      { ramp: wthHard(TRIM) },
      iron:      { ramp: nightOnly(IRON) },
      roof:      { ramp: wthHard(MEMB) },
      metalTrim: { ramp: wthHard(BODY.galv) },
      balGlass:  { ramp: nightOnly(BALGLASS) },
      glass:     { ramp: night?GLASSN:GLASSD },
      glassOff:  { ramp: night?nightOnly(GLASSD):GLASSD },
      glassHi:   { ramp: [night?'#f6dfa6':GLASS_HI] },
      cavity:    { ramp: ['#0d1013','#141a1e','#1b2328','#232c32'] },
    };
    for(const k of DOORKEYS) M['door_'+k] = { ramp: wthHard(DOORPAINTS[k]) };
    return M;
  }

  // ---- window / door assemblies (crisp flat quads: panes stay legible) ------
  function sash(out, axis, plane, nrm, c, z, ww, wh, b, litGlass, style){
    const put=putter(axis,plane,nrm);
    const fr=0.085, fm=(b.tier==='luxury')?'metalTrim':'trim';
    const gmat = litGlass ? 'glass' : (b.night?'glassOff':'glass');
    put(out, c-ww/2-fr, c+ww/2+fr, z-fr, z+wh+fr, fm, 0.30, 0.05);                // casing
    put(out, c-ww/2, c+ww/2, z, z+wh, gmat, b.night?1.6:0.0, 0.07);              // glass field
    // muntins / mullions
    const st = style||'twoOverTwo';
    const vSplit = st==='sixOverSix'?2 : (st==='twoOverTwo'?1 : (st==='wide'?2:0));
    const hSplit = st==='sixOverSix'?[0.34,0.67] : (st==='wide'?[0.5]:[0.5]);
    for(const r of (st==='oneOverOne'?[0.5]:hSplit))
      put(out, c-ww/2, c+ww/2, z+wh*r-0.022, z+wh*r+0.022, fm, 0.28, 0.085);
    for(let v=1;v<=vSplit;v++){ const u=c-ww/2+ww*v/(vSplit+1);
      put(out, u-0.02, u+0.02, z, z+wh, fm, 0.28, 0.085); }
    // pane catch-light, top-left of the opening
    put(out, c-ww/2+0.06, c-ww/2+ww*0.26, z+wh*0.66, z+wh*0.88, 'glassHi', 0, 0.09);
    // sill
    if(b.tier==='basic'){
      put(out, c-ww/2-0.20, c+ww/2+0.20, z-0.15, z-0.02, 'stone', 0.2, 0.02, null, false);
      // segmental brick arch — stepped voussoirs approximating the curve
      const segs=5, rise=0.20;
      for(let s2=0;s2<segs;s2++){
        const t0=s2/segs, t1=(s2+1)/segs, mid=(t0+t1)/2;
        const h=rise*Math.sin(Math.PI*mid)*1.0;
        put(out, c-ww/2-0.06+ww*1.12*t0, c-ww/2-0.06+ww*1.12*t1, z+wh+fr, z+wh+fr+0.12+h, 'body', 0.45, 0.03, null, false);
      }
    } else {
      put(out, c-ww/2-0.12, c+ww/2+0.12, z-0.10, z-0.01, 'metalTrim', 0.25, 0.02, null, false);
      put(out, c-ww/2-0.12, c+ww/2+0.12, z+wh+fr, z+wh+fr+0.09, 'metalTrim', 0.25, 0.02, null, false);
    }
  }
  function panelDoor(out, axis, plane, nrm, c, z0, dw, dh, doorMat, b){
    const put=putter(axis,plane,nrm);
    put(out, c-dw/2-0.10, c+dw/2+0.10, z0, z0+dh+0.10, 'trim', 0.35, 0.05);        // surround
    put(out, c-dw/2, c+dw/2, z0, z0+dh, doorMat, 0.2, 0.07);                       // leaf
    put(out, c-dw/2+0.10, c+dw/2-0.10, z0+0.18, z0+dh*0.46, doorMat, -0.7, 0.085); // lower panels
    put(out, c-dw/2+0.10, c+dw/2-0.10, z0+dh*0.52, z0+dh-0.12, doorMat, -0.7, 0.085);
    put(out, c+dw/2-0.20, c+dw/2-0.12, z0+dh*0.44, z0+dh*0.50, 'metalTrim', 1.2, 0.10); // knob
    put(out, c-dw/2-0.10, c+dw/2+0.10, z0+dh+0.10, z0+dh+0.52, 'trim', 0.35, 0.05);  // transom light
    put(out, c-dw/2, c+dw/2, z0+dh+0.16, z0+dh+0.46, b.night?'glass':'glass', b.night?1.6:0.0, 0.07);
  }
  function glazedDoor(out, axis, plane, nrm, c, z0, dw, dh, b, lit){
    const put=putter(axis,plane,nrm);
    put(out, c-dw/2-0.07, c+dw/2+0.07, z0, z0+dh+0.07, 'metalTrim', 0.25, 0.05);
    put(out, c-dw/2, c+dw/2, z0+0.05, z0+dh, lit?'glass':(b.night?'glassOff':'glass'), b.night?1.6:0.0, 0.07);
    put(out, c-0.02, c+0.02, z0+0.05, z0+dh, 'metalTrim', 0.25, 0.085);
    put(out, c-dw/2+0.06, c-dw/2+dw*0.3, z0+dh*0.55, z0+dh*0.85, 'glassHi', 0, 0.09);
    put(out, c+dw/2-0.14, c+dw/2-0.09, z0+dh*0.42, z0+dh*0.62, 'metalTrim', 1.3, 0.10);
  }

  // ---- one bay -------------------------------------------------------------
  function buildBay(out, b, bay, tex, sTex, mTex, litFn){
    const { x0, x1, w, xc, yf, topZ, parapetZ, storeys } = bay;
    const Ln=b.Ln, rear=-Ln/2, lux=b.tier==='luxury';
    const bodyTex=tex, first=bay.i===0, last=bay.i===b.n-1;

    // shell
    wall(out, x0,yf, x1,yf, 0, topZ, 'body', bodyTex);                 // front (+Y)
    wall(out, x1,rear, x0,rear, 0, topZ, 'body', bodyTex);             // rear (-Y)
    if(first) wall(out, x0,yf, x0,rear, 0, topZ, 'body', bodyTex);     // -X end
    if(last)  wall(out, x1,rear, x1,yf, 0, topZ, 'body', bodyTex);     // +X end
    // stagger reveal: the side of a forward bay that its neighbour does not cover
    const nb=b.bays[bay.i+1];
    if(nb && Math.abs(nb.yf-yf)>0.01){
      const fwd = yf>nb.yf ? bay : nb;
      const back = yf>nb.yf ? nb : bay;
      wall(out, x1, back.yf, x1, fwd.yf, 0, Math.min(fwd.topZ,topZ), 'body', bodyTex, 0.15);
    }

    // plinth / water table
    if(lux){
      boxSolid(out, x0-0.05,x1+0.05, rear-0.05, yf+0.05, 0, b.fH, 'conc', null, -0.1);
    } else {
      boxSolid(out, x0-0.07,x1+0.07, rear-0.07, yf+0.07, 0, b.fH, 'stone', sTex, -0.15, 0.2);
    }

    // cornice + parapet + coping
    if(lux){
      bandRing(out, x0-0.10,x1+0.10, rear-0.10, yf+0.10, 0.42, topZ-0.14, topZ, 'metalTrim', null, 0.15, 0.2, first, last);
      bandRing(out, x0-0.02,x1+0.02, rear-0.02, yf+0.02, 0.30, topZ, parapetZ-0.07, 'body', bodyTex, 0.1, 0.2, first, last);
      bandRing(out, x0-0.09,x1+0.09, rear-0.09, yf+0.09, 0.48, parapetZ-0.07, parapetZ, 'metalTrim', null, -0.15, 0.1, first, last);
    } else {
      bandRing(out, x0-0.09,x1+0.09, rear-0.09, yf+0.09, 0.50, topZ-0.34, topZ-0.18, 'body', bodyTex, 0.45, 0.3, first, last);
      bandRing(out, x0-0.17,x1+0.17, rear-0.17, yf+0.17, 0.58, topZ-0.18, topZ, 'body', bodyTex, 0.5, 0.3, first, last);
      bandRing(out, x0-0.05,x1+0.05, rear-0.05, yf+0.05, 0.34, topZ, parapetZ-0.11, 'body', bodyTex, 0.05, 0.2, first, last);
      bandRing(out, x0-0.14,x1+0.14, rear-0.14, yf+0.14, 0.62, parapetZ-0.11, parapetZ, 'stone', sTex, 0.05, 0.1, first, last);
    }

    // roof deck inside the parapet (mono slope on the luxury tier). Inset + dropped 3 cm so it never
    // shares depth with the parapet band (that seam showed as a dashed line at the rear).
    if(b.roofForm==='mono' && lux){
      const drop=0.55, i=0.06;
      quad(out, [x0-0.03,yf-i,topZ-0.03], [x1+0.03,yf-i,topZ-0.03], [x1+0.03,rear+i,topZ-drop], [x0-0.03,rear+i,topZ-drop], 'roof', 0.8, mTex);
    } else {
      const i=0.06;
      slab(out, [[x0-0.03,yf-i],[x1+0.03,yf-i],[x1+0.03,rear+i],[x0-0.03,rear+i]], topZ-0.03, 'roof', 0.75, mTex);
    }

    // party-wall expression: a brick pilaster on the joint line (basic only)
    if(!lux && !last){
      boxSolid(out, x1-0.16, x1+0.16, yf-0.02, yf+0.13, 0, topZ-0.34, 'body', bodyTex, 0.6);
    }

    // ---- windows, per storey
    const wsty = b.winStyle;
    for(let s=0;s<storeys;s++){
      const base=b.fH + s*b.storeyH;
      if(lux){
        // cedar entry band on the left of the bay, glazing and balcony on the living side
        const band=Math.min(1.9, w*0.30), lw=w-band, lx=x0+band;
        const winC = lx + lw*0.82, ww=Math.min(1.75, lw*0.36), wh=2.05, sill=base+0.55;
        const bcx = lx + lw*0.40;
        sash(out,'y',yf,1, winC, sill, ww, wh, b, litFn(bay.i,s,0), 'wide');
        if(s>0 && b.balcony!=='none'){
          glazedDoor(out,'y',yf,1, bcx, base+0.06, 1.10, 2.25, b, litFn(bay.i,s,1));
        } else {
          sash(out,'y',yf,1, bcx, sill, Math.min(2.1, lw*0.46), wh, b, litFn(bay.i,s,1), 'wide');
        }
        // rear: one wide + one narrow per storey
        sash(out,'y',rear,-1, xc - w*0.18, base+0.6, Math.min(2.0,w*0.36), 1.9, b, litFn(bay.i,s,2), 'wide');
        sash(out,'y',rear,-1, xc + w*0.26, base+0.7, 0.95, 1.6, b, litFn(bay.i,s,3), 'oneOverOne');
      } else {
        const ww=0.95, wh=s===0?1.80:1.68, sill=base+(s===0?0.98:0.88);
        const cnt = s===0 ? (bay.beds>=3?2:1) : (bay.beds>=3?3:2);
        const span = w-1.5, x00 = s===0 ? xc + w*0.10 : x0+0.75;
        for(let q=0;q<cnt;q++){
          const c = s===0 ? (x00 + (q-(cnt-1)/2)*1.35) : (x0 + 0.75 + span*(cnt===1?0.5:q/(cnt-1)));
          sash(out,'y',yf,1, c, sill, ww, wh, b, litFn(bay.i,s,q), wsty);
        }
        // rear elevation: two per storey, plainer
        sash(out,'y',rear,-1, xc-w*0.24, base+0.95, ww, wh, b, litFn(bay.i,s,8), wsty);
        sash(out,'y',rear,-1, xc+w*0.24, base+0.95, ww, wh, b, litFn(bay.i,s,9), wsty);
      }
      // end-wall sash on the two end bays
      if(b.ends==='window' && (first||last)){
        const xv = first? x0 : x1, nrm = first? -1 : 1;
        sash(out,'x',xv,nrm, lux? -Ln*0.12 : Ln*0.06, base+(lux?0.6:0.95), lux?1.7:0.95, lux?1.9:1.68, b, litFn(bay.i,s,7), lux?'wide':wsty);
      }
    }

    // ---- entry
    const dMat = 'door_'+bay.door;
    if(lux && b.entry==='recessed'){
      const band=Math.min(1.9, w*0.30), bx0=x0+0.12, bx1=x0+band-0.06;
      const ex=bay.doorX, eh=2.55;
      // the cedar screen band: grade to the underside of the cornice, the tier's warm accent
      decalY(out, yf,1, bx0, bx1, 0.02, topZ-0.22, 'accent', 0.15, cladTex('cedar'), false, 0.02);
      // shadowed entry notch inside the band, with the leaf set into it
      decalY(out, yf,1, bx0+0.10, bx1-0.10, 0, eh, 'cavity', 0, null, true, 0.05);
      glazedDoor(out,'y',yf,1, ex, 0.04, Math.min(1.15, band-0.55), 2.30, b, true);
      // canopy over the entry + concrete landing and step
      boxSolid(out, bx0-0.16, bx1+0.16, yf, yf+1.10, eh+0.06, eh+0.24, 'metalTrim', null, 0.15);
      boxSolid(out, ex-1.05, ex+1.05, yf, yf+1.00, 0, Math.max(0.12,b.fH), 'conc', null, -0.05, 0.15);
      boxSolid(out, ex-1.05, ex+1.05, yf+1.00, yf+1.34, 0, Math.max(0.06,b.fH-0.16), 'conc', null, -0.1, 0.1);
    } else {
      const dx=bay.doorX, dh=2.10, dw=1.02;
      panelDoor(out,'y',yf,1, dx, b.fH, dw, dh, dMat, b);
      // stone stoop: three treads + cheek walls + iron rail
      const treads=3, rise=b.fH/treads, run=0.32, sw=1.70;
      for(let t=0;t<treads;t++){
        const z1=b.fH-t*rise;
        boxSolid(out, dx-sw/2, dx+sw/2, yf+t*run, yf+(t+1)*run+0.02, 0, z1, 'stone', stoneTex(), -0.1, 0.3);
      }
      for(const sgn of [-1,1]){
        const rx=dx+sgn*(sw/2-0.06);
        boxSolid(out, rx-0.035, rx+0.035, yf+0.06, yf+0.12, b.fH, b.fH+0.86, 'iron', null, 0.4);
        boxSolid(out, rx-0.035, rx+0.035, yf+treads*run-0.06, yf+treads*run, 0.10, 0.10+0.86, 'iron', null, 0.4);
        // handrail run, following the nose of the treads
        quad(out, [rx-0.03, yf+0.09, b.fH+0.86], [rx-0.03, yf+treads*run-0.03, 0.96],
                  [rx+0.03, yf+treads*run-0.03, 0.96], [rx+0.03, yf+0.09, b.fH+0.86], 'iron', 0.7);
      }
      // wall-mounted mailbox + number plate beside the door
      decalY(out, yf,1, dx+dw/2+0.16, dx+dw/2+0.46, b.fH+1.05, b.fH+1.32, 'metalTrim', 0.7, null, true, 0.06);
      decalY(out, yf,1, dx-dw/2-0.34, dx-dw/2-0.14, b.fH+1.45, b.fH+1.66, 'trim', 0.9, null, true, 0.06);
      // areaway sash at grade
      if(b.areaway){
        decalY(out, yf,1, xc+w*0.24, xc+w*0.24+0.72, 0.12, 0.50, 'cavity', 0, null, true, 0.05);
        decalY(out, yf,1, xc+w*0.24, xc+w*0.24+0.72, 0.14, 0.48, b.night?'glassOff':'glass', 0, null, true, 0.06);
      }
    }

    // ---- balconies on the upper storeys (luxury)
    if(lux && b.balcony!=='none'){
      for(let s=1;s<storeys;s++){
        const base=b.fH + s*b.storeyH;
        const band=Math.min(1.9, w*0.30), lw=w-band, lx=x0+band;
        const bx=lx + lw*0.40, bw=Math.min(2.7, lw*0.62);
        const deep = b.balcony==='glass' ? 1.15 : 0.28;
        boxSolid(out, bx-bw/2, bx+bw/2, yf, yf+deep, base-0.16, base+0.02, 'conc', null, 0.05, 0.2);
        const rh=1.02;
        // glass front panel, cedar cheeks, metal cap rail
        quad(out, [bx-bw/2, yf+deep, base+0.02], [bx+bw/2, yf+deep, base+0.02],
                  [bx+bw/2, yf+deep, base+rh],  [bx-bw/2, yf+deep, base+rh], 'balGlass', 0.0);
        for(const sgn of [-1,1]){
          const px=bx+sgn*bw/2;
          boxSolid(out, px-0.055, px+0.055, yf+0.05, yf+deep, base, base+rh, 'accent', cladTex('cedar'), 0.1);
        }
        boxSolid(out, bx-bw/2-0.06, bx+bw/2+0.06, yf+deep-0.05, yf+deep+0.05, base+rh, base+rh+0.07, 'metalTrim', null, 0.2);
      }
    }

    // ---- downpipe on the joint (basic)
    if(b.downpipes && !last){
      boxSolid(out, x1-0.07, x1+0.07, yf+0.02, yf+0.16, 0, topZ-0.22, 'iron', null, 0.25);
    }
    // ---- roof furniture
    if(!lux){
      boxSolid(out, xc-0.16, xc+0.16, -Ln*0.16, -Ln*0.16+0.32, topZ, topZ+0.55, 'iron', null, 0.35);   // vent stack
      boxSolid(out, xc+0.55, xc+1.35, -Ln*0.02, -Ln*0.02+0.80, topZ, topZ+0.34, 'stone', null, -0.05, 0.2); // roof hatch
    } else if(bay.i===0 && b.roofDeck){
      boxSolid(out, x0+0.5, x0+2.3, rear+0.6, rear+2.2, topZ, topZ+2.25, 'body', bodyTex, 0.1);      // stair bulkhead
      boxSolid(out, x0+0.42, x0+2.38, rear+0.52, rear+2.28, topZ+2.25, topZ+2.38, 'metalTrim', null, 0.15);
    }
  }

  // ---- chimney stacks on the party lines (basic) -----------------------------
  function buildStacks(out, b, tex, sTex){
    if(b.tier!=='basic' || !b.chimneys) return;
    const joints=[]; for(let i=0;i<b.n-1;i++) joints.push(b.bays[i].x1);
    if(!joints.length) joints.push(0);
    const wanted=Math.min(b.chimneys, joints.length);
    const step=joints.length/wanted;
    for(let q=0;q<wanted;q++){
      const jx=joints[Math.min(joints.length-1, Math.round(q*step))];
      const y0=-b.Ln*0.10, z0=b.topZ, z1=b.parapetZ+1.35;
      boxSolid(out, jx-0.42, jx+0.42, y0-0.30, y0+0.30, z0, z1, 'body', tex, 0.05);
      boxSolid(out, jx-0.52, jx+0.52, y0-0.40, y0+0.40, z1, z1+0.16, 'stone', sTex, 0.1, 0.25);
      for(const sgn of [-1,1]) boxSolid(out, jx+sgn*0.20-0.09, jx+sgn*0.20+0.09, y0-0.09, y0+0.09, z1+0.16, z1+0.46, 'stone', null, 0.1, 0.2);
    }
  }

  function build(b){
    const out=[];
    const tex=cladTex(b.clad), sTex=stoneTex(), mTex=membraneTex();
    const rnd=mulberry32(4711 + b.n*17 + Math.round(b.lit*100));
    const litTable={};
    const litFn=(i,s,q)=>{ if(!b.night) return false; const k=i+'|'+s+'|'+q;
      if(litTable[k]==null) litTable[k] = rnd() < b.lit; return litTable[k]; };
    for(const bay of b.bays) buildBay(out, b, bay, tex, sTex, mTex, litFn);
    buildStacks(out, b, tex, sTex);
    return out;
  }

  // ---- weathering / night post pass + RGBA -----------------------------------
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
      const rnd=mulberry32(2211|((b.Wd*13)|0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='body'||m==='accent') && rnd()<wx*0.06){ out[i]=rbuf[i][Math.max(0,Math.min(rbuf[i].length-1,ibuf[i]-1))]; }
        if(m==='stone' && rnd()<wx*0.05){ out[i]=mix(out[i], '#4d5646', 0.22+rnd()*0.14); }
        if(m==='roof' && rnd()<wx*0.03){ out[i]=mix(out[i], '#47543c', 0.24+rnd()*0.14); }
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
    if(b.outline){                                    // ADR-0031: gated A/B, OFF by default
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
    const MATS=makeMats(b);
    const faces=build(b);
    return toRGBA(post(paint(faces, {dir, elev:opts.elev}, MATS), b));
  }

  // ---- published metrics ----------------------------------------------------
  function dims(opts){
    const b=resolve(opts||{});
    return { tier:b.tier, units:b.n, Wd:b.Wd, Ln:b.Ln, wallH:b.wallH, topZ:b.topZ, parapetZ:b.parapetZ,
      fH:b.fH, storeyH:b.storeyH, storeys:b.storeys, storeyZ:b.storeyZ.slice(), wallT:b.wallT,
      bays:b.bays.map(v=>({ i:v.i, x0:+v.x0.toFixed(3), x1:+v.x1.toFixed(3), w:+v.w.toFixed(3),
        beds:v.beds, storeys:v.storeys, yFront:+v.yf.toFixed(3), topZ:+v.topZ.toFixed(3) })) };
  }
  // the interior pass measures NOTHING — it reads this (station precedent)
  function shell(opts){
    const b=resolve(opts||{});
    const t=b.wallT, party=0.26;
    return {
      contract:'32 px = 1 m · origin ground-centre of the row footprint · +y street/front · +x along the row',
      wallT:t, partyWallT:party, floorT:0.24, ceilH:b.storeyH-0.24, fH:b.fH, storeyZ:b.storeyZ.slice(),
      roofZ:b.topZ, parapetZ:b.parapetZ, tier:b.tier,
      units: b.bays.map(v=>{
        const ix0=v.x0+(v.i===0?t:party/2), ix1=v.x1-(v.i===b.n-1?t:party/2);
        return { id:'unit_'+(v.i+1), bay:v.i, beds:v.beds, storeys:v.storeys,
          interior:{ x0:+ix0.toFixed(3), x1:+ix1.toFixed(3), yFront:+(v.yf-t).toFixed(3), yRear:+(-b.Ln/2+t).toFixed(3) },
          openings:{
            entry:{ x:+v.doorX.toFixed(3), y:+v.yf.toFixed(3), z:+(b.tier==='luxury'?0.04:b.fH).toFixed(3),
                    clearW:b.tier==='luxury'?1.15:1.02, clearH:b.tier==='luxury'?2.35:2.10,
                    kind:b.tier==='luxury'?'glazed_single':'panelled_single' },
            frontWindows:v.storeys, rearWindows:v.storeys*2,
            balconyDoors: (b.tier==='luxury'&&b.balcony!=='none') ? v.storeys-1 : 0,
          } };
      }),
    };
  }
  // the gameplay unit table the occupancy/schedule pass consumes
  function units(opts){
    const b=resolve(opts||{}), t=b.wallT, party=0.26;
    return b.bays.map(v=>{
      const iw=v.w-(v.i===0||v.i===b.n-1?t+party/2:party), id=b.Ln-2*t;
      const area=+(iw*id*v.storeys).toFixed(1);
      return { id:'unit_'+(v.i+1), bay:v.i, tier:b.tier, beds:v.beds, storeys:v.storeys,
        baths: b.tier==='luxury' ? (v.beds>=2?2:1) : (v.beds>=3?2:1),
        ensuite: b.tier==='luxury' && v.beds>=2,
        floorArea_m2:area, sleeps:v.beds===1?2:(v.beds===2?3:5),
        entry:{ side:'front', x:+v.doorX.toFixed(3), y:+v.yf.toFixed(3) },
        partyWalls:[v.i>0?'unit_'+v.i:null, v.i<b.n-1?'unit_'+(v.i+2):null].filter(Boolean),
        privateOutdoor: b.tier==='luxury' && b.balcony==='glass' ? 'balcony_per_upper_storey' : (b.tier==='basic'?'stoop':'none') };
    });
  }

  function anchors(dir, opts){
    opts=opts||{}; const b=resolve(opts), B=camBasis({dir,elev:opts.elev});
    const pj=(x,y,z)=>{ const v=projVert(x,y,z,B); return {x:Math.round(v.sx),y:Math.round(v.sy)}; };
    const lux=b.tier==='luxury';
    const unitsA=b.bays.map(v=>{
      const wins=[];
      for(let s=0;s<v.storeys;s++){
        const base=b.fH+s*b.storeyH;
        const band=Math.min(1.9, v.w*0.30), lw=v.w-band;
        wins.push(Object.assign({storey:s, side:'front'}, pj(lux? v.x0+band+lw*0.82 : (s===0? v.xc+v.w*0.10 : v.x0+0.75+ (v.w-1.5)*0.5), v.yf, base+(lux?1.6:1.85))));
      }
      return {
        unit:'unit_'+(v.i+1), beds:v.beds, storeys:v.storeys,
        door: pj(v.doorX, v.yf, lux?1.2:b.fH+1.05),
        threshold: pj(v.doorX, v.yf, lux?0.04:b.fH),
        stoop: lux ? pj(v.doorX, v.yf+1.15, 0.12) : pj(v.doorX, v.yf+3*0.32, 0.10),
        mail: lux ? null : pj(v.doorX+0.68, v.yf, b.fH+1.18),
        balcony: (lux && b.balcony!=='none' && v.storeys>1)
          ? pj(v.x0+Math.min(1.9,v.w*0.30)+(v.w-Math.min(1.9,v.w*0.30))*0.40, v.yf+1.15, b.fH+b.storeyH) : null,
        windows: wins,
      };
    });
    const stacks=[];
    if(b.tier==='basic' && b.chimneys){
      const joints=[]; for(let i=0;i<b.n-1;i++) joints.push(b.bays[i].x1);
      if(!joints.length) joints.push(0);
      const wanted=Math.min(b.chimneys, joints.length), step=joints.length/wanted;
      for(let q=0;q<wanted;q++){ const jx=joints[Math.min(joints.length-1,Math.round(q*step))];
        stacks.push(pj(jx, -b.Ln*0.10, b.parapetZ+1.35+0.46)); }
    }
    const vents = b.tier==='basic' ? b.bays.map(v=>pj(v.xc, -b.Ln*0.16+0.16, v.topZ+0.55)) : [];
    return { units:unitsA, chimneys:stacks, vents,
      roof: pj(0, 0, b.topZ), roofDeck: (b.tier==='luxury'&&b.roofDeck)? pj(b.bays[0].x0+1.4, -b.Ln/2+1.4, b.topZ+2.38) : null,
      Wd:b.Wd, Ln:b.Ln, wallH:b.wallH, topZ:b.topZ };
  }
  // ---- THE ONE GAMEPLAY ENTRY POINT ----------------------------------------
  // Station precedent, extended to a dwelling: this rig owns the building block, the unit table, the
  // street entries, the mail drops and the exterior blockers; Art/rowhouseUnitIsoRig.js owns SOLE,
  // the interior THRESHOLDs, STAIRS, INTERACT and the interior BLOCKERS; PropIso owns every piece of
  // furniture those INTERACT points act on. gameplayAll() composes all three into ONE sidecar, so the
  // integration is a single call and a single file per building.
  //
  //   RowhouseIso.gameplayAll(opts)   -> the whole sidecar, interiors merged when the room rig is loaded
  //   RowhouseIso.gameplay(opts)      -> the exterior sections alone
  //
  // Generated, never edited. Three hashes are stamped by Art/_sidecarExport.js, one per renderer that
  // can drift (shell, room, props). If any moves, re-run the generator rather than patching a number.
  function gameplay(opts){
    const o=opts||{}, b=resolve(o), sh=shell(o), us=units(o), lux=b.tier==='luxury';
    const UNITS = us.map((u,i)=>{
      const si=sh.units[i];
      return { id:u.id, bay:u.bay, tier:u.tier,
        beds:u.beds, baths:u.baths, ensuite:u.ensuite, storeys:u.storeys,
        floor_area_m2:u.floorArea_m2, resident_slots:u.sleeps,
        interior_box:si.interior,
        entry:{ x:si.openings.entry.x, y:si.openings.entry.y, z:si.openings.entry.z, kind:si.openings.entry.kind },
        party_walls:u.partyWalls, private_outdoor:u.privateOutdoor,
        levels: Array.from({length:u.storeys}, (_,s)=>({ level:'storey_'+s, z:sh.storeyZ[s], ceil_height_m:sh.ceilH })) };
    });
    const THRESHOLD = us.map((u,i)=>{
      const op=sh.units[i].openings.entry;
      return { id:u.id+'.entry', unit:u.id, kind:'entry', storey:0,
        from:'outside', to:'g_entry', axis:'x', plane:op.y, centre:op.x,
        clear_width_m:op.clearW, clear_height_m:op.clearH, sill_z:op.z,
        mechanism:'hinged_single', hinge_axis:{ x:r3(op.x-op.clearW/2), y:op.y, vertical:true },
        swing:{ outward:true, degrees:95,
          keep_clear:[[r3(op.x-op.clearW/2),r3(op.y)],[r3(op.x+op.clearW/2),r3(op.y+op.clearW)]] },
        default_state:'shut', leaf:op.kind };
    });
    const MAIL = lux ? [] : us.map((u,i)=>{
      const bay=b.bays[i], op=sh.units[i].openings.entry;
      return { id:u.id+'.mail', unit:u.id, type:'wall_box', storey:0,
        pos:[r3(op.x+0.68), r3(bay.yf), r3(b.fH+1.18)],
        stand:[r3(op.x+0.68), r3(bay.yf+0.55), 0], accepts:['letter','small_parcel'] };
    });
    const WALK = [];
    for(let i=0;i<b.n;i++){
      const bay=b.bays[i], id='unit_'+(i+1);
      if(lux){
        WALK.push({ id:id+'.landing', unit:id, kind:'entry_landing', z:r3(Math.max(0.12,b.fH)),
          polygon:[[r3(bay.doorX-1.05),r3(bay.yf)],[r3(bay.doorX+1.05),r3(bay.yf)],
                   [r3(bay.doorX+1.05),r3(bay.yf+1.00)],[r3(bay.doorX-1.05),r3(bay.yf+1.00)]],
          treatment:'flush', note:'concrete landing under the entry canopy; one 0.16 m step down to grade' });
        if(b.balcony!=='none') for(let s=1;s<bay.storeys;s++){
          const band=Math.min(1.9,bay.w*0.30), lw=bay.w-band, bx=bay.x0+band+lw*0.40, bw=Math.min(2.7,lw*0.62);
          WALK.push({ id:id+'.balcony_s'+s, unit:id, kind:'balcony', level:'storey_'+s,
            z:r3(b.fH+s*b.storeyH),
            polygon:[[r3(bx-bw/2),r3(bay.yf)],[r3(bx+bw/2),r3(bay.yf)],
                     [r3(bx+bw/2),r3(bay.yf+1.15)],[r3(bx-bw/2),r3(bay.yf+1.15)]],
            balustrade:{ height_m:1.02, infill:'glass', cap:'metal' }, private_to:id });
        }
      } else {
        WALK.push({ id:id+'.stoop', unit:id, kind:'stoop', z:r3(b.fH),
          polygon:[[r3(bay.doorX-0.85),r3(bay.yf)],[r3(bay.doorX+0.85),r3(bay.yf)],
                   [r3(bay.doorX+0.85),r3(bay.yf+0.96)],[r3(bay.doorX-0.85),r3(bay.yf+0.96)]],
          steps:{ count:3, rise_m:r3(b.fH/3), run_m:0.32,
                  handrail:{ sides:'both', height_m:0.86, material:'iron' } },
          treatment:'step_up', note:'stone stoop; three treads from grade to the threshold' });
      }
    }
    const BLOCKERS = [{ what:'building', level:'grade', treatment:'wall',
      footprint:[[r3(-b.Wd/2),r3(-b.Ln/2)],[r3(b.Wd/2),r3(b.Ln/2)]],
      height_above_grade_m:r3(b.parapetZ),
      note:'the terrace shell; interior floors are walkable via SOLE' }];
    for(let i=0;i<b.n;i++){
      const bay=b.bays[i], id='unit_'+(i+1);
      if(!lux){
        BLOCKERS.push({ what:'stoop', unit:id, level:'grade', treatment:'step_up',
          footprint:[[r3(bay.doorX-0.85),r3(bay.yf)],[r3(bay.doorX+0.85),r3(bay.yf+0.96)]],
          height_above_grade_m:r3(b.fH) });
        if(b.downpipes && i<b.n-1) BLOCKERS.push({ what:'downpipe', level:'grade', treatment:'wall',
          footprint:[[r3(bay.x1-0.07),r3(bay.yf+0.02)],[r3(bay.x1+0.07),r3(bay.yf+0.16)]],
          height_above_grade_m:r3(bay.topZ-0.22) });
      } else {
        BLOCKERS.push({ what:'entry_landing', unit:id, level:'grade', treatment:'step_up',
          footprint:[[r3(bay.doorX-1.05),r3(bay.yf)],[r3(bay.doorX+1.05),r3(bay.yf+1.42)]],
          height_above_grade_m:r3(Math.max(0.12,b.fH)) });
      }
    }
    const ROOF = { deck_z:r3(b.topZ), parapet_z:r3(b.parapetZ), walkable:!!(lux&&b.roofDeck),
      access:(lux&&b.roofDeck) ? { kind:'stair_bulkhead', unit:'unit_1',
        footprint:[[r3(b.bays[0].x0+0.5),r3(-b.Ln/2+0.6)],[r3(b.bays[0].x0+2.3),r3(-b.Ln/2+2.2)]],
        height_m:2.38 } : null,
      note:(lux&&b.roofDeck) ? 'mono-pitch membrane behind a shallow parapet; the bulkhead is the way up'
                             : 'flat membrane behind the parapet; no ladder, no hatch — not walkable' };
    return { UNITS, THRESHOLD, MAIL:MAIL.length?MAIL:undefined, WALK, BLOCKERS, ROOF };
  }

  function gameplayAll(opts){
    const o=opts||{}, b=resolve(o), dm=dims(o), sh=shell(o);
    const ext=gameplay(o);
    const IN=root.RowhouseUnitIso;
    const inner=(IN&&IN.gameplaySections) ? IN.gameplaySections(Object.assign({},o,{focus:'all',explode:0})) : null;
    const out={
      schema:'hidden-harbours/building-gameplay@1',
      rig:'Art/rowhouseIsoRig.js',
      exportSymbol:'globalThis.RowhouseIso',
      interiorRig:'Art/rowhouseUnitIsoRig.js',
      propRig:'Art/interiorPropRig.js',
      variant:b.tier+'_'+b.n+'u_'+b.beds+'bed_'+b.storeys+'st',
      generator:'RowhouseIso.gameplayAll(opts)',
      phase:'multi-unit phase 1 — the terrace',
      build:{ tier:b.tier, units:b.n, beds:b.beds, mix:b.mix, storeys:b.storeys, clad:b.clad,
        body:b.body, accent:b.accent, roofForm:b.roofForm, entry:b.entry, balcony:b.balcony,
        stagger:b.stagger, chimneys:b.chimneys, roofDeck:b.roofDeck },
      frame:{ units:'metres', scale_px_per_m:PX, origin:'ground centre of the row footprint',
        axes:'+x along the row, +y street/front, +z up', heading_independent:true,
        cell:{ w:W, h:H, pivot:{ x:cx, y:groundY } },
        note:'the interior rig paints into this same cell and pivot, so a unit interior registers under its bay to the pixel' },
      building:{ width_m:dm.Wd, depth_m:dm.Ln, storeys:b.storeys, unit_count:b.n,
        wall_thickness_m:sh.wallT, party_wall_thickness_m:sh.partyWallT, floor_thickness_m:sh.floorT,
        storey_height_m:b.storeyH, ceiling_height_m:sh.ceilH, ground_floor_z:sh.fH,
        storey_z:sh.storeyZ, roof_z:sh.roofZ, parapet_z:sh.parapetZ },
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
        u.nominal_occupancy=u.resident_slots;
        u.resident_slots=sleeps.length;
        u.bedrooms_built=new Set(sleeps.map(x=>x.room)).size;
        u.sleep_anchors=sleeps.map(x=>x.id);
      }
      if(inner._unplaced&&inner._unplaced.length) out._unplaced=inner._unplaced;
      out._interiorNote=inner._interiorNote;
    }
    out._excluded={
      ELEVATOR:'A terrace is walk-up by definition: every unit has its own street door and its own private stair. Elevator, lobby, corridor, shared laundry and basement lockers belong to the walk-up block rig, not here.',
      CORRIDOR:'No shared circulation exists — unit doors open straight onto the street.',
      LOBBY:'Same: there is no shared entrance to model.',
      WASHBOARD:'Not a hull.', CLEATS:'Not a hull.',
      walkable_roof:(b.tier==='luxury'&&b.roofDeck) ? undefined
        : 'Flat membrane behind a parapet, no ladder and no hatch — basic terraces are not walkable up top.',
      rear_garden:'The rig bakes the building, not its plot. Yards, bins and bike racks are site dressing placed by the scene.',
    };
    out._confirm={
      interiors: inner ? undefined : 'Art/rowhouseUnitIsoRig.js was not loaded, so SOLE, STAIRS and INTERACT are ABSENT. Load the room rig and regenerate.',
      resident_slots:'UNITS[].resident_slots is the count of SLEEP anchors the interior actually built (one per approachable bed side), so a double seats two. nominal_occupancy is the figure units() derives from the bed request; the two differ when a plate cap trims a bedroom. Trust resident_slots — it is the one backed by a bed in a room.',
      bedsBuilt:'A plate holds two bedrooms, so a 3-bed unit needs three storeys; a 2-storey 3-bed request builds two and reports it (interior rig bedsBuilt / bedsRequested).',
      mail:'Luxury units carry no wall box — a recessed entry takes a slot in the door, which is not modelled. MAIL is omitted rather than invented.',
      entry_swing:'Street doors are given a 95 degree outward swing and its swept rect. The bake draws the leaf shut, so the arc is declared, not measured off pixels.',
    };
    return out;
  }

  function project(dir, p, elev){ const v=projVert(p[0],p[1],p[2],camBasis({dir,elev})); return {x:v.sx,y:v.sy}; }

  root.RowhouseIso = { W, H, PX, DIRS:8, pivot:{x:cx,y:groundY}, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    TIERS, CLADS, BODY, STONE, TRIM, IRON, DOORPAINTS, WINDOWS, ROOFFORMS, ENTRIES, BALCONIES, PRESETS, KEY,
    KEYLINE_DEFAULT, dims, shell, units, gameplay, gameplayAll, render, anchors, project };
})(typeof globalThis!=='undefined'?globalThis:window);
