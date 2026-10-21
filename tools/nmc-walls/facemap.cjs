// NMC walls (H3): every steep piece G3 holds. A texel is steep where the ground's gradient at its centre (central
// differences over the texel grid, 0.5 m) is at least the face floor (41 deg); steep texels group 8-connected into pieces.
// A piece is a face where it falls MinFaceDrop or more (its highest texel, or a texel bordering it, over its lowest).
'use strict';
const F = require('./faces.cjs');

function steepPieces(g) {
  const W = g.W, H = g.H, E = g.elev, T = 0.5;
  const steep = new Uint8Array(W * H), slope = new Float32Array(W * H), az = new Float32Array(W * H);
  for (let r = 1; r < H - 1; r++) for (let c = 1; c < W - 1; c++) {
    const i = r * W + c, gx = (E[i + 1] - E[i - 1]) / (2 * T), gy = (E[i + W] - E[i - W]) / (2 * T);
    const s = Math.atan(Math.hypot(gx, gy)) * 180 / Math.PI;
    slope[i] = s;
    let a = Math.atan2(-gx, -gy) * 180 / Math.PI; if (a < 0) a += 360; az[i] = a;     // downhill bearing, N = 0, clockwise
    if (s >= F.FACE_FLOOR) steep[i] = s >= F.MIN_BATTER ? 2 : 1;
  }
  const lab = new Int32Array(W * H), pieces = [];
  for (let i = 0; i < W * H; i++) {
    if (!steep[i] || lab[i]) continue;
    const id = pieces.length + 1, q = [i]; lab[i] = id;
    const p = { id, n: 0, kit: 0, c0: 1e9, c1: -1, r0: 1e9, r1: -1, zMax: -99, zMin: 99, sSum: 0, sMax: 0, ax: 0, ay: 0 };
    while (q.length) {
      const j = q.pop(), r = (j / W) | 0, c = j % W;
      p.n++; if (steep[j] === 2) p.kit++;
      p.c0 = Math.min(p.c0, c); p.c1 = Math.max(p.c1, c); p.r0 = Math.min(p.r0, r); p.r1 = Math.max(p.r1, r);
      p.zMax = Math.max(p.zMax, E[j]); p.zMin = Math.min(p.zMin, E[j]);
      p.sSum += slope[j]; p.sMax = Math.max(p.sMax, slope[j]);
      p.ax += Math.sin(az[j] * Math.PI / 180); p.ay += Math.cos(az[j] * Math.PI / 180);
      for (let dr = -1; dr <= 1; dr++) for (let dc = -1; dc <= 1; dc++) {
        const rr = r + dr, cc = c + dc; if (rr < 0 || cc < 0 || rr >= H || cc >= W) continue;
        const k = rr * W + cc; if (steep[k] && !lab[k]) { lab[k] = id; q.push(k); }
      }
    }
    pieces.push(p);
  }
  for (let r = 1; r < H - 1; r++) for (let c = 1; c < W - 1; c++) {       // the ground just outside each piece
    const i = r * W + c; if (lab[i]) continue;
    for (const k of [i - 1, i + 1, i - W, i + W]) if (lab[k]) { const p = pieces[lab[k] - 1]; p.zMax = Math.max(p.zMax, E[i]); p.zMin = Math.min(p.zMin, E[i]); }
  }
  for (const p of pieces) {
    const [x0, y0] = g.centre(p.c0, p.r0), [x1, y1] = g.centre(p.c1, p.r1);
    let a = Math.atan2(p.ax, p.ay) * 180 / Math.PI; if (a < 0) a += 360;
    Object.assign(p, { x0, x1, y0, y1, drop: p.zMax - p.zMin, slopeMean: p.sSum / p.n, downhill: a, coherence: Math.hypot(p.ax, p.ay) / p.n,
      face: p.zMax - p.zMin >= F.MIN_DROP, ...F.aspectOf(a) });
  }
  /** The piece at a world point: its own texel's, else the nearest labelled texel within `reach` texels. */
  function pieceAt(x, y, reach = 1) {
    const c = g.colOf(x), r = g.rowOf(y);
    if (r >= 0 && r < H && c >= 0 && c < W && lab[r * W + c]) return lab[r * W + c];
    let best = 0, bd = 1e9;
    for (let dr = -reach; dr <= reach; dr++) for (let dc = -reach; dc <= reach; dc++) {
      const rr = r + dr, cc = c + dc; if (rr < 0 || cc < 0 || rr >= H || cc >= W || !lab[rr * W + cc]) continue;
      const d = dr * dr + dc * dc; if (d < bd) { bd = d; best = lab[rr * W + cc]; }
    }
    return best;
  }
  /** The pieces a polyline crosses or touches (every texel within `reach` of it, sampled every quarter texel). */
  function piecesAlong(points, reach = 1) {
    const out = new Set();
    for (let i = 0; i + 1 < points.length; i++) {
      const [ax, ay] = points[i], [bx, by] = points[i + 1], n = Math.max(1, Math.ceil(Math.hypot(bx - ax, by - ay) / 0.125));
      for (let k = 0; k <= n; k++) { const id = pieceAt(ax + (bx - ax) * k / n, ay + (by - ay) * k / n, reach); if (id) out.add(id); }
    }
    return out;
  }
  return { lab, pieces, slope, az, pieceAt, piecesAlong };
}

module.exports = { steepPieces };
