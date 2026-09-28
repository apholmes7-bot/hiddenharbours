/* Hidden Harbours — parametric ISO SHOP BUILDING rig: MULTI-ROOM PLANS on top of shopInteriorRig.js.
   Same turntable, camera, palette, texture, dither, z-buffer, depth-edge and keyline as every other
   rig — this file adds no rendering of its own. It composes shopInteriorRig's exact builders into a
   WHOLE BUILDING and bakes ONE MERGED SHEET PER LEVEL, so occlusion, shading and keyline are exact
   across rooms instead of approximated by stitching per-room sheets.
   REQUIRES shopInteriorRig.js to be loaded first (it reads ShopInterior._i).

   ---------------------------------------------------------------------------------------------
   THE PLAN MODEL
     A plan is a list of ROOM RECTS on a shared UNIT grid, per level. Units are resolved to 0.5 m
     CELLS at bake time from the trade's own footprint, so a plan scales with `size` and can never
     develop a gap: both rooms either side of a boundary map that boundary to the SAME cell line.
       main:   the rect that IS the shopfront mass — Wd x Ln from ShopInterior.TYPES, centred on the
               pivot exactly as the single-room rig centres its box. THIS IS THE REGISTRATION CONTRACT.
       wing:   any rect outside `main` — a rear ell (kitchen, ice house) under its own lower roof.
               wingOf(type,size) hands shopfrontRig the same numbers so the shell grows to match.
     WALLS ARE DERIVED, NEVER AUTHORED. The rooms are rasterised to a cell-ownership map and every
     cell edge whose two sides have different owners becomes wall. Contiguous edges with the same
     pair merge into one segment, which is then classified:
       room|outside -> EXTERIOR   (outer face on the envelope line, 0.16 m thick, inward finish)
       roomA|roomB  -> PARTY      (centred on the line, 0.14 m, one wall, built once, opening punched)
     This handles L-plans, wings, courtyards and unequal room sizes with no special cases.

   OPENINGS
     Every party segment gets one opening, its kind chosen from the two room kinds (dining|kitchen ->
     service double, front|bakehouse -> pass hatch, floor|stock -> slab door...) and overridable per
     segment. Segments too short for their door fall back to a full-width cased opening.

   THE CUTAWAY — how party walls read at every facing
     Exterior walls use the shared keepWall test: the two whose outward face points at the camera are
     dropped (orthogonal facings keep three, diagonals keep two). A party wall has no outward side, so
     a plain keepWall on it either hides the far room (kept) or erases the plan (dropped). Instead:
       edge-on to camera  (|t| <= 0.35) -> built solid, full height, opening punched.
       facing the camera  (|t| >  0.35) -> CUT BACK to an architectural-cutaway read: the wall's
            footprint trace on the floor for its full length, a full-height RETURN NIB at each end,
            and the opening's casing, lintel and leaf left standing free. Nothing occludes the room
            behind it, and the plan is still legible from every one of the eight facings.
     Ceilings are never slabbed (a horizontal plane at ceiling height would z-sort in front of the
     whole room) — the deck above reads as joists plus the wall-top cap, and rooms with roof over
     them get the same trimmed roof-underside the single-room rig uses.

   LEVELS
     levels.ground / levels.upper, each its own rect list, each baked with its floor plane at z 0 —
     the same convention the single-room rig uses for storey:'upper'. planDims().levelZ gives the
     world height of that floor so the engine stacks the two sheets on one pivot. The upper level is
     constrained to the main block, and the STAIR REGISTERS IN BOTH: one authored {room,side,along}
     puts the flight in the lower level and the matching well, trimmer and rail in the upper one, at
     the same cells. If the authored room has no upper room over it the stair migrates to one that has.

   THE BUILDER SURFACE
     type:  any ShopInterior.TYPES key (nine trades, one plan each)
     level: 'ground'|'upper'      size:0..1      elev:30..50
     floor/wall/paper/... : per-ROOM finishes come from the room KIND; building-level overrides here
     stock:0..1  weather:0..1  night:bool  seed:int  beams:bool
     items: [{room,k,gx,gy,rot}] | null   — manual slot layout, per room, in that room's own cell grid
     rooms: {roomId:false} to omit a room      doors: {segKey:'kind'} to override an opening
   Exposes globalThis.ShopBuilding = { PX, CELL, DIRS, defaultElev, PLANS, KINDS, DOORS, TYPES,
     plans, rooms, dims, sheet, grid, items, wingOf, render, renderLayers, ghost, anchors, project }. */
