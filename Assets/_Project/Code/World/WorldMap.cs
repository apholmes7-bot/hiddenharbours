using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// ⭐ <b>The list of regions that make up the one world</b> (ADR 0057). One asset, under a
    /// <c>Resources</c> folder so the persistent travel rig finds it without a scene reference (the same
    /// library pattern as <c>AmbientFleetLibrary</c>): each listed <see cref="RegionDef"/> contributes its
    /// <see cref="RegionDef.Frame"/> — <see cref="RegionDef.WorldOrigin"/> plus its extent — to the shared
    /// <see cref="WorldFrame"/>.
    ///
    /// <para><b>Membership is the switch.</b> A region listed here is governed by the frame: its edges are
    /// crossings into whatever neighbour touches them, and its scene's keyed <c>RegionPassage</c> triggers
    /// stand down. A region NOT listed (the legacy Coddle Cove greybox, the unbuilt West Water) keeps the old
    /// keyed behaviour where anything still reaches it, and is not part of the world picture.</para>
    ///
    /// <para>Not a Def: it has no id of its own and holds no content, only the list (one entity per file
    /// stays true of the regions themselves).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World Map", fileName = "WorldMap")]
    public sealed class WorldMap : ScriptableObject
    {
        /// <summary><c>Resources.Load</c> path of the shipped map.</summary>
        public const string ResourcesPath = "WorldMap";

        [Tooltip("Every region that is part of the one stitched world. Each must have a scene, a usable " +
                 "extent and a WorldOrigin that puts it edge to edge with its neighbours (no overlap). " +
                 "Leave a region out to keep it off the map.")]
        public RegionDef[] Regions = new RegionDef[0];

        /// <summary>The shipped map, or null when none is in Resources (a stripped test build).</summary>
        public static WorldMap LoadShipped() => Resources.Load<WorldMap>(ResourcesPath);

        /// <summary>Build the Core frame from the listed regions (nulls and unusable extents skipped).</summary>
        public WorldFrame BuildFrame()
        {
            var frame = new WorldFrame();
            if (Regions == null) return frame;
            foreach (var r in Regions)
                if (r != null && r.HasUsableExtent) frame.Register(r.Frame);
            return frame;
        }

        /// <summary>The listed region with this id.</summary>
        public bool TryGet(string regionId, out RegionDef region)
        {
            region = null;
            if (Regions == null || string.IsNullOrEmpty(regionId)) return false;
            foreach (var r in Regions)
            {
                if (r == null || !string.Equals(r.Id, regionId, System.StringComparison.Ordinal)) continue;
                region = r;
                return true;
            }
            return false;
        }
    }
}
