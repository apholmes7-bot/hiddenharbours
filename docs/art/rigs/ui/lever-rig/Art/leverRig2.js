// leverRig2.js — binnacle throttle/shift lever, ASTERN-POSE PASS.
//
// Problem with v1: the swing was centred 23deg TOWARD the operator and the camera
// only looked down 17deg, so at full ASTERN the lever sat 56deg off vertical and
// the view axis was 73deg off it — the arm projected to ~29% of its length and
// collapsed into a blob behind a full-width grip. Foreshortening was *correct*,
// it was just aimed at the camera.
//
// Fixes shared by every variant here:
//   * the throw is re-centred so neutral leans AWAY, and no pose approaches the
//     view axis: apparent length now stays ~0.57–0.83 of full at both extremes;
//   * the grip ends in a real domed CAP (v1 tapered to a 6px point, so the aft
//     pose had nothing to turn toward you);
//   * a static binnacle BOSS + ground plane is part of the rig, so the aft pose
//     has something to be nearer *than*;
//   * the drop shadow is cast properly onto that plane and swings with the lever,
//     stretching toward the viewer as it comes aft.
//
// Three variants trade off differently — see VARIANTS below.
//
//   LeverRig2.render(sig, variantId, specId) -> { c, px, py }   lever sprite
//   LeverRig2.boss(variantId, specId)        -> { c, px, py }   static binnacle
//   LeverRig2.handleOffset(sig, variantId)   -> { dx, dy }
//   LeverRig2.sigFromOffset(dx, dy, variantId) -> sig
//   LeverRig2.bakeStrip(n, variantId, specId) -> canvas
//   LeverRig2.metrics(variantId)             -> { astern, neutral, ahead, gripGrow }
(function (root) {
  const DEG = Math.PI / 180;
  const W = 244, H = 384, PX = 122, PY = 296;   // PY = pivot (knuckle centre)
  const PLANE = -40;                            // console surface, in lever units

  // ---- materials -------------------------------------------------------------
  const RAMPS = {
    chrome:   ['#12181c', '#2f3a42', '#586770', '#8ea1a9', '#c6d2d5', '#f1f7f7'],
    graphite: ['#06090b', '#11171b', '#212b32', '#36424b', '#586771', '#8b9ca4'],
    rubber:   ['#040608', '#0c1013', '#161c21', '#222a30', '#323c44', '#4a575f'],
    boss:     ['#05080a', '#0e1418', '#1a222a', '#28323a', '#3f4c55', '#5f6f79'],
    red:      ['#3f120d', '#7c2a20', '#b3372a', '#e0554a', '#f59183', '#ffb7ac'],
  };
  const SPECS = { graphite: { id: 'graphite', arm: 'graphite' }, chrome: { id: 'chrome', arm: 'chrome' } };
  const MATS = {
    metal:  { amb: 0.10, gain: 0.80, rim: 0.14, spec: 0.42, tight: 34 },
    cap:    { amb: 0.02, gain: 0.66, rim: 0.12, spec: 0.30, tight: 40 },
    rubber: { amb: 0.08, gain: 0.60, rim: 0.05, spec: 0.10, tight: 10 },
    boss:   { amb: 0.09, gain: 0.64, rim: 0.09, spec: 0.16, tight: 24 },
  };
  function matFor(mat, spec) {
    if (mat === 'grip') return { ramp: RAMPS.rubber, m: MATS.rubber };
    if (mat === 'arm' || mat === 'knuckle') return { ramp: RAMPS[spec.arm], m: MATS.metal };
    if (mat === 'boss') return { ramp: RAMPS.boss, m: MATS.boss };
    if (mat === 'bezel') return { ramp: RAMPS[spec.arm], m: MATS.boss };
    if (mat === 'cap') return { ramp: RAMPS.chrome, m: MATS.cap };
    return { ramp: RAMPS.chrome, m: MATS.metal };   // collar
  }
  // key light in view space: upper-left, front
  const L = (() => { const v = [-0.50, 0.70, 0.51], m = Math.hypot(v[0], v[1], v[2]); return [v[0] / m, v[1] / m, v[2] / m]; })();

  // ---- profiles: h up the lever, r fore/aft radius, ell port-stbd stretch,
  //      bend fore/aft offset of the section centre (negative = forward/away) ---
  const STRAIGHT = [
    { h: -15.0, r:  4.2, ell: 1.00, mat: 'knuckle', bend: 0 },
    { h: -11.0, r:  9.6, ell: 1.00, mat: 'knuckle', bend: 0 },
    { h:  -6.0, r: 13.4, ell: 1.00, mat: 'knuckle', bend: 0 },
    { h:   0.0, r: 15.4, ell: 1.00, mat: 'knuckle', bend: 0 },
    { h:   8.0, r: 14.6, ell: 1.00, mat: 'arm',     bend: 0 },
    { h:  26.0, r: 13.0, ell: 1.01, mat: 'arm',     bend: -0.5 },
    { h:  54.0, r: 11.6, ell: 1.04, mat: 'arm',     bend: -1.8 },
    { h:  85.0, r: 10.6, ell: 1.07, mat: 'arm',     bend: -3.6 },
    { h: 114.0, r:  9.8, ell: 1.11, mat: 'arm',     bend: -5.4 },
    { h: 132.0, r:  9.4, ell: 1.16, mat: 'arm',     bend: -6.4 },
    { h: 140.0, r: 10.2, ell: 1.30, mat: 'collar',  bend: -6.9 },
    { h: 147.0, r: 12.2, ell: 1.48, mat: 'collar',  bend: -7.4 },
    { h: 153.0, r: 14.8, ell: 1.62, mat: 'grip',    bend: -7.9 },
    { h: 163.0, r: 16.9, ell: 1.68, mat: 'grip',    bend: -8.4 },
    { h: 175.0, r: 17.5, ell: 1.68, mat: 'grip',    bend: -8.9 },
    { h: 186.0, r: 17.0, ell: 1.66, mat: 'grip',    bend: -9.2 },
    { h: 196.0, r: 15.8, ell: 1.60, mat: 'grip',    bend: -9.2 },
    { h: 202.0, r: 15.0, ell: 1.52, mat: 'cap',     bend: -9.2 },
    { h: 207.0, r: 13.6, ell: 1.42, mat: 'cap',     bend: -9.2 },
    { h: 211.0, r: 10.8, ell: 1.30, mat: 'cap',     bend: -9.2 },
    { h: 214.0, r:  6.4, ell: 1.16, mat: 'cap',     bend: -9.2 },
  ];
  // dogleg: the arm sweeps forward hard above the shaft, so the handle is offset
  // from the pivot axis. At ASTERN that offset resolves as visible ARM LENGTH
  // instead of collapsing — the silhouette stays a lever, not a lump.
  const DOGLEG = STRAIGHT.map((n) => {
    const t = Math.max(0, (n.h - 30) / 184);
    return Object.assign({}, n, { bend: n.h <= 30 ? n.bend : -32 * t * t });
  });
  const PROFILES = { straight: STRAIGHT, dogleg: DOGLEG };
  const GRIP_H = 175;

  // static binnacle boss the lever pivots out of
  const BOSS = [
    { h: PLANE,      r: 42.0, ell: 1.10, mat: 'boss',  bend: 0 },
    { h: PLANE + 4,  r: 41.0, ell: 1.10, mat: 'boss',  bend: 0 },
    { h: PLANE + 7,  r: 37.5, ell: 1.09, mat: 'boss',  bend: 0 },
    { h: PLANE + 22, r: 33.0, ell: 1.07, mat: 'boss',  bend: 0 },
    { h: PLANE + 29, r: 29.0, ell: 1.05, mat: 'boss',  bend: 0 },
    { h: PLANE + 31, r: 26.5, ell: 1.03, mat: 'bezel', bend: 0 },
    { h: PLANE + 34, r: 22.0, ell: 1.02, mat: 'bezel', bend: 0 },
  ];

  // ---- variants ---------------------------------------------------------------
  // th0  neutral lean, deg (negative = leaning away from the operator)
  // thr  throw either side of neutral
  // apparent length of the arm on screen == cos(theta + pitch)
  // yaw  cant of the whole binnacle, deg. At yaw 0 a fore/aft lever projects onto
  //      a straight VERTICAL screen line — it can only shrink, never turn, and the
  //      grip's downward travel is bought entirely with foreshortening. Canting the
  //      binnacle buys that travel sideways instead, so the aft pose can keep its
  //      length. That is the whole trade-off between 1a and 1b/1c.
  const VARIANTS = {
    A: { id: 'A', badge: '1a', name: 'Overhead, dead-on',
         profile: 'straight', pitch: 30, th0: 9,   thr: 25, camd: 560, yaw: 0 },
    // D: halfway back to v1 — v1's shallower camera and longer throw (so the grip
    // travels 80px north-south instead of 60) but stopped short of v1's 73deg aft
    // reach, and wearing 1a's cap, boss and cast shadow.
    D: { id: 'D', badge: '1d', name: 'v1 throw, 1a dressing',
         profile: 'straight', pitch: 26, th0: 12,  thr: 30, camd: 620, yaw: 0 },
    // B/C: the throw is deliberately short. Screen height of the grip peaks at
    // phi = asin(GRIP_H/camd) and falls off either side, so DOWN=ASTERN only holds
    // while the whole throw stays above that angle. Cant buys the extra travel
    // sideways; a longer throw would flatten (or invert) the vertical component.
    B: { id: 'B', badge: '1b', name: 'Overhead, canted binnacle',
         profile: 'straight', pitch: 40, th0: 0, thr: 22, camd: 520, yaw: 22 },
    C: { id: 'C', badge: '1c', name: 'Canted + dogleg handle',
         profile: 'dogleg', pitch: 32, th0: 8, thr: 22, camd: 460, yaw: 20 },
  };
  const VLIST = ['D', 'A', 'B', 'C'];

  // ---- centreline sampling ----------------------------------------------------
  function buildLine(nodes) {
    const S = [];
    for (let i = 0; i < nodes.length - 1; i++) {
      const a = nodes[i], b = nodes[i + 1];
      const steps = Math.max(2, Math.round((b.h - a.h) / 2.0));
      for (let k = (i === 0 ? 0 : 1); k <= steps; k++) {
        const t = k / steps;
        S.push({ h: a.h + (b.h - a.h) * t, r: a.r + (b.r - a.r) * t,
                 ell: a.ell + (b.ell - a.ell) * t, bend: a.bend + (b.bend - a.bend) * t,
                 mat: t < 0.5 ? a.mat : b.mat });
      }
    }
    for (let i = 0; i < S.length; i++) {
      const p = S[Math.max(0, i - 1)], n = S[Math.min(S.length - 1, i + 1)];
      const dh = (n.h - p.h) || 1;
      S[i].dr = (n.r - p.r) / dh;
      S[i].da = (n.r * n.ell - p.r * p.ell) / dh;
      S[i].db = (n.bend - p.bend) / dh;
      S[i].rib = S[i].mat === 'grip' && (Math.abs(S[i].h - 160) < 1.1 || Math.abs(S[i].h - 182) < 1.1);
    }
    return S;
  }
  const LINES = { straight: buildLine(STRAIGHT), dogleg: buildLine(DOGLEG), boss: buildLine(BOSS) };

  const SEG = 24;
  const CU = [], SU = [];
  for (let j = 0; j < SEG; j++) { const u = j / SEG * Math.PI * 2; CU.push(Math.cos(u)); SU.push(Math.sin(u)); }

  // ---- camera -----------------------------------------------------------------
  function cam(V) {
    const cA = Math.cos(V.pitch * DEG), sA = Math.sin(V.pitch * DEG), D = V.camd;
    const yw = (V.yaw || 0) * DEG, cY = Math.cos(yw), sY = Math.sin(yw);
    return {
      theta: (sig) => (V.th0 - sig * V.thr) * DEG,
      // swing about the port-stbd axis, then cant the binnacle, then pitch the camera
      view: (x, y, z, c, s) => {
        const y0 = y * c - z * s, z0 = y * s + z * c;
        const x1 = x * cY + z0 * sY, z1 = -x * sY + z0 * cY;
        return { x: x1, y: y0 * cA - z1 * sA, z: y0 * sA + z1 * cA };
      },
      screen: (v) => { const k = D / (D - v.z); return { x: PX + v.x * k, y: PY - v.y * k, vx: v.x, vy: v.y, z: v.z, k: k }; },
      D: D,
    };
  }

  // ---- raster primitives -------------------------------------------------------
  function cv(w, h) { const c = document.createElement('canvas'); c.width = Math.round(w); c.height = Math.round(h); return c; }
  function disc(ctx, cx, cy, r, wx, fill) {
    if (r < 0.5) return;
    ctx.fillStyle = fill; cx = Math.round(cx); cy = Math.round(cy);
    const ry = Math.max(1, Math.round(r)), rx = Math.max(1, Math.round(r * wx));
    for (let dy = -ry; dy <= ry; dy++) { const f = Math.sqrt(Math.max(0, 1 - (dy * dy) / (ry * ry))); ctx.fillRect(cx - Math.floor(rx * f), cy + dy, 2 * Math.floor(rx * f) + 1, 1); }
  }
  function fillPoly(ctx, pts, color) {
    let minY = 1e9, maxY = -1e9, minX = 1e9, maxX = -1e9; const n = pts.length;
    for (let i = 0; i < n; i++) { const p = pts[i]; if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y; if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x; }
    if (maxY < -2 || minY > H + 2 || maxX < -2 || minX > W + 2) return;
    const y0 = Math.max(-1, Math.round(minY)), y1 = Math.min(H, Math.round(maxY));
    ctx.fillStyle = color;
    for (let y = y0; y <= y1; y++) {
      const yc = y + 0.5; let lo = 1e9, hi = -1e9;
      for (let i = 0; i < n; i++) {
        const a = pts[i], b = pts[(i + 1) % n];
        if ((a.y <= yc) !== (b.y <= yc)) { const x = a.x + (yc - a.y) / (b.y - a.y) * (b.x - a.x); if (x < lo) lo = x; if (x > hi) hi = x; }
      }
      if (lo > hi) { lo = minX; hi = maxX; }
      const xa = Math.round(lo), xb = Math.round(hi);
      ctx.fillRect(xa, y, Math.max(1, xb - xa), 1);
    }
  }
  function swell(p) {
    let cx = 0, cy = 0; for (const q of p) { cx += q.x; cy += q.y; } cx /= p.length; cy /= p.length;
    return p.map(q => { const dx = q.x - cx, dy = q.y - cy, m = Math.hypot(dx, dy) || 1; return { x: q.x + dx / m * 0.6, y: q.y + dy / m * 0.6 }; });
  }
  function ball(ctx, cx, cy, r, ramp) {
    disc(ctx, cx, cy, r + 1.1, 1, '#05080b');
    disc(ctx, cx, cy, r, 1, ramp[1]);
    disc(ctx, cx + r * 0.16, cy - r * 0.18, r * 0.80, 1, ramp[2]);
    disc(ctx, cx + r * 0.30, cy - r * 0.34, r * 0.54, 1, ramp[3]);
    disc(ctx, cx + r * 0.40, cy - r * 0.46, r * 0.30, 1, ramp[4]);
    disc(ctx, cx + r * 0.46, cy - r * 0.54, Math.max(1, r * 0.13), 1, ramp[5]);
  }
  function shade(nx, ny, nz, vx, vy, vz, ramp, m, dark) {
    const d = nx * L[0] + ny * L[1] + nz * L[2];
    let t = m.amb + m.gain * (0.5 + 0.5 * d);
    const nv = Math.max(0, nx * vx + ny * vy + nz * vz);
    t += m.rim * Math.pow(1 - nv, 2.4);
    const hx = L[0] + vx, hy = L[1] + vy, hz = L[2] + vz, hm = Math.hypot(hx, hy, hz) || 1;
    const sp = Math.max(0, (nx * hx + ny * hy + nz * hz) / hm);
    t += m.spec * Math.pow(sp, m.tight);
    let i = Math.round(t * (ramp.length - 1));
    if (dark) i -= 1;
    return ramp[i < 0 ? 0 : (i > ramp.length - 1 ? ramp.length - 1 : i)];
  }

  // ---- swept-surface mesh -> z-sorted quads -----------------------------------
  function meshQuads(line, c, s, K, spec, out, capTop) {
    const rings = new Array(line.length);
    for (let i = 0; i < line.length; i++) {
      const n = line[i], a = n.r * n.ell, b = n.r, P = new Array(SEG), N = new Array(SEG);
      for (let j = 0; j < SEG; j++) {
        const cu = CU[j], su = SU[j];
        P[j] = K.screen(K.view(a * cu, n.h, n.bend + b * su, c, s));
        const nv = K.view(cu, -(n.da * cu * cu + n.ell * su * (n.dr * su + n.db)), n.ell * su, c, s);
        const m = Math.hypot(nv.x, nv.y, nv.z) || 1;
        N[j] = { x: nv.x / m, y: nv.y / m, z: nv.z / m };
      }
      rings[i] = { P: P, N: N, mat: n.mat, rib: n.rib };
    }
    for (let i = 0; i < rings.length - 1; i++) {
      const A = rings[i], B = rings[i + 1], mt = matFor(A.mat, spec), dark = A.rib;
      for (let j = 0; j < SEG; j++) {
        const j2 = (j + 1) % SEG;
        const p0 = A.P[j], p1 = A.P[j2], p2 = B.P[j2], p3 = B.P[j];
        const nx = (A.N[j].x + A.N[j2].x + B.N[j].x + B.N[j2].x) / 4;
        const ny = (A.N[j].y + A.N[j2].y + B.N[j].y + B.N[j2].y) / 4;
        const nz = (A.N[j].z + A.N[j2].z + B.N[j].z + B.N[j2].z) / 4;
        const nm = Math.hypot(nx, ny, nz) || 1;
        const cx = (p0.vx + p1.vx + p2.vx + p3.vx) / 4, cy = (p0.vy + p1.vy + p2.vy + p3.vy) / 4, cz = (p0.z + p1.z + p2.z + p3.z) / 4;
        let vx = -cx, vy = -cy, vz = K.D - cz;
        const vm = Math.hypot(vx, vy, vz) || 1; vx /= vm; vy /= vm; vz /= vm;
        if ((nx * vx + ny * vy + nz * vz) / nm <= 0.01) continue;
        out.push({ z: cz, pts: [p0, p1, p2, p3], col: shade(nx / nm, ny / nm, nz / nm, vx, vy, vz, mt.ramp, mt.m, dark) });
      }
    }
    if (capTop) {
      const top = rings[rings.length - 1], last = line[line.length - 1];
      const ax = K.view(0, 1, last.db, c, s), am = Math.hypot(ax.x, ax.y, ax.z) || 1;
      let cz = 0; for (let j = 0; j < SEG; j++) cz += top.P[j].z; cz /= SEG;
      const vz = 1;
      if ((ax.z / am) * vz > 0.02) {
        const mt = matFor(capTop, spec);
        out.push({ z: cz + 0.8, pts: top.P.slice(), cap: true,
                   col: shade(ax.x / am, ax.y / am, ax.z / am, 0, 0, 1, mt.ramp, mt.m, false) });
      }
    }
    return rings;
  }

  // ---- lever frame -------------------------------------------------------------
  function drawLever(sig, V, spec) {
    const K = cam(V), line = LINES[V.profile];
    const out = cv(W, H), octx = out.getContext('2d'); octx.imageSmoothingEnabled = false;
    const body = cv(W, H), ctx = body.getContext('2d'); ctx.imageSmoothingEnabled = false;
    const th = K.theta(sig), c = Math.cos(th), s = Math.sin(th);

    // cast shadow: project the axis onto the console plane along the light, and
    // keep it on the pad — a streak running off into empty space reads as a scratch
    const sh = cv(W, H), sctx = sh.getContext('2d');
    for (let i = 0; i < line.length; i++) {
      const n = line[i];
      const y0 = n.h * c - n.bend * s, z0 = n.h * s + n.bend * c;
      const t = y0 - PLANE; if (t <= 0) continue;
      const sx = 0.40 * t, sz = z0 - 0.34 * t;
      const d = Math.hypot(sx, sz); if (d > 74) continue;
      const p = K.screen(K.view(sx, PLANE, sz, 1, 0));
      const f = Math.max(0.2, 1 - t / 250) * Math.min(1, (74 - d) / 16);
      disc(sctx, p.x, p.y, Math.max(1.5, n.r * 0.9 * f) * p.k, 1.5, '#03060a');
    }

    const quads = [];
    meshQuads(line, c, s, K, spec, quads, 'cap');
    quads.sort((p, q) => p.z - q.z);
    for (const q of quads) fillPoly(ctx, q.cap ? q.pts : swell(q.pts), q.col);

    // red neutral-lock trigger on the starboard face of the grip
    {
      const u = 0.86, cu = Math.cos(u), su = Math.sin(u);
      let g = null; for (let i = 0; i < line.length; i++) if (line[i].h >= GRIP_H) { g = line[i]; break; }
      const a = g.r * g.ell, b = g.r;
      const nv = K.view(cu, 0, g.ell * su, c, s), nm2 = Math.hypot(nv.x, nv.y, nv.z) || 1;
      for (const t of [-1, 0, 1]) {
        const p = K.screen(K.view(a * cu, g.h + t * 6.4, g.bend + b * su, c, s));
        let vx = -p.vx, vy = -p.vy, vz = K.D - p.z; const vm = Math.hypot(vx, vy, vz) || 1;
        if ((nv.x * vx + nv.y * vy + nv.z * vz) / (nm2 * vm) < 0.10) continue;
        ball(ctx, p.x, p.y, (t === 0 ? 5.0 : 4.1) * p.k, RAMPS.red);
      }
    }

    outline(body, ctx, octx, sh);
    return { c: out, px: PX, py: PY };
  }

  // ---- static binnacle boss ----------------------------------------------------
  function drawBoss(V, spec) {
    const K = cam(V);
    const out = cv(W, H), octx = out.getContext('2d'); octx.imageSmoothingEnabled = false;
    const body = cv(W, H), ctx = body.getContext('2d'); ctx.imageSmoothingEnabled = false;

    // console plane pad under the boss — sits BELOW the silhouette pass so it
    // reads as surface, not as part of the object
    {
      const p = K.screen(K.view(0, PLANE, 0, 1, 0));
      const back = K.screen(K.view(0, PLANE, -56, 1, 0)), front = K.screen(K.view(0, PLANE, 56, 1, 0));
      const cy = (back.y + front.y) / 2, ry = Math.max(3, (front.y - back.y) / 2), rx = 58 * p.k;
      for (let dy = -Math.round(ry); dy <= Math.round(ry); dy++) {
        const f = Math.sqrt(Math.max(0, 1 - (dy * dy) / (ry * ry)));
        const w = Math.floor(rx * f);
        octx.fillStyle = dy < -ry * 0.3 ? '#131d24' : (dy < ry * 0.4 ? '#0f181e' : '#0b1216');
        octx.fillRect(Math.round(PX) - w, Math.round(cy) + dy, 2 * w + 1, 1);
      }
    }
    const quads = [];
    meshQuads(LINES.boss, 1, 0, K, spec, quads, 'bezel');
    quads.sort((p, q) => p.z - q.z);
    for (const q of quads) fillPoly(ctx, q.cap ? q.pts : swell(q.pts), q.col);

    // fore/aft quadrant slot the lever runs in
    {
      const a = K.screen(K.view(0, PLANE + 34, -19, 1, 0)), b = K.screen(K.view(0, PLANE + 34, 19, 1, 0));
      const cy = (a.y + b.y) / 2, ry = Math.max(2, (b.y - a.y) / 2);
      disc(ctx, PX, cy, ry, 0.62, '#05080b');
      disc(ctx, PX, cy - 1, ry - 1.6, 0.55, '#0b1116');
    }
    outline(body, ctx, octx, null);
    return { c: out, px: PX, py: PY };
  }

  // one unified 1px silhouette, shadow underneath
  function outline(body, ctx, octx, sh) {
    const px = ctx.getImageData(0, 0, W, H).data;
    const line = octx.createImageData(W, H), lp = line.data;
    for (let y = 0; y < H; y++) {
      for (let x = 0; x < W; x++) {
        const i = (y * W + x) * 4;
        if (px[i + 3] > 8) continue;
        const near = (x > 0 && px[i - 4 + 3] > 8) || (x < W - 1 && px[i + 4 + 3] > 8) ||
                     (y > 0 && px[i - W * 4 + 3] > 8) || (y < H - 1 && px[i + W * 4 + 3] > 8);
        if (near) { lp[i] = 5; lp[i + 1] = 8; lp[i + 2] = 11; lp[i + 3] = 255; }
      }
    }
    if (sh) { octx.save(); octx.globalAlpha = 0.26; octx.drawImage(sh, 0, 0); octx.restore(); }
    const lc = cv(W, H); lc.getContext('2d').putImageData(line, 0, 0);
    octx.drawImage(lc, 0, 0);
    octx.drawImage(body, 0, 0);
  }

  // ---- handle tracking ---------------------------------------------------------
  function gripPt(sig, V) {
    const K = cam(V), line = LINES[V.profile];
    let g = line[line.length - 1];
    for (let i = 0; i < line.length; i++) if (line[i].h >= GRIP_H) { g = line[i]; break; }
    const p = K.screen(K.view(0, g.h, g.bend, Math.cos(K.theta(sig)), Math.sin(K.theta(sig))));
    return { x: p.x - PX, y: p.y - PY };
  }
  const TABLES = {};
  // The grip's screen Y is monotonic across the throw on every variant, so the
  // drag is mapped mostly from the pointer's Y: the control reads north-south the
  // way the operator expects, while the sprite itself travels its canted arc.
  const DRAG_WX = 0.12;
  function sigFromOffset(dx, dy, vid) {
    const V = VARIANTS[vid] || VARIANTS.A;
    let t = TABLES[V.id];
    if (!t) { t = []; for (let i = -100; i <= 100; i++) { const s = i / 100, g = gripPt(s, V); t.push({ s: s, x: g.x, y: g.y }); } TABLES[V.id] = t; }
    let best = 0, bd = 1e9;
    for (const e of t) { const ex = e.x - dx, ey = e.y - dy, d = DRAG_WX * ex * ex + ey * ey; if (d < bd) { bd = d; best = e.s; } }
    return best;
  }
  function handleOffset(sig, vid) { const g = gripPt(sig, VARIANTS[vid] || VARIANTS.A); return { dx: g.x, dy: g.y }; }

  // apparent arm length + grip scale at each pose — the numbers behind the fix
  function metrics(vid) {
    const V = VARIANTS[vid] || VARIANTS.A, K = cam(V);
    const tip = LINES[V.profile][LINES[V.profile].length - 1].h;
    const len = (sig) => {
      const th = K.theta(sig), c = Math.cos(th), s = Math.sin(th);
      const a = K.screen(K.view(0, 0, 0, c, s)), b = K.screen(K.view(0, tip, 0, c, s));
      return Math.hypot(b.x - a.x, b.y - a.y) / tip;
    };
    const k = (sig) => { const th = K.theta(sig), c = Math.cos(th), s = Math.sin(th); return K.screen(K.view(0, GRIP_H, 0, c, s)).k; };
    return { astern: len(-1), neutral: len(0), ahead: len(1), gripGrow: k(-1) / k(1) };
  }

  // ---- cache -------------------------------------------------------------------
  const cache = new Map(), bossCache = new Map();
  function render(sig, vid, specOrId) {
    const V = VARIANTS[vid] || VARIANTS.A;
    const spec = typeof specOrId === 'string' ? SPECS[specOrId] : (specOrId || SPECS.graphite);
    const q = Math.round(Math.max(-1, Math.min(1, sig)) * 48) / 48;
    const key = V.id + ':' + spec.id + ':' + Math.round(q * 48);
    let hit = cache.get(key);
    if (!hit) { hit = drawLever(q, V, spec); cache.set(key, hit); }
    return hit;
  }
  function boss(vid, specOrId) {
    const V = VARIANTS[vid] || VARIANTS.A;
    const spec = typeof specOrId === 'string' ? SPECS[specOrId] : (specOrId || SPECS.graphite);
    const key = V.id + ':' + spec.id;
    let hit = bossCache.get(key);
    if (!hit) { hit = drawBoss(V, spec); bossCache.set(key, hit); }
    return hit;
  }
  function bakeStrip(n, vid, specOrId) {
    const strip = cv(W * n, H), g = strip.getContext('2d'); g.imageSmoothingEnabled = false;
    for (let i = 0; i < n; i++) g.drawImage(render(-1 + 2 * i / (n - 1), vid, specOrId).c, i * W, 0);
    return strip;
  }

  root.LeverRig2 = { W, H, PX, PY, PLANE, GRIP_H, VARIANTS, VLIST, SPECS, RAMPS, PROFILES,
                     render, boss, handleOffset, sigFromOffset, bakeStrip, gripPt, metrics };
})(typeof globalThis !== 'undefined' ? globalThis : window);
