/* Hero-scene bake for the v8 iso shoreline kit.
   Material bands are derived from the LIVE SHORELINE, not from fixed rows — the beach
   profile (wet flat → storm berm → dry sand → backshore) hugs the coast the way a real
   one does, and the fringe autotiles are stamped wherever two materials meet. */
globalThis.SHORE_HERO = function (K, opts) {
  const { createCanvas, dory, RockIso } = opts;
  const TW = 26, TH = 15, W = TW * 32, H = TH * 32, SEED = 5;

  function cellCanvas(cell) {
    const c = createCanvas(cell.w, cell.h);
    c.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(cell.buf), cell.w, cell.h), 0, 0);
    return c;
  }
  const put = (ctx, cell, x, y) => ctx.drawImage(cellCanvas(cell), x, y);

  // ── coastline, in fractional tile rows (where the depth-0 contour crosses each column)
  const CTRL = [[0, 6.2], [3, 6.3], [5, 6.7], [7, 8.5], [10, 10.5], [13, 11.7], [16, 11.1], [19, 9.7], [22, 10.3], [25, 11.5]];
  function shoreRow(gx) {
    let i = 0; while (i < CTRL.length - 2 && gx > CTRL[i + 1][0]) i++;
    const [x0, y0] = CTRL[i], [x1, y1] = CTRL[i + 1];
    const t = Math.max(0, Math.min(1, (gx - x0) / (x1 - x0))), u = t * t * (3 - 2 * t);
    return y0 + (y1 - y0) * u + (K.fbm(gx * 0.42, 0, 61, 2) - 0.5) * 0.9;
  }
  const CLIFF_END = 5, DUNE_START = 18;
  const RANK = { grass: 5, marram: 4.6, shelf: 4, sand: 3, shingle: 2, ripple: 1 };

  // material by distance above the waterline (tiles) — the beach profile
  function matAt(gx, gy) {
    const d = shoreRow(gx) - gy;
    if (d <= 1.05) return 'ripple';
    if (d <= 2.05) return 'shingle';
    if (d <= 5.6) return 'sand';
    // bedrock breaks through the backshore turf here and there
    if (K.fbm(gx * 0.62 + 3, gy * 0.62, 88, 2) > 0.735) return 'shelf';
    return (gx - (DUNE_START - 1.5)) + (K.fbm(gy * 0.55, 0, 91, 2) - 0.5) * 4 > 0 ? 'marram' : 'grass';
  }

  function plan(gx, gy) {
    const ops = [];
    const sr = shoreRow(gx);
    if (gx <= CLIFF_END) {                                  // ── headland: cliff steps down with the shore
      const cap = 2, toe = 5;
      const piece = gx <= 4 ? 'faceS' : 'cornSE';
      if (gy < cap) ops.push(['g', 'grass']);
      else if (gy === cap) ops.push(['g', 'grass'], ['c', piece, 'cap']);
      else if (gy < toe) ops.push(['g', 'shelf'], ['c', piece, 'mid']);
      else if (gy === toe) ops.push(['g', 'shelf'], ['c', piece, 'toe', gx === 2 ? 'cave' : null]);
      else ops.push(['g', gy > sr + 0.6 ? 'shelf' : gy > sr - 1.2 ? 'ripple' : 'shingle']);
      return ops;
    }
    const m = matAt(gx, gy);
    if (gx >= DUNE_START) {                                 // ── dune bank runs level along the backshore
      const bank = 5;
      const pc = gx === DUNE_START ? 'cornSW' : 'faceS';
      if (gy === bank) { ops.push(['g', 'sand'], ['d', pc, 'cap']); return ops; }
      if (gy === bank + 1) { ops.push(['g', 'sand'], ['d', pc, 'toe']); return ops; }
      if (gy === bank + 2) { ops.push(['g', 'sand'], ['ct', 'n']); return ops; }
    }
    ops.push(['g', m]);
    // the higher material laps onto the lower one on every side it touches — organic seams
    for (const [dx, dy, piece] of [[0, -1, 'edN'], [0, 1, 'edS'], [-1, 0, 'edW'], [1, 0, 'edE']]) {
      const nx = gx + dx, ny = gy + dy;
      if (nx < 0 || nx >= TW || ny < 0 || ny >= TH) continue;
      if (nx < CLIFF_END) continue;
      const nm = matAt(nx, ny);
      if (nm !== m && RANK[nm] > RANK[m]) ops.push(['f', nm, piece]);
    }
    return ops;
  }

  return function bake(style) {
    const ST = K.styleOf(style);
    const cv = createCanvas(W, H), ctx = cv.getContext('2d');
    ctx.imageSmoothingEnabled = false;
    ctx.fillStyle = ST.key; ctx.fillRect(0, 0, W, H);

    const cache = [];
    for (let gy = 0; gy < TH; gy++) for (let gx = 0; gx < TW; gx++) cache.push([gx, gy, plan(gx, gy)]);
    for (const base of [true, false]) {
      for (const [gx, gy, ops] of cache) for (const op of ops) {
        const isBase = (op[0] === 'g' || op[0] === 'c' || op[0] === 'd');
        if (base !== isBase) continue;
        if (op[0] === 'g') put(ctx, K.ground(op[1], { gx, gy, seed: SEED, style }), gx * 32, gy * 32);
        else if (op[0] === 'c') put(ctx, K.cliff(op[1], { band: op[2], gx, gy: gy * 32, seed: SEED, feature: op[3], style }), gx * 32, gy * 32);
        else if (op[0] === 'd') put(ctx, K.dune(op[1], { gx, band: op[2], seed: SEED, style }), gx * 32, gy * 32);
        else if (op[0] === 'f') put(ctx, K.fringe(op[1], op[2], { gx, gy, seed: SEED, style }), gx * 32, gy * 32);
        else if (op[0] === 'ct') put(ctx, K.contact(op[1], { style }), gx * 32, gy * 32);
      }
    }
    // contact shade where the cliff toe meets the shelf, and along its east flank
    for (let gx = 0; gx <= CLIFF_END; gx++) put(ctx, K.contact(gx === CLIFF_END ? 'ne' : 'n', { style }), gx * 32, 6 * 32);
    for (let gy = 2; gy <= 7; gy++) put(ctx, K.contact('w', { style }), (CLIFF_END + 1) * 32, gy * 32);


    // ── rock props: Rock Iso volumes, placed by their ground-contact pivot
    function rock(species, variant, gx, gy, tide, dress) {
      const r = RockIso.render(species, dress == null ? { variant } : { variant, dress }, tide || 'dry');
      const rc = createCanvas(r.w, r.h);
      rc.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(r.rgba), r.w, r.h), 0, 0);
      const px = gx - r.anchors.pivot.x, py = gy - r.anchors.pivot.y;
      ctx.save(); ctx.globalAlpha = 0.34; ctx.fillStyle = ST.key;
      ctx.beginPath(); ctx.ellipse(gx + 2, gy - 1, r.anchors.footprint.rx * 34, r.anchors.footprint.ry * 15, 0, 0, 7);
      ctx.fill(); ctx.restore();
      ctx.drawImage(rc, px, py);
    }
    // dry backshore + berm
    rock('Outcrop', 1, 320, 322, 'dry', 0);
    rock('Erratic', 1, 246, 268, 'dry', 0);
    rock('Erratic', 0, 404, 296, 'dry', 0);
    rock('Cobble', 1, 470, 306, 'dry', 0);
    rock('Outcrop', 0, 660, 290, 'dry', 1);
    rock('Erratic', 3, 742, 316, 'dry', 1);
    rock('Cobble', 0, 566, 336, 'dry', 0);
    // wave-cut shelf at the cliff toe — tide pools the shader fills
    rock('PoolLedge', 0, 198, 212, 'wet', 2);
    rock('Erratic', 2, 560, 96, 'dry', 0);            // erratics dumped on the headland turf
    rock('Erratic', 0, 742, 132, 'dry', 0);
    rock('Outcrop', 2, 892, 108, 'dry', 2);
    rock('PoolLedge', 1, 116, 200, 'wet', 2);
    rock('Outcrop', 2, 60, 206, 'wet', 2);

    // ── shader emulation: depth-0 contour, DepthRamp tint, foam + swash (never baked into tiles)
    const shoreY = x => shoreRow(x / 32) * 32 + (K.fbm(x * 0.05, 0, 72, 2) - 0.5) * 5;
    const bar = { cx: 636, cy: 442, rx: 88, ry: 22 };
    const barIn = (x, y) => {
      const dx = (x - bar.cx) / bar.rx, dy = (y - bar.cy) / bar.ry;
      return dx * dx + dy * dy < 1 + (K.fbm(x * 0.045, y * 0.045, 73, 2) - 0.5) * 0.6;
    };
    const img = ctx.getImageData(0, 0, W, H), D = img.data;
    const shal = K.hex2rgb(ST.depthShallow), deep = K.hex2rgb(ST.depthDeep), foam = K.hex2rgb(ST.foam), B8 = K.B8;
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = (y * W + x) * 4;
      let d = y - shoreY(x);
      if (barIn(x, y)) d = Math.min(d, -6);
      if (d < -9) continue;
      const land = [D[i], D[i + 1], D[i + 2]];
      if (d < 0) {
        const m = K.mixc(land, K.mixc(shal, ST.key, 0.5), ((d + 9) / 9) * 0.17);
        D[i] = m[0]; D[i + 1] = m[1]; D[i + 2] = m[2]; continue;
      }
      const swell = K.fbm(x * 0.018, y * 0.048, 74, 3);
      let t = Math.min(1, Math.pow(d / 122, 0.58)) + (swell - 0.5) * 0.10;
      t = Math.max(0, Math.min(1, t));
      let col = K.mixc(land, shal, Math.min(1, d / 22) * 0.92);
      col = K.mixc(col, deep, Math.pow(t, 0.85));
      const fw = 0.8 + Math.max(0, K.fbm(x * 0.08, 0, 75, 3) * 6.2 - 1.7);
      const thr = (B8[y & 7][x & 7] + 0.5) / 64;
      if (d < fw && thr < 1 - (d / fw) * 0.85) col = foam;
      else if (d > fw && d < fw + 7 && thr < 0.18) col = K.mixc(col, foam, 0.6);
      if (swell > 0.74 && ((x * 7 + y * 13) % 131) < 3) col = K.mixc(col, foam, 0.4);
      D[i] = col[0]; D[i + 1] = col[1]; D[i + 2] = col[2]; D[i + 3] = 255;
    }
    ctx.putImageData(img, 0, 0);

    // ── sea stacks & skerries standing in the shader's water, each with a foam collar
    function seaRock(species, variant, gx, gy, dress) {
      const r = RockIso.render(species, { variant, dress }, 'awash');
      const rc = createCanvas(r.w, r.h);
      rc.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(r.rgba), r.w, r.h), 0, 0);
      const rx = Math.max(9, r.anchors.footprint.rx * 34), ry = Math.max(5, r.anchors.footprint.ry * 17);
      const ox = Math.round(gx - rx - 5), oy = Math.round(gy - ry - 5), cw = Math.round(rx * 2 + 10), ch = Math.round(ry * 2 + 10);
      if (ox >= 0 && oy >= 0 && ox + cw <= W && oy + ch <= H) {
        const g2 = ctx.getImageData(ox, oy, cw, ch), G = g2.data;
        for (let yy = 0; yy < ch; yy++) for (let xx = 0; xx < cw; xx++) {
          const dx = (ox + xx - gx) / rx, dy = (oy + yy - gy) / ry;
          const rr = Math.sqrt(dx * dx + dy * dy);
          if (rr > 0.72 && rr < 1.25 && ((B8[(oy + yy) & 7][(ox + xx) & 7] + 0.5) / 64) < (1 - Math.abs(rr - 0.98) / 0.27) * 0.72) {
            const j = (yy * cw + xx) * 4; G[j] = foam[0]; G[j + 1] = foam[1]; G[j + 2] = foam[2]; G[j + 3] = 255;
          }
        }
        ctx.putImageData(g2, ox, oy);
      }
      ctx.drawImage(rc, gx - r.anchors.pivot.x, gy - r.anchors.pivot.y);
    }
    seaRock('Cloven', 0, 62, 384, 1);
    seaRock('Skerry', 1, 150, 430, 2);
    seaRock('Skerry', 2, 210, 348, 2);
    seaRock('Skerry', 3, 250, 456, 0);
    seaRock('Skerry', 0, 452, 468, 2);
    seaRock('Skerry', 2, 700, 462, 2);
    rock('Cobble', 1, 596, 442, 'wet', 0);            // exposed low-tide bar
    rock('Erratic', 2, 668, 436, 'wet', 2);

    // the baked Dory Iso at true scale — the kit's scale reference
    const dcv = createCanvas(160, 156), dc = dcv.getContext('2d');
    dc.imageSmoothingEnabled = false; dc.drawImage(dory, 160, 0, 160, 156, 0, 0, 160, 156);
    ctx.drawImage(dcv, 214, 336);
    return cv;
  };
};
