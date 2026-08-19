using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Vehicles;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>THE LAUNCH LOOP, END TO END, UNDER A RUNNING FRAME PUMP</b> — board her on dry ground,
    /// drive her down a ramp until she floats, step off into water the fisher can stand in, and get
    /// back aboard. Every part of that already existed and was tested apart: #562 built the drive⇄swim
    /// model, #572 built the exit rules, #581 baked her. Nothing had ever run them as one sequence, and
    /// "she is placed at a launch" is only a claim until something does.
    ///
    /// <para><b>What this composes rather than re-implements.</b> The afloat rule is
    /// <see cref="VehicleGrounding"/>'s (draft plus hysteresis, read off her def). The exit rule is
    /// <see cref="ControlSwitcher.LeaveDriving"/>'s depth band. This file drives them and asserts the
    /// SEQUENCE — it does not restate either rule, and it does not test #572's refusal over deep water,
    /// which is <c>VehicleDriveModeTests</c>' and lives with the rule it guards.</para>
    ///
    /// <para><b>⚠️ The terrain here is a RAMP PROFILE, not Nine Mile Creek.</b> This assembly cannot
    /// reference the editor-only region plan (and should not: it must survive a player build), so the
    /// ramp below reproduces the profile MEASURED at her landing on 2026-08-19 — dry apron at +3.44 m
    /// falling to the −1.60 m basin shoal over about six metres. What ties the two together is
    /// <c>NineMileCreekOtterLandingTests</c>, which asserts the real site against the real terrain for
    /// the same three facts this fixture relies on: dry ground to start on, water she floats in within
    /// a short drive, and a stretch where she is afloat while the fisher can still wade.</para>
    ///
    /// <para><b>No key is ever pressed</b> — a headless fixture cannot drive the keyboard, so the gates
    /// are driven directly (<c>TryEnterDriving</c>, <c>DriveInput</c>, <c>LeaveDriving</c>), which is
    /// the #555 lesson and the same shape <see cref="DriveModePlayTests"/> uses.</para>
    /// </summary>
    public class OtterLaunchPlayTests
    {
        // ---- her published numbers (sidecar; pinned by AmphibiousVehicleTests) ----------------------
        const float FloatSink = 0.52f;          // FLOAT.sink_m_at_float_1 / G.sinkMax
        const float FloatDraft = 0.28f;         // FLOAT.at_float_1.draft_above_keel_m
        const float Hysteresis = 0.06f;         // her def's tuned dead band
        const float WheelRadius = 0.32f;
        const float TrackWidth = 1.26f;
        const float WaterlineHalfBeam = 0.687f;
        const float LowestGunwale = 0.82f;
        const float DoorLocalX = -1.4f;         // INTERACT[id=drive].reach_point
        const float DoorLocalY = 0.2f;

        /// <summary>She lifts here — the same arithmetic <c>VehicleGrounding.IsAfloatAtDepth</c> does
        /// entering the water. Stated once so the fixture cannot drift from the rule.</summary>
        const float FloatsAtDepth = FloatDraft + Hysteresis;

        /// <summary>The owner's ruled wade ceiling (<c>GameConfig.WadeDepth</c>).</summary>
        const float WadeDepth = 0.5f;

        // ---- the measured ramp ----------------------------------------------------------------------

        /// <summary>
        /// The profile measured at her landing: a dry apron, then a slope into the basin shoal, laid
        /// along +x so a test can talk about "how far down the ramp" as one number.
        ///
        /// <para>Elevations and the run are the surveyed ones. The ramp being a straight line here
        /// rather than the region's exact curve is deliberate and harmless: what the loop needs from it
        /// is monotone descent through the two bands, which is the property
        /// <c>NineMileCreekOtterLandingTests</c> asserts of the real ground.</para>
        /// </summary>
        sealed class RampTerrain : ITidalTerrain
        {
            public const float ApronElevation = 3.44f;    // dry at spring high (+2.2) with 1.2 m to spare
            public const float ShoalElevation = -1.60f;   // the basin bed
            public const float RampStartX = 0f;
            public const float RampEndX = 6f;

            public float ElevationAt(Vector2 worldPos)
            {
                if (worldPos.x <= RampStartX) return ApronElevation;
                if (worldPos.x >= RampEndX) return ShoalElevation;
                float t = (worldPos.x - RampStartX) / (RampEndX - RampStartX);
                return Mathf.Lerp(ApronElevation, ShoalElevation, t);
            }

            /// <summary>Where on the ramp the water is exactly this deep, for a given level.</summary>
            public static float XAtDepth(float depth, float waterLevel)
            {
                float wantElevation = waterLevel - depth;
                float t = Mathf.InverseLerp(ApronElevation, ShoalElevation, wantElevation);
                return Mathf.Lerp(RampStartX, RampEndX, t);
            }
        }

        /// <summary>A still water level — the tide is not what this fixture is about.</summary>
        sealed class StillWater : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        readonly List<Object> _spawned = new();
        VehicleMeshDef _mesh;
        VehicleDef _def;
        StillWater _water;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            EventBus.Clear<ControlModeChanged>();

            _water = new StillWater { Level = 0f };          // mean tide
            GameServices.Environment = _water;
            GameServices.TidalTerrain = new RampTerrain();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveVehicleChanged>();
            foreach (Object o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            InteractionGate.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            GameServices.Reset();
        }

        sealed class Rig
        {
            public ControlSwitcher Switcher;
            public PlayerWalkController Walk;
            public VehicleDoor Door;
            public VehicleController Vehicle;
            public GameObject PlayerGo, OtterGo;
        }

        void BuildDefs()
        {
            // ⚠️ A real UnityEngine.Mesh with vertices, because VehicleMeshDef.IsUsable checks for
            // one — and VehicleDoor.IsDrivable refuses an unusable def, so without this she cannot be
            // boarded at all and every test here fails at the first line.
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            mesh.triangles = new[] { 0, 1, 2 };
            _spawned.Add(mesh);

            _mesh = ScriptableObject.CreateInstance<VehicleMeshDef>();
            _spawned.Add(_mesh);
            _mesh.Mesh = mesh;
            _mesh.Id = "vehiclemesh.otter_8x8";
            _mesh.Ramps = new[]
            {
                new HullMeshDef.Ramp { Colors = new Color32[] { new Color32(255, 255, 255, 255) } },
            };
            _mesh.Bayer16 = new float[16];
            _mesh.PxPerMetre = 32;
            _mesh.CellW = 256;
            _mesh.CellH = 192;
            _mesh.WheelbaseMeters = 2.04f;
            _mesh.FrontTrackMeters = TrackWidth;
            _mesh.WheelRadiusMeters = WheelRadius;
            _mesh.FrontAxleY = 1.02f;
            _mesh.RearAxleY = -1.02f;
            _mesh.MaxInnerSteerDegrees = 0f;      // skid steer — no steering axle
            _mesh.MaxOuterSteerDegrees = 0f;
            _mesh.FloatSinkMeters = FloatSink;
            _mesh.FloatDraftMeters = FloatDraft;
            _mesh.WatertightDeckHeightMeters = LowestGunwale;
            _mesh.WatertightHalfBeamMeters = WaterlineHalfBeam;
            _mesh.DriveDoorLocal = new Vector2(DoorLocalX, DoorLocalY);

            _def = ScriptableObject.CreateInstance<VehicleDef>();
            _spawned.Add(_def);
            _def.Id = "vehicle.otter_8x8";
            _def.KindToken = "amphibious_vehicle";
            _def.Mesh = _mesh;
            _def.SwimHysteresisMeters = Hysteresis;
            _def.CameraWorldHeightMeters = 18f;
        }

        Rig BuildRig(Vector3 otterAt)
        {
            BuildDefs();

            var playerGo = new GameObject("Fisher", typeof(SpriteRenderer), typeof(Rigidbody2D));
            playerGo.transform.position = otterAt + new Vector3(DoorLocalX, DoorLocalY, 0f);
            _spawned.Add(playerGo);
            var walk = playerGo.AddComponent<PlayerWalkController>();

            var otterGo = new GameObject("Otter", typeof(Rigidbody2D));
            otterGo.transform.position = otterAt;
            _spawned.Add(otterGo);
            var vehicle = otterGo.AddComponent<VehicleController>();
            vehicle.SetVehicle(_def);
            var door = otterGo.AddComponent<VehicleDoor>();

            var switcherGo = new GameObject("Switcher");
            _spawned.Add(switcherGo);
            var switcher = switcherGo.AddComponent<ControlSwitcher>();
            switcher.Configure(walk, null, null, null, 0f, null);

            return new Rig
            {
                Switcher = switcher, Walk = walk, Door = door, Vehicle = vehicle,
                PlayerGo = playerGo, OtterGo = otterGo,
            };
        }

        /// <summary>
        /// Put her at a depth and let a physics tick see it — the fixture's way of "driving down the
        /// ramp" without asserting anything about how fast she gets there.
        ///
        /// <para>⚠️ <b>The BODY is moved, not just the Transform.</b> <c>Physics2D.autoSyncTransforms</c>
        /// is off by default, so writing <c>transform.position</c> alone leaves
        /// <c>Rigidbody2D.position</c> at the old spot until the next simulation step — and
        /// <c>VehicleController.ReadAfloat</c> samples the BODY. Cost one full PlayMode run: she was
        /// asked whether she floated at a place she had not been moved to yet, and answered honestly.</para>
        ///
        /// <para>The throttle is released and the brake held, so the tick that reads her medium is not
        /// also driving her out of the water being measured.</para>
        /// </summary>
        static IEnumerator MoveTo(Rig rig, float x)
        {
            rig.Switcher.DriveInput(0f, 0f, true);

            var rb = rig.OtterGo.GetComponent<Rigidbody2D>();
            var at = new Vector2(x, 0f);
            rig.OtterGo.transform.position = new Vector3(x, 0f, 0f);
            rb.position = at;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            yield return new WaitForFixedUpdate();
            yield return null;
        }

        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>THE WHOLE LOOP, IN ORDER.</b> Board on the apron · drive down the ramp until she
        /// lifts · step off where the fisher can stand · get back aboard. This is the acceptance the
        /// placement exists to satisfy.
        /// </summary>
        [UnityTest]
        public IEnumerator SheLaunchesFloatsAndIsSteppedOffOntoGroundTheFisherCanStandOn()
        {
            Rig rig = BuildRig(new Vector3(-3f, 0f, 0f));      // on the dry apron

            // --- 1. board her, on dry ground -------------------------------------------------------
            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door),
                "she could not be boarded on the apron — the launch never starts.");
            yield return null;

            Assert.IsFalse(rig.Vehicle.IsAfloat,
                "she reports afloat while parked on ground 3.44 m above the water.");

            // --- 2. drive her down the ramp, still ashore -------------------------------------------
            rig.Switcher.DriveInput(1f, 0f, false);
            yield return MoveTo(rig, RampTerrain.XAtDepth(0.1f, _water.Level));
            Assert.IsFalse(rig.Vehicle.IsAfloat,
                "she lifted in 0.1 m of water — her draft is " + FloatDraft + " m, so she should " +
                "still be on her wheels.");

            // --- 3. drive in until she floats -------------------------------------------------------
            yield return MoveTo(rig, RampTerrain.XAtDepth(FloatsAtDepth + 0.05f, _water.Level));
            Assert.IsTrue(rig.Vehicle.IsAfloat,
                $"she is in {FloatsAtDepth + 0.05f:0.##} m of water and still has not lifted. " +
                "VehicleGrounding says draft + hysteresis, and her def carries both.");

            // --- 4. step off where the fisher can stand ---------------------------------------------
            // Back up the ramp a little: afloat behind her, wadeable under her door.
            yield return MoveTo(rig, RampTerrain.XAtDepth(WadeDepth - 0.05f, _water.Level));

            Assert.IsTrue(rig.Switcher.LeaveDriving(),
                "stepping off into water shallower than the wade ceiling was refused. #572 admits " +
                "Dry, Wade and Swim and refuses only Deep.");
            yield return null;

            float standingDepth = _water.Level -
                new RampTerrain().ElevationAt(rig.PlayerGo.transform.position);
            Assert.That(standingDepth, Is.LessThanOrEqualTo(WadeDepth + 0.25f),
                $"the fisher was set down in {standingDepth:0.##} m of water. He is meant to step off " +
                "into water he can stand in, not to be dropped into a swim.");

            // --- 5. get back aboard ------------------------------------------------------------------
            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door),
                "he could not get back aboard from the water beside her. A launch you can only use " +
                "once is a way of losing the machine.");
        }

        /// <summary>
        /// ⭐ <b>She keeps floating on the way back out</b> — the hysteresis band, driven rather than
        /// reasoned about. A machine that dropped onto her wheels the instant the water shallowed by a
        /// millimetre would judder between the two states at every ripple on the ramp.
        /// </summary>
        [UnityTest]
        public IEnumerator OnceAfloatSheStaysAfloatThroughTheHysteresisBand()
        {
            Rig rig = BuildRig(new Vector3(-3f, 0f, 0f));
            rig.Switcher.TryEnterDriving(rig.Door);

            yield return MoveTo(rig, RampTerrain.XAtDepth(FloatsAtDepth + 0.05f, _water.Level));
            Assert.IsTrue(rig.Vehicle.IsAfloat, "fixture: she must be afloat before the band is tested.");

            // Shallower than she needed to LIFT, but still inside the band she stays UP in.
            yield return MoveTo(rig, RampTerrain.XAtDepth(FloatDraft - Hysteresis + 0.02f, _water.Level));
            Assert.IsTrue(rig.Vehicle.IsAfloat,
                "she dropped onto her wheels inside the hysteresis band — the two thresholds have " +
                "collapsed into one and she will chatter at the waterline.");

            // Past the bottom of the band she takes the ground, which is the point of having one.
            yield return MoveTo(rig, RampTerrain.XAtDepth(FloatDraft - Hysteresis - 0.05f, _water.Level));
            Assert.IsFalse(rig.Vehicle.IsAfloat,
                "she is still floating in water shallower than draft − hysteresis, so nothing ever " +
                "puts her back on her wheels and she would swim up the ramp.");
        }

        /// <summary>
        /// The launch survives the tide moving under it: at a level where the shoal is deep, she still
        /// floats — and the ramp still offers somewhere shallow enough to step off. This is the claim
        /// the site's note makes ("the loop works at any tide above the bare"), driven at a second
        /// water level so it is not a one-tide accident.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLoopStillWorksWithTheTideWellUp()
        {
            _water.Level = 1.5f;
            Rig rig = BuildRig(new Vector3(-3f, 0f, 0f));

            Assert.That(new RampTerrain().ElevationAt(rig.OtterGo.transform.position),
                        Is.GreaterThan(_water.Level),
                        "fixture: the apron should still be dry with the tide at +1.5 m.");
            Assert.IsTrue(rig.Switcher.TryEnterDriving(rig.Door),
                "she could not be boarded with the tide up.");

            yield return MoveTo(rig, RampTerrain.XAtDepth(FloatsAtDepth + 0.05f, _water.Level));
            Assert.IsTrue(rig.Vehicle.IsAfloat, "she does not float at a higher tide.");

            yield return MoveTo(rig, RampTerrain.XAtDepth(WadeDepth - 0.05f, _water.Level));
            Assert.IsTrue(rig.Switcher.LeaveDriving(),
                "with the tide up there is no longer anywhere on this ramp to step off — the wade " +
                "band has run off the top of the slope.");
        }
    }
}
