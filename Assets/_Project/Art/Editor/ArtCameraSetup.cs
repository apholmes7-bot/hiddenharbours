#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal; // URP PixelPerfectCamera (assembly: Unity.RenderPipelines.Universal.2D.Runtime)

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// VS-23 — the locked <b>Pixel Perfect Camera</b> convention (bible §3.7, §9.2). One PPU never
    /// changes; only camera distance does, and the render snaps to whole pixels so there is no
    /// sub-pixel shimmer as the follow-cam tracks the boat. This helper applies those locked settings
    /// to any camera, so the greybox and (later) world-content's authored region scenes all share one
    /// camera spec.
    /// </summary>
    public static class ArtCameraSetup
    {
        // The locked camera spec (see Art/README.md §Pixel-Perfect camera).
        public const int AssetsPPU      = 32;   // matches the sprite PPU lock
        public const int RefResolutionX = 288;  // portrait mobile base (9:16)
        public const int RefResolutionY = 512;  // 512 / 32 = ~16 m of world height (intimate default zoom)

        /// <summary>
        /// Ensure <paramref name="cameraGo"/> carries a Pixel Perfect Camera configured to the locked
        /// spec. Idempotent: reuses an existing component if present. Returns true on success.
        /// </summary>
        public static bool ConfigurePixelPerfect(GameObject cameraGo)
        {
            if (cameraGo == null) return false;

            var ppc = cameraGo.GetComponent<PixelPerfectCamera>()
                   ?? cameraGo.AddComponent<PixelPerfectCamera>();

            ppc.assetsPPU      = AssetsPPU;
            ppc.refResolutionX = RefResolutionX;
            ppc.refResolutionY = RefResolutionY;
            ppc.gridSnapping   = PixelPerfectCamera.GridSnapping.PixelSnapping; // snap render to whole pixels → no shimmer
            ppc.cropFrame      = PixelPerfectCamera.CropFrame.None;             // fill the view (dev-friendly; no bars)

            EditorUtility.SetDirty(ppc);
            return true;
        }

        [MenuItem("Hidden Harbours/Art/Configure Pixel-Perfect Camera (active scene)")]
        static void ConfigureActiveSceneCamera()
        {
            Camera cam = Camera.main;
#if UNITY_2023_1_OR_NEWER
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
            if (cam == null) cam = UnityEngine.Object.FindObjectOfType<Camera>();
#endif
            if (cam == null)
            {
                Debug.LogWarning("[ArtCameraSetup] No Camera found in the active scene.");
                return;
            }

            ConfigurePixelPerfect(cam.gameObject);
            EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
            Debug.Log($"[ArtCameraSetup] Pixel Perfect Camera locked on '{cam.name}' " +
                      $"(PPU {AssetsPPU}, ref {RefResolutionX}x{RefResolutionY}, pixel-snapping on).");
        }
    }
}
#endif
