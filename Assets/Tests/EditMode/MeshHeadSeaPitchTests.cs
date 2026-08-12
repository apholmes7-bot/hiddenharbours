using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE MESH HULL'S ORDINARY-SEA FORE/AFT PITCH</b> (owner report 2026-08-11: the fore/aft
    /// rocking of hulls in waves reads as minimal).
    ///
    /// <para>A mesh hull's whole ordinary-sea pitch was the rig's CANNED cycle, and the cycle has
    /// two properties that between them produce the owner's read:</para>
    ///
    /// <list type="number">
    ///   <item><b>Roll-dominant by authorship</b> — every rig declares pitchA ≈ 0.55–0.60 × rollA,
    ///   so a dory pitches 3° and a lobster boat 1.6°. At the 40° bake that is a 2.9–4.1 px lift at the
    ///   stem on hulls 150–370 px long.</item>
    ///   <item><b>Heading-blind</b> — <c>HullMeshMath.RockPose</c> is roll = rollA·sin(θ),
    ///   pitch = pitchA·cos(θ), the same corkscrew bows-on to the swell as beam-on to it.</item>
    /// </list>
    ///
    /// <para>The honest heading-decomposed attitude did exist, but only above the storm gate:
    /// <c>StormRockMath.StormBlend01</c> is exactly 0 below <c>StormStartSeaState01</c> = 0.4, so in
    /// every ordinary sea the decomposition never reached the pose. These tests pin the channel that
    /// opens it below that gate — and, first, the negative control that keeps the owner's A/B
    /// honest.</para>
    ///
    /// <para>Headless, GPU-free, deterministic (rule 5): scripted clock, scripted sea, recording
    /// renderer, real component tick bodies — the <c>StormSeakeepingReadTests</c> harness pattern.</para>
    /// </summary>
    public class MeshHeadSeaPitchTests
    {
        const float Dt = 1f / 60f;
        const int Frames = 600;                      // 10 s of sail
        const int PxPerMetre = 32;
        const float RigElevationDegrees = 40f;
        // Below StormRockSettings.Default.StormStartSeaState01 (0.4): an ORDINARY working sea, where
        // the storm channel contributes exactly nothing and the canned cycle was all there was.
        const float OrdinarySeaState = 0.3f;
        // The scripted wind, and therefore the swell axis the boat is turned against.
        static readonly Vector2 Wind = new Vector2(6f, 3f);

        readonly object _seaOwner = new object();
        readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            DisplacedSea.Clear(_seaOwner);
            GameServices.Reset();
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ------------------------------------------------------------------ doubles

        sealed class ScriptedClock : IGameClock
        {
            public double TotalSeconds { get; private set; }
            public void Advance(double dt) => TotalSeconds += dt;
            public GameTime Now => new GameTime(TotalSeconds);
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public int DayIndex => 0;
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => 0f;
            public float HourOfDay => 12f;
            public void SeekTo(double totalSeconds) => TotalSeconds = totalSeconds;
        }

        sealed class ScriptedSea : IEnvironmentService
        {
            public float SeaState01 = OrdinarySeaState;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Wind, Vector2.zero, tideHeight: 0f, HiddenHarbours.Core.SeaState.Moderate,
                visibility: 1f, seaState01: SeaState01);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        sealed class RecordingRenderer : IHullMeshRenderer
        {
            public float HeadingDirUnits { get; set; }
            public float RollDegrees { get; set; }
            public float PitchDegrees { get; set; }
            public float HeavePixels { get; set; }
            public float RidePixels { get; set; }
            public bool IsConfigured => true;
            public void SetSorting(int sortingLayerId, int sortingOrder) { }
            public void SetDeckOccupant(Vector3 rigLocalMeters, bool active) { }
            public float DeckOccluderId => 0f;
            public IDeckOccupantSlots DeckOccupants => NoDeckOccupantSlots.Instance;
        }

        // ------------------------------------------------------------------ the mesh chain

        sealed class MeshRig : System.IDisposable
        {
            public readonly GameObject Root;
            public readonly RecordingRenderer Renderer = new RecordingRenderer();
            public readonly MeshHullDriver Driver;
            public readonly BoatWaveMotion Wave;
            public readonly ScriptedClock Clock = new ScriptedClock();
            readonly HullMeshDef _def;

            /// <param name="bowDirection">Where the bow points (transform.up) — the axis the
            /// head-sea decomposition takes its bow share along.</param>
            /// <param name="headSeaPitchPerSlope">The new serialized tunable, written by reflection:
            /// the field is private, and its NAME is the Inspector-override contract.</param>
            public MeshRig(float seaState01, float rockPitchDegrees, Vector2 bowDirection,
                           float headSeaPitchPerSlope)
            {
                GameServices.Clock = Clock;
                GameServices.Environment = new ScriptedSea { SeaState01 = seaState01 };

                Root = new GameObject("Boat");
                Root.transform.up = new Vector3(bowDirection.x, bowDirection.y, 0f).normalized;
                var visual = new GameObject("Visual");
                visual.transform.SetParent(Root.transform, false);

                _def = ScriptableObject.CreateInstance<HullMeshDef>();
                _def.ElevationDeg = RigElevationDegrees;
                _def.AzimuthCounterClockwise = true;
                _def.RockRollDegrees = 2.8f;              // lobster boat — the owner's working hull
                _def.RockPitchDegrees = rockPitchDegrees;
                _def.RockHeavePixels = 0f;
                _def.PxPerMetre = PxPerMetre;
                _def.RestingDraftMeters = 0f;

                Driver = Root.AddComponent<MeshHullDriver>();
                Driver.Configure(visual.transform, Renderer, _def, zeroHeadingDegrees: 0f);
                Wave = Root.AddComponent<BoatWaveMotion>();
                SetPrivateFloat(Wave, "_meshHeadSeaPitchDegreesPerSlope", headSeaPitchPerSlope);
                Wave.Configure(visual.transform, new MeshHullPresenter(Driver));
            }

            public void Tick(float dt = Dt)
            {
                Clock.Advance(dt);
                Wave.Tick();
                Driver.Drive();
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(_def);
            }
        }

        static void SetPrivateFloat(object target, string fieldName, float value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field,
                $"serialized tunable '{fieldName}' is gone — renaming it silently discards the " +
                "owner's Inspector overrides (rule 6: amplitudes are data he can edit)");
            field.SetValue(target, value);
        }

        static void SailMesh(MeshRig rig, int frames, out float[] roll, out float[] pitch)
        {
            for (int w = 0; w < 5; w++) rig.Tick();      // animator warm-up (snap frame)
            roll = new float[frames];
            pitch = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                rig.Tick();
                roll[f] = rig.Renderer.RollDegrees;
                pitch[f] = rig.Renderer.PitchDegrees;
            }
        }

        static float MaxAbs(float[] series)
        {
            float max = 0f;
            for (int i = 0; i < series.Length; i++) max = Mathf.Max(max, Mathf.Abs(series[i]));
            return max;
        }

        static int SignChanges(float[] series)
        {
            int changes = 0;
            for (int i = 1; i < series.Length; i++)
                if (Mathf.Sign(series[i]) != Mathf.Sign(series[i - 1])) changes++;
            return changes;
        }

        // ------------------------------------------------------------------ the negative control

        [Test]
        public void ShippedDefault_IsZero_SoTheOwnerABsAtStrengthZero()
        {
            var go = new GameObject("BoatWaveMotionDefaultsProbe");
            _spawned.Add(go);
            var motion = go.AddComponent<BoatWaveMotion>();
            FieldInfo field = typeof(BoatWaveMotion).GetField(
                "_meshHeadSeaPitchDegreesPerSlope", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "the tunable's name is the Inspector-override contract");
            Assert.AreEqual(0f, (float)field.GetValue(motion), 1e-6f,
                "the head-sea pitch channel must ship OFF: 0 is the owner's A/B, and off must be " +
                "the canned cycle alone, exactly as the fleet shipped");
        }

        [Test]
        public void AtZero_TheDrawnPitchIsTheCannedCycleAlone()
        {
            // The negative control every other assertion leans on. Below the storm gate and with the
            // channel at its shipped 0, the amplitude the renderer is handed is exactly the def's
            // RockPitchDegrees — the rig's own transcribed pitchA, nothing added.
            const float cannedPitch = 1.6f;              // lobster boat
            using var rig = new MeshRig(OrdinarySeaState, cannedPitch, Wind, headSeaPitchPerSlope: 0f);
            SailMesh(rig, Frames, out _, out float[] pitch);

            Assert.AreEqual(cannedPitch, MaxAbs(pitch), 1e-3f,
                "with the channel off, an ordinary sea must draw the canned pitch amplitude and no " +
                "more — this is the byte-identical A/B the owner tunes against");
        }

        // ------------------------------------------------------------------ the fix

        [Test]
        public void InAnOrdinarySea_TheChannelMakesTheHullPitchMore()
        {
            // The headline. Same sea, same hull, same heading — the ONE axis that differs is the
            // channel. Below the storm gate the old path could not answer the sea at all.
            const float cannedPitch = 1.6f;
            float off, on;
            using (var rig = new MeshRig(OrdinarySeaState, cannedPitch, Wind, 0f))
            {
                SailMesh(rig, Frames, out _, out float[] pitch);
                off = MaxAbs(pitch);
            }
            GameServices.Reset();
            using (var rig = new MeshRig(OrdinarySeaState, cannedPitch, Wind, 14f))
            {
                SailMesh(rig, Frames, out _, out float[] pitch);
                on = MaxAbs(pitch);
            }

            Debug.Log($"[head-sea pitch] ordinary sea {OrdinarySeaState}: canned {off:F2}° → {on:F2}°");
            Assert.Greater(on, off * 1.25f,
                "the channel must make the fore/aft read materially bigger in an ORDINARY sea — " +
                "'the pitch reads as minimal' is the owner's report, verbatim");
        }

        [Test]
        public void TheChannel_AnswersTheHeading_WhichTheCannedCycleCannot()
        {
            // What the canned cycle is structurally blind to: a hull driving INTO the swell should
            // pitch, a hull lying across it should not. bowShare is the decomposition that knows.
            const float cannedPitch = 1.6f;
            Vector2 acrossTheSwell = new Vector2(-Wind.y, Wind.x);   // 90° off the wave axis
            float headOn, beamOn, cannedHeadOn, cannedBeamOn;

            using (var rig = new MeshRig(OrdinarySeaState, cannedPitch, Wind, 14f))
            {
                SailMesh(rig, Frames, out _, out float[] pitch);
                headOn = MaxAbs(pitch);
            }
            GameServices.Reset();
            using (var rig = new MeshRig(OrdinarySeaState, cannedPitch, acrossTheSwell, 14f))
            {
                SailMesh(rig, Frames, out _, out float[] pitch);
                beamOn = MaxAbs(pitch);
            }
            GameServices.Reset();
            // …and the control: with the channel off, the two headings are indistinguishable, which
            // IS limiter (2) measured rather than asserted.
            using (var rig = new MeshRig(OrdinarySeaState, cannedPitch, Wind, 0f))
            {
                SailMesh(rig, Frames, out _, out float[] pitch);
                cannedHeadOn = MaxAbs(pitch);
            }
            GameServices.Reset();
            using (var rig = new MeshRig(OrdinarySeaState, cannedPitch, acrossTheSwell, 0f))
            {
                SailMesh(rig, Frames, out _, out float[] pitch);
                cannedBeamOn = MaxAbs(pitch);
            }

            Debug.Log($"[head-sea pitch] head-on {headOn:F2}° vs beam-on {beamOn:F2}° " +
                      $"(canned cycle: {cannedHeadOn:F2}° vs {cannedBeamOn:F2}°)");
            Assert.AreEqual(cannedHeadOn, cannedBeamOn, 1e-3f,
                "the canned cycle draws the SAME pitch at every heading — the blindness this fixes");
            Assert.Greater(headOn, beamOn,
                "with the channel live, driving into the swell must pitch more than lying across it");
        }

        [Test]
        public void TheChannel_LeavesRollExactlyAlone()
        {
            // Scope discipline: the owner reported pitch. The roll is his tuned read and the canned
            // cycle's own, and this must not have touched it.
            float[] off, on;
            using (var rig = new MeshRig(OrdinarySeaState, 1.6f, Wind, 0f))
                SailMesh(rig, 240, out off, out _);
            GameServices.Reset();
            using (var rig = new MeshRig(OrdinarySeaState, 1.6f, Wind, 14f))
                SailMesh(rig, 240, out on, out _);

            for (int f = 0; f < off.Length; f++)
                Assert.AreEqual(off[f], on[f], 1e-6f,
                    $"frame {f}: the head-sea pitch channel must not disturb the roll by one bit");
        }

        [Test]
        public void TheChannel_AddsNoNewFrequencyContent()
        {
            // The MeshRockSmoothness lesson (CI run 30968839931): the first B2.5 cut drove its
            // extras from a DIFFERENT frequency mix from the canned cycle and the renderer's
            // roll/pitch stopped being one cycle's sine/cosine pair. This term cannot do that — it
            // rides cos() of the very phase the canned pitch is posed at, so the sum is pure
            // amplitude modulation. Measured as: the same number of zero crossings over the same
            // sail, i.e. the same cycle count, at 10× the amplitude.
            const float cannedPitch = 1.6f;
            float[] off, on;
            using (var rig = new MeshRig(OrdinarySeaState, cannedPitch, Wind, 0f))
                SailMesh(rig, Frames, out _, out off);
            GameServices.Reset();
            using (var rig = new MeshRig(OrdinarySeaState, cannedPitch, Wind, 40f))
                SailMesh(rig, Frames, out _, out on);

            Assert.AreEqual(SignChanges(off), SignChanges(on),
                "the drawn pitch must stay ONE clean cycle — a changed crossing count is exactly " +
                "the harmonics the smoothness pins read as a pop");
            Assert.Greater(MaxAbs(on), MaxAbs(off),
                "harness: the arm must actually be driving the channel hard");
        }
    }
}
