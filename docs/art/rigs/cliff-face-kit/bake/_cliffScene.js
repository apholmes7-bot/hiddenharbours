/* Art/_cliffScene.js — the axonometric harness the cliff rig is CHECKED in.
   Not shipped, not part of the kit. It exists because a cliff face cannot be
   judged flat: the whole point of the v10 form pass is what the wall does in
   PLAN, and a batter is geometry by definition. Both only show when the wall is
   real geometry under the kit's own camera.

   Camera: yaw from the south, pitch above the horizon, orthographic, S px/m.
   Screen y is down. Depth is camera-space so a z-buffer sorts everything and no
   painter ordering is needed.

   Everything is written into one RGBA buffer with a Float32 depth buffer, then
   handed back as a canvas.

     CliffScene.render({
       W, H, S, yaw, pitch, ox, oy,
       coast: [[x,y], ...],          // control points, sea on the RIGHT of travel
       at(s) -> {rock, slope, top},  // what the cliff is doing at arc length s
       faceTex(rock, slope) -> {data,W,H},
       brow(rock, slope), toe(rock, slope),   // optional RGBA decals
       ground(x, y) -> [r,g,b],      // plan-view land surface
       platform(x, y) -> [r,g,b],    // plan-view shore platform
       sea: [r,g,b], inland: metres, platWidth: metres
     })
*/
globalThis.CliffScene = (function () {
  const F = Math.floor, PI = Math.PI;
  const clamp = (x, a, b) => x < a ? a : (x > b ? b : x);
  const lerp = (a, b, t) => a + (b - a) * t;

  /* ---- catmull-rom coast, arc-length parameterised ---- */
  function coast(pts) {
    const P = pts.slice();
    P.unshift(pts[0]); P.push(pts[pts.length - 1], pts[pts.length - 1]);
    const seg = P.length - 3;
    const NS = 3000, xs = new Float64Array(NS + 1), ys = new Float64Array(NS + 1), ls = new Float64Array(NS + 1);
    for (let i = 0; i <= NS; i++) {
      const g = i / NS * seg, gi = Math.min(F(g), seg - 1), t = g - gi;
      const p0 = P[gi], p1 = P[gi + 1], p2 = P[gi + 2], p3 = P[gi + 3];
      const t2 = t * t, t3 = t2 * t;
      xs[i] = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3);
      ys[i] = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3);
      ls[i] = i ? ls[i - 1] + Math.hypot(xs[i] - xs[i - 1], ys[i] - ys[i - 1]) : 0;
    }
    const L = ls[NS];
    let cur = 0;
    return {
      L,
      at(s) {
        s = clamp(s, 0, L);
        while (cur < NS && ls[cur + 1] < s) cur++;
        while (cur > 0 && ls[cur] > s) cur--;
        const f = (s - ls[cur]) / Math.max(ls[cur + 1] - ls[cur], 1e-9);
        const x = lerp(xs[cur], xs[cur + 1], f), y = lerp(ys[cur], ys[cur + 1], f);
        const i0 = Math.max(cur - 2, 0), i1 = Math.min(cur + 3, NS);
        let tx = xs[i1] - xs[i0], ty = ys[i1] - ys[i0];
        const m = Math.hypot(tx, ty) || 1; tx /= m; ty /= m;
        return { x, y, tx, ty, nx: ty, ny: -tx };   /* normal to the RIGHT of travel */
      }
    };
  }

  function render(o) {
    const W = o.W, H = o.H, S = o.S || 32;
    const ps = Math.sin((o.pitch || 40) * PI / 180), pc = Math.cos((o.pitch || 40) * PI / 180);
    const ys = Math.sin((o.yaw || 0) * PI / 180), yc = Math.cos((o.yaw || 0) * PI / 180);
    const ox = o.ox || 0, oy = o.oy || 0;
    const buf = new Uint8ClampedArray(W * H * 4);
    const zb = new Float32Array(W * H).fill(1e9);

    const sea = o.sea || [22, 62, 74];
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = (y * W + x) * 4, k = 1 - y / H * 0.35;
      buf[i] = sea[0] * k; buf[i + 1] = sea[1] * k; buf[i + 2] = sea[2] * k; buf[i + 3] = 255;
    }

    /* project + depth-tested plot */
    function proj(x, y, z) {
      const e = x * yc + y * ys, n = -x * ys + y * yc;
      return { sx: ox + e * S, sy: oy - (n * ps + z * pc) * S, d: n * pc - z * ps };
    }
    function px(sx, sy, d, r, g, b, a) {
      const x = sx | 0, y = sy | 0;
      if (x < 0 || y < 0 || x >= W || y >= H) return;
      const k = y * W + x, i = k * 4;
      if (a >= 254) {
        if (d >= zb[k]) return;
        zb[k] = d; buf[i] = r; buf[i + 1] = g; buf[i + 2] = b; buf[i + 3] = 255;
      } else {
        if (a <= 1 || d > zb[k] + 0.35) return;
        const t = a / 255;
        buf[i] = lerp(buf[i], r, t); buf[i + 1] = lerp(buf[i + 1], g, t); buf[i + 2] = lerp(buf[i + 2], b, t);
        if (d < zb[k]) zb[k] = d;
      }
    }

    const C = coast(o.coast), L = C.L;
    const ds = 0.42 / S;                    /* sub-pixel step along the coast */
    const inland = o.inland == null ? 16 : o.inland;
    const platW = o.platWidth == null ? 5 : o.platWidth;
    const TILE = 12, TH = 9;                /* face texture covers 12 x 9 m */

    /* ---------------------------------------------------------- land + shore */
    for (let s = 0; s < L; s += ds) {
      const c = C.at(s), a = o.at(s);
      const tanS = Math.tan(a.slope * PI / 180);
      const u = ((s % TILE) / TILE + 1) % 1;
      const dTop = o.disp ? o.disp(u, 0, a) : 0;
      const dToe = o.disp ? o.disp(u, Math.min(a.top / Math.sin(a.slope * PI / 180) / TH, 0.999), a) : 0;
      /* land: march inland from the brow */
      const eX = c.x + c.nx * (dTop - a.top / tanS), eY = c.y + c.ny * (dTop - a.top / tanS);
      for (let d = 0; d <= inland; d += 0.5 / S) {
        const gx = eX - c.nx * d, gy = eY - c.ny * d;
        const col = o.ground(gx, gy, a);
        const p = proj(gx, gy, a.top);
        px(p.sx, p.sy, p.d, col[0], col[1], col[2], 255);
      }
      /* shore platform: march seaward from the toe */
      if (o.platform) for (let d = 0; d <= platW; d += 0.5 / S) {
        const gx = c.x + c.nx * (dToe + d), gy = c.y + c.ny * (dToe + d);
        const z = -0.05 - d * 0.045;
        const col = o.platform(gx, gy, d / platW, a);
        const p = proj(gx, gy, z);
        px(p.sx, p.sy, p.d, col[0], col[1], col[2], 255);
      }
    }

    /* ---------------------------------------------------------------- walls */
    for (let s = 0; s < L; s += ds) {
      const c = C.at(s), a = o.at(s);
      const sinS = Math.sin(a.slope * PI / 180), tanS = Math.tan(a.slope * PI / 180);
      const tex = o.faceTex(a.rock, a.texSlope != null ? a.texSlope : a.slope);
      const brow = o.brow && o.brow(a.rock, a.slope);
      const toe = o.toe && o.toe(a.rock, a.slope);
      const surf = a.top / sinS;                    /* metres down the surface */
      const u = ((s % TILE) / TILE + 1) % 1;
      const tx = clamp(F(u * tex.W), 0, tex.W - 1);
      const rows = Math.max(2, Math.ceil(surf * S * 1.15));
      let prevY = null, prevX = null;
      for (let k = 0; k <= rows; k++) {
        const fs = k / rows;
        const sd = fs * surf;                       /* down the surface, metres */
        const z = a.top - sd * sinS;
        const back = z / tanS;
        const v = (sd / TH) % 1;
        const d = o.disp ? o.disp(u, v, a) : 0;
        const gx = c.x + c.nx * (d - back), gy = c.y + c.ny * (d - back);
        const p = proj(gx, gy, z);
        const ty = clamp(F(v * tex.H), 0, tex.H - 1);
        const ti = (ty * tex.W + tx) * 4;
        let r = tex.data[ti], g = tex.data[ti + 1], b = tex.data[ti + 2];
        /* decals */
        if (brow) {
          const bt = 0.42 + sd / 4;
          if (bt < 1) {
            const by = clamp(F(bt * brow.H), 0, brow.H - 1);
            const bx = clamp(F(u * brow.W), 0, brow.W - 1);
            const bi = (by * brow.W + bx) * 4, al = brow.data[bi + 3] / 255;
            if (al > 0.004) { r = lerp(r, brow.data[bi], al); g = lerp(g, brow.data[bi + 1], al); b = lerp(b, brow.data[bi + 2], al); }
          }
        }
        if (toe) {
          const tt = 1 - (a.top - z) / 4;
          if (tt > 0) {
            const yy = clamp(F((1 - tt) * toe.H), 0, toe.H - 1);
            const xx = clamp(F(u * toe.W), 0, toe.W - 1);
            const ii = (yy * toe.W + xx) * 4, al = toe.data[ii + 3] / 255;
            if (al > 0.004) { r = lerp(r, toe.data[ii], al); g = lerp(g, toe.data[ii + 1], al); b = lerp(b, toe.data[ii + 2], al); }
          }
        }
        const yNow = p.sy, xNow = p.sx;
        if (prevY == null) { px(xNow, yNow, p.d, r, g, b, 255); }
        else {
          const y0 = Math.min(prevY, yNow), y1 = Math.max(prevY, yNow);
          for (let yy = y0; yy <= y1; yy += 1) {
            const f = y1 > y0 ? (yy - y0) / (y1 - y0) : 0;
            px(lerp(prevX, xNow, f), yy, p.d, r, g, b, 255);
          }
        }
        prevY = yNow; prevX = xNow;
      }
    }

    const cv = globalThis.__mkCanvas(W, H);
    cv.getContext('2d').putImageData(new ImageData(buf, W, H), 0, 0);
    return { canvas: cv, buf, W, H };
  }

  return { render, coast };
})();
