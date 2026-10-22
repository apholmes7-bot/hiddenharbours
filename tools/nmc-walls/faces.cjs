// NMC walls (H3): faces on G3 along a line, and the kit's aspect and batter tables. A face is a maximal run of the profile
// steeper than a floor angle that falls at least MinFaceDrop; its brow is where the run starts (seaward of the land), its
// toe where it ends.
'use strict';
const STEP = 0.05;                    // the profile's step (m)
const MIN_DROP = 1;                   // StPetersCliffWalls.MinFaceDropMetres
const BATTERS = ['wall', 'steep', 'ramp', 'bank'], ANGLES = [90, 76, 62, 48];      // CliffCatalog.Batters, BatterAngles
const BAKED = [0, 1, 2];                                                           // CliffBaker.DefaultBatters
const MIN_BATTER = ANGLES[2] - (ANGLES[2] - ANGLES[3]) / 2;   // 55: StPetersCliffWalls.MinBatterDegrees, the builder's law
const FACE_FLOOR = ANGLES[3] - (ANGLES[2] - ANGLES[3]) / 2;   // 41: the same construction one rung down, half a step under bank
const ASPECTS = ['W', 'SW', 'S', 'SE', 'E'], AZIMUTHS = [270, 225, 180, 135, 90];  // CliffCatalog.Aspects, AspectAzimuths
const ASPECT_ARC = 22.5;              // half the step between two aspects: the aspect law
const deg = (r) => r * 180 / Math.PI;

/** G3 from p along the unit direction d, for s in [s0, s1]. */
function profile(g, p, d, s0, s1) {
  const n = Math.round((s1 - s0) / STEP), s = new Float64Array(n + 1), z = new Float64Array(n + 1);
  for (let i = 0; i <= n; i++) { s[i] = s0 + i * STEP; z[i] = g.at(p[0] + d[0] * s[i], p[1] + d[1] * s[i]); }
  return { s, z };
}

/** The faces along a profile: runs falling faster than tan(floorDeg), merged across a flatter gap shorter than
 *  mergeMetres where the ground keeps falling. */
function faces(pr, floorDeg = FACE_FLOOR, mergeMetres = 0.3) {
  const t = Math.tan(floorDeg * Math.PI / 180), out = [], n = pr.s.length;
  let i = 0;
  while (i < n - 1) {
    if ((pr.z[i] - pr.z[i + 1]) / STEP < t) { i++; continue; }
    let j = i;
    for (;;) {
      while (j < n - 1 && (pr.z[j] - pr.z[j + 1]) / STEP >= t) j++;
      let k = j;
      while (k < n - 1 && (pr.z[k] - pr.z[k + 1]) / STEP < t && pr.z[k + 1] <= pr.z[k] + 1e-6 && (k - j) * STEP < mergeMetres) k++;
      if (k < n - 1 && k > j && (pr.z[k] - pr.z[k + 1]) / STEP >= t && (k - j) * STEP < mergeMetres) { j = k; continue; }
      break;
    }
    const drop = pr.z[i] - pr.z[j], run = (j - i) * STEP;
    if (drop >= MIN_DROP) out.push({ sBrow: pr.s[i], sToe: pr.s[j], zBrow: pr.z[i], zToe: pr.z[j], drop, run, batter: deg(Math.atan2(drop, run)) });
    i = j + 1;
  }
  return out;
}

/** The catalog batter nearest an angle, out of the given indices (all four by default). */
function snapBatter(batterDeg, among = [0, 1, 2, 3]) {
  let best = among[0];
  for (const k of among) if (Math.abs(batterDeg - ANGLES[k]) < Math.abs(batterDeg - ANGLES[best])) best = k;
  return BATTERS[best];
}
/** Compass azimuth (N = 0, clockwise) of a plan direction: atan2(east, north), as CliffWallGeometry.AzimuthOf. */
function azimuthOf(d) { let a = deg(Math.atan2(d[0], d[1])); if (a < 0) a += 360; return a; }
/** The kit aspect nearest an azimuth, how far off it is, and whether the aspect law allows it. */
function aspectOf(az) {
  let best = -1, e = 1e9;
  for (let k = 0; k < AZIMUTHS.length; k++) { const x = Math.abs(((az - AZIMUTHS[k] + 540) % 360) - 180); if (x < e) { e = x; best = k; } }
  return { aspect: ASPECTS[best], off: e, allowed: e <= ASPECT_ARC };
}

module.exports = { STEP, MIN_DROP, BATTERS, ANGLES, BAKED, MIN_BATTER, FACE_FLOOR, ASPECTS, AZIMUTHS, ASPECT_ARC,
  profile, faces, snapBatter, azimuthOf, aspectOf };
