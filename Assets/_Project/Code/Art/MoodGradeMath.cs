using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// How much of each authored mood is on screen right now. <see cref="Day"/> + <see cref="GoldenHour"/> +
    /// <see cref="Night"/> partition the time of day (they sum to 1); <see cref="Fog"/> and
    /// <see cref="Storm"/> are 0..1 overlays laid on top in that order.
    /// </summary>
    public readonly struct MoodWeights
    {
        public readonly float Day;
        public readonly float GoldenHour;
        public readonly float Night;
        public readonly float Fog;
        public readonly float Storm;

        public MoodWeights(float day, float goldenHour, float night, float fog, float storm)
        {
            Day = day; GoldenHour = goldenHour; Night = night; Fog = fog; Storm = storm;
        }

        public override string ToString() =>
            $"day {Day:F2} golden {GoldenHour:F2} night {Night:F2} | fog {Fog:F2} storm {Storm:F2}";
    }

    /// <summary>
    /// The pure model behind the grade (juice charter PR 1, P1/P5). Given the facts the world already
    /// publishes — the clock hour, the profile's sunrise/sunset, <c>EnvironmentSample.Visibility</c> and
    /// <c>SeaState01</c> — and the owner's <see cref="JuiceSettings"/> blend knobs, it says how much of
    /// each authored <see cref="MoodGrade"/> is on screen and blends them into one.
    ///
    /// <para><b>No accumulator.</b> Every function here is a pure function of its arguments: the same
    /// hour/weather always yields the same weights and the same grade, in any order, from any starting
    /// state (rule 5). There is no smoothing, no previous-frame term, no random. The 10 Hz tick in
    /// <see cref="MoodGradeDirector"/> is a sampling rate, not a filter.</para>
    ///
    /// <para><b>The blend curve is <c>GameConfig.Juice.Grade*</c></b> (rule 6): the golden-hour width,
    /// the night fade, and the fog / storm thresholds. The LOOKS themselves are the profile's.</para>
    /// </summary>
    public static class MoodGradeMath
    {
        private const float HoursPerDay = 24f;

        /// <summary>Wrap an hour onto 0..24.</summary>
        public static float Wrap24(float hour) => hour - HoursPerDay * Mathf.Floor(hour / HoursPerDay);

        /// <summary>Shortest distance between two clock hours around the 24 h circle (0..12).</summary>
        public static float CircularHourDistance(float a, float b)
        {
            float d = Mathf.Abs(Wrap24(a - b));
            return Mathf.Min(d, HoursPerDay - d);
        }

        /// <summary>
        /// The time-of-day partition. <paramref name="night"/> is 0 through the daylight span
        /// (<see cref="DayNightMath.IsDaylight"/>) and fades to 1 over <paramref name="nightBlendHours"/>
        /// after sunset / before sunrise (0 hours = a hard step). <paramref name="goldenHour"/> is a
        /// triangular kernel of half-width <paramref name="goldenWidthHours"/> centred on sunrise and on
        /// sunset (0 hours = no golden hour), scaled by what night leaves. <paramref name="day"/> is the
        /// remainder, so the three always sum to 1 and each is ≥ 0.
        /// </summary>
        public static void TimeOfDay(float hour, float sunriseHour, float sunsetHour,
                                     float goldenWidthHours, float nightBlendHours,
                                     out float day, out float goldenHour, out float night)
        {
            hour = Wrap24(hour);

            if (DayNightMath.IsDaylight(hour, sunriseHour, sunsetHour))
            {
                night = 0f;
            }
            else
            {
                // Hours into the dark from whichever daylight edge is nearer.
                float sinceSunset  = Wrap24(hour - sunsetHour);
                float untilSunrise = Wrap24(sunriseHour - hour);
                float intoNight = Mathf.Min(sinceSunset, untilSunrise);
                night = nightBlendHours <= 0f ? 1f : Mathf.Clamp01(intoNight / nightBlendHours);
            }

            float toEdge = Mathf.Min(CircularHourDistance(hour, sunriseHour),
                                     CircularHourDistance(hour, sunsetHour));
            float golden = goldenWidthHours <= 0f ? 0f : Mathf.Clamp01(1f - toEdge / goldenWidthHours);

            goldenHour = golden * (1f - night);
            day = Mathf.Max(0f, 1f - night - goldenHour);
        }

        /// <summary>
        /// 0 at/above <paramref name="visibilityStart"/> (clear), 1 at/below <paramref name="visibilityFull"/>
        /// (thick fog), linear between. A start at or below full is a hard step at full.
        /// </summary>
        public static float FogWeight(float visibility, float visibilityStart, float visibilityFull)
        {
            if (visibility >= visibilityStart) return 0f;
            if (visibility <= visibilityFull) return 1f;
            float span = visibilityStart - visibilityFull;
            return span <= 0f ? 1f : Mathf.Clamp01((visibilityStart - visibility) / span);
        }

        /// <summary>
        /// 0 at/below <paramref name="seaStateStart"/>, 1 at/above <paramref name="seaStateFull"/>, linear
        /// between. A full at or below start is a hard step at start.
        /// </summary>
        public static float StormWeight(float seaState01, float seaStateStart, float seaStateFull)
        {
            if (seaState01 <= seaStateStart) return 0f;
            if (seaState01 >= seaStateFull) return 1f;
            float span = seaStateFull - seaStateStart;
            return span <= 0f ? 1f : Mathf.Clamp01((seaState01 - seaStateStart) / span);
        }

        /// <summary>All five weights from the world's facts and the owner's blend knobs.</summary>
        public static MoodWeights Weights(float hour, float sunriseHour, float sunsetHour,
                                          float visibility, float seaState01, in JuiceSettings juice)
        {
            TimeOfDay(hour, sunriseHour, sunsetHour,
                      juice.GradeGoldenHourWidthHours, juice.GradeNightBlendHours,
                      out float day, out float golden, out float night);
            float fog = FogWeight(visibility, juice.GradeFogVisibilityStart, juice.GradeFogVisibilityFull);
            float storm = StormWeight(seaState01, juice.GradeStormSeaStateStart, juice.GradeStormSeaStateFull);
            return new MoodWeights(day, golden, night, fog, storm);
        }

        /// <summary>
        /// The time-of-day partition mixed, then fog laid over, then storm laid over — so a foggy dusk is
        /// mostly fog with a little amber left in it, and a storm at night is the storm look darkened by
        /// the night's share. Not clamped; see <see cref="Evaluate"/>.
        /// </summary>
        public static MoodGrade Blend(in MoodGrade day, in MoodGrade goldenHour, in MoodGrade night,
                                      in MoodGrade fog, in MoodGrade storm, in MoodWeights w)
        {
            var g = MoodGrade.Mix3(day, w.Day, goldenHour, w.GoldenHour, night, w.Night);
            g = MoodGrade.Lerp(g, fog, w.Fog);
            g = MoodGrade.Lerp(g, storm, w.Storm);
            return g;
        }

        /// <summary><see cref="Blend"/> from a profile's five keys.</summary>
        public static MoodGrade Blend(MoodGradeProfile profile, in MoodWeights w) =>
            Blend(profile.Day, profile.GoldenHour, profile.Night, profile.Fog, profile.Storm, w);

        /// <summary>
        /// Lay a region's bias over a blended grade: the tint multiplies the colour filter, the offsets
        /// add. A null override is the identity.
        /// </summary>
        public static MoodGrade ApplyRegion(in MoodGrade g, MoodGradeRegionOverride region)
        {
            if (region == null) return g;
            var r = g;
            r.ColorFilter       = g.ColorFilter * region.ColorFilterTint;
            r.ColorFilter.a     = 1f;
            r.Saturation        = g.Saturation + region.SaturationOffset;
            r.Contrast          = g.Contrast + region.ContrastOffset;
            r.PostExposure      = g.PostExposure + region.ExposureOffset;
            r.VignetteIntensity = g.VignetteIntensity + region.VignetteOffset;
            r.BloomIntensity    = g.BloomIntensity + region.BloomOffset;
            return r;
        }

        /// <summary>
        /// The whole model in one call — what the director runs every tick and what the tests drive:
        /// weights from the facts, the profile blended by them, the region laid over, clamped to URP's
        /// ranges. Pure.
        /// </summary>
        public static MoodGrade Evaluate(MoodGradeProfile profile, MoodGradeRegionOverride region,
                                         float hour, float sunriseHour, float sunsetHour,
                                         float visibility, float seaState01, in JuiceSettings juice,
                                         out MoodWeights weights)
        {
            weights = Weights(hour, sunriseHour, sunsetHour, visibility, seaState01, juice);
            var g = Blend(profile, weights);
            g = ApplyRegion(g, region);
            return g.Clamped();
        }
    }
}
