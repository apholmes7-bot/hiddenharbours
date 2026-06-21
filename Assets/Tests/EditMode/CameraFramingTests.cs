using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// PC-first readability framing (CameraFollow). The greybox follow-cam should frame an intimate
    /// LANDSCAPE view — ~14 m of world height so the 4.5 m Dory reads large — and the Pixel-Perfect
    /// reference should resolve to an exact integer (shimmer-free) zoom at 1920×1080.
    /// </summary>
    public class CameraFramingTests
    {
        private const int LockedPPU = 32; // VS-23 locked assets PPU (one PPU never changes)

        [Test]
        public void OrthoSize_IsHalfWorldHeight()
        {
            Assert.AreEqual(7f, CameraFollow.OrthoSizeForWorldHeight(14f), 1e-4f);
            Assert.AreEqual(4f, CameraFollow.OrthoSizeForWorldHeight(8f), 1e-4f);
        }

        [Test]
        public void DefaultFraming_OrthoSize_MapsToAbout14mTall()
        {
            float ortho = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.DefaultWorldHeightMeters);
            float worldHeight = CameraFollow.WorldHeightForOrthoSize(ortho);

            Assert.AreEqual(14f, worldHeight, 0.5f, "default framing should show ~14 m of world height");
            Assert.That(worldHeight, Is.InRange(12f, 16f), "framing should sit in the intimate 12–16 m band");
        }

        [Test]
        public void ConfiguredCamera_OrthographicSize_ShowsTheIntendedHeight()
        {
            var go = new GameObject("Cam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.DefaultWorldHeightMeters);

                // The actual camera's ortho size maps back to the intended ~14 m world height.
                Assert.AreEqual(CameraFollow.DefaultWorldHeightMeters, cam.orthographicSize * 2f, 0.5f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DefaultFraming_IsLandscape_WiderThanTall()
        {
            float h = CameraFollow.DefaultWorldHeightMeters;
            float w = CameraFollow.WorldWidthForHeight(h, 16f / 9f);

            Assert.Greater(w, h, "a 16:9 landscape view is wider than tall");
            Assert.AreEqual(h * 16f / 9f, w, 1e-3f);
        }

        [Test]
        public void PixelPerfectReference_Is16x9Landscape()
        {
            float aspect = CameraFollow.ReferenceWidthPx / (float)CameraFollow.ReferenceHeightPx;
            Assert.AreEqual(16f / 9f, aspect, 1e-3f, "the Pixel-Perfect reference should be 16:9 landscape");
        }

        [Test]
        public void PixelPerfectReference_At1080p_IsExactIntegerZoom_AndStaysIntimate()
        {
            int zoom = CameraFollow.PixelPerfectZoom(1920, 1080,
                CameraFollow.ReferenceWidthPx, CameraFollow.ReferenceHeightPx);

            // Exact ×3 integer zoom at 1080p → no sub-pixel shimmer.
            Assert.AreEqual(3, zoom, "640×360 resolves to an exact ×3 pixel-perfect zoom at 1080p");

            // The live (pixel-snapped) framing the player sees: PPU-32 quantises to the step nearest the
            // 14 m authored intent — ~11.25 m at 1080p — which keeps the Dory large (it reads as ~40% of
            // screen height). Documents the locked-PPU trade-off behind the ~14 m intent.
            float liveHeight = CameraFollow.WorldHeightAtZoom(1080, zoom, LockedPPU);
            Assert.AreEqual(11.25f, liveHeight, 0.01f);
            Assert.That(liveHeight, Is.InRange(10f, 16f), "live framing stays intimate — the Dory reads large");
        }

        [Test]
        public void PixelPerfectReference_HoldsIntegerZoom_InSmallerDesktopWindows()
        {
            // The old portrait reference (512 tall) collapsed to ×1 below ~1024 px tall (over-wide view,
            // tiny Dory). The landscape reference keeps an intimate integer zoom down to 720p.
            int zoom720 = CameraFollow.PixelPerfectZoom(1280, 720,
                CameraFollow.ReferenceWidthPx, CameraFollow.ReferenceHeightPx);
            Assert.GreaterOrEqual(zoom720, 2, "stays intimate (zoom ≥ ×2) at 720p instead of collapsing");

            float liveHeight720 = CameraFollow.WorldHeightAtZoom(720, zoom720, LockedPPU);
            Assert.That(liveHeight720, Is.InRange(10f, 16f), "720p framing is still intimate");
        }
    }
}
