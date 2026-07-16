using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The authored index of the four GRADED wake plume textures (Small/Medium/Large/Huge) the self-installing
    /// <see cref="BoatWakeEmitter"/> loads at boot — the exact <c>SeaweedLibrary</c> / <c>AmbientFleetLibrary</c>
    /// / <c>IconLibrary</c> pattern: the art lives under <c>Art/VFX/Wake</c>, this one asset gathers the refs and
    /// lives in <c>Resources</c> so a <see cref="RuntimeInitializeOnLoadMethod"/> host can load it with no scene
    /// and no builder wiring.
    ///
    /// <para><b>Why Texture2D refs, not Sprite refs (the spriteMode-Multiple gotcha).</b> The Wake PNGs import
    /// as <c>spriteMode: Multiple</c> and Unity auto-slices each into many disconnected alpha islands — there is
    /// NO single full-image sub-sprite to reference, and <c>Resources.Load&lt;Sprite&gt;</c> returns null for a
    /// Multiple-mode texture (the documented trap). Referencing the TEXTURE (always the present main asset) and
    /// letting the emitter build one full-image <see cref="Sprite"/> per texture in code (the same way it already
    /// builds its foam/crest sprites) sidesteps the slicing entirely: we get the whole authored plume regardless
    /// of how the importer sliced it, with no per-frame cost (built once, shared, batched — rule 7).</para>
    ///
    /// <para>Pure content metadata — never serialized into a save, no determinism concern. Create via
    /// Assets ▸ Create ▸ Hidden Harbours ▸ Wake Sprite Library, save at <c>Resources/WakeSpriteLibrary</c>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Wake Sprite Library", fileName = "WakeSpriteLibrary")]
    public sealed class WakeSpriteLibrary : ScriptableObject
    {
        /// <summary>Resources path (no extension) the emitter loads the library from at boot.</summary>
        public const string ResourcesPath = "WakeSpriteLibrary";

        [Tooltip("The graded wake plume, smallest (tier 0). A light/slow hull's wake.")]
        public Texture2D Small;
        [Tooltip("The graded wake plume, tier 1.")]
        public Texture2D Medium;
        [Tooltip("The graded wake plume, tier 2.")]
        public Texture2D Large;
        [Tooltip("The graded wake plume, biggest (tier 3). A heavy hull driven hard.")]
        public Texture2D Huge;

        [Header("Bow spray (Art/VFX/BowSpray — same Texture2D-ref pattern, same slicing trap)")]
        [Tooltip("The graded bow spray, smallest (tier 0). The dory's occasional wisp.")]
        public Texture2D SpraySmall;
        [Tooltip("The graded bow spray, tier 1.")]
        public Texture2D SprayMedium;
        [Tooltip("The graded bow spray, tier 2.")]
        public Texture2D SprayLarge;
        [Tooltip("The graded bow spray, biggest (tier 3). A fast hull throwing a full sheet.")]
        public Texture2D SprayHuge;

        /// <summary>
        /// The wake plume tier textures in ascending order [Small, Medium, Large, Huge], for indexing by the
        /// grade tier. Allocates a small array — call once at boot, not per frame.
        /// </summary>
        public Texture2D[] Ordered() => new[] { Small, Medium, Large, Huge };

        /// <summary>
        /// The BOW SPRAY tier textures in ascending order [Small, Medium, Large, Huge]. Any slot may be null
        /// on an older library asset (the fields were added after the wake shipped) — the emitter falls back
        /// per-tier, so an un-migrated asset degrades gracefully instead of breaking the wake. Allocates a
        /// small array — call once at boot, not per frame.
        /// </summary>
        public Texture2D[] OrderedSpray() => new[] { SpraySmall, SprayMedium, SprayLarge, SprayHuge };
    }
}
