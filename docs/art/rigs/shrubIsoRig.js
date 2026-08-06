/* Hidden Harbours — SHRUB / BUSH RIG.  20 Acadian shrubs across five habitats, one generator.

   The gap this fills: the tree rig owns anything with a leader, the shore plant rig owns anything the
   tide reaches, the flower rig owns anything herbaceous. Everything woody, waist-high and multi-stemmed
   — the alder swale, the blueberry barren, the dogwood on the creek bank — was hand-drawn per season,
   which is why the same alder had a summer sprite twice the width of its winter one.

   WHAT THIS RIG KNOWS THAT THE OTHERS DON'T: the CALENDAR, and the SNOW LINE.

   1. PHENOLOGY, NOT SEASON.  The tree rig has three seasons because a spruce has three. A shrub has
      eight states and its IDENTITY LIVES IN THE FLOWER AND THE FRUIT, not the leaf — at 32 px/m a
      blueberry leaf is 1 px and a rhodora leaf is 1 px and they are the same 1 px. What tells them
      apart is that one is magenta in late May before it has any leaves at all, and the other is
      scarlet in October. Winterberry is invisible for eleven months and then it is the loudest thing
      in the swamp. So the axis is eight PHASES of the year, and one table derives leaf / bloom /
      fruit / catkin / twig from the phase and the species together, so they can never disagree.
      Two consequences that fall out for free: `bloomFirst` species (rhodora, shadbush) flower on bare
      wood, and `holds` species (winterberry, rose, sumac, juniper) carry fruit through the dormant
      frame — which is the whole reason to draw them.

   2. THE SNOW LINE.  A shrub is short enough for winter to ERASE it. 0.35 m of pack takes lowbush
      blueberry off the map entirely and does nothing at all to a 4 m alder. One number decides four
      things, the way the tide does in the shore rig: how many rows are CLIPPED, whether a snow crust
      sits on the twigs, how much sway is left, and whether the species is legal to place at that
      depth at all (`buried >= 0.95` → do not draw the shrub, draw a drift). Habitat scales it: a
      wind-scoured barren keeps 0.6x the nominal depth, an alder swale drifts 1.35x. Snow CLIPS, it
      never floods — same ruling as the rock rig's `awash` sheets. No tile bakes ground.

   3. A BARE SHRUB IS FILAMENTS, NOT A DITHER.  This is the mechanism the rig exists for. A leafless
      alder is two hundred twigs 1 px wide. Draw them as limbs and you get a chicken-wire fence; drop
      them and the shrub vanishes for four months of the year. So bare twig volume is a MATERIAL —
      M.VEIL — emitted over the whole bundle and resolved as STRANDS THAT FOLLOW THE BRANCHING.
      This used to be an ordered dither: a 4x4 Bayer threshold against a smooth radial density. It was
      right as coverage and wrong as drawing — isolated pixels, no strand the eye could follow, and at
      a glance a grey screen door rather than a bush. A twig bundle is a DIRECTION FIELD: everything
      radiates out and up from the root crown. So the pass builds that field, lays strands across it at
      a fixed pitch, breaks them into finite runs, and inks the strand cores — same duty cycle as the
      dither had, but the pixels are CONNECTED and they run along the branch. Under them sit real
      authored, forking twigs at every growth stage, so the coherent shapes have coherent structure to
      agree with. `report.veilDuty` is the measured duty cycle. Three things the veil must be exempt
      from, and the audit checks all three: the mass floor, the rim, and the KEYLINE — an outline round
      every strand is mush, and that was the first version of this.

   4. A SHRUB IS A HOLLOW BASKET.  A tree crown is a solid cap seen from below; a shrub is open and
      you look INTO it. So foliage masses sit on a SHELL over the stem bundle with the interior left to
      wood and veil, and `report.windows` counts the enclosed sky holes — background regions fully
      surrounded by ink. A shrub with zero windows has failed no matter how nice its edge is.

   5. GRAIN IS RE-AUTHORED FOR THE SCALE, NOT INHERITED.  The tree rig's `broad` leaf cell is
      8.4 x 5.6 px. A sheep-laurel canopy is 20 px across, so that cell is a third of the whole plant.
      Every grain here is re-measured at shrub scale (2.2–6.2 px) against the canopy it has to sit in.

   6. A BERRY INSIDE THE BUSH IS A WASTED PIXEL.  A blueberry is 0.3 px, so fruit is FLECK — single
      pixels, never masses (the shore rig's ruling, same reason). New here: a fleck is only emitted if
      it would be PRESENTED — frontmost at that pixel and within 4 px of the silhouette. Sites that
      fail are dropped, not moved, and `report.presented / tried` says how many. Scattering 26 berries
      through a canopy and having 6 survive to the front is the honest answer; faking it by pushing
      them all to z-max gives a bush with beads glued to the outside. Three things make the survivors
      read as fruit rather than as confetti: per-species BUNCHES (six to an elderberry cyme, one to a
      rose hip), UNEVEN RIPENESS drawn per berry across four ramps so a laggard or two stays green,
      and one key-side tip pixel plus one dark seat pixel — the difference between a sphere and a dot.

   THE THREE RULES still hold and are still measured per sprite:
     1. MASS — RIM_PX=2 leaves MIN_BODY=6 px, so no BODY mass under MIN_R=5 px radius, clamped at the
        emitter. VEIL, WOOD, FLECK and BLOOM are LINEAR/SUB-PIXEL materials: exempt by declaration,
        forbidden a rim, and policed by the rim gate instead.
     2. SILHOUETTE — per-species tooth pitch and amplitude in PIXELS against the local radius, then a
        veil-aware de-speckle (the tree rig's pass would eat the entire veil).
     3. THICKNESS-GATED RIM — rim *= smoothstep(local thickness); too thin to hold a rim, never gets one.

   THICKETS TILE.  Alder, leatherleaf, sweet gale, blueberry and raspberry ship as `wrap` units: every
   emitter is modulo the tile width, so a row of them butt-joins with no seam BY CONSTRUCTION. What is
   measured is `report.crossings` — how many rows actually cross the join. A tile that never crosses
   tiles perfectly and reads as a fence of separate bushes.

   SPEC: PPU 32 (32 px = 1 m) · ¾ from S at 40° (ADR-0006/0022 — same camera as boats/rock/trees/shore)
   · bottom-centre ROOT-CROWN pivot · no AA · binary alpha · sheets ≤ 2048 px/axis.
   PALETTE: cold ambient bounce + ONE warm key. Autumn is not neon.
   NOT IN THIS RIG: bayberry and sweet fern — shorePlantRig owns them as dune/upland units, and a
   species living in two rigs is how two silhouettes happen.

   globalThis.Shrubs:
     SPECIES · HABITATS · PHASES · SNOWS · STAGES · PPU · RIM_PX · MIN_BODY · MIN_R · SNOW_M
     render(key,{variant,phase,frame,snow,stage|size}) -> {w,h,pivot,rgba,masks,report,...}
     packMask(res)  R=key G=rim B=depth A=coverage
     packState(res) R=veil G=bloom/fruit B=snow A=coverage
     phaseOf · snowOf · grainOf · edgeOf · grey · massView · leafView · phaseView · normalView
     sheetSpec · cellOf · sheetAudit · contract
   Runs in the bake sandbox and in the browser. */
