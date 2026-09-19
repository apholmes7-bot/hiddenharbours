using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>A placed route end must stand on the floor it names, or the route is refused — loudly, per
    /// link.</b> The reader places a companionway from its opening and a ladder leg from its base;
    /// <see cref="BoatInteriorRouteLanding"/> asks whether each end is somewhere she can stand, in plan
    /// AND in height. Floors here are built the way the importer builds them — levels as the reader
    /// makes them, deck areas through <see cref="DeckArea.From"/>, the one builder — so no test pins a
    /// bake the pipeline does not perform.
    ///
    /// <para>The bar is this file's own: 0.1 m, written here. A guard never asks the code for its
    /// tolerance.</para>
    /// </summary>
    public sealed class BoatInteriorRouteLandingTests
    {
        const float Tolerance = 0.1f;

        static BoatInteriorLevel Level(string id, float x0, float x1, float y0, float y1, float z) =>
            new BoatInteriorLevel
            {
                Id = id,
                SoleZMeters = z,
                Outline = new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) },
            };

        static DeckArea Flat(string id, float x0, float x1, float y0, float y1, float z) =>
            DeckArea.From(id, DeckAreaKind.Deck, new[]
            {
                new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x0, y1, z),
            });

        static BoatInteriorRoute Placed(string id, string from, Vector3 fromPoint, string to, Vector3 toPoint) =>
            new BoatInteriorRoute
            {
                Id = id,
                FromLevel = from,
                ToLevel = to,
                FromPoint = fromPoint,
                ToPoint = toPoint,
                Placed = true,
            };

        // A house sole over a cuddy, the reader fixture's two levels.
        static BoatInteriorLevel[] HouseAndCuddy() => new[]
        {
            Level("house_sole", -1.2f, 1.2f, 0.6f, 2.4f, 1.1f),
            Level("cuddy_sole", -0.9f, 0.9f, 2.5f, 3.6f, 0.4f),
        };

        [Test]
        public void AStairWhoseEndsStandOnTheirFloors_Lands()
        {
            BoatInteriorRoute stair = Placed("house_to_cuddy", "house_sole", new Vector3(-0.1f, 1.9f, 1.1f),
                                             "cuddy_sole", new Vector3(-0.1f, 2.7f, 0.4f));

            var refused = BoatInteriorRouteLanding.Land("interior.test", new[] { stair }, HouseAndCuddy(), null, Tolerance);

            Assert.IsEmpty(refused, string.Join(" | ", refused));
            Assert.IsTrue(stair.Placed, stair.NotPlacedBecause);
            Assert.IsEmpty(stair.NotPlacedBecause);
        }

        [Test]
        public void AnEndHalfAMetreOffItsFloorInHeight_IsRefusedWithTheMetres()
        {
            BoatInteriorRoute stair = Placed("house_to_cuddy", "house_sole", new Vector3(-0.1f, 1.9f, 1.1f),
                                             "cuddy_sole", new Vector3(-0.1f, 2.7f, 0.9f));

            var refused = BoatInteriorRouteLanding.Land("interior.test", new[] { stair }, HouseAndCuddy(), null, Tolerance);

            Assert.AreEqual(1, refused.Count, string.Join(" | ", refused));
            Assert.IsFalse(stair.Placed);
            StringAssert.Contains("its to end", stair.NotPlacedBecause);
            StringAssert.Contains("'cuddy_sole'", stair.NotPlacedBecause);
            StringAssert.Contains("+0.50 m in height", stair.NotPlacedBecause);
            StringAssert.Contains("house_to_cuddy", refused[0]);
            StringAssert.Contains("interior.test", refused[0]);
        }

        /// <summary>
        /// <b>The 53's flybridge ladder (Phase A, A1).</b> Its head names <c>bridge_sole</c> at the
        /// right height, but in plan it stands half a metre aft of that deck — over the aft deck, 0.2 m
        /// lower. The old law checked height alone and passed it; a walk that trusted it would stand her
        /// in the air. The refusal must name the deck, the metres, and what is really under her.
        /// </summary>
        [Test]
        public void ALadderHeadThatNamesOneDeckButStandsOverAnother_IsRefusedNamingBoth()
        {
            DeckArea[] decks =
            {
                Flat("mezzanine", -2f, 2f, -7f, -4f, 1.78f),
                Flat("aft_deck", -2f, 2f, -6.5f, -4.8f, 4.66f),
                Flat("bridge_sole", -1.5f, 1.5f, -4.8f, -1f, 4.86f),
            };
            BoatInteriorRoute ladder = Placed("flybridge_ladder", "mezzanine", new Vector3(1.3f, -5.3f, 1.78f),
                                              "bridge_sole", new Vector3(1.3f, -5.3f, 4.86f));

            var refused = BoatInteriorRouteLanding.Land("interior.sf_conv", new[] { ladder },
                                                        new BoatInteriorLevel[0], decks, Tolerance);

            Assert.AreEqual(1, refused.Count, string.Join(" | ", refused));
            Assert.IsFalse(ladder.Placed);
            string why = ladder.NotPlacedBecause;
            StringAssert.DoesNotContain("its from end", why, "the foot stands on the mezzanine: " + why);
            StringAssert.Contains("its to end", why);
            StringAssert.Contains("'bridge_sole' by 0.50 m in plan", why);
            StringAssert.Contains("'aft_deck' at 4.66 m", why);
        }

        [Test]
        public void AnEndNamingAFloorHerHullDoesNotHave_IsRefused()
        {
            BoatInteriorRoute withNoDeckDef = Placed("boat_deck_ladder", "house_sole", new Vector3(0f, 1f, 1.1f),
                                                     "boat_deck", new Vector3(0f, 1f, 3.5f));
            BoatInteriorRoute withADeckDef = Placed("boat_deck_ladder", "house_sole", new Vector3(0f, 1f, 1.1f),
                                                    "boat_deck", new Vector3(0f, 1f, 3.5f));

            BoatInteriorRouteLanding.Land("interior.test", new[] { withNoDeckDef }, HouseAndCuddy(), null, Tolerance);
            BoatInteriorRouteLanding.Land("interior.test", new[] { withADeckDef }, HouseAndCuddy(),
                                          new[] { Flat("aft_deck", -1f, 1f, -2f, 0f, 1.5f) }, Tolerance);

            Assert.IsFalse(withNoDeckDef.Placed);
            StringAssert.Contains("no deck def", withNoDeckDef.NotPlacedBecause);
            Assert.IsFalse(withADeckDef.Placed);
            StringAssert.Contains("neither a level of this interior nor a deck area", withADeckDef.NotPlacedBecause);
        }

        [Test]
        public void ARouteTheReaderLeftUnplaced_KeepsTheReadersReasonAndIsNotReported()
        {
            var bulkhead = new BoatInteriorRoute
            {
                Id = "cuddy_companionway",
                FromLevel = "house_sole",
                ToLevel = "cuddy_sole",
                Placed = false,
                NotPlacedBecause = "its opening names a bulkhead line (at_y), not a hole with a run to walk",
            };

            var refused = BoatInteriorRouteLanding.Land("interior.test", new[] { bulkhead }, HouseAndCuddy(), null, Tolerance);

            Assert.IsEmpty(refused);
            Assert.IsFalse(bulkhead.Placed);
            StringAssert.Contains("at_y", bulkhead.NotPlacedBecause);
        }

        [Test]
        public void AnUnsetWalkTunable_ReadsAsItsDefault_AndAnOwnerValueStands()
        {
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();
            try
            {
                def.FloorToleranceMetres = 0f;
                def.RouteEndReachMetres = -1f;
                Assert.Greater(def.FloorTolerance, 0f, "an absent tolerance must never read as zero");
                Assert.Greater(def.RouteEndReach, 0f, "an absent reach must never read as zero");

                def.FloorToleranceMetres = 0.25f;
                def.RouteEndReachMetres = 0.6f;
                Assert.AreEqual(0.25f, def.FloorTolerance, 1e-6f);
                Assert.AreEqual(0.6f, def.RouteEndReach, 1e-6f);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }
    }
}
