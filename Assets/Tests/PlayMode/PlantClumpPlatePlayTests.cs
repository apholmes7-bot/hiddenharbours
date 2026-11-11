#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The clump law's plates: St Peters at High Summer noon, one asset pixel to one plate pixel, the same
    /// camera and seed, BEFORE (the law at strength 0 = the scatter as it shipped) and AFTER (the asset as
    /// committed). Three stations of recipe scatter: a meadow, a path verge and a marsh-and-dune shore.
    /// The field's Definition and law are copied before a number is changed, so no asset is written.
    /// </summary>
    public partial class WharfBuildingPass2PlatePlayTests
    {
        sealed class ClumpWindow
        {
            public string Id; public float X0, X1, Top, Bottom, PixelsPerMetre;
            public ClumpWindow(string id, float x0, float x1, float top, float bottom, float pixelsPerMetre = 32)
            { Id = id; X0 = x0; X1 = x1; Top = top; Bottom = bottom; PixelsPerMetre = pixelsPerMetre; }
        }
        // 40 x 22.5 m = 1280 x 720 at 32 px/m.
        static readonly ClumpWindow[] ClumpWindows =
        {
            new ClumpWindow("meadow", 20f, 60f, -20f, -42.5f),   // open meadow under the south wood
            new ClumpWindow("path", 0f, 40f, 12.5f, -10f),       // the cross-island path and a cottage verge
            new ClumpWindow("shore", 10f, 50f, -47.5f, -70f),    // marsh bed, shore path, dune grass
        };
        const int ClumpMinute = 720;   // noon, on the middle day of High Summer by the game's own calendar

        [Serializable] sealed class ClumpNumbers
        {
            public string window, phase, file;
            public float strength, tintR, tintG, tintB;
            public int plants, authored, chunks, missing, dormant;
            public long draws, batches;
            public double rebuildMs, cpuP95Ms;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator StPeters_PlantClumpLaw_BeforeAndAfter_AtSummerNoon()
        {
            RequireAGraphicsDevice();
            string folder = Path.Combine(Application.temporaryCachePath, "plants-clump");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "_floor.txt"), DateTime.UtcNow.ToString("O"));
            TestContext.WriteLine("CLUMP_FOLDER: " + folder); Debug.Log("CLUMP_FOLDER: " + folder);
            yield return LoadRegion(StPeters); EnsureTheFollow(); HoldTheMood();
            var field = Object.FindFirstObjectByType<PlantField>(); Assert.IsNotNull(field, "St Peters has no plant field");
            PlantFieldDefinition committed = field.Definition; Assert.IsNotNull(committed);
            Assert.IsNotNull(committed.Clump, "the St Peters field binds no clump law");
            Assert.IsTrue(PlantClumpLaw.IsOn(committed.Clump), "the law ships ON");
            var definition = Object.Instantiate(committed); var law = Object.Instantiate(committed.Clump);
            definition.Clump = law; float shipped = law.Strength;
            // Optional: a folder of JSON overrides of the law, each drawn as one more AFTER (a tuning pass
            // inside one editor launch). Unset, the run is the committed asset and nothing else.
            var phases = new List<(string name, string json, float strength)> { ("before", null, 0f), ("after", null, shipped) };
            string variants = System.Environment.GetEnvironmentVariable("HH_CLUMP_VARIANTS");
            if (!string.IsNullOrEmpty(variants) && Directory.Exists(variants))
                foreach (string path in Directory.GetFiles(variants, "*.json").OrderBy(p => p, StringComparer.Ordinal))
                    phases.Add(("after-" + Path.GetFileNameWithoutExtension(path), File.ReadAllText(path), shipped));
            // Optional: other stations, "id:x0:x1:top:bottom[:pixelsPerMetre]" joined by ";" (a scouting pass).
            ClumpWindow[] windows = ClumpWindows;
            string stations = System.Environment.GetEnvironmentVariable("HH_CLUMP_WINDOWS");
            if (!string.IsNullOrEmpty(stations))
            {
                float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                windows = stations.Split(';').Select(s => s.Split(':')).Select(p =>
                    new ClumpWindow(p[0], F(p[1]), F(p[2]), F(p[3]), F(p[4]), p.Length > 5 ? F(p[5]) : 32)).ToArray();
            }
            string committedJson = JsonUtility.ToJson(committed.Clump);
            Behaviour pixel = _cam.GetComponents<Behaviour>().FirstOrDefault(b => b.GetType().Name == "PixelPerfectCamera");
            bool pixelEnabled = pixel != null && pixel.enabled, followEnabled = _follow.enabled;
            try
            {
                _follow.enabled = false; if (pixel != null) pixel.enabled = false;
                var clock = GameServices.Clock; Assert.IsNotNull(clock); Assert.IsNotNull(GameServices.Config);
                var config = GameServices.Config;
                int day = Enumerable.Range(0, config.DaysPerSeason * 4).First(elapsedDay =>
                {
                    CalendarDate date = GameCalendar.AtDay(elapsedDay, config.DaysPerSeason, config.StartDayOfYear);
                    return date.Season == Season.HighSummer && date.DayOfSeason == Mathf.Max(1, config.DaysPerSeason / 2);
                });
                double time = (day + ClumpMinute / 1440.0) * config.SecondsPerDay;
                Time.timeScale = 1; clock.SeekTo(time); clock.TimeScale = 0;
                Assert.AreEqual(Season.HighSummer, clock.Season);
                Color previous = Shader.GetGlobalColor("_DayNightTint"); int stable = 0; float waited = 0;
                for (int i = 0; i < 900; i++)
                {
                    yield return null; waited += Time.deltaTime;
                    Color current = Shader.GetGlobalColor("_DayNightTint");
                    float delta = Mathf.Abs(current.r - previous.r) + Mathf.Abs(current.g - previous.g) + Mathf.Abs(current.b - previous.b);
                    stable = delta < .0001f ? stable + 1 : 0; previous = current;
                    if (waited >= ClearRunSeconds && stable >= 4) break;
                    Assert.Less(i, 899, "the noon light did not settle");
                }
                field.Definition = definition;
                foreach (var phase in phases)
                {
                    JsonUtility.FromJsonOverwrite(committedJson, law);
                    if (phase.json != null) JsonUtility.FromJsonOverwrite(phase.json, law); else law.Strength = phase.strength;
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    field.Rebuild();
                    watch.Stop();
                    field.RefreshAppearance(); Assert.AreEqual((int)Season.HighSummer, field.CurrentSeason);
                    Assert.AreEqual(9115, field.AuthoredPlantCount, "the law never touches an authored plant");
                    foreach (var window in windows)
                    {
                        _cam.targetTexture = null;
                        if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
                        _w = Mathf.RoundToInt((window.X1 - window.X0) * window.PixelsPerMetre); _h = Mathf.RoundToInt((window.Top - window.Bottom) * window.PixelsPerMetre);
                        _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf); _rt.Create(); _cam.targetTexture = _rt;
                        _cam.orthographic = true; _cam.aspect = (float)_w / _h; _cam.orthographicSize = (window.Top - window.Bottom) / 2;
                        var centre = new Vector2((window.X0 + window.X1) / 2, (window.Top + window.Bottom) / 2);
                        _cam.transform.position = new Vector3(centre.x, centre.y, _cam.transform.position.z);
                        Time.timeScale = 1;
                        for (int i = 0; i < 30; i++) yield return null;
                        yield return ShadersReady();
                        Assert.Less(Vector2.Distance(_cam.transform.position, centre), .0001f, "the camera left its station");
                        var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
                        var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                        var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                        var times = new List<double>(); long draw = 0, batch = 0;
                        try
                        {
                            for (int i = 0; i < 90; i++)
                            {
                                yield return null;
                                if (cpu.Valid && cpu.LastValue > 0) times.Add(cpu.LastValue / 1000000.0);
                                if (draws.Valid) draw = Math.Max(draw, draws.LastValue);
                                if (batches.Valid) batch = Math.Max(batch, batches.LastValue);
                            }
                        }
                        finally { cpu.Dispose(); draws.Dispose(); batches.Dispose(); }
                        times.Sort(); Time.timeScale = 0; yield return null;
                        string file = window.Id + "-" + phase.name + ".png";
                        var image = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
                        try { image.LoadRawTextureData(Capture()); image.Apply(); File.WriteAllBytes(Path.Combine(folder, file), image.EncodeToPNG()); }
                        finally { Object.DestroyImmediate(image); }
                        var tint = Shader.GetGlobalColor("_DayNightTint");
                        var row = new ClumpNumbers { window = window.Id, phase = phase.name, file = file, strength = law.Strength,
                            tintR = tint.r, tintG = tint.g, tintB = tint.b, plants = field.PlantCount, authored = field.AuthoredPlantCount,
                            chunks = field.ChunkCount, missing = field.MissingCells, dormant = field.DormantCells,
                            draws = draw, batches = batch, rebuildMs = watch.Elapsed.TotalMilliseconds,
                            cpuP95Ms = times.Count > 0 ? times[Math.Min(times.Count - 1, (int)(times.Count * .95))] : -1 };
                        File.WriteAllText(Path.Combine(folder, window.Id + "-" + phase.name + ".json"), JsonUtility.ToJson(row, true));
                        TestContext.WriteLine("CLUMP " + JsonUtility.ToJson(row));
                        Debug.Log("CLUMP " + JsonUtility.ToJson(row));
                    }
                }
            }
            finally
            {
                field.Definition = committed; field.Rebuild();
                Object.Destroy(definition); Object.Destroy(law);
                _follow.enabled = followEnabled; if (pixel != null) pixel.enabled = pixelEnabled; Time.timeScale = 1;
            }
        }
    }
}
#endif
