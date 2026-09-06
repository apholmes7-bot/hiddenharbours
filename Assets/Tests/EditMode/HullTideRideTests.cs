using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>HULLS RIDE THE TIDE</b> (owner playtest 2026-09-06, Nine Mile Creek north wall: <i>"boats
    /// dont ride the tide"</i>). The moored fleet lay at the same place on a 4.6 m quay face at dead low
    /// spring as it lay at high water, while the float beside it rode.
    ///
    /// <para><b>The float is the ORACLE here, deliberately and throughout.</b> Nothing below asserts a
    /// transcribed number for the rise: every claim is made against
    /// <see cref="FloatingPlatformVisual.ScreenRise"/> over <see cref="FloatingPlatform.DeckElevation"/>,
    /// which is what the harbour's own dock has been riding since it was drawn. A boat tied to a float
    /// has to go up and down with the planks she is made fast to, and the only way to guarantee that is
    /// for both of them to be asking one publisher — so these tests fail if the two ever start
    /// disagreeing, which a pair of transcribed constants could not notice.</para>
    /// </summary>
    public class HullTideRideTests
    {
        /// <summary>A bed far below anything in this region, so the hull under test is unambiguously
        /// AFLOAT at every state of tide and the grounding arm is out of the picture. Grounding has its
        /// own tests below — mixing them is how a rise test quietly becomes a bed test.</summary>
        const float DeepBed = -50f;

        readonly System.Collections.Generic.List<GameObject> _spawned = new();

        [SetUp]
        public void SetUp() => GameServices.Reset();   // no sea wired: the gate-off shape is one of the claims

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            GameServices.Reset();
        }

        GameObject Hull(string name = "hull")
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        /// <summary>A hull of her own — one rider per boat, because the component is
        /// <c>DisallowMultipleComponent</c> and a second one on the same object is not a second boat, it
        /// is a null.</summary>
        HullTideRide Rider(float bakedWaterline = 0f, float draught = 1.4f)
        {
            var rider = Hull().AddComponent<HullTideRide>();
            rider.Configure(bakedWaterline, draught);
            return rider;
        }

        /// <summary>How far the FLOAT's picture moves between two water levels — the oracle. Her draught
        /// and freeboard are the region's own measured ones, and they are DIFFERENT from the hull's on
        /// purpose: while both are afloat the rise is the water's and neither hull form may change
        /// it.</summary>
        static float FloatRiseBetween(float fromWater, float toWater)
        {
            float draught = NineMileCreekQuayFace.BakedRigFloatDraughtMetres;
            float freeboard = NineMileCreekQuayFace.BakedRigFloatFreeboard;
            return FloatingPlatformVisual.ScreenRise(
                       FloatingPlatform.DeckElevation(toWater, DeepBed, draught, freeboard),
                       FloatingPlatform.DeckElevation(fromWater, DeepBed, draught, freeboard));
        }

        // ---- the ride ------------------------------------------------------------------------------

        /// <summary>
        /// ⭐ <b>THE ACCEPTANCE.</b> Spring low, mean, spring high — the rise between each pair is the
        /// float's own, and the two steps are equal, because a tide that lifts a dock lifts the boat tied
        /// to it by the same amount.
        /// </summary>
        [Test]
        public void HerPictureRisesByTheSameAmountTheFloatsDoes_AtEveryStateOfTheTide()
        {
            HullTideRide rider = Rider();

            float low = NineMileCreekMainland.SpringLowWater;
            float high = NineMileCreekMainland.SpringHighWater;
            float mean = NineMileCreekMainland.TideMean;

            float atLow = rider.ScreenRiseAt(low, DeepBed);
            float atMean = rider.ScreenRiseAt(mean, DeepBed);
            float atHigh = rider.ScreenRiseAt(high, DeepBed);

            Assert.That(atMean - atLow, Is.EqualTo(FloatRiseBetween(low, mean)).Within(1e-4f),
                "the flood from spring low to mean does not lift the hull by what it lifts the float — " +
                "a boat lying at the float would slide down her own dock as the tide made");
            Assert.That(atHigh - atMean, Is.EqualTo(FloatRiseBetween(mean, high)).Within(1e-4f),
                "…nor from mean to spring high");

            // …and the two halves of a symmetric tide are the same half, which is the cheapest possible
            // guard against a rise that is a curve instead of a line.
            Assert.That(atHigh - atMean, Is.EqualTo(atMean - atLow).Within(1e-4f),
                "the tide is symmetric about its mean here, so the two steps must be");
        }

        /// <summary>
        /// A metre of tide is a metre of HEIGHT — <c>IsoGround.HeightScale</c> ≈ 0.766 up the screen — and
        /// emphatically not the 0.643 a metre of northward GROUND draws. Nineteen per cent, over a 4.4 m
        /// range that is 0.54 units, and indistinguishable by eye from "the art is a bit off".
        /// </summary>
        [Test]
        public void AMetreOfTideIsAMetreOfHeight_NotAMetreOfNorthing()
        {
            HullTideRide rider = Rider();

            Assert.That(rider.ScreenRiseAt(1f, DeepBed) - rider.ScreenRiseAt(0f, DeepBed),
                        Is.EqualTo(IsoGround.HeightScale).Within(1e-5f));
            Assert.That(IsoGround.HeightScale, Is.Not.EqualTo(IsoGround.GroundDepthScale).Within(0.01f),
                        "if these two ever became the same number this test stopped meaning anything");
        }

        /// <summary>The whole of the owner's complaint, as a number: between the lowest and highest water
        /// this coast reaches, a hull at the wall must travel the tidal range up the face. It travelled 0.
        /// </summary>
        [Test]
        public void OverASpringTideSheTravelsTheWholeRangeUpTheFace()
        {
            HullTideRide rider = Rider();

            float travel = rider.ScreenRiseAt(NineMileCreekMainland.SpringHighWater, DeepBed)
                         - rider.ScreenRiseAt(NineMileCreekMainland.SpringLowWater, DeepBed);
            float range = NineMileCreekMainland.SpringHighWater - NineMileCreekMainland.SpringLowWater;

            Assert.That(travel, Is.EqualTo(range * IsoGround.HeightScale).Within(1e-4f));
            Assert.That(travel, Is.GreaterThan(3f),
                "a fleet that moves less than three units over a 4.4 m tide is a fleet nobody will " +
                "believe floats");
        }

        /// <summary>Her picture is where the builder put it at the level it was struck at — the plan point
        /// is not an offset from anywhere, it IS the answer at that one state of tide.</summary>
        [Test]
        public void AtTheLevelHerPictureWasStruckAt_SheIsExactlyWhereHerBuilderPutHer()
        {
            Assert.That(Rider(bakedWaterline: 0f).ScreenRiseAt(0f, DeepBed), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(Rider(bakedWaterline: 1.3f).ScreenRiseAt(1.3f, DeepBed),
                        Is.EqualTo(0f).Within(1e-6f),
                        "a region whose plan lines were struck at another state of tide says so, and then " +
                        "THAT is the level at which nothing moves");
        }

        // ---- she takes the ground ------------------------------------------------------------------

        /// <summary>
        /// ⭐ <b>A HULL STOPS AT THE BOTTOM.</b> Below the level where her keel meets the bed the water
        /// keeps falling and she does not — which is the same <c>max</c> the float takes the ground by,
        /// and the whole difference between a harbour that dries and one where the boats sink into the
        /// mud.
        /// </summary>
        [Test]
        public void WhenTheEbbSetsHerDown_ThePictureStopsWithHerAndTheWaterGoesOn()
        {
            const float bed = -1.6f;            // the basin's own filled bed
            const float draught = 1.0f;
            HullTideRide rider = Rider(draught: draught);

            float grounds = bed + draught;      // she is on the bottom at and below this water level
            float atGrounding = rider.ScreenRiseAt(grounds, bed);

            Assert.That(rider.ScreenRiseAt(grounds - 0.5f, bed), Is.EqualTo(atGrounding).Within(1e-5f),
                "half a metre of ebb below her grounding level still moved her picture — she is drawn " +
                "sinking into the seabed");
            Assert.That(rider.ScreenRiseAt(grounds - 2f, bed), Is.EqualTo(atGrounding).Within(1e-5f));

            // …and she floats off again on the flood, by the same expression read the other way.
            Assert.That(rider.ScreenRiseAt(grounds + 1f, bed) - atGrounding,
                        Is.EqualTo(IsoGround.HeightScale).Within(1e-5f));
            Assert.IsTrue(TidalRide.IsAground(grounds - 0.01f, bed, draught));
            Assert.IsFalse(TidalRide.IsAground(grounds + 0.01f, bed, draught));
        }

        /// <summary>
        /// The wall berths are DREDGED — a trench cut to carry the deepest resident at dead low spring —
        /// so the fleet the owner was looking at rides the whole tide and never touches. This is the test
        /// that says the grounding arm above is not quietly the answer at Nine Mile Creek.
        /// </summary>
        [Test]
        public void TheWallFleetNeverTakesTheGround_BecauseHerBerthWasDredgedForIt()
        {
            float bed = NineMileCreekMainland.BerthTrenchBedElevation;
            float draught = NineMileCreekMainland.DeepestResidentDraughtMetres;
            float low = NineMileCreekMainland.SpringLowWater;

            Assert.IsFalse(TidalRide.IsAground(low, bed, draught),
                $"the deepest resident draws {draught:0.00} m and her berth's bed is at {bed:0.00} m; at " +
                $"spring low ({low:0.00} m) she would be sitting on it, and the fleet would stop riding " +
                "the bottom half of every tide");

            HullTideRide rider = Rider(draught: draught);
            Assert.That(rider.ScreenRiseAt(NineMileCreekMainland.SpringHighWater, bed)
                        - rider.ScreenRiseAt(low, bed),
                Is.EqualTo((NineMileCreekMainland.SpringHighWater - low) * IsoGround.HeightScale)
                  .Within(1e-4f));
        }

        /// <summary>Open water has no bottom to be shallow over — the same reading
        /// <c>BoatCrossing.DepthAt</c> gives a region with no height map — so a hull out at sea can never
        /// be drawn aground.</summary>
        [Test]
        public void WithNoBedKnown_SheCanNeverTakeTheGround()
        {
            HullTideRide rider = Rider(draught: 1.4f);
            float deepEbb = -100f;
            Assert.That(rider.ScreenRiseAt(deepEbb, float.NegativeInfinity),
                        Is.EqualTo(TidalRide.ScreenRise(deepEbb, 0f)).Within(1e-3f));
            Assert.IsFalse(TidalRide.IsAground(deepEbb, float.NegativeInfinity, 1.4f));
        }

        // ---- one publisher ---------------------------------------------------------------------------

        /// <summary>
        /// ⭐ <b>THE FLOAT AND THE BOAT TIED TO HER ASK ONE PUBLISHER.</b> The float's deck is her
        /// waterline plus her freeboard, and the waterline is the very function a hull rides. Pinned as an
        /// identity across the whole range, including through the grounding, so a change to either one
        /// that the other did not get has nowhere to hide.
        /// </summary>
        [Test]
        public void TheFloatsDeckIsTheHullsWaterlinePlusHerFreeboard_AtEveryLevelAndOverTheBed()
        {
            const float bed = -1.0f, draught = 0.31f, freeboard = 0.40f;
            for (float water = -4f; water <= 4f; water += 0.25f)
            {
                Assert.That(FloatingPlatform.DeckElevation(water, bed, draught, freeboard),
                    Is.EqualTo(TidalRide.Waterline(water, bed, draught) + freeboard).Within(1e-5f),
                    $"at water {water:0.00} the dock and the hull alongside her are riding two rules");
                Assert.That(FloatingPlatform.IsAground(water, bed, draught),
                    Is.EqualTo(TidalRide.IsAground(water, bed, draught)));
            }
        }

        /// <summary>The float's own projection is the hull's — one line, one camera, asked by name rather
        /// than copied.</summary>
        [Test]
        public void TheFloatsScreenRiseIsTheSharedOne()
        {
            for (float m = -3f; m <= 3f; m += 0.5f)
                Assert.That(FloatingPlatformVisual.ScreenRise(m, 0f),
                            Is.EqualTo(TidalRide.ScreenRise(m, 0f)).Within(1e-6f));
        }

        // ---- the gate-off shape ----------------------------------------------------------------------

        /// <summary>
        /// ⚠️ <b>NO SEA, NO RIDE — and this is what keeps every fixture that predates the tide honest.</b>
        /// With no environment service the water reads 0, which is the level a plan point is struck at, so
        /// the rise is EXACTLY zero and <see cref="BoatWaveMotion"/> composes a term that changes nothing.
        /// The established gate-off shape (<c>BoatCleats.ElevationOf</c>,
        /// <c>FloatingPlatform.WaterLevelNow</c>), not a special case.
        /// </summary>
        [Test]
        public void WithNoEnvironmentServiceWired_ThereIsNoTideAndTheRideIsExactlyZero()
        {
            Assert.That(HullTideRide.WaterLevelNow(), Is.EqualTo(0f));
            HullTideRide rider = Rider();
            Assert.That(rider.ScreenRiseNow(), Is.EqualTo(0f).Within(1e-7f));
            Assert.That(rider.BedElevation, Is.EqualTo(float.NegativeInfinity),
                        "no terrain wired is open water, and open water has no bottom");
            Assert.IsFalse(rider.IsAgroundNow());
        }

        // ---- whose draught -----------------------------------------------------------------------------

        /// <summary>
        /// The draught is read off the hull she IS, live — not captured once. The player swaps boats under
        /// this component (<c>BoatController.SetHull</c>), and a draught taken at skin time would leave
        /// her grounding at the last boat's keel.
        /// </summary>
        [Test]
        public void ADraughtIsReadOffTheHullSheIs_AndFollowsAHullSwap()
        {
            GameObject go = Hull("piloted");
            var controller = go.AddComponent<BoatController>();
            var rider = go.AddComponent<HullTideRide>();    // no override: read it off her

            var shallow = ScriptableObject.CreateInstance<BoatHullDef>();
            shallow.DraughtMeters = 0.3f;
            var deep = ScriptableObject.CreateInstance<BoatHullDef>();
            deep.DraughtMeters = 1.4f;
            try
            {
                controller.SetHull(shallow);
                Assert.That(rider.DraughtMetres, Is.EqualTo(0.3f).Within(1e-4f));

                controller.SetHull(deep);
                Assert.That(rider.DraughtMetres, Is.EqualTo(1.4f).Within(1e-4f),
                            "she took a bigger boat and kept the little one's keel");

                // …and the deeper hull grounds first over the same bed, which is what a draught is FOR.
                Assert.IsTrue(TidalRide.IsAground(-1.0f, -2.0f, deep.DraughtMeters));
                Assert.IsFalse(TidalRide.IsAground(-1.0f, -2.0f, shallow.DraughtMeters));
            }
            finally
            {
                Object.DestroyImmediate(shallow);
                Object.DestroyImmediate(deep);
            }
        }

        /// <summary>Art on the water with no hull def behind her (the review moorage's hulls) reads no
        /// draught and therefore never grounds — named rather than guessed at, the same way the float
        /// berth's beam gate admits a sprite-only boat with the gap stated.</summary>
        [Test]
        public void AHullWithNoDefKnownReadsNoDraught_AndSoNeverGrounds()
        {
            var rider = Hull().AddComponent<HullTideRide>();
            Assert.That(rider.DraughtMetres, Is.EqualTo(0f));
            Assert.IsFalse(TidalRide.IsAground(-2.2f, -50f, rider.DraughtMetres));
        }
    }
}
