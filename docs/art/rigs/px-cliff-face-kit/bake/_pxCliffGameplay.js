/* Hidden Harbours — PIXEL CLIFF gameplay-sidecar generator.

   The rig is ours, not the art director's, and it exports enough (DEF, TIERS, tierBase, ASPECT,
   SLOPES, SL, setSlope, keyFor, profile, paletteLUT) that NO shimmed copy is needed — every number
   below is read or computed from the public exports. That is the difference between this and the
   boat extractors in _sportSkiffGameplay.js / _sportFisherGameplay.js.

   Two hashes, because two renderers can drift: the cliff rig owns the form, the tiers and the
   channels; Art/pixelLanguage.js owns the KEY VECTOR and the band ramps, so it owns half the
   lighting contract. Station/rowhouse precedent — stamp both, never one.

     PXCF_GAMEPLAY.sidecar()         -> the object, unstamped
     PXCF_GAMEPLAY.lut()             -> {rows, W, H, rgba}  the palette LUT as a texture
     PXCF_GAMEPLAY.textures()        -> the file manifest (names only; the pack ships the rig)
*/
globalThis.PXCF_GAMEPLAY = (function () {
  const PF = globalThis.PxCliffFace, P = globalThis.PxLang;
  const R2D = 180 / Math.PI, D2R = Math.PI / 180;
  const r3 = v => Math.round(v * 1000) / 1000;
  const cap = s => s[0].toUpperCase() + s.slice(1);
  const SLOPETAG = { 90: '', 76: '_S76', 62: '_S62', 48: '_S48' };
  const STEPS = ['_Lo', '', '_Hi'];

  /* ---- what gameplay owns and the rig does not model -------------------------------------- */
  /* A cliff is scenery the player does not traverse — except that four batters ship, and a 48°
     bank is a hillside. So the one gameplay decision this kit forces is per BATTER, not per rock. */
  const TRAVERSE = {
    90: { traversable: false, mode: 'wall', speed: 0, note: 'A wall. Collider is the face; no route.' },
    76: { traversable: false, mode: 'wall', speed: 0, note: 'Still a wall — the 2 m setback on 8 m is not a foothold.' },
    62: { traversable: true, mode: 'scramble', speed: 0.45, note: 'A scramble: passable on foot, no vehicles, no carried load.' },
    48: { traversable: true, mode: 'walk', speed: 0.7, note: 'A steep hillside. Walkable; the rig already puts colluvium and plants on it.' },
  };
  /* Footstep and grip are for the two places a body DOES touch this material: the brow it walks to
     the edge of, and the toe debris at its foot. Declared, not read — the rig has no such field. */
  const SURFACE = {
    sandstone: { footstep: 'stone', grip: 0.62, hardness: 'soft', spall: true },
    till: { footstep: 'earth', grip: 0.48, hardness: 'loose', spall: false },
    basalt: { footstep: 'stone', grip: 0.74, hardness: 'hard', spall: true },
  };

  function batters() {
    const out = {};
    for (const [name, deg] of Object.entries(PF.SLOPES)) {
      PF.setSlope(deg);
      const K = PF.keyFor(), tf = PF.SL.tf, sin = PF.SL.sin;
      out[name] = {
        deg, tag: SLOPETAG[deg] === '' ? null : SLOPETAG[deg],
        key_in_face_frame: K.map(r3),
        setback_on_8m_m: r3(8 / Math.tan(deg * D2R)),
        beds_per_9m_of_face: Math.round(9 * sin / 0.25),
        sky_gain: r3(0.30 * tf),
        surface_length_per_metre_of_height: r3(1 / sin),
        traverse: TRAVERSE[deg],
        provenance: 'key = PxCliffFace.keyFor() at this batter · setback = 8/tan · beds = 9·sin/0.25 · sky = 0.30·SL.tf (the channels() sky term)',
      };
    }
    return out;
  }

  function materials() {
    const out = {};
    for (const r of PF.ROCKS) {
      const d = PF.DEF[r];
      out[r] = {
        base: d.base, contrast: d.c,
        lut_rows: [PF.ROCKS.indexOf(r) * 6, PF.ROCKS.indexOf(r) * 6 + 5],
        surface: SURFACE[r],
        note: d.note,
        provenance: 'base/contrast/note = PxCliffFace.DEF · lut_rows computed · surface declared (see _confirm)',
      };
    }
    return out;
  }

  function aspects() {
    const out = {};
    for (const a of PF.ASPECTS) {
      const A = PF.ASPECT[a];
      out[a] = { tier_shift: A.shift, dust: A.dust, lee: A.lee, read: A.read, xanthoria: A.xan, shadow_cool: A.cool };
    }
    return out;
  }

  /* ---- the palette LUT, as data and as a texture ------------------------------------------ */
  function lut() {
    const rows = PF.paletteLUT();
    const W = 8, H = 32, rgba = new Uint8ClampedArray(W * H * 4);
    for (let y = 0; y < H; y++) {
      const src = rows[Math.min(y, rows.length - 1)];
      for (let x = 0; x < W; x++) {
        const c = P.h2r(src.bands[Math.min(x, 4)]), o = (y * W + x) * 4;
        rgba[o] = c[0]; rgba[o + 1] = c[1]; rgba[o + 2] = c[2]; rgba[o + 3] = y < rows.length ? 255 : 0;
      }
    }
    return { rows, W, H, rgba };
  }

  /* ---- texture manifest ------------------------------------------------------------------- */
  function textures() {
    const faces = [];
    for (const rock of PF.ROCKS) for (const deg of [90, 76, 62, 48]) for (const a of PF.ASPECTS) for (let st = 0; st < 3; st++)
      faces.push(cap(rock) + '_' + a + SLOPETAG[deg] + STEPS[st]);
    return {
      faces: { count: faces.length, channels: ['', '_unlit', '_mask', '_normal', '_index'], stems: faces },
      brow: PF.ASPECTS.flatMap(a => STEPS.map(s => 'Brow/' + a + s)),
      toe: PF.ASPECTS.flatMap(a => STEPS.map(s => 'Toe/' + a + s)).concat(
        PF.ASPECTS.flatMap(a => ['cave', 'slump'].map(f => 'Toe/' + a + '_' + f))),
      profile: PF.ROCKS.flatMap(r => [90, 76, 62, 48].map(d => 'Profile/' + cap(r) + SLOPETAG[d])),
    };
  }

  /* ---- the real-time lighting contract ---------------------------------------------------- */
  function lighting() {
    return {
      _: 'The whole point of this block. Three paths; pick one per wall and do not mix them.',
      baked_key: {
        vector: P.LIGHT.key.map(r3),
        rim: P.LIGHT.rim.map(r3),
        frame: 'the pixel language\'s fixed upper-left key, in FACE space at 90°. Per-batter rotations are in BATTERS[*].key_in_face_frame.',
        note: 'Aspect is NOT a second sun. It is one tier of incidence plus permanent weathering — so a live sun replaces the key, never the aspect.',
      },
      tangent_basis: { R: 's, along the cliff', G: 't, UP the face', B: 'out of the face', A: 'cavity AO', srgb: false,
        build: 'per wall segment, on the CPU: Ts = normalize(along-cliff, horizontal) · Nw = normalize(N_plan·sin(batter) + up·cos(batter)) · Tt = cross(Nw, Ts) · L = vec3(dot(S,Ts), dot(S,Tt), dot(S,Nw)) for world sun S',
        note: 'Nw is TIPPED BACK by the batter. Skip that and a 48° bank is lit as though it were a wall, which is the one thing the batter exists to fix.' },
      channels: {
        '': { role: 'pre-lit albedo', relightable: false, srgb: true, note: 'the fixed-sun path. Ships the key, the aspect incidence and the cast shadow already in the pixels.' },
        _unlit: { role: 'relightable base colour', relightable: true, srgb: true, note: 'authored bands + non-directional cavity only. No key, no incidence, no cast shadow.' },
        _normal: { role: 'tangent normal + cavity AO', relightable: true, srgb: false },
        _mask: { role: 'R baked N·L × cast shadow · G sky occlusion · B height · A coverage', relightable: true, srgb: false,
          note: 'R is the one thing a normal map cannot reproduce — a CAST shadow off the ribs and bench lips. Divide out the baked N·L to recover the shadow term alone (see relight_continuous).' },
        _index: { role: 'R LUT row · G band 0..4 · B tier-shiftable flag · A coverage', relightable: true, srgb: false,
          note: 'OPT-IN channel: PxCliffFace.channels(b, {index:true}). The palette-shift path needs it — a colour-only unlit texture cannot be band-shifted without an inverse lookup.' },
      },
      law: 'ONE step per light decision, never a gradient. The bake moves a texel one BAND for detail light and one TIER for form, shadow and incidence; it never interpolates. A shader that multiplies _unlit by a continuous N·L is lighting a photograph, not pixel art — it will read as mud at dawn and dusk.',
      aspect_and_live_sun: {
        rule: 'Under a live sun, do NOT also apply ASPECTS[*].tier_shift. It double-counts.',
        why: 'tier_shift is one tier of INCIDENCE — the bake\'s stand-in for which way the face points, because the fixed key cannot know. An engine that builds its tangent-space sun from the segment\'s real outward normal already has that, and adding the shift darkens every E face twice.',
        still_baked_in: ['dust crowns on the windward lips', 'seep, lichen and moss in the lee joints', 'Xanthoria on W/SW basalt only', 'the read gain that thickens joints on W and E'],
        consequence: 'Weathering is permanent and cannot be relit out of the pixels, which is why the five aspects still earn their keep once light is live — you pick the aspect for the weathering, and the sun does the rest.',
        apply_shift_when: 'you light every wall with ONE tangent-space sun vector rather than per-segment. Then the shift is the only thing distinguishing a W face from an E one.',
      },
      thresholds: {
        band_up: 0.74, band_down: 0.40,
        tier_up: 0.40, tier_down: -0.40,
        shadow_one_tier: 0.45, shadow_two_tiers: 0.85,
        cavity_band_up: 0.80, cavity_band_down: 0.22,
        provenance: 'read verbatim from PxCliffFace.channels() (lo/hi) and face() (the tier and shadow gates)',
      },
      quantisation: {
        texel_px: 2, form_cell_texels: [8, 6], form_cell_m: [0.5, 0.375],
        note: 'The form is sampled ONCE PER CELL, not per texel, and the cell rows are staggered and their edges torn. A relight that quantises per texel will fizz along every tier boundary; quantise the lighting term on the same cell grid, or accept the _index channel\'s rows as already-quantised and only shift them.',
      },
      tiers: { list: PF.TIERS, zero_index: PF.T0, count: 6,
        note: '0 is the wall, +1 a rib crown, −1 a flank or gully, −2/−3 cast shadow. −3 exists so a shaded E wall still has somewhere to put its own shadows.' },
      paths: {
        fixed_sun: { cost: '1 sample', textures: [''], exact: true,
          use: 'Interiors of the day, static time of day, distant LODs. Correct by construction — it is the art.' },
          relight_continuous: { cost: '3 samples', textures: ['_unlit', '_normal', '_mask'], exact: false,
          use: 'A traversing sun on a 24 h cycle where the pixel banding can soften a little.',
          shader: 'export/px-cliff-face-kit/shaders/relight_continuous.glsl' },
        relight_palette: { cost: '2 samples + 1 LUT tap', textures: ['_index', '_normal'], exact: true,
          use: 'THE ONE THAT KEEPS THE PIXEL LOOK. Step the index, do not multiply the pixel.',
          shader: 'export/px-cliff-face-kit/shaders/relight_palette.glsl',
          requires: '_index maps baked with channels(b, {index:true}) — see _confirm.lightmap_bake' },
      },
      palette_lut: {
        file: 'CliffPx_palette.png', size: [8, 32], used: [5, 25], srgb: true, filter: 'Point', wrap: 'Clamp', mips: false,
        layout: 'x = band 0..4 (columns 5..7 repeat band 4 to pad to a power of two) · y = LUT row',
        rows: '0..5 sandstone tiers −3..+2 · 6..11 till · 12..17 basalt · 18 silt · 19 moss · 20 dust · 21 peb · 22 turf · 23 grit · 24 xanthoria · 25..31 padding (alpha 0)',
        note: 'Accessory rows 18..24 take a BAND step but never a TIER step — lichen does not go the colour of shadowed rock. The _index channel\'s B flag says which.',
      },
      import: {
        faces: { wrap: 'Repeat', filter: 'Point', compression: 'None', mips: 'off at 1:1' },
        decals: { wrap: 'Repeat S · Clamp T', filter: 'Point', compression: 'None', alpha_is_transparency: true },
        profile: { wrap: 'Repeat S · Clamp T', filter: 'Bilinear', srgb: false, note: 'it is geometry, not pixels' },
        raw_channels: ['_normal', '_mask', '_index', 'profile'],
        warning: '_index carries RAW INDICES. sRGB conversion, bilinear filtering, mips or block compression on it will all produce colours that are not in the palette.',
      },
    };
  }

  function sidecar() {
    return {
      _: 'Hidden Harbours — pixel cliff face kit gameplay sidecar. Generated by Art/_pxCliffGameplay.js from Art/pxCliffFaceRig.js + Art/pixelLanguage.js; do not hand-edit.',
      schema: 'hidden-harbours/terrain-cliff-gameplay@1',
      rig: 'Art/pxCliffFaceRig.js',
      language: 'Art/pixelLanguage.js',
      generated: new Date().toISOString().slice(0, 10),
      frame: {
        units: 'metres', scale_px_per_m: PF.PPU, texel_px: 2,
        tile_px: [384, 288], tile_m: [12, 9],
        uv: 's along the cliff (wraps) · t DOWN the face (wraps)',
        note: 't is 32 px/m ALONG THE SURFACE, not along the height. Size the quad by surface length: an 8 m bank at 48° needs 8/sin(48°) = 10.8 m of t.',
        heading: 'A cliff face does not rotate relative to the camera, so there is no facing split — the aspect IS the orientation.',
      },
      MATERIALS: materials(),
      BATTERS: batters(),
      ASPECTS: aspects(),
      LIGHTING: lighting(),
      PALETTES: lut().rows,
      PROFILE: {
        relief_m: PF.RELIEF_M, ppu: PF.PPU, size: [384, 288],
        encoding: 'grey, 128 = zero, full range ±1.15 m of PLAN displacement',
        depends_on: ['rock seed', 'batter'],
        independent_of: ['aspect', 'wear step'],
        note: 'one profile serves all fifteen bakes of a (rock, batter) group',
        displace: 'subdivide along s at 0.25 m; p += Nw · (sample(u,v) − 0.5) · 2 · 1.15',
        why: 'Do it and the silhouette, the baked cast shadow and the normal map are all the same shape, and the brow line stops being a smooth curve. It is also the geometry a live sun needs to cast its own shadow instead of borrowing mask.R.',
      },
      COLLISION: {
        source: 'PROFILE — the collider is the displaced face, not the quad',
        default: 'blocking volume per wall segment, 1.15 m of plan depth included',
        traverse_by_batter: Object.fromEntries(Object.entries(TRAVERSE).map(([d, v]) => [d, v.mode])),
        brow_edge: { fall: true, note: 'the brow decal\'s line sits at t = 0.42 of its strip; the walkable ground stops there, not at the quad edge' },
        toe: { note: 'toe debris is a decal, not geometry. ToeCave is a decal too — there is no hole to walk into.' },
      },
      DECALS: {
        dirs: ['Art/Textures/CliffPx/Brow/', 'Art/Textures/CliffPx/Toe/'],
        size: [384, 128], metres: [12, 4], wrap: 'Repeat S · Clamp T', brow_line: 0.42,
        rock_agnostic: true,
        relightable: false,
        why_not: 'Their darks are cast shadow and geometric occlusion — the sod lip\'s undercut, the notch recess — not N·L, so a normal map cannot relight them. Flat washes at two or three alpha levels, never a gradient. They read from mid-morning to late afternoon and go slightly wrong at a low sun.',
        if_you_need_them_lit: 'palette-swap the strip per time of day, or re-bake with a different key. Do not multiply them by a live N·L.',
      },
      TEXTURES: textures(),
      _excluded: {
        WALKABLE_SURFACE: 'No walkable polygon on a 90° or 76° face — the absence is the data. 62° and 48° publish a traverse mode instead of a polygon because the surface is a displaced quad, not a plan area; the collider is generated from PROFILE.',
        CLEATS: 'not a hull',
        THRESHOLD: 'no doors, caves or arches. ToeCave is a decal.',
        INTERACT: 'nothing on a cliff face is interactable in this pass. Climbing anchors, nesting sites and mineral picks were all discussed and none is modelled.',
        N_ASPECT: 'N faces the camera\'s back and is not authored. The composer snaps each coast segment\'s outward normal to the nearest of five.',
        LEDGE_TILES: 'the 32 px iso ledge tiles bake from Art/_pxCliffBake.js into Art/Tilesets/CliffPx/ and are fixed-sun pixel art by nature — 8 steps on a 32 px face. They take no batter and are not in this sidecar; relight them with a palette swap per time of day, never a normal map.',
        CHUNK_OFFSET: 'the face tile repeats every 12 m along the shore and carries no per-chunk offset by design. The brow and toe decals, the aspect changes and the batter changes are what break it up; on a 200 m straight run you will see it.',
      },
      _confirm: {
        lightmap_bake: 'The _index maps are NOT in Art/Textures/CliffPx/ yet. The channel is implemented and opt-in; baking the full set is 180 more PNGs. The pack ships the rig and a five-file tex-sample (Sandstone wall, all five aspects) so the shader can be wired up before committing to the run — same policy as the v10 photoreal kit, which ships the rig rather than 800 PNGs.',
        traverse_rules: 'TRAVERSE (which batters a body can climb, and how fast) is DECLARED here, not read from the rig. 62° as a scramble and 48° as a walk are our call; the owner has not ruled.',
        surface_grip: 'MATERIALS[*].surface — footstep, grip, hardness — is declared on the roadPathRig vocabulary. It only applies at the brow and in the toe debris; nothing walks on the face itself.',
        palette_path_cell_grid: 'relight_palette steps the _index rows, which are already quantised on the 8×6 form cell. relight_continuous quantises per texel and WILL fizz along tier boundaries unless the engine quantises its own lighting term on the same grid. Untested in engine.',
        cast_shadow_reuse: 'relight_continuous recovers the cast shadow by dividing mask.R by the baked N·L. Where the baked N·L is near zero that term is noise; the shader clamps at 0.02 and the result is a guess in those texels. A live sun over displaced PROFILE geometry is the honest answer.',
      },
    };
  }

  return { sidecar, lut, textures, batters, materials, aspects, lighting, TRAVERSE, SURFACE, SLOPETAG, STEPS, cap };
})();
