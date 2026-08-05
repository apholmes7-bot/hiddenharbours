/* docs/art/rigs/grassRig.js — Hidden Harbours · the GRASS HABITAT LIBRARY (STATIC sprites)
 *
 * ⚠️ THIS FILE IS *NOT* AN ART-DIRECTOR DROP. Unlike its neighbours it is authored in-repo, by PR,
 * in the art-pipeline lane — the tree / shrub / flower precedent. `grassSpeciesRig.js` beside it IS
 * a verbatim drop and must not be edited; this file COMPOSES on it.
 *
 * WHY COMPOSE INSTEAD OF COPY. The species drop already owns the hard part: cubic-Bezier blades
 * stamped across the local tangent (so a 2 px blade stays 2 px wide round an arch), the mound root
 * mass, the tip-referenced value ramp, and the `validate()` that proves the shader contract. Copying
 * ~250 lines of that here would give us a second grass renderer that drifts from the first, and the
 * two would disagree about the ramp — which is exactly the failure the tint knob cannot survive.
 * So this file adds only what the drop does not have: the HABITAT SET, on the BASE ramp.
 *
 *   grassSpeciesRig.js  five marsh / meadow SPECIES  (cattail · rush · sedge · saltmeadow · timothy)
 *   grassRig.js         the HABITATS St Peters needs (meadow · clump · fringe · marram · headland)
 *
 * ── THE CONTRACT (identical to the three shipped tufts — the shader is what enforces it) ────────
 *
 * `HiddenHarboursGrass.shader` bends tips in the VERTEX stage, and the bend weight is the sprite's
 * own UV.y² — 0 at the canvas bottom edge, 1 at the canvas top. Four consequences, and every
 * variant below obeys all four:
 *
 *   1. ROOT ON THE BOTTOM EDGE. A blade that starts a row up is a blade that shears off in wind.
 *   2. CLIMB IS THE SWAY DIAL. How far a variant's blades reach up the canvas *is* how much bend it
 *      takes. A short tuft is stiff and a tall one is lively for free — there is no per-variant
 *      wind knob and this file does not want one.
 *   3. NOTHING DETACHED. Every lit pixel 8-connects down to the bottom edge (asserted).
 *   4. THE EXACT RAMP, ALWAYS: #283a22 #3a542a #567834 #7ca248 #aac660. The runtime lush→straw knob
 *      MULTIPLIES a tint over this. An off-ramp variant becomes a colour island the moment a field
 *      is tinted, so `SPECIES.grass` (hue +0°, sat ×1.00 — the base ramp verbatim) is the ONLY
 *      species this file uses. Marram and headland read dry by being TINTED, not by being repainted.
 *
 * Hard alpha (0 or 255), PPU 32, bottom-centre pivot, widths in multiples of 32, no soil / shadow /
 * outline pixels. STATIC — one frame per variant, no pose sheet. The shader animates.
 *
 * ── USE ────────────────────────────────────────────────────────────────────────────────────────
 *
 *     <script src="grassSpeciesRig.js"></script>   // REQUIRED FIRST — this file throws without it
 *     <script src="grassRig.js"></script>
 *
 *     GrassRig.VARIANTS                  the habitat set (name, size, class, habitat, stiffness)
 *     GrassRig.render(spec)              -> Buf, the drop's own renderer
 *     GrassRig.toRGBA(buf)               -> Uint8ClampedArray, base ramp
 *     GrassRig.validate(buf, spec)       -> { lit, rooted, detached, climb, offRamp }
 *     GrassRig.bake(name)                -> { name, w, h, rgba, report } for one variant
 *     GrassRig.manifest()                the machine-readable catalog the engine imports
 *
 * `manifest()` is the deliverable the tool reads. It carries, per variant: canvas size, height CLASS
 * (short / medium / tall — the paint tool's existing three-way mix), HABITAT tags (what ground this
 * belongs on), measured CLIMB, and STIFFNESS. Nothing downstream restates any of it.
 */
