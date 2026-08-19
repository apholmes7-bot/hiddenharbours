/* Hidden Harbours — SHARED ISO BOAT INTERIOR rig (ADR-0006 bake pipeline; camperInteriorRig.js
   precedent, scaled to the fleet). One rig, many hulls: every cabined boat's room is built here
   from the loft its EXTERIOR rig publishes — cell, pivot, stations, house planes, glazing, door,
   cuddy — so an interior composites UNDER its exterior 1:1 and cannot drift off its hull.

   REGISTRATION CONTRACT (the whole point of the file):
     cell + pivot   W/H/pivot are the exterior's, per hull. Same camBasis (incl. roll/pitch/heave),
                    so an interior rides the same wave as the hull it lives in.
     the loft       station / skin / dfrac / halfAtZ / sheerZ and the HOUSE block are read from
                    <Hull>.loft. NOTHING is re-derived here. If the hull changes, the room follows.
     the door       the exterior owns the leaf (opts.doorOpen). This rig draws the INNER face of
                    the same leaf at the same open fraction, so the two sprites always agree.
     the fit-out    LAYOUT here is our layer (gameplay's), in metres, clamped to the published plan.

   THE CUT: near walls are culled by the fleet's facing test (outward normal turned toward the
   camera in plan) and replaced by a low bright section lip, so the room reads as a sectioned
   model, not a floating floor. Far walls keep a short roof lip to say there was a ceiling.

   LEVELS. level:'house' is the wheelhouse; level:'cuddy' is the under-foredeck berth space where
   the hull has one (both first-tranche hulls do). Each level bakes to the full hull cell.

   INTERACTABLES (the reason the room exists): helm=drive, stove=cook, locker=storage,
   berth=sleep_save (cuddy level). focus:'<id>' lifts one a ramp step and rims it warm.
   hotspots(dir,opts) -> screen rects + world reach points, per level, wall-visibility resolved.
   interactables(opts) / gameplaySections(opts) generate the sidecar INTERACT / DECK / THRESHOLD /
   STAIRS additions — Art/_sidecarExport.js stamps them against the EXTERIOR rig's bytes.

   Exposes globalThis.BoatInterior = { HULLS, LAYERS, ITEMS, list(), cellOf(hull), dims(opts),
     resolve(opts), render(dir,opts), renderLayers(dir,opts), hotspots(dir,opts), anchors(dir,opts),
     project(hull,dir,p,opts), interactables(opts), gameplaySections(opts) }. */
