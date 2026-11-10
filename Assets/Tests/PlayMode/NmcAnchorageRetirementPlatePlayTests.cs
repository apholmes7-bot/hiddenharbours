#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class NmcAnchorageRetirementPlatePlayTests
    {
        [Serializable] public class HullIds
        {
            public string name;
            public int hull, fore, top;
        }
        [Serializable] public class Census
        {
            public string version;
            public float hour;
            public int reviewHulls, workingHulls, facetWarnings, reflectionWarnings;
            public int registryHulls, figures, topHullId, topForeId, topFigureId, topIssuedId;
            public int reflectors, reflectionCap;
            public HullIds[] hullIds;
        }

        WharfNightStage _stage;
        int _facetWarnings, _reflectionWarnings;
        void CountLog(string text, string trace, LogType type)
        {
            if (text.Contains("Facet hull ids exhausted")) _facetWarnings++;
            if (text.Contains("[ReflectionRegistry]") && text.Contains("over the")) _reflectionWarnings++;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheOpenWaterAndTheWorkingWall_HaveTheirMatchedPlates()
        {
            WharfNightStage.RequireAGraphicsDevice();
            bool baseline = System.Environment.GetEnvironmentVariable("HH_ANCHORAGE_BASELINE") == "1";
            string label = baseline ? "main" : "branch";
            Application.logMessageReceived += CountLog;
            _stage = new WharfNightStage("NineMileCreek", "nmc-anchorage-off");
            yield return _stage.Load();
            yield return _stage.SetNight(14f);
            var boats = Object.FindObjectsByType<MooredBoat>(FindObjectsSortMode.None);
            int review = boats.Count(b => b.name.StartsWith("Review_", StringComparison.Ordinal));
            // The wharf's water side (NMC H5) names the float residents by their registered rows (boat.nmc_*) and
            // the register has three of them where the old float had two; the five wall boats keep their names.
            var working = boats.Where(b => b.name.StartsWith("Moored_owner.", StringComparison.Ordinal)
                                           || b.name.StartsWith("boat.nmc_", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(baseline ? 23 : 0, review);
            Assert.AreEqual(baseline ? 7 : 8, working.Length);
            foreach (var boat in working)
            {
                Assert.IsTrue(boat.IsPresented, boat.name);
                Assert.IsNotNull(boat.GetComponentInChildren<IsoFacetHullRenderer>(), boat.name);
            }

            yield return _stage.FrameOn(new Vector2(240f, 50f));
            var follow = Object.FindFirstObjectByType<CameraFollow>();
            follow.enabled = false;
            var cam = _stage.Camera;
            var pixelCamera = cam.GetComponents<Behaviour>()
                .FirstOrDefault(b => b.GetType().Name == "PixelPerfectCamera");
            if (pixelCamera != null) pixelCamera.enabled = false;
            string dir = "artifacts/nmc-anchorage-off";
            Directory.CreateDirectory(dir);
            foreach (var view in new[]
            {
                (name: "open-water", x: 240f, y: 50f, size: 160f),
                (name: "working-wall", x: 126f, y: 85f, size: 55f),
            })
            {
                cam.transform.position = new Vector3(view.x, view.y, cam.transform.position.z);
                cam.orthographicSize = view.size;
                yield return null;
                yield return null;
                Assert.AreEqual(view.size, cam.orthographicSize, "the plate must keep its declared framing");
                byte[] rgba = _stage.Capture();
                var png = new Texture2D(_stage.Width, _stage.Height, TextureFormat.RGBA32, false);
                png.LoadRawTextureData(rgba); png.Apply();
                byte[] bytes = png.EncodeToPNG();
                Object.DestroyImmediate(png);
                string file = label + "-" + view.name + ".png";
                File.WriteAllBytes(Path.Combine(dir, file), bytes);
                _stage.SavePlate(file, rgba);
                Debug.Log($"[AnchorageOff] {label} {view.name}: centre {view.x}, {view.y}; size {view.size}; hour 14");
            }

            var ids = Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsSortMode.None)
                .Select(h => new HullIds { name = h.name, hull = h.HullId, fore = h.ForeHullId, top = h.ForeHullIdTop }).ToArray();
            var figureIds = (HashSet<int>)typeof(IsoFacetHullRegistry).GetField("s_FigureIds", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            object pool = typeof(IsoFacetHullRegistry).GetField("s_Pool", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            int next = (int)pool.GetType().GetField("_nextId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pool);
            var census = new Census
            {
                version = label, hour = (float)GameServices.Clock.HourOfDay,
                reviewHulls = review, workingHulls = working.Length,
                facetWarnings = _facetWarnings, reflectionWarnings = _reflectionWarnings,
                registryHulls = IsoFacetHullRegistry.Count, figures = IsoFacetHullRegistry.FigureCount,
                topHullId = ids.Select(h => h.hull).DefaultIfEmpty().Max(),
                topForeId = ids.Select(h => h.top).DefaultIfEmpty().Max(),
                topFigureId = figureIds.DefaultIfEmpty().Max(), topIssuedId = next - 1,
                reflectors = ReflectionRegistry.Count, reflectionCap = ReflectionRegistry.MaxReflectors,
                hullIds = ids,
            };
            string json = JsonUtility.ToJson(census, true);
            Directory.CreateDirectory("Evidence~/nmc-h6/anchorage-off");
            File.WriteAllText("Evidence~/nmc-h6/anchorage-off/" + label + "-census.json", json);
            Debug.Log("[AnchorageOff] census " + JsonUtility.ToJson(census));
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            Application.logMessageReceived -= CountLog;
            if (_stage != null) yield return _stage.TearDown();
        }
    }
}
#endif
