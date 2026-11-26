using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Where the fish coming to a villager's bait is</b>, a pure function of the seconds left before her bite
    /// (presentation only, rule 5: nothing here is saved or drawn from a random stream). She starts
    /// <see cref="AnglerLook.ApproachFromM"/> beyond the bobber, off the line by
    /// <see cref="AnglerLook.ApproachOffLineDegrees"/> on the side her own fight seed picks, and eases in on a
    /// half-sine swing, slowing as she reaches the bait at the bite.
    /// </summary>
    public static class AnglerApproachMath
    {
        /// <summary>
        /// False when no approach is drawn now (approach seconds unset, or the bite further off than that).
        /// <paramref name="bobber"/> is the bait's offset from his stand; the outputs are her offset from the stand,
        /// her compass heading (radians, 0 = North, clockwise) and her opacity (0..alpha).
        /// </summary>
        public static bool At(in AnglerLook look, Vector2 bobber, float secondsToBite, int fightSeed,
                              out Vector2 offset, out float headingRadians, out float alpha01)
        {
            offset = bobber;
            headingRadians = 0f;
            alpha01 = 0f;
            if (!(look.ApproachSeconds > 0f) || float.IsNaN(secondsToBite) || secondsToBite > look.ApproachSeconds)
                return false;
            float p = Mathf.Clamp01(1f - secondsToBite / look.ApproachSeconds);
            Path(in look, bobber, fightSeed, p, out offset, out headingRadians);
            float fade = look.ApproachFadeSeconds > 0f
                ? Mathf.Clamp01((look.ApproachSeconds - Mathf.Max(0f, secondsToBite)) / look.ApproachFadeSeconds) : 1f;
            alpha01 = look.ApproachAlpha01 * fade;
            return true;
        }

        /// <summary>Her heading as she reaches the bait (the path's end), for holding her there through the
        /// bite.</summary>
        public static float ArrivalHeading(in AnglerLook look, Vector2 bobber, int fightSeed)
        {
            Path(in look, bobber, fightSeed, 1f, out _, out float heading);
            return heading;
        }

        /// <summary>A compass heading (radians, 0 = North, clockwise) along <paramref name="v"/>, or
        /// <paramref name="fallback"/> for a zero vector.</summary>
        public static float HeadingOf(Vector2 v, float fallback)
            => v.sqrMagnitude > 1e-8f ? Mathf.Atan2(v.x, v.y) : fallback;

        private static void Path(in AnglerLook look, Vector2 bobber, int fightSeed, float p,
                                 out Vector2 offset, out float headingRadians)
        {
            Vector2 dir = bobber.sqrMagnitude > 1e-8f ? bobber.normalized : Vector2.up;
            float side = (fightSeed & 1) == 0 ? 1f : -1f;
            float a = side * look.ApproachOffLineDegrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            var from = new Vector2(dir.x * c - dir.y * s, dir.x * s + dir.y * c);
            Vector2 start = bobber + from * look.ApproachFromM;
            Vector2 chord = bobber - start;
            Vector2 perp = chord.sqrMagnitude > 1e-8f ? new Vector2(-chord.y, chord.x).normalized * side : Vector2.zero;

            float e = 1f - (1f - p) * (1f - p);          // ease out: she slows onto the bait
            float de = 2f * (1f - p);
            offset = start + chord * e + perp * (look.ApproachSwingM * Mathf.Sin(Mathf.PI * e));
            Vector2 v = (chord + perp * (look.ApproachSwingM * Mathf.PI * Mathf.Cos(Mathf.PI * e))) * Mathf.Max(de, 1e-3f);
            headingRadians = HeadingOf(v, HeadingOf(chord, 0f));
        }
    }
}
