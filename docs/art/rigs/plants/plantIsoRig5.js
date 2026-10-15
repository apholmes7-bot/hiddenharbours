/* Hidden Harbours — PLANT RIG 5 (pixel terrain pass 9).  PlantRig4's light contract, new plants.

   Pass 8 put every plant below the trees on the trees' G-buffer and light law, and at 1x the plants
   still did not read as the trees' kin: a shrub was a scatter of single leaf stamps with random
   normals over a dark wedge of converging stems (a broom), a grass was a firework of straight 1 px
   lines twice as wide as the species, a flower a stick with an icon, kelp a brown sausage, drained
   wrack a flat speckle; at dusk every blade took the warm rim and the meadow glowed orange; weed
   under two metres of water stood on the sea like paint. This rig keeps PlantRig4's model / frame /
   relight / castShadow / view / sheet contract verbatim and replaces what is drawn:

     A. MASSES.  The trees' floret: a core ellipsoid and its satellites with the grain's edge profile
        on the outline, dome normals per pixel, a band down (the dark between leaves), and leaf
        stamps laid over its front on the ellipsoid's own normals — so a shrub reads as lit domes
        with shaded undersides, the way a crown does. Sites are Poisson-spaced along the outer part
        of every branch, tips first, so a shrub is leafy where the species is and hollow where not.
     B. STEMS FROM A ROOT CROWN.  Stems start apart across a crown and taper to twigs; no wedge.
     C. TUFTS.  Grasses, rushes and sedges are dense tufts on a dark crown: steep inner blades, outer
        ones leaning, the arch at the tip, base dark and tip pale, held inside the species' width.
     D. FLOWERS BY PARTS.  Racemes built row by row (so the live tip and seam shade each row like a
        cylinder), umbels on rays, ray discs with an eye, iris falls, a slipper over pleated leaves;
        every flowering herb stands on a leafy basal mass and a mature plant is a clump of stems.
     E. WEED THAT IS WEED.  Kelp as broad ruffled blades with a pale midband, wrack as a heap of
        branching straps and bladders on a dark body, sea lettuce as crinkled translucent sheets,
        Irish moss as a cushion of small masses.
     F. LIGHT FIXES.  The back rim is thickness-gated (the trees' rule 3): a pixel takes the warm rim
        only with body behind it, so a 1 px blade never does — at most its tip steps up. Weed deeper
        than 0.9 m is a dark shape in the water; deeper than 1.7 m it is gone.

   PPU 32 · ¾ from S at 40° · true heights · bottom-centre ROOT pivot · no AA · binary alpha · ringless.
   globalThis.PlantRig5 — same API as PlantRig4 (model · frame · relight · castShadow · view · sheet · cellOf).  */
