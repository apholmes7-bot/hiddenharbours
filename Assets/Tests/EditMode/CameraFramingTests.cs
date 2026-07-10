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

        // ---- data-driven per-boat framing (the bigger-boat zoom-out) -------------------------

        [Test]
        public void DoryReference_DerivesToTheDefaultConstants()
        {
            // The named Dory constants must equal the mapping for the default (~14 m) height.
            CameraFollow.ReferenceResolutionForWorldHeight(CameraFollow.DefaultWorldHeightMeters,
                out int w, out int h);
            Assert.AreEqual(CameraFollow.ReferenceWidthPx, w);
            Assert.AreEqual(CameraFollow.ReferenceHeightPx, h);
        }

        [Test]
        public void EachTierReference_Is16x9_AndPixelPerfectAt1080p()
        {
            foreach (float worldHeight in new[] { 14f, 17f, 25f, 40f })
            {
                CameraFollow.ReferenceResolutionForWorldHeight(worldHeight, out int w, out int h);
                Assert.AreEqual(16f / 9f, w / (float)h, 1e-2f, $"tier {worldHeight} m must be 16:9");

                int zoom = CameraFollow.PixelPerfectZoom(1920, 1080, w, h);
                Assert.GreaterOrEqual(zoom, 1, $"tier {worldHeight} m must hold an integer pixel-perfect zoom");
            }
        }

        [Test]
        public void BiggerBoat_FramesMoreWater_ThanSmaller()
        {
            // Dory (14 m) vs Punt (17 m): the bigger boat's reference is taller (more world) and its
            // live pixel-perfect framing shows strictly more water — the tangible "bigger boat" beat.
            CameraFollow.ReferenceResolutionForWorldHeight(14f, out int dW, out int dH);
            CameraFollow.ReferenceResolutionForWorldHeight(17f, out int pW, out int pH);

            Assert.Greater(pH, dH, "the bigger boat uses a larger (taller) reference → more world");
            Assert.Greater(pW, dW);

            float doryLive = CameraFollow.WorldHeightAtZoom(1080,
                CameraFollow.PixelPerfectZoom(1920, 1080, dW, dH), LockedPPU);
            float puntLive = CameraFollow.WorldHeightAtZoom(1080,
                CameraFollow.PixelPerfectZoom(1920, 1080, pW, pH), LockedPPU);

            Assert.Greater(puntLive, doryLive, "buying the Punt zooms the camera out (more water on screen)");
        }

        [Test]
        public void Framing_IsMonotonic_BiggerRequest_NeverShowsLessWater()
        {
            // Across the ladder, a larger CameraWorldHeightMeters never frames LESS water (non-decreasing).
            float prevLive = 0f;
            foreach (float worldHeight in new[] { 8f, 11f, 14f, 17f, 22f, 30f, 45f })
            {
                CameraFollow.ReferenceResolutionForWorldHeight(worldHeight, out int w, out int h);
                float live = CameraFollow.WorldHeightAtZoom(1080,
                    CameraFollow.PixelPerfectZoom(1920, 1080, w, h), LockedPPU);
                Assert.GreaterOrEqual(live + 1e-3f, prevLive, $"framing must not shrink as the boat grows ({worldHeight} m)");
                prevLive = live;
            }
        }

        // ---- on-foot framing (tighter than the boat; same pixel-perfect mapping) -------------

        [Test]
        public void OnFootFraming_OrthoSize_MapsToAbout9mTall()
        {
            float ortho = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.OnFootWorldHeightMeters);
            Assert.AreEqual(9f, CameraFollow.WorldHeightForOrthoSize(ortho), 0.5f, "on-foot frames ~9 m of world height");
        }

        [Test]
        public void OnFootReference_Is16x9_AndExactIntegerZoomAt1080p()
        {
            CameraFollow.ReferenceResolutionForWorldHeight(CameraFollow.OnFootWorldHeightMeters, out int w, out int h);
            Assert.AreEqual(16f / 9f, w / (float)h, 1e-2f, "on-foot reference is 16:9 landscape");

            int zoom = CameraFollow.PixelPerfectZoom(1920, 1080, w, h);
            Assert.AreEqual(4, zoom, "9 m on-foot resolves to an exact ×4 pixel-perfect zoom at 1080p");
        }

        [Test]
        public void OnFoot_IsTighter_ThanTheBoat()
        {
            Assert.Less(CameraFollow.OnFootWorldHeightMeters, CameraFollow.DefaultWorldHeightMeters,
                "on foot the camera is tighter than in the dory");

            CameraFollow.ReferenceResolutionForWorldHeight(CameraFollow.OnFootWorldHeightMeters, out int fw, out int fh);
            CameraFollow.ReferenceResolutionForWorldHeight(CameraFollow.DefaultWorldHeightMeters, out int bw, out int bh);
            float footLive = CameraFollow.WorldHeightAtZoom(1080, CameraFollow.PixelPerfectZoom(1920, 1080, fw, fh), LockedPPU);
            float boatLive = CameraFollow.WorldHeightAtZoom(1080, CameraFollow.PixelPerfectZoom(1920, 1080, bw, bh), LockedPPU);
            Assert.Less(footLive, boatLive, "the on-foot live framing shows less water than the dory");
        }

        // ---- on-deck zoom steps (owner playtest 2026-07-08: step in close on the deck) --------

        [Test]
        public void DeckFraming_IsExactlyTheNextPixelPerfectStep_InsideOnFoot()
        {
            CameraFollow.ReferenceResolutionForWorldHeight(CameraFollow.DeckWorldHeightMeters, out int w, out int h);
            Assert.AreEqual(16f / 9f, w / (float)h, 1e-2f, "the deck reference is 16:9 landscape");

            int zoom = CameraFollow.PixelPerfectZoom(1920, 1080, w, h);
            Assert.AreEqual(5, zoom, "the deck step is the exact ×5 integer zoom at 1080p — one step in from on-foot's ×4");

            float live = CameraFollow.WorldHeightAtZoom(1080, zoom, LockedPPU);
            Assert.AreEqual(CameraFollow.DeckWorldHeightMeters, live, 1e-4f,
                "the default lands EXACTLY on the pixel-perfect step — no quantisation drift, no shimmer");
        }

        [Test]
        public void HaulFraming_IsOneStepTighterAgain_AndLandsOnTheStep()
        {
            CameraFollow.ReferenceResolutionForWorldHeight(CameraFollow.HaulWorldHeightMeters, out int w, out int h);
            Assert.AreEqual(16f / 9f, w / (float)h, 1e-2f, "the haul reference is 16:9 landscape");

            int zoom = CameraFollow.PixelPerfectZoom(1920, 1080, w, h);
            Assert.AreEqual(6, zoom, "the live-haul step is the exact ×6 integer zoom at 1080p");

            float live = CameraFollow.WorldHeightAtZoom(1080, zoom, LockedPPU);
            Assert.AreEqual(CameraFollow.HaulWorldHeightMeters, live, 1e-4f,
                "the haul default lands EXACTLY on the pixel-perfect step");
        }

        [Test]
        public void TheZoomLadder_IsStrictlyOrdered_HaulInsideDeckInsideOnFootInsideBoat()
        {
            Assert.Less(CameraFollow.HaulWorldHeightMeters, CameraFollow.DeckWorldHeightMeters,
                "a live haul is the tightest framing");
            Assert.Less(CameraFollow.DeckWorldHeightMeters, CameraFollow.OnFootWorldHeightMeters,
                "the deck is a step closer than walking ashore");
            Assert.Less(CameraFollow.OnFootWorldHeightMeters, CameraFollow.DefaultWorldHeightMeters,
                "and every on-your-feet framing is tighter than the helm's");
        }

        [Test]
        public void AFreshCamera_MapsEachFraming_ToItsDefaultStep()
        {
            var go = new GameObject("Cam");
            try
            {
                var follow = go.AddComponent<CameraFollow>();
                Assert.AreEqual(CameraFollow.OnFootWorldHeightMeters, follow.WorldHeightFor(CameraFraming.OnFoot), 1e-4f);
                Assert.AreEqual(CameraFollow.DeckWorldHeightMeters, follow.WorldHeightFor(CameraFraming.Deck), 1e-4f);
                Assert.AreEqual(CameraFollow.HaulWorldHeightMeters, follow.WorldHeightFor(CameraFraming.DeckHaul), 1e-4f);
                Assert.AreEqual(CameraFollow.DefaultWorldHeightMeters, follow.WorldHeightFor(CameraFraming.Boat), 1e-4f,
                    "before any ActiveBoatChanged the boat framing falls back to the Dory default");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
