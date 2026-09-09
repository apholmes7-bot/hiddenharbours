using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One region's bias on the grade (art bible §4.3 — "each region biases the grade so travel FEELS like
    /// crossing into a new mood"): Nine Mile Creek warm and the most colourful, Coddle Cove balanced and
    /// warmest, an Ironbound colder and darker. <b>One entity per file</b> (rule 2), keyed by the
    /// <see cref="RegionId"/> the region publishes through <c>GameServices.CurrentRegionId</c>.
    ///
    /// <para>The director loads every one of these under <c>Resources/MoodGrade/</c> once and looks the
    /// current region up by id each tick; a region with no file gets the identity (no bias). The bias is
    /// laid over the BLENDED grade (<see cref="MoodGradeMath.ApplyRegion"/>): the tint multiplies the
    /// colour filter and the offsets add, then everything is clamped to URP's ranges.</para>
    ///
    /// <para>To add a region: <c>Assets ▸ Create ▸ Hidden Harbours ▸ Lighting ▸ Mood Grade Region
    /// Override</c>, save it under <c>Assets/_Project/Resources/MoodGrade/</c>, set the id. No code.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Lighting/Mood Grade Region Override", fileName = "MoodGradeRegion")]
    public sealed class MoodGradeRegionOverride : ScriptableObject
    {
        [Tooltip("The region this biases — the RegionDef's stable id (e.g. region.nine_mile_creek).")]
        [SerializeField] private string _regionId = "";

        [Header("Bias (laid over the blended grade; white / 0 = no bias)")]
        [Tooltip("Multiplies the colour filter. Warm it (1, 0.97, 0.92) for a human, painted place; cool it " +
                 "(0.9, 0.94, 1) for a bleak one.")]
        [SerializeField] private Color _colorFilterTint = Color.white;
        [Tooltip("Added to saturation (-100..100). Positive = the most colourful place on the coast.")]
        [Range(-50f, 50f)] [SerializeField] private float _saturationOffset;
        [Tooltip("Added to contrast (-100..100).")]
        [Range(-50f, 50f)] [SerializeField] private float _contrastOffset;
        [Tooltip("Added to exposure (EV). Negative for a darker region.")]
        [Range(-1f, 1f)] [SerializeField] private float _exposureOffset;
        [Tooltip("Added to the vignette intensity (0..1). Positive closes the frame in.")]
        [Range(-0.5f, 0.5f)] [SerializeField] private float _vignetteOffset;
        [Tooltip("Added to the bloom intensity.")]
        [Range(-1f, 1f)] [SerializeField] private float _bloomOffset;

        public string RegionId         => _regionId;
        public Color  ColorFilterTint  => _colorFilterTint;
        public float  SaturationOffset => _saturationOffset;
        public float  ContrastOffset   => _contrastOffset;
        public float  ExposureOffset   => _exposureOffset;
        public float  VignetteOffset   => _vignetteOffset;
        public float  BloomOffset      => _bloomOffset;

        /// <summary>True when every field is at its identity — the override changes nothing.</summary>
        public bool IsIdentity =>
            _colorFilterTint == Color.white && _saturationOffset == 0f && _contrastOffset == 0f &&
            _exposureOffset == 0f && _vignetteOffset == 0f && _bloomOffset == 0f;

        /// <summary>Author a region's bias in code (the create-only builder uses this to ship the coast).</summary>
        public void Set(string regionId, Color colorFilterTint, float saturationOffset, float contrastOffset,
                        float exposureOffset, float vignetteOffset, float bloomOffset)
        {
            _regionId = regionId;
            _colorFilterTint = colorFilterTint;
            _saturationOffset = saturationOffset;
            _contrastOffset = contrastOffset;
            _exposureOffset = exposureOffset;
            _vignetteOffset = vignetteOffset;
            _bloomOffset = bloomOffset;
        }
    }
}
