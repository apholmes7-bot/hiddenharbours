#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE QUAY GETS ITS WALLS.</b> Nine Mile Creek's wharf is an L of made ground standing 4.6 m over
    /// the basin, and until this pass there was nothing on its edges at all: the owner's 2026-08-19 walk
    /// stepped off the quay and kept going.
    ///
    /// <para><b>It does not own a wall system — it uses THE one.</b> The runs come out of
    /// <see cref="QuayEdgePlan"/> and are handed to <see cref="PropertyBoundary"/>, which is the component
    /// PR #604 built expressly so that this pass (and the shipyard's, and the cliffs') would stop growing a
    /// new collider scheme each. A fifth bespoke wall here was the stated rejection criterion.</para>
    ///
    /// <para><b>Nothing here traces a line.</b> Every metre of wall is the answer to one question asked of
    /// the region's own terrain — "is the ground two metres outside this edge a metre or more below the
    /// deck, with nothing standable there?" — so the walls follow the fills. Move
    /// <see cref="NineMileCreekMainland.NorthWallFill"/> and the wall moves with it; dredge the basin and
    /// nothing changes, because a deeper hole is still a hole.</para>
    ///
    /// <para><b>WHAT IS DELIBERATELY LEFT OPEN, AND WHAT IS DELIBERATELY NOT.</b></para>
    /// <list type="bullet">
    /// <item><description><b>The brow gets a gate.</b> Walking down the gangway to the float is ORDINARY
    /// WALKING onto a registered surface, so a wall across its hinge would strand the float. The gate is
    /// <see cref="BrowGateClearMetres"/> — the brow's own 0.95 m plus a body either side, because the brow
    /// is narrower than the fisher and a gate its exact width is a gate she cannot enter.</description></item>
    /// <item><description><b>The boat ramp needs no gate</b> — it is nowhere near a deck. The one
    /// legitimate grade into this water (<see cref="NineMileCreekMainland.BoatRampHeadPos"/> to
    /// <see cref="NineMileCreekMainland.BoatRampToePos"/>) is a REAL SLOPE IN THE TERRAIN out on the
    /// natural shore, 4 m west of the apron's west face. No wall is planned within reach of it and a test
    /// holds that so.</description></item>
    /// <item><description><b>The LADDER gets no gate, on purpose.</b> Climbing down to a boat is a scripted
    /// move that begins from wherever the fisher is STANDING (<c>ControlSwitcher</c> looks for a ladder
    /// within <c>LadderReachMetres</c> of her, then drives her transform), so a wall at the lip does not
    /// prevent boarding — it is not in the way of anything. A hole there WOULD be: the ladder sits at
    /// y = 87 on a face that bares 5.2 m at spring low, so a gate at the ladder is a 4.6 m fall cut into
    /// the exact wall this slice exists to build.</description></item>
    /// <item><description><b>The SLIPWAY gets no gate either, and this one is a finding.</b> The boatyard's
    /// ramp is drawn running east off the apron at y = 52, but it is NOT IN THE TERRAIN — it appears in no
    /// fill, so the ground a metre east of the apron there is the −1.6 m basin, exactly as it is either
    /// side. Gating it would open 8 m of quay onto a drop with no slope under it. It is walled, and the
    /// honest fix if the owner wants to walk it is a fill: the grade must exist before the wall may open
    /// for it.</description></item>
    /// </list>
    /// </summary>
    public static class NineMileCreekWalls
    {
        /// <summary>The object the quay's walls hang on, under the wharf root.</summary>
        public const string RootName = "QuayWalls";

        /// <summary>Carried into the scene on the boundary itself. Not read by code — it is there because
        /// the failure mode is a wall nobody can explain, and the fix starts with knowing who put it
        /// there.</summary>
        public const string BoundaryReason =
            "The quay edge: 4.6 m from the deck to the basin. Gated at the brow only — the ladder is a " +
            "scripted climb that needs no hole, and the slipway has no grade in the terrain to open onto.";

        /// <summary>
        /// The daylight left at the brow's hinge (m): the gangway's own walking width plus a body either
        /// side of it.
        ///
        /// <para><b>Asymmetric with a ramp on purpose.</b> A ramp wide enough to drag a hull up is wider
        /// than the fisher, so its gate is its own width — open it further and you have opened quay, not
        /// ramp. The brow is 0.95 m, NARROWER than she is: a gate cut to the brow's width is a gate she
        /// cannot walk through, so the body is added here and only here.</para>
        /// </summary>
        public static float BrowGateClearMetres =>
            QuayEdgePlan.GateClearFor(NineMileCreekQuayFace.BakedRigGangwayWidthMetres);

        /// <summary>Every hole in the quay's walls. Derived from the structures that walk through them, so
        /// re-siting the brow takes its gate with it.</summary>
        public static List<EdgeGate> Gates() => new List<EdgeGate>
        {
            new EdgeGate(NineMileCreekWharf.GangwayShoreEnd, BrowGateClearMetres,
                         "the brow down to the floating dock"),
        };

        /// <summary>The two fixed decks, each at the height the wharf builder MEASURED for it. The float
        /// and the brow are deliberately absent: their decks ride the tide a foot over the water, so
        /// stepping off one is a wade, not a fall, and the wading model already owns that.</summary>
        public static List<QuayDeck> Decks(ITidalTerrain terrain) => new List<QuayDeck>
        {
            new QuayDeck(NineMileCreekWharf.DeckFootprint(), NineMileCreekWharf.DeckElevationFrom(terrain)),
            new QuayDeck(NineMileCreekWharf.ApronFootprint(), NineMileCreekWharf.ApronElevationFrom(terrain)),
        };

        /// <summary>The quay's guarded edges as world polylines. Pure given a terrain — this is what the
        /// tests measure and what <see cref="Place"/> builds, with nothing in between.</summary>
        public static List<Vector2[]> Runs(ITidalTerrain terrain)
        {
            if (terrain == null) return new List<Vector2[]>();
            return QuayEdgePlan.GuardedRuns(Decks(terrain), terrain.ElevationAt, Gates());
        }

        /// <summary>
        /// Build the quay's walls under <paramref name="parent"/>. Returns the metres of blocking edge
        /// laid — the one number that says at a glance whether the quay got walled at all.
        /// </summary>
        public static float Place(GameObject parent, ITidalTerrain terrain)
        {
            if (terrain == null)
            {
                Debug.LogWarning("[NineMileCreekWalls] no terrain, so no wall: which edges of the quay are " +
                                 "a fall is a question asked of the ground, and there is no ground to ask. " +
                                 "The quay is UNWALLED — you can walk off it.");
                return 0f;
            }

            var go = new GameObject(RootName);
            go.transform.SetParent(parent.transform, worldPositionStays: false);

            List<Vector2[]> runs = Runs(terrain);
            var boundary = go.AddComponent<PropertyBoundary>();
            boundary.Configure(runs, PropertyBoundary.DefaultThicknessMetres, BoundaryReason);

            float metres = boundary.TotalLengthMetres();
            if (boundary.SegmentCount == 0)
                Debug.Log("[NineMileCreekWalls] no wall: nothing outside this quay is a fall. That is the " +
                          "honest answer on a FLAT test terrain (the unit fixtures build the wharf on one), " +
                          "and a RED FLAG in the real region — there it means the decks have moved off the " +
                          "fills that raise them, or the probe no longer clears the fills' falloff. On the " +
                          "authored terrain this lays about 190 m; NineMileCreekWallsTests.TheQuay_IsWalled " +
                          "is what fails if it stops.");
            else
                Debug.Log(
                    $"[NineMileCreekWalls] the quay is walled: {metres:0.#} m of blocking edge in " +
                    $"{runs.Count} run(s), {boundary.SegmentCount} collider(s), " +
                    $"{PropertyBoundary.DefaultThicknessMetres:0.00} m thick. Every metre of it MEASURED — " +
                    $"an edge is a wall where the ground {QuayEdgePlan.DefaultProbeMetres:0.#} m outside it " +
                    $"is {QuayEdgePlan.DefaultFallMetres:0.#} m or more below the deck. Gated at the brow " +
                    $"({BrowGateClearMetres:0.00} m clear at {NineMileCreekWharf.GangwayShoreEnd}) and " +
                    "nowhere else: the ladder is a scripted climb from the deck and the slipway has no " +
                    "grade in the terrain to open onto. The boat ramp is out on the natural shore and no " +
                    "wall goes near it.");

            return metres;
        }
    }
}
#endif
