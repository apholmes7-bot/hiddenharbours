using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE SEA AT EVERY HOUR, AND WHAT EACH LAYER ADDS</b>: the St Peters wharf's north water photographed at
    /// 13:00, 17:00, 18:30 and 20:30 on day 59 through the game's own camera, at the closest walking stop and the
    /// farthest, plus one open-sea and one shoreline frame at 18:30. At the close framing each hour is shot four
    /// ways in the same frozen frame, each with one layer of the water switched off on the water's own property
    /// block: as drawn, the seabed hidden (no see-through), the sky reflection off, and both off (the water body
    /// alone). The mean colour of the water inside depth bands (by the on-foot depth model) is logged per shot, so
    /// a layer's share of the colour is a subtraction, not a guess.
    ///
    /// <para>A look tool for the owner's eye: it asserts only that the frames were taken. No GPU on CI: every
    /// case skips there before anything loads. Plates go to <c>HH_DUSK_SEA_DIR</c> when set, else to
    /// <c>temporaryCachePath/dusk-sea-plates</c>.</para>
    /// </summary>
    public class DuskSeaPlatePlayTests
    {
        private const string PlateDir = "dusk-sea-plates";
        private const string DirEnv = "HH_DUSK_SEA_DIR";
        private const string StPetersScene = "StPeters";
        private const int Day = 59;
        private static readonly float[] Hours = { 13f, 17f, 18.5f, 20.5f };
        private const float WideHour = 18.5f;
        /// <summary>The take that showed the fault was centred here (m).</summary>
        private static readonly Vector2 WharfNorth = new Vector2(207.9f, 7.4f);
        /// <summary>The open-sea frame is centred on the deepest drawn water found, if deeper than this (m).</summary>
        private const float OpenSeaDepth = 2f;
        /// <summary>A shoreline frame is centred on water this shallow (m).</summary>
        private const float ShoreMinDepth = 0.15f, ShoreMaxDepth = 0.5f;
        /// <summary>The depth bands the colour is averaged in (m).</summary>
        private static readonly float[] BandEdges = { 0.05f, 0.6f, 1.5f, 2.5f, 4f, 50f };
        /// <summary>The mask is sampled every this many plate pixels.</summary>
        private const int MaskStep = 6;
        /// <summary>A turbidity this high passes no light to the bed at any depth the region has.</summary>
        private const float OpaqueTurbidity = 60f;

        private static readonly int IdTurbidity = Shader.PropertyToID("_Turbidity");
        private static readonly int IdReflection = Shader.PropertyToID("_ReflectionStrength");
        private static readonly int IdSkyReflection = Shader.PropertyToID("_SkyReflectionStrength");
        private static readonly int IdLowSunSheen = Shader.PropertyToID("_LowSunSheen");
        private static readonly int IdGrade = Shader.PropertyToID("_PaletteGradeStrength");
        private const string WaterShaderName = "HiddenHarbours/Water";

        private WharfNightStage _stage;
        private Transform _anchor;
        private CameraFollow _follow;
        private readonly StringBuilder _log = new StringBuilder();
        private string _dir;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (_stage != null) { yield return _stage.TearDown(); _stage = null; }
        }

        [UnityTest, Timeout(1_800_000)]
        public IEnumerator Plates_StPetersNorthWater_FourHours_TwoZooms_AndTheLayers()
        {
            WharfNightStage.RequireAGraphicsDevice();
            string env = System.Environment.GetEnvironmentVariable(DirEnv);
            _dir = !string.IsNullOrEmpty(env) ? env : Path.Combine(Application.temporaryCachePath, PlateDir);
            Directory.CreateDirectory(_dir);

            _stage = new WharfNightStage(StPetersScene, PlateDir);
            yield return _stage.Load();
            if (GameServices.Clock == null)
            {
                var live = Object.FindFirstObjectByType<HiddenHarbours.Environment.GameClock>();
                if (live != null) GameServices.Clock = live;
            }
            Assert.IsNotNull(GameServices.Clock, "harness: no clock");
            Assert.IsNotNull(GameServices.Config, "harness: no config");
            GameServices.Clock.TimeScale = 0f;
            yield return SeekHour(Hours[0]);
            yield return _stage.FrameOn(WharfNorth);
            _anchor = GameObject.Find("PlateAnchor")?.transform;
            _follow = _stage.Camera.GetComponent<CameraFollow>();
            Assert.IsNotNull(_anchor, "harness: the stage left no anchor");
            Assert.IsNotNull(_follow, "harness: no follow on the stage camera");

            var zoom = GameServices.Config.PlayerZoom.Sanitized();
            _log.AppendLine($"seed {GameServices.Environment?.WorldSeed}; day {Day}; wharf north {WharfNorth}; " +
                            $"zooms closest {zoom.ClosestWorldHeightMeters} m, " +
                            $"default {zoom.DefaultWorldHeightMeters} m, farthest {zoom.FarthestWorldHeightMeters} m");

            int shots = 0;
            foreach (float hour in Hours)
            {
                yield return SeekHour(hour);
                string hh = HourLabel(hour);
                yield return Frame(WharfNorth, zoom.ClosestWorldHeightMeters);
                LogUniforms(hh);
                bool[][] bands = Bands();
                shots += ShootLayers($"wharf-close-{hh}", bands);
                yield return Frame(WharfNorth, zoom.FarthestWorldHeightMeters);
                shots += ShootOne($"wharf-far-{hh}", Bands());
                if (Mathf.Approximately(hour, WideHour))
                {
                    FindFrames(out Vector2 open, out Vector2 shore);
                    _log.AppendLine($"  open sea frame {open}; shoreline frame {shore}");
                    yield return Frame(open, zoom.DefaultWorldHeightMeters);
                    shots += ShootOne($"open-sea-{hh}", Bands());
                    yield return Frame(shore, zoom.DefaultWorldHeightMeters);
                    shots += ShootOne($"shoreline-{hh}", Bands());
                }
            }
            File.WriteAllText(Path.Combine(_dir, "dusk-sea-log.txt"), _log.ToString(), new UTF8Encoding(false));
            Debug.Log("[dusk-sea] " + _log.ToString().Replace("\n", "\n[dusk-sea] "));
            Assert.Greater(shots, 0, "no plate was taken");
        }

        private static string HourLabel(float hour)
        {
            int h = Mathf.FloorToInt(hour), m = Mathf.RoundToInt((hour - h) * 60f);
            return h.ToString("00", CultureInfo.InvariantCulture) + m.ToString("00", CultureInfo.InvariantCulture);
        }

        private IEnumerator SeekHour(float hour)
        {
            Time.timeScale = 1f;
            GameServices.Clock.TimeScale = 0f;
            GameServices.Clock.SeekTo((Day + hour / 24.0) * GameServices.Config.SecondsPerDay);
            Color tint = Shader.GetGlobalColor("_DayNightTint");
            int still = 0;
            for (int f = 0; f < 900 && still < 6; f++)
            {
                yield return null;
                Color now = Shader.GetGlobalColor("_DayNightTint");
                float move = Mathf.Abs(now.r - tint.r) + Mathf.Abs(now.g - tint.g) + Mathf.Abs(now.b - tint.b);
                still = move < 1e-4f && f > 30 ? still + 1 : 0;
                tint = now;
            }
            for (int f = 0; f < 60; f++) yield return null;     // the water mood eases on engine time too
            Time.timeScale = 0f;
            yield return null;
        }

        private IEnumerator Frame(Vector2 at, float worldHeight)
        {
            Time.timeScale = 1f;
            _anchor.position = new Vector3(at.x, at.y, 0f);
            _follow.SetFraming(worldHeight, 0f);
            Camera cam = _stage.Camera;
            Vector3 lastPos = cam.transform.position;
            float lastSize = cam.orthographicSize;
            int still = 0;
            for (int f = 0; f < 400 && still < 5; f++)
            {
                yield return null;
                bool moved = (cam.transform.position - lastPos).sqrMagnitude > 1e-8f ||
                             Mathf.Abs(cam.orthographicSize - lastSize) > 1e-5f;
                still = moved ? 0 : still + 1;
                lastPos = cam.transform.position;
                lastSize = cam.orthographicSize;
            }
            for (int f = 0; f < 4; f++) yield return null;
            Time.timeScale = 0f;
            yield return null;
        }

        private void FindFrames(out Vector2 open, out Vector2 shore)
        {
            open = WharfNorth; shore = WharfNorth;
            float deepest = OpenSeaDepth, bestShore = float.MaxValue, farthest = 0f;
            ReadHeight();
            float level = WaterLevel();
            for (float r = 8f; r <= 160f; r += 4f)
                for (float a = 0f; a < 360f; a += 10f)
                {
                    Vector2 p = WharfNorth + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * r;
                    if (StandableSurfaces.TryGetDeckElevationNow(p, out _)) continue;
                    float d = DrawnDepthAt(p, level);
                    if (float.IsNaN(d)) d = TidalWalkability.DepthNow(p);
                    // The deepest water found, and among equally deep water (a flat floor) the farthest out.
                    if (d > deepest + 1e-3f || (d > deepest - 1e-3f && r > farthest))
                    {
                        deepest = Mathf.Max(deepest, d); farthest = r; open = p;
                    }
                    if (d > ShoreMinDepth && d < ShoreMaxDepth && r > 15f && r < bestShore) { bestShore = r; shore = p; }
                }
        }

        private void LogUniforms(string hh)
        {
            Color tint = Shader.GetGlobalColor("_DayNightTint");
            _log.AppendLine($"{hh}: clock {GameServices.Clock.HourOfDay:0.00} h, _DayNightTint ({tint.r:0.000}, " +
                            $"{tint.g:0.000}, {tint.b:0.000}), _SunElevation {Shader.GetGlobalFloat("_SunElevation"):0.000}");
            var mpb = new MaterialPropertyBlock();
            foreach (Renderer r in WaterRenderers())
            {
                r.GetPropertyBlock(mpb);
                Material m = r.sharedMaterial;
                _log.AppendLine($"  water '{r.name}' ({r.GetType().Name}, visible {r.isVisible}) mat {m?.name}: turbidity {Read(mpb, m, "_Turbidity")}, reflection " +
                                $"{Read(mpb, m, "_ReflectionStrength")}, sky tint {Read(mpb, m, "_ReflectionSkyTint")}, chop " +
                                $"{Read(mpb, m, "_Chop")}, roughness {Read(mpb, m, "_Roughness")}, sky content " +
                                $"{Read(mpb, m, "_SkyReflectionStrength")}, deep blue {Read(mpb, m, "_DeepBlueStrength")}");
            }
        }

        private static string Read(MaterialPropertyBlock mpb, Material m, string name)
        {
            if (mpb.HasFloat(name)) return mpb.GetFloat(name).ToString("0.###", CultureInfo.InvariantCulture) + "(mpb)";
            if (m != null && m.HasProperty(name)) return m.GetFloat(name).ToString("0.###", CultureInfo.InvariantCulture);
            return "-";
        }

        /// <summary>Every renderer drawing the water shader in the loaded world.</summary>
        private static List<Renderer> WaterRenderers()
        {
            var list = new List<Renderer>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Material m = r != null ? r.sharedMaterial : null;
                if (m != null && m.shader != null && m.shader.name == WaterShaderName) list.Add(r);
            }
            return list;
        }

        // ---- the drawn depth: the water shader's own height read, decoded on the CPU -------------------------
        private float[] _height;
        private int _hw, _hh;
        private Vector4 _hMin, _hSize;
        private float _hLo, _hHi;

        /// <summary>Read the height map the visible water samples, through a blit (it need not be readable).</summary>
        private void ReadHeight()
        {
            _height = null;
            var mpb = new MaterialPropertyBlock();
            foreach (Renderer r in WaterRenderers())
            {
                r.GetPropertyBlock(mpb);
                Material m = r.sharedMaterial;
                Texture tex = mpb.HasTexture("_HeightTex") ? mpb.GetTexture("_HeightTex")
                            : m.HasProperty("_HeightTex") ? m.GetTexture("_HeightTex") : null;
                if (tex == null) continue;
                _hMin = mpb.HasVector("_HeightWorldMin") ? mpb.GetVector("_HeightWorldMin") : m.GetVector("_HeightWorldMin");
                _hSize = mpb.HasVector("_HeightWorldSize") ? mpb.GetVector("_HeightWorldSize") : m.GetVector("_HeightWorldSize");
                _hLo = mpb.HasFloat("_HeightMin") ? mpb.GetFloat("_HeightMin") : m.GetFloat("_HeightMin");
                _hHi = mpb.HasFloat("_HeightMax") ? mpb.GetFloat("_HeightMax") : m.GetFloat("_HeightMax");
                _hw = tex.width; _hh = tex.height;
                var rt = RenderTexture.GetTemporary(_hw, _hh, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                Graphics.Blit(tex, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var read = new Texture2D(_hw, _hh, TextureFormat.RGBAFloat, false, true);
                read.ReadPixels(new Rect(0, 0, _hw, _hh), 0, 0);
                read.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Color[] px = read.GetPixels();
                Object.DestroyImmediate(read);
                _height = new float[px.Length];
                for (int i = 0; i < px.Length; i++) _height[i] = px[i].r;
                _log.AppendLine($"  height map from '{r.name}': {_hw}x{_hh}, world min {_hMin}, size {_hSize}, " +
                                $"elevation {_hLo}..{_hHi} m");
                return;
            }
            _log.AppendLine("  height map: none found on a visible water renderer");
        }

        /// <summary>The water level the frame draws with (a global or the material's).</summary>
        private float WaterLevel()
        {
            var mpb = new MaterialPropertyBlock();
            foreach (Renderer r in WaterRenderers())
            {
                r.GetPropertyBlock(mpb);
                if (mpb.HasFloat("_WaterLevel")) return mpb.GetFloat("_WaterLevel");
                return r.sharedMaterial.GetFloat("_WaterLevel");
            }
            return float.NaN;
        }

        /// <summary>Per drawn-depth band, which sampled pixels are open water (no deck over them) in that band. A
        /// coarse grid of drawn and on-foot depth is logged so the two models can be compared.</summary>
        private bool[][] Bands()
        {
            ReadHeight();
            float level = WaterLevel();
            Camera cam = _stage.Camera;
            int w = _stage.Width, h = _stage.Height;
            var masks = new bool[BandEdges.Length - 1][];
            for (int b = 0; b < masks.Length; b++) masks[b] = new bool[w * h];
            var grid = new StringBuilder();
            for (int y = MaskStep / 2; y < h; y += MaskStep)
                for (int x = MaskStep / 2; x < w; x += MaskStep)
                {
                    Vector3 wp = cam.ViewportToWorldPoint(new Vector3((x + 0.5f) / w, (y + 0.5f) / h, 0f));
                    Vector2 p = wp;
                    if (StandableSurfaces.TryGetDeckElevationNow(p, out _)) continue;
                    float d = DrawnDepthAt(p, level);
                    if (float.IsNaN(d)) d = TidalWalkability.DepthNow(p);
                    for (int b = 0; b < masks.Length; b++)
                        if (d >= BandEdges[b] && d < BandEdges[b + 1]) masks[b][y * w + x] = true;
                }
            for (int gy = 3; gy >= 0; gy--)
            {
                grid.Append("    ");
                for (int gx = 0; gx < 5; gx++)
                {
                    Vector2 p = cam.ViewportToWorldPoint(new Vector3((gx + 0.5f) / 5f, (gy + 0.5f) / 4f, 0f));
                    grid.Append($"({p.x:0.0},{p.y:0.0}) drawn {DrawnDepthAt(p, level):0.00} walk {TidalWalkability.DepthNow(p):0.00}  ");
                }
                grid.AppendLine();
            }
            _log.AppendLine($"  water level {level:0.000}; depth grid, top row first:");
            _log.Append(grid);
            return masks;
        }

        private float DrawnDepthAt(Vector2 p, float level)
        {
            if (_height == null || float.IsNaN(level)) return float.NaN;
            float u = (p.x - _hMin.x) / Mathf.Max(_hSize.x, 1e-3f), v = (p.y - _hMin.y) / Mathf.Max(_hSize.y, 1e-3f);
            if (u < 0f || u > 1f || v < 0f || v > 1f) return float.NaN;
            int x = Mathf.Clamp(Mathf.FloorToInt(u * _hw), 0, _hw - 1), y = Mathf.Clamp(Mathf.FloorToInt(v * _hh), 0, _hh - 1);
            return level - Mathf.Lerp(_hLo, _hHi, _height[y * _hw + x]);
        }

        /// <summary>The displaced sea copies the flat sea's block onto its chunks once a frame; a plate taken
        /// inside one frame has to ask for that copy, or the chunks draw the block of the frame before.</summary>
        private static void SyncDisplaced()
        {
            MethodInfo sync = typeof(DisplacedWaterSurface).GetMethod("SyncUniforms",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(sync, "harness: DisplacedWaterSurface.SyncUniforms is gone");
            foreach (DisplacedWaterSurface d in Object.FindObjectsByType<DisplacedWaterSurface>(FindObjectsSortMode.None))
                sync.Invoke(d, new object[] { false });
        }

        private int ShootOne(string name, bool[][] bands)
        {
            byte[] px = _stage.Capture();
            _stage.SavePlate(name + ".png", px);
            Copy(name + ".png");
            _log.AppendLine($"  {name}: {Means(px, bands)}");
            return 1;
        }

        [System.Flags]
        private enum Off { None = 0, Bed = 1, Reflection = 2, LowSun = 4, Grade = 8 }

        private int ShootLayers(string name, bool[][] bands)
        {
            List<Renderer> renderers = WaterRenderers();
            var originals = new MaterialPropertyBlock[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
            {
                originals[i] = new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(originals[i]);
            }
            int n = ShootOne(name + "-asdrawn", bands);
            n += ShootWith(name + "-nobed", renderers, originals, Off.Bed, bands);
            n += ShootWith(name + "-noreflection", renderers, originals, Off.Reflection, bands);
            n += ShootWith(name + "-nolowsun", renderers, originals, Off.LowSun, bands);
            n += ShootWith(name + "-nograde", renderers, originals, Off.Grade, bands);
            n += ShootWith(name + "-bodyonly", renderers, originals, Off.Bed | Off.Reflection | Off.LowSun, bands);
            for (int i = 0; i < renderers.Count; i++) renderers[i].SetPropertyBlock(originals[i]);
            return n;
        }

        private int ShootWith(string name, List<Renderer> renderers, MaterialPropertyBlock[] originals, Off off,
                              bool[][] bands)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer r = renderers[i];
                r.SetPropertyBlock(originals[i]);
                var m = new MaterialPropertyBlock();
                r.GetPropertyBlock(m);
                Material mat = r.sharedMaterial;
                if ((off & Off.Bed) != 0) m.SetFloat(IdTurbidity, OpaqueTurbidity);
                if ((off & Off.Reflection) != 0) { m.SetFloat(IdReflection, 0f); m.SetFloat(IdSkyReflection, 0f); }
                if ((off & Off.LowSun) != 0)
                {
                    Vector4 s = m.HasVector(IdLowSunSheen) ? m.GetVector(IdLowSunSheen) : mat.GetVector(IdLowSunSheen);
                    s.x = 0f;
                    m.SetVector(IdLowSunSheen, s);
                }
                if ((off & Off.Grade) != 0) m.SetFloat(IdGrade, 0f);
                r.SetPropertyBlock(m);
            }
            SyncDisplaced();
            int n = ShootOne(name, bands);
            for (int i = 0; i < renderers.Count; i++) renderers[i].SetPropertyBlock(originals[i]);
            SyncDisplaced();
            return n;
        }

        private void Copy(string file)
        {
            string from = Path.Combine(Application.temporaryCachePath, PlateDir, file);
            string to = Path.Combine(_dir, file);
            if (from != to && File.Exists(from)) File.Copy(from, to, true);
        }

        private static string Means(byte[] px, bool[][] bands)
        {
            var sb = new StringBuilder();
            for (int b = 0; b < bands.Length; b++)
            {
                double r = 0, g = 0, bl = 0; int c = 0;
                bool[] mask = bands[b];
                for (int i = 0; i < mask.Length; i++)
                {
                    if (!mask[i]) continue;
                    r += px[i * 4]; g += px[i * 4 + 1]; bl += px[i * 4 + 2]; c++;
                }
                if (c == 0) continue;
                sb.Append($"[{BandEdges[b]:0.##}-{BandEdges[b + 1]:0.##} m n{c}: {r / c:0} {g / c:0} {bl / c:0}] ");
            }
            return sb.ToString();
        }
    }
}
