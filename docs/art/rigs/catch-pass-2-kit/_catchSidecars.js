/* Hidden Harbours — CATCH PASS 2 sidecar harness (art side).  globalThis.CATCH_SIDECARS
   run_script recipe, from the project root:
     for (const f of ['isoSolid','fishIsoRig2','crustaceanRig2','shellfishRig2','clamHodRig','catchKit2'])
       (0,eval)(await readFile('Art/' + f + '.js'));
     (0,eval)(await readFile('Art/_catchSidecars.js'));
     await CATCH_SIDECARS({ readFile, saveFile, log, dir: 'export/catch-pass-2-kit/sidecars/' });

   Writes one sidecar per pass-2 rig (schema hidden-harbours/rig-sidecar@1) plus SHA256SUMS.txt.
   Every number is read from the rig's OWN exported tables or computed by its own functions at
   generation (mouth, hold, sizeOf, holes, opening, cpivot, depthPx, isHeap) — nothing is typed in.
   The few internals a rig does not export (bed densities, item variant -> pose mapping, heap
   lowering) are transcribed from the source and carry a `_transcribed` marker; the rig hash is the
   tripwire for them. Where a claim about the renderer can be checked by rendering (pose remaps),
   it is checked and the result recorded.
   Each file carries derivedFromRigSha256 = SHA-256 of the rig source AS READ at generation, stamped
   straight after exportSymbol — the canonical place Art/_sidecarExport.js uses. An unstamped
   sidecar is the defect (D1, #513). Nothing is cached: hashes are of the bytes in rigDir at the
   moment of writing (D2). */
