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
    /// <b>THE EYEBALL FOR A REGION'S SHOPS</b> — renders a built scene to PNGs so shop placement and the
    /// interior reveal can be LOOKED AT rather than argued about from a log. Region-agnostic; the two
    /// callers are <see cref="StPetersShopProof"/> and <see cref="NineMileCreekShopProof"/>.
    ///
    /// <para><b>What it proves that a test cannot.</b> The EditMode suite can assert that a doorway gap
    /// lands on a door anchor and that a footprint clears its neighbour. It cannot say whether the fish
    /// market reads as a fish market, whether a 9 × 11.5 m restaurant is simply too big for a working
    /// wharf, or whether the reveal looks like walking indoors. Those are the owner's call and he needs
    /// a picture to make it — which is the whole point on THIS slice, where both sites are proposals
    /// clearing their neighbours by under half a metre.</para>
    ///
    /// <para><b>⚠️ It renders whatever scene is on disk.</b> It does not build one — running it against
    /// a stale scene produces a confident picture of the previous region. Build first.</para>
    ///
    /// <para><b>⚠️ DAY ONLY, deliberately.</b> The dusk half of a day/dusk pair would mean driving
    /// <c>DayNightController</c>, whose tick, overlay and camera fit are all private and none of which
    /// run outside play mode — so a dusk sheet here would be a picture of this tool's guess at the tint
    /// rather than of the tint.</para>
    /// </summary>
    public static class ShopProof
    {
        /// <summary>Pixels per world unit in the proof sheets. 8 keeps a 60 m frame inside 512 px, which
        /// is a readable screenshot rather than a wall.</summary>
        public const int PixelsPerUnit = 8;

        /// <summary>One wide establishing frame: what to call it, where to look, how tall a view.</summary>
        public readonly struct Wide
        {
            public readonly string Name;
            public readonly Vector2 Centre;
            public readonly float WorldHeight;
            public Wide(string name, Vector2 centre, float worldHeight)
            { Name = name; Centre = centre; WorldHeight = worldHeight; }
        }

        /// <summary>One shop to shoot shut-then-open: the name its GameObject carries, and where it is.</summary>
        public readonly struct Subject
        {
            public readonly string Key;
            public readonly Vector2 At;
            public Subject(string key, Vector2 at) { Key = key; At = at; }
        }

        /// <summary>
        /// Render the sheets for one region. Returns how many were written.
        /// </summary>
        public static int Bake(string scenePath, string outputFolder,
                               IReadOnlyList<Wide> wides, IReadOnlyList<Subject> subjects,
                               string logTag, float shopFrameMetres = 26f)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"{logTag} No scene at '{scenePath}'. Build it first — this tool renders " +
                               "what is on disk and would otherwise say nothing at all.");
                return 0;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(outputFolder);

            var interiors = new Dictionary<string, BuildingInterior>();
            foreach (var bi in Object.FindObjectsByType<BuildingInterior>(FindObjectsSortMode.None))
                interiors[bi.gameObject.name] = bi;

            int written = 0;

            // ⚠️ THE FIRST RENDER AFTER OPENING A SCENE DROPS SPRITES, and it does it silently.
            // Measured on this region 2026-08-12: the first frame — a 110 m view of the whole wharf —
            // came back with the sea, the piles and the bollards but NO BUILDINGS, while the very next
            // frame over the same ground drew the restaurant perfectly. A reviewer reads that as "the
            // shops were never placed", which is the most expensive possible way for a proof tool to be
            // wrong. So the first frame is rendered and THROWN AWAY. Same family as the cold shader
            // cache faking a rendering regression; the cure is the same — warm it, then measure.
            WarmUp(wides.Count > 0 ? wides[0].Centre
                                   : subjects.Count > 0 ? subjects[0].At : Vector2.zero);

            foreach (var wide in wides)
                written += Shot(outputFolder, logTag, wide.Name, wide.Centre, wide.WorldHeight) ? 1 : 0;

            // Each shop twice: shut, then with its ground plan showing. The pair IS the reveal — the
            // runtime swap is exactly these two renders, chosen by whether the player is past the wall.
            foreach (var subject in subjects)
            {
                SetRevealed(interiors, subject.Key, false);
                written += Shot(outputFolder, logTag, $"{subject.Key}-1-shut", subject.At, shopFrameMetres) ? 1 : 0;

                SetRevealed(interiors, subject.Key, true);
                written += Shot(outputFolder, logTag, $"{subject.Key}-2-open", subject.At, shopFrameMetres) ? 1 : 0;

                SetRevealed(interiors, subject.Key, false);
            }

            Debug.Log($"{logTag} wrote {written} sheet(s) to {outputFolder}/ — the wide shot(s), and each " +
                      "shop shut then open. ⚠️ DAY ONLY, by design; see the class remarks.");
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

            Transform room = root.Find(ShopPlacement.InteriorChildName);
            if (room != null)
            {
                var sr = room.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = inside;
            }
        }

        /// <summary>Render one frame and discard it, so the frame that IS kept is not the first one.
        /// See the call site for what the first one leaves out.</summary>
        static void WarmUp(Vector2 centre) => Shot(null, null, null, centre, 40f);

        /// <summary>Render one orthographic frame centred on a world point and write it as a PNG.
        /// A null <paramref name="outputFolder"/> renders and discards (the warm-up).</summary>
        static bool Shot(string outputFolder, string logTag, string name, Vector2 centre, float worldHeight)
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

                    if (outputFolder == null) return true;      // the warm-up: rendered, discarded

                    string path = Path.Combine(outputFolder, name + ".png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Debug.Log($"{logTag} {path} ({w}×{h}, {worldHeight:0} m tall, centred on {centre})");
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
