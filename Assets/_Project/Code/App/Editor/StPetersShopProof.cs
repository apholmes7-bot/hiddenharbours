#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.World;               // BuildingInterior

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE EYEBALL FOR THE TWO SHOPS.</b> Renders the built St Peters scene to PNGs so the shop
    /// placement and the interior reveal can be LOOKED AT rather than argued about from a log. Sibling
    /// of <c>CliffProofMenu</c> and <c>RadarProofEyeball</c>, and the same kind of thing: a dev tool that
    /// makes a claim visible, not a build step.
    ///
    /// <para><b>What it proves that a test cannot.</b> The EditMode suite can assert that a doorway gap
    /// lands on a door anchor and that a footprint clears its neighbour. It cannot say whether the post
    /// office reads as a post office, whether the two shops sit right beside the houses at the same
    /// scale, or whether the reveal looks like walking indoors. Those are the owner's call and he needs
    /// a picture to make it.</para>
    ///
    /// <para><b>⚠️ It renders whatever scene is on disk.</b> It does not build one — running it against
    /// a stale StPeters.unity produces a confident picture of the previous village. Build first.</para>
    ///
    /// <para><b>⚠️ DAY ONLY, deliberately.</b> The dusk half of a day/dusk pair would mean driving
    /// <c>DayNightController</c>, whose tick, overlay and camera fit are all private and none of which
    /// run outside play mode — so a dusk sheet here would have to reach in by reflection and would be a
    /// picture of this tool's guess at the tint rather than of the tint. The lighting stack has its own
    /// proofs; this one is about where the buildings stand and what is behind their doors.</para>
    /// </summary>
    public static class StPetersShopProof
    {
        const string ScenePath = "Assets/_Project/Scenes/StPeters.unity";
        const string OutputFolder = "artifacts/shop-proof";

        /// <summary>Pixels per world unit in the proof sheets. 8 keeps a 60 m frame inside 512 px, which
        /// is a readable screenshot rather than a wall.</summary>
        const int PixelsPerUnit = 8;

        [MenuItem("Hidden Harbours/Dev/Bake St Peters Shop Proofs (village + reveal)")]
        public static void BakeMenu() => Bake();

        public static void BakeFromCommandLine()
        {
            try
            {
                int n = Bake();
                Debug.Log($"[shop-proof] (batch) wrote {n} sheet(s).");
                if (n == 0)
                {
                    Debug.LogError("[shop-proof] wrote nothing — see above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[shop-proof] threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Render the sheets. Returns how many were written.</summary>
        public static int Bake()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[shop-proof] No scene at '{ScenePath}'. Run " +
                               "Hidden Harbours ▸ Build St Peters Scene first — this tool renders what " +
                               "is on disk and would otherwise say nothing at all.");
                return 0;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(OutputFolder);

            var interiors = new Dictionary<string, BuildingInterior>();
            foreach (var bi in Object.FindObjectsByType<BuildingInterior>(FindObjectsSortMode.None))
                interiors[bi.gameObject.name] = bi;

            int written = 0;

            // The village, wide: the lane, the green, the houses and both shops in one frame — the shot
            // that answers "do the two kits stand at the same scale beside each other".
            written += Shot("village-wide", new Vector2(6f, 24f), 80f) ? 1 : 0;

            // Each shop twice: shut, then with its ground plan showing. The pair IS the reveal — the
            // runtime swap is exactly these two renders, chosen by whether the player is past the wall.
            foreach (var site in StPetersShops.Sites)
            {
                Vector2 at = site.Position;

                SetRevealed(interiors, site.Key, false);
                written += Shot($"{site.Key}-1-shut", at, 26f) ? 1 : 0;

                SetRevealed(interiors, site.Key, true);
                written += Shot($"{site.Key}-2-open", at, 26f) ? 1 : 0;

                SetRevealed(interiors, site.Key, false);
            }

            Debug.Log($"[shop-proof] wrote {written} sheet(s) to {OutputFolder}/ — the village wide, and " +
                      "each shop shut then open. ⚠️ DAY ONLY, by design; see the class remarks.");
            return written;
        }

        /// <summary>
        /// Show or hide one shop's interior, the way <see cref="BuildingInterior"/> does at the threshold.
        ///
        /// <para>The renderers are switched directly rather than by moving a fake player across the
        /// doorway: the component decides in <c>Update</c>, which does not run outside play mode, so a
        /// moved transform would change nothing and the "open" sheet would be a second copy of the shut
        /// one. Switching the same two renderers the component switches is the same picture with none of
        /// the ceremony — and the geometry that decides WHICH picture is what the EditMode suite
        /// pins.</para>
        /// </summary>
        static void SetRevealed(Dictionary<string, BuildingInterior> interiors, string key, bool inside)
        {
            if (!interiors.TryGetValue(key, out var bi) || bi == null) return;

            Transform root = bi.transform;
            var shell = root.GetComponent<SpriteRenderer>();
            if (shell != null) shell.enabled = !inside;

            Transform room = root.Find(StPetersShops.InteriorChildName);
            if (room != null)
            {
                var sr = room.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = inside;
            }
        }

        /// <summary>Render one orthographic frame centred on a world point and write it as a PNG.</summary>
        static bool Shot(string name, Vector2 centre, float worldHeight)
        {
            var camGo = new GameObject("__proofCamera");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = worldHeight * 0.5f;
                cam.transform.position = new Vector3(centre.x, centre.y, -50f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 1f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 200f;

                int h = Mathf.RoundToInt(worldHeight * PixelsPerUnit);
                int w = Mathf.RoundToInt(worldHeight * cam.aspect * PixelsPerUnit);
                if (w <= 0 || h <= 0) return false;

                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { useMipMap = false };
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
                RenderTexture prev = RenderTexture.active;
                try
                {
                    cam.targetTexture = rt;
                    cam.Render();

                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply(false, false);

                    string path = Path.Combine(OutputFolder, name + ".png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Debug.Log($"[shop-proof] {path} ({w}×{h}, {worldHeight:0} m tall, centred on {centre})");
                    return true;
                }
                finally
                {
                    RenderTexture.active = prev;
                    cam.targetTexture = null;
                    Object.DestroyImmediate(tex);
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
#endif
