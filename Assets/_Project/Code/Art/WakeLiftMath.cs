using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twin of the WAKE LIFT — the stern wave train a hull making way draws
    /// into the DISPLACED water surface's vertex stage (mirrored in
    /// <c>Art/Shaders/HiddenHarboursWater.shader</c>'s <c>WakeTrainHeight</c>; change one, change BOTH
    /// in the same PR, and <c>WakeLiftMathTests</c> reads the shader source to prove it).
    ///
    /// <para><b>The owner, 2026-09-11:</b> <i>"i do want the wake to lift the water and create visual
    /// waves."</i> (register row 27). Until now every wake family was PAINT — the advected foam sheet
    /// (family A), the emitter's sprite deposits (B), the crest gates (C). All three colour a flat
    /// sea. This is the first one that MOVES it: the water behind a hull genuinely stands up and
    /// falls away, in the same vertex stage and the same frame the swell displaces.</para>
    ///
    /// <para>🔴 <b>DRAWN ONLY — this is not the sea anything rides.</b> The height computed here is
    /// added inside the water shader's <c>vertDisplaced</c> and nowhere else. It never enters
    /// <c>WaveFieldSample</c>, never reaches <c>ShoreFadeMath.DisplacedHeight</c>, and is not
    /// published on the <c>DisplacedSea</c> seam the ride reads — so no hull, buoy, deck rider or
    /// seakeeping force can see it (ADR 0018's one-sea rule / one force path). Whether other hulls
    /// SHOULD ride another hull's wash is a simulation question with its own PR and its own helm
    /// verdict; this PR deliberately does not answer it. The two dials live on the water MATERIAL,
    /// which is the strongest fence available: no Core type carries them, so nothing outside the
    /// water shader can read them even by accident.</para>
    ///
    /// <para><b>Why a hull-frame pattern and not a shed history.</b> For steady motion the Kelvin
    /// wake IS stationary in the hull's frame — that is the whole content of the stationary-phase
    /// solution, and it is why a ship's wake photographs the same shape every time. So the train is a
    /// function of (root, heading, speed) alone: no marching state, no history buffer, no RNG,
    /// nothing saved, recomputed every frame from published state (rule 5). It also means the lift
    /// needs no second field and no second march — it rides the transom root, heading and
    /// speed-through-water that <see cref="FoamInjector"/> already computes each <c>LateUpdate</c>
    /// for the foam buffer.</para>
    ///
    /// <para><b>Two stationary-phase members, no invented shapes.</b> A deep-water gravity wave whose
    /// crest normal makes angle θ with the track keeps station behind the hull when its phase speed
    /// equals <c>V·cosθ</c>, i.e. at wavenumber <c>k(θ) = k₀/cos²θ</c> with <c>k₀ = g/V²</c>. The
    /// train shipped here is that family evaluated at its two landmarks — <b>θ = 0</b>, the
    /// TRANSVERSE system whose crests lie square across the track, and <b>θ = θc</b> where the wedge
    /// angle is maximal (<see cref="DivergentNormalDegrees"/>), the DIVERGENT system that rides the
    /// arms. The wedge those arms make is 19.47°, which is exactly the angle already named as
    /// <see cref="FoamBuffer.KelvinSlope"/> for the foam's spread: the lift and the foam widen inside
    /// ONE geometry, and <c>WakeLiftMathTests</c> re-derives the slope from θc to prove it.</para>
    ///
    /// <para><b>What is an approximation, stated plainly.</b> (1) Amplitude. The physical transverse
    /// system decays as <c>s^-1/2</c> and the divergent as <c>s^-1/3</c>; both are replaced by ONE
    /// owner dial, an e-folding length (<see cref="Fall"/>) — rule 6 wants a knob the owner can turn,
    /// not two exponents he cannot. (2) The two members are given an equal share
    /// (<see cref="MemberShare"/>) rather than the relative weight the stationary-phase amplitude
    /// gives them, so the dial reads literally as metres of crest at the transom. (3) The pattern is
    /// QUASI-steady: as speed changes the wavelength changes and the crests slide. The published
    /// speed is the injector's own speed-through-water, which is already a per-frame difference of
    /// the transom's position, so that slide is a drift, never a snap.</para>
    /// </summary>
    public static class WakeLiftMath
    {
        /// <summary>
        /// The DIVERGENT member's crest-normal angle to the track, in degrees:
        /// <c>arccos √(2/3) ≈ 35.264°</c>. This is the θ that maximises the wake's wedge half-angle
        /// <c>β(θ) = atan(sinθ·cosθ / (1 + sin²θ))</c> — so it is not a look constant, it is where the
        /// Kelvin caustic is, and <see cref="FoamBuffer.KelvinSlope"/>'s 19.5° is <c>tan β(θc)</c>
        /// rounded. One geometry, named twice because the foam needed a slope and the lift needs an
        /// angle.
        /// </summary>
        public static readonly float DivergentNormalDegrees =
            Mathf.Acos(Mathf.Sqrt(2f / 3f)) * Mathf.Rad2Deg;

        /// <summary><c>cos θc = √(2/3) ≈ 0.8165</c>.</summary>
        public static readonly float DivergentCos = Mathf.Sqrt(2f / 3f);

        /// <summary><c>sin θc = √(1/3) ≈ 0.5774</c>.</summary>
        public static readonly float DivergentSin = Mathf.Sqrt(1f / 3f);

        /// <summary>
        /// The divergent member's wavenumber as a multiple of the transverse member's:
        /// <c>1/cos²θc = 3/2</c>, EXACTLY. Its crests are therefore two thirds of the transverse
        /// wavelength apart — the feathered inner detail of a real wake, and a number that is derived
        /// rather than chosen.
        /// </summary>
        public const float DivergentWavenumberRatio = 1.5f;

        /// <summary>
        /// Each member's share of the dialled amplitude. The two are summed and both are at a crest
        /// directly astern of the transom (<c>cos 0 = 1</c>), so an equal half share makes the dial
        /// read literally: the material's wake-lift dial is the crest height AT the transom, on the
        /// centreline, at full gate. Not a tuning weight — a unit convention.
        /// </summary>
        public const float MemberShare = 0.5f;

        /// <summary>
        /// The transverse wavelength that keeps station behind a hull making <paramref name="speed"/>
        /// through the water: <c>λ = 2πV²/g</c> (m). The exact inverse of
        /// <see cref="WaterDispersion.DeepPhaseSpeed"/>, which is the relation the sea's own bands
        /// travel by — so the wake is not a second physics bolted beside the swell, and
        /// <c>WakeLiftMathTests</c> round-trips the two to prove it.
        ///
        /// <para>0 at rest and below: a hull with no way on keeps station with no wave at all, so
        /// there is nothing to draw. That is the same gate the foam buffer's wake channel already
        /// uses, and it is what makes the at-rest arm of the acceptance plate exactly zero rather
        /// than nearly zero.</para>
        /// </summary>
        public static float TransverseWavelength(float speed)
        {
            if (speed <= 0f) return 0f;
            return 2f * Mathf.PI * speed * speed / WaterDispersion.Gravity;
        }

        /// <summary>
        /// The train's lateral half-width at <paramref name="asternMetres"/> behind the transom:
        /// <c>w = r₀ + KelvinSlope·s</c>, the hull's own churned half-beam opening out along the
        /// Kelvin caustic. Identical in form to the foam edge's spread law
        /// (<c>FoamBuffer.SpreadSlope</c> at its ceiling), because it is the same wedge: the lift can
        /// never reach outside the foam's physical bound.
        /// </summary>
        public static float HalfWidth(float asternMetres, float halfBeamMetres)
        {
            return Mathf.Max(halfBeamMetres, 0f)
                   + FoamBuffer.KelvinSlope * Mathf.Max(asternMetres, 0f);
        }

        /// <summary>
        /// The amplitude's fall astern: <c>exp(-s/decay)</c>, one owner dial. <b>Decay 0 returns 0</b>
        /// — not 1, and not a NaN: an e-folding length of nothing is a train that never gets away
        /// from the transom, which is indistinguishable from no train, and making that the exact zero
        /// is what gives the second dial a passthrough of its own.
        /// </summary>
        public static float Fall(float asternMetres, float decayMetres)
        {
            if (decayMetres <= 0f) return 0f;
            return Mathf.Exp(-Mathf.Max(asternMetres, 0f) / decayMetres);
        }

        /// <summary>
        /// The lead-in that ROOTS the train at the transom. The train's first crest sits AT the
        /// transom (both members are at <c>cos 0</c> there — the rooster tail), so switching it on at
        /// <c>s = 0</c> would leave a step of the full amplitude across the transom line. This ramps
        /// it in over the hull's own half-beam AHEAD of the transom — water the hull is drawn over
        /// anyway (and which the hull guard discards) — so the sea is continuous everywhere it is
        /// visible. It is <see cref="FoamBuffer.Profile"/> read backwards: the same curve, so no new
        /// shape enters the sea, and one fewer thing to drift between C# and HLSL.
        /// </summary>
        public static float RootRamp01(float asternMetres, float halfBeamMetres)
        {
            return FoamBuffer.Profile(-asternMetres, Mathf.Max(halfBeamMetres, 0f));
        }

        /// <summary>
        /// The drawn height of one hull's stern wave train at a point on the sea, in metres — the
        /// whole law, and the C# twin of the water shader's <c>WakeTrainHeight</c>.
        /// </summary>
        /// <param name="asternMetres">Metres ASTERN of the transom along the hull's heading
        /// (negative = ahead of it, under the hull).</param>
        /// <param name="lateralMetres">Distance from the hull's centreline, signed or not — only its
        /// magnitude is used, because the wake is symmetric about the track.</param>
        /// <param name="amplitudeMetres">The dialled crest height at the transom, already multiplied
        /// by the hull's wake gate (0 at rest). <b>0 returns exactly 0</b>.</param>
        /// <param name="wavelengthMetres">The transverse wavelength from
        /// <see cref="TransverseWavelength"/>. <b>0 returns exactly 0</b> — a hull with no way
        /// on.</param>
        /// <param name="halfBeamMetres">The hull's churned half-beam: where the wedge starts, and the
        /// length the root ramp uses.</param>
        /// <param name="decayMetres">The e-folding length astern. <b>0 returns exactly 0</b>.</param>
        public static float TrainHeight(float asternMetres, float lateralMetres,
                                        float amplitudeMetres, float wavelengthMetres,
                                        float halfBeamMetres, float decayMetres)
        {
            // Four independent passthroughs, each an EXACT zero rather than a small number: the dial
            // at 0, the decay at 0, a hull at rest (no wavelength), and a hull with no beam.
            if (amplitudeMetres <= 0f || wavelengthMetres <= 0f
                || halfBeamMetres <= 0f || decayMetres <= 0f) return 0f;

            float astern = asternMetres;
            float lateral = Mathf.Abs(lateralMetres);

            // OUTSIDE THE WEDGE FIRST. Everything past the caustic is bare sea, and saying so before
            // any trigonometry is both the cheap branch and the honest one.
            float window = FoamBuffer.Profile(lateral, HalfWidth(astern, halfBeamMetres));
            if (window <= 0f) return 0f;

            float ramp = RootRamp01(astern, halfBeamMetres);
            if (ramp <= 0f) return 0f;

            float k0 = 2f * Mathf.PI / wavelengthMetres;
            float transverse = Mathf.Cos(k0 * astern);
            float divergent = Mathf.Cos(k0 * DivergentWavenumberRatio
                                        * (astern * DivergentCos + lateral * DivergentSin));

            return amplitudeMetres * ramp * window * Fall(astern, decayMetres)
                   * MemberShare * (transverse + divergent);
        }

        /// <summary>
        /// The same height, given a point in WORLD metres and the hull's published root — the frame
        /// change the shader's per-hull loop does, written once so a test can walk the sea in world
        /// coordinates exactly as the vertex stage does.
        /// </summary>
        /// <param name="worldXY">The UNDISPLACED ground position of the sea vertex (the water
        /// shader's <c>ground</c>, which is also what its fragment paints and clips at).</param>
        /// <param name="rootXY">The transom, in world metres — <c>FoamBuffer.SternWorld</c>.</param>
        /// <param name="heading">The hull's unit forward direction in world XY.</param>
        public static float HeightAt(Vector2 worldXY, Vector2 rootXY, Vector2 heading,
                                     float amplitudeMetres, float wavelengthMetres,
                                     float halfBeamMetres, float decayMetres)
        {
            Vector2 d = worldXY - rootXY;
            // Astern is MINUS the heading; lateral is the 2D cross product with it. Both are exact
            // for a unit heading, and a degenerate (zero) heading falls out as astern = lateral = 0,
            // which the wedge window then treats as the centreline at the transom.
            float astern = -(d.x * heading.x + d.y * heading.y);
            float lateral = d.x * heading.y - d.y * heading.x;
            return TrainHeight(astern, lateral, amplitudeMetres, wavelengthMetres,
                               halfBeamMetres, decayMetres);
        }
    }
}
