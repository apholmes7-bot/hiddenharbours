/* tools/check-shading.cjs — THE SHADING RULE, as a reference implementation, proven equal to the rig, and what each part of
   it does to the picture.
     node tools/check-shading.cjs          writes reports/shading.{json,txt}; compares data/shading.v9.json with the regenerated spec
     node tools/check-shading.cjs --write  also rewrites data/shading.v9.json
   refPaint() below is the whole facet pass written out long-hand for porting (every switch on = the rig). It is run against
   CharacterIso9.paintSolved() on 800 renders (10 presets x 10 poses x 8 facings) and must match to the pixel. Then one part
   at a time is switched off (or swapped for a plausible engine default) on 240 renders (10 presets x idle, walk, dig x 8
   facings) and the pixels that change are counted. A part is optional for the look only if switching it off changes nothing. */
'use strict';
(function () {
function run(C, H) {
  const OUT = 'data/shading.v9.json', problems = [], SH = C.SHADING, DEG = Math.PI / 180;
  const rigSha = H.sha256('Art/characterIsoRig9.js'), poseSha = H.sha256('Art/characterIsoRig9.poses.js');
  const hex2 = (c) => [parseInt(c.slice(1, 3), 16), parseInt(c.slice(3, 5), 16), parseInt(c.slice(5, 7), 16)];
  const mixHex = (a, b, t) => { const A = hex2(a), B = hex2(b); return '#' + [0, 1, 2].map((i) => Math.round(A[i] + (B[i] - A[i]) * t).toString(16).padStart(2, '0')).join(''); };
  const bankers = (x) => { const f = Math.floor(x), r = x - f; return r > 0.5 ? f + 1 : r < 0.5 ? f : (f % 2 === 0 ? f : f + 1); };
  const vnorm = (a) => { const m = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / m, a[1] / m, a[2] / m]; };
  /* ---------------- the reference facet pass ---------------- */
  const ON = { form:true, loHi:true, band:true, off:true, roundS:true, roundD:true, db:true, minT:true, edges:true, keyline:true, key:null, round:Math.round, f32:true };
  function refPaint(faces, cam, MATS, snap, sw) { sw = Object.assign({}, ON, sw || {});
    const W = cam.W, Hh = cam.H, N = W * Hh, Buf = sw.f32 ? Float32Array : Float64Array, zb = new Buf(N).fill(Infinity), dep = new Buf(N), mat = new Int16Array(N).fill(-1), stp = new Int8Array(N);
    const keys = Object.keys(MATS), mIx = {}; keys.forEach((k, i) => { mIx[k] = i; }); const K = sw.key || SH.key, R = sw.round;
    for (let fi = 0; fi < faces.length; fi++) { const f = faces[fi], P = f.v.map((p) => C.proj(p, cam));
      if (snap && f.head) for (const q of P) { q.sx += snap[0]; q.sy += snap[1]; }
      /* the face normal, Newell's method on the camera-space corners: x right, y away from the camera along the ground, z up */
      let nx = 0, ny = 0, nz = 0; for (let i = 0; i < P.length; i++) { const a = P[i], c = P[(i + 1) % P.length]; nx += (a.yr - c.yr) * (a.zr + c.zr); ny += (a.zr - c.zr) * (a.xr + c.xr); nz += (a.xr - c.xr) * (a.yr + c.yr); }
      const nl = Math.hypot(nx, ny, nz) || 1; nx /= nl; ny /= nl; nz /= nl;
      /* toward: the component toward the eye; up: the screen-up component. Cull back faces, and face marks below their minT */
      const toward = -ny * cam.ce + nz * cam.se; if (toward <= Math.max(1e-4, sw.minT ? (f.minT || 0) : 0)) continue;
      const up = ny * cam.se + nz * cam.ce, M = MATS[f.mat] || MATS.skin; let step = 0;
      if (!M.fixed) { let s = nx * K[0] + up * K[1] + toward * K[2] + (sw.form ? SH.form * (toward - SH.formMid) : 0); if (sw.roundS) s = Math.round(s * 1e9) / 1e9;
        let t = R(s * M.gain + M.bias + (sw.band ? (f.b || 0) : 0)); if (sw.loHi) t = Math.max(M.lo == null ? 0 : M.lo, Math.min(M.hi == null ? 99 : M.hi, t));
        step = Math.max(0, Math.min(M.ramp.length - 1, t + (sw.off ? (M.off || 0) : 0))); }
      const mi = mIx[f.mat] != null ? mIx[f.mat] : mIx.skin, db = sw.db ? (f.db || 0) : 0;
      for (let t = 1; t + 1 < P.length; t++) { const a = P[0], b = P[t], c = P[t + 1];
        const area = (b.sx - a.sx) * (c.sy - a.sy) - (c.sx - a.sx) * (b.sy - a.sy); if (Math.abs(area) < 1e-9) continue;
        const x0 = Math.max(0, Math.floor(Math.min(a.sx, b.sx, c.sx))), x1 = Math.min(W - 1, Math.ceil(Math.max(a.sx, b.sx, c.sx)));
        const y0 = Math.max(0, Math.floor(Math.min(a.sy, b.sy, c.sy))), y1 = Math.min(Hh - 1, Math.ceil(Math.max(a.sy, b.sy, c.sy)));
        for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) { const px = x + 0.5, py = y + 0.5;
          const w0 = ((b.sx - px) * (c.sy - py) - (c.sx - px) * (b.sy - py)) / area, w1 = ((c.sx - px) * (a.sy - py) - (a.sx - px) * (c.sy - py)) / area, w2 = 1 - w0 - w1;
          if (w0 < -1e-6 || w1 < -1e-6 || w2 < -1e-6) continue;
          const d = w0 * a.d + w1 * b.d + w2 * c.d, i = y * W + x, dq = sw.roundD ? Math.round((d - db) * 1e7) / 1e7 : d - db;
          if (dq < zb[i]) { zb[i] = dq; dep[i] = d; mat[i] = mi; stp[i] = step; } } } }
    if (sw.edges) { const drop = new Uint8Array(N);
      for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) { const i = y * W + x; if (mat[i] < 0) continue;
        for (const [dx, dy] of [[1, 0], [0, 1]]) { const X = x + dx, Y = y + dy; if (X >= W || Y >= Hh) continue; const j = Y * W + X; if (mat[j] < 0) continue;
          if (Math.abs(dep[i] - dep[j]) > SH.edge) drop[dep[i] > dep[j] ? i : j] = 1; } }
      for (let i = 0; i < N; i++) if (drop[i] && !MATS[keys[mat[i]]].fixed && stp[i] > 0) stp[i]--; }
    const col = new Array(N).fill(null); for (let i = 0; i < N; i++) if (mat[i] >= 0) { const M = MATS[keys[mat[i]]]; col[i] = M.ramp[Math.min(M.ramp.length - 1, stp[i])]; }
    const out = col.slice();
    if (sw.keyline) for (let y = 0; y < Hh; y++) for (let x = 0; x < W; x++) { const i = y * W + x; if (col[i]) continue; let src = null, sd = Infinity, sl = Infinity;
      for (const [dx, dy] of [[0, -1], [1, 0], [-1, 0], [0, 1]]) { const X = x + dx, Y = y + dy; if (X < 0 || X >= W || Y < 0 || Y >= Hh) continue; const j = Y * W + X, c = col[j]; if (!c) continue;
        const lum = hex2(c).reduce((s, v) => s + v, 0); if (dep[j] < sd - 1e-9 || (Math.abs(dep[j] - sd) <= 1e-9 && lum < sl)) { src = c; sd = dep[j]; sl = lum; } }
      if (src) out[i] = mixHex(SH.keyline, src, SH.keylineMix); }
    const rgba = new Uint8ClampedArray(N * 4); for (let i = 0; i < N; i++) { const c = out[i]; if (!c) continue; const h = hex2(c); rgba[i * 4] = h[0]; rgba[i * 4 + 1] = h[1]; rgba[i * 4 + 2] = h[2]; rgba[i * 4 + 3] = 255; }
    return rgba; }
  /* the head snap, as paintSolved does it: the head bone's centre (headMid) onto a pixel centre */
  const mV = (R, v) => [R[0] * v[0] + R[3] * v[1] + R[6] * v[2], R[1] * v[0] + R[4] * v[1] + R[7] * v[2], R[2] * v[0] + R[5] * v[1] + R[8] * v[2]];
  function snapOf(S, B, cam) { const Wh = S.W[B.sk.ix.head], c = mV(Wh.R, B.D.headMid), q = C.proj([Wh.p[0] + c[0], Wh.p[1] + c[1], Wh.p[2] + c[2]], cam);
    return [Math.round(q.sx - 0.5) + 0.5 - q.sx, Math.round(q.sy - 0.5) + 0.5 - q.sy]; }
  const diff = (a, b) => { let n = 0, fig = 0, sil = 0; for (let i = 0; i < a.length; i += 4) { const ea = a[i + 3] === 0, eb = b[i + 3] === 0; if (ea && eb) continue; fig++;
      if (a[i] !== b[i] || a[i + 1] !== b[i + 1] || a[i + 2] !== b[i + 2] || a[i + 3] !== b[i + 3]) n++; if (ea !== eb) sil++; } return { n, fig, sil }; };
  /* ---------------- 1. the reference equals the rig ---------------- */
  const POSES = [['idle', 0], ['walk', 0], ['walk', 4], ['run', 2], ['dig', 3], ['hold', 0], ['cast', 3], ['swim', 2], ['sleep', 0], ['astride', 0]];
  let renders = 0, bad = 0, badPx = 0;
  for (const p of C.CAST) { const B = C.buildOf(p);
    for (const [clip, k] of POSES) { const S = C.evalClip(clip, C.uOf(clip, k), B);
      for (let dir = 0; dir < 8; dir++) { const R = C.paintSolved(S, B, { dir }), cam = C.camOf({ dir }), mine = refPaint(C.posed(S, B), cam, B.mats, snapOf(S, B, cam));
        const d = diff(R.rgba, mine); renders++; if (d.n) { bad++; badPx += d.n; } } } }
  if (bad) problems.push('refPaint differs from paintSolved on ' + bad + ' of ' + renders + ' renders (' + badPx + ' px)');
  /* ---------------- 2. one part at a time ---------------- */
  const ABL = [
    ['form', { form:false }, 'no form term (s = n.key)'],
    ['loHi', { loHi:false }, 'no lo..hi limit (clamped to the ramp only)'],
    ['band', { band:false }, 'no per-face band offset b'],
    ['off', { off:false }, 'no material offset off (every shade variant on its base tone)'],
    ['edges', { edges:false }, 'no inner contour'],
    ['keyline', { keyline:false }, 'no keyline'],
    ['snap', { snap:false }, 'no head snap'],
    ['minT', { minT:false }, 'face marks without their minT (only the back-face cull)'],
    ['db', { db:false }, 'no depth bias db'],
    ['roundS', { roundS:false }, 's not rounded to 1e-9 before the step'],
    ['roundD', { roundD:false }, 'depth not quantised to 1e-7 before the test'],
    ['f32', { f32:false }, 'float64 depth buffers instead of float32'],
    ['bankers', { round:bankers }, 'round half to even (C# Math.Round, Unity Mathf.Round) instead of Math.round (half up)'],
    ['fleetKey', { key:SH.fleetKey }, 'rig 6/7\'s fleet key, with its left component, instead of v9\'s'] ];
  const APOSES = [['idle', 0], ['walk', 2], ['dig', 3]], abl = {};
  for (const [id, sw, what] of ABL) abl[id] = { what, renders:0, changed:0, px:0, fig:0, silhouette:0, worstPct:0, worstAt:null };
  for (const p of C.CAST) { const B = C.buildOf(p);
    for (const [clip, k] of APOSES) { const S = C.evalClip(clip, C.uOf(clip, k), B), faces = C.posed(S, B);
      for (let dir = 0; dir < 8; dir++) { const cam = C.camOf({ dir }), sn = snapOf(S, B, cam), base = refPaint(faces, cam, B.mats, sn);
        for (const [id, sw] of ABL) { const img = refPaint(faces, cam, B.mats, sw.snap === false ? null : sn, sw), d = diff(base, img), a = abl[id], pct = d.fig ? 100 * d.n / d.fig : 0;
          a.renders++; if (d.n) a.changed++; a.px += d.n; a.fig += d.fig; a.silhouette += d.sil; if (pct > a.worstPct + 1e-12) { a.worstPct = pct; a.worstAt = p + ' ' + clip + ' f' + k + ' ' + C.order[dir]; } } } } }
  /* 3. the mirror test (the golden suite's light check): the rest pose at E is W mirrored, to the pixel, on every preset */
  const mirrorPx = (sw) => { let px = 0;
    for (const p of C.CAST) { const B = C.buildOf(p), S = C.solve(C.baseIntent(B.D), B); S.B = B; const faces = C.posed(S, B), E = C.camOf({ dir:2 }), Wc = C.camOf({ dir:6 });
      const a = refPaint(faces, E, B.mats, sw.snap === false ? null : snapOf(S, B, E), sw), b = refPaint(faces, Wc, B.mats, sw.snap === false ? null : snapOf(S, B, Wc), sw);
      for (let y = 0; y < E.H; y++) for (let x = 0; x < E.W; x++) { const i = (y * E.W + x) * 4, j = (y * E.W + (E.W - 1 - x)) * 4; if (a[i] !== b[j] || a[i + 1] !== b[j + 1] || a[i + 2] !== b[j + 2] || a[i + 3] !== b[j + 3]) px++; } }
    return px; };
  const mirror0 = mirrorPx({}); if (mirror0) problems.push('the rest pose at E is not W mirrored (' + mirror0 + ' px)');
  for (const [id, sw] of ABL) abl[id].mirrorPx = mirrorPx(sw);
  for (const a of Object.values(abl)) { a.pct = a.fig ? +(100 * a.px / a.fig).toFixed(2) : 0; a.worstPct = +a.worstPct.toFixed(2); a.optional = a.px === 0 && a.mirrorPx === 0; delete a.fig; }

  /* ---------------- the spec ---------------- */
  const M0 = C.shadingContract('fisher').materials, T6 = { gain:2.4, bias:2.7, lo:2, hi:5 }, T5 = { gain:2.4, bias:1.8, lo:1, hi:4 };
  const all = {}; for (const p of C.CAST) Object.assign(all, C.shadingContract(p).materials);
  for (const hat of C.OPTIONS.hat) for (const g of C.OPTIONS.garment) Object.assign(all, C.shadingContract(Object.assign({}, C.BUILDS.fisher, { preset:null, hat, garment:g })).materials);
  const mats = {}; for (const m of Object.keys(all).sort()) { const x = all[m]; if (x.fixed) { mats[m] = { fixed:true }; continue; }
    const t = ['gain','bias','lo','hi'].every((k) => x[k] === T6[k]) ? 'T6' : ['gain','bias','lo','hi'].every((k) => x[k] === T5[k]) ? 'T5' : null; if (!t) problems.push('material ' + m + ' has no tone preset');
    mats[m] = { tone:t, gain:x.gain, bias:x.bias, lo:x.lo, hi:x.hi, off:x.off, ramp:x.ramp.length + ' tones' }; }
  const cam0 = C.camOf({ dir:0 });
  const spec = { schema:'hidden-harbours/character-shading@1', rig:'characterIsoRig9.js', exportSymbol:'CharacterIso9', derivedFromRigSha256:rigSha, posesDerivedFromRigSha256:poseSha, revision:C.revision,
    authoring:'Generated by tools/check-shading.cjs from CharacterIso9.SHADING and shadingContract(). Do not hand-edit: node tools/check-shading.cjs --write. refPaint() in that file is the rule in code.',
    camera:{ px_per_m:C.PX, elev_deg:C.ELEV, cell:[C.W, C.H], pivot:[C.pivot.x, C.pivot.y], facings:C.order, yaw_deg:'dir x 45 (N = 0, clockwise)',
      project:'x1 = x cos(roll) + z sin(roll); z1 = -x sin(roll) + z cos(roll); y2 = y cos(pitch) - z1 sin(pitch); z2 = y sin(pitch) + z1 cos(pitch); xr = x1 cos(-yaw) - y2 sin(-yaw); yr = x1 sin(-yaw) + y2 cos(-yaw); sx = pivotX + xr px; sy = pivotY - (yr sin(elev) + z2 cos(elev)) px - heave; depth d = yr cos(elev) - z2 sin(elev) (smaller is nearer). roll, pitch and heave are the hull\'s live rock, 0 on land.' },
    globals:{ key:SH.key, key_is:'normalise(0, 0.72, 0.52) in the screen basis (right, up, toward the eye): the fleet key without its left component. It turns with the camera, not with the world.',
      fleetKey:SH.fleetKey, form:SH.form, formMid:SH.formMid, edge_m:SH.edge, keyline:SH.keyline, keylineMix:SH.keylineMix, cullMin:1e-4, sRound:1e-9, depthRound:1e-7,
      tones:{ T6, T5 } },
    rule:[
      '1. Pose the bind mesh (linear blend skinning). Draw only the active face group of each slot (eyes, brows, mouth); body faces first, groups after, in mesh order.',
      '2. Head snap: move every face whose first vertex binds to the head by (round(hx - 0.5) + 0.5 - hx, round(hy - 0.5) + 0.5 - hy), where (hx, hy) is the head bone\'s headMid projected. Per facing, per frame.',
      '3. Normal n: Newell\'s method on the face\'s camera-space corners (xr, yr, zr), normalised. toward = -n.y cos(elev) + n.z sin(elev); up = n.y sin(elev) + n.z cos(elev).',
      '4. Cull: skip the face if toward <= max(1e-4, minT). minT is 0 for the body; the face marks carry 0.45 (near), 0.68 (far), 0.62 (side), 0.25 (mouth).',
      '5. s = n.x key[0] + up key[1] + toward key[2] + form (toward - formMid), then rounded to 1e-9: s = round(s 1e9) / 1e9.',
      '6. Fixed material: tone 0 (its one colour). Otherwise tone = min(len - 1, max(0, clamp(round(s gain + bias + b), lo, hi) + off)), b the face\'s band offset (0 or -1), len the ramp\'s length.',
      '7. round() is JavaScript Math.round: floor(x + 0.5), halves go up (-2.5 -> -2). Not half-to-even, not half-away-from-zero.',
      '8. Raster: fan the polygon from its first corner; skip a triangle with |area| < 1e-9 px^2; a pixel (x, y) is covered when its centre (x + 0.5, y + 0.5) has every barycentric weight >= -1e-6. Bounding box floor(min) .. ceil(max), clipped to the cell.',
      '9. Depth test: d interpolated linearly in screen space; dq = round((d - db) 1e7) / 1e7 in double precision; write when dq < zbuf (strict: the first face drawn keeps a tie). zbuf and the stored depth are float32 buffers (Float32Array).',
      '10. Inner contour: for every covered pixel and its right and lower neighbour, both covered, when |depth_i - depth_j| > edge (0.12 m) the farther one is marked. A marked pixel of a ramp material with tone > 0 drops one tone (once, however many marks).',
      '11. Colour = ramp[min(len - 1, tone)].',
      '12. Keyline: an empty pixel with a covered 4-neighbour (checked up, right, left, down) takes the neighbour with the smallest depth (within 1e-9, the darker by r + g + b) and becomes mix(keyline, that colour, 0.22), per channel round(k + (c - k) 0.22). One ring; keyline pixels are opaque.'],
    materials:mats,
    ramps:'each build\'s ramps are in builds/<preset>.v9.json shading.materials and, for every option, data/options.v9.json (materials, ramps)',
    rig7:'rig 6 / 7: tone index = sh GAIN gain + bias + b with GAIN 3.0 and a per-material span, sh = n . LN with LN the fleet key (-0.42, 0.72, 0.52) in the same screen basis, a step up when the fraction passes 0.55 (a 4x4 Bayer threshold on hair), no lo / hi limit, no form term, b a continuous offset, back faces with b <= -1 shaded by the flipped normal x 0.9, contour depth 0.13 m.' };
  const text = JSON.stringify(spec, null, 1) + '\n', committed = H.exists(OUT) ? H.text(OUT) : null, same = committed === text;
  if (!problems.length && !same) problems.push(OUT + (committed == null ? ' is missing' : ' differs from the regenerated file'));
  const L = [], pad = (s, n) => (String(s) + '                                        ').slice(0, n);
  L.push('check-shading — the facet pass, written out and switched part by part', 'rig ' + rigSha + '  poses ' + poseSha, '');
  L.push('refPaint (every part on) against CharacterIso9.paintSolved: ' + renders + ' renders, ' + (bad ? bad + ' DIFFER (' + badPx + ' px)' : 'identical to the pixel'), '');
  L.push('One part off at a time, ' + abl.form.renders + ' renders (10 presets x idle f0, walk f2, dig f3 x 8 facings); % of figure pixels that change:');
  L.push('  ' + pad('part', 10) + pad('renders changed', 17) + pad('pixels', 9) + pad('mean %', 8) + pad('worst %', 9) + pad('silhouette px', 15) + pad('E/W mirror px', 15) + 'what');
  for (const [id] of ABL) { const a = abl[id]; L.push('  ' + pad(id, 10) + pad(a.changed + ' / ' + a.renders, 17) + pad(a.px, 9) + pad(a.pct, 8) + pad(a.worstPct, 9) + pad(a.silhouette, 15) + pad(a.mirrorPx, 15) + a.what); }
  L.push('  E/W mirror px: the rest pose at E against W mirrored, summed over the ten presets (' + mirror0 + ' with every part on).');
  const opt = ABL.filter(([id]) => abl[id].optional).map(([id]) => id);
  L.push('', 'Optional for the look (switching it off changed no pixel in this sample and kept E the mirror of W): ' + (opt.length ? opt.join(', ') : 'none') + '.');
  L.push(OUT + ': ' + (same ? 'regenerated, byte-identical to the committed file' : committed == null ? 'MISSING (run with --write)' : 'DIFFERS from the regenerated file'));
  L.push(problems.length ? 'PROBLEMS:\n  ' + problems.join('\n  ') : 'OK — no problems.', '');
  const json = { tool:'check-shading', rig:rigSha, poses:poseSha, ok:problems.length === 0, problems, identity:{ renders, differ:bad, px:badPx }, mirrorPx:mirror0, ablations:abl, optional:opt, dataFile:OUT, dataIdentical:same };
  return { name:'shading', ok:json.ok, json, text:L.join('\n'), data:{ [OUT]:text } };
}
if (typeof module !== 'undefined' && module.exports) { module.exports = run; if (require.main === module) require('./_run.cjs').main(run); }
else (globalThis.HHTools = globalThis.HHTools || {}).shading = run;
})();