(function (root) {
  'use strict';

  const PPU = 32, RIM_PX = 2, MIN_BODY = 6;
  const MIN_R = Math.ceil((MIN_BODY + 2 * RIM_PX) / 2);   // 5 px radius → 10 px body mass
  const SWAY = 4, VARIANTS = 4;

  const ELEV = 40, CE = Math.cos(ELEV * Math.PI / 180), SE = Math.sin(ELEV * Math.PI / 180);
  const KEYLINE = '#101d21';
  // ADR 0031 — the outline is retired from world art (wave 2, with the trees; shore plants piloted
  // it). Gated OFF by default, reachable with `{outline:true}`, mirroring the engine's own
  // `GameConfig.HullKeylineFlood` so the owner keeps a one-flag A/B.
  //
  // ⚠ KEYLINE IS TWO DIFFERENT THINGS IN THIS FILE AND ONLY ONE OF THEM IS AN OUTLINE. The colour
  // is also the mix anchor for the FRUIT SEAT — one dark pixel UNDER a berry, an interior shading
  // term on a painted pixel that never leaves the silhouette. This flag does not touch it, and a
  // sweep for `KEYLINE` must not either: without the seat a 2 px berry reads as a hole punched in
  // the canopy. Same trap as rockIsoRig's wet-rock anchor (outline-interaction-language.md §1.1).
  const KEYLINE_DEFAULT = false;
  const COLD = '#22404e', WARM = '#e8b06a';
  const SNOWC = '#cdd9dc', CRUST = '#e6eef0';

  // ---- the calendar ----------------------------------------------------------
  // Eight stations, not four seasons. `doy` is only for the label — every derived value comes out of
  // the table below, per species, so a bloom-before-leaf shrub cannot end up green in May.
  const PHASES = [
    { key: 'dormant', label: 'DORMANT', when: 'Feb' },
    { key: 'catkin',  label: 'CATKIN',  when: 'early Apr' },
    { key: 'bloom',   label: 'BLOOM',   when: 'late May' },
    { key: 'leaf',    label: 'LEAF',    when: 'Jun' },
    { key: 'green',   label: 'GREEN',   when: 'Jul' },
    { key: 'fruit',   label: 'FRUIT',   when: 'Aug' },
    { key: 'turn',    label: 'TURN',    when: 'early Oct' },
    { key: 'bare',    label: 'BARE',    when: 'Nov' },
  ];
  const PHASE_KEYS = PHASES.map(p => p.key);
  // canopy fullness for a DECIDUOUS shrub, by phase. bloomFirst species override 'bloom' to 0.12:
  // rhodora and shadbush flower on naked wood and that is their whole read.
  const LEAFING = { dormant: 0, catkin: 0.02, bloom: 0.55, leaf: 0.88, green: 1, fruit: 1, turn: 0.94, bare: 0.06 };
  const COLOURING = { dormant: 'winter', catkin: 'winter', bloom: 'fresh', leaf: 'fresh', green: 'green', fruit: 'green', turn: 'turn', bare: 'turn' };

  // ---- habitat ---------------------------------------------------------------
  // snowK: a barren is scoured to bedrock and a swale drifts over its own alders. One nominal depth,
  // five real ones — the same trick as the shore rig's zone base.
  const HABITATS = [
    { key: 'barren', name: 'Blueberry barren',  snowK: 0.60, sub: 'till · bedrock',  note: 'wind-scoured; the snow blows off' },
    { key: 'bog',    name: 'Bog & fen margin',  snowK: 1.05, sub: 'sphagnum · peat',  note: 'level, so it takes what falls' },
    { key: 'swale',  name: 'Alder swale',       snowK: 1.35, sub: 'alluvium · seep',  note: 'the stems catch drift' },
    { key: 'edge',   name: 'Old field edge',    snowK: 1.00, sub: 'loam · fencerow',  note: 'the reference depth' },
    { key: 'woods',  name: 'Forest understorey', snowK: 0.72, sub: 'duff · shade',    note: 'the canopy holds it up' },
  ];
  const habOf = {}; HABITATS.forEach(h => habOf[h.key] = h);

  const SNOW_M = 1.20;                                    // nominal deep-winter pack on the reference habitat
  const SNOWS = [
    { key: 'none',  m: 0.00, label: 'BARE GROUND' },
    { key: 'dust',  m: 0.10, label: 'DUSTING' },
    { key: 'pack',  m: 0.30, label: 'PACK' },
    { key: 'deep',  m: 0.65, label: 'DEEP' },
    { key: 'drift', m: 1.20, label: 'DRIFT' },
  ];
  const SNOW_KEYS = SNOWS.map(s => s.key);

  // ---- vec / colour ----------------------------------------------------------
  const nrm = (v) => { const L = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / L, v[1] / L, v[2] / L]; };
  const LIGHT = { key: nrm([-0.55, -0.66, 0.52]), rim: nrm([0.48, -0.28, -0.83]) };
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const smooth = (e0, e1, x) => { const t = clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); };
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r) => '#' + r.map(v => clamp(Math.round(v), 0, 255).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2r(a), B = h2r(b); return r2h([0, 1, 2].map(i => A[i] + (B[i] - A[i]) * t)); };

  // Six flat bands, one band further apart between shadow and mid than the tree rig's pass-1 ramp —
  // the leaf-cell edges are drawn by a one-band step, so the bands must be far enough apart to show.
  function rampOf(base, opt) {
    const o = opt || {};
    return {
      dp:  mix(mix(base, '#000000', 0.78), COLD, 0.34),
      sh:  mix(mix(base, '#000000', 0.52), COLD, 0.24),
      mid: mix(base, '#000000', 0.21),
      hi:  mix(base, WARM, 0.13),
      key: mix(mix(base, '#ffffff', 0.06), WARM, o.warm == null ? 0.32 : o.warm),
      rim: mix(WARM, '#fff3df', 0.14),
    };
  }
  const SNOWR = { dp: '#5f7382', sh: '#8297a2', mid: '#adc0c7', hi: '#d4e0e3', key: '#f0f6f6', rim: '#fff6e6' };

  // ---- leaf grain, re-measured at shrub scale --------------------------------
  // w x h in PIXELS of one leaf sprig, rot = lattice rotation, jit = site jitter, warp = domain warp,
  // tone = how far a cell's flat tone may wander from its mean, edge = which silhouette it cuts.
  // Compare the tree rig: broad is 8.4 x 5.6 there. Here the biggest cell in the family is 6.2 x 2.4,
  // because the biggest canopy in the family is 40 px across and a cell has to sit INSIDE it.
  const GRAINS = {
    heathSmall: { w: 2.6, h: 2.0, rot: -0.50, jit: 0.95, warp: 1.6, tone: 0.13, edge: 'fine' },
    broadSmall: { w: 4.4, h: 3.4, rot:  0.34, jit: 0.92, warp: 2.0, tone: 0.18, edge: 'lobe',  corner: 1 },
    serrate:    { w: 3.6, h: 2.6, rot:  0.42, jit: 0.94, warp: 1.8, tone: 0.16, edge: 'saw',   corner: 1 },
    pinnate:    { w: 6.2, h: 2.4, rot: -0.34, jit: 0.96, warp: 2.4, tone: 0.19, edge: 'comb' },
    needleScale:{ w: 2.2, h: 3.6, rot:  0.10, jit: 0.90, warp: 1.4, tone: 0.11, edge: 'prick' },
    willowLance:{ w: 5.6, h: 1.8, rot: -0.70, jit: 0.96, warp: 2.2, tone: 0.15, edge: 'lance' },
  };
  // pitch/amp in PIXELS against the LOCAL radius (a shrub canopy mass is 10–16 px, so a tooth
  // normalised against the mean radius would be a third of a pixel and invisible).
  const EDGES = {
    fine:  { pitch: 2.2, amp: 0.95, lobe: [0.10, 0.06] },
    lobe:  { pitch: 5.0, amp: 1.55, lobe: [0.14, 0.08], pitch2: 2.2, amp2: 0.50 },
    saw:   { pitch: 2.6, amp: 1.30, lobe: [0.11, 0.07], pitch2: 1.4, amp2: 0.40 },
    comb:  { pitch: 3.4, amp: 1.70, lobe: [0.09, 0.05] },
    prick: { pitch: 1.9, amp: 1.25, lobe: [0.08, 0.05] },
    lance: { pitch: 4.2, amp: 1.15, lobe: [0.12, 0.07] },
  };
  const grainOf = (sp) => GRAINS[sp.grain] || GRAINS.broadSmall;
  const edgeOf = (sp) => EDGES[grainOf(sp).edge] || EDGES.lobe;
  const grainName = (sp) => sp.grain;

  // ---- species ---------------------------------------------------------------
  // h = TRUE standing height in world px (32 px = 1 m). unit: plant = one individual · clump = a
  // multi-stem stool · mat = a square metre of cover (wraps, tiles).
  // hollow = how open the basket is: how far the foliage shell is held off the stem bundle.
  const SPECIES = [
    { key: 'LowbushBlueberry', name: 'Lowbush Blueberry', latin: 'Vaccinium angustifolium', form: 'mat', hab: 'barren',
      unit: 'mat', w: 46, h: 12, fol: '#4e7a4f', fall: '#a8342c', bark: '#6b4a3a', grain: 'heathSmall',
      stems: 9, sway: 0.5, wrap: true, hollow: 0.14,
      flower: '#e8dcd0', bloomShape: 'bell', blooms: 12, fruit: '#5f7ba8', fruits: 20 },
    { key: 'SheepLaurel', name: 'Sheep Laurel', latin: 'Kalmia angustifolia', form: 'clump', hab: 'barren',
      unit: 'clump', w: 34, h: 26, fol: '#3f6647', winterFol: '#4c5850', fall: '#5f6b48', bark: '#6b5240',
      grain: 'heathSmall', evergreen: true, stems: 7, sway: 0.7, hollow: 0.28,
      flower: '#c9607f', bloomShape: 'whorl', blooms: 7 },
    { key: 'Rhodora', name: 'Rhodora', latin: 'Rhododendron canadense', form: 'plant', hab: 'barren',
      unit: 'plant', w: 40, h: 32, fol: '#5c7a56', fall: '#8a6b3a', bark: '#5f4a3c', grain: 'heathSmall',
      bloomFirst: true, stems: 5, sway: 0.8, hollow: 0.34,
      flower: '#c25a92', bloomShape: 'puff', blooms: 11 },
    { key: 'BlackHuckleberry', name: 'Black Huckleberry', latin: 'Gaylussacia baccata', form: 'clump', hab: 'barren',
      unit: 'clump', w: 36, h: 29, fol: '#557a4a', fall: '#a85a2c', bark: '#6b4f3c', grain: 'heathSmall',
      stems: 8, sway: 0.7, hollow: 0.30,
      flower: '#d9a89c', bloomShape: 'bell', blooms: 8, fruit: '#1e2430', fruits: 18 },
    { key: 'CommonJuniper', name: 'Common Juniper', latin: 'Juniperus communis', form: 'mat', hab: 'barren',
      unit: 'mat', w: 52, h: 20, fol: '#3d5f52', winterFol: '#4a6154', bark: '#6b5a44', grain: 'needleScale',
      evergreen: true, stems: 11, sway: 0.6, wrap: true, hollow: 0.17,
      fruit: '#6b86a8', fruits: 14, holds: true },
    { key: 'Leatherleaf', name: 'Leatherleaf', latin: 'Chamaedaphne calyculata', form: 'thicket', hab: 'bog',
      unit: 'mat', w: 44, h: 27, fol: '#4a6b45', winterFol: '#7a6444', fall: '#8a6b42', bark: '#6b5138',
      grain: 'heathSmall', evergreen: true, stems: 13, sway: 0.8, wrap: true, hollow: 0.25,
      flower: '#e2e2d4', bloomShape: 'bell', blooms: 14 },
    { key: 'SweetGale', name: 'Sweet Gale', latin: 'Myrica gale', form: 'thicket', hab: 'bog',
      unit: 'mat', w: 46, h: 34, fol: '#5c7a5c', fall: '#8a7a4a', bark: '#5f4a3a', grain: 'willowLance',
      stems: 11, sway: 1.0, wrap: true, hollow: 0.30,
      catkin: '#8a6b3a', catkins: 12 },
    { key: 'WinterberryHolly', name: 'Winterberry', latin: 'Ilex verticillata', form: 'plant', hab: 'bog',
      unit: 'plant', w: 44, h: 78, fol: '#3f6b45', fall: '#8a7a3a', bark: '#4f4438', grain: 'broadSmall',
      stems: 5, sway: 1.0, hollow: 0.36,
      flower: '#e2ddd0', bloomShape: 'bell', blooms: 8, fruit: '#b8342c', fruits: 30, holds: true },
    { key: 'SpeckledAlder', name: 'Speckled Alder', latin: 'Alnus incana rugosa', form: 'thicket', hab: 'swale',
      unit: 'mat', w: 62, h: 128, fol: '#4a6b3f', fall: '#7a6b3a', bark: '#5a4c42', grain: 'serrate',
      stems: 9, sway: 1.6, wrap: true, hollow: 0.40,
      catkin: '#6b5230', catkins: 16 },
    { key: 'PussyWillow', name: 'Pussy Willow', latin: 'Salix discolor', form: 'clump', hab: 'swale',
      unit: 'clump', w: 58, h: 108, fol: '#5f7a5a', fall: '#a89450', bark: '#6b5744', grain: 'willowLance',
      stems: 6, sway: 1.8, hollow: 0.40,
      catkin: '#cfc8b8', catkins: 18 },
    { key: 'RedOsierDogwood', name: 'Red Osier Dogwood', latin: 'Cornus sericea', form: 'clump', hab: 'swale',
      unit: 'clump', w: 52, h: 70, fol: '#4a6b52', fall: '#9c3f38', bark: '#6b4a3c', stemWinter: '#a8422e',
      grain: 'broadSmall', stems: 8, sway: 1.3, hollow: 0.42,
      flower: '#e2ded0', bloomShape: 'umbel', blooms: 7, fruit: '#d8dcd8', fruits: 16 },
    { key: 'Meadowsweet', name: 'Meadowsweet', latin: 'Spiraea alba', form: 'clump', hab: 'swale',
      unit: 'clump', w: 34, h: 40, fol: '#5f7a4a', fall: '#a8853a', bark: '#6b5540', grain: 'serrate',
      stems: 9, sway: 1.0, hollow: 0.30,
      flower: '#e8e4d6', bloomShape: 'plume', blooms: 9, bloomAt: 'green' },
    { key: 'Steeplebush', name: 'Steeplebush', latin: 'Spiraea tomentosa', form: 'clump', hab: 'swale',
      unit: 'clump', w: 32, h: 34, fol: '#67784a', fall: '#a87a3a', bark: '#6b5238', grain: 'serrate',
      stems: 8, sway: 0.9, hollow: 0.28,
      flower: '#c96f8a', bloomShape: 'steeple', blooms: 8, bloomAt: 'green' },
    { key: 'Raspberry', name: 'Wild Raspberry', latin: 'Rubus idaeus', form: 'thicket', hab: 'edge',
      unit: 'mat', w: 54, h: 46, fol: '#5c7a52', fall: '#a8663a', bark: '#7a6b5c', stemWinter: '#8a7a92',
      grain: 'pinnate', cane: true, stems: 7, sway: 1.1, wrap: true, hollow: 0.38,
      flower: '#e2e2d4', bloomShape: 'puff', blooms: 6, fruit: '#a83a44', fruits: 16 },
    { key: 'WildRose', name: 'Wild Rose', latin: 'Rosa virginiana', form: 'clump', hab: 'edge',
      unit: 'clump', w: 40, h: 42, fol: '#4a6b4a', fall: '#b8763a', bark: '#6b5240', grain: 'pinnate',
      stems: 7, sway: 0.9, hollow: 0.32,
      flower: '#d97f9c', bloomShape: 'rose', blooms: 5, fruit: '#b8442c', fruits: 11, fruitAt: 'turn', holds: true },
    { key: 'StaghornSumac', name: 'Staghorn Sumac', latin: 'Rhus typhina', form: 'plant', hab: 'edge',
      unit: 'plant', w: 62, h: 110, fol: '#57784a', fall: '#b8342c', bark: '#7a6450', grain: 'pinnate',
      stems: 3, sway: 1.4, hollow: 0.34,
      fruit: '#9c3a30', fruitShape: 'steeple', fruits: 5, holds: true },
    { key: 'Serviceberry', name: 'Serviceberry', latin: 'Amelanchier canadensis', form: 'plant', hab: 'edge',
      unit: 'plant', w: 56, h: 118, fol: '#5c7a5f', fall: '#c2712c', bark: '#6b6155', grain: 'broadSmall',
      bloomFirst: true, stems: 4, sway: 1.5, hollow: 0.42,
      flower: '#eae6da', bloomShape: 'puff', blooms: 13, fruit: '#4a3f5c', fruits: 20 },
    { key: 'BeakedHazelnut', name: 'Beaked Hazelnut', latin: 'Corylus cornuta', form: 'clump', hab: 'woods',
      unit: 'clump', w: 60, h: 92, fol: '#5a7a4f', fall: '#b89440', bark: '#6b5a48', grain: 'serrate',
      stems: 6, sway: 1.4, hollow: 0.38,
      catkin: '#a89058', catkins: 11 },
    { key: 'WildRaisin', name: 'Wild Raisin', latin: 'Viburnum nudum cassinoides', form: 'plant', hab: 'woods',
      unit: 'plant', w: 50, h: 84, fol: '#44664a', fall: '#9c4a52', bark: '#5f5348', grain: 'broadSmall',
      stems: 4, sway: 1.2, hollow: 0.38,
      flower: '#e6e2d2', bloomShape: 'umbel', blooms: 6, fruit: '#4a4f7a', fruits: 28 },
    { key: 'RedElderberry', name: 'Red Elderberry', latin: 'Sambucus racemosa', form: 'plant', hab: 'woods',
      unit: 'plant', w: 56, h: 96, fol: '#5f7a4a', fall: '#8a8a4a', bark: '#7a6b58', grain: 'pinnate',
      stems: 4, sway: 1.3, hollow: 0.40,
      flower: '#e6e2cc', bloomShape: 'puff', blooms: 7, fruit: '#c2352c', fruits: 26 },
  ];
  // HOW FRUIT IS CARRIED is as much a species signature as its colour, and at this scale it is the
  // more legible of the two: winterberry beads tight along the twig, elderberry and wild raisin ride
  // in flat cymes, a rose hip and a raspberry sit alone. Scattering loose single berries at even
  // spacing — which is what the first pass did — gives every species the same speckled read.
  // cluster = berries per bunch · berryR = emitted radius (1.2 → a 5 px plus, 1.0 → one pixel)
  const FRUITING = {
    WinterberryHolly: { cluster: 3, berryR: 1.25 },
    RedElderberry:    { cluster: 6, berryR: 1.20 },
    WildRaisin:       { cluster: 5, berryR: 1.20 },
    RedOsierDogwood:  { cluster: 4, berryR: 1.20 },
    Serviceberry:     { cluster: 4, berryR: 1.15 },
    LowbushBlueberry: { cluster: 3, berryR: 1.05 },
    BlackHuckleberry: { cluster: 3, berryR: 1.05 },
    CommonJuniper:    { cluster: 2, berryR: 1.05 },
    WildRose:         { cluster: 1, berryR: 1.40 },   // a hip is one big fruit, not a bunch
    Raspberry:        { cluster: 1, berryR: 1.25 },
    StaghornSumac:    { cluster: 1, berryR: 1.00 },   // a steeple, not a berry
  };
  const byKey = {}; SPECIES.forEach(s => {
    byKey[s.key] = s; s.worldH = s.h; s.H = habOf[s.hab];
    const f = FRUITING[s.key];
    s.cluster = f ? f.cluster : 3; s.berryR = f ? f.berryR : 1.10;
  });

  const STAGES = { young: 0.42, half: 0.70, full: 1.00 };
  const STAGE_KEYS = ['young', 'half', 'full'];
  function sizeOf(o) {
    if (o && typeof o.size === 'number') return clamp(o.size, 0.2, 1.3);
    if (o && STAGES[o.stage]) return STAGES[o.stage];
    return 1;
  }
  const stageName = (t) => t < 0.55 ? 'young' : t < 0.86 ? 'half' : 'full';

  const hashKey = (k) => { let h = 2166136261; for (let i = 0; i < k.length; i++) { h ^= k.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; };
  const rngOf = (a) => function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };

  // ---- the calendar, resolved for one species --------------------------------
  // ONE place decides. Everything downstream reads this object, so leaf / bloom / fruit / catkin /
  // twig-visibility cannot contradict each other — the failure mode this replaces was a hand-drawn
  // rhodora with magenta flowers on a fully leafed-out bush, which happens in no May anywhere.
  function phaseOf(key, sp) {
    const ph = PHASE_KEYS.indexOf(key) >= 0 ? key : 'green';
    if (!sp) return { key: ph, leaf: 1, bloom: 0, fruit: 0, catkin: 0, veil: 0, colour: 'green' };
    const ever = !!sp.evergreen;
    let leaf = ever ? 1 : LEAFING[ph];
    if (!ever && sp.bloomFirst && ph === 'bloom') leaf = 0.12;      // flowers on naked wood
    let colour = COLOURING[ph];
    if (ever && (colour === 'turn' || colour === 'winter')) colour = 'winter';
    if (ever && colour === 'fresh') colour = 'fresh';

    const bloomAt = sp.bloomAt || 'bloom';
    let bloom = 0;
    if (sp.flower) {
      if (ph === bloomAt) bloom = 1;
      else if (bloomAt === 'bloom' && ph === 'leaf') bloom = 0.32;
      else if (bloomAt === 'green' && (ph === 'fruit' || ph === 'leaf')) bloom = 0.30;
    }
    const fruitAt = sp.fruitAt || 'fruit';
    let fruit = 0;
    if (sp.fruit) {
      if (ph === fruitAt) fruit = 1;
      else if (ph === 'turn') fruit = 0.90;                          // October is peak, not decline
      else if (ph === 'green' && fruitAt === 'fruit') fruit = 0.30;  // green fruit, dull colour
      else if (ph === 'fruit' && fruitAt === 'turn') fruit = 0.28;   // set but unripe — a green hip is still a hip
      if (sp.holds && (ph === 'bare' || ph === 'dormant')) fruit = ph === 'bare' ? 0.96 : 0.62;
    }
    let catkin = 0;
    if (sp.catkin) {
      if (ph === 'catkin') catkin = 1;
      else if (ph === 'dormant') catkin = 0.34;                      // set buds, not open catkins
      else if (ph === 'bloom') catkin = 0.25;
    }
    // twig visibility. An evergreen never shows a haze; a bare deciduous shrub IS the haze.
    const veil = ever ? 0 : clamp(1 - leaf * 0.92, 0, 1);
    return {
      key: ph, leaf, bloom, fruit, catkin, veil, colour, evergreen: ever,
      greenFruit: fruit > 0 && fruit <= 0.31,
      brightStem: !!sp.stemWinter && (ph === 'dormant' || ph === 'bare' || ph === 'catkin'),
      label: (PHASES.find(p => p.key === ph) || PHASES[4]).label,
      when: (PHASES.find(p => p.key === ph) || PHASES[4]).when,
    };
  }

  // ---- the snow line, resolved for one species -------------------------------
  function snowOf(m, sp) {
    const mm = Math.max(0, m == null ? 0 : m);
    if (!sp) return { m: mm, depthM: mm, buried: 0, legal: true, cap: 0 };
    const depthM = mm * sp.H.snowK;
    const hM = sp.worldH / PPU;
    const buried = clamp(depthM / Math.max(0.06, hM), 0, 1.4);
    return {
      m: mm, depthM, buried, gone: buried >= 0.95, legal: buried < 0.95,
      cap: depthM > 0.02 ? clamp(0.22 + 0.78 * smooth(0, 0.45, depthM), 0, 1) : 0,
      stillPct: Math.round(clamp(buried, 0, 1) * 100),
      key: (SNOWS.reduce((a, s) => Math.abs(s.m - mm) < Math.abs(a.m - mm) ? s : a, SNOWS[0])).key,
    };
  }

  // ---- volume buffer ---------------------------------------------------------
  const M = { NONE: 0, BODY: 1, WOOD: 2, VEIL: 3, FLECK: 4, BLOOM: 5, SNOW: 6, EDGE: 7 };
  const MAT_NAME = ['none', 'body', 'wood', 'veil', 'fleck', 'bloom', 'snow', 'edge'];
  const LINEAR = { 2: 1, 3: 1, 4: 1, 5: 1, 7: 1 };        // exempt from the mass floor, forbidden a rim

  function Vol(w, h) {
    const n = w * h;
    this.w = w; this.h = h;
    this.z = new Float32Array(n).fill(-1e9);
    this.nx = new Float32Array(n); this.ny = new Float32Array(n); this.nz = new Float32Array(n);
    this.mat = new Uint8Array(n); this.a = new Uint8Array(n); this.id = new Int16Array(n).fill(-1);
    this.wrap = 0;
  }
  Vol.prototype.clearPx = function (i) { this.a[i] = 0; this.mat[i] = 0; this.z[i] = -1e9; this.id[i] = -1; };
  Vol.prototype.put = function (x, y, z, nx, ny, nz, mat, id) {
    if (y < 0 || y >= this.h) return;
    if (this.wrap) { x = ((x % this.w) + this.w) % this.w; }
    else if (x < 0 || x >= this.w) return;
    const i = y * this.w + x;
    if (z <= this.z[i]) return;
    const L = Math.hypot(nx, ny, nz) || 1;
    this.z[i] = z; this.nx[i] = nx / L; this.ny[i] = ny / L; this.nz[i] = nz / L;
    this.mat[i] = mat; this.a[i] = 1; this.id[i] = id;
  };
  // is this site PRESENTED — would a pixel put here be the frontmost thing at that pixel?
  Vol.prototype.front = function (x, y, z, tol) {
    if (y < 0 || y >= this.h) return false;
    if (this.wrap) x = ((x % this.w) + this.w) % this.w;
    else if (x < 0 || x >= this.w) return false;
    const i = y * this.w + x;
    return !this.a[i] || z >= this.z[i] - (tol == null ? 1.5 : tol);
  };

  // ---- primitives ------------------------------------------------------------
  // toothed, lobed ellipsoid front surface. Teeth are in PIXELS against the local radius and spaced
  // along real arc length, so a wide flat mass is toothed along its long edges, not only at its tips.
  function blob(v, cx, cy, cz, rx, ry, rz, mat, id, seed, E) {
    const e = E || EDGES.lobe;
    const p1 = seed * 1.7, p2 = seed * 3.1, p3 = seed * 5.3;
    const rmax = Math.max(rx, ry) * 1.45;
    const x0 = Math.floor(cx - rx * 1.45), x1 = Math.ceil(cx + rx * 1.45);
    const y0 = Math.max(0, Math.floor(cy - ry * 1.45)), y1 = Math.min(v.h - 1, Math.ceil(cy + ry * 1.45));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      let u = (x + 0.5 - cx) / rx, w = (y + 0.5 - cy) / ry;
      const th = Math.atan2(w, u);
      // low-order lobing: a shrub mass is not a billiard ball
      let k = 1 + e.lobe[0] * Math.sin(3 * th + p1) + e.lobe[1] * Math.sin(5 * th + p2);
      // local radius in px along this ray, then a tooth wave measured in px against IT
      const lr = Math.max(1.2, Math.hypot(Math.cos(th) * rx, Math.sin(th) * ry) * k);
      const arc = th * lr;
      let tooth = (2 / Math.PI) * Math.asin(Math.sin(Math.PI * arc / e.pitch + p3));
      let amp = e.amp;
      if (e.amp2) tooth += (e.amp2 / amp) * (2 / Math.PI) * Math.asin(Math.sin(Math.PI * arc / e.pitch2 + p1));
      k += (tooth * amp) / lr;
      u /= k; w /= k;
      const s = u * u + w * w;
      if (s > 1) continue;
      const t = Math.sqrt(1 - s), z = cz + t * rz;
      v.put(x, y, z, u / rx, w / ry, t / rz, mat, id);
    }
    return rmax;
  }
  // swept sphere: stems, canes, root crown
  function limb(v, x0, y0, z0, x1, y1, z1, r0, r1, mat, id) {
    const dx = x1 - x0, dy = y1 - y0, L2 = dx * dx + dy * dy || 1e-6, R = Math.max(r0, r1) + 1;
    const ax = Math.floor(Math.min(x0, x1) - R), bx = Math.ceil(Math.max(x0, x1) + R);
    const ay = Math.max(0, Math.floor(Math.min(y0, y1) - R)), by = Math.min(v.h - 1, Math.ceil(Math.max(y0, y1) + R));
    for (let y = ay; y <= by; y++) for (let x = ax; x <= bx; x++) {
      let t = ((x + 0.5 - x0) * dx + (y + 0.5 - y0) * dy) / L2; t = clamp(t, 0, 1);
      const px = x0 + dx * t, py = y0 + dy * t, pz = z0 + (z1 - z0) * t, r = Math.max(0.60, r0 + (r1 - r0) * t);
      const ox = x + 0.5 - px, oy = y + 0.5 - py, d2 = ox * ox + oy * oy;
      if (d2 > r * r) continue;
      const k = Math.sqrt(Math.max(0, r * r - d2));
      v.put(x, y, pz + k, ox / r, oy / r * 0.34, Math.max(0.25, k / r), mat, id);
    }
  }

  // ---- THE TWIG VEIL ---------------------------------------------------------
  // Bare twig volume, resolved as FILAMENTS. There is no dither here any more, and that is the point:
  // an ordered threshold against a smooth density is the correct coverage drawn as the wrong thing —
  // scattered single pixels with no strand to follow. What a twig bundle actually is, is a DIRECTION
  // FIELD radiating out and up from the root crown. So: build the field, lay strands across it at a
  // fixed pitch, break each strand into finite runs, ink the cores. Same duty cycle, connected pixels.
  //
  // `flare` makes the bundle a shrub and not a rugby ball: a shrub is narrow at the crown and spreads
  // upward, so the horizontal radius scales with height.
  function veilMass(v, cx, cy, cz, rx, ry, rz, dens, id, seed, stats, flare) {
    const fl = flare == null ? 0.55 : flare;
    // A SMALL BUNDLE CARRIES FEWER TWIGS, NOT FINER ONES. A strand is 1 px wide at every scale — that
    // is the floor of the medium — so a seedling gets a wider pitch and a shorter run instead, which
    // is also the truth of it: countable sticks, not a bundle.
    const small = smooth(15, 7.5, rx);                     // 0 on a full alder bundle, 1 on a seedling
    const pitch = 2.20 + 1.20 * small;                     // px between neighbouring strands
    const runL = 15 - 6.5 * small;                         // px a strand runs before it ends
    const densK = 1 - 0.30 * small;
    // the crown sits under the bundle: every strand leaves from there. A CARPET HAS MANY CROWNS — a
    // blueberry barren is a hundred little stools, not one fan — so a bundle wider than it is tall gets
    // repeated virtual crowns across its width, and the crown is pushed far enough below a flat bundle
    // that its rays still leave the ground going UP. One fan under a mat gives horizontal dashes.
    const flat = clamp(rx / Math.max(1, ry) - 1.15, 0, 3);
    const spanX = flat > 0 ? Math.max(7, ry * 2.2) : 1e9;
    const crownDrop = Math.max(ry * 0.92, flat > 0 ? spanX * 0.85 : 0);
    const crownY = cy + crownDrop;
    const x0 = Math.floor(cx - rx), x1 = Math.ceil(cx + rx);
    const y0 = Math.max(0, Math.floor(cy - ry)), y1 = Math.min(v.h - 1, Math.ceil(cy + ry));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const w = (y + 0.5 - cy) / ry;
      if (w < -1 || w > 1) continue;
      const up = clamp(0.5 - w * 0.5, 0, 1);                // 0 at the bottom of the bundle, 1 at the top
      const rxl = rx * (1 - fl + fl * (0.35 + 0.85 * up));
      const u = (x + 0.5 - cx) / rxl;
      const s = u * u + w * w;
      if (s > 1) continue;
      stats.tried++;

      // 1 — THE FIELD IS POLAR ABOUT THE ROOT CROWN, because that is what a shrub is: no leader, every
      // twig a ray out of one stool. Level sets of the angle are exactly rays from the crown, so a
      // strand is guaranteed CONNECTED — which is the property the dither never had.
      const many = spanX < 1e8;
      const stool = many ? Math.round((x + 0.5 - cx) / spanX) : 0;
      const cxs = cx + (many ? stool * spanX + (vnoise(stool * 5.1 + seed, stool * 2.3, seed + 31) - 0.5) * spanX * 0.45 : 0);
      const ax = x + 0.5 - cxs, ay = crownY - (y + 0.5);
      const rad = Math.hypot(ax, ay * 0.92) + 0.001;
      const th = Math.atan2(ax, Math.max(0.4, ay));            // 0 straight up, ± out to the flanks

      // 2 — STRANDS DOUBLE EVERY TIME THE RADIUS DOUBLES. Rays alone diverge, so the outer bundle
      // would go bald; real twigs answer that by FORKING, and a doubling per radius octave is that,
      // exactly. It also keeps the gap between neighbouring strands inside one octave of `pitch`
      // whatever the size of the bundle — so the same rule reads at 12 px and at 120.
      const gen = Math.floor(Math.log2(Math.max(1, rad / 6)));
      const perRad = Math.pow(2, gen) * 6 / pitch;
      // the wander. Without it the fan is a sunburst; with it the rays bend past each other like wood.
      // Amplitude grows with radius, because a long ray held straight is a reed, not a twig — the tall
      // thicket forms are where that shows.
      const b = th * perRad + (0.85 + rad * 0.030) * snoise(rad / 9 + seed, th * 2.6, seed + 6);
      // ALONG the strand is now simply the radius — monotonic, so a run breaks once and not per pixel
      const a = rad;
      const sid = Math.floor(b);
      // distance from the strand core, measured ACROSS the ray in real pixels
      const off = Math.abs(b - sid - 0.5) * 2 * (rad / Math.max(0.3, perRad));

      // 3 — HOW MUCH TWIG IS OWED HERE. Same density law as the dither used: clumped, dissolving
      // outward. What it buys is different, and this is the whole correction — density no longer buys
      // a THRESHOLD ROLL per pixel (which is how you get speckle), it buys strand COUNT and strand
      // LENGTH. A twig is one pixel wide wherever it exists; thinner than that is not a thinner twig,
      // it is a broken one.
      const cl = 0.66 + 0.50 * snoise(x / 6.5 + seed, y / 6.5, seed + 3);
      const local = clamp(dens * densK * cl * (1 - 0.58 * Math.pow(s, 0.65)), 0, 1.15);

      // 4 — WHOLE STRANDS LIVE OR DIE. Per-strand, so a sparse bundle is FEWER twigs and not a
      // dissolved one. Because `local` falls off outward, a strand thins out toward the silhouette
      // instead of being cut flat: near-solid in the core, countable sticks at the edge.
      if (vnoise(sid * 7.7 + seed, sid * 3.1, seed + 23) > 0.10 + 1.20 * local) continue;

      // 5 — FINITE RUNS. A twig starts and stops; an unbroken strand from crown to silhouette is a
      // wire. Cut on a slow along-strand noise, per strand, so the breaks fall in different places —
      // and let the dense core hold longer runs than the dissolving edge does.
      const run = snoise(a / runL + sid * 4.3, sid * 2.1 + seed, seed + 11);
      if (run < 0.34 - 0.34 * local) continue;
      // 6 — WIDTH. Half a pixel of half-width prints a DOTTED diagonal — the grid takes every second
      // pixel off a 1 px ray — and a dotted line is the speckle this pass exists to get rid of. So the
      // core is a comfortable pixel and a bit, and only where strands crowd do they fuse into mass.
      if (off > 0.72 + 1.00 * Math.max(0, local - 0.52)) continue;
      stats.kept++;
      const t = Math.sqrt(Math.max(0, 1 - s)), z = cz + t * rz * 0.72;
      // a strand is a cylinder lit across its length: normal points off the ray, biased to the key
      const nl = Math.hypot(ax, ay) || 1;
      v.put(x, y, z, (ay / nl) * 0.50 - 0.30, (ax / nl) * 0.50 - 0.42, 0.72, M.VEIL, id);
    }
  }

  // single-pixel detail: berries, buds, catkin beads. Never a mass — a blueberry is 0.3 px.
  function fleck(v, x, y, z, r, mat, id) {
    const R = Math.max(0.66, r);
    const ri = Math.ceil(R);
    for (let dy = -ri; dy <= ri; dy++) for (let dx = -ri; dx <= ri; dx++) {
      if (dx * dx + dy * dy > R * R) continue;
      v.put(Math.round(x) + dx, Math.round(y) + dy, z + 0.9, -0.42, -0.52, 0.74, mat, id);
    }
  }

  // ---- skeleton --------------------------------------------------------------
  // A shrub has NO LEADER. Every form here is a bundle of stems out of a root crown, and the foliage
  // is a SHELL over the bundle held off it by `hollow` — which is what leaves the windows the audit
  // counts. Nothing in this file grows a trunk.
  function build(sp, variant, phase, size, snow) {
    const t = size == null ? 1 : size;
    const rng = rngOf(hashKey(sp.key) + variant * 7717 + hashKey(phase.key) * 13 + Math.round(t * 997));
    const grow = smooth(0.2, 1, t);
    const H = Math.max(8, sp.worldH * t);
    const W = sp.wrap ? sp.w : Math.max(14, sp.w * Math.pow(t, 0.66));
    const cx = sp.wrap ? W / 2 : Math.floor(W / 2) + 0.5;
    const baseY = H + 2;
    const A = () => rng() * Math.PI * 2;
    const stems = [], clumps = [], veils = [], flecks = [], blooms = [];
    const notes = [];
    const nStem = Math.max(2, Math.round(sp.stems * (0.45 + 0.55 * grow)));
    const hollow = sp.hollow;
    // a stem at 32 px/m is 1.5–2 px whatever the shrub's height — scaling thickness off H gave a 4 m
    // alder 3.5 px timbers on a 62 px tile, which read as fence posts.
    const stemR = clamp(H * 0.013 * (0.6 + 0.4 * grow), 0.80, 2.30);

    // one stool: n stems out of a root crown, each carrying its share of the shell. The lean is a
    // fraction of the CANOPY, never of the height: as a fraction of height a 4 m alder threw stems
    // 46 px sideways across a 62 px tile and every unit came out as a big painted X.
    function stool(sx, sz, n, hh, spread, canopyR) {
      const tips = [];
      for (let i = 0; i < n; i++) {
        const th = (i / n) * Math.PI * 2 + (rng() * 2 - 1) * 0.5;
        const reach = canopyR * spread * (0.45 + rng() * 0.55);
        const hi = hh * (0.62 + rng() * 0.44);
        const ex = sx + Math.cos(th) * reach, ez = sz + Math.sin(th) * reach * 0.8;
        const mx = sx + (ex - sx) * 0.22, mz = sz + (ez - sz) * 0.22;   // nearly vertical off the crown
        stems.push([sx, baseY, sz, mx, baseY - hi * 0.52, mz, stemR * 1.30, stemR, M.WOOD]);
        stems.push([mx, baseY - hi * 0.52, mz, ex, baseY - hi, ez, stemR, stemR * 0.52, M.WOOD]);
        tips.push([ex, baseY - hi, ez, th]);
      }
      // the shell: masses on the OUTSIDE of the bundle, held off it by `hollow`
      if (phase.leaf > 0.05) {
        const rr = Math.max(MIN_R, canopyR * (0.30 + 0.10 * rng()));
        // Under the mass floor a ring COLLAPSES: three floored blobs on a 5 px canopy fuse into one
        // lozenge wider than the plant is tall, which is what made every young sprite a green pill.
        // A young shrub is one legible tuft — so emit that, instead of pretending to a canopy.
        if (canopyR < MIN_R * 1.25) {
          clumps.push({ x: sx + (rng() * 2 - 1) * 0.8, y: baseY - hh * 0.72, z: sz, r: MIN_R });
          return tips;
        }
        const ring = Math.max(3, Math.round((2 * Math.PI * canopyR) / (2.15 * rr)));
        for (let i = 0; i < ring; i++) {
          if (rng() < 0.18) continue;                       // deliberate gaps — a shrub is not upholstered
          const a = (i / ring) * Math.PI * 2 + (rng() * 2 - 1) * 0.11;
          const rad = canopyR * (0.78 + hollow * 0.30);
          clumps.push({ x: sx + Math.cos(a) * rad, y: baseY - hh * (0.60 + 0.30 * Math.sin(a * 1.7 + variant)),
            z: sz + Math.sin(a) * rad * 0.8, r: rr * (0.85 + 0.3 * rng()) });
        }
        for (const [ex, ey, ez] of tips) {
          if (rng() < 0.35) continue;
          const rr2 = Math.max(MIN_R, canopyR * 0.26);
          clumps.push({ x: ex, y: ey + rr2 * 0.3, z: ez, r: rr2 });
        }
      }
      return tips;
    }

    switch (sp.form) {

      case 'mat': {   // blueberry, juniper: a carpet. Wider than tall, so the shell is a flattened
        // dome and the stems are 3 px of woody base — all of the read is in the surface and the edge.
        // Under 16 px tall the mass floor is taller than the plant, so FEWER masses, not smaller ones:
        // a young mat is one or two legible domes, never a stack of floored blobs fused into a lozenge.
        const nc = H < 16 ? Math.max(2, Math.round(nStem * 0.55)) : Math.max(4, Math.round(nStem * 1.4));
        // A mat is a WRAP tile, so its domes go on a regular lattice with one sitting ON the seam at
        // x=0, which the modulo emitter wraps for free. Random x across the width left the blueberry
        // barren with rows that touched neither edge: crossings=0, a barren that tiles perfectly and
        // reads as a row of separate cushions. Evergreen mats also get deliberate gaps and tighter
        // domes: a prostrate juniper seen from above shows sky between its sprays, and fused domes
        // gave it zero windows.
        const rk = sp.evergreen ? 0.46 : 0.52;
        for (let i = 0; i < nc; i++) {
          if (i && rng() < 0.16) continue;
          const x = cx + ((i / nc) - 0.5) * W + (rng() * 2 - 1) * (W / nc) * 0.30;
          const z = (rng() * 2 - 1) * W * 0.22;
          const hh = H * (0.62 + rng() * 0.45);
          stems.push([x, baseY, z, x + (rng() * 2 - 1) * 2.5, baseY - hh * 0.7, z, stemR * 1.2, stemR * 0.7, M.WOOD]);
          if (phase.leaf > 0.05) {
            const rr = Math.max(MIN_R, H * rk);
            clumps.push({ x, y: baseY - hh * 0.78, z, r: rr * (0.85 + 0.3 * rng()) });
          }
        }
        notes.push('a mat is a surface and an edge — the stems are 3 px of woody base and nothing else');
        break;
      }

      case 'clump': {   // the default shrub: one multi-stem stool from a root crown
        const canopyR = Math.min(W * 0.42, H * 0.44);
        stool(cx, 0, nStem, H, 0.95 + hollow * 0.9, canopyR);
        // root crown: the one place a shrub thickens, and the reason it survives a fire
        stems.push([cx, baseY, 0, cx, baseY - Math.max(2, H * 0.05), 0, stemR * 2.1, stemR * 1.5, M.WOOD]);
        break;
      }

      case 'plant': {   // one dominant individual: 3–5 main stems, a taller open canopy, visible wood
        const canopyR = Math.min(W * 0.44, H * 0.30);
        const cyc = baseY - H * 0.68;
        for (let i = 0; i < nStem; i++) {
          const th = (i / nStem) * Math.PI * 2 + rng() * 0.6;
          const lean = 0.14 + rng() * 0.16;
          const ex = cx + Math.cos(th) * H * lean, ez = Math.sin(th) * H * lean * 0.8;
          stems.push([cx + Math.cos(th) * 1.5, baseY, Math.sin(th) * 1.2, ex, cyc + H * 0.10, ez,
            stemR * 1.9, stemR * 0.9, M.WOOD]);
          // secondaries aimed into the shell, stopping short of it so the wood shows in the gaps
          for (let k = 0; k < 2; k++) {
            const a2 = th + (k ? 1 : -1) * (0.5 + rng() * 0.5);
            stems.push([ex, cyc + H * 0.10, ez,
              ex + Math.cos(a2) * canopyR * 0.55, cyc - canopyR * (0.1 + rng() * 0.3), ez + Math.sin(a2) * canopyR * 0.45,
              stemR * 0.85, stemR * 0.45, M.WOOD]);
          }
        }
        if (phase.leaf > 0.05) {
          const rr = Math.max(MIN_R, canopyR * 0.34);
          if (canopyR < MIN_R * 1.25) {                    // same collapse rule as the stool
            clumps.push({ x: cx, y: cyc + canopyR * 0.2, z: 0, r: MIN_R });
          } else {
          const ring = Math.max(4, Math.round((2 * Math.PI * canopyR) / (2.05 * rr)));
          for (let i = 0; i < ring; i++) {
            if (rng() < 0.20) continue;
            const a = (i / ring) * Math.PI * 2 + (rng() * 2 - 1) * 0.1;
            clumps.push({ x: cx + Math.cos(a) * canopyR * (0.82 + hollow * 0.26),
              y: cyc + Math.sin(a) * canopyR * 0.62 * (1 - hollow * 0.3),
              z: Math.sin(a) * canopyR * 0.7, r: rr * (0.85 + 0.3 * rng()) });
          }
          // two masses hung under the branch line, one held back on each flank
          for (let i = 0; i < 2; i++) {
            clumps.push({ x: cx + (i ? 1 : -1) * canopyR * 0.5, y: cyc + canopyR * 0.78,
              z: -canopyR * 0.3, r: Math.max(MIN_R, rr * 0.82) });
          }
          }
        }
        break;
      }

      case 'thicket': {   // alder, leatherleaf, sweet gale, raspberry: a WRAPPING TILE of stools.
        // Every emitter is modulo the tile width, so a row butt-joins with no seam by construction.
        const stools = Math.max(2, Math.round((sp.cane ? 3 : 3) * (0.55 + 0.6 * grow)));
        const canopyR = Math.min(W * 0.30, H * 0.34);
        for (let s = 0; s < stools; s++) {
          // stools start AT the seam, not half a step in: with stool 0 at x=0 the modulo emitter puts
          // half of it on each side of the join, which is what makes a thicket cross. Half-step
          // centring left a young leatherleaf sitting entirely inside its own tile.
          const sx = (s + (rng() * 2 - 1) * 0.22) * (W / stools);
          const sz = (rng() * 2 - 1) * W * 0.16;
          const hh = H * (0.72 + rng() * 0.36);
          if (sp.cane) {
            // a cane arches over and comes back DOWN — that is the whole silhouette of a raspberry
            const nc = Math.max(2, Math.round(nStem / stools) + 1);
            for (let i = 0; i < nc; i++) {
              const th = A(), reach = W * (0.18 + rng() * 0.16);
              const apex = hh * (0.72 + rng() * 0.3);
              const mx = sx + Math.cos(th) * reach * 0.7, mz = sz + Math.sin(th) * reach * 0.6;
              const tx = sx + Math.cos(th) * reach * 1.25, tz = sz + Math.sin(th) * reach;
              stems.push([sx, baseY, sz, mx, baseY - apex, mz, stemR * 1.2, stemR * 0.9, M.WOOD]);
              stems.push([mx, baseY - apex, mz, tx, baseY - apex * 0.35, tz, stemR * 0.9, stemR * 0.5, M.WOOD]);
              if (phase.leaf > 0.05) {
                const rr = Math.max(MIN_R, canopyR * 0.30);
                for (const f of [0.35, 0.7, 1.0]) {
                  if (rng() < 0.22) continue;
                  clumps.push({ x: sx + (mx - sx) * f + (rng() * 2 - 1) * 2,
                    y: baseY - apex * (0.45 + f * 0.55), z: sz + (mz - sz) * f, r: rr * (0.85 + 0.3 * rng()) });
                }
              }
            }
          } else {
            stool(sx, sz, Math.max(2, Math.round(nStem / stools) + 1), hh, 0.70 + hollow * 0.8, canopyR);
          }
        }
        notes.push('wrap tile: every emitter is modulo ' + Math.round(W) + ' px, so the join is seamless by construction');
        break;
      }
    }

    // ---- the twig haze. Sized to the STEM BUNDLE, not the canopy: a leafless shrub is exactly as
    // wide as its twigs reach, which is narrower than its summer silhouette. That is why the
    // hand-drawn winter alder was wrong — it was the summer outline emptied out.
    // Growth stage thins the haze as well as shrinking it: a young shrub has a fraction of the twig
    // count, so holding density constant made every seedling a grey smudge with a plant inside it.
    // The curve is deliberately steep — a young plant should read as STICKS, and the authored twigs
    // below take over the structural job the filaments give up.
    // A MAT IS DIFFERENT. A carpet does not thicken with age so much as spread, and when it is bare the
    // filaments ARE the whole plant — thinned on the steep curve, a young dormant blueberry barren faded to
    // nothing and stopped crossing its own tile join. So mats keep a high floor and only the upright
    // forms, where the smothering was actually objectionable, get the steep one.
    const veilK = sp.form === 'mat' ? 0.62 + 0.38 * Math.pow(grow, 1.2) : 0.30 + 0.70 * Math.pow(grow, 1.4);
    if (phase.veil > 0.02) {
      // A mat's veil OVERRUNS its tile on purpose: the modulo emitter wraps the overflow, so the haze
      // reaches every column. Fitted inside the width instead, a bare blueberry barren left the seam
      // columns empty — crossings=0 on the one form whose whole job is to carpet.
      const vr = (sp.form === 'mat') ? W * 0.54 : (sp.form === 'thicket' ? W * 0.34 : W * 0.30);
      const vh = H * (sp.form === 'mat' ? 0.50 : 0.46);
      const vy = baseY - H * (sp.form === 'mat' ? 0.42 : 0.52);
      const n = sp.form === 'thicket' ? 3 : 1;
      for (let i = 0; i < n; i++) {
        const ox = n > 1 ? (i + 0.5) * (W / n) - cx : 0;
        // A CARPET IS FLAT. Tying the veil's depth radius to its (deliberately overrun) width gave the
        // blueberry mat a 60 px tall cell for a 0.37 m plant — all of it empty atlas, because in ¾ view
        // depth buys height. A mat's haze is as deep as the mat, not as wide as the tile.
        veils.push({ x: cx + ox, y: vy, z: 0, rx: vr / (n > 1 ? 1.55 : 1), ry: vh,
          rz: vr * (sp.form === 'mat' ? 0.22 : 0.55),
          seed: i * 13 + variant * 7,
          flare: sp.form === 'mat' ? 0.20 : 0.62,
          dens: (0.34 + 0.36 * phase.veil) * (sp.form === 'mat' ? 1.12 : 1) * veilK });
      }
    }

    // WHAT THE FILAMENTS AGREE WITH: real, authored, FORKING twigs — at every growth stage, not just
    // the young ones. The strand field gives coherent shapes; this gives them something coherent to be
    // shapes OF. A first-order twig off a stem tip, then two branchlets off that: the smallest unit
    // that reads as branching rather than as spokes, and the thing the eye actually follows when it
    // decides a winter bush is a bush. Young plants still get more of them relative to their haze,
    // which is the truth of it — a seedling IS a handful of countable sticks.
    if (phase.veil > 0.25 && stems.length) {
      const base = stems.length, nT = 3 + Math.round((1 - grow) * 9 + grow * 7);
      const tw = Math.max(0.60, stemR * 0.52);
      let drawn = 0;
      for (let i = 0; i < nT; i++) {
        const s0 = stems[Math.floor(rng() * base)];
        const bx = s0[3], by = s0[4], bz = s0[5];
        // out and UP, always: a twig that heads sideways off a stem tip reads as a snapped branch
        const a = A(), len = H * (0.08 + rng() * 0.14);
        const ex = bx + Math.cos(a) * len * 0.52, ey = by - len * (0.52 + rng() * 0.60),
          ez = bz + Math.sin(a) * len * 0.40;
        stems.push([bx, by, bz, ex, ey, ez, tw, 0.60, M.WOOD]); drawn++;
        if (rng() < 0.72) {
          // the fork. Both branchlets leave the SAME node and keep the parent's general heading, which
          // is what makes it read as one twig branching instead of two twigs crossing.
          const nf = rng() < 0.35 ? 1 : 2, fl2 = len * (0.34 + rng() * 0.30);
          for (let k = 0; k < nf; k++) {
            const fa = a + (k ? 1 : -1) * (0.45 + rng() * 0.55);
            stems.push([ex, ey, ez,
              ex + Math.cos(fa) * fl2 * 0.62, ey - fl2 * (0.55 + rng() * 0.50), ez + Math.sin(fa) * fl2 * 0.42,
              0.60, 0.60, M.WOOD]); drawn++;
          }
        }
      }
      notes.push(drawn + ' authored twigs and branchlets carry the structure the filaments describe');
    }

    const oneSite = () => {
      const src = clumps.length ? clumps[Math.floor(rng() * clumps.length)] : null;
      if (src) {
        const a = A();
        return { x: src.x + Math.cos(a) * src.r * 0.9, y: src.y + Math.sin(a) * src.r * 0.6,
          z: src.z + src.r * (0.4 + rng() * 0.7) };
      }
      const a = A(), rad = (sp.wrap ? W * 0.42 : W * 0.32) * Math.sqrt(rng());
      return { x: cx + Math.cos(a) * rad, y: baseY - H * (0.35 + rng() * 0.55), z: Math.sin(a) * rad * 0.7 + 2 };
    };
    // Fruit and flowers are sited FRONT-BIASED — best of six candidates, scored on depth AND how far
    // out on the flank the site sits, because in ¾ view the flanks present as well as the front does.
    // This is the honest way to raise the presented count: fruit really does hang on the lit outside
    // of a bush, so bias the SEARCH and still drop whatever loses the depth test at emit. Nudging a
    // failed berry forward instead is what gives you beads glued to the silhouette.
    const shellSite = (front) => {
      if (!front) return oneSite();
      let best = null, bs = -1e9;
      for (let k = 0; k < 6; k++) {
        const c = oneSite(), sc = c.z + Math.abs(c.x - cx) * 0.34;
        if (sc > bs) { bs = sc; best = c; }
      }
      return best;
    };
    if (phase.bloom > 0.04 && sp.flower) {
      const n = Math.max(1, Math.round((sp.blooms || 7) * phase.bloom * (0.5 + 0.5 * grow)));
      for (let i = 0; i < n; i++) blooms.push(Object.assign(shellSite(true), { shape: sp.bloomShape || 'puff', seed: rng() }));
    }
    if (phase.catkin > 0.04 && sp.catkin) {
      const n = Math.max(1, Math.round((sp.catkins || 10) * phase.catkin * (0.5 + 0.5 * grow)));
      for (let i = 0; i < n; i++) blooms.push(Object.assign(shellSite(true), { shape: 'catkin', open: phase.catkin > 0.6, seed: rng() }));
    }
    if (phase.fruit > 0.04 && sp.fruit) {
      const per = Math.max(1, sp.cluster);
      const n = Math.max(1, Math.round((sp.fruits || 12) * Math.min(1.15, phase.fruit + 0.34) * (0.58 + 0.42 * grow)));
      const groups = Math.max(1, Math.round(n / per));
      // RIPENESS IS PER BERRY. A real bunch carries one or two laggards, and that unevenness is most
      // of what separates fruit from confetti — a cluster of identical dots reads as a decal.
      const ripeBase = phase.greenFruit ? 0.14 : clamp(0.52 + 0.48 * phase.fruit, 0, 1);
      for (let gi = 0; gi < groups; gi++) {
        const site = shellSite(true);
        const cnt = per === 1 ? 1 : Math.max(2, Math.round(per * (0.6 + rng() * 0.7)));
        const spread = per === 1 ? 0 : 1.3 + per * 0.17;
        const cRipe = clamp(ripeBase + (rng() * 2 - 1) * 0.16, 0, 1);
        for (let k = 0; k < cnt; k++) {
          // a bunch hangs DOWN and slightly out from its node, so it reads as weight on a twig
          flecks.push({ x: site.x + (rng() * 2 - 1) * spread,
            y: site.y + (k ? rng() * spread * 1.2 : 0),
            z: site.z + (rng() * 2 - 1) * 0.8 + 0.7,
            ripe: clamp(cRipe - (k === 0 ? 0 : rng() * 0.42), 0, 1),
            shape: sp.fruitShape || 'berry', lead: k === 0, seed: rng() });
        }
      }
    }

    return { W, H, cx, baseY, stems, clumps, veils, flecks, blooms, notes, rng, veilK };
  }

  // ---- camera ----------------------------------------------------------------
  const pyRel = (g, y, z) => -(g.baseY - y) * CE + z * SE;
  const pzOf = (g, y, z) => z * CE + (g.baseY - y) * SE;
  const WOBBLE = 1.30;

  function extents(g, sp) {
    let top = 1e9, bot = -1e9, xl = 1e9, xr = -1e9;
    const acc = (x, y, r) => {
      if (y - r < top) top = y - r; if (y + r > bot) bot = y + r;
      if (x - r < xl) xl = x - r; if (x + r > xr) xr = x + r;
    };
    for (const c of g.clumps) {
      const r = Math.max(MIN_R, c.r);
      acc(c.x, pyRel(g, c.y, c.z), Math.max(r, Math.hypot(r * CE, r * SE)) * WOBBLE);
    }
    for (const L of g.stems) {
      const r = Math.max(L[6], L[7]) + 1.2;
      acc(L[0], pyRel(g, L[1], L[2]), r); acc(L[3], pyRel(g, L[4], L[5]), r);
    }
    // A VEIL IS NOT ROUND. Measuring it with one radius — the larger of its two — gave the blueberry
    // mat a 60 px tall cell for a 0.37 m plant: the haze is deliberately WIDER than its tile and only
    // a few pixels deep, and in ¾ view an over-measured depth buys height. All of it was empty atlas.
    for (const v of g.veils) {
      const vy = pyRel(g, v.y, v.z), ry = Math.hypot(v.ry * CE, v.rz * SE) + 1, rx = v.rx + 1;
      if (vy - ry < top) top = vy - ry; if (vy + ry > bot) bot = vy + ry;
      if (v.x - rx < xl) xl = v.x - rx; if (v.x + rx > xr) xr = v.x + rx;
    }
    for (const f of g.flecks) acc(f.x, pyRel(g, f.y, f.z), 2.4);
    for (const b of g.blooms) acc(b.x, pyRel(g, b.y, b.z), 4.6);
    if (top > bot) { top = -1; bot = 1; xl = 0; xr = 1; }
    return { top, bot, xl, xr };
  }

  // ONE union cell and ONE ground-contact pivot per species per stage, unioned over 4 variants x 8
  // PHASES. The phases are in the union on purpose: a bloom-first shrub is taller in flower than in
  // leaf, and a runtime phase swap must not shift the plant on the ground. Snow is NOT in the union —
  // snow only ever clips, never grows, so it cannot change the cell.
  function cellOf(sp, size) {
    const t = size == null ? 1 : size, ck = 'c' + Math.round(t * 1000);
    if (!sp._cells) sp._cells = {};
    if (sp._cells[ck]) return sp._cells[ck];
    const MG = 3;
    let top = 1e9, bot = -1e9, xl = 1e9, xr = -1e9;
    for (const ph of PHASE_KEYS) for (let v = 0; v < VARIANTS; v++) {
      const e = extents(build(sp, v, phaseOf(ph, sp), t, snowOf(0, sp)), sp);
      if (e.top < top) top = e.top;
      if (e.bot > bot) bot = e.bot;
      if (e.xl < xl) xl = e.xl;
      if (e.xr > xr) xr = e.xr;
    }
    const pivotY = Math.ceil(MG - top);
    const h = Math.ceil(pivotY + bot + MG) + 1;
    let cell;
    if (sp.wrap) {
      // a tile is EXACTLY its declared width with no margin, or the margins would show as gutters
      const w = Math.round(sp.w);
      cell = { w, h, dx: 0, pivotX: Math.round(w / 2), pivotY, pad: h - 1 - pivotY, size: t, wrap: true };
    } else {
      const dx = Math.max(0, Math.ceil(MG - xl));
      const w = Math.max(18, dx + Math.ceil(xr + MG) + 1);
      cell = { w, h, dx, pivotX: Math.round(dx + (xl + xr) / 2), pivotY, pad: h - 1 - pivotY, size: t, wrap: false };
    }
    sp._cells[ck] = cell;
    return cell;
  }

  // ---- RULE 2: de-speckle, VEIL-AWARE ----------------------------------------
  // The tree rig kills any pixel with <=1 of 8 neighbours. Run that here and the entire twig veil
  // disappears — a strand tip and a 1 px twig are single pixels BY DESIGN. So VEIL and FLECK are spared, and the
  // pinhole fill refuses to fill a hole whose neighbours are veil, or the twig field silts up solid.
  function despeckle(v) {
    let removed = 0, spared = 0;
    for (let pass = 0; pass < 2; pass++) {
      const kill = [];
      for (let y = 0; y < v.h; y++) for (let x = 0; x < v.w; x++) {
        const i = y * v.w + x; if (!v.a[i]) continue;
        if (v.mat[i] === M.VEIL || v.mat[i] === M.FLECK) { if (!pass) spared++; continue; }
        let n = 0;
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
          if (!dx && !dy) continue;
          const jx = v.wrap ? (((x + dx) % v.w) + v.w) % v.w : x + dx, jy = y + dy;
          if (jx < 0 || jy < 0 || jx >= v.w || jy >= v.h) continue;
          const j = jy * v.w + jx;
          if (v.a[j] && v.mat[j] !== M.VEIL) n++;
        }
        if (n <= 1) kill.push(i);
      }
      for (const i of kill) { v.clearPx(i); removed++; }
      if (!kill.length) break;
    }
    for (let y = 1; y < v.h - 1; y++) for (let x = 1; x < v.w - 1; x++) {
      const i = y * v.w + x; if (v.a[i]) continue;
      const l = i - 1, r = i + 1, u = i - v.w, d = i + v.w;
      if (!(v.a[l] && v.a[r] && v.a[u] && v.a[d])) continue;
      if (v.mat[l] === M.VEIL || v.mat[r] === M.VEIL || v.mat[u] === M.VEIL || v.mat[d] === M.VEIL) continue;
      const src = v.z[l] > v.z[r] ? l : r;
      v.a[i] = 1; v.mat[i] = v.mat[src]; v.z[i] = v.z[src] - 0.2; v.id[i] = v.id[src];
      v.nx[i] = v.nx[src]; v.ny[i] = v.ny[src]; v.nz[i] = v.nz[src];
    }
    return { removed, spared };
  }

  // Chamfer distance field. WRAP MATTERS HERE: on a tile species the columns at x=0 and x=w-1 are not
  // edges, they are the join. Left unwrapped, D falls to 0 there, the rim gate opens, and every tile
  // seam gets a bright vertical stripe down it — which is exactly what the first thicket bake did.
  // RULE 1, RESOLVED ON WHAT IS ACTUALLY VISIBLE. The floor is clamped at the emitter, but the depth
  // buffer still gets a say: a mass that ends up mostly behind a stem or another mass survives as a
  // thin crescent, and a canopy's outermost leaves survive as a 2 px fringe. Clamping harder fixes
  // neither — only looking after z-resolution can. And the two want opposite treatment, because rule 1
  // is a statement about INTERIOR BODY: can this shape hold a 2 px rim and still show 6 px of interior?
  // A fringe is not trying to. So a fringe is not a failed mass, it is an EDGE — same foliage ramp,
  // reclassified linear, so the rim gate governs it instead of the floor. Only true specks are deleted.
  // This is the difference between an audit that measures what it claims to and one that looks away.
  function resolveThinBody(v, D) {
    const w = v.w, h = v.h, n = w * h, lab = new Int32Array(n).fill(-1), stack = [], comps = [];
    for (let s = 0; s < n; s++) {
      if (!v.a[s] || lab[s] >= 0) continue;
      const id = comps.length, cells = [];
      let px = 0, maxd = 0, body = 0, lin = 0;
      lab[s] = id; stack.push(s);
      const push = (j) => { if (v.a[j] && lab[j] < 0) { lab[j] = id; stack.push(j); } };
      while (stack.length) {
        const i = stack.pop(); px++; cells.push(i);
        if (D[i] > maxd) maxd = D[i];
        if (v.mat[i] === M.BODY) body++; else lin++;
        const x = i % w, y = (i / w) | 0;
        if (x > 0) push(i - 1); else if (v.wrap) push(i + w - 1);
        if (x < w - 1) push(i + 1); else if (v.wrap) push(i - w + 1);
        if (y > 0) push(i - w);
        if (y < h - 1) push(i + w);
      }
      comps.push({ px, maxd, body, lin, cells });
    }
    if (!comps.length) return { culled: 0, edged: 0 };
    let big = 0;
    for (let i = 1; i < comps.length; i++) if (comps[i].px > comps[big].px) big = i;
    let culled = 0, edged = 0;
    for (let i = 0; i < comps.length; i++) {
      const c = comps[i];
      if (c.body <= c.lin) continue;                       // linear-dominant: not this rule's business
      if ((c.maxd - RIM_PX) * 2 >= MIN_BODY) continue;     // a real mass, and it clears the floor
      if (i !== big && c.px < 24) {                        // an occluded crescent nobody would miss
        for (const j of c.cells) v.clearPx(j);
        culled++;
        continue;
      }
      for (const j of c.cells) if (v.mat[j] === M.BODY) { v.mat[j] = M.EDGE; edged++; }
    }
    return { culled, edged };
  }

  function distField(a, w, h, wrap) {
    const D = new Float32Array(w * h), BIG = 1e6;
    for (let i = 0; i < w * h; i++) D[i] = a[i] ? BIG : 0;
    const XL = (x) => wrap ? (x - 1 + w) % w : x - 1;
    const XR = (x) => wrap ? (x + 1) % w : x + 1;
    const passes = wrap ? 2 : 1;                            // an extra sweep so distance can travel round
    for (let p = 0; p < passes; p++) {
      for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = y * w + x; if (!D[i]) continue; let d = D[i];
        const xl = XL(x), xr = XR(x);
        if (y > 0) { d = Math.min(d, D[i - w] + 3); if (xl >= 0) d = Math.min(d, D[y * w - w + xl] + 4); if (xr < w) d = Math.min(d, D[y * w - w + xr] + 4); }
        if (xl >= 0) d = Math.min(d, D[y * w + xl] + 3);
        D[i] = d;
      }
      for (let y = h - 1; y >= 0; y--) for (let x = w - 1; x >= 0; x--) {
        const i = y * w + x; if (!D[i]) continue; let d = D[i];
        const xl = XL(x), xr = XR(x);
        if (y < h - 1) { d = Math.min(d, D[i + w] + 3); if (xl >= 0) d = Math.min(d, D[y * w + w + xl] + 4); if (xr < w) d = Math.min(d, D[y * w + w + xr] + 4); }
        if (xr < w) d = Math.min(d, D[y * w + xr] + 3);
        D[i] = d;
      }
    }
    for (let i = 0; i < w * h; i++) D[i] /= 3;
    return D;
  }

  // ---- RULE 1 audit + the window count (rule 4) -------------------------------
  // Both of these must use the SAME connectivity as resolveThinBody, and on a tile that means WRAPPING.
  // Measured without it, a component that continues across the seam is scored as two — the far piece
  // thin and failing — so every thicket in the family reported a mass failure that does not exist.
  function massReport(a, mat, D, w, h, wrap) {
    const lab = new Int32Array(w * h).fill(-1), stack = [], comps = [];
    for (let s = 0; s < w * h; s++) {
      if (!a[s] || lab[s] >= 0) continue;
      const id = comps.length; let px = 0, maxd = 0, body = 0, lin = 0;
      lab[s] = id; stack.push(s);
      const push = (j) => { if (a[j] && lab[j] < 0) { lab[j] = id; stack.push(j); } };
      while (stack.length) {
        const i = stack.pop(); px++; if (D[i] > maxd) maxd = D[i];
        if (mat[i] === M.BODY) body++; else lin++;
        const x = i % w, y = (i / w) | 0;
        if (x > 0) push(i - 1); else if (wrap) push(i + w - 1);
        if (x < w - 1) push(i + 1); else if (wrap) push(i - w + 1);
        if (y > 0) push(i - w);
        if (y < h - 1) push(i + w);
      }
      comps.push({ id, px, maxd, bodyDepth: Math.max(0, (maxd - RIM_PX) * 2), linear: lin >= body });
    }
    const real = comps.filter(c => c.px >= 6);
    const bodyComps = real.filter(c => !c.linear);
    const fail = bodyComps.filter(c => c.bodyDepth < MIN_BODY);
    let bodyPx = 0, total = 0;
    for (let i = 0; i < w * h; i++) if (a[i]) { total++; if (D[i] > RIM_PX) bodyPx++; }
    return {
      masses: real.length, bodyMasses: bodyComps.length, linearMasses: real.length - bodyComps.length,
      failed: fail.length, pass: fail.length === 0,
      minBody: bodyComps.length ? Math.round(Math.min.apply(null, bodyComps.map(c => c.bodyDepth)) * 10) / 10 : 0,
      bodyRatio: total ? Math.round(bodyPx / total * 100) : 0,
    };
  }
  // RULE 4: sky you can see through the basket. Background regions with no path to the border.
  // A shrub with zero windows has failed no matter how good its edge is.
  function windowReport(a, w, h, wrap) {
    const seen = new Uint8Array(w * h), stack = [];
    for (let x = 0; x < w; x++) { for (const y of [0, h - 1]) { const i = y * w + x; if (!a[i] && !seen[i]) { seen[i] = 1; stack.push(i); } } }
    // on a tile the side columns are the JOIN, not open sky: flooding from them would count every
    // enclosed hole that happens to touch a seam as background.
    if (!wrap) for (let y = 0; y < h; y++) { for (const x of [0, w - 1]) { const i = y * w + x; if (!a[i] && !seen[i]) { seen[i] = 1; stack.push(i); } } }
    const step = (i, x, y) => {
      if (x > 0) { const j = i - 1; if (!a[j] && !seen[j]) { seen[j] = 1; stack.push(j); } }
      else if (wrap) { const j = i + w - 1; if (!a[j] && !seen[j]) { seen[j] = 1; stack.push(j); } }
      if (x < w - 1) { const j = i + 1; if (!a[j] && !seen[j]) { seen[j] = 1; stack.push(j); } }
      else if (wrap) { const j = i - w + 1; if (!a[j] && !seen[j]) { seen[j] = 1; stack.push(j); } }
      if (y > 0) { const j = i - w; if (!a[j] && !seen[j]) { seen[j] = 1; stack.push(j); } }
      if (y < h - 1) { const j = i + w; if (!a[j] && !seen[j]) { seen[j] = 1; stack.push(j); } }
    };
    while (stack.length) { const i = stack.pop(); step(i, i % w, (i / w) | 0); }
    const lab = new Int32Array(w * h).fill(-1);
    let n = 0, px = 0;
    for (let s = 0; s < w * h; s++) {
      if (a[s] || seen[s] || lab[s] >= 0) continue;
      let sz = 0; const id = n; lab[s] = id; stack.push(s);
      const push = (j) => { if (!a[j] && !seen[j] && lab[j] < 0) { lab[j] = id; stack.push(j); } };
      while (stack.length) {
        const i = stack.pop(); sz++;
        const x = i % w, y = (i / w) | 0;
        if (x > 0) push(i - 1); else if (wrap) push(i + w - 1);
        if (x < w - 1) push(i + 1); else if (wrap) push(i - w + 1);
        if (y > 0) push(i - w);
        if (y < h - 1) push(i + w);
      }
      if (sz >= 2) { n++; px += sz; } else lab[s] = -1;
    }
    return { windows: n, windowPx: px, enclosed: lab };
  }

  // ---- shade ----------------------------------------------------------------
  const STEPS = [[0.07, 'dp'], [0.19, 'sh'], [0.38, 'mid'], [0.64, 'hi'], [1.4, 'key']];
  const BANDI = { dp: 0, sh: 1, mid: 2, hi: 3, key: 4 };
  const BANDN = ['dp', 'sh', 'mid', 'hi', 'key'];
  function bandOf(l) { for (let i = 0; i < STEPS.length; i++) if (l < STEPS[i][0]) return STEPS[i][1]; return 'key'; }
  function vnoise(x, y, s) { const n = Math.sin(x * 127.1 + y * 311.7 + s * 74.7) * 43758.5453; return n - Math.floor(n); }
  function snoise(x, y, s) {
    const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi;
    const u = fx * fx * (3 - 2 * fx), vv = fy * fy * (3 - 2 * fy);
    const a = vnoise(xi, yi, s), b = vnoise(xi + 1, yi, s), c = vnoise(xi, yi + 1, s), d = vnoise(xi + 1, yi + 1, s);
    return a + (b - a) * u + (c - a) * vv + (a - b - c + d) * u * vv;
  }

  // one place decides a species' foliage colour for a phase.
  function folColour(sp, ph) {
    let base = sp.fol;
    if (ph.colour === 'turn' && sp.fall) base = mix(base, sp.fall, 0.86);
    else if (ph.colour === 'winter') base = sp.winterFol ? mix(base, sp.winterFol, 0.90) : mix(base, '#3a5a5c', 0.28);
    else if (ph.colour === 'fresh') base = mix(base, '#a8c46a', 0.20);      // new growth is yellower
    return base;
  }
  function stemColour(sp, ph) {
    if (ph.brightStem && sp.stemWinter) return sp.stemWinter;
    return mix(sp.bark, COLD, 0.16);
  }

  // ---- LEAF CELLS ------------------------------------------------------------
  // Partition the foliage surface into jittered lattice cells clipped to their own mass, shade each
  // FLAT from its own mean, then step the lower-right border down one band. The lattice takes its
  // span AND its origin from the same rotated frame — the pass-2 tree bug was taking them from
  // different corners, which dumped a whole corner of every sprite into cell 0.
  function leafCells(v, g, seed) {
    const w = v.w, h = v.h, cell = new Int32Array(w * h).fill(-1);
    const co = Math.cos(g.rot), si = Math.sin(g.rot);
    const map = new Map();
    let next = 0;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x;
      if (!v.a[i] || v.mat[i] !== M.BODY) continue;
      const wx = x + (snoise(x / 6.5, y / 6.5, seed) - 0.5) * g.warp;
      const wy = y + (snoise(x / 6.5 + 3.3, y / 6.5 + 1.7, seed + 5) - 0.5) * g.warp;
      const u = (wx * co + wy * si) / g.w, vv = (-wx * si + wy * co) / g.h;
      const bu = Math.floor(u), bv = Math.floor(vv);
      let best = 1e9, bi = bu, bj = bv;
      for (let j = bv - 1; j <= bv + 1; j++) for (let k = bu - 1; k <= bu + 1; k++) {
        const jx = k + 0.5 + (vnoise(k, j, seed) - 0.5) * g.jit;
        const jy = j + 0.5 + (vnoise(k, j, seed + 11) - 0.5) * g.jit;
        const d = (u - jx) * (u - jx) + (vv - jy) * (vv - jy);
        if (d < best) { best = d; bi = k; bj = j; }
      }
      const key = ((bi + 4096) * 8192 + (bj + 4096)) * 512 + (v.id[i] & 511);
      let id = map.get(key);
      if (id === undefined) { id = next++; map.set(key, id); }
      cell[i] = id;
    }
    return { cell, count: next };
  }

  function shade(v, sp, ph, sn, D, o, snowRow, presented) {
    const w = v.w, h = v.h, n = w * h;
    const FOL = rampOf(folColour(sp, ph));
    const WOODR = rampOf(stemColour(sp, ph), { warm: ph.brightStem ? 0.20 : 0.40 });
    const VEILR = rampOf(mix(sp.bark, COLD, 0.34), { warm: 0.26 });
    const BLM = rampOf(sp.flower || '#e2ded0', { warm: 0.22 });
    const CAT = rampOf(sp.catkin || '#a89058', { warm: 0.24 });
    const FRT = rampOf(sp.fruit || '#b8342c', { warm: 0.18 });
    // Four ripeness ramps rather than one: unripe green through to full colour, picked PER BERRY from
    // the site's own ripeness. One ramp gives a bunch of identical dots, which reads as a decal.
    const UNRIPE = mix(sp.fruit || '#b8342c', '#7a8a52', 0.72);
    const RIPE = [0, 1, 2, 3].map(k => rampOf(mix(UNRIPE, sp.fruit || '#b8342c', k / 3), { warm: 0.18 }));
    const FRTG = RIPE[0];
    const g = grainOf(sp), E = edgeOf(sp);
    const rgba = new Uint8ClampedArray(n * 4);
    const mFront = new Uint8Array(n), mRim = new Uint8Array(n), mDepth = new Uint8Array(n);
    const mVeil = new Uint8Array(n), mOrn = new Uint8Array(n), mSnow = new Uint8Array(n);
    let zmin = 1e9, zmax = -1e9;
    for (let i = 0; i < n; i++) if (v.a[i]) { if (v.z[i] < zmin) zmin = v.z[i]; if (v.z[i] > zmax) zmax = v.z[i]; }
    const zr = Math.max(1, zmax - zmin);
    const sd = (hashKey(sp.key) % 97) + 1;
    const K = LIGHT.key, R = LIGHT.rim;
    const RSl = Math.hypot(R[0], R[1]) || 1, RS = [R[0] / RSl, R[1] / RSl];

    // local mass thickness — rule 3's input. Wraps for the same reason the distance field does.
    const TH = new Float32Array(n), tmp = new Float32Array(n), RAD = 5, wrp = !!v.wrap;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      let m = 0;
      for (let k = -RAD; k <= RAD; k++) {
        let jx = x + k;
        if (wrp) jx = ((jx % w) + w) % w; else if (jx < 0 || jx >= w) continue;
        const d = D[y * w + jx]; if (d > m) m = d;
      }
      tmp[y * w + x] = m;
    }
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      let m = 0; for (let k = -RAD; k <= RAD; k++) { const jy = y + k; if (jy < 0 || jy >= h) continue; const d = tmp[jy * w + x]; if (d > m) m = d; }
      TH[y * w + x] = m;
    }

    // ambient occlusion off real depth
    const OCC = new Float32Array(n);
    const ring = [[2, 0], [1, 1], [0, 2], [-1, 1], [-2, 0], [-1, -1], [0, -2], [1, -1], [3, 1], [-3, 1], [1, 3], [-1, 3]];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i]) continue;
      let occ = 0;
      for (const [dx, dy] of ring) {
        const jx = x + dx, jy = y + dy;
        if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue;
        const j = jy * w + jx; if (!v.a[j] || v.mat[j] === M.VEIL) continue;
        const dz = v.z[j] - v.z[i];
        if (dz > 0.8) occ += clamp(dz / 5, 0, 1);
      }
      OCC[i] = clamp(1 - occ / ring.length * 2.0, 0.26, 1);
    }

    // pass 1 — luminance
    const LUM = new Float32Array(n);
    let veilPx = 0, bodyPx = 0, edgePx = 0, thinBody = 0, inkPx = 0, veilRimLeak = 0, ornRimLeak = 0, snowPx = 0;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i]) continue;
      const nx = v.nx[i], ny = v.ny[i], nz = v.nz[i], d = D[i];
      const lam = Math.pow(Math.max(0, nx * K[0] + ny * K[1] + nz * K[2]), 1.30);
      const ao = clamp(0.30 + 0.70 * Math.exp(-Math.max(0, d - 1) / 6.0), 0.30, 1) * OCC[i];
      const zf = 0.64 + 0.36 * ((v.z[i] - zmin) / zr);
      const sky = 0.46 + 0.54 * clamp(-ny, 0, 1);
      const tex = (snoise(x / 3.2, y / 3.2, sd) - 0.5) * 0.10;
      LUM[i] = 0.11 * sky * ao * zf + 1.05 * lam * ao * zf + tex;
      mFront[i] = clamp(Math.round(lam * 255), 0, 255);
      mDepth[i] = clamp(Math.round(((v.z[i] - zmin) / zr) * 255), 0, 255);
    }

    // pass 2 — leaf cells + per-cell means
    const LC = leafCells(v, g, sd);
    const sum = new Float32Array(LC.count), cnt = new Float32Array(LC.count);
    for (let i = 0; i < n; i++) { const c = LC.cell[i]; if (c >= 0) { sum[c] += LUM[i]; cnt[c]++; } }
    const mean = new Float32Array(LC.count);
    for (let c = 0; c < LC.count; c++) mean[c] = cnt[c] ? sum[c] / cnt[c] : 0;

    // pass 3 — write
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x;
      if (!v.a[i]) { rgba[i * 4 + 3] = 0; continue; }
      const mt = v.mat[i], d = D[i], nx = v.nx[i], ny = v.ny[i], nz = v.nz[i];
      const lin = !!LINEAR[mt];
      inkPx++;
      if (mt === M.VEIL) veilPx++;
      else if (mt === M.BODY) { bodyPx++; if (TH[i] * 2 < MIN_BODY + 2 * RIM_PX) thinBody++; }
      else if (mt === M.EDGE) edgePx++;

      // RULE 3 + the linear-material exemption in one line
      const nl = Math.hypot(nx, ny) || 1;
      const back = Math.max(0, (nx * RS[0] + ny * RS[1]) / nl);
      const fres = Math.pow(1 - clamp(nz, 0, 1), 1.7);
      const gate = lin ? 0 : smooth(4.0, 5.2, TH[i]);
      const rim = Math.pow(back, 1.15) * fres * gate * smooth(3.6, 0.8, d);
      if (mt === M.VEIL && rim > 0.16) veilRimLeak++;
      if ((mt === M.BLOOM || mt === M.FLECK) && rim > 0.16) ornRimLeak++;

      let ramp = FOL, lum = LUM[i], band = null;
      if (mt === M.WOOD) ramp = WOODR;
      else if (mt === M.VEIL) {
        // a filament gets TWO tones, not five: a 1 px strand banded five ways is visual mud.
        ramp = VEILR;
        band = lum > 0.34 ? 'hi' : 'sh';
      } else if (mt === M.BLOOM) {
        ramp = v.catkinIds.has(v.id[i]) ? CAT : BLM;
        band = lum > 0.40 ? 'key' : lum > 0.18 ? 'hi' : 'mid';   // a 4 px flower needs its top bands
      } else if (mt === M.FLECK) {
        // A berry is 1-5 px, so it gets its tones from GEOMETRY rather than from lighting: the
        // key-side tip pixel takes the top band and the rest steps down. That one pixel is the whole
        // difference between a sphere and a dot, and two flat tones (the first pass) gave dots.
        ramp = ph.greenFruit ? FRTG : FRT;
        const rq = v.ripe ? v.ripe.get(v.id[i]) : null;
        if (rq != null) ramp = RIPE[clamp(Math.round(rq * 3), 0, 3)];
        const up = y > 0 ? v.mat[i - w] : 0, lf = x > 0 ? v.mat[i - 1] : 0;
        const tip = up !== M.FLECK && lf !== M.FLECK;
        band = tip ? (lum > 0.20 ? 'key' : 'hi') : lum > 0.34 ? 'hi' : lum > 0.13 ? 'mid' : 'sh';
      } else if (mt === M.BODY) {
        // LEAF CELL: flat from the cell's own mean, one band down on the lower-right border, plus a
        // one-pixel specular at the cell's key-side tip. Detail ONLY in the light — a cell already in
        // shadow draws no outline, or the canopy becomes black crazy paving.
        const c = LC.cell[i];
        if (c >= 0) {
          const m = mean[c] + (vnoise(c * 7 + 1, c * 3 + 5, 11) - 0.5) * g.tone;
          let bi = BANDI[bandOf(clamp(m, 0, 1.2))];
          const rt = x < w - 1 ? LC.cell[i + 1] : (v.wrap ? LC.cell[y * w] : c);
          const dn = y < h - 1 ? LC.cell[i + w] : c;
          const onEdge = (rt >= 0 && rt !== c) || (dn >= 0 && dn !== c);
          if (onEdge && bi > 0 && m > 0.19) bi -= 1;
          if (g.corner && rt >= 0 && dn >= 0 && rt !== c && dn !== c && bi > 0 && m > 0.30) bi = Math.max(0, bi - 1);
          const lf = x > 0 ? LC.cell[i - 1] : -2, up = y > 0 ? LC.cell[i - w] : -2;
          if (m > 0.55 && lf !== c && up !== c && bi < 4) bi += 1;      // key-side tip specular
          band = BANDN[bi];
        }
      }
      if (band === null) band = bandOf(clamp(lum, 0, 1.2));

      // SNOW: a crust on upward-facing pixels above the snow line, weighted by depth. On veil this is
      // the classic read — snow lodged in the twigs — so it deliberately applies to the filaments too.
      let hex = ramp[band];
      if (sn.cap > 0.02) {
        const up = i - w;
        const topOfMass = y === 0 || !v.a[up] || (mt === M.VEIL && v.mat[up] !== M.VEIL);
        const facing = -ny > 0.34 || topOfMass;
        const load = sn.cap * (topOfMass ? 1 : 0.55) * (mt === M.VEIL ? 0.85 : 1);
        if (facing && load > 0.30 && (d > 0.8 || mt === M.VEIL || topOfMass)) {
          hex = SNOWR[band === 'dp' ? 'sh' : band === 'sh' ? 'mid' : band === 'mid' ? 'hi' : 'key'];
          mSnow[i] = clamp(Math.round(load * 255), 0, 255);
          snowPx++;
        }
      }
      if (rim > 0.16) hex = mix(hex, ramp.rim, clamp((rim - 0.12) * 1.35, 0, 0.95));

      const c3 = h2r(hex);
      rgba[i * 4] = c3[0]; rgba[i * 4 + 1] = c3[1]; rgba[i * 4 + 2] = c3[2]; rgba[i * 4 + 3] = 255;
      mRim[i] = clamp(Math.round(rim * 255), 0, 255);
      mVeil[i] = mt === M.VEIL ? 255 : 0;
      mOrn[i] = mt === M.BLOOM ? 255 : mt === M.FLECK ? 170 : 0;
    }

    // snow crust row: one bright pair of rows where the surface crosses the sprite. One row, not a glow
    // — and the drift itself is the SCENE's tile. No sprite bakes ground.
    if (snowRow > 1 && snowRow < h - 1) {
      const sr = Math.round(snowRow), cr = h2r(CRUST), c2 = h2r(mix(SNOWC, COLD, 0.22));
      for (let x = 0; x < w; x++) {
        for (const [yy, cc] of [[sr, cr], [sr + 1, c2]]) {
          if (yy < 0 || yy >= h) continue;
          const i = yy * w + x; if (!v.a[i]) continue;
          rgba[i * 4] = cc[0]; rgba[i * 4 + 1] = cc[1]; rgba[i * 4 + 2] = cc[2];
          mSnow[i] = 255;
        }
      }
    }

    // FRUIT SEAT. A 2 px berry on a leafy field needs one dark pixel under it or it reads as a hole
    // punched in the canopy rather than as fruit sitting in front of it. This is why FLECK is
    // keyline-exempt: a full outline at this size swallows the berry, a single seat pixel sells it.
    // Collected first, applied after, so a stacked bunch never double-darkens.
    {
      const kl = h2r(KEYLINE), seat = [];
      for (let y = 0; y < h - 1; y++) for (let x = 0; x < w; x++) {
        const i = y * w + x;
        if (!v.a[i] || v.mat[i] !== M.FLECK) continue;
        const d = i + w;
        if (!v.a[d] || v.mat[d] === M.FLECK || v.mat[d] === M.BLOOM) continue;
        seat.push(d);
      }
      for (const d of seat) for (let c = 0; c < 3; c++)
        rgba[d * 4 + c] = rgba[d * 4 + c] * 0.44 + kl[c] * 0.56;
    }

    // KEYLINE — RETIRED BY DEFAULT (ADR 0031), and the one rule that makes the veil work: a
    // filament takes NO keyline. An outline round every 1 px strand turns the whole field into a
    // grey solid, which is what the first pass did. That exemption is the ADR's own perimeter law
    // found early and encoded locally; retiring the ring generalises it to the whole shrub rather
    // than replacing it — the VEIL/FLECK carve-out below stays exactly as it was, because with the
    // flag ON (the A/B arm) it is still what keeps the veil readable.
    //
    // Switching it off is a PURE RING DELETION: every pixel this pass writes has no geometry under
    // it, so no painted pixel of any shrub changes value (proven per species, 0 violations).
    if (o.outline === undefined ? KEYLINE_DEFAULT : o.outline !== false) {
      const kl = h2r(KEYLINE), add = [];
      for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = y * w + x; if (v.a[i]) continue;
        let hit = false;
        for (let dy = -1; dy <= 1 && !hit; dy++) for (let dx = -1; dx <= 1 && !hit; dx++) {
          const jx = v.wrap ? ((x + dx) % w + w) % w : x + dx, jy = y + dy;
          if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue;
          const j = jy * w + jx;
          if (v.a[j] && v.mat[j] !== M.VEIL && v.mat[j] !== M.FLECK) hit = true;
        }
        if (hit) add.push(i);
      }
      for (const i of add) { rgba[i * 4] = kl[0]; rgba[i * 4 + 1] = kl[1]; rgba[i * 4 + 2] = kl[2]; rgba[i * 4 + 3] = 255; }
    }

    return { rgba, masks: { front: mFront, rim: mRim, depth: mDepth, veil: mVeil, orn: mOrn, snow: mSnow },
      veilPx, bodyPx, edgePx, thinBody, inkPx, veilRimLeak, ornRimLeak, snowPx, TH, cells: LC.count, cell: LC.cell };
  }

  // ---- sway: a shrub is stiff, and the buried part does not move --------------
  function swayShear(buffers, w, h, baseY, frame, sp, sn, snowRow) {
    if (frame == null || !frame) return buffers;
    const curve = Math.sin(frame / SWAY * Math.PI * 2);
    const amp = sp.sway * (1 - clamp(sn.buried, 0, 1) * 0.85);
    const out = buffers.map(b => new b.constructor(b.length));
    const stride = buffers.map(b => b.length / (w * h));
    const wrap = !!sp.wrap;
    for (let y = 0; y < h; y++) {
      const hf = clamp((baseY - y) / Math.max(1, baseY), 0, 1);
      // pinned at the pivot row AND at the snow line: snow holds the lower stems dead still
      const pin = snowRow < h ? clamp((snowRow - y) / Math.max(1, snowRow), 0, 1) : 1;
      const dx = Math.round(amp * curve * Math.pow(hf, 1.6) * pin);
      for (let x = 0; x < w; x++) {
        let sx = x - dx;
        if (wrap) sx = ((sx % w) + w) % w;
        else if (sx < 0 || sx >= w) continue;
        for (let b = 0; b < buffers.length; b++) {
          const st = stride[b], si = (y * w + sx) * st, di = (y * w + x) * st;
          for (let c = 0; c < st; c++) out[b][di + c] = buffers[b][si + c];
        }
      }
    }
    return out;
  }

  // ---- bloom shapes ----------------------------------------------------------
  // Each is 3–8 px. None gets a rim, none gets leaf-cell quantisation: at this size a flower cluster
  // IS one cell, and a rim on a 4 px puff turns it into a bead.
  function bloomShape(v, b, X, Y, Z, id, catkin) {
    const s = b.shape;
    if (s === 'puff') { blob(v, X, Y, Z, 2.7, 2.2, 2.0, M.BLOOM, id, b.seed * 9, EDGES.fine); }
    else if (s === 'umbel') { blob(v, X, Y, Z, 3.4, 1.5, 1.6, M.BLOOM, id, b.seed * 9, EDGES.fine); }
    else if (s === 'whorl') { for (let k = 0; k < 5; k++) { const a = k / 5 * Math.PI * 2 + b.seed * 6; fleck(v, X + Math.cos(a) * 2.1, Y + Math.sin(a) * 1.4, Z, 0.8, M.BLOOM, id); } }
    else if (s === 'bell') { fleck(v, X, Y, Z, 1.1, M.BLOOM, id); fleck(v, X + 1.4, Y + 1.1, Z, 0.8, M.BLOOM, id); }
    else if (s === 'plume') { for (let k = 0; k < 4; k++) blob(v, X, Y - k * 1.8, Z, 2.4 - k * 0.45, 1.3, 1.4, M.BLOOM, id, b.seed * 9 + k, EDGES.fine); }
    else if (s === 'steeple') { for (let k = 0; k < 5; k++) blob(v, X, Y - k * 1.7, Z, 2.6 - k * 0.44, 1.2, 1.5, M.BLOOM, id, b.seed * 9 + k, EDGES.fine); }
    else if (s === 'rose') { blob(v, X, Y, Z, 3.0, 2.5, 1.4, M.BLOOM, id, b.seed * 9, EDGES.fine); fleck(v, X, Y, Z + 1.6, 0.8, M.BLOOM, id); }
    else if (s === 'catkin') {
      const L = b.open ? 6.5 : 3.0, r = b.open ? 1.35 : 1.0;
      limb(v, X, Y, Z, X + 0.6, Y + L, Z, r, r * 0.7, M.BLOOM, id);
    }
    else blob(v, X, Y, Z, 2.4, 2.0, 1.8, M.BLOOM, id, b.seed * 9, EDGES.fine);
    if (catkin) v.catkinIds.add(id);
  }

  // ---- public ----------------------------------------------------------------
  function render(key, o) {
    o = o || {};
    const sp = byKey[key] || SPECIES[0];
    const ph = phaseOf(o.phase || 'green', sp);
    const size = sizeOf(o);
    const variant = ((o.variant | 0) % VARIANTS + VARIANTS) % VARIANTS;
    const sn = snowOf(o.snow == null ? 0 : o.snow, sp);
    const g = build(sp, variant, ph, size, sn);
    const cell = cellOf(sp, size);
    const v = new Vol(cell.w, cell.h);
    v.wrap = sp.wrap ? 1 : 0;
    v.catkinIds = new Set();
    v.ripe = new Map();
    const pivot = { x: cell.pivotX, y: cell.pivotY };
    const py = (y, z) => pivot.y + pyRel(g, y, z);
    const pz = (y, z) => pzOf(g, y, z);
    const px = (x) => sp.wrap ? x : x + cell.dx;
    const E = edgeOf(sp);
    let id = 0, floored = 0;

    for (const L of g.stems) {
      limb(v, px(L[0]), py(L[1], L[2]), pz(L[1], L[2]), px(L[3]), py(L[4], L[5]), pz(L[4], L[5]), L[6], L[7], L[8], id++);
    }
    for (const c of g.clumps) {
      // RULE 1 AT THE EMITTER — and measured on the MINOR axis, which is the subtle part. The ¾ squash
      // takes 14% off a mass's vertical radius, so a mass authored at exactly MIN_R lands 4.6 px deep
      // and fails the very rule it was clamped to. Scale until the minor axis clears the floor.
      const r0 = Math.max(MIN_R, c.r);
      const sq = Math.hypot(CE, SE) * 0.86;
      const k = MIN_R / Math.min(r0, r0 * sq);
      const r = k > 1 ? r0 * k : r0;
      if (r > c.r + 0.001) floored++;
      blob(v, px(c.x), py(c.y, c.z), pz(c.y, c.z), r,
        Math.hypot(r * CE, r * SE) * 0.86, Math.hypot(r * SE, r * CE) * 0.9, M.BODY, id++, (id * 1.7) % 6.28, E);
    }
    const vst = { tried: 0, kept: 0 };
    for (const q of g.veils) {
      veilMass(v, px(q.x), py(q.y, q.z), pz(q.y, q.z), q.rx, Math.hypot(q.ry * CE, q.rz * SE),
        Math.hypot(q.ry * SE, q.rz * CE), q.dens, id++, q.seed || 0, vst, q.flare);
    }
    for (const b of g.blooms) {
      bloomShape(v, b, px(b.x), py(b.y, b.z), pz(b.y, b.z), id++, b.shape === 'catkin');
    }
    // RULE 6: a berry inside the bush is a wasted pixel. Only PRESENTED sites are emitted.
    let tried = 0, presented = 0;
    for (const f of g.flecks) {
      tried++;
      const X = Math.round(px(f.x)), Y = Math.round(py(f.y, f.z)), Z = pz(f.y, f.z);
      if (!v.front(X, Y, Z, 2.2)) continue;
      presented++;
      const fid = id++;
      v.ripe.set(fid, f.ripe == null ? 1 : f.ripe);
      if (f.shape === 'steeple') { for (let k = 0; k < 4; k++) blob(v, X, Y - k * 2.0, Z + 1, 2.2 - k * 0.4, 1.4, 1.4, M.FLECK, fid, k, EDGES.fine); }
      else fleck(v, X, Y, Z, f.shape === 'berry' ? (sp.berryR || 1.1) : 1.2, M.FLECK, fid);
    }

    // where the snow surface crosses THIS sprite, in its own rows — reported so a scene can line its
    // drift tile up with the bake instead of guessing.
    const snowRow = sn.depthM > 0.01 ? pivot.y - sn.depthM * PPU * CE : 1e9;
    if (snowRow < v.h - 1) {
      for (let y = Math.max(0, Math.ceil(snowRow) + 2); y < v.h; y++)
        for (let x = 0; x < v.w; x++) { const i = y * v.w + x; if (v.a[i]) v.clearPx(i); }
    }

    const dsp = despeckle(v);
    // ripeness spread, read back off what actually SURVIVED presentation — a bunch whose laggards all
    // lost the depth test is a uniform bunch again, so this is measured after the emit, not before.
    const ripeStat = { n: 0, sum: 0, min: 1, max: 0 };
    v.ripe.forEach((r) => { ripeStat.n++; ripeStat.sum += r; if (r < ripeStat.min) ripeStat.min = r; if (r > ripeStat.max) ripeStat.max = r; });
    let D = distField(v.a, v.w, v.h, v.wrap);
    const thin = resolveThinBody(v, D);
    if (thin.culled) D = distField(v.a, v.w, v.h, v.wrap);   // ink changed, so the field must be redone
    const audit = massReport(v.a, v.mat, D, v.w, v.h, v.wrap);
    const win = windowReport(v.a, v.w, v.h, v.wrap);
    const sh = shade(v, sp, ph, sn, D, o, snowRow, presented);

    // wrap continuity: guaranteed by the modulo emitter, so what is worth measuring is whether ink
    // actually CROSSES the join. Measured in a 2 px band with a 1-row tolerance, NOT column-exact:
    // a 24%-duty twig field can cover the seam completely and still never put a lit pixel in column 0
    // and column w-1 of the very same row, which failed every bare thicket for no visual reason.
    let crossings = 0; {
      const near = (y, left) => {
        for (let dy = -1; dy <= 1; dy++) {
          const yy = y + dy;
          if (yy < 0 || yy >= v.h) continue;
          for (let k = 0; k < 2; k++) if (v.a[yy * v.w + (left ? k : v.w - 1 - k)]) return true;
        }
        return false;
      };
      for (let y = 0; y < v.h; y++) if (near(y, true) && near(y, false)) crossings++;
    }

    let rgba = sh.rgba, mf = sh.masks.front, mr = sh.masks.rim, md = sh.masks.depth,
      mv = sh.masks.veil, mo = sh.masks.orn, ms = sh.masks.snow;
    if (o.frame) {
      const out = swayShear([rgba, mf, mr, md, mv, mo, ms], v.w, v.h, pivot.y, o.frame, sp, sn, snowRow);
      rgba = out[0]; mf = out[1]; mr = out[2]; md = out[3]; mv = out[4]; mo = out[5]; ms = out[6];
    }

    const sr = (snowRow > v.h || snowRow <= 0) ? null : Math.round(snowRow);
    return {
      w: v.w, h: v.h, pivot, rgba,
      masks: { front: mf, rim: mr, depth: md, veil: mv, orn: mo, snow: ms },
      alpha: v.a, dist: D, mat: v.mat, nx: v.nx, ny: v.ny, nz: v.nz, thick: sh.TH,
      cell: sh.cell, enclosed: win.enclosed,
      species: sp, phase: ph, snow: sn, variant, size, stage: stageName(size),
      snowRow: sr, buriedOut: sn.gone,
      stems: g.stems.length, clumps: g.clumps.length, veils: g.veils.length,
      blooms: g.blooms.length, flecks: presented,
      report: {
        // A snow-clipped tile is not a broken tile: snow is a runtime state and it can legitimately eat
        // the rows that crossed the join, so the crossing assert only applies to the unclipped bake.
        pass: audit.pass && sh.veilRimLeak === 0 && sh.ornRimLeak === 0 && sn.legal &&
          (!sp.wrap || crossings > 0 || sn.cap > 0),
        masses: audit.masses, bodyMasses: audit.bodyMasses, linearMasses: audit.linearMasses,
        failed: audit.failed, minBody: audit.minBody, bodyRatio: audit.bodyRatio, floored,
        despeckled: dsp.removed, spared: dsp.spared, culled: thin.culled, edged: thin.edged,
        veilRimLeak: sh.veilRimLeak, ornRimLeak: sh.ornRimLeak,
        veilPx: sh.veilPx, veilTried: vst.tried,
        edgePx: sh.edgePx,
        veilDuty: vst.tried ? Math.round(vst.kept / vst.tried * 1000) / 10 : 0,
        veilShare: sh.inkPx ? Math.round(sh.veilPx / sh.inkPx * 100) : 0,
        thinBodyPct: Math.round(sh.thinBody / Math.max(1, sh.bodyPx) * 1000) / 10,
        leafCells: sh.cells,
        pxPerCell: sh.cells ? Math.round(sh.bodyPx / sh.cells * 10) / 10 : 0,
        windows: win.windows, windowPx: win.windowPx,
        // rule 4 is about BASKETS. You do not look into a carpet, so a mat is exempt by form — stated
        // here rather than left for a reader to infer from a red cell.
        windowsApply: sp.unit !== 'mat',
        tried, presented, presentPct: tried ? Math.round(presented / tried * 100) : null,
        ripeAvg: ripeStat.n ? Math.round(ripeStat.sum / ripeStat.n * 100) : null,
        ripeSpread: ripeStat.n ? Math.round((ripeStat.max - ripeStat.min) * 100) : null,
        cluster: sp.cluster, berryR: sp.berryR, veilK: Math.round(g.veilK * 100),
        snowPx: sh.snowPx, buried: Math.round(sn.buried * 100), legal: sn.legal,
        crossings, wrap: !!sp.wrap,
        metres: Math.round(sp.worldH * size / PPU * 100) / 100,
        notes: g.notes,
        underFloor: sp.worldH * size < 10,
      },
    };
  }

  function packMask(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      out[i * 4] = res.masks.front[i]; out[i * 4 + 1] = res.masks.rim[i];
      out[i * 4 + 2] = res.masks.depth[i]; out[i * 4 + 3] = res.rgba[i * 4 + 3];
    }
    return out;
  }
  // the calendar bake: R = veil (no-rim, no-keyline flag) · G = ornament (bloom 255 / fruit 170) ·
  // B = snow load · A = coverage
  function packState(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      out[i * 4] = res.masks.veil[i]; out[i * 4 + 1] = res.masks.orn[i];
      out[i * 4 + 2] = res.masks.snow[i]; out[i * 4 + 3] = res.rgba[i * 4 + 3];
    }
    return out;
  }
  function grey(mask, res, tint) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4), t = tint ? h2r(tint) : null;
    for (let i = 0; i < n; i++) {
      const a = res.rgba[i * 4 + 3]; if (!a) { out[i * 4 + 3] = 0; continue; }
      const g = mask[i];
      out[i * 4] = t ? g * t[0] / 255 : g; out[i * 4 + 1] = t ? g * t[1] / 255 : g; out[i * 4 + 2] = t ? g * t[2] / 255 : g;
      out[i * 4 + 3] = 255;
    }
    return out;
  }
  function massView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    const RIMC = h2r('#e8b06a'), BODY = h2r('#2f6a4c'), CORE = h2r('#8fd6a0'), BAD = h2r('#d2453c'),
      VL = h2r('#5b93b8'), WD = h2r('#3f4f57'), FL = h2r('#d9b47c'), BL = h2r('#c98fb0'), SN = h2r('#dfeaec');
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      const d = res.dist[i], mt = res.mat[i];
      let c;
      if (mt === M.VEIL) c = VL;
      else if (mt === M.FLECK) c = FL;
      else if (mt === M.BLOOM) c = BL;
      else if (mt === M.WOOD) c = WD;
      else if (mt === M.EDGE) c = h2r('#6f9a72');
      else if (mt === M.SNOW) c = SN;
      else c = (res.thick[i] * 2 < MIN_BODY + 2 * RIM_PX) ? BAD : d <= RIM_PX ? RIMC : d >= RIM_PX + MIN_BODY / 2 ? CORE : BODY;
      out[i * 4] = c[0]; out[i * 4 + 1] = c[1]; out[i * 4 + 2] = c[2]; out[i * 4 + 3] = 255;
    }
    return out;
  }
  // every leaf cell in its own flat colour — the view to check. A patch of one colour spanning a
  // whole canopy mass is the lattice failing, not the shading.
  function leafView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      const c = res.cell[i];
      if (c < 0) { const g = res.mat[i] === M.VEIL ? 60 : 34; out[i * 4] = g; out[i * 4 + 1] = g + 8; out[i * 4 + 2] = g + 10; out[i * 4 + 3] = 255; continue; }
      out[i * 4] = 60 + vnoise(c, c * 3 + 1, 3) * 190;
      out[i * 4 + 1] = 60 + vnoise(c, c * 5 + 2, 7) * 190;
      out[i * 4 + 2] = 60 + vnoise(c, c * 7 + 3, 13) * 190;
      out[i * 4 + 3] = 255;
    }
    return out;
  }
  // the calendar view: what the phase actually put on the plant
  function phaseView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    const LEAFC = h2r('#4f8a5f'), VL = h2r('#6b7f8c'), BL = h2r('#d98fb4'), FR = h2r('#d2564c'),
      WD = h2r('#5a4a3c'), SN = h2r('#e6eef0');
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      const mt = res.mat[i];
      let c = mt === M.VEIL ? VL : mt === M.BLOOM ? BL : mt === M.FLECK ? FR : mt === M.WOOD ? WD : LEAFC;
      if (res.masks.snow[i] > 200) c = SN;
      out[i * 4] = c[0]; out[i * 4 + 1] = c[1]; out[i * 4 + 2] = c[2]; out[i * 4 + 3] = 255;
    }
    return out;
  }
  // the window view: the sky you can see through the basket, which rule 4 is measured on
  function windowView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      if (res.enclosed && res.enclosed[i] >= 0 && !res.alpha[i]) {
        out[i * 4] = 232; out[i * 4 + 1] = 176; out[i * 4 + 2] = 106; out[i * 4 + 3] = 255; continue;
      }
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      const g = res.mat[i] === M.VEIL ? 52 : 30;
      out[i * 4] = g; out[i * 4 + 1] = g + 12; out[i * 4 + 2] = g + 14; out[i * 4 + 3] = 255;
    }
    return out;
  }
  function normalView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      out[i * 4] = (res.nx[i] * 0.5 + 0.5) * 255; out[i * 4 + 1] = (-res.ny[i] * 0.5 + 0.5) * 255;
      out[i * 4 + 2] = (res.nz[i] * 0.5 + 0.5) * 255; out[i * 4 + 3] = 255;
    }
    return out;
  }

  function sheetSpec(key, size, axis) {
    const sp = byKey[key] || SPECIES[0], t = size == null ? 1 : size, cell = cellOf(sp, t);
    const cols = axis === 'phase' ? PHASES.length : VARIANTS;
    const w = cell.w * cols, h = cell.h * SWAY;
    return { cell: [cell.w, cell.h], cols, rows: SWAY, w, h, fits: w <= 2048 && h <= 2048, ppu: PPU,
      pivot: [cell.pivotX, cell.pivotY], pad: cell.pad, elev: ELEV, stage: stageName(t),
      axis: axis === 'phase' ? 'phase × sway' : 'variant × sway', wrap: !!sp.wrap,
      metres: Math.round(sp.worldH * t / PPU * 100) / 100 };
  }

  function inkOf(res) {
    let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
    for (let y = 0; y < res.h; y++) for (let x = 0; x < res.w; x++) if (res.alpha[y * res.w + x]) {
      if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
    }
    return { w: Math.max(0, x1 - x0 + 1), h: Math.max(0, y1 - y0 + 1) };
  }
  const _auditCache = {};
  function sheetAudit(size) {
    const t = size == null ? 1 : size, ck = 'a' + Math.round(t * 1000);
    if (_auditCache[ck]) return _auditCache[ck];
    let bytes = 0, allFit = true, worstAll = 100, worstKey = null;
    const rows = SPECIES.map(sp => {
      const cell = cellOf(sp, t), vs = sheetSpec(sp.key, t), ps = sheetSpec(sp.key, t, 'phase');
      let worst = 1e9, worstPhase = null, best = 0, minWin = 1e9;
      for (const ph of PHASE_KEYS) {
        const r = render(sp.key, { variant: 0, phase: ph, size: t, frame: 0 });
        const ink = inkOf(r), ratio = ink.w * ink.h / (cell.w * cell.h);
        if (ratio < worst) { worst = ratio; worstPhase = ph; }
        if (ratio > best) best = ratio;
        if (r.report.windows < minWin) minWin = r.report.windows;
      }
      const px = vs.w * vs.h + ps.w * ps.h;
      bytes += px * 4;
      const fits = vs.fits && ps.fits;
      if (!fits) allFit = false;
      const wp = Math.round(worst * 100);
      if (wp < worstAll) { worstAll = wp; worstKey = sp.key; }
      return {
        key: sp.key, name: sp.name, hab: sp.hab, unit: sp.unit, wrap: !!sp.wrap,
        cell: [cell.w, cell.h], pivot: [cell.pivotX, cell.pivotY],
        worstInk: wp, worstPhase, bestInk: Math.round(best * 100), minWindows: minWin,
        variantSheet: [vs.w, vs.h], phaseSheet: [ps.w, ps.h], fits,
        headroom: 2048 - Math.max(vs.w, vs.h, ps.w, ps.h),
        kb: Math.round(px * 4 / 1024),
      };
    });
    const out = { rows, mb: Math.round(bytes / 1048576 * 100) / 100, fits: allFit, cap: 2048,
      worstInk: worstAll, worstInkSpecies: worstKey, stage: stageName(t), size: t };
    _auditCache[ck] = out;
    return out;
  }

  // The veil exemption is a CONTRACT, not a look. A consuming shader must branch on it from DATA —
  // never by guessing from the sprite — so the rig states it machine-readably, serialised out of the
  // live rig, which is why it cannot drift from the bake.
  function contract(size) {
    const audit = sheetAudit(size);
    return {
      rig: 'shrubIsoRig', version: 1, generated: new Date().toISOString(),
      projection: { ppu: PPU, elev: ELEV, heightScale: +CE.toFixed(4), depthScale: +SE.toFixed(4),
        pivot: 'ground contact (root crown), bottom-centre of the union cell',
        antialias: false, alpha: 'binary',
        // ADR 0031: retired. The colour is kept so the A/B arm (`{outline:true}`) and any archived
        // sheet remain describable — `keylineDefault:false` is what production bakes. It is also
        // still the FRUIT SEAT's mix anchor, which is not an outline and is not gated.
        keyline: KEYLINE, keylineDefault: KEYLINE_DEFAULT },
      materials: {
        body:  { id: M.BODY,  linear: false, massFloorPx: MIN_R, carriesRim: true, keyline: true,
          note: 'foliage masses. Obeys the mass floor — no radius under MIN_R in any axis, clamped at the emitter.' },
        wood:  { id: M.WOOD,  linear: true,  massFloorPx: null, carriesRim: 'thickness-gated', keyline: true,
          note: 'stems, canes, root crown. Linear, so not policed by the floor; the rim gate decides per pixel.' },
        edge:  { id: M.EDGE, linear: true,  massFloorPx: null, carriesRim: 'thickness-gated', keyline: true,
          note: 'foliage thinner than the floor — a canopy fringe or an occluded crescent — reclassified after z-resolution. Same ramp as body, but linear: rule 1 is about interior body, and a fringe is not trying to hold a rim. Read `edged` for the count.' },
        veil:  { id: M.VEIL,  linear: true,  massFloorPx: null, carriesRim: false, keyline: false,
          note: 'bare twig volume drawn as FILAMENTS along the branch direction field \u2014 no dither. Exempt from the floor, forbidden a rim, and forbidden a KEYLINE \u2014 an outline round every strand collapses it into a solid.' },
        fleck: { id: M.FLECK, linear: true,  massFloorPx: null, carriesRim: false, keyline: false,
          note: 'fruit and buds, 1-5 px, carried in per-species clusters, each cluster ripening unevenly across four ramps. Only emitted where PRESENTED (frontmost at that pixel). Keyline-exempt but given a single dark SEAT pixel below, which is what makes a berry read as sitting in front of the canopy rather than as a hole in it.' },
        bloom: { id: M.BLOOM, linear: true,  massFloorPx: null, carriesRim: false, keyline: true,
          note: 'flower clusters and catkins, 3-8 px. No leaf-cell quantisation: at this size the cluster IS one cell.' },
      },
      bakes: {
        light: { file: '<key>_light.png',
          R: 'key light · N·L from the fixed upper-left key · all materials',
          G: 'back rim · DEFINED FOR BODY AND WOOD ONLY. Identically 0 on veil, fleck and bloom. Gate every read on state.R.',
          B: 'surface depth, normalised per sprite', A: 'coverage' },
        state: { file: '<key>_calendar.png',
          R: '255 on VEIL pixels — the no-rim / no-keyline flag. This is the branch. Read it, do not infer it.',
          G: 'ornament: 255 bloom · 170 fruit · 0 otherwise (a hue-shift hook for berry variation without a rebake)',
          B: 'snow load 0-255', A: 'coverage' },
      },
      calendar: {
        phases: PHASES.map(p => ({ key: p.key, label: p.label, when: p.when })),
        derives: ['leaf', 'bloom', 'fruit', 'catkin', 'veil', 'colour', 'brightStem'],
        note: 'ONE table maps (phase, species) -> all six. bloomFirst species flower on naked wood; holds species carry fruit through dormant. A shrub cannot end up in flower and full leaf in the same frame.',
      },
      snow: {
        nominalM: SNOW_M,
        steps: SNOWS.map(s => ({ key: s.key, m: s.m })),
        habitatK: HABITATS.map(h => ({ key: h.key, snowK: h.snowK })),
        derives: ['clipped rows', 'crust', 'sway damping', 'legality'],
        clipsNotFloods: true,
        note: 'depth = step.m * habitat.snowK. Rows below the surface are CLIPPED, never flooded — the drift itself is the scene\u2019s tile (same ruling as the rock rig\u2019s awash sheets). buried >= 0.95 means DO NOT DRAW the shrub; draw a drift.',
        snowRow: 'row index in sprite space where the surface crosses. null when it is outside the sprite — read `buriedOut` to tell which side.',
      },
      habitats: HABITATS.map(h => ({ key: h.key, name: h.name, snowK: h.snowK, substrate: h.sub })),
      sway: { frames: SWAY, note: 'pinned at the pivot row AND at the snow line; amplitude scales with (1 - buried).' },
      sheets: { axisA: 'variant × sway', axisB: 'phase × sway', cap: 2048,
        cellPolicy: 'ONE union cell and ONE pivot per species per growth stage, unioned over 4 variants x 8 phases. Snow is NOT in the union: it only clips. Runtime phase swaps are pivot-stable by construction.',
        wrapUnits: SPECIES.filter(s => s.wrap).map(s => ({ key: s.key, tileW: s.w })),
        totalMb: audit.mb, allFit: audit.fits },
      species: SPECIES.map(s => {
        const a = audit.rows.find(r => r.key === s.key);
        return { key: s.key, name: s.name, latin: s.latin, habitat: s.hab, form: s.form, unit: s.unit,
          heightM: +(s.worldH / PPU).toFixed(2), evergreen: !!s.evergreen, bloomFirst: !!s.bloomFirst,
          holdsFruit: !!s.holds, grain: s.grain, wrap: !!s.wrap,
          cell: a.cell, pivot: a.pivot, variantSheet: a.variantSheet, phaseSheet: a.phaseSheet,
          worstInkPct: a.worstInk, worstInkPhase: a.worstPhase, minWindows: a.minWindows, fits2048: a.fits };
      }),
      asserts: [
        'every sheet axis <= 2048 (Unity downscales silently; only a pivot assert catches it)',
        'state.R (veil flag) must gate every read of light.G',
        'no veil pixel carries light.G > 0, and no veil pixel is keylined',
        'pivot is identical across all phases and snow depths of a species (union cell invariant)',
        'every wrap unit has crossings > 0 — a tile whose stems never cross the join reads as a row of pots',
        'every non-mat species has windows > 0 in leaf at FULL stage: a shrub you cannot see into is a bush-shaped rock. Mats are exempt by form (you do not look into a carpet) and young stages by scale (below the mass floor a shrub is legitimately one solid tuft).',
      ],
      notInThisRig: ['Bayberry', 'SweetFern', '— shorePlantRig owns both as dune/upland units'],
    };
  }

  root.Shrubs = {
    PPU, RIM_PX, MIN_BODY, MIN_R, SWAY, VARIANTS, SPECIES, byKey, LIGHT, KEYLINE, KEYLINE_DEFAULT,
    COLD, WARM,
    SNOWC, CRUST, ELEV, CE, SE, SNOW_M, SNOWS, SNOW_KEYS, PHASES, PHASE_KEYS, HABITATS, habOf,
    STAGES, STAGE_KEYS, GRAINS, EDGES, M, MAT_NAME, LINEAR,
    sizeOf, stageName, phaseOf, snowOf, grainOf, edgeOf, grainName, folColour, stemColour, rampOf,
    render, packMask, packState, grey, massView, leafView, phaseView, windowView, normalView,
    sheetSpec, cellOf, inkOf, sheetAudit, contract,
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
