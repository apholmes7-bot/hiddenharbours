using System;
using System.Collections.Generic;
using System.Linq;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// One articulated fitting the baker knows how to bake, and which hull visuals wear it.
    /// </summary>
    public readonly struct FleetProp
    {
        public readonly string Key;
        public readonly string ScriptPath;
        public readonly string GlobalName;
        public readonly string AssetPath;
        public readonly string PropId;

        /// <summary>How to reach this fitting's geometry inside its rig — the builder call at its
        /// canonical pose, the pivot, the cell, and the private symbols that must be shimmed.</summary>
        public readonly RigPropExtraction Extraction;

        /// <summary>The <c>BoatVisualDef</c>s that wear this fitting, and the slot name each wears it
        /// in. One fitting, many boats: the same outboard bolts to the console skiff and both sport
        /// skiffs, which is exactly why fittings are their own assets.</summary>
        public readonly (string visualAssetPath, string slot)[] WornBy;

        public readonly string Label;

        public FleetProp(string key, string scriptPath, string globalName, string assetPath,
                         string propId, RigPropExtraction extraction,
                         (string, string)[] wornBy, string label)
        {
            Key = key;
            ScriptPath = scriptPath;
            GlobalName = globalName;
            AssetPath = assetPath;
            PropId = propId;
            Extraction = extraction;
            WornBy = wornBy;
            Label = label;
        }
    }

    /// <summary>
    /// <b>The fittings that let the small boats go mesh (ADR 0022 phase 7).</b>
    ///
    /// <para>Phase 6 baked all eleven hulls and could present only seven. The gate was never the
    /// hulls — it was that the dory's oars and the skiffs' outboards are baked one sprite cell per
    /// facing, and a mesh hull turns continuously, so there is no cell to look up. Flipping those
    /// boats without their fittings is how you ship a rowboat with no oars, and the PlayMode suite
    /// says so in four red tests. This table is the fittings becoming meshes on the same terms the
    /// hulls did.</para>
    ///
    /// <para><b>What the mesh path deletes.</b> The sprite fittings carry two whole mechanisms that
    /// exist only because sprites cannot interleave in depth: the <c>upper</c>/<c>lower</c> part split
    /// (so the engine's leg can be drawn UNDER the hull on stern-away headings, the rigs'
    /// <c>MOTOR.behind=[3,4,5]</c>), and the twin outboard's draw-the-far-engine-first ordering. A
    /// fitting parented to a mesh hull joins the same renderer list and writes the same depth buffer,
    /// so both are simply true. Neither is transcribed below, on purpose.</para>
    /// </summary>
    public static class HullPropFleet
    {
        const string Rigs = "docs/art/rigs";
        const string PropFolder = "Assets/_Project/Data/Boats/HullProps";
        const string Visuals = "Assets/_Project/Data/Boats/Visuals";

        /// <summary>
        /// The dory's oars, one asset per side.
        ///
        /// <para><b>Why per side rather than one mirrored asset.</b> Port and starboard ARE mirror
        /// images in the rig (<c>oarlockPt</c> and <c>oarDir</c> both carry a <c>side</c> factor on
        /// x), so one mesh drawn with a negative scale would look right — and shade wrong, because
        /// mirroring reverses triangle winding and inverts every normal against a key light that is
        /// FIXED in screen space. Two honest bakes cost 30 KB and no thought.</para>
        ///
        /// <para><b>The canonical pose is (0, 0), and it is not reachable through the rig's public
        /// pose API.</b> <c>oarPose('row', t)</c> traces an ellipse — <c>sweep=30·sin</c>,
        /// <c>dip=6+22·cos</c> — that never passes through sweep 0 with dip 0. Baking anywhere on
        /// that ellipse would make the runtime rotation a rotation away from an arbitrary point,
        /// which is a needless invitation to sign errors. So the bake calls <c>buildOar</c> directly
        /// with the pose it wants, and takes the shim cost of one more private symbol.</para>
        /// </summary>
        static FleetProp Oar(int side, string sideName, string slot) => new FleetProp(
            key: $"doryOar{sideName}",
            scriptPath: $"{Rigs}/doryIsoRig.js",
            globalName: "DoryIso",
            assetPath: $"{PropFolder}/DoryOar{sideName}PropMesh.asset",
            propId: $"hullprop.dory_oar_{sideName.ToLowerInvariant()}",
            extraction: new RigPropExtraction
            {
                FaceBuilderCall = $"buildOar({side},{{sweep:0,dip:0}})",
                PivotCall = $"oarlockPt({side})",
                // Her oars are drawn through the same camera and pivot as her hull — the rig says so
                // ("separate overlay layer, same camera + pivot as the hull") — so no cell override.
                CellPath = "",
                ExtraSymbols = new[] { "buildOar", "oarlockPt" },
            },
            wornBy: new[] { ("DoryIso", slot) }
                .Select(w => ($"{Visuals}/{w.Item1}.asset", w.Item2)).ToArray(),
            label: $"dory oar, {sideName.ToLowerInvariant()} ({(side < 0 ? "port" : "starboard")})");

        public static readonly IReadOnlyList<FleetProp> All = new[]
        {
            Oar(-1, "Port", "OarPort"),
            Oar(+1, "Star", "OarStar"),
        };

        public static FleetProp Get(string key)
        {
            foreach (var p in All) if (p.Key == key) return p;
            throw new ArgumentException(
                $"No fitting '{key}' in the prop catalog. Known: {string.Join(", ", All.Select(p => p.Key))}.");
        }
    }
}
