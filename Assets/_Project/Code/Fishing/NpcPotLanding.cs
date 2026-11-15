using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>What the skipper is doing with the day's crates once she is fast at her berth.</summary>
    public enum NpcLandingStep
    {
        /// <summary>Not landing: before she is home, with nothing to land, or once it is all done.</summary>
        Aboard,
        /// <summary>Picking a crate up off the deck.</summary>
        Lifting,
        /// <summary>Carrying it across the deck, up to the wharf and along to the buyer.</summary>
        Carrying,
        /// <summary>Putting it down at the buyer's counter.</summary>
        SettingDown,
        /// <summary>At the counter while the buyer weighs the lot.</summary>
        Selling,
        /// <summary>Walking back empty-handed: to the boat for the next crate, or home aboard at the end.</summary>
        WalkingBack,
    }

    /// <summary>The landing at one instant.</summary>
    public readonly struct NpcLandingMoment
    {
        public readonly NpcLandingStep Step;
        /// <summary>Where she is, world metres.</summary>
        public readonly Vector2 Position;
        /// <summary>The way she faces, compass degrees.</summary>
        public readonly float HeadingDegrees;
        /// <summary>The crate (grade) in her hands, or -1.</summary>
        public readonly int Crate;
        /// <summary>True once she is off the boat: on the ladder or the wharf.</summary>
        public readonly bool Ashore;
        public readonly float Step01;
        /// <summary>True on the ladder between her rail and the wharf top. <see cref="Step01"/> then runs
        /// from where the leg starts to where it ends, so a presenter can pin the foot of the ladder to
        /// the hull as the tide has it.</summary>
        public readonly bool OnLadder;

        /// <summary>World metres per clock second along this leg; zero during a dwell.</summary>
        public readonly Vector2 Velocity;

        public NpcLandingMoment(NpcLandingStep step, Vector2 position, float headingDegrees, int crate, bool ashore, float step01,
                                bool onLadder = false, Vector2 velocity = default)
        {
            Step = step; Position = position; HeadingDegrees = headingDegrees; Crate = crate; Ashore = ashore; Step01 = step01;
            OnLadder = onLadder; Velocity = velocity;
        }
    }

    /// <summary>
    /// <b>Landing the day's catch</b>, as a pure function of the day (rule 5). Once she is fast, every crate
    /// that holds a keeper is picked up off the deck, carried by hand over the rail, up to the wharf and
    /// along the Def's route to the buyer's counter, and put down there; she walks back for the next. When
    /// the last crate is down the buyer weighs the lot, and she walks home aboard.
    ///
    /// <para>Nothing is saved: where she is, which crate is in her hands and what stands at the counter at
    /// any clock second are lookups over the plan. The plan moves no money and touches no market.</para>
    /// </summary>
    public sealed class NpcPotLanding
    {
        private struct Key
        {
            public double T0, T1;
            public Vector2 A, B;
            public NpcLandingStep Step;
            public int Crate;
            public bool Ashore;
            public bool Ladder;
            public float Heading;
        }

        private readonly List<Key> _keys = new List<Key>();
        private int[] _crateCount = Array.Empty<int>();
        private double[] _liftedAt = Array.Empty<double>();
        private double[] _downAt = Array.Empty<double>();

        /// <summary>True when the day has at least one keeper to land.</summary>
        public bool HasLanding => _keys.Count > 0;
        public double StartSeconds { get; private set; }
        /// <summary>Clock second the buyer has weighed the lot.</summary>
        public double SoldAtSeconds { get; private set; }
        /// <summary>Clock second she is back aboard.</summary>
        public double EndSeconds { get; private set; }
        public int TripCount { get; private set; }
        public int GradeCount => _crateCount.Length;
        public int SoldCount { get; private set; }
        public float SoldKg { get; private set; }
        /// <summary>Where the crates are put down, world metres.</summary>
        public Vector2 CounterPosition { get; private set; }

        /// <summary>Where the crate for <paramref name="grade"/> stands on a boat at the Def's berth, world metres.</summary>
        public static Vector2 CrateWorld(NpcPotDayDef def, int grade)
            => ToWorld(def, def.CrateLocalMeters + grade * def.CrateStepLocalMeters);

        /// <summary>A hull-local point (x starboard, y bow) on a boat lying at the Def's berth, world metres.</summary>
        public static Vector2 ToWorld(NpcPotDayDef def, Vector2 localMeters)
            => def.BerthPosition
               + NpcPotDay.Starboard(def.BerthHeadingDegrees) * localMeters.x
               + NpcPotDay.Along(def.BerthHeadingDegrees) * localMeters.y;

        /// <summary>Null when the Def can land a catch; otherwise what is missing.</summary>
        public static string Problem(NpcPotDayDef def)
        {
            if (def == null) return "no Def";
            if (def.LandingRoute == null || def.LandingRoute.Length < 2)
                return "LandingRoute needs the top of the ladder and the buyer's counter at least";
            if (def.LandingWalkSpeed <= 0f) return "LandingWalkSpeed must be above zero";
            return null;
        }

        public static NpcPotLanding Build(NpcPotDayDef def, NpcPotDay today, NpcPotDayCatch caught)
        {
            var plan = new NpcPotLanding();
            if (def == null || today == null || caught == null || Problem(def) != null) return plan;

            int grades = (def.GradeBreaksMm != null ? def.GradeBreaksMm.Length : 0) + 1;
            plan._crateCount = new int[grades];
            plan._liftedAt = new double[grades];
            plan._downAt = new double[grades];
            int last = -1;
            for (int g = 0; g < grades; g++)
            {
                plan._crateCount[g] = caught.StowedAt(today.ReturnSeconds, g);
                plan._liftedAt[g] = double.MaxValue;
                plan._downAt[g] = double.MaxValue;
                if (plan._crateCount[g] > 0) last = g;
            }
            Vector2[] route = def.LandingRoute;
            plan.CounterPosition = route[route.Length - 1];
            plan.StartSeconds = today.ReturnSeconds + def.LandingStartDelaySeconds;
            plan.SoldAtSeconds = plan.EndSeconds = plan.StartSeconds;
            if (last < 0) return plan;

            Vector2 helm = ToWorld(def, def.LandingHelmLocalMeters);
            Vector2 rail = ToWorld(def, def.StepAshoreLocalMeters);
            double t = plan.StartSeconds;
            Vector2 at = helm;

            for (int g = 0; g < grades; g++)
            {
                if (plan._crateCount[g] <= 0) continue;
                Vector2 crate = CrateWorld(def, g);
                plan.Walk(ref t, ref at, crate, def.DeckWalkSeconds, NpcLandingStep.WalkingBack, -1, false);
                plan.Dwell(ref t, at, def.CrateLiftSeconds, NpcLandingStep.Lifting, g, false);
                plan._liftedAt[g] = t;

                plan.Walk(ref t, ref at, rail, def.DeckWalkSeconds, NpcLandingStep.Carrying, g, false);
                plan.Walk(ref t, ref at, route[0], def.LadderSeconds, NpcLandingStep.Carrying, g, true, ladder: true);
                for (int i = 1; i < route.Length; i++)
                    plan.Walk(ref t, ref at, route[i], Seconds(at, route[i], def.LandingWalkSpeed), NpcLandingStep.Carrying, g, true);
                plan.Dwell(ref t, at, def.CrateSetDownSeconds, NpcLandingStep.SettingDown, g, true);
                plan._downAt[g] = t;
                plan.TripCount++;

                if (g == last)
                {
                    plan.Dwell(ref t, at, def.SaleSeconds, NpcLandingStep.Selling, -1, true);
                    plan.SoldAtSeconds = t;
                }
                for (int i = route.Length - 2; i >= 0; i--)
                    plan.Walk(ref t, ref at, route[i], Seconds(at, route[i], def.LandingWalkSpeed), NpcLandingStep.WalkingBack, -1, true);
                plan.Walk(ref t, ref at, rail, def.LadderSeconds, NpcLandingStep.WalkingBack, -1, true, ladder: true);
            }
            plan.Walk(ref t, ref at, helm, def.DeckWalkSeconds, NpcLandingStep.WalkingBack, -1, false);
            plan.EndSeconds = t;
            plan.SoldCount = caught.KeptCount;
            plan.SoldKg = caught.KeptKg;
            return plan;
        }

        /// <summary>Where she is and what she holds at <paramref name="totalSeconds"/>.</summary>
        public NpcLandingMoment MomentAt(double totalSeconds)
        {
            if (_keys.Count == 0 || totalSeconds < StartSeconds || totalSeconds >= EndSeconds)
            {
                Vector2 rest = _keys.Count > 0 ? _keys[0].A : default;
                float head = _keys.Count > 0 ? _keys[0].Heading : 0f;
                return new NpcLandingMoment(NpcLandingStep.Aboard, rest, head, -1, false, 0f);
            }

            int lo = 0, hi = _keys.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (totalSeconds >= _keys[mid].T1) lo = mid + 1; else hi = mid;
            }
            Key k = _keys[lo];
            double span = k.T1 - k.T0;
            float u = span > 1e-9 ? Mathf.Clamp01((float)((totalSeconds - k.T0) / span)) : 1f;
            Vector2 velocity = span > 0d ? (k.B - k.A) / (float)span : Vector2.zero;
            return new NpcLandingMoment(k.Step, Vector2.LerpUnclamped(k.A, k.B, u), k.Heading, k.Crate, k.Ashore, u, k.Ladder, velocity);
        }

        /// <summary>Animals still in the boat's crate for <paramref name="grade"/> at <paramref name="totalSeconds"/>.</summary>
        public int AboardAt(double totalSeconds, int grade)
            => grade >= 0 && grade < _crateCount.Length && totalSeconds < _liftedAt[grade] ? _crateCount[grade] : 0;

        /// <summary>Animals standing at the counter; <paramref name="grade"/> -1 counts every crate. None once sold.</summary>
        public int AtCounterAt(double totalSeconds, int grade = -1)
        {
            if (!HasLanding || totalSeconds >= SoldAtSeconds) return 0;
            int count = 0;
            for (int g = 0; g < _crateCount.Length; g++)
                if ((grade < 0 || grade == g) && totalSeconds >= _downAt[g]) count += _crateCount[g];
            return count;
        }

        /// <summary>True once the buyer has taken the lot.</summary>
        public bool IsSoldAt(double totalSeconds) => HasLanding && totalSeconds >= SoldAtSeconds;

        private static float Seconds(Vector2 a, Vector2 b, float speed) => (b - a).magnitude / Mathf.Max(0.01f, speed);

        private void Walk(ref double t, ref Vector2 at, Vector2 to, float seconds, NpcLandingStep step, int crate, bool ashore,
                          bool ladder = false)
        {
            float heading = NpcPotDay.Bearing(at, to, _keys.Count > 0 ? _keys[_keys.Count - 1].Heading : 0f);
            if (seconds > 1e-6f)
                _keys.Add(new Key { T0 = t, T1 = t + seconds, A = at, B = to, Step = step, Crate = crate, Ashore = ashore, Ladder = ladder, Heading = heading });
            t += Mathf.Max(0f, seconds);
            at = to;
        }

        private void Dwell(ref double t, Vector2 at, float seconds, NpcLandingStep step, int crate, bool ashore)
        {
            float heading = _keys.Count > 0 ? _keys[_keys.Count - 1].Heading : 0f;
            if (seconds > 1e-6f)
                _keys.Add(new Key { T0 = t, T1 = t + seconds, A = at, B = at, Step = step, Crate = crate, Ashore = ashore, Heading = heading });
            t += Mathf.Max(0f, seconds);
        }
    }
}
