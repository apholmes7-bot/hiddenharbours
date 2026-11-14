/* NINE MILE CREEK, KEY SCENES: MAIN STREET REDRAWN (hh-nmc-key-scenes/main-street/, 2026-10-05): Water Street's block, the corner up
   Route 91, the square and the back lane's row (globalThis.NmcMainStreet). Kinds on NmcStores' builder, with Shore Lane's walls and the
   creek's and the estates' parts: load plaza-stores/nmcStores.js, shore-lane/nmcShoreLane.js, north-creek/nmcCreekHouses.js and
   south-shore/nmcShoreEstates.js first. Plan metres (e east, n north, z up). A building's origin is the middle of its front at the
   sidewalk: n 0 its front face, n < 0 the sidewalk in front. The camera sees each south face, the corner block's chamfer and the tops.
     msBlock     a main-street building, brick or clapboard: a flat roof behind its parapet and cornice, a gable front or a false front;
                 a storefront to each shop bay (bulkhead, a display open to the street under its glass and lit from behind at dusk, the
                 door in its recess, the transom, the fascia and its channel letters, an awning, goosenecks, an OPEN sign); the flats'
                 windows above (segmental, round, flat-lintelled, hooded or capped); a corner block's chamfer and its east face, whose
                 blade signs face the street; the roof's units, chimneys and a terrace; its own things on the sidewalk (o.walk)
     msBandstand . msFountain . msPlay (a tot lot) . msTownSign . msKiosk (map and notice board) . msShelter (bus) . msCharger (EV)
     msPatio (a fenced terrace) . msProp (o.k: bin planter sandwich drink mailbox hoops bollards grate buckets bench kayaks bikes cone)
     msCar (o.body hatch | suv | sedan, o.paint): parked cars, nose south at dir 4
   The words are trade words (BOOKS, BAKERY, POST OFFICE) and the town's name on its sign; the stores' own names stay open (call 35). */
