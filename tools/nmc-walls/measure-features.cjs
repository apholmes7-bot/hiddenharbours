// NMC walls (H3): the measures the handoff asks for, read off G3 beside the inventory: the south shore's bluff, the salt
// pond's berm, the gully, the creek banks, the wharf's west corner, the faces at x -380, Dan's lot, QuayWalls and the
// town river's wall. Runs derive-walls.cjs first (same arguments) and writes measures.md beside its table.
//
//   node tools/nmc-walls/measure-features.cjs --ground-plan <ground/plan.json> [--plan B|A] [--out <dir>]
'use strict';
const fs = require('fs'), path = require('path');
const DW = require('./derive-walls.cjs');
const G = require('./g3.cjs'), S = require('./scene.cjs'), FM = require('./facemap.cjs'), F = require('./faces.cjs'), NF = require('./newfaces.cjs');

const ROOT = path.resolve(__dirname, '../..');
const arg = (k, d) => { const i = process.argv.indexOf(k); return i < 0 ? d : process.argv[i + 1]; };
const OUT = path.resolve(ROOT, arg('--out', 'artifacts/nmc-h3'));
const { plan } = NF.readPlan(arg('--ground-plan', null));
const g = G.field(G.decode(path.join(ROOT, 'Assets/_Project/Data/Terrain/NineMileCreekGround/NineMileCreekGround_Height.png')).codes);
const sceneText = fs.readFileSync(path.join(ROOT, 'Assets/_Project/Scenes/NineMileCreek.unity'), 'utf8');
const map = FM.steepPieces(g), tide = NF.neapHigh();
const fx = (x, n = 2) => (+x).toFixed(n);
const rng = (xs, n = 2) => (xs.length ? `${fx(Math.min(...xs), n)}..${fx(Math.max(...xs), n)}` : '-');
const deg = (r) => r * 180 / Math.PI;
const out = ['# NMC walls on G3: the measures', '', `Neap high ${fx(tide.neapHigh)} m, spring high ${fx(tide.springHigh)} m.`, ''];
const defs = DW.defs, live = defs.filter(d => d.live), held = defs.filter(d => d.status === 'Held');
const lineOf = (d) => d.stations.map(s => s.brow);
const toSeg = (p, a, b) => { const vx = b[0] - a[0], vy = b[1] - a[1], L2 = vx * vx + vy * vy; const t = L2 ? Math.max(0, Math.min(1, ((p[0] - a[0]) * vx + (p[1] - a[1]) * vy) / L2)) : 0; return Math.hypot(p[0] - a[0] - vx * t, p[1] - a[1] - vy * t); };
const toLine = (p, pts) => { let d = Infinity; for (let i = 0; i + 1 < pts.length; i++) d = Math.min(d, toSeg(p, pts[i], pts[i + 1])); return d; };
const zAt = (p) => g.at(p[0], p[1]);

