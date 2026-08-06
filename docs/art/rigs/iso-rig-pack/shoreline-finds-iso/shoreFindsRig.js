/* Hidden Harbours — parametric SHORE FINDS ISO pack (beachcombing decor & pickups).
   Successor to the baked Art/Sprites/Shore/Finds/*.png pack: same 12 finds, 36 total, all
   generated. The house rig pattern (wharfDecorRig / shorePlantRig / driftWeedRig): one
   parametric source, forms not drawings, colour via ramps, anchors as data.

   CAMERA — ¾ iso, camera from the south, elev 40°. Ground foreshorten Q = 0.72 baked into
   every shape. 32 px = 1 m. No AA, binary alpha, 1 px soft keyline #231d14 (warm shore
   keyline, darkened from the finds panel ground #12100b).

   HEADING — these lie FLAT, so there is no facing, only a LIE ANGLE. 8 canonical steps
   (N NE E SE S SW W NW = the object's long axis in the ground plane) so the compositor can
   scatter without repeats. Rotation happens in the GENERATOR (ground plane, then projected),
   never as a pixel rotate — no resampling, no mush.

   READ SCALE — a periwinkle is 2.5 cm and a drift log is 1.1 m; drawn linearly one of them
   is 1 px. So every find declares its TRUE size in cm and the pack derives the drawn size
   from ONE rule: px = 4.7 · cm^0.62 (compressive). A clam lands on 17 px (the old baked pack
   drew 18), a periwinkle on 8 px instead of 2, a log on 87 px. Relative sizes stay honest,
   nothing falls under the readability floor. Both numbers ship in the report.

   FORMS (19 generators, 36 finds) — valve · fanValve · spiral · disc · star · carapace ·
   claw · rod · root · feather · pouch · shard · bottle · coil · mesh · ball · cluster ·
   cobble · can. Height field first (z per pixel), lit from the height field — so ribs,
   whorls, domes and splits are lighting, not drawn lines.

   STATES (ramp transforms, structure identical): wet (tideline — darker, cool cast, sky
   glint), dry (upper beach — as-quarried, matte, sand mottle), bleached (old wrack / dune —
   pulled toward bone #e9e6df, desaturated).
   AXES: variant 0–2 (seeded) · wear 0–1 (chipped silhouette, softened relief) ·
         sand 0–1 (grains clinging in the contact shadow and along the lee).

   ANCHORS AS DATA — sit (ground-contact centre = the pivot) · pick (hand grab point, the
   crown) · catch (2–3 outer points where weed and rope snag).

   GAMEPLAY DATA — trueCm, zone (tide/wrack/upper/dune), rarity, stack (pile cap).

   Exposes globalThis.ShoreFinds = { PPU, Q, KEYLINE, DIRS, FINDS, CATS, MAT, STATES,
     list(), cellOf(key), rampsOf(key,state), render(key, dir, opts) ->
     { w, h, rgba, anchors, params, report } }
     opts: { variant:0..2, state:'wet'|'dry'|'bleached', wear:0..1, sand:0..1 }
   Runs in the bake sandbox and in the browser. */
