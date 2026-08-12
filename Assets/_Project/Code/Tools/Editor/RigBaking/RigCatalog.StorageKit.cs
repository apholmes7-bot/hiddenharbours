using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The catch-handling wave: what a container is filled WITH, and the glue rig that composes
        /// the fills over the other catch rigs.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> StorageKitRigs() =>
            new RigRegistration
            {
                // ---- the storage wave (catch-handling pass) — container fills ------------------

                // CONTINUOUS heading (ang in radians), not an 8-way turntable — fills scatter it.
                // The declared convention is a placeholder nothing probes; the storage bake renders
                // the exact per-variant angles CatchKit.item composes.
                ["crustacean"] = new RigEntry($"{RigFolder}/crustaceanRig.js", "Crustacean",
                                              AzimuthConvention.Clockwise),

                // NOT DIRECTIONAL: 14×12 item lays + 22×16 handfuls, no camera. Exposes IW/IH/
                // ipivot instead of W/H/pivot, so it must be loaded with InstallModule, never
                // Install. Placeholder convention, nothing probes it.
                ["shellfish"] = new RigEntry($"{RigFolder}/shellfishRig.js", "Shellfish",
                                             AzimuthConvention.Clockwise),

                // THE GLUE, not a renderer: item()/fillItems()/tintSpoil over the other catch
                // rigs. No cell geometry of its own (InstallModule only) and — uniquely — it calls
                // document.createElement('canvas') inside item(), so the storage baker installs a
                // host-side canvas shim FIRST (ADR 0021 §5: anything the engine needs that the rig
                // doesn't provide belongs in OUR host code, never in his file).
                ["catchKit"] = new RigEntry($"{RigFolder}/catchKit.js", "CatchKit",
                                            AzimuthConvention.Clockwise),

                // The insulated deck tote. README claims CLOCKWISE (th = −dir·45°, the character/
                // fish term, and the fish MEASURED clockwise in the #265 bake) — still measured
                // from pixels by StorageRigAzimuthProbe before any bake, per the correction note.
                ["fishTote"] = new RigEntry($"{RigFolder}/fishToteRig.js", "FishTote",
                                            AzimuthConvention.Clockwise),

                // Pails + fish tray. README's CCW-inferred group (th = +dir·45°, the boat term) —
                // measured from pixels (tray-footprint chirality) before any bake. Exposes
                // pivotCarry/pivotRest instead of pivot → InstallModule; the storage baker reads
                // the REST pivot itself (rest mode is the only mode it bakes).
                ["bucket"] = new RigEntry($"{RigFolder}/bucketRig.js", "BucketIso",
                                          AzimuthConvention.CounterClockwise),
            };
    }
}
