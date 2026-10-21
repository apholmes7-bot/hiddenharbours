// NMC walls (H3): the faces G3 holds that no live wall covers, measured, and a held Def for each one that needs a wall.
// Re-runnable (rule 6): every number is read off G3, the scene, the tide's assets or the ground plan here.
//
// Each steep piece of G3 that falls a metre or more (facemap.cjs) gets a guide: the piece's mid-height contour, traced on
// the texel grid (marching squares) and walked every metre (St Peters' 1 m cut). At each station G3 is read along the
// downhill normal for the face through it, brow and toe off G3. A station is then, in this order:
//   none     no face of 1 m or more crosses the guide there;
//   quay     the face is a piece QuayWalls' runs touch: the wharf's;
//   covered  the face's middle lies within COVER of a live wall's footprint: that wall's;
//   built    the face's brow lies inside the ground plan's built grading (a fill, cut, pad, slip, ramp or a lot's laid
//            patio): its owner's;
//   falls    it stands at the Falls (the town river's head of tide): H7's wave;
//   seabed   less than MinFaceDrop of it stands above neap high water: the sea's floor, not a coast face;
//   away     it faces north of the kit's aspects (W to E): the camera sees its brow, not its face;
//   bank     it is under the builder's 55 deg (StPetersCliffWalls.MinBatterDegrees): the ground draws it, as it draws
//            every slope a wall does not stand on, unless the key scene laid it (grading kind scene_laid);
//   new      a face G3 asks for that nothing covers: one the builder's law calls a wall, or one the key scene laid.
// A banked wall (plan B) covers the face below it: its footprint reaches twice its run seaward, the reach the inventory
// read its station's face in.
// The new stations, in runs along a guide, are cut where the aspect or the batter rung changes and where the chunk math
// would cut them; each piece of two or more stations is one held Def (wall.nmc_NNN from the first free id), its lines
// read off G3 and its FaceNeeded the face it wears. A held Def spawns nothing: it is the forecast of a wall to come.
'use strict';
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const F = require('./faces.cjs'), C = require('./chunks.cjs');

const STATION = 1;            // m between a held face's stations: St Peters' 1 m cut
const REACH = 6;              // m either side of the guide its normal is read for the face
const COVER = 2;              // m: a face whose middle is within this of a live wall's footprint is that wall's
const SMOOTH = 2;             // stations either side whose batter rungs vote for a station's
const FALLS_REACH = 25;       // m from the head of tide the plan calls the Falls: the Falls are H7's
const ROOT = path.resolve(__dirname, '../..');
const REGION = 'Assets/_Project/Data/Regions/NineMileCreek.asset';
const CONFIG = 'Assets/_Project/Data/Config/GameConfig.asset';
const PLAN_SHA = '06d0217c';  // the ground plan's sha256 prefix (the re-issue 3 drop, ground/plan.json)
const f = Math.fround;
const fx = (x, n = 2) => (+x).toFixed(n);
const range = (xs, n = 2) => (xs.length ? (fx(Math.min(...xs), n) === fx(Math.max(...xs), n) ? fx(xs[0], n) : `${fx(Math.min(...xs), n)} to ${fx(Math.max(...xs), n)}`) : '-');

/** Neap high water: the region's mean level plus its amplitude times the config's neap fraction. */
function neapHigh() {
  const num = (file, key) => { const m = fs.readFileSync(path.join(ROOT, file), 'utf8').match(new RegExp('^  ' + key + ': (.*)$', 'm')); if (!m) throw new Error(`${file} has no ${key}`); return +m[1]; };
  const mean = num(REGION, 'TideMeanLevel'), amp = num(REGION, 'TideAmplitude'), neap = num(CONFIG, 'NeapAmplitudeFraction');
  return { mean, amp, neap, neapHigh: mean + amp * neap, springHigh: mean + amp };
}

