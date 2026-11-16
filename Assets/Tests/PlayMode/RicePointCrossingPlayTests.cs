#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Doors = HiddenHarbours.App.Editor.NineMileCreekRicePointDoors;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>TO NINE MILE CREEK AND BACK: WALKED OVER THE STRAND, ROWED AND SAILED ACROSS THE SEAM</b> (NR1a;
    /// REWRITTEN DELIBERATELY for ADR 0057, owner ruling 2026-11-15: "the whole wall is a door", "doors do not
    /// teleport"). Each crossing is now the region's EDGE itself, not a keyed band: no key is published, and the
    /// walker or boat lands at the SAME WORLD POINT on the other side, i.e. her position the moment the scene
    /// changed plus <c>from.WorldOrigin - to.WorldOrigin</c>. A walk over the strand leaves the boat exactly where
    /// she was (and hidden, because she is in another region); a sail carries her across and brings her back.
    /// Rice Point played direct, then each crossing driven as a player drives it, both ways, through the travel
    /// coordinator's edge crossing and the creek's loader. Each leg prints its frame times from the crossing to
    /// <see cref="SettleFrames"/> frames after the arrival.
    ///
    /// <para>The sloop sails on a glass sea, 12 kn of wind from the north, so both legs are beam reaches. She is
    /// laid at each way in with the polar's way on, because a sloop at rest has no steerage to turn by.</para>
    /// </summary>
    public sealed class RicePointCrossingPlayTests : RicePointSceneFixture
    {
        const string CreekScene = "NineMileCreek";
        const string Sloop30 = "Assets/_Project/Data/Boats/Sloop30.asset";

        /// <summary>How near its way in's point a walker lands or a boat lies, after the frame or two of way she
        /// carries before the test sees the arrival.</summary>
        const float LandedWithinMetres = 1f;
        const string NoKey = null;
        /// <summary>Past a passage's 1.5 s re-entry guard, which it stamps again when its scene's roots come back on.</summary>
        const float ReArmSeconds = 2f;
        const int SettleFrames = 30;
        const int AfloatSteps = 10;
        const float CrossingLimitSeconds = 120f;
        /// <summary>How far past the seam the helmsman aims, so the aim never turns her before the band.</summary>
        const float AimPastSeamMetres = 30f;
        /// <summary>Rigidbody2D rotations, counter-clockwise from north.</summary>
        const float HeadEast = -90f, HeadWest = 90f;
        const float CourseEast = 90f, CourseWest = 270f;
        const float TwsKn = 12f, FullHelmAtErrorDeg = 6f, BeamReachDeg = 90f;

        RegionTravelCoordinator _coordinator;
        string _key;
        Vector3 _walkerAt, _boatAt;
        /// <summary>Where the walker and the boat stood the moment the scene changed, before the shift.</summary>
        Vector3 _walkerBefore, _boatBefore;
        bool _crossed;

        [UnityTest, Timeout(300000)]
        public IEnumerator TheStrand_WalksToTheCreekAndBack_LandingByKey()
        {
            yield return LoadDirect();
            WatchTheKey();
            LandDoor strand = Plan.Doors.Single(d => d.OwnArrival == Doors.StrandDoorArrivalKey);
            StageStrand();
            Vector3 boatLeft = Boat.transform.position;
            yield return Cross("Rice Point to the creek, walked east over the strand", NoKey,
                () => WalkInput.Walk(Vector2.right), () => WalkInput.Release(), CreekScene, strand.Target.Id);
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.OnFoot), "the walker arrives on foot");
            Vector2 toCreek = Plan.Region.WorldOrigin - strand.Target.WorldOrigin;
            Landed("the creek, same world point", _walkerAt, (Vector2)_walkerBefore + toCreek);
            Assert.That(Vector2.Distance(Boat.transform.position, boatLeft), Is.LessThan(0.01f),
                "a walk never moves the boat: she stays where she was left at Rice Point");
            Assert.That(Boat.gameObject.activeSelf, Is.False, "and she is not shown in the creek, she is at Rice Point");

            yield return new WaitForSecondsRealtime(ReArmSeconds);
            yield return Cross("the creek to Rice Point, walked west over the strand", NoKey,
                () => WalkInput.Walk(Vector2.left), () => WalkInput.Release(), RegionScene.name, Plan.Region.Id);
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.OnFoot), "and comes home on foot");
            Landed("Rice Point, same world point", _walkerAt, (Vector2)_walkerBefore - toCreek);
            Assert.That(Boat.gameObject.activeSelf, Is.True, "home again, the boat is shown where she was left");
            Assert.That(Vector2.Distance(Boat.transform.position, boatLeft), Is.LessThan(0.01f), "untouched");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheSeaDoor_RowsTheDoryToTheCreekAndBack_ArrivingByKey()
        {
            yield return LoadDirect();
            WatchTheKey();
            LandDoor sea = Plan.Doors.Single(d => d.OwnArrival == Doors.SeaDoorArrivalKey);
            yield return BoardAtStart();
            LayAt(sea.Arrival, HeadEast, 0f);
            yield return Cross("Rice Point to the creek, rowed east across the seam", NoKey,
                () => StrokeToward(PastTheSeam(sea, true)), () => Oars.DriveOars(0, 0, true), CreekScene, sea.Target.Id);
            Oars.enabled = false;   // the arrival hands the helm back to the hardware read; the strokes stay scripted
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.Aboard), "she arrives with her oarsman");
            Landed("the creek, same world point", _boatAt, (Vector2)_boatBefore + ToCreek(sea));
            yield return Afloat("the dory where she crossed");

            yield return new WaitForSecondsRealtime(ReArmSeconds);
            yield return Cross("the creek to Rice Point, rowed west across the seam", NoKey,
                () => StrokeToward(PastTheSeam(sea, false)), () => Oars.DriveOars(0, 0, true), RegionScene.name, Plan.Region.Id);
            Oars.enabled = false;
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.Aboard), "and comes home with him");
            LaidBack(sea);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheSeaDoor_SailsTheSloopToTheCreekAndBack_ArrivingAfloatByKey()
        {
            yield return LoadDirect();
            WatchTheKey();
            LandDoor sea = Plan.Doors.Single(d => d.OwnArrival == Doors.SeaDoorArrivalKey);
            yield return BoardAtStart();
            var sloop = AssetDatabase.LoadAssetAtPath<BoatHullDef>(Sloop30);
            Assert.That(sloop != null && sloop.HasSailPlan, Is.True, "premise: the shipped sloop sails");
            IEnvironmentService shipped = GameServices.Environment;
            GameServices.Environment = new GlassSea(Vector2.down * SailDrive.ToMetresPerSecond(TwsKn),
                                                    shipped.WaterLevelAt(GameServices.Clock.TotalSeconds));
            Boat.SetHull(sloop);
            float reachKn = SailDrive.TargetSpeedKn(sloop.SailPolar, BeamReachDeg, TwsKn, sloop.NoGoTrueWindDeg);
            Assert.That(reachKn, Is.GreaterThan(0f), "premise: the polar drives her on a beam reach");

            LayAt(sea.Arrival, HeadEast, reachKn);
            yield return Cross("Rice Point to the creek, sailed east across the seam", NoKey,
                () => Helm(CourseEast), () => Oars.DriveSailHelm(0f), CreekScene, sea.Target.Id);
            Oars.enabled = false;
            Landed("the creek, same world point", _boatAt, (Vector2)_boatBefore + ToCreek(sea));
            yield return Afloat("the sloop where she crossed");

            yield return new WaitForSecondsRealtime(ReArmSeconds);
            LayAt(NineMileCreekMainland.RicePointSeaArrivalPos, HeadWest, reachKn);
            yield return Cross("the creek to Rice Point, sailed west across the seam", NoKey,
                () => Helm(CourseWest), () => Oars.DriveSailHelm(0f), RegionScene.name, Plan.Region.Id);
            Oars.enabled = false;
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.Aboard), "she comes home with her helmsman");
            LaidBack(sea);
            GameServices.Environment = shipped;
        }

        // ---- the crossing ----------------------------------------------------------------------------------------

        /// <summary>Subscribe ahead of the coordinator, so each crossing's key is read the moment before it is taken.</summary>
        void WatchTheKey()
        {
            _coordinator = Object.FindAnyObjectByType<RegionTravelCoordinator>();
            Assert.That(_coordinator != null && _coordinator.isActiveAndEnabled, Is.True, "premise: the core's travel coordinator is live");
            _coordinator.enabled = false;
            SceneManager.activeSceneChanged += PeekKey;
            _coordinator.enabled = true;
        }

        void PeekKey(Scene previous, Scene next)
        {
            _key = GameServices.PendingArrivalKey;
            _walkerBefore = Walk.transform.position;
            _boatBefore = Boat.transform.position;
            _crossed = true;
        }

        Vector2 ToCreek(LandDoor sea) => Plan.Region.WorldOrigin - sea.Target.WorldOrigin;

        /// <summary>Drive until the crossing lands (the active scene and the current region are the far side's),
        /// then stop and keep the frames to <see cref="SettleFrames"/> after.</summary>
        IEnumerator Cross(string leg, string key, Action drive, Action stop, string toScene, string toRegion)
        {
            _key = null;
            _crossed = false;
            var frames = new List<float>();
            float start = Time.unscaledTime;
            bool fired = false, there = false;
            for (int settled = 0; !there || settled < SettleFrames;)
            {
                Assert.That(Time.unscaledTime - start, Is.LessThan(CrossingLimitSeconds),
                    $"{leg}: not across inside {CrossingLimitSeconds} s; the walker is at {Walk.transform.position}, " +
                    $"the boat at {Boat.transform.position}");
                if (!there) drive();
                yield return null;
                fired |= _crossed;
                if (fired) frames.Add(Time.unscaledDeltaTime * 1000f);
                if (there) { settled++; continue; }
                there = SceneManager.GetActiveScene().name == toScene && GameServices.CurrentRegionId == toRegion;
                if (!there) continue;
                stop();
                _walkerAt = Walk.transform.position;
                _boatAt = Boat.transform.position;
            }
            Assert.That(_key, Is.EqualTo(key), $"{leg}: an edge crossing publishes no way-in key");
            Assert.That(Object.FindObjectsByType<GameRoot>().Length, Is.EqualTo(1), $"{leg}: one persistent core");
            Assert.That(frames, Is.Not.Empty, $"{leg}: frames were kept from the crossing on");
            frames.Sort();
            float p95 = frames[Mathf.Max(0, Mathf.CeilToInt(frames.Count * .95f) - 1)];
            string line = $"{leg}: {frames.Count} frames from the crossing to {SettleFrames} after the arrival, " +
                          $"p95 {p95:0.0} ms, worst {frames[frames.Count - 1]:0.0} ms";
            TestContext.WriteLine(line);
            Debug.Log("[RicePointCrossing] " + line);
        }

        /// <summary>A point on the far side of the seam, level with her way in, for the helmsman to aim at.</summary>
        Vector2 PastTheSeam(LandDoor sea, bool toTheCreek) => toTheCreek
            ? new Vector2(Plan.WorldSize.x / 2f + AimPastSeamMetres, sea.Arrival.y)
            : new Vector2(-sea.Target.WorldSizeMeters.x / 2f - AimPastSeamMetres, NineMileCreekMainland.RicePointSeaArrivalPos.y);

        /// <summary>Lay her at a way in, her head to the seam, with the way she is given.</summary>
        void LayAt(Vector2 at, float rotationDeg, float wayKn)
        {
            var body = Boat.GetComponent<Rigidbody2D>();
            Boat.Stop();
            body.position = at;
            body.rotation = rotationDeg;
            Boat.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, rotationDeg));
            body.linearVelocity = (Vector2)Boat.transform.up * SailDrive.ToMetresPerSecond(wayKn);
            Physics2D.SyncTransforms();
        }

        void Helm(float courseDeg)
        {
            float error = Mathf.DeltaAngle(BoatKinematics.BearingDegrees(Boat.transform.up), courseDeg);
            Oars.DriveSailHelm(Mathf.Clamp(error / FullHelmAtErrorDeg, -1f, 1f));
        }

        IEnumerator Afloat(string what)
        {
            for (int i = 0; i < AfloatSteps; i++) yield return new WaitForFixedUpdate();
            Assert.That(Boat.IsAground, Is.False, $"{what} floats");
        }

        /// <summary>Home again at the same world point she left the creek from: no berth snap, no way-in point
        /// (the authored berth answered the old keyed door, D8; an edge crossing never consults it).</summary>
        void LaidBack(LandDoor sea) =>
            Landed("Rice Point, same world point", _boatAt, (Vector2)_boatBefore - ToCreek(sea));

        static void Landed(string where, Vector3 at, Vector2 point)
        {
            float off = Vector2.Distance(at, point);
            TestContext.WriteLine($"{where}: {off:0.###} m from its point {point}");
            Assert.That(off, Is.LessThanOrEqualTo(LandedWithinMetres), $"{where}: landed at {at}, {off:0.###} m from {point}");
        }

        sealed class GlassSea : IEnvironmentService
        {
            readonly Vector2 _wind;
            readonly float _level;
            public GlassSea(Vector2 wind, float level) { _wind = wind; _level = level; }
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(_wind, Vector2.zero, tideHeight: _level,
                HiddenHarbours.Core.SeaState.Calm, visibility: 1f, seaState01: 0f);
            public float TideHeightAt(double totalSeconds) => _level;
            public float WaterLevelAt(double totalSeconds) => _level;
        }

        [UnityTearDown]
        public IEnumerator LeaveTheCreek()
        {
            SceneManager.activeSceneChanged -= PeekKey;
            Scene creek = SceneManager.GetSceneByName(CreekScene);
            if (!creek.IsValid() || !creek.isLoaded) yield break;
            if (_coordinator != null) _coordinator.enabled = false;   // the test is over: no arrival in the scene the unload leaves
            yield return HiddenHarbours.Tests.Support.SceneSettle.Unload(creek);
        }
    }
}
#endif
