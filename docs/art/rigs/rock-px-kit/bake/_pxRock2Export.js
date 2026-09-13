/* Hidden Harbours — PIXEL ISO ROCK PASS THREE, shipping export.  Not shipped itself.

   Writes the in-game payload for PxRockIso2: co-registered shading channels, per-form gameplay
   sidecars, and one kit manifest — all stamped with the SHA-256 of the rig AS READ, per
   _sidecarExport.js. The bake harness (_pxRock2Bake.js) is for looking at rocks; this is for
   handing them to the engine.

   SHEET LAYOUT — 8 cols x 3 rows, one cell = FORMS[k].cell
     cols  variant 0..3, then the same four mirrored (4..7)
     rows  tide: 0 dry · 1 wet · 2 awash
   Tide is the row axis so the shader can crossfade two rows as the water comes up instead of
   swapping sheets. Dressing is a separate file (bare is unsuffixed).

   CHANNELS — four PNGs per sheet, pixel-identical registration
     <stem>.png          albedo, key light already banded in. What draws if nothing is shading it.
     <stem>_unlit.png    the same texels with the key band removed — the base colour to relight.
     <stem>_mask.png     R key light · G back rim · B depth along the view axis · A coverage
     <stem>_normal.png   view-space normal, R = x, G = y-up, B = toward camera

   The mask's B channel is the depth key the sprite sorter wants, and its A is the only correct
   coverage test (the albedo's alpha is binary but the sheet's empty cells are not distinguishable
   from a hole in a rock without it).

   SEAT DECALS — <Form><V>_skirt_<seat>[_<seat2>].png plus _blend.png. Drawn OVER the rock on the
   same cell and pivot. The blend map is PxKit's own: R weight · G mark lead · B mark id · A 255.

   SIDECARS — export/rock-px-kit/sidecar/<form>.json, one per form, four variants each: cell,
   pivot, footprint in metres, perch, snags, hazard radius, pool rect, weed line, and the params
   the geometry came from. derivedFromRigSha256 on every file.

   run_script recipe (slice it — 5 sheets per call is about one budget):
     (0,eval)(await readFile('Art/pixelLanguage.js'));
     (0,eval)(await readFile('Art/pxKit.js'));
     (0,eval)(await readFile('Art/pxKit2.js'));
     (0,eval)(await readFile('Art/pxRockIso2.js'));
     (0,eval)(await readFile('Art/_pxRock2Export.js'));
     await PXROCK2_EXPORT({ createCanvas, saveFile, readFile, log, phase:'sheets', from:0, to:5 });

   phases
     jobs      log the job list and its length, write nothing
     sheets    {from,to} slice of the sheet list -> tex/ + sidecar/
     seats     the seat-decal set -> seat/
     manifest  RockPx.json + README.md  (run LAST — it reads what landed)              */
