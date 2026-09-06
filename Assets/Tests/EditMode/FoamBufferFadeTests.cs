using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>REGISTER ROW 28 — THE FOAM BUFFER COULD NOT FADE AT 60 FPS.</b> The owner, 2026-09-04 and
    /// again 09-06: <i>"foam fades behind boat … and fade away"</i>; <i>"it doesnt widen over time and
    /// fade away."</i> PR 11b (#747) was the widening. This is the fading, and it was never a look
    /// problem — it was the render target.
    ///
    /// <para><b>The finding.</b> The buffer shipped as <c>RenderTextureFormat.RG16</c>, 8 bits per
    /// channel. The advect pass multiplies the stored value by an exponential decay factor and writes
    /// the result back into those 8 bits, so <b>a per-frame change under half a code rounds back to
    /// where it started and is lost</b>. A decay is a MULTIPLY, so its per-frame change is largest at
    /// full white — and at the PC-first 60 fps baseline even that is 0.491 of a code. The coverage
    /// channel therefore did not decay <i>at any stored value</i>, and the freshness clock stopped a
    /// third of the way down #724's colour walk. Every "fade" anyone had seen was the 96 m WINDOW
    /// running out, not the decay.</para>
    ///
    /// <para><b>Why the shipped guards were all green through it.</b>
    /// <c>FoamBufferTests.DecayFactor_HalvesAtTheHalfLife_AndComposes</c> and
    /// <c>HalfLife_SetsAVisibleLifetime_NotAnInstantPop</c> test the FACTOR, in float. The factor was
    /// always right. Nothing asked what the BUFFER did with it, which is the guard-rot lesson in its
    /// purest form: a term is not running until something measures it through the thing that stores
    /// it. That is what <see cref="FoamBuffer.Store"/> is for, and every arm below goes through it.</para>
    ///
    /// <para>All pure arithmetic — no graphics device, nothing to skip on CI. The photographs are
    /// <c>FoamFadePlateTests</c>.</para>
    /// </summary>
    public class FoamBufferFadeTests
    {
        /// <summary>IsoFacetHullFeature._foamHalfLifeSeconds — the COVERAGE channel's.</summary>
        const float CoverageHalfLife = 6f;
        /// <summary>IsoFacetHullFeature._foamAgeHalfLifeSeconds — the FRESHNESS channel's.</summary>
        const float AgeHalfLife = 4f;

        /// <summary>HiddenHarboursWater.shader's <c>_WakeFoamThreshold</c>: the stored coverage the
        /// compose starts drawing at. Below this the sea is clean water, so this — not zero — is what
        /// "faded away" means to the player.</summary>
        const float ComposeThreshold = 0.12f;
        /// <summary><c>_WakeFoamThreshold + _WakeFoamSoftness</c>: the stored value the compose reaches
        /// full strength at. Between the two is the whole of the visible fade.</summary>
        const float ComposeFull = 0.30f;

        /// <summary>IsoFacetHullFeature._foamWindowMeters, and its derived resolution.</summary>
        const float ShippedWindowMeters = 96f;

        /// <summary>The frame rates the arms are stated at. 60 is the PC-first baseline (rule 7).</summary>
        static readonly float[] FrameRates = { 30f, 60f, 120f, 144f };

        static readonly (string name, float halfLife)[] Channels =
        {
            ("coverage R", CoverageHalfLife),
            ("freshness G", AgeHalfLife),
        };

        /// <summary>The format the policy picks on a device that will render to anything.</summary>
        static RenderTextureFormat Fixed => FoamBuffer.SelectFormat(_ => true);
        /// <summary>🔴 THE SABOTAGE ARM: the format that shipped.</summary>
        const RenderTextureFormat Shipped = RenderTextureFormat.RG16;

        /// <summary>Step a texel written at 1.0 through nothing but decay and STORAGE, and answer how
        /// many frames it takes to reach half. -1 when it never does.</summary>
        static int FramesToHalf(float halfLife, float dt, RenderTextureFormat format, int cap)
        {
            float factor = FoamBuffer.DecayFactor(halfLife, dt);
            float v = FoamBuffer.Store(1f, format);
            for (int i = 1; i <= cap; i++)
            {
                float next = FoamBuffer.Store(v * factor, format);
                if (next >= v) return -1;               // stalled: it will never get there
                v = next;
                if (v <= 0.5f) return i;
            }
            return -1;
        }

        // ==== 1. THE DECAY IS REAL ==================================================================

        /// <summary>
        /// 🔴 <b>THE GUARD.</b> A texel written at 1.0, with no injection, must decay to at most half
        /// within one half-life — allowing the one frame the quantization can cost either way — at
        /// every frame rate this game is played at, in BOTH channels. That is the whole of what row 28
        /// asked for, stated through <see cref="FoamBuffer.Store"/> so it is the BUFFER being measured
        /// and not the factor.
        ///
        /// <para>The sabotage arm is the format that shipped, in the same run: it must fail the
        /// identical statement, or this guard is comparing two arms that have converged and proves
        /// nothing.</para>
        /// </summary>
        [Test]
        public void TheDecayIsReal_AtEveryFrameRate_InBothChannels_AndTheShippedFormatIsNot()
        {
            int shippedFailures = 0;
            foreach ((string name, float halfLife) in Channels)
            foreach (float fps in FrameRates)
            {
                float dt = 1f / fps;
                int budget = Mathf.RoundToInt(halfLife / dt) + 1;      // one half-life, plus one frame
                int fixedFrames = FramesToHalf(halfLife, dt, Fixed, budget);
                int shippedFrames = FramesToHalf(halfLife, dt, Shipped, budget);
                float fixedFloor = FoamBuffer.DecayFloor(halfLife, dt, Fixed);
                float shippedFloor = FoamBuffer.DecayFloor(halfLife, dt, Shipped);

                TestContext.WriteLine(
                    $"{fps,3:0} fps  {name,-12} budget {budget,4} frames | " +
                    $"{Fixed,-6} reached half in {fixedFrames,4} (floor {fixedFloor:0.00000}) | " +
                    $"{Shipped} reached half in {shippedFrames,4} (floor {shippedFloor:0.000})");

                Assert.AreNotEqual(-1, fixedFrames,
                    $"At {fps:0} fps the {name} channel never reaches half in {Fixed}: it stalls at " +
                    $"{fixedFloor:0.00000}. The decay is a multiply, so a per-frame change under half " +
                    "a code of the target rounds back to where it started — either the format policy " +
                    "has been narrowed or a half-life has been pushed past what it can carry.");

                if (shippedFrames == -1) shippedFailures++;
            }

            Assert.AreEqual(Channels.Length * FrameRates.Length, shippedFailures,
                "DEAD CONTROL: the shipped 8-bit target is supposed to fail every one of these arms — " +
                "that is register row 28. If it now passes any of them, the two formats have converged " +
                "and nothing above is being proved by the change under test.");

            // The headline, pinned so it cannot quietly stop being the reason this PR exists.
            Assert.AreEqual(1f, FoamBuffer.DecayFloor(CoverageHalfLife, 1f / 60f, Shipped), 1e-6f,
                "At the PC-first 60 fps baseline the shipped coverage channel does not move AT ALL: " +
                "0.491 codes per frame at full white is under the half-code rounding floor at every " +
                "stored value. This is the sentence row 28 is.");
            Assert.Less(FoamBuffer.DecayFloor(CoverageHalfLife, 1f / 60f, Fixed), ComposeThreshold,
                $"...and in {Fixed} it must come to rest below the compose's {ComposeThreshold} " +
                "threshold, or the wake stalls at a value the player can still see.");
        }

        /// <summary>
        /// The decay's reach scales with dt, so a FASTER machine gets a SMALLER per-frame change and
        /// therefore MORE permanent foam. That is the property that made row 28 invisible for two
        /// months — it looked like art, not like a bug — and it is arithmetic, so it can be pinned.
        /// </summary>
        [Test]
        public void AFasterMachineGetsASmallerStep_WhichIsWhyThisHidInPlainSight()
        {
            float previous = float.MaxValue;
            foreach (float fps in FrameRates)
            {
                float step = 1f - FoamBuffer.DecayFactor(CoverageHalfLife, 1f / fps);
                TestContext.WriteLine($"{fps,3:0} fps  loses {step * 255f:0.000} codes/frame at white " +
                                      $"in RG16, {step * 65535f:0.0} in RG32");
                Assert.Less(step, previous,
                    "A shorter frame is a smaller step of an exponential — if this ever inverts, the " +
                    "decay is not exponential any more.");
                previous = step;
            }
        }

        // ==== 2. THE COLOUR WALK ====================================================================

        /// <summary>
        /// 🔴 <b>#724's BLUES MUST REACH THEIR LAST RUNG.</b> The freshness channel is the wake's
        /// clock: the compose reads <c>age01 = 1 − freshness</c> and walks the sea's own palette
        /// anchors down it. Stepped through the buffer's storage, that walk must actually finish — and
        /// on the shipped target it stopped at the first third, so the wake could never reach the
        /// shallow blue, let alone the mid.
        ///
        /// <para>Read through the shipped twins (<see cref="WakeFoamAgeing.Age01FromFreshness"/>,
        /// <see cref="WakeFoamAgeing.Knots"/>) at the material's own defaults, so this measures the
        /// colour the player gets and not a transcription of it.</para>
        /// </summary>
        [Test]
        public void TheFreshnessClock_WalksItsWholeRamp_AndTheShippedFormatStopsAtTheFirstThird()
        {
            // HiddenHarboursWater.shader defaults.
            const float freshFloor = 1f, whiteHold = 0.12f, blueReach = 0.45f, deepReach = 0.85f;
            const float dt = 1f / 60f;

            var report = new List<string>();
            float fixedRamp = 0f, shippedRamp = 0f;
            int fixedCodes = 0, shippedCodes = 0;

            foreach (RenderTextureFormat format in new[] { Fixed, Shipped })
            {
                float factor = FoamBuffer.DecayFactor(AgeHalfLife, dt);
                float v = FoamBuffer.Store(1f, format);
                var distinct = new HashSet<float> { v };
                for (int i = 0; i < 100000; i++)
                {
                    float next = FoamBuffer.Store(v * factor, format);
                    if (next >= v) break;
                    v = next;
                    distinct.Add(v);
                }
                float age01 = WakeFoamAgeing.Age01FromFreshness(v, freshFloor);
                float ramp = WakeFoamAgeing.Knots(age01, whiteHold, blueReach, deepReach);
                report.Add($"{format,-6}: freshness comes to rest at {v:0.00000} -> age01 {age01:0.0000} " +
                           $"-> ramp {ramp:0.0000} across {distinct.Count} distinct stored values");
                if (format == Shipped) { shippedRamp = ramp; shippedCodes = distinct.Count; }
                else { fixedRamp = ramp; fixedCodes = distinct.Count; }
            }
            foreach (string line in report) TestContext.WriteLine(line);

            Assert.AreEqual(1f, fixedRamp, 1e-4f,
                "The colour walk must finish: age01 has to reach the ramp's last rung (the mid blue) " +
                "on a straight run, or #724's palette walk is a staircase between its first two stops.");
            Assert.GreaterOrEqual(fixedCodes, 64,
                "...and it has to WALK, not step: at least the 64 distinct stored values PR 11b " +
                "measured at the dispersal rim, or the ramp is quantized into a handful of bands.");

            Assert.Less(shippedRamp, 0.5f,
                "DEAD CONTROL: on the shipped 8-bit target the clock stalls at stored 0.680, so the " +
                "walk stops at ramp 0.305 and the SHALLOW blue (0.5) is never reached. If this arm " +
                "now completes the walk, the formats have converged and the arm above proves nothing.");
            TestContext.WriteLine(
                $"the shipped arm resolves the walk in {shippedCodes} values and stops at ramp " +
                $"{shippedRamp:0.000} of 1.000 — the shallow-blue anchor is at 0.5");
        }

        // ==== 3. THE POLICY =========================================================================

        /// <summary>
        /// The format is CHOSEN, from a list that states why, against what the device will actually
        /// render to — never typed at the descriptor, which is where row 28 hid. Pinned with injected
        /// support predicates: <c>SystemInfo</c> on a null graphics device answers for no device at
        /// all, and a policy pinned against that is pinned against nothing.
        /// </summary>
        [Test]
        public void TheFormatPolicy_TakesTheWidestTheDeviceWillRenderTo_AndNamesItsLastRung()
        {
            Assert.AreEqual(RenderTextureFormat.RG32, FoamBuffer.SelectFormat(_ => true),
                "A device that will render to anything must get the 16-bit UNORM: uniform codes are " +
                "the right grid for a quantity that lives in [0,1], and the half float carries a " +
                "frame-rate-dependent decay bias (0.535 after one half-life at 144 fps, against " +
                "RG32's 0.50001).");
            Assert.AreEqual(RenderTextureFormat.RGHalf,
                FoamBuffer.SelectFormat(f => f != RenderTextureFormat.RG32),
                "A device without RG16_UNorm as a colour target — GLES3 makes it optional and RG16F " +
                "core — must fall back to the half float, which still decays. Rule 7: the later " +
                "mobile port is not painted into a corner.");
            Assert.AreEqual(RenderTextureFormat.RG16,
                FoamBuffer.SelectFormat(f => f == RenderTextureFormat.RG16),
                "A device with neither 16-bit format still has to get a buffer — foam that cannot " +
                "fade beats no foam, and it is NAMED here rather than silently reintroduced.");
            Assert.AreEqual(RenderTextureFormat.RG16, FoamBuffer.SelectFormat(null),
                "A missing probe must fail to the shipped format, not to null.");
            Assert.AreEqual(RenderTextureFormat.RG16,
                FoamBuffer.FormatPreference[FoamBuffer.FormatPreference.Length - 1],
                "The last rung is the always-allocatable one, and the policy returns it when nothing " +
                "else is supported — those two facts have to stay the same fact.");

            Assert.AreEqual(2, FoamBuffer.BytesPerTexel(RenderTextureFormat.RG16));
            Assert.AreEqual(4, FoamBuffer.BytesPerTexel(RenderTextureFormat.RG32));
            Assert.AreEqual(4, FoamBuffer.BytesPerTexel(RenderTextureFormat.RGHalf));
            Assert.AreEqual(4, FoamBuffer.BytesPerTexel(RenderTextureFormat.ARGBFloat),
                "An unrecognised format must cost the WIDEST thing the policy can pick, so a budget " +
                "statement can never be flattered by a format the table does not know.");
        }

        /// <summary>
        /// The production allocation has to go through the policy. A guard on
        /// <see cref="FoamBuffer.SelectFormat"/> alone would stay green forever beside a descriptor
        /// with a literal format typed into it — which is exactly the shape row 28 shipped in.
        /// </summary>
        [Test]
        public void TheProductionAllocation_AsksThePolicy_AndTypesNoFormatOfItsOwn()
        {
            const string rel = "Assets/_Project/Code/Art/IsoFacetHullFeature.cs";
            string path = Path.Combine(Application.dataPath, "..", rel);
            Assert.IsTrue(File.Exists(path), "IsoFacetHullFeature not found at " + rel);
            string code = File.ReadAllText(path);

            StringAssert.Contains("FoamBuffer.SelectFormat(SystemInfo.SupportsRenderTextureFormat)", code,
                "The foam pair must be allocated in whatever format the POLICY picks for this device.");
            Assert.IsFalse(
                code.Contains("new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat."),
                "The foam descriptor must take the policy's variable, never a literal " +
                "RenderTextureFormat: a constant typed here is how row 28 hid for two months.");
        }

        // ==== 4. THE BUDGET (rule 7) ================================================================

        /// <summary>What the fix costs, stated in bytes at the shipped window rather than asserted to
        /// be small. Rule 7 is a feature: the number goes in the PR body from here.</summary>
        [Test]
        public void TheBudget_DoublesTheBuffer_AndIsStatedInBytes()
        {
            int res = FoamBuffer.ResolutionForExtent(ShippedWindowMeters);
            long Bytes(RenderTextureFormat f) => 2L * res * res * FoamBuffer.BytesPerTexel(f);

            long before = Bytes(Shipped), after = Bytes(Fixed);
            TestContext.WriteLine(
                $"{ShippedWindowMeters:0} m window -> {res}^2 texels, ping-pong x2: " +
                $"{Shipped} {before / 1024f / 1024f:0.00} MB -> {Fixed} {after / 1024f / 1024f:0.00} MB " +
                $"per camera (+{(after - before) / 1024f / 1024f:0.00} MB)");

            Assert.AreEqual(2 * before, after, "The fix is exactly one extra byte per channel.");
            Assert.LessOrEqual(after, 8L * 1024 * 1024,
                "The foam buffer is per CAMERA and sits beside the seabed bake (1.0 MB) and an " +
                "ARGBHalf reflection target at camera resolution. Past 8 MB the window dial, not the " +
                "format, is what needs the conversation.");
        }

        // ==== 5. THE ALTERNATIVE THAT WAS NOT SHIPPED ===============================================

        /// <summary>A cheap integer hash of a WORLD cell and the frame — the shape fix (b) would use.
        /// Never <c>Time</c>-seeded (rule 5), and world-locked so it cannot crawl under a pan.</summary>
        static float Hash01(int x, int y, int frame)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + frame * 2246822519);
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return h / 4294967296f;
            }
        }

        /// <summary>
        /// 🔴 <b>FIX (b), MEASURED AND REJECTED — the record of why the format was the honest fix.</b>
        /// Keep 8 bits and add a dither so the expected decay over many frames equals the analytic one.
        /// Three arms, one run:
        ///
        /// <list type="number">
        /// <item><b>A TEMPORAL dither</b> (hash of cell and frame) gets the mean right to a fraction of
        /// a percent — and its error is a RANDOM WALK, so identically-laid texels drift apart by tens
        /// of codes. The compose's whole visible fade is the 46 codes between
        /// <c>_WakeFoamThreshold</c> 0.12 and 0.30; at 60 fps the walk is wider than that, so the
        /// dying rim — the only place a fade is visible at all — would be noise rather than foam.</item>
        /// <item><b>An ORDERED dither</b> (a fixed per-cell offset, no frame index) does not random-walk
        /// and does not decay either: it only moves each texel's stall value, so the patch freezes as a
        /// fixed-pattern dissolve.</item>
        /// <item><b>An exact temporal error diffusion</b> would need the residual STORED — one per
        /// channel, which is the same two bytes a texel that the wider format costs. The honest dither
        /// costs exactly what the honest format costs, and is noisier.</item>
        /// </list>
        ///
        /// <para>⚠️ This asserts nothing about shipped behaviour. It is arithmetic about a design that
        /// was not taken, and it stays true whatever the buffer does next.</para>
        /// </summary>
        [Test]
        public void TheDitherAlternative_GetsTheMeanRight_AndLosesTheDyingRimToNoise_Measured()
        {
            const int side = 64, n = side * side;
            const float dt = 1f / 60f;
            float factor = FoamBuffer.DecayFactor(CoverageHalfLife, dt);
            int perHalfLife = Mathf.RoundToInt(CoverageHalfLife / dt);
            float lsb = 1f / 255f;
            float rampCodes = (ComposeFull - ComposeThreshold) * 255f;

            // Three arms walked side by side, sampled at one, two and four half-lives — because the
            // whole disagreement between them is about TIME: the dither's error is a random walk, so
            // its spread grows as the square root of the frames while the ramp it has to sit inside
            // does not move at all.
            var none = new float[n];
            var temporal = new float[n];
            var ordered = new float[n];
            for (int i = 0; i < n; i++) none[i] = temporal[i] = ordered[i] = FoamBuffer.Store(1f, Shipped);

            (float mean, float spread) Stat(float[] v)
            {
                float sum = 0f, lo = 1f, hi = 0f;
                foreach (float f in v) { sum += f; lo = Mathf.Min(lo, f); hi = Mathf.Max(hi, f); }
                return (sum / v.Length, (hi - lo) * 255f);
            }

            (float mean, float spread) noneAtOne = default, tempAtOne = default, ordAtOne = default;
            (float mean, float spread) tempAtTwo = default, ordAtFour = default;

            TestContext.WriteLine($"8-bit buffer, 60 fps, {CoverageHalfLife:0} s half-life; the compose's " +
                                  $"whole visible fade is {rampCodes:0.0} codes ({ComposeThreshold} -> {ComposeFull})");
            for (int half = 1; half <= 4; half++)
            {
                for (int f = (half - 1) * perHalfLife; f < half * perHalfLife; f++)
                for (int i = 0; i < n; i++)
                {
                    int cx = i % side, cy = i / side;
                    none[i] = FoamBuffer.Store(none[i] * factor, Shipped);
                    temporal[i] = FoamBuffer.Store(temporal[i] * factor + (Hash01(cx, cy, f) - 0.5f) * lsb, Shipped);
                    ordered[i] = FoamBuffer.Store(ordered[i] * factor + (Hash01(cx, cy, 0) - 0.5f) * lsb, Shipped);
                }
                if (half == 3) continue;
                float analytic = Mathf.Pow(0.5f, half);
                (float mean, float spread) a = Stat(none), b = Stat(temporal), c = Stat(ordered);
                TestContext.WriteLine(
                    $"  after {half} half-li{(half == 1 ? "fe" : "ves")} (analytic {analytic:0.0000}): " +
                    $"no dither {a.mean:0.0000}/{a.spread:0.0}c · " +
                    $"temporal {b.mean:0.0000}/{b.spread:0.0}c · ordered {c.mean:0.0000}/{c.spread:0.0}c");
                if (half == 1) { noneAtOne = a; tempAtOne = b; ordAtOne = c; }
                if (half == 2) tempAtTwo = b;
                if (half == 4) ordAtFour = c;
            }

            Assert.AreEqual(1f, noneAtOne.mean, 1e-6f,
                "The shipped arm is the control: with no dither an 8-bit buffer at 60 fps does not " +
                "decay at all, at any age.");
            Assert.AreEqual(0.5f, tempAtOne.mean, 0.02f,
                "The temporal dither DOES fix the decay in expectation — that is why it was a " +
                "candidate, and saying so is the point of measuring it.");
            // 🔴 WHERE IT LANDS IS THE POINT, and the measurement corrected the first guess here.
            // The walk does NOT widen without bound — the decay's multiply is a restoring pull, so the
            // spread settles and then shrinks with the mean (50 -> 46 -> 28 codes). What it does
            // instead is worse: it is at its widest exactly as the trail passes through the compose's
            // fade band, so the noise arrives precisely where the fade becomes visible.
            Assert.GreaterOrEqual(tempAtTwo.mean, ComposeThreshold,
                "Two half-lives is where the trail enters the compose's fade band; if it no longer " +
                "does, this arm is being sampled somewhere the argument is not about.");
            Assert.LessOrEqual(tempAtTwo.mean, ComposeFull,
                "...and it must still be inside that band, not through it.");
            Assert.GreaterOrEqual(tempAtTwo.spread, 0.75f * rampCodes,
                $"...and THIS is why the dither was not shipped. Its error is a random walk, and where " +
                $"the trail sits in the compose's fade band identically-laid texels are " +
                $"{tempAtTwo.spread:0} codes apart against the {rampCodes:0} codes that band is wide. " +
                "The dying rim — the only place a fade is visible at all — would read as noise rather " +
                "than as foam.");
            Assert.Greater(ordAtFour.mean, 4f * Mathf.Pow(0.5f, 4f),
                $"The ordered dither does not decay at all: a FIXED per-cell offset only moves each " +
                $"texel's stall value, so after four half-lives the patch still averages " +
                $"{ordAtFour.mean:0.000} against an analytic {Mathf.Pow(0.5f, 4f):0.0000} — it froze " +
                "as a fixed-pattern dissolve instead of fading.");
            Assert.Greater(ordAtOne.spread, rampCodes,
                "...and it freezes SPLIT: some texels stall high and some at zero, which is the " +
                "dissolve, not a fade.");

            Assert.AreEqual(2, FoamBuffer.BytesPerTexel(Fixed) - FoamBuffer.BytesPerTexel(Shipped),
                "🔴 THE ARGUMENT THAT SETTLED IT. An EXACT temporal error diffusion — one that does " +
                "not random-walk — has to STORE its residual, one byte per channel, two per texel. " +
                "That is precisely what the wider format costs. The honest dither buys nothing over " +
                "the honest format and pays for it in noise.");
        }

        // ==== 6. WHAT THE OWNER NOW HAS A DIAL FOR ==================================================

        /// <summary>
        /// <b>The tail is a LENGTH now, and it is the half-life dial's to set.</b> White crosses the
        /// compose threshold after <c>halfLife · log2(1/0.12)</c> = 3.06 half-lives, so the visible
        /// trail astern is that time times the hull's speed. Reported as a table because
        /// <c>_foamHalfLifeSeconds</c> has never done anything at 60 fps and is therefore untuned —
        /// the owner picks, and this is the arithmetic he picks against.
        ///
        /// <para>⚠️ Nothing here asserts a particular half-life. Only that the tail is finite, and
        /// monotone in the two things it is made of.</para>
        /// </summary>
        [Test]
        public void TheVisibleTail_IsAFiniteLength_MonotoneInSpeedAndHalfLife_Tabulated()
        {
            float lives = Mathf.Log(1f / ComposeThreshold, 2f);
            float asternWindow = ShippedWindowMeters * 0.5f;   // the buffer is centred on the camera

            TestContext.WriteLine($"white crosses the compose threshold at {lives:0.00} half-lives; " +
                                  $"the window reaches {asternWindow:0} m astern of the camera");
            foreach (float halfLife in new[] { 6f, 4f, 3f })
            {
                float seconds = halfLife * lives;
                string row = $"  half-life {halfLife:0}s ({seconds:0.0}s of trail):";
                foreach (float knots in new[] { 3f, 5f, 8f, 14f })
                    row += $"  {knots:0}kn {seconds * knots * 0.514444f,6:0.0} m";
                TestContext.WriteLine(row);
            }

            // Finite, because the buffer can now reach the threshold at all.
            Assert.Less(FoamBuffer.DecayFloor(CoverageHalfLife, 1f / 60f, Fixed), ComposeThreshold);
            Assert.GreaterOrEqual(FoamBuffer.DecayFloor(CoverageHalfLife, 1f / 60f, Shipped),
                ComposeThreshold,
                "DEAD CONTROL: on the shipped format the trail never falls below the threshold, so it " +
                "has no length at all — it is cut off by the window and nothing else.");

            float Tail(float halfLife, float knots) => halfLife * lives * knots * 0.514444f;
            Assert.Greater(Tail(6f, 8f), Tail(6f, 3f), "A faster hull leaves a longer trail.");
            Assert.Greater(Tail(6f, 8f), Tail(3f, 8f), "A longer half-life leaves a longer trail.");
        }

        // ==== 7. THE STORE ITSELF ===================================================================

        /// <summary>
        /// <see cref="FoamBuffer.Store"/> is the twin of a hardware write, and everything above is
        /// only as good as it is. Round to nearest, saturating, idempotent, and no better than the
        /// format's own grid.
        /// </summary>
        [Test]
        public void Store_IsTheFormatsOwnRounding_SaturatingAndIdempotent()
        {
            foreach (RenderTextureFormat format in FoamBuffer.FormatPreference)
            {
                Assert.AreEqual(0f, FoamBuffer.Store(0f, format), 0f, $"{format}: 0 must store exactly.");
                Assert.AreEqual(1f, FoamBuffer.Store(1f, format), 0f, $"{format}: 1 must store exactly.");
                Assert.AreEqual(0f, FoamBuffer.Store(-3f, format), 0f, $"{format}: writes saturate low.");
                Assert.AreEqual(1f, FoamBuffer.Store(7f, format), 0f, $"{format}: writes saturate high.");

                float worst = 0f;
                for (int i = 0; i <= 1000; i++)
                {
                    float x = i / 1000f;
                    float s = FoamBuffer.Store(x, format);
                    worst = Mathf.Max(worst, Mathf.Abs(s - x));
                    Assert.AreEqual(s, FoamBuffer.Store(s, format), 0f,
                        $"{format}: storing a stored value must be a no-op, or the buffer drifts on " +
                        "every frame that changes nothing.");
                }
                TestContext.WriteLine($"{format,-6} worst round-trip error over [0,1]: {worst:0.0000000}");
                if (format == RenderTextureFormat.RG16)
                    Assert.AreEqual(0.5f / 255f, worst, 1e-6f, "8-bit UNORM: half a code of 255.");
                if (format == RenderTextureFormat.RG32)
                    Assert.LessOrEqual(worst, 0.5f / 65535f + 1e-9f, "16-bit UNORM: half a code of 65535.");
                if (format == RenderTextureFormat.RGHalf)
                    Assert.LessOrEqual(worst, Mathf.Pow(2f, -11f), "binary16: half an ULP at 1.0.");
            }
        }
    }
}
