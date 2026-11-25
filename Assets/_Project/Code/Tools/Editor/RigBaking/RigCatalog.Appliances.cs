using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The bathroom and kitchen appliances (owner ask 2026-11-17: they read as boxes): hollow basins with
        /// water in them, a clawfoot bath, a water closet, a tiled shower, the cook stove, the prep counter and
        /// the microwave, each with its fill, froth, run and flush states.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract).</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> ApplianceRigs() =>
            new RigRegistration
            {
                // A sibling of interiorProp, never an edit of it: interiorPropRig.js is pinned by hash in the
                // placed interiors' recipes. Same camera, same 460-px cell, same pivot (the floor centre of
                // the piece's own footprint) and the same turntable, so a piece drops onto a room floor point
                // with no offset maths. Exposes project() like the room does, so InteriorRigAzimuthProbe
                // measures the convention against the room at bake time. render(name, dir, opts); its opts
                // carry the states (fill, pour, froth, flush, run) and split:'near' for the layer drawn in
                // front of a figure using the piece.
                ["appliance"] = new RigEntry($"{RigFolder}/appliance-kit/applianceIsoRig.js", "ApplianceIso",
                                             AzimuthConvention.CounterClockwise),
            };
    }
}