globalThis.PXROCK2_EXPORT = async function (o) {
  const P = globalThis.PxLang, K = globalThis.PxKit, R = globalThis.PxRockIso2;
  if (!R) throw new Error('load Art/pxRockIso2.js first');
  const { createCanvas, saveFile } = o, log = o.log || (() => {});
  const DIR = (o.dir || 'export/rock-px-kit/').replace(/\/?$/, '/');
  const TIDES = ['dry', 'wet', 'awash'], COLS = 8, ROWS = 3;
  const cap = s => s[0].toUpperCase() + s.slice(1);
  let files = 0;
  const save = async (p, c) => { await saveFile(p, c); files++; };

  /* ---- the stamp: hashed from the bytes on disk at the moment of writing, never cached ---- */
  async function rigSha() {
    if (!(globalThis.crypto && globalThis.crypto.subtle)) throw new Error('export: no SubtleCrypto — cannot stamp the sidecars');
    const src = await o.readFile('Art/pxRockIso2.js');
    const d = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(src));
    return Array.from(new Uint8Array(d)).map(b => b.toString(16).padStart(2, '0')).join('');
  }
  const SHA = await rigSha();
  const stamp = obj => Object.assign({ exportSymbol: 'PxRockIso2', derivedFromRigSha256: SHA }, obj);
  const saveJSON = (p, obj) => save(p, JSON.stringify(stamp(obj), null, 1));

  /* which stones each role is allowed to be cut from. A skerry in glacial till is not a thing. */
  const STONE_SET = {
    mass: ['granite', 'sandstone', 'basalt', 'quartzite', 'till'],
    flat: ['sandstone', 'granite', 'basalt', 'quartzite', 'till'],
    seam: ['sandstone', 'granite', 'basalt', 'quartzite', 'till'],
    heap: ['quartzite', 'sandstone', 'granite', 'till'],
    water: ['basalt', 'granite', 'sandstone'],
  };
  const DRESS = ['bare', 'barnacled', 'weeded'];

  /* form -> stone -> dress, native stone and bare dressing FIRST, so a short slice from 0 is
     already a usable kit and the long tail is the variety. */
  function jobs() {
    const out = [];
    for (const k of R.FORM_KEYS) {
      const F = R.FORMS[k], allow = STONE_SET[F.role] || STONE_SET.mass;
      const stones = [F.stone].concat(allow.filter(s => s !== F.stone));
      for (const stone of stones) for (let d = 0; d < DRESS.length; d++)
        out.push({ form: k, stone, dress: d, dressName: DRESS[d] });
    }
    return out;
  }
  const JOBS = jobs();

  const bmp = (rgba, w, h) => P.toCanvas(rgba, w, h, createCanvas);
  const blit = (cx, rgba, w, h, x, y) => { cx.imageSmoothingEnabled = false; cx.drawImage(bmp(rgba, w, h), x, y); };

  if (o.phase === 'jobs') {
    log(JOBS.length + ' sheets · ' + JOBS.length * 4 + ' PNGs');
    JOBS.forEach((j, i) => { if (i < 24 || j.dress === 0 && j.stone === R.FORMS[j.form].stone) log(i + '  ' + cap(j.form) + '_' + j.stone + (j.dress ? '_' + j.dressName : '')); });
    return { jobs: JOBS.length };
  }

  if (o.phase === 'sheets') {
    /* saveFile is buffered to the end of the script and a timeout throws the whole buffer away, so
       the loop watches its own clock and stops early rather than losing the slice. Returns `next`. */
    const t0 = Date.now(), budget = o.budgetMs == null ? 18000 : o.budgetMs;
    const from = o.from || 0, to = o.to == null ? JOBS.length : o.to;
    const slice = JOBS.slice(from, to);
    const side = {}; let done = 0, next = to;
    for (const j of slice) {
      if (Date.now() - t0 > budget) { next = from + done; break; }
      done++;
      const F = R.FORMS[j.form], cw = F.cell.w, ch = F.cell.h;
      const W = cw * COLS, H = ch * ROWS;
      const ch4 = ['albedo', 'unlit', 'mask', 'normal'];
      const cv = {}, cx = {};
      for (const c of ch4) { cv[c] = createCanvas(W, H); cx[c] = cv[c].getContext('2d'); }
      const vs = [];
      for (let r = 0; r < ROWS; r++) for (let c = 0; c < COLS; c++) {
        const b = R.build(j.form, { variant: c % 4, mirror: c >= 4, stone: j.stone, tide: TIDES[r], dress: j.dress });
        const x = c * cw, y = r * ch;
        blit(cx.albedo, R.albedo(b), b.w, b.h, x, y);
        blit(cx.unlit, R.unlit(b), b.w, b.h, x, y);
        blit(cx.mask, R.mask(b), b.w, b.h, x, y);
        blit(cx.normal, R.normal(b), b.w, b.h, x, y);
        if (r === 0 && c < 4) vs.push({ id: b.id, variant: c % 4, sizeM: b.params.sizeM, topM: b.topM,
          cell: [cw, ch], anchors: b.anchors, params: b.params });
      }
      const stem = DIR + 'tex/' + cap(j.form) + '_' + j.stone + (j.dress ? '_' + j.dressName : '');
      await save(stem + '.png', cv.albedo);
      await save(stem + '_unlit.png', cv.unlit);
      await save(stem + '_mask.png', cv.mask);
      await save(stem + '_normal.png', cv.normal);
      /* the sidecar is geometry, so it is per form+stone — dressings do not move anchors.
         NOTE: a slice only sees the stones in its own range, so writing sidecar/<form>.json from
         here can only ever be PARTIAL. Phase 'sidecars' writes the complete file in one pass and
         is the one that ships; this is kept for a single-call full run. */
      if (j.dress === 0) {
        side[j.form] = side[j.form] || { form: j.form, name: F.name, role: F.role, note: F.note,
          cell: [cw, ch], sheet: { cols: COLS, rows: ROWS, colAxis: '4 variants then the same 4 mirrored', rowAxis: TIDES },
          pivot: 'bottom-centre ground contact', stones: {} };
        side[j.form].stones[j.stone] = vs;
      }
      log(stem.replace(DIR, '') + '  ' + W + 'x' + H);
    }
    for (const k in side) await saveJSON(DIR + 'sidecar/' + k + '.json', side[k]);
    log('— ' + files + ' files · sheets ' + from + '..' + (next - 1) + ' of ' + JOBS.length);
    return { files, from, next, total: JOBS.length, doneAll: next >= JOBS.length };
  }

  if (o.phase === 'seats') {
    /* the seam pieces on their own boundaries, plus one single-seat decal per ground the kit
       actually meets. seat2 = null is a plain apron; seat2 set makes the decal carry the frontier. */
    const JOBS2 = [
      ['knuckle', 0, 'grass', 'sand'], ['knuckle', 1, 'grass', 'marram'], ['knuckle', 2, 'marram', 'sand'],
      ['knuckle', 3, 'sand', 'foreshore'], ['spine', 0, 'grass', 'marram'], ['spine', 1, 'grass', 'sand'],
      ['spine', 2, 'marram', 'sand'], ['bench', 0, 'path', 'grass'], ['bench', 1, 'grass', 'dirt'],
      ['bench', 3, 'ledge', 'shingle'],
      ['erratic', 1, 'grass', null], ['erratic', 2, 'marram', null], ['block', 1, 'sand', null],
      ['block', 3, 'talus', null], ['perched', 1, 'grass', null], ['slab', 1, 'ledge', null],
      ['shelf', 1, 'ledge', null], ['fin', 1, 'grass', null], ['wedge', 2, 'shingle', null],
      ['cloven', 1, 'grass', null], ['prisms', 1, 'ledge', null], ['cobbles', 1, 'shingle', null],
      ['cobbles', 2, 'sand', null], ['scree', 1, 'talus', null], ['apron', 1, 'talus', null],
      ['apron', 2, 'shingle', null], ['skerry', 1, 'water', null], ['skerry', 2, 'water', null],
    ];
    const index = [];
    for (const [k, v, s1, s2] of JOBS2) {
      const F = R.FORMS[k];
      const b = R.build(k, { variant: v, tide: F.role === 'water' ? 'awash' : 'dry' });
      const sk = R.skirt(b, { seat: s1, seat2: s2 });
      if (!sk) { log('skip ' + k + ' ' + s1); continue; }
      const stem = DIR + 'seat/' + cap(k) + String.fromCharCode(65 + v) + '_skirt_' + s1 + (s2 ? '_' + s2 : '');
      await save(stem + '.png', bmp(sk.rgba, b.w, b.h));
      await save(stem + '_blend.png', bmp(sk.blend, b.w, b.h));
      index.push({ file: stem.replace(DIR, ''), form: k, variant: v, seat: s1, seat2: s2, reach: sk.reach,
        cell: [b.w, b.h], pivot: b.anchors.pivot });
    }
    await saveJSON(DIR + 'seat/seats.json', { note: 'Seat decals. Draw OVER the rock, same cell, same pivot. A seam decal (seat2 set) must be placed with its frontier ON the level\'s own material boundary — the decal does not invent the boundary, it dresses it.', decals: index });
    log('— ' + files + ' files');
    return { files };
  }

  if (o.phase === 'sidecars') {
    /* Every form × every allowed stone in ONE pass, so the file cannot come out short.
       Geometry only — no canvases, no PNGs — which is why all sixteen fit in one call.
       Anchors are read from the dry row exactly as the sheet phase reads them, so a sidecar
       written here and one written alongside the sheets are the same bytes. */
    for (const k of R.FORM_KEYS) {
      const F = R.FORMS[k], cw = F.cell.w, ch = F.cell.h;
      const allow = STONE_SET[F.role] || STONE_SET.mass;
      const stones = [F.stone].concat(allow.filter(s => s !== F.stone));
      const rec = { form: k, name: F.name, role: F.role, note: F.note, cell: [cw, ch],
        sheet: { cols: COLS, rows: ROWS, colAxis: '4 variants then the same 4 mirrored', rowAxis: TIDES },
        pivot: 'bottom-centre ground contact', stones: {} };
      for (const stone of stones) {
        const vs = [];
        for (let c = 0; c < 4; c++) {
          const b = R.build(k, { variant: c, mirror: false, stone, tide: TIDES[0], dress: 0 });
          vs.push({ id: b.id, variant: c, sizeM: b.params.sizeM, topM: b.topM, cell: [cw, ch],
            anchors: b.anchors, params: b.params });
        }
        rec.stones[stone] = vs;
      }
      await saveJSON(DIR + 'sidecar/' + k + '.json', rec);
      log(k + '  ' + Object.keys(rec.stones).length + ' stones · ' + Object.keys(rec.stones).join(' '));
    }
    log('— ' + files + ' sidecars · rig ' + SHA.slice(0, 8));
    return { files, sha: SHA };
  }

  if (o.phase === 'manifest') {
    const man = {
      kit: 'Rock Iso, pass three', rig: 'Art/pxRockIso2.js', ppu: R.PPU, texel: 1,
      camera: { name: 'ADR-0006/0022', view: '3/4 from the south, orthographic', elevDeg: R.ELEV,
        depthSquash: +R.Q.toFixed(4), heightScale: +R.HZ.toFixed(4),
        note: '+y north. Depth key runs along the view axis and is packed in the mask\'s B channel.' },
      pivot: 'bottom-centre GROUND CONTACT — the tile the rock stands on, not the bbox centre',
      sheet: { cols: COLS, rows: ROWS, colAxis: '4 variants then the same 4 mirrored',
        rowAxis: TIDES, naming: '<Form>_<stone>[_<dress>].png, bare unsuffixed' },
      channels: {
        albedo: 'RGBA, binary alpha, no AA. The key band is already in it.',
        unlit: 'the same texels with the key band removed — relight this one',
        mask: 'R = key light · G = back rim · B = view-axis depth · A = coverage',
        normal: 'view-space normal, R = x · G = y-up · B = toward camera',
        blend: 'seat decals only — R = material weight · G = mark lead · B = mark id · A = 255',
      },
      shading: [
        'Sort and occlude on mask.B, not on the sprite\'s y — a skerry\'s far head is behind its near one inside one sheet cell.',
        'Relight = unlit x a THREE-step band (multipliers 0.76 / 1.00 / 1.24), not a smooth ramp. Smooth ramps stop it being pixel art.',
        'The dynamic key NUDGES the baked band, it does not replace it: take the band from mask.R (>200 lit, <90 shade), take a wanted band from saturate(N.L) (>0.84 lit, <0.30 shade), and move the baked one at most ONE step toward it. Recomputing the band from N.L alone bands the mark relief too and the rock dissolves into speckle.',
        'mask.G is a back rim, already keyline-correct at 2 px. Add dusk/lantern rim to it, do not replace it.',
        'Wet and awash rows are ALBEDO changes, not shader ones. Crossfade rows on the tide clock; do not tint the dry row.',
        'Water, foam, spray and tide-pool fill are NOT baked. The pool rect in the sidecar is where to put them.',
      ],
      stones: R.STONE_KEYS.map(k => ({ key: k, name: R.STONES[k].name, note: R.STONES[k].note })),
      tides: TIDES, dress: DRESS, seats: R.SEATS, bury: R.BURY,
      forms: R.FORM_KEYS.map(k => ({ key: k, name: R.FORMS[k].name, role: R.FORMS[k].role,
        cell: [R.FORMS[k].cell.w, R.FORMS[k].cell.h], nativeStone: R.FORMS[k].stone,
        stones: (STONE_SET[R.FORMS[k].role] || STONE_SET.mass), sidecar: 'sidecar/' + k + '.json' })),
      sheetCount: JOBS.length, pngCount: JOBS.length * 4,
    };
    await saveJSON(DIR + 'RockPx.json', man);
    const md = ['# Rock Iso, pass three — export', '',
      'Baked from `Art/pxRockIso2.js` by `Art/_pxRock2Export.js`. Regenerate, never hand-edit.',
      'Rig SHA-256 `' + SHA + '` is stamped into every JSON here; if it does not match the rig you',
      'are building against, the sheets and the contract have come apart — rebake.', '',
      '## Layout', '',
      '`tex/<Form>_<stone>[_<dress>].png` — ' + COLS + ' cols (4 variants, then the same 4 mirrored)',
      '× ' + ROWS + ' rows (tide: ' + TIDES.join(' / ') + '). Cell size per form is in `RockPx.json`.', '',
      '## Channels', '', 'Four co-registered PNGs per sheet:', '',
      '| file | contents |', '| --- | --- |',
      '| `<stem>.png` | albedo, key light baked in |',
      '| `<stem>_unlit.png` | base colour to relight |',
      '| `<stem>_mask.png` | R key · G rim · B depth · A coverage |',
      '| `<stem>_normal.png` | view-space normal (R x, G y-up, B toward camera) |', '',
      '## Shading', ''].concat(man.shading.map(s => '- ' + s)).concat(['',
      '## Seats', '',
      'A rock does not blend itself into the ground — `seat/` does. Each decal is painted by PxKit in',
      'the terrain\'s own material, so it IS grass or shingle rather than a colour resembling it.',
      'Draw it over the rock on the same pivot. Two-seat decals carry a frontier: place that frontier',
      'on the level\'s real material boundary, and the rock hides the seam for you.', '',
      '## Not baked', '',
      'Sea, foam, spray, tide-pool fill, and cast shadow. The sidecars give you the pool rect, the',
      'hazard radius, the weed line and the footprint to drive all five.', ''
      ]).join('\n');
    await save(DIR + 'README.md', md);
    log('manifest + readme');
    return { files, sha: SHA };
  }

  throw new Error('unknown phase: ' + o.phase);
};
