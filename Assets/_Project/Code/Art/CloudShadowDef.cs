using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Every number behind the soft cloud shade that drifts over land and water: how much of the ground a
    /// patch covers and how deep it reads for a given cloud cover, its colour, its size, how fast the wind
    /// carries it, and the fair-weather cloud that comes and goes on otherwise clear days. Presentation only:
    /// nothing here is simulated or saved; the drift is recomputed from the world seed and game time.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Cloud Shadows", fileName = "CloudShadowDef")]
    public sealed class CloudShadowDef : ScriptableObject
    {
        public string Id = "cloud_shadows.default";
        public bool Enabled = true;
        public Shader Shader;

        [Tooltip("Cloud cover (0 clear .. 1 flat overcast) to how far a shaded pixel moves toward ShadeColour. " +
                 "Zero in a clear sky; strongest under broken cloud; back to zero when the patches merge into " +
                 "the flat overcast the day/night light already dims.")]
        public AnimationCurve Strength = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.2f, .5f),
            new Keyframe(.5f, .65f), new Keyframe(.8f, .35f), new Keyframe(1, 0));

        [Tooltip("Cloud cover to the share of the ground under a patch (0..1).")]
        public AnimationCurve Coverage = new AnimationCurve(new Keyframe(0, .2f), new Keyframe(.2f, .35f),
            new Keyframe(.5f, .55f), new Keyframe(.8f, .8f), new Keyframe(1, 1));

        [Tooltip("The multiply at full strength: darker and a little cooler than daylight. Never near black.")]
        public Color ShadeColour = new Color(.62f, .67f, .8f, 1);

        [Tooltip("Sun elevation (sin) at which the shade reaches full strength; it fades to nothing at the horizon.")]
        [Range(0, 1)] public float FullSunElevation = .25f;

        [Tooltip("Seconds for the shade to fade out on stepping indoors (and back in on stepping out).")]
        [Min(0)] public float IndoorFadeSeconds = .6f;

        [Header("Shape")]
        [Tooltip("Noise texture edge in texels (power of two). Made once per world seed; never an asset.")]
        [Min(2)] public int NoiseSize = 128;
        [Tooltip("Lattice cells across the texture for the broadest octave; must divide NoiseSize.")]
        [Min(1)] public int NoiseCells = 16;
        [Tooltip("Octaves of detail, each half the size of the last.")]
        [Min(1)] public int NoiseOctaves = 4;
        [Tooltip("World metres one texture repeat covers. 1024 m and 16 cells make the broadest clouds ~64 m: bigger than the view, far bigger than a boat, with an edge that crosses the screen.")]
        [Min(0)] public float ScaleMetres = 1024;
        [Tooltip("Width of a patch's soft edge, in noise units either side of the threshold.")]
        [Range(0, 1)] public float Softness = .08f;
        [Min(0)] public float SecondaryScale = 1.73f;
        [Min(0)] public float SecondaryRate = .71f;
        [Range(0, 1)] public float SecondaryWeight = .35f;

        [Header("Drift")]
        [Tooltip("Metres the shade moves per game second for each m/s of wind, downwind.")]
        [Min(0)] public float DriftMetresPerGameSecondPerMps = .08f;
        [Tooltip("Game seconds between the wind samples the drift is integrated over (from the start of the day).")]
        [Min(0)] public float DriftStepSeconds = 15;

        [Header("Fair-weather cloud")]
        [Tooltip("Highest cloud cover a fair day's passing cumulus reaches. Weather overcast counts as the larger of the two.")]
        [Range(0, 1)] public float FairCoverMax = .5f;
        [Tooltip("Game seconds over which fair-weather cloud builds and clears.")]
        [Min(0)] public float FairCoverPeriodSeconds = 600;
        [Tooltip("Below this share of its range the fair-weather channel is a clear sky.")]
        [Range(0, 1)] public float FairCoverClearBelow = .4f;

        [Tooltip("Ground plane the shade is laid on: the camera's forward ray meets it here.")]
        public float WorldPlaneZ;
        [Tooltip("The shade quad's size over the camera's view (at least 1, so it always covers the screen).")]
        [Min(1)] public float CullingOversize = 1.1f;

        /// <summary>The darkest the multiply may go on any channel: the shade darkens, it never blacks out.</summary>
        public const float MinShadeChannel = .4f;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Id) && Id.StartsWith("cloud_shadows.", StringComparison.Ordinal)
                && Shader != null && CurveValid(Strength) && CurveValid(Coverage)
                && ColourValid(ShadeColour)
                && Positive(FullSunElevation) && FullSunElevation <= 1
                && Finite(IndoorFadeSeconds) && IndoorFadeSeconds >= 0
                && NoiseSize >= 2 && NoiseSize <= 256 && (NoiseSize & (NoiseSize - 1)) == 0
                && NoiseCells >= 1 && NoiseCells <= NoiseSize && NoiseSize % NoiseCells == 0
                && NoiseOctaves >= 1 && NoiseOctaves <= 6
                && Positive(ScaleMetres) && Finite(DriftMetresPerGameSecondPerMps) && DriftMetresPerGameSecondPerMps >= 0
                && Positive(DriftStepSeconds)
                && Positive(SecondaryScale) && Positive(SecondaryRate)
                && Finite(SecondaryWeight) && SecondaryWeight >= 0 && SecondaryWeight <= 1
                && Positive(Softness) && Softness <= 1
                && Finite(FairCoverMax) && FairCoverMax >= 0 && FairCoverMax <= 1
                && Positive(FairCoverPeriodSeconds)
                && Finite(FairCoverClearBelow) && FairCoverClearBelow >= 0 && FairCoverClearBelow < 1
                && Finite(WorldPlaneZ) && Finite(CullingOversize) && CullingOversize >= 1;
        }

        static bool ColourValid(Color c) => Finite(c.r) && Finite(c.g) && Finite(c.b)
            && c.r >= MinShadeChannel && c.g >= MinShadeChannel && c.b >= MinShadeChannel
            && c.r <= 1 && c.g <= 1 && c.b <= 1;
        static bool Positive(float x) => Finite(x) && x > 0;
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        static bool CurveValid(AnimationCurve curve)
        {
            if (curve == null || curve.length < 2 || curve[0].time != 0 || curve[curve.length - 1].time != 1) return false;
            for (int i = 0; i < curve.length; i++)
            {
                var key = curve[i];
                if (!Finite(key.time) || !Finite(key.value) || !Finite(key.inTangent) || !Finite(key.outTangent)
                    || key.time < 0 || key.time > 1 || key.value < 0 || key.value > 1
                    || (i > 0 && key.time <= curve[i - 1].time)) return false;
            }
            return true;
        }
    }
}
