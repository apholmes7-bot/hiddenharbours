using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// **THE WHOLE VESSEL VISIBLE** — the owner's helm-framing ruling (2026-07-29, boats-and-navigation
    /// §9.8): *"cameras should zoom out on larger vessels so the whole vessels are visible; they seem
    /// fine up till lobster boat and then you're too zoomed in on larger vessels."*
    ///
    /// <para>⚠️ <b>The defect was a silent CAP, not a missing derivation.</b> The step search ran
    /// integer upscale only (x1..x8) and <c>PixelPerfectCamera</c>'s zoom clamps at ≥ 1, so the widest
    /// framing expressible at all was <c>screenH / ppu</c> = 33.75 m at 1080p. The defs already ask for
    /// 40 / 60 / 90 / 160 m for dragger → tanker; every one was quietly served 33.75 and rendered at
    /// the SAME framing as the others. That is exactly what he saw.</para>
    ///
    /// <para>⚠️ <b>And the ruling's two constraints cannot both hold above ~37.5 m of hull</b> at the
    /// locked PPU 32 (VS-23) — a 110 m tanker is 3,520 asset pixels on a 1,920 px screen. Resolved by
    /// the coordinator (2026-07-30): the ladder continues outward by integer DOWNSCALE, a clean pixel
    /// ratio rather than an arbitrary ortho, so big hulls lose pixel DETAIL rather than being cropped.</para>
    /// </summary>
    public class HelmFramingTests
    {
        private const int Ppu = 32;
        private const int ScreenH = 1080;
        private const float Aspect = 16f / 9f;
        private const float Elev = 40f;
        private const float Margin = 1.4f;

        // ===== the ladder =================================================================================

        [Test]
        public void TheLadderRunsBothWays_AndStep1IsThePivot()
        {
            Assert.AreEqual(33.75f, CameraZoomPolicy.WorldHeightForStep(1, Ppu, ScreenH), 1e-3f, "x1 = 1:1");
            Assert.AreEqual(16.875f, CameraZoomPolicy.WorldHeightForStep(2, Ppu, ScreenH), 1e-3f, "x2 in");
            Assert.AreEqual(67.5f, CameraZoomPolicy.WorldHeightForStep(-2, Ppu, ScreenH), 1e-3f, "2:1 out");
            Assert.AreEqual(101.25f, CameraZoomPolicy.WorldHeightForStep(-3, Ppu, ScreenH), 1e-3f, "3:1 out");

            Assert.IsTrue(CameraZoomPolicy.StepIsPixelPerfectUpscale(1));
            Assert.IsFalse(CameraZoomPolicy.StepIsPixelPerfectUpscale(-2),
                "a downscale step must be flagged — PixelPerfectCamera cannot express it, and driving " +
                "it through PPC anyway would silently snap back to 33.75 m");
        }

        [Test]
        public void EveryStepIsACleanPixelRatio_NeverAnArbitraryOrtho()
        {
            // The ruling's standing constraint. Each step must be screenH/(ppu·k) or screenH·k/ppu for
            // an integer k — i.e. asset pixels map to screen pixels by a whole number either way.
            for (int s = CameraZoomPolicy.MinStep; s <= CameraZoomPolicy.MaxStep; s++)
            {
                if (s == -1 || s == 0) continue;
                float h = CameraZoomPolicy.WorldHeightForStep(s, Ppu, ScreenH);
                float ratio = s >= 1 ? ScreenH / (h * Ppu) : h * Ppu / ScreenH;
                Assert.AreEqual(Mathf.Round(ratio), ratio, 1e-4f,
                    $"step {s} ({h:F3} m) is not a whole pixel ratio — that is an arbitrary ortho size");
            }
        }

        // ===== the defect ==================================================================================

        [Test]
        public void TheOldSearch_CappedEveryBigHullOntoOneFraming_TheDefect()
        {
            // Sabotage-as-evidence: with an upscale-only ladder, 40 / 60 / 90 / 160 m ALL land on
            // 33.75. If they did not, there was no defect and this whole PR is decoration.
            float capped = CameraZoomPolicy.WorldHeightForStep(1, Ppu, ScreenH);
            foreach (float asked in new[] { 40f, 60f, 90f, 160f })
            {
                int upscaleOnly = 1;
                float bestErr = float.MaxValue;
                for (int z = 1; z <= 8; z++)
                {
                    float err = Mathf.Abs(CameraZoomPolicy.WorldHeightForStep(z, Ppu, ScreenH) - asked);
                    if (err < bestErr) { bestErr = err; upscaleOnly = z; }
                }
                Assert.AreEqual(capped, CameraZoomPolicy.WorldHeightForStep(upscaleOnly, Ppu, ScreenH), 1e-3f,
                    $"a def asking {asked} m used to be served {capped} m — every big hull, the same framing");
            }
        }

        [Test]
        public void ADefAskingForMoreThanTheOldCap_NowGetsIt()
        {
            foreach ((float asked, float atLeast) in new[] { (40f, 33.75f), (60f, 60f), (160f, 135f) })
            {
                int step = CameraZoomPolicy.StepForWorldHeight(asked, Ppu, ScreenH);
                float got = CameraZoomPolicy.WorldHeightForStep(step, Ppu, ScreenH);
                Assert.GreaterOrEqual(got, atLeast - 1e-3f,
                    $"asking {asked} m must no longer be capped at 33.75 (got {got:F2})");
            }
        }

        // ===== the floor ===================================================================================

        [Test]
        public void TheSmallFleetIsUNTOUCHED_TheFloorOnlyEverPushesOut()
        {
            // The owner is happy below the lobster boat, so the derivation must not move those hulls.
            // A floor, not a replacement: a dory's 14 m is intimacy, not a fit requirement.
            foreach ((string name, float len, float authored) in new[]
                     { ("Dory", 4.5f, 14f), ("FishingSkiff", 4f, 13.5f), ("PuntUpgraded", 5.2f, 17f),
                       ("SportSkiff", 7f, 19f), ("LobsterBoat", 12f, 23f), ("CapeIslander", 12.9f, 24f) })
            {
                float framed = CameraZoomPolicy.HelmWorldHeightMeters(authored, len, Margin, Elev, Aspect);
                Assert.AreEqual(authored, framed, 1e-4f,
                    $"{name}: the authored framing must stand — the ruling is about BIG vessels");
            }
        }

        [Test]
        public void EveryHullFitsOnScreenAtItsWorstHeading()
        {
            // The acceptance, as arithmetic: at the framing each hull ends up with, the vessel fits —
            // bow-on (foreshortened into the SHORT axis) and beam-on (across the long axis) alike.
            float foreshorten = Mathf.Sin(Elev * Mathf.Deg2Rad);
            foreach ((string name, float len, float authored) in new[]
                     { ("Dory", 4.5f, 14f), ("LobsterBoat", 12f, 23f), ("CapeIslander", 12.9f, 24f),
                       ("SideDragger", 25f, 40f), ("SternTrawler", 38f, 60f),
                       ("CoastalPacket", 60f, 90f), ("Tanker", 110f, 160f) })
            {
                float wanted = CameraZoomPolicy.HelmWorldHeightMeters(authored, len, Margin, Elev, Aspect);
                int step = CameraZoomPolicy.StepForWorldHeight(wanted, Ppu, ScreenH);
                float h = CameraZoomPolicy.WorldHeightForStep(step, Ppu, ScreenH);

                Assert.GreaterOrEqual(h, len * foreshorten, $"{name}: bow-on does not fit in {h:F2} m of height");
                Assert.GreaterOrEqual(h * Aspect, len, $"{name}: beam-on does not fit in {h * Aspect:F2} m of width");
            }
        }

        [Test]
        public void ABiggerHullNeverFramesTIGHTER_TheScaleFantasyHoldsMonotonically()
        {
            float prev = 0f;
            foreach ((float len, float authored) in new[]
                     { (4.5f, 14f), (12f, 23f), (25f, 40f), (38f, 60f), (60f, 90f), (110f, 160f) })
            {
                float wanted = CameraZoomPolicy.HelmWorldHeightMeters(authored, len, Margin, Elev, Aspect);
                float h = CameraZoomPolicy.WorldHeightForStep(
                    CameraZoomPolicy.StepForWorldHeight(wanted, Ppu, ScreenH), Ppu, ScreenH);
                Assert.GreaterOrEqual(h, prev - 1e-3f, "a bigger boat must never show LESS water");
                prev = h;
            }
        }

        [Test]
        public void TheForeshorteningIsLoadBearing_NotDecoration()
        {
            // Measuring the fit against raw length (ignoring that the view is iso) would zoom out
            // ~55 % further than any heading needs — a real cost in how close the boat reads.
            float withIso = CameraZoomPolicy.HelmWorldHeightMeters(0f, 40f, Margin, Elev, Aspect);
            float withoutIso = CameraZoomPolicy.HelmWorldHeightMeters(0f, 40f, Margin, 90f, Aspect);
            Assert.Less(withIso, withoutIso * 0.75f,
                $"iso foreshortening must reduce the requirement ({withIso:F1} m vs {withoutIso:F1} m)");
        }

        // ===== framing x bounds: the #329 clamp interaction the handoff flagged =============================

        [Test]
        public void ABigHullsFraming_CanExceedARegion_AndTheClampCentresInsteadOfJittering()
        {
            // ⚠️ THE INTERACTION. A tanker framing is 135 m tall / 240 m wide — wider than plenty of
            // regions, so the per-axis CENTRE path built in #329 stops being an edge case and becomes
            // the normal state for big vessels in small waters. If it ever regressed to a naive clamp,
            // the camera would snap to one edge and flip to the other as the boat crossed.
            float tanker = CameraZoomPolicy.HelmWorldHeightMeters(160f, 110f, Margin, Elev, Aspect);
            float h = CameraZoomPolicy.WorldHeightForStep(
                CameraZoomPolicy.StepForWorldHeight(tanker, Ppu, ScreenH), Ppu, ScreenH);
            float halfH = h * 0.5f, halfW = halfH * Aspect;

            // Nine Mile Creek's cove is far smaller than that view.
            var cove = new CameraBounds(new Vector2(40f, -25f), new Vector2(160f, 120f));
            Assert.Less(cove.SizeMeters.x, halfW * 2f, "fixture: the view is wider than the region");
            Assert.Less(cove.SizeMeters.y, halfH * 2f, "fixture: …and taller than it");

            foreach (Vector2 target in new[] { new Vector2(-9999f, -9999f), new Vector2(9999f, 9999f),
                                               new Vector2(40f, -25f) })
            {
                Vector2 c = CameraBounds.Clamp(target, cove, halfW, halfH);
                Assert.AreEqual(cove.Center, c,
                    $"target {target}: a view bigger than the region must CENTRE, not pick an edge");
            }
        }

        [Test]
        public void ASmallHullInTheSameRegion_StillClampsToTheEdges()
        {
            // The other half of the interaction: the same region with a dory's framing is bigger than
            // the view, so the clamp must still behave as a fence.
            float dory = CameraZoomPolicy.HelmWorldHeightMeters(14f, 4.5f, Margin, Elev, Aspect);
            float h = CameraZoomPolicy.WorldHeightForStep(
                CameraZoomPolicy.StepForWorldHeight(dory, Ppu, ScreenH), Ppu, ScreenH);
            float halfH = h * 0.5f, halfW = halfH * Aspect;

            var cove = new CameraBounds(new Vector2(40f, -25f), new Vector2(160f, 120f));
            Vector2 c = CameraBounds.Clamp(new Vector2(9999f, 9999f), cove, halfW, halfH);
            Assert.AreEqual(40f + 80f - halfW, c.x, 1e-3f, "the view's right edge sits on the region edge");
            Assert.AreEqual(-25f + 60f - halfH, c.y, 1e-3f, "…and its top edge likewise");
        }
    }
}
