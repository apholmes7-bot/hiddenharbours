using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The SPRITE LIGHT RESPONSE — <see cref="SpriteLightMath"/>, the headless twin of
    /// <c>Assets/_Project/Art/Shaders/Include/SpriteLightResponse.hlsl</c>. Every assert that could be
    /// decoration carries a MEASURED SABOTAGE beside it, in the shape
    /// <c>TreeRigBakeTests</c> established: break the thing on purpose, report how much of the sprite
    /// moved, and require the break to be large. An assert nobody can fail is an assert nobody needs.
    ///
    /// <para><b>The oracle.</b> The rig writes mask R as <c>max(0, N·K)^1.35</c> against its own fixed
    /// key. <see cref="SpriteLightMath.KeyTerm"/> is that same function for an arbitrary light — so
    /// feeding it the rig's key against the decoded NORMAL sheet must reproduce the baked MASK sheet.
    /// The shipped bake is therefore the test oracle for the whole decode convention at once: the
    /// channel assignment, the exponent, and — the one that would otherwise ship silently wrong — the
    /// normal sheet's <b>green channel y-flip</b>.</para>
    ///
    /// <para>Reads the committed PNGs as raw bytes through <see cref="ImageConversion.LoadImage"/>
    /// rather than <c>GetPixels</c> on the imported asset (which would need Read/Write enabled) — so
    /// this measures THE COMMITTED PIXELS, not an import of them. No GPU: byte arithmetic and pure
    /// functions only, nothing to gate on a Null Device.</para>
    /// </summary>
    public class SpriteLightResponseTests
    {
        // The species the tree-bake handoff measured §8.3 against, so the numbers logged here can be
        // compared with the ones already recorded there.
        private const string Species = "RedSpruce";
        private const string Stage = "mature";
        private const string Season = "summer";

        private static TreeKitCatalog.Contract _contract;

        [SetUp]
        public void LoadContract()
        {
            _contract = TreeKitCatalog.Load();
            Assert.IsNotNull(_contract,
                $"No contract at {TreeKitCatalog.ContractPath} — the tree kit is not baked, so there " +
                "is nothing to validate the light response against.");
        }

        // =================================================================================
        // 🔴 THE MASK CHANNEL ORDER, PINNED AT THE POINT OF CONSUMPTION
        // =================================================================================

        [Test]
        public void MaskChannelOrder_IsKeyRimDepthCoverage_AtTheConsumptionPoint()
        {
            // TreeRigBakeTests pins this at the BAKE. It has to be pinned again HERE because that is
            // where the mistake actually happens: every public description of this technique says
            // "green = front, blue = rim", so a ported snippet swaps two channels and produces
            // something subtly wrong rather than obviously broken.
            Assert.AreEqual(0, SpriteLightMath.MaskKey, "R is the KEY light.");
            Assert.AreEqual(1, SpriteLightMath.MaskRim, "G is the BACK RIM.");
            Assert.AreEqual(2, SpriteLightMath.MaskDepth, "B is DEPTH.");
            Assert.AreEqual(3, SpriteLightMath.MaskCoverage, "A is COVERAGE.");

            // Behavioural, not just numeric: a mask with ONLY R set must produce key and no rim, and a
            // mask with ONLY G set the reverse. A swapped consumer fails both of these.
            Vector3 n = new Vector3(0f, 0f, 1f);
            Vector3 front = SpriteLightMath.LightViewDirection(new Vector2(0f, -1f), 0.5f);   // south, in front
            Vector3 behind = SpriteLightMath.LightViewDirection(new Vector2(0f, 1f), 0.1f);   // north, behind

            float keyOnly = SpriteLightMath.KeyResponse(1f, 1f, 1f, n, front, 0f, SpriteLightMath.DefaultFrontBand, 0f);
            float rimFromKeyOnly = SpriteLightMath.RimResponse(0f, 1f, 1f, n, behind, 0f, SpriteLightMath.DefaultFrontBand, 0f);
            Assert.Greater(keyOnly, 0.5f, "mask R alone must light the KEY term.");
            Assert.AreEqual(0f, rimFromKeyOnly, 1e-6f, "mask R must not leak into the RIM term.");

            float rimOnly = SpriteLightMath.RimResponse(1f, 1f, 1f, n, behind, 0f, SpriteLightMath.DefaultFrontBand, 0f);
            float keyFromRimOnly = SpriteLightMath.KeyResponse(0f, 1f, 1f, n, front, 0f, SpriteLightMath.DefaultFrontBand, 0f);
            Assert.Greater(rimOnly, 0.5f, "mask G alone must light the RIM term.");
            Assert.AreEqual(0f, keyFromRimOnly, 1e-6f, "mask G must not leak into the KEY term.");
        }

        [Test]
        public void SabotageRedGreenSwap_MovesAMeasuredShareOfTheRealSprite()
        {
            // MEASURED SABOTAGE. Consume the real Red Spruce mask with R and G swapped — the exact
            // mistake a ported snippet makes — and report how much of the covered sprite changes.
            var (mask, normal, _, _) = LoadSheets();

            Vector3 lightDir = SpriteLightMath.LightViewDirection(new Vector2(0.3f, -0.95f), 0.35f);
            int covered = 0, moved = 0;
            double sumDelta = 0, worst = 0;

            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i].a == 0) continue;
                covered++;
                Vector3 n = SpriteLightMath.DecodeNormal(normal[i]);
                float r = mask[i].r / 255f, g = mask[i].g / 255f, b = mask[i].b / 255f, a = mask[i].a / 255f;

                float ok = SpriteLightMath.KeyResponse(r, b, a, n, lightDir, 0f, SpriteLightMath.DefaultFrontBand, 0.3f)
                         + SpriteLightMath.RimResponse(g, b, a, n, lightDir, 0.8f, SpriteLightMath.DefaultFrontBand, 0.3f);
                float swapped = SpriteLightMath.KeyResponse(g, b, a, n, lightDir, 0f, SpriteLightMath.DefaultFrontBand, 0.3f)
                              + SpriteLightMath.RimResponse(r, b, a, n, lightDir, 0.8f, SpriteLightMath.DefaultFrontBand, 0.3f);

                double d = Math.Abs(ok - swapped);
                sumDelta += d;
                if (d > 1.0 / 255.0) moved++;
                worst = Math.Max(worst, d);
            }

            Assert.Greater(covered, 1000, "too few covered pixels to measure anything");
            double pct = 100.0 * moved / covered;
            Debug.Log($"[sprite-light] R↔G swap at CONSUMPTION: {moved}/{covered} covered px change " +
                      $"({pct:F2}%), mean |Δ| {sumDelta / covered:F4}, worst {worst:F4}. " +
                      "Measured 2026-07-29: 21628/31001 px = 69.77%, mean 0.2566. (The bake's own " +
                      "R↔G sabotage moved 29.60% of the cell.)");
            Assert.Greater(pct, 10.0,
                "A channel swap must change a LOT of the sprite, or the order does not matter and " +
                "this whole guard is decoration.");
        }

        // =================================================================================
        // ⭐ THE ORACLE — the bake validates the decode
        // =================================================================================

        [Test]
        public void TheLiveKeyTerm_ReproducesTheBakedMask_WhenFedTheRigsOwnKey()
        {
            // The rig writes mask R = pow(max(0, N·K), 1.35) with K = its fixed upper-left key. So the
            // SAME function, fed the SAME direction and the decoded normal sheet, must give the baked
            // bytes back. Anything wrong in the decode — most dangerously the normal sheet's green
            // y-flip — fails this, and only this.
            var (mask, normal, _, _) = LoadSheets();

            var errors = new List<double>();
            int compared = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (normal[i].a == 0) continue;                    // the keyline ring: no normal, no oracle
                Vector3 n = SpriteLightMath.DecodeNormal(normal[i]);
                double live = SpriteLightMath.KeyTerm(n, SpriteLightMath.RigKeyDirection);
                errors.Add(Math.Abs(live - mask[i].r / 255.0));
                compared++;
            }

            Assert.Greater(compared, 1000, "too few normal-covered pixels to validate against");
            errors.Sort();
            double mean = errors.Average();
            double p99 = errors[(int)(errors.Count * 0.99)];
            double max = errors[errors.Count - 1];

            Debug.Log($"[sprite-light] ORACLE — live key vs baked mask R over {compared} px of " +
                      $"{Species}: mean {mean * 255:F2}/255, p99 {p99 * 255:F2}/255, max {max * 255:F2}/255. " +
                      "The gap is 8-bit quantisation of the normal, nothing else. " +
                      "Measured 2026-07-29: 0.32 / 1.25 / 1.73.");

            Assert.Less(mean, 4.0 / 255.0,
                "The recomputed key light does not match what the rig baked. Suspect (in order): the " +
                "normal sheet's GREEN CHANNEL Y-FLIP (normalView writes -ny, so a decoded normal is " +
                "y-UP and SpriteLightMath.RigKeyDirection flips sign with it), the exponent 1.35, or " +
                "the channel assignment.");
            Assert.Less(p99, 12.0 / 255.0, "the tail is too heavy for pure quantisation error");
        }

        [Test]
        public void SabotageNormalYFlip_BreaksTheOracle_ByAMeasuredMargin()
        {
            // MEASURED SABOTAGE for the trap above: decode the normal WITHOUT accounting for the rig's
            // y-flip (i.e. pretend green is screen-DOWN, which is what the rig's own internal `ny` is).
            // The tree would then light from below. This is the failure this suite exists to catch.
            var (mask, normal, _, _) = LoadSheets();

            double sumOk = 0, sumFlipped = 0;
            int compared = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (normal[i].a == 0) continue;
                Vector3 n = SpriteLightMath.DecodeNormal(normal[i]);
                var wrong = new Vector3(n.x, -n.y, n.z);

                double baked = mask[i].r / 255.0;
                sumOk += Math.Abs(SpriteLightMath.KeyTerm(n, SpriteLightMath.RigKeyDirection) - baked);
                sumFlipped += Math.Abs(SpriteLightMath.KeyTerm(wrong, SpriteLightMath.RigKeyDirection) - baked);
                compared++;
            }

            double ok = sumOk / compared * 255.0, bad = sumFlipped / compared * 255.0;
            Debug.Log($"[sprite-light] Y-FLIP sabotage: mean error {ok:F2}/255 correct vs {bad:F2}/255 " +
                      $"flipped — {bad / Math.Max(ok, 1e-6):F0}x worse over {compared} px. " +
                      "Measured 2026-07-29: 0.32 vs 111.09 = 351x.");
            Assert.Greater(bad, ok * 8.0,
                "Flipping the normal's y must be dramatically worse than not flipping it. If it is " +
                "not, this suite cannot tell a y-up decode from a y-down one and the oracle is blind.");
        }

        [Test]
        public void SabotageKeyExponent_BreaksTheOracle()
        {
            // The 1.35 is the rig's, not a taste value: a plain Lambert (1.0) is measurably a
            // different curve, so the exponent cannot drift unnoticed.
            var (mask, normal, _, _) = LoadSheets();

            double sumOk = 0, sumLambert = 0;
            int compared = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (normal[i].a == 0) continue;
                Vector3 n = SpriteLightMath.DecodeNormal(normal[i]);
                double baked = mask[i].r / 255.0;
                double dot = Math.Max(0f, Vector3.Dot(n, SpriteLightMath.RigKeyDirection));
                sumOk += Math.Abs(Math.Pow(dot, SpriteLightMath.KeyExponent) - baked);
                sumLambert += Math.Abs(dot - baked);
                compared++;
            }

            double ok = sumOk / compared * 255.0, lambert = sumLambert / compared * 255.0;
            Debug.Log($"[sprite-light] EXPONENT sabotage: mean error {ok:F2}/255 at 1.35 vs " +
                      $"{lambert:F2}/255 at a plain Lambert, over {compared} px. " +
                      "Measured 2026-07-29: 0.32 vs 14.19.");
            Assert.Greater(lambert, ok * 3.0, "1.35 must be measurably the rig's curve, not a preference.");
        }

        // =================================================================================
        // 🔴 THE KEYLINE — coverage without a normal
        // =================================================================================

        [Test]
        public void TheKeylineRing_HasCoverageButNoNormal_AndTheResponseNeverInventsLightThere()
        {
            // The measured asymmetry from the bake (§8.3): the rig's 1 px keyline ring is opaque in the
            // albedo and so in the mask's A, but has NO surface normal. Two things must hold, and the
            // second is the one a naive consumer gets wrong: the ring exists in the mask, and a
            // decoded (0,0,0,0) normal must NOT be normalised into a fabricated direction that lights
            // the outline from wherever it happens to point.
            var (mask, normal, _, _) = LoadSheets();

            int ring = 0, litRing = 0;
            Vector3 sweepWorst = Vector3.zero;
            float worstLit = 0f;

            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i].a == 0 || normal[i].a != 0) continue;
                ring++;

                Assert.AreEqual(Vector3.zero, SpriteLightMath.DecodeNormal(normal[i]),
                    "A texel with no normal coverage must decode to zero, never to a normalised " +
                    "(-1,-1,-1) that points somewhere plausible.");

                // Sweep the light all the way round: no direction may light this texel.
                for (int deg = 0; deg < 360; deg += 30)
                {
                    float rad = deg * Mathf.Deg2Rad;
                    Vector3 l = SpriteLightMath.LightViewDirection(
                        new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)), 0.3f);
                    float m = mask[i].r / 255f, g = mask[i].g / 255f, b = mask[i].b / 255f, a = mask[i].a / 255f;
                    float lit = SpriteLightMath.KeyResponse(m, b, a, Vector3.zero, l, 1f, SpriteLightMath.DefaultFrontBand, 0.3f)
                              + SpriteLightMath.RimResponse(g, b, a, Vector3.zero, l, 0.8f, SpriteLightMath.DefaultFrontBand, 0.3f);
                    if (lit > worstLit) { worstLit = lit; sweepWorst = l; }
                    if (lit > 1e-4f) litRing++;
                }
            }

            Assert.Greater(ring, 0,
                "No mask-covered texel lacks a normal — the rig stopped drawing the keyline. That is " +
                "a look change, not a bake bug, but this guard is now testing nothing.");
            Debug.Log($"[sprite-light] keyline ring: {ring} px with coverage and no normal; brightest " +
                      $"response over a full 360° light sweep = {worstLit:E2} (dir {sweepWorst}). " +
                      "Measured 2026-07-29: 2564 ring px over the 4-variant sheet, brightest 0. " +
                      "(The bake measured 611 px on ONE cell.)");
            Assert.AreEqual(0, litRing,
                "The keyline lit up. It carries coverage but no surface, so its mask R and G are 0 " +
                "and it must stay the deliberate dark outline the rig drew — light the keyline from " +
                "the MASK, never from a reconstructed normal.");
        }

        [Test]
        public void TheThreeSheets_ShareOneUvSpace_SoOneAtlasUvHitsAllThree()
        {
            // The shader samples the mask and the normal at the ALBEDO's uv through the ALBEDO's mesh.
            // That is only sound while all three sheets are the same size with the same cell layout —
            // which is a bake property, not a law, so it is asserted rather than assumed. It also
            // covers §2.5's multi-cell trap from the other side: raw atlas uv is fine BECAUSE the three
            // sheets share it; nothing here needs a per-sprite 0..1 fraction.
            foreach (var e in _contract.trees)
            foreach (string season in e.seasons)
            {
                var dims = TreeKitCatalog.Channels
                    .Select(c => (c, path: TreeKitCatalog.SheetPath(e.species, e.stage, season, c)))
                    .Select(t => (t.c, t.path, size: PngSize(t.path)))
                    .ToArray();

                var albedo = dims.First(d => d.c == TreeKitCatalog.Channel.Albedo);
                Assert.AreEqual(e.sheetW, albedo.size.w, $"{e.species}: albedo width vs contract");
                Assert.AreEqual(e.sheetH, albedo.size.h, $"{e.species}: albedo height vs contract");

                foreach (var d in dims)
                    Assert.AreEqual(albedo.size, d.size,
                        $"{e.species} {d.c}: {d.size.w}x{d.size.h} against the albedo's " +
                        $"{albedo.size.w}x{albedo.size.h}. The shader samples all three at ONE atlas " +
                        "uv; a different sheet size silently offsets the mask and normal from the art.");
            }
        }

        // =================================================================================
        // FRONT vs BEHIND — the decision the reference makes with a gradient sprite
        // =================================================================================

        [Test]
        public void LightViewDirection_UsesTheSharedBakeCamera_AndPutsTheNorthBehind()
        {
            // The published camera constants, from the direction that matters: a metre of HEIGHT draws
            // 0.766 tall and a NORTHWARD metre draws 0.643 up the screen (kit README; ADR 0006/0022).
            Vector3 overhead = SpriteLightMath.LightViewDirection(Vector2.zero, 1f);
            Assert.AreEqual(0f, overhead.x, 1e-5f);
            Assert.AreEqual(0.76604f, overhead.y, 1e-4f, "a light straight up draws at cos 40 up-screen");
            Assert.AreEqual(0.64279f, overhead.z, 1e-4f, "and leans toward the viewer by sin 40");

            Vector3 north = SpriteLightMath.LightViewDirection(new Vector2(0f, 1f), 0f);
            Assert.AreEqual(0.64279f, north.y, 1e-4f, "a northward metre draws sin 40 up-screen");
            Assert.Less(north.z, -0.7f, "NORTH is AWAY from the camera — the light is BEHIND the sprite");

            Vector3 south = SpriteLightMath.LightViewDirection(new Vector2(0f, -1f), 0f);
            Assert.Greater(south.z, 0.7f, "SOUTH is toward the camera — the light is IN FRONT");

            // Frontness is the whole front/behind decision, and it must be monotonic and symmetric.
            Assert.AreEqual(1f, SpriteLightMath.Frontness(south.z, SpriteLightMath.DefaultFrontBand), 1e-5f);
            Assert.AreEqual(0f, SpriteLightMath.Frontness(north.z, SpriteLightMath.DefaultFrontBand), 1e-5f);
            Assert.AreEqual(0.5f, SpriteLightMath.Frontness(0f, SpriteLightMath.DefaultFrontBand), 1e-5f, "on the sprite's plane: half and half");
        }

        [Test]
        public void TheShippedSunNeverGoesBehind_SoTheBandIsWhatBuysTheGrazingRim()
        {
            // A load-bearing consequence of this world's geometry that the design doc could not have
            // known: DayNightMath keeps the sun in the SOUTH all day (northern hemisphere, camera
            // looking north), so its view z is positive from dawn to dusk and a rim gated purely on
            // "the light is behind" would be dead for the sun forever. The changeover BAND is what
            // gives a low sun a grazing rim — so the band is a look decision, not a smoothing detail.
            var profile = DayNightProfile.CreateDefault();
            float lowestZ = float.MaxValue, noonZ = 0f;

            for (float hour = 0f; hour < 24f; hour += 0.25f)
            {
                Vector2 g = DayNightMath.SunDirection(hour, profile.SunriseHour, profile.SunsetHour,
                                                      profile.ShadowSouthBias, profile.ShadowNoonLift);
                float elev = DayNightMath.SunElevation(hour, profile.SunriseHour, profile.SunsetHour);
                if (elev <= 0f) continue;                                  // sun down: the term is gated off
                float z = SpriteLightMath.LightViewDirection(g, elev).z;
                lowestZ = Mathf.Min(lowestZ, z);
                if (Mathf.Abs(elev - 1f) < 0.01f) noonZ = z;
            }

            Debug.Log($"[sprite-light] daylight sun view z: {lowestZ:F3} at the horizon .. {noonZ:F3} at noon. " +
                      $"Frontness at the horizon with the shipped 0.35 band = " +
                      $"{SpriteLightMath.Frontness(lowestZ, SpriteLightMath.DefaultFrontBand):F2}.");

            Assert.Greater(lowestZ, 0f,
                "The sun went BEHIND the sprites. If DayNightMath ever moves the sun north of the " +
                "camera this test is the place to re-reason about the rim, not the shader.");
            Assert.Less(SpriteLightMath.Frontness(lowestZ, SpriteLightMath.DefaultFrontBand), 0.95f,
                "At the shipped band a horizon sun must leave some rim, or dawn and dusk read exactly " +
                "like noon and the whole term is inert.");
            Assert.AreEqual(1f, SpriteLightMath.Frontness(noonZ, SpriteLightMath.DefaultFrontBand), 1e-4f,
                "A noon sun must be pure key: no rim from a light directly overhead and in front.");
        }

        [Test]
        public void SabotageRimOnTheSunSide_InvertsTheTermByAMeasuredMargin()
        {
            // MEASURED SABOTAGE: gate the rim on the light being IN FRONT instead of BEHIND — "flip the
            // rim to the sun side". Because the shipped sun is always front-ish, this does not shade
            // the rim slightly differently; it moves essentially the entire term.
            var (mask, normal, _, _) = LoadSheets();

            // Measured at the GRAZING hour — the last quarter-hour of daylight, where the sun sits
            // lowest and the rim actually lives. At noon frontness saturates to 1 and the correct term
            // is exactly 0, which would make this comparison vacuous rather than measured.
            var (l, hour) = GrazingSun();

            double sumOk = 0, sumFlipped = 0;
            int rimPixels = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i].a == 0 || mask[i].g == 0) continue;
                rimPixels++;
                Vector3 n = SpriteLightMath.DecodeNormal(normal[i]);
                float rim = mask[i].g / 255f, depth = mask[i].b / 255f, cov = mask[i].a / 255f;

                float steered = Mathf.Lerp(1f, n.sqrMagnitude > 1e-8f ? SpriteLightMath.RimSteer(n, l) : 1f, 0.8f);
                float shared = rim * steered * SpriteLightMath.DepthAttenuation(depth, 0.3f) * cov;
                float front = SpriteLightMath.Frontness(l.z, SpriteLightMath.DefaultFrontBand);

                sumOk += shared * (1f - front);                              // BEHIND — correct
                sumFlipped += shared * front;                                // the sabotage
            }

            Assert.Greater(rimPixels, 200, "too little baked rim to measure");
            double ok = sumOk / rimPixels, bad = sumFlipped / rimPixels;
            Debug.Log($"[sprite-light] RIM-SIDE sabotage at hour {hour:F2} (sun view z {l.z:F3}, " +
                      $"frontness {SpriteLightMath.Frontness(l.z, SpriteLightMath.DefaultFrontBand):F2}): mean rim {ok:F4} correct " +
                      $"vs {bad:F4} gated on the sun side, over {rimPixels} rim px — " +
                      $"{bad / Math.Max(ok, 1e-9):F1}x. " +
                      "Measured 2026-07-29 at hour 6.25: 0.0158 vs 0.0663 = 4.2x.");
            Assert.Greater(bad, ok * 1.5,
                "Gating the rim on the wrong side of the sprite must change it substantially. If the " +
                "two agree, the front/behind decision is doing no work and the rim is just a glow.");
        }

        [Test]
        public void WithNoCycleRunning_TheSunFallsBackToAPlausibleOne_SoTheOwnerCanTuneInEditMode()
        {
            // DayNightController installs at RUNTIME. In edit mode _SunDir/_SunElevation are unset, and
            // a light response that reads as dead in the scene view is one the owner cannot dial in —
            // the same reason the additive lights carry NightGateWithFallback's cycle-off branch.
            Vector3 fallback = SpriteLightMath.SunDirection(Vector2.zero, 0f, out float amount);
            Assert.Greater(amount, 0.5f, "an unset cycle must still give a lit tree, not a dead one");
            Assert.Greater(fallback.z, 0.3f, "and the fallback sun is IN FRONT, like the real one");
            Assert.Less(fallback.x, 0f, "and a little to the west, so the catch is not dead centre");

            // A published cycle must win, including a below-horizon sun taking the term to nothing.
            var profile = DayNightProfile.CreateDefault();
            Vector2 g = DayNightMath.SunDirection(12f, profile.SunriseHour, profile.SunsetHour,
                                                  profile.ShadowSouthBias, profile.ShadowNoonLift);
            SpriteLightMath.SunDirection(g, 0.9f, out float dayAmount);
            SpriteLightMath.SunDirection(g, -0.6f, out float nightAmount);
            Assert.AreEqual(0.9f, dayAmount, 1e-6f, "a published sun must be used, not the fallback");
            Assert.AreEqual(0f, nightAmount, 1e-6f,
                "below the horizon the SUN term is nothing — at night the moon reaches these trees " +
                "through the day/night tint's own moonlight lift, not through this term.");
        }

        [Test]
        public void RimSteer_PutsTheRimOnTheSideFacingTheLight_NotTheFarSide()
        {
            // A rim lights the part of the silhouette that faces the light, and on a silhouette the
            // normal lies in the screen plane. Same quantity the rig's own `back` term uses.
            Vector3 rightEdge = new Vector3(1f, 0f, 0f);
            Vector3 leftEdge = new Vector3(-1f, 0f, 0f);
            Vector3 fromTheRight = SpriteLightMath.LightViewDirection(new Vector2(1f, 0.6f), 0.1f);

            Assert.Greater(fromTheRight.x, 0.5f, "sanity: this light really is to the right");
            Assert.Greater(SpriteLightMath.RimSteer(rightEdge, fromTheRight), 0.8f,
                "the RIGHT edge faces a light from the right — that is where the rim goes");
            Assert.AreEqual(0f, SpriteLightMath.RimSteer(leftEdge, fromTheRight), 1e-6f,
                "the LEFT edge faces away from it and must stay dark");

            // An OVERHEAD light is not a degenerate case — the 40° camera projects "up" onto
            // screen-up, so it has a real lateral direction and rims the TOP of the silhouette rather
            // than either side. Worth pinning: the intuition that overhead means "no direction" is
            // wrong here, and acting on it would put the rim in the wrong place.
            Vector3 overhead = SpriteLightMath.LightViewDirection(Vector2.zero, 1f);
            Assert.Greater(overhead.y, 0.7f, "sanity: overhead reads as up-screen under this camera");
            Assert.AreEqual(1f, SpriteLightMath.RimSteer(new Vector3(0f, 1f, 0f), overhead), 1e-6f,
                "the TOP edge faces an overhead light");
            Assert.AreEqual(0f, SpriteLightMath.RimSteer(rightEdge, overhead), 1e-6f,
                "a side edge does not");

            // The genuinely degenerate cases pass the baked band through rather than snapping to black.
            Assert.AreEqual(1f, SpriteLightMath.RimSteer(new Vector3(0f, 0f, 1f), fromTheRight), 1e-6f,
                "a normal pointing straight at the camera has no lateral direction to steer by");
            Assert.AreEqual(1f, SpriteLightMath.RimSteer(rightEdge, new Vector3(0f, 0f, -1f)), 1e-6f,
                "nor does a light pointing straight away from it");
        }

        // =================================================================================
        // COVERAGE and DEPTH
        // =================================================================================

        [Test]
        public void SabotageZeroCoverage_SilencesTheEntireResponse()
        {
            // MEASURED SABOTAGE: zero mask A. Coverage is the channel that keeps the response inside
            // the sprite, so with it at 0 nothing may light — over the real sheet, at any light angle.
            var (mask, normal, _, _) = LoadSheets();

            double withCoverage = 0, withoutCoverage = 0;
            int covered = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i].a == 0) continue;
                covered++;
                Vector3 n = SpriteLightMath.DecodeNormal(normal[i]);
                Vector3 l = SpriteLightMath.LightViewDirection(new Vector2(-0.4f, -0.9f), 0.45f);
                float r = mask[i].r / 255f, g = mask[i].g / 255f, b = mask[i].b / 255f;

                withCoverage += SpriteLightMath.KeyResponse(r, b, 1f, n, l, 0.75f, SpriteLightMath.DefaultFrontBand, 0.3f)
                              + SpriteLightMath.RimResponse(g, b, 1f, n, l, 0.8f, SpriteLightMath.DefaultFrontBand, 0.3f);
                withoutCoverage += SpriteLightMath.KeyResponse(r, b, 0f, n, l, 0.75f, SpriteLightMath.DefaultFrontBand, 0.3f)
                                 + SpriteLightMath.RimResponse(g, b, 0f, n, l, 0.8f, SpriteLightMath.DefaultFrontBand, 0.3f);
            }

            Debug.Log($"[sprite-light] COVERAGE sabotage over {covered} px: total response " +
                      $"{withCoverage:F1} with A=1 vs {withoutCoverage:F1} with A=0. " +
                      "Measured 2026-07-29: 12698.1 to 0.0.");
            Assert.Greater(withCoverage, covered * 0.02,
                "With coverage the response must actually do something, or zeroing it proves nothing.");
            Assert.AreEqual(0.0, withoutCoverage, 1e-9, "A=0 must silence every term, everywhere.");
        }

        [Test]
        public void DepthAttenuation_ReadsBAsNearEqualsOne_TheWayTheRigWritesIt()
        {
            // The rig writes (z − zmin)/zrange and treats HIGHER z as occluding its neighbours, so
            // B = 1 is NEAR the camera. Getting this backwards inverts the form: the far side of the
            // canopy would catch a light the near side does not.
            Assert.AreEqual(1f, SpriteLightMath.DepthAttenuation(1f, 1f), 1e-6f, "near, full bias: unattenuated");
            Assert.AreEqual(0f, SpriteLightMath.DepthAttenuation(0f, 1f), 1e-6f, "far, full bias: nothing");
            Assert.AreEqual(1f, SpriteLightMath.DepthAttenuation(0f, 0f), 1e-6f, "bias 0: the channel is unused");
            Assert.Greater(SpriteLightMath.DepthAttenuation(0.9f, 0.5f),
                           SpriteLightMath.DepthAttenuation(0.2f, 0.5f),
                           "nearer must always catch more, at any bias");
        }

        [Test]
        public void Relight_MovesTheCatchWithTheLight_AndCollapsesToTheBakeWithoutANormal()
        {
            // The relight blend is the difference between "brighten what the rig lit" and "light this
            // for the sun that is actually up". At 1 the two must genuinely disagree; where there is
            // no normal it must fall back to the bake no matter what the blend says.
            Vector3 facingRight = new Vector3(0.8f, 0.2f, 0.56f).normalized;
            Vector3 fromLeft = SpriteLightMath.LightViewDirection(new Vector2(-0.9f, -0.4f), 0.2f);
            const float bakedKey = 0.9f;                                   // the rig lit this pixel brightly

            float baked = SpriteLightMath.KeyResponse(bakedKey, 1f, 1f, facingRight, fromLeft, 0f, SpriteLightMath.DefaultFrontBand, 0f);
            float live = SpriteLightMath.KeyResponse(bakedKey, 1f, 1f, facingRight, fromLeft, 1f, SpriteLightMath.DefaultFrontBand, 0f);
            Assert.Greater(baked, live * 2f,
                "A pixel facing RIGHT, lit from the LEFT, must go darker under relight than the bake " +
                "says — that swing is the entire point of consuming the normal sheet.");

            float noNormal = SpriteLightMath.KeyResponse(bakedKey, 1f, 1f, Vector3.zero, fromLeft, 1f, SpriteLightMath.DefaultFrontBand, 0f);
            float noNormalBaked = SpriteLightMath.KeyResponse(bakedKey, 1f, 1f, Vector3.zero, fromLeft, 0f, SpriteLightMath.DefaultFrontBand, 0f);
            Assert.AreEqual(noNormalBaked, noNormal, 1e-6f,
                "Without a normal the relight blend must be ignored entirely — 🔴 light the keyline " +
                "from the MASK, never the normal.");
        }

        // =================================================================================
        // the lamp shape — a transcription, so assert it against the ORIGINAL
        // =================================================================================

        [Test]
        public void TheLampShape_IsLightMathsOwnCone_NotASecondDefinition()
        {
            // SpriteLightResponse.hlsl transcribes LightMath.WaterConeTerm / NightGate rather than
            // inventing a second lamp. Assert against the original across the beam so a future edit to
            // either one shows up as a numeric disagreement here.
            const float range = 20f, edge = 0.4f;
            float cosHalf = LightMath.CosFromHalfAngleDeg(35f);
            float cosInner = LightMath.CosFromHalfAngleDeg(35f * 0.65f);

            float onAxis = LightMath.WaterConeTerm(0, 0, 0, 1, 0, 5, range, cosHalf, cosInner, edge);
            float offAxis = LightMath.WaterConeTerm(0, 0, 0, 1, 5, 0, range, cosHalf, cosInner, edge);
            float beyond = LightMath.WaterConeTerm(0, 0, 0, 1, 0, 25, range, cosHalf, cosInner, edge);

            Assert.Greater(onAxis, 0.4f, "a point on the beam axis, well inside the throw, is lit");
            Assert.AreEqual(0f, offAxis, 1e-6f, "90° off a 35° cone is outside it");
            Assert.AreEqual(0f, beyond, 1e-6f, "past the range there is no light");

            // The night gate: off in daylight, on in the dark, and SHOWN when no cycle is running so a
            // bare art scene can be tuned.
            Assert.AreEqual(0f, LightMath.NightGate(1f, 0.35f, 0.25f), 1e-5f, "full daylight: no lamp");
            Assert.AreEqual(1f, LightMath.NightGate(0.03f, 0.35f, 0.25f), 1e-5f, "deep night: full lamp");
            Assert.AreEqual(1f, LightMath.NightGateWithFallback(0f, 0.35f, 0.25f, cycleActive: false, 1f), 1e-5f);
        }

        [Test]
        public void TheLampRidesTheNightCompensation_SoItSurvivesCompleteDark()
        {
            // §2.3 decided PER TERM: the SUN's catch is added pre-grade and must dim with the world;
            // the LAMP's must survive it, so it rides LightMath's divide-by-tint. Assert the survival
            // property directly — compensated × tint back at the authored value — at the shipped
            // deepest-night tint, where an uncompensated add reads at ~3%.
            var deepNight = new Color(0.0216f, 0.0288f, 0.0612f, 1f);      // skyTint(.12,.16,.34) × the 0.18 floor
            var authored = new Color(0.9f, 0.75f, 0.45f, 1f);

            Color raw = authored * deepNight;                              // what the overlay does to a plain add
            Assert.Less(raw.r, authored.r * 0.05f, "sanity: the night really does crush an uncompensated add");

            Color comp = LightMath.CompensateForDayNightTint(
                authored, deepNight, LightMath.DayNightCompensationMinChannel);
            Color onScreen = comp * deepNight;
            Assert.AreEqual(authored.r, onScreen.r, 1e-4f, "the multiply must cancel exactly at deep night");
            Assert.AreEqual(authored.g, onScreen.g, 1e-4f);
            Assert.AreEqual(authored.b, onScreen.b, 1e-4f);

            // And the cycle-off branch: a near-black/unset tint means there is NO overlay to cancel.
            Color unset = LightMath.CompensateForDayNightTint(
                authored, new Color(0f, 0f, 0f, 1f), LightMath.DayNightCompensationMinChannel);
            Assert.AreEqual(authored, unset, "with no cycle running the term must pass through untouched");
        }

        // =================================================================================
        // the sheets actually reach the renderer
        // =================================================================================

        [Test]
        public void AConfiguredTree_HandsTheShaderBothSheetsAndTheChannelsFlag()
        {
            var placements = AcadianTreeCatalog.Scan();
            Assert.IsNotEmpty(placements);
            var material = AcadianTreeCatalog.LoadMaterial();

            var go = new GameObject("light-response-test-tree");
            try
            {
                var placement = placements.First(p => p.Species == Species);
                var sprite = AcadianTreeCatalog.LoadVariant(placement, 0);
                Assert.IsNotNull(sprite, $"{placement.Stem} is not sliced.");

                var sr = AcadianTreeCatalog.Configure(go, placement, sprite, material);
                var anchor = go.GetComponent<TreeTrunkAnchor>();

                Assert.IsTrue(anchor.HasLightSheets,
                    "Configure did not bind this species' baked mask and normal sheets. The response " +
                    "would be silently inert on every placed tree.");
                Assert.AreEqual(1f, TreeTrunkAnchor.LightChannelsOn(sr), 1e-6f,
                    "the CHANNELS flag is what the shader gates on — it must reach the renderer's block");

                // The block still carries what it carried before: adding textures must not have
                // dropped the anchor (the GET-modify-SET rule this component is built around).
                Assert.AreEqual(placement.Entry.trunkAnchor, TreeTrunkAnchor.AnchorOn(sr), 1e-6f,
                    "binding the sheets clobbered the trunk anchor already in the block");

                Texture mask = TreeTrunkAnchor.MaskOn(sr), normal = TreeTrunkAnchor.NormalOn(sr);
                Assert.IsNotNull(mask, "no mask texture on the renderer");
                Assert.IsNotNull(normal, "no normal texture on the renderer");
                Assert.AreNotSame(mask, normal, "the same sheet was bound twice");
                Assert.AreEqual(sprite.texture.width, mask.width,
                    "the mask must be the albedo's own size — the shader samples it at the albedo's uv");
                Assert.AreEqual(sprite.texture.height, normal.height);

                // Clearing them must turn the response OFF rather than half-on.
                anchor.SetLightSheets(null, null);
                Assert.AreEqual(0f, TreeTrunkAnchor.LightChannelsOn(sr), 1e-6f);
                Assert.IsFalse(anchor.HasLightSheets);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // =================================================================================
        // helpers
        // =================================================================================

        /// <summary>
        /// The committed mask and normal PNGs as raw bytes. Decoded from disk rather than read off the
        /// imported asset: <c>GetPixels32</c> on the import would need Read/Write enabled, and this
        /// way the test measures the pixels that are actually in git.
        ///
        /// <para>Both are decoded identically, so index <c>i</c> is the same texel in both — which is
        /// all the oracle needs. (Unity's decode puts row 0 at the BOTTOM while the rig's canvas puts
        /// it at the top; that flip applies to both sheets equally and cancels.)</para>
        /// </summary>
        private static (Color32[] mask, Color32[] normal, int w, int h) LoadSheets()
        {
            var mask = ReadPng(TreeKitCatalog.SheetPath(Species, Stage, Season, TreeKitCatalog.Channel.Mask));
            var normal = ReadPng(TreeKitCatalog.SheetPath(Species, Stage, Season, TreeKitCatalog.Channel.Normal));
            Assert.AreEqual(mask.px.Length, normal.px.Length,
                "the mask and normal sheets differ in size — one atlas uv cannot address both");
            return (mask.px, normal.px, mask.w, mask.h);
        }

        /// <summary>
        /// The view-space direction of the LOWEST sun still above the horizon, and the hour it happens
        /// at — the grazing dawn/dusk light, which is the only time the shipped sun is anywhere near
        /// the rim's half of the front/behind decision.
        /// </summary>
        private static (Vector3 dir, float hour) GrazingSun()
        {
            var profile = DayNightProfile.CreateDefault();
            Vector3 best = Vector3.forward;
            float bestHour = 12f, bestZ = float.MaxValue;

            for (float hour = 0f; hour < 24f; hour += 0.25f)
            {
                float elev = DayNightMath.SunElevation(hour, profile.SunriseHour, profile.SunsetHour);
                if (elev <= 0f) continue;
                Vector2 g = DayNightMath.SunDirection(hour, profile.SunriseHour, profile.SunsetHour,
                                                      profile.ShadowSouthBias, profile.ShadowNoonLift);
                Vector3 v = SpriteLightMath.LightViewDirection(g, elev);
                if (v.z < bestZ) { bestZ = v.z; best = v; bestHour = hour; }
            }
            return (best, bestHour);
        }

        private static (Color32[] px, int w, int h) ReadPng(string path)
        {
            Assert.IsTrue(File.Exists(path), $"missing committed sheet: {path}");
            // linear:true = these are DATA (the sheets import with sRGB OFF); GetPixels32 hands back
            // the stored bytes either way, but saying so keeps the intent visible.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: true);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)), $"could not decode {path}");
                return (tex.GetPixels32(), tex.width, tex.height);
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        /// <summary>PNG width/height straight out of the IHDR chunk — no import, no decode.</summary>
        private static (int w, int h) PngSize(string path)
        {
            Assert.IsTrue(File.Exists(path), $"missing committed sheet: {path}");
            byte[] head = new byte[24];
            using (var fs = File.OpenRead(path))
                Assert.AreEqual(24, fs.Read(head, 0, 24), $"{path} is too short to be a PNG");
            int W = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            int H = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            return (W, H);
        }
    }
}
