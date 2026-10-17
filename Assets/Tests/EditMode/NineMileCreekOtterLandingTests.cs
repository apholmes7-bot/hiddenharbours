using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE OTTER, PARKED SAFE — is the amphibian left somewhere she cannot come to harm?</b>
    ///
    /// <para>The sibling of <see cref="NineMileCreekTruckParkTests"/>. She was staged beside the boat
    /// ramp, pointed down it, and this file proved the launch. G3 has no boat ramp, and the owner ruled
    /// (2026-10-15): "Just place the otter somewhere safe for now. Finishing the scene is more
    /// important". So this file proves the park instead: her spot
    /// (<see cref="NineMileCreekGroundPlan.OtterParkPos"/>) and her heading
    /// (<see cref="NineMileCreekGroundPlan.OtterParkHeadingDegrees"/>) against the scene's own ground
    /// (G3, <see cref="NineMileCreekLiveGround.Plan"/>), the roads the scene paves, the wharf's working
    /// ground and the region. Her launch is a later wave's, and so are its tests.</para>
    ///
    /// <para>Every claim reads the plan's two constants, never a coordinate, so moving her is a change
    /// to those and this file follows.</para>
    /// </summary>
    public class NineMileCreekOtterLandingTests
    {
        /// <summary>Round her: her collider's far corner is 1.8 m from her origin
        /// (<c>Otter8x8VehicleMesh</c>: x ±0.83, y −1.48 to +1.62), so 10 m is her, room to walk round
        /// her to the door, and room to spare.</summary>
        const float DryRoundHerMetres = 10f;

        /// <summary>Ahead of her nose: a forward drive from the park stays on dry yard at least this far.</summary>
        const float DryAheadMetres = 20f;

        /// <summary>Off a paved way's edge or a pad: her far corner and a lane to pass her.</summary>
        const float ClearOfThePavingMetres = 4f;

        static Vector2 Park => NineMileCreekGroundPlan.OtterParkPos;

        /// <summary>Her nose: local +y turned by her heading.</summary>
        static Vector2 Nose
        {
            get
            {
                float rad = NineMileCreekGroundPlan.OtterParkHeadingDegrees * Mathf.Deg2Rad;
                return new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
            }
        }

        static float Elev(Vector2 p) => NineMileCreekLiveGround.Plan.ElevationAt(p);

        // =============================================================================================
        //  1. SHE IS ON DRY GROUND — at the biggest tide there is, and all round her
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The load-bearing one.</b> A machine found underwater is a machine the player never
        /// finds, and the tide here swings 4.4 m. Asserted at SPRING HIGH, not at mean.
        /// </summary>
        [Test]
        public void SheStandsOnGroundThatIsDryAtSpringHigh()
        {
            float ground = Elev(Park);
            float high = NineMileCreekMainland.SpringHighWater;

            Assert.That(ground, Is.GreaterThan(high),
                $"the Otter's park {Park} is at {ground:0.##} m on G3 and spring high water is " +
                $"{high:0.##} m — she is parked in the sea for part of every spring cycle. Move " +
                "NineMileCreekGroundPlan.OtterParkPos onto higher ground.");
        }

        /// <summary>Not merely dry — dry with room. A site clearing the biggest tide by a few
        /// centimetres is one the owner's next tide-pacing tweak drowns.</summary>
        [Test]
        public void HerGroundClearsTheBiggestTideByAMarginRatherThanAHair()
        {
            float freeboard = Elev(Park) - NineMileCreekMainland.SpringHighWater;

            Assert.That(freeboard, Is.GreaterThan(0.5f),
                $"her park clears spring high by only {freeboard:0.##} m. That is inside the noise " +
                "of a tide-amplitude change.");
        }

        /// <summary>
        /// ⭐ <b>Safe is a disc, not a point.</b> Every metre round her is above spring high: no step
        /// face, slip or drain she can be nudged into, and dry ground to walk round her to her door.
        /// </summary>
        [Test]
        public void EveryMetreRoundHerIsDryAtSpringHigh()
        {
            float high = NineMileCreekMainland.SpringHighWater;
            for (float dx = -DryRoundHerMetres; dx <= DryRoundHerMetres; dx += 0.5f)
                for (float dy = -DryRoundHerMetres; dy <= DryRoundHerMetres; dy += 0.5f)
                {
                    if (dx * dx + dy * dy > DryRoundHerMetres * DryRoundHerMetres) continue;
                    var p = new Vector2(Park.x + dx, Park.y + dy);
                    float ground = Elev(p);
                    if (ground <= high)
                        Assert.Fail($"G3 at {p}, {Mathf.Sqrt(dx * dx + dy * dy):0.#} m from the Otter, is " +
                                    $"{ground:0.##} m: under spring high ({high:0.##} m) within " +
                                    $"{DryRoundHerMetres} m of her.");
                }
        }

        /// <summary>She faces inland: the first thing the fisher does on climbing in is drive forward,
        /// and that must not be into the basin.</summary>
        [Test]
        public void TheYardAheadOfHerNoseIsDryAtSpringHigh()
        {
            float high = NineMileCreekMainland.SpringHighWater;
            for (float s = 0f; s <= DryAheadMetres; s += 0.25f)
            {
                Vector2 p = Park + Nose * s;
                float ground = Elev(p);
                Assert.That(ground, Is.GreaterThan(high),
                    $"{s:0.##} m ahead of her nose, at {p}, G3 is {ground:0.##} m: under spring high. " +
                    "She is parked facing the water.");
            }
        }

        // =============================================================================================
        //  2. SHE IS OUT OF EVERYONE'S WAY — the roads, the wharf, the buildings, the region
        // =============================================================================================

        /// <summary>
        /// ⭐ Off every road the scene paves (G3's network: Route 91 and Wharf Road on their final
        /// lines, the turning pad, and the rest unchanged), by her far corner and a lane: a machine left
        /// on a carriageway is a machine in the way.
        /// </summary>
        [Test]
        public void SheIsClearOfEveryPavedWay()
        {
            foreach (NineMileCreekRoads.Way way in NineMileCreekGroundPlan.Ways())
            {
                float centre = Vector2.Distance(Park, NineMileCreekRoads.NearestPointOnRoute(way.Route, Park));
                Assert.That(centre - way.HalfWidth, Is.GreaterThan(ClearOfThePavingMetres),
                    $"the Otter's park is {centre - way.HalfWidth:0.##} m from {way.Name}'s edge: inside " +
                    $"the {ClearOfThePavingMetres} m that keeps her out of its way.");
            }
        }

        /// <summary>And off every paved pad: the winch apron, the buyers' gravel, the truck park, the
        /// laydown and the forecourt.</summary>
        [Test]
        public void SheIsOffEveryPavedPad()
        {
            foreach (NineMileCreekRoads.Pad pad in NineMileCreekRoads.Pads())
            {
                Rect r = pad.Area;
                float gx = Mathf.Max(r.xMin - Park.x, 0f, Park.x - r.xMax);
                float gy = Mathf.Max(r.yMin - Park.y, 0f, Park.y - r.yMax);
                float gap = Mathf.Sqrt(gx * gx + gy * gy);
                Assert.That(gap, Is.GreaterThan(ClearOfThePavingMetres),
                    $"the Otter's park is {gap:0.##} m from the {pad.Name} pad: inside the " +
                    $"{ClearOfThePavingMetres} m that keeps her off it.");
            }
        }

        /// <summary>Clear of every working site the wharf plan reserves — by the plan's own rule rather
        /// than by a number restated here.</summary>
        [Test]
        public void SheIsNotStandingOnTheWharfsWorkingGround()
        {
            Assert.That(NineMileCreekMainland.IsWorkingSiteInTheWay(Park), Is.False,
                $"the Otter's park {Park} is inside a working site's reserved ground. The wharf is " +
                "already full, so she must have her own corner of it.");
        }

        /// <summary>Clear of the creekside buildings too, at the radius the wharf's own sheds keep from
        /// one another.</summary>
        [Test]
        public void SheIsClearOfEveryCreeksideBuilding()
        {
            foreach (Vector3 site in NineMileCreekBuilder.CreeksideBuildingSites)
            {
                float d = Vector2.Distance(Park, new Vector2(site.x, site.y));
                Assert.That(d, Is.GreaterThan(NineMileCreekMainland.WharfShedRadius * 2f),
                    $"she is {d:0.##} m from a creekside building at {site} — inside the " +
                    $"{NineMileCreekMainland.WharfShedRadius * 2f:0.#} m the wharf's own sites keep " +
                    "from each other, so she is parked in somebody's dooryard.");
            }
        }

        /// <summary>Inside the region — a park past the edge is somewhere nobody can reach.</summary>
        [Test]
        public void HerParkLiesInsideTheRegion()
        {
            Assert.That(NineMileCreekRoads.RegionRect().Contains(Park), Is.True,
                $"the Otter's park {Park} is outside the region.");
        }

        // =============================================================================================
        //  3. THE BUILDER ACTUALLY PLACES HER — scene-wired is not builder-wired
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>The pin that matters.</b> #560 shipped a drive mode reachable in no scene and nothing
        /// failed; the Otter was usable-and-nowhere from #562. This asserts the placement the builder
        /// makes — one drivable <c>ParkedVehicle</c>, on the park's own published constant, carrying
        /// the def #581 baked.
        /// </summary>
        [Test]
        public void TheBuilderStagesADrivableOtterOnTheParksOwnConstant()
        {
            GameObject otter = NineMileCreekOtterLanding.Place();
            try
            {
                Assert.IsNotNull(otter,
                    "Place() returned nothing — the Otter's def is missing from " +
                    NineMileCreekOtterLanding.OtterDefPath + ", so she stands nowhere.");

                Assert.AreEqual(Park, (Vector2)otter.transform.position,
                    "she is not on the park's published constant, NineMileCreekGroundPlan.OtterParkPos.");

                var parked = otter.GetComponent<HiddenHarbours.Vehicles.ParkedVehicle>();
                Assert.IsNotNull(parked, "no ParkedVehicle — she would never skin at play.");
                Assert.IsNotNull(parked.Vehicle, "no def on her — a machine with no identity.");
                Assert.AreEqual("vehicle.otter_8x8", parked.Vehicle.Id,
                    "the park carries something other than the Otter.");

                Assert.IsNotNull(parked.Vehicle.Mesh,
                    "her def wears no mesh, so VehicleDef.IsUsable refuses her and she is placed " +
                    "invisible — the state #581 existed to end.");
                Assert.IsTrue(parked.Vehicle.Mesh.Floats,
                    "the amphibian reports that she does not float. Her flotation fields are zero — " +
                    "see AmphibiousVehicleTests for where they come from.");

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
        public void ThePlacedOtterCarriesTheParkHeading()
        {
            GameObject otter = NineMileCreekOtterLanding.Place();
            try
            {
                Assert.IsNotNull(otter, "no Otter to measure — see the placement test.");

                float off = Mathf.DeltaAngle(otter.transform.eulerAngles.z,
                                             NineMileCreekGroundPlan.OtterParkHeadingDegrees);
                Assert.That(Mathf.Abs(off), Is.LessThan(1e-2f),
                    "the placed machine does not carry OtterParkHeadingDegrees — she is parked facing " +
                    "somewhere the park does not say.");
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
