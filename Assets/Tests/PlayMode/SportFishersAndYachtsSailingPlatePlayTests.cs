using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Profiling;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>The four intake hulls, boarded and steered on St Peters' own sea through the scene's picker.</summary>
    public class SportFishersAndYachtsSailingPlatePlayTests
    {
        WharfNightStage _stage;
        readonly HashSet<GameObject> _before = new();
        bool _loaded, _intro;
        static string Plates => Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "artifacts/sf-120-intake/b3/plates");

        [UnitySetUp]
        public IEnumerator Snapshot()
        {
            foreach (var root in PersistentRoots()) _before.Add(root);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            if (_stage != null) yield return _stage.TearDown();
            if (!_loaded) yield break;
            foreach (var root in PersistentRoots()) if (root != null && !_before.Contains(root)) Object.DestroyImmediate(root);
            GameServices.Reset();
            GameServices.OpeningCinematicRunning = _intro;
            ShellFlow.Reset(); InteractionGate.Reset();
            _before.Clear(); _stage = null; _loaded = false;
            yield return null;
        }

        static GameObject[] PersistentRoots()
        {
            var probe = new GameObject("SailingPlateRootProbe");
            Object.DontDestroyOnLoad(probe);
            var scene = probe.scene;
            Object.DestroyImmediate(probe);
            return scene.GetRootGameObjects();
        }

        [UnityTest] public IEnumerator The53_BoardsTakesTheHelmAndSails() => Sail("SportFisherConvertible");
        [UnityTest] public IEnumerator The90_BoardsTakesTheHelmAndSails() => Sail("SportFisherSkybridge");
        [UnityTest] public IEnumerator The120_BoardsTakesTheHelmAndSails() => Sail("ExpeditionYacht120");
        [UnityTest] public IEnumerator The84_BoardsTakesTheHelmAndSails() => Sail("MotorYacht84");

        IEnumerator Sail(string file)
        {
            WharfNightStage.RequireAGraphicsDevice();
#if UNITY_EDITOR
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/" + file + ".asset");
            Assert.IsNotNull(hull, file);
            _intro = GameServices.OpeningCinematicRunning; _loaded = true;
            _stage = new WharfNightStage("StPeters", Plates);
            yield return _stage.Load();
            ShellFlow.Reset(); InteractionGate.Reset(); GameServices.OpeningCinematicRunning = false;
            yield return _stage.SetNight(12f);
            var picker = Object.FindFirstObjectByType<DevBoatPicker>();
            var sw = Object.FindFirstObjectByType<ControlSwitcher>();
            var player = Object.FindFirstObjectByType<PlayerWalkController>();
            Assert.IsNotNull(picker); Assert.IsNotNull(sw); Assert.IsNotNull(player);
            var boat = picker.GetComponent<BoatController>();
            Assert.IsNotNull(boat);
            // Next is the F seam. Verify this asset is actually one of the scene's rungs.
            bool found = false;
            for (int n = 0; n < picker.RosterCount; n++) { picker.Next(); if (picker.Current == hull) { found = true; break; } }
            Assert.IsTrue(found, file + " must be on the scene's F roster");
            yield return null;
            // Start beside her at the scene's dock; only setup places the on-foot skipper.
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
            player.transform.position = boat.transform.position + boat.transform.right;
            bool viaDory = false;
            bool inReach = sw.WithinBoardReach(), boardable = sw.BoardableNow();
            bool pressed = sw.BeginInteract();
            if (file == "SportFisherSkybridge")
            {
                LogBoardRead(file, "origin-plus-right", sw, player, inReach, boardable, pressed);
                if (!pressed)
                {
                    StandAtRail(sw, player, boat);
                    inReach = sw.WithinBoardReach(); boardable = sw.BoardableNow();
                    pressed = sw.BeginInteract();
                    LogBoardRead(file, "rail-abeam", sw, player, inReach, boardable, pressed);
                    if (!pressed)
                    {
                        viaDory = true;
                        for (int n = 0; n < picker.RosterCount; n++)
                        {
                            picker.Next();
                            if (picker.Current != null && picker.Current.Id == "boat.dory") break;
                        }
                        yield return null;
                        StandAtRail(sw, player, boat);
                        pressed = sw.BeginInteract();
                        Debug.Log(file + ": direct rail boarding refused; boarding the dory before the F swap");
                    }
                }
            }
            Assert.IsTrue(pressed, "E boards this hull");
            float until = Time.realtimeSinceStartup + 12f;
            while (sw.Mode != ControlMode.OnDeck && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "boarding must finish on her delivered deck");
            player.transform.position = sw.HelmWorldPosition;
            Assert.IsTrue(sw.TakesTheHelmOnThisPress());
            Assert.IsTrue(sw.BeginInteract());
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            if (viaDory)
            {
                for (int n = 0; n < picker.RosterCount; n++) { picker.Next(); if (picker.Current == hull) break; }
                yield return null;
            }
            Assert.AreSame(hull, boat.Hull);
            Assert.IsTrue(picker.IsAtHelm);
            yield return null;
            var mooring = boat.GetComponent<BoatMooring>(); if (mooring != null) mooring.Stow();
            var anchor = boat.GetComponent<BoatAnchor>(); if (anchor != null) anchor.Weigh();
            var input = boat.GetComponent<DevBoatInput>(); if (input != null) input.enabled = false;
            Vector2 berthStart = boat.transform.position;
            float began = Time.time;
            while (Time.time - began < 3f) { boat.SetControl(1f, 0f); yield return new WaitForFixedUpdate(); }
            float berthDepth = BoatCrossing.DepthAt(GameServices.TidalTerrain, GameServices.Environment,
                GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0, boat.transform.position);
            float berthMoved = Vector2.Distance(berthStart, boat.transform.position);
            string berthRead = $"{hull.Id}; depth {berthDepth:0.000} m; IsAground {boat.IsAground}; moved {berthMoved:0.000} m in 3 seconds at throttle 1";
            Debug.Log("[berth-departure] " + berthRead);
            Directory.CreateDirectory(Plates);
            File.WriteAllText(Path.Combine(Plates, file + "-berth.txt"), berthRead);
            boat.Stop();
            // Choose actual deep sea in this region, then move the boarded boat there for the review run.
            Vector2 sea = FindSea(boat.transform.position, hull.DraughtMeters + .5f, out Vector2 course);
            boat.transform.SetPositionAndRotation(new Vector3(sea.x, sea.y, 0f),
                Quaternion.Euler(0f, 0f, Mathf.Atan2(course.y, course.x) * Mathf.Rad2Deg - 90f));
            boat.Stop();
            began = Time.time;
            Time.timeScale = 4f;
            while (Time.time - began < 10f) { boat.SetControl(1f, 0f); yield return new WaitForFixedUpdate(); }
            float heading = boat.transform.eulerAngles.z;
            began = Time.time;
            while (Time.time - began < 4f) { boat.SetControl(1f, .6f); yield return new WaitForFixedUpdate(); }
            var rb = boat.GetComponent<Rigidbody2D>();
            float speed = rb.linearVelocity.magnitude;
            Assert.Greater(speed, .5f, "throttle must move her through St Peters' water");
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(heading, boat.transform.eulerAngles.z)), .5f, "her rudder must turn her");
            Assert.IsFalse(boat.IsAground);
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            Time.timeScale = 1f;
            yield return null;
            yield return null;
            Assert.Less(Vector2.Distance(player.transform.position, sw.HelmWorldPosition), 1f, "the skipper stays at her helm");
            Assert.IsNotNull(boat.transform.Find(BoatHullSkinner.VisualChildName));
            Time.timeScale = 1f;
            _stage.Camera.orthographicSize = Mathf.Max(_stage.Camera.orthographicSize, hull.LengthMeters * .8f);
            yield return _stage.FrameOn(boat.transform.position);
            speed = rb.linearVelocity.magnitude;
            Assert.Greater(speed, .5f, "the captured frame is still under way");
            Vector3 inFrame = _stage.Camera.WorldToViewportPoint(boat.transform.position);
            Assert.That(inFrame.x, Is.InRange(.05f, .95f), "the hull is inside the review camera horizontally");
            Assert.That(inFrame.y, Is.InRange(.05f, .95f), "the hull is inside the review camera vertically");
            var planes = GeometryUtility.CalculateFrustumPlanes(_stage.Camera);
            Assert.IsTrue(boat.GetComponentsInChildren<MeshRenderer>().Any(r => r.enabled &&
                GeometryUtility.TestPlanesAABB(planes, r.bounds)), "the review camera sees her mesh");
            _stage.SavePlate(file + ".png", _stage.Capture());
            string caption = $"{hull.Id}; St Peters sea; noon; skipper aboard at helm; heading {Mathf.Repeat(-boat.transform.eulerAngles.z, 360f):0.0} degrees; speed {speed:0.00} m/s; throttle 1; rudder 0.6; mass {hull.MassKg}; power {hull.EnginePower}; drag {hull.ForwardDrag}; camera {_stage.Camera.transform.position}, ortho {_stage.Camera.orthographicSize:0.0}";
            File.WriteAllText(Path.Combine(Plates, file + ".txt"), caption);
            Debug.Log(caption);
            if (file.StartsWith("SportFisher")) yield return FrameCost(file, boat);
