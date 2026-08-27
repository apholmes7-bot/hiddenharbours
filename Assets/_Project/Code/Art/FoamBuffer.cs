using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twin of the ADR 0027 #6 ADVECTED FOAM BUFFER — the cell law, the
    /// decay, the whole-cell advection and the injection shaping (mirrored in
    /// <c>Art/Shaders/FoamBufferAdvect.shader</c>; change one, change BOTH in the same PR, and the
    /// <c>FoamBufferTests</c> tripwires read the shader source to prove it).
    ///
    /// <para><b>What the buffer is.</b> One persistent single-channel render target, ping-ponged per
    /// frame, holding "how much churned foam is on this patch of sea". Every frame it is scrolled,
    /// decayed and injected into where hulls disturb the water; the water shader samples it as a mask
    /// that <b>ADDS</b> to the existing foam. It is a mark left on a PLACE IN THE SEA — foam the
    /// shipped <c>BoatWakeEmitter</c> trail cannot make, because it persists and drifts downwind
    /// after the boat has gone, and because it churns around a hull that is merely BOBBING.</para>
    ///
    /// <para>🔴 <b>THE CELL LAW — the one thing this item can get catastrophically wrong.</b> A render
    /// target is screen-space by nature. If the buffer's cells are camera-relative, the whole wake
    /// crawls under every pan and the effect reads as a screen filter rather than as foam on water.
    /// So the window's origin is snapped onto a WORLD cell lattice (<see cref="WorldCellOrigin"/>):
    /// camera-relative ADDRESSING only, and every scroll is a WHOLE number of world cells, which also
    /// makes the frame-to-frame copy an exact texel move with no resampling blur.
    /// <see cref="CameraRelativeOrigin"/> exists ONLY as the wrong answer the tests measure the crawl
    /// against; nothing ships calling it.</para>
    ///
    /// <para>⚠️ <b>Its own grid constant, deliberately.</b> <see cref="CellsPerUnit"/> is NOT the
    /// material's <c>_PixelsPerUnit</c>. That property is an ART knob the owner may drag; the C# side
    /// cannot read a material, so quantizing through it would let the two halves of this seam drift
    /// silently. Same ruling, same reason, as <c>WaveFetch.PixelsPerUnit</c> / <c>FETCH_MARCH_PPU</c>.
    /// It is also deliberately COARSER than the pixel grid (8 cells/m = one cell per 4 screen px at
    /// PPU 32), which is the ADR's own "deliberately different grids per layer — foam coarser than
    /// caustics" note taken up rather than ignored.</para>
    ///
    /// <para><b>Determinism boundary, stated honestly.</b> This buffer is ACCUMULATED VISUAL STATE.
    /// It is not a deterministic function of <c>(worldSeed, gameTime)</c>, it is allowed to differ
    /// run-to-run with frame pacing (exactly like particles), it feeds NO simulation, and it enters no
    /// save (rule 5 / ADR 0008). Nothing but the water shader's foam compose may ever read it. Every
    /// function here is nonetheless pure and deterministic in its own arguments — that is what makes
    /// the maths testable headless even though the accumulation is not.</para>
    /// </summary>
    public static class FoamBuffer
    {
        // ---- the grid ---------------------------------------------------------------------------

        /// <summary>
        /// The buffer's OWN world grid: cells per metre. Mirrored in the advect shader as
        /// <c>FOAM_CELLS_PER_UNIT</c> and pinned by <c>FoamBufferTests.CellGrid_MatchesTheShader</c>.
        ///
        /// <para>8 cells/m = 0.125 m per cell = <b>4 screen pixels</b> at the project's locked PPU 32
        /// — coarser than the water's per-pixel layers on purpose (the ADR's scale-hierarchy note),
        /// so wake foam reads as chunky churn rather than as a smooth airbrushed smear. ⚠️ Never
        /// replace this with <c>_PixelsPerUnit</c>: see the class doc.</para>
        /// </summary>
        public const float CellsPerUnit = 8f;

        /// <summary>
        /// How many hulls can inject in one frame. The advect shader loops over exactly this many
        /// slots as a COMPILE-TIME constant — never a runtime bound, because
        /// <c>WaterShaderCompileGuardTests</c> names <c>[unroll]</c> over a runtime bound as a known
        /// magenta trap. Unused slots carry amount 0 and contribute exactly nothing. Mirrored as
        /// <c>FOAM_MAX_INJECTORS</c>.
        /// </summary>
        public const int MaxInjectors = 8;

        /// <summary>Floor on the world window so a degenerate extent can never divide by zero.</summary>
        public const float MinExtentMeters = 1f;

        /// <summary>Floor on the slap shaping exponent — 1 is linear, and below that the curve stops
        /// distinguishing a slap from a swell.</summary>
        public const float MinShapeExponent = 1f;

        /// <summary>The size of one buffer cell, in metres (the reciprocal of <see cref="CellsPerUnit"/>).</summary>
        public static float CellSize => 1f / CellsPerUnit;

        /// <summary>
        /// The texel resolution a world window of <paramref name="extentMetres"/> needs so that one
        /// texel is EXACTLY one world cell. Deriving the resolution from the extent (rather than
        /// letting both be free) is what makes "one texel = one world cell" true by construction
        /// instead of by careful tuning.
        /// </summary>
        public static int ResolutionForExtent(float extentMetres)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(extentMetres, MinExtentMeters) * CellsPerUnit));
        }

        /// <summary>Snap a world position DOWN onto the buffer's cell lattice — the buffer's own
        /// <c>Pixelize</c>, on its own grid.</summary>
        public static Vector2 SnapToCells(Vector2 p)
        {
            return new Vector2(Mathf.Floor(p.x * CellsPerUnit) / CellsPerUnit,
                               Mathf.Floor(p.y * CellsPerUnit) / CellsPerUnit);
        }

        /// <summary>
        /// 🔴 <b>THE CELL LAW.</b> The world position of the buffer window's lower-left corner for a
        /// window of <paramref name="extentMetres"/> centred near <paramref name="centreXY"/> (in
        /// practice the camera): the ideal corner snapped DOWN onto the world cell lattice.
        ///
        /// <para>The consequence is the whole item: this result <b>does not move at all</b> for a
        /// camera pan smaller than one cell, and when it does move it moves by a WHOLE number of
        /// cells. So a foam cell belongs to a place on the sea and stays there while the camera
        /// moves (zero crawl by construction), and the frame-to-frame scroll is an exact integer
        /// texel copy rather than a resample (no accumulating blur). Compare
        /// <see cref="CameraRelativeOrigin"/>.</para>
        /// </summary>
        public static Vector2 WorldCellOrigin(Vector2 centreXY, float extentMetres)
        {
            float half = Mathf.Max(extentMetres, MinExtentMeters) * 0.5f;
            return SnapToCells(new Vector2(centreXY.x - half, centreXY.y - half));
        }

        /// <summary>
        /// 🔴 <b>THE ORIGIN THE SHADERS ARE GIVEN</b> — the cell-snapped lattice origin plus the
        /// sub-cell drift that has been BANKED but not yet spent as a whole-cell scroll.
        ///
        /// <para><b>The defect this retires</b> (owner eyeball 2026-08-27: <i>"the whole foam band
        /// shifts by 1–2 px as ONE unit … it's noticeable it's a separate entity from the water; they
        /// shift in large groups"</i>). <see cref="AdvectCells"/> moves the buffer's content downwind in
        /// WHOLE cells and banks the remainder, which is right — a sub-texel scroll would resample the
        /// buffer into itself every frame and smudge the wake. But the content was also DRAWN at the
        /// lattice origin, so the banked remainder was invisible until it crossed a whole cell and the
        /// entire band teleported 0.125 m (4 screen px at PPU 32) at once. Every texel moved together,
        /// because a buffer scroll is rigid: that is the "one unit / large groups" read exactly, and it
        /// happened while the sea around it flowed continuously.</para>
        ///
        /// <para><b>The fix is one addition, and it is exact.</b> Draw at
        /// <c>lattice + residual</c>. Let <c>C</c> be the content's cumulative whole-cell displacement
        /// and <c>R</c> the banked remainder; <see cref="AdvectCells"/> guarantees
        /// <c>C + R</c> increases by exactly this frame's drift, for ANY drift sequence. So the DRAWN
        /// position advances smoothly at the true drift speed, and on the frame the content jumps a
        /// whole cell the residual drops by the same whole cell — the jump cancels to nothing. The foam
        /// never moves relative to the water it sits in; it only ever drifts, at the speed it should.
        /// Zero drift ⇒ <c>R</c> = 0 ⇒ this returns the lattice origin BIT-EXACTLY, which is the A/B.</para>
        ///
        /// <para>Both shaders take this one origin: the water's read maps a world position through it,
        /// and the advect pass stamps new injections through it, so a mark is born exactly under the
        /// hull that made it and then drifts. Feeding them different origins would tear the two apart
        /// by the residual — the <c>compositing-window-must-follow-the-ride</c> lesson, in a second
        /// window.</para>
        /// </summary>
        public static Vector2 DrawOrigin(Vector2 latticeOrigin, Vector2 driftResidualMeters)
        {
            return latticeOrigin + driftResidualMeters;
        }

        /// <summary>
        /// ⚠️ <b>THE WRONG ANSWER, kept so the tests can measure how wrong.</b> The window origin
        /// taken straight off the camera with no world snap — the natural thing to reach for once the
        /// buffer lives in a render target, and precisely what the ADR warns against.
        ///
        /// <para>It slides continuously under the sea, so a fixed patch of water lands in a DIFFERENT
        /// buffer cell on every sub-cell pan: the whole trail crawls. <c>FoamBufferTests</c> reports
        /// that as a camera-offset-dependent cell delta rather than as an adjective. Nothing ships
        /// calling this. (Sibling of <c>WaterReflectionWarp.ScreenSnappedSampleWorld</c>.)</para>
        /// </summary>
        public static Vector2 CameraRelativeOrigin(Vector2 centreXY, float extentMetres)
        {
            float half = Mathf.Max(extentMetres, MinExtentMeters) * 0.5f;
            return new Vector2(centreXY.x - half, centreXY.y - half);
        }

        /// <summary>
        /// Which buffer cell a world position falls in, given a window origin. Integer by
        /// construction — this is the index the crawl tests watch: with
        /// <see cref="WorldCellOrigin"/> a fixed world point keeps its cell across sub-cell pans,
        /// with <see cref="CameraRelativeOrigin"/> it does not.
        /// </summary>
        public static Vector2Int SampleCell(Vector2 worldXY, Vector2 originWorld)
        {
            return new Vector2Int(Mathf.FloorToInt((worldXY.x - originWorld.x) * CellsPerUnit),
                                  Mathf.FloorToInt((worldXY.y - originWorld.y) * CellsPerUnit));
        }

        /// <summary>The buffer UV a world position maps to (0..1 across the window). The water
        /// shader's read; world-anchored, so it inherits the cell law for free.</summary>
        public static Vector2 SampleUv(Vector2 worldXY, Vector2 originWorld, float extentMetres)
        {
            float extent = Mathf.Max(extentMetres, MinExtentMeters);
            return new Vector2((worldXY.x - originWorld.x) / extent, (worldXY.y - originWorld.y) / extent);
        }

        /// <summary>
        /// The WHOLE-CELL texel shift between two window origins — how far the previous frame's
        /// content must move to stay on the same water. Both origins are on the cell lattice, so this
        /// is exact integer arithmetic and never a rounding guess.
        /// </summary>
        public static Vector2Int OriginShiftCells(Vector2 previousOrigin, Vector2 newOrigin)
        {
            return new Vector2Int(Mathf.RoundToInt((previousOrigin.x - newOrigin.x) * CellsPerUnit),
                                  Mathf.RoundToInt((previousOrigin.y - newOrigin.y) * CellsPerUnit));
        }

        /// <summary>
        /// The integer texel offset the advect pass ADDS to a destination texel to find its SOURCE
        /// in the previous buffer: <c>dst[i] = prev[i + offset]</c>.
        ///
        /// <para>Content moves by <c>−offset</c>, so this is the negation of everything the content
        /// should move by this frame — the window's own move (<see cref="OriginShiftCells"/>) plus
        /// the wind drift (<see cref="AdvectCells"/>). It exists as one named function precisely
        /// because the sign is the easy thing to get backwards, and backwards means the wake slides
        /// the wrong way under every pan; <c>FoamBufferTests</c> pins it by following a mark on a
        /// fixed patch of water across a camera move.</para>
        /// </summary>
        public static Vector2Int SourceOffsetCells(Vector2 previousOrigin, Vector2 newOrigin,
                                                   Vector2Int driftCells)
        {
            Vector2Int contentMove = OriginShiftCells(previousOrigin, newOrigin) + driftCells;
            return new Vector2Int(-contentMove.x, -contentMove.y);
        }

        // ---- decay ------------------------------------------------------------------------------

        /// <summary>
        /// The per-step multiplier that halves the buffer every <paramref name="halfLifeSeconds"/>:
        /// <c>0.5 ^ (dt / halfLife)</c>. Exponential, so it composes — ten small steps decay exactly
        /// as far as one big one, and the trail's lifetime is independent of frame rate.
        ///
        /// <para><paramref name="halfLifeSeconds"/> ≤ 0 returns 0 (the foam dies instantly rather
        /// than living forever — the safe end of a degenerate tunable). dt ≤ 0 returns 1.</para>
        /// </summary>
        public static float DecayFactor(float halfLifeSeconds, float dt)
        {
            if (dt <= 0f) return 1f;
            if (halfLifeSeconds <= 0f) return 0f;
            return Mathf.Pow(0.5f, dt / halfLifeSeconds);
        }

        /// <summary>One decay step applied to a stored value.</summary>
        public static float Decay(float value, float halfLifeSeconds, float dt)
        {
            return value * DecayFactor(halfLifeSeconds, dt);
        }

        // ---- freshness: the SECOND channel, and the reason the wake can change colour -------------

        /// <summary>
        /// 🔴 <b>THE AGE CHANNEL.</b> One step of the buffer's <b>G</b> channel: decay what was there,
        /// then take the MAX against this frame's churn mark. Twin: <c>FoamBufferAdvect.shader</c>'s
        /// <c>fresh</c> line. Mirrored, tested, and — like the R channel — pure in its arguments.
        ///
        /// <para><b>Why a second channel exists at all, measured rather than argued.</b> #665 derived a
        /// texel's age from its COVERAGE (<c>age = 1 − coverage/fresh</c>), on the reasoning that a
        /// decaying buffer's surviving coverage IS its freshness. The owner's eyeball found the band
        /// still solid white, and the numbers say why: coverage is <b>saturated</b> and then
        /// <b>quantized</b> before anything reads it. A dory at 3 m/s lays 36 frames of deposit into
        /// one texel and pins R at 1.000; the water shader then thresholds
        /// (<c>smoothstep(0.12, 0.30)</c>) and posterizes to 3 bands, so the value the age proxy sees
        /// can only ever be one of {0, 0.425, 0.85}. 72–81% of the visible band therefore drew at age
        /// exactly 0 — white — at every speed. No threshold retune can recover a gradient from three
        /// values; it can only pick which flat colour the band is. <c>WakeFoamAgeingMeasurementTests</c>
        /// keeps that measurement red if it is ever true again.</para>
        ///
        /// <para><b>Why MAX and not ADD.</b> Adding is what saturated the coverage channel. Freshness is
        /// not an amount, it is a CLOCK: churn resets it to how hard the hull is working right now, and
        /// nothing else can raise it. Because the mark is bounded by 1 and the update takes a max, this
        /// channel can never clamp, so it is monotone in time-since-churn by construction — which is
        /// exactly the property coverage lost. Fresh churn reads 1.0 for every hull at every speed, so
        /// the ramp means the same thing everywhere; the fast hull whose thin coverage never reached
        /// the old threshold is no longer born blue.</para>
        ///
        /// <para><paramref name="mark01"/> is the injection's <b>vigour</b> (0..1, dt-independent) times
        /// its band falloff, so the churned heart is born white and the torn fringe is born already a
        /// step down the ramp — which is what the edge of a churn looks like, and it costs nothing.</para>
        /// </summary>
        public static float Freshness(float previous, float decayFactor, float mark01)
        {
            float decayed = Mathf.Clamp01(previous) * Mathf.Clamp01(decayFactor);
            return Mathf.Max(decayed, Mathf.Clamp01(mark01));
        }

        // ---- advection --------------------------------------------------------------------------

        /// <summary>
        /// Turn a continuous drift (metres this frame, along the shared <c>FoamDriftDir</c>) into a
        /// WHOLE-CELL step plus the sub-cell remainder to carry into the next frame.
        ///
        /// <para><b>Why whole cells and not a sub-texel slide.</b> A sub-texel advection has to be
        /// resampled, and resampling a buffer into itself every frame is a blur filter: after a few
        /// seconds the wake is a smudge, which is the opposite of pixel art. Accumulating the
        /// remainder instead means the drift is not lost — it is banked until it is worth a whole
        /// cell, so the foam travels at exactly the right speed while every individual move stays an
        /// exact texel copy. The camera scroll and the wind drift therefore obey ONE law.</para>
        /// </summary>
        /// <param name="residualMeters">Carried sub-cell remainder; updated in place.</param>
        /// <param name="driftMeters">This frame's drift (direction × speed × dt), in metres.</param>
        /// <returns>The whole-cell step to scroll the buffer content by.</returns>
        public static Vector2Int AdvectCells(ref Vector2 residualMeters, Vector2 driftMeters)
        {
            Vector2 total = residualMeters + driftMeters;
            var cells = new Vector2Int(Mathf.FloorToInt(total.x * CellsPerUnit),
                                       Mathf.FloorToInt(total.y * CellsPerUnit));
            residualMeters = new Vector2(total.x - cells.x / CellsPerUnit,
                                         total.y - cells.y / CellsPerUnit);
            return cells;
        }

        // ---- injection --------------------------------------------------------------------------

        /// <summary>
        /// The shaping curve every injection channel runs through: normalise a rate against its
        /// <paramref name="knee"/> (the rate that saturates the channel), then raise it to
        /// <paramref name="exponent"/>.
        ///
        /// <para><b>The exponent is the owner's ask made numeric.</b> At 1 the response is linear and
        /// a gentle rise-and-fall churns half as much as a slap twice as hard. Above 1 it is
        /// super-linear: the gentle end is pushed down toward nothing while a hard slap still reaches
        /// full white — which is what "a hull slapping at the waterline churns foam, a hull breathing
        /// on a swell does not" means as a curve. Every constant here is a tunable (rule 6), never a
        /// literal in a component.</para>
        /// </summary>
        public static float Shape01(float rate, float knee, float exponent)
        {
            if (rate <= 0f || knee <= 0f) return 0f;
            float x = Mathf.Clamp01(rate / knee);
            return Mathf.Pow(x, Mathf.Max(exponent, MinShapeExponent));
        }

        /// <summary>
        /// How much foam a hull lays down this instant, 0..1, from BOTH components of its motion
        /// relative to the water — the whole point of the item:
        ///
        /// <list type="bullet">
        /// <item><b>Horizontal</b> — speed through the water. The classic wake case, already served
        /// visually by <c>BoatWakeEmitter</c>; here it becomes foam that stays on the sea.</item>
        /// <item><b>Vertical</b> — the hull's heave RATE relative to the local wave surface
        /// (<see cref="RelativeHeaveRate"/>). This is the case nothing served before, and the reason
        /// the owner moved this item forward: a dory bobbing at her mooring works against the water
        /// and churns, even at zero speed.</item>
        /// </list>
        ///
        /// <para>The two are ADDED and clamped, so a boat punching to windward in a chop churns more
        /// than either alone, and a moored boat in a slop still churns with no way on at all.</para>
        /// </summary>
        public static float Injection01(float horizontalSpeed, float verticalRate,
                                        float wakeKnee, float wakeExponent, float wakeWeight,
                                        float slapKnee, float slapExponent, float slapWeight)
        {
            float wake = Shape01(horizontalSpeed, wakeKnee, wakeExponent) * Mathf.Max(wakeWeight, 0f);
            float slap = Shape01(verticalRate, slapKnee, slapExponent) * Mathf.Max(slapWeight, 0f);
            return Mathf.Clamp01(wake + slap);
        }

        /// <summary>
        /// The hull's vertical position as a first-order lag of the sea surface it floats on — the
        /// hull's INERTIA, and the only thing that makes bobbing churn at all.
        ///
        /// <para>A hull with no inertia would ride the surface exactly, its relative velocity would be
        /// identically zero, and a bobbing boat would leave the water undisturbed. Real hulls lag,
        /// and the lag IS the slap. <paramref name="tauSeconds"/> = 0 returns the surface exactly,
        /// which is that no-churn limit — a genuine off switch, not an approximation of one.</para>
        ///
        /// <para><b>What this reads and what it must never do.</b> The surface height fed in is the
        /// same displaced sea the ride already samples per hull; this models the hull's response to
        /// it and <b>never writes anything back to Boats</b>. It is a read of the ride, not a second
        /// opinion about it.</para>
        /// </summary>
        public static float FollowSurface(float previousHullY, float surfaceY, float tauSeconds, float dt)
        {
            if (tauSeconds <= 0f || dt <= 0f) return surfaceY;
            float alpha = 1f - Mathf.Exp(-dt / tauSeconds);
            return previousHullY + (surfaceY - previousHullY) * alpha;
        }

        /// <summary>
        /// |relative vertical velocity| between hull and water surface, m/s — the rate at which the
        /// GAP between them is opening or closing. Zero whenever the hull tracks the surface exactly
        /// (see <see cref="FollowSurface"/>), largest when a steep face arrives faster than the hull
        /// can rise to it: the slap.
        /// </summary>
        public static float RelativeHeaveRate(float surfaceY, float hullY,
                                              float previousSurfaceY, float previousHullY, float dt)
        {
            if (dt <= 0f) return 0f;
            return Mathf.Abs((surfaceY - hullY) - (previousSurfaceY - previousHullY)) / dt;
        }
    }
}
