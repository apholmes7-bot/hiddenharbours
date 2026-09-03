/* Hidden Harbours — TREE RIG, PASS 3 (species architecture · authored leaf stamps · true-scale heights).
   Pass 2 gave the family real masses, leaf cells and a serrated edge — and every broadleaf still came
   out of the same cauliflower, because the crown was placed on an ellipse FIRST and a few branches
   were drawn to it afterwards. The cells were Voronoi polygons: convex, random, the same shape on an
   oak as on a birch. And the whole family stood 5–7 m tall in a 32 px/m world where a house ridge is
   8 m. Pass 3 changes what a species IS in this rig:

     A. SKELETON FIRST.  A broadleaf is built from its branch architecture: fork height, number of
        primaries, the CURVE a limb takes to its target (oak: out nearly flat then up, kinked; maple:
        straight ascending co-dominants; birch: steep then arching, often two stems from one foot;
        aspen: a long clean pole with a few short limbs high up). Florets hang off the limb tips, so
        the crown silhouette is a consequence of the wood, and the wood is visible under the crown
        between the fork and the lowest florets. Winter is the SAME skeleton with twig fans — not a
        second tree.
        A conifer keeps its tier system but every species owns it: whorl count, taper exponent, plate
        droop, TIP-UP (the white pine's upswept plumes), openness, windswept asymmetry, a club top
        (black spruce), a flat top (mature pine), twin leaders (cedar), tufts strung along a bare bough
        (tamarack), dead bare twigs under the live crown (spruces).
     B. LEAF STAMPS, NOT CELLS.  The foliage surface is covered by authored 4–8 px leaf-cluster
        STENCILS, one grain per species (oak lobes, maple points, birch drops, aspen coins, spruce
        combs, fir shelves, pine tuft-fans, cedar fans, tamarack rosettes), scattered on a rotated,
        warped lattice and painted lower-over-upper so each stamp's authored TOP CONTOUR is what
        shows. A stamp is shaded flat from its own mean; its down/right seam steps down a band, its
        key-ward tip steps up, and the pixels no stamp covers are the dark between the leaves.
     C. EDGE PROFILE PER SPECIES.  The silhouette tooth wave now has a SHAPE — spikes for needles,
        round scallops for oak and birch, flat fans for cedar, pointed lobes for maple — so the outline
        agrees with the stamps.
     D. TRUE HEIGHTS × SCALE.  Every species carries its real mature height and crown spread in
        metres; the bake applies SCALE (0.6) so the tallest (white pine, 27 m) still fits a 4-row
        sheet under the 2048 cap. Relative scale across the family is now real: a white pine is 2.5×
        a black spruce, an oak crown is 3× an aspen's.
     E. RINGLESS (ADR 0031).  KEYLINE_DEFAULT = false; render(key, {outline:true}) is the live A/B.

   THE THREE RULES still hold and are still measured per sprite (mass floor · authored silhouette ·
   thickness-gated rim). SPEC: PPU 32 · ¾ from S at 40° (ADR-0006/0022) · bottom-centre TRUNK pivot
   · no AA · binary alpha · sheets ≤ 2048 px/axis · upper-left key (as every rig in this project).
   PALETTE: cold ambient (#1d3b4a) + ONE warm key (#e8b06a).

   globalThis.TreeRig3 — same surface as TreeRig2:
     SPECIES  VARIANTS  SWAY  SEASONS  PPU  SCALE  RIM_PX  MIN_BODY  MIN_R  KEYLINE_DEFAULT
     render(key,{variant,season,frame,size|stage,outline}) -> {w,h,pivot,rgba,masks:{front,rim,depth},report}
     packMask · grey · massView · normalView · leafView · massIdView · sheetSpec · cellOf · STENCILS */
