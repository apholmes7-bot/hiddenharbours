#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class CameraStopTableTests
    {
        [Test]
        public void EveryCurrentHull_ExportsItsRuledStop_WithoutAHandMaintainedRoster()
        {
            var hulls = AssetDatabase.FindAssets("t:BoatHullDef")
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BoatHullDef>)
                .Where(h => h != null).OrderBy(h => h.Id).ToArray();
            Assert.IsNotEmpty(hulls);
            string evidence = System.Environment.GetEnvironmentVariable("CAMERA_EVIDENCE");
            using TextWriter rows = string.IsNullOrEmpty(evidence) ? new StringWriter()
                : new StreamWriter(Path.Combine(evidence, "ruled-hull-stops.csv"));
            rows.WriteLine("id,name,length,has_helm,authored_height,ruled_height,width,height,base_stop_at_rest,base_pullback_top_calm,base_pullback_top_full_sea,new_stop,one_notch_wider,pixel_perfect");
            var pixelType = Type.GetType("UnityEngine.Rendering.Universal.PixelPerfectCamera, Unity.RenderPipelines.Universal.2D.Runtime", true);
            try
            {
                foreach (var hull in hulls)
                    foreach (int height in new[] { 1080, 1440, 2160 })
                    {
                        var go = new GameObject("Hull stop table");
                        try
                        {
                            var camera = go.AddComponent<Camera>();
                            camera.orthographic = true;
                            camera.aspect = 16f / 9f;
                            var pixel = (Behaviour)go.AddComponent(pixelType);
                            pixelType.GetProperty("assetsPPU").SetValue(pixel, 32);
                            var follow = go.AddComponent<CameraFollow>();
                            follow.ScreenPixelsOverride = new Vector2Int(height * 16 / 9, height);
                            follow.OnActiveBoatChanged(new ActiveBoatChanged(hull.Id, hull.CameraWorldHeightMeters, hull.LengthMeters));
                            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
                            follow.TickZoom(100d);
                            float actual = 2f * camera.orthographicSize;
                            float ratio = height / (actual * 32f);
                            float integerRatio = ratio >= 1f ? ratio : 1f / ratio;
                            Assert.AreEqual(Mathf.Round(integerRatio), integerRatio, 0.0001f, hull.Id + " must rest on an integer pixel ratio");
                            float footprint = hull.LengthMeters * Mathf.Max(Mathf.Sin(40f * Mathf.Deg2Rad), 9f / 16f);
                            Assert.GreaterOrEqual(actual + 0.0001f, footprint, hull.Id + " must show the hull at its worst heading");
                            float ruled = follow.WorldHeightFor(CameraFraming.Boat);
                            float baseRest = BaseRestingHeight(ruled, height);
                            float baseCalmTop = ruled * 1.15f;
                            float baseFullSeaTop = ruled * 1.2f;
                            int newStep = CameraZoomPolicy.StepForWorldHeight(actual, 32, height);
                            float wider = CameraZoomPolicy.WorldHeightForStep(CameraZoomPolicy.StepClosestBy(newStep, -1), 32, height);
                            if (hull.Id == "boat.cape_islander" && height == 1080)
                            {
                                Assert.AreEqual(24f, ruled, 0.0001f);
                                Assert.AreEqual(16.875f, baseRest, 0.0001f);
                                Assert.AreEqual(27.6f, baseCalmTop, 0.0001f);
                                Assert.AreEqual(16.875f, actual, 0.0001f);
                                Assert.AreEqual(33.75f, wider, 0.0001f);
                            }
                            rows.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:R},{3},{4:R},{5:R},{6},{7},{8:R},{9:R},{10:R},{11:R},{12:R},{13}",
                                hull.Id, hull.DisplayName.Replace(',', ' '), hull.LengthMeters, hull.Helm != null, hull.CameraWorldHeightMeters,
                                ruled, height * 16 / 9, height, baseRest, baseCalmTop, baseFullSeaTop, actual, wider, pixel.enabled));
                        }
                        finally { Object.DestroyImmediate(go); }
                    }
            }
            finally { GameServices.Reset(); }
        }

        // The base used the 1080p ladder and reference at every viewport. Downscales drove
        // the design-height ortho directly; upscales were reimposed by the reference's integer zoom.
        static float BaseRestingHeight(float ruled, int screenHeight)
        {
            float Height(int step) => step > 0 ? 1080f / (32f * step) : 1080f * -step / 32f;
            int step = Enumerable.Range(-8, 7).Concat(Enumerable.Range(1, 8))
                .OrderBy(candidate => Mathf.Abs(Height(candidate) - ruled)).First();
            if (step < 0) return Height(step);
            int refHeight = 1080 / step;
            int refWidth = Mathf.RoundToInt(refHeight * 16f / 9f);
            int zoom = Mathf.Max(1, Mathf.Min((screenHeight * 16 / 9) / refWidth, screenHeight / refHeight));
            return screenHeight / (32f * zoom);
        }
    }
}
#endif
