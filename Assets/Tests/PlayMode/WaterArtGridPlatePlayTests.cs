using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The sea on the art grid, photographed</b> (owner ruling 2026-11-26, "no mixels"): the St Peters wharf
    /// head at high water (+2 m) and at low water, on a calm sea and in a breeze with whitecaps, at the 2x camera
    /// tier (1920x1080, two screen pixels to the art pixel) and at the close 5x deck tier. Every shot of one sea
    /// state is taken in ONE frozen frame (Time.timeScale 0, so the shader's _Time is the same number for every
    /// state), and the states are laid over it through the water renderers' property block:
    /// <c>s0</c> the old grid (_PixelsPerUnit 24, every new dial off), <c>s1</c> the art grid (32),
    /// <c>s1m</c> plus the calm mirror's edge axis, <c>s2</c> plus the shore band on the art cell and the shore
    /// foam tones, <c>s3</c> plus the depth colour bands, and <c>asset</c> the materials as shipped. A dial the
    /// shader does not have yet (main) is simply ignored, so the same fixture shoots main and the branch.
    /// Then an 8-frame pan (one art pixel a frame) of the shore band for the crawl check.
    /// Plates go to HH_WATER_GRID_PLATES (an absolute folder) or temporaryCachePath. Needs a GPU.
    /// </summary>
    public partial class WharfBuildingPass2PlatePlayTests
    {
#if UNITY_EDITOR
        const string WaterShaderName = "HiddenHarbours/Water";
        const string CalmMirrorCentreAxisGlobal = "_HHCalmMirrorCentreAxis";
        const int ArtPpu = CameraFollow.AssetsPPU;
        const float GridHighTide = 2.0f, GridLowTide = -0.85f;
        const float GridSettleSeconds = 6f;
        const int GridPanFrames = 8;

        sealed class GridSea : IEnvironmentService
        {
            readonly IEnvironmentService _inner;
            readonly float _level, _sea01;
            readonly Vector2 _wind;
            public GridSea(IEnvironmentService inner, float level, Vector2 wind, float sea01)
            { _inner = inner; _level = level; _wind = wind; _sea01 = sea01; }
            public int WorldSeed => _inner.WorldSeed;
            public TideProfile ActiveTideProfile { get => _inner.ActiveTideProfile; set => _inner.ActiveTideProfile = value; }
            public EnvironmentSample Sample()
            {
                var s = _inner.Sample();
                var band = (SeaState)Mathf.Clamp(Mathf.FloorToInt(_sea01 * (int)SeaState.Storm), (int)SeaState.Glass, (int)SeaState.Storm);
                return new EnvironmentSample(_wind, s.CurrentVector, _level, band, 1f, _sea01);
            }
            public float TideHeightAt(double seconds) => _level;
            public float WaterLevelAt(double seconds) => _level;
        }

        static string GridPlates
        {
            get
            {
                string env = System.Environment.GetEnvironmentVariable("HH_WATER_GRID_PLATES");
                return string.IsNullOrEmpty(env) ? Path.Combine(Application.temporaryCachePath, "WaterArtGridPlates") : env;
            }
        }

        // NaN = take the material's own value (the "asset" state).
        static readonly (string Name, float Ppu, float Snap, float ShoreBands, float DepthBands, float MirrorCentre)[] GridStates =
        {
            ("s0",    24f, 0f, 0f, 0f, 1f),
            ("s1",    32f, 0f, 0f, 0f, 1f),
            ("s1m",   32f, 0f, 0f, 0f, 0f),
            ("s2",    32f, 1f, float.NaN, 0f, 0f),
            ("s3",    32f, 1f, float.NaN, float.NaN, 0f),
            ("asset", float.NaN, float.NaN, float.NaN, float.NaN, 0f),
        };

        List<Renderer> WaterRenderers() => Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(r => r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name == WaterShaderName)
            .ToList();

        static void SetOrAsset(MaterialPropertyBlock mpb, Material m, string key, float v)
        {
            if (!m.HasProperty(key)) return;
            mpb.SetFloat(key, float.IsNaN(v) ? m.GetFloat(key) : v);
        }

        void ApplyGridState(List<Renderer> water, (string Name, float Ppu, float Snap, float ShoreBands, float DepthBands, float MirrorCentre) s)
        {
            var mpb = new MaterialPropertyBlock();
            foreach (var r in water)
            {
                r.GetPropertyBlock(mpb);
                var m = r.sharedMaterial;
                SetOrAsset(mpb, m, "_PixelsPerUnit", s.Ppu);
                SetOrAsset(mpb, m, "_ArtGridSnap", s.Snap);
                SetOrAsset(mpb, m, "_ShoreFoamBands", s.ShoreBands);
                SetOrAsset(mpb, m, "_DepthBands", s.DepthBands);
                r.SetPropertyBlock(mpb);
            }
            Shader.SetGlobalFloat(CalmMirrorCentreAxisGlobal, s.MirrorCentre);
        }

        // The St Peters sea is the displaced surface: its chunks copy the flat renderer's block once a frame and
        // the render feature draws them off-screen in the player loop, so a manual Render right after a block
        // change photographs the previous frame's sea. Two frames at timeScale 0 (the shader's _Time does not move)
        // let the copy and the off-screen draw both happen.
        static IEnumerator GridLetTheSeaRedraw()
        {
            yield return null;
            yield return null;
        }

        static float SnapToArt(float v) => Mathf.Round(v * ArtPpu) / ArtPpu;

        void GridCamera(Vector2 centre, int scale)
        {
            _follow.enabled = false;
            foreach (var c in _cam.GetComponents<Behaviour>().Where(x => x.GetType().Name == "PixelPerfectCamera")) c.enabled = false;
            if (_rt == null || _rt.width != PlateWidthPx || _rt.height != PlateHeightPx)
            {
                if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
                _w = PlateWidthPx; _h = PlateHeightPx;
                _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf) { filterMode = FilterMode.Point };
                _rt.Create();
            }
            _cam.targetTexture = _rt;
            _cam.transform.position = new Vector3(SnapToArt(centre.x), SnapToArt(centre.y), _cam.transform.position.z);
            _cam.orthographicSize = PlateHeightPx / (2f * ArtPpu * scale);
            _cam.aspect = (float)PlateWidthPx / PlateHeightPx;
        }

        void GridSave(string name)
        {
            Directory.CreateDirectory(GridPlates);
            var texture = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            try { texture.LoadRawTextureData(Capture()); texture.Apply(); File.WriteAllBytes(Path.Combine(GridPlates, name + ".png"), texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
        }

        string ScreenPx(Bounds b, int scale)
        {
            float pxm = ArtPpu * scale;
            Vector3 c = _cam.transform.position;
            float left = c.x - _cam.orthographicSize * _cam.aspect, bottom = c.y - _cam.orthographicSize;
            return string.Format(CultureInfo.InvariantCulture, "minPx=({0:0.###},{1:0.###}) maxPx=({2:0.###},{3:0.###})",
                (b.min.x - left) * pxm, (b.min.y - bottom) * pxm, (b.max.x - left) * pxm, (b.max.y - bottom) * pxm);
        }

        IEnumerator GridHoldSea(IEnvironmentService inner, float level, Vector2 wind, float sea01)
        {
            GameServices.Environment = new GridSea(inner, level, wind, sea01);
            Time.timeScale = 1;
            float start = Time.realtimeSinceStartup;
            do { yield return null; }
            while (Time.realtimeSinceStartup < start + 30f &&
                   (Time.realtimeSinceStartup < start + GridSettleSeconds ||
                    Shader.GetGlobalVector("_HHSeaLevelWorld").w < .5f ||
                    Mathf.Abs(Shader.GetGlobalVector("_HHSeaLevelWorld").x - level) > .01f));
            Assert.That(Shader.GetGlobalVector("_HHSeaLevelWorld").x, Is.EqualTo(level).Within(.01f), "the drawn sea never re-read the tide");
            Time.timeScale = 0;
        }

        [UnityTest]
        public IEnumerator StPeters_WaterOnTheArtGrid_BeforeAndAfter()
        {
            RequireAGraphicsDevice();
            yield return LoadRegion(StPeters);
            EnsureTheFollow(); HoldTheMood();
            IEnvironmentService held = GameServices.Environment;
            Directory.CreateDirectory(GridPlates);
            string numbers = Path.Combine(GridPlates, "numbers.txt");
            File.WriteAllText(numbers, $"run {DateTime.UtcNow:o} seed={held.WorldSeed}\n");

            var deck = GameObject.Find("StPetersWharf")?.transform.Find("Deck/WharfPass2Deck")?.GetComponent<SpriteRenderer>();
            var ladder = GameObject.Find("Fitting_ladder");
            var dory = GameObject.Find("Dory");
            File.AppendAllText(numbers, $"deck={(deck ? deck.bounds.ToString("F4") : "none")} ladder={(ladder ? ladder.transform.position.ToString("F4") : "none")} dory={(dory ? dory.transform.position.ToString("F4") : "none")}\n");

            var framings = new List<(string Id, Vector2 At, int Scale)> { ("wide2x", new Vector2(203f, 2.649f), 2) };
            if (dory != null) framings.Add(("dory2x", (Vector2)dory.transform.position, 2));
            framings.Add(("deck5x", ladder != null ? (Vector2)ladder.transform.position + new Vector2(0f, -1.5f) : new Vector2(190f, 5.5f), 5));

            var tides = new[] { ("high", GridHighTide), ("low", GridLowTide) };
            var seas = new[] { ("calm", new Vector2(0.4f, 0.3f), 0.05f), ("breeze", new Vector2(5.6f, 4.2f), 0.35f) };
            try
            {
                yield return PinTheHour(Noon, ClearRunSeconds);
                foreach (var (tideId, level) in tides)
                foreach (var (seaId, wind, sea01) in seas)
                {
                    yield return GridHoldSea(held, level, wind, sea01);
                    yield return ShadersReady();
                    var water = WaterRenderers();
                    Assert.IsNotEmpty(water, "no renderer draws the water shader");
                    File.AppendAllText(numbers, $"{tideId}-{seaId}: tide={level:R} wind={wind} sea01={sea01:R} hour={GameServices.Clock.HourOfDay:R} time={Time.time:R} waterRenderers={water.Count} matPPU={water[0].sharedMaterial.GetFloat("_PixelsPerUnit"):R}\n");
                    if (tideId == "high" && seaId == "calm")
                        File.AppendAllText(numbers, "  water renderers: " + string.Join(", ", water.Select(r => $"{r.name}[{r.GetType().Name} enabled={r.enabled} active={r.gameObject.activeInHierarchy}]")) + "\n");
                    foreach (var f in framings)
                    {
                        GridCamera(f.At, f.Scale);
                        File.AppendAllText(numbers, $"  {f.Id}: camera={_cam.transform.position.ToString("F5")} ortho={_cam.orthographicSize:R} deck {(deck ? ScreenPx(deck.bounds, f.Scale) : "none")}\n");
                        foreach (var s in GridStates)
                        {
                            ApplyGridState(water, s);
                            yield return GridLetTheSeaRedraw();
                            GridSave($"{f.Id}-{tideId}-{seaId}-{s.Name}");
                        }
                    }
                    if (tideId == "high" && seaId == "breeze")
                    {
                        foreach (var s in GridStates.Where(x => x.Name == "s1" || x.Name == "s3"))
                        {
                            ApplyGridState(water, s);
                            for (int i = 0; i < GridPanFrames; i++)
                            {
                                GridCamera(new Vector2(203f, 2.649f) + new Vector2(i, i) / ArtPpu, 2);
                                yield return GridLetTheSeaRedraw();
                                GridSave($"pan-{s.Name}-{i}");
                            }
                        }
                    }
                }
            }
            finally
            {
                var water = WaterRenderers();
                ApplyGridState(water, GridStates.Last());
                Shader.SetGlobalFloat(CalmMirrorCentreAxisGlobal, 0f);
                GameServices.Environment = held;
                LandingPropsReleaseCamera();
            }
        }
#endif
    }
}
