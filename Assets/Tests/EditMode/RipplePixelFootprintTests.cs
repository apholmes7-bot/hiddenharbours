using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE PIN behind ADR 0027 #10 — <b>a capillary ripple's pixel footprint is the same at every
    /// framing the game has, and that is what retired the item's kill condition.</b>
    ///
    /// <para><b>Provenance.</b> These two assertions are the ARITHMETIC HALF of the report-only spike on
    /// <c>spike/ripples-zoom-fade</c> (<c>RippleZoomFadeSpikeTests</c>), promoted here unchanged in
    /// substance when the #10 band was built. The spike's RENDER half stayed behind: it needs a GPU, and
    /// probing pixels through the real material is a probe fixture's job, not this one's.</para>
    ///
    /// <para><b>What the ADR feared.</b> ADR 0027 wrote #10's kill condition as a zoom problem: *"at a
    /// wider zoom tier a ripple falls below one pixel and becomes pure aliasing … prototype the zoom fade
    /// before committing to the band — if it cannot be made stable across the zoom tiers, this item is
    /// not worth shipping."* That describes a camera that changes ORTHOGRAPHIC SIZE at a fixed screen
    /// resolution. It is not this camera.</para>
    ///
    /// <para><b>What is actually true.</b> <c>PixelPerfectCamera.assetsPPU</c> is LOCKED at 32
    /// (<see cref="CameraFollow.AssetsPPU"/>, mirroring <c>ArtCameraSetup</c>) and
    /// <see cref="CameraFollow"/> frames a tier by changing the REFERENCE RESOLUTION, never the
    /// world-metres-per-pixel ratio. So one pixel is 1/32 m = 3.125 cm at EVERY framing the game asks
    /// for — the 5.6 m live-haul framing through the 90 m Coastal Packet — and a 0.10 m ripple is 3.2 px
    /// throughout. It never goes sub-pixel, so there is no per-tier amplitude fade to build.</para>
    ///
    /// <para><b>Why pin it rather than just write it down.</b> The finding rests entirely on
    /// <c>assetsPPU</c> staying locked. Unlock it — or let a framing path start driving the orthographic
    /// size independently of the reference resolution — and #10's original kill condition comes back
    /// silently, with the shipped ripple band already in the shader. This fixture is the tripwire: it
    /// fails the moment the footprint stops being tier-invariant.</para>
    ///
    /// <para><b>What DOES vary, and what the band does about it</b> (the second test): the widest framing
    /// shows ~6x more sea than the tightest, so it carries ~6x more ripple CYCLES at the same pixel size.
    /// That is a DENSITY problem, not an aliasing one, and it is why the shipped band's fade is keyed to
    /// the FRAMING (<c>_SeaFramingHeight</c> — how much sea is on screen) rather than to pixel size.
    /// Twin: <c>HiddenHarbours.Art.WaterRipple.FramingFade01</c>.</para>
    ///
    /// <para>Pure arithmetic over <see cref="CameraFollow"/>'s public framing math — no camera, no
    /// graphics device, runs anywhere including CI.</para>
    /// </summary>
    public class RipplePixelFootprintTests
    {
        /// <summary>The ADR's band: 0.08–0.15 m. The midpoint stands in for all of it — the claim is
        /// about the RATIO, so any wavelength in the band gives the same verdict.</summary>
        const float RippleWavelengthM = 0.10f;

        const int Ppu = CameraFollow.AssetsPPU;     // 32, locked

        /// <summary>Every framing the game actually asks for: the live-haul/deck/on-foot steps and each
        /// hull's authored <c>CameraWorldHeightMeters</c>, from the Dory to the Coastal Packet.</summary>
        static readonly (string Name, float WorldHeight)[] Framings =
        {
            ("haul (tightest)", 5.625f), ("deck", 6.75f), ("on foot", 9f),
            ("Dory", 14f), ("FishingSkiff", 13.5f), ("PuntUpgraded", 17f),
            ("ConsoleSkiff", 18.5f), ("SportSkiff", 19f), ("LobsterBoat", 23f),
            ("CapeIslander", 24f), ("SideDragger", 40f), ("SternTrawler", 60f),
            ("CoastalPacket", 90f),
        };

        [Test]
        public void TheRipplesPixelFootprintIsTierInvariant()
        {
            var report = new StringBuilder();
            report.AppendLine("framing              want m  zoom  refH px  live m   m/px     ripple px");
            float minPx = float.MaxValue, maxPx = float.MinValue;

            foreach (var (name, want) in Framings)
            {
                CameraFollow.ReferenceResolutionForWorldHeight(want, out _, out int refH);
                int zoom = Mathf.Max(1, CameraFollow.DesignScreenHeightPx / Mathf.Max(1, refH));
                float live = CameraFollow.WorldHeightAtZoom(CameraFollow.DesignScreenHeightPx, zoom, Ppu);
                float metresPerPixel = live / refH;          // world height over the internal buffer
                float ripplePx = RippleWavelengthM / metresPerPixel;
                minPx = Mathf.Min(minPx, ripplePx);
                maxPx = Mathf.Max(maxPx, ripplePx);
                report.AppendLine($"{name,-20}{want,7:0.#}{zoom,6}{refH,9}{live,8:0.00}" +
                                  $"{metresPerPixel * 100,7:0.000}cm{ripplePx,10:0.00}");
            }
            TestContext.WriteLine("[ripple footprint] " + report);

            // THE FINDING: the footprint does not vary. assetsPPU is locked, so a tier changes the
            // reference resolution, never the world-per-pixel ratio.
            Assert.AreEqual(minPx, maxPx, 0.01f,
                $"the ripple's pixel footprint must be tier-INVARIANT; measured {minPx:0.00}..{maxPx:0.00} px. " +
                "If this fails, assetsPPU has been unlocked (or a framing path now drives ortho size " +
                "independently of the reference resolution) and ADR 0027 #10's sub-pixel kill condition " +
                "is LIVE again — with the ripple band already shipped in the shader.");
            Assert.AreEqual(1f / Ppu, RippleWavelengthM / minPx, 1e-4f,
                "and it must equal 1/assetsPPU — the locked pixel grid.");

            // …and it never goes sub-pixel, which is the condition the ADR feared.
            Assert.Greater(minPx, 2f,
                $"a {RippleWavelengthM * 100:0}cm ripple resolves at {minPx:0.00} px — the ADR's " +
                "sub-pixel kill condition does not arise on this camera.");
        }

        [Test]
        public void WhatVariesIsHowMuchSeaIsOnScreen()
        {
            // The real remaining risk is DENSITY, not aliasing: the widest framing shows ~6x more world
            // than the tightest, so ~6x more ripple cycles at the same pixel size. That is a busyness /
            // moire question — which the shipped band answers with the FRAMING-keyed fade
            // (WaterRipple.FramingFade01) plus the ADR's threshold dither, NOT with a per-tier
            // amplitude fade keyed to pixel size (there is nothing there to key to — see the test above).
            CameraFollow.ReferenceResolutionForWorldHeight(5.625f, out _, out int tightRefH);
            CameraFollow.ReferenceResolutionForWorldHeight(90f, out _, out int wideRefH);
            float tight = tightRefH / (float)Ppu;
            float wide = wideRefH / (float)Ppu;
            float cyclesTight = tight / RippleWavelengthM;
            float cyclesWide = wide / RippleWavelengthM;
            TestContext.WriteLine(
                $"[ripple footprint] world height {tight:0.0} m -> {wide:0.0} m across the tiers; " +
                $"ripple cycles down-screen {cyclesTight:0} -> {cyclesWide:0} " +
                $"({cyclesWide / cyclesTight:0.0}x more). The pixel SIZE is unchanged; only the COUNT grows.");
            Assert.Greater(cyclesWide, cyclesTight * 2f,
                "the density delta is the finding the framing fade exists for — pin it so it cannot " +
                "silently shrink out from under the fade's reason to exist.");
        }
    }
}
