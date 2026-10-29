/* Hidden Harbours — ROADS, WALKS & PATHS · v4  (road / path kit v4, 2026-09-26; the paved street added 2026-09-30).

   v3 stood the roads up to the family vertical but lit them itself: one baked upper-left key, six-step
   ramps with a 4×4 Bayer dither between the steps, alpha contact shadows, and a 32 px blob-47 grid
   every road had to be built from. Pass 9 put the ground, the plants and the trees under one live sky,
   and the road was the one thing on the floor that could not follow it. v4 rebuilds on the pass-9 floor:

   · THE FLOOR'S OWN LIGHT.  A route is painted INTO the pass-9 floor tile it crosses — palette id,
     band, height and mark per texel, structure only — and TerrainLight5 lights it with the grass beside
     it: sky × visibility, sun × self-shadow (a kerb throws its shadow for the hour), a live tip and seam
     per mark (every slab, kerbstone, brick, sett, plank, stone, patch and paint dash is one), wet by
     class (asphalt goes dark and throws a little sky, concrete darkens like soil), water where the section
     holds it, snow by shape, fog, the grade, and the shade TreeRig4 / PlantRig5 cast. No dither, no
     keyline, binary alpha. Soft routes (footpath, alley, lane, the verges and ditch bottoms) are the
     kit's OWN materials, zoned in before PxKit8.floor paints, so its edge painter frays them.
   · CURVES, NOT CELLS.  A route is a polyline through control points in scene px, smoothed by
     centripetal Catmull–Rom and resampled every 3 px. Every texel knows s (along), d (across, + to the
     right of travel) and e (from the carriage edge, + inside), and every pattern is laid in (s, d):
     planks stay square to the boardwalk, slab joints fan on the outside of a bend, a sett course and a
     brick bond follow the street, dashes and zebra bars follow the lane. Routes of one group meet in a
     round union, so a kerb return is a true circular fillet and the sidewalk wraps it at constant width.
   · SECTIONS BY ROUTE CLASS.  The municipal plan's classes (§2.2): footpath · alley · walk · lane ·
     street · route · town · boardwalk, each a cross-section of bands — carriage, gutter, kerb, walk,
     verge, shoulder, ditch — with its own height profile. A lower class meets a higher one by rule: a
     footpath stops at a sidewalk, a drive drops the kerb it crosses and fills the ditch (the culvert
     spot), a boardwalk stands over everything. St Peters uses only the first four, and no kerb.
   · FACES LIT BY THE HOUR.  Raised work (kerbs, slabs, verge ramps, asphalt edges, plank walks, decks)
     projects at the family vertical, 24.5 px = 1 m. Its visible faces are a second small G-buffer with
     their own normals round the curve, relit by the same TerrainLight5.relight: a west-facing kerb end
     takes the evening sun, the boardwalk's piles stand dark under the deck.
   · A ROAD'S OWN WEATHER.  After the relight: carriageways ploughed to wet slush with the edges left,
     town walks shovelled, footpaths and wheel ruts trodden through.

   API (globalThis.RoadKit4) — the floor pipeline, in order:
     net = network(routes, {w, h, seed})     routes: [{id, cls, pts:[[x, y, wMetres?]…] scene px, …overrides}]
     zones(net, zone, zi)                    soft routes into the scene's zone map, BEFORE PxKit8.floor
     paint(tile, net, {grass})               hard surfaces, marks, markings, fringes, heights — AFTER it
     G = TerrainLight5.gbuf(tile, …); patchG(G, net)      one-sided normals + arrises at steps; ponds
                                                          from the ground alone (a deck is not a dam)
     faces(net, tile, G)                     projection map + the face G-buffer (net.F), once per build
     lit = TerrainLight5.relight(G, sky, {lv, frame}); weather(G, net, sky, lit, {lv, frame})
     fl = relightFaces(net, sky, {lv, frame}); out = composite(net, lit, fl)
   and gameplay(net) · roleView(net) · lift(net, x, y) · spline(pts, w) · CLASSES · SURF · PALS.        */
