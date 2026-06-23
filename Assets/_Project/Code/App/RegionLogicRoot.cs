using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>
    /// Marker component (ADR 0011) that tags the single GameObject which roots a committed region scene's
    /// builder-generated <b>LOGIC layer</b> — the invisible gameplay scaffolding the simulation reads
    /// (the <see cref="RegionAnchor"/>, the wharf economy + its persistent proxies, the loader/passage,
    /// the shore colliders, the standalone-review camera, the fishing-spot marker). The root GameObject is
    /// conventionally named <c>--LOGIC-- (generated, do not edit)</c>.
    ///
    /// <para>The owner's hand-painted <b>VISUAL layer</b> — painted Tilemaps + dropped decor prefab
    /// instances from the #71 toolkit — lives <i>outside</i> this root and is authored by the owner alone.
    /// The two layers share the scene file but never the same GameObjects, so each layer has exactly one
    /// author (single-author-per-layer rule) and there is no within-scene contention to merge.</para>
    ///
    /// <para>This tag is the stable marker the builder's "Refresh … Logic" command keys off: it finds the
    /// tagged root in the open committed scene, destroys + regenerates ONLY that subtree, and leaves
    /// everything else (the painting) untouched. It is a runtime MonoBehaviour (not editor-only) so it
    /// serializes into the committed <c>.unity</c>; it carries no behaviour, only the region id it roots.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RegionLogicRoot : MonoBehaviour
    {
        [Tooltip("Stable region id whose generated logic this root holds (matches RegionDef.Id), " +
                 "e.g. region.coddle_cove. Set by the builder; used to sanity-check a Refresh.")]
        [SerializeField] private string _regionId;

        /// <summary>The region id this generated logic root belongs to.</summary>
        public string RegionId => _regionId;

        /// <summary>Set the region id (called by the builder when it (re)creates the root).</summary>
        public void SetRegionId(string regionId) => _regionId = regionId;
    }
}
