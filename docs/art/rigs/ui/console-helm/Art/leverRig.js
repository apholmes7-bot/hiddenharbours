// leverRig.js — a SINGLE-LEVER marine binnacle control (throttle + F/N/R shift),
// built as a true little 3D rig and rasterised into the game's pixel-art idiom,
// then baked to sprite frames. It PIVOTS about its hub in the fore/aft plane:
// NEUTRAL stands upright (a touch toward the operator), AHEAD swings the grip
// forward & away (smaller, up-and-into the panel), ASTERN swings it back toward
// the operator (nearer, larger, the cap end turning to face you).
//
// The body is a swept surface, not a stack of discs: every cross-section ring is
// projected as real geometry and the quads between rings are lit from their own
// surface normals, so the aft/foreshortened poses keep a correct silhouette and
// show the end cap instead of squashing flat.
//
//   window.LeverRig.render(sig, specId)  -> { c, px, py }   (px,py = hub pivot in c)
//   window.LeverRig.handleOffset(sig,id) -> { dx, dy }       grip centre rel. pivot
//   window.LeverRig.sigFromOffset(dx,dy,id) -> sig           invert a drag
//   window.LeverRig.bakeStrip(n, id)     -> canvas           n frames astern->ahead
//   window.LeverRig.SPECS                -> { chrome, graphite }
(function (root) {
  const DEG = Math.PI / 180;

  // ---- rig canvas + hub pivot anchor ---------------------------------------
  const W = 232, H = 344, PX = 116, PY = 300;   // pivot sits low-centre

  // ---- swing / camera --------------------------------------------------------
  const TH0   = 23;         // neutral lean toward the operator (deg)
  const THROW = 33;         // throw either side of neutral
  const PITCH = 17 * DEG;   // camera looks slightly down over the console
  const CAMD  = 760;        // perspective distance: near (astern) grows, far shrinks
  const ARM   = 178;        // world length of the lever
  const cA = Math.cos(PITCH), sA = Math.sin(PITCH);
  const theta = (sig) => (TH0 - sig * THROW) * DEG;

  // key light in view space (x right, y up, z toward viewer): upper-left, front
  const L = (() => { const v = [-0.50, 0.70, 0.51], m = Math.hypot(v[0], v[1], v[2]); return [v[0]/m, v[1]/m, v[2]/m]; })();

  // world -> view. c,s = cos/sin of the pivot swing about the port-stbd (+X) axis.
  // Doubles as the transform for direction vectors (pure rotation).
  function toView(x, y, z, c, s) {
    const y0 = y * c - z * s, z0 = y * s + z * c;
    return { x: x, y: y0 * cA - z0 * sA, z: y0 * sA + z0 * cA };
  }
  function toScreen(v) {
    const k = CAMD / (CAMD - v.z);
    return { x: PX + v.x * k, y: PY - v.y * k, vx: v.x, vy: v.y, z: v.z, k: k };
  }

  // ---- centreline: hub root -> tapered shaft -> collar -> WIDE rubber grip -> cap
  // h = height up the lever; r = fore/aft radius; ell = stretch across port-stbd
  // (a broad palm grip); bend = slight forward kink of the grip.
  const NODES = [
    { h:  0,  r: 15.5, ell: 1.00, mat: 'arm',    bend: 0 },
    { h: 14,  r: 14.4, ell: 1.00, mat: 'arm',    bend: 0 },
    { h: 34,  r: 12.6, ell: 1.02, mat: 'arm',    bend: 0 },
    { h: 58,  r: 11.2, ell: 1.05, mat: 'arm',    bend: -1.0 },
    { h: 82,  r: 10.2, ell: 1.08, mat: 'arm',    bend: -2.6 },
    { h: 100, r:  9.6, ell: 1.14, mat: 'arm',    bend: -3.8 },
    { h: 110, r: 10.0, ell: 1.26, mat: 'collar', bend: -4.4 },
    { h: 117, r: 11.8, ell: 1.46, mat: 'collar', bend: -4.8 },
    { h: 123, r: 14.6, ell: 1.62, mat: 'grip',   bend: -5.2 },
    { h: 133, r: 16.8, ell: 1.68, mat: 'grip',   bend: -5.6 },
    { h: 145, r: 17.4, ell: 1.68, mat: 'grip',   bend: -6.0 },
    { h: 156, r: 16.6, ell: 1.64, mat: 'grip',   bend: -6.2 },
    { h: 165, r: 13.8, ell: 1.52, mat: 'grip',   bend: -6.2 },
    { h: 173, r: 10.0, ell: 1.34, mat: 'cap',    bend: -6.2 },
    { h: 178, r:  6.0, ell: 1.14, mat: 'cap',    bend: -6.2 },
  ];
  const GRIP_H = 145;   // handle waist — the drag handle / hit-test point

  // ---- material ramps (dark -> light) --------------------------------------
  const RAMPS = {
    chrome:   ['#12181c', '#2f3a42', '#586770', '#8ea1a9', '#c6d2d5', '#f1f7f7'],
    graphite: ['#06090b', '#11171b', '#212b32', '#36424b', '#586771', '#8b9ca4'],
    rubber:   ['#040608', '#0c1013', '#161c21', '#222a30', '#323c44', '#4a575f'],
    red:      ['#3f120d', '#7c2a20', '#b3372a', '#e0554a', '#f59183', '#ffb7ac'],
  };
  const SPECS = {
    graphite: { id: 'graphite', arm: 'graphite' },  // console helm
    chrome:   { id: 'chrome',   arm: 'chrome'  },   // sport helm
  };
  // amb/gain shape the ramp response; rim/spec are the metal cues
  const MATS = {
    metal:  { amb: 0.10, gain: 0.80, rim: 0.14, spec: 0.42, tight: 34 },
    cap:    { amb: 0.00, gain: 0.62, rim: 0.09, spec: 0.22, tight: 48 },
    rubber: { amb: 0.08, gain: 0.60, rim: 0.05, spec: 0.10, tight: 10 },
  };
  function matFor(mat, spec) {
    if (mat === 'grip') return { ramp: RAMPS.rubber, m: MATS.rubber };
    if (mat === 'arm')  return { ramp: RAMPS[spec.arm], m: MATS.metal };
    return { ramp: RAMPS.chrome, m: MATS.cap };   // collar + cap always chromed
  }

  // ---- centreline sampling (with slopes, for the surface normals) ----------
  const LINE = (() => {
    const S = [];
    for (let i = 0; i < NODES.length - 1; i++) {
      const a = NODES[i], b = NODES[i + 1];
      const steps = Math.max(2, Math.round((b.h - a.h) / 2.0));
      for (let k = (i === 0 ? 0 : 1); k <= steps; k++) {
        const t = k / steps;
        S.push({
          h: a.h + (b.h - a.h) * t, r: a.r + (b.r - a.r) * t,
          ell: a.ell + (b.ell - a.ell) * t, bend: a.bend + (b.bend - a.bend) * t,
          mat: t < 0.5 ? a.mat : b.mat,
        });
      }
    }
    for (let i = 0; i < S.length; i++) {
      const p = S[Math.max(0, i - 1)], n = S[Math.min(S.length - 1, i + 1)];
      const dh = (n.h - p.h) || 1;
      S[i].dr = (n.r - p.r) / dh;
      S[i].da = (n.r * n.ell - p.r * p.ell) / dh;
      S[i].db = (n.bend - p.bend) / dh;
      // one moulding groove round the rubber grip
      S[i].rib = S[i].mat === 'grip' && Math.abs(S[i].h - 138) < 1.1;
    }
    return S;
  })();
  const SEG = 22;   // cross-section vertices
  const CU = [], SU = [];
  for (let j = 0; j < SEG; j++) { const u = j / SEG * Math.PI * 2; CU.push(Math.cos(u)); SU.push(Math.sin(u)); }

  // ---- crisp primitives ------------------------------------------------------
  function cv(w, h) { const c = document.createElement('canvas'); c.width = Math.round(w); c.height = Math.round(h); return c; }
  function disc(ctx, cx, cy, r, wx, fill) {
    if (r < 0.5) return;
    ctx.fillStyle = fill; cx = Math.round(cx); cy = Math.round(cy);
    const ry = Math.max(1, Math.round(r)), rx = Math.max(1, Math.round(r * wx));
    for (let dy = -ry; dy <= ry; dy++) { const f = Math.sqrt(Math.max(0, 1 - (dy * dy) / (ry * ry))); ctx.fillRect(cx - Math.floor(rx * f), cy + dy, 2 * Math.floor(rx * f) + 1, 1); }
  }
  // scanline polygon fill, integer spans (no AA — this is pixel art)
  function fillPoly(ctx, pts, color) {
    let minY = 1e9, maxY = -1e9, minX = 1e9, maxX = -1e9, n = pts.length;
    for (let i = 0; i < n; i++) { const p = pts[i]; if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y; if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x; }
    if (maxY < -2 || minY > H + 2 || maxX < -2 || minX > W + 2) return;
    const y0 = Math.round(minY), y1 = Math.round(maxY);
    ctx.fillStyle = color;
    for (let y = y0; y <= y1; y++) {
      const yc = y + 0.5; let lo = 1e9, hi = -1e9;
      for (let i = 0; i < n; i++) {
        const a = pts[i], b = pts[(i + 1) % n];
        if ((a.y <= yc) !== (b.y <= yc)) { const x = a.x + (yc - a.y) / (b.y - a.y) * (b.x - a.x); if (x < lo) lo = x; if (x > hi) hi = x; }
      }
      if (lo > hi) { lo = minX; hi = maxX; }   // sub-pixel sliver: keep the seam closed
      const xa = Math.round(lo), xb = Math.round(hi);
      ctx.fillRect(xa, y, Math.max(1, xb - xa), 1);
    }
  }
  // nudge a quad's corners outward so adjacent facets never leave a hairline gap
  function swell(p) {
    let cx = 0, cy = 0; for (const q of p) { cx += q.x; cy += q.y; } cx /= p.length; cy /= p.length;
    return p.map(q => { const dx = q.x - cx, dy = q.y - cy, m = Math.hypot(dx, dy) || 1; return { x: q.x + dx / m * 0.6, y: q.y + dy / m * 0.6 }; });
  }
  // shaded ball, for the small red trigger
  function ball(ctx, cx, cy, r, ramp) {
    disc(ctx, cx, cy, r + 1.1, 1, '#05080b');
    disc(ctx, cx, cy, r, 1, ramp[1]);
    disc(ctx, cx + r * 0.16, cy - r * 0.18, r * 0.80, 1, ramp[2]);
    disc(ctx, cx + r * 0.30, cy - r * 0.34, r * 0.54, 1, ramp[3]);
    disc(ctx, cx + r * 0.40, cy - r * 0.46, r * 0.30, 1, ramp[4]);
    disc(ctx, cx + r * 0.46, cy - r * 0.54, Math.max(1, r * 0.13), 1, ramp[5]);
  }

  // ---- lighting ---------------------------------------------------------------
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

  // ---- build one frame -------------------------------------------------------
  function drawFrame(sig, spec) {
    const out = cv(W, H), octx = out.getContext('2d'); octx.imageSmoothingEnabled = false;
    const body = cv(W, H), ctx = body.getContext('2d'); ctx.imageSmoothingEnabled = false;
    const th = theta(sig), c = Math.cos(th), s = Math.sin(th);

    // rings of projected vertices + view-space normals
    const rings = new Array(LINE.length);
    for (let i = 0; i < LINE.length; i++) {
      const n = LINE[i], a = n.r * n.ell, b = n.r, P = new Array(SEG), N = new Array(SEG);
      for (let j = 0; j < SEG; j++) {
        const cu = CU[j], su = SU[j];
        P[j] = toScreen(toView(a * cu, n.h, n.bend + b * su, c, s));
        const nv = toView(cu, -(n.da * cu * cu + n.ell * su * (n.dr * su + n.db)), n.ell * su, c, s);
        const m = Math.hypot(nv.x, nv.y, nv.z) || 1;
        N[j] = { x: nv.x / m, y: nv.y / m, z: nv.z / m };
      }
      rings[i] = { P: P, N: N, mat: n.mat, rib: n.rib };
    }

    // ---- contact shadow: the shaft dropped onto the console plane -----------
    const sh = cv(W, H), sctx = sh.getContext('2d');
    for (let i = 0; i < LINE.length; i++) {
      const n = LINE[i]; if (n.h > 95) break;
      const y0 = n.h * c - n.bend * s, z0 = n.h * s + n.bend * c;   // axis point, world
      const p = toScreen(toView(0.30 * y0, 0, z0 + 0.34 * y0, 1, 0));
      const f = 1 - n.h / 118;
      disc(sctx, p.x, p.y, Math.max(2, n.r * 0.8 * f + 1.5) * p.k, 1.5, '#03060a');
    }

    // ---- facets: cull backfaces, paint far -> near --------------------------
    const quads = [];
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
        let vx = -cx, vy = -cy, vz = CAMD - cz;
        const vm = Math.hypot(vx, vy, vz) || 1; vx /= vm; vy /= vm; vz /= vm;
        if ((nx * vx + ny * vy + nz * vz) / nm <= 0.01) continue;   // backface
        quads.push({ z: cz, pts: [p0, p1, p2, p3], col: shade(nx / nm, ny / nm, nz / nm, vx, vy, vz, mt.ramp, mt.m, dark) });
      }
    }
    // end cap — what turns to face you as the lever comes aft
    const top = rings[rings.length - 1], ax = toView(0, 1, LINE[LINE.length - 1].db, c, s);
    const am = Math.hypot(ax.x, ax.y, ax.z) || 1;
    {
      let cz = 0; for (let j = 0; j < SEG; j++) cz += top.P[j].z; cz /= SEG;
      let vx = 0, vy = 0, vz = CAMD - cz; const vm = Math.hypot(vx, vy, vz) || 1;
      vx /= vm; vy /= vm; vz /= vm;
      if ((ax.x * vx + ax.y * vy + ax.z * vz) / am > 0) {
        quads.push({ z: cz + 0.6, pts: top.P.slice(), col: shade(ax.x / am, ax.y / am, ax.z / am, vx, vy, vz, RAMPS.chrome, MATS.metal, false), cap: true });
      }
    }
    quads.sort((p, q) => p.z - q.z);
    for (const q of quads) fillPoly(ctx, q.cap ? q.pts : swell(q.pts), q.col);

    // ---- red neutral-lock trigger, on the starboard face of the grip ---------
    {
      const u = 0.86, cu = Math.cos(u), su = Math.sin(u);
      let g = null; for (let i = 0; i < LINE.length; i++) if (LINE[i].h >= GRIP_H) { g = LINE[i]; break; }
      const a = g.r * g.ell, b = g.r;
      const nv = toView(cu, 0, g.ell * su, c, s), nm2 = Math.hypot(nv.x, nv.y, nv.z) || 1;
      for (const t of [-1, 0, 1]) {
        const p = toScreen(toView(a * cu, g.h + t * 6.4, g.bend + b * su, c, s));
        let vx = -p.vx, vy = -p.vy, vz = CAMD - p.z; const vm = Math.hypot(vx, vy, vz) || 1;
        if ((nv.x * vx + nv.y * vy + nv.z * vz) / (nm2 * vm) < 0.10) continue;   // rotated out of sight
        ball(ctx, p.x, p.y, (t === 0 ? 5.0 : 4.1) * p.k, RAMPS.red);
      }
    }

    // ---- one unified silhouette: dilate the body's alpha --------------------
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
    octx.save(); octx.globalAlpha = 0.24; octx.drawImage(sh, 0, 0); octx.restore();
    const lc = cv(W, H); lc.getContext('2d').putImageData(line, 0, 0);
    octx.drawImage(lc, 0, 0);
    octx.drawImage(body, 0, 0);

    return { c: out, px: PX, py: PY };
  }

  // ---- handle tracking -------------------------------------------------------
  function gripPt(sig) {
    const th = theta(sig), c = Math.cos(th), s = Math.sin(th);
    const p = toScreen(toView(0, GRIP_H, -6.0, c, s));
    return { x: p.x - PX, y: p.y - PY };
  }

  // ---- frame cache (== baked sprites, reused live) --------------------------
  const cache = new Map();
  function key(sig, id) { return id + ':' + Math.round(Math.max(-1, Math.min(1, sig)) * 48); }
  function render(sig, specOrId) {
    const spec = typeof specOrId === 'string' ? SPECS[specOrId] : specOrId;
    const k = key(sig, spec.id), hit = cache.get(k);
    if (hit) return hit;
    const out = drawFrame(Math.round(Math.max(-1, Math.min(1, sig)) * 48) / 48, spec);
    cache.set(k, out);
    return out;
  }

  function handleOffset(sig) { const g = gripPt(sig); return { dx: g.x, dy: g.y }; }
  // invert a pointer offset (from the pivot) back to a signal, by nearest sample
  let TABLE = null;
  function table() { if (TABLE) return TABLE; TABLE = []; for (let i = -100; i <= 100; i++) { const s = i / 100, g = gripPt(s); TABLE.push({ s: s, x: g.x, y: g.y }); } return TABLE; }
  function sigFromOffset(dx, dy) {
    const t = table(); let best = 0, bd = 1e9;
    for (const e of t) { const d = (e.x - dx) * (e.x - dx) + (e.y - dy) * (e.y - dy); if (d < bd) { bd = d; best = e.s; } }
    return best;
  }

  function bakeStrip(n, specOrId) {
    const spec = typeof specOrId === 'string' ? SPECS[specOrId] : specOrId;
    const strip = cv(W * n, H), g = strip.getContext('2d'); g.imageSmoothingEnabled = false;
    for (let i = 0; i < n; i++) { const sig = -1 + 2 * i / (n - 1); g.drawImage(render(sig, spec).c, i * W, 0); }
    return strip;
  }

  root.LeverRig = { W, H, PX, PY, TH0, THROW, ARM, SPECS, RAMPS, render, handleOffset, sigFromOffset, bakeStrip, gripPt };
})(typeof globalThis !== 'undefined' ? globalThis : window);
