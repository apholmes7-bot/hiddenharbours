// leverRig.js — a SINGLE-LEVER marine binnacle control (throttle + F/N/R shift),
// built as a true little 3D rig and rasterised into the game's pixel-art idiom,
// then baked to sprite frames. It PIVOTS about its hub: neutral stands upright
// (leaning slightly toward the operator), AHEAD swings the grip forward & away
// (foreshortened, smaller, up-and-into the panel), ASTERN swings it back toward
// the operator (nearer, larger, down-and-out). Companion to consoleRig / sportRig,
// which draw the hub + housing and composite render(sig, spec) at the pivot.
//
//   window.LeverRig.render(sig, specId)  -> { c, px, py }   (px,py = hub pivot in c)
//   window.LeverRig.handleOffset(sig,id) -> { dx, dy }       grip centre rel. pivot
//   window.LeverRig.sigFromOffset(dx,dy,id) -> sig           invert a drag
//   window.LeverRig.bakeStrip(n, id)     -> canvas           n frames astern->ahead
//   window.LeverRig.SPECS                -> { chrome, graphite }
(function (root) {
  const DEG = Math.PI / 180;

  // ---- rig canvas + hub pivot anchor ---------------------------------------
  const W = 232, H = 330, PX = 116, PY = 300;   // pivot sits low-centre

  // ---- swing / camera --------------------------------------------------------
  const TH0   = 24;    // neutral lean toward the operator (deg, about port-stbd axis)
  const THROW = 34;    // ahead swings up to near-upright (highest), astern leans toward you (low)
  const YAW   = 0;          // centred swing — the fore/aft plane faces us, no sideways arc
  const PITCH = 17 * DEG;   // camera looks slightly down over the console
  const DEPTHK = 0.20;      // perspective: near (astern) grows, far (ahead) shrinks
  const ARM   = 176;        // world length, for depth normalisation
  const LX = -0.60, LY = -0.78;   // key light, upper-left (screen space)

  const cB = Math.cos(YAW),   sB = Math.sin(YAW);
  const cA = Math.cos(PITCH), sA = Math.sin(PITCH);

  // rotate a point about the +X (port-starboard) pivot axis, then camera-project
  function project(p, th) {
    const c = Math.cos(th), s = Math.sin(th);
    // pivot swing about X: y up, z toward viewer (+ = aft/near)
    const y0 = p.y * c - p.z * s;
    const z0 = p.y * s + p.z * c;
    // camera yaw about Y
    const x1 =  p.x * cB + z0 * sB;
    const z1 = -p.x * sB + z0 * cB;
    // camera pitch about X (look down over the console)
    const yr = y0 * cA - z1 * sA;
    const scale = 1 + DEPTHK * (z1 / ARM);
    return { x: x1 * scale, y: -yr * scale, s: scale, depth: z1 };
  }

  // ---- centreline: hub root -> tapered shaft -> collar -> WIDE rubber grip -> cap
  // h = height up the lever; r = radius in the swing plane; ell = width stretch across
  // it (a broad palm grip); bend = slight forward kink of the grip.
  const NODES = [
    { h:  0,  r: 15.5, ell: 1.00, mat: 'arm',    bend: 0 },
    { h: 14,  r: 14.4, ell: 1.00, mat: 'arm',    bend: 0 },
    { h: 34,  r: 12.6, ell: 1.02, mat: 'arm',    bend: 0 },
    { h: 58,  r: 11.2, ell: 1.05, mat: 'arm',    bend: -1.0 },
    { h: 82,  r: 10.2, ell: 1.08, mat: 'arm',    bend: -2.6 },
    { h: 100, r:  9.6, ell: 1.14, mat: 'arm',    bend: -3.8 },
    { h: 110, r: 10.0, ell: 1.34, mat: 'collar', bend: -4.4 },
    { h: 117, r: 11.8, ell: 1.58, mat: 'collar', bend: -4.8 },
    { h: 123, r: 14.6, ell: 1.82, mat: 'grip',   bend: -5.2 },
    { h: 133, r: 16.8, ell: 1.90, mat: 'grip',   bend: -5.6 },
    { h: 145, r: 17.6, ell: 1.90, mat: 'grip',   bend: -6.0 },
    { h: 156, r: 16.8, ell: 1.84, mat: 'grip',   bend: -6.2 },
    { h: 165, r: 13.8, ell: 1.66, mat: 'grip',   bend: -6.2 },
    { h: 173, r: 10.0, ell: 1.42, mat: 'cap',    bend: -6.2 },
    { h: 178, r:  6.0, ell: 1.20, mat: 'cap',    bend: -6.2 },
  ];
  const GRIP_H = 145;   // where the red trim / handle waist sit

  // ---- material ramps (dark -> light) --------------------------------------
  const RAMPS = {
    chrome:   ['#12181c', '#2f3a42', '#586770', '#8ea1a9', '#c6d2d5', '#f1f7f7'],
    graphite: ['#06090b', '#11171b', '#212b32', '#36424b', '#586771', '#8b9ca4'],
    rubber:   ['#040608', '#0c1013', '#161c21', '#222a30', '#323c44', '#4a575f'],
    red:      ['#3f120d', '#7c2a20', '#b3372a', '#e0554a', '#f59183', '#ffb7ac'],
  };
  const SPECS = {
    graphite: { id: 'graphite', arm: 'graphite' },  // console helm
    chrome:   { id: 'chrome',   arm: 'chrome'  },    // sport helm
  };
  function rampFor(mat, spec) {
    if (mat === 'arm')  return RAMPS[spec.arm];
    if (mat === 'grip') return RAMPS.rubber;
    return RAMPS.chrome;                 // collar + cap always chromed
  }

  // ---- crisp primitives ------------------------------------------------------
  function cv(w, h) { const c = document.createElement('canvas'); c.width = Math.round(w); c.height = Math.round(h); return c; }
  function circle(ctx, cx, cy, rad, fill) {
    if (rad < 0.5) return;
    ctx.fillStyle = fill; cx = Math.round(cx); cy = Math.round(cy); rad = Math.round(rad);
    for (let dy = -rad; dy <= rad; dy++) { const dx = Math.floor(Math.sqrt(Math.max(0, rad*rad - dy*dy))); ctx.fillRect(cx-dx, cy+dy, 2*dx+1, 1); }
  }
  // ellipse scanline fill: r = half-height, wx = width stretch (wx=1 is a circle)
  function disc(ctx, cx, cy, r, wx, fill) {
    if (r < 0.5) return;
    ctx.fillStyle = fill; cx = Math.round(cx); cy = Math.round(cy);
    const ry = Math.max(1, Math.round(r)), rx = Math.max(1, Math.round(r * wx));
    for (let dy = -ry; dy <= ry; dy++) { const f = Math.sqrt(Math.max(0, 1 - (dy*dy)/(ry*ry))); const dx = Math.floor(rx * f); ctx.fillRect(cx-dx, cy+dy, 2*dx+1, 1); }
  }
  // one shaded bead of the lathe: body ramp, light offset toward the key light.
  // wx stretches it across the swing plane (wide grip). NO per-bead outline
  // (that segments the tube) — a silhouette pass owns the edge.
  function bead(ctx, cx, cy, r, wx, ramp, shiny) {
    disc(ctx, cx, cy, r,                                        wx, ramp[1]);
    disc(ctx, cx - LX*r*wx*0.16, cy - LY*r*0.16, r*0.84,        wx, ramp[2]);
    disc(ctx, cx - LX*r*wx*0.32, cy - LY*r*0.34, r*0.60,        wx, ramp[3]);
    disc(ctx, cx - LX*r*wx*0.46, cy - LY*r*0.50, r*0.38,        wx, ramp[4]);
    if (shiny) disc(ctx, cx - LX*r*wx*0.56, cy - LY*r*0.62, Math.max(1, r*0.16), wx, ramp[5]);
  }

  // ---- build one frame -------------------------------------------------------
  function projAtH(h, bend, th) { return project({ x: 0, y: h, z: bend }, th); }
  // grip-centre screen offset from the hub pivot, for hit-test / drag
  function gripPt(sig) {
    const th = (TH0 - sig * THROW) * DEG;
    return projAtH(GRIP_H, -6.0, th);
  }

  function drawFrame(sig, spec) {
    const c = cv(W, H), ctx = c.getContext('2d'); ctx.imageSmoothingEnabled = false;
    const th = (TH0 - sig * THROW) * DEG;

    // sample the centreline into shaded beads
    const beads = [];
    for (let i = 0; i < NODES.length - 1; i++) {
      const a = NODES[i], b = NODES[i + 1];
      const steps = Math.max(2, Math.round((b.h - a.h) / 1.1));
      for (let k = (i === 0 ? 0 : 1); k <= steps; k++) {
        const t = k / steps;
        const h = a.h + (b.h - a.h) * t, r = a.r + (b.r - a.r) * t, bend = a.bend + (b.bend - a.bend) * t;
        const mat = t < 0.5 ? a.mat : b.mat;
        const ell = a.ell + (b.ell - a.ell) * t;
        const pr = project({ x: 0, y: h, z: bend }, th);
        beads.push({ x: PX + pr.x, y: PY + pr.y, r: r * pr.s, wx: ell, depth: pr.depth, mat });
      }
    }
    // soft contact shadow where the arm meets the housing
    ctx.save(); ctx.globalAlpha = 0.22;
    const base = beads[0];
    circle(ctx, base.x + 4, base.y + 3, base.r * 0.9, '#03060a');
    ctx.restore();

    // pass 1 — one continuous dark silhouette (clean unified edge)
    for (const bd of beads) disc(ctx, bd.x, bd.y, bd.r + 1.2, bd.wx, '#05080b');
    // pass 2 — shaded bodies, far (ahead/into-panel) first, near (astern) last
    beads.sort((p, q) => p.depth - q.depth);
    for (const bd of beads) bead(ctx, bd.x, bd.y, bd.r, bd.wx, rampFor(bd.mat, spec), bd.mat !== 'grip');

    // red neutral-lock trigger on the fore/right face of the wide grip
    const gp = project({ x: 0, y: GRIP_H, z: -6.0 }, th);
    const gx0 = PX + gp.x, gy0 = PY + gp.y, halfW = 17.6 * 1.90 * gp.s;
    const rx = gx0 + halfW * 0.58, ry = gy0 + 1;
    for (const t of [{o:-1,r:4.4},{o:0,r:5.3},{o:1,r:4.4}]) {
      const by = ry + t.o * 5.6 * gp.s;
      disc(ctx, rx, by, t.r * gp.s + 1.1, 1.0, '#05080b');
      bead(ctx, rx, by, t.r * gp.s, 1.0, RAMPS.red, true);
    }

    return { c, px: PX, py: PY };
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
  function table() { if (TABLE) return TABLE; TABLE = []; for (let i = -100; i <= 100; i++) { const s = i / 100, g = gripPt(s); TABLE.push({ s, x: g.x, y: g.y }); } return TABLE; }
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

  root.LeverRig = { W, H, PX, PY, TH0, THROW, SPECS, RAMPS, render, handleOffset, sigFromOffset, bakeStrip, gripPt };
})(typeof globalThis !== 'undefined' ? globalThis : window);
