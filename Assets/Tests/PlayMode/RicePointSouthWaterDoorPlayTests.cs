#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// Out by the channel mouth to the South Water and home again, rowed as a player rows it, through
    /// the game's own passages, loaders and travel coordinator. Rice Point is played direct. Each leg
    /// reads the key its passage published the moment before the coordinator takes it, and asserts
    /// where that way in laid the boat.
    ///
    /// <para>Playing direct, Rice Point's scene laid the boat, so her berth there answers her way home
    /// (<c>RegionTravelCoordinator.ApplyArrival</c>); a game that boots elsewhere has no berth here and
    /// lays her at the door's own way in. Both are checked: the leg home against whichever applies,
    /// and the way in's point against the plan.</para>
    /// </summary>
    public sealed class RicePointSouthWaterDoorPlayTests : RicePointSceneFixture
    {
        const float LandedWithinMetres = 1f;
        /// <summary>Past a passage's re-entry guard, which it stamps again when its scene's roots come back on.</summary>
        const float ReArmSeconds = 2f;
        const int SettleFrames = 30, AfloatSteps = 10;
        const float CrossingLimitSeconds = 150f;
        const float AimPastSeamMetres = 30f;
        /// <summary>Rigidbody2D rotations, counter-clockwise from north.</summary>
        const float HeadSouth = 180f, HeadNorth = 0f;
        const int PlateWidth = 1920, PlateHeight = 1080;
        const float MouthViewHalfHeight = 30f, ArrivalViewHalfHeight = 30f;
        /// <summary>The pictures are taken at noon; the row itself keeps the fixture's hour and tide.</summary>
        const double PlateHour = 12;
        /// <summary>The day's light follows the clock on a throttled tick, so a picture waits real time, not frames.</summary>
        const float PlateSettleSeconds = 1f;
        /// <summary>A noon picture is not a night one: the least mean brightness (0 to 255) a plate may have.</summary>
        const float PlateLeastMeanBrightness = 40f;

        RegionTravelCoordinator _coordinator;
        string _key;
        Vector3 _boatAt;

        [UnityTest, Timeout(600000)]
        public IEnumerator TheChannelMouth_RowsTheDoryToTheSouthWaterAndHome_ArrivingByKey()
        {
            yield return LoadDirect();
            WatchTheKey();
            LandDoor door = RicePointDoors.SouthWaterDoor(Plan);
            RegionAnchor home = RegionAnchor.ForScene(RegionScene);
            Assert.That(home, Is.Not.Null, "Rice Point's anchor");
            Assert.That((Vector2)home.ArrivalPointFor(RicePointDoors.FromSouthWaterArrivalKey).position, Is.EqualTo(door.Arrival),
                "a boat from the South Water is laid at the channel mouth's own way in");

            yield return BoardAtStart();
            LayAt(door.Arrival, HeadSouth);
            yield return Afloat("the dory at the channel mouth");
            yield return Plate("leaving-by-the-channel-mouth", new Vector2(door.Centre.x, (door.Arrival.y + door.Centre.y) * .5f), MouthViewHalfHeight);

            yield return Cross("Rice Point to the South Water, rowed south out of the channel mouth", RicePointDoors.ToSouthWaterArrivalKey,
                new Vector2(door.Centre.x, -Plan.WorldSize.y / 2f - AimPastSeamMetres), SouthWaterPlan.SceneName, door.Target.Id);
            Oars.enabled = false;   // the arrival hands the helm back to the hardware read; the strokes stay scripted
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.Aboard), "she arrives with her oarsman");
            Landed("the South Water's " + RicePointDoors.ToSouthWaterArrivalKey, _boatAt, SouthWaterPlan.FromRicePointArrivalPos);
            yield return Afloat("the dory at the South Water's way in");
            yield return Plate("arrived-in-the-south-water", SouthWaterPlan.FromRicePointArrivalPos, ArrivalViewHalfHeight);

            yield return new WaitForSecondsRealtime(ReArmSeconds);
            LayAt(SouthWaterPlan.FromRicePointArrivalPos, HeadNorth);
            Vector2 north = SouthWaterPlan.ToRicePointPassagePos;
            yield return Cross("the South Water to Rice Point, rowed north into the channel mouth", RicePointDoors.FromSouthWaterArrivalKey,
                new Vector2(north.x, north.y + AimPastSeamMetres), RegionScene.name, Plan.Region.Id);
            Oars.enabled = false;
            Assert.That(Switcher.Mode, Is.EqualTo(ControlMode.Aboard), "and comes home with him");
            if (home.HasAuthoredBoatBerth)
            {
                TestContext.WriteLine("Rice Point's scene laid her, so her berth answers her way home (played direct)");
                Landed("her berth at Rice Point", _boatAt, home.AuthoredBoatBerthPosition);
            }
            else Landed("Rice Point's " + RicePointDoors.FromSouthWaterArrivalKey, _boatAt, door.Arrival);
            yield return Afloat("the dory home at Rice Point");
            yield return Plate("home-at-rice-point", Boat.transform.position, ArrivalViewHalfHeight);
        }

        void WatchTheKey()
        {
            _coordinator = Object.FindAnyObjectByType<RegionTravelCoordinator>();
            Assert.That(_coordinator != null && _coordinator.isActiveAndEnabled, Is.True, "premise: the core's travel coordinator is live");
            _coordinator.enabled = false;
            SceneManager.activeSceneChanged += PeekKey;
            _coordinator.enabled = true;
        }

        void PeekKey(Scene previous, Scene next) => _key = GameServices.PendingArrivalKey;

        IEnumerator Cross(string leg, string key, Vector2 aim, string toScene, string toRegion)
        {
            _key = null;
            float start = Time.unscaledTime;
            bool there = false;
            for (int settled = 0; !there || settled < SettleFrames;)
            {
                Assert.That(Time.unscaledTime - start, Is.LessThan(CrossingLimitSeconds),
                    $"{leg}: not across inside {CrossingLimitSeconds} s; the boat is at {Boat.transform.position}");
                if (!there) StrokeToward(aim);
                yield return null;
                if (there) { settled++; continue; }
                there = SceneManager.GetActiveScene().name == toScene && GameServices.CurrentRegionId == toRegion;
                if (!there) continue;
                Oars.DriveOars(0, 0, true);
                _boatAt = Boat.transform.position;
            }
            Assert.That(_key, Is.EqualTo(key), $"{leg}: the passage asks for its way in by key");
            Assert.That(Object.FindObjectsByType<GameRoot>().Length, Is.EqualTo(1), $"{leg}: one persistent core");
            TestContext.WriteLine($"{leg}: across in {Time.unscaledTime - start:0.0} s, laid at {_boatAt}");
        }

        void LayAt(Vector2 at, float rotationDeg)
        {
            var body = Boat.GetComponent<Rigidbody2D>();
            Boat.Stop();
            body.position = at;
            body.rotation = rotationDeg;
            Boat.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, rotationDeg));
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        IEnumerator Afloat(string what)
        {
            for (int i = 0; i < AfloatSteps; i++) yield return new WaitForFixedUpdate();
            Assert.That(Boat.IsAground, Is.False, $"{what} floats");
        }

        static void Landed(string where, Vector3 at, Vector2 point)
        {
            float off = Vector2.Distance(at, point);
            TestContext.WriteLine($"{where}: {off:0.###} m from its point {point}");
            Assert.That(off, Is.LessThanOrEqualTo(LandedWithinMetres), $"{where}: landed at {at}, {off:0.###} m from {point}");
        }

        /// <summary>A picture of the place, from the game's own camera, put back as it was. Skipped without a GPU.</summary>
        IEnumerator Plate(string name, Vector2 centre, float halfHeight)
        {
            Camera camera = Camera.main;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || camera == null) yield break;
            Vector3 wasAt = camera.transform.position;
            float wasSize = camera.orthographicSize, wasAspect = camera.aspect;
            RenderTexture wasTarget = camera.targetTexture;
            var target = new RenderTexture(PlateWidth, PlateHeight, 24, RenderTextureFormat.ARGB32);
            target.Create();
            double wasSeconds = GameServices.Clock.TotalSeconds;
            bool wasPaused = GameServices.Clock.IsPaused;
            SetHour(PlateHour);
            for (float until = Time.realtimeSinceStartup + PlateSettleSeconds; Time.realtimeSinceStartup < until;)
            {
                camera.transform.position = new Vector3(centre.x, centre.y, wasAt.z);
                camera.orthographicSize = halfHeight;
                yield return null;
            }
            camera.transform.position = new Vector3(centre.x, centre.y, wasAt.z);
            camera.orthographicSize = halfHeight;
            camera.targetTexture = target;
            camera.aspect = (float)PlateWidth / PlateHeight;
            RenderTexture previous = RenderTexture.active;
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(PlateWidth, PlateHeight, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, PlateWidth, PlateHeight), 0, 0);
            pixels.Apply();
            double sum = 0;
            Color32[] read = pixels.GetPixels32();
            foreach (Color32 c in read) sum += (c.r + c.g + c.b) / 3.0;
            double brightness = sum / read.Length;
            string folder = Path.Combine(Evidence, "south-water-door");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = wasTarget;
            camera.aspect = wasAspect;
            camera.ResetAspect();
            camera.orthographicSize = wasSize;
            camera.transform.position = wasAt;
            GameServices.Clock.SeekTo(wasSeconds);
            GameServices.Clock.IsPaused = wasPaused;
            Object.DestroyImmediate(pixels);
            target.Release();
            Object.DestroyImmediate(target);
            TestContext.WriteLine($"plate {name}: mean brightness {brightness:0.0} at hour {PlateHour}");
            Assert.That(brightness, Is.GreaterThan(PlateLeastMeanBrightness), $"plate {name} is lit as at noon, not dark");
        }

        [UnityTearDown]
        public IEnumerator LeaveTheSouthWater()
        {
            SceneManager.activeSceneChanged -= PeekKey;
            Scene water = SceneManager.GetSceneByName(SouthWaterPlan.SceneName);
            if (!water.IsValid() || !water.isLoaded) yield break;
            if (_coordinator != null) _coordinator.enabled = false;   // the test is over: no arrival in the scene the unload leaves
            yield return SceneManager.UnloadSceneAsync(water);
        }
    }
}
#endif
