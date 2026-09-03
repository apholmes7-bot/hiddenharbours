using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The hulls. The golden-master punt that every azimuth measurement is checked against, and
        /// the Tier 3 lobster boat that Phase 1 exists for.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> FleetRigs() =>
            new RigRegistration
            {
                // The golden-master probe. Both its TUBS are at x:0 — see RigAzimuthProbe.
                ["punt"] = new RigEntry($"{RigFolder}/puntIsoRig.js", "PuntIso",
                                        AzimuthConvention.CounterClockwise),

                // The Cape Islander — the hub workboat, and the LAST hull whose exterior sheet was a
                // hand export (#224, before her rig was imported at #227). Registered here for the
                // sprite re-bake that retired that sheet (PR 2b of the full-mesh rollout): the
                // turntable resolves rigs by catalog key, and until this row existed only the MESH
                // baker (which walks HullMeshFleet) could load her.
                //
                // The convention is MEASURED, not inherited, and from two independent sources: her
                // committed HullMeshDef carries AzimuthCounterClockwise = 1 (the mesh bake's probe —
                // "port→starboard ground bearing −90.00° at a quarter turn", #690), and the
                // turntable's own pixel probe re-measures her at every sprite bake and REFUSES on a
                // disagreement with this line (RigBaker.Bake). RigAzimuthConventionTests pins both
                // readings against each other.
                ["capeIslander"] = new RigEntry($"{RigFolder}/capeIslanderIsoRig.js", "CapeIslanderIso",
                                                AzimuthConvention.CounterClockwise),

                // The reason Phase 1 exists: Tier 3, ~12.0 m LOA, and no baked art anywhere.
                //
                // ⚠️ SHE CARRIES A PAINT AXIS (kit drop 2026-08-12). Twelve named schemes, and her
                // `MATS` is no longer a const — it is `matsFor(id)`, so the mesh extractor reads her
                // table through a Reconstructions entry (`matsFor('gelcoat').MATS`), exactly as it
                // does for the punt and the console. Still ONE file and NO prerequisites: the paint
                // is generated inside her own closure (OKLCH), not delegated to another rig, so
                // nothing has to be loaded before her and load order cannot go silently wrong here.
                //
                // The convention below is unchanged, and was RE-MEASURED rather than inherited: her
                // 8 facings render byte-identical to the pre-paint rig (456×420×4, V8 harness), so
                // any measurement over her pixels — the azimuth probe included — necessarily returns
                // the answer it returned before paint existed.
                ["lobsterBoat"] = new RigEntry($"{RigFolder}/lobsterBoatIsoRig.js", "LobsterBoatIso",
                                               AzimuthConvention.CounterClockwise),
            };
    }
}