(function (root) {
  const DEG = Math.PI/180, WT = 0.07;          // wall thickness the room is inset by
  const RIM = '#f6d98a';

  const HULLS = {
    lobster: { sym:'LobsterBoatIso', rig:'lobsterBoatIsoRig.js', label:'Lobster Boat · 12.0 m',
               liner:'CREAM', metal:'STEEL', railZ:0.78 },
    cape:    { sym:'CapeIslanderIso', rig:'capeIslanderIsoRig.js', label:'Cape Islander · 12.8 m',
               liner:'CREAM', metal:'MOTO', railZ:0.80 },
    dragger: { sym:'SideDraggerIso', rig:'sideDraggerIsoRig.js', label:'Side Dragger · 25 m',
               liner:'CREAM', metal:'STEEL', railZ:0.90 },
    trawler: { sym:'SternTrawlerIso', rig:'sternTrawlerIsoRig.js', label:'Stern Trawler · 38 m',
               liner:'CREAM', metal:'STEEL', railZ:0.90 },
    trawler2:{ sym:'SternTrawlerMk2Iso', rig:'sternTrawlerMk2IsoRig.js', label:'Stern Trawler Mk II · 38 m',
               liner:'CREAM', metal:'STEEL', railZ:0.90 },
    packet:  { sym:'CoastalPacketIso', rig:'coastalPacketIsoRig.js', label:'Coastal Packet · 60 m',
               liner:'WHITE', metal:'STEEL', railZ:0.90 },
    tanker:  { sym:'TankerIso', rig:'tankerIsoRig.js', label:'Tanker · 110 m',
               liner:'WHITE', metal:'STEEL', railZ:0.90 },
    sport53: { sym:'SportFisherIso2', pick:'convertible', rig:'sportFisherIsoRig2.js',
               rigUrl:'export/sport-fisher-rig-kit/sportFisherIsoRig2.js',
               stem:'sportFisherIsoRig2.convertible', label:'53′ Convertible · 16.2 m',
               liner:'CREAM', metal:'STEEL', railZ:0.90 },
    sport90: { sym:'SportFisherIso2', pick:'skybridge', rig:'sportFisherIsoRig2.js',
               rigUrl:'export/sport-fisher-rig-kit/sportFisherIsoRig2.js',
               stem:'sportFisherIsoRig2.skybridge', label:'90′ Skybridge · 27.4 m',
               liner:'CREAM', metal:'STEEL', railZ:0.90 },
    lobvar:  { sym:'LobsterBoatVariantsIso', variantAware:true, rig:'lobsterBoatVariantsIsoRig.js',
               stem:'lobsterBoatVariantsIsoRig', label:'Lobster Variants · ×18',
               liner:'CREAM', metal:'STEEL', railZ:0.78 },
  };
  const DIRL=['N','NE','E','SE','S','SW','W','NW'];
  // fit-out, metres, boat frame (+x stbd, +y bow). Heights above the house sole.
  const LAYOUT = {
    lobster: {
      helm:   { x0:-0.62, x1:0.62, y0:2.95, y1:3.44, h:1.10, wheel:[0,2.90,1.30], seat:[0,2.06] },
      stove:  { x0:-1.30, x1:-0.90, y0:1.15, y1:2.15, h:0.92 },
      locker: { x0: 0.92, x1: 1.32, y0:0.85, y1:1.75, h:1.62 },
      bench:  { x0:-1.32, x1:-0.60, y0:0.66, y1:1.04, h:0.46 },
      hooks:  { side:-1, y:0.86 },
      bunk:   { y0:4.02, y1:5.18, top:0.45 },
    },
    cape: {
      helm:   { x0:-0.58, x1:0.58, y0:1.86, y1:2.34, h:1.10, wheel:[0,1.80,1.50], seat:[0,1.10] },
      stove:  { x0:-1.18, x1:-0.80, y0:0.72, y1:1.58, h:0.92 },
      locker: { x0: 0.80, x1: 1.20, y0:0.70, y1:1.48, h:1.60 },
      bench:  null,
      hooks:  { side:1, y:0.78 },
      bunk:   { y0:3.40, y1:5.30, top:0.45 },
    },
    dragger: {
      helm:   { x0:-0.62, x1:0.62, y0:-5.78, y1:-5.30, h:1.06, wheel:[0,-5.36,5.90], seat:[0,-6.40], level:'bridge' },
      stove:  { x0:-2.24, x1:-1.64, y0:-9.90, y1:-8.90, h:0.92, level:'house' },
      locker: { x0: 1.64, x1: 2.24, y0:-9.70, y1:-8.60, h:1.66, level:'house' },
      bench:  { x0:-2.24, x1:-0.95, y0:-6.55, y1:-6.05, h:0.46, level:'house' },
      bunk:   { x0:-1.60, x1:-0.60, y0:-9.95, y1:-7.95, top:0.50, level:'below' },
      hooks:  { side:-1, y:-5.40, level:'house' },
      stairs: { up:{ x0:1.28, x1:2.02, yBot:-7.90, yTop:-6.55, treads:6 },
                down:{ x0:-1.85, x1:-1.10, yTop:-7.45, yBot:-6.45, treads:4 } },
      furn: [ { kind:'bunk', x0:0.60, x1:1.60, y0:-9.95, y1:-7.95, top:0.50, level:'below' },
              { kind:'table', x0:-1.60, x1:-0.75, y0:-6.00, y1:-5.35, h:0.78, level:'house' },
              { kind:'engine', x0:-0.50, x1:0.80, y0:-10.05, y1:-8.55, h:1.02, level:'below' },
              { kind:'chart', x0:0.95, x1:1.80, y0:-7.75, y1:-7.15, h:0.98, level:'bridge' } ],
    },
    trawler: {
      helm:   { x0:-0.62, x1:0.62, y0:7.86, y1:8.38, h:1.06, wheel:[0,8.32,7.92], seat:[0,7.52], level:'bridge' },
      stove:  { x0:-3.32, x1:-2.68, y0:2.30, y1:3.50, h:0.92, level:'house' },
      locker: { x0: 2.68, x1: 3.32, y0:2.50, y1:3.60, h:1.72, level:'house' },
      bench:  { x0:-3.32, x1:-1.95, y0:7.55, y1:8.05, h:0.46, level:'house' },
      bunk:   { x0:-3.05, x1:-1.95, y0:5.60, y1:7.75, top:0.50, level:'below' },
      hooks:  { side:1, y:2.30, level:'house' },
      stairs: { up:{ x0:2.30, x1:3.05, yBot:6.10, yTop:8.45, treads:7 },
                down:{ x0:-0.45, x1:0.45, yTop:4.25, yBot:2.55, treads:6 } },
      furn: [ { kind:'bunk', x0:1.95, x1:3.05, y0:5.60, y1:7.75, top:0.50, level:'below' },
              { kind:'bunk', x0:-3.05, x1:-1.95, y0:2.60, y1:4.75, top:0.50, level:'below' },
              { kind:'table', x0:-2.95, x1:-2.05, y0:6.35, y1:7.35, h:0.78, level:'house' },
              { kind:'chart', x0:-2.55, x1:-1.60, y0:3.80, y1:4.45, h:0.98, level:'bridge' } ],
    },
  };
  LAYOUT.trawler2 = LAYOUT.trawler;   // the Mk II shares the arrangement; its hull flare comes from ITS loft
  LAYOUT.packet = {
    helm:   { x0:-0.62, x1:0.62, y0:-20.95, y1:-20.45, h:1.06, wheel:[0,-20.50,11.30], seat:[0,-21.70], level:'bridge' },
    stove:  { x0:-3.55, x1:-2.95, y0:-27.60, y1:-26.40, h:0.92, level:'house' },
    locker: { x0: 2.95, x1: 3.55, y0:-27.50, y1:-26.40, h:1.70, level:'house' },
    bench:  { x0:-3.55, x1:-2.20, y0:-22.20, y1:-21.70, h:0.46, level:'house' },
    bunk:   { x0:-3.05, x1:-1.95, y0:-27.70, y1:-25.60, top:0.50, level:'below' },
    hooks:  { side:1, y:-18.90, level:'house' },
    stairs: { up:{ x0:2.30, x1:3.10, yBot:-25.90, yTop:-22.90, treads:10 },
              down:{ x0:-3.30, x1:-2.55, yTop:-24.30, yBot:-25.90, treads:5 } },
    furn: [ { kind:'bunk', x0:1.95, x1:3.05, y0:-27.70, y1:-25.60, top:0.50, level:'below' },
            { kind:'table', x0:-3.30, x1:-2.30, y0:-21.40, y1:-20.50, h:0.78, level:'house' },
            { kind:'engine', x0:-0.70, x1:0.80, y0:-24.80, y1:-22.60, h:1.55, level:'below' },
            { kind:'chart', x0:1.35, x1:2.55, y0:-27.30, y1:-26.60, h:0.98, level:'bridge' } ],
  };
  LAYOUT.tanker = {
    helm:   { x0:-0.62, x1:0.62, y0:-34.85, y1:-34.30, h:1.06, wheel:[0,-34.35,20.90], seat:[0,-35.60], level:'bridge' },
    stove:  { x0:-7.30, x1:-6.65, y0:-47.90, y1:-46.60, h:0.92, level:'house' },
    locker: { x0: 6.65, x1: 7.30, y0:-47.80, y1:-46.60, h:1.75, level:'house' },
    bench:  { x0:-7.30, x1:-5.80, y0:-42.30, y1:-41.80, h:0.46, level:'house' },
    bunk:   { x0:-5.95, x1:-4.75, y0:-46.40, y1:-44.20, top:0.50, level:'below' },
    hooks:  { side:-1, y:-44.90, level:'house' },
    stairs: { up:{ x0:4.85, x1:5.75, yBot:-45.40, yTop:-41.80, treads:12 },
              down:{ x0:-6.10, x1:-5.20, yTop:-43.60, yBot:-45.20, treads:5 } },
    furn: [ { kind:'bunk', x0:4.75, x1:5.95, y0:-46.40, y1:-44.20, top:0.50, level:'below' },
            { kind:'bunk', x0:-5.95, x1:-4.75, y0:-43.60, y1:-41.40, top:0.50, level:'below' },
            { kind:'table', x0:-6.90, x1:-5.70, y0:-41.40, y1:-40.30, h:0.78, level:'house' },
            { kind:'engine', x0:-1.50, x1:1.50, y0:-44.60, y1:-41.00, h:1.90, level:'below' },
            { kind:'chart', x0:-3.20, x1:-1.80, y0:-46.20, y1:-45.50, h:0.98, level:'bridge' } ],
  };
  /* Sport fishers — PRIZE BOATS: luxury fit-out that reads nothing like the workboat cabins.
     Salon: fitted carpet + teak margin, L-settees in cream leather, gloss low table, galley in
     stone + teak (53: run w/ stools; 90: island), fridge column, TV panel, table lamps.
     Below: carpeted staterooms, quilted-leather headboards, flanking side tables + lamps,
     wardrobes, and DRESSED compartments (ensuite heads) as walled abstractions; machinery + forepeak
     stay excluded notes. The open bridge is the EXTRACTOR's; the 53's companionway up CLOSES the
     no-route-up finding. */
  LAYOUT.sport53 = {
    helm:   { x0:-0.62, x1:0.62, y0:0.98, y1:1.44, h:1.06, dz:0.45, wheel:[0,0.93,3.08], seat:[0,0.62], level:'house' },
    helmDeck:{ x0:-1.05, x1:1.05, y0:0.35, y1:1.48, rise:0.45, treads:3, sx0:-0.45, sx1:0.45 },
    stove:  { x0:-1.86, x1:-1.30, y0:-4.90, y1:-3.85, h:0.92, level:'house' },
    locker: { x0: 1.32, x1: 1.86, y0:-4.95, y1:-3.90, h:1.75, level:'house' },
    bunk:   { x0:-0.85, x1:0.85, y0:3.05, y1:4.40, top:0.55, level:'below' },
    stairs: { up:{ x0:0.40, x1:1.10, yBot:-2.70, yTop:-3.70, treads:9 },
              down:{ x0:-1.72, x1:-1.12, yTop:0.60, yBot:1.35, treads:5 } },
    furn: [ { kind:'rug', x0:-0.95, x1:0.95, y0:-2.90, y1:-0.55, level:'house' },
            { kind:'settee', x0:1.22, x1:1.88, y0:-2.95, y1:-0.35, back:'star', level:'house' },
            { kind:'settee', x0:0.10, x1:1.22, y0:-0.95, y1:-0.35, back:'fwd', level:'house' },
            { kind:'table', x0:0.30, x1:1.10, y0:-2.30, y1:-1.20, h:0.42, level:'house' },
            { kind:'fridge', x0:-1.86, x1:-1.42, y0:-3.65, y1:-3.20, h:1.90, level:'house' },
            { kind:'stool', x0:-0.95, x1:-0.63, y0:-4.45, y1:-4.13, level:'house' },
            { kind:'stool', x0:-0.95, x1:-0.63, y0:-3.75, y1:-3.43, level:'house' },
            { kind:'sidetable', x0:-1.82, x1:-1.42, y0:-0.92, y1:-0.52, lamp:true, level:'house' },
            { kind:'tv', side:-1, y0:-2.40, y1:-1.00, z0:2.60, z1:3.50, level:'house' },
            { kind:'bunk', x0:-1.38, x1:-0.48, y0:1.05, y1:3.00, top:0.55, level:'below' },
            { kind:'wardrobe', x0:0.90, x1:1.42, y0:1.30, y1:2.25, h:1.60, level:'below' },
            { kind:'sidetable', x0:-1.30, x1:-0.95, y0:2.58, y1:2.93, lamp:true, level:'below' },
            { kind:'rug', x0:-0.62, x1:0.62, y0:1.20, y1:2.80, level:'below' } ],
    spaces:[ { id:'head_compartment', x0:0.70, x1:1.38, y0:2.40, y1:3.00, h:2.0, level:'below',
               note:'ensuite head — shower + MSD; dressed volume, a wall to routing' } ],
    excluded:{ engine_room:'the machinery space lies AFT of the flat under the raised salon (y < 0.45) — dressed, never walkable; access is the cockpit sole hatches (extractor sole_hatches)',
               rode_locker:'chain/rode stowage forward of y 4.50 in the forepeak — dressed' },
  };
  LAYOUT.sport90 = {
    helm:   { x0:-0.62, x1:0.62, y0:-1.20, y1:-0.70, h:1.06, wheel:[0,-1.25,8.15], seat:[0,-1.95], level:'bridge' },
    stove:  { x0:-2.92, x1:-2.30, y0:-9.55, y1:-8.35, h:0.92, level:'house' },
    locker: { x0: 2.22, x1: 2.94, y0:-9.65, y1:-8.55, h:1.85, level:'house' },
    bunk:   { x0:-1.10, x1:1.10, y0:4.60, y1:6.40, top:0.55, level:'below' },
    stairs: { up:{ x0:0.55, x1:1.35, yBot:-6.55, yTop:-7.55, treads:14 },
              down:{ x0:-1.30, x1:-0.55, yTop:0.30, yBot:1.05, treads:7 } },
    furn: [ { kind:'rug', x0:-1.45, x1:1.45, y0:-6.70, y1:-3.85, level:'house' },
            { kind:'island', x0:-1.40, x1:-0.05, y0:-8.95, y1:-7.95, h:0.95, level:'house' },
            { kind:'stool', x0:0.30, x1:0.62, y0:-8.85, y1:-8.53, level:'house' },
            { kind:'stool', x0:0.30, x1:0.62, y0:-8.35, y1:-8.03, level:'house' },
            { kind:'fridge', x0:-2.92, x1:-2.40, y0:-8.10, y1:-7.50, h:2.00, level:'house' },
            { kind:'settee', x0:2.12, x1:2.94, y0:-6.95, y1:-3.55, back:'star', level:'house' },
            { kind:'settee', x0:0.60, x1:2.12, y0:-4.30, y1:-3.55, back:'fwd', level:'house' },
            { kind:'settee', x0:-2.94, x1:-2.20, y0:-3.30, y1:-0.55, back:'port', level:'house' },
            { kind:'table', x0:1.05, x1:2.00, y0:-6.15, y1:-4.70, h:0.42, level:'house' },
            { kind:'sidetable', x0:-2.18, x1:-1.78, y0:-0.92, y1:-0.52, lamp:true, level:'house' },
            { kind:'tv', side:-1, y0:-7.40, y1:-5.90, z0:4.05, z1:5.05, level:'house' },
            { kind:'settee', x0:-2.18, x1:-1.52, y0:-6.60, y1:-3.60, back:'port', level:'bridge' },
            { kind:'rug', x0:-1.10, x1:1.10, y0:-3.20, y1:-0.95, level:'bridge' },
            { kind:'sidetable', x0:-2.08, x1:-1.70, y0:-3.35, y1:-2.97, lamp:true, level:'bridge' },
            { kind:'bunk', x0:-1.95, x1:-0.75, y0:1.20, y1:3.30, top:0.55, level:'below' },
            { kind:'bunk', x0:0.75, x1:1.95, y0:1.20, y1:3.30, top:0.55, level:'below' },
            { kind:'wardrobe', x0:-2.00, x1:-1.55, y0:3.55, y1:4.30, h:1.65, level:'below' },
            { kind:'wardrobe', x0:1.55, x1:2.00, y0:3.55, y1:4.30, h:1.65, level:'below' },
            { kind:'sidetable', x0:-1.62, x1:-1.24, y0:5.95, y1:6.33, lamp:true, level:'below' },
            { kind:'sidetable', x0:1.24, x1:1.62, y0:5.95, y1:6.33, lamp:true, level:'below' },
            { kind:'rug', x0:-0.85, x1:0.85, y0:1.40, y1:4.30, level:'below' } ],
    spaces:[ { id:'head_master', x0:1.30, x1:2.00, y0:4.75, y1:5.75, h:2.05, level:'below',
               note:'master ensuite — shower + MSD; dressed volume' },
             { id:'head_guest', x0:-2.40, x1:-1.60, y0:0.30, y1:1.05, h:2.05, level:'below',
               note:'guest head — dressed volume' } ],
    excluded:{ engine_room:'machinery space aft of the flat under the salon (y < 0.20) — dressed; access via the cockpit hatches (extractor sole_hatches)',
               rode_locker:'forepeak rode + thruster space forward of y 6.80 — dressed',
               crew_cabin:'crew cabin abaft the engine room stays OUT with the machinery space — prize-boat guest spaces only this pass' },
  };
  /* Lobster variants: ONE parametric arrangement measured off each variant's published house. */
  LAYOUT.lobvar = (L)=>{ const Hh=L.house, yA=Hh.yAft, yF=Hh.yFwd, HL=yF-yA;
    const hx=Hh.hxAt(yA+0.45*HL), k=Math.min(1,hx/1.55), m=Math.max(0.75,Math.min(1.25,HL/3.9));
    const f=(t)=>yA+t*HL;
    return {
      helm:  { x0:-0.60*k, x1:0.60*k, y0:f(0.62), y1:f(0.62)+0.48*m, h:1.06,
               wheel:[0, f(0.62)-0.05, L.DECK+1.28], seat:[0, f(0.62)-0.85*m] },
      stove: { x0:-hx+0.10, x1:-hx+0.66, y0:f(0.16), y1:f(0.16)+0.95*m, h:0.90, level:'house' },
      locker:{ x0:hx-0.62, x1:hx-0.08, y0:f(0.10), y1:f(0.10)+0.85*m, h:1.65, level:'house' },
      bench: { x0:hx-0.55, x1:hx-0.08, y0:f(0.44), y1:f(0.44)+1.10*m, h:0.44, level:'house' },
      bunk:  { y0:Hh.cuddy.y0+0.22, y1:Hh.cuddy.y1-0.12, top:0.42, level:'cuddy' },
      hooks: { side:-1, y:f(0.10) },
    }; };
  /* Ids are append-only and permanent (gameplay brief 2026-08-19); each maps to a SHIPPED mechanism. */
  const ITEMS = {
    helm:   { action:'enter_helm', label:'Helm',   verb:'Take the wheel',   back:'fwd',  level:'house', mech:'the existing helm' },
    stove:  { action:'cook',       label:'Stove',  verb:'Cook · brew up',   back:'port', level:'house', mech:'camper stove pattern (cook)' },
    locker: { action:'storage',    label:'Locker', verb:'Store · retrieve', back:'star', level:'house', mech:'InteriorWardrobe (storage)' },
    bunk:   { action:'sleep',      label:'Bunk',   verb:'Sleep · save',     back:'fwd',  level:'cuddy', mech:'InteriorBed (rest + save)' },
  };
  const LAYERS = ['sole','shell','fitout','props','interact'];

  // ---- shared interior ramps (dark->light) ----
  const SOLEW = ['#3a2c1c','#4a3826','#5c4630','#6e553a','#806445','#927451'];
  const CABIN = ['#54462e','#68583a','#7c6a47','#907c55','#a48e64','#b8a074'];
  const BIRCH = ['#7e7358','#968a69','#ad9f7d','#c3b596','#d7cab0','#e9e0cb'];
  const SHEET = ['#8c928c','#a6aaa2','#bfc2b9','#d5d8cf','#e7e9e0','#f3f4ec'];
  const QUILTL= ['#5a2320','#75302a','#8f3f35','#a85142','#bd6650'];            // madder red (lobster)
  const QUILTC= ['#1e3a44','#2a4e59','#38636e','#4b7a83','#639097'];            // teal (cape + packet)
  const QUILTN= ['#1c2740','#26365a','#324a74','#42608e','#5578a4'];            // navy (tanker)
  const CUSH  = ['#2c3a2e','#3a4c3c','#4a5f4b','#5c735c','#70876e'];
  const BRASS = ['#59410f','#795819','#977326','#b28e3b','#c8a757','#dcc17d'];
  const SLICK = ['#6f5a10','#8f7a16','#b0981f','#cab12b','#e0ca48'];            // oilskins
  const CHART = ['#9a9377','#b3ab8b','#c9c1a0','#dcd4b4','#eae3c8'];
  const GLASSD= ['#7d9ea6','#a1c2c6','#c2dcdd','#d8ebec','#e8f4f4','#f4fafa'];  // looking OUT, day
  const GLASSN= ['#141d2b','#1d2a3d','#2a3c53','#3d5570','#54708c','#6b87a3'];
  const GLOW  = ['#7a5a18','#c09a2c','#efd06a','#fdf0b6'];
  const CAVITY= ['#0d0b0a','#141110','#1b1715','#231d1a','#2b2420','#332b26'];
  // prize-boat (sport fisher) luxury set — reads NOTHING like the workboat cabins: fitted carpet,
  // gloss-teak joinery, stone counters, cream leather, chrome hardware, smooth white overheads
  const CARPET= ['#4a3d33','#5c4d40','#6e5d4d','#806c5a','#927b67','#a48a74'];
  const LEATH = ['#6e5f4c','#8a7a62','#a69378','#c0ac8d','#d6c4a4','#e8d9bd'];
  const TEAKG = ['#2e1d10','#432b16','#5a3a1e','#714a27','#8a5c31','#a4703c'];
  const STONE = ['#8d8d86','#a5a59d','#bcbcb3','#d0d0c7','#e1e1d8','#eeeee6'];
  const WHITEG=['#9aa09c','#b4b9b3','#ccd0c9','#dee1da','#eceee8','#f7f8f3'];
  const RUG   = ['#2c1e2e','#3e2a40','#503752','#624565','#745377'];

  function hex2rgb(h){ return [parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16)]; }
  function rgb2hex(r,g,b){ const q=(n)=>Math.max(0,Math.min(255,Math.round(n))).toString(16).padStart(2,'0'); return '#'+q(r)+q(g)+q(b); }
  function mix(a,b,t){ const A=hex2rgb(a),B=hex2rgb(b); return rgb2hex(A[0]+(B[0]-A[0])*t,A[1]+(B[1]-A[1])*t,A[2]+(B[2]-A[2])*t); }
  function desat(hex,t){ const [r,g,b]=hex2rgb(hex); const l=0.3*r+0.59*g+0.11*b; return rgb2hex(r+(l-r)*t,g+(l-g)*t,b+(l-b)*t); }
  function hash2(a,b){ let h=(a*374761393+b*668265263)>>>0; h=(h^(h>>13))*1274126177>>>0; return ((h^(h>>16))>>>0)/4294967296; }
  function mulberry32(a){return function(){a|=0;a=a+0x6D2B79F5|0;let t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296;};}

  function hullEnv(key, vnt){
    const meta=HULLS[key]; if(!meta) return null;
    let E=root[meta.sym]; if(!E) return null;
    if(meta.pick && E.byId) E=E.byId(meta.pick);
    if(meta.variantAware && E.interiorEnv) E=E.interiorEnv(vnt||null);
    if(!E || !E.loft) return null;
    const lay=(typeof LAYOUT[key]==='function') ? LAYOUT[key](E.loft) : LAYOUT[key];
    return { key, meta, E, L:E.loft, H:E.loft.house, lay };
  }
  function list(){ return Object.keys(HULLS).filter(k=>hullEnv(k)); }
  function cellOf(hull){ const env=hullEnv(hull); return env ? env.L.cell : null; }

  function levelsOf(hull){ const env=hullEnv(hull); if(!env) return ['house'];
    if(env.H.kind==='ship'||env.H.kind==='sport') return env.H.levels ? env.H.levels.slice() : ['bridge','house','below'];
    return env.H.cuddy ? ['house','cuddy'] : ['house']; }
  function itemLevel(hull, id){ const env=hullEnv(hull); if(!env) return ITEMS[id]?ITEMS[id].level:'house';
    const e=env.lay[id]; return (e&&e.level)||(ITEMS[id]?ITEMS[id].level:'house'); }
  function soleZof(env, level){ const Hh=env.H;
    if(Hh.kind==='ship'||Hh.kind==='sport') return Hh.decks[level].soleZ;
    return level==='cuddy' ? Hh.cuddy.soleZ : Hh.soleZ; }

  function resolve(opts){
    opts=opts||{}; const g=(k,d)=>opts[k]!=null?opts[k]:d;
    const hull = HULLS[opts.hull] ? opts.hull : 'lobster';
    const lv = levelsOf(hull);
    return { hull, level: lv.indexOf(opts.level)>=0 ? opts.level : (lv.indexOf('house')>=0?'house':lv[0]),
      doorOpen: Math.max(0,Math.min(1,g('doorOpen',0))),
      night:!!opts.night, lamp:g('lamp',!!opts.night), weather:g('weather',0.30),
      focus:g('focus',null), clutter:g('clutter',true),
      layers:opts.layers||null, outline:opts.outline!=null?!!opts.outline:false, variant:opts.variant||null };
  }
  function dims(opts){ const s=resolve(opts), env=hullEnv(s.hull, s.variant); if(!env) return null;
    const Hh=env.H;
    if(Hh.kind==='ship'||Hh.kind==='sport') return { hull:s.hull, label:env.meta.label, cell:env.L.cell, levels:levelsOf(s.hull) };
    return { hull:s.hull, label:env.meta.label, cell:env.L.cell,
      soleZ:Hh.soleZ, headroom:+(Hh.eaveZ-Hh.soleZ).toFixed(2),
      houseLen:+(Hh.yFwd-Hh.yAft).toFixed(2), cuddy:!!Hh.cuddy }; }

  // ---- camera (fleet basis, incl. rock params) ----
  function camBasis(opts, env){
    const dir=opts.dir||0, th=dir*Math.PI/4;
    const e=(opts.elev!=null?opts.elev:env.E.defaultElev)*DEG;
    const roll=(opts.roll||0)*DEG, pitch=(opts.pitch||0)*DEG;
    return { dir, ct:Math.cos(th), stt:Math.sin(th), se:Math.sin(e), ce:Math.cos(e),
      cr:Math.cos(roll), sr:Math.sin(roll), cq:Math.cos(pitch), sq:Math.sin(pitch), heave:(opts.heave||0) };
  }
  function projVert(x,y,z,B,C){
    const x1=x*B.cr+z*B.sr, z1=-x*B.sr+z*B.cr;
    const y2=y*B.cq - z1*B.sq, z2=y*B.sq + z1*B.cq;
    const xr=x1*B.ct - y2*B.stt, yr=x1*B.stt + y2*B.ct, zr=z2;
    return { xr,yr,zr, sx:C.cx+xr*C.S, sy:C.cy-(yr*B.se+zr*B.ce)*C.S - B.heave, d:(yr*B.ce-zr*B.se) };
  }
  const hFace=(n,B)=> n[0]*B.stt + n[1]*B.ct;
  const cullWall=(n,B)=> hFace(n,B) < -0.12;

  // ---- faces ----
  let LAYER='base', IACT=null;
  function F(v,mat,b,db,uv,tex,flat){ return { v,mat,b:b||0,db:db==null?0.03:db,uv:uv||null,tex:tex||null,flat:!!flat,layer:LAYER,iact:IACT }; }
  function quad(out,a,b,c,d,mat,bi,db,uv,tex){ out.push(F([a,b,c,d],mat,bi,db,uv,tex)); }
  function slab(out,pts,z,mat,b,tex){ out.push(F(pts.map(p=>[p[0],p[1],z]),mat,b||0,0.02,tex?pts.map(p=>[p[0],p[1]]):null,tex)); }
  function wallQ(out,x0,y0,x1,y1,z0,z1,mat,tex,b,db){ const Lm=Math.hypot(x1-x0,y1-y0);
    out.push(F([[x0,y0,z0],[x1,y1,z0],[x1,y1,z1],[x0,y0,z1]],mat,b||0,db,[[0,z0],[Lm,z0],[Lm,z1],[0,z1]],tex)); }
  function box(out,x0,x1,y0,y1,z0,z1,mat,b,tex,noTop){ b=b||0;
    wallQ(out,x0,y0,x1,y0,z0,z1,mat,tex,b-0.30); wallQ(out,x1,y1,x0,y1,z0,z1,mat,tex,b+0.10);
    wallQ(out,x1,y0,x1,y1,z0,z1,mat,tex,b+0.18); wallQ(out,x0,y1,x0,y0,z0,z1,mat,tex,b-0.42);
    if(!noTop) slab(out,[[x0,y0],[x1,y0],[x1,y1],[x0,y1]],z1,mat,b+0.34,tex); }
  const sub=(a,b)=>[a[0]-b[0],a[1]-b[1],a[2]-b[2]];
  const crs=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
  const nrm=(v)=>{ const m=Math.hypot(v[0],v[1],v[2])||1; return [v[0]/m,v[1]/m,v[2]/m]; };
  function tube(out,p0,p1,r,n,mat,b,caps){ const u=nrm(sub(p1,p0)); const a=Math.abs(u[2])>0.9?[1,0,0]:[0,0,1];
    const e1=nrm(crs(a,u)), e2=nrm(crs(u,e1)); n=n||8; b=b||0;
    const P=(i,end)=>{ const th=(i/n)*Math.PI*2,c=Math.cos(th)*r,s=Math.sin(th)*r,q=end?p1:p0;
      return [q[0]+e1[0]*c+e2[0]*s,q[1]+e1[1]*c+e2[1]*s,q[2]+e1[2]*c+e2[2]*s]; };
    for(let i=0;i<n;i++){ const j=(i+1)%n; out.push(F([P(i,0),P(j,0),P(j,1),P(i,1)],mat,b)); }
    if(caps!==false){ const t=[],bt=[]; for(let i=0;i<n;i++){ t.push(P(i,1)); bt.push(P(n-1-i,0)); }
      out.push(F(t,mat,b+0.26)); out.push(F(bt,mat,b-0.5)); } }

  // ---- textures ----
  const plankTex=(p)=>{ p=p||0.16; return (u,v)=>{ const f=((v%p)+p)%p; if(f<0.022) return -2;
    return hash2(Math.floor(v/p)|0, Math.floor(u*3.1)|0)<0.5?0:-1; }; };
  const boardTex=(p)=>{ p=p||0.34; return (u,v)=>{ const f=((u%p)+p)%p; return f<0.026?-1:0; }; };
  const quiltTex=()=>{ const c=0.20; return (u,v)=>{ const fu=((u%c)+c)%c, fv=((v%c)+c)%c;
    return (fu<0.030||fv<0.030)?-1:(hash2(Math.floor(u/c)|0,Math.floor(v/c)|0)<0.5?0:1); }; };

  // ---- generic wall with rectangular cuts (windows / door / opening) ----
  // map(u,zv)->[x,y,z]; outward = OUTWARD normal in plan; cuts carry their own mats.
  function wallGrid(out, spec, B, s){
    const { u0,u1,z0,z1,map,outward,railZ }=spec;
    const cuts=(spec.cuts||[]).filter(c=>c.u1>u0+0.01 && c.u0<u1-0.01);
    const inner=[-outward[0],-outward[1],-outward[2]];
    const us=[u0,u1], zs=[z0,z1];
    for(const c of cuts){ us.push(Math.max(u0,c.u0),Math.min(u1,c.u1)); zs.push(Math.max(z0,c.z0),Math.min(z1,c.z1)); }
    if(railZ>z0+0.05 && railZ<z1-0.05) zs.push(railZ);
    const U=[...new Set(us.map(v=>+v.toFixed(4)))].sort((a,b)=>a-b);
    const Z=[...new Set(zs.map(v=>+v.toFixed(4)))].sort((a,b)=>a-b);
    const put=(ua,ub,za,zb,mat,b,db,noTex)=>{
      let P=[map(ua,za),map(ub,za),map(ub,zb),map(ua,zb)];
      const n=crs(sub(P[1],P[0]),sub(P[3],P[0]));
      if(n[0]*inner[0]+n[1]*inner[1]+n[2]*inner[2] < 0) P=P.slice().reverse();
      const useTex=(!noTex && spec.tex) ? spec.tex : null;
      out.push(F(P,mat,b||0,db==null?0.04:db,useTex?[[ua,za],[ub,za],[ub,zb],[ua,zb]]:null,useTex)); };
    for(let i=0;i+1<U.length;i++) for(let j=0;j+1<Z.length;j++){
      const ua=U[i],ub=U[i+1],za=Z[j],zb=Z[j+1], um=(ua+ub)/2, zm=(za+zb)/2;
      const c=cuts.find(c=>um>c.u0&&um<c.u1&&zm>c.z0&&zm<c.z1);
      if(c) continue;
      const mat = zm<spec.railZ ? (spec.railMat||'wains') : (spec.mat||'liner');
      put(ua,ub,za,zb,mat, spec.b||0);
    }
    // cuts drawn as frame ring + fill (glass/daylight/cavity), slightly proud of the wall plane
    for(const c of cuts){
      if(c.mat==='skip') continue;
      const e=0.055, pu=(v)=>Math.max(u0,Math.min(u1,v));
      const ring=[[c.u0,c.u1,c.z0,c.z0+e],[c.u0,c.u1,c.z1-e,c.z1],[c.u0,c.u0+e,c.z0+e,c.z1-e],[c.u1-e,c.u1,c.z0+e,c.z1-e]];
      if(!c.noFrame) for(const r of ring) put(pu(r[0]),pu(r[1]),r[2],r[3],c.frameMat||'frame',0.20,0.05,true);
      const fu0=c.noFrame?c.u0:c.u0+e, fu1=c.noFrame?c.u1:c.u1-e, fz0=c.noFrame?c.z0:c.z0+e, fz1=c.noFrame?c.z1:c.z1-e;
      put(pu(fu0),pu(fu1),fz0,fz1, c.mat, c.b!=null?c.b:0.55, 0.06, true);
    }
  }

  // ---- HOUSE level ----
  function buildHouse(out, s, env, B){
    const Hh=env.H, lay=env.lay, sole=Hh.soleZ, eave=Hh.eaveZ, D=Hh.door;
    const yA=Hh.yAft+WT, yF=Hh.yFwd-0.015, hx=(y)=>Hh.hxAt(y)-WT;
    const railZ=sole+ (env.meta.railZ||0.78);
    LAYER='sole';
    const NS=10, pts=[]; for(let i=0;i<=NS;i++){ const y=yA+(yF-yA)*i/NS; pts.push([hx(y),y]); }
    slab(out, pts.concat(pts.slice().reverse().map(p=>[-p[0],p[1]])), sole, 'sole', -0.10, plankTex(0.15));
    LAYER='shell';
    const cutLip=[];
    // side walls (ruled planes at +-hx(y)); windows from HOUSE.sideGlass
    for(const side of [-1,1]){
      const outward=[side,0,0];
      const cuts=(Hh.sideGlass.runs||[]).map(r=>({u0:Math.min(r[0],r[1]),u1:Math.max(r[0],r[1]),z0:Hh.sideGlass.z0,z1:Hh.sideGlass.z1,mat:s.night?'glassN':'daylight'}));
      if(cullWall(outward,B)){ cutLip.push({side}); continue; }
      wallGrid(out,{ u0:yA,u1:yF, z0:sole,z1:eave, railZ, mat:'liner', railMat:'wains',
        tex:boardTex(0.38), map:(y,z)=>[side*hx(y),y,z], outward, cuts, b: side<0?0.16:-0.55 }, B, s);
      // roof lip on the surviving wall
      const lipPts=[]; for(let i=0;i<=NS;i++){ const y=yA+(yF-yA)*i/NS; lipPts.push([side*hx(y),y]); }
      for(let i=0;i<NS;i++){ const a=lipPts[i], b2=lipPts[i+1];
        quad(out,[a[0],a[1],eave],[b2[0],b2[1],eave],[b2[0]-side*0.18,b2[1],eave],[a[0]-side*0.18,a[1],eave],'ceil',-1.6,0.02); }
    }
    // aft wall: door opening (real) + aft light; inner leaf face rides doorOpen
    (function(){
      const outward=[0,-1,0], yw=Hh.yAft+WT;
      if(cullWall(outward,B)){ cutLip.push({aft:true}); }
      else {
        const cuts=[ {u0:D.x0,u1:D.x1,z0:D.z0-0.02,z1:D.z1,mat:'skip'},
          {u0:Math.min(Hh.aftGlass.x0,Hh.aftGlass.x1),u1:Math.max(Hh.aftGlass.x0,Hh.aftGlass.x1),z0:Hh.aftGlass.z0,z1:Hh.aftGlass.z1,mat:s.night?'glassN':'daylight'} ];
        wallGrid(out,{ u0:-hx(Hh.yAft+0.01),u1:hx(Hh.yAft+0.01), z0:sole,z1:eave, railZ, mat:'liner', railMat:'wains',
          tex:boardTex(0.38), map:(x,z)=>[x,yw,z], outward, cuts, b:-0.28 }, B, s);
        // doorway jamb ring + what the opening shows: the daylight of the cockpit, or the leaf's inner face
        for(const r of [[D.x0-0.05,D.x0,D.z0,D.z1],[D.x1,D.x1+0.05,D.z0,D.z1],[D.x0-0.05,D.x1+0.05,D.z1,D.z1+0.05]])
          quad(out,[r[0],yw,r[2]],[r[1],yw,r[2]],[r[1],yw,r[3]],[r[0],yw,r[3]],'frame',0.24,0.05);
        const t=s.doorOpen, sft=t*D.travel, lx0=D.leaf.x0+sft, lx1=D.leaf.x1+sft;
        const cov0=Math.max(D.x0,lx0), cov1=Math.min(D.x1,lx1);
        if(cov1>cov0+0.02){ // inner face of the sliding leaf, seen just outside the opening
          quad(out,[cov0,yw+0.015,D.z0],[cov1,yw+0.015,D.z0],[cov1,yw+0.015,D.z1],[cov0,yw+0.015,D.z1],'leafIn',-0.35,0.05);
          const wx0=Math.max(cov0+0.10,D.x0+0.06), wx1=Math.min(cov1-0.10,D.x1-0.06), wz1=Math.min(2.26,D.z1-0.10);
          if(wx1>wx0+0.06) quad(out,[wx0,yw+0.008,1.96],[wx1,yw+0.008,1.96],[wx1,yw+0.008,wz1],[wx0,yw+0.008,wz1], s.night?'glassN':'daylight',0.35,0.06);
        }
        if(cov0>D.x0+0.02) quad(out,[D.x0,yw+0.01,D.z0],[cov0,yw+0.01,D.z0],[cov0,yw+0.01,D.z1],[D.x0,yw+0.01,D.z1], s.night?'glassN':'daylight',0.6,0.05);
        if(cov1<D.x1-0.02) quad(out,[cov1,yw+0.01,D.z0],[D.x1,yw+0.01,D.z0],[D.x1,yw+0.01,D.z1],[cov1,yw+0.01,D.z1], s.night?'glassN':'daylight',0.6,0.05);
      }
    })();
    // front: reclined windscreen band + bulkhead below (lobster) or full raked wall (cape)
    (function(){
      const Fr=Hh.front, outward=[0,1,0];
      if(cullWall(outward,B)){ cutLip.push({front:true}); return; }
      // ONE plane law for both fronts: y(z) runs yBot@soleZ -> yTop@eaveZ (matches the exterior's
      // yFront(z) exactly); zLo is just where the wall's lower edge stops.
      const yAtZ=(z)=> Fr.yBot + (Fr.yTop-Fr.yBot)*Math.max(0,Math.min(1,(z-sole)/(eave-sole)));
      const glass=Fr.glass, cuts=glass.panes.map(p=>({u0:p[0],u1:p[1],z0:glass.z0,z1:glass.z1,mat:s.night?'glassN':'daylight',b:0.45}));
      const zLo = Fr.kind==='rake' ? sole : Fr.zBot;
      if(Hh.cuddy && Fr.kind==='rake') cuts.push({u0:Hh.cuddy.opening.x0,u1:Hh.cuddy.opening.x1,z0:sole,z1:Hh.cuddy.opening.z1,mat:'cavity',b:-0.5,frameMat:'cab'});
      wallGrid(out,{ u0:-hx(Math.min(Fr.yBot,yF))+0.02,u1:hx(Math.min(Fr.yBot,yF))-0.02, z0:zLo,z1:eave, railZ:zLo+0.01, mat:'liner',
        tex:boardTex(0.38), map:(x,z)=>[x,yAtZ(z)-0.02,z], outward, cuts, b:0.05 }, B, s);
      if(Fr.kind!=='rake'){
        // the V bulkhead below the screen, with the cuddy opening cut in
        const bcuts = Hh.cuddy ? [{u0:Hh.cuddy.opening.x0,u1:Hh.cuddy.opening.x1,z0:sole,z1:Hh.cuddy.opening.z1,mat:'cavity',b:-0.5,frameMat:'cab'}] : [];
        wallGrid(out,{ u0:-hx(yF)+0.02,u1:hx(yF)-0.02, z0:sole,z1:Fr.zBot, railZ:railZ, mat:'liner', railMat:'wains',
          tex:boardTex(0.38), map:(x,z)=>[x,yF,z], outward, cuts:bcuts, b:0.2 }, B, s);
        // dash shelf bridging the screen base to the bulkhead — chart lives here
        const yd0=yAtZ(zLo)-0.02, yd1=yF-0.01, hw=hx(yd1)-0.06;
        LAYER='fitout';
        slab(out,[[-hw,yd0],[hw,yd0],[hw,yd1],[-hw,yd1]],zLo,'work',0.30);
        if(s.clutter) tube(out,[-0.55,(yd0+yd1)/2,zLo+0.045],[0.15,(yd0+yd1)/2,zLo+0.045],0.04,6,'chart',0.35);
        LAYER='shell';
      }
    })();
    // section lip where near walls were culled
    for(const c of cutLip){
      if(c.side!=null){ const NSg=8; for(let i=0;i<NSg;i++){ const y0=yA+(yF-yA)*i/NSg, y1=yA+(yF-yA)*(i+1)/NSg;
        quad(out,[c.side*hx(y0),y0,sole+0.02],[c.side*hx(y1),y1,sole+0.02],[c.side*hx(y1),y1,sole+0.17],[c.side*hx(y0),y0,sole+0.17],'cut',0.20,0.05); } }
      if(c.aft){ const w=hx(Hh.yAft+0.01), yw=Hh.yAft+WT, D2=Hh.door;
        for(const r of [[-w,D2.x0],[D2.x1,w]])
          quad(out,[r[0],yw,sole+0.02],[r[1],yw,sole+0.02],[r[1],yw,sole+0.17],[r[0],yw,sole+0.17],'cut',0.20,0.05); }
      if(c.front){ const w=hx(Math.min(Hh.front.yBot,yF))-0.02, yw=Math.min(Hh.front.yBot,yF)-0.02;
        const o=Hh.cuddy?Hh.cuddy.opening:null;
        const rr = (o && Hh.front.kind==='rake') ? [[-w,o.x0],[o.x1,w]] : [[-w,w]];
        for(const r of rr) quad(out,[r[0],yw,sole+0.02],[r[1],yw,sole+0.02],[r[1],yw,sole+0.17],[r[0],yw,sole+0.17],'cut',0.48,0.05); }
    }
    // the companionway treads live on the CUDDY level (they sit beyond this bulkhead); drawing them
    // here left floating steps whenever the front wall was culled — the cavity opening carries the read.
    buildFitout(out, s, env, B);
    buildProps(out, s, env, B);
  }

  function buildFitout(out, s, env, B){
    const Hh=env.H, lay=env.lay, sole=soleZof(env, s.level), lvl=(e,d)=>((e&&e.level)||d);
    LAYER='interact';
    // HELM: console + sloped instrument top + wheel + pedestal seat
    if(lay.helm && lvl(lay.helm,'house')===s.level){ const h=lay.helm, base=sole+(h.dz||0), top=base+h.h; IACT='helm';
      box(out,h.x0,h.x1,h.y0,h.y1,base,top-0.16,'cab',0,null,true);
      quad(out,[h.x0,h.y0,top-0.16],[h.x1,h.y0,top-0.16],[h.x1,h.y1-0.10,top+0.06],[h.x0,h.y1-0.10,top+0.06],'cab',0.30,0.03);
      quad(out,[h.x0+0.08,h.y0+0.02,top-0.15],[h.x1-0.08,h.y0+0.02,top-0.15],[h.x1-0.08,h.y1-0.14,top+0.045],[h.x0+0.08,h.y1-0.14,top+0.045],'panel',-0.6,0.05);
      for(const gx of [-0.30,0.06]) quad(out,[gx,h.y0+0.05,top-0.10],[gx+0.20,h.y0+0.05,top-0.10],[gx+0.20,h.y1-0.18,top+0.02],[gx,h.y1-0.18,top+0.02], s.night?'screen':'glass',s.night?0.9:0.2,0.07);
      const w=h.wheel, wr=0.30, tl=0.22*DEG*0; // wheel: rim + 4 spokes, raked slightly aft
      const ring=[]; const NW=10;
      for(let i=0;i<NW;i++){ const a0=(i/NW)*Math.PI*2, a1=((i+1)/NW)*Math.PI*2;
        tube(out,[w[0]+Math.cos(a0)*wr, w[1]-0.14+Math.sin(a0)*0.06, w[2]+Math.sin(a0)*wr],
                 [w[0]+Math.cos(a1)*wr, w[1]-0.14+Math.sin(a1)*0.06, w[2]+Math.sin(a1)*wr],0.028,4,'wheel',0.15,false); }
      for(const a of [0,Math.PI/2]) tube(out,[w[0]+Math.cos(a)*wr,w[1]-0.14+Math.sin(a)*0.06,w[2]+Math.sin(a)*wr],
        [w[0]-Math.cos(a)*wr,w[1]-0.14-Math.sin(a)*0.06,w[2]-Math.sin(a)*wr],0.020,4,'wheel',0.05,false);
      tube(out,[w[0],w[1]-0.02,w[2]],[w[0],w[1]-0.16,w[2]],0.045,6,'iron',-0.1);
      const sp=lay.helm.seat, sbase=sole+(lay.helm.dz||0);
      tube(out,[sp[0],sp[1],sbase],[sp[0],sp[1],sbase+0.52],0.045,6,'iron',-0.2);
      box(out,sp[0]-0.22,sp[0]+0.22,sp[1]-0.20,sp[1]+0.20,sbase+0.52,sbase+0.62,'cush',0.25,quiltTex(),false);
      box(out,sp[0]-0.22,sp[0]+0.22,sp[1]-0.26,sp[1]-0.18,sbase+0.62,sbase+1.02,'cush',0.05,quiltTex(),false);
      IACT=null; }
    // STOVE: counter + rings + kettle + fiddle rail
    if(lay.stove && lvl(lay.stove,'house')===s.level){ const g=lay.stove, top=sole+g.h; IACT='stove';
      box(out,g.x0,g.x1,g.y0,g.y1,sole,top,'cab',0,null,true);
      slab(out,[[g.x0,g.y0],[g.x1,g.y0],[g.x1,g.y1],[g.x0,g.y1]],top,'work',0.40);
      const sx=(g.x0+g.x1)/2, by=g.y0+(g.y1-g.y0)*0.30;
      box(out,sx-0.16,sx+0.16,by-0.15,by+0.15,top,top+0.03,'steel',0.10,null,false);
      for(const dy of [-0.075,0.075]) tube(out,[sx,by+dy,top+0.03],[sx,by+dy,top+0.048],0.052,8,'iron',0.2);
      if(s.clutter){ const ky=g.y1-(g.y1-g.y0)*0.24;
        tube(out,[sx,ky,top],[sx,ky,top+0.15],0.075,8,'steel',0.15);
        tube(out,[sx,ky,top+0.15],[sx,ky,top+0.185],0.04,6,'steel',0.3); }
      for(const yy of [g.y0,g.y1]) wallQ(out,g.x0,yy,g.x1,yy,top+0.02,top+0.06,'brass',null,0.4,0.05);
      wallQ(out,g.x1,g.y0,g.x1,g.y1,top+0.02,top+0.06,'brass',null,0.4,0.05);
      IACT=null; }
    // LOCKER: tall cabinet, twin doors ajar-shut, brass pulls, drawer under
    if(lay.locker && lvl(lay.locker,'house')===s.level){ const k=lay.locker, top=sole+k.h; IACT='locker';
      box(out,k.x0,k.x1,k.y0,k.y1,sole,top,'cab',0,null,false);
      const fx=(k.x0+k.x1)/2<0?k.x1:k.x0, fn=(k.x0+k.x1)/2<0?1:-1, ym=(k.y0+k.y1)/2;
      for(const [ya,yb2] of [[k.y0+0.03,ym-0.01],[ym+0.01,k.y1-0.03]])
        { let P=[[fx+fn*0.012,ya,sole+0.34],[fx+fn*0.012,yb2,sole+0.34],[fx+fn*0.012,yb2,top-0.06],[fx+fn*0.012,ya,top-0.06]];
          const n=crs(sub(P[1],P[0]),sub(P[3],P[0])); if(n[0]*fn<0) P=P.reverse();
          out.push(F(P,'cab',-1.3,0.05)); }
      for(const gy of [ym-0.06,ym+0.06]) tube(out,[fx+fn*0.035,gy,sole+(k.h*0.62)],[fx+fn*0.035,gy,sole+(k.h*0.62)+0.11],0.016,5,'brass',0.5,false);
      { let P=[[fx+fn*0.012,k.y0+0.04,sole+0.08],[fx+fn*0.012,k.y1-0.04,sole+0.08],[fx+fn*0.012,k.y1-0.04,sole+0.28],[fx+fn*0.012,k.y0+0.04,sole+0.28]];
        const n=crs(sub(P[1],P[0]),sub(P[3],P[0])); if(n[0]*fn<0) P=P.reverse();
        out.push(F(P,'cab',-1.6,0.05));
        tube(out,[fx+fn*0.035,ym-0.05,sole+0.18],[fx+fn*0.035,ym+0.05,sole+0.18],0.015,5,'brass',0.45,false); }
      IACT=null; }
    LAYER='fitout';
    if(lay.bench && lvl(lay.bench,'house')===s.level){ const b=lay.bench, top=sole+b.h;
      box(out,b.x0,b.x1,b.y0,b.y1,sole,top,'cab',0,null,true);
      slab(out,[[b.x0,b.y0],[b.x1,b.y0],[b.x1,b.y1],[b.x0,b.y1]],top,'cush',0.30,quiltTex()); }
  }

  function buildProps(out, s, env, B){
    const Hh=env.H, lay=env.lay, sole=Hh.soleZ; LAYER='props';
    // oilskins on hooks by the door (the wall must survive the cut to show them)
    if(lay.hooks){ const hk=lay.hooks, x=hk.side*(Hh.hxAt(hk.y)-WT-0.02);
      if(!cullWall([hk.side,0,0],B)){
        for(const dy of [-0.14,0.10]){
          tube(out,[x,hk.y+dy,sole+1.62],[x-hk.side*0.06,hk.y+dy,sole+1.66],0.014,4,'brass',0.4,false);
          quad(out,[x-hk.side*0.02,hk.y+dy-0.12,sole+1.60],[x-hk.side*0.06,hk.y+dy+0.12,sole+1.60],
                   [x-hk.side*0.10,hk.y+dy+0.10,sole+0.72],[x-hk.side*0.06,hk.y+dy-0.10,sole+0.72],'slick',dy<0?0.2:-0.1,0.05); } } }
    // VHF box under the eave by the helm
    if(lay.helm){ const h=lay.helm, rz=Hh.eaveZ-0.30, ry=h.y1-0.02;
      box(out,-0.42,-0.12,ry-0.10,ry+0.06,rz,rz+0.18,'panel',-0.3,null,false);
      tube(out,[-0.27,ry-0.11,rz+0.06],[-0.27,ry-0.16,rz+0.02],0.012,4,'iron',0.2,false);
      if(s.night) quad(out,[-0.38,ry-0.105,rz+0.05],[-0.30,ry-0.105,rz+0.05],[-0.30,ry-0.105,rz+0.11],[-0.38,ry-0.105,rz+0.11],'screen',0.9,0.06); }
    // enamel mug on the helm console
    if(s.clutter && lay.helm){ const h=lay.helm, mx=h.x1-0.16, my=h.y0+0.10, mz=sole+h.h-0.16+0.02;
      tube(out,[mx,my,mz],[mx,my,mz+0.10],0.045,7,'linen',0.3); }
    // lamp on the ceiling centreline when lit
    if(s.lamp){ const yc=(Hh.yAft+Hh.yFwd)/2, cz=Hh.eaveZ-0.05;
      tube(out,[0,yc,cz],[0,yc,cz-0.12],0.012,4,'iron',0.2);
      tube(out,[0,yc,cz-0.12],[0,yc,cz-0.26],0.10,8,s.night?'flame':'steel',s.night?0.85:0.25); }
  }

  // ---- CUDDY level ----
  function buildCuddy(out, s, env, B){
    const Hh=env.H, L=env.L, C=Hh.cuddy; if(!C) return;
    const lay=env.lay, sole=C.soleZ, y0=C.y0, y1=C.y1;
    const half=(y,z)=>Math.max(0.06, L.halfAtZ(y,z)-0.12);
    LAYER='sole';
    const NS=9, pts=[]; for(let i=0;i<=NS;i++){ const y=y0+(y1-y0)*i/NS; pts.push([half(y,sole+0.05),y]); }
    slab(out, pts.concat(pts.slice().reverse().map(p=>[-p[0],p[1]])), sole, 'sole', -0.15, plankTex(0.15));
    LAYER='shell';
    // hull-side liner, lofted between stations, culled per facing; ceiling stays open (the cut)
    const NSg=8, topAt=(y)=>L.sheerZ(y)-0.16;
    for(const side of [-1,1]){
      const culled=cullWall([side,0,0],B);
      for(let i=0;i<NSg;i++){
        const ya=y0+(y1-y0)*i/NSg, yb=y0+(y1-y0)*(i+1)/NSg;
        if(culled){
          quad(out,[side*half(ya,sole+0.10),ya,sole+0.02],[side*half(yb,sole+0.10),yb,sole+0.02],
                   [side*half(yb,sole+0.10),yb,sole+0.16],[side*half(ya,sole+0.10),ya,sole+0.16],'cut',0.48,0.05);
          continue; }
        for(let k=0;k<3;k++){
          const za=sole+(topAt(ya)-sole)*k/3, zb=sole+(topAt(ya)-sole)*(k+1)/3;
          const za2=sole+(topAt(yb)-sole)*k/3, zb2=sole+(topAt(yb)-sole)*(k+1)/3;
          let P=[[side*half(ya,za),ya,za],[side*half(yb,za2),yb,za2],[side*half(yb,zb2),yb,zb2],[side*half(ya,zb),ya,zb]];
          const n=crs(sub(P[1],P[0]),sub(P[3],P[0])); if(n[0]*side>0) P=P.reverse();
          out.push(F(P,'ceil',side<0?0.10:-0.5,0.04,[[ya,za],[yb,za2],[yb,zb2],[ya,zb]],boardTex(0.30)));
        }
        // far-side lip of the foredeck underside
        quad(out,[side*half(ya,topAt(ya)),ya,topAt(ya)],[side*half(yb,topAt(yb)),yb,topAt(yb)],
                 [side*(half(yb,topAt(yb))-0.22),yb,topAt(yb)],[side*(half(ya,topAt(ya))-0.22),ya,topAt(ya)],'ceil',-0.85,0.02);
      }
    }
    // aft bulkhead (to the house): opening with treads climbing up
    (function(){
      const o=C.opening, outward=[0,-1,0], yw=y0+0.02;
      if(cullWall(outward,B)){
        for(const r of [[-half(y0,sole+0.3),o.x0],[o.x1,half(y0,sole+0.3)]])
          quad(out,[r[0],yw,sole+0.02],[r[1],yw,sole+0.02],[r[1],yw,sole+0.16],[r[0],yw,sole+0.16],'cut',0.48,0.05);
        return; }
      const w=half(y0,sole+0.8);
      wallGrid(out,{ u0:-w,u1:w, z0:sole,z1:topAt(y0)-0.02, railZ:sole+0.01, mat:'liner',
        tex:boardTex(0.38), map:(x,z)=>[x,yw,z], outward,
        cuts:[{u0:o.x0,u1:o.x1,z0:sole,z1:o.z1, mat:'cavityWarm', b:-0.2, frameMat:'cab'}], b:0.12 }, B, s);
      const st=C.step;
      for(let i=1;i<=st.treads;i++){ const z=sole+(Hh.soleZ-sole)*i/(st.treads+1)+0.01, ty=y0+0.05+(st.treads-i)*0.02;
        slab(out,[[o.x0+0.05,ty],[o.x1-0.05,ty],[o.x1-0.05,ty+0.22],[o.x0+0.05,ty+0.22]],z,'sole',-0.30,plankTex(0.2)); }
    })();
    // V-BERTH: platform tapering with the hull, quilt, two pillows aft (the wide end)
    const bt=lay.bunk; if(bt){
      LAYER='interact'; IACT='bunk';
      const top=sole+bt.top, NB=6, bw=(y)=>Math.max(0.10,half(y,top)-0.06);
      for(let i=0;i<NB;i++){ const ya=bt.y0+(bt.y1-bt.y0)*i/NB, yb=bt.y0+(bt.y1-bt.y0)*(i+1)/NB;
        quad(out,[-bw(ya),ya,top],[bw(ya),ya,top],[bw(yb),yb,top],[-bw(yb),yb,top], i<2?'linen':'quilt', 0.24, 0.03, [[ -bw(ya),ya],[bw(ya),ya],[bw(yb),yb],[-bw(yb),yb]], i<2?null:quiltTex()); }
      quad(out,[-bw(bt.y0),bt.y0,sole+0.10],[bw(bt.y0),bt.y0,sole+0.10],[bw(bt.y0),bt.y0,top],[-bw(bt.y0),bt.y0,top],'cab',-0.6,0.03);
      const pw=bw(bt.y0+0.18)*0.44;
      for(const k of [-1,1]) box(out,k*pw-pw*0.42,k*pw+pw*0.42,bt.y0+0.06,bt.y0+0.34,top,top+0.12,'linen',0.30,null,false);
      IACT=null;
    }
    // side shelf + lantern
    LAYER='props';
    const shy0=y0+0.25, shy1=bt?bt.y0-0.12:y1-0.6;
    if(shy1>shy0+0.3){ const side=-1, z=sole+0.78;
      if(!cullWall([side,0,0],B)){
        const NP=4; for(let i=0;i<NP;i++){ const ya=shy0+(shy1-shy0)*i/NP, yb=shy0+(shy1-shy0)*(i+1)/NP;
          quad(out,[side*half(ya,z),ya,z],[side*half(yb,z),yb,z],[side*(half(yb,z)-0.26),yb,z],[side*(half(ya,z)-0.26),ya,z],'cab',0.2,0.03); }
        wallQ(out,side*(half(shy0,z)-0.26),shy0,side*(half(shy1,z)-0.26),shy1,z,z+0.045,'cab',null,0.25,0.04);
        if(s.clutter){ const my=(shy0+shy1)/2;
          tube(out,[side*(half(my,z)-0.14),my,z],[side*(half(my,z)-0.14),my,z+0.12],0.045,6,'steel',0.2); } } }
    if(s.lamp||s.night){ const ly=(y0+y1)/2, lz=topAt? (L.sheerZ(ly)-0.34) : sole+1.3;
      tube(out,[0,ly,L.sheerZ(ly)-0.20],[0,ly,L.sheerZ(ly)-0.32],0.012,4,'iron',0.2);
      tube(out,[0,ly,L.sheerZ(ly)-0.32],[0,ly,L.sheerZ(ly)-0.46],0.085,8,s.night?'flame':'steel',s.night?0.85:0.25); }
  }

  // ---- SHIP levels (dragger + trawlers): rectangular deckhouse rooms + lofted below-deck flat ----
  function shipWallLips(out, spans, y, sole){
    for(const r of spans) quad(out,[r[0],y,sole+0.02],[r[1],y,sole+0.02],[r[1],y,sole+0.17],[r[0],y,sole+0.17],'cut',0.20,0.05);
  }
  function shipStairFlight(out, stq, z0, z1, n){
    const dir=stq.yTop>stq.yBot?1:-1, yA=stq.yBot!=null?stq.yBot:stq.yTop, span=Math.abs((stq.yTop!=null?stq.yTop:0)-(stq.yBot!=null?stq.yBot:0));
    for(let i=1;i<=n;i++){ const z=z0+(z1-z0)*i/(n+1);
      const ty=(stq.yBot!=null?stq.yBot:stq.yTop)+((stq.yTop!=null?stq.yTop:0)-(stq.yBot!=null?stq.yBot:0))*(i-1)/n;
      const a=Math.min(ty,ty+0.24*Math.sign((stq.yTop||0)-(stq.yBot||0))), b2=Math.max(ty,ty+0.24*Math.sign((stq.yTop||0)-(stq.yBot||0)));
      slab(out,[[stq.x0+0.04,a],[stq.x1-0.04,a],[stq.x1-0.04,b2],[stq.x0+0.04,b2]],z,'sole',-0.25,plankTex(0.2)); }
    for(const xx of [stq.x0+0.02,stq.x1-0.02]){
      const ya=(stq.yBot!=null?stq.yBot:stq.yTop), yb=(stq.yTop!=null?stq.yTop:stq.yBot);
      out.push(F([[xx,ya,z0+0.02],[xx,yb,z1+0.02],[xx,yb,z1+0.24],[xx,ya,z0+0.24]],'cab',-0.5,0.04));
    }
  }
  function shipOpening(out, o, soleZ){
    slab(out,[[o.x0,Math.min(o.ya,o.yb)],[o.x1,Math.min(o.ya,o.yb)],[o.x1,Math.max(o.ya,o.yb)],[o.x0,Math.max(o.ya,o.yb)]],soleZ+0.015,'cavityWarm',-0.3);
    for(const seg of [[[o.x0,o.ya],[o.x1,o.ya]],[[o.x0,o.yb],[o.x1,o.yb]],[[o.x0,o.ya],[o.x0,o.yb]],[[o.x1,o.ya],[o.x1,o.yb]]])
      wallQ(out,seg[0][0],seg[0][1],seg[1][0],seg[1][1],soleZ+0.015,soleZ+0.10,'cab',null,0.15,0.04);
  }
  function buildShip(out, s, env, B){
    const Hh=env.H, lay=env.lay;
    if(s.level==='below') return buildShipBelow(out,s,env,B);
    const Dk=Hh.decks[s.level], sole=Dk.soleZ, ceil=Dk.ceilZ, D=Hh.door;
    const hxAt=(z)=>(Dk.hxAt?Dk.hxAt(z):Dk.hx)-WT;
    const lux=(Hh.kind==='sport');
    const isBridge=(s.level==='bridge'), Fr=Dk.front;
    const yAtZ = Fr ? ((z)=> Fr.yBot + (Fr.yTop-Fr.yBot)*Math.max(0,Math.min(1,(z-sole)/(ceil-sole)))) : null;
    const y0=Dk.y0+WT, y1=(Fr? Math.min(Fr.yBot,Fr.yTop) : Dk.y1)-WT;
    const railZ=sole+0.85;
    LAYER='sole';
    if(lux){
      // fitted carpet with a gloss-teak margin — nothing like the workboats' plank soles
      slab(out,[[-hxAt(sole+1),y0],[hxAt(sole+1),y0],[hxAt(sole+1),y1],[-hxAt(sole+1),y1]],sole,'sole',-0.05);
      const hxs=hxAt(sole+1), bw=0.22;
      for(const seg of [[[-hxs,y0],[hxs,y0],[hxs,y0+bw],[-hxs,y0+bw]],[[-hxs,y1-bw],[hxs,y1-bw],[hxs,y1],[-hxs,y1]],
                        [[-hxs,y0],[-hxs+bw,y0],[-hxs+bw,y1],[-hxs,y1]],[[hxs-bw,y0],[hxs,y0],[hxs,y1],[hxs-bw,y1]]])
        slab(out,seg,sole+0.004,'wains',-0.30,plankTex(0.12));
    } else slab(out,[[-hxAt(sole+1),y0],[hxAt(sole+1),y0],[hxAt(sole+1),y1],[-hxAt(sole+1),y1]],sole,'sole',-0.10,plankTex(0.16));
    // raised helm deck against the windshield (53): platform + a short flight down into the lounge
    if(!isBridge && lay.helmDeck){ const hd=lay.helmDeck, hz=sole+hd.rise;
      LAYER='fitout';
      box(out,hd.x0,hd.x1,hd.y0,hd.y1,sole,hz-0.012,'cab',-0.20,null,true);
      slab(out,[[hd.x0,hd.y0],[hd.x1,hd.y0],[hd.x1,hd.y1],[hd.x0,hd.y1]],hz,'sole',0.02);
      slab(out,[[hd.x0,hd.y0],[hd.x1,hd.y0],[hd.x1,hd.y0+0.10],[hd.x0,hd.y0+0.10]],hz+0.004,'wains',-0.15);
      const sx0=(hd.sx0!=null?hd.sx0:-0.45), sx1=(hd.sx1!=null?hd.sx1:0.45);
      for(let i=1;i<=hd.treads;i++){ const z=sole+hd.rise*(hd.treads+1-i)/(hd.treads+1), ty=hd.y0-0.26*i+0.02;
        slab(out,[[sx0,ty],[sx1,ty],[sx1,ty+0.24],[sx0,ty+0.24]],z,'wains',-0.10); }
      LAYER='shell'; }
    LAYER='shell';
    // side walls
    for(const side of [-1,1]){
      const outward=[side,0,0];
      const cuts=[];
      if(!isBridge && Dk.portholes) for(const py of Dk.portholes.ys) cuts.push({u0:py-0.22,u1:py+0.22,z0:Dk.portholes.z0,z1:Dk.portholes.z1,mat:s.night?'glassN':'daylight',frameMat:'frame'});
      if(isBridge && Dk.sideGlass) for(const r of Dk.sideGlass.runs) cuts.push({u0:Math.min(r[0],r[1]),u1:Math.max(r[0],r[1]),z0:Dk.sideGlass.z0,z1:Dk.sideGlass.z1,mat:s.night?'glassN':'daylight'});
      if(cullWall(outward,B)){
        const xw=side*hxAt(sole+0.5);
        quad(out,[xw,y0,sole+0.02],[xw,y1,sole+0.02],[xw,y1,sole+0.17],[xw,y0,sole+0.17],'cut',0.20,0.05);
        continue; }
      wallGrid(out,{ u0:y0,u1:y1, z0:sole,z1:ceil, railZ, mat:'liner', railMat:'wains', tex:lux?null:boardTex(0.38),
        map:(y,z)=>[side*hxAt(z),y,z], outward, cuts, b: side<0?0.16:-0.55 }, B, s);
      quad(out,[side*hxAt(ceil),y0,ceil],[side*hxAt(ceil),y1,ceil],[side*(hxAt(ceil)-0.20),y1,ceil],[side*(hxAt(ceil)-0.20),y0,ceil],'ceil',-1.6,0.02);
    }
    // end walls: y0 (aft) and y1/raked front
    for(const end of [{y:y0-0.0, outward:[0,-1,0], isDoor:((!isBridge && D.face==='aft')||(isBridge && !!Hh.door2 && !Dk.external)), glass:(isBridge?Dk.aftGlass:null), ports:(!isBridge&&Dk.aftPorts)?Dk.aftPorts:null, raked:false},
                      {y:y1, outward:[0,1,0], isDoor:(!isBridge && D.face==='fwd'), glass:(isBridge&&Fr?Dk.frontGlass:null), ports:null, raked:!!(isBridge&&Fr)}]){
      const w=hxAt(sole+1.2);
      if(cullWall(end.outward,B)){
        const spans = end.isDoor ? [[-w,D.x0],[D.x1,w]] : [[-w,w]];
        shipWallLips(out, spans, end.y, sole);
        continue; }
      const cuts=[];
      const DD=(isBridge && Hh.door2) ? Hh.door2 : D;
      if(end.isDoor) cuts.push({u0:DD.x0,u1:DD.x1,z0:DD.z0-0.02,z1:DD.z1,mat:'skip'});
      if(end.glass) for(const p of (end.glass.panes||[])) cuts.push({u0:p[0],u1:p[1],z0:end.glass.z0,z1:end.glass.z1,mat:s.night?'glassN':'daylight',b:0.55});
      if(end.ports) for(const px of end.ports.xs) cuts.push({u0:px-0.22,u1:px+0.22,z0:end.ports.z0,z1:end.ports.z1,mat:s.night?'glassN':'daylight',frameMat:'frame'});
      const map = end.raked ? ((x,z)=>[x, yAtZ(z)-0.02, z]) : ((x,z)=>[x, end.y, z]);
      wallGrid(out,{ u0:-w+0.02,u1:w-0.02, z0:sole,z1:ceil, railZ:(isBridge?sole+0.01:railZ), mat:'liner', railMat:'wains',
        tex:lux?null:boardTex(0.38), map, outward:end.outward, cuts, b: end.outward[1]>0?0.05:-0.28 }, B, s);
      if(end.isDoor){
        const DR=DD;
        for(const r of [[DR.x0-0.05,DR.x0],[DR.x1,DR.x1+0.05]])
          quad(out,[r[0],end.y,DR.z0],[r[1],end.y,DR.z0],[r[1],end.y,DR.z1],[r[0],end.y,DR.z1],'frame',0.24,0.05);
        quad(out,[DR.x0-0.05,end.y,DR.z1],[DR.x1+0.05,end.y,DR.z1],[DR.x1+0.05,end.y,DR.z1+0.05],[DR.x0-0.05,end.y,DR.z1+0.05],'frame',0.24,0.05);
        const t=s.doorOpen, yl=end.y+(end.outward[1]>0?0.02:-0.02)*-1;
        if(DR.kind==='slide'){
          // glass slider seen from inside: leaf rides +x; daylight where it has left the opening
          const sft=t*DR.travel, c0=Math.max(DR.x0,DR.leaf.x0+sft), c1=Math.min(DR.x1,DR.leaf.x1+sft);
          if(c1>c0+0.02) quad(out,[c0,yl,DR.z0],[c1,yl,DR.z0],[c1,yl,DR.z1],[c0,yl,DR.z1],'glass',-0.25,0.05);
          if(c0>DR.x0+0.02) quad(out,[DR.x0,yl,DR.z0],[c0,yl,DR.z0],[c0,yl,DR.z1],[DR.x0,yl,DR.z1], s.night?'glassN':'daylight',0.6,0.05);
          if(c1<DR.x1-0.02) quad(out,[c1,yl,DR.z0],[DR.x1,yl,DR.z0],[DR.x1,yl,DR.z1],[c1,yl,DR.z1], s.night?'glassN':'daylight',0.6,0.05);
        } else {
          // hinged leaf seen from inside: covers (1-t) of the opening from the hinge side; daylight beyond
          const wcov=(1-t)*(D.x1-D.x0);
          const hingePort=(D.hinge==='port');
          const c0=hingePort?D.x0:D.x1-wcov, c1=hingePort?D.x0+wcov:D.x1;
          if(wcov>0.03) quad(out,[c0,yl,D.z0],[c1,yl,D.z0],[c1,yl,D.z1],[c0,yl,D.z1],'leafIn',-0.35,0.05);
          if(wcov<(D.x1-D.x0)-0.03){ const d0=hingePort?c1:D.x0, d1=hingePort?D.x1:c0;
            quad(out,[d0,yl,D.z0],[d1,yl,D.z0],[d1,yl,D.z1],[d0,yl,D.z1], s.night?'glassN':'daylight',0.6,0.05); }
        }
      }
    }
    // stairs on this level
    const stq=lay.stairs;
    if(stq){
      const zH=Hh.decks.house.soleZ, zB=Hh.decks.bridge.soleZ, zL=Hh.decks.below.soleZ;
      LAYER='fitout';
      if(s.level==='house'){
        if(stq.up) shipStairFlight(out, stq.up, zH, zB-0.35, stq.up.treads);
        if(stq.down){ shipOpening(out,{x0:stq.down.x0,x1:stq.down.x1,ya:stq.down.yTop,yb:stq.down.yBot}, zH);
          for(let i=1;i<=2;i++){ const z=zH-(zH-zL)*i/(stq.down.treads+1);
            const ty=stq.down.yTop+(stq.down.yBot-stq.down.yTop)*(i-1)/stq.down.treads;
            const a=Math.min(ty,ty+0.22*Math.sign(stq.down.yBot-stq.down.yTop)), b2=Math.max(ty,ty+0.22*Math.sign(stq.down.yBot-stq.down.yTop));
            slab(out,[[stq.down.x0+0.05,a],[stq.down.x1-0.05,a],[stq.down.x1-0.05,b2],[stq.down.x0+0.05,b2]],z,'sole',-0.45,plankTex(0.2)); } }
      }
      if(s.level==='bridge' && stq.up){
        shipOpening(out,{x0:stq.up.x0,x1:stq.up.x1,ya:stq.up.yTop-0.9,yb:stq.up.yTop+0.15}, zB);
        tubeS(out,[stq.up.x0+0.02,stq.up.yTop-0.9,zB+0.02],[stq.up.x0+0.02,stq.up.yTop-0.9,zB+0.85],0.025,'steel',0.2);
        tubeS(out,[stq.up.x1-0.02,stq.up.yTop-0.9,zB+0.02],[stq.up.x1-0.02,stq.up.yTop-0.9,zB+0.85],0.025,'steel',0.2);
      }
    }
    buildFitout(out, s, env, B);
    // props: slickers by the door + lamp + table mug
    LAYER='props';
    if(lay.hooks && (lay.hooks.level||'house')===s.level){ const hk=lay.hooks, x=hk.side*(hxAt(sole+1.6)-0.02);
      if(!cullWall([hk.side,0,0],B)) for(const dy of [-0.14,0.10]){
        tubeS(out,[x,hk.y+dy,sole+1.62],[x-hk.side*0.06,hk.y+dy,sole+1.66],0.014,'brass',0.4);
        quad(out,[x-hk.side*0.02,hk.y+dy-0.12,sole+1.60],[x-hk.side*0.06,hk.y+dy+0.12,sole+1.60],
                 [x-hk.side*0.10,hk.y+dy+0.10,sole+0.72],[x-hk.side*0.06,hk.y+dy-0.10,sole+0.72],'slick',dy<0?0.2:-0.1,0.05); } }
    for(const fu of (lay.furn||[])){ if(fu.level!==s.level||fu.kind==='bunk') continue;
      if(fu.kind==='table'){ box(out,fu.x0,fu.x1,fu.y0,fu.y1,sole+fu.h-0.06,sole+fu.h,'cab',0.25,null,false);
        box(out,(fu.x0+fu.x1)/2-0.05,(fu.x0+fu.x1)/2+0.05,(fu.y0+fu.y1)/2-0.05,(fu.y0+fu.y1)/2+0.05,sole,sole+fu.h-0.06,'cab',-0.3,null,true);
        if(s.clutter) tubeS(out,[(fu.x0+fu.x1)/2-0.14,(fu.y0+fu.y1)/2,sole+fu.h],[(fu.x0+fu.x1)/2-0.14,(fu.y0+fu.y1)/2,sole+fu.h+0.10],0.045,'linen',0.3); }
      if(fu.kind==='chart'){ box(out,fu.x0,fu.x1,fu.y0,fu.y1,sole,sole+fu.h,'cab',0.1,null,false);
        if(s.clutter) tubeS(out,[fu.x0+0.10,(fu.y0+fu.y1)/2,sole+fu.h+0.035],[fu.x1-0.10,(fu.y0+fu.y1)/2,sole+fu.h+0.035],0.035,'chart',0.35); }
      if(fu.kind==='engine'){ box(out,fu.x0,fu.x1,fu.y0,fu.y1,sole,sole+fu.h,'panel',-0.2,null,false);
        tubeS(out,[(fu.x0+fu.x1)/2,fu.y0+0.2,sole+fu.h],[(fu.x0+fu.x1)/2,fu.y0+0.2,ceil-0.05],0.07,'iron',-0.1); }
      if(fu.kind==='rug'){ slab(out,[[fu.x0,fu.y0],[fu.x1,fu.y0],[fu.x1,fu.y1],[fu.x0,fu.y1]],sole+0.006,'rug',0.10,quiltTex()); }
      if(fu.kind==='settee'){ const bk=fu.back||'aft', t2=0.16;
        box(out,fu.x0,fu.x1,fu.y0,fu.y1,sole,sole+0.24,'cab',-0.10,null,true);
        box(out,fu.x0+0.02,fu.x1-0.02,fu.y0+0.02,fu.y1-0.02,sole+0.24,sole+0.40,'cush',0.10,quiltTex(),false);
        const bb= bk==='star'?{x0:fu.x1-t2,x1:fu.x1,y0:fu.y0,y1:fu.y1}
               : bk==='port'?{x0:fu.x0,x1:fu.x0+t2,y0:fu.y0,y1:fu.y1}
               : bk==='fwd' ?{x0:fu.x0,x1:fu.x1,y0:fu.y1-t2,y1:fu.y1}
               :             {x0:fu.x0,x1:fu.x1,y0:fu.y0,y1:fu.y0+t2};
        box(out,bb.x0,bb.x1,bb.y0,bb.y1,sole+0.40,sole+0.88,'cush',0.22,quiltTex(),false); }
      if(fu.kind==='island'){ const th=fu.h||0.95;
        box(out,fu.x0,fu.x1,fu.y0,fu.y1,sole+0.06,sole+th-0.04,'cab',0.05,null,true);
        slab(out,[[fu.x0-0.06,fu.y0-0.06],[fu.x1+0.06,fu.y0-0.06],[fu.x1+0.06,fu.y1+0.06],[fu.x0-0.06,fu.y1+0.06]],sole+th,'work',0.42);
        if(s.clutter) tubeS(out,[(fu.x0+fu.x1)/2,fu.y1-0.20,sole+th],[(fu.x0+fu.x1)/2,fu.y1-0.20,sole+th+0.12],0.05,'steel',0.25); }
      if(fu.kind==='fridge'){ box(out,fu.x0,fu.x1,fu.y0,fu.y1,sole,sole+(fu.h||1.95),'panel',0.10,null,false);
        const fx=(fu.x0+fu.x1)/2<0?fu.x1:fu.x0, fn2=(fu.x0+fu.x1)/2<0?1:-1;
        tubeS(out,[fx+fn2*0.03,fu.y0+0.10,sole+0.55],[fx+fn2*0.03,fu.y0+0.10,sole+1.45],0.018,'steel',0.45); }
      if(fu.kind==='stool'){ const cx2=(fu.x0+fu.x1)/2, cy2=(fu.y0+fu.y1)/2, r2=(fu.x1-fu.x0)/2;
        tubeS(out,[cx2,cy2,sole],[cx2,cy2,sole+0.58],0.045,'steel',0.10);
        box(out,cx2-r2,cx2+r2,cy2-r2,cy2+r2,sole+0.58,sole+0.70,'cush',0.25,quiltTex(),false); }
      if(fu.kind==='sidetable'){ box(out,fu.x0,fu.x1,fu.y0,fu.y1,sole,sole+0.52,'cab',0.12,null,false);
        if(fu.lamp){ const cx2=(fu.x0+fu.x1)/2, cy2=(fu.y0+fu.y1)/2;
          tubeS(out,[cx2,cy2,sole+0.52],[cx2,cy2,sole+0.72],0.014,'steel',0.2);
          tubeS(out,[cx2,cy2,sole+0.72],[cx2,cy2,sole+0.86],0.075,s.night?'flame':'linen',s.night?0.85:0.35); } }
      if(fu.kind==='tv'){
        if(fu.face==='fwd'){ if(!cullWall([0,1,0],B))
          quad(out,[fu.x0,y1-0.03,fu.z0],[fu.x1,y1-0.03,fu.z0],[fu.x1,y1-0.03,fu.z1],[fu.x0,y1-0.03,fu.z1], s.night?'screen':'panel', s.night?0.9:-1.2, 0.05);
        } else { const sd=fu.side||1;
          if(!cullWall([sd,0,0],B)){ const xw=sd*(hxAt((fu.z0+fu.z1)/2)-0.10);
            quad(out,[xw,fu.y0,fu.z0],[xw,fu.y1,fu.z0],[xw,fu.y1,fu.z1],[xw,fu.y0,fu.z1], s.night?'screen':'panel', s.night?0.9:-1.2, 0.05); } } }
    }
    if(s.lamp){ const yc=(y0+y1)/2, cz=ceil-0.05;
      tubeS(out,[0,yc,cz],[0,yc,cz-0.12],0.012,'iron',0.2);
      tubeS(out,[0,yc,cz-0.12],[0,yc,cz-0.26],0.10,s.night?'flame':'steel',s.night?0.85:0.25); }
  }
  function buildShipBelow(out, s, env, B){
    const Hh=env.H, L=env.L, lay=env.lay, Dk=Hh.decks.below;
    const sole=Dk.soleZ, ceilZ=Dk.ceilZ, y0=Dk.y0, y1=Dk.y1;
    const lux=(Hh.kind==='sport');
    const cap=(Dk.hxCap!=null)?Dk.hxCap:1e9;
    const half=(y,z)=>Math.max(0.4, Math.min(cap, L.halfAtZ(y,z)-0.28));
    LAYER='sole';
    const NS=9, pts=[]; for(let i=0;i<=NS;i++){ const y=y0+(y1-y0)*i/NS; pts.push([half(y,sole+0.05),y]); }
    slab(out, pts.concat(pts.slice().reverse().map(p=>[-p[0],p[1]])), sole, 'sole', lux?-0.05:-0.15, lux?null:plankTex(0.15));
    LAYER='shell';
    const NSg=8;
    for(const side of [-1,1]){
      const culled=cullWall([side,0,0],B);
      for(let i=0;i<NSg;i++){
        const ya=y0+(y1-y0)*i/NSg, yb=y0+(y1-y0)*(i+1)/NSg;
        if(culled){ quad(out,[side*half(ya,sole+0.10),ya,sole+0.02],[side*half(yb,sole+0.10),yb,sole+0.02],
                     [side*half(yb,sole+0.10),yb,sole+0.16],[side*half(ya,sole+0.10),ya,sole+0.16],'cut',0.20,0.05); continue; }
        for(let k=0;k<3;k++){
          const za=sole+(ceilZ-sole)*k/3, zb=sole+(ceilZ-sole)*(k+1)/3;
          let P=[[side*half(ya,za),ya,za],[side*half(yb,za),yb,za],[side*half(yb,zb),yb,zb],[side*half(ya,zb),ya,zb]];
          const n=crs(sub(P[1],P[0]),sub(P[3],P[0])); if(n[0]*side>0) P=P.reverse();
          out.push(F(P,'ceil',side<0?0.10:-0.5,0.04,[[ya,za],[yb,za],[yb,zb],[ya,zb]],boardTex(0.30)));
        }
        quad(out,[side*half(ya,ceilZ),ya,ceilZ],[side*half(yb,ceilZ),yb,ceilZ],
                 [side*(half(yb,ceilZ)-0.22),yb,ceilZ],[side*(half(ya,ceilZ)-0.22),ya,ceilZ],'ceil',-0.85,0.02);
      }
    }
    for(const end of [{y:y0+0.02,outward:[0,-1,0]},{y:y1-0.02,outward:[0,1,0]}]){
      const w=half(end.y,sole+1.0);
      if(cullWall(end.outward,B)){ shipWallLips(out,[[-w,w]],end.y,sole); continue; }
      wallGrid(out,{ u0:-w,u1:w, z0:sole,z1:ceilZ-0.02, railZ:sole+0.01, mat:'liner', tex:lux?null:boardTex(0.38),
        map:(x,z)=>[x,end.y,z], outward:end.outward, cuts:[], b:-0.15 }, B, s);
    }
    // bunks: the interactable + plain siblings
    const drawBunk=(bt, interact)=>{
      if(interact){ LAYER='interact'; IACT='bunk'; } else LAYER='fitout';
      const top=sole+(bt.top||0.5);
      box(out,bt.x0,bt.x1,bt.y0,bt.y1,sole,top-0.10,'cab',-0.2,null,true);
      slab(out,[[bt.x0,bt.y0],[bt.x1,bt.y0],[bt.x1,bt.y1],[bt.x0,bt.y1]],top,'quilt',0.24,quiltTex());
      const hy = Math.abs(bt.y0-((Hh.decks.below.y0+Hh.decks.below.y1)/2)) > Math.abs(bt.y1-((Hh.decks.below.y0+Hh.decks.below.y1)/2)) ? bt.y0 : bt.y1;
      const py0=hy===bt.y0?bt.y0+0.06:bt.y1-0.34, py1=py0+0.28;
      box(out,(bt.x0+bt.x1)/2-0.28,(bt.x0+bt.x1)/2+0.28,py0,py1,top,top+0.11,'linen',0.30,null,false);
      if(Hh.kind==='sport'){ // quilted leather headboard + a throw across the foot
        const hb= hy===bt.y0 ? {y0:bt.y0-0.09,y1:bt.y0} : {y0:bt.y1,y1:bt.y1+0.09};
        box(out,bt.x0+0.04,bt.x1-0.04,hb.y0,hb.y1,top,top+0.58,'cush',0.20,quiltTex(),false);
        const fy0= hy===bt.y0 ? bt.y1-0.44 : bt.y0+0.10;
        slab(out,[[bt.x0+0.02,fy0],[bt.x1-0.02,fy0],[bt.x1-0.02,fy0+0.34],[bt.x0+0.02,fy0+0.34]],top+0.012,'rug',0.22,quiltTex());
      }
      IACT=null; };
    if(lay.bunk && (lay.bunk.level||'cuddy')==='below') drawBunk(lay.bunk,true);
    for(const fu of (lay.furn||[])) if(fu.kind==='bunk'&&fu.level==='below') drawBunk(fu,false);
    for(const fu of (lay.furn||[])) if(fu.kind==='engine'&&fu.level==='below'){
      LAYER='fitout'; box(out,fu.x0,fu.x1,fu.y0,fu.y1,sole,sole+fu.h,'panel',-0.25,null,false);
      tubeS(out,[(fu.x0+fu.x1)/2,fu.y0+0.25,sole+fu.h],[(fu.x0+fu.x1)/2,fu.y0+0.25,ceilZ-0.03],0.07,'iron',-0.1); }
    LAYER='fitout';
    for(const fu of (lay.furn||[])){ if(fu.level!=='below') continue;
      if(fu.kind==='rug') slab(out,[[fu.x0,fu.y0],[fu.x1,fu.y0],[fu.x1,fu.y1],[fu.x0,fu.y1]],sole+0.006,'rug',0.10,quiltTex());
      if(fu.kind==='wardrobe'){ box(out,fu.x0,fu.x1,fu.y0,fu.y1,sole,sole+(fu.h||1.6),'cab',0.10,null,false);
        const fx=(fu.x0+fu.x1)/2<0?fu.x1:fu.x0, fn2=(fu.x0+fu.x1)/2<0?1:-1, ym=(fu.y0+fu.y1)/2;
        for(const gy of [ym-0.05,ym+0.05]) tubeS(out,[fx+fn2*0.03,gy,sole+0.85],[fx+fn2*0.03,gy,sole+0.97],0.014,'brass',0.45); }
      if(fu.kind==='sidetable'){ box(out,fu.x0,fu.x1,fu.y0,fu.y1,sole,sole+0.50,'cab',0.12,null,false);
        if(fu.lamp){ const cx2=(fu.x0+fu.x1)/2, cy2=(fu.y0+fu.y1)/2;
          tubeS(out,[cx2,cy2,sole+0.50],[cx2,cy2,sole+0.68],0.014,'steel',0.2);
          tubeS(out,[cx2,cy2,sole+0.68],[cx2,cy2,sole+0.82],0.070,s.night?'flame':'linen',s.night?0.85:0.35); } }
    }
    // dressed compartments (ensuite heads etc): smooth partitions with a gloss-teak door + chrome pull
    for(const sp of (lay.spaces||[])){ if(sp.level!=='below') continue;
      const hW=Math.min(ceilZ-sole-0.18, sp.h||2.0);
      box(out,sp.x0,sp.x1,sp.y0,sp.y1,sole,sole+hW,'liner',-0.12,null,false);
      const inb=(sp.x0+sp.x1)/2<0?sp.x1:sp.x0, dn=(sp.x0+sp.x1)/2<0?1:-1, ym=(sp.y0+sp.y1)/2;
      let P=[[inb+dn*0.012,ym-0.30,sole+0.02],[inb+dn*0.012,ym+0.30,sole+0.02],[inb+dn*0.012,ym+0.30,sole+Math.min(hW-0.10,1.85)],[inb+dn*0.012,ym-0.30,sole+Math.min(hW-0.10,1.85)]];
      const n2=crs(sub(P[1],P[0]),sub(P[3],P[0])); if(n2[0]*dn<0) P=P.reverse();
      out.push(F(P,'cab',-0.35,0.05));
      tubeS(out,[inb+dn*0.035,ym+0.20,sole+0.92],[inb+dn*0.035,ym+0.20,sole+1.04],0.015,'brass',0.5);
    }
    // the companionway down lands here: flight climbing back to the house
    const stq=lay.stairs;
    if(stq&&stq.down){ LAYER='fitout';
      shipStairFlight(out,{x0:stq.down.x0,x1:stq.down.x1,yBot:stq.down.yBot,yTop:stq.down.yTop}, sole, Hh.decks.house.soleZ-0.30, stq.down.treads); }
    LAYER='props';
    if(s.lamp||s.night){ const ly=(y0+y1)/2;
      tubeS(out,[0,ly,ceilZ-0.02],[0,ly,ceilZ-0.14],0.012,'iron',0.2);
      tubeS(out,[0,ly,ceilZ-0.14],[0,ly,ceilZ-0.28],0.085,s.night?'flame':'steel',s.night?0.85:0.25); }
  }
  function tubeS(out,a,b2,r,mat,b){ tube(out,a,b2,r,6,mat,b); }

  function build(s, env, B){
    const out=[];
    if(env.H.kind==='ship'||env.H.kind==='sport') buildShip(out,s,env,B);
    else if(s.level==='cuddy') buildCuddy(out,s,env,B); else buildHouse(out,s,env,B);
    LAYER='base'; IACT=null;
    return out;
  }

  function makeMats(s, env){
    const E=env.E, wx=s.weather, night=s.night;
    const grime=r=>r.map(c=>mix(desat(c,wx*0.20),'#3a3128',wx*0.10));
    const warm =r=>night?r.map(c=>mix(c,'#e0a848',0.20)):r;
    const t=r=>warm(grime(r));
    const liner=E[env.meta.liner]||(env.meta.liner==='CREAM'?SHEET:BIRCH), metal=E[env.meta.metal]||E.STEEL||E.MOTO||SHEET;
    const lux=(s.hull==='sport53'||s.hull==='sport90');
    const MM = {
      liner:{ ramp:t(liner) }, wains:{ ramp:t(CABIN) }, sole:{ ramp:t(SOLEW) },
      cab:{ ramp:t(CABIN) }, work:{ ramp:t(CABIN.map(c=>mix(c,'#d3c29a',0.30))) },
      ceil:{ ramp:t(BIRCH.map(c=>mix(c,'#8a7f66',0.18))) },
      cut:{ ramp:warm((liner).slice(Math.max(0,liner.length-4))) },
      frame:{ ramp:t((metal||CABIN).map(c=>mix(c,'#c9cfd1',0.18))) },
      panel:{ ramp:t(E.IRON||CAVITY) }, iron:{ ramp:t(E.IRON||CAVITY) },
      steel:{ ramp:t(metal) }, brass:{ ramp:t(BRASS) }, wheel:{ ramp:t(CABIN.map(c=>mix(c,'#7a4a22',0.35))) },
      cush:{ ramp:t(CUSH) }, linen:{ ramp:warm(SHEET) },
      quilt:{ ramp:t(s.hull==='cape'||s.hull==='packet'?QUILTC:((s.hull==='tanker'||s.hull==='sport53'||s.hull==='sport90')?QUILTN:QUILTL)) },
      slick:{ ramp:t(SLICK) }, chart:{ ramp:warm(CHART) },
      leafIn:{ ramp:t(liner.map(c=>mix(c,'#4a4a40',0.30))) },
      cavity:{ ramp:CAVITY }, cavityWarm:{ ramp:CAVITY.map(c=>mix(c,'#7a5a2a',0.25)) },
      glass:{ ramp:night?GLASSN:GLASSD }, daylight:{ ramp:night?GLASSN:GLASSD }, glassN:{ ramp:GLASSN },
      screen:{ ramp:GLOW }, flame:{ ramp:GLOW },
    };
    if(lux) Object.assign(MM, {
      liner:{ ramp:t(WHITEG) }, ceil:{ ramp:t(WHITEG) }, sole:{ ramp:t(CARPET) },
      wains:{ ramp:t(TEAKG) }, cab:{ ramp:t(TEAKG) }, work:{ ramp:t(STONE) },
      cush:{ ramp:t(LEATH) }, quilt:{ ramp:t(LEATH) }, linen:{ ramp:warm(SHEET) },
      rug:{ ramp:t(RUG) }, wheel:{ ramp:t(TEAKG) }, brass:{ ramp:t(E.STEEL||STONE) },
      cut:{ ramp:warm(WHITEG.slice(2)) } });
    return MM;
  }

  // ---- rasteriser (fleet recipe + focus lift; per-hull cell + shade constants) ----
  function paint(faces, B, MATS, s, env, layerSet){
    const C=env.L.cell, SH=env.L.shade, Wp=C.W, Hp=C.H, N=Wp*Hp;
    const zbuf=new Float32Array(N).fill(Infinity), dep=new Float32Array(N);
    const rbuf=new Array(N).fill(null), ibuf=new Int16Array(N), nbuf=new Array(N).fill(null), abuf=new Array(N).fill(null);
    const LN=SH.LN, GAIN=SH.GAIN, BIAS=SH.BIAS, BAYER=SH.BAYER;
    const shadeOf=(n)=> n[0]*LN[0] + (n[1]*B.se+n[2]*B.ce)*LN[1] + (-n[1]*B.ce+n[2]*B.se)*LN[2];
    for(const f of faces){
      if(layerSet && !layerSet.has(f.layer)) continue;
      const rv=f.v.map(([x,y,z])=>projVert(x,y,z,B,C));
      const a0=rv[0], b0=rv[1], c0=rv[2];
      let n=nrm(crs([b0.xr-a0.xr,b0.yr-a0.yr,b0.zr-a0.zr],[c0.xr-a0.xr,c0.yr-a0.yr,c0.zr-a0.zr]));
      let sh=shadeOf(n); if(sh<0 && f.b<=-0.8) sh=shadeOf([-n[0],-n[1],-n[2]])*0.9;
      const M=MATS[f.mat]||MATS.liner, ramp=M.ramp, tex=f.tex, uv=f.uv, flat=f.flat;
      const foc = s.focus && f.iact===s.focus ? 1.15 : 0;
      const fidx=sh*GAIN+BIAS+f.b+foc;
      for(let t=1;t+1<rv.length;t++) tri(rv[0],rv[t],rv[t+1],0,t,t+1);
      function tri(a,b,c,ia,ib,ic){
        const minX=Math.max(0,Math.floor(Math.min(a.sx,b.sx,c.sx))), maxX=Math.min(Wp-1,Math.ceil(Math.max(a.sx,b.sx,c.sx)));
        const minY=Math.max(0,Math.floor(Math.min(a.sy,b.sy,c.sy))), maxY=Math.min(Hp-1,Math.ceil(Math.max(a.sy,b.sy,c.sy)));
        const area=(b.sx-a.sx)*(c.sy-a.sy)-(c.sx-a.sx)*(b.sy-a.sy); if(Math.abs(area)<1e-6) return;
        const ua=uv?uv[ia]:null, ub=uv?uv[ib]:null, uc=uv?uv[ic]:null;
        for(let y=minY;y<=maxY;y++) for(let x=minX;x<=maxX;x++){ const px=x+0.5, py=y+0.5;
          const w0=((b.sx-px)*(c.sy-py)-(c.sx-px)*(b.sy-py))/area, w1=((c.sx-px)*(a.sy-py)-(a.sx-px)*(c.sy-py))/area, w2=1-w0-w1;
          if(w0<-0.001||w1<-0.001||w2<-0.001) continue;
          const d=w0*a.d+w1*b.d+w2*c.d, deff=d-f.db, i=y*Wp+x;
          if(deff<zbuf[i]){ zbuf[i]=deff; dep[i]=d; nbuf[i]=f.mat; abuf[i]=f.iact;
            let fi=fidx;
            if(tex&&uv){ const uu=w0*ua[0]+w1*ub[0]+w2*uc[0], vv=w0*ua[1]+w1*ub[1]+w2*uc[1]; fi+=tex(uu,vv); }
            let idx; if(flat){ idx=Math.round(fi); } else { const base=Math.floor(fi); idx=base+((fi-base)>BAYER[x&3][y&3]?1:0); }
            ibuf[i]=Math.max(0,Math.min(ramp.length-1,idx)); rbuf[i]=ramp; } }
      }
    }
    return { rbuf, ibuf, nbuf, abuf, dep };
  }
  function post(bufs, s, env){
    const C=env.L.cell, SH=env.L.shade, Wp=C.W, Hp=C.H, N=Wp*Hp, EDGE=SH.EDGE||0.30, KEY=SH.KEY;
    const { rbuf, ibuf, nbuf, abuf, dep }=bufs, out=new Array(N).fill(null);
    for(let i=0;i<N;i++) if(rbuf[i]) out[i]=rbuf[i][ibuf[i]];
    for(let y=0;y<Hp;y++) for(let x=0;x<Wp;x++){ const i=y*Wp+x; if(!rbuf[i]) continue;
      for(const [dx,dy] of [[1,0],[0,1]]){ const nx=x+dx,ny=y+dy; if(nx>=Wp||ny>=Hp) continue; const j=ny*Wp+nx; if(!rbuf[j]) continue;
        if(Math.abs(dep[i]-dep[j])>EDGE){ const far=dep[i]>dep[j]?i:j; out[far]=rbuf[far][Math.max(0,ibuf[far]-2)]; } } }
    if(s.weather>0.02){ const rnd=mulberry32(4211+(s.hull==='cape'?77:0));
      for(let i=0;i<N;i++){ const m=nbuf[i]; if(!m||!rbuf[i]) continue;
        if((m==='sole'||m==='liner'||m==='cab'||m==='wains') && rnd()<s.weather*0.05) out[i]=rbuf[i][Math.max(0,ibuf[i]-1)]; } }
    if(s.night){ for(let y=1;y<Hp-1;y++) for(let x=1;x<Wp-1;x++){ const i=y*Wp+x;
      if(nbuf[i]!=='flame'&&nbuf[i]!=='screen') continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,-1],[1,-1],[-1,1]]){ const j=(y+dy)*Wp+(x+dx);
        if(out[j]&&nbuf[j]!=='flame'&&nbuf[j]!=='screen') out[j]=mix(out[j],'#f2c25e',0.22); } } }
    for(let y=0;y<Hp;y++) for(let x=0;x<Wp;x++){ const i=y*Wp+x; if(!out[i]) continue; let n=0;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<Wp&&ny>=0&&ny<Hp&&out[ny*Wp+nx]) n++; }
      if(n===0){ out[i]=null; rbuf[i]=null; } }
    if(s.focus){ for(let y=1;y<Hp-1;y++) for(let x=1;x<Wp-1;x++){ const i=y*Wp+x;
      if(abuf[i]!==s.focus) continue;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const j=(y+dy)*Wp+(x+dx);
        if(abuf[j]!==s.focus){ out[j]=out[j]?mix(out[j],RIM,0.72):RIM; } } } }
    if(s.outline){ for(let y=0;y<Hp;y++) for(let x=0;x<Wp;x++){ const i=y*Wp+x; if(out[i]) continue; let touch=false;
      for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){ const nx=x+dx,ny=y+dy; if(nx>=0&&nx<Wp&&ny>=0&&ny<Hp&&rbuf[ny*Wp+nx]){ touch=true; break; } }
      if(touch) out[i]=KEY; } }
    return out;
  }
  function toRGBA(cols, C){ const rgba=new Uint8ClampedArray(C.W*C.H*4);
    for(let i=0;i<C.W*C.H;i++){ const c=cols[i]; if(!c){ rgba[i*4+3]=0; continue; }
      const [r,g,b]=hex2rgb(c); rgba[i*4]=r;rgba[i*4+1]=g;rgba[i*4+2]=b;rgba[i*4+3]=255; }
    return rgba; }

  function render(dir, opts){
    opts=(typeof opts==='number')?{elev:opts}:(opts||{});
    const s=resolve(opts), env=hullEnv(s.hull, s.variant);
    if(!env){ return new Uint8ClampedArray(4); }
    const B=camBasis(Object.assign({},opts,{dir}), env);
    const faces=build(s,env,B), MATS=makeMats(s,env);
    const set = s.layers ? new Set(s.layers) : null;
    return toRGBA(post(paint(faces,B,MATS,s,env,set),s,env), env.L.cell);
  }
  function renderLayers(dir, opts){
    return LAYERS.map(name=>({ name, rgba:render(dir, Object.assign({},opts,{layers:[name]})) }));
  }
  function project(hull, dir, p, opts){
    const env=hullEnv(hull, opts&&opts.variant); if(!env) return null;
    const B=camBasis(Object.assign({},opts||{},{dir}), env);
    const v=projVert(p[0],p[1],p[2],B,env.L.cell); return {x:v.sx,y:v.sy};
  }

  // ---- hotspots / anchors / sidecar generators ----
  function itemBox(id, env){
    const lay=env.lay, e=lay[id]; if(!e) return null;
    const lvl=(e.level)||(ITEMS[id]?ITEMS[id].level:'house'), sole=soleZof(env,lvl);
    if(id==='bunk' && e.x0==null){ const C=env.H.cuddy; if(!C) return null;
      const half=(y,z)=>Math.max(0.06, env.L.halfAtZ(y,z)-0.18);
      return { x0:-half(e.y0,C.soleZ+e.top), x1:half(e.y0,C.soleZ+e.top), y0:e.y0,y1:e.y1,
               z0:C.soleZ, z1:C.soleZ+e.top+0.14, level:'cuddy' }; }
    if(id==='helm') return { x0:e.x0,x1:e.x1, y0:(e.seat?Math.min(e.seat[1]-0.30,e.y0):e.y0), y1:e.y1, z0:sole+(e.dz||0), z1:sole+(e.dz||0)+e.h+0.30, level:lvl };
    const h=(e.h!=null?e.h:(e.top||0.5)+0.14);
    return { x0:e.x0,x1:e.x1,y0:e.y0,y1:e.y1, z0:sole, z1:sole+h+(id==='stove'?0.20:0), level:lvl };
  }
  function backVisible(back, B){
    if(back==='port') return !cullWall([-1,0,0],B);
    if(back==='star') return !cullWall([ 1,0,0],B);
    if(back==='aft')  return !cullWall([0,-1,0],B);
    if(back==='fwd')  return !cullWall([0, 1,0],B);
    return true;
  }
  function hotspots(dir, opts){
    opts=opts||{}; const s=resolve(opts), env=hullEnv(s.hull, s.variant); if(!env) return [];
    const B=camBasis(Object.assign({},opts,{dir}), env), C=env.L.cell, out=[];
    for(const id of Object.keys(ITEMS)){
      const meta=ITEMS[id], bb=itemBox(id,env); if(!bb) continue;
      let minx=1e9,miny=1e9,maxx=-1e9,maxy=-1e9;
      for(const x of [bb.x0,bb.x1]) for(const y of [bb.y0,bb.y1]) for(const z of [bb.z0,bb.z1]){
        const q=projVert(x,y,z,B,C); minx=Math.min(minx,q.sx); maxx=Math.max(maxx,q.sx);
        miny=Math.min(miny,q.sy); maxy=Math.max(maxy,q.sy); }
      const cxm=(bb.x0+bb.x1)/2, cym=(bb.y0+bb.y1)/2, soleZ=soleZof(env, bb.level);
      const reach = meta.back==='port' ? [bb.x1+0.42,cym,bb.z0]
                  : meta.back==='star' ? [bb.x0-0.42,cym,bb.z0]
                  : meta.back==='fwd'  ? [cxm,bb.y0-0.50,bb.z0]
                  : [cxm,bb.y1+0.50,bb.z0];
      const rp=projVert(reach[0],reach[1],reach[2],B,C);
      const tall=(bb.z1-soleZ)>=0.95;
      out.push({ id, action:meta.action, label:meta.label, verb:meta.verb, level:bb.level,
        visible: (tall ? backVisible(meta.back,B) : true),
        rect:{ x:Math.round(minx), y:Math.round(miny), w:Math.round(maxx-minx), h:Math.round(maxy-miny) },
        centre:{ x:Math.round((minx+maxx)/2), y:Math.round((miny+maxy)/2) },
        world:{ x:+cxm.toFixed(3), y:+cym.toFixed(3), z:+((bb.z0+bb.z1)/2).toFixed(3) },
        reach:{ world:reach.map(v=>+v.toFixed(3)), screen:{x:Math.round(rp.sx),y:Math.round(rp.sy)} },
        footprint:[[bb.x0,bb.y0],[bb.x1,bb.y0],[bb.x1,bb.y1],[bb.x0,bb.y1]].map(p=>[+p[0].toFixed(3),+p[1].toFixed(3)]),
        height_above_sole_m:+(bb.z1-soleZ).toFixed(2), back:meta.back });
    }
    return out;
  }
  function anchors(dir, opts){
    opts=opts||{}; const s=resolve(opts), env=hullEnv(s.hull, s.variant); if(!env) return null;
    const Hh=env.H, D=Hh.door;
    const P=(p)=>{ const q=project(s.hull,dir,p,opts); return {x:q.x,y:q.y,m:p}; };
    if(Hh.kind==='ship'||Hh.kind==='sport'){
      const dk=Hh.decks[s.level]||Hh.decks.house;
      const yTop = dk.y1!=null ? dk.y1 : (dk.front ? Math.min(dk.front.yBot,dk.front.yTop) : dk.y0+4);
      return { sole:P([0,(dk.y0+yTop)/2,dk.soleZ]),
        door:P([(D.x0+D.x1)/2, D.y, Hh.decks.house.soleZ]),
        companion:null, items:hotspots(dir,opts) };
    }
    return { sole:P([0,(Hh.yAft+Hh.yFwd)/2,Hh.soleZ]),
      door:P([(D.x0+D.x1)/2, Hh.yAft, Hh.soleZ]),
      companion: Hh.cuddy?P([(Hh.cuddy.opening.x0+Hh.cuddy.opening.x1)/2,(Hh.front.kind==='rake'?Hh.front.yBot:Hh.yFwd),Hh.soleZ]):null,
      items:hotspots(dir,opts) };
  }

  function soleObstructions(env, level){
    level=level||'house';
    const lay=env.lay, rows=[], lvl=(e,d)=>((e&&e.level)||d);
    const add=(id,b,h,tr)=>{ if(!b) return; rows.push({ id, footprint:[[b.x0,b.y0],[b.x1,b.y0],[b.x1,b.y1],[b.x0,b.y1]].map(p=>[+p[0].toFixed(2),+p[1].toFixed(2)]),
      height_above_sole_m:+h.toFixed(2), treatment: tr||(h<=0.50?'step_over':(h<=0.95?'waist_block':'wall')) }); };
    if(lay.helm && lvl(lay.helm,'house')===level){ add('helm_console',lay.helm,lay.helm.h+(lay.helm.dz||0));
      if(lay.helm.seat) add('helm_seat',{x0:lay.helm.seat[0]-0.24,x1:lay.helm.seat[0]+0.24,y0:lay.helm.seat[1]-0.26,y1:lay.helm.seat[1]+0.22},1.02+(lay.helm.dz||0)); }
    if(lay.helmDeck && level==='house') add('helm_deck_riser', lay.helmDeck, lay.helmDeck.rise);
    if(lay.stove && lvl(lay.stove,'house')===level) add('stove_counter',lay.stove,lay.stove.h);
    if(lay.locker && lvl(lay.locker,'house')===level) add('locker',lay.locker,lay.locker.h);
    if(lay.bench && lvl(lay.bench,'house')===level) add('bench',lay.bench,lay.bench.h);
    if(lay.bunk && lvl(lay.bunk,'cuddy')===level && lay.bunk.x0!=null) add('bunk',lay.bunk,(lay.bunk.top||0.5));
    for(const fu of (lay.furn||[])){ if(fu.level!==level||fu.kind==='tv'||fu.kind==='rug') continue;
      add(fu.kind+((fu.x0+fu.x1)/2<0?'_port':'_stbd'),fu,(fu.h!=null?fu.h:(fu.kind==='settee'?0.88:(fu.kind==='stool'?0.70:(fu.kind==='sidetable'?0.52:(fu.top||0.5)))))); }
    for(const sp of (lay.spaces||[])) if(sp.level===level) add(sp.id,sp,(sp.h!=null?sp.h:2.0),'wall');
    if(lay.stairs){
      if(lay.stairs.up && level==='house') add('companionway_up_flight',{x0:lay.stairs.up.x0,x1:lay.stairs.up.x1,y0:Math.min(lay.stairs.up.yBot,lay.stairs.up.yTop),y1:Math.max(lay.stairs.up.yBot,lay.stairs.up.yTop)},1.2,'stair_flight');
      if(lay.stairs.down && level==='house') add('companionway_down_opening',{x0:lay.stairs.down.x0,x1:lay.stairs.down.x1,y0:Math.min(lay.stairs.down.yBot,lay.stairs.down.yTop),y1:Math.max(lay.stairs.down.yBot,lay.stairs.down.yTop)},0,'stair_opening');
      if(lay.stairs.up && level==='bridge') add('companionway_up_opening',{x0:lay.stairs.up.x0,x1:lay.stairs.up.x1,y0:lay.stairs.up.yTop-0.9,y1:lay.stairs.up.yTop+0.15},0,'stair_opening');
    }
    return rows;
  }
  /* THRESHOLD in the camper schema, verbatim per the gameplay brief (2026-08-19): id · side ·
     door_clear_* · threshold_point · hinge_axis · swing{keep_clear}. Sliders carry a `slide` block in
     swing's position (a slider has no hinge — flagged in its note) plus mechanism:'sliding'. */
  function thresholdBlock(env, DD, tid){
    const Hh=env.H, D=DD||Hh.door, r3=(v)=>+v.toFixed(3), sill=(D.sillZ!=null?D.sillZ:Hh.soleZ);
    const base={ id:tid||'entry', side:(D.face==='aft'?'aft':(D.face==='fwd'?'forward':D.face)),
      door_clear_width_m:r3(D.x1-D.x0), door_clear_height_m:r3(D.z1-D.z0),
      threshold_point:[r3((D.x0+D.x1)/2), r3(D.y), r3(sill)] };
    if(D.kind==='hinge'){
      const hx=(D.hinge==='port')?D.x0:D.x1, tip=(D.hinge==='port')?D.x1:D.x0, r=Math.abs(D.x1-D.x0);
      const sgn=(D.face==='aft')?-1:1, A=D.swingDeg*Math.PI/180;
      const pt=(a)=>[ r3(hx+(tip-hx)*Math.cos(a)), r3(D.y + sgn*r*Math.sin(a)) ];
      base.hinge_axis={ x:r3(hx), y:r3(D.y), vertical:true };
      base.swing={ outward:true,
        toward:(D.face==='aft'?('-y (aft, onto the '+(env.H.poop?'poop mooring deck':'working deck')+')'):'+y (forward, onto the working deck)'),
        open_deg:D.swingDeg, arc_radius_m:r3(r),
        keep_clear:[[r3(hx),r3(D.y)], pt(0), pt(A*0.5), pt(A)],
        note:'Collider is the swept arc, not the leaf. 8-frame cue (doorOpen = k/7), played reversed on exit.' };
    } else {
      base.mechanism='sliding';
      base.slide={ outward:false, toward:'+x (starboard) along an exterior track; parks over the aft wall',
        travel_m:D.travel, clear_at_open_fraction:D.clearAt,
        keep_clear:[[r3(D.leaf.x0),r3(D.y)],[r3(D.leaf.x1+D.travel),r3(D.y)],[r3(D.leaf.x1+D.travel),r3(D.y-0.15)],[r3(D.leaf.x0),r3(D.y-0.15)]],
        note:'No swept arc — the collider is the leaf itself (proud of the wall 0.085 m), riding doorOpen*travel along +x. 8-frame cue (doorOpen = k/7), played reversed on exit. hinge_axis/swing omitted: this leaf slides — flagged for intake.' };
    }
    base.door_cue={ frames:8, played_reversed_on_exit:true, doorOpen_per_frame:'k/7 for k in 0..7', suggested_ms_per_frame:70 };
    return base;
  }
  function interactables(opts){
    opts=opts||{}; const s=resolve(opts), env=hullEnv(s.hull, s.variant); if(!env) return null;
    const hs=hotspots(0,{hull:s.hull, variant:s.variant}).map(h=>({
      id:h.id, action:h.action, label:h.label, prompt:h.verb, level:h.level,
      footprint:h.footprint, height_above_sole_m:h.height_above_sole_m,
      anchor:[h.world.x,h.world.y,h.world.z], reach_point:h.reach.world, backs_onto:h.back,
      visible_facings:[0,1,2,3,4,5,6,7].filter(d=>{ const B=camBasis({dir:d},env);
        return h.height_above_sole_m<0.95 ? true : backVisible(h.back,B); }).map(d=>DIRL[d]),
      mechanism:ITEMS[h.id]?ITEMS[h.id].mech:undefined,
      provenance:'boatInteriorRig LAYOUT.'+s.hull+' entry "'+h.id+'", measured against the published loft' }));
    return { exportSymbol:'BoatInterior', rig:'boatInteriorRig.js', hull:s.hull,
      generatedBy:'BoatInterior.interactables({hull:"'+s.hull+'"})',
      frame:'same as the hull sidecar — metres, '+env.E.PX+' px = 1 m, origin amidships/keel/centreline',
      note:'Merges into the hull file as its INTERACT section. Reach points are sole-level standing '+
           'spots; check them against DECK before use — a reach point is a request, not a promise.',
      INTERACT:hs };
  }
  // the full sidecar additions for this hull: DECK add-ons + THRESHOLD + STAIRS (+LADDER) + INTERACT
  function gameplaySections(opts){
    opts=opts||{}; const s=resolve(opts), env=hullEnv(s.hull, s.variant); if(!env) return null;
    if(env.H.kind==='ship'||env.H.kind==='sport') return shipGameplaySections(s, env);
    const Hh=env.H, D=Hh.door, C=Hh.cuddy, r3=(v)=>+v.toFixed(3);
    const NS=10, yA=Hh.yAft+WT, yF=Hh.yFwd-0.015, hx=(y)=>Hh.hxAt(y)-WT;
    const housePoly=[]; for(let i=0;i<=NS;i++){ const y=yA+(yF-yA)*i/NS; housePoly.push([r3(-hx(y)),r3(y)]); }
    for(let i=NS;i>=0;i--){ const y=yA+(yF-yA)*i/NS; housePoly.push([r3(hx(y)),r3(y)]); }
    const deck=[{ id:'house_sole', z:Hh.soleZ, winding:'ccw_from_above', polygon:housePoly,
      note:'Wheelhouse sole, house plan inset '+WT+' m for the walls. Obstructions in _notes (owner ruling: game-side colliders, never authored holes).',
      _notes:soleObstructions(env,'house') }];
    if(C){ const half=(y,z)=>Math.max(0.06, env.L.halfAtZ(y,z)-0.12), NP=8, poly=[];
      for(let i=0;i<=NP;i++){ const y=C.y0+(C.y1-C.y0)*i/NP; poly.push([r3(-half(y,C.soleZ+0.05)),r3(y)]); }
      for(let i=NP;i>=0;i--){ const y=C.y0+(C.y1-C.y0)*i/NP; poly.push([r3(half(y,C.soleZ+0.05)),r3(y)]); }
      deck.push({ id:'cuddy_sole', z:C.soleZ, winding:'ccw_from_above', polygon:poly,
        note:'Under-foredeck berth space; headroom is the foredeck underside (sheerZ(y)-0.16). The berth platform occupies y '+env.lay.bunk.y0+'..'+env.lay.bunk.y1+' at +'+env.lay.bunk.top+' m.',
        _notes:[{ id:'v_berth', footprint:[[-0.9,env.lay.bunk.y0],[0.9,env.lay.bunk.y0],[0.4,env.lay.bunk.y1],[-0.4,env.lay.bunk.y1]],
          height_above_sole_m:env.lay.bunk.top, treatment:'waist_block' }] }); }
    const threshold=Object.assign(thresholdBlock(env), { provenance:'rig DOOR const (published as HOUSE.door)' });
    let stairs=null;
    if(C){ const st=C.step, treads=[];
      for(let i=1;i<=st.treads;i++) treads.push({ top_z:r3(C.soleZ+(Hh.soleZ-C.soleZ)*i/(st.treads+1)), going_m:0.24 });
      stairs={ companionways:[{ id:'cuddy_companionway', from:'house_sole', to:'cuddy_sole',
        opening:{ x0:C.opening.x0, x1:C.opening.x1, at_y:r3(Hh.front.kind==='rake'?Hh.front.yBot:Hh.yFwd), z0:r3(C.soleZ), z1:r3(C.opening.z1) },
        total_rise_m:r3(Hh.soleZ-C.soleZ), treads, direction:'down going forward (+y)',
        provenance:'HOUSE.cuddy published by the exterior rig; treads placed by boatInteriorRig' }] }; }
    return { DECK_ADD:deck, THRESHOLD:threshold, STAIRS:stairs,
      LADDER_EXCLUDED:'no ladder on a '+(env.L.L)+' m wheelhouse boat — the washboards are the route forward and the roof is gear, not deck',
      INTERACT:interactables({hull:s.hull, variant:s.variant}).INTERACT };
  }

  // ships: the whole sidecar section set is generated (these hulls had no prior gameplay file)
  function shipGameplaySections(s, env){
    const Hh=env.H, L=env.L, lay=env.lay, r3=(v)=>+v.toFixed(3);
    const rectPoly=(hx,y0,y1)=>[[-hx,y0],[hx,y0],[hx,y1],[-hx,y1]].map(p=>[r3(p[0]),r3(p[1])]);
    const bandPoly=(yA,yB,z)=>{ const half=(y)=>Math.max(0.5, L.halfAtZ(y,z+0.35)-0.45), NP=10, poly=[];
      for(let i=0;i<=NP;i++){ const y=yA+(yB-yA)*i/NP; poly.push([r3(-half(y)),r3(y)]); }
      for(let i=NP;i>=0;i--){ const y=yA+(yB-yA)*i/NP; poly.push([r3(half(y)),r3(y)]); }
      return poly; };
    const mainU=Hh.main||null;
    const yStern= mainU ? (L.L*mainU.u0-L.L/2) : (-L.L/2+0.55);
    const yBreak= L.L*(mainU?mainU.u1:L.SOLE_U)-L.L/2-0.10;
    const main=bandPoly(yStern,yBreak,L.DECK);
    if(Hh.ramp){ main.push([r3(Hh.ramp.halfW),r3(yStern)],[r3(Hh.ramp.halfW),r3(Hh.ramp.yTop)],
                           [r3(-Hh.ramp.halfW),r3(Hh.ramp.yTop)],[r3(-Hh.ramp.halfW),r3(yStern)]); }
    const dkH=Hh.decks.house, dkB=Hh.decks.bridge, dkL=Hh.decks.below;
    const bHx=(dkB.hxAt?dkB.hxAt(dkB.soleZ):dkB.hx)-WT;
    const blk=Hh.block||{ hx:dkH.hx, y0:dkH.y0, y1:dkH.y1, topZ:dkH.ceilZ };
    const extraObs=(Hh.deckObstructions||[]).map(o=>({ id:o.id, footprint:o.footprint.map(p=>[r3(p[0]),r3(p[1])]),
      height_above_sole_m:r3(o.height), treatment:o.treatment||'wall' }));
    const mainNotes=(Hh.poop?[]:[{ id:'deckhouse', footprint:rectPoly(blk.hx,blk.y0,blk.y1), height_above_sole_m:r3(blk.topZ-L.DECK), treatment:'wall' }]).concat(extraObs);
    const deck=(Hh.mainDeckExternal?[]:[
      { id:'main_deck', z:r3(L.DECK), winding:'as_listed', polygon:main,
        note:'Working deck inside the bulwark liner (deckHalf = halfAtZ(y, DECK+0.35) - 0.45).'
          +(Hh.ramp?' The stern-ramp slot is notched OUT of the polygon — the ramp floor is sloped wet steel, not walkable deck.':'')
          +(Hh.poop?' Runs the tank deck between the poop break and the foc\'sle; reached from the poop by the break ladders (LADDER).':'')
          +' Loose deck gear colliders are game-side (owner ruling); the authored obstructions are in _notes.',
        _notes:mainNotes }]).concat(Hh.poop?[
      { id:'poop_deck', z:r3(Hh.poop.z), winding:'as_listed',
        polygon:bandPoly(L.L*Hh.poop.u0-L.L/2, L.L*Hh.poop.u1-L.L/2-0.06, Hh.poop.z),
        note:'Raised poop around the deckhouse — mooring deck aft, alleyways beside the house. Down to the weather deck by the break ladders; up to the boat deck by the aft ladder (LADDER).',
        _notes:[{ id:'deckhouse', footprint:rectPoly(blk.hx, blk.y0, Math.min(blk.y1, r3(L.L*Hh.poop.u1-L.L/2))), height_above_sole_m:r3(blk.topZ-Hh.poop.z), treatment:'wall' }] }]:[]).concat([
      { id:'house_sole', z:r3(dkH.soleZ), winding:'ccw_from_above', polygon:rectPoly(dkH.hx-WT,dkH.y0+WT,dkH.y1-WT),
        note:(Hh.kind==='sport'?'Salon interior at mezzanine height — the raked glass nose forward of y '+dkH.y1+' is dressed lounge (interior walls straighten the rounded plan; authored abstraction, flagged). Stair flight and opening carried as obstructions.'
             :(s.hull==='tanker'?'Deckhouse interior — aft mess + galley at poop level. Forward of y '+dkH.y1+' the block is cabins + alleyways, dressed closed this pass. Stair flight and opening carried as obstructions.'
             :'Deckhouse interior — galley/mess. Stair flight and opening carried as obstructions.')), _notes:soleObstructions(env,'house') }].concat(dkB.external?[]:[
      { id:(dkB.deckId||'bridge_sole'), z:r3(dkB.soleZ), winding:'ccw_from_above',
        polygon:rectPoly(bHx, dkB.y0+WT, Math.min(dkB.front.yBot,dkB.front.yTop)-0.06),
        note:(Hh.kind==='sport'?'The skylounge — a FULL DECK dedicated to the helm: glass on three sides, the helm against the windshield, companion settee aft; its own slider onto the aft deck (THRESHOLD.additional).':'Wheelhouse, reached by the internal companionway.'), _notes:soleObstructions(env,'bridge') }]).concat([
      { id:'below_sole', z:r3(dkL.soleZ), winding:'ccw_from_above',
        polygon:(function(){ const cap=(dkL.hxCap!=null)?dkL.hxCap:1e9;
          const half=(y)=>Math.max(0.4, Math.min(cap, L.halfAtZ(y,dkL.soleZ+0.05)-0.28)), poly=[];
          for(let i=0;i<=8;i++){ const y=dkL.y0+(dkL.y1-dkL.y0)*i/8; poly.push([r3(-half(y)),r3(y)]); }
          for(let i=8;i>=0;i--){ const y=dkL.y0+(dkL.y1-dkL.y0)*i/8; poly.push([r3(half(y)),r3(y)]); }
          return poly; })(),
        note:'Crew flat below the house; headroom to the deck beams '+r3(dkL.ceilZ-dkL.soleZ)+' m'
          +(s.hull==='dragger'?' — cramped and true; a 25 m side trawler gives no more':'')
          +(dkL.hxCap!=null?'. Flat is capped at ±'+dkL.hxCap+' m — engine casing and wing tanks eat the sides (authored cap, not the hull)':'')+'.',
        _notes:soleObstructions(env,'below') },
    ]));
    const st=lay.stairs, zH=dkH.soleZ, zB=dkB.soleZ, zL=dkL.soleZ;
    const tr=(n,za,zb)=>{ const a=[]; for(let i=1;i<=n;i++) a.push({ top_z:r3(za+(zb-za)*i/(n+1)), going_m:0.24 }); return a; };
    const upCw={ id:'house_to_bridge', from:'house_sole', to:(dkB.external?'bridge_sole':(dkB.deckId||'bridge_sole')), mechanism:'InteriorStair',
        opening:{ x0:st.up.x0, x1:st.up.x1, y0:r3(st.up.yTop-0.9), y1:r3(st.up.yTop+0.15), in:'bridge_sole' },
        total_rise_m:r3(zB-zH), treads:tr(st.up.treads,zH,zB),
        direction:(st.up.yTop>st.up.yBot?'up going forward (+y)':'up going aft (-y)'),
        provenance:'boatInteriorRig LAYOUT.'+s.hull+'.stairs.up' };
    if(zB-zH>4 && Hh.kind!=='sport') upCw.note='one companionway carries house→bridge whole — the accommodation deck(s) it passes are dressed (game abstraction, flagged)';
    if(Hh.kind==='sport') upCw.note= dkB.external
      ? 'interior companionway through the salon to the open bridge — closes the extractor finding that NO route up was modelled; bridge_sole is the extractor’s polygon'
      : 'interior companionway salon→skylounge — the full helm deck; the open coaming above stays the extractor’s, reached by the declared LADDER legs';
    const stairs={ companionways:[ upCw,
      { id:'house_to_below', from:'house_sole', to:'below_sole', mechanism:'InteriorStair',
        opening:{ x0:st.down.x0, x1:st.down.x1, y0:r3(Math.min(st.down.yTop,st.down.yBot)), y1:r3(Math.max(st.down.yTop,st.down.yBot)), in:'house_sole' },
        total_rise_m:r3(zH-zL), treads:tr(st.down.treads,zL,zH),
        direction:(st.down.yBot>st.down.yTop?'down going forward (+y)':'down going aft (-y)'),
        provenance:'boatInteriorRig LAYOUT.'+s.hull+'.stairs.down' } ] };
    // the 53's raised helm deck: its own DECK entry + the short flight up from the lounge
    if(Hh.kind==='sport' && lay.helmDeck){ const hd=lay.helmDeck, hz=r3(dkH.soleZ+hd.rise);
      deck.push({ id:'helm_deck', z:hz, winding:'ccw_from_above',
        polygon:[[hd.x0,hd.y0],[hd.x1,hd.y0],[hd.x1,hd.y1],[hd.x0,hd.y1]].map(p=>[r3(p[0]),r3(p[1])]),
        note:'Raised helm deck hard against the salon windshield — the lower station, a short flight up from the lounge (the enter-from-the-cockpit level).', _notes:[] });
      stairs.companionways.push({ id:'lounge_to_helm_deck', from:'house_sole', to:'helm_deck', mechanism:'InteriorStair',
        opening:{ x0:(hd.sx0!=null?hd.sx0:-0.45), x1:(hd.sx1!=null?hd.sx1:0.45), y0:r3(hd.y0-0.26*hd.treads), y1:r3(hd.y0), in:'house_sole' },
        total_rise_m:r3(hd.rise), treads:tr(hd.treads,dkH.soleZ,dkH.soleZ+hd.rise), direction:'up going forward (+y)',
        provenance:'boatInteriorRig LAYOUT.'+s.hull+'.helmDeck' }); }
    const lads=Hh.ladders || (Hh.ladder?[Hh.ladder]:[]);
    const LADDER=lads.map(lad=>{ const row={ id:lad.id, kind:'vertical_ladder', exterior:true,
      base:[r3(lad.x), r3(lad.y)], z0:r3(lad.z0), z1:r3(lad.z1), face:lad.face,
      rungs:Math.max(2,Math.floor((lad.z1-lad.z0)/0.31)), rung_spacing_m:0.31,
      note:lad.note||('Main deck to the boat deck, fixed to the house '+(lad.face==='aft'?'aft wall stbd of':'front wall port of')+' the crew door.'),
      provenance:'exterior rig HOUSE.'+(Hh.ladders?'ladders':'ladder') };
      if(lad.connects) row.connects=lad.connects;
      return row; });
    const IN=interactables({hull:s.hull, variant:s.variant}).INTERACT;
    // sport fishers: the boat is worked from MULTIPLE helm points — declare each as a stable
    // enter_helm interactable at its station (exterior decks are the extractor's; ids append-only)
    for(const hm of (Hh.helms||[])) IN.push({
      id:hm.id, action:'enter_helm', at:hm.deck||undefined, exterior:true,
      anchor:hm.pos.map(r3), reach_point:hm.reach.map(r3),
      visible_facings:DIRL.slice(),
      dressed:hm.dressed||undefined,
      mechanism:'the existing helm',
      provenance:'exterior rig HOUSE.helms (anchors HELM / TOWER_STATION)',
      _note:'mechanism: the existing helm · '+hm.note });
    return { DECK_ADD:deck,
      THRESHOLD:(function(){ const TH=Object.assign(thresholdBlock(env), { provenance:'rig DOOR const (published as HOUSE.door)' });
        if(Hh.door2) TH.additional=[Object.assign(thresholdBlock(env, Hh.door2, 'sky_entry'),
          { note:'skylounge slider onto the aft deck — the second-level door; rides the same doorOpen cue this pass (flagged if it needs its own)',
            provenance:'rig DOOR2 (published as HOUSE.door2)' })];
        return TH; })(),
      STAIRS:stairs, LADDER:(LADDER.length?LADDER:undefined), INTERACT:IN,
      _excluded:Object.assign(Hh.kind==='sport'?{}:{ side_doors:'the lower-house side doors are dressed closed — not routes this pass',
        focsle_deck:"the whaleback foc'sle rises with the sheer; a flat-z DECK entry cannot carry it — forward work stays on main_deck this pass" }, Hh.excluded||{}, lay.excluded||{}) };
  }
  // a COMPLETE gameplay sidecar for hulls that never had one (stamped at export)
  function fullSidecar(opts){
    const s=resolve(opts), env=hullEnv(s.hull, s.variant); if(!env) return null;
    const E=env.E, gs=gameplaySections({hull:s.hull, variant:s.variant}); if(!gs) return null;
    const doc={ schema:'hidden-harbours/boat-gameplay-geometry@1', rig:env.meta.rig, exportSymbol:env.meta.sym,
      cell:{ w:E.W, h:E.H, pivot:{ x:E.pivot.x, y:E.pivot.y } }, px_per_m:E.PX,
      frame:{ units:'m', scale_px_per_m:E.PX, origin:'amidships, keel bottom, centreline',
              axes:'+x starboard, +y bow, +z up', heading_independent:true },
      authoring:'Generated whole in the boat-interiors program (2026-08-19) — this hull had no prior sidecar. '+
        'The hull rig owns the shell (derivedFromRigSha256); boatInteriorRig.js authored DECK/THRESHOLD/STAIRS/LADDER/INTERACT (interiorDerivedFromRigSha256).',
      DECK:gs.DECK_ADD, THRESHOLD:gs.THRESHOLD, STAIRS:gs.STAIRS, INTERACT:gs.INTERACT };
    if(gs.LADDER && gs.LADDER.length) doc.LADDER=gs.LADDER;
    doc._excluded=Object.assign({}, gs._excluded||{}, gs.LADDER_EXCLUDED?{LADDER:gs.LADDER_EXCLUDED}:{});
    return doc;
  }
  /* The per-hull interior export — <hullStem>.interior.json per the gameplay brief (2026-08-19):
     named by the HULL stem, camper THRESHOLD schema, INTERACT exactly {id, action, at?, reach_point,
     visible_facings, _note}, frame block stated, derivedFromRigSha256 = LF sha of THE INTERIOR RIG
     (the rig that renders the sheets). Sha placeholders are stamped by the exporter. */
  function interiorSidecar(opts){
    const s=resolve(opts), env=hullEnv(s.hull, s.variant); if(!env) return null;
    const E=env.E, Hh=env.H, L=env.L, lay=env.lay, r3=(v)=>+v.toFixed(3);
    const gs=gameplaySections({hull:s.hull, variant:s.variant});
    const stem=env.meta.stem||env.meta.rig.replace('.js','');
    const vkey=s.variant?[s.variant.size||'standard',s.variant.style||'hardtop',s.variant.region||'northumberland'].join('_'):null;
    const hullStem=vkey?stem+'.'+vkey:stem;
    const FOOTPRINT={}, WALKABLE={};
    if(Hh.kind==='ship'||Hh.kind==='sport'){
      const rp=(hx,y0,y1)=>[[-hx,y0],[hx,y0],[hx,y1],[-hx,y1]].map(p=>[r3(p[0]),r3(p[1])]);
      FOOTPRINT.house=rp(Hh.decks.house.hx,Hh.decks.house.y0,Hh.decks.house.y1);
      if(!Hh.decks.bridge.external)
        FOOTPRINT.bridge=rp(Hh.decks.bridge.hx,Hh.decks.bridge.y0,Math.max(Hh.decks.bridge.front.yBot,Hh.decks.bridge.front.yTop));
    } else {
      const NSP=10, hp=[];
      for(let i=0;i<=NSP;i++){ const y=Hh.yAft+(Hh.yFwd-Hh.yAft)*i/NSP; hp.push([r3(-Hh.hxAt(y)),r3(y)]); }
      for(let i=NSP;i>=0;i--){ const y=Hh.yAft+(Hh.yFwd-Hh.yAft)*i/NSP; hp.push([r3(Hh.hxAt(y)),r3(y)]); }
      FOOTPRINT.house=hp;
    }
    for(const d of (gs.DECK_ADD||[])) WALKABLE[d.id]={ z:d.z, polygon:d.polygon, obstructions:d._notes||[] };
    const IN=(gs.INTERACT||[]).map(h=>({ id:h.id, action:h.action, at:h.at, reach_point:h.reach_point,
      visible_facings:h.visible_facings, exterior:h.exterior, dressed:h.dressed,
      _note:(h.exterior&&h._note) ? h._note
        : 'mechanism: '+(ITEMS[h.id]?ITEMS[h.id].mech:'—')+' · '+h.level+' level · footprint + obstructions in the hull gameplay sidecar' }));
    if((Hh.kind==='ship'||Hh.kind==='sport')&&lay.stairs){ const mid=(a)=>r3((a.x0+a.x1)/2), zH=Hh.decks.house.soleZ;
      IN.push({ id:'companionway_up', action:'stair', at:'house_to_bridge',
        reach_point:[mid(lay.stairs.up), r3(lay.stairs.up.yBot-0.55), r3(zH)], visible_facings:DIRL.slice(),
        _note:'mechanism: InteriorStair · house→bridge' });
      IN.push({ id:'companionway_down', action:'stair', at:'house_to_below',
        reach_point:[mid(lay.stairs.down), r3(lay.stairs.down.yTop+0.55), r3(zH)], visible_facings:DIRL.slice(),
        _note:'mechanism: InteriorStair · house→below' });
    }
    if(Hh.kind!=='ship'&&Hh.kind!=='sport'&&Hh.cuddy){ const o=Hh.cuddy.opening, yb=(Hh.front&&Hh.front.kind==='rake')?Hh.front.yBot:Hh.yFwd;
      IN.push({ id:'companionway', action:'stair', at:'cuddy_companionway',
        reach_point:[r3((o.x0+o.x1)/2), r3(yb-0.60), r3(Hh.soleZ)], visible_facings:DIRL.slice(),
        _note:'mechanism: InteriorStair · house→cuddy, '+Hh.cuddy.step.treads+' tread(s)' });
    }
    const doc={
      schema:'hidden-harbours/boat-interior@1', hull_stem:hullStem, fits_hulls:[hullStem],
      interior_rig:'boatInteriorRig.js',
      derivedFromRigSha256:'STAMP_AT_EXPORT_LF_SHA256_OF_boatInteriorRig.js',
      hullRigSha256:{},
      frame:{ units:'m', scale_px_per_m:E.PX, origin:'amidships, keel bottom, centreline',
              axes:'+x starboard, +y bow, +z up', heading_independent:true },
      cell:{ w:E.W, h:E.H, pivot:{ x:E.pivot.x, y:E.pivot.y }, facings:DIRL.slice(), levels:levelsOf(s.hull),
             note:'One sheet per level per facing, baked to the full hull cell at the hull pivot — composites under the exterior 1:1.'
               +(E.PX!==32?' This hull works at '+E.PX+' px/m (half fleet standard); interior sheets bake at the same scale — scale ×2 in-engine together.':'') },
      motion:{ rides_hull_rock:true,
               source:'exterior rig ROCK + rock(i) — pass the SAME roll/pitch/heave to both renders and registration holds mid-wave',
               level_floor_assumptions:'none — nothing is gimballed; the lamp, kettle and door tilt with the hull (diegetic)',
               comfort_clamp_compatible:true },
      FOOTPRINT, WALKABLE,
      THRESHOLD:gs.THRESHOLD, STAIRS:gs.STAIRS||null, INTERACT:IN,
      mechanism_map:{ helm:'existing helm (enter_helm)', bunk:'InteriorBed (sleep, rest + save)',
        locker:'InteriorWardrobe (storage)', stove:'camper stove pattern (cook)', companionway:'InteriorStair' },
    };
    doc.hullRigSha256[stem]='STAMP_AT_EXPORT_LF_SHA256_OF_'+env.meta.rig;
    if(s.variant) doc.variant={ size:s.variant.size||'standard', style:s.variant.style||'hardtop', region:s.variant.region||'northumberland' };
    if(gs.LADDER && gs.LADDER.length) doc.LADDER=gs.LADDER;
    if(gs.LADDER_EXCLUDED) doc._excluded={ LADDER:gs.LADDER_EXCLUDED };
    else if(gs._excluded) doc._excluded=gs._excluded;
    if(s.hull==='lobster') doc._stem_note='Stem mapping RESOLVED (tranche 4): the 18 variant hulls now carry their own interior sidecars (lobsterBoatVariantsIsoRig.<size>_<style>_<region>.interior.json); this file remains the canonical 12 m Tier-3 hull.';
    if(s.hull==='trawler2'||s.hull==='packet'||s.hull==='tanker') doc._stem_note='Stem '+stem+' is not yet in the gameplay repo list — new hull, flagged for intake.';
    if(s.hull==='sport53'||s.hull==='sport90'||s.hull==='lobvar') doc._stem_note='Stem '+hullStem+' is not yet in the gameplay repo list — new stem, flagged for intake.';
    return doc;
  }

  root.BoatInterior = { HULLS, LAYERS, ITEMS, LAYOUT, RIM, DIRL,
    list, cellOf, dims, resolve, render, renderLayers, hotspots, anchors, project,
    levelsOf, itemLevel, interactables, gameplaySections, fullSidecar, interiorSidecar };
})(typeof globalThis!=='undefined'?globalThis:window);
