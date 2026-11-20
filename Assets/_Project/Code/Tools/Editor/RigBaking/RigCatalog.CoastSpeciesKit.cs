using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// Coast species kit: the six inshore catches that shipped as Defs before they had art of
        /// their own (rainbow smelt, Atlantic tomcod, cunner, green crab, northern quahog, Atlantic
        /// surf clam). One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract).
        ///
        /// <para><b>Additive, never an edit of catch pass 2.</b> The pass-2 rigs are pinned by
        /// SHA256SUMS and their ORDER / KINDS lists by CatchPass2KitTests, so this kit registers the
        /// three finfish INTO <c>FishIso2.SPECIES</c> at load time and draws the crab and the clams by
        /// recolouring the pass-2 renders. Loading it moves zero pixels of the seven pass-2 species:
        /// its render wrapper only touches its own keys.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> CoastSpeciesKitRigs() =>
            new RigRegistration
            {
                // Extends FishIso2 with smelt, tomcod and cunner (same 64x80 cell, same states, same
                // pivot), and exposes iconCell / greenCrab / crabDeckSheet / shellIcon. No cell
                // geometry of its own: InstallModule only.
                //
                // It reaches root.FishIso2 (body), root.Crustacean2 (the crab, recoloured) and
                // root.Shellfish2 (the clam plate, re-proportioned per call and restored after) —
                // grepped from the source. The crab and shell helpers are only called by the coast
                // baker, which installs those two rigs first; the fish extension needs only fish2.
                ["coastSpecies"] = new RigEntry($"{RigFolder}/coast-species-kit/Art/coastSpeciesRig.js",
                                                "CoastSpecies", AzimuthConvention.Clockwise,
                                                prerequisites: new[] { "fish2" }),
            };
    }
}
