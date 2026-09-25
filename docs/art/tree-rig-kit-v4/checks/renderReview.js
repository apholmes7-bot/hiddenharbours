/* Hidden Harbours — TREE KIT REVIEW RENDERS, pass 4.1.  globalThis.TREE_REVIEW
   Every image in renders/ is made here, from the kit's own files, on the pixel terrain the page uses.
   No canvas: images are RGBA buffers with a 3 × 5 pixel font for the labels, so Node can make them too.
     node checks/render.js            (Node 18+)  writes renders/*.png
     TREE_REVIEW.NAMES                 the renders, in order
     TREE_REVIEW.render(name) → {w, h, rgba}
   Needs: treeIsoRig4.js, weatherSky.js, treeMaps4.js, lib/treeIsoRig3.js, lib/pixelLanguage.js,
   lib/pxKit.js, lib/harmonyScenes.js.                                                                  */
(function (root) {
  'use strict';
  const R = () => root.TreeRig4, WS = () => root.WeatherSky, M4 = () => root.TreeMaps4;
  const FAMILY = ['RedOak', 'RedMaple', 'WhiteBirch', 'TremblingAspen', 'WhitePine', 'RedSpruce', 'BalsamFir', 'BlackSpruce', 'WhiteCedar', 'Tamarack'];
  const BG = [11, 20, 24], INK = [231, 236, 231], DIM = [120, 140, 136], GOLD = [201, 168, 106];
  const h2r = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];

  // ---- a 3 × 5 font --------------------------------------------------------------------------------
  const GLYPH = {
    A: '.#./#.#/###/#.#/#.#', B: '##./#.#/##./#.#/##.', C: '.##/#../#../#../.##', D: '##./#.#/#.#/#.#/##.', E: '###/#../##./#../###', F: '###/#../##./#../#..', G: '.##/#../#.#/#.#/.##',
    H: '#.#/#.#/###/#.#/#.#', I: '###/.#./.#./.#./###', J: '..#/..#/..#/#.#/.#.', K: '#.#/#.#/##./#.#/#.#', L: '#../#../#../#../###', M: '#.#/###/###/#.#/#.#', N: '##./#.#/#.#/#.#/#.#',
    O: '.#./#.#/#.#/#.#/.#.', P: '##./#.#/##./#../#..', Q: '.#./#.#/#.#/##./.##', R: '##./#.#/##./#.#/#.#', S: '.##/#../.#./..#/##.', T: '###/.#./.#./.#./.#.', U: '#.#/#.#/#.#/#.#/###',
    V: '#.#/#.#/#.#/#.#/.#.', W: '#.#/#.#/###/###/#.#', X: '#.#/#.#/.#./#.#/#.#', Y: '#.#/#.#/.#./.#./.#.', Z: '###/..#/.#./#../###',
    0: '###/#.#/#.#/#.#/###', 1: '.#./##./.#./.#./###', 2: '##./..#/.#./#../###', 3: '##./..#/.#./..#/##.', 4: '#.#/#.#/###/..#/..#', 5: '###/#../##./..#/##.',
    6: '.##/#../###/#.#/###', 7: '###/..#/.#./.#./.#.', 8: '###/#.#/###/#.#/###', 9: '###/#.#/###/..#/##.',
    '.': '.../.../.../.../.#.', '·': '.../.../.#./.../...', '%': '#.#/..#/.#./#../#.#', '/': '..#/..#/.#./#../#..', '-': '.../.../###/.../...', '>': '#../.#./..#/.#./#..',
    ':': '.../.#./.../.#./...', ',': '.../.../.../.#./#..', '×': '.../#.#/.#./#.#/...', '(': '.#./#../#../#../.#.', ')': '.#./..#/..#/..#/.#.', '+': '.../.#./###/.#./...', '=': '.../###/.../###/...', '^': '.#./#.#/.../.../...', '_': '.../.../.../.../###', ' ': '.../.../.../.../...',
  };
  for (const k in GLYPH) GLYPH[k] = GLYPH[k].replace(/\//g, '');
  function text(img, x, y, s, col, sc) {
    sc = sc || 2; let cx = x;
    for (const ch of String(s).toUpperCase()) {
      const g = GLYPH[ch] || GLYPH[' '];
      for (let r = 0; r < 5; r++) for (let c = 0; c < 3; c++) if (g[r * 3 + c] === '#') rect(img, cx + c * sc, y + r * sc, sc, sc, col);
      cx += 4 * sc;
    }
    return cx;
  }
  function blank(w, h, col) { const a = new Uint8ClampedArray(w * h * 4); for (let i = 0; i < w * h; i++) { a[i * 4] = col[0]; a[i * 4 + 1] = col[1]; a[i * 4 + 2] = col[2]; a[i * 4 + 3] = 255; } return { w, h, rgba: a }; }
  function rect(img, x, y, w, h, col) { for (let yy = Math.max(0, y); yy < Math.min(img.h, y + h); yy++) for (let xx = Math.max(0, x); xx < Math.min(img.w, x + w); xx++) { const o = (yy * img.w + xx) * 4; img.rgba[o] = col[0]; img.rgba[o + 1] = col[1]; img.rgba[o + 2] = col[2]; img.rgba[o + 3] = 255; } }
  function paste(dst, src, x, y) { for (let yy = 0; yy < src.h; yy++) { const Y = y + yy; if (Y < 0 || Y >= dst.h) continue; for (let xx = 0; xx < src.w; xx++) { const X = x + xx; if (X < 0 || X >= dst.w) continue; const s = (yy * src.w + xx) * 4, d = (Y * dst.w + X) * 4; dst.rgba[d] = src.rgba[s]; dst.rgba[d + 1] = src.rgba[s + 1]; dst.rgba[d + 2] = src.rgba[s + 2]; dst.rgba[d + 3] = 255; } } }
  // panels stacked, each under a label strip
  function stack(panels, title, note) {
    const W = Math.max(...panels.map(p => p.img.w)), LH = 26, TH = title ? 44 : 0, H = TH + panels.reduce((s, p) => s + p.img.h + LH, 0);
    const out = blank(W, H, BG);
    if (title) { text(out, 12, 10, title, INK, 3); if (note) text(out, 12, 30, note, DIM, 2); }
    let y = TH;
    for (const p of panels) { text(out, 12, y + 8, p.label, GOLD, 2); y += LH; paste(out, p.img, 0, y); y += p.img.h; }
    return out;
  }

  // ---- the floor: the page's pixel terrain, relit by the sky ------------------------------------------
  let TILE = null, NR = null;
  function tile() {
    if (TILE) return TILE;
    const P = root.PxLang, K = root.PxKit, H = root.Harmony, FW = 520, FH = 520;
    if (H && H.floor) {
      const mid = (x) => FH * 0.87 + 12 * Math.sin(x / FW * 2 * Math.PI) + 5 * Math.sin(x / FW * 4 * Math.PI + 1.3);
      const onPath = (x, y) => Math.abs(y - mid(x)) < 10 + 3 * Math.sin(x / FW * 6 * Math.PI);
      TILE = H.floor(null, FW, FH, [{ key: 'grass', step: 1, R: (x, y) => !onPath(x, y), seed: 3072 }, { key: 'path', step: 1, R: onPath, seed: 4410 }], 55).tile;
    } else TILE = K.build('grass', 1).tile;
    NR = P.normals(TILE, 1.2);
    return TILE;
  }
  function floor(sky, W, H, flat) {
    if (flat) return [0, 1, 2, 3].map(() => blank(W, H, flat).rgba);
    const t = tile();
    return [0, 1, 2, 3].map(l => WS().lightTile(t, NR, sky, { x0: 0, y0: 0, w: W, h: H, level: l }));
  }
  function blit(img, lv, it, tx, ty) {
    const piv = it.pv;
    if (it.sh) {
      const sh = it.sh, sox = tx - piv.x, soy = ty - piv.y;
      for (let y = 0; y < sh.h; y++) { const Y = soy + sh.y0 + y; if (Y < 0 || Y >= img.h) continue;
        for (let x = 0; x < sh.w; x++) { const l = sh.lv[y * sh.w + x]; if (!l) continue; const X = sox + sh.x0 + x; if (X < 0 || X >= img.w) continue;
          const o = (Y * img.w + X) * 4, L = lv[l]; img.rgba[o] = L[o]; img.rgba[o + 1] = L[o + 1]; img.rgba[o + 2] = L[o + 2]; } }
    }
  }
  function sprite(img, it, tx, ty) {
    const px = it.px, ox = tx - it.pv.x, oy = ty - it.pv.y;
    for (let y = 0; y < it.ph; y++) { const Y = oy + y; if (Y < 0 || Y >= img.h) continue;
      for (let x = 0; x < it.pw; x++) { const i = (y * it.pw + x) * 4; if (!px[i + 3]) continue; const X = ox + x; if (X < 0 || X >= img.w) continue;
        const o = (Y * img.w + X) * 4; img.rgba[o] = px[i]; img.rgba[o + 1] = px[i + 1]; img.rgba[o + 2] = px[i + 2]; } }
  }
  // the family on one strip of floor, spaced by crown as the page's lineup is
  function lineup(get, sky, flat, keys) {
    keys = keys || FAMILY;
    const xs = []; let x = 40;
    for (const k of keys) { const w = Math.max(70, R().byKey[k].crown * R().M2PX); xs.push(Math.round(x + w / 2)); x += w + 26; }
    const W = Math.round(x + 80), H = 480, base = 452, lv = floor(sky, W, H, flat), img = { w: W, h: H, rgba: new Uint8ClampedArray(lv[0]) };
    const items = keys.map((k, i) => Object.assign(get(k), { x: xs[i] }));
    for (const it of items) blit(img, lv, it, it.x, base);
    for (const it of items) sprite(img, it, it.x, base);
    return img;
  }
  const LIT = (fr, sky) => ({ px: R().relight(fr, sky), pw: fr.v.w, ph: fr.v.h, pv: fr.mdl.pivot, sh: R().castShadow(fr, sky) });
  const MAT = { stage: 'mature', variant: 0 };
  const SKY = () => WS().at({ time: 14, cloud: 0, rain: 0, fog: 0, snow: 0, wind: 0 });
  const SNOWSKY = (s) => WS().at({ time: 10, cloud: 0.1, rain: 0, fog: 0, snow: s, wind: 0.08 });

  // colour keys for the maps
  function snowColour(fr) {
    const sm = M4().snowMap(fr), v = fr.v, px = new Uint8ClampedArray(v.w * v.h * 4), A = h2r('#f2f7f7'), B = h2r('#24406e'), NV = h2r('#3a3f44');
    for (let i = 0; i < v.w * v.h; i++) { if (!v.a[i]) continue; const b = sm[i], c = b === 255 ? NV : A.map((a, k) => a + (B[k] - a) * (b - 1) / 253); px[i * 4] = c[0]; px[i * 4 + 1] = c[1]; px[i * 4 + 2] = c[2]; px[i * 4 + 3] = 255; }
    return { px, pw: v.w, ph: v.h, pv: fr.mdl.pivot, sh: null };
  }
  function mapView(fr, which, chan) {
    const m = M4().windMaps(fr)[which], v = fr.v, px = new Uint8ClampedArray(v.w * v.h * 4);
    for (let i = 0; i < v.w * v.h; i++) { if (!v.a[i]) continue; for (let k = 0; k < 3; k++) px[i * 4 + k] = chan == null ? m[i * 4 + k] : m[i * 4 + chan]; px[i * 4 + 3] = 255; }
    return { px, pw: v.w, ph: v.h, pv: fr.mdl.pivot, sh: null };
  }
  function legend(w, stops, labels) {   // a horizontal ramp with end labels
    const img = blank(w, 26, BG), x0 = 12, x1 = Math.min(w - 12, 320);
    for (let x = x0; x < x1; x++) { const t = (x - x0) / (x1 - x0), s = Math.min(stops.length - 2, Math.floor(t * (stops.length - 1))), f = t * (stops.length - 1) - s, a = h2r(stops[s]), b = h2r(stops[s + 1]); rect(img, x, 4, 1, 10, a.map((v, k) => v + (b[k] - v) * f)); }
    let tx = x1 + 12; for (const l of labels) tx = text(img, tx, 4, l, DIM, 2) + 16;
    return img;
  }

  // ---- the renders ----------------------------------------------------------------------------------
  const WIND = { calm: 0, breeze: 0.3, gale: 1 }, GUST = 0.5, WF = 4;
  const SPECS = {
    'family-seasons': () => {
      const sky = SKY();
      return stack(['summer', 'autumn', 'winter'].map(season => ({ label: season + ' · mature · variant 1 · rest pose · 14:00 clear', img: lineup(k => LIT(M4().rest(k, Object.assign({ season }, MAT)), sky), sky) })),
        'The family, three seasons', 'autumn = summer geometry (only the five fall-colour species change colour) · winter = a new build');
    },
    'snow-000': () => snowRender(0), 'snow-050': () => snowRender(0.5), 'snow-100': () => snowRender(1),
    'snow-map': () => {
      const panels = ['summer', 'winter'].map(season => ({ label: '_snow · ' + season + ' · mature · the cover at which each pixel turns to snow', img: lineup(k => snowColour(M4().rest(k, Object.assign({ season }, MAT))), null, [16, 24, 27]) }));
      panels.push({ label: 'key', img: legend(panels[0].img.w, ['#f2f7f7', '#24406e'], ['BYTE 1 = SNOWS FIRST', 'BYTE 254 = ONLY AT 100%', 'GREY = NEVER (SHELTERED)']) });
      return stack(panels, 'One snow map', 'snowed = round(cover × 254) >= byte · exact against relight() at every cover k/254 (checks/out/snow.txt)');
    },
    'wind-weights': () => {
      const panels = [];
      panels.push({ label: '_wind · R lean · G sway · B flutter', img: lineup(k => mapView(M4().rest(k, Object.assign({ season: 'summer' }, MAT)), 'wind'), null, [16, 24, 27]) });
      panels.push({ label: '_wind R · lean: (height / H)^1.8', img: lineup(k => mapView(M4().rest(k, Object.assign({ season: 'summer' }, MAT)), 'wind', 0), null, [16, 24, 27]) });
      panels.push({ label: '_wind G · sway: reach from the stem', img: lineup(k => mapView(M4().rest(k, Object.assign({ season: 'summer' }, MAT)), 'wind', 1), null, [16, 24, 27]) });
      panels.push({ label: '_wind B · flutter: every leaf (the dark between leaves and the wood only sway)', img: lineup(k => mapView(M4().rest(k, Object.assign({ season: 'summer' }, MAT)), 'wind', 2), null, [16, 24, 27]) });
      panels.push({ label: '_phase · R wave position · G mass play · B depth', img: lineup(k => mapView(M4().rest(k, Object.assign({ season: 'summer' }, MAT)), 'phase'), null, [16, 24, 27]) });
      return stack(panels, 'Wind weights on the rest pose', 'mature · summer · variant 1 · stamp-flat on leaves · shown inside the silhouette (the files are filled outward)');
    },
    'pass3-vs-4.1': () => {
      const sky = SKY(), R3 = root.TreeRig3;
      const p3 = lineup(k => { const r = R3.render(k, { stage: 'mature', season: 'summer', variant: 0 }); return { px: r.rgba, pw: r.w, ph: r.h, pv: r.pivot, sh: null }; }, sky);
      const p4 = lineup(k => LIT(M4().rest(k, Object.assign({ season: 'summer' }, MAT)), sky), sky);
      return stack([{ label: 'pass 3 · light baked at the authored key · no cast shadow', img: p3 }, { label: 'pass 4.1 · relit by the 14:00 sky · cast shadow', img: p4 }], 'Pass 3 against pass 4.1', 'mature · summer · variant 1 · both on their own pivot at the same baseline');
    },
  };
  function snowRender(s) {
    const sky = SNOWSKY(s);
    return stack(['winter', 'summer'].map(season => ({ label: season + ' · snow cover ' + Math.round(s * 100) + '% · 10:00 after snowfall', img: lineup(k => LIT(M4().rest(k, Object.assign({ season }, MAT)), sky), sky) })),
      'Snow cover ' + Math.round(s * 100) + '%', 'trees: relight() at cover ' + Math.round(s * 254) + '/254 = the _snow map thresholded there · floor: WeatherSky.lightTile under the same sky');
  }
  for (const lvl of ['calm', 'breeze', 'gale']) for (const dir of [1, -1]) {
    SPECS['wind-' + lvl + '-' + (dir > 0 ? 'west' : 'east')] = () => {
      const sky = SKY(), W = { w: WIND[lvl], gust: GUST, dir }, o = Object.assign({ season: 'summer' }, MAT);
      const tag = lvl + ' ' + Math.round(WIND[lvl] * 100) + '% · gust ' + Math.round(GUST * 100) + '% · wind from the ' + (dir > 0 ? 'west' : 'east') + ' · frame ' + (WF + 1) + ' of 16';
      return stack([
        { label: 'shader · rest pose + _wind + _phase (treeMaps4.shade) · ' + tag, img: lineup(k => LIT(M4().shade(M4().rest(k, o), W, WF), sky), sky) },
        { label: 'rig · baked frame (TreeRig4.frame) · ' + tag, img: lineup(k => LIT(R().frame(k, Object.assign({ wind: W, frame: WF }, o)), sky), sky) },
      ], 'Wind ' + lvl + ', from the ' + (dir > 0 ? 'west' : 'east'), 'mature · summer · variant 1 · 14:00 clear · agreement per species in checks/out/wind.txt');
    };
  }
  // one gale loop, shader over rig, for three trees
  SPECS['wind-gale-loop'] = () => {
    const sky = SKY(), W = { w: 1, gust: GUST, dir: 1 }, o = Object.assign({ season: 'summer' }, MAT), FR = [0, 2, 4, 6, 8, 10, 12, 14], panels = [];
    for (const k of ['RedOak', 'WhitePine', 'TremblingAspen']) {
      const rf = M4().rest(k, o), cw = rf.v.w, ch = rf.v.h, Wd = FR.length * (cw + 6), lv = floor(sky, Wd, ch + 10);
      for (const src of ['shader', 'rig']) {
        const img = { w: Wd, h: ch + 10, rgba: new Uint8ClampedArray(lv[0]) };
        const items = FR.map((f, i) => Object.assign(LIT(src === 'shader' ? M4().shade(rf, W, f) : R().frame(k, Object.assign({ wind: W, frame: f }, o)), sky), { x: i * (cw + 6) + rf.mdl.pivot.x }));
        for (const it of items) blit(img, lv, it, it.x, rf.mdl.pivot.y + 4);
        for (const it of items) sprite(img, it, it.x, rf.mdl.pivot.y + 4);
        panels.push({ label: R().byKey[k].name + ' · ' + src + ' · frames 1 3 5 7 9 11 13 15', img });
      }
    }
    return stack(panels, 'A gale loop, shader over rig', 'wind 100% · gust 50% · from the west · mature summer variant 1');
  };
  const NAMES = ['family-seasons', 'snow-000', 'snow-050', 'snow-100', 'snow-map', 'wind-calm-west', 'wind-calm-east', 'wind-breeze-west', 'wind-breeze-east', 'wind-gale-west', 'wind-gale-east', 'wind-gale-loop', 'wind-weights', 'pass3-vs-4.1'];
  function render(name) { const f = SPECS[name]; if (!f) throw new Error('TREE_REVIEW: no render named ' + name); return f(); }

  root.TREE_REVIEW = { NAMES, render, FAMILY, text, stack, lineup };
})(typeof globalThis !== 'undefined' ? globalThis : window);
