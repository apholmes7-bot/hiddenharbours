/* Hidden Harbours — WHARF RIG PASS 2 · THE CHECKS (globalThis.WharfChecks2). Plain script: runs in Node (checks/run.js) or in
   a page or sandbox after the five rig files. Deterministic: a re-run writes the same checks/out/*.txt.

     run(env, o) -> [{ name, ok, text, json }]     env.read(path) -> string | null (kit-relative), o.only = [names], o.keys = [keys]

     sidecars   every committed gameplay/*.json is byte for byte what WharfRig2Kit.gameplay() writes today (mid tide, frame 0)
     fits       every ladder, tie, boarding step, fixture and verb fits every creator body (character v9.2), at five tides
     gangways   every harbour gangway: toe rollers >= 0.3 m inside the float deck at chart datum and at highest water; slope
     joins      every joined end meets another module's deck edge within 3 cm, at the same deck height (fixed) or freeboard (float)
     berths     no hull footprint overlaps another hull, another module or a gangway; every berth floats at some tide
     snow       no snow below the wash line, on a face steeper than 72 degrees, or on growth; cover fractions
     sprites    cell, pivot and sheet for every preset at every facing; a moving preset's 4 x 4 loop within 2048          */
(function (root) {
  'use strict';
  const r2 = (v) => Math.round(v * 100) / 100, r3 = (v) => Math.round(v * 1000) / 1000, pad = (s, n) => (String(s) + ' '.repeat(n)).slice(0, Math.max(n, String(s).length));
  const HARB = ['fishing', 'cove', 'commercial', 'jetty'];
  const W = () => root.WharfRig2, K = () => root.WharfRig2Kit, V = () => root.WharfVerbs2, G = () => root.WharfGeo2;
  const keys = () => Object.keys(W().PRESETS).concat(HARB.map(h => 'harbour:' + h));
  const isH = (k) => /^harbour:/.test(k), fileOf = (k) => 'wharfRig2.' + k.replace(':', '-') + '.gameplay.json';
  const rangeOf = (k) => W().model(k, {}).tideRange, base = (k) => ({ dir: isH(k) ? 0 : 1, frame: 0 });
  const gameplay = (k, q) => K().gameplay(k, Object.assign(base(k), { tide: r3(rangeOf(k) * q) }));
  const sidecar = (k) => gameplay(k, 0.5), text = (gp) => JSON.stringify(gp, null, 1) + '\n';
  const res = (name, ok, lines, json) => ({ name, ok, text: ['check ' + name + ' \u2014 ' + (ok ? 'OK' : 'PROBLEMS'), ''].concat(lines).join('\n') + '\n', json: Object.assign({ check: name, ok }, json || {}) });

  function sidecars(env, o) {
    const L = [], diff = [], miss = []; let same = 0;
    for (const k of o.keys || keys()) { const t = text(sidecar(k)), c = env.read ? env.read('gameplay/' + fileOf(k)) : null;
      if (c == null) miss.push(k); else if (c === t) same++; else diff.push(k); L.push(pad(fileOf(k), 54) + (c == null ? 'MISSING' : c === t ? 'identical' : 'DIFFERS')); }
    L.push('', same + ' identical, ' + diff.length + ' differ, ' + miss.length + ' missing');
    return res('sidecars', !diff.length && !miss.length, L, { same, diff, miss });
  }

  function fits(env, o) {
    const L = [], bad = [], tot = { climb: 0, tie: 0, board: 0, fixtures: 0, verbs: 0 }, Q = [0, 0.25, 0.5, 0.75, 1];
    const rep = V().fitReport(); for (const r of rep) if (!r.ok) bad.push('fitReport ' + r.fixture + ' ' + r.what + ' ' + r.value);
    L.push('fixture heights against the character v9.2 world fit (every creator body, 5 mm):');
    for (const r of rep) L.push('  ' + pad(r.fixture + ' \u00b7 ' + r.what, 52) + pad(String(r.value), 13) + (r.range ? r.range.join('\u2013') : '') + (r.ok ? '' : '  OUTSIDE'));
    L.push('', 'per key, over tides ' + Q.map(q => q * 100 + '%').join(' ') + ' of the range (counts summed):', '  ' + pad('key', 26) + pad('climb', 7) + pad('tie', 6) + pad('board', 7) + pad('fixt', 6) + pad('verbs', 7) + 'unfit');
    for (const k of o.keys || keys()) { const n = { climb: 0, tie: 0, board: 0, fixtures: 0, verbs: 0 }; let f = 0;
      for (const q of Q) { const gp = gameplay(k, q), at = ' @' + r2(gp.params.tide);
        for (const c of gp.CLIMB) { n.climb++; if (!c.fitsCreatorBodies) { f++; bad.push(k + at + ' ' + c.id + ' rung ' + c.rung); } }
        for (const t of gp.TIE) { n.tie++; if (!t.fitsCreatorBodies) { f++; bad.push(k + at + ' ' + t.id + ' lift ' + t.opts.lift); } }
        for (const b of gp.BOARD) { n.board++; if (!b.fitsCreatorBodies) { f++; bad.push(k + at + ' ' + b.berth + ' step ' + b.step + ' ' + JSON.stringify(b.how)); } }
        for (const x of gp.FIXTURES) { n.fixtures++; if (x.fits.some(y => !y.ok)) { f++; bad.push(k + at + ' ' + x.id); } }
        for (const v of gp.VERBS) { n.verbs++; if ((v.fits || []).some(y => !y.ok) || v.fitsCreatorBodies === false) { f++; bad.push(k + at + ' ' + v.id); } } }
      for (const q in n) tot[q] += n[q];
      L.push('  ' + pad(k, 26) + pad(n.climb, 7) + pad(n.tie, 6) + pad(n.board, 7) + pad(n.fixtures, 6) + pad(n.verbs, 7) + f); }
    L.push('', 'total ' + Object.entries(tot).map(([a, b]) => b + ' ' + a).join(' \u00b7 ') + ' \u00b7 ' + bad.length + ' unfit');
    if (bad.length) L.push('', 'UNFIT:', ...bad.slice(0, 80).map(b => '  ' + b));
    return res('fits', !bad.length, L, { total: tot, unfit: bad });
  }

  function gangways(env, o) {
    const L = [], bad = [];
    for (const h of HARB) { const k = 'harbour:' + h; if (o.keys && o.keys.indexOf(k) < 0) continue;
      for (const q of [0, 1]) { const gp = gameplay(k, q);
        for (const w of gp.WALK.filter(x => x.kind === 'gangway')) { const m = w.toeOnFloat_m || {}, okT = m.datum >= 0.3 && m.highestWater >= 0.3, okS = q > 0 || w.slopeDeg <= G().FIT.gang.maxSlope + 0.6;
          if (!okT || !okS || !w.walkable) bad.push(k + ' ' + w.module + ' @' + gp.params.tide);
          L.push(pad(k + ' ' + w.module, 30) + pad('tide ' + gp.params.tide, 12) + pad('L ' + w.L + ' m', 10) + pad('slope ' + w.slopeDeg + '\u00b0', 15) + 'toe inside the float ' + m.datum + ' m at datum, ' + m.highestWater + ' m at highest water' + (okT && okS ? '' : '  PROBLEM')); } } }
    L.push('', 'rule: toe >= 0.3 m inside the deck at both extremes; slope at chart datum <= 18.43\u00b0 + 0.6\u00b0 of sea heave', bad.length + ' problems');
    return res('gangways', !bad.length, L, { problems: bad });
  }

  function joins(env, o) {
    const L = [], bad = []; let n = 0;
    for (const h of HARB) { const k = 'harbour:' + h; if (o.keys && o.keys.indexOf(k) < 0) continue; const mdl = W().model(k, {});
      const R = mdl.modules.map(M => ({ M, hx: M.s.L / 2, hy: M.s.W / 2, z: M.s.floating ? M.s.freeboard : M.s.deckZ }));
      for (const A of R) for (const e of ['nx', 'px']) { if (A.M.s.ends[e] !== 'join') continue; n++;
        const p = A.M.toH(e === 'nx' ? -A.hx : A.hx, 0); let best = null;
        for (const B of R) { if (B === A) continue; const c = Math.cos(B.M.rot), s = Math.sin(B.M.rot), dx = p[0] - B.M.at[0], dy = p[1] - B.M.at[1], u = dx * c + dy * s, v = -dx * s + dy * c, d = Math.max(Math.abs(u) - B.hx, Math.abs(v) - B.hy);
          if (Math.abs(d) < 0.03 && (!best || Math.abs(d) < Math.abs(best.d))) best = { B, d }; }
        const ok = !!best && Math.abs(best.B.z - A.z) < 0.006 && !!best.B.M.s.floating === !!A.M.s.floating && (!A.M.s.floating || best.B.M.grp === A.M.grp);
        if (!ok) bad.push(k + ' ' + A.M.id + '.' + e);
        L.push(pad(k, 20) + pad(A.M.id + '.' + e, 16) + (best ? 'meets ' + pad(best.B.M.id, 10) + ' edge ' + r3(best.d) + ' m \u00b7 deck ' + r3(A.z) + ' / ' + r3(best.B.z) + (A.M.s.floating ? ' \u00b7 group ' + A.M.grp + ' / ' + best.B.M.grp : '') : 'meets nothing') + (ok ? '' : '  PROBLEM')); } }
    L.push('', n + ' joined ends, ' + bad.length + ' problems');
    return res('joins', !bad.length, L, { joins: n, problems: bad });
  }

  // separating-axis overlap of two convex quads (metres), a 2 cm tolerance so touching is not overlapping
  function overlap(P, Q) { for (const S of [P, Q]) for (let i = 0; i < 4; i++) { const a = S[i], b = S[(i + 1) % 4], nx = -(b[1] - a[1]), ny = b[0] - a[0], l = Math.hypot(nx, ny) || 1;
      let p0 = 1e9, p1 = -1e9, q0 = 1e9, q1 = -1e9; for (const v of P) { const t = (v[0] * nx + v[1] * ny) / l; p0 = Math.min(p0, t); p1 = Math.max(p1, t); } for (const v of Q) { const t = (v[0] * nx + v[1] * ny) / l; q0 = Math.min(q0, t); q1 = Math.max(q1, t); }
      if (p1 <= q0 + 0.02 || q1 <= p0 + 0.02) return false; } return true; }
  const quad = (c, h, L, B) => { const n = [-h[1], h[0]]; return [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([a, b]) => [c[0] + h[0] * a * L / 2 + n[0] * b * B / 2, c[1] + h[1] * a * L / 2 + n[1] * b * B / 2]); };
  function berths(env, o) {
    const L = [], bad = []; let n = 0;
    for (const h of HARB) { const k = 'harbour:' + h; if (o.keys && o.keys.indexOf(k) < 0) continue; const gp = gameplay(k, 0.5), mdl = W().model(k, {}), Rr = mdl.tideRange;
      const B = gp.BERTHS.map(b => ({ b, q: quad(b.footprint.centre, b.footprint.heading, b.footprint.length, b.footprint.width) }));
      const M = mdl.modules.map(m => ({ id: m.id, q: quad(m.at, [Math.cos(m.rot), Math.sin(m.rot)], m.s.L, m.s.W) }));
      const Gw = gp.WALK.filter(w => w.kind === 'gangway').map(w => { const a = w.hinge, t = w.toe, d = [t[0] - a[0], t[1] - a[1]], l = Math.hypot(d[0], d[1]) || 1; return { id: w.module, q: quad([(a[0] + t[0]) / 2, (a[1] + t[1]) / 2], [d[0] / l, d[1] / l], l, (w.clearWidth || 1.1) + 0.14) }; });
      L.push(k + ' \u00b7 ' + B.length + ' berths');
      for (let i = 0; i < B.length; i++) { const x = B[i], hit = []; n++;
        for (let j = i + 1; j < B.length; j++) if (overlap(x.q, B[j].q)) hit.push('berth ' + B[j].b.id);
        for (const m of M) if (m.id !== x.b.module && overlap(x.q, m.q)) hit.push('module ' + m.id);
        for (const g of Gw) if (overlap(x.q, g.q)) hit.push('gangway ' + g.id);
        const floats = x.b.floatsFrom <= Rr; if (hit.length || !floats) bad.push(x.b.id + ' ' + hit.join(', ') + (floats ? '' : ' never floats'));
        L.push('  ' + pad(x.b.id, 28) + pad(x.b.label, 26) + pad('bed ' + x.b.bedUnder, 11) + pad(x.b.afloatAtDatum ? 'afloat at datum' : 'dries, floats from ' + x.b.floatsFrom + ' m', 30) + 'ties ' + x.b.ties.length + (x.b.ladder ? ' \u00b7 ladder' : '') + (hit.length ? '  OVERLAPS ' + hit.join(', ') : '') + (floats ? '' : '  NEVER FLOATS')); } }
    L.push('', n + ' berths, ' + bad.length + ' problems');
    return res('berths', !bad.length, L, { berths: n, problems: bad });
  }

  function snow(env, o) {
    const L = [], bad = [], cases = [['tallPier', 0], ['logCrib', 0], ['timberFloat', 0], ['timberFloat', 5], ['breakwater', 0], ['torbayQuay', 0]];
    for (const [k, f] of cases) { if (o.keys && o.keys.indexOf(k) < 0) continue; const R = rangeOf(k), fr = W().frame(k, { dir: 1, tide: r3(R * 0.5), frame: f, season: 'winter', wind: { w: 0.4, gust: 0.4, dir: 1 } }), gb = fr.gb, MA = fr.mdl.MA, wash = fr.tide + 0.25 + 0.2 * fr.wind.w;
      let px = 0, never = 0, low = 0, steep = 0, grow = 0; const cov = [0.25, 0.5, 0.75, 1].map(() => 0);
      for (let i = 0; i < fr.w * fr.h; i++) { if (!gb.a[i]) continue; px++; const b = fr.snow[i]; if (b === 255) { never++; continue; }
        if (gb.Z[i] < wash - 1e-6) low++; if (gb.nz[i] < 0.3) steep++; if (!MA[gb.mi[i]].hold) grow++;
        [0.25, 0.5, 0.75, 1].forEach((c, j) => { if (Math.round(c * 254) >= b) cov[j]++; }); }
      const ok = !low && !steep && !grow; if (!ok) bad.push(k + ' f' + f);
      L.push(pad(k + ' winter f' + f, 24) + pad(px + ' px', 10) + pad('never ' + Math.round(never / px * 100) + '%', 12) + 'snowed at 25/50/75/100%: ' + cov.map(c => Math.round(c / px * 100) + '%').join(' / ') + (ok ? '' : '  PROBLEM low ' + low + ' steep ' + steep + ' growth ' + grow)); }
    L.push('', 'rule: snowed = round(cover \u00d7 254) >= byte, 255 never; never below tide + 0.25 + 0.2\u00b7wind, never where n.z < 0.3, never on growth', bad.length + ' problems');
    return res('snow', !bad.length, L, { problems: bad });
  }

  function sprites(env, o) {
    const L = [], bad = [], table = {}, DIRS = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
    L.push(pad('preset', 18) + pad('moving', 8) + DIRS.map(d => pad(d, 11)).join('') + 'largest sheet');
    for (const k of o.keys || Object.keys(W().PRESETS)) { if (isH(k)) continue; const R = rangeOf(k), row = []; let big = [0, 0], mv = false;
      for (let d = 0; d < 8; d++) { const sp = W().sheetSpec(k, { dir: d, tide: r3(R * 0.5), frame: 0 }); mv = sp.moving; const sw = mv ? sp.w : sp.cell[0], sh = mv ? sp.h : sp.cell[1];
        if (sw * sh > big[0] * big[1]) big = [sw, sh]; if (sw > 2048 || sh > 2048) bad.push(k + ' ' + DIRS[d] + ' ' + sw + '\u00d7' + sh); row.push({ dir: d, cell: sp.cell, pivot: sp.pivot, sheet: mv ? [sp.w, sp.h] : null }); }
      table[k] = { moving: mv, frames: mv ? 16 : 1, dirs: row }; L.push(pad(k, 18) + pad(mv ? 'yes' : 'no', 8) + row.map(r => pad(r.cell.join('\u00d7'), 11)).join('') + big.join('\u00d7')); }
    L.push('', 'a fixed module is one frame per facing (only the water moves, and the water is the shader\u2019s); a floating one is the 16-frame loop as 4 \u00d7 4', bad.length + ' over 2048');
    const json = { schema: 'hidden-harbours/wharf-sprites@2', ppu: 32, elev: 40, pivotIs: 'the projection of the model origin (module centre at chart datum)', presets: table };
    const c = env.read ? env.read('maps/sprites.json') : null, same = c == null ? null : c === JSON.stringify(json, null, 1) + '\n';
    if (same === false) bad.push('maps/sprites.json differs'); L.push('maps/sprites.json: ' + (c == null ? 'not compared' : same ? 'identical' : 'DIFFERS'));
    return res('sprites', !bad.length, L, { problems: bad, table: json });
  }

  const ALL = { sidecars, fits, gangways, joins, berths, snow, sprites };
  function run(env, o) { env = env || {}; o = o || {}; return (o.only || Object.keys(ALL)).map(n => ALL[n](env, o)); }
  root.WharfChecks2 = { run, ALL, keys, HARB, fileOf, sidecar, gameplay, text, overlap, quad };
})(typeof globalThis !== 'undefined' ? globalThis : window);
