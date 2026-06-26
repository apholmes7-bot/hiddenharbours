#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tools.Editor
{
    /// <summary>
    /// REVERSIBLE demo harness for the WIND-and-FOOTSTEP grass (mirrors <c>BoatRotationTestBuilder</c>). ONE menu
    /// item drops a patch of grass tufts + a movable avatar into the current scene so the owner can press Play and
    /// IMMEDIATELY walk through the grass, watch it sway with the wind, and watch it bend under their footsteps —
    /// WITHOUT wrestling the St Peters scene builder (world-content integrates the grass into St Peters as a
    /// follow-up). It is ADDITIVE and surgical: it touches no committed scene/prefab/Data asset; delete the spawned
    /// "GrassTest" object to fully revert.
    ///
    /// What it spawns:
    ///   • a field of grass-tuft <see cref="SpriteRenderer"/>s, all sharing the ONE grass material (GPU-instanced /
    ///     dynamic-batched, all sway/bend in-shader — hundreds of tufts stay cheap, CLAUDE.md rule 7);
    ///   • a movable avatar with <see cref="GrassDevWalker"/> (WASD/arrows) + <see cref="GrassFootstep"/> (publishes
    ///     <c>_PlayerWorld</c> so the grass bends away from it);
    ///   • <see cref="GrassDevWind"/> on the root, which feeds a gentle veering TEST wind ONLY while there is no
    ///     environment sim — so the demo sways out of the box; once the real sim is present, the self-installing
    ///     <see cref="GrassWindBridge"/> drives the SAME global off the deterministic wind (grass + water together).
    ///
    /// Menu: <b>Hidden Harbours ▸ Build Grass Test</b>.
    /// </summary>
    public static class GrassTestBuilder
    {
        private const string MenuPath = "Hidden Harbours/Build Grass Test";
        private const string RootName = "GrassTest";
        // The greybox tuft variants (medium / short / tall) — scattered as a mix for a dense, painterly read.
        private static readonly string[] TuftPaths =
        {
            "Assets/_Project/Art/Sprites/GrassTuft.png",
            "Assets/_Project/Art/Sprites/GrassTuft_Short.png",
            "Assets/_Project/Art/Sprites/GrassTuft_Tall.png",
        };
        private const string MaterialPath = "Assets/_Project/Art/Materials/Grass.mat";
        private const string ShaderName = "HiddenHarbours/GrassWind";

        // Patch layout (greybox demo numbers — not gameplay tunables, so a const here is fine).
        private const int CountX = 26;
        private const int CountY = 18;
        private const float Spacing = 0.55f;     // metres between tufts
        private const float Jitter = 0.22f;      // metres of random placement scatter

        [MenuItem(MenuPath)]
        public static void Build()
        {
            // --- the grass material (prefer the committed asset; fall back to a fresh one off the shader). ---
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    EditorUtility.DisplayDialog(
                        "Build Grass Test",
                        $"Couldn't find the grass shader '{ShaderName}'.\n\n" +
                        "Open Unity so it imports Assets/_Project/Art/Shaders/HiddenHarboursGrass.shader, " +
                        "then run the menu item again.",
                        "OK");
                    return;
                }
                material = new Material(shader) { name = "Grass (runtime)" };
                material.enableInstancing = true;
            }

            // --- the tuft sprites (prefer the committed variants; fall back to one generated greybox tuft). ---
            var tufts = new System.Collections.Generic.List<Sprite>();
            foreach (string p in TuftPaths)
            {
                var s = LoadSpriteAny(p);
                if (s != null) tufts.Add(s);
            }
            if (tufts.Count == 0) tufts.Add(GenerateFallbackTuft());

            // Remove a prior rig so re-running is idempotent (no stacked patches).
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;
            root.AddComponent<GrassDevWind>();   // gentle test wind until a real sim takes over

            // --- ground backing so the tufts read against something (in-memory sprite; not saved). ---
            BuildGround(root.transform, new Color(0.27f, 0.36f, 0.22f, 1f));

            // --- the grass field. Deterministic placement (fixed seed) so the greybox looks the same each run. ---
            var rng = new System.Random(1234);
            var fieldParent = new GameObject("Field");
            fieldParent.transform.SetParent(root.transform, false);
            float originX = -(CountX - 1) * Spacing * 0.5f;
            float originY = -(CountY - 1) * Spacing * 0.5f;
            for (int gy = 0; gy < CountY; gy++)
            for (int gx = 0; gx < CountX; gx++)
            {
                float jx = (float)(rng.NextDouble() * 2.0 - 1.0) * Jitter;
                float jy = (float)(rng.NextDouble() * 2.0 - 1.0) * Jitter;
                var pos = new Vector3(originX + gx * Spacing + jx, originY + gy * Spacing + jy, 0f);

                var tuftGo = new GameObject("Tuft");
                tuftGo.transform.SetParent(fieldParent.transform, false);
                tuftGo.transform.position = pos;
                // slight scale variety so the patch isn't a uniform stamp (honest PPU: only the demo varies it).
                float s = 0.85f + (float)rng.NextDouble() * 0.4f;
                tuftGo.transform.localScale = new Vector3(s, s, 1f);

                var sr = tuftGo.AddComponent<SpriteRenderer>();
                sr.sprite = tufts[rng.Next(tufts.Count)];   // mix the height variants for density
                sr.sharedMaterial = material;
                // per-tuft tint jitter (value + a touch of hue) so the field reads painterly, not stamped.
                // The shader multiplies vertex colour, so this just shades each tuft; it stays in the palette.
                float v = 0.82f + (float)rng.NextDouble() * 0.30f;   // 0.82..1.12 brightness
                float warm = (float)(rng.NextDouble() * 0.10 - 0.05);
                sr.color = new Color(Mathf.Clamp01(v + warm), Mathf.Clamp01(v), Mathf.Clamp01(v - warm * 0.5f), 1f);
                // lower on screen draws in front (¾ top-down depth read).
                sr.sortingOrder = Mathf.RoundToInt(-pos.y * 100f);
            }

            // --- the movable avatar (WASD/arrows) carrying the footstep bender. ---
            var player = new GameObject("Player");
            player.transform.SetParent(root.transform, false);
            player.transform.position = Vector3.zero;
            var psr = player.AddComponent<SpriteRenderer>();
            psr.sprite = GenerateMarker(new Color(0.85f, 0.3f, 0.25f, 1f));
            psr.sortingOrder = 32000;   // always on top so the owner can see where they are
            player.AddComponent<GrassDevWalker>();
            player.AddComponent<GrassFootstep>();

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log(
                "[GrassTest] Spawned 'GrassTest' (" + (CountX * CountY) + " tufts). Press Play, then:\n" +
                "  - the grass sways on its own (a gentle test wind veers the lean) until a real sim is present;\n" +
                "  - walk the red marker with W/A/S/D or arrow keys and watch the grass bend away and spring back;\n" +
                "  - tune the look live on " + MaterialPath + " (sway amount/speed, footstep radius/strength, etc.).\n" +
                "Delete the 'GrassTest' object to fully revert. In St Peters the SAME wind drives grass + water.");
        }

        // ---- helpers -------------------------------------------------------------------------------------

        /// <summary>Load a Sprite whether the texture imported Single OR Multiple (mirrors the boat builder).</summary>
        private static Sprite LoadSpriteAny(string path)
        {
            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (direct != null) return direct;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (obj is Sprite s) return s;
            return null;
        }

        /// <summary>A simple in-memory greybox tuft if the committed PNG isn't imported yet. Not saved to disk.</summary>
        private static Sprite GenerateFallbackTuft()
        {
            const int W = 32, H = 32;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.hideFlags = HideFlags.DontSave;
            var clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                tex.SetPixel(x, y, clear);

            var mid = new Color(0.31f, 0.48f, 0.22f, 1f);
            var tip = new Color(0.59f, 0.74f, 0.36f, 1f);
            // a few leaning blades rooted at the bottom centre
            int[] baseX = { 16, 13, 19, 11, 22, 16, 9, 24 };
            int[] lean = { -6, -3, 4, -1, 6, 1, -4, 3 };
            int[] hgt = { 30, 26, 28, 21, 23, 24, 17, 18 };
            for (int b = 0; b < baseX.Length; b++)
            for (int yy = 0; yy < hgt[b]; yy++)
            {
                float t = yy / (float)Mathf.Max(hgt[b] - 1, 1);
                int xi = Mathf.RoundToInt(baseX[b] + lean[b] * t * t);
                if (xi >= 0 && xi < W) tex.SetPixel(xi, yy, Color.Lerp(mid, tip, t));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 32f);
        }

        /// <summary>A solid 1x1 marker sprite (the avatar / ground fill). In-memory; not saved.</summary>
        private static Sprite GenerateMarker(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.hideFlags = HideFlags.DontSave;
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private static void BuildGround(Transform parent, Color color)
        {
            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GenerateMarker(color);
            sr.sortingOrder = -32000;   // behind every tuft
            float w = (CountX + 2) * Spacing;
            float h = (CountY + 2) * Spacing;
            go.transform.localScale = new Vector3(w, h, 1f);
        }
    }
}
#endif
