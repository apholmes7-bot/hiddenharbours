using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The PIXEL-VERIFIED orientation contract between the authored wake/spray art and the render code — the
    /// regression guard born from the owner's "the wake is backwards" playtest report. The render math in
    /// <see cref="BoatWakeEmitter"/> bakes in two assumptions about the authored textures:
    /// <list type="bullet">
    /// <item><description><b>Wake plume</b> (<c>Art/VFX/Wake/*.png</c>): the NARROW apex (the boat end, the
    /// dense churn) is at the TOP of the image and the plume WIDENS toward the bottom — so an apex pivot of
    /// <c>PlumePivotY = 1</c> aligned +Y-to-bow pins the narrow end at the stern and trails the wide wash
    /// astern.</description></item>
    /// <item><description><b>Bow spray</b> (<c>Art/VFX/BowSpray/*.png</c>): the dense IMPACT churn (the
    /// cutwater end) is at the BOTTOM of the image and the droplet fan spreads upward — so an impact pivot of
    /// <c>SprayPivotY = 0</c> aligned +Y-to-bow pins the impact at the bow and fans the spray ahead.</description></item>
    /// </list>
    /// These tests measure the ACTUAL opaque pixels of every tier texture, so a future art re-export that
    /// flips either set FAILS here instead of silently shipping a backwards wake again. The textures import
    /// non-readable (<c>isReadable: 0</c>), so the tests read the raw PNG bytes from disk and decode with
    /// <see cref="ImageConversion.LoadImage"/> — the decoded texture follows the same Unity convention the
    /// renderer sees (texture y = 0 is the BOTTOM of the image as displayed). Also pins the pure placement
    /// math (<see cref="WakeGrading.SternAnchor"/> / <see cref="WakeGrading.OrientAngleDeg"/> /
    /// <see cref="WakeGrading.FlipPivotY"/>) and the config defaults those pixels justify.
    /// </summary>
    public class WakeArtOrientationTests
    {
        private const byte AlphaOver = 32;                 // "visibly opaque" — ignores faint dither dust
        private static readonly string[] TierNames = { "Small", "Medium", "Large", "Huge" };

        // ==== png loading (from disk — the imported assets are not CPU-readable) ==========================

        private static Texture2D LoadPng(string assetsRelativePath)
        {
            string full = Path.Combine(Application.dataPath, assetsRelativePath);
            Assert.IsTrue(File.Exists(full), $"authored art present on disk: Assets/{assetsRelativePath}");
            byte[] bytes = File.ReadAllBytes(full);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(tex, bytes), $"PNG decodes: Assets/{assetsRelativePath}");
            return tex;
        }

        /// <summary>Widest opaque row-span (px) over texture rows [yFrom, yTo). Texture y=0 = image bottom.</summary>
        private static int MaxOpaqueSpan(Color32[] px, int w, int yFrom, int yTo)
        {
            int best = 0;
            for (int y = yFrom; y < yTo; y++)
            {
                int minX = int.MaxValue, maxX = -1;
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (px[row + x].a > AlphaOver)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                    }
                }
                if (maxX >= minX && maxX - minX + 1 > best) best = maxX - minX + 1;
            }
            return best;
        }

        /// <summary>Count of opaque pixels over texture rows [yFrom, yTo).</summary>
        private static int OpaqueCount(Color32[] px, int w, int yFrom, int yTo)
        {
            int n = 0;
            for (int y = yFrom; y < yTo; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                    if (px[row + x].a > AlphaOver) n++;
            }
            return n;
        }

        // ==== WAKE: narrow apex at the image TOP (widens astern when pinned +Y-to-bow) ====================

        [Test]
        public void WakePlumes_NarrowApexIsAtTheTopOfTheImage([ValueSource(nameof(Tiers))] string tier)
        {
            Texture2D tex = LoadPng($"_Project/Art/VFX/Wake/{tier}.png");
            try
            {
                var px = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                // Texture y=0 is the image BOTTOM, so the image TOP is the high-y band.
                int apexSpan = MaxOpaqueSpan(px, w, Mathf.RoundToInt(h * 0.85f), h);
                int bodySpan = MaxOpaqueSpan(px, w, Mathf.RoundToInt(h * 0.25f), Mathf.RoundToInt(h * 0.60f));

                Assert.Greater(apexSpan, 0, $"{tier}: the apex band has visible churn (art present at the top)");
                Assert.Greater(bodySpan, apexSpan * 1.3f,
                    $"{tier}: the plume WIDENS away from the top of the image (apex {apexSpan}px vs body " +
                    $"{bodySpan}px). If this fails the art was re-authored flipped — turn on PlumeFlip (or " +
                    "re-export the art) instead of shipping a backwards wake.");
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        // ==== BOW SPRAY: dense impact churn at the image BOTTOM (fans ahead when pinned +Y-to-bow) ========

        [Test]
        public void BowSprays_ImpactChurnIsAtTheBottomOfTheImage([ValueSource(nameof(Tiers))] string tier)
        {
            Texture2D tex = LoadPng($"_Project/Art/VFX/BowSpray/{tier}.png");
            try
            {
                var px = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                // Texture y=0 is the image BOTTOM — where the cutwater impact churn must live.
                int impact = OpaqueCount(px, w, 0, Mathf.RoundToInt(h * 0.30f));
                int far = OpaqueCount(px, w, Mathf.RoundToInt(h * 0.70f), h);

                Assert.Greater(impact, 20, $"{tier}: the impact band holds real churn (not just stray dots)");
                Assert.Greater(impact, far * 3,
                    $"{tier}: the spray's dense IMPACT end is at the image bottom (impact {impact}px vs far " +
                    $"{far}px). If this fails the art was re-authored flipped — flip the spray in config (or " +
                    "re-export the art) instead of shipping spray that fans backwards.");
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        private static string[] Tiers() => TierNames;

        // ==== the defaults those pixels justify (keep config and art honest together) ====================

        [Test]
        public void Defaults_MatchTheAuthoredArtOrientation()
        {
            var c = WakeGradeConfig.Default;
            Assert.AreEqual(1f, c.PlumePivotY, 1e-5f,
                "the wake art's narrow apex is at the image TOP (pixel-verified above) → pivot 1 pins it at the stern");
            Assert.IsFalse(c.PlumeFlip,
                "the current art is authored apex-at-top → no flip. PlumeFlip exists for re-authored art only.");
        }

        // ==== the pure placement/orientation math the fix rides on =======================================

        /// <summary>A plan view — no foreshortening. These cases were written against the top-down placement,
        /// which is still exactly what art declaring 90 must get; the PROJECTION for art that is a real ¾ bake
        /// is pinned separately, in WakeProjectionTests.</summary>
        private const float PlanViewElev = 90f;

        [Test]
        public void SternAnchor_SitsAtTheSternNotTheBoatOrigin()
        {
            // The dory: 4.5 m hull, origin at the hull centre, 0.3 m nudge → apex 2.55 m astern of the origin.
            Vector2 a = WakeGrading.SternAnchor(Vector2.zero, Vector2.up, 4.5f, 0.3f, PlanViewElev);
            Assert.AreEqual(0f, a.x, 1e-4f);
            Assert.AreEqual(-2.55f, a.y, 1e-4f, "half the hull + the nudge — BEHIND the stern, never under the hull");

            // Whatever the heading, the anchor is at least half the hull astern (the old bug: only the offset).
            Vector2 bow = new Vector2(1f, 1f).normalized;
            Vector2 b = WakeGrading.SternAnchor(new Vector2(3f, -2f), bow, 6f, 0.25f, PlanViewElev);
            float astern = Vector2.Dot(b - new Vector2(3f, -2f), bow);
            Assert.LessOrEqual(astern, -3f, "anchor is astern of the hull's rear edge (≤ −length/2 along the bow)");
        }

        [Test]
        public void SternAnchor_DegenerateBow_FallsBackToUp_NoNaN()
        {
            Vector2 a = WakeGrading.SternAnchor(Vector2.zero, Vector2.zero, 4.5f, 0.3f, PlanViewElev);
            Assert.IsFalse(float.IsNaN(a.x) || float.IsNaN(a.y), "zero bow never yields NaN");
            Assert.AreEqual(-2.55f, a.y, 1e-4f, "falls back to +Y as the bow");
        }

        [Test]
        public void OrientAngleDeg_PointsLocalUpAtTheBow_AndFlipAdds180()
        {
            Assert.AreEqual(0f, WakeGrading.OrientAngleDeg(Vector2.up, flip: false), 1e-4f, "bow north → no turn");
            Assert.AreEqual(-90f, WakeGrading.OrientAngleDeg(Vector2.right, flip: false), 1e-4f, "bow east → −90 (CW)");
            Assert.AreEqual(90f, WakeGrading.OrientAngleDeg(Vector2.left, flip: false), 1e-4f, "bow west → +90 (CCW)");
            Assert.AreEqual(180f, WakeGrading.OrientAngleDeg(Vector2.up, flip: true), 1e-4f, "flip turns it around");
        }

        [Test]
        public void FlipPivotY_MirrorsThePivotOntoTheOtherEnd()
        {
            Assert.AreEqual(1f, WakeGrading.FlipPivotY(1f, flip: false), 1e-5f, "un-flipped: as authored");
            Assert.AreEqual(0f, WakeGrading.FlipPivotY(1f, flip: true), 1e-5f, "flipped: top pivot becomes bottom");
            Assert.AreEqual(0.75f, WakeGrading.FlipPivotY(0.25f, flip: true), 1e-5f, "mirror is exact, not a snap");
            Assert.AreEqual(1f, WakeGrading.FlipPivotY(5f, flip: false), 1e-5f, "clamped into 0..1");
        }
    }
}
