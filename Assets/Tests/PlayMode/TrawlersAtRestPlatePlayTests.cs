using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The working trawlers, each lying at rest on St Peters' own sea at noon, shown through the scene's
    /// picker at two fixed headings. A plate, not a gate: the same test shoots a hull before and after its
    /// rebuild (ADR 0048 batch 3), into a folder named by <c>HH_TRAWL_PLATE_TAG</c>, so the two pictures
    /// share one pose, one light and one camera. PNGs go under the ignored <c>artifacts/</c> folder.
    /// </summary>
    public class TrawlersAtRestPlatePlayTests
    {
        const string TagVariable = "HH_TRAWL_PLATE_TAG";
        static readonly float[] Headings = { 35f, 215f };

        WharfNightStage _stage;
        readonly HashSet<GameObject> _before = new();
        bool _loaded, _intro;

        static string Plates => Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "artifacts/trawlers/plates",
            string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(TagVariable))
                ? "after" : System.Environment.GetEnvironmentVariable(TagVariable));

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
            var probe = new GameObject("TrawlerPlateRootProbe");
            Object.DontDestroyOnLoad(probe);
            var scene = probe.scene;
            Object.DestroyImmediate(probe);
            return scene.GetRootGameObjects();
        }

        [UnityTest] public IEnumerator SternTrawler_AtRest() => AtRest("SternTrawler");
        [UnityTest] public IEnumerator SternTrawlerMk2_AtRest() => AtRest("SternTrawlerMk2");
        [UnityTest] public IEnumerator SideDragger_AtRest() => AtRest("SideDragger");

        IEnumerator AtRest(string file)
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
            Assert.IsNotNull(picker);
            var boat = picker.GetComponent<BoatController>();
            Assert.IsNotNull(boat);
            bool found = false;
            for (int n = 0; n < picker.RosterCount; n++) { picker.Next(); if (picker.Current == hull) { found = true; break; } }
            Assert.IsTrue(found, file + " must be on the scene's F roster");
            yield return null;
            Assert.AreSame(hull, boat.Hull);
            var mooring = boat.GetComponent<BoatMooring>(); if (mooring != null) mooring.Stow();
            var anchor = boat.GetComponent<BoatAnchor>(); if (anchor != null) anchor.Weigh();
            var input = boat.GetComponent<DevBoatInput>(); if (input != null) input.enabled = false;

            Vector2 sea = FindSea(boat.transform.position, hull.DraughtMeters + .5f, out float depth);
            Directory.CreateDirectory(Plates);
            var caption = new System.Text.StringBuilder();
            foreach (float heading in Headings)
            {
                Time.timeScale = 1f;   // the stage's framing stops the clock; the hull settles on fixed steps
                boat.transform.SetPositionAndRotation(new Vector3(sea.x, sea.y, 0f), Quaternion.Euler(0f, 0f, heading));
                boat.Stop();
                for (int i = 0; i < 30; i++) { boat.SetControl(0f, 0f); yield return new WaitForFixedUpdate(); }
                var reviewCamera = Camera.main;
                if (reviewCamera != null) reviewCamera.orthographicSize = hull.LengthMeters * .8f;
                yield return _stage.FrameOn(boat.transform.position);
                Vector3 inFrame = _stage.Camera.WorldToViewportPoint(boat.transform.position);
                Assert.That(inFrame.x, Is.InRange(.05f, .95f), "the hull is inside the review camera horizontally");
                Assert.That(inFrame.y, Is.InRange(.05f, .95f), "the hull is inside the review camera vertically");
                string name = file + "-rest-" + heading.ToString("000");
                _stage.SavePlate(name + ".png", _stage.Capture());
                caption.AppendLine($"{name}: {hull.Id}; St Peters sea; noon; at rest; z rotation {boat.transform.eulerAngles.z:0.0}; " +
                                   $"water {depth:0.0} m under a {hull.DraughtMeters} m draught; length {hull.LengthMeters}; mass {hull.MassKg}; power {hull.EnginePower}; " +
                                   $"camera {_stage.Camera.transform.position}, ortho {_stage.Camera.orthographicSize:0.0}");
            }
            File.WriteAllText(Path.Combine(Plates, file + "-rest.txt"), caption.ToString());
#else
            Assert.Ignore("Editor-authored plate");
            yield break;