// ---- the ground plan: built grading and the features' names ---------------------------------------------------------
function readPlan(file) {
  if (!file) throw new Error('newfaces needs --ground-plan <ground/plan.json> (the re-issue 3 drop)');
  const text = fs.readFileSync(file), sha = crypto.createHash('sha256').update(text).digest('hex');
  if (!sha.startsWith(PLAN_SHA)) throw new Error(`the ground plan's sha256 is ${sha}, not ${PLAN_SHA}...`);
  return { plan: JSON.parse(text), sha };
}
const inBox = (p, b, m = 0) => p[0] >= b[0] - m && p[0] <= b[1] + m && p[1] >= b[2] - m && p[1] <= b[3] + m;
/** The built grading whose box holds a point (a face's brow: a pad's or a wall's edge is its top), within a metre: a
 *  fill, cut, pad, slip, ramp or a lot's laid patio. The south shore's laid ground (scene_laid) is the ground itself. */
function builtAt(plan, p) {
  for (const gr of plan.grading) {
    if (gr.kind === 'scene_laid') continue;
    const boxes = [gr.area, gr.box, ...(Array.isArray(gr.boxes) ? gr.boxes : [])].map(b => (b && !Array.isArray(b) ? { ...b, b: b.box } : { b }));
    for (const { b, lot, what } of boxes)
      if (Array.isArray(b) && b.length === 4 && b.every(Number.isFinite) && inBox(p, b, 1)) return gr.id + (lot ? ` (${lot}, ${what})` : '');
  }
  return null;
}
/** The Falls: within FALLS_REACH of a stream's head of tide that the plan calls the Falls (H7's wave). */
function fallsAt(plan, p) {
  for (const st of plan.water.streams) {
    const h = st.head_of_tide;
    if (h && Array.isArray(h.at) && /^the Falls/.test(h.rule || '') && Math.hypot(p[0] - h.at[0], p[1] - h.at[1]) <= FALLS_REACH) return 'the Falls';
  }
  return null;
}
const toSeg = (p, a, b) => { const vx = b[0] - a[0], vy = b[1] - a[1], L2 = vx * vx + vy * vy; const t = L2 ? Math.max(0, Math.min(1, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vy) / L2)) : 0; return Math.hypot(p[0] - a[0] - vx * t, p[1] - a[1] - vy * t); };
const toLine = (p, pts) => { let d = Infinity; for (let i = 0; i + 1 < pts.length; i++) d = Math.min(d, toSeg(p, pts[i], pts[i + 1])); return d; };
function insidePoly(p, poly) {
  let inside = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const a = poly[j], b = poly[i];
    if ((a[1] > p[1]) !== (b[1] > p[1]) && p[0] < (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0]) inside = !inside;
  }
  return inside;
}
const toPoly = (p, poly) => (insidePoly(p, poly) ? 0 : toLine(p, [...poly, poly[0]]));

/** The plan's name for the ground at a point: the salt pond, a pond, a stream's bank, the bluff, a coast section. */
function featureAt(plan, p, aspect) {
  for (const pd of plan.water.ponds) {
    if (Array.isArray(pd.lobes) && pd.lobes.some(l => ((p[0] - l.c[0]) / (l.ax + 4)) ** 2 + ((p[1] - l.c[1]) / (l.ay + 4)) ** 2 <= 1)) return pd.name + "'s bank";
    if (Array.isArray(pd.outline) && Array.isArray(pd.outline[0]) && toPoly(p, pd.outline) <= 6) return pd.name + "'s bank";
    if (Array.isArray(pd.c) && Number.isFinite(pd.r) && Math.hypot(p[0] - pd.c[0], p[1] - pd.c[1]) <= pd.r + 6) return pd.name + "'s bank";
  }
  for (const st of plan.water.streams) {
    if (!Array.isArray(st.pts)) continue;
    const wMax = Math.max(...(Array.isArray(st.w) ? st.w : [st.w || 4]));
    if (toLine(p, st.pts) <= wMax / 2 + 10) return (st.name || st.id) + "'s bank";
  }
  const laid = plan.grading.find(gr => gr.id === 'laid.nmc_south_shore');
  if (laid && inBox(p, laid.box) && aspect === 'S') return "the south shore's bluff";
  const ss = plan.coast.south_shore;
  if (ss && Array.isArray(ss.line) && toLine(p, ss.line) <= 90) {
    const sec = ss.sections.find(s => p[0] >= s.from && p[0] <= s.to);
    return sec ? sec.name : ss.name;
  }
  return null;
}

