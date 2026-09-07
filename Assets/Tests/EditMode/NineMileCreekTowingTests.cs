using System.Collections.Generic;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>WHY NOBODY HAULS A TRAILER AT NINE MILE CREEK YET — measured, on the region's own roads.</b>
    ///
    /// <para>Road fleet PR 6a gave a scheduled trip a towed body. This fixture is the answer to "so put
    /// one on the creek", and the answer is <b>not yet, and here is the number</b>: every road the region
    /// publishes is a dead end, so every errand is an out-and-back, so a machine arrives at each bay on
    /// the reverse of the heading she left it on. The eight-block trip solves that by pivoting her about
    /// her own centre while her driver walks over — and with a trailer on the pin that pivot sweeps a
    /// 53-footer through whatever is parked either side of her.</para>
    ///
    /// <para><b>This is a fact about the WORLD, not about the code</b>, and the second half of the fixture
    /// proves it: take the creek's own bay, the creek's own road out and the creek's own shipped pair,
    /// give the far end and the home end a turning head so each is a PULL-THROUGH, and the identical spec
    /// builds. So what is owed is ground, not arithmetic — the smallest honest change is a turning head at
    /// each end of one route, and that is an owner ruling (§Q1 of the charter) because it decides where
    /// the run goes.</para>
    ///
    /// <para>⚠️ <b>Read the first test as a REPORT, not as an approval.</b> It asserts the refusal because
    /// that is today's truth and a silent "no trailers here" is how a village quietly stops working. The
    /// day somebody cuts a turning head, it reddens — and its message says so.</para>
    /// </summary>
    public class NineMileCreekTowingTests
    {
        const string AeroMesh = "Assets/_Project/Data/Vehicles/Meshes/AeroSemiVehicleMesh.asset";
        const string BoxTrailer = "Assets/_Project/Data/Vehicles/Meshes/TrailerReefer28VehicleMesh.asset";

        const float SecondsPerGameHour = 75f;   // the pinned day, near enough — no hour is asserted here
        const float Cruise = 7f, Walk = 1.4f;

        static VehicleMeshDef Load(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(path);
            Assert.That(def, Is.Not.Null, $"{path} did not load — re-run the vehicle bake.");
            return def;
        }

        /// <summary>The three runs the creek actually ships, each as the region derives it.</summary>
        static IEnumerable<(string name, Vector2[] outbound, Vector2[] home, Vector2 post, Vector2 far)>
            ShippedRuns()
        {
            yield return ("the fish buyer's run", NineMileCreekTrips.OutboundRoute(),
                          NineMileCreekTrips.ReturnRoute(), NineMileCreekTrips.ParkPost(),
                          NineMileCreekTrips.WharfPost());

            Vector2 chandler = NineMileCreekTrips.ChandleryPost();
            yield return ("the chandler's fuel run",
                          NineMileCreekTrips.ToTheForecourt(NineMileCreekMainland.ThroughRoad, chandler),
                          NineMileCreekTrips.FromTheForecourt(NineMileCreekMainland.ThroughRoad, chandler),
                          chandler, NineMileCreekTrips.ForecourtPost());

            Vector2 outboard = NineMileCreekTrips.DoryYardPost();
            yield return ("the outboard man's run",
                          NineMileCreekTrips.ToTheForecourt(NineMileCreekMainland.WharfRoad, outboard),
                          NineMileCreekTrips.FromTheForecourt(NineMileCreekMainland.WharfRoad, outboard),
                          outboard, NineMileCreekTrips.ForecourtPost());
        }

        static Vector2 Direction(Vector2[] route, bool atTheEnd)
        {
            float at = atTheEnd ? Polyline.Length(route, 0, route.Length) : 0f;
            return Polyline.TangentAlong(route, 0, route.Length, at);
        }

        static VehicleTripSpec Spec(VehicleMeshDef tractor, VehicleMeshDef trailer, Vector2[] outbound,
                                    Vector2[] home, Vector2 post, Vector2 far)
        {
            // The trailer stands couple-ready in the bay: pin in the middle of the tractor's own capture
            // window, on the heading the road out leaves. Nothing typed — the same solve the laydown uses.
            float heading = BoatKinematics.BearingDegrees(Direction(outbound, atTheEnd: false));
            VehicleFifthWheel wheel = tractor.FifthWheel;
            var slot = new Vector2(wheel.CouplingPointLocal.x,
                                   (wheel.RampMouthY + wheel.SlotSeatY) * 0.5f);
            Vector2 pin = outbound[0] + VehicleCouplingMath.LocalOffsetToWorld(slot, heading);
            Vector2 bay = VehicleCouplingMath.BodyOriginFromKingpin(pin, heading, trailer.Kingpin);

            return new VehicleTripSpec(outbound, home, post, Vector2.down, far, Vector2.down,
                                       tractor.DriveDoorLocal, 6f, 15f, Cruise, Walk,
                                       new VehicleTowedSpec(wheel, trailer.Kingpin, bay, heading));
        }

        // =============================================================================================
        //  1. THE REPORT: no road at the creek can carry a coupled pair today
        // =============================================================================================

        [Test]
        public void EveryShippedRunIsRefusedForATrailer_BecauseBothBaysNeedAPivot()
        {
            VehicleMeshDef tractor = Load(AeroMesh), trailer = Load(BoxTrailer);
            float band = VehicleCouplingMath.CaptureHeadingToleranceDegrees(tractor.FifthWheel);

            foreach ((string name, Vector2[] outbound, Vector2[] home, Vector2 post, Vector2 far)
                     in ShippedRuns())
            {
                float leaveBay = BoatKinematics.BearingDegrees(Direction(outbound, false));
                float arriveBay = BoatKinematics.BearingDegrees(Direction(home, true));
                float arriveFar = BoatKinematics.BearingDegrees(Direction(outbound, true));
                float leaveFar = BoatKinematics.BearingDegrees(Direction(home, false));

                float atHome = Mathf.Abs(Mathf.DeltaAngle(arriveBay, leaveBay));
                float atFar = Mathf.Abs(Mathf.DeltaAngle(arriveFar, leaveFar));

                Assert.That(atHome, Is.EqualTo(180f).Within(1f),
                    $"{name}: the home bay is in {arriveBay:0.0}° / out {leaveBay:0.0}°. If this is no " +
                    "longer 180° apart somebody has cut a turning head — good news, and this fixture is " +
                    "now the wrong shape: wire the towing run instead.");
                Assert.That(atFar, Is.EqualTo(180f).Within(1f),
                    $"{name}: the far bay is {atFar:0.0}° apart, not 180°.");

                VehicleTripPlan plan = VehicleTripPlan.Build(
                    Spec(tractor, trailer, outbound, home, post, far), SecondsPerGameHour,
                    out string problem);

                Assert.That(plan, Is.Null,
                    $"{name} was accepted as a towing run. She arrives at each bay {atHome:0.0}° from " +
                    $"the heading she leaves it on, against the slot's own {band:0.00}° — a coupled " +
                    "pair cannot pivot, so this would sweep the trailer through the neighbouring bays.");
                Assert.That(problem, Does.Contain("cannot turn in a bay"),
                    $"{name}: refused for the wrong reason — '{problem}'");
            }
        }

        [Test]
        public void TheLaydownHasNowhereForAPairToTurn()
        {
            Rect apron = NineMileCreekLaydown.ApronArea();
            Vector2[] spur = NineMileCreekLaydown.LaydownSpurRoute();

            // The yard is one apron with a lane along its south edge and eight bays on the north of it.
            // The only way in is the spur, and it ENDS at the yard — so a pair that drives out of a bay
            // has nothing to drive round.
            Assert.That(NineMileCreekLaydown.LaneArea().yMax,
                        Is.EqualTo(NineMileCreekLaydown.BayArea(0).yMin).Within(0.001f),
                "the lane no longer abuts the bays, so this fixture is reading a yard that has changed.");

            float lane = NineMileCreekLaydown.LaneWidthMetres;
            float pair = NineMileCreekLaydown.LongestPairMetres;
            Assert.That(lane, Is.LessThan(pair),
                $"the lane is {lane:0.0} m and the longest pair {pair:0.0} m. A turning head has to be " +
                "wider than the thing turning in it; if the lane has grown past the pair, a towing run " +
                "out of this yard may now close — measure it and wire one.");

            Vector2 yard = new(NineMileCreekMainland.LaydownPos.x, NineMileCreekMainland.LaydownPos.y);
            Assert.That((spur[spur.Length - 1] - yard).magnitude, Is.LessThan(0.01f),
                "the laydown spur no longer ends at the yard — if it now runs THROUGH it, the yard has " +
                "a pull-through and a towing run can be sited here.");
            Assert.That(apron.Contains(spur[spur.Length - 1]), Is.True);
        }

        // =============================================================================================
        //  2. AND IT IS THE ROADS, NOT THE PAIR
        // =============================================================================================

        [Test]
        public void TheSameShippedPairHaulsTheSameRoadOnceEachEndHasATurningHead()
        {
            VehicleMeshDef tractor = Load(AeroMesh), trailer = Load(BoxTrailer);

            foreach ((string name, Vector2[] outbound, Vector2[] home, Vector2 post, Vector2 far)
                     in ShippedRuns())
            {
                VehicleTripPlan plan = VehicleTripPlan.Build(
                    Spec(tractor, trailer, outbound, WithTurningHeads(outbound), post, far),
                    SecondsPerGameHour, out string problem);

                Assert.That(plan, Is.Not.Null,
                    $"{name}: the creek's own bay, the creek's own road out and the creek's own shipped " +
                    $"pair still would not build with a turning head at each end — {problem}. That would " +
                    "mean the refusal is NOT about the roads, which is what this whole fixture claims.");
                Assert.That(plan.Tows, Is.True);

                // …and the day closes on the plate she was picked up off, which is the acceptance.
                Assert.That(VehicleCouplingMath.WouldCapture(
                                tractor.FifthWheel, trailer.Kingpin, outbound[0],
                                BoatKinematics.BearingDegrees(Direction(outbound, false)),
                                plan.TrailerRestPosition, plan.TrailerRestHeadingDegrees), Is.True,
                    $"{name}: the day settled her somewhere her own plate would not offer her tomorrow.");
            }
        }

        /// <summary>
        /// ⭐ <b>The smallest change that unblocks a towing run, drawn as geometry.</b> The road home is
        /// the road out with a loop at each end: she drives ON past the far bay, comes round, runs back,
        /// passes the home bay and comes round again — so she enters each bay on the heading she left it
        /// on and never has to pivot.
        ///
        /// <para>This is a SKETCH, not a proposal for a route: it is here to isolate the variable. The
        /// real thing is a turning head cut into the ground at each end, which is a change to the region
        /// and therefore the owner's (§Q1).</para>
        /// </summary>
        static Vector2[] WithTurningHeads(Vector2[] outbound)
        {
            Vector2 bay = outbound[0], far = outbound[outbound.Length - 1];
            Vector2 leaveBay = Direction(outbound, false).normalized;
            Vector2 arriveFar = Direction(outbound, true).normalized;

            // A turning head has to be wider than the pair that turns in it — the laydown's own rule for
            // its lane, applied to a loop.
            const float Head = 30f;
            Vector2 farSide = new(arriveFar.y, -arriveFar.x);
            Vector2 baySide = new(leaveBay.y, -leaveBay.x);

            var home = new List<Vector2>
            {
                far,
                far + arriveFar * Head,                                   // drive on past the far bay
                far + arriveFar * Head + farSide * Head,                  // …and round
                far + farSide * Head,
            };
            for (int i = outbound.Length - 2; i >= 1; i--) home.Add(outbound[i] + farSide * Head * 0.5f);
            home.Add(bay - leaveBay * Head + baySide * Head);             // past the home bay
            home.Add(bay - leaveBay * Head);                              // …and round
            home.Add(bay);                                                // in on the heading she left on
            return home.ToArray();
        }
    }
}