// ---- the bluff: its held Defs, station by station ---------------------------------------------------------------
{
  const bl = held.filter(d => d.feature === "the south shore's bluff");
  const st = bl.flatMap(d => d.stations.map(s => ({ ...s, browZ: s.toeZ + s.drop, run: Math.hypot(s.toe[0] - s.brow[0], s.toe[1] - s.brow[1]) })));
  const bat = st.map(s => deg(Math.atan2(s.drop, s.run)));
  const med = (xs) => { const a = [...xs].sort((p, q) => p - q); return a[a.length >> 1]; };
  out.push('## The south shore\'s bluff', '',
    `${bl.length} held Defs, ${bl[0].id.slice(-3)} to ${bl.at(-1).id.slice(-3)}, ${st.length} stations (a station shared where two meet) from x ${fx(st[0].brow[0], 1)} to ${fx(st.at(-1).brow[0], 1)}, ` +
    `y ${rng(st.map(s => s.brow[1]), 1)}; facing ${[...new Set(bl.map(d => d.aspect))].join(', ')}, wearing ${[...new Set(bl.map(d => d.batter))].join(', ')}.`,
    `- the lip (brow): ${rng(st.map(s => s.browZ))} m, median ${fx(med(st.map(s => s.browZ)))} (the amendment: +6.0, the format's ceiling)`,
    `- the foot (toe): ${rng(st.map(s => s.toeZ))} m, median ${fx(med(st.map(s => s.toeZ)))} (the amendment: +2.25)`,
    `- the fall: ${rng(st.map(s => s.drop))} m, median ${fx(med(st.map(s => s.drop)))} (the amendment: 3.75)`,
    `- the run: ${rng(st.map(s => s.run))} m, median ${fx(med(st.map(s => s.run)))} (the amendment: 3.3)`,
    `- the batter: ${rng(bat, 1)} deg, median ${fx(med(bat), 1)} (the amendment's 3.75 over 3.3: ${fx(deg(Math.atan2(3.75, 3.3)), 1)}); the kit's bank rung is 48 deg, the builder's floor 55`, '');
  // the full section: along each station's normal, the lip (the highest ground within 6 m inland) and the foot (the lowest within 6 m seaward)
  const full = st.map(s => { const L = Math.hypot(s.toe[0] - s.brow[0], s.toe[1] - s.brow[1]), d = [(s.toe[0] - s.brow[0]) / L, (s.toe[1] - s.brow[1]) / L];
    let top = -Infinity, sTop = 0, bot = Infinity, sBot = 0;
    for (let k = -6; k <= L + 6; k += 0.05) { const z = zAt([s.brow[0] + d[0] * k, s.brow[1] + d[1] * k]); if (k <= L / 2 && z > top) { top = z; sTop = k; } if (k >= L / 2 && z < bot) { bot = z; sBot = k; } }
    let s1 = sTop, s2 = sBot;                                     // where the section leaves the lip and reaches the foot, within 5 cm
    for (let k = sTop; k <= sBot; k += 0.05) if (zAt([s.brow[0] + d[0] * k, s.brow[1] + d[1] * k]) >= top - 0.05) s1 = k;
    for (let k = sBot; k >= sTop; k -= 0.05) if (zAt([s.brow[0] + d[0] * k, s.brow[1] + d[1] * k]) <= bot + 0.05) s2 = k;
    return { top, bot, run: s2 - s1 }; });
  out.push(`The full section (the lip within 5 cm of the highest ground 6 m inland, the foot within 5 cm of the lowest 6 m seaward): lip ${rng(full.map(x => x.top))} m (median ${fx(med(full.map(x => x.top)))}), ` +
    `foot ${rng(full.map(x => x.bot))} m (median ${fx(med(full.map(x => x.bot)))}), fall median ${fx(med(full.map(x => x.top - x.bot)))} m over ${fx(med(full.map(x => x.run)))} m. ` +
    "The Defs' brow and toe are where the face is steeper than the 41 deg face floor, as every re-lined station's are: the rounded lip and foot either side are ground.", '');
  const foot = st.map(s => s.toeZ);
  out.push(`At spring high (${fx(tide.springHigh)}) the sea ${Math.min(...foot) < tide.springHigh ? `reaches ${st.filter(s => s.toeZ < tide.springHigh).length} of its feet` : 'reaches none of its feet'}; at neap high (${fx(tide.neapHigh)}) ${st.some(s => s.toeZ < tide.neapHigh) ? 'some' : 'none'}.`, '');
}

// ---- the salt pond and its berm: north-south sections across the pond ---------------------------------------------
{
  const pond = plan.water.ponds.find(p => p.id === 'pond.nmc_salt_pond');
  const xs = []; for (const l of pond.lobes) for (let x = Math.ceil(l.c[0] - l.ax); x <= l.c[0] + l.ax; x += 2) xs.push(x);
  const rows = [];
  for (const x of [...new Set(xs)].sort((a, b) => a - b)) {
    // the pond's floor (its lowest point on the section), then south of it the berm's crest, then the beach
    let yLow = null, zLow = Infinity;
    for (let y = -150; y >= -180; y -= 0.25) { const z = zAt([x, y]); if (z < zLow) { zLow = z; yLow = y; } }
    let yCrest = yLow, zCrest = -Infinity;
    for (let y = yLow; y >= yLow - 25; y -= 0.25) { const z = zAt([x, y]); if (z > zCrest) { zCrest = z; yCrest = y; } }
    const steepest = (y0, y1) => { let m = 0; for (let y = y0; y > y1; y -= 0.25) m = Math.max(m, Math.abs(zAt([x, y]) - zAt([x, y - 0.5])) / 0.5); return deg(Math.atan(m)); };
    rows.push({ x, yLow, zLow, yCrest, zCrest, pondSide: steepest(yLow, yCrest), seaSide: steepest(yCrest, yCrest - 12) });
  }
  const berm = rows.filter(r => r.zCrest > r.zLow + 0.05);
  out.push('## The salt pond\'s berm', '',
    `The plan: still water ${pond.still}, depth ${pond.depthM} m, the berm's crest ${pond.berm.crest} (${pond.berm.id}). G3, ${rows.length} north-south sections every 2 m across the pond's lobes:`,
    `- the pond's floor: ${rng(berm.map(r => r.zLow))} m`,
    `- the berm's crest: ${rng(berm.map(r => r.zCrest))} m at y ${rng(berm.map(r => r.yCrest), 1)}`,
    `- its steepest pitch on the pond's side ${rng(berm.map(r => r.pondSide), 1)} deg, on the sea's side ${rng(berm.map(r => r.seaSide), 1)} deg`,
    `It is ${Math.max(...berm.map(r => Math.max(r.pondSide, r.seaSide))) < F.FACE_FLOOR ? `no face: nowhere steeper than the ${F.FACE_FLOOR} deg face floor, and the ground draws it` : 'steep in places: see the face table'}.`, '');
  const notch = DW.defs.filter(d => d.status === 'Held' && d.feature === "the salt pond's bank");
  for (const d of notch) {
    const s = d.stations;
    out.push(`F4, the pond's east-bank notch near (-211, -164), as drawn: ${d.id} (held), ${s.length} stations from (${fx(s[0].brow[0], 1)}, ${fx(s[0].brow[1], 1)}) to (${fx(s.at(-1).brow[0], 1)}, ${fx(s.at(-1).brow[1], 1)}), ` +
      `facing ${d.aspect}, ${d.faceNeeded}; brow ${rng(s.map(x => x.toeZ + x.drop))} m, toe ${rng(s.map(x => x.toeZ))} m.`);
  }
  out.push('');
}

