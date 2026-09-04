using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The owner-tunable look of SUN-CAST SPRITE SHADOWS (ADR 0013 §"Projected shadows"; CLAUDE.md rule 6
    /// — no magic numbers). One asset says how dark a shadow is, how far it rakes as the sun drops, how
    /// far it is allowed to rake at all, and how big the pool of shade under a crown is. Every
    /// <see cref="SpriteShadow"/> in the game reads exactly one of these (from
    /// <c>Resources/SpriteShadowProfile</c> if present, otherwise the built-in default — the same
    /// convention as <see cref="DayNightProfile"/> and <see cref="LampShadowProfile"/>).
    ///
    /// <para><b>Why this exists.</b> Until now every one of these numbers was a <c>[SerializeField]</c> on
    /// the component, and <see cref="AcadianTreeCatalog"/> attaches that component with NO per-tree dials —
    /// so the length of a dawn rake was a constant in a C# file that the owner could not reach without a
    /// code change, and re-tuning it meant re-planting a forest. Now it is one asset and one Build click.
    /// Per-caster machinery stays on the component — the foot offset, the fallback hour, the pixel grid,
    /// the refresh rate and the sorting offset are how ONE caster works, not how the game looks; every
    /// LOOK decision lives here. A profile field nothing reads would be dead config, so there are none.</para>
    ///
    /// <para><b>How to tune (owner).</b> <c>Assets ▸ Create ▸ Hidden Harbours ▸ Lighting ▸ Sprite Shadow
    /// Profile</c>, saved at <c>Assets/_Project/Resources/SpriteShadowProfile.asset</c> (the name matters —
    /// that is the path the component loads).</para>
    ///
    /// <para><b>⚠️ THREE values deliberately differ between the code default and the shipped asset</b>, and
    /// each is a proposal this PR puts in front of the owner on a plate: <see cref="MaxLength"/> (below),
    /// <see cref="GroundContactRadius"/> (0 in code — no pool at all — against the asset's 0.42) and
    /// <see cref="SortByFarEnd"/> (false in code against the asset's true). The code defaults are the
    /// component's own historical numbers, so a project with NO asset renders exactly the pre-PR frame; the
    /// asset is where the proposals live, and each is one field in an inspector.</para>
    ///
    /// <para><b>On <see cref="MaxLength"/>.</b> The code default is 7, which is what the component has always carried —
    /// so a project with no asset renders exactly today's frame. But 7 caps a MULTIPLIER whose own ceiling
    /// is <see cref="LengthAtHorizon"/> (5), so it has never once bound on any caster in the game: a mature
    /// white pine rakes 54.8 m at 07:00 and 61.9 m at 06:30, unclamped. The shipped asset carries 3, which
    /// binds. Which of the two the game keeps is the owner's call off the PR's rake plate, and it is one
    /// field in an inspector either way.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Authored constants only; a shadow is a pure function of (the
    /// published sun globals, the caster's sprite, these) and nothing here is saved or randomised.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Lighting/Sprite Shadow Profile", fileName = "SpriteShadowProfile")]
    public sealed class SpriteShadowProfile : ScriptableObject
    {
        [Header("Darkness")]
        [Tooltip("The darkest a shadow ever gets (its alpha at a firm clear noon). Scaled DOWN by the sun " +
                 "being low and by overcast — so this is the cap, not the constant. 0 = no sun shadows at " +
                 "all, 1 = solid.")]
        [Range(0f, 1f)] [SerializeField] private float _maxAlpha = 0.45f;

        [Tooltip("The flat shadow colour (RGB). Near-black with a hint of the cool sky reads best on the " +
                 "North-Atlantic palette; pure black is harsher. Shared with the lamp shadows.")]
        [SerializeField] private Color _shadowColor = new Color(0.04f, 0.05f, 0.10f, 1f);

        [Header("Length (× the caster's height)")]
        [Tooltip("Shadow length at NOON (sun overhead), as a multiple of the caster's height — a short " +
                 "stub under the feet. 0.3..0.5 reads well.")]
        [Min(0f)] [SerializeField] private float _lengthAtNoon = 0.35f;

        [Tooltip("Shadow length at a LOW sun (near the horizon), as a multiple of the caster's height — " +
                 "the long dawn/dusk rake. Bigger = more dramatic raking shadows.")]
        [Min(0f)] [SerializeField] private float _lengthAtHorizon = 5f;

        [Tooltip("Hard CAP on the shadow length (× height). ⚠️ It caps the MULTIPLIER, whose own ceiling " +
                 "is 'Length at horizon' — so a value above that never binds. At the shipped 3 a mature " +
                 "white pine rakes 41 m at dawn instead of 55 m; at 7 (the code default) nothing in the " +
                 "game is ever clamped.")]
        [Min(0f)] [SerializeField] private float _maxLength = 3f;

        [Header("The shade under a crown")]
        [Tooltip("A GROUND-CONTACT pool at the caster's feet — the shade a crown throws straight down, " +
                 "which a sheared silhouette cannot draw because at noon it rakes NORTH and leaves the " +
                 "trunk foot in full sun. Its radius is this × the caster's own drawn WIDTH, squashed to " +
                 "the ground plane. 0 = off (no pool at all), which is exactly the pre-PR frame.")]
        [Range(0f, 2f)] [SerializeField] private float _groundContactRadius = 0.42f;

        [Tooltip("How dark the ground-contact pool is, as a fraction of the cast shadow's own alpha at the " +
                 "same moment. 1 = the same shade (seamless where the two meet — recommended, since they " +
                 "are the same sun); lower reads as a lighter halo around a darker rake.")]
        [Range(0f, 1f)] [SerializeField] private float _groundContactAlpha = 1f;

        [Tooltip("Only casters at least this tall (metres, as DRAWN) get a pool. A short caster does not " +
                 "need one: its own noon shadow is 'Length at noon' x its height, which for anything under " +
                 "a couple of metres lands ON its own footprint already. The gate is what keeps 148 shrubs " +
                 "and 384 shore plants out of the pool pass — they are unchanged, and the shade under a " +
                 "CROWN is what this is for.")]
        [Min(0f)] [SerializeField] private float _groundContactMinHeight = 3f;

        [Tooltip("How soft the pool's edge is, as a fraction of its radius. 0 = a hard ellipse (reads as a " +
                 "sticker); 1 = fades all the way from the centre. The default keeps a solid core with a " +
                 "feathered rim.")]
        [Range(0f, 1f)] [SerializeField] private float _groundContactSoftness = 0.55f;

        [Header("Where a shadow lands")]
        [Tooltip("Sort a shadow by its FAR END rather than by the caster's feet. A rake runs NORTH, which " +
                 "is up-screen and therefore BEHIND — so sorted by its far end a shadow slides under " +
                 "everything it crosses, and a tree standing in it draws over it instead of wearing it as " +
                 "a crown-shaped blot. Off = the pre-PR behaviour, where a shadow paints over every sprite " +
                 "between its caster and its tip.")]
        [SerializeField] private bool _sortByFarEnd = true;

        [Header("Look")]
        [Tooltip("Edge feather of the silhouette (0 = crisp pixel cutout — the pixel-art default; up to " +
                 "1 = soft-edged). The shape is always the caster's own sprite alpha.")]
        [Range(0f, 1f)] [SerializeField] private float _edgeSoftness = 0f;

        public float MaxAlpha { get => _maxAlpha; set => _maxAlpha = Mathf.Clamp01(value); }
        public Color ShadowColor { get => _shadowColor; set => _shadowColor = value; }
        public float LengthAtNoon { get => _lengthAtNoon; set => _lengthAtNoon = Mathf.Max(0f, value); }
        public float LengthAtHorizon { get => _lengthAtHorizon; set => _lengthAtHorizon = Mathf.Max(0f, value); }
        public float MaxLength { get => _maxLength; set => _maxLength = Mathf.Max(0f, value); }
        public float GroundContactRadius { get => _groundContactRadius; set => _groundContactRadius = Mathf.Clamp(value, 0f, 2f); }
        public float GroundContactAlpha { get => _groundContactAlpha; set => _groundContactAlpha = Mathf.Clamp01(value); }
        public float GroundContactMinHeight { get => _groundContactMinHeight; set => _groundContactMinHeight = Mathf.Max(0f, value); }
        public float GroundContactSoftness { get => _groundContactSoftness; set => _groundContactSoftness = Mathf.Clamp01(value); }
        public bool SortByFarEnd { get => _sortByFarEnd; set => _sortByFarEnd = value; }
        public float EdgeSoftness { get => _edgeSoftness; set => _edgeSoftness = Mathf.Clamp01(value); }

        /// <summary>
        /// The COMPONENT'S OWN historical defaults as an in-memory profile — what every
        /// <see cref="SpriteShadow"/> carried as serialized fields before this asset existed. A project
        /// with no <c>Resources/SpriteShadowProfile.asset</c> therefore renders exactly the pre-PR frame,
        /// which is what makes the asset an override rather than a dependency.
        ///
        /// <para>⚠️ Two of these are NOT the shipped asset's values, deliberately, and both are the
        /// owner's call off this PR's plates: <see cref="MaxLength"/> (7 here — the dead clamp — against
        /// the asset's binding 3) and <see cref="GroundContactRadius"/> (0 here — no pool at all — against
        /// the asset's 0.42). Everything else agrees, and the shipped-asset test names each divergence
        /// explicitly rather than skipping it.</para>
        /// </summary>
        public static SpriteShadowProfile CreateDefault()
        {
            var p = CreateInstance<SpriteShadowProfile>();
            p.name = "SpriteShadowProfile (built-in default)";
            p._maxLength = 7f;              // the dead clamp the component always carried
            p._groundContactRadius = 0f;    // no ground pool: the pre-PR frame
            p._sortByFarEnd = false;        // shadows paint over what they cross, as they always did
            return p;
        }
    }
}
