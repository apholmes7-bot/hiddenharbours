using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// Hand TOOLS that are not fishing tackle — today just the clam spade.
        ///
        /// <para>Its own file rather than a line in <see cref="FishingKitRigs"/> for the reason
        /// <c>RigCatalog.cs</c> gives for the split: one kit, one file, no shared tail for two kit PRs
        /// to collide on. It is also honest about what the shovel is — the rod's structural twin (same
        /// 112×112 cell, same grip-centre pivot, same "posed by <c>CharacterIso.tool()</c>" contract)
        /// but a different kit, driven by a different animation, for a different job.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> ToolKitRigs() =>
            new RigRegistration
            {
                // ---- the clam spade -------------------------------------------------------------
                //
                // ⚠️⚠️ COUNTER-CLOCKWISE, and its own header says otherwise. The file opens
                // "Same fixed 3/4 turntable as the fleet / character / rod (45deg steps …)" — but its
                // camBasis is `th = dir*PI/4` where BOTH the character and the rod use
                // `th = -dir*PI/4`, and the rod's carries the comment "ADR-0006 fix: CW azimuth, kept
                // in sync with characterIsoRig so the mount still aligns". The spade never got that
                // fix, so it is the mirror of the rig that drives it.
                //
                // MEASURED four independent ways in the standalone V8 harness before this line was
                // written, because the error class hides where you look — `drawn − true = −2·heading`
                // is exactly ZERO at north and south and 180° out at east and west, so a spot-check
                // that reaches for a cardinal cannot see it:
                //   1. the source sign term above;
                //   2. tip() at the E/W rows: −21.28 / +21.28 px from the grip column, against the
                //      rod's +16.89 / −17.96 measured in the same run — a clean mirror;
                //   3. project([0,1,0]).dx per dir: 0, −22.63, −32, −22.63, 0, +22.63, +32, +22.63,
                //      i.e. the rig's own forward swings screen-LEFT at east;
                //   4. the rendered silhouette's far extreme: −22 px at east, +21 px at west.
                // Declaring it CCW is what makes RigBaker.DirForCell emit (N−k)%N, so the SHEET on
                // disk is genuinely clockwise like every other sheet in the project. Declaring it CW
                // would ship a spade that is pixel-perfect at N and S and backwards at E and W.
                //
                // Still measured at bake time like everything else — ShovelRigAzimuthProbe reads the
                // blade's own extreme from pixels and RefuseOnMismatch cross-checks it against this
                // declaration. The day the art director folds the sign fix into the rig, that probe
                // reddens and this comment is the record of why.
                //
                // Declares no DIRS global (Install reports 0); the 8 headings are the ADR-0006
                // recipe, supplied by ShovelKitBaker like the rest of the tool/tackle kits.
                //
                // No prerequisites: the spade renders standalone. The CHARACTER rig is its pose
                // DRIVER, not its prerequisite — ShovelKitBaker installs both and probes both, the
                // same arrangement FishingKitBaker.BakeRod uses.
                ["shovel"] = new RigEntry($"{RigFolder}/shovelIsoRig.js", "ShovelIso",
                                          AzimuthConvention.CounterClockwise),
            };
    }
}
