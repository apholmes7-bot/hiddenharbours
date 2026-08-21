using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>The rule that decides where a quay gets a wall.</b> <see cref="QuayEdgePlan"/> exists so that
    /// nobody traces a wall line by hand again — the owner's 2026-08-19 walk found three walls that were
    /// each traced separately and each wrong in its own way. These tests hold the four properties the
    /// rule is worth having for: it walls a drop, it does NOT wall the land behind, it does NOT wall the
    /// seam between two decks of one structure, and a gate leaves exactly the daylight it was asked for.
    ///
    /// <para><b>⚠ Geometry off the run points, never off a physics query</b> — the same discipline
    /// <see cref="PropertyBoundaryTests"/> keeps, and for the same reason: EditMode has no simulation to
    /// lean on, so a test that leans on one passes for the wrong reason.</para>
    /// </summary>
    public class QuayEdgePlanTests
    {
        const float Deck = 3f;          // a deck three metres above datum
        const float Basin = -1.6f;      // …over a basin, the way Nine Mile Creek's actually stands
        const float Yard = 3.6f;        // …with made ground a step HIGHER behind it

        static readonly Rect Square = new Rect(0f, 0f, 20f, 20f);

        static List<QuayDeck> One(Rect r, float elevation = Deck) =>
            new List<QuayDeck> { new QuayDeck(r, elevation) };

        /// <summary>Ground that is basin everywhere: a deck on this is an island, walled all round.</summary>
        static Func<Vector2, float> AllBasin() => _ => Basin;

        /// <summary>Basin everywhere EXCEPT north of <paramref name="northOf"/>, which is yard.</summary>
        static Func<Vector2, float> YardToTheNorth(float northOf) =>
            p => p.y > northOf ? Yard : Basin;

        /// <summary>How far <paramref name="p"/> is from the nearest metre of planned wall.</summary>
        static float DistanceToWall(IReadOnlyList<Vector2[]> runs, Vector2 p)
        {
            float best = float.PositiveInfinity;
            for (int r = 0; r < runs.Count; r++)
            {
                Vector2[] pts = runs[r];
                for (int i = 0; i + 1 < pts.Length; i++)
                    best = Mathf.Min(best, GroundPolygon.DistanceToSegment(p, pts[i], pts[i + 1]));
            }
            return best;
        }

        // =========================================================================================
        //  the rule
        // =========================================================================================

        [Test]
        public void ADeckOverAHole_IsWalledAllRound()
        {
            var runs = QuayEdgePlan.GuardedRuns(One(Square), AllBasin());

            Assert.AreEqual(1, runs.Count,
                "a deck with a drop on every side is ONE closed run, not four. Four means the walk did " +
                "not join its last stretch to its first, and the seam is a pinhole at the corner a player " +
                "pressed into a corner is trying to get through.");
            Assert.AreEqual(Square.width * 2f + Square.height * 2f, QuayEdgePlan.TotalLength(runs), 0.25f,
                "the wall bill is the perimeter when every side is a fall.");
        }

        [Test]
        public void TheLandwardEdge_IsLeftOpen_SoTheQuayCanBeReached()
        {
            // Yard from the deck's north edge outward: the probe lands on ground HIGHER than the deck,
            // which is a step up, not a fall.
            var runs = QuayEdgePlan.GuardedRuns(One(Square), YardToTheNorth(Square.yMax));

            for (float x = Square.xMin + 1f; x <= Square.xMax - 1f; x += 2f)
                Assert.Greater(DistanceToWall(runs, new Vector2(x, Square.yMax)), 0.5f,
                    $"there is a wall on the LANDWARD edge at x={x:0.#}. Walling the way onto a quay " +
                    "fences the fisher off it entirely — the rule must read the ground, and the ground " +
                    "behind this edge is higher than the deck.");

            // …and the three water sides still are walled, or the test above passed by walling nothing.
            Assert.Less(DistanceToWall(runs, new Vector2(Square.center.x, Square.yMin)), 0.2f,
                "SABOTAGE: the seaward edge came back unwalled too, so the assertion above measured an " +
                "empty plan rather than a rule that can tell land from water.");
            Assert.Less(DistanceToWall(runs, new Vector2(Square.xMin, Square.center.y)), 0.2f);
            Assert.Less(DistanceToWall(runs, new Vector2(Square.xMax, Square.center.y)), 0.2f);
        }

        [Test]
        public void TheSeamBetweenTwoDecksOfOneQuay_IsNotWalled()
        {
            // An L, the shape Nine Mile Creek's quay actually is: a wall running east, and an apron
            // hanging south off its west end. Where they overlap is not an edge of anything.
            var decks = new List<QuayDeck>
            {
                new QuayDeck(new Rect(0f, 40f, 80f, 10f), Deck),   // the north wall
                new QuayDeck(new Rect(0f, 0f, 10f, 45f), Deck),    // the apron, overlapping it
            };
            var runs = QuayEdgePlan.GuardedRuns(decks, AllBasin());

            // Straight through the seam — the apron's north edge under the wall, and the wall's south
            // edge over the apron.
            Assert.Greater(DistanceToWall(runs, new Vector2(5f, 45f)), 0.5f,
                "the apron's north edge is WALLED where the north wall stands on it — a wall across the " +
                "seam fences the fisher onto whichever half of the quay she stepped onto first.");
            Assert.Greater(DistanceToWall(runs, new Vector2(5f, 40f)), 0.5f,
                "the north wall's south edge is WALLED where the apron continues under it, same fault.");

            // …but the mooring face east of the apron, and the apron's own faces, are walled.
            Assert.Less(DistanceToWall(runs, new Vector2(40f, 40f)), 0.2f,
                "SABOTAGE: the mooring face east of the apron came back unwalled, so the seam assertions " +
                "above measured a plan that walls nothing at all.");
            Assert.Less(DistanceToWall(runs, new Vector2(10f, 20f)), 0.2f);
            Assert.Less(DistanceToWall(runs, new Vector2(0f, 20f)), 0.2f);
        }

        // =========================================================================================
        //  gates
        // =========================================================================================

        [Test]
        public void AGate_LeavesExactlyTheDaylightItAskedFor()
        {
            const float Clear = 1.65f;
            const float Thickness = PropertyBoundary.DefaultThicknessMetres;
            var gate = new EdgeGate(new Vector2(Square.xMax, 10f), Clear, "a brow");

            var runs = QuayEdgePlan.GuardedRuns(One(Square), AllBasin(), new[] { gate }, Thickness);

            // Build it for real: the daylight is a property of the COLLIDERS, and PropertyBoundary
            // overruns each quad's ends by half a thickness, so a plan that cut only `Clear` would leave
            // `Clear - Thickness` of hole.
            var go = new GameObject("GateProof");
            try
            {
                go.AddComponent<PropertyBoundary>().Configure(runs, Thickness, "test");
                Transform walls = go.transform.Find(PropertyBoundary.CollidersChildName);
                Assert.IsNotNull(walls, "the boundary built nothing, so there is no daylight to measure");

                float halfClear = Clear * 0.5f;
                foreach (var col in walls.GetComponents<PolygonCollider2D>())
                {
                    Vector2[] path = col.GetPath(0);
                    foreach (Vector2 v in path)
                    {
                        // Only the gate's own edge matters: a wall on the far side of the deck is 20 m away.
                        if (Mathf.Abs(v.x - Square.xMax) > Thickness) continue;
                        Assert.GreaterOrEqual(Mathf.Abs(v.y - gate.Centre.y), halfClear - 1e-3f,
                            $"a wall corner at {v} reaches inside the {Clear:0.00} m gate centred on " +
                            $"{gate.Centre}. The cut must be the daylight PLUS one thickness, because " +
                            "every quad overruns its ends by half of one.");
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }

            // …and the gate did not simply delete the edge: there is wall either side of it.
            Assert.Less(DistanceToWall(runs, new Vector2(Square.xMax, 3f)), 0.2f,
                "SABOTAGE: the whole east face vanished, so the daylight above was the absence of a wall " +
                "rather than a gate in one.");
            Assert.Less(DistanceToWall(runs, new Vector2(Square.xMax, 17f)), 0.2f);
        }

        [Test]
        public void GateClearFor_AddsABodyEitherSideOfTheStructure()
        {
            Assert.AreEqual(0.95f + QuayEdgePlan.WalkerFootRadiusMetres * 2f,
                            QuayEdgePlan.GateClearFor(0.95f), 1e-4f,
                "a gate cut to the exact width of a 0.95 m brow is a gate the fisher (0.70 m across) " +
                "cannot walk through.");
        }

        // =========================================================================================
        //  the traps
        // =========================================================================================

        [Test]
        public void AProbeInsideTheFillsSkirt_LeavesTheQuayUNWALLED()
        {
            // ⚠ THE TRAP THE DEFAULT PROBE DISTANCE EXISTS FOR. Made ground is authored as a fill with a
            // soft skirt, so the ground just outside a quay is still nearly deck height. Probe inside the
            // skirt and the rule finds no drop anywhere and the quay ships unwalled — which looks exactly
            // like a quay you have not walked off yet.
            const float Skirt = 1.2f;
            Func<Vector2, float> skirted = p =>
            {
                float outside = Mathf.Max(Square.xMin - p.x, p.x - Square.xMax,
                                          Square.yMin - p.y, p.y - Square.yMax);
                if (outside <= 0f) return Deck;
                return outside >= Skirt ? Basin : Mathf.Lerp(Deck, Basin, outside / Skirt);
            };

            var tooClose = QuayEdgePlan.GuardedRuns(One(Square), skirted, null,
                                                    PropertyBoundary.DefaultThicknessMetres,
                                                    QuayEdgePlan.DefaultFallMetres, probeMetres: 0.2f);
            Assert.AreEqual(0f, QuayEdgePlan.TotalLength(tooClose), 1e-3f,
                "a fifth-of-a-metre probe reads the skirt, not the drop — if this ever comes back walled, " +
                "the trap has changed shape and the default probe distance needs re-measuring.");

            var clear = QuayEdgePlan.GuardedRuns(One(Square), skirted);
            Assert.Greater(QuayEdgePlan.TotalLength(clear), Square.width * 3f,
                $"the default {QuayEdgePlan.DefaultProbeMetres:0.#} m probe must clear a {Skirt:0.#} m " +
                "skirt and find the drop. It did not, so every quay in the game is about to ship unwalled.");
        }

        [Test]
        public void AStepDown_IsNotAFall()
        {
            // Half a metre below the deck all round: a kerb, not a quay face. Nothing to wall.
            var runs = QuayEdgePlan.GuardedRuns(One(Square), _ => Deck - 0.5f);
            Assert.AreEqual(0f, QuayEdgePlan.TotalLength(runs), 1e-3f,
                $"a {0.5f:0.0} m step is under the {QuayEdgePlan.DefaultFallMetres:0.#} m fall threshold " +
                "and must not grow a wall — walls around every kerb in the region is how a world stops " +
                "being walkable.");
        }

        [Test]
        public void ThePlanCarriesCornersRatherThanAPointEveryStep()
        {
            var runs = QuayEdgePlan.GuardedRuns(One(Square), AllBasin());
            int points = 0;
            foreach (Vector2[] r in runs) points += r.Length;

            Assert.LessOrEqual(points, 8,
                $"the plan emitted {points} points for a rectangle. The walk steps every " +
                $"{QuayEdgePlan.WalkStepMetres:0.00} m but must emit only corners and transitions — " +
                "PropertyBoundary makes one collider per segment, so a point every step is 800 colliders " +
                "for one deck (rule 7).");
        }

        [Test]
        public void NoTerrain_MeansNoClaim_RatherThanAWallEverywhere()
        {
            Assert.AreEqual(0, QuayEdgePlan.GuardedRuns(One(Square), null).Count);
            Assert.AreEqual(0, QuayEdgePlan.GuardedRuns(null, AllBasin()).Count);
        }
    }
}
