#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The waterline wraps the float: the plate.</b> The real floats, wired the way the game wires
    /// them (a nav mark through <see cref="NavBuoyVisual"/> from her shipped def; the three pot floats off
    /// <c>PotFloats.png</c> with their shipped radii and the owner's paint), drawn by the real shader over
    /// a plain sea, at rest, in a trough and on a crest (with the bob), BEFORE (arc 0, the straight cut)
    /// and AFTER.
    ///
    /// <para><b>What it proves in pixels, not just numbers.</b> The arc can only ever take water OFF the
    /// near side: every pixel that differs between before and after must lie BELOW the float line, and
    /// the centre column must differ. A difference above the line would mean the arc moved something it
    /// has no business touching; no difference at the centre would mean it never reached the screen.</para>
    ///
    /// <para><b>Two more plates for the eye.</b> The scale line-up puts each float beside a nav mark, a
    /// standing fisher and the dory (all at true size, 32 px a metre), with the old painted float for
    /// comparison; the colour plate paints every shape in every owner's colours from data.</para>
    ///
    /// <para>Pictures land in <c>HH_WATERLINE_PLATES</c> (or the project's own <c>Temp/WaterlinePlates</c>,
    /// never the shared cache dir): 1 texel = 3 px (the on-foot framing on a 1080p screen) and 4x that.</para>
    /// </summary>
    public class BuoyWaterlinePlatePlayTests
    {
        private const int Ppu = 32;
        private const int W = 384, H = 192;            // the plate in TEXELS (1 texel = 1 px)
        private const int GameZoom = 3;                // 1080 / (11.25 m x 32) = the shipped on-foot framing
        private const int CloseZoom = GameZoom * 4;
        private const string SheetPath = "Assets/_Project/Art/Fishing/PotFloats.png";
        private const string OldSheetPath = "Assets/_Project/Art/Fishing/LobsterBuoys.png";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        private readonly List<Object> _made = new List<Object>();
        private Camera _cam;
        private RenderTexture _rt;

        private sealed class Float
        {
            public string Name;
            public BuoyWaveVisual Wave;
            public SpriteRenderer Renderer;
            public float Radius;
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
        /// the arc at the radius in data, the owner's paint by ramp swap.</summary>
        private Float PotFloat(string shape, float x, float y, float radius, Color body, Color band)
        {
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
            return new Float { Name = "PotFloat_" + shape, Wave = wave, Renderer = sr, Radius = radius };
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

        private void BuildStage()
        {
            var camGo = Make("PlateCamera");
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = H / (2f * Ppu);
            _cam.transform.position = new Vector3(W / (2f * Ppu), H / (2f * Ppu), -10f);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.16f, 0.32f, 0.40f, 1f);   // a plain sea
            _cam.enabled = false;
            _rt = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            _rt.Create();
            _cam.targetTexture = _rt;
        }

        private Color32[] Shoot(IList<Float> floats, float height, float amplitude)
        {
            foreach (var f in floats) f.Wave.ApplyWaveHeight(height, amplitude);
            _cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var img = new Texture2D(W, H, TextureFormat.RGBA32, false);
            img.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            img.Apply();
            RenderTexture.active = prev;
            Color32[] px = img.GetPixels32();
            Object.DestroyImmediate(img);
            return px;
        }

        private static string Folder()
        {
            string env = System.Environment.GetEnvironmentVariable("HH_WATERLINE_PLATES");
            string dir = string.IsNullOrEmpty(env)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "WaterlinePlates"))
                : env;
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void Save(Color32[] px, string dir, string name)
        {
            foreach (int z in new[] { GameZoom, CloseZoom })
            {
                var big = new Texture2D(W * z, H * z, TextureFormat.RGBA32, false);
                var outPx = new Color32[W * z * H * z];
                for (int y = 0; y < H * z; y++)
                    for (int x = 0; x < W * z; x++)
                        outPx[y * W * z + x] = px[(y / z) * W + (x / z)];
                big.SetPixels32(outPx);
                big.Apply();
                File.WriteAllBytes(Path.Combine(dir, $"{name}_x{z}.png"), big.EncodeToPNG());
                Object.DestroyImmediate(big);
            }
        }

        /// <summary>The float line's screen row and the body's centre column, in plate texels.</summary>
        private void LineAndCentre(Float f, out int row, out int col)
        {
            Sprite s = f.Renderer.sprite;
            float lineWorldY = f.Renderer.transform.position.y
                             + (f.Wave.WaterlineFrac * s.rect.height - s.pivot.y) / s.pixelsPerUnit;
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

        private static GameConfig Config()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(cfg);
            return cfg;
        }

        private static float Radius(string trapOrFleetPath)
        {
            var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(trapOrFleetPath));
            SerializedProperty p = so.FindProperty("BuoyFloatRadiusMeters");
            Assert.IsNotNull(p, trapOrFleetPath);
            return p.floatValue;
        }

        private const string LobsterPath = "Assets/_Project/Data/Traps/LobsterTrap.asset";
        private const string CrabPath = "Assets/_Project/Data/Traps/CrabTrap.asset";
        private const string FleetPath = "Assets/_Project/Data/Boats/StPetersAmbientFleet.asset";

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
                PotFloat("bullet", 9.0f, 1.6f, cfg.LobsterFloatRadiusMeters, gold, rust),
                PotFloat("ball", 9.8f, 1.6f, Radius(CrabPath), gold, rust),
                PotFloat("spar", 10.6f, 1.6f, Radius(FleetPath), gold, rust),
            };
            yield return null;   // Awake/LateUpdate settle; every shot below overrides the wave by hand

            foreach (var f in floats)
                Assert.Greater(f.Radius, 0f, $"{f.Name} never opted into the arc");

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
                    Assert.AreNotEqual(Vector4.zero, f.Wave.WaterlineArc, $"{f.Name}: no arc pushed");
                    LineAndCentre(f, out int row, out int col);
                    Sprite s = f.Renderer.sprite;
                    int halfW = Mathf.CeilToInt(s.rect.width * 0.5f) + 1;
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

            Prop("Assets/_Project/Art/Characters/Iso/Fisher_idle.png", "Fisher_idle_d0_f0", 2.9f, 2.4f);
            Prop("Assets/_Project/Art/Boats/DoryIso.png", "DoryIso_0", 5.2f, 2.2f);
            var floats = new List<Float>
            {
                NavMark("Assets/_Project/Data/NavBuoys/StbdNun.asset", 1.1f, 1.8f),
                OldFloat(7.6f, 1.8f, cfg.LobsterFloatRadiusMeters),
                PotFloat("bullet", 8.7f, 1.8f, Radius(LobsterPath), gold, rust),
                PotFloat("ball", 9.6f, 1.8f, Radius(CrabPath), gold, rust),
                PotFloat("spar", 10.6f, 1.8f, Radius(FleetPath), gold, rust),
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
            float[] radius = { Radius(LobsterPath), Radius(CrabPath), Radius(FleetPath) };
            float step = Mathf.Min(0.75f, (W / (float)Ppu - 0.8f) / owners.Count);
            var floats = new List<Float>();
            for (int s = 0; s < shapes.Length; s++)
                for (int o = 0; o < owners.Count; o++)
                {
                    Float f = PotFloat(shapes[s], 0.6f + o * step, rowY[s], radius[s], owners[o].body, owners[o].band);
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
    }
}
#endif
