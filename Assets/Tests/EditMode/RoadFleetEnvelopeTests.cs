using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>WHAT EACH MACHINE CAN ACTUALLY DO, AND WHAT A TRAILER COSTS HER</b> — road fleet PR 6b.
    ///
    /// <para><b>The defect this fixture exists on the far side of:</b> nine of the fleet's ten
    /// <c>VehicleDef</c> assets carried the <i>identical</i> shipped default — a 200 cc trike, a utility
    /// quad, a hightop van and a 53-ft-capable highway tractor all doing 11 m/s, 4.5 m/s² and two full
    /// locks a second. Only the Otter had ever been authored. Nothing was wrong; nothing had been
    /// decided.</para>
    ///
    /// <para><b>What is asserted here is the RELATIONSHIP to the art, never the numbers.</b> The numbers
    /// are feel and they are the owner's — a fixture that pinned them would go red the first time he
    /// moved a knob, which is the opposite of what a tuning gate is for. So: heavier machines stop
    /// harder than they pull away, astern is slower than ahead, a load never makes anybody faster, and
    /// — the one that ties feel to the art — <b>a machine with a tighter minimum turning circle loses
    /// her steering sooner</b>, because that is what the falloff was derived from.</para>
    ///
    /// <para><b>The coupled pair's steering is not feel at all.</b> It is solved from the two machines'
    /// published lengths, and §2 asserts the identity exactly.</para>
    /// </summary>
    public class RoadFleetEnvelopeTests
    {
        const string DefDir = "Assets/_Project/Data/Vehicles/";
        const string MeshDir = "Assets/_Project/Data/Vehicles/Meshes/";

        static readonly string[] Fleet =
        {
            "AeroSemi", "ClassicSemi", "ConvBox", "CaboverBox", "HightopVan",
            "Dually3500", "UtilityQuad", "Trike200", "Enduro250", "Otter8x8",
        };

        static readonly string[] Trailers =
        {
            "TrailerReefer28VehicleMesh", "TrailerFlatbed28VehicleMesh",
            "TrailerReefer53VehicleMesh", "TrailerFlatbed53VehicleMesh",
        };

        static VehicleDef Def(string name)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleDef>(DefDir + name + ".asset");
            Assert.That(def, Is.Not.Null, $"{name} did not load — re-run the vehicle bake.");
            return def;
        }

        static VehicleMeshDef Mesh(string name)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(MeshDir + name + ".asset");
            Assert.That(def, Is.Not.Null, $"{name} did not load — re-run the vehicle bake.");
            return def;
        }

        /// <summary>Her tightest circle, metres — <c>wheelbase / tan(inner lock)</c>, both numbers the
        /// art's. Meaningless for a skid-steered machine, which has no steering axle at all.</summary>
        static float MinimumTurningCircle(VehicleMeshDef mesh) =>
            mesh.MaxInnerSteerDegrees > 0f
                ? mesh.WheelbaseMeters / Mathf.Tan(mesh.MaxInnerSteerDegrees * Mathf.Deg2Rad)
                : 0f;

        // =============================================================================================
        //  1. THE FLEET IS A FLEET
        // =============================================================================================

        [Test]
        public void NoTwoMachinesShareOneEnvelopeAnyMore()
        {
            var seen = new Dictionary<string, string>();
            foreach (string name in Fleet)
            {
                VehicleDef def = Def(name);
                string key = $"{def.MaxSpeedMetersPerSecond}/{def.MaxReverseSpeedMetersPerSecond}/" +
                             $"{def.AccelerationMetersPerSecondSquared}/" +
                             $"{def.BrakingMetersPerSecondSquared}/" +
                             $"{def.CoastDecelerationMetersPerSecondSquared}/" +
                             $"{def.SteerRateFullLocksPerSecond}/" +
                             $"{def.SteerFalloffHalfSpeedMetersPerSecond}";

                Assert.That(seen.ContainsKey(key), Is.False,
                    $"{name} and {(seen.ContainsKey(key) ? seen[key] : "")} drive identically ({key}). " +
                    "Nine of ten machines carried one default before PR 6b; a machine that has not been " +
                    "given numbers of her own has not been designed, she has been copied.");
                seen[key] = name;
            }
        }

        [Test]
        public void EveryMachineStopsHarderThanShePullsAwayAndBacksSlowerThanSheDrives()
        {
            foreach (string name in Fleet)
            {
                VehicleDef def = Def(name);
                Assert.That(def.BrakingMetersPerSecondSquared,
                            Is.GreaterThan(def.AccelerationMetersPerSecondSquared),
                    $"{name} accelerates harder than she brakes — brakes are the one thing every " +
                    "machine has more of than engine.");
                Assert.That(def.MaxReverseSpeedMetersPerSecond,
                            Is.LessThan(def.MaxSpeedMetersPerSecond),
                    $"{name} reverses as fast as she drives forward — no gearbox in the fleet does.");
                Assert.That(def.CoastDecelerationMetersPerSecondSquared,
                            Is.LessThan(def.BrakingMetersPerSecondSquared),
                    $"{name} sheds speed off the throttle as hard as under the brake.");
            }
        }

        [Test]
        public void AMachineWithATighterCircleLosesHerSteeringSooner()
        {
            // ⭐ The one place a FEEL number is tied to the ART: the falloff half-speed was derived as
            // sqrt(a_lat · R_min) with one lateral-acceleration constant for the whole fleet, anchored
            // so the Dually keeps the 9 m/s she was shipped with. What that leaves behind is an
            // ORDERING, and the ordering is what a fixture can hold without pinning the owner's hand.
            var steered = Fleet
                .Select(name => (name, def: Def(name), mesh: Mesh(NameOfMesh(name))))
                .Where(m => m.mesh.MaxInnerSteerDegrees > 0f)
                .OrderBy(m => MinimumTurningCircle(m.mesh))
                .ToList();

            Assert.That(steered.Count, Is.GreaterThanOrEqualTo(8),
                "the fleet lost a steered machine, or a mesh stopped publishing her lock.");

            for (int i = 1; i < steered.Count; i++)
            {
                (string name, VehicleDef def, VehicleMeshDef mesh) tighter = steered[i - 1];
                (string name, VehicleDef def, VehicleMeshDef mesh) wider = steered[i];

                Assert.That(tighter.def.SteerFalloffHalfSpeedMetersPerSecond,
                            Is.LessThanOrEqualTo(wider.def.SteerFalloffHalfSpeedMetersPerSecond + 1e-3f),
                    $"{tighter.name} turns inside {wider.name} " +
                    $"({MinimumTurningCircle(tighter.mesh):0.00} m against " +
                    $"{MinimumTurningCircle(wider.mesh):0.00} m) and yet holds her steering LONGER " +
                    $"({tighter.def.SteerFalloffHalfSpeedMetersPerSecond:0.00} against " +
                    $"{wider.def.SteerFalloffHalfSpeedMetersPerSecond:0.00} m/s). The falloff is the " +
                    "speed at which she runs out of lateral grip, so it has to follow her circle.");
            }
        }

        [Test]
        public void ALoadNeverMakesAnybodyFaster()
        {
            foreach (string name in Fleet)
            {
                VehicleDef def = Def(name);
                TowingLoad load = def.TowingLoad;
                Assert.That(load.SpeedFraction, Is.InRange(0.1f, 1f), name);
                Assert.That(load.AccelerationFraction, Is.InRange(0.1f, 1f), name);
                Assert.That(load.BrakingFraction, Is.InRange(0.1f, 1f), name);
                Assert.That(load.CoastFraction, Is.InRange(0.1f, 1f), name);

                Assert.That(load.AccelerationFraction, Is.LessThanOrEqualTo(load.SpeedFraction),
                    $"{name}: a load costs her less off the mark than at the top, which is backwards — " +
                    "mass is felt first in acceleration.");
            }
        }

        [Test]
        public void ALoadedEnvelopeIsNarrowerInEveryDirection()
        {
            VehicleDef def = Def("AeroSemi");
            DriveEnvelope bare = def.LandEnvelope;
            DriveEnvelope loaded = bare.With(def.TowingLoad);

            Assert.That(loaded.MaxAheadMetersPerSecond, Is.LessThan(bare.MaxAheadMetersPerSecond));
            Assert.That(loaded.MaxAsternMetersPerSecond, Is.LessThan(bare.MaxAsternMetersPerSecond));
            Assert.That(loaded.AccelerationMetersPerSecondSquared,
                        Is.LessThan(bare.AccelerationMetersPerSecondSquared));
            Assert.That(loaded.BrakingMetersPerSecondSquared,
                        Is.LessThan(bare.BrakingMetersPerSecondSquared));
            Assert.That(loaded.CoastDecelerationMetersPerSecondSquared,
                        Is.LessThan(bare.CoastDecelerationMetersPerSecondSquared),
                "a loaded pair must carry her way FURTHER off the throttle, not less far — this is the " +
                "one of the four that makes her feel heavy rather than merely feeble.");
        }

        // =============================================================================================
        //  2. THE PAIR'S STEERING IS SOLVED, NOT TUNED
        // =============================================================================================

        [Test]
        public void ACoupledPairTurnsExactlyLikeTheLongVehicleItIs()
        {
            foreach (string tractorName in new[] { "AeroSemi", "ClassicSemi" })
            foreach (string trailerName in Trailers)
            {
                VehicleMeshDef tractor = Mesh(NameOfMesh(tractorName)), trailer = Mesh(trailerName);
                float wb = tractor.WheelbaseMeters;
                float inner = tractor.MaxInnerSteerDegrees;
                float length = trailer.Kingpin.KingpinToAxleCentreMeters;

                float narrowed = VehicleCouplingMath.CoupledSteer(1f, inner, wb, length);
                float narrowedInner = narrowed * inner * Mathf.Deg2Rad;

                float bobtailCircle = wb / Mathf.Tan(inner * Mathf.Deg2Rad);
                float pairCircle = wb / Mathf.Tan(narrowedInner);

                Assert.That(pairCircle, Is.EqualTo((wb + length) / Mathf.Tan(inner * Mathf.Deg2Rad))
                                                  .Within(1e-3f),
                    $"{tractorName} + {trailerName}: with the trailer on she draws a {pairCircle:0.00} m " +
                    $"circle at full lock, and the pair is {wb + length:0.00} m long — the identity is " +
                    "that she turns like the long vehicle she has become, and nothing about it is a " +
                    "tuned number.");

                Assert.That(pairCircle, Is.GreaterThan(bobtailCircle),
                    $"{tractorName} + {trailerName}: the pair turns no wider than the bobtail tractor.");
            }
        }

        [Test]
        public void ALongerTrailerLeavesLessLock()
        {
            VehicleMeshDef tractor = Mesh(NameOfMesh("AeroSemi"));
            float pup = VehicleCouplingMath.CoupledSteer(
                1f, tractor.MaxInnerSteerDegrees, tractor.WheelbaseMeters,
                Mesh("TrailerReefer28VehicleMesh").Kingpin.KingpinToAxleCentreMeters);
            float long53 = VehicleCouplingMath.CoupledSteer(
                1f, tractor.MaxInnerSteerDegrees, tractor.WheelbaseMeters,
                Mesh("TrailerFlatbed53VehicleMesh").Kingpin.KingpinToAxleCentreMeters);

            Assert.That(long53, Is.LessThan(pup),
                $"a 53 leaves her {long53:0.000} of her lock and a pup {pup:0.000} — the longer body " +
                "must be the one that takes more away, and it falls out of her own published length.");
            Assert.That(pup, Is.LessThan(1f));
        }

        [Test]
        public void ABobtailTractorIsNotNarrowedAtAll()
        {
            VehicleMeshDef tractor = Mesh(NameOfMesh("AeroSemi"));
            foreach (float steer in new[] { -1f, -0.4f, 0f, 0.4f, 1f })
                Assert.That(VehicleCouplingMath.CoupledSteer(
                                steer, tractor.MaxInnerSteerDegrees, tractor.WheelbaseMeters, 0f),
                            Is.EqualTo(steer),
                    "an unpublished or zero-length body narrowed her steering — nothing on the plate " +
                    "must cost nothing.");
        }

        [Test]
        public void TheNarrowingKeepsHerSignAndNeverAddsLock()
        {
            VehicleMeshDef tractor = Mesh(NameOfMesh("ClassicSemi"));
            float length = Mesh("TrailerReefer53VehicleMesh").Kingpin.KingpinToAxleCentreMeters;

            float previous = float.NegativeInfinity;
            for (float steer = -1f; steer <= 1.0001f; steer += 0.05f)
            {
                float narrowed = VehicleCouplingMath.CoupledSteer(
                    steer, tractor.MaxInnerSteerDegrees, tractor.WheelbaseMeters, length);

                Assert.That(Mathf.Abs(narrowed), Is.LessThanOrEqualTo(Mathf.Abs(steer) + 1e-5f),
                    $"at {steer:0.00} the pair was given MORE lock than the wheel is asking for.");
                if (!Mathf.Approximately(steer, 0f))
                    Assert.That(Mathf.Sign(narrowed), Is.EqualTo(Mathf.Sign(steer)),
                        $"at {steer:0.00} the narrowing mirrored her lock.");
                Assert.That(narrowed, Is.GreaterThanOrEqualTo(previous - 1e-5f),
                    "the narrowing is not monotone, so a driver winding on lock would feel it reverse.");
                previous = narrowed;
            }
        }

        // =============================================================================================
        //  3. THE WIRING: the hitch is what tells her she is loaded
        // =============================================================================================

        [Test]
        public void TakingATrailerNarrowsHerEnvelope_AndLettingHerGoGivesItBack()
        {
            VehicleDef def = Def("AeroSemi");
            VehicleMeshDef trailerMesh = Mesh("TrailerFlatbed53VehicleMesh");

            var tractorObject = new GameObject("TractorUnderTest", typeof(Rigidbody2D));
            var trailerObject = new GameObject("TrailerUnderTest");
            try
            {
                VehicleController controller = tractorObject.AddComponent<VehicleController>();
                controller.SetVehicle(def);

                VehicleHitch hitch = tractorObject.AddComponent<VehicleHitch>();
                hitch.Configure(def.Mesh, controller, def.Id);

                TowedBody body = trailerObject.AddComponent<TowedBody>();
                body.Configure(trailerMesh);   // an editor AddComponent fires no OnEnable

                float bare = controller.Envelope.MaxAheadMetersPerSecond;
                Assert.That(controller.IsTowing, Is.False, "she started the test already loaded.");

                Assert.That(hitch.Couple(body), Is.True, "the shipped hitch refused a clean pair.");
                Assert.That(controller.IsTowing, Is.True,
                    "the pin went in and the drive model never heard about it. The hitch is the one "
                    + "thing that knows a load arrived \u2014 nothing else looks.");
                Assert.That(controller.Towing.KingpinToAxleCentreMeters,
                            Is.EqualTo(trailerMesh.Kingpin.KingpinToAxleCentreMeters),
                    "she was handed a load but not the LENGTH, which is what her steering is solved on.");
                Assert.That(controller.Envelope.MaxAheadMetersPerSecond, Is.LessThan(bare));

                // Her legs have to be down before she may be set down \u2014 the shipped interlock.
                var doors = body.GetComponent<VehicleDoors>();
                if (doors != null) doors.SnapAllShut();

                Assert.That(hitch.TryUncouple(out string refusal), Is.True, refusal);
                Assert.That(controller.IsTowing, Is.False,
                    "the pin came out and she is still driving like a loaded truck.");
                Assert.That(controller.Envelope.MaxAheadMetersPerSecond, Is.EqualTo(bare));
            }
            finally
            {
                Object.DestroyImmediate(trailerObject);
                Object.DestroyImmediate(tractorObject);
            }
        }

        [Test]
        public void SwappingMachineDropsWhateverTheLastOneWasPulling()
        {
            var tractorObject = new GameObject("TractorUnderTest", typeof(Rigidbody2D));
            try
            {
                VehicleController controller = tractorObject.AddComponent<VehicleController>();
                controller.SetVehicle(Def("AeroSemi"));
                controller.SetTow(Mesh("TrailerReefer53VehicleMesh").Kingpin);
                Assert.That(controller.IsTowing, Is.True);

                controller.SetVehicle(Def("Dually3500"));
                Assert.That(controller.IsTowing, Is.False,
                    "the player climbed out of a loaded semi into a pickup and took the trailer's "
                    + "numbers with him.");
            }
            finally { Object.DestroyImmediate(tractorObject); }
        }

        /// <summary>The mesh asset name for a def name. Every road machine's mesh is her own name plus
        /// the suffix the baker uses.</summary>
        static string NameOfMesh(string defName) => defName + "VehicleMesh";
    }
}
