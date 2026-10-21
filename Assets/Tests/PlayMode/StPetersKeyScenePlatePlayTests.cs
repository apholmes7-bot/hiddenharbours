using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // Shares the existing GPU/title-only camera harness and its complete teardown.
    public partial class WharfBuildingPass2PlatePlayTests
    {
        readonly struct KeyFrame
        {
            public readonly string Id;
            public readonly Vector2 Centre;
            public readonly float Height;
            public KeyFrame(string id, float x, float y, float height)
            { Id = id; Centre = new Vector2(x, y); Height = height; }
        }

        [UnityTest]
        public IEnumerator StPeters_Wave1b_LandingCanneryBenchAndEastEnd_ThroughTheGameCamera()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            EnsureTheFollow();
            _look = "wave1b";
            _lookEvidence = "key scenes, ground XY centres from CD's scene frames; independent props retain their game XY (ADR 0042)";
            HoldTheMood();
            yield return PinTheHour(Noon, ClearRunSeconds);
            string captions = StartFile("captions.txt");
            string numbers = StartFile("numbers.txt");
            var frames = new[]
            {
                new KeyFrame("landing.of_road", 181.5f, -1.5f, 9f),
                new KeyFrame("landing.of_deck", 199f, -1.6f, 9f),
                new KeyFrame("landing.of_disembark", 211.5f, -1.9f, 9f),
                new KeyFrame("cannery.of_door", 176.3f, 8.2f, 9f),
                new KeyFrame("cannery.of_shingle", 174.5f, 30f, 9f),
                new KeyFrame("cannery.wide_landing", 181f, 8f, 14f),
                new KeyFrame("whelps_lookout.of_bench", 166.9f, -39.3f, 9f),
                new KeyFrame("east_end.loft_corner", 183.2f, 23.8f, 14f),
                new KeyFrame("east_end.look_pass", 178f, 14f, 33.75f),
            };
            var newPieces = Object.FindObjectsByType<SpriteRenderer>().Where(r =>
                r.transform.parent != null && r.transform.parent.name.StartsWith("keyscene.stp_")).ToList();
            Assert.Greater(newPieces.Count, 20, "The regenerated key-scene root is missing.");
            File.AppendAllText(numbers, Census("wave 1b placed sheets", newPieces), Utf8);
            foreach (var frame in frames)
            {
                int zoom = CameraZoomPolicy.StepForWorldHeight(frame.Height, CameraFollow.AssetsPPU, CameraFollow.DesignScreenHeightPx);
                float height = CameraZoomPolicy.WorldHeightForStep(zoom, CameraFollow.AssetsPPU, CameraFollow.DesignScreenHeightPx);
                float pixel = CameraZoomPolicy.WorldUnitsPerRenderedPixel(zoom, CameraFollow.AssetsPPU, CameraFollow.DesignScreenHeightPx);
                Vector2 centre = new Vector2(Mathf.Round(frame.Centre.x / pixel) * pixel, Mathf.Round(frame.Centre.y / pixel) * pixel);
                yield return FrameOn(centre, height);
                yield return RunTheWorld(ClearRunSeconds, centre);
                yield return ShadersReady();
                SavePlate(frame.Id + ".png", Capture());
                File.AppendAllText(captions, Caption(frame.Id, "wave 1b", centre, ""), Utf8);
                File.AppendAllText(numbers, $"{frame.Id}: CD requested height={frame.Height} game ladder height={height} centre={centre.x:R},{centre.y:R}\n", Utf8);
                using var draws = OfferedCounter("Draw Calls Count");
                using var batches = OfferedCounter("Batches Count");
                using var sets = OfferedCounter("SetPass Calls Count");
                for (int i = 0; i < 5; i++) yield return null;
                long calls = draws.Valid ? draws.LastValue : -1;
                long passes = sets.Valid ? sets.LastValue : -1;
                string counterSource = "ProfilerRecorder";
#if UNITY_EDITOR
                if (calls <= 0)
                {
                    calls = UnityEditor.UnityStats.drawCalls;
                    passes = UnityEditor.UnityStats.setPassCalls;
                    counterSource = "UnityStats (editor Game view)";
                }
#endif
                Assert.Greater(calls, 0, "No rendered draw calls; this is not GPU evidence.");
                File.AppendAllText(numbers, $"{frame.Id}: drawCalls={calls} batches={(batches.Valid ? batches.LastValue : -1)} setPass={passes} source={counterSource}\n", Utf8);
            }
            foreach (GullPerch perch in Object.FindObjectsByType<GullPerch>())
                File.AppendAllText(numbers, $"perch {perch.Id}: ground={perch.GroundPoint} screen={perch.ScreenPoint} sortY={perch.SortY}\n", Utf8);
            // Village_stPetersCannery.json gives this ridge pixel in every cell, measured from top-left.
            // Keep CD's gull coordinates: this records their relation to the baked ridge, not a relocation.
            SpriteRenderer cannery = GameObject.Find("StPetersCannery").GetComponent<SpriteRenderer>();
            Sprite roof = cannery.sprite;
            Assert.AreEqual(new Vector2(892, 681), roof.rect.size, "Re-read the ridge anchor when the sheet changes.");
            Vector2 ridgeLocal = (new Vector2(446f, roof.rect.height - 169.8f) - roof.pivot) / roof.pixelsPerUnit;
            Vector2 ridgeWorld = cannery.transform.TransformPoint(ridgeLocal);
            GullPerch gullA = Object.FindObjectsByType<GullPerch>().Single(p => p.Id == "prop.stp_cannery_gull_a");
            Vector2 ridgeDelta = gullA.ScreenPoint - ridgeWorld;
            File.AppendAllText(numbers, $"gull_a vs baked ridge: ridgeWorld={ridgeWorld:F4} perchWorld={gullA.ScreenPoint:F4} " +
                $"deltaWorld={ridgeDelta:F4} deltaSheetPixels={ridgeDelta * roof.pixelsPerUnit:F3}\n", Utf8);
            Debug.Log("[O3Wave1b] PLATES_COMPLETE " + PlatePath());
        }

        static ProfilerRecorder OfferedCounter(string name)
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.Name == name) return ProfilerRecorder.StartNew(description.Category, name);
            }
            return default;
        }
    }
}
