using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless twin tests for the §18 drift-line UPGRADE — <see cref="WaterDriftLines"/>, the C# mirror
    /// of the shader's <c>DriftLines()</c> basis blend, convergence weight and <c>PixelizeGrid</c>
    /// (change one, change BOTH in the same PR). Contracts pinned:
    ///
    /// <list type="bullet">
    /// <item><b>all three passthroughs are EXACT</b>, and asserted with exact equality rather than a
    /// tolerance — blend 0 returns the normalized current itself, convergence 0 returns literally 1,
    /// and grid divisor 1 lands on the same lattice <c>Pixelize</c> does. A tolerance here would let a
    /// sub-perceptual drift ship as "unchanged";</item>
    /// <item>the basis blend reaches the SHARED drift at 1 and is monotone between, so the dial means
    /// what its label says;</item>
    /// <item>the convergence weight only ever DIMS lanes (it is a gate, never a brightener);</item>
    /// <item>the grid coarsens by exactly the divisor, and the world-cell arithmetic reports the
    /// hierarchy the shipped numbers already had.</item>
    /// </list>
    ///
    /// Sabotage MEASURED (2026-07-30, headless runs over this fixture, <b>14</b> tests — see the PR):
    /// <list type="number">
    /// <item><b>invert the convergence weight</b> (<c>lerp(convergence, 1, blend)</c>): <b>3 of 14</b> —
    /// <c>ConvergenceWeight_BlendZero_IsExactlyOne</c> (0.0 where 1.0 was required, so every dialled-in
    /// material would lose its lanes outright), <c>_BlendOne_IsTheGateItself</c> (1.0 for 0.0) and
    /// <c>_GathersLanes_DivergenceLosesThem</c>. In pixels: scum collecting where the surface DIVERGES —
    /// precisely the clean water between real drift lines;</item>
    /// <item><b>multiply instead of divide</b> by the grid divisor: <b>1 of 14</b> —
    /// <c>PixelizeGrid_CoarsensByExactlyTheDivisor</c> (a fine-cell step moved the coarse cell,
    /// 3.34 vs 3.30). The grid would get FINER as the owner asked for coarser, so the one knob meant to
    /// fight aliasing would cause it;</item>
    /// <item><b>delete the shader's shared-drift guard</b> (<c>if (_DriftLineFoamDrift &gt; 0.001)</c> →
    /// <c>if (true)</c>): <b>1 of 14</b> — <c>ShaderGuards_KeepTheUpgradeUnreachableAtTheDefaults</c>.</item>
    /// </list>
    ///
    /// <para>⚠️ <b>And one sabotage that measured ZERO, which changed the fixture.</b> Dropping the
    /// blend-0 short circuit from <c>DriftDirection</c> failed <b>0 of 13</b>: <c>Lerp(a, b, 0)</c>
    /// returns <c>a</c>, and re-normalizing an already-unit vector is bit-stable, so that short circuit
    /// is <b>defensive rather than load-bearing</b>. Rather than invent a test to catch a difference
    /// that is not there, the fixture now pins the guard that IS load-bearing — the SHADER's, where
    /// blend 0 must not merely compute the same answer but must not run <c>FoamDriftDir()</c> or four
    /// <c>WaveFieldSample</c> taps at all. That is arm 3 above, and it bites.</para>
    /// </summary>
    public class WaterDriftLinesTests
    {
        const float Ppu = 32f;
        const float ShippedScale = 0.3f;      // _DriftLineScale's shipped default

        static readonly Vector2 Current = new Vector2(1f, 0f);
        static readonly Vector2 SharedDrift = new Vector2(0f, 1f);

        // ===== (1) the basis blend =========================================================

        [Test]
        public void DriftDirection_BlendZero_IsTheNormalizedCurrentEXACTLY()
        {
            // EXACT, not approximate. A lerp-then-normalize at blend 0 is mathematically the same
            // vector and numerically a hair off it, and "a hair off" on a material nobody opted in is
            // still a changed look. The shader short-circuits for the same reason.
            Vector2 expected = Current.normalized;
            Vector2 got = WaterDriftLines.DriftDirection(Current, SharedDrift, 0f);
            Assert.AreEqual(expected.x, got.x, "blend 0 must return the current bit-for-bit.");
            Assert.AreEqual(expected.y, got.y);
        }

        [Test]
        public void DriftDirection_BlendOne_IsTheSharedDrift()
        {
            Vector2 got = WaterDriftLines.DriftDirection(Current, SharedDrift, 1f);
            Assert.AreEqual(SharedDrift.normalized.x, got.x, 1e-5f);
            Assert.AreEqual(SharedDrift.normalized.y, got.y, 1e-5f);
        }

        [Test]
        public void DriftDirection_IsMonotoneBetween_SoTheDialMeansWhatItSays()
        {
            // The angle to the shared drift must fall as the blend rises — no overshoot, no plateau.
            float prev = Vector2.Angle(WaterDriftLines.DriftDirection(Current, SharedDrift, 0f), SharedDrift);
            foreach (float b in new[] { 0.2f, 0.4f, 0.6f, 0.8f, 1f })
            {
                float angle = Vector2.Angle(WaterDriftLines.DriftDirection(Current, SharedDrift, b), SharedDrift);
                Assert.Less(angle, prev + 1e-4f, $"the basis must keep turning toward the shared drift at {b}.");
                prev = angle;
            }
            Assert.Less(prev, 1e-3f, "and reach it at 1.");
        }

        [Test]
        public void DriftDirection_IsAlwaysUnitLength()
        {
            foreach (float b in new[] { 0f, 0.5f, 1f })
                Assert.AreEqual(1f, WaterDriftLines.DriftDirection(Current, SharedDrift, b).magnitude, 1e-4f);
        }

        [Test]
        public void DriftDirection_ZeroVectors_FallBackRatherThanNaN()
        {
            // A dead-calm sea publishes a zero current, and a zero-length normalize is a NaN that
            // paints the whole surface black. Both inputs are guarded.
            Vector2 a = WaterDriftLines.DriftDirection(Vector2.zero, SharedDrift, 0f);
            Assert.IsFalse(float.IsNaN(a.x) || float.IsNaN(a.y));
            Assert.AreEqual(1f, a.magnitude, 1e-4f);

            Vector2 b = WaterDriftLines.DriftDirection(Current, Vector2.zero, 1f);
            Assert.IsFalse(float.IsNaN(b.x) || float.IsNaN(b.y));
            Assert.AreEqual(1f, b.magnitude, 1e-4f);
        }

        // ===== (2) the convergence weight ==================================================

        [Test]
        public void ConvergenceWeight_BlendZero_IsExactlyOne()
        {
            foreach (float c in new[] { 0f, 0.3f, 1f })
                Assert.AreEqual(1f, WaterDriftLines.ConvergenceWeight(c, 0f),
                    "weight 0 must leave the lanes exactly as shipped.");
        }

        [Test]
        public void ConvergenceWeight_BlendOne_IsTheGateItself()
        {
            foreach (float c in new[] { 0f, 0.25f, 0.8f, 1f })
                Assert.AreEqual(c, WaterDriftLines.ConvergenceWeight(c, 1f), 1e-6f);
        }

        [Test]
        public void ConvergenceWeight_OnlyEverDIMS_NeverBrightens()
        {
            // It is a GATE. A weight above 1 would make a drift line brighter than the noise that
            // drew it, which reads as a paint splash rather than as gathered scum.
            for (float c = 0f; c <= 1f; c += 0.1f)
            for (float b = 0f; b <= 1f; b += 0.25f)
            {
                float w = WaterDriftLines.ConvergenceWeight(c, b);
                Assert.LessOrEqual(w, 1f + 1e-6f);
                Assert.GreaterOrEqual(w, -1e-6f);
            }
        }

        [Test]
        public void ConvergenceWeight_GathersLanes_DivergenceLosesThem()
        {
            // The physical claim: at full weight a converging patch keeps its lanes and a diverging
            // one loses them. That asymmetry IS the band-with-clean-water-between look.
            Assert.Greater(WaterDriftLines.ConvergenceWeight(0.9f, 1f),
                           WaterDriftLines.ConvergenceWeight(0.05f, 1f));
        }

        // ===== (3) the layer's own grid ====================================================

        [Test]
        public void PixelizeGrid_DivisorOne_MatchesThePlainPixelizeLattice()
        {
            // Bit-identical to floor(p*ppu)/ppu — the passthrough. Checked on coordinates that are
            // deliberately NOT already on the grid.
            foreach (var p in new[] { new Vector2(3.3123f, -1.2871f), new Vector2(-17.9f, 41.04f) })
            {
                Vector2 plain = WaterReflectionWarp.Pixelize(p, Ppu);   // the shipped helper's twin
                Vector2 grid = WaterDriftLines.PixelizeGrid(p, Ppu, 1f);
                Assert.AreEqual(plain.x, grid.x, "divisor 1 must land on the shipped lattice exactly.");
                Assert.AreEqual(plain.y, grid.y);
            }
        }

        [Test]
        public void PixelizeGrid_CoarsensByExactlyTheDivisor()
        {
            // Two points a fine cell apart must share a coarse cell; a coarse cell apart must not.
            const float divisor = 4f;
            float fine = 1f / Ppu;
            var p = new Vector2(3.3123f, -1.2871f);
            Vector2 home = WaterDriftLines.PixelizeGrid(p, Ppu, divisor);
            Assert.AreEqual(home, WaterDriftLines.PixelizeGrid(p + new Vector2(fine, 0f), Ppu, divisor),
                "a fine-cell step must not change a coarse cell.");
            Vector2 next = WaterDriftLines.PixelizeGrid(p + new Vector2(fine * divisor, 0f), Ppu, divisor);
            Assert.AreEqual(fine * divisor, next.x - home.x, 1e-5f,
                "a coarse-cell step must advance exactly one coarse cell.");
        }

        [Test]
        public void PixelizeGrid_DegenerateDivisor_ClampsToTheShippedGrid()
        {
            foreach (float d in new[] { 0f, -3f, 0.5f })
            {
                Vector2 got = WaterDriftLines.PixelizeGrid(new Vector2(3.3123f, -1.2871f), Ppu, d);
                Assert.AreEqual(WaterDriftLines.PixelizeGrid(new Vector2(3.3123f, -1.2871f), Ppu, 1f), got,
                    "a divisor below 1 must clamp to the shipped grid, never invert it.");
            }
        }

        // ===== the guards that make the passthrough real, pinned in the SHADER SOURCE ======

        /// <summary>
        /// ⚠️ <b>Why this test exists, and what a sabotage taught.</b> Removing the blend-0 short
        /// circuit from <see cref="WaterDriftLines.DriftDirection"/> failed <b>0 of 13</b> tests: in C#
        /// <c>Lerp(a, b, 0)</c> returns <c>a</c> and re-normalizing an already-unit vector is bit-stable,
        /// so that short circuit is <b>defensive, not load-bearing</b>.
        ///
        /// <para>What IS load-bearing is the <b>shader's</b> two <c>if</c> guards. Blend 0 there must not
        /// merely compute the same answer — it must not run <c>FoamDriftDir()</c> or four
        /// <c>WaveFieldSample</c> taps at all, or the passthrough costs real GPU time on every material
        /// that never opted in, and the epsilon inside the shader's <c>normalize</c> gets a second
        /// application. A twin cannot see a branch, so this reads the shader SOURCE — the same thing the
        /// compile-guard fixture does, for the same reason.</para>
        /// </summary>
        [Test]
        public void ShaderGuards_KeepTheUpgradeUnreachableAtTheDefaults()
        {
            const string path = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
            string src = System.IO.File.ReadAllText(path);

            Assert.IsTrue(src.Contains("if (_DriftLineFoamDrift > 0.001)"),
                "the shared-drift blend must sit behind a guard, so blend 0 never calls FoamDriftDir().");
            Assert.IsTrue(src.Contains("if (_DriftLineConvergence > 0.001)"),
                "the convergence gate must sit behind a guard — it costs FOUR WaveFieldSample taps, " +
                "which no material that left it at 0 should pay for.");
            // …and the layer as a whole still bails before any of it when the owner has not dialled the
            // lines in at all, which is what makes the SHIPPED look untouched by inspection.
            Assert.IsTrue(src.Contains("if (_DriftLineStrength <= 0.001)"),
                "the early-out that makes the shipped look untouched must survive.");
        }

        [Test]
        public void WorldCell_ReportsTheHierarchyTheShippedNumbersAlreadyHad()
        {
            // ⚠️ The finding worth recording. DriftLines pixelizes the SCALED coord, so its world cell
            // is 1/(ppu × scale) — at PPU 32 and the shipped _DriftLineScale 0.3 that is 10.4 cm,
            // against the caustics' 3.1 cm on the RAW world grid. The scale hierarchy ADR 0027 asks
            // for therefore partly existed before anyone chose it; the divisor makes it deliberate.
            float causticCell = 1f / Ppu;                                              // 3.125 cm
            float driftCell = WaterDriftLines.WorldCellMetres(Ppu, ShippedScale, 1f);   // 10.4 cm
            Assert.AreEqual(0.104f, driftCell, 0.001f);
            Assert.Greater(driftCell, causticCell * 3f,
                "the shipped drift grid is already >3x coarser than the caustics'.");

            // …and the divisor scales it linearly, so the recommended 3 lands near 31 cm.
            Assert.AreEqual(driftCell * 3f, WaterDriftLines.WorldCellMetres(Ppu, ShippedScale, 3f), 1e-5f);
        }
    }
}
