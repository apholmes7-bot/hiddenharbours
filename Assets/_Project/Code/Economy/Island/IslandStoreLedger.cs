using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>What one day did to the island's back room, summed over every good.</summary>
    public readonly struct IslandStoreDay
    {
        /// <summary>The game day (<c>IGameClock.DayIndex</c>).</summary>
        public readonly int Day;
        /// <summary>True when the supply run came over the bar this day.</summary>
        public readonly bool RunToday;
        /// <summary>What the run brought, kg and litres as carried.</summary>
        public readonly MassVolume Delivered;
        /// <summary>The island's use the back room could not meet, kg and litres.</summary>
        public readonly MassVolume Shortfall;

        public IslandStoreDay(int day, bool runToday, MassVolume delivered, MassVolume shortfall)
        {
            Day = day;
            RunToday = runToday;
            Delivered = delivered;
            Shortfall = shortfall;
        }
    }

    /// <summary>
    /// <b>The island's back room, recomputed for any day</b> (docs/design/st-peters-living-economy.md §4).
    /// Each morning the supply run (when <see cref="IslandRunCalendar"/> says it goes) tops every good up toward
    /// its buffer target, all goods scaled down together when the runner's kit cannot carry the whole want;
    /// then the island uses its day. A day short of a good records the unmet use and the shelf stands empty.
    ///
    /// <para><b>No state is saved.</b> A day is answered by replaying from a full back room
    /// <see cref="LookbackDays"/> before it. A run that tops the room right up erases the start, so the answer
    /// is the same for any long-enough lookback: the island looks the same whether the player watched the
    /// days or slept through them, and a return after any absence costs the same few hundred additions.</para>
    ///
    /// <para>Units are each good's own (<see cref="IslandGoodDef.Unit"/>); mass and volume per unit come from
    /// its daily figures. The target includes the player's allowance, the island's use does not: the allowance
    /// is what stands on the shelf for the player. Player purchases come in through <see cref="ExtraDraw"/>, held
    /// for the session only (saving them is slice 8).
    /// Plain C#, no Unity lifecycle; drive it from the day tick or read it lazily.</para>
    /// </summary>
    public sealed class IslandStoreLedger
    {
        private readonly IslandGoodDef[] _goods;
        private readonly double[] _kgPerUnit;
        private readonly double[] _litresPerUnit;
        private readonly MassVolume _runCapacity;
        private readonly int _cadenceDays;
        private readonly double _bufferDays;
        private readonly Func<int, bool> _runPossible;
        private readonly Func<int, bool> _winter;

        // Per-good results of the last Settle, reused (no allocation per settle).
        private readonly double[] _level;
        private readonly double[] _dawn;
        private readonly double[] _delivered;
        private readonly double[] _shortfall;

        /// <param name="goods">The back room's goods; only <see cref="GoodSource.MainlandOverBar"/> goods with
        /// a positive daily use are stocked.</param>
        /// <param name="runCapacity">What one run carries at most (the runner's kit).</param>
        /// <param name="cadenceDays">A run is scheduled every this many days.</param>
        /// <param name="bufferDays">The target, in days of use (with the player's allowance).</param>
        /// <param name="lookbackDays">How many days before the asked day the replay starts full.</param>
        /// <param name="runPossible">Day to "the runner can go and come back over the bar this day".</param>
        /// <param name="winter">Day to "winter" (applies each good's winter multiplier); null for never.</param>
        public IslandStoreLedger(IEnumerable<IslandGoodDef> goods, MassVolume runCapacity, int cadenceDays,
                                 double bufferDays, int lookbackDays, Func<int, bool> runPossible,
                                 Func<int, bool> winter = null)
        {
            var kept = new List<IslandGoodDef>();
            if (goods != null)
                foreach (IslandGoodDef g in goods)
                    if (g != null && g.Source == GoodSource.MainlandOverBar && g.DailyUnits > 0f) kept.Add(g);
            _goods = kept.ToArray();
            int n = _goods.Length;
            _kgPerUnit = new double[n];
            _litresPerUnit = new double[n];
            for (int i = 0; i < n; i++)
            {
                _kgPerUnit[i] = _goods[i].DailyMassKg / (double)_goods[i].DailyUnits;
                _litresPerUnit[i] = _goods[i].DailyVolumeLitres / (double)_goods[i].DailyUnits;
            }
            _runCapacity = runCapacity;
            _cadenceDays = cadenceDays;
            _bufferDays = Math.Max(0.0, bufferDays);
            LookbackDays = Math.Max(1, lookbackDays);
            _runPossible = runPossible ?? (_ => false);
            _winter = winter;
            _level = new double[n];
            _dawn = new double[n];
            _delivered = new double[n];
            _shortfall = new double[n];
        }

        /// <summary>Builds the ledger an <see cref="IslandLedgerDef"/> describes: its goods, its runner's
        /// cadence and kit (through <paramref name="carriers"/>), and the route's buffer.</summary>
        public static IslandStoreLedger FromDef(IslandLedgerDef def, IslandSupplyConfig route,
                                                IReadOnlyDictionary<string, CarrierDef> carriers,
                                                Func<int, bool> runPossible, Func<int, bool> winter = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (route == null) throw new ArgumentNullException(nameof(route));
            MassVolume kit = SupplyPlanMath.KitCapacity(def.Runner, carriers, out _);
            int cadence = def.Runner != null ? def.Runner.CadenceDays : 0;
            return new IslandStoreLedger(def.Goods, kit, cadence, route.BufferDays, def.LookbackDays,
                                         runPossible, winter);
        }

        /// <summary>How many days before the asked day the replay starts from a full back room.</summary>
        public int LookbackDays { get; }

        /// <summary>
        /// Units of a good taken from the back room on a day on top of the island's own use, as
        /// <c>(goodIndex, day) =&gt; units</c>: the player's purchases off the shelf (slice 2, held for the
        /// session only). Applied after the island's use, never below empty. Null means nothing extra, which
        /// is the island as the calendar alone says. Draws older than <see cref="LookbackDays"/> fall out of
        /// the replay: the run tops the room back up, so the deviation clears itself.
        /// </summary>
        public Func<int, int, double> ExtraDraw { get; set; }

        /// <summary>The stocked goods, in order; the index used by every per-good read.</summary>
        public int GoodCount => _goods.Length;

        public IslandGoodDef Good(int index) => _goods[index];

        /// <summary>The buffer target of a good on a day, in its units (with the player's allowance).</summary>
        public double Target(int index, int day) => _bufferDays * DailyUse(index, day) * (1.0 + _goods[index].PlayerAllowanceFraction);

        /// <summary>The island's own use of a good on a day, in its units (winter applied).</summary>
        public double DailyUse(int index, int day)
        {
            IslandGoodDef g = _goods[index];
            double k = _winter != null && _winter(day) ? g.WinterMultiplier : 1.0;
            return g.DailyUnits * k;
        }

        /// <summary>The level at dawn of the last settled day, before the run.</summary>
        public double LevelAtDawn(int index) => _dawn[index];

        /// <summary>The level at the end of the last settled day, after the run and the island's use.</summary>
        public double LevelAtDusk(int index) => _level[index];

        /// <summary>What the run brought of a good on the last settled day.</summary>
        public double Delivered(int index) => _delivered[index];

        /// <summary>The island's use of a good the back room could not meet on the last settled day.</summary>
        public double Shortfall(int index) => _shortfall[index];

        /// <summary>True when the run comes over the bar on <paramref name="day"/>.</summary>
        public bool RunsOn(int day) => IslandRunCalendar.RunsOn(day, _cadenceDays, _runPossible);

        /// <summary>
        /// Recomputes <paramref name="day"/>: replays from a full back room <see cref="LookbackDays"/> earlier
        /// (or from day 0) and leaves that day's per-good figures readable. A pure function of the day and the
        /// inputs: calling it for any days in any order gives the same answers.
        /// </summary>
        public IslandStoreDay Settle(int day)
        {
            if (day < 0) day = 0;
            int start = Math.Max(0, day - LookbackDays);
            int n = _goods.Length;
            for (int i = 0; i < n; i++) _level[i] = Target(i, start);

            bool run = false;
            for (int t = start; t <= day; t++) run = Step(t);

            double dKg = 0.0, dL = 0.0, sKg = 0.0, sL = 0.0;
            for (int i = 0; i < n; i++)
            {
                dKg += _delivered[i] * _kgPerUnit[i];
                dL += _delivered[i] * _litresPerUnit[i];
                sKg += _shortfall[i] * _kgPerUnit[i];
                sL += _shortfall[i] * _litresPerUnit[i];
            }
            return new IslandStoreDay(day, run, new MassVolume(dKg, dL), new MassVolume(sKg, sL));
        }

        /// <summary>One day: the morning run, then the island's use. Returns whether the run went.</summary>
        private bool Step(int day)
        {
            int n = _goods.Length;
            bool run = RunsOn(day);
            double wantKg = 0.0, wantL = 0.0;
            for (int i = 0; i < n; i++)
            {
                _dawn[i] = _level[i];
                _delivered[i] = 0.0;
                if (!run) continue;
                double want = Math.Max(0.0, Target(i, day) - _level[i]);
                _delivered[i] = want; // the whole want for now; scaled below if the kit cannot carry it
                wantKg += want * _kgPerUnit[i];
                wantL += want * _litresPerUnit[i];
            }

            if (run)
            {
                double scale = 1.0;
                if (wantKg > _runCapacity.MassKg) scale = Math.Min(scale, _runCapacity.MassKg / wantKg);
                if (wantL > _runCapacity.VolumeLitres) scale = Math.Min(scale, _runCapacity.VolumeLitres / wantL);
                if (scale < 0.0) scale = 0.0;
                for (int i = 0; i < n; i++)
                {
                    if (_delivered[i] <= 0.0) continue;
                    if (scale >= 1.0)
                    {
                        // A full top-up lands exactly on the target, so two replays that both reach it agree
                        // to the bit from here on.
                        _level[i] = Target(i, day);
                    }
                    else
                    {
                        _delivered[i] *= scale;
                        _level[i] += _delivered[i];
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                double use = DailyUse(i, day);
                if (_level[i] >= use)
                {
                    _level[i] -= use;
                    _shortfall[i] = 0.0;
                }
                else
                {
                    _shortfall[i] = use - _level[i];
                    _level[i] = 0.0;
                }
            }

            Func<int, int, double> extra = ExtraDraw;
            if (extra != null)
                for (int i = 0; i < n; i++)
                {
                    double taken = extra(i, day);
                    if (taken > 0.0) _level[i] = Math.Max(0.0, _level[i] - taken);
                }
            return run;
        }
    }
}
