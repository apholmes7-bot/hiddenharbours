using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The advected foam buffer is <c>RenderTextureFormat.RG16</c> — <b>8 bits per channel</b>. Every
    /// frame the pass multiplies the previous value by a decay factor, adds this frame's deposits and
    /// writes the result back into those 8 bits, so <b>a per-frame change smaller than half a code
    /// rounds back to the value it started from and is lost</b>. That one sentence bounds two very
    /// different things, and this class exists so both are measured rather than assumed:
    ///
    /// <list type="number">
    /// <item><b>What the DECAY can do</b> — reported here, because it is a shipped property of the
    /// buffer and not of anything PR 11b adds. The numbers are in the failure messages and in the
    /// test log; <c>docs/design/water-rendering.md</c> §35 and register row 26 carry them with the
    /// proposal.</item>
    /// <item><b>What the DISPERSAL EDGE writes</b> — guarded, because that IS PR 11b's, and because
    /// it is exactly the constraint that decides how wide the envelope dial may usefully be
    /// opened.</item>
    /// </list>
    ///
    /// <para>⚠️ Nothing here asserts a defect is present. A guard that goes green only while
    /// something is broken inverts the moment it is fixed.</para>
    /// </summary>
    public class FoamBufferQuantizationTests
    {
        /// <summary>One code of the buffer's 8-bit channels.</summary>
        const float Lsb = 1f / 255f;

        /// <summary>IsoFacetHullFeature._foamHalfLifeSeconds — the COVERAGE channel's.</summary>
        const float CoverageHalfLife = 6f;
        /// <summary>IsoFacetHullFeature._foamAgeHalfLifeSeconds — the FRESHNESS channel's.</summary>
        const float AgeHalfLife = 4f;

        /// <summary>Does a change of this size survive being written into an 8-bit channel? Round to
        /// nearest is what the float→UNORM conversion does, so half a code is the floor.</summary>
        static bool Survives(float change) => Mathf.Abs(change) >= 0.5f * Lsb;

        /// <summary>The rounding rule itself, stated once so everything below reads off one
        /// definition rather than three transcriptions of it.</summary>
        [Test]
        public void TheRoundingFloor_IsHalfACode()
        {
            Assert.IsFalse(Survives(0.49f * Lsb), "Just under half a code must round away.");
            Assert.IsTrue(Survives(0.51f * Lsb), "Just over half a code must survive.");
            Assert.AreEqual(0.00392f, Lsb, 1e-5f, "RG16 is 8 bits per channel, so a code is 1/255.");
        }

        /// <summary>
        /// 🔴 <b>THE MEASUREMENT (a finding, reported not asserted).</b> How much of each channel's
        /// exponential decay actually reaches the buffer, at the frame rates this game is played at.
        /// The decay is a MULTIPLY, so its per-frame change is proportional to the stored value — the
        /// largest change any stored value can produce is at full white, and if even that is under
        /// half a code the channel does not decay at all.
        /// </summary>
        [Test]
        public void HowMuchOfTheDecayReachesAnEightBitBuffer_Measured()
        {
            foreach (float fps in new[] { 30f, 60f, 120f, 144f })
            {
                float dt = 1f / fps;
                foreach (var channel in new[]
                         {
                             ("coverage R", CoverageHalfLife),
                             ("freshness G", AgeHalfLife),
                         })
                {
                    float factor = FoamBuffer.DecayFactor(channel.Item2, dt);
                    float atWhite = (1f - factor) * 255f;                 // codes lost per frame at 1.0
                    // Below this stored value the per-frame change is under half a code and the
                    // channel stops moving; at or above 1 it never moves at all.
                    float stall = Mathf.Min(1f, 0.5f / (255f * (1f - factor)));
                    TestContext.WriteLine(
                        $"{fps,3:0} fps  {channel.Item1,-12}  half-life {channel.Item2:0}s  " +
                        $"factor {factor:0.000000}  loses {atWhite:0.000} codes/frame at white  " +
                        $"stalls at stored {stall:0.000}" +
                        (stall >= 1f ? "   <- does not decay at any stored value" : ""));
                }
            }
            // The one thing that is safe to ASSERT here, because it is arithmetic and not a defect:
            // the decay's reach scales with dt, so a higher frame rate can only ever make it smaller.
            Assert.Less(1f - FoamBuffer.DecayFactor(CoverageHalfLife, 1f / 120f),
                        1f - FoamBuffer.DecayFactor(CoverageHalfLife, 1f / 30f),
                        "A shorter frame is a smaller step of an exponential — if this ever inverts, " +
                        "the decay is not exponential any more.");
        }

        /// <summary>
        /// 🔴 <b>THE GUARD ON PR 11b's OWN TERM.</b> The dispersal edge must write more than a whole
        /// code per frame where the band's visible rim is, at the frame rates the game runs at —
        /// otherwise the widening is rounded away and the owner sees the shipped trail with extra
        /// arithmetic behind it.
        ///
        /// <para>This is also what bounds the envelope dial. The edge lays the envelope ONCE, as it
        /// sweeps, so its per-frame value is set by how fast the rim crosses a cell; a term that
        /// re-stamped the whole widened profile every frame instead spreads the same foam over the
        /// deposit's entire life and lands BELOW the floor — the arm this test shoots beside it.</para>
        /// </summary>
        [Test]
        public void TheDispersalEdge_WritesWellAboveTheRoundingFloor_TheReStampedSkirtDoesNot()
        {
            const float r0 = 2.4f;              // the cape's half-beam
            const float envelope = 2.5f;        // FoamInjector._spreadEnvelopeHalfBeams, shipped
            const float kelvin = 0.8f;          // FoamInjector._spreadKelvinFraction, shipped
            const float rate = 2.5f;            // _depositPerSecond at full churn
            float speed = 8f * 0.514444f;
            float wMax = r0 * envelope;
            float slope = FoamBuffer.SpreadSlope(kelvin);
            float reach = (wMax - r0) / slope;
            // Where the band still draws: the envelope's own value, at the amplitude conservation
            // fixes, against the water material's _WakeFoamThreshold.
            float amplitude = FoamBuffer.DispersingShare(r0, wMax)
                            * FoamBuffer.ShippedFoamPerMetre(rate, r0, speed)
                            / FoamBuffer.EnvelopeIntegral(r0, wMax);
            float rim = r0;
            for (float d = r0; d <= wMax; d += FoamBuffer.CellSize)
                if (amplitude * FoamBuffer.EnvelopeShape(d, r0, wMax) >= 0.12f) rim = d;

            foreach (float fps in new[] { 30f, 60f, 120f, 144f })
            {
                float dt = 1f / fps;
                float edgeWidth = FoamBuffer.EdgeWidth(slope * speed, dt);
                float gain = FoamBuffer.EdgeGain(rate, r0, wMax, kelvin, dt, edgeWidth);
                float edgeWrite = gain * FoamBuffer.EnvelopeShape(rim, r0, wMax);

                // The sabotage arm: the SAME conserved foam, spread over the deposit's whole life
                // instead of laid once at the sweeping rim. Same total, same envelope, same physics —
                // only the stamping strategy differs, which is the point.
                float life = reach / speed;
                float reStamped = amplitude * FoamBuffer.EnvelopeShape(rim, r0, wMax) * dt / life;

                TestContext.WriteLine(
                    $"{fps,3:0} fps  rim {rim:0.00} m  edge writes {edgeWrite * 255f:0.00} codes/frame, " +
                    $"re-stamped skirt {reStamped * 255f:0.00}");

                Assert.IsTrue(Survives(edgeWrite),
                    $"At {fps:0} fps the dispersal edge writes {edgeWrite * 255f:0.000} codes per " +
                    "frame at the band's visible rim, which an 8-bit buffer rounds away. Either the " +
                    "envelope dial has been opened past what the format can carry, or the stamp has " +
                    "stopped laying the envelope in one pass.");
                if (Mathf.Approximately(fps, 60f))
                    Assert.Greater(edgeWrite, Lsb,
                        "At the PC-first baseline a whole code, not just half of one: at half a code " +
                        "the write only survives by rounding UP, which conserves nothing. The " +
                        "margin narrows as dt does — the edge's write is proportional to dt while " +
                        "its width is floored on the world grid — so this is the number that bounds " +
                        "how far the envelope dial can usefully be opened.");
                Assert.Less(reStamped, edgeWrite,
                    "DEAD CONTROL: laying the same foam over the deposit's whole life is supposed to " +
                    "write far less per frame than laying it once at the rim. If it no longer does, " +
                    "the two strategies have converged and this comparison proves nothing.");
            }
        }

        /// <summary>
        /// The FRESHNESS mark the dispersal writes is a value, not an increment — a MAX, not an ADD —
        /// so it is not exposed to the accumulation floor at all. What it does need is enough codes
        /// to separate one age from the next across the walk the colour rides.
        /// </summary>
        [Test]
        public void TheDispersedAgeMark_HasCodesToSpareAcrossTheColourWalk()
        {
            const float r0 = 2.4f;
            float slope = FoamBuffer.SpreadSlope(0.8f);
            float reach = (r0 * 2.5f - r0) / slope;
            float speed = 8f * 0.514444f;
            float tailAge = reach / speed;

            int distinct = 0;
            int previous = -1;
            for (float age = 0f; age <= tailAge; age += tailAge / 64f)
            {
                int code = Mathf.RoundToInt(FoamBuffer.AgeMark(age, AgeHalfLife) * 255f);
                if (code != previous) distinct++;
                previous = code;
            }
            TestContext.WriteLine($"across {tailAge:0.00} s of dispersal the mark takes {distinct} " +
                                  "distinct codes of 256");
            Assert.Greater(distinct, 16,
                "The widening band has to WALK the blues, not step between two of them; if the mark " +
                "resolves to a handful of codes the walk is a staircase.");
        }
    }
}
