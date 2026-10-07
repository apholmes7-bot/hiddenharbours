/* Hidden Harbours — TREE KIT CHECKS, pass 4.1.  globalThis.TREE_CHECKS
   Deterministic (no timings, no dates): a re-run reproduces checks/out/ byte for byte.
   Node 18+, no packages:  node checks/run.js [name …]     (all, in ORDER, when no name is given)
   In a page or sandbox: load treeIsoRig4.js, weatherSky.js, _treeGameplay.js, treeMaps4.js,
   lib/treeIsoRig3.js and this file, then  await TREE_CHECKS.run(name, io, keys?)  and  format(result).

     sums      SHA256SUMS.txt against every file it lists, and no kit file left out of it
     stamps    each sidecar's three hashes against the rig, sky and writer as shipped; their self-tests
     sidecars  the writer re-run: each committed sidecar is byte for byte what it writes today
     cells     pass 3 → 4.1 cell · pivot · sheet for every species × stage; 4.1 pivot = trunk-foot column;
               every sheet ≤ 2048; the README table says the same, row for row
     snow      the snow map against relight() output at covers 0 · 6 · 64 · 127 · 153 · 200 · 254, every
               species × stage × season group, variant 1, at rest and in a gale frame; a full 0–254 sweep
               on three mature trees
     wind      the reference shader against the rig's baked frames, calm · breeze · gale × west · east,
               gust 0.5, frames 0 4 8 12, mature summer variant 1, every species
     seasons   which rest-pose maps autumn and winter share with summer, every species × stage × variant
     budget    bytes: every level and direction baked, against rest pose + weights; the sheets each season adds

   io = { read(path) → text, readBytes(path) → bytes, list() → kit-relative paths, sha256(bytes) → hex }
   (all may be async). Paths are relative to the kit root.                                              */