#endif
        }

        /// <summary>The stern trawler as a wreck: her wrecked paint scheme, heeled sixty degrees on drying
        /// ground, at the same two headings. A hull on her own (no boat controller), the way a scene would
        /// place a wreck from the hull mesh Def and a paint scheme Def.</summary>
        [UnityTest]
        public IEnumerator SternTrawler_Wrecked_Heeled()
        {
            WharfNightStage.RequireAGraphicsDevice();
#if UNITY_EDITOR
            const float HeelDegrees = 60f;
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>("Assets/_Project/Data/Boats/HullMeshes/SternTrawlerIsoHullMesh.asset");
            var scheme = AssetDatabase.LoadAssetAtPath<HullPaintSchemeDef>("Assets/_Project/Data/Boats/PaintSchemes/SternTrawlerIso_Wrecked.asset");
            Assert.IsNotNull(def, "the stern trawler's hull mesh Def"); Assert.IsNotNull(scheme, "her wrecked paint scheme");
            Assert.AreEqual("paint.stern_trawler_wrecked", scheme.Id);
            _intro = GameServices.OpeningCinematicRunning; _loaded = true;
            _stage = new WharfNightStage("StPeters", Plates);
            yield return _stage.Load();
            ShellFlow.Reset(); InteractionGate.Reset(); GameServices.OpeningCinematicRunning = false;
            yield return _stage.SetNight(12f);
            var picker = Object.FindFirstObjectByType<DevBoatPicker>();
            Assert.IsNotNull(picker);
            Vector2 ground = FindDryingGround(picker.transform.position, out float depth);
            var root = new GameObject("WreckedSternTrawlerPlate");
            var host = new GameObject("FacetMesh"); host.transform.SetParent(root.transform, false);
            root.transform.position = new Vector3(ground.x, ground.y, 0f);
            IHullMeshRenderer wreck = new HiddenHarbours.Art.IsoFacetHullPresentationService().Install(host, def, scheme);
            Assert.IsNotNull(wreck, "the install path took the Def and the scheme");
            Directory.CreateDirectory(Plates);
            var caption = new System.Text.StringBuilder();
            foreach (float heading in Headings)
            {
                wreck.HeadingDirUnits = HullMeshMath.HeadingToDirUnits(heading, 0f, def.AzimuthCounterClockwise);
                wreck.RollDegrees = HeelDegrees;
                var reviewCamera = Camera.main;
                if (reviewCamera != null) reviewCamera.orthographicSize = WreckFrameMeters;
                yield return null;
                yield return _stage.FrameOn(ground);
                string name = "SternTrawler-wrecked-heeled-" + heading.ToString("000");
                _stage.SavePlate(name + ".png", _stage.Capture());
                caption.AppendLine($"{name}: {scheme.Id}; St Peters drying ground ({depth:0.0} m of water); noon; roll {wreck.RollDegrees:0}; " +
                                   $"camera {_stage.Camera.transform.position}, ortho {_stage.Camera.orthographicSize:0.0}");
            }
            File.WriteAllText(Path.Combine(Plates, "SternTrawler-wrecked-heeled.txt"), caption.ToString());
            Object.DestroyImmediate(root);
#else
            Assert.Ignore("Editor-authored plate");
            yield break;
#endif
        }

        const float WreckFrameMeters = 30f;   // half-height of the wreck plate: a 38 m hull with room round her

        // The nearest ground to the berth that the tide leaves bare or nearly so: where a wreck lies.
        static Vector2 FindDryingGround(Vector2 origin, out float depth)
        {
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0;
            for (int radius = 40; radius <= 1600; radius += 20)
                for (int step = 0; step < 16; step++)
                {
                    float angle = step * Mathf.PI / 8f;
                    Vector2 at = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    depth = BoatCrossing.DepthAt(GameServices.TidalTerrain, GameServices.Environment, now, at);
                    if (depth > -.6f && depth < .3f) return at;
                }
            depth = float.NaN;
            Assert.Fail("no drying ground found near the St Peters berth");
            return origin;
        }

        // The deepest still water near the berth, found the same way every run: same hull, same spot. St Peters
        // has no ground as deep as the big trawlers draw, so the plate takes the deepest patch and says how deep.
        static Vector2 FindSea(Vector2 origin, float requiredDepth, out float depth)
        {
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0;
            Vector2 best = origin; depth = float.NegativeInfinity;
            for (int radius = 80; radius <= 1600; radius += 40)
                for (int step = 0; step < 16; step++)
                {
                    float angle = step * Mathf.PI / 8f;
                    Vector2 at = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    float shallowest = float.PositiveInfinity;
                    for (int dx = -40; dx <= 40; dx += 10)
                        for (int dy = -40; dy <= 40; dy += 10)
                            shallowest = Mathf.Min(shallowest, BoatCrossing.DepthAt(GameServices.TidalTerrain,
                                GameServices.Environment, now, at + new Vector2(dx, dy)));
                    if (shallowest > depth) { depth = shallowest; best = at; }
                    if (depth > requiredDepth) return best;
                }
            Assert.Greater(depth, 0f, "no St Peters water found for this hull");
            return best;
        }
    }
}
