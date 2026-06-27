#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Owner-facing tooling for the PROJECTED SPRITE SHADOWS (PR 2, ADR 0013). Two ways for Alex to SEE the
    /// "read the time from your shadow" feature with no hand-wiring:
    ///
    /// <list type="bullet">
    /// <item><description><b>Hidden Harbours ▸ Lighting ▸ Add Sprite Shadow to Selection</b> — batch-adds the
    /// <see cref="SpriteShadow"/> component to every selected object that has a <see cref="SpriteRenderer"/>
    /// (the player, a boat, trees, buildings). Idempotent (skips ones that already have it).</description></item>
    /// <item><description><b>Hidden Harbours ▸ Build Shadow Test</b> — a REVERSIBLE demo (mirrors "Build Grass
    /// Test"): drops a ground plane + a few casters (a post, a tree, a standing figure) into the current
    /// scene, each already carrying <see cref="SpriteShadow"/>. Press Play, scrub the clock, and watch the
    /// shadows SWING (west → north → east) and LENGTHEN (long at dawn/dusk, short at noon). Delete the spawned
    /// "ShadowTest" object to fully revert.</description></item>
    /// </list>
    ///
    /// It touches no committed scene / prefab / Data asset and writes nothing to disk — surgical and additive,
    /// exactly like the grass demo. Real-scene casters get the component via the menu above or by world-content
    /// later (this tool never edits the scene builders).
    /// </summary>
    public static class SpriteShadowMenu
    {
        private const string AddMenuPath   = "Hidden Harbours/Lighting/Add Sprite Shadow to Selection";
        private const string BuildMenuPath = "Hidden Harbours/Build Shadow Test";
        private const string RootName      = "ShadowTest";

        // ---- batch-add the component to selected SpriteRenderers -----------------------------------------

        [MenuItem(AddMenuPath, priority = 30)]
        public static void AddToSelection()
        {
            var targets = new List<GameObject>();
            foreach (var go in Selection.gameObjects)
            {
                if (go == null) continue;
                if (go.GetComponent<SpriteRenderer>() == null) continue;
                if (go.GetComponent<SpriteShadow>() != null) continue;   // idempotent
                targets.Add(go);
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Add Sprite Shadow",
                    "Select one or more objects that have a SpriteRenderer (and don't already have a Sprite " +
                    "Shadow). Then run this again.",
                    "OK");
                return;
            }

            foreach (var go in targets)
            {
                Undo.AddComponent<SpriteShadow>(go);
                EditorUtility.SetDirty(go);
            }

            Debug.Log($"[SpriteShadow] Added a projected shadow to {targets.Count} caster(s). Press Play and " +
                      "scrub the clock — the shadows swing + lengthen with the sun. Tune darkness/length on " +
                      "each component (or share Resources/SpriteShadow.mat for the look).");
        }

        // Only enable the menu when at least one selected object has a SpriteRenderer.
        [MenuItem(AddMenuPath, validate = true)]
        public static bool ValidateAddToSelection()
        {
            foreach (var go in Selection.gameObjects)
                if (go != null && go.GetComponent<SpriteRenderer>() != null) return true;
            return false;
        }

        // ---- the "Build Shadow Test" demo ---------------------------------------------------------------

        [MenuItem(BuildMenuPath, priority = 31)]
        public static void BuildShadowTest()
        {
            // Remove a prior rig so re-running is idempotent.
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;

            // A ground plane the shadows fall on (mid grass-green; in-memory sprite, not saved).
            BuildGround(root.transform, new Color(0.30f, 0.40f, 0.26f, 1f), 18f, 12f);

            // A few casters, spaced out, each carrying SpriteShadow so the demo needs zero hand-wiring.
            //   - a tall thin POST (reads the angle cleanly)
            //   - a chunky TREE (reads the silhouette shape)
            //   - a standing FIGURE (the "read the time from your shadow" hero)
            SpawnCaster(root.transform, "Post",   new Vector3(-5f, 0f, 0f), MakePostSprite(),   2.4f);
            SpawnCaster(root.transform, "Tree",   new Vector3( 0f, 0f, 0f), MakeTreeSprite(),   3.0f);
            SpawnCaster(root.transform, "Figure", new Vector3( 5f, 0f, 0f), MakeFigureSprite(), 1.8f);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log(
                "[ShadowTest] Spawned 'ShadowTest' (a post, a tree, a standing figure on a ground plane), each " +
                "with a projected SpriteShadow. Press Play, then SCRUB THE CLOCK and watch each shadow SWING " +
                "(long WEST at dawn → short NORTH at noon → long EAST at dusk) and LENGTHEN as the sun sinks; it " +
                "fades out at night and softens under overcast. No clock in the scene? The shadow uses each " +
                "component's 'Fallback Hour' so it still shows. Tune darkness/length per component or on " +
                "Resources/SpriteShadow.mat. Delete 'ShadowTest' to fully revert.");
        }

        // ---- spawn helpers ------------------------------------------------------------------------------

        private static void SpawnCaster(Transform parent, string name, Vector3 pos, Sprite sprite, float heightMeters)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // lower on screen draws in front (¾ top-down depth read).
            sr.sortingOrder = Mathf.RoundToInt(-pos.y * 100f) + 10;

            // Scale the unit-tall sprite to the intended height in metres so the shadow length (× height) reads.
            float spriteH = sprite.bounds.size.y;
            float scale = spriteH > 1e-4f ? heightMeters / spriteH : 1f;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            // The sprites below are authored with the pivot at the FEET, so footOffset stays 0 (default).
            go.AddComponent<SpriteShadow>();
        }

        private static void BuildGround(Transform parent, Color color, float w, float h)
        {
            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite(color);
            sr.sortingOrder = -32000;   // behind every caster AND every shadow
            go.transform.localScale = new Vector3(w, h, 1f);
        }

        // ---- in-memory greybox sprites (not saved to disk; pivots at the FEET = bottom-centre) -----------

        private static Sprite SolidSprite(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.hideFlags = HideFlags.DontSave;
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>A tall, thin post — a clean stick whose shadow angle is unmistakable. Pivot at the feet.</summary>
        private static Sprite MakePostSprite()
        {
            const int W = 8, H = 32, PPU = 32;
            var tex = NewClear(W, H);
            var wood = new Color(0.45f, 0.32f, 0.18f, 1f);
            for (int y = 0; y < H; y++)
                for (int x = 3; x <= 4; x++)
                    tex.SetPixel(x, y, wood);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), PPU);
        }

        /// <summary>A chunky tiered evergreen — a recognisable silhouette. Pivot at the feet (trunk base).</summary>
        private static Sprite MakeTreeSprite()
        {
            const int W = 32, H = 40, PPU = 32;
            var tex = NewClear(W, H);
            var trunk = new Color(0.36f, 0.25f, 0.15f, 1f);
            var leaf = new Color(0.16f, 0.34f, 0.20f, 1f);
            // trunk
            for (int y = 0; y < 9; y++)
                for (int x = 14; x <= 17; x++)
                    tex.SetPixel(x, y, trunk);
            // three tiers, widest at the bottom
            FillTriangle(tex, 8, 16, leaf, 2, 16);    // bottom tier
            FillTriangle(tex, 22, 30, leaf, 5, 13);   // mid tier
            FillTriangle(tex, 30, 39, leaf, 8, 11);   // top tier
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), PPU);
        }

        /// <summary>A simple standing figure (head + body + legs) — the "read your shadow" hero. Pivot at the feet.</summary>
        private static Sprite MakeFigureSprite()
        {
            const int W = 16, H = 32, PPU = 32;
            var tex = NewClear(W, H);
            var skin = new Color(0.85f, 0.68f, 0.55f, 1f);
            var coat = new Color(0.28f, 0.40f, 0.55f, 1f);
            // legs
            for (int y = 0; y < 11; y++) { tex.SetPixel(6, y, coat); tex.SetPixel(7, y, coat); tex.SetPixel(9, y, coat); tex.SetPixel(10, y, coat); }
            // body / coat
            for (int y = 11; y < 24; y++)
                for (int x = 5; x <= 10; x++)
                    tex.SetPixel(x, y, coat);
            // head
            for (int y = 24; y < 30; y++)
                for (int x = 6; x <= 9; x++)
                    tex.SetPixel(x, y, skin);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), PPU);
        }

        private static Texture2D NewClear(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.hideFlags = HideFlags.DontSave;
            var clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, clear);
            return tex;
        }

        /// <summary>Fill a symmetric triangle (a tree tier): rows yLo..yHi, widening downward to half-width.</summary>
        private static void FillTriangle(Texture2D tex, int yLo, int yHi, Color color, int halfWidthTop, int halfWidthBottom)
        {
            int cx = tex.width / 2;
            int span = Mathf.Max(yHi - yLo, 1);
            for (int y = yLo; y <= yHi; y++)
            {
                float t = (float)(yHi - y) / span;   // 0 at top of tier, 1 at bottom
                int hw = Mathf.RoundToInt(Mathf.Lerp(halfWidthTop, halfWidthBottom, t));
                for (int x = cx - hw; x <= cx + hw; x++)
                    if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                        tex.SetPixel(x, y, color);
            }
        }
    }
}
#endif
