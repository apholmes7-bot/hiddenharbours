using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE OTTER'S LANDING — is there somewhere to leave an amphibian, and is it a LAUNCH?</b>
    ///
    /// <para>The sibling of <see cref="NineMileCreekTruckParkTests"/>, and it asks one question a truck
    /// never has to answer: a road vehicle needs dry ground, but a machine whose whole point is
    /// changing medium needs dry ground <em>with water at the bottom of it</em>. So the site is
    /// asserted against the built terrain — the same
    /// <see cref="NineMileCreekMainland.ConfigureTerrain"/> the builder calls — rather than against the
    /// arithmetic that chose it.</para>
    ///
    /// <para>Every claim derives from <see cref="NineMileCreekMainland.OtterLandingPos"/> and the boat
    /// ramp's own two ends, never from a coordinate, because the site is a PROPOSAL awaiting the
    /// owner's walk: he must be able to move the ramp, or her, and have this file follow.</para>
    /// </summary>
    public class NineMileCreekOtterLandingTests
    {
        GameObject _terrainGo;
        MainlandTidalTerrain _terrain;

        /// <summary>Her flotation, from the sidecar and pinned there by <c>AmphibiousVehicleTests</c>:
        /// she lifts at draft + hysteresis, which is what <c>VehicleGrounding.IsAfloatAtDepth</c>
        /// compares against.</summary>
        const float FloatsAtDepth = 0.28f + 0.06f;

        /// <summary>The owner's ruled wade ceiling (<c>GameConfig.WadeDepth</c>) — deeper than this and
        /// stepping off is a swim, not a step.</summary>
        const float WadeDepth = 0.5f;

        static Vector2 Landing => NineMileCreekMainland.OtterLandingPos;
        static Vector2 RampHead => NineMileCreekMainland.BoatRampHeadPos;
        static Vector2 RampToe => NineMileCreekMainland.BoatRampToePos;
        static Vector2 DownRamp => (RampToe - RampHead).normalized;

        [SetUp]
        public void SetUp()
        {
            _terrainGo = new GameObject("NineMileCreekMainland_OtterLandingTest");
            _terrain = _terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            GameServices.Reset();
        }

        float Elev(Vector2 p) => _terrain.ElevationAt(p);

        /// <summary>Water depth at a point for a given water level — the same subtraction every depth
        /// gate in the game makes.</summary>
        float Depth(Vector2 p, float waterLevel) => waterLevel - Elev(p);

        // =============================================================================================
        //  1. SHE IS ON DRY GROUND — at the biggest tide there is
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The load-bearing one.</b> A machine found underwater is a machine the player never
        /// finds, and the tide here swings 4.4 m. Asserted at SPRING HIGH, not at mean.
        /// </summary>
        [Test]
        public void SheStandsOnGroundThatIsDryAtSpringHigh()
        {
            float ground = Elev(Landing);
            float high = NineMileCreekMainland.SpringHighWater;

            Assert.That(ground, Is.GreaterThan(high),
                $"the Otter's landing {Landing} is at {ground:0.##} m and spring high water is " +
                $"{high:0.##} m — she is parked in the sea for part of every spring cycle. Move " +
                "NineMileCreekMainland.OtterLandingPos onto higher ground.");
        }

        /// <summary>Not merely dry — dry with room. A site clearing the biggest tide by a few
        /// centimetres is one the owner's next tide-pacing tweak drowns.</summary>
        [Test]
        public void HerGroundClearsTheBiggestTideByAMarginRatherThanAHair()
        {
            float freeboard = Elev(Landing) - NineMileCreekMainland.SpringHighWater;

            Assert.That(freeboard, Is.GreaterThan(0.5f),
                $"her landing clears spring high by only {freeboard:0.##} m. That is inside the noise " +
                "of a tide-amplitude change, and the elevations here are deliberately tide-FRACTIONS " +
                "for exactly that reason.");
        }

        // =============================================================================================
        //  2. IT IS A LAUNCH — the question a truck park never has to answer
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>There is water she can float in, and it is a short drive DOWN THE RAMP.</b> This is
        /// the whole reason the site is the boat ramp rather than any dry rectangle: an amphibian
        /// parked where she cannot reach water is a truck with a silly hull.
        /// </summary>
        [Test]
        public void ThereIsWaterSheFloatsIn_AShortDriveDownTheRamp()
        {
            const float reach = 20f;
            float found = -1f;
            for (float s = 0.5f; s <= reach; s += 0.25f)
            {
                if (Depth(Landing + DownRamp * s, NineMileCreekMainland.TideMean) >= FloatsAtDepth)
                {
                    found = s;
                    break;
                }
            }

            Assert.That(found, Is.GreaterThan(0f),
                $"driving {reach} m down the ramp's axis from {Landing} never reaches water " +
                $"{FloatsAtDepth:0.##} m deep at mean tide, so she cannot launch from here at all. " +
                "That is the one thing this site exists to provide.");

            Assert.That(found, Is.LessThan(12f),
                $"she has to drive {found:0.#} m before she floats — a long way to walk a machine " +
                "down a ramp. The site is further from the water than it looks.");
        }

        /// <summary>
        /// ⭐ <b>The loop closes: somewhere on this ramp she is AFLOAT and the fisher can still step
        /// off into water he can stand in.</b> Those are two different rules — hers is a draft, his is
        /// the wade ceiling — and a launch where they never overlap is one where getting out means
        /// swimming every single time.
        /// </summary>
        [Test]
        public void SomewhereOnTheRampSheFloatsAndTheFisherCanStillWade()
        {
            float found = -1f;
            for (float s = 0f; s <= 20f; s += 0.1f)
            {
                float d = Depth(Landing + DownRamp * s, NineMileCreekMainland.TideMean);
                if (d >= FloatsAtDepth && d <= WadeDepth)
                {
                    found = s;
                    break;
                }
            }

            Assert.That(found, Is.GreaterThanOrEqualTo(0f),
                $"nowhere on the first 20 m of this ramp is the water between {FloatsAtDepth:0.##} m " +
                $"(she floats) and {WadeDepth:0.##} m (he can wade) at mean tide. The face is too " +
                "steep for the two bands to overlap, so every exit here would be a swim.");
        }

        /// <summary>
        /// ⭐⭐ <b>There is water she can float in at EVERY tide, including the lowest one — and this
        /// test is the reason that claim is true rather than plausible.</b>
        ///
        /// <para><b>It failed the first time it ran, asserting the opposite.</b> The obvious reading of
        /// this region is that her ramp dries out: the basin bed is −1.6 m against a spring low of
        /// −2.2 m, and both harbours baring is the ruled character of the place. So the first version
        /// of this test asserted that, and the site's own note said so. The terrain disagreed — there
        /// is 1.47 m at the foot of her ramp at spring low, because her axis crosses a GUT (down to
        /// −3.67 m) before it ever reaches the shoal.</para>
        ///
        /// <para>Kept pointed the same way round: if a re-cut of the basin or the tide ever DOES dry
        /// this out, the machine staged at a launch would have a launch that works for part of the day,
        /// and the owner should hear it from a red test rather than from a walk.</para>
        /// </summary>
        [Test]
        public void ThereIsStillWaterSheFloatsInAtSpringLow()
        {
            float deepest = 0f;
            for (float s = 0f; s <= 24f; s += 0.5f)
            {
                deepest = Mathf.Max(deepest,
                    Depth(Landing + DownRamp * s, NineMileCreekMainland.SpringLowWater));
            }

            Assert.That(deepest, Is.GreaterThan(FloatsAtDepth),
                $"at spring low the deepest water within 24 m down her ramp is {deepest:0.##} m, and " +
                $"she needs {FloatsAtDepth:0.##} m to float. Her launch now dries out for part of " +
                "every spring cycle — which may be acceptable, but it is a change to what the site " +
                "was chosen for and OtterLandingPos should be rewritten to say so.");
        }

        /// <summary>
        /// ⭐ <b>The loop works at the extremes, not just at mean tide.</b> The wade-and-afloat overlap
        /// is asserted at spring low and spring high as well, because that band is NARROW here — the
        /// gut is close in and the face is steep — and a site where it exists only near mean water is
        /// one the player can arrive at and find unusable.
        /// </summary>
        [Test]
        public void SheCanBeSteppedOffAtSpringLowAndAtSpringHighToo()
        {
            foreach (float water in new[] { NineMileCreekMainland.SpringLowWater,
                                            NineMileCreekMainland.SpringHighWater })
            {
                bool found = false;
                for (float s = 0f; s <= 28f && !found; s += 0.05f)
                {
                    float d = Depth(Landing + DownRamp * s, water);
                    found = d >= FloatsAtDepth && d <= WadeDepth;
                }

                Assert.That(found, Is.True,
                    $"at a water level of {water:0.##} m there is nowhere on her ramp where she is " +
                    $"afloat ({FloatsAtDepth:0.##} m) and the fisher can still stand " +
                    $"({WadeDepth:0.##} m). At that tide the launch is drive-in-and-swim-out only.");
            }
        }

        // =============================================================================================
        //  3. SHE IS BESIDE THE RAMP, NOT ON IT — and clear of the working wharf
        // =============================================================================================

        /// <summary>Off the ramp's centre line: hulls are dragged up this, and a machine left across it
        /// is a machine in the way.</summary>
        [Test]
        public void SheStandsBesideTheRampRatherThanOnIt()
        {
            float off = MainlandTidalTerrain.DistanceToSegment(Landing, RampHead, RampToe);

            Assert.That(off, Is.GreaterThan(3f),
                $"she is {off:0.##} m from the boat ramp's centre line — close enough to be parked " +
                "across the thing hulls are hauled up.");

            Assert.That(off, Is.LessThan(12f),
                $"she is {off:0.##} m from the ramp. At that distance she no longer reads as staged " +
                "AT the launch, which is the whole argument for this site.");
        }

        /// <summary>Clear of every working site the wharf plan reserves — by the plan's own rule rather
        /// than by a number restated here.</summary>
        [Test]
        public void SheIsNotStandingOnTheWharfsWorkingGround()
        {
            Assert.That(NineMileCreekMainland.IsWorkingSiteInTheWay(Landing), Is.False,
                $"the Otter's landing {Landing} is inside a working site's reserved ground. The wharf " +
                "is already full — see the yard finding — so she must have her own corner of it.");
        }

        /// <summary>Clear of the creekside buildings too, at the radius the wharf's own sheds keep from
        /// one another.</summary>
        [Test]
        public void SheIsClearOfEveryCreeksideBuilding()
        {
            foreach (Vector3 site in NineMileCreekBuilder.CreeksideBuildingSites)
            {
                float d = Vector2.Distance(Landing, new Vector2(site.x, site.y));
                Assert.That(d, Is.GreaterThan(NineMileCreekMainland.WharfShedRadius * 2f),
                    $"she is {d:0.##} m from a creekside building at {site} — inside the " +
                    $"{NineMileCreekMainland.WharfShedRadius * 2f:0.#} m the wharf's own sites keep " +
                    "from each other, so she is parked in somebody's dooryard.");
            }
        }

        /// <summary>Inside the region — a landing past the edge is somewhere nobody can reach.</summary>
        [Test]
        public void TheLandingLiesInsideTheRegion()
        {
            Assert.That(NineMileCreekRoads.RegionRect().Contains(Landing), Is.True,
                $"the Otter's landing {Landing} is outside the region.");
        }

        // =============================================================================================
        //  4. IT DERIVES — so the owner's walk moves her
        // =============================================================================================

        /// <summary>
        /// ⭐ Her position is the ramp's own geometry plus one published offset, at a right angle to
        /// the ramp's axis. Asserted as a RELATIONSHIP rather than a coordinate, which is what lets the
        /// owner move either end of the ramp and have her follow.
        /// </summary>
        [Test]
        public void HerSpotIsDerivedFromTheRampRatherThanTyped()
        {
            Vector2 fromHead = Landing - RampHead;

            Assert.That(fromHead.magnitude,
                Is.EqualTo(NineMileCreekMainland.OtterLandingBesideRampMetres).Within(1e-3f),
                "she is not the published offset from the ramp head, so the two have drifted apart.");

            Assert.That(Vector2.Dot(fromHead.normalized, DownRamp), Is.EqualTo(0f).Within(1e-3f),
                "her offset from the ramp head is not square to the ramp's axis — she is up or down " +
                "the slope rather than beside it.");
        }

        /// <summary>She is pointed DOWN the ramp: staged to launch, not parked square to the water. Her
        /// nose is local +y, so the heading maps that onto the ramp's descent.</summary>
        [Test]
        public void SheIsPointedDownTheRamp()
        {
            float deg = NineMileCreekMainland.OtterLandingHeadingDegrees;
            Vector2 nose = Quaternion.Euler(0f, 0f, deg) * Vector2.up;

            Assert.That(Vector2.Dot(nose, DownRamp), Is.EqualTo(1f).Within(1e-3f),
                $"her heading {deg:0.#}° points {nose} and the ramp descends {DownRamp} — she is " +
                "staged facing somewhere other than the water.");
        }

        // =============================================================================================
        //  5. THE BUILDER ACTUALLY PLACES HER — scene-wired is not builder-wired
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>The pin that matters.</b> #560 shipped a drive mode reachable in no scene and nothing
        /// failed; the Otter has been usable-and-nowhere since #562. This asserts the placement the
        /// builder now makes — one drivable <c>ParkedVehicle</c>, on the landing's own published
        /// constant, carrying the def #581 baked.
        /// </summary>
        [Test]
        public void TheBuilderStagesADrivableOtterOnTheLandingsOwnConstant()
        {
            GameObject otter = NineMileCreekOtterLanding.Place();
            try
            {
                Assert.IsNotNull(otter,
                    "Place() returned nothing — the Otter's def is missing from " +
                    NineMileCreekOtterLanding.OtterDefPath + ", so the ramp builds empty.");

                Assert.AreEqual(Landing, (Vector2)otter.transform.position,
                    "she is not on the landing's published constant. She must derive from " +
                    "OtterLandingPos so the owner's walk verdict moves her with the ramp.");

                var parked = otter.GetComponent<HiddenHarbours.Vehicles.ParkedVehicle>();
                Assert.IsNotNull(parked, "no ParkedVehicle — she would never skin at play.");
                Assert.IsNotNull(parked.Vehicle, "no def on her — a machine with no identity.");
                Assert.AreEqual("vehicle.otter_8x8", parked.Vehicle.Id,
                    "the ramp carries something other than the Otter.");

                Assert.IsNotNull(parked.Vehicle.Mesh,
                    "her def wears no mesh, so VehicleDef.IsUsable refuses her and she is placed " +
                    "invisible — the state #581 existed to end.");
                Assert.IsTrue(parked.Vehicle.Mesh.Floats,
                    "the machine staged at a LAUNCH reports that she does not float. Her flotation " +
                    "fields are zero — see AmphibiousVehicleTests for where they come from.");

                Assert.IsNotNull(otter.GetComponent<HiddenHarbours.Vehicles.VehicleDoor>(),
                    "no driver's door — parked scenery, not the drivable machine the gap was about.");

                // ⚠️ Top-down world: a Rigidbody2D ships with gravityScale 1 and would pull her south
                // forever. VehicleController.Awake re-zeroes at play; the SERIALIZED state must be
                // zero too, so the committed scene never carries a falling machine.
                Assert.AreEqual(0f, otter.GetComponent<Rigidbody2D>().gravityScale,
                    "her serialized gravityScale is not 0 — in a top-down world that is an amphibian " +
                    "accelerating south through the basin.");
            }
            finally
            {
                if (otter != null) Object.DestroyImmediate(otter);
            }
        }

        /// <summary>The heading actually reaches the transform, not just the constant.</summary>
        [Test]
        public void ThePlacedOtterCarriesTheRampHeading()
        {
            GameObject otter = NineMileCreekOtterLanding.Place();
            try
            {
                Assert.IsNotNull(otter, "no Otter to measure — see the placement test.");

                float want = Quaternion.Euler(0f, 0f, NineMileCreekMainland.OtterLandingHeadingDegrees)
                                       .eulerAngles.z;
                Assert.That(otter.transform.eulerAngles.z, Is.EqualTo(want).Within(1e-2f),
                    "the placed machine does not carry OtterLandingHeadingDegrees — she is staged " +
                    "facing somewhere the ramp does not go.");
            }
            finally
            {
                if (otter != null) Object.DestroyImmediate(otter);
            }
        }

        /// <summary>She has her own stable name, so a rebuild replaces rather than accumulates — and it
        /// is not the truck's, which would have one replace the other.</summary>
        [Test]
        public void HerNameIsStableAndHerOwn()
        {
            Assert.That(NineMileCreekOtterLanding.OtterName, Is.Not.Null.And.Not.Empty,
                "she has no stable name, so a rebuild cannot find the previous one to replace it.");

            Assert.That(NineMileCreekOtterLanding.OtterName,
                Is.Not.EqualTo(NineMileCreekTruckPark.TruckName),
                "the Otter and the Dually share a placed-object name — a rebuild would replace one " +
                "with the other.");
        }
    }
}
