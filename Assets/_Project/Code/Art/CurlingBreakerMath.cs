using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>The curl's shape at one moment of its event: how tall, how far thrown, how present.</summary>
    public readonly struct CurlStage
    {
        /// <summary>0..1 of the event's full curl height.</summary>
        public readonly float Height01;
        /// <summary>0..1 of the event's full forward throw.</summary>
        public readonly float Throw01;
        /// <summary>0..1: how much of the sheet (and of the flat lip's handoff) is drawn.</summary>
        public readonly float Cover01;

        public CurlStage(float height01, float throw01, float cover01)
        {
            Height01 = height01;
            Throw01 = throw01;
            Cover01 = cover01;
        }
    }

    /// <summary>
    /// <b>ADR 0040 B2 — the bounded curling sheet's arithmetic.</b> Pure and allocation-free: every
    /// value is a read of the bore the water already draws (<see cref="BreakerMath.SurfAt"/> at the
    /// picked site), keyed to the published bore clock. Nothing here decides WHERE a curl may happen
    /// (the seabed and the tide do, through the surf) or adds a model of its own; it only gives the
    /// plunging stretch a raised silhouette for the few frames around each crest.
    ///
    /// <para><b>The clock.</b> <c>q</c> is the signed fraction of a period from the crest's arrival
    /// (<see cref="BreakerMath.SignedSecondsFromCrest"/> over <see cref="BreakerMath.PeriodSeconds"/>):
    /// negative while the face steepens, 0 at the crest, positive as the lip lands. The stage keys
    /// in <see cref="CurlingBreakerSettings"/> are fractions of that same period.</para>
    ///
    /// <para><b>The section.</b> A cubic from the bore front to the landing point:
    /// <c>x = T·t²(3−2t)</c>, <c>y = 4P·t(1−t)</c>. The lip leaves the front vertically, peaks at
    /// <c>P</c> half way and lands <c>T</c> ahead, which is the flat lip's throw stated as a
    /// shape.</para>
    /// </summary>
    public static class CurlingBreakerMath
    {
        /// <summary>The flat barrel's far edge in event throws — the water shader's
        /// <c>smoothstep(throwM * 0.75, throwM * 1.15, anatomyAge)</c> ends at 1.15 throws.</summary>
        public const float BarrelReachThrows = 1.15f;

        /// <summary>The flat lip's narrowest half band — the shader's
        /// <c>max(_SurfLipWidth, 0.05)</c>.</summary>
        public const float LipWidthFloorMeters = 0.05f;

        /// <summary>A smooth 0→1 ramp from <paramref name="from"/> to <paramref name="to"/>. Equal (or
        /// reversed) keys are the ramp's analytic limit, a step at <paramref name="to"/>, so a stage
        /// whose keys meet is omitted rather than divided by zero.</summary>
        public static float Ramp(float from, float to, float x)
        {
            if (to <= from) return x >= to ? 1f : 0f;
            float t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The signed fraction of a period from the crest, on the published bore clock.</summary>
        public static float PhaseFraction(float borePhaseDegrees, float periodSeconds)
            => periodSeconds > 0f
                ? BreakerMath.SignedSecondsFromCrest(borePhaseDegrees, periodSeconds) / periodSeconds
                : 0f;

        /// <summary>The curl's stage at <paramref name="q"/>: it rises from <c>SteepenStart</c>,
        /// throws from <c>ThrowStart</c> to the crest, collapses from the crest to <c>Land</c> and is
        /// gone by <c>Gone</c>.</summary>
        public static CurlStage StageAt(float q, in CurlingBreakerSettings settings)
        {
            float rise = Ramp(settings.SteepenStart, settings.ThrowStart, q);
            float height = rise * (1f - Ramp(settings.Crest, settings.Land, q));
            float reach = Ramp(settings.ThrowStart, settings.Crest, q);
            float cover = rise * (1f - Ramp(settings.Land, settings.Gone, q));
            return new CurlStage(height, reach, cover);
        }

        /// <summary>0 at either end of the picked stretch, 1 once <paramref name="endTaperMeters"/>
        /// inside it. <paramref name="along"/> is metres from the site centre along the shore.</summary>
        public static float Taper01(float along, float lengthMeters, float endTaperMeters)
            => Ramp(0f, endTaperMeters, Mathf.Max(0f, lengthMeters) * 0.5f - Mathf.Abs(along));

        /// <summary>The section at <paramref name="t"/> (0 = the bore front, 1 = the landing point):
        /// x metres ahead of the front, y metres above the water surface.</summary>
        public static Vector2 SectionPoint(float t, float throwMeters, float peakMeters)
        {
            t = Mathf.Clamp01(t);
            return new Vector2(throwMeters * t * t * (3f - 2f * t), 4f * peakMeters * t * (1f - t));
        }

        /// <summary>The handoff band in metres ahead of the bore front: from one lip width behind it to
        /// past both the flat lip's far edge and the flat barrel's. Every flat lip and barrel band
        /// with a throw up to <paramref name="fullThrowMeters"/> lies inside it.</summary>
        public static Vector2 HandoffBand(float fullThrowMeters, float lipWidthMeters)
        {
            float width = Mathf.Max(lipWidthMeters, LipWidthFloorMeters);
            float reach = Mathf.Max(0f, fullThrowMeters);
            return new Vector2(-width, Mathf.Max(BarrelReachThrows * reach, reach + width));
        }

        /// <summary>The event's full throw — the flat lip's <c>throwM</c> at the crest, where the
        /// shader's bore event is the birth energy: the standing height × the plunging weight × the
        /// plunge strength × the birth, times the lip throw per metre of height.</summary>
        public static float EventThrow(float standingHeightMeters, float plungingWeight01, float plunge01,
                                       float birth01, float lipThrowPerHeight)
            => BreakerMath.LipThrowMeters(standingHeightMeters,
                Mathf.Clamp01(plungingWeight01) * Mathf.Clamp01(plunge01) * Mathf.Clamp01(birth01),
                lipThrowPerHeight);

        /// <summary>How many frames of one event (at <c>FrameRate</c>) show a curl at least
        /// <c>MinReadableHeight</c> pixels tall and thrown at least <c>MinReadableThrow</c> pixels, at
        /// <c>DefaultPixelsPerMetre</c>.</summary>
        public static int ReadableFrames(float peakMeters, float throwMeters, float periodSeconds,
                                         in CurlingBreakerSettings settings)
        {
            if (periodSeconds <= 0f || settings.FrameRate <= 0) return 0;
            float framesPerPeriod = periodSeconds * settings.FrameRate;
            int first = Mathf.CeilToInt(settings.SteepenStart * framesPerPeriod);
            int last = Mathf.FloorToInt(settings.Gone * framesPerPeriod);
            float ppm = settings.DefaultPixelsPerMetre;
            int count = 0;
            for (int frame = first; frame <= last; frame++)
            {
                CurlStage stage = StageAt(frame / framesPerPeriod, in settings);
                if (stage.Cover01 <= 0f) continue;
                if (peakMeters * stage.Height01 * ppm < settings.MinReadableHeight) continue;
                if (throwMeters * stage.Throw01 * ppm < settings.MinReadableThrow) continue;
                count++;
            }
            return count;
        }

        /// <summary>True when an event clears the readable bar: <c>ReadableFrameBar</c> frames.</summary>
        public static bool IsReadable(float peakMeters, float throwMeters, float periodSeconds,
                                      in CurlingBreakerSettings settings)
            => ReadableFrames(peakMeters, throwMeters, periodSeconds, in settings) >= settings.ReadableFrameBar;
    }
}