(function (root) {
  'use strict';
  const NS = root.NmcStores, SL = root.NmcShoreLane, CH = root.NmcCreekHouses, SE = root.NmcShoreEstates;
  if (!NS || !SL || !CH || !SE) throw new Error('nmcMainStreet.js: load nmcStores.js, nmcShoreLane.js, nmcCreekHouses.js and nmcShoreEstates.js first');
  const { md, hsh, TEX, PMATS, F, box, plate, flat, stick, cyl, fbox, lamp, letters, textTex, textW } = NS.lib;
  const { wallX, roofQ, rail, hcyl, buoy, chairD, rtable, umbrella } = SL.lib, { shrub, bench } = CH.lib, { bike, planter } = SE.lib;
  const B = NS.B, K = NS.KINDS, fl = Math.floor, r2 = (v) => Math.round(v * 100) / 100;

  Object.assign(NS.RAMP, {
    brickO: ['#5a2416', '#73301d', '#8d3d25', '#a64d31', '#bb6141', '#cc7856'], brickD: ['#3a1912', '#4c2218', '#5f2c1f', '#743828', '#894634', '#9c5642'],
    stoneL: ['#6d6150', '#857761', '#9d8e74', '#b4a589', '#c8ba9e', '#d9cdb3'], clapB: ['#2f3e4a', '#3c4f5d', '#4b6171', '#5c7586', '#708a9b', '#87a0b0'],
    clapY: ['#7a6a3a', '#95824a', '#af9b5d', '#c7b373', '#dacb8e', '#e9dcaa'], clapR: ['#3e1210', '#561915', '#6f221b', '#882d22', '#9e3b2d', '#b14c3b'],
    clapG: ['#38463a', '#475848', '#586b58', '#6a7f69', '#7e937c', '#94a891'], clapT: ['#163a3c', '#1f4c4e', '#2a6062', '#377577', '#478a8b', '#5b9f9f'],
    awnG: ['#0c2416', '#12321f', '#1a4229', '#225334', '#2c6541', '#38774f'], awnK: ['#0d0e10', '#151619', '#1e2023', '#282b2f', '#33373c', '#3f444a'],
    gilt: ['#5e4a12', '#7c6119', '#987a26', '#b39440', '#c8ab5e', '#dbc182'], roofT: ['#3a3b3a', '#464746', '#535452', '#61625f', '#70716d', '#80817c'],
    verd: ['#2f5a50', '#3b6e62', '#4a8275', '#5b9688', '#70aa9b', '#88bdae'], mulchR: ['#3a2014', '#4a2a1a', '#5a3421', '#6b3f29', '#7c4a32', '#8d563c'],
    slateK: ['#14171a', '#1c2024', '#25292e', '#2f343a', '#3a4047', '#464d55'], plum: ['#2e2333', '#3f3047', '#523f5d', '#664f73', '#7d648b', '#9079a1'],
    teal: ['#123a3a', '#1b4d4b', '#26635e', '#357b73', '#4d968b', '#6cb1a4'], mint: ['#2f5a48', '#3b6e59', '#4a836b', '#5c987e', '#73ad93', '#8fc1a9'],
    carW: ['#7f857f', '#9aa09a', '#b5bab3', '#ccd0c9', '#dfe2dc', '#eef0ea'], carS: ['#3e4448', '#50575c', '#646c71', '#7a8287', '#90989c', '#a7aeb1'],
    carR: ['#3a0c0e', '#55121a', '#741a22', '#93242b', '#ad3436', '#c24a47'], carB: ['#0f1d33', '#162a47', '#1f395e', '#2a4a76', '#3a5e8e', '#5075a5'],
    carK: ['#101214', '#181b1e', '#212529', '#2b3035', '#363c42', '#434a51'], carG: ['#1f3326', '#29432f', '#34553b', '#416848', '#517c57', '#649068'],
  });
  Object.assign(TEX, {
    dentil: (u, v) => md(u, 0.16) < 0.08 ? 0 : -2, tarG: (u, v) => { const h = hsh(fl(u * 12), fl(v * 12), 61); return h < 0.18 ? -1 : h > 0.93 ? 1 : 0; },
    mapT: (u, v) => { const a = Math.sin(u * 7.1) + Math.cos(v * 9.3 + u * 2.2); return a > 0.9 ? 1 : a < -0.8 ? -2 : (md(u + v * 0.6, 0.18) < 0.02 ? -1 : 0); },
    corkN: (u, v) => { const i = fl(u / 0.21), j = fl(v / 0.27); return (md(u, 0.21) < 0.03 || md(v, 0.27) < 0.04) ? -1 : (hsh(i, j, 63) < 0.3 ? -2 : 2); },
    grate: (u, v) => (md(u, 0.08) < 0.03 || md(v, 0.08) < 0.03) ? 0 : -3, slats2: (u, v) => md(v, 0.1) < 0.025 ? -1 : 0,
  });
  const BLOOM = ['pink', 'red', 'yel', 'prodP', 'white', 'rose'];
  const glassQ = (C, M, u0, u1, z0, z1, off, em, tag) => F(C, [M(u0, z0, off), M(u1, z0, off), M(u1, z1, off), M(u0, z1, off)], 'glass', { tag: tag || 'win.glass', em });
  // a band of an arch on a face, radii R1..R2 round (uc, zc), from -t0 to t0 (from the vertical); an arch's glass (the chord and the arc)
  function arcBand(C, M, uc, zc, R1, R2, t0, off, mat, k) { k = k || 8; const P = (r, t) => M(uc + r * Math.sin(t), zc + r * Math.cos(t), off);
    for (let i = 0; i < k; i++) { const a = -t0 + 2 * t0 * i / k, b = -t0 + 2 * t0 * (i + 1) / k; F(C, [P(R1, a), P(R1, b), P(R2, b), P(R2, a)], mat, { tag: 'win.arch' }); } }
  function arcGlass(C, M, uc, zc, R, t0, off, em, k) { k = k || 8; const pts = []; for (let i = 0; i <= k; i++) { const t = t0 - 2 * t0 * i / k; pts.push(M(uc + R * Math.sin(t), zc + R * Math.cos(t), off)); } F(C, pts, 'glass', { tag: 'win.glass', em }); }

  // ---- an upper window: the glass (some flats dark at night), a two-over-two sash, its sill and its head ---------------------------------
  function upWin(C, Wf, h, st, cfg) { const M = Wf.M, fr = cfg.sash || 'trimW', ds = cfg.brick ? cfg.dress : fr, em = (h.dark ? 'dim:' : 'win:') + (C.win++), um = (h.u0 + h.u1) / 2, R = (h.u1 - h.u0) / 2;
    const T = (u0, u1, z0, z1, a, b, m) => fbox(C, M, u0, u1, z0, z1, a, b, m || fr, { tag: 'win.sash' });
    glassQ(C, M, h.u0, h.u1, h.z0, h.z1, -0.1, em); F(C, [M(h.u0 + 0.07, h.z1 - 0.42, -0.095), M(h.u0 + 0.28, h.z1 - 0.42, -0.095), M(h.u0 + 0.1, h.z1 - 0.14, -0.095)], 'glassHi', { tag: 'win.glass', em });
    T(h.u0, h.u0 + 0.06, h.z0, h.z1, -0.1, -0.05); T(h.u1 - 0.06, h.u1, h.z0, h.z1, -0.1, -0.05); const zm = (h.z0 + h.z1) / 2; T(h.u0, h.u1, zm - 0.035, zm + 0.035, -0.1, -0.04); T(um - 0.02, um + 0.02, h.z0, h.z1, -0.1, -0.06);
    T(h.u0 - 0.1, h.u1 + 0.1, h.z0 - 0.12, h.z0, -0.02, 0.09, ds);
    if (st === 'round') { arcGlass(C, M, um, h.z1, R, Math.PI / 2, 0.005, em, 10); arcBand(C, M, um, h.z1, R, R + 0.2, Math.PI / 2, 0.03, ds, 10); T(um - 0.09, um + 0.09, h.z1 + R - 0.03, h.z1 + R + 0.23, 0, 0.07, ds); }
    else if (st === 'seg') { const s = 0.22, Rr = (R * R + s * s) / (2 * s), zc = h.z1 + s - Rr, t2 = Math.asin(Math.min(1, R / Rr)), t3 = Math.asin(Math.min(1, (R + 0.12) / (Rr + 0.13)));
      arcGlass(C, M, um, zc, Rr, t2, 0.005, em, 6); arcBand(C, M, um, zc, Rr + 0.01, Rr + 0.25, t3, 0.03, ds, 8); }
    else if (st === 'flat') T(h.u0 - 0.15, h.u1 + 0.15, h.z1, h.z1 + 0.26, 0, 0.07, ds);
    else if (st === 'hood') { T(h.u0 - 0.17, h.u1 + 0.17, h.z1 + 0.08, h.z1 + 0.22, 0, 0.22, fr); T(h.u0 - 0.13, h.u0 - 0.03, h.z1 - 0.2, h.z1 + 0.08, 0, 0.15, fr); T(h.u1 + 0.03, h.u1 + 0.13, h.z1 - 0.2, h.z1 + 0.08, 0, 0.15, fr); }
    else { T(h.u0 - 0.1, h.u0, h.z0, h.z1, -0.02, 0.035); T(h.u1, h.u1 + 0.1, h.z0, h.z1, -0.02, 0.035); T(h.u0 - 0.13, h.u1 + 0.13, h.z1, h.z1 + 0.15, -0.02, 0.05);
      if (st === 'cap') F(C, [M(h.u0 - 0.16, h.z1 + 0.15, 0.05), M(h.u1 + 0.16, h.z1 + 0.15, 0.05), M(um, h.z1 + 0.4, 0.05)], fr, { tag: 'win.cap' }); }
    if (h.shut) for (const [a, b] of [[h.u0 - 0.5, h.u0 - 0.08], [h.u1 + 0.08, h.u1 + 0.5]]) fbox(C, M, a, b, h.z0 - 0.02, h.z1 + 0.04, 0, 0.04, h.shut, { tag: 'win.shutter', tex: TEX.louvre });
    if (h.box) { fbox(C, M, h.u0 - 0.1, h.u1 + 0.1, h.z0 - 0.42, h.z0 - 0.12, 0.02, 0.26, h.box, { tag: 'win.box' }); for (let u = h.u0 + 0.02, k = 0; u < h.u1 + 0.05; u += 0.17, k++) { const p = M(u, 0, 0.14); cyl(C, p[0], p[1], 0.09, h.z0 - 0.12, h.z0 + 0.06, 6, 'leaf', { tag: 'win.flowers', top: BLOOM[k % 4] }); } } }

  // ---- a display behind the open lower half of a shop window (plan metres: e ea..eb, n 0.1..0.9, its floor zb) ------------------------
  function disp(C, ea, eb, zb, k) {
    const run = (step, f) => { for (let e = ea + step / 2, i = 0; e < eb - step / 3; e += step, i++) f(e, i); };
    if (k === 'books') { box(C, ea, eb, 0.62, 0.9, zb, zb + 1.05, 'wood', { tag: 'disp.case', s: { mat: 'books', tex: TEX.books } });
      run(0.42, (e, i) => { const h = 0.06 + 0.04 * (i % 3); box(C, e - 0.13, e + 0.13, 0.2, 0.42, zb, zb + h, PMATS[i % 8], { tag: 'disp.books', tex: TEX.box }); if (i % 2) box(C, e - 0.11, e + 0.11, 0.3, 0.34, zb + h, zb + h + 0.32, PMATS[(i + 3) % 8], { tag: 'disp.book' }); }); }
    else if (k === 'flowers') run(0.36, (e, i) => { const n = 0.3 + 0.3 * (i % 2), h = 0.28 + 0.1 * (i % 3); cyl(C, e, n, 0.13, zb, zb + h, 7, 'galv', { tag: 'disp.bucket', top: 'leaf' }); cyl(C, e, n, 0.17, zb + h, zb + h + 0.22, 7, 'leaf', { tag: 'disp.bunch', top: BLOOM[i % 6] }); });
    else if (k === 'cakes') { box(C, ea, eb, 0.35, 0.9, zb, zb + 0.35, 'wood', { tag: 'disp.counter', top: 'white' });
      run(0.55, (e, i) => { cyl(C, e, 0.62, 0.04, zb + 0.35, zb + 0.5, 5, 'white'); cyl(C, e, 0.62, 0.2, zb + 0.5, zb + 0.53, 8, 'white'); cyl(C, e, 0.62, 0.16, zb + 0.53, zb + 0.66 + 0.06 * (i % 2), 8, ['cream', 'pink', 'prodBr', 'white'][i % 4], { tag: 'disp.cake', top: ['pink', 'white', 'prodBr', 'red'][i % 4] }); });
      run(0.5, (e) => box(C, e - 0.16, e + 0.16, 0.12, 0.3, zb, zb + 0.12, 'prodBr', { tag: 'disp.loaf', b: 1 })); }
    else if (k === 'icecream') { box(C, ea + 0.05, eb - 0.05, 0.3, 0.9, zb, zb + 0.5, 'enamel', { tag: 'disp.cabinet', top: 'glass' });
      run(0.32, (e, i) => cyl(C, e, 0.55, 0.12, zb + 0.46, zb + 0.56, 7, ['pink', 'cream', 'prodBr', 'mint', 'yel', 'white', 'red'][i % 7], { tag: 'disp.tub' })); }
    else if (k === 'choc') { box(C, ea, eb, 0.45, 0.9, zb, zb + 0.18, 'wood', { tag: 'disp.riser' }); box(C, ea + 0.2, eb - 0.2, 0.62, 0.9, zb + 0.18, zb + 0.4, 'wood', { tag: 'disp.riser' });
      run(0.3, (e, i) => { const tz = zb + (i % 2 ? 0.4 : 0.18), n = i % 2 ? 0.74 : 0.52; box(C, e - 0.11, e + 0.11, n - 0.09, n + 0.08, tz, tz + 0.08, ['prodBr', 'red', 'gilt', 'cream'][i % 4], { tag: 'disp.box', top: i % 3 ? 'red' : 'gilt' }); });
      run(0.6, (e, i) => cyl(C, e, 0.24, 0.1, zb, zb + 0.32, 7, 'glassHi', { tag: 'disp.jar', top: ['red', 'yel', 'pink', 'mint'][i % 4] })); }
    else if (k === 'clothes') run(0.8, (e, i) => { const m = ['navy', 'red', 'cream', 'prodG', 'pink', 'awnK'][i % 6]; box(C, e - 0.08, e + 0.08, 0.46, 0.58, zb, zb + 0.06, 'dark'); stick(C, [e, 0.52, zb + 0.06], [e, 0.52, zb + 0.45], 0.04, 'dark');
      box(C, e - 0.16, e + 0.16, 0.44, 0.6, zb + 0.4, zb + 0.8, i % 2 ? m : 'navy', { tag: 'disp.skirt' }); box(C, e - 0.2, e + 0.2, 0.42, 0.62, zb + 0.8, zb + 1.3, m, { tag: 'disp.top' }); cyl(C, e, 0.52, 0.1, zb + 1.32, zb + 1.55, 7, 'white', { tag: 'disp.head' }); });
    else if (k === 'gifts') { box(C, ea, eb, 0.6, 0.9, zb, zb + 0.95, 'trimW', { tag: 'disp.shelf', s: { mat: 'prodB', tex: TEX.box } });
      run(0.5, (e, i) => { if (i % 2) buoy(C, e, 0.35, zb + 0.32, [['trimW', 'red', 'yel'], ['yel', 'navy', 'red'], ['red', 'trimW', 'prodG']][i % 3]); else { cyl(C, e, 0.35, 0.09, zb, zb + 0.55, 7, 'white', { tag: 'disp.lighthouse' }); cyl(C, e, 0.35, 0.095, zb + 0.22, zb + 0.32, 7, 'red'); cyl(C, e, 0.35, 0.07, zb + 0.55, zb + 0.66, 6, 'red', { top: 'red' }); } }); }
    else if (k === 'gallery') { run(0.9, (e, i) => { stick(C, [e - 0.2, 0.5, zb], [e, 0.45, zb + 1.1], 0.03, 'wood'); stick(C, [e + 0.2, 0.5, zb], [e, 0.45, zb + 1.1], 0.03, 'wood');
        box(C, e - 0.3, e + 0.3, 0.4, 0.44, zb + 0.4, zb + 0.95, ['prodB', 'prodY', 'prodR', 'prodG', 'prodP'][i % 5], { tag: 'disp.canvas', s: { mat: ['prodB', 'prodY', 'prodR', 'prodG', 'prodP'][i % 5], tex: TEX.mapT } }); });
      run(0.7, (e, i) => box(C, e - 0.22, e + 0.22, 0.86, 0.89, zb + 0.35, zb + 0.85, ['prodG', 'prodO', 'blue'][i % 3], { tag: 'disp.frame', s: { mat: ['prodG', 'prodO', 'blue'][i % 3], tex: TEX.mapT } })); }
    else if (k === 'antiques') { const e = ea + (eb - ea) * 0.3; chairD(C, e, 0.55, zb, 'S', 'wood'); cyl(C, ea + 0.35, 0.7, 0.05, zb, zb + 0.9, 5, 'brass'); cyl(C, ea + 0.35, 0.7, 0.2, zb + 0.9, zb + 1.1, 8, 'cream', { tag: 'disp.lamp' });
      box(C, eb - 0.9, eb - 0.15, 0.45, 0.85, zb, zb + 0.4, 'wood', { tag: 'disp.trunk', top: 'cord' }); box(C, eb - 0.6, eb - 0.4, 0.6, 0.72, zb + 0.4, zb + 0.72, 'wood', { tag: 'disp.clock', s: { mat: 'cream' } }); box(C, (ea + eb) / 2 - 0.25, (ea + eb) / 2 + 0.25, 0.86, 0.89, zb + 0.2, zb + 1.0, 'gilt', { tag: 'disp.mirror', s: { mat: 'glassHi' } }); }
    else if (k === 'outfit') { box(C, ea + 0.1, eb - 0.1, 0.5, 0.82, zb + 0.15, zb + 0.42, 'red', { tag: 'disp.kayak', top: 'red' }); box(C, (ea + eb) / 2 - 0.3, (ea + eb) / 2 + 0.3, 0.55, 0.77, zb + 0.42, zb + 0.46, 'dark');
      run(0.45, (e, i) => box(C, e - 0.15, e + 0.15, 0.2, 0.34, zb, zb + 0.45, i % 2 ? 'yel' : 'prodO', { tag: 'disp.pfd' })); stick(C, [eb - 0.3, 0.86, zb], [eb - 0.45, 0.86, zb + 1.2], 0.04, 'wood'); }
    else if (k === 'wine') { box(C, ea, eb, 0.55, 0.9, zb, zb + 1.05, 'wood', { tag: 'disp.racks', s: { mat: 'prodG', tex: TEX.bottle } }); cyl(C, ea + 0.4, 0.35, 0.26, zb, zb + 0.6, 9, 'wood', { tag: 'disp.barrel', top: 'wood' });
      run(0.25, (e, i) => { if (e > ea + 0.75) cyl(C, e, 0.3, 0.04, zb, zb + 0.3, 6, ['prodG', 'prodR', 'dark', 'prodY'][i % 4], { tag: 'disp.bottle', top: 'dark' }); }); }
    else if (k === 'shelves') box(C, ea, eb, 0.55, 0.9, zb, zb + 1.05, 'shelf', { tag: 'disp.shelves', s: { mat: 'prodB', tex: TEX.box } });
    else if (k === 'post') box(C, ea, eb, 0.55, 0.9, zb, zb + 1.05, 'wood', { tag: 'disp.boxes', s: { mat: 'brass', tex: TEX.grille } });
    else if (k === 'brochures') { box(C, ea + 0.1, eb - 0.1, 0.6, 0.85, zb, zb + 1.0, 'wood', { tag: 'disp.rack', s: { mat: 'prodY', tex: TEX.box } }); box(C, ea + 0.3, ea + 1.3, 0.3, 0.34, zb + 0.3, zb + 1.0, 'prodG', { tag: 'disp.map', s: { mat: 'prodG', tex: TEX.mapT } }); }
    else if (k === 'cafe') { box(C, ea + 0.1, eb - 0.1, 0.45, 0.9, zb, zb + 0.4, 'enamel', { tag: 'disp.case', top: 'glass' }); run(0.28, (e, i) => cyl(C, e, 0.68, 0.09, zb + 0.4, zb + 0.47, 7, ['prodBr', 'cream', 'pink'][i % 3], { tag: 'disp.pastry' })); }
  }

  // ---- an awning over a bay: the cloth from the wall out pj and down drop, its valance, two arms; stripes of A.m ------------------------
  function awning(C, Wf, u0, u1, zt, A, f) { const M = Wf.M, pj = A.pj || 1.4, zb = zt - (A.drop || 0.8), ms = A.m || ['awnG'], sw = A.t === 'stripe' ? (A.sw || 0.3) : u1 - u0, vh = A.word ? 0.34 : 0.28;
    for (let u = u0, k = 0; u < u1 - 1e-6; u += sw, k++) { const v = Math.min(u1, u + sw), m = ms[k % ms.length];
      F(C, [M(u, zb, pj), M(v, zb, pj), M(v, zt, 0.04), M(u, zt, 0.04)], m, { tag: 'awning' }); F(C, [M(u, zb - vh, pj), M(v, zb - vh, pj), M(v, zb, pj), M(u, zb, pj)], A.val || m, { tag: 'awning.valance' }); }
    if (A.word && f && f.k === 'S') letters(C, A.word, f.a[0] + (u0 + u1) / 2, -pj, zb - vh + 0.03, 1, A.ink || 'trimW', { d: 0.02 });
    for (const u of [u0 + 0.15, u1 - 0.15]) stick(C, M(u, zt - 0.85, 0.02), M(u, zb + 0.03, pj - 0.06), 0.03, 'iron', { tag: 'awning.arm' }); }

  // ---- a storefront in a bay of a face (u0..u1 along it), its shop floor at z0 -------------------------------------------------------
  function shopfront(C, f, Wf, u0, u1, S, z0, cfg) {
    const M = Wf.M, fr = S.frame || 'green', a = u0 + 0.3, b = u1 - 0.3, hd = z0 + 3.3, south = f.k === 'S', E0 = f.a[0];
    for (const [p, q] of [[u0 - 0.02, a], [b, u1 + 0.02]]) fbox(C, M, p, q, -0.12, hd + 0.8, 0, 0.1, cfg.brick ? cfg.clad : fr, { tag: 'shop.pier', tex: cfg.brick ? cfg.ctex : null });
    fbox(C, M, u0 + 0.02, u1 - 0.02, hd + 0.04, hd + 0.66, 0, 0.12, S.sign || 'navy', { tag: 'shop.fascia', tex: south ? null : textTex(S.word || '', (u0 + u1) / 2, hd + 0.2, 1) });
    fbox(C, M, u0 - 0.06, u1 + 0.06, hd + 0.66, hd + 0.8, 0, 0.2, fr, { tag: 'shop.cornice' });
    if (south && S.word) { const sc = textW(S.word, 2) <= u1 - u0 - 0.5 ? 2 : 1; letters(C, S.word, E0 + (u0 + u1) / 2, -0.12, hd + (sc === 2 ? 0.06 : 0.2), sc, S.ink || 'gilt', { d: 0.035, em: S.lit ? 'glow:cream' : undefined }); }
    const dw = S.dw || 1.0, side = S.door || 'R', du0 = side === 'L' ? a + 0.05 : side === 'C' ? (a + b) / 2 - dw / 2 : b - 0.05 - dw, du1 = du0 + dw, wins = [];
    if (du0 - a > 0.5) wins.push([a, du0 - 0.07]); if (b - du1 > 0.5) wins.push([du1 + 0.07, b]);
    wins.forEach(([p, q], wi) => { const em = 'win:' + (C.win++);
      fbox(C, M, p, q, -0.12, z0 + 0.5, -0.12, -0.02, fr, { tag: 'shop.bulkhead', tex: TEX.panel });
      F(C, [M(p, z0 + 0.5, -0.92), M(q, z0 + 0.5, -0.92), M(q, z0 + 1.75, -0.92), M(p, z0 + 1.75, -0.92)], S.back || 'plaster', { tag: 'shop.back', em });
      if (south) { flat(C, E0 + p, E0 + q, 0.02, 0.92, z0 + 0.5, S.deck || 'wood', { tag: 'shop.display' }); disp(C, E0 + p + 0.05, E0 + q - 0.05, z0 + 0.5, S.disp); }
      else glassQ(C, M, p, q, z0 + 0.5, z0 + 1.55, -0.06, em, 'shop.glass');
      glassQ(C, M, p, q, z0 + 1.55, z0 + 2.75, -0.06, em, 'shop.glass'); F(C, [M(p + 0.12, z0 + 2.3, -0.055), M(p + 0.5, z0 + 2.3, -0.055), M(p + 0.2, z0 + 2.7, -0.055)], 'glassHi', { tag: 'shop.glass', em });
      glassQ(C, M, p, q, z0 + 2.85, hd - 0.04, -0.06, em, 'shop.transom');
      fbox(C, M, p, q, z0 + 0.5, z0 + 0.56, -0.1, 0.02, fr, { tag: 'shop.sill' }); fbox(C, M, p, q, z0 + 1.52, z0 + 1.57, -0.09, -0.04, fr, { tag: 'shop.bar' });
      fbox(C, M, p, q, z0 + 2.75, z0 + 2.85, -0.1, 0, fr, { tag: 'shop.transomBar' }); fbox(C, M, p - 0.04, q + 0.04, hd - 0.06, hd + 0.04, -0.1, 0, fr, { tag: 'shop.head' });
      const nm = Math.max(1, Math.round((q - p) / 1.6)); for (let k = 1; k < nm; k++) { const u = p + (q - p) * k / nm; fbox(C, M, u - 0.035, u + 0.035, z0 + 0.5, hd, -0.1, -0.02, fr, { tag: 'shop.mullion' }); }
      for (let u = p + 0.4; u < q - 0.2; u += 0.4) fbox(C, M, u - 0.015, u + 0.015, z0 + 2.85, hd - 0.04, -0.075, -0.05, fr, { tag: 'shop.muntin' });
      fbox(C, M, p - 0.05, p, z0 + 0.5, hd, -0.1, 0, fr); fbox(C, M, q, q + 0.05, z0 + 0.5, hd, -0.1, 0, fr);
      if (S.open && wi === 0 && south) letters(C, 'OPEN', E0 + (p + q) / 2, 0.05, z0 + 1.95, 1, 'red', { d: 0.02, em: 'neon:red' }); });
    const dem = 'win:' + (C.win++);
    fbox(C, M, du0 - 0.07, du0, z0, hd, -0.26, 0, fr, { tag: 'shop.jamb' }); fbox(C, M, du1, du1 + 0.07, z0, hd, -0.26, 0, fr, { tag: 'shop.jamb' }); fbox(C, M, du0, du1, z0 + 2.62, z0 + 2.75, -0.26, 0, fr, { tag: 'shop.lintel' });
    F(C, [M(du0, z0, -0.26), M(du1, z0, -0.26), M(du1, z0 + 2.62, -0.26), M(du0, z0 + 2.62, -0.26)], S.doorMat || fr, { tag: 'shop.door' });
    glassQ(C, M, du0 + 0.12, du1 - 0.12, z0 + 0.85, z0 + 2.45, -0.25, dem, 'shop.doorGlass'); fbox(C, M, du0 + 0.04, du1 - 0.04, z0 + 0.05, z0 + 0.3, -0.25, -0.23, 'brass', { tag: 'shop.kick' });
    glassQ(C, M, du0, du1, z0 + 2.75, hd - 0.04, -0.06, dem, 'shop.transom');
    if (south) flat(C, E0 + du0, E0 + du1, -0.02, 0.26, z0, 'tile', { tag: 'shop.recess' });
    fbox(C, M, du0 - 0.15, du1 + 0.15, -0.15, z0, -0.02, 0.32, cfg.brick ? 'stoneL' : 'conc', { tag: 'shop.step' });
    const dc = M((du0 + du1) / 2, z0, -0.3), ds = M((du0 + du1) / 2, z0, 0.9); C.st.push({ id: 'door.' + (S.id || S.word || 'shop').toLowerCase().replace(/[^a-z]+/g, '_'), verb: 'enter', at: dc.map(r2), stand: [r2(ds[0]), r2(ds[1])] });
    if (S.aw && S.aw.t !== 'none') awning(C, Wf, u0 + 0.05, u1 - 0.05, hd - 0.02, S.aw, f);
    if (S.goose) for (const t of [0.25, 0.75]) { const u = u0 + (u1 - u0) * t, p0 = M(u, hd + 1.02, 0.02), p1 = M(u, hd + 1.1, 0.5), q = M(u, hd + 0.98, 0.56); stick(C, p0, p1, 0.03, 'iron', { tag: 'shop.goose' }); stick(C, p1, [q[0], q[1], hd + 1.04], 0.03, 'iron', { tag: 'shop.goose' });
      cyl(C, q[0], q[1], 0.1, hd + 0.9, hd + 1.0, 6, 'iron', { tag: 'shop.goose', top: 'iron' }); lamp(C, q[0], q[1], hd + 0.86, '#ffd9a0', 0.5, 3.0); }
    if (S.blade && f.k === 'E') { const nb = f.a[1] + (u0 + u1) / 2, eb = f.a[0], zb = hd - 0.6; stick(C, [eb, nb, zb + 0.75], [eb + 1.25, nb, zb + 0.75], 0.04, 'iron', { tag: 'shop.bladeArm' });
      box(C, eb + 0.2, eb + 1.2, nb - 0.04, nb + 0.04, zb, zb + 0.62, S.sign || 'navy', { tag: 'shop.blade' }); letters(C, S.blade, eb + 0.7, nb - 0.04, zb + 0.18, 1, S.ink || 'gilt', { d: 0.02 }); }
  }

  // ---- the cornice at the head of a face: a corbel table of brick, or a bracketed wooden cornice --------------------------------------
  function cornice(C, M, u0, u1, z, kind, cfg) { const m = cfg.clad, ds = cfg.dress, tx = cfg.ctex;
    if (kind === 'bracket') { fbox(C, M, u0, u1, z - 0.72, z - 0.26, 0, 0.06, ds, { tag: 'cornice.frieze' }); for (let u = u0 + 0.3; u <= u1 - 0.25; u += 0.85) fbox(C, M, u - 0.07, u + 0.07, z - 0.62, z - 0.12, 0, 0.34, ds, { tag: 'cornice.bracket' });
      fbox(C, M, u0 - 0.12, u1 + 0.12, z - 0.12, z + 0.06, 0, 0.44, ds, { tag: 'cornice.crown' }); return; }
    fbox(C, M, u0, u1, z - 0.66, z - 0.54, 0, 0.06, m, { tag: 'cornice.course', tex: tx }); fbox(C, M, u0, u1, z - 0.54, z - 0.4, 0, 0.11, m, { tag: 'cornice.dentil', tex: TEX.dentil });
    fbox(C, M, u0, u1, z - 0.4, z - 0.26, 0, 0.16, m, { tag: 'cornice.course', tex: tx }); fbox(C, M, u0, u1, z - 0.26, z - 0.1, 0, 0.22, ds, { tag: 'cornice.cap' }); }

  /* ==================================================================================================================================
     THE BUILDING ======================================================================================================================== */
  B.msBlock = function (C) {
    const o = C.s.o, w = o.w || 8, d = o.d || 12, hw = w / 2, Z = [0]; for (const h of o.fl || [4.3, 3.3]) Z.push(Z[Z.length - 1] + h);
    const H = Z[Z.length - 1], top = o.top || { t: 'flat' }, cl = o.clad || 'brick', brick = !/^clap|^shingle/.test(cl), ctex = TEX[o.tex || (brick ? 'brick' : /^shingle/.test(cl) ? 'shingle' : 'clap')];
    const cfg = { clad: cl, ctex, brick, dress: o.dress || (brick ? 'stoneL' : 'trimW'), sash: o.sash || 'trimW' }, z0 = o.base != null ? o.base : 0.15, ch = o.cham || 0, cw = o.chamW || 0;   // 10-06: o.chamW, a chamfer at the south-west corner (its bays o.chamWBays), and o.west, bays along the west face
    const gam = top.t === 'gambrel', pitched = top.t === 'gable' || gam, par = pitched ? 0 : (top.par || 1.0), Ht = H + par, bk = top.bk || 1.4, zK = H + (top.p || 1.9) * bk, zRg = zK + (top.pu || 0.5) * (hw - bk);
    const rise = top.t === 'gable' ? (top.rise || w * 0.36) : gam ? zRg - H : top.t === 'false' ? (top.rise || w * 0.3) : 0, rz = top.raise || 0;
    const faces = [{ k: 'S', a: [-hw + cw, 0], b: [hw - ch, 0], out: [0, -1], bays: o.bays || [{ w: w - ch - cw }] }];
    if (cw > 0) faces.push({ k: 'C2', a: [-hw, cw], b: [-hw + cw, 0], out: [-Math.SQRT1_2, -Math.SQRT1_2], bays: o.chamWBays || [{ w: cw * Math.SQRT2 }] });
    if (ch > 0) faces.push({ k: 'C', a: [hw - ch, 0], b: [hw, ch], out: [Math.SQRT1_2, -Math.SQRT1_2], bays: o.chamBays || [{ w: ch * Math.SQRT2 }] });
    faces.push({ k: 'E', a: [hw, ch], b: [hw, d], out: [1, 0], bays: o.east || [], plain: !o.east });
    faces.push({ k: 'N', a: [hw, d], b: [-hw, d], out: [0, 1], bays: [], plain: true }, { k: 'W', a: [-hw, d], b: [-hw, cw], out: [-1, 0], bays: o.west || [], plain: !o.west });
    for (const f of faces) {
      const L = Math.hypot(f.b[0] - f.a[0], f.b[1] - f.a[1]), front = f.k === 'S', gab = pitched && (f.k === 'S' || f.k === 'N'), uc = L / 2;
      const wt = top.t === 'flat' || (front && top.t === 'false') ? Ht : H; let outline = gab ? (gam ? [[0, 0], [L, 0], [L, H], [L - bk, zK], [uc, zRg], [bk, zK], [0, H]] : [[0, 0], [L, 0], [L, H], [uc, H + rise], [0, H]]) : [[0, 0], [L, 0], [L, wt], [0, wt]];
      if (front && rz && top.t !== 'gable') { const rw = top.rw || Math.min(2.2, L * 0.18); outline = [[0, 0], [L, 0], [L, Ht], [uc + rw, Ht], [uc + rw, Ht + rz], [uc - rw, Ht + rz], [uc - rw, Ht], [0, Ht]]; }
      const holes = [], wins = [], shops = []; let u = 0;
      for (const by of f.bays) { const u0 = u, u1 = Math.min(L, u + (by.w || L)); u = u1;
        if (by.s) { holes.push({ u0: u0 + 0.3, u1: u1 - 0.3, z0: 0, z1: z0 + 3.3 }); shops.push([u0, u1, by.s]); }
        else { if (by.door && Z.length > 1) { const dc = by.dc != null ? u0 + by.dc : (u0 + u1) / 2, dw = by.dw || 1.0; holes.push({ u0: dc - dw / 2, u1: dc + dw / 2, z0: z0, z1: z0 + (by.dh || 2.4), t: 'D', dbl: dw > 1.3, word: by.word, wm: by.wm }); wins.push(holes[holes.length - 1]); }
          if (by.gw) { const gww = by.gww || 1.05, gh = by.gwh || 2.1; for (let i = 0; i < by.gw; i++) { const c = u0 + (u1 - u0) * (i + 0.5) / by.gw; if (by.door && Math.abs(c - (by.dc != null ? u0 + by.dc : (u0 + u1) / 2)) < 0.9) continue; const h = { u0: c - gww / 2, u1: c + gww / 2, z0: z0 + 0.85, z1: z0 + 0.85 + gh, st: by.gup || by.up || o.up || (brick ? 'seg' : 'cap'), shut: by.shut, box: by.box }; holes.push(h); wins.push(h); } } }
        const nw = by.n != null ? by.n : Math.max(1, Math.round((u1 - u0) / 2.3)), st = by.up || o.up || (brick ? 'seg' : 'cap');
        for (let k = 1; k < Z.length - 1; k++) { const zf = Z[k], ww = by.ww || (brick ? 0.95 : 0.9), sk = k === Z.length - 2 && by.upTop ? by.upTop : st, head = sk === 'round' ? ww / 2 + 0.26 : sk === 'seg' ? 0.5 : sk === 'cap' ? 0.45 : 0.3, hh = Math.min(by.wh || 2.05, Z[k + 1] - zf - 0.78 - head - 0.15);
          for (let i = 0; i < nw; i++) { const c = u0 + (u1 - u0) * (i + 0.5) / nw, h = { u0: c - ww / 2, u1: c + ww / 2, z0: zf + 0.78, z1: zf + 0.78 + hh, st: sk,
            dark: hsh(Math.round(c * 10), k, (o.seed | 0) + 5) < (o.darkFlats != null ? o.darkFlats : 0.35), shut: by.shut, box: by.box }; holes.push(h); wins.push(h); } } }
      if (gab && f.k === 'S' && rise > 2.4) { const h = { u0: uc - 0.42, u1: uc + 0.42, z0: H + 0.45, z1: H + Math.min(1.75, rise - 0.9), st: top.attic || 'cap' }; holes.push(h); wins.push(h); }
      if (f.plain && !front) { wallX(C, { a: f.a, b: f.b, out: f.out, outline, holes: [], mat: f.k === 'N' && brick ? 'brickD' : cl, tex: ctex, inMat: cl, inTex: ctex, revMat: cl, t: 0.3, tag: 'ms.wall.' + f.k }); continue; }
      const Wf = wallX(C, { a: f.a, b: f.b, out: f.out, outline, holes, mat: cl, tex: ctex, inMat: brick ? cl : 'plaster', inTex: brick ? ctex : null, revMat: brick ? cl : 'trimW', t: 0.3, tag: 'ms.wall.' + f.k }), M = Wf.M;
      for (const [u0, u1, S] of shops) shopfront(C, f, Wf, u0, u1, S, z0, cfg);
      for (const h of wins) { if (h.t === 'D') { for (const [p, q, za, zb] of [[h.u0 - 0.1, h.u0, h.z0, h.z1 + 0.1], [h.u1, h.u1 + 0.1, h.z0, h.z1 + 0.1], [h.u0 - 0.1, h.u1 + 0.1, h.z1, h.z1 + 0.14]]) fbox(C, M, p, q, za, zb, -0.02, 0.06, cfg.dress, { tag: 'door.casing' }); F(C, [M(h.u0, h.z0, -0.12), M(h.u1, h.z0, -0.12), M(h.u1, h.z1, -0.12), M(h.u0, h.z1, -0.12)], o.flatDoor || 'navy', { tag: 'door.flats' }); if (h.dbl) fbox(C, M, (h.u0 + h.u1) / 2 - 0.03, (h.u0 + h.u1) / 2 + 0.03, h.z0, h.z1, -0.12, -0.09, cfg.dress, { tag: 'door.meet' });
          glassQ(C, M, h.u0 + 0.15, h.u1 - 0.15, h.z0 + 1.3, h.z1 - 0.15, -0.11, 'win:' + (C.win++)); fbox(C, M, h.u0 - 0.15, h.u1 + 0.15, -0.15, h.z0, -0.02, 0.3, 'stoneL', { tag: 'door.step' });
          if (h.word && front) { const wwd = Math.max(h.u1 - h.u0 + 0.4, textW(h.word, 1) + 0.3), uc2 = (h.u0 + h.u1) / 2; fbox(C, M, uc2 - wwd / 2, uc2 + wwd / 2, h.z1 + 0.18, h.z1 + 0.58, 0, 0.06, h.wm || 'navy', { tag: 'door.sign' }); letters(C, h.word, f.a[0] + uc2, -0.06, h.z1 + 0.24, 1, 'trimW', { d: 0.02 }); } continue; }
        upWin(C, Wf, h, h.st, cfg); }
      // the face's dress: piers between the bays (brick), string courses at the floors, the cornice, the parapet's plaque and its coping
      if (brick && front) { let uu = 0; for (const by of f.bays.slice(0, -1)) { uu += by.w || 0; fbox(C, M, uu - 0.2, uu + 0.2, z0 + 4.1, H, 0, 0.08, cl, { tag: 'face.pier', tex: ctex }); } }
      if (o.courses !== false && Z.length > 2) for (let k = 2; k < Z.length - 1; k++) fbox(C, M, -0.02, L + 0.02, Z[k] + 0.55, Z[k] + 0.66, 0, 0.05, cfg.dress, { tag: 'face.course' });
      if (pitched) { if (gab && !gam) { const ov = top.ov || 0.35, A = [0, H], Pk = [uc, H + rise], Bp = [L, H], mv = (p, q, s) => [p[0] + (q[0] - p[0]) * s, p[1] + (q[1] - p[1]) * s];
          for (const [p, q] of [[A, Pk], [Pk, Bp]]) { const a = mv(p, q, -ov / Math.hypot(q[0] - p[0], q[1] - p[1]) * 0.6), b = q; stick(C, M(a[0], a[1] - 0.12, 0.08), M(b[0], b[1] - 0.12, 0.08), 0.2, cfg.dress, { tag: 'gable.barge' }); } }
          if (gab && front) { fbox(C, M, -0.08, 0.08, 0, H, 0, 0.05, cfg.dress, { tag: 'face.cornerboard' }); fbox(C, M, L - 0.08, L + 0.08, 0, H, 0, 0.05, cfg.dress, { tag: 'face.cornerboard' }); fbox(C, M, -0.1, L + 0.1, H - 0.28, H, 0, 0.06, cfg.dress, { tag: 'face.frieze' }); } }
      else if (f.k !== 'N' && f.k !== 'W' && (front || top.t === 'flat')) { cornice(C, M, -0.02, L + 0.02, H + 0.6, top.cor || (brick ? 'corbel' : 'bracket'), cfg);
        if (front && top.plaque) { fbox(C, M, uc - 1.25, uc + 1.25, H + 0.7, Ht + rz - 0.14, 0, 0.05, cfg.dress, { tag: 'face.plaque' }); fbox(C, M, uc - 1.1, uc + 1.1, H + 0.8, Ht + rz - 0.24, 0.05, 0.07, cfg.dress, { tag: 'words.' + (o.id || 'block'), b: -1 }); } }
      if (!pitched && (top.t === 'flat' || front)) { fbox(C, M, -0.06, L + 0.06, wt, wt + 0.1, -0.34, 0.06, cfg.dress, { tag: 'face.coping' }); if (front && rz) { const rw = top.rw || Math.min(2.2, L * 0.18); fbox(C, M, uc - rw - 0.06, uc + rw + 0.06, Ht + rz, Ht + rz + 0.1, -0.34, 0.06, cfg.dress, { tag: 'face.coping' }); } } }
    // ---- the roof
    if (gam) { const ov = top.ov || 0.3, n0 = -ov, n1 = d + ov, rm = top.roof || 'roofA', tx = TEX[top.rtex || 'roofSh'], eE = hw + ov, zE = H - ov * (top.p || 1.9) * 0.5, eK = hw - bk;
      roofQ(C, [-eE, n1, zE], [-eE, n0, zE], [-eK, n0, zK], [-eK, n1, zK], rm, { tex: tx }); roofQ(C, [eE, n0, zE], [eE, n1, zE], [eK, n1, zK], [eK, n0, zK], rm, { tex: tx });
      roofQ(C, [-eK, n1, zK], [-eK, n0, zK], [0, n0, zRg], [0, n1, zRg], rm, { tex: tx }); roofQ(C, [eK, n0, zK], [eK, n1, zK], [0, n1, zRg], [0, n0, zRg], rm, { tex: tx });
      stick(C, [0, n0, zRg + 0.04], [0, n1, zRg + 0.04], 0.14, rm, { tag: 'roof.ridge' }); for (const e of [-eE, eE]) stick(C, [e, n0, zE - 0.06], [e, n1, zE - 0.06], 0.15, cfg.dress, { tag: 'roof.fascia' });
      for (const [p, q] of [[[-eE, zE], [-eK, zK]], [[-eK, zK], [0, zRg]], [[0, zRg], [eK, zK]], [[eK, zK], [eE, zE]]]) stick(C, [p[0], n0 + 0.02, p[1] - 0.08], [q[0], n0 + 0.02, q[1] - 0.08], 0.18, cfg.dress, { tag: 'gambrel.barge' }); }
    else if (top.t === 'gable' || top.t === 'false') { const ov = top.ov || 0.35, n0 = top.t === 'false' ? 0.3 : -ov, n1 = d + ov, zE = (top.t === 'false' ? H - 0.1 : H) - 0.12, zR = H + rise - 0.05, rm = top.roof || 'roofA', tx = TEX[top.rtex || 'roofSh'];
      roofQ(C, [-hw - ov, n1, zE], [-hw - ov, n0, zE], [0, n0, zR], [0, n1, zR], rm, { tex: tx }); roofQ(C, [hw + ov, n0, zE], [hw + ov, n1, zE], [0, n1, zR], [0, n0, zR], rm, { tex: tx });
      stick(C, [0, n0, zR + 0.05], [0, n1, zR + 0.05], 0.14, rm, { tag: 'roof.ridge' }); for (const e of [-hw - ov, hw + ov]) stick(C, [e, n0, zE - 0.06], [e, n1, zE - 0.06], 0.16, cfg.dress, { tag: 'roof.fascia' });
      if (top.t === 'false') { for (const e of [-hw, hw]) box(C, e - 0.15, e + 0.15, 0, 0.3, 0, Ht, cl, { tag: 'false.return', tex: ctex }); } }
    else { const p = (cw > 0 ? [[-hw + 0.3, cw - 0.12], [-hw + cw - 0.12, 0.3]] : [[-hw + 0.3, 0.3]]).concat(ch > 0 ? [[hw - ch + 0.12, 0.3], [hw - 0.3, ch - 0.12]] : [[hw - 0.3, 0.3]], [[hw - 0.3, d - 0.3], [-hw + 0.3, d - 0.3]]);
      F(C, p.map(q => [q[0], q[1], H + 0.02]), o.roofMat || 'roofT', { tag: 'roof.membrane', tex: TEX.tarG, uv: p.map(q => [q[0], q[1]]) });
      for (const [a, b] of [[[hw, d], [-hw, d]], [[-hw, d], [-hw, cw]], [[hw, ch], [hw, d]]]) { const L = Math.hypot(b[0] - a[0], b[1] - a[1]), dx = (b[0] - a[0]) / L, dy = (b[1] - a[1]) / L;
        stick(C, [a[0] - dx * 0.06, a[1] - dy * 0.06, Ht + 0.04], [b[0] + dx * 0.06, b[1] + dy * 0.06, Ht + 0.04], 0.24, cfg.dress, { tag: 'roof.coping' }); } }
    if (o.porch) { const Pc = o.porch, pd = Pc.d || 2.2, pe0 = Pc.e0 != null ? Pc.e0 : -hw, pe1 = Pc.e1 != null ? Pc.e1 : hw, pz = Pc.z || 0.45, ph = Pc.h || 2.9, pm = Pc.mat || cfg.dress;
      box(C, pe0, pe1, -pd, 0, -0.15, pz, Pc.base || 'stoneL', { tag: 'porch.base', top: 'deck', topTex: TEX.boardsE }); const nc = Pc.cols || Math.max(2, Math.round((pe1 - pe0) / 2.4) + 1);
      for (let i = 0; i < nc; i++) { const e = pe0 + 0.15 + (pe1 - pe0 - 0.3) * i / (nc - 1); cyl(C, e, -pd + 0.18, 0.11, pz, pz + ph, 8, pm, { tag: 'porch.column' }); box(C, e - 0.15, e + 0.15, -pd + 0.03, -pd + 0.33, pz, pz + 0.12, pm); }
      roofQ(C, [pe0 - 0.2, -pd - 0.3, pz + ph], [pe1 + 0.2, -pd - 0.3, pz + ph], [pe1 + 0.2, 0, pz + ph + 0.7], [pe0 - 0.2, 0, pz + ph + 0.7], Pc.roof || 'roofA', { tex: TEX.roofSh }); stick(C, [pe0 - 0.2, -pd - 0.3, pz + ph - 0.06], [pe1 + 0.2, -pd - 0.3, pz + ph - 0.06], 0.18, pm, { tag: 'porch.fascia' });
      const sw = Pc.steps || 2.0, sc = Pc.sc != null ? Pc.sc : 0; for (let k = 0; k < 3; k++) box(C, sc - sw / 2, sc + sw / 2, -pd - 0.3 * (k + 1), -pd - 0.3 * k, -0.15, pz - (k + 1) * pz / 3 + 0.01, Pc.base || 'stoneL', { tag: 'porch.step' });
      rail(C, [pe0 + 0.15, -pd + 0.18], [sc - sw / 2, -pd + 0.18], pz, { mat: pm, h: 0.85 }); rail(C, [sc + sw / 2, -pd + 0.18], [pe1 - 0.15, -pd + 0.18], pz, { mat: pm, h: 0.85 });
      if (Pc.word) { box(C, pe0 + 0.3, pe1 - 0.3, -pd - 0.36, -pd - 0.28, pz + ph - 0.62, pz + ph - 0.1, Pc.sign || 'navy', { tag: 'porch.sign' }); letters(C, Pc.word, (pe0 + pe1) / 2, -pd - 0.36, pz + ph - 0.55, textW(Pc.word, 2) <= pe1 - pe0 - 0.8 ? 2 : 1, Pc.ink || 'trimW', { d: 0.025 }); }
      lamp(C, sc, -pd / 2, pz + ph - 0.2, '#ffd39a', 0.5, 4); }
    if (o.frieze) { const Fz = o.frieze, zf = Z[1] - 0.75; box(C, -hw + 0.4 + cw, hw - 0.4 - ch, -0.12, 0, zf, zf + 0.62, Fz.mat || 'stoneL', { tag: 'frieze.board' }); letters(C, Fz.word, Fz.e || 0, -0.12, zf + 0.06, textW(Fz.word, 2) <= w - 1.2 ? 2 : 1, Fz.ink || 'navy', { d: 0.03, em: Fz.lit ? 'glow:cream' : undefined }); }
    if (o.cross) { const [ce, cz] = o.cross, cn = -0.9; stick(C, [ce, 0, cz + 0.45], [ce, cn - 0.1, cz + 0.45], 0.05, 'iron'); box(C, ce - 0.08, ce + 0.08, cn - 0.06, cn + 0.06, cz, cz + 0.9, 'awnG', { tag: 'cross', em: 'glow:green' }); box(C, ce - 0.45, ce + 0.45, cn - 0.06, cn + 0.06, cz + 0.32, cz + 0.58, 'awnG', { tag: 'cross', em: 'glow:green' });
      box(C, ce - 0.3, ce + 0.3, cn - 0.08, cn + 0.08, cz + 0.37, cz + 0.53, 'mint', { tag: 'cross', em: 'glow:green' }); box(C, ce - 0.05, ce + 0.05, cn - 0.08, cn + 0.08, cz + 0.12, cz + 0.78, 'mint', { tag: 'cross', em: 'glow:green' }); }
    const R = o.roof || {};
    for (const [e, n, a, b, h] of R.hvac || []) { box(C, e - a / 2, e + a / 2, n - b / 2, n + b / 2, H, H + h, 'steel', { tag: 'roof.unit', s: { mat: 'steel', tex: TEX.grille } }); cyl(C, e, n, Math.min(a, b) * 0.3, H + h, H + h + 0.06, 8, 'dark', { tag: 'roof.fan' }); }
    for (const [e, n] of R.chim || []) { box(C, e - 0.38, e + 0.38, n - 0.28, n + 0.28, H - 0.5, Ht + 1.4, brick ? cl : 'brick', { tag: 'roof.chimney', tex: TEX.brick }); box(C, e - 0.45, e + 0.45, n - 0.35, n + 0.35, Ht + 1.4, Ht + 1.52, cfg.dress, { tag: 'roof.chimneyCap' }); }
    for (const [e, n] of R.hatch || []) box(C, e - 0.5, e + 0.5, n - 0.5, n + 0.5, H, H + 0.55, 'galv', { tag: 'roof.hatch' });
    for (const [e, n] of R.vent || []) cyl(C, e, n, 0.12, H, H + 0.8, 6, 'galv', { tag: 'roof.vent', top: 'dark' });
    for (const [e, n, a, b] of R.sky || []) box(C, e - a / 2, e + a / 2, n - b / 2, n + b / 2, H, H + 0.35, 'frame', { tag: 'roof.skylight', top: 'glassHi' });
    if (R.terrace) { const T = R.terrace; flat(C, T.e0, T.e1, T.n0, T.n1, H + 0.12, 'deck', { tag: 'terrace.deck', tex: TEX.boardsE }); box(C, T.e0, T.e1, T.n0, T.n1, H, H + 0.12, 'deck', { tag: 'terrace.sleepers', noTop: true });
      rail(C, [T.e0, T.n1], [T.e1, T.n1], H + 0.12, { mat: 'iron', h: 1.05 }); rail(C, [T.e0, T.n0], [T.e0, T.n1], H + 0.12, { mat: 'iron', h: 1.05 });
      (T.um || []).forEach(([e, n, m], i) => { rtable(C, e, n, H + 0.12, 0.35, 0.74, 'trimW'); chairD(C, e - 0.55, n, H + 0.12, 'E', 'iron'); chairD(C, e + 0.55, n, H + 0.12, 'W', 'iron'); umbrella(C, e, n, H + 0.12, 1.15, 2.35, m || 'red', i % 2 ? 'trimW' : (m || 'red')); });
      for (let e = T.e0 + 0.2; e < T.e1; e += 1.2) { box(C, e - 0.04, e + 0.04, T.n1 - 0.08, T.n1 - 0.02, H + 2.55, H + 2.62, 'cream', { tag: 'terrace.bulb', em: 'glow:amber' }); lamp(C, e, T.n1 - 0.1, H + 2.5, '#ffcf8a', 0.25, 2.2); }
      stick(C, [T.e0, T.n1 - 0.05, H + 2.62], [T.e1, T.n1 - 0.05, H + 2.62], 0.015, 'dark', { tag: 'terrace.string' }); }
    for (const q of o.walk || []) walkProp(C, q);
    C.dims = { w, d, h: Ht + rise + rz, floor: z0 }; C.door = C.st[0] || null;
  };

  /* ==================================================================================================================================
     THE STREET'S THINGS (in a building's walk, or a piece of their own) ================================================================= */
  function walkProp(C, q) { const e = q.e || 0, n = q.n || 0, z = q.z || 0, k = q.k;
    if (k === 'cafe') { rtable(C, e, n, z, 0.33, 0.74, q.m || 'iron'); for (const [dx, f] of (q.two === false ? [[-0.52, 'E']] : [[-0.52, 'E'], [0.52, 'W']])) chairD(C, e + dx, n, z, f, q.cm || 'iron'); if (q.um) umbrella(C, e, n, z, q.r || 1.05, 2.3, q.um, q.um2 || q.um); }
    else if (k === 'planter') { if (q.sq) planter(C, e, n, z, q.s || 1, q.m || 'iron'); else { cyl(C, e, n, 0.36 * (q.s || 1), z, z + 0.55, 9, q.m || 'terra', { tag: 'planter.pot', top: 'soil' }); cyl(C, e, n, 0.42 * (q.s || 1), z + 0.55, z + 0.95, 9, 'leaf', { tag: 'planter.flowers', top: q.bloom || BLOOM[(Math.round(e * 7) & 7) % 6] }); } }
    else if (k === 'sandwich') { for (const s of [-1, 1]) F(C, s < 0 ? [[e - 0.3, n - 0.2, z], [e + 0.3, n - 0.2, z], [e + 0.3, n, z + 0.95], [e - 0.3, n, z + 0.95]] : [[e + 0.3, n + 0.2, z], [e - 0.3, n + 0.2, z], [e - 0.3, n, z + 0.95], [e + 0.3, n, z + 0.95]], 'slateK', { tag: 'sandwich.board', tex: TEX.slats2 });
      stick(C, [e - 0.3, n - 0.2, z], [e - 0.3, n, z + 0.95], 0.04, 'wood'); stick(C, [e + 0.3, n - 0.2, z], [e + 0.3, n, z + 0.95], 0.04, 'wood'); }
    else if (k === 'buckets') { for (let t = 0; t < 3; t++) box(C, e - 0.8, e + 0.8, n - 0.3 + t * 0.3, n + 0.3, z + t * 0.28, z + (t + 1) * 0.28, 'wood', { tag: 'stand.step' });
      for (let t = 0; t < 3; t++) for (let i = 0; i < 4; i++) { const be = e - 0.6 + i * 0.4, bn = n - 0.15 + t * 0.3, bz = z + (t + 1) * 0.28; cyl(C, be, bn, 0.12, bz, bz + 0.25, 7, 'galv', { top: 'leaf' }); cyl(C, be, bn, 0.15, bz + 0.25, bz + 0.45, 7, 'leaf', { tag: 'stand.bunch', top: BLOOM[(i + t * 2) % 6] }); } }
    else if (k === 'bench') bench(C, e, n, z, q.f || 'S', q.m || 'wood');
    else if (k === 'hoops') { for (let i = 0; i < (q.nh || 3); i++) { const he = e - (q.nh || 3) * 0.45 + 0.45 + i * 0.9; stick(C, [he, n - 0.3, z], [he, n - 0.3, z + 0.82], 0.05, q.m || 'iron'); stick(C, [he, n + 0.3, z], [he, n + 0.3, z + 0.82], 0.05, q.m || 'iron'); stick(C, [he, n - 0.3, z + 0.82], [he, n + 0.3, z + 0.82], 0.05, q.m || 'iron'); } }
    else if (k === 'bikes') { const nb = q.nb || 3; for (let i = 0; i < nb; i++) bike(C, e, n - (nb - 1) * 0.3 + i * 0.6, z, i % 2 ? 'W' : 'E', (q.ms || ['teal', 'red', 'yel', 'navy'])[i % (q.ms || [1, 2, 3, 4]).length], !!q.basket);
      stick(C, [e - 0.9, n - (nb - 1) * 0.3 - 0.4, z + 0.6], [e - 0.9, n + (nb - 1) * 0.3 + 0.4, z + 0.6], 0.05, 'iron'); }
    else if (k === 'kayaks') { for (const dn of [-0.6, 0.6]) { stick(C, [e - (q.len || 3.6) * 0.3, n + dn, z], [e - (q.len || 3.6) * 0.3, n + dn, z + 1.5], 0.08, 'wood'); stick(C, [e + (q.len || 3.6) * 0.3, n + dn, z], [e + (q.len || 3.6) * 0.3, n + dn, z + 1.5], 0.08, 'wood'); }
      for (const t of [0.5, 1.0, 1.5]) stick(C, [e - (q.len || 3.6) * 0.3, n - 0.6, z + t - 0.05], [e - (q.len || 3.6) * 0.3, n + 0.6, z + t - 0.05], 0.06, 'wood');
      (q.ms || ['red', 'yel', 'teal']).forEach((m, i) => { const L = q.len || 3.6, kz = z + 0.5 + i * 0.5; box(C, e - L / 2 + 0.3, e + L / 2 - 0.3, n - 0.3, n + 0.3, kz, kz + 0.3, m, { tag: 'kayak.hull', top: m });
        F(C, [[e - L / 2, n, kz + 0.12], [e - L / 2 + 0.3, n - 0.3, kz], [e - L / 2 + 0.3, n - 0.3, kz + 0.3]], m); F(C, [[e + L / 2 - 0.3, n - 0.3, kz], [e + L / 2, n, kz + 0.12], [e + L / 2 - 0.3, n - 0.3, kz + 0.3]], m);
        box(C, e - 0.35, e + 0.35, n - 0.2, n + 0.2, kz + 0.3, kz + 0.33, 'dark', { tag: 'kayak.cockpit' }); }); }
    else if (k === 'cone') { stick(C, [e, n, z], [e, n, z + 1.0], 0.06, 'iron'); const zc = z + 1.0; for (let i = 0; i < 8; i++) { const a = i / 8 * 2 * Math.PI, b = (i + 1) / 8 * 2 * Math.PI; F(C, [[e, n, zc], [e + Math.cos(b) * 0.24, n + Math.sin(b) * 0.24, zc + 0.6], [e + Math.cos(a) * 0.24, n + Math.sin(a) * 0.24, zc + 0.6]], 'cord', { tag: 'cone.wafer' }); }
      cyl(C, e, n, 0.27, zc + 0.6, zc + 0.85, 8, 'pink', { top: 'pink' }); cyl(C, e, n, 0.2, zc + 0.85, zc + 1.02, 8, 'cream', { top: 'cream' }); }
    else if (k === 'bin') { for (const [dx, m, lid] of [[-0.3, q.m1 || 'awnG', 'awnG'], [0.3, q.m2 || 'blue', 'blue']]) { box(C, e + dx - 0.25, e + dx + 0.25, n - 0.25, n + 0.25, z, z + 0.95, m, { tag: 'bin.body', s: { mat: m, tex: TEX.slats } }); box(C, e + dx - 0.27, e + dx + 0.27, n - 0.27, n + 0.27, z + 0.95, z + 1.02, lid, { tag: 'bin.lid', top: 'dark' }); } }
    else if (k === 'drink') { cyl(C, e, n, 0.14, z, z + 0.85, 8, 'steel', { tag: 'fountain.post' }); cyl(C, e, n, 0.24, z + 0.85, z + 0.95, 8, 'steel', { tag: 'fountain.bowl', top: 'glassHi' }); stick(C, [e, n - 0.05, z + 0.95], [e, n - 0.08, z + 1.03], 0.025, 'brass'); cyl(C, e + 0.3, n, 0.1, z, z + 0.45, 6, 'steel', { tag: 'fountain.dog', top: 'water' }); }
    else if (k === 'mailbox') { for (const dx of [-0.2, 0.2]) box(C, e + dx - 0.03, e + dx + 0.03, n - 0.15, n + 0.15, z, z + 0.4, 'red'); box(C, e - 0.3, e + 0.3, n - 0.24, n + 0.24, z + 0.4, z + 1.25, 'red', { tag: 'mailbox.body' }); cyl(C, e, n, 0.3, z + 1.25, z + 1.4, 8, 'red', { tag: 'mailbox.top', top: 'red' });
      box(C, e - 0.18, e + 0.18, n - 0.26, n - 0.22, z + 1.0, z + 1.08, 'dark', { tag: 'mailbox.slot' }); }
    else if (k === 'bollards') { for (let i = 0; i < (q.nb || 3); i++) { const be = e - ((q.nb || 3) - 1) * (q.gap || 1.4) / 2 + i * (q.gap || 1.4); cyl(C, be, n, 0.1, z, z + 0.85, 8, 'iron', { tag: 'bollard', top: 'iron' }); cyl(C, be, n, 0.12, z + 0.6, z + 0.66, 8, 'gilt'); } }
    else if (k === 'grate') { flat(C, e - 0.75, e + 0.75, n - 0.75, n + 0.75, z + 0.13, 'iron', { tag: 'tree.grate', tex: TEX.grate }); for (const [a, b] of [[-0.35, -0.35], [0.35, -0.35], [0.35, 0.35], [-0.35, 0.35]]) stick(C, [e + a, n + b, z + 0.13], [e + a * 0.9, n + b * 0.9, z + 1.7], 0.03, 'iron', { tag: 'tree.guard' });
      for (const zz of [0.6, 1.6]) { stick(C, [e - 0.33, n - 0.33, z + zz], [e + 0.33, n - 0.33, z + zz], 0.03, 'iron'); stick(C, [e - 0.33, n + 0.33, z + zz], [e + 0.33, n + 0.33, z + zz], 0.03, 'iron'); } }
    else if (k === 'lights') { const a = q.a, b = q.b, nn = Math.max(2, Math.round(Math.hypot(b[0] - a[0], b[1] - a[1]) / 0.9)); stick(C, [a[0], a[1], a[2]], [b[0], b[1], b[2]], 0.015, 'dark', { tag: 'string' });
      for (let i = 0; i <= nn; i++) { const t = i / nn, p = [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t - 0.25 * Math.sin(Math.PI * t)]; box(C, p[0] - 0.04, p[0] + 0.04, p[1] - 0.04, p[1] + 0.04, p[2] - 0.08, p[2], 'cream', { tag: 'string.bulb', em: 'glow:amber' }); if (i % 2 === 0) lamp(C, p[0], p[1], p[2] - 0.1, '#ffcf8a', 0.22, 2.0); } }
  }

  B.msProp = function (C) { const o = C.s.o; walkProp(C, Object.assign({}, o, { e: 0, n: 0, z: 0 })); C.dims = { w: 2, d: 1, h: 1.6, floor: 0 }; };

  // a fenced terrace (the restaurant's on the square): pavers, a low iron fence with planters, tables under umbrellas, string lights
  B.msPatio = function (C) { const o = C.s.o, e0 = -o.w / 2, e1 = o.w / 2, n0 = -o.d / 2, n1 = o.d / 2;
    flat(C, e0, e1, n0, n1, 0.04, o.floor || 'stoneL', { tag: 'patio.floor', tex: TEX.walk });
    const gaps = o.gap ? [o.gap] : []; rail(C, [e0, n0], [e1, n0], 0.04, { mat: 'iron', h: 0.9, gaps }); rail(C, [e0, n0], [e0, n1], 0.04, { mat: 'iron', h: 0.9 }); rail(C, [e1, n0], [e1, n1], 0.04, { mat: 'iron', h: 0.9 });
    for (let e = e0 + 0.5; e < e1; e += 2.2) walkProp(C, { k: 'planter', e, n: n0 - 0.35, s: 0.7 });
    (o.tables || []).forEach(([e, n, um], i) => walkProp(C, { k: 'cafe', e, n, z: 0.04, um: um || null, um2: i % 2 ? 'trimW' : null }));
    if (o.lights) walkProp(C, { k: 'lights', a: [e0, n1, 2.7], b: [e1, n1, 2.7] }), walkProp(C, { k: 'lights', a: [e0, n0, 2.5], b: [e0, n1, 2.7] });
    C.dims = { w: o.w, d: o.d, h: 2.8, floor: 0 }; };

  /* ==================================================================================================================================
     THE SQUARE ========================================================================================================================== */
  // the bandstand: an octagon on a stone plinth, eight posts, a balustrade open to its steps, a bell-cast roof with a finial; bunting
  B.msBandstand = function (C) { const o = C.s.o, R = o.r || 2.7, zd = o.deck || 0.8, H = zd + 2.6, ang = (k) => (-112.5 + 45 * k) * Math.PI / 180, V = (k, r) => [Math.cos(ang(k)) * r, Math.sin(ang(k)) * r];
    const ring = (r, z) => [0, 1, 2, 3, 4, 5, 6, 7].map(k => [...V(k, r), z]);
    for (let k = 0; k < 8; k++) { const a = V(k, R + 0.15), b = V(k + 1, R + 0.15); F(C, [[a[0], a[1], -0.2], [b[0], b[1], -0.2], [b[0], b[1], zd], [a[0], a[1], zd]], 'stoneW', { tag: 'band.plinth', tex: TEX.fieldstone, uv: [[0, -0.2], [2.2, -0.2], [2.2, zd], [0, zd]] }); }
    F(C, ring(R + 0.15, zd), 'deck', { tag: 'band.deck', tex: TEX.boardsE, uv: ring(R + 0.15, zd).map(p => [p[0], p[1]]) });
    for (let k = 0; k < 3; k++) box(C, -1.0, 1.0, -R - 0.15 - (k + 1) * 0.3, -R - 0.15 - k * 0.3, -0.2, zd - k * (zd / 3), 'stoneW', { tag: 'band.step', tex: TEX.fieldstone });
    for (let k = 0; k < 8; k++) { const p = V(k, R); box(C, p[0] - 0.09, p[0] + 0.09, p[1] - 0.09, p[1] + 0.09, zd, H, 'trimW', { tag: 'band.post' }); }
    for (let k = 0; k < 8; k++) { if (k === 0) continue; const a = V(k, R), b = V(k + 1, R); rail(C, a, b, zd, { mat: 'trimW', h: 0.85 }); }
    for (let k = 0; k < 8; k++) { const a = V(k, R), b = V(k + 1, R); stick(C, [a[0], a[1], H - 0.25], [b[0], b[1], H - 0.25], 0.18, 'trimW', { tag: 'band.frieze' });
      for (let i = 1; i < 6; i++) { const t = i / 6, p = [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]; F(C, [[p[0] + 0.08, p[1], H - 0.42], [p[0] - 0.08, p[1], H - 0.42], [p[0], p[1], H - 0.64]].map(q => [q[0], q[1] - (Math.sin(ang(k) + Math.PI / 8) > 0 ? -0.01 : 0.01), q[2]]), i % 2 ? 'red' : 'navy', { tag: 'band.bunting' }); } }
    const RE = R + 0.55, rk = o.roof || 'verd'; for (let k = 0; k < 8; k++) { const a = V(k, RE), b = V(k + 1, RE), c = V(k + 1, R * 0.55), dd = V(k, R * 0.55);
      roofQ(C, [a[0], a[1], H - 0.05], [b[0], b[1], H - 0.05], [c[0], c[1], H + 0.75], [dd[0], dd[1], H + 0.75], rk, { tex: TEX.roofSh }); F(C, [[dd[0], dd[1], H + 0.75], [c[0], c[1], H + 0.75], [0, 0, H + 2.0]], rk, { tag: 'roof', tex: TEX.roofSh, uv: [[0, 0], [1.3, 0], [0.65, 1.4]] });
      stick(C, [a[0], a[1], H - 0.1], [b[0], b[1], H - 0.1], 0.14, 'trimW', { tag: 'roof.fascia' }); }
    cyl(C, 0, 0, 0.12, H + 1.9, H + 2.35, 6, 'gilt', { tag: 'band.finial', top: 'gilt' });
    if (o.lights !== false) for (let k = 0; k < 8; k++) { const p = V(k, R - 0.1); box(C, p[0] - 0.05, p[0] + 0.05, p[1] - 0.05, p[1] + 0.05, H - 0.5, H - 0.4, 'cream', { tag: 'band.bulb', em: 'glow:amber' }); if (k % 2 === 0) lamp(C, p[0], p[1], H - 0.55, '#ffcf8a', 0.35, 3.5); }
    C.dims = { w: 2 * RE, d: 2 * RE, h: H + 2.4, floor: zd }; C.st.push({ id: 'bandstand', verb: 'climb', at: [0, -R - 1.0, 0], stand: [0, -R - 1.3] }); };

  // the fountain: a round stone basin, its water, a pedestal and two bowls, the jets
  B.msFountain = function (C) { const o = C.s.o, R = o.r || 1.8;
    cyl(C, 0, 0, R, -0.1, 0.5, 16, 'stoneL', { tag: 'fountain.basin', top: 'stoneL' }); cyl(C, 0, 0, R - 0.22, 0.38, 0.52, 16, 'water', { tag: 'fountain.water', top: 'water' });
    cyl(C, 0, 0, 0.28, 0.5, 1.15, 8, 'stoneL', { tag: 'fountain.pedestal' }); cyl(C, 0, 0, 0.85, 1.15, 1.28, 12, 'stoneL', { tag: 'fountain.bowl', top: 'water' }); cyl(C, 0, 0, 0.14, 1.28, 1.8, 6, 'stoneL'); cyl(C, 0, 0, 0.42, 1.8, 1.9, 10, 'stoneL', { top: 'water' });
    for (let k = 0; k < 10; k++) { const a = k / 10 * 2 * Math.PI; stick(C, [Math.cos(a) * 0.85, Math.sin(a) * 0.85, 1.25], [Math.cos(a) * 1.25, Math.sin(a) * 1.25, 0.5], 0.03, 'glassHi', { tag: 'fountain.sheet' }); }
    stick(C, [0, 0, 1.9], [0, 0, 2.55], 0.05, 'glassHi', { tag: 'fountain.jet' }); for (let k = 0; k < 6; k++) { const a = k / 6 * 2 * Math.PI; stick(C, [0, 0, 2.5], [Math.cos(a) * 0.32, Math.sin(a) * 0.32, 1.95], 0.025, 'glassHi', { tag: 'fountain.spray' }); }
    lamp(C, 0, -R + 0.5, 0.5, '#e8f4ff', 0.4, 2.5); C.dims = { w: 2 * R, d: 2 * R, h: 2.6, floor: 0 }; };

  // the tot lot: a timber play deck under a little roof, its slide, a climbing wall, a two-swing frame; wood chips in a timber border
  B.msPlay = function (C) { const o = C.s.o, w = o.w || 7, d = o.d || 5.2, e0 = -w / 2, e1 = w / 2, n0 = -d / 2, n1 = d / 2;
    flat(C, e0, e1, n0, n1, 0.05, 'mulchR', { tag: 'play.chips', tex: TEX.chips }); for (const [a, b, c, dd] of [[e0, e1, n0 - 0.15, n0], [e0, e1, n1, n1 + 0.15], [e0 - 0.15, e0, n0, n1], [e1, e1 + 0.15, n0, n1]]) box(C, a, b, c, dd, -0.1, 0.15, 'wood', { tag: 'play.border' });
    const dx = e0 + 1.6, dn = 0.3, zd = 1.2; for (const [a, b] of [[-0.7, -0.7], [0.7, -0.7], [-0.7, 0.7], [0.7, 0.7]]) box(C, dx + a - 0.06, dx + a + 0.06, dn + b - 0.06, dn + b + 0.06, 0, 2.8, 'wood', { tag: 'play.post' });
    box(C, dx - 0.75, dx + 0.75, dn - 0.75, dn + 0.75, zd - 0.08, zd, 'wood', { tag: 'play.deck', top: 'deck' }); rail(C, [dx - 0.7, dn + 0.7], [dx + 0.7, dn + 0.7], zd, { mat: 'yel', h: 0.75 }); rail(C, [dx - 0.7, dn - 0.7], [dx - 0.7, dn + 0.7], zd, { mat: 'yel', h: 0.75 });
    roofQ(C, [dx - 0.95, dn + 0.95, 2.75], [dx - 0.95, dn - 0.95, 2.75], [dx, dn - 0.95, 3.45], [dx, dn + 0.95, 3.45], 'red', { tex: TEX.roofSh }); roofQ(C, [dx + 0.95, dn - 0.95, 2.75], [dx + 0.95, dn + 0.95, 2.75], [dx, dn + 0.95, 3.45], [dx, dn - 0.95, 3.45], 'red', { tex: TEX.roofSh });
    F(C, [[dx - 0.45, dn - 0.75, zd], [dx + 0.45, dn - 0.75, zd], [dx + 0.45, dn - 2.6, 0.25], [dx - 0.45, dn - 2.6, 0.25]].reverse(), 'yel', { tag: 'play.slide' }); for (const s of [-1, 1]) stick(C, [dx + s * 0.47, dn - 0.75, zd + 0.15], [dx + s * 0.47, dn - 2.6, 0.4], 0.05, 'yel', { tag: 'play.slideRail' });
    F(C, [[dx + 1.9, dn - 0.6, 0.05], [dx + 1.9, dn + 0.6, 0.05], [dx + 0.75, dn + 0.6, zd], [dx + 0.75, dn - 0.6, zd]], 'blue', { tag: 'play.wall', tex: TEX.produce, uv: [[0, 0], [1.2, 0], [1.2, 1.6], [0, 1.6]] });
    const sx = e1 - 1.9; for (const s of [-1, 1]) { stick(C, [sx + s * 1.3, n0 + 0.5, 0], [sx + s * 1.3, dn, 2.3], 0.08, 'navy'); stick(C, [sx + s * 1.3, n1 - 0.4, 0], [sx + s * 1.3, dn, 2.3], 0.08, 'navy'); }
    stick(C, [sx - 1.35, dn, 2.3], [sx + 1.35, dn, 2.3], 0.1, 'navy', { tag: 'play.beam' }); for (const s of [-0.6, 0.6]) { for (const t of [-0.22, 0.22]) stick(C, [sx + s + t, dn, 2.28], [sx + s + t, dn, 0.5], 0.015, 'galv'); box(C, sx + s - 0.26, sx + s + 0.26, dn - 0.1, dn + 0.1, 0.45, 0.5, 'red', { tag: 'play.seat' }); }
    C.dims = { w, d, h: 3.5, floor: 0 }; };

  // the town's sign: two fieldstone piers, a painted board with the town's name in gilt, a bed of flowers along its foot
  B.msTownSign = function (C) { const o = C.s.o, w = o.w || 3.6, word = o.word || 'NINE MILE CREEK', bm = o.board || 'navy';
    for (const e of [-w / 2 - 0.25, w / 2 + 0.25]) { box(C, e - 0.3, e + 0.3, -0.3, 0.3, -0.1, 1.55, 'stoneW', { tag: 'sign.pier', tex: TEX.fieldstone }); box(C, e - 0.36, e + 0.36, -0.36, 0.36, 1.55, 1.65, 'stoneL', { tag: 'sign.cap' }); }
    box(C, -w / 2, w / 2, -0.07, 0.07, 0.55, 1.4, bm, { tag: 'sign.board' }); fbox(C, (u, z, off) => [-w / 2 + u, -0.07 - off, z], -0.03, w + 0.03, 1.36, 1.46, 0, 0.04, 'gilt', { tag: 'sign.trim' });
    const sc = textW(word, 2) <= w - 0.3 ? 2 : 1; letters(C, word, 0, -0.07, sc === 2 ? 0.67 : 0.86, sc, 'gilt', { d: 0.025 });
    box(C, -w / 2 - 0.5, w / 2 + 0.5, -1.0, -0.35, -0.05, 0.22, 'stoneL', { tag: 'sign.bed', top: 'soil' }); for (let e = -w / 2 - 0.3, i = 0; e <= w / 2 + 0.31; e += 0.32, i++) cyl(C, e, -0.68, 0.17, 0.22, 0.48, 7, 'leaf', { tag: 'sign.flowers', top: BLOOM[i % 6] });
    C.dims = { w: w + 1, d: 1.4, h: 1.7, floor: 0 }; };

  // the map and notice board: two posts, a map and a cork board under a little gable roof, its header lettered
  B.msKiosk = function (C) { const o = C.s.o, w = o.w || 2.6;
    for (const e of [-w / 2, w / 2]) box(C, e - 0.07, e + 0.07, -0.07, 0.07, 0, 2.6, 'wood', { tag: 'kiosk.post' });
    box(C, -w / 2, 0, -0.04, 0.04, 0.9, 2.1, 'prodG', { tag: 'kiosk.map', s: { mat: 'prodG', tex: TEX.mapT } }); box(C, 0, w / 2, -0.04, 0.04, 0.9, 2.1, 'cord', { tag: 'kiosk.cork', s: { mat: 'cord', tex: TEX.corkN } });
    box(C, -w / 2 - 0.05, w / 2 + 0.05, -0.06, 0.06, 2.1, 2.42, 'navy', { tag: 'kiosk.header' }); letters(C, o.word || 'MAP & NOTICES', 0, -0.06, 2.16, 1, 'trimW', { d: 0.02 });
    roofQ(C, [w / 2 + 0.3, 0.5, 2.6], [-w / 2 - 0.3, 0.5, 2.6], [-w / 2 - 0.3, 0, 2.95], [w / 2 + 0.3, 0, 2.95], 'roofA', { tex: TEX.roofSh }); roofQ(C, [-w / 2 - 0.3, -0.5, 2.6], [w / 2 + 0.3, -0.5, 2.6], [w / 2 + 0.3, 0, 2.95], [-w / 2 - 0.3, 0, 2.95], 'roofA', { tex: TEX.roofSh });
    C.dims = { w, d: 1, h: 3, floor: 0 }; };

  // the bus shelter: a green frame, glass at its back and ends, its bench, a flat roof; the stop's sign on its post
  B.msShelter = function (C) { const o = C.s.o, w = o.w || 3.2, d = o.d || 1.5, fr = o.frame || 'awnG';
    for (const [e, n] of [[-w / 2, 0], [w / 2, 0], [-w / 2, d], [w / 2, d]]) box(C, e - 0.05, e + 0.05, n - 0.05, n + 0.05, 0, 2.35, fr, { tag: 'shelter.post' });
    F(C, [[-w / 2, d, 0.1], [w / 2, d, 0.1], [w / 2, d, 2.2], [-w / 2, d, 2.2]], 'glass', { tag: 'shelter.glass' }); for (const e of [-w / 2, w / 2]) F(C, e < 0 ? [[e, d, 0.1], [e, 0.3, 0.1], [e, 0.3, 2.2], [e, d, 2.2]] : [[e, 0.3, 0.1], [e, d, 0.1], [e, d, 2.2], [e, 0.3, 2.2]], 'glass', { tag: 'shelter.glass' });
    box(C, -w / 2 - 0.12, w / 2 + 0.12, -0.15, d + 0.12, 2.35, 2.5, fr, { tag: 'shelter.roof', top: 'galv' }); box(C, -w / 2 + 0.3, w / 2 - 0.3, d - 0.45, d - 0.1, 0.42, 0.47, 'wood', { tag: 'shelter.bench', tex: TEX.slats });
    stick(C, [w / 2 + 0.6, 0.2, 0], [w / 2 + 0.6, 0.2, 2.8], 0.06, 'galv', { tag: 'stop.post' }); box(C, w / 2 + 0.3, w / 2 + 0.9, 0.17, 0.23, 2.2, 2.75, 'blue', { tag: 'stop.sign' }); letters(C, 'BUS', w / 2 + 0.6, 0.17, 2.36, 1, 'trimW', { d: 0.02 });
    lamp(C, 0, d / 2, 2.25, '#f4f1e2', 0.4, 3); box(C, -0.6, 0.6, d / 2 - 0.05, d / 2 + 0.05, 2.28, 2.33, 'white', { tag: 'shelter.light', em: 'glow:cool' });
    C.dims = { w: w + 1.2, d, h: 2.9, floor: 0 }; };

  // the EV chargers: two pedestals on a kerbed island, their screens and cables, a sign
  B.msCharger = function (C) { const o = C.s.o, n = o.n || 2, gap = o.gap || 2.75;
    box(C, -n * gap / 2, n * gap / 2, -0.4, 0.4, 0, 0.15, 'conc', { tag: 'ev.island' });
    for (let i = 0; i < n; i++) { const e = -n * gap / 2 + gap * (i + 0.5); box(C, e - 0.22, e + 0.22, -0.15, 0.15, 0.15, 1.65, 'white', { tag: 'ev.post', s: { mat: 'white' } }); box(C, e - 0.22, e + 0.22, -0.16, 0.16, 1.65, 1.75, 'awnG', { tag: 'ev.cap' });
      box(C, e - 0.14, e + 0.14, -0.17, -0.15, 1.2, 1.45, 'dark', { tag: 'ev.screen', em: 'glow:green' }); for (let t = 0; t < 4; t++) stick(C, [e + 0.2, -0.16, 1.0 - t * 0.2], [e + 0.28, -0.2, 0.85 - t * 0.2], 0.035, 'dark', { tag: 'ev.cable' });
      cyl(C, e, -0.6, 0.08, 0, 0.9, 6, 'yel', { tag: 'ev.bollard', top: 'yel' }); }
    stick(C, [n * gap / 2 + 0.3, 0, 0], [n * gap / 2 + 0.3, 0, 2.3], 0.05, 'galv'); box(C, n * gap / 2 + 0.05, n * gap / 2 + 0.55, -0.03, 0.03, 1.8, 2.25, 'awnG', { tag: 'ev.sign' }); letters(C, 'EV', n * gap / 2 + 0.3, -0.03, 1.94, 1, 'trimW', { d: 0.02 });
    C.dims = { w: n * gap + 1, d: 1, h: 2.3, floor: 0 }; };

  // a parked car: nose to the south (dir 4), its body, glasshouse, wheels and lamps. body hatch | suv | sedan
  B.msCar = function (C) { const o = C.s.o, bd = o.body || 'hatch', p = o.paint || 'carS', L = bd === 'suv' ? 4.6 : bd === 'sedan' ? 4.75 : 4.15, W = bd === 'suv' ? 1.86 : 1.78, hw = W / 2, hl = L / 2;
    const zb = bd === 'suv' ? 0.42 : 0.32, zw = bd === 'suv' ? 1.05 : 0.9, zr = bd === 'suv' ? 1.72 : bd === 'sedan' ? 1.42 : 1.5, nf = bd === 'sedan' ? -hl + 1.25 : -hl + 0.95, nwf = nf + (bd === 'suv' ? 0.55 : 0.75), nr = bd === 'sedan' ? hl - 1.15 : hl - 0.32, nrb = bd === 'sedan' ? hl - 0.55 : hl - 0.1;
    box(C, -hw, hw, -hl, hl, zb, zw, p, { tag: 'car.body', top: p }); box(C, -hw - 0.02, hw + 0.02, -hl - 0.04, -hl + 0.12, zb - 0.06, zb + 0.22, 'carK', { tag: 'car.bumper' }); box(C, -hw - 0.02, hw + 0.02, hl - 0.12, hl + 0.04, zb - 0.06, zb + 0.22, 'carK', { tag: 'car.bumper' });
    const ch = hw - 0.1; F(C, [[-ch, nf, zw], [ch, nf, zw], [ch, nwf, zr], [-ch, nwf, zr]], 'glass', { tag: 'car.windshield' }); flat(C, -ch, ch, nwf, nr, zr, p, { tag: 'car.roof' });
    F(C, [[ch, nrb, zw], [-ch, nrb, zw], [-ch, nr, zr], [ch, nr, zr]], 'glass', { tag: 'car.rear' });
    for (const s of [-1, 1]) F(C, s < 0 ? [[-ch, nrb, zw], [-ch, nf, zw], [-ch, nwf, zr], [-ch, nr, zr]] : [[ch, nf, zw], [ch, nrb, zw], [ch, nr, zr], [ch, nwf, zr]], 'glass', { tag: 'car.side' });
    for (const s of [-1, 1]) stick(C, [s * ch, (nwf + nr) / 2, zr], [s * ch, (nwf + nr) / 2, zw], 0.07, p, { tag: 'car.pillar' });
    for (const [e, n] of [[-hw + 0.08, -hl + 0.78], [hw - 0.08, -hl + 0.78], [-hw + 0.08, hl - 0.75], [hw - 0.08, hl - 0.75]]) hcyl(C, e < 0 ? e - 0.14 : e - 0.06, e < 0 ? e + 0.06 : e + 0.14, n, 0.33, 0.33, 10, 'rubber', { tag: 'car.wheel', cap: 'steel' });
    for (const s of [-1, 1]) { box(C, s * (hw - 0.35) - 0.2, s * (hw - 0.35) + 0.2, -hl - 0.02, -hl + 0.02, zb + 0.3, zb + 0.42, 'white', { tag: 'car.headlamp' }); box(C, s * (hw - 0.3) - 0.18, s * (hw - 0.3) + 0.18, hl - 0.02, hl + 0.02, zb + 0.32, zb + 0.46, 'red', { tag: 'car.taillamp' });
      box(C, s * (hw + 0.1) - 0.06, s * (hw + 0.1) + 0.06, nf + 0.1, nf + 0.22, zw - 0.02, zw + 0.12, p, { tag: 'car.mirror' }); }
    if (o.rack) { for (const n of [nwf + 0.3, nr - 0.3]) stick(C, [-ch, n, zr + 0.08], [ch, n, zr + 0.08], 0.04, 'dark'); if (o.rack === 'kayak') box(C, -0.3, 0.3, -hl + 0.2, hl - 0.2, zr + 0.12, zr + 0.36, 'yel', { tag: 'car.kayak', top: 'yel' }); }
    C.dims = { w: W, d: L, h: zr + 0.1, floor: 0 }; };

  Object.assign(K, {
    msBlock: { label: 'A main-street building', foot: [-12, 12, -4, 16] }, msProp: { label: 'A street thing', foot: [-1.5, 1.5, -1, 1] }, msPatio: { label: 'A fenced terrace', foot: [-5, 5, -4, 4] },
    msBandstand: { label: 'The bandstand', foot: [-3.4, 3.4, -4.4, 3.4] }, msFountain: { label: 'The fountain', foot: [-1.9, 1.9, -1.9, 1.9] }, msPlay: { label: 'The tot lot', foot: [-3.6, 3.6, -2.8, 2.8] },
    msTownSign: { label: 'The town sign', foot: [-2.6, 2.6, -1.1, 0.5] }, msKiosk: { label: 'The map and notice board', foot: [-1.6, 1.6, -0.6, 0.6] }, msShelter: { label: 'The bus shelter', foot: [-2, 2.6, -0.3, 1.7] },
    msCharger: { label: 'EV chargers', foot: [-3, 3.2, -0.8, 0.5] }, msCar: { label: 'A parked car', foot: [-1, 1, -2.5, 2.5] },
  });
  root.NmcMainStreet = { revision: '1.0 (main street redrawn, 10-05)', kinds: ['msBlock', 'msProp', 'msPatio', 'msBandstand', 'msFountain', 'msPlay', 'msTownSign', 'msKiosk', 'msShelter', 'msCharger', 'msCar'],
    lib: { upWin, shopfront, awning, disp, cornice, walkProp, arcBand, arcGlass } };
})(typeof globalThis !== 'undefined' ? globalThis : this);
