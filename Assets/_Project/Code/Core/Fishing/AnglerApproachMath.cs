using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Where the fish coming to a villager's bait is and how she is drawn at one instant.</summary>
    public struct ApproachPose
    {
        /// <summary>Her offset from his stand (m) and her compass heading (radians, 0 = North, clockwise).</summary>
        public Vector2 Offset;
        public float HeadingRadians;
        /// <summary>The anim she is drawn from and the seconds into it (the swim's tail-beat clock, or the
        /// seconds into a roll).</summary>
        public SwimFishPose Pose;
        public double PoseSeconds;
        /// <summary>Her fade-in (0..1), before the water's own fade.</summary>
        public float FadeIn01;
    }

    /// <summary>
    /// <b>The fish coming to a villager's bait</b>, a pure function of the seconds left before her bite
    /// (presentation only, rule 5: nothing is saved and no random stream is drawn; the side she comes from is
    /// her own fight seed's). A curious fish, not a straight line: she swims in off the line on a lazy swing,
    /// turns and circles the bait, hangs facing it, then takes it. Her flank flashes (the roll) on each of the
    /// two turns. Every distance and time is the look's (<see cref="AnglerApproachLook"/>).
    /// </summary>
    public static class AnglerApproachMath
    {
        /// <summary>
        /// False when no approach is drawn now (unset look, or the bite further off than her approach).
        /// <paramref name="bobber"/> is the bait's offset from his stand; <paramref name="rollSeconds"/> one
        /// play of the roll anim (0 = no flank flash).
        /// </summary>
        public static bool At(in AnglerApproachLook look, Vector2 bobber, float secondsToBite, int fightSeed,
                              float rollSeconds, out ApproachPose pose)
        {
            pose = default;
            pose.Offset = bobber;
            if (!(look.Seconds > 0f) || float.IsNaN(secondsToBite) || secondsToBite > look.Seconds) return false;
            float tau = Mathf.Clamp(look.Seconds - secondsToBite, 0f, look.Seconds);
            Evaluate(in look, bobber, fightSeed, rollSeconds, tau, out pose);
            pose.FadeIn01 = look.FadeInSeconds > 0f ? Mathf.Clamp01(tau / look.FadeInSeconds) : 1f;
            return true;
        }

        /// <summary>Her pose at the bait once the bite has come (<paramref name="secondsIntoBite"/> on): facing
        /// the bait as she took it, her tail beating at the take's pace.</summary>
        public static ApproachPose AtTheBait(in AnglerApproachLook look, Vector2 bobber, int fightSeed,
                                             float secondsIntoBite)
        {
            Evaluate(in look, bobber, fightSeed, 0f, look.Seconds, out ApproachPose pose);
            pose.Offset = bobber;
            pose.PoseSeconds += Mathf.Max(0f, secondsIntoBite) * Rate(look.TakeBeatRate);
            pose.FadeIn01 = 1f;
            return pose;
        }

        /// <summary>A compass heading (radians, 0 = North, clockwise) along <paramref name="v"/>, or
        /// <paramref name="fallback"/> for a zero vector.</summary>
        public static float HeadingOf(Vector2 v, float fallback)
            => v.sqrMagnitude > 1e-8f ? Mathf.Atan2(v.x, v.y) : fallback;

        /// <summary>The seconds of swim-in before she reaches the circle (the approach's remainder).</summary>
        public static float SwimInSeconds(in AnglerApproachLook look)
            => Mathf.Max(0f, look.Seconds - look.CircleSeconds - look.PauseSeconds - look.TakeSeconds);

        private static float Rate(float r) => r > 0f ? r : 1f;

        private static void Evaluate(in AnglerApproachLook look, Vector2 bobber, int fightSeed, float rollSeconds,
                                     float tau, out ApproachPose pose)
        {
            pose = default;
            Vector2 dir = bobber.sqrMagnitude > 1e-8f ? bobber.normalized : Vector2.up;
            float side = (fightSeed & 1) == 0 ? 1f : -1f;
            float a = side * look.OffLineDegrees * Mathf.Deg2Rad;
            var from = new Vector2(dir.x * Mathf.Cos(a) - dir.y * Mathf.Sin(a), dir.x * Mathf.Sin(a) + dir.y * Mathf.Cos(a));
            float r = Mathf.Max(0f, look.CircleM);
            Vector2 start = bobber + from * Mathf.Max(look.FromM, r);
            Vector2 entry = bobber + from * r;

            float tIn = SwimInSeconds(in look);
            float tCircle = Mathf.Max(0f, look.CircleSeconds);
            float tPause = Mathf.Max(0f, look.PauseSeconds);
            float tTake = Mathf.Max(0f, look.TakeSeconds);
            float circleEnd = tIn + tCircle, pauseEnd = circleEnd + tPause;

            float phi0 = Mathf.Atan2(from.y, from.x);
            float sweep = side * look.CircleDegrees * Mathf.Deg2Rad;
            Vector2 hang = bobber + new Vector2(Mathf.Cos(phi0 + sweep), Mathf.Sin(phi0 + sweep)) * r;

            // Her tail-beat clock: her swim pace, slower as she hangs, quicker as she takes the bait.
            float pauseRate = Rate(look.PauseBeatRate), takeRate = Rate(look.TakeBeatRate);
            double beat = Mathf.Min(tau, circleEnd)
                          + Mathf.Clamp(tau - circleEnd, 0f, tPause) * pauseRate
                          + Mathf.Clamp(tau - pauseEnd, 0f, tTake) * takeRate;

            Vector2 pos, vel;
            if (tau < tIn)
            {
                float p = tIn > 0f ? tau / tIn : 1f;
                float e = 1f - (1f - p) * (1f - p);          // she slows as she nears the bait
                Vector2 chord = entry - start;
                Vector2 perp = chord.sqrMagnitude > 1e-8f ? new Vector2(-chord.y, chord.x).normalized * side : Vector2.zero;
                pos = start + chord * e + perp * (look.SwingM * Mathf.Sin(Mathf.PI * e));
                vel = chord + perp * (look.SwingM * Mathf.PI * Mathf.Cos(Mathf.PI * e));
            }
            else if (tau < circleEnd)
            {
                float u = tCircle > 0f ? (tau - tIn) / tCircle : 1f;
                float q = u * u * (3f - 2f * u);
                float phi = phi0 + sweep * q;
                pos = bobber + new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)) * r;
                vel = new Vector2(-Mathf.Sin(phi), Mathf.Cos(phi)) * side;
            }
            else if (tau < pauseEnd)
            {
                pos = hang;
                vel = bobber - hang;                          // she hangs facing the bait
            }
            else
            {
                float p = tTake > 0f ? Mathf.Clamp01((tau - pauseEnd) / tTake) : 1f;
                pos = hang + (bobber - hang) * (p * p);       // a quick dart onto it
                vel = bobber - hang;
            }

            pose.Offset = pos;
            pose.HeadingRadians = HeadingOf(vel, HeadingOf(bobber - start, 0f));
            pose.Pose = SwimFishPose.Swim;
            pose.PoseSeconds = beat;

            // Her flank flashes as she turns into the circle and as she turns to face the bait.
            if (rollSeconds > 0f)
            {
                if (tCircle > 0f && tau >= tIn && tau < tIn + rollSeconds)
                {
                    pose.Pose = SwimFishPose.Roll;
                    pose.PoseSeconds = tau - tIn;
                }
                else if (tPause > 0f && tau >= circleEnd && tau < circleEnd + rollSeconds)
                {
                    pose.Pose = SwimFishPose.Roll;
                    pose.PoseSeconds = tau - circleEnd;
                }
            }
        }
    }
}