// ---- the guide: a piece's mid-height contour, marching squares over the texel centres --------------------------------
function guides(g, lab, piece) {
  const W = g.W, H = g.H, E = g.elev, level = (piece.zMax + piece.zMin) / 2;
  const c0 = Math.max(0, piece.c0 - 2), c1 = Math.min(W - 2, piece.c1 + 1), r0 = Math.max(0, piece.r0 - 2), r1 = Math.min(H - 2, piece.r1 + 1);
  const mine = (c, r) => lab[r * W + c] === piece.id;
  const pt = new Map(), adj = new Map();
  const edge = (c, r, horiz) => {                       // the crossing on the edge from (c, r) east (horiz) or north
    const k = (horiz ? 'h' : 'v') + c + ',' + r;
    if (!pt.has(k)) {
      const a = E[r * W + c], b = horiz ? E[r * W + c + 1] : E[(r + 1) * W + c], t = (level - a) / (b - a);
      const [x, y] = g.centre(c, r);
      pt.set(k, horiz ? [x + t * 0.5, y] : [x, y + t * 0.5]);
    }
    return k;
  };
  const link = (a, b) => { for (const [u, v] of [[a, b], [b, a]]) { if (!adj.has(u)) adj.set(u, []); adj.get(u).push(v); } };
  for (let r = r0; r <= r1; r++) for (let c = c0; c <= c1; c++) {
    if (!(mine(c, r) || mine(c + 1, r) || mine(c, r + 1) || mine(c + 1, r + 1))) continue;
    const z00 = E[r * W + c], z10 = E[r * W + c + 1], z01 = E[(r + 1) * W + c], z11 = E[(r + 1) * W + c + 1];
    const code = (z00 > level ? 1 : 0) | (z10 > level ? 2 : 0) | (z11 > level ? 4 : 0) | (z01 > level ? 8 : 0);
    if (code === 0 || code === 15) continue;
    const S = () => edge(c, r, true), N = () => edge(c, r + 1, true), Wd = () => edge(c, r, false), Ed = () => edge(c + 1, r, false);
    const centreHigh = (z00 + z10 + z01 + z11) / 4 > level;
    switch (code) {
      case 1: case 14: link(S(), Wd()); break;
      case 2: case 13: link(S(), Ed()); break;
      case 3: case 12: link(Wd(), Ed()); break;
      case 4: case 11: link(N(), Ed()); break;
      case 6: case 9: link(S(), N()); break;
      case 7: case 8: link(Wd(), N()); break;
      case 5: if (centreHigh) { link(S(), Ed()); link(N(), Wd()); } else { link(S(), Wd()); link(N(), Ed()); } break;
      case 10: if (centreHigh) { link(S(), Wd()); link(N(), Ed()); } else { link(S(), Ed()); link(N(), Wd()); } break;
    }
  }
  // chain the segments: open chains from their ends first, then the closed loops, each from its westmost point
  const seen = new Set(), chains = [];
  const walk = (start) => { const out = [start]; seen.add(start); let prev = null, cur = start;
    for (;;) { const nx = (adj.get(cur) || []).find(v => v !== prev && !seen.has(v)); if (!nx) break; out.push(nx); seen.add(nx); prev = cur; cur = nx; }
    if (out.length > 2 && (adj.get(cur) || []).includes(start)) out.push(start);
    return out; };
  const keys = [...adj.keys()].sort();
  for (const k of keys) if (!seen.has(k) && adj.get(k).length === 1) chains.push(walk(k));
  const rest = keys.filter(k => !seen.has(k)).sort((a, b) => pt.get(a)[0] - pt.get(b)[0] || pt.get(a)[1] - pt.get(b)[1]);
  for (const k of rest) if (!seen.has(k)) chains.push(walk(k));
  // walk each so that downhill is on its right (the high ground on the left), every STATION metres
  return chains.map(ch => {
    let pts = ch.map(k => pt.get(k));
    if (pts.length < 2) return [];
    const [a, b] = [pts[0], pts[1]], tx = b[0] - a[0], ty = b[1] - a[1], m = [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];
    const right = [m[0] + ty * 0.5, m[1] - tx * 0.5];          // (tx, ty) turned clockwise
    if (g.at(right[0], right[1]) > g.at(m[0] - ty * 0.5, m[1] + tx * 0.5)) pts = pts.reverse();
    const out = [pts[0]]; let carry = 0;
    for (let i = 0; i + 1 < pts.length; i++) {
      const [x0, y0] = pts[i], [x1, y1] = pts[i + 1], L = Math.hypot(x1 - x0, y1 - y0);
      let s = STATION - carry;
      while (s <= L + 1e-9) { out.push([x0 + (x1 - x0) * s / L, y0 + (y1 - y0) * s / L]); s += STATION; }
      carry = L - (s - STATION);
    }
    return out;
  }).filter(ch => ch.length >= 2);
}

