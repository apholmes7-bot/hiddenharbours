using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>What an NPC trawler is doing at an instant of her day.</summary>
    public enum NpcTrawlBoatStage
    {
        /// <summary>Fast at her berth: before the crew come down or after she is home.</summary>
        AtBerth,
        /// <summary>Alongside with the crew aboard, engine running.</summary>
        Boarding,
        /// <summary>The lines are coming off.</summary>
        LettingGo,
        /// <summary>Under way in her harbour, outbound.</summary>
        HarbourOut,
        /// <summary>Steaming across the open water to her ground.</summary>
        PassageOut,
        /// <summary>Working her ground.</summary>
        OnTheGround,
        /// <summary>Steaming back to the seam.</summary>
        PassageHome,
        /// <summary>Under way in her harbour, inbound.</summary>
        HarbourHome,
        /// <summary>Alongside, the lines going ashore.</summary>
        MakingFast,
    }

    /// <summary>The trawler at one instant: a lookup on the day's track, never an integration.</summary>
    public readonly struct NpcTrawlBoatPose
    {
        /// <summary>World metres, in the region <see cref="OnTheOpenWater"/> names.</summary>
        public readonly Vector2 Position;
        /// <summary>Compass heading of the bow (0 = North, clockwise).</summary>
        public readonly float HeadingDegrees;
        public readonly NpcTrawlBoatStage Stage;
        /// <summary>False: she is in her harbour's region. True: she is in her ground's region.</summary>
        public readonly bool OnTheOpenWater;
        /// <summary>Progress through the stage's current leg, 0 to 1.</summary>
        public readonly float Stage01;

        public NpcTrawlBoatPose(Vector2 position, float headingDegrees, NpcTrawlBoatStage stage, bool onTheOpenWater,
                                float stage01)
        {
            Position = position;
            HeadingDegrees = headingDegrees;
            Stage = stage;
            OnTheOpenWater = onTheOpenWater;
            Stage01 = stage01;
        }

        /// <summary>True while she is making way through the water.</summary>
        public bool UnderWay => Stage == NpcTrawlBoatStage.HarbourOut || Stage == NpcTrawlBoatStage.PassageOut
                                || Stage == NpcTrawlBoatStage.OnTheGround || Stage == NpcTrawlBoatStage.PassageHome
                                || Stage == NpcTrawlBoatStage.HarbourHome;
    }

    /// <summary>
    /// <b>One NPC trawler's whole day, as a pure function of <c>(def, worldSeed, dayIndex, secondsPerDay)</c></b>
    /// (rule 5). Built once per day; after that her region, position and heading at any clock second are a
    /// lookup. Nothing is saved: load the game at any hour and she is where the clock says.
    ///
    /// <para><b>The day.</b> Crew aboard, let go, out of the harbour to the channel mouth; at that same second
    /// she is at the seam of the open water; across to her ground; up and down her working track for the hours
    /// the Def gives; back to the seam, in through the channel mouth, alongside and fast.</para>
    ///
    /// <para>This plan is the passage only. Shooting, towing and hauling are legs the next step adds inside
    /// <see cref="NpcTrawlBoatStage.OnTheGround"/>; nothing here fishes.</para>
    ///
    /// <para>Engine-light: Vector2 and Mathf only, no allocation after <see cref="Build"/>.</para>
    /// </summary>
    public sealed class NpcTrawlDay
    {
        private struct Segment
        {
            public double T0, T1;
            public Vector2 A, B;
            public float HeadFrom, HeadTo, TurnSeconds;
            public NpcTrawlBoatStage Stage;
            public bool Outside;
        }

        private const int StreamDepart = 1;

        private Segment[] _segments = Array.Empty<Segment>();
        private Vector2 _berth;
        private float _berthHeading;

        public int DayIndex { get; private set; }
        /// <summary>Clock second the day begins.</summary>
        public double DayStartSeconds { get; private set; }
        /// <summary>Clock second the crew are aboard: the first second she is not simply at her berth.</summary>
        public double DepartSeconds { get; private set; }
        /// <summary>Clock second she leaves the harbour's region and is at the seam of the open water.</summary>
        public double OutsideFromSeconds { get; private set; }
        /// <summary>Clock second she reaches her working track.</summary>
        public double OnGroundFromSeconds { get; private set; }
        /// <summary>Clock second she leaves her working track for home.</summary>
        public double OnGroundUntilSeconds { get; private set; }
        /// <summary>Clock second she leaves the open water and is at the channel mouth.</summary>
        public double InsideFromSeconds { get; private set; }
        /// <summary>Clock second she is fast at her berth again.</summary>
        public double ReturnSeconds { get; private set; }
        /// <summary>The legs of the day, for tests and for laying the track on a chart.</summary>
        public int LegCount => _segments.Length;

        /// <summary>Why <paramref name="def"/> cannot make a day, or null when it can.</summary>
        public static string Problem(NpcTrawlDayDef def)
        {
            if (def == null) return "no Def";
            if (def.HarbourOut == null || def.HarbourOut.Length == 0) return "HarbourOut is empty";
            if (def.HarbourHome == null || def.HarbourHome.Length == 0) return "HarbourHome is empty";
            if (def.PassageOut == null || def.PassageOut.Length == 0) return "PassageOut is empty";
            if (def.PassageHome == null || def.PassageHome.Length == 0) return "PassageHome is empty";
            if (def.WorkTrack == null || def.WorkTrack.Length < 2) return "WorkTrack has fewer than two points";
            if (def.HarbourSpeed <= 0f || def.CruiseSpeed <= 0f || def.WorkSpeed <= 0f) return "a speed is not positive";
            if (TrackLength(def.WorkTrack) < 1f) return "WorkTrack has no length";
            return null;
        }

        /// <summary>Builds the day. A Def with a <see cref="Problem"/> gives a day at the berth.</summary>
        public static NpcTrawlDay Build(NpcTrawlDayDef def, int worldSeed, int dayIndex, double secondsPerDay)
        {
            var day = new NpcTrawlDay
            {
                DayIndex = dayIndex,
                DayStartSeconds = dayIndex * secondsPerDay,
            };
            if (def != null)
            {
                day._berth = def.BerthPosition;
                day._berthHeading = def.BerthHeadingDegrees;
            }
            if (Problem(def) != null)
            {
                day.DepartSeconds = day.OutsideFromSeconds = day.OnGroundFromSeconds = day.OnGroundUntilSeconds
                    = day.InsideFromSeconds = day.ReturnSeconds = day.DayStartSeconds;
                return day;
            }

            double gameMinute = secondsPerDay / 1440.0;
            double jitter = (NpcPotDay.Hash01(worldSeed, def.Id, dayIndex, -1, StreamDepart) * 2.0 - 1.0)
                            * def.DepartJitterMinutes * gameMinute;
            day.DepartSeconds = day.DayStartSeconds + def.DepartHour / 24.0 * secondsPerDay + jitter;

            var segments = new List<Segment>(def.HarbourOut.Length + def.HarbourHome.Length + def.PassageOut.Length
                                             + def.PassageHome.Length + 16);
            var cursor = new Cursor
            {
                T = day.DepartSeconds,
                Position = def.BerthPosition,
                Heading = def.BerthHeadingDegrees,
                Turn = def.TurnSeconds,
            };

            // In harbour, outbound.
            cursor.Dwell(segments, def.BoardSeconds, NpcTrawlBoatStage.Boarding, false, def.BerthHeadingDegrees);
            cursor.Dwell(segments, def.LetGoSeconds, NpcTrawlBoatStage.LettingGo, false, def.BerthHeadingDegrees);
            for (int k = 0; k < def.HarbourOut.Length; k++)
            {
                cursor.Move(segments, def.HarbourOut[k], def.HarbourSpeed, NpcTrawlBoatStage.HarbourOut, false,
                            holdHeading: k < def.SternFirstLegCount);
            }

            // The same second, the other region: she is at the seam, bow along her first leg.
            day.OutsideFromSeconds = cursor.T;
            cursor.Position = def.PassageOut[0];
            Vector2 firstAim = def.PassageOut.Length > 1 ? def.PassageOut[1] : def.WorkTrack[0];
            cursor.Heading = NpcPotDay.Bearing(cursor.Position, firstAim, cursor.Heading);
            for (int k = 1; k < def.PassageOut.Length; k++)
                cursor.Move(segments, def.PassageOut[k], def.CruiseSpeed, NpcTrawlBoatStage.PassageOut, true, false);
            cursor.Move(segments, def.WorkTrack[0], def.CruiseSpeed, NpcTrawlBoatStage.PassageOut, true, false);

            // On the ground: up the track and back, for the hours the Def gives.
            day.OnGroundFromSeconds = cursor.T;
            double leaveAt = cursor.T + def.OnGroundHours / 24.0 * secondsPerDay;
            int at = 0, step = 1;
            int guard = 0;
            while (cursor.T < leaveAt && guard++ < MaxWorkLegs)
            {
                int next = at + step;
                if (next < 0 || next >= def.WorkTrack.Length) { step = -step; next = at + step; }
                Vector2 to = def.WorkTrack[next];
                double legSeconds = (to - cursor.Position).magnitude / Mathf.Max(0.01f, def.WorkSpeed);
                double left = leaveAt - cursor.T;
                if (legSeconds > left)
                {
                    // She turns for home part way along the leg.
                    to = Vector2.Lerp(cursor.Position, to, (float)(left / legSeconds));
                    cursor.Move(segments, to, def.WorkSpeed, NpcTrawlBoatStage.OnTheGround, true, false);
                    break;
                }
                cursor.Move(segments, to, def.WorkSpeed, NpcTrawlBoatStage.OnTheGround, true, false);
                at = next;
            }
            day.OnGroundUntilSeconds = cursor.T;

            for (int k = 0; k < def.PassageHome.Length; k++)
                cursor.Move(segments, def.PassageHome[k], def.CruiseSpeed, NpcTrawlBoatStage.PassageHome, true, false);

            // The same second, back in her harbour's region at the channel mouth.
            day.InsideFromSeconds = cursor.T;
            cursor.Position = def.HarbourHome[0];
            Vector2 homeAim = def.HarbourHome.Length > 1 ? def.HarbourHome[1] : def.BerthPosition;
            cursor.Heading = NpcPotDay.Bearing(cursor.Position, homeAim, cursor.Heading);
            for (int k = 1; k < def.HarbourHome.Length; k++)
                cursor.Move(segments, def.HarbourHome[k], def.HarbourSpeed, NpcTrawlBoatStage.HarbourHome, false, false);
            cursor.Move(segments, def.BerthPosition, def.HarbourSpeed, NpcTrawlBoatStage.HarbourHome, false, false);
            cursor.Dwell(segments, def.MakeFastSeconds, NpcTrawlBoatStage.MakingFast, false, def.BerthHeadingDegrees);

            day.ReturnSeconds = cursor.T;
            day._segments = segments.ToArray();
            return day;
        }

        // A working track is walked a bounded number of times however short it is authored.
        private const int MaxWorkLegs = 4096;

        /// <summary>True when she is fast again before the day ends.</summary>
        public bool FitsTheDay(double secondsPerDay)
            => DepartSeconds >= DayStartSeconds && ReturnSeconds <= DayStartSeconds + secondsPerDay;

        /// <summary>True when <paramref name="totalSeconds"/> has her in her ground's region.</summary>
        public bool IsOutside(double totalSeconds)
            => _segments.Length > 0 && totalSeconds >= OutsideFromSeconds && totalSeconds < InsideFromSeconds;

        /// <summary>The trawler at <paramref name="totalSeconds"/>. Outside the working day she is at her berth.</summary>
        public NpcTrawlBoatPose PoseAt(double totalSeconds)
        {
            if (_segments.Length == 0 || totalSeconds < DepartSeconds || totalSeconds >= ReturnSeconds)
                return new NpcTrawlBoatPose(_berth, _berthHeading, NpcTrawlBoatStage.AtBerth, false, 0f);

            int lo = 0, hi = _segments.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (totalSeconds >= _segments[mid].T1) lo = mid + 1; else hi = mid;
            }

            Segment s = _segments[lo];
            double span = s.T1 - s.T0;
            float f = span > 1e-9 ? Mathf.Clamp01((float)((totalSeconds - s.T0) / span)) : 1f;
            float turn = s.TurnSeconds > 1e-4f
                ? Mathf.Clamp01((float)((totalSeconds - s.T0) / s.TurnSeconds))
                : 1f;
            float heading = Mathf.LerpAngle(s.HeadFrom, s.HeadTo, Mathf.SmoothStep(0f, 1f, turn));
            heading = Mathf.Repeat(heading, 360f);
            return new NpcTrawlBoatPose(Vector2.Lerp(s.A, s.B, f), heading, s.Stage, s.Outside, f);
        }

        /// <summary>
        /// Leg <paramref name="index"/> of the day: its two ends, its region and its stage. For the content
        /// tests that sound every leg, and for a chart that draws her track.
        /// </summary>
        public void LegAt(int index, out Vector2 from, out Vector2 to, out bool onTheOpenWater,
                          out NpcTrawlBoatStage stage, out double startSeconds, out double endSeconds)
        {
            Segment s = _segments[index];
            from = s.A;
            to = s.B;
            onTheOpenWater = s.Outside;
            stage = s.Stage;
            startSeconds = s.T0;
            endSeconds = s.T1;
        }

        private static float TrackLength(Vector2[] track)
        {
            float length = 0f;
            for (int i = 1; i < track.Length; i++) length += (track[i] - track[i - 1]).magnitude;
            return length;
        }

        private struct Cursor
        {
            public double T;
            public Vector2 Position;
            public float Heading;
            public float Turn;

            public void Move(List<Segment> into, Vector2 to, float speed, NpcTrawlBoatStage stage, bool outside,
                             bool holdHeading)
            {
                float length = (to - Position).magnitude;
                if (length < 1e-3f) return;
                float leg = holdHeading ? Heading : NpcPotDay.Bearing(Position, to, Heading);
                double duration = length / Mathf.Max(0.01f, speed);
                into.Add(new Segment
                {
                    T0 = T, T1 = T + duration, A = Position, B = to,
                    HeadFrom = Heading, HeadTo = leg, TurnSeconds = Mathf.Min(Turn, (float)duration),
                    Stage = stage, Outside = outside,
                });
                T += duration;
                Position = to;
                Heading = leg;
            }

            public void Dwell(List<Segment> into, float seconds, NpcTrawlBoatStage stage, bool outside, float headTo)
            {
                if (seconds <= 0f) return;
                into.Add(new Segment
                {
                    T0 = T, T1 = T + seconds, A = Position, B = Position,
                    HeadFrom = Heading, HeadTo = headTo, TurnSeconds = Mathf.Min(Turn, seconds),
                    Stage = stage, Outside = outside,
                });
                T += seconds;
                Heading = headTo;
            }
        }
    }
}
