using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // Decks over calm water, photographed with their underside OFF (the share at 0, the look before) and ON,
    // at pinned tides on held calm water, 32 px a metre, the follow and the pixel camera off. Shares the
    // GPU/title-only harness. A local look for the owner; it asserts only that each named subject was found.
    public partial class WharfBuildingPass2PlatePlayTests
    {
        const string UndersidePlatesVariable = "HH_UNDERSIDE_PLATES";
        const float UndersideMarginMetres = 3f, UndersideBelowMetres = 9f, UndersideMaxWidth = 48f, UndersideMaxHeight = 36f;

        readonly struct UndersideSubject
        {
            public readonly string Scene, Id, Name;
            public readonly float[] Tides;
            public UndersideSubject(string scene, string id, string name, params float[] tides)
            { Scene = scene; Id = id; Name = name; Tides = tides; }
        }

        static UndersideSubject[] UndersideSubjects() => new[]
        {
            new UndersideSubject("StPeters", "stp_pier", "StPetersWharf", -2.0f, 0f, 2.0f),
            new UndersideSubject("StPeters", "stp_ladder", "~#WharfLadder", -2.0f, 0f, 2.0f),
            new UndersideSubject("StPeters", "check_stp_gap_footbridge", "bridge.stp_gap_footbridge", -2.0f, 2.0f),
            new UndersideSubject("NineMileCreek", "nmc_pierW", "pierW/walk0", -1.5f, 1.5f),
            new UndersideSubject("NineMileCreek", "nmc_w1", "w1.walk", -1.5f, 1.5f),
            new UndersideSubject("NineMileCreek", "nmc_float_working", "float.nmc_working", -1.5f, 1.5f),
            new UndersideSubject("NineMileCreek", "nmc_gangway_west", "gangway.nmc_west_gangway", -1.5f, 1.5f),
            new UndersideSubject("NineMileCreek", "nmc_ladder", "~#WharfLadder", -1.5f, 1.5f),
            new UndersideSubject("NineMileCreek", "check_slab", "slab/walk0", -1.5f, 1.5f),
            new UndersideSubject("NineMileCreek", "check_shed", "shed/walk0", -1.5f, 1.5f),
            new UndersideSubject("NineMileCreek", "check_route91_bridge", "~Route91RiverBridge", -1.5f, 1.5f),
            new UndersideSubject("NineMileCreek", "check_camp_brook_bridge", "~CampBrookBridge", -1.5f, 1.5f),
            new UndersideSubject("NineMileCreek", "check_camp_pond_culvert", "~CampPondCulvert", -1.5f, 1.5f),
            new UndersideSubject("NineMileCreek", "check_dispenser_sdock", "~dispenser_sDock", -1.5f, 1.5f),
            new UndersideSubject("NineMileCreek", "check_wharf_shower", "~WharfShower", -1.5f, 1.5f),
        };

#if UNITY_EDITOR
        /// <summary>The held weather with the sea laid flat and the tide pinned: the mirror's calm case.</summary>
        sealed class CalmPinnedTide : IEnvironmentService
        {
            readonly IEnvironmentService _inner;
            readonly float _level;
            public CalmPinnedTide(IEnvironmentService inner, float level) { _inner = inner; _level = level; }
            public int WorldSeed => _inner.WorldSeed;
            public TideProfile ActiveTideProfile { get => _inner.ActiveTideProfile; set => _inner.ActiveTideProfile = value; }
            public EnvironmentSample Sample()
            {
                var s = _inner.Sample();
                return new EnvironmentSample(Vector2.zero, s.CurrentVector, _level, s.SeaState, s.Visibility, 0f);
            }
            public float TideHeightAt(double seconds) => _level;
            public float WaterLevelAt(double seconds) => _level;
        }

        /// <summary>The view that draws with the calm mirror's dial at 0: what the water shows with no mirror at all.</summary>
        const int NoMirrorView = -1;

        /// <summary>0 off, 1 on, then the mirror's own debug views (HH_UNDERSIDE_PLATES_DEBUG, e.g. "1,3": classes,
        /// deck tops; "m" adds the plate with the mirror's dial at 0).</summary>
        static IEnumerable<int> Views()
        {
            yield return 0; yield return 1;
            string d = System.Environment.GetEnvironmentVariable(UndersidePlatesVariable + "_DEBUG");
            if (string.IsNullOrEmpty(d)) yield break;
            foreach (string v in d.Split(','))
            {
                if (v.Trim() == "m") yield return NoMirrorView;
                else if (v.Trim() == "h") for (int g = 0; g < HideGroups().Length; g++) yield return HiddenView - g;
                else if (int.TryParse(v, out int n) && n > 0) yield return n + 1;
            }
        }

        /// <summary>Views HiddenView, HiddenView-1, ... draw with the mirror on and one group of objects' renderers
        /// switched off (HH_UNDERSIDE_PLATES_HIDE: groups split by ';', exact object names by ','; "h" in the debug
        /// list adds them): what each group adds to the picture.</summary>
        const int HiddenView = -2;

        static string[] HideGroups()
        {
            string hide = System.Environment.GetEnvironmentVariable(UndersidePlatesVariable + "_HIDE");
            return string.IsNullOrEmpty(hide) ? new string[0] : hide.Split(';').Where(g => g.Trim().Length > 0).ToArray();
        }

        static List<Renderer> HiddenRenderers(int view)
        {
            var found = new List<Renderer>();
            string[] groups = HideGroups();
            int g = HiddenView - view;
            if (view > HiddenView || g >= groups.Length) return found;
            var names = new HashSet<string>(groups[g].Split(',').Select(n => n.Trim()).Where(n => n.Length > 0));
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
                if (names.Contains(t.name))
                    found.AddRange(t.GetComponentsInChildren<Renderer>().Where(r => r.enabled));
            return found;
        }

        static string ViewName(int view) =>
            view <= HiddenView ? "hidden" + (HiddenView - view) : view == NoMirrorView ? "nomirror" : view == 0 ? "off" : view == 1 ? "on" : "debug" + (view - 1);

        /// <summary>Looks only: the calm mirror's debug view (an internal static of the Art assembly).</summary>
        static void SetMirrorDebugView(float view)
        {
            var type = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("HiddenHarbours.Art.CalmMirror", false)).FirstOrDefault(t => t != null);
            var field = type?.GetField("DebugView", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            field?.SetValue(null, view);
        }

        static bool UndersideBounds(string name, out Rect r, out string how)
        {
            r = default; how = "";
            var outline = new Vector2[32]; var elevation = new float[32];
            if (!name.StartsWith("~"))
            {
                foreach (var b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
                {
                    if (!(b is IStandableOutline o)) continue;
                    Transform t = b.transform;
                    string path = t.parent != null ? t.parent.name + "/" + t.name : t.name;
                    if (t.name != name && path != name) continue;
                    int n = o.GetDrawnOutline(outline, elevation);
                    if (n == 0) continue;
                    float x0 = outline.Take(n).Min(p => p.x), x1 = outline.Take(n).Max(p => p.x);
                    float y0 = outline.Take(n).Min(p => p.y), y1 = outline.Take(n).Max(p => p.y);
                    r = Rect.MinMaxRect(x0, y0, x1, y1);
                    how = $"drawn outline of {b.GetType().Name} '{path}', deck {elevation[0]:0.00} m";
                    return true;
                }
                return false;
            }
            string part = name.Substring(1);
            // "~#Type" is the first component of that type (a WharfLadder), "~text" the first object named with it.
            GameObject go = part.StartsWith("#")
                ? Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)
                    .Where(m => m.GetType().Name == part.Substring(1)).Select(m => m.gameObject)
                    .OrderBy(g => g.name, System.StringComparer.Ordinal).FirstOrDefault()
                : Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)
                    .Where(t => t.name.StartsWith(part)).Select(t => t.gameObject)
                    .OrderBy(g => g.name, System.StringComparer.Ordinal).FirstOrDefault();
            if (go == null) return false;
            var rs = go.GetComponentsInChildren<Renderer>().Where(x => x.enabled).ToArray();
            if (rs.Length == 0) { r = new Rect((Vector2)go.transform.position - Vector2.one * 3f, Vector2.one * 6f); how = $"'{go.name}' position (no renderer)"; return true; }
            Bounds all = rs[0].bounds; foreach (var x in rs) all.Encapsulate(x.bounds);
            r = Rect.MinMaxRect(all.min.x, all.min.y, all.max.x, all.max.y);
            how = $"renderer bounds of '{go.name}' ({rs.Length} renderer(s))";
            return true;
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator Undersides_DecksOverCalmWater_OffBesideOn_AtPinnedTides()
        {
            RequireAGraphicsDevice();
            string dir = System.Environment.GetEnvironmentVariable(UndersidePlatesVariable);
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.temporaryCachePath, PlateDir, "undersides");
            Directory.CreateDirectory(dir);
            var numbers = new StringBuilder();
            File.WriteAllText(Path.Combine(dir, "numbers.txt"), "");
            var missing = new List<string>();
            string only = System.Environment.GetEnvironmentVariable(UndersidePlatesVariable + "_ONLY");
            // HH_UNDERSIDE_PLATES_HOUR: the hour the plates are shot at (default noon), e.g. the golden hour 19.5.
            float hour = float.TryParse(System.Environment.GetEnvironmentVariable(UndersidePlatesVariable + "_HOUR"),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsedHour) ? parsedHour : Noon;
            foreach (var scene in UndersideSubjects().Where(s => string.IsNullOrEmpty(only) || only.Split(',').Any(o => s.Id.Contains(o)))
                         .GroupBy(s => s.Scene))
            {
                yield return LoadRegion(scene.Key);
                EnsureTheFollow();
                HoldTheMood();
                _look = "undersides";
                _lookEvidence = "held calm (sea 0, no wind), tide pinned, 32 px a metre, follow and pixel camera off";
                yield return PinTheHour(hour, ClearRunSeconds);

                IEnvironmentService held = GameServices.Environment;
                GameConfig config = GameServices.Config;
                GameConfig off = Object.Instantiate(config);
                off.WaterMirror.UndersideShare = 0f;
                GameConfig noMirror = Object.Instantiate(config);
                noMirror.WaterMirrorStrength = 0f;
                bool followEnabled = _follow.enabled;
                float aspect = _cam.aspect, ortho = _cam.orthographicSize;
                Vector3 camAt = _cam.transform.position;
                var pixelCameras = _cam.GetComponents<Behaviour>()
                    .Where(b => b.GetType().Name == "PixelPerfectCamera" && b.enabled).ToArray();
                try
                {
                    foreach (UndersideSubject s in scene)
                    {
                        if (!UndersideBounds(s.Name, out Rect deck, out string how)) { missing.Add(s.Scene + "/" + s.Name); continue; }
                        float x0 = deck.xMin - UndersideMarginMetres, x1 = deck.xMax + UndersideMarginMetres;
                        float y0 = deck.yMin - UndersideBelowMetres, y1 = deck.yMax + UndersideMarginMetres;
                        if (x1 - x0 > UndersideMaxWidth) { float c = deck.center.x; x0 = c - UndersideMaxWidth * 0.5f; x1 = c + UndersideMaxWidth * 0.5f; }
                        if (y1 - y0 > UndersideMaxHeight) { y0 = deck.yMin - UndersideBelowMetres; y1 = y0 + UndersideMaxHeight; }
                        x0 = Mathf.Floor(x0 * BoardPixelsPerMetre) / BoardPixelsPerMetre; y0 = Mathf.Floor(y0 * BoardPixelsPerMetre) / BoardPixelsPerMetre;
                        x1 = Mathf.Ceil(x1 * BoardPixelsPerMetre) / BoardPixelsPerMetre; y1 = Mathf.Ceil(y1 * BoardPixelsPerMetre) / BoardPixelsPerMetre;
                        int w = Mathf.RoundToInt((x1 - x0) * BoardPixelsPerMetre), h = Mathf.RoundToInt((y1 - y0) * BoardPixelsPerMetre);
                        // The deck's south edge in plate pixels (from the top), for the crops.
                        int faceX = Mathf.RoundToInt((deck.center.x - x0) * BoardPixelsPerMetre);
                        int faceY = Mathf.RoundToInt((y1 - deck.yMin) * BoardPixelsPerMetre);

                        foreach (float tide in s.Tides)
                            foreach (int view in Views())
                            {
                                SetMirrorDebugView(view > 1 ? view - 1 : 0);
                                GameServices.Config = view == NoMirrorView ? noMirror : view == 0 ? off : config;
                                List<Renderer> hidden = HiddenRenderers(view);
                                foreach (Renderer r in hidden) r.enabled = false;
                                GameServices.Environment = new CalmPinnedTide(held, tide);
                                Time.timeScale = 1f;
                                float until = Time.realtimeSinceStartup + 20f;
                                do { yield return null; }
                                while (Time.realtimeSinceStartup < until &&
                                       (Shader.GetGlobalVector("_HHSeaLevelWorld").w < .5f ||
                                        Mathf.Abs(Shader.GetGlobalVector("_HHSeaLevelWorld").x - tide) > .01f));
                                float level = Shader.GetGlobalVector("_HHSeaLevelWorld").x;

                                _follow.enabled = false;
                                foreach (Behaviour ppc in pixelCameras) ppc.enabled = false;
                                if (_rt == null || _rt.width != w || _rt.height != h)
                                {
                                    if (_rt != null) { _cam.targetTexture = null; _rt.Release(); Object.DestroyImmediate(_rt); }
                                    _w = w; _h = h;
                                    _rt = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGBHalf);
                                    _rt.Create();
                                }
                                _cam.targetTexture = _rt;
                                _cam.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, _cam.transform.position.z);
                                _cam.orthographicSize = (y1 - y0) * 0.5f;
                                _cam.aspect = (x1 - x0) / (y1 - y0);

                                float run = Time.unscaledTime + ClearRunSeconds * 2f;
                                while (Time.unscaledTime < run) yield return null;
                                yield return ShadersReady();
                                Time.timeScale = 0f;
                                for (int i = 0; i < 2; i++) yield return null;

                                var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
                                tex.LoadRawTextureData(Capture());
                                tex.Apply();
                                string file = $"{s.Scene}.{s.Id}.tide{tide:+0.0;-0.0;0.0}.{ViewName(view)}.png";
                                SetMirrorDebugView(0);
                                foreach (Renderer r in hidden) if (r != null) r.enabled = true;
                                File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
                                Object.DestroyImmediate(tex);
                                numbers.Clear().AppendLine($"{file}\t{_w}x{_h}\tface {faceX},{faceY}\tframe ({x0:0.###},{y0:0.###})-({x1:0.###},{y1:0.###})\t" +
                                                   $"sea level read {level:0.000} (asked {tide:0.00})\tshare {GameServices.WaterMirror.UndersideShare:0.00}\t{how}");
                                File.AppendAllText(Path.Combine(dir, "numbers.txt"), numbers.ToString());
                                Time.timeScale = 1f;
                            }
                    }
                }
                finally
                {
                    GameServices.Config = config;
                    GameServices.Environment = held;
                    Object.DestroyImmediate(off);
                    Object.DestroyImmediate(noMirror);
                    _follow.enabled = followEnabled;
                    foreach (Behaviour ppc in pixelCameras) if (ppc != null) ppc.enabled = true;
                    _cam.aspect = aspect; _cam.orthographicSize = ortho; _cam.transform.position = camAt;
                    Time.timeScale = 1f;
                }
            }
            if (missing.Count > 0) File.WriteAllText(Path.Combine(dir, "missing.txt"), string.Join("\n", missing));
            Debug.Log($"[Undersides] PLATES_COMPLETE {dir}; missing: {string.Join(", ", missing)}");
            Assert.IsFalse(missing.Any(m => !m.Contains("~")), "a named deck was not found: " + string.Join(", ", missing));
        }
#endif
    }
}