/** The face through a guide point: G3 along the downhill normal, brow and toe read off G3 in float32. */
function faceAt(g, at, p) {
  const e = 0.25, gx = (g.at(p[0] + e, p[1]) - g.at(p[0] - e, p[1])) / (2 * e), gy = (g.at(p[0], p[1] + e) - g.at(p[0], p[1] - e)) / (2 * e);
  const n = Math.hypot(gx, gy); if (!(n > 1e-6)) return null;
  const d = [-gx / n, -gy / n];
  let best = null, gap = Infinity;
  for (const fc of F.faces(F.profile(g, p, d, -REACH, REACH))) {
    const gp = Math.max(0, fc.sBrow, -fc.sToe);
    if (gp < gap) { gap = gp; best = fc; }
  }
  if (!best || gap > 0.5) return null;
  const P = (s) => [f(p[0] + d[0] * s), f(p[1] + d[1] * s)];
  const brow = P(best.sBrow), toe = P(best.sToe), browZ = at(brow), toeZ = at(toe);
  const st = { brow, toe, toeZ, drop: f(browZ - toeZ), browZ };
  const az = F.azimuthOf([toe[0] - brow[0], toe[1] - brow[1]]);
  return { ...st, batter: C.batterDegrees(st), run: Math.hypot(toe[0] - brow[0], toe[1] - brow[1]), az, ...F.aspectOf(az),
    mid: [(brow[0] + toe[0]) / 2, (brow[1] + toe[1]) / 2] };
}

/**
 * The held faces. ctx: { g, at, defs (derive-walls' Defs, live ones standing), map (facemap), quayPieces, firstId,
 * groundPlan (path) }. Returns { held: [Def], pieces: [report row], tide, planSha }.
 */
