/* expeditionYacht120IsoRig.js as the game loads it: two rigs in one file. YachtTenderIso (yachtTenderIsoRig.js)
   comes first, so that its global is defined when the ship's tenders draw; ExpeditionYacht120Iso
   (expeditionYacht120IsoRig.js, the ship's own source) follows. Edit the two sources, never this file.
     yachtTenderIsoRig.js         LF sha256 5bb3e86b14bf682d01006f666bb7f90069454984a4d39ec3932f25b67f4cc3c6
     expeditionYacht120IsoRig.js  LF sha256 f0eed8415039ffc979a78eacd0cafa8acfb97634ac726d25d1f96519b84706ba */
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
   Pass 6 (2026-10-26): her walk is the cockpit. The strips at the console and forward of it, a centimetre or two deep
   at the walker's radius and joined to nothing, are no place to stand: each is an _excluded floor. Nothing drawn
   changed.

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
    // the standing room at the console and the sole forward of it: strips a centimetre or two deep at the walker's radius,
    // joined to nothing. No place to stand, so not walked (pass 6): each is an _excluded floor, its outline the strip's
    const helm = rect('helm', FP.post.y1 + R, FP.wheel.y0 - R), bow = rect('bow', FP.console.y1 + R, FP.seat.y0 - R);
    const strip = (a) => q3(a.poly[2][1] - a.poly[0][1]) + ' by ' + q3(a.poly[1][0] - a.poly[0][0]) + ' m';
    const areas = [cockpit];
    const yC = (cockpit.poly[0][1] + cockpit.poly[2][1]) / 2;
    const spawn = { x: 0, y: q3(yC), area: 'cockpit' };
    const tubeTop = TUBE_Z + TUBE_R, wPost = q3(soleEdge((FP.post.y0 + FP.post.y1) / 2) - FP.post.x1);
    const wCon = q3(soleEdge(FP.console.y0) - CON.x), wRail = q3(soleEdge(FP.console.y0) - FP.console.x1);
    const _excluded = [
      { id: 'helm', of: 'area', z: SOLE, poly: helm.poly, reason: 'the standing room at the console: ' + q3(FP.wheel.y0 - FP.post.y1) + ' m between the leaning post and the wheel’s rim, a ' +
        strip(helm) + ' strip at the walker’s radius that does not join the cockpit (beside_post). No place to stand: the helm is taken from the cockpit (interact helm).' },
      { id: 'bow', of: 'area', z: SOLE, poly: bow.poly, reason: 'the sole between the console and the bow seat: ' + q3(FP.seat.y0 - FP.console.y1) + ' m, a ' + strip(bow) +
        ' strip at the walker’s radius that joins neither the helm nor the cockpit (beside_console). No place to stand: the bow seat is taken from it, off the walk (interact seat_bow).' },
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
      { id: 'seat_bow', action: 'enter_ride', label: 'Bow seat', prompt: 'Ride', at: xyz(BOW_SEAT), reach: on(bow, 0, bow.poly[2][1]), area: null, offWalk: 'bow', waitsOn: ['sit'],
        note: 'taken at its stand, off the walk (see _excluded bow)' },
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
/* Hidden Harbours — ISO EXPEDITION YACHT 120, PASS 1: the exterior (ADR-0006 bake recipe).
   36.75 m LOA, 8.51 m beam, 2.74 m draft: a full-displacement hull whose forward topsides rise a full deck (the
   master suite's walls) to a flush foredeck, a curved 39° stem over a small bulb, a sugar-scoop stern with a swim
   platform and twin stairs, a salon house on the main deck behind 1.1 m side decks, the 01 house (sky lounge,
   captain's cabin, pilothouse with a reverse-raked windshield) inside a Portuguese bridge, an enclosed flybridge
   under a hardtop with the spa deck aft, a raked mast with twin domes, and two 5.2 m RIB tenders on chocks either
   side of a telescopic crane on the foredeck.

   Same camera, rasteriser and conventions as the fleet (sportFisherIsoRig2.js): 32 px = 1 m, elev 40 default,
   8 facings at 45°, flat facets, ordered dither, 1 px depth keyline, no AA. Frame +x stbd, +y bow, +z up; origin
   amidships, keel bottom, centreline; the pivot is that origin, pinned at every facing.

   PASS 2 (2026-10-09): the tags are ADR 0048's, as sportFisherIsoRig3.js writes them. Every face carries lv (level),
   ro (role) and it (interior): lv hull 0, main_deck 1, house 2, bridge 3, below 4, rigging 5, foredeck 6, top 7;
   ro shell 0, wall 1, roof 2, lining 3, part 4, floor 5, fit 6, tall 7, trim 8, rig 9; it is false on the exterior and
   true in the rooms. The level map, v3's way with the 90 (her salon is house, her skybridge bridge):
     hull       the shell to the sheer, its glazing, the anchor pockets, the gate seams
     main_deck  swim platform, transom, shoulders, aft and side decks, the bulwark's lining and cap, the stair to the 01
     house      the main-deck house (salon, dining, galley, day head, foyer, the master suite under the foredeck);
                its roof is the 01 slab's underside
     bridge     the 01 deck: slab, rails, Portuguese bridge, the 01 house (sky lounge, captain, pilothouse), the stair
                to the fly; its roof is the fly slab's underside
     top        the flybridge: slab, rails, the enclosure (lounge, bar, upper helm), the spa deck; its roof is the hardtop
     below      the lower deck (lazarette, guest cabins, laundry, engine room, VIP suites, crew)
     foredeck   foredeck, its bulwark and rails, windlasses, chocks, the crane (ro rig), the tenders (their own roles)
     rigging    mast, yard, domes, radar, whips
   Pass 1's roles map as ceil → the roof of the level below, glass → wall (shell on the hull), stair → fit, crane → rig.

   The crane and the tenders are posed per call: render(dir, { launch:{ side:'stbd'|'port', t:0…1 } }) runs the
   design sequence (stowed → hooked → lifted → slewed out → lowered → afloat; recovery is the same t run back).
   YachtTenderIso (yachtTenderIsoRig.js) must be loaded first for the tenders to draw.

   STEP 0 (2026-09-28, the pass-1 carry-overs). The salon side doors and the captain's side doors widen to 0.80 clear.
   Each side deck gets a boarding gate cut in the bulwark (GATE, y −6.50…−5.70, 0.80 clear): the leaf is the bulwark
   itself, hinged at its forward edge; opts.gates (0…1, true, or { port, stbd }) folds it forward flat against the
   lining. The swim platform gets its telescopic ladder (LADDER): stowed, only the head plate shows; opts.ladder hangs
   six 0.24 m rungs into the water with grab loops above the platform. Closed and stowed, every render matches pass 1
   except at those four places (the baseline is the pass-1 rig).

   PASS 2, the doors. Every door with a leaf is in LEAVES: salon_aft_port/_stbd (sliders), salon_side_*, side_deck_fwd_*,
   captain_*, wing_* (hinged, out), skylounge_aft_*, fly_aft (sliders) and gate_port/_stbd (the bulwark gates). Each leaf
   is cut from its wall: its panel, its glass and the plug of wall behind them; closed, it is exactly the faces it was cut
   from. doorFaces(t) poses them all (t a number, or { id: t }); faceList() is the exterior without them, and render()
   draws both (opts.doors, a number or { id: t }; the gates keep opts.gates).

   PASS 2, the rooms and the cut. The four levels' rooms are tagged mesh (interiorFaces(), it true): the lower deck, the
   main deck (salon, dining, galley, master suite), the 01 (sky lounge, captain, pilothouse) and the fly, with closed
   flights between them; fitCheck(0.01) holds every room face inside the shell. render(dir, { cutaway: { mode, level,
   player } }) adds the rooms, lifts the level's CUTAWAY set and ghosts the walls between the camera and the player, as v3
   does. design() carries the rooms, their doorways, flights and stations, the mesh budget, and the checks on all of them.

   PASS 4 (2026-10-18), hull-layout-1's API (its spec §4–§5; amendment 1 §2.1). The global is the hull's: hullIds, byId,
   kind 'ship', schemeIds and defaultScheme. matsFor(scheme | { scheme }) is the spec's paint table. faceList(state) takes
   the states geometry().states lists (tenders, launch, ladder; doorOpen and gates are render's). partFaces(id, state)
   poses one part: the crane, a tender, the ladder, a leaf, or a door's leaves. geometry() names the rig, its door
   (house.door: the salon's aft sliders) and its parts. renderCut(dir, key, opts) draws a key's gate with the shown
   level's room, as the bake places it. The game loads her as one file: the tender's rig, then hers.
   walk() is her hull-layout walk (kind ship), every number read off the drawn faces; hull-walk.mjs writes it from walk().

   PASS 5 (2026-10-19): her names in the game, on the files, the globals, the hull ids and the paints. Nothing drawn
   changed.

   PASS 6 (2026-10-26): her floors, one per height on a key, as the game joins them. Every indoor walk area stands at its
   level's sole (below 1.90, house 4.30, bridge 7.00, fly 9.70), the carpet and teak over it drawn and never walked. Aft
   of the engine room the lower deck is one floor on the lazarette's sole, 2.08: the aft lobby, the corridor, the guest
   cabins and their ensuites, the laundry and the linen rise 0.18 m with their rooms, the step at the lazarette's door
   goes, and lower_aft lands on it in twelve risers of 0.185. The captain's wardrobe is 0.35 m deep, not 0.60, by the
   owner's ruling (10-27), so the strip inside the cabin's starboard door joins its floor. Nothing else drawn moved.

   Exposes globalThis.ExpeditionYacht120Iso = { id, label, kind, hullIds, defaultHull, byId(id), loa, loaText, L, DWL, cockpitZ,
     W, H, PX, DIRS, pivot, order, defaultElev, PAINTS, paintRamps, schemeIds, defaultScheme, matsFor(scheme), MATS, GAIN,
     BIAS, LN, BAYER, KEY, EDGE, cell, LV, RO, GHOST_ROLES, LEVEL_IDS, ROLES, render(dir, opts), renderCut(dir, key, opts),
     faceList(state), doorFaces(t), partFaces(id, state), LEAVES, interiorFaces(), fitCheck(tol), geometry(), design(),
     CRANE, cranePose(opts), launchState(side, t), sequence(side), loft, rock(i), ROCK, project(p, dir, opts), CHAR92,
     ROOM_RULES, GATE, LADDER, walk() }: ADR 0048 §6.6's names and hull-layout-1's.
     Plain script, no DOM. */
(function (root) {
  const PX = 32, S = 32, DEG = Math.PI / 180, DEFAULT_ELEV = 40;

  // ================================= COLOUR =================================
  function oklchHex(Lp, C, h) {
    const hr = h * DEG, a = C * Math.cos(hr), b = C * Math.sin(hr);
    const l_ = Lp + 0.3963377774 * a + 0.2158037573 * b, m_ = Lp - 0.1055613458 * a - 0.0638541728 * b, s_ = Lp - 0.0894841775 * a - 1.2914855480 * b;
    const l = l_ * l_ * l_, m = m_ * m_ * m_, s = s_ * s_ * s_;
    const r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s, g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s, bb = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
    const enc = (u) => { u = u <= 0.0031308 ? 12.92 * u : 1.055 * Math.pow(Math.max(u, 0), 1 / 2.4) - 0.055; const n = Math.round(Math.max(0, Math.min(1, u)) * 255); return (n < 16 ? '0' : '') + n.toString(16); };
    return '#' + enc(r) + enc(g) + enc(bb);
  }
  function mkRamp(n, L0, L1, C, h) { const o = []; for (let i = 0; i < n; i++) { const t = n === 1 ? 0 : i / (n - 1); o.push(oklchHex(L0 + (L1 - L0) * t, C * (1 - 0.12 * t), h)); } return o; }
  const HULLW = ['#7f878c', '#9ea6a9', '#bcc3c2', '#d5dbd7', '#e9ede7', '#f4f7f1', '#fcfdf8'];
  const BOOT = ['#090c11', '#0f131a', '#161c25', '#202734', '#2b3341'];
  const CREAM = ['#8a9297', '#a7afb2', '#c4cbca', '#dbe0dc', '#ecefe9', '#f6f8f2', '#fdfef9'];
  const BLUE = ['#0d2a4e', '#17457a', '#245f9c', '#3878ba', '#5391cf'];
  const TEAK = ['#3f2814', '#54351d', '#6c4626', '#855a31', '#9c6e40', '#b28553'];
  const GLAS = ['#05090c', '#0a1218', '#101a22', '#17242e', '#20323f'];
  const STEEL = ['#3a4148', '#565f66', '#7a858c', '#9fabb1', '#c3ced2', '#e6edee'];
  const IRON = ['#0d1013', '#161a20', '#222931', '#323b45'];
  const KEY = '#0d1418';
  // the rooms' own finishes (pass 2): carpet, leather, the joinery's teak, stone tops, the white linings, glass seen from inside
  const CARPET = ['#6f685d', '#847c70', '#999183', '#ada596', '#c0b9a9', '#d1cbbc'], LEATH = ['#6e5f4c', '#8a7a62', '#a69378', '#c0ac8d', '#d6c4a4', '#e8d9bd'];
  const TEAKG = ['#2e1d10', '#432b16', '#5a3a1e', '#714a27', '#8a5c31', '#a4703c'], STONE = ['#8d8d86', '#a5a59d', '#bcbcb3', '#d0d0c7', '#e1e1d8', '#eeeee6'];
  const WHITEG = ['#9aa09c', '#b4b9b3', '#ccd0c9', '#dee1da', '#eceee8', '#f7f8f3'], GLASSD = ['#7d9ea6', '#a1c2c6', '#c2dcdd', '#d8ebec', '#e8f4f4', '#f4fafa'];
  const NSKID = mkRamp(6, 0.50, 0.86, 0.008, 95), RED = mkRamp(5, 0.34, 0.64, 0.17, 27), WATER = mkRamp(5, 0.36, 0.70, 0.07, 215);
  const PAINTS = [
    { id: 'champagne', label: 'CHAMPAGNE', note: 'Champagne topsides and main-deck house, white upper decks, a royal-blue boot stripe, black bottom.',
      top: [0.60, 0.94, 0.028, 80], literal: { boot: BOOT, stripe: BLUE, house: CREAM }, mainHouse: 'top' },
    { id: 'white', label: 'WHITE', note: 'White topsides and house, navy boot stripe.', literal: { top: HULLW, boot: BOOT, house: CREAM }, stripe: [0.28, 0.52, 0.090, 262], mainHouse: 'house' },
    { id: 'navy', label: 'NAVY', note: 'Navy topsides, white house, a cream boot stripe.', top: [0.22, 0.50, 0.086, 264], boot: [0.14, 0.27, 0.042, 264], stripe: [0.70, 0.95, 0.030, 85], literal: { house: CREAM }, mainHouse: 'house' },
    { id: 'expedition', label: 'EXPEDITION GREY', note: 'Warm-grey topsides, white house, a red boot stripe.', top: [0.40, 0.72, 0.012, 70], boot: [0.13, 0.26, 0.010, 250], stripe: [0.40, 0.62, 0.150, 28], literal: { house: CREAM }, mainHouse: 'house' },
  ];
  const PAINT_BY = {}; PAINTS.forEach(p => { PAINT_BY[p.id] = p; });
  const _rc = {};
  function paintRamps(id) {
    const p = PAINT_BY[id] || PAINT_BY.champagne; if (_rc[p.id]) return _rc[p.id];
    const lit = p.literal || {}, R = {};
    R.top = lit.top || mkRamp(7, p.top[0], p.top[1], p.top[2], p.top[3]);
    R.boot = lit.boot || mkRamp(5, p.boot[0], p.boot[1], p.boot[2], p.boot[3]);
    R.stripe = lit.stripe || mkRamp(5, p.stripe[0], p.stripe[1], p.stripe[2], p.stripe[3]);
    R.house = lit.house || CREAM; R.mainHouse = p.mainHouse === 'top' ? R.top : R.house;
    return (_rc[p.id] = R);
  }
  const LVRAMP = { hull: mkRamp(5, 0.36, 0.74, 0.06, 250), main_deck: mkRamp(5, 0.40, 0.80, 0.11, 55), house: mkRamp(5, 0.40, 0.80, 0.14, 20), bridge: mkRamp(5, 0.40, 0.80, 0.11, 145),
    below: mkRamp(5, 0.36, 0.74, 0.10, 330), rigging: mkRamp(5, 0.40, 0.80, 0.06, 200), foredeck: mkRamp(5, 0.40, 0.82, 0.11, 95), top: mkRamp(5, 0.40, 0.80, 0.11, 300) };
  // matsFor(scheme, tenderPaint): the paint table (hull-layout spec §5) { scheme, MATS, RINDEX, ramps, M, roomMats }. scheme
  // is a PAINTS id, or a state { scheme | paint, tenderPaint }; none, or an unknown id, is 'champagne'. Every paint dithers
  // (dith 1): _paint dithers every face between its ramp's steps. ramps are MATS' ramps deduplicated by content, M indexes
  // them, and roomMats names the room's own finishes
  const _mc = {}, ROOM_MATS = ['carpet', 'leath', 'teakg', 'stone', 'whiteg', 'glassd'];
  function matsFor(id, tenderPaint) {
    if (id && typeof id === 'object') { if (tenderPaint == null) tenderPaint = id.tenderPaint; id = id.scheme != null ? id.scheme : id.paint; }
    const scheme = PAINT_BY[id] ? id : 'champagne', key = scheme + '|' + (tenderPaint || 'grey'); if (_mc[key]) return _mc[key];
    const R = paintRamps(scheme), TR = root.YachtTenderIso ? root.YachtTenderIso.paintRamps(tenderPaint || 'grey').tube : mkRamp(6, 0.34, 0.72, 0.010, 250);
    const MATS = { hull: { ramp: R.top, off: 0 }, boot: { ramp: R.boot, off: 0 }, stripe: { ramp: R.stripe, off: 0 }, cream: { ramp: R.house, off: 0 }, mainhouse: { ramp: R.mainHouse, off: 0 },
      teak: { ramp: TEAK, off: 0 }, deck: { ramp: NSKID, off: 0 }, glas: { ramp: GLAS, off: 0 }, steel: { ramp: STEEL, off: 0 }, iron: { ramp: IRON, off: 0 },
      blk: { ramp: R.boot, off: -1 }, dark: { ramp: R.boot, off: -2 }, tube: { ramp: TR, off: 0 }, red: { ramp: RED, off: 0 }, water: { ramp: WATER, off: 0 },
      carpet: { ramp: CARPET, off: 0 }, leath: { ramp: LEATH, off: 0 }, teakg: { ramp: TEAKG, off: 0 }, stone: { ramp: STONE, off: 0 }, whiteg: { ramp: WHITEG, off: 0 }, glassd: { ramp: GLASSD, off: 0 } };
    for (const k in LVRAMP) MATS['lv_' + k] = { ramp: LVRAMP[k], off: 0 };
    for (const k in MATS) MATS[k].dith = 1;
    const RINDEX = {}; [CARPET, LEATH, TEAKG, STONE, WHITEG, GLASSD, R.top, R.boot, R.stripe, R.house, TEAK, NSKID, GLAS, STEEL, IRON, TR, RED, WATER].concat(Object.values(LVRAMP)).forEach(r => r.forEach((c, i) => { RINDEX[c] = { r, i }; }));
    const ramps = [], at = {}, M = {};
    for (const k in MATS) { const r = MATS[k].ramp, s = r.join(','); if (at[s] == null) { at[s] = ramps.length; ramps.push(r.slice()); } M[k] = { ramp: at[s], off: MATS[k].off, dith: MATS[k].dith }; }
    return (_mc[key] = { scheme, MATS, RINDEX, ramps, M, roomMats: ROOM_MATS.slice() });
  }

  // ================================= RASTERISER (the fleet's locked recipe) =================================
  const GAIN = 3.0, BIAS = 2.7, EDGE = 0.30;             // EDGE: the depth step (m) whose far side the outline darkens
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
    const GP = opts._ghost || null;                               // a cutaway's ghost round the player, on the ghost roles
    for (const f of faces) {
      const rv = f.v.map(([x, y, z]) => projVert(x, y, z, B, G)), g = GP && GHOST_RO.has(f.ro) ? GP : null;
      const n = normal(rv[0], rv[1], rv[2]);
      let sh = shadeOf(n, B.se, B.ce); if (sh < 0 && ((f.b || 0) <= -1)) sh = shadeOf([-n[0], -n[1], -n[2]], B.se, B.ce) * 0.9;
      const fidx = sh * GAIN + BIAS + (f.b || 0), M = MATS[f.mat] || MATS.hull;
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
          if (g && d < g.dP) { const ex = (px - g.cx) * g.irx, ey = (py - g.cy) * g.iry, e = ex * ex + ey * ey; if (e < 1) { const r = Math.sqrt(e); if (r < g.core || (r - g.core) * g.icore < BAYER[x & 3][y & 3]) continue; } }
          if (deff < zbuf[i]) { zbuf[i] = deff; dep[i] = d; const base = Math.floor(fidx); const idx = base + ((fidx - base) > BAYER[x & 3][y & 3] ? 1 : 0) + M.off;
            col[i] = M.ramp[Math.max(0, Math.min(M.ramp.length - 1, idx))]; }
        }
      }
    }
    const out = col.slice();
    for (let y = 0; y < PH; y++) for (let x = 0; x < PW; x++) { const i = y * PW + x; if (!col[i]) continue;
      for (const [dx, dy] of [[1, 0], [0, 1]]) { const nx = x + dx, ny = y + dy; if (nx >= PW || ny >= PH) continue; const j = ny * PW + nx; if (!col[j]) continue;
        if (Math.abs(dep[i] - dep[j]) > EDGE) { const far = dep[i] > dep[j] ? i : j, e = RINDEX[col[far]]; if (e && e.i > 0) out[far] = e.r[Math.max(0, e.i - 2)]; } } }
    for (let y = 0; y < PH; y++) for (let x = 0; x < PW; x++) { const i = y * PW + x; if (out[i]) continue; let touch = false;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const nx = x + dx, ny = y + dy; if (nx >= 0 && nx < PW && ny >= 0 && ny < PH && col[ny * PW + nx]) { touch = true; break; } }
      if (touch) { out[i] = KEY; dep[i] = Infinity; } }
    out.dep = dep; return out;
  }
  function _toRGBA(out, PW, PH) { const rgba = new Uint8ClampedArray(PW * PH * 4); if (out.dep) rgba.dep = out.dep;
    for (let i = 0; i < PW * PH; i++) { const c = out[i]; if (!c) { rgba[i * 4 + 3] = 0; continue; }
      rgba[i * 4] = parseInt(c.slice(1, 3), 16); rgba[i * 4 + 1] = parseInt(c.slice(3, 5), 16); rgba[i * 4 + 2] = parseInt(c.slice(5, 7), 16); rgba[i * 4 + 3] = 255; }
    return rgba; }

  // ================================= MATH =================================
  const lerp = (a, b, t) => a + (b - a) * t, clamp = (v, a, b) => v < a ? a : (v > b ? b : v), clamp01 = (t) => clamp(t, 0, 1);
  const v_sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]], v_add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]], v_mul = (a, s) => [a[0] * s, a[1] * s, a[2] * s];
  const v_norm = (a) => { const m = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / m, a[1] / m, a[2] / m]; }, v_cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const r3 = (v) => +(+v).toFixed(3);
  function mono(pts) {
    pts = pts.slice().sort((a, b) => a[0] - b[0]); const n = pts.length, xs = pts.map(p => p[0]), ys = pts.map(p => p[1]); if (n === 1) return () => ys[0];
    const d = []; for (let i = 0; i < n - 1; i++) d.push((ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]));
    const m = [d[0]]; for (let i = 1; i < n - 1; i++) { if (d[i - 1] * d[i] <= 0) m.push(0); else { const h0 = xs[i] - xs[i - 1], h1 = xs[i + 1] - xs[i], w1 = 2 * h1 + h0, w2 = h1 + 2 * h0; m.push((w1 + w2) / (w1 / d[i - 1] + w2 / d[i])); } } m.push(d[n - 2]);
    return function (x) { if (x <= xs[0]) return ys[0] + m[0] * (x - xs[0]); if (x >= xs[n - 1]) return ys[n - 1] + m[n - 1] * (x - xs[n - 1]);
      let i = 0; while (i < n - 2 && x > xs[i + 1]) i++; const h = xs[i + 1] - xs[i], t = (x - xs[i]) / h, t2 = t * t, t3 = t2 * t;
      return (2 * t3 - 3 * t2 + 1) * ys[i] + (t3 - 2 * t2 + t) * h * m[i] + (-2 * t3 + 3 * t2) * ys[i + 1] + (t3 - t2) * h * m[i + 1]; };
  }
  function newell(v) { let nx = 0, ny = 0, nz = 0; for (let i = 0; i < v.length; i++) { const a = v[i], b = v[(i + 1) % v.length]; nx += (a[1] - b[1]) * (a[2] + b[2]); ny += (a[2] - b[2]) * (a[0] + b[0]); nz += (a[0] - b[0]) * (a[1] + b[1]); } return [nx, ny, nz]; }
  function splitBy(v, g) { const neg = [], pos = [], n = v.length;
    for (let i = 0; i < n; i++) { const a = v[i], b = v[(i + 1) % n], ga = g(a), gb = g(b); if (ga <= 0) neg.push(a); else pos.push(a);
      if ((ga <= 0 && gb > 0) || (ga > 0 && gb <= 0)) { const t = ga / (ga - gb); const p = [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t]; neg.push(p); pos.push(p); } }
    return [neg.length >= 3 ? neg : null, pos.length >= 3 ? pos : null]; }
  function punch(v, r) { const out = []; let rest = v;
    for (const g of [(p) => p[0] - r.x0, (p) => r.x1 - p[0], (p) => p[1] - r.y0, (p) => r.y1 - p[1]]) { if (!rest) break; const s = splitBy(rest, g); if (s[0]) out.push(s[0]); rest = s[1]; }
    return out; }

  // ================================= PRINCIPAL DIMENSIONS =================================
  const L = 36.75, yT = -L / 2, yB = L / 2, DWL = 2.74, ZW = DWL + 0.25;
  const ZP = 3.40, ZM = 4.30, Z01 = 7.00, Z01U = 6.85, ZF = 9.70, ZFU = 9.55, ZHT = 12.00, ZBELOW = 1.90;
  const Y_PLAT = -17.35, Y_DECK_AFT = -16.15, Y_COAM = -16.95, XC = 2.00, XSH = 3.00, Y_SHOULDER = -15.60, Y_SALON = -10.80, HX_SAL = 3.02, Y_STEP = -1.00;
  const PST = { rise: 0.18, going: 0.30 };   // the swim-platform stairs in the channels (5 risers, Y_PLAT up to Y_DECK_AFT); the stern void's lid keeps under them
  const GATE = { y0: -6.50, y1: -5.70 }, LADDER = { half: 0.24, rung: 0.24, rungs: Math.ceil((ZP - DWL + 0.6) / 0.24), y: yT - 0.04 };   // step 0: side-deck gates, swim ladder
  // sheer (the top edge of the shell: the bulwark cap aft of the step, the foredeck edge forward of it)
  const fSz = mono([[yT, 3.40], [-17.40, 3.42], [-16.90, 3.75], [-16.40, 4.35], [-15.90, 4.80], [-15.40, 5.08], [-14.60, 5.30], [-13.80, 5.35], [-1.60, 5.35], [-1.20, 5.48],
    [-0.80, 5.90], [-0.40, 6.30], [0.00, 6.46], [0.90, 6.55], [4.90, 6.62], [9.80, 6.81], [14.00, 6.98], [yB, 7.20]]);
  const fSx0 = mono([[yT, 3.55], [-17.0, 3.80], [-15.0, 4.00], [-12.0, 4.15], [-8.0, 4.24], [-4.0, 4.255], [0, 4.25], [4.0, 4.16], [7.9, 4.02], [10.5, 3.72], [12.9, 3.35],
    [14.6, 2.80], [15.8, 2.15], [16.9, 1.40], [17.8, 0.62], [18.2, 0.20], [yB, 0.0]]);
  const fSx = (y) => Math.max(0, fSx0(Math.min(y, yB)));
  const stemY = mono([[0.95, 13.30], [1.40, 14.35], [1.90, 14.80], [2.40, 14.92], [2.99, 14.95], [3.60, 15.30], [4.50, 16.00], [5.50, 16.85], [6.40, 17.65], [7.20, yB]]);
  const Y_KEND = stemY(0.95), Y_WEND = stemY(ZW);
  const fKz = mono([[yT, 2.55], [-16.5, 1.85], [-14.5, 1.05], [-12.5, 0.45], [-10, 0.10], [-7, 0.0], [4, 0.0], [8, 0.06], [10.5, 0.22], [12.2, 0.50], [Y_KEND, 0.95]]);
  const fWx0 = mono([[yT, 3.35], [-16, 3.75], [-12, 4.05], [-6, 4.15], [0, 4.13], [4, 4.00], [7, 3.65], [9.5, 3.05], [11.5, 2.30], [13.0, 1.50], [14.0, 0.80], [Y_WEND, 0.0]]);
  const fWx = (y) => Math.max(0, fWx0(Math.min(y, Y_WEND)));
  const fN = mono([[yT, 2.4], [-12, 3.0], [0, 3.2], [8, 2.8], [12, 2.3], [Y_WEND, 2.0]]);
  const fP = mono([[yT, 1.0], [0, 1.05], [6, 1.25], [10, 1.55], [14, 1.85], [yB, 2.0]]);

  // ---- the shell: rows keel → sheer, each ending on the stem line at its own height ----
  const TH = [0, 14, 28, 42, 56, 70, 82, 90].map(a => a * DEG), PH = [0.20, 0.42, 0.64, 0.84, 1.0], NR = TH.length + PH.length;
  const nEnd = fN(Y_WEND), zkE = 0.95, zsE = fSz(yB);
  const ROWZ1 = TH.map(t => ZW - (ZW - zkE) * Math.pow(Math.cos(t), 2 / nEnd)).concat(PH.map(p => ZW + p * (zsE - ZW)));
  const YEND = ROWZ1.map((z, r) => r === 0 ? Y_KEND : (r === NR - 1 ? yB : Math.min(yB, stemY(z))));
  function section(s) {
    const yr = (r) => yT + s * (YEND[r] - yT), yW = yT + s * (Y_WEND - yT), yS = yT + s * (yB - yT);
    const zk = fKz(yr(0)), Bw = fWx(yW), Bs = Math.max(Bw, fSx(yS)), zs = fSz(yS), n = fN(yW), p = fP(yS), P = [];
    for (let r = 0; r < TH.length; r++) { const t = TH[r]; P.push([Bw * Math.pow(Math.sin(t), 2 / n), yr(r), ZW - (ZW - zk) * Math.pow(Math.cos(t), 2 / n)]); }
    for (let q = 0; q < PH.length; q++) { const r = TH.length + q, f = PH[q]; P.push([Bw + (Bs - Bw) * Math.pow(f, p), yr(r), ZW + f * (zs - ZW)]); }
    return P;
  }
  const SSET = new Set(); for (let i = 0; i <= 52; i++) SSET.add(+(0.5 - 0.5 * Math.cos(Math.PI * i / 52)).toFixed(5));
  for (let y = -17.6; y <= -14.4; y += 0.30) SSET.add(+((y - yT) / L).toFixed(5));
  for (let y = -2.2; y <= 1.0; y += 0.20) SSET.add(+((y - yT) / L).toFixed(5));
  const SS = [...SSET].filter(s => s >= 0 && s <= 1).sort((a, b) => a - b), SEC = SS.map(section), NSEC = SEC.length;

  // mesh-true half-breadth: the shell quads sliced at y = const, x(z) tabulated
  const HDY = 0.05, HDZ = 0.04, HNY = Math.ceil(L / HDY) + 1, HNZ = Math.ceil(7.8 / HDZ) + 1, HX = new Float32Array(HNY * HNZ);
  for (let iy = 0; iy < HNY; iy++) { const yc = Math.min(yB - 1e-4, Math.max(yT + 1e-4, yT + iy * HDY)), sg = [];
    for (let i = 0; i + 1 < NSEC; i++) { const A = SEC[i], B2 = SEC[i + 1]; let y0 = 1e9, y1 = -1e9; for (let k = 0; k < NR; k++) { y0 = Math.min(y0, A[k][1], B2[k][1]); y1 = Math.max(y1, A[k][1], B2[k][1]); } if (y0 > yc || y1 < yc) continue;
      for (let k = 0; k + 1 < NR; k++) { const q = [A[k], B2[k], B2[k + 1], A[k + 1]], pts = [];
        for (let e = 0; e < 4; e++) { const a = q[e], b = q[(e + 1) % 4], da = a[1] - yc, db = b[1] - yc; if ((da <= 0 && db > 0) || (da > 0 && db <= 0)) { const t = da / (da - db); pts.push([a[0] + (b[0] - a[0]) * t, a[2] + (b[2] - a[2]) * t]); } }
        if (pts.length >= 2) sg.push(pts[0], pts[1]); } }
    let lo = -1, hi = -1;
    for (let iz = 0; iz < HNZ; iz++) { const zc = iz * HDZ; let best = -1;
      for (let s = 0; s < sg.length; s += 2) { const p = sg[s], r = sg[s + 1]; if ((p[1] - zc) * (r[1] - zc) > 0) continue; const dz = r[1] - p[1];
        const x = Math.abs(dz) < 1e-9 ? Math.min(p[0], r[0]) : p[0] + (r[0] - p[0]) * (zc - p[1]) / dz; if (best < 0 || x < best) best = x; }
      HX[iy * HNZ + iz] = best; if (best >= 0) { if (lo < 0) lo = iz; hi = iz; } }
    for (let iz = 0; iz < HNZ; iz++) { const k = iy * HNZ + iz; if (lo < 0 || iz < lo) HX[k] = 0; else if (iz > hi) HX[k] = HX[iy * HNZ + hi]; else if (HX[k] < 0) HX[k] = HX[k - 1]; } }
  function halfAtZ(y, z) { if (y < yT || y > yB) return 0;
    const fy = (y - yT) / HDY, iy = Math.min(HNY - 2, Math.max(0, Math.floor(fy))), ty = clamp01(fy - iy), fz = clamp(z / HDZ, 0, HNZ - 1.001), iz = Math.floor(fz), tz = fz - iz;
    const a = HX[iy * HNZ + iz], b = HX[iy * HNZ + iz + 1], c = HX[(iy + 1) * HNZ + iz], d = HX[(iy + 1) * HNZ + iz + 1];
    return Math.max(0, Math.min(lerp(a, b, tz), lerp(c, d, tz))); }
  const xin = (y) => Math.max(0.3, halfAtZ(y, ZM + 0.06) - 0.13);               // the main-deck bulwark's inner face

  // ================================= FACE EMISSION =================================
  // ADR 0048's ids, as sportFisherIsoRig3.js has them: every face carries lv, ro and it
  const LV = { hull: 0, main_deck: 1, house: 2, bridge: 3, below: 4, rigging: 5, foredeck: 6, top: 7 };
  const RO = { shell: 0, wall: 1, roof: 2, lining: 3, part: 4, floor: 5, fit: 6, tall: 7, trim: 8, rig: 9 };
  const GHOST_ROLES = ['shell', 'wall', 'roof', 'lining', 'part', 'tall', 'trim'], LV_NAME = Object.keys(LV), RO_NAME = Object.keys(RO);
  const F = [], IF = []; let TG = F, cLV = LV.hull, cRO = RO.shell, cIT = false;
  const tagOf = (T, k, n) => { if (T[n] == null) throw new Error('ExpeditionYacht120Iso: no ' + k + ' id ' + n); return T[n]; };
  const at = (lv, ro) => { cLV = tagOf(LV, 'lv', lv); if (ro) cRO = tagOf(RO, 'ro', ro); }, role = (ro) => { cRO = tagOf(RO, 'ro', ro); };
  // outside, leather is the cream vinyl it was in pass 1; the rooms keep their leather
  const put = (f) => { if (f.mat === 'leath' && !cIT) { f.mat = 'cream'; f.b = (f.b || 0) - 0.25; } f.lv = cLV; f.ro = cRO; f.it = cIT; TG.push(f); };
  function Q(v, out, mat, b, db) { const n = newell(v); if (Math.hypot(n[0], n[1], n[2]) < 1e-9) return; if (n[0] * out[0] + n[1] * out[1] + n[2] * out[2] < 0) v = v.slice().reverse(); put({ v, mat, b: b || 0, db: db || 0 }); }
  function boxQ(x0, x1, y0, y1, z0, z1, mat, b, db, topMat, skip) { const s = skip || '';
    if (s.indexOf('t') < 0) Q([[x0, y0, z1], [x1, y0, z1], [x1, y1, z1], [x0, y1, z1]], [0, 0, 1], topMat || mat, b, db);
    if (s.indexOf('b') < 0) Q([[x0, y0, z0], [x1, y0, z0], [x1, y1, z0], [x0, y1, z0]], [0, 0, -1], mat, (b || 0) - 1.2, db);
    if (s.indexOf('f') < 0) Q([[x0, y1, z0], [x1, y1, z0], [x1, y1, z1], [x0, y1, z1]], [0, 1, 0], mat, b, db);
    if (s.indexOf('a') < 0) Q([[x0, y0, z0], [x1, y0, z0], [x1, y0, z1], [x0, y0, z1]], [0, -1, 0], mat, b, db);
    if (s.indexOf('r') < 0) Q([[x1, y0, z0], [x1, y1, z0], [x1, y1, z1], [x1, y0, z1]], [1, 0, 0], mat, b, db);
    if (s.indexOf('l') < 0) Q([[x0, y0, z0], [x0, y1, z0], [x0, y1, z1], [x0, y0, z1]], [-1, 0, 0], mat, b, db); }
  function tubeQ(A, B2, r0, r1, mat, b) { const ax = v_norm(v_sub(B2, A)); let up = [0, 0, 1]; if (Math.abs(ax[2]) > 0.9) up = [0, 1, 0];
    const u = v_norm(v_cross(ax, up)), w = v_cross(u, ax);
    const ring = (P, r) => [0, 1, 2, 3].map(k => { const a = k * Math.PI / 2 + Math.PI / 4; return v_add(P, v_add(v_mul(u, Math.cos(a) * r), v_mul(w, Math.sin(a) * r))); });
    const R0 = ring(A, r0), R1 = ring(B2, r1 == null ? r0 : r1), mid = v_mul(v_add(A, B2), 0.5);
    for (let k = 0; k < 4; k++) { const k2 = (k + 1) % 4, q = [R0[k], R0[k2], R1[k2], R1[k]], c = v_mul(v_add(v_add(q[0], q[1]), v_add(q[2], q[3])), 0.25); Q(q, v_sub(c, mid), mat, b, -0.15); } }
  function cylZ(cx, cy, z0, z1, r, n, mat, b, topMat, r1) { const R1 = r1 == null ? r : r1, P = (rr, a, z) => [cx + rr * Math.cos(a), cy + rr * Math.sin(a), z];
    for (let k = 0; k < n; k++) { const a0 = k / n * 2 * Math.PI, a1 = (k + 1) / n * 2 * Math.PI, am = (a0 + a1) / 2; Q([P(r, a0, z0), P(r, a1, z0), P(R1, a1, z1), P(R1, a0, z1)], [Math.cos(am), Math.sin(am), 0], mat, b, 0); }
    if (topMat !== null) { const top = []; for (let k = 0; k < n; k++) top.push(P(R1, k / n * 2 * Math.PI, z1)); Q(top, [0, 0, 1], topMat || mat, (b || 0) + 0.2, 0); } }
  function domeZ(cx, cy, z0, r, hCyl, n, mat, b) { cylZ(cx, cy, z0, z0 + hCyl, r, n, mat, b, null);
    const rings = [[r, z0 + hCyl], [r * 0.87, z0 + hCyl + r * 0.5], [r * 0.5, z0 + hCyl + r * 0.87], [0.001, z0 + hCyl + r]];
    for (let i = 0; i + 1 < rings.length; i++) { const [ra, za] = rings[i], [rb, zb] = rings[i + 1];
      for (let k = 0; k < n; k++) { const a0 = k / n * 2 * Math.PI, a1 = (k + 1) / n * 2 * Math.PI, am = (a0 + a1) / 2, P = (rr, a, z) => [cx + rr * Math.cos(a), cy + rr * Math.sin(a), z];
        Q([P(ra, a0, za), P(ra, a1, za), P(rb, a1, zb), P(rb, a0, zb)], [Math.cos(am) * (1 - i * 0.3), Math.sin(am) * (1 - i * 0.3), 0.3 + i * 0.35], mat, (b || 0) + 0.1 * i, 0); } } }
  function prismQ(poly, z0, z1, mat, b, topMat, db) { const n = poly.length;
    Q(poly.map(p => [p[0], p[1], z1]), [0, 0, 1], topMat || mat, b, db);
    let cx = 0, cy = 0; poly.forEach(p => { cx += p[0] / n; cy += p[1] / n; });
    for (let i = 0; i < n; i++) { const a = poly[i], c = poly[(i + 1) % n]; Q([[a[0], a[1], z0], [c[0], c[1], z0], [c[0], c[1], z1], [a[0], a[1], z1]], [(a[0] + c[0]) / 2 - cx, (a[1] + c[1]) / 2 - cy, 0], mat, b, db); } }
  function bands(v, out, BD, bb) { let rest = v;
    for (const [zt, mat, b, db] of BD) { if (!rest) break; if (zt === Infinity) { Q(rest, out, mat, (b || 0) + (bb || 0), db); break; }
      const [lo, hi] = splitBy(rest, (p) => p[2] - zt); if (lo) Q(lo, out, mat, (b || 0) + (bb || 0), db); rest = hi; } }
  // a tight hole (the rooms' stairwells) cuts only the pieces it overlaps; the older holes cut every piece their lines cross
  const hits = (p, r) => { let a = Infinity, c = -Infinity, d = Infinity, e = -Infinity; for (const q of p) { a = Math.min(a, q[0]); c = Math.max(c, q[0]); d = Math.min(d, q[1]); e = Math.max(e, q[1]); } return a < r.x1 && c > r.x0 && d < r.y1 && e > r.y0; };
  function putH(v, out, mat, b, db, holes) { let parts = [v]; if (holes && holes.length) for (const r of holes) { const nx = []; parts.forEach(p => (r.tight && !hits(p, r) ? [p] : punch(p, r)).forEach(q => nx.push(q))); parts = nx; } parts.forEach(p => Q(p, out, mat, b, db)); }
  function hullPatch(y0, y1, z0, z1, mat, b, off, ends, db) {
    const N = 10, e = (t) => { const k = Math.min(1, Math.min(t, 1 - t) / (ends || 0.16)); return Math.sqrt(Math.max(0, 1 - (1 - k) * (1 - k))); };
    for (let i = 0; i < N; i++) { const ta = i / N, tb = (i + 1) / N, ya = lerp(y0, y1, ta), yb = lerp(y0, y1, tb), zm = (z0 + z1) / 2, ha = (z1 - z0) / 2 * e(ta), hb = (z1 - z0) / 2 * e(tb);
      for (const sd of [-1, 1]) { const P = (y, z) => [sd * (halfAtZ(y, z) + off), y, z]; Q([P(ya, zm - ha), P(yb, zm - hb), P(yb, zm + hb), P(ya, zm + ha)], [sd, 0, 0], mat, b, db == null ? 0.05 : db); } } }

  // ---- plan outlines + ring lofts (house tiers and slabs), as the fleet builds them ----
  const NN = 14, NSD = 18, NCR = 5, NAF = 4, NC = NN + 1 + NSD + NCR, NP = NC + NAF;
  function outline(R, o) {
    const e = o.e || 2.2, rA = Math.min(o.rA || 0.5, R.W * 0.5), P = [];
    const yS1 = R.yA + rA, yN = Math.max(R.yF - R.N, yS1 + 0.02), N = R.yF - yN;
    for (let i = 0; i <= NN; i++) { const th = (i / NN) * Math.PI / 2; P.push([R.W * Math.pow(Math.sin(th), 2 / e), yN + N * Math.pow(Math.cos(th), 2 / e)]); }
    for (let i = 1; i <= NSD; i++) P.push([R.W, lerp(yN, yS1, i / NSD)]);
    for (let i = 1; i <= NCR; i++) { const a = (i / NCR) * Math.PI / 2; P.push([R.W - rA + rA * Math.cos(a), R.yA + rA - rA * Math.sin(a)]); }
    for (let i = 1; i <= NAF; i++) P.push([(R.W - rA) * (1 - i / NAF), R.yA]);
    if (o.clamp) for (const p of P) p[0] = Math.min(p[0], Math.max(0.02, o.clamp(p[1])));
    return P;
  }
  const pnorm = (P, j) => { const a = P[Math.max(0, j - 1)], b = P[Math.min(P.length - 1, j + 1)]; const tx = b[0] - a[0], ty = b[1] - a[1], m = Math.hypot(tx, ty) || 1; return [-ty / m, tx / m]; };
  function ringTable(tbl) { const fs = [1, 2, 3, 4].map(c => mono(tbl.map(r => [r[0], r[c]]))); return (z) => ({ z, yF: fs[0](z), yA: fs[1](z), W: fs[2](z), N: fs[3](z) }); }
  function ringsFrom(tbl, zs, o) { const f = ringTable(tbl); return zs.map(z => ({ z, P: outline(f(z), o) })); }
  function ringAt(RG, z) { let k = 0; while (k < RG.length - 2 && z > RG[k + 1].z) k++; const A = RG[k], B2 = RG[k + 1], t = clamp01((z - A.z) / (B2.z - A.z));
    return { z, P: A.P.map((p, j) => [lerp(p[0], B2.P[j][0], t), lerp(p[1], B2.P[j][1], t)]) }; }
  function hwAt(P, y) { if (y > P[0][1]) return 0; for (let j = 0; j + 1 < NC; j++) { const a = P[j], b = P[j + 1]; if ((a[1] - y) * (b[1] - y) <= 0) { const t = a[1] === b[1] ? 0 : (a[1] - y) / (a[1] - b[1]); return lerp(a[0], b[0], t); } } return 0; }
  function loftWalls(RG, mat, b) {
    for (let k = 0; k + 1 < RG.length; k++) { const A = RG[k], B2 = RG[k + 1], zf = k / (RG.length - 1);
      for (let j = 0; j + 1 < NC; j++) for (const sd of [-1, 1]) { const a0 = A.P[j], a1 = A.P[j + 1], b0 = B2.P[j], b1 = B2.P[j + 1], n0 = pnorm(A.P, j), n1 = pnorm(A.P, j + 1), nx = (n0[0] + n1[0]) / 2, ny = (n0[1] + n1[1]) / 2;
        Q([[sd * a0[0], a0[1], A.z], [sd * a1[0], a1[1], A.z], [sd * b1[0], b1[1], B2.z], [sd * b0[0], b0[1], B2.z]], [sd * nx, ny, 0.04], mat, (b || 0) + 0.02 + 0.40 * Math.max(0, ny) - 0.45 * Math.max(0, -ny) + 0.12 * zf, 0); } } }
  function aftWall(RG, mat, b) { for (let k = 0; k + 1 < RG.length; k++) { const A = RG[k], B2 = RG[k + 1], EA = A.P[NC - 1][0], EB = B2.P[NC - 1][0], ya = A.P[NC][1], yb = B2.P[NC][1];
    Q([[-EA, ya, A.z], [EA, ya, A.z], [EB, yb, B2.z], [-EB, yb, B2.z]], [0, -1, 0.05], mat, (b || 0) - 0.45, 0); } }
  function clipSeg(pa, pb, y0, y1) { const ya = pa[1], yb = pb[1]; if (Math.abs(yb - ya) < 1e-9) return (ya >= y0 && ya <= y1) ? [0, 1] : null;
    let t0 = (y0 - ya) / (yb - ya), t1 = (y1 - ya) / (yb - ya); if (t0 > t1) { const t = t0; t0 = t1; t1 = t; } t0 = Math.max(0, t0); t1 = Math.min(1, t1); return t1 - t0 > 1e-4 ? [t0, t1] : null; }
  // a patch laid on a ring loft's nose + sides between two heights (windows, doors, glazing bands)
  function patchOn(RG, y0, y1, z0, z1, mat, b, off, db) {
    const nz = Math.max(1, Math.ceil((z1 - z0) / 0.30));
    for (let s = 0; s < nz; s++) { const za = lerp(z0, z1, s / nz), zb = lerp(z0, z1, (s + 1) / nz), A = ringAt(RG, za).P, B2 = ringAt(RG, zb).P;
      for (let j = 0; j + 1 < NC; j++) { const ca = clipSeg(A[j], A[j + 1], y0, y1), cb = clipSeg(B2[j], B2[j + 1], y0, y1); if (!ca || !cb) continue;
        const n = pnorm(A, j), P = (R, t, z, sd) => { const p = [lerp(R[j][0], R[j + 1][0], t), lerp(R[j][1], R[j + 1][1], t)]; return [sd * (p[0] + off * n[0]), p[1] + off * n[1], z]; };
        for (const sd of [-1, 1]) Q([P(A, ca[0], za, sd), P(A, ca[1], za, sd), P(B2, cb[1], zb, sd), P(B2, cb[0], zb, sd)], [sd * n[0], n[1], 0.05], mat, b, db); } } }
  function winOn(RG, y0, y1, z0, z1, kind) { const fr = kind === 'door' ? 0.04 : 0.05;
    patchOn(RG, y0 - fr, y1 + fr, z0 - fr, z1 + fr, 'iron', kind === 'door' ? 0.5 : -0.15, 0.018, 0.02);
    if (kind === 'door') patchOn(RG, y0 + 0.12, y1 - 0.12, z1 - 0.75, z1 - 0.15, 'glas', -0.5, 0.032, 0.035);
    else patchOn(RG, y0, y1, z0, z1, 'glas', -0.55, 0.032, 0.035); }
  // planked (or plain) top over an outline, with holes; its underside; its fascia down to zf(y)
  function deckTop(P, z, mat, holes, planks, b0) { const XS = []; const K = planks || 4; for (let k = 0; k <= K; k++) XS.push(k / K);
    for (let j = 0; j + 1 < NP; j++) { const A = P[j], B2 = P[j + 1]; if (Math.abs(A[1] - B2[1]) < 1e-6) continue;
      for (let k = 0; k < K; k++) for (const sd of [-1, 1]) putH([[sd * A[0] * XS[k], A[1], z], [sd * A[0] * XS[k + 1], A[1], z], [sd * B2[0] * XS[k + 1], B2[1], z], [sd * B2[0] * XS[k], B2[1], z]], [0, 0, 1], mat, (b0 || 0) + (planks ? (k % 2 ? -0.35 : -0.05) : 0.3 - 0.08 * k), 0, holes); } }
  function deckUnder(P, z, holes, yMax) { for (let j = 0; j + 1 < NP; j++) { const A = P[j], B2 = P[j + 1]; if (Math.abs(A[1] - B2[1]) < 1e-6) continue; if (Math.min(A[1], B2[1]) > yMax) continue;
    for (const sd of [-1, 1]) putH([[0, A[1], z], [sd * A[0], A[1], z], [sd * B2[0], B2[1], z], [0, B2[1], z]], [0, 0, -1], 'cream', -1.6, 0, holes); } }
  // brk (the y's where zf steps): a segment across one is cut there, and a piece whose lower edge would stand more than 0.005
  // over zf is halved until it follows zf (the 01's comes down to the sheer forward of Y_STEP). Without brk, one quad a segment.
  function fascia(P, zTop, zf, mat, b, brk) { for (let j = 0; j + 1 < NP; j++) { const a = P[j], c = P[j + 1], nn = pnorm(P, j), bb = (b || 0) + 0.15 + 0.35 * Math.max(0, nn[1]) - 0.4 * Math.max(0, -nn[1]);
    const quad = (p, q, z0, z1) => { for (const sd of [-1, 1]) Q([[sd * p[0], p[1], z0], [sd * q[0], q[1], z1], [sd * q[0], q[1], zTop], [sd * p[0], p[1], zTop]], [sd * nn[0], nn[1], 0], mat, bb, 0); };
    if (!brk) { quad(a, c, zf(a[1]), zf(c[1])); continue; }
    const ts = brk.map((y) => (y - a[1]) / (c[1] - a[1])).filter((t) => t > 1e-9 && t < 1 - 1e-9).sort((u, v) => u - v);
    const pt = (t) => t === 0 ? a : t === 1 ? c : [lerp(a[0], c[0], t), lerp(a[1], c[1], t)];
    const zAt = (t, s) => zf(pt(t)[1] + (ts.includes(t) ? s * 1e-7 * Math.sign(c[1] - a[1]) : 0));   // at a break, zf on the piece's side
    const piece = (t0, t1, n) => { const z0 = zAt(t0, 1), z1 = zAt(t1, -1), tm = (t0 + t1) / 2;
      if (n < 8 && (z0 + z1) / 2 - zf(pt(tm)[1]) > 0.005) { piece(t0, tm, n + 1); piece(tm, t1, n + 1); } else quad(pt(t0), pt(t1), z0, z1); };
    const T = [0, ...ts, 1]; for (let k = 0; k + 1 < T.length; k++) piece(T[k], T[k + 1], 0); } }
  function railAlong(pts, z0, h, every, mat) { // pts: [[x,y,zBase]] one side (already signed); stanchions + top + mid rail
    for (let i = 0; i + 1 < pts.length; i++) { const a = pts[i], c = pts[i + 1];
      tubeQ([a[0], a[1], a[2] + h], [c[0], c[1], c[2] + h], 0.026, 0.026, mat || 'steel', 0.3); tubeQ([a[0], a[1], a[2] + h * 0.52], [c[0], c[1], c[2] + h * 0.52], 0.02, 0.02, mat || 'steel', 0.3); }
    pts.forEach((p, i) => { if (i % (every || 3) === 0 || i === pts.length - 1) tubeQ([p[0], p[1], p[2]], [p[0], p[1], p[2] + h], 0.024, 0.024, mat || 'steel', 0.1); }); }
  function openStair(x0, x1, y0, dir, n, rise, going, z0) {   // n risers; treads k = 1…n−1; the n-th riser lands on the upper deck
    const yTop = y0 + dir * (n - 1) * going, zTop = z0 + n * rise;
    for (let k = 1; k < n; k++) { const ya = y0 + dir * (k - 1) * going, yb = ya + dir * (going + 0.03), zt = z0 + k * rise; boxQ(x0 + 0.03, x1 - 0.03, Math.min(ya, yb), Math.max(ya, yb), zt - 0.05, zt, 'teak', -0.1, 0.01); }
    for (const x of [x0, x1]) { Q([[x, y0, z0 + 0.14], [x, yTop, zTop + 0.10], [x, yTop, zTop - 0.24], [x, y0 + dir * 0.40, z0]], [x === x0 ? -1 : 1, 0, 0], 'cream', 0.05, 0.005);
      tubeQ([x, y0, z0 + 0.95], [x, yTop, zTop + 0.95], 0.024, 0.024, 'steel', 0.3); tubeQ([x, y0, z0 + 0.14], [x, y0, z0 + 0.95], 0.022, 0.022, 'steel', 0.1); tubeQ([x, yTop, zTop + 0.1], [x, yTop, zTop + 0.95], 0.022, 0.022, 'steel', 0.1); }
    return { x: [r3(x0), r3(x1)], bottom: [r3((x0 + x1) / 2), r3(y0 - dir * 0.30), r3(z0)], top: [r3((x0 + x1) / 2), r3(yTop + dir * 0.60), r3(zTop)], risers: n, rise, going, width: r3(x1 - x0) }; }
  function holeEdges(H, zTop, zU) { Q([[H.x0, H.y0, zU], [H.x1, H.y0, zU], [H.x1, H.y0, zTop], [H.x0, H.y0, zTop]], [0, 1, 0], 'cream', -0.9, 0); Q([[H.x0, H.y1, zU], [H.x1, H.y1, zU], [H.x1, H.y1, zTop], [H.x0, H.y1, zTop]], [0, -1, 0], 'cream', -1.1, 0);
    Q([[H.x0, H.y0, zU], [H.x0, H.y1, zU], [H.x0, H.y1, zTop], [H.x0, H.y0, zTop]], [1, 0, 0], 'cream', -1.0, 0); Q([[H.x1, H.y0, zU], [H.x1, H.y1, zU], [H.x1, H.y1, zTop], [H.x1, H.y0, zTop]], [-1, 0, 0], 'cream', -1.2, 0); }

  // ================================= DOOR LEAVES (ADR 0048: doorFaces(t)) =================================
  // A door is a rect in its wall's plane: n the wall's normal axis (0: a side wall, the rect in y; 1: an end wall, the rect
  // in x), w the wall on that axis, [u0, u1] × [z0, z1] the opening, o its outward normal in plan. leafCut(doors, fn) splits
  // what fn emits by each door it touches: the parts in the opening (the door's panel, its glass, and the plug of wall behind
  // them, which is the leaf's back) become the leaf; the rest stay where they were. Closed, a leaf is exactly the faces it was
  // cut from. Hinged leaves swing out 90°; sliders stand out 0.07 m, then slide over their fixed panels; the gates fold.
  const LEAVES = [], LEAF = {}, GLEAF = { '-1': [], '1': [] };
  function door(id, n, w, u0, u1, z0, z1, o, pose) { const D = Object.assign({ id, n, w, u0, u1, z0, z1, o, faces: [] }, pose); LEAVES.push(D); LEAF[id] = D; return D; }
  function cleanPoly(v) { const o = []; for (const p of v) { const q = o[o.length - 1]; if (!q || Math.abs(p[0] - q[0]) + Math.abs(p[1] - q[1]) + Math.abs(p[2] - q[2]) > 1e-9) o.push(p); }
    while (o.length > 2) { const a = o[0], q = o[o.length - 1]; if (Math.abs(a[0] - q[0]) + Math.abs(a[1] - q[1]) + Math.abs(a[2] - q[2]) > 1e-9) break; o.pop(); }
    const n = newell(o); return o.length >= 3 && Math.hypot(n[0], n[1], n[2]) > 2e-8 ? o : null; }
  function leafCut(doors, fn) { const s = TG, tmp = []; TG = tmp; fn(); TG = s;
    for (const f of tmp) { let parts = [f.v];
      for (const D of doors) { const u = 1 - D.n, nx = [];
        for (const v of parts) { let c = 0, lo = Infinity, hi = -Infinity, zl = Infinity, zh = -Infinity;
          for (const p of v) { c += p[D.n] / v.length; lo = Math.min(lo, p[u]); hi = Math.max(hi, p[u]); zl = Math.min(zl, p[2]); zh = Math.max(zh, p[2]); }
          if (Math.abs(c - D.w) > 0.12 || hi <= D.u0 + 1e-6 || lo >= D.u1 - 1e-6 || zh <= D.z0 + 1e-6 || zl >= D.z1 - 1e-6) { nx.push(v); continue; }
          const outs = []; let rest = v;
          for (const g of [(p) => p[u] - D.u0, (p) => D.u1 - p[u], (p) => p[2] - D.z0, (p) => D.z1 - p[2]]) { if (!rest) break; const sp = splitBy(rest, g); if (sp[0]) outs.push(sp[0]); rest = sp[1]; }
          rest = rest && cleanPoly(rest); if (!rest) { nx.push(v); continue; }
          D.faces.push(Object.assign({}, f, { v: rest })); for (const q of outs) { const c2 = cleanPoly(q); if (c2) nx.push(c2); } }
        parts = nx; }
      if (parts.length === 1 && parts[0] === f.v) TG.push(f); else for (const v of parts) TG.push(Object.assign({}, f, { v })); } }
  function leafPose(D, t) { if (!(t > 1e-4)) return D.faces; if (D.kind === 'gate') return gatePose(D, t); let M;
    if (D.kind === 'slide') { const k = clamp01(t / 0.15), d = D.dir * t * D.travel, dx = (D.n === 1 ? d : 0) + D.o[0] * D.out * k, dy = (D.n === 0 ? d : 0) + D.o[1] * D.out * k; M = (p) => [p[0] + dx, p[1] + dy, p[2]]; }
    else { const hx = D.n === 0 ? D.w : D.hu, hy = D.n === 0 ? D.hu : D.w, e = Math.sign((D.u0 + D.u1) / 2 - D.hu), fx = D.n === 1 ? e : 0, fy = D.n === 0 ? e : 0;
      const a = Math.sign(fx * D.o[1] - fy * D.o[0]) * t * (D.swing || 90) * DEG, c = Math.cos(a), s = Math.sin(a);
      M = (p) => { const dx = p[0] - hx, dy = p[1] - hy; return [hx + dx * c - dy * s, hy + dx * s + dy * c, p[2]]; }; }
    return D.faces.map(f => Object.assign({}, f, { v: f.v.map(M) })); }
  // the doors with leaves (design().DOORS); the gates' leaves are the bulwark itself, cut in the hull section
  door('gate_port', 0, -4.10, GATE.y0, GATE.y1, ZM, 5.35, [-1, 0], { kind: 'gate', side: -1 }); LEAF.gate_port.faces = GLEAF['-1'];
  door('gate_stbd', 0, 4.10, GATE.y0, GATE.y1, ZM, 5.35, [1, 0], { kind: 'gate', side: 1 }); LEAF.gate_stbd.faces = GLEAF['1'];
  for (const sd of [-1, 1]) { const s = sd < 0 ? 'port' : 'stbd', o = [sd, 0];
    door('salon_aft_' + s, 1, Y_SALON, sd < 0 ? -1.21 : 0, sd < 0 ? 0 : 1.21, ZM + 0.02, 6.66, [0, -1], { kind: 'slide', dir: sd, travel: 1.15, out: 0.07, side: sd });
    door('salon_side_' + s, 0, sd * HX_SAL, -2.42, -1.62, ZM + 0.03, ZM + 2.05, o, { kind: 'hinge', hu: -1.62, side: sd });
    door('side_deck_fwd_' + s, 1, Y_STEP, Math.min(sd * 3.22, sd * 3.98), Math.max(sd * 3.22, sd * 3.98), ZM + 0.02, ZM + 2.02, [0, -1], { kind: 'hinge', hu: sd * 3.98, side: sd });
    door('skylounge_aft_' + s, 1, -10.00, sd < 0 ? -1.10 : 0, sd < 0 ? 0 : 1.10, 7.02, 9.36, [0, -1], { kind: 'slide', dir: sd, travel: 1.05, out: 0.07, side: sd });
    door('captain_' + s, 0, sd * 3.05, -2.00, -1.20, 7.03, 9.10, o, { kind: 'hinge', hu: -1.20, side: sd });
    door('wing_' + s, 0, sd * 3.05, 0.18, 0.95, 7.03, 9.10, o, { kind: 'hinge', hu: 0.95, side: sd }); }
  door('fly_aft', 1, -6.80, 0.02, 0.98, ZF + 0.03, 11.55, [0, -1], { kind: 'slide', dir: 1, travel: 0.98, out: 0.07, side: 0 });

  // ================================= HULL SHELL =================================
  at('hull', 'shell');
  const mx = (p, sd) => [sd * p[0], p[1], p[2]];
  const HB = [[DWL + 0.02, 'boot', -0.3, 0], [DWL + 0.40, 'stripe', 0.1, 0.004], [Infinity, 'hull', 0, 0]];
  // the boarding gates: the bulwark inside each opening becomes that side's leaf (GLEAF): LEAF.gate_port and gate_stbd, posed per call by doorFaces()
  const toLeaf = (sd, fn) => { const s = TG; TG = GLEAF[sd]; fn(); TG = s; };
  function shellQ(q, sd, out, bb) { let ya = Infinity, yb = -Infinity, zt = -Infinity; for (const p of q) { ya = Math.min(ya, p[1]); yb = Math.max(yb, p[1]); zt = Math.max(zt, p[2]); }
    if (yb <= GATE.y0 || ya >= GATE.y1 || zt <= ZM + 1e-6) { bands(q, out, HB, bb); return; }
    const [aft, r1] = splitBy(q, (p) => p[1] - GATE.y0); if (aft) bands(aft, out, HB, bb); if (!r1) return;
    const [mid, fwd] = splitBy(r1, (p) => p[1] - GATE.y1); if (fwd) bands(fwd, out, HB, bb); if (!mid) return;
    const [lo, hi] = splitBy(mid, (p) => p[2] - ZM); if (lo) bands(lo, out, HB, bb); if (hi) toLeaf(sd, () => bands(hi, out, HB, bb)); }
  for (let i = 0; i + 1 < NSEC; i++) { const A = SEC[i], B2 = SEC[i + 1];
    for (let k = 0; k + 1 < NR; k++) for (const sd of [-1, 1]) { const q = [mx(A[k], sd), mx(B2[k], sd), mx(B2[k + 1], sd), mx(A[k + 1], sd)]; shellQ(q, sd, k < 3 ? [sd * 0.4, 0, -1] : [sd, 0, 0.06], k < 3 ? -0.15 : 0); } }
  { const s0 = SEC[0], poly = []; for (let k = NR - 1; k >= 0; k--) poly.push([s0[k][0], yT, s0[k][2]]); for (let k = 1; k < NR; k++) poly.push([-s0[k][0], yT, s0[k][2]]);
    bands(poly, [0, -1, 0], [[DWL + 0.02, 'boot', -0.9, 0], [DWL + 0.40, 'stripe', -0.5, 0.004], [Infinity, 'hull', -0.7, 0]], 0); }
  // hull glazing: lower-deck ports, round ports forward, the main-deck strakes, the master suite's windows forward
  role('shell');
  for (const [y0, y1] of [[-13.95, -13.45], [-12.50, -12.00], [-8.80, -8.30], [-7.70, -7.20], [0.75, 1.25], [2.45, 2.95], [3.45, 3.95]]) { hullPatch(y0, y1, 3.62, 3.98, 'iron', -0.2, 0.012, 0.3); hullPatch(y0 + 0.04, y1 - 0.04, 3.66, 3.94, 'glas', -0.5, 0.026, 0.3, 0.07); }
  for (const y of [6.33, 7.19, 7.90, 9.64]) { hullPatch(y - 0.19, y + 0.19, 4.02, 4.40, 'steel', 0.1, 0.012, 0.5); hullPatch(y - 0.13, y + 0.13, 4.08, 4.34, 'glas', -0.45, 0.026, 0.5, 0.07); }
  for (const [y0, y1] of [[-13.70, -12.20], [-9.50, -8.00], [-5.30, -3.80]]) hullPatch(y0, y1, 4.50, 4.62, 'glas', -0.4, 0.014, 0.12);
  for (const [y0, y1, z0] of [[2.74, 3.52, 4.95], [3.74, 4.52, 4.95], [4.77, 5.55, 4.95], [6.58, 7.37, 5.41], [8.36, 9.15, 5.41], [9.29, 10.07, 5.41]]) { hullPatch(y0 - 0.05, y1 + 0.05, z0 - 0.05, 6.46, 'iron', -0.2, 0.012, 0.08); hullPatch(y0, y1, z0, 6.41, 'glas', -0.55, 0.026, 0.06, 0.07); }
  // anchor pockets (the anchor stows in a recess below the foredeck)
  hullPatch(13.40, 14.45, 4.25, 5.35, 'dark', 0.0, 0.010, 0.10); hullPatch(13.62, 14.20, 4.50, 5.10, 'steel', -0.2, 0.030, 0.35, 0.06);
  hullPatch(-13.95, -13.55, 4.95, 5.12, 'dark', 0.2, 0.012, 0.5);   // quarter fairleads

  // ================================= STERN: platform, stairs, transom panel, shoulders =================================
  at('main_deck', 'floor');
  { const N = 6, K = 14; for (let i = 0; i < N; i++) { const y0 = lerp(yT + 0.02, Y_PLAT, i / N), y1 = lerp(yT + 0.02, Y_PLAT, (i + 1) / N), w0 = fSx(y0) - 0.03, w1 = fSx(y1) - 0.03;
    for (let k = 0; k < K; k++) { const t0 = k / K, t1 = (k + 1) / K; Q([[lerp(-w0, w0, t0), y0, ZP + 0.01], [lerp(-w0, w0, t1), y0, ZP + 0.01], [lerp(-w1, w1, t1), y1, ZP + 0.01], [lerp(-w1, w1, t0), y1, ZP + 0.01]], [0, 0, 1], 'teak', k % 2 ? -0.5 : -0.1, 0); } } }
  at('main_deck', 'trim');
  { const y = yT + 0.14; for (const sd of [-1, 1]) { const pts = [0.55, 1.50, 2.45, 3.20].map(x => [sd * x, y, ZP + 0.01]); for (let i = 0; i + 1 < pts.length; i++) tubeQ([pts[i][0], y, ZP + 0.78], [pts[i + 1][0], y, ZP + 0.78], 0.024, 0.024, 'steel', 0.3); pts.forEach(p => tubeQ(p, [p[0], y, ZP + 0.78], 0.022, 0.022, 'steel', 0.1)); } }
  at('main_deck', 'wall');
  // the transom panel between the stairs (the garage face), teak-capped as the aft deck's coaming
  Q([[-XC, Y_PLAT, ZP], [XC, Y_PLAT, ZP], [XC, Y_PLAT, ZM + 0.35], [-XC, Y_PLAT, ZM + 0.35]], [0, -1, 0], 'hull', -0.35, 0);
  Q([[-XC, Y_PLAT, ZM + 0.35], [XC, Y_PLAT, ZM + 0.35], [XC, Y_COAM, ZM + 0.35], [-XC, Y_COAM, ZM + 0.35]], [0, 0, 1], 'teak', -0.1, 0);
  Q([[-XC, Y_COAM, ZM], [XC, Y_COAM, ZM], [XC, Y_COAM, ZM + 0.35], [-XC, Y_COAM, ZM + 0.35]], [0, 1, 0], 'cream', -0.2, 0);
  for (const sd of [-1, 1]) { const x = sd * XC;
    Q([[x, Y_PLAT, ZM], [x, Y_COAM, ZM], [x, Y_COAM, ZM + 0.35], [x, Y_PLAT, ZM + 0.35]], [sd, 0, 0], 'cream', -0.1, 0);
    Q([[x, Y_PLAT, ZP], [x, Y_DECK_AFT, ZP], [x, Y_DECK_AFT, ZM], [x, Y_PLAT, ZM]], [sd, 0, 0], 'cream', -0.2, 0);
    // the shoulder: its inner wall on the stair, its white top, its forward face onto the aft deck
    const NSh = 10; for (let i = 0; i < NSh; i++) { const ya = lerp(Y_PLAT, Y_SHOULDER, i / NSh), yb = lerp(Y_PLAT, Y_SHOULDER, (i + 1) / NSh), X = sd * XSH;
      Q([[X, ya, ZP], [X, yb, ZP], [X, yb, fSz(yb)], [X, ya, fSz(ya)]], [-sd, 0, 0], 'cream', -0.3, 0);
      Q([[X, ya, fSz(ya) + 0.004], [X, yb, fSz(yb) + 0.004], [sd * (fSx(yb) + 0.012), yb, fSz(yb) + 0.004], [sd * (fSx(ya) + 0.012), ya, fSz(ya) + 0.004]], [0, 0, 1], 'cream', 0.35, 0.004); }
    Q([[sd * XSH, Y_SHOULDER, ZM], [sd * xin(Y_SHOULDER), Y_SHOULDER, ZM], [sd * xin(Y_SHOULDER), Y_SHOULDER, fSz(Y_SHOULDER)], [sd * XSH, Y_SHOULDER, fSz(Y_SHOULDER)]], [0, 1, 0], 'cream', -0.2, 0);
    // swim-platform stair (5 risers of 0.18, goings 0.30) in the channel
    at('main_deck', 'fit'); const xa = Math.min(sd * XC, sd * XSH), xb = Math.max(sd * XC, sd * XSH);
    for (let k = 1; k <= 5; k++) { const y = Y_PLAT + (k - 1) * PST.going, zb = ZP + (k - 1) * PST.rise, zt = ZP + k * PST.rise;
      Q([[xa, y, zb], [xb, y, zb], [xb, y, zt], [xa, y, zt]], [0, -1, 0], 'cream', -0.35, 0.002);
      if (k < 5) Q([[xa, y, zt], [xb, y, zt], [xb, y + PST.going, zt], [xa, y + PST.going, zt]], [0, 0, 1], 'teak', k % 2 ? -0.1 : -0.3, 0.002);
      // the stair's outer end where its tread stands over the shoulder's top (the lower treads): closed down to that top
      if (k < 5 && fSz(y) < zt) { const X = sd * XSH, ye = y + PST.going; let y1 = ye;
        if (fSz(ye) > zt) { let lo = y, hi = ye; for (let s = 0; s < 30; s++) { const m = (lo + hi) / 2; if (fSz(m) < zt) lo = m; else hi = m; } y1 = lo; }
        for (let s = 0; s < 4; s++) { const ya = lerp(y, y1, s / 4), yb = lerp(y, y1, (s + 1) / 4);
          Q([[X, ya, fSz(ya)], [X, yb, Math.min(zt, fSz(yb))], [X, yb, zt], [X, ya, zt]], [sd, 0, 0], 'cream', -0.3, 0); } } }
    tubeQ([sd * (XSH - 0.08), Y_PLAT, ZP + 0.95], [sd * (XSH - 0.08), Y_DECK_AFT, ZM + 0.95], 0.024, 0.024, 'steel', 0.3);
    tubeQ([sd * (XSH - 0.08), Y_PLAT, fSz(Y_PLAT)], [sd * (XSH - 0.08), Y_PLAT, ZP + 0.95], 0.022, 0.022, 'steel', 0.1);
    at('main_deck', 'wall'); }
  // the swim ladder: telescopic, stowed under the platform with only its head plate showing, centred in the rail's 1.10 m gap
  at('main_deck', 'trim');
  Q([[-0.27, yT + 0.03, ZP + 0.013], [0.27, yT + 0.03, ZP + 0.013], [0.27, yT + 0.11, ZP + 0.013], [-0.27, yT + 0.11, ZP + 0.013]], [0, 0, 1], 'steel', 0.1, 0.004);
  const LADDER_DEP = []; { const s = TG; TG = LADDER_DEP; role('trim'); const y = LADDER.y, h = LADDER.half, zb = ZP - LADDER.rungs * LADDER.rung - 0.06;
    for (const x of [-h, h]) { tubeQ([x, y, ZP + 0.013], [x, y, zb], 0.02, 0.02, 'steel', 0.3); tubeQ([x, y, ZP], [x, y, ZP + 0.55], 0.02, 0.02, 'steel', 0.3);
      tubeQ([x, y, ZP + 0.55], [x, yT + 0.07, ZP + 0.55], 0.02, 0.02, 'steel', 0.3); tubeQ([x, yT + 0.07, ZP + 0.55], [x, yT + 0.07, ZP + 0.013], 0.02, 0.02, 'steel', 0.3); }
    for (let k = 1; k <= LADDER.rungs; k++) { const z = ZP - k * LADDER.rung; boxQ(-h + 0.02, h - 0.02, y - 0.09, y + 0.01, z - 0.015, z + 0.015, 'steel', 0.2, 0.01); }
    TG = s; LADDER.bottom = r3(zb); }
  at('main_deck', 'wall');

  // ================================= MAIN DECK: aft deck, side decks, bulwark, salon house =================================
  at('main_deck', 'floor');
  const plank = (y0, y1, xa0, xb0, xa1, xb1, K, z) => { for (let k = 0; k < K; k++) { const t0 = k / K, t1 = (k + 1) / K;
    Q([[lerp(xa0, xb0, t0), y0, z], [lerp(xa0, xb0, t1), y0, z], [lerp(xa1, xb1, t1), y1, z], [lerp(xa1, xb1, t0), y1, z]], [0, 0, 1], 'teak', k % 2 ? -0.5 : -0.1, 0); } };
  plank(Y_COAM, Y_DECK_AFT, -XC, XC, -XC, XC, 12, ZM + 0.01);
  plank(Y_DECK_AFT, Y_SHOULDER, -XSH, XSH, -XSH, XSH, 18, ZM + 0.01);
  { const N = 10; for (let i = 0; i < N; i++) { const y0 = lerp(Y_SHOULDER, Y_SALON, i / N), y1 = lerp(Y_SHOULDER, Y_SALON, (i + 1) / N); plank(y0, y1, -xin(y0), xin(y0), -xin(y1), xin(y1), 24, ZM + 0.01); }
    const M = 20; for (let i = 0; i < M; i++) { const y0 = lerp(Y_SALON, Y_STEP, i / M), y1 = lerp(Y_SALON, Y_STEP, (i + 1) / M);
      for (const sd of [-1, 1]) plank(y0, y1, sd * HX_SAL, sd * xin(y0), sd * HX_SAL, sd * xin(y1), 3, ZM + 0.01); } }
  at('main_deck', 'trim');
  { const N = 44, segs = []; for (let i = 0; i < N; i++) { const a = lerp(Y_SHOULDER, Y_STEP, i / N), b = lerp(Y_SHOULDER, Y_STEP, (i + 1) / N);
      if (b <= GATE.y0 || a >= GATE.y1) { segs.push([a, b, 0]); continue; }
      if (a < GATE.y0) segs.push([a, GATE.y0, 0]); segs.push([Math.max(a, GATE.y0), Math.min(b, GATE.y1), 1]); if (b > GATE.y1) segs.push([GATE.y1, b, 0]); }
    for (const [y0, y1, g] of segs) for (const sd of [-1, 1]) { const emit = () => { Q([[sd * xin(y0), y0, ZM], [sd * xin(y1), y1, ZM], [sd * xin(y1), y1, fSz(y1)], [sd * xin(y0), y0, fSz(y0)]], [-sd, 0, 0], 'cream', -0.35, 0);
      Q([[sd * (xin(y0) - 0.005), y0, fSz(y0) + 0.010], [sd * (xin(y1) - 0.005), y1, fSz(y1) + 0.010], [sd * (fSx(y1) + 0.015), y1, fSz(y1) + 0.006], [sd * (fSx(y0) + 0.015), y0, fSz(y0) + 0.006]], [0, 0, 1], 'cream', 0.35, 0.006); };
      if (g) toLeaf(sd, emit); else emit(); } }
  // the gate dressing: seams round each leaf (outside, on the lining, across the cap), two hinges on the forward jamb; the latch rides the leaf
  { const S = 0.045, zT = fSz(GATE.y0);
    for (const sd of [-1, 1]) {
      const outR = (ya, yb, za, zb) => { const ny = Math.max(1, Math.ceil((yb - ya) / 0.25)), nz = 3;
        for (let i = 0; i < ny; i++) for (let s = 0; s < nz; s++) { const y0 = lerp(ya, yb, i / ny), y1 = lerp(ya, yb, (i + 1) / ny), z0 = lerp(za, zb, s / nz), z1 = lerp(za, zb, (s + 1) / nz), P = (y, z) => [sd * (halfAtZ(y, z) + 0.012), y, z];
          Q([P(y0, z0), P(y1, z0), P(y1, z1), P(y0, z1)], [sd, 0, 0], 'iron', -0.2, 0.02); } };
      at('hull', 'trim'); outR(GATE.y0 - S, GATE.y0, ZM - S, zT); outR(GATE.y1, GATE.y1 + S, ZM - S, zT); outR(GATE.y0, GATE.y1, ZM - S, ZM);
      at('main_deck', 'trim');
      for (const [ya, yb] of [[GATE.y0 - S, GATE.y0], [GATE.y1, GATE.y1 + S]]) {
        Q([[sd * (xin(ya) - 0.012), ya, ZM], [sd * (xin(yb) - 0.012), yb, ZM], [sd * (xin(yb) - 0.012), yb, fSz(yb)], [sd * (xin(ya) - 0.012), ya, fSz(ya)]], [-sd, 0, 0], 'iron', -0.2, 0.02);
        Q([[sd * (xin(ya) - 0.012), ya, fSz(ya) + 0.016], [sd * (xin(yb) - 0.012), yb, fSz(yb) + 0.016], [sd * (fSx(yb) + 0.022), yb, fSz(yb) + 0.016], [sd * (fSx(ya) + 0.022), ya, fSz(ya) + 0.016]], [0, 0, 1], 'iron', -0.1, 0.02); }
      const xi = xin(GATE.y1), hA = Math.min(sd * (xi - 0.06), sd * (xi - 0.005)), hB = Math.max(sd * (xi - 0.06), sd * (xi - 0.005));
      for (const z of [ZM + 0.18, ZM + 0.72]) boxQ(hA, hB, GATE.y1 - 0.03, GATE.y1 + 0.05, z, z + 0.14, 'steel', 0.1, 0.02);
      toLeaf(sd, () => { const xl = xin(GATE.y0 + 0.11), a = Math.min(sd * (xl - 0.045), sd * (xl - 0.005)), b = Math.max(sd * (xl - 0.045), sd * (xl - 0.005));
        boxQ(a, b, GATE.y0 + 0.06, GATE.y0 + 0.16, ZM + 0.74, ZM + 0.80, 'steel', 0.2, 0.02); }); } }
  for (const sd of [-1, 1]) tubeQ([sd * 3.30, -13.60, ZM], [sd * 3.30, -13.60, Z01U], 0.07, 0.07, 'cream', 0.1);
  at('house', 'wall');
  leafCut([LEAF.salon_side_port, LEAF.salon_side_stbd, LEAF.salon_aft_port, LEAF.salon_aft_stbd], () => {
  for (const sd of [-1, 1]) Q([[sd * HX_SAL, Y_SALON, ZM], [sd * HX_SAL, Y_STEP, ZM], [sd * HX_SAL, Y_STEP, Z01U], [sd * HX_SAL, Y_SALON, Z01U]], [sd, 0, 0], 'mainhouse', 0.05, 0);
  Q([[-HX_SAL, Y_SALON, ZM], [HX_SAL, Y_SALON, ZM], [HX_SAL, Y_SALON, Z01U], [-HX_SAL, Y_SALON, Z01U]], [0, -1, 0], 'cream', -0.4, 0); });
  leafCut([LEAF.side_deck_fwd_port, LEAF.side_deck_fwd_stbd], () => {
  for (const sd of [-1, 1]) { const xo = xin(Y_STEP) + 0.01;   // the forward section's aft face at the end of each side deck, with its door
    Q([[sd * HX_SAL, Y_STEP, ZM], [sd * xo, Y_STEP, ZM], [sd * xo, Y_STEP, Z01U], [sd * HX_SAL, Y_STEP, Z01U]], [0, -1, 0], 'mainhouse', -0.3, 0);
    const d0 = Math.min(sd * 3.22, sd * 3.98), d1 = Math.max(sd * 3.22, sd * 3.98);
    Q([[d0, Y_STEP - 0.016, ZM + 0.02], [d1, Y_STEP - 0.016, ZM + 0.02], [d1, Y_STEP - 0.016, ZM + 2.02], [d0, Y_STEP - 0.016, ZM + 2.02]], [0, -1, 0], 'iron', 0.4, 0.02);
    Q([[d0 + 0.12, Y_STEP - 0.03, ZM + 1.30], [d1 - 0.12, Y_STEP - 0.03, ZM + 1.30], [d1 - 0.12, Y_STEP - 0.03, ZM + 1.85], [d0 + 0.12, Y_STEP - 0.03, ZM + 1.85]], [0, -1, 0], 'glas', -0.5, 0.035); } });
  role('wall');
  const winX = (y0, y1, z0, z1, kind) => { for (const sd of [-1, 1]) { const x = sd * HX_SAL, fr = 0.05;
    Q([[sd * (HX_SAL + 0.016), y0 - fr, z0 - fr], [sd * (HX_SAL + 0.016), y1 + fr, z0 - fr], [sd * (HX_SAL + 0.016), y1 + fr, z1 + fr], [sd * (HX_SAL + 0.016), y0 - fr, z1 + fr]], [sd, 0, 0], 'iron', kind === 'door' ? 0.5 : -0.15, 0.02);
    if (kind === 'louvre') { for (let z = z0 + 0.06; z < z1; z += 0.14) Q([[sd * (HX_SAL + 0.03), y0, z], [sd * (HX_SAL + 0.03), y1, z], [sd * (HX_SAL + 0.03), y1, z + 0.07], [sd * (HX_SAL + 0.03), y0, z + 0.07]], [sd, 0.0, 0.3], 'steel', 0.2, 0.035); }
    else if (kind === 'door') Q([[sd * (HX_SAL + 0.03), y0 + 0.1, z1 - 0.7], [sd * (HX_SAL + 0.03), y1 - 0.1, z1 - 0.7], [sd * (HX_SAL + 0.03), y1 - 0.1, z1 - 0.15], [sd * (HX_SAL + 0.03), y0 + 0.1, z1 - 0.15]], [sd, 0, 0], 'glas', -0.5, 0.035);
    else Q([[sd * (HX_SAL + 0.03), y0, z0], [sd * (HX_SAL + 0.03), y1, z0], [sd * (HX_SAL + 0.03), y1, z1], [sd * (HX_SAL + 0.03), y0, z1]], [sd, 0, 0], 'glas', -0.55, 0.035); } };
  for (const [y0, y1] of [[-9.07, -7.55], [-7.45, -5.95], [-5.85, -4.45]]) winX(y0, y1, 5.37, 6.70);
  winX(-4.20, -2.85, 5.40, 6.62, 'louvre'); leafCut([LEAF.salon_side_port, LEAF.salon_side_stbd], () => winX(-2.42, -1.62, ZM + 0.03, ZM + 2.05, 'door')); winX(-1.48, -1.12, 5.40, 6.45);
  leafCut([LEAF.salon_aft_port, LEAF.salon_aft_stbd], () => { const y = Y_SALON - 0.016; Q([[-2.50, y, ZM + 0.02], [2.50, y, ZM + 0.02], [2.50, y, 6.70], [-2.50, y, 6.70]], [0, -1, 0], 'iron', -0.1, 0.02);
    for (const [x0, x1] of [[-2.42, -1.24], [-1.18, -0.03], [0.03, 1.18], [1.24, 2.42]]) Q([[x0, y - 0.016, ZM + 0.06], [x1, y - 0.016, ZM + 0.06], [x1, y - 0.016, 6.62], [x0, y - 0.016, 6.62]], [0, -1, 0], 'glas', -0.5, 0.035); });
  // aft deck: settee against the coaming, teak table
  at('main_deck', 'fit');
  boxQ(-1.85, 1.85, Y_COAM + 0.02, Y_COAM + 0.58, ZM, ZM + 0.40, 'cream', -0.1); boxQ(-1.80, 1.80, Y_COAM + 0.04, Y_COAM + 0.56, ZM + 0.40, ZM + 0.48, 'leath', 0.2); boxQ(-1.80, 1.80, Y_COAM + 0.02, Y_COAM + 0.16, ZM + 0.48, ZM + 0.82, 'leath', -0.05);
  cylZ(0, -15.35, ZM, ZM + 0.70, 0.12, 8, 'steel', -0.1, null); boxQ(-1.00, 1.00, -15.90, -14.85, ZM + 0.70, ZM + 0.75, 'teak', 0.1);

  // ================================= THE 01 DECK: slab, rails, Portuguese bridge, the 01 house =================================
  const O01 = outline({ yF: 5.00, yA: -15.40, W: 4.30, N: 2.40 }, { e: 2.2, rA: 1.0, clamp: (y) => fSx(y) - 0.005 });
  const S3H = { x0: -3.02, x1: -2.06, y0: -13.70, y1: -10.80 }, S01H = { x0: -2.92, x1: -1.48, y0: -6.27, y1: -3.35, tight: true };
  const zf01 = (y) => y < Y_STEP ? Z01U : Math.min(Z01U, fSz(y) + 0.004);
  at('bridge', 'floor'); deckTop(O01, Z01, 'teak', [S3H, S01H], 10);
  at('house', 'roof'); deckUnder(O01, Z01U, [S3H, S01H], Y_STEP + 0.2);
  at('bridge', 'wall'); fascia(O01, Z01, zf01, 'cream', 0.05, [Y_STEP]); holeEdges(S3H, Z01, Z01U);
  at('house', 'wall');   // the forward section's aft face outboard of the side deck, over the bulwark: from the sheer up to the 01's underside, out to the fascia
  for (const sd of [-1, 1]) { const xo = xin(Y_STEP) + 0.01, xf = hwAt(O01, Y_STEP), zs = fSz(Y_STEP);
    Q([[sd * xo, Y_STEP, zs], [sd * xf, Y_STEP, zs], [sd * xf, Y_STEP, Z01U], [sd * xo, Y_STEP, Z01U]], [0, -1, 0], 'mainhouse', -0.3, 0); }
  at('bridge', 'wall');
  // rails round the 01 aft deck and side decks (a low white coaming, stainless above); the Portuguese bridge takes over forward of y 0.75
  at('bridge', 'trim');
  { const pts = []; O01.forEach((p, j) => { if (p[1] <= 0.75 && j < NP) { const n = pnorm(O01, j); pts.push([Math.max(0, p[0] - 0.07 * n[0]), p[1] - 0.07 * n[1]]); } });
    for (const sd of [-1, 1]) { const P3 = pts.map(p => [sd * p[0], p[1], Z01 + 0.12]);
      for (let i = 0; i + 1 < P3.length; i++) { const a = P3[i], c = P3[i + 1]; Q([[a[0], a[1], Z01], [c[0], c[1], Z01], [c[0], c[1], Z01 + 0.12], [a[0], a[1], Z01 + 0.12]], [sd * 0.3, -0.2, 0.3], 'cream', 0.1, 0.01); }
      railAlong(P3, 0, 0.88, 3); } }
  for (const [x0, x1, y0, y1] of [[S3H.x1, S3H.x1, S3H.y0, S3H.y1], [S3H.x0, S3H.x1, S3H.y0, S3H.y0]]) railAlong([[x0, y0, Z01], [x1, y1, Z01]], 0, 0.95, 1);
  // Portuguese bridge: 1.25 m bulwark round the pilothouse front, gates at the forward corners, a step down to the foredeck at each
  at('bridge', 'wall');
  const PB_TOP = 8.25, GATES = [], TREADS = [];   // TREADS: the gate steps' plan and top, for walk()
  for (let j = 0; j + 1 < NC; j++) { const a = O01[j], c = O01[j + 1], ym = (a[1] + c[1]) / 2, xm = (a[0] + c[0]) / 2; if (ym < 0.75) continue;
    if (xm > 1.70 && xm < 2.70 && ym > 3.5) { GATES.push([a, c]); continue; }
    const na = pnorm(O01, j), nc = pnorm(O01, j + 1), ai = [a[0] - 0.14 * na[0], a[1] - 0.14 * na[1]], ci = [c[0] - 0.14 * nc[0], c[1] - 0.14 * nc[1]];
    for (const sd of [-1, 1]) { const nn = [(na[0] + nc[0]) / 2, (na[1] + nc[1]) / 2];
      Q([[sd * a[0], a[1], zf01(a[1])], [sd * c[0], c[1], zf01(c[1])], [sd * c[0], c[1], PB_TOP], [sd * a[0], a[1], PB_TOP]], [sd * nn[0], nn[1], 0], 'cream', 0.15 + 0.3 * Math.max(0, nn[1]), 0);
      Q([[sd * ai[0], ai[1], Z01], [sd * ci[0], ci[1], Z01], [sd * ci[0], ci[1], PB_TOP], [sd * ai[0], ai[1], PB_TOP]], [-sd * nn[0], -nn[1], 0], 'cream', -0.55, 0);
      Q([[sd * a[0], a[1], PB_TOP], [sd * c[0], c[1], PB_TOP], [sd * ci[0], ci[1], PB_TOP], [sd * ai[0], ai[1], PB_TOP]], [0, 0, 1], 'teak', 0.1, 0.004); } }
  { // end faces at the gates and at the aft ends, and the steps down (one tread, two risers of 0.19)
    const endFace = (p, nrm) => { const pi = [p[0] - 0.14 * nrm[0], p[1] - 0.14 * nrm[1]]; for (const sd of [-1, 1]) Q([[sd * p[0], p[1], Z01], [sd * pi[0], pi[1], Z01], [sd * pi[0], pi[1], PB_TOP], [sd * p[0], p[1], PB_TOP]], [sd * nrm[1], -nrm[0], 0], 'cream', -0.2, 0); };
    let jA = -1; for (let j = 0; j + 1 < NC; j++) if ((O01[j][1] + O01[j + 1][1]) / 2 >= 0.75) jA = j + 1; if (jA > 0) endFace(O01[jA], pnorm(O01, jA));
    if (GATES.length) { const g0 = GATES[0][0], g1 = GATES[GATES.length - 1][1]; endFace(g0, pnorm(O01, O01.indexOf(g0))); endFace(g1, pnorm(O01, O01.indexOf(g1)));
      at('bridge', 'fit'); const nx = (g1[1] - g0[1]), ny = -(g1[0] - g0[0]), m = Math.hypot(nx, ny), o = [-nx / m, -ny / m];   // outward (forward) normal of the gate chord
      const zfd = fSz((g0[1] + g1[1]) / 2);
      for (const sd of [-1, 1]) { const A = [sd * g0[0], g0[1]], B2 = [sd * g1[0], g1[1]], oo = [sd * o[0], o[1]], A2 = [A[0] + oo[0] * 0.34, A[1] + oo[1] * 0.34], B3 = [B2[0] + oo[0] * 0.34, B2[1] + oo[1] * 0.34];
        prismQ(sd > 0 ? [A, B2, B3, A2] : [A2, B3, B2, A], zfd - 0.02, Z01 - 0.19, 'cream', -0.1, 'teak', 0.004); TREADS.push({ sd, poly: [A, B2, B3, A2], z: r3(Z01 - 0.19) }); }
      GATES.width = r3(Math.hypot(g1[0] - g0[0], g1[1] - g0[1])); GATES.mid = [r3((g0[0] + g1[0]) / 2), r3((g0[1] + g1[1]) / 2)]; } }
  // the 01 house: sky lounge aft, captain's cabin, pilothouse forward with the reverse-raked windshield
  const H01 = ringsFrom([[7.00, 3.25, -10.00, 3.05, 1.45], [8.20, 3.29, -10.00, 3.05, 1.45], [9.00, 3.47, -10.00, 3.05, 1.50], [9.56, 3.64, -10.00, 3.05, 1.55]], [7.00, 7.60, 8.20, 8.60, 9.00, 9.30, 9.56], { e: 2.2, rA: 0.35 });
  at('bridge', 'wall'); leafCut([LEAF.captain_port, LEAF.captain_stbd, LEAF.wing_port, LEAF.wing_stbd, LEAF.skylounge_aft_port, LEAF.skylounge_aft_stbd], () => { loftWalls(H01, 'cream', 0); aftWall(H01, 'cream', 0); });
  role('wall');
  for (const [y0, y1] of [[-8.65, -7.40], [-7.30, -6.05], [-5.95, -4.80]]) winOn(H01, y0, y1, 8.11, 9.35);
  winOn(H01, -0.90, -0.20, 8.25, 9.10); winOn(H01, 1.42, 2.05, 8.25, 9.35);
  patchOn(H01, 2.12, 4.00, 8.20, 9.40, 'iron', -0.15, 0.018, 0.02); patchOn(H01, 2.18, 4.00, 8.26, 9.34, 'glas', -0.45, 0.032, 0.035);
  leafCut([LEAF.captain_port, LEAF.captain_stbd, LEAF.wing_port, LEAF.wing_stbd], () => { winOn(H01, -2.00, -1.20, 7.03, 9.10, 'door'); winOn(H01, 0.18, 0.95, 7.03, 9.10, 'door'); });
  leafCut([LEAF.skylounge_aft_port, LEAF.skylounge_aft_stbd], () => { const y = -10.0 - 0.016; Q([[-2.30, y, 7.02], [2.30, y, 7.02], [2.30, y, 9.40], [-2.30, y, 9.40]], [0, -1, 0], 'iron', -0.1, 0.02);
    for (const [x0, x1] of [[-2.22, -1.13], [-1.07, -0.03], [0.03, 1.07], [1.13, 2.22]]) Q([[x0, y - 0.016, 7.06], [x1, y - 0.016, 7.06], [x1, y - 0.016, 9.32], [x0, y - 0.016, 9.32]], [0, -1, 0], 'glas', -0.5, 0.035); });
  // 01 aft deck: settee along the aft rail, table
  at('bridge', 'fit');
  boxQ(-2.0, 2.0, -15.10, -14.55, Z01, Z01 + 0.40, 'cream', -0.1); boxQ(-1.95, 1.95, -15.08, -14.57, Z01 + 0.40, Z01 + 0.48, 'leath', 0.2); boxQ(-1.95, 1.95, -15.10, -14.96, Z01 + 0.48, Z01 + 0.84, 'leath', -0.05);
  cylZ(0, -13.35, Z01, Z01 + 0.70, 0.12, 8, 'steel', -0.1, null); boxQ(-1.10, 1.10, -13.90, -12.80, Z01 + 0.70, Z01 + 0.75, 'teak', 0.1);
  // the ensign on its staff at the 01 deck's aft edge
  tubeQ([0, -15.22, Z01], [0, -15.34, Z01 + 2.25], 0.03, 0.022, 'steel', 0.2);
  { const x = 0.01, y0 = -15.34, z1 = Z01 + 2.20, z0 = z1 - 0.56, w = 0.96, seg = (a, b, m) => Q([[x, y0 - a * w, z0], [x, y0 - b * w, z0], [x, y0 - b * w, z1], [x, y0 - a * w, z1]], [1, 0, 0], m, 0.1, 0.02);
    seg(0, 0.25, 'red'); seg(0.25, 0.75, 'cream'); seg(0.75, 1, 'red');
    Q([[x + 0.004, y0 - 0.42 * w, z0 + 0.16], [x + 0.004, y0 - 0.58 * w, z0 + 0.16], [x + 0.004, y0 - 0.58 * w, z0 + 0.40], [x + 0.004, y0 - 0.42 * w, z0 + 0.40]], [1, 0, 0], 'red', 0.1, 0.03); }

  // ================================= THE FLYBRIDGE: slab, rails, enclosure, hardtop, spa deck =================================
  const OFL = outline({ yF: 3.90, yA: -13.30, W: 3.98, N: 2.60 }, { e: 2.2, rA: 1.0, clamp: (y) => fSx(y) - 0.72 });
  const S4H = { x0: 2.06, x1: 3.04, y0: -12.86, y1: -10.00 }, SFH = { x0: -1.42, x1: -0.48, y0: -5.67, y1: -2.75, tight: true };
  at('top', 'floor'); { const aft = OFL.map(p => [p[0], Math.min(p[1], -6.80)]), fwd = OFL.map(p => [p[0], Math.max(p[1], -6.80)]);
    deckTop(aft, ZF, 'teak', [S4H], 10); deckTop(fwd, ZF, 'cream', [SFH], 0, 0.1); }
  at('bridge', 'roof'); deckUnder(OFL, ZFU, [S4H, SFH], 99);
  at('top', 'wall'); fascia(OFL, ZF, (y) => y > 2.4 ? ZFU - 0.08 : ZFU, 'cream', 0.1); holeEdges(S4H, ZF, ZFU);
  at('top', 'trim');
  { const pts = []; OFL.forEach((p, j) => { if (p[1] <= -6.6 && j < NP) { const n = pnorm(OFL, j); pts.push([Math.max(0, p[0] - 0.08 * n[0]), p[1] - 0.08 * n[1]]); } });
    for (const sd of [-1, 1]) { const P3 = pts.map(p => [sd * p[0], p[1], ZF + 0.28]);
      for (let i = 0; i + 1 < P3.length; i++) { const a = P3[i], c = P3[i + 1]; Q([[a[0], a[1], ZF], [c[0], c[1], ZF], [c[0], c[1], ZF + 0.28], [a[0], a[1], ZF + 0.28]], [sd * 0.3, -0.2, 0.3], 'cream', 0.15, 0.01); }
      railAlong(P3, 0, 0.72, 3); } }
  for (const [x0, x1, y0, y1] of [[S4H.x0, S4H.x0, S4H.y0, S4H.y1], [S4H.x0, S4H.x1, S4H.y0, S4H.y0]]) railAlong([[x0, y0, ZF], [x1, y1, ZF]], 0, 0.95, 1);
  // the enclosure: the pilothouse roof rises as a long low hood into a raked windscreen band
  const FLY = ringsFrom([[9.70, 3.72, -6.80, 2.95, 3.3], [9.95, 2.20, -6.80, 2.90, 2.9], [10.20, 0.60, -6.80, 2.84, 2.5], [10.42, -0.90, -6.80, 2.78, 2.2], [10.60, -1.70, -6.80, 2.74, 2.0], [11.10, -2.20, -6.80, 2.70, 1.85], [11.87, -2.70, -6.80, 2.64, 1.7]],
    [9.70, 9.82, 9.95, 10.08, 10.20, 10.31, 10.42, 10.51, 10.60, 10.85, 11.10, 11.45, 11.87], { e: 2.2, rA: 0.45 });
  at('top', 'wall'); leafCut([LEAF.fly_aft], () => { loftWalls(FLY, 'cream', 0.04); aftWall(FLY, 'cream', 0.04); });
  role('wall');
  patchOn(FLY, -6.50, 0.50, 10.46, 11.66, 'iron', -0.15, 0.018, 0.02); patchOn(FLY, -6.42, 0.50, 10.52, 11.60, 'glas', -0.5, 0.032, 0.035);
  for (const y of [-5.05, -3.75]) patchOn(FLY, y - 0.05, y + 0.05, 10.48, 11.64, 'cream', 0.1, 0.045, 0.045);
  leafCut([LEAF.fly_aft], () => { const y = -6.80 - 0.016; Q([[-2.20, y, ZF + 0.02], [2.20, y, ZF + 0.02], [2.20, y, 11.60], [-2.20, y, 11.60]], [0, -1, 0], 'iron', -0.1, 0.02);
    for (const [x0, x1, z0] of [[-2.12, -1.10, 10.30], [-1.04, -0.05, 10.30], [0.05, 0.95, ZF + 0.06], [1.01, 2.12, 10.30]]) Q([[x0, y - 0.016, z0], [x1, y - 0.016, z0], [x1, y - 0.016, 11.52], [x0, y - 0.016, 11.52]], [0, -1, 0], 'glas', -0.5, 0.035); });
  // hardtop over the enclosure (its roof)
  at('top', 'roof');
  { const P = outline({ yF: -1.75, yA: -7.30, W: 2.70, N: 1.20 }, { e: 2.0, rA: 0.7 }); deckTop(P, ZHT, 'cream', null, 0, 0.25); fascia(P, ZHT, () => ZHT - 0.14, 'cream', 0.3); deckUnder(P, ZHT - 0.14, null, 99); }
  // spa, sun pads, a settee and table forward to port
  at('top', 'fit');
  cylZ(0, -10.60, ZF, ZF + 0.62, 1.15, 16, 'cream', 0.05, null);
  { const n = 16, ring = []; for (let k = 0; k < n; k++) { const a0 = k / n * 2 * Math.PI, a1 = (k + 1) / n * 2 * Math.PI, P = (r, a, z) => [r * Math.cos(a), -10.60 + r * Math.sin(a), z];
    Q([P(0.95, a0, ZF + 0.62), P(0.95, a1, ZF + 0.62), P(1.15, a1, ZF + 0.62), P(1.15, a0, ZF + 0.62)], [0, 0, 1], 'teak', 0.1, 0.004);
    Q([P(0.95, a0, ZF + 0.50), P(0.95, a1, ZF + 0.50), P(0.95, a1, ZF + 0.62), P(0.95, a0, ZF + 0.62)], [-Math.cos((a0 + a1) / 2), -Math.sin((a0 + a1) / 2), 0], 'cream', -0.6, 0.004); ring.push(P(0.95, a0, ZF + 0.50)); }
    Q(ring, [0, 0, 1], 'water', 0.2, 0.004); }
  for (const x of [-1.45, 1.45]) { boxQ(x - 0.42, x + 0.42, -13.05, -11.95, ZF, ZF + 0.30, 'cream', -0.1); boxQ(x - 0.40, x + 0.40, -13.03, -11.97, ZF + 0.30, ZF + 0.38, 'leath', 0.2); boxQ(x - 0.40, x + 0.40, -12.20, -11.97, ZF + 0.38, ZF + 0.62, 'leath', 0.05); }
  boxQ(-2.95, -2.42, -9.30, -7.40, ZF, ZF + 0.40, 'cream', -0.1); boxQ(-2.93, -2.44, -9.28, -7.42, ZF + 0.40, ZF + 0.48, 'leath', 0.2); boxQ(-2.95, -2.80, -9.30, -7.40, ZF + 0.48, ZF + 0.84, 'leath', -0.05);
  cylZ(-1.90, -8.35, ZF, ZF + 0.70, 0.10, 8, 'steel', -0.1, null); boxQ(-2.30, -1.50, -8.85, -7.85, ZF + 0.70, ZF + 0.75, 'teak', 0.1);

  // ================================= THE STAIRS BETWEEN DECKS (character v9.2: 0.18 m risers, 0.26 m goings) =================================
  at('main_deck', 'fit'); const S3 = openStair(S3H.x0 + 0.02, S3H.x1 - 0.02, -14.44, 1, 15, 0.18, 0.26, ZM);
  at('bridge', 'fit'); const S4 = openStair(S4H.x0 + 0.02, S4H.x1 - 0.02, -13.64, 1, 15, 0.18, 0.26, Z01);

  // ================================= RIGGING: mast, yard, domes, radar, whips =================================
  at('rigging', 'rig');
  { const b0 = [-7.05, -6.45, 0.30], b1 = [-7.40, -6.98, 0.18], z0 = ZHT, z1 = 13.46;
    const P = (bb, sx, sy, z) => [sx * bb[2], sy < 0 ? bb[0] : bb[1], z];
    for (const [a, c, n] of [[[-1, -1], [1, -1], [0, -1, 0]], [[1, -1], [1, 1], [1, 0, 0]], [[1, 1], [-1, 1], [0, 1, 0.3]], [[-1, 1], [-1, -1], [-1, 0, 0]]])
      Q([P(b0, a[0], a[1], z0), P(b0, c[0], c[1], z0), P(b1, c[0], c[1], z1), P(b1, a[0], a[1], z1)], n, 'cream', 0.1, 0);
    boxQ(-1.66, 1.66, -7.34, -7.06, 13.46, 13.60, 'cream', 0.25);
    for (const x of [-1.24, 1.24]) { cylZ(x, -7.20, 13.60, 13.70, 0.16, 8, 'steel', 0, null); domeZ(x, -7.20, 13.70, 0.60, 0.46, 14, 'cream', 0.2); }
    tubeQ([0, -7.20, 13.60], [0, -7.30, 16.30], 0.055, 0.035, 'steel', 0.2); tubeQ([-0.42, -7.26, 15.30], [0.42, -7.26, 15.30], 0.025, 0.025, 'steel', 0.2);
    boxQ(-0.06, 0.06, -7.36, -7.24, 16.30, 16.44, 'cream', 0.4); boxQ(0.30, 0.44, -7.32, -7.14, 15.18, 15.30, 'steel', 0.1);
    boxQ(-0.18, 0.18, -4.58, -4.22, ZHT, ZHT + 0.30, 'cream', 0.1); boxQ(-0.95, 0.95, -4.47, -4.33, ZHT + 0.30, ZHT + 0.44, 'cream', 0.35);
    domeZ(0, -5.70, ZHT, 0.30, 0.18, 10, 'cream', 0.2); boxQ(-0.12, 0.12, -1.55, -1.30, ZHT, ZHT + 0.20, 'steel', 0.1);
    for (const [x, y] of [[2.72, -6.30], [-2.72, -6.30], [2.72, -4.30], [-2.72, -4.30]]) tubeQ([x, y, ZHT - 0.05], [x * 1.02, y - 0.55, 16.9], 0.035, 0.012, 'steel', 0.3); }

  // ================================= FOREDECK: deck, bulwark, rails, windlasses, crane seat, chocks =================================
  const Y_FD0 = O01[NN][1];                                     // where the 01 slab's nose meets the hull side
  at('foredeck', 'floor');
  { const N = 46; for (let i = 0; i < N; i++) { const y0 = lerp(Y_FD0, yB - 0.05, i / N), y1 = lerp(Y_FD0, yB - 0.05, (i + 1) / N), e0 = Math.max(0.01, fSx(y0) - 0.12), e1 = Math.max(0.01, fSx(y1) - 0.12);
    const n0 = Math.min(e0, hwAt(O01, y0)), n1 = Math.min(e1, hwAt(O01, y1)), z0 = fSz(y0) + 0.01, z1 = fSz(y1) + 0.01;
    for (const sd of [-1, 1]) { const K = 3; for (let k = 0; k < K; k++) { const t0 = k / K, t1 = (k + 1) / K;
      Q([[sd * lerp(n0, e0, t0), y0, z0], [sd * lerp(n0, e0, t1), y0, z0], [sd * lerp(n1, e1, t1), y1, z1], [sd * lerp(n1, e1, t0), y1, z1]], [0, 0, 1], 'deck', (i + k) % 2 ? 0.05 : -0.1, 0); }
      // a strip whose inner edge cuts across the 01 nose (its tip): the deck between the nose and that edge
      const nose = O01.slice(0, NN + 1).filter((p) => p[1] > y0 && p[1] < y1).sort((p, q) => p[1] - q[1]), zAt = (y) => lerp(z0, z1, (y - y0) / (y1 - y0));
      if (nose.length && nose.every((p) => p[0] < lerp(n0, n1, (p[1] - y0) / (y1 - y0)) - 1e-6))
        Q([[sd * n0, y0, z0], ...nose.map((p) => [sd * p[0], p[1], zAt(p[1])]), [sd * n1, y1, z1]], [0, 0, 1], 'deck', i % 2 ? 0.05 : -0.1, 0); } } }
  at('foredeck', 'wall');
  const BW = 0.32;
  { const N = 52; for (let i = 0; i < N; i++) { const y0 = lerp(Y_FD0 - 0.3, yB - 0.02, i / N), y1 = lerp(Y_FD0 - 0.3, yB - 0.02, (i + 1) / N);
    for (const sd of [-1, 1]) { const o0 = fSx(y0) + 0.004, o1 = fSx(y1) + 0.004, i0 = Math.max(0.005, fSx(y0) - 0.12), i1 = Math.max(0.005, fSx(y1) - 0.12), z0 = fSz(y0), z1 = fSz(y1);
      Q([[sd * o0, y0, z0 - 0.01], [sd * o1, y1, z1 - 0.01], [sd * o1, y1, z1 + BW], [sd * o0, y0, z0 + BW]], [sd, 0.05, 0.05], 'cream', 0.1, 0);
      Q([[sd * i0, y0, z0], [sd * i1, y1, z1], [sd * i1, y1, z1 + BW], [sd * i0, y0, z0 + BW]], [-sd, 0, 0.05], 'cream', -0.45, 0);
      Q([[sd * i0, y0, z0 + BW], [sd * i1, y1, z1 + BW], [sd * o1, y1, z1 + BW], [sd * o0, y0, z0 + BW]], [0, 0, 1], 'cream', 0.4, 0.004); } } }
  at('foredeck', 'trim');
  { const ys = []; for (let y = Y_FD0 + 0.2; y < 17.6; y += 1.35) ys.push(y);
    for (const sd of [-1, 1]) { const pts = ys.map(y => [sd * (fSx(y) - 0.06), y, fSz(y) + BW]); pts.push([sd * 0.10, 18.18, fSz(18.18) + BW]); railAlong(pts, 0, 0.68, 1); } }
  at('foredeck', 'fit');
  const zd = (y) => fSz(y) + 0.01;
  for (const sd of [-1, 1]) { const x = sd * 0.62, y = 15.40, z = zd(y);
    boxQ(x - 0.28, x + 0.28, y - 0.32, y + 0.30, z, z + 0.22, 'iron', 0.1); tubeQ([x - 0.24, y, z + 0.40], [x + 0.24, y, z + 0.40], 0.17, 0.17, 'steel', 0.1);
    cylZ(x + sd * 0.34, y, z + 0.22, z + 0.52, 0.11, 8, 'steel', 0.2); boxQ(x - 0.05, x + 0.05, y + 0.30, 16.55, z + 0.005, z + 0.035, 'blk', 0.2);
    cylZ(sd * 0.62, 16.62, zd(16.62), zd(16.62) + 0.06, 0.10, 8, 'iron', 0.2);
    boxQ(sd * 0.95 - 0.04, sd * 0.95 + 0.04, 16.10, 16.36, zd(16.2), zd(16.2) + 0.08, 'steel', 0.3); }
  boxQ(-0.07, 0.07, 17.70, yB + 0.06, fSz(yB) + 0.02, fSz(yB) + 0.10, 'steel', 0.2);

  // ================================= ROOMS (pass 2): ADR 0048's interior, four levels =================================
  // Tagged mesh in IF (it: true): the lower deck (lv below), the main deck's salon, galley and master suite (house), the 01's
  // sky lounge, captain's cabin and pilothouse (bridge), the fly (top). render() draws IF only under a cutaway, so the plain
  // exterior is unchanged. Every face keeps inside the shell (fitCheck): the hull's half-breadth less the lining below the
  // sheer, the house walls' inner faces above it. ROOMS, IDOORS, ISTN and IFLIGHTS are what design() checks: the rooms (and
  // what is _excluded from the walk), the doorways, the stations and the interior stairs. BEDS and HCH are walk()'s: the
  // mattresses (its berths) and the helm chairs (its seats).
  const ROOMS = [], IDOORS = [], ISTN = [], IFLIGHTS = [], BEDS = [], HCH = [];
  {
    TG = IF; cIT = true;
    const LIN = 0.10, DADO = 0.92, ZLC = ZM - 0.15, PT = 0.06, DH = 2.05, Y_LAZ = -16.10, Z_LAZ = 2.08, Y_COLL = 10.60, Y_MB = 2.55;
    // the envelope a level's fittings keep inside, (y, z) → half-breadth: the lining's face
    const ENV = { below: (y, z) => halfAtZ(y, Math.min(z, ZLC)) - LIN,
      house: (y, z) => (y < Y_STEP ? HX_SAL : (z <= fSz(y) ? halfAtZ(y, z) : hwAt(O01, y))) - LIN,
      bridge: (y, z) => hwAt(ringAt(H01, z).P, y) - LIN, top: (y, z) => hwAt(ringAt(FLY, z).P, y) - LIN };
    const CEIL_OF = { below: ZLC, house: Z01U, bridge: ZFU, top: ZHT - 0.14 };
    let E = ENV.below, S0 = ZBELOW, CEIL = ZLC, LVL = 'below';
    const level = (lv, sole) => { LVL = lv; E = ENV[lv]; S0 = sole; CEIL = CEIL_OF[lv]; at(lv, 'fit'); };
    const tag = (ro, fn) => { const k = cRO; role(ro); fn(); cRO = k; };
    const envMin = (y0, y1, z0, z1) => { let h = Infinity; for (let i = 0; i <= 4; i++) for (let k = 0; k <= 3; k++) h = Math.min(h, E(lerp(y0, y1, i / 4), lerp(z0, z1, k / 3))); return h; };
    // a box held inside the lining: x clamps to the envelope's least half-breadth over it (±9 runs it to the lining)
    const fitBox = (x0, x1, y0, y1, z0, z1, mat, b, topMat, skip) => { const h = envMin(y0, y1, z0, z1), a = Math.max(x0, -h), c = Math.min(x1, h);
      if (!(c - a >= 0.02)) throw new Error('ExpeditionYacht120Iso: no room for a box at x ' + x0 + '..' + x1 + ', y ' + y0 + '..' + y1 + ', z ' + r3(z0) + '..' + r3(z1));
      boxQ(a, c, y0, y1, z0, z1, mat, b, 0, topMat, skip); return [a, c]; };
    // the same by heights above the sole; a box standing on the sole skips its bottom; fb2 adds the port mirror; tall is ro tall
    const fb = (x0, x1, y0, y1, h0, h1, mat, b, topMat, skip) => fitBox(x0, x1, y0, y1, S0 + h0, S0 + h1, mat, b, topMat, skip != null ? skip : (h0 <= 0 ? 'b' : ''));
    const fb2 = (x0, x1, y0, y1, h0, h1, mat, b, topMat, skip) => { fb(x0, x1, y0, y1, h0, h1, mat, b, topMat, skip); fb(-x1, -x0, y0, y1, h0, h1, mat, b, topMat, skip); };
    const tall = (x0, x1, y0, y1, h0, h1, mat, b, topMat) => tag('tall', () => fb(x0, x1, y0, y1, h0, h1, mat, b, topMat));
    // a mattress (0.22 to 0.30 over the sole), kept for walk()'s berths: its id, which way its head lies, its box and its top
    const bed = (id, head, x0, x1, y0, y1) => { const x = fb(x0, x1, y0, y1, 0.22, 0.30, 'whiteg', 0.2); BEDS.push({ id, lv: LVL, x: x.map(r3), y: [r3(y0), r3(y1)], topZ: r3(S0 + 0.30), head }); };
    // a room, a station (where a character stands on the sole, the way it faces, the fixture and its top above the sole)
    const room = (id, label, x, y, o) => { o = o || {}; const walk = o.walk !== false;
      ROOMS.push(Object.assign({ id, label, lv: LVL, sole: r3(o.sole != null ? o.sole : S0), ceil: r3(o.ceil != null ? o.ceil : CEIL), x: x.map(r3), y: y.map(r3), walk }, walk ? {} : { _excluded: true }, o.note ? { note: o.note } : {})); };
    const stn = (id, label, kind, x, y, face, fixture, top) => ISTN.push({ id, label, lv: LVL, kind, stand: [r3(x), r3(y), r3(S0)], face, fixture, fixtureZ: r3(top) });
    // flat carpet (or another floor finish) just over the sole, held 0.12 off the lining
    const carpet = (x0, x1, y0, y1, mat, holes) => { const h = envMin(y0, y1, S0, S0 + 0.02) - 0.12, a = Math.max(x0, -h), c = Math.min(x1, h), z = S0 + 0.008;
      tag('floor', () => putH([[a, y0, z], [c, y0, z], [c, y1, z], [a, y1, z]], [0, 0, 1], mat || 'carpet', 0.1, 0, holes)); };
    // a sole at z between y stations, out to hAt(y) a side, in planks; holes punched
    const sole = (ys, z, hAt, matAt, holes) => tag('floor', () => { for (let i = 0; i + 1 < ys.length; i++) { const ya = ys[i], yb = ys[i + 1], ha = hAt(ya + 1e-4), hb = hAt(yb - 1e-4), m = matAt((ya + yb) / 2);
      [[-1, -0.5], [-0.5, 0], [0, 0.5], [0.5, 1]].forEach(([f0, f1], k) => putH([[ha * f0, ya, z], [ha * f1, ya, z], [hb * f1, yb, z], [hb * f0, yb, z]], [0, 0, 1], m, k % 2 ? -0.3 : -0.05, 0, holes)); } });
    // cut v by the rect u0..u1 (on axis ax) × z0..z1: [the part inside, the parts outside]
    const rectCut = (v, ax, u0, u1, z0, z1) => { const outs = []; let rest = v;
      for (const g of [(p) => p[ax] - u0, (p) => u1 - p[ax], (p) => p[2] - z0, (p) => z1 - p[2]]) { if (!rest) break; const s = splitBy(rest, g); if (s[0]) outs.push(s[0]); rest = s[1]; }
      return [rest && cleanPoly(rest), outs.map(cleanPoly).filter(Boolean)]; };
    // Q with openings: ops [u0, u1, z0, z1, mat] on axis ax: inside each, its mat (glass seen from inside by default) or
    // nothing ('hole'); the rest in mat
    const lineQ = (v, out, mat, b, ops, ax) => { let rest = [v];
      for (const w of ops || []) { const nx = [];
        for (const p of rest) { const [inr, outs] = rectCut(p, ax, w[0], w[1], w[2], w[3]); outs.forEach(q => nx.push(q)); if (inr && w[4] !== 'hole') Q(inr, out, w[4] || 'glassd', -0.4); }
        rest = nx; }
      rest.forEach(p => Q(p, out, mat, b)); };
    // a lining along the shell, both sides: strips at x = ±xAt(y, z) between y stations, in bands from zLo(y) up to each band's
    // top [[top(y)|z, mat(y)|mat], …]; openings ops [y0, y1, z0, z1, mat|'hole']; a cap on top out to the shell (LIN out)
    const hullLining = (ys, xAt, zLo, bands, ops, cap) => tag('lining', () => { const zv = (f, y) => typeof f === 'function' ? f(y) : f;
      for (let i = 0; i + 1 < ys.length; i++) { const ya = ys[i], yb = ys[i + 1], ym = (ya + yb) / 2, P = (sd, y, z) => [sd * xAt(y, z), y, z];
        for (const sd of [-1, 1]) { let za = zv(zLo, ya + 1e-4), zb = zv(zLo, yb - 1e-4);
          for (const [top, m] of bands) { const ta = zv(top, ya + 1e-4), tb = zv(top, yb - 1e-4); if (ta - za < 1e-3 && tb - zb < 1e-3) continue;
            lineQ([P(sd, ya, za), P(sd, yb, zb), P(sd, yb, tb), P(sd, ya, ta)], [-sd, 0, 0], zv(m, ym), sd < 0 ? 0.1 : -0.1, ops, 1); za = ta; zb = tb; }
          if (cap) Q([P(sd, ya, za), [sd * (xAt(ya, za) + LIN - 0.005), ya, za], [sd * (xAt(yb, zb) + LIN - 0.005), yb, zb], P(sd, yb, zb)], [0, 0, 1], 'whiteg', 0.3); } } });
    const hullX = (y, z) => halfAtZ(y, z) - LIN;
    // a house's lining from its ring loft: the outline inset LIN at each height in zs, in bands (teak to the dado, white above),
    // the straight runs merged; openings on the nose and sides in y (opsY), on the aft face in x (opsX); a cap on top out to the shell
    const ringLining = (RG, zs, opsY, opsX) => tag('lining', () => {
      const inset = (R, d) => R.P.map((p, j) => { if (j === 0) return [0, p[1] - d]; if (j === NP - 1) return [0, p[1] + d];
        const n = pnorm(R.P, j); return [Math.max(0, p[0] - d * n[0]), p[1] - d * n[1]]; });
      const RS = zs.map(z => { const R = ringAt(RG, z); return { z, P: inset(R, LIN), O: inset(R, 0.005), N: R.P }; });
      for (let k = 0; k + 1 < RS.length; k++) { const A = RS[k], B2 = RS[k + 1], m = B2.z <= zs[0] + DADO + 1e-6 ? 'teakg' : 'whiteg', last = k + 2 === RS.length;
        for (const sd of [-1, 1]) for (let j = 0; j + 1 < NP;) { let j2 = j + 1;
          const eq = (c) => (q) => Math.abs(A.P[q][c] - A.P[j][c]) < 1e-6 && Math.abs(B2.P[q][c] - B2.P[j][c]) < 1e-6, ex = eq(0), ey = eq(1);
          if (ex(j + 1)) { while (j2 + 1 < NP && ex(j2 + 1)) j2++; } else if (ey(j + 1)) { while (j2 + 1 < NP && ey(j2 + 1)) j2++; }
          const a0 = A.P[j], a1 = A.P[j2], b0 = B2.P[j], b1 = B2.P[j2], n = pnorm(A.N, j), aft = j >= NC - 1 && Math.abs(a0[1] - a1[1]) < 1e-6;
          lineQ([[sd * a0[0], a0[1], A.z], [sd * a1[0], a1[1], A.z], [sd * b1[0], b1[1], B2.z], [sd * b0[0], b0[1], B2.z]], [-sd * n[0], -n[1], 0], m, sd < 0 ? 0.1 : -0.1, aft ? opsX : opsY, aft ? 0 : 1);
          if (last) { const o0 = B2.O[j], o1 = B2.O[j2]; Q([[sd * b0[0], b0[1], B2.z], [sd * b1[0], b1[1], B2.z], [sd * o1[0], o1[1], B2.z], [sd * o0[0], o0[1], B2.z]], [0, 0, 1], 'whiteg', 0.3); }
          j = j2; } } });
    // a flat lining on axis ax (0: at x = c, running in y; 1: at y = c, running in x) from u0 to u1 (numbers or u(z)), z0 to z1,
    // teak to the dado; openings in u; a cap d back to the wall behind it
    const flatLining = (ax, c, u0, u1, z0, z1, out, ops, d) => tag('lining', () => {
      const U = (u, z) => typeof u === 'function' ? u(z) : u, W = (u, z) => ax ? [u, c, z] : [c, u, z], zd = Math.min(z1, z0 + DADO);
      for (const [za, zb, m] of [[z0, zd, 'teakg'], [zd, z1, 'whiteg']]) if (zb - za > 1e-3) lineQ([W(U(u0, za), za), W(U(u1, za), za), W(U(u1, zb), zb), W(U(u0, zb), zb)], out, m, 0, ops, ax ? 0 : 1);
      if (d) { const dv = ax ? [0, d, 0] : [d, 0, 0], p0 = W(U(u0, z1), z1), p1 = W(U(u1, z1), z1); Q([p0, p1, v_add(p1, dv), v_add(p0, dv)], [0, 0, 1], 'whiteg', 0.3); } });
    // a lid over a void (lv hull): strips across the shell at zAt(y)
    const lid = (y0, y1, n, zAt) => { const sv = [cLV, cRO]; at('hull', 'floor');
      const hAt = (y) => halfAtZ(y, zAt(y)) - 0.01;   // a strip that runs into the stem stops where the shell is 0.02 a side
      for (let i = 0; i < n; i++) { let ya = lerp(y0, y1, i / n), yb = lerp(y0, y1, (i + 1) / n); const oa = hAt(ya) > 0.02, ob = hAt(yb) > 0.02; if (!oa && !ob) continue;
        if (oa !== ob) { let lo = oa ? ya : yb, hi = oa ? yb : ya; for (let k = 0; k < 30; k++) { const m = (lo + hi) / 2; if (hAt(m) > 0.02) lo = m; else hi = m; } if (oa) yb = lo; else ya = lo; }
        const za = zAt(ya), zb = zAt(yb), ha = hAt(ya), hb = hAt(yb); Q([[-ha, ya, za], [ha, ya, za], [hb, yb, zb], [-hb, yb, zb]], [0, 0, 1], 'whiteg', -0.3); }
      cLV = sv[0]; cRO = sv[1]; };
    // the four faces round a stairwell, from the ceiling below (zLo) to the sole (zTop)
    const holeRim = (H, zTop, zLo) => tag('floor', () => { Q([[H.x0, H.y0, zLo], [H.x1, H.y0, zLo], [H.x1, H.y0, zTop], [H.x0, H.y0, zTop]], [0, 1, 0], 'teakg', -0.6);
      Q([[H.x0, H.y1, zLo], [H.x1, H.y1, zLo], [H.x1, H.y1, zTop], [H.x0, H.y1, zTop]], [0, -1, 0], 'teakg', -0.8);
      Q([[H.x0, H.y0, zLo], [H.x0, H.y1, zLo], [H.x0, H.y1, zTop], [H.x0, H.y0, zTop]], [1, 0, 0], 'teakg', -0.7);
      Q([[H.x1, H.y0, zLo], [H.x1, H.y1, zLo], [H.x1, H.y1, zTop], [H.x1, H.y0, zTop]], [-1, 0, 0], 'teakg', -0.9); });
    // a guard rail along pts ([[x, y], …]) on the sole: a steel top rail at 0.90, posts at the points
    const rail = (pts) => tag('fit', () => { for (let i = 0; i + 1 < pts.length; i++) tubeQ([pts[i][0], pts[i][1], S0 + 0.90], [pts[i + 1][0], pts[i + 1][1], S0 + 0.90], 0.025, 0.025, 'steel', 0.3);
      pts.forEach(p => tubeQ([p[0], p[1], S0], [p[0], p[1], S0 + 0.90], 0.022, 0.022, 'steel', 0.1)); });
    // a closed flight on a solid base: n risers of rise from zFoot at yFoot, going 0.26, climbing in dir (±1 in y); teak treads,
    // white risers and sides (none on a walled side: sides holds 'l' for x0, 'r' for x1), a handrail each side. Its headroom
    // is the least height from a tread to the ceiling (ceil) over it, the treads under the stairwell H aside (where every
    // tread is under it, from the floor at its foot).
    const flight = (id, x0, x1, yFoot, dir, n, rise, zFoot, sides, H, ceil) => { sides = sides || ''; const going = 0.26, yTop = yFoot + dir * (n - 1) * going, zTop = zFoot + n * rise; let head = Infinity;
      const skip = 'b' + (sides.indexOf('l') >= 0 ? 'l' : '') + (sides.indexOf('r') >= 0 ? 'r' : '');
      for (let k = 1; k < n; k++) { const ya = yFoot + dir * (k - 1) * going, yb = ya + dir * going, zt = zFoot + k * rise, lo = Math.min(ya, yb), hi = Math.max(ya, yb);
        tag(zt - zFoot > 1.3 ? 'tall' : 'fit', () => boxQ(x0, x1, lo, hi, zFoot, zt, 'whiteg', 0, 0, 'teakg', skip));
        if (H && !(lo >= H.y0 - 1e-6 && hi <= H.y1 + 1e-6)) head = Math.min(head, ceil - zt); }
      if (H && head === Infinity) head = ceil - zFoot;
      for (const [x, s, e] of [[x0, 'l', 1], [x1, 'r', -1]]) { const walled = sides.indexOf(s) >= 0, xr = x + e * (walled ? 0.05 : 0.03);
        tag('fit', () => { tubeQ([xr, yFoot, zFoot + rise + 0.90], [xr, yTop, zTop + 0.90], 0.025, 0.025, 'steel', 0.3);
          if (!walled) { tubeQ([xr, yFoot, zFoot + rise], [xr, yFoot, zFoot + rise + 0.90], 0.022, 0.022, 'steel', 0.1); tubeQ([xr, yTop, zTop], [xr, yTop, zTop + 0.90], 0.022, 0.022, 'steel', 0.1); } }); }
      const f = { id, lv: LVL, x: [r3(x0), r3(x1)], foot: [r3((x0 + x1) / 2), r3(yFoot - dir * 0.30), r3(zFoot)], top: [r3((x0 + x1) / 2), r3(yTop + dir * 0.60), r3(zTop)], risers: n, rise: r3(rise), going, width: r3(x1 - x0), headroom: r3(head) };
      IFLIGHTS.push(f); return f; };
    // a partition PT thick: on axis ax 0 it stands at x = c and runs in y, on 1 at y = c and runs in x; from u0 to u1 (±9: to
    // the lining, following it up the wall); sole to ceiling unless o.z0/o.z1; doorways [d0, d1, id, head] open to DH (or their
    // own head). Teak to the dado, white above (o.mat for one finish); both faces, the top, the jambs, the heads, any free ends.
    const part = (ax, c, u0, u1, doors, o) => { o = o || {}; doors = (doors || []).slice().sort((p, q) => p[0] - q[0]);
      const z0 = o.z0 != null ? o.z0 : S0, z1 = o.z1 != null ? o.z1 : CEIL, hd = (d) => Math.min(z1, z0 + (d[3] || DH)), t = PT / 2;
      const lo = (z) => u0 <= -9 ? -E(c, z) : u0, hi = (z) => u1 >= 9 ? E(c, z) : u1, W = (u, z, s) => ax ? [u, c + s * t, z] : [c + s * t, u, z];
      const zs = [z0, z1]; if (z0 + DADO < z1) zs.push(z0 + DADO); doors.forEach(d => zs.push(hd(d))); if (u0 <= -9 || u1 >= 9) for (let z = z0 + 0.45; z < z1 - 0.1; z += 0.45) zs.push(z);
      const Z = [...new Set(zs.map(r3))].sort((p, q) => p - q);
      tag('part', () => {
        for (let k = 0; k + 1 < Z.length; k++) { const za = Z[k], zb = Z[k + 1], mat = o.mat || (zb <= z0 + DADO + 1e-6 ? 'teakg' : 'whiteg'), pcs = []; let l = [lo(za), lo(zb)];
          for (const d of doors) if (zb <= hd(d) + 1e-6) { pcs.push([l, [d[0], d[0]]]); l = [d[1], d[1]]; }
          pcs.push([l, [hi(za), hi(zb)]]);
          for (const [a, r] of pcs) if (r[0] - a[0] > 1e-4 || r[1] - a[1] > 1e-4) for (const s of [-1, 1]) Q([W(a[0], za, s), W(r[0], za, s), W(r[1], zb, s), W(a[1], zb, s)], ax ? [0, s, 0] : [s, 0, 0], mat, s < 0 ? -0.15 : 0.1); }
        Q([W(lo(z1), z1, -1), W(hi(z1), z1, -1), W(hi(z1), z1, 1), W(lo(z1), z1, 1)], [0, 0, 1], o.mat || 'whiteg', 0.3);
        for (const [u, e, f] of [[u0, -1, u0 > -9], [u1, 1, u1 < 9]]) if (f) Q([W(u, z0, -1), W(u, z0, 1), W(u, z1, 1), W(u, z1, -1)], ax ? [e, 0, 0] : [0, e, 0], o.mat || 'whiteg', 0.1);
        doors.forEach((d) => { const zh = hd(d); for (const [u, e] of [[d[0], 1], [d[1], -1]]) Q([W(u, z0, -1), W(u, z0, 1), W(u, zh, 1), W(u, zh, -1)], ax ? [e, 0, 0] : [0, e, 0], 'teakg', -0.2);
          Q([W(d[0], zh, -1), W(d[1], zh, -1), W(d[1], zh, 1), W(d[0], zh, 1)], [0, 0, -1], 'whiteg', -0.8);
          IDOORS.push({ id: d[2] || (LVL + '_door_' + (IDOORS.length + 1)), lv: LVL, axis: ax, at: r3(c), u: [r3(d[0]), r3(d[1])], clear: r3(d[1] - d[0]), head: r3(zh - z0) }); }); }); };
    // y stations from y0 to y1 about step apart, keeping the ones given
    const ysOf = (y0, y1, step, keep) => { const s = new Set([y0, y1].concat(keep || [])), n = Math.ceil((y1 - y0) / step); for (let i = 1; i < n; i++) s.add(r3(lerp(y0, y1, i / n)));
      return [...s].filter(y => y >= y0 && y <= y1).sort((a, b) => a - b); };
    const MIR = (sd) => (a, b) => sd < 0 ? [-b, -a] : [a, b], SIDE = (sd) => sd < 0 ? 'port' : 'stbd';

    // ---- the lower deck (lv below): aft of the engine room, a block on the lazarette's sole (the lazarette, the guest cabins,
    // laundry and linen, the aft lobby: one floor, pass 6); the engine room (off the walk); the VIP suites
    // round the foyer stair, the crew's mess, cabins and head forward ----
    level('below', ZBELOW);
    const util = (y) => y < -14.30 || (y > -10.70 && y < -8.00) || (y > -6.20 && y < -1.80), soleAt = (y) => y < -6.20 ? Z_LAZ : ZBELOW;
    const LOW_PORTS = [[-13.95, -13.45], [-12.50, -12.00], [-8.80, -8.30], [-7.70, -7.20], [0.75, 1.25], [2.45, 2.95], [3.45, 3.95]].map(p => [p[0] + 0.04, p[1] - 0.04, 3.66, 3.94]);
    hullLining(ysOf(Y_LAZ, Y_COLL, 0.6, [-14.30, -10.70, -8.00, -6.20, -1.80, 4.10, 6.40, 9.00]), hullX, soleAt,
      [[(y) => soleAt(y) + DADO, (y) => util(y) ? 'whiteg' : 'teakg'], [ZLC, 'whiteg'], [ZM, 'teakg']], LOW_PORTS, true);
    sole([Y_LAZ, -15.50, -14.90, -14.27], Z_LAZ, (y) => halfAtZ(y, Z_LAZ) - LIN, () => 'teakg');
    { const ys = ysOf(-14.27, Y_COLL, 0.6, [-6.20, -1.80, 9.00]);   // the aft block's to the engine room's bulkhead, then the deck's own
      sole(ys.filter((y) => y <= -6.20), Z_LAZ, (y) => halfAtZ(y, Z_LAZ) - LIN, () => 'teakg');
      sole(ys.filter((y) => y >= -6.20), ZBELOW, (y) => halfAtZ(y, ZBELOW) - LIN, (y) => y > -6.20 && y < -1.80 ? 'steel' : 'teakg'); }
    lid(yT + 0.02, Y_LAZ - 0.03, 6, (y) => Math.min(ZLC, fSz(y) - 0.10, y < Y_PLAT ? ZLC : ZP + (y - Y_PLAT) * PST.rise / PST.going - 0.02));   // over the stern void, under the platform stairs' soffit (the risers' feet)
    lid(Y_COLL + 0.03, yB - 0.05, 12, () => ZLC);                                     // over the forepeak
    room('forepeak', 'Forepeak and chain locker', [-2.6, 2.6], [Y_COLL, 15.0], { walk: false, note: 'a void under its lid; the chain locker forward' });

    // the aft block, on the lazarette's sole to the engine room. The lazarette: its aft bulkhead, its door, lockers, the
    // workbench, shelving
    S0 = Z_LAZ;
    part(1, Y_LAZ, -9, 9, [], { z0: Z_LAZ });
    part(1, -14.30, -9, 9, [[-0.45, 0.45, 'lazarette']]);
    room('lazarette', 'Lazarette', [-2.8, 2.8], [Y_LAZ, -14.30], { note: 'stores and the steering gear, on the aft block\'s sole (2.08)' });
    tall(-9, -2.10, -16.02, -14.40, 0, 1.85, 'teakg'); fb(1.60, 9, -15.40, -14.40, 0, 0.85, 'teakg', 0, 'steel'); tall(1.70, 9, -16.02, -15.50, 0, 1.85, 'whiteg');

    // the aft corridor; the aft guest cabins either side, each with its en-suite inboard aft
    part(0, -0.53, -14.27, -8.00, [[-12.40, -11.55, 'guest_aft_port'], [-9.00, -8.15, 'laundry']]);
    part(0, 0.53, -14.27, -8.00, [[-12.40, -11.55, 'guest_aft_stbd'], [-9.20, -8.35, 'linen']]);
    room('corridor_aft', 'Aft corridor', [-0.50, 0.50], [-14.27, -8.00]); carpet(-0.50, 0.50, -14.27, -8.00);
    for (const sd of [-1, 1]) { const s = SIDE(sd), X = MIR(sd);
      part(1, -10.70, ...X(0.56, 9)); part(0, sd * 1.78, -14.27, -12.95); part(1, -12.98, ...X(0.56, 1.81), [X(0.70, 1.55).concat(['ensuite_aft_' + s])]);
      room('guest_aft_' + s, 'Guest cabin, aft ' + s, X(0.56, 3.9), [-12.95, -10.73]); room('ensuite_aft_' + s, 'Guest en-suite, aft ' + s, X(0.56, 1.78), [-14.27, -13.01]);
      fb(...X(0.62, 1.20), -14.24, -13.80, 0, 0.85, 'whiteg', 0, 'stone'); fb(...X(1.30, 1.70), -14.20, -13.70, 0, 0.42, 'whiteg', 0.2); carpet(...X(0.59, 1.75), -14.27, -13.01, 'stone');
      fb(...X(1.90, 3.60), -12.80, -11.40, 0, 0.22, 'teakg'); bed('guest_aft_' + s, sd < 0 ? 'port' : 'starboard', ...X(1.92, 3.60), -12.78, -11.42); fb(...X(3.60, 9), -12.85, -11.35, 0.22, 0.95, 'leath', -0.1);
      fb(...X(3.18, 3.56), -12.70, -12.15, 0.30, 0.40, 'whiteg', 0.4); fb(...X(3.18, 3.56), -12.05, -11.50, 0.30, 0.40, 'whiteg', 0.4);
      stn('bed_guest_aft_' + s, 'Guest bed, aft ' + s, 'sleep', sd * 1.62, -12.10, [sd, 0], 'rest', 0.30);
      tall(...X(2.40, 9), -11.30, -10.76, 0, 1.95, 'teakg'); fb(...X(0.62, 1.40), -11.25, -10.76, 0, 0.80, 'teakg', 0, 'stone'); carpet(...X(0.62, 9), -12.92, -10.76); }

    // laundry (port), linen (stbd) beside the aft guest stair; the stair climbs aft from the lobby to the salon
    part(1, -8.00, -9, -0.56); part(1, -8.00, 0.56, 1.92);
    room('laundry', 'Laundry', [-3.9, -0.56], [-10.67, -8.03]); room('linen', 'Linen and stores', [0.56, 1.95], [-10.67, -8.03]);
    tall(-9, -3.10, -10.65, -10.05, 0, 1.75, 'whiteg', 0, 'steel'); fb(-9, -3.30, -9.95, -8.06, 0, 0.85, 'teakg', 0, 'stone'); tall(-1.40, -0.62, -10.65, -10.10, 0, 1.95, 'teakg');
    stn('bench_laundry', 'Laundry counter', 'bench', -2.95, -9.00, [-1, 0], 'bench', 0.85);
    tall(0.62, 1.88, -10.65, -10.15, 0, 1.95, 'whiteg'); tall(1.96, 2.84, -10.65, -10.04, 0, 1.95, 'whiteg'); tall(2.90, 9, -10.65, -8.05, 0, 1.95, 'teakg');
    const HAG = { x0: 1.93, x1: 2.87, y0: -10.03, y1: -7.14 };
    flight('lower_aft', 1.95, 2.85, -7.14, -1, 12, (ZM - Z_LAZ) / 12, Z_LAZ, '', HAG, ZLC);   // its top tread where it was, its foot one going aft
    room('lobby_aft', 'Aft lobby', [-3.9, 2.85], [-7.97, -6.23]); carpet(-9, 1.90, -7.97, -6.23); fb(-2.60, -1.40, -6.62, -6.23, 0, 0.80, 'teakg', 0, 'stone');

    // the engine room: its bulkheads, a watertight door (shut, on the aft lobby's sole), the mains under red covers, gensets, exhausts
    part(1, -6.20, -9, 9, [], { mat: 'whiteg', z0: ZBELOW }); part(1, -1.80, -9, 9, [], { mat: 'whiteg', z0: ZBELOW });
    fb(-0.42, 0.42, -6.27, -6.23, 0, 1.85, 'steel', 0.1);
    S0 = ZBELOW;
    room('engine_room', 'Engine room', [-3.98, 3.98], [-6.17, -1.83], { walk: false, note: 'drawn, off the walk' });
    for (const sd of [-1, 1]) { fb(sd * 1.30 - 0.55, sd * 1.30 + 0.55, -5.40, -2.70, 0, 0.30, 'steel', -0.2); fb(sd * 1.30 - 0.45, sd * 1.30 + 0.45, -5.30, -2.80, 0.30, 1.25, 'red', 0);
      fb(sd * 3.00 - 0.40, sd * 3.00 + 0.40, -5.90, -4.80, 0, 0.95, 'whiteg', 0, 'steel'); tag('tall', () => tubeQ([sd * 1.30, -2.95, ZBELOW + 1.25], [sd * 1.30, -2.95, ZLC - 0.05], 0.12, 0.12, 'steel', 0)); }

    // the VIP suites either side of the foyer stair, en-suite aft; the lower foyer at the stair's foot
    const HFY = { x0: -0.52, x1: 0.52, y0: -1.33, y1: 1.56 };
    part(0, -0.58, -1.77, 4.07, [[2.00, 2.85, 'vip_port']]); part(0, 0.58, -1.77, 4.07, [[2.00, 2.85, 'vip_stbd']]);
    flight('foyer_lower', -0.50, 0.50, 1.82, -1, 13, 2.40 / 13, ZBELOW, 'lr', HFY, ZLC); tall(-0.52, 0.52, -1.74, -1.33, 0, 2.10, 'teakg');
    room('foyer_lower', 'Lower foyer', [-0.55, 0.55], [1.82, 4.07]); carpet(-0.55, 0.55, 1.84, 4.04);
    for (const sd of [-1, 1]) { const s = SIDE(sd), X = MIR(sd);
      part(1, 0.40, ...X(0.61, 9), [X(0.66, 1.51).concat(['ensuite_vip_' + s])]);
      room('vip_' + s, 'VIP suite, ' + s, X(0.61, 4.0), [0.43, 4.07]); room('ensuite_vip_' + s, 'VIP en-suite, ' + s, X(0.61, 4.0), [-1.77, 0.37]);
      fb(...X(0.66, 1.45), -1.74, -1.25, 0, 0.85, 'whiteg', 0, 'stone'); fb(...X(1.90, 2.30), -1.70, -1.20, 0, 0.42, 'whiteg', 0.2);
      fb(...X(2.80, 9), -1.74, -0.40, 0, 0.06, 'stone', 0.1); tag('tall', () => fb(...X(2.76, 2.80), -1.74, -0.40, 0.06, 2.05, 'glassd', -0.2)); carpet(...X(0.64, 9), -1.74, 0.37, 'stone');
      fb(...X(1.60, 3.20), 0.52, 2.45, 0, 0.22, 'teakg'); bed('vip_' + s, 'aft', ...X(1.62, 3.18), 0.54, 2.43); fb(...X(1.55, 3.25), 0.43, 0.52, 0.22, 1.05, 'leath', -0.1);
      fb(...X(1.75, 2.35), 0.56, 0.95, 0.30, 0.40, 'whiteg', 0.4); fb(...X(2.45, 3.05), 0.56, 0.95, 0.30, 0.40, 'whiteg', 0.4);
      stn('bed_vip_' + s, 'VIP bed, ' + s, 'sleep', sd * 1.30, 1.50, [sd, 0], 'rest', 0.30);
      tall(...X(1.00, 2.60), 3.45, 4.04, 0, 1.95, 'teakg'); fb(...X(3.30, 9), 2.60, 3.35, 0, 0.42, 'leath', 0.2); fb(...X(3.75, 9), 2.60, 3.35, 0.42, 0.85, 'leath', -0.1);
      carpet(...X(0.64, 9), 0.46, 4.04); }

    // the crew forward: mess and pantry, a passage between two cabins, the head; the collision bulkhead
    part(1, 4.10, -9, 9, [[-0.45, 0.45, 'crew']]);
    room('crew_mess', 'Crew mess', [-3.8, 3.8], [4.13, 6.37]); carpet(-2.85, 2.70, 4.16, 6.34);
    fb(-9, -2.90, 4.30, 6.20, 0, 0.42, 'leath', 0.2); fb(-9, -3.30, 4.30, 6.20, 0.42, 0.85, 'leath', -0.1);
    fb(-2.85, -1.90, 4.60, 5.90, 0.70, 0.74, 'teakg', 0.3, null, ''); fb(-2.45, -2.30, 5.10, 5.40, 0, 0.70, 'steel', 0);
    fb(2.75, 9, 4.16, 6.34, 0, 0.85, 'teakg', 0, 'stone'); tall(2.00, 2.70, 5.75, 6.34, 0, 1.85, 'whiteg', 0, 'steel');
    stn('bench_crew_pantry', 'Crew pantry', 'bench', 2.45, 5.25, [1, 0], 'bench', 0.85);
    part(1, 6.40, -9, 9, [[-0.45, 0.45, 'crew_passage']]);
    part(0, -0.58, 6.43, 8.97, [[7.00, 7.85, 'crew_port']]); part(0, 0.58, 6.43, 8.97, [[7.00, 7.85, 'crew_stbd']]);
    room('crew_passage', 'Crew passage', [-0.55, 0.55], [6.43, 8.97]); carpet(-0.55, 0.55, 6.43, 8.97);
    for (const sd of [-1, 1]) { const s = SIDE(sd), X = MIR(sd);
      room('crew_' + s, 'Crew cabin, ' + s, X(0.61, 3.5), [6.43, 8.97]); carpet(...X(0.64, 9), 6.46, 8.94);
      fb(...X(2.10, 9), 6.50, 8.50, 0, 0.22, 'teakg'); bed('crew_' + s, 'aft', ...X(2.12, 9), 6.52, 8.48);
      tag('tall', () => { fb(...X(2.10, 9), 6.50, 8.50, 1.20, 1.30, 'teakg', 0.1, 'whiteg', ''); fb(...X(2.10, 2.16), 6.50, 6.56, 0.30, 1.20, 'teakg', 0); });
      stn('bed_crew_' + s, 'Crew berth, ' + s, 'sleep', sd * 1.80, 7.50, [sd, 0], 'rest', 0.30); tall(...X(0.64, 1.30), 8.45, 8.94, 0, 1.95, 'teakg'); }
    part(1, 9.00, -9, 9, [[-0.42, 0.42, 'crew_head']]); part(1, Y_COLL, -9, 9);
    room('crew_head', 'Crew head', [-2.9, 2.9], [9.03, 10.57]); carpet(-9, 9, 9.03, 10.57, 'stone');
    fb(-1.40, -0.80, 10.05, 10.54, 0, 0.42, 'whiteg', 0.2); fb(0.60, 1.60, 10.05, 10.54, 0, 0.85, 'whiteg', 0, 'stone');
    fb(-9, -1.60, 9.06, 10.00, 0, 0.06, 'stone', 0.1); tag('tall', () => fb(-1.64, -1.60, 9.06, 10.00, 0.06, 2.05, 'glassd', -0.2));
    // a chair: its seat, and its back on side bk ('a' aft, 'f' forward, 'l' to x0, 'r' to x1)
    const chair = (x0, x1, y0, y1, bk, mat) => { fb(x0, x1, y0, y1, 0, 0.45, mat || 'leath', 0.2);
      fb(bk === 'r' ? x1 - 0.06 : x0, bk === 'l' ? x0 + 0.06 : x1, bk === 'f' ? y1 - 0.06 : y0, bk === 'a' ? y0 + 0.06 : y1, 0.45, 0.90, mat || 'leath', -0.1); };

    // ---- the main deck (lv house): the salon and dining aft, the foyer and its two stairs, the galley and day head, the
    // master suite forward under the foredeck ----
    level('house', ZM);
    const HMAIN = [HFY, HAG], ZMS = 6.55;                      // ZMS: the master suite's partitions stop under the foredeck
    // linings: the salon's sides and aft wall, the step out at Y_STEP, the hull forward of it, and above the sheer the 01 slab's edge
    for (const sd of [-1, 1]) {
      flatLining(0, sd * (HX_SAL - LIN), Y_SALON + LIN, Y_STEP + LIN, ZM, Z01U, [-sd, 0, 0], [[-9.07, -7.55, 5.37, 6.70], [-7.45, -5.95, 5.37, 6.70], [-5.85, -4.45, 5.37, 6.70],
        [-4.20, -2.85, 5.40, 6.62, 'steel'], [-2.42, -1.62, ZM + 0.03, ZM + 2.05, 'hole'], [-1.48, -1.12, 5.40, 6.45]], sd * LIN);
      flatLining(1, Y_STEP + LIN, sd * (HX_SAL - LIN), (z) => sd * ENV.house(Y_STEP + LIN, z), ZM, Z01U, [0, 1, 0], [[sd < 0 ? -3.98 : 3.22, sd < 0 ? -3.22 : 3.98, ZM + 0.02, ZM + 2.02, 'hole']], -LIN); }
    flatLining(1, Y_SALON + LIN, -(HX_SAL - LIN), HX_SAL - LIN, ZM, Z01U, [0, 1, 0], [[-2.42, -1.24, ZM + 0.06, 6.62], [1.24, 2.42, ZM + 0.06, 6.62], [-1.21, 1.21, ZM + 0.02, 6.66, 'hole']], -LIN);
    const MASTER_WIN = [[2.74, 3.52, 4.95, 6.41], [3.74, 4.52, 4.95, 6.41], [4.77, 5.55, 4.95, 6.41], [6.58, 7.37, 5.41, 6.41], [8.36, 9.15, 5.41, 6.41], [9.29, 10.07, 5.41, 6.41]];
    const FWD_BANDS = [[ZM + DADO, 'teakg'], [(y) => fSz(y) - 0.005, 'whiteg']];
    hullLining(ysOf(Y_STEP + LIN, Y_MB, 0.5), hullX, ZM, FWD_BANDS, [], false);
    hullLining(ysOf(Y_MB, Y_COLL, 0.5, [2.74, 3.52, 3.74, 4.52, 4.77, 5.55, 6.58, 7.37, 8.36, 9.15, 9.29, 10.07]), hullX, ZM, FWD_BANDS, MASTER_WIN, true);
    hullLining(ysOf(Y_STEP + LIN, Y_MB, 0.5), (y) => hwAt(O01, y) - LIN, (y) => fSz(y) - 0.005, [[Z01U, 'whiteg']], [], true);
    // the sole, with the two stairwells down to the lower deck
    sole(ysOf(Y_SALON + LIN, Y_STEP + LIN, 1.0), ZM + 0.005, () => HX_SAL - LIN, () => 'teakg', HMAIN);
    sole(ysOf(Y_STEP + LIN, Y_COLL, 0.6), ZM + 0.005, (y) => halfAtZ(y, ZM + 0.005) - LIN, () => 'teakg', HMAIN);
    holeRim(HFY, ZM + 0.005, ZLC); holeRim(HAG, ZM + 0.005, ZLC);
    rail([[-0.52, -1.33], [-0.52, 1.56], [0.52, 1.56]]); rail([[2.87, -7.14], [1.93, -7.14], [1.93, -10.03]]);

    // salon: an L-sofa to port, its coffee table, two armchairs, a sideboard; dining forward of it; the foyer and the stair up
    room('salon', 'Salon', [-2.92, 2.92], [-10.70, -6.30]); carpet(-2.80, 1.80, -10.58, -4.30, null, [S01H]);
    fb(-2.88, -2.10, -9.70, -6.70, 0, 0.42, 'leath', 0.2); fb(-2.88, -2.62, -9.70, -6.70, 0.42, 0.85, 'leath', -0.1);
    fb(-2.10, -0.90, -9.70, -8.92, 0, 0.42, 'leath', 0.2); fb(-2.10, -0.90, -9.70, -9.44, 0.42, 0.85, 'leath', -0.1);
    fb(-1.80, -0.90, -8.50, -7.20, 0, 0.40, 'teakg', 0.1, 'stone');
    for (const y of [-8.55, -7.50]) chair(-0.30, 0.45, y, y + 0.75, 'r');
    fb(2.40, 9, -6.90, -5.20, 0, 0.80, 'teakg', 0, 'stone');
    room('dining', 'Dining', [-1.50, 2.92], [-6.30, -2.70]);
    fb(-0.30, 1.50, -6.20, -4.60, 0.71, 0.75, 'teakg', 0.3, null, ''); fb(0.45, 0.75, -5.55, -5.25, 0, 0.71, 'steel', 0);
    for (const x of [0.00, 0.80]) { chair(x, x + 0.42, -6.62, -6.22, 'a'); chair(x, x + 0.42, -4.58, -4.18, 'f'); }
    chair(-0.75, -0.35, -5.62, -5.18, 'l'); chair(1.53, 1.93, -5.62, -5.18, 'r');
    flight('main_01', -2.88, -1.52, -2.60, -1, 15, 0.18, ZM, 'l', S01H, Z01U);
    room('foyer', 'Foyer', [-2.92, 0.60], [-2.70, 2.52]); carpet(-1.50, 0.50, -2.60, -1.40); fb(2.40, 9, -4.40, -3.20, 0, 0.80, 'teakg', 0, 'stone');

    // galley to starboard (the side-deck door aft opens into it), the day head to port
    part(1, -0.93, 0.60, 2.92, [[0.70, 1.55, 'galley']]); part(0, 0.63, -0.96, 2.52);
    room('galley', 'Galley', [0.66, 4.0], [-0.90, 2.52]); carpet(0.66, 9, -0.90, 2.52, 'stone');
    fb(3.35, 9, -0.20, 2.49, 0, 0.85, 'teakg', 0, 'stone'); fb(1.45, 3.30, 1.95, 2.49, 0, 0.85, 'teakg', 0, 'stone'); fb(1.60, 2.60, 0.60, 1.40, 0, 0.85, 'teakg', 0, 'stone');
    fb(1.55, 2.55, -0.40, 0.05, 0, 0.72, 'teakg', 0.15); tall(0.70, 1.40, 1.89, 2.49, 0, 2.00, 'whiteg', 0, 'steel');
    stn('chop_galley', 'Galley cutting board', 'chop', 2.05, -0.75, [0, 1], 'knife', 0.72);
    stn('bench_galley', 'Galley counter', 'bench', 3.00, 1.00, [1, 0], 'bench', 0.85);
    part(1, -0.10, -9, -1.65, [[-2.60, -1.75, 'day_head']]); part(0, -1.62, -0.10, 2.52);
    room('day_head', 'Day head', [-4.0, -1.65], [-0.07, 2.52]); carpet(-9, -1.65, -0.07, 2.52, 'stone');
    fb(-3.40, -2.95, 1.95, 2.49, 0, 0.42, 'whiteg', 0.2); fb(-9, -3.30, 0.20, 1.30, 0, 0.85, 'whiteg', 0, 'stone');

    // the master suite: its bulkhead and door, the en-suite to starboard, dressing to port, the king bed forward
    part(1, Y_MB, -9, 9, [[-0.45, 0.45, 'master']]); part(1, Y_COLL, -9, 9, [], { z1: ZMS });
    part(0, 1.08, Y_MB + 0.03, 5.58, [[4.65, 5.50, 'ensuite_master']], { z1: ZMS }); part(1, 5.58, 1.05, 9, [], { z1: ZMS });
    room('master', 'Master suite', [-3.9, 3.9], [Y_MB + 0.03, Y_COLL - 0.03], { ceil: fSz(Y_MB) - 0.005 });
    room('ensuite_master', 'Master en-suite', [1.11, 3.9], [Y_MB + 0.03, 5.55], { ceil: fSz(Y_MB) - 0.005 }); carpet(1.11, 9, Y_MB + 0.03, 5.55, 'stone');
    fb(1.20, 3.20, 2.61, 3.10, 0, 0.85, 'whiteg', 0, 'stone'); fb(3.40, 3.85, 3.60, 4.10, 0, 0.42, 'whiteg', 0.2);
    fb(2.30, 9, 4.40, 5.52, 0, 0.06, 'stone', 0.1); tag('tall', () => fb(2.26, 2.30, 4.40, 5.52, 0.06, 2.05, 'glassd', -0.2));
    tall(-3.60, -1.20, 2.58, 3.18, 0, 2.00, 'teakg'); fb(-9, -3.30, 4.00, 5.40, 0, 0.75, 'teakg', 0, 'teakg');
    fb(-0.98, 0.98, 8.20, 10.30, 0, 0.22, 'teakg'); bed('master', 'forward', -0.96, 0.96, 8.22, 10.28); fb(-1.05, 1.05, 10.30, 10.42, 0.10, 1.25, 'leath', -0.1);
    for (const x of [-0.85, 0.10]) fb(x, x + 0.75, 9.80, 10.25, 0.30, 0.42, 'whiteg', 0.4);
    for (const sd of [-1, 1]) fb(sd < 0 ? -1.62 : 1.12, sd < 0 ? -1.12 : 1.62, 9.90, 10.40, 0, 0.55, 'teakg', 0, 'stone');
    stn('bed_master', 'Master bed', 'sleep', -1.30, 9.10, [1, 0], 'rest', 0.30);
    fb(3.00, 9, 6.00, 7.50, 0, 0.42, 'leath', 0.2); fb(3.40, 9, 6.00, 7.50, 0.42, 0.85, 'leath', -0.1); fb(-9, -2.90, 6.20, 7.60, 0, 0.80, 'teakg', 0, 'stone');
    carpet(-1.05, 1.05, Y_MB + 0.03, 5.65); carpet(-9, 9, 5.65, 10.50);

    // a helm chair: a steel pedestal, the seat at 0.62, its back aft
    const helmChair = (x0, x1, y0, y1) => { HCH.push({ lv: LVL, x: [r3(x0), r3(x1)], y: [r3(y0), r3(y1)], seatZ: r3(S0 + 0.62), soleZ: r3(S0) }); cylZ((x0 + x1) / 2, (y0 + y1) / 2, S0, S0 + 0.32, 0.09, 8, 'steel', 0, null);
      fb(x0, x1, y0, y1, 0.32, 0.62, 'leath', 0.2, null, ''); fb(x0, x1, y0, y0 + 0.08, 0.62, 1.30, 'leath', -0.1); };

    // ---- the 01 (lv bridge): the sky lounge aft with its bar and games table, the stair hall to port, the captain's cabin and
    // head to starboard, the pilothouse forward ----
    level('bridge', Z01);
    ringLining(H01, [7.00, 7.92, 8.20, 9.00, 9.55],
      [[-8.65, -7.40, 8.11, 9.35], [-7.30, -6.05, 8.11, 9.35], [-5.95, -4.80, 8.11, 9.35], [-2.00, -1.20, Z01 + 0.03, 9.10, 'hole'], [-0.90, -0.20, 8.25, 9.10],
        [0.18, 0.95, Z01 + 0.03, 9.10, 'hole'], [1.42, 2.05, 8.25, 9.35], [2.18, 4.00, 8.26, 9.34]],
      [[-2.22, -1.13, 7.06, 9.32], [1.13, 2.22, 7.06, 9.32], [-1.10, 1.10, Z01 + 0.02, 9.36, 'hole']]);
    // the stair hall: the stairwell up from the main deck (arriving aft, into the lounge), the flight on to the fly beside it
    holeRim(S01H, Z01, Z01U); rail([[-1.48, -6.27], [-1.48, -3.35], [-2.92, -3.35]]);
    flight('01_fly', -1.40, -0.50, -2.00, -1, 15, 0.18, Z01, '', SFH, ZFU);
    room('stair_hall', 'Stair hall', [-2.95, -0.50], [-6.27, -1.20]); carpet(-9, -1.48, -3.35, -1.20);

    // sky lounge: an L-sofa to port and its table, the games table, the bar to starboard with the back-bar against the side
    room('sky_lounge', 'Sky lounge', [-2.95, 2.95], [-9.90, -5.33]); carpet(-9, 9, -9.80, -6.40);
    fb(-9, -2.20, -9.75, -7.30, 0, 0.42, 'leath', 0.2); fb(-9, -2.69, -9.75, -7.30, 0.42, 0.85, 'leath', -0.1);
    fb(-2.20, -1.20, -9.75, -9.00, 0, 0.42, 'leath', 0.2); fb(-2.20, -1.20, -9.75, -9.49, 0.42, 0.85, 'leath', -0.1);
    fb(-1.90, -1.10, -8.60, -7.60, 0, 0.40, 'teakg', 0.1, 'stone');
    fb(0.20, 1.00, -9.20, -8.40, 0, 0.74, 'teakg', 0.1, 'stone');
    chair(0.39, 0.81, -9.65, -9.25, 'a'); chair(0.39, 0.81, -8.35, -7.95, 'f'); chair(-0.25, 0.15, -9.01, -8.59, 'l'); chair(1.05, 1.45, -9.01, -8.59, 'r');
    fb(1.40, 1.90, -7.00, -5.45, 0, 0.85, 'teakg', 0, 'stone'); fb(2.50, 9, -7.00, -5.45, 0, 0.85, 'teakg', 0, 'stone');
    for (const y of [-6.75, -5.70]) cylZ(1.15, y, S0, S0 + 0.72, 0.16, 8, 'leath', 0.1, 'leath');
    stn('bench_sky_bar', 'Sky lounge bar', 'bench', 2.20, -6.25, [-1, 0], 'bench', 0.85);

    // the captain's cabin and its head, off a short corridor to starboard of the stairs
    part(0, 0.48, -5.30, -1.20, [[-3.00, -2.15, 'captain']]); part(1, -5.30, 0.48, 9); part(1, -1.20, 0.48, 9);
    part(0, 1.95, -5.30, -4.20); part(1, -4.20, 1.95, 9, [[2.05, 2.90, 'captain_head']]);
    room('corridor_01', 'Corridor', [-0.45, 0.45], [-5.30, -1.20]); carpet(-0.47, 0.45, -5.30, -1.20);
    room('captain', "Captain's cabin", [0.51, 2.95], [-5.27, -1.23]); carpet(0.51, 9, -4.17, -1.23);
    room('captain_head', "Captain's head", [1.98, 2.95], [-5.27, -4.23]); carpet(1.98, 9, -5.27, -4.23, 'stone');
    fb(2.45, 2.90, -5.25, -4.80, 0, 0.42, 'whiteg', 0.2); fb(2.00, 2.40, -5.25, -4.85, 0, 0.85, 'whiteg', 0, 'stone');
    fb(1.20, 2.60, -4.05, -2.12, 0, 0.22, 'teakg'); bed('captain', 'aft', 1.22, 2.58, -4.03, -2.14); fb(1.20, 2.60, -4.15, -4.05, 0.10, 1.10, 'leath', -0.1, null, '');
    for (const x of [1.35, 1.95]) fb(x, x + 0.50, -4.00, -3.60, 0.30, 0.42, 'whiteg', 0.4);
    stn('bed_captain', "Captain's bed", 'sleep', 0.95, -3.10, [1, 0], 'rest', 0.30);
    tall(0.55, 1.15, -1.60, -1.25, 0, 2.00, 'teakg');   // the wardrobe, 0.35 deep (the owner, 10-27): 0.52 clear to the bed's foot

    // the pilothouse: the console across the windscreen, the helm chair and two more, the chart table, a settee aft to starboard
    room('pilothouse', 'Pilothouse', [-2.95, 2.95], [-1.20, 3.25]); carpet(-9, 9, -1.17, 2.40);
    fb(-1.60, 1.60, 2.45, 2.95, 0, 1.00, 'dark', 0, 'iron');
    helmChair(-0.30, 0.30, 1.40, 1.95); helmChair(-1.50, -0.90, 1.40, 1.95); helmChair(0.90, 1.50, 1.40, 1.95);
    fb(-2.60, -1.60, -0.90, -0.10, 0, 0.92, 'teakg', 0, 'teakg');
    fb(2.20, 9, -1.12, 0.08, 0, 0.42, 'leath', 0.2); fb(2.78, 9, -1.12, 0.08, 0.42, 0.85, 'leath', -0.1);

    // ---- the fly (lv top): the lounge under the hardtop, its bar to port, the upper helm forward, the stair down ----
    level('top', ZF);
    ringLining(FLY, [9.70, 9.95, 10.20, 10.42, 10.60, 11.10, 11.60, 11.86], [[-6.42, 0.50, 10.52, 11.60]],
      [[-2.12, -1.10, 10.30, 11.52], [-1.04, -0.05, 10.30, 11.52], [1.01, 2.12, 10.30, 11.52], [0.02, 0.98, ZF + 0.03, 11.55, 'hole']]);
    holeRim(SFH, ZF, ZFU); rail([[-1.42, -5.67], [-1.42, -2.75], [-0.48, -2.75], [-0.48, -5.67]]);
    room('fly_lounge', 'Fly lounge', [-2.85, 2.85], [-6.70, -2.45]); carpet(-9, 9, -6.68, -2.50, null, [SFH]);
    fb(0.25, 1.75, -2.95, -2.45, 0, 1.00, 'dark', 0, 'iron'); helmChair(0.70, 1.30, -4.30, -3.75);
    fb(-9, -2.30, -5.80, -4.20, 0, 0.85, 'teakg', 0, 'stone');
    for (const y of [-5.55, -4.45]) cylZ(-2.00, y, S0, S0 + 0.72, 0.16, 8, 'leath', 0.1, 'leath');
    stn('bench_fly_bar', 'Fly bar', 'bench', -2.00, -5.00, [-1, 0], 'bench', 0.85);
    fb(2.00, 9, -6.60, -4.80, 0, 0.42, 'leath', 0.2); fb(2.55, 9, -6.60, -4.80, 0.42, 0.85, 'leath', -0.1);
    fb(1.20, 1.90, -6.20, -5.20, 0, 0.72, 'teakg', 0.1, 'stone');

    TG = F; cIT = false; at('foredeck', 'fit');
  }
  // ================================= TENDERS + CRANE: the published design numbers =================================
  const CR = { base: [0, 12.40], pedestalTop: 8.00, pivot: [0, 0.15, 8.40], boom: 4.20, extMax: 2.50, sheave: 0.12, hookBlock: 0.30, swl_kg: 1500,
    crutch: [0, 8.60], controls: [0, 12.05], note: 'Telescopic knuckle-free crane, hydraulic slew / luff / telescope, a 1.5 t hoist winch on the slewing housing. Stows boom-aft on the crutch between the tenders.' };
  const TEND = { port: { x: -1.75, y: 8.90 }, stbd: { x: 1.75, y: 8.90 } };
  const T_KEEL = (y) => fSz(y) + 0.30;                     // the tender's keel on its chocks
  for (const k of ['port', 'stbd']) { const t = TEND[k]; t.z = r3(T_KEEL(t.y)); t.deckZ = r3(zd(t.y)); }
  // crutch + chocks (static)
  { const z = zd(CR.crutch[1]); tubeQ([0, CR.crutch[1], z], [0, CR.crutch[1], 7.88], 0.07, 0.07, 'cream', 0.1); boxQ(-0.20, 0.20, CR.crutch[1] - 0.10, CR.crutch[1] + 0.10, 7.86, 7.93, 'blk', 0.3); }
  for (const k of ['port', 'stbd']) { const t = TEND[k];
    for (const dy of [-1.60, 1.00]) for (const dx of [-0.52, 0.52]) { const x = t.x + dx, y = t.y + dy, z = zd(y); boxQ(x - 0.10, x + 0.10, y - 0.22, y + 0.22, z, t.z + 0.16, 'cream', 0.0, 0, 'blk'); } }

  // ================================= THE CRANE (posed per call) =================================
  const STOW = { slew: 180, luff: -4.5, ext: 0, drop: 0.35 };
  function craneTip(p) { const s = p.slew * DEG, V = [Math.sin(s), Math.cos(s)], Lb = CR.boom + clamp(p.ext, 0, CR.extMax), f = p.luff * DEG, h = CR.pivot[1] + Lb * Math.cos(f);
    return [CR.base[0] + V[0] * h, CR.base[1] + V[1] * h, CR.pivot[2] + Lb * Math.sin(f)]; }
  function solveTip(T, sd) { const dx = T[0] - CR.base[0], dy = T[1] - CR.base[1]; let s = Math.atan2(dx, dy) / DEG; if (sd < 0 && s < 0) s += 360; if (sd > 0 && s < 0) s += 360;
    const r = Math.hypot(dx, dy) - CR.pivot[1], dz = T[2] - CR.pivot[2], Lb = Math.hypot(r, dz); return { slew: s, luff: Math.atan2(dz, r) / DEG, ext: Lb - CR.boom }; }
  function craneFaces(p) {
    const out = [], saveTG = TG, saveLV = cLV, saveRO = cRO; TG = out; at('foredeck', 'rig');
    const z0 = zd(CR.base[1]), s = p.slew * DEG, U = [Math.cos(s), -Math.sin(s)], V = [Math.sin(s), Math.cos(s)];
    const W = (u, v, w) => [CR.base[0] + U[0] * u + V[0] * v, CR.base[1] + U[1] * u + V[1] * v, CR.pedestalTop + w];
    cylZ(CR.base[0], CR.base[1], z0, CR.pedestalTop, 0.30, 10, 'cream', 0.05);
    boxQ(-0.20, 0.20, CR.controls[1] - 0.10, CR.controls[1] + 0.02, z0 + 0.62, z0 + 0.98, 'iron', 0.2);
    Q([[-0.16, CR.controls[1] - 0.105, z0 + 0.70], [0.16, CR.controls[1] - 0.105, z0 + 0.70], [0.16, CR.controls[1] - 0.105, z0 + 0.92], [-0.16, CR.controls[1] - 0.105, z0 + 0.92]], [0, -1, 0], 'glas', -0.3, 0.02);
    for (const x of [-0.10, 0, 0.10]) tubeQ([x, CR.controls[1] - 0.06, z0 + 0.98], [x, CR.controls[1] - 0.10, z0 + 1.12], 0.012, 0.012, 'steel', 0.3);
    // slewing housing
    const H = [[-0.42, -0.45], [0.42, -0.45], [0.42, 0.40], [-0.42, 0.40]].map(([u, v]) => W(u, v, 0));
    prismQ(H.map(p3 => [p3[0], p3[1]]), CR.pedestalTop, CR.pedestalTop + 0.55, 'cream', 0.2);
    tubeQ(W(-0.36, -0.30, 0.66), W(0.36, -0.30, 0.66), 0.14, 0.14, 'iron', 0.2);                      // hoist winch drum
    // boom: base section + telescope
    const f = p.luff * DEG, D = [V[0] * Math.cos(f), V[1] * Math.cos(f), Math.sin(f)], P0 = W(0, CR.pivot[1], CR.pivot[2] - CR.pedestalTop), Lb = CR.boom + clamp(p.ext, 0, CR.extMax);
    const at_ = (t) => v_add(P0, v_mul(D, t));
    tubeQ(v_add(P0, v_mul(D, -0.25)), at_(CR.boom), 0.19, 0.17, 'cream', 0.15);
    tubeQ(at_(CR.boom - 0.40), at_(Lb), 0.13, 0.13, 'cream', 0.3);
    boxQ(-0.001, 0.001, -0.001, 0.001, 0, 0, 'cream');                                                // (no-op keeps face order stable)
    const tip = at_(Lb); boxQ(tip[0] - 0.12, tip[0] + 0.12, tip[1] - 0.12, tip[1] + 0.12, tip[2] - 0.14, tip[2] + 0.12, 'steel', 0.2);
    tubeQ(W(0, 0.48, 0.18), at_(1.55), 0.075, 0.06, 'steel', 0.2);                                    // luff ram
    const hk = [tip[0], tip[1], tip[2] - Math.max(0.2, p.drop)];
    tubeQ([tip[0], tip[1], tip[2] - 0.12], [hk[0], hk[1], hk[2] + 0.14], 0.014, 0.014, 'iron', 0.2);
    boxQ(hk[0] - 0.09, hk[0] + 0.09, hk[1] - 0.09, hk[1] + 0.09, hk[2] - 0.14, hk[2] + 0.14, 'red', 0.3);
    TG = saveTG; cLV = saveLV; cRO = saveRO; return { faces: out, tip, hook: [hk[0], hk[1], hk[2] - 0.14] };
  }

  // ================================= LAUNCH / RECOVER: the design sequence =================================
  const RING = root.YachtTenderIso ? root.YachtTenderIso.LIFT.ring : [0, -0.20, 2.35], TFLOAT = root.YachtTenderIso ? root.YachtTenderIso.FLOAT.waterlineZ : 0.30;
  const HOOK_OVER = 0.55, LIFT_CLEAR = 1.20, OUT_GAP = 0.35;
  function outboardPos(sd) { const y = 10.60, x = sd * (fSx(y) + 1.10 + OUT_GAP); return [x, y]; }
  const _seq = {};
  function sequence(side) {
    if (_seq[side]) return _seq[side]; const sd = side === 'port' ? -1 : 1, t = TEND[side];
    const ringChocks = [t.x + RING[0], t.y + RING[1], t.z + RING[2]], [ox, oy] = outboardPos(sd);
    const tipK1 = [ringChocks[0], ringChocks[1], ringChocks[2] + HOOK_OVER], tipK2 = [tipK1[0], tipK1[1], tipK1[2] + LIFT_CLEAR];
    const keelOut = t.z + LIFT_CLEAR, tipK3 = [ox + RING[0], oy + RING[1], keelOut + RING[2] + HOOK_OVER];
    const k1 = solveTip(tipK1, sd), k2 = solveTip(tipK2, sd), k3 = solveTip(tipK3, sd), dropWater = tipK3[2] - (DWL - TFLOAT + RING[2]);
    const K = [
      { t: 0.00, id: 'stowed', label: 'Stowed', pose: STOW, hooked: false, note: 'Boom aft on its crutch, tenders on their chocks.' },
      { t: 0.18, id: 'hooked', label: 'Hooked on', pose: { slew: k1.slew, luff: k1.luff, ext: k1.ext, drop: HOOK_OVER }, hooked: true, note: 'The operator slews over the tender; a deckhand shackles the three-leg sling to the hook.' },
      { t: 0.32, id: 'lifted', label: 'Lifted clear', pose: { slew: k2.slew, luff: k2.luff, ext: k2.ext, drop: HOOK_OVER }, hooked: true, note: 'Hoisted ' + LIFT_CLEAR.toFixed(2) + ' m: the keel clears the chocks and the rail.' },
      { t: 0.56, id: 'outboard', label: 'Slewed out', pose: { slew: k3.slew, luff: k3.luff, ext: k3.ext, drop: HOOK_OVER }, hooked: true, note: 'Slewed and telescoped out; she hangs ' + OUT_GAP.toFixed(2) + ' m off the topsides.' },
      { t: 0.90, id: 'lowered', label: 'Lowered', pose: { slew: k3.slew, luff: k3.luff, ext: k3.ext, drop: dropWater }, hooked: true, note: 'The winch pays out ' + (dropWater - HOOK_OVER).toFixed(2) + ' m of wire until she floats.' },
      { t: 1.00, id: 'afloat', label: 'Afloat', pose: { slew: k3.slew, luff: k3.luff, ext: k3.ext, drop: dropWater + 0.30 }, hooked: true, slack: true, note: 'Wire slack, sling released; recovery runs this sequence backward.' } ];
    return (_seq[side] = { side, sd, keys: K, ringChocks, outboard: [r3(ox), r3(oy)], dropWater: r3(dropWater), reach: [r3(Math.hypot(tipK1[0], tipK1[1] - CR.base[1])), r3(Math.hypot(tipK3[0], tipK3[1] - CR.base[1]))] });
  }
  function launchState(side, t) { const Sq = sequence(side), K = Sq.keys; t = clamp01(+t || 0);
    let i = 0; while (i < K.length - 2 && t > K[i + 1].t) i++; const A = K[i], B2 = K[i + 1], u = clamp01((t - A.t) / ((B2.t - A.t) || 1));
    const pose = {}; for (const k of ['slew', 'luff', 'ext', 'drop']) pose[k] = lerp(A.pose[k], B2.pose[k], u);
    const tip = craneTip(pose), ring = [tip[0], tip[1], tip[2] - pose.drop], hooked = t >= K[1].t - 1e-6, T = TEND[side];
    let pos = [T.x, T.y, T.z]; if (hooked) pos = [ring[0] - RING[0], ring[1] - RING[1], Math.max(DWL - TFLOAT, ring[2] - RING[2])];
    const afloat = pos[2] <= DWL - TFLOAT + 1e-3; const phase = u < 0.5 ? A.id : B2.id;
    return { side, t, pose, tip, ring, hooked, afloat, tender: pos, tilt: t > 0.94 ? clamp01(1 - (t - 0.94) / 0.06) : 1, phase, key: A.id, next: B2.id, u }; }

  // ================================= TENDER FACES (placed from YachtTenderIso) =================================
  function tenderFaces(side, pos, o) { const TR = root.YachtTenderIso; if (!TR) return [];
    const slingTop = o.sling ? [o.ringLocal[0], o.ringLocal[1], o.ringLocal[2]] : null;
    const src = TR.faces({ tilt: o.tilt, sling: !!o.sling, slingTop }), out = new Array(src.length);
    for (let i = 0; i < src.length; i++) { const f = src[i]; out[i] = { v: f.v.map(p => [p[0] + pos[0], p[1] + pos[1], p[2] + pos[2]]), mat: f.mat, b: f.b, db: f.db, lv: LV.foredeck, ro: RO[f.rl] != null ? RO[f.rl] : RO.fit, it: false, side }; }
    return out; }

  // ================================= BOARDING GATES (posed per call) =================================
  function gateOpen(opts, side) { const g = opts && opts.gates; if (g == null || g === false) return 0; if (g === true) return 1; return clamp01(typeof g === 'number' ? g : (+g[side] || 0)); }
  function gatePose(D, t) { const sd = D.side, L = D.faces; {
      const th = -sd * t * Math.PI, c = Math.cos(th), s = Math.sin(th), px = sd * (xin(GATE.y1) - 0.02), py = GATE.y1;   // hinge 0.02 inboard of the lining: folded, the leaf clears it
      const R = (p) => { const dx = p[0] - px, dy = p[1] - py; return [px + dx * c - dy * s, py + dx * s + dy * c, p[2]]; }, Rv = (n) => [n[0] * c - n[1] * s, n[0] * s + n[1] * c, n[2]];
      const buf = L.map(f => ({ v: f.v.map(R), mat: f.mat, b: f.b, db: f.db, lv: f.lv, ro: f.ro, it: f.it }));
      const saveTG = TG, saveLV = cLV, saveRO = cRO; TG = buf; at('main_deck', 'trim');
      const endPoly = (y) => { const zm = (ZM + fSz(y)) / 2; return [[sd * xin(y), y, ZM], [sd * halfAtZ(y, ZM), y, ZM], [sd * halfAtZ(y, zm), y, zm], [sd * fSx(y), y, fSz(y)], [sd * xin(y), y, fSz(y)]]; };
      Q(endPoly(GATE.y0).map(R), Rv([0, -1, 0]), 'cream', -0.2, 0); Q(endPoly(GATE.y1).map(R), Rv([0, 1, 0]), 'cream', -0.2, 0);   // the leaf's ends
      Q(endPoly(GATE.y0), [0, 1, 0], 'cream', -0.3, 0); Q(endPoly(GATE.y1), [0, -1, 0], 'cream', -0.3, 0);                     // the jambs
      Q([[sd * xin(GATE.y0), GATE.y0, ZM + 0.012], [sd * halfAtZ(GATE.y0, ZM), GATE.y0, ZM + 0.012], [sd * halfAtZ(GATE.y1, ZM), GATE.y1, ZM + 0.012], [sd * xin(GATE.y1), GATE.y1, ZM + 0.012]], [0, 0, 1], 'steel', 0.1, 0.004);   // sill plate
      TG = saveTG; cLV = saveLV; cRO = saveRO; return buf; } }

  // ================================= FACE LISTS + RENDER =================================
  // doorFaces(t): every leaf at t in [0, 1]; t is a number for all of them, or { id: t } per leaf ({ doorOpen } as v3)
  function leafT(t, D) { if (t == null || t === false) return 0; if (t === true) return 1; if (typeof t === 'number') return clamp01(t);
    const v = t[D.id] != null ? t[D.id] : t.doorOpen; return v === true ? 1 : clamp01(+v || 0); }
  function doorFaces(t) { let out = []; for (const D of LEAVES) out = out.concat(leafPose(D, leafT(t, D))); return out; }
  // render's leaves: opts.doors (or opts.doorOpen; open by default under a cutaway); the gates keep opts.gates unless named
  function leafTimes(opts) { const base = opts.doors != null ? opts.doors : (opts.doorOpen != null ? opts.doorOpen : (opts.cutaway ? 1 : 0)), m = {};
    for (const D of LEAVES) m[D.id] = (D.kind === 'gate' && !(base && typeof base === 'object' && base[D.id] != null)) ? gateOpen(opts, D.side < 0 ? 'port' : 'stbd') : leafT(base, D);
    return m; }
  // faceList(state): the exterior without the leaves. state.tenders: true (the default, both on their chocks), false (neither)
  // or the sides aboard (['port', 'stbd']); state.launch: the crane's launch, -1..1 (the sign is the side, - port, + stbd; |t|
  // is the sequence's t, 0 stowed, 1 afloat) or { side, t }; state.ladder: the swim ladder down; state.crane: a pose (STOW)
  function launchOf(l) { if (typeof l === 'number') return l ? { side: l < 0 ? 'port' : 'stbd', t: Math.min(1, Math.abs(l)) } : null;
    return l && (l.side === 'port' || l.side === 'stbd') ? l : null; }
  function faceList(opts) {
    opts = opts || {}; const ln = launchOf(opts.launch); let crane = null, placements = { port: { pos: [TEND.port.x, TEND.port.y, TEND.port.z], tilt: 1 }, stbd: { pos: [TEND.stbd.x, TEND.stbd.y, TEND.stbd.z], tilt: 1 } };
    if (ln) { const Lz = launchState(ln.side, ln.t); crane = craneFaces(Lz.pose);
      const ringLocal = [Lz.ring[0] - Lz.tender[0], Lz.ring[1] - Lz.tender[1], Lz.ring[2] - Lz.tender[2]];
      placements[ln.side] = { pos: Lz.tender, tilt: Lz.tilt, sling: Lz.hooked, ringLocal }; }
    else crane = craneFaces(opts.crane || STOW);
    const aboard = opts.tenders === false ? [] : (Array.isArray(opts.tenders) ? opts.tenders : ['port', 'stbd']); let out = F.concat(opts.ladder ? LADDER_DEP : [], crane.faces);
    for (const k of ['port', 'stbd']) if (aboard.indexOf(k) >= 0 || (ln && ln.side === k)) out = out.concat(tenderFaces(k, placements[k].pos, placements[k]));
    return out;
  }
  // render(dir, opts): the exterior (faceList and the doors' leaves). opts.cutaway = { mode: 'ghost' | 'gate', level: a CUT_KEYS
  // key, player: {x, y, z} or [x, y, z] (left out, no ghost), ghost: GHOST overrides } adds the rooms (IF; opts.interior false
  // leaves them out), lifts the key's set and ghosts the ghost roles round the player, as v3 does. geometry().cutaway.players
  // holds each key's preview stand
  function render(dir, opts) {
    opts = (typeof opts === 'number') ? { elev: opts } : (opts || {});
    const cw = opts.cutaway, key = cw ? (cw.level || cw.key || 'deck') : null;
    let faces = faceList(opts).concat(doorFaces(leafTimes(opts)));
    if (cw) { const lift = liftOf(cw.mode, key); if (opts.interior !== false) faces = faces.concat(IF); if (lift) faces = faces.filter(f => !lift(f)); }
    if (opts.cullLevels && opts.cullLevels.length) { const cut = new Set(opts.cullLevels.map(n => LV[n] != null ? LV[n] : n)); faces = faces.filter(f => !cut.has(f.lv)); }
    if (opts.channel === 'levels') faces = faces.map(f => ({ v: f.v, mat: 'lv_' + (LV_NAME[f.lv] || 'hull'), b: (f.b || 0) * 0.5, db: f.db, lv: f.lv, ro: f.ro }));
    const o = Object.assign({}, opts, { dir });
    if (cw && cw.player) o._ghost = ghostParams(camBasis(o), cw.player, cw.ghost);
    return _toRGBA(_paint(faces, o, G, matsFor(opts)), G.W, G.H);
  }
  const G = { W: 1232, H: 948, cx: 616, cy: 602 };             // every facing × every launch pose × the rock extremes clears the edge by ≥ 24 px
  const ROCK = { frames: 8, rollA: 0.9, pitchA: 0.45, heaveA: 0.6, period: 6.0 };
  function rock(i, frames) { frames = frames || ROCK.frames; const a = 2 * Math.PI * (((i % frames) + frames) % frames) / frames;
    return { roll: ROCK.rollA * Math.sin(a), pitch: ROCK.pitchA * Math.sin(a + Math.PI / 2), heave: ROCK.heaveA * Math.sin(a) }; }
  function project(p, dir, opts) { const o = Object.assign({}, (typeof opts === 'number' ? { elev: opts } : opts || {}), { dir }); const q = projVert(p[0], p[1], p[2], camBasis(o), G); return { x: q.sx, y: q.sy, d: q.d }; }

  // ================================= CHARACTER v9.2 CONTRACT + DESIGN TABLES =================================
  const CHAR92 = { rig: 'characterIsoRig9.js', exportSymbol: 'CharacterIso9', revision: '9.2', radiusMax: 0.229, footprintMax: [0.458, 0.370],
    fits: { bench: [0.643, 0.853], knife: [0.598, 0.760], load: [0.718, 0.978], rest: [0, 1.012], rail: [0.549, 0.724], rung: [0.211, 0.273], stepDown: [0.06, 1.30], stepUp: [0.549, 0.724], stepUpNoGrip: [0.06, 1.30] },
    rung: 0.24, standoff: 0.275, topOut: 0.72, topAdvance: 0.60, walk: 0.727, clearDoor: 0.758, headroom: 2.0, bedZ: 0.30,
    stair: { riserMax: 0.19, goingMin: 0.26, widthMin: 0.9, outsideRiserMax: 0.2 }, source: 'export/character-v9.2-import-kit/reports/worldfit.txt, every creator body (375)' };
  const fitOf = (name, v) => { const r = CHAR92.fits[name]; return r ? { name, value: r3(v), range: r, ok: v >= r[0] - 1e-9 && v <= r[1] + 1e-9 } : null; };
  // pass 2's room rules: the headroom over every walked sole and under every doorway's head, a doorway's clear width, and the
  // bed, counter and cutting board heights over the sole; stair is the spec the rooms' flights are built to (the check on a
  // flight is v9.2's stair rule, see STAIRS)
  const ROOM_RULES = { headroom: CHAR92.headroom, door: 0.80, bed: CHAR92.bedZ, counter: 0.85, board: 0.72, stair: { rise: 0.18, going: 0.26, width: 0.90 } };
  function design() {
    const fd = (y) => r3(zd(y)), sq = { port: sequence('port'), stbd: sequence('stbd') };
    // where a point stands: the room whose sole is at its height and whose plan holds it; a deck or room id's ADR level
    const roomAt = (p) => (ROOMS.find(r => Math.abs(r.sole - p[2]) < 0.05 && p[0] >= r.x[0] - 1e-6 && p[0] <= r.x[1] + 1e-6 && p[1] >= r.y[0] - 1e-6 && p[1] <= r.y[1] + 1e-6) || {}).id || null;
    const lvOf = (id) => { const r = ROOMS.find(q => q.id === id); if (r) return r.lv; const d = DECKS.find(q => q.id === id); return d ? d.level : null; };
    const DECKS = [
      { id: 'swim_platform', level: 'main_deck', z: ZP, y: [r3(yT), Y_PLAT], note: 'teak, 0.66 m over the DWL; boarding from a tender alongside' },
      { id: 'aft_deck', level: 'main_deck', z: ZM, y: [Y_COAM, Y_SALON], note: 'under the 01 overhang (headroom ' + r3(Z01U - ZM) + ' m)' },
      { id: 'side_deck_port / _stbd', level: 'main_deck', z: ZM, y: [Y_SALON, Y_STEP], note: 'clear ' + r3(xin(-6) - HX_SAL) + ' m between the house and the bulwark' },
      { id: 'deck01_aft', level: 'bridge', z: Z01, y: [-15.40, -10.00], note: 'under the flybridge overhang aft to y −13.3' },
      { id: 'deck01_side_port / _stbd', level: 'bridge', z: Z01, y: [-10.00, 3.40], note: 'clear ' + r3(fSx(-4) - 0.07 - 3.05) + ' m, leads forward into the Portuguese bridge' },
      { id: 'portuguese_bridge', level: 'bridge', z: Z01, y: [3.25, 5.00], note: '1.25 m bulwark, gates ' + (GATES.width || 0) + ' m wide at the forward corners' },
      { id: 'foredeck', level: 'foredeck', z: [fd(Y_FD0), fd(17.0)], y: [r3(Y_FD0), 17.6], note: 'non-skid, rises with the sheer; tenders, crane, windlasses' },
      { id: 'fly_aft_deck', level: 'top', z: ZF, y: [-13.30, -6.80], note: 'spa, sun pads, settee; open to the sky' },
      { id: 'flybridge (enclosed)', level: 'top', z: ZF, y: [-6.80, -2.20], note: 'the enclosure: the upper helm to starboard, the lounge and the bar (room fly_lounge)' } ];
    const stairRow = (id, from, to, st, headroom, note) => { const ok = st.rise <= CHAR92.stair.riserMax + 1e-9 && st.going >= CHAR92.stair.goingMin - 1e-9 && st.width >= CHAR92.stair.widthMin - 1e-9 && headroom >= CHAR92.headroom;
      return { id, from, to, risers: st.risers, rise: st.rise, going: st.going, width: st.width, headroom: r3(headroom), ok, note }; };
    const STAIRS = [
      stairRow('platform_stair_port / _stbd', 'swim_platform', 'aft_deck', { risers: 5, rise: 0.18, going: 0.30, width: XSH - XC }, 99, 'solid treads in the transom channels'),
      stairRow('aft_to_01', 'aft_deck', 'deck01_aft', S3, 2.0, 'open flight to port; the 01 deck is open above it from the 4th tread'),
      stairRow('01_to_fly', 'deck01_aft', 'fly_aft_deck', S4, 2.0, 'open flight to starboard; the flybridge deck is open above it from the 4th tread'),
      stairRow('bridge_step_port / _stbd', 'portuguese_bridge', 'foredeck', { risers: 2, rise: r3((Z01 - zd(5.0)) / 2), going: 0.34, width: GATES.width || 0 }, 99, 'one tread outside each gate') ];
    // the rooms' flights, closed on solid bases: v9.2's stair rule is the check, ROOM_RULES.stair the spec they are built to
    for (const f of IFLIGHTS) STAIRS.push(Object.assign(stairRow(f.id, roomAt(f.foot) || f.lv, roomAt(f.top) || f.lv, f, f.headroom, f.risers + ' risers of ' + f.rise +
      (f.rise > ROOM_RULES.stair.rise + 1e-9 ? ', over the ' + ROOM_RULES.stair.rise + ' spec (whole risers in the drop); v9.2 walks to ' + CHAR92.stair.riserMax : ', on the spec')), { lv: f.lv, foot: f.foot.slice(), top: f.top.slice() }));
    const S = (id, label, deck, stand, face, clip, fixture, target, operates, note) => ({ id, label, deck, lv: lvOf(deck), stand: stand.map(r3), face, clip, fixture, fit: fixture ? fitOf(fixture.name, fixture.value) : null, target: target ? target.map(r3) : null, operates, note });
    const eyeZ = (side) => TEND[side].z + 0.74 - zd(TEND[side].y + 2.50);
    const STATIONS = [
      S('helm_pilothouse', 'Pilothouse helm', 'pilothouse', [0, 2.30, Z01], [0, 1], 'helm_idle', { name: 'rest', value: 1.00 }, [0, 2.62, Z01 + 1.00], 'the ship: wheel, throttles, thrusters', 'stand-up helm; the helm chairs are furniture (no seated clip fits a 0.6 m seat)'),
      S('helm_flybridge', 'Flybridge helm', 'fly_lounge', [1.00, -3.25, ZF], [0, 1], 'helm_idle', { name: 'rest', value: 1.00 }, [1.00, -2.62, ZF + 1.00], 'the ship (upper station)', 'to starboard in the enclosure; stand-up, the helm chair is furniture'),
      S('crane_controls', 'Crane controls', 'foredeck', [0, CR.controls[1] - 0.45, zd(CR.controls[1] - 0.45)], [0, 1], 'bench', { name: 'bench', value: 0.80 }, [0, CR.controls[1], zd(CR.controls[1]) + 0.80], 'crane: slew, luff, telescope, hoist winch', 'bench stands in for a lever clip (requests)'),
      S('hook_port', 'Hook on — port tender', 'foredeck', [TEND.port.x, TEND.port.y + 2.95, zd(TEND.port.y + 2.95)], [0, -1], 'reach', { name: 'rest', value: r3(eyeZ('port')) }, [TEND.port.x, TEND.port.y + 2.50, TEND.port.z + 0.74], 'sling to the bow eye; the stern eyes from either quarter', 'the operator lowers the hook to the sling ring before this'),
      S('hook_stbd', 'Hook on — starboard tender', 'foredeck', [TEND.stbd.x, TEND.stbd.y + 2.95, zd(TEND.stbd.y + 2.95)], [0, -1], 'reach', { name: 'rest', value: r3(eyeZ('stbd')) }, [TEND.stbd.x, TEND.stbd.y + 2.50, TEND.stbd.z + 0.74], 'sling to the bow eye', ''),
      S('tagline', 'Tag line (steady the tender)', 'foredeck', [2.95, 10.40, zd(10.40)], [1, 0], 'haul', null, null, 'a line to the tender bow while it swings out', 'haul stands in for a tag-line clip (requests)'),
      S('windlass_port', 'Windlass, port', 'foredeck', [-0.62, 14.80, zd(14.80)], [0, 1], 'reach', { name: 'rest', value: 0.45 }, [-0.62, 15.20, zd(15.2) + 0.45], 'port anchor: brake and gypsy', ''),
      S('windlass_stbd', 'Windlass, starboard', 'foredeck', [0.62, 14.80, zd(14.80)], [0, 1], 'reach', { name: 'rest', value: 0.45 }, [0.62, 15.20, zd(15.2) + 0.45], 'starboard anchor', ''),
      S('lines_stern_port', 'Stern line, port', 'aft_deck', [-3.20, -14.90, ZM], [-1, 0], 'haul', null, [-3.75, -14.90, fSz(-14.9) + 0.06], 'quarter cleat on the cap rail', ''),
      S('lines_stern_stbd', 'Stern line, starboard', 'aft_deck', [3.20, -14.90, ZM], [1, 0], 'haul', null, [3.75, -14.90, fSz(-14.9) + 0.06], 'quarter cleat on the cap rail', ''),
      S('lines_bow', 'Bow lines', 'foredeck', [0, 16.00, zd(16.0)], [0, 1], 'haul', null, [0.95, 16.23, zd(16.2) + 0.08], 'bow cleats', '') ];
    // the rooms' stations: counters on v9.2's bench fit, the galley board on its knife fit, the beds at the room rule. v9.2
    // has no lie-down clip: reach at the bed top stands in (REQUESTS). want is the room rule the fixture is built to
    const RK = { sleep: ['reach', 'bed', 'the bed'], bench: ['bench', 'counter', 'the counter'], chop: ['chop', 'board', 'the cutting board'] };
    for (const s of ISTN) { const [clip, rule, what] = RK[s.kind];
      STATIONS.push({ id: s.id, label: s.label, deck: roomAt(s.stand) || s.lv, lv: s.lv, stand: s.stand.slice(), face: s.face.slice(), clip, fixture: { name: s.fixture, value: s.fixtureZ },
        fit: fitOf(s.fixture, s.fixtureZ), want: ROOM_RULES[rule], target: [r3(s.stand[0] + s.face[0] * 0.45), r3(s.stand[1] + s.face[1] * 0.45), r3(s.stand[2] + s.fixtureZ)],
        operates: what, note: s.kind === 'sleep' ? 'reach stands in for a lie-down clip (requests)' : '' }); }
    const BOARDING = [
      { id: 'platform_from_tender', deck: 'swim_platform', stand: [0, r3(yT + 0.45), ZP], face: [0, -1], clip: 'stepUpNoGrip / stepDown', step: r3(ZP - (DWL - TFLOAT + (root.YachtTenderIso ? root.YachtTenderIso.DIMS.sole : 0.42))), fit: fitOf('stepUpNoGrip', ZP - (DWL - TFLOAT + (root.YachtTenderIso ? root.YachtTenderIso.DIMS.sole : 0.42))), note: 'tender sole alongside the platform edge; the gate in the aft rail is 1.10 m' },
      { id: 'swim_ladder', deck: 'swim_platform', stand: [0, r3(yT + 0.30), ZP], face: [0, -1], clip: 'ladderDown', rung: CHAR92.rung, rungs: Math.ceil((ZP - DWL + 0.6) / CHAR92.rung), fit: fitOf('rung', CHAR92.rung), bottom: LADDER.bottom, note: 'telescopic ladder under the platform, centred in the rail gap; opts.ladder hangs it ' + r3(DWL - LADDER.bottom) + ' m below the waterline' },
      { id: 'side_gate_port / _stbd', deck: 'side_deck', stand: [r3(xin(-6.1) - 0.35), -6.10, ZM], face: [1, 0], clip: 'stepDown / stepUpNoGrip', clear: r3(GATE.y1 - GATE.y0), gate: [GATE.y0, GATE.y1], note: 'bulwark gate, ' + r3(GATE.y1 - GATE.y0).toFixed(2) + ' m clear, for a wharf at deck height ±1.3 m; opts.gates folds the leaf forward' } ];
    const DOORS = [
      { id: 'salon_aft', where: 'main, aft wall', clear: 2.30, kind: 'slider (glass)', ok: true }, { id: 'side_deck_fwd_port / _stbd', where: 'main, forward end of each side deck', clear: 0.76, kind: 'hinged', ok: 0.76 >= CHAR92.clearDoor },
      { id: 'salon_side_port / _stbd', where: 'main, house side y −2.42…−1.62', clear: 0.80, kind: 'hinged (dressed)', ok: 0.80 >= CHAR92.clearDoor, note: 'widened from 0.58 in step 0' },
      { id: 'side_gate_port / _stbd', where: 'main, bulwark y ' + GATE.y0.toFixed(2) + '…' + GATE.y1.toFixed(2), clear: r3(GATE.y1 - GATE.y0), kind: 'hinged gate (opts.gates)', ok: GATE.y1 - GATE.y0 >= CHAR92.clearDoor, note: 'the leaf folds forward flat against the lining' },
      { id: 'skylounge_aft', where: '01, aft wall', clear: 2.10, kind: 'slider (glass)', ok: true }, { id: 'captain_side', where: '01, port+stbd y −2.00…−1.20', clear: 0.80, kind: 'hinged', ok: 0.80 >= CHAR92.clearDoor, note: 'widened from 0.71 in step 0' },
      { id: 'pilothouse_wing_port / _stbd', where: '01, y 0.2', clear: 0.77, kind: 'hinged', ok: 0.77 >= CHAR92.clearDoor }, { id: 'fly_aft', where: 'fly, aft wall', clear: 0.90, kind: 'slider (glass)', ok: true } ];
    // the rooms' doorways (no leaves): ROOM_RULES.door clear, ROOM_RULES.headroom under the head
    for (const d of IDOORS) DOORS.push({ id: d.id, where: d.lv + ', ' + (d.axis === 0 ? 'x ' + d.at + ', y ' : 'y ' + d.at + ', x ') + d.u[0] + '…' + d.u[1], clear: d.clear, head: d.head,
      kind: 'doorway', lv: d.lv, rule: ROOM_RULES.door, ok: d.clear >= ROOM_RULES.door - 1e-9 && d.head >= ROOM_RULES.headroom - 1e-9 });
    const REQUESTS = [
      { clip: 'sit', why: 'settees, sun pads, spa, helm chairs, the tender seats, and the sofas, chairs and stools in the rooms', now: 'furniture only; nobody sits until a sit clip lands' },
      { clip: 'stairWalk', why: '0.18 m risers', now: 'walk at 0.727 m/s with root z linear from nosing to nosing (as the wharf and lighthouse kits)' },
      { clip: 'leverWork', why: 'crane control levers', now: 'bench at 0.80 (fits every creator body)' },
      { clip: 'tagLine', why: 'steadying the tender while it swings', now: 'haul, stroke held' },
      { clip: 'lieDown', why: 'the beds and berths (top ' + ROOM_RULES.bed + ' over the sole)', now: 'reach at the bed top; nobody lies down until a lie-down clip lands' } ];
    // the rooms: level, sole, ceiling, plan and headroom; the forepeak and the engine room are drawn, not walked (_excluded)
    const RM = ROOMS.map(r => Object.assign({}, r, { x: r.x.slice(), y: r.y.slice(), headroom: r3(r.ceil - r.sole) }));
    // the mesh budget, closed and stowed: vertices are Σ f.v.length, each mesh under vertMax; ramps per mesh, the hull's at most
    // 16 and the room's 24
    const MT = matsFor('champagne').MATS, mesh = (fl, rampMax) => { const m = new Set(), r = new Set(); let v = 0; fl.forEach(f => { v += f.v.length; m.add(f.mat); r.add((MT[f.mat] || MT.hull).ramp); });
      return { faces: fl.length, verts: v, mats: m.size, ramps: r.size, rampMax }; };
    const MESH = { exterior: mesh(faceList({}).concat(doorFaces(0)), 16), room: mesh(IF, 24), vertMax: 65535 };
    MESH.whole = { verts: MESH.exterior.verts + MESH.room.verts }; MESH.whole.note = MESH.whole.verts > MESH.vertMax ? 'one mesh would be over vertMax; the exterior and the room are each under it' : 'under vertMax as one mesh';
    const checks = [];
    for (const s of STATIONS) if (s.fit && !s.fit.ok) checks.push('station ' + s.id + ': ' + s.fit.name + ' ' + s.fit.value + ' outside ' + s.fit.range.join('–'));
    for (const s of STATIONS) if (s.want != null && Math.abs(s.fixture.value - s.want) > 0.005) checks.push('station ' + s.id + ': ' + s.fixture.value + ' over the sole, the room rule ' + s.want);
    for (const r of RM) if (r.walk && r.headroom < ROOM_RULES.headroom - 1e-9) checks.push('room ' + r.id + ': headroom ' + r.headroom + ', under ' + ROOM_RULES.headroom);
    for (const s of STAIRS) if (!s.ok) checks.push('stair ' + s.id + ': outside the v9.2 stair rule');
    for (const d of DOORS) if (!d.ok) checks.push('door ' + d.id + ': ' + d.clear + ' m clear' + (d.head != null ? ', head ' + d.head : '') + '; the rule ' + (d.rule || CHAR92.clearDoor) + (d.head != null ? ' clear, ' + ROOM_RULES.headroom + ' head' : ''));
    for (const b of BOARDING) if (b.fit && !b.fit.ok) checks.push('boarding ' + b.id + ': outside ' + b.fit.range.join('–'));
    for (const k of ['port', 'stbd']) { const Sq = sq[k]; for (const K of Sq.keys) { if (K.pose.ext < -1e-6 || K.pose.ext > CR.extMax + 1e-6) checks.push('crane ' + k + ' ' + K.id + ': telescope ' + r3(K.pose.ext) + ' outside 0–' + CR.extMax); } }
    const fc = fitCheck(0.01); if (fc.outside) checks.push('fit: ' + fc.outside + ' room faces outside the shell, worst ' + fc.worst + ' m');
    for (const k of ['exterior', 'room']) { const m = MESH[k]; if (m.verts > MESH.vertMax) checks.push('mesh ' + k + ': ' + m.verts + ' vertices, over ' + MESH.vertMax); if (m.ramps > m.rampMax) checks.push('mesh ' + k + ': ' + m.ramps + ' ramps, over ' + m.rampMax); }
    return { CHAR92, ROOM_RULES, DECKS, ROOMS: RM, STAIRS, STATIONS, BOARDING, DOORS, REQUESTS, MESH, CRANE: CR, TENDERS: TEND, SEQUENCE: sq, checks };
  }
  // ================================= ADR 0048: THE ROOM'S FIT, THE LEVELS AND THE CUT =================================
  // fitCheck(tol): every room face inside the shell. The shell is five containers, each over its own heights: the hull
  // under its sheer, the salon house, the forward section under the 01 slab, the 01 house and the fly enclosure. A corner
  // stands out by |x| less the half-breadth of the best container at its y and z (Infinity when none spans it); a face is
  // outside when a corner stands out by more than tol. It returns { tol, faces, vertices, outside (faces), worst (the
  // furthest corner past the shell, m; 0 when every corner is inside), worstAt (that corner, or null) }: §6.11's qa.fitCheck.
  const FIT = [
    (y, z) => (y >= yT && y <= yB && z >= 0 && z <= fSz(y) + 0.02) ? halfAtZ(y, z) : 0,
    (y, z) => (y >= Y_SALON - 0.02 && y <= Y_STEP + 0.02 && z >= ZM - 0.02 && z <= Z01 + 0.02) ? HX_SAL : 0,
    (y, z) => (y >= Y_STEP - 0.02 && z >= ZM - 0.02 && z <= Z01 + 0.02) ? hwAt(O01, y) : 0,
    (y, z) => (z >= Z01 - 0.02 && z <= ZF + 0.02) ? hwAt(ringAt(H01, z).P, y) : 0,
    (y, z) => (z >= ZF - 0.02 && z <= ZHT) ? hwAt(ringAt(FLY, z).P, y) : 0 ];
  function outBy(p) { let best = Infinity; for (const c of FIT) { const h = c(p[1], p[2]); if (h > 0) best = Math.min(best, Math.abs(p[0]) - h); } return best; }
  function fitCheck(tol) { tol = tol == null ? 0.01 : tol; let worst = 0, worstAt = null, outside = 0, vertices = 0;
    for (const f of IF) { let m = -Infinity; for (const p of f.v) { vertices++; const e = outBy(p); m = Math.max(m, e); if (e !== Infinity && e > worst) { worst = e; worstAt = p.map(r3); } }
      if (m > tol) outside++; }
    return { tol, faces: IF.length, vertices, outside, worst: r3(worst), worstAt: r3(worst) > 0 ? worstAt : null }; }

  // The cut, per key (where the player stands; the level it shows). Ghost: the levels above lift whole, with the shown
  // level's roof; its walls stay, ghosted near the player (GHOST, the rule below). Gate, in §6.4's lift-set form: the
  // ghost's levels, and the shown level's exterior walls and roof. The master suite lies under the foredeck, so the house
  // lifts it too. The lower deck has no exterior and no roof of its own: both modes lift every level above it. A key's
  // player is where its preview stands (the aft deck, the salon, the sky lounge, the fly helm, the lower foyer).
  const CUT_KEYS = { deck: { shows: null, lift: [], player: [0, -14.00, ZM] }, house: { shows: 'house', lift: ['bridge', 'top', 'rigging', 'foredeck'], player: [-2.20, -8.50, ZM] },
    bridge: { shows: 'bridge', lift: ['top', 'rigging'], player: [0, -7.60, Z01] }, fly: { shows: 'top', lift: ['rigging'], player: [1.00, -3.25, ZF] },
    below: { shows: 'below', lift: ['main_deck', 'house', 'bridge', 'top', 'rigging', 'foredeck'], player: [0, 2.30, ZBELOW] } };
  const CUTAWAY = { ghost: {}, gate: {} };
  for (const k in CUT_KEYS) { const c = CUT_KEYS[k], s = c.shows && c.shows !== 'below' ? c.shows : null;
    CUTAWAY.ghost[k] = c.lift.concat(s ? [s + ':roof'] : []); CUTAWAY.gate[k] = c.lift.concat(s ? [s + ':wall', s + ':roof'] : []); }
  const GHOST = { rx: 1.25, ry: 1.10, above: 0.95, margin: 0.20, core: 0.60 };
  const GHOST_RULE = 'role ∈ ghostRoles && depth < playerDepth − ' + GHOST.margin.toFixed(2) + ' m && inside the reveal ellipse (rx ' + GHOST.rx.toFixed(2) + ' m, ry ' + GHOST.ry.toFixed(2) +
    ' m of screen at 32 px/m, centred ' + GHOST.above.toFixed(2) + ' m above the feet) → discard; solid < ' + GHOST.core.toFixed(2) + ' R, Bayer 4×4 dither ' + GHOST.core.toFixed(2) + '–1.0 R';
  const GHOST_RO = new Set(GHOST_ROLES.map(n => RO[n])), _lift = {};
  // the faces a cut lifts, from CUTAWAY's lists: a level whole ('bridge') or one role of it ('house:roof')
  function liftOf(mode, key) { const m = CUTAWAY[mode] ? mode : 'ghost', k = m + '|' + key; if (k in _lift) return _lift[k];
    const L = CUTAWAY[m][key]; if (!L) throw new Error('ExpeditionYacht120Iso: no cutaway level ' + key + ' (one of ' + Object.keys(CUT_KEYS).join(', ') + ')');
    const lv = new Set(), lr = new Set(); L.forEach(s => { const [a, b] = s.split(':'); if (b) lr.add(LV[a] * 16 + RO[b]); else lv.add(LV[a]); });
    return (_lift[k] = L.length ? (f) => lv.has(f.lv) || lr.has(f.lv * 16 + f.ro) : null); }
  // the ghost's screen ellipse: centred GHOST.above over the player's feet, rx × ry m at S px/m; a ghost-role face nearer the
  // camera than the player less the margin is discarded inside it (solid to core, Bayer-dithered beyond)
  function ghostParams(B, p, g) { g = Object.assign({}, GHOST, g || {}); const q = Array.isArray(p) ? p : [p.x, p.y, p.z], c = projVert(q[0], q[1], q[2] + g.above, B, G);
    return { cx: c.sx, cy: c.sy, irx: 1 / (g.rx * S), iry: 1 / (g.ry * S), dP: c.d - g.margin, core: g.core, icore: 1 / (1 - g.core) }; }
  function geometry() {
    const lev = (id, deck, sole, ceil, of, more) => Object.assign({ id, deck, sole: r3(sole), ceil: ceil == null ? null : r3(ceil), kind: ceil == null ? 'open' : 'hard', soleZ: r3(sole), ceilingZ: ceil == null ? null : r3(ceil),
      ceiling: ceil == null ? { kind: 'open', lid: null, note: of } : { kind: 'hard', lid: (more && more.lid) || null, z: r3(ceil), of } }, more && more.note ? { note: more.note } : {});
    return { schema: 'hidden-harbours/hull-geometry@1', rig: RIG_FILE, exportSymbol: EXPORT_SYMBOL, hull: HULL_ID, units: 'metres', frame: FRAME,
      ids: Object.assign({}, LV), roles: Object.assign({}, RO), ghostRoles: GHOST_ROLES.slice(), dwlZ: DWL,
      riggingClass: 'rigging: mast, yard, domes, radar, whips (by class); the hardtop is the top level\'s roof (top:roof)',
      levels: [
        lev('below', 'below_sole', ZBELOW, ZM - 0.15, 'the main deck: the salon\'s sole (house) and the aft deck (main_deck)', { lid: 'house', note: 'lower deck: aft of the engine room, a block on the lazarette\'s sole 2.08 (lazarette, guest cabins, laundry, linen, aft lobby), engine room, VIP suites, crew forward; the engine room and the forepeak are drawn, not walked (_excluded)' }),
        lev('main_deck', 'aft_deck', ZM, null, 'the aft and side decks, under the 01 overhang (ghosted, not cut); the swim platform is at ' + ZP.toFixed(2)),
        lev('house', 'house_sole', ZM, Z01U, 'the 01 slab\'s underside, role roof on lv house; the 01 deck above is lv bridge. Forward, the master suite is under the foredeck (lv foredeck)'),
        lev('bridge', 'bridge_sole', Z01, ZFU, 'the fly slab\'s underside, role roof on lv bridge; the fly above is lv top'),
        lev('top', 'top_sole', ZF, ZHT - 0.14, 'the hardtop over the enclosure, role roof on lv top; the spa deck aft is sky'),
        lev('foredeck', 'foredeck', zd(Y_FD0), null, 'the foredeck is sky; it rises with the sheer to ' + r3(zd(17.6)) + ' at the bow') ],
      cutaway: { ghost: copyLists(CUTAWAY.ghost), gate: copyLists(CUTAWAY.gate), shows: Object.fromEntries(Object.entries(CUT_KEYS).map(([k, c]) => [k, c.shows])), note: CUT_NOTE,
        players: Object.fromEntries(Object.entries(CUT_KEYS).map(([k, c]) => [k, c.player.map(r3)])), ghostRule: GHOST_RULE, ghostParams: Object.assign({}, GHOST) },
      door: clone(doorsOf().door), parts: partsList(), states: Object.assign({}, STATES) };
  }
  const copyLists = (m) => Object.fromEntries(Object.entries(m).map(([k, v]) => [k, v.slice()]));
  const CUT_NOTE = 'one key per place the player stands: deck (the open decks: nothing lifts), house (the main deck\'s rooms: the 01, the fly, the rigging ' +
    'and the foredeck lift whole, and the house\'s roof), bridge (the 01\'s rooms: the fly and the rigging lift, and the bridge\'s roof), fly (the flybridge ' +
    'enclosure: the rigging lifts, and the top\'s roof) and below (the lower deck: every level above it lifts whole; it has no exterior and no roof of its own). ' +
    'Ghost lifts the shown level\'s roof and ghosts its walls round the player; gate lifts its exterior walls too. The gate lists are complete lift sets in ' +
    'ghost\'s vocabulary.';

  // ================================= HULL-LAYOUT-1: THE NAMES, THE DOORS, THE PARTS, THE CUT =================================
  const HULL_ID = 'expedition_yacht_120', RIG_FILE = 'expeditionYacht120IsoRig.js', EXPORT_SYMBOL = 'ExpeditionYacht120Iso', LABEL = 'EXPEDITION YACHT 120';
  const FRAME = 'metres; +x starboard, +y bow, +z up; origin amidships, keel bottom, centreline; polygons counter-clockwise seen from above';
  // the walker: radius = the largest collider of character rig 10.3's cast (0.23 m), stepTol = v3's, as on the fleet's walks
  const WALK = { radius: 0.23, stepTol: 0.34 }, COCKPIT_Z = r3(ZM), LOA_TEXT = '36.75 m · 120′7″', LOA_M = L;
  const clone = (o) => JSON.parse(JSON.stringify(o));
  // the most common level and role of a set of faces (a part's or a leaf's tags), and every role it has; none drawn (no
  // YachtTenderIso loaded), the tenders' own tags
  function tagsOf(faces) { if (!faces.length) return { lv: 'foredeck', ro: 'fit', roles: ['fit'] };
    const lv = {}, ro = {}; for (const f of faces) { lv[f.lv] = (lv[f.lv] || 0) + 1; ro[f.ro] = (ro[f.ro] || 0) + 1; }
    const top = (m) => +Object.keys(m).sort((a, b) => m[b] - m[a] || a - b)[0];
    return { lv: LV_NAME[top(lv)], ro: RO_NAME[top(ro)], roles: Object.keys(ro).map(Number).sort((a, b) => a - b).map(k => RO_NAME[k]) }; }
  // a set of faces' extent in plan and height
  function boxOf(faces) { const b = { x0: Infinity, x1: -Infinity, y0: Infinity, y1: -Infinity, z0: Infinity, z1: -Infinity };
    for (const f of faces) for (const p of f.v) { b.x0 = Math.min(b.x0, p[0]); b.x1 = Math.max(b.x1, p[0]); b.y0 = Math.min(b.y0, p[1]); b.y1 = Math.max(b.y1, p[1]); b.z0 = Math.min(b.z0, p[2]); b.z1 = Math.max(b.z1, p[2]); }
    return b; }
  // The doors (the walk's house.door and house.doors[]): one per opening with a leaf, a slider pair one door of two leaves.
  // A door in an end wall has the spec's fields as the cruiser's companionway does: y the wall, x0..x1 the opening across it.
  // A door in a side wall (face port / starboard) keeps them a box in the hull's frame: x0..x1 the leaf's thickness through
  // the wall at x, y0..y1 the opening along it, y its middle. sillZ is the opening's foot. clearAt is the t at which the
  // opening first passes the walker (2 × WALK.radius): a hinged leaf of width w leaves w (1 − cos θ), a slider its travel × t
  // (each leaf of a pair), a gate folds through 180° about its forward end. keepClear is the hinged leaf's swept arc in plan.
  // The gates' openings are open above the bulwark, to the 01's side deck over them (z1).
  const DOOR_SET = [
    { id: 'salon_aft', leaves: ['salon_aft_port', 'salon_aft_stbd'], label: 'Salon doors', note: 'the salon\'s aft doors onto the aft deck: a glass slider pair, each leaf sliding outboard over its fixed panel' },
    { id: 'side_deck_fwd_port', leaves: ['side_deck_fwd_port'], label: 'Side deck door', note: 'at the forward end of the port side deck, into the house\'s forward section (the foyer and the day head)' },
    { id: 'side_deck_fwd_stbd', leaves: ['side_deck_fwd_stbd'], label: 'Side deck door', note: 'at the forward end of the starboard side deck, into the house\'s forward section (the galley)' },
    { id: 'salon_side_port', leaves: ['salon_side_port'], label: 'Salon side door', note: 'from the port side deck into the foyer' },
    { id: 'salon_side_stbd', leaves: ['salon_side_stbd'], label: 'Salon side door', note: 'from the starboard side deck into the dining space' },
    { id: 'gate_port', leaves: ['gate_port'], label: 'Boarding gate', note: 'the port bulwark gate, for a wharf at deck height; the leaf folds forward flat against the lining (render opts.gates)' },
    { id: 'gate_stbd', leaves: ['gate_stbd'], label: 'Boarding gate', note: 'the starboard bulwark gate, for a wharf at deck height; the leaf folds forward flat against the lining (render opts.gates)' },
    { id: 'skylounge_aft', leaves: ['skylounge_aft_port', 'skylounge_aft_stbd'], label: 'Sky lounge doors', note: 'the sky lounge\'s aft doors onto the 01 aft deck: a glass slider pair' },
    { id: 'captain_port', leaves: ['captain_port'], label: 'Side door', note: 'from the port 01 side deck into the stair hall' },
    { id: 'captain_stbd', leaves: ['captain_stbd'], label: 'Captain\'s door', note: 'from the starboard 01 side deck into the captain\'s cabin' },
    { id: 'wing_port', leaves: ['wing_port'], label: 'Wing door', note: 'the pilothouse\'s port wing door, onto the 01 side deck' },
    { id: 'wing_stbd', leaves: ['wing_stbd'], label: 'Wing door', note: 'the pilothouse\'s starboard wing door, onto the 01 side deck' },
    { id: 'fly_aft', leaves: ['fly_aft'], label: 'Flybridge door', note: 'the flybridge enclosure\'s aft door onto the fly aft deck: one glass slider' } ];
  let _doors = null;
  function doorsOf() { if (_doors) return _doors;
    const need = 2 * WALK.radius, doors = [], by = {};
    for (const S of DOOR_SET) { const Ls = S.leaves.map(id => LEAF[id]), D = Ls[0], faces = [].concat(...Ls.map(l => l.faces)), T = tagsOf(faces), b = boxOf(faces);
      const side = D.n === 0, face = side ? (D.w < 0 ? 'port' : 'starboard') : (D.o[1] < 0 ? 'aft' : 'forward'), z0 = r3(Math.min(...Ls.map(l => l.z0))), z1 = r3(Math.max(...Ls.map(l => l.z1)));
      const u0 = Math.min(...Ls.map(l => l.u0)), u1 = Math.max(...Ls.map(l => l.u1)), d = { id: S.id, kind: D.kind === 'gate' ? 'hinge' : D.kind, face };
      if (side) Object.assign(d, { y: r3((u0 + u1) / 2), x0: r3(b.x0), x1: r3(b.x1), x: r3(D.w), y0: r3(u0), y1: r3(u1) }); else Object.assign(d, { y: r3(D.w), x0: r3(u0), x1: r3(u1) });
      d.z0 = z0; d.z1 = D.kind === 'gate' ? r3(gateHead(D)) : z1; d.sillZ = z0;
      let width, clearAt;
      if (D.kind === 'slide') { width = r3(Ls.reduce((s, l) => s + Math.min(l.travel, l.u1 - l.u0), 0)); clearAt = r3(Math.min(1, need / Ls.reduce((s, l) => s + l.travel, 0)));
        Object.assign(d, { travel: D.travel, opensTo: face, clearAt, leaves: S.leaves.slice() }); }
      else { const w = D.u1 - D.u0, deg = D.kind === 'gate' ? 180 : (D.swing || 90), th = Math.acos(clamp(1 - need / w, -1, 1)) / DEG; width = r3(w); clearAt = r3(Math.min(1, th / deg));
        const hx = side ? D.w : D.hu, hy = side ? D.hu : D.w;
        const g = D.kind === 'gate' ? gateHinge(D) : [hx, hy];
        Object.assign(d, { hinge: { x: r3(g[0]), y: r3(g[1]), z0, z1 }, maxDeg: deg, swings: D.kind === 'gate' ? 'forward, folding flat against the lining' : (side ? face : 'aft') + ', outward',
          opensTo: face, clearAt, keepClear: sweptArc(D, g, deg) }); }
      Object.assign(d, { leaf: 'doorFaces({ ' + S.leaves.map(l => l + ': t').join(', ') + ' })', lv: T.lv, ro: T.ro, label: S.label,
        clear: { width, height: r3(d.z1 - d.sillZ) }, note: S.note + (T.roles.length > 1 ? '; its leaf ' + (S.leaves.length > 1 ? 'faces are' : 'is') + ' tagged ' + T.lv + ' / ' + T.roles.join(', ') : '') });
      doors.push(d); by[d.id] = d; }
    return (_doors = { door: by.salon_aft, doors, by }); }
  // a gate's hinge: its forward end, 0.02 m inboard of the lining (gatePose); the opening's head: the 01's side deck over it
  function gateHinge(D) { return [D.side * (xin(GATE.y1) - 0.02), GATE.y1]; }
  function gateHead(D) { const x = D.side * (xin((GATE.y0 + GATE.y1) / 2) - 0.30), y = (GATE.y0 + GATE.y1) / 2; let z = Infinity;
    for (const f of F) { const b = boxOf([f]); if (b.z0 > D.z1 + 0.05 && b.z0 < z && x >= b.x0 && x <= b.x1 && y >= b.y0 && y <= b.y1) z = b.z0; }
    return z === Infinity ? D.z1 : z; }
  // a hinged leaf's swept arc in plan: the hinge, then the leaf's free end from shut to maxDeg (counter-clockwise)
  function sweptArc(D, g, deg) { const pose = (t) => leafPose(D, t), b0 = boxOf(D.faces), w = D.kind === 'gate' ? D.u1 - D.u0 : D.u1 - D.u0;
    const e = D.kind === 'gate' ? [0, -1] : (D.n === 0 ? [0, Math.sign((D.u0 + D.u1) / 2 - D.hu)] : [Math.sign((D.u0 + D.u1) / 2 - D.hu), 0]);
    const out = D.kind === 'gate' ? [-D.side, 0] : [D.o[0], D.o[1]], pts = [[r3(g[0]), r3(g[1])]], N = 8;
    for (let i = 0; i <= N; i++) { const a = deg * DEG * i / N, c = Math.cos(a), s = Math.sin(a);
      pts.push([r3(g[0] + w * (e[0] * c + out[0] * s)), r3(g[1] + w * (e[1] * c + out[1] * s))]); }
    let A = 0; for (let i = 0; i < pts.length; i++) { const p = pts[i], q = pts[(i + 1) % pts.length]; A += p[0] * q[1] - q[0] * p[1]; }
    return A < 0 ? [pts[0]].concat(pts.slice(1).reverse()) : pts; }
  // the hull-layout parts (geometry().parts): the posed pieces, the state that moves them, and the leaves
  const STATES = { tenders: 'true|false: both tenders on their chocks (true, the default) or neither (false); an array names the sides aboard',
    launch: '-1..1: the crane\'s launch sequence; the sign is the side (- port, + starboard), |t| the sequence\'s t (0 stowed, 1 afloat); { side, t } as well',
    ladder: 'true|false: the swim ladder hung off the platform\'s aft edge into the water (false, the default: stowed, only its head plate shows)',
    doorOpen: '0..1 (render option; the leaves are doorFaces(t), t a number or { leaf id: t })',
    gates: '0..1 (render option; the boarding gates\' leaves fold forward, a number or { port, stbd })' };
  let _parts = null;
  function partsList() { if (_parts) return clone(_parts); const D = doorsOf(), leafDoor = {}; for (const d of D.doors) for (const l of (d.leaves || [d.id])) leafDoor[l] = d.id;
    const t = (fs) => tagsOf(fs), cr = t(craneFaces(STOW).faces), tp = t(partFaces('tender_port', {})), ts = t(partFaces('tender_stbd', {})), ld = t(LADDER_DEP);
    _parts = [
      { id: 'crane', group: 'state', state: 'launch', lv: cr.lv, ro: cr.ro, alsoRoles: cr.roles.filter(r => r !== cr.ro), base: [CR.base[0], CR.base[1], r3(zd(CR.base[1]))], slewAxis: [0, 0, 1],
        boom_m: CR.boom, extMax_m: CR.extMax, swl_kg: CR.swl_kg, stow: Object.assign({}, STOW),
        note: 'the foredeck crane, posed per call: stowed boom-aft on its crutch, or through the launch sequence (state launch, sequence(side), launchState(side, t)); partFaces(\'crane\', state)' },
      { id: 'tender_port', group: 'state', state: 'tenders', lv: tp.lv, ro: tp.ro, alsoRoles: tp.roles.filter(r => r !== tp.ro), rig: 'YachtTenderIso', chocks: [TEND.port.x, TEND.port.y, TEND.port.z],
        note: 'the port tender: YachtTenderIso\'s faces on her chocks (her keel at the chocks\' z), or on the crane and afloat through launch -1..0; partFaces(\'tender_port\', state)' },
      { id: 'tender_stbd', group: 'state', state: 'tenders', lv: ts.lv, ro: ts.ro, alsoRoles: ts.roles.filter(r => r !== ts.ro), rig: 'YachtTenderIso', chocks: [TEND.stbd.x, TEND.stbd.y, TEND.stbd.z],
        note: 'the starboard tender, as tender_port, through launch 0..1' },
      { id: 'ladder', group: 'state', state: 'ladder', lv: ld.lv, ro: ld.ro, note: 'the swim ladder\'s rungs and grab loops, hung off the platform\'s aft edge (state ladder); stowed, only its head plate shows, in the exterior' } ]
      .concat(LEAVES.map(L0 => { const T = tagsOf(L0.faces); return { id: L0.id, group: 'doorFaces', door: leafDoor[L0.id], lv: T.lv, ro: T.ro, kind: L0.kind,
        note: 'doorFaces({ ' + L0.id + ': t }); its door is house.doors[] ' + leafDoor[L0.id] }; }));
    return clone(_parts); }
  // partFaces(id, state): a part's faces in a state: the crane, a tender, the swim ladder, one leaf, or a door's leaves
  function partFaces(id, state) { state = state || {};
    if (id === 'crane') { const ln = launchOf(state.launch); return craneFaces(ln ? launchState(ln.side, ln.t).pose : (state.crane || STOW)).faces; }
    if (id === 'tender_port' || id === 'tender_stbd') { const k = id.slice(7); return faceList(state).filter(f => f.side === k); }
    if (id === 'ladder') return state.ladder ? LADDER_DEP.slice() : [];
    if (LEAF[id]) return leafPose(LEAF[id], leafTimes(state)[id]);
    const d = doorsOf().by[id]; if (d) return [].concat(...(d.leaves || [d.id]).map(l => partFaces(l, state)));
    return []; }
  // renderCut(dir, key, opts): the exterior and the leaves less what the gate lifts under key (geometry().cutaway.gate), with
  // the shown level's room, its faces turned to the camera only, moved toward it by the hull's bounding diagonal (each face's
  // own db kept), as the bake places the room. renderCut(dir, 'deck', opts) is render(dir, opts).
  function renderCut(dir, key, opts) { opts = (typeof opts === 'number') ? { elev: opts } : (opts || {});
    if (!Object.prototype.hasOwnProperty.call(CUTAWAY.gate, key)) throw new Error('renderCut: no cut key ' + key);
    const lift = liftOf('gate', key), all = faceList(opts).concat(doorFaces(leafTimes(opts))), ext = lift ? all.filter(f => !lift(f)) : all, o = Object.assign({}, opts, { dir }), room = [];
    if (CUT_KEYS[key].shows) { const lv = LV[CUT_KEYS[key].shows], B = camBasis(o), b = boxOf(all), shift = Math.hypot(b.x1 - b.x0, b.y1 - b.y0, b.z1 - b.z0);
      for (const f of IF) { if (f.lv !== lv) continue; const rv = f.v.map(p => projVert(p[0], p[1], p[2], B, G)); let ny = 0, nz = 0;
        for (let i = 0; i < rv.length; i++) { const a = rv[i], c = rv[(i + 1) % rv.length]; ny += (a.zr - c.zr) * (a.xr + c.xr); nz += (a.xr - c.xr) * (a.yr + c.yr); }
        if (-ny * B.ce + nz * B.se < 0) continue;
        room.push(Object.assign({}, f, { db: (f.db || 0) + shift })); } }
    return _toRGBA(_paint(ext.concat(room), o, G, matsFor(opts)), G.W, G.H); }
  // ================================= THE WALK (pass 4): walk(), as hull-walk.mjs writes it =================================
  // walk() is the hull-layout walk (hidden-harbours/hull-walk@1, kind ship), every number read off the drawn faces: the
  // exterior as faceList({}) draws it (the tenders on their chocks, the crane stowed) and the rooms (IF). The leaves are
  // left out: a door is an area of its own. Each standing surface (a sole at its height, the gate treads, the foredeck
  // along its sheer) is rastered at WK.cell. A cell is floor where a sole lies within floorTol of the surface; a step where
  // a sole, a stair or a tread lies within stepTol over it (a step down where one lies within stepTol under it and no floor
  // covers it); an obstacle where anything else stands in the body's band over it. The walker's centre keeps radius +
  // margin off every obstacle and off the floor's edge (an exact distance transform), so the polygons come out
  // player-centre, already inset. What is left is labelled: the doors' openings; each room, the largest part of its rect
  // (a corner of the rect outside its walls is the deck's), then out across its doorways; the decks by design()'s rows,
  // each within its row's span, reaching into the rooms' rects only. Floor that no label reaches is a pocket, or, where it
  // lies beyond every row and rect (the house roof round the pilothouse), floor drawn beyond the decks: both _excluded. The
  // labels are cut into pieces, grown WK.grow cells over floor and steps so that neighbours overlap, traced and simplified
  // (tau). An indoor piece (a room's, a door's, a pocket in a room) stands at its level's sole, the finish over it (carpet,
  // teak) drawn and never walked (pass 6); a deck stands on its planks, the median of the faces under it.
  // The stairs are ramps of their own, run through their treads' centres; the foredeck is cut into chords of its
  // sheer, each within WK.chord of the drawn deck. A piece no walk from the aft deck reaches is _excluded with its reason,
  // as is what is drawn and not walked. A station whose stand no area comes within reachTol of is taken at its stand,
  // offWalk on the floor under it: its pocket, or its own _excluded floor where the floor is too tight (gaps_m).
  // band: what stands 0.06 to 1.90 m over the floor is an obstacle, so every build stands upright anywhere on the walk
  // (character 10.3's tallest crown is 1.864 m, and the writer wants 0.02 over it); up to head it is headroom. A door is
  // the exception: its lintel is headroom over its opening (the writer's through_ rows name the builds that duck).
  // sill: a door's sill is floor across its wall, between its jambs, sill m either side of its leaf (no face tops the
  // thresholds: they are drawn open).
  const WK = { cell: 0.01, margin: 0.03, band: [0.06, 1.90], floorTol: 0.03, head: 3.60, grow: 2, minCells: 400, minHole: 10, tau: 0.012, chord: 0.015,
    x0: -4.60, y0: -18.80, nx: 920, ny: 3760, pad: 0.40, sillTol: 0.06, sill: 0.15, doorHalf: 0.275, climbBand: 0.25, over: 0.66, reachTol: 0.15 };
  const WALK_RULE = 'A step is legal when the target point is inside an area polygon and outside its holes, and |area z - player z| < stepTol. ' +
    'Ramps interpolate z along their axis (x or y) between a0 and a1, and hold z0 / z1 beyond them. cut = the cutaway key while standing there; ' +
    'cutHi / cutLo replace it above / below zMid (stairs and ladders). Polygons are player-centre: already inset by radius, so the reader never insets them again.';
  let _walk = null;
  function walk() { if (!_walk) _walk = wkBuild(); return clone(_walk); }
  // the checker's tests, word for word: point in polygon (even-odd), distance to an edge, on an area, an area's z
  const wkPip = (pt, poly) => { let c = false; for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) { const a = poly[i], b = poly[j];
    if ((a[1] > pt[1]) !== (b[1] > pt[1]) && pt[0] < (b[0] - a[0]) * (pt[1] - a[1]) / (b[1] - a[1]) + a[0]) c = !c; } return c; };
  const wkSegD = (p, a, b) => { const dx = b[0] - a[0], dy = b[1] - a[1], l2 = dx * dx + dy * dy; let t = l2 ? ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / l2 : 0;
    t = Math.max(0, Math.min(1, t)); return Math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy); };
  const wkEdgeD = (p, poly) => { let d = Infinity; for (let i = 0; i < poly.length; i++) d = Math.min(d, wkSegD(p, poly[i], poly[(i + 1) % poly.length])); return d; };
  const wkOn = (a, x, y) => wkPip([x, y], a.poly) && !(a.holes || []).some((h) => wkPip([x, y], h));
  const wkZ = (a, x, y) => { if (a.z != null) return a.z; const R = a.ramp, v = R.axis === 'x' ? x : y;
    const t = Math.max(0, Math.min(1, (v - R.a0) / ((R.a1 - R.a0) || 1e-12))); return R.z0 + (R.z1 - R.z0) * t; };
  // the faces the walk reads, each with its unit normal (Newell's: up for a sole), a point of its plane and its box
  function wkFaces() { const out = [];
    for (const f of faceList({}).concat(IF)) { const v = f.v, n = v.length; if (n < 3) continue; let nx = 0, ny = 0, nz = 0, px = 0, py = 0, pz = 0; const b = [Infinity, -Infinity, Infinity, -Infinity, Infinity, -Infinity];
      for (let i = 0; i < n; i++) { const a = v[i], q = v[(i + 1) % n]; nx += (a[1] - q[1]) * (a[2] + q[2]); ny += (a[2] - q[2]) * (a[0] + q[0]); nz += (a[0] - q[0]) * (a[1] + q[1]);
        px += a[0]; py += a[1]; pz += a[2]; for (let k = 0; k < 3; k++) { if (a[k] < b[2 * k]) b[2 * k] = a[k]; if (a[k] > b[2 * k + 1]) b[2 * k + 1] = a[k]; } }
      const m = Math.hypot(nx, ny, nz); if (m < 1e-12) continue;
      out.push({ v, lv: f.lv, ro: f.ro, nx: nx / m, ny: ny / m, nz: nz / m, anz: Math.abs(nz / m), px: px / n, py: py / n, pz: pz / n, x0: b[0], x1: b[1], y0: b[2], y1: b[3], z0: b[4], z1: b[5] }); }
    return out; }
  const wkPlaneZ = (f, x, y) => f.pz - (f.nx * (x - f.px) + f.ny * (y - f.py)) / f.nz;
  // the lattice: cell (i, j) is centred on (x0 + (i + ½) cell, y0 + (j + ½) cell); a window W is rows j0…j0+nj−1 and columns
  // i0…i0+ni−1, its cell k = (j − j0) ni + i − i0
  const wkXc = (i) => WK.x0 + (i + 0.5) * WK.cell, wkYc = (j) => WK.y0 + (j + 0.5) * WK.cell;
  // the window's cells whose centres lie inside P in plan (even-odd, as wkPip): cb(k, x, y)
  function wkFill(P, W, cb) { const c = WK.cell; let ya = Infinity, yb = -Infinity; for (const p of P) { if (p[1] < ya) ya = p[1]; if (p[1] > yb) yb = p[1]; }
    const ja = Math.max(W.j0, Math.ceil((ya - WK.y0) / c - 0.5)), jb = Math.min(W.j0 + W.nj - 1, Math.floor((yb - WK.y0) / c - 0.5)), xs = [];
    for (let j = ja; j <= jb; j++) { const y = wkYc(j); xs.length = 0;
      for (let a = 0, b = P.length - 1; a < P.length; b = a++) { const p = P[a], q = P[b]; if ((p[1] > y) !== (q[1] > y)) xs.push((q[0] - p[0]) * (y - p[1]) / (q[1] - p[1]) + p[0]); }
      xs.sort((u, w) => u - w);
      for (let m = 0; m + 1 < xs.length; m += 2) { const ia = Math.max(W.i0, Math.ceil((xs[m] - WK.x0) / c - 0.5)), ib = Math.min(W.i0 + W.ni - 1, Math.ceil((xs[m + 1] - WK.x0) / c - 0.5) - 1);
        for (let i = ia; i <= ib; i++) cb((j - W.j0) * W.ni + i - W.i0, wkXc(i), y); } } }
  // the window's cells a segment passes through, sampled every third of a cell: cb(k, z)
  function wkSeg(p, q, W, cb) { const c = WK.cell, n = Math.max(1, Math.ceil(Math.hypot(q[0] - p[0], q[1] - p[1]) / (c / 3)));
    for (let s = 0; s <= n; s++) { const t = s / n, i = Math.floor((p[0] + (q[0] - p[0]) * t - WK.x0) / c), j = Math.floor((p[1] + (q[1] - p[1]) * t - WK.y0) / c);
      if (i >= W.i0 && i < W.i0 + W.ni && j >= W.j0 && j < W.j0 + W.nj) cb((j - W.j0) * W.ni + i - W.i0, p[2] + (q[2] - p[2]) * t); } }
  // a polygon of [x, y, z, d] points clipped to lo ≤ d ≤ hi (d: the height over the surface)
  function wkClip(P, lo, hi) { let R = P;
    for (const [s, k] of [[1, lo], [-1, hi]]) { const O = [];
      for (let a = 0; a < R.length; a++) { const p = R[a], q = R[(a + 1) % R.length], fp = s * (p[3] - k), fq = s * (q[3] - k);
        if (fp >= 0) O.push(p); if ((fp >= 0) !== (fq >= 0)) { const t = fp / (fp - fq); O.push([p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t, p[2] + (q[2] - p[2]) * t, p[3] + (q[3] - p[3]) * t]); } }
      R = O; if (R.length < 2) return []; }
    return R; }
  // the squared distance (cells²) from every cell to the nearest set cell of O: Felzenszwalb and Huttenlocher's exact transform
  function wkEDT(O, ni, nj) { const INF = 1e12, D = new Float64Array(ni * nj), n = Math.max(ni, nj), f = new Float64Array(n), g = new Float64Array(n), v = new Int32Array(n), z = new Float64Array(n + 1);
    const dt = (len) => { let k = 0; v[0] = 0; z[0] = -Infinity; z[1] = Infinity;
      for (let q = 1; q < len; q++) { let s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2 * q - 2 * v[k]);
        while (s <= z[k]) { k--; s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2 * q - 2 * v[k]); }
        k++; v[k] = q; z[k] = s; z[k + 1] = Infinity; }
      k = 0; for (let q = 0; q < len; q++) { while (z[k + 1] < q) k++; const r = q - v[k]; g[q] = r * r + f[v[k]]; } };
    for (let i = 0; i < ni; i++) { for (let j = 0; j < nj; j++) f[j] = O[j * ni + i] ? 0 : INF; dt(nj); for (let j = 0; j < nj; j++) D[j * ni + i] = g[j]; }
    for (let j = 0; j < nj; j++) { for (let i = 0; i < ni; i++) f[i] = D[j * ni + i]; dt(ni); for (let i = 0; i < ni; i++) D[j * ni + i] = g[i]; }
    return D; }
  // the boundary of the cells stamped p in mk (window W): loops of lattice corners [I, J, d], the set on their left (the outer
  // loop counter-clockwise, a hole clockwise). Where two cells meet only at a corner the loop turns left, so they part.
  function wkTrace(cells, W, mk, p) { const VW = W.ni + 1, E = new Map(), inS = (i, j) => i >= 0 && j >= 0 && i < W.ni && j < W.nj && mk[j * W.ni + i] === p;
    const add = (I, J, d) => { const key = J * VW + I; E.set(key, (E.get(key) || 0) | (1 << d)); };
    for (const k of cells) { const i = k % W.ni, j = (k - i) / W.ni;
      if (!inS(i, j - 1)) add(i, j, 0); if (!inS(i + 1, j)) add(i + 1, j, 1); if (!inS(i, j + 1)) add(i + 1, j + 1, 2); if (!inS(i - 1, j)) add(i, j + 1, 3); }
    const DI = [1, 0, -1, 0], DJ = [0, 1, 0, -1], loops = [], corners = (L0) => L0.filter((q, i) => L0[(i + L0.length - 1) % L0.length][2] !== q[2]).map((q) => [q[0], q[1], q[2]]);
    for (const key0 of [...E.keys()]) while (E.get(key0)) { let key = key0, m = E.get(key0), d = m & 1 ? 0 : m & 2 ? 1 : m & 4 ? 2 : 3; const pts = [], at = new Map();
      for (;;) { m = E.get(key); E.set(key, m & ~(1 << d)); const I = key % VW, J = (key - I) / VW;
        if (at.has(key)) { const sub = pts.splice(at.get(key)); for (const q of sub) at.delete(q[3]); loops.push(corners(sub)); }   // the loop met itself: what ran since is a loop of its own
        at.set(key, pts.length); pts.push([I, J, d, key]); key = (J + DJ[d]) * VW + I + DI[d]; if (key === key0) break;
        const mm = E.get(key) || 0, a = (d + 1) & 3, e = (d + 3) & 3; d = mm & (1 << a) ? a : mm & (1 << d) ? d : mm & (1 << e) ? e : -1; if (d < 0) break; }
      loops.push(corners(pts)); }
    return loops; }
  const wkArea2 = (R) => { let s = 0; for (let i = 0; i < R.length; i++) { const a = R[i], b = R[(i + 1) % R.length]; s += a[0] * b[1] - b[0] * a[1]; } return s / 2; };
  // Douglas–Peucker on a closed ring of lattice corners, tau in cells
  function wkDP(R, tau) { const n = R.length; if (n <= 4) return R.slice(); let far = 0, fd = -1;
    for (let i = 1; i < n; i++) { const d = (R[i][0] - R[0][0]) ** 2 + (R[i][1] - R[0][1]) ** 2; if (d > fd) { fd = d; far = i; } }
    const keep = new Uint8Array(n), st = [[0, far], [far, n]]; keep[0] = keep[far] = 1;
    while (st.length) { const [a, b] = st.pop(), A = R[a], B = R[b % n], dx = B[0] - A[0], dy = B[1] - A[1], l2 = dx * dx + dy * dy; let best = -1, bi = -1;
      for (let i = a + 1; i < b; i++) { const P = R[i]; let t = l2 ? ((P[0] - A[0]) * dx + (P[1] - A[1]) * dy) / l2 : 0; t = Math.max(0, Math.min(1, t));
        const d = Math.hypot(P[0] - A[0] - t * dx, P[1] - A[1] - t * dy); if (d > best) { best = d; bi = i; } }
      if (best > tau) { keep[bi] = 1; st.push([a, bi], [bi, b]); } }
    return R.filter((_, i) => keep[i]); }
  // rings that touch nowhere but at their own neighbouring edges, the outer counter-clockwise, the holes clockwise and inside it
  function wkValid(Rs) { if (Rs.some((R) => R.length < 3) || !(wkArea2(Rs[0]) > 0) || Rs.slice(1).some((R) => !(wkArea2(R) < 0))) return false;
    const o = (a, b, p) => Math.sign((b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]));
    const on = (a, b, p) => Math.min(a[0], b[0]) <= p[0] && p[0] <= Math.max(a[0], b[0]) && Math.min(a[1], b[1]) <= p[1] && p[1] <= Math.max(a[1], b[1]);
    const hit = (a, b, p, q) => { const d1 = o(p, q, a), d2 = o(p, q, b), d3 = o(a, b, p), d4 = o(a, b, q);
      return (d1 * d2 < 0 && d3 * d4 < 0) || (!d1 && on(p, q, a)) || (!d2 && on(p, q, b)) || (!d3 && on(a, b, p)) || (!d4 && on(a, b, q)); };
    const S = []; Rs.forEach((R, r) => R.forEach((p, i) => S.push([p, R[(i + 1) % R.length], r, i, R.length])));
    for (let a = 0; a < S.length; a++) for (let b = a + 1; b < S.length; b++) { const A = S[a], B = S[b];
      if (A[2] === B[2] && (B[3] - A[3] === 1 || (A[3] === 0 && B[3] === A[4] - 1))) continue;
      if (Math.max(A[0][0], A[1][0]) < Math.min(B[0][0], B[1][0]) || Math.max(B[0][0], B[1][0]) < Math.min(A[0][0], A[1][0]) ||
        Math.max(A[0][1], A[1][1]) < Math.min(B[0][1], B[1][1]) || Math.max(B[0][1], B[1][1]) < Math.min(A[0][1], A[1][1])) continue;
      if (hit(A[0], A[1], B[0], B[1])) return false; }
    return Rs.slice(1).every((R) => wkPip(R[0], Rs[0])); }

  function wkBuild() {
    const c = WK.cell, NX = WK.nx, NY = WK.ny, tol = WALK.stepTol, rc = Math.round((WALK.radius + WK.margin) / c), need = rc * rc;
    const des = design(), geo = geometry(), DOR = doorsOf(), FS = wkFaces(), byId = (A, id) => A.find((s) => s.id === id), stn = (id) => byId(des.STATIONS, id);
    const P3 = (p) => ({ x: r3(p[0]), y: r3(p[1]), z: r3(p[2]) }), gx = (I) => r3(WK.x0 + I * c), gy = (J) => r3(WK.y0 + J * c);
    const inTread = (p) => TREADS.some((t) => wkPip(p, t.poly) || wkEdgeD(p, t.poly) <= 0.001);
    const TOPS = [...new Set(TREADS.map((t) => t.z).concat(FS.filter((f) => f.ro === RO.floor && f.nz > 0.5 && f.z1 - f.z0 <= 0.002).map((f) => r3(f.pz))))];
    const nearTop = (z) => TOPS.some((t) => Math.abs(z - t) <= 0.012);
    const CUT_OF = { below: 'below', house: 'house', bridge: 'bridge', top: 'fly' }, DOOR_CUT = { main: 'house', bridge: 'bridge', top: 'fly' };

    // ---- a surface: floor, steps, obstacles and ceiling over it, then the free cells (the walker's centre fits) ----
    function wkSurface(id, z) { const h = z != null ? () => z : (x, y) => zd(y), tread = id === 'tread', dOf = (f) => f.v.map((p) => p[2] - h(p[0], p[1]));
      const isFloor = (f, d) => tread ? f.lv === LV.bridge && f.ro === RO.fit && f.nz > 0.99 && d.every((e) => Math.abs(e) <= 0.002) && f.v.every(inTread)
        : f.ro === RO.floor && f.nz > 0.5 && (z == null) === (f.lv === LV.foredeck) && d.every((e) => Math.abs(e) <= WK.floorTol);
      const floors = new Set(); let ya = Infinity, yb = -Infinity;
      for (const f of FS) if (f.nz > 0.5 && isFloor(f, dOf(f))) { floors.add(f); ya = Math.min(ya, f.y0); yb = Math.max(yb, f.y1); }
      if (!floors.size) return null;
      const j0 = Math.max(0, Math.floor((ya - WK.pad - WK.y0) / c)), j1 = Math.min(NY, Math.ceil((yb + WK.pad - WK.y0) / c)), W = { i0: 0, ni: NX, j0, nj: j1 - j0 }, n = W.ni * W.nj;
      const flo = new Uint8Array(n), stp = new Uint8Array(n), dwn = new Uint8Array(n), obs = new Uint8Array(n), fz = new Float32Array(n).fill(-Infinity), ceil = new Float32Array(n).fill(Infinity);
      const yLo = WK.y0 + j0 * c, yHi = WK.y0 + j1 * c, toObs = (k) => { obs[k] = 1; }, toCeil = (k, zz) => { if (zz < ceil[k]) ceil[k] = zz; };
      const DS = [];   // the doors that open off this surface: each opening's plan (sill m either side of its leaf) and its lintel's foot
      if (z != null) for (const ds of DOOR_SET) { const Ls = ds.leaves.map((lid) => LEAF[lid]), d0 = DOR.by[ds.id]; if (Ls[0].kind === 'gate' || Math.abs(d0.sillZ - z) > WK.sillTol) continue;
        const u0 = Math.min(...Ls.map((l) => l.u0)), u1 = Math.max(...Ls.map((l) => l.u1)), w = Ls[0].w, s = WK.sill, x = Ls[0].n === 0;
        DS.push({ sillZ: d0.sillZ, top: d0.sillZ + d0.clear.height - 0.005, P: x ? [[w - s, u0], [w + s, u0], [w + s, u1], [w - s, u1]] : [[u0, w - s], [u1, w - s], [u1, w + s], [u0, w + s]],
          b: x ? [w - s, w + s, u0 - 0.005, u1 + 0.005] : [u0 - 0.005, u1 + 0.005, w - s, w + s] }); }
      for (const f of FS) { if (f.y1 < yLo || f.y0 > yHi) continue;
        if (floors.has(f)) { wkFill(f.v, W, (k, x, y) => { const zz = wkPlaneZ(f, x, y); flo[k] = 1; if (zz > fz[k]) fz[k] = zz; }); continue; }
        const d = dOf(f); let dn = Infinity, dx = -Infinity; for (const e of d) { if (e < dn) dn = e; if (e > dx) dx = e; }
        if (dx < -tol || dn > WK.head) continue;
        if ((f.ro === RO.floor || f.ro === RO.fit || f.ro === RO.tall) && f.nz > 0.7) { const fore = z != null && f.lv === LV.foredeck;   // an upward face: floor, step, obstacle or ceiling, cell by cell
          wkFill(f.v, W, (k, x, y) => { const zz = wkPlaneZ(f, x, y), dz = zz - h(x, y), a = Math.abs(dz);
            if (a <= WK.floorTol) { if (fore) stp[k] = 1; else { flo[k] = 1; if (zz > fz[k]) fz[k] = zz; } }
            else if (dz < 0) { if (a < tol) dwn[k] = 1; }                                                // a step down: hidden where a floor covers it
            else if (a < tol && (f.ro === RO.floor || inStair(x, y) || inTread([x, y]))) stp[k] = 1; else if (dz <= WK.band[1]) obs[k] = 1; else if (dz <= WK.head && zz < ceil[k]) ceil[k] = zz; });
          continue; }
        if (f.ro === RO.floor && dn > -tol && dx < tol) continue;                                    // a sole's own edge
        if (TREADS.length && f.z1 <= TREADS[0].z + 0.002 && f.v.every(inTread)) continue;              // a gate tread's sides: a step
        if (f.anz < 0.05 && (f.ro === RO.wall || f.ro === RO.shell || f.ro === RO.trim) && dn > -tol && dx < tol && nearTop(f.z1)) continue;   // a riser under a sole
        const lin = DS.some((q) => f.x0 >= q.b[0] && f.x1 <= q.b[1] && f.y0 >= q.b[2] && f.y1 <= q.b[3] && f.z0 >= q.top);   // a door's lintel: headroom only
        const P = f.v.map((p, m) => [p[0], p[1], p[2], d[m]]), ob = lin ? [] : wkClip(P, WK.band[0], WK.band[1]), ce = wkClip(P, lin ? WK.band[0] : WK.band[1], WK.head);
        if (ob.length > 2) wkFill(ob, W, toObs); for (let m = 0; m < ob.length; m++) wkSeg(ob[m], ob[(m + 1) % ob.length], W, toObs);
        if (ce.length > 2 && f.anz >= 0.05) wkFill(ce, W, (k, x, y) => toCeil(k, wkPlaneZ(f, x, y))); for (let m = 0; m < ce.length; m++) wkSeg(ce[m], ce[(m + 1) % ce.length], W, toCeil); }
      for (const q of DS) wkFill(q.P, W, (k) => { if (!flo[k] && !stp[k]) { flo[k] = 1; fz[k] = q.sillZ; } });   // the doors' sills: floor at sillZ where nothing is drawn
      const O = new Uint8Array(n); for (let k = 0; k < n; k++) O[k] = obs[k] || !(flo[k] || stp[k] || dwn[k]) ? 1 : 0;
      for (let i = 0; i < W.ni; i++) O[i] = O[n - 1 - i] = 1; for (let j = 0; j < W.nj; j++) O[j * W.ni] = O[j * W.ni + W.ni - 1] = 1;
      const D = wkEDT(O, W.ni, W.nj), free = new Uint8Array(n), grow = new Uint8Array(n);
      for (let k = 0; k < n; k++) if (D[k] >= need) { if (flo[k] && !stp[k]) free[k] = 1; if (flo[k] || stp[k] || dwn[k]) grow[k] = 1; }
      return { id, z, h, W, flo, stp, dwn, obs, fz, ceil, free, grow, floors }; }

    // ---- pieces: a label's 4-connected parts, largest first; grown over floor and steps, traced, simplified ----
    const PIECES = [];
    function wkPiece(R, own, mk, fields) { const W = R.W, pi = PIECES.length, g = WK.grow, add = [];
      for (const k of own) mk[k] = pi;
      if (R.grow) for (const k of own) { const i = k % W.ni, j = (k - i) / W.ni;
        if (i > 0 && i < W.ni - 1 && mk[k - 1] === pi && mk[k + 1] === pi && k >= W.ni && mk[k - W.ni] === pi && mk[k + W.ni] === pi) continue;
        for (let dj = -g; dj <= g; dj++) { const jj = j + dj; if (jj < 0 || jj >= W.nj) continue;
          for (let di = -g; di <= g; di++) { const ii = i + di; if (ii < 0 || ii >= W.ni) continue; const q = jj * W.ni + ii; if (mk[q] !== pi && R.grow[q]) { mk[q] = pi; add.push(q); } } } }
      let bi0 = Infinity, bi1 = -Infinity, bj0 = Infinity, bj1 = -Infinity;
      for (const A of [own, add]) for (const k of A) { const i = k % W.ni, j = (k - i) / W.ni; if (i < bi0) bi0 = i; if (i > bi1) bi1 = i; if (j < bj0) bj0 = j; if (j > bj1) bj1 = j; }
      // where two of its cells meet only at a corner its outline would touch itself: drop the one with fewer neighbours, until none do
      const inP = (k) => mk[k] === pi, nb = (k) => (inP(k - 1) ? 1 : 0) + (inP(k + 1) ? 1 : 0) + (inP(k - W.ni) ? 1 : 0) + (inP(k + W.ni) ? 1 : 0);
      for (let again = true; again;) { again = false;
        for (let j = Math.max(0, bj0 - 1); j <= Math.min(W.nj - 2, bj1); j++) for (let i = Math.max(0, bi0 - 1); i <= Math.min(W.ni - 2, bi1); i++) {
          const a = j * W.ni + i, b = a + 1, u = a + W.ni, v = u + 1, A = inP(a);
          if (A === inP(v) && inP(b) === inP(u) && A !== inP(b)) { const [p, q] = A ? [a, v] : [b, u]; mk[nb(p) <= nb(q) ? p : q] = -1; again = true; } } }
      const cl = []; for (let j = bj0; j <= bj1; j++) for (let i = bi0; i <= bi1; i++) if (mk[j * W.ni + i] === pi) cl.push(j * W.ni + i);
      const all = Int32Array.from(cl);
      const loops = wkTrace(all, W, mk, pi); let outer = null, oa = 0; for (const L0 of loops) { const a = wkArea2(L0); if (a > oa) { oa = a; outer = L0; } }
      const right = (L0) => { const [I, J, d] = L0[0]; return [I + [0.5, 0.5, -0.5, -0.5][d], J + [-0.5, 0.5, 0.5, -0.5][d]]; };
      const holes = loops.filter((L0) => { const a = wkArea2(L0); return a < 0 && -a >= WK.minHole && wkPip(right(L0), outer); });
      let rings = null; for (const t of [1, 0.5, 0.25]) { const Rs = [outer].concat(holes).map((L0) => wkDP(L0, WK.tau / c * t)); if (wkValid(Rs)) { rings = Rs; break; } }
      rings = rings || [outer].concat(holes);
      const toW = (L0) => L0.map(([I, J]) => [gx(W.i0 + I), gy(W.j0 + J)]), glob = (k) => (W.j0 + (k - k % W.ni) / W.ni) * NX + W.i0 + k % W.ni;
      const rec = Object.assign({ R, own, cells: all, g: Int32Array.from(all, glob), poly: toW(rings[0]), holes: rings.slice(1).map((L0) => toW(L0).reverse()) }, fields);
      let gi0 = Infinity, gi1 = -Infinity, gj0 = Infinity, gj1 = -Infinity; for (const k of all) { const i = k % W.ni, j = (k - i) / W.ni; gi0 = Math.min(gi0, i); gi1 = Math.max(gi1, i); gj0 = Math.min(gj0, j); gj1 = Math.max(gj1, j); }
      rec.bb = [W.i0 + gi0, W.i0 + gi1, W.j0 + gj0, W.j0 + gj1]; PIECES.push(rec); return rec; }
    // the components of lab's labels (> 0), largest first, by label
    function wkComps(W, lab) { const n = W.ni * W.nj, seen = new Uint8Array(n), qu = new Int32Array(n), by = new Map();
      for (let k0 = 0; k0 < n; k0++) { const L0 = lab[k0]; if (L0 <= 0 || seen[k0]) continue; let qh = 0, qt = 0; qu[qt++] = k0; seen[k0] = 1;
        while (qh < qt) { const k = qu[qh++], i = k % W.ni;
          for (const q of [i > 0 ? k - 1 : -1, i < W.ni - 1 ? k + 1 : -1, k - W.ni, k + W.ni]) if (q >= 0 && q < n && !seen[q] && lab[q] === L0) { seen[q] = 1; qu[qt++] = q; } }
        const cells = qu.slice(0, qt).sort(); if (!by.has(L0)) by.set(L0, []); by.get(L0).push(cells); }
      for (const A of by.values()) A.sort((a, b) => b.length - a.length || a[0] - b[0]);
      return by; }
    const zMed = (R, own) => { const z = Float64Array.from(own, (k) => R.fz[k]).sort(); return r3(z[(z.length - 1) >> 1]); };
    const headOf = (ceil, own, base) => { let hn = Infinity, hx = -Infinity, cov = 0; for (const k of own) if (ceil[k] < Infinity) { const hr = ceil[k] - base(k); cov++; hn = Math.min(hn, hr); hx = Math.max(hx, hr); }
      return cov === 0 ? {} : cov === own.length ? { headroom: r3(hn), headroomMax: r3(hx) } : { headroom: r3(hn) }; };

    // ---- the stairs (climbed below): the swim platform's pair, the two open stairs aft, the interior flights. A tread in a
    // stair's plan is a step over the deck it rises from; other furniture standing within stepTol of a sole is not (a berth) ----
    const CL = [];
    for (const sd of [-1, 1]) CL.push({ id: 'platform_stair_' + (sd < 0 ? 'port' : 'stbd'), x: sd < 0 ? [-XSH, -XC] : [XC, XSH], yF: Y_PLAT, dir: 1, n: Math.round((ZM - ZP) / PST.rise), going: PST.going, zF: ZP, zT: ZM, row: 'platform_stair_port' });
    for (const [id, st] of [['aft_to_01', S3], ['01_to_fly', S4]]) { const dir = Math.sign(st.top[1] - st.bottom[1]); CL.push({ id, x: st.x, yF: st.bottom[1] + dir * 0.30, dir, n: st.risers, going: st.going, zF: st.bottom[2], zT: st.top[2], row: id }); }
    for (const f of IFLIGHTS) { const dir = Math.sign(f.top[1] - f.foot[1]); CL.push({ id: ROOMS.some((r) => r.id === f.id) ? f.id + '_stair' : f.id, x: f.x, yF: f.foot[1] + dir * 0.30, dir, n: f.risers, going: f.going, zF: f.foot[2], zT: f.top[2], row: f.id, flight: f }); }
    const STEPS = CL.map((C) => { const y1 = C.yF + C.dir * (C.n - 1) * C.going; return [C.x[0] - 0.05, C.x[1] + 0.05, Math.min(C.yF, y1) - 0.05, Math.max(C.yF, y1) + 0.05]; });
    const inStair = (x, y) => STEPS.some((b) => x >= b[0] && x <= b[1] && y >= b[2] && y <= b[3]);
    const aftSole = byId(ROOMS, 'lazarette').sole;   // the aft below block's, the lazarette's
    const SURFS = [['below', ZBELOW], ['below_aft', aftSole], ['platform', ZP], ['main', ZM]].concat(TREADS.length ? [['tread', TREADS[0].z]] : [], [['bridge', Z01], ['top', ZF], ['foredeck', null]])
      .map(([id, z]) => wkSurface(id, z)).filter(Boolean);
    const EX = [], POCKETS = [], deckRows = (R) => R.id === 'tread' ? des.STAIRS.filter((s) => /^bridge_step_port/.test(s.id)).map((s) => ({ id: s.id, y: [-Infinity, Infinity], note: s.note }))
      : des.DECKS.filter((d) => typeof d.z === 'number' && d.z === R.z && /^[a-z0-9_]+( \/ _stbd)?$/.test(d.id));
    const dkName = (row, x) => { const m = / \/ _stbd$/.exec(row.id); return !m ? row.id : x < 0 ? row.id.slice(0, m.index) : row.id.slice(0, m.index).replace(/_port$/, '_stbd'); };

    // ---- the flat surfaces: the doors' openings, the rooms (rects, then across their doorways), the decks; pockets ----
    for (const R of SURFS) { if (R.z == null) continue; const W = R.W, n = W.ni * W.nj, lab = new Int32Array(n), rm = new Uint8Array(n), names = [null];
      const rect = (x0, x1, y0, y1, fn) => { const ia = Math.max(0, Math.ceil((x0 - WK.x0) / c - 0.5)), ib = Math.min(NX - 1, Math.floor((x1 - WK.x0) / c - 0.5));
        const ja = Math.max(W.j0, Math.ceil((y0 - WK.y0) / c - 0.5)), jb = Math.min(W.j0 + W.nj - 1, Math.floor((y1 - WK.y0) / c - 0.5));
        for (let j = ja; j <= jb; j++) for (let i = ia; i <= ib; i++) fn((j - W.j0) * W.ni + i); };
      DOOR_SET.forEach((ds, q) => { const Ls = ds.leaves.map((id) => LEAF[id]), d0 = DOR.by[ds.id]; if (Ls[0].kind === 'gate' || Math.abs(d0.sillZ - R.z) > WK.sillTol) return;
        const u0 = Math.min(...Ls.map((l) => l.u0)), u1 = Math.max(...Ls.map((l) => l.u1)), w = Ls[0].w, hw = WK.doorHalf, L0 = names.push({ kind: 'door', id: ds.id, ord: [2, q], door: d0, label: ds.label }) - 1;
        const fn = (k) => { if (R.free[k] && !lab[k]) lab[k] = L0; }; if (Ls[0].n === 0) rect(w - hw, w + hw, u0, u1, fn); else rect(u0, u1, w - hw, w + hw, fn); });
      const nb4 = (k) => { const i = k % W.ni; return [i > 0 ? k - 1 : -1, i < W.ni - 1 ? k + 1 : -1, k - W.ni, k + W.ni]; };
      const flood = (kind, only) => { const qu = new Int32Array(n); let qh = 0, qt = 0; for (let k = 0; k < n; k++) if (lab[k] > 0 && names[lab[k]].kind === kind) qu[qt++] = k;
        while (qh < qt) { const k = qu[qh++]; for (const q of nb4(k)) if (q >= 0 && q < n && R.free[q] && !lab[q] && (!only || only[q])) { lab[q] = lab[k]; qu[qt++] = q; } } };
      const seen = new Int32Array(n); let st = 0;   // a room is its rect's largest part: a corner of the rect outside its walls (a raked front) is the deck's
      ROOMS.map((r, q) => ({ r, q, a: (r.x[1] - r.x[0]) * (r.y[1] - r.y[0]) })).filter((o) => Math.abs(o.r.sole - R.z) <= WK.floorTol).sort((a, b) => a.a - b.a || a.q - b.q)
        .forEach(({ r, q }) => { const cs = []; rect(r.x[0], r.x[1], r.y[0], r.y[1], (k) => { rm[k] = 1; if (R.free[k] && !lab[k]) cs.push(k); });
          if (!r.walk) { for (const k of cs) lab[k] = -1; return; }
          const L0 = names.push({ kind: 'room', id: r.id, ord: [1, q], room: r }) - 1; let best = []; st++; for (const k of cs) seen[k] = st;
          for (const k0 of cs) { if (seen[k0] !== st) continue; const part = [k0]; seen[k0] = -st;
            for (let p = 0; p < part.length; p++) for (const q2 of nb4(part[p])) if (q2 >= 0 && q2 < n && seen[q2] === st) { seen[q2] = -st; part.push(q2); }
            if (part.length > best.length) best = part; }
          for (const k of best) lab[k] = L0; });
      flood('room');
      const rows = deckRows(R), dk = new Map(), inRow = (y) => rows.some((d) => d.y[0] <= y && y <= d.y[1]);
      if (rows.length) for (let j = 0; j < W.nj; j++) { const y = wkYc(W.j0 + j); if (!inRow(y)) continue; let row = null; for (const d of rows) if (d.y[0] <= y && (!row || d.y[0] > row.y[0])) row = d; row = row || rows[0];
        for (let i = 0; i < W.ni; i++) { const k = j * W.ni + i; if (!R.free[k] || lab[k] || rm[k]) continue; const nm = dkName(row, wkXc(i));
          if (!dk.has(nm)) dk.set(nm, names.push({ kind: 'deck', id: nm, ord: [0, rows.indexOf(row) * 2 + (wkXc(i) < 0 ? 0 : 1)], row }) - 1); lab[k] = dk.get(nm); } }
      flood('deck', rm);
      { const qu = new Int32Array(n); for (let k0 = 0; k0 < n; k0++) { if (!R.free[k0] || lab[k0]) continue;   // pockets: free cells no label reached, a label each
          const nm = { kind: 'pocket', id: 'pocket', ord: [3, names.length] }, L0 = names.push(nm) - 1; let qh = 0, qt = 0; qu[qt++] = k0; lab[k0] = L0;
          while (qh < qt) { const k = qu[qh++]; if (rm[k]) nm.room = true; else if (rows.length && !inRow(wkYc(W.j0 + (k - k % W.ni) / W.ni))) nm.off = true;
            for (const q of nb4(k)) if (q >= 0 && q < n && R.free[q] && !lab[q]) { lab[q] = L0; qu[qt++] = q; } } } }
      R.lab = lab; const mk = new Int32Array(n).fill(-1), comps = wkComps(W, lab);
      for (const [L0, parts] of [...comps].sort((a, b) => names[a[0]].ord[0] - names[b[0]].ord[0] || names[a[0]].ord[1] - names[b[0]].ord[1])) { const nm = names[L0];
        parts.forEach((own, q) => { if (own.length < WK.minCells) return;
          if (nm.kind === 'pocket') { POCKETS.push({ R, own, z: !nm.off && (nm.room || !rows.length) ? r3(R.z) : zMed(R, own), off: !!nm.off }); return; }   // indoors (in a room, or below the decks), the sole
          const id = nm.id + (q ? '_' + (q + 1) : ''), base = { sid: R.id, kind: nm.kind, id, z: zMed(R, own) };
          if (nm.kind === 'room') Object.assign(base, { cut: CUT_OF[nm.room.lv], note: nm.room.label + (nm.room.note ? ': ' + nm.room.note : '') });
          else if (nm.kind === 'door') Object.assign(base, { cut: DOOR_CUT[R.id], door: nm.id, note: nm.label + ': a wall while shut; passable from doorOpen ' + nm.door.clearAt + ' (interact ' + nm.id + ')' });
          else Object.assign(base, { cut: 'deck', note: nm.row.note || '' });
          if (base.cut !== 'deck') base.z = r3(R.z);   // indoors, the level's sole: a finish over it (carpet, teak) is drawn, never walked
          Object.assign(base, headOf(R.ceil, own, (k) => R.fz[k]));
          wkPiece(R, own, mk, base); }); } }

    // ---- the foredeck: chords of its sheer (the rows' median sole), each a ramp; cut into pieces per chord ----
    let surfaceFit = null; const FD = SURFS.find((R) => R.z == null);
    if (FD) { const W = FD.W, n = W.ni * W.nj, zr = new Float64Array(W.nj).fill(NaN), vals = [];
      for (let j = 0; j < W.nj; j++) { vals.length = 0; for (let i = 0; i < W.ni; i++) { const k = j * W.ni + i; if (FD.flo[k]) vals.push(FD.fz[k]); }
        if (vals.length) { vals.sort((a, b) => a - b); zr[j] = vals[(vals.length - 1) >> 1]; } }
      let ja = 0; while (ja < W.nj && isNaN(zr[ja])) ja++; let jb = W.nj - 1; while (jb >= 0 && isNaN(zr[jb])) jb--;
      for (let j = ja, last = ja; j <= jb; j++) if (!isNaN(zr[j])) { for (let q = last + 1; q < j; q++) zr[q] = zr[last] + (zr[j] - zr[last]) * (q - last) / (j - last); last = j; }
      const ze = (e) => e <= ja ? zr[ja] : e > jb ? zr[jb] : (zr[e - 1] + zr[e]) / 2, bands = [];
      for (let s = ja; s <= jb;) { let best = s + 1;
        for (let e = s + 2; e <= jb + 1; e++) { const za = ze(s), zb = ze(e); let ok = true; for (let q = s; q < e && ok; q++) ok = Math.abs(zr[q] - (za + (zb - za) * (q + 0.5 - s) / (e - s))) <= WK.chord; if (!ok) break; best = e; }
        bands.push([s, best]); s = best; }
      const lab = new Int32Array(n); bands.forEach(([s, e], q) => { for (let j = s; j < e; j++) for (let i = 0; i < W.ni; i++) { const k = j * W.ni + i; if (FD.free[k]) lab[k] = q + 1; } });
      FD.lab = lab; const mk = new Int32Array(n).fill(-1), comps = wkComps(W, lab), FR = [];
      bands.forEach(([s, e], q) => { const parts = comps.get(q + 1) || [], cnt = { c: 0, p: 0, s: 0 };
        const ramp = { axis: 'y', a0: gy(W.j0 + s), a1: gy(W.j0 + e), z0: r3(ze(s)), z1: r3(ze(e)) };
        for (const own of parts) { if (own.length < WK.minCells) continue; let lo = false, hi = false; for (const k of own) { if (wkXc(W.i0 + k % W.ni) < 0) lo = true; else hi = true; }
          const sd = lo && hi ? 'c' : lo ? 'p' : 's'; cnt[sd]++; const id = 'foredeck_' + (q + 1) + (sd === 'c' ? (cnt.c > 1 ? 'c' + cnt.c : 'c') : sd + cnt[sd]);
          FR.push(wkPiece(FD, own, mk, Object.assign({ sid: 'foredeck', kind: 'fore', id, cut: 'deck', ramp: Object.assign({}, ramp), note: '' }, headOf(FD.ceil, own, (k) => FD.fz[k])))); } });
      if (FR.length) FR[0].note = (byId(des.DECKS, 'foredeck') || {}).note || '';
      let dev = 0; for (const f of FD.floors) for (const p of f.v) { const j = Math.max(ja, Math.min(jb, Math.floor((p[1] - WK.y0) / c) - W.j0)), b = bands.find(([s, e]) => j >= s && j < e);
        const za = ze(b[0]), zb = ze(b[1]), t = Math.max(0, Math.min(1, (p[1] - (WK.y0 + (W.j0 + b[0]) * c)) / ((b[1] - b[0]) * c))); dev = Math.max(dev, Math.abs(p[2] - (za + (zb - za) * t))); }
      surfaceFit = { foredeck: r3(dev), tol: 0.02 }; }

    // ---- the climbs: each stair a ramp through its treads' centres, held at its decks beyond; its own raster, not grown ----
    const surfAt = (z) => SURFS.find((R) => R.z != null && R.id !== 'tread' && Math.abs(R.z - z) <= 0.01), stairRow = (id) => des.STAIRS.find((s) => s.id === id || s.id.split(' / ')[0] === id);
    const CLIMBS = [], BLOCKED = [];
    for (const C of CL) { const a0 = C.yF - C.dir * C.going / 2, a1 = C.yF + C.dir * (C.n - 0.5) * C.going, lo = surfAt(C.zF), hi = surfAt(C.zT);
      const i0 = Math.max(0, Math.floor((C.x[0] - WK.x0) / c) - 1), i1 = Math.min(NX, Math.ceil((C.x[1] - WK.x0) / c) + 1);
      const j0 = Math.max(0, Math.floor((Math.min(a0, a1) - WK.over - WK.y0) / c)), j1 = Math.min(NY, Math.ceil((Math.max(a0, a1) + WK.over - WK.y0) / c)), W = { i0, ni: i1 - i0, j0, nj: j1 - j0 }, n = W.ni * W.nj;
      const tOf = (y) => (y - a0) / (a1 - a0), rz = (y) => { const t = tOf(y); return t <= 0 ? C.zF : t >= 1 ? C.zT : C.zF + (C.zT - C.zF) * t; };
      const flAt = (S2, i, j) => { if (!S2) return 0; const jj = j - S2.W.j0; return jj >= 0 && jj < S2.W.nj ? S2.flo[jj * S2.W.ni + i - S2.W.i0] : 0; };
      const Fm = new Uint8Array(n), obs = new Uint8Array(n), ceil = new Float32Array(n).fill(Infinity), tt = new Float32Array(n);
      for (let j = 0; j < W.nj; j++) { const y = wkYc(W.j0 + j), t = tOf(y); for (let i = 0; i < W.ni; i++) { const x = wkXc(W.i0 + i), k = j * W.ni + i; tt[k] = t;
          if (x >= C.x[0] && x <= C.x[1] && ((t >= 0 && t <= 1) || (t < 0 && flAt(lo, W.i0 + i, W.j0 + j)) || (t > 1 && flAt(hi, W.i0 + i, W.j0 + j)))) Fm[k] = 1; } }
      const xa = WK.x0 + i0 * c, xb = WK.x0 + i1 * c, ya = WK.y0 + j0 * c, yb = WK.y0 + j1 * c, toObs = (k) => { obs[k] = 1; }, toCeil = (k, zz) => { if (zz < ceil[k]) ceil[k] = zz; };
      for (const f of FS) { if (f.x1 < xa || f.x0 > xb || f.y1 < ya || f.y0 > yb) continue;
        const P = f.v.map((p) => [p[0], p[1], p[2], p[2] - rz(p[1])]), ob = wkClip(P, WK.climbBand, WK.band[1]), ce = wkClip(P, WK.band[1], WK.head);
        if (ob.length > 2) wkFill(ob, W, toObs); for (let m = 0; m < ob.length; m++) wkSeg(ob[m], ob[(m + 1) % ob.length], W, toObs);
        if (ce.length > 2 && f.anz >= 0.05) wkFill(ce, W, (k, x, y) => toCeil(k, wkPlaneZ(f, x, y))); for (let m = 0; m < ce.length; m++) wkSeg(ce[m], ce[(m + 1) % ce.length], W, toCeil); }
      const O = new Uint8Array(n); for (let k = 0; k < n; k++) O[k] = !Fm[k] || obs[k] ? 1 : 0;
      for (let i = 0; i < W.ni; i++) O[i] = O[n - 1 - i] = 1; for (let j = 0; j < W.nj; j++) O[j * W.ni] = O[j * W.ni + W.ni - 1] = 1;
      const D = wkEDT(O, W.ni, W.nj), lab = new Int32Array(n); for (let k = 0; k < n; k++) if (Fm[k] && D[k] >= need) lab[k] = 1;
      const parts = (wkComps(W, lab).get(1) || []).filter((own) => { let lo2 = false, hi2 = false; for (const k of own) { if (tt[k] < 0) lo2 = true; if (tt[k] > 1) hi2 = true; } return lo2 && hi2; });
      const row = stairRow(C.row) || {}, note = row.note || (C.flight ? 'the ' + C.flight.id + ' flight (' + C.flight.lv + '): ' + C.n + ' risers of ' + C.flight.rise + ' m, going ' + C.going + ' m' : '');
      if (!parts.length) { BLOCKED.push({ id: C.id, of: 'climb', kind: 'stair', reason: 'no walker fits the stair between its decks (radius ' + WALK.radius + ' m + margin ' + WK.margin + ' m, band ' + WK.climbBand + '–' + WK.band[1] + ' m over its treads); ' + note }); continue; }
      const R = { id: C.id, W, grow: null, a0, a1, rz, tt, lo, hi }, own = parts[0], mk = new Int32Array(n).fill(-1);
      CLIMBS.push(wkPiece(R, own, mk, Object.assign({ sid: 'climb', kind: 'climb', id: C.id, C, note }, headOf(ceil, own, (k) => rz(wkYc(W.j0 + (k - k % W.ni) / W.ni)))))); }

    // ---- the climbs' ends: the decks they share most cells with give their z and cut ----
    const meet = (A, B) => { let p = 0, q = 0; const a = A.g, b = B.g, out = []; while (p < a.length && q < b.length) { if (a[p] < b[q]) p++; else if (a[p] > b[q]) q++; else { out.push(a[p]); p++; q++; } } return out; };
    const box = (A, B) => A.bb[0] <= B.bb[1] && B.bb[0] <= A.bb[1] && A.bb[2] <= B.bb[3] && B.bb[2] <= A.bb[3];
    for (const K of CLIMBS) { const R = K.R, end = (S2, lo) => { let best = null, bn = 0; if (!S2) return null;
        for (const A of PIECES) { if (A.R !== S2 || !box(A, K)) continue; const m = meet(A, K).filter((g) => { const t = R.tt[((g - g % NX) / NX - R.W.j0) * R.W.ni + g % NX - R.W.i0]; return lo ? t < 0 : t > 1; }).length; if (m > bn) { bn = m; best = A; } }
        return best; };
      const A0 = end(R.lo, true), A1 = end(R.hi, false); let z0 = A0 ? A0.z : r3(K.C.zF), z1 = A1 ? A1.z : r3(K.C.zT), a0 = r3(R.a0), a1 = r3(R.a1);
      const cutLo = A0 ? A0.cut : 'deck', cutHi = A1 ? A1.cut : 'deck';
      if (a0 > a1) { [a0, a1] = [a1, a0]; [z0, z1] = [z1, z0]; }
      Object.assign(K, { cut: cutLo, cutHi, cutLo, zMid: r3((z0 + z1) / 2), ramp: { axis: 'y', a0, a1, z0, z1 } }); }

    // ---- reached from the aft deck (stepping where two grown pieces share a cell within stepTol); the rest _excluded ----
    const zAt = (A, g) => A.z != null && A.ramp == null ? A.z : wkZ(A, wkXc(g % NX), wkYc((g - g % NX) / NX));
    const zr2 = (A) => A.ramp ? [Math.min(A.ramp.z0, A.ramp.z1), Math.max(A.ramp.z0, A.ramp.z1)] : [A.z, A.z];
    const ALL = PIECES.slice(), adj = ALL.map(() => []);
    for (let p = 0; p < ALL.length; p++) for (let q = p + 1; q < ALL.length; q++) { const A = ALL[p], B = ALL[q], ra = zr2(A), rb = zr2(B);
      if (!box(A, B) || ra[0] - rb[1] >= tol || rb[0] - ra[1] >= tol) continue; if (meet(A, B).some((g) => Math.abs(zAt(A, g) - zAt(B, g)) < tol)) { adj[p].push(q); adj[q].push(p); } }
    const start = ALL.findIndex((A) => A.id === 'aft_deck'), seen = new Uint8Array(ALL.length), qu = [start]; seen[start] = 1;
    while (qu.length) { const p = qu.shift(); for (const q of adj[p]) if (!seen[q]) { seen[q] = 1; qu.push(q); } }
    const LOST = ALL.filter((A, p) => !seen[p]), KEPT = ALL.filter((A, p) => seen[p]);

    // ---- the areas, in the surfaces' order (decks, rooms, doors), then the foredeck's chords, then the climbs ----
    const sOrd = (A) => A.kind === 'climb' ? 99 : SURFS.indexOf(A.R);
    const area = (A) => { const o = { id: A.id };
      if (A.kind === 'climb') Object.assign(o, { kind: 'stair', cut: A.cut, cutHi: A.cutHi, cutLo: A.cutLo, zMid: A.zMid, ramp: A.ramp, poly: A.poly, holes: A.holes });
      else Object.assign(o, { cut: A.cut }, A.ramp ? { ramp: A.ramp } : { z: A.z }, { poly: A.poly, holes: A.holes });
      if (A.headroom != null) o.headroom = A.headroom; if (A.headroomMax != null) o.headroomMax = A.headroomMax;
      if (A.kind === 'climb') o.waitsOn = ['stairDown', 'stairUp']; if (A.door) o.door = A.door; o.note = A.note; return o; };
    const areas = KEPT.map((A, p) => [A, p]).sort((a, b) => sOrd(a[0]) - sOrd(b[0]) || a[1] - b[1]).map(([A]) => area(A)), aid = new Map(areas.map((a) => [a.id, a]));
    const pieceOf = new Map(KEPT.map((A) => [A.id, A]));

    // ---- reach: the point itself where it stands on one of the areas, else the nearest of their cells' centres that does.
    // A station whose nearest area point is more than WK.reachTol off its stand, or behind a fitting from it, is taken at
    // its stand, offWalk on the floor under it: the pocket it stands in, or its own _excluded floor where that floor is too
    // tight for the walker (gaps_m.across: the gap the stand is in, along its face, from the nearest drawn face ahead to the nearest behind) or a
    // fitting stands over it (headroom: that fitting's underside over the floor) ----
    const PID = new Map(), TIGHT = [], RCH = [];
    POCKETS.filter((P) => !P.off).forEach((P, q) => PID.set(P, 'pocket_' + (q + 1))); POCKETS.filter((P) => P.off).forEach((P, q) => PID.set(P, 'offdeck_' + (q + 1)));
    const surfOf = (p) => SURFS.find((Q) => Q.z != null && Q.id !== 'tread' && Math.abs(Q.z - p[2]) <= WK.sillTol) || FD;
    const cellIn = (R, x, y) => { const W = R.W, i = Math.floor((x - WK.x0) / c) - W.i0, j = Math.floor((y - WK.y0) / c) - W.j0; return i < 0 || j < 0 || i >= W.ni || j >= W.nj ? -1 : j * W.ni + i; };
    const notFloor = (R, k) => k < 0 || R.obs[k] || !(R.flo[k] || R.stp[k] || R.dwn[k]);
    const clearTo = (p, q) => { const R = surfOf(p), n = Math.ceil(Math.hypot(q.x - p[0], q.y - p[1]) / (c / 2));   // nothing but floor between the stand and its reach
      for (let s = 0; s <= n; s++) if (notFloor(R, cellIn(R, p[0] + (q.x - p[0]) * s / (n || 1), p[1] + (q.y - p[1]) * s / (n || 1)))) return false; return true; };
    function offWalk(sid, p) { const R = surfOf(p), k = R ? cellIn(R, p[0], p[1]) : -1; if (k < 0 || !R.flo[k]) return null;
      const at = (z) => ({ x: r3(p[0]), y: r3(p[1]), z }), W = R.W, i = k % W.ni, j = (k - i) / W.ni;
      const P = POCKETS.find((Q) => Q.R === R && Q.own.includes(k)); if (P) { (P.stands = P.stands || []).push(sid); return { area: null, offWalk: PID.get(P), reach: at(P.z) }; }
      const S = stn(sid); if (!S || !S.face) return null;
      const id = sid + '_floor', b0 = R.fz[k] + WK.band[0], b1 = R.fz[k] + WK.band[1], inBand = (f) => f.z1 > b0 && f.z0 < b1;
      const head = { id, of: 'area' }, a0 = named(S.deck).map((q) => aid.get(q)).find(Boolean); if (a0) head.cut = a0.cut;
      const z = a0 && a0.cut !== 'deck' && R.z != null ? r3(R.z) : r3(R.fz[k]);   // indoors, the level's sole, as its areas
      const tail = 'no area is drawn there, and the station is taken at its stand', who = 'the stand of ' + S.label + ' (' + sid + ')';
      if (R.obs[k]) { let zo = Infinity;   // a fitting over the stand: the lowest face over it in the band
        for (const f of FS) if (inBand(f) && f.x0 <= p[0] && p[0] <= f.x1 && f.y0 <= p[1] && p[1] <= f.y1) zo = Math.min(zo, f.anz >= 0.05 ? wkPlaneZ(f, p[0], p[1]) : f.z0);
        const hr = r3(zo - R.fz[k]); TIGHT.push(Object.assign(head, { z, headroom: hr, reason: who + ' lies under a fitting: its underside ' + hr + ' m over the floor, inside the walker\'s band (' + WK.band[0] + ' to ' + WK.band[1] + ' m); ' + tail + '; standing up waits on duck', waitsOn: ['duck'] }));
        return { area: null, offWalk: id, reach: at(z) }; }
      const f = Math.abs(S.face[0]) >= Math.abs(S.face[1]) ? [Math.sign(S.face[0]), 0] : [0, Math.sign(S.face[1])];
      const run = (di, dj) => { let a = i, b = j; while (a >= 0 && b >= 0 && a < W.ni && b < W.nj && !notFloor(R, b * W.ni + a)) { a += di; b += dj; }   // the raster: to the first cell that is not floor
        let e = di > 0 ? WK.x0 + (W.i0 + a) * c - p[0] : di < 0 ? p[0] - (WK.x0 + (W.i0 + a + 1) * c) : dj > 0 ? WK.y0 + (W.j0 + b) * c - p[1] : p[1] - (WK.y0 + (W.j0 + b + 1) * c);
        const ax = di ? 0 : 1, sg = di || dj, u = p[ax], v = p[1 - ax], e0 = e; let ef = Infinity;   // then the drawn face it stopped at, where one crosses the ray within a cell of it
        for (const g of FS) { if (!inBand(g)) continue; const lo = ax ? g.y0 : g.x0, hi = ax ? g.y1 : g.x1, c0 = ax ? g.x0 : g.y0, c1 = ax ? g.x1 : g.y1; if (v < c0 || v > c1) continue;
          const t = sg > 0 ? lo - u : u - hi; if (t > -1e-9 && Math.abs(t - e0) <= c) ef = Math.min(ef, t); }
        return r3(Number.isFinite(ef) ? ef : e); };
      const ahead = run(f[0], f[1]), behind = run(-f[0], -f[1]);
      TIGHT.push(Object.assign(head, { z, gaps_m: { across: r3(ahead + behind) }, reason: who + ': ' + r3(ahead + behind) + ' m clear across it (' + ahead + ' m ahead, to ' + S.operates + '; ' + behind +
        ' m behind), where the walk needs ' + r3(2 * (WALK.radius + WK.margin)) + ' m (the walker\'s ' + r3(2 * WALK.radius) + ' m and its ' + WK.margin + ' m margin a side); ' + tail }));
      return { area: null, offWalk: id, reach: at(z) }; }
    function reachOn(ids, p) { const C2 = ids.map((id) => aid.get(id)).filter(Boolean), x = r3(p[0]), y = r3(p[1]);
      for (const a of C2) if (wkOn(a, x, y)) return { area: a.id, reach: { x, y, z: r3(wkZ(a, x, y)) } };
      for (const lim of [1.5, Infinity]) { const cand = [];
        for (const a of C2) for (const g of pieceOf.get(a.id).g) { const cx = wkXc(g % NX), cy = wkYc((g - g % NX) / NX); if (Math.abs(cx - p[0]) <= lim && Math.abs(cy - p[1]) <= lim) cand.push([(cx - p[0]) ** 2 + (cy - p[1]) ** 2, cx, cy, a]); }
        cand.sort((u, v) => u[0] - v[0]); for (const [, cx, cy, a] of cand.slice(0, 400)) { const qx = r3(cx), qy = r3(cy); if (wkOn(a, qx, qy)) return { area: a.id, reach: { x: qx, y: qy, z: r3(wkZ(a, qx, qy)) } }; } }
      return { area: null, reach: P3(p) }; }
    const onSurf = (z) => areas.filter((a) => !a.kind && a.z != null && Math.abs(a.z - z) <= 0.05).map((a) => a.id), named = (id) => areas.filter((a) => a.id === id || a.id.startsWith(id + '_')).map((a) => a.id);
    const fore = areas.filter((a) => /^foredeck_/.test(a.id)).map((a) => a.id);

    // ---- house: the salon's door, every door with a leaf, the open doorways, the levels and their decks ----
    const hard = geo.levels.filter((l) => l.kind === 'hard');
    const house = { door: clone(DOR.door), doors: DOR.doors.map(clone),
      doorways: IDOORS.map((d) => ({ id: d.id, leaf: null, open: true, lv: d.lv, wall: d.axis === 0 ? 'x' : 'y', at: d.at, u: d.u.slice(), clear: { width: d.clear, height: d.head }, reason: 'an open doorway, no leaf: walked through, never shut' })),
      room: { levels: hard.map((l) => ({ level: l.id, soleZ: l.soleZ, ceilingZ: l.ceilingZ })), fitCheck: 'qa.fitCheck' }, levels: hard.map((l) => l.id),
      decks: Object.fromEntries(geo.levels.map((l) => [l.id, Object.assign({ deck: l.deck, kind: l.kind, soleZ: l.sole, ceilZ: l.ceil }, l.kind === 'open' ? { open: true } : {})])) };

    // ---- anchors: both helms, the chairs, the ladder, the platform, the berths ----
    const hp = stn('helm_pilothouse'), hf = stn('helm_flybridge'), seatOf = (s) => ({ x: r3((s.x[0] + s.x[1]) / 2), y: r3((s.y[0] + s.y[1]) / 2), z: s.seatZ });
    const SEATS = [['helm', 'the pilothouse helm chair: the helm is taken standing; sitting waits on sit'], ['helm_port', 'the pilothouse\'s port chair: sitting waits on sit'],
      ['helm_stbd', 'the pilothouse\'s starboard chair: sitting waits on sit'], ['fly_helm', 'the flybridge helm chair: the helm is taken standing; sitting waits on sit']].map(([id, why], q) => ({ id, at: seatOf(HCH[q]), why }));
    const pft = byId(des.BOARDING, 'platform_from_tender'), lad = byId(des.BOARDING, 'swim_ladder');
    const deckhead = (b) => { const x0 = b.x[0] + 0.05, x1 = b.x[1] - 0.05, y0 = b.y[0] + 0.05, y1 = b.y[1] - 0.05; let z = Infinity;   // the lowest overhead over the mattress: a face lying flatter than 60°, not the top of a fitting (a pillow)
      for (const f of FS) { if (f.anz < 0.5 || (f.nz > 0 && (f.ro === RO.fit || f.ro === RO.tall)) || f.z0 <= b.topZ + 0.02 || f.x1 < x0 || f.x0 > x1 || f.y1 < y0 || f.y0 > y1 || f.z0 >= z) continue; let P = f.v;
        for (const [ax, s, k] of [[0, 1, x0], [0, -1, x1], [1, 1, y0], [1, -1, y1]]) { const O = []; for (let a = 0; a < P.length; a++) { const p = P[a], q = P[(a + 1) % P.length], fp = s * (p[ax] - k), fq = s * (q[ax] - k);
            if (fp >= 0) O.push(p); if ((fp >= 0) !== (fq >= 0)) { const t = fp / (fp - fq); O.push([p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t, p[2] + (q[2] - p[2]) * t]); } } P = O; if (!P.length) break; }
        for (const p of P) z = Math.min(z, p[2]); }
      return r3(z); };
    const berths = BEDS.map((b) => ({ id: 'berth_' + b.id, interact: 'bed_' + b.id, kind: /^guest_aft/.test(b.id) ? 'midberth' : 'vberth', topZ: b.topZ, head: b.head, deckheadZ: deckhead(b),
      poly: [[b.x[0], b.y[0]], [b.x[1], b.y[0]], [b.x[1], b.y[1]], [b.x[0], b.y[1]]] }));
    const anchors = { helm: SEATS[0].at, wheel: P3(hp.target), pilot: P3(hp.stand),
      helms: [{ id: 'pilothouse', interact: 'helm', seat: SEATS[0].at, wheel: P3(hp.target), pilot: P3(hp.stand) }, { id: 'flybridge', interact: 'helm_flybridge', seat: SEATS[3].at, wheel: P3(hf.target), pilot: P3(hf.stand) }],
      seats: SEATS.map((s) => Object.assign({ id: s.id }, s.at)), ladder: { x: 0, y: LADDER.y, z: r3(ZP) }, platform: P3(pft.stand), berths };

    // ---- interact: the helms, the doors, the windlasses, the berths, the benches, the platform ----
    const it = (o, ids, p, note, sid0) => { const sid = sid0 || o.id || o.station; let r = reachOn(ids, p);
      const d = r.area ? Math.hypot(r.reach.x - p[0], r.reach.y - p[1]) : Infinity, clear = r.area != null && clearTo(p, r.reach);
      if (d > WK.reachTol || !clear) { const q = offWalk(sid, p); if (q) { r = q; note = (note ? note + '; ' : '') + 'taken at its stand, off the walk (see _excluded ' + q.offWalk + ')'; } }
      RCH.push({ id: sid, d: Number.isFinite(d) ? r3(d) : null, clear, offWalk: r.offWalk || null }); return Object.assign(o, r, note ? { note } : {}); };
    const interact = [
      it({ id: 'helm', action: 'enter_helm', label: hp.label, prompt: 'Take the wheel', at: P3(hp.target), seat: SEATS[0].at, stand: P3(hp.stand) }, named(hp.deck), hp.stand, 'taken standing at the wheel; the chair behind it is seat_helm', hp.id),
      it({ id: 'helm_flybridge', action: 'enter_helm', label: hf.label, prompt: 'Take the wheel', at: P3(hf.target), seat: SEATS[3].at, stand: P3(hf.stand) }, named(hf.deck), hf.stand, 'the flybridge helm, taken standing at the wheel; the chair behind it is seat_fly_helm', hf.id)];
    const bg = byId(des.BOARDING, 'side_gate_port / _stbd');
    for (const d of DOR.doors) { const L0 = LEAF[d.leaves ? d.leaves[0] : d.id], gate = L0.kind === 'gate', side = d.x != null, u = side ? (d.y0 + d.y1) / 2 : (d.x0 + d.x1) / 2, w = side ? d.x : d.y;
      const at = side ? [w, u, d.sillZ] : [u, w, d.sillZ], o = L0.o, p = gate ? [Math.sign(w) * Math.abs(bg.stand[0]), bg.stand[1], bg.stand[2]] : [at[0] + o[0] * 0.5, at[1] + o[1] * 0.5, at[2]];
      interact.push(it({ id: d.id, action: 'open_close', label: d.label, prompt: 'Open · shut', state: gate ? 'gates' : 'doorOpen', clearAt: d.clearAt, at: P3(at) }, onSurf(d.sillZ), p)); }
    for (const id of ['windlass_port', 'windlass_stbd']) { const s = stn(id); interact.push(it({ id, action: 'anchor', label: s.label, prompt: 'Let go / weigh anchor', at: P3(s.target) }, fore, s.stand)); }
    for (const b of BEDS) { const s = stn('bed_' + b.id); interact.push(it({ id: s.id, action: 'sleep', label: s.label, prompt: 'Sleep · save', at: P3(s.target) }, named(s.deck), s.stand)); }
    for (const s of ISTN) { if (s.kind === 'sleep') continue; const S2 = stn(s.id), wash = /laundry/.test(s.id);
      interact.push(it({ id: s.id, action: wash ? 'wash_dishes' : 'cook', label: S2.label, prompt: wash ? 'Wash up' : 'Cook · brew up', at: P3(S2.target) }, named(S2.deck), s.stand)); }
    interact.push(it({ id: 'swim', action: 'enter_water', label: 'Swim platform', prompt: 'Swim', at: P3([0, LADDER.y, ZP]) }, named('swim_platform'), lad.stand));
    for (const o of interact) { const r = o.reach, a = o.area, ow = o.offWalk, nt = o.note; delete o.reach; delete o.area; delete o.offWalk; delete o.note;
      Object.assign(o, { reach: r, area: a }, ow ? { offWalk: ow } : {}, nt ? { note: nt } : {}); }

    // ---- the cleats (the line stations' fixtures), spawn, _excluded, the launch ----
    const sp = stn('lines_stern_port'), ss = stn('lines_stern_stbd'), lb = stn('lines_bow'), cl = (id, p, note) => ({ id, type: 'cleat', pos: [r3(p[0]), r3(p[1]), r3(p[2])], note });
    const cleats = [cl('stern_port', sp.target, 'the aft deck\'s port line station (' + sp.id + '); a fixture of the station, not a drawn part'), cl('stern_stbd', ss.target, 'as stern_port, starboard (' + ss.id + ')'),
      cl('bow_port', [-lb.target[0], lb.target[1], lb.target[2]], 'the bow\'s port fairlead, mirrored from ' + lb.id), cl('bow_stbd', lb.target, 'the bow line station (' + lb.id + ')')];
    const sx = CUT_KEYS.deck.player, aft = aid.get('aft_deck'), spawnAt = wkOn(aft, sx[0], sx[1]) ? { area: 'aft_deck', reach: { x: sx[0], y: sx[1] } } : reachOn(['aft_deck'], sx);
    const spawn = { x: spawnAt.reach.x, y: spawnAt.reach.y, area: 'aft_deck', note: 'on the aft deck, where the deck key\'s preview stands' };
    const T = root.YachtTenderIso, m2 = (k) => r3(k * c * c);
    const cc = stn('crane_controls'), tg = stn('tagline');   // the launch first: its stations' offWalk floors are _excluded below
    const launch = { note: 'optional, and no hull-layout-1 row reads it (the kit README\'s questions): the foredeck crane and the two tenders it launches, and where the ship\'s walk hands a player to a tender\'s. A reader that knows no launch skips it.',
      crane: { part: 'crane', state: 'launch', base: [CR.base[0], CR.base[1], r3(zd(CR.base[1]))], pedestalTop: CR.pedestalTop, pivot: CR.pivot.slice(), slewAxis: [0, 0, 1], boom_m: CR.boom, extMax_m: CR.extMax, swl_kg: CR.swl_kg,
        crutch: CR.crutch.slice(), stow: Object.assign({}, STOW), controls: it({ station: cc.id, at: P3(cc.target) }, fore, cc.stand) },
      tenders: ['port', 'stbd'].map((side) => { const Sq = sequence(side), t = TEND[side], hk = stn('hook_' + side);
        return { side, part: 'tender_' + side, rig: 'YachtTenderIso', hull: T ? T.id : null, walk: T ? 'yachtTenderIsoRig.' + T.id + '.walk.json' : null, chocks: [t.x, t.y, t.z], soleZ: T ? r3(t.z + T.DIMS.sole) : null,
          ringChocks: Sq.ringChocks.map(r3), outboard: Sq.outboard.slice(), dropWater: Sq.dropWater, reach: Sq.reach.slice(), launch: side === 'port' ? '-1..0' : '0..1',
          keys: Sq.keys.map((k) => ({ t: k.t, id: k.id, label: k.label })), hook: it({ station: hk.id, at: P3(hk.target) }, fore, hk.stand) }; }),
      handoff: { tagline: it({ station: tg.id }, fore, tg.stand), boarding: it({ from: 'swim_platform', station: pft.id, step: pft.step, at: P3(pft.stand) }, named('swim_platform'), pft.stand),
        note: 'afloat alongside the swim platform, a tender is boarded down ' + pft.step + ' m from the platform\'s edge: the player leaves this walk at the boarding reach and enters the tender\'s at her sole' } };
    for (const r of ROOMS) if (!r.walk) EX.push({ id: r.id, of: 'area', z: r.sole, reason: r.label + ': ' + (r.note || 'drawn, not walked') + '; drawn, not walked' });
    if (T) for (const side of ['port', 'stbd']) EX.push({ id: 'tender_' + side + '_sole', of: 'area', z: r3(TEND[side].z + T.DIMS.sole), reason: 'the ' + side + ' tender\'s sole on her chocks: she is walked in her own walk (' + T.id + '), afloat; aboard, she is cargo (launch)' });
    EX.push({ id: 'hardtop', of: 'area', z: r3(ZHT), reason: 'the hardtop over the flybridge: a roof, drawn and not walked' });
    for (const b of berths) EX.push({ id: b.id, of: 'berth', z: b.topZ, reason: 'a berth ' + r3(b.deckheadZ - b.topZ) + ' m under its deckhead; sleeping is the ' + b.interact + ' interact' });
    for (const s of SEATS) EX.push({ id: 'seat_' + s.id, of: 'seat', at: s.at, reason: s.why + '.', waitsOn: ['sit'] });
    EX.push({ id: 'swim_ladder', of: 'climb', kind: 'ladder', at: { x: 0, y: LADDER.y, z: r3(ZP) }, reason: 'the swim ladder off the platform\'s aft edge (state ladder), into and out of the water', waitsOn: ['ladderOn'] });
    EX.push({ id: 'washboards', of: 'washboards', reason: 'none: her doors are house.doors[] and her decks are walk areas' });
    const inIt = (P) => P.stands ? '; ' + P.stands.join(', ') + (P.stands.length > 1 ? ' stand' : ' stands') + ' in it (offWalk)' : '';
    POCKETS.filter((P) => !P.off).forEach((P) => EX.push({ id: PID.get(P), of: 'area', z: P.z, reason: 'a pocket of floor (' + m2(P.own.length) + ' m² of standing room, ' + P.R.id + ') shut in by fittings: no walk reaches it' + inIt(P) }));
    POCKETS.filter((P) => P.off).forEach((P) => EX.push({ id: PID.get(P), of: 'area', z: P.z, reason: 'floor drawn beyond the decks and rooms of design() (' + m2(P.own.length) + ' m² of standing room, ' + P.R.id + '; no rail at its edge): not walked' + inIt(P) }));
    for (const T2 of TIGHT) EX.push(T2);
    for (const A of LOST) EX.push(A.kind === 'climb' ? { id: A.id, of: 'climb', kind: 'stair', reason: 'no walk from the aft deck reaches it; ' + A.note }
      : { id: A.id, of: 'area', z: A.z != null ? A.z : A.ramp.z0, reason: 'no walk from the aft deck reaches it (' + m2(A.own.length) + ' m² of standing room, ' + A.R.id + ')' + (A.note ? '; ' + A.note : '') });
    for (const B of BLOCKED) EX.push(B);

    const W0 = { schema: 'hidden-harbours/hull-walk@1', rig: RIG_FILE, exportSymbol: EXPORT_SYMBOL, hull: HULL_ID, label: LABEL, kind: 'ship', units: 'metres', frame: FRAME,
      loa_m: r3(LOA_M), loh_m: r3(L), beam_m: API.beam, DWL, cockpitZ: COCKPIT_Z, cell: { W: G.W, H: G.H, pivot: { x: G.cx, y: G.cy }, pxPerM: PX, defaultElev: DEFAULT_ELEV, order: API.order.slice() },
      tags: { lv: clone(geo.ids), ro: clone(geo.roles), ghostRoles: clone(geo.ghostRoles) }, walk: { radius: WALK.radius, stepTol: WALK.stepTol, rule: WALK_RULE, spawn, areas },
      house, anchors, interact, cleats, washboards: [], _excluded: EX, rock: Object.assign({}, ROCK), launch, qa: { fitCheck: fitCheck(), surfaceFit } };
    return W0; }
  const LEVEL_IDS = LV;
  // the sprite cell, as v3's loft.cell, with the hull-layout fields (W, H, PX, pivot, defaultElev)
  const cell = { W: G.W, H: G.H, PX, pivot: { x: G.cx, y: G.cy }, defaultElev: DEFAULT_ELEV, cx: G.cx, cy: G.cy, S };
  const loft = { station: section, halfAtZ, sheerZ: fSz, sheerX: fSx, L, DWL, levels: { below: ZBELOW, main_deck: ZM, house: ZM, bridge: Z01, top: ZF, platform: ZP, hardtop: ZHT },
    shade: { GAIN, BIAS, LN, BAYER, KEY, EDGE }, cell };

  // the global: one hull (byId), kind ship; loa the drawn length overall (LOA_M, the walk's loa_m), loaText the design's figure
  const API = { id: HULL_ID, label: LABEL, kind: 'ship', hullIds: [HULL_ID], defaultHull: HULL_ID, loa: LOA_M, loaText: LOA_TEXT, L, DWL, beam: 8.51, draft: 2.74, cockpitZ: COCKPIT_Z,
    W: G.W, H: G.H, PX, DIRS: 8, pivot: { x: G.cx, y: G.cy }, order: ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'], defaultElev: DEFAULT_ELEV,
    PAINTS, paintRamps, matsFor, get MATS() { return matsFor('champagne').MATS; }, schemeIds: PAINTS.map(p => p.id), defaultScheme: 'champagne', defaultPaint: 'champagne', GAIN, BIAS, LN, BAYER, KEY, EDGE, cell,
    LV, RO, GHOST_ROLES, LEVEL_IDS, ROLES: RO, GATE, LADDER, LEVEL_COLORS: Object.fromEntries(Object.entries(LVRAMP).map(([k, r]) => [k, r[3]])), loft, geometry, design, CHAR92, ROOM_RULES, CRANE: CR, TENDERS: TEND, STOW,
    faceCount: F.length, interiorFaceCount: IF.length, faceList, doorFaces, partFaces, LEAVES: LEAVES.map(D => ({ id: D.id, kind: D.kind, wall: D.n === 0 ? 'x' : 'y', at: D.w, u: [D.u0, D.u1], z: [D.z0, D.z1], out: D.o,
      side: D.side, hinge: D.hu, slide: D.kind === 'slide' ? { dir: D.dir, travel: D.travel, standOut: D.out } : undefined })), interiorFaces: () => IF, fitCheck, render, renderCut,
    cranePose: (o) => { const ln = launchOf(o && o.launch); return ln ? launchState(ln.side, ln.t).pose : ((o && o.crane) || STOW); }, launchState, sequence, craneTip,
    ROCK, rock, project, walk, _G: G };
  API.byId = (id) => id === HULL_ID ? API : null;
  root.ExpeditionYacht120Iso = API;
})(typeof globalThis !== 'undefined' ? globalThis : window);
