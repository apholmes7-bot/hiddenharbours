using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the WATER PALETTE GUARD-RAIL (the final soft colour-grade stage on the water,
    /// ADR 0015) — the pure twin in <see cref="WaterPaletteGrade"/> the shader mirrors. The guard-rail bounds
    /// the composited water colour to an art-directed palette so it can never wash out (too bright) or go
    /// muddy (too dark), while preserving the dynamic diversity. These lock the contract without opening Unity:
    ///
    ///   • STRENGTH 0 = an EXACT passthrough (today's look) at every input + day/night state.
    ///   • The day/night-aware FLOOR keeps DAYLIGHT from going muddy, yet lets TRUE NIGHT go genuinely dark.
    ///   • The CEILING caps blowout; the SATURATION CAP tames an over-saturated layer; the ANCHOR PULL is soft.
    ///
    /// All VISUAL-only (col.rgb) — the grade never touches depth/clip/_WaterLevel/the sim (P1, rule 5).
    /// </summary>
    public class WaterPaletteGradeTests
    {
        // A North-Atlantic-ish palette mirroring the shipped Water.mat defaults (ADR 0015) so the tests
        // exercise the shipped configuration, not an invented one.
        private static WaterPaletteGradeParams NorthAtlantic(float strength = 0.35f) => new WaterPaletteGradeParams
        {
            Strength = strength,
            ValueFloor = 0.10f,
            ValueCeil = 0.85f,
            SatCap = 0.55f,
            PullStrength = 0.35f,
            NightFloor = 0.0f,
            Deep = new Vector3(0.05f, 0.135f, 0.205f),
            Mid = new Vector3(0.14f, 0.30f, 0.38f),
            Shallow = new Vector3(0.34f, 0.60f, 0.62f),
            Foam = new Vector3(0.92f, 0.96f, 0.98f),
        };

        private static Vector3 V(float r, float g, float b) => new Vector3(r, g, b);
        private static float Luma(Vector3 c) => WaterPaletteGrade.Luminance(c);

        // ===== STRENGTH 0 = exact identity (the revertible passthrough) ====================================

        [Test]
        public void Strength0_IsExactPassthrough_AtEveryInputAndDayNight()
        {
            var p = NorthAtlantic(strength: 0f);
            Vector3[] samples =
            {
                V(0f, 0f, 0f), V(0.02f, 0.05f, 0.08f), V(0.5f, 0.7f, 0.7f), V(1f, 1f, 1f), V(0.9f, 0.2f, 0.1f),
            };
            foreach (var c in samples)
                foreach (float dn in new[] { 1f, 0.6f, 0.2f, 0.02f })
                {
                    Vector3 outc = WaterPaletteGrade.Grade(c, p, dn);
                    Assert.AreEqual(c.x, outc.x, 1e-6f, "strength 0 must not change R (exact today's look)");
                    Assert.AreEqual(c.y, outc.y, 1e-6f, "strength 0 must not change G");
                    Assert.AreEqual(c.z, outc.z, 1e-6f, "strength 0 must not change B");
                }
        }

        // ===== VALUE FLOOR: never muddy in DAYLIGHT ========================================================

        [Test]
        public void Floor_LiftsAMuddyColour_TowardThePaletteFloor_InDaylight()
        {
            // A too-dark (muddy) water colour, full daylight (dayNightLuma = 1): after the grade the on-screen
            // luminance must reach ~the palette floor (the day-floor pre-comp is ~identity when dayNightLuma=1).
            var p = NorthAtlantic(strength: 1f);   // strength 1 so the floor fully applies (isolate the clamp)
            Vector3 muddy = V(0.01f, 0.02f, 0.03f);
            Vector3 graded = WaterPaletteGrade.Grade(muddy, p, dayNightLuma: 1f);
            // strength 1 applies the full grade (floor + sat + a soft pull); the floor guarantees the value is
            // at LEAST the palette floor (the pull toward the deep anchor can only sit near/above it here).
            Assert.GreaterOrEqual(Luma(graded), p.ValueFloor - 1e-3f,
                "in daylight a muddy colour is lifted to at least the palette value floor (no mud)");
        }

        [Test]
        public void FloorPreCompensation_LandsAtPaletteFloor_AfterTheDownstreamMultiply()
        {
            // The day/night overlay multiplies the frame by dayNightLuma AFTER the water renders. The shader
            // floors the water's PRE-overlay value at ValueFloorDayNight; the on-screen value is that × dnLuma.
            // In daylight (dnLuma ≈ 1) the on-screen floor must land at ~paletteFloor (never muddy).
            float paletteFloor = 0.10f, nightFloor = 0f;
            foreach (float dn in new[] { 1f, 0.85f, 0.6f })
            {
                float pre = WaterPaletteGrade.ValueFloorDayNight(paletteFloor, dn, nightFloor);
                float onScreen = pre * dn;
                // Either we hit the floor exactly, OR the pre-comp saturated at 1 (only when paletteFloor/dn>1,
                // i.e. dn < paletteFloor — not in this daylight range) — so here it must equal the floor.
                Assert.AreEqual(paletteFloor, onScreen, 1e-3f,
                    $"daylight pre-compensation lands on-screen at the palette floor (dnLuma={dn})");
            }
        }

        [Test]
        public void Floor_AllowsTrueNight_ToGoGenuinelyDark()
        {
            // At deep night (small dayNightLuma) with NightFloor = 0, the LEGACY (knee-0) pre-comp floor
            // saturates at 1, so the overlay still darkens the water to genuinely dark — the owner's
            // dark-nights vision is preserved. (The 3-param overload IS the knee-0 legacy curve.)
            float paletteFloor = 0.10f, nightFloor = 0f;
            float deepNight = 0.04f;   // a dark-blue night tint luminance
            float pre = WaterPaletteGrade.ValueFloorDayNight(paletteFloor, deepNight, nightFloor);
            Assert.AreEqual(1f, pre, 1e-4f,
                "at true night the LEGACY pre-comp floor saturates at 1 (water full-bright pre-overlay)");
            float onScreen = pre * deepNight;
            Assert.LessOrEqual(onScreen, 0.06f,
                "after the overlay multiply, true-night water is still genuinely dark (no muddy lift)");

            // The production curve (the shipped knee) is DARKER still at deep night — the knee can only
            // ever lower the day floor, never raise it.
            float preKnee = WaterPaletteGrade.ValueFloorDayNight(
                paletteFloor, deepNight, nightFloor, WaterPaletteGrade.DefaultFloorDayKnee);
            Assert.LessOrEqual(preKnee, pre + 1e-4f, "the knee never raises the floor above the legacy curve");
            Assert.LessOrEqual(preKnee * deepNight, 0.06f, "kneed deep night stays genuinely dark");
        }

        // ===== the floor's DAY KNEE (owner playtest 2026-07-23 — the dusk white-out fix) ==================

        [Test]
        public void FloorKnee_HoldsDaylightAndOvercast_ExactlyAtTheShippedCurve()
        {
            // At/above the knee the divisor is dn itself, so the kneed curve == the legacy curve bit-for-bit:
            // every daylight/overcast look ships unchanged.
            float paletteFloor = 0.08f, nightFloor = 0f;
            foreach (float dn in new[] { 1f, 0.85f, 0.6f, WaterPaletteGrade.DefaultFloorDayKnee })
            {
                float legacy = WaterPaletteGrade.ValueFloorDayNight(paletteFloor, dn, nightFloor);
                float kneed = WaterPaletteGrade.ValueFloorDayNight(
                    paletteFloor, dn, nightFloor, WaterPaletteGrade.DefaultFloorDayKnee);
                Assert.AreEqual(legacy, kneed, 1e-6f,
                    $"at dnLuma={dn} (daylight/overcast) the kneed floor must equal the shipped curve exactly");
            }
        }

        [Test]
        public void FloorKnee_RidesTheOnScreenFloorDown_ThroughDusk()
        {
            // BELOW the knee the divisor holds at the knee, so the pre-overlay floor stops growing and the
            // ON-SCREEN floor (pre × dnLuma) scales down with the scene — the dusk sea darkens WITH the
            // world instead of holding daylight-floor brightness (the 2026-07-23 "whole sea becomes white").
            float paletteFloor = 0.08f, nightFloor = 0f;
            float knee = WaterPaletteGrade.DefaultFloorDayKnee;
            float duskStorm = 0.167f;   // the dusk-storm repro tint luma
            float dusk = 0.335f;        // the dusk-calm repro tint luma

            float preConst = paletteFloor / knee;   // the pre-overlay floor holds at floor/knee below the knee
            foreach (float dn in new[] { dusk, duskStorm, 0.08f })
            {
                float pre = WaterPaletteGrade.ValueFloorDayNight(paletteFloor, dn, nightFloor, knee);
                Assert.AreEqual(preConst, pre, 1e-5f,
                    $"below the knee the pre-overlay floor holds constant at floor/knee (dn={dn})");
                Assert.Less(pre * dn, paletteFloor - 1e-4f,
                    $"the ON-SCREEN floor at dn={dn} must sit BELOW the daylight floor — dusk darkens with the scene");
            }
            // The legacy curve is exactly what the fix retired: at the dusk-storm tint it held the on-screen
            // floor at daylight brightness (the flattening clamp of the white-out repro).
            float legacyPre = WaterPaletteGrade.ValueFloorDayNight(paletteFloor, duskStorm, nightFloor);
            Assert.AreEqual(paletteFloor, legacyPre * duskStorm, 1e-3f,
                "the legacy curve held dusk at the DAYLIGHT floor — the defect this knee exists to fix");
        }

        [Test]
        public void FloorKnee_ZeroIsTheLegacyCurve_AndTheNightFloorKeepsItsSaturatingDivide()
        {
            float paletteFloor = 0.10f;
            // Knee 0 = the pre-fix curve EXACTLY, at every dnLuma (the passthrough contract).
            foreach (float dn in new[] { 1f, 0.6f, 0.3f, 0.1f, 0.03f })
                Assert.AreEqual(
                    WaterPaletteGrade.ValueFloorDayNight(paletteFloor, dn, 0f),
                    WaterPaletteGrade.ValueFloorDayNight(paletteFloor, dn, 0f, 0f), 1e-6f,
                    $"FloorKnee = 0 must be the exact legacy curve (dn={dn})");

            // The NIGHT floor is untouched by the knee: its job is to SURVIVE deep night, so it keeps the
            // saturating divide and still lands on-screen at the requested value.
            float nightFloor = 0.03f, deepNight = 0.1f;
            float pre = WaterPaletteGrade.ValueFloorDayNight(
                paletteFloor, deepNight, nightFloor, WaterPaletteGrade.DefaultFloorDayKnee);
            Assert.GreaterOrEqual(pre * deepNight, nightFloor - 1e-3f,
                "a positive night floor still lands on-screen at the requested night value under the knee");
        }

        [Test]
        public void NightFloor_KeepsAFaintReadableSea_WhenTheOwnerAsksForIt()
        {
            // The night floor sets a minimum ON-SCREEN luminance, achievable up to the overlay's own ceiling
            // (you can't make the sea brighter than full-bright-water × the overlay). At deep night the
            // daylight pre-comp already saturates to 1, so the on-screen value is the overlay's own dn; a
            // night floor below that is satisfied automatically.
            float paletteFloor = 0.10f, nightFloor = 0.03f, deepNight = 0.04f;
            float preDeep = WaterPaletteGrade.ValueFloorDayNight(paletteFloor, deepNight, nightFloor);
            float onScreenDeep = preDeep * deepNight;
            Assert.GreaterOrEqual(onScreenDeep, nightFloor - 1e-3f,
                "with a positive night floor the on-screen deep-night sea keeps at least that floor");
            // ...and deep night is STILL darker than daylight (the night floor doesn't un-darken night).
            float dayOnScreen = WaterPaletteGrade.ValueFloorDayNight(paletteFloor, 1f, nightFloor) * 1f;
            Assert.Less(onScreenDeep, dayOnScreen, "a night floor keeps a faint sea but night is still darker than day");

            // In the TWILIGHT band (dayPre < 1) the night floor actively LIFTS the pre-comp above the
            // zero-night-floor case — this is where the knob bites. dn = 0.5 > paletteFloor so dayPre = 0.2 < 1.
            float twilight = 0.5f;
            float preNo = WaterPaletteGrade.ValueFloorDayNight(paletteFloor, twilight, /*nightFloor*/0f);
            float preYes = WaterPaletteGrade.ValueFloorDayNight(paletteFloor, twilight, /*nightFloor*/0.2f);
            Assert.Greater(preYes, preNo,
                "in the twilight band a positive night floor raises the pre-comp floor above the zero-night-floor case");
            Assert.AreEqual(0.2f, preYes * twilight, 1e-3f,
                "the twilight night floor lands on-screen at ~the requested night floor after the overlay multiply");
        }

        // ===== VALUE CEILING: cap blowout =================================================================

        [Test]
        public void Ceiling_CapsABlownOutColour_InDaylight()
        {
            var p = NorthAtlantic(strength: 1f);
            Vector3 blown = V(1f, 1f, 1f);   // a washed-out white (e.g. over-bright specular + reflection)
            Vector3 graded = WaterPaletteGrade.Grade(blown, p, dayNightLuma: 1f);
            Assert.LessOrEqual(Luma(graded), p.ValueCeil + 1e-3f,
                "a blown-out colour is capped to at most the palette value ceiling (no wash-out)");
            Assert.Less(Luma(graded), Luma(blown),
                "the ceiling actually darkened the over-bright input");
        }

        // ===== SATURATION CAP =============================================================================

        [Test]
        public void SatCap_PullsAnOverSaturatedColourTowardGrey_PreservingLuminance()
        {
            // Pure saturation maths (isolate it from the floor/ceil/pull by clamping value into range).
            Vector3 garish = V(0.9f, 0.1f, 0.05f);
            float before = WaterPaletteGrade.SaturationOf(garish);
            Vector3 capped = WaterPaletteGrade.CapSaturation(garish, 0.5f);
            float after = WaterPaletteGrade.SaturationOf(capped);
            Assert.Less(after, before, "an over-saturated colour is desaturated by the cap");
            Assert.AreEqual(0.5f, after, 1e-2f, "the cap pulls saturation down to ~the cap value");
            Assert.AreEqual(Luma(garish), Luma(capped), 1e-2f,
                "the saturation cap preserves luminance (only desaturates, doesn't darken)");
        }

        [Test]
        public void SatCap_IsANoOp_WhenAlreadyWithinTheCap()
        {
            Vector3 calm = V(0.30f, 0.40f, 0.42f);   // a low-sat cold colour, already within cap
            Vector3 capped = WaterPaletteGrade.CapSaturation(calm, 0.55f);
            Assert.AreEqual(calm.x, capped.x, 1e-5f, "a within-cap colour is unchanged (R)");
            Assert.AreEqual(calm.y, capped.y, 1e-5f, "a within-cap colour is unchanged (G)");
            Assert.AreEqual(calm.z, capped.z, 1e-5f, "a within-cap colour is unchanged (B)");
        }

        // ===== ANCHOR PULL: soft, by luminance ============================================================

        [Test]
        public void AnchorPull_IsSoft_StaysBetweenTheRawColourAndTheAnchor()
        {
            // A soft pull (0.35) leaves the colour mostly itself — it nudges toward the palette, never snaps to it.
            var p = NorthAtlantic(strength: 1f);
            Vector3 raw = V(0.20f, 0.36f, 0.40f);   // a mid-water teal
            Vector3 graded = WaterPaletteGrade.Grade(raw, p, dayNightLuma: 1f);
            Vector3 anchor = WaterPaletteGrade.AnchorForLuma(Luma(graded), p);
            // distance to anchor should be smaller than raw's, but graded must NOT equal the anchor (soft rail).
            float dRaw = (raw - anchor).magnitude;
            float dGraded = (graded - anchor).magnitude;
            Assert.Less(dGraded, dRaw + 1e-3f, "the pull moves the colour TOWARD the palette anchor");
            Assert.Greater(dGraded, 1e-3f, "the pull is SOFT — it does not snap the colour onto the anchor (a rail, not a cage)");
        }

        [Test]
        public void AnchorForLuma_BlendsContinuously_DarkToFoam()
        {
            // The anchor selector is continuous in luminance (no banding): a darker input pulls toward Deep,
            // a brighter one toward Foam, and a sweep is monotonic non-decreasing in luminance.
            var p = NorthAtlantic();
            float prev = -1f;
            for (float l = 0f; l <= 1f; l += 0.05f)
            {
                Vector3 a = WaterPaletteGrade.AnchorForLuma(l, p);
                float al = Luma(a);
                Assert.GreaterOrEqual(al, prev - 1e-3f, "anchor luminance is non-decreasing across the luma sweep");
                Assert.That(al, Is.InRange(Luma(p.Deep) - 1e-3f, Luma(p.Foam) + 1e-3f),
                    "every anchor sits between the deep and foam anchors");
                prev = al;
            }
        }

        // ===== STRENGTH scales the whole grade back toward the raw colour =================================

        [Test]
        public void Strength_ScalesTheGradeBetweenIdentityAndFull()
        {
            // The master strength lerps the whole grade between the raw colour (0) and the full grade (1):
            // a partial strength sits strictly between, so the owner dials how hard the rail bites.
            var pFull = NorthAtlantic(strength: 1f);
            var pHalf = NorthAtlantic(strength: 0.5f);
            Vector3 muddy = V(0.01f, 0.02f, 0.03f);
            Vector3 full = WaterPaletteGrade.Grade(muddy, pFull, 1f);
            Vector3 half = WaterPaletteGrade.Grade(muddy, pHalf, 1f);
            // half is the midpoint of raw and full (the grade is a single lerp by strength).
            Vector3 mid = Vector3.Lerp(muddy, full, 0.5f);
            Assert.AreEqual(mid.x, half.x, 1e-4f, "half strength = midpoint of raw and full grade (R)");
            Assert.AreEqual(mid.y, half.y, 1e-4f, "half strength = midpoint (G)");
            Assert.AreEqual(mid.z, half.z, 1e-4f, "half strength = midpoint (B)");
            Assert.Greater(Luma(half), Luma(muddy), "even a soft strength still lifts mud somewhat");
        }
    }
}
