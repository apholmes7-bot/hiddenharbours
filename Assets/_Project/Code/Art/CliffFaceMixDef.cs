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

        [Header("The turf lip on a bank with no cliff wall (owner 11 26: the brow needs a real turf lip)")]
        [Tooltip("The material the bank's lip is drawn with (the cliff face's own, so it takes the same light). " +
                 "None = banks hang no lip.")]
        public Material ShoreBrowMaterial;
        [Tooltip("How far from the brow the shore lines traced the turf's own edge is looked for, metres. " +
                 "0 = banks hang no lip.")]
        [Min(0f)] public float ShoreBrowReachMetres = 0f;
        [Tooltip("How squarely down the screen a bank must face to hang a lip, 0 to 1 (1 = straight down). The lip " +
                 "is laid in upright columns of whole texels, so a bank facing across the screen cannot carry one.")]
        [Range(0f, 1f)] public float ShoreBrowMinFacing = 0.5f;
        [Tooltip("A run of lip shorter than this is dropped, metres.")]
        [Min(0f)] public float ShoreBrowMinRunMetres = 2f;
        [Tooltip("How far inside the turf the lip's line is laid, metres, so no grass shows above its sod.")]
        public float ShoreBrowInsetMetres = 0.15f;
        [Tooltip("How far the jag bites the line further into the turf, metres (the scales and seed are the jag's).")]
        [Min(0f)] public float ShoreBrowJagMetres = 0.4f;
        [Tooltip("A run's two ends crumble away in whole columns over this many metres.")]
        [Min(0f)] public float ShoreBrowEndCrumbleMetres = 0.75f;
        [Tooltip("Two stations a quarter metre apart whose turf edges differ by more than this are not one run, metres.")]
        [Min(0f)] public float ShoreBrowJumpMetres = 0.75f;

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
        [Tooltip("How much of the roughness (the jag, and the rib warp's change of depth) a wall takes by how it faces " +
                 "the camera. The measure is how far a station's outward push runs along its drawn column: 1 on a south " +
                 "wall, near 0 on a steep east or west wall, where a push drags the rock sideways across a face that is " +
                 "a sliver on screen and breaks it into shards. x = at or under this, none; y = at or over this, all of " +
                 "it; a smooth step between. (0, 0) = every wall takes all of it.")]
        public Vector2 RoughnessByFacing = Vector2.zero;
        [Tooltip("A wall whose facing (averaged along its run) is under this is laid on the screen grid: level bedding, " +
                 "square texels, no shear (owner 11 26: one pixel size on screen). 0 = the lower bound above.")]
        [Range(0f, 1f)] public float ScreenGridUnderFacing = 0f;
        [Tooltip("How far either side of a chunk its facing is averaged over, metres of brow, so one continuous wall " +
                 "takes one mapping and does not flicker chunk to chunk. 0 = each chunk by itself.")]
        [Min(0f)] public float FacingRunWindowMetres = 0f;
        [Tooltip("How far a change of mapping may be moved along the run to land on a corner of the outline, metres. " +
                 "0 = it stays where the average crosses.")]
        [Min(0f)] public float FacingCornerReachMetres = 0f;
        [Tooltip("The least turn between two joined chunks that counts as a corner, degrees.")]
        [Range(0f, 180f)] public float FacingCornerDegrees = 20f;

        [Header("Faulted bedding, on walls laid on the screen grid (rough, never plank siding; block 0 = off)")]
        [Tooltip("The blocks the bedding is faulted in, ground texels (32 a metre): x the mean width of a block, y the " +
                 "mean height of a tier of them. Widths and heights vary from the cell (CliffFaultMath). (0, 0) = off: " +
                 "the bedding runs the wall in level courses.")]
        public Vector2 FaultBlockTexels = Vector2.zero;
        [Tooltip("The most a block's bedding is thrown up or down against its neighbours, whole texels.")]
        [Min(0)] public int FaultThrowTexels = 0;
        [Tooltip("The most a block's rock is slid along its bedding, whole texels: each block shows a different stretch " +
                 "of the painted rock, so course heights differ from block to block.")]
        [Min(0)] public int FaultSlideTexels = 0;
        [Tooltip("How many rows a crack at a block edge runs before it may step a texel left or right.")]
        [Min(1)] public int FaultCrackRunTexels = 4;
        [Tooltip("The share of block edges that carry a one-texel crack, 0 to 1.")]
        [Range(0f, 1f)] public float FaultCrackShare = 0f;
        [Tooltip("The step a block's facing snaps to for its shade, degrees: each block is lit as one of the two " +
                 "nearest steps, so tone changes block by block and not on the straight line between two chunks. " +
                 "0 = every chunk lit at its own facing.")]
        [Range(0f, 90f)] public float FaultFacetDegrees = 0f;
        [Tooltip("On the VARIANT skin's own face, how much of the block size above a block takes. 1 = the same blocks.")]
        [Range(0.1f, 2f)] public float FaultVariantBlockScale = 1f;
        [Tooltip("On the VARIANT skin's own face, how much of the throw above a block takes. Its courses are drawn at " +
                 "the tile's own texel (the base skin's are finer), so they are taller than the base skin's beds and " +
                 "a throw that breaks those leaves these as long level slabs. 1 = the same throw.")]
        [Range(0f, 4f)] public float FaultVariantThrowScale = 1f;

        [Header("Height on walls laid on the screen grid (owner 11 27: walls read tall; spacing 0 and shares 0 = off)")]
        [Tooltip("Upright joints: one per this many ground texels across the screen, at a place in the stretch fixed by " +
                 "its hash, running down the face in runs (below). Each is a one-texel crack. 0 = no joints.")]
        [Min(0)] public int JointSpacingTexels = 0;
        [Tooltip("How many rows one run of a joint covers before it may break off or step a texel aside.")]
        [Min(1)] public int JointRunTexels = 24;
        [Tooltip("The share of runs a joint is drawn in, 0 to 1: the rest are gaps, so a joint breaks off and picks up.")]
        [Range(0f, 1f)] public float JointRunShare = 0.8f;
        [Tooltip("The share of runs that step one texel left or right of the joint's line, 0 to 1.")]
        [Range(0f, 1f)] public float JointStepShare = 0.3f;
        [Tooltip("A joint's right-hand texel catches the light one band up, so the crack reads as a cut with an edge.")]
        public bool JointLitEdge = false;
        [Tooltip("The share of the face under the brow drawn one band lighter (the top catches the sky), 0 to 1. " +
                 "Judged on the whole face, brow 0 to foot 1, not band by band. 0 = none.")]
        [Range(0f, 1f)] public float FaceRampTopShare = 0f;
        [Tooltip("The share of the face over the foot drawn one band darker (the foot sits in its own shade), 0 to 1. " +
                 "0 = none.")]
        [Range(0f, 1f)] public float FaceRampFootShare = 0f;
        [Tooltip("How far either ramp's edge wanders column by column, as a share of the face, so it is a ragged " +
                 "texel edge and never a level line.")]
        [Range(0f, 0.5f)] public float FaceRampJitter = 0.06f;
        [Tooltip("The share of the face over the foot that is also drawn one shade tier down (deeper than a band), 0 to " +
                 "1, so the foot reads clearly darker than the brow. 0 = none.")]
        [Range(0f, 1f)] public float FaceRampDeepShare = 0f;
        [Tooltip("Buttresses: the face is grouped into broad upright masses, one per this many ground texels across the " +
                 "screen, each with a shaded side drawn one shade tier down. 0 = none.")]
        [Min(0)] public int ButtressPitchTexels = 0;
        [Tooltip("The shaded side's share of its buttress, min and max (each buttress picks its own by hash).")]
        public Vector2 ButtressShadeShare = new Vector2(0.3f, 0.55f);
        [Tooltip("Rows of one bed of a buttress's edge: at each bed the edge jogs sideways (below), so it is never one line.")]
        [Min(1)] public int ButtressBedTexels = 18;
        [Tooltip("How far a buttress's edge jogs left or right at a bed, in ground texels.")]
        [Min(0)] public int ButtressJogTexels = 3;
        [Tooltip("Whether the ramp and the buttresses are drawn on walls NOT laid on the screen grid too (the sheared, " +
                 "stepped walls). Off = only walls laid on the screen grid take them.")]
        public bool HeightOnShearedWalls = false;
        [Tooltip("Walls that face down the screen (not across it) are laid on the screen grid too, their bedding counted " +
                 "down from their own brow so it follows the curve in whole-texel steps, every texel square and " +
                 "unrotated (owner 11 27: the bends did not read as pixel art). Off = they are laid down the column.")]
        public bool StepShearedWalls = false;

        [Header("The bank's own lip (owner 11 27: banks with no wall read tall)")]
        [Tooltip("The strip a bank with no cliff wall hangs, where it differs from the wall's (its own rock riser under the " +
                 "soil). None = the bank hangs the wall's TurfLip.")]
        public CliffTurfLipDef ShoreBrowTurfLip;
        [Tooltip("The strip a wall NOT laid on the screen grid hangs (the stepped walls: a rock riser and a grass terrace " +
                 "between tiers). None = it hangs TurfLip like every wall.")]
        public CliffTurfLipDef ShearedTurfLip;

        [Header("The ledge at the foot (owner 11 27: the flat ledge reads apart from the face; strength 0 = off)")]
        [Tooltip("How much of the calm below the ground's rock (shelf, talus, ledge) takes, 0 to 1. 0 = drawn as before.")]
        [Range(0f, 1f)] public float LedgeCalm = 0f;
        [Tooltip("How much of its colour the calmed rock keeps, 0 (grey) to 1 (all of it).")]
        [Range(0f, 1f)] public float LedgeSaturationKeep = 1f;
        [Tooltip("How much of its light-to-dark spread the calmed rock keeps around the pivot, 0 (flat) to 1 (all of it).")]
        [Range(0f, 1f)] public float LedgeContrastKeep = 1f;
        [Tooltip("The brightness the spread is drawn in toward (linear luma, 0 to 1).")]
        [Range(0f, 1f)] public float LedgeCalmPivot = 0.12f;

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
