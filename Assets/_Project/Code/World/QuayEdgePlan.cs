using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>One part of a quay you can stand on: the deck's world footprint and the height it stands
    /// at. The same two facts a <see cref="StandablePlatform"/> carries, taken as a plain value so the
    /// edge plan can be computed with no scene and no components.</summary>
    public readonly struct QuayDeck
    {
        /// <summary>The standable rectangle, WORLD metres — the platform's own footprint.</summary>
        public readonly Rect Footprint;

        /// <summary>Standing height, metres above chart datum — the platform's own deck elevation.</summary>
        public readonly float Elevation;

        public QuayDeck(Rect footprint, float elevation)
        {
            Footprint = footprint;
            Elevation = elevation;
        }

        public override string ToString() =>
            $"[{Footprint.xMin:0.#}, {Footprint.xMax:0.#}] x [{Footprint.yMin:0.#}, {Footprint.yMax:0.#}] " +
            $"at +{Elevation:0.00} m";
    }

    /// <summary>
    /// A hole deliberately left in a guarded edge — the ramp, the brow, the steps. <b>The gate is
    /// authored where the thing you walk through IS</b>, so the opening and the structure cannot drift
    /// apart.
    ///
    /// <para><see cref="ClearWidthMetres"/> is the opening you GET, not the cut that is made:
    /// <see cref="QuayEdgePlan"/> removes an extra wall thickness because <see cref="PropertyBoundary"/>
    /// overruns each quad's ends by half a thickness (its own corner-sealing rule). Ask for 1.65 m and
    /// 1.65 m of daylight is what the built wall leaves.</para>
    /// </summary>
    public readonly struct EdgeGate
    {
        /// <summary>Where the gate is, in world metres — on the edge it opens.</summary>
        public readonly Vector2 Centre;

        /// <summary>The DAYLIGHT the finished wall must leave here, in metres.</summary>
        public readonly float ClearWidthMetres;

        /// <summary>What walks through it, in words. Not read by any code — it is here because a hole in
        /// a wall that nobody can explain is how a hole gets closed by the next pass.</summary>
        public readonly string Reason;

        public EdgeGate(Vector2 centre, float clearWidthMetres, string reason)
        {
            Centre = centre;
            ClearWidthMetres = Mathf.Max(0f, clearWidthMetres);
            Reason = reason ?? "";
        }

        public override string ToString() =>
            $"{Reason} at ({Centre.x:0.#}, {Centre.y:0.#}), {ClearWidthMetres:0.00} m clear";
    }

    /// <summary>
    /// <b>WHICH EDGES OF A QUAY ARE A FALL — MEASURED, NOT TRACED.</b> Turns a set of standable decks and
    /// the ground under them into the runs <see cref="PropertyBoundary"/> builds walls from.
    ///
    /// <para><b>The rule is one sentence.</b> A metre of deck edge is guarded when the ground just
    /// outside it is <see cref="DefaultFallMetres"/> or more BELOW the deck and nothing else standable is
    /// there. That is all. The landward edges come out unguarded because the yard behind a quay is higher
    /// than the quay; the internal seams where two decks meet come out unguarded because the probe lands
    /// on the other deck; and the corner where a wall stops being a wall lands wherever the terrain
    /// actually puts it.</para>
    ///
    /// <para><b>WHY A RULE RATHER THAN A TRACED LINE.</b> Nine Mile Creek's quay is an L of two fills
    /// whose shared seam, whose notch of open water between the apron and the spit, and whose east end
    /// (guarded below the spit, land above it) are all things a person tracing a polyline gets wrong —
    /// and gets wrong invisibly, because a missing wall looks exactly like a wall you have not walked
    /// into yet. Retune the fills and the walls move with them. Same "one law, two consumers" discipline
    /// the cliff walls and the seabed bake already run on.</para>
    ///
    /// <para><b>The fall test subsumes the water test at a tidal quay.</b> The wading model
    /// (<c>TidalWalkability</c>) already soft-walls water deeper than the owner's wade band, and that is
    /// what stops you swimming out of the harbour. It does NOT stop you walking off a quay, because at
    /// low water the berths under it BARE — there is no depth to read, only a drop. So the number asked
    /// about here is the drop; and anywhere a metre of drop stands over a tidal basin, the water answer
    /// would have said the same thing at half tide.</para>
    ///
    /// <para><b>Pure and deterministic.</b> No components, no scene, no lifecycle: decks in, a ground
    /// sampler in, runs out. The region builder measures the ground off the region's own terrain and the
    /// tests measure it off the same one.</para>
    /// </summary>
    public static class QuayEdgePlan
    {
        /// <summary>How far the ground must drop just outside a deck before that edge is a wall (m).
        /// <para>A metre: over the tallest step-down anyone should take without noticing, and a quarter of
        /// the smallest real quay face here (Nine Mile Creek's is 4.6 m). Between those two numbers there
        /// is nothing to argue about, which is why it is not a tunable.</para></summary>
        public const float DefaultFallMetres = 1f;

        /// <summary>How far outside the deck edge the ground is sampled (m) — the ground is read at every
        /// quarter of this out to it, and the LOWEST reading is the one that answers.
        /// <para><b>THIS MUST CLEAR THE FILL'S FALLOFF.</b> A made-ground quay is authored as a fill with
        /// a soft skirt — Nine Mile Creek's walls ease over 1.2 m — so a probe half a metre out reads the
        /// skirt, finds no drop, and leaves the whole quay unwalled. Two metres is past every skirt on
        /// this coast and still inside the berth line.</para></summary>
        public const float DefaultProbeMetres = 2f;

        /// <summary>How finely the edge is walked (m). Ten centimetres, the same step
        /// <see cref="GroundPolygon.PerimeterRuns"/> walks a fence at: fine enough that a gate lands
        /// within a hand's width of where it was asked for, and the output carries only corners and
        /// transitions rather than a point every step.</summary>
        public const float WalkStepMetres = 0.1f;

        /// <summary>
        /// The radius of the thing that walks (m) — the <c>CircleCollider2D</c> both region builders give
        /// the player.
        ///
        /// <para><b>THIS IS A SECOND COPY OF A NUMBER Core SHOULD OWN.</b> It is written as a literal in
        /// <c>NineMileCreekBuilder</c> and again in <c>PersistentCoreBuilder</c>; a gate sized without it
        /// is a gate the fisher cannot fit through, so the plan needs it too. Its honest home is a Core
        /// walker record that all three read — flagged for <c>lead-architect</c> rather than taken here,
        /// because adding to Core is that role's call and this slice is a wall.</para>
        /// </summary>
        public const float WalkerFootRadiusMetres = 0.35f;

        /// <summary>The daylight a gate needs for a walker to pass without scraping either post: the
        /// thing being walked onto, plus a body either side of it.</summary>
        public static float GateClearFor(float structureWidthMetres) =>
            Mathf.Max(0f, structureWidthMetres) + WalkerFootRadiusMetres * 2f;

        /// <summary>
        /// The guarded edges of <paramref name="decks"/>, as world polylines ready for
        /// <see cref="PropertyBoundary.Configure"/>.
        /// </summary>
        /// <param name="decks">Every standable part of the structure. Their shared seams are FOUND, not
        /// authored — pass the whole quay, not one wall at a time, or the seam between two walls gets
        /// walled.</param>
        /// <param name="groundAt">The region's ground height at a world point, metres above datum
        /// (<c>ITidalTerrain.ElevationAt</c>). Required.</param>
        /// <param name="gates">Holes to leave. May be null.</param>
        /// <param name="wallThicknessMetres">The thickness the walls will be built at — needed so a
        /// gate's DAYLIGHT comes out the width it was asked for.</param>
        public static List<Vector2[]> GuardedRuns(
            IReadOnlyList<QuayDeck> decks,
            Func<Vector2, float> groundAt,
            IReadOnlyList<EdgeGate> gates = null,
            float wallThicknessMetres = PropertyBoundary.DefaultThicknessMetres,
            float fallMetres = DefaultFallMetres,
            float probeMetres = DefaultProbeMetres)
        {
            var runs = new List<Vector2[]>();
            if (decks == null || groundAt == null) return runs;

            for (int d = 0; d < decks.Count; d++)
                WalkDeck(decks, d, groundAt, gates, wallThicknessMetres, fallMetres, probeMetres, runs);

            return runs;
        }

        /// <summary>
        /// Is the edge at <paramref name="on"/>, whose outside lies along <paramref name="outward"/>, a
        /// fall? Public because it IS the rule, and a test that can ask it one point at a time can say
        /// WHERE a wall went missing rather than just that one did.
        ///
        /// <para><b>⚠ THE GROUND IS SAMPLED ALL THE WAY OUT, AND THE LOWEST READING WINS.</b> A single
        /// reading at <paramref name="probeMetres"/> can STRADDLE a trough. Measured at Nine Mile Creek:
        /// between the apron's north edge and the spit the ground runs +3.0 → +0.6 → +2.1 → +3.6 over
        /// four metres, because two fills' skirts pass each other there. One reading two metres out finds
        /// +2.1, calls it a step, and leaves a 2.4 m hole in the quay unwalled — with the wall either side
        /// of it built, which is the most convincing way for this to be wrong.</para>
        ///
        /// <para><b>A sample that lands on another deck ends the question.</b> An edge that leads onto
        /// more quay is the seam between two walls of one structure, not a fall, and walling it fences
        /// the fisher onto whichever half she stepped onto first.</para>
        /// </summary>
        public static bool IsFallEdge(IReadOnlyList<QuayDeck> decks, float deckElevation, Vector2 on,
                                      Vector2 outward, Func<Vector2, float> groundAt,
                                      float fallMetres = DefaultFallMetres,
                                      float probeMetres = DefaultProbeMetres)
        {
            if (decks == null || groundAt == null) return false;

            Vector2 dir = outward.normalized;
            float step = Mathf.Max(0.05f, probeMetres * 0.25f);
            float lowest = float.PositiveInfinity;

            for (float d = step; d <= probeMetres + 1e-4f; d += step)
            {
                Vector2 probe = on + dir * d;
                for (int i = 0; i < decks.Count; i++)
                    if (StandablePlatform.Contains(decks[i].Footprint, probe)) return false;
                lowest = Mathf.Min(lowest, groundAt(probe));
            }

            if (float.IsPositiveInfinity(lowest)) return false;   // nothing sampled: no claim
            return deckElevation - lowest >= fallMetres;
        }

        /// <summary>Does a gate open at <paramref name="p"/>? The cut is the daylight asked for plus one
        /// wall thickness, because <see cref="PropertyBoundary"/> gives every quad half a thickness of
        /// overrun at each end.</summary>
        public static bool InGate(Vector2 p, IReadOnlyList<EdgeGate> gates, float wallThicknessMetres)
        {
            if (gates == null) return false;
            for (int g = 0; g < gates.Count; g++)
            {
                float cut = (gates[g].ClearWidthMetres + Mathf.Max(0f, wallThicknessMetres)) * 0.5f;
                if (Vector2.Distance(p, gates[g].Centre) < cut) return true;
            }
            return false;
        }

        /// <summary>Total metres of blocking edge in a set of runs — the wall bill, and the one number
        /// that says at a glance whether a plan produced anything at all.</summary>
        public static float TotalLength(IReadOnlyList<Vector2[]> runs)
        {
            float total = 0f;
            if (runs == null) return total;
            for (int r = 0; r < runs.Count; r++)
            {
                Vector2[] pts = runs[r];
                if (pts == null) continue;
                for (int i = 0; i + 1 < pts.Length; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
            }
            return total;
        }

        // =============================================================================================
        //  the walk
        // =============================================================================================

        /// <summary>
        /// Walk one deck's rectangle counter-clockwise and emit its guarded stretches.
        ///
        /// <para>The walk is fine-grained but the OUTPUT is not: every step is classified, but only
        /// corners and transitions are emitted. A wall built from a point every ten centimetres is a
        /// thousand ten-centimetre colliders, and <see cref="PropertyBoundary"/> makes one per
        /// segment.</para>
        ///
        /// <para>The last stretch is joined to the first when they meet, or a deck whose only unguarded
        /// edge is its north one comes back as two runs with a seam at the south-west corner — a pinhole
        /// in exactly the place a player pressed into a corner is trying to get through.</para>
        /// </summary>
        static void WalkDeck(IReadOnlyList<QuayDeck> decks, int index, Func<Vector2, float> groundAt,
                             IReadOnlyList<EdgeGate> gates, float thickness, float fallMetres,
                             float probeMetres, List<Vector2[]> into)
        {
            Rect box = decks[index].Footprint;
            if (box.width <= 0f || box.height <= 0f) return;
            float deckElevation = decks[index].Elevation;

            // Counter-clockwise, so rotating the direction a quarter-turn clockwise points OUT.
            var corners = new[]
            {
                new Vector2(box.xMin, box.yMin),
                new Vector2(box.xMax, box.yMin),
                new Vector2(box.xMax, box.yMax),
                new Vector2(box.xMin, box.yMax),
            };

            var current = new List<Vector2>();
            var made = new List<Vector2[]>();
            bool wasGuarded = false;
            Vector2 lastGuarded = Vector2.zero;

            for (int e = 0; e < 4; e++)
            {
                Vector2 a = corners[e], b = corners[(e + 1) % 4];
                Vector2 along = (b - a).normalized;
                Vector2 outward = new Vector2(along.y, -along.x);
                float len = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / WalkStepMetres));

                for (int s = 0; s <= steps; s++)
                {
                    if (e > 0 && s == 0) continue;          // the corner was walked as the last edge's end
                    Vector2 p = Vector2.Lerp(a, b, (float)s / steps);

                    bool guarded = IsFallEdge(decks, deckElevation, p, outward, groundAt, fallMetres, probeMetres)
                                   && !InGate(p, gates, thickness);

                    if (guarded)
                    {
                        bool isCorner = s == 0 || s == steps;
                        if (!wasGuarded || current.Count == 0 || isCorner) AppendIfNew(current, p);
                        lastGuarded = p;
                    }
                    else if (wasGuarded)
                    {
                        // The stretch ends at the last point that was still a fall — usually filler,
                        // which would otherwise never be emitted at all.
                        AppendIfNew(current, lastGuarded);
                        Close(current, made, thickness);
                        current = new List<Vector2>();
                    }

                    wasGuarded = guarded;
                }
            }

            if (wasGuarded) { AppendIfNew(current, lastGuarded); Close(current, made, thickness); }

            if (made.Count >= 2)
            {
                Vector2[] first = made[0], last = made[made.Count - 1];
                if (Vector2.Distance(last[last.Length - 1], first[0]) < 1e-3f)
                {
                    var joined = new List<Vector2>(last);
                    for (int i = 1; i < first.Length; i++) joined.Add(first[i]);
                    made.RemoveAt(made.Count - 1);
                    made[0] = joined.ToArray();
                }
            }

            into.AddRange(made);
        }

        /// <summary>Bank a stretch, unless it is shorter than the wall is thick — a stub that short is one
        /// square collider standing in the open, which reads as a bug and stops nobody.</summary>
        static void Close(List<Vector2> run, List<Vector2[]> into, float thickness)
        {
            if (run.Count < 2) return;
            float len = 0f;
            for (int i = 0; i + 1 < run.Count; i++) len += Vector2.Distance(run[i], run[i + 1]);
            if (len < Mathf.Max(0.01f, thickness)) return;
            into.Add(run.ToArray());
        }

        /// <summary>Append unless it would repeat the point already there: a run carrying the same point
        /// twice becomes a zero-length segment, which builds nothing and reads as a gap.</summary>
        static void AppendIfNew(List<Vector2> list, Vector2 p)
        {
            if (list.Count > 0 && Vector2.Distance(list[list.Count - 1], p) < 1e-4f) return;
            list.Add(p);
        }
    }
}
