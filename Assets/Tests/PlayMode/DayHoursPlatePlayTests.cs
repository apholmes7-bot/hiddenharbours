using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class DayHoursPlatePlayTests
    {
        WharfNightStage _stage;
        ISaveService _previousSave;
        bool _capturing;
        CameraFollow _pinnedFollow;
        Behaviour _pinnedLens;
        int _previousRefX, _previousRefY;
        object _previousCrop;
        bool _previousLensEnabled;

        sealed class PlateSave : ISaveService
        {
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => key == ArrivalOpening.ArrivedFlagKey;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheSameCameraReadsTheWholeDayInBothRegions()
        {
            WharfNightStage.RequireAGraphicsDevice();
            Assert.AreNotEqual(SaveStore.DefaultPath, SaveStore.ActivePath);
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-dayHoursPlates");
            if (at < 0 || at + 1 >= args.Length)
                Assert.Ignore("Day contact sheets are an explicitly requested GPU evidence run.");
            string output = args[at + 1];
            Directory.CreateDirectory(output);
            _capturing = true;
            _previousSave = GameServices.Save;
            GameServices.Save = new PlateSave();
            foreach (string region in new[] { "StPeters", "NineMileCreek" })
            {
                var previousClock = GameServices.Clock;
                var previousConfig = GameServices.Config;
                var previousEnvironment = GameServices.Environment;
                var previousWallet = GameServices.Wallet;
                _stage = new WharfNightStage(region, "DayHours");
                yield return _stage.Load();
                if (region == "NineMileCreek")
                {
                    GameServices.Clock = previousClock;
                    GameServices.Config = previousConfig;
                    GameServices.Environment = previousEnvironment;
                    GameServices.Wallet = previousWallet;
                }
                ShellFlow.ContinueGame();
                foreach (var opening in Object.FindObjectsByType<ArrivalOpening>(FindObjectsSortMode.None))
                    opening.enabled = false;
                var clock = GameServices.Clock as HiddenHarbours.Environment.GameClock;
                if (clock == null) clock = Object.FindFirstObjectByType<HiddenHarbours.Environment.GameClock>();
                Assert.IsNotNull(clock);
                GameServices.Clock = clock;
                GameServices.Config = clock.Config;
                clock.IsPaused = true;
                clock.IsHeld = false;
                // Each scene supplies its own review subject; the same anchor and lens serve every hour.
                Vector2 subject = region == "StPeters" ? new Vector2(224f, 0f)
                    : HiddenHarbours.App.Editor.NineMileCreekWharf.DeckFootprint().center;
                Time.timeScale = 1f;
                yield return _stage.FrameOn(subject);
                _pinnedFollow = Object.FindAnyObjectByType<CameraFollow>();
                Assert.IsNotNull(_pinnedFollow);
                _pinnedFollow.enabled = false;
                // Match the recorded base lens once, through the render-time pixel-perfect path.
                _pinnedLens = _stage.Camera.GetComponent("PixelPerfectCamera") as Behaviour;
                Assert.IsNotNull(_pinnedLens);
                _previousRefX = ReadLens<int>("refResolutionX");
                _previousRefY = ReadLens<int>("refResolutionY");
                _previousCrop = ReadLens<object>("cropFrame");
                _previousLensEnabled = _pinnedLens.enabled;
                int referenceHeight = Mathf.RoundToInt(7.03125f * 2f * ReadLens<int>("assetsPPU"));
                WriteLens("refResolutionY", referenceHeight);
                WriteLens("refResolutionX", Mathf.RoundToInt(referenceHeight * _stage.Camera.aspect));
                WriteLens("cropFrame", System.Enum.Parse(_previousCrop.GetType(), "Letterbox"));
                _pinnedLens.enabled = true;
                yield return null;
                yield return null;
                Assert.AreEqual(7.03125f, _stage.Camera.orthographicSize, "The contact lens must match the recorded base.");
                Vector3 cameraPosition = _stage.Camera.transform.position;
                float cameraSize = _stage.Camera.orthographicSize;
                foreach (float hour in new[] { 5f, 5.5f, 7f, 7.5f, 8f, 12f, 14.5f, 20f, 21.5f, 22f })
                {
                    Time.timeScale = 1f;
                    clock.SeekTo(hour / 24d * clock.Config.SecondsPerDay);
                    yield return new WaitForSeconds(0.4f);
                    Time.timeScale = 0f;
                    foreach (var cycle in Object.FindObjectsByType<DayNightController>(FindObjectsSortMode.None))
                        typeof(DayNightController).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(cycle, null);
                    FoliageSnowBridge.PublishFromClock();
                    if (MoodGradeDirector.Instance != null) MoodGradeDirector.Instance.Tick();
                    for (int i = 0; i < 4; i++) yield return null;
                    Assert.AreEqual(cameraPosition, _stage.Camera.transform.position);
                    Assert.AreEqual(cameraSize, _stage.Camera.orthographicSize);
                    byte[] first = _stage.Capture();
                    byte[] repeat = _stage.Capture();
                    CollectionAssert.AreEqual(first, repeat, "A plate must repeat within this run.");
                    string name = region + "-" + Mathf.RoundToInt(hour * 60f).ToString("0000");
                    var texture = new Texture2D(_stage.Width, _stage.Height, TextureFormat.RGBA32, false);
                    texture.LoadRawTextureData(first);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
                    Object.DestroyImmediate(texture);
                    Color tint = Shader.GetGlobalColor("_DayNightTint");
                    var grade = MoodGradeDirector.Instance;
                    var sample = GameServices.Environment != null ? GameServices.Environment.Sample() : default;
                    File.WriteAllText(Path.Combine(output, name + ".txt"),
                        $"hour={clock.HourOfDay:R}\nseconds={clock.TotalSeconds:R}\nseason={clock.Season}\n" +
                        $"day={clock.DayOfSeason}\ntint={tint}\ncamera={cameraPosition}\nsize={cameraSize:R}\n" +
                        $"visibility={sample.Visibility:R}\nseaState={sample.SeaState01:R}\n" +
                        $"graded={grade != null && grade.LastTickGraded}\nweights={grade?.LastWeights}\n" +
                        $"snow={Shader.GetGlobalFloat(FoliageSnowBridge.CoverageProperty):R}\nrepeat=identical\n");
                }
                RestoreLens();
                _pinnedFollow.enabled = true;
                _pinnedFollow = null;
                yield return _stage.TearDown();
                _stage = null;
                yield return null;
            }
        }

        // The fixture reads this optional camera component without adding a module reference.
        T ReadLens<T>(string property) => (T)_pinnedLens.GetType().GetProperty(property).GetValue(_pinnedLens);
        void WriteLens(string property, object value) => _pinnedLens.GetType().GetProperty(property).SetValue(_pinnedLens, value);

        void RestoreLens()
        {
            if (_pinnedLens == null) return;
            WriteLens("refResolutionX", _previousRefX);
            WriteLens("refResolutionY", _previousRefY);
            WriteLens("cropFrame", _previousCrop);
            _pinnedLens.enabled = _previousLensEnabled;
            _pinnedLens = null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (!_capturing) yield break;
            RestoreLens();
            if (_pinnedFollow != null) _pinnedFollow.enabled = true;
            _pinnedFollow = null;
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = false;
            ShellRestart.DestroyPersistentCore();
            ShellFlow.Reset();
            GameServices.Save = _previousSave;
            _capturing = false;
            yield return null;
        }
    }
}
