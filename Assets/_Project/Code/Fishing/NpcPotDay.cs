using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>What an NPC pot boat is doing at an instant of her day.</summary>
    public enum NpcPotBoatStage
    {
        /// <summary>Moored: before she casts off or after she is made fast.</summary>
        AtBerth,
        PassageOut,
        /// <summary>Lying to a pot, drawing its buoy to the rail.</summary>
        Gaffing,
        /// <summary>The pot is coming up the rope.</summary>
        Hauling,
        /// <summary>The pot is aboard and being worked.</summary>
        DeckWork,
        /// <summary>Easing along the string between a haul and a set, or on to the next pot.</summary>
        Shifting,
        /// <summary>The pot is going back over.</summary>
        Setting,
        PassageHome,
        /// <summary>Alongside, swinging to the berth's heading.</summary>
        MakingFast,
    }

    /// <summary>Where one pot is in its day.</summary>
    public enum NpcPotPhase
    {
        /// <summary>On the bottom, buoy on the surface.</summary>
        Soaking,
        /// <summary>Its buoy is being drawn to the rail; the pot is still on the bottom.</summary>
        Gaffed,
        /// <summary>Coming up the rope.</summary>
        Hauling,
        /// <summary>On deck.</summary>
        Aboard,
        /// <summary>Going down again at today's position.</summary>
        Setting,
    }

    /// <summary>The boat at one instant: a lookup on the day's track, never an integration.</summary>
    public readonly struct NpcPotBoatPose
    {
        public readonly Vector2 Position;
        /// <summary>Compass heading of the bow (0 = North, clockwise).</summary>
        public readonly float HeadingDegrees;
        public readonly NpcPotBoatStage Stage;
        /// <summary>The pot being worked, or -1 on passage and at the berth.</summary>
        public readonly int PotIndex;
        /// <summary>Progress through the stage's current step, 0 to 1.</summary>
        public readonly float Stage01;
        /// <summary>Clock seconds in the current segment, for presentation of its timed movement.</summary>
        public readonly double StageSeconds;

        public NpcPotBoatPose(Vector2 position, float headingDegrees, NpcPotBoatStage stage, int potIndex, float stage01,
                             double stageSeconds = 0d)
        {
            Position = position;
            HeadingDegrees = headingDegrees;
            Stage = stage;
            PotIndex = potIndex;
            Stage01 = stage01;
            StageSeconds = stageSeconds;
        }

        /// <summary>True while she is making way through the water (lamps, wake).</summary>
        public bool UnderWay => Stage == NpcPotBoatStage.PassageOut || Stage == NpcPotBoatStage.PassageHome
                                || Stage == NpcPotBoatStage.Shifting;
    }

    /// <summary>One pot at one instant.</summary>
    public readonly struct NpcPotState
    {
        public readonly NpcPotPhase Phase;
        /// <summary>Progress through <see cref="Phase"/>, 0 to 1 (0 while soaking or aboard).</summary>
        public readonly float Phase01;
        /// <summary>Where the pot lies, or is going down, on the seabed.</summary>
        public readonly Vector2 Seabed;
        /// <summary>The clock second the pot last reached the bottom there: the start of its soak.</summary>
        public readonly double SetAtSeconds;

        public NpcPotState(NpcPotPhase phase, float phase01, Vector2 seabed, double setAtSeconds)
        {
            Phase = phase;
            Phase01 = phase01;
            Seabed = seabed;
            SetAtSeconds = setAtSeconds;
        }
    }

    /// <summary>
    /// <b>One NPC boat's whole day at the pots, as a pure function of <c>(def, worldSeed, dayIndex,
    /// secondsPerDay)</c></b> (rule 5). Built once per day; after that the boat's pose and every pot's state at
    /// any clock second are lookups. Load the game at any hour and boat, pots and buoys are where the clock says.
    ///
    /// <para><b>The day.</b> Cast off, run out, then at each pot in turn: lie to it with the pot to starboard,
    /// gaff the buoy, haul the pot, work it on deck, ease to today's position and set it again; then run home
    /// and make fast.</para>
    ///
    /// <para><b>Two days make a pot.</b> A pot lies where the latest set put it: today's once today's set is
    /// done, yesterday's until today's haul. So <see cref="PotAt"/> takes today's plan and yesterday's and needs
    /// nothing else. The fishery did not start with the player: day 0 hauls what day -1 set.</para>
    ///
    /// <para>Engine-light: Vector2 and Mathf only, no allocation after <see cref="Build"/>.</para>
    /// </summary>
    public sealed class NpcPotDay
    {
        private struct Segment
        {
            public double T0, T1;
            public Vector2 A, B;
            public float HeadFrom, HeadTo, TurnSeconds;
            public NpcPotBoatStage Stage;
            public int Pot;
        }

        private const int StreamDepart = 1;
        private const int StreamShift = 2;

        private Segment[] _segments = Array.Empty<Segment>();
        private Vector2 _berth;
        private float _berthHeading;

        public int DayIndex { get; private set; }
        /// <summary>Clock second the day begins.</summary>
        public double DayStartSeconds { get; private set; }
        /// <summary>Clock second she casts off.</summary>
        public double DepartSeconds { get; private set; }
        /// <summary>Clock second she is fast at the berth again.</summary>
        public double ReturnSeconds { get; private set; }
        public int PotCount => SetPositions.Length;

        /// <summary>Where today's set puts each pot.</summary>
        public Vector2[] SetPositions { get; private set; } = Array.Empty<Vector2>();
        /// <summary>Clock second the gaff reaches for each pot's buoy.</summary>
        public double[] GaffStart { get; private set; } = Array.Empty<double>();
        /// <summary>Clock second the buoy is at the rail and the pot starts up.</summary>
        public double[] HaulStart { get; private set; } = Array.Empty<double>();
        /// <summary>Clock second the pot is on deck.</summary>
        public double[] HaulEnd { get; private set; } = Array.Empty<double>();
        /// <summary>Clock second the pot goes back over.</summary>
        public double[] SetStart { get; private set; } = Array.Empty<double>();
        /// <summary>Clock second the pot is on the bottom and its buoy is streaming: the new soak begins.</summary>
        public double[] SetEnd { get; private set; } = Array.Empty<double>();

        /// <summary>The day a clock second falls in.</summary>
        public static int DayIndexOf(double totalSeconds, double secondsPerDay)
            => secondsPerDay > 0.0 ? (int)Math.Floor(totalSeconds / secondsPerDay) : 0;

        /// <summary>Why <paramref name="def"/> cannot make a day, or null when it can.</summary>
        public static string Problem(NpcPotDayDef def)
        {
            if (def == null) return "no Def";
            if (def.Trap == null) return "no Trap";
            if (def.PotCount < 1) return "PotCount is under 1";
            if (def.PassageOut == null || def.PassageOut.Length == 0) return "PassageOut is empty";
            if (def.PassageHome == null || def.PassageHome.Length == 0) return "PassageHome is empty";
            if (def.HarbourSpeed <= 0f || def.CruiseSpeed <= 0f || def.WorkSpeed <= 0f) return "a speed is not positive";
            if (def.DailyShiftMeters >= def.PotSpacingMeters) return "DailyShiftMeters reaches the next pot";
            return null;
        }

        /// <summary>The unit vector a compass heading points along (0 = North = +y, clockwise).</summary>
        public static Vector2 Along(float headingDegrees)
        {
            float r = headingDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

        /// <summary>The unit vector to starboard of a compass heading.</summary>
        public static Vector2 Starboard(float headingDegrees)
        {
            float r = headingDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), -Mathf.Sin(r));
        }

        /// <summary>The compass bearing from one point to another; <paramref name="fallback"/> when they coincide.</summary>
        public static float Bearing(Vector2 from, Vector2 to, float fallback)
        {
            Vector2 d = to - from;
            if (d.sqrMagnitude < 1e-8f) return fallback;
            float deg = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            return deg < 0f ? deg + 360f : deg;
        }

        /// <summary>A stable 0..1 draw for this boat, day, pot and stream. No hidden state.</summary>
        public static float Hash01(int worldSeed, string defId, int dayIndex, int potIndex, int stream)
        {
            uint h = 2166136261u;
            h = StableHash.Fold(h, worldSeed);
            h = StableHash.Fold(h, defId);
            h = StableHash.Fold(h, dayIndex);
            h = StableHash.Fold(h, potIndex);
            h = StableHash.Fold(h, stream);
            return (StableHash.Finalize(h) >> 8) / 16777216f;
        }

        /// <summary>Where day <paramref name="dayIndex"/>'s set puts pot <paramref name="potIndex"/> on the seabed.</summary>
        public static Vector2 PotPosition(NpcPotDayDef def, int worldSeed, int dayIndex, int potIndex)
        {
            float shift = Hash01(worldSeed, def.Id, dayIndex, potIndex, StreamShift) * def.DailyShiftMeters;
            return def.StringStart + Along(def.StringBearingDegrees) * (potIndex * def.PotSpacingMeters + shift);
        }

        /// <summary>Where she lies to work a pot at <paramref name="pot"/>: the pot to starboard, bow along the string.</summary>
        public static Vector2 StationFor(NpcPotDayDef def, Vector2 pot)
            => pot - Starboard(def.StringBearingDegrees) * def.LayAlongsideMeters;

        /// <summary>The stable instance id of an NPC pot, for the catch maths. Never a player pot's id.</summary>
        public static string PotInstanceId(NpcPotDayDef def, int potIndex) => "npc." + def.Id + "." + potIndex;

        /// <summary>Builds the day. Call <see cref="Problem"/> first; a Def with a problem gives a day at the berth.</summary>
        public static NpcPotDay Build(NpcPotDayDef def, int worldSeed, int dayIndex, double secondsPerDay)
        {
            var day = new NpcPotDay
            {
                DayIndex = dayIndex,
                DayStartSeconds = dayIndex * secondsPerDay,
            };
            if (Problem(def) != null)
            {
                day.DepartSeconds = day.ReturnSeconds = day.DayStartSeconds;
                return day;
            }

            day._berth = def.BerthPosition;
            day._berthHeading = def.BerthHeadingDegrees;

            int n = def.PotCount;
            day.SetPositions = new Vector2[n];
            day.GaffStart = new double[n];
            day.HaulStart = new double[n];
            day.HaulEnd = new double[n];
            day.SetStart = new double[n];
            day.SetEnd = new double[n];

            double gameMinute = secondsPerDay / 1440.0;
            double jitter = (Hash01(worldSeed, def.Id, dayIndex, -1, StreamDepart) * 2.0 - 1.0)
                            * def.DepartJitterMinutes * gameMinute;
            day.DepartSeconds = day.DayStartSeconds + def.DepartHour / 24.0 * secondsPerDay + jitter;

            var segments = new List<Segment>(def.PassageOut.Length + def.PassageHome.Length + n * 6 + 2);
            var cursor = new Cursor
            {
                T = day.DepartSeconds,
                Position = def.BerthPosition,
                Heading = def.BerthHeadingDegrees,
                Turn = def.TurnSeconds,
            };

            for (int k = 0; k < def.PassageOut.Length; k++)
            {
                float speed = k < def.HarbourLegCount ? def.HarbourSpeed : def.CruiseSpeed;
                cursor.Move(segments, def.PassageOut[k], speed, NpcPotBoatStage.PassageOut, -1, holdHeading: false);
            }

            float bearing = def.StringBearingDegrees;
            for (int i = 0; i < n; i++)
            {
                Vector2 lying = PotPosition(def, worldSeed, dayIndex - 1, i);   // where yesterday's set left it
                Vector2 today = PotPosition(def, worldSeed, dayIndex, i);
                day.SetPositions[i] = today;

                if (i == 0)
                    cursor.Move(segments, StationFor(def, lying), def.CruiseSpeed, NpcPotBoatStage.PassageOut, -1, holdHeading: false);
                else
                    cursor.Move(segments, StationFor(def, lying), def.WorkSpeed, NpcPotBoatStage.Shifting, i, holdHeading: true);

                day.GaffStart[i] = cursor.T;
                cursor.Dwell(segments, def.GaffSeconds, NpcPotBoatStage.Gaffing, i, bearing);
                day.HaulStart[i] = cursor.T;
                cursor.Dwell(segments, def.HaulSeconds, NpcPotBoatStage.Hauling, i, bearing);
                day.HaulEnd[i] = cursor.T;
                cursor.Dwell(segments, def.DeckWorkSeconds, NpcPotBoatStage.DeckWork, i, bearing);
                cursor.Move(segments, StationFor(def, today), def.WorkSpeed, NpcPotBoatStage.Shifting, i, holdHeading: true);
                day.SetStart[i] = cursor.T;
                cursor.Dwell(segments, def.SetSeconds, NpcPotBoatStage.Setting, i, bearing);
                day.SetEnd[i] = cursor.T;
            }

            for (int k = 0; k < def.PassageHome.Length; k++)
            {
                float speed = k >= def.PassageHome.Length - def.HarbourLegCount ? def.HarbourSpeed : def.CruiseSpeed;
                cursor.Move(segments, def.PassageHome[k], speed, NpcPotBoatStage.PassageHome, -1, holdHeading: false);
            }
            cursor.Move(segments, def.BerthPosition, def.HarbourSpeed, NpcPotBoatStage.PassageHome, -1, holdHeading: false);
            cursor.Dwell(segments, def.MakeFastSeconds, NpcPotBoatStage.MakingFast, -1, def.BerthHeadingDegrees);

            day.ReturnSeconds = cursor.T;
            day._segments = segments.ToArray();
            return day;
        }

        /// <summary>True when she is fast again before the day ends, which <see cref="PotAt"/> relies on.</summary>
        public bool FitsTheDay(double secondsPerDay)
            => DepartSeconds >= DayStartSeconds && ReturnSeconds <= DayStartSeconds + secondsPerDay;

        /// <summary>The boat at <paramref name="totalSeconds"/>. Outside the working day she is at her berth.</summary>
        public NpcPotBoatPose PoseAt(double totalSeconds)
        {
            if (_segments.Length == 0 || totalSeconds < DepartSeconds || totalSeconds >= ReturnSeconds)
                return new NpcPotBoatPose(_berth, _berthHeading, NpcPotBoatStage.AtBerth, -1, 0f);

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
            return new NpcPotBoatPose(Vector2.Lerp(s.A, s.B, f), heading, s.Stage, s.Pot, f, span);
        }

        /// <summary>
        /// Pot <paramref name="potIndex"/> at <paramref name="totalSeconds"/>, from today's plan and yesterday's.
        /// <paramref name="totalSeconds"/> must fall in <paramref name="today"/>.
        /// </summary>
        public static NpcPotState PotAt(NpcPotDay today, NpcPotDay yesterday, int potIndex, double totalSeconds)
        {
            int i = potIndex;
            if (today == null || yesterday == null || i < 0 || i >= today.PotCount || i >= yesterday.PotCount)
                return default;

            Vector2 lying = yesterday.SetPositions[i];
            double lyingSince = yesterday.SetEnd[i];
            if (totalSeconds < today.GaffStart[i])
                return new NpcPotState(NpcPotPhase.Soaking, 0f, lying, lyingSince);
            if (totalSeconds < today.HaulStart[i])
                return new NpcPotState(NpcPotPhase.Gaffed, Unit(today.GaffStart[i], today.HaulStart[i], totalSeconds), lying, lyingSince);
            if (totalSeconds < today.HaulEnd[i])
                return new NpcPotState(NpcPotPhase.Hauling, Unit(today.HaulStart[i], today.HaulEnd[i], totalSeconds), lying, lyingSince);

            Vector2 set = today.SetPositions[i];
            if (totalSeconds < today.SetStart[i])
                return new NpcPotState(NpcPotPhase.Aboard, 0f, set, today.SetEnd[i]);
            if (totalSeconds < today.SetEnd[i])
                return new NpcPotState(NpcPotPhase.Setting, Unit(today.SetStart[i], today.SetEnd[i], totalSeconds), set, today.SetEnd[i]);
            return new NpcPotState(NpcPotPhase.Soaking, 0f, set, today.SetEnd[i]);
        }

        private static float Unit(double from, double to, double t)
        {
            double span = to - from;
            return span > 1e-9 ? Mathf.Clamp01((float)((t - from) / span)) : 1f;
        }

        private struct Cursor
        {
            public double T;
            public Vector2 Position;
            public float Heading;
            public float Turn;

            public void Move(List<Segment> into, Vector2 to, float speed, NpcPotBoatStage stage, int pot, bool holdHeading)
            {
                float length = (to - Position).magnitude;
                if (length < 1e-3f) return;
                float leg = holdHeading ? Heading : Bearing(Position, to, Heading);
                double duration = length / Mathf.Max(0.01f, speed);
                into.Add(new Segment
                {
                    T0 = T, T1 = T + duration, A = Position, B = to,
                    HeadFrom = Heading, HeadTo = leg, TurnSeconds = Mathf.Min(Turn, (float)duration),
                    Stage = stage, Pot = pot,
                });
                T += duration;
                Position = to;
                Heading = leg;
            }

            public void Dwell(List<Segment> into, float seconds, NpcPotBoatStage stage, int pot, float headTo)
            {
                if (seconds <= 0f) return;
                into.Add(new Segment
                {
                    T0 = T, T1 = T + seconds, A = Position, B = Position,
                    HeadFrom = Heading, HeadTo = headTo, TurnSeconds = Mathf.Min(Turn, seconds),
                    Stage = stage, Pot = pot,
                });
                T += seconds;
                Heading = headTo;
            }
        }
    }
}
