/* Hidden Harbours — PIXEL CLIFF FACE bake harness.  run_script recipe:
     (0,eval)(await readFile('Art/pixelLanguage.js'));
     (0,eval)(await readFile('Art/pxCliffFaceRig.js'));
     (0,eval)(await readFile('Art/_pxCliffBake.js'));
     await PXCF_BAKE({ createCanvas, saveFile, log, rocks:['sandstone'], slopes:[90] });

   Writes into Art/Textures/CliffPx/ — the pixel-language twin of Art/Textures/Cliff/, same names,
   same UV contract (s along the cliff, t DOWN it, both wrapping), so the composer swaps directory
   and nothing else:
     <Rock>_<Aspect>{|_S76|_S62|_S48}{_Lo|""|_Hi}{|_unlit|_mask|_normal}.png   384×288 = 12×9 m
     Brow/<Aspect>{_Lo|""|_Hi}.png · Toe/<Aspect>{_Lo|""|_Hi}{|_cave|_slump}.png  RGBA 384×128 */
globalThis.PXCF_BAKE = async function (o) {
  const P = globalThis.PxLang, PF = globalThis.PxCliffFace;
  const { createCanvas, saveFile } = o, log = o.log || (() => {});
  const D = 'Art/Textures/CliffPx/', CH = ['', '_unlit', '_mask', '_normal'];
  /* index: true adds the fifth channel — the palette-shift relight path's LUT row/band map.
     180 more PNGs across the full cross product, so it is opt-in at both ends (here and in the
     rig's channels()). See Art/gameplay/pxCliffFaceRig.gameplay.json → LIGHTING.paths. */
  if (o.index) CH.push('_index');
  const STEPS = ['_Lo', '', '_Hi'];
  const SLOPETAG = { 90: '', 76: '_S76', 62: '_S62', 48: '_S48' };
  const rocks = o.rocks || PF.ROCKS, aspects = o.aspects || PF.ASPECTS;
  const slopes = o.slopes || [90], steps = o.steps || [0, 1, 2];
  let files = 0;
  const put = async (path, rgba, W, H) => {
    const cv = createCanvas(W, H);
    cv.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(rgba), W, H), 0, 0);
    await saveFile(path, cv); files++;
  };
  for (const rock of rocks) for (const slope of slopes) for (const A of aspects) for (const st of steps) {
    const b = PF.face(rock, A, st, { slope });
    const ch = PF.channels(b, { index: !!o.index });
    const stem = D + rock[0].toUpperCase() + rock.slice(1) + '_' + A + SLOPETAG[slope] + STEPS[st];
    for (const c of CH) await put(stem + c + '.png', ch[c], b.W, b.H);
    log('face ' + stem.slice(D.length));
  }
  if (o.decals) {
    for (const A of PF.ASPECTS) for (const st of [0, 1, 2]) {
      const br = PF.brow(A, st, {});
      await put(D + 'Brow/' + A + STEPS[st] + '.png', br.data, br.W, br.H);
      const to = PF.toe(A, st, {});
      await put(D + 'Toe/' + A + STEPS[st] + '.png', to.data, to.W, to.H);
    }
    for (const A of PF.ASPECTS) for (const f of ['cave', 'slump']) {
      const to = PF.toe(A, 1, { feature: f });
      await put(D + 'Toe/' + A + '_' + f + '.png', to.data, to.W, to.H);
    }
    log('decals');
  }
  if (o.contract) {
    const J = {
      _note: 'Baked by Art/_pxCliffBake.js from Art/pxCliffFaceRig.js through Art/pixelLanguage.js. Regenerate rather than hand-edit.',
      dir: D, ppu: PF.PPU, texel: 2, size: [384, 288], metres: [12, 9], uv: 's along the cliff (wraps), t DOWN it (wraps)',
      rocks: PF.ROCKS, aspects: PF.ASPECTS, steps: ['_Lo', '', '_Hi'], slopes: PF.SLOPES, slopeTags: SLOPETAG,
      light: { key: P.LIGHT.key, rim: P.LIGHT.rim, note: 'the pixel language\'s fixed upper-left key, rotated into the tipped frame by the batter. Aspect is NOT a second sun — it is one band of incidence plus the weathering.' },
      tiers: { list: PF.TIERS, note: 'the form is quantised into six palettes cut from the rock\'s own hue: 0 is the wall, +1 a rib crown, −1 a flank or gully, −2/−3 cast shadow. The aspect\'s incidence moves the whole sheet one tier (W +1 · SW +1 · S 0 · SE −1 · E −1).' },
      form: { relief_m: PF.RELIEF_M, cell: [8, 6], note: 'ribs on an alternating 6-cell lattice (4 m), clefts gated into the re-entrants, three benches periodic in t. Sampled once per 8×6-texel cell so a buttress is a mosaic of flat blocks, not a gradient.' },
      channels: {
        albedo: 'pre-lit ART at the fixed key: tier (form + aspect + cast shadow) plus one band of detail key. The cheap fixed-sun path.',
        _unlit: 'the authored bands with the non-directional cavity only — no key, no aspect incidence, no cast shadow. Survives any grade and any sun.',
        _mask: 'R key N·L with the cast shadow multiplied in · G sky occlusion · B height (detail + form) · A coverage',
        _normal: 'tangent space: R = s · G = t-up · B = out of the face · A = cavity AO. Import with sRGB off.',
        _index: 'OPT-IN (channels(b, {index:true}) · PXCF_BAKE({index:true})). R = palette LUT row · G = band 0..4 · B = tier-shiftable · A = coverage. RAW INDICES — sRGB off, Point, no mips, no compression. What the palette-shift relight path needs: a colour-only unlit texture cannot be band-shifted without an inverse lookup.',
      },
      paletteLUT: { file: D + 'CliffPx_palette.png', size: [8, 32], used: [5, 25],
        rows: '0..5 sandstone tiers −3..+2 · 6..11 till · 12..17 basalt · 18 silt · 19 moss · 20 dust · 21 peb · 22 turf · 23 grit · 24 xanthoria · 25..31 padding (alpha 0)',
        note: 'Every colour the face bake can emit. Accessory rows 18..24 take a BAND step but never a TIER step. Night is a second LUT, not a grade — a grade over a six-tier ramp crushes the two shadow tiers together and the form stops reading.' },
      gameplay: { sidecar: 'Art/gameplay/pxCliffFaceRig.gameplay.json', pack: 'export/px-cliff-face-kit/',
        note: 'schema hidden-harbours/terrain-cliff-gameplay@1 — the relight contract, the three lighting paths, the palette table as data, PROFILE-derived collision and the per-batter traverse rules. Generated by Art/_pxCliffGameplay.js; stamped with BOTH rig hashes.' },
      decals: { dir: [D + 'Brow/', D + 'Toe/'], size: [384, 128], metres: [12, 4], wrap: 'Repeat S / Clamp T', browLine: 0.42,
        note: 'Rock-agnostic. The undercut and the salt bleach are flat washes at two or three alpha levels — never a gradient — so the wall\'s own beds, joints and sockets read through them. Fixed-sun by nature: their darks are cast shadow and occlusion, not N·L.' },
      api: 'PxCliffFace.face(rock, aspect, step, {slope}) · channels(b, {index}) · profile(rock, aspect, {slope}) · brow(aspect, step) · toe(aspect, step, {feature}) · paletteLUT()',
      rocksNote: {},
    };
    for (const r of PF.ROCKS) J.rocksNote[r] = PF.DEF[r].note;
    await saveFile(D + 'CliffPx.json', JSON.stringify(J, null, 1));
    log('contract');
  }
  log('— ' + files + ' PNGs');
  return { files };
};
