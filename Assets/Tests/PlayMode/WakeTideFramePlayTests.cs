using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.PlayMode
{
    // Coroutines resume before LateUpdate. Sample the completed pose AND wake together,
    // after the presenter and emitter, rather than comparing last frame's foam to new physics.
    [DefaultExecutionOrder(1000)]
    public sealed class WakeCompletedFrameProbe : MonoBehaviour
    {
        public System.Action Read;
        private void LateUpdate() => Read?.Invoke();
    }

    /// <summary>
    /// The wake is DRAWN in the tide frame. The owner, 09-18: <i>"the white water discolouration in the 3 band
    /// wakes was still off cetnre on the intro boat"</i>.
    ///
    /// <para><b>What was wrong.</b> A hull's picture rides the tide: <see cref="BoatWaveMotion"/> moves her visual
    /// child up-screen by <see cref="HullTideRide.ScreenRiseNow"/>, so off her baked waterline she draws above or
    /// below her datum root. The wake was laid from that root and drawn where it was laid, so all of it sat off
    /// the stern the player sees by the tide's drawn rise. The owner's ruling (09-18, fix 1): the emitter adds the
    /// water's drawn rise where each stream is DRAWN, never where it is laid, and the astern drift is unchanged.</para>
    ///
    /// <para><b>Guard 1: the laid foam rides the tide with her picture.</b> A skiff lays a trail, is held still,
    /// and the water jumps from −1 m to +1 m under her. Each live puff's screen height is read one tick, then two
    /// ticks, apart, and its residual against a straight-line prediction must match her visual child's within
    /// 1 px. Over three ticks the drift and the dispersal are near-linear, so they cancel and only the tide is
    /// left: a wake drawn in the datum frame misses by the whole rise.</para>
    ///
    /// <para><b>Guard 2: a newborn centre-lane puff is laid on her picture's stern</b>, heading north, east and
    /// south-west, and through a turn that passes the intro's 259.8°, with her baked waterline 1.5 m above the
    /// water. (A) At least five newborns lie within 1 px of her picture's swept stern segment. (B) That 2-px
    /// stripe, 1 px either side of the stern line, holds more newborns than any 2-px stripe of the rest, so the
    /// densest lane is on her stern and not merely touching it.</para>
    ///
    /// <para><b>Why her picture's stern and not the foam injector's lift root.</b> The ruled bar names the mesh
    /// hull's injector root. That injector sits on the visual child that <c>MeshHullDriver.Drive</c> resets to
    /// identity rotation every frame (<c>MeshHullDriver.cs:260</c>), so its root (<c>FoamInjector.cs:191-192</c>)
    /// always reads screen-south of her and its lift axis (<c>:564</c>) screen-north, whatever her heading. That is
    /// a second fault, reported to the owner and not fixed here, and a guard read off it would pin it. This guard
    /// uses a plain host instead: the emitter takes her stern as half her length plus the trail's astern nudge,
    /// in plan view, and the bar is that same point read off her drawn picture.</para>
    ///
    /// <para><b>Premises</b>, asserted where they can be and named in every failure: no displaced sea (so no sea
    /// lift), a calm sample (so no wave distortion), a flat bed far below (she is afloat, so her waterline is the
    /// water), one emitter tick per pinned 1/20 s frame, at most four rigs (so the legs run one boat at a time),
    /// and a ring pool (so a newborn is a slot that was dark, or that jumped).</para>
    /// </summary>
    public class WakeTideFramePlayTests
    {
        private const float Px = 1f / 32f;                  // one screen pixel in metres (32 px to the metre)
        private const float FrameSeconds = 1f / 20f;        // longer than the emitter's 1/30 s tick: one tick a frame
        private const float DriveSpeed = 3f;                // m/s, over the trail's speed gate
        private const float SkiffLengthMetres = 7f;
        // A plain host's stern: half her length (no presenter rigs one) plus the trail's 0.15 m astern nudge,
        // in plan view (no presenter bakes an elevation).
        private const float SternFromOriginMetres = SkiffLengthMetres * 0.5f + 0.15f;
        private const float RebirthJumpMetres = 0.5f;       // a live slot that moved this far in one tick was reborn
        private const float LayTrailSeconds = 1.2f;
        private const float LegSeconds = 2f;
        private const float RigWaitSeconds = 3f;            // the emitter rescans once a second
        private const float IntroCompassDegrees = 259.8f;   // the intro boat's heading on the owner's plate
        private const float TurnDegreesPerSecond = 40f;     // to port: 80° over the leg, centred on the intro's

        private const string Premises =
            "Premises: a plain host, so the emitter's stern is half her 7 m length plus the trail's 0.15 m " +
            "astern nudge, in plan view; no displaced sea; a calm sample; a flat bed 10 m down; one emitter tick " +
            "per pinned 1/20 s frame; at most four rigs, so one boat per leg; a ring pool, so a newborn is a slot " +
            "that was dark or jumped more than 0.5 m. Her picture's stern is the bar, not the mesh foam " +
            "injector's root, which MeshHullDriver.cs:260 holds at identity rotation (reported, not fixed here).";

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation = -4f;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        /// <summary>A deterministic tide on the seam the whole game reads, with a calm sample: no wind, no
        /// current, <c>SeaState01 = 0</c>, so the emitter's roughness and wave distortion are zero.</summary>
        private sealed class TidalEnv : IEnvironmentService
        {
            public Vector2 Wind = Vector2.zero;
            public Vector2 Current = Vector2.zero;
            public float BaseLevel;            // water level (m above datum) at t = 0
            public float RisePerSecond;

            public int WorldSeed => 7;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Wind, Current, 0f, SeaState.Calm, 1f, 0f);
            public float TideHeightAt(double totalSeconds) => WaterLevelAt(totalSeconds);
            public float WaterLevelAt(double totalSeconds) => BaseLevel + RisePerSecond * (float)totalSeconds;
        }

        /// <summary>A clock that stands still, so the water is exactly what a test sets.</summary>
        private sealed class TestClock : IGameClock
        {
            public double Fixed;

            public double TotalSeconds => Fixed;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        /// <summary>Every "foam" slot of one rig, read at one resume: drawn or dark, and where it is drawn.</summary>
        private struct FoamFrame
        {
            public bool[] Live;
            public Vector2[] Pos;
        }

        private readonly struct Leg
        {
            public readonly string Name;
            public readonly float StartZ;
            public readonly float TurnDegreesPerSecond;

            public Leg(string name, float startZ, float turnDegreesPerSecond)
            {
                Name = name;
                StartZ = startZ;
                TurnDegreesPerSecond = turnDegreesPerSecond;
            }
        }

        private FlatTerrain _terrain;
        private TidalEnv _env;
        private TestClock _clock;
        private GameObject _ownEmitter;
        private GameObject _rigRoot;
        private int _ticksSeen;
        private int _framesSeen;
        private float _timeScaleBefore;
        private float _captureBefore;
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            HelmKeyCapture.Reset();
            EventBus.Clear<ControlModeChanged>();

            _terrain = new FlatTerrain { Elevation = -10f };
            _env = new TidalEnv { BaseLevel = -1f };
            _clock = new TestClock();
            Pin();

            // Pin the frame: 1/20 s is longer than the emitter's 1/30 s tick, so every frame is exactly one tick.
            _timeScaleBefore = Time.timeScale;
            _captureBefore = Time.captureDeltaTime;
            Time.timeScale = 1f;
            Time.captureDeltaTime = FrameSeconds;

            EnsureEmitter();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = _captureBefore;
            Time.timeScale = _timeScaleBefore;
            EventBus.Clear<ControlModeChanged>();
            InteractionGate.Reset();
            HelmKeyCapture.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            if (_ownEmitter != null) Object.Destroy(_ownEmitter);
            _ownEmitter = null;
            _rigRoot = null;
            yield return null;          // let the destroys land, so the next fixture's emitter scan finds none of ours
            GameServices.Reset();
        }

        [UnityTest]
        public IEnumerator TheLaidFoam_RidesTheTideWithTheHullsPicture_WhenTheWaterRisesTwoMetres()
        {
            Assert.IsFalse(DisplacedSea.IsActive,
                "premise: no displaced sea is live, so the emitter's sea lift is off and the tide is the only " +
                "thing that moves a held puff's picture up-screen");

            const string name = "TideFrameRise";
            GameObject go = MakeRiggedBoat(name, 0f, 0f, out Transform visual);
            var boat = go.GetComponent<BoatController>();
            var rb = go.GetComponent<Rigidbody2D>();

            yield return WaitForRig(name);
            Assert.IsNotNull(_rigRoot, $"premise: the emitter built no rig Wake[{name}] within {RigWaitSeconds} s; " +
                                       "it keeps at most four, so is another test's boat still alive?");
            List<Transform> foam = FoamSlots(_rigRoot);

            // Lay a trail heading north. She is posed by hand, as the deposit guards pose her, with the velocity set
            // as well because the emitter's speed gate reads it; the body's own physics step moves her too.
            for (float elapsed = 0f; elapsed < LayTrailSeconds; )
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                Vector2 course = (Vector2)go.transform.up * DriveSpeed;
                go.transform.position += (Vector3)(course * dt);
                rb.linearVelocity = course;
                rb.angularVelocity = 0f;
                yield return null;
                Pin();
            }

            // Then hold her still: no puff is born, and every live one only drifts and disperses.
            Vector3 at = go.transform.position;
            for (int i = 0; i < 2; i++)
            {
                Hold(go, rb, at, 0f);
                yield return null;
                Pin();
            }

            FoamFrame f0 = Snapshot(foam);
            float v0 = visual.position.y;
            float s0 = boat.Velocity.magnitude;

            yield return HoldUntilTicks(go, rb, at, 0f, foam, 1, 1f);
            int ticks01 = _ticksSeen, frames01 = _framesSeen;
            FoamFrame f1 = Snapshot(foam);
            float v1 = visual.position.y;
            float s1 = boat.Velocity.magnitude;

            _env.BaseLevel = 1f;            // the water jumps two metres under her
            yield return HoldUntilTicks(go, rb, at, 0f, foam, 2, 1f);
            int ticks12 = _ticksSeen, frames12 = _framesSeen;
            FoamFrame f2 = Snapshot(foam);
            float v2 = visual.position.y;
            float s2 = boat.Velocity.magnitude;

            Assert.IsTrue(ticks01 == 1 && frames01 == 1 && ticks12 == 2 && frames12 == 2,
                $"premise: one emitter tick per pinned frame, then one tick and two ticks between the reads " +
                $"(saw {ticks01} ticks in {frames01} frames, then {ticks12} in {frames12})");
            Assert.LessOrEqual(Mathf.Max(s0, Mathf.Max(s1, s2)), 0.05f,
                "premise: she is held still at every read, so no puff is born between them");
            Assert.GreaterOrEqual(v2 - v1, 1f,
                $"premise: her picture rode the two-metre rise at least a metre up-screen (it moved {v2 - v1:F3} m); " +
                "the visual child is the tide frame the foam must follow");

            // Her picture's residual against a straight line through the first two reads is the tide alone.
            float visualResidual = (v2 - v1) - 2f * (v1 - v0);
            int compared = 0;
            int worstSlot = -1;
            float worst = 0f;
            for (int i = 0; i < foam.Count; i++)
            {
                if (!f0.Live[i] || !f1.Live[i] || !f2.Live[i]) continue;
                float residual = (f2.Pos[i].y - f1.Pos[i].y) - 2f * (f1.Pos[i].y - f0.Pos[i].y);
                float miss = Mathf.Abs(residual - visualResidual);
                compared++;
                if (miss > worst)
                {
                    worst = miss;
                    worstSlot = i;
                }
            }

            Assert.GreaterOrEqual(compared, 3, "premise: at least three puffs are live across all three reads");
            Assert.LessOrEqual(worst, Px,
                $"a laid puff did not ride the tide with her picture: slot {worstSlot} missed by {worst:F4} m " +
                $"({worst / Px:F1} px) while her picture moved {visualResidual:F4} m, over {compared} live puffs. " +
                "A wake drawn in the datum frame misses by the whole rise. " + Premises);
        }

        [UnityTest]
        public IEnumerator ANewbornCentreLanePuff_IsLaidOnThePicturesStern_OnThreeHeadingsAndATurn()
        {
            Assert.IsFalse(DisplacedSea.IsActive,
                "premise: no displaced sea is live, so the emitter's sea lift is off and the tide is the only " +
                "thing between the laid foam and her picture");

            _env.BaseLevel = -1f;
            const float baked = 0.5f;       // her baked waterline 1.5 m above the water: she draws well below her root

            float introZ = 360f - IntroCompassDegrees;   // compass is clockwise from north; Z turns anticlockwise
            var legs = new[]
            {
                new Leg("TideFrameNorth", 0f, 0f),
                new Leg("TideFrameEast", -90f, 0f),
                new Leg("TideFrameSouthWest", 135f, 0f),
                new Leg("TideFrameTurn", introZ - 0.5f * TurnDegreesPerSecond * LegSeconds, TurnDegreesPerSecond),
            };

            var failures = new List<string>();
            var report = new List<string>();
            foreach (Leg leg in legs)
                yield return RunLeg(leg, baked, failures, report);

            Assert.IsEmpty(failures, string.Join("\n", failures) + "\n" + string.Join("\n", report) + "\n" +
                                     Premises);
        }

        /// <summary>One boat, one heading (or one turn): drive her, and read every puff born on each tick
        /// against the stern segment her picture swept on that tick.</summary>
        private IEnumerator RunLeg(Leg leg, float baked, List<string> failures, List<string> report)
        {
            GameObject go = MakeRiggedBoat(leg.Name, baked, leg.StartZ, out Transform visual);
            var boat = go.GetComponent<BoatController>();
            var rb = go.GetComponent<Rigidbody2D>();

            yield return WaitForRig(leg.Name);
            if (_rigRoot == null)
            {
                failures.Add($"{leg.Name}: premise: the emitter built no rig Wake[{leg.Name}] within " +
                             $"{RigWaitSeconds} s (it keeps at most four)");
                Object.Destroy(go);
                yield break;
            }
            List<Transform> foam = FoamSlots(_rigRoot);

            Vector2 completedStern = default;
            FoamFrame completedFoam = default;
            var probe = go.AddComponent<WakeCompletedFrameProbe>();
            probe.Read = () =>
            {
                completedStern = PictureStern(go.transform, visual);
                completedFoam = Snapshot(foam);
            };

            // One held frame: the emitter's next tick takes this pose as her previous stern, and so does the guard.
            float z = leg.StartZ;
            Hold(go, rb, go.transform.position, z);
            yield return null;
            Pin();

            Vector2 sternBefore = completedStern;
            FoamFrame before = completedFoam;
            var abeam = new List<float>();
            int ticks = 0, newborns = 0, inSegment = 0, onStern = 0;
            float worstSlip = 0f;

            for (float elapsed = 0f; elapsed < LegSeconds; )
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                z += leg.TurnDegreesPerSecond * dt;
                go.transform.rotation = Quaternion.Euler(0f, 0f, z);
                Vector2 course = (Vector2)go.transform.up * DriveSpeed;
                go.transform.position += (Vector3)(course * dt);
                rb.linearVelocity = course;
                rb.angularVelocity = 0f;
                yield return null;
                Pin();

                // Read what this frame's tick saw: the physics step has moved her since the pose, so her stern is
                // read off her picture, never predicted.
                worstSlip = Mathf.Max(worstSlip, Mathf.Abs(Mathf.DeltaAngle(go.transform.eulerAngles.z, z)));
                Vector2 stern = completedStern;
                float speed = boat.Velocity.magnitude;
                FoamFrame now = completedFoam;
                if (!Ticked(before, now))
                {
                    before = now;
                    continue;
                }
                ticks++;

                Vector2 swept = sternBefore - stern;
                float sweptLength = swept.magnitude;
                if (sweptLength < 1e-4f)
                {
                    sternBefore = stern;
                    before = now;
                    continue;
                }
                Vector2 astern = swept / sweptLength;
                var across = new Vector2(-astern.y, astern.x);
                // A puff is laid on the segment and then drifts astern for its first tick.
                float alongMax = sweptLength + speed * dt + Px;

                for (int i = 0; i < foam.Count; i++)
                {
                    if (!now.Live[i]) continue;
                    bool born = !before.Live[i] ||
                                (now.Pos[i] - before.Pos[i]).sqrMagnitude > RebirthJumpMetres * RebirthJumpMetres;
                    if (!born) continue;
                    newborns++;

                    Vector2 offset = now.Pos[i] - stern;
                    float along = Vector2.Dot(offset, astern);
                    if (along < -Px || along > alongMax) continue;
                    float off = Vector2.Dot(offset, across);
                    inSegment++;
                    abeam.Add(off);
                    if (Mathf.Abs(off) <= Px) onStern++;
                }

                sternBefore = stern;
                before = now;
            }

            // (B): the stern line's own stripe against the densest 2-px stripe of the newborns off it.
            var offStern = new List<float>();
            foreach (float off in abeam)
                if (Mathf.Abs(off) > Px) offStern.Add(off);
            int densestOff = DensestStripe(offStern, 2f * Px, out float densestOffCentre);

            report.Add($"{leg.Name}: {ticks} ticks, {newborns} newborns, {inSegment} on the swept segment, " +
                       $"{onStern} within 1 px of the stern line, the densest 2-px stripe off it holds {densestOff} " +
                       $"(centred {densestOffCentre / Px:F1} px abeam), median abeam {Median(abeam) / Px:F1} px, " +
                       $"worst heading slip {worstSlip:F2}°");
            if (worstSlip > 0.5f)
                failures.Add($"{leg.Name}: premise: her physics turned her {worstSlip:F2}° off the posed heading");
            if (onStern < 5)
                failures.Add($"{leg.Name}: (A) only {onStern} newborn puffs lie within 1 px of her picture's " +
                             "stern segment; at least 5 must");
            if (onStern <= densestOff)
                failures.Add($"{leg.Name}: (B) the stern line's 2-px stripe holds {onStern} newborns, not more " +
                             $"than the {densestOff} of the densest stripe off it");

            Object.Destroy(go);
            yield return null;
            Pin();
        }

        private void Pin()
        {
            GameServices.TidalTerrain = _terrain;
            GameServices.Environment = _env;
            GameServices.Clock = _clock;
        }

        private void EnsureEmitter()
        {
            var existing = Object.FindObjectsByType<BoatWakeEmitter>(FindObjectsInactive.Include,
                                                                     FindObjectsSortMode.None);
            if (existing != null && existing.Length > 0) return;
            _ownEmitter = new GameObject("BoatWakeEmitter[test]");
            _ownEmitter.AddComponent<BoatWakeEmitter>();
        }

        private static BoatHullDef Skiff()
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.test_skiff";
            hull.LengthMeters = SkiffLengthMetres;
            hull.MassKg = 950f;
            hull.DraughtMeters = 0f;
            return hull;
        }

        /// <summary>A plain-hosted skiff whose picture rides the tide: a <see cref="HullTideRide"/> baked at
        /// <paramref name="bakedWaterline"/>, and a visual child moved by <see cref="BoatWaveMotion"/> with the
        /// swell off, so the tide is all that moves it.</summary>
        private GameObject MakeRiggedBoat(string name, float bakedWaterline, float headingZ, out Transform visual)
        {
            var go = new GameObject(name);
            go.transform.rotation = Quaternion.Euler(0f, 0f, headingZ);
            _spawned.Add(go);

            var boat = go.AddComponent<BoatController>();      // RequireComponent adds the Rigidbody2D
            BoatHullDef hull = Skiff();
            _spawned.Add(hull);
            boat.SetHull(hull);
            var rb = go.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;

            go.AddComponent<HullTideRide>().Configure(bakedWaterline);

            visual = new GameObject("visual").transform;
            visual.SetParent(go.transform, false);
            var motion = go.AddComponent<BoatWaveMotion>();
            motion.Configure(visual, (IBoatHullPresenter)null);
            motion.MasterStrength = 0f;
            return go;
        }

        private IEnumerator WaitForRig(string boatName)
        {
            _rigRoot = null;
            for (float waited = 0f; waited < RigWaitSeconds; waited += Time.deltaTime)
            {
                _rigRoot = GameObject.Find($"Wake[{boatName}]");
                if (_rigRoot != null) yield break;
                yield return null;
                Pin();
            }
        }

        private static List<Transform> FoamSlots(GameObject rigRoot)
        {
            var slots = new List<Transform>();
            foreach (Transform child in rigRoot.transform)
                if (child.name == "foam") slots.Add(child);
            return slots;
        }

        private static FoamFrame Snapshot(List<Transform> slots)
        {
            var frame = new FoamFrame { Live = new bool[slots.Count], Pos = new Vector2[slots.Count] };
            for (int i = 0; i < slots.Count; i++)
            {
                frame.Live[i] = slots[i].gameObject.activeSelf;
                frame.Pos[i] = slots[i].position;
            }
            return frame;
        }

        /// <summary>Did the emitter tick between two reads? The foam is only drawn on a tick, and every live puff
        /// disperses on every tick, so a tick shows as a slot lit, a slot gone dark, or a live slot moved. With
        /// nothing live there is nothing to read it by, and the pinned frame ticks every time.</summary>
        private static bool Ticked(FoamFrame before, FoamFrame after)
        {
            bool anyLive = false;
            for (int i = 0; i < after.Live.Length; i++)
            {
                if (before.Live[i] != after.Live[i]) return true;
                if (!after.Live[i]) continue;
                anyLive = true;
                if ((after.Pos[i] - before.Pos[i]).sqrMagnitude > 1e-10f) return true;
            }
            return !anyLive;
        }

        private static void Hold(GameObject go, Rigidbody2D rb, Vector3 at, float z)
        {
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, z));
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        private IEnumerator HoldUntilTicks(GameObject go, Rigidbody2D rb, Vector3 at, float z,
                                           List<Transform> foam, int ticks, float maxSeconds)
        {
            _ticksSeen = 0;
            _framesSeen = 0;
            FoamFrame before = Snapshot(foam);
            for (float waited = 0f; _ticksSeen < ticks && waited < maxSeconds; )
            {
                Hold(go, rb, at, z);
                yield return null;
                Pin();
                waited += Time.deltaTime;
                _framesSeen++;
                FoamFrame now = Snapshot(foam);
                if (Ticked(before, now)) _ticksSeen++;
                before = now;
            }
        }

        /// <summary>Her stern as her PICTURE draws it: her drawn visual less the plain host's stern offset along
        /// her bow.</summary>
        private static Vector2 PictureStern(Transform root, Transform visual)
            => (Vector2)visual.position - (Vector2)root.up * SternFromOriginMetres;

        /// <summary>The most values any closed stripe <paramref name="width"/> wide holds, and its centre. A
        /// densest stripe can always slide up until its low edge sits on a value, so trying each value as the low
        /// edge is exact.</summary>
        private static int DensestStripe(List<float> values, float width, out float centre)
        {
            centre = float.NaN;
            var sorted = new List<float>(values);
            sorted.Sort();
            int best = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                int n = 0;
                for (int j = i; j < sorted.Count && sorted[j] <= sorted[i] + width; j++) n++;
                if (n > best)
                {
                    best = n;
                    centre = sorted[i] + 0.5f * width;
                }
            }
            return best;
        }

        private static float Median(List<float> values)
        {
            if (values.Count == 0) return float.NaN;
            var sorted = new List<float>(values);
            sorted.Sort();
            int m = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[m] : 0.5f * (sorted[m - 1] + sorted[m]);
        }
    }
}