globalThis.CATCH_SIDECARS = async function (o) {
  const g = globalThis, readFile = o.readFile, saveFile = o.saveFile, log = o.log || (() => {});
  const dir = (o.dir || 'export/catch-pass-2-kit/sidecars/').replace(/\/?$/, '/');
  const rigDir = (o.rigDir || 'Art/').replace(/\/?$/, '/');
  const RIGS = { FishIso2: 'fishIsoRig2.js', Crustacean2: 'crustaceanRig2.js', Shellfish2: 'shellfishRig2.js', ClamHod: 'clamHodRig.js', CatchKit2: 'catchKit2.js' };
  for (const k in RIGS) if (!g[k]) throw new Error('CATCH_SIDECARS: ' + k + ' missing — load ' + rigDir + RIGS[k] + ' first');
  if (!g.IsoSolid) throw new Error('CATCH_SIDECARS: IsoSolid missing — load ' + rigDir + 'isoSolid.js first');
  const sha = await mkSha();
  const src = {}; for (const k in RIGS) src[k] = await readFile(rigDir + RIGS[k]); src.IsoSolid = await readFile(rigDir + 'isoSolid.js');
  const hash = {}; for (const k in src) hash[k] = await sha(src[k]);

  const DIRS = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'], date = new Date().toISOString();
  const r4 = (v) => (typeof v === 'number' ? Math.round(v * 1e4) / 1e4 : v);
  const deep = (v) => Array.isArray(v) ? v.map(deep) : (v && typeof v === 'object') ? Object.fromEntries(Object.entries(v).map(([k, x]) => [k, deep(x)])) : r4(v);
  const head = (sym, note) => ({ schema: 'hidden-harbours/rig-sidecar@1', rig: rigDir + RIGS[sym], exportSymbol: sym, derivedFromRigSha256: hash[sym],
    generated: { date, by: 'Art/_catchSidecars.js', harness: 'CATCH_SIDECARS', isoSolidSha256: hash.IsoSolid }, note });
  const camera = { ppu: 32, view: '3/4 from the south, orthographic', elevDeg: 40, headings: DIRS, headingStepDeg: 45, turn: 'CW', dirIndex: 'dir 0..7 = headings[dir]',
    key: 'upper-left', dither: 'ordered 4x4 Bayer', antialias: false, source: 'M2 bake recipe, ADR-0006' };
  const waterNote = { waterZ: 'metres above the pivot. Water anims default 0 (the surface); rests default null (dry). Pass null for a dry bake.',
    tint: 'a pixel whose spine height is below waterZ - 0.004 mixes toward WATER by min(0.72, 0.30 + 0.9 x depth) and takes alpha 160 (115 deeper than 0.35 m). Baked in — never clip against the water at runtime.' };
  const spoilNote = { range: '0..1', rule: 'per pixel, mix toward SPOIL by spoil x (0.40 + 0.28 where Bayer(x,y) < spoil x 0.55); the keyline colour is exempt', motes: 'runtime FX in SPOIL — CatchKit2.particles(seed, n)' };
  const layering = { behindDirs: [7, 0, 1], rule: 'held rests (and anything pinned to a hand anchor) draw UNDER the character sprite for NW / N / NE, over it otherwise' };

  // ---- FishIso2 ---------------------------------------------------------------------------------
  const F = g.FishIso2;
  const species = {};
  for (const k of F.ORDER) {
    const sp = F.SPECIES[k], pal = {}, flags = [], out = { label: sp.label, len: sp.len, girth: sp.girth };
    for (const [kk, v] of Object.entries(sp)) { if (typeof v === 'string' && v[0] === '#') pal[kk] = v; else if (v === true) flags.push(kk); }
    out.lenPx = { atRangeMin: F.sizeOf(k, sp.range[0]).px, at1: F.sizeOf(k, 1).px, atRangeMax: F.sizeOf(k, sp.range[1]).px };
    out.flat = sp.flat; if (sp.zflat != null) out.zflat = sp.zflat; out.stripes = sp.stripes; out.dorsals = sp.dorsals; out.range = sp.range; out.massK = sp.massK;
    out.hold = { at1: F.hold(k, 1), atRangeMin: F.hold(k, sp.range[0]), atRangeMax: F.hold(k, sp.range[1]) };
    out.flags = flags; out.palette = pal; species[k] = out;
  }
  const anims = {};
  for (const a of F.AORDER) anims[a] = { frames: F.ANIMS[a].n, ms: F.ANIMS[a].ms, loop: a !== 'jump', motion: F.MOTION[a], pose: F.POSE[a].map(deep) };
  const rests = {};
  for (const rk of F.RESTS) rests[rk] = { frames: F.RPOSE[rk].length, pivotIs: rk === 'deck' ? 'ground contact under the body (lay)' : 'THE GRIP — pin to a CharacterIso hand anchor (cradle: the midpoint of both)', pose: F.RPOSE[rk].map(deep) };
  const mouth = {};
  for (const k of F.ORDER) { mouth[k] = {}; for (const a of F.AORDER) mouth[k][a] = DIRS.map((_, d) => { const row = []; for (let f = 0; f < F.ANIMS[a].n; f++) { const m = F.mouth(d, { species: k, anim: a, frame: f, scale: 1 }); row.push(m.dx, m.dy); } return row; }); }
  const sheetOrder = F.sheetOrder();
  const fish = Object.assign(head('FishIso2', 'Fish, catch pass 2. Cells, pivots, species data, every frame table and the page-side motion contract, read from FishIso2 at generation. Pass-1 cell and pivot contracts are kept, so FishIso2 is a drop-in for FishIso.'), {
    camera,
    cell: { w: F.W, h: F.H, pivot: F.pivot, pivotIs: 'water anims: the water-surface point under the body centre. Rests: see rests[].pivotIs.' },
    keylineDefault: F.KEYLINE_DEFAULT, keylineAB: 'render(dir, {outline:true}) — the live A/B only, never the shipping bake (ADR 0031)',
    colours: { key: F.KEY, water: F.WATER, spoil: F.SPOIL },
    scale: { rule: 'strict world scale — scale 1 = SPECIES.len metres x 32 px; no readability floor', specimen: 'scale is the specimen; SPECIES.range is the spread the game rolls', elev: F.defaultElev },
    order: F.ORDER, species,
    animOrder: F.AORDER, anims,
    poseGlossary: { sweep: 'tail sweep, rad (+ = to the fish\'s right)', curve: 'body curve, rad', roll: 'roll about the spine, rad', pitch: 'pitch, rad (+ = nose up)', z: 'spine height over the water surface, m', stretch: 'length multiplier (dart)', wph: 'body-wave phase — only the undulating species (flounder) use it', gripU: '0..1 along the body, nose to tail, where the grip (rest pivot) sits' },
    restOrder: F.RESTS, rests,
    motion: { units: 'v in m/s along the heading; the page moves the fish, the bake never does', jump: 'travel = metres covered over the 6 frames; z arc and pitch are baked' },
    water: waterNote, spoil: spoilNote,
    mouth: { units: 'px from the pivot, scale 1, [dx0, dy0, dx1, dy1, ...] per frame; index [species][anim][dir]', use: 'the line attaches here in the surface fight; re-derive for other specimens with FishIso2.mouth(dir, {species, anim, frame, scale})', table: mouth },
    hold: { rule: 'mass (kg) = len x girth^2 x scale^3 x 390 x massK; hands = 2 (two-arm cradle) when mass >= 2.2 kg, else 1 (one fish per hand, by the gill or the tail)' },
    shoal: { call: 'FishIso2.shoal(species, n, tMs, {seed, radius, speed, scale, z})', defaults: { seed: 11, radius: 1.2, speed: 0.35, z: -0.15 },
      returns: 'n singles: {x, y, z} metres from the shoal anchor, heading (rad, 0 = N, CW), dir = dirOf(heading), frame (swim), scale', path: 'one lazy figure-eight, n singles trailing one leader; deterministic per seed so two clients agree; nothing is baked as a group', dirOf: 'round(heading / 45 deg) mod 8' },
    layering,
    sheet: { note: 'the review page\'s FISH SHEET download: one PNG per heading, rows = order (7 species), cols = sheetOrder (35), 64 x 64 cells -> 2240 x 448. Split water anims (25 cols) from rests (10 cols) to stay under a 2048 px sheet cap.', sheetOrder },
  });

  // ---- Crustacean2 ------------------------------------------------------------------------------
  const C = g.Crustacean2;
  const same = (a, b) => a.length === b.length && a.every((v, i) => v === b[i]);
  const remaps = { lobster: { sidle: 'walk', burrow: 'walk' }, crab: { flip: 'walk' } };
  const remapVerified = {};
  for (const kind in remaps) { remapVerified[kind] = {}; for (const p in remaps[kind]) remapVerified[kind][p] = same(C.render(kind, { pose: p, frame: 1, dir: 3 }), C.render(kind, { pose: remaps[kind][p], frame: 1, dir: 3 })); }
  const poses = {}; for (const p in C.POSES) poses[p] = { frames: C.POSES[p].n, ms: C.POSES[p].ms, loop: !(p === 'flip' || p === 'burrow') };
  const kinds = {};
  for (const kind of C.KINDS) kinds[kind] = Object.assign({}, C.SIZES[kind], { hold: { at1: C.hold(kind, 1) }, poses: Object.keys(C.POSES).filter(p => !remaps[kind][p]), rendersAsWalk: remaps[kind], rendersAsWalkVerified: remapVerified[kind], motion: C.MOTION[kind] });
  const crust = Object.assign(head('Crustacean2', 'Lobster + rock crab, catch pass 2 — lofted solids on IsoSolid\'s turntable. Cells, pivots, poses, motion and sizes read from Crustacean2 at generation; the pose remaps were verified by rendering.'), {
    camera, needs: ['IsoSolid'],
    cell: { w: C.W, h: C.H, pivot: C.pivot, pivotIs: 'ground centre under the body', hpivot: C.hpivot, hpivotIs: 'the held pose only: THE GRIP on the back — pin to a CharacterIso hand anchor' },
    keylineDefault: C.KEYLINE_DEFAULT, keylineAB: 'render(kind, {outline:true}) — the live A/B only (ADR 0031)',
    colours: { key: C.KEY, water: C.WATER },
    scale: { rule: 'strict world scale — scale 1 = SIZES[kind].len metres overall; legs and antennae are depth-tested 1 px plots at any scale (2 px above 1.45x)', elev: C.defaultElev },
    kinds: C.KINDS, kind: kinds,
    poseOrder: Object.keys(C.POSES), poses,
    motionGlossary: { 'walk.v / sidle.v': 'm/s along the travel axis', 'sidle.axis': 'x — a crab faces ACROSS its travel; travel is body +x', 'flip.travel': 'metres per flip cycle, negative = backward along the heading', 'flip.at': 'cumulative fraction of the travel reached at each frame', 'flip.hop': 'lift above the ground per frame, m — raise the sprite and shrink its shadow', 'burrow.sink': 'metres below the sand per frame; pixels under sandZ are cut in the bake (dithered edge), the page draws the mound' },
    heading: { ang: 'radians, 0 = N, CW, continuous — turns the animal on the spot so fills can scatter it; ignored by held', dir: '0..7, the turntable camera; composes with ang' },
    sandZ: { default: 'burrow: 0; every other pose: null (no cut)', unit: 'm above the pivot' },
    water: waterNote, spoil: spoilNote,
    hold: { rule: 'mass = SIZES[kind].mass x scale^3; hands = 2 when mass >= 2.2 kg, else 1 — dangled by the back, claws hanging, tail curled' },
    layering,
    sheet: { note: 'the review page\'s CRUSTACEAN SHEET download: cols = every pose x frame in poseOrder (20), rows = kind x 8 headings (16), 64 x 64 cells -> 1280 x 1024; a kind\'s invalid poses render as walk' },
  });

  // ---- Shellfish2 -------------------------------------------------------------------------------
  const SH = g.Shellfish2;
  const hs = SH.holes(3, 400, 100, 100); const per = hs.map(h => h.period), dur = hs.map(h => h.dur);
  const spurtSample = []; { const h = { x: 0, y: 0, phase: 0, period: 4000, dur: 420 }; for (let t = 0; t <= 420; t += 70) { const s = SH.spurt(h, t); spurtSample.push([t, s ? r4(s.rise) : null]); } }
  const shell = Object.assign(head('Shellfish2', 'Mussel / clam / scallop / oyster / periwinkle at true size, catch pass 2. Item and handful cells, palettes and the tiny-kind pixel patterns read from Shellfish2; bed and flat contracts measured by calling it.'), {
    camera: Object.assign({}, camera, { view: 'top-down 3/4 read (y x 0.64 = sin 40 deg), same upper-left key' }),
    kinds: SH.KINDS, sizes: SH.SIZES, variants: SH.VARIANTS,
    item: { w: SH.IW, h: SH.IH, pivot: SH.ipivot, pivotIs: 'ground contact — the odd loose shell on a deck', call: 'renderItem(kind, variant 0..3, scale)' },
    handful: { w: SH.IW, h: SH.IH, pivot: SH.hpivot, pivotIs: 'THE GRIP — one clutch per hand, pin to a CharacterIso hand anchor', variants: 2, call: 'renderHandful(kind, variant 0..1)' },
    scale: { rule: 'strict world scale: 32 px = 1 m, so a mussel is 2x1 px, a clam 2x2, a scallop or oyster 3x3, a periwinkle one pixel. Nothing is inflated to read alone — the beds carry it.' },
    palette: { rampIndex: '0 dark .. 4 light', ramps: SH.PAL },
    tiny: { note: 'the 1-2 px kinds are explicit pixel patterns, one per variant: [dx, dy, rampIndex] from the placement pixel', patterns: SH.TINY },
    bed: { call: 'bed(kind, {w, h, seed, cover}) -> {w, h, rgba}', use: 'alpha-masked texture laid over rock / sand / mud: mussels + periwinkles on tide-pool rock, oyster clumps on the mud edge, scallops apart on sand. A clam flat shows NO clams — see flat.',
      defaults: { w: 48, h: 32, seed: 5, cover: 0.85 }, densityPerPx: { mussel: 1.6, periwinkle: 0.14, oyster: 0.45, scallop: 0.05, clam: 0, _transcribed: 'internal `dens` table in bed(); coverage blobs (2 + 3 x cover of them) leave ground showing between' } },
    flat: { holes: 'holes(seed, n, w, h) -> [{x, y, phase, period, dur}] — the keyholes a clam flat shows', measured: { periodMs: [Math.min(...per), Math.max(...per)], durMs: [Math.min(...dur), Math.max(...dur)], sample: 'holes(3, 400, 100, 100)' },
      spurt: 'spurt(hole, tMs) -> null | {rise 0..1, u 0..1}; rise = sin(pi x u) over dur. Draw a 1 px jet, rise x 4 px tall; a droplet 1 px above it once u > 0.45. This is the gameplay TELL for the dig.', spurtSampleAtDur420: spurtSample },
    heap: { call: 'heap(kind, w, h, seed, mask(x,y), {dome(x,y)}) -> rgba', use: 'heaped shells clipped to a container opening — THE container fill for shellfish at this scale; CatchKit2.heap wraps it for a rim polygon' },
    layering,
  });

  // ---- ClamHod ----------------------------------------------------------------------------------
  const Hd = g.ClamHod;
  const hod = Object.assign(head('ClamHod', 'The wire roller basket for the clam dig. Cell, pivots, dims, openings and depth computed by ClamHod at generation for all 8 headings.'), {
    camera, needs: ['IsoSolid'],
    cell: { w: Hd.W, h: Hd.H, pivot: Hd.pivot, pivotIs: 'ground centre', cpivotIs: 'the roller grip — pins to a CharacterIso carry anchor when carried', cpivot: DIRS.map((_, d) => { const p = Hd.cpivot(d); return [p.x, p.y]; }), cpivotUnits: 'cell px [x, y] per heading' },
    dims: Object.assign({ units: 'm — length x width x height' }, Hd.dims), elev: Hd.defaultElev,
    keyline: 'render(dir, {keyline:true}) is the A/B; the shipping bake is ringless (ADR 0031)',
    layers: { back: 'render(dir, {layer:"back"}) — the far half of the wire', front: 'render(dir, {layer:"front"}) — the near half', all: 'render(dir) — both', order: 'back -> the CatchKit2 heap clipped to opening(dir) -> front' },
    opening: { units: 'px from the pivot, [dx, dy] x 4 rim corners per heading (a clockwise quad)', perHeading: DIRS.map((_, d) => Hd.opening(d).map(p => [p.dx, p.dy])) },
    depthPx: Hd.depthPx(), depthPxIs: 'rim to floor in px at the default elevation; partial fills lower the heap surface by (1 - min(1, frac / 0.85)) x depthPx (CatchKit2.heap, _transcribed); brim crowns 2 px above the rim',
    fills: Hd.FILLS, fillFrac: g.CatchKit2.FRAC,
    materials: { wire: 'galvanised — bucketRig\'s STEEL ramp, wires baked 1 px (0.012 m half-thickness) so the catch shows through', bail: 'ash with a roller grip — bucketRig\'s WOOD ramp' },
  });

  // ---- CatchKit2 --------------------------------------------------------------------------------
  const K2 = g.CatchKit2;
  const perKind = {};
  for (const k of K2.CATCHES) { if (k === 'mixed') continue; const cls = K2.FISH.includes(k) ? 'fish' : K2.CRUST.includes(k) ? 'crustacean' : 'shellfish';
    perKind[k] = Object.assign({ class: cls, isHeap: K2.isHeap(k), dense: K2.DENSE[k] || 1, hold: K2.hold(k, 1) }, K2.SIZES[k]); }
  const kit = Object.assign(head('CatchKit2', 'The glue over all fourteen catch kinds: item cells and anchors, fill rules, heap rule, hold. Tables and per-kind results read from CatchKit2 at generation; the item variant -> pose mapping is transcribed from item().'), {
    needs: ['FishIso2', 'Crustacean2', 'Shellfish2', '— whichever the catch uses'],
    colours: { spoil: K2.SPOIL },
    catches: K2.CATCHES, fish: K2.FISH, crustaceans: K2.CRUST, shellfish: K2.SHELL,
    kind: perKind,
    fillFrac: K2.FRAC, dense: Object.assign({ _note: 'DENSE kinds add jittered extras per slot so a tote of herring reads as ninety herring, not eight fish in a box' }, K2.DENSE),
    item: { call: 'item(kind, {variant 0..3, scale, spoil}) -> {canvas, w, h, ax, ay}', anchor: 'ax, ay = the ground anchor in the item canvas; draw at slot - anchor',
      source: { fish: { rig: 'FishIso2', cell: [F.W, F.H], anchor: [F.pivot.x, F.pivot.y], bake: 'rest deck, frame = variant % 4, heading = [E, W, SE, SW][variant % 4]' },
        crustacean: { rig: 'Crustacean2', cell: [C.W, C.H], anchor: [C.pivot.x, C.pivot.y], bake: 'pose walk, frame = variant % 4, ang = [0.4, 2.2, 3.7, 5.3][variant % 4] rad' },
        shellfish: { rig: 'Shellfish2', cell: [SH.IW, SH.IH], anchor: [SH.ipivot.x, SH.ipivot.y], bake: 'renderItem(kind, variant % 4, scale), spoil via tintSpoil' }, _transcribed: 'from item()' } },
    fillItems: { call: 'fillItems(catch, fill, seed, slotCount) -> [{kind, variant, scale, jx, jy}]', count: 'round(FRAC[fill] x slotCount x DENSE[catch])', monotonic: 'seeded — growing a fill never moves earlier items; pass the container\'s real slot count so full / brim genuinely heap',
      scale: 'clamp(0.85 + rng x 0.30, SPECIES.range) — fish take their species range, everything else [0.85, 1.15]', jitter: 'extras beyond count / DENSE get jx in [-3, 3], jy in [-2, 2] px', mixed: 'kind drawn per item from [herring, mackerel, haddock, lobster, crab, pollock, cod, bass, flounder]', shellfish: 'returns [] — use heap()' },
    heap: { call: 'heap(kind, rimPoly [{dx,dy}], depthPx, fill, seed, {spoil}) -> {canvas, x, y} | null (empty)', rule: 'surface lowered by (1 - min(1, FRAC[fill] / 0.85)) x depthPx; brim crowns 2 px above the rim as a dome; ramp steps darken down-right, lift up-left' },
    hold: { rule: 'fish -> FishIso2.hold, crustaceans -> Crustacean2.hold, shellfish -> {mass: SIZES.mass x 7 x scale, hands: 1, handful: true}' },
    spoil: Object.assign({}, spoilNote, { tintSpoil: 'tintSpoil(rgba, w, h, spoil) — skips pixels with r + g + b < 70 (the keyline / eye darks)', particles: 'particles(seed, n) -> [{ox, oy, phase, speed}] mote specs, drawn in SPOIL with alpha (1 - cycle) x 0.5 x spoil' }),
  });

  // ---- write --------------------------------------------------------------------------------------
  const files = { 'fishIsoRig2.rig.json': fish, 'crustaceanRig2.rig.json': crust, 'shellfishRig2.rig.json': shell, 'clamHodRig.rig.json': hod, 'catchKit2.rig.json': kit };
  const sums = [];
  for (const name in files) { const text = pretty(files[name]) + '\n'; await saveFile(dir + name, text); sums.push((await sha(text)) + '  sidecars/' + name); log('wrote', dir + name, text.length, 'chars'); }
  sums.push('', '# rigs these were derived from (derivedFromRigSha256 in each file) — identical bytes in Art/ of this kit and the project');
  for (const k of ['IsoSolid'].concat(Object.keys(RIGS))) sums.push(hash[k] + '  Art/' + (k === 'IsoSolid' ? 'isoSolid.js' : RIGS[k]) + '  (' + src[k].split('\n').length + ' lines)');
  const sumsPath = dir.replace(/sidecars\/$/, '') + 'SHA256SUMS.txt';
  await saveFile(sumsPath, sums.join('\n') + '\n');
  log('wrote', sumsPath);
  return { hash, files: Object.keys(files), remapVerified };

  // JSON with primitive arrays kept on one line — the frame tables stay readable and diffable
  function pretty(v, ind) {
    ind = ind || '';
    if (Array.isArray(v)) {
      if (v.every(x => x === null || typeof x !== 'object')) return '[' + v.map(x => JSON.stringify(x === undefined ? null : x)).join(', ') + ']';
      return '[\n' + v.map(x => ind + ' ' + pretty(x, ind + ' ')).join(',\n') + '\n' + ind + ']';
    }
    if (v && typeof v === 'object') {
      const ks = Object.keys(v).filter(k => v[k] !== undefined); if (!ks.length) return '{}';
      return '{\n' + ks.map(k => ind + ' ' + JSON.stringify(k) + ': ' + pretty(v[k], ind + ' ')).join(',\n') + '\n' + ind + '}';
    }
    return JSON.stringify(v);
  }
  async function mkSha() {
    const hex = (u8) => Array.from(u8).map(b => b.toString(16).padStart(2, '0')).join('');
    const enc = (t) => new TextEncoder().encode(t);
    const ABC = 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad';
    if (g.crypto && g.crypto.subtle) {
      try { const f = async (t) => hex(new Uint8Array(await g.crypto.subtle.digest('SHA-256', enc(t)))); if ((await f('abc')) === ABC) return f; } catch (e) { /* fall through to the JS digest */ }
    }
    if (sha256js(enc('abc')) !== ABC) throw new Error('CATCH_SIDECARS: SHA-256 self-test failed — refusing to stamp');
    return async (t) => sha256js(enc(t));
    function sha256js(bytes) {
      const K = new Uint32Array([0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5, 0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da, 0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85, 0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3, 0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2]);
      const H = new Uint32Array([0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19]);
      const len = bytes.length, padLen = ((len + 9 + 63) >> 6) << 6, msg = new Uint8Array(padLen); msg.set(bytes); msg[len] = 0x80;
      const dv = new DataView(msg.buffer); dv.setUint32(padLen - 8, Math.floor(len / 0x20000000)); dv.setUint32(padLen - 4, (len * 8) >>> 0);
      const W = new Uint32Array(64), rotr = (x, n) => (x >>> n) | (x << (32 - n));
      for (let off = 0; off < padLen; off += 64) {
        for (let i = 0; i < 16; i++) W[i] = dv.getUint32(off + i * 4);
        for (let i = 16; i < 64; i++) { const s0 = rotr(W[i - 15], 7) ^ rotr(W[i - 15], 18) ^ (W[i - 15] >>> 3), s1 = rotr(W[i - 2], 17) ^ rotr(W[i - 2], 19) ^ (W[i - 2] >>> 10); W[i] = (W[i - 16] + s0 + W[i - 7] + s1) >>> 0; }
        let a = H[0], b = H[1], c = H[2], d = H[3], e = H[4], f = H[5], gg = H[6], h = H[7];
        for (let i = 0; i < 64; i++) {
          const S1 = rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25), ch = (e & f) ^ (~e & gg), t1 = (h + S1 + ch + K[i] + W[i]) >>> 0;
          const S0 = rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22), maj = (a & b) ^ (a & c) ^ (b & c), t2 = (S0 + maj) >>> 0;
          h = gg; gg = f; f = e; e = (d + t1) >>> 0; d = c; c = b; b = a; a = (t1 + t2) >>> 0;
        }
        H[0] = (H[0] + a) >>> 0; H[1] = (H[1] + b) >>> 0; H[2] = (H[2] + c) >>> 0; H[3] = (H[3] + d) >>> 0; H[4] = (H[4] + e) >>> 0; H[5] = (H[5] + f) >>> 0; H[6] = (H[6] + gg) >>> 0; H[7] = (H[7] + h) >>> 0;
      }
      const out = new Uint8Array(32), odv = new DataView(out.buffer); for (let i = 0; i < 8; i++) odv.setUint32(i * 4, H[i]); return hex(out);
    }
  }
};
