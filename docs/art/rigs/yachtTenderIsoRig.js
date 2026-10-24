/* Hidden Harbours — ISO YACHT TENDER, PASS 3: her own boat (ADR-0006 bake recipe, same rasteriser as the fleet).
   A 5.2 m centre-console RIB, the expedition yacht's tender. Two ride the yacht's foredeck on chocks either side of
   the crane (ExpeditionYacht120Iso places them from faces()); this file is also the tender's own standalone rig.

   Frame: +x stbd, +y bow, +z up; origin amidships, keel bottom, centreline. 32 px = 1 m, elev 40 default, 8 facings,
   flat facets, ordered dither, 1 px depth keyline, no AA. faces() carry lv:'tender' and a role (shell, floor, fit, rig),
   as the ship reads them; faceList() carries the hull layout's numbers.

   Pass 1 is the exterior and the numbers the next pass builds its deck plan from: LIFT (the three-leg sling to one
   ring over the centre of gravity), HELM (a standing console, character v9.2 helm_idle), BOARD, FLOAT. The outboard
   tilts (opts.tilt 0 down … 1 up); the sling is opt-in (opts.sling).
   Pass 3 (2026-10-11) makes her a hull in ADR 0048's layout (hull-layout-1's spec): byId('yacht_tender'), an OPEN
   boat. faceList, partFaces, interiorFaces ([]), doorFaces ([]), geometry, the spec's paint table (matsFor),
   renderCut, fitCheck and walk(), which hull-layout-1's writer writes to her walk file (never by hand). Her pictures
   are pass 1's, byte for byte.
   Pass 5 (2026-10-19): her names in the game, on the file, the global and the hull id. Nothing drawn changed.

   Exposes globalThis.YachtTenderIso = { W, H, PX, DIRS, pivot, order, defaultElev, PAINTS, paintRamps, matsFor, DIMS, LIFT,
     HELM, BOARD, FLOAT, faces(opts), render(dir, opts), rock(i), ROCK, project(p, dir, opts), and the hull's members:
     id, label, kind, byId, hullIds, LV, RO, GHOST_ROLES, faceList, partFaces, interiorFaces, doorFaces, geometry, walk,
     fitCheck, renderCut, schemeIds, defaultScheme, DWL, cockpitZ, loa, GAIN, BIAS, LN, BAYER, cell, WHEEL_GEO }.
   Plain script, no DOM. */