(function (root) {
  'use strict';
  const PPU = 32, LOOP = 16, VARIANTS = 4, TAU = Math.PI * 2;
  const ELEV = 40, CE = Math.cos(ELEV * Math.PI / 180), SE = Math.sin(ELEV * Math.PI / 180), D2R = Math.PI / 180;
  const COLD = '#1d3b4a', WARM = '#e8b06a', WATER = '#27535e';
  const SEASONS = ['spring', 'summer', 'autumn', 'winter'];
  const STAGES = { seedling: 0.42, young: 0.7, mature: 1.0 }, STAGE_KEYS = Object.keys(STAGES);
  const M = { LEAF: 0, WOOD: 1, BLADE: 2, PETAL: 3, FRUIT: 4, STEM: 5 };
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r) => '#' + r.map(v => clamp(Math.round(v), 0, 255).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2r(a), B = h2r(b); return r2h([0, 1, 2].map(i => A[i] + (B[i] - A[i]) * t)); };
  const nrm = (v) => { const L = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / L, v[1] / L, v[2] / L]; };
  const toView = (w) => [w[0], -w[2] * CE + w[1] * SE, w[2] * SE + w[1] * CE];
  const dirOf = (az, el) => { const c = Math.cos(el * D2R); return [Math.sin(az * D2R) * c, -Math.cos(az * D2R) * c, Math.sin(el * D2R)]; };
  const UPV = toView([0, 0, 1]);
  const hsh = (n, k) => { let h = Math.imul((n | 0) ^ Math.imul((k | 0) + 1, 0x9e3779b9), 0x85ebca6b); h ^= h >>> 13; h = Math.imul(h, 0xc2b2ae35); h ^= h >>> 16; return (h >>> 0) / 4294967296; };
  const strH = (s) => { let h = 2166136261; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; };

  // ---- ramps: the trees' formulas ---------------------------------------------------------------------
  const folRamp = (b) => [mix(mix(b, '#000000', 0.74), COLD, 0.24), mix(mix(b, '#000000', 0.52), COLD, 0.22), mix(b, '#000000', 0.22), mix(b, WARM, 0.16), mix(mix(b, '#ffffff', 0.10), WARM, 0.34)];
  const barkRamp = (b) => [mix(mix(b, '#000000', 0.82), COLD, 0.42), mix(mix(b, '#000000', 0.60), COLD, 0.28), mix(b, '#000000', 0.34), mix(b, WARM, 0.18), mix(mix(b, '#000000', 0.10), WARM, 0.40)];
  const petalRamp = (b) => [mix(mix(b, '#000000', 0.56), COLD, 0.20), mix(mix(b, '#000000', 0.32), COLD, 0.10), mix(b, '#000000', 0.08), mix(b, '#ffffff', 0.10), mix(mix(b, '#ffffff', 0.32), WARM, 0.14)];
  const fruitRamp = (b) => [mix(mix(b, '#000000', 0.70), COLD, 0.25), mix(mix(b, '#000000', 0.45), COLD, 0.12), mix(b, '#000000', 0.12), mix(b, WARM, 0.10), mix(b, '#ffffff', 0.55)];
  const SNOW = ['#5c7180', '#7d93a0', '#a8bcc4', '#cfdde1', '#eef4f4'];
  const DEAD = '#8e7c58', SPRING = '#a4c24e';
  /* the silhouette profile a mass cuts, per grain (the trees' EDGES, in plant sizes) */
  const EDGE5 = {
    round: { shape: 'round', pitch: 3.4, amp: 1.0, base: 0.55, under: 0.35 },
    tri: { shape: 'tri', pitch: 3.0, amp: 1.15, base: 0.55, under: 0.35 },
    spike: { shape: 'spike', pitch: 2.4, amp: 1.5, base: 0.45, under: 0.45 },
    fan: { shape: 'tri', pitch: 4.4, amp: 1.0, base: 0.5, under: 0.2 },
    soft: { shape: 'round', pitch: 2.6, amp: 0.7, base: 0.6, under: 0.3 },
  };
  const GRAIN_EDGE = { heath: 'soft', heath2: 'round', broad: 'round', serrate: 'tri', lance: 'tri', pinnate: 'tri', scale: 'spike', coin: 'round', fern: 'fan', sheet: 'soft', moss: 'soft' };
  /* pass-9 overrides on the species table: mr = leaf-mass radius px, cover = leaf cover over a mass, dense = blade multiplier */
  const P5 = {
    LowbushBlueberry: { mr: 2.6, stems: 16, leafDen: 1.5, cover: 1.3, leafFrom: 0.12 }, SheepLaurel: { mr: 2.5, leafDen: 1.0 }, Rhodora: { mr: 2.4 }, BlackHuckleberry: { mr: 2.6, leafDen: 1.0 },
    CommonJuniper: { mr: 3.0, leafDen: 1.6, cover: 1.35, leafFrom: 0.12 }, Leatherleaf: { mr: 2.3, leafDen: 1.0 }, SweetGale: { mr: 2.5, leafDen: 0.9 }, WinterberryHolly: { mr: 3.4, leafDen: 0.9 },
    SpeckledAlder: { mr: 4.4, leafDen: 0.95 }, PussyWillow: { mr: 3.6, leafDen: 0.7 }, RedOsierDogwood: { mr: 3.4, leafDen: 0.85 }, Meadowsweet: { mr: 2.8, leafDen: 1.0, leafFrom: 0.2 },
    Steeplebush: { mr: 2.6, leafDen: 1.0, leafFrom: 0.2 }, Raspberry: { mr: 2.7, leafDen: 0.8 }, WildRose: { mr: 2.8, leafDen: 0.95 }, Serviceberry: { mr: 3.8, leafDen: 0.75 },
    BeakedHazelnut: { mr: 4.0, leafDen: 0.85 }, WildRaisin: { mr: 3.6, leafDen: 0.85 }, RedElderberry: { mr: 3.8, leafDen: 0.8 }, Bayberry: { mr: 3.8, leafDen: 1.2, cover: 1.25, leafFrom: 0.05, stems: 16, forks: 2 },
    SweetFern: { mr: 2.6, leafDen: 1.0 },
    MeadowGrass: { dense: 1.9 }, Timothy: { dense: 2.0 }, SoftRush: { dense: 1.7 }, TussockSedge: { dense: 1.8 }, Cordgrass: { dense: 1.5 }, SaltmeadowHay: { dense: 1.3 },
    BlackRush: { dense: 1.8 }, Cattail: { dense: 1.25 }, Threesquare: { dense: 1.8 }, MarramGrass: { dense: 1.7 }, Eelgrass: { dense: 1.25, crownMass: false },
  };
  const Q5 = (sp) => Object.assign({}, sp.p, P5[sp.key] || {});

  // ---- stencils -------------------------------------------------------------------------------------------
  const ST = (rows) => { const h = rows.length, w = rows[0].length, px = []; for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (rows[y][x] !== '.') px.push([x - (w >> 1), y - (h >> 1)]); return { w, h, px, r: Math.max(w, h) / 2 }; };
  const SET = (list) => { const out = []; for (const r of list) { out.push(ST(r)); out.push(ST(r.map(q => q.split('').reverse().join('')))); } return out; };
  const GR = {
    heath: SET([['oo'], ['o', 'o'], ['.o', 'o.']]),
    heath2: SET([['ooo'], ['.oo', 'oo.'], ['oo', '.o']]),
    broad: SET([['.oo.', 'oooo', '.oo.'], ['oo.', 'ooo', '.oo'], ['.o.', 'ooo', 'oo.']]),
    serrate: SET([['o.oo', 'oooo', '.oo.'], ['.oo.', 'oooo', 'oo.o'], ['.o.', 'ooo', 'ooo']]),
    lance: SET([['ooo'], ['.ooo', 'ooo.'], ['oo..', '.ooo']]),
    pinnate: SET([['o.o', 'ooo', '.o.'], ['oo.oo', '.ooo.'], ['o.o', '.o.']]),
    scale: SET([['o.o', '.o.'], ['.o.', 'o.o', '.o.'], ['oo', '.o']]),
    coin: SET([['oo', 'oo'], ['.o.', 'ooo', '.o.'], ['ooo', '.o.']]),
    fern: SET([['o.o.o', '.ooo.'], ['.o.o.', 'ooooo'], ['o.o', 'ooo']]),
    sheet: SET([['.oooo.', 'oooooo', 'ooooo.', '.ooo..'], ['...oo.', '.ooooo', 'oooooo', '.oooo.'], ['.ooooo', 'ooooo.', 'oooo..'], ['..ooo.', 'oooooo', 'oooooo', '.oo.oo']]),
    moss: SET([['o.o', 'ooo', '.o.'], ['oo', 'oo'], ['.o.', 'ooo']]),
  };
  const ST5 = { rw3: ST(['ooo']), rw2: ST(['oo']), rw1: ST(['o']), umb7: ST(['.o.o.o.', 'ooooooo', '.ooooo.']), umb5: ST(['.o.o.', 'ooooo', '.ooo.']), ray5: ST(['.ooo.', 'ooooo', '.ooo.']),
    iris5: ST(['..o..', 'o.o.o', 'ooooo', '.o.o.']), slipper: ST(['..ooo..', '.ooooo.', 'ooooooo', '.ooooo.']), tim: ST(['.o', 'oo', 'oo', 'oo', 'oo', 'o.']), mar: ST(['o.', 'oo', 'oo', 'oo', 'oo', 'oo', '.o']),
    plus: ST(['.o.', 'ooo', '.o.']), bud: ST(['o', 'o']), cyl: ST(['oo', 'oo', 'oo', 'oo', 'oo', 'oo', 'oo']), sheet2: ST(['..oooo..', '.oooooo.', 'oooooooo', '.oooooo.', '..ooo...']), bladder: ST(['.o.', 'ooo', '.o.']), panicle: ST(['.o.', '.o.', 'ooo', 'ooo', '.o.']) };
  const PT = { bell: ST(['o', 'o']), cup: ST(['oo', 'oo']), rose: ST(['.o.', 'ooo', '.o.']), disc: ST(['.o.o.', 'ooooo', '.o.o.']), puff: ST(['.o.', 'ooo']),
    umbel: ST(['.ooo.', 'ooooo']), spire: ST(['o', 'o', 'o', 'o']), dot: ST(['o']), pair: ST(['oo']), cone: ST(['.o.', 'ooo', 'ooo', '.o.']), catkin: ST(['o', 'o', 'o']),
    iris: ST(['o.o', 'ooo', '.o.']), pouch: ST(['.o.', 'ooo', 'ooo']), cyl: ST(['oo', 'oo', 'oo', 'oo', 'oo', 'oo']), tcyl: ST(['o', 'o', 'o', 'o', 'o']), nest: ST(['o.o', '.o.']), pod: ST(['ooo']), plume: ST(['o.', 'oo', '.o']), fluff: ST(['.o.', 'o.o']) };

  // ---- species ---------------------------------------------------------------------------------------------
  /* h, w  TRUE size in px at 32 px/m (the old rigs' measured sizes).  fol · fall · winter · bark ·
     winterBark · stem colours.  wind = [bend (tip lean at a gale, × h), sway, flutter, bob].
     bloom / fruit / catkin: {s: seasons, c: colour, n: count, k: kind}; holds = fruit through winter.
     zone (shore) = metres above chart datum of the ground it grows on; limp = what it owes the water. */
  const WSHRUB = [0.05, 1.4, 0.35, 0.6], WMAT = [0.03, 0.6, 0.25, 0.3], WHERB = [0.14, 0.9, 0.5, 0.4], WGRASS = [0.17, 1.2, 0.3, 0.3], WALGA = [0.02, 0.4, 0.1, 0.2];
  const SPECIES = [
    // shrubs
    { key: 'LowbushBlueberry', name: 'Lowbush Blueberry', latin: 'Vaccinium angustifolium', fam: 'shrub', hab: 'barren', habit: 'woody', h: 12, w: 46, fol: '#4e7a4f', fall: '#a8342c', bark: '#8a3a30', wind: WMAT,
      p: { form: 'mat', stems: 14, grain: 'heath', leafDen: 1.1, leafFrom: 0.2 }, bloom: { s: ['spring'], c: '#e8dcd0', n: 12, k: 'bell' }, fruit: { s: ['summer', 'autumn'], c: '#4f6fa8', n: 20 } },
    { key: 'SheepLaurel', name: 'Sheep Laurel', latin: 'Kalmia angustifolia', fam: 'shrub', hab: 'barren', habit: 'woody', h: 26, w: 34, fol: '#3f6647', winter: '#4c5850', evergreen: true, bark: '#6b5240', wind: WSHRUB,
      p: { form: 'clump', stems: 7, spread: 0.35, grain: 'heath2', leafDen: 0.9, leafFrom: 0.35 }, bloom: { s: ['summer'], c: '#c9607f', n: 9, k: 'whorl' } },
    { key: 'Rhodora', name: 'Rhodora', latin: 'Rhododendron canadense', fam: 'shrub', hab: 'barren', habit: 'woody', h: 32, w: 40, fol: '#5f7d5c', fall: '#8a6b3a', bark: '#5f4a3c', wind: WSHRUB,
      p: { form: 'clump', stems: 5, spread: 0.5, forks: 2, grain: 'heath2', leafDen: 0.6, leafFrom: 0.45 }, bloom: { s: ['spring'], c: '#c25a92', n: 12, k: 'puff', bare: true } },
    { key: 'BlackHuckleberry', name: 'Black Huckleberry', latin: 'Gaylussacia baccata', fam: 'shrub', hab: 'barren', habit: 'woody', h: 29, w: 36, fol: '#557a4a', fall: '#a85a2c', bark: '#6b4f3c', wind: WSHRUB,
      p: { form: 'clump', stems: 8, spread: 0.4, grain: 'heath2', leafDen: 0.9, leafFrom: 0.3 }, bloom: { s: ['spring'], c: '#d9a89c', n: 8, k: 'bell' }, fruit: { s: ['summer'], c: '#1e2430', n: 18 } },
    { key: 'CommonJuniper', name: 'Common Juniper', latin: 'Juniperus communis', fam: 'shrub', hab: 'barren', habit: 'woody', h: 20, w: 52, fol: '#3d5f52', winter: '#4a6154', evergreen: true, bark: '#6b5a44', wind: WMAT,
      p: { form: 'mat', stems: 11, grain: 'scale', leafDen: 1.6, leafFrom: 0.1, spreadL: 2.2 }, fruit: { s: ['spring', 'summer', 'autumn', 'winter'], c: '#6b86a8', n: 14 } },
    { key: 'Leatherleaf', name: 'Leatherleaf', latin: 'Chamaedaphne calyculata', fam: 'shrub', hab: 'bog', habit: 'woody', h: 27, w: 44, fol: '#4a6b45', winter: '#7a6444', evergreen: true, bark: '#6b5138', wind: WSHRUB,
      p: { form: 'thicket', stems: 13, spread: 0.3, arch: 0.4, grain: 'heath2', leafDen: 0.9, leafFrom: 0.3 }, bloom: { s: ['spring'], c: '#e2e2d4', n: 14, k: 'bell' } },
    { key: 'SweetGale', name: 'Sweet Gale', latin: 'Myrica gale', fam: 'shrub', hab: 'bog', habit: 'woody', h: 34, w: 46, fol: '#5c7a5c', fall: '#8a7a4a', bark: '#5f4a3a', wind: WSHRUB,
      p: { form: 'thicket', stems: 11, spread: 0.3, grain: 'lance', leafDen: 0.8, leafFrom: 0.35 }, catkin: { s: ['spring', 'winter'], c: '#8a6b3a', n: 12 } },
    { key: 'WinterberryHolly', name: 'Winterberry', latin: 'Ilex verticillata', fam: 'shrub', hab: 'bog', habit: 'woody', h: 78, w: 44, fol: '#3f6b45', fall: '#8a7a3a', bark: '#4f4438', wind: WSHRUB,
      p: { form: 'clump', stems: 5, spread: 0.28, forks: 2, grain: 'broad', leafDen: 0.8, leafFrom: 0.4 }, bloom: { s: ['summer'], c: '#e2ddd0', n: 8, k: 'bell' }, fruit: { s: ['autumn'], c: '#b8342c', n: 30, holds: true } },
    { key: 'SpeckledAlder', name: 'Speckled Alder', latin: 'Alnus incana rugosa', fam: 'shrub', hab: 'swale', habit: 'woody', h: 128, w: 62, fol: '#4a6b3f', fall: '#7a6b3a', bark: '#5a4c42', wind: WSHRUB,
      p: { form: 'clump', stems: 9, spread: 0.24, forks: 2, grain: 'serrate', leafDen: 0.8, leafFrom: 0.42 }, catkin: { s: ['winter', 'spring'], c: '#6b5230', n: 16 } },
    { key: 'PussyWillow', name: 'Pussy Willow', latin: 'Salix discolor', fam: 'shrub', hab: 'swale', habit: 'woody', h: 108, w: 58, fol: '#5f7a5a', fall: '#a89450', bark: '#6b5744', wind: WSHRUB,
      p: { form: 'clump', stems: 6, spread: 0.32, forks: 2, grain: 'lance', leafDen: 0.55, leafFrom: 0.45 }, catkin: { s: ['spring'], c: '#cfc8b8', n: 18, k: 'puss' } },
    { key: 'RedOsierDogwood', name: 'Red Osier Dogwood', latin: 'Cornus sericea', fam: 'shrub', hab: 'swale', habit: 'woody', h: 70, w: 52, fol: '#4a6b52', fall: '#9c3f38', bark: '#6b4a3c', winterBark: '#a8422e', wind: WSHRUB,
      p: { form: 'clump', stems: 8, spread: 0.4, forks: 1, grain: 'broad', leafDen: 0.6, leafFrom: 0.4 }, bloom: { s: ['summer'], c: '#e2ded0', n: 7, k: 'umbel' }, fruit: { s: ['autumn'], c: '#d8dcd8', n: 16 } },
    { key: 'Meadowsweet', name: 'Meadowsweet', latin: 'Spiraea alba', fam: 'shrub', hab: 'swale', habit: 'woody', h: 40, w: 34, fol: '#5f7a4a', fall: '#a8853a', bark: '#6b5540', wind: WSHRUB,
      p: { form: 'clump', stems: 9, spread: 0.22, grain: 'serrate', leafDen: 0.9, leafFrom: 0.2 }, bloom: { s: ['summer'], c: '#e8e4d6', n: 9, k: 'spire' } },
    { key: 'Steeplebush', name: 'Steeplebush', latin: 'Spiraea tomentosa', fam: 'shrub', hab: 'swale', habit: 'woody', h: 34, w: 32, fol: '#67784a', fall: '#a87a3a', bark: '#6b5238', wind: WSHRUB,
      p: { form: 'clump', stems: 8, spread: 0.18, grain: 'serrate', leafDen: 0.9, leafFrom: 0.2 }, bloom: { s: ['summer'], c: '#c96f8a', n: 8, k: 'spire' } },
    { key: 'Raspberry', name: 'Wild Raspberry', latin: 'Rubus idaeus', fam: 'shrub', hab: 'edge', habit: 'woody', h: 46, w: 54, fol: '#5c7a52', fall: '#a8663a', bark: '#7a6b5c', winterBark: '#8a7a92', wind: WSHRUB,
      p: { form: 'cane', stems: 7, spread: 0.45, arch: 1, grain: 'pinnate', leafDen: 0.6, leafFrom: 0.25 }, bloom: { s: ['spring'], c: '#e2e2d4', n: 6, k: 'puff' }, fruit: { s: ['summer'], c: '#a83a44', n: 16 } },
    { key: 'WildRose', name: 'Wild Rose', latin: 'Rosa virginiana', fam: 'shrub', hab: 'edge', habit: 'woody', h: 42, w: 40, fol: '#4a6b4a', fall: '#b8763a', bark: '#6b3a30', wind: WSHRUB,
      p: { form: 'clump', stems: 7, spread: 0.35, grain: 'pinnate', leafDen: 0.8, leafFrom: 0.2 }, bloom: { s: ['summer'], c: '#d97f9c', n: 6, k: 'rose' }, fruit: { s: ['autumn'], c: '#b8442c', n: 11, holds: true } },
    { key: 'StaghornSumac', name: 'Staghorn Sumac', latin: 'Rhus typhina', fam: 'shrub', hab: 'edge', habit: 'woody', h: 110, w: 62, fol: '#57784a', fall: '#c23a2c', bark: '#7a6450', wind: WSHRUB,
      p: { form: 'antler', stems: 3, spread: 0.3, forks: 2, grain: 'lance', r0: 1.9 }, fruit: { s: ['summer', 'autumn'], c: '#9c3a30', n: 5, k: 'cone', holds: true } },
    { key: 'Serviceberry', name: 'Serviceberry', latin: 'Amelanchier canadensis', fam: 'shrub', hab: 'edge', habit: 'woody', h: 118, w: 56, fol: '#5c7a5f', fall: '#c2712c', bark: '#6b6155', wind: WSHRUB,
      p: { form: 'clump', stems: 4, spread: 0.28, forks: 2, grain: 'broad', leafDen: 0.55, leafFrom: 0.45 }, bloom: { s: ['spring'], c: '#eae6da', n: 16, k: 'puff', bare: true }, fruit: { s: ['summer'], c: '#4a3f5c', n: 20 } },
    { key: 'BeakedHazelnut', name: 'Beaked Hazelnut', latin: 'Corylus cornuta', fam: 'shrub', hab: 'woods', habit: 'woody', h: 92, w: 60, fol: '#5a7a4f', fall: '#b89440', bark: '#6b5a48', wind: WSHRUB,
      p: { form: 'clump', stems: 6, spread: 0.4, forks: 1, arch: 0.3, grain: 'serrate', leafDen: 0.6, leafFrom: 0.35 }, catkin: { s: ['winter', 'spring'], c: '#a89058', n: 11 } },
    { key: 'WildRaisin', name: 'Wild Raisin', latin: 'Viburnum nudum cassinoides', fam: 'shrub', hab: 'woods', habit: 'woody', h: 84, w: 50, fol: '#44664a', fall: '#9c4a52', bark: '#5f5348', wind: WSHRUB,
      p: { form: 'clump', stems: 4, spread: 0.3, forks: 2, grain: 'broad', leafDen: 0.6, leafFrom: 0.4 }, bloom: { s: ['summer'], c: '#e6e2d2', n: 6, k: 'umbel' }, fruit: { s: ['autumn'], c: '#4a4f7a', n: 28 } },
    { key: 'RedElderberry', name: 'Red Elderberry', latin: 'Sambucus racemosa', fam: 'shrub', hab: 'woods', habit: 'woody', h: 96, w: 56, fol: '#5f7a4a', fall: '#8a8a4a', bark: '#7a6b58', wind: WSHRUB,
      p: { form: 'clump', stems: 4, spread: 0.35, forks: 1, arch: 0.35, grain: 'pinnate', leafDen: 0.6, leafFrom: 0.35 }, bloom: { s: ['spring'], c: '#ece6c8', n: 6, k: 'umbel' }, fruit: { s: ['summer'], c: '#c0302a', n: 24 } },
    // shore
    { key: 'SugarKelp', name: 'Sugar Kelp', latin: 'Saccharina latissima', fam: 'shore', hab: 'fringe', zone: 0.15, habit: 'alga', h: 88, w: 70, fol: '#5a461c', limp: 0.92, trans: 1.0, wind: WALGA, p: { form: 'kelp', blades: 3, bw: 9 } },
    { key: 'IrishMoss', name: 'Irish Moss', latin: 'Chondrus crispus', fam: 'shore', hab: 'fringe', zone: 0.15, habit: 'alga', h: 17, w: 46, fol: '#5e2a3e', bleach: '#b9a47c', limp: 0.5, wind: WALGA, p: { form: 'turf', n: 48 } },
    { key: 'Eelgrass', name: 'Eelgrass', latin: 'Zostera marina', fam: 'shore', hab: 'fringe', zone: 0.15, habit: 'grass', h: 46, w: 62, fol: '#416b34', fall: '#5d5a2c', limp: 0.62, trans: 0.9, wind: WALGA, p: { blades: 13, crown: 14, el: 1.25, fall: 0.9, bw: 2, comb: 0.6 } },
    { key: 'KnottedWrack', name: 'Knotted Wrack', latin: 'Ascophyllum nodosum', fam: 'shore', hab: 'mid', zone: 1.55, habit: 'alga', h: 52, w: 60, fol: '#574a1c', limp: 0.84, wind: WALGA, p: { form: 'frond', straps: 5, bw: 2.4, knots: true } },
    { key: 'Bladderwrack', name: 'Bladderwrack', latin: 'Fucus vesiculosus', fam: 'shore', hab: 'mid', zone: 1.55, habit: 'alga', h: 34, w: 50, fol: '#4c4a22', limp: 0.8, wind: WALGA, p: { form: 'frond', straps: 6, bw: 3, pairs: true } },
    { key: 'SeaLettuce', name: 'Sea Lettuce', latin: 'Ulva lactuca', fam: 'shore', hab: 'mid', zone: 1.55, habit: 'alga', h: 15, w: 44, fol: '#5b8f3c', limp: 0.95, trans: 1.2, wind: WALGA, p: { form: 'sheet', n: 9 } },
    { key: 'Cordgrass', name: 'Saltmarsh Cordgrass', latin: 'Spartina alterniflora', fam: 'shore', hab: 'lowmarsh', zone: 2.55, habit: 'grass', h: 58, w: 54, fol: '#5c7d3a', fall: '#c2a552', wind: WGRASS, p: { blades: 18, crown: 8, el: 1.46, fall: 0.28, bw: 2, head: 'cord', culms: 5 } },
    { key: 'Glasswort', name: 'Glasswort', latin: 'Salicornia maritima', fam: 'shore', hab: 'lowmarsh', zone: 2.55, habit: 'succulent', h: 15, w: 40, fol: '#5f8a4e', fall: '#b8382f', wind: WMAT, p: { stems: 9 } },
    { key: 'SaltmeadowHay', name: 'Saltmeadow Hay', latin: 'Spartina patens', fam: 'shore', hab: 'highmarsh', zone: 3.35, habit: 'grass', h: 24, w: 58, fol: '#7d8a4a', fall: '#cbb072', wind: WGRASS, p: { blades: 34, crown: 16, el: 0.9, fall: 1.2, bw: 1, comb: 1.0, cowlick: true } },
    { key: 'BlackRush', name: 'Black Rush', latin: 'Juncus gerardii', fam: 'shore', hab: 'highmarsh', zone: 3.35, habit: 'grass', h: 21, w: 34, fol: '#3b563e', fall: '#8a7d4a', wind: WGRASS, p: { blades: 18, crown: 7, el: 1.42, fall: 0.2, bw: 1, head: 'rush', culms: 6, stiff: true } },
    { key: 'Cattail', name: 'Cattail', latin: 'Typha latifolia', fam: 'shore', hab: 'highmarsh', zone: 3.35, habit: 'grass', h: 66, w: 46, fol: '#547a3c', fall: '#b8a05a', spike: '#5a3a24', wind: WGRASS, p: { blades: 10, crown: 6, el: 1.5, fall: 0.3, bw: 3, head: 'cattail', culms: 2 } },
    { key: 'Threesquare', name: 'Threesquare Bulrush', latin: 'Schoenoplectus pungens', fam: 'shore', hab: 'highmarsh', zone: 3.35, habit: 'grass', h: 36, w: 36, fol: '#4c7040', fall: '#a89250', wind: WGRASS, p: { blades: 12, crown: 7, el: 1.48, fall: 0.1, bw: 1, head: 'three', culms: 5, stiff: true } },
    { key: 'MarramGrass', name: 'Marram Grass', latin: 'Ammophila breviligulata', fam: 'shore', hab: 'upland', zone: 4.65, habit: 'grass', h: 33, w: 50, fol: '#7d9470', fall: '#bcb07a', wind: WGRASS, p: { blades: 22, crown: 10, el: 1.2, fall: 0.9, bw: 1.4, head: 'marram', culms: 3, comb: 0.5, dry: 0.25 } },
    { key: 'Bayberry', name: 'Bayberry', latin: 'Morella pensylvanica', fam: 'shore', hab: 'upland', zone: 4.65, habit: 'woody', h: 58, w: 74, fol: '#56704f', fall: '#5a6a44', winter: '#6a6a4a', semi: true, bark: '#6b6155', wind: WSHRUB,
      p: { form: 'dome', stems: 9, spread: 0.55, forks: 2, grain: 'coin', leafDen: 1.0, leafFrom: 0.25 }, fruit: { s: ['autumn'], c: '#aeb6a4', n: 22, holds: true, k: 'wax' } },
    { key: 'SweetFern', name: 'Sweet Fern', latin: 'Comptonia peregrina', fam: 'shore', hab: 'upland', zone: 4.65, habit: 'woody', h: 37, w: 56, fol: '#5c7038', fall: '#9c5a2c', bark: '#6b4a38', wind: WSHRUB,
      p: { form: 'clump', stems: 7, spread: 0.42, grain: 'fern', leafDen: 1.0, leafFrom: 0.3 } },
    { key: 'BeachPea', name: 'Beach Pea', latin: 'Lathyrus japonicus', fam: 'shore', hab: 'upland', zone: 4.65, habit: 'sprawl', h: 16, w: 78, fol: '#648a5c', fall: '#8a8a4a', wind: WMAT, p: { runners: 6 }, bloom: { s: ['summer'], c: '#8464ad', n: 8 } },
    // flowers
    { key: 'Lupin', name: 'Lupin', latin: 'Lupinus polyphyllus', fam: 'flower', hab: 'roadside', habit: 'herb', h: 40, w: 26, fol: '#4c7a45', morphs: ['#7d5aa8', '#d17aa6', '#5a72b8', '#e9edea'], morphNames: ['purple', 'pink', 'blue', 'white'], wind: WHERB,
      p: { head: 'spike', basal: 'palmate', bloomS: ['summer'] } },
    { key: 'LadySlipper', name: "Lady's Slipper", latin: 'Cypripedium acaule', fam: 'flower', hab: 'woods', habit: 'herb', h: 24, w: 18, fol: '#4f7a45', bloomC: '#e58fb4', wind: WHERB, p: { head: 'orchid', basal: 'pair', bloomS: ['spring'] } },
    { key: 'Fireweed', name: 'Fireweed', latin: 'Chamaenerion angustifolium', fam: 'flower', hab: 'roadside', habit: 'herb', h: 42, w: 22, fol: '#3c6f4a', bloomC: '#c85a90', wind: WHERB, p: { head: 'spike2', leaves: 'alt', bloomS: ['summer'] } },
    { key: 'QueenAnne', name: "Queen Anne's Lace", latin: 'Daucus carota', fam: 'flower', hab: 'roadside', habit: 'herb', h: 38, w: 24, fol: '#4f7a45', bloomC: '#eef0ea', wind: WHERB, p: { head: 'umbel', basal: 'feather', bloomS: ['summer'] } },
    { key: 'Goldenrod', name: 'Goldenrod', latin: 'Solidago canadensis', fam: 'flower', hab: 'roadside', habit: 'herb', h: 40, w: 24, fol: '#4c7040', bloomC: '#e0b23a', wind: WHERB, p: { head: 'plume', leaves: 'alt', bloomS: ['autumn'] } },
    { key: 'OxeyeDaisy', name: 'Oxeye Daisy', latin: 'Leucanthemum vulgare', fam: 'flower', hab: 'meadow', habit: 'herb', h: 32, w: 20, fol: '#4f7a45', bloomC: '#eef0ea', eye: '#e6c23c', wind: WHERB, p: { head: 'disc', basal: 'rosette', leaves: 'few', bloomS: ['summer'] } },
    { key: 'BlueFlag', name: 'Blue Flag Iris', latin: 'Iris versicolor', fam: 'flower', hab: 'wet', habit: 'herb', h: 38, w: 22, fol: '#3f7a52', bloomC: '#5a6fb2', eye: '#e6c23c', wind: WHERB, p: { head: 'iris', basal: 'sword', bloomS: ['summer'] } },
    { key: 'Buttercup', name: 'Buttercup & Clover', latin: 'Ranunculus acris · Trifolium', fam: 'flower', hab: 'meadow', habit: 'herb', h: 22, w: 30, fol: '#4f7a45', bloomC: '#f0c62c', clover: '#d9a0c0', wind: WHERB, p: { head: 'cup', basal: 'lobed', bloomS: ['summer'] } },
    // grasses
    { key: 'MeadowGrass', name: 'Meadow Grass', latin: 'Poa pratensis', fam: 'grass', hab: 'meadow', habit: 'grass', h: 30, w: 26, fol: '#567834', fall: '#b7a066', wind: WGRASS, p: { blades: 20, crown: 6, el: 1.25, fall: 0.8, bw: 1, head: 'panicle', culms: 4 } },
    { key: 'Timothy', name: 'Timothy', latin: 'Phleum pratense', fam: 'grass', hab: 'meadow', habit: 'grass', h: 44, w: 24, fol: '#5f7a3a', fall: '#b09a60', wind: WGRASS, p: { blades: 12, crown: 5, el: 1.2, fall: 0.9, bw: 1.4, head: 'timothy', culms: 4 } },
    { key: 'SoftRush', name: 'Soft Rush', latin: 'Juncus effusus', fam: 'grass', hab: 'wet', habit: 'grass', h: 40, w: 28, fol: '#4f6f38', fall: '#8a7d4a', wind: WGRASS, p: { blades: 26, crown: 5, el: 1.36, fall: 0.35, bw: 1, head: 'rush', culms: 5, stiff: true } },
    { key: 'TussockSedge', name: 'Tussock Sedge', latin: 'Carex stricta', fam: 'grass', hab: 'wet', habit: 'grass', h: 42, w: 34, fol: '#6a7a34', fall: '#a89458', wind: WGRASS, p: { blades: 26, crown: 6, el: 1.2, fall: 1.0, bw: 1.4, head: 'sedge', culms: 3, mound: true } },
  ];
  const byKey = {}; SPECIES.forEach(s => { byKey[s.key] = s; s.trans = s.trans == null ? 0.8 : s.trans; });
  const FAMILIES = [['shrub', 'Shrubs'], ['shore', 'Shore'], ['flower', 'Flowers'], ['grass', 'Grasses']];
  const HABITATS = { barren: 'Blueberry barren', bog: 'Bog & fen', swale: 'Alder swale', edge: 'Field edge', woods: 'Woods edge', fringe: 'Subtidal fringe', mid: 'Mid intertidal', lowmarsh: 'Low marsh', highmarsh: 'High marsh', upland: 'Dune & upland', roadside: 'Roadside', meadow: 'Meadow', wet: 'Wet ground' };

  // ---- the builder ------------------------------------------------------------------------------------------------
  function Builder(sp, o) {
    this.sp = sp; this.o = o; this.prims = []; this.k = 0; this.part = 0; this.nS = 0; this.sd = o.seed;
    this.H = sp.h * o.size; this.W = sp.w * (0.55 + 0.45 * o.size);
    this.ramps = [SNOW.map(h2r)]; this.rmap = new Map();
    this.env = { c: [0, 0, this.H * 0.55], r: [this.W * 0.5, this.W * 0.42, this.H * 0.5] };
  }
  Builder.prototype.rnd = function () { this.k++; let a = (this.rs = ((this.rs == null ? (this.sd ^ 0x5bd1e995) : this.rs) + 0x6D2B79F5) | 0); let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };
  Builder.prototype.ramp = function (kind, hex) {
    const key = kind + hex; if (this.rmap.has(key)) return this.rmap.get(key);
    const R = (kind === 'bark' ? barkRamp : kind === 'petal' ? petalRamp : kind === 'fruit' ? fruitRamp : folRamp)(hex).map(h2r);
    this.ramps.push(R); this.rmap.set(key, this.ramps.length - 1); return this.ramps.length - 1;
  };
  Builder.prototype.envN = function (p, k) {
    const E = this.env, g = [(p[0] - E.c[0]) / (E.r[0] * E.r[0]), (p[1] - E.c[1]) / (E.r[1] * E.r[1]), (p[2] - E.c[2]) / (E.r[2] * E.r[2])], n = nrm(g);
    const t = [this.rnd() - 0.5, this.rnd() - 0.5, this.rnd() - 0.5];
    return nrm(toView(nrm([n[0] * k + t[0] * 0.5, n[1] * k + t[1] * 0.5, n[2] * k + 0.55 + t[2] * 0.3])));
  };
  Builder.prototype.stamp = function (p, st, rid, part, o) {
    o = o || {};
    this.prims.push({ k: 0, p, st, rid, mat: o.mat == null ? M.LEAF : o.mat, part, tone: o.tone == null ? (this.rnd() < 0.1 ? -1 : this.rnd() < 0.1 ? 1 : 0) : o.tone, n: o.n || this.envN(p, 0.8), id: this.nS++, zb: o.zb || 0, fl: o.fl == null ? 1 : o.fl, ng: o.ng || 0 });
  };
  Builder.prototype.leaf = function (p, grain, rid, part, o) { const S = GR[grain] || GR.broad; this.stamp(p, S[Math.floor(this.rnd() * S.length)], rid, part, o); };
  Builder.prototype.seg = function (a, b, r0, r1, rid, mat, part) { this.prims.push({ k: 1, a, b, r0, r1, rid, mat: mat == null ? M.WOOD : mat, part }); };
  Builder.prototype.chain = function (pts, r0, r1, rid, mat, part) {
    const n = pts.length - 1; for (let q = 0; q < n; q++) this.seg(pts[q], pts[q + 1], r0 + (r1 - r0) * q / n, r0 + (r1 - r0) * (q + 1) / n, rid, mat, part);
  };
  /* a strap: split into short pieces that share one stamp id (one blade = one stamp for seam and tip),
     each piece its own light sample, so a blade's base in the clump is dark and its tip is lit */
  Builder.prototype.blade = function (pts, w0, w1, rid, part, o) {
    o = o || {}; const id = this.nS++, n = pts.length - 1, sideN = o.n || null;
    const tv = nrm([pts[n][0] - pts[0][0], pts[n][1] - pts[0][1], pts[n][2] - pts[0][2]]);
    const nW = sideN || nrm([-tv[1] * 0.5 + (this.rnd() - 0.5) * 0.6, 0.5 + tv[0] * 0.3, 0.75 + (this.rnd() - 0.5) * 0.3]), nV = nrm(toView(nW));
    for (let q = 0; q < n; q++) this.prims.push({ k: 2, a: pts[q], b: pts[q + 1], w0: w0 + (w1 - w0) * q / n, w1: w0 + (w1 - w0) * (q + 1) / n, rid, mat: o.mat == null ? M.BLADE : o.mat, part, n: nV, id, tone: o.tone || 0 });
    return id;
  };
  Builder.prototype.fleck = function (p, rid, part, mat) { this.prims.push({ k: 3, p, rid, part, mat: mat == null ? M.FRUIT : mat, id: this.nS++, n: nrm(toView([this.rnd() - 0.5, 0.3, 0.9])) }); };
  /* a leaf mass: an ellipsoid (world radii r = [x, y depth, z up]) drawn with the grain's edge profile */
  Builder.prototype.mass = function (p, r, rid, part, o) {
    o = o || {};
    const q = { k: 4, p, r, rid, rid2: o.rid2, part, mat: o.mat == null ? M.LEAF : o.mat, tone: o.tone == null ? -1 : o.tone, id: this.nS++, E: o.E || this.E || EDGE5.round, sd: this.rnd() * 7, zb: o.zb || 0 };
    q.rs = [r[0], Math.hypot(r[2] * CE, r[1] * SE), Math.hypot(r[2] * SE, r[1] * CE)];
    this.prims.push(q); (this.masses || (this.masses = [])).push(q); return q;
  };
  /* leaves over a mass's front, each on the ellipsoid's own normal: the lit dome and its shaded underside */
  Builder.prototype.cover = function (q, grain, rid, k, rid2) {
    const S = GR[grain] || GR.broad, avg = S.reduce((a, s) => a + s.px.length, 0) / S.length;
    const n = Math.max(2, Math.round(Math.PI * q.rs[0] * q.rs[1] * (k || 1.15) / avg * 0.9));
    for (let j = 0; j < n; j++) {
      let u = [this.rnd() * 2 - 1, this.rnd() * 2 - 1, this.rnd() * 2 - 1 + 0.35]; const L = Math.hypot(u[0], u[1], u[2]) || 1; u = [u[0] / L, u[1] / L, u[2] / L];
      const dv = u[1] * CE + u[2] * SE; if (dv < 0) u = [u[0], u[1] - 2 * dv * CE, u[2] - 2 * dv * SE];
      const p = [q.p[0] + u[0] * q.r[0] * 0.9, q.p[1] + u[1] * q.r[1] * 0.9, q.p[2] + u[2] * q.r[2] * 0.9];
      const g = nrm([u[0] / q.r[0], u[1] / q.r[1], u[2] / q.r[2]]), wn = nrm([g[0] + (this.rnd() - 0.5) * 0.4, g[1] + (this.rnd() - 0.5) * 0.4, g[2] + (this.rnd() - 0.5) * 0.3]);
      this.leaf(p, grain, rid2 && this.rnd() < 0.18 ? rid2 : rid, q.part, { n: nrm(toView(wn)), zb: 0.5, ng: 1 });
    }
  };
  /* a strap whose ramp changes along it (dark base, pale tip) — still one stamp for seam and tip */
  Builder.prototype.blade5 = function (pts, w0, w1, ridAt, part, o) {
    o = o || {}; const id = this.nS++, n = pts.length - 1;
    const tv = nrm([pts[n][0] - pts[0][0], pts[n][1] - pts[0][1], pts[n][2] - pts[0][2]]);
    const nV = o.n || nrm(toView(nrm([-tv[1] * 0.5 + (this.rnd() - 0.5) * 0.6, 0.5 + tv[0] * 0.3, 0.75 + (this.rnd() - 0.5) * 0.3])));
    for (let q = 0; q < n; q++) { const wa = o.wAt ? o.wAt(q / n) : w0 + (w1 - w0) * q / n, wb = o.wAt ? o.wAt((q + 1) / n) : w0 + (w1 - w0) * (q + 1) / n; this.prims.push({ k: 2, a: pts[q], b: pts[q + 1], w0: wa, w1: wb, rid: typeof ridAt === 'function' ? ridAt(q, n) : ridAt, mat: o.mat == null ? M.BLADE : o.mat, part, n: nV, id, tone: o.tone || 0 }); }
    return id;
  };
  const bez = (p0, p1, p2, t) => { const u = 1 - t; return [u * u * p0[0] + 2 * u * t * p1[0] + t * t * p2[0], u * u * p0[1] + 2 * u * t * p1[1] + t * t * p2[1], u * u * p0[2] + 2 * u * t * p1[2] + t * t * p2[2]]; };
  const curve = (p0, p1, p2, n) => { const out = []; for (let q = 0; q <= n; q++) out.push(bez(p0, p1, p2, q / n)); return out; };
  /* a blade from a root: rises at el0 (radians above the ground plane), arches over by `fall`, turns by `bow` */
  function bladePts(root, th, len, el0, fall, bow, lay, n) {
    const pts = [root.slice()]; let p = root.slice(); n = n || Math.max(3, Math.round(len / 3));
    for (let q = 1; q <= n; q++) {
      const s = q / n, el = (el0 - fall * Math.pow(s, 1.4)) * (1 - lay * 0.9), a = th + bow * s, st = len / n;
      p = [p[0] + Math.cos(el) * Math.cos(a) * st, p[1] + Math.cos(el) * Math.sin(a) * st * 0.8, Math.max(0.2, p[2] + Math.sin(el) * st)];
      pts.push(p.slice());
    }
    return pts;
  }

  // ---- phenology ------------------------------------------------------------------------------------------------------
  function phen(B, sp) {
    const s = B.o.season, dec = !sp.evergreen;
    B.bare = s === 'winter' && dec && !sp.semi;
    B.leafK = s === 'spring' ? 0.55 : s === 'autumn' ? 0.85 : s === 'winter' ? (sp.semi ? 0.45 : 0.9) : 1;
    const fol = s === 'spring' && dec ? mix(sp.fol, SPRING, 0.28) : s === 'autumn' && sp.fall ? sp.fall : s === 'winter' ? (sp.winter || mix(sp.fol, '#2c4a4f', 0.25)) : sp.fol;
    B.rF = B.ramp('fol', fol); B.rF2 = B.ramp('fol', s === 'autumn' && sp.fall ? mix(sp.fall, sp.fol, 0.3) : mix(fol, '#2c4a4f', 0.12));
    B.rB = B.ramp('bark', s === 'winter' && sp.winterBark ? sp.winterBark : sp.bark || '#5a4a3c');
    B.rS = B.ramp('fol', s === 'winter' ? DEAD : s === 'autumn' ? mix(sp.fol, DEAD, 0.45) : (sp.stem || sp.fol));
    const on = (x) => x && (x.s.includes(s) || (s === 'winter' && x.holds));
    B.bloomOn = on(sp.bloom) && !(sp.bloom.bare && B.o.stage === 'seedling');
    B.fruitOn = on(sp.fruit) && B.o.stage !== 'seedling'; B.catkinOn = on(sp.catkin);
    if (B.bloomOn && sp.bloom.bare && s === 'spring') B.leafK = 0.18;
  }

  // ---- habits -----------------------------------------------------------------------------------------------------------
  const scr = (p) => [p[0], p[1] * SE - p[2] * CE];
  /* crown sites: every 2 px along the outer part of each branch, tips first, thinned to a Poisson-spaced set in screen space */
  function sites(B, branches, from, dmin, cap) {
    const cand = [];
    for (const b of branches) {
      const pts = b.pts, n = pts.length - 1, f0 = Math.max(0, Math.floor(n * (b.from == null ? from : b.from)));
      for (let q = f0; q < n; q++) { const a = pts[q], c = pts[q + 1], m = Math.max(1, Math.round(Math.hypot(c[0] - a[0], c[1] - a[1], c[2] - a[2]) / 2)); for (let j = 0; j < m; j++) { const f = j / m; cand.push({ p: [a[0] + (c[0] - a[0]) * f, a[1] + (c[1] - a[1]) * f, a[2] + (c[2] - a[2]) * f], part: b.part, tip: 0 }); } }
      cand.push({ p: pts[n], part: b.part, tip: 1 });
    }
    for (let i = cand.length - 1; i > 0; i--) { const j = Math.floor(B.rnd() * (i + 1)), t = cand[i]; cand[i] = cand[j]; cand[j] = t; }
    cand.sort((a, b) => b.tip - a.tip);
    const out = [], d2 = dmin * dmin;
    for (const c of cand) { const s = scr(c.p); let ok = true; for (const o of out) { const dx = o.s[0] - s[0], dy = o.s[1] - s[1]; if (dx * dx + dy * dy < d2) { ok = false; break; } } if (ok) { c.s = s; out.push(c); if (out.length >= cap) break; } }
    return out;
  }
  /* the trees' floret at shrub size: a core mass and up to two satellites riding its top and shoulder */
  function floret(B, c, mr, part, flat, rid, rid2) {
    B.mass([c[0], c[1], c[2] + mr * 0.1], [mr, mr * 0.8, mr * flat], rid, part);
    const ns = mr > 3.6 ? 2 : mr > 2.6 ? 1 : 0;
    for (let s = 0; s < ns; s++) { const a = B.rnd() * TAU, rr = mr * (0.55 + 0.25 * B.rnd()); B.mass([c[0] + Math.cos(a) * mr * 0.7, c[1] + Math.sin(a) * mr * 0.5, c[2] + mr * (0.25 + 0.45 * B.rnd())], [rr, rr * 0.8, rr * flat], B.rnd() < 0.3 ? rid2 : rid, part); }
  }
  const HABITS = {
    woody(B, sp) {
      phen(B, sp);
      const P = Q5(sp), H = B.H, W = B.W, form = P.form, size = B.o.size, s = B.o.season, halfW = W * 0.5;
      const grain = P.grain || 'broad'; B.E = EDGE5[GRAIN_EDGE[grain] || 'round'];
      const stems = Math.max(2, Math.round(P.stems * (0.5 + 0.5 * size))), r0 = (P.r0 || (H > 60 ? 0.95 : H > 25 ? 0.75 : 0.55)) * (0.6 + 0.4 * size);
      const crownR = Math.min(W * 0.18, H > 60 ? 7 : 3.5) * (0.5 + 0.5 * size);
      if (form === 'dome') B.env = { c: [0, 0, H * 0.5], r: [W * 0.5, W * 0.42, H * 0.52] };
      if (form === 'mat') B.env = { c: [0, 0, H * 0.2], r: [W * 0.55, W * 0.42, H * 0.9] };
      const branches = [];
      for (let k = 0; k < stems; k++) {
        const th = (k + B.rnd() * 0.8) / stems * TAU, fr = B.rnd(), out = [Math.cos(th), Math.sin(th)], rb = crownR * (0.3 + 0.7 * B.rnd()), base = [out[0] * rb, out[1] * rb * 0.6, 0];
        let phi = Math.max(form === 'clump' ? 0.22 : 0, P.spread == null ? 0.4 : P.spread) * (0.35 + 0.65 * fr) * (form === 'antler' ? 1.3 : 1), L = H * (0.72 + 0.3 * B.rnd());
        if (form === 'mat') { phi = 1.2 + 0.25 * fr; L = W * (0.3 + 0.18 * B.rnd()); }
        if (form === 'dome') { L = H * (0.6 + 0.35 * B.rnd()); phi = 0.1 + 1.15 * fr; }
        const tipH = form === 'mat' ? H * (0.3 + 0.55 * B.rnd()) : Math.min(H * 0.96, Math.cos(phi) * L), reach = Math.min(halfW * 0.86, form === 'mat' ? L : Math.sin(phi) * L), arch = P.arch || 0;
        let tip = [base[0] + out[0] * reach, base[1] + out[1] * reach * 0.75, tipH];
        if (arch) tip = [tip[0] + out[0] * L * arch * 0.28, tip[1] + out[1] * L * arch * 0.2, tip[2] * (1 - 0.45 * arch)];
        const midP = [base[0] + (tip[0] - base[0]) * 0.3, base[1] + (tip[1] - base[1]) * 0.3, tip[2] * (form === 'mat' ? 0.95 : 0.6 + 0.3 * arch)];
        const n = Math.max(3, Math.round(L / 4)), pts = curve(base, midP, tip, n), part = B.part++;
        B.chain(pts, r0, 0.5, B.rB, M.WOOD, part); branches.push({ pts, part, L });
        const forks = P.forks == null ? 1 : P.forks;
        for (let f = 0; f < forks; f++) {
          const at = 0.35 + 0.4 * B.rnd(), j = Math.max(1, Math.min(n - 1, Math.floor(at * n))), fb = pts[j];
          const side = (B.rnd() < 0.5 ? -1 : 1) * (0.45 + 0.45 * B.rnd()), a2 = th + side, fl = L * (1 - at) * (0.6 + 0.3 * B.rnd());
          const ft = [fb[0] + Math.cos(a2) * fl * (form === 'mat' ? 0.9 : 0.5), fb[1] + Math.sin(a2) * fl * 0.4, Math.min(H * 0.98, fb[2] + fl * (form === 'mat' ? 0.12 : 0.72))];
          if (Math.abs(ft[0]) > halfW * 0.9) ft[0] = Math.sign(ft[0]) * halfW * 0.9;
          const fm = [(fb[0] + ft[0]) / 2, (fb[1] + ft[1]) / 2, Math.max(fb[2], ft[2]) - fl * 0.08 * arch], fp = curve(fb, fm, ft, Math.max(2, Math.round(fl / 4))), fpart = B.part++;
          B.chain(fp, Math.max(0.5, r0 * 0.6), 0.45, B.rB, M.WOOD, fpart); branches.push({ pts: fp, part: fpart, L: fl, from: 0.1 });
          if (form === 'antler' && f === 0) for (const sg of [-1, 1]) {
            const a3 = a2 + sg * 0.5, t2 = [ft[0] + Math.cos(a3) * fl * 0.35, ft[1] + Math.sin(a3) * fl * 0.2, Math.min(H, ft[2] + fl * 0.3)], ap = curve(ft, [(ft[0] + t2[0]) / 2, (ft[1] + t2[1]) / 2, t2[2]], t2, 3), apart = B.part++;
            B.chain(ap, 0.75, 0.45, B.rB, M.WOOD, apart); branches.push({ pts: ap, part: apart, L: fl * 0.35, from: 0.5 });
          }
        }
      }
      const tips = branches.map(b => ({ p: b.pts[b.pts.length - 1], part: b.part, pts: b.pts }));
      const flat = form === 'mat' ? 0.5 : form === 'dome' ? 0.85 : form === 'thicket' ? 0.7 : 0.78;
      const mr0 = (P.mr || (H < 22 ? 2.3 : H < 45 ? 2.9 : H < 90 ? 3.8 : 4.6)) * (0.62 + 0.38 * size), dens = (P.leafDen || 0.7) * B.leafK;
      if (!B.bare && form !== 'antler' && dens > 0.05) {
        const A = form === 'mat' ? W * (H * CE + W * 0.4 * SE) : form === 'dome' ? W * 0.85 * H * CE : W * 0.72 * H * CE, cap = Math.round(clamp(A / (Math.PI * mr0 * mr0 * 0.85), 5, 110) * Math.min(1.2, dens + 0.25));
        for (const c of sites(B, branches, P.leafFrom == null ? 0.35 : P.leafFrom, mr0 * (1.55 - 0.3 * Math.min(1, dens)), cap)) floret(B, c.p, mr0 * (0.8 + 0.35 * B.rnd()), c.part, flat, B.rF, B.rF2);
        for (const q of B.masses || []) B.cover(q, grain, q.rid, P.cover || 1.15, B.rF2);
        for (const t of tips) for (let j = 0; j < 2; j++) if (B.rnd() < dens) { const a = B.rnd() * TAU; B.leaf([t.p[0] + Math.cos(a) * mr0 * 1.1, t.p[1] + Math.sin(a) * mr0 * 0.7, t.p[2] + mr0 * (0.2 + 0.6 * B.rnd())], grain, B.rF, t.part); }
      }
      if (form === 'antler' && !B.bare) for (const t of tips) {       // sumac: drooping compound leaves off the antler tips
        B.mass([t.p[0], t.p[1], t.p[2] + 1], [2.4, 2, 1.8], B.rF2, t.part);
        const nL = 4 + Math.floor(B.rnd() * 3);
        for (let j = 0; j < nL; j++) {
          const a = (j / nL) * TAU + B.rnd() * 0.6, len = 10 + B.rnd() * 7, p0 = t.p, tipL = [p0[0] + Math.cos(a) * len, p0[1] + Math.sin(a) * len * 0.6, p0[2] - len * (0.2 + 0.3 * B.rnd())];
          const rp = curve(p0, [p0[0] + Math.cos(a) * len * 0.5, p0[1] + Math.sin(a) * len * 0.3, p0[2] + 2.5], tipL, 6);
          B.chain(rp, 0.45, 0.4, B.rS, M.STEM, t.part);
          for (let q = 1; q <= 6; q++) for (const sg of [-1, 1]) { const p = rp[q], lp = [p[0] - Math.sin(a) * sg * 2, p[1] + Math.cos(a) * sg * 1.3, p[2] - 0.8]; if (B.rnd() < B.leafK) B.leaf(lp, 'lance', q > 4 && B.rnd() < 0.3 ? B.rF2 : B.rF, t.part, { n: nrm(toView(nrm([-Math.sin(a) * sg * 0.4, 0.25, 0.9]))) }); }
        }
      }
      if (B.bare) for (const t of tips) for (let j = 0; j < 3; j++) { const a = B.rnd() * TAU, L2 = 2 + B.rnd() * 3, e = [t.p[0] + Math.cos(a) * L2, t.p[1] + Math.sin(a) * L2 * 0.6, t.p[2] + L2 * (0.4 + 0.5 * B.rnd())]; B.seg(t.p, e, 0.45, 0.4, B.rB, M.WOOD, t.part); }
      // flowers, fruit and catkins sit on the masses' front where there are masses, on the twigs where there are none
      const ms = (B.masses || []).filter(q => q.mat === M.LEAF);
      const site = () => {
        if (ms.length && B.rnd() < 0.85) { const q = ms[Math.floor(B.rnd() * ms.length)], a = B.rnd() * Math.PI, e = 0.1 + B.rnd() * 0.8; return { p: [q.p[0] + Math.cos(a) * Math.cos(e) * q.r[0], q.p[1] + Math.sin(a) * Math.cos(e) * q.r[1], q.p[2] + Math.sin(e) * q.r[2]], part: q.part }; }
        const t = tips[Math.floor(B.rnd() * tips.length)], n = t.pts.length - 1; return { p: t.pts[Math.max(0, n - Math.floor(B.rnd() * n * 0.45))], part: t.part };
      };
      if (B.bloomOn) {
        const bl = sp.bloom, rP = B.ramp('petal', bl.c), k = bl.k || 'puff', nn = Math.round(bl.n * (0.5 + 0.5 * size) * (ms.length ? 1.3 : 1));
        if (bl.bare && s === 'spring') for (const t of tips) {         // bloom on bare wood: small clouds of petals at the twig ends
          const pm = B.mass([t.p[0], t.p[1], t.p[2] + 1], [2.2, 1.8, 1.7], rP, t.part, { mat: M.PETAL, tone: 0, E: EDGE5.soft });
          for (let j = 0; j < 3; j++) B.stamp([pm.p[0] + (B.rnd() - 0.5) * 3, pm.p[1] + 0.8, pm.p[2] + (B.rnd() - 0.2) * 2], PT.puff, rP, t.part, { mat: M.PETAL, zb: 0.8, tone: B.rnd() < 0.3 ? 1 : 0, n: nrm(toView([0, 0.4, 0.9])) });
        }
        for (let j = 0; j < nn; j++) {
          const st = site(), p = st.p;
          if (k === 'bell') B.stamp([p[0], p[1] + 0.5, p[2] - 1.5], PT.bell, rP, st.part, { mat: M.PETAL, zb: 0.8, tone: 0 });
          else if (k === 'spire') B.stamp([p[0], p[1] + 0.5, p[2] + 3], ST5.panicle, rP, st.part, { mat: M.PETAL, zb: 0.8, tone: 0, n: nrm(toView([0, 0.4, 0.9])) });
          else if (k === 'umbel') B.stamp([p[0], p[1] + 0.5, p[2] + 1], ST5.umb5, rP, st.part, { mat: M.PETAL, zb: 0.8, tone: 0, n: nrm(toView([0, 0.2, 1])) });
          else if (k === 'rose') { B.stamp([p[0], p[1] + 0.6, p[2]], PT.rose, rP, st.part, { mat: M.PETAL, zb: 1.0, tone: 0 }); B.fleck([p[0], p[1] + 0.9, p[2] + 0.3], B.ramp('petal', '#e6c23c'), st.part, M.PETAL); }
          else if (k === 'whorl') for (const dx of [-1.2, 1.2]) B.stamp([p[0] + dx, p[1] + 0.5, p[2] - 1], PT.pair, rP, st.part, { mat: M.PETAL, zb: 0.8, tone: 0 });
          else if (!(bl.bare && s === 'spring')) B.stamp([p[0], p[1] + 0.5, p[2] + 0.5], PT.puff, rP, st.part, { mat: M.PETAL, zb: 0.8, tone: B.rnd() < 0.3 ? 1 : 0 });
        }
      }
      if (B.fruitOn) {
        const fu = sp.fruit, rR = B.ramp('fruit', s === 'summer' && fu.s.includes('autumn') && !fu.s.includes('summer') ? '#5f7a3a' : fu.c), nn = Math.round(fu.n * (0.5 + 0.5 * size) * (ms.length ? 1.2 : 1));
        if (fu.k === 'cone') for (const t of tips.slice(0, Math.max(2, fu.n))) B.stamp([t.p[0], t.p[1] + 0.5, t.p[2] + 3.5], PT.cone, B.ramp('fruit', s === 'winter' ? '#6b2a24' : fu.c), t.part, { mat: M.FRUIT, zb: 1, tone: 0 });
        else for (let j = 0; j < nn; j++) { const st = site(), cl = fu.k === 'wax' ? 3 : 1 + Math.floor(B.rnd() * 3); for (let q = 0; q < cl; q++) B.fleck([st.p[0] + (B.rnd() - 0.5) * 2.4, st.p[1] + 0.8, st.p[2] - B.rnd() * 1.8], rR, st.part); }
      }
      if (B.catkinOn) {
        const ck = sp.catkin, rC = B.ramp('petal', ck.c);
        for (let j = 0; j < Math.round(ck.n * (0.5 + 0.5 * size)); j++) { const st = site(); B.stamp([st.p[0], st.p[1] + 0.6, st.p[2] - (ck.k === 'puss' ? -1 : 2)], ck.k === 'puss' ? PT.cup : PT.catkin, rC, st.part, { mat: M.PETAL, zb: 0.8, tone: 0 }); }
      }
    },

    grass(B, sp) {
      phen(B, sp);
      const P = Q5(sp), H = B.H, s = B.o.season, size = B.o.size, lay = B.lay || 0, halfW = B.W * 0.5;
      const n = Math.max(6, Math.round(P.blades * (P.dense || 1.7) * (0.45 + 0.55 * size))), rc = Math.max(1.5, (P.crown || 6) * 0.55 * (0.6 + 0.4 * size));
      const live = s === 'winter' ? DEAD : s === 'autumn' ? (sp.fall || DEAD) : s === 'spring' ? mix(sp.fol, SPRING, 0.25) : sp.fol;
      const rBase = B.ramp('fol', mix(live, '#1c2a18', 0.32)), rL = B.ramp('fol', live), rTip = B.ramp('fol', mix(live, s === 'summer' || s === 'spring' ? '#d6d8a0' : '#e0d2a6', 0.22));
      const rD = B.ramp('fol', mix(sp.fall || DEAD, DEAD, 0.4)), dryP = (P.dry || 0.08) + (s === 'autumn' ? 0.35 : s === 'winter' ? 0.6 : 0);
      const hK = s === 'spring' ? 0.62 : s === 'winter' ? 0.72 : 1, flat = s === 'winter' ? 0.3 : 0;
      B.env = { c: [0, 0, H * 0.35], r: [rc + H * 0.3, rc + H * 0.25, H * 0.7] }; B.E = EDGE5.spike;
      if (lay < 0.5 && P.crownMass !== false) B.mass([0, 0, Math.min(2.5, H * 0.08)], [rc * 1.1 + 1, rc * 0.8 + 0.8, Math.max(1.2, H * 0.09) * (P.mound ? 2 : 1)], rBase, 0, { mat: M.BLADE, tone: -1 });
      const comb = P.comb || 0, cth = -Math.PI / 2 + 0.35;
      for (let k = 0; k < n; k++) {
        const a = B.rnd() * TAU, rr = Math.sqrt(B.rnd()) * rc, o = rr / rc, root = [Math.cos(a) * rr, Math.sin(a) * rr * 0.6, 0];
        let th = P.cowlick ? a + Math.PI / 2 * (B.rnd() < 0.5 ? 1 : -1) * 0.8 : a + (B.rnd() - 0.5) * 1.1;
        if (comb) th = th * (1 - comb) + (cth + Math.PI + (B.rnd() - 0.5) * 0.9) * comb;
        const el0 = clamp((P.el || 1.25) + 0.18 - o * 0.5 - B.rnd() * 0.22, 0.35, 1.52), fall = (P.fall || 0.6) * (0.4 + 0.8 * B.rnd()) * (P.stiff ? 0.35 : 1) + flat;
        const len = H * hK * (0.5 + 0.5 * B.rnd()) * (1 - 0.18 * o) / Math.max(0.55, Math.sin(el0));
        const pts = bladePts(root, th, len, el0, fall, (B.rnd() - 0.5) * 0.5, lay);
        let mx = 0; for (const p of pts) mx = Math.max(mx, Math.abs(p[0])); if (mx > halfW) { const f = halfW / mx; for (const p of pts) { p[0] = root[0] + (p[0] - root[0]) * f; p[1] = root[1] + (p[1] - root[1]) * f; } }
        const dry = B.rnd() < dryP;
        B.blade5(pts, P.bw || 1, Math.max(0.6, (P.bw || 1) * 0.55), (q, m) => dry ? rD : q < m * 0.3 ? rBase : q >= m * 0.72 ? rTip : rL, k % 7);
      }
      const culms = P.head ? Math.max(1, Math.round((P.culms || 3) * size * 1.3)) : 0;
      if (!culms || (s === 'spring' && P.head !== 'cattail')) return;
      const hc = s === 'winter' ? mix(DEAD, '#6a5a44', 0.3) : s === 'autumn' ? (sp.fall || DEAD) : P.head === 'cattail' ? sp.spike : P.head === 'panicle' ? '#8a7466' : P.head === 'timothy' ? '#7d8a4c' : P.head === 'marram' ? '#c8bc8a' : '#6a5a3a';
      const rH = B.ramp(P.head === 'cattail' ? 'fruit' : 'fol', hc), rH2 = B.ramp('fol', mix(hc, '#2a2418', 0.3));
      for (let k = 0; k < culms; k++) {
        if (s === 'winter' && P.head !== 'cattail' && B.rnd() < 0.5) continue;
        const a = B.rnd() * TAU, rr = B.rnd() * rc * 0.6, root = [Math.cos(a) * rr, Math.sin(a) * rr * 0.6, 0], ch = H * (P.head === 'cattail' ? 1.05 : 1.1) * (0.82 + 0.22 * B.rnd()) * hK;
        const lean = (B.rnd() - 0.5) * (P.stiff ? 0.12 : 0.34), top = [root[0] + lean * ch, root[1], ch], part = 7 + (k % 5);
        const cp = curve(root, [root[0] + lean * ch * 0.15, root[1], ch * 0.5], top, Math.max(3, Math.round(ch / 4))), nT = cp.length - 1, hp = P.head;
        B.blade(cp, 1, 0.7, B.rS, part, { mat: M.STEM });
        if (hp === 'panicle') for (let j = 0; j < 5; j++) { const f = j / 4, sg = j % 2 ? 1 : -1, bp = [top[0], top[1], top[2] - f * 5], e = [bp[0] + sg * (2 + 2.5 * B.rnd()), bp[1], bp[2] - 2 - B.rnd() * 2.5]; B.seg(bp, e, 0.4, 0.4, B.rS, M.STEM, part); if (j % 2 === 0) B.fleck(e, rH, part, M.BLADE); }
        else if (hp === 'timothy') B.stamp([top[0], top[1] + 0.3, top[2] - 3], ST5.tim, rH, part, { mat: M.BLADE, tone: 0, n: nrm(toView([0, 0.6, 0.5])) });
        else if (hp === 'cattail') { B.stamp([top[0], top[1] + 0.3, top[2] - 5], ST5.cyl, rH, part, { mat: M.FRUIT, tone: 0, n: nrm(toView([-0.3, 0.6, 0.4])) }); B.seg([top[0], top[1], top[2] - 1], [top[0], top[1], top[2] + 4], 0.4, 0.4, B.rS, M.STEM, part); }
        else if (hp === 'rush') { const p = cp[Math.floor(nT * 0.72)]; for (let j = 0; j < 4; j++) B.fleck([p[0] + 0.8 + B.rnd() * 1.4, p[1] + 0.3, p[2] + (B.rnd() - 0.5) * 2.4], j % 2 ? rH : rH2, part, M.BLADE); }
        else if (hp === 'three') B.stamp([top[0] + 0.8, top[1] + 0.3, top[2] - 3], PT.cup, rH, part, { mat: M.BLADE, tone: 0 });
        else if (hp === 'cord') for (let j = 0; j < 3; j++) { const p = cp[Math.max(1, nT - j)]; B.stamp([p[0] + (j % 2 ? 0.8 : -0.8), p[1] + 0.3, p[2] - 1], PT.catkin, rH, part, { mat: M.BLADE, tone: 0 }); }
        else if (hp === 'marram') B.stamp([top[0], top[1] + 0.3, top[2] - 3.5], ST5.mar, rH, part, { mat: M.BLADE, tone: 0 });
        else if (hp === 'sedge') for (let j = 0; j < 2; j++) { const p = cp[Math.max(1, nT - j * 2)]; B.stamp([p[0] + (j ? 1 : -0.5), p[1] + 0.3, p[2] - 1], PT.catkin, j ? rH2 : rH, part, { mat: M.BLADE, tone: 0 }); }
      }
    },

    herb(B, sp) {
      phen(B, sp);
      const P = Q5(sp), H = B.H, s = B.o.season, size = B.o.size, stage = B.o.stage;
      const col = sp.morphs ? sp.morphs[B.o.variant % sp.morphs.length] : sp.bloomC || '#e0e0d0';
      const bloom = P.bloomS.includes(s) && stage !== 'seedling', seed = !bloom && (s === 'autumn' || s === 'winter') && stage !== 'seedling', dead = s === 'winter';
      const leafC = dead ? DEAD : s === 'autumn' && !bloom ? mix(sp.fol, DEAD, 0.35) : s === 'spring' ? mix(sp.fol, SPRING, 0.2) : sp.fol;
      const rL = B.ramp('fol', leafC), rL2 = B.ramp('fol', mix(leafC, '#1c2a18', 0.28)), rS = B.ramp('fol', dead ? mix(DEAD, '#6a5a44', 0.2) : sp.stem || mix(sp.fol, '#2a3a20', 0.12));
      const rP = B.ramp('petal', col), rP2 = B.ramp('petal', mix(col, '#2a1a2a', 0.25)), rPb = B.ramp('petal', mix(col, '#ffffff', 0.35));
      const rDry = B.ramp('fol', dead ? '#6a5a44' : '#7a6a4a'), UP = nrm(toView([0, 0.35, 0.95]));
      const plants = stage === 'mature' ? 3 + Math.floor(B.rnd() * 3) : stage === 'young' ? 2 : 1;
      B.env = { c: [0, 0, H * 0.4], r: [B.W * 0.5, B.W * 0.4, H * 0.6] }; B.E = EDGE5.round;
      const along = (cp, u) => { const f = clamp(u, 0, 1) * (cp.length - 1), i = Math.min(cp.length - 2, Math.floor(f)), t = f - i, a = cp[i], b = cp[i + 1]; return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t]; };
      for (let j = 0; j < plants; j++) {
        const x0 = plants > 1 ? (B.rnd() - 0.5) * B.W * 0.6 : 0, y0 = plants > 1 ? (B.rnd() - 0.5) * 5 : 0, part = j * 3, base = [x0, y0, 0];
        const h = H * (stage === 'seedling' ? 0.35 : 0.72 + 0.32 * B.rnd()) * (dead ? 0.85 : 1);
        basal(B, P.basal, base, h, rL, rL2, rS, part + 1, dead);
        if (P.head === 'cup') {
          for (let q = 0; q < 4; q++) { const a = B.rnd() * TAU, tp = [x0 + Math.cos(a) * 4.5, y0 + Math.sin(a) * 2, h * (0.55 + 0.45 * B.rnd())], cp = curve(base, [x0 + Math.cos(a), y0, tp[2] * 0.6], tp, 4); B.chain(cp, 0.45, 0.4, rS, M.STEM, part + 2);
            if (bloom) { B.stamp([tp[0], tp[1] + 0.4, tp[2] + 0.5], PT.cup, rP, part + 2, { mat: M.PETAL, zb: 0.8, tone: 0, n: UP }); if (q % 2 === 0) { const sb = along(cp, 0.6); B.seg(sb, [sb[0] + 2.5, sb[1], sb[2] + 2], 0.4, 0.4, rS, M.STEM, part + 2); B.stamp([sb[0] + 2.5, sb[1] + 0.4, sb[2] + 2.5], PT.cup, rP, part + 2, { mat: M.PETAL, zb: 0.8, tone: 0, n: UP }); } }
            else if (!dead) B.fleck(tp, rL, part + 2, M.LEAF); }
          if (!dead) for (let q = 0; q < 5; q++) { const p = [x0 + (B.rnd() - 0.5) * 13, y0 + (B.rnd() - 0.5) * 4, 1 + B.rnd()]; for (let l = 0; l < 3; l++) B.leaf([p[0] + (l - 1) * 1.4, p[1], p[2] + (l === 1 ? 0.8 : 0)], 'coin', B.ramp('fol', '#4a7a4a'), part, {}); if (bloom && q % 2 === 0) B.stamp([p[0], p[1] + 0.5, p[2] + 2], PT.cup, B.ramp('petal', q % 4 ? sp.clover : '#e8e6dc'), part, { mat: M.PETAL, zb: 0.8, tone: 0, n: UP }); }
          continue;
        }
        if ((s === 'spring' && !bloom) || (stage === 'seedling' && !bloom)) continue;
        const lean = (B.rnd() - 0.5) * 3, top = [x0 + lean, y0, h], nS = Math.max(4, Math.round(h / 3)), cp = curve(base, [x0 + lean * 0.2, y0, h * 0.55], top, nS), p2 = part + 2, hp = P.head;
        B.chain(cp, hp === 'iris' ? 0.6 : 0.5, 0.45, rS, M.STEM, p2);
        if (!dead && P.leaves === 'alt') for (let q = 1; q < nS - 2; q++) { const p = cp[q], sg = q % 2 ? 1 : -1, L = 4.4 - q / nS * 2; B.blade([p, [p[0] + sg * L * 0.8, p[1] + 0.6, p[2] + L * 0.5], [p[0] + sg * L, p[1] + 0.8, p[2] + L * 0.3]], 1.4, 0.8, q % 3 ? rL : rL2, p2); }
        if (!dead && P.leaves === 'few') for (let q = 1; q <= 2; q++) { const p = cp[Math.floor(nS * q / 4)]; B.leaf([p[0] + (q % 2 ? 1.6 : -1.6), p[1], p[2]], 'lance', rL, p2); }
        if (hp === 'spike') {                                // lupin: a raceme laid row by row, the live tip and seam shade each row
          const rows = Math.max(5, Math.round(h * 0.52 * CE));
          for (let r = 0; r < rows; r++) { const u = r / (rows - 1), p = along(cp, 0.48 + 0.52 * u), wS = u < 0.55 ? (r % 2 ? ST5.rw2 : ST5.rw3) : u < 0.86 ? ST5.rw2 : ST5.rw1;
            if (bloom) B.stamp([p[0] + (r % 2 ? 0.5 : -0.5), p[1] + 0.6, p[2]], wS, u < 0.22 ? rP2 : u < 0.8 ? rP : rPb, p2, { mat: M.PETAL, zb: 0.8, tone: 0, n: UP });
            else if (seed && r % 2 === 0) B.stamp([p[0] + (r % 4 ? 1 : -1), p[1] + 0.6, p[2]], ST5.rw2, rDry, p2, { mat: M.LEAF, tone: 0, n: UP }); }
        } else if (hp === 'spike2') {                        // fireweed: open four-petal flowers below, buds nodding above, fluff in seed
          const k0 = 0.58, nF = Math.max(3, Math.round(h * 0.42 / 2.6));
          for (let q = 0; q < nF; q++) { const u = k0 + (1 - k0) * q / nF, p = along(cp, u), sg = q % 2 ? 1 : -1;
            if (bloom) { if (q < nF * 0.65) { B.seg(p, [p[0] + sg * 1.6, p[1], p[2] + 0.8], 0.4, 0.4, rS, M.STEM, p2); B.stamp([p[0] + sg * 1.8, p[1] + 0.6, p[2] + 0.9], ST5.plus, q % 3 ? rP : rPb, p2, { mat: M.PETAL, zb: 0.8, tone: 0, n: UP }); } else B.stamp([p[0] + sg * 0.6, p[1] + 0.6, p[2] + 0.5], ST5.bud, rP2, p2, { mat: M.PETAL, zb: 0.6, tone: 0 }); }
            else if (seed) { B.seg(p, [p[0] + sg * 1.4, p[1], p[2] + 2.2], 0.4, 0.4, rDry, M.STEM, p2); if (s === 'autumn' || q % 2) B.stamp([p[0] + sg * 1.8, p[1] + 0.5, p[2] + 2.6], PT.fluff, B.ramp('petal', '#e8e4dc'), p2, { mat: M.PETAL, tone: 0 }); } }
        } else if (hp === 'plume') {                         // goldenrod: arching one-sided sprays off the top third
          const hc = bloom ? null : seed ? (dead ? '#8a7a62' : '#c8b89a') : mix(col, sp.fol, 0.6), rc = bloom ? rP : B.ramp('petal', hc);
          for (let q = 0; q < 6; q++) { const p = along(cp, 0.7 + q * 0.05), sg = q % 2 ? 1 : -1, L = 3 + (5 - q) * 0.7, e = [p[0] + sg * L, p[1], p[2] + 1.2 - q * 0.25], m = [(p[0] + e[0]) / 2, p[1], Math.max(p[2], e[2]) + 1.2], bp = curve(p, m, e, 4);
            B.chain(bp, 0.4, 0.4, rS, M.STEM, p2); for (let z = 1; z <= 4; z++) B.stamp([bp[z][0], bp[z][1] + 0.6, bp[z][2] + 0.8], z === 4 ? ST5.rw1 : ST5.rw2, z % 2 ? rc : (bloom ? rPb : rc), p2, { mat: M.PETAL, zb: 0.6, tone: 0, n: UP }); }
          if (bloom || seed) B.stamp([top[0], top[1] + 0.6, top[2] + 0.8], ST5.rw2, rc, p2, { mat: M.PETAL, zb: 0.6, tone: 0, n: UP });
        } else if (hp === 'umbel') {                         // Queen Anne's lace: a flat lacy umbel on its rays; a bird's nest in seed
          const umb = (c, big) => { const rim = big ? 3.4 : 2.4; for (let q = -1; q <= 1; q++) B.seg([c[0], c[1], c[2] - 2.2], [c[0] + q * rim * 0.8, c[1] + 0.3, c[2] - 0.2], 0.4, 0.4, rS, M.STEM, p2);
            if (bloom) { B.stamp([c[0], c[1] + 0.6, c[2] + 0.8], big ? ST5.umb7 : ST5.umb5, rP, p2, { mat: M.PETAL, zb: 0.8, tone: 0, n: nrm(toView([0, 0.1, 1])) }); if (big) B.fleck([c[0], c[1] + 1, c[2] + 1.4], B.ramp('petal', '#5a2a4a'), p2, M.PETAL); }
            else if (seed) B.stamp([c[0], c[1] + 0.6, c[2] + 0.8], PT.nest, rDry, p2, { mat: M.LEAF, tone: 0 }); };
          umb([top[0], top[1], top[2] + 1.2], true);
          if (stage === 'mature' && B.rnd() < 0.7) { const sb = along(cp, 0.62), e = [sb[0] + 3.5, sb[1], sb[2] + h * 0.25]; B.seg(sb, e, 0.4, 0.4, rS, M.STEM, p2); if (bloom) B.stamp([e[0], e[1] + 0.6, e[2] + 0.6], ST5.umb5, rP, p2, { mat: M.PETAL, zb: 0.8, tone: 0, n: nrm(toView([0, 0.1, 1])) }); else if (seed) B.stamp([e[0], e[1] + 0.6, e[2] + 0.6], PT.nest, rDry, p2, { mat: M.LEAF, tone: 0 }); }
        } else if (hp === 'disc') {                          // daisy: a ray disc and its eye
          if (bloom) { B.stamp([top[0], top[1] + 0.6, top[2] + 0.6], ST5.ray5, rP, p2, { mat: M.PETAL, zb: 0.6, tone: 0, n: nrm(toView([0, 0.4, 0.9])) }); B.stamp([top[0], top[1] + 1.1, top[2] + 0.9], ST5.rw2, B.ramp('petal', sp.eye), p2, { mat: M.PETAL, zb: 1.2, tone: 0, n: UP }); }
          else if (seed && !dead) B.fleck(top, rDry, p2, M.LEAF);
        } else if (hp === 'iris') {
          if (bloom) { B.stamp([top[0], top[1] + 0.6, top[2]], ST5.iris5, rP, p2, { mat: M.PETAL, zb: 0.8, tone: 0 }); B.fleck([top[0], top[1] + 1.1, top[2] - 0.6], B.ramp('petal', sp.eye), p2, M.PETAL); B.stamp([top[0] + 2.2, top[1] + 0.4, top[2] + 1.6], ST5.bud, rP2, p2, { mat: M.PETAL, zb: 0.6, tone: 0 }); }
          else if (seed) B.stamp([top[0], top[1] + 0.4, top[2]], PT.pod, rDry, p2, { mat: M.LEAF, tone: 0 });
        } else if (hp === 'orchid') {
          if (bloom) { B.stamp([top[0] + 1, top[1] + 0.6, top[2] - 1.2], PT.pouch, rP, p2, { mat: M.PETAL, zb: 0.8, tone: 0 }); B.fleck([top[0] + 1, top[1] + 1.1, top[2] - 1], rP2, p2, M.PETAL); for (const sg of [-1, 1]) B.fleck([top[0] + 1 + sg * 1.6, top[1] + 0.8, top[2] + 0.4], B.ramp('fol', '#6a4a3a'), p2, M.LEAF); }
        }
      }
    },

    alga(B, sp) {
      const P = Q5(sp), H = B.H, W = B.W, lay = B.lay || 0, size = B.o.size, s = B.o.season;
      const fol = s === 'winter' ? mix(sp.fol, '#2a2a1a', 0.2) : s === 'spring' ? mix(sp.fol, '#9a8a3a', 0.1) : sp.fol;
      const rF = B.ramp('bark', fol), rF2 = B.ramp('bark', mix(fol, '#1a1a0e', 0.25)), rB = B.ramp('fol', mix(fol, sp.bleach || '#b0a070', 0.5)), hold = B.ramp('bark', '#3a3020'), rHi = B.ramp('fol', mix(fol, '#d0b868', 0.32));
      B.env = { c: [0, 0, H * 0.4 * (1 - lay * 0.9)], r: [W * 0.5, W * 0.4, Math.max(3, H * 0.5 * (1 - lay * 0.9))] }; B.E = EDGE5.soft;
      for (let j = 0; j < 3; j++) B.fleck([(B.rnd() - 0.5) * 3, (B.rnd() - 0.5) * 2, 0.3], hold, 0, M.WOOD);
      const down = Math.PI / 2, nFlat = nrm(toView([0, 0.25, 1]));
      if (P.form === 'kelp') {                           // broad ruffled blades with a pale midband; lying drained, standing in water
        const nb = Math.max(1, Math.round(P.blades * (0.6 + 0.6 * size))), st = [0, 0, Math.max(1.5, H * 0.1 * (1 - lay * 0.85))];
        B.seg([0, 0, 0], st, 1.1, 0.9, B.ramp('bark', '#4a3a1c'), M.WOOD, 0);
        for (let k = 0; k < nb; k++) {
          const th = lay > 0.5 ? down + (k - (nb - 1) / 2) * 0.6 + (B.rnd() - 0.5) * 0.3 : -Math.PI / 2 + (k - (nb - 1) / 2) * 0.45, len = H * (0.65 + 0.35 * B.rnd()) * (0.5 + 0.5 * size);
          const pts = bladePts(st, th, len, lay > 0.5 ? 0.25 : 1.3, lay > 0.5 ? 0.1 : 0.8, (B.rnd() - 0.5) * 0.9, lay, 12), bw = P.bw * (0.6 + 0.4 * size), ph0 = B.rnd() * 6;
          if (lay > 0.5) for (let q = 1; q < pts.length; q++) { const f = q / (pts.length - 1), wv = Math.sin(f * 6 + ph0) * 2.4 * f; pts[q][0] += Math.cos(th + Math.PI / 2) * wv; pts[q][1] += Math.sin(th + Math.PI / 2) * wv * 0.6; }
          B.blade5(pts, bw, bw, (q) => (q % 3 === 1 ? rF2 : rF), 1 + k, { n: nFlat, wAt: (f) => bw * (0.25 + 0.75 * Math.pow(Math.sin(Math.PI * Math.min(1, f * 1.1)), 0.6)) * (1 + 0.22 * Math.sin(f * 19 + ph0)) });
          B.blade5(pts.slice(1, -1).map(p => [p[0], p[1] + 0.35, p[2] + 0.25]), 1, 1, rHi, 1 + k, { n: nFlat });
        }
      } else if (P.form === 'frond') {                   // wrack: a heap of branching straps and bladders on a dark body
        const ns = Math.max(3, Math.round(P.straps * 2.2 * (0.5 + 0.5 * size)));
        if (lay > 0.5) B.mass([0, 0.5, 0.9], [W * 0.15, W * 0.1, 1.1], rF2, 0, { tone: -1, mat: M.BLADE });
        for (let k = 0; k < ns; k++) {
          const th0 = lay > 0.5 ? down + (B.rnd() - 0.5) * 2.6 : (k - (ns - 1) / 2) * 0.5, len = H * (0.45 + 0.4 * B.rnd());
          const grow = (from, th, L, depth, part) => {
            const pts = lay > 0.5 ? bladePts(from, th, L, 0.5, 0.45, (B.rnd() - 0.5) * 0.7, lay, 4) : bladePts(from, -Math.PI / 2 + th * 0.6, L, 1.35, 0.35, (B.rnd() - 0.5) * 0.6, lay, 4);
            B.blade(pts, P.bw, P.bw * 0.8, depth % 2 ? rF2 : rF, part, { n: nFlat });
            const e = pts[pts.length - 1];
            if (P.knots) for (let q = 1; q < pts.length; q += 2) B.stamp([pts[q][0], pts[q][1] + 0.3, pts[q][2] + 0.4], ST5.bladder, rHi, part, { mat: M.BLADE, tone: 0, n: nrm(toView([0.2, 0.3, 0.9])) });
            if (P.pairs && depth > 0) for (const sg of [-1, 1]) B.fleck([pts[1][0] + sg * 1.4, pts[1][1] + 0.3, pts[1][2] + 0.5], rHi, part, M.BLADE);
            if (depth < 2) for (const sg of [-1, 1]) grow(e, th + sg * 0.42, L * 0.7, depth + 1, part);
          };
          grow([(B.rnd() - 0.5) * 3, 0, 0.5], th0, len * 0.45, 0, 1 + (k % 6));
        }
      } else if (P.form === 'sheet') {                   // sea lettuce: crinkled translucent sheets
        const nn = Math.max(4, Math.round(P.n * 1.4 * (0.6 + 0.5 * size)));
        for (let k = 0; k < nn; k++) { const a = B.rnd() * TAU, r = Math.sqrt(B.rnd()) * W * 0.4, p = [Math.cos(a) * r, Math.sin(a) * r * 0.6, 0.6 + (1 - lay) * (2 + B.rnd() * 5)]; B.stamp(p, ST5.sheet2, k % 3 ? rF : rF2, 1 + (k % 6), { mat: M.LEAF, n: nrm(toView([(B.rnd() - 0.5) * 0.5, 0.25 + (B.rnd() - 0.5) * 0.3, 1])), tone: B.rnd() < 0.3 ? 1 : 0 }); B.leaf([p[0] + 2.5, p[1] + 0.5, p[2] + 0.3], 'sheet', rF, 1 + (k % 6), { tone: 0 }); }
      } else {                                           // Irish moss: a cushion of small masses, bleached tips when drained in summer
        const nn = Math.max(4, Math.round(P.n * 0.4 * (0.5 + 0.5 * size))), bleach = (s === 'summer' || s === 'spring') && lay > 0.5;
        for (let k = 0; k < nn; k++) {
          const a = B.rnd() * TAU, r = Math.pow(B.rnd(), 0.7) * W * 0.38, hh = H * (0.35 + 0.5 * B.rnd()) * (1 - r / W) * (1 - lay * 0.4);
          const mq = B.mass([Math.cos(a) * r, Math.sin(a) * r * 0.55, Math.max(1.2, hh * 0.6)], [2.2 + B.rnd(), 1.8, Math.max(1.2, hh * 0.45)], k % 3 ? rF : rF2, k % 5, { mat: M.BLADE, E: EDGE5.tri });
          B.cover(mq, 'moss', mq.rid, 1.1, bleach ? rB : rF2);
        }
      }
    },

    succulent(B, sp) {                                   // glasswort: jointed green fingers, red by autumn
      const P = sp.p, H = B.H, s = B.o.season, size = B.o.size;
      const col = s === 'autumn' ? sp.fall : s === 'winter' ? '#6a4a3a' : s === 'spring' ? mix(sp.fol, SPRING, 0.2) : sp.fol, rF = B.ramp('fol', col), rF2 = B.ramp('fol', mix(col, '#1a2a1a', 0.22)), rT = B.ramp('fol', mix(col, s === 'autumn' ? '#e06040' : '#c8d890', 0.25));
      B.env = { c: [0, 0, H * 0.4], r: [B.W * 0.5, B.W * 0.4, H * 0.6] }; B.E = EDGE5.soft;
      B.mass([0, 0, 1], [B.W * 0.22, B.W * 0.14, 1.2], rF2, 0, { mat: M.BLADE });
      const n = Math.max(4, Math.round(P.stems * 1.6 * (0.5 + 0.5 * size)));
      for (let k = 0; k < n; k++) {
        const a = B.rnd() * TAU, r = Math.sqrt(B.rnd()) * B.W * 0.33; let p = [Math.cos(a) * r, Math.sin(a) * r * 0.6, 0];
        const joints = 3 + Math.floor(B.rnd() * 3), jl = H * (s === 'winter' ? 0.6 : 1) / joints * (1 - 0.3 * r / B.W);
        for (let j = 0; j < joints; j++) {
          const e = [p[0] + (B.rnd() - 0.5) * 1.4, p[1], p[2] + jl * (0.8 + 0.3 * B.rnd())];
          B.seg(p, e, 0.95, 0.85, j === joints - 1 ? rT : j % 2 ? rF2 : rF, M.BLADE, k % 5);
          if (j > 0 && B.rnd() < 0.5) { const sg = B.rnd() < 0.5 ? -1 : 1, b = [p[0] + sg * 2.5, p[1], p[2] + jl * 0.9]; B.seg(p, b, 0.8, 0.7, rT, M.BLADE, k % 5); }
          p = e;
        }
      }
    },

    sprawl(B, sp) {                                      // beach pea: runners with leaflet pairs, pea flowers held up, pods by autumn
      phen(B, sp);
      const P = sp.p, W = B.W, s = B.o.season, size = B.o.size;
      if (s === 'winter') { for (let k = 0; k < 5; k++) { const a = B.rnd() * TAU; B.seg([0, 0, 0.5], [Math.cos(a) * W * 0.25, Math.sin(a) * W * 0.12, 0.6], 0.5, 0.45, B.ramp('fol', DEAD), M.STEM, k); } return; }
      const rP = B.ramp('petal', sp.bloom.c), rP2 = B.ramp('petal', mix(sp.bloom.c, '#ffffff', 0.3)), rPod = B.ramp('fol', s === 'autumn' ? '#6a5a3a' : '#7a9a5a');
      B.env = { c: [0, 0, 2], r: [W * 0.5, W * 0.3, 8] }; B.E = EDGE5.round;
      const n = Math.max(3, Math.round(P.runners * (0.6 + 0.5 * size)));
      for (let k = 0; k < n; k++) {
        const a = (k / n) * TAU + B.rnd() * 0.5, L = W * (0.3 + 0.18 * B.rnd()), pts = [];
        for (let q = 0; q <= 8; q++) { const f = q / 8; pts.push([Math.cos(a + Math.sin(f * 3) * 0.3) * L * f, Math.sin(a + Math.sin(f * 3) * 0.3) * L * f * 0.6, 1 + Math.sin(f * Math.PI) * B.H * 0.45]); }
        B.chain(pts, 0.5, 0.45, B.rS, M.STEM, k);
        for (let q = 1; q <= 8; q++) {
          const p = pts[q];
          if (q % 3 === 1 && B.rnd() < B.leafK) B.cover(B.mass([p[0], p[1], p[2] + 1], [2.2, 1.8, 1.4], B.rF2, k), 'coin', B.rF, 1.0, B.rF2);
          for (const sg of [-1, 1]) if (B.rnd() < B.leafK) B.leaf([p[0] - Math.sin(a) * sg * 2, p[1] + Math.cos(a) * sg * 1.2, p[2] + 1], 'coin', q % 3 ? B.rF : B.rF2, k);
          if (s === 'summer' && q % 3 === 0 && B.rnd() < 0.85) { B.seg(p, [p[0], p[1], p[2] + 3], 0.4, 0.4, B.rS, M.STEM, k); B.stamp([p[0], p[1] + 0.5, p[2] + 3.5], ST5.plus, q % 2 ? rP : rP2, k, { mat: M.PETAL, zb: 0.8, tone: 0 }); }
          if (s === 'autumn' && q % 3 === 1) B.stamp([p[0], p[1] + 0.4, p[2] + 2], PT.pod, rPod, k, { mat: M.LEAF, tone: 0 });
        }
      }
    },
  };

  function basal(B, kind, base, h, rL, rL2, rS, part, dead) {
    const x0 = base[0], y0 = base[1];
    if (dead && kind !== 'rosette') return;
    if (kind === 'palmate') { B.mass([x0, y0, 2], [4, 3, 2], rL2, part); for (let f = 0; f < 5; f++) { const a = B.rnd() * TAU, c = [x0 + Math.cos(a) * 3.5, y0 + Math.sin(a) * 2.2, 2.5 + B.rnd() * 2.5]; B.seg(base, c, 0.4, 0.4, rS, M.STEM, part); for (let q = 0; q < 6; q++) { const b2 = a + (q - 2.5) * 0.42; B.blade([c, [c[0] + Math.cos(b2) * 3.5, c[1] + Math.sin(b2) * 2.2, c[2] + 0.5]], 1, 0.8, q % 2 ? rL : rL2, part); } } }
    else if (kind === 'sword') { for (let f = 0; f < 6; f++) { const th = -Math.PI / 2 + (f - 2.5) * 0.25, pts = bladePts(base, th + Math.PI / 2 * (f < 3 ? -1 : 1) * 0.08, h * (0.6 + 0.3 * B.rnd()), 1.45 - Math.abs(f - 2.5) * 0.08, 0.22, 0, 0); B.blade(pts, 2.2, 1, f % 2 ? rL : rL2, part); } }
    else if (kind === 'pair') { for (const sg of [-1, 1]) B.stamp([x0 + sg * 3.2, y0 + 1, 0.8], ST5.slipper, rL, part, { n: nrm(toView([sg * 0.2, 0.3, 1])) }); }
    else if (kind) { B.mass([x0, y0, 1.5], [3.4, 2.6, 1.6], rL2, part); for (let f = 0; f < 7; f++) { const a = f / 7 * TAU + B.rnd(); B.leaf([x0 + Math.cos(a) * 3.2, y0 + Math.sin(a) * 2, 1.4 + B.rnd() * 1.4], kind === 'feather' ? 'fern' : kind === 'lobed' ? 'coin' : 'lance', f % 3 ? rL : rL2, part); } }
  }

  // ---- the model: build, bound, pivot, opacity grid, sky visibility --------------------------------------------------------
  const MODELS = new Map();
  function model(key, o) {
    o = o || {};
    const sp = byKey[key] || SPECIES[0], variant = (((o.variant | 0) % VARIANTS) + VARIANTS) % VARIANTS, season = SEASONS.includes(o.season) ? o.season : 'summer', stage = STAGES[o.stage] ? o.stage : 'mature';
    const water = sp.fam === 'shore' && o.water != null ? Math.round(o.water * 20) / 20 : null;
    const mk = [sp.key, variant, season, stage, water].join('|');
    if (MODELS.has(mk)) return MODELS.get(mk);
    const t0 = Date.now(), size = STAGES[stage], seed = (strH(sp.key) + variant * 7919 + (season === 'winter' ? 31 : 0)) | 0;
    const B = new Builder(sp, { variant, season, stage, size, seed, water });
    const limp = sp.limp || 0, Hm = B.H / PPU;
    B.lay = !limp ? 0 : water == null ? limp : water <= 0 ? limp : Math.max(limp * 0.62, limp * clamp(1 - water / Math.max(0.2, Hm), 0, 1));   // (9) submerged weed streams with the current, it does not stand up out of the sea
    HABITS[sp.habit](B, sp);
    const prims = B.prims, wv = sp.wind || WSHRUB;
    // rest bounds in screen space around the root
    const S = (p) => [p[0], p[1] * SE - p[2] * CE, p[1] * CE + p[2] * SE];
    let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9, zmin = 1e9, zmax = -1e9, hmax = 1;
    const ext = (p, r) => { const s = S(p); x0 = Math.min(x0, s[0] - r); x1 = Math.max(x1, s[0] + r); y0 = Math.min(y0, s[1] - r); y1 = Math.max(y1, s[1] + r); zmin = Math.min(zmin, s[2]); zmax = Math.max(zmax, s[2]); hmax = Math.max(hmax, p[2]); };
    for (const q of prims) { if (q.k === 0) ext(q.p, q.st.r + 1); else if (q.k === 3) ext(q.p, 1); else if (q.k === 4) { ext(q.p, Math.max(q.rs[0], q.rs[1]) + 2); ext([q.p[0], q.p[1], q.p[2] + q.r[2]], 1); } else { ext(q.a, (q.r0 || q.w0 || 1) + 1); ext(q.b, (q.r1 || q.w1 || 1) + 1); } }
    const Hs = Math.max(4, -y0), reach = Math.ceil(wv[0] * hmax * 1.9 + wv[1] * hmax / 60 * 2.6 + (limp ? 5 : 0)) + 3;
    const padX = reach + 2, padT = 3, padB = 3;
    const w = Math.ceil(x1 - x0) + padX * 2, h = Math.ceil(y1 - y0) + padT + padB, pivot = { x: Math.round(-x0 + padX), y: Math.round(-y0 + padT) };
    const mdl = { key: sp.key, sp, variant, season, stage, size, water, lay: B.lay, prims, ramps: B.ramps, cell: { w, h }, pivot, zmin, zmax, hmax, Hs, reach, wv, stampN: B.nS, parts: B.part, footR: Math.max(2, B.W * 0.12), R: (x1 - x0) / 2, cx: (x0 + x1) / 2 };
    mdl.tideWet = limp ? (water == null ? 0.25 : water <= 0 ? clamp(1 - (-water) / 0.85, 0.15, 1) : 1) : 0;
    buildGrid(mdl);
    mdl.ms = Date.now() - t0;
    MODELS.set(mk, mdl); if (MODELS.size > 260) MODELS.delete(MODELS.keys().next().value);
    return mdl;
  }
  /* the opacity grid, 2 px voxels over the rest pose: leaves σ per px, wood near opaque */
  function buildGrid(mdl) {
    const { w, h } = mdl.cell, P = mdl.pivot, z0 = mdl.zmin - 6, gz = Math.ceil((mdl.zmax - z0 + 8) / 2) + 1, gx = Math.ceil(w / 2) + 1, gy = Math.ceil(h / 2) + 1;
    const sigM = 0.09, d = new Float32Array(gx * gy * gz), sig = mdl.sp.fam === 'grass' || mdl.sp.habit === 'grass' ? 0.07 : 0.11;
    /* a stem's normal is the world's: its front faces the camera across the stem's own axis, its sides
       turn with it — so a vertical stem's front is not "up" and takes no snow, a level twig's top does */
    const VW = [0, CE, SE];
    for (const q of mdl.prims) if (q.k === 1) {
      const dd = nrm([q.b[0] - q.a[0], q.b[1] - q.a[1], q.b[2] - q.a[2]]), vd = VW[0] * dd[0] + VW[1] * dd[1] + VW[2] * dd[2];
      let c = [VW[0] - vd * dd[0], VW[1] - vd * dd[1], VW[2] - vd * dd[2]]; if (Math.hypot(c[0], c[1], c[2]) < 1e-3) c = [0, 1, 0]; c = nrm(c);
      const s = nrm([dd[1] * c[2] - dd[2] * c[1], dd[2] * c[0] - dd[0] * c[2], dd[0] * c[1] - dd[1] * c[0]]);
      q.nf = toView(c); q.ns = toView(s);
      const sx = q.b[0] - q.a[0], sy = (q.b[1] - q.a[1]) * SE - (q.b[2] - q.a[2]) * CE, sl = Math.hypot(sx, sy) || 1;
      q.pS = [-sy / sl, sx / sl]; const dot = q.ns[0] * q.pS[0] + q.ns[1] * q.pS[1]; q.sg = dot < 0 ? -1 : 1;
    }
    const add = (x, y, z, v) => { const X = Math.floor(x / 2), Y = Math.floor(y / 2), Z = Math.floor((z - z0) / 2); if (X < 0 || Y < 0 || Z < 0 || X >= gx || Y >= gy || Z >= gz) return; d[(Z * gy + Y) * gx + X] += v; };
    const S = (p) => [P.x + p[0], P.y + p[1] * SE - p[2] * CE, p[1] * CE + p[2] * SE];
    const cen = new Float32Array(mdl.prims.length * 3);
    mdl.prims.forEach((q, i) => {
      let c;
      if (q.k === 0) { c = S(q.p); if (!q.ng) for (const [dx, dy] of q.st.px) add(c[0] + dx, c[1] + dy, c[2], sig); else c = [c[0] + q.n[0] * 1.8, c[1] + q.n[1] * 1.8, c[2] + q.n[2] * 1.8]; }
      else if (q.k === 3) { c = S(q.p); add(c[0], c[1], c[2], sig); }
      else if (q.k === 4) { c = S(q.p); const a = q.rs[0], b = q.rs[1], e = q.rs[2]; for (let dz = -e; dz <= e; dz += 1.5) for (let dy = -b; dy <= b; dy += 1.5) for (let dx = -a; dx <= a; dx += 1.5) if (dx * dx / (a * a) + dy * dy / (b * b) + dz * dz / (e * e) <= 1) add(c[0] + dx, c[1] + dy, c[2] + dz, sigM); c = [c[0], c[1] - b * 0.3, c[2] + e * 0.7]; }
      else {
        const a = S(q.a), b = S(q.b), L = Math.max(1, Math.hypot(b[0] - a[0], b[1] - a[1])), r = q.k === 1 ? (q.r0 + q.r1) / 2 : (q.w0 + q.w1) / 4, v = q.k === 1 ? (q.mat === M.WOOD ? 0.35 : 0.15) : sig;
        for (let s = 0; s <= L; s++) { const f = s / L; add(a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f, v * Math.max(1, r)); }
        c = [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2];
      }
      cen[i * 3] = c[0]; cen[i * 3 + 1] = c[1]; cen[i * 3 + 2] = c[2];
    });
    mdl.grid = { d, gx, gy, gz, z0 }; mdl.cen = cen;
    const dirs = [UPV, nrm([UPV[0] + 0.55, UPV[1], UPV[2]]), nrm([UPV[0] - 0.55, UPV[1], UPV[2]]), nrm([UPV[0], UPV[1], UPV[2] + 0.5]), nrm([UPV[0], UPV[1] + 0.3, UPV[2] - 0.5])];
    const ao = new Float32Array(mdl.prims.length);
    for (let i = 0; i < mdl.prims.length; i++) { let s = 0; for (const dv of dirs) s += march(mdl.grid, cen[i * 3], cen[i * 3 + 1], cen[i * 3 + 2], dv, 12, 2); ao[i] = s / dirs.length; }
    mdl.ao = ao;
  }
  function march(G, x, y, z, dv, n, st) {
    let acc = 0;
    for (let k = 2; k <= n; k++) {
      const X = Math.floor((x + dv[0] * k * st) / 2), Y = Math.floor((y + dv[1] * k * st) / 2), Z = Math.floor((z + dv[2] * k * st - G.z0) / 2);
      if (X < 0 || Y < 0 || Z < 0 || X >= G.gx || Y >= G.gy || Z >= G.gz) continue;
      acc += G.d[(Z * G.gy + Y) * G.gx + X] * st;
    }
    return Math.exp(-acc);
  }
  function sunT(mdl, Lv) {
    const k = Lv.map(q => Math.round(q * 300)).join(',');
    if (mdl._tk === k) return mdl._t;
    const T = new Float32Array(mdl.prims.length);
    for (let i = 0; i < T.length; i++) T[i] = march(mdl.grid, mdl.cen[i * 3], mdl.cen[i * 3 + 1], mdl.cen[i * 3 + 2], Lv, 36, 1.5);
    mdl._tk = k; mdl._t = T; return T;
  }

  // ---- wind -----------------------------------------------------------------------------------------------------------------------
  function windAt(mdl, W, p, part, ph, id, flut) {
    const w = W.w || 0, dir = W.dir < 0 ? -1 : 1, hf = clamp(p[2] / Math.max(4, mdl.hmax), 0, 1);
    if (mdl.water != null && mdl.water > 0 && p[2] / PPU < mdl.water) {      // submerged: the surge, not the wind
      const s = (mdl.sp.limp || 0.3) * 2.6 * hf * Math.sin(TAU * ph + p[0] * 0.05 + part * 0.7);
      return [s, -Math.abs(s) * 0.2];
    }
    if (mdl.lay > 0.5) return [0, 0];                                         // drained weed lies still
    if (!(w > 0)) return [0, 0];
    const wv = mdl.wv, env = 1 + (W.gust || 0) * 0.9 * Math.sin(TAU * ph + 0.7), bend = wv[0] * mdl.hmax, hk = Math.pow(hf, 1.6);
    let dx = (w * w * bend * (0.8 + 0.2 * env) + w * bend * 0.42 * Math.sin(TAU * ph + 0.3) * (0.55 + 0.45 * env)) * hk;
    const R = Math.max(6, mdl.R), r = Math.min(1.3, Math.hypot(p[0], p[1]) / R + hf * 0.5), pm = -dir * p[0] / R * 0.9 + (((part * 0.618034) % 1) - 0.5) * 0.5, la = wv[1] * mdl.hmax / 60 * w * (0.6 + 0.4 * env);
    dx += la * Math.pow(r, 1.2) * (Math.sin(2 * TAU * ph + pm) + 0.5);
    let dy = -la * wv[3] * 0.5 * r * Math.sin(2 * TAU * ph + pm + 1.1);
    if (flut && id >= 0 && wv[2] > 0) {                                        // a leaf's own flutter: one px downwind on its own slots
      const rate = 1 + Math.floor(hsh(id, 5) * 3), slot = Math.floor(ph * LOOP * rate / 4 + hsh(id, 7) * 4) % 4;
      if (slot === 0 && hsh(id, 9) < wv[2] * (0.3 + w)) dx += 1;
    }
    return [dx * dir, dy];
  }

  // ---- a frame: rasterise every primitive at its wind pose ----------------------------------------------------------------------------
  const FRAMES = new Map();
  function frame(key, o) {
    o = o || {};
    const mdl = model(key, o), Wn = o.wind || { w: 0, gust: 0, dir: 1 }, fi = (((o.frame | 0) % LOOP) + LOOP) % LOOP;
    const fk = [key, mdl.variant, mdl.season, mdl.stage, mdl.water, Math.round((Wn.w || 0) * 100), Math.round((Wn.gust || 0) * 100), Wn.dir < 0 ? -1 : 1, fi].join('|');
    if (FRAMES.has(fk)) return FRAMES.get(fk);
    const { w, h } = mdl.cell, N = w * h, P = mdl.pivot, ph = fi / LOOP;
    const v = { w, h, a: new Uint8Array(N), mat: new Uint8Array(N), rid: new Uint8Array(N), tone: new Int8Array(N), nx: new Float32Array(N), ny: new Float32Array(N), nz: new Float32Array(N),
      z: new Float32Array(N).fill(-1e9), hg: new Float32Array(N), st: new Int32Array(N).fill(-1), part: new Int16Array(N), pid: new Int32Array(N) };
    const S = (p, part, id, flut) => { const d = windAt(mdl, Wn, p, part, ph, id, flut); return [P.x + p[0] + d[0], P.y + p[1] * SE - p[2] * CE + d[1], p[1] * CE + p[2] * SE]; };
    const put = (X, Y, z, q, i0, nx, ny, nz, hg) => {
      if (X < 0 || Y < 0 || X >= w || Y >= h) return; const i = Y * w + X; if (z <= v.z[i]) return;
      v.z[i] = z; v.a[i] = 1; v.mat[i] = q.mat; v.rid[i] = q.rid; v.tone[i] = q.tone || 0; v.nx[i] = nx; v.ny[i] = ny; v.nz[i] = nz; v.hg[i] = hg; v.st[i] = q.id == null ? -1 : q.id; v.part[i] = q.part; v.pid[i] = i0;
    };
    const line = (a, b, ra, rb, q, i0, nFlat, ha, hb) => {
      const dx = b[0] - a[0], dy = b[1] - a[1], L2 = dx * dx + dy * dy || 1e-6, R = Math.max(ra, rb);
      if (R < 0.8) {                                                          // a 1 px line, stepped so it never breaks on a diagonal
        const n = Math.max(1, Math.ceil(Math.max(Math.abs(dx), Math.abs(dy)) * 1.5));
        for (let s = 0; s <= n; s++) { const f = s / n, X = Math.floor(a[0] + dx * f), Y = Math.floor(a[1] + dy * f); const nn = nFlat || q.nf || [0, -0.3, 0.95]; put(X, Y, a[2] + (b[2] - a[2]) * f + 0.3, q, i0, nn[0], nn[1], nn[2], ha + (hb - ha) * f); }
        return;
      }
      const ax = Math.floor(Math.min(a[0], b[0]) - R), bx = Math.ceil(Math.max(a[0], b[0]) + R), ay = Math.floor(Math.min(a[1], b[1]) - R), by = Math.ceil(Math.max(a[1], b[1]) + R);
      for (let y = ay; y <= by; y++) for (let x = ax; x <= bx; x++) {
        const t = clamp(((x + 0.5 - a[0]) * dx + (y + 0.5 - a[1]) * dy) / L2, 0, 1), px = a[0] + dx * t, py = a[1] + dy * t, r = ra + (rb - ra) * t;
        const ox = x + 0.5 - px, oy = y + 0.5 - py, d2 = ox * ox + oy * oy; if (d2 > r * r) continue;
        const k = Math.sqrt(r * r - d2), z = a[2] + (b[2] - a[2]) * t + (nFlat ? 0.2 : k);
        if (nFlat) put(x, y, z, q, i0, nFlat[0], nFlat[1], nFlat[2], ha + (hb - ha) * t);
        else if (q.nf) { const u = clamp((ox * q.pS[0] + oy * q.pS[1]) / r, -1, 1), cu = Math.sqrt(Math.max(0, 1 - u * u)), su = u * q.sg; let nx = q.nf[0] * cu + q.ns[0] * su, ny = q.nf[1] * cu + q.ns[1] * su, nz = q.nf[2] * cu + q.ns[2] * su; const Ln = Math.hypot(nx, ny, nz) || 1; put(x, y, z, q, i0, nx / Ln, ny / Ln, nz / Ln, ha + (hb - ha) * t); }
        else { let nx = ox / r, ny = oy / r * 0.35, nz = k / r; const Ln = Math.hypot(nx, ny, nz) || 1; put(x, y, z, q, i0, nx / Ln, ny / Ln, nz / Ln, ha + (hb - ha) * t); }
      }
    };
    const blobM = (q, i0) => {
      const c = S(q.p, q.part, -1, 0), rx = q.rs[0], ry = q.rs[1], rz = q.rs[2], E = q.E || EDGE5.round, sd = q.sd || 0, ak = Math.min(1, rx / 3.2);
      const pad = E.amp + 1.5, X0 = Math.floor(c[0] - rx - pad), X1 = Math.ceil(c[0] + rx + pad), Y0 = Math.floor(c[1] - ry - pad), Y1 = Math.ceil(c[1] + ry + pad);
      const nT = Math.max(4, Math.round(TAU * Math.sqrt((rx * rx + ry * ry) * 0.5) / E.pitch));
      for (let y = Y0; y <= Y1; y++) for (let x = X0; x <= X1; x++) {
        let u = (x + 0.5 - c[0]) / rx, wv = (y + 0.5 - c[1]) / ry;
        const th = Math.atan2(wv, u), st = Math.sin(th), rE = Math.hypot(Math.cos(th) * rx, st * ry) || 1, f = (((th / TAU + 1) * nT + sd) % 1 + 1) % 1;
        const tw = E.shape === 'tri' ? Math.abs(f * 2 - 1) - 0.5 : E.shape === 'spike' ? Math.pow(1 - Math.abs(f * 2 - 1), 2.6) - 0.35 : 0.5 * Math.cos(TAU * f);
        const kk = 1 + 0.11 * Math.sin(3 * th + sd * 1.7) + 0.06 * Math.sin(5 * th + sd * 3.1) + tw * E.amp * ak * (E.base + E.under * (0.5 + 0.5 * st)) / rE;
        u /= kk; wv /= kk; const s2 = u * u + wv * wv; if (s2 > 1) continue;
        const t = Math.sqrt(1 - s2); let nx = u / rx, ny = wv / ry, nz = t / rz; const Ln = Math.hypot(nx, ny, nz) || 1;
        put(x, y, c[2] + t * rz + (q.zb || 0), q, i0, nx / Ln, ny / Ln, nz / Ln, Math.max(0, q.p[2] - (y + 0.5 - c[1]) / CE));
      }
    };
    mdl.prims.forEach((q, i0) => {
      if (q.k === 4) { blobM(q, i0); return; }
      if (q.k === 0) { const c = S(q.p, q.part, q.id, q.fl), cx = Math.round(c[0]), cy = Math.round(c[1]), rr = q.st.r * q.st.r + 1; for (const [dx, dy] of q.st.px) put(cx + dx, cy + dy, c[2] + q.zb + 0.4 * (1 - (dx * dx + dy * dy) / rr), q, i0, q.n[0], q.n[1], q.n[2], q.p[2]); }
      else if (q.k === 3) { const c = S(q.p, q.part, q.id, 1); put(Math.round(c[0]), Math.round(c[1]), c[2] + 1, q, i0, q.n[0], q.n[1], q.n[2], q.p[2]); }
      else if (q.k === 1) line(S(q.a, q.part, -1, 0), S(q.b, q.part, -1, 0), q.r0, q.r1, q, i0, null, q.a[2], q.b[2]);
      else line(S(q.a, q.part, q.id, 0), S(q.b, q.part, q.id, 0), q.w0 / 2, q.w1 / 2, q, i0, q.n, q.a[2], q.b[2]);
    });
    const fr = { mdl, v, frame: fi, wind: Wn };
    FRAMES.set(fk, fr); if (FRAMES.size > 900) FRAMES.delete(FRAMES.keys().next().value);
    return fr;
  }

  // ---- relight: TreeRig4.relight's law on the plant G-buffer ------------------------------------------------------------------------------------
  const FSTEPS = [0.06, 0.16, 0.33, 0.74], STEPS = [0.05, 0.145, 0.31, 0.56];
  const bandF = (l) => { for (let i = 0; i < 4; i++) if (l < FSTEPS[i]) return i; return 4; };
  const bandW = (l) => { for (let i = 0; i < 4; i++) if (l < STEPS[i]) return i; return 4; };
  const REF_SUN = dirOf(290, 55);
  const REF_SKY = { name: 'Afternoon · reference key', sunW: REF_SUN, sunV: toView(REF_SUN), sunI: 1, skyI: 0.60, expo: 1, kc: '#fff0cf', ac: '#1d3b4a', ka: 0.16, aa: 0.30, rc: '#bcd6e2', ra: 0.14, wash: '#fff4dd', wa: 0.03, amb: 0.62, fogC: '#c3cdce', fog: 0, wet: 0, snow: 0, skyC: '#b0c9d8' };
  const UNLIT_SKY = { name: 'unlit', sunW: [0, 0, 1], sunV: UPV, sunI: 0, skyI: 1.25, expo: 1, kc: '#ffffff', ac: '#000000', ka: 0, aa: 0, wash: '#ffffff', wa: 0, amb: 1, fogC: '#ffffff', fog: 0, wet: 0, snow: 0, grade: false };
  const N4 = [[1, 0], [-1, 0], [0, 1], [0, -1]];
  function relight(fr, sky, o) {
    o = o || {}; sky = sky || REF_SKY;
    const mdl = fr.mdl, sp = mdl.sp, v = fr.v, w = v.w, h = v.h, N = w * h, RAMP = mdl.ramps;
    const Lv = sky.sunV || REF_SKY.sunV, sunI = sky.sunI == null ? 1 : sky.sunI, skyI = sky.skyI == null ? 0.6 : sky.skyI, expo = sky.expo || 1;
    const grade = sky.grade !== false, snow = sky.snow || 0, wet = Math.max(sky.wet || 0, mdl.tideWet || 0), fog = sky.fog || 0;
    const T = sunI > 0.01 ? sunT(mdl, Lv) : new Float32Array(mdl.prims.length), AO = mdl.ao;
    let lx = Lv[0], ly = Lv[1]; const ll = Math.hypot(lx, ly);
    if (ll < 0.18) { lx = -0.6; ly = -0.8; } else { lx /= ll; ly /= ll; }
    const ax = lx > 0.38 ? -1 : lx < -0.38 ? 1 : 0, ay = ly > 0.38 ? -1 : ly < -0.38 ? 1 : 0;
    const tipV = new Float32Array(mdl.stampN + 1).fill(-1e9), tipI = new Int32Array(mdl.stampN + 1).fill(-1);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const i = y * w + x; if (!v.a[i]) continue; const s = v.st[i]; if (s < 0) continue; const val = x * lx + y * ly; if (val > tipV[s]) { tipV[s] = val; tipI[s] = i; } }
    const zr = Math.max(1, mdl.zmax - mdl.zmin), kS = 0.36, kD = 1.0, kT = 0.62 * sp.trans;
    const sunW = sky.sunW || REF_SKY.sunW, gB = 0.13 * (sunI * Math.max(0, sunW[2]) + 0.35 * skyI) * (1 + 0.8 * snow);
    const bury = snow > 0.25 ? Math.pow(snow, 1.6) * 0.30 * PPU : 0, water = mdl.water, backlit = Lv[2] < -0.12 && sunI > 0.05;
    const kc = h2r(sky.kc || '#ffffff'), ac = h2r(sky.ac || '#000000'), wc = h2r(sky.wash || '#ffffff'), fc = h2r(sky.fogC || '#c3cdce');
    const wat = h2r(mix(WATER, sky.skyC || '#b0c9d8', 0.18)), SUBK = [0, 0.35, 0.55, 0.72], cache = new Map();
    const colour = (rid, bi, lit, fq, spc, wk, sub) => {
      const key = (((((rid * 5 + bi) * 2 + lit) * 5 + fq) * 3 + spc) * 3 + wk) * 4 + sub;
      let c = cache.get(key); if (c) return c;
      const R = RAMP[rid], r = (spc === 1 ? R[4].map((q, k) => q + (kc[k] - q) * 0.55) : spc === 2 ? R[4].map(q => q + (255 - q) * 0.6) : R[bi].slice());
      if (sub) for (let k = 0; k < 3; k++) r[k] += (wat[k] - r[k]) * SUBK[sub];
      if (grade) {
        const u = bi / 4, ta = (sky.aa || 0) * (1 - u) * (1 - (sky.amb || 0) * 0.35), tk = (sky.ka || 0) * u * (lit ? 1 : 0.3), wd = wk === 2 ? 0.30 : wk === 1 ? 0.12 : 0;
        for (let k = 0; k < 3; k++) { r[k] += (ac[k] - r[k]) * ta; r[k] += (kc[k] - r[k]) * tk; r[k] *= 1 - wd; if (sky.wa) r[k] += (wc[k] - r[k]) * sky.wa; if (fq) r[k] += (fc[k] - r[k]) * fq * 0.17; }
      }
      c = [clamp(Math.round(r[0]), 0, 255), clamp(Math.round(r[1]), 0, 255), clamp(Math.round(r[2]), 0, 255)]; cache.set(key, c); return c;
    };
    const stAt = (x, y) => { if (x < 0 || y < 0 || x >= w || y >= h) return -2; const j = y * w + x; return v.a[j] ? v.st[j] : -2; };
    const out = new Uint8ClampedArray(N * 4), fi = fr.frame;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i]) continue;
      if (bury && v.hg[i] < bury) continue;                                  // under the snow: the ground's own snow shows
      const pid = v.pid[i], mat = v.mat[i], s = v.st[i], soft = mat !== M.WOOD && mat !== M.STEM, nx = v.nx[i], ny = v.ny[i], nz = v.nz[i];
      let sub = 0, deep = 0; if (water != null && water > 0) { const dd = water - v.hg[i] / PPU; if (dd > 1.7) continue; if (dd > 0) sub = dd < 0.3 ? 1 : dd < 0.8 ? 2 : 3; if (dd > 0.9) deep = 1; }
      const t = T[pid], ao = AO[pid], zn = clamp((v.z[i] - mdl.zmin) / zr, 0, 1);
      const nl = nx * Lv[0] + ny * Lv[1] + nz * Lv[2], up = nx * UPV[0] + ny * UPV[1] + nz * UPV[2];
      const sun = kD * sunI * (nl > 0 ? Math.pow(nl, 1.25) : 0) * t * (sub ? 0.45 : 1);
      const L = ((0.74 + 0.26 * zn) * (kS * skyI * (0.30 + 0.70 * ao) * (0.45 + 0.55 * clamp(up, 0, 1)) + sun + (soft && mat !== M.FRUIT ? kT * sunI * (nl < 0 ? Math.pow(-nl, 0.7) : 0) * t : 0)) + gB * (0.5 - 0.5 * up) * (0.5 + 0.5 * ao)) * expo;
      const lit = sun > 0.22 ? 1 : 0;
      let rid = v.rid[i], bi, spc = 0, wk = 0;
      if (soft) {
        bi = bandF(L + (hsh(s < 0 ? pid : s, 3) - 0.5) * 0.05) + v.tone[i];
        bi = clamp(bi, 0, 4);
        const b0 = bi, sA = ax ? stAt(x + ax, y) : -2, sB = ay ? stAt(x, y + ay) : -2, dA = sA !== -2 && sA !== s, dB = sB !== -2 && sB !== s;
        if (s >= 0 && b0 >= 2 && (dA || dB)) bi -= 1;
        else if (s >= 0 && b0 >= 3 && tipI[s] === i) { bi += 1; if (wet > 0.25 && lit && !sub && hsh(s, 17) < wet * 0.6) spc = 2; }
        if (wet > 0.15 && !sub) wk = 1;
      } else {
        bi = bandW(L); if (wet > 0.4 && !sub) bi -= 1; wk = sub ? 0 : wet > 0.5 ? 2 : wet > 0.15 ? 1 : 0;
      }
      let crev = 0;
      for (const [dx, dy] of N4) { const jx = x + dx, jy = y + dy; if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue; const j = jy * w + jx; if (v.a[j] && v.pid[j] !== pid && v.st[j] !== s && v.z[j] > v.z[i] + 1.2) { crev = 1; break; } }
      bi -= crev;
      if (snow > 0.02 && !sub) {
        const hs = hsh(s >= 0 ? s : pid, 91), thr = 1.02 - snow * 0.95 + (hs - 0.5) * 0.34 + (mat === M.BLADE || mat === M.STEM ? 0.22 : mat === M.PETAL || mat === M.FRUIT ? 0.3 : 0);
        if (up > thr && ao > 0.2) { rid = 0; bi = clamp(bi + 1, 1, 4); wk = 0; spc = 0; }
      }
      if (backlit && soft && t > 0.5 && rid !== 0 && !sub) {                 // the rim only where there is body behind it (the trees' rule 3)
        const jx = x + Math.round(lx * 1.4), jy = y + Math.round(ly * 1.4), kx = x - Math.round(lx * 2.2), ky = y - Math.round(ly * 2.2);
        const open = jx < 0 || jy < 0 || jx >= w || jy >= h || !v.a[jy * w + jx], thick = kx >= 0 && ky >= 0 && kx < w && ky < h && v.a[ky * w + kx] && v.mat[ky * w + kx] !== M.WOOD;
        if (open && thick && mat !== M.BLADE) { spc = 1; bi = 4; } else if (open && mat === M.BLADE && s >= 0 && tipI[s] === i) bi = Math.min(4, bi + 1);
      }
      if (sub >= 2) { bi = Math.min(bi, deep ? 1 : 2); spc = 0; }
      const hf = clamp(v.hg[i] / Math.max(8, mdl.hmax), 0, 1);
      const fq = fog > 0.01 ? Math.round(clamp(fog * (0.62 + 0.38 * (1 - hf)), 0, 1) * 4) : 0;
      const c = colour(rid, clamp(bi, 0, 4), lit, fq, spc, wk, sub);
      out[i * 4] = c[0]; out[i * 4 + 1] = c[1]; out[i * 4 + 2] = c[2]; out[i * 4 + 3] = 255;
    }
    return out;
  }

  // ---- cast shadow on the ground ------------------------------------------------------------------------------------------------------------------
  function castShadow(fr, sky) {
    sky = sky || REF_SKY;
    const mdl = fr.mdl, piv = mdl.pivot, G = mdl.grid, sunI = sky.sunI == null ? 1 : sky.sunI, Lw = sky.sunW || REF_SKY.sunW, Lv = sky.sunV || REF_SKY.sunV;
    if (mdl.water != null && mdl.water > mdl.hmax / PPU) return { x0: 0, y0: 0, w: 0, h: 0, lv: new Uint8Array(0) };
    const sunOK = sunI > 0.04 && Lw[2] > 0.03, R = mdl.R + 3, Hh = mdl.hmax;
    let xa = piv.x - R, xb = piv.x + R, ya = piv.y - R * SE - 3, yb = piv.y + R * SE + 4;
    if (sunOK) { const k = 1 / Math.max(Lw[2], 0.14), ex = -Lw[0] * k * Hh, ey = -Lw[1] * k * Hh * SE; xa = Math.min(xa, piv.x + ex - R); xb = Math.max(xb, piv.x + ex + R); ya = Math.min(ya, piv.y + ey - R * SE - 3); yb = Math.max(yb, piv.y + ey + R * SE + 4); }
    const X0 = Math.floor(xa), Y0 = Math.floor(ya), W = Math.ceil(xb) - X0 + 1, H = Math.ceil(yb) - Y0 + 1, lv = new Uint8Array(W * H), strong = sunI > 0.25;
    const n = Math.ceil((Hh * 2.2 + G.gz * 2) / 1.5) + 4;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const X = X0 + x + 0.5, Y = Y0 + y + 0.5, Z = (Y - piv.y) / SE * CE;
      let L = 0;
      if (sunOK) { const T = march(G, X, Y, Z, Lv, n, 1.5); if (T < 0.36) L = strong ? 3 : 2; else if (T < 0.68) L = 2; }
      if (L < 1 && march(G, X, Y, Z, UPV, 30, 2) < 0.45) L = 1;
      const dx = (X - piv.x - 0.5) / (mdl.footR * 1.1 + 1), dy = (Y - piv.y - 0.5) / (mdl.footR * 0.45 + 1);
      if (dx * dx + dy * dy < 1 && L < 2 && mdl.sp.habit !== 'alga') L = 2;
      lv[y * W + x] = L;
    }
    return { x0: X0, y0: Y0, w: W, h: H, lv };
  }

  // ---- channels, sheets --------------------------------------------------------------------------------------------------------------------------------
  const MATB = [255, 42, 128, 212, 170, 85];            // _detail B: leaf · wood · blade · petal · fruit · stem
  function view(fr, ch, sky) {
    if (!ch || ch === 'lit') return relight(fr, sky || REF_SKY);
    if (ch === 'unlit') return relight(fr, UNLIT_SKY);
    const v = fr.v, mdl = fr.mdl, N = v.w * v.h, out = new Uint8ClampedArray(N * 4), T = ch === 'sun' ? sunT(mdl, (sky || REF_SKY).sunV) : null;
    for (let i = 0; i < N; i++) {
      if (!v.a[i]) continue; let r = 128, g = 128, b = 128; const p = v.pid[i];
      if (ch === 'normal') { r = (v.nx[i] * 0.5 + 0.5) * 255; g = (-v.ny[i] * 0.5 + 0.5) * 255; b = (v.nz[i] * 0.5 + 0.5) * 255; }
      else if (ch === 'ao') r = g = b = mdl.ao[p] * 255;
      else if (ch === 'sun') { r = 24 + T[p] * 231; g = 22 + T[p] * 200; b = 30 + T[p] * 120; }
      else if (ch === 'height') r = g = b = clamp(v.hg[i] / Math.max(4, mdl.hmax), 0, 1) * 255;
      else if (ch === 'stamps') { const s = v.st[i]; if (s < 0) { r = 150; g = 118; b = 84; } else { r = 60 + 190 * hsh(s, 31); g = 60 + 190 * hsh(s, 32); b = 60 + 190 * hsh(s, 33); } }
      else if (ch === 'parts') { const q = v.part[i]; r = 50 + 200 * hsh(q, 3); g = 50 + 200 * hsh(q, 6); b = 50 + 200 * hsh(q, 9); if (v.mat[i] === M.WOOD) { r = 214; g = 168; b = 104; } }
      else if (ch === 'water') { const dd = mdl.water == null ? -1 : mdl.water - v.hg[i] / PPU; r = dd > 0 ? 30 : 200; g = dd > 0 ? 90 + 60 * clamp(1 - dd, 0, 1) : 190; b = dd > 0 ? 160 : 170; }
      else if (ch === 'light') { const soft = v.mat[i] !== M.WOOD && v.mat[i] !== M.STEM; r = mdl.ao[p] * 255; g = soft ? clamp(mdl.sp.trans, 0, 1) * 255 : 0; b = clamp(v.hg[i] / Math.max(4, mdl.hmax), 0, 1) * 255; }
      else if (ch === 'detail') { const s = v.st[i] + 1; r = s & 255; g = (s >> 8) & 255; b = MATB[v.mat[i]]; }
      out[i * 4] = r; out[i * 4 + 1] = g; out[i * 4 + 2] = b; out[i * 4 + 3] = 255;
    }
    return out;
  }
  function sheet(key, o, ch, sky) {
    const f0 = frame(key, Object.assign({}, o, { frame: 0 })), cw = f0.v.w, chh = f0.v.h, W = cw * 4, H = chh * 4, out = new Uint8ClampedArray(W * H * 4);
    for (let f = 0; f < LOOP; f++) { const px = view(frame(key, Object.assign({}, o, { frame: f })), ch || 'lit', sky), ox = (f % 4) * cw, oy = Math.floor(f / 4) * chh; for (let y = 0; y < chh; y++) out.set(px.subarray(y * cw * 4, (y + 1) * cw * 4), ((oy + y) * W + ox) * 4); }
    return { w: W, h: H, rgba: out, cell: [cw, chh], pivot: [f0.mdl.pivot.x, f0.mdl.pivot.y], frames: LOOP };
  }
  function cellOf(key, o) { const m = model(key, o); return { w: m.cell.w, h: m.cell.h, pivot: m.pivot, stamps: m.stampN, prims: m.prims.length, parts: m.parts, ms: m.ms, metres: Math.round(m.hmax / PPU * 100) / 100 }; }
  function clearCache() { MODELS.clear(); FRAMES.clear(); }

  root.PlantRig5 = { EDGE5, P5, ST5, PPU, LOOP, VARIANTS, SEASONS, STAGES, STAGE_KEYS, M, SPECIES, byKey, FAMILIES, HABITATS, REF_SKY, UNLIT_SKY, ELEV, CE, SE, UPV, toView,
    model, frame, relight, castShadow, view, sheet, cellOf, clearCache, windAt, folRamp, barkRamp, petalRamp, fruitRamp };
})(typeof globalThis !== 'undefined' ? globalThis : window);
