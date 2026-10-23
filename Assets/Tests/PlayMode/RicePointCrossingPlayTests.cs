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
    /// ⭐ <b>TO NINE MILE CREEK AND BACK: WALKED OVER THE STRAND, ROWED AND SAILED THROUGH THE SEA DOOR</b> (NR1a).
    /// Rice Point played direct, then each crossing driven as a player drives it, both ways, through the game's own
    /// passages, the creek's loader and the travel coordinator. Each leg reads the key its passage published, the
    /// moment before the coordinator takes it, and asserts where that way in put the walker or laid the boat; each
    /// prints its frame times from the band to <see cref="SettleFrames"/> frames after the arrival.
    ///
    /// <para>One rule of playing direct answers the boat's way back: Rice Point's scene laid her, so its anchor
    /// remembered her berth when it first opened, and her berth beats any way in (<c>RegionTravelCoordinator.
    /// ApplyArrival</c>, ruled 2026-09-08). A game that boots at St Peters records no berth here, and lays
    /// her at Rice Point's own sea way in.</para>
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

        [UnityTest, Timeout(300000)]
        public IEnumerator TheStrand_WalksToTheCreekAndBack_LandingByKey()
        {
            yield return LoadDirect();
            WatchTheKey();
            LandDoor strand = Plan.Doors.Single(d => d.OwnArrival == Doors.StrandDoorArrivalKey);
            StageStrand();
            yield return Cross("Rice Point to the creek, walked east over the strand", Doors.StrandArrivalKey,
                () => WalkInput.Walk(Vector2.right), () => WalkInput.Release(), CreekScene, strand.Target.Id);
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.OnFoot), "the walker arrives on foot");
            Landed("the creek's " + Doors.StrandArrivalKey, _walkerAt, NineMileCreekMainland.RicePointStrandLandingPos);

            yield return new WaitForSecondsRealtime(ReArmSeconds);
            yield return Cross("the creek to Rice Point, walked west over the strand", Doors.StrandDoorArrivalKey,
                () => WalkInput.Walk(Vector2.left), () => WalkInput.Release(), RegionScene.name, Plan.Region.Id);
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.OnFoot), "and comes home on foot");
            Landed("Rice Point's " + Doors.StrandDoorArrivalKey, _walkerAt, strand.Disembark);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheSeaDoor_RowsTheDoryToTheCreekAndBack_ArrivingByKey()
        {
            yield return LoadDirect();
            WatchTheKey();
            LandDoor sea = Plan.Doors.Single(d => d.OwnArrival == Doors.SeaDoorArrivalKey);
            yield return BoardAtStart();
            LayAt(sea.Arrival, HeadEast, 0f);
            yield return Cross("Rice Point to the creek, rowed east through the sea door", Doors.SeaArrivalKey,
                () => StrokeToward(PastTheSeam(sea, true)), () => Oars.DriveOars(0, 0, true), CreekScene, sea.Target.Id);
            Oars.enabled = false;   // the arrival hands the helm back to the hardware read; the strokes stay scripted
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.Aboard), "she arrives with her oarsman");
            Landed("the creek's " + Doors.SeaArrivalKey, _boatAt, NineMileCreekMainland.RicePointSeaArrivalPos);
            yield return Afloat("the dory at the creek's way in");

            yield return new WaitForSecondsRealtime(ReArmSeconds);
            yield return Cross("the creek to Rice Point, rowed west through the sea door", Doors.SeaDoorArrivalKey,
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
            yield return Cross("Rice Point to the creek, sailed east through the sea door", Doors.SeaArrivalKey,
                () => Helm(CourseEast), () => Oars.DriveSailHelm(0f), CreekScene, sea.Target.Id);
            Oars.enabled = false;
            Landed("the creek's " + Doors.SeaArrivalKey, _boatAt, NineMileCreekMainland.RicePointSeaArrivalPos);
            yield return Afloat("the sloop at the creek's way in");

            yield return new WaitForSecondsRealtime(ReArmSeconds);
            LayAt(NineMileCreekMainland.RicePointSeaArrivalPos, HeadWest, reachKn);
            yield return Cross("the creek to Rice Point, sailed west through the sea door", Doors.SeaDoorArrivalKey,
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

        void PeekKey(Scene previous, Scene next) => _key = GameServices.PendingArrivalKey;

        /// <summary>Drive until the crossing lands (the active scene and the current region are the far side's),
        /// then stop and keep the frames to <see cref="SettleFrames"/> after.</summary>
        IEnumerator Cross(string leg, string key, Action drive, Action stop, string toScene, string toRegion)
        {
            _key = null;
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
                fired |= !string.IsNullOrEmpty(GameServices.PendingArrivalKey) || _key != null;
                if (fired) frames.Add(Time.unscaledDeltaTime * 1000f);
                if (there) { settled++; continue; }
                there = SceneManager.GetActiveScene().name == toScene && GameServices.CurrentRegionId == toRegion;
                if (!there) continue;
                stop();
                _walkerAt = Walk.transform.position;
                _boatAt = Boat.transform.position;
            }
            Assert.That(_key, Is.EqualTo(key), $"{leg}: the passage asks for its way in by key");
            Assert.That(Object.FindObjectsByType<GameRoot>().Length, Is.EqualTo(1), $"{leg}: one persistent core");
            Assert.That(frames, Is.Not.Empty, $"{leg}: frames were kept from the band on");
            frames.Sort();
            float p95 = frames[Mathf.Max(0, Mathf.CeilToInt(frames.Count * .95f) - 1)];
            string line = $"{leg}: {frames.Count} frames from the band to {SettleFrames} after the arrival, " +
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

        /// <summary>Home again: her berth, where Rice Point's scene laid her (playing direct), else its sea way in.</summary>
        void LaidBack(LandDoor sea)
        {
            RegionAnchor anchor = RegionAnchor.ForScene(RegionScene);
            Assert.That(anchor, Is.Not.Null, "Rice Point's anchor");
            if (anchor.HasAuthoredBoatBerth)
            {
                TestContext.WriteLine("Rice Point's scene laid her, so her berth answers her way home (played direct)");
                Landed("her berth at Rice Point", _boatAt, anchor.AuthoredBoatBerthPosition);
            }
            else Landed("Rice Point's " + Doors.SeaDoorArrivalKey, _boatAt, sea.Arrival);
        }

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
            yield return SceneManager.UnloadSceneAsync(creek);
        }
    }
}
#endif
