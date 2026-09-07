using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>ONE TOW, TWO CALLERS</b> — the road-fleet PR 6a guard that a scheduled trip's trailer and
    /// the player's trailer are the same arithmetic and not two transcriptions of it.
    ///
    /// <para>The subject is a rule, not a number: <c>TowedFollowTrack</c> walks a road at build time so a
    /// pose plan can read a trailer off the clock, and every step of that walk has to be the step
    /// <c>TowedBody.FollowKingpin</c> takes. So the fixture drives a LIVE <c>TowedBody</c> — the
    /// component the player's hitch drives — over the track's own stations and asks for the same answer
    /// EXACTLY. Not within a tolerance: both go through
    /// <see cref="VehicleCouplingMath.FollowStep"/> on identical inputs in identical order, so anything
    /// but bit equality means somebody has written the follow out a second time.</para>
    ///
    /// <para>(That is the one place bit equality is the right assertion. Between two independent
    /// transcriptions it is unattainable and a ULP band is the honest bar — memory
    /// <c>bit-equality-is-unattainable-between-two-transcriptions</c>. Here there is only one
    /// transcription, and proving that is the whole point of the test.)</para>
    /// </summary>
    public class TowedFollowTrackTests
    {
        const string AeroMesh = "Assets/_Project/Data/Vehicles/Meshes/AeroSemiVehicleMesh.asset";
        const string ClassicMesh = "Assets/_Project/Data/Vehicles/Meshes/ClassicSemiVehicleMesh.asset";
        const string Pup = "Assets/_Project/Data/Vehicles/Meshes/TrailerReefer28VehicleMesh.asset";
        const string Long = "Assets/_Project/Data/Vehicles/Meshes/TrailerFlatbed53VehicleMesh.asset";

        GameObject _spawned;

        [TearDown]
        public void TearDown()
        {
            if (_spawned != null) Object.DestroyImmediate(_spawned);
            _spawned = null;
        }

        static VehicleMeshDef Load(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(path);
            Assert.That(def, Is.Not.Null, $"{path} did not load — re-run the vehicle bake.");
            return def;
        }

        /// <summary>A road with a real bend in it: straight out, a long sweeping left, straight again.
        /// A trailer only differs from her tractor where the road turns, so a straight-line fixture
        /// would pass on a follow that did nothing at all.</summary>
        static Vector2[] BendyRoad() => new[]
        {
            new Vector2(0f, 0f), new Vector2(0f, 40f), new Vector2(-12f, 62f),
            new Vector2(-40f, 74f), new Vector2(-90f, 74f),
        };

        static Vector2 PlateLocal(VehicleMeshDef tractor) =>
            new Vector2(tractor.FifthWheel.CouplingPointLocal.x, tractor.FifthWheel.CouplingPointLocal.y);

        TowedBody StandTrailer(VehicleMeshDef mesh, Vector2 at, float headingDegrees)
        {
            _spawned = new GameObject("TrailerUnderTest");
            _spawned.transform.position = new Vector3(at.x, at.y, 0f);
            _spawned.transform.rotation = Quaternion.Euler(0f, 0f, -headingDegrees);

            var body = _spawned.AddComponent<TowedBody>();
            body.Configure(mesh);   // an editor AddComponent fires no OnEnable — see TowedBody's note
            return body;
        }

        // =============================================================================================
        //  1. THE PLAN'S TRAILER AND THE PLAYER'S TRAILER ARE ONE COMPUTATION
        // =============================================================================================

        [Test]
        public void TheTrackIsTheArithmeticThePlayersOwnTowRuns_ExactlyAtEveryStation()
        {
            VehicleMeshDef tractor = Load(AeroMesh), trailer = Load(Pup);
            Vector2[] road = BendyRoad();
            float cap = VehicleCouplingMath.JackknifeCapDegrees(trailer.Kingpin, tractor.FifthWheel);
            const float StartHeading = 0f;      // lined up behind her, pointing up the road

            TowedFollowTrack track = TowedFollowTrack.Build(road, 0, road.Length, PlateLocal(tractor),
                                                            trailer.Kingpin, cap, StartHeading);

            Vector2 start = VehicleCouplingMath.BodyOriginFromKingpin(track.PlateAt(0f), StartHeading,
                                                                      trailer.Kingpin);
            TowedBody body = StandTrailer(trailer, start, StartHeading);

            Assert.That(track.StationCount, Is.GreaterThan(200),
                "the road should be walked in steps off the trailer's own length scale, not in a " +
                "handful of jumps — a coarse walk would make this comparison meaningless.");

            for (int i = 1; i < track.StationCount; i++)
            {
                float here = track.StationDistance(i), before = track.StationDistance(i - 1);

                // Exactly what VehicleHitch.Step hands her every LateUpdate: the plate, the tractor's
                // heading, and the signed distance the TRACTOR covered.
                body.FollowKingpin(track.PlateAt(here), track.TractorHeadingAt(here), here - before, cap);

                Assert.That(body.HeadingDegrees, Is.EqualTo(track.StationHeading(i)),
                    $"station {i} ({here:0.00} m): the plan's trailer and the player's trailer parted " +
                    "company. They must both be VehicleCouplingMath.FollowStep on the same inputs — a " +
                    "difference of any size means the follow has been written out twice.");
            }

            // …and the same for where she is drawn, which is the number a plate would show.
            track.SampleAt(track.LengthMetres, out Vector2 origin, out _);
            Assert.That(((Vector2)body.transform.position - origin).magnitude, Is.LessThan(1e-4f),
                "she ends the road in a different place from the one the plan poses her at.");
        }

        // =============================================================================================
        //  2. THE STEP IS FINE ENOUGH THAT IT IS NOT PART OF THE ANSWER
        // =============================================================================================

        [Test]
        public void HalvingTheStepDoesNotMoveHer()
        {
            VehicleMeshDef tractor = Load(AeroMesh), trailer = Load(Long);
            Vector2[] road = BendyRoad();
            float cap = VehicleCouplingMath.JackknifeCapDegrees(trailer.Kingpin, tractor.FifthWheel);

            float shipped = TowedFollowTrack.WalkHeading(road, 0, road.Length, PlateLocal(tractor),
                                                         trailer.Kingpin, cap, 0f);
            float twice = WalkAtStep(road, PlateLocal(tractor), trailer.Kingpin, cap, 0f, 2);
            float fourTimes = WalkAtStep(road, PlateLocal(tractor), trailer.Kingpin, cap, 0f, 4);

            // The claim is that the STEP is not part of the answer. Refining it must move her by less
            // than anything a 32 px/m picture could show — a hundredth of a degree over a 145 m road
            // with two bends in it is about a third of a millimetre at her tail.
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(shipped, twice)), Is.LessThan(0.05f),
                $"halving the step moved her {Mathf.DeltaAngle(shipped, twice):0.0000}° — the " +
                "integration resolution is showing up in the answer, so it has become a tunable.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(twice, fourTimes)),
                        Is.LessThan(Mathf.Abs(Mathf.DeltaAngle(shipped, twice)) + 1e-4f),
                "refining further moved her MORE, which is divergence rather than convergence.");
        }

        /// <summary>The same walk at <paramref name="refine"/>× the shipped resolution. The step is
        /// derived inside the track and has no test hook, so the convergence arm re-runs the SHIPPED yaw
        /// law (<see cref="VehicleCouplingMath.FollowStep"/>) at a finer spacing rather than measuring a
        /// second model of it.</summary>
        static float WalkAtStep(Vector2[] road, Vector2 plateLocal, in VehicleKingpin pin, float cap,
                                float startHeading, int refine)
        {
            float length = Polyline.Length(road, 0, road.Length);
            float step = pin.KingpinToAxleCentreMeters / (TowedFollowTrack.StepsPerLengthScale * refine);
            int steps = Mathf.CeilToInt(length / step);

            float heading = startHeading;
            for (int i = 1; i <= steps; i++)
            {
                float here = Mathf.Min(i * step, length), before = Mathf.Min((i - 1) * step, length);
                Vector2 at = Polyline.PointAlong(road, 0, road.Length, here);
                float tractor = BoatKinematics.BearingDegrees(
                    Polyline.TangentAlong(road, 0, road.Length, here));
                Vector2 plate = at + VehicleCouplingMath.LocalOffsetToWorld(plateLocal, tractor);

                VehicleCouplingMath.FollowStep(heading, plate, tractor, here - before, cap, pin,
                                               out heading, out _);
            }
            return heading;
        }

        // =============================================================================================
        //  3. WHAT A TOW ACTUALLY LOOKS LIKE
        // =============================================================================================

        [Test]
        public void HerPinIsOnThePlateAtEverySample()
        {
            VehicleMeshDef tractor = Load(ClassicMesh), trailer = Load(Long);
            Vector2[] road = BendyRoad();
            float cap = VehicleCouplingMath.JackknifeCapDegrees(trailer.Kingpin, tractor.FifthWheel);

            TowedFollowTrack track = TowedFollowTrack.Build(road, 0, road.Length, PlateLocal(tractor),
                                                            trailer.Kingpin, cap, 0f);

            // Between stations, not on them: the sample has to derive her position from the plate at the
            // distance ASKED, or she floats off the coupling by up to half a step.
            for (float d = 0f; d <= track.LengthMetres; d += track.StepMetres * 0.37f)
            {
                track.SampleAt(d, out Vector2 origin, out float heading);
                Vector2 pin = origin + VehicleCouplingMath.LocalOffsetToWorld(
                    new Vector2(trailer.Kingpin.CouplingPointLocal.x,
                                trailer.Kingpin.CouplingPointLocal.y), heading);

                Assert.That((pin - track.PlateAt(d)).magnitude, Is.LessThan(1e-3f),
                    $"at {d:0.00} m her pin is off the plate — a coupled pair is welded at the pin in " +
                    "every single frame, which is what makes it a coupling rather than a follow.");
            }
        }

        [Test]
        public void SheCutsTheCornerAndTheLongerBodyCutsItMore()
        {
            VehicleMeshDef tractor = Load(AeroMesh);
            Vector2[] road = BendyRoad();

            float pupSweep = OffTracking(road, tractor, Load(Pup));
            float longSweep = OffTracking(road, tractor, Load(Long));

            Assert.That(pupSweep, Is.GreaterThan(0.5f),
                "the trailer tracked the road exactly, so she is not off-tracking at all.");
            Assert.That(longSweep, Is.GreaterThan(pupSweep),
                $"the 53 cut the corner by {longSweep:0.00} m and the pup by {pupSweep:0.00} m — a " +
                "longer body straightens more slowly and must cut MORE, which is the one line in " +
                "TrailerYawDeltaDegrees and the whole reason the length scale is published per body.");
        }

        /// <summary>How far, at most, the trailer's own axle centre falls inside the road the tractor
        /// drove — the corner-cutting, in metres.</summary>
        static float OffTracking(Vector2[] road, VehicleMeshDef tractor, VehicleMeshDef trailer)
        {
            float cap = VehicleCouplingMath.JackknifeCapDegrees(trailer.Kingpin, tractor.FifthWheel);
            TowedFollowTrack track = TowedFollowTrack.Build(road, 0, road.Length, PlateLocal(tractor),
                                                            trailer.Kingpin, cap, 0f);
            float worst = 0f;
            for (float d = 0f; d <= track.LengthMetres; d += 1f)
            {
                track.SampleAt(d, out Vector2 origin, out float heading);
                Vector2 axle = origin + VehicleCouplingMath.LocalOffsetToWorld(
                    new Vector2(trailer.Kingpin.CouplingPointLocal.x,
                                trailer.Kingpin.CouplingPointLocal.y
                                - trailer.Kingpin.KingpinToAxleCentreMeters), heading);

                Vector2 onRoad = Polyline.NearestPoint(road, 0, road.Length, axle, out _);
                worst = Mathf.Max(worst, (axle - onRoad).magnitude);
            }
            return worst;
        }
    }
}
