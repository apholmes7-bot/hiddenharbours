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
    /// them (a nav mark through <see cref="NavBuoyVisual"/> from her shipped def; the painted pot float
    /// with the shipped config's radius), drawn by the real shader over a plain sea, at rest, in a
    /// trough and on a crest (with the bob), BEFORE (arc 0, the straight cut) and AFTER.
    ///
    /// <para><b>What it proves in pixels, not just numbers.</b> The arc can only ever take water OFF the
    /// near side: every pixel that differs between before and after must lie BELOW the float line, and
    /// the centre column must differ. A difference above the line would mean the arc moved something it
    /// has no business touching; no difference at the centre would mean it never reached the screen.</para>
    ///
    /// <para>Pictures land in <c>HH_WATERLINE_PLATES</c> (or the project's own <c>Temp/WaterlinePlates</c>,
    /// never the shared cache dir): 1 texel = 3 px (the on-foot framing on a 1080p screen) and 4x that.</para>
    /// </summary>
    public class BuoyWaterlinePlatePlayTests
    {
        private const int Ppu = 32;
        private const int W = 320, H = 192;            // the plate in TEXELS (1 texel = 1 px)
        private const int GameZoom = 3;                // 1080 / (11.25 m x 32) = the shipped on-foot framing
        private const int CloseZoom = GameZoom * 4;

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

        private Float PotFloat(float x, float y, float radius)
        {
            Sprite art = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Fishing/LobsterBuoys.png"))
                if (o is Sprite s && s.name == "LobsterBuoys_4") art = s;
            Assert.IsNotNull(art, "the painted pot float (LobsterBuoys_4) is missing");
            var root = Make("PotFloat");
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
            return new Float { Name = "PotFloat", Wave = wave, Renderer = sr, Radius = radius };
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

        [UnityTest]
        public IEnumerator TheWaterlineWrapsEveryFloatAtRestInATroughAndOnACrest()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(cfg);
            BuildStage();

            var floats = new List<Float>
            {
                NavMark("Assets/_Project/Data/NavBuoys/StbdNun.asset", 1.2f, 1.6f),
                NavMark("Assets/_Project/Data/NavBuoys/PortCan.asset", 3.4f, 1.6f),
                NavMark("Assets/_Project/Data/NavBuoys/Mooring.asset", 5.4f, 1.6f),
                NavMark("Assets/_Project/Data/NavBuoys/CardinalN.asset", 7.4f, 1.4f),
                PotFloat(9.2f, 1.6f, cfg.LobsterFloatRadiusMeters),
            };
            yield return null;   // Awake/LateUpdate settle; every shot below overrides the wave by hand

            foreach (var f in floats)
                Assert.Greater(f.Radius, 0f, $"{f.Name} never opted into the arc");

            string dir = Folder();
            var states = new (string name, float height)[] { ("rest", 0f), ("trough", -0.35f), ("crest", 0.35f) };
            const float amplitude = 2f;   // swamp threshold 1.4 m: none of these crests vanish a float

            foreach (var st in states)
            {
                yield return null;
                foreach (var f in floats) f.Wave.ConfigureWaterlineArc(0f);
                Color32[] before = Shoot(floats, st.height, amplitude);
                foreach (var f in floats) f.Wave.ConfigureWaterlineArc(f.Radius);
                Color32[] after = Shoot(floats, st.height, amplitude);
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
                    Assert.Greater(changedCentre, 0,
                        $"{f.Name} ({st.name}): the centre column did not change: the arc never reached the screen");
                }
            }
            Debug.Log($"[BuoyWaterlinePlate] plates in {dir}");
        }
    }
}
#endif
