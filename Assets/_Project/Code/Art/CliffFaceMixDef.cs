using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>ONE REGION'S CLIFF MIX.</b> Which stretches of a region's cliffs are drawn in a second skin, as data:
    /// the share of the coast that wears it, how long a stretch of one skin runs, and how wide the change from
    /// one skin to the other is. Chosen by <see cref="CliffFaceMixMath"/> from each station's toe position and
    /// this asset's seed alone: no hidden randomness, nothing saved, the same coast every load and every save.
    ///
    /// <para><b>Found by scene.</b> One asset per region in <c>Resources/CliffFaceMixes</c>, matched to a cliff
    /// wall by its scene's name (<see cref="SceneName"/>), so a region gets a mix by adding an asset and no scene
    /// is edited. A region with none draws its cliffs exactly as before.</para>
    ///
    /// <para>Presentation only. It moves no collider, brow, blocker or walk rule: it picks a palette.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Art/Cliff Face Mix", fileName = "CliffFaceMix")]
    public class CliffFaceMixDef : ScriptableObject
    {
        /// <summary>Every mix id starts with this.</summary>
        public const string IdPrefix = "cliffmix.";

        /// <summary>The Resources folder the mixes are loaded from.</summary>
        public const string ResourcesFolder = "CliffFaceMixes";

        [Header("Identity")]
        [Tooltip("Stable id, append-only (cliffmix.snake_case).")]
        public string Id = "cliffmix.example";
        [Tooltip("The region scene whose cliff walls this mix colours (the scene's name, e.g. StPeters).")]
        public string SceneName = "";

        [Header("The two skins")]
        [Tooltip("The skin most of the coast wears where the mix does not pick the variant (the current look).")]
        public CliffFaceSkinDef BaseSkin;
        [Tooltip("The second skin, drawn on the stretches the mix picks.")]
        public CliffFaceSkinDef VariantSkin;

        [Header("The mix (owner-tunable)")]
        [Tooltip("The share of the plan drawn in the variant skin, 0 to 1. 0 turns the mix off, 1 draws it all. " +
                 "A region's coast is only a few runs long, so its own share lands near this, not on it: raising " +
                 "it always adds coast, and the seed moves where. CliffFaceMixTests reports St Peters' real share.")]
        [Range(0f, 1f)] public float VariantShare = 0.4f;
        [Tooltip("How long a stretch of one skin runs, in metres of coast, before the other may take over. " +
                 "Long enough to read as a change in the rock, not a checkerboard.")]
        [Min(1f)] public float RunMetres = 24f;
        [Tooltip("How wide the change from one skin to the other is, in metres of coast. Drawn as the rock's own " +
                 "blocks giving way one by one, never a fade.")]
        [Min(0f)] public float BlendMetres = 3f;
        [Tooltip("The block the change gives way in, in ground texels (32 a metre): along the shore, then down the " +
                 "face. Wide and short, like the beds.")]
        public Vector2 BlendBlockTexels = new Vector2(6f, 3f);
        [Tooltip("The mix's seed. Change it to move every stretch; nothing else moves them.")]
        public int Seed = 1;

        [Header("One texel size (owner 11 26: no mixels)")]
        [Tooltip("How many times finer the base skin's kit faces are drawn. The kit paints two pixels a texel, 16 " +
                 "texels a metre; 2 draws one kit texel per ground texel (32 a metre), so the rock's grain matches " +
                 "the ground's and its pattern is half the size. 1 leaves the kit's size.")]
        [Range(1f, 4f)] public float BaseFaceTexelScale = 1f;
        [Tooltip("The clifftop's turf lip: a generated strip at the ground's texel that stands in for the kit's " +
                 "brow strip. None leaves the brow strip.")]
        public CliffTurfLipDef TurfLip;

        [Header("Rough edges: the brow and the foot broken into chips, notches, buttresses and bites (0 = off)")]
        [Tooltip("How far the clifftop's line is pushed in and out, metres. Added to the kit's plan displacement, so the " +
                 "face, its decals and its joins all follow it. 0 leaves the brow exactly as the kit draws it.")]
        [Min(0f)] public float BrowJagMetres = 0f;
        [Tooltip("How far the foot's line is pushed in and out, metres. The toe and face blockers stand on the same line " +
                 "(closed over MinBiteMetres), so the drawn foot and the collision agree. 0 = off, and then the blockers " +
                 "stand on the stations exactly as before.")]
        [Min(0f)] public float FootJagMetres = 0f;
        [Tooltip("The three scales the line is broken at, metres of coast per step on average: buttresses and bays, " +
                 "notches, chips. Steps are measured along the run, never along the 12 m tile, so the tile does not repeat.")]
        public Vector3 JagCellMetres = new Vector3(3f, 0.9f, 0.25f);
        [Tooltip("Each scale's share of the reach above.")]
        public Vector3 JagWeights = new Vector3(0.6f, 0.3f, 0.15f);
        [Tooltip("The narrowest bite a walker may enter, metres: narrower bites in the foot are filled in the collision " +
                 "(his foot is 0.7 m across), so nobody wedges in a notch.")]
        [Min(0f)] public float MinBiteMetres = 1f;

        [TextArea] public string Why = "";

        /// <summary>True when the mix draws anything: a variant skin with a palette and a share above zero.</summary>
        public bool IsActive => VariantSkin != null && VariantSkin.Palette != null && VariantShare > 0f && RunMetres > 0f;

        /// <summary>The variant's weight at a station's toe, 0 (base skin) to 1 (variant skin).</summary>
        public float VariantWeightAt(Vector2 toePlan) =>
            IsActive ? CliffFaceMixMath.VariantWeight(toePlan, VariantShare, RunMetres, BlendMetres, Seed) : 0f;

        // ---- the lookup by scene ---------------------------------------------------------------------------

        static Dictionary<string, CliffFaceMixDef> _byScene;

        /// <summary>The mix for a scene, or null when the scene has none. Loaded once and cached.</summary>
        public static CliffFaceMixDef ForScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            Dictionary<string, CliffFaceMixDef> byScene = _byScene;
            if (byScene == null)
            {
                // Built in a local and published once: loading an asset runs its OnValidate in the editor, which
                // clears the cache, so filling the static in place could null it under the loop.
                byScene = new Dictionary<string, CliffFaceMixDef>();
                foreach (CliffFaceMixDef d in Resources.LoadAll<CliffFaceMixDef>(ResourcesFolder))
                    if (d != null && !string.IsNullOrEmpty(d.SceneName) && !byScene.ContainsKey(d.SceneName))
                        byScene.Add(d.SceneName, d);
                _byScene = byScene;
            }
            return byScene.TryGetValue(sceneName, out CliffFaceMixDef mix) ? mix : null;
        }

        /// <summary>Forget the cache (a test, or an edit to the assets).</summary>
        public static void ClearCache() => _byScene = null;

        private void OnValidate() => ClearCache();
    }
}
