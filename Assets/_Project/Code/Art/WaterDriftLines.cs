using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twin of the §18 drift-line UPGRADE (Arc C) — the three laws that put
    /// the shipped lines on the SHARED drift direction and the SHARED wave field instead of a private
    /// reading of the current (mirrored in <c>HiddenHarboursWater.shader</c>'s <c>DriftLines()</c>:
    /// <c>DriftBasisDir</c> / the convergence weight / <c>PixelizeGrid</c> — change one, change BOTH
    /// in the same PR).
    ///
    /// <para><b>Why an upgrade and not a new layer.</b> §18 already ships drift lines. Adding a second
    /// drift-line system beside it is precisely the mistake #323 cleaned up a week ago — eight copies
    /// of the wave settings — so nothing here duplicates the existing streak build, the sea-state bell,
    /// the wander or the tint. Three knobs join it, each defaulting to the shipped behaviour
    /// BIT-for-bit.</para>
    ///
    /// <para><b>(1) The drift direction becomes the SHARED one.</b> §18.1 keyed the streak basis to
    /// <c>_FlowDir</c> (the current) and recorded that as a deliberate correction against using
    /// <c>_WindDir</c>. That reasoning was right about wind-vs-current and still incomplete: the foam
    /// on the same surface already drifts along <c>FoamDriftDir()</c>, a wind/current BLEND that also
    /// carries the shoreward bias — so drift lines and the foam they are made of were reading two
    /// different directions. Real windrows align with that blend, not with either force alone.
    /// <see cref="DriftDirection"/> dials from today's raw current to the shared blend, so the §18.1
    /// decision becomes a tunable rather than a hard-coded choice, and blend 0 is exactly today.</para>
    ///
    /// <para><b>(2) Scum GATHERS where the surface converges.</b> A drift line is not a texture that
    /// happens to be long — it is floating material collected on a convergence line, which is why real
    /// ones sit in bands with clean water between them. The shared field already exposes that term:
    /// ADR 0027 #3's <c>ConvergenceGate</c> (twinned by <see cref="WaterFoam"/>). <see cref="ConvergenceWeight"/>
    /// blends the lanes from "everywhere" toward "only where the surface pinches", reusing that gate
    /// rather than computing a second opinion about the same physics.</para>
    ///
    /// <para><b>(3) The grid is chosen, not inherited.</b> ADR 0027's Pixelation section asks for
    /// deliberately DIFFERENT grids per layer instead of every edge snapping to one shared lattice.
    /// <see cref="PixelizeGrid"/> makes the drift lines' cell an explicit multiple of the PPU cell —
    /// divisor 1 reproduces the shipped grid exactly. See <see cref="WorldCellMetres"/> for what the
    /// shipped numbers already were, which is not what one might assume.</para>
    ///
    /// <para>Everything here drives <c>col.rgb</c> only — never <c>depth</c>/<c>clip()</c>/
    /// <c>_WaterLevel</c>/the height read/<c>_WaveFieldParams</c>/anything the hulls ride (P1
    /// integrity, CLAUDE.md rule 5; the interior-mask clamp stack never notices). Nothing is
    /// saved.</para>
    /// </summary>
    public static class WaterDriftLines
    {
        /// <summary>Floor for the pixels-per-unit grid (mirrors the shader's <c>max(_PixelsPerUnit, 1)</c>).</summary>
        public const float MinPixelsPerUnit = 1f;

        /// <summary>Floor for the grid divisor: 1 = the shipped PPU cell, the passthrough.</summary>
        public const float MinGridDivisor = 1f;

        /// <summary>
        /// (1) The streak basis direction: today's current, dialled toward the SHARED foam drift.
        /// <b>Blend 0 returns the normalized current EXACTLY</b> — the passthrough contract — and the
        /// caller feeds the same <c>FoamDriftDir()</c> the foam and whitecaps already drift along, so
        /// this cannot drift out of step with them.
        /// </summary>
        /// <param name="flowDir">The raw current direction (<c>_FlowDir.xy</c>), un-normalized is fine.</param>
        /// <param name="foamDriftDir">The shared wind/current/shoreward blend (<c>FoamDriftDir()</c>).</param>
        /// <param name="blend">0 = today's current, 1 = the shared drift (<c>_DriftLineFoamDrift</c>).</param>
        public static Vector2 DriftDirection(Vector2 flowDir, Vector2 foamDriftDir, float blend)
        {
            Vector2 current = Safe(flowDir, new Vector2(1f, 0f));
            float b = Mathf.Clamp01(blend);
            if (b <= 0f) return current;                    // EXACT passthrough — no lerp, no re-normalize
            Vector2 shared = Safe(foamDriftDir, current);
            return Safe(Vector2.Lerp(current, shared, b), current);
        }

        /// <summary>
        /// (2) How much the lanes are confined to convergence lines. <b>Blend 0 returns exactly 1</b>
        /// (lanes read wherever the noise puts them — today), 1 returns the convergence gate itself
        /// (lanes only where the surface pinches). The gate comes from
        /// <see cref="WaterFoam.Convergence"/>, so the drift lines and the convergence FOAM agree
        /// about where the surface is folding by construction.
        /// </summary>
        public static float ConvergenceWeight(float convergence, float blend)
        {
            return Mathf.Lerp(1f, Mathf.Clamp01(convergence), Mathf.Clamp01(blend));
        }

        /// <summary>
        /// (3) Snap a coordinate onto a grid <paramref name="divisor"/> times COARSER than the PPU
        /// cell. <b>Divisor 1 is bit-identical to the shader's <c>Pixelize</c></b>
        /// (<c>floor(p·ppu)/ppu</c>) — the passthrough.
        /// </summary>
        public static Vector2 PixelizeGrid(Vector2 p, float pixelsPerUnit, float divisor)
        {
            float ppu = Mathf.Max(pixelsPerUnit, MinPixelsPerUnit) / Mathf.Max(divisor, MinGridDivisor);
            return new Vector2(Mathf.Floor(p.x * ppu) / ppu, Mathf.Floor(p.y * ppu) / ppu);
        }

        /// <summary>
        /// The drift lines' cell size in WORLD metres, which is the number the scale-hierarchy question
        /// is actually about.
        ///
        /// <para>⚠️ <b>The shipped lines were ALREADY coarser than the caustics, by accident of the
        /// scale multiply.</b> <c>DriftLines</c> pixelizes the SCALED coordinate
        /// (<c>Pixelize(worldXY · _DriftLineScale)</c>), so its quantum is <c>1/ppu</c> in scaled space
        /// and <c>1/(ppu·scale)</c> in world metres — at the shipped PPU 32 and
        /// <c>_DriftLineScale</c> 0.3 that is <b>10.4 cm</b>, against the caustics' 3.1 cm on the raw
        /// world grid. So the hierarchy ADR 0027 asks for partly existed without anyone choosing it.
        /// This makes it explicit and tunable instead.</para>
        /// </summary>
        public static float WorldCellMetres(float pixelsPerUnit, float scale, float divisor)
        {
            float ppu = Mathf.Max(pixelsPerUnit, MinPixelsPerUnit);
            float s = Mathf.Max(Mathf.Abs(scale), 1e-4f);
            return Mathf.Max(divisor, MinGridDivisor) / (ppu * s);
        }

        private static Vector2 Safe(Vector2 v, Vector2 fallback)
        {
            return v.sqrMagnitude > 1e-8f ? v.normalized : fallback;
        }
    }
}
