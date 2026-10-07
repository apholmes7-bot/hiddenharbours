/* Hidden Harbours — WHARF RIG, PASS 2 (weather-lit · G-buffer · floats on the water shader · snow · character v9.2).
   The wharf, pier, float and quay kit with the tree rig's parameters and the cliffs' light. Pass 1 lit every face
   once from the fixed upper-left key and ordered-dithered it, threw no shadow on itself (the piles under a deck
   were as bright as the deck), rocked its float on an 8-frame sine that no other water agreed with, sized its
   ladders for a figure that was never measured, and stopped its berths at an 18 m dragger. What pass 2 does:

     A. LIGHT IS NOT BAKED.  frame() rasterises a G-buffer (material + structure band, normal, position, depth,
        part, layer, group); relight(fr, sky) lights it from any WeatherSky.at() with the cliffs' and trees'
        law: sky × visibility × up-facing + sun × N·L × shadow + ground bounce, banded on the same ladder, then
        harmony's grade. No dither: a face takes one band, as a stamp does on a tree.
     B. SHADOW AND SHADE.  Sky visibility from nine occlusion maps (the gloom under a deck, between piles, inside
        a crib); a sun map per sky, so a deck shades its piles and a bollard shades the planks; castShadow()
        drops the structure on the water or the ground, levels 1 canopy · 2 partial · 3 full, like TreeRig4.
     C. THE WATER IS THE SHADER'S.  Nothing wet is painted. Every pixel knows its depth under the current water
        (view 'sub'), the water shader (TerrainLight6's sea) draws over it, contact() marks where the structure
        pierces the surface for the foam lace, and castShadow() is the shade the water takes (TL6 o.lv).
     D. FLOATS SIT ON IT.  A float's heave, roll and pitch come from the same clock (LOOP 16), wind and direction
        as the water shader, as a waterplane average over the float's own rectangle (sinc for heave, the first
        moment for tilt) of three loop-periodic wave components. The guide piles, anchor and hinge never move;
        the gangway is re-solved every frame from the hinge to the rocked float deck and its toe rolls.
     E. WEATHER.  cloud · rain (porosity darkens, lit tops glint) · fog (height-heavy) · snow cover through one
        byte per pixel (snowMap, TreeMaps4's contract) · the wet the tide leaves and the spray line · a sun rim
        when the sun is behind · water-bounce dapple on shaded faces just above the water.
     F. TREE PARAMETERS.  variant 0–3 · season summer | autumn | winter · stage new | seasoned | weathered |
        derelict (o.age 0–1 overrides) · o.wind {w, gust, dir} · o.frame 0–15 · o.tide · o.sea. Ringless by
        default (ADR 0031), {outline: true} is the A/B. Same camera: ¾ at 40°, 32 px = 1 m, 8 facings.

   globalThis.WharfRig2
     model(key, o)          faces + groups + placements, cached            key = family | preset | 'harbour:<id>'
     frame(key, o)          one pose: G-buffer, sky visibility, snow bytes  o = {dir, tide, frame, wind, sea, ppu}
     relight(fr, sky, o)    -> RGBA          castShadow(fr, sky, o) -> {x0, y0, w, h, lv}     contact(fr) -> mask
     view(fr, ch, sky)      lit unlit normal ao sun height depth parts mat wet snow sub light detail layer
     sheet(key, o, ch, sky) the 16-frame loop as 4 × 4   · sheetSpec · shadeAt · seaMotion · rockFor · REF_SKY · UNLIT_SKY
   Gameplay, harbours and the character contract: Art/wharfRig2.kit.js.                                                   */
