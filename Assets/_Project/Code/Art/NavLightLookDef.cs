using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>A per-site look for a navigation mark's lantern</b> (<c>navlight_look.snake_case</c>): a small bright glass
    /// core at the lamp, a soft low halo that falls away smoothly, lens colours rather than signal neon, and an optional
    /// short faint streak on the water under the mark.
    ///
    /// <para>The coast-wide <see cref="NavLightPresets"/> stays the default for every mark. A mark carries one of these
    /// only where a region asks for it (St Peters' inner channel pair beside the pier), so a harbour can read its marks
    /// as lamps without relighting every buoy on the coast.</para>
    ///
    /// <para>Plain data plus a pure <see cref="ApplyTo"/>; <see cref="NavLight"/> calls it after the preset stamp, so a
    /// mark without a look is byte-for-byte what it was.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Art/Nav Light Look", fileName = "NavLightLook")]
    public class NavLightLookDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (navlight_look.snake_case).")]
        public string Id = "navlight_look.example";
        public string DisplayName = "";

        [Header("The lamp")]
        [Tooltip("Radius of the hard bright glass core at the lamp, metres.")]
        [Min(0f)] public float CoreRadiusMetres = 0.28f;
        [Tooltip("How far the soft halo reaches from the lamp, metres (the glow's whole radius).")]
        [Min(0.01f)] public float HaloRangeMetres = 1f;
        [Tooltip("The halo's brightness next to the core's 1. Low, so the halo is a haze and not a disc.")]
        [Range(0f, 1f)] public float HaloStrength = 0.18f;
        [Tooltip("0 a hard disc edge, 1 all falloff.")]
        [Range(0f, 1f)] public float EdgeSoftness = 1f;
        [Tooltip("How far the core is pulled toward white: a lit lens burns paler than its colour at the centre.")]
        [Range(0f, 1f)] public float CoreWhiten = 0.1f;
        [Tooltip("The lamp's strength at full dark. Just enough that the lens's own channel burns full: any more and the " +
                 "additive core clips every channel and a red or green mark reads white.")]
        [Min(0f)] public float Intensity = 1.15f;

        [Header("Lens colours")]
        [Tooltip("A red lens: warm and deep, not neon.")]
        public Color LensRed = new Color(0.92f, 0.24f, 0.16f, 1f);
        [Tooltip("A green lens: a little toward blue, as real ones read.")]
        public Color LensGreen = new Color(0.30f, 0.90f, 0.58f, 1f);
        [Tooltip("A white lens.")]
        public Color LensWhite = new Color(1f, 0.95f, 0.86f, 1f);
        [Tooltip("A yellow lens.")]
        public Color LensYellow = new Color(0.98f, 0.80f, 0.32f, 1f);

        [Header("Reflection on the water")]
        [Tooltip("Length of the faint streak drawn down the water under the mark, metres. 0 = no streak.")]
        [Min(0f)] public float StreakLengthMetres = 1.4f;
        [Tooltip("The streak's half-width as a cone half-angle, degrees. Narrow, so it reads as a glint down the water.")]
        [Range(0f, 45f)] public float StreakHalfAngleDegrees = 7f;
        [Tooltip("The streak's strength at full dark. Faint: the lamp is the light, this is its glint.")]
        [Min(0f)] public float StreakIntensity = 0.35f;
        [Tooltip("The streak's edge softness, along and across.")]
        [Range(0f, 1f)] public float StreakSoftness = 1f;

        /// <summary>The lens colour this look gives a lamp colour.</summary>
        public Color LensFor(NavLightColour colour)
        {
            switch (colour)
            {
                case NavLightColour.Green: return LensGreen;
                case NavLightColour.Red: return LensRed;
                case NavLightColour.Yellow: return LensYellow;
                default: return LensWhite;
            }
        }

        /// <summary>Does this look draw a streak on the water?</summary>
        public bool DrawsStreak => StreakLengthMetres > 0f && StreakIntensity > 0f;

        /// <summary>Dress a lamp the preset has already stamped: core, halo, lens colour, strength.</summary>
        public void ApplyTo(SceneLight lamp, NavLightColour colour)
        {
            if (lamp == null) return;
            lamp.Shape = SceneLight.LightShape.Radial;
            lamp.Color = LensFor(colour);
            lamp.Intensity = Intensity;
            lamp.Range = HaloRangeMetres;
            lamp.EdgeSoftness = EdgeSoftness;
            lamp.CoreRadiusMetres = CoreRadiusMetres;
            lamp.HaloStrength = HaloStrength;
            lamp.CoreWhiten = CoreWhiten;
        }

        /// <summary>Dress the streak: a narrow cone thrown DOWN the water from the mark's waterline, no pool, no
        /// shadows.</summary>
        public void ApplyStreakTo(SceneLight streak, NavLightColour colour)
        {
            if (streak == null) return;
            streak.ClearLampLook();
            streak.Shape = SceneLight.LightShape.Cone;
            streak.ConeHalfAngle = StreakHalfAngleDegrees;
            streak.Range = Mathf.Max(0.01f, StreakLengthMetres);
            streak.Color = LensFor(colour);
            streak.Intensity = StreakIntensity;
            streak.EdgeSoftness = StreakSoftness;
            streak.AngularSoftness = StreakSoftness;
            streak.CoreBoost = 0f;
            streak.FlickerAmount = 0f;
            streak.CastsShadows = false;
            streak.ReachMetres = 0f;
        }
    }
}