(function (global) {
  'use strict';

  const Sp = global.GrassSpeciesRig;
  if (!Sp || typeof Sp.render !== 'function' || !Sp.SPECIES || !Sp.SPECIES.grass)
    throw new Error(
      'grassRig.js requires grassSpeciesRig.js to be loaded FIRST — it composes on that drop\'s ' +
      'renderer rather than carrying a second copy of it. Load order: grassSpeciesRig.js, then this.');

  /* ── height classes ─────────────────────────────────────────────────────────────────────────
     The paint tool has shipped a three-way Short / Medium / Tall mix since #102 and the owner uses
     it. Every variant declares which of the three it answers to, so the library can grow without
     the UI changing shape. The bands are CLIMB in pixels — measured off the bake, not asserted:
     short ≤ 20 · medium 21–34 · tall ≥ 35. `classOf()` is the one place that rule lives. */

  const CLASS_BANDS = { short: [0, 20], medium: [21, 34], tall: [35, 999] };

  function classOf(climb) {
    for (const k of ['short', 'medium', 'tall'])
      if (climb >= CLASS_BANDS[k][0] && climb <= CLASS_BANDS[k][1]) return k;
    return 'tall';
  }

  /* ── habitats ───────────────────────────────────────────────────────────────────────────────
     What ground a variant belongs on. A scatter pass reads these tags; it never reads a name.
     Tags are additive — `meadow` grass is also fine on a `headland`, which is why the dry look is
     a TINT rather than a separate silhouette for every habitat. */

  const HABITATS = {
    meadow:   'lush inland field — the default sward',
    sward:    'close-cropped turf; the short mix that carpets between the tall stuff',
    fringe:   'where grass gives out onto bare ground, rock or sand — thin, low, spread wide',
    dune:     'loose sand near the beach — marram, wispy and tall',
    headland: 'exposed, wind-raked and dry; reads right with the straw knob up',
    verge:    'path and track edges — wide low clumps',
  };

  /* ── the set ────────────────────────────────────────────────────────────────────────────────
     Fifteen silhouettes. Two to three ALTERNATES per look, because the island is about to be
     covered in these and a field of one silhouette repeated reads as wallpaper the moment the
     player walks along it. Alternates differ by seed AND by rake/blade count, not by seed alone —
     a re-seed of the same spec is recognisably the same plant.

     `stiff` is carried through from the drop's convention (1 = a normal tuft) for the manifest.
     Nothing in the engine reads it yet; it is what a later per-variant bend multiplier would use,
     and recording it at bake time costs nothing. */

  const VARIANTS = [
    /* ── meadow: SHORT ─ the carpet. Most of a dense field is these. ───────────────────────── */
    { name: 'GrassTuft_MeadowShortA', habitat: ['meadow', 'sward'], note: 'even fan, upright',
      w: 32, h: 32, seed: 211, rootRows: 2, stiff: 1,
      groups: [{ n: 11, root: [12, 20], tip: [5, 27], maxLen: 18, lenProf: [0.62, 0.88, 1, 0.9, 0.66],
                 fall: [1, 3], bow: [-3, 3], thick: [2, 1] }] },
    { name: 'GrassTuft_MeadowShortB', habitat: ['meadow', 'sward'], note: 'off-centre, leans left',
      w: 32, h: 32, seed: 223, rootRows: 2, baseBias: -0.2, stiff: 1,
      groups: [{ n: 9, root: [11, 18], tip: [2, 20], maxLen: 17, lenProf: [0.7, 0.95, 1, 0.8, 0.6],
                 fall: [2, 4], bow: [-5, -1], thick: [2, 1] }] },
    { name: 'GrassTuft_MeadowShortC', habitat: ['meadow', 'sward'], note: 'sparse, splayed wide',
      w: 32, h: 32, seed: 227, rootRows: 2, stiff: 1,
      groups: [{ n: 7, root: [13, 19], tip: [1, 30], maxLen: 16, lenProf: [0.8, 1, 0.86, 0.72],
                 fall: [3, 6], bow: [-6, 6], thick: [2, 1], jit: 0.24 }] },

    /* ── meadow: MEDIUM ─ the body of the sward, matching the shipped GrassTuft at climb 28. ── */
    { name: 'GrassTuft_MeadowMidA', habitat: ['meadow'], note: 'full fan, tips just nodding',
      w: 32, h: 32, seed: 233, rootRows: 3, stiff: 1,
      groups: [{ n: 13, root: [11, 21], tip: [3, 29], maxLen: 29, lenProf: [0.6, 0.86, 1, 0.92, 0.68],
                 fall: [2, 5], bow: [-4, 4], thick: [2, 1] }] },
    { name: 'GrassTuft_MeadowMidB', habitat: ['meadow'], note: 'two crowns, one taller',
      w: 32, h: 32, seed: 239, rootRows: 3, stiff: 1,
      groups: [{ n: 8, root: [9, 15], tip: [1, 17], maxLen: 28, lenProf: [0.66, 0.94, 1, 0.74],
                 fall: [2, 5], bow: [-5, 0], thick: [2, 1] },
               { n: 6, root: [18, 23], tip: [16, 30], maxLen: 23, lenProf: [0.8, 1, 0.8],
                 fall: [3, 6], bow: [1, 5], thick: [2, 1] }] },
    { name: 'GrassTuft_MeadowMidC', habitat: ['meadow'], note: 'raked right by a steady wind',
      w: 32, h: 32, seed: 241, rootRows: 3, baseBias: 0.25, stiff: 1,
      groups: [{ n: 11, root: [10, 18], tip: [8, 31], maxLen: 27, lenProf: [0.68, 0.92, 1, 0.86, 0.64],
                 fall: [3, 7], bow: [1, 7], thick: [2, 1] }] },

    /* ── meadow: TALL ─ the stuff you wade through. 32×48 so the climb has room. ───────────── */
    { name: 'GrassTuft_MeadowTallA', habitat: ['meadow'], note: 'deep fan, whole blades',
      w: 32, h: 48, seed: 251, rootRows: 3, stiff: 1,
      groups: [{ n: 15, root: [11, 21], tip: [2, 30], maxLen: 45, lenProf: [0.58, 0.84, 1, 0.9, 0.66],
                 fall: [3, 7], bow: [-5, 5], thick: [2, 1] }] },
    { name: 'GrassTuft_MeadowTallB', habitat: ['meadow'], note: 'tall spray over a short skirt',
      w: 32, h: 48, seed: 257, rootRows: 3, stiff: 1,
      groups: [{ n: 9, root: [12, 20], tip: [4, 28], maxLen: 43, lenProf: [0.7, 0.94, 1, 0.82],
                 fall: [4, 9], bow: [-4, 4], thick: [2, 1] },
               { n: 7, root: [10, 22], tip: [3, 29], maxLen: 22, lenProf: [0.8, 1, 0.78],
                 fall: [2, 5], bow: [-4, 4], thick: [2, 1] }] },

    /* ── low wide clumps ─ 64×32, the handoff's own size. One object covers two metres of verge
         instead of two objects; the paint pass leans on these where density has to come cheap. ── */
    { name: 'GrassTuft_ClumpWideA', habitat: ['meadow', 'verge'], note: '64×32 — two crowns, low',
      w: 64, h: 32, seed: 263, rootRows: 2, stiff: 0.9,
      groups: [{ n: 12, root: [14, 26], tip: [4, 34], maxLen: 22, lenProf: [0.64, 0.9, 1, 0.84, 0.62],
                 fall: [3, 7], bow: [-6, 4], thick: [2, 1] },
               { n: 11, root: [36, 50], tip: [28, 60], maxLen: 20, lenProf: [0.7, 1, 0.88, 0.6],
                 fall: [3, 8], bow: [-4, 6], thick: [2, 1] }] },
    { name: 'GrassTuft_ClumpWideB', habitat: ['meadow', 'verge'], note: '64×32 — one broad sweep',
      w: 64, h: 32, seed: 269, rootRows: 2, stiff: 0.9,
      groups: [{ n: 19, root: [8, 54], tip: [0, 63], maxLen: 19, lenProf: [0.56, 0.8, 1, 0.92, 0.74, 0.5],
                 fall: [4, 9], bow: [-7, 7], thick: [2, 1], jit: 0.22 }] },

    /* ── fringe ─ NOT a small tuft. This is the last centimetre of grass before bare ground, so
         it is WIDE and LOW and thin: blades that lie almost flat and spread sideways, which is
         what hides the splat boundary the ground shader draws. ─────────────────────────────── */
    { name: 'GrassTuft_FringeA', habitat: ['fringe'], note: 'flat spread, both ways',
      w: 32, h: 32, seed: 271, rootRows: 1, stiff: 0.7,
      groups: [{ n: 13, root: [10, 22], tip: [-3, 34], maxLen: 14, lenProf: [0.7, 1, 0.86, 0.7],
                 fall: [6, 10], bow: [-9, 9], thick: [2, 1] }] },
    { name: 'GrassTuft_FringeB', habitat: ['fringe'], note: '64×32 — a long thin edge',
      w: 64, h: 32, seed: 277, rootRows: 1, stiff: 0.7,
      groups: [{ n: 21, root: [6, 56], tip: [-4, 67], maxLen: 12, lenProf: [0.6, 0.9, 1, 0.86, 0.64],
                 fall: [5, 9], bow: [-8, 8], thick: [2, 1], jit: 0.26 }] },

    /* ── dune marram ─ 32×48 per the handoff. Ammophila is a TIGHT tussock of long thin blades
         that arch right over: few roots, big spread, heavy fall. Stays on the base ramp — it reads
         as dune grass when the straw knob is up, which is where the sand is. ─────────────────── */
    { name: 'GrassTuft_MarramA', habitat: ['dune'], note: 'tight tussock, long arching blades',
      w: 32, h: 48, seed: 281, rootRows: 2, stiff: 0.8,
      groups: [{ n: 11, root: [14, 18], tip: [-2, 33], maxLen: 44, lenProf: [0.66, 0.9, 1, 0.88, 0.62],
                 fall: [8, 15], bow: [-7, 7], thick: [2, 1], rootJit: 0.6, jit: 0.2 }] },
    { name: 'GrassTuft_MarramB', habitat: ['dune'], note: 'wispier, combed downwind',
      w: 32, h: 48, seed: 283, rootRows: 2, baseBias: 0.2, stiff: 0.8,
      groups: [{ n: 8, root: [13, 18], tip: [6, 34], maxLen: 45, lenProf: [0.72, 1, 0.9, 0.64],
                 fall: [8, 16], bow: [0, 9], thick: [2, 1], rootJit: 0.6, jit: 0.22 }] },

    /* ── dry headland ─ exposed, cropped by salt wind: short, hard-raked one way, gappy. The DRY
         is the tint knob's job; the silhouette carries the exposure. ──────────────────────────── */
    { name: 'GrassTuft_HeadlandA', habitat: ['headland'], note: 'wind-cropped, all one way',
      w: 32, h: 32, seed: 293, rootRows: 2, baseBias: 0.35, stiff: 0.9,
      groups: [{ n: 10, root: [10, 17], tip: [12, 32], maxLen: 20, lenProf: [0.66, 0.92, 1, 0.8, 0.58],
                 fall: [5, 10], bow: [3, 9], thick: [2, 1] }] },
    { name: 'GrassTuft_HeadlandB', habitat: ['headland'], note: 'gappy, half of it laid over',
      w: 32, h: 32, seed: 307, rootRows: 2, baseBias: 0.3, stiff: 0.9,
      groups: [{ n: 6, root: [11, 18], tip: [14, 31], maxLen: 19, lenProf: [0.8, 1, 0.74],
                 fall: [4, 8], bow: [2, 8], thick: [2, 1] },
               { n: 5, root: [12, 20], tip: [17, 33], maxLen: 12, lenProf: [0.9, 1, 0.7],
                 fall: [7, 11], bow: [4, 10], thick: [2, 1] }] },
  ];

  /* ── the three shipped tufts ────────────────────────────────────────────────────────────────
     #102's originals. They are NOT re-baked here and NOT replaced — the owner has painted with
     them, they are wired into the St Peters builder, and their pixels are the reference the ramp
     above was read off. They appear in the manifest so the tool sees ONE library instead of a
     special case plus a catalog, and their `climb` is the value MEASURED off the committed PNGs
     (28 / 15 / 31). `GrassLibraryContractTests` re-measures them, so if one is ever re-exported
     shorter the manifest fails rather than quietly mis-filing it in the height mix. */

  const SHIPPED = [
    { name: 'GrassTuft',       path: 'Assets/_Project/Art/Sprites/GrassTuft.png',
      w: 32, h: 32, climb: 28, habitat: ['meadow'],           note: 'the original medium tuft' },
    { name: 'GrassTuft_Short', path: 'Assets/_Project/Art/Sprites/GrassTuft_Short.png',
      w: 32, h: 32, climb: 15, habitat: ['meadow', 'sward'],  note: 'the original short tuft' },
    { name: 'GrassTuft_Tall',  path: 'Assets/_Project/Art/Sprites/GrassTuft_Tall.png',
      w: 32, h: 32, climb: 31, habitat: ['meadow'],           note: 'the original tall tuft' },
  ];

  /* ── habitat tags for the species drop ──────────────────────────────────────────────────────
     `grassSpeciesRig.js` declares each species' BIOME in prose ('freshwater marsh', 'wet edge,
     ditch', …). A scatter pass needs tags it can match on, and the drop is not ours to edit — so
     the prose→tag mapping lives here, in the host file, which is exactly where ADR 0021 puts
     "anything the engine needs that the rig doesn't provide". Keyed by the drop's own species
     keys; an unknown key falls back to `meadow` rather than throwing, so a future species in a
     re-drop appears in the library the day it lands instead of breaking the bake. */

  const SPECIES_HABITAT = {
    cattail:    ['marsh', 'wet'],
    rush:       ['marsh', 'wet', 'fringe'],
    sedge:      ['marsh', 'wet', 'meadow'],
    saltmeadow: ['saltmarsh', 'dune'],
    timothy:    ['meadow', 'hay'],
    grass:      ['meadow'],
  };

  Object.assign(HABITATS, {
    marsh:     'freshwater marsh and wet ground — cattail, rush, sedge',
    wet:       'standing or seeping water at the root',
    saltmarsh: 'salt-flooded flats behind the beach — saltmeadow hay',
    hay:       'mown / hay meadow — timothy and seed heads',
  });

  /* ── out ────────────────────────────────────────────────────────────────────────────────────
     Every variant renders through the DROP's renderer on the DROP's `grass` species — hue +0°,
     saturation ×1.00, i.e. the base ramp verbatim. There is no second colour path here, which is
     what makes "every variant stays on the ramp" true by construction rather than by review. */

  const SPECIES_KEY = 'grass';

  const render = spec => Sp.render(spec);
  const toRGBA = buf => Sp.toRGBA(buf, SPECIES_KEY);
  const validate = (buf, spec) => Sp.validate(buf, spec);

  const byName = {};
  VARIANTS.forEach(v => { byName[v.name] = v; });

  /* One variant, baked: pixels plus the measured report the importer asserts on. */
  function bake(name) {
    const spec = byName[name];
    if (!spec) throw new Error(`GrassRig knows no variant '${name}'. Known: ${VARIANTS.map(v => v.name).join(', ')}.`);
    const buf = render(spec);
    const report = validate(buf, spec);
    return { name, w: spec.w, h: spec.h, rgba: toRGBA(buf), report, spec };
  }

  /* One species-kit variant, baked through the DROP's own renderer and its OWN species ramp.
     Named `GrassTuft_<Name>` to match the filenames the drop ships under. */
  function bakeSpecies(name) {
    const spec = (Sp.VARIANTS || []).find(v => v.name === name);
    if (!spec) throw new Error(`grassSpeciesRig declares no variant '${name}'.`);
    const buf = Sp.render(spec);
    return {
      name: 'GrassTuft_' + spec.name, w: spec.w, h: spec.h,
      rgba: Sp.toRGBA(buf, spec.sp), report: Sp.validate(buf, spec), spec,
    };
  }

  /* The machine-readable catalog — ONE library over all three sources, which is the whole point:
     the tool stops carrying a hard-coded list and reads this instead.

       shipped   the three #102 tufts, declared (their pixels predate any rig)
       habitat   this file's fifteen — meadow, clump, fringe, marram, headland
       species   the art-director drop's ten — cattail, rush, sedge, saltmeadow hay, timothy

     `climb` and the height CLASS are MEASURED off the actual bake for the two baked sources, so a
     spec edit that shortens a variant re-files it in the height mix without anyone remembering to.
     `outputFolder` is where the two baked sources land; `shipped` entries carry their own path
     because they live one directory up and are not moving. */
  function manifest(outputFolder) {
    const folder = (outputFolder || 'Assets/_Project/Art/Sprites/Grass').replace(/\/+$/, '');
    const out = {
      rig: 'grassRig',
      composes: 'grassSpeciesRig',
      note: 'STATIC sprites. Rooted on the bottom row; climb = how far blades rise, which is also ' +
            'how much of the shader bend the sprite takes (HiddenHarboursGrass.shader bends by ' +
            'UV.y²). One shared material; the runtime lush→straw knob multiplies a tint over the ' +
            'ramp, so every entry here stays ON its species ramp.',
      ppu: 32,
      pivot: 'bottom-centre',
      outputFolder: folder,
      baseRamp: Sp.SPECIES[SPECIES_KEY].ramp.slice(),
      spikeRamp: (Sp.SPIKE || []).slice(),
      classBands: CLASS_BANDS,
      habitats: HABITATS,
      species: {},
      variants: {},
    };

    for (const key of Object.keys(Sp.SPECIES)) {
      const s = Sp.SPECIES[key];
      out.species[key] = { label: s.label, latin: s.latin, biome: s.biome, ramp: s.ramp.slice() };
    }

    for (const s of SHIPPED)
      out.variants[s.name] = {
        path: s.path, w: s.w, h: s.h, species: SPECIES_KEY, source: 'shipped',
        heightClass: classOf(s.climb), habitat: s.habitat.slice(),
        climb: s.climb, stiffness: 1, note: s.note,
      };

    for (const v of VARIANTS) {
      const b = bake(v.name);
      out.variants[v.name] = {
        path: `${folder}/${v.name}.png`, w: v.w, h: v.h, species: SPECIES_KEY, source: 'habitat',
        heightClass: classOf(b.report.climb), habitat: v.habitat.slice(),
        climb: b.report.climb, stiffness: v.stiff, note: v.note,
      };
    }

    for (const sv of (Sp.VARIANTS || [])) {
      const b = bakeSpecies(sv.name);
      out.variants[b.name] = {
        path: `${folder}/${b.name}.png`, w: sv.w, h: sv.h, species: sv.sp, source: 'species',
        heightClass: classOf(b.report.climb),
        habitat: (SPECIES_HABITAT[sv.sp] || ['meadow']).slice(),
        climb: b.report.climb, stiffness: sv.stiff, note: sv.note,
      };
    }
    return out;
  }

  /* Everything the baker writes, in one call: the two baked sources' pixels plus the manifest.
     The shipped three are deliberately absent — this list is what gets WRITTEN. */
  function bakeAll() {
    return VARIANTS.map(v => bake(v.name))
      .concat((Sp.VARIANTS || []).map(v => bakeSpecies(v.name)));
  }

  global.GrassRig = {
    VARIANTS, SHIPPED, HABITATS, CLASS_BANDS, SPECIES_KEY, SPECIES_HABITAT,
    render, toRGBA, validate, bake, bakeSpecies, bakeAll, manifest, classOf, byName,
  };
})(typeof window !== 'undefined' ? window : globalThis);