#else
            Assert.Ignore("Editor-authored sailing plate");
            yield break;
#endif
        }

        static void StandAtRail(ControlSwitcher sw, PlayerWalkController player, BoatController boat)
        {
            float beam = boat.Hull.LengthMeters;
            player.transform.position = boat.transform.position + boat.transform.right * (beam + sw.BoardReachMetres);
            var nearest = typeof(ControlSwitcher).GetMethod("NearestPointOnHull",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Vector3 rail = (Vector3)nearest.Invoke(sw, null);
            player.transform.position = rail + boat.transform.right * .05f;
        }

        static void LogBoardRead(string file, string setup, ControlSwitcher sw, PlayerWalkController player,
            bool inReach, bool boardable, bool pressed)
        {
            var nearest = typeof(ControlSwitcher).GetMethod("NearestPointOnHull",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            float distance = Vector3.Distance(player.transform.position, (Vector3)nearest.Invoke(sw, null));
            string gate = !inReach ? "WithinBoardReach" : !boardable ? "BoardableNow"
                : !pressed ? "BeginBoardingMove and the instant fall-through" : "none";
            string read = $"{file}; {setup}; WithinBoardReach {inReach}; BoardableNow {boardable}; E {pressed}; move {sw.IsBoardingMove}; rail distance {distance:0.000}; reach {sw.BoardReachMetres:0.000}; refused gate {gate}";
            Debug.Log("[boarding-read] " + read);
            Directory.CreateDirectory(Plates);
            File.AppendAllText(Path.Combine(Plates, file + "-boarding.txt"), read + "\n");
        }

        static IEnumerator FrameCost(string file, BoatController boat)
        {
            var renderers = boat.GetComponentsInChildren<Renderer>(true);
            bool[] forcedOff = renderers.Select(r => r.forceRenderingOff).ToArray();
            var csv = new StringBuilder("drawn,frame,editor_main_thread_ms,drawn_renderers\n");
            try
            {
                foreach (bool drawn in new[] { true, false })
                {
                    for (int i = 0; i < renderers.Length; i++) renderers[i].forceRenderingOff = !drawn || forcedOff[i];
                    using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 90);
                    for (int i = 0; i < 10; i++) yield return null;
                    for (int i = 0; i < 90; i++)
                    {
                        yield return null;
                        double ms = recorder.Valid ? recorder.LastValue / 1e6 : -1;
                        csv.Append(drawn ? "1," : "0,").Append(i).Append(',')
                            .Append(ms.ToString("0.000000", CultureInfo.InvariantCulture)).Append(',')
                            .Append(renderers.Count(r => r.enabled && !r.forceRenderingOff)).Append('\n');
                    }
                }
                File.WriteAllText(Path.Combine(Plates, file + "-frame-cost.csv"), csv.ToString());
                Debug.Log(file + ": editor main-thread samples, boat renderers drawn and forced off; no GPU-time claim.");
            }
            finally
            {
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].forceRenderingOff = forcedOff[i];
            }
        }

        static Vector2 FindSea(Vector2 origin, float requiredDepth, out Vector2 course)
        {
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0;
            course = Vector2.up;
            for (int radius = 80; radius <= 800; radius += 40)
                for (int step = 0; step < 16; step++)
                {
                    float angle = step * Mathf.PI / 8f;
                    Vector2 ahead = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 side = new Vector2(-ahead.y, ahead.x);
                    Vector2 at = origin + ahead * radius;
                    bool clear = true;
                    // The longest hull's stern and the complete throttle/rudder run need water too.
                    for (int along = -20; along <= 100 && clear; along += 10)
                        foreach (float across in new[] { -12f, 0f, 12f })
                            if (BoatCrossing.DepthAt(GameServices.TidalTerrain, GameServices.Environment,
                                now, at + ahead * along + side * across) <= requiredDepth) { clear = false; break; }
                    if (clear) { course = ahead; return at; }
                }
            LogDeepestCorridor(origin, requiredDepth, now);
            Assert.Fail("No sufficiently deep St Peters sea found for this hull");
            return origin;
        }

        static void LogDeepestCorridor(Vector2 origin, float requiredDepth, double now)
        {
            float deepest = float.NegativeInfinity;
            Vector2 best = origin;
            for (int radius = 80; radius <= 800; radius += 40)
                for (int step = 0; step < 16; step++)
                {
                    float angle = step * Mathf.PI / 8f;
                    Vector2 ahead = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 side = new Vector2(-ahead.y, ahead.x);
                    Vector2 at = origin + ahead * radius;
                    float minimum = float.PositiveInfinity;
                    for (int along = -20; along <= 100; along += 10)
                        foreach (float across in new[] { -12f, 0f, 12f })
                            minimum = Mathf.Min(minimum, BoatCrossing.DepthAt(GameServices.TidalTerrain,
                                GameServices.Environment, now, at + ahead * along + side * across));
                    if (minimum > deepest) { deepest = minimum; best = at; }
                }
            Debug.Log($"[deepest-corridor] minimum depth {deepest:0.000} m at {best}; required {requiredDepth:0.000} m; origin {origin}");
        }
    }
}
