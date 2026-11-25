using System;
using UnityEngine;
using HiddenHarbours.Core;   // ITidalTerrain

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>How much of a region's baked grass field is actually drawn</b> — the tunables of
    /// <see cref="GrassThinning"/>, one asset per region, read by that region's <see cref="GrassField"/>.
    ///
    /// <para>The field itself is untouched: it still says what the old grass pass planted where. This
    /// asset decides which of those tufts survive — only those sheltered by a tree or a shrub, only a share
    /// of those, and none on ground below the floor. Turning <see cref="KeepFraction"/> or a radius is a
    /// look change the owner can make without a re-bake; deleting the reference on the field restores the
    /// old meadow exactly.</para>
    ///
    /// <para><b>Anchors are matched by MATERIAL</b>, because that is what trees and shrubs share and grass
    /// does not: every tree draws on <c>Tree.mat</c>, every shrub on <c>LitShrub.mat</c>. Shore plants
    /// (wrack, eelgrass, sea lettuce) are deliberately NOT anchors — they stand on the strand and in the
    /// water, which is exactly where the owner wants no grass.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Grass Thinning", fileName = "GrassThinning")]
    public sealed class GrassThinningDef : ScriptableObject
    {
        [Tooltip("Stable id, grassthin.<region>. Append-only.")]
        public string Id = "grassthin.region";

        [Serializable]
        public sealed class AnchorClass
        {
            [Tooltip("What this class is, for the inspector (tree, shrub…).")]
            public string Name;

            [Tooltip("Any sprite drawn with this material shelters grass around its pivot.")]
            public Material Material;

            [Tooltip("How far (m) from that pivot a tuft still reads as growing IN this vegetation.")]
            [Min(0f)] public float RadiusMetres;
        }

        [Tooltip("The vegetation a tuft may survive beside. A tuft near none of these is not drawn.")]
        public AnchorClass[] Anchors = Array.Empty<AnchorClass>();

        [Tooltip("Share (0..1) of the sheltered tufts that survive — the density cap. Each site's keep roll " +
                 "is hashed from the site, so the same tufts survive on every machine and every load.")]
        [Range(0f, 1f)] public float KeepFraction;

        [Tooltip("OPTIONAL ground height source: any asset implementing Core's ITidalTerrain (the region's " +
                 "PaintedHeightMap). Empty = the dry-ground test is skipped.")]
        [SerializeField] private ScriptableObject _ground;

        [Tooltip("The lowest ground (m above chart datum) a tuft may stand on. Below this is strand, mud or " +
                 "water and keeps nothing even beside a shrub.")]
        public float MinElevationMetres;

        /// <summary>The ground height source, or null when none is set (or the asset is not one).</summary>
        public ITidalTerrain Ground => _ground as ITidalTerrain;

        /// <summary>The raw reference, for content checks that want to know WHICH asset is wired.</summary>
        public ScriptableObject GroundAsset => _ground;

        /// <summary>
        /// The shelter radius for a renderer drawn with <paramref name="material"/>, if it is an anchor.
        /// Matches the asset itself, or a runtime instance of it (Unity names those
        /// "<c>Name (Instance)</c>"), so a renderer whose material got instanced still counts.
        /// </summary>
        public bool TryRadiusFor(Material material, out float radius)
        {
            radius = 0f;
            if (material == null || Anchors == null) return false;
            for (int i = 0; i < Anchors.Length; i++)
            {
                var a = Anchors[i];
                if (a == null || a.Material == null) continue;
                if (a.Material == material ||
                    material.name.StartsWith(a.Material.name + " (Instance)", StringComparison.Ordinal))
                {
                    radius = a.RadiusMetres;
                    return true;
                }
            }
            return false;
        }

        void OnValidate()
        {
            if (_ground != null && !(_ground is ITidalTerrain))
            {
                Debug.LogWarning($"[GrassThinningDef] {name}: '{_ground.name}' is not a ground height source " +
                                 "(it does not implement ITidalTerrain). Cleared.", this);
                _ground = null;
            }
        }
    }
}
