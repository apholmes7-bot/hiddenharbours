using System;

namespace HiddenHarbours.Fishing
{
    public enum BenthicMotion { Sessile, WalkAndShelter, Scuttle }
    public enum BenthicActivity { Attached, Sheltering, Resting, Walking, Scuttling }

    /// <summary>World ground-plane XY and bed elevation, all in metres. No sprite/body offset.</summary>
    public readonly struct BenthicPoint
    {
        public readonly double X, Y, Elevation;

        public BenthicPoint(double x, double y, double elevation)
        {
            X = x;
            Y = y;
            Elevation = elevation;
        }

        internal bool IsFinite => BenthicMovement.Finite(X) && BenthicMovement.Finite(Y) &&
                                  BenthicMovement.Finite(Elevation);
    }

    /// <summary>
    /// Caller-certified straight bed segment, including its slope. Certification means EVERY point
    /// on the segment is supported, traversable substrate, not merely that its endpoints touch bed.
    /// This value does not sample terrain or manufacture that certificate. No certificate: no pose.
    /// Home is a shelter entrance for walking animals or an attachment/burrow for sessile animals.
    /// </summary>
    public readonly struct BenthicSegment
    {
        public readonly BenthicPoint Home, Away;
        public readonly bool SupportCertified;

        public BenthicSegment(BenthicPoint home, BenthicPoint away, bool supportCertified)
        {
            Home = home;
            Away = away;
            SupportCertified = supportCertified;
        }

        public double LengthMetres
        {
            get
            {
                double dx = Away.X - Home.X, dy = Away.Y - Home.Y;
                double dz = Away.Elevation - Home.Elevation;
                return Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }

        internal BenthicPoint At(double fraction) => new BenthicPoint(
            (1 - fraction) * Home.X + fraction * Away.X,
            (1 - fraction) * Home.Y + fraction * Away.Y,
            (1 - fraction) * Home.Elevation + fraction * Away.Elevation);
    }

    /// <summary>
    /// Explicit, uncalibrated inputs; no species defaults or existing balance changes. A future
    /// reviewed Def adapter supplies speed (metres per Core game-clock second) and both rest times
    /// (Core game-clock seconds, not real seconds or an assumed 86,400-second day).
    /// </summary>
    public readonly struct BenthicMovementProfile
    {
        public readonly BenthicMotion Motion;
        public readonly double SpeedMetresPerGameSecond, HomeRestGameSeconds, AwayRestGameSeconds;

        public BenthicMovementProfile(BenthicMotion motion, double speedMetresPerGameSecond,
            double homeRestGameSeconds, double awayRestGameSeconds)
        {
            Motion = motion;
            SpeedMetresPerGameSecond = speedMetresPerGameSecond;
            HomeRestGameSeconds = homeRestGameSeconds;
            AwayRestGameSeconds = awayRestGameSeconds;
        }
    }

    public readonly struct BenthicPose
    {
        public readonly BenthicPoint Position;
        public readonly double FractionAlongSegment;
        public readonly BenthicActivity Activity;

        internal BenthicPose(BenthicPoint position, double fraction, BenthicActivity activity)
        {
            Position = position;
            FractionAlongSegment = fraction;
            Activity = activity;
        }
    }

    /// <summary>
    /// Inactive, stateless motion arithmetic. No callers in gameplay, no save state, no terrain,
    /// renderer or SchoolField dependency. A fixed world seed, stable occupant id, game time and
    /// immutable inputs produce the same pose in any query order. Does not model capture, escape
    /// reactions, tides/exposure, habitat certification, connected paths or physical collisions.
    /// </summary>
    public static class BenthicMovement
    {
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>Returns false for unknown support or invalid inputs, never a floating fallback.</summary>
        public static bool TrySample(int worldSeed, string stableOccupantId, double gameSeconds,
            in BenthicSegment segment, in BenthicMovementProfile profile, out BenthicPose pose)
        {
            pose = default;
            if (string.IsNullOrEmpty(stableOccupantId) || !Finite(gameSeconds) ||
                !segment.SupportCertified || !segment.Home.IsFinite || !segment.Away.IsFinite ||
                !Finite(profile.SpeedMetresPerGameSecond) || profile.SpeedMetresPerGameSecond < 0 ||
                !Finite(profile.HomeRestGameSeconds) || profile.HomeRestGameSeconds < 0 ||
                !Finite(profile.AwayRestGameSeconds) || profile.AwayRestGameSeconds < 0)
                return false;

            if (profile.Motion == BenthicMotion.Sessile)
            {
                pose = new BenthicPose(segment.Home, 0, BenthicActivity.Attached);
                return true;
            }
            if ((profile.Motion != BenthicMotion.WalkAndShelter && profile.Motion != BenthicMotion.Scuttle) ||
                profile.SpeedMetresPerGameSecond <= 0 ||
                (profile.Motion == BenthicMotion.WalkAndShelter && profile.HomeRestGameSeconds <= 0))
                return false;

            double length = segment.LengthMetres;
            double travel = length / profile.SpeedMetresPerGameSecond;
            double period = profile.HomeRestGameSeconds + travel + profile.AwayRestGameSeconds + travel;
            if (!Finite(length) || length <= 0 || !Finite(travel) || travel <= 0 ||
                !Finite(period) || period <= 0)
                return false;

            // Reuse the existing process-stable integer hash. Constants here define the hash,
            // not species balance. Reduce double time BEFORE adding the permanent phase.
            uint key = StableHash.Finalize(StableHash.Fold(StableHash.Fold(2166136261u, worldSeed), stableOccupantId));
            double phase = key / 4294967296.0 * period;
            double time = gameSeconds % period;
            if (time < 0) time += period;
            // Avoid overflow even for very long, finite authored periods.
            time = time >= period - phase ? time - (period - phase) : time + phase;

            double fraction;
            BenthicActivity activity;
            if (time < profile.HomeRestGameSeconds)
            {
                fraction = 0;
                activity = profile.Motion == BenthicMotion.WalkAndShelter
                    ? BenthicActivity.Sheltering : BenthicActivity.Resting;
            }
            else if ((time -= profile.HomeRestGameSeconds) < travel)
            {
                fraction = time / travel;
                activity = Moving(profile.Motion);
            }
            else if ((time -= travel) < profile.AwayRestGameSeconds)
            {
                fraction = 1;
                activity = BenthicActivity.Resting;
            }
            else
            {
                fraction = 1 - (time - profile.AwayRestGameSeconds) / travel;
                activity = Moving(profile.Motion);
            }
            fraction = Math.Max(0, Math.Min(1, fraction));
            pose = new BenthicPose(segment.At(fraction), fraction, activity);
            return true;
        }

        private static BenthicActivity Moving(BenthicMotion motion) =>
            motion == BenthicMotion.WalkAndShelter ? BenthicActivity.Walking : BenthicActivity.Scuttling;
    }
}
