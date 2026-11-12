using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    // The water she pushes up at her leading end. The cape is driven north at the fixture's speed, the clock
    // is stopped, and the SAME frozen frame is photographed three times through the game's own camera with
    // only one material value changed between them: the mound off (as shipped), at a suggested height, and
    // at a tall one that makes its shape plain. The wake sprites are hushed so the water itself is what shows.
    //
    // Asserted: she published her length, and the dial reaches the renderer that drew the plate (the "on"
    // plate differs from the "off" plate by more than the off plate differs from its own repeat). Whether
    // the mound looks right is for the plates.
    public partial class WakeCentrePhotographPlayTests
    {
        const string BowMoundProp = "_WakeBowLiftMetres";
        const float BowMoundSuggestedMetres = 0.15f;
        const float BowMoundTallMetres = 0.5f;

        [UnityTest, Timeout(600000)]
        public IEnumerator TheCape_MakingWayNorth_PhotographsTheMoundAtHerBow()
        {
            RequireAGraphicsDevice();   // FIRST, before any yield
            BoatHullDef hull = LoadAsset<BoatHullDef>(CapeHullPath);
            BoatVisualDef visual = LoadAsset<BoatVisualDef>(CapeVisualPath);
            if (hull == null || visual == null) yield break;

            yield return LoadRegion();
            yield return SetHour(ShotHour);

            const string subject = "bow-mound";
            var leg = new Leg("north-000", 0f, 0f);
            if (!TryLegAnchors(1, out List<Vector2> anchors, out float depth))
            {
                Assert.Ignore("SKIPPED, NOT VERIFIED: no open water inside " + SceneName +
                              "'s camera bounds deep enough to drive her.");
                yield break;
            }

            Spawned she = Spawn(subject, hull, visual, false, leg, anchors[0]);
            Assert.IsNotNull(she.Injector, "the cape was fitted with no foam injector, so she publishes no lift");
            DisplacedWaterSurface displaced = FindDisplacedSurface();
            Assert.IsNotNull(displaced, "no displaced sea in the region, and only the displaced pass draws a lift");
            Assert.IsTrue(displaced.Displaced, "the displaced sea is off, so nothing could draw the mound");

            Bounds run = PredictRun(leg, anchors[0], 3f * she.BandHalfWidth + she.SternOffset + 2f);
            yield return FrameOn(she.Go.transform, run);

            var track = new List<Vector2>(256);
            var injRoot = new List<Vector2>(256);
            yield return Drive(she.Go, she.Rb, leg, track, injRoot, she.SternOffset, she.ElevationDeg, she.Injector);
            Assert.Greater(track.Count, 8, "she never got under way");

            Time.timeScale = 0f;
            for (int i = 0; i < 2; i++) yield return null;
            HushTheWakeSprites(subject, FindWakeRoot(she.Go.name));
            IdentifyTheDrawingWater(subject, "under way");

            Vector2 expectRoot = WakeRootMath.SternWorld((Vector2)she.Go.transform.position,
                                                        (Vector2)she.Go.transform.up,
                                                        she.SternOffset, she.ElevationDeg);
            int slot = IdentifyTheLiftSlot(subject, "under way", expectRoot, out Vector4 root, out Vector4 shape);
            Assert.Greater(shape.x, 0f, "her wake gate is shut, so there is no mound to draw");
            Assert.AreEqual(2f * she.SternOffset, shape.w, 0.01f,
                            "she did not publish her length, so the shader cannot find her leading end");

            var was = new List<float>();
            foreach (Material m in _liftMats) was.Add(m != null ? m.GetFloat(BowMoundProp) : 0f);
            try
            {
                byte[] off = ShootBowMound(0f, out double offMs);
                byte[] off2 = ShootBowMound(0f, out _);
                byte[] on = ShootBowMound(BowMoundSuggestedMetres, out double onMs);
                byte[] tall = ShootBowMound(BowMoundTallMetres, out _);
                SavePlate(subject + "-off.png", off);
                SavePlate(subject + "-on-suggested.png", on);
                SavePlate(subject + "-on-tall.png", tall);

                float repeat = WorstChannelDelta(off, off2, out int repeatPx);
                float moved = WorstChannelDelta(off, on, out int movedPx);
                float movedTall = WorstChannelDelta(off, tall, out int tallPx);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[{0}] {1}: slot {2}, gate {3:0.000}, half-beam {4:0.00} m, length {5:0.00} m, {6:0.0} m under her; " +
                    "off against its repeat {7:0.00000} over {8} px; off against {9:0.00} m {10:0.00000} over {11} px; " +
                    "off against {12:0.00} m {13:0.00000} over {14} px; render {15:0.00} ms off, {16:0.00} ms on " +
                    "(mean of {17}); {18}x{19} px.",
                    PlateDir, subject, slot, shape.x, shape.z, shape.w, depth, repeat, repeatPx,
                    BowMoundSuggestedMetres, moved, movedPx, BowMoundTallMetres, movedTall, tallPx,
                    offMs, onMs, RenderTimingFrames, _w, _h));
                Assert.Greater(movedTall, Mathf.Max(4f * repeat, 2f / 255f),
                    "the tall mound changed the plate no more than the plate changes by itself: the dial never " +
                    "reached the renderer that drew it");
                Assert.Greater(movedPx, repeatPx, "the suggested mound moved no more pixels than a repeat does");
            }
            finally
            {
                for (int i = 0; i < _liftMats.Count; i++)
                    if (_liftMats[i] != null) _liftMats[i].SetFloat(BowMoundProp, was[i]);
                Time.timeScale = 1f;
            }
        }

        byte[] ShootBowMound(float metres, out double renderMs)
        {
            foreach (Material m in _liftMats)
                if (m != null) m.SetFloat(BowMoundProp, metres);
            byte[] plate = Capture();
            renderMs = 0d;
            var one = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            RenderTexture before = RenderTexture.active;
            for (int i = -4; i < RenderTimingFrames; i++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                _cam.Render();
                RenderTexture.active = _rt;
                one.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                if (i >= 0) renderMs += watch.Elapsed.TotalMilliseconds;
            }
            RenderTexture.active = before;
            Object.Destroy(one);
            renderMs /= RenderTimingFrames;
            return plate;
        }
    }
}
