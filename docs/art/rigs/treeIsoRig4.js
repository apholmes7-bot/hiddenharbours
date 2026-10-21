/* Hidden Harbours — TREE RIG, PASS 4 (weather-lit · G-buffer light maps · mass-anchored wind · cast shadows).
   Pass 3 got the architecture right and baked the light into it: every sprite lit once from the
   authored upper-left key, masks that were N·L at that key, a 4-frame scanline shear for wind. A tree
   could not follow the hour, a cloud or a gust; the harmony relight had to cap sprites at ±1 band
   because the albedo already carried noon. What pass 4 found, and what it does:

     A. LIGHT IS NOT BAKED.  The rig emits a G-buffer per pixel — material + structure, a STAMP-FLAT
        normal, sky visibility, view depth, height above ground, stamp id, part id — and lights it on
        call from a sky (WeatherSky.at, or any object with its fields). A stamp still takes ONE band
        (its pixels share one normal), so a live sun stays pixel art.
     B. BIG FORM FIRST.  Pass 3 lit every floret from its own normal and the crown read as even popcorn
        with no light side. Normals are blended leaf → floret → crown envelope (ellipsoid for
        broadleaves, cone round the stem for conifers), so a crown has a lit shoulder and a shade side
        before it has leaves.
     C. VOLUMETRIC SHADE.  Sky visibility and sun transmission come from opacity maps over the real
        clump ellipsoids and limbs (foliage σ per species, wood near-opaque): the interior, the lower
        crown under the upper, a spruce tier under the one above, the bole under an oak. The same map
        throws the CAST SHADOW, pixel-exact, sun flecks where the crown is open, and a canopy shade
        that stays under overcast.
     D. BACK LIGHT.  Sun behind a tree: thin foliage transmits (species trans) and the silhouette takes
        a sun rim — the only rim now. Pass 3's fixed warm rim on every crown's upper right is gone.
     E. WIND.  A displacement field — lean ∝ w² and sway on the trunk (h^1.8), limb sway and bob on
        one wave that crosses the crown downwind — sampled at each mass (its between-leaves base moves
        rigidly), at each LEAF (so a crown bends leaf by leaf, never a floret at a time) and at each
        limb end (wood re-rasterised bent; limbs split so a bend is a curve). Periodic in LOOP = 16.
     F. FLUTTER.  (4.1) The flutter unit was a whole mass: florets jumped a pixel together. It is now
        the leaf, and leaves are about half the size (4–6 px broadleaf, 3–4 px aspen). Each flutters on
        its own 1–3-per-loop cycle — a 1 px push downwind, a lift, a recoil — more of them where the
        gust is passing. Deciduous leaves also turn over to a paler underside (lit ones glint); the
        aspen trembles in air nothing else moves in.
     G. SHAPES.  Florets domed on top and flat underneath (no rosette of equal satellites); the white
        pine carries flat layered sprays that sweep up at the tip, not pom-poms; birch and aspen crowns
        close into one oval; broadleaf limbs are curves through their elbow; birch bark warm white.
     H. WEATHER.  cloud · rain (wet bark a band darker, glints on lit tips) · fog (ground-heavy haze) ·
        snow cover (settles on up-facing stamps and limb tops, any season) · wind.

     I. PIVOT.  (4.1) The pivot is the trunk-foot column in every cell. It had been the centre of the
        crown's silhouette, 1–5 px off the trunk; a collider or a terrain anchor at the pivot missed it.

   Unchanged: the three rules (mass floor · authored silhouette · thickness-gated rim), PPU 32 ·
   ¾ from S at 40° · trunk-foot pivot · no AA · binary alpha · ringless · true heights × SCALE 0.6.

   globalThis.TreeRig4
     model(key, o)             geometry + parts + sky visibility, cached     o = {variant, season, stage|size}
     frame(key, o)             one wind pose                                 o.wind = {w, gust, dir}, o.frame 0–15
     relight(fr, sky)          -> RGBA                                       sky = WeatherSky.at(...) | REF_SKY
     castShadow(fr, sky)       -> {x0, y0, w, h, lv}   levels 1 canopy · 2 partial · 3 full, cell coords
     view(fr, ch, sky)         -> RGBA   lit unlit normal ao trans height depth sun stamps parts wood light detail
     render(key, o)            the pass-3 surface: lit at REF_SKY (or o.sky) + masks + rule report
     sheet(key, o, ch, sky)    the 16-frame wind loop as a 4 × 4 sheet
     sheetSpec · cellOf · windAt · SPECIES · LOOP · REF_SKY · UNLIT_SKY                                  */
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
    // 4.1: every leaf-bearing grain at about half its pass-4 size (needle and fir combs were already fine)
    pineTuft: [['o.o.o', '.ooo.', '.ooo.', 'ooooo'], ['o..o.o', '.o.oo.', '..ooo.', 'oooooo'], ['.o.o', 'o.o.', '.oo.', 'oooo']],
    scale:    [['.o.o.', 'ooooo', '.ooo.', '.ooo.', '..o..'], ['o.o.', 'oooo', 'oooo', '.oo.'], ['.o.o', 'oooo', '.oo.', '.o..']],
    tuft:     [['.o.o.', 'ooooo', '.ooo.', '.o.o.'], ['.o.', 'ooo', 'ooo', '.o.'], ['o.o.', '.oo.', 'oooo', '.o.o']],
    broad:    [['.o.oo.', 'oooooo', 'oooooo', '.ooo..'], ['o.oo', 'oooo', 'oooo', '.oo.'], ['.oo.o', 'ooooo', 'ooooo', '..oo.']],
    maple:    [['o.o.o', 'ooooo', '.ooo.', '..o..'], ['.o.o', 'oooo', 'oooo', '.oo.'], ['o.o.o', 'ooooo', 'ooooo', '.ooo.', '..o..']],
    small:    [['.oo.', 'oooo', '.oo.', '.o..'], ['.ooo', 'oooo', '.oo.', '..o.'], ['.ooo.', 'ooooo', '.ooo.', '..o..']],
    coin:     [['.oo.', 'oooo', '.oo.'], ['ooo', 'ooo', '.o.'], ['.oo', 'ooo', 'oo.']],
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
    pineTuft: { w: 4.3, h: 2.6, rot: -0.50, jit: 0.70, warp: 2.3, tone: 0.12, edge: 'pineTuft' },
    scale:    { w: 2.8, h: 3.6, rot:  0.10, jit: 0.60, warp: 1.6, tone: 0.11, edge: 'scale' },
    tuft:     { w: 2.9, h: 2.8, rot:  0.60, jit: 0.80, warp: 1.8, tone: 0.13, edge: 'tuft' },
    broad:    { w: 4.6, h: 3.5, rot:  0.35, jit: 0.75, warp: 1.8, tone: 0.24, edge: 'broad', corner: 1 },
    maple:    { w: 4.3, h: 3.2, rot: -0.30, jit: 0.75, warp: 1.7, tone: 0.22, edge: 'maple', corner: 1 },
    small:    { w: 3.6, h: 2.9, rot: -0.50, jit: 0.80, warp: 1.6, tone: 0.20, edge: 'small', flip: 0.07 },
    coin:     { w: 2.9, h: 2.4, rot:  0.40, jit: 0.85, warp: 1.4, tone: 0.20, edge: 'coin', flip: 0.10 },
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
    const b = pale ? mix(bark, COLD, 0.16) : bark;   // pass 4: birch is warm white, not blue
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
      real: 21, crown: 6.0, dbh: 0.55, fol: '#33614a', bark: '#5a4433', wind: [0.040, 2.4, 0.12, 1.0], sigma: 0.075, trans: 0.35,
      cb: 0.22, tiers: 17, taperE: 0.76, droop: 0.42, tipUp: 0.12, gappy: 0.08, asym: 0.08, top: 'spire', dead: 3, boughs: 8, plate: 0.36, leaderZ: -0.6 },
    { key: 'BlackSpruce', name: 'Black Spruce', latin: 'Picea mariana', form: 'spire', grain: 'needle', barkGrain: 'scale',
      real: 11, crown: 2.8, dbh: 0.28, fol: '#2d5446', bark: '#4e3b2d', wind: [0.050, 2.0, 0.10, 0.8], sigma: 0.070, trans: 0.30,
      cb: 0.30, tiers: 14, taperE: 0.86, droop: 0.46, tipUp: 0.05, gappy: 0.45, asym: 0.05, top: 'club', dead: 4, boughs: 6, plate: 0.38, leaderZ: -0.6 },
    { key: 'BalsamFir', name: 'Balsam Fir', latin: 'Abies balsamea', form: 'spire', grain: 'fir', barkGrain: 'smooth',
      real: 16, crown: 4.6, dbh: 0.40, fol: '#316741', bark: '#55432f', wind: [0.034, 2.0, 0.10, 0.8], sigma: 0.075, trans: 0.35,
      cb: 0.12, tiers: 17, taperE: 0.72, droop: 0.04, tipUp: 0.10, gappy: 0.0, asym: 0.0, top: 'spire', dead: 0, boughs: 8, plate: 0.36, stiff: true, leaderZ: -0.6 },
    { key: 'WhitePine', name: 'E. White Pine', latin: 'Pinus strobus', form: 'pine', grain: 'pineTuft', barkGrain: 'plate',
      real: 27, crown: 11, dbh: 0.90, fol: '#467a52', bark: '#5f4834', wind: [0.050, 3.6, 0.20, 1.2], sigma: 0.042, trans: 0.50,
      cb: 0.42, tiers: 7, taperE: 0.55, droop: 0.0, tipUp: 0.55, gappy: 0.12, asym: 0.35, top: 'flat', dead: 1, boughs: 5, plate: 0.30, maxPlate: 18, whorlJit: 0.30, leaderZ: -0.25 },
    { key: 'WhiteCedar', name: 'E. White Cedar', latin: 'Thuja occidentalis', form: 'cedar', grain: 'scale', barkGrain: 'shred',
      real: 13, crown: 4.2, dbh: 0.45, fol: '#4a6e36', bark: '#6a4c37', wind: [0.030, 1.4, 0.12, 0.5], sigma: 0.085, trans: 0.40,
      cb: 0.10, tiers: 20, taperE: 0.45, droop: 0.30, tipUp: 0.0, gappy: 0.0, asym: 0.0, top: 'spire', dead: 0, boughs: 7, plate: 0.50, stems: 2, leaderZ: -0.6 },
    { key: 'Tamarack', name: 'Tamarack', latin: 'Larix laricina', form: 'larch', grain: 'tuft', barkGrain: 'plate',
      real: 17, crown: 5.2, dbh: 0.40, fol: '#638a38', bark: '#57422f', fall: '#d3a238', wind: [0.056, 3.0, 0.30, 0.8], sigma: 0.040, trans: 0.70,
      cb: 0.25, tiers: 13, taperE: 0.74, droop: 0.12, tipUp: 0.0, gappy: 0.30, asym: 0.06, top: 'spire', dead: 0, boughs: 5, leaderZ: -0.35 },
    { key: 'WhiteBirch', name: 'White Birch', latin: 'Betula papyrifera', form: 'oval', grain: 'small', barkGrain: 'paper', pale: true,
      real: 17, crown: 7.0, dbh: 0.40, fol: '#4c7a33', bark: '#dcd6c8', fall: '#d9a832', wind: [0.070, 3.6, 0.60, 0.9], sigma: 0.048, trans: 1.0, shimmer: [0.75, 0.10], under: '#b9c7a2',
      fork: 0.32, nP: 4, curve: 'birch', kink: 0.10, limbR: [0.55, 0.30, 0.14], cycF: 0.66, chF: 0.32, underOpen: 0.40, flor: 0.34, hang: 2, secs: 2, secAt: 0.35, stems: 2, droop: 0.30, fill: 2.0 },
    { key: 'RedMaple', name: 'Red Maple', latin: 'Acer rubrum', form: 'round', grain: 'maple', barkGrain: 'plate',
      real: 20, crown: 9.0, dbh: 0.60, fol: '#3d6f30', bark: '#544639', fall: '#bf3f26', wind: [0.045, 3.0, 0.40, 0.7], sigma: 0.058, trans: 0.9, shimmer: [0.60, 0.06], under: '#c3cdbd',
      fork: 0.27, nP: 4, curve: 'maple', kink: 0.10, limbR: [0.62, 0.34, 0.16], cycF: 0.60, chF: 0.33, underOpen: 0.40, flor: 0.30, hang: 1, secs: 1, secAt: 0.0, droop: 0.22 },
    { key: 'RedOak', name: 'Red Oak', latin: 'Quercus rubra', form: 'round', grain: 'broad', barkGrain: 'furrow',
      real: 22, crown: 15, dbh: 0.85, fol: '#395f2d', bark: '#4f4235', fall: '#a35429', wind: [0.032, 2.4, 0.30, 0.6], sigma: 0.060, trans: 0.8, shimmer: [0.40, 0.04], under: '#8fa46e',
      fork: 0.30, nP: 4, curve: 'oak', kink: 0.22, limbR: [0.72, 0.42, 0.18], cycF: 0.63, chF: 0.29, underOpen: 0.70, flor: 0.26, hang: 2, secs: 2, secAt: 0.5, droop: 0.16 },
    { key: 'TremblingAspen', name: 'Trembling Aspen', latin: 'Populus tremuloides', form: 'oval', grain: 'coin', barkGrain: 'smooth', pale: true,
      real: 18, crown: 5.0, dbh: 0.35, fol: '#5f853a', bark: '#bcc0ad', fall: '#e0b03a', wind: [0.060, 2.8, 1.00, 0.7], sigma: 0.044, trans: 1.0, shimmer: [1.00, 0.35], under: '#c6d1b0',
      fork: 0.50, nP: 4, curve: 'aspen', kink: 0.06, limbR: [0.55, 0.30, 0.14], cycF: 0.70, chF: 0.30, underOpen: 0.42, flor: 0.36, hang: 0, secs: 2, secAt: 0.3, droop: 0.12, fill: 2.2 },
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

  // ---- floret: a leaf mass, domed on top and flatter underneath ---------------------------------
  // Pass 3 ringed the core with 5–6 EQUAL satellites at equal spacing — a rosette, so every floret was a
  // cauliflower. Satellites now vary in size and spacing, the big ones ride the top and outer shoulder,
  // and the underside is smaller, flatter and tucked in.
  function floret(clumps, rng, fx, fy, fz, fr, mid) {
    clumps.push({ x: fx, y: fy + fr * 0.06, z: fz - fr * 0.30, rx: fr * 0.94, ry: fr * 0.74, rz: fr * 0.86, m: mid });
    const ns = fr > MIN_R * 3.2 ? 7 : fr > MIN_R * 2.4 ? 6 : 5, sp0 = rng() * Math.PI * 2;
    for (let s = 0; s < ns; s++) {
      const sa = sp0 + (s + (rng() - 0.5) * 0.6) / ns * Math.PI * 2, down = Math.sin(sa), under = down > 0.25;
      const sr = Math.max(MIN_R, fr * (0.40 + rng() * 0.24) * (under ? 0.74 : down < -0.35 ? 1.10 : 1));
      const rr = fr * (0.58 + rng() * 0.14) * (under ? 0.80 : 1);
      clumps.push({ x: fx + Math.cos(sa) * rr, y: fy + down * rr * (under ? 0.58 : 0.90), z: fz - down * fr * 0.46 + (rng() * 2 - 1) * fr * 0.14,
        rx: sr * 1.06, ry: sr * (under ? 0.72 : 0.90), rz: sr, m: mid });
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

  // a limb as a curve THROUGH its elbow — the quadratic Bézier whose midpoint is C — in six tapered pieces
  function bezLimb(limbs, F, C, T, r0, r1, r2, twigMat, mid) {
    const P1 = [2 * C[0] - (F[0] + T[0]) / 2, 2 * C[1] - (F[1] + T[1]) / 2, 2 * C[2] - (F[2] + T[2]) / 2], N = 6;
    const at = (s) => { const a = (1 - s) * (1 - s), b = 2 * (1 - s) * s, c = s * s; return [a * F[0] + b * P1[0] + c * T[0], a * F[1] + b * P1[1] + c * T[1], a * F[2] + b * P1[2] + c * T[2]]; };
    const rad = (s) => s < 0.5 ? r0 + (r1 - r0) * (s / 0.5) : r1 + (r2 - r1) * ((s - 0.5) / 0.5);
    let A = F;
    for (let k = 1; k <= N; k++) { const s0 = (k - 1) / N, s1 = k / N, B = at(s1); limbs.push([A[0], A[1], A[2], B[0], B[1], B[2], rad(s0), rad(s1), s1 <= 0.5 ? M.BARK : twigMat, mid]); A = B; }
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
    const env = {};
    const done = () => ({ W, H, cx, baseY, limbs, clumps, bare, conifer, rng, masses: massN, eMax, trunkR, env, crownR: crownPx / 2 });

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
      env.cone = { x: cx + lean * H * 0.25, yb: crownBase, yt: topY, r: maxR * 1.3 };
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
            // WHITE PINE: a long bare ARM, and on its outer half a SPRAY of 3–4 flat overlapping pads that
            // step up toward the tip — the layered, up-swept plume. Pass 3 hung round tufts on the arm and
            // got pom-poms. Nothing hangs; the trunk shows between whorls.
            const reach = Rs * (0.74 + rng() * 0.26) * asymK;
            const ex = sxk + Math.cos(a) * reach, ez = Math.sin(a) * reach * zsq, ey = y + (rng() * 2 - 1) * rr * 0.22 + rr * 0.10;
            limbs.push([sxk, y - 0.5, -trunkR * 0.2, sxk + (ex - sxk) * 0.84, y + (ey - y) * 0.84 + rr * 0.08, ez * 0.84, Math.max(1.6, trunkR * 0.28), 1.3, M.BARK, bm]);
            const nPad = rr > MR * 1.8 ? 4 : 3;
            for (let q = 0; q < nPad; q++) {
              const u = 0.50 + 0.50 * q / (nPad - 1), pr = rr * (1.05 - 0.32 * u);
              clumps.push({ x: sxk + (ex - sxk) * u + (rng() * 2 - 1) * rr * 0.12, y: y + (ey - y) * u - sp.tipUp * rr * 0.95 * u * u - q * rr * 0.08, z: ez * u + (rng() * 2 - 1) * rr * 0.15, m: bm,
                rx: Math.max(MR, pr * (1.10 + ca * 0.45)), ry: Math.max(MR, pr * 0.34), rz: Math.max(MR, pr * (0.85 + sa * 0.40)) });
            }
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
    env.ell = { x: cx + lean * H * 0.3, y: cyc, rx: cw + 2, ry: ch + 2, rz: cw * 0.85 };
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
      bezLimb(limbs, F, C, tip, trunkR * r0, trunkR * r1, Math.max(1.2, trunkR * r2), twigMat, mid);
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
    let top = 1e9, bot = -1e9, xl = 1e9, xr = -1e9, fx = 0;
    for (const season of ['summer', 'winter']) for (let v = 0; v < VARIANTS; v++) {
      const g = build(sp, v, season, t), e = extents(g); fx = g.cx - 0.5;
      if (e.top < top) top = e.top; if (e.bot > bot) bot = e.bot;
      if (e.xl < xl) xl = e.xl; if (e.xr > xr) xr = e.xr;
    }
    const reach = windReach(sp, t); xl -= reach; xr += reach; top -= 3;   // pass 4: room for a gale either way
    const pivotY = Math.ceil(MG - top);
    const h = Math.ceil(pivotY + bot + MG) + 1;
    const dx = Math.max(0, Math.ceil(MG - xl));
    const wCell = Math.max(24, dx + Math.ceil(xr + MG) + 1);
    // 4.1: the pivot is the trunk-foot COLUMN. It was the centre of the unioned silhouette, up to 5 px
    // off the trunk on an asymmetric crown (pole white pine, pole oak) — the cell size is unchanged.
    const cell = { w: wCell, h, dx, pivotX: dx + fx, pivotY, pad: h - 1 - pivotY, size: t };
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
        if (v.st) { v.st[i] = v.st[src]; v.ao[i] = v.ao[src]; v.part[i] = v.part[src]; v.r[i] = v.r[src]; }
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
  const hsh = (n, k) => { let h = Math.imul((n | 0) ^ Math.imul(k + 1, 0x9e3779b9), 0x85ebca6b); h ^= h >>> 13; h = Math.imul(h, 0xc2b2ae35); h ^= h >>> 16; return (h >>> 0) / 4294967296; };
  function snoise(x, y, s) {
    const xi = Math.floor(x), yi = Math.floor(y), fx = x - xi, fy = y - yi;
    const u = fx * fx * (3 - 2 * fx), vv = fy * fy * (3 - 2 * fy);
    const a = vnoise(xi, yi, s), b = vnoise(xi + 1, yi, s), c = vnoise(xi, yi + 1, s), d = vnoise(xi + 1, yi + 1, s);
    return a + (b - a) * u + (c - a) * vv + (a - b - c + d) * u * vv;
  }

  // =================================================================================================
  //  PASS 4 — parts · wind · G-buffer · weather relight · cast shadow
  // =================================================================================================
  const NEAR = [[0, 0], [1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1]];
  const BRING = [[4, 0], [3, 3], [0, 4], [-3, 3], [-4, 0], [-3, -3], [0, -4], [3, -3], [7, 0], [5, 5], [0, 7], [-5, 5], [-7, 0], [-5, -5], [0, -7], [5, -5]];
  const N4 = [[1, 0], [-1, 0], [0, 1], [0, -1]];
  const LOOP = 16;
  const D2R = Math.PI / 180, TAU = Math.PI * 2;
  // foliage bands: the key band is for the most-lit stamps only — the lit side of a crown is `hi`
  const FSTEPS = [0.06, 0.16, 0.33, 0.74];
  const bandF = (l) => { for (let i = 0; i < 4; i++) if (l < FSTEPS[i]) return i; return 4; };
  const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const toView = (w) => [w[0], -w[2] * CE + w[1] * SE, w[2] * SE + w[1] * CE];     // world [east, south, up] → view
  const dirOf = (az, el) => { const c = Math.cos(el * D2R); return [Math.sin(az * D2R) * c, -Math.cos(az * D2R) * c, Math.sin(el * D2R)]; };
  const UPV = toView([0, 0, 1]);
  const REF_SUN = dirOf(290, 55);
  // the authored key as a sky: what render() and the _lit export are lit by (= WeatherSky.at({time: 14}))
  const REF_SKY = { name: 'Afternoon · reference key', sunW: REF_SUN, sunV: toView(REF_SUN), sunI: 1, skyI: 0.60, expo: 1,
    kc: '#fff0cf', ac: '#1d3b4a', ka: 0.16, aa: 0.30, rc: '#bcd6e2', ra: 0.14, wash: '#fff4dd', wa: 0.03, amb: 0.62,
    fogC: '#c3cdce', fog: 0, wet: 0, snow: 0 };
  // no sun, flat bright sky, no grade: the structure bands only — the _unlit export
  const UNLIT_SKY = { name: 'unlit', sunW: [0, 0, 1], sunV: UPV, sunI: 0, skyI: 1.25, expo: 1, kc: '#ffffff', ac: '#000000', ka: 0, aa: 0,
    wash: '#ffffff', wa: 0, amb: 1, fogC: '#ffffff', fog: 0, wet: 0, snow: 0, grade: false };

  // ---- wind ---------------------------------------------------------------------------------------
  // [bend (tip lean at a gale, × H), limb sway px per 300 px of height, leaf flutter, bough bob]
  const windOf = (sp) => sp.wind || [0.04, 2.5, 0.3, 0.8];
  function windReach(sp, t) { const H = Math.max(26, sp.worldH * (t == null ? 1 : t)), w = windOf(sp); return Math.ceil(w[0] * H * 1.8 + w[1] * H / 300 * 2.8) + 5; }
  /* the displacement field, build space in → screen px out. Trunk: lean ∝ w² plus a sway around it,
     both ∝ h^1.8, under one gust per loop. Limbs: sway ∝ reach^1.2 on a wave that crosses the crown
     downwind (the phase follows position, upwind side leading, with a little per-mass play — so
     neighbours move together and nothing pops alone), and a vertical bob. Sampled per mass, per leaf
     and per limb end. Periodic in the loop phase ph ∈ [0, 1). */
  function windAt(mdl, W, x, y, z, m, ph) {
    const w = W.w; if (!(w > 0)) return [0, 0];
    const g = mdl.g, wv = windOf(mdl.sp), TAU = 6.283185307179586, dir = W.dir < 0 ? -1 : 1;
    const env = 1 + W.gust * 0.9 * Math.sin(TAU * ph + 0.7), bendPx = wv[0] * g.H;
    const hk = Math.pow(clamp((g.baseY - y) / g.H, 0, 1), 1.8);
    let dx = (w * w * bendPx * (0.8 + 0.2 * env) + w * bendPx * 0.42 * Math.sin(TAU * ph + 0.3) * (0.55 + 0.45 * env)) * hk;
    const r = Math.min(1.3, Math.hypot(x - g.cx, z) / Math.max(8, g.crownR));
    const pm = -dir * (x - g.cx) / Math.max(8, g.crownR) * 0.9 + ((((m == null ? 0 : m) * 0.618034) % 1) - 0.5) * 0.5, la = wv[1] * g.H / 300 * w * (0.6 + 0.4 * env);
    dx += la * Math.pow(r, 1.2) * (Math.sin(2 * TAU * ph + pm) + 0.5);
    return [dx * dir, -la * wv[3] * 0.5 * r * Math.sin(2 * TAU * ph + pm + 1.1)];
  }

  // ---- geometry helpers ----------------------------------------------------------------------------
  // limbs split into ≤ maxLen pieces (so a bend is a curve), each end keeping its build-space point
  function splitLimbs(g, SX, SY, SZ, maxLen) {
    const out = [];
    for (const L of g.limbs) {
      const sa = [SX(L[0]), SY(L[1], L[2]), SZ(L[1], L[2])], sb = [SX(L[3]), SY(L[4], L[5]), SZ(L[4], L[5])];
      const n = Math.max(1, Math.ceil(Math.hypot(sb[0] - sa[0], sb[1] - sa[1]) / maxLen));
      const P = (t) => [L[0] + (L[3] - L[0]) * t, L[1] + (L[4] - L[1]) * t, L[2] + (L[5] - L[2]) * t];
      const S = (t) => [sa[0] + (sb[0] - sa[0]) * t, sa[1] + (sb[1] - sa[1]) * t, sa[2] + (sb[2] - sa[2]) * t];
      for (let k = 0; k < n; k++) {
        const a = k / n, b = (k + 1) / n;
        out.push({ a: P(a), b: P(b), sa: S(a), sb: S(b), r0: L[6] + (L[7] - L[6]) * a, r1: L[6] + (L[7] - L[6]) * b, mat: L[8], m: L[9] == null ? 900 : L[9] });
      }
    }
    return out;
  }
  // limb() plus the pass-4 channels (radius for the bark-grain gate; no stamp, no part)
  function limb4(v, x0, y0, z0, x1, y1, z1, r0, r1, mat, id, mid) {
    const dx = x1 - x0, dy = y1 - y0, L2 = dx * dx + dy * dy || 1e-6, R = Math.max(r0, r1);
    const ax = Math.max(0, Math.floor(Math.min(x0, x1) - R)), bx = Math.min(v.w - 1, Math.ceil(Math.max(x0, x1) + R));
    const ay = Math.max(0, Math.floor(Math.min(y0, y1) - R)), by = Math.min(v.h - 1, Math.ceil(Math.max(y0, y1) + R));
    for (let y = ay; y <= by; y++) for (let x = ax; x <= bx; x++) {
      const t = clamp(((x + 0.5 - x0) * dx + (y + 0.5 - y0) * dy) / L2, 0, 1);
      const px = x0 + dx * t, py = y0 + dy * t, pz = z0 + (z1 - z0) * t, r = r0 + (r1 - r0) * t;
      if (r <= 0.35) continue;
      const ox = x + 0.5 - px, oy = y + 0.5 - py, d2 = ox * ox + oy * oy;
      if (d2 > r * r) continue;
      const k = Math.sqrt(r * r - d2), z = pz + k, i = y * v.w + x;
      if (z <= v.z[i]) continue;
      let nx = ox / r, ny = oy / r * 0.35, nz = k / r; const Ln = Math.hypot(nx, ny, nz) || 1;
      v.z[i] = z; v.nx[i] = nx / Ln; v.ny[i] = ny / Ln; v.nz[i] = nz / Ln;
      v.mat[i] = mat; v.a[i] = 1; v.id[i] = id; v.mid[i] = mid; v.st[i] = -1; v.part[i] = -1; v.r[i] = r;
    }
  }
  // the crown envelope's outward normal: an ellipsoid (broadleaves) or a cone round the stem (conifers)
  function envN(E, P) {
    if (!E) return null;
    if (E.kind === 'ell') return nrm([(P[0] - E.c[0]) / (E.r[0] * E.r[0]), (P[1] - E.c[1]) / (E.r[1] * E.r[1]), (P[2] - E.c[2]) / (E.r[2] * E.r[2])]);
    const bx = P[0] - E.b[0], by = P[1] - E.b[1], bz = P[2] - E.b[2], t = bx * UPV[0] + by * UPV[1] + bz * UPV[2];
    const rx = bx - t * UPV[0], ry = by - t * UPV[1], rz = bz - t * UPV[2], rl = Math.hypot(rx, ry, rz);
    if (rl < 1e-3) return UPV.slice();
    return nrm([rx / rl * E.hc + UPV[0] * E.r, ry / rl * E.hc + UPV[1] * E.r, rz / rl * E.hc + UPV[2] * E.r]);
  }
  function limbSpheres(out, x0, y0, z0, x1, y1, z1, r0, r1, sig) {
    const n = Math.max(1, Math.ceil(Math.hypot(x1 - x0, y1 - y0, z1 - z0) / Math.max(1.5, Math.min(r0, r1) * 1.2)));
    for (let k = 0; k <= n; k++) { const t = k / n, r = Math.max(0.8, r0 + (r1 - r0) * t); out.push({ x: x0 + (x1 - x0) * t, y: y0 + (y1 - y0) * t, z: z0 + (z1 - z0) * t, a: r, b: r, c: r, sig }); }
  }

  // ---- opacity maps --------------------------------------------------------------------------------
  /* Light-space opacity: the clump ellipsoids and limb spheres rasterised along a direction, each
     chord's σ·length deposited over the depth slices it spans, stored as the opacity ABOVE each slice
     boundary (toward the light). opAt(P) is then bilinear in the plane and linear in depth — so a
     leaf on the lit face of its own clump is not shaded by that clump, one deep inside it is. */
  function opMap(prims, dir, tex, K) {
    const w = nrm(dir), hint = Math.abs(w[1]) < 0.9 ? [0, 1, 0] : [1, 0, 0], u = nrm(cross(hint, w)), v = cross(w, u);
    const n = prims.length, PU = new Float32Array(n), PV = new Float32Array(n), PD = new Float32Array(n), PR = new Float32Array(n);
    let u0 = 1e9, u1 = -1e9, v0 = 1e9, v1 = -1e9, d0 = 1e9, d1 = -1e9;
    for (let k = 0; k < n; k++) {
      const p = prims[k], R = Math.max(p.a, p.b, p.c);
      PU[k] = p.x * u[0] + p.y * u[1] + p.z * u[2]; PV[k] = p.x * v[0] + p.y * v[1] + p.z * v[2]; PD[k] = p.x * w[0] + p.y * w[1] + p.z * w[2]; PR[k] = R;
      if (PU[k] - R < u0) u0 = PU[k] - R; if (PU[k] + R > u1) u1 = PU[k] + R;
      if (PV[k] - R < v0) v0 = PV[k] - R; if (PV[k] + R > v1) v1 = PV[k] + R;
      if (PD[k] - R < d0) d0 = PD[k] - R; if (PD[k] + R > d1) d1 = PD[k] + R;
    }
    if (!n) { u0 = v0 = d0 = 0; u1 = v1 = d1 = 1; }
    u0 -= tex; v0 -= tex;
    const gw = Math.max(1, Math.ceil((u1 - u0) / tex) + 2), gh = Math.max(1, Math.ceil((v1 - v0) / tex) + 2), dz = Math.max(1e-3, (d1 - d0) / K), K1 = K + 1;
    const P = new Float32Array(gw * gh * K1);
    for (let k = 0; k < n; k++) {
      const p = prims[k], ia = 1 / (p.a * p.a), ib = 1 / (p.b * p.b), ic = 1 / (p.c * p.c), A = w[0] * w[0] * ia + w[1] * w[1] * ib + w[2] * w[2] * ic, R = PR[k];
      const iu0 = Math.max(0, Math.floor((PU[k] - R - u0) / tex)), iu1 = Math.min(gw - 1, Math.ceil((PU[k] + R - u0) / tex));
      const iv0 = Math.max(0, Math.floor((PV[k] - R - v0) / tex)), iv1 = Math.min(gh - 1, Math.ceil((PV[k] + R - v0) / tex));
      const dens = p.sig * dz;
      for (let iv = iv0; iv <= iv1; iv++) for (let iu = iu0; iu <= iu1; iu++) {
        const du = u0 + (iu + 0.5) * tex - PU[k], dv = v0 + (iv + 0.5) * tex - PV[k];
        const qx = du * u[0] + dv * v[0], qy = du * u[1] + dv * v[1], qz = du * u[2] + dv * v[2];
        const B = 2 * (qx * w[0] * ia + qy * w[1] * ib + qz * w[2] * ic), C = qx * qx * ia + qy * qy * ib + qz * qz * ic - 1, disc = B * B - 4 * A * C;
        if (disc <= 0) continue;
        const sq = Math.sqrt(disc), s0 = (PD[k] + (-B - sq) / (2 * A) - d0) / dz, s1 = (PD[k] + (-B + sq) / (2 * A) - d0) / dz;
        const b = (iv * gw + iu) * K1, k0 = Math.max(0, Math.floor(s0)), k1 = Math.min(K - 1, Math.floor(s1));
        for (let q = k0; q <= k1; q++) { const ov = Math.min(s1, q + 1) - Math.max(s0, q); if (ov > 0) P[b + q] += dens * ov; }
      }
    }
    for (let c = 0; c < gw * gh; c++) { const b = c * K1; let s = 0; P[b + K] = 0; for (let q = K - 1; q >= 0; q--) { s += P[b + q]; P[b + q] = s; } }
    return { u, v, w, u0, v0, d0, dz, K, K1, gw, gh, tex, P };
  }
  function opAt(M_, x, y, z) {
    const pu = x * M_.u[0] + y * M_.u[1] + z * M_.u[2], pv = x * M_.v[0] + y * M_.v[1] + z * M_.v[2], pd = x * M_.w[0] + y * M_.w[1] + z * M_.w[2];
    let kf = (pd - M_.d0) / M_.dz;
    if (kf >= M_.K) return 0;
    if (kf < 0) kf = 0;
    const k = Math.floor(kf), fk = kf - k, fu = (pu - M_.u0) / M_.tex - 0.5, fv = (pv - M_.v0) / M_.tex - 0.5;
    const iu = Math.floor(fu), iv = Math.floor(fv), au = fu - iu, av = fv - iv;
    let o = 0;
    for (let c = 0; c < 4; c++) {
      const cu = iu + (c & 1), cv = iv + (c >> 1);
      if (cu < 0 || cv < 0 || cu >= M_.gw || cv >= M_.gh) continue;
      const wt = ((c & 1) ? au : 1 - au) * ((c >> 1) ? av : 1 - av), b = (cv * M_.gw + cu) * M_.K1;
      o += wt * (M_.P[b + k] + (M_.P[b + k + 1] - M_.P[b + k]) * fk);
    }
    return o;
  }

  // ---- leaf stamps, per part -------------------------------------------------------------------------
  // pass 3's rotated · jittered · warped lattice, but in SPRITE coordinates and painted over one part's
  // whole surface (hidden pixels too): the stamps belong to the mass, and each also rides the wind alone.
  function stampPart(v, ox, oy, G, s) {
    const w = v.w, h = v.h, LW = G.w, LH = G.h, jit = G.jit, ca = Math.cos(G.rot), sa = Math.sin(G.rot);
    let mnx = 1e9, mxx = -1e9, mny = 1e9, mxy = -1e9;
    for (const c of [[ox, oy], [ox + w, oy], [ox, oy + h], [ox + w, oy + h]]) {
      const rx = c[0] * ca + c[1] * sa, ry = -c[0] * sa + c[1] * ca;
      if (rx < mnx) mnx = rx; if (rx > mxx) mxx = rx; if (ry < mny) mny = ry; if (ry > mxy) mxy = ry;
    }
    const pad = G.warp + 6, sites = [];
    for (let j = Math.floor((mny - pad) / LH); j <= Math.ceil((mxy + pad) / LH); j++) for (let i = Math.floor((mnx - pad) / LW); i <= Math.ceil((mxx + pad) / LW); i++) {
      const ii = i + 4096, jj = j + 4096;
      const rx = (i + 0.5 + jit * (vnoise(ii * 3 + 1, jj * 5 + 2, s) - 0.5)) * LW, ry = (j + 0.5 + jit * (vnoise(ii * 7 + 53, jj * 11 + 17, s + 3) - 0.5)) * LH;
      let x = rx * ca - ry * sa, y = rx * sa + ry * ca;
      x += (snoise(x / 9.3, y / 7.1, s + 11) - 0.5) * 2 * G.warp;
      y += (snoise(x / 7.7, y / 9.9, s + 29) - 0.5) * 2 * G.warp;
      const xi = Math.round(x) - ox, yi = Math.round(y) - oy;
      if (xi < -6 || yi < -6 || xi >= w + 6 || yi >= h + 6) continue;
      sites.push({ x: xi, y: yi, k: ii * 7919 + jj * 104729 });
    }
    sites.sort((A, B) => A.y - B.y || A.x - B.x);
    // each stamp keeps its whole footprint (clipped to the part) in paint order, so frame() can re-lay
    // the leaves every frame, each at its own offset, the lower still painting over the upper
    const id = new Int32Array(w * h), shapes = G.shapes, fpO = [0], fpP = [];
    let n = 0;
    for (const S of sites) {
      let ok = false;
      for (const [dx, dy] of NEAR) { const jx = S.x + dx, jy = S.y + dy; if (jx >= 0 && jy >= 0 && jx < w && jy < h && v.a[jy * w + jx]) { ok = true; break; } }
      if (!ok) continue;
      const sh = shapes[Math.floor(vnoise(S.k & 65535, S.k >>> 16, s + 7) * shapes.length) % shapes.length], sid = n + 1, f0 = fpP.length;
      for (const p of sh.px) { const jx = S.x + p[0], jy = S.y + p[1]; if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue; const j = jy * w + jx; if (v.a[j]) { id[j] = sid; fpP.push(j); } }
      if (fpP.length > f0) { n = sid; fpO.push(fpP.length); }
    }
    return { id, n, fpO: Int32Array.from(fpO), fpP: Int32Array.from(fpP) };
  }

  // ---- the model: geometry + parts + light-independent channels, once per species/variant/season/size ----
  const MODELS = new Map();
  function model(key, o) {
    o = o || {};
    const sp = byKey[key] || SPECIES[0];
    const season = SEASONS.indexOf(o.season) >= 0 ? o.season : 'summer';
    const size = sizeOf(o), variant = ((o.variant | 0) % VARIANTS + VARIANTS) % VARIANTS;
    const mk = sp.key + '|' + variant + '|' + season + '|' + Math.round(size * 1000);
    const hit = MODELS.get(mk); if (hit) return hit;
    const t0 = Date.now();
    const g = build(sp, variant, season, size), cell = cellOf(sp, size), pivot = { x: cell.pivotX, y: cell.pivotY };
    const SX = (x) => x + cell.dx, SY = (y, z) => pivot.y + pyRel(g, y, z), SZ = (y, z) => pzOf(g, y, z);
    const E = edgeOf(sp), G = grainOf(sp), eM = eMaxOf(E), limbs = splitLimbs(g, SX, SY, SZ, 14);
    let env = null;
    if (g.env.ell) { const e = g.env.ell; env = { kind: 'ell', c: [SX(e.x), SY(e.y, 0), SZ(e.y, 0)], r: [e.rx, Math.hypot(e.ry * CE, e.rz * SE), Math.hypot(e.ry * SE, e.rz * CE)] }; }
    else if (g.env.cone) { const e = g.env.cone; env = { kind: 'cone', b: [SX(e.x), SY(e.yb, 0), SZ(e.yb, 0)], hc: Math.max(4, e.yb - e.yt), r: e.r }; }
    // normal blend, leaf · floret · crown: how much of the big form each form keeps
    const form = sp.form, WB = (form === 'round' || form === 'oval') ? [0.42, 0.22, 0.36] : form === 'pine' ? [0.60, 0.12, 0.28] : form === 'larch' ? [0.54, 0.16, 0.30] : [0.50, 0.14, 0.36];
    const sd = (sp.key.length * 13 + Math.round(sp.real * 7)) % 97;
    const groups = new Map();
    g.clumps.forEach((c, k) => { const m = c.m == null ? 900 : c.m; let a = groups.get(m); if (!a) groups.set(m, a = []); a.push(k); });
    const parts = [], base = limbs.length;
    let stampN = 0, leafPx = 0;
    for (const [m, ks] of groups) {
      let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9, ax = 0, ay = 0, az = 0, sw = 0, cxs = 0, cys = 0, czs = 0;
      const pd = eM + 2;
      const prims = ks.map(k => {
        const c = g.clumps[k], sx = SX(c.x), sy = SY(c.y, c.z), sz = SZ(c.y, c.z), ry = Math.hypot(c.ry * CE, c.rz * SE), rz = Math.hypot(c.ry * SE, c.rz * CE);
        if (sx - c.rx * 1.3 - pd < x0) x0 = sx - c.rx * 1.3 - pd; if (sx + c.rx * 1.3 + pd > x1) x1 = sx + c.rx * 1.3 + pd;
        if (sy - ry * 1.3 - pd < y0) y0 = sy - ry * 1.3 - pd; if (sy + ry * 1.3 + pd > y1) y1 = sy + ry * 1.3 + pd;
        const wt = c.rx * ry * rz; ax += c.x * wt; ay += c.y * wt; az += c.z * wt; cxs += sx * wt; cys += sy * wt; czs += sz * wt; sw += wt;
        return { k, x: sx, y: sy, z: sz, a: c.rx, b: ry, c: rz };
      });
      x0 = Math.floor(x0); y0 = Math.floor(y0); x1 = Math.ceil(x1); y1 = Math.ceil(y1);
      const w = x1 - x0 + 1, h = y1 - y0 + 1, v = new Vol(w, h);
      for (const p of prims) blob(v, p.x - x0, p.y - y0, p.z, p.a, p.b, p.c, M.FOLIAGE, base + p.k, m, base + p.k, E);
      const li = []; for (let i = 0; i < w * h; i++) if (v.a[i]) li.push(i);
      if (!li.length) continue;
      const n = li.length, fc = [cxs / sw, cys / sw, czs / sw];
      let fx = 1, fy = 1, fz = 1;
      for (const p of prims) { fx = Math.max(fx, Math.abs(p.x - fc[0]) + p.a); fy = Math.max(fy, Math.abs(p.y - fc[1]) + p.b); fz = Math.max(fz, Math.abs(p.z - fc[2]) + p.c); }
      const SF = stampPart(v, x0, y0, G, sd), sn = SF.n, qOf = new Int32Array(w * h).fill(-1);
      const P = { m, x0, y0, w, h, n, li: Int32Array.from(li), z: new Float32Array(n), nx: new Float32Array(n), ny: new Float32Array(n), nz: new Float32Array(n),
        id: new Int32Array(n), ao: new Float32Array(n), prims, anchor: [ax / sw, ay / sw, az / sw], sb: stampN, sn, maxD: 0,
        snx: new Float32Array(sn), sny: new Float32Array(sn), snz: new Float32Array(sn), sao: new Float32Array(sn), sc: new Float32Array(sn * 3), scs: new Float32Array(sn * 2), fpO: SF.fpO, fpQ: new Int32Array(SF.fpP.length), qOf, zc: 0 };
      for (let q = 0; q < n; q++) {
        const i = li[q], X = x0 + (i % w) + 0.5, Y = y0 + ((i / w) | 0) + 0.5, Z = v.z[i];
        const nF = nrm([(X - fc[0]) / (fx * fx), (Y - fc[1]) / (fy * fy), (Z - fc[2]) / (fz * fz)]), nC = envN(env, [X, Y, Z]) || nF;
        const b = nrm([WB[0] * v.nx[i] + WB[1] * nF[0] + WB[2] * nC[0], WB[0] * v.ny[i] + WB[1] * nF[1] + WB[2] * nC[1], WB[0] * v.nz[i] + WB[1] * nF[2] + WB[2] * nC[2]]);
        P.z[q] = Z; P.nx[q] = b[0]; P.ny[q] = b[1]; P.nz[q] = b[2]; P.id[q] = v.id[i]; qOf[i] = q; P.zc += Z;
      }
      P.zc /= n;
      // a leaf takes ONE normal — the mean over its footprint — so a live light gives it one band; the
      // base keeps the blended per-pixel normal (it is what shows between leaves). Each leaf keeps its
      // centre in build space too: the wind field is sampled there.
      for (let k = 0; k < sn; k++) {
        const f0 = SF.fpO[k], f1 = SF.fpO[k + 1];
        let X = 0, Y = 0, Z = 0, a = 0, b = 0, c = 0;
        for (let f = f0; f < f1; f++) { const i = SF.fpP[f], q = qOf[i]; P.fpQ[f] = q; X += x0 + (i % w) + 0.5; Y += y0 + ((i / w) | 0) + 0.5; Z += P.z[q]; a += P.nx[q]; b += P.ny[q]; c += P.nz[q]; }
        const L = Math.hypot(a, b, c) || 1, cnt = Math.max(1, f1 - f0), Yr = Y / cnt - pivot.y, Zc = Z / cnt;
        P.snx[k] = a / L; P.sny[k] = b / L; P.snz[k] = c / L; P.scs[k * 2] = X / cnt; P.scs[k * 2 + 1] = Y / cnt;
        P.sc[k * 3] = X / cnt - cell.dx; P.sc[k * 3 + 1] = g.baseY - (SE * Zc - CE * Yr); P.sc[k * 3 + 2] = SE * Yr + CE * Zc;
      }
      stampN += sn; leafPx += SF.fpP.length;
      const Dp = distField(v.a, w, h); for (const i of li) if (Dp[i] > P.maxD) P.maxD = Dp[i];
      parts.push(P);
    }
    // the rest pose's visible depth range: relight's height term reads it, so it cannot flicker frame to frame
    const zb = new Float32Array(cell.w * cell.h).fill(-1e9);
    for (const P of parts) for (let q = 0; q < P.n; q++) { const li = P.li[q], X = P.x0 + (li % P.w), Y = P.y0 + ((li / P.w) | 0); if (X < 0 || Y < 0 || X >= cell.w || Y >= cell.h) continue; const i = Y * cell.w + X; if (P.z[q] > zb[i]) zb[i] = P.z[q]; }
    let zmin = 1e9, zmax = -1e9;
    for (let i = 0; i < zb.length; i++) if (zb[i] > -1e8) { if (zb[i] < zmin) zmin = zb[i]; if (zb[i] > zmax) zmax = zb[i]; }
    for (const L of limbs) { zmin = Math.min(zmin, L.sa[2], L.sb[2]); zmax = Math.max(zmax, L.sa[2] + L.r0, L.sb[2] + L.r1); }
    // sky visibility: five directions through the rest-pose volume, flattened per stamp
    const sig = sp.sigma || 0.06, rest = [];
    for (const P of parts) for (const c of P.prims) rest.push({ x: c.x, y: c.y, z: c.z, a: c.a, b: c.b, c: c.c, sig });
    for (const L of limbs) limbSpheres(rest, L.sa[0], L.sa[1], L.sa[2], L.sb[0], L.sb[1], L.sb[2], L.r0, L.r1, 0.35);
    const SKYD = [[UPV, 2], [toView(nrm([0.7, 0, 0.7])), 1], [toView(nrm([-0.7, 0, 0.7])), 1], [toView(nrm([0, 0.7, 0.7])), 1], [toView(nrm([0, -0.7, 0.7])), 1]];
    const maps = SKYD.map(([d]) => opMap(rest, d, 3, 16));
    for (const P of parts) {
      for (let q = 0; q < P.n; q++) {
        const i = P.li[q], X = P.x0 + (i % P.w) + 0.5 + P.nx[q] * 1.5, Y = P.y0 + ((i / P.w) | 0) + 0.5 + P.ny[q] * 1.5, Z = P.z[q] + P.nz[q] * 1.5;
        let s = 0; for (let d = 0; d < 5; d++) s += SKYD[d][1] * Math.exp(-opAt(maps[d], X, Y, Z));
        P.ao[q] = s / 6;
      }
      for (let k = 0; k < P.sn; k++) { const f0 = P.fpO[k], f1 = P.fpO[k + 1]; let a = 0; for (let f = f0; f < f1; f++) a += P.ao[P.fpQ[f]]; P.sao[k] = f1 > f0 ? a / (f1 - f0) : 0.5; }
    }
    const FOL = folRamp(sp.fol, season, sp.fall), BARK = barkRamp(sp.bark, sp.pale), BARKB = {};
    for (const k of ['dp', 'sh', 'mid', 'hi', 'key', 'rim']) BARKB[k] = mix(BARK[k], FOL.dp, k === 'rim' ? 0.40 : 0.74);
    // the leaf UNDERSIDE — what a turned-over leaf shows: paler, cooler (silvery on aspen, birch, maple)
    const UNDER = {}, uc = season === 'autumn' && sp.fall ? mix(sp.fall, '#f2e4b4', 0.35) : (sp.under || mix(sp.fol, '#c8d2bc', 0.5));
    for (const k of ['dp', 'sh', 'mid', 'hi', 'key', 'rim']) UNDER[k] = mix(FOL[k], uc, k === 'dp' ? 0.12 : k === 'sh' ? 0.26 : 0.40);
    const mdl = { key: sp.key, sp, g, cell, pivot, season, variant, size, stage: stageName(size), limbs, parts, stampN, leafPx: stampN ? Math.round(leafPx / stampN * 10) / 10 : 0, zmin, zmax, env, G, E,
      upMap: maps[0], FOL, BARK, BARKB, UNDER, frames: new Map(), ms: 0 };
    mdl.ms = Date.now() - t0;
    MODELS.set(mk, mdl); if (MODELS.size > 14) MODELS.delete(MODELS.keys().next().value);
    return mdl;
  }

  // ---- a frame: one wind pose, composited ----------------------------------------------------------
  function frame(key, o) {
    o = o || {};
    const mdl = model(key, o), W0 = o.wind || {};
    const Wn = { w: clamp(+W0.w || 0, 0, 1), gust: W0.gust == null ? 0.4 : clamp(+W0.gust, 0, 1), dir: W0.dir < 0 ? -1 : 1 };
    const fi = o.phase != null ? null : ((((o.frame | 0) % LOOP) + LOOP) % LOOP);
    const ph = o.phase != null ? (((+o.phase) % 1) + 1) % 1 : fi / LOOP;
    const fk = Math.round(Wn.w * 100) + ',' + Math.round(Wn.gust * 100) + ',' + Wn.dir + ',' + Math.round(ph * 1600);
    const hit = mdl.frames.get(fk); if (hit) return hit;
    const cell = mdl.cell, Wd = cell.w, Hd = cell.h, N = Wd * Hd, v = new Vol(Wd, Hd);
    v.st = new Int32Array(N).fill(-1); v.ao = new Float32Array(N); v.part = new Int16Array(N).fill(-1); v.r = new Float32Array(N);
    const lp = [];
    for (let k = 0; k < mdl.limbs.length; k++) {
      const L = mdl.limbs[k], da = windAt(mdl, Wn, L.a[0], L.a[1], L.a[2], L.m, ph), db = windAt(mdl, Wn, L.b[0], L.b[1], L.b[2], L.m, ph);
      const q = [L.sa[0] + da[0], L.sa[1] + da[1], L.sa[2], L.sb[0] + db[0], L.sb[1] + db[1], L.sb[2], L.r0, L.r1];
      limb4(v, q[0], q[1], q[2], q[3], q[4], q[5], L.r0, L.r1, L.mat, k, L.m);
      lp.push(q);
    }
    // FLUTTER (4.1). Pass 4 jumped whole masses a pixel at random, a floret at a time. Now nothing larger
    // than a leaf moves on its own. The wind field is sampled on a 4 px grid over each mass: the mass's
    // between-leaves base is warped through it pixel by pixel, and every LEAF is laid at the field under
    // its own centre, rounded with its own dither, so a sway crosses a crown leaf by leaf instead of a
    // mass at a time. On top, each leaf has two flutter slots a loop — a 1–2-frame push downwind, lift
    // or recoil, taken more often where the gust is passing — and deciduous leaves two turn-over slots
    // (the pale underside), also taken while they flutter.
    const sp = mdl.sp, g = mdl.g, wv = windOf(sp), shS = sp.shimmer, dir = Wn.dir, cR = Math.max(8, g.crownR), pv = mdl.pivot;
    const f16 = Math.floor(ph * LOOP) % LOOP, resp = smooth(0, 0.85, Wn.w), GS = 4;
    const dRate = Math.min(1, (wv[2] * resp * 0.35 + (shS ? shS[0] * shS[1] * 0.03 : 0)) * LOOP / 2.9);
    const tRate = shS ? Math.min(1, shS[0] * (shS[1] + (1 - shS[1]) * resp) * 0.10 * LOOP / 2.9) : 0;
    const offs = new Int16Array(mdl.parts.length * 2), flut = new Uint8Array(mdl.stampN + 1), fv = [0, 0];
    let moved = 0, FX = new Float32Array(4096), FY = new Float32Array(4096);
    for (let p = 0; p < mdl.parts.length; p++) {
      const P = mdl.parts[p], an = P.anchor, pw = P.w, pH = P.h, px0 = P.x0, py0 = P.y0;
      const d0 = windAt(mdl, Wn, an[0], an[1], an[2], P.m, ph), ox0 = Math.round(d0[0]), oy0 = Math.round(d0[1]);
      offs[p * 2] = ox0; offs[p * 2 + 1] = oy0;
      const gx0 = px0 - 3, gy0 = py0 - 3, GW = Math.ceil((pw + 6) / GS) + 1, GH = Math.ceil((pH + 6) / GS) + 1, GN = GW * GH;
      if (GN > FX.length) { FX = new Float32Array(GN); FY = new Float32Array(GN); }
      let fx0 = 0, fx1 = 0, fy0 = 0, fy1 = 0;
      if (Wn.w > 0) {
        fx0 = fy0 = 1e9; fx1 = fy1 = -1e9;
        for (let gy = 0; gy < GH; gy++) for (let gx = 0; gx < GW; gx++) {
          const Yr = gy0 + gy * GS - pv.y, e = windAt(mdl, Wn, gx0 + gx * GS - cell.dx, g.baseY - (SE * P.zc - CE * Yr), SE * Yr + CE * P.zc, P.m, ph), b = gy * GW + gx;
          FX[b] = e[0]; FY[b] = e[1];
          if (e[0] < fx0) fx0 = e[0]; if (e[0] > fx1) fx1 = e[0]; if (e[1] < fy0) fy0 = e[1]; if (e[1] > fy1) fy1 = e[1];
        }
      } else { FX.fill(0, 0, GN); FY.fill(0, 0, GN); }
      const fAt = (X, Y) => {
        const u = clamp((X - gx0) / GS, 0, GW - 1.001), t = clamp((Y - gy0) / GS, 0, GH - 1.001), iu = u | 0, it = t | 0, au = u - iu, at = t - it, b = it * GW + iu;
        fv[0] = (FX[b] + (FX[b + 1] - FX[b]) * au) * (1 - at) + (FX[b + GW] + (FX[b + GW + 1] - FX[b + GW]) * au) * at;
        fv[1] = (FY[b] + (FY[b + 1] - FY[b]) * au) * (1 - at) + (FY[b + GW] + (FY[b + GW + 1] - FY[b + GW]) * au) * at;
      };
      // the base, warped: every destination pixel looks back through the field to the mass at rest
      const X0 = Math.max(0, px0 + Math.floor(fx0) - 1), X1 = Math.min(Wd, px0 + pw + Math.ceil(fx1) + 1);
      const Y0 = Math.max(0, py0 + Math.floor(fy0) - 1), Y1 = Math.min(Hd, py0 + pH + Math.ceil(fy1) + 1);
      for (let DY = Y0; DY < Y1; DY++) for (let DX = X0; DX < X1; DX++) {
        fAt(DX - ox0 + 0.5, DY - oy0 + 0.5);
        const lx = DX - Math.round(fv[0]) - px0, ly = DY - Math.round(fv[1]) - py0;
        if (lx < 0 || ly < 0 || lx >= pw || ly >= pH) continue;
        const q = P.qOf[ly * pw + lx]; if (q < 0) continue;
        const i = DY * Wd + DX, z = P.z[q];
        if (z <= v.z[i]) continue;
        v.z[i] = z; v.nx[i] = P.nx[q]; v.ny[i] = P.ny[q]; v.nz[i] = P.nz[q]; v.mat[i] = M.FOLIAGE; v.a[i] = 1;
        v.id[i] = P.id[q]; v.mid[i] = P.m; v.st[i] = -1; v.ao[i] = P.ao[q]; v.part[i] = p; v.r[i] = 0;
      }
      // the leaves, in paint order
      for (let k = 0; k < P.sn; k++) {
        const gs = P.sb + k + 1;
        fAt(P.scs[k * 2], P.scs[k * 2 + 1]);
        let sx = Math.round(fv[0] + (hsh(gs, 8) - 0.5) * 0.8), sy = Math.round(fv[1] + (hsh(gs, 9) - 0.5) * 0.8), mv = false;
        if (dRate > 0) for (let e = 0; e < 2; e++) {
          const s0 = (hsh(gs, 10 + e) * LOOP) | 0;
          if ((f16 - s0 + LOOP) % LOOP >= (hsh(gs, 12 + e) < 0.55 ? 1 : 2)) continue;
          if (hsh(gs, 14 + e) >= dRate * (1 + Wn.gust * 0.9 * Math.sin(TAU * s0 / LOOP + 0.7 - dir * (P.sc[k * 3] - g.cx) / cR * 1.3))) continue;
          const r = hsh(gs, 16 + e);
          if (g.conifer) sx += r < 0.8 ? dir : -dir;
          else if (r < 0.55) sx += dir; else if (r < 0.75) sy -= 1; else if (r < 0.9) { sx += dir; sy -= 1; } else sx -= dir;
          mv = true; moved++; flut[gs] |= 1; break;
        }
        if (tRate > 0) {
          let tn = mv && hsh(gs, 18) < shS[0] * 0.5;
          for (let e = 0; e < 2 && !tn; e++) { const s0 = (hsh(gs, 20 + e) * LOOP) | 0; if ((f16 - s0 + LOOP) % LOOP < (hsh(gs, 22 + e) < 0.5 ? 1 : 2) && hsh(gs, 24 + e) < tRate) tn = true; }
          if (tn) flut[gs] |= 2;
        }
        const nx = P.snx[k], ny = P.sny[k], nz = P.snz[k], ao = P.sao[k], f1 = P.fpO[k + 1];
        for (let f = P.fpO[k]; f < f1; f++) {
          const q = P.fpQ[f], li = P.li[q], X = px0 + (li % pw) + sx, Y = py0 + ((li / pw) | 0) + sy;
          if (X < 0 || Y < 0 || X >= Wd || Y >= Hd) continue;
          const i = Y * Wd + X, z = P.z[q];
          if (v.part[i] === p) { if (z > v.z[i]) v.z[i] = z; }      // over its own mass: painter's order
          else if (z > v.z[i]) v.z[i] = z; else continue;             // over anything else: depth
          v.nx[i] = nx; v.ny[i] = ny; v.nz[i] = nz; v.mat[i] = M.FOLIAGE; v.a[i] = 1;
          v.id[i] = P.id[q]; v.mid[i] = P.m; v.st[i] = gs; v.ao[i] = ao; v.part[i] = p; v.r[i] = 0;
        }
      }
    }
    const despeckled = despeckle(v);
    const bury = new Uint8Array(N);
    for (let y = 0; y < Hd; y++) for (let x = 0; x < Wd; x++) {
      const i = y * Wd + x; if (!v.a[i] || v.mat[i] === M.FOLIAGE) continue;
      v.ao[i] = 0.45 + 0.55 * Math.exp(-opAt(mdl.upMap, x + 0.5 + v.nx[i] * 1.5, y + 0.5 + v.ny[i] * 1.5, v.z[i] + v.nz[i] * 1.5));
      let f = 0, t = 0;
      for (const [dx, dy] of BRING) { const jx = x + dx, jy = y + dy; if (jx < 0 || jy < 0 || jx >= Wd || jy >= Hd) continue; t++; const j = jy * Wd + jx; if (v.a[j] && v.mat[j] === M.FOLIAGE) f++; }
      bury[i] = !t ? 0 : f * 2 >= t ? 2 : f * 3 >= t ? 1 : 0;
    }
    const fr = { mdl, v, D: distField(v.a, Wd, Hd), bury, ph, W: Wn, frame: fi, offs, lp, despeckled, flut, moved };
    mdl.frames.set(fk, fr); if (mdl.frames.size > 40) mdl.frames.delete(mdl.frames.keys().next().value);
    return fr;
  }
  function framePrims(fr) {
    if (fr._prims) return fr._prims;
    const mdl = fr.mdl, sig = mdl.sp.sigma || 0.06, pr = [];
    mdl.parts.forEach((P, p) => { const ox = fr.offs[p * 2], oy = fr.offs[p * 2 + 1]; for (const c of P.prims) pr.push({ x: c.x + ox, y: c.y + oy, z: c.z, a: c.a, b: c.b, c: c.c, sig }); });
    for (const q of fr.lp) limbSpheres(pr, q[0], q[1], q[2], q[3], q[4], q[5], q[6], q[7], 0.35);
    return (fr._prims = pr);
  }
  function sunMap(fr, Lv) {
    const k = Lv.map(q => Math.round(q * 400)).join(',');
    if (fr._smk !== k) { fr._sm = opMap(framePrims(fr), Lv, 2, 32); fr._smk = k; }
    return fr._sm;
  }
  // per-pixel sun transmission: 1 = the sun reaches it, through nothing
  function sunTrans(fr, sky) {
    const v = fr.v, N = v.w * v.h, T = new Float32Array(N);
    if (!((sky.sunI == null ? 1 : sky.sunI) > 0.01)) return T;
    const SM = sunMap(fr, sky.sunV);
    for (let y = 0; y < v.h; y++) for (let x = 0; x < v.w; x++) {
      const i = y * v.w + x; if (!v.a[i]) continue;
      T[i] = Math.exp(-opAt(SM, x + 0.5 + v.nx[i] * 2, y + 0.5 + v.ny[i] * 2, v.z[i] + v.nz[i] * 2));
    }
    return T;
  }
  // a leaf's sun transmission, read from the REST pose once per model and sun direction: leaves move,
  // what shades them hardly does, and a leaf that has not moved must not change band
  function restT(mdl, Lv) {
    const key = Lv.map(q => Math.round(q * 400)).join(',');
    if (mdl._rtk === key) return mdl._rt;
    const sig = mdl.sp.sigma || 0.06, pr = [];
    for (const P of mdl.parts) for (const c of P.prims) pr.push({ x: c.x, y: c.y, z: c.z, a: c.a, b: c.b, c: c.c, sig });
    for (const L of mdl.limbs) limbSpheres(pr, L.sa[0], L.sa[1], L.sa[2], L.sb[0], L.sb[1], L.sb[2], L.r0, L.r1, 0.35);
    const SM = opMap(pr, Lv, 2, 32), T = new Float32Array(mdl.stampN + 1);
    for (const P of mdl.parts) for (let k = 0; k < P.sn; k++) {
      const f0 = P.fpO[k], f1 = P.fpO[k + 1], nx = P.snx[k] * 2, ny = P.sny[k] * 2, nz = P.snz[k] * 2;
      let a = 0;
      for (let f = f0; f < f1; f++) { const q = P.fpQ[f], i = P.li[q]; a += Math.exp(-opAt(SM, P.x0 + (i % P.w) + 0.5 + nx, P.y0 + ((i / P.w) | 0) + 0.5 + ny, P.z[q] + nz)); }
      T[P.sb + k + 1] = a / Math.max(1, f1 - f0);
    }
    mdl._rtk = key; mdl._rt = T;
    return T;
  }

  // ---- relight -------------------------------------------------------------------------------------
  /* The pass-3 shade with the light taken out of the constants. Per STAMP: sky (visibility × how much
     the stamp faces up) + sun (N·L × transmission) + back light through thin foliage; banded on the
     pass-3 steps; then the stamp's tone break, its SEAM on the side away from this sun and its TIP on
     the side toward it; contact crevices; bark grain on wood. Weather: snow settles on up-facing
     stamps and limb tops, wet darkens bark a band and puts glints on lit tips, fog is ground-heavy.
     The grade is harmony's (ambient into the low bands, key into the lit ones, wash), cached per
     (ramp, band, lit, fog step, special, wet) — a finite palette for any sky. */
  function relight(fr, sky, o) {
    o = o || {}; sky = sky || REF_SKY;
    const mdl = fr.mdl, sp = mdl.sp, v = fr.v, w = v.w, h = v.h, N = w * h, D = fr.D, G = mdl.G;
    const Lv = sky.sunV || REF_SKY.sunV, sunI = sky.sunI == null ? 1 : sky.sunI, skyI = sky.skyI == null ? 0.6 : sky.skyI, expo = sky.expo || 1;
    const grade = sky.grade !== false && o.grade !== false, snow = sky.snow || 0, wet = sky.wet || 0, fog = sky.fog || 0;
    const T = o.T || sunTrans(fr, sky), nS = mdl.stampN + 1;
    const tS = sunI > 0.01 ? restT(mdl, Lv) : new Float32Array(nS);
    let lx = Lv[0], ly = Lv[1]; const ll = Math.hypot(lx, ly);
    if (ll < 0.18) { lx = -0.6; ly = -0.8; } else { lx /= ll; ly /= ll; }
    const ax = lx > 0.38 ? -1 : lx < -0.38 ? 1 : 0, ay = ly > 0.38 ? -1 : ly < -0.38 ? 1 : 0;   // the side AWAY from the light
    const tipI = new Int32Array(nS).fill(-1), tipV = new Float32Array(nS).fill(-1e9), zmin = mdl.zmin, zmax = mdl.zmax;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i]) continue;
      const s = v.st[i]; if (s < 0) continue;
      const val = x * lx + y * ly; if (val > tipV[s]) { tipV[s] = val; tipI[s] = i; }
    }
    const zr = Math.max(1, zmax - zmin), kS = 0.36, kD = 1.0, kT = 0.62 * (sp.trans == null ? 0.8 : sp.trans);
    // light off the ground (more off snow) into everything that faces down or sideways
    const sunW = sky.sunW || REF_SKY.sunW, gB = 0.13 * (sunI * Math.max(0, sunW[2]) + 0.35 * skyI) * (1 + 0.8 * snow);
    const pivY = mdl.pivot.y, Hs = mdl.g.H, RAMP = [mdl.FOL, mdl.BARK, mdl.BARKB, SNOW, mdl.UNDER];
    const kc = h2r(sky.kc || '#ffffff'), ac = h2r(sky.ac || '#000000'), wc = h2r(sky.wash || '#ffffff'), fc = h2r(sky.fogC || '#c3cdce');
    const cache = new Map();
    const colour = (rid, bi, lit, fq, spc, wk) => {
      const key = ((((rid * 5 + bi) * 2 + lit) * 5 + fq) * 3 + spc) * 3 + wk;
      let c = cache.get(key); if (c) return c;
      const R = RAMP[rid];
      const r = h2r(spc === 1 ? mix(R.key, sky.kc || '#ffe0b0', 0.55) : spc === 2 ? mix(R.key, '#ffffff', 0.6) : R[BANDS[bi]]);
      if (grade) {
        const u = bi / 4, ta = (sky.aa || 0) * (1 - u) * (1 - (sky.amb || 0) * 0.35), tk = (sky.ka || 0) * u * (lit ? 1 : 0.3), wd = wk === 2 ? 0.30 : wk === 1 ? 0.12 : 0;
        for (let k = 0; k < 3; k++) {
          r[k] += (ac[k] - r[k]) * ta; r[k] += (kc[k] - r[k]) * tk; r[k] *= 1 - wd;
          if (sky.wa) r[k] += (wc[k] - r[k]) * sky.wa;
          if (fq) r[k] += (fc[k] - r[k]) * fq * 0.17;
        }
      }
      c = [clamp(Math.round(r[0]), 0, 255), clamp(Math.round(r[1]), 0, 255), clamp(Math.round(r[2]), 0, 255)];
      cache.set(key, c); return c;
    };
    const stAt = (x, y) => { if (x < 0 || y < 0 || x >= w || y >= h) return -2; const j = y * w + x; return v.a[j] && v.mat[j] === M.FOLIAGE ? v.st[j] : -2; };
    const BG = sp.barkGrain || 'plate', sd = (sp.key.length * 13 + Math.round(sp.real * 7)) % 97;
    const flip = G.flip || 0, backlit = Lv[2] < -0.12 && sunI > 0.05;
    // SHIMMER — a leaf frame() has turned over shows its pale underside; a lit one can catch the sun.
    // (sp.shimmer = [amount, still-air share]: the aspen trembles when nothing else moves.)
    const flut = fr.flut, fi = Math.floor(fr.ph * LOOP);
    const out = new Uint8ClampedArray(N * 4);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = y * w + x; if (!v.a[i]) continue;
      const nx = v.nx[i], ny = v.ny[i], nz = v.nz[i], zn = clamp((v.z[i] - zmin) / zr, 0, 1), mat = v.mat[i], s = v.st[i], fol = mat === M.FOLIAGE;
      const t = fol && s >= 0 ? tS[s] : T[i];
      const nl = nx * Lv[0] + ny * Lv[1] + nz * Lv[2], up = nx * UPV[0] + ny * UPV[1] + nz * UPV[2];
      const sun = kD * sunI * (nl > 0 ? Math.pow(nl, 1.25) : 0) * t;
      const L = ((0.74 + 0.26 * zn) * (kS * skyI * (0.30 + 0.70 * v.ao[i]) * (0.45 + 0.55 * clamp(up, 0, 1)) + sun + (fol ? kT * sunI * (nl < 0 ? Math.pow(-nl, 0.7) : 0) * t : 0)) + gB * (0.5 - 0.5 * up) * (0.5 + 0.5 * v.ao[i])) * expo;
      const lit = sun > 0.22 ? 1 : 0;
      let rid = 0, bi, spc = 0, wk = 0;
      if (fol) {
        const mB = (vnoise((v.mid[i] & 511) * 5 + 3, (v.mid[i] & 511) * 9 + 7, 29) - 0.5) * 0.06;
        if (s >= 0) {
          bi = bandF(L + mB);
          const hj = vnoise(s & 8191, (s >>> 13) & 8191, 19);
          let fp = flip > 0 && hj > 1 - flip, turned = false;
          if (flut && (flut[s] & 2)) { fp = !fp; turned = true; }
          if (fp) { rid = 4; bi += 1; } else if (hj < G.tone) bi -= 1; else if (hj > 1 - G.tone) bi += 1;
          bi = clamp(bi, 0, 4);
          const b0 = bi, sA = ax ? stAt(x + ax, y) : -2, sB = ay ? stAt(x, y + ay) : -2, dA = sA !== -2 && sA !== s, dB = sB !== -2 && sB !== s;
          if (b0 >= 2 && (dA || dB)) bi -= (G.corner && b0 >= 3 && dA && dB) ? 2 : 1;
          else if (b0 >= 3 && tipI[s] === i) {
            bi += 1;
            if ((wet > 0.25 && lit && vnoise(s & 8191, 3, 17) < wet * 0.6) || (turned && lit && vnoise(s & 8191, fi * 7 + 1, 23) < 0.5)) spc = 2;
          }
        } else bi = bandF(L + mB) - 1;                                    // the dark between the leaves
        if (wet > 0.15) wk = 1;
      } else {
        bi = bandIdx(L - (sp.pale && mat === M.TWIG ? 0.30 : 0)) - fr.bury[i];
        rid = fr.bury[i] ? 2 : 1;
        if (v.r[i] >= 1.6) {
          if (BG === 'furrow') { const sn = vnoise(Math.floor((x + Math.floor(y / 11)) / 2), 3, sd + 11); if (sn > 0.62) bi += 1; else if (sn < 0.30) bi -= 1; }
          else if (BG === 'plate') { const col = Math.floor((x + Math.floor(y / 14)) / 3), sn = vnoise(col, Math.floor(y / 14), sd + 11); if (sn > 0.78) bi += 1; else if (sn < 0.22) bi -= 1; if (((y + col * 5) % 14) === 0 && vnoise(col, 9, sd) > 0.4) bi -= 1; }
          else if (BG === 'scale') { const sn = vnoise(Math.floor(x / 2), Math.floor(y / 3), sd + 11); if (sn > 0.80) bi += 1; else if (sn < 0.24) bi -= 1; }
          else if (BG === 'shred') { const sn = vnoise(Math.floor(x / 1.5), Math.floor(y / 22), sd + 11); if (sn > 0.84) bi += 1; else if (sn < 0.18) bi -= 1; }
          else if (BG === 'smooth') { if (vnoise(Math.floor(x / 6), Math.floor(y / 9), sd + 11) > 0.93 && (x % 6) < 2 && (y % 9) < 2) bi -= 2; }
        }
        if (BG === 'paper' && mat === M.BARK && !fr.bury[i]) { const bY = Math.floor(y / 5), seg = Math.floor((x + bY * 3) / 7); if (vnoise(seg, bY, sd + 23) > 0.74 && (y % 5) < 2) bi -= 2; }
        if (wet > 0.4) bi -= 1;
        wk = wet > 0.5 ? 2 : wet > 0.15 ? 1 : 0;
      }
      // contact crevices — a nearer primitive beside this pixel — and the lit lip of a nearer mass
      let crev = 0;
      for (const [dx, dy] of N4) {
        const jx = x + dx, jy = y + dy; if (jx < 0 || jy < 0 || jx >= w || jy >= h) continue;
        const j = jy * w + jx; if (!v.a[j]) continue;
        if (v.id[j] !== v.id[i] && v.z[j] > v.z[i] + 1.0) crev = Math.max(crev, (mdl.g.conifer && v.mid[j] !== v.mid[i] && v.z[j] > v.z[i] + 2.5) ? 2 : 1);
      }
      if (crev) bi -= crev;
      else if (lit && (ax || ay)) { const jx = x - ax, jy = y - ay; if (jx >= 0 && jy >= 0 && jx < w && jy < h) { const j = jy * w + jx; if (v.a[j] && v.mid[j] !== v.mid[i] && v.z[j] < v.z[i] - 1.5) bi += 1; } }
      if (snow > 0.02) {
        const hs = s >= 0 ? vnoise(s & 8191, 7, 91) : vnoise(x >> 1, y >> 1, 91), thr = 1.02 - snow * 0.95 + (hs - 0.5) * 0.34;
        if (up > thr && v.ao[i] > 0.2 && (!fol || s >= 0 || snow > 0.6)) { rid = 3; bi = clamp(bi + 1, 1, 4); wk = 0; }
      }
      if (backlit && sunI > 0.15 && D[i] <= 1.2 && T[i] > 0.6 && rid !== 3) {
        const pl = Math.hypot(nx, ny) || 1, pp = v.part[i];
        if ((nx * lx + ny * ly) / pl > 0.45 && (!fol || (pp >= 0 && mdl.parts[pp].maxD * 2 >= MIN_BODY + 2 * RIM_PX))) { spc = 1; bi = 4; }
      }
      let fq = 0;
      if (fog > 0.01) { const hf = clamp((-(y + 0.5 - pivY) * CE + v.z[i] * SE) / Hs, 0, 1); fq = Math.round(clamp(fog * (0.62 + 0.38 * (1 - hf)), 0, 1) * 4); }
      const c = colour(rid, clamp(bi, 0, 4), lit, fq, spc, wk);
      out[i * 4] = c[0]; out[i * 4 + 1] = c[1]; out[i * 4 + 2] = c[2]; out[i * 4 + 3] = 255;
    }
    return out;
  }

  // ---- cast shadow on the ground ---------------------------------------------------------------------
  /* In CELL coordinates, may reach well outside the cell. Levels: 3 full sun shadow · 2 partial (the
     crown's thinner parts, sun flecks' edges, the trunk-foot contact) · 1 canopy shade — the sky
     occlusion straight down, which is what stays under a tree on an overcast day. A ground texel's
     world point is on the plane h = 0; the sun map's column through it is everything between it and
     the sun. */
  function castShadow(fr, sky) {
    sky = sky || REF_SKY;
    const mdl = fr.mdl, g = mdl.g, piv = mdl.pivot, sunI = sky.sunI == null ? 1 : sky.sunI, Lw = sky.sunW || REF_SKY.sunW;
    const sunOK = sunI > 0.04 && Lw[2] > 0.03, R = g.crownR + 6;
    let xa = piv.x - R, xb = piv.x + R, ya = piv.y - R * SE - 4, yb = piv.y + R * SE + 6;
    if (sunOK) {
      const k = 1 / Math.max(Lw[2], 0.14), ex = -Lw[0] * k * g.H, ey = -Lw[1] * k * g.H * SE;
      xa = Math.min(xa, piv.x + ex - R); xb = Math.max(xb, piv.x + ex + R); ya = Math.min(ya, piv.y + ey - R * SE - 4); yb = Math.max(yb, piv.y + ey + R * SE + 6);
    }
    const X0 = Math.floor(xa), Y0 = Math.floor(ya), W = Math.ceil(xb) - X0 + 1, H = Math.ceil(yb) - Y0 + 1, lv = new Uint8Array(W * H);
    const SM = sunOK ? sunMap(fr, sky.sunV) : null, UM = mdl.upMap, tr = g.trunkR, strong = sunI > 0.25;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const X = X0 + x + 0.5, Y = Y0 + y + 0.5, Z = (Y - piv.y) / SE * CE;
      let L = 0;
      if (SM) { const T = Math.exp(-opAt(SM, X, Y, Z)); if (T < 0.32) L = strong ? 3 : 2; else if (T < 0.64) L = 2; }
      if (L < 1 && Math.exp(-opAt(UM, X, Y, Z)) < 0.34) L = 1;
      const dx = (X - piv.x - 0.5) / (tr * 2.4 + 2), dy = (Y - piv.y - 0.5) / (tr * 0.9 + 1.5);   // foot = centre of the pivot column
      if (dx * dx + dy * dy < 1 && L < 2) L = 2;
      lv[y * W + x] = L;
    }
    return { x0: X0, y0: Y0, w: W, h: H, lv };
  }

  // ---- channel views + packs -----------------------------------------------------------------------
  function view(fr, ch, sky) {
    if (ch === 'lit' || !ch) return relight(fr, sky || REF_SKY);
    if (ch === 'unlit') return relight(fr, UNLIT_SKY);
    const v = fr.v, N = v.w * v.h, out = new Uint8ClampedArray(N * 4), mdl = fr.mdl;
    const T = ch === 'sun' ? sunTrans(fr, sky || REF_SKY) : null, trans = mdl.sp.trans == null ? 0.8 : mdl.sp.trans;
    let zmin = 1e9, zmax = -1e9; for (let i = 0; i < N; i++) if (v.a[i]) { if (v.z[i] < zmin) zmin = v.z[i]; if (v.z[i] > zmax) zmax = v.z[i]; }
    for (let i = 0; i < N; i++) {
      if (!v.a[i]) continue;
      const y = (i / v.w) | 0, fol = v.mat[i] === M.FOLIAGE, hw = clamp((-(y + 0.5 - mdl.pivot.y) * CE + v.z[i] * SE) / (mdl.g.H * 1.02), 0, 1);
      const th = fol ? (1 - clamp(fr.D[i] / 7, 0, 1)) * trans : 0;
      let r, g, b;
      if (ch === 'normal') { r = (v.nx[i] * 0.5 + 0.5) * 255; g = (-v.ny[i] * 0.5 + 0.5) * 255; b = (v.nz[i] * 0.5 + 0.5) * 255; }
      else if (ch === 'ao') r = g = b = v.ao[i] * 255;
      else if (ch === 'trans') { r = th * 255; g = th * 232; b = th * 120; }
      else if (ch === 'height') r = g = b = hw * 255;
      else if (ch === 'depth') r = g = b = (v.z[i] - zmin) / Math.max(1, zmax - zmin) * 255;
      else if (ch === 'sun') { r = 24 + T[i] * 231; g = 22 + T[i] * 200; b = 30 + T[i] * 120; }
      else if (ch === 'stamps') { const s = v.st[i]; if (s < 0) { r = fol ? 18 : 62; g = fol ? 22 : 74; b = fol ? 26 : 82; } else { r = 60 + 190 * hsh(s, 31); g = 60 + 190 * hsh(s, 32); b = 60 + 190 * hsh(s, 33); } }
      else if (ch === 'parts') { const p = v.part[i]; if (p < 0) { r = 214; g = 168; b = 104; } else { r = 50 + 200 * vnoise(p * 3 + 1, 7, 3); g = 50 + 200 * vnoise(p * 3 + 2, 11, 6); b = 50 + 200 * vnoise(p * 3 + 3, 13, 9); } }
      else if (ch === 'wood') { const c = v.mat[i] === M.BARK ? [214, 168, 104] : v.mat[i] === M.TWIG ? [232, 208, 160] : [34, 52, 48]; r = c[0]; g = c[1]; b = c[2]; }
      else if (ch === 'light') { r = v.ao[i] * 255; g = th * 255; b = hw * 255; }
      else if (ch === 'detail') { const s = v.st[i] + 1; r = s & 255; g = (s >> 8) & 255; b = fol ? 255 : v.mat[i] === M.BARK ? 170 : 85; }
      else { r = g = b = 128; }
      out[i * 4] = r; out[i * 4 + 1] = g; out[i * 4 + 2] = b; out[i * 4 + 3] = 255;
    }
    return out;
  }

  // ---- public ----------------------------------------------------------------------------------------
  // the pass-3 surface: a lit sprite (REF_SKY unless o.sky), its masks, and the rule audit
  function render(key, o) {
    o = o || {};
    const fr = frame(key, o), v = fr.v, mdl = fr.mdl, sky = o.sky || REF_SKY, N = v.w * v.h;
    const rgba = relight(fr, sky), front = new Uint8Array(N), depth = new Uint8Array(N), K = REF_SKY.sunV;
    let zmin = 1e9, zmax = -1e9; for (let i = 0; i < N; i++) if (v.a[i]) { if (v.z[i] < zmin) zmin = v.z[i]; if (v.z[i] > zmax) zmax = v.z[i]; }
    for (let i = 0; i < N; i++) if (v.a[i]) { front[i] = clamp(Math.round((v.nx[i] * K[0] + v.ny[i] * K[1] + v.nz[i] * K[2]) * 255), 0, 255); depth[i] = Math.round((v.z[i] - zmin) / Math.max(1, zmax - zmin) * 255); }
    const audit = massReport(v.a, fr.D, v.w, v.h, v.mat), sp = mdl.sp;
    return { w: v.w, h: v.h, pivot: { x: mdl.pivot.x, y: mdl.pivot.y }, rgba, masks: { front, rim: new Uint8Array(N), depth },
      alpha: v.a, dist: fr.D, mat: v.mat, nx: v.nx, ny: v.ny, nz: v.nz, mid: v.mid, unit: v.st, frame: fr,
      species: sp, season: mdl.season, variant: mdl.variant, size: mdl.size, stage: mdl.stage,
      report: { pass: audit.pass, masses: audit.masses, failed: audit.failed, minBody: audit.minBody, bodyRatio: audit.bodyRatio, despeckled: fr.despeckled,
        parts: mdl.parts.length, stamps: mdl.stampN, leafPx: mdl.leafPx, fluttering: fr.moved || 0, limbs: mdl.limbs.length, buildMs: mdl.ms,
        metres: Math.round(sp.worldH * mdl.size / PPU * 10) / 10, trueMetres: Math.round(sp.real * mdl.size * 10) / 10, trunkPx: Math.round(mdl.g.trunkR * 20) / 10 } };
  }
  // the wind loop as a 4 × 4 sheet, one channel
  function sheet(key, o, ch, sky) {
    const f0 = frame(key, Object.assign({}, o, { frame: 0 })), cw = f0.v.w, chh = f0.v.h, W = cw * 4, H = chh * (LOOP / 4), out = new Uint8ClampedArray(W * H * 4);
    for (let f = 0; f < LOOP; f++) {
      const px = view(frame(key, Object.assign({}, o, { frame: f })), ch || 'lit', sky), ox = (f % 4) * cw, oy = Math.floor(f / 4) * chh;
      for (let y = 0; y < chh; y++) out.set(px.subarray(y * cw * 4, (y + 1) * cw * 4), ((oy + y) * W + ox) * 4);
    }
    return { w: W, h: H, rgba: out, cell: [cw, chh], pivot: [f0.mdl.pivot.x, f0.mdl.pivot.y], frames: LOOP };
  }
  function sheetSpec(key, size) {
    const sp = byKey[key] || SPECIES[0], t = size == null ? 1 : size, cell = cellOf(sp, t), rows = LOOP / 4;
    return { cell: [cell.w, cell.h], cols: 4, rows, frames: LOOP, w: cell.w * 4, h: cell.h * rows, fits: cell.w * 4 <= 2048 && cell.h * rows <= 2048, ppu: PPU, scale: SCALE,
      pivot: [cell.pivotX, cell.pivotY], pad: cell.pad, elev: ELEV, stage: stageName(t), windReach: windReach(sp, t),
      metres: Math.round(sp.worldH * t / PPU * 10) / 10, trueMetres: Math.round(sp.real * t * 10) / 10 };
  }
  function clearCache() { MODELS.clear(); SPECIES.forEach(s => { s._cells = null; }); }

  root.TreeRig4 = {
    PPU, SCALE, M2PX, RIM_PX, MIN_BODY, MIN_R, LOOP, VARIANTS, SEASONS, SPECIES, byKey, KEYLINE_DEFAULT, COLD, WARM, ELEV, CE, SE, UPV,
    STAGES, STAGE_KEYS, sizeOf, stageName, REF_SKY, UNLIT_SKY, toView, dirOf,
    model, frame, relight, castShadow, sunTrans, view, render, sheet, sheetSpec, cellOf, windAt, windOf, windReach, clearCache,
    folRamp, barkRamp, M, GRAINS, grainOf, EDGES, edgeOf, STENCILS, BANDS,
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
