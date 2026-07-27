using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Which faces of a hull can the SEA see? (ADR 0023 — the per-face interior mask.)</b>
    ///
    /// <para>The displaced sea and the hulls share one z-buffer, and a z-test has no concept of a
    /// shell: a cockpit sole is just geometry under a big enough lift, so the sea drew inside the
    /// boat. The shipped mitigation shoved each whole hull toward the camera until nothing above a
    /// hand-measured deck line could be reached — which failed in two directions at once (owner
    /// playtest 2026-07-25): it guessed the hull's reach from a half-BEAM, so a bow-on hull's stern
    /// lay outside the guarded band and leaked; and closing that gap would have cost another
    /// 0.4–2.5 m of shove on every boat, which is what put the props out of the water. This
    /// classifier replaces the guess with a fact.</para>
    ///
    /// <para><b>The rule: a face is EXTERIOR iff the sea can REACH it</b> — which takes two halves,
    /// because the sea is not light.</para>
    /// <list type="number">
    /// <item><b>It can see it.</b> Render the hull orthographically from many directions strictly
    /// below the horizon, all round, and mark every face frontmost at even one pixel from even one
    /// direction.</item>
    /// <item><b>It can rise to it.</b> The sea has a LEVEL. Any face lying wholly above the hull's
    /// own rail (<see cref="DeriveRailHeight"/>) is out of reach whichever way it faces.</item>
    /// </list>
    ///
    /// <para><b>Visibility alone was tried first, and shipped, and kept failing in new places</b> —
    /// each one needing its own patch. Light gets in through the gap over a gunwale (the level-ray
    /// defect); light reaches the UNDERSIDE of an overhanging washboard and the classifier then
    /// wets its top; light lands on elevated bow decks. The owner named the pattern rather than the
    /// instances (2026-07-26): <i>"fixing one surface at a time feels tedious, there must be a
    /// better solution, like defining the exterior walls of a boat or something."</i> The level is
    /// that better solution: it retires the whole family at once, and it needs no notion of which
    /// SIDE of a face is being looked at — which is fortunate, because the raster takes
    /// <c>Mathf.Abs(area)</c> and could never be given one (winding does not survive mirroring on 7
    /// of the 11 hulls).</para>
    ///
    /// <para>Everything else — cockpit soles, hold floors, inner bulwarks, washboards, foredecks —
    /// is INTERIOR, and the guard pass keeps the water off it per pixel.</para>
    ///
    /// <para>⚠️ <b>The accepted cost.</b> A level cuts both ways: the sea also stops drawing on her
    /// OUTSIDE above the rail, so she no longer takes green water over the topsides in a heavy sea.
    /// Owner-accepted, because a boat that read as swamped was the defect being removed.</para>
    ///
    /// <para><b>It reads no normal, no material and no rig source, and that is deliberate.</b> Each
    /// of those was tried and measured false:</para>
    /// <list type="bullet">
    /// <item><b>Normals do not survive mirroring.</b> Pairing every off-centreline face with its
    /// mirror twin and comparing normals: on 7 of the 11 hulls the MAJORITY of paired faces carry
    /// the shared winding, i.e. one side's normal is the negation of the true outward one. Any rule
    /// containing <c>n.z &gt; 0</c> misclassifies half the planking on most of the fleet.</item>
    /// <item><b>Material names do not separate.</b> The dory's entire palette is
    /// <c>{wood, iron}</c>; the trawler's stern-ramp floor is <c>iron</c>, the same name as her
    /// bollards.</item>
    /// <item><b>The face bias does not either.</b> <c>b &lt;= -1</c> includes the boot stripe — the
    /// most-submerged band on every hull — and excludes every cockpit sole.</item>
    /// <item><b>An inset/envelope threshold is a continuum, not a split.</b> Sweeping it from 0.002
    /// to 0.10 of beam moves the dory from 167 interior faces to 7; no single tolerance works
    /// fleet-wide when one hull's planking is 0.035 m thick and another's beam is 10.4 m.</item>
    /// </list>
    ///
    /// <para><b>The cross-check that says this works.</b> The LOWEST interior face this produces,
    /// from geometry alone, lands exactly on the independently hand-measured
    /// <c>HullMeshDef.WatertightDeckHeightMeters</c> for 9 of the 11 hulls — cape 0.72, console
    /// 0.28, dory 0.06, lobster 0.50, punt 0.06, dragger 2.05, sport skiff 0.28, trawler 1.75,
    /// Mk2 1.75. Two entirely independent derivations, the same numbers.</para>
    ///
    /// <para>⚠️ <b>This is a GOLDEN MASTER, not a formula.</b> 5–30% of the sea-band faces move
    /// under a doubling of the azimuth count. The sampling constants below are committed data:
    /// changing one silently re-classifies the whole fleet at the next bake. A test pins the
    /// resulting per-hull interior counts; treat any change here as a deliberate re-bake with
    /// re-measured pins. The perturbation direction is at least safe-ish — coarser over-classifies
    /// (a dry patch on the planking), finer under-classifies (a small flood).</para>
    /// </summary>
    public static class RigMeshInteriorClassifier
    {
        /// <summary>Azimuths in the sampling ring. Pinned: 32 → 48 moves 5–30% of sea-band faces.</summary>
        public const int Azimuths = 32;

        /// <summary>
        /// Sampling elevations in DEGREES, all STRICTLY BELOW the horizon — the directions the sea
        /// actually occupies. −90 (straight up from underneath) closes the bottom.
        ///
        /// <para>⚠️ <b>NEVER add an elevation of 0 or above.</b> Two separate failures live there:
        /// <list type="bullet">
        /// <item>At +1° an upward-facing deck becomes visible and leaks into EXTERIOR — measured at
        /// 18 (dory) / 18 (lobster) / 44 (packet) faces flipping, i.e. decks becoming floodable
        /// fleet-wide.</item>
        /// <item>At exactly 0 the ray is LEVEL, and a level ray passes over a low gunwale straight
        /// into the boat and lands on the INNER planking of the far side. Sheer curve makes it
        /// routine rather than a graze: freeboard is lowest amidships, so level rays enter there
        /// freely. One frontmost pixel condemns the whole face, so the interior walls were being
        /// flagged EXTERIOR and the sea shaded them (owner playtest 2026-07-26: <i>"it doesnt seem
        /// to affect the lower floor deck though, now its just the interior walls of the boat"</i>).
        /// It survived the deck-height cross-check because that measures the LOWEST interior face,
        /// which the sole satisfies on every hull — the metric is structurally blind to the walls.
        /// Every elevation below 0 looks UPWARD at the hull and can only ever reach her outside,
        /// which is why removing this one ring is surgical.</item>
        /// </list>
        /// <see cref="Classify"/> asserts this rather than trusting the comment.</para>
        /// </summary>
        public static readonly float[] ElevationsDeg = { -8f, -18f, -30f, -45f, -62f, -80f, -90f };

        /// <summary>Raster density in pixels per MODEL METRE. Density, never a fixed pixel count:
        /// a fixed 384 px made the 60 m coastal packet unstable by 30–49 faces run to run.</summary>
        public const int PixelsPerMetre = 16;

        /// <summary>Upper bound on either raster axis, so the tanker cannot allocate unboundedly.</summary>
        public const int MaxResolution = 2048;

        /// <summary>How close to frontmost still counts as visible (metres). Load-bearing: the rigs
        /// paint coplanar decoration over the planking (boot stripe, cove stripe, the
        /// <c>db</c>-biased bands), and with a winner-takes-all test those bands lose the z-fight to
        /// the plank beneath and get flagged INTERIOR — on the dory alone that was 184 wrongly
        /// interior faces against 85 correct ones. 4 mm fixed it.</summary>
        public const float CoplanarEpsMeters = 0.004f;

        // ---- THE RAIL: the sea has a LEVEL, and that is what visibility could never express -----
        //
        // Visibility was always the wrong primitive. Light reaches places water cannot: in through
        // the gap over a gunwale (the level-ray defect above), and onto the UNDERSIDE of any
        // overhanging lip — a washboard rides the sheer outside the station line, so every
        // below-horizon ray from outboard finds its underside, the classifier calls the face
        // wettable on that evidence, and the sea then draws on its TOP. Note the raster takes
        // Mathf.Abs(area): it has no idea which side of a face it is looking at, and cannot be
        // given one, because winding does not survive mirroring on 7 of the 11 hulls.
        //
        // Water is not light. It arrives from outside, and it has a LEVEL it cannot climb above.
        // So the rule gains a second half: a face is exterior iff the sea can see it AND the sea can
        // RISE to it. Everything above the rail is out of the sea's reach whichever way it faces —
        // which retires the whole family of over-the-lip and elevated-deck defects at once instead
        // of one surface at a time (owner, 2026-07-26: "fixing one surface at a time feels tedious,
        // there must be a better solution, like defining the exterior walls of a boat").
        //
        // ⚠️ The deliberate cost: the sea also stops drawing on her OUTSIDE above the rail, so she
        // no longer takes green water over the topsides in a big sea. Owner-accepted — a boat that
        // reads as swamped was the defect being removed.

        /// <summary>Lengthwise buckets used to trace the sheer. Enough to resolve a sweeping sheer
        /// on a 4 m dory without any bucket going empty on a 60 m packet.</summary>
        public const int RailStations = 48;

        /// <summary>A vertex counts as "on the hull's side" at its station when it lies at this
        /// fraction of that station's own half-beam or beyond. Local, never fleet-wide: one hull's
        /// planking is 0.035 m thick and another's beam is 10.4 m, so an absolute inset is a
        /// continuum, not a split (the same reason it failed as a classifier).</summary>
        public const float RailBeamFraction = 0.80f;

        /// <summary>Which point of the per-station sheer becomes the rail. The sea enters at the
        /// LOW point of a sweeping sheer — a Cape drops her freeboard aft for hauling — so this is
        /// a low percentile rather than a mean. Not the raw minimum: that would hand the whole
        /// fleet's rail to one stray vertex at a stem or a transom corner.</summary>
        public const float RailPercentile = 0.15f;

        /// <summary>
        /// The height the sea can rise to before she is swamped, in metres above the keel, measured
        /// off her own geometry (owner's choice 2026-07-26: derive it, do not author it).
        ///
        /// <para>Traces the sheer station by station — at each lengthwise bucket, the highest vertex
        /// still out at that station's own half-beam — then takes a low percentile of those, because
        /// water enters where the sheer is lowest. Cabins, masts and consoles are inset from the
        /// hull side and are excluded by the beam fraction rather than by any name.</para>
        /// </summary>
        public static float DeriveRailHeight(RigMeshData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            float yMin = float.MaxValue, yMax = float.MinValue;
            foreach (var f in data.Faces)
                foreach (var vert in f.V)
                {
                    Vector3 p = vert.ToVector3();
                    if (p.y < yMin) yMin = p.y;
                    if (p.y > yMax) yMax = p.y;
                }
            if (yMax <= yMin) return float.MaxValue;   // degenerate: cap nothing

            // Pass 1: each station's own half-beam. Pass 2: the highest vertex still out at it.
            var halfBeam = new float[RailStations];
            var sheer = new float[RailStations];
            var counted = new int[RailStations];
            for (int i = 0; i < RailStations; i++) sheer[i] = float.MinValue;

            float span = yMax - yMin;
            foreach (var f in data.Faces)
                foreach (var vert in f.V)
                {
                    Vector3 p = vert.ToVector3();
                    int s = Mathf.Clamp((int)((p.y - yMin) / span * RailStations), 0, RailStations - 1);
                    float ax = Mathf.Abs(p.x);
                    if (ax > halfBeam[s]) halfBeam[s] = ax;
                }

            foreach (var f in data.Faces)
                foreach (var vert in f.V)
                {
                    Vector3 p = vert.ToVector3();
                    int s = Mathf.Clamp((int)((p.y - yMin) / span * RailStations), 0, RailStations - 1);
                    if (halfBeam[s] <= 1e-5f) continue;
                    if (Mathf.Abs(p.x) < RailBeamFraction * halfBeam[s]) continue;
                    counted[s]++;
                    if (p.z > sheer[s]) sheer[s] = p.z;
                }

            var sheers = new List<float>();
            for (int i = 0; i < RailStations; i++)
                if (counted[i] >= 3 && sheer[i] > float.MinValue) sheers.Add(sheer[i]);
            if (sheers.Count == 0) return float.MaxValue;

            sheers.Sort();
            int idx = Mathf.Clamp(Mathf.RoundToInt((sheers.Count - 1) * RailPercentile), 0, sheers.Count - 1);
            return sheers[idx];
        }

        /// <summary>
        /// Classify every face of <paramref name="data"/>. Returns one flag per face, in
        /// <c>data.Faces</c> order: true = INTERIOR (the sea must never draw over it).
        ///
        /// <para>Deterministic by construction — single-threaded, fixed iteration order, no
        /// parallelism and no hashing. A bake that is not reproducible is not a golden master.</para>
        /// </summary>
        public static bool[] Classify(RigMeshData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            for (int i = 0; i < ElevationsDeg.Length; i++)
                if (ElevationsDeg[i] >= 0f)
                    throw new InvalidOperationException(
                        $"RigMeshInteriorClassifier: elevation {ElevationsDeg[i]}° is not BELOW the horizon. " +
                        "Above it, the sea sees decks and flags them exterior (fleet-wide flooding); at " +
                        "exactly 0 a level ray clears a low gunwale and flags the INTERIOR WALLS exterior. " +
                        "See the ElevationsDeg doc.");

            // Flatten to triangles once, remembering which face each came from. The builder's fan
            // (v0, vt, vt+1) is reproduced exactly so a triangle's face index is not a guess.
            var triA = new List<Vector3>();
            var triB = new List<Vector3>();
            var triC = new List<Vector3>();
            var triFace = new List<int>();
            int faceIndex = 0;
            foreach (var f in data.Faces)
            {
                for (int t = 1; t + 1 < f.V.Length; t++)
                {
                    triA.Add(f.V[0].ToVector3());
                    triB.Add(f.V[t].ToVector3());
                    triC.Add(f.V[t + 1].ToVector3());
                    triFace.Add(faceIndex);
                }
                faceIndex++;
            }

            var exterior = new bool[faceIndex];
            int triCount = triFace.Count;
            if (triCount == 0) return new bool[faceIndex];   // nothing to see: all "exterior", i.e. inert

            for (int e = 0; e < ElevationsDeg.Length; e++)
            {
                float elev = ElevationsDeg[e] * Mathf.Deg2Rad;
                // At the pole every azimuth is the same view — sample it once, so the straight-up
                // direction does not get 32x the weight (and 31x the cost).
                int azCount = Mathf.Approximately(ElevationsDeg[e], -90f) ? 1 : Azimuths;
                for (int a = 0; a < azCount; a++)
                {
                    float az = (2f * Mathf.PI * a) / Azimuths;
                    // The direction the CAMERA sits in, relative to the hull: rig frame is
                    // x = abeam, y = along the length, z = height above the keel.
                    var eye = new Vector3(
                        Mathf.Cos(elev) * Mathf.Cos(az),
                        Mathf.Cos(elev) * Mathf.Sin(az),
                        Mathf.Sin(elev));
                    MarkVisible(triA, triB, triC, triFace, eye, exterior);
                }
            }

            // ---- the sea's LEVEL: nothing above the rail is in reach, whichever way it faces ----
            // Strictly below, not at: a washboard's own plane sits exactly ON the sheer that defines
            // the rail, and "<=" would leave every one of them wettable — which is the defect.
            float railZ = DeriveRailHeight(data);
            if (railZ < float.MaxValue)
            {
                int fi = 0;
                foreach (var f in data.Faces)
                {
                    if (exterior[fi])
                    {
                        float lowest = float.MaxValue;
                        foreach (var vert in f.V)
                        {
                            float z = vert.ToVector3().z;
                            if (z < lowest) lowest = z;
                        }
                        if (!(lowest < railZ)) exterior[fi] = false;
                    }
                    fi++;
                }
            }

            var interior = new bool[faceIndex];
            for (int i = 0; i < faceIndex; i++) interior[i] = !exterior[i];
            return interior;
        }

        /// <summary>
        /// One orthographic view along <paramref name="eye"/>: rasterise every triangle keeping the
        /// nearest depth, then sweep again and mark every triangle that is frontmost (within
        /// <see cref="CoplanarEpsMeters"/>) at any pixel it covers.
        ///
        /// <para>The TWO-PHASE structure is the point. A single winner-takes-all pass silently
        /// loses every coplanar decoration band to the surface underneath it.</para>
        /// </summary>
        private static void MarkVisible(List<Vector3> ta, List<Vector3> tb, List<Vector3> tc,
                                        List<int> triFace, Vector3 eye, bool[] exterior)
        {
            // An orthonormal basis with `eye` as the depth axis. Depth grows AWAY from the camera,
            // so the nearest surface has the smallest value.
            Vector3 n = eye.normalized;
            Vector3 up = Mathf.Abs(n.z) > 0.9f ? Vector3.up : Vector3.forward;
            Vector3 u = Vector3.Normalize(Vector3.Cross(up, n));
            Vector3 v = Vector3.Cross(n, u);

            int count = triFace.Count;
            float minU = float.MaxValue, maxU = float.MinValue;
            float minV = float.MaxValue, maxV = float.MinValue;
            var pu = new Vector3[count * 3];   // (u, v, depth) per corner, projected once
            for (int i = 0; i < count; i++)
            {
                Vector3 p0 = ta[i], p1 = tb[i], p2 = tc[i];
                pu[i * 3 + 0] = new Vector3(Vector3.Dot(p0, u), Vector3.Dot(p0, v), -Vector3.Dot(p0, n));
                pu[i * 3 + 1] = new Vector3(Vector3.Dot(p1, u), Vector3.Dot(p1, v), -Vector3.Dot(p1, n));
                pu[i * 3 + 2] = new Vector3(Vector3.Dot(p2, u), Vector3.Dot(p2, v), -Vector3.Dot(p2, n));
                for (int k = 0; k < 3; k++)
                {
                    Vector3 q = pu[i * 3 + k];
                    if (q.x < minU) minU = q.x;
                    if (q.x > maxU) maxU = q.x;
                    if (q.y < minV) minV = q.y;
                    if (q.y > maxV) maxV = q.y;
                }
            }

            // Density, not a fixed count — see PixelsPerMetre.
            int w = Mathf.Clamp(Mathf.CeilToInt((maxU - minU) * PixelsPerMetre) + 2, 2, MaxResolution);
            int h = Mathf.Clamp(Mathf.CeilToInt((maxV - minV) * PixelsPerMetre) + 2, 2, MaxResolution);
            float scaleU = (w - 1) / Mathf.Max(maxU - minU, 1e-6f);
            float scaleV = (h - 1) / Mathf.Max(maxV - minV, 1e-6f);

            var depth = new float[w * h];
            for (int i = 0; i < depth.Length; i++) depth[i] = float.MaxValue;

            // ---- phase 1: nearest depth per pixel ----
            for (int i = 0; i < count; i++)
                Raster(pu, i, minU, minV, scaleU, scaleV, w, h, depth, null, 0, triFace, exterior);

            // ---- phase 2: mark everything frontmost within the coplanar epsilon ----
            for (int i = 0; i < count; i++)
                Raster(pu, i, minU, minV, scaleU, scaleV, w, h, depth, depth, 1, triFace, exterior);
        }

        /// <summary>Scanline-rasterise one projected triangle. <paramref name="phase"/> 0 writes the
        /// depth buffer; phase 1 reads it and marks the triangle's FACE exterior where it is
        /// frontmost within the epsilon.</summary>
        private static void Raster(Vector3[] pu, int tri, float minU, float minV,
                                   float scaleU, float scaleV, int w, int h,
                                   float[] depth, float[] read, int phase,
                                   List<int> triFace, bool[] exterior)
        {
            Vector3 a = pu[tri * 3 + 0], b = pu[tri * 3 + 1], c = pu[tri * 3 + 2];
            float ax = (a.x - minU) * scaleU, ay = (a.y - minV) * scaleV;
            float bx = (b.x - minU) * scaleU, by = (b.y - minV) * scaleV;
            float cx = (c.x - minU) * scaleU, cy = (c.y - minV) * scaleV;

            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, Mathf.Min(bx, cx))));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(ax, Mathf.Max(bx, cx))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, Mathf.Min(by, cy))));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(ay, Mathf.Max(by, cy))));
            if (x1 < x0 || y1 < y0) return;

            float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (Mathf.Abs(area) < 1e-9f)
            {
                // Degenerate on screen (edge-on). It occupies no pixel, so it can neither occlude
                // nor be seen from here — correct to skip, and it is why a deck is invisible at
                // exactly elevation 0 rather than by a special case.
                return;
            }
            float inv = 1f / area;

            for (int y = y0; y <= y1; y++)
            {
                float py = y + 0.5f;
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f;
                    float w0 = ((bx - px) * (cy - py) - (by - py) * (cx - px)) * inv;
                    float w1 = ((cx - px) * (ay - py) - (cy - py) * (ax - px)) * inv;
                    float w2 = 1f - w0 - w1;
                    if (w0 < 0f || w1 < 0f || w2 < 0f) continue;

                    float d = w0 * a.z + w1 * b.z + w2 * c.z;
                    int idx = y * w + x;
                    if (phase == 0)
                    {
                        if (d < depth[idx]) depth[idx] = d;
                    }
                    else if (d <= read[idx] + CoplanarEpsMeters)
                    {
                        exterior[triFace[tri]] = true;
                        return;   // one visible pixel is enough; the rest of this triangle is moot
                    }
                }
            }
        }
    }
}