(function (root) {
  'use strict';

  const PPU = 32, RIM_PX = 2, MIN_BODY = 6;
  const MIN_R = Math.ceil((MIN_BODY + 2 * RIM_PX) / 2);   // 5 px radius → 10 px clump
  const SWAY = 4, VARIANTS = 4;
  const SEASONS = ['summer', 'autumn', 'winter'];
  const SCALE = 0.6;                                      // bake scale against TRUE height (see D)
  const M2PX = SCALE * PPU;                               // px per true metre
  const KEYLINE_DEFAULT = false;                          // ADR 0031 — ringless; {outline:true} is the A/B
  const ELEV = 40, CE = Math.cos(ELEV * Math.PI / 180), SE = Math.sin(ELEV * Math.PI / 180);
  const KEYLINE = '#101d21';
  const COLD = '#1d3b4a', WARM = '#e8b06a';

  // ---- leaf stencils: one authored cluster shape per grain, several variants each ---------------
  // 'o' = leaf body. Lower stamps paint over upper ones, so the TOP contour of each shape is the
  // read: oak lobes with notches, maple points, birch drops (point down), aspen coins, spruce combs,
  // fir shelves (two ranks of flat needles), pine fans of long needles, cedar flattened fans,
  // tamarack rosettes. Mirrors are generated, so 3 rows here is 6 shapes.
  const ST = {
    needle:   [['..o..o.', 'ooooooo', '.oooooo'], ['.o..o..', 'oooooo.', 'ooooooo', '..o.o..'], ['o.o....', 'oooooo.', '.oooooo']],
    fir:      [['.o.o.o.', 'ooooooo', 'ooooooo'], ['o.o.o.o.', 'oooooooo', '.oooooo.'], ['.o.o.', 'ooooo', 'ooooo']],
    pineTuft: [['o..o..o', '.o.o.o.', '..ooo..', '.ooooo.', 'ooooooo'], ['o.o..o.o', '.o.oo.o.', '..oooo..', '.oooooo.', 'oooooooo'], ['.o..o..o', '.o.o..o.', '..ooo...', '.ooooo..', 'oooooo..']],
    scale:    [['.o.o.', 'ooooo', 'ooooo', '.ooo.', '.ooo.', '..o..'], ['o.o.o', 'ooooo', 'ooooo', 'ooooo', '.ooo.', '.ooo.', '..o..'], ['.o.o', 'oooo', 'oooo', '.oo.', '.oo.']],
    tuft:     [['.o.o.', 'ooooo', '.ooo.', 'ooooo', '.o.o.'], ['..o..', '.ooo.', 'ooooo', '.ooo.', '..o..'], ['.o.o.o', 'oooooo', '.oooo.', 'oooooo', '..o.o.']],
    broad:    [['...oo.ooo.', '.ooooooooo', 'oooooooooo', 'oooooooooo', '.ooooooooo', '..ooo.ooo.', '...oo..o..'], ['..oo.oo..', '.ooooooo.', 'ooooooooo', 'ooooooooo', '.ooooooo.', '..oo.oo..'], ['.ooo.oo.', 'oooooooo', 'oooooooo', 'oooooooo', '.oooooo.', '..oo.oo.']],
    maple:    [['..o..o..o', '..oo.o.oo', '.ooooooo.', 'ooooooooo', '.ooooooo.', '..ooooo..', '...ooo...', '....o....'], ['o...o...o', '.o..o..o.', '.ooooooo.', 'ooooooooo', '.ooooooo.', '..ooooo..', '....o....'], ['..o.o.o..', '.ooooooo.', 'ooooooooo', 'ooooooooo', '.ooooooo.', '..ooooo..', '...o.o...']],
    small:    [['.oooo.', 'oooooo', 'oooooo', '.oooo.', '..oo..', '..o...'], ['oooo', 'oooo', 'oooo', '.oo.', '.o..'], ['.ooooo.', 'ooooooo', 'ooooooo', '.ooooo.', '..ooo..', '...o...']],
    coin:     [['.ooo.', 'ooooo', 'ooooo', 'ooooo', '.ooo.'], ['.oo.', 'oooo', 'oooo', 'oooo', '.oo.'], ['..oo..', '.oooo.', 'oooooo', '.oooo.', '..oo..']],
  };
  function parseStencil(rows, mirror) {
    const h = rows.length, w = rows[0].length, px = [];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (rows[y][mirror ? w - 1 - x : x] !== '.') px.push([x - (w >> 1), y - (h >> 1)]);
    return { w, h, px, rows: mirror ? rows.map(r => r.split('').reverse().join('')) : rows };
  }
  const STENCILS = {};
  for (const k in ST) { STENCILS[k] = []; for (const rows of ST[k]) { STENCILS[k].push(parseStencil(rows, false)); STENCILS[k].push(parseStencil(rows, true)); } }

  // ---- leaf grain: PER SPECIES ------------------------------------------------------------------
  //   w,h   lattice pitch in px — sized so the stamps cover ~115% of the surface: what is left
  //         uncovered is the dark between leaves.   rot lattice rotation.   jit site jitter (a needle
  //         row keeps more order than a leaf pile).   warp low-frequency domain warp of the sites.
  //   tone  per-stamp ±1-band tone break.   flip  fraction of stamps two bands brighter (birch/aspen
  //         underside flash).   edge  which outline profile (EDGES) the grain cuts.
  const GRAINS = {
    needle:   { w: 4.6, h: 2.6, rot: -0.62, jit: 0.60, warp: 2.6, tone: 0.10, edge: 'needle' },
    fir:      { w: 5.0, h: 2.8, rot:  0.08, jit: 0.62, warp: 2.2, tone: 0.09, edge: 'fir' },
    pineTuft: { w: 5.6, h: 3.4, rot: -0.50, jit: 0.70, warp: 3.0, tone: 0.12, edge: 'pineTuft' },
    scale:    { w: 3.6, h: 4.6, rot:  0.10, jit: 0.60, warp: 2.0, tone: 0.11, edge: 'scale' },
    tuft:     { w: 3.8, h: 3.6, rot:  0.60, jit: 0.80, warp: 2.4, tone: 0.13, edge: 'tuft' },
    broad:    { w: 8.0, h: 6.0, rot:  0.35, jit: 0.75, warp: 3.0, tone: 0.24, edge: 'broad', corner: 1 },
    maple:    { w: 7.2, h: 5.4, rot: -0.30, jit: 0.75, warp: 2.8, tone: 0.22, edge: 'maple', corner: 1 },
    small:    { w: 5.2, h: 4.2, rot: -0.50, jit: 0.80, warp: 2.4, tone: 0.20, edge: 'small', flip: 0.07 },
    coin:     { w: 4.6, h: 3.8, rot:  0.40, jit: 0.85, warp: 2.2, tone: 0.20, edge: 'coin', flip: 0.10 },
  };
  for (const k in GRAINS) GRAINS[k].shapes = STENCILS[k];

  // ---- silhouette edge profile: PER SPECIES ------------------------------------------------------
  // amp in PIXELS against the local radius, teeth spaced along real arc length (pass 2b). NEW: shape —
  // 'spike' narrow needles on a pulled-in edge, 'round' scallops, 'fan' flat-topped cedar sprays,
  // 'tri' pointed lobes. base/under/flank weight where the teeth bite.
  const EDGES = {
    needle:   { shape: 'spike', pitch: 3.2, amp: 1.9, base: 0.30, under: 0.40, flank: 0.75, lobe: [0.085, 0.055] },
    fir:      { shape: 'tri',   pitch: 2.8, amp: 1.3, base: 0.35, under: 0.30, flank: 0.70, lobe: [0.060, 0.040] },
    pineTuft: { shape: 'spike', pitch: 4.2, amp: 2.5, base: 0.32, under: 0.36, flank: 0.80, lobe: [0.095, 0.050] },
    scale:    { shape: 'fan',   pitch: 5.4, amp: 1.4, base: 0.55, under: 0.30, flank: 0.30, lobe: [0.135, 0.075] },
    tuft:     { shape: 'round', pitch: 2.8, amp: 1.4, base: 0.55, under: 0.35, flank: 0.35, lobe: [0.110, 0.080] },
    broad:    { shape: 'round', pitch: 8.6, amp: 2.4, base: 0.60, under: 0.45, flank: 0.15, lobe: [0.150, 0.090], pitch2: 3.2, amp2: 0.6 },
    maple:    { shape: 'tri',   pitch: 6.0, amp: 2.3, base: 0.60, under: 0.40, flank: 0.20, lobe: [0.140, 0.080], pitch2: 2.4, amp2: 0.7 },
    small:    { shape: 'round', pitch: 4.0, amp: 1.3, base: 0.62, under: 0.40, flank: 0.20, lobe: [0.120, 0.075], pitch2: 2.0, amp2: 0.4 },
    coin:     { shape: 'round', pitch: 3.6, amp: 1.1, base: 0.62, under: 0.35, flank: 0.20, lobe: [0.110, 0.070] },
  };
  const grainOf = (sp) => GRAINS[sp.grain] || GRAINS.broad;
  const edgeOf = (sp) => EDGES[grainOf(sp).edge] || EDGES.broad;
  const eMaxOf = (E) => E.amp * (E.base + E.under + (E.flank || 0)) + (E.amp2 || 0) + 1;
  function tooth(shape, f) {
    const tri = Math.abs(f * 2 - 1) - 0.5;
    if (shape === 'round') return 0.5 * Math.cos(2 * Math.PI * f);
    if (shape === 'spike') { const t = 1 - Math.abs(f * 2 - 1); return Math.pow(t, 2.6) - 0.35; }
    if (shape === 'fan') return clamp(tri * 2.4, -0.5, 0.5);
    return tri;
  }

  // ---- vec ------------------------------------------------------------------
  const nrm = (v) => { const L = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / L, v[1] / L, v[2] / L]; };
  const LIGHT = {
    key: nrm([-0.55, -0.66, 0.52]),   // upper-LEFT, slightly toward camera — the key every rig here shades from
    rim: nrm([0.48, -0.28, -0.83]),   // behind & upper-right — the back-rim channel
  };
  function clamp(v, a, b) { return v < a ? a : v > b ? b : v; }
  const smooth = (e0, e1, x) => { const t = clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); };

  // ---- colour ---------------------------------------------------------------
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const r2h = (r) => '#' + r.map(v => clamp(Math.round(v), 0, 255).toString(16).padStart(2, '0')).join('');
  const mix = (a, b, t) => { const A = h2r(a), B = h2r(b); return r2h([0, 1, 2].map(i => A[i] + (B[i] - A[i]) * t)); };
  function folRamp(fol, season, fall) {
    const base = season === 'autumn' && fall ? mix(fol, fall, 0.78) : season === 'winter' ? mix(fol, '#2c4a4f', 0.34) : fol;
    return {
      dp:  mix(mix(base, '#000000', 0.74), COLD, 0.24),
      sh:  mix(mix(base, '#000000', 0.52), COLD, 0.22),
      mid: mix(base, '#000000', 0.22),
      hi:  mix(base, WARM, 0.16),
      key: mix(mix(base, '#ffffff', 0.10), WARM, 0.34),
      rim: mix(WARM, '#fff3df', 0.14),
    };
  }
  function barkRamp(bark, pale) {
    const b = pale ? mix(bark, COLD, 0.46) : bark;
    return {
      dp:  mix(mix(b, '#000000', 0.82), COLD, 0.42),
      sh:  mix(mix(b, '#000000', 0.60), COLD, 0.28),
      mid: mix(b, '#000000', 0.34),
      hi:  mix(b, WARM, pale ? 0.08 : 0.18),
      key: mix(mix(b, '#000000', 0.10), WARM, pale ? 0.18 : 0.40),
      rim: mix(b, WARM, pale ? 0.55 : 0.72),
    };
  }
  const SNOW = { dp: '#5c7180', sh: '#7d93a0', mid: '#a8bcc4', hi: '#cfdde1', key: '#eef4f4', rim: '#fff6e6' };

  // ---- species --------------------------------------------------------------------------------
  // real   TRUE mature height, m.  crown  TRUE mature crown spread, m.  dbh  TRUE trunk diameter, m.
  // Conifers: cb crown-base fraction of H · tiers · taperE (R ∝ (1−f)^taperE) · droop · tipUp (plume
  //   rise) · gappy (bough skip) · asym (windswept reach) · top spire|club|flat · dead (bare twigs
  //   under the crown) · boughs per whorl · stems (twin leaders) · stiff (low variance) · leaderZ.
  // Broadleaves: fork (fraction of H) · nP primaries · curve oak|maple|birch|aspen · kink · limbR
  //   [fork, elbow, tip] × trunkR · cycF/chF crown ellipse centre & half-height (of H) · underOpen
  //   (radians of the ellipse bottom left open — the wood shows there) · flor floret radius (of cw) ·
  //   hang hanging florets · secs secondaries per primary · stems (two trunks from one foot, even
  //   variants).  bark grain: furrow | plate | scale | shred | smooth | paper.
  const SPECIES = [
    { key: 'RedSpruce', name: 'Red Spruce', latin: 'Picea rubens', form: 'spire', grain: 'needle', barkGrain: 'scale',
      real: 21, crown: 6.0, dbh: 0.55, fol: '#356343', bark: '#5a4433', sway: 1.5,
      cb: 0.22, tiers: 17, taperE: 0.76, droop: 0.42, tipUp: 0.12, gappy: 0.08, asym: 0.08, top: 'spire', dead: 3, boughs: 8, plate: 0.36, leaderZ: -0.6 },
    { key: 'BlackSpruce', name: 'Black Spruce', latin: 'Picea mariana', form: 'spire', grain: 'needle', barkGrain: 'scale',
      real: 11, crown: 2.8, dbh: 0.28, fol: '#2c5740', bark: '#4e3b2d', sway: 1.3,
      cb: 0.30, tiers: 14, taperE: 0.86, droop: 0.46, tipUp: 0.05, gappy: 0.45, asym: 0.05, top: 'club', dead: 4, boughs: 6, plate: 0.38, leaderZ: -0.6 },
    { key: 'BalsamFir', name: 'Balsam Fir', latin: 'Abies balsamea', form: 'spire', grain: 'fir', barkGrain: 'smooth',
      real: 16, crown: 4.6, dbh: 0.40, fol: '#356842', bark: '#55432f', sway: 1.4,
      cb: 0.12, tiers: 17, taperE: 0.72, droop: 0.04, tipUp: 0.10, gappy: 0.0, asym: 0.0, top: 'spire', dead: 0, boughs: 8, plate: 0.36, stiff: true, leaderZ: -0.6 },
    { key: 'WhitePine', name: 'E. White Pine', latin: 'Pinus strobus', form: 'pine', grain: 'pineTuft', barkGrain: 'plate',
      real: 27, crown: 11, dbh: 0.90, fol: '#3e7048', bark: '#5f4834', sway: 2.0,
      cb: 0.42, tiers: 6, taperE: 0.55, droop: 0.0, tipUp: 0.55, gappy: 0.12, asym: 0.35, top: 'flat', dead: 1, boughs: 5, plate: 0.30, maxPlate: 18, whorlJit: 0.30, leaderZ: -0.25 },
    { key: 'WhiteCedar', name: 'E. White Cedar', latin: 'Thuja occidentalis', form: 'cedar', grain: 'scale', barkGrain: 'shred',
      real: 13, crown: 4.2, dbh: 0.45, fol: '#386639', bark: '#6a4c37', sway: 1.2,
      cb: 0.10, tiers: 20, taperE: 0.45, droop: 0.30, tipUp: 0.0, gappy: 0.0, asym: 0.0, top: 'spire', dead: 0, boughs: 7, plate: 0.50, stems: 2, leaderZ: -0.6 },
    { key: 'Tamarack', name: 'Tamarack', latin: 'Larix laricina', form: 'larch', grain: 'tuft', barkGrain: 'plate',
      real: 17, crown: 5.2, dbh: 0.40, fol: '#5d8133', bark: '#57422f', fall: '#d3a238', sway: 1.8,
      cb: 0.25, tiers: 13, taperE: 0.74, droop: 0.12, tipUp: 0.0, gappy: 0.30, asym: 0.06, top: 'spire', dead: 0, boughs: 5, leaderZ: -0.35 },
    { key: 'WhiteBirch', name: 'White Birch', latin: 'Betula papyrifera', form: 'oval', grain: 'small', barkGrain: 'paper', pale: true,
      real: 17, crown: 7.0, dbh: 0.40, fol: '#477534', bark: '#d8dcd4', fall: '#d9a832', sway: 2.6,
      fork: 0.32, nP: 4, curve: 'birch', kink: 0.10, limbR: [0.55, 0.30, 0.14], cycF: 0.66, chF: 0.30, underOpen: 0.42, flor: 0.30, hang: 2, secs: 1, secAt: 0.35, stems: 2, droop: 0.30 },
    { key: 'RedMaple', name: 'Red Maple', latin: 'Acer rubrum', form: 'round', grain: 'maple', barkGrain: 'plate',
      real: 20, crown: 9.0, dbh: 0.60, fol: '#3a6e30', bark: '#544639', fall: '#bf3f26', sway: 2.2,
      fork: 0.27, nP: 4, curve: 'maple', kink: 0.10, limbR: [0.62, 0.34, 0.16], cycF: 0.60, chF: 0.33, underOpen: 0.40, flor: 0.30, hang: 1, secs: 1, secAt: 0.0, droop: 0.22 },
    { key: 'RedOak', name: 'Red Oak', latin: 'Quercus rubra', form: 'round', grain: 'broad', barkGrain: 'furrow',
      real: 22, crown: 15, dbh: 0.85, fol: '#37602f', bark: '#4f4235', fall: '#a35429', sway: 1.9,
      fork: 0.30, nP: 4, curve: 'oak', kink: 0.22, limbR: [0.72, 0.42, 0.18], cycF: 0.63, chF: 0.29, underOpen: 0.70, flor: 0.26, hang: 2, secs: 2, secAt: 0.5, droop: 0.16 },
    { key: 'TremblingAspen', name: 'Trembling Aspen', latin: 'Populus tremuloides', form: 'oval', grain: 'coin', barkGrain: 'smooth', pale: true,
      real: 18, crown: 5.0, dbh: 0.35, fol: '#5b8136', bark: '#b9bfae', fall: '#e0b03a', sway: 3.1,
      fork: 0.54, nP: 3, curve: 'aspen', kink: 0.06, limbR: [0.55, 0.30, 0.14], cycF: 0.72, chF: 0.27, underOpen: 0.42, flor: 0.32, hang: 0, secs: 1, secAt: 0.3, droop: 0.12 },
  ];
  const byKey = {}; SPECIES.forEach(s => byKey[s.key] = s);
  SPECIES.forEach(s => { s.worldH = s.real * M2PX; s.h = s.worldH; s.w = s.crown * M2PX; s.birch = !!s.pale; });

  const hashKey = (k) => { let h = 2166136261; for (let i = 0; i < k.length; i++) { h ^= k.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; };
  const rngOf = (a) => function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };

  // ---- volume buffer (z-buffered surface with per-pixel normals) -------------
  const M = { NONE: 0, FOLIAGE: 1, BARK: 2, TWIG: 3 };
  function Vol(w, h) {
    const n = w * h;
    this.w = w; this.h = h;
    this.z = new Float32Array(n).fill(-1e9);
    this.nx = new Float32Array(n); this.ny = new Float32Array(n); this.nz = new Float32Array(n);
    this.mat = new Uint8Array(n); this.a = new Uint8Array(n);
    this.id = new Int16Array(n).fill(-1);
    this.mid = new Int16Array(n).fill(-1);
  }
  Vol.prototype.clearPx = function (i) { this.a[i] = 0; this.mat[i] = 0; this.z[i] = -1e9; this.id[i] = -1; this.mid[i] = -1; };

  // ellipsoid front surface with the species' edge profile on its outline (rule 2)
  function blob(v, cx, cy, cz, rx, ry, rz, mat, id, mid, seed, E) {
    E = E || EDGES.broad;
    const p1 = (seed || 0) * 1.7, p2 = (seed || 0) * 3.1, p3 = (seed || 0) * 5.3;
    const NA = 64, TAU = Math.PI * 2, arc = new Float32Array(NA + 1);
    let ax = rx, ay = 0, tot = 0;
    for (let i = 1; i <= NA; i++) {
      const a = i / NA * TAU, X = Math.cos(a) * rx, Y = Math.sin(a) * ry;
      tot += Math.hypot(X - ax, Y - ay); arc[i] = tot; ax = X; ay = Y;
    }
    if (tot < 1e-3) return;
    const nT = Math.max(4, Math.round(tot / E.pitch));
    const nT2 = E.pitch2 ? Math.max(6, Math.round(tot / E.pitch2)) : 0;
    const pad = eMaxOf(E) + 1, shape = E.shape || 'tri';
    const x0 = Math.max(0, Math.floor(cx - rx * 1.3 - pad)), x1 = Math.min(v.w - 1, Math.ceil(cx + rx * 1.3 + pad));
    const y0 = Math.max(0, Math.floor(cy - ry * 1.3 - pad)), y1 = Math.min(v.h - 1, Math.ceil(cy + ry * 1.3 + pad));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      let u = (x + 0.5 - cx) / rx, w = (y + 0.5 - cy) / ry;
      const th = Math.atan2(w, u);
      const ct = Math.cos(th), stt = Math.sin(th);
      const under = 0.5 + 0.5 * stt, flank = Math.abs(stt);
      const fi = ((th + TAU) % TAU) / TAU * NA, i0 = fi | 0;
      const s = (arc[i0] + (arc[i0 + 1] - arc[i0]) * (fi - i0)) / tot;
      const rEff = Math.hypot(ct * rx, stt * ry) || 1;
      const tw = tooth(shape, ((s * nT + p3) % 1 + 1) % 1);
      let bite = tw * E.amp * (E.base + E.under * under + (E.flank || 0) * flank);
      if (nT2) bite += (Math.abs(((s * nT2 + p2) % 1 + 1) % 1 * 2 - 1) - 0.5) * E.amp2;
      const k = 1 + E.lobe[0] * Math.sin(3 * th + p1) + E.lobe[1] * Math.sin(5 * th + p2) + bite / rEff;
      u /= k; w /= k;
      const sq = u * u + w * w;
      if (sq > 1) continue;
      const t = Math.sqrt(1 - sq), z = cz + t * rz, i = y * v.w + x;
      if (z <= v.z[i]) continue;
      let nx = u / rx, ny = w / ry, nz = t / rz; const L = Math.hypot(nx, ny, nz) || 1;
      v.z[i] = z; v.nx[i] = nx / L; v.ny[i] = ny / L; v.nz[i] = nz / L;
      v.mat[i] = mat; v.a[i] = 1; v.id[i] = id; v.mid[i] = mid;
    }
  }
  // swept sphere (trunk / limb): cylinder-ish normals, tapered
  function limb(v, x0, y0, z0, x1, y1, z1, r0, r1, mat, id, mid) {
    const dx = x1 - x0, dy = y1 - y0, L2 = dx * dx + dy * dy || 1e-6, R = Math.max(r0, r1);
    const ax = Math.max(0, Math.floor(Math.min(x0, x1) - R)), bx = Math.min(v.w - 1, Math.ceil(Math.max(x0, x1) + R));
    const ay = Math.max(0, Math.floor(Math.min(y0, y1) - R)), by = Math.min(v.h - 1, Math.ceil(Math.max(y0, y1) + R));
    for (let y = ay; y <= by; y++) for (let x = ax; x <= bx; x++) {
      let t = ((x + 0.5 - x0) * dx + (y + 0.5 - y0) * dy) / L2; t = clamp(t, 0, 1);
      const px = x0 + dx * t, py = y0 + dy * t, pz = z0 + (z1 - z0) * t, r = r0 + (r1 - r0) * t;
      if (r <= 0.35) continue;
      const ox = x + 0.5 - px, oy = y + 0.5 - py, d2 = ox * ox + oy * oy;
      if (d2 > r * r) continue;
      const k = Math.sqrt(r * r - d2), z = pz + k, i = y * v.w + x;
      if (z <= v.z[i]) continue;
      let nx = ox / r, ny = oy / r * 0.35, nz = k / r; const Ln = Math.hypot(nx, ny, nz) || 1;
      v.z[i] = z; v.nx[i] = nx / Ln; v.ny[i] = ny / Ln; v.nz[i] = nz / Ln;
      v.mat[i] = mat; v.a[i] = 1; v.id[i] = id; v.mid[i] = mid;
    }
  }

  // ---- RULE 1: a ring only carries clumps it can carry at full size ----------
  function ringPlan(R, want, rWant) {
    const r = Math.max(MIN_R, rWant);
    const n = Math.max(1, Math.min(want, Math.floor((2 * Math.PI * Math.max(R, 0.5)) / (2.15 * r))));
    return { n, r };
  }
  function tierCount(crownH, want) { return clamp(Math.floor(crownH / (2 * MIN_R + 8)), 4, want); }
  function tierCountOpen(crownH, want) { return clamp(Math.floor(crownH / (MIN_R + 6)), 5, want); }

  // ---- growth stage ----------------------------------------------------------
  const STAGES = { sapling: 0.22, young: 0.45, pole: 0.70, mature: 1.00 };
  const STAGE_KEYS = ['sapling', 'young', 'pole', 'mature'];
  function sizeOf(o) {
    if (o && typeof o.size === 'number') return clamp(o.size, 0.12, 1.4);
    if (o && STAGES[o.stage]) return STAGES[o.stage];
    return 1;
  }
  const stageName = (t) => t < 0.33 ? 'sapling' : t < 0.58 ? 'young' : t < 0.85 ? 'pole' : 'mature';

  // ---- floret: a core plus its own ring of satellites, one mass ---------------------------------
  function floret(clumps, rng, fx, fy, fz, fr, mid) {
    clumps.push({ x: fx, y: fy, z: fz - fr * 0.30, rx: fr * 0.92, ry: fr * 0.80, rz: fr * 0.86, m: mid });
    const ns = fr > MIN_R * 2.6 ? 6 : 5, sp0 = rng() * Math.PI * 2;
    for (let s = 0; s < ns; s++) {
      const sa = sp0 + s / ns * Math.PI * 2;
      const sr = Math.max(MIN_R, fr * (0.50 + rng() * 0.10));
      const up = -Math.sin(sa);
      clumps.push({
        x: fx + Math.cos(sa) * fr * (0.62 + rng() * 0.10),
        y: fy + Math.sin(sa) * fr * (0.56 + rng() * 0.10) * 0.92,
        z: fz + up * fr * 0.52 + (rng() * 2 - 1) * fr * 0.12,
        rx: sr * 1.04, ry: sr * 0.88, rz: sr, m: mid,
      });
    }
  }
  // a limb's path from F to T, shaped by the species (see SPECIES). Returns the elbow.
  function elbow(F, T, curve, kink, rng) {
    const dx = T[0] - F[0], dy = T[1] - F[1], dz = T[2] - F[2], L = Math.hypot(dx, dy, dz) || 1;
    const hl = Math.hypot(dx, dz) || 1, ox = dx / hl, oz = dz / hl;   // outward, horizontal
    let C;
    if (curve === 'oak')        C = [F[0] + dx * 0.46 + ox * L * 0.14, F[1] + dy * 0.46 + L * 0.05, F[2] + dz * 0.46 + oz * L * 0.14];
    else if (curve === 'birch') C = [F[0] + dx * 0.42 - ox * L * 0.06, F[1] + dy * 0.42 - L * 0.24, F[2] + dz * 0.42 - oz * L * 0.06];
    else if (curve === 'maple') C = [F[0] + dx * 0.55 + ox * L * 0.05, F[1] + dy * 0.55 - L * 0.06, F[2] + dz * 0.55 + oz * L * 0.05];
    else                        C = [F[0] + dx * 0.50 + ox * L * 0.03, F[1] + dy * 0.50, F[2] + dz * 0.50];
    C[0] += (rng() * 2 - 1) * kink * L * 0.45;
    C[1] += (rng() * 2 - 1) * kink * L * 0.25;
    return C;
  }

  // ---- skeleton + crown builders --------------------------------------------
  function build(sp, variant, season, size) {
    const t = size == null ? 1 : size;
    const rng = rngOf(hashKey(sp.key) + variant * 7717 + (season === 'winter' ? 31 : 0) + Math.round(t * 997));
    const grow = smooth(0.18, 1, t);
    const H = Math.max(26, sp.worldH * t);
    const crownPx = Math.max(18, sp.crown * M2PX * Math.pow(t, 1.15));
    const W = crownPx + 8;
    const cx = Math.floor(W / 2) + 0.5, baseY = H - 1.5;
    const conifer = sp.form === 'spire' || sp.form === 'pine' || sp.form === 'cedar' || sp.form === 'larch';
    const bare = season === 'winter' && (sp.form === 'round' || sp.form === 'oval' || sp.form === 'larch');
    const lean = (rng() * 2 - 1) * 0.04;
    const limbs = [], clumps = [];
    let massN = 0;
    const droopF = sp.droop * (0.5 + 0.5 * grow);
    const topY = Math.max(MIN_R + 2, baseY - (H - 6) * (0.95 + rng() * 0.08));
    const E = edgeOf(sp), eMax = eMaxOf(E);
    // A 'spike' edge sits its flats INSIDE the nominal outline (the needles stand proud of a pulled-in
    // edge), so a needle species floors its plates one pixel thicker to keep rule 1's 10 px body.
    const MR = MIN_R + (E.shape === 'spike' ? Math.ceil(0.35 * E.amp * (E.base + E.under + (E.flank || 0)) - 0.01) : 0);
    const done = () => ({ W, H, cx, baseY, limbs, clumps, bare, conifer, rng, masses: massN, eMax, trunkR });

    // ---- trunk: 3 splayed root buttresses → bole. Trunk radius is TRUE dbh × SCALE, grown with t. ---
    const trunkR = Math.max(1.4, (sp.dbh || 0.5) * M2PX / 2 * Math.pow(t, 1.6));
    const tz = 0, rise = trunkR * 1.6;
    const boleY = baseY - H * (conifer ? 0.08 : Math.min(0.10, sp.fork * 0.4));
    const boleX = cx + lean * H * 0.25;
    for (let r = 0; r < 3; r++) {
      const a = -Math.PI / 2 + (r - 1) * 1.15 + (rng() * 2 - 1) * 0.12;
      const ex = cx + Math.cos(a) * trunkR * (0.85 + rng() * 0.25);
      const ez = Math.sin(a) * trunkR * 0.75;
      limbs.push([ex, baseY + 0.5, ez, cx, baseY - rise, tz, trunkR * 0.46, trunkR * 1.02, M.BARK, massN++]);
    }
    const boleM = massN++;
    limbs.push([cx, baseY - rise * 0.6, tz, boleX, boleY, tz, trunkR * 1.10, trunkR * 1.0, M.BARK, boleM]);
    // two trunks from one foot (birch clumps, cedar twin leaders) on even variants, once grown
    const stems = (sp.stems && variant % 2 === 0 && grow > 0.5 && crownPx > 60) ? sp.stems : 1;

    if (conifer) {
      const cbF = sp.cb * (0.35 + 0.65 * grow);
      const crownBase = baseY - H * cbF;
      const crownH = crownBase - topY;
      const open = sp.form === 'larch' || sp.form === 'cedar';
      const tiers = open ? tierCountOpen(crownH, sp.tiers) : tierCount(crownH, sp.tiers);
      const maxR = crownPx / 2 / 1.55;
      const zsq = sp.form === 'spire' ? 0.52 : sp.form === 'pine' ? 0.62 : 0.82;
      const asymDir = rng() * Math.PI * 2;
      const stemDX = stems > 1 ? crownPx * 0.11 : 0;
      const stemX = (k, f) => cx + lean * H * 0.5 * f + (stems > 1 ? ((k % 2) ? 1 : -1) * stemDX * smooth(0.05, 0.45, f) : 0);
      // leader(s): behind the bough origins for the spires (a glimpse between tiers), close to the
      // front for the pine, whose trunk is meant to be SEEN between its whorls. The set-back is in
      // PIXELS, not trunk radii: a sapling's 1.4 px leader at z = −0.8 sliced its own back plates
      // into slivers too thin to hold a rim.
      for (let s = 0; s < stems; s++) {
        const midY = boleY - (boleY - topY) * 0.35, lz = sp.form === 'pine' ? sp.leaderZ * trunkR : -Math.max(2.6, -sp.leaderZ * trunkR);
        const mx = stemX(s, 0.35), txp = stemX(s, 1);
        limbs.push([boleX, boleY, tz, mx, midY, lz, trunkR * 1.02, trunkR * 0.74, M.BARK, boleM]);
        limbs.push([mx, midY, lz, txp, topY + MIN_R * 0.6, lz * 1.6, trunkR * 0.74, 1.0, M.BARK, boleM]);
      }
      // dead lower branches: bare, drooping twigs under the live crown (mature spruces self-prune
      // but keep the stubs). Rule 3 keeps the rim off them.
      if (sp.dead && grow > 0.62 && !bare) {
        for (let d = 0; d < sp.dead; d++) {
          const a = rng() * Math.PI * 2, y = crownBase + H * (0.02 + rng() * 0.09), reach = maxR * (0.35 + rng() * 0.30);
          limbs.push([cx + lean * H * 0.5 * 0.1, y, -trunkR * 0.3, cx + Math.cos(a) * reach, y + reach * 0.45, Math.sin(a) * reach * 0.6, 1.7, 0.8, M.TWIG, massN++]);
        }
      }
      if (bare) {   // bare tamarack: the same boughs with no tufts
        for (let i = 0; i < tiers; i++) {
          const f = i / (tiers - 1), y = crownBase + (topY - crownBase) * f;
          const R = maxR * Math.pow(1 - f, sp.taperE) * (0.85 + rng() * 0.3);
          const n = R < 6 ? 2 : 4;
          for (let k = 0; k < n; k++) {
            const a = rng() * Math.PI * 2 + k / n * Math.PI * 2, reach = R * (0.70 + rng() * 0.30);
            limbs.push([stemX(0, f), y, tz, cx + Math.cos(a) * reach, y + reach * 0.30, tz + Math.sin(a) * reach * 0.8, 2.0, 0.9, M.TWIG, massN++]);
          }
        }
        return done();
      }
      for (let i = 0; i < tiers; i++) {
        const f = i / (tiers - 1);
        let y = crownBase + (topY - crownBase) * Math.pow(f, sp.form === 'pine' ? 0.88 : 1.0);
        if (sp.whorlJit && i > 0 && i < tiers - 1) y += (rng() * 2 - 1) * sp.whorlJit * crownH / tiers;
        let R = maxR * Math.pow(1 - f * (sp.top === 'flat' ? 0.82 : 1), sp.taperE);   // a flat top keeps its width
        if (sp.top === 'club') R *= 1 + 0.9 * smooth(0.66, 0.92, f) * (1 - smooth(0.92, 1.0, f));   // the crow's-nest top
        R *= sp.stiff ? (0.96 + rng() * 0.08) : (0.88 + rng() * 0.24);
        const sxk = stemX(i, f);
        const Rs = stems > 1 ? R * 0.74 : R;
        const rWant = Math.min(sp.maxPlate || 24, Math.max(MR, Rs * (sp.plate || 0.40)));
        const plan = ringPlan(Rs, sp.boughs || 7, rWant);
        const phase = rng() * Math.PI * 2;
        const gapHere = (sp.top === 'club' && f > 0.66) ? 0 : (sp.gappy || 0) * (1 - f * 0.5);
        for (let k = 0; k < plan.n; k++) {
          if (gapHere && rng() < gapHere && plan.n > 2) continue;
          const a = phase + (k / plan.n) * Math.PI * 2;
          const ca = Math.abs(Math.cos(a)), sa = Math.abs(Math.sin(a));
          const rr = Math.max(MR, plan.r * (sp.stiff ? 0.96 + rng() * 0.1 : 0.92 + rng() * 0.2));
          const bm = massN++;
          const asymK = 1 + (sp.asym || 0) * Math.cos(a - asymDir);

          if (sp.form === 'larch') {
            // a slender bare bough with 2–3 rosette tufts strung ALONG it — the branch shows between
            const reach = Rs * (0.72 + rng() * 0.28) * asymK;
            const ex = sxk + Math.cos(a) * reach, ez = Math.sin(a) * reach * 0.82;
            const ey = y + reach * 0.18 * (0.5 + droopF);
            limbs.push([sxk, y, 0, ex, ey, ez, 1.9, 1.0, M.TWIG, bm]);
            const nT = reach > 24 ? 3 : 2, us = nT === 3 ? [0.42, 0.72, 1.0] : [0.58, 1.0];
            for (let q = 0; q < nT; q++) {
              const u = us[q], tr = Math.max(MIN_R, rr * (q === nT - 1 ? 0.80 : 0.62));
              clumps.push({ x: sxk + (ex - sxk) * u, y: y + (ey - y) * u - tr * 0.25, z: ez * u, rx: Math.max(MIN_R, tr * 1.1), ry: Math.max(MIN_R, tr * 0.85), rz: Math.max(MIN_R, tr), m: bm });
            }
            continue;
          }
          if (sp.form === 'cedar') {
            const alt = (k % 2) ? 0.62 : 0.98;
            const reach = Rs * alt * (0.92 + rng() * 0.16);
            const px = sxk + Math.cos(a) * reach, pz = Math.sin(a) * reach * 0.7;
            const py = y + (rng() * 2 - 1) * rr * 0.4 - (alt > 0.8 ? rr * 0.3 : 0);
            clumps.push({ x: px, y: py, z: pz, m: bm, rx: Math.max(MIN_R, rr * 0.74), ry: Math.max(MIN_R, rr * 1.5), rz: Math.max(MIN_R, rr * 0.74) });
            continue;
          }
          // spruce / fir / pine bough: feeder → plate → tip (raised by tipUp) → inner shoulder → droop
          const reach = Rs * (0.62 + rng() * 0.38) * asymK;
          const px = sxk + Math.cos(a) * reach, pz = Math.sin(a) * reach * zsq;
          const skirt = (1 - f) * (1 - f);
          const jit = sp.stiff ? 0.10 : 0.30;
          const py = y + rr * droopF * (0.4 + ca * 0.9) + (rng() * 2 - 1) * rr * jit + rng() * rr * 0.42 * skirt - sp.tipUp * rr * 0.25;
          const pine = sp.form === 'pine';
          if (pine) {
            // WHITE PINE: a long bare ARM out from the trunk, a thin flat plate of needles along its
            // outer half, and the tuft at its end swept UP — the plume that names the tree. Nothing
            // hangs, there is no inner shoulder, and the trunk shows in the gap between whorls.
            const reach = Rs * (0.72 + rng() * 0.28) * asymK;
            const px = sxk + Math.cos(a) * reach, pz = Math.sin(a) * reach * zsq;
            const py = y + (rng() * 2 - 1) * rr * 0.25 + rr * 0.15;
            limbs.push([sxk, y - 0.5, -trunkR * 0.2, px * 0.80 + sxk * 0.20, py * 0.80 + y * 0.20 + rr * 0.10, pz * 0.80,
              Math.max(1.6, trunkR * 0.28), 1.3, M.BARK, bm]);
            clumps.push({ x: px, y: py, z: pz, m: bm, rx: Math.max(MR, rr * (1.15 + ca * 0.55)), ry: Math.max(MR, rr * 0.36), rz: Math.max(MR, rr * (0.9 + sa * 0.5)) });
            clumps.push({ x: px + Math.cos(a) * rr * 0.75, y: py - rr * (0.55 + sp.tipUp * 0.8), z: pz + Math.sin(a) * rr * 0.6 * zsq, m: bm,
              rx: Math.max(MR, rr * 0.74), ry: Math.max(MR, rr * 0.72), rz: Math.max(MR, rr * 0.72) });
            clumps.push({ x: px - Math.cos(a) * rr * 0.75, y: py - rr * 0.50, z: pz - Math.sin(a) * rr * 0.5 * zsq, m: bm,
              rx: Math.max(MR, rr * 0.56), ry: Math.max(MR, rr * 0.52), rz: Math.max(MR, rr * 0.56) });
            continue;
          }
          if (reach > Rs * 0.5 && f < 0.92) {
            // the feeder. On a pine it is a real LIMB — long, bare, seen in the whorl gap — and it
            // ends inside the plate; on a spruce it stops early and sits behind the needles.
            const u = pine ? 0.72 : 0.40;
            limbs.push([sxk, y - 0.5, pine ? -trunkR * 0.2 : -trunkR * 0.5,
              px * u + sxk * (1 - u), py * u + y * (1 - u) + (pine ? rr * 0.15 : 0), pz * u * (pine ? 0.9 : 0.8),
              Math.max(1.5, trunkR * (pine ? 0.26 : 0.32)), 1.1, pine ? M.BARK : M.TWIG, bm]);
          }
          const plateAsp = sp.form === 'spire' ? 0.62 : 1;
          const small = rr <= MR * 1.3 ? 0.74 : 1;          // a floor-size plate stays rounder: a flat ellipse's tips fall under the rim floor
          clumps.push({ x: px, y: py, z: pz, m: bm,
            rx: Math.max(MR, rr * (0.86 + ca * 0.95) * small),
            ry: Math.max(MR, rr * (pine ? 0.40 : 0.44) * (1 + sp.tipUp * 0.5)),
            rz: Math.max(MR, rr * (0.86 + sa * 0.95) * plateAsp) });
          if (reach > Rs * 0.5 && rr > MR * 1.3) {
            const tr = Math.max(MR, rr * 0.66);
            clumps.push({ x: px + Math.cos(a) * rr * 0.95, y: py + rr * 0.18 - sp.tipUp * rr * 1.05, z: pz + Math.sin(a) * rr * 0.8 * zsq, m: bm,
              rx: Math.max(MR, tr * (0.78 + ca * 0.6)), ry: Math.max(MR, tr * (0.42 + sp.tipUp * 0.55)),
              rz: Math.max(MR, tr * (0.78 + sa * 0.6) * plateAsp) });
          }
          // inner shoulder — not on a pine, whose boughs are bare wood until the tuft
          if (!pine) clumps.push({ x: px * 0.62 + sxk * 0.38, y: py - rr * 0.42, z: pz * 0.62, m: bm,
            rx: Math.max(MR, rr * 0.78), ry: Math.max(MR, rr * 0.48),
            rz: Math.max(MR, rr * 0.78 * (sp.form === 'spire' ? 0.7 : 1)) });
          if (droopF > 0.25 && reach > Rs * 0.62 && rr > MR * 1.4) {
            const dr = Math.max(MR, rr * 0.62);
            clumps.push({ x: px + Math.cos(a) * dr * 0.9, y: py + dr * (0.55 + droopF * 0.55), z: pz + Math.sin(a) * dr * 0.7 * zsq,
              rx: dr * 1.0, ry: Math.max(MR, dr * 0.62), rz: Math.max(MR, dr * 0.9 * (sp.form === 'spire' ? 0.66 : 1)), m: bm });
          }
        }
        // dark heart, deep in z, its own mass — tiers separate against it
        const hm = massN++;
        if (sp.form === 'larch' || sp.form === 'pine') { /* open crown: the trunk is meant to show */ }
        else if (f < 0.94 && sp.form !== 'spire') clumps.push({ x: sxk, y: y + 3, z: -R * 0.9 - 3, rx: Math.max(MR, R * 0.30), ry: Math.max(MR, R * 0.40), rz: MR, m: hm });
        else if (f < 0.94) clumps.push({ x: sxk, y: y + 5, z: -R * 0.9 - 3, rx: MR, ry: MR * 1.3, rz: MR * 0.8, m: hm });
      }
      // the apex: a spire point, a fir's needle-sharp tip, or the pine's irregular flat top
      for (let s = 0; s < stems; s++) {
        const ax = stemX(s, 1);
        if (sp.top === 'flat') clumps.push({ x: ax + (rng() * 2 - 1) * maxR * 0.2, y: topY + MR * 1.1, z: 2, rx: MR * 1.5, ry: MR * 0.9, rz: MR * 1.2, m: massN++ });
        else clumps.push({ x: ax, y: topY + MR * 0.7, z: 0, rx: MR * 1.05, ry: MR * (sp.stiff ? 1.6 : 1.25), rz: MR, m: massN++ });
      }
      return done();
    }

    // ================= BROADLEAF: skeleton first ================================================
    const cw = crownPx / 2 - 2.5;
    const ch = H * sp.chF;
    const cyc = baseY - H * sp.cycF * (0.78 + 0.22 * grow);
    const forkY = baseY - H * sp.fork;
    // fork point(s): a single trunk forks at forkY; a two-stem birch splits at the bole and each stem
    // carries its own fork, set apart
    const forks = [];
    if (stems > 1) {
      for (let s = 0; s < stems; s++) {
        const sgn = s ? 1 : -1, fx = cx + sgn * cw * 0.20 + lean * H * 0.4, fy = forkY + (rng() * 2 - 1) * H * 0.03;
        limbs.push([boleX, boleY, tz, fx, fy, sgn * trunkR * 0.4, trunkR * 0.82, trunkR * 0.60, M.BARK, boleM]);
        forks.push([fx, fy, sgn * trunkR * 0.4]);
      }
    } else {
      const fx = cx + lean * H * 0.5;
      limbs.push([boleX, boleY, tz, fx, forkY, tz, trunkR * 1.08, trunkR * 0.72, M.BARK, boleM]);
      forks.push([fx, forkY, tz]);
    }
    // arc-length parameterisation of the crown ellipse — floret targets are spaced along it (rule 2)
    const NS = 240, cum = [0];
    let per = 0;
    for (let i = 1; i <= NS; i++) {
      const a0 = (i - 1) / NS * Math.PI * 2, a1 = i / NS * Math.PI * 2;
      per += Math.hypot((Math.cos(a1) - Math.cos(a0)) * cw, (Math.sin(a1) - Math.sin(a0)) * ch);
      cum.push(per);
    }
    const angAt = (tt) => {
      const target = ((tt % 1) + 1) % 1 * per; let lo = 0, hi = NS;
      while (lo < hi) { const m = (lo + hi) >> 1; if (cum[m] < target) lo = m + 1; else hi = m; }
      return lo / NS * Math.PI * 2;
    };
    const tOf = (ang) => { const a = ((ang % (Math.PI * 2)) + Math.PI * 2) % (Math.PI * 2); const i = Math.floor(a / (Math.PI * 2) * NS); return cum[i] / per; };
    // the open arc: everything but the bottom `underOpen` radians either side of straight down
    const span = Math.PI - sp.underOpen;                     // half-arc, from the top
    const aTop = -Math.PI / 2, a0 = aTop - span, a1 = aTop + span;
    const t0 = tOf(a0), t1 = tOf(a1) < t0 ? tOf(a1) + 1 : tOf(a1);
    const frBase = Math.max(MIN_R * 1.9, cw * sp.flor);
    const arcLen = (t1 - t0) * per;
    const nP = clamp(Math.round(arcLen / (2.05 * frBase)), 2, Math.round(sp.nP * (0.55 + 0.45 * grow) + 0.49) + (grow > 0.9 ? 1 : 0));
    // one or two florets held back as GAPS on the sides — sky through the crown, never on top
    const gaps = {};
    if (nP >= 5) { const g = 1 + Math.floor(rng() * (nP - 2)); if (Math.abs(g - (nP - 1) / 2) > 0.8) gaps[g] = 1; }
    const targets = [];
    for (let k = 0; k < nP; k++) {
      const tt = t0 + (k + 0.5) / nP * (t1 - t0) + (rng() * 2 - 1) * 0.012;
      const a = angAt(tt);
      const shrink = gaps[k] ? 0.72 : 1;
      const fr = Math.max(MIN_R * 1.9, cw * sp.flor * (0.92 + rng() * 0.16)) * shrink;
      const ring = 0.86 + rng() * 0.10;
      const fx = cx + Math.cos(a) * (cw - fr * 0.55) * ring + (rng() * 2 - 1) * 2;
      const fy = cyc + Math.sin(a) * (ch - fr * 0.50) * ring;
      const fz = (rng() * 2 - 1) * cw * 0.28 - Math.sin(a) * cw * 0.06;
      targets.push({ x: fx, y: fy, z: fz, r: fr, a, gap: !!gaps[k] });
    }
    // primaries: fork → elbow → target, the species' own curve, radii from limbR × trunkR
    const [r0, r1, r2] = sp.limbR;
    const twigMat = sp.pale ? M.TWIG : M.BARK;
    const twigFan = (P, r, m) => {   // winter: a fan of twigs where a floret was, each forking once
      const n = 3 + (rng() < 0.5 ? 1 : 0);
      for (let q = 0; q < n; q++) {
        const aa = -Math.PI / 2 + (q - (n - 1) / 2) * 0.7 + (rng() * 2 - 1) * 0.2, L = r * (0.75 + rng() * 0.45);
        const ex = P[0] + Math.cos(aa) * L, ey = P[1] + Math.sin(aa) * L * 0.9, ez = P[2] + (rng() * 2 - 1) * r * 0.4;
        limbs.push([P[0], P[1], P[2], ex, ey, ez, 1.7, 0.8, M.TWIG, m]);
        const bx = P[0] + (ex - P[0]) * 0.5, by = P[1] + (ey - P[1]) * 0.5, bz = P[2] + (ez - P[2]) * 0.5;
        const ab = aa + (rng() < 0.5 ? -0.55 : 0.55), L2 = L * 0.5;
        limbs.push([bx, by, bz, bx + Math.cos(ab) * L2, by + Math.sin(ab) * L2 * 0.9, bz, 1.3, 0.8, M.TWIG, m]);
      }
    };
    for (let k = 0; k < targets.length; k++) {
      const T = targets[k], F = forks[k % forks.length];
      const mid = massN++;
      // the limb tip is buried a half-radius inside the floret core
      const tip = [T.x - Math.cos(T.a) * T.r * 0.45, T.y - Math.sin(T.a) * T.r * 0.40, T.z - T.r * 0.55];
      const C = elbow(F, tip, sp.curve, sp.kink, rng);
      limbs.push([F[0], F[1], F[2], C[0], C[1], C[2], trunkR * r0, trunkR * r1, M.BARK, mid]);
      limbs.push([C[0], C[1], C[2], tip[0], tip[1], tip[2], trunkR * r1, Math.max(1.2, trunkR * r2), twigMat, mid]);
      if (bare) twigFan(tip, T.r, mid); else floret(clumps, rng, T.x, T.y, T.z, T.r, mid);
      // secondaries. The first sits ON the ring at the midpoint to the next primary — a crown has
      // far more leaf masses than it has limbs — and the second sits inside the ring between them.
      // More wood in the gaps either way, and it is wood that goes somewhere.
      for (let s = 0; s < (sp.secs || 0); s++) {
        const nb = targets[(k + 1) % targets.length];
        const sm = massN++;
        const sr = Math.max(MIN_R * 1.6, T.r * (s ? 0.62 : 0.74));
        const ringK = s ? 0.55 : 0.94;
        const mxp = (T.x + nb.x) / 2, myp = (T.y + nb.y) / 2, mzp = (T.z + nb.z) / 2;
        const sx = cx + (mxp - cx) * ringK + (rng() * 2 - 1) * 3, sy = cyc + (myp - cyc) * ringK + (s ? sr * 0.2 : -sr * 0.1), sz = mzp * ringK + (s ? sr * 0.4 : -sr * 0.2);
        const sa = sp.secAt == null ? 0.4 : sp.secAt;   // where on the limb the secondary leaves: the elbow (maple) or further out
        const P = [C[0] + (tip[0] - C[0]) * sa, C[1] + (tip[1] - C[1]) * sa, C[2] + (tip[2] - C[2]) * sa];
        const sEnd = [sx, sy + sr * 0.3, sz - sr * 0.4];
        limbs.push([P[0], P[1], P[2], sEnd[0], sEnd[1], sEnd[2], trunkR * r1 * 0.8, Math.max(1.1, trunkR * r2 * 0.8), twigMat, sm]);
        if (bare) twigFan(sEnd, sr, sm); else floret(clumps, rng, sx, sy, sz, sr, sm);
      }
    }
    // hanging florets under the lowest side florets — the crown's skirt (droopy species: birch, oak)
    if (!bare && sp.hang) {
      const low = targets.filter(T => !T.gap && Math.sin(T.a) > 0.15).sort((A, B) => Math.sin(B.a) - Math.sin(A.a)).slice(0, sp.hang);
      for (const T of low) {
        const dm = massN++, dr = Math.max(MIN_R * 1.35, T.r * (0.52 + rng() * 0.14));
        const hang = T.r * (0.60 + rng() * 0.30 + droopF * 0.4), br = Math.max(MIN_R, T.r * 0.46);
        clumps.push({ x: T.x + (rng() * 2 - 1) * 2, y: T.y + hang * 0.50, z: T.z - dr * 0.12, rx: br * 1.02, ry: br * 0.92, rz: br, m: dm });
        floret(clumps, rng, T.x + (rng() * 2 - 1) * 3, T.y + hang, T.z - dr * 0.2, dr, dm);
      }
    }
    if (bare) return done();
    // crown dome: one forward mass above the ring centre so the top domes instead of ruling flat
    { const dm = massN++, fr = Math.max(MIN_R * 1.7, cw * sp.flor * 0.9);
      floret(clumps, rng, cx + (rng() * 2 - 1) * cw * 0.15, cyc - ch * 0.42, cw * 0.12, fr, dm); }
    // interior heart: deep in z, its own mass — keeps the crown one silhouette without closing the gaps
    const heartM = massN++;
    const fill = Math.max(4, Math.round(nP * (sp.fill || 1.6)));
    for (let i = 0; i < fill; i++) {
      const a = rng() * Math.PI * 2, rad = 0.16 + rng() * 0.34;
      const up = -0.55 + rng() * 0.75;
      const rr = Math.max(MIN_R, cw * (0.20 + rng() * 0.08));
      clumps.push({ x: cx + Math.cos(a) * cw * rad, y: cyc + up * ch * (0.35 + rng() * 0.35), z: cw * (0.02 + rng() * 0.32), rx: rr * 1.05, ry: rr * 0.9, rz: rr, m: heartM });
    }
    return done();
  }

  // ---- camera projection + measured cell fit ---------------------------------
  const pyRel = (g, y, z) => -(g.baseY - y) * CE + z * SE;
  const pzOf = (g, y, z) => z * CE + (g.baseY - y) * SE;
  const WOBBLE = 1.3;
  function extents(g) {
    let top = 1e9, bot = -1e9, xl = 1e9, xr = -1e9;
    const eM = g.eMax || 3;
    for (const c of g.clumps) {
      const y = pyRel(g, c.y, c.z), r = Math.hypot(c.ry * CE, c.rz * SE) * WOBBLE + eM, rx = c.rx * WOBBLE + eM;
      if (y - r < top) top = y - r; if (y + r > bot) bot = y + r;
      if (c.x - rx < xl) xl = c.x - rx; if (c.x + rx > xr) xr = c.x + rx;
    }
    for (const L of g.limbs) {
      const r = Math.max(L[6], L[7]) + 1;
      for (const e of [[L[0], L[1], L[2]], [L[3], L[4], L[5]]]) {
        const y = pyRel(g, e[1], e[2]);
        if (y - r < top) top = y - r; if (y + r > bot) bot = y + r;
        if (e[0] - r < xl) xl = e[0] - r; if (e[0] + r > xr) xr = e[0] + r;
      }
    }
    if (top > bot) { top = -1; bot = 1; xl = 0; xr = 1; }
    return { top, bot, xl, xr };
  }
  // One cell per species per size, unioned over every variant × summer/winter — a sheet stays a grid
  function cellOf(sp, size) {
    const t = size == null ? 1 : size, ck = 'c' + Math.round(t * 1000);
    if (!sp._cells) sp._cells = {};
    if (sp._cells[ck]) return sp._cells[ck];
    const MG = 3;
    let top = 1e9, bot = -1e9, xl = 1e9, xr = -1e9;
    for (const season of ['summer', 'winter']) for (let v = 0; v < VARIANTS; v++) {
      const e = extents(build(sp, v, season, t));
      if (e.top < top) top = e.top; if (e.bot > bot) bot = e.bot;
      if (e.xl < xl) xl = e.xl; if (e.xr > xr) xr = e.xr;
    }
    const pivotY = Math.ceil(MG - top);
    const h = Math.ceil(pivotY + bot + MG) + 1;
    const dx = Math.max(0, Math.ceil(MG - xl));
    const wCell = Math.max(24, dx + Math.ceil(xr + MG) + 1);
    const cell = { w: wCell, h, dx, pivotX: Math.round(dx + (xl + xr) / 2), pivotY, pad: h - 1 - pivotY, size: t };
    sp._cells[ck] = cell;
    return cell;
  }

  // ---- RULE 2: de-speckle, tooth-aware ------------------------------------------
  function despeckle(v) {
    let removed = 0;
    for (let pass = 0; pass < 2; pass++) {
      const kill = [];
      for (let y = 0; y < v.h; y++) for (let x = 0; x < v.w; x++) {
        const i = y * v.w + x; if (!v.a[i]) continue;
        let n = 0, n8 = 0;
        if (x > 0 && v.a[i - 1]) n++;
        if (x < v.w - 1 && v.a[i + 1]) n++;
        if (y > 0 && v.a[i - v.w]) n++;
        if (y < v.h - 1 && v.a[i + v.w]) n++;
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
          if (!dx && !dy) continue;
          const jx = x + dx, jy = y + dy;
          if (jx < 0 || jy < 0 || jx >= v.w || jy >= v.h) continue;
          if (v.a[jy * v.w + jx]) n8++;
        }
        if (n === 0 || (n === 1 && n8 <= 2)) kill.push(i);
      }
      for (const i of kill) { v.clearPx(i); removed++; }
      if (!kill.length) break;
    }
    for (let y = 1; y < v.h - 1; y++) for (let x = 1; x < v.w - 1; x++) {
      const i = y * v.w + x; if (v.a[i]) continue;
      if (v.a[i - 1] && v.a[i + 1] && v.a[i - v.w] && v.a[i + v.w]) {
        const src = v.z[i - 1] > v.z[i + 1] ? i - 1 : i + 1;
        v.a[i] = 1; v.mat[i] = v.mat[src]; v.z[i] = v.z[src] - 0.2; v.id[i] = v.id[src]; v.mid[i] = v.mid[src];
        v.nx[i] = v.nx[src]; v.ny[i] = v.ny[src]; v.nz[i] = v.nz[src];
      }
    }
    return removed;
  }
  // chamfer distance-to-edge (3-4), in px
  function distField(a, w, h) {
    const D = new Float32Array(w * h), BIG = 1e6;
    for (let i = 0; i < w * h; i++) D[i] = a[i] ? BIG : 0;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!D[i]) continue; let d = D[i];
      if (y > 0) { d = Math.min(d, D[i - w] + 3); if (x > 0) d = Math.min(d, D[i - w - 1] + 4); if (x < w - 1) d = Math.min(d, D[i - w + 1] + 4); }
      if (x > 0) d = Math.min(d, D[i - 1] + 3);
      D[i] = d;
    }
    for (let y = h - 1; y >= 0; y--) for (let x = w - 1; x >= 0; x--) {
      const i = y * w + x; if (!D[i]) continue; let d = D[i];
      if (y < h - 1) { d = Math.min(d, D[i + w] + 3); if (x > 0) d = Math.min(d, D[i + w - 1] + 4); if (x < w - 1) d = Math.min(d, D[i + w + 1] + 4); }
      if (x < w - 1) d = Math.min(d, D[i + 1] + 3);
      D[i] = d;
    }
    for (let i = 0; i < w * h; i++) D[i] /= 3;
    return D;
  }
  // ---- RULE 1 audit. Connectivity runs through wood (a bough crossed by the trunk is one mass),
  // but only components that CARRY FOLIAGE are held to the body rule — bare wood is allowed thin.
  function massReport(a, D, w, h, mat) {
    const lab = new Int32Array(w * h).fill(-1), stack = [], comps = [];
    const on = (i) => a[i];
    for (let s = 0; s < w * h; s++) {
      if (!on(s) || lab[s] >= 0) continue;
      const id = comps.length; let px = 0, maxd = 0, fol = 0;
      lab[s] = id; stack.push(s);
      while (stack.length) {
        const i = stack.pop(); px++; if (mat && mat[i] === M.FOLIAGE) { fol++; if (D[i] > maxd) maxd = D[i]; }
        const x = i % w, y = (i / w) | 0;
        if (x > 0 && on(i - 1) && lab[i - 1] < 0) { lab[i - 1] = id; stack.push(i - 1); }
        if (x < w - 1 && on(i + 1) && lab[i + 1] < 0) { lab[i + 1] = id; stack.push(i + 1); }
        if (y > 0 && on(i - w) && lab[i - w] < 0) { lab[i - w] = id; stack.push(i - w); }
        if (y < h - 1 && on(i + w) && lab[i + w] < 0) { lab[i + w] = id; stack.push(i + w); }
      }
      comps.push({ id, px, fol, maxd, body: Math.max(0, (maxd - RIM_PX) * 2) });
    }
    const real = comps.filter(c => c.fol >= 6), fail = real.filter(c => c.body < MIN_BODY);
    let bodyPx = 0, total = 0;
    for (let i = 0; i < w * h; i++) if (on(i) && mat[i] === M.FOLIAGE) { total++; if (D[i] > RIM_PX) bodyPx++; }
    return { masses: real.length, failed: fail.length, pass: fail.length === 0,
      minBody: real.length ? Math.round(Math.min.apply(null, real.map(c => c.body)) * 10) / 10 : 0,
      bodyRatio: total ? Math.round(bodyPx / total * 100) : 0, lab, comps };
  }

  // ---- shade -----------------------------------------------------------------
  const STEPS = [[0.05, 'dp'], [0.145, 'sh'], [0.31, 'mid'], [0.56, 'hi'], [1.4, 'key']];
  const BANDS = ['dp', 'sh', 'mid', 'hi', 'key'];
  function bandOf(l) { for (let i = 0; i < STEPS.length; i++) if (l < STEPS[i][0]) return STEPS[i][1]; return 'key'; }
  function bandIdx(l) { for (let i = 0; i < STEPS.length; i++) if (l < STEPS[i][0]) return i; return 4; }
  function vnoise(x, y, s) { const n = Math.sin(x * 127.1 + y * 311.7 + s * 74.7) * 43758.5453; return n - Math.floor(n); }
  function snoise(x, y, s) {
    const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi;
    const u = fx * fx * (3 - 2 * fx), vv = fy * fy * (3 - 2 * fy);
    const a = vnoise(xi, yi, s), b = vnoise(xi + 1, yi, s), c = vnoise(xi, yi + 1, s), d = vnoise(xi + 1, yi + 1, s);
    return a + (b - a) * u + (c - a) * vv + (a - b - c + d) * u * vv;
  }

  // LEAF STAMPS. Sites on a rotated, jittered, domain-warped lattice; each site takes one authored
  // stencil of the species' grain and paints it onto the foliage of ONE mass (the mass under its
  // centre), lower sites over upper ones. id 0 = no stamp = the dark between leaves.
  const NEAR = [[0, 0], [1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1]];
  function stampField(v, s, G) {
    const w = v.w, h = v.h, LW = G.w, LH = G.h, jit = G.jit, ca = Math.cos(G.rot), sa = Math.sin(G.rot);
    let mnx = 1e9, mxx = -1e9, mny = 1e9, mxy = -1e9;
    for (const c of [[0, 0], [w, 0], [0, h], [w, h]]) {
      const rx = c[0] * ca + c[1] * sa, ry = -c[0] * sa + c[1] * ca;
      if (rx < mnx) mnx = rx; if (rx > mxx) mxx = rx; if (ry < mny) mny = ry; if (ry > mxy) mxy = ry;
    }
    const pad = G.warp + 6, ox = mnx - pad, oy = mny - pad;
    const gw = Math.ceil((mxx + pad - ox) / LW) + 1, gh = Math.ceil((mxy + pad - oy) / LH) + 1;
    const sites = [];
    for (let j = 0; j < gh; j++) for (let i = 0; i < gw; i++) {
      const rx = ox + (i + 0.5 + jit * (vnoise(i * 3 + 1, j * 5 + 2, s) - 0.5)) * LW;
      const ry = oy + (j + 0.5 + jit * (vnoise(i * 7 + 53, j * 11 + 17, s + 3) - 0.5)) * LH;
      let x = rx * ca - ry * sa, y = rx * sa + ry * ca;
      x += (snoise(x / 9.3, y / 7.1, s + 11) - 0.5) * 2 * G.warp;
      y += (snoise(x / 7.7, y / 9.9, s + 29) - 0.5) * 2 * G.warp;
      const xi = Math.round(x), yi = Math.round(y);
      if (xi < -6 || yi < -6 || xi >= w + 6 || yi >= h + 6) continue;
      sites.push({ x: xi, y: yi, k: i * 7919 + j * 104729 });
    }
    sites.sort((A, B) => A.y - B.y || A.x - B.x);
    const id = new Int32Array(w * h), shapes = G.shapes;
    let n = 0;
    for (const S of sites) {
      let mid = -1;
      for (const [dx, dy] of NEAR) {
        const jx = S.x + dx, jy = S.y + dy; if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue;
        const j = jy * w + jx; if (v.a[j] && v.mat[j] === M.FOLIAGE) { mid = v.mid[j]; break; }
      }
      if (mid < 0) continue;
      const sh = shapes[Math.floor(vnoise(S.k & 65535, S.k >>> 16, s + 7) * shapes.length) % shapes.length];
      const sid = ++n;
      for (const p of sh.px) {
        const jx = S.x + p[0], jy = S.y + p[1]; if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue;
        const j = jy * w + jx; if (v.a[j] && v.mat[j] === M.FOLIAGE && v.mid[j] === mid) id[j] = sid;
      }
    }
    return { id, n };
  }

  function shade(v, sp, season, D, opts) {
    const w = v.w, h = v.h, n = w * h;
    const FOL = folRamp(sp.fol, season, sp.fall), BARK = barkRamp(sp.bark, sp.pale);
    const BARKB = {};
    for (const k of ['dp', 'sh', 'mid', 'hi', 'key', 'rim']) BARKB[k] = mix(BARK[k], FOL.dp, k === 'rim' ? 0.40 : 0.74);
    const rgba = new Uint8ClampedArray(n * 4);
    const mFront = new Uint8Array(n), mRim = new Uint8Array(n), mDepth = new Uint8Array(n);
    let zmin = 1e9, zmax = -1e9;
    for (let i = 0; i < n; i++) if (v.a[i]) { if (v.z[i] < zmin) zmin = v.z[i]; if (v.z[i] > zmax) zmax = v.z[i]; }
    const zr = Math.max(1, zmax - zmin);
    const sd = (sp.key.length * 13 + Math.round(sp.real * 7)) % 97;
    const K = LIGHT.key, R = LIGHT.rim;
    const RSl = Math.hypot(R[0], R[1]) || 1, RS = [R[0] / RSl, R[1] / RSl];
    const snowy = season === 'winter' && v.conifer !== false;

    // screen-space occlusion — carves the crevices between masses apart
    const OCC = new Float32Array(n);
    const ring = [[2, 0], [1, 1], [0, 2], [-1, 1], [-2, 0], [-1, -1], [0, -2], [1, -1], [3, 1], [-3, 1], [1, 3], [-1, 3]];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i]) continue;
      let occ = 0;
      for (const [dx, dy] of ring) {
        const jx = x + dx, jy = y + dy;
        if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue;
        const j = jy * w + jx; if (!v.a[j]) continue;
        const dz = v.z[j] - v.z[i];
        if (dz > 0.8) occ += clamp(dz / 5, 0, 1);
      }
      OCC[i] = clamp(1 - occ / ring.length * 2.1, 0.22, 1);
    }
    // local mass thickness — RULE 3 gates the rim on this
    const TH = new Float32Array(n), tmp = new Float32Array(n), RAD = 6;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      let m = 0; for (let k = -RAD; k <= RAD; k++) { const jx = x + k; if (jx < 0 || jx >= w) continue; const d = D[y * w + jx]; if (d > m) m = d; }
      tmp[y * w + x] = m;
    }
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      let m = 0; for (let k = -RAD; k <= RAD; k++) { const jy = y + k; if (jy < 0 || jy >= h) continue; const d = tmp[jy * w + x]; if (d > m) m = d; }
      TH[y * w + x] = m;
    }
    let thin = 0, tot = 0;
    for (let i = 0; i < n; i++) if (v.a[i] && v.mat[i] === M.FOLIAGE) { tot++; if (TH[i] * 2 < MIN_BODY + 2 * RIM_PX) thin++; }

    // ---- pass A: per-pixel lighting, accumulated per STAMP ----------------------
    // The cavity term is scaled to THIS sprite: pass 2 fell off over a fixed 6.5 px from the
    // silhouette, which on a crown twice the size put every interior pixel on the AO floor and
    // sent 75% of the foliage into the two shadow bands — no light, so no leaves to draw.
    let dMax = 0;
    for (let i = 0; i < n; i++) if (v.a[i] && v.mat[i] === M.FOLIAGE && D[i] > dMax) dMax = D[i];
    const aoLen = Math.max(6.5, dMax * 0.7);
    const G = grainOf(sp);
    const SF = stampField(v, sd, G);
    const unit = new Int32Array(n).fill(-1);
    const lum0 = new Float32Array(n);
    const uSum = new Map();
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i]) continue;
      const nx = v.nx[i], ny = v.ny[i], nz = v.nz[i], d = D[i];
      const lam = Math.pow(Math.max(0, nx * K[0] + ny * K[1] + nz * K[2]), 1.35);
      const ao = clamp(0.50 + 0.50 * Math.exp(-Math.max(0, d - 1) / aoLen), 0.50, 1) * OCC[i];
      const zf = 0.62 + 0.38 * ((v.z[i] - zmin) / zr);
      const sky = 0.45 + 0.55 * clamp(-ny, 0, 1);
      lum0[i] = 0.135 * sky * ao * zf + 1.22 * lam * ao * zf;
      mFront[i] = clamp(Math.round(lam * 255), 0, 255);
      mDepth[i] = clamp(Math.round(((v.z[i] - zmin) / zr) * 255), 0, 255);
      if (v.mat[i] !== M.FOLIAGE || !SF.id[i]) continue;
      const u = SF.id[i];
      unit[i] = u;
      const kw = x + y;
      const e = uSum.get(u);
      if (e) { e.s += lum0[i]; e.c++; if (kw < e.kw) { e.kw = kw; e.tip = i; } }
      else uSum.set(u, { s: lum0[i], c: 1, kw, tip: i });
    }
    const uLum = new Map(), uTip = new Map();
    for (const [u, e] of uSum) { uLum.set(u, e.s / e.c); uTip.set(u, e.tip); }

    // wood buried in the crown drops into the leaves' own shade
    const BRING = [[4, 0], [3, 3], [0, 4], [-3, 3], [-4, 0], [-3, -3], [0, -4], [3, -3],
                   [7, 0], [5, 5], [0, 7], [-5, 5], [-7, 0], [-5, -5], [0, -7], [5, -5]];
    const BURY = new Uint8Array(n);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i] || v.mat[i] === M.FOLIAGE) continue;
      let f = 0, t = 0;
      for (const [dx, dy] of BRING) {
        const jx = x + dx, jy = y + dy;
        if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue;
        t++; const j = jy * w + jx;
        if (v.a[j] && v.mat[j] === M.FOLIAGE) f++;
      }
      BURY[i] = !t ? 0 : f * 2 >= t ? 2 : f * 3 >= t ? 1 : 0;
    }

    // ---- pass B: quantise, draw the edges — everything in RAMP STEPS ------------
    const BG = sp.barkGrain || 'plate';
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x;
      if (!v.a[i]) { rgba[i * 4 + 3] = 0; continue; }
      const nx = v.nx[i], ny = v.ny[i], nz = v.nz[i], d = D[i];
      const nl = Math.hypot(nx, ny) || 1;
      const back = Math.max(0, (nx * RS[0] + ny * RS[1]) / nl);
      const fres = Math.pow(1 - clamp(nz, 0, 1), 1.7);
      const thick = smooth(4.0, 5.2, TH[i]);
      let rim = Math.pow(back, 1.15) * fres * thick * smooth(3.6, 0.8, d);

      let lum, ramp, band = null;
      if (v.mat[i] === M.FOLIAGE) {
        const u = unit[i];
        const mB = (vnoise((v.mid[i] & 511) * 5 + 3, (v.mid[i] & 511) * 9 + 7, 29) - 0.5) * 0.06;
        let bi;
        const rt = x < w - 1 ? i + 1 : -1, dn = y < h - 1 ? i + w : -1;
        const up = y > 0 ? i - w : -1, lf = x > 0 ? i - 1 : -1;
        if (u >= 0) {
          bi = bandIdx(uLum.get(u) + mB);
          const hj = vnoise(u & 8191, (u >>> 13) & 8191, 19);
          if (G.flip && hj > 1 - G.flip) bi += 2;
          else if (hj < G.tone) bi -= 1;
          else if (hj > 1 - G.tone) bi += 1;
          bi = bi < 0 ? 0 : bi > 4 ? 4 : bi;
          const base = bi;
          const uR = rt >= 0 && v.a[rt] && v.mat[rt] === M.FOLIAGE ? unit[rt] : -2;
          const uD = dn >= 0 && v.a[dn] && v.mat[dn] === M.FOLIAGE ? unit[dn] : -2;
          // 1 · the stamp's down/right SEAM — the next leaf's authored top contour, one step darker.
          //     Detail only in the light: a stamp already in shade draws no seam.
          if (base >= 2 && ((uR !== -2 && uR !== u) || (uD !== -2 && uD !== u))) bi -= (G.corner && base >= 3 && uR !== u && uD !== u) ? 2 : 1;
          // 1b · the stamp's key-ward tip — one bright pixel per leaf
          else if (base >= 3 && uTip.get(u) === i) bi += 1;
        } else {
          // no stamp reached here: the dark between the leaves
          bi = bandIdx(lum0[i] + mB) - 1;
        }
        // 2 · clump crevice / lip
        if (dn >= 0 && v.a[dn] && v.id[dn] !== v.id[i] && v.z[dn] > v.z[i] + 1.0) bi -= 1;
        else if (rt >= 0 && v.a[rt] && v.id[rt] !== v.id[i] && v.z[rt] > v.z[i] + 1.0) bi -= 1;
        if (up >= 0 && v.a[up] && v.id[up] !== v.id[i] && v.z[up] < v.z[i] - 1.0) bi += 1;
        else if (lf >= 0 && v.a[lf] && v.id[lf] !== v.id[i] && v.z[lf] < v.z[i] - 1.0) bi += 1;
        // 3 · floret / bough boundary
        const mc = v.conifer ? 2 : 1;
        const mDn = dn >= 0 && v.a[dn] && v.mid[dn] !== v.mid[i];
        const mRt = !mDn && rt >= 0 && v.a[rt] && v.mid[rt] !== v.mid[i];
        if (mDn || mRt) {
          const zn = mDn ? v.z[dn] : v.z[rt];
          bi -= zn > v.z[i] + 0.6 ? mc : (v.conifer ? 1 : 0);
        }
        bi = bi < 0 ? 0 : bi > 4 ? 4 : bi;
        band = BANDS[bi];
        lum = u >= 0 ? uLum.get(u) : lum0[i];
        ramp = FOL;
      } else {
        let bi = bandIdx(lum0[i] - (sp.pale && v.mat[i] === M.TWIG ? 0.30 : 0));
        bi -= BURY[i];
        if (TH[i] >= 3.2) {
          // BARK GRAIN, per species. Steps, never noise: furrows are 2 px ridges, plates are 3 px
          // columns broken every ~14 rows, scales a 2×3 checker, shreds long thin stripes, smooth bark
          // is flat with knots, paper (birch) carries lenticel dashes.
          if (BG === 'furrow') { const sn = vnoise(Math.floor((x + Math.floor(y / 11)) / 2), 3, sd + 11); if (sn > 0.62) bi += 1; else if (sn < 0.30) bi -= 1; }
          else if (BG === 'plate') { const col = Math.floor((x + Math.floor(y / 14)) / 3), sn = vnoise(col, Math.floor(y / 14), sd + 11); if (sn > 0.78) bi += 1; else if (sn < 0.22) bi -= 1; if (((y + col * 5) % 14) === 0 && vnoise(col, 9, sd) > 0.4) bi -= 1; }
          else if (BG === 'scale') { const sn = vnoise(Math.floor(x / 2), Math.floor(y / 3), sd + 11); if (sn > 0.80) bi += 1; else if (sn < 0.24) bi -= 1; }
          else if (BG === 'shred') { const sn = vnoise(Math.floor(x / 1.5), Math.floor(y / 22), sd + 11); if (sn > 0.84) bi += 1; else if (sn < 0.18) bi -= 1; }
          else if (BG === 'smooth') { if (vnoise(Math.floor(x / 6), Math.floor(y / 9), sd + 11) > 0.93 && (x % 6) < 2 && (y % 9) < 2) bi -= 2; }
          if (d < 1.7 && nx > 0.20) bi -= 1;                        // shade side of the bole
          else if (d < 1.7 && nx < -0.30) bi += 1;                  // lit side catches the key
        }
        if (x < w - 1 && v.a[i + 1] && v.mid[i + 1] !== v.mid[i] && v.z[i + 1] > v.z[i] + 0.4) bi -= 1;
        if (y < h - 1 && v.a[i + w] && v.mid[i + w] !== v.mid[i] && v.z[i + w] > v.z[i] + 0.4) bi -= 1;
        if (BG === 'paper' && v.mat[i] === M.BARK && !BURY[i]) {
          const bandY = Math.floor(y / 5), seg = Math.floor((x + bandY * 3) / 7);
          if (vnoise(seg, bandY, sd + 23) > 0.74 && (y % 5) < 2) bi -= 2;
        }
        bi = bi < 0 ? 0 : bi > 4 ? 4 : bi;
        band = BANDS[bi];
        lum = lum0[i];
        ramp = BURY[i] ? BARKB : BARK;
      }
      if (snowy && -ny > 0.42 && d > 0.9 && lum > 0.10) { ramp = SNOW; band = null; lum = 0.34 + lum * 0.7; }
      let hex = ramp[band || bandOf(clamp(lum, 0, 1.2))];
      if (rim > 0.16) hex = mix(hex, ramp.rim, clamp((rim - 0.12) * 1.35, 0, 0.95));
      const c = h2r(hex);
      rgba[i * 4] = c[0]; rgba[i * 4 + 1] = c[1]; rgba[i * 4 + 2] = c[2]; rgba[i * 4 + 3] = 255;
      mRim[i] = clamp(Math.round(rim * 255), 0, 255);
    }

    // keyline — ADR 0031: off by default, {outline:true} is the live A/B
    const outline = opts.outline === true || (opts.outline == null && KEYLINE_DEFAULT);
    if (outline) {
      const kl = h2r(KEYLINE), add = [];
      for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = y * w + x; if (v.a[i]) continue;
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
          const jx = x + dx, jy = y + dy; if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue;
          if (v.a[jy * w + jx]) { add.push(i); dx = 2; dy = 2; }
        }
      }
      for (const i of add) { rgba[i * 4] = kl[0]; rgba[i * 4 + 1] = kl[1]; rgba[i * 4 + 2] = kl[2]; rgba[i * 4 + 3] = 255; }
    }
    return { rgba, masks: { front: mFront, rim: mRim, depth: mDepth }, thin, tot, TH, unit, stamps: SF.n, outline };
  }

  // ---- sway: per-scanline shear pinned at the trunk pivot --------------------
  function swayShear(buffers, w, h, baseY, frame, amp) {
    if (frame == null || !frame) return buffers;
    const curve = Math.sin(frame / SWAY * Math.PI * 2);
    const out = buffers.map(b => new b.constructor(b.length));
    const stride = buffers.map(b => b.length / (w * h));
    for (let y = 0; y < h; y++) {
      const hf = clamp((baseY - y) / Math.max(1, baseY), 0, 1);
      const dx = Math.round(amp * curve * Math.pow(hf, 1.6));
      for (let x = 0; x < w; x++) {
        const sx = x - dx; if (sx < 0 || sx >= w) continue;
        for (let b = 0; b < buffers.length; b++) {
          const st = stride[b], si = (y * w + sx) * st, di = (y * w + x) * st;
          for (let c = 0; c < st; c++) out[b][di + c] = buffers[b][si + c];
        }
      }
    }
    return out;
  }

  // ---- public ---------------------------------------------------------------
  function render(key, o) {
    o = o || {};
    const sp = byKey[key] || SPECIES[0];
    const season = SEASONS.indexOf(o.season) >= 0 ? o.season : 'summer';
    const size = sizeOf(o);
    const variant = ((o.variant | 0) % VARIANTS + VARIANTS) % VARIANTS;
    const g = build(sp, variant, season, size);
    const cell = cellOf(sp, size);
    const v = new Vol(cell.w, cell.h);
    v.conifer = g.conifer;
    const pivot = { x: cell.pivotX, y: cell.pivotY };
    const py = (y, z) => pivot.y + pyRel(g, y, z);
    const pz = (y, z) => pzOf(g, y, z);
    const px = (x) => x + cell.dx;
    let id = 0;
    const E = edgeOf(sp);
    for (const L of g.limbs) limb(v, px(L[0]), py(L[1], L[2]), pz(L[1], L[2]), px(L[3]), py(L[4], L[5]), pz(L[4], L[5]), L[6], L[7], L[8], id++, L[9] == null ? 900 : L[9]);
    for (const c of g.clumps) blob(v, px(c.x), py(c.y, c.z), pz(c.y, c.z), c.rx, Math.hypot(c.ry * CE, c.rz * SE), Math.hypot(c.ry * SE, c.rz * CE), M.FOLIAGE, id, c.m == null ? 900 : c.m, id++, E);

    const despeckled = despeckle(v);
    const D = distField(v.a, v.w, v.h);
    const audit = massReport(v.a, D, v.w, v.h, v.mat);
    const sh = shade(v, sp, season, D, o);
    let rgba = sh.rgba, mf = sh.masks.front, mr = sh.masks.rim, md = sh.masks.depth;
    if (o.frame) {
      const out = swayShear([rgba, mf, mr, md], v.w, v.h, pivot.y, o.frame, sp.sway * Math.max(0.6, g.H / 180));
      rgba = out[0]; mf = out[1]; mr = out[2]; md = out[3];
    }
    return {
      w: v.w, h: v.h, pivot, rgba, masks: { front: mf, rim: mr, depth: md },
      alpha: v.a, dist: D, mat: v.mat, nx: v.nx, ny: v.ny, nz: v.nz, mid: v.mid, unit: sh.unit,
      clumps: g.clumps.length, limbs: g.limbs.length, species: sp, season, variant, size, stage: stageName(size), outline: sh.outline,
      report: {
        pass: audit.pass && sh.thin / Math.max(1, sh.tot) <= 0.04, masses: audit.masses, failed: audit.failed,
        foliagePx: sh.tot, florets: g.masses, stamps: sh.stamps,
        leafCells: sh.stamps,
        minBody: audit.minBody, bodyRatio: audit.bodyRatio, despeckled,
        thinPct: Math.round(sh.thin / Math.max(1, sh.tot) * 1000) / 10,
        metres: Math.round(sp.worldH * size / PPU * 10) / 10,
        trueMetres: Math.round(sp.real * size * 10) / 10,
        trunkPx: Math.round(g.trunkR * 20) / 10,
        underFloor: sp.worldH * size < 34,
      },
      thick: sh.TH,
      _audit: audit,
    };
  }

  function leafView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      if (res.unit[i] < 0) { const fol = res.mat[i] === M.FOLIAGE; out[i * 4] = fol ? 18 : 62; out[i * 4 + 1] = fol ? 22 : 74; out[i * 4 + 2] = fol ? 26 : 82; out[i * 4 + 3] = 255; continue; }
      const u = res.unit[i];
      out[i * 4] = 60 + 190 * vnoise(u & 8191, 1, 5);
      out[i * 4 + 1] = 60 + 190 * vnoise(u & 8191, 2, 9);
      out[i * 4 + 2] = 60 + 190 * vnoise(u & 8191, 3, 13);
      out[i * 4 + 3] = 255;
    }
    return out;
  }
  function massIdView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      const m = res.mid[i] & 511;
      out[i * 4] = 50 + 200 * vnoise(m * 3 + 1, 7, 3);
      out[i * 4 + 1] = 50 + 200 * vnoise(m * 3 + 2, 11, 6);
      out[i * 4 + 2] = 50 + 200 * vnoise(m * 3 + 3, 13, 9);
      out[i * 4 + 3] = 255;
    }
    return out;
  }
  // wood view: every pixel of bark / twig in its own flat tone, foliage ghosted — the skeleton read
  function woodView(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      const m = res.mat[i];
      const c = m === M.BARK ? [214, 168, 104] : m === M.TWIG ? [232, 208, 160] : [34, 52, 48];
      out[i * 4] = c[0]; out[i * 4 + 1] = c[1]; out[i * 4 + 2] = c[2]; out[i * 4 + 3] = 255;
    }
    return out;
  }
  function packMask(res) {
    const n = res.w * res.h, out = new Uint8ClampedArray(n * 4);
    for (let i = 0; i < n; i++) {
      const a = res.rgba[i * 4 + 3];
      out[i * 4] = res.masks.front[i]; out[i * 4 + 1] = res.masks.rim[i];
      out[i * 4 + 2] = res.masks.depth[i]; out[i * 4 + 3] = a;
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
    const RIMC = h2r('#e8b06a'), BODY = h2r('#2f6a4c'), BAD = h2r('#d2453c'), CORE = h2r('#8fd6a0'), WOOD = h2r('#3f4f57');
    for (let i = 0; i < n; i++) {
      if (!res.alpha[i]) { out[i * 4 + 3] = 0; continue; }
      const d = res.dist[i], fol = res.mat[i] === M.FOLIAGE;
      const thinHere = fol && res.thick[i] * 2 < MIN_BODY + 2 * RIM_PX;
      const c = !fol ? WOOD : thinHere ? BAD : d <= RIM_PX ? RIMC : d >= RIM_PX + MIN_BODY / 2 ? CORE : BODY;
      out[i * 4] = c[0]; out[i * 4 + 1] = c[1]; out[i * 4 + 2] = c[2]; out[i * 4 + 3] = 255;
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
  function sheetSpec(key, size) {
    const sp = byKey[key] || SPECIES[0], t = size == null ? 1 : size, cell = cellOf(sp, t);
    const w = cell.w * VARIANTS, h = cell.h * SWAY;
    return { cell: [cell.w, cell.h], cols: VARIANTS, rows: SWAY, w, h, fits: w <= 2048 && h <= 2048, ppu: PPU, scale: SCALE,
      pivot: [cell.pivotX, cell.pivotY], pad: cell.pad, elev: ELEV, stage: stageName(t),
      metres: Math.round(sp.worldH * t / PPU * 10) / 10, trueMetres: Math.round(sp.real * t * 10) / 10 };
  }

  root.TreeRig3 = {
    PPU, SCALE, M2PX, RIM_PX, MIN_BODY, MIN_R, SWAY, VARIANTS, SEASONS, SPECIES, byKey, LIGHT, KEYLINE, KEYLINE_DEFAULT, COLD, WARM, ELEV, CE, SE,
    STAGES, STAGE_KEYS, sizeOf, stageName,
    render, packMask, grey, massView, normalView, leafView, massIdView, woodView, sheetSpec, cellOf, folRamp, barkRamp,
    M, GRAINS, grainOf, EDGES, edgeOf, STENCILS,
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