function heldFaces(ctx) {
  const { g, at, defs, map, quayPieces } = ctx;
  const tide = neapHigh(), { plan, sha: planSha } = readPlan(ctx.groundPlan);
  const reachOf = (d, s) => (d.status === 'Banked' ? [2 * s.toe[0] - s.brow[0], 2 * s.toe[1] - s.brow[1]] : s.toe);
  const foot = defs.filter(d => d.live).map(d => ({ id: d.id, poly: [...d.stations.map(s => s.brow), ...d.stations.map(s => reachOf(d, s)).reverse()] }));
  const laidBoxes = plan.grading.filter(gr => gr.kind === 'scene_laid' && Array.isArray(gr.box)).map(gr => ({ id: gr.id, box: gr.box }));
  const laidAt = (p) => (laidBoxes.find(l => inBox(p, l.box)) || {}).id || null;
  const coveredBy = (p) => { let best = null, bd = COVER; for (const w of foot) { const dd = toPoly(p, w.poly); if (dd <= bd) { bd = dd; best = w.id; } } return best; };
  let nextId = ctx.firstId;
  const held = [], report = [];
  for (const piece of map.pieces) {
    if (!piece.face) continue;
    const row = { piece, guides: 0, stations: 0, classes: {}, owners: new Set(), features: new Set(), held: [], newStations: [] };
    for (const guide of guides(g, map.lab, piece)) {
      row.guides++;
      const sts = guide.map((p, i) => {
        const fc = faceAt(g, at, p), s = { i, p, fc };
        if (!fc) s.cls = 'none';
        else {
          const pc = map.pieceAt(fc.mid[0], fc.mid[1], 2);
          const cov = coveredBy(fc.mid), built = builtAt(plan, fc.brow), falls = fallsAt(plan, fc.mid);
          if (quayPieces.has(pc) || quayPieces.has(piece.id)) s.cls = 'quay';
          else if (cov) { s.cls = 'covered'; s.owner = cov; }
          else if (built) { s.cls = 'built'; s.owner = built; }
          else if (falls) { s.cls = 'falls'; s.owner = falls; }
          else if (fc.browZ - Math.max(fc.toeZ, tide.neapHigh) < F.MIN_DROP) s.cls = 'seabed';
          else if (!fc.allowed) s.cls = 'away';
          else if (fc.batter < F.MIN_BATTER - 1e-3 && !laidAt(fc.mid)) s.cls = 'bank';
          else { s.cls = 'new'; s.laid = laidAt(fc.mid); }
          const feat = featureAt(plan, fc.mid, fc.aspect); if (feat) row.features.add(feat);
        }
        row.stations++; row.classes[s.cls] = (row.classes[s.cls] || 0) + 1; if (s.owner) row.owners.add(s.owner);
        return s;
      });
      // the batter rung each new station wears: its own and its neighbours' votes (all four rungs, bank included)
      for (const s of sts) if (s.cls === 'new') {
        const votes = {};
        for (let j = Math.max(0, s.i - SMOOTH); j <= Math.min(sts.length - 1, s.i + SMOOTH); j++)
          if (sts[j].cls === 'new') { const b = F.snapBatter(sts[j].fc.batter); votes[b] = (votes[b] || 0) + 1; }
        s.rung = Object.entries(votes).sort((a, b) => b[1] - a[1] || F.BATTERS.indexOf(a[0]) - F.BATTERS.indexOf(b[0]))[0][0];
        row.newStations.push(s);
      }
      // runs of new stations, cut by aspect, rung and the chunk math
      let run = [];
      const flush = () => {
        if (run.length >= 2) {
          const lines = run.map(s => ({ brow: s.fc.brow, toe: s.fc.toe, drop: s.fc.drop, toeZ: s.fc.toeZ }));
          let prev = null;
          for (const [a, z] of C.cutPieces(lines)) {
            if (z - a + 1 < 2) continue;
            const part = run.slice(a, z + 1), id = 'wall.nmc_' + String(nextId++).padStart(3, '0');
            const d = heldDef(id, part, prev, piece, [...row.features][0] || null);
            held.push(d); row.held.push(d.id); prev = d;
          }
        }
        run = [];
      };
      for (const s of sts) {
        const fits = s.cls === 'new' && (!run.length || (run.at(-1).i === s.i - 1 && run.at(-1).fc.aspect === s.fc.aspect && run.at(-1).rung === s.rung));
        if (!fits) flush();
        if (s.cls === 'new') run.push(s);
      }
      flush();
    }
    report.push(row);
  }
  return { held, pieces: report, tide, planSha };
}

function heldDef(id, part, prev, piece, feature) {
  const fc = part.map(s => s.fc), aspect = fc[0].aspect, batter = part[0].rung, baked = F.BAKED.includes(F.BATTERS.indexOf(batter));
  const len = part.length - 1;
  const stations = part.map((s, k) => ({ brow: s.fc.brow, toe: s.fc.toe, drop: s.fc.drop, toeZ: s.fc.toeZ, k, browFrom: 'Measured', toeFrom: 'Measured', rule: 'held' }));
  const why = `Held: a face G3 holds and no wall covers${feature ? `, ${feature}` : ''} (G3 piece ${piece.id}): ${len} m along, brow ` +
    `${range(fc.map(x => x.browZ))} m, toe ${range(fc.map(x => x.toeZ))} m, falling ${range(fc.map(x => x.drop))} m over ` +
    `${range(fc.map(x => x.run))} m (${range(fc.map(x => x.batter), 0)} deg), facing ${aspect}. ` +
    (baked ? `The kit draws ${aspect} ${batter}; ` : `The kit's ${batter} rung is not in the default bake; `) +
    `it stands in no scene until its wall is built.`;
  return { id, name: 'Wall_' + id.slice(-3), status: 'Held', splitFrom: '', follows: prev ? prev.id : '', cls: 'Cliff', aspect, batter, stations,
    faceNeeded: `${aspect} ${batter}${baked ? '' : ' (not in the default bake)'}`, why, piece: piece.id, feature };
}

module.exports = { heldFaces, neapHigh, readPlan, featureAt, builtAt, guides, faceAt, STATION, REACH, COVER };