(function (root) {
  'use strict';
  const PPU = 32, Q = 0.72, KEYLINE = '#231d14';
  const COLD = '#2a3b47', WARM = '#f2cf94', BONE = '#e9e6df', WETC = '#2f4148';
  const DIRS = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  const STATES = ['wet', 'dry', 'bleached'];

  // ---- colour ---------------------------------------------------------------
  function h2r(h) { h = h.replace('#', ''); return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)]; }
  function r2h(c) { return '#' + c.map(v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')).join(''); }
  function mix(a, b, t) { const A = h2r(a), B = h2r(b); return r2h([0, 1, 2].map(i => A[i] + (B[i] - A[i]) * t)); }
  function desat(a, t) { const A = h2r(a), g = A[0] * 0.3 + A[1] * 0.59 + A[2] * 0.11; return r2h(A.map(v => v + (g - v) * t)); }
  // 6-step band ramp + a specular step. Same shape as the plant/decor rigs.
  function rampFrom(base, o) {
    o = o || {};
    return {
      dp: mix(mix(base, '#000000', 0.72), COLD, 0.34),
      sh: mix(mix(base, '#000000', 0.48), COLD, 0.22),
      mid: mix(base, '#000000', 0.17),
      hi: mix(base, WARM, 0.15),
      key: mix(mix(base, '#ffffff', 0.11), WARM, o.warm == null ? 0.30 : o.warm),
      rim: mix(mix(base, '#ffffff', 0.36), WARM, 0.20),
      gl: mix(mix(base, '#ffffff', 0.62), '#d2e4e6', 0.34),
    };
  }
  const BANDS = ['dp', 'sh', 'mid', 'hi', 'key', 'rim'];

  /* Materials — base hex + traits. (v) = verbatim from an existing Hidden Harbours ramp,
     so the new pack cannot drift away from the baked one it replaces. */
  const MAT = {
    clam:      { n: 'clam shell',   base: '#c9b083', gloss: 0.30, warm: 0.30, _p: 'shellfishRig clam mid (v)' },
    clamRing:  { n: 'growth ring',  base: '#9c7f57', gloss: 0.20, warm: 0.26, _p: 'shellfishRig clam sh (v)' },
    quahog:    { n: 'quahog',       base: '#b3a794', gloss: 0.26, warm: 0.24 },
    quahogLip: { n: 'purple lip',   base: '#6d5570', gloss: 0.34, warm: 0.18 },
    nacre:     { n: 'mussel nacre', base: '#293747', gloss: 0.62, warm: 0.16, _p: 'shellfishRig mussel mid (v)' },
    sheen:     { n: 'nacre sheen',  base: '#c2d6da', gloss: 0.70, warm: 0.14, _p: 'shellfishRig mussel acc (v)' },
    scallop:   { n: 'scallop',      base: '#e2b98f', gloss: 0.28, warm: 0.34, _p: 'baked Finds scallop dot (v)' },
    scalRib:   { n: 'scallop rib',  base: '#c2916a', gloss: 0.24, warm: 0.30 },
    oyster:    { n: 'oyster',       base: '#a8a191', gloss: 0.14, warm: 0.22 },
    oysterIn:  { n: 'oyster nacre', base: '#cbbfa4', gloss: 0.52, warm: 0.24, _p: 'baked Finds oyster dot (v)' },
    razor:     { n: 'razor shell',  base: '#8c7f52', gloss: 0.34, warm: 0.26 },
    jingle:    { n: 'jingle shell', base: '#d6bd7e', gloss: 0.58, warm: 0.36 },
    snail:     { n: 'periwinkle',   base: '#6b5734', gloss: 0.34, warm: 0.24, _p: 'baked Finds periwinkle dot #7a6a4a, deepened' },
    snailBand: { n: 'whorl band',   base: '#cbb48c', gloss: 0.30, warm: 0.28 },
    whelk:     { n: 'dog whelk',    base: '#c4b391', gloss: 0.28, warm: 0.30 },
    moon:      { n: 'moon snail',   base: '#b09772', gloss: 0.36, warm: 0.28 },
    bone:      { n: 'bone / test',  base: '#ded7c4', gloss: 0.12, warm: 0.28, _p: 'baked Finds bone dot (v)' },
    boneDp:    { n: 'marrow / pore', base: '#9a8f76', gloss: 0.10, warm: 0.20 },
    urchin:    { n: 'urchin test',  base: '#9aa87e', gloss: 0.16, warm: 0.22 },
    star:      { n: 'sea star',     base: '#c96a4a', gloss: 0.20, warm: 0.30, _p: 'baked Finds starfish dot (v)' },
    brittle:   { n: 'brittle star', base: '#8a7f6a', gloss: 0.18, warm: 0.24 },
    chitin:    { n: 'rock crab',    base: '#b6613a', gloss: 0.38, warm: 0.30, _p: 'baked Finds crab dot (v)' },
    chitinGrn: { n: 'green crab',   base: '#6b7a44', gloss: 0.34, warm: 0.24 },
    chitinRed: { n: 'lobster shell', base: '#b34a33', gloss: 0.40, warm: 0.28 },
    chitinPale: { n: 'joint / tip',  base: '#e0c79a', gloss: 0.30, warm: 0.32 },
    drift:     { n: 'silvered wood', base: '#9c917c', gloss: 0.08, warm: 0.34, _p: 'baked Finds driftwood dot #b3a68c, greyed' },
    driftDp:   { n: 'split / knot', base: '#6e6553', gloss: 0.06, warm: 0.24, _p: 'Finds panel muted #6e6553 (v)' },
    lath:      { n: 'trap lath',    base: '#8c6a52', gloss: 0.10, warm: 0.30 },
    lathPaint: { n: 'lath paint',   base: '#a8503c', gloss: 0.16, warm: 0.26 },
    feather:   { n: 'gull feather', base: '#e9e6de', gloss: 0.14, warm: 0.24, _p: 'baked Finds feather dot (v)' },
    featherGry: { n: 'grey vane',    base: '#9aa0a0', gloss: 0.14, warm: 0.18 },
    purse:     { n: 'skate case',   base: '#3a3428', gloss: 0.44, warm: 0.20 },
    parchment: { n: 'egg case',     base: '#cbbfa4', gloss: 0.22, warm: 0.30 },
    chalk:     { n: 'barnacle',     base: '#e6e2d4', gloss: 0.12, warm: 0.26 },
    granite:   { n: 'granite',      base: '#8e8b83', gloss: 0.16, warm: 0.24 },
    graniteWm: { n: 'warm cobble',  base: '#a09380', gloss: 0.16, warm: 0.28 },
    vein:      { n: 'quartz vein',  base: '#d4cfc2', gloss: 0.22, warm: 0.22 },
    char:      { n: 'sea coal',     base: '#3a3630', gloss: 0.26, warm: 0.14 },
    glassGrn:  { n: 'frosted green', base: '#7fae9a', gloss: 0.66, warm: 0.22, frost: 1, _p: 'baked Finds sea glass dot (v)' },
    glassBlu:  { n: 'frosted blue', base: '#6f92a8', gloss: 0.66, warm: 0.20, frost: 1 },
    glassWht:  { n: 'frosted white', base: '#c3cbc4', gloss: 0.66, warm: 0.22, frost: 1 },
    bottle:    { n: 'bottle glass', base: '#46614a', gloss: 0.72, warm: 0.18 },
    bottleLip: { n: 'bottle lip',   base: '#7d9a7f', gloss: 0.70, warm: 0.20 },
    ropeOr:    { n: 'poly rope',    base: '#c46a35', gloss: 0.22, warm: 0.28 },
    ropeCore:  { n: 'frayed core',  base: '#8a4a24', gloss: 0.18, warm: 0.24 },
    twine:     { n: 'net twine',    base: '#5d7a52', gloss: 0.24, warm: 0.22 },
    foam:      { n: 'plastic float', base: '#d8d3c4', gloss: 0.30, warm: 0.26 },
    foamMark:  { n: 'float band',   base: '#3f6b8c', gloss: 0.30, warm: 0.20 },
    rust:      { n: 'rusted tin',   base: '#8a5433', gloss: 0.20, warm: 0.26 },
    rustDp:    { n: 'rust hole',    base: '#4e3020', gloss: 0.14, warm: 0.18 },
    sand:      { n: 'clinging sand', base: '#b09a72', gloss: 0.10, warm: 0.34 },
  };

  // ---- the pack -------------------------------------------------------------
  const CATS = [
    { key: 'shell',   name: 'SHELLS' },
    { key: 'remains', name: 'REMAINS & MOULTS' },
    { key: 'wood',    name: 'DRIFTWOOD & BONE' },
    { key: 'case',    name: 'CASES & CRUSTS' },
    { key: 'stone',   name: 'STONE & COAL' },
    { key: 'flotsam', name: 'FLOTSAM' },
  ];

  const F = (key, name, cat, form, cm, hRel, body, acc, zone, rarity, stack, note, p) =>
    ({ key, name, cat, form, cm, hRel, body, acc, zone, rarity, stack, note, p: p || {} });

  const FINDS = [
    // ---- shells
    F('SoftshellClam', 'Soft-shell Clam', 'shell', 'valve', 9, 0.20, 'clam', 'clamRing', 'tide', 'common', 6,
      'The digging clam — steamer. Thin chalky valve, deep growth rings', { elong: 1.50, ends: 2.8, taper: 0.20, rings: 5, cup: 0.9 }),
    F('Quahog', 'Quahog', 'shell', 'valve', 8, 0.27, 'quahog', 'quahogLip', 'tide', 'common', 5,
      'Hard clam — heavy valve, tight rings, purple inner lip', { elong: 1.16, ends: 2.4, taper: 0.10, rings: 8, cup: 1.05, lip: 1 }),
    F('BlueMussel', 'Blue Mussel', 'shell', 'valve', 6, 0.24, 'nacre', 'sheen', 'tide', 'common', 9,
      'Wedge shell, blue-black with a worn nacre shoulder', { elong: 1.95, ends: 2.2, taper: 0.44, rings: 3, cup: 1.0, edgeBand: 1.6 }),
    F('Scallop', 'Scallop Shell', 'shell', 'fanValve', 9, 0.22, 'scallop', 'scalRib', 'wrack', 'often', 4,
      'Ribbed fan with hinge ears — the postcard shell', { ribs: 9, arc: 1.85, ears: 1, cup: 1.0 }),
    F('Oyster', 'Oyster Shell', 'shell', 'valve', 11, 0.30, 'oyster', 'oysterIn', 'tide', 'often', 4,
      'Rough frilled cup, chalk outside, nacre in the bowl', { elong: 1.30, ends: 2.2, taper: 0.16, rings: 4, cup: 1.25, rough: 0.55, bowl: 1 }),
    F('RazorClam', 'Razor Shell', 'shell', 'valve', 13, 0.13, 'razor', 'clamRing', 'tide', 'often', 5,
      'Long flat-sided blade, olive periostracum', { elong: 5.2, ends: 7, taper: 0.06, rings: 3, cup: 0.6 }),
    F('JingleShell', 'Jingle Shell', 'shell', 'valve', 4, 0.09, 'jingle', 'clamRing', 'wrack', 'often', 8,
      'Translucent gold disc, one worn hole — rattles in a pocket', { elong: 1.08, ends: 2.2, taper: 0.05, rings: 2, cup: 0.5, hole: 1 }),
    F('Periwinkle', 'Periwinkle', 'shell', 'spiral', 2.5, 0.62, 'snail', 'snailBand', 'tide', 'common', 12,
      'Small dark coiled snail — the one under every rock', { whorls: 2.7, spire: 0.55, shrink: 0.62 }),
    F('Dogwhelk', 'Dog Whelk', 'shell', 'spiral', 3.5, 0.72, 'whelk', 'snailBand', 'tide', 'often', 8,
      'Tall banded spire with axial ribs', { whorls: 3.2, spire: 0.80, shrink: 0.60, ribs: 9 }),
    F('MoonSnail', 'Moon Snail', 'shell', 'spiral', 6, 0.44, 'moon', 'snailBand', 'wrack', 'scarce', 3,
      'Fat low body whorl, wide aperture, sunken spire', { whorls: 2.3, spire: 0.22, shrink: 0.52 }),
    // ---- remains & moults
    F('SandDollar', 'Sand Dollar', 'remains', 'disc', 7, 0.13, 'bone', 'boneDp', 'wrack', 'scarce', 4,
      'Petal star on the disc — snaps if you pocket it', { petals: 5 }),
    F('GreenUrchin', 'Urchin Test', 'remains', 'disc', 5, 0.42, 'urchin', 'boneDp', 'tide', 'scarce', 4,
      'Bare green test, tubercle rows where the spines were', { tuber: 1, dome: 1.6 }),
    F('Starfish', 'Sea Star', 'remains', 'star', 15, 0.16, 'star', 'chitinPale', 'tide', 'scarce', 2,
      'Five arms, pebbled back, one arm always short', { arms: 5, disc: 0.34, bumps: 1 }),
    F('BrittleStar', 'Brittle Star', 'remains', 'star', 13, 0.09, 'brittle', 'boneDp', 'tide', 'rare', 2,
      'Tiny disc, whip arms — comes up in the trap bait', { arms: 5, disc: 0.20, thin: 1, bands: 1 }),
    F('CrabMoult', 'Crab Moult', 'remains', 'carapace', 12, 0.30, 'chitin', 'chitinPale', 'wrack', 'often', 3,
      'Empty rock-crab carapace, legs shed with it', { legs: 1, spines: 7 }),
    F('GreenCrabShell', 'Green Crab Shell', 'remains', 'carapace', 7, 0.28, 'chitinGrn', 'chitinPale', 'wrack', 'common', 5,
      'The invader — smaller, squarer, five points a side', { legs: 0, spines: 5 }),
    F('LobsterClaw', 'Lobster Claw', 'remains', 'claw', 19, 0.30, 'chitinRed', 'chitinPale', 'wrack', 'scarce', 2,
      'Crusher claw off a boiled one — thrown from a boat', { crusher: 1 }),
    // ---- driftwood & bone
    F('Driftwood', 'Drift Log', 'wood', 'rod', 110, 0.26, 'drift', 'driftDp', 'wrack', 'common', 2,
      'Barked-off log, sun-silvered, checked along the grain', { knots: 3, splits: 2, taper: 0.62, bow: 0.13 }),
    F('DriftBranch', 'Drift Branch', 'wood', 'rod', 70, 0.15, 'drift', 'driftDp', 'wrack', 'common', 3,
      'Forked limb, bark gone, ends worn to points', { forks: 2, knots: 2, taper: 0.42, bow: 0.24 }),
    F('DriftRoot', 'Root Wad', 'wood', 'rootWad', 55, 0.34, 'drift', 'driftDp', 'upper', 'often', 2,
      'Gnarled root knot — the wrack line catches on it', { roots: 5 }),
    F('DriftPlank', 'Drift Plank', 'wood', 'rod', 85, 0.10, 'drift', 'driftDp', 'upper', 'often', 3,
      'Sawn plank off something — nail holes, split end', { flat: 1, wRel: 0.13, ends: 8, holes: 2, splits: 1, taper: 0.94 }),
    F('WeatheredBone', 'Weathered Bone', 'wood', 'rod', 28, 0.22, 'bone', 'boneDp', 'upper', 'scarce', 3,
      'Sun-bleached long bone, knobbed at both ends', { knobEnds: 1, hollow: 1, taper: 0.8, bow: 0.10 }),
    F('GullFeather', 'Gull Feather', 'wood', 'feather', 20, 0.06, 'feather', 'featherGry', 'upper', 'common', 6,
      'White with a grey vane and a black tip band', { split: 1 }),
    // ---- cases & crusts
    F('MermaidsPurse', "Mermaid's Purse", 'case', 'pouch', 9, 0.16, 'purse', 'driftDp', 'wrack', 'scarce', 3,
      'Skate egg case — black leather with four horns', { horns: 4 }),
    F('WhelkEggMass', 'Whelk Egg Case', 'case', 'cluster', 11, 0.30, 'parchment', 'boneDp', 'wrack', 'scarce', 2,
      'Papery string of capsules — the sea wash ball', { kind: 'bubble', n: 14 }),
    F('BarnacleCluster', 'Barnacle Crust', 'case', 'cluster', 9, 0.26, 'chalk', 'boneDp', 'tide', 'common', 4,
      'Chalk cones crusted on a shell scrap', { kind: 'barnacle', n: 11, base: 1 }),
    // ---- stone & coal
    F('WaveCobble', 'Wave Cobble', 'stone', 'cobble', 14, 0.46, 'granite', 'vein', 'tide', 'common', 4,
      'Rolled granite, one quartz band, no edges left', { vein: 1 }),
    F('PebbleScatter', 'Pebble Scatter', 'stone', 'cluster', 26, 0.16, 'graniteWm', 'granite', 'tide', 'common', 3,
      'Handful of shingle — reads as texture, not a prop', { kind: 'pebble', n: 13 }),
    F('SeaCoal', 'Sea Coal', 'stone', 'cluster', 12, 0.22, 'char', 'granite', 'wrack', 'often', 4,
      'Black angular bits — old fire, old cargo', { kind: 'coal', n: 6 }),
    // ---- flotsam
    F('SeaGlass', 'Sea Glass', 'flotsam', 'shard', 3, 0.32, 'glassGrn', 'glassBlu', 'wrack', 'often', 10,
      'Frosted and edge-worn — green, blue or white', { sides: 6, jag: 0.30 }),
    F('GlassBottle', 'Glass Bottle', 'flotsam', 'bottle', 26, 0.30, 'bottle', 'bottleLip', 'upper', 'often', 2,
      'Green bottle, neck up the beach, sand inside', { neck: 0.34 }),
    F('RopeScrap', 'Rope Scrap', 'flotsam', 'coil', 24, 0.20, 'ropeOr', 'ropeCore', 'wrack', 'common', 3,
      'Cut length of poly warp, frayed at one end', { turns: 2.3, fray: 1 }),
    F('NetScrap', 'Net Scrap', 'flotsam', 'mesh', 28, 0.05, 'twine', 'ropeOr', 'wrack', 'often', 3,
      'Torn mesh panel, knots at every crossing', { mesh: 5 }),
    F('NetFloat', 'Net Float', 'flotsam', 'ball', 11, 0.62, 'foam', 'foamMark', 'wrack', 'often', 4,
      'Cracked plastic float, painted band, line gone', { crack: 1, band: 1 }),
    F('TrapLath', 'Trap Lath', 'flotsam', 'rod', 62, 0.07, 'lath', 'lathPaint', 'wrack', 'common', 4,
      'Broken trap slat, paint left in the grain', { flat: 1, wRel: 0.075, ends: 8, holes: 2, paint: 1, taper: 0.97 }),
    F('RustedCan', 'Rusted Can', 'flotsam', 'can', 12, 0.44, 'rust', 'rustDp', 'upper', 'often', 3,
      'Crushed tin, rusted through at the seam', { crush: 0.5 }),
  ];
  const byKey = {}; FINDS.forEach(f => byKey[f.key] = f);

  // drawn px from true cm — ONE compressive rule for the whole pack
  const READ_K = 4.7, READ_E = 0.62;
  const drawnPx = (cm) => READ_K * Math.pow(Math.max(0.5, cm), READ_E);
  const VAR_S = [0.88, 1.0, 1.14];
  /* Cell reach — how far past the long axis a form's outermost geometry actually goes. Crab legs
     run 1.4× the shell width, a rope's fray tail and a root's third segment overshoot the coil,
     end caps overrun by a pixel. The cell is sized from L × reach so no cell can ever clip; the
     rig asserts it (report.clipped) and the whole 864-state grid must come back clean. */
  const REACH = { carapace: 1.58, coil: 1.52, rootWad: 1.52, cluster: 1.32, pouch: 1.26, rod: 1.22,
    bottle: 1.22, claw: 1.18, can: 1.16, fanValve: 1.14, spiral: 1.14, feather: 1.12, star: 1.12 };
  const reachOf = (f) => REACH[f.form] || 1.08;

  function paramsOf(key, variant) {
    const f = byKey[key] || FINDS[0], v = Math.max(0, Math.min(2, variant | 0));
    const s = VAR_S[v], L = drawnPx(f.cm) * s;
    return Object.assign({}, f.p, { seed: hashStr(key) + v * 7919, s, L, H: L * f.hRel, cm: f.cm * s, v });
  }
  function cellOf(key) {
    const f = byKey[key] || FINDS[0], L = drawnPx(f.cm) * VAR_S[2], H = L * f.hRel;
    const S = L * reachOf(f);
    const w = Math.ceil(S) + 7, half = Math.ceil(S * Q / 2) + 3;
    return { w, h: half + Math.ceil(S * Q / 2 + H) + 4, pvx: Math.round(w / 2), pvy: 0, _half: half, L, H, reach: reachOf(f) };
  }
  function cellFull(key) { const c = cellOf(key); c.pvy = c.h - c._half; return c; }

  // ---- rng / dither ---------------------------------------------------------
  function hashStr(s) { let h = 2166136261; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); } return (h >>> 0) % 100000; }
  function mulberry(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
  function hash2(x, y, s) { let h = (x * 374761393 + y * 668265263 + s * 2246822519) | 0; h = Math.imul(h ^ (h >>> 13), 1274126177); return ((h ^ (h >>> 16)) >>> 0) / 4294967296; }
  const B4 = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]];
  const bayer = (x, y) => (B4[y & 3][x & 3] + 0.5) / 16;

  // ---- height-field buffer --------------------------------------------------
  function Buf(w, h) { this.w = w; this.h = h; this.z = new Float32Array(w * h); this.mat = new Uint8Array(w * h); }
  Buf.prototype.i = function (x, y) { return y * this.w + x; };
  Buf.prototype.inb = function (x, y) { return x >= 0 && x < this.w && y >= 0 && y < this.h; };
  Buf.prototype.put = function (x, y, z, m) {
    if (!this.inb(x, y)) return; const i = y * this.w + x;
    if (this.mat[i] === 0 || z > this.z[i]) { this.z[i] = z; this.mat[i] = m; }
  };
  Buf.prototype.mark = function (x, y, m, dz) {                 // recolour existing pixels only
    if (!this.inb(x, y)) return; const i = y * this.w + x;
    if (this.mat[i]) { this.mat[i] = m; if (dz) this.z[i] = Math.max(0, this.z[i] + dz); }
  };
  Buf.prototype.cut = function (x, y) { if (!this.inb(x, y)) return; const i = y * this.w + x; this.mat[i] = 0; this.z[i] = 0; };

  // scan a local-space box; hands back local (u,v) per screen pixel — ground rotate, then project
  function scan(b, C, u0, u1, v0, v1, fn) {
    const cx = C.cx, cy = C.cy, c = C.c, s = C.s;
    const xs = [], ys = [];
    [[u0, v0], [u1, v0], [u0, v1], [u1, v1]].forEach(p => { xs.push(cx + p[0] * c - p[1] * s); ys.push(cy + (p[0] * s + p[1] * c) * Q); });
    const x0 = Math.max(0, Math.floor(Math.min.apply(null, xs)) - 1), x1 = Math.min(b.w - 1, Math.ceil(Math.max.apply(null, xs)) + 1);
    const y0 = Math.max(0, Math.floor(Math.min.apply(null, ys)) - 1), y1 = Math.min(b.h - 1, Math.ceil(Math.max.apply(null, ys)) + 1);
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const up = x - cx, vp = (y - cy) / Q;
      fn(x, y, up * c + vp * s, -up * s + vp * c);
    }
  }
  // elliptical dome
  function dome(b, C, cu, cv, ru, rv, H, m, o) {
    o = o || {}; ru = Math.max(0.6, ru); rv = Math.max(0.6, rv);
    scan(b, C, cu - ru, cu + ru, cv - rv, cv + rv, (x, y, u, v) => {
      const du = (u - cu) / ru, dv = (v - cv) / rv, d2 = du * du + dv * dv;
      if (d2 > 1) return;
      let z = H * Math.pow(Math.max(0, 1 - d2), o.pow == null ? 0.5 : o.pow);
      if (o.zf) z = o.zf(u - cu, v - cv, z, Math.sqrt(d2));
      b.put(x, y, z + (o.lift || 0), m);
    });
  }
  // swept circle (log / arm / rope / quill); flat=1 gives a flattened top (planks, laths)
  function capsule(b, C, u0, v0, u1, v1, r0, r1, m, o) {
    o = o || {}; const rM = Math.max(r0, r1), hs = o.hs == null ? 1 : o.hs;
    scan(b, C, Math.min(u0, u1) - rM, Math.max(u0, u1) + rM, Math.min(v0, v1) - rM, Math.max(v0, v1) + rM, (x, y, u, v) => {
      const du = u1 - u0, dv = v1 - v0, L2 = du * du + dv * dv || 1;
      let t = ((u - u0) * du + (v - v0) * dv) / L2;
      if (o.ends) { if (t < 0 || t > 1) return; } else t = Math.max(0, Math.min(1, t));
      const r = Math.max(0.5, r0 + (r1 - r0) * t), d = Math.hypot(u - (u0 + du * t), v - (v0 + dv * t));
      if (d > r) return;
      const k = Math.max(0, 1 - (d / r) * (d / r));
      let z = hs * r * (o.flat ? Math.pow(k, 0.22) : Math.sqrt(k));
      if (o.zf) z = o.zf(t, d / r, z);
      b.put(x, y, z + (o.lift || 0), m);
    });
  }
  function polyz(b, C, pts, H, m, o) {
    o = o || {};
    let u0 = 1e9, u1 = -1e9, v0 = 1e9, v1 = -1e9;
    pts.forEach(p => { u0 = Math.min(u0, p[0]); u1 = Math.max(u1, p[0]); v0 = Math.min(v0, p[1]); v1 = Math.max(v1, p[1]); });
    const n = pts.length;
    scan(b, C, u0, u1, v0, v1, (x, y, u, v) => {
      let inside = false, dmin = 1e9;
      for (let i = 0, j = n - 1; i < n; j = i++) {
        const xi = pts[i][0], yi = pts[i][1], xj = pts[j][0], yj = pts[j][1];
        if ((yi > v) !== (yj > v) && u < (xj - xi) * (v - yi) / (yj - yi) + xi) inside = !inside;
        const ex = xj - xi, ey = yj - yi, L2 = ex * ex + ey * ey || 1;
        const t = Math.max(0, Math.min(1, ((u - xi) * ex + (v - yi) * ey) / L2));
        dmin = Math.min(dmin, Math.hypot(u - (xi + ex * t), v - (yi + ey * t)));
      }
      if (!inside) return;
      const k = Math.min(1, dmin / Math.max(1.2, H * 0.9));
      b.put(x, y, H * Math.pow(k, o.pow == null ? 0.45 : o.pow) + (o.lift || 0), m);
    });
  }
  function carve(b, C, cu, cv, ru, rv) {
    scan(b, C, cu - ru, cu + ru, cv - rv, cv + rv, (x, y, u, v) => {
      const du = (u - cu) / ru, dv = (v - cv) / rv; if (du * du + dv * dv <= 1) b.cut(x, y);
    });
  }
  function markEll(b, C, cu, cv, ru, rv, m, dz) {
    scan(b, C, cu - ru, cu + ru, cv - rv, cv + rv, (x, y, u, v) => {
      const du = (u - cu) / ru, dv = (v - cv) / rv; if (du * du + dv * dv <= 1) b.mark(x, y, m, dz);
    });
  }

  // ---- forms ----------------------------------------------------------------
  // Mat slots: 1 body · 2 accent · 3 = accent2 (bowl / paint / band) · 4 sand.
  const M_B = 1, M_A = 2, M_C = 3, M_S = 4;

  function valve(b, C, P) {
    const ru = P.L / 2, rv = ru / (P.elong || 1.4), H = P.H, ends = P.ends || 2.4;
    const rough = (P.rough || 0) * (1 - C.wear * 0.4), rng = C.rng, ph = rng() * 6.28;
    const hv = (u) => {
      const t = Math.abs(u) / ru; if (t > 1) return 0;
      let k = Math.pow(Math.max(0, 1 - Math.pow(t, ends)), 0.5) * (1 + (P.taper || 0) * (u / ru));
      if (rough) k *= 1 + rough * 0.20 * Math.sin(u * 0.9 + ph) + rough * 0.14 * Math.sin(u * 2.3 - ph);
      return Math.max(0, rv * k);
    };
    const rings = (P.rings || 0) * (1 - C.wear * 0.5);
    scan(b, C, -ru - 1, ru + 1, -rv * 1.4, rv * 1.4, (x, y, u, v) => {
      const w = hv(u); if (w <= 0 || Math.abs(v) > w) return;
      const du = u / ru, dv = v / w;
      let z = H * (P.cup || 1) * Math.sqrt(Math.max(0, 1 - du * du * 0.72)) * Math.sqrt(Math.max(0, 1 - dv * dv * 0.86));
      const d = Math.hypot((u + ru * 0.62) / (ru * 1.5), v / (rv * 1.5));   // distance from the umbo
      if (rings) z += Math.sin(d * rings * 2.1) * H * 0.11 * (1 - C.wear * 0.6);
      if (rough) z += (hash2(Math.round(u * 3), Math.round(v * 3), P.seed) - 0.5) * H * 0.30 * rough;
      z += Math.exp(-Math.pow((u + ru * 0.74) / (ru * 0.24), 2)) * H * 0.34;  // umbo
      b.put(x, y, Math.max(0.4, z), M_B);
    });
    if (P.edgeBand) edgeMark(b, P.edgeBand, M_A);
    if (rings > 3) {                                                          // one read-through ring
      const rr = ru * 0.66;
      scan(b, C, -ru, ru, -rv, rv, (x, y, u, v) => {
        const d = Math.hypot((u + ru * 0.62) / 1.5, v / 1.5);
        if (Math.abs(d - rr / 1.5) < 0.55) b.mark(x, y, M_A, -0.2);
      });
    }
    if (P.bowl) markEll(b, C, ru * 0.10, 0, ru * 0.44, rv * 0.44, M_C, 0);
    if (P.lip) { const w = hv(ru * 0.52); markEll(b, C, ru * 0.56, w * 0.55, ru * 0.26, rv * 0.20, M_C, 0); }
    if (P.hole) carve(b, C, -ru * 0.30, rv * 0.10, 1.2, 1.2);
  }

  function fanValve(b, C, P) {
    const R = P.L * 0.90, H = P.H, A = (P.arc || 2.0) / 2, ou = -P.L * 0.42;
    const ribs = Math.max(6, Math.round(P.ribs || 12)), rel = 1 - C.wear * 0.45;
    scan(b, C, ou - 2, ou + R + 3, -R, R, (x, y, u, v) => {
      const du = u - ou, a = Math.atan2(v, du), d = Math.hypot(du, v);
      if (Math.abs(a) > A || d < 0.5) return;
      const ea = Math.abs(a) / A;
      const edge = R * (0.96 + 0.035 * Math.cos(a * ribs)) * (1 - 0.13 * ea * ea);   // scalloped margin
      if (d > edge) return;
      const t = d / edge;
      let z = H * (P.cup || 1) * Math.sqrt(Math.max(0.05, 1 - Math.pow((t - 0.58) / 0.72, 2) * 0.85))
                              * Math.sqrt(Math.max(0.10, 1 - ea * ea * 0.62));
      z += Math.cos(a * ribs) * H * 0.30 * Math.min(1, t * 2.2) * rel;             // radial ribs
      b.put(x, y, Math.max(0.4, z), M_B);
      if (Math.cos(a * ribs) < -0.55 && t > 0.28) b.mark(x, y, M_A, 0);
    });
    if (P.ears) {                                                                  // hinge ears, tucked in
      capsule(b, C, ou + 0.6, -1.2, ou + R * 0.10, -R * 0.30, 1.9, 1.1, M_B, { hs: H / 2.2 });
      capsule(b, C, ou + 0.6, 1.2, ou + R * 0.10, R * 0.30, 1.9, 1.1, M_B, { hs: H / 2.2 });
      markEll(b, C, ou + 1.2, 0, 2.0, 1.6, M_A, 0);
    }
  }

  function spiral(b, C, P) {
    const R = P.L * 0.5, H = P.H, wh = P.whorls || 2.6, shrink = P.shrink || 0.6;
    const steps = Math.max(40, Math.round(R * 9));
    const apexU = R * (0.34 * (P.spire || 0.5)), apexV = -R * 0.10;
    for (let i = steps; i >= 0; i--) {
      const t = i / steps, a = t * wh * Math.PI * 2 + 0.6;
      const rad = Math.max(0.7, R * 0.46 * Math.pow(shrink, t * wh));
      const dist = (R - rad * 1.02) * Math.pow(1 - t, 0.86);
      const u = apexU + Math.cos(a) * dist * 0.98 - dist * 0.10, v = apexV + Math.sin(a) * dist;
      const lift = (P.spire || 0.5) * H * 0.55 * t;
      const rr = rad * (P.ribs ? 1 + 0.10 * Math.sin(a * (P.ribs / (wh || 1))) : 1);
      dome(b, C, u, v, rr, rr, Math.min(H - lift * 0.4, rad * 1.55), M_B, { lift: lift, pow: 0.5 });
    }
    for (let i = steps; i >= 0; i--) {                                        // suture — the seam between whorls
      const t = i / steps, a = t * wh * Math.PI * 2 + 0.6;
      const rad = Math.max(0.7, R * 0.46 * Math.pow(shrink, t * wh));
      const dist = (R - rad * 1.02) * Math.pow(1 - t, 0.86);
      const su = apexU + Math.cos(a) * (dist - rad * 0.84) * 0.98, sv = apexV + Math.sin(a) * (dist - rad * 0.84);
      if (t > 0.05 && t < 0.95) markEll(b, C, su, sv, Math.max(0.7, rad * 0.28), Math.max(0.7, rad * 0.28), M_A, 0);
    }
    const a0 = 0.6, rad0 = R * 0.46;                                          // aperture at the outer end
    markEll(b, C, apexU + Math.cos(a0) * (R - rad0) * 0.98 - (R - rad0) * 0.10 + rad0 * 0.35,
      apexV + Math.sin(a0) * (R - rad0) + rad0 * 0.30, rad0 * 0.62, rad0 * 0.52, M_A, -rad0 * 0.35);
  }

  function disc(b, C, P) {
    const r = P.L * 0.5, H = P.H, pw = P.dome ? 0.62 : 0.36;
    const zBase = (d) => H * Math.pow(Math.max(0, 1 - d * d), pw);
    dome(b, C, 0, 0, r, r, H, M_B, { pow: pw });
    if (P.petals) {                                                             // the five-petal star
      for (let i = 0; i < P.petals; i++) {
        const a = -Math.PI / 2 + i * Math.PI * 2 / P.petals, ca = Math.cos(a), sa = Math.sin(a);
        const cu = ca * r * 0.46, cv = sa * r * 0.46;
        scan(b, C, cu - r * 0.55, cu + r * 0.55, cv - r * 0.55, cv + r * 0.55, (x, y, u, v) => {
          const du = (u - cu) * ca + (v - cv) * sa, dv = -(u - cu) * sa + (v - cv) * ca;
          if (Math.pow(du / (r * 0.42), 2) + Math.pow(dv / (r * 0.14), 2) <= 1) b.mark(x, y, M_A, -H * 0.34);
        });
      }
      markEll(b, C, 0, 0, r * 0.14, r * 0.14, M_A, -H * 0.22);
    }
    if (P.tuber) {                                                              // urchin: 5 ambulacra as relief, not beads
      scan(b, C, -r, r, -r, r, (x, y, u, v) => {
        const d = Math.hypot(u, v) / r; if (d > 1) return;
        const a = Math.atan2(v, u);
        const band = Math.cos(a * 5);
        b.put(x, y, zBase(d) + H * 0.13 * band * Math.min(1, (1 - d) * 3.2), band < -0.45 ? M_A : M_B);
      });
      markEll(b, C, 0, 0, r * 0.20, r * 0.20, M_A, -H * 0.30);
    }
  }

  function star(b, C, P) {
    const R = P.L * 0.5, H = P.H, n = P.arms || 5, rd = R * (P.disc || 0.3), rng = C.rng;
    const wArm = P.thin ? R * 0.075 : R * 0.20;
    for (let i = 0; i < n; i++) {
      const a = -Math.PI / 2 + i * Math.PI * 2 / n + (rng() - 0.5) * 0.22;
      const len = R * (i === n - 1 ? 0.78 : 0.94 + (rng() - 0.5) * 0.14);
      const curl = (rng() - 0.5) * 0.5, mid = len * 0.55;
      const mu = Math.cos(a + curl * 0.4) * mid, mv = Math.sin(a + curl * 0.4) * mid;
      const tu = Math.cos(a + curl) * len, tv = Math.sin(a + curl) * len;
      capsule(b, C, 0, 0, mu, mv, wArm, wArm * 0.72, M_B, { hs: H / Math.max(1, wArm) * 0.85 });
      capsule(b, C, mu, mv, tu, tv, wArm * 0.72, Math.max(0.55, wArm * 0.20), M_B, { hs: H / Math.max(1, wArm) * 0.75 });
      if (P.bumps) for (let k = 1; k <= 4; k++) {
        const t = k / 5, u = mu * (1 - t) + tu * t, v = mv * (1 - t) + tv * t;
        dome(b, C, u, v, 0.9, 0.9, H * 0.12, M_A, { lift: H * 0.55 });
      }
      if (P.bands) for (let k = 1; k <= 6; k++) {
        const t = k / 7, u = mu * (1 - t) + tu * t, v = mv * (1 - t) + tv * t;
        markEll(b, C, u, v, 0.75, 0.75, M_A, 0);
      }
    }
    dome(b, C, 0, 0, rd, rd, H, M_B, { pow: 0.42 });
    if (P.bumps) markEll(b, C, 0, 0, rd * 0.24, rd * 0.24, M_A, -H * 0.12);
  }

  function carapace(b, C, P) {
    const w = P.L * 0.5, d = w * 0.70, H = P.H, sp = P.spines || 6;
    if (P.legs) for (let s = -1; s <= 1; s += 2) for (let i = 0; i < 3; i++) {     // legs first: shell overlaps them
      const su = s * w * 0.70, sv = -d * 0.24 + i * d * 0.40;
      const ku = su + s * w * 0.42, kv = sv + d * 0.34;
      capsule(b, C, su, sv, ku, kv, 1.5, 1.0, M_A, { hs: 1.0 });
      capsule(b, C, ku, kv, ku + s * w * 0.30, kv - d * 0.16, 1.0, 0.55, M_A, { hs: 1.0 });
    }
    scan(b, C, -w - 1, w + 1, -d - 1, d + 1, (x, y, u, v) => {
      const teeth = v < 0 ? 1 + 0.09 * Math.cos(u / w * sp * 1.3) : 1;             // toothed front margin
      const du = u / (w * teeth), dv = v / d;
      const r2 = du * du + dv * dv * (v < 0 ? 1.22 : 0.84);
      if (r2 > 1) return;
      let z = H * Math.pow(Math.max(0, 1 - r2), 0.34);
      z -= H * 0.06 * Math.cos(u / w * 3.2) * (v > 0 ? 1 : 0.4);                   // gastric ridges
      b.put(x, y, Math.max(0.4, z), M_B);
      if (v < -d * 0.66) b.mark(x, y, M_A, 0);                                     // dark front margin
    });
    markEll(b, C, -w * 0.30, -d * 0.56, 1.6, 1.2, M_A, -H * 0.18);                 // eye sockets
    markEll(b, C, w * 0.30, -d * 0.56, 1.6, 1.2, M_A, -H * 0.18);
    for (let i = 0; i < 3; i++) markEll(b, C, (-1 + i) * w * 0.30, d * 0.58, w * 0.13, d * 0.10, M_A, -H * 0.10);
  }

  function claw(b, C, P) {
    const L = P.L, H = P.H, pw = L * (P.crusher ? 0.30 : 0.24);
    dome(b, C, -L * 0.16, 0, L * 0.34, pw, H, M_B, { pow: 0.44 });            // palm
    capsule(b, C, L * 0.10, -pw * 0.30, L * 0.48, -pw * 0.42, pw * 0.62, pw * 0.20, M_B, { hs: H / Math.max(1, pw * 0.62) * 0.9 }); // fixed finger
    capsule(b, C, L * 0.08, pw * 0.34, L * 0.44, pw * 0.16, pw * 0.50, pw * 0.18, M_B, { hs: H / Math.max(1, pw * 0.5) * 0.8 });    // dactyl
    capsule(b, C, -L * 0.50, 0, -L * 0.24, 0, pw * 0.34, pw * 0.52, M_B, { hs: H / Math.max(1, pw * 0.5) * 0.7 });                  // joint stub
    markEll(b, C, L * 0.47, -pw * 0.42, pw * 0.26, pw * 0.20, M_A, 0);
    markEll(b, C, L * 0.43, pw * 0.16, pw * 0.24, pw * 0.18, M_A, 0);
    markEll(b, C, -L * 0.50, 0, pw * 0.30, pw * 0.30, M_A, 0);
    if (P.crusher) for (let i = 0; i < 4; i++) {                              // crusher molars
      const t = i / 3;
      markEll(b, C, L * (0.14 + t * 0.26), -pw * 0.10 + t * 0.4, pw * 0.11, pw * 0.09, M_A, 0);
    }
  }

  function rod(b, C, P) {
    const L = P.L, H = P.H, rng = C.rng, flat = P.flat || 0;
    // round rods: radius = half the declared height. Flat stock: wRel sets the half-width and
    // the thickness stays H. Caps live INSIDE L, so a fat log never outgrows its cell.
    const r0 = flat ? (P.wRel || 0.13) * L : H * 0.5, r1 = Math.max(0.7, r0 * (P.taper == null ? 0.6 : P.taper));
    const hs = flat ? H / Math.max(1, r0) : 2;
    const bow = (P.bow || 0) * L * 0.5, u0 = -L / 2 + r0, u1 = L / 2 - r1;
    const segs = 6, pts = [];
    for (let i = 0; i <= segs; i++) { const t = i / segs; pts.push([u0 + (u1 - u0) * t, Math.sin(t * Math.PI) * bow]); }
    for (let i = 0; i < segs; i++) {
      const t = i / segs, r = r0 + (r1 - r0) * t, rn = r0 + (r1 - r0) * (i + 1) / segs;
      capsule(b, C, pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], r, rn, M_B, { hs: hs, flat: !!flat });
    }
    if (P.ends >= 6) { carve(b, C, u0 - r0, pts[0][1], r0 * 0.92, r0 * 1.4); carve(b, C, u1 + r1, pts[segs][1], r1 * 0.92, r1 * 1.4); }
    for (let i = 0; i < (P.splits || 0); i++) {                               // checks along the grain
      const off = (i - (P.splits - 1) / 2) * r0 * 0.7, a = u0 + L * (0.10 + rng() * 0.14), bx = a + L * (0.24 + rng() * 0.3);
      capsule(b, C, a, off * 0.6, Math.min(u1, bx), off, 0.55, 0.55, M_A, { hs: 0.2, lift: r0 * hs * 0.55 });
    }
    for (let i = 0; i < (P.knots || 0); i++) {                                // knots
      const u = u0 + (u1 - u0) * ((i + 0.5) / (P.knots || 1)) + (rng() - 0.5) * L * 0.05;
      const rr = r0 * (0.34 + rng() * 0.18);
      dome(b, C, u, (rng() - 0.5) * r0 * 0.5, rr, rr, r0 * 0.4, M_A, { lift: r0 * hs * 0.5 });
    }
    for (let i = 0; i < (P.forks || 0); i++) {
      const t = 0.18 + i * 0.38, u = u0 + (u1 - u0) * t, v = Math.sin(t * Math.PI) * bow;
      const a = (i % 2 ? 1 : -1) * (0.5 + rng() * 0.4), fl = L * (0.26 + rng() * 0.14);
      capsule(b, C, u, v, u + Math.cos(a) * fl * 0.7, v + Math.sin(a) * fl, r0 * 0.62, r0 * 0.24, M_B, { hs: hs });
    }
    for (let i = 0; i < (P.holes || 0); i++) {
      const u = u0 + (u1 - u0) * (0.16 + i * 0.66);
      markEll(b, C, u, 0, 1.15, 1.15, M_A, -0.6);
    }
    if (P.knobEnds) {
      dome(b, C, u0, pts[0][1], r0 * 1.42, r0 * 1.26, r0 * hs * 0.62, M_B, { pow: 0.5 });
      dome(b, C, u1, pts[segs][1], r1 * 1.66, r1 * 1.44, r1 * hs * 0.70, M_B, { pow: 0.5 });
      if (P.hollow) markEll(b, C, u1 + r1 * 0.5, pts[segs][1], r1 * 0.55, r1 * 0.45, M_A, -r1 * 0.5);
    }
    if (P.paint) {                                                            // paint left in the grain
      const s0 = u0 + rng() * L * 0.2;
      scan(b, C, s0, s0 + L * 0.5, -r0 * 1.2, r0 * 1.2, (x, y, u, v) => {
        if (u < s0 || u > s0 + L * 0.5) return;
        if (hash2(x, y, P.seed) < 0.62) b.mark(x, y, M_C, 0);
      });
    }
  }

  function rootWad(b, C, P) {   // named rootWad: `root` is the IIFE's global handle
    const R = P.L * 0.5, H = P.H, n = Math.max(3, P.roots || 5), rng = C.rng, kx = -R * 0.16;
    dome(b, C, kx, 0, R * 0.46, R * 0.34, H, M_B, { pow: 0.5 });                  // the knot: dominant mass
    dome(b, C, kx + R * 0.24, -R * 0.10, R * 0.30, R * 0.23, H * 0.86, M_B, { pow: 0.5 });
    dome(b, C, kx - R * 0.20, R * 0.13, R * 0.25, R * 0.19, H * 0.72, M_B, { pow: 0.5 });
    const a0 = -0.75 + rng() * 0.5;
    for (let i = 0; i < n; i++) {                                                 // roots fan to ONE side — torn out
      const a = a0 + (i / (n - 1) - 0.5) * 2.0 + (rng() - 0.5) * 0.30;
      const l = R * (0.50 + rng() * 0.80), r0 = Math.max(1.1, H * 0.30 * (0.7 + rng() * 0.5));
      const p1 = [kx + Math.cos(a) * l * 0.45, Math.sin(a) * l * 0.45];
      const a2 = a + (rng() - 0.5) * 1.15;
      const p2 = [p1[0] + Math.cos(a2) * l * 0.40, p1[1] + Math.sin(a2) * l * 0.40];
      capsule(b, C, kx, 0, p1[0], p1[1], r0, r0 * 0.70, M_B, { hs: 1.5 });
      capsule(b, C, p1[0], p1[1], p2[0], p2[1], r0 * 0.70, r0 * 0.42, M_B, { hs: 1.5 });
      if (rng() < 0.7) {
        const a3 = a2 + (rng() - 0.5) * 1.5;
        capsule(b, C, p2[0], p2[1], p2[0] + Math.cos(a3) * l * 0.30, p2[1] + Math.sin(a3) * l * 0.30, r0 * 0.42, r0 * 0.22, M_B, { hs: 1.5 });
      }
      if (rng() < 0.6) markEll(b, C, p1[0], p1[1], r0 * 0.55, r0 * 0.55, M_A, -0.5);
    }
    for (let i = 0; i < 5; i++) markEll(b, C, kx + (rng() - 0.5) * R * 0.7, (rng() - 0.5) * R * 0.5, H * 0.22, H * 0.18, M_A, -H * 0.14);
  }

  function feather(b, C, P) {
    const L = P.L, H = Math.max(1.2, P.H), rng = C.rng, vw = L * 0.14;
    const quill = (t) => Math.sin(t * 0.6) * L * 0.06;
    scan(b, C, -L / 2 - 1, L / 2 + 1, -vw - 1, vw + 1, (x, y, u, v) => {
      const t = (u + L / 2) / L; if (t < 0 || t > 1) return;
      const c = quill(t), w = vw * Math.pow(Math.sin(Math.min(1, t * 1.06) * Math.PI), 0.55) * (t < 0.18 ? t / 0.18 : 1);
      const dv = v - c; if (Math.abs(dv) > w) return;
      if (P.split && t > 0.52 && t < 0.66 && dv > w * 0.2) return;            // one split in the vane
      let z = H * (1 - Math.pow(Math.abs(dv) / Math.max(0.6, w), 1.6)) * 0.55 + H * 0.45 * Math.exp(-Math.pow(dv / 1.1, 2));
      b.put(x, y, Math.max(0.35, z), dv < -w * 0.16 && t > 0.30 ? M_A : M_B); // grey vane on the leading side
      if (hash2(x, y, P.seed) < 0.16 && Math.abs(dv) > w * 0.45) b.mark(x, y, M_A, 0);  // barb texture
    });
    capsule(b, C, -L * 0.48, quill(0.02), L * 0.44, quill(0.96), 0.85, 0.45, M_B, { hs: 1.1 });
    markEll(b, C, L * 0.40, quill(0.9), L * 0.06, vw * 0.5, M_A, 0);          // tip band
  }

  function pouch(b, C, P) {
    const L = P.L, H = P.H, ru = L * 0.34, rv = L * 0.24, rng = C.rng;
    dome(b, C, 0, 0, ru, rv, H, M_B, {
      pow: 0.5, zf: (u, v, z) => z * (1 + 0.10 * Math.sin(u * 1.4 + v * 0.9)),
    });
    for (let i = 0; i < (P.horns || 4); i++) {
      const su = (i < 2 ? -1 : 1) * ru * 0.82, sv = (i % 2 ? 1 : -1) * rv * 0.62;
      const a = Math.atan2(sv, su), l = L * 0.24;
      const mu = su + Math.cos(a) * l * 0.6, mv = sv + Math.sin(a) * l * 0.6;
      const a2 = a + (i % 2 ? 0.7 : -0.7);
      capsule(b, C, su, sv, mu, mv, 1.5, 1.0, M_B, { hs: 0.9 });
      capsule(b, C, mu, mv, mu + Math.cos(a2) * l * 0.5, mv + Math.sin(a2) * l * 0.5, 1.0, 0.5, M_B, { hs: 0.9 });
    }
    for (let i = 0; i < 5; i++) markEll(b, C, (rng() - 0.5) * ru * 1.2, (rng() - 0.5) * rv * 1.1, 1.3, 1.0, M_A, -H * 0.12);
  }

  function shard(b, C, P) {
    const R = P.L * 0.5, H = P.H, rng = C.rng, n = Math.max(4, P.sides || 6), pts = [];
    for (let i = 0; i < n; i++) {
      const a = i * Math.PI * 2 / n + (rng() - 0.5) * 0.4;
      const r = R * (1 - (P.jag || 0.25) * rng()) * (1 - C.wear * 0.18);
      pts.push([Math.cos(a) * r, Math.sin(a) * r * 0.86]);
    }
    polyz(b, C, pts, H, M_B, { pow: 0.38 });
    scan(b, C, -R, R, -R, R, (x, y, u, v) => {                                // one higher facet
      if (u + v * 0.5 > R * 0.15 && hash2(x, y, P.seed) < 0.8) b.mark(x, y, M_B, H * 0.16);
    });
    if (P.sides > 5) markEll(b, C, -R * 0.30, R * 0.22, R * 0.30, R * 0.22, M_A, 0);
  }

  function bottle(b, C, P) {
    const L = P.L, H = P.H, r = H * 0.5, nk = P.neck || 0.34;
    capsule(b, C, -L * 0.46, 0, L * (0.5 - nk) - L * 0.06, 0, r, r * 0.98, M_B, { hs: 1 });
    capsule(b, C, L * (0.5 - nk) - L * 0.06, 0, L * (0.5 - nk) + L * 0.02, 0, r * 0.98, r * 0.5, M_B, { hs: 1 });
    capsule(b, C, L * (0.5 - nk) + L * 0.02, 0, L * 0.44, 0, r * 0.48, r * 0.44, M_B, { hs: 1 });
    dome(b, C, L * 0.46, 0, r * 0.55, r * 0.52, r * 0.55, M_A, { pow: 0.5 });  // lip
    markEll(b, C, -L * 0.46, 0, r * 0.5, r * 0.9, M_A, 0);                     // punt
    scan(b, C, -L * 0.40, -L * 0.05, -r, r, (x, y, u, v) => {                  // sand settled inside
      if (u > -L * 0.40 && u < -L * 0.05 && v > r * 0.1 && hash2(x, y, P.seed) < 0.5) b.mark(x, y, M_S, 0);
    });
  }

  function coil(b, C, P) {
    const R = P.L * 0.42, H = P.H, r = Math.max(1.1, H * 0.5), turns = P.turns || 2.3, rng = C.rng;
    const N = Math.round(turns * 26), ph = rng() * 6.28;
    let pu = 0, pv = 0;
    for (let i = 0; i <= N; i++) {
      const t = i / N, a = t * turns * Math.PI * 2 + ph;
      const rr = R * (0.55 + 0.45 * t) * (1 + 0.10 * Math.sin(a * 2.2));
      const u = Math.cos(a) * rr, v = Math.sin(a) * rr * 0.92;
      if (i) capsule(b, C, pu, pv, u, v, r, r, M_B, { hs: 1 });
      if (i % 4 === 0 && i) markEll(b, C, u, v, r * 0.5, r * 0.5, M_A, 0);      // lay of the strands
      pu = u; pv = v;
    }
    if (P.fray) {                                                              // frayed tail
      const a = ph + turns * Math.PI * 2;
      for (let i = 0; i < 5; i++) {
        const aa = a + (i - 2) * 0.22, l = R * (0.5 + rng() * 0.5);
        capsule(b, C, pu, pv, pu + Math.cos(aa) * l, pv + Math.sin(aa) * l * 0.9, r * 0.42, 0.5, M_A, { hs: 0.9 });
      }
    }
  }

  function mesh(b, C, P) {
    const R = P.L * 0.5, H = Math.max(1, P.H), g = Math.max(3.2, P.L / (P.mesh || 5)), rng = C.rng;
    const inside = (u, v) => Math.pow(u / R, 2) + Math.pow(v / (R * 0.62), 2) < 1 - 0.22 * Math.sin(u * 0.4 + v * 0.6);
    for (let i = -6; i <= 6; i++) for (const s of [1, -1]) {
      const off = i * g;
      const u0 = -R, v0 = off + s * R, u1 = R, v1 = off - s * R;
      const N = 22; let pu = null, pv = null;
      for (let k = 0; k <= N; k++) {
        const t = k / N, u = u0 + (u1 - u0) * t, v = v0 + (v1 - v0) * t;
        if (inside(u, v)) { if (pu !== null) capsule(b, C, pu, pv, u, v, 0.62, 0.62, M_B, { hs: 1.4 }); pu = u; pv = v; }
        else { pu = null; }
      }
    }
    for (let i = -6; i <= 6; i++) for (let j = -6; j <= 6; j++) {               // knots
      const u = (i + j) * g / 2, v = (i - j) * g / 2;
      if (inside(u, v) && rng() < 0.9) dome(b, C, u, v, 0.95, 0.95, H, M_A, { pow: 0.5 });
    }
  }

  function ball(b, C, P) {
    const r = P.L * 0.5, H = P.H, rng = C.rng;
    dome(b, C, 0, 0, r, r * 0.94, H, M_B, { pow: 0.5 });
    if (P.band) scan(b, C, -r, r, -r, r, (x, y, u, v) => {                      // painted band
      if (Math.abs(v) < r * 0.20) b.mark(x, y, M_A, 0);
    });
    markEll(b, C, r * 0.62, 0, r * 0.22, r * 0.20, M_A, -H * 0.25);             // line eye
    if (P.crack) {
      const a = rng() * 6.28;
      capsule(b, C, -Math.cos(a) * r * 0.8, -Math.sin(a) * r * 0.7, Math.cos(a) * r * 0.5, Math.sin(a) * r * 0.5, 0.6, 0.6, M_A, { hs: 0.2, lift: H * 0.8 });
    }
  }

  function cluster(b, C, P) {
    const R = P.L * 0.5, H = P.H, rng = C.rng, n = P.n || 10, kind = P.kind || 'pebble';
    if (P.base) dome(b, C, 0, 0, R * 0.92, R * 0.66, H * 0.30, M_B, { pow: 0.35 });
    const spots = [];
    for (let i = 0; i < n * 6 && spots.length < n; i++) {
      const a = rng() * 6.28, d = Math.sqrt(rng()) * R * 0.86;
      const u = Math.cos(a) * d, v = Math.sin(a) * d * 0.64;
      const rr = kind === 'bubble' ? H * (0.42 + rng() * 0.20) : R * (kind === 'coal' ? 0.20 + rng() * 0.22 : 0.13 + rng() * 0.16);
      if (spots.every(s => Math.hypot(s[0] - u, s[1] - v) > (s[2] + rr) * (kind === 'bubble' ? 0.60 : 0.78))) spots.push([u, v, rr]);
    }
    spots.sort((a, b2) => a[1] - b2[1]);
    spots.forEach((s, i) => {
      const u = s[0], v = s[1], rr = s[2];
      if (kind === 'barnacle') {
        dome(b, C, u, v, rr, rr * 0.94, Math.max(1.5, rr * 1.5), M_B, { pow: 0.85 });
        markEll(b, C, u, v - rr * 0.10, rr * 0.34, rr * 0.30, M_A, 0);
      } else if (kind === 'coal') {
        const pts = []; const m = 5;
        for (let k = 0; k < m; k++) { const a = k * 6.28 / m + rng(); const q = rr * (0.7 + rng() * 0.5); pts.push([u + Math.cos(a) * q, v + Math.sin(a) * q * 0.8]); }
        polyz(b, C, pts, rr * 1.1, i % 4 === 3 ? M_A : M_B, { pow: 0.3 });
      } else if (kind === 'bubble') {
        dome(b, C, u, v, rr, rr * 0.90, rr * 1.15, M_B, { pow: 0.55 });
        markEll(b, C, u - rr * 0.2, v - rr * 0.2, rr * 0.26, rr * 0.22, M_A, 0);
      } else {
        dome(b, C, u, v, rr, rr * 0.82, rr * (0.75 + rng() * 0.4), i % 5 === 4 ? M_A : M_B, { pow: 0.55 });
      }
    });
  }

  function cobble(b, C, P) {
    const r = P.L * 0.5, H = P.H, rng = C.rng, ph = rng() * 6.28;
    scan(b, C, -r - 1, r + 1, -r - 1, r + 1, (x, y, u, v) => {
      const a = Math.atan2(v, u), rr = r * (1 + 0.09 * Math.sin(a * 3 + ph) + 0.06 * Math.sin(a * 5 - ph));
      const d = Math.hypot(u, v / 0.80) / rr; if (d > 1) return;
      b.put(x, y, H * Math.pow(Math.max(0, 1 - d * d), 0.42), M_B);
    });
    if (P.vein) scan(b, C, -r, r, -r, r, (x, y, u, v) => {                       // one quartz band
      if (Math.abs(v - u * 0.42 - r * 0.12) < r * 0.10) b.mark(x, y, M_A, 0);
    });
  }

  function can(b, C, P) {
    const L = P.L, H = P.H, r = H * 0.5, rng = C.rng, cr = P.crush || 0.4;
    capsule(b, C, -L * 0.40, 0, L * 0.40, 0, r, r * 0.96, M_B, {
      hs: 1, zf: (t, dr, z) => z * (1 - cr * 0.55 * Math.exp(-Math.pow((t - 0.45) / 0.16, 2))),
    });
    markEll(b, C, -L * 0.42, 0, r * 0.42, r * 0.92, M_A, 0);                     // rims
    markEll(b, C, L * 0.42, 0, r * 0.42, r * 0.92, M_A, 0);
    for (let i = 0; i < 3; i++) {                                                // rusted-through holes
      const u = -L * 0.3 + rng() * L * 0.6, v = (rng() - 0.5) * r;
      markEll(b, C, u, v, 1.2 + rng(), 1.0, M_A, -r * 0.3);
    }
    scan(b, C, -L * 0.44, L * 0.44, -r, r, (x, y, u, v) => {
      if (hash2(x, y, P.seed + 5) < 0.22) b.mark(x, y, M_A, 0);
    });
  }

  const FORMS = { valve, fanValve, spiral, disc, star, carapace, claw, rod, rootWad, feather, pouch, shard, bottle, coil, mesh, ball, cluster, cobble, can };

  function edgeMark(b, band, m) {                                              // recolour the outer band
    const w = b.w, h = b.h, hits = [];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!b.mat[i]) continue;
      if (!b.mat[i - 1] || !b.mat[i + 1] || y === 0 || y === h - 1 || !b.mat[i - w] || !b.mat[i + w]) hits.push(i);
    }
    hits.forEach(i => { b.mat[i] = m; });
    if (band > 1.4) hits.forEach(i => { [i - 1, i + 1, i - w, i + w].forEach(j => { if (j >= 0 && j < w * h && b.mat[j]) b.mat[j] = m; }); });
  }

  // ---- wear / sand ----------------------------------------------------------
  function wearPass(b, wear, seed) {
    if (wear <= 0.02) return;
    const w = b.w, h = b.h, kill = [];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!b.mat[i]) continue;
      const edge = !b.mat[i - 1] || !b.mat[i + 1] || !b.mat[i - w] || !b.mat[i + w];
      if (!edge) continue;
      if (hash2(x * 5, y * 5, seed) < wear * 0.34) kill.push(i);
    }
    kill.forEach(i => { b.mat[i] = 0; b.z[i] = 0; });
    for (let i = 0; i < b.z.length; i++) if (b.mat[i]) b.z[i] *= 1 - wear * 0.12;  // relief softens
  }
  function sandPass(b, sand, seed) {
    if (sand <= 0.02) return;
    const w = b.w, h = b.h, zmax = Math.max(0.6, maxZ(b)), add = [];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x;
      if (b.mat[i]) {
        const low = 1 - Math.min(1, b.z[i] / (zmax * 0.42));
        if (low > 0.1 && hash2(x, y, seed + 3) < sand * 0.55 * low) b.mat[i] = M_S;
      } else {                                                                  // grains banked on the lee
        const nb = (b.mat[i - 1] ? 1 : 0) + (b.mat[i + 1] ? 1 : 0) + (y > 0 && b.mat[i - w] ? 1 : 0) + (y < h - 1 && b.mat[i + w] ? 1 : 0);
        if (nb && hash2(x, y, seed + 11) < sand * 0.30) add.push(i);
      }
    }
    add.forEach(i => { b.mat[i] = M_S; b.z[i] = 0.35; });
  }
  function maxZ(b) { let m = 0; for (let i = 0; i < b.z.length; i++) if (b.mat[i] && b.z[i] > m) m = b.z[i]; return m; }

  // ---- ramps per state ------------------------------------------------------
  function stateRamp(matKey, state) {
    const M = MAT[matKey] || MAT.clam;
    let base = M.base, warm = M.warm == null ? 0.28 : M.warm, gloss = M.gloss || 0.2;
    if (state === 'wet') { base = mix(mix(base, '#000000', 0.13), WETC, 0.15); warm -= 0.08; gloss = Math.min(1, gloss + 0.42); }
    else if (state === 'bleached') { base = desat(mix(base, BONE, 0.44), 0.26); warm -= 0.04; gloss *= 0.55; }
    const r = rampFrom(base, { warm: Math.max(0.08, warm) });
    r._gloss = gloss; r._frost = M.frost ? 1 : 0; r._n = M.n;
    return r;
  }
  function rampsOf(key, state) {
    const f = byKey[key] || FINDS[0];
    return {
      1: stateRamp(f.body, state), 2: stateRamp(f.acc || f.body, state),
      3: stateRamp(f.p && (f.p.lip || f.p.bowl || f.p.paint || f.p.band) ? (f.body === 'quahog' ? 'quahogLip' : f.acc || f.body) : f.acc || f.body, state),
      4: stateRamp('sand', state === 'wet' ? 'wet' : 'dry'),
    };
  }

  // ---- shade ----------------------------------------------------------------
  const LX = -0.55, LY = -0.735, LZ = 0.40;                                    // upper-left key
  function toRGBA(b, ramps, state, seed) {
    const w = b.w, h = b.h, out = new Uint8ClampedArray(w * h * 4);
    const kl = h2r(KEYLINE), zmax = Math.max(0.8, maxZ(b));
    const mott = state === 'wet' ? 0.030 : state === 'bleached' ? 0.075 : 0.055;
    const cols = {};
    for (const k in ramps) { const r = ramps[k]; cols[k] = BANDS.map(n => h2r(r[n])); cols[k + '_gl'] = h2r(r.gl); }
    const zAt = (x, y) => (x < 0 || x >= w || y < 0 || y >= h || !b.mat[y * w + x]) ? -0.6 : b.z[y * w + x];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x, o = i * 4, m = b.mat[i];
      if (!m) {                                                                 // 1 px soft keyline outside
        const nb = zAt(x - 1, y) >= 0 || zAt(x + 1, y) >= 0 || zAt(x, y - 1) >= 0 || zAt(x, y + 1) >= 0;
        if (!nb) { out[o + 3] = 0; continue; }
        const lit = (zAt(x + 1, y) >= 0 || zAt(x, y + 1) >= 0) && !(zAt(x - 1, y) >= 0 || zAt(x, y - 1) >= 0);
        const c = lit ? h2r(mix(KEYLINE, '#6b6045', 0.30)) : kl;
        out[o] = c[0]; out[o + 1] = c[1]; out[o + 2] = c[2]; out[o + 3] = 255; continue;
      }
      const R = ramps[m] || ramps[1], C = cols[m] || cols[1];
      const dzx = (zAt(x + 1, y) - zAt(x - 1, y)) * 0.5, dzy = (zAt(x, y + 1) - zAt(x, y - 1)) * 0.5 / Q;
      let nx = -dzx, ny = -dzy, nz = 1.15, nl = Math.hypot(nx, ny, nz) || 1;
      nx /= nl; ny /= nl; nz /= nl;
      let lam = nx * LX + ny * LY + nz * LZ;
      lam = Math.max(0, lam);
      let lum = 0.27 + 0.76 * Math.pow(lam, 0.80);
      lum *= 0.88 + 0.12 * Math.min(1, b.z[i] / (zmax * 0.55));                 // contact shade, nestles it in
      lum += (hash2(x, y, seed) - 0.5) * mott * (R._frost ? 2.2 : 1);
      if (m === M_S) lum = 0.34 + lum * 0.62;
      const t = Math.max(0, Math.min(1, lum)) * (BANDS.length - 1);
      let bi = Math.floor(t); if (t - bi > bayer(x, y)) bi++;
      bi = Math.max(0, Math.min(BANDS.length - 1, bi));
      let c = C[bi];
      const spec = R._gloss * (state === 'wet' ? 1 : 0.6);
      if (spec > 0.30 && lam > 0.90 - spec * 0.20 && hash2(x, y, seed + 7) < spec * 0.85) c = cols[m + '_gl'] || c;
      out[o] = c[0]; out[o + 1] = c[1]; out[o + 2] = c[2]; out[o + 3] = 255;
    }
    return out;
  }

  // ---- anchors + report -----------------------------------------------------
  function anchorsFor(b, cell) {
    const w = b.w, h = b.h; let lowY = -1, sx = 0, sn = 0, topZ = -1, tx = 0, ty = 0, px = 0, n = 0;
    let x0 = w, x1 = -1, y0 = h, y1 = -1, clipped = false;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!b.mat[i]) continue;
      n++; if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
      if (x === 0 || y === 0 || x === w - 1 || y === h - 1) clipped = true;
      if (y > lowY) { lowY = y; sx = x; sn = 1; } else if (y === lowY) { sx += x; sn++; }
      if (b.z[i] > topZ) { topZ = b.z[i]; tx = x; ty = y; }
    }
    if (!n) return { sit: { x: cell.pvx, y: cell.pvy }, pick: { x: cell.pvx, y: cell.pvy }, catch: [], report: { px: 0, clipped: false, bbox: [0, 0, 0, 0] } };
    const cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, cand = [];
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const i = y * w + x; if (!b.mat[i]) continue;
      if (b.mat[i - 1] && b.mat[i + 1] && b.mat[i - w] && b.mat[i + w]) continue;
      cand.push({ x, y, d: Math.hypot(x - cx, (y - cy) / Q), a: Math.atan2((y - cy) / Q, x - cx) });
    }
    cand.sort((p, q) => q.d - p.d);
    const cat = [];
    for (const c of cand) { if (cat.every(k => Math.abs(((c.a - k.a + Math.PI * 3) % (Math.PI * 2)) - Math.PI) > 1.05)) cat.push(c); if (cat.length === 3) break; }
    return {
      sit: { x: Math.round(sx / sn), y: lowY },
      pick: { x: tx, y: ty },
      catch: cat.map(c => ({ x: c.x, y: c.y })),
      report: { px: n, clipped, bbox: [x0, y0, x1 - x0 + 1, y1 - y0 + 1] },
    };
  }

  // ---- public ---------------------------------------------------------------
  function render(key, dir, opts) {
    const f = byKey[key] || FINDS[0];
    const o = opts || {}, state = STATES.indexOf(o.state) >= 0 ? o.state : 'dry';
    const d = typeof dir === 'number' ? ((dir % 8) + 8) % 8 : Math.max(0, DIRS.indexOf(dir || 'SE'));
    const P = paramsOf(f.key, o.variant == null ? 0 : o.variant);
    const cell = cellFull(f.key), b = new Buf(cell.w, cell.h);
    const rot = d * Math.PI / 4;
    const C = { cx: cell.pvx + 0.5, cy: cell.pvy + 0.5, c: Math.cos(rot), s: Math.sin(rot), rng: mulberry(P.seed), wear: o.wear || 0 };
    (FORMS[f.form] || valve)(b, C, P);
    wearPass(b, o.wear || 0, P.seed);
    sandPass(b, o.sand == null ? 0 : o.sand, P.seed);
    const ramps = rampsOf(f.key, state);
    const rgba = toRGBA(b, ramps, state, P.seed);
    const an = anchorsFor(b, cell);
    return {
      w: cell.w, h: cell.h, rgba, pivot: { x: cell.pvx, y: cell.pvy },
      anchors: { sit: an.sit, pick: an.pick, catch: an.catch },
      params: P, ramps,
      report: {
        px: an.report.px, clipped: an.report.clipped, bbox: an.report.bbox,
        drawnPx: Math.round(P.L), trueCm: Math.round(P.cm * 10) / 10,
        readX: Math.round(P.L / Math.max(0.5, P.cm / 100 * PPU) * 10) / 10,
        zPx: Math.round(P.H * 10) / 10, state, dir: DIRS[d], variant: P.v,
      },
    };
  }

  root.ShoreFinds = {
    PPU, Q, KEYLINE, DIRS, STATES, CATS, MAT, FINDS, BANDS,
    READ: { k: READ_K, e: READ_E, px: drawnPx },
    list: () => FINDS.map(f => f.key),
    get: (k) => byKey[k],
    cellOf: cellFull, paramsOf, rampsOf, render, mix, rampFrom,
    variants: 3,
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
