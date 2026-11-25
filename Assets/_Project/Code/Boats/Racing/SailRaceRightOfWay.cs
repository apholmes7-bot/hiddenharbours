using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    public readonly struct RaceBoatMotion
    {
        public readonly string Id;
        public readonly Vector2 Position, Forward, Velocity;
        public RaceBoatMotion(string id, Vector2 position, Vector2 forward, Vector2 velocity)
        {
            RaceGeometry.RequireId(id, "racer.");
            RaceGeometry.RequireFinite(position, nameof(position));
            RaceGeometry.RequireFinite(forward, nameof(forward));
            RaceGeometry.RequireFinite(velocity, nameof(velocity));
            if (forward.sqrMagnitude == 0f) throw new ArgumentException("A boat needs a heading.");
            Id = id; Position = position; Forward = forward.normalized; Velocity = velocity;
        }
    }

    public readonly struct RacePenalty
    {
        public readonly string OffenderId, OtherId;
        public readonly double Time;
        public readonly float Seconds;
        internal RacePenalty(string offender, string other, double time, float seconds)
        { OffenderId = offender; OtherId = other; Time = time; Seconds = seconds; }
    }

    /// <summary>Deliberately small game rules: overtaker keeps clear; otherwise port gives way to
    /// starboard, then windward to leeward on the same tack. No mark-room/protest simulation.</summary>
    public sealed class SailRaceRightOfWay
    {
        private readonly SailRaceRules _rules;
        private readonly double _gunTime;
        private readonly Dictionary<(string, string), Contact> _contacts = new Dictionary<(string, string), Contact>();
        private struct Contact { public double Time; public bool Active; }

        public SailRaceRightOfWay(SailRaceRules rules, double gunTime)
        { rules.Validate(); RaceGeometry.RequireFinite(gunTime, nameof(gunTime)); _rules = rules; _gunTime = gunTime; }

        // Call once per pair at a shared game time. Exact duplicate observations are idempotent.
        // A contact remains latched until separation exceeds the release radius.
        public RacePenalty? Observe(double time, RaceBoatMotion a, RaceBoatMotion b, Vector2 trueWindMs)
        {
            RaceGeometry.RequireFinite(time, nameof(time));
            RaceGeometry.RequireFinite(trueWindMs, nameof(trueWindMs));
            if (a.Id == null || b.Id == null || a.Id == b.Id) throw new ArgumentException("Two distinct entrants required.");
            var key = string.CompareOrdinal(a.Id, b.Id) < 0 ? (a.Id, b.Id) : (b.Id, a.Id);
            bool seen = _contacts.TryGetValue(key, out var contact);
            if (seen && time < contact.Time) throw new ArgumentException("Contact observations cannot go backwards.");
            if (seen && time == contact.Time) return null;
            float distance = Vector2.Distance(a.Position, b.Position);
            if (distance > _rules.ContactReleaseMetres) contact.Active = false;
            bool entered = !contact.Active && distance <= _rules.ContactDistanceMetres && time >= _gunTime;
            contact.Time = time;
            contact.Active |= entered;
            _contacts[key] = contact;
            if (!entered) return null;
            string offender = GiveWay(a, b, trueWindMs, _rules);
            return offender == null ? (RacePenalty?)null : new RacePenalty(offender, offender == a.Id ? b.Id : a.Id,
                time, _rules.RightOfWayPenaltySeconds);
        }

        public static string GiveWay(RaceBoatMotion a, RaceBoatMotion b, Vector2 trueWindMs, SailRaceRules rules)
        {
            bool aOvertakes = Overtakes(a, b, rules.OvertakingConeHalfDeg);
            bool bOvertakes = Overtakes(b, a, rules.OvertakingConeHalfDeg);
            if (aOvertakes != bOvertakes) return aOvertakes ? a.Id : b.Id;
            if (trueWindMs.magnitude <= rules.CalmWindMetresPerSecond) return null;
            var aw = SailDrive.ResolveWind(trueWindMs, a.Forward, a.Velocity);
            var bw = SailDrive.ResolveWind(trueWindMs, b.Forward, b.Velocity);
            // Head-to-wind and dead astern do not establish sail side in a motion-only sample.
            if (aw.TrueAngleDeg == 0f || bw.TrueAngleDeg == 0f ||
                Mathf.Abs(aw.TrueAngleDeg) == 180f || Mathf.Abs(bw.TrueAngleDeg) == 180f) return null;
            bool aStarboard = aw.TrueAngleDeg > 0f, bStarboard = bw.TrueAngleDeg > 0f;
            if (aStarboard != bStarboard) return aStarboard ? b.Id : a.Id;
            float lee = Vector2.Dot(b.Position - a.Position, trueWindMs.normalized);
            return lee > 0f ? a.Id : lee < 0f ? b.Id : null;
        }

        private static bool Overtakes(RaceBoatMotion follower, RaceBoatMotion leader, float halfCone)
        {
            Vector2 behind = follower.Position - leader.Position;
            if (behind.sqrMagnitude == 0f) return false;
            float aft = Vector2.Dot(behind.normalized, -leader.Forward);
            float closing = Vector2.Dot(follower.Velocity - leader.Velocity, -behind.normalized);
            return aft >= Mathf.Cos(halfCone * Mathf.Deg2Rad) && closing > 0f;
        }
    }
}
