using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// Rod Fishing v2 wave 3: the parametric fish loft, the rod, and the non-directional bobber.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> FishingKitRigs() =>
            new RigRegistration
            {
                // ---- the fishing kit (drop of 2026-07-22, PR #258) — Rod Fishing v2 wave 3 ------

                // Parametric fish loft: one skeleton, a SPECIES data table. CLAIMED clockwise
                // (th = −dir·45°, same term as the character) but the kit is UNVERIFIED — the
                // README's correction note is explicit that the sign term is not proof, so
                // FishingRigAzimuthProbe measures the head side from pixels before any bake.
                // Declares no DIRS global (Install reports 0); the 8 headings are the ADR-0006
                // recipe, supplied by FishingKitBaker.
                ["fish"] = new RigEntry($"{RigFolder}/fishIsoRig.js", "FishIso",
                                        AzimuthConvention.Clockwise),

                // One of the two rigs the art director fixed CLOCKWISE at source (README's
                // pixel-verified group). Still measured at bake time like everything else —
                // FishingRigAzimuthProbe reads which side the blank extends at the E/W rows.
                ["rod"] = new RigEntry($"{RigFolder}/rodIsoRig.js", "RodIso",
                                       AzimuthConvention.Clockwise),

                // NOT DIRECTIONAL: a 16×22 state sprite (float/nibble/strike/fly) with no azimuth
                // term at all — render(state, frame), no dir argument. The declared convention
                // below is a placeholder that nothing reads and nothing probes; the bobber bake
                // path never consults it and never calls DirForCell.
                ["bobber"] = new RigEntry($"{RigFolder}/bobberRig.js", "RodBobber",
                                          AzimuthConvention.Clockwise),
            };
    }
}
