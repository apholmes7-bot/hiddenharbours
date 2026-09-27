using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        // Wharf rig kit pass 2, drop 12. The bake needs no lib/ scripts. Install wharfRig2Kit
        // with InstallModule: its dependency chain executes geo, fam, verbs, rig, kit.
        // FAMILIES is the object installed by fam.js, not another WharfGeo2 registration.
        // This distinct completion marker prevents prerequisite de-duplication skipping fam.js.
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> WharfPass2Rigs() =>
            new RigRegistration
            {
                // Raster convention measured from rendered G-buffer coordinates at dir 0 and 1:
                // the +X axis rotates -45 degrees after undoing the 40-degree foreshortening.
                ["wharfRig2"] = new RigEntry($"{RigFolder}/wharf-rig-kit-v2/wharfRig2.js",
                    "WharfRig2", AzimuthConvention.CounterClockwise,
                    new[] { "wharfRig2Geometry", "wharfRig2Families", "wharfRig2Verbs" }),
                // Geometry builders/verbs have no rendered heading. Clockwise is an unused
                // placeholder, following the existing non-directional catalog entries.
                ["wharfRig2Families"] = new RigEntry($"{RigFolder}/wharf-rig-kit-v2/wharfRig2.fam.js",
                    "WharfGeo2.FAMILIES", AzimuthConvention.Clockwise,
                    new[] { "wharfRig2Geometry" }),
                ["wharfRig2Geometry"] = new RigEntry($"{RigFolder}/wharf-rig-kit-v2/wharfRig2.geo.js",
                    "WharfGeo2", AzimuthConvention.Clockwise),
                // Harbours use the raster's camera, hence the same measured convention.
                ["wharfRig2Kit"] = new RigEntry($"{RigFolder}/wharf-rig-kit-v2/wharfRig2.kit.js",
                    "WharfRig2Kit", AzimuthConvention.CounterClockwise,
                    new[] { "wharfRig2" }),
                ["wharfRig2Verbs"] = new RigEntry($"{RigFolder}/wharf-rig-kit-v2/wharfRig2.verbs.js",
                    "WharfVerbs2", AzimuthConvention.Clockwise,
                    new[] { "wharfRig2Families" }),
            };
    }
}
