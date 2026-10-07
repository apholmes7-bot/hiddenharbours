namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>HHTreePass4</b>, the pass-4 tree bake glue: host-side code in the sense of ADR 0021 §5.
    /// <c>treeIsoRig4.js</c> and <c>treeMaps4.js</c> run UNMODIFIED; this reads them and packs what
    /// the game draws. <see cref="TreePass4Baker"/> installs the rig, then the maps, then this.
    ///
    /// <para>Per species, stage and season list it renders the four variants at REST and returns
    /// six channels per cell: the albedo (<c>relight</c> under <c>REF_SKY</c>), pass 3's mask in
    /// pass 3's order by rig 3's own formulas, the rig's view-space normal, the kit's two wind maps
    /// with a 24-bit word <c>W</c> packed into <c>wind.B</c>, <c>wind.A</c> and <c>phase.A</c>, and
    /// the snow threshold. <c>W</c> is <c>class 2 · gapSnow 3 · gap 3 · snow 3 · id + 1 13</c>, high
    /// to low; <c>TreeWindMath</c> and <c>TreeWindMaps.hlsl</c> decode it.</para>
    ///
    /// <para><b>It refuses rather than guesses.</b> <c>bake()</c> takes the contract's snow row and
    /// gap rows and throws on any colour the rows lack, on a gap row that drifted, on an autumn
    /// that no longer matches summer's maps pixel for pixel, and on a stamp id <c>W</c> cannot
    /// hold. The baker writes nothing when it throws. Proven on all ten species × three seasons in
    /// the repo's V8 harness at intake (2026-09-24): every class, id and snow byte round-trips and
    /// the packed colours equal the direct ones.</para>
    ///
    /// <para>The source is below verbatim, so a diff of this file IS the glue's diff. It carries no
    /// double quotes.</para>
    /// </summary>
    public static class TreePass4Glue
    {
        /// <summary>The glue's JS. Run it after the rig and the maps; it installs
        /// <c>globalThis.HHTreePass4</c> once and is a no-op the second time.</summary>
        public const string Js = @"
/* HHTreePass4 — the pass-4 bake glue. Host-side code (ADR 0021 §5): treeIsoRig4.js and treeMaps4.js run
   UNMODIFIED, this reads them. Load treeIsoRig4.js, then treeMaps4.js, then this.

   For one species, stage and season list it renders the four variants at REST (TreeMaps4.rest) and returns
   six channels per cell, all in the rig's row order (top row first):
     albedo  TreeRig4.relight(rest, REF_SKY): the sprite as the rig draws it in still air
     mask    pass 3's mask in pass 3's order, R key · G back rim · B depth · A coverage, by rig 3's own
             formulas (LIGHT.key, LIGHT.rim, the 13 × 13 thickness gate) applied to the rest frame
     normal  TreeRig4.view(rest, 'normal')
     wind    TreeMaps4 wind: R lean · G sway as the kit writes them; B and A carry the word W below
     phase   TreeMaps4 phase: R wave · G play · B depth as the kit writes them; A carries W's low byte
     snow    TreeMaps4.snowMap: the cover × 254 at which the pixel turns to snow, 255 never, 0 outside
   W, 24 bits = wind.B << 16 | wind.A << 8 | phase.A, high to low:
     class 2 · gapSnow 3 · gap 3 · snow 3 · id + 1 13
     class    0 outside · 1 wood · 2 the dark between leaves · 3 a leaf. It replaces the kit's wind.B leaf
              flag (255 / 0): read W, never wind.B > 127.
     snow     the pixel snowed: an index into the palette's snow row
     gap      a leaf pixel its leaf has moved off: the dark between leaves there, an index into the gap row
     gapSnow  the same pixel snowed (snow row). The rig snows the dark between leaves only above 60 % cover,
              so a gap snows at max(its leaf's snow byte, 153)
     id + 1   the leaf's stamp id + 1: the flutter hash and the gather's tie-break key on it. 0 = not a leaf
   The palette is 8 wide: a snow row shared by every species (the SNOW ramp's colours), then a gap row per
   species × season. Rows are sorted by packed RGB and padded by repeating the last colour; autumn keeps
   summer's gap order, because autumn shares summer's maps (the glue checks that, pixel by pixel).
   Colours are the rig's: snowed = relight under FORCED (every pixel with sky snowed; the ground bounce held at
   the rig's full-cover value), gap = relight of the rest frame with every stamp id cleared.

     survey(key, stage, seasons)                    JSON: what the contract records (gap rows, snow colours)
     bake(key, stage, seasons, snowRow, gapRows)    JSON: the sheets to write. Refuses a colour the rows lack
     cell(season, channel, variant)                 Uint8ClampedArray, cellW × cellH × 4, of the last bake
     release()                                      drops the held cells and the rig's model cache         */
(function (root) {
  'use strict';
  if (root.HHTreePass4) return;
  const R = root.TreeRig4, M4 = root.TreeMaps4;
  if (!R || !M4) throw new Error('HHTreePass4: load treeIsoRig4.js and then treeMaps4.js first');
  const FOL = R.M.FOLIAGE, NEVER = M4.SNOW_NEVER, GAP_SNOW_MIN = 153, PAL = 8, ID_MAX = 8191, VARIANTS = 4;
  const OUTSIDE = 0, WOOD = 1, BETWEEN = 2, LEAF = 3;
  const CHANNELS = ['albedo', 'mask', 'normal', 'wind', 'phase', 'snow'];
  const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
  const smooth = (e0, e1, x) => { const t = clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); };
  const nrm = (v) => { const L = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / L, v[1] / L, v[2] / L]; };
  // rig 3's LIGHT, verbatim: the mask's R shades from the key, its G is the back rim
  const KEY = nrm([-0.55, -0.66, 0.52]), RIM = nrm([0.48, -0.28, -0.83]);
  const RSl = Math.hypot(RIM[0], RIM[1]) || 1, RS = [RIM[0] / RSl, RIM[1] / RSl];
  // FORCED: snow 5 clears every threshold where ao > 0.2; sunW.z lowered so the ground bounce is the rig's at cover 1
  const sw = R.REF_SKY.sunW, skyI = R.REF_SKY.skyI, sunI = R.REF_SKY.sunI;
  const SF = 5, tgt = (sunI * Math.max(0, sw[2]) + 0.35 * skyI) * 1.8, zF = (tgt / (1 + 0.8 * SF) - 0.35 * skyI) / sunI;
  const FORCED = Object.assign({}, R.REF_SKY, { snow: SF, sunW: [sw[0], sw[1], zF] });
  const gapFrame = (rf) => Object.assign({}, rf, { v: Object.assign({}, rf.v, { st: new Int32Array(rf.v.st.length).fill(-1) }) });
  const rgb = (c, o) => (c[o] << 16) | (c[o + 1] << 8) | c[o + 2];
  const hex6 = (n) => ('00000' + n.toString(16)).slice(-6);
  const fail = (msg) => { throw new Error('HHTreePass4: ' + msg); };
  const byNum = (a, b) => a - b;
  const unhex = (s, what) => { if (typeof s !== 'string' || !/^[0-9a-f]{6}$/.test(s)) fail(what + ': not a colour: ' + s); return parseInt(s, 16); };

  // rig 3's local mass thickness, verbatim: the largest distance-to-edge in a 13 × 13 box, rows then columns
  function thickness(D, w, h) {
    const n = w * h, TH = new Float32Array(n), tmp = new Float32Array(n), RAD = 6;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      let m = 0; for (let k = -RAD; k <= RAD; k++) { const jx = x + k; if (jx < 0 || jx >= w) continue; const d = D[y * w + jx]; if (d > m) m = d; }
      tmp[y * w + x] = m;
    }
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      let m = 0; for (let k = -RAD; k <= RAD; k++) { const jy = y + k; if (jy < 0 || jy >= h) continue; const d = tmp[jy * w + x]; if (d > m) m = d; }
      TH[y * w + x] = m;
    }
    return TH;
  }

  // one variant at rest: every array the survey and the bake read
  function renderVariant(key, stage, season, variant) {
    const rf = M4.rest(key, { stage: stage, season: season, variant: variant }), v = rf.v, w = v.w, h = v.h, N = w * h;
    const alb = R.relight(rf, R.REF_SKY), sb = M4.snowMap(rf), fs = R.relight(rf, FORCED);
    const gf = gapFrame(rf), gap = R.relight(gf, R.REF_SKY), gfs = R.relight(gf, FORCED);
    const wm = M4.windMaps(rf), kwind = wm.wind.slice(), kphase = wm.phase.slice(), normal = R.view(rf, 'normal');
    const mask = new Uint8ClampedArray(N * 4), snow = new Uint8ClampedArray(N * 4), cls = new Uint8Array(N), id1 = new Int32Array(N);
    let zmin = 1e9, zmax = -1e9;
    for (let i = 0; i < N; i++) if (v.a[i]) { if (v.z[i] < zmin) zmin = v.z[i]; if (v.z[i] > zmax) zmax = v.z[i]; }
    const zr = Math.max(1, zmax - zmin), D = rf.D, TH = thickness(D, w, h);
    const r = { w, h, N, a: v.a, alb, fs, gap, gfs, sb, kwind, kphase, normal, mask, snow, cls, id1, clipped: wm.clipped,
      px: 0, nLeaf: 0, nBetween: 0, nWood: 0, idMax: 0, leafNoPart: 0 };
    for (let i = 0; i < N; i++) {
      if (!v.a[i]) continue;
      r.px++;
      const o = i * 4, nx = v.nx[i], ny = v.ny[i], nz = v.nz[i], d = D[i];
      const lam = Math.pow(Math.max(0, nx * KEY[0] + ny * KEY[1] + nz * KEY[2]), 1.35);
      const nl = Math.hypot(nx, ny) || 1, back = Math.max(0, (nx * RS[0] + ny * RS[1]) / nl);
      const fres = Math.pow(1 - clamp(nz, 0, 1), 1.7), thick = smooth(4.0, 5.2, TH[i]);
      const rim = Math.pow(back, 1.15) * fres * thick * smooth(3.6, 0.8, d);
      mask[o] = clamp(Math.round(lam * 255), 0, 255);
      mask[o + 1] = clamp(Math.round(rim * 255), 0, 255);
      mask[o + 2] = clamp(Math.round(((v.z[i] - zmin) / zr) * 255), 0, 255);
      mask[o + 3] = 255;
      snow[o] = snow[o + 1] = snow[o + 2] = sb[i]; snow[o + 3] = 255;
      if (v.mat[i] !== FOL) { cls[i] = WOOD; r.nWood++; }
      else if (kwind[o + 2] === 255) { cls[i] = LEAF; r.nLeaf++; id1[i] = v.st[i] + 1; if (id1[i] > r.idMax) r.idMax = id1[i]; }
      else { cls[i] = BETWEEN; r.nBetween++; if (v.st[i] >= 0) r.leafNoPart++; }
    }
    return r;
  }

  function seasonList(seasons) {
    const list = seasons == null ? ['summer'] : Array.from(seasons);
    if (!list.length) fail('no seasons');
    list.forEach((s) => { if (R.SEASONS.indexOf(s) < 0) fail('unknown season ' + s); });
    if (new Set(list).size !== list.length) fail('a season twice: ' + list.join(','));
    if (list.indexOf('autumn') >= 0 && list.indexOf('summer') < 0) fail('autumn draws on summer maps: bake summer with it');
    return R.SEASONS.filter((s) => list.indexOf(s) >= 0);
  }
  const padRow = (sorted) => { const row = []; for (let j = 0; j < PAL; j++) row.push(hex6(sorted.length ? sorted[Math.min(j, sorted.length - 1)] : 0)); return row; };
  function rowIndex(row, what) {
    if (!row || row.length !== PAL) fail(what + ': a row is ' + PAL + ' colours');
    const ix = new Map();
    row.forEach((s, j) => { const c = unhex(s, what); if (!ix.has(c)) ix.set(c, j); });
    return ix;
  }
  const find = (ix, c, what) => { const j = ix.get(c); if (j === undefined) fail(what + ' colour ' + hex6(c) + ' is not in the contract: export the contract again'); return j; };
  const same = (x, y) => { if (x.length !== y.length) return false; for (let i = 0; i < x.length; i++) if (x[i] !== y[i]) return false; return true; };

  function pack(r, snowIx, what) {
    const N = r.N, wind = r.kwind.slice(), phase = r.kphase.slice();
    for (let i = 0; i < N; i++) {
      const o = i * 4;
      if (!r.a[i]) { wind[o + 2] = 0; wind[o + 3] = 0; phase[o + 3] = 0; continue; }
      const c = r.cls[i], snowable = r.sb[i] !== NEVER;
      let si = 0, gi = 0, gsi = 0, id = 0;
      if (snowable) si = find(snowIx, rgb(r.fs, o), what + ' snow');
      if (c === LEAF) { gi = r.gapIdx[i]; id = r.id1[i]; if (snowable) gsi = find(snowIx, rgb(r.gfs, o), what + ' gap snow'); }
      const W = (c << 22) | (gsi << 19) | (gi << 16) | (si << 13) | id;
      wind[o + 2] = (W >>> 16) & 255; wind[o + 3] = (W >>> 8) & 255; phase[o + 3] = W & 255;
    }
    r.wind = wind; r.phase = phase;
  }

  // autumn shares summer's maps: everything but the colours must match, pixel for pixel
  function autumnMatches(a, s) {
    if (a.w !== s.w || a.h !== s.h) return 'the cell size';
    if (!same(a.a, s.a)) return 'the silhouette';
    if (!same(a.cls, s.cls) || !same(a.id1, s.id1)) return 'the leaves';
    if (!same(a.sb, s.sb)) return 'the snow map';
    if (!same(a.mask, s.mask) || !same(a.normal, s.normal)) return 'the mask or the normals';
    if (!same(a.kwind, s.kwind) || !same(a.kphase, s.kphase)) return 'the wind maps';
    for (let i = 0; i < a.N; i++) {
      if (!a.a[i] || a.sb[i] === NEVER) continue;
      const o = i * 4;
      if (rgb(a.fs, o) !== rgb(s.fs, o)) return 'a snowed colour';
      if (a.cls[i] === LEAF && rgb(a.gfs, o) !== rgb(s.gfs, o)) return 'a snowed gap colour';
    }
    return null;
  }

  let held = null;
  function run(key, stage, seasons, rows) {
    if (!R.byKey[key]) fail('unknown species ' + key);
    if (!Object.prototype.hasOwnProperty.call(R.STAGES, stage)) fail('unknown stage ' + stage);
    const list = seasonList(seasons), baking = rows != null, t0 = Date.now();
    if (baking && (!rows.gap || typeof rows.gap !== 'object')) fail(key + ': no gap rows');
    const snowIx = baking ? rowIndex(rows.snow, 'the snow row') : null;
    const res = { key, stage, seasons: {}, snow: [], idMax: 0, leafNoPart: 0, cell: null, sheets: [], mapsSeason: {}, albedoSeason: {}, ms: 0 };
    const snowSet = new Set(), cells = new Map();
    let summer = null;
    for (const season of list) {
      const t1 = Date.now(), vars = [];
      for (let variant = 0; variant < VARIANTS; variant++) { vars.push(renderVariant(key, stage, season, variant)); R.clearCache(); }
      const S = { px: 0, leaf: 0, between: 0, wood: 0, snowable: 0, clipped: 0, gapColours: 0, gapRow: null, maps: season, albedo: season, ms: 0 };
      const gapSet = new Set();
      for (const r of vars) {
        if (r.w !== vars[0].w || r.h !== vars[0].h) fail(key + ' ' + season + ': the variants differ in size');
        S.px += r.px; S.leaf += r.nLeaf; S.between += r.nBetween; S.wood += r.nWood; S.clipped += r.clipped;
        res.idMax = Math.max(res.idMax, r.idMax); res.leafNoPart += r.leafNoPart;
        for (let i = 0; i < r.N; i++) {
          if (!r.a[i]) continue;
          const o = i * 4, snowable = r.sb[i] !== NEVER;
          if (snowable) { S.snowable++; snowSet.add(rgb(r.fs, o)); }
          if (r.cls[i] === LEAF) { gapSet.add(rgb(r.gap, o)); if (snowable) snowSet.add(rgb(r.gfs, o)); }
        }
      }
      if (res.cell && (res.cell[0] !== vars[0].w || res.cell[1] !== vars[0].h)) fail(key + ': the seasons differ in cell size');
      res.cell = [vars[0].w, vars[0].h];
      if (res.leafNoPart > 0) fail(key + ' ' + season + ': ' + res.leafNoPart + ' leaf pixels belong to no part');
      if (res.idMax > ID_MAX) fail(key + ' ' + season + ': stamp id + 1 reaches ' + res.idMax + ', W holds ' + ID_MAX);
      let row, sameAlbedo = false;
      if (season === 'autumn') {
        const map = new Array(PAL).fill(-1);
        sameAlbedo = true;
        vars.forEach((r, variant) => {
          const s = summer.vars[variant], why = autumnMatches(r, s);
          if (why) fail(key + ' autumn variant ' + (variant + 1) + ': ' + why + ' differs from summer; autumn cannot share summer maps');
          if (!same(r.alb, s.alb)) sameAlbedo = false;
          for (let i = 0; i < r.N; i++) {
            if (!r.a[i] || r.cls[i] !== LEAF) continue;
            const j = s.gapIdx[i], c = rgb(r.gap, i * 4);
            if (map[j] < 0) map[j] = c;
            else if (map[j] !== c) fail(key + ' autumn: summer gap ' + j + ' turns to two autumn colours, ' + hex6(map[j]) + ' and ' + hex6(c));
          }
        });
        const n = summer.gapColours;
        row = [];
        for (let j = 0; j < PAL; j++) {
          const c = n ? map[Math.min(j, n - 1)] : 0;
          if (c < 0) fail(key + ' autumn: summer gap ' + j + ' has no autumn pixel');
          row.push(hex6(c));
        }
        S.gapColours = new Set(row.slice(0, Math.max(1, n))).size;
        S.maps = 'summer'; S.albedo = sameAlbedo ? 'summer' : 'autumn';
      } else {
        const sorted = Array.from(gapSet).sort(byNum);
        if (sorted.length > PAL) fail(key + ' ' + season + ': ' + sorted.length + ' gap colours; a palette row holds ' + PAL);
        row = padRow(sorted); S.gapColours = sorted.length;
        const gapIx = rowIndex(row, key + ' ' + season);
        for (const r of vars) {
          r.gapIdx = new Int8Array(r.N).fill(-1);
          for (let i = 0; i < r.N; i++) if (r.a[i] && r.cls[i] === LEAF) r.gapIdx[i] = gapIx.get(rgb(r.gap, i * 4));
        }
      }
      if (baking) {
        const want = rows.gap[season];
        if (!want || want.length !== PAL) fail(key + ': the contract has no ' + season + ' gap row');
        for (let j = 0; j < PAL; j++) if (want[j] !== row[j]) fail(key + ' ' + season + ': gap row drift at ' + j + ', contract ' + want[j] + ', rig ' + row[j] + ': export the contract again');
        if (season === 'autumn') {
          if (!sameAlbedo) { res.sheets.push({ season, channel: 'albedo' }); vars.forEach((r, variant) => cells.set(season + '/albedo/' + variant, r.alb)); }
        } else {
          vars.forEach((r, variant) => {
            pack(r, snowIx, key + ' ' + season + ' variant ' + (variant + 1));
            const ch = { albedo: r.alb, mask: r.mask, normal: r.normal, wind: r.wind, phase: r.phase, snow: r.snow };
            for (const c of CHANNELS) cells.set(season + '/' + c + '/' + variant, ch[c]);
          });
          for (const c of CHANNELS) res.sheets.push({ season, channel: c });
        }
      }
      res.mapsSeason[season] = S.maps; res.albedoSeason[season] = S.albedo;
      S.gapRow = row; S.ms = Date.now() - t1;
      res.seasons[season] = S;
      if (season === 'summer') summer = { vars, gapColours: S.gapColours };
      else if (season === 'autumn') summer = null;
    }
    res.snow = Array.from(snowSet).sort(byNum).map(hex6);
    res.constants = constants(key, stage);
    res.ms = Date.now() - t0;
    return { res, cells };
  }

  function constants(key, stage) {
    const c = M4.constants(key, stage);
    return { H: c.H, bendPx: c.bendPx, limbPx: c.limbPx, bob: c.bob, flutter: c.flutter, shimmer: c.shimmer ? c.shimmer.slice() : null, conifer: c.conifer, loop: c.loop };
  }

  function survey(key, stage, seasons) {
    release();
    const out = run(key, stage, seasons, null).res;
    R.clearCache();
    return JSON.stringify(out);
  }
  function bake(key, stage, seasons, snowRow, gapRows) {
    release();
    const snow = Array.from(snowRow), gap = {};
    for (const s in gapRows) gap[s] = Array.from(gapRows[s]);
    const got = run(key, stage, seasons, { snow, gap });
    held = { key, stage, cellW: got.res.cell[0], cellH: got.res.cell[1], cells: got.cells };
    R.clearCache();
    return JSON.stringify(got.res);
  }
  function cell(season, channel, variant) {
    if (!held) fail('nothing baked: call bake() first');
    const c = held.cells.get(season + '/' + channel + '/' + (variant | 0));
    if (!c) fail(held.key + ': no ' + season + ' ' + channel + ' cell for variant ' + ((variant | 0) + 1));
    return c;
  }
  function release() { held = null; R.clearCache(); }

  root.HHTreePass4 = { VERSION: 1, PAL, ID_MAX, GAP_SNOW_MIN, CHANNELS, OUTSIDE, WOOD, BETWEEN, LEAF, FORCED, KEY, RIM, RS,
    survey, bake, cell, release, renderVariant, thickness };
})(typeof globalThis !== 'undefined' ? globalThis : this);
";

        /// <summary><c>HHTreePass4.VERSION</c>: the contract records it, and a contract written by
        /// another glue version is re-exported, not baked against.</summary>
        public const int Version = 1;
    }
}
