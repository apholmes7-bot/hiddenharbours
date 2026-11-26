#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The waterline wraps the float, and the small floats ride the surface: the plates.</b> The real
    /// floats, wired the way the game wires them (a nav mark through <see cref="NavBuoyVisual"/> from her
    /// shipped def; the three pot floats off <c>PotFloats.png</c> with their shipped radii, ride reach and
    /// the owner's paint), drawn by the real shader over a plain sea, at rest, in a trough and on a crest
    /// (with the bob), BEFORE (arc 0, the straight cut) and AFTER.
    ///
    /// <para><b>What it proves in pixels, not just numbers.</b> The arc can only ever take water OFF the
    /// near side: every pixel that differs between before and after must lie BELOW the float line, and
    /// the centre column must differ. A difference above the line would mean the arc moved something it
    /// has no business touching; no difference at the centre would mean it never reached the screen.</para>
    ///
    /// <para><b>The ride.</b> A pot float lifts with most of the wave and only the rest climbs its side,
    /// held to its reach in data: a trough never lowers its line past its draft (so the rope eye never
    /// shows) and a crest never raises it past its freeboard (so the whole float is never swamped). The
    /// nav marks never opt in and keep the cork bob they had.</para>
    ///
    /// <para><b>More plates for the eye.</b> The scale line-up puts each float beside a nav mark, a
    /// standing fisher and the dory (the props at true size, 32 px a metre, the floats at the shipped
    /// <see cref="GameConfig.PotFloatDrawScale"/>, the owner's 2x); the colour plate paints every shape
    /// in every owner's colours from data; the size comparison draws the floats at 1x, 1.5x and 2x at the
    /// on-foot framing and at sea around the dory; the shipped plate is the dory's screen at the shipped size.</para>
    ///
    /// <para>Pictures land in <c>HH_WATERLINE_PLATES</c> (the size sheets in <c>HH_SIZE_PLATES</c>; both
    /// default to the project's own <c>Temp/</c>, never the shared cache dir).</para>
    /// </summary>
    public class BuoyWaterlinePlatePlayTests
    {
        private const int Ppu = 32;
        private const int W = 384, H = 192;            // the plate in TEXELS (1 texel = 1 px)
        private const int GameZoom = 3;                // 1080 / (11.25 m x 32) = the shipped on-foot framing
        private const int CloseZoom = GameZoom * 4;
        private const int BigBoatZoom = 2;             // the 17-19.5 m boats' framing (16.875 m tall)
        private const int ScreenW = 1920, ScreenH = 1080;
        private const string SheetPath = "Assets/_Project/Art/Fishing/PotFloats.png";
        private const string OldSheetPath = "Assets/_Project/Art/Fishing/LobsterBuoys.png";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const string FisherPath = "Assets/_Project/Art/Characters/Iso/Fisher_idle.png";
        private const string FisherSprite = "Fisher_idle_d0_f0";
        private const string DoryPath = "Assets/_Project/Art/Boats/DoryIso.png";
        private const string DorySprite = "DoryIso_0";
        private const string NunPath = "Assets/_Project/Data/NavBuoys/StbdNun.asset";

        private readonly List<Object> _made = new List<Object>();
        private Camera _cam;
        private RenderTexture _rt;
        private int _w = W, _h = H;

        private sealed class Float
        {
            public string Name;
            public BuoyWaveVisual Wave;
            public SpriteRenderer Renderer;
            public float Radius;
            public float Draft, Freeboard, Scale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            GameServices.Reset();
        }

        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            _made.Add(go);
            return go;
        }

        private static Sprite SpriteAt(string path, string name)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite s && s.name == name) return s;
            Assert.Fail($"{name} is missing from {path} (an LFS pointer?)");
            return null;
        }

        private Float NavMark(string defPath, float x, float y)
        {
            var def = AssetDatabase.LoadAssetAtPath<NavBuoyDef>(defPath);
            Assert.IsNotNull(def, defPath);
            var root = Make(def.name);
            root.transform.position = new Vector3(x, y, 0f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            var wave = root.AddComponent<BuoyWaveVisual>();
            wave.Configure(sr, visual.transform);
            root.AddComponent<NavBuoyMooring>();
            var mark = root.AddComponent<NavBuoyVisual>();
            mark.Configure(def, def.DefaultSize().SizeId, 0);
            Assert.IsNotNull(sr.sprite, $"{def.name} has no art in this box (an LFS pointer?)");
            return new Float { Name = def.name, Wave = wave, Renderer = sr, Radius = wave.WaterlineRadiusMeters };
        }

        /// <summary>A pot float off the sheet, wired as the presenters wire it: geometry off the art,
        /// the arc at the radius in data, the owner's paint by ramp swap, the ride reach from its def
        /// and the drawn size (the shipped <see cref="GameConfig.PotFloatDrawScale"/> unless one is given).</summary>
        private Float PotFloat(string shape, float x, float y, Color body, Color band, float scale = 0f)
        {
            if (scale <= 0f) scale = Config().PotFloatDrawScale;
            string defPath = DefFor(shape);
            float radius = Field(defPath, "BuoyFloatRadiusMeters");
            float draft = Field(defPath, "BuoyDraftMeters"), freeboard = Field(defPath, "BuoyFreeboardMeters");
            Sprite art = SpriteAt(SheetPath, "PotFloats_" + shape);
            var root = Make("PotFloat_" + shape);
            root.transform.position = new Vector3(x, y, 0f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = art;
            sr.sortingOrder = 10;
            var wave = root.AddComponent<BuoyWaveVisual>();
            wave.Configure(sr, visual.transform);
            wave.ConfigureFloatFromSprite(art);
            wave.ConfigureWaterlineArc(radius);
            wave.ConfigurePaint(body, band);
            wave.ConfigureRide(Config().PotFloatSurfaceFollow, draft, freeboard, Config().PotFloatFoamTexels);
            wave.ConfigureDrawScale(scale);
            return new Float
            {
                Name = "PotFloat_" + shape, Wave = wave, Renderer = sr, Radius = radius,
                Draft = draft, Freeboard = freeboard, Scale = scale,
            };
        }

        /// <summary>The OLD painted float (LobsterBuoys_4, tinted yellow as the game drew it), for the
        /// before/after comparison only; nothing in the game uses it now.</summary>
        private Float OldFloat(float x, float y, float radius)
        {
            Sprite art = SpriteAt(OldSheetPath, "LobsterBuoys_4");
            var root = Make("OldPotFloat");
            root.transform.position = new Vector3(x, y, 0f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = art;
            sr.color = new Color(1f, 0.85f, 0.2f, 1f);
            sr.sortingOrder = 10;
            var wave = root.AddComponent<BuoyWaveVisual>();
            wave.Configure(sr, visual.transform);
            wave.ConfigureWaterlineArc(radius);
            return new Float { Name = "OldPotFloat", Wave = wave, Renderer = sr, Radius = radius };
        }

        /// <summary>A still prop for scale (a fisher standing, the dory), drawn at its own PPU.</summary>
        private void Prop(string path, string sprite, float x, float y)
        {
            var go = Make(sprite);
            go.transform.position = new Vector3(x, y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteAt(path, sprite);
            sr.sortingOrder = 5;
        }

        private void BuildStage(int w = W, int h = H)
        {
            _w = w; _h = h;
            var camGo = Make("PlateCamera");
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = h / (2f * Ppu);
            _cam.transform.position = new Vector3(w / (2f * Ppu), h / (2f * Ppu), -10f);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.16f, 0.32f, 0.40f, 1f);   // a plain sea
            _cam.enabled = false;
            // No GPU (CI): no target, and Shoot skips; rendering into a failed target crashes the editor.
            if (!HasGpu) return;
            _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            _rt.Create();
            _cam.targetTexture = _rt;
        }

        private static bool HasGpu => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        private Color32[] Shoot(IList<Float> floats, float height, float amplitude)
        {
            if (!HasGpu) Assert.Ignore("SKIPPED: plate capture needs a GPU.");
            foreach (var f in floats) f.Wave.ApplyWaveHeight(height, amplitude);
            _cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var img = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            img.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
            img.Apply();
            RenderTexture.active = prev;
            Color32[] px = img.GetPixels32();
            Object.DestroyImmediate(img);
            return px;
        }

        private static string Folder(string envName = "HH_WATERLINE_PLATES", string fallback = "WaterlinePlates")
        {
            string env = System.Environment.GetEnvironmentVariable(envName);
            string dir = string.IsNullOrEmpty(env)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", fallback))
                : env;
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void Save(Color32[] px, string dir, string name) => Save(px, W, H, dir, name, GameZoom, CloseZoom);

        /// <summary>Writes the plate (w x h texels) nearest-neighbour upscaled at each zoom.</summary>
        private static void Save(Color32[] px, int w, int h, string dir, string name, params int[] zooms)
        {
            foreach (int z in zooms)
            {
                var big = new Texture2D(w * z, h * z, TextureFormat.RGBA32, false);
                var outPx = new Color32[w * z * h * z];
                for (int y = 0; y < h * z; y++)
                    for (int x = 0; x < w * z; x++)
                        outPx[y * w * z + x] = px[(y / z) * w + (x / z)];
                big.SetPixels32(outPx);
                big.Apply();
                File.WriteAllBytes(Path.Combine(dir, $"{name}_x{z}.png"), big.EncodeToPNG());
                Object.DestroyImmediate(big);
            }
        }

        /// <summary>What a 1920x1080 screen shows at a framing zoom, centred on (cx, cy) texels: the crop
        /// upscaled by the zoom, exactly the player's view.</summary>
        private static void SaveScreen(Color32[] px, int w, int h, int cx, int cy, int zoom, string dir, string name)
        {
            int cw = ScreenW / zoom, ch = ScreenH / zoom;
            int x0 = Mathf.Clamp(cx - cw / 2, 0, w - cw), y0 = Mathf.Clamp(cy - ch / 2, 0, h - ch);
            var crop = new Color32[cw * ch];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                    crop[y * cw + x] = px[(y0 + y) * w + (x0 + x)];
            Save(crop, cw, ch, dir, name, zoom);
        }

        private static void Outline(Color32[] px, int w, int h, int cx, int cy, int rw, int rh, Color32 c)
        {
            int x0 = cx - rw / 2, y0 = cy - rh / 2, x1 = x0 + rw - 1, y1 = y0 + rh - 1;
            for (int x = x0; x <= x1; x++)
            {
                if (x < 0 || x >= w) continue;
                if (y0 >= 0 && y0 < h) px[y0 * w + x] = c;
                if (y1 >= 0 && y1 < h) px[y1 * w + x] = c;
            }
            for (int y = y0; y <= y1; y++)
            {
                if (y < 0 || y >= h) continue;
                if (x0 >= 0 && x0 < w) px[y * w + x0] = c;
                if (x1 >= 0 && x1 < w) px[y * w + x1] = c;
            }
        }

        /// <summary>The float line's screen row and the body's centre column, in plate texels.</summary>
        private void LineAndCentre(Float f, out int row, out int col)
        {
            Sprite s = f.Renderer.sprite;
            float scale = f.Renderer.transform.lossyScale.y;
            float lineWorldY = f.Renderer.transform.position.y
                             + (f.Wave.WaterlineFrac * s.rect.height - s.pivot.y) * scale / s.pixelsPerUnit;
            Vector3 p = _cam.WorldToScreenPoint(new Vector3(f.Renderer.transform.position.x, lineWorldY, 0f));
            row = Mathf.FloorToInt(p.y);
            col = Mathf.FloorToInt(p.x);
        }

        /// <summary>The lowest plate row (bottom-up) holding anything but sea in the float's centre
        /// columns; H when there is none.</summary>
        private static int LowestInk(Color32[] px, int col)
        {
            Color32 sea = px[0];
            for (int y = 0; y < H; y++)
                for (int x = Mathf.Max(0, col - 1); x <= Mathf.Min(W - 1, col + 1); x++)
                    if (!px[y * W + x].Equals(sea)) return y;
            return H;
        }

        private static readonly (string name, float height)[] States =
            { ("rest", 0f), ("trough", -0.35f), ("crest", 0.35f) };
        private const float Amplitude = 2f;   // swamp threshold 1.4 m: none of these crests vanish a float

        private static readonly float[] Sizes = { 1f, 1.5f, 2f };
        private static string SizeName(float s) => s == 1f ? "1x" : s == 1.5f ? "1_5x" : "2x";

        private static GameConfig Config()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(cfg);
            return cfg;
        }

        private static float Field(string path, string field)
        {
            var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
            SerializedProperty p = so.FindProperty(field);
            Assert.IsNotNull(p, $"{path}: {field}");
            return p.floatValue;
        }

        private const string LobsterPath = "Assets/_Project/Data/Traps/LobsterTrap.asset";
        private const string CrabPath = "Assets/_Project/Data/Traps/CrabTrap.asset";
        private const string FleetPath = "Assets/_Project/Data/Boats/StPetersAmbientFleet.asset";

        private static string DefFor(string shape) =>
            shape == "bullet" ? LobsterPath : shape == "ball" ? CrabPath : FleetPath;

        /// <summary>The ride's promise, in the float's own sprite pixels: the line moved by the wave but
        /// stayed inside the reach in data (never below the draft, so the eye stays under; never above
        /// the freeboard, so the float is never swamped whole).</summary>
        private static void AssertRideHeld(Float f, string state, float rest)
        {
            Assert.IsTrue(f.Wave.RidesTheSurface, $"{f.Name}: never opted into the ride");
            Sprite s = f.Renderer.sprite;
            float line = f.Wave.WaterlineFrac * s.rect.height, pivot = s.pivot.y;
            float lo = pivot - f.Draft * Ppu - 0.51f, hi = pivot + f.Freeboard * Ppu + 0.51f;
            Assert.That(line, Is.InRange(lo, hi), $"{f.Name} ({state}, {f.Scale}x): line {line:F2} px outside its reach");
            if (state == "crest") Assert.Greater(f.Wave.WaterlineFrac, rest, $"{f.Name}: a crest did not climb");
            if (state == "trough") Assert.Less(f.Wave.WaterlineFrac, rest, $"{f.Name}: a trough did not lower the line");
        }

        [UnityTest]
        public IEnumerator TheWaterlineWrapsEveryFloatAtRestInATroughAndOnACrest()
        {
            GameConfig cfg = Config();
            BuildStage();
            Color gold = cfg.PlayerPotFloatColour, rust = cfg.PlayerPotFloatBandColour;

            var floats = new List<Float>
            {
                NavMark("Assets/_Project/Data/NavBuoys/StbdNun.asset", 1.2f, 1.6f),
                NavMark("Assets/_Project/Data/NavBuoys/PortCan.asset", 3.4f, 1.6f),
                NavMark("Assets/_Project/Data/NavBuoys/Mooring.asset", 5.4f, 1.6f),
                NavMark("Assets/_Project/Data/NavBuoys/CardinalN.asset", 7.4f, 1.4f),
                PotFloat("bullet", 8.9f, 1.6f, gold, rust),
                PotFloat("ball", 10.0f, 1.6f, gold, rust),
                PotFloat("spar", 11.1f, 1.6f, gold, rust),
            };
            yield return null;   // Awake/LateUpdate settle; every shot below overrides the wave by hand

            foreach (var f in floats)
                Assert.Greater(f.Radius, 0f, $"{f.Name} never opted into the arc");
            for (int i = 0; i < 4; i++)
            {
                Assert.IsFalse(floats[i].Wave.RidesTheSurface, $"{floats[i].Name}: a nav mark must keep its own bob");
                Assert.AreEqual(1f, floats[i].Renderer.transform.lossyScale.y, 1e-6f, $"{floats[i].Name}: a nav mark is never scaled");
                Assert.AreEqual(1f, floats[i].Wave.DrawScale, 1e-6f, $"{floats[i].Name}: a nav mark keeps true size");
            }
            for (int i = 4; i < floats.Count; i++)
                Assert.AreEqual(cfg.PotFloatDrawScale, floats[i].Renderer.transform.lossyScale.y, 1e-6f,
                                $"{floats[i].Name}: drawn at the shipped size");

            var rest = new Dictionary<Float, float>();
            foreach (var f in floats) { f.Wave.ApplyWaveHeight(0f, Amplitude); rest[f] = f.Wave.WaterlineFrac; }
            // The pale band at the line: a pot float's is PotFloatFoamTexels of its own texels (a wide band bled
            // down the thin stick as a pale tick in a trough); a nav mark keeps the buoy default.
            for (int i = 4; i < floats.Count; i++)
                Assert.AreEqual(cfg.PotFloatFoamTexels / floats[i].Renderer.sprite.rect.height,
                                floats[i].Wave.WaterlineFoamWidth, 1e-6f, $"{floats[i].Name}: band in its own texels");
            for (int i = 0; i < 4; i++)
                Assert.Greater(floats[i].Wave.WaterlineFoamWidth, floats[4].Wave.WaterlineFoamWidth,
                               $"{floats[i].Name}: a nav mark keeps the buoy's own band");

            string dir = Folder();
            foreach (var st in States)
            {
                yield return null;
                foreach (var f in floats) f.Wave.ConfigureWaterlineArc(0f);
                Color32[] before = Shoot(floats, st.height, Amplitude);
                foreach (var f in floats) f.Wave.ConfigureWaterlineArc(f.Radius);
                Color32[] after = Shoot(floats, st.height, Amplitude);
                Save(before, dir, $"waterline_{st.name}_before");
                Save(after, dir, $"waterline_{st.name}_after");

                foreach (var f in floats)
                {
                    if (f.Wave.RidesTheSurface) AssertRideHeld(f, st.name, rest[f]);
                    Assert.AreNotEqual(Vector4.zero, f.Wave.WaterlineArc, $"{f.Name}: no arc pushed");
                    LineAndCentre(f, out int row, out int col);
                    Sprite s = f.Renderer.sprite;
                    int halfW = Mathf.CeilToInt(s.rect.width * 0.5f * f.Renderer.transform.lossyScale.x) + 1;
                    int changedCentre = 0, changedAbove = 0;
                    for (int x = Mathf.Max(0, col - halfW); x < Mathf.Min(W, col + halfW); x++)
                    {
                        for (int y = 0; y < H; y++)
                        {
                            int i = y * W + x;
                            if (before[i].Equals(after[i])) continue;
                            if (y > row + 1) changedAbove++;
                            if (Mathf.Abs(x - col) <= 1 && y <= row) changedCentre++;
                        }
                    }
                    Assert.AreEqual(0, changedAbove,
                        $"{f.Name} ({st.name}): the arc changed {changedAbove} px ABOVE the float line");

                    // A float small against the wave can ride a trough with its line BELOW its lowest
                    // painted pixel (nothing under water, so nothing for the arc to reveal): there the
                    // honest check is that nothing changed at all.
                    if (row < LowestInk(before, col))
                    {
                        Assert.AreEqual(0, changedCentre,
                            $"{f.Name} ({st.name}): the line is under the float, yet the arc changed its centre");
                        continue;
                    }
                    Assert.Greater(changedCentre, 0,
                        $"{f.Name} ({st.name}): the centre column did not change: the arc never reached the screen");
                }
            }
            Debug.Log($"[BuoyWaterlinePlate] plates in {dir}");
        }

        [UnityTest]
        public IEnumerator TheScaleLineUpPutsEachFloatBesideAMarkAFisherAndTheDory()
        {
            GameConfig cfg = Config();
            BuildStage();
            Color gold = cfg.PlayerPotFloatColour, rust = cfg.PlayerPotFloatBandColour;

            Prop(FisherPath, FisherSprite, 2.9f, 2.4f);
            Prop(DoryPath, DorySprite, 5.2f, 2.2f);
            var floats = new List<Float>
            {
                NavMark(NunPath, 1.1f, 1.8f),
                OldFloat(7.6f, 1.8f, cfg.LobsterFloatRadiusMeters),
                PotFloat("bullet", 8.6f, 1.8f, gold, rust),
                PotFloat("ball", 9.8f, 1.8f, gold, rust),
                PotFloat("spar", 11.0f, 1.8f, gold, rust),
            };
            yield return null;

            string dir = Folder();
            foreach (var st in States)
            {
                yield return null;
                Color32[] shot = Shoot(floats, st.height, Amplitude);
                Save(shot, dir, $"lineup_{st.name}");
            }

            // true scale: the bullet float's painted body is about 0.4 m long, so it can never stand
            // taller than the fisher (a 1.8 m man is ~58 px)
            Sprite bullet = floats[2].Renderer.sprite;
            Assert.AreEqual(Ppu, bullet.pixelsPerUnit, 0.01f, "true scale: 32 px a metre");
            Assert.AreEqual(2f, cfg.PotFloatDrawScale, 1e-6f, "the owner's pick (2026-11-25): pot floats are drawn at 2x");
            Debug.Log($"[BuoyWaterlinePlate] line-up in {dir}");
        }

        [UnityTest]
        public IEnumerator EveryShapeTakesEveryOwnersColoursFromData()
        {
            GameConfig cfg = Config();
            BuildStage();

            var owners = new List<(string name, Color body, Color band)>
            {
                ("player", cfg.PlayerPotFloatColour, cfg.PlayerPotFloatBandColour),
            };
            foreach (string guid in AssetDatabase.FindAssets("PotDay t:ScriptableObject", new[] { "Assets/_Project/Data/Traps" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
                SerializedProperty c = so.FindProperty("BuoyColour");
                if (c != null) owners.Add((Path.GetFileNameWithoutExtension(path), c.colorValue, cfg.PotFloatBandColour));
            }
            var fleet = AssetDatabase.LoadAssetAtPath<AmbientFleetDef>(FleetPath);
            Assert.IsNotNull(fleet);
            for (int i = 0; i < fleet.BuoyPalette.Length; i++)
                owners.Add(($"fleet{i}", fleet.BuoyPalette[i], cfg.PotFloatBandColour));
            Assert.Greater(owners.Count, 3, "the player, the pot days and the fleet");

            string[] shapes = { "bullet", "ball", "spar" };
            float[] rowY = { 0.8f, 2.5f, 4.2f };
            float step = Mathf.Min(0.75f, (W / (float)Ppu - 0.8f) / owners.Count);
            var floats = new List<Float>();
            for (int s = 0; s < shapes.Length; s++)
                for (int o = 0; o < owners.Count; o++)
                {
                    // true size here: a paint check, packed tight (the shipped size is on the other plates)
                    Float f = PotFloat(shapes[s], 0.6f + o * step, rowY[s], owners[o].body, owners[o].band, 1f);
                    bool bodyBaked = (Color32)owners[o].body is var b && b.r == 0xe0 && b.g == 0xb1 && b.b == 0x3a;
                    bool bandBaked = (Color32)owners[o].band is var n && n.r == 0x9c && n.g == 0x4a && n.b == 0x3c;
                    int want = (bodyBaked ? 0 : 5) + (bandBaked ? 0 : 5);
                    Assert.AreEqual(want, f.Wave.PaintSwapCount, $"{owners[o].name} {shapes[s]}: swaps");
                    floats.Add(f);
                }
            yield return null;

            string dir = Folder();
            Color32[] shot = Shoot(floats, 0f, Amplitude);
            Save(shot, dir, "colours_rest");
            Debug.Log($"[BuoyWaterlinePlate] colours ({string.Join(", ", owners.ConvertAll(x => x.name))}) in {dir}");
        }

        /// <summary>
        /// <b>The float, its arc and its ride scale together.</b> One float at true size and one at 2x on
        /// the same wave: the arc (in the sprite's own UV) is the same vector, so it wraps the drawn body
        /// at the drawn width; the bob (world metres of water) is the same; and the line moves half as far
        /// up the drawn-up sprite, which is the same water against a body twice the size.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFloatItsArcAndItsRideScaleTogether()
        {
            GameConfig cfg = Config();
            BuildStage();
            Float one = PotFloat("bullet", 3f, 2f, cfg.PlayerPotFloatColour, cfg.PlayerPotFloatBandColour, 1f);
            Float two = PotFloat("bullet", 8f, 2f, cfg.PlayerPotFloatColour, cfg.PlayerPotFloatBandColour, 2f);
            yield return null;

            one.Wave.ApplyWaveHeight(0f, Amplitude); two.Wave.ApplyWaveHeight(0f, Amplitude);
            float rest1 = one.Wave.WaterlineFrac, rest2 = two.Wave.WaterlineFrac;
            Assert.AreEqual(rest1, rest2, 1e-6f, "the same float line at any size");

            const float h = 0.2f;   // small enough that neither clamps
            one.Wave.ApplyWaveHeight(h, Amplitude); two.Wave.ApplyWaveHeight(h, Amplitude);
            Assert.AreEqual(one.Wave.WaterlineArc, two.Wave.WaterlineArc, "the arc is the sprite's own, so it scales with it");
            Assert.AreEqual((one.Wave.WaterlineFrac - rest1) * 0.5f, two.Wave.WaterlineFrac - rest2, 1e-5f,
                            "the same water climbs half as far up a body drawn twice the size");
            float bob1 = one.Renderer.transform.position.y - one.Wave.transform.position.y;
            float bob2 = two.Renderer.transform.position.y - two.Wave.transform.position.y;
            Assert.AreEqual(bob1, bob2, 1e-5f, "the lift is metres of water, the same at any size");
            Assert.AreEqual(2f, two.Renderer.transform.lossyScale.x, 1e-6f);
            Assert.AreEqual(2f, two.Wave.DrawScale, 1e-6f);
        }

        /// <summary>
        /// <b>The shipped size from the dory.</b> What a 1920x1080 screen shows in the dory's framing (x3,
        /// 20 m x 11.25 m) with the floats at the shipped <see cref="GameConfig.PotFloatDrawScale"/>: a
        /// nav mark, the fisher in the dory, floats 5 m and about 9.7 m off, at rest, in a trough, on a
        /// crest and in a deep trough that holds the line at its draft. Into <c>HH_SHIPPED_PLATES</c>.
        /// </summary>
        [UnityTest]
        public IEnumerator TheShippedSizeFromTheDory()
        {
            GameConfig cfg = Config();
            Color gold = cfg.PlayerPotFloatColour, rust = cfg.PlayerPotFloatBandColour;
            string dir = Folder("HH_SHIPPED_PLATES", "ShippedPlates");
            const int w = ScreenW / GameZoom, h = ScreenH / GameZoom;   // 640 x 360 texels
            BuildStage(w, h);
            float cx = w / (2f * Ppu), cy = h / (2f * Ppu);
            Prop(DoryPath, DorySprite, cx, cy);
            Prop(FisherPath, FisherSprite, cx, cy + 0.2f);
            var floats = new List<Float> { NavMark(NunPath, cx - 6f, cy + 2f) };
            string[] shapes = { "bullet", "ball", "spar" };
            float scale = cfg.PotFloatDrawScale;
            for (int s = 0; s < shapes.Length; s++)
            {
                floats.Add(PotFloat(shapes[s], cx + 3.2f + s * 0.7f * scale, cy - 1.5f, gold, rust));
                floats.Add(PotFloat(shapes[s], cx + 8.6f - (2 - s) * 0.7f * scale, cy - 4.4f, gold, rust));
            }
            yield return null;

            var rest = new Dictionary<Float, float>();
            foreach (var f in floats) { f.Wave.ApplyWaveHeight(0f, Amplitude); rest[f] = f.Wave.WaterlineFrac; }
            var states = new List<(string name, float height)>(States) { ("deeptrough", -1.0f) };
            foreach (var st in states)
            {
                yield return null;
                Color32[] shot = Shoot(floats, st.height, Amplitude);
                Save(shot, w, h, dir, $"shipped_{SizeName(scale)}_dory_{st.name}", GameZoom);
                foreach (var f in floats)
                    if (f.Wave.RidesTheSurface)
                        AssertRideHeld(f, st.name == "deeptrough" ? "trough" : st.name, rest[f]);
            }
            Debug.Log($"[BuoyWaterlinePlate] shipped-size plates in {dir}");
        }

        /// <summary>
        /// <b>The size question, as put to the owner (he picked 2x, 2026-11-25).</b> The same line-up (a
        /// nav mark, a standing fisher, the dory, the three floats) with the floats drawn at 1x, 1.5x and
        /// 2x through the one data value (<see cref="GameConfig.PotFloatDrawScale"/>), at rest, in a
        /// trough and on a crest:
        /// <list type="bullet">
        /// <item>on foot: one row per size, at the on-foot framing (1 texel = 3 px);</item>
        /// <item>at sea: the dory alone on a 54 m strip with floats 10 m and 25 m off, cut to what a
        /// 1920x1080 screen shows at the dory's framing (x3, 20 m x 11.25 m) and the big boats' (x2,
        /// 30 m x 16.875 m), plus the whole strip with both screens outlined (a float 25 m off is off
        /// both screens: the camera never shows that far).</item>
        /// </list>
        /// </summary>
        [UnityTest]
        public IEnumerator TheSizeComparisonDrawsTheFloatsAtOneOneAndAHalfAndTwoTimes()
        {
            GameConfig cfg = Config();
            Color gold = cfg.PlayerPotFloatColour, rust = cfg.PlayerPotFloatBandColour;
            string dir = Folder("HH_SIZE_PLATES", "SizePlates");
            string[] shapes = { "bullet", "ball", "spar" };

            // ---- on foot: one row per size ---------------------------------------------------------
            const int footW = 416, footH = 352;     // 13 m x 11 m: 1x at the bottom, 2x at the top
            float[] rowY = { 0.8f, 3.7f, 7.1f };    // the spar's pole grows with the size, so the rows do too
            BuildStage(footW, footH);
            var foot = new List<Float>();
            for (int r = 0; r < Sizes.Length; r++)
            {
                float y = rowY[r];
                foot.Add(NavMark(NunPath, 1.1f, y));
                Prop(FisherPath, FisherSprite, 2.9f, y + 0.6f);
                Prop(DoryPath, DorySprite, 5.2f, y + 0.4f);
                for (int s = 0; s < shapes.Length; s++)
                    foot.Add(PotFloat(shapes[s], 7.6f + s * 1.1f * Sizes[r], y, gold, rust, Sizes[r]));
            }
            yield return null;
            foreach (var st in States)
            {
                yield return null;
                Color32[] shot = Shoot(foot, st.height, Amplitude);
                Save(shot, footW, footH, dir, $"size_onfoot_{st.name}_bottom1x-mid1_5x-top2x", GameZoom);
                foreach (var f in foot) if (f.Wave.RidesTheSurface) Assert.AreEqual(f.Scale, f.Wave.DrawScale, 1e-6f);
            }

            // ---- at sea: around the dory, floats 10 m and 25 m off --------------------------------
            foreach (float size in Sizes)
            {
                foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
                _made.Clear();
                if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }

                const int seaW = 1728, seaH = 540;  // 54 m x 16.875 m
                BuildStage(seaW, seaH);
                float cx = seaW / (2f * Ppu), cy = seaH / (2f * Ppu);
                Prop(DoryPath, DorySprite, cx, cy);
                Prop(FisherPath, FisherSprite, cx, cy + 0.2f);
                var sea = new List<Float> { NavMark(NunPath, cx - 6f, cy + 2f) };
                foreach (float off in new[] { 10f, 25f })
                    for (int s = 0; s < shapes.Length; s++)
                    {
                        sea.Add(PotFloat(shapes[s], cx + off + (s - 1) * 0.7f * size, cy - 1f, gold, rust, size));
                        sea.Add(PotFloat(shapes[s], cx - off + (s - 1) * 0.7f * size, cy + 1f, gold, rust, size));
                    }
                // and one group about 10 m off on the diagonal (8.6 m across, 4.4 m down: 9.7 m), the
                // whole group inside the dory's screen at every size
                for (int s = 0; s < shapes.Length; s++)
                    sea.Add(PotFloat(shapes[s], cx + 8.6f - (2 - s) * 0.7f * size, cy - 4.4f, gold, rust, size));
                yield return null;

                foreach (var st in States)
                {
                    yield return null;
                    Color32[] shot = Shoot(sea, st.height, Amplitude);
                    int pcx = seaW / 2, pcy = seaH / 2;
                    string n = $"size_atsea_{SizeName(size)}_{st.name}";
                    SaveScreen(shot, seaW, seaH, pcx, pcy, GameZoom, dir, n + "_dory-screen");
                    SaveScreen(shot, seaW, seaH, pcx, pcy, BigBoatZoom, dir, n + "_bigboat-screen");
                    Outline(shot, seaW, seaH, pcx, pcy, ScreenW / GameZoom, ScreenH / GameZoom, new Color32(255, 255, 255, 255));
                    Outline(shot, seaW, seaH, pcx, pcy, ScreenW / BigBoatZoom, ScreenH / BigBoatZoom, new Color32(255, 200, 0, 255));
                    Save(shot, seaW, seaH, dir, n + "_wide", 1, 2);
                }
            }
            Debug.Log($"[BuoyWaterlinePlate] size sheets in {dir}");
        }
    }
}
#endif
