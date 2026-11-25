using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>A seeded starting tie, as the graph takes it (built from a <see cref="RelationshipTieDef"/>).</summary>
    public readonly struct RelationshipSeed
    {
        public readonly string A;
        public readonly string B;
        public readonly int Strength;

        public RelationshipSeed(string a, string b, int strength)
        {
            A = a;
            B = b;
            Strength = strength;
        }
    }

    /// <summary>One pair's strength, ids in ordinal order (<see cref="A"/> &lt; <see cref="B"/>).</summary>
    public readonly struct RelationshipPairState
    {
        public readonly string A;
        public readonly string B;
        public readonly int Strength;

        public RelationshipPairState(string a, string b, int strength)
        {
            A = a;
            B = b;
            Strength = strength;
        }
    }

    /// <summary>A pair crossed from one stage to another at a day's apply. Ids in ordinal order.</summary>
    public readonly struct RelationshipStageChanged
    {
        public readonly string A;
        public readonly string B;
        public readonly RelationshipStage From;
        public readonly RelationshipStage To;

        public RelationshipStageChanged(string a, string b, RelationshipStage from, RelationshipStage to)
        {
            A = a;
            B = b;
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// <b>The web of ties between people, as plain arithmetic</b> (relationships slice 1, owner ruling
    /// 2026-11-14). No Unity, no clock, no dice: a pair's strength is its seed plus what the rule table made
    /// of the events recorded against it, so the same seeds and the same events give the same graph on
    /// every machine (rule 5).
    ///
    /// <para><b>Symmetric.</b> A pair is stored once under its ids in ordinal order; (a, b) and (b, a) are
    /// the same pair. A pair never seeded and never moved reads 0, a Stranger. The graph keeps no roster:
    /// any non-empty id is a person, so an unknown id is just a stranger to everyone, never a crash.</para>
    ///
    /// <para><b>Recorded, then applied once a day.</b> <see cref="Record"/> only queues; <see cref="ApplyDay"/>
    /// sums the day's units per (pair, kind), prices each sum through its <see cref="RelationshipRule"/>,
    /// caps it, and moves each pair once, kinds in ascending value. The result does not depend on the order
    /// events were recorded within a day. The day apply returns each pair that changed stage, exactly once
    /// per crossing, in ordinal pair order; the caller (the service) announces them.</para>
    ///
    /// <para><b>Never shown.</b> <see cref="Strength"/> exists for code and tests. The owner's ruling: no
    /// bar, meter, number or hearts is ever shown; stages are noticed through talk and opportunities.</para>
    /// </summary>
    public sealed class RelationshipGraph
    {
        /// <summary>The id the player stands under in the web (one shared village and business, so in co-op
        /// either player's custom counts here). Merchants' custom (<see cref="RelationshipEventKind.CustomGiven"/>)
        /// is a tie with this id.</summary>
        public const string PlayerId = "player";

        private readonly RelationshipStageThresholds _thresholds;
        private readonly Dictionary<RelationshipEventKind, RelationshipRule> _rules =
            new Dictionary<RelationshipEventKind, RelationshipRule>();
        private readonly Dictionary<PairKey, int> _strengths = new Dictionary<PairKey, int>(PairKey.Comparer);
        private readonly List<RelationshipEvent> _pending = new List<RelationshipEvent>();

        /// <param name="thresholds">Stage edges; an invalid set falls back to
        /// <see cref="RelationshipStageThresholds.Default"/> so stages always read sensibly.</param>
        /// <param name="rules">The rule table. The first valid row per kind wins; a kind with no row is
        /// recorded but moves nothing.</param>
        /// <param name="seeds">Starting ties. Clamped to the bounds; a bad id or a repeated pair after the
        /// first is skipped.</param>
        public RelationshipGraph(RelationshipStageThresholds thresholds, IEnumerable<RelationshipRule> rules,
            IEnumerable<RelationshipSeed> seeds = null)
        {
            _thresholds = thresholds.IsValid ? thresholds : RelationshipStageThresholds.Default;
            if (rules != null)
                foreach (RelationshipRule rule in rules)
                    if (rule.IsValid && !_rules.ContainsKey(rule.Kind)) _rules.Add(rule.Kind, rule);
            if (seeds != null)
                foreach (RelationshipSeed seed in seeds)
                {
                    if (!PairKey.TryMake(seed.A, seed.B, out PairKey key) || _strengths.ContainsKey(key)) continue;
                    _strengths.Add(key, _thresholds.Clamp(seed.Strength));
                }
        }

        /// <summary>The stage edges in use.</summary>
        public RelationshipStageThresholds Thresholds => _thresholds;

        /// <summary>Events recorded since the last <see cref="ApplyDay"/>.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>The hidden strength of the pair; 0 for an unknown or invalid pair. Never shown.</summary>
        public int Strength(string a, string b)
            => PairKey.TryMake(a, b, out PairKey key) && _strengths.TryGetValue(key, out int s) ? s : 0;

        /// <summary>The stage of the pair; <see cref="RelationshipStage.Stranger"/> for an unknown pair.</summary>
        public RelationshipStage Stage(string a, string b) => _thresholds.StageOf(Strength(a, b));

        /// <summary>Queue <paramref name="e"/> for the next <see cref="ApplyDay"/>. False (and ignored) for a
        /// null, empty or repeated id or a non-positive unit count.</summary>
        public bool Record(in RelationshipEvent e)
        {
            if (e.Units <= 0 || !PairKey.TryMake(e.A, e.B, out _)) return false;
            _pending.Add(e);
            return true;
        }

        /// <summary>
        /// Apply every queued event, then clear the queue. Returns the pairs that changed stage (empty when
        /// none), in ordinal pair order. Allocates; it runs once a day on the slow tick, never per frame.
        /// </summary>
        public List<RelationshipStageChanged> ApplyDay()
        {
            var changes = new List<RelationshipStageChanged>();
            if (_pending.Count == 0) return changes;

            // Sum units per (pair, kind) so the order of recording within a day cannot matter.
            var units = new Dictionary<PairKey, long[]>(PairKey.Comparer);
            int kindSlots = KindSlots;
            foreach (RelationshipEvent e in _pending)
            {
                PairKey.TryMake(e.A, e.B, out PairKey key);
                int slot = (int)e.Kind;
                if (slot < 0 || slot >= kindSlots) continue;
                if (!units.TryGetValue(key, out long[] perKind)) units.Add(key, perKind = new long[kindSlots]);
                perKind[slot] += e.Units;
            }
            _pending.Clear();

            var pairs = new List<PairKey>(units.Keys);
            pairs.Sort(PairKey.Comparer);
            foreach (PairKey key in pairs)
            {
                long[] perKind = units[key];
                int before = _strengths.TryGetValue(key, out int s) ? s : 0;
                int after = before;
                for (int slot = 0; slot < kindSlots; slot++)
                {
                    if (perKind[slot] == 0 || !_rules.TryGetValue((RelationshipEventKind)slot, out RelationshipRule rule))
                        continue;
                    after = Step(after, perKind[slot], rule);
                }
                if (after == before) continue;
                _strengths[key] = after;
                RelationshipStage from = _thresholds.StageOf(before);
                RelationshipStage to = _thresholds.StageOf(after);
                if (from != to) changes.Add(new RelationshipStageChanged(key.A, key.B, from, to));
            }
            return changes;
        }

        // ---- PERSISTENCE SEAM (relationships slice 2) ------------------------------------------------
        // Strengths are shaped by the player's actions, so unlike tide and weather they must be SAVED.
        // Slice 1 keeps them in memory only (the next save version is taken by another change). Slice 2
        // adds a list of RelationshipPairState to SaveData with a migration, writes Snapshot() on save and
        // calls Restore() on load. Pending events are not part of the seam: apply the day before saving.

        /// <summary>Every stored pair, in ordinal pair order. For the save (slice 2) and tests.</summary>
        public List<RelationshipPairState> Snapshot()
        {
            var keys = new List<PairKey>(_strengths.Keys);
            keys.Sort(PairKey.Comparer);
            var list = new List<RelationshipPairState>(keys.Count);
            foreach (PairKey key in keys) list.Add(new RelationshipPairState(key.A, key.B, _strengths[key]));
            return list;
        }

        /// <summary>Replace every strength with <paramref name="pairs"/> (a loaded save, slice 2). Invalid
        /// pairs are skipped, values clamped, a repeated pair keeps its last value. Clears the queue.</summary>
        public void Restore(IEnumerable<RelationshipPairState> pairs)
        {
            _strengths.Clear();
            _pending.Clear();
            if (pairs == null) return;
            foreach (RelationshipPairState p in pairs)
                if (PairKey.TryMake(p.A, p.B, out PairKey key)) _strengths[key] = _thresholds.Clamp(p.Strength);
        }

        private int Step(int strength, long totalUnits, RelationshipRule rule)
        {
            long raw = totalUnits * rule.PointsPerUnit;
            long delta = Math.Max(-rule.CapPerPairPerDay, Math.Min(rule.CapPerPairPerDay, raw));
            long next = strength + delta;
            if (delta < 0)
            {
                // A rule that may not make rivals stops just above the rival edge, and never lifts a pair
                // that is already colder than that.
                int floor = rule.CanMakeRival ? _thresholds.MinStrength : _thresholds.RivalAtOrBelow + 1;
                next = Math.Max(next, Math.Min(strength, floor));
            }
            return _thresholds.Clamp((int)Math.Max(int.MinValue, Math.Min(int.MaxValue, next)));
        }

        private static readonly int KindSlots = MaxKind() + 1;

        private static int MaxKind()
        {
            int max = 0;
            foreach (RelationshipEventKind k in Enum.GetValues(typeof(RelationshipEventKind)))
                max = Math.Max(max, (int)k);
            return max;
        }

        private readonly struct PairKey
        {
            public readonly string A;
            public readonly string B;

            private PairKey(string a, string b)
            {
                A = a;
                B = b;
            }

            public static bool TryMake(string x, string y, out PairKey key)
            {
                key = default;
                if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y)) return false;
                int order = string.CompareOrdinal(x, y);
                if (order == 0) return false;
                key = order < 0 ? new PairKey(x, y) : new PairKey(y, x);
                return true;
            }

            public static readonly OrdinalComparer Comparer = new OrdinalComparer();

            public sealed class OrdinalComparer : IEqualityComparer<PairKey>, IComparer<PairKey>
            {
                public bool Equals(PairKey p, PairKey q)
                    => string.Equals(p.A, q.A, StringComparison.Ordinal) && string.Equals(p.B, q.B, StringComparison.Ordinal);

                public int GetHashCode(PairKey p)
                {
                    unchecked
                    {
                        return (StringComparer.Ordinal.GetHashCode(p.A) * 397) ^ StringComparer.Ordinal.GetHashCode(p.B);
                    }
                }

                public int Compare(PairKey p, PairKey q)
                {
                    int c = string.CompareOrdinal(p.A, q.A);
                    return c != 0 ? c : string.CompareOrdinal(p.B, q.B);
                }
            }
        }
    }
}