(function (root) {
  let SI=null, I=null;
  function need(){
    if(!I){ SI=root.ShopInterior; I=SI&&SI._i; }
    if(!I) throw new Error('shopBuildingRig.js requires shopInteriorRig.js to load first');
    return I;
  }
  const PX=32, CELL=0.5, WT=0.16, PT=0.14, DECK=0.34;
  const DEFAULT_ELEV=40;

  // ---- ROOM KINDS: finish + ceiling share + who lives in them ---------------------------------
  const KINDS = {
    salesFloor: { label:'sales floor', floor:'plank',     wall:'wainscot', paper:'cream',       wainscot:true,  ceil:1.00, winD:0.55 },
    stockroom:  { label:'stock room',  floor:'plank',     wall:'board',    paper:'greyShingle', wainscot:false, ceil:0.94, winD:0.28 },
    parlour:    { label:'parlour',     floor:'plank',     wall:'plaster',  paper:'sage',        wainscot:true,  ceil:1.00, winD:0.50 },
    bedroom:    { label:'bedroom',     floor:'plank',     wall:'plaster',  paper:'blue',        wainscot:false, ceil:1.00, winD:0.40 },
    flat:       { label:'flat',        floor:'plank',     wall:'plaster',  paper:'cream',       wainscot:true,  ceil:1.00, winD:0.48 },
    fishCounter:{ label:'fish counter',floor:'painted',   wall:'tile',     paper:'white',       wainscot:false, ceil:1.00, winD:0.50 },
    cutting:    { label:'cutting room',floor:'painted',   wall:'tile',     paper:'white',       wainscot:false, ceil:0.94, winD:0.45 },
    iceHouse:   { label:'ice house',   floor:'stoneFlag', wall:'board',    paper:'galv',        wainscot:false, ceil:0.82, winD:0.16 },
    chandFloor: { label:'chandlery floor',floor:'wideBoard',wall:'board',  paper:'greyShingle', wainscot:false, ceil:1.00, winD:0.50 },
    chandLoft:  { label:'rigging loft',floor:'wideBoard', wall:'stud',     paper:'greyShingle', wainscot:false, ceil:1.00, winD:0.35 },
    bakeFront:  { label:'café front',  floor:'checker',   wall:'tile',     paper:'cream',       wainscot:true,  ceil:1.00, winD:0.55 },
    bakehouse:  { label:'bakehouse',   floor:'stoneFlag', wall:'brick',    paper:'cream',       wainscot:false, ceil:0.92, winD:0.35 },
    dining:     { label:'dining room', floor:'wideBoard', wall:'wainscot', paper:'sage',        wainscot:true,  ceil:1.00, winD:0.60 },
    bar:        { label:'bar',         floor:'wideBoard', wall:'wainscot', paper:'sage',        wainscot:true,  ceil:1.00, winD:0.40 },
    kitchen:    { label:'kitchen',     floor:'checker',   wall:'tile',     paper:'white',       wainscot:false, ceil:0.88, winD:0.40 },
    stock:      { label:'stock room',  floor:'plank',     wall:'board',    paper:'greyShingle', wainscot:false, ceil:0.88, winD:0.24 },
    barroom:    { label:'bar room',    floor:'plank',     wall:'panel',    paper:'red',         wainscot:true,  ceil:1.00, winD:0.45 },
    snug:       { label:'snug',        floor:'plank',     wall:'panel',    paper:'plum',        wainscot:true,  ceil:1.00, winD:0.30 },
    lobby:      { label:'lobby',       floor:'stoneFlag', wall:'wainscot', paper:'blue',        wainscot:true,  ceil:1.00, winD:0.50 },
    sorting:    { label:'sorting room',floor:'plank',     wall:'board',    paper:'cream',       wainscot:false, ceil:0.94, winD:0.40 },
    servery:    { label:'servery',     floor:'painted',   wall:'board',    paper:'mustard',     wainscot:false, ceil:1.00, winD:0.30 },
    giftFloor:  { label:'gift floor',  floor:'wideBoard', wall:'plaster',  paper:'teal',        wainscot:true,  ceil:1.00, winD:0.55 },
  };

  // ---- FIXTURE LAYOUT PER KIND ---------------------------------------------------------------
  // {k, side, along} hugs a wall (rot derived so the front faces into the room); {k, fx, fy, rot}
  // is free, in -1..1 of the room's clear rect. Overflow is clamped, so short rooms still resolve.
  const LAY = {
    salesFloor: [ {k:'counter',side:'W',along:0.62}, {k:'wallShelf',side:'S',along:0.30},
                  {k:'wallShelf',side:'S',along:0.72}, {k:'gondola',fx:0.25,fy:0.10,rot:0},
                  {k:'binRack',side:'E',along:0.34}, {k:'potbelly',fx:0.30,fy:-0.55,rot:0},
                  {k:'crateStack',fx:-0.62,fy:-0.30,rot:0} ],
    stockroom:  [ {k:'stockShelf',side:'W',along:0.30}, {k:'stockShelf',side:'W',along:0.72},
                  {k:'stockShelf',side:'E',along:0.50}, {k:'barrelStack',fx:0.10,fy:-0.45,rot:0},
                  {k:'sackPile',fx:-0.20,fy:0.45,rot:0}, {k:'crateStack',fx:0.45,fy:0.35,rot:0} ],
    parlour:    [ {k:'table',fx:-0.10,fy:0.15,rot:0}, {k:'chair',fx:-0.10,fy:-0.30,rot:0},
                  {k:'chair',fx:-0.10,fy:0.58,rot:2}, {k:'larder',side:'E',along:0.35},
                  {k:'rug',fx:-0.05,fy:0.10,rot:0}, {k:'potbelly',fx:0.62,fy:-0.55,rot:0} ],
    bedroom:    [ {k:'larder',side:'W',along:0.30}, {k:'bench',side:'S',along:0.50},
                  {k:'crateStack',fx:0.55,fy:0.45,rot:0}, {k:'rug',fx:0,fy:0,rot:0} ],
    flat:       [ {k:'table',fx:-0.15,fy:0.20,rot:0}, {k:'chair',fx:-0.15,fy:-0.28,rot:0},
                  {k:'chair',fx:0.30,fy:0.20,rot:1}, {k:'larder',side:'E',along:0.30},
                  {k:'potbelly',fx:0.55,fy:-0.55,rot:0}, {k:'rug',fx:-0.10,fy:0.15,rot:0} ],
    fishCounter:[ {k:'coldTable',side:'N',along:0.42}, {k:'coldTable',side:'N',along:0.80},
                  {k:'counterShort',side:'W',along:0.55}, {k:'iceChest',side:'E',along:0.40},
                  {k:'crateStack',fx:-0.55,fy:-0.55,rot:0} ],
    cutting:    [ {k:'prepTable',fx:0,fy:0.20,rot:0}, {k:'sinkRun',side:'W',along:0.40},
                  {k:'wallShelf',side:'S',along:0.55}, {k:'crateStack',fx:0.55,fy:-0.45,rot:0} ],
    iceHouse:   [ {k:'iceChest',side:'W',along:0.40}, {k:'iceChest',side:'E',along:0.40},
                  {k:'crateStack',fx:0,fy:-0.35,rot:0} ],
    chandFloor: [ {k:'counter',side:'E',along:0.60}, {k:'ropeRack',side:'S',along:0.30},
                  {k:'wallShelf',side:'S',along:0.74}, {k:'binRack',side:'W',along:0.34},
                  {k:'gondola',fx:-0.15,fy:0.05,rot:0}, {k:'barrelStack',fx:0.45,fy:-0.50,rot:0} ],
    chandLoft:  [ {k:'ropeRack',side:'W',along:0.35}, {k:'stockShelf',side:'E',along:0.45},
                  {k:'crateStack',fx:0,fy:-0.30,rot:0}, {k:'sackPile',fx:0.30,fy:0.30,rot:0} ],
    bakeFront:  [ {k:'displayCase',side:'W',along:0.58}, {k:'roundTable',fx:0.35,fy:0.45,rot:0},
                  {k:'chair',fx:0.35,fy:0.08,rot:0}, {k:'roundTable',fx:0.35,fy:-0.30,rot:0},
                  {k:'chair',fx:0.35,fy:-0.66,rot:0}, {k:'chalkMenu',side:'S',along:0.30},
                  {k:'plantTub',fx:-0.70,fy:0.60,rot:0} ],
    bakehouse:  [ {k:'ovenBank',side:'S',along:0.30}, {k:'prepTable',fx:0.05,fy:0.10,rot:0},
                  {k:'wallShelf',side:'E',along:0.55}, {k:'sinkRun',side:'W',along:0.55},
                  {k:'sackPile',fx:-0.35,fy:-0.55,rot:0} ],
    dining:     [ {k:'table',fx:-0.45,fy:0.48,rot:0}, {k:'chair',fx:-0.45,fy:0.16,rot:0},
                  {k:'table',fx:0.45,fy:0.48,rot:0},  {k:'chair',fx:0.45,fy:0.16,rot:0},
                  {k:'booth',side:'W',along:0.30}, {k:'booth',side:'E',along:0.30},
                  {k:'counterShort',side:'S',along:0.72}, {k:'plantTub',fx:-0.72,fy:0.72,rot:0},
                  {k:'chalkMenu',side:'S',along:0.24} ],
    kitchen:    [ {k:'pass',side:'N',along:0.60}, {k:'ovenBank',side:'S',along:0.28},
                  {k:'prepTable',fx:0,fy:0.05,rot:0}, {k:'sinkRun',side:'W',along:0.55},
                  {k:'wallShelf',side:'E',along:0.40}, {k:'crateStack',fx:0.60,fy:-0.55,rot:0} ],
    stock:      [ {k:'stockShelf',side:'W',along:0.35}, {k:'stockShelf',side:'E',along:0.35},
                  {k:'barrelStack',fx:0,fy:-0.40,rot:0}, {k:'sackPile',fx:0.15,fy:0.40,rot:0} ],
    bar:        [ {k:'backBar',side:'S',along:0.50}, {k:'bar',fx:0,fy:-0.28,rot:0},
                  {k:'stool',fx:-0.34,fy:0.12,rot:0}, {k:'stool',fx:0,fy:0.12,rot:0},
                  {k:'stool',fx:0.34,fy:0.12,rot:0}, {k:'booth',side:'W',along:0.46},
                  {k:'booth',side:'E',along:0.46} ],
    barroom:    [ {k:'bar',side:'W',along:0.52}, {k:'backBar',side:'S',along:0.30},
                  {k:'stool',fx:-0.05,fy:0.30,rot:1}, {k:'stool',fx:-0.05,fy:0.02,rot:1},
                  {k:'stool',fx:-0.05,fy:-0.26,rot:1}, {k:'table',fx:0.50,fy:0.40,rot:0},
                  {k:'chair',fx:0.50,fy:0.05,rot:0}, {k:'potbelly',fx:0.62,fy:-0.55,rot:0} ],
    snug:       [ {k:'booth',side:'W',along:0.40}, {k:'table',fx:0.30,fy:0,rot:0},
                  {k:'chair',fx:0.30,fy:-0.50,rot:0}, {k:'rug',fx:0,fy:0,rot:0} ],
    lobby:      [ {k:'wicket',side:'S',along:0.50}, {k:'bench',side:'W',along:0.45},
                  {k:'chalkMenu',side:'E',along:0.35}, {k:'plantTub',fx:0.70,fy:0.62,rot:0} ],
    sorting:    [ {k:'pigeonholes',side:'W',along:0.32}, {k:'pigeonholes',side:'W',along:0.74},
                  {k:'prepTable',fx:0.15,fy:0.10,rot:0}, {k:'stockShelf',side:'E',along:0.45},
                  {k:'crateStack',fx:0.50,fy:-0.45,rot:0} ],
    servery:    [ {k:'counterShort',side:'N',along:0.50}, {k:'prepTable',fx:0,fy:-0.25,rot:0},
                  {k:'wallShelf',side:'S',along:0.50}, {k:'crateStack',fx:-0.55,fy:-0.55,rot:0} ],
    giftFloor:  [ {k:'counter',side:'E',along:0.60}, {k:'wallShelf',side:'S',along:0.32},
                  {k:'wallShelf',side:'S',along:0.74}, {k:'gondola',fx:-0.20,fy:0.10,rot:0},
                  {k:'displayCase',side:'W',along:0.40}, {k:'plantTub',fx:0.70,fy:0.66,rot:0} ],
  };

  // ---- PLANS: one per trade. Rects in UNITS; `main` is the shopfront mass (Wd x Ln). ----------
  // +Y is the street. Anything below main's y is a rear WING behind the shell's back wall.
  const PLANS = {
    generalStore:{ main:{x:0,y:0,w:6,h:10},
      ground:[ {id:'salesFloor',kind:'salesFloor',x:0,y:3,w:6,h:7}, {id:'stockroom',kind:'stockroom',x:0,y:0,w:6,h:3} ],
      upper:[ {id:'flat',kind:'flat',x:0,y:0,w:6,h:10} ],
      stair:{ room:'stockroom', side:'E', along:0.50 } },
    fishMarket:{ main:{x:0,y:0,w:6,h:8},
      ground:[ {id:'counter',kind:'fishCounter',x:0,y:3,w:6,h:5}, {id:'cutting',kind:'cutting',x:0,y:0,w:6,h:3},
               {id:'ice',kind:'iceHouse',x:1,y:-2,w:4,h:2} ],
      upper:[], stair:null },
    chandlery:{ main:{x:0,y:0,w:6,h:9},
      ground:[ {id:'floor',kind:'chandFloor',x:0,y:3,w:6,h:6}, {id:'stockroom',kind:'stockroom',x:0,y:0,w:6,h:3} ],
      upper:[ {id:'flat',kind:'flat',x:0,y:5,w:6,h:4}, {id:'loft',kind:'chandLoft',x:0,y:0,w:6,h:5} ],
      stair:{ room:'stockroom', side:'W', along:0.50 } },
    bakery:{ main:{x:0,y:0,w:6,h:9},
      ground:[ {id:'front',kind:'bakeFront',x:0,y:4,w:6,h:5}, {id:'bakehouse',kind:'bakehouse',x:0,y:0,w:6,h:4} ],
      upper:[ {id:'flat',kind:'flat',x:0,y:0,w:6,h:9} ],
      stair:{ room:'bakehouse', side:'E', along:0.52 } },
    restaurant:{ main:{x:0,y:3,w:6,h:7},
      ground:[ {id:'dining',kind:'dining',x:0,y:5,w:6,h:5}, {id:'bar',kind:'bar',x:0,y:3,w:6,h:2},
               {id:'kitchen',kind:'kitchen',x:0,y:0,w:4,h:3}, {id:'stock',kind:'stock',x:4,y:0,w:2,h:3} ],
      upper:[ {id:'flat',kind:'flat',x:0,y:3,w:6,h:7} ],
      stair:{ room:'dining', side:'W', along:0.20 } },
    tavern:{ main:{x:0,y:4,w:6,h:6},
      ground:[ {id:'barroom',kind:'barroom',x:0,y:6,w:6,h:4}, {id:'snug',kind:'snug',x:0,y:4,w:6,h:2},
               {id:'kitchen',kind:'kitchen',x:0,y:1,w:6,h:3} ],
      upper:[ {id:'flat',kind:'flat',x:0,y:4,w:6,h:6} ],
      stair:{ room:'barroom', side:'E', along:0.22 } },
    postOffice:{ main:{x:0,y:0,w:6,h:9},
      ground:[ {id:'lobby',kind:'lobby',x:0,y:4,w:6,h:5}, {id:'sorting',kind:'sorting',x:0,y:0,w:6,h:4} ],
      upper:[ {id:'flat',kind:'flat',x:0,y:0,w:6,h:9} ],
      stair:{ room:'sorting', side:'W', along:0.50 } },
    takeoutStand:{ main:{x:0,y:0,w:6,h:6},
      ground:[ {id:'servery',kind:'servery',x:0,y:0,w:6,h:6} ],
      upper:[], stair:null },
    giftShop:{ main:{x:0,y:0,w:6,h:9},
      ground:[ {id:'floor',kind:'giftFloor',x:0,y:3,w:6,h:6}, {id:'stockroom',kind:'stockroom',x:0,y:0,w:6,h:3} ],
      upper:[ {id:'flat',kind:'flat',x:0,y:0,w:6,h:9} ],
      stair:{ room:'stockroom', side:'E', along:0.50 } },
  };

  // ---- OPENING CATALOG -----------------------------------------------------------------------
  const DOORS = ['doorway','slab','swingPair','hatch','arch','curtain','dutch'];
  const DOOR_SPEC = {
    doorway:  { w:1.06, h:2.08, sill:0,    label:'cased doorway' },
    slab:     { w:0.96, h:2.05, sill:0,    label:'slab door' },
    swingPair:{ w:1.56, h:2.05, sill:0,    label:'service double' },
    hatch:    { w:1.34, h:0.98, sill:1.06, label:'pass hatch' },
    arch:     { w:2.40, h:2.32, sill:0,    label:'wide arch' },
    curtain:  { w:1.06, h:2.10, sill:0,    label:'curtained doorway' },
    dutch:    { w:1.00, h:2.05, sill:0,    label:'dutch door' },
  };
  const PAIR = {
    'dining|kitchen':'swingPair', 'dining|stock':'slab', 'kitchen|stock':'doorway',
    'bar|dining':'arch', 'bar|kitchen':'swingPair', 'bar|stock':'slab',
    'chandLoft|flat':'doorway', 'chandLoft|stockroom':'slab',
    'barroom|snug':'arch', 'barroom|kitchen':'swingPair', 'kitchen|snug':'slab',
    'salesFloor|stockroom':'slab', 'chandFloor|stockroom':'slab', 'giftFloor|stockroom':'curtain',
    'cutting|fishCounter':'hatch', 'cutting|iceHouse':'dutch',
    'bakeFront|bakehouse':'hatch', 'bakehouse|bedroom':'slab',
    'lobby|sorting':'slab', 'bedroom|parlour':'slab', 'bed|flat':'slab', 'bedroom|flat':'slab',
  };
  function doorKind(kA,kB){ const k=[kA,kB].sort().join('|'); return PAIR[k]||'doorway'; }

  // ============================================================================================
  // RESOLVE
  // ============================================================================================
  const ROOM_MATS = ['floor','paper','plaster','trim','paint','worktop'];

  function resolve(opts){
    need(); opts=opts||{};
    const type = PLANS[opts.type] ? opts.type : 'restaurant';
    const P = PLANS[type], T = SI.TYPES[type];
    const size = opts.size!=null ? opts.size : T.size;
    const D = SI.dims({ type, size });
    const MW = Math.max(4, Math.round(D.Wd/CELL)), ML = Math.max(4, Math.round(D.Ln/CELL));
    const kx = MW/P.main.w, ky = ML/P.main.h;
    const ci = (u)=> Math.round((u - P.main.x)*kx);
    const cj = (u)=> Math.round((u - P.main.y)*ky);
    const wx = (i)=> (i - MW/2)*CELL, wy = (j)=> (j - ML/2)*CELL;

    const hasUpper = !!(P.upper && P.upper.length);
    const level = (opts.level==='upper'||opts.level===1) && hasUpper ? 'upper' : 'ground';
    const shopH = D.shopH;
    const plate = Math.max(0.95, Math.min(1.62, D.wallH - (shopH + DECK)));
    const pitch = opts.pitch!=null ? opts.pitch : T.pitch;
    const rise  = (D.Wd/2) * pitch * 0.84;

    const cut=(list)=>list.filter(r=>!(opts.rooms && opts.rooms[r.id]===false));
    const rect=(r)=>({ ci0:ci(r.x), ci1:ci(r.x+r.w), cj0:cj(r.y), cj1:cj(r.y+r.h) });
    const upRects = (P.upper||[]).map(rect);
    const covered=(q)=> upRects.some(u=> q.ci0<u.ci1 && u.ci0<q.ci1 && q.cj0<u.cj1 && u.cj0<q.cj1);
    const mainC = { ci0:ci(P.main.x), ci1:ci(P.main.x+P.main.w), cj0:cj(P.main.y), cj1:cj(P.main.y+P.main.h) };

    const src = cut(level==='upper' ? P.upper : P.ground);
    const rooms = src.map((r,idx)=>{
      const K = KINDS[r.kind]||KINDS.salesFloor, R = rect(r);
      const block = (R.ci0>=mainC.ci0 && R.ci1<=mainC.ci1 && R.cj0>=mainC.cj0 && R.cj1<=mainC.cj1) ? 'main' : 'wing';
      let ceilZ, roofOver=null;
      if(level==='upper'){ ceilZ = plate; roofOver='main'; }
      else if(block==='wing'){ ceilZ = Math.max(2.35, shopH*(K.ceil||0.9)); roofOver='wing'; }
      else { ceilZ = shopH; roofOver = covered(R) ? null : 'main'; }
      return Object.assign({}, R, {
        id:r.id, idx, kind:r.kind, label:K.label, block, ceilZ, roofOver,
        x0:wx(R.ci0), x1:wx(R.ci1), y0:wy(R.cj0), y1:wy(R.cj1),
        floor: r.floor||K.floor, wall: r.wall||K.wall, paper: r.paper||K.paper,
        wainscot: r.wainscot!=null?r.wainscot:K.wainscot, winD: r.winD!=null?r.winD:K.winD,
      });
    });

    const b = {
      type, label:T.label, ext:T.ext, size, level, hasUpper, plan:P,
      Wd:D.Wd, Ln:D.Ln, wallH:D.wallH, fH:D.fH, shopH, plate, pitch, rise,
      MW, ML, mainC, rooms, storefront: opts.storefront||T.storefront,
      windows: opts.windows||T.windows, trimTone: opts.trimTone!==undefined?opts.trimTone:T.trimTone,
      counterTone: opts.counterTone||T.counterTone, goods:T.goods, wares:T.wares,
      beams: opts.beams!=null?opts.beams:true,
      stock: opts.stock!=null?opts.stock:0.95,
      weather: opts.weather!=null?opts.weather:0.3,
      night: !!opts.night, seed:(opts.seed!=null?opts.seed:7)|0,
      levelZ: level==='upper' ? D.fH + shopH + DECK : D.fH,
      wx, wy, ci, cj,
    };
    b.env = envOf(rooms, wx, wy);
    b.own = ownMap(rooms);
    b.walls = wallsOf(rooms, b);
    b.rooms.forEach(r=> r.clear = clearRect(r, b));
    b.shape = SI.TYPES[type].shape || 'gable';
    b.streetY = wy(mainC.cj1);
    const SA = stairAnchor(b, P, covered);
    b.stair = SA ? Object.assign({}, SA, { level }) : null;
    b.peakZ = peakOf(b);
    return b;
  }

  function envOf(rooms, wx, wy){
    let I0=Infinity,I1=-Infinity,J0=Infinity,J1=-Infinity;
    for(const r of rooms){ I0=Math.min(I0,r.ci0); I1=Math.max(I1,r.ci1); J0=Math.min(J0,r.cj0); J1=Math.max(J1,r.cj1); }
    if(!rooms.length){ I0=I1=J0=J1=0; }
    return { I0,I1,J0,J1, x0:wx(I0), x1:wx(I1), y0:wy(J0), y1:wy(J1) };
  }
  function ownMap(rooms){ const m=new Map();
    rooms.forEach((r,i)=>{ for(let x=r.ci0;x<r.ci1;x++) for(let y=r.cj0;y<r.cj1;y++) m.set(x+','+y, i); });
    return m; }
  function at(m,x,y){ const v=m.get(x+','+y); return v==null?-1:v; }

  // every cell edge whose two sides disagree is wall; contiguous same-pair edges merge into a run
  function wallsOf(rooms, b){
    const m=ownMap(rooms), E=envOf(rooms, b.wx, b.wy), out=[];
    const push=(c)=>{ if(c) out.push(c); };
    for(let x=E.I0;x<=E.I1;x++){ let cur=null;
      for(let y=E.J0;y<E.J1;y++){
        const A=at(m,x-1,y), B=at(m,x,y), k=(A===B)?null:A+'>'+B;
        if(cur && cur.k===k){ cur.c1=y+1; continue; }
        push(cur); cur=null;
        if(k) cur={ axis:'x', line:x, c0:y, c1:y+1, A, B, k };
      }
      push(cur);
    }
    for(let y=E.J0;y<=E.J1;y++){ let cur=null;
      for(let x=E.I0;x<E.I1;x++){
        const A=at(m,x,y-1), B=at(m,x,y), k=(A===B)?null:A+'>'+B;
        if(cur && cur.k===k){ cur.c1=x+1; continue; }
        push(cur); cur=null;
        if(k) cur={ axis:'y', line:y, c0:x, c1:x+1, A, B, k };
      }
      push(cur);
    }
    return out.map(s=>{
      const w = s.axis==='x' ? { plane:b.wx(s.line), a0:b.wy(s.c0), a1:b.wy(s.c1) }
                             : { plane:b.wy(s.line), a0:b.wx(s.c0), a1:b.wx(s.c1) };
      const party = s.A>=0 && s.B>=0;
      const nrm = party ? 0 : (s.A<0 ? -1 : 1);            // exterior: outward normal sign
      const side = s.axis==='x' ? (nrm>0?'E':'W') : (nrm>0?'N':'S');
      return Object.assign({}, s, w, { party, nrm, side, len:w.a1-w.a0,
        room: party?-1:(s.A<0?s.B:s.A), key:s.axis+s.line+':'+s.c0+'-'+s.c1 });
    });
  }
  // a room's clear rect: pull each edge in by the wall that stands on it
  function clearRect(r, b){
    const ins=(edge)=>{ let ext=false, party=false;
      if(edge==='W'){ for(let y=r.cj0;y<r.cj1;y++){ const o=at(b.own,r.ci0-1,y); if(o<0) ext=true; else party=true; } }
      if(edge==='E'){ for(let y=r.cj0;y<r.cj1;y++){ const o=at(b.own,r.ci1,y);   if(o<0) ext=true; else party=true; } }
      if(edge==='S'){ for(let x=r.ci0;x<r.ci1;x++){ const o=at(b.own,x,r.cj0-1); if(o<0) ext=true; else party=true; } }
      if(edge==='N'){ for(let x=r.ci0;x<r.ci1;x++){ const o=at(b.own,x,r.cj1);   if(o<0) ext=true; else party=true; } }
      return ext?WT:(party?PT/2:WT); };
    return { x0:r.x0+ins('W'), x1:r.x1-ins('E'), y0:r.y0+ins('S'), y1:r.y1-ins('N') };
  }
  function peakOf(b){
    let z=0;
    for(const r of b.rooms) z=Math.max(z, r.ceilZ);
    for(const r of b.rooms){ if(r.roofOver==='main') z=Math.max(z, r.ceilZ + b.rise);
      else if(r.roofOver==='wing') z=Math.max(z, r.ceilZ + (r.x1-r.x0)/2*0.62); }
    return z + 0.25;
  }
  // ONE authored {room,side,along} resolves against the GROUND plan and is reused verbatim by both
  // levels — that is what makes the flight and the well register on the same cells.
  function stairAnchor(b, P, covered){
    if(!P.stair || !b.hasUpper) return null;
    const gr=(P.ground||[]).map(r=>({ id:r.id, kind:r.kind, block:'main',
      ci0:b.ci(r.x), ci1:b.ci(r.x+r.w), cj0:b.cj(r.y), cj1:b.cj(r.y+r.h) }));
    if(!gr.length) return null;
    gr.forEach(r=>{ r.x0=b.wx(r.ci0); r.x1=b.wx(r.ci1); r.y0=b.wy(r.cj0); r.y1=b.wy(r.cj1);
      r.inMain = r.ci0>=b.mainC.ci0 && r.ci1<=b.mainC.ci1 && r.cj0>=b.mainC.cj0 && r.cj1<=b.mainC.cj1; });
    const own=ownMap(gr);
    gr.forEach(r=> r.clear = clearRect(r, { own }));
    let host = gr.find(r=>r.id===P.stair.room);
    if(!host || !host.inMain || !covered(host)) host = gr.find(r=> r.inMain && covered(r)) || host || gr[0];
    if(!host) return null;
    const K=KINDS[host.kind]||KINDS.salesFloor;
    const ceil = host.inMain ? b.shopH : Math.max(2.35, b.shopH*(K.ceil||0.9));
    const side=P.stair.side, along=P.stair.along!=null?P.stair.along:0.5, C=host.clear, w=1.02;
    const ax=(side==='W'||side==='E')?'y':'x';
    const run=Math.min(2.85, (ax==='y'?(C.y1-C.y0):(C.x1-C.x0))*0.62);
    let x,y;
    if(ax==='y'){ y=C.y0+(C.y1-C.y0)*along; x = side==='W' ? C.x0+w/2 : C.x1-w/2; }
    else        { x=C.x0+(C.x1-C.x0)*along; y = side==='S' ? C.y0+w/2 : C.y1-w/2; }
    // point the flight toward the middle of its host room
    const up = ax==='y' ? (y < (C.y0+C.y1)/2 ? 1 : -1) : (x < (C.x0+C.x1)/2 ? 1 : -1);
    if(ax==='y') y = Math.max(C.y0+run/2, Math.min(C.y1-run/2, y));
    else         x = Math.max(C.x0+run/2, Math.min(C.x1-run/2, x));
    return { room:host.id, side, ax, x, y, w, run, up, rise: ceil + DECK };
  }

  // STAIR KEEP-OUT: the flight/well footprint PLUS the landing you step onto, on BOTH levels.
  // Nothing may be placed in it — a stair blocked by a shelf on either floor is unusable, and the
  // ground flight and the upper well occupy the same cells, so the zone is derived from the same
  // anchor on each level and only the landing end differs (foot below, arrival above).
  function stairZone(b){
    const st=b.stair; if(!st) return null;
    const hw=st.w/2, pad=0.16, land=0.95;
    const a0=(st.ax==='y'?st.y:st.x)-st.run/2, a1=(st.ax==='y'?st.y:st.x)+st.run/2;
    // ground: land at the FOOT (-up end). upper: land at the ARRIVAL (+up end).
    const lo=a0-(st.up>0?(b.level==='upper'?0:land):(b.level==='upper'?land:0));
    const hi=a1+(st.up>0?(b.level==='upper'?land:0):(b.level==='upper'?0:land));
    return st.ax==='y'
      ? { x0:st.x-hw-pad, x1:st.x+hw+pad, y0:lo-pad, y1:hi+pad }
      : { x0:lo-pad, x1:hi+pad, y0:st.y-hw-pad, y1:st.y+hw+pad };
  }

  // ============================================================================================
  // ITEMS — per-room manual slots
  // ============================================================================================
  function roomGrid(r){
    const C=r.clear, wIn=C.x1-C.x0, lIn=C.y1-C.y0;
    const nx=Math.max(1, Math.floor(wIn/CELL+0.001)), ny=Math.max(1, Math.floor(lIn/CELL+0.001));
    return { cell:CELL, nx, ny, ox:C.x0+(wIn-nx*CELL)/2, oy:C.y0+(lIn-ny*CELL)/2, room:r.id, label:r.label };
  }
  function cellW(G,gx,gy,cw,ch){ return { x:G.ox+(gx+cw/2)*CELL, y:G.oy+(gy+ch/2)*CELL }; }
  const SIDE_ROT = { N:2, S:0, W:3, E:1 };
  // cell-space overlap against the stair keep-out, and a slide that walks a blocked fixture along
  // its wall (or off the zone on the freer axis) before giving up on it
  function hits(G,gx,gy,c,Z){
    const x0=G.ox+gx*CELL, x1=x0+c.cw*CELL, y0=G.oy+gy*CELL, y1=y0+c.ch*CELL;
    return x0<Z.x1 && Z.x0<x1 && y0<Z.y1 && Z.y0<y1;
  }
  function slide(G,gx,gy,c,Z,side){
    const axes = side ? [(side==='N'||side==='S')?'x':'y'] : ['x','y'];
    for(const ax of axes){
      const lim = ax==='x' ? G.nx-c.cw : G.ny-c.ch;
      for(let d=1; d<=lim; d++) for(const s of [-1,1]){
        const nx = ax==='x' ? gx+s*d : gx, ny = ax==='y' ? gy+s*d : gy;
        if(nx<0||ny<0||nx>G.nx-c.cw||ny>G.ny-c.ch) continue;
        if(!hits(G,nx,ny,c,Z)) return { gx:nx, gy:ny };
      }
    }
    return null;
  }
  function itemsOf(b, opts){
    if(opts && opts.items) return opts.items;
    const out=[], Z=stairZone(b);
    for(const r of b.rooms){
      const G=roomGrid(r), L=LAY[r.kind]||[];
      for(const it of L){
        const rot = it.rot!=null ? it.rot : (it.side ? SIDE_ROT[it.side] : 0);
        const c = SI.itemCells(it.k, rot); if(!c) continue;
        const f = SI.fixtureFoot(it.k, rot);
        let wxp, wyp;
        if(it.side){
          const A = it.along!=null?it.along:0.5;
          if(it.side==='N'){ wyp=r.clear.y1 - f.d/2 - 0.02; wxp=r.clear.x0 + (r.clear.x1-r.clear.x0)*A; }
          else if(it.side==='S'){ wyp=r.clear.y0 + f.d/2 + 0.02; wxp=r.clear.x0 + (r.clear.x1-r.clear.x0)*A; }
          else if(it.side==='W'){ wxp=r.clear.x0 + f.d/2 + 0.02; wyp=r.clear.y0 + (r.clear.y1-r.clear.y0)*A; }
          else { wxp=r.clear.x1 - f.d/2 - 0.02; wyp=r.clear.y0 + (r.clear.y1-r.clear.y0)*A; }
        } else {
          wxp = (r.clear.x0+r.clear.x1)/2 + (it.fx||0)*((r.clear.x1-r.clear.x0)/2 - f.w/2);
          wyp = (r.clear.y0+r.clear.y1)/2 + (it.fy||0)*((r.clear.y1-r.clear.y0)/2 - f.d/2);
        }
        let gx = Math.max(0, Math.min(G.nx-c.cw, Math.round((wxp-G.ox)/CELL - c.cw/2)));
        let gy = Math.max(0, Math.min(G.ny-c.ch, Math.round((wyp-G.oy)/CELL - c.ch/2)));
        if(Z && hits(G,gx,gy,c,Z)){
          const fix = slide(G, gx, gy, c, Z, it.side);
          if(!fix) continue;                       // nowhere clear in this room — drop it
          gx=fix.gx; gy=fix.gy;
        }
        out.push({ room:r.id, k:it.k, gx, gy, rot });
      }
    }
    return out;
  }

  // ============================================================================================
  // GEOMETRY
  // ============================================================================================
  function roomShim(b, r){
    return { wall:r.wall, paper:r.paper, wainscot:r.wainscot, trimTone:b.trimTone, floor:r.floor,
      counterTone:b.counterTone, goods:b.goods, wares:b.wares, seed:b.seed, size:b.size,
      weather:b.weather, night:b.night, fZ:0, ceilZ:r.ceilZ, Wd:b.Wd, Ln:b.Ln, wt:WT,
      storefront:b.storefront, windows:b.windows, stock:b.stock, prof:null };
  }
  function mats(b){
    const base = I.makeMats(Object.assign(roomShim(b, b.rooms[0]||{wall:'plaster',paper:'cream',floor:'plank',wainscot:false,ceilZ:2.8}), {}));
    for(const r of b.rooms){
      const rm = I.makeMats(roomShim(b, r));
      for(const k of ROOM_MATS) base[k+'@'+r.idx] = rm[k];
    }
    const wallRamp = (base.plaster && base.plaster.ramp) || ['#333','#444','#555'];
    base.trace = { ramp: wallRamp.map(c=>I.mix(c,'#171310',0.42)) };
    return base;
  }
  // stamp a room's own finish ramps onto the faces it owns
  function roomFaces(out, idx, fn){
    const tmp=[]; fn(tmp);
    const suf='@'+idx;
    for(const f of tmp){ if(ROOM_MATS.indexOf(f.mat)>=0) f.mat=f.mat+suf; out.push(f); }
  }
  function layer(id){ I.setLayer(id); }

  function floorOf(out, b, r, hole){
    const ft=I.floorTex(r.floor), fb=I.FLOOR_BIAS[r.floor]!=null?I.FLOOR_BIAS[r.floor]:-3;
    const flat = r.floor==='checker';
    const rects = hole ? subtract(r, hole) : [{x0:r.x0,x1:r.x1,y0:r.y0,y1:r.y1}];
    for(const q of rects){
      out.push(I.F([[q.x0,q.y0,0],[q.x1,q.y0,0],[q.x1,q.y1,0],[q.x0,q.y1,0]], 'floor', fb, 0,
        [[q.x0-r.x0,q.y0-r.y0],[q.x1-r.x0,q.y0-r.y0],[q.x1-r.x0,q.y1-r.y0],[q.x0-r.x0,q.y1-r.y0]], ft, flat));
    }
  }
  function subtract(r, h){
    const out=[];
    if(h.y0>r.y0) out.push({x0:r.x0,x1:r.x1,y0:r.y0,y1:h.y0});
    if(h.y1<r.y1) out.push({x0:r.x0,x1:r.x1,y0:h.y1,y1:r.y1});
    const ly=Math.max(r.y0,h.y0), hy=Math.min(r.y1,h.y1);
    if(h.x0>r.x0) out.push({x0:r.x0,x1:h.x0,y0:ly,y1:hy});
    if(h.x1<r.x1) out.push({x0:h.x1,x1:r.x1,y0:ly,y1:hy});
    return out.filter(q=>q.x1-q.x0>0.01 && q.y1-q.y0>0.01);
  }

  function exteriorWall(out, b, s, B){
    const nx = s.axis==='x'? s.nrm:0, ny = s.axis==='x'? 0:s.nrm;
    if(!I.keepWall(nx,ny,B)) return;
    const r=b.rooms[s.room]; if(!r) return;
    const plane = s.plane - s.nrm*WT, nrm = -s.nrm, ceil = r.ceilZ;
    const shim = roomShim(b, r);
    roomFaces(out, r.idx, (o)=>{
      // wall-top cap, seen from above — the wall's own thickness
      const lo=Math.min(plane,s.plane), hi=Math.max(plane,s.plane);
      if(s.axis==='x') I.slab(o, [[lo,s.a0],[hi,s.a0],[hi,s.a1],[lo,s.a1]], ceil, 'plaster', 0.55);
      else             I.slab(o, [[s.a0,lo],[s.a1,lo],[s.a1,hi],[s.a0,hi]], ceil, 'plaster', 0.55);
      I.finishBands(o, s.axis, plane, nrm, s.a0, s.a1, 0, ceil, shim);
      // gable field where the roof springs off this wall line
      if(b.level==='upper' && s.axis==='y'){
        const prof=I.roofProfile(b.shape==='falseFront'?'gable':b.shape, b.Wd/2, ceil, b.rise);
        const gm = r.wall==='board'||r.wall==='stud'?'wood' : r.wall==='block'?'cinder':'paper';
        I.gableField(o, Object.assign({}, shim, { prof, ceilZ:ceil }), plane, nrm, gm, I.wallTex(r.wall==='stud'?'board':r.wall));
      }
      openingsExterior(o, b, s, r, plane, nrm, ceil);
    });
  }
  function openingsExterior(o, b, s, r, plane, nrm, ceil){
    const street = s.side==='N' && s.line===b.mainC.cj1 && r.block==='main';
    const sill = 1.0, ww=0.82, wh=Math.min(1.15, ceil-sill-0.30);
    if(street && b.level==='ground'){
      I.storefrontInside(o, Object.assign({}, roomShim(b,r), { Wd:b.Wd, ceilZ:ceil, fZ:0, storefront:b.storefront }), plane, nrm);
      return;
    }
    if(wh<0.45) return;
    const n = Math.max(street?2:0, Math.round((s.len/2.9)*(0.55+r.winD)));
    for(let i=0;i<n;i++){ const c=s.a0 + s.len*((i+0.5)/n);
      I.windowOn(o, s.axis, plane, nrm, c, sill, ww, wh, b.windows); }
    // service entrance: the deepest exterior S wall on the ground floor
    if(b.level==='ground' && s.side==='S' && s.line===b.env.J0 && s.len>1.5)
      I.doorwayOn(o, s.axis, plane, nrm, s.a0+s.len*0.5, 0, 1.02, Math.min(2.10, ceil-0.20), true, false);
  }

  // ---- party walls ---------------------------------------------------------------------------
  function openingOn(b, s, rA, rB, opts){
    const over = opts && opts.doors && opts.doors[s.key];
    let kind = over || doorKind(rA.kind, rB.kind);
    let spec = DOOR_SPEC[kind]||DOOR_SPEC.doorway;
    const top = Math.min(rA.ceilZ, rB.ceilZ);
    let w = spec.w;
    if(s.len < w + 0.70){ kind='doorway'; spec=DOOR_SPEC.doorway; w=Math.min(spec.w, s.len-0.36); }
    if(w < 0.55) return null;                          // stub too short for any opening
    const h = Math.min(spec.h, top - spec.sill - 0.12);
    if(h < 0.55) return null;
    return { kind, c:(s.a0+s.a1)/2, w, h, sill:spec.sill, top:spec.sill+h, label:spec.label };
  }
  function partyWall(out, b, s, B, opts){
    const rA=b.rooms[s.A], rB=b.rooms[s.B]; if(!rA||!rB) return null;
    const top=Math.max(rA.ceilZ, rB.ceilZ);
    const f0=s.plane-PT/2, f1=s.plane+PT/2;
    const t = s.axis==='x' ? B.stt : B.ct;
    const solid = Math.abs(t) <= 0.35;
    const op = openingOn(b, s, rA, rB, opts);
    const lo = op ? op.c-op.w/2 : s.a1, hi = op ? op.c+op.w/2 : s.a1;
    const panels = op ? [[s.a0,lo],[hi,s.a1]] : [[s.a0,s.a1]];

    const core=(a,c,z0,z1)=>{ if(c-a<0.015||z1-z0<0.015) return;
      if(s.axis==='x') I.box(out, f0,f1, a,c, z0,z1, 'plaster', null, 0.0);
      else             I.box(out, a,c, f0,f1, z0,z1, 'plaster', null, 0.0); };
    const face=(r,plane,nrm,a,c,z0,z1)=>{ if(c-a<0.02||z1-z0<0.05) return;
      roomFaces(out, r.idx, (o)=> I.finishBands(o, s.axis, plane, nrm, a, c, z0, z1, roomShim(b,r))); };

    if(solid){
      for(const [a,c] of panels){ core(a,c,0,top);
        face(rA, f0, -1, a, c, 0, rA.ceilZ); face(rB, f1, 1, a, c, 0, rB.ceilZ); }
      if(op){
        if(op.sill>0.02){ core(lo,hi,0,op.sill); face(rA,f0,-1,lo,hi,0,op.sill); face(rB,f1,1,lo,hi,0,op.sill); }
        if(top-op.top>0.05) core(lo,hi,op.top,top);
      }
    } else {
      // CUT BACK: footprint trace, return nibs, and the opening left standing free
      const nib = Math.max(0, Math.min(0.62, (s.len - (op?op.w:0))/2 - 0.10));
      if(s.axis==='x') I.slab(out, [[f0,s.a0],[f1,s.a0],[f1,s.a1],[f0,s.a1]], 0.014, 'trace', -1.1);
      else             I.slab(out, [[s.a0,f0],[s.a1,f0],[s.a1,f1],[s.a0,f1]], 0.014, 'trace', -1.1);
      if(nib>0.12){
        for(const [a,c] of [[s.a0,s.a0+nib],[s.a1-nib,s.a1]]){ core(a,c,0,top);
          face(rA,f0,-1,a,c,0,rA.ceilZ); face(rB,f1,1,a,c,0,rB.ceilZ); }
      }
      if(op){
        if(op.sill>0.02){ core(lo,hi,0,op.sill); face(rA,f0,-1,lo,hi,0,op.sill); face(rB,f1,1,lo,hi,0,op.sill); }
        const jw=0.10, hz=Math.min(top, op.top+0.13);
        core(lo-jw,lo,0,hz); core(hi,hi+jw,0,hz);                       // jambs
        core(lo-jw,hi+jw,op.top,hz);                                    // lintel
      }
    }
    if(op) doorLeaf(out, b, s, op, rA, rB, f0, f1, solid);
    return op;
  }
  function doorLeaf(out, b, s, op, rA, rB, f0, f1, solid){
    const ax=s.axis, lo=op.c-op.w/2, hi=op.c+op.w/2, mid=(f0+f1)/2;
    const put=(a,c,z0,z1,mat,bias,nrm)=>{ if(ax==='x') I.decalX(out, nrm>0?f1:f0, nrm, a,c, z0,z1, mat, bias, null, true, 0.09);
                                          else         I.decalY(out, nrm>0?f1:f0, nrm, a,c, z0,z1, mat, bias, null, true, 0.09); };
    const slabBox=(a,c,z0,z1,mat)=>{ if(ax==='x') I.box(out, f0+0.012,f1-0.012, a,c, z0,z1, mat, null, 0.05);
                                     else         I.box(out, a,c, f0+0.012,f1-0.012, z0,z1, mat, null, 0.05); };
    // threshold
    if(ax==='x') I.slab(out, [[f0,lo],[f1,lo],[f1,hi],[f0,hi]], 0.022, 'wood', 0.35);
    else         I.slab(out, [[lo,f0],[hi,f0],[hi,f1],[lo,f1]], 0.022, 'wood', 0.35);
    const K=op.kind;
    if(K==='slab'){ slabBox(lo+0.02,hi-0.02, 0.01, op.top-0.01, 'wood');
      for(const n of [-1,1]) for(let i=1;i<3;i++){ const p=lo+op.w*(i/3);
        put(p-0.02,p+0.02, 0.06, op.top-0.06, 'wood', -1.6, n); }
      for(const n of [-1,1]) put(hi-0.20,hi-0.12, op.top*0.46, op.top*0.46+0.13, 'trim', 0.9, n);
    } else if(K==='swingPair'){
      for(const half of [[lo+0.02,op.c-0.02],[op.c+0.02,hi-0.02]]){
        slabBox(half[0],half[1], 0.01, op.top-0.01, 'trim');
        for(const n of [-1,1]){
          put(half[0]+0.05,half[1]-0.05, 0.06, 0.34, 'zinc', 0.7, n);                 // kick plate
          put(half[0]+0.10,half[1]-0.10, op.top-0.62, op.top-0.20, 'glass', 0.2, n);   // porthole light
        }
      }
    } else if(K==='dutch'){
      slabBox(lo+0.02,hi-0.02, 0.01, op.top*0.52, 'wood');
      for(const n of [-1,1]) put(lo+0.06,hi-0.06, op.top*0.52-0.10, op.top*0.52-0.02,'trim',0.9,n);
    } else if(K==='curtain'){
      const nf=5; for(let i=0;i<nf;i++){ const a=lo+op.w*(i/nf), c=lo+op.w*((i+1)/nf);
        for(const n of [-1,1]) put(a+0.01,c-0.01, 0.18, op.top-0.06, 'cloth', i%2?0.35:-0.45, n); }
      for(const n of [-1,1]) put(lo,hi, op.top-0.06, op.top-0.01, 'brass', 0.9, n);
    } else if(K==='hatch'){
      // shelf through the opening, plus the flap propped up
      if(ax==='x') I.box(out, f0-0.16,f1+0.16, lo,hi, op.sill-0.07, op.sill, 'worktop', null, 0.4);
      else         I.box(out, lo,hi, f0-0.16,f1+0.16, op.sill-0.07, op.sill, 'worktop', null, 0.4);
      for(const n of [-1,1]) put(lo,hi, op.top, op.top+0.30, 'wood', 0.5, n);
    } else if(K==='arch'){
      const rr=Math.min(op.w*0.5, 0.46);
      for(const n of [-1,1]){ put(lo-0.10,hi+0.10, op.top-0.02, op.top+0.10, 'trim', 0.6, n);
        put(lo-0.10,lo+rr*0.5, op.top-rr, op.top, 'trim', 0.45, n);
        put(hi-rr*0.5,hi+0.10, op.top-rr, op.top, 'trim', 0.45, n); }
    }
    if(!solid && (K==='doorway'||K==='arch'||K==='curtain')){
      // a free-standing casing needs a visible head so the opening still reads
      if(ax==='x') I.box(out, f0-0.03,f1+0.03, lo-0.11,hi+0.11, op.top, op.top+0.11,'trim',null,0.7);
      else         I.box(out, lo-0.11,hi+0.11, f0-0.03,f1+0.03, op.top, op.top+0.11,'trim',null,0.7);
    }
  }

  // ---- roof undersides + joists ---------------------------------------------------------------
  function roofUnder(out, b, r, B, shape, riseIn){
    const hw=(r.x1-r.x0)/2, cxr=(r.x0+r.x1)/2;
    const prof=I.roofProfile(shape==='falseFront'?'gable':shape, hw, r.ceilZ, riseIn);
    const keepE=I.keepWall(1,0,B), keepW=I.keepWall(-1,0,B), keepN=I.keepWall(0,1,B), keepS=I.keepWall(0,-1,B);
    const xR=cxr+I.ridgeX(prof), xLo=keepW?r.x0:xR, xHi=keepE?r.x1:xR;
    const inset=(keepE&&keepW)?(r.y1-r.y0)*0.40:0;
    const yLo=keepS?r.y0:r.y0+inset, yHi=keepN?r.y1:r.y1-inset;
    if(xHi-xLo<0.2||yHi-yLo<0.2) return;
    const cMat=(r.wall==='plaster'||r.wall==='wainscot'||r.wall==='tile')?'plaster':(r.wall==='block'?'cinder':'wood');
    const cTex=cMat==='plaster'?I.wallTex('plaster'):cMat==='cinder'?I.wallTex('block'):I.wallTex('board');
    roomFaces(out, r.idx, (o)=>{
      for(let i=0;i+1<prof.length;i++){
        let ax=cxr+prof[i][0], az=prof[i][1], bx=cxr+prof[i+1][0], bz=prof[i+1][1];
        if(bx<=xLo+0.01||ax>=xHi-0.01) continue;
        if(ax<xLo){ az=I.profZ(prof,xLo-cxr); ax=xLo; }
        if(bx>xHi){ bz=I.profZ(prof,xHi-cxr); bx=xHi; }
        const L=Math.hypot(bx-ax,bz-az);
        o.push(I.F([[ax,yLo,az],[bx,yLo,bz],[bx,yHi,bz],[ax,yHi,az]], cMat, bz<az?0.30:-0.30, 0,
          [[0,0],[L,0],[L,yHi-yLo],[0,yHi-yLo]], cTex));
      }
      if(b.beams){
        const nR=Math.max(2, Math.round((yHi-yLo)/1.5));
        for(let k=0;k<=nR;k++){ const yy=yLo+(yHi-yLo)*(k/nR);
          for(let i=0;i+1<prof.length;i++){
            let ax=cxr+prof[i][0], az=prof[i][1], bx=cxr+prof[i+1][0], bz=prof[i+1][1];
            if(bx<=xLo+0.01||ax>=xHi-0.01) continue;
            if(ax<xLo){ az=I.profZ(prof,xLo-cxr); ax=xLo; }
            if(bx>xHi){ bz=I.profZ(prof,xHi-cxr); bx=xHi; }
            o.push(I.F([[ax,yy-0.055,az-0.13],[bx,yy-0.055,bz-0.13],[bx,yy+0.055,bz-0.13],[ax,yy+0.055,az-0.13]],'wood',0.25,0.07));
            o.push(I.F([[ax,yy+0.055,az-0.13],[bx,yy+0.055,bz-0.13],[bx,yy+0.055,bz-0.02],[ax,yy+0.055,az-0.02]],'wood',-0.35,0.08));
          } }
      }
    });
  }
  function deckJoists(out, b, r){
    if(!b.beams) return;
    const C=r.clear, bz=r.ceilZ-0.20, nb=Math.max(2, Math.round((C.y1-C.y0)/1.9));
    for(let i=1;i<nb;i++){ const yy=C.y0+(C.y1-C.y0)*(i/nb);
      I.joist(out, C.x0, C.x1, yy-0.065, yy+0.065, bz, r.ceilZ-0.03, 'wood'); }
  }

  // ---- stair ---------------------------------------------------------------------------------
  function stairFlight(out, b, st){
    const n=Math.max(6, Math.round(st.rise/0.185)), tr=st.run/n, ri=st.rise/n, hw=st.w/2;
    const along=(v)=> st.ax==='y' ? st.y + st.up*v : st.x + st.up*v;
    for(let i=0;i<n;i++){
      const a0=along(i*tr - st.run/2), a1=along((i+1)*tr - st.run/2);
      const lo=Math.min(a0,a1), hi=Math.max(a0,a1), z=(i+1)*ri;
      if(st.ax==='y') I.box(out, st.x-hw, st.x+hw, lo, hi, z-0.055, z, 'wood', I.plankTex(0.24), 0.15);
      else            I.box(out, lo, hi, st.y-hw, st.y+hw, z-0.055, z, 'wood', I.plankTex(0.24), 0.15);
      // riser face
      if(st.ax==='y') I.decalY(out, st.up>0?lo:hi, -st.up, st.x-hw, st.x+hw, z-ri, z-0.055, 'wood', -0.5, null, true, 0.06);
      else            I.decalX(out, st.up>0?lo:hi, -st.up, st.y-hw, st.y+hw, z-ri, z-0.055, 'wood', -0.5, null, true, 0.06);
    }
    // stringer + newel + rail on the open side
    const side = (st.side==='W'||st.side==='E') ? (st.side==='W'?1:-1) : (st.side==='S'?1:-1);
    const s0=along(-st.run/2), s1=along(st.run/2);
    const lo=Math.min(s0,s1), hi=Math.max(s0,s1);
    if(st.ax==='y'){
      const xo=st.x + (st.side==='W'?hw:-hw);
      I.box(out, xo-0.05, xo+0.05, lo, hi, 0, 0.12, 'wood', null, 0.1);
      I.box(out, xo-0.055, xo+0.055, (st.up>0?hi:lo)-0.055, (st.up>0?hi:lo)+0.055, 0, st.rise+0.92, 'oak', null, 0.3);
      const ns=Math.max(3, Math.round(st.run/0.42));
      for(let i=0;i<ns;i++){ const p=lo+(hi-lo)*((i+0.5)/ns), zz=st.rise*((st.up>0? (p-lo):(hi-p))/(hi-lo));
        I.box(out, xo-0.03, xo+0.03, p-0.03, p+0.03, zz, zz+0.86, 'oak', null, 0.2); }
      for(let i=0;i<ns;i++){ const p0=lo+(hi-lo)*(i/ns), p1=lo+(hi-lo)*((i+1)/ns);
        const z0=st.rise*((st.up>0?(p0-lo):(hi-p0))/(hi-lo))+0.86, z1=st.rise*((st.up>0?(p1-lo):(hi-p1))/(hi-lo))+0.86;
        out.push(I.F([[xo-0.045,p0,z0],[xo+0.045,p0,z0],[xo+0.045,p1,z1],[xo-0.045,p1,z1]],'oak',0.7,0.02)); }
    } else {
      const yo=st.y + (st.side==='S'?hw:-hw);
      I.box(out, lo, hi, yo-0.05, yo+0.05, 0, 0.12, 'wood', null, 0.1);
      I.box(out, (st.up>0?hi:lo)-0.055, (st.up>0?hi:lo)+0.055, yo-0.055, yo+0.055, 0, st.rise+0.92, 'oak', null, 0.3);
      const ns=Math.max(3, Math.round(st.run/0.42));
      for(let i=0;i<ns;i++){ const p=lo+(hi-lo)*((i+0.5)/ns), zz=st.rise*((st.up>0?(p-lo):(hi-p))/(hi-lo));
        I.box(out, p-0.03, p+0.03, yo-0.03, yo+0.03, zz, zz+0.86, 'oak', null, 0.2); }
      for(let i=0;i<ns;i++){ const p0=lo+(hi-lo)*(i/ns), p1=lo+(hi-lo)*((i+1)/ns);
        const z0=st.rise*((st.up>0?(p0-lo):(hi-p0))/(hi-lo))+0.86, z1=st.rise*((st.up>0?(p1-lo):(hi-p1))/(hi-lo))+0.86;
        out.push(I.F([[p0,yo-0.045,z0],[p0,yo+0.045,z0],[p1,yo+0.045,z1],[p1,yo-0.045,z1]],'oak',0.7,0.02)); }
    }
    void side;
  }
  function stairWellRect(st){
    if(!st) return null;
    const hw=st.w/2, pad=0.07;
    return st.ax==='y'
      ? { x0:st.x-hw-pad, x1:st.x+hw+pad, y0:Math.min(st.y-st.up*st.run/2, st.y+st.up*st.run/2)-pad,
          y1:Math.max(st.y-st.up*st.run/2, st.y+st.up*st.run/2)+pad }
      : { x0:Math.min(st.x-st.up*st.run/2, st.x+st.up*st.run/2)-pad,
          x1:Math.max(st.x-st.up*st.run/2, st.x+st.up*st.run/2)+pad, y0:st.y-hw-pad, y1:st.y+hw+pad };
  }
  function stairWell(out, b, st){
    const h=stairWellRect(st); if(!h) return;
    // trimmer upstand round the opening, newel + rail on the two open sides
    const t=0.075;
    I.box(out, h.x0-t, h.x1+t, h.y0-t, h.y0, 0, 0.10, 'wood', null, 0.3);
    I.box(out, h.x0-t, h.x1+t, h.y1, h.y1+t, 0, 0.10, 'wood', null, 0.3);
    I.box(out, h.x0-t, h.x0, h.y0, h.y1, 0, 0.10, 'wood', null, 0.3);
    I.box(out, h.x1, h.x1+t, h.y0, h.y1, 0, 0.10, 'wood', null, 0.3);
    const rail=(x0,x1,y0,y1)=>{
      const horiz=(x1-x0)>(y1-y0), L=horiz?(x1-x0):(y1-y0), n=Math.max(2,Math.round(L/0.40));
      for(let i=0;i<=n;i++){ const f=i/n, px=horiz?x0+(x1-x0)*f:(x0+x1)/2, py=horiz?(y0+y1)/2:y0+(y1-y0)*f;
        I.box(out, px-0.028, px+0.028, py-0.028, py+0.028, 0.10, 0.10+((i===0||i===n)?0.94:0.82), 'oak', null, 0.25); }
      if(horiz) I.box(out, x0,x1, (y0+y1)/2-0.045, (y0+y1)/2+0.045, 1.00, 1.07, 'oak', null, 0.7);
      else      I.box(out, (x0+x1)/2-0.045, (x0+x1)/2+0.045, y0,y1, 1.00, 1.07, 'oak', null, 0.7);
    };
    if(st.ax==='y'){ rail(h.x0-t, h.x1+t, st.up>0?h.y0-t:h.y1, st.up>0?h.y0:h.y1+t);
      rail(h.x0-t, h.x0, h.y0, h.y1); }
    else { rail(st.up>0?h.x0-t:h.x1, st.up>0?h.x0:h.x1+t, h.y0-t, h.y1+t);
      rail(h.x0, h.x1, h.y0-t, h.y0); }
  }

  // ============================================================================================
  // BUILD
  // ============================================================================================
  function build(b, B, items, opts){
    const out=[];
    const st=b.stair, well = (b.level==='upper' && st) ? stairWellRect(st) : null;

    for(const r of b.rooms){
      layer('fl'+r.idx);
      const hole = well && overlaps(r, well) ? clip(well, r) : null;
      roomFaces(out, r.idx, (o)=> floorOf(o, b, r, hole));
    }
    for(const s of b.walls){
      if(s.party){ layer('pw'+s.A+'_'+s.B+'@'+s.key); partyWall(out, b, s, B, opts); }
      else { layer('xw'+s.side+s.line+'_'+s.c0); exteriorWall(out, b, s, B); }
    }
    for(const r of b.rooms){
      layer('ce'+r.idx);
      if(r.roofOver==='main') roofUnder(out, b, r, B, b.shape, b.rise);
      else if(r.roofOver==='wing') roofUnder(out, b, r, B, 'gable', (r.x1-r.x0)/2*0.62);
      else deckJoists(out, b, r);
    }
    if(st && st.level===b.level){
      layer('stair');
      if(b.level==='upper') stairWell(out, b, st); else stairFlight(out, b, st);
    }
    const byId={}; b.rooms.forEach(r=>byId[r.id]=r);
    items.forEach((it, idx)=>{
      const r=byId[it.room]||b.rooms[0]; if(!r) return;
      if(!SI.FIXTURES[it.k]) return;
      layer('it'+idx);
      const G=roomGrid(r), c=SI.itemCells(it.k, it.rot), w=cellW(G, it.gx, it.gy, c.cw, c.ch);
      const s={ stock:b.stock, wares:b.wares, cloth:(b.type==='restaurant'||b.type==='bakery'),
        rnd: I.mulberry32(3301 + b.seed*97 + idx*7919) };
      roomFaces(out, r.idx, (o)=> I.placeItem(o, it.k, s, w.x, w.y, it.rot|0));
    });
    layer('base');
    return out;
  }
  function overlaps(r,q){ return r.x0<q.x1 && q.x0<r.x1 && r.y0<q.y1 && q.y0<r.y1; }
  function clip(q,r){ return { x0:Math.max(q.x0,r.x0), x1:Math.min(q.x1,r.x1),
                               y0:Math.max(q.y0,r.y0), y1:Math.min(q.y1,r.y1) }; }

  // ============================================================================================
  // SHEET / RENDER
  // ============================================================================================
  const SHEETS={};
  function sheet(opts){
    need();
    const b=resolve(opts), elev=(opts&&opts.elev!=null)?opts.elev:DEFAULT_ELEV;
    const key=b.type+'|'+b.level+'|'+b.size+'|'+elev+'|'+JSON.stringify((opts&&opts.rooms)||0);
    if(SHEETS[key]) return SHEETS[key];
    let L=0,R=0,U=0,Dn=0;
    const zs=[0, b.peakZ];
    for(let d=0;d<8;d++){ const B=I.camBasis({dir:d, elev});
      const o=I.projVert(0,0,0,B);
      for(const X of [b.env.x0,b.env.x1]) for(const Y of [b.env.y0,b.env.y1]) for(const Z of zs){
        const p=I.projVert(X,Y,Z,B);
        L=Math.max(L,o.sx-p.sx); R=Math.max(R,p.sx-o.sx); U=Math.max(U,o.sy-p.sy); Dn=Math.max(Dn,p.sy-o.sy); } }
    const pad=16;
    const out={ W:Math.min(3200, Math.ceil(L+R)+pad*2), H:Math.min(3200, Math.ceil(U+Dn)+pad*2),
      cx:Math.round(L+pad), gy:Math.round(U+pad) };
    SHEETS[key]=out; return out;
  }
  function view(opts){ const S=sheet(opts); I.setView(S.W, S.H, S.cx, S.gy); return S; }

  function render(dir, opts){
    need(); opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const S=view(opts), b=resolve(opts), B=I.camBasis({dir, elev:opts.elev});
    const M=mats(b), faces=build(b, B, itemsOf(b,opts), opts);
    const rgba=I.toRGBA(I.post(I.paint(faces, B, M), b));
    I.resetView();
    return { rgba, W:S.W, H:S.H, pivot:{x:S.cx,y:S.gy} };
  }
  function renderLayers(dir, opts){
    need(); opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const S=view(opts), b=resolve(opts), B=I.camBasis({dir, elev:opts.elev});
    const M=mats(b), items=itemsOf(b,opts), faces=build(b, B, items, opts);
    const bufs=I.paint(faces, B, M), cols=I.post(bufs, b, true), lb=bufs.lbuf, N=S.W*S.H;
    const groups={};
    for(let i=0;i<N;i++){ const l=lb[i]; if(cols[i]==null||l==null) continue; (groups[l]||(groups[l]=[])).push(i); }
    const kindOf=(id)=> id.slice(0,2)==='fl'?'floor' : id.slice(0,2)==='xw'?'wall' : id.slice(0,2)==='pw'?'party'
      : id.slice(0,2)==='ce'?'ceiling' : id==='stair'?'stair' : id.slice(0,2)==='it'?'fixture':'base';
    const rank=(id)=>({floor:0,fixture:1,party:2,wall:3,stair:1,ceiling:4,base:5})[kindOf(id)];
    const layers=Object.keys(groups).sort((a,c)=>rank(a)-rank(c)).map(id=>{
      const rgba=new Uint8ClampedArray(N*4);
      for(const i of groups[id]){ const c=cols[i]; if(!c) continue;
        const r=parseInt(c.slice(1,3),16), g=parseInt(c.slice(3,5),16), bl=parseInt(c.slice(5,7),16), j=i*4;
        rgba[j]=r; rgba[j+1]=g; rgba[j+2]=bl; rgba[j+3]=255; }
      I.addKeyline(rgba);
      const m=/^it(\d+)$/.exec(id);
      return { id, kind:kindOf(id), item: m?items[+m[1]]:null, rgba };
    });
    const out={ W:S.W, H:S.H, pivot:{x:S.cx,y:S.gy}, layers, anchors:anchors(dir,opts) };
    I.resetView(); return out;
  }
  function ghost(rgba, keep, W, H){ need(); return SI.ghost(rgba, keep, W, H); }

  function anchors(dir, opts){
    need(); opts=opts||{};
    const S=view(opts), b=resolve(opts), B=I.camBasis({dir, elev:opts.elev});
    const pj=(x,y,z)=>{ const v=I.projVert(x,y,z,B); return { x:v.sx, y:v.sy }; };
    const rooms=b.rooms.map(r=>({ id:r.id, kind:r.kind, label:r.label, block:r.block, ceilZ:r.ceilZ,
      centre:pj((r.clear.x0+r.clear.x1)/2,(r.clear.y0+r.clear.y1)/2,0),
      rect:[pj(r.clear.x0,r.clear.y0,0),pj(r.clear.x1,r.clear.y0,0),pj(r.clear.x1,r.clear.y1,0),pj(r.clear.x0,r.clear.y1,0)],
      clear:r.clear }));
    const doors=[], occ=[];
    for(const s of b.walls){
      if(s.party){
        const rA=b.rooms[s.A], rB=b.rooms[s.B]; if(!rA||!rB) continue;
        const op=openingOn(b,s,rA,rB,opts); if(!op) continue;
        const p = s.axis==='x' ? pj(s.plane, op.c, 0) : pj(op.c, s.plane, 0);
        doors.push({ kind:op.kind, label:op.label, from:rA.id, to:rB.id, at:p, w:op.w, seg:s.key });
        continue;
      }
      const nx=s.axis==='x'?s.nrm:0, ny=s.axis==='x'?0:s.nrm;
      if(!I.keepWall(nx,ny,B)) continue;
      const r=b.rooms[s.room]; if(!r) continue;
      const p0 = s.axis==='x' ? pj(s.plane,s.a0,0) : pj(s.a0,s.plane,0);
      const p1 = s.axis==='x' ? pj(s.plane,s.a1,0) : pj(s.a1,s.plane,0);
      occ.push({ id:'xw'+s.side+s.line, kind:'wall', base:[p0,p1],
        worldY: s.axis==='x'?0:s.plane, topY:(s.axis==='x'?pj(s.plane,(s.a0+s.a1)/2,r.ceilZ):pj((s.a0+s.a1)/2,s.plane,r.ceilZ)).y });
    }
    const items=itemsOf(b,opts), byId={}; b.rooms.forEach(r=>byId[r.id]=r);
    const fixtures=items.map((it,idx)=>{
      const r=byId[it.room]||b.rooms[0]; if(!r||!SI.FIXTURES[it.k]) return null;
      const G=roomGrid(r), c=SI.itemCells(it.k,it.rot), w=cellW(G,it.gx,it.gy,c.cw,c.ch);
      const P=SI.FIXTURES[it.k], f=SI.fixtureFoot(it.k,it.rot);
      if(P.tall) occ.push({ id:'it'+idx, kind:'fixture', item:it.k,
        base:[pj(w.x-f.w/2,w.y,0), pj(w.x+f.w/2,w.y,0)], worldY:w.y, topY:pj(w.x,w.y,1.9).y });
      return { idx, k:it.k, room:it.room, cat:P.cat, builtin:!!P.builtin, tall:!!P.tall,
        x:w.x, y:w.y, rot:it.rot|0, px:pj(w.x,w.y,0) };
    }).filter(Boolean);
    const streetDoorX = b.storefront==='plate'?-b.Wd*0.28 : b.storefront==='smallPane'?b.Wd*0.26
      : b.storefront==='hatch'?-b.Wd*0.30 : b.storefront==='narrow'?-b.Wd*0.24 : 0;
    const st=b.stair;
    const out={
      type:b.type, label:b.label, ext:b.ext, level:b.level, levelZ:b.levelZ,
      Wd:b.Wd, Ln:b.Ln, fH:b.fH, shopH:b.shopH, plate:b.plate, peakZ:b.peakZ,
      env:b.env, wing:wingOf(b.type, b.size),
      rooms, doors, fixtures, occluders:occ,
      walkable: b.rooms.map(r=>({ room:r.id, x0:r.clear.x0, x1:r.clear.x1, y0:r.clear.y0, y1:r.clear.y1 })),
      door: pj(streetDoorX, b.streetY, 0),
      backDoor: pj((b.env.x0+b.env.x1)/2, b.env.y0, 0),
      stair: st ? { room:st.room, at:pj(st.x, st.y, 0), top:pj(st.x, st.y, st.rise), rise:st.rise,
                    well: b.level==='upper'?stairWellRect(st):null, clear:stairZone(b) } : null,
      lamps: b.rooms.map(r=>pj((r.clear.x0+r.clear.x1)/2,(r.clear.y0+r.clear.y1)/2, r.ceilZ*0.84)),
      pivot:{x:S.cx,y:S.gy}, W:S.W, H:S.H,
    };
    I.resetView(); return out;
  }
  function project(dir, p, elev, opts){ need(); view(opts||{});
    const v=I.projVert(p[0],p[1],p[2], I.camBasis({dir,elev})); const o={x:v.sx,y:v.sy}; I.resetView(); return o; }

  // ---- the exterior contract: what shopfrontRig must grow to cover this plan ------------------
  function wingOf(type, size){
    need();
    const P=PLANS[type]; if(!P) return null;
    const T=SI.TYPES[type], sz=size!=null?size:T.size, D=SI.dims({type,size:sz});
    const MW=Math.max(4,Math.round(D.Wd/CELL)), ML=Math.max(4,Math.round(D.Ln/CELL));
    const kx=MW/P.main.w, ky=ML/P.main.h;
    const ci=(u)=>Math.round((u-P.main.x)*kx), cj=(u)=>Math.round((u-P.main.y)*ky);
    let i0=Infinity,i1=-Infinity,j0=Infinity,j1=-Infinity,any=false;
    for(const r of (P.ground||[])){
      const R={ci0:ci(r.x),ci1:ci(r.x+r.w),cj0:cj(r.y),cj1:cj(r.y+r.h)};
      if(R.cj0<0){ any=true; i0=Math.min(i0,R.ci0); i1=Math.max(i1,R.ci1); j0=Math.min(j0,R.cj0); j1=Math.max(j1,Math.min(0,R.cj1)); }
    }
    if(!any) return null;
    const wgW=(i1-i0)*CELL, depth=(0-j0)*CELL;
    const K=KINDS[((P.ground||[]).find(r=>cj(r.y)<0)||{}).kind]||KINDS.kitchen;
    const ceil=Math.max(2.35, D.shopH*(K.ceil||0.9));
    // wallH is in GRADE coords, like shopfrontRig's own wallH: pavement -> wing eave
    return { depth, width:wgW, offset:((i0+i1)/2 - MW/2)*CELL, wallH:D.fH+ceil+0.30, ceil,
             shape:'gable', pitch:0.62, rise:wgW/2*0.62, ridgeAxis:'y' };
  }

  // ---- the interior contract: ONE room's box, so shopInteriorRig renders it at plan size ------
  // shopInteriorRig calls this for (type, room) and adopts the numbers, which is what makes a
  // fixture list authored in the single-room editor drop straight into this room of the building:
  // same clear rect, same nx/ny, same cell size. `sides` says which of the four walls is on the
  // envelope (exterior, gets glazing) and which is a party wall (gets a doorway instead).
  function roomBox(type, room, size){
    need();
    const P=PLANS[type]; if(!P) return null;
    const up=(P.upper||[]).some(r=>r.id===room);
    if(!up && !(P.ground||[]).some(r=>r.id===room)) return null;
    const b=resolve({ type, size, level:up?'upper':'ground' });
    const r=b.rooms.find(q=>q.id===room); if(!r) return null;
    const C=r.clear, G=roomGrid(r), tol=WT-0.005;
    return { room:r.id, kind:r.kind, label:r.label, level:up?'upper':'ground', block:r.block,
      Wd:(C.x1-C.x0)+2*WT, Ln:(C.y1-C.y0)+2*WT, ceilZ:r.ceilZ, roofOver:r.roofOver,
      nx:G.nx, ny:G.ny, cell:CELL,
      sides:{ N:(r.y1-C.y1)>=tol, S:(C.y0-r.y0)>=tol, E:(r.x1-C.x1)>=tol, W:(C.x0-r.x0)>=tol },
      street: Math.abs(r.y1 - b.streetY) < 0.01 };
  }

  function dims(opts){ const b=resolve(opts||{});
    return { type:b.type, label:b.label, ext:b.ext, level:b.level, hasUpper:b.hasUpper,
      Wd:b.Wd, Ln:b.Ln, wallH:b.wallH, fH:b.fH, shopH:b.shopH, plate:b.plate, peakZ:b.peakZ,
      levelZ:b.levelZ, env:b.env, wing:wingOf(b.type,b.size),
      rooms:b.rooms.map(r=>({ id:r.id, kind:r.kind, label:r.label, block:r.block, ceilZ:r.ceilZ,
        cells:[r.ci1-r.ci0, r.cj1-r.cj0], clear:r.clear })),
      doors:b.walls.filter(s=>s.party).length, walls:b.walls.length,
      stair:b.stair?{room:b.stair.room, side:b.stair.side, rise:b.stair.rise}:null }; }
  function roomsOf(opts){ return resolve(opts||{}).rooms.map(r=>({ id:r.id, kind:r.kind, label:r.label,
    block:r.block, ceilZ:r.ceilZ, grid:roomGrid(r) })); }
  function gridOf(opts, roomId){ const b=resolve(opts||{});
    const r=b.rooms.find(q=>q.id===roomId)||b.rooms[0]; return r?roomGrid(r):null; }

  // the derived plan, for the engine and for the plan diagram: every wall segment with its class,
  // the two rooms it separates and the opening that was punched in it
  function wallsOf_(opts){
    const b=resolve(opts||{});
    return b.walls.map(s=>{
      const rA=s.A>=0?b.rooms[s.A]:null, rB=s.B>=0?b.rooms[s.B]:null;
      const op=(s.party&&rA&&rB)?openingOn(b,s,rA,rB,opts||{}):null;
      return { axis:s.axis, plane:s.plane, a0:s.a0, a1:s.a1, len:s.len, party:s.party, side:s.side,
        key:s.key, nrm:s.nrm, rooms:[rA?rA.id:null, rB?rB.id:null],
        door: op?{ kind:op.kind, c:op.c, w:op.w, sill:op.sill, top:op.top, label:op.label }:null };
    });
  }

  root.ShopBuilding = { PX, CELL, DIRS:8, defaultElev:DEFAULT_ELEV,
    order:['N','NE','E','SE','S','SW','W','NW'],
    PLANS, KINDS, LAY, DOORS, DOOR_SPEC, PAIR,
    plans:()=>Object.keys(PLANS), rooms:roomsOf, dims, sheet, grid:gridOf, items:(o)=>itemsOf(resolve(o||{}), o||{}),
    walls:wallsOf_, env:(o)=>resolve(o||{}).env, stairOf:(o)=>resolve(o||{}).stair,
    stairZone:(o)=>stairZone(resolve(o||{})),
    doorKind, wingOf, roomBox, render, renderLayers, ghost, anchors, project, roomGrid, cellW };
})(typeof globalThis!=='undefined'?globalThis:window);