(function (root) {
  'use strict';
  const P = root.PxLang, K = root.PxKit, K8 = root.PxKit8, TL = root.TerrainLight5;
  if (!P || !K || !K8 || !TL) throw new Error('roadPathRig4: pixelLanguage, pxKit, pxKit8 and terrainLight5 must load first');
  const { hash2, clamp } = P, CL = TL.CL, GR = K.GROUND, vn = TL.vn;
  const PPM = 32, ZS = 24.5, HU = PPM / K.DETAIL, STEP = 3;
  const sm = (a, b, x) => { const t = clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };
  const fract = (v) => v - Math.floor(v);
  const fb = (x, y, s) => 0.55 * vn(x, y, s) + 0.3 * vn(x * 2.1 + 5.3, y * 2.1 + 1.7, s + 1) + 0.15 * vn(x * 4.3 + 2.1, y * 4.3 + 7.9, s + 2);
  const hi = (a, b, s) => Math.floor(hash2(a | 0, b | 0, s | 0) * 2147483646);

  const ROLES = ['none', 'carriage', 'gutter', 'kerb', 'walk', 'verge', 'shoulder', 'ditch'];
  const RI = {}; ROLES.forEach((k, i) => { RI[k] = i; });
  const BAND_STR = [0, 0, 3, 2, 2, 1, 2, 1];
  const SK = ['', 'asphalt', 'gravel', 'slab', 'kerb', 'gutter', 'brick', 'setts', 'shell', 'stones', 'planks', 'deck', 'shoulder'];
  const SKI = {}; SK.forEach((k, i) => { SKI[k] = i; });

  // ---- route classes: the municipal plan's, as cross-sections (metres) ---------------------------------------
  const CLASSES = {
    footpath: { label: 'footpath', lane: 'footpath', where: 'St Peters · Nine Mile Creek', group: 'foot', pri: 1, veh: 0, str: 2, w: 1.2, surf: 'trodden', zone: 'dirt', rag: 0.14, wv: 0.2, sink: 0.025, snow: 'trodden', bands: [] },
    alley: { label: 'alley', lane: 'alley', where: 'St Peters (leaves only)', group: 'foot', pri: 0, veh: 0, str: 2, w: 1.0, surf: 'trodden', zone: 'dirt', faint: 0.5, rag: 0.2, wv: 0.28, sink: 0.01, snow: 'trodden', bands: [] },
    walk: { label: 'front walk', lane: 'walk', where: 'St Peters (stones, planks) · Nine Mile Creek (shell)', group: 'walk', pri: 2, veh: 0, str: 2, w: 1.3, surf: 'shell', rag: 0.03, snow: 'trodden', bands: [] },
    lane: { label: 'lane', lane: 'lane', where: 'St Peters · every yard and drive', group: 'lane', pri: 3, veh: 1, str: 3, w: 2.6, surf: 'lane', zone: 'dirt', rag: 0.09, crown: 0.6, ruts: { off: 0.66, w: 0.34, depth: 0.075 }, snow: 'ruts', bands: [] },
    street: { label: 'street', lane: 'street', where: 'Nine Mile Creek (the bar road, Mill Road)', group: 'street', pri: 4, veh: 1, str: 3, w: 5.0, surf: 'gravel', rag: 0.05, camber: 0.07, wear: 0.55, snow: 'plough', bands: [{ role: 'shoulder', w: 0.45 }, { role: 'ditch', w: 1.4, depth: 0.28, bottom: 'sedge' }] },
    route: { label: 'route', lane: 'street', where: 'Nine Mile Creek (Route 91)', group: 'route', pri: 5, veh: 1, str: 3, w: 5.8, surf: 'asphalt', rag: 0.02, camber: 0.05, lift: 0.04, wear: 0.4, marks: { centre: 'auto', edge: 1 }, snow: 'plough', bands: [{ role: 'shoulder', w: 0.55 }, { role: 'ditch', w: 1.45, depth: 0.34, bottom: 'sedge' }] },
    town: { label: 'town street', lane: 'street', where: "Finnigan's Landing only", group: 'town', pri: 5, veh: 1, str: 3, w: 5.6, surf: 'asphalt', rag: 0, camber: 0.05, fillet: 2.2, wear: 0.3, marks: { centre: 'dash' }, snow: 'plough', snowWalk: 'shovel', bands: [{ role: 'gutter', w: 0.3, z: -0.015 }, { role: 'kerb', w: 0.16, z: 0.14 }, { role: 'walk', w: 1.5, z: 0.125, surf: 'slab' }, { role: 'verge', w: 0.7, z: 0.125, z1: 0 }] },
    boardwalk: { label: 'boardwalk', lane: 'walk', where: 'Nine Mile Creek dunes · marsh crossings', group: 'deck', pri: 6, veh: 0, str: 5, w: 1.5, surf: 'deck', rag: 0, lift: 0.35, ends: 'flat', snow: 'none', bands: [] },
    paved: { label: 'paved street', lane: 'street', where: 'Nine Mile Creek (Wharf Road, Water Street)', group: 'route', pri: 4, veh: 1, str: 3, w: 5.6, surf: 'asphalt', rag: 0.02, camber: 0.05, lift: 0.04, wear: 0.35, marks: { centre: 'auto', edge: 1 }, snow: 'plough', bands: [{ role: 'shoulder', w: 0.5 }, { role: 'ditch', w: 1.4, depth: 0.3, bottom: 'sedge' }] },
  };
  const SURF = {
    trodden: { label: 'trodden earth', foot: 'earth', grip: 0.7 }, lane: { label: 'dirt lane', foot: 'earth', grip: 0.68 },
    gravel: { label: 'gravel', foot: 'gravel', grip: 0.66 }, shoulder: { label: 'loose gravel', foot: 'gravel', grip: 0.6 },
    asphalt: { label: 'asphalt', foot: 'stone', grip: 0.94 }, slab: { label: 'concrete slabs', foot: 'stone', grip: 0.9 },
    kerb: { label: 'granite kerb', foot: 'stone', grip: 0.85 }, gutter: { label: 'concrete gutter', foot: 'stone', grip: 0.88 },
    brick: { label: 'brick pavers', foot: 'stone', grip: 0.82 }, setts: { label: 'granite setts', foot: 'stone', grip: 0.76 },
    shell: { label: 'crushed shell', foot: 'shell', grip: 0.66 }, stones: { label: 'stepping stones', foot: 'stone', grip: 0.8 },
    planks: { label: 'plank walk', foot: 'plank', grip: 0.74 }, deck: { label: 'boardwalk deck', foot: 'plank', grip: 0.76 },
  };
  /* bases for PxLang.bands (hue-true, cut per material), registered with the relight's surface classes.
     The class is chosen for how the surface behaves wet: asphalt, granite, setts and gravel take ROCK (they
     go dark and throw a little sky), concrete and brick take SOIL (porous: they darken, no sheen), shell
     SHELL, wood LITTER, the mud in a rut MUD. v3's ramps are where the bases came from. */
  const PALS = {
    asphalt0: ['#3c4043', 0.8, CL.ROCK], asphalt1: ['#4a4e50', 0.8, CL.ROCK], asphalt2: ['#595c5c', 0.78, CL.ROCK], tar: ['#2a2d2f', 0.8, CL.ROCK],
    paintW: ['#d4d6cd', 0.5, CL.ROCK], paintY: ['#cea53f', 0.6, CL.ROCK],
    conc: ['#9c9c92', 0.7, CL.SOIL], conc2: ['#8e8f87', 0.7, CL.SOIL], conc3: ['#a8a69b', 0.7, CL.SOIL],
    granite: ['#8b8b87', 0.85, CL.ROCK], granite2: ['#7b7c7c', 0.85, CL.ROCK],
    brick: ['#8e4a36', 0.8, CL.SOIL], brick2: ['#7a3e30', 0.8, CL.SOIL], brick3: ['#9d5b3f', 0.8, CL.SOIL], sandJ: ['#8f8574', 0.7, CL.SAND],
    sett: ['#78777a', 0.85, CL.ROCK], sett2: ['#6a6866', 0.85, CL.ROCK], sett3: ['#867e75', 0.85, CL.ROCK],
    fines: ['#978a74', 0.75, CL.SOIL], gravel: [GR.gravel, 0.85, CL.ROCK], gravel2: ['#b0a38e', 0.8, CL.ROCK], gravel3: ['#6c665c', 0.85, CL.ROCK],
    mud: [GR.mud, 0.85, CL.MUD], shell: ['#cdc2a8', 0.55, CL.SHELL], shell2: ['#a99d84', 0.65, CL.SHELL],
    stone: [GR.stone, 0.9, CL.ROCK], slate: ['#666b6e', 0.85, CL.ROCK], lichen: ['#9c9a6c', 0.7, CL.ROCK],
    plank: ['#7e776b', 0.8, CL.LITTER], plank2: ['#8c8475', 0.8, CL.LITTER], plank3: ['#9a8061', 0.8, CL.LITTER], pile: ['#4d3e33', 0.85, CL.LITTER],
    grass: [GR.grass, 0.75, CL.VEG],
  };

  // ---- the spline ------------------------------------------------------------------------------------------------
  /* centripetal Catmull–Rom (Barry–Goldman) through the control points, resampled every STEP px of arc.
     A point's third value is its width in metres; widths blend between points. */
  function spline(pts, w) {
    const Pn = pts.map(p => [p[0], p[1], p[2] == null ? w : p[2]]);
    if (Pn.length === 1) Pn.push([Pn[0][0] + 1, Pn[0][1], Pn[0][2]]);
    const n = Pn.length, Q = [[2 * Pn[0][0] - Pn[1][0], 2 * Pn[0][1] - Pn[1][1], Pn[0][2]], ...Pn, [2 * Pn[n - 1][0] - Pn[n - 2][0], 2 * Pn[n - 1][1] - Pn[n - 2][1], Pn[n - 1][2]]];
    const L2 = (a, b, t) => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t];
    const X = [], Y = [], Wm = [];
    for (let i = 1; i < Q.length - 2; i++) {
      const p0 = Q[i - 1], p1 = Q[i], p2 = Q[i + 1], p3 = Q[i + 2];
      const k01 = Math.max(1e-3, Math.sqrt(Math.hypot(p1[0] - p0[0], p1[1] - p0[1]))), k12 = Math.max(1e-3, Math.sqrt(Math.hypot(p2[0] - p1[0], p2[1] - p1[1]))), k23 = Math.max(1e-3, Math.sqrt(Math.hypot(p3[0] - p2[0], p3[1] - p2[1])));
      const t1 = k01, t2 = t1 + k12, t3 = t2 + k23, m = Math.max(2, Math.ceil(Math.hypot(p2[0] - p1[0], p2[1] - p1[1]) / 2));
      for (let j = 0; j < m; j++) {
        const t = t1 + (t2 - t1) * j / m;
        const A1 = L2(p0, p1, t / t1), A2 = L2(p1, p2, (t - t1) / (t2 - t1)), A3 = L2(p2, p3, (t - t2) / (t3 - t2));
        const B1 = L2(A1, A2, t / t2), B2 = L2(A2, A3, (t - t1) / (t3 - t1)), C = L2(B1, B2, (t - t1) / (t2 - t1));
        X.push(C[0]); Y.push(C[1]); Wm.push(p1[2] + (p2[2] - p1[2]) * sm(0, 1, j / m));
      }
    }
    X.push(Pn[n - 1][0]); Y.push(Pn[n - 1][1]); Wm.push(Pn[n - 1][2]);
    const s0 = [0]; for (let k = 1; k < X.length; k++) s0.push(s0[k - 1] + Math.hypot(X[k] - X[k - 1], Y[k] - Y[k - 1]));
    const len = Math.max(1, s0[s0.length - 1]), nS = Math.max(2, Math.round(len / STEP) + 1);
    const x = new Float32Array(nS), y = new Float32Array(nS), s = new Float32Array(nS), hw = new Float32Array(nS), tx = new Float32Array(nS), ty = new Float32Array(nS), kap = new Float32Array(nS);
    let j = 0;
    for (let k = 0; k < nS; k++) {
      const sk = len * k / (nS - 1); while (j < s0.length - 2 && s0[j + 1] < sk) j++;
      const f = clamp((sk - s0[j]) / Math.max(1e-6, s0[j + 1] - s0[j]), 0, 1);
      x[k] = X[j] + (X[j + 1] - X[j]) * f; y[k] = Y[j] + (Y[j + 1] - Y[j]) * f; s[k] = sk; hw[k] = (Wm[j] + (Wm[j + 1] - Wm[j]) * f) * PPM / 2;
    }
    for (let k = 0; k < nS; k++) { const a = Math.max(0, k - 1), b = Math.min(nS - 1, k + 1), dx = x[b] - x[a], dy = y[b] - y[a], L = Math.hypot(dx, dy) || 1; tx[k] = dx / L; ty[k] = dy / L; }
    const kr = new Float32Array(nS);
    for (let k = 1; k < nS - 1; k++) { let da = Math.atan2(ty[k + 1], tx[k + 1]) - Math.atan2(ty[k - 1], tx[k - 1]); while (da > Math.PI) da -= 2 * Math.PI; while (da < -Math.PI) da += 2 * Math.PI; kr[k] = Math.abs(da) / Math.max(1e-3, s[k + 1] - s[k - 1]); }
    const Wn = Math.round(3 * PPM / STEP), cs = new Float64Array(nS + 1); for (let k = 0; k < nS; k++) cs[k + 1] = cs[k] + kr[k];
    for (let k = 0; k < nS; k++) { const a = Math.max(0, k - Wn), b = Math.min(nS, k + Wn + 1); kap[k] = (cs[b] - cs[a]) / (b - a); }
    return { n: nS, x, y, s, hw, tx, ty, kap, len, ds: len / (nS - 1) };
  }
  const kapAt = (sp, s) => sp.kap[clamp(Math.round(s / sp.ds), 0, sp.n - 1)];

  // ---- per-route fields --------------------------------------------------------------------------------------------
  function prep(r, k, seed) {
    const C = CLASSES[r.cls] || CLASSES.footpath, q = Object.assign({}, C, r);
    q.k = k; q.cls = CLASSES[r.cls] ? r.cls : 'footpath'; q.id = r.id || (q.cls + '_' + k);
    q.bands = (r.bands || C.bands).map(b => Object.assign({}, b));
    if (r.walkSurf) for (const b of q.bands) if (b.role === 'walk') b.surf = r.walkSurf;
    if (q.surf === 'planks' && r.lift == null) q.lift = 0.07;
    if ((q.surf === 'planks' || q.surf === 'deck') && r.ends == null) q.ends = 'flat';
    q.hwc = q.w * PPM / 2; q.seed = (seed | 0) + k * 131 + 17;
    q.sp = spline(r.pts, q.w);
    q.reach = q.hwc * (1 + (q.wv || 0)) + q.bands.reduce((a, b) => a + b.w * PPM, 0) + (q.rag || 0) * PPM + 4;
    q.xw = []; q.stops = []; q.jx = [];
    return q;
  }
  /* nearest point on the route for every texel in its reach: coarse chords first, then the fine samples
     round the best chord. Folds straight into the group's best-two, so a group costs one pass per route. */
  function splat(q, g, W, H) {
    const sp = q.sp, n = sp.n, CS = 8, reach = q.reach, cut = (reach + 12) * (reach + 12);
    const cx = []; for (let k = 0; k < n - 1; k += CS) cx.push(k); if (cx[cx.length - 1] !== n - 1) cx.push(n - 1);
    let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
    for (let k = 0; k < n; k++) { if (sp.x[k] < x0) x0 = sp.x[k]; if (sp.x[k] > x1) x1 = sp.x[k]; if (sp.y[k] < y0) y0 = sp.y[k]; if (sp.y[k] > y1) y1 = sp.y[k]; }
    x0 = Math.max(0, Math.floor(x0 - reach)); y0 = Math.max(0, Math.floor(y0 - reach)); x1 = Math.min(W - 1, Math.ceil(x1 + reach)); y1 = Math.min(H - 1, Math.ceil(y1 + reach));
    const rag = (q.rag || 0) * PPM, wv = q.wv || 0, sd = q.seed, flat = q.ends === 'flat', nc = cx.length;
    const X = sp.x, Y = sp.y;
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const px = x + 0.5, py = y + 0.5;
      let best = 1e18, bc = 0;
      for (let c = 0; c < nc - 1; c++) {
        const ax = X[cx[c]], ay = Y[cx[c]], dx = X[cx[c + 1]] - ax, dy = Y[cx[c + 1]] - ay, L2 = dx * dx + dy * dy || 1e-6;
        let t = ((px - ax) * dx + (py - ay) * dy) / L2; t = t < 0 ? 0 : t > 1 ? 1 : t;
        const ex = ax + dx * t - px, ey = ay + dy * t - py, d2 = ex * ex + ey * ey; if (d2 < best) { best = d2; bc = c; }
      }
      if (best > cut) continue;
      const kA = cx[Math.max(0, bc - 1)], kB = cx[Math.min(nc - 1, bc + 2)];
      let bd = 1e18, bk = kA, bt = 0, braw = 0;
      for (let k = kA; k < kB; k++) {
        const ax = X[k], ay = Y[k], dx = X[k + 1] - ax, dy = Y[k + 1] - ay, L2 = dx * dx + dy * dy || 1e-6;
        const tr = ((px - ax) * dx + (py - ay) * dy) / L2, t = tr < 0 ? 0 : tr > 1 ? 1 : tr, ex = ax + dx * t - px, ey = ay + dy * t - py, d2 = ex * ex + ey * ey;
        if (d2 < bd) { bd = d2; bk = k; bt = t; braw = tr; }
      }
      const k = bk, dx = X[k + 1] - X[k], dy = Y[k + 1] - Y[k], L = Math.hypot(dx, dy) || 1e-6, tx = dx / L, ty = dy / L;
      const vx = px - (X[k] + dx * bt), vy = py - (Y[k] + dy * bt), dist = Math.sqrt(bd), d = tx * vy - ty * vx, s = sp.s[k] + L * bt;
      let hw = sp.hw[k] + (sp.hw[k + 1] - sp.hw[k]) * bt;
      if (wv) hw *= 1 + wv * (vn(s / 90, q.k * 3.1, sd + 5) - 0.5) * 2;
      let f = hw - dist;
      if (flat) { const ex = (k === 0 && braw < 0) ? -braw * L : (k === n - 2 && braw > 1) ? (braw - 1) * L : 0; if (ex > 0) f = Math.min(hw - Math.abs(d), -ex); }
      if (rag) f += rag * (fb(px / 5.5, py / 5.5, sd + 7) - 0.5) * 2;
      if (f < -reach) continue;
      const i = y * W + x;
      if (f > g.F1[i]) { g.F2[i] = g.F1[i]; g.R2[i] = g.R1[i]; g.F1[i] = f; g.R1[i] = q.k; g.D[i] = d; g.S[i] = s; g.TX[i] = tx; g.TY[i] = ty; }
      else if (f > g.F2[i]) { g.F2[i] = f; g.R2[i] = q.k; }
    }
  }
  /* round union of two signed fields (positive inside): a true circular fillet of radius r where they meet */
  function unionRound(f1, f2, r) {
    if (r <= 0 || f2 < -1e8) return f1;
    const a = -f1, b = -f2, ux = Math.max(r - a, 0), uy = Math.max(r - b, 0);
    return -(Math.max(r, Math.min(a, b)) - Math.hypot(ux, uy));
  }
  const _b = { role: 0, a: 0, bw: 0, j: -1 };
  function bandAt(q, e) {
    if (e > 0) { _b.role = 1; _b.a = e; _b.bw = q.hwc; _b.j = -1; return true; }
    let c = 0; const x = -e;
    for (let j = 0; j < q.bands.length; j++) { const w = q.bands[j].w * PPM; if (x < c + w) { _b.role = RI[q.bands[j].role]; _b.a = x - c; _b.bw = w; _b.j = j; return true; } c += w; }
    return false;
  }
  const walkZ = (q) => { for (const b of q.bands) if (b.role === 'walk') return b.z || 0; return 0; };
  const vergeZ = (b, u) => b.z + ((b.z1 == null ? b.z : b.z1) - b.z) * sm(0.1, 0.95, u);

  function network(routes, o) {
    o = o || {};
    const W = o.w | 0, H = o.h | 0, N = W * H, seed = o.seed || 7;
    const R = (routes || []).filter(r => r && r.pts && r.pts.length >= 2).map((r, k) => prep(r, k, seed));
    const groups = [], gi = {};
    for (const q of R) { let g = gi[q.group]; if (!g) { g = gi[q.group] = { key: q.group, routes: [], fillet: 0 }; groups.push(g); } g.routes.push(q); g.fillet = Math.max(g.fillet, (q.fillet || 0) * PPM); }
    for (const g of groups) {
      g.F1 = new Float32Array(N).fill(-1e9); g.F2 = new Float32Array(N).fill(-1e9); g.R1 = new Int16Array(N).fill(-1); g.R2 = new Int16Array(N).fill(-1);
      g.D = new Float32Array(N); g.S = new Float32Array(N); g.TX = new Float32Array(N); g.TY = new Float32Array(N);
      for (const q of g.routes) splat(q, g, W, H);
      g.E = new Float32Array(N); for (let i = 0; i < N; i++) g.E[i] = g.R1[i] < 0 ? -1e9 : unionRound(g.F1[i], g.F2[i], g.fillet);
    }
    const net = { W, H, N, seed, routes: R, groups, role: new Uint8Array(N), rid: new Int16Array(N).fill(-1), bi: new Int8Array(N).fill(-1),
      A: new Float32Array(N), BW: new Float32Array(N), E: new Float32Array(N), S: new Float32Array(N), D: new Float32Array(N), TX: new Float32Array(N), TY: new Float32Array(N),
      drop: new Uint8Array(N), junc: new Uint8Array(N), zr: new Float32Array(N), zg: new Float32Array(N) };
    for (let i = 0; i < N; i++) {
      let wg = null, wq = null, wr = 0, wa = 0, ww = 0, wj = -1, ws = -1, wp = -1, we = 0, vz = 0;
      for (const g of groups) {
        const e = g.E[i]; if (e < -1e8) continue;
        const q = R[g.R1[i]]; if (!bandAt(q, e)) continue;
        const st = _b.role === 1 ? q.str : BAND_STR[_b.role];
        if (_b.role === RI.verge) vz = Math.max(vz, vergeZ(q.bands[_b.j], _b.a / _b.bw));
        if (st > ws || (st === ws && q.pri > wp)) { wg = g; wq = q; wr = _b.role; wa = _b.a; ww = _b.bw; wj = _b.j; ws = st; wp = q.pri; we = e; }
      }
      if (!wg) continue;
      let dk = 0;
      if (wr === 1 && wq.veh) for (const g of groups) {         // a drive through a higher class's kerb and walk: keep them, drop the kerb
        if (g === wg) continue; const e = g.E[i]; if (e < -1e8) continue; const q = R[g.R1[i]];
        if (q.pri <= wq.pri || !bandAt(q, e)) continue;
        if (_b.role === RI.gutter || _b.role === RI.kerb || _b.role === RI.walk) { wg = g; wq = q; wr = _b.role; wa = _b.a; ww = _b.bw; wj = _b.j; we = e; dk = 1; break; }
      }
      net.role[i] = wr; net.rid[i] = wq.k; net.bi[i] = wj; net.A[i] = wa; net.BW[i] = ww; net.E[i] = we;
      net.S[i] = wg.S[i]; net.D[i] = wg.D[i]; net.TX[i] = wg.TX[i]; net.TY[i] = wg.TY[i]; net.drop[i] = dk;
      net.junc[i] = (wg.R2[i] >= 0 && wg.R2[i] !== wg.R1[i] && wg.F2[i] > -1.2 * PPM) ? 1 : 0;
      heights(net, i, wq, wr, wa, ww, wj, dk);
      if (wr === 1 && vz > net.zr[i]) net.zr[i] = vz;          // a carriage through a raised verge follows its ramp
    }
    junctions(net);
    return net;
  }
  function heights(net, i, q, r, a, bw, j, dk) {
    let zr = 0, zg = 0; const d = net.D[i];
    if (r === 1) {
      zr = q.lift || 0;
      const u = clamp(Math.abs(d) / q.hwc, 0, 1);
      if (q.camber) zg += q.camber * (1 - u * u);
      if (q.sink) zg -= q.sink * (1 - u * u);
      if (q.ruts) { const t = Math.abs(Math.abs(d) - q.ruts.off * PPM) / (q.ruts.w * PPM / 2); if (t < 1) zg -= q.ruts.depth * (1 - t * t); }
    } else {
      const b = q.bands[j], u = clamp(a / bw, 0, 1);
      if (r === RI.kerb) zr = dk ? walkZ(q) * sm(0, 1, u) : b.z;
      else if (r === RI.walk) zr = b.z + 0.012 * u;
      else if (r === RI.verge) zr = vergeZ(b, u);
      else if (r === RI.gutter) zg = b.z || 0;
      else if (r === RI.ditch) zg = -(b.depth || 0.3) * Math.pow(Math.sin(Math.PI * u), 0.85);
      else if (r === RI.shoulder) zg = -0.012 * u;
    }
    net.zr[i] = zr; net.zg[i] = zg;
  }
  /* where each route runs through another of its own group — markings stop there; crosswalks and stop
     bars sit just off it, clear of the kerb return */
  function junctions(net) {
    const { W, H } = net, gi = {}; for (const g of net.groups) gi[g.key] = g;
    for (const q of net.routes) {
      const g = gi[q.group], sp = q.sp; q.jx = []; let on = false, s0 = 0;
      for (let k = 0; k < sp.n; k++) {
        const x = Math.floor(sp.x[k]), y = Math.floor(sp.y[k]); let c = false;
        if (x >= 0 && y >= 0 && x < W && y < H) { const i = y * W + x; c = (g.R1[i] >= 0 && g.R1[i] !== q.k && g.F1[i] > 0) || (g.R2[i] >= 0 && g.R2[i] !== q.k && g.F2[i] > 0); }
        if (c && !on) { on = true; s0 = sp.s[k]; } else if (!c && on) { on = false; q.jx.push([s0, sp.s[k]]); }
      }
      if (on) q.jx.push([s0, sp.len]);
      const off = (q.fillet || 0) * PPM * 0.5 + 0.6 * PPM, cw = (q.xwW || 2.4) * PPM, xw = q.crosswalk, base = xw ? cw : 0;
      for (const [a, b] of q.jx) {
        if (xw && xw !== 'b' && a - off - cw > 0.4 * PPM) q.xw.push([a - off - cw, a - off]);
        if (xw && xw !== 'a' && b + off + cw < sp.len - 0.4 * PPM) q.xw.push([b + off, b + off + cw]);
        if (q.stop && b >= sp.len - 2 && a - off - base > 1.2 * PPM) q.stops.push([a - off - base - 0.7 * PPM, a - off - base - 0.4 * PPM, 1]);
        if (q.stop && a <= 2 && b + off + base < sp.len - 1.2 * PPM) q.stops.push([b + off + base + 0.4 * PPM, b + off + base + 0.7 * PPM, -1]);
      }
      for (const c of q.crossings || []) q.xw.push([c * PPM, (c + (q.xwW || 2.4)) * PPM]);
    }
  }

  // ---- the soft routes into the zone map (before PxKit8.floor) -------------------------------------------------------
  function zones(net, zone, zi) {
    const { W, N, role, rid, D, A, BW } = net;
    for (let i = 0; i < N; i++) {
      const r = role[i]; if (!r) continue; const q = net.routes[rid[i]], x = i % W, y = (i / W) | 0;
      let z = null;
      if (r === 1) {
        if (q.zone) z = q.zone;
        if (q.crown && Math.abs(D[i]) < q.crown * PPM / 2 - 2 * (fb(x / 4, y / 4, q.seed + 3) - 0.5)) z = 'grass';
        if (q.faint && fb(x / 8, y / 8, q.seed + 4) < 1 - q.faint - 0.25 * (1 - Math.abs(D[i]) / q.hwc)) z = null;
      } else if (r === RI.shoulder) z = q.shoulderZone || 'path';
      else if (r === RI.ditch) { const u = A[i] / BW[i], b = q.bands[net.bi[i]]; z = Math.abs(u - 0.5) < 0.14 + 0.05 * (fb(x / 6, y / 6, q.seed + 5) - 0.5) ? (b.bottom || 'sedge') : 'grass'; }
      else if (r === RI.verge) z = 'grass';
      if (z && zi[z] != null) zone[i] = zi[z];
    }
  }

  // ---- units: the slab, the stone, the plank — the mark each texel belongs to --------------------------------------
  let _pu = 0, _pr = 0;
  function part(v, L, jit, sd) {
    const b = (m) => m * L + (hash2(m, 3, sd) - 0.5) * jit * L;
    let j = Math.floor(v / L); if (v < b(j)) j--; else if (v >= b(j + 1)) j++;
    const b0 = b(j), b1 = b(j + 1); _pu = clamp((v - b0) / Math.max(1e-3, b1 - b0), 0, 1); return j;
  }
  function unitAt(sk, q, s, d, a, bw, i, LU) {
    const M = PPM, k = q.k;
    switch (sk) {
      case 'slab': { const Ls = 1.5 * M, La = bw > 1.9 * M ? bw / 2 : bw; return 1 + hi(Math.floor(s / Ls), Math.floor(a / La) + 13 * k, 301); }
      case 'kerb': return 1 + hi(part(s, 1.05 * M, 0.3, q.seed + 302), k, 302);
      case 'gutter': return 1 + hi(Math.floor(s / (3 * M)), k, 303);
      case 'brick': {
        if (a < 0.2 * M || a > bw - 0.2 * M) return 1 + hi(Math.floor(s / (0.1 * M)), a < 0.2 * M ? 1 : 2, 304 + k);
        const c = Math.floor((a - 0.2 * M) / (0.1 * M)); return 1 + hi(Math.floor((s + (c & 1 ? 0.1 * M : 0)) / (0.2 * M)), c + 3, 305 + k);
      }
      case 'setts': { const L = 0.12 * M, row = Math.floor(s / L), j = part(a + (row & 1 ? 0.07 * M : 0), 0.14 * M, 0.35, q.seed + row * 7), v = fract(s / L); LU[i] = (1 - (2 * _pu - 1) ** 2) * (1 - (2 * v - 1) ** 2); return 1 + hi(row, j, 306 + k); }
      case 'deck': return a < 2.2 ? 1 + hi(Math.floor(s / (2.4 * M)), d > 0 ? 1 : 2, 307 + k) : 1 + hi(Math.floor(s / (0.2 * M)), 0, 308 + k);
      case 'planks': { const pl = d < 0 ? 0 : 1; return 1 + hi(Math.floor((s + pl * 1.2 * M) / (2.4 * M)), pl, 309 + k); }
      case 'stones': {
        const L = 0.64 * M, j0 = Math.floor(s / L);
        for (let j = j0 - 1; j <= j0 + 1; j++) {
          const sc = (j + 0.5 + (hash2(j, 1, q.seed) - 0.5) * 0.25) * L, dc = ((j & 1) ? 1 : -1) * 0.07 * M + (hash2(j, 2, q.seed) - 0.5) * 0.08 * M;
          const rs = (0.19 + 0.06 * hash2(j, 3, q.seed)) * M, rd = (0.21 + 0.07 * hash2(j, 4, q.seed)) * M, u = (s - sc) / rs, v = (d - dc) / rd;
          const r2 = u * u + v * v + 0.4 * (vn(s / 3.2, d / 3.2, q.seed + j) - 0.5);
          if (r2 < 1) { LU[i] = r2; return 1 + hi(j, 0, 310 + k); }
        }
        return 0;
      }
      case 'asphalt': {
        const L = 4.5 * M, c = Math.floor(s / L), w = q.wear || 0; if (hash2(c, k, q.seed + 20) > 0.15 + 0.5 * w) return 0;
        const s0 = (c + 0.1 + 0.4 * hash2(c, 1, q.seed + 21)) * L, sl = (1.0 + 1.6 * hash2(c, 2, q.seed + 21)) * M, dw = (0.7 + 0.8 * hash2(c, 3, q.seed + 21)) * M, d0 = (hash2(c, 4, q.seed + 21) - 0.5) * Math.max(0, 2 * q.hwc - dw);
        return (s >= s0 && s < s0 + sl && d >= d0 - dw / 2 && d < d0 + dw / 2) ? 1 + hi(c, 5, 311 + k) : 0;
      }
      default: return 0;
    }
  }
  function pothole(q, s, d) {
    const L = 2.6 * PPM, c0 = Math.floor(s / L), p = ((q.wear || 0) - 0.42) * 1.1;
    for (let c = c0 - 1; c <= c0 + 1; c++) {
      if (hash2(c, q.k, q.seed + 80) > p) continue;
      const sc = (c + 0.2 + 0.6 * hash2(c, 1, q.seed + 81)) * L, side = hash2(c, 2, q.seed + 81) < 0.5 ? -1 : 1;
      const dc = side * (q.hwc * 0.5 + (hash2(c, 3, q.seed + 81) - 0.5) * 1.1 * PPM);
      const rs = (0.2 + 0.22 * hash2(c, 4, q.seed + 81)) * PPM, rd = (0.15 + 0.15 * hash2(c, 5, q.seed + 81)) * PPM;
      const u = (s - sc) / rs, v = (d - dc) / rd, r2 = u * u + v * v + 0.45 * (vn(s / 2.5, d / 2.5, q.seed + c) - 0.5);
      if (r2 < 1) { _pr = r2; return 40000 + (hi(c, q.k, 82) % 9000); }
    }
    return 0;
  }
  /* paint on the carriage. {c:'W'|'Y', u: its unit} or null. Lines are 2 px, dashes are units. */
  function markAt(q, s, d, a, junc) {
    const M = q.marks || {};
    for (const c of q.xw) if (s >= c[0] && s < c[1]) { if (a < 0.3 * PPM) return null; const bar = Math.floor((d + 4096 + 8) / (0.5 * PPM)); return (bar & 1) ? { c: 'W', u: 50000 + (hi(bar, c[0], 11) % 5000) } : null; }
    for (const st of q.stops) if (s >= st[0] && s < st[1] && d * st[2] > 0 && a > 0.25 * PPM) return { c: 'W', u: 56000 + (q.k & 255) };
    if (M.edge && a >= 0.2 * PPM && a < 0.2 * PPM + 2) return { c: 'W', u: 1 + (hi(Math.floor(s / 300), q.k, 12) % 20000) };
    if (!M.centre || junc) return null;
    const style = M.centre === 'auto' ? (kapAt(q.sp, s) > 1 / (22 * PPM) ? 'double' : 'dash') : M.centre, ad = Math.abs(d), col = M.colour || 'Y';
    if (style === 'double') return ad >= 1 && ad < 3 ? { c: col, u: 20001 + (hi(Math.floor(s / 300), d > 0 ? 1 : 2, q.k + 13) % 9000) } : null;
    if (style === 'solid') return ad < 1 ? { c: col, u: 20001 + (hi(Math.floor(s / 300), 3, q.k + 13) % 9000) } : null;
    const Pd = (M.period || 3.2) * PPM, j = Math.floor(s / Pd);
    return ad < 1 && s - j * Pd < (M.duty || 0.42) * Pd ? { c: col, u: 30001 + (hi(j, q.k, 14) % 9000) } : null;
  }

  // ---- paint (after PxKit8.floor) --------------------------------------------------------------------------------------
  function paint(t, net, o) {
    o = o || {};
    const PA = {}; for (const k in PALS) { const v = PALS[k]; PA[k] = K8.pal(t, v[0], v[1], v[2]); }
    net.PA = PA;
    const { W, N, role, rid, S, D, A, BW, junc } = net;
    const U = new Int32Array(N), SKA = new Uint8Array(N), LU = new Float32Array(N);
    for (let i = 0; i < N; i++) {
      const r = role[i]; if (!r) continue; const q = net.routes[rid[i]];
      let sk = '';
      if (r === 1) sk = q.surf; else if (r === RI.gutter) sk = 'gutter'; else if (r === RI.kerb) sk = 'kerb'; else if (r === RI.walk) sk = q.bands[net.bi[i]].surf || 'slab'; else if (r === RI.shoulder) sk = 'shoulder';
      if (!SKI[sk]) continue;
      SKA[i] = SKI[sk]; U[i] = unitAt(sk, q, S[i], D[i], A[i], BW[i], i, LU);
    }
    net.U = U; net.SKA = SKA;
    const jt = (i) => { const x = i % W, s = SKA[i], u = U[i]; return (x < W - 1 && SKA[i + 1] === s && U[i + 1] !== u) || (i + W < N && SKA[i + W] === s && U[i + W] !== u); };
    const set = (i, pal, band, h, mk) => { t.pal[i] = pal; t.band[i] = band < 0 ? 0 : band > 4 ? 4 : band; t.h[i] = h; t.mark[i] = mk; t.lock[i] = 1; };
    const cid = (j, q) => Math.floor((S[j] + 0.5 * PPM * (vn(D[j] / 12, 2.7, q.seed + 60) - 0.5) * 2) / (5.5 * PPM));
    const crack = (i, q) => {
      const x = i % W, c0 = cid(i, q), same = (j) => rid[j] === q.k && role[j] === 1, p = q.wear;
      if (x < W - 1 && same(i + 1)) { const c1 = cid(i + 1, q); if (c1 !== c0 && hash2(Math.max(c0, c1), q.k, q.seed + 61) < p) return true; }
      if (i + W < N && same(i + W)) { const c2 = cid(i + W, q); if (c2 !== c0 && hash2(Math.max(c0, c2), q.k, q.seed + 61) < p) return true; }
      return Math.abs(D[i]) < 0.6 && p > 0.55 && hash2(Math.floor(S[i] / 64), q.k, q.seed + 62) < p;
    };
    const gid = (j, q) => Math.floor(S[j] / 8.5 + 1.2 * vn(S[j] / 30, D[j] / 30, q.seed + 70)) * 4099 + Math.floor(D[j] / 8.5 + 1.2 * vn(D[j] / 30 + 9, S[j] / 30, q.seed + 71));
    const gator = (i, q) => {
      const lc = q.hwc / 2; if (Math.abs(Math.abs(Math.abs(D[i]) - lc) - 0.7 * PPM) > 0.34 * PPM || fb(S[i] / 90, D[i] / 40, q.seed + 72) < 0.5) return false;
      const x = i % W, g0 = gid(i, q); return (x < W - 1 && rid[i + 1] === q.k && gid(i + 1, q) !== g0) || (i + W < N && rid[i + W] === q.k && gid(i + W, q) !== g0);
    };
    for (let i = 0; i < N; i++) {
      const r = role[i]; if (!r) continue;
      const q = net.routes[rid[i]], x = i % W, y = (i / W) | 0, s = S[i], d = D[i], a = A[i], bw = BW[i], sd = q.seed, wear = q.wear || 0, r1 = hash2(x, y, sd + 11), u = U[i], sk = SK[SKA[i]];
      if (r === 1 && q.surf === 'lane') {                                   // the kit's own earth: ruts and their mud
        if (q.ruts) { const t2 = Math.abs(Math.abs(d) - q.ruts.off * PPM) / (q.ruts.w * PPM / 2);
          if (t2 < 1 && t.pal[i] !== PA.grass) { if (t2 < 0.55) t.band[i] = Math.max(0, t.band[i] - 1); if (t2 < 0.42 && fb(s / 40, d / 6, sd + 50) > 0.5) { t.pal[i] = PA.mud; t.band[i] = r1 < 0.2 ? 1 : 2; t.mark[i] = 0; t.h[i] = Math.min(t.h[i], 0); } } }
        continue;
      }
      if (!sk) continue;
      if (sk === 'asphalt') {
        const pa = wear < 0.34 ? PA.asphalt0 : wear < 0.67 ? PA.asphalt1 : PA.asphalt2, f = fb(x / 26, y / 26, sd + 3);
        let pal = pa, band = 2, h = 0, mk = 0;
        if (r1 < 0.016 + 0.024 * f) { band = 3; h = 0.12; } else if (r1 > 0.972 - 0.03 * (1 - f)) { band = 1; h = -0.1; }
        else if (f > 0.68 && hash2(x >> 1, y >> 1, sd + 4) < 0.3 * wear + 0.08) band = 1;
        const ad = Math.abs(d), lc = q.hwc / 2;
        if (Math.abs(Math.abs(ad - lc) - 0.7 * PPM) < 0.26 * PPM && r1 > 0.25 && r1 < 0.34) band = 3;                // polished wheel paths
        if (Math.abs(ad - lc) < 0.24 * PPM && fb(s / 7, d / 4, sd + 5) > 0.6 && r1 < 0.55) band = 1;                // the drip line, in blots
        if (u) { pal = pa === PA.asphalt0 ? PA.tar : PA.asphalt0; band = r1 < 0.03 + 0.02 * f ? 3 : r1 > 0.975 ? 1 : 2; h = 0.16 + (band === 3 ? 0.1 : 0); mk = u; if (jt(i)) { band = 1; h = 0.05; } }
        else if (wear > 0.22 && crack(i, q)) { if (wear < 0.7) { pal = PA.tar; band = 2; h = 0.05; } else { band = 0; h = -0.45; } }
        else if (wear > 0.7 && gator(i, q)) { band = 0; h = -0.35; }
        const ph = wear > 0.42 ? pothole(q, s, d) : 0;
        if (ph) { band = _pr > 0.62 ? 1 : 0; h = -1.4 * Math.sqrt(Math.max(0, 1 - _pr)) - 0.2; if (_pr < 0.4) pal = PA.fines; mk = ph; }
        else {
          const pm = markAt(q, s, d, a, junc[i]);
          if (pm && fb(x / 2.6, y / 2.6, sd + 31) > 0.18 + wear * 0.42 + (fb(s / 45, d / 18, sd + 32) > 0.72 ? wear * 0.25 : 0)) { pal = pm.c === 'Y' ? PA.paintY : PA.paintW; band = r1 < 0.05 ? 3 : 2; h += 0.06; mk = pm.u; }
        }
        set(i, pal, band, h, mk);
      } else if (sk === 'gravel' || sk === 'shoulder') {
        const ad = Math.abs(d), hwc = q.hwc, sh = sk === 'shoulder';
        if (sh && a / bw > 0.5 && hash2(x, y, sd + 45) < (a / bw - 0.5) * 1.7) continue;                           // the verge takes the shoulder back
        let inT = 0;
        if (!sh) for (const tq of (hwc > 1.8 * PPM ? [0.3, 0.72] : [0.55])) if (Math.abs(ad - tq * hwc) < 0.17 * PPM) inT = 1;
        const edge = sh || ad > hwc - 0.3 * PPM, dens = inT ? 0.05 : edge ? 0.3 : 0.16 + 0.08 * fb(x / 14, y / 14, sd + 41);
        let pal = PA.fines, band = 2, h = 0, mk = 0;
        if (inT) { band = r1 < 0.3 ? 3 : 2; const wb = fb(s / 60, d / 30, sd + 46); h = -0.18 + (wb > 0.56 ? 0.12 * Math.sin(s / (0.3 * PPM) * 6.2832 + 2.5 * vn(s / 20, d / 9, sd + 47)) : 0); }
        else if (r1 < 0.05) band = 1;
        if (hash2(x, y, sd + 42) < dens) { const r2 = hash2(x, y, sd + 43); pal = r2 < 0.5 ? PA.gravel : r2 < 0.82 ? PA.gravel2 : PA.gravel3; band = 2; h += 0.35; mk = 1 + (hi(x >> 1, y, sd + 44) % 60000); }
        const ph = !sh && wear > 0.42 ? pothole(q, s, d) : 0;
        if (ph) { pal = PA.fines; band = _pr > 0.6 ? 1 : 0; h = -1.3 * Math.sqrt(Math.max(0, 1 - _pr)) - 0.25; mk = ph; }
        set(i, pal, band, h, mk);
      } else if (sk === 'slab') {
        const hp = hash2(u, 1, 401); let pal = hp < 0.62 ? PA.conc : hp < 0.9 ? PA.conc2 : PA.conc3, band = 2, h = 0, mk = u;
        const hv = hash2(u, 2, 402); if (hv < 0.06 && pal !== PA.conc3) band = 3; else if (hv > 0.95 && pal !== PA.conc2) band = 1;
        const ls = fract(s / (1.5 * PPM)) - 0.5; h = (hash2(u, 3, 403) - 0.5) * 0.7 * ls;
        if (r1 < 0.018) band = Math.min(4, band + 1); else if (r1 > 0.982) band = Math.max(0, band - 1);
        if (net.drop[i]) band = Math.min(band, 2);
        if (jt(i)) { pal = PA.conc2; band = 1; h = -0.45; mk = 0; }
        else if (wear > 0.2 && hash2(u, 4, 404) < wear * 0.3) {
          const La = bw > 1.9 * PPM ? bw / 2 : bw, la = fract(a / La) - 0.5, c0 = (hash2(u, 5, 405) - 0.5) * 0.6, c1 = (hash2(u, 6, 405) - 0.5) * 1.2;
          if (Math.abs((la - c0 - c1 * ls) * La) < 0.6) { band = 0; h = -0.35; }
        }
        if (hash2(x, y, sd + 406) < 0.004) { pal = PA.tar; band = 2; }
        set(i, pal, band, h, mk);
      } else if (sk === 'kerb') {
        let pal = hash2(u, 1, 411) < 0.7 ? PA.granite : PA.granite2, band = 2, h = 0, mk = u;
        if (r1 < 0.05) band = 3; else if (r1 > 0.955) band = 1;
        if (jt(i)) { band = 1; h = -0.4; mk = 0; }
        set(i, pal, band, h, mk);
      } else if (sk === 'gutter') {
        let band = 2, h = 0; if (a / bw > 0.6 && r1 < 0.35) band = 1; if (r1 > 0.97) band = 3;
        if (jt(i)) { band = 1; h = -0.3; }
        set(i, PA.conc2, band, h, 0);
      } else if (sk === 'brick') {
        const hb = hash2(u, 1, 421); let pal = hb < 0.55 ? PA.brick : hb < 0.82 ? PA.brick2 : PA.brick3, band = 2, h = 0.1 + (hash2(u, 2, 422) - 0.5) * 0.2, mk = u;
        const hv = hash2(u, 3, 423); if (hv < 0.1) band = 3; else if (hv > 0.92) band = 1;
        if (jt(i)) { pal = PA.sandJ; band = 2; h = -0.35; mk = 0; }
        set(i, pal, band, h, mk);
      } else if (sk === 'setts') {
        const hb = hash2(u, 1, 431); let pal = hb < 0.5 ? PA.sett : hb < 0.8 ? PA.sett2 : PA.sett3, band = 2, h = 0.1 + 0.55 * LU[i], mk = u;
        const hv = hash2(u, 2, 432); if (hv < 0.1) band = 3; else if (hv > 0.9) band = 1;
        if (jt(i)) { pal = PA.sandJ; band = 1; h = -0.35; mk = 0; }
        set(i, pal, band, h, mk);
      } else if (sk === 'shell') {
        const f = fb(x / 11, y / 11, sd + 441); let pal = PA.shell2, band = 2, h = 0.05;
        if (r1 < 0.16 + 0.08 * f) { pal = PA.shell; band = r1 < 0.03 ? 4 : 3; h = 0.2; } else if (r1 > 0.95) band = 1; else if (f > 0.7 && r1 < 0.4) band = 3;
        if (a < 0.1 * PPM && hash2(x, y, sd + 442) < 0.3) continue;                                               // the grass edge keeps a few blades
        set(i, pal, band, h, 0);
      } else if (sk === 'stones') {
        if (!u) continue;
        const hs = hash2(u, 1, 451); let pal = hs < 0.6 ? PA.stone : PA.slate, band = 2, h = 0.35 + 0.4 * (1 - LU[i]);
        const hv = hash2(u, 2, 452); if (hv < 0.15) band = 3; else if (hv > 0.88) band = 1;
        if (hash2(x, y, sd + 453) < 0.07 && hash2(u, 3, 454) < 0.55) pal = PA.lichen;
        set(i, pal, band, h, u);
      } else if (sk === 'planks') {
        let pal = hash2(u, 1, 461) < 0.6 ? PA.plank : PA.plank2, band = 2, h = 0, mk = u;
        if (Math.abs(d) < 0.5) { band = 0; h = -1.5; mk = 0; }
        else if (jt(i)) { band = 1; h = -0.8; mk = 0; }
        else { const g = hash2(Math.floor(s / 3), Math.round(d), sd + 462); if (g < 0.12) band = 1; else if (g > 0.93) band = 3; }
        set(i, pal, band, h, mk);
      } else if (sk === 'deck') {
        if (a < 2.2) { set(i, PA.plank2, jt(i) ? 1 : 2, 0.15, u); continue; }
        if (jt(i)) { set(i, PA.pile, 1, -1.6, 0); continue; }
        const hp = hash2(u, 1, 471); let pal = hp < 0.6 ? PA.plank : hp < 0.92 ? PA.plank2 : PA.plank3, band = 2, h = (hash2(u, 2, 472) - 0.5) * 0.3;
        const hv = hash2(u, 3, 473); if (hv < 0.12) band = 3; else if (hv > 0.9) band = 1;
        const g = hash2(u, Math.floor((d + 4096) / 3), 474); if (g < 0.06) band = Math.max(0, band - 1); else if (g > 0.95) band = Math.min(4, band + 1);
        if ((Math.abs(Math.abs(d) - (q.hwc - 0.22 * PPM)) < 0.5 || Math.abs(d) < 0.5) && fract(s / (0.2 * PPM)) < 0.3) band = 1;
        set(i, pal, band, h, u);
      }
    }
    // fringes: blades from the grass beside a soft-edged surface lie over its edge
    const FR = { shell: 0.34, gravel: 0.3, shoulder: 0.34, slab: 0.14, brick: 0.14, setts: 0.16, asphalt: 0.08 };
    for (let i = 0; i < N; i++) {
      const p = FR[SK[SKA[i]]]; if (!p) continue; const x = i % W, y = (i / W) | 0;
      for (const j of [i - 1, i + 1, i - W, i + W]) {
        if (j < 0 || j >= N || SKA[j] || (j === i - 1 && x === 0) || (j === i + 1 && x === W - 1)) continue;
        if (TL.CLASS_OF.get(P.r2h(t.pals[t.pal[j]][2])) !== CL.VEG || Math.abs(net.zr[j] - net.zr[i]) > 0.03) continue;
        if (hash2(x, y, 491) < p) { t.pal[i] = t.pal[j]; t.band[i] = hash2(x, y, 492) < 0.5 ? 3 : 2; t.h[i] = 0.6; t.mark[i] = 0; }
        break;
      }
    }
    for (let i = 0; i < N; i++) if (role[i]) t.h[i] += (net.zr[i] + net.zg[i]) * HU;
    return net;
  }

  // ---- the G-buffer, road-aware -----------------------------------------------------------------------------------------
  /* gbuf differences the height field straight across a kerb, so the texel under a face would take a lit
     slope it does not have. At a raise step the normal is one-sided, and the raised side takes a rounded
     arris toward any lower neighbour N, E or W (the south edge shows its face instead). Ponds come from
     the ground alone: water gathers in the gutter pan and the ruts, not in a ring round every deck. */
  function box(A, W, H, r) {
    const N = W * H, T = new Float32Array(N), B = new Float32Array(N), k = 1 / (2 * r + 1);
    for (let y = 0; y < H; y++) { const o = y * W; let s = 0; for (let d = -r; d <= r; d++) s += A[o + clamp(d, 0, W - 1)]; for (let x = 0; x < W; x++) { T[o + x] = s * k; s += A[o + Math.min(W - 1, x + r + 1)] - A[o + Math.max(0, x - r)]; } }
    for (let x = 0; x < W; x++) { let s = 0; for (let d = -r; d <= r; d++) s += T[clamp(d, 0, H - 1) * W + x]; for (let y = 0; y < H; y++) { B[y * W + x] = s * k; s += T[Math.min(H - 1, y + r + 1) * W + x] - T[Math.max(0, y - r) * W + x]; } }
    return B;
  }
  function patchG(G, net) {
    const W = G.n, Hh = G.m, N = W * Hh, zr = net.zr, H = G.H, kz = HU * G.rel;
    for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x, z = zr[i], iL = x > 0 ? i - 1 : i, iR = x < W - 1 ? i + 1 : i, iU = y > 0 ? i - W : i, iD = y < Hh - 1 ? i + W : i;
      const sL = Math.abs(zr[iL] - z) > 0.012, sR = Math.abs(zr[iR] - z) > 0.012, sU = Math.abs(zr[iU] - z) > 0.012, sD = Math.abs(zr[iD] - z) > 0.012;
      if (!(sL || sR || sU || sD)) continue;
      let a = -((sR ? H[i] : H[iR]) - (sL ? H[i] : H[iL])) * 0.5, b = -((sD ? H[i] : H[iD]) - (sU ? H[i] : H[iU])) * 0.5;
      if (sL && zr[iL] < z) a -= 0.6; if (sR && zr[iR] < z) a += 0.6; if (sU && zr[iU] < z) b -= 0.6;
      const L = Math.hypot(a, b, 1); G.nx[i] = a / L; G.ny[i] = b / L; G.nz[i] = 1 / L;
    }
    const Hg = new Float32Array(N); for (let i = 0; i < N; i++) Hg[i] = H[i] - zr[i] * kz;
    const m10 = box(Hg, W, Hh, 10); for (let i = 0; i < N; i++) G.pond[i] = Math.max(0, m10[i] - Hg[i]);
    return G;
  }

  // ---- the projection and the faces --------------------------------------------------------------------------------------
  /* each column walked north to south at the family vertical. A texel's top lands at y − round(zr·ZS);
     where the raise falls by more than 3 cm between two texels the gap is the higher one's FACE, else the
     slope is stretched. SRC maps every screen pixel to the plan texel that shows there (≥ 0) or to a face
     (−1, its attributes in the face G-buffer at that pixel). Built once per build; the sky only relights. */
  function faces(net, t, G) {
    const { W, H, N, zr, role, rid, S } = net, PA = net.PA, U = net.U || new Int32Array(N);
    const SRC = new Int32Array(N); for (let i = 0; i < N; i++) SRC[i] = i;
    const pal = new Uint8Array(N), band0 = new Int8Array(N), cls = new Uint8Array(N), a8 = new Uint8Array(N), nx = new Float32Array(N), ny = new Float32Array(N), nz = new Float32Array(N).fill(1), ao = new Float32Array(N).fill(1);
    const foot = new Int32Array(N).fill(-1), air = new Uint8Array(N), zero = new Float32Array(N);
    const zAt = (x, y) => zr[clamp(y, 0, H - 1) * W + clamp(x, 0, W - 1)];
    const face = (p, o, f, fy, nF, x) => {
      const oy = (o / W) | 0; let hx = -(zAt(x + 1, oy) - zAt(x - 1, oy)), hy = -(zAt(x, oy + 1) - zAt(x, oy - 1)), L = Math.hypot(hx, hy);
      if (L < 1e-5) { hx = 0; hy = 1; } else { hx /= L; hy /= L; }
      if (hy < 0.25) { hy = 0.25; const l2 = Math.hypot(hx, hy); hx /= l2; hy /= l2; }
      let n0 = hx, n1 = hy, n2 = 0; if (fy === 1) { const l3 = Math.hypot(hx, hy, 0.9); n0 = hx / l3; n1 = hy / l3; n2 = 0.9 / l3; }
      const q = rid[o] >= 0 ? net.routes[rid[o]] : null, sk = SK[net.SKA ? net.SKA[o] : 0];
      let pp = t.pal[o], b = 2, a0 = 0.92 - 0.4 * fy / Math.max(1, nF), c = G.cls[o], isAir = 0;
      if (sk === 'deck' || (q && q.surf === 'deck' && role[o] === 1)) {
        const ps = fract(S[o] / (1.2 * PPM));
        if (fy <= 3) { pp = PA.plank2; b = fy === 1 ? 3 : 2; }
        else if (ps < 0.15) { pp = PA.pile; b = ps < 0.03 || ps > 0.12 ? 1 : 2; a0 = 0.75 - 0.3 * fy / nF; }
        else { pp = t.pal[f]; b = 0; a0 = 0.3; c = G.cls[f]; n0 = 0; n1 = 0; n2 = 1; isAir = 1; }
      } else {
        const ox = o % W, j = ox < W - 1 ? o + 1 : o;
        b = (net.SKA && net.SKA[j] === net.SKA[o] && U[j] !== U[o]) ? 1 : 2;
        if (fy === nF && nF > 1) b = Math.max(0, b - 1);
        if (sk === 'slab' || sk === 'kerb') { const r1 = hash2(x, fy, 481); if (r1 < 0.06) b = Math.min(4, b + 1); }
      }
      pal[p] = pp; band0[p] = b; cls[p] = c; a8[p] = 255; nx[p] = n0; ny[p] = n1; nz[p] = n2; ao[p] = a0; foot[p] = f; air[p] = isAir;
    };
    for (let x = 0; x < W; x++) {
      let syp = -1e9, zp = 0, op = -1;
      for (let y = 0; y < H; y++) {
        const i = y * W + x, z = zr[i], sy = y - Math.round(z * ZS);
        if (op >= 0 && sy > syp + 1) {
          const big = zp - z > 0.03, nF = sy - syp - 1;
          for (let r = syp + 1; r < sy; r++) { if (r < 0 || r >= H) continue; const p = r * W + x; if (!big) { SRC[p] = i; continue; } SRC[p] = -1; face(p, op, i, r - syp, nF, x); }
        }
        if (sy >= 0 && sy < H) SRC[sy * W + x] = i;
        syp = sy; zp = z; op = i;
      }
    }
    const only = []; for (let p = 0; p < N; p++) if (SRC[p] === -1) only.push(p);
    const Gf = { t: { pals: t.pals, pal, mark: new Uint16Array(N), a: a8 }, n: W, m: H, N, wrap: false, rel: G.rel, ix: (x, y) => clamp(y, 0, H - 1) * W + clamp(x, 0, W - 1),
      H: zero, hmax: 0, nx, ny, nz, ao, proud: zero, hollow: zero, pond: zero, cls, band0, elev: null, far: G.far, water: null, fetch: null, slope: null };
    net.F = { SRC, G: Gf, only: Int32Array.from(only), foot, air, lv: new Uint8Array(N), out: new Uint8ClampedArray(N * 4) };
    return net.F;
  }
  function relightFaces(net, sky, o) {
    o = o || {}; const F = net.F; if (!F) return null;
    if (!F.only.length) return F.out;
    for (const p of F.only) F.lv[p] = F.air[p] ? 3 : (o.lv ? o.lv[F.foot[p]] : 0);
    if (o.ch && o.ch !== 'lit') { const v = TL.view(F.G, o.ch, sky, { lv: F.lv }); for (const p of F.only) { const k = p * 4; F.out[k] = v[k]; F.out[k + 1] = v[k + 1]; F.out[k + 2] = v[k + 2]; F.out[k + 3] = 255; } return F.out; }
    TL.relight(F.G, sky, { only: F.only, out: F.out, lv: F.lv, frame: o.frame | 0 });
    return F.out;
  }
  function composite(net, lit, flit, out) {
    const SRC = net.F.SRC, N = SRC.length; out = out || new Uint8ClampedArray(N * 4);
    for (let p = 0; p < N; p++) {
      const s = SRC[p], o4 = p * 4;
      if (s >= 0) { const j = s * 4; out[o4] = lit[j]; out[o4 + 1] = lit[j + 1]; out[o4 + 2] = lit[j + 2]; }
      else { out[o4] = flit[o4]; out[o4 + 1] = flit[o4 + 1]; out[o4 + 2] = flit[o4 + 2]; }
      out[o4 + 3] = 255;
    }
    return out;
  }
  const lift = (net, x, y) => { x = clamp(Math.round(x), 0, net.W - 1); y = clamp(Math.round(y), 0, net.H - 1); return Math.round(net.zr[y * net.W + x] * ZS); };

  // ---- a road's own weather ------------------------------------------------------------------------------------------------
  /* after the floor relight: the plough clears the carriageway to wet slush and leaves its edges; town walks
     are shovelled down the middle; footpaths are trodden through and wheel ruts cleared. Each is the same
     relight over its texels under a sky with less snow, so the light, the grade and the shade still agree. */
  function weather(G, net, sky, out, o) {
    o = o || {}; if (!(sky.snow > 0.02)) return 0;
    const A = [], B = [], C = [], { role, rid, A: AA, BW, D } = net;
    for (let i = 0; i < net.N; i++) {
      const r = role[i]; if (!r) continue; const q = net.routes[rid[i]], mode = q.snow, a = AA[i];
      if (r === 1) {
        if (mode === 'plough') { if (a > 0.4 * PPM) A.push(i); else if (a > 0.15 * PPM) B.push(i); }
        else if (mode === 'ruts' && q.ruts) { if (Math.abs(Math.abs(D[i]) - q.ruts.off * PPM) < q.ruts.w * PPM * 0.4) B.push(i); }
        else if (mode === 'trodden') { if (Math.abs(D[i]) < q.hwc * 0.45) B.push(i); }
      } else if (r === RI.walk && q.snowWalk === 'shovel') { if (a > 0.3 * PPM && a < BW[i] - 0.3 * PPM) C.push(i); }
      else if (r === RI.gutter && mode === 'plough') B.push(i);
    }
    const run = (idx, patch) => { if (idx.length) TL.relight(G, Object.assign({}, sky, patch), { only: Int32Array.from(idx), out, lv: o.lv, frame: o.frame | 0, _tips: o._tips }); };
    run(A, { snow: 0, wet: Math.max(sky.wet || 0, 0.45) });
    run(B, { snow: sky.snow * 0.42, wet: Math.max(sky.wet || 0, 0.35) });
    run(C, { snow: 0, wet: Math.max(sky.wet || 0, 0.3) });
    return A.length + B.length + C.length;
  }

  // ---- views ---------------------------------------------------------------------------------------------------------------
  const ROLE_COL = [[34, 44, 38], [196, 196, 188], [120, 150, 170], [232, 186, 96], [156, 206, 170], [98, 150, 84], [170, 150, 120], [70, 120, 140]];
  function roleView(net) {
    const out = new Uint8ClampedArray(net.N * 4);
    for (let i = 0; i < net.N; i++) {
      const r = net.role[i], c = ROLE_COL[r], o4 = i * 4; let k = 1;
      if (r === 1) { const q = net.routes[net.rid[i]]; k = q.veh ? 0.85 : 0.62; if (net.junc[i]) k *= 0.8; }
      if (net.drop[i]) k *= 0.7;
      out[o4] = c[0] * k; out[o4 + 1] = c[1] * k; out[o4 + 2] = c[2] * k; out[o4 + 3] = 255;
    }
    return out;
  }

  // ---- gameplay sidecar ------------------------------------------------------------------------------------------------------
  function gameplay(net, o) {
    o = o || {}; const r3 = (v) => Math.round(v * 1000) / 1000, m = (v) => r3(v / PPM);
    return {
      schema: 'hidden-harbours/road-gameplay@1', rig: 'roadPathRig4', scene: o.scene || null,
      frame: 'scene metres · 32 px = 1 m · +x east · +y south (toward the camera) · +z up · heights at 24.5 px = 1 m on screen',
      routes: net.routes.map(q => {
        const dec = []; for (let k = 0; k < q.sp.n; k += Math.max(1, Math.round(0.5 * PPM / STEP))) dec.push([m(q.sp.x[k]), m(q.sp.y[k])]);
        dec.push([m(q.sp.x[q.sp.n - 1]), m(q.sp.y[q.sp.n - 1])]);
        let c = q.w / 2; const bands = [{ role: 'carriage', from_m: 0, to_m: r3(q.w / 2), surface: q.surf, z_m: r3(q.lift || 0) }];
        for (const b of q.bands) { const s = b.role === 'kerb' ? 'kerb' : b.role === 'gutter' ? 'gutter' : b.role === 'shoulder' ? 'shoulder' : b.surf || (b.role === 'walk' ? 'slab' : 'ground'); bands.push({ role: b.role, from_m: r3(c), to_m: r3(c + b.w), surface: s, z_m: r3(b.role === 'ditch' ? -(b.depth || 0.3) : b.z || 0) }); c += b.w; }
        const sf = SURF[q.surf] || {}, kb = q.bands.find(b => b.role === 'kerb');
        return {
          id: q.id, class: q.cls, laneClass: q.lane, surface: q.surf, foot: sf.foot || 'earth', grip: r3((sf.grip || 0.7) * (1 - 0.18 * (q.wear || 0))),
          width_m: q.w, length_m: m(q.sp.len), centreline_m: dec, section: bands, symmetric: true,
          kerb: kb ? { z_m: kb.z, step: 'blocks wheels · a person steps up', dropped: 'where a lower vehicular class crosses it' } : null,
          deck: q.lift >= 0.2 ? { z_m: q.lift, edge: 'drop · no rail modelled' } : null,
          junctions_m: q.jx.map(([a, b]) => [m(a), m(b)]), crossings_m: q.xw.map(([a, b]) => [m(a), m(b)]), stops_m: q.stops.map(([a]) => m(a)),
          water: [q.ruts ? 'ruts' : null, (q.wear || 0) > 0.42 ? 'potholes' : null, q.bands.some(b => b.role === 'gutter') ? 'gutter pan' : null, q.bands.some(b => b.role === 'ditch') ? 'ditch bottom' : null, q.sink ? 'trodden centre' : null].filter(Boolean),
          snow: q.snow,
        };
      }),
      _notes: ['Sections are symmetric; the section table is the half-width, carriage centre outward.', 'Where routes of one group meet, the carriage edge is a circular fillet of the group radius; the bands follow it.', 'A footpath ends at a sidewalk; a vehicular class drops the kerb it crosses and fills the ditch.'],
    };
  }

  root.RoadKit4 = { PPM, ZS, HU, STEP, ROLES, RI, CLASSES, SURF, PALS, SK, spline, network, zones, paint, patchG, faces, relightFaces, composite, weather, gameplay, roleView, lift, kapAt, unionRound };
})(typeof globalThis !== 'undefined' ? globalThis : window);
