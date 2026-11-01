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

#if UNITY_EDITOR
        sealed class LandingBoardTide : IEnvironmentService
        {
            readonly IEnvironmentService _inner;
            readonly float _level;
            public LandingBoardTide(IEnvironmentService inner, float level) { _inner = inner; _level = level; }
            public int WorldSeed => _inner.WorldSeed;
            public TideProfile ActiveTideProfile { get => _inner.ActiveTideProfile; set => _inner.ActiveTideProfile = value; }
            public EnvironmentSample Sample()
            {
                var sample = _inner.Sample();
                return new EnvironmentSample(sample.WindVector, sample.CurrentVector, _level,
                    sample.SeaState, sample.Visibility, sample.SeaState01);
            }
            public float TideHeightAt(double seconds) => _level;
            public float WaterLevelAt(double seconds) => _level;
        }

        [UnityTest]
        public IEnumerator StPeters_LandingPassTwo_AtBothBoardHoursAndPinnedTides_OffBesideOn()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            EnsureTheFollow(); HoldTheMood();
            _look = "k0-landing";
            _lookEvidence = "pass-two pier; under-pier tide occlusion absent; gameplay floor, fittings and lamps unchanged";
            string captions = StartFile("captions.txt"), numbers = StartFile("numbers.txt");
            var wharf = GameObject.Find("StPetersWharf");
            Assert.IsNotNull(wharf);
            Transform deck = wharf.transform.Find("Deck");
            var migrated = deck.Find("WharfPass2Deck").GetComponent<SpriteRenderer>();
            Assert.IsNotNull(migrated.sprite);
            var legacy = new GameObject("LegacyDeckComparison");
            legacy.transform.SetParent(deck, false); _spawned.Add(legacy);
            var atlas = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Tilesets/Wharf/WharfAtlas.png")
                .OfType<Sprite>().ToDictionary(s => s.name);
            string[] variants = { "ctr", "edN", "edE", "edS", "edW", "coNE", "coSE", "coSW", "coNW" };
            foreach (Vector2Int cell in HiddenHarbours.App.Editor.StPetersWharf.DeckCellsBackToFront())
            {
                int index = 17 + System.Array.IndexOf(variants, HiddenHarbours.App.Editor.StPetersWharf.DeckVariantAt(cell.x, cell.y));
                var go = new GameObject("Deck_" + cell.x + "_" + cell.y); go.transform.SetParent(legacy.transform, false);
                go.transform.position = new Vector3(cell.x, cell.y + 1, 0);
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = atlas["WharfAtlas_" + index];
                renderer.sortingOrder = HiddenHarbours.App.Editor.StPetersWharf.SortingOrderFor(cell.y);
            }
            Assert.AreEqual(186, legacy.GetComponentsInChildren<SpriteRenderer>().Length);
            IEnvironmentService held = GameServices.Environment;
            bool followEnabled = _follow.enabled;
            var pixelCameras = _cam.GetComponents<Behaviour>().Where(b => b.GetType().Name == "PixelPerfectCamera" && b.enabled).ToArray();
            var boards = new[]
            {
                new { Id = "landing.wide_approach.1431", Day = 29, Minute = 871, Tide = 2.18f, X = 203f, Y = 2.649f },
                new { Id = "cannery.wide_landing.0600", Day = 39, Minute = 360, Tide = -0.85f, X = 181f, Y = 12.596f },
            };
            try
            {
                foreach (var board in boards)
                {
                    GameServices.Environment = new LandingBoardTide(held, board.Tide);
                    yield return PinTheHour(board.Minute / 60f, ClearRunSeconds);
                    double target = (board.Day + board.Minute / 1440.0) * GameServices.Config.SecondsPerDay;
                    GameServices.Clock.SeekTo(target); GameServices.Clock.TimeScale = 0;
                    _follow.enabled = followEnabled;
                    foreach (var pixelCamera in pixelCameras) pixelCamera.enabled = true;
                    yield return FrameOn(new Vector2(board.X, board.Y), 14);
                    yield return RunTheWorld(2f, new Vector2(board.X, board.Y));
                    float until = Time.realtimeSinceStartup + 20;
                    while (Time.realtimeSinceStartup < until)
                    {
                        Vector4 sea = Shader.GetGlobalVector("_HHSeaLevelWorld");
                        if (sea.w > 0.5f && Mathf.Abs(sea.x - board.Tide) < 0.01f) break;
                        yield return null;
                    }
                    Assert.That(Shader.GetGlobalVector("_HHSeaLevelWorld").x, Is.EqualTo(board.Tide).Within(0.01f));
                    Assert.That(GameServices.Clock.DayIndex, Is.EqualTo(board.Day));
                    Assert.That(GameServices.Clock.HourOfDay, Is.EqualTo(board.Minute / 60f).Within(0.001f));
                    yield return ShadersReady();
                    _follow.enabled = false;
                    foreach (var pixelCamera in pixelCameras) pixelCamera.enabled = false;
                    Time.timeScale = 0;
                    _cam.targetTexture = null;
                    if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
                    _w = 796; _h = 448;
                    _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf); _rt.Create();
                    _cam.targetTexture = _rt;
                    _cam.transform.position = new Vector3(board.X, board.Y, _cam.transform.position.z);
                    _cam.orthographicSize = 7; _cam.aspect = 24.89f / 14;
                    foreach (bool on in new[] { false, true })
                    {
                        legacy.SetActive(!on); migrated.enabled = on;
                        yield return null;
                        _cam.transform.position = new Vector3(board.X, board.Y, _cam.transform.position.z);
                        _cam.orthographicSize = 7;
                        string mode = on ? "on" : "off";
                        SavePlate(board.Id + "-" + mode + ".png", Capture());
                        File.AppendAllText(captions, $"{board.Id} {mode}: day {board.Day}, minute {board.Minute}, tide {board.Tide:R}; " +
                            $"centre {board.X:R},{board.Y:R}, height 14, width 24.89, 796x448; " +
                            "under-pier tide occlusion absent; floor, Def placements and lamp owners unchanged\n", Utf8);
                        using var draws = OfferedCounter("Draw Calls Count");
                        for (int i = 0; i < 5; i++) yield return null;
                        long calls = draws.Valid ? draws.LastValue : UnityEditor.UnityStats.drawCalls;
                        if (calls <= 0) calls = UnityEditor.UnityStats.drawCalls;
                        File.AppendAllText(numbers, $"{board.Id} {mode}: deckRenderers={(on ? 1 : 186)} drawCalls={calls} " +
                            $"deckTextureBytes={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(migrated.sprite.texture)}\n", Utf8);
                    }
                }
            }
            finally
            {
                GameServices.Environment = held; migrated.enabled = true;
                legacy.SetActive(false);
                _follow.enabled = followEnabled;
                foreach (var pixelCamera in pixelCameras) pixelCamera.enabled = true;
                Time.timeScale = 1;
                Object.DestroyImmediate(legacy);
            }
            Debug.Log("[StPetersLandingDeck] PLATES_COMPLETE " + PlatePath());
        }
#endif
    }
}
