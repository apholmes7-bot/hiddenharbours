using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    public enum SailRaceClubRule { None, PortStarboard, WindwardLeeward, MarkRoom }

    public readonly struct SailRaceEntitlement
    {
        public readonly string GiveWayId;
        public readonly SailRaceClubRule Rule;
        internal SailRaceEntitlement(string giveWayId, SailRaceClubRule rule) { GiveWayId = giveWayId; Rule = rule; }
    }

    public readonly struct SailRaceClubBoat
    {
        public readonly RaceBoatMotion Motion;
        public readonly int Lap, NextMark;
        public readonly float OverlapLengthMetres;
        public SailRaceClubBoat(RaceBoatMotion motion, int lap, int nextMark, float overlapLengthMetres)
        {
            ValidateMotion(motion);
            if (lap < 0 || nextMark < 0) throw new ArgumentOutOfRangeException(nameof(lap));
            RaceGeometry.RequirePositive(overlapLengthMetres, nameof(overlapLengthMetres));
            Motion = motion; Lap = lap; NextMark = nextMark; OverlapLengthMetres = overlapLengthMetres;
        }
        internal static void ValidateMotion(RaceBoatMotion motion)
        {
            RaceGeometry.RequireId(motion.Id, "racer.");
            RaceGeometry.RequireFinite(motion.Position, nameof(motion.Position));
            RaceGeometry.RequireFinite(motion.Forward, nameof(motion.Forward));
            RaceGeometry.RequireFinite(motion.Velocity, nameof(motion.Velocity));
            if (motion.Forward.sqrMagnitude == 0f) throw new ArgumentException("Heading required.");
        }
    }

    /// <summary>Current-geometry club rules, independent of contact history. Opposite tacks first;
    /// same-tack overlapping inside boats receive room, then windward gives way. Pair order is irrelevant.</summary>
    public static class SailRaceClubJudge
    {
        public static SailRaceEntitlement Judge(SailRaceClubBoat a, SailRaceClubBoat b, Vector2 wind, SailRaceCourse course)
        {
            if (course == null) throw new ArgumentNullException(nameof(course));
            SailRaceClubBoat.ValidateMotion(a.Motion); SailRaceClubBoat.ValidateMotion(b.Motion);
            RaceGeometry.RequirePositive(a.OverlapLengthMetres, nameof(a.OverlapLengthMetres));
            RaceGeometry.RequirePositive(b.OverlapLengthMetres, nameof(b.OverlapLengthMetres));
            RaceGeometry.RequireFinite(wind, nameof(wind));
            if (a.Motion.Id == b.Motion.Id) throw new ArgumentException("Distinct boats required.");
            if (wind.magnitude <= course.Rules.CalmWindMetresPerSecond) return default;
            float aw = SailDrive.ResolveWind(wind, a.Motion.Forward, a.Motion.Velocity).TrueAngleDeg;
            float bw = SailDrive.ResolveWind(wind, b.Motion.Forward, b.Motion.Velocity).TrueAngleDeg;
            if (aw == 0f || bw == 0f || Mathf.Abs(aw) == 180f || Mathf.Abs(bw) == 180f) return default;
            if ((aw > 0f) != (bw > 0f))
                return new SailRaceEntitlement(aw > 0f ? b.Motion.Id : a.Motion.Id, SailRaceClubRule.PortStarboard);
            if (a.Lap == b.Lap && a.NextMark == b.NextMark && a.NextMark >= 0 && a.NextMark < course.MarkCount)
            {
                RaceMark mark = course.Mark(a.NextMark);
                float ar = Vector2.Distance(a.Motion.Position, mark.Position), br = Vector2.Distance(b.Motion.Position, mark.Position);
                Vector2 heading = (a.Motion.Forward + b.Motion.Forward).normalized;
                bool overlap = Mathf.Abs(Vector2.Dot(b.Motion.Position - a.Motion.Position, heading)) <
                    (a.OverlapLengthMetres + b.OverlapLengthMetres) * 0.5f;
                if (ar <= mark.ZoneRadiusMetres && br <= mark.ZoneRadiusMetres && ar != br &&
                    Vector2.Dot(a.Motion.Forward, b.Motion.Forward) > 0f && overlap)
                    return new SailRaceEntitlement(ar > br ? a.Motion.Id : b.Motion.Id, SailRaceClubRule.MarkRoom);
            }
            float lee = Vector2.Dot(b.Motion.Position - a.Motion.Position, wind.normalized);
            return lee == 0f ? default : new SailRaceEntitlement(lee > 0f ? a.Motion.Id : b.Motion.Id,
                SailRaceClubRule.WindwardLeeward);
        }
    }
}
