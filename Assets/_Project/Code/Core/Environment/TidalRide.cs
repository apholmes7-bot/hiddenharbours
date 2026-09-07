using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// ⭐ <b>WHAT THE TIDE DOES TO A THING THAT FLOATS — AND WHERE THAT PUTS ITS PICTURE.</b> The
    /// companion to <see cref="TidalExposure"/>, which answers the same question from the other side:
    /// that one is about ground the water covers, this one is about hulls the water carries.
    ///
    /// <para><b>Why it is in Core and not beside its first customer.</b> Two things in this game ride the
    /// tide and they must ride it identically — the harbour's <c>FloatingPlatform</c> (World) and every
    /// boat afloat (Boats) — and those two modules may not see each other (CLAUDE.md rule 4). A second
    /// spelling of the rule in the second module is exactly the shape the memory
    /// <c>a-weight-and-its-colour-must-come-from-one-publisher</c> is about: a float and the boat tied to
    /// it, drawn from two arithmetics, disagreeing by a few centimetres per metre of tide and nobody able
    /// to say which one is lying. So the law is stated once, here, and both of them ask.</para>
    ///
    /// <para><b>Deterministic and stateless</b> (rule 5): every function is pure, of the deterministic
    /// water level (<see cref="IEnvironmentService.WaterLevelAt"/>, itself recomputed from
    /// <c>(worldSeed, gameTime)</c>) and authored geometry. Nothing is saved and nothing accumulates —
    /// a ride computed from last frame's answer is a drift, not a tide.</para>
    /// </summary>
    public static class TidalRide
    {
        /// <summary>
        /// ⭐ <b>THE WATERLINE A FLOATING THING IS ACTUALLY SITTING AT</b> (metres above chart datum) —
        /// the sea's level, until the ebb puts her on the bottom and the ground takes over:
        /// <code>underside = max(waterLevel − draught, bed);  waterline = underside + draught</code>
        ///
        /// <para>Afloat that is exactly <paramref name="waterLevel"/>, which is what the mooring lines
        /// have always assumed (<c>MooringLineMath.BoatCleatElevation</c> adds a fitting's freeboard to
        /// the raw water level). Aground it is <c>bed + draught</c> and it stops falling: she <b>takes
        /// the ground</b> and the water keeps going without her. Grounding is not a second rule that
        /// could disagree with the first — it is the same expression, read from the other side.</para>
        ///
        /// <para>Written as an explicit comparison rather than <see cref="Mathf.Max(float,float)"/> so an
        /// unknown bed degrades to "afloat" instead of poisoning the answer: pass
        /// <see cref="float.NegativeInfinity"/> (or NaN — <c>Mathf.Max(x, NaN)</c> is NaN) for
        /// <paramref name="bedElevation"/> and she can never take the ground, which is the honest
        /// reading of open water with no height map under it.</para>
        /// </summary>
        public static float Waterline(float waterLevel, float bedElevation, float draughtMetres)
        {
            float draught = Mathf.Max(0f, draughtMetres);
            float underside = waterLevel - draught;
            if (bedElevation > underside) underside = bedElevation;
            return underside + draught;
        }

        /// <summary>True when the ebb has put a hull of <paramref name="draughtMetres"/> over a bed at
        /// <paramref name="bedElevation"/> on the bottom — the same <c>depth &lt; draught</c> rule
        /// <c>BoatCrossing.CanFloat</c> holds every hull to, so a float, a boat and the crossing gate
        /// cannot disagree about what "aground" means.</summary>
        public static bool IsAground(float waterLevel, float bedElevation, float draughtMetres)
            => waterLevel - Mathf.Max(0f, draughtMetres) < bedElevation;

        /// <summary>
        /// ⭐ <b>HOW FAR UP-SCREEN A PICTURE BELONGS WHEN THE THING IT DRAWS HAS RISEN.</b> The art was
        /// drawn with its subject at ONE elevation (<paramref name="bakedElevation"/>); the sim carries
        /// it at another (<paramref name="elevationNow"/>). A metre of HEIGHT draws
        /// <see cref="IsoGround.HeightScale"/> ≈ 0.766 up the screen — <b>not</b>
        /// <see cref="IsoGround.GroundDepthScale"/> ≈ 0.643, which is the other half of the same camera
        /// and 19% short — so the picture belongs <c>(now − baked) × 0.766</c> above the line its plan
        /// sits on. Positive on the flood.
        ///
        /// <para><b>Always from the plan line, never from last frame's position.</b> This returns an
        /// OFFSET, so the caller adds it to a place it knows. An implementation that added a delta to
        /// wherever the picture already was would accumulate every rounding error the tide ever made,
        /// and there would be no way back to the truth.</para>
        /// </summary>
        public static float ScreenRise(float elevationNow, float bakedElevation)
            => (elevationNow - bakedElevation) * IsoGround.HeightScale;
    }
}
