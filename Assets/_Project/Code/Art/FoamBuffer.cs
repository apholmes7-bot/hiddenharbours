using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twin of the ADR 0027 #6 ADVECTED FOAM BUFFER — the cell law, the
    /// decay, the whole-cell advection and the injection shaping (mirrored in
    /// <c>Art/Shaders/FoamBufferAdvect.shader</c>; change one, change BOTH in the same PR, and the
    /// <c>FoamBufferTests</c> tripwires read the shader source to prove it).
    ///
    /// <para><b>What the buffer is.</b> One persistent TWO-channel render target, ping-ponged per
    /// frame, holding <b>R</b> = "how much churned foam is on this patch of sea" and <b>G</b> = "how
    /// recently it was churned" (<see cref="Freshness"/> — the clock the wake's colour walks down).
    /// Every frame it is scrolled, decayed and injected into where hulls disturb the water; the water
    /// shader samples R as a mask that <b>ADDS</b> to the existing foam and G as the age that picks its
    /// colour. It is a mark left on a PLACE IN THE SEA — foam the
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

        /// <summary>ADR 0040 rev 3: foam laid per second under a full-strength bore front, before the
        /// owner's <c>_SurfDepositStrength</c>. Twin: <c>SURF_DEPOSIT_RATE</c> in the advect shader.</summary>
        public const float SurfDepositRatePerSecond = 2f;

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

        // ---- where the trail is laid ------------------------------------------------------------

        /// <summary>
        /// The world point a hull sheds her churn at: <paramref name="sternOffsetMeters"/> back from her
        /// origin along her heading, foreshortened in Y the way her art is drawn.
        ///
        /// <para>The foreshortening is not decoration. The stern is a distance ON THE WATER, and a 3/4
        /// camera draws that distance in full to the east and only <c>sin(elevation)</c> of it to the
        /// north — so an unprojected anchor would sit off the transom by an amount that OPENS AND CLOSES
        /// through every turn. (The plume anchor paid for this exact lesson: "not even connected to it and
        /// way off to the stern".)</para>
        ///
        /// <para><paramref name="sternOffsetMeters"/> 0 returns the origin unchanged, which is the shipped
        /// behaviour for a hull whose stern has never been measured. Pure + static.</para>
        /// </summary>
        /// <remarks>⚠️ Row 29: this is now a DELEGATION, not a second copy. It kept its name and signature
        /// so PR 11a/11b's guards read unchanged, but the arithmetic is
        /// <see cref="HiddenHarbours.Core.WakeRootMath.SternWorld"/> — the one place a wake springs from,
        /// shared with the sprite families in HiddenHarbours.Boats. The old body clamped the elevation to
        /// [1, 90] where the sprite path answered 1 at or below 0; one function, one answer.</remarks>
        public static Vector2 SternWorld(Vector2 origin, Vector2 heading, float sternOffsetMeters,
                                         float bakeElevationDegrees)
            => HiddenHarbours.Core.WakeRootMath.SternWorld(origin, heading, sternOffsetMeters,
                                                           bakeElevationDegrees);

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
        /// <para><paramref name="mark01"/> is <b>1 wherever a hull churned this water this frame</b> and 0
        /// elsewhere — deliberately NOT scaled by how hard she was working. A clock scaled by vigour
        /// conflates "how hard" with "how long ago": a dory at half her knee speed would be born
        /// half-aged and could never make white foam at all, which is the same category error round 1
        /// made with the coverage channel, one level down. (Caught by
        /// <c>WakeFoamAgeingMeasurementTests</c> on the first run, at 1.5 m/s.) How MUCH foam is on this
        /// water is the R channel's job, and it already does it.</para>
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

        // ---- DISPERSAL: the trail widens and thins with age (water fidelity PR 11b) ---------------
        //
        // 🔴 THE OWNER, 2026-09-04 and 09-06: "foam fades behind boat but doesnt disperse in width" ·
        // "the foam always stays to the original foam path, it doesnt widen over time and fade away."
        //
        // WHY NOTHING HERE BLURS. The buffer's cell law makes every scroll an exact integer texel
        // Load (see AdvectCells), which is what stops a wake smudging under a camera pan — so the
        // buffer has no diffusion term and a laid trail can fade but cannot widen. The dispersal is
        // therefore evaluated at INJECTION, per deposit, against that deposit's own AGE. Nothing is
        // ever applied to the target between frames.
        //
        // THE TERM, IN ONE LINE. A deposit's profile is taken to widen with age at a fraction of the
        // Kelvin slope while its amount falls by the ratio of the profiles' integrals — the
        // AREA-PRESERVING family P_w(d) = (Phi / 1.5w) * falloff(d/w), every member of which lays the
        // same foam. Widening then changes the DISTRIBUTION of the churn and never its total, which
        // is exactly what the naive skirt could not do (it multiplied by 5.6x, measured).
        //
        // ⚠️ AN ADDITIVE BUFFER RETAINS THE ENVELOPE OF THAT FAMILY, NOT ITS CURRENT MEMBER. Nothing
        // can take foam back out of the core as the profile spreads, so what the sea ends up holding
        // is max over w of P_w(d) — EnvelopeShape. Stating conservation on the instantaneous profile
        // and stamping it anyway is how an area-preserving term still overshoots; conservation is
        // stated on the envelope (EnvelopeIntegral) and the stamp lays that envelope ONCE at each
        // lateral distance, as its outer edge sweeps past. That single-pass stamp is also what keeps
        // the per-frame amount clear of the buffer's 8-bit rounding floor: the same term re-stamped
        // over the whole trail every frame lands BELOW it and is quantized away
        // (docs/design/water-rendering.md §35 carries both sets of numbers).

        /// <summary>
        /// The Kelvin wake's half-angle as a SLOPE — metres of half-width per metre of track. The
        /// physical outer limit a hull's disturbance can spread at, and therefore the bound on
        /// <see cref="SpreadSlope"/>: the churn inside the arms spreads slower, never faster.
        /// </summary>
        public static readonly float KelvinSlope = Mathf.Tan(19.5f * Mathf.Deg2Rad);

        /// <summary>
        /// The AREA integral of the shipped injection stamp, in units of its own radius squared:
        /// <c>∬ falloff(|p|) dA = 2π ∫₀¹ falloff(ρ)·ρ dρ = 0.575π</c>.
        ///
        /// <para>This — not the lateral cross-section — is what the shipped disc actually lays on a
        /// parcel of sea, because a parcel is stamped for its whole DWELL under the passing hull
        /// rather than once. It is the conserved quantity: foam laid per metre of track is
        /// <c>rate · StampAreaIntegral · r₀² / speed</c>. Reaching for the 1.5·r₀ cross-section
        /// instead makes an "area-preserving" stamp lay 1.4× the shipped foam — measured, and the
        /// reason this constant is named rather than inlined.</para>
        /// </summary>
        public const float StampAreaIntegral = 0.575f * Mathf.PI;

        /// <summary>The lateral integral of the shipped profile, per unit half-width:
        /// <c>∫ falloff(|d|/w) dd = 1.5·w</c>, exactly.</summary>
        public const float ProfileLateralIntegral = 1.5f;

        /// <summary>
        /// <c>argmax x·falloff(x)</c> — where a member of the area-preserving family peaks once its
        /// amplitude has been scaled by 1/w. Root of <c>8t³ − 3t² − 6t + 1</c> at <c>t = 2x − 1</c>;
        /// <c>WakeDispersalMathTests</c> re-derives it by search rather than trusting this number.
        /// </summary>
        public const float EnvelopePeakX = 0.5796825f;

        /// <summary>The value of <c>x·falloff(x)</c> at <see cref="EnvelopePeakX"/> — the envelope's
        /// own constant, so that the envelope is <c>EnvelopePeak / d</c> across the middle of its
        /// range: the widening band thins as 1/d, which is the whole look.</summary>
        public const float EnvelopePeak = 0.5402084f;

        /// <summary>
        /// The shipped injection profile as a function of distance from the swept segment — the
        /// C# twin of the advect shader's <c>falloff</c> line. Full along the line, gone by
        /// <paramref name="halfWidth"/>.
        /// </summary>
        public static float Profile(float distance, float halfWidth)
        {
            if (halfWidth <= 0f) return 0f;
            float half = halfWidth * 0.5f;
            if (distance <= half) return 1f;
            if (distance >= halfWidth) return 0f;
            float t = (distance - half) / half;
            return 1f - (3f * t * t - 2f * t * t * t);
        }

        /// <summary>
        /// The lateral spread rate as a SLOPE, from the owner-facing dial: a fraction of the Kelvin
        /// slope, clamped to it. 0 is the passthrough — no dispersal, and the injector emits exactly
        /// the shipped stamp.
        /// </summary>
        public static float SpreadSlope(float kelvinFraction)
        {
            return Mathf.Clamp01(kelvinFraction) * KelvinSlope;
        }

        /// <summary>
        /// <c>∫₀ˣ falloff(u) du</c> in closed form — the running lateral integral of the profile,
        /// needed by <see cref="DispersingShare"/>. <c>F(1) = 0.75</c>, which is the 1.5 of
        /// <see cref="ProfileLateralIntegral"/> halved.
        /// </summary>
        public static float ProfileRunningIntegral(float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 0.75f;
            if (x <= 0.5f) return x;
            float t = 2f * x - 1f;
            return 0.5f + 0.5f * (t - t * t * t + 0.5f * t * t * t * t);
        }

        /// <summary>
        /// 🔴 <b>THE DISPERSING SHARE, DERIVED — not a third dial.</b> How much of a hull's churn
        /// leaves her own band: the fraction of the fully-spread profile that lies OUTSIDE the
        /// original half-width. <c>β = 1 − (4/3)·F(r₀/w_max)</c>.
        ///
        /// <para>It falls out of the geometry rather than being tuned, and it is 0 exactly when
        /// <paramref name="maxHalfWidth"/> equals <paramref name="halfWidth"/> — which is why the
        /// spread dial at 0 hands the whole deposit back to the shipped stamp, bit for bit. At a
        /// 2× envelope it is 1/3; at 2.5× it is 7/15.</para>
        /// </summary>
        public static float DispersingShare(float halfWidth, float maxHalfWidth)
        {
            if (halfWidth <= 0f || maxHalfWidth <= halfWidth) return 0f;
            return Mathf.Clamp01(1f - (4f / 3f) * ProfileRunningIntegral(halfWidth / maxHalfWidth));
        }

        /// <summary>
        /// 🔴 <b>THE ENVELOPE</b> — what an additive buffer is left holding once the area-preserving
        /// family <c>P_w(d) ∝ falloff(d/w)/w</c> has widened from <paramref name="halfWidth"/> to
        /// <paramref name="maxHalfWidth"/>: <c>max over w of falloff(d/w)/w</c>, in units of
        /// 1/metres.
        ///
        /// <para>Flat at <c>1/r₀</c> across the core, <see cref="EnvelopePeak"/><c>/d</c> through the
        /// middle, and tapering to exactly 0 at <paramref name="maxHalfWidth"/> because the widest
        /// member of the family does. No shaping choice is made here: the family's own outer taper
        /// IS the band's outer edge, so there is no third curve to tune.</para>
        /// </summary>
        public static float EnvelopeShape(float distance, float halfWidth, float maxHalfWidth)
        {
            if (halfWidth <= 0f || distance < 0f) return 0f;
            float wMax = Mathf.Max(maxHalfWidth, halfWidth);
            if (distance >= wMax) return 0f;
            if (distance <= 1e-6f) return 1f / halfWidth;
            // x = distance/w, so w over [r0, wMax] is x over [d/wMax, d/r0]; the family peaks at
            // EnvelopePeakX, and the clamp picks the nearest reachable member.
            float xLo = distance / wMax;
            float xHi = Mathf.Min(1f, distance / halfWidth);
            float x = Mathf.Clamp(EnvelopePeakX, xLo, xHi);
            return x * Profile(x, 1f) / distance;
        }

        /// <summary>
        /// <c>2·∫ envelope(d) dd</c> across the DISPERSED annulus (from the band's own half-width out
        /// to the envelope) — the integral conservation is stated on. Fixed-step Simpson, so it is
        /// pure, deterministic and allocation-free; the shape is smooth and 64 panels put it well
        /// inside the discretisation of everything downstream.
        /// </summary>
        public static float EnvelopeIntegral(float halfWidth, float maxHalfWidth)
        {
            if (halfWidth <= 0f || maxHalfWidth <= halfWidth) return 0f;
            const int panels = 64;                       // even, so Simpson's rule closes
            float h = (maxHalfWidth - halfWidth) / panels;
            float sum = EnvelopeShape(halfWidth, halfWidth, maxHalfWidth)
                      + EnvelopeShape(maxHalfWidth, halfWidth, maxHalfWidth);
            for (int i = 1; i < panels; i++)
                sum += (i % 2 == 1 ? 4f : 2f) * EnvelopeShape(halfWidth + i * h, halfWidth, maxHalfWidth);
            return 2f * sum * h / 3f;
        }

        /// <summary>
        /// Foam laid per METRE OF TRACK by the shipped stamp — <c>rate · StampAreaIntegral · r₀² /
        /// speed</c>. The quantity every arm of this term is conserved against, and the reason a
        /// slower hull leaves a denser mark: she dwells over each parcel longer.
        /// </summary>
        public static float ShippedFoamPerMetre(float depositRatePerSecond, float halfWidth,
                                                float speed)
        {
            if (speed <= 1e-4f || halfWidth <= 0f) return 0f;
            return Mathf.Max(depositRatePerSecond, 0f) * StampAreaIntegral * halfWidth * halfWidth / speed;
        }

        /// <summary>
        /// The dispersal edge's soft width, metres. Floored at two world cells so the ring can never
        /// fall between texels, and at four frames of its own advance so it cannot outrun itself at a
        /// low frame rate. Derived, not a dial.
        /// </summary>
        public static float EdgeWidth(float spreadSpeed, float dt)
        {
            return Mathf.Max(2f * CellSize, 4f * Mathf.Max(spreadSpeed, 0f) * Mathf.Max(dt, 0f));
        }

        /// <summary>
        /// 🔴 <b>THE STAMP.</b> The per-frame amplitude of the dispersal edge: the envelope scaled so
        /// that sweeping the edge once across a parcel leaves exactly the envelope value there, and
        /// so that the annulus receives exactly the dispersing share of the shipped foam.
        ///
        /// <para><b>The speed cancels.</b> The envelope's amplitude carries <c>1/v</c> (a slow hull
        /// lays a denser mark) and the edge's advance carries <c>v</c>, so this gain has no speed
        /// term at all: a hull losing way lays a fainter ring, never a divide. Nothing needs a
        /// minimum-speed gate, and nothing blows up at rest.</para>
        ///
        /// <para><b>Why the ring's own width divides.</b> The edge is soft over
        /// <paramref name="edgeWidth"/> metres, so a parcel is under it for several frames as it
        /// sweeps past; dividing by that width is what makes those frames SUM to the envelope
        /// instead of to a multiple of it. Conservation is exact for any edge width — the width
        /// only decides how many frames the deposit is spread over, which is what keeps every one
        /// of them clear of the buffer's 8-bit rounding floor.</para>
        ///
        /// <para>Multiply by <see cref="EnvelopeShape"/> and by <see cref="EdgeBump"/> to get the
        /// amount added to a texel this frame. Twin: the advect shader's dispersal block.</para>
        /// </summary>
        public static float EdgeGain(float depositRatePerSecond, float halfWidth, float maxHalfWidth,
                                     float kelvinFraction, float dt, float edgeWidth)
        {
            float slope = SpreadSlope(kelvinFraction);
            if (slope <= 0f || halfWidth <= 0f || maxHalfWidth <= halfWidth || dt <= 0f) return 0f;
            if (edgeWidth <= 0f) return 0f;
            float envelope = EnvelopeIntegral(halfWidth, maxHalfWidth);
            if (envelope <= 1e-9f) return 0f;
            float share = DispersingShare(halfWidth, maxHalfWidth);
            if (share <= 0f) return 0f;
            // share * (foam per metre) / envelope is the envelope's amplitude, which carries 1/speed;
            // the edge advances at slope*speed. Written with the speed already cancelled rather than
            // divided out and multiplied back, so no speed can ever reach a denominator here.
            float amplitudePerSpeed = share * Mathf.Max(depositRatePerSecond, 0f) * StampAreaIntegral
                                    * halfWidth * halfWidth / envelope;
            return amplitudePerSpeed * slope * Mathf.Max(dt, 0f) / edgeWidth;
        }

        /// <summary>
        /// The ring's own shape across the sweeping edge: a triangle of half-width
        /// <paramref name="edgeWidth"/>, whose integral over the edge's passage is exactly 1 — which
        /// is what makes the single pass lay exactly the envelope and no more.
        /// </summary>
        public static float EdgeBump(float distance, float edgePosition, float edgeWidth)
        {
            if (edgeWidth <= 0f) return 0f;
            float u = Mathf.Abs(distance - edgePosition) / edgeWidth;
            return u >= 1f ? 0f : 1f - u;
        }

        /// <summary>
        /// The freshness a deposit of the given AGE should carry — the value a mark made now will
        /// have decayed to by then. The dispersal lays foam on water the hull never touched, and
        /// that water has an age: without this the whole widening band would be born at the far end
        /// of #724's colour walk while the core beside it is white (measured: age01 1.000 against
        /// the 0.414 it should read at the envelope).
        ///
        /// <para>Because the freshness update is a MAX, this is a no-op inside the churn band — whose
        /// own mark has decayed to exactly this — and correct outside it.</para>
        /// </summary>
        public static float AgeMark(float ageSeconds, float ageHalfLifeSeconds)
        {
            if (ageSeconds <= 0f) return 1f;
            if (ageHalfLifeSeconds <= 0f) return 0f;
            return Mathf.Pow(0.5f, ageSeconds / ageHalfLifeSeconds);
        }

        // ---- THE STORE: what the buffer can actually hold (register row 28) ----------------------

        /// <summary>
        /// 🔴 <b>THE FORMATS THE BUFFER MAY BE ALLOCATED IN, BEST FIRST.</b> The buffer is not a
        /// float: everything above is computed at full precision and then <b>written into a render
        /// target</b>, and the target's own rounding is the last word on whether a term happened.
        ///
        /// <para><b>Why this list exists.</b> The shipped target was <c>RG16</c> — 8 bits per
        /// channel. The advect pass multiplies the stored value by a decay factor and writes it back,
        /// so a per-frame change under HALF A CODE rounds to where it started and is lost. A decay is
        /// a multiply, so its change is largest at full white, and at 60 fps even that is 0.491 codes
        /// for the coverage channel: <b>the wake did not fade at all at the PC-first baseline</b>, and
        /// the freshness clock stopped a third of the way down #724's colour walk. It hid because the
        /// foam still left the 96 m window and the compose's lace still tore at the edge — every
        /// "fade" anyone had seen was the WINDOW, not the decay.</para>
        ///
        /// <para><b>Why 16-bit UNORM and not half float.</b> Both are 4 bytes a texel and both make
        /// the decay run. <c>RGHalf</c>'s mantissa grid is fixed within an octave while the decay's
        /// change is proportional to the value, so round-to-nearest lands off by up to a whole ULP
        /// either way and the decay carries a frame-rate-dependent BIAS: measured, a texel at 1.0
        /// reads 0.5073 after one 6 s half-life at 60 fps and 0.5347 at 144 fps, against
        /// <c>RG32</c>'s 0.50001. Uniform codes are the right grid for a quantity that lives in
        /// [0,1], so the UNORM goes first and the float is the fallback for a device that cannot
        /// render to it (GLES3 makes RG16_UNorm optional and RG16F core, which is exactly the mobile
        /// case rule 7 asks us not to paint into a corner).</para>
        ///
        /// <para>⚠️ The last rung is the SHIPPED format, and on it the decay does not run. It is here
        /// so a device that supports neither 16-bit format still draws foam rather than none — named,
        /// not silent.</para>
        /// </summary>
        public static readonly RenderTextureFormat[] FormatPreference =
        {
            RenderTextureFormat.RG32,      // 16-bit UNORM per channel: 65 535 codes
            RenderTextureFormat.RGHalf,    // 16-bit float per channel: never stalls, slightly biased
            RenderTextureFormat.RG16,      // 8-bit UNORM per channel: THE SHIPPED STALL (row 28)
        };

        /// <summary>
        /// The best format <paramref name="supports"/> will give us, from
        /// <see cref="FormatPreference"/>. Production passes
        /// <c>SystemInfo.SupportsRenderTextureFormat</c>; a test passes its own predicate, because
        /// <c>SystemInfo</c> on a null graphics device answers for no device at all and a policy
        /// pinned against that is pinned against nothing.
        ///
        /// <para>A null predicate, or one that refuses everything, returns the last rung — the buffer
        /// must always be allocatable.</para>
        /// </summary>
        public static RenderTextureFormat SelectFormat(System.Func<RenderTextureFormat, bool> supports)
        {
            if (supports != null)
                foreach (RenderTextureFormat candidate in FormatPreference)
                    if (supports(candidate))
                        return candidate;
            return FormatPreference[FormatPreference.Length - 1];
        }

        /// <summary>Bytes one texel of <paramref name="format"/> costs — the buffer's whole budget is
        /// this times the resolution squared times two (the ping-pong). An unrecognised format answers
        /// 4, the cost of the widest thing <see cref="FormatPreference"/> can pick, so a budget
        /// statement can never be flattered by a format it does not know.</summary>
        public static int BytesPerTexel(RenderTextureFormat format)
        {
            switch (format)
            {
                case RenderTextureFormat.RG16:   return 2;
                case RenderTextureFormat.RG32:   return 4;
                case RenderTextureFormat.RGHalf: return 4;
                default:                         return 4;
            }
        }

        /// <summary>Codes per channel of a UNORM format, or 0 for a format that is not one.</summary>
        static int UnormCodes(RenderTextureFormat format)
        {
            switch (format)
            {
                case RenderTextureFormat.RG16: return 255;
                case RenderTextureFormat.RG32: return 65535;
                default:                       return 0;
            }
        }

        /// <summary>
        /// 🔴 <b>ONE WRITE INTO THE BUFFER.</b> Round-trip <paramref name="value"/> through
        /// <paramref name="format"/>'s own storage exactly as the hardware does — saturate, then round
        /// to nearest representable. This is the step every term in this class is ultimately judged by,
        /// and modelling it is what turned row 28 from an eyeball complaint into arithmetic.
        ///
        /// <para><c>RGHalf</c> is snapped onto the binary16 GRID (see <see cref="HalfGrid"/>) rather
        /// than pushed through <c>Mathf.FloatToHalf</c>: that pair is a native ECall, and this class's
        /// whole claim is that its arithmetic runs with no editor under it.</para>
        /// </summary>
        public static float Store(float value, RenderTextureFormat format)
        {
            float v = Mathf.Clamp01(value);
            // ⚠️ The snap is taken in DOUBLE. The hardware converts exactly; doing it with a float
            // divide instead disagrees with IEEE binary16 by a whole ULP on values that land on an
            // exact midpoint (measured: 1 in 4000 uniform samples before this line said double).
            if (format == RenderTextureFormat.RGHalf)
            {
                double grid = HalfGrid(v);
                return (float)(System.Math.Round(v / grid, System.MidpointRounding.ToEven) * grid);
            }
            int codes = UnormCodes(format);
            if (codes <= 0) return v;                       // a format we do not model: no rounding
            return (float)(System.Math.Round((double)v * codes, System.MidpointRounding.ToEven) / codes);
        }

        /// <summary>
        /// The spacing of the IEEE binary16 grid at <paramref name="v"/>, for v in [0,1] — a 10-bit
        /// mantissa, so 2^(e−10) inside the octave [2^e, 2^(e+1)), floored at the subnormal step 2^−24.
        ///
        /// <para>This is the whole reason the half float is the FALLBACK and not the choice: the grid
        /// is CONSTANT across an octave while the decay's per-frame change is PROPORTIONAL to the
        /// stored value, so round-to-nearest over-steps at the top of each octave and under-steps at
        /// the bottom, and the decay carries a bias that changes with the frame rate. A UNORM's grid
        /// is uniform, which is what a quantity living in [0,1] wants.</para>
        /// </summary>
        static float HalfGrid(float v)
        {
            float spacing = Mathf.Pow(2f, -24f);            // the subnormal step
            for (int e = -14; e < 0; e++)
                if (v >= Mathf.Pow(2f, e)) spacing = Mathf.Pow(2f, e - 10);
            return spacing;
        }

        /// <summary>
        /// 🔴 <b>WHERE THE DECAY STOPS.</b> Step a texel written at <paramref name="from"/> through
        /// nothing but decay and storage until the stored value stops moving, and answer where it
        /// stuck. That value is the row-28 defect stated as a number: on the shipped 8-bit target it is
        /// <b>1.000</b> for the coverage channel at 60 fps — the wake never fades at all — and 0.680
        /// for the freshness clock; on a 16-bit target it is under 0.004, below anything the compose's
        /// 0.12 threshold can draw.
        ///
        /// <para>Bounded: it gives up after <paramref name="maxFrames"/> and returns what it reached,
        /// so a format that genuinely decays to zero cannot spin here.</para>
        /// </summary>
        public static float DecayFloor(float halfLifeSeconds, float dt, RenderTextureFormat format,
                                       float from = 1f, int maxFrames = 100000)
        {
            float factor = DecayFactor(halfLifeSeconds, dt);
            float v = Store(from, format);
            for (int i = 0; i < maxFrames; i++)
            {
                float next = Store(v * factor, format);
                if (next >= v) return v;                    // it has stopped moving down
                if (next <= 0f) return 0f;
                v = next;
            }
            return v;
        }
    }
}
