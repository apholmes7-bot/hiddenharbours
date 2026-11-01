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
            Assert.AreSame(hull, boat.Hull);
            // Start beside her at the scene's dock; only setup places the on-foot skipper.
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
            player.transform.position = boat.transform.position + boat.transform.right;
            Assert.IsTrue(sw.BeginInteract(), "E boards this hull");
            float until = Time.realtimeSinceStartup + 12f;
            while (sw.Mode != ControlMode.OnDeck && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "boarding must finish on her delivered deck");
            player.transform.position = sw.HelmWorldPosition;
            Assert.IsTrue(sw.TakesTheHelmOnThisPress());
            Assert.IsTrue(sw.BeginInteract());
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            Assert.IsTrue(picker.IsAtHelm);
            yield return null;
            var mooring = boat.GetComponent<BoatMooring>(); if (mooring != null) mooring.Stow();
            var anchor = boat.GetComponent<BoatAnchor>(); if (anchor != null) anchor.Weigh();
            // Choose actual deep sea in this region, then move the boarded boat there for the review run.
            Vector2 sea = FindSea(boat.transform.position, hull.DraughtMeters + .5f, out Vector2 course);
            boat.transform.SetPositionAndRotation(new Vector3(sea.x, sea.y, 0f),
                Quaternion.Euler(0f, 0f, Mathf.Atan2(course.y, course.x) * Mathf.Rad2Deg - 90f));
            boat.Stop();
            var input = boat.GetComponent<DevBoatInput>(); if (input != null) input.enabled = false;
            float began = Time.time;
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
            Assert.Fail("No sufficiently deep St Peters sea found for this hull");
            return origin;
        }
    }
}
