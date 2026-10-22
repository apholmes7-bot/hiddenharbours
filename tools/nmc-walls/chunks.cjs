// NMC walls (H3): StPetersCliffWalls' chunk math over a region's wall Defs, in float32 the way the editor does it, so
// the generator can tell before any editor runs that each Def is one chunk (ChunksOfDefs refuses one the math would cut
// inside), and what each wall's along, row basis and azimuth come to. Whole stations only: no Def here is cut finer.
'use strict';
const f = Math.fround;
const DEG2RAD = f(Math.PI * 2 / 360), RAD2DEG = f(1 / DEG2RAD);              // Mathf.Deg2Rad, Mathf.Rad2Deg
const HEIGHT_SCALE = f(Math.cos(f(40 * DEG2RAD)));                          // SpriteLightMath.HeightScale (camera at 40 deg)
const CHUNK_METRES = 6, TOE_SPAN = 2;                                       // StPetersCliffWalls.ChunkMetres, ChunkToeSpanMetres
const K_EPSILON = 1e-5;                                                     // Vector2.kEpsilon

const sub = (a, b) => [f(a[0] - b[0]), f(a[1] - b[1])];
const mag = (v) => f(Math.sqrt(f(f(v[0] * v[0]) + f(v[1] * v[1]))));
const dist = (a, b) => mag(sub(a, b));                                      // Vector2.Distance
const toeScreenY = (s) => f(s.toe[1] - f(s.drop * HEIGHT_SCALE));           // CliffWallGeometry.ToeScreen(s).y
const surface = (s) => { const run = dist(s.toe, s.brow); return f(Math.sqrt(f(f(run * run) + f(s.drop * s.drop)))); };
function azimuthOfOutward(s) {                                              // AzimuthOf(OutwardPlan(s))
  let d = sub(s.toe, s.brow);
  if (!(f(f(d[0] * d[0]) + f(d[1] * d[1])) > 1e-8)) d = [0, -1];
  else { const m = mag(d); d = m > K_EPSILON ? [f(d[0] / m), f(d[1] / m)] : [0, 0]; }
  if (f(f(d[0] * d[0]) + f(d[1] * d[1])) < 1e-12) return 180;
  const a = f(f(Math.atan2(d[0], d[1])) * RAD2DEG);
  return a < 0 ? f(a + 360) : a;
}
const batterDegrees = (s) => { const run = dist(s.toe, s.brow); return run <= 1e-4 ? 90 : f(f(Math.atan2(Math.max(0, s.drop), run)) * RAD2DEG); };

/** A Def's stations as samples: brow, toe, drop, toe height, all float32. */
const samples = (d) => d.brow.map((b, i) => ({ brow: b.map(f), toe: d.toe[i].map(f), drop: f(d.drop[i]), toeZ: f(d.toeZ[i]) }));
const sameStation = (a, b) => a.brow[0] === b.brow[0] && a.brow[1] === b.brow[1] && a.toe[0] === b.toe[0] && a.toe[1] === b.toe[1] &&
  a.drop === b.drop && a.toeZ === b.toeZ;

/** Where the chunk math would cut inside one Def's stations (its first is the seed): the index of the first station
 *  it would not reach, or -1 where the Def is one chunk. */
function cutInside(st, toeSpan = TOE_SPAN) {
  let run = 0, lo = toeScreenY(st[0]), hi = lo;
  for (let j = 1; j < st.length; j++) {
    run = f(run + dist(st[j - 1].brow, st[j].brow));
    const y = toeScreenY(st[j]), span = f(Math.max(hi, y) - Math.min(lo, y));
    if (j >= 2 && (run >= CHUNK_METRES || span > toeSpan)) return j;
    lo = Math.min(lo, y); hi = Math.max(hi, y);
  }
  return -1;
}

/** Cut a piece's stations where the chunk math would, the boundary station seeding the next (one station shared). */
function cutPieces(st, toeSpan = TOE_SPAN) {
  const out = []; let from = 0;
  for (;;) {
    const part = st.slice(from), j = cutInside(part, toeSpan);
    if (j < 0) { out.push([from, st.length - 1]); return out; }
    out.push([from, from + j - 1]);
    from = from + j - 1;
  }
}

/**
 * ChunksOfDefs over live Defs, chained by follows: runs in the order of their least id, each Def one chunk. Refuses what
 * the C# refuses (a join that is not exact, a Def the math would cut inside, fewer than two stations, a ring, two
 * followers). Returns the chunks in order: { def, along, rows, azimuth, batter, runIndex }.
 */
function chunksOfDefs(defs, toeSpan = TOE_SPAN) {
  const live = new Map();
  for (const d of defs) if (d.live) { if (live.has(d.id)) throw new Error(`two live walls are '${d.id}'`); live.set(d.id, d); }
  const next = new Map(), starts = [];
  for (const d of live.values()) {
    if (!d.follows) { starts.push(d); continue; }
    if (!live.has(d.follows)) throw new Error(`'${d.id}' follows '${d.follows}', which is no live wall`);
    if (next.has(d.follows)) throw new Error(`'${d.id}' and '${next.get(d.follows).id}' both follow '${d.follows}'`);
    next.set(d.follows, d);
  }
  const runs = starts.map(s => { const r = []; for (let d = s; d; d = next.get(d.id)) r.push(d); return r; });
  if (runs.reduce((n, r) => n + r.length, 0) !== live.size) throw new Error('live walls follow one another in a ring');
  const least = (r) => r.map(d => d.id).sort()[0];
  runs.sort((a, b) => (least(a) < least(b) ? -1 : least(a) > least(b) ? 1 : 0));

  const out = [];
  runs.forEach((run, runIndex) => {
    let along = 0, lastTexture = null;
    const first = out.length;
    run.forEach((d, w) => {
      const st = samples(d);
      if (st.length < 2) throw new Error(`'${d.id}' needs two or more stations`);
      if (w > 0 && !sameStation(samples(run[w - 1]).at(-1), st[0])) throw new Error(`'${d.id}' follows '${run[w - 1].id}' but does not start on its last station`);
      if (cutInside(st, toeSpan) >= 0) throw new Error(`the chunk math cuts '${d.id}' inside its own stations (at ${cutInside(st, toeSpan)})`);
      // along: the run's arc length to this chunk's first column (the shared station's along, where it follows)
      let chunkAlong;
      if (w === 0) { chunkAlong = 0; lastTexture = st[0].brow; }
      else chunkAlong = along;
      for (let j = 1; j < st.length; j++) { along = f(along + dist(lastTexture, st[j].brow)); lastTexture = st[j].brow; }
      out.push({ id: d.id, def: d, along: chunkAlong, azimuth: azimuthOfOutward(st[st.length >> 1]), runIndex,
        rowsOwn: st.reduce((m, s) => Math.max(m, surface(s)), 0), batters: st.map(batterDegrees) });
    });
    const rows = out.slice(first).reduce((m, c) => Math.max(m, c.rowsOwn), 0);
    for (let k = first; k < out.length; k++) out[k].rows = rows;
  });
  return out;
}

module.exports = { f, HEIGHT_SCALE, CHUNK_METRES, TOE_SPAN, dist, toeScreenY, surface, batterDegrees, azimuthOfOutward,
  samples, sameStation, cutInside, cutPieces, chunksOfDefs };