(function (root) {
  'use strict';
  const G = root.WharfGeo2; if (!G || !G.FAMILIES) throw new Error('wharfRig2.js: load wharfRig2.geo.js and wharfRig2.fam.js first');
  const PPU = 32, ELEV = 40, D2R = Math.PI / 180, CE = Math.cos(ELEV * D2R), SE = Math.sin(ELEV * D2R), KZ = PPU * CE, PYM = PPU * SE, LOOP = 16, PAD = 4, TAU = Math.PI * 2;
  const clamp = G.clamp, h2r = G.h2r;
  const KEYLINE = '#101d21', SNOWR = ['#5c7180', '#7d93a0', '#a8bcc4', '#cfdde1', '#eef4f4'].map(h2r);
  const nrm = (v) => { const L = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / L, v[1] / L, v[2] / L]; };
  const crs = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const toView = (w) => [w[0], -w[2] * CE + w[1] * SE, w[2] * SE + w[1] * CE];
  const dirOf = (az, el) => { const c = Math.cos(el * D2R); return [Math.sin(az * D2R) * c, -Math.cos(az * D2R) * c, Math.sin(el * D2R)]; };
  const REF_SUN = dirOf(290, 55);
  const REF_SKY = { name: 'Afternoon · reference key', sunW: REF_SUN, sunV: toView(REF_SUN), sunI: 1, skyI: 0.60, expo: 1, kc: '#fff0cf', ac: '#1d3b4a', ka: 0.16, aa: 0.30,
    rc: '#bcd6e2', ra: 0.14, wash: '#fff4dd', wa: 0.03, amb: 0.62, fogC: '#c3cdce', skyC: '#b0c9d8', fog: 0, wet: 0, snow: 0, wind: { w: 0.2, gust: 0.4, dir: 1 } };
  const UNLIT_SKY = { name: 'unlit', sunW: [0, 0, 1], sunV: toView([0, 0, 1]), sunI: 0, skyI: 1.25, expo: 1, kc: '#ffffff', ac: '#000000', ka: 0, aa: 0, wash: '#ffffff', wa: 0, amb: 1,
    fogC: '#ffffff', fog: 0, wet: 0, snow: 0, grade: false };
  function faceBand(E) { return E >= 0.72 ? 1 : E >= 0.24 ? 0 : E >= 0.095 ? -1 : E >= 0.04 ? -2 : -3; }
  const hsh = (a, b, c, s) => { let h = Math.imul(a | 0, 374761393) + Math.imul(b | 0, 668265263) + Math.imul(c | 0, 1440670441) + Math.imul(s | 0, 1274126177) | 0; h ^= h >>> 13; h = Math.imul(h, 1103515245) | 0; h ^= h >>> 16; return (h >>> 0) / 4294967296; };
  function vn3(x, y, z, s) {
    const xi = Math.floor(x), yi = Math.floor(y), zi = Math.floor(z), fx = x - xi, fy = y - yi, fz = z - zi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy), w = fz * fz * (3 - 2 * fz);
    const L = (a, b, t) => a + (b - a) * t, c = (i, j, k) => hsh(xi + i, yi + j, zi + k, s);
    return L(L(L(c(0, 0, 0), c(1, 0, 0), u), L(c(0, 1, 0), c(1, 1, 0), u), v), L(L(c(0, 0, 1), c(1, 0, 1), u), L(c(0, 1, 1), c(1, 1, 1), u), v), w);
  }

  // ============================ CAMERA (the fleet's: xr = x·cos − y·sin, yr = x·sin + y·cos, yr north) ====
  function cam(dir, ppu) { const th = (dir || 0) * Math.PI / 4; return { dir: dir || 0, ct: Math.cos(th), st: Math.sin(th), S: ppu || PPU }; }
  const toLocal = (C, w) => [w[0] * C.ct - w[1] * C.st, -w[0] * C.st - w[1] * C.ct, w[2]];   // world [east, south, up] → model
  const toWorld = (C, v) => { const xr = v[0] * C.ct - v[1] * C.st, yr = v[0] * C.st + v[1] * C.ct; return [xr, -yr, v[2]]; };
  function proj(p, C) { const xr = p[0] * C.ct - p[1] * C.st, yr = p[0] * C.st + p[1] * C.ct; return [xr * C.S, -(yr * SE + p[2] * CE) * C.S, yr * CE - p[2] * SE]; }
  // a screen pixel (relative to the projected origin) back onto the plane z, in model coordinates
  function unproj(sx, sy, z, C) { const xr = sx / C.S, yr = (-sy / C.S - z * CE) / SE; return [xr * C.ct + yr * C.st, -xr * C.st + yr * C.ct, z]; }

  // ============================ THE SEA UNDER A FLOAT ========================================================
  /* Three components, periodic in the loop (1, 2 and 1 cycles), running with the water shader's swell toward
     the shore (+south, TerrainLight6's sdir = 1). Heights from the wind the way a harbour sees it (shelter 0.35
     of open water) or a named sea state (NavBuoy's SEAS, same Hs and Tp → L = 1.56 Tp²). */
  const SEAS = { calm: { Hs: 0.06, L: 18 }, chop: { Hs: 0.38, L: 9 }, working: { Hs: 1.05, L: 33 }, rough: { Hs: 2.6, L: 68 }, swell: { Hs: 1.6, L: 206 } };
  function seaOf(o, wind) {
    const w = wind && wind.w != null ? clamp(+wind.w, 0, 1) : 0.2, sh = o.shelter != null ? clamp(+o.shelter, 0, 1) : 0.35, st = o.sea && SEAS[o.sea];
    const Hs = st ? st.Hs * sh : sh * (0.10 + 0.9 * w + 1.2 * w * w), L = st ? st.L : 5 + 20 * w, h0 = (o.waveDeg || 0) * D2R;
    return { Hs, L, w, comps: [{ a: Hs * 0.40, L, n: 1, h: h0, ph: 0 }, { a: Hs * 0.22, L: L * 0.62, n: 2, h: h0 + 28 * D2R, ph: 2.19 }, { a: Hs * 0.15, L: L * 2.2, n: 1, h: h0 - 20 * D2R, ph: 4.71 }] };
  }
  const sinc = (q) => Math.abs(q) < 1e-4 ? 1 : Math.sin(q) / q, g1 = (q) => Math.abs(q) < 1e-3 ? 1 : 3 * (Math.sin(q) - q * Math.cos(q)) / (q * q * q);
  /* the rectangle's response: waterplane-averaged heave and best-fit slopes, in the float's own axes */
  function seaMotion(sea, grp, C, fi) {
    const ph = TAU * fi / LOOP, wc = toWorld({ ct: C.ct, st: C.st }, [grp.cx, grp.cy, 0]);
    let eta = 0, sx = 0, sy = 0;
    for (const c of sea.comps) {
      const k = TAU / c.L, kw = [k * Math.sin(c.h), k * Math.cos(c.h), 0], kl = toLocal(C, kw), ca = Math.cos(grp.ax || 0), sa = Math.sin(grp.ax || 0);
      const kx = kl[0] * ca + kl[1] * sa, ky = -kl[0] * sa + kl[1] * ca, psi = kw[0] * wc[0] + kw[1] * wc[1] - c.n * ph + c.ph;
      const Sx = sinc(kx * grp.Lx / 2), Sy = sinc(ky * grp.By / 2);
      eta += c.a * Sx * Sy * Math.cos(psi);
      sx += -c.a * kx * Math.sin(psi) * g1(kx * grp.Lx / 2) * Sy; sy += -c.a * ky * Math.sin(psi) * Sx * g1(ky * grp.By / 2);
    }
    return { heave: eta * 0.9, pitch: Math.atan(sx) * 0.7, roll: Math.atan(sy) * 0.7, slope: [sx, sy] };
  }
  // pose of a float group: pitch about its own y axis, roll about its x axis, lifted onto the water at z0
  function poseOf(grp, m, z0) {
    const ca = Math.cos(grp.ax || 0), sa = Math.sin(grp.ax || 0), cp = Math.cos(m.pitch), sp = Math.sin(m.pitch), cr = Math.cos(m.roll), sr = Math.sin(m.roll);
    const fwd = (p) => { const dx = p[0] - grp.cx, dy = p[1] - grp.cy, u = dx * ca + dy * sa, v = -dx * sa + dy * ca, z = p[2];
      const u1 = u * cp - z * sp, z1 = u * sp + z * cp, v2 = v * cr - z1 * sr, z2 = v * sr + z1 * cr;
      return [grp.cx + u1 * ca - v2 * sa, grp.cy + u1 * sa + v2 * ca, z2 + z0]; };
    const inv = (p) => { const dx = p[0] - grp.cx, dy = p[1] - grp.cy, u1 = dx * ca + dy * sa, v2 = -dx * sa + dy * ca, z2 = p[2] - z0;
      const v = v2 * cr + z2 * sr, z1 = -v2 * sr + z2 * cr, u = u1 * cp + z1 * sp, z = -u1 * sp + z1 * cp;
      return [grp.cx + u * ca - v * sa, grp.cy + u * sa + v * ca, z]; };
    return { fwd, inv };
  }

  // ============================ MODELS ======================================================================
  const MODELS = new Map(), GEO_SKIP = { tide: 1, frame: 1, wind: 1, sky: 1, dir: 1, sea: 1, outline: 1, ppu: 1, shelter: 1, waveDeg: 1, bed: 1 };
  const geoKey = (key, o) => key + '|' + JSON.stringify(Object.keys(o).sort().filter(k => !GEO_SKIP[k] && typeof o[k] !== 'function').map(k => [k, o[k]]));
  function model(key, o) {
    o = o || {}; const mk = geoKey(key, o); let mdl = MODELS.get(mk); if (mdl) return mdl;
    const t0 = Date.now();
    if (/^harbour:/.test(key)) { if (!root.WharfRig2Kit) throw new Error('wharfRig2: harbours need Art/wharfRig2.kit.js'); mdl = root.WharfRig2Kit.buildHarbour(key.slice(8), o); }
    else {
      const s = G.resolve(key, o), T = G.frame(s), faces = [];
      s.grp = s.floating ? 1 : s.family === 'gangway' ? 2 : 0; G.CTX.grp = 0; G.CTX.ux = 0;
      if (s.family !== 'gangway') G.FAMILIES[s.family].build(faces, s, T);
      const P = G.addFittings(faces, s, T);
      const groups = {};
      if (s.floating) groups[1] = { id: 1, kind: 'float', cx: 0, cy: 0, ax: 0, Lx: s.L, By: s.W, fb: s.freeboard, chains: s._chain ? [s._chain] : [] };
      const gangs = s.family === 'gangway' ? [{ hinge: [-s.L / 2, 0, s.deckZ], u: [1, 0], L: s.L, W: s.W, floatGrp: null, landZ: () => s.tide + 0.45 }] : [];
      mdl = { key, s, T, faces, P, groups, gangs, modules: [{ id: 'm0', key, s, P, rot: 0, at: [0, 0], toH: (x, y) => [x, y], grp: s.grp }], tideRange: s.tideRange };
    }
    mdl.mk = mk; mdl.mats = G.makeMats(mdl.s); mdl.matKeys = Object.keys(mdl.mats); mdl.MI = {}; mdl.matKeys.forEach((k, i) => { mdl.MI[k] = i; });
    mdl.MA = mdl.matKeys.map(k => { const m = mdl.mats[k]; return { ramp: m.ramp.map(h2r), hex: m.ramp, por: m.por, gloss: m.gloss, hold: m.hold, cls: m.cls }; });
    mdl.PARTS = []; mdl.PI = {}; for (const f of mdl.faces) if (mdl.PI[f.part] == null) { mdl.PI[f.part] = mdl.PARTS.length; mdl.PARTS.push(f.part); }
    mdl.static = mdl.faces.filter(f => !f.grp); mdl.moving = mdl.faces.filter(f => f.grp);
    mdl.frames = new Map(); mdl.rasters = new Map(); mdl.sunMaps = new Map(); mdl.ms = Date.now() - t0;
    if (MODELS.size > 24) MODELS.delete(MODELS.keys().next().value);
    MODELS.set(mk, mdl); return mdl;
  }
  function partIx(mdl, p) { if (mdl.PI[p] == null) { mdl.PI[p] = mdl.PARTS.length; mdl.PARTS.push(p); } return mdl.PI[p]; }

  // ============================ OCCLUSION MAPS ===============================================================
  /* an orthographic depth map along direction L (toward the light): per texel the largest t = P·L of any face,
     conservative by 0.6 texel so rails and rungs cast. lit(): t(P) ≥ map − bias. */
  function occMap(faceSets, L, res, xf) {
    const w = nrm(L), hint = Math.abs(w[2]) < 0.95 ? [0, 0, 1] : [1, 0, 0], u = nrm(crs(hint, w)), v = crs(w, u);
    let u0 = 1e9, u1 = -1e9, v0 = 1e9, v1 = -1e9;
    const P = (p) => { const q = xf ? xf(p) : p; return [q[0] * u[0] + q[1] * u[1] + q[2] * u[2], q[0] * v[0] + q[1] * v[1] + q[2] * v[2], q[0] * w[0] + q[1] * w[1] + q[2] * w[2]]; };
    const tri = [];
    for (const fs of faceSets) for (const f of fs) { const pv = f.v.map(P); for (const q of pv) { if (q[0] < u0) u0 = q[0]; if (q[0] > u1) u1 = q[0]; if (q[1] < v0) v0 = q[1]; if (q[1] > v1) v1 = q[1]; } tri.push(pv); }
    if (!tri.length) { u0 = v0 = 0; u1 = v1 = 1; }
    u0 -= 2 / res; v0 -= 2 / res;
    const gw = Math.max(1, Math.ceil((u1 - u0) * res) + 4), gh = Math.max(1, Math.ceil((v1 - v0) * res) + 4), T = new Float32Array(gw * gh).fill(-1e9);
    for (const pv of tri) for (let t = 1; t + 1 < pv.length; t++) {
      const A = pv[0], B = pv[t], Cc = pv[t + 1], ax = (A[0] - u0) * res, ay = (A[1] - v0) * res, bx = (B[0] - u0) * res, by = (B[1] - v0) * res, cx = (Cc[0] - u0) * res, cy = (Cc[1] - v0) * res;
      const area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay), sg = area < 0 ? -1 : 1, lab = Math.hypot(bx - ax, by - ay) || 1, lbc = Math.hypot(cx - bx, cy - by) || 1, lca = Math.hypot(ax - cx, ay - cy) || 1;
      const tmin = Math.min(A[2], B[2], Cc[2]), tmax = Math.max(A[2], B[2], Cc[2]), deg = Math.abs(area) < 1e-6;
      const X0 = Math.max(0, Math.floor(Math.min(ax, bx, cx) - 1)), X1 = Math.min(gw - 1, Math.ceil(Math.max(ax, bx, cx) + 1)), Y0 = Math.max(0, Math.floor(Math.min(ay, by, cy) - 1)), Y1 = Math.min(gh - 1, Math.ceil(Math.max(ay, by, cy) + 1));
      for (let y = Y0; y <= Y1; y++) for (let x = X0; x <= X1; x++) {
        const px = x + 0.5, py = y + 0.5;
        const e0 = ((bx - ax) * (py - ay) - (by - ay) * (px - ax)) * sg, e1 = ((cx - bx) * (py - by) - (cy - by) * (px - bx)) * sg, e2 = ((ax - cx) * (py - cy) - (ay - cy) * (px - cx)) * sg;
        if (e0 / lab < -0.6 || e1 / lbc < -0.6 || e2 / lca < -0.6) continue;
        let tv; if (deg) tv = tmax; else { const wa = clamp(e1 / Math.abs(area), 0, 1), wb = clamp(e2 / Math.abs(area), 0, 1), wc = clamp(1 - wa - wb, 0, 1); tv = clamp(wa * A[2] + wb * B[2] + wc * Cc[2], tmin, tmax); }
        const i = y * gw + x; if (tv > T[i]) T[i] = tv;
      }
    }
    return { u, v, w, u0, v0, res, gw, gh, T };
  }
  function litIn(M, x, y, z, bias) {
    const pu = x * M.u[0] + y * M.u[1] + z * M.u[2], pv = x * M.v[0] + y * M.v[1] + z * M.v[2], iu = Math.floor((pu - M.u0) * M.res), iv = Math.floor((pv - M.v0) * M.res);
    if (iu < 0 || iv < 0 || iu >= M.gw || iv >= M.gh) return 1;
    return (x * M.w[0] + y * M.w[1] + z * M.w[2]) >= M.T[iv * M.gw + iu] - bias ? 1 : 0;
  }
  // nine sky directions: the zenith and a ring at 32°
  const SKYD = [[0, 0, 1]]; for (let k = 0; k < 8; k++) { const a = k * Math.PI / 4 + 0.2, c = Math.cos(32 * D2R); SKYD.push([Math.cos(a) * c, Math.sin(a) * c, Math.sin(32 * D2R)]); }
  function aoMaps(mdl) { if (!mdl._ao) mdl._ao = SKYD.map(d => occMap([mdl.static], d, 12)); return mdl._ao; }
  function aoGroup(mdl, g) { mdl._aoG = mdl._aoG || {}; if (!mdl._aoG[g]) mdl._aoG[g] = SKYD.map(d => occMap([mdl.moving.filter(f => f.grp === g)], d, 16)); return mdl._aoG[g]; }

  // ============================ FRAME ========================================================================
  function frame(key, o) {
    o = o || {};
    const mdl = model(key, o), s = mdl.s, dir = (((o.dir | 0) % 8) + 8) % 8, ppu = o.ppu || PPU, C = cam(dir, ppu);
    const tide = o.tide != null ? +o.tide : s.tide, fi = (((o.frame | 0) % LOOP) + LOOP) % LOOP, wind = Object.assign({ w: 0.2, gust: 0.4, dir: 1 }, o.wind || {});
    const fk = [dir, ppu, tide.toFixed(3), fi, wind.w.toFixed(3), o.sea || '', o.shelter == null ? '' : o.shelter, o.waveDeg || 0].join('|');
    let fr = mdl.frames.get(fk); if (fr) return fr;
    const sea = seaOf(o, wind), motion = {}, poses = {};
    for (const g in mdl.groups) { const grp = mdl.groups[g], m = seaMotion(sea, grp, C, fi); motion[g] = Object.assign({ z0: tide + m.heave }, m); poses[g] = poseOf(grp, m, tide + m.heave); }
    // the moving faces this frame: floats posed, gangways solved to their float, chains to their anchors
    const mov = [];
    for (const f of mdl.moving) { const P = poses[f.grp]; mov.push(P ? Object.assign({}, f, { v: f.v.map(P.fwd) }) : f); }
    const gangInfo = [];
    for (const gw of mdl.gangs) { const tmp = [], lz = gw.floatGrp != null && poses[gw.floatGrp] ? poses[gw.floatGrp].fwd([gw.land[0], gw.land[1], mdl.groups[gw.floatGrp].fb])[2] : gw.landZ();
      G.CTX.grp = 90; const r = G.gangwayFaces(tmp, gw.hinge, gw.u[0], gw.u[1], gw.L, lz, gw.W, mdl.T, s); for (const f of tmp) { f.grp = 90; mov.push(f); } gangInfo.push(Object.assign({ id: gw.id || 'gangway' }, r, { L: gw.L })); }
    for (const g in mdl.groups) for (const ch of (mdl.groups[g].chains || [])) { const tmp = [], p0 = poses[g].fwd(ch.attach), slack = Math.max(0, 0.55 - (tide - mdl.s.bedMin) * 0.06);
      G.chainFaces(tmp, p0, ch.anchor, slack); for (const f of tmp) { f.grp = 91; mov.push(f); } }
    G.CTX.grp = 0;
    // the cell: static faces ∪ the moving faces swept over the whole tide range, so every frame and tide shares it
    let R = mdl.rasters.get(dir + '|' + ppu);
    if (!R) {
      let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9; const add = (p) => { const q = proj(p, C); if (q[0] < x0) x0 = q[0]; if (q[0] > x1) x1 = q[0]; if (q[1] < y0) y0 = q[1]; if (q[1] > y1) y1 = q[1]; };
      for (const f of mdl.static) for (const p of f.v) add(p);
      for (const zz of [-0.35, mdl.tideRange + 0.35]) { for (const f of mdl.moving) for (const p of f.v) add([p[0], p[1], p[2] + zz]); for (const f of mov) if (f.grp >= 90) for (const p of f.v) add(p); }
      for (const gw of mdl.gangs) add(gw.hinge);
      if (x0 > x1) { x0 = y0 = 0; x1 = y1 = 1; }
      const W = Math.ceil(x1 - x0) + PAD * 2 + 1, H = Math.ceil(y1 - y0) + PAD * 2 + 1, ox = PAD - Math.floor(x0), oy = PAD - Math.floor(y0);
      R = { W, H, ox, oy, base: raster(mdl, mdl.static, C, W, H, ox, oy, null, null) };
      mdl.rasters.set(dir + '|' + ppu, R);
    }
    const gb = cloneGB(R.base); raster(mdl, mov, C, R.W, R.H, R.ox, R.oy, gb, mdl.static.length);
    cull(gb, R.W, R.H);
    fr = { mdl, s, key, dir, C, ppu, tide, fi, wind, sea, motion, poses, mov, gangs: gangInfo, w: R.W, h: R.H, px: R.ox, py: R.oy, gb, cell: [R.W, R.H], pivot: { x: R.ox, y: R.oy } };
    // static pixels keep the sky visibility and snow byte of the static raster (their occluders never move); movers are redone
    if (!R.sv0) { const f0 = { mdl, C, gb: R.base, w: R.W, h: R.H, poses: {}, tide: 0, wind, wash: -1e9 }; skyVis(f0); snowBytes(f0); R.sv0 = f0.sv; R.sn0 = f0.snow; }
    skyVis(fr, R.sv0); snowBytes(fr, R.sn0);
    if (mdl.frames.size > 40) mdl.frames.delete(mdl.frames.keys().next().value);
    mdl.frames.set(fk, fr); return fr;
  }
  function newGB(N) { return { a: new Uint8Array(N), fid: new Int32Array(N).fill(-1), mi: new Uint16Array(N), sb: new Int8Array(N), nx: new Float32Array(N), ny: new Float32Array(N), nz: new Float32Array(N),
    X: new Float32Array(N), Y: new Float32Array(N), Z: new Float32Array(N), d: new Float32Array(N).fill(1e9), g: new Uint8Array(N), lay: new Uint8Array(N), part: new Uint8Array(N) }; }
  function cloneGB(b) { const o = {}; for (const k in b) o[k] = b[k].slice(); return o; }
  function raster(mdl, faces, C, W, H, ox, oy, gb, fid0) {
    gb = gb || newGB(W * H); fid0 = fid0 || 0; const V = [0, -CE, SE];
    for (let fi = 0; fi < faces.length; fi++) {
      const f = faces[fi], pv = f.v.map(p => proj(p, C)), rv = f.v.map(p => [p[0] * C.ct - p[1] * C.st, p[0] * C.st + p[1] * C.ct, p[2]]);
      if (pv.length < 3) continue;
      let n = nrm(crs([rv[1][0] - rv[0][0], rv[1][1] - rv[0][1], rv[1][2] - rv[0][2]], [rv[2][0] - rv[0][0], rv[2][1] - rv[0][1], rv[2][2] - rv[0][2]]));
      if (n[0] * V[0] + n[1] * V[1] + n[2] * V[2] < 0) n = [-n[0], -n[1], -n[2]];
      const nl = [n[0] * C.ct + n[1] * C.st, -n[0] * C.st + n[1] * C.ct, n[2]], mi = mdl.MI[f.mat] != null ? mdl.MI[f.mat] : mdl.MI.wood;
      const bias = f.b >= 0.45 ? 1 : f.b <= -0.45 ? -1 : 0, tex = f.tex, uv = f.uv, part = partIx(mdl, f.part), g = f.grp > 255 ? 255 : (f.grp | 0), lay = f.lay | 0, id = fid0 + fi;
      for (let t = 1; t + 1 < pv.length; t++) {
        const a = pv[0], b = pv[t], c = pv[t + 1], ax = a[0] + ox, ay = a[1] + oy, bx = b[0] + ox, by = b[1] + oy, cx = c[0] + ox, cy = c[1] + oy;
        const area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay); if (Math.abs(area) < 1e-7) continue;
        const X0 = Math.max(0, Math.floor(Math.min(ax, bx, cx))), X1 = Math.min(W - 1, Math.ceil(Math.max(ax, bx, cx))), Y0 = Math.max(0, Math.floor(Math.min(ay, by, cy))), Y1 = Math.min(H - 1, Math.ceil(Math.max(ay, by, cy)));
        const A = f.v[0], B = f.v[t], Cc = f.v[t + 1], ua = uv ? uv[0] : null, ub = uv ? uv[t] : null, uc = uv ? uv[t + 1] : null;
        for (let y = Y0; y <= Y1; y++) for (let x = X0; x <= X1; x++) {
          const px = x + 0.5, py = y + 0.5, w0 = ((bx - px) * (cy - py) - (cx - px) * (by - py)) / area, w1 = ((cx - px) * (ay - py) - (ax - px) * (cy - py)) / area, w2 = 1 - w0 - w1;
          if (w0 < -0.001 || w1 < -0.001 || w2 < -0.001) continue;
          const d = w0 * a[2] + w1 * b[2] + w2 * c[2], i = y * W + x;
          if (d - f.db >= gb.d[i]) continue;
          let sbv = bias; if (tex && uv) sbv += Math.round(tex(w0 * ua[0] + w1 * ub[0] + w2 * uc[0], w0 * ua[1] + w1 * ub[1] + w2 * uc[1]));
          gb.d[i] = d - f.db; gb.a[i] = 1; gb.fid[i] = id; gb.mi[i] = mi; gb.sb[i] = sbv < -3 ? -3 : sbv > 1 ? 1 : sbv;
          gb.nx[i] = nl[0]; gb.ny[i] = nl[1]; gb.nz[i] = nl[2]; gb.X[i] = w0 * A[0] + w1 * B[0] + w2 * Cc[0]; gb.Y[i] = w0 * A[1] + w1 * B[1] + w2 * Cc[1]; gb.Z[i] = w0 * A[2] + w1 * B[2] + w2 * Cc[2];
          gb.g[i] = g; gb.lay[i] = lay; gb.part[i] = part;
        }
      }
    }
    return gb;
  }
  function cull(gb, W, H) { for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const i = y * W + x; if (!gb.a[i]) continue;
    const n = (x > 0 && gb.a[i - 1]) + (x < W - 1 && gb.a[i + 1]) + (y > 0 && gb.a[i - W]) + (y < H - 1 && gb.a[i + W]); if (!n) { gb.a[i] = 0; gb.fid[i] = -1; gb.d[i] = 1e9; } } }
  // rest coordinates of a pixel (a float pixel back in its own group's rest frame) — AO and snow ride with it
  function restOf(fr, i) { const g = fr.gb.g[i], P = fr.poses[g]; const p = [fr.gb.X[i], fr.gb.Y[i], fr.gb.Z[i]]; return P ? P.inv(p) : p; }
  function skyVis(fr, base) {
    const gb = fr.gb, N = fr.w * fr.h, sv = new Uint8Array(N), AO = aoMaps(fr.mdl);
    for (let i = 0; i < N; i++) { if (!gb.a[i]) continue; if (base && !gb.g[i]) { sv[i] = base[i]; continue; }
      const nx = gb.nx[i], ny = gb.ny[i], nz = gb.nz[i], X = gb.X[i] + nx * 0.03, Y = gb.Y[i] + ny * 0.03, Z = gb.Z[i] + nz * 0.03, g = gb.g[i], mine = g && g < 90 ? aoGroup(fr.mdl, g) : null;
      let rest = null; if (mine) { const r = restOf(fr, i); rest = [r[0] + nx * 0.03, r[1] + ny * 0.03, r[2] + nz * 0.03]; }
      let acc = 0, wt = 0;
      for (let k = 0; k < SKYD.length; k++) { const d = SKYD[k], wk = nx * d[0] + ny * d[1] + nz * d[2]; if (wk <= 0) continue; wt += wk;
        const tn = Math.min(4, Math.sqrt(Math.max(0, 1 - wk * wk)) / Math.max(wk, 0.05));          // slope-scaled: no acne on a plane the map sees at a slant
        let vis = litIn(AO[k], X, Y, Z, 0.06 + 0.062 * tn); if (vis && mine) vis = litIn(mine[k], rest[0], rest[1], rest[2], 0.04 + 0.047 * tn); acc += wk * vis; }
      sv[i] = wt > 1e-3 ? Math.round(acc / wt * 255) : 0; }
    fr.sv = sv;
  }
  /* SNOW: one byte per pixel, the cover × 254 at which it turns to snow (TreeMaps4's contract):
       snowed = round(cover × 254) >= byte;  255 = never (sheltered, vertical, growth, or where the sea washes it)
     The byte is relight()'s own rule solved for the cover, keyed on the rest position so it rides a float. */
  function snowBytes(fr, base) {
    const gb = fr.gb, N = fr.w * fr.h, out = new Uint8Array(N).fill(255), MA = fr.mdl.MA, wash = fr.wash != null ? fr.wash : fr.tide + 0.25 + 0.2 * fr.wind.w;
    for (let i = 0; i < N; i++) { if (!gb.a[i]) continue; if (base && !gb.g[i]) { if (gb.Z[i] >= wash) out[i] = base[i]; continue; }
      const m = MA[gb.mi[i]], up = gb.nz[i], sv = fr.sv[i] / 255; if (!m.hold || up < 0.3 || sv < 0.18 || gb.Z[i] < wash) continue;
      const p = restOf(fr, i), e = 0.60 * vn3(p[0] * 1.6, p[1] * 1.6, p[2] * 1.6, 17) + 0.34 * vn3(p[0] * 5.5, p[1] * 5.5, p[2] * 5.5, 29) + 0.06 * hsh(Math.round(p[0] * 32), Math.round(p[1] * 32), Math.round(p[2] * 32), 31)
        + (1 - up) * 1.4 + (1 - sv) * 0.7 + (1 - m.hold) * 0.6 - (gb.sb[i] <= -2 ? 0.08 : 0);
      const k = (e + 0.12) / 1.3; if (k >= 1) continue; out[i] = clamp(Math.ceil(k * 254), 1, 254); }
    fr.snow = out;
  }

  // ============================ SUN ===========================================================================
  function sunMaps(fr, sky) {
    const Lw = sky.sunW || REF_SKY.sunW, L = nrm(toLocal(fr.C, Lw)), key = L.map(q => Math.round(q * 300)).join(',');
    let S = fr.mdl.sunMaps.get(key); if (!S) { S = occMap([fr.mdl.static], L, 24); if (fr.mdl.sunMaps.size > 12) fr.mdl.sunMaps.delete(fr.mdl.sunMaps.keys().next().value); fr.mdl.sunMaps.set(key, S); }
    fr._sunM = fr._sunM || {}; if (!fr._sunM[key]) fr._sunM[key] = fr.mov.length ? occMap([fr.mov], L, 24) : null;
    return { L, S, M: fr._sunM[key] };
  }
  const sunLit = (SM, x, y, z, b) => { b = b == null ? 0.05 : b; return litIn(SM.S, x, y, z, b) && (!SM.M || litIn(SM.M, x, y, z, b)); };
  const sunBias = (nl) => 0.03 + 0.032 * Math.min(4, Math.sqrt(Math.max(0, 1 - nl * nl)) / Math.max(nl, 0.05));

  // ============================ RELIGHT ======================================================================
  function relight(fr, sky, o) {
    o = o || {}; sky = sky || REF_SKY;
    const gb = fr.gb, W = fr.w, H = fr.h, N = W * H, MA = fr.mdl.MA, out = new Uint8ClampedArray(N * 4), unlit = sky.grade === false && !sky.sunI;
    const Lw = sky.sunW || REF_SKY.sunW, Lv = sky.sunV || toView(Lw), sunI = sky.sunI == null ? 1 : sky.sunI, skyI = sky.skyI == null ? 0.6 : sky.skyI, expo = sky.expo || 1;
    const snow = sky.snow || 0, wet = sky.wet || 0, fog = sky.fog || 0, grade = sky.grade !== false && o.grade !== false, sunOn = sunI > 0.01 && Lw[2] > 0.02;
    const SM = sunOn ? sunMaps(fr, sky) : null, Ll = SM ? SM.L : [0, 0, 1], gB = 0.13 * (sunI * Math.max(0, Lw[2]) + 0.35 * skyI) * (1 + 0.8 * snow), tide = fr.tide;
    const splash = tide + 0.06 + 0.12 * fr.wind.w, damp = tide + 0.40, snowK = Math.round(snow * 254), cover = snow > 0.02;
    let lx = Lv[0], ly = Lv[1]; const ll = Math.hypot(lx, ly); if (ll > 1e-3) { lx /= ll; ly /= ll; }
    const backlit = sunOn && Lv[2] < -0.12 && sunI > 0.12, P0 = MA.length, fi = fr.fi;
    const kc = h2r(sky.kc || '#ffffff'), ac = h2r(sky.ac || '#000000'), wc = h2r(sky.wash || '#ffffff'), fc = h2r(sky.fogC || '#c3cdce'), cache = new Map();
    const colour = (pid, bi, lit, fq, spc, wk) => {
      const key = ((((pid * 5 + bi) * 2 + lit) * 5 + fq) * 3 + spc) * 3 + wk; let c = cache.get(key); if (c) return c;
      const Rr = pid < P0 ? MA[pid].ramp : SNOWR, r = Rr[bi].slice();
      if (spc === 1) for (let k = 0; k < 3; k++) r[k] = Rr[4][k] + (kc[k] - Rr[4][k]) * 0.55; else if (spc === 2) for (let k = 0; k < 3; k++) r[k] = Rr[4][k] + (255 - Rr[4][k]) * 0.6;
      if (grade) { const u = bi / 4, ta = (sky.aa || 0) * (1 - u) * (1 - (sky.amb || 0) * 0.35), tk = (sky.ka || 0) * u * (lit ? 1 : 0.3), wd = wk === 2 ? 0.30 : wk === 1 ? 0.12 : 0;
        for (let k = 0; k < 3; k++) { r[k] += (ac[k] - r[k]) * ta; r[k] += (kc[k] - r[k]) * tk; r[k] *= 1 - wd; if (sky.wa) r[k] += (wc[k] - r[k]) * sky.wa; if (fq) r[k] += (fc[k] - r[k]) * fq * 0.17; } }
      c = [clamp(Math.round(r[0]), 0, 255), clamp(Math.round(r[1]), 0, 255), clamp(Math.round(r[2]), 0, 255)]; cache.set(key, c); return c; };
    const E = new Float32Array(N), LT = new Uint8Array(N);
    for (let i = 0; i < N; i++) { if (!gb.a[i]) continue;
      const nx = gb.nx[i], ny = gb.ny[i], nz = gb.nz[i], X = gb.X[i], Y = gb.Y[i], Z = gb.Z[i], sv = fr.sv[i] / 255, nl = nx * Ll[0] + ny * Ll[1] + nz * Ll[2];
      const vis = SM && nl > 0 ? sunLit(SM, X + nx * 0.03, Y + ny * 0.03, Z + nz * 0.03, sunBias(nl)) : 0, sun = sunI * (nl > 0 ? Math.pow(nl, 1.25) : 0) * vis;
      let cz = 0;                                            // water-bounce dapple: shaded faces a little above the water, sun up
      if (sunOn && !vis && Z > tide && Z < tide + 1.6 && nz < 0.5 && sunI > 0.15) { const k = 1 - (Z - tide) / 1.6, pat = vn3(X * 3.1 + Math.cos(TAU * fi / LOOP) * 0.9, Y * 3.1 + Math.sin(TAU * fi / LOOP) * 0.9, Z * 1.3, 53); cz = (0.04 + (pat > 0.62 ? 0.16 : 0)) * sunI * k * Math.max(0, Lw[2]); }
      E[i] = (0.36 * skyI * (0.30 + 0.70 * sv) * (0.45 + 0.55 * Math.max(0, nz)) + sun + gB * (0.5 - 0.5 * nz) * (0.5 + 0.5 * sv) + cz) * expo; LT[i] = sun > 0.22 ? 1 : 0; }
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x; if (!gb.a[i]) continue;
      const m = MA[gb.mi[i]], nz = gb.nz[i], Z = gb.Z[i], db = unlit ? 0 : faceBand(E[i]), lit = LT[i];
      let pid = gb.mi[i], bi = 2 + gb.sb[i] + db, spc = 0, wk = 0;
      // a crevice: the far side of a depth break of more than 0.16 m takes a band down (pass 1's depth-edge rule)
      const dd = gb.d[i]; let crev = false;
      if (x > 0 && gb.a[i - 1] && dd - gb.d[i - 1] > 0.16) crev = true; else if (x < W - 1 && gb.a[i + 1] && dd - gb.d[i + 1] > 0.16) crev = true;
      else if (y > 0 && gb.a[i - W] && dd - gb.d[i - W] > 0.16) crev = true; else if (y < H - 1 && gb.a[i + W] && dd - gb.d[i + W] > 0.16) crev = true;
      if (crev && !unlit) bi -= 1;
      // wet: the tide's splash line and what it has just left, rain by porosity; lit wet tops glint
      if (!unlit) { const wt = Z < splash ? 0.9 : Z < damp ? 0.45 : 0, ww = Math.max(wt, wet), wp = ww * Math.max(m.por, wt ? 0.25 : 0);
        wk = wp > 0.21 ? 2 : wp > 0.06 ? 1 : 0; if (ww > 0.3 && lit && nz > 0.5 && bi >= 3 && hsh(x, y, fi, 17) < ww * m.gloss * 0.35) spc = 2; }
      if (cover && fr.snow[i] <= snowK) { pid = P0; bi = 3 + db + (nz > 0.9 ? 1 : 0) - (fr.sv[i] < 115 ? 1 : 0); wk = 0; spc = 0; }
      if (backlit && pid !== P0 && nz > 0.25) { const edge = (x === 0 || !gb.a[i - 1]) || (x === W - 1 || !gb.a[i + 1]) || (y === 0 || !gb.a[i - W]);
        if (edge) { const sxn = gb.nx[i] * fr.C.ct - gb.ny[i] * fr.C.st; if (sxn * lx < -0.05 || y === 0 || !gb.a[i - W]) { spc = 1; bi = 4; } } }
      let fq = 0; if (fog > 0.01) { const hf = clamp((Z - tide) / 9, 0, 1); fq = Math.round(clamp(fog * (0.62 + 0.38 * (1 - hf)), 0, 1) * 4); }
      const c = colour(pid, clamp(bi, 0, 4), lit, fq, spc, wk), o4 = i * 4; out[o4] = c[0]; out[o4 + 1] = c[1]; out[o4 + 2] = c[2]; out[o4 + 3] = 255;
    }
    if (o.outline) outline(out, gb.a, W, H);
    if (o.clipBelowWater) for (let i = 0; i < N; i++) if (gb.a[i] && gb.Z[i] < tide) out[i * 4 + 3] = 0;
    return out;
  }
  function outline(out, a, W, H) { const k = h2r(KEYLINE); for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const i = y * W + x; if (a[i]) continue;
    if ((x > 0 && a[i - 1]) || (x < W - 1 && a[i + 1]) || (y > 0 && a[i - W]) || (y < H - 1 && a[i + W])) { out[i * 4] = k[0]; out[i * 4 + 1] = k[1]; out[i * 4 + 2] = k[2]; out[i * 4 + 3] = 255; } } }

  // ============================ SHADE ON THE WATER / GROUND ==================================================
  /* level at a model-space point on the floor: 3 full sun shadow (2 under a thin sun), 2 partial (the edge of a
     shadow: some of four sub-points blocked), 1 canopy — sky straight down blocked, which stays under overcast */
  function shadeAt(fr, sky, x, y, z) {
    sky = sky || REF_SKY; const sunI = sky.sunI == null ? 1 : sky.sunI, Lw = sky.sunW || REF_SKY.sunW;
    let L = 0;
    if (sunI > 0.04 && Lw[2] > 0.03) { const SM = sunMaps(fr, sky); let b = 0; for (const [dx, dy] of [[-0.05, -0.05], [0.05, -0.05], [-0.05, 0.05], [0.05, 0.05]]) b += sunLit(SM, x + dx, y + dy, z + 0.02) ? 0 : 1;
      if (b >= 3) L = sunI > 0.25 ? 3 : 2; else if (b >= 1) L = 2; }
    if (L < 1 && !litIn(aoMaps(fr.mdl)[0], x, y, z + 0.02, 0.1)) L = 1;
    return L;
  }
  // the plane z (default the water) under the cell and its shadow's reach, in CELL pixels
  function castShadow(fr, sky, o) {
    o = o || {}; sky = sky || REF_SKY; const z = o.z != null ? o.z : fr.tide, Lw = sky.sunW || REF_SKY.sunW, C = fr.C, reach = Lw[2] > 0.03 ? Math.min(40, 6 / Math.max(Lw[2], 0.14)) : 0;
    const ex = Math.abs(Lw[0]) * reach * C.S, ey = Math.abs(Lw[1]) * reach * C.S * SE, X0 = Math.floor(-ex) - 2, Y0 = Math.floor(-ey) - 2, w = fr.w + Math.ceil(2 * ex) + 4, h = fr.h + Math.ceil(2 * ey) + 4, lv = new Uint8Array(w * h);
    for (let yy = 0; yy < h; yy++) for (let xx = 0; xx < w; xx++) { const p = unproj(X0 + xx + 0.5 - fr.px, Y0 + yy + 0.5 - fr.py, z, C); lv[yy * w + xx] = shadeAt(fr, sky, p[0], p[1], z); }
    return { x0: X0, y0: Y0, w, h, lv, z };
  }
  /* where the structure pierces the water this frame, in cell pixels: 2 on the line itself, 1 a pixel either side.
     The compositor lays the water shader's foam ramp over these with its lace (TerrainLight6.FOAMR, vn). */
  function contact(fr) {
    const gb = fr.gb, W = fr.w, H = fr.h, m = new Uint8Array(W * H), t = fr.tide;
    for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) { const i = y * W + x; if (!gb.a[i]) continue; const zi = gb.Z[i];
      if (zi >= t - 0.03 && zi <= t + 0.03) { m[i] = 2; for (const j of [i - 1, i + 1, i + W]) if (!m[j]) m[j] = 1; }
      else if (zi < t && gb.a[i - W] && gb.Z[i - W] >= t) { m[i] = 2; if (!m[i - 1]) m[i - 1] = 1; if (!m[i + 1]) m[i + 1] = 1; } }
    return m;
  }

  // ============================ VIEWS · SHEETS ===============================================================
  function view(fr, ch, sky, o) {
    if (!ch || ch === 'lit') return relight(fr, sky || REF_SKY, o);
    if (ch === 'unlit') return relight(fr, UNLIT_SKY, o);
    const gb = fr.gb, N = fr.w * fr.h, out = new Uint8ClampedArray(N * 4), MA = fr.mdl.MA, SM = ch === 'sun' ? sunMaps(fr, sky || REF_SKY) : null, R = fr.mdl.tideRange + 3.5;
    let d0 = 1e9, d1 = -1e9; if (ch === 'depth') for (let i = 0; i < N; i++) if (gb.a[i]) { d0 = Math.min(d0, gb.d[i]); d1 = Math.max(d1, gb.d[i]); }
    for (let i = 0; i < N; i++) { if (!gb.a[i]) continue; let r = 128, g = 128, b = 128; const m = MA[gb.mi[i]];
      if (ch === 'normal') { const w = toWorld(fr.C, [gb.nx[i], gb.ny[i], gb.nz[i]]); r = (w[0] * 0.5 + 0.5) * 255; g = (-w[1] * 0.5 + 0.5) * 255; b = (w[2] * 0.5 + 0.5) * 255; }
      else if (ch === 'ao') r = g = b = fr.sv[i];
      else if (ch === 'sun') { const nl = gb.nx[i] * SM.L[0] + gb.ny[i] * SM.L[1] + gb.nz[i] * SM.L[2], v = nl > 0 && sunLit(SM, gb.X[i] + gb.nx[i] * 0.03, gb.Y[i] + gb.ny[i] * 0.03, gb.Z[i] + gb.nz[i] * 0.03, sunBias(nl)) ? nl : 0; r = 24 + v * 231; g = 22 + v * 200; b = 30 + v * 120; }
      else if (ch === 'height') r = g = b = clamp((gb.Z[i] + 3) / R, 0, 1) * 255;
      else if (ch === 'depth') r = g = b = 255 - (gb.d[i] - d0) / Math.max(1e-3, d1 - d0) * 255;
      else if (ch === 'parts') { const p = gb.part[i]; r = 50 + 200 * hsh(p, 1, 3, 5); g = 50 + 200 * hsh(p, 2, 3, 5); b = 50 + 200 * hsh(p, 3, 3, 5); }
      else if (ch === 'mat') { const c = m.ramp[clamp(2 + gb.sb[i], 0, 4)]; r = c[0]; g = c[1]; b = c[2]; }
      else if (ch === 'wet') { r = m.por * 600; g = m.gloss * 220; b = gb.Z[i] < fr.tide + 0.4 ? 200 : 40; }
      else if (ch === 'snow') { const v = fr.snow[i]; if (v === 255) { r = 30; g = 36; b = 44; } else { r = 255 - v * 0.7; g = 255 - v * 0.55; b = 255 - v * 0.3; } }
      else if (ch === 'sub') { const s2 = fr.tide - gb.Z[i]; if (s2 <= 0) { r = g = b = 40; } else { const k = clamp(s2 / 0.64, 0, 1); r = 30; g = 90 + 120 * (1 - k); b = 140 + 115 * (1 - k); } }
      else if (ch === 'light') { r = fr.sv[i]; g = m.por / 0.4 * 255; b = clamp((gb.Z[i] + 3) / R, 0, 1) * 255; }
      else if (ch === 'detail') { const f = gb.fid[i] + 1; r = f & 255; g = (f >> 8) & 255; b = gb.part[i] * 9 & 255; }
      else if (ch === 'layer') { r = gb.lay[i] ? 230 : 60; g = gb.lay[i] ? 180 : 90; b = gb.g[i] ? 230 : 100; }
      out[i * 4] = r; out[i * 4 + 1] = g; out[i * 4 + 2] = b; out[i * 4 + 3] = 255; }
    return out;
  }
  function sheet(key, o, ch, sky) {
    const f0 = frame(key, Object.assign({}, o, { frame: 0 })), cw = f0.w, chh = f0.h, W = cw * 4, H = chh * 4, out = new Uint8ClampedArray(W * H * 4);
    for (let f = 0; f < LOOP; f++) { const px = view(frame(key, Object.assign({}, o, { frame: f })), ch || 'lit', sky), ox = (f % 4) * cw, oy = Math.floor(f / 4) * chh;
      for (let y = 0; y < chh; y++) out.set(px.subarray(y * cw * 4, (y + 1) * cw * 4), ((oy + y) * W + ox) * 4); }
    return { w: W, h: H, rgba: out, cell: [cw, chh], pivot: [f0.px, f0.py], frames: LOOP, fits: W <= 2048 && H <= 2048 };
  }
  function sheetSpec(key, o) { const f = frame(key, o || {}); return { cell: [f.w, f.h], cols: 4, rows: 4, frames: LOOP, w: f.w * 4, h: f.h * 4, fits: f.w * 4 <= 2048 && f.h * 4 <= 2048, pivot: [f.px, f.py], ppu: f.ppu, elev: ELEV, moving: !!f.mov.length }; }
  /* the deck rock as character v9.2 takes it: render({roll, pitch}) for a figure standing on group g, facing yaw
     (degrees, model frame, 0 = +y). roll tips the figure's top to its right, pitch to its back. */
  function rockFor(fr, g, yawDeg) {
    const m = fr.motion[g]; if (!m) return { roll: 0, pitch: 0 };
    const grp = fr.mdl.groups[g], ca = Math.cos(grp.ax || 0), sa = Math.sin(grp.ax || 0), sx = m.slope[0] * 0.7, sy = m.slope[1] * 0.7, gx = sx * ca - sy * sa, gy = sx * sa + sy * ca;
    const y = (yawDeg || 0) * D2R, fx = Math.sin(y), fy = Math.cos(y), rx = fy, ry = -fx;
    return { roll: +(-Math.atan(gx * rx + gy * ry) / D2R).toFixed(3), pitch: +(Math.atan(gx * fx + gy * fy) / D2R).toFixed(3), heave: +m.heave.toFixed(3) };
  }
  function clearCache() { MODELS.clear(); }

  root.WharfRig2 = { PPU, ELEV, CE, SE, KZ, PYM, LOOP, KEYLINE, SNOWR, REF_SKY, UNLIT_SKY, SEAS, SKYD, toView, dirOf, faceBand, vn3, hsh,
    cam, proj, unproj, toLocal, toWorld, seaOf, seaMotion, poseOf, model, frame, relight, castShadow, shadeAt, contact, view, sheet, sheetSpec, rockFor, restOf, occMap, litIn, clearCache,
    STAGES: G.STAGES, STAGE_KEYS: G.STAGE_KEYS, SEASONS: G.SEASONS, VARIANTS: G.VARIANTS, FAMILIES: G.FAMILIES, PRESETS: G.PRESETS, HULLS: G.HULLS, CLASSES: G.CLASSES, CHAR: G.CHAR, FIT: G.FIT };
})(typeof globalThis !== 'undefined' ? globalThis : window);