// ---- the gully: the east coast's access, by the builder's coast line ----------------------------------------------
{
  const ec = plan.coast.east_coast, gully = ec.sections.find(s => s.id === 'coast.nmc_gully'), pts = ec.points_today;
  const along = (s) => { let acc = 0; for (let i = 0; i + 1 < pts.length; i++) { const L = Math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]); if (acc + L >= s) { const t = (s - acc) / L; return [pts[i][0] + (pts[i + 1][0] - pts[i][0]) * t, pts[i][1] + (pts[i + 1][1] - pts[i][1]) * t]; } acc += L; } return pts.at(-1); };
  const a = along(gully.s[0]), b = along(gully.s[1]);
  const ux = b[0] - a[0], uy = b[1] - a[1], L = Math.hypot(ux, uy);
  const inGap = (p) => { const s = ((p[0] - a[0]) * ux + (p[1] - a[1]) * uy) / L; return s > 0 && s < L && Math.abs(((p[0] - a[0]) * uy - (p[1] - a[1]) * ux) / L) <= 12; };
  const standing = live.filter(d => d.stations.some(s => inGap(s.brow))), heldIn = held.filter(d => d.stations.some(s => inGap(s.brow)));
  // the open gap: the longest stretch of the span (by projection) that no live wall's brow within 12 m of the line covers
  const proj = (p) => ((p[0] - a[0]) * ux + (p[1] - a[1]) * uy) / L, lat = (p) => Math.abs(((p[0] - a[0]) * uy - (p[1] - a[1]) * ux) / L);
  const cover = live.flatMap(d => d.stations.filter(s => lat(s.brow) <= 12).map(s => ({ s: proj(s.brow), id: d.id }))).sort((p, q) => p.s - q.s);
  let gapFrom = null, gapTo = null, best = 0;
  for (let i = 0; i + 1 < cover.length; i++) { const g0 = Math.max(0, cover[i].s), g1 = Math.min(L, cover[i + 1].s); if (g1 - g0 > best) { best = g1 - g0; gapFrom = cover[i]; gapTo = cover[i + 1]; } }
  const P = (s) => [a[0] + ux * s / L, a[1] + uy * s / L];
  out.push('## The gully', '', `${gully.id} (${gully.cls}, "${gully.fate}"), s ${gully.s[0]}..${gully.s[1]} m along the builder's coast line: ` +
    `(${fx(a[0], 1)}, ${fx(a[1], 1)}) to (${fx(b[0], 1)}, ${fx(b[1], 1)}), ${fx(gully.s[1] - gully.s[0], 0)} m. Inside its span, within 12 m of the line, stand ` +
    `${standing.length ? standing.map(d => d.id.slice(-3)).join(' ') : 'no live wall'} and ${heldIn.length ? heldIn.map(d => d.id.slice(-3)).join(' ') : 'no held face'}. ` +
    (gapFrom ? `The open gap runs from ${gapFrom.id}'s last brow (${fx(P(gapFrom.s)[0], 1)}, ${fx(P(gapFrom.s)[1], 1)}) to ${gapTo.id}'s first (${fx(P(gapTo.s)[0], 1)}, ${fx(P(gapTo.s)[1], 1)}), ${fx(gapTo.s - gapFrom.s, 1)} m, as built; ` +
    `${standing.length ? `${standing.map(d => d.id.slice(-3)).join(' ')} reaches ${fx(L - Math.max(gapTo.s, 0), 1)} m into the span's far end, as built` : 'no wall reaches into the span'}: it stays open, the one way down.` : 'it stays open.'), '');
}

// ---- the creek banks: each stream, both banks, every 4 m ---------------------------------------------------------
{
  out.push('## The creek banks', '', 'Each stream every 4 m, each bank read 25 m out along the normal: the steepest metre, and the rise to the bank top. A bank steeper than the face floor anywhere is named by the face table\'s piece.', '');
  for (const st of plan.water.streams) {
    if (st.id === 'stream.nmc_town_river_mouth') continue;
    const pts = st.pts, res = { left: [], right: [] }, pieces = new Set();
    for (let i = 0; i + 1 < pts.length; i++) {
      const [ax, ay] = pts[i], [bx, by] = pts[i + 1], L = Math.hypot(bx - ax, by - ay), t = [(bx - ax) / L, (by - ay) / L];
      const w = (Array.isArray(st.w) ? (st.w[i] + st.w[i + 1]) / 2 : st.w || 4) / 2;
      for (let s = 0; s < L; s += 4) {
        const p = [ax + t[0] * s, ay + t[1] * s];
        for (const [side, n] of [['left', [-t[1], t[0]]], ['right', [t[1], -t[0]]]]) {
          let m = 0, top = -Infinity;
          for (let r = w; r <= w + 25; r += 0.5) {
            const q = [p[0] + n[0] * r, p[1] + n[1] * r], z0 = zAt(q), z1 = zAt([q[0] + n[0], q[1] + n[1]]);
            m = Math.max(m, z1 - z0); top = Math.max(top, z1);
            const pc = map.pieceAt(q[0], q[1], 0); if (pc && map.pieces[pc - 1].face) pieces.add(pc);
          }
          res[side].push({ deg: deg(Math.atan(Math.max(0, m))), top });
        }
      }
    }
    const desc = (a) => { const d = a.map(x => x.deg).sort((p, q) => p - q); return `median ${fx(d[d.length >> 1], 0)} deg, steepest ${fx(d.at(-1), 0)} deg; ${a.filter(x => x.deg >= F.FACE_FLOOR).length} of ${a.length} sections over ${F.FACE_FLOOR}`; };
    out.push(`- ${st.name || st.id} (the plan: ${JSON.stringify(st.banks && st.banks.note ? st.banks.note : Object.values(st.banks || {}).map(b => b.note).filter(Boolean).join(' / '))}):`,
      `  left bank ${desc(res.left)}; right bank ${desc(res.right)}; face pieces touched: ${[...pieces].sort((a, b) => a - b).join(' ') || 'none'}`);
  }
  out.push('');
}

// ---- the wharf's west corner: the faces where the quay's west end meets the yard -----------------------------------
{
  const quayRuns = S.boundaryRuns(sceneText, 'QuayWalls');
  const west = Math.min(...quayRuns.flat().map(p => p[0]));
  const near = map.pieces.filter(p => p.face && p.x0 <= west + 6 && p.x1 >= west - 6 && p.y1 >= 40 && p.y0 <= 100);
  out.push('## The wharf\'s west corner (from G3)', '', `QuayWalls' runs reach west to x ${fx(west, 2)}. G3's faces within 6 m of it:`);
  for (const p of near) {
    const quay = quayRuns.some(r => [...map.piecesAlong(r)].includes(p.id));
    out.push(`- piece ${p.id}: x ${fx(p.x0, 1)}..${fx(p.x1, 1)}, y ${fx(p.y0, 1)}..${fx(p.y1, 1)}, z ${fx(p.zMin)}..${fx(p.zMax)}, mean ${fx(p.slopeMean, 1)} deg, facing ${p.aspect}${p.allowed ? '' : ' (away)'}` +
      `${quay ? ': QuayWalls\' runs touch it, the wharf\'s' : ''}`);
  }
  // the E face of the dredged basin at the corner: read its batter on west-east sections
  const sec = [];
  for (let y = 66; y <= 76; y += 2) {
    let best = null;
    for (const fc of F.faces(F.profile(g, [west - 8, y], [1, 0], 0, 16))) if (!best || fc.drop > best.drop) best = fc;
    if (best) sec.push({ y, x: west - 8 + (best.sBrow + best.sToe) / 2, batter: best.batter, zb: best.zBrow, zt: best.zToe });
  }
  if (sec.length) out.push(`West-east sections y 66..76: the face at x ${rng(sec.map(s => s.x), 1)}, ${rng(sec.map(s => s.batter), 0)} deg, brow ${rng(sec.map(s => s.zb))} m, toe ${rng(sec.map(s => s.zt))} m.`);
  out.push('');
}

// ---- x -380, Dan's lot, QuayWalls, the river wall ------------------------------------------------------------------
{
  const atEdge = (d) => d.stations.some(s => s.brow[0] <= -375 || s.toe[0] <= -375);
  const edge = defs.filter(d => d.status !== 'Retired' && atEdge(d));
  out.push('## Faces at x -380 (the region\'s west edge, Rice Point\'s east edge)', '', 'Every wall, live or held, with a station within 5 m of x -380:');
  for (const d of edge) { const ys = d.stations.flatMap(s => [s.brow[1], s.toe[1]]); out.push(`- ${d.id} (${d.status}, ${d.faceNeeded || d.batter}): y ${rng(ys, 1)}, x from ${fx(Math.min(...d.stations.map(s => s.brow[0])), 1)}`); }
  const edgePieces = map.pieces.filter(p => p.face && p.x0 <= -378);
  out.push(`G3's faces that reach x -378 or further west: ${edgePieces.map(p => `piece ${p.id} y ${fx(p.y0, 1)}..${fx(p.y1, 1)}`).join('; ') || 'none'}. Rice Point's doors (y -280..-245, y -196): ` +
    `${edge.some(d => d.stations.some(s => (s.brow[1] >= -280 && s.brow[1] <= -245) || Math.abs(s.brow[1] + 196) <= 5)) ? 'a face meets one' : 'no face of mine meets either'}.`, '');

  const lot = [-204.35, -195.65, -11.5, -4.5], pad = 6;
  const inLot = (p, m) => p[0] >= lot[0] - m && p[0] <= lot[1] + m && p[1] >= lot[2] - m && p[1] <= lot[3] + m;
  const lotDefs = defs.filter(d => d.status !== 'Retired' && d.stations.some(s => inLot(s.brow, pad) || inLot(s.toe, pad)));
  const lotPieces = map.pieces.filter(p => p.face && p.x1 >= lot[0] - pad && p.x0 <= lot[1] + pad && p.y1 >= lot[2] - pad && p.y0 <= lot[3] + pad);
  out.push('## Dan\'s lot (lot.nmc_house_south, footprint only)', '', `x ${lot[0]}..${lot[1]}, y ${lot[2]}..${lot[3]}, with its ${pad} m pad: ` +
    `${lotDefs.length ? 'walls ' + lotDefs.map(d => d.id).join(', ') : 'no wall, live or held'}; ${lotPieces.length ? 'G3 faces ' + lotPieces.map(p => p.id).join(', ') : 'no face of G3'} meets it.`, '');

  const quayRuns = S.boundaryRuns(sceneText, 'QuayWalls');
  const meets = defs.filter(d => d.status !== 'Retired').map(d => ({ d, r: Math.min(...d.stations.flatMap(s => [s.brow, s.toe]).map(p => Math.min(...quayRuns.map(run => toLine(p, run))))) })).filter(x => x.r <= 2);
  out.push('## QuayWalls', '', `${quayRuns.length} runs, left as they are. Walls of mine within 2 m of them: ${meets.length ? meets.map(x => `${x.d.id} (${x.d.status}, ${fx(x.r)} m)`).join(', ') : 'none'}.`, '');

  const rw = plan.grading.find(gr => gr.id === 'fill.nmc_river_wall');
  const rwPiece = map.pieces.filter(p => p.face && NF.builtAt(plan, [(p.x0 + p.x1) / 2, p.y1]) === rw.id);
  out.push('## The town river\'s wall (H7\'s)', '', `${rw.id}: x ${rw.x.join('..')}, top ${rw.top}, area ${JSON.stringify(rw.area)}. G3 draws it as piece ${rwPiece.map(p => `${p.id} (x ${fx(p.x0, 1)}..${fx(p.x1, 1)}, ${fx(p.slopeMean, 1)} deg mean, ${fx(p.drop)} m)`).join(', ')}. ` +
    'No Def of mine stands on it or holds it: it is built masonry, H7\'s.', '');
}

fs.writeFileSync(path.join(OUT, 'measures.md'), out.join('\n') + '\n');
console.log(out.join('\n'));