(function (root) {
  'use strict';
  const ORDER = ['stamps', 'sidecars', 'cells', 'snow', 'wind', 'seasons', 'budget', 'sums'];
  const G = () => ({ R: root.TreeRig4, R3: root.TreeRig3, TG: root.TREE_GAMEPLAY, M4: root.TreeMaps4 });
  const pad = (s, n) => String(s).padEnd(n), lp = (s, n) => String(s).padStart(n);
  const pct = (a, b) => b ? (a / b * 100).toFixed(1) + '%' : '–';
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
  const KEYS = () => G().R.SPECIES.map(s => s.key);

  // ---- sums ------------------------------------------------------------------------------------------
  async function sums(io) {
    const L = [], listed = new Map(); let bad = 0;
    for (const ln of String(await io.read('SHA256SUMS.txt')).split('\n')) { const m = ln.match(/^([0-9a-f]{64}) {2}(.+)$/); if (m) listed.set(m[2], m[1]); }
    for (const [f, h] of listed) {
      let got = null; try { got = await io.sha256(await io.readBytes(f)); } catch (e) { got = null; }
      if (got !== h) { bad++; L.push('FAIL      ' + f + '  listed ' + h.slice(0, 12) + '…  file ' + (got ? got.slice(0, 12) + '…' : 'missing')); }
    }
    const kit = (await io.list()).filter(f => f !== 'SHA256SUMS.txt' && f !== 'checks/out/sums.txt').sort();
    const unlisted = kit.filter(f => !listed.has(f));
    unlisted.forEach(f => L.push('UNLISTED  ' + f));
    L.unshift(listed.size + ' files listed, ' + (listed.size - bad) + ' match their hash · ' + unlisted.length + ' kit files not listed',
      '(this file and SHA256SUMS.txt itself are the two files the list cannot contain)', '');
    return { name: 'sums', lines: L, failed: bad + unlisted.length };
  }

  // ---- stamps ----------------------------------------------------------------------------------------
  async function hashes(io) { const h = {}; for (const f of ['treeIsoRig4.js', 'weatherSky.js', '_treeGameplay.js']) h[f] = await io.sha256(await io.readBytes(f)); return h; }
  async function stamps(io) {
    const { TG } = G(), h = await hashes(io), L = [], rig = h['treeIsoRig4.js'], sky = h['weatherSky.js'], wr = h['_treeGameplay.js'];
    let bad = 0;
    L.push('treeIsoRig4.js    ' + rig, 'weatherSky.js     ' + sky, '_treeGameplay.js  ' + wr, '');
    for (const key of KEYS()) {
      const doc = JSON.parse(await io.read('gameplay/' + TG.fileName(key)));
      const miss = [['rig', doc.derivedFromRigSha256, rig], ['sky', doc.skyDerivedFromRigSha256, sky], ['writer', doc.writerDerivedFromRigSha256, wr]].filter(x => x[1] !== x[2]).map(x => x[0]);
      const ok = !miss.length && doc._checks && doc._checks.failed === 0;
      if (!ok) bad++;
      L.push(pad(key, 16) + (miss.length ? 'STALE ' + miss.join(' + ') : 'hashes match') + ' · self-test ' + doc._checks.checked + ' checks, ' + doc._checks.failed + ' failed · generated ' + doc.generated);
    }
    return { name: 'stamps', lines: L, failed: bad };
  }

  // ---- sidecars ----------------------------------------------------------------------------------------
  async function sidecars(io, keys) {
    const { R, TG } = G(), h = await hashes(io), L = [];
    let bad = 0, checks = 0;
    for (const key of keys || KEYS()) {
      const txt = String(await io.read('gameplay/' + TG.fileName(key))), doc = JSON.parse(txt);
      const out = TG.stamp(TG.sidecar(R, key, { generated: doc.generated }), h['treeIsoRig4.js'], h['weatherSky.js'], h['_treeGameplay.js']);
      const same = JSON.stringify(out, null, 2) === txt.replace(/\s+$/, '');
      if (!same) bad++;
      checks += out._checks.checked;
      L.push(pad(key, 16) + (same ? 'identical to the committed file' : 'DIFFERS from the committed file') + ' · writer self-test ' + out._checks.checked + ' checks, ' + out._checks.failed + ' failed');
    }
    if (!keys) L.push('', checks + ' self-test checks re-run');
    return { name: 'sidecars', lines: L, failed: bad };
  }

  // ---- cells -----------------------------------------------------------------------------------------
  function cellRows() {
    const { R, R3 } = G(), rows = [];
    for (const sp of R.SPECIES) for (const st of R.STAGE_KEYS) {
      const a = R3.sheetSpec(sp.key, R3.STAGES[st]), b = R.sheetSpec(sp.key, R.STAGES[st]);
      rows.push({ key: sp.key, name: sp.name, stage: st, a, b,
        md: '| ' + sp.name + ' | ' + st + ' | ' + a.cell.join('×') + ' → ' + b.cell.join('×') + ' | ' + a.pivot.join(', ') + ' → ' + b.pivot.join(', ') + ' | ' + a.w + '×' + a.h + ' → ' + b.w + '×' + b.h + ' |' });
    }
    return rows;
  }
  async function cells(io) {
    const { R } = G(), L = [], rows = cellRows();
    let bad = 0, readme = '';
    try { readme = String(await io.read('README.md')); } catch (e) { readme = ''; }
    L.push(pad('species', 16) + pad('stage', 9) + pad('cell 3', 10) + pad('cell 4.1', 10) + pad('pivot 3', 10) + pad('pivot 4.1', 10) + pad('sheet 3', 11) + pad('sheet 4.1', 11) + 'foot  fits  README');
    for (const r of rows) {
      const m = R.model(r.key, { stage: r.stage, season: 'summer', variant: 0 }), foot = Math.abs(m.g.cx + m.cell.dx - (m.pivot.x + 0.5)) < 1e-6;
      const inReadme = readme.indexOf(r.md) >= 0, ok = foot && r.b.fits && inReadme;
      if (!ok) bad++;
      L.push(pad(r.key, 16) + pad(r.stage, 9) + pad(r.a.cell.join('×'), 10) + pad(r.b.cell.join('×'), 10) + pad(r.a.pivot.join(','), 10) + pad(r.b.pivot.join(','), 10) +
        pad(r.a.w + '×' + r.a.h, 11) + pad(r.b.w + '×' + r.b.h, 11) + pad(foot ? 'ok' : 'OFF', 6) + pad(r.b.fits ? 'ok' : 'NO', 6) + (inReadme ? 'row matches' : 'ROW MISSING'));
    }
    const s3 = rows.reduce((s, r) => s + r.a.w * r.a.h, 0), s4 = rows.reduce((s, r) => s + r.b.w * r.b.h, 0);
    L.push('', 'pass 3 sheet = 4 variants × 4 sway frames; pass 4.1 sheet = one variant × 16 wind frames (4 × 4).',
      'sheet pixels, all 40: pass 3 ' + s3.toLocaleString('en-US') + ' · pass 4.1 ' + s4.toLocaleString('en-US') + ' (one variant; × 4 for all four)',
      'largest 4.1 sheet: ' + rows.reduce((b, r) => r.b.w * r.b.h > b.b.w * b.b.h ? r : b).key + ' mature ' + rows.reduce((b, r) => r.b.w * r.b.h > b.b.w * b.b.h ? r : b).b.w + '×' + rows.reduce((b, r) => r.b.w * r.b.h > b.b.w * b.b.h ? r : b).b.h);
    return { name: 'cells', lines: L, failed: bad };
  }

  // ---- snow ------------------------------------------------------------------------------------------
  // relight() with the grade off paints a snowed pixel from the SNOW ramp, bands 1–4 (or its glint); those
  // five colours are the detector. Cover 0 must show none of them anywhere, which proves no other ramp
  // lands on them. No sun: the rule does not read it, and relight is faster without the sun map.
  const SNOWC = new Set(['#7d93a0', '#a8bcc4', '#cfdde1', '#eef4f4', '#f8fbfb'].map(h => h2r(h).join(',')));
  function snowCompare(fr, k) {
    const { R, M4 } = G(), v = fr.v, N = v.w * v.h, map = M4.snowMap(fr);
    const px = R.relight(fr, Object.assign({}, R.REF_SKY, { sunI: 0, grade: false, snow: k / 254, fog: 0, wet: 0 }));
    let snowed = 0, mis = 0;
    for (let i = 0; i < N; i++) {
      if (!v.a[i]) continue;
      const a = SNOWC.has(px[i * 4] + ',' + px[i * 4 + 1] + ',' + px[i * 4 + 2]), b = map[i] <= k;
      if (a) snowed++; if (a !== b) mis++;
    }
    return { snowed, mis, px: v.a.reduce((s, x) => s + x, 0), never: (() => { let n = 0; for (let i = 0; i < N; i++) if (v.a[i] && map[i] === 255) n++; return n; })() };
  }
  const COVERS = [0, 6, 64, 127, 153, 200, 254];
  function snow(io, keys, part) {
    const { R, M4 } = G(), L = [];
    let bad = 0, frames = 0, tests = 0;
    if (!part || part === 'grid') {
      if (!keys) L.push('covers k/254: ' + COVERS.join(' · ') + '   columns: pixels snowed at each cover (relight) · mismatches with map ≤ k', '');
      for (const key of keys || KEYS()) for (const stage of R.STAGE_KEYS) for (const season of ['summer', 'winter']) {
        const o = { stage, season, variant: 0 };
        for (const [tag, fr] of [['rest', M4.rest(key, o)], ['gale', R.frame(key, Object.assign({}, o, { wind: { w: 1, gust: 1, dir: 1 }, frame: 5 }))]]) {
          const res = COVERS.map(k => snowCompare(fr, k)), mis = res.reduce((s, r) => s + r.mis, 0);
          frames++; tests += res.length; if (mis) bad++;
          L.push(pad(key, 16) + pad(stage, 8) + pad(season, 7) + pad(tag, 5) + lp(res[0].px, 6) + ' px  ' + res.map(r => lp(r.snowed, 5)).join(' ') + '   mismatches ' + mis);
        }
      }
      if (!keys) L.push('', frames + ' frames × ' + COVERS.length + ' covers = ' + tests + ' relights, ' + bad + ' frames with a mismatch');
    }
    if (!part || part === 'sweep') {
      L.push('', 'full sweep, every cover 0–254:');
      for (const [key, stage, season] of [['RedMaple', 'mature', 'summer'], ['BalsamFir', 'mature', 'winter'], ['RedOak', 'mature', 'winter']]) {
        const fr = M4.rest(key, { stage, season, variant: 0 }); let mis = 0, top = 0;
        for (let k = 0; k <= 254; k++) { const r = snowCompare(fr, k); mis += r.mis; if (k === 254) top = r.snowed; }
        if (mis) bad++;
        L.push(pad(key, 16) + pad(stage, 8) + pad(season, 7) + 'rest  255 covers · ' + top + ' px snowed at 100% · mismatches ' + mis);
      }
    }
    return { name: 'snow', lines: L, failed: bad };
  }

  // ---- wind ------------------------------------------------------------------------------------------
  const CONDS = [['calm', 0, 1], ['calm', 0, -1], ['breeze', 0.3, 1], ['breeze', 0.3, -1], ['gale', 1, 1], ['gale', 1, -1]];
  const WFRAMES = [0, 4, 8, 12], GUST = 0.5;
  function windCompare(a, b, la, lb) {
    const N = a.v.w * a.v.h; let uni = 0, both = 0, geo = 0, lit = 0;
    for (let i = 0; i < N; i++) {
      const A = a.v.a[i], B = b.v.a[i]; if (!A && !B) continue; uni++;
      if (!A || !B) continue; both++;
      if (a.v.mat[i] === b.v.mat[i] && a.v.st[i] === b.v.st[i]) geo++;
      if (la[i * 4] === lb[i * 4] && la[i * 4 + 1] === lb[i * 4 + 1] && la[i * 4 + 2] === lb[i * 4 + 2]) lit++;
    }
    return { uni, both, geo, lit };
  }
  function wind(io, keys) {
    const { R, M4 } = G(), L = [], sum = {};
    let bad = 0;
    if (!keys || keys[0] === KEYS()[0]) L.push('mature · summer · variant 1 · gust ' + GUST + ' · frames ' + WFRAMES.join(' ') + ', lit under the 14:00 reference sky',
      'silhouette = IoU of the two silhouettes · geometry = same material and same leaf stamp · lit = same colour (over the union)',
      'rest = the rest pose, unmoved, against the same rig frames: what a sprite with no wind would score', '');
    for (const key of keys || KEYS()) {
      const o = { stage: 'mature', season: 'summer', variant: 0 }, rf = M4.rest(key, o), lr = R.relight(rf, R.REF_SKY);
      for (const [nm, w, dir] of CONDS) {
        const acc = { uni: 0, both: 0, geo: 0, lit: 0 }, base = { uni: 0, both: 0, geo: 0, lit: 0 };
        for (const f of WFRAMES) {
          const W = { w, gust: GUST, dir }, rg = R.frame(key, Object.assign({}, o, { wind: W, frame: f })), sh = M4.shade(rf, W, f), lg = R.relight(rg, R.REF_SKY);
          const c = windCompare(sh, rg, R.relight(sh, R.REF_SKY), lg), b = windCompare(rf, rg, lr, lg);
          for (const k in acc) { acc[k] += c[k]; base[k] += b[k]; }
        }
        const tag = nm + (dir > 0 ? ' west' : ' east'), iou = acc.both / acc.uni;
        if (iou < 0.95 || acc.geo / acc.uni < 0.8) bad++;
        (sum[tag] = sum[tag] || []).push([acc.both / acc.uni, acc.geo / acc.uni, acc.lit / acc.uni, base.geo / base.uni, base.lit / base.uni]);
        L.push(pad(key, 16) + pad(tag, 13) + 'silhouette ' + lp(pct(acc.both, acc.uni), 6) + ' · geometry ' + lp(pct(acc.geo, acc.uni), 6) + ' · lit ' + lp(pct(acc.lit, acc.uni), 6) +
          '   rest: geometry ' + lp(pct(base.geo, base.uni), 6) + ' · lit ' + lp(pct(base.lit, base.uni), 6));
      }
    }
    return { name: 'wind', lines: L, failed: bad, sum };
  }
  function windSummary(sums) {
    const L = ['', 'mean over the species       silhouette  geometry   lit     rest: geometry  lit'];
    for (const [nm, , dir] of CONDS) {
      const tag = nm + (dir > 0 ? ' west' : ' east'), a = sums[tag] || [];
      if (!a.length) continue;
      const m = (k) => (a.reduce((s, r) => s + r[k], 0) / a.length * 100).toFixed(1) + '%';
      L.push(pad('  ' + tag, 28) + lp(m(0), 9) + lp(m(1), 10) + lp(m(2), 8) + lp(m(3), 15) + lp(m(4), 8));
    }
    L.push('', 'pass: silhouette ≥ 95% and geometry ≥ 80% in every row.');
    return L;
  }

  // ---- seasons ---------------------------------------------------------------------------------------
  function bytesEq(a, b) { if (a.length !== b.length) return false; for (let i = 0; i < a.length; i++) if (a[i] !== b[i]) return false; return true; }
  function seasons(io, keys) {
    const { R, M4 } = G(), L = [], MAPS = M4.SHEET_MAPS;
    let bad = 0;
    if (!keys || keys[0] === KEYS()[0]) L.push('rest pose, variants 1–4, byte comparison of each map against summer', 'maps: ' + MAPS.join(' · '), '');
    for (const key of keys || KEYS()) for (const stage of R.STAGE_KEYS) {
      const au = {}, wi = {};
      MAPS.forEach(m => { au[m] = 0; wi[m] = 0; });
      for (let variant = 0; variant < 4; variant++) {
        const fs = M4.rest(key, { stage, season: 'summer', variant }), fa = M4.rest(key, { stage, season: 'autumn', variant }), fw = M4.rest(key, { stage, season: 'winter', variant });
        for (const m of MAPS) { const s = M4.view(fs, m); if (bytesEq(s, M4.view(fa, m))) au[m]++; if (fw.v.w === fs.v.w && bytesEq(s, M4.view(fw, m))) wi[m]++; }
      }
      const shared = (o) => MAPS.filter(m => o[m] === 4), partial = (o) => MAPS.filter(m => o[m] > 0 && o[m] < 4);
      const aS = shared(au), wS = shared(wi), fall = !!R.byKey[key].fall;
      const ok = MAPS.filter(m => m !== 'unlit').every(m => au[m] === 4) && (fall ? au.unlit === 0 : au.unlit === 4);
      if (!ok) bad++;
      L.push(pad(key, 16) + pad(stage, 8) + 'autumn shares ' + (aS.length ? aS.join(' ') : 'nothing') + (au.unlit === 0 ? ' · own _unlit (fall colour)' : '') + (partial(au).length ? ' · PARTIAL ' + partial(au).join(' ') : '') +
        '   winter shares ' + (wS.length ? wS.join(' ') : 'nothing'));
    }
    if (!keys) L.push('', 'pass: autumn shares every geometry map with summer in all four variants, and _unlit too unless the species has a fall colour.',
      'Fall colour: ' + R.SPECIES.filter(s => s.fall).map(s => s.name).join(', ') + '. The other five are identical in autumn and cost nothing.',
      'Winter shares nothing, evergreens included: build() adds 31 to the seed in winter, so a winter spruce is a different draw of the same species.');
    return { name: 'seasons', lines: L, failed: bad };
  }

  // ---- budget ----------------------------------------------------------------------------------------
  function budget() {
    const { R, M4 } = G(), L = [], B = 4, GB = (b) => (b / 1e9).toFixed(2) + ' GB (' + (b / 1073741824).toFixed(2) + ' GiB)', MB = (b) => (b / 1e6).toFixed(1) + ' MB';
    let sheetPx = 0, cellPx = 0, fallSheetPx = 0, fallCellPx = 0;
    for (const sp of R.SPECIES) for (const st of R.STAGE_KEYS) {
      const s = R.sheetSpec(sp.key, R.STAGES[st]); sheetPx += s.w * s.h; cellPx += s.cell[0] * s.cell[1];
      if (sp.fall) { fallSheetPx += s.w * s.h; fallCellPx += s.cell[0] * s.cell[1]; }
    }
    const FS = R.SPECIES.filter(s => s.fall).length * R.STAGE_KEYS.length;
    const SS = R.SPECIES.length * R.STAGE_KEYS.length, LV = 4, DIRS = 2, VAR = 4, SEA = 3, MAPS4 = 4;
    const perSet = sheetPx * B;   // one 4 × 4 wind sheet per species × stage, one map, one variant · level · direction · season
    L.push('BAKED FRAMES (the 4.1 contract): a 4 × 4 sheet of 16 frames per species × stage × season × variant × wind level × direction × map',
      '  maps _unlit _normal _light _detail (4; _lit optional, +25%) · levels calm breeze wind gale (4) · directions 2 · RGBA8, uncompressed',
      '  sheet pixels, all 40 species × stages, one map · variant · level · direction · season: ' + sheetPx.toLocaleString('en-US') + ' = ' + MB(perSet),
      '  one variant:   ' + (SS * SEA * LV * DIRS * MAPS4).toLocaleString('en-US') + ' sheets · ' + GB(perSet * SEA * LV * DIRS * MAPS4),
      '  four variants: ' + (SS * SEA * LV * DIRS * MAPS4 * VAR).toLocaleString('en-US') + ' sheets · ' + GB(perSet * SEA * LV * DIRS * MAPS4 * VAR),
      '  per season, four variants: ' + (SS * LV * DIRS * MAPS4 * VAR).toLocaleString('en-US') + ' sheets · ' + GB(perSet * LV * DIRS * MAPS4 * VAR),
      '  autumn shares _normal _light _detail with summer (see seasons), and _unlit too on the five species with no fall colour,',
      '  so it adds _unlit for the other five only: ' + (FS * LV * DIRS * VAR).toLocaleString('en-US') + ' sheets · ' + GB(fallSheetPx * B * LV * DIRS * VAR),
      '  with that sharing: summer ' + GB(perSet * LV * DIRS * MAPS4 * VAR) + ' + autumn ' + GB(fallSheetPx * B * LV * DIRS * VAR) + ' + winter ' + GB(perSet * LV * DIRS * MAPS4 * VAR) + ' = ' + GB(perSet * LV * DIRS * VAR * MAPS4 * 2 + fallSheetPx * B * LV * DIRS * VAR), '');
    const set = cellPx * VAR * B, MAPS = M4.SHEET_MAPS.length;   // one 4 × 1 rest sheet per species × stage, one map, one season
    L.push('REST POSE + WEIGHTS (treeMaps4.js): a 4 × 1 sheet of the four variants at rest per species × stage × season × map',
      '  maps ' + M4.SHEET_MAPS.map(m => '_' + m).join(' ') + ' (' + MAPS + ') · any wind level, direction, gust and frame from the shader · any snow cover from _snow',
      '  sheet pixels, all 40 species × stages, one map · season: ' + (cellPx * VAR).toLocaleString('en-US') + ' = ' + MB(set),
      '  summer: ' + SS * MAPS + ' sheets · ' + MB(set * MAPS),
      '  autumn: +' + FS + ' sheets (_unlit of the five fall-colour species; everything else is summer\'s) · +' + MB(fallCellPx * VAR * B),
      '  winter: +' + SS * MAPS + ' sheets · +' + MB(set * MAPS) + ' (bare broadleaves and larch are new geometry; winter also reseeds the evergreens — build() adds 31 to the seed)',
      '  total: ' + (SS * MAPS * 2 + FS) + ' sheets · ' + MB(set * MAPS * 2 + fallCellPx * VAR * B) + ' · _snow as one channel (R8) saves ' + MB(set * 0.75 * 2) + ' of that',
      '  _lit (optional, per sky you bake): ' + MB(set) + ' per season per sky', '',
      'ratio, four variants, all seasons: ' + Math.round(perSet * LV * DIRS * MAPS4 * VAR * SEA / (set * MAPS * 2 + fallCellPx * VAR * B)) + '× smaller than the naive bake (' +
        Math.round((perSet * LV * DIRS * VAR * MAPS4 * 2 + fallSheetPx * B * LV * DIRS * VAR) / (set * MAPS * 2 + fallCellPx * VAR * B)) + '× against the bake with autumn shared)');
    return { name: 'budget', lines: L, failed: 0 };
  }

  // ---- runner ----------------------------------------------------------------------------------------
  async function run(name, io, keys, part) {
    if (name === 'sums') return sums(io);
    if (name === 'stamps') return stamps(io);
    if (name === 'sidecars') return sidecars(io, keys);
    if (name === 'cells') return cells(io);
    if (name === 'snow') return snow(io, keys, part);
    if (name === 'wind') { const r = wind(io, keys); if (!keys) r.lines = r.lines.concat(windSummary(r.sum)); return r; }
    if (name === 'seasons') return seasons(io, keys);
    if (name === 'budget') return budget();
    throw new Error('TREE_CHECKS: no check named ' + name);
  }
  const format = (r) => '# ' + r.name + ' — ' + (r.failed ? r.failed + ' FAILED' : 'passed') + '\n\n' + r.lines.join('\n') + '\n';

  root.TREE_CHECKS = { ORDER, run, format, cellRows, windSummary, COVERS, CONDS, WFRAMES, GUST, snowCompare };
})(typeof globalThis !== 'undefined' ? globalThis : window);
