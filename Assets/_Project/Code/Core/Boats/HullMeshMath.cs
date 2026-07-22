using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The pure maths of POSING a mesh hull (ADR 0022 phase 4): the compass→rig-dir mapping and the
    /// rig's own rock cycle as a continuous function of wave phase. POCO statics, deterministic,
    /// EditMode-testable headless — the same discipline as <c>IsoFacing</c> / <c>DoryRockMath</c>.
    ///
    /// <para><b>Kept OUT of the renderer on purpose.</b> Phase 3's <c>IsoFacetHullRenderer</c> takes
    /// rig dir units and rig roll/pitch/heave and knows nothing of compass headings or waves — "the
    /// per-artwork mirror saga" is phase 4's seam, and this is where it lives, next to the measured
    /// <see cref="HullMeshDef.AzimuthCounterClockwise"/> flag it consumes.</para>
    /// </summary>
    public static class HullMeshMath
    {
        /// <summary>One rig dir unit is 45° of turntable (<c>th = dir·π/4</c>).</summary>
        public const float DegreesPerDirUnit = 45f;

        /// <summary>
        /// Map a compass heading (degrees, 0 = North, CLOCKWISE — the project's bearing convention)
        /// onto the rig's <c>dir</c> argument (1 unit = 45°, fractional = a genuine intermediate
        /// heading — continuous rotation is the point of ADR 0022).
        ///
        /// <para><b>The sign is the CCW saga, resolved by measurement.</b> Every boat rig probed so
        /// far turns its model COUNTER-CLOCKWISE per +dir unit (dir d depicts compass −45°·d), so for
        /// those the mapping NEGATES: to draw compass heading h, ask the rig for dir −h/45. The flag
        /// comes off <see cref="HullMeshDef.AzimuthCounterClockwise"/>, which the baker MEASURES from
        /// rendered pixels (<c>RigAzimuthProbe</c>) — never from the rig's declared facing order,
        /// which has shipped mirrored boats five times. Note this is the mesh-path analogue of
        /// <c>BoatVisualDef.FacingsAreCounterClockwise</c> but is NOT the same fact: that flag
        /// describes a SHEET's cell order (the lobster's sheet is true clockwise because the baker
        /// corrected at bake time); this one describes the LIVE RIG, which is what the mesh runs.</para>
        /// </summary>
        /// <param name="headingDegrees">Compass heading of the bow (0 = North, CW positive).</param>
        /// <param name="zeroHeadingDegrees">The compass heading the rig's dir 0 depicts (0 for every
        /// boat rig — element 0 is the North-facing view).</param>
        /// <param name="azimuthCounterClockwise">The MEASURED rig convention (see above).</param>
        public static float HeadingToDirUnits(float headingDegrees, float zeroHeadingDegrees,
                                              bool azimuthCounterClockwise)
        {
            float units = (headingDegrees - zeroHeadingDegrees) / DegreesPerDirUnit;
            return azimuthCounterClockwise ? -units : units;
        }

        /// <summary>
        /// The rig's canned rock cycle as a CONTINUOUS function of wave phase — the transcription of
        /// every boat rig's <c>rockMotion(i)</c>:
        /// <c>roll = rollA·sin(a), pitch = pitchA·sin(a+π/2), heave = heaveA·sin(a)</c>, with the
        /// frame index generalised to a phase angle (frame i of N is <c>a = i·360°/N</c>).
        ///
        /// <para>Driven by the SAME reconstructed wave phase that picks a sprite hull's rock frame
        /// (<c>DoryRockMath.PhaseDegrees</c>: crest → 90°, trough → 270°), so a mesh hull and a
        /// sprite hull side by side rock in lockstep on the same swell — the mesh just stops
        /// quantising to N frames. At the crest (90°) roll and heave peak and pitch passes through
        /// zero, exactly as the baked frames have it.</para>
        /// </summary>
        public static void RockPose(float phaseDegrees,
                                    float rollAmplitudeDegrees, float pitchAmplitudeDegrees,
                                    float heaveAmplitudePixels,
                                    out float rollDegrees, out float pitchDegrees, out float heavePixels)
        {
            float a = phaseDegrees * Mathf.Deg2Rad;
            float s = Mathf.Sin(a);
            float c = Mathf.Cos(a);          // sin(a + π/2) — the rig's own quarter-cycle pitch lead
            rollDegrees = rollAmplitudeDegrees * s;
            pitchDegrees = pitchAmplitudeDegrees * c;
            heavePixels = heaveAmplitudePixels * s;
        }
    }
}