(function (root) {
  const PX = 32, S = 32, DEG = Math.PI / 180, DEFAULT_ELEV = 40;

  // ---- colour (the fleet's ramps; oklch for the tube colourways) ----
  function oklchHex(Lp, C, h) {
    const hr = h * DEG, a = C * Math.cos(hr), b = C * Math.sin(hr);
    const l_ = Lp + 0.3963377774 * a + 0.2158037573 * b, m_ = Lp - 0.1055613458 * a - 0.0638541728 * b, s_ = Lp - 0.0894841775 * a - 1.2914855480 * b;
    const l = l_ * l_ * l_, m = m_ * m_ * m_, s = s_ * s_ * s_;
    const r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s, g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s, bb = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
    const enc = (u) => { u = u <= 0.0031308 ? 12.92 * u : 1.055 * Math.pow(Math.max(u, 0), 1 / 2.4) - 0.055; const n = Math.round(Math.max(0, Math.min(1, u)) * 255); return (n < 16 ? '0' : '') + n.toString(16); };
    return '#' + enc(r) + enc(g) + enc(bb);
  }
  function mkRamp(n, L0, L1, C, h) { const o = []; for (let i = 0; i < n; i++) { const t = n === 1 ? 0 : i / (n - 1); o.push(oklchHex(L0 + (L1 - L0) * t, C * (1 - 0.12 * t), h)); } return o; }
  const CREAM = ['#8a9297', '#a7afb2', '#c4cbca', '#dbe0dc', '#ecefe9', '#f6f8f2', '#fdfef9'];
  const BOOT = ['#090c11', '#0f131a', '#161c25', '#202734', '#2b3341'];
  const GLAS = ['#05090c', '#0a1218', '#101a22', '#17242e', '#20323f'];
  const STEEL = ['#3a4148', '#565f66', '#7a858c', '#9fabb1', '#c3ced2', '#e6edee'];
  const IRON = ['#0d1013', '#161a20', '#222931', '#323b45'];
  const KEY = '#0d1418';
  const NSKID = mkRamp(6, 0.50, 0.86, 0.008, 95);
  const PAINTS = [
    { id: 'grey', label: 'GREY TUBES', note: 'Grey tubes, white hull — the yacht tender.', tube: [0.34, 0.72, 0.010, 250] },
    { id: 'orange', label: 'RESCUE ORANGE', note: 'Orange tubes, white hull.', tube: [0.46, 0.80, 0.150, 50] },
    { id: 'navy', label: 'NAVY', note: 'Navy tubes to match a navy mother ship.', tube: [0.22, 0.52, 0.070, 262] },
  ];
  const PAINT_BY = {}; PAINTS.forEach(p => { PAINT_BY[p.id] = p; });
  const _rc = {};
  function paintRamps(id) { const p = PAINT_BY[id] || PAINT_BY.grey; return _rc[p.id] || (_rc[p.id] = { tube: mkRamp(6, p.tube[0], p.tube[1], p.tube[2], p.tube[3]) }); }
  const _mc = {};
  // a scheme's paint table (hull-layout spec §5): matsFor(id) is matsFor({ scheme: id }) (or { paint: id }); an unknown
  // id paints grey. Every paint dithers at full scale (dith 1): the painter thresholds every face against BAYER. No room,
  // so no roomMats. RINDEX is the painter's own (the keyline's darker step)
  function matsFor(id) {
    if (id && typeof id === 'object') id = id.scheme != null ? id.scheme : id.paint;
    const key = PAINT_BY[id] ? id : 'grey'; if (_mc[key]) return _mc[key];
    const R = paintRamps(key);
    const MATS = { cream: { ramp: CREAM, off: 0, dith: 1 }, tube: { ramp: R.tube, off: 0, dith: 1 }, deck: { ramp: NSKID, off: 0, dith: 1 }, glas: { ramp: GLAS, off: 0, dith: 1 },
      steel: { ramp: STEEL, off: 0, dith: 1 }, iron: { ramp: IRON, off: 0, dith: 1 }, blk: { ramp: BOOT, off: -1, dith: 1 }, dark: { ramp: BOOT, off: -2, dith: 1 } };
    const RINDEX = {}; [CREAM, R.tube, NSKID, GLAS, STEEL, IRON, BOOT].forEach(r => r.forEach((c, i) => { RINDEX[c] = { r, i }; }));
    const ramps = [], at = {}, M = {};
    for (const k in MATS) { const r = MATS[k].ramp, s = r.join(','); if (at[s] == null) { at[s] = ramps.length; ramps.push(r.slice()); }
      M[k] = { ramp: at[s], off: MATS[k].off, dith: MATS[k].dith }; }
    return (_mc[key] = { scheme: key, MATS, RINDEX, ramps, M, roomMats: [] });
  }

  // ---- rasteriser (the fleet's locked recipe, byte-for-byte the sport fisher's _paint) ----
  const GAIN = 3.0, BIAS = 2.7;
  const LN = (() => { const v = [-0.42, 0.72, 0.52]; const m = Math.hypot(...v); return v.map(c => c / m); })();
  const BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]].map(r => r.map(v => (v + 0.5) / 16));
  function normal(a, b, c) { const ux = b.xr - a.xr, uy = b.yr - a.yr, uz = b.zr - a.zr, vx = c.xr - a.xr, vy = c.yr - a.yr, vz = c.zr - a.zr;
    let nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx; const m = Math.hypot(nx, ny, nz) || 1; return [nx / m, ny / m, nz / m]; }
  function shadeOf(n, se, ce) { return n[0] * LN[0] + (n[1] * se + n[2] * ce) * LN[1] + (-n[1] * ce + n[2] * se) * LN[2]; }
  function camBasis(o) { const th = (o.dir || 0) * Math.PI / 4, e = (o.elev != null ? o.elev : DEFAULT_ELEV) * DEG, roll = (o.roll || 0) * DEG, pitch = (o.pitch || 0) * DEG;
    return { ct: Math.cos(th), stt: Math.sin(th), se: Math.sin(e), ce: Math.cos(e), cr: Math.cos(roll), sr: Math.sin(roll), cq: Math.cos(pitch), sq: Math.sin(pitch), heave: (o.heave || 0) }; }
  function projVert(x, y, z, B, G) { const x1 = x * B.cr + z * B.sr, z1 = -x * B.sr + z * B.cr, y2 = y * B.cq - z1 * B.sq, z2 = y * B.sq + z1 * B.cq;
    const xr = x1 * B.ct - y2 * B.stt, yr = x1 * B.stt + y2 * B.ct, zr = z2; return { xr, yr, zr, sx: G.cx + xr * S, sy: G.cy - (yr * B.se + zr * B.ce) * S - B.heave, d: (yr * B.ce - zr * B.se) }; }
  function _paint(faces, opts, G, MAT) {
    const PW = G.W, PH = G.H, MATS = MAT.MATS, RINDEX = MAT.RINDEX, B = camBasis(opts);
    const zbuf = new Float32Array(PW * PH).fill(Infinity), col = new Array(PW * PH).fill(null), dep = new Float32Array(PW * PH);
    for (const f of faces) {
      const rv = f.v.map(([x, y, z]) => projVert(x, y, z, B, G));
      const n = normal(rv[0], rv[1], rv[2]);
      let sh = shadeOf(n, B.se, B.ce); if (sh < 0 && ((f.b || 0) <= -1)) sh = shadeOf([-n[0], -n[1], -n[2]], B.se, B.ce) * 0.9;
      const fidx = sh * GAIN + BIAS + (f.b || 0), M = MATS[f.mat] || MATS.cream;
      for (let t = 1; t + 1 < rv.length; t++) fillTri(rv[0], rv[t], rv[t + 1]);
      function fillTri(a, b, c) {
        const minX = Math.max(0, Math.floor(Math.min(a.sx, b.sx, c.sx))), maxX = Math.min(PW - 1, Math.ceil(Math.max(a.sx, b.sx, c.sx)));
        const minY = Math.max(0, Math.floor(Math.min(a.sy, b.sy, c.sy))), maxY = Math.min(PH - 1, Math.ceil(Math.max(a.sy, b.sy, c.sy)));
        const area = (b.sx - a.sx) * (c.sy - a.sy) - (c.sx - a.sx) * (b.sy - a.sy); if (Math.abs(area) < 1e-6) return;
        for (let y = minY; y <= maxY; y++) for (let x = minX; x <= maxX; x++) {
          const px = x + 0.5, py = y + 0.5;
          const w0 = ((b.sx - px) * (c.sy - py) - (c.sx - px) * (b.sy - py)) / area, w1 = ((c.sx - px) * (a.sy - py) - (a.sx - px) * (c.sy - py)) / area, w2 = 1 - w0 - w1;
          if (w0 < -0.001 || w1 < -0.001 || w2 < -0.001) continue;
          const d = w0 * a.d + w1 * b.d + w2 * c.d, deff = d - (f.db || 0), i = y * PW + x;
          if (deff < zbuf[i]) { zbuf[i] = deff; dep[i] = d; const base = Math.floor(fidx); const idx = base + ((fidx - base) > BAYER[x & 3][y & 3] ? 1 : 0) + M.off;
            col[i] = M.ramp[Math.max(0, Math.min(M.ramp.length - 1, idx))]; }
        }
      }
    }
    const out = col.slice();
    for (let y = 0; y < PH; y++) for (let x = 0; x < PW; x++) { const i = y * PW + x; if (!col[i]) continue;
      for (const [dx, dy] of [[1, 0], [0, 1]]) { const nx = x + dx, ny = y + dy; if (nx >= PW || ny >= PH) continue; const j = ny * PW + nx; if (!col[j]) continue;
        if (Math.abs(dep[i] - dep[j]) > 0.30) { const far = dep[i] > dep[j] ? i : j, e = RINDEX[col[far]]; if (e && e.i > 0) out[far] = e.r[Math.max(0, e.i - 2)]; } } }
    for (let y = 0; y < PH; y++) for (let x = 0; x < PW; x++) { const i = y * PW + x; if (out[i]) continue; let touch = false;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const nx = x + dx, ny = y + dy; if (nx >= 0 && nx < PW && ny >= 0 && ny < PH && col[ny * PW + nx]) { touch = true; break; } }
      if (touch) { out[i] = KEY; dep[i] = Infinity; } }
    out.dep = dep; return out;
  }
  function _toRGBA(out, PW, PH) { const rgba = new Uint8ClampedArray(PW * PH * 4); if (out.dep) rgba.dep = out.dep;
    for (let i = 0; i < PW * PH; i++) { const c = out[i]; if (!c) { rgba[i * 4 + 3] = 0; continue; }
      rgba[i * 4] = parseInt(c.slice(1, 3), 16); rgba[i * 4 + 1] = parseInt(c.slice(3, 5), 16); rgba[i * 4 + 2] = parseInt(c.slice(5, 7), 16); rgba[i * 4 + 3] = 255; }
    return rgba; }

  // ---- geometry helpers ----
  const lerp = (a, b, t) => a + (b - a) * t, clamp01 = (t) => t < 0 ? 0 : (t > 1 ? 1 : t);
  const v_sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]], v_add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]], v_mul = (a, s) => [a[0] * s, a[1] * s, a[2] * s];
  const v_norm = (a) => { const m = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / m, a[1] / m, a[2] / m]; }, v_cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  function newell(v) { let nx = 0, ny = 0, nz = 0; for (let i = 0; i < v.length; i++) { const a = v[i], b = v[(i + 1) % v.length]; nx += (a[1] - b[1]) * (a[2] + b[2]); ny += (a[2] - b[2]) * (a[0] + b[0]); nz += (a[0] - b[0]) * (a[1] + b[1]); } return [nx, ny, nz]; }
  function mono(pts) {
    pts = pts.slice().sort((a, b) => a[0] - b[0]); const n = pts.length, xs = pts.map(p => p[0]), ys = pts.map(p => p[1]); if (n === 1) return () => ys[0];
    const d = []; for (let i = 0; i < n - 1; i++) d.push((ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]));
    const m = [d[0]]; for (let i = 1; i < n - 1; i++) { if (d[i - 1] * d[i] <= 0) m.push(0); else { const h0 = xs[i] - xs[i - 1], h1 = xs[i + 1] - xs[i], w1 = 2 * h1 + h0, w2 = h1 + 2 * h0; m.push((w1 + w2) / (w1 / d[i - 1] + w2 / d[i])); } } m.push(d[n - 2]);
    return function (x) { if (x <= xs[0]) return ys[0] + m[0] * (x - xs[0]); if (x >= xs[n - 1]) return ys[n - 1] + m[n - 1] * (x - xs[n - 1]);
      let i = 0; while (i < n - 2 && x > xs[i + 1]) i++; const h = xs[i + 1] - xs[i], t = (x - xs[i]) / h, t2 = t * t, t3 = t2 * t;
      return (2 * t3 - 3 * t2 + 1) * ys[i] + (t3 - 2 * t2 + t) * h * m[i] + (-2 * t3 + 3 * t2) * ys[i + 1] + (t3 - t2) * h * m[i + 1]; };
  }

  // ---- dimensions ----
  const L = 5.2, yT = -2.55, yBow = 2.55, SOLE = 0.42, TUBE_R = 0.26, TUBE_X = 0.84, TUBE_Z = 0.56, FLOAT_Z = 0.30;
  const fKz = mono([[yT, 0.06], [-1.0, 0.0], [0.6, 0.0], [1.4, 0.08], [2.0, 0.24], [yBow, 0.46]]);
  const fCz = mono([[yT, 0.20], [0.6, 0.21], [1.6, 0.27], [2.2, 0.36], [yBow, 0.46]]);
  const fCx = mono([[yT, 0.64], [0.4, 0.62], [1.4, 0.50], [2.0, 0.30], [yBow, 0.0]]);
  const fTx = mono([[yT, 0.74], [0.6, 0.72], [1.5, 0.60], [2.1, 0.36], [yBow, 0.04]]);
  const PIV = [0, -2.62, 0.80];                           // outboard tilt pivot (transom bracket)
  const RING = [0, -0.20, 2.35], CG = [0, -0.20, 0.52];
  const EYES = [[0, 2.50, 0.74], [-0.62, -2.50, 0.70], [0.62, -2.50, 0.70]];
  const TILT_MAX = 66;                                    // the outboard's tilt at tilt 1, degrees
  // the deck and its fittings, as build() draws them and walk() keeps the walker clear of them (pass 3)
  const SOLE_Y = [-2.45, 2.05], SOLE_N = 10;              // the sole: from under the transom cap to the bow, in 10 quads
  const soleHalf = (y) => Math.max(0.05, Math.min(0.60, TUBE_X - TUBE_R - 0.02 - Math.max(0, (y - 0.9)) * 0.5));
  const CAP_Y = -2.42;                                    // the transom cap's forward face
  const CON = { x: 0.36, y0: 0.10, y1: 0.82, zt: 1.28 };  // the console: half-width, aft and forward faces, top
  const WHEEL = { hub: [0, CON.y0 - 0.03, 1.10], R: 0.16, r: 0.02 };                   // the wheel: hub, rim radius, rim tube
  const POST = { x: 0.40, y0: -0.78, y1: -0.42, z: 0.96, seatZ: 1.05, backY: -0.66 }; // the leaning post: base, cushion top, backrest face
  const BSEAT = { x: 0.46, y0: 1.30, y1: 1.70, z: 0.78, seatZ: 0.86, xFwd: 0.30, yFwd: 1.98 }; // the bow seat and its forward box

  function build(o) {
    o = o || {}; const tilt = clamp01(o.tilt == null ? 0 : +o.tilt), F = []; let cRL = 'shell', cSec = 'hull';
    const at = (rl) => { cRL = rl; }, sec = (s) => { cSec = s; };      // sec: the section faceList() tags by (pass 3)
    const put = (f) => { if (f.mat === 'leath') { f.mat = 'cream'; f.b = (f.b || 0) - 0.25; } f.lv = 'tender'; f.rl = cRL; f.sec = cSec; F.push(f); };
    function Q(v, out, mat, b, db) { const n = newell(v); if (Math.hypot(n[0], n[1], n[2]) < 1e-9) return; if (n[0] * out[0] + n[1] * out[1] + n[2] * out[2] < 0) v = v.slice().reverse(); put({ v, mat, b: b || 0, db: db || 0 }); }
    function boxQ(x0, x1, y0, y1, z0, z1, mat, b, db, topMat) {
      Q([[x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]], [0, 0, 1], topMat || mat, b, db);
      Q([[x0, y0, z0], [x1, y0, z0], [x1, y1, z0], [x0, y1, z0]], [0, 0, -1], mat, (b || 0) - 1.2, db);
      Q([[x0, y1, z0], [x1, y1, z0], [x1, y1, z1], [x0, y1, z1]], [0, 1, 0], mat, b, db);
      Q([[x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1]], [0, -1, 0], mat, b, db);
      Q([[x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1]], [1, 0, 0], mat, b, db);
      Q([[x0, y0, z0], [x0, y1, z0], [x0, y1, z1], [x0, y0, z1]], [-1, 0, 0], mat, b, db); }
    function tubeQ(A, B2, r0, r1, mat, b) { const ax = v_norm(v_sub(B2, A)); let up = [0, 0, 1]; if (Math.abs(ax[2]) > 0.9) up = [0, 1, 0];
      const u = v_norm(v_cross(ax, up)), w = v_cross(u, ax);
      const ring = (P, r) => [0, 1, 2, 3].map(k => { const a = k * Math.PI / 2 + Math.PI / 4; return v_add(P, v_add(v_mul(u, Math.cos(a) * r), v_mul(w, Math.sin(a) * r))); });
      const R0 = ring(A, r0), R1 = ring(B2, r1 == null ? r0 : r1), mid = v_mul(v_add(A, B2), 0.5);
      for (let k = 0; k < 4; k++) { const k2 = (k + 1) % 4, q = [R0[k], R0[k2], R1[k2], R1[k]], c = v_mul(v_add(v_add(q[0], q[1]), v_add(q[2], q[3])), 0.25); Q(q, v_sub(c, mid), mat, b, -0.15); } }

    // ---- GRP hull: keel, chine, tube seat ----
    at('shell');
    const NS = 20, SEC = [];
    for (let i = 0; i <= NS; i++) { const s = i / NS, y = lerp(yT, yBow, 0.5 - 0.5 * Math.cos(Math.PI * s));
      const zk = fKz(y), zc = fCz(y), xc = Math.max(0, fCx(y)), xt = Math.max(xc, fTx(y));
      SEC.push([[0, y, zk], [xc * 0.5, y, lerp(zk, zc, 0.42)], [xc, y, zc], [lerp(xc, xt, 0.6), y, lerp(zc, 0.42, 0.55)], [xt, y, 0.42]]); }
    for (let i = 0; i < NS; i++) { const A = SEC[i], B2 = SEC[i + 1];
      for (let k = 0; k < 4; k++) for (const sd of [-1, 1]) { const m = (p) => [sd * p[0], p[1], p[2]];
        const q = [m(A[k]), m(B2[k]), m(B2[k + 1]), m(A[k + 1])]; Q(q, k < 2 ? [sd * 0.4, 0, -1] : [sd, 0, 0], 'cream', k < 2 ? -0.55 : (k === 2 ? 0.05 : -0.2), 0); } }
    { const s0 = SEC[0], poly = []; for (let k = 4; k >= 0; k--) poly.push([s0[k][0], yT, s0[k][2]]); for (let k = 1; k <= 4; k++) poly.push([-s0[k][0], yT, s0[k][2]]); Q(poly, [0, -1, 0], 'cream', -0.45, 0); }

    // ---- tubes: one loop, straight sides, elliptical bow, coned tails; rub strake below the outboard shoulder ----
    sec('tube');
    const path = []; for (let i = 0; i <= 8; i++) path.push([TUBE_X, lerp(-2.74, 0.90, i / 8), TUBE_Z]);
    for (let i = 1; i <= 10; i++) { const a = (i / 10) * Math.PI / 2; path.push([TUBE_X * Math.cos(a), 0.90 + 1.66 * Math.sin(a), TUBE_Z + 0.11 * Math.pow(Math.sin(a), 2)]); }
    const NR = 8;
    function ringAtPath(i, sd, r) { const P = path[i], Pm = path[Math.max(0, i - 1)], Pp = path[Math.min(path.length - 1, i + 1)];
      const t = v_norm([Pp[0] - Pm[0], Pp[1] - Pm[1], 0]), nrm = [t[1], -t[0], 0];   // outboard horizontal normal (for the stbd side)
      const out = []; for (let k = 0; k < NR; k++) { const a = (k / NR) * 2 * Math.PI; out.push([sd * (P[0] + r * Math.cos(a) * nrm[0]), P[1] + r * Math.cos(a) * nrm[1] * (sd > 0 ? 1 : 1), P[2] + r * Math.sin(a)]); }
      return out; }
    for (const sd of [-1, 1]) {
      const RG = path.map((_, i) => ringAtPath(i, sd, TUBE_R));
      for (let i = 0; i + 1 < RG.length; i++) for (let k = 0; k < NR; k++) { const k2 = (k + 1) % NR, q = [RG[i][k], RG[i + 1][k], RG[i + 1][k2], RG[i][k2]];
        const ca = (k + 0.5) / NR * 2 * Math.PI, strake = (ca > 4.35 && ca < 5.6);
        const c = v_mul(v_add(v_add(q[0], q[1]), v_add(q[2], q[3])), 0.25), ctr = [sd * path[i][0] * 0.5 + sd * path[i + 1][0] * 0.5, (path[i][1] + path[i + 1][1]) / 2, (path[i][2] + path[i + 1][2]) / 2];
        Q(q, v_sub(c, ctr), strake ? 'dark' : 'tube', strake ? 0.4 : 0.1, 0.01); }
      // tail cone
      const tip = [sd * TUBE_X, -3.00, TUBE_Z - 0.02], R0 = RG[0];
      for (let k = 0; k < NR; k++) { const k2 = (k + 1) % NR; Q([R0[k], R0[k2], tip], v_sub(v_mul(v_add(R0[k], R0[k2]), 0.5), [sd * TUBE_X, -2.74, TUBE_Z]), 'tube', -0.1, 0.01); }
    }

    // ---- sole, transom cap ----
    at('floor'); sec('sole');
    { const N = SOLE_N; for (let i = 0; i < N; i++) { const y0 = lerp(SOLE_Y[0], SOLE_Y[1], i / N), y1 = lerp(SOLE_Y[0], SOLE_Y[1], (i + 1) / N), w = soleHalf;
      Q([[-w(y0), y0, SOLE], [w(y0), y0, SOLE], [w(y1), y1, SOLE], [-w(y1), y1, SOLE]], [0, 0, 1], 'deck', i % 2 ? 0 : -0.15, 0); } }
    at('fit'); sec('cap');
    boxQ(-0.60, 0.60, -2.55, CAP_Y, SOLE, 0.66, 'cream', 0.1);

    // ---- console, windscreen, wheel ----
    sec('console');
    { const x0 = -CON.x, x1 = CON.x, y0 = CON.y0, y1 = CON.y1, zt = CON.zt;
      boxQ(x0, x1, y0, y1, SOLE, zt, 'cream', 0.05);
      Q([[x0 + 0.04, y0 + 0.04, zt + 0.005], [x1 - 0.04, y0 + 0.04, zt + 0.005], [x1 - 0.04, y0 + 0.30, zt + 0.06], [x0 + 0.04, y0 + 0.30, zt + 0.06]], [0, -0.4, 1], 'glas', -0.35, 0.02);
      Q([[x0 + 0.02, y1 - 0.30, zt + 0.02], [x1 - 0.02, y1 - 0.30, zt + 0.02], [x1 - 0.02, y1 - 0.44, zt + 0.40], [x0 + 0.02, y1 - 0.44, zt + 0.40]], [0, 0.6, 0.5], 'glas', -0.55, 0.03);
      tubeQ([x0, y1 - 0.30, zt + 0.03], [x0, y1 - 0.44, zt + 0.42], 0.018, 0.018, 'steel', 0.2); tubeQ([x1, y1 - 0.30, zt + 0.03], [x1, y1 - 0.44, zt + 0.42], 0.018, 0.018, 'steel', 0.2);
      tubeQ([x0, y1 - 0.44, zt + 0.42], [x1, y1 - 0.44, zt + 0.42], 0.018, 0.018, 'steel', 0.3);
      for (const sd of [-1, 1]) tubeQ([sd * 0.40, y0 + 0.05, zt - 0.02], [sd * 0.40, y1 - 0.05, zt - 0.02], 0.016, 0.016, 'steel', 0.3);
      sec('wheel');
      const wc = WHEEL.hub, NW = 8, R = WHEEL.R;
      for (let k = 0; k < NW; k++) { const a0 = k / NW * 2 * Math.PI, a1 = (k + 1) / NW * 2 * Math.PI;
        tubeQ([wc[0] + R * Math.cos(a0), wc[1], wc[2] + R * Math.sin(a0)], [wc[0] + R * Math.cos(a1), wc[1], wc[2] + R * Math.sin(a1)], WHEEL.r, WHEEL.r, 'iron', 0.3); }
      tubeQ([0, y0, 1.10], [0, y0 - 0.04, 1.10], 0.03, 0.03, 'iron', 0.1); }
    // ---- leaning post with stern light, bow seat ----
    sec('post');
    boxQ(-POST.x, POST.x, POST.y0, POST.y1, SOLE, POST.z, 'cream', 0.0);
    boxQ(-0.38, 0.38, -0.76, -0.44, POST.z, POST.seatZ, 'leath', 0.25);
    boxQ(-0.38, 0.38, POST.y0, POST.backY, POST.seatZ, 1.38, 'leath', -0.1);
    tubeQ([0, -0.80, 1.36], [0, -0.82, 1.92], 0.022, 0.018, 'steel', 0.2); boxQ(-0.04, 0.04, -0.86, -0.78, 1.92, 1.99, 'cream', 0.5);
    sec('seat');
    boxQ(-BSEAT.x, BSEAT.x, BSEAT.y0, BSEAT.y1, SOLE, BSEAT.z, 'cream', 0.05); boxQ(-BSEAT.xFwd, BSEAT.xFwd, BSEAT.y1, BSEAT.yFwd, SOLE, BSEAT.z, 'cream', 0.05);
    boxQ(-0.44, 0.44, 1.32, 1.68, BSEAT.z, BSEAT.seatZ, 'leath', 0.2); boxQ(-0.28, 0.28, 1.70, 1.96, BSEAT.z, BSEAT.seatZ, 'leath', 0.2);
    // ---- lifting eyes ----
    sec('eyes');
    for (const e of EYES) boxQ(e[0] - 0.035, e[0] + 0.035, e[1] - 0.035, e[1] + 0.035, e[2] - 0.04, e[2] + 0.02, 'steel', 0.3);

    // ---- outboard (tilts about the transom bracket) ----
    sec('motor');
    const M = []; const saveF = F.length;
    boxQ(-0.12, 0.12, -2.70, -2.56, 0.46, 0.84, 'iron', 0.2);                          // bracket
    boxQ(-0.23, 0.23, -3.06, -2.62, 0.80, 1.36, 'iron', 0.55); boxQ(-0.20, 0.20, -3.02, -2.66, 1.36, 1.47, 'iron', 0.75);
    boxQ(-0.235, 0.235, -3.07, -2.61, 1.02, 1.07, 'steel', 0.6);                       // cowl band
    boxQ(-0.09, 0.09, -2.96, -2.72, -0.10, 0.80, 'iron', 0.1);                           // leg
    boxQ(-0.17, 0.17, -3.02, -2.64, 0.00, 0.03, 'iron', 0.2);                            // anti-cav plate
    boxQ(-0.07, 0.07, -3.00, -2.70, -0.26, -0.10, 'iron', 0.0);                          // gearcase
    boxQ(-0.02, 0.02, -3.08, -3.02, -0.30, -0.08, 'steel', 0.3);                         // prop (edge-on)
    for (let i = saveF; i < F.length; i++) M.push(F[i]);
    if (tilt > 0) { const b = -TILT_MAX * DEG * tilt, cb = Math.cos(b), sb = Math.sin(b);
      for (const f of M) f.v = f.v.map(([x, y, z]) => { const dy = y - PIV[1], dz = z - PIV[2]; return [x, PIV[1] + dy * cb - dz * sb, PIV[2] + dy * sb + dz * cb]; }); }

    // ---- the sling (opt-in): three legs from the eyes to one ring over the centre of gravity ----
    if (o.sling) { at('rig'); sec('sling'); const top = o.slingTop || RING;
      for (const e of EYES) tubeQ([e[0], e[1], e[2] + 0.02], top, 0.012, 0.012, 'iron', 0.3);
      boxQ(top[0] - 0.05, top[0] + 0.05, top[1] - 0.05, top[1] + 0.05, top[2] - 0.04, top[2] + 0.04, 'steel', 0.3); }
    return F;
  }

  const G = { W: 256, H: 244, cx: 128, cy: 152 };
  const _fc = {};
  function faces(o) { o = o || {}; const k = (+o.tilt || 0).toFixed(2) + (o.sling ? 's' : '') + (o.slingTop ? o.slingTop.join(',') : ''); return _fc[k] || (_fc[k] = build(o)); }
  // what render() paints: faces(opts), pass 1's, unless a part is dropped (wheel: false, drive: false: faceList's)
  function drawn(opts) { const s = stateOf(opts); return s.wheel && s.drive ? faces(opts) : faceList(opts); }
  // opts.paint (pass 1's name) or opts.scheme (the spec's) picks the paint; the pictures are pass 1's, byte for byte
  function render(dir, opts) { opts = (typeof opts === 'number') ? { elev: opts } : (opts || {});
    return _toRGBA(_paint(drawn(opts), Object.assign({}, opts, { dir }), G, matsFor(opts)), G.W, G.H); }
  const ROCK = { frames: 8, rollA: 3.2, pitchA: 1.6, heaveA: 1.2, period: 2.4 };
  function rock(i, frames) { frames = frames || ROCK.frames; const a = 2 * Math.PI * (((i % frames) + frames) % frames) / frames;
    return { roll: ROCK.rollA * Math.sin(a), pitch: ROCK.pitchA * Math.sin(a + Math.PI / 2), heave: ROCK.heaveA * Math.sin(a) }; }
  function project(p, dir, opts) { const o = Object.assign({}, (typeof opts === 'number' ? { elev: opts } : opts || {}), { dir }); const q = projVert(p[0], p[1], p[2], camBasis(o), G); return { x: q.sx, y: q.sy }; }

  const LIFT = { ring: RING, cg: CG, eyes: EYES, legs: 3, note: 'Three-leg sling (bow eye, two stern eyes) to one ring 2.35 m over the keel, plumb over the centre of gravity: she hangs level.' };
  const HELM = { stand: [0, -0.22, SOLE], face: [0, 1], wheel: [0, 0.07, 1.10], clip: 'helm_idle', carry: 'helm',
    note: 'Standing console: the helmsman stands between the console and the leaning post. v9.2 drive (seated) needs a 0.25–0.29 m seat, so the tender is driven standing.' };
  const BOARD = { tubeTop: TUBE_Z + TUBE_R, sole: SOLE, points: [{ id: 'port_mid', stand: [-0.45, -0.9, SOLE], over: [-1.1, -0.9] }, { id: 'stbd_mid', stand: [0.45, -0.9, SOLE], over: [1.1, -0.9] }, { id: 'bow', stand: [0, 2.0, SOLE], over: [0, 2.8] }],
    note: 'Step over the tube (top 0.82 over the keel, 0.40 over the sole): stepDown / stepUpNoGrip, both in the v9.2 range 0.06–1.30.' };
  const FLOAT = { waterlineZ: FLOAT_Z, note: 'She floats with her keel 0.30 m under the water: world keel z = water − 0.30.' };

  // ==================================================================================================
  // PASS 3: THE TENDER AS A HULL (ADR 0048 §8 A; hull-layout-1's spec). Hull id 'yacht_tender', through
  // byId('yacht_tender'). An OPEN boat: no room (interiorFaces() is empty), no door leaf (doorFaces() is empty), no
  // house.
  //   levels  main_deck  the sole and what stands on it: the console, the wheel, the leaning post, the bow seat
  //           hull       the GRP hull, the tubes, the transom cap, the lifting eyes, the outboard (never lifted)
  //           rigging    the sling, drawn only while she hangs from the crane (state sling)
  //   cut     'deck'     nothing lifts: she is open to the sky
  //   parts   the wheel is face group tag 1 (state wheel:false drops it), the outboard tag 2 (drive:false); the
  //           outboard's tilt and the sling are state
  // faces() stays pass 1's (lv 'tender' and a role name, as the ship reads them); faceList() gives the same faces in
  // the same order with the spec's tags, from the section build() drew each one in.
  // ==================================================================================================
  const HULL_ID = 'yacht_tender', RIG_FILE = 'yachtTenderIsoRig.js', EXPORT_SYMBOL = 'YachtTenderIso', LABEL = 'YACHT TENDER';
  const LV = { hull: 0, main_deck: 1, house: 2, bridge: 3, below: 4, rigging: 5, foredeck: 6, top: 7 };
  const RO = { shell: 0, wall: 1, roof: 2, lining: 3, part: 4, floor: 5, fit: 6, tall: 7, trim: 8, rig: 9 };
  const GHOST_ROLES = ['shell', 'wall', 'roof', 'lining', 'part', 'tall', 'trim'];
  // build()'s sections: [level, role, tag]; tag 1 the wheel, 2 the drive (the outboard)
  const SECTIONS = { hull: ['hull', 'shell', 0], tube: ['hull', 'shell', 0], cap: ['hull', 'fit', 0], eyes: ['hull', 'fit', 0], motor: ['hull', 'part', 2],
    sole: ['main_deck', 'floor', 0], console: ['main_deck', 'fit', 0], wheel: ['main_deck', 'fit', 1], post: ['main_deck', 'fit', 0], seat: ['main_deck', 'fit', 0],
    sling: ['rigging', 'rig', 0] };
  const ORDER = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  const FRAME = 'metres; +x starboard, +y bow, +z up; origin amidships, keel bottom, centreline; polygons counter-clockwise seen from above';
  // the walker: radius = the largest collider of character rig 10.3's cast (0.23 m), stepTol = v3's, as on the three
  const WALK = { radius: 0.23, stepTol: 0.34 };
  const RULE = 'A step is legal when the target point is inside an area polygon and outside its holes, and |area z - player z| < stepTol. ' +
    'Ramps interpolate z along their axis (x or y) between a0 and a1, and hold z0 / z1 beyond them. cut = the cutaway key while standing there; ' +
    'cutHi / cutLo replace it above / below zMid (stairs and ladders). Polygons are player-centre: already inset by radius, so the reader never insets them again.';
  const WHEEL_GEO = { rad: WHEEL.R, rake: 0 };           // the wheel's rim radius; rake 0: it stands vertical, its axis fore and aft
  const DWL = FLOAT_Z, COCKPIT_Z = SOLE;
  const PERCH = [0, -0.54, POST.seatZ];                   // the helm's perch: the hip on the leaning post's cushion, 0.10 m behind its front edge
  const BOW_SEAT = [0, (BSEAT.y0 + BSEAT.y1) / 2, BSEAT.seatZ];
  const q3 = (v) => Math.round(v * 1000) / 1000, v3 = (p) => [q3(p[0]), q3(p[1]), q3(p[2])];
  const xyz = (p) => ({ x: q3(p[0]), y: q3(p[1]), z: q3(p[2]) });
  // the length overall: the tubes' nose to their tails (the outboard, down, reaches 0.08 m further aft)
  const LOA = (() => { let y0 = Infinity, y1 = -Infinity;
    for (const f of faces({})) if (f.sec === 'hull' || f.sec === 'tube') for (const p of f.v) { if (p[1] < y0) y0 = p[1]; if (p[1] > y1) y1 = p[1]; }
    return y1 - y0; })();
  // every face's length at an outboard tilt, the outboard counted (for DIMS' note)
  const lengthAt = (tilt) => { let y0 = Infinity, y1 = -Infinity; for (const f of faces({ tilt })) for (const p of f.v) { if (p[1] < y0) y0 = p[1]; if (p[1] > y1) y1 = p[1]; } return y1 - y0; };
  // the design figures, as drawn: loa is LOA (the walk's loa_m), hull the drawn hull (transom to stem)
  const DIMS = { loa: q3(LOA), hull: q3(yBow - yT), beam: 2 * (TUBE_X + TUBE_R), sole: SOLE, tubeTop: TUBE_Z + TUBE_R, tubeDia: 2 * TUBE_R, mass_kg: 1100,
    note: 'LOA over the tubes, the outboard not counted (as the walk\'s loa_m): ' + q3(lengthAt(0)) + ' m with the outboard down, ' + q3(lengthAt(1)) + ' m tilted. ' +
      'Hull: the drawn hull, transom to stem. Mass is the design figure the crane is rated against (hull, 115 hp outboard, fuel).' };

  // state: { tilt 0..1, sling, slingTop, wheel, drive }; tower: none on this hull
  function stateOf(o) { o = (o && typeof o === 'object') ? o : {};
    return { tilt: clamp01(o.tilt == null ? 0 : +o.tilt), sling: !!o.sling, slingTop: o.sling && Array.isArray(o.slingTop) ? o.slingTop : null,
      wheel: o.wheel !== false, drive: o.drive !== false }; }
  function faceList(state, tower) { const s = stateOf(state), out = [];
    for (const f of faces({ tilt: s.tilt, sling: s.sling, slingTop: s.slingTop })) { const t = SECTIONS[f.sec];
      if ((t[2] === 1 && !s.wheel) || (t[2] === 2 && !s.drive)) continue;
      const g = { v: f.v.map((p) => p.slice()), mat: f.mat, b: f.b, db: f.db, lv: LV[t[0]], ro: RO[t[1]], it: false }; if (t[2]) g.tag = t[2]; out.push(g); }
    return out; }
  function partFaces(id, state) { const tag = ({ wheel: 1, drive: 2 })[id]; return tag ? faceList(state).filter((f) => f.tag === tag) : []; }
  function interiorFaces() { return []; }
  function doorFaces(t) { return []; }
  function fitCheck(tol) { return { tol: tol == null ? 0.01 : tol, faces: 0, outside: 0, worst: 0, note: 'an open boat: no room faces to check' }; }
  const CUTAWAY = { ghost: { deck: [] }, gate: { deck: [] }, shows: { deck: null },
    note: 'gate lists are complete lift sets in ghost’s vocabulary (whole levels, or level:role). One key, deck: she is open to the sky, nothing lifts, and she has no room.' };
  function geometry() {
    return {
      schema: 'hidden-harbours/hull-geometry@1', rig: RIG_FILE, exportSymbol: EXPORT_SYMBOL, hull: HULL_ID, units: 'metres', frame: FRAME,
      ids: Object.assign({}, LV), roles: Object.assign({}, RO), ghostRoles: GHOST_ROLES.slice(), dwlZ: DWL,
      riggingClass: 'rigging: the three-leg sling, drawn only while she hangs from the crane (state sling, role rig). Afloat or on her chocks there is none.',
      levels: [
        { id: 'main_deck', deck: 'cockpit', sole: SOLE, ceil: null, kind: 'open', soleZ: SOLE, ceilingZ: null,
          ceiling: { kind: 'open', lid: null, note: 'the whole sole is sky: no top' } },
      ],
      cutaway: JSON.parse(JSON.stringify(CUTAWAY)),
      parts: [
        { id: 'wheel', group: 'tag', tag: 1, state: 'wheel', lv: 'main_deck', ro: 'fit', hub: v3(WHEEL.hub), axis: [0, -1, 0], rake_deg: WHEEL_GEO.rake, radius: WHEEL_GEO.rad,
          note: 'the console’s wheel: it stands vertical and turns about its fore-and-aft axis through the hub' },
        { id: 'drive', group: 'tag', tag: 2, state: 'drive', lv: 'hull', ro: 'part', pivot: v3(PIV), tiltMax_deg: TILT_MAX,
          note: 'the outboard on its transom bracket: it tilts about the bracket’s pivot (state tilt)' },
        { id: 'sling', group: 'state', state: 'sling', lv: 'rigging', ro: 'rig', ring: v3(RING), eyes: EYES.map(v3), legs: 3,
          note: 'three legs from the lifting eyes to one ring over the centre of gravity; slingTop moves its top (the crane’s hook)' },
      ],
      states: { tilt: '0..1 (the outboard: 0 down, 1 tilted up ' + TILT_MAX + '°)', sling: 'true|false (the sling drawn; slingTop [x, y, z] its top, default the ring)',
        drive: 'true|false', wheel: 'true|false' },
    };
  }
  // renderCut(dir, key, opts): the gate's lift set dropped (whole levels, or level:role), painted as render paints. No
  // key shows a room: she has none
  function renderCut(dir, key, opts) { opts = (typeof opts === 'number') ? { elev: opts } : (opts || {});
    if (!Object.prototype.hasOwnProperty.call(CUTAWAY.gate, key)) throw new Error('renderCut: no cut key ' + key);
    const whole = new Set(), roles = new Map();
    for (const e of CUTAWAY.gate[key]) { const k = e.indexOf(':'); if (k < 0) { whole.add(LV[e]); continue; }
      const l = LV[e.slice(0, k)]; if (!roles.has(l)) roles.set(l, new Set()); roles.get(l).add(RO[e.slice(k + 1)]); }
    const F = faceList(opts).filter((f) => !(whole.has(f.lv) || (roles.has(f.lv) && roles.get(f.lv).has(f.ro))));
    return _toRGBA(_paint(F, Object.assign({}, opts, { dir }), G, matsFor(opts)), G.W, G.H); }

  // ---- the walk: written to her walk file by hull-layout-1's writer (tools/rig-recipes/hull-walk.mjs), never by hand ----
  // the sole's outline as build() draws it (counter-clockwise), and a footprint per fitting: the plan box of the faces
  // of its sections, the sole to head height (the post's stern light hangs 0.08 m aft of its backrest at 1.5 m)
  const SOLE_POLY = (() => { const rt = [], lt = []; for (let i = 0; i <= SOLE_N; i++) { const y = lerp(SOLE_Y[0], SOLE_Y[1], i / SOLE_N), w = soleHalf(y); rt.push([w, y]); lt.unshift([-w, y]); }
    return rt.concat(lt); })();
  function footprint(secs) { const b = { x0: Infinity, x1: -Infinity, y0: Infinity, y1: -Infinity, z0: Infinity, z1: -Infinity };
    for (const f of faces({})) if (secs.includes(f.sec)) for (const p of f.v) { b.x0 = Math.min(b.x0, p[0]); b.x1 = Math.max(b.x1, p[0]);
      b.y0 = Math.min(b.y0, p[1]); b.y1 = Math.max(b.y1, p[1]); b.z0 = Math.min(b.z0, p[2]); b.z1 = Math.max(b.z1, p[2]); }
    return b; }
  function wPip(p, poly) { let c = false; for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) { const a = poly[i], b = poly[j];
      if ((a[1] > p[1]) !== (b[1] > p[1]) && p[0] < (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0]) c = !c; } return c; }
  function wEdgeD(p, poly) { let d = Infinity; for (let i = 0; i < poly.length; i++) { const a = poly[i], b = poly[(i + 1) % poly.length], dx = b[0] - a[0], dy = b[1] - a[1], L2 = dx * dx + dy * dy;
      const t = L2 ? Math.max(0, Math.min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / L2)) : 0; d = Math.min(d, Math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy)); } return d; }
  const wBoxD = (p, b) => Math.hypot(Math.max(b.x0 - p[0], 0, p[0] - b.x1), Math.max(b.y0 - p[1], 0, p[1] - b.y1));
  // the sole's half-width at y, along its drawn outline
  function soleEdge(y) { const n = SOLE_N, k = Math.max(0, Math.min(n - 1, Math.floor((y - SOLE_Y[0]) / (SOLE_Y[1] - SOLE_Y[0]) * n)));
    const ya = lerp(SOLE_Y[0], SOLE_Y[1], k / n), yb = lerp(SOLE_Y[0], SOLE_Y[1], (k + 1) / n); return lerp(soleHalf(ya), soleHalf(yb), (y - ya) / (yb - ya)); }
  function walk() {
    const R = WALK.radius, FP = { cap: footprint(['cap']), post: footprint(['post']), console: footprint(['console']), wheel: footprint(['wheel']), seat: footprint(['seat']) };
    const obst = Object.values(FP);
    // a centre point fits where the walker's disc lies on the sole and clear of every fitting (1e-9: a row exactly R
    // from a fitting fits, whatever the subtraction rounds to)
    const fits = (x, y) => wPip([x, y], SOLE_POLY) && wEdgeD([x, y], SOLE_POLY) >= R - 1e-9 && obst.every((b) => wBoxD([x, y], b) >= R - 1e-9);
    const half = (y) => { if (!fits(0, y)) return -1; let k = 0; while (k < 700 && fits((k + 1) / 1000, y)) k++; return k / 1000; };
    // an area between two fittings: y0 / y1 rounded inward to the millimetre, its half-width the narrowest of its rows
    const rect = (id, ya, yb, note) => { const y0 = Math.ceil(ya * 1000 - 1e-6) / 1000, y1 = Math.floor(yb * 1000 + 1e-6) / 1000; let h = Infinity;
      for (let k = 0; y0 + k / 1000 <= y1 + 1e-9; k++) h = Math.min(h, half(y0 + k / 1000));
      return { id, cut: 'deck', z: SOLE, poly: [[-h, y0], [h, y0], [h, y1], [-h, y1]], holes: [], note }; };
    const cockpit = rect('cockpit', FP.cap.y1 + R, FP.post.y0 - R,
      'aft of the leaning post to the transom cap; the post’s stern light (' + q3(FP.post.y0) + ') is its forward edge');
    const helm = rect('helm', FP.post.y1 + R, FP.wheel.y0 - R,
      'the standing room at the console: ' + q3(FP.wheel.y0 - FP.post.y1) + ' m between the leaning post and the wheel’s rim, a ' + q3(FP.wheel.y0 - FP.post.y1 - 2 * R) +
      ' m strip at the walker’s radius. It does not join the cockpit (beside_post): the helm is taken from the cockpit (interact helm)');
    const bow = rect('bow', FP.console.y1 + R, FP.seat.y0 - R,
      'the sole between the console and the bow seat: ' + q3(FP.seat.y0 - FP.console.y1) + ' m, a ' + q3(FP.seat.y0 - FP.console.y1 - 2 * R) +
      ' m strip at the walker’s radius. It does not join the helm or the cockpit (beside_console)');
    const areas = [cockpit, helm, bow];
    const yC = (cockpit.poly[0][1] + cockpit.poly[2][1]) / 2;
    const spawn = { x: 0, y: q3(yC), area: 'cockpit' };
    const tubeTop = TUBE_Z + TUBE_R, wPost = q3(soleEdge((FP.post.y0 + FP.post.y1) / 2) - FP.post.x1);
    const wCon = q3(soleEdge(FP.console.y0) - CON.x), wRail = q3(soleEdge(FP.console.y0) - FP.console.x1);
    const _excluded = [
      { id: 'beside_post', of: 'area', z: SOLE, reason: 'the sole beside the leaning post: ' + wPost + ' m between the post (' + q3(2 * FP.post.x1) + ' m wide) and the sole’s edge, narrower than the walker’s ' +
        q3(2 * R) + ' m. The cockpit and the helm do not join at the walker’s radius.' },
      { id: 'beside_console', of: 'area', z: SOLE, reason: 'the sole beside the console: ' + wCon + ' m between the console (' + q3(2 * CON.x) + ' m wide) and the sole’s edge, ' + wRail +
        ' m beside its grab rails, narrower than the walker’s ' + q3(2 * R) + ' m. The bow does not join the helm or the cockpit at the walker’s radius.' },
      { id: 'bow_well', of: 'area', z: SOLE, reason: 'the sole forward of the bow seat’s box: ' + q3(SOLE_Y[1] - FP.seat.y1) + ' m deep fore and aft, ' + q3(2 * soleEdge(FP.seat.y1)) + '-' +
        q3(2 * soleEdge(SOLE_Y[1])) + ' m wide: no centre point fits the walker.' },
      { id: 'tubes', of: 'area', z: q3(tubeTop), reason: 'the tubes: ' + q3(2 * TUBE_R) + ' m round, their tops ' + q3(tubeTop - SOLE) + ' m over the sole and ' + q3(tubeTop - DWL) +
        ' m over the water. Sat on and stepped over to board (board / boardDown), not walked. She has no ladder: a swimmer comes aboard over them (ladderOn).', waitsOn: ['ladderOn', 'sit'] },
      { id: 'transom_cap', of: 'area', z: q3(FP.cap.z1), reason: 'the transom cap: ' + q3(FP.cap.y1 - FP.cap.y0) + ' m deep fore and aft and ' + q3(FP.cap.z1 - SOLE) +
        ' m over the sole, a ledge, not a floor. The outboard is worked from the cockpit (interact outboard).' },
      { id: 'leaning_post', of: 'seat', at: xyz(PERCH), reason: 'the leaning post: its cushion, ' + q3(POST.seatZ - SOLE) + ' m over the sole, is the helm’s perch (anchors.helm), not a standing surface. Perching waits on sit.',
        waitsOn: ['sit'] },
      { id: 'bow_seat', of: 'seat', at: xyz(BOW_SEAT), reason: 'the bow seat and its forward box, the cushion ' + q3(BSEAT.seatZ - SOLE) + ' m over the sole: a seat. Sitting waits on sit.', waitsOn: ['sit'] },
      { id: 'washboards', of: 'washboards', reason: 'an open boat: no side deck. The tubes are sat on and stepped over, not walked (tubes); her lines are made fast to the lifting eyes (cleats).' },
    ];
    const on = (a, x, y) => ({ x: q3(x), y: q3(y), z: a.z });
    const interact = [
      { id: 'helm', action: 'enter_helm', label: 'Helm', prompt: 'Take the wheel', at: xyz(WHEEL.hub), reach: on(cockpit, 0, cockpit.poly[2][1]), area: 'cockpit',
        seat: xyz(PERCH), stand: xyz(HELM.stand),
        note: 'a standing console: the pilot stands between it and the leaning post, whose cushion is the perch (seat). Taken from the cockpit, behind the post.' },
      { id: 'seat_bow', action: 'enter_ride', label: 'Bow seat', prompt: 'Ride', at: xyz(BOW_SEAT), reach: on(bow, 0, bow.poly[2][1]), area: 'bow', waitsOn: ['sit'] },
      { id: 'outboard', action: 'open_close', label: 'Outboard', prompt: 'Tilt up · down', state: 'tilt', at: xyz(PIV), reach: on(cockpit, 0, cockpit.poly[0][1]), area: 'cockpit',
        note: 'tilt 0 down … 1 up ' + TILT_MAX + '° about the transom bracket, worked over the transom cap' },
    ];
    const cleats = [
      { id: 'bow_eye', type: 'eye', pos: v3(EYES[0]), note: 'the bow lifting eye: the painter' },
      { id: 'stern_port', type: 'eye', pos: v3(EYES[1]), note: 'a stern lifting eye, also the stern line’s' },
      { id: 'stern_star', type: 'eye', pos: v3(EYES[2]), note: 'a stern lifting eye, also the stern line’s' },
    ];
    return {
      schema: 'hidden-harbours/hull-walk@1', rig: RIG_FILE, exportSymbol: EXPORT_SYMBOL, hull: HULL_ID, label: LABEL, kind: 'open',
      units: 'metres', frame: FRAME,
      loa_m: q3(LOA), loh_m: q3(yBow - yT), beam_m: q3(DIMS.beam), DWL, cockpitZ: COCKPIT_Z,
      cell: { W: G.W, H: G.H, pivot: { x: G.cx, y: G.cy }, pxPerM: PX, defaultElev: DEFAULT_ELEV, order: ORDER.slice() },
      tags: { lv: Object.assign({}, LV), ro: Object.assign({}, RO), ghostRoles: GHOST_ROLES.slice() },
      walk: { radius: R, stepTol: WALK.stepTol, rule: RULE, spawn, areas },
      house: null,
      anchors: { helm: xyz(PERCH), wheel: xyz(WHEEL.hub), pilot: xyz(HELM.stand),
        seats: [Object.assign({ id: 'leaning_post' }, xyz(PERCH)), Object.assign({ id: 'bow_seat' }, xyz(BOW_SEAT))],
        painter: xyz(EYES[0]), sternEyes: EYES.slice(1).map(xyz), driveMounts: [xyz(PIV)] },
      interact, cleats, washboards: [], _excluded,
      rock: Object.assign({}, ROCK),
      qa: { fitCheck: fitCheck() },
    };
  }

  const API = { W: G.W, H: G.H, PX, DIRS: 8, pivot: { x: G.cx, y: G.cy }, order: ORDER.slice(), defaultElev: DEFAULT_ELEV,
    PAINTS, paintRamps, matsFor, defaultPaint: 'grey', DIMS, LIFT, HELM, BOARD, FLOAT, faces, render, ROCK, rock, project,
    _paint, _toRGBA, camBasis, projVert, KEY,
    // pass 3: the hull, through byId('yacht_tender'); the global carries her members under the same names
    id: HULL_ID, label: LABEL, kind: 'open', hullIds: [HULL_ID], defaultHull: HULL_ID, LV, RO, GHOST_ROLES,
    faceList, partFaces, interiorFaces, doorFaces, geometry, walk, fitCheck, renderCut,
    schemeIds: PAINTS.map((p) => p.id), defaultScheme: 'grey', DWL, cockpitZ: COCKPIT_Z, loa: LOA,
    GAIN, BIAS, LN, BAYER, cell: { W: G.W, H: G.H, pivot: { x: G.cx, y: G.cy }, PX, defaultElev: DEFAULT_ELEV }, WHEEL_GEO };
  API.byId = (id) => (id === HULL_ID ? API : null);
  root.YachtTenderIso = API;
})(typeof globalThis !== 'undefined' ? globalThis : window);
