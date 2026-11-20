using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b><see cref="INeedsService"/> over the save, the clock and the config</b> (needs slice 1). The
    /// <see cref="TraitService"/> shape: every seam is injectable so a test drives it with its own save,
    /// clock, numbers and traits; each defaults to its <see cref="GameServices"/> accessor, read at each
    /// call (they self-install and can be swapped by a load).
    ///
    /// <para><b>Nothing ticks a level.</b> The save holds (level, instant) per need and the level now is
    /// read off the clock on demand, so the walk's per-tick read and the slow tick's stage check can never
    /// disagree, and a missed tick costs nothing but a late bubble.</para>
    /// </summary>
    public sealed class NeedsService : INeedsService
    {
        // Index = (int)NeedKind: the stage baseline arrays below are indexed by the enum value.
        private static readonly NeedKind[] Kinds = { NeedKind.Hunger, NeedKind.Tiredness, NeedKind.Dirtiness };

        private readonly Func<ISaveService> _save;
        private readonly Func<IGameClock> _clock;
        private readonly Func<NeedsSettings> _settings;
        private readonly Func<ITraitService> _traits;
        private readonly Func<double> _secondsPerGameHour;
        private readonly Func<IBodyService> _body;
        private readonly Func<ICleanlinessService> _cleanliness;
        private readonly Func<CleanlinessSettings> _cleanlinessSettings;
        private readonly Func<KitchenSettings> _kitchen;

        private readonly NeedStage[] _lastStage = new NeedStage[Kinds.Length];
        private readonly bool[] _stageKnown = new bool[Kinds.Length];

        public NeedsService(Func<ISaveService> save = null, Func<IGameClock> clock = null,
                            Func<NeedsSettings> settings = null, Func<ITraitService> traits = null,
                            Func<double> secondsPerGameHour = null, Func<IBodyService> body = null,
                            Func<ICleanlinessService> cleanliness = null,
                            Func<CleanlinessSettings> cleanlinessSettings = null,
                            Func<KitchenSettings> kitchen = null)
        {
            _cleanliness = cleanliness ?? (() => GameServices.Cleanliness);
            _cleanlinessSettings = cleanlinessSettings ?? (() => GameServices.CleanlinessSettings);
            _save = save ?? (() => GameServices.Save);
            _clock = clock ?? (() => GameServices.Clock);
            _settings = settings ?? (() => GameServices.NeedsSettings);
            _traits = traits ?? (() => GameServices.Traits);
            _secondsPerGameHour = secondsPerGameHour ?? (() => GameServices.SecondsPerDay / 24d);
            _body = body ?? (() => GameServices.Body);
            _kitchen = kitchen ?? (() => GameServices.KitchenSettings);
        }

        public float Level(NeedKind need)
        {
            PlayerNeedsDto needs = Needs();
            if (needs == null) return 0f;
            NeedsSettings settings = _settings();
            return LevelNow(need, needs, settings, out _);
        }

        public NeedStage Stage(NeedKind need)
        {
            PlayerNeedsDto needs = Needs();
            if (needs == null) return NeedStage.None;
            NeedsSettings settings = _settings();
            float level = LevelNow(need, needs, settings, out NeedCurve curve);
            return NeedsMath.StageOf(level, curve);
        }

        /// <summary>The level of <paramref name="need"/> now and its curve. Hunger and tiredness are read off
        /// their saved line, on the curve as this body feels it (<see cref="CurveFor"/>, body slice 1);
        /// dirtiness is the cleanliness service's (cleanliness slice 1).</summary>
        private float LevelNow(NeedKind need, PlayerNeedsDto needs, in NeedsSettings settings, out NeedCurve curve)
        {
            if (need == NeedKind.Dirtiness)
            {
                curve = _cleanlinessSettings().Curve;
                ICleanlinessService cleanliness = _cleanliness();
                return cleanliness != null ? cleanliness.Dirt : 0f;
            }
            curve = CurveFor(need, settings);
            return LevelOf(needs.Get(need), curve);
        }

        public float PlayerMultiplier(TraitStat stat)
        {
            if (stat != TraitStat.WalkSpeed && stat != TraitStat.SellPrice) return TraitMath.Identity;
            PlayerNeedsDto needs = Needs();
            if (needs == null) return TraitMath.Identity;
            NeedsSettings settings = _settings();

            float total = 0f;
            for (int k = 0; k < Kinds.Length; k++)
            {
                NeedKind need = Kinds[k];
                float level = LevelNow(need, needs, settings, out NeedCurve curve);
                NeedStage stage = NeedsMath.StageOf(level, curve);
                float penalty = NeedsMath.PenaltyFor(stat, stage, curve);
                if (penalty > 0f) total += penalty * ReliefFor(need);   // traits only read when it matters
            }
            return NeedsMath.Factor(total, NeedsMath.FloorFor(stat, settings));
        }

        public void Eat(float amount)
        {
            if (!(amount > 0f)) return;
            PlayerNeedsDto needs = Needs();
            if (needs == null) return;
            // Body slice 1: carry the body to now first, so a meal never erases time spent starving.
            _body()?.Settle();
            NeedStage before = Stage(NeedKind.Hunger);
            NeedStateDto state = needs.Hunger;
            float now = Level(NeedKind.Hunger) - amount;
            needs.Hunger = new NeedStateDto(now > 0f ? now : 0f, NowOr(state.SinceGameSeconds));
            Announce(NeedKind.Hunger, before);
        }

        public float Spend(float amount)
        {
            if (!(amount > 0f)) return 0f;
            PlayerNeedsDto needs = Needs();
            if (needs == null) return 0f;
            // Carry the body to now first, as a meal does: time already spent starving stays spent.
            _body()?.Settle();
            NeedStage before = Stage(NeedKind.Hunger);
            NeedStateDto state = needs.Hunger;
            float level = Level(NeedKind.Hunger) + amount;
            float over = level > 1f ? level - 1f : 0f;
            needs.Hunger = new NeedStateDto(level < 1f ? level : 1f, NowOr(state.SinceGameSeconds));
            Announce(NeedKind.Hunger, before);
            return over;
        }

        public void Exert(float amount)
        {
            if (!(amount > 0f) || float.IsInfinity(amount)) return;
            PlayerNeedsDto needs = Needs();
            if (needs == null) return;
            NeedStage before = Stage(NeedKind.Tiredness);
            double level = (double)Level(NeedKind.Tiredness) + amount;
            needs.Tiredness = new NeedStateDto((float)Math.Min(1d, level), NowOr(needs.Tiredness.SinceGameSeconds));
            Announce(NeedKind.Tiredness, before);
        }

        public void RestSeated(double seatedSinceGameSeconds)
        {
            PlayerNeedsDto needs = Needs();
            IGameClock clock = _clock();
            if (needs == null || clock == null) return;
            double now = clock.TotalSeconds;
            NeedStateDto state = needs.Tiredness;
            double from = seatedSinceGameSeconds;
            if (state.IsStamped && state.SinceGameSeconds > from) from = state.SinceGameSeconds;
            if (!(now > from)) return;

            NeedStage before = Stage(NeedKind.Tiredness);
            NeedsSettings settings = _settings();
            NeedCurve curve = CurveFor(NeedKind.Tiredness, settings);
            double perHour = _secondsPerGameHour();
            if (!(perHour > 0d)) return;
            float atSit = NeedsMath.LevelAt(state, curve, from, perHour);
            // Held under the chosen-duration sleep's rate: an hour seated never beats an hour asleep.
            float after = SeatedRest.LevelAfter(atSit, (now - from) / perHour, curve.RisePerGameHour, _kitchen(),
                                                settings.SleepReliefPerGameHour);
            needs.Tiredness = new NeedStateDto(after, now);
            Announce(NeedKind.Tiredness, before);
        }

        public void Sleep()
        {
            PlayerNeedsDto needs = Needs();
            if (needs == null) return;
            NeedStage before = Stage(NeedKind.Tiredness);
            needs.Tiredness = new NeedStateDto(0f, NowOr(needs.Tiredness.SinceGameSeconds));
            Announce(NeedKind.Tiredness, before);
        }

        public void SleepSince(double bedtimeSeconds)
        {
            PlayerNeedsDto needs = Needs();
            if (needs == null) return;
            IGameClock clock = _clock();
            if (clock == null) return;
            double now = clock.TotalSeconds;
            if (!(now > bedtimeSeconds)) return;   // no time slept (and NaN), no relief

            NeedsSettings settings = _settings();
            NeedStage before = Stage(NeedKind.Tiredness);
            float relief = settings.SleepReliefPerGameHour;
            float after = 0f;
            if (relief > 0f)
            {
                // Read at BEDTIME: tiredness does not rise while you sleep, it falls.
                NeedCurve curve = NeedsMath.CurveOf(NeedKind.Tiredness, settings);
                double perHour = _secondsPerGameHour();
                float atBedtime = NeedsMath.LevelAt(needs.Tiredness, curve, bedtimeSeconds, perHour);
                double hoursSlept = perHour > 0d ? (now - bedtimeSeconds) / perHour : 0d;
                double left = atBedtime - hoursSlept * relief;
                after = left > 0d ? (float)left : 0f;
            }
            // relief <= 0 is the pre-ruling rule (any sleep rests you fully), so an unwired number never
            // turns sleeping into doing nothing.
            needs.Tiredness = new NeedStateDto(after, now);
            Announce(NeedKind.Tiredness, before);
        }

        public void Tick()
        {
            PlayerNeedsDto needs = Needs();
            if (needs == null) return;

            IGameClock clock = _clock();
            if (clock != null)
            {
                double now = clock.TotalSeconds;
                // A fresh game starts rising from the first instant the clock is seen, holding its level.
                if (!needs.Hunger.IsStamped) needs.Hunger = new NeedStateDto(needs.Hunger.Level, now);
                if (!needs.Tiredness.IsStamped) needs.Tiredness = new NeedStateDto(needs.Tiredness.Level, now);
            }
            // Cleanliness slice 1: anchor a fresh game's dirt and close a stretch past its guard window.
            _cleanliness()?.Tick();

            for (int k = 0; k < Kinds.Length; k++)
            {
                NeedStage stage = Stage(Kinds[k]);
                if (_stageKnown[k] && stage != _lastStage[k])
                    EventBus.Publish(new NeedStageChanged(Kinds[k], _lastStage[k], stage));
                _lastStage[k] = stage;
                _stageKnown[k] = true;
            }
        }

        private void Announce(NeedKind need, NeedStage before)
        {
            int k = (int)need;
            NeedStage after = Stage(need);
            if (after != before) EventBus.Publish(new NeedStageChanged(need, before, after));
            _lastStage[k] = after;
            _stageKnown[k] = true;
        }

        /// <summary>The product of the player's trait reliefs on <paramref name="need"/>: 1 with none.
        /// Known, distinct traits only, at most the allowed picks (the <see cref="TraitService"/> rule).
        /// Allocation-free.</summary>
        private float ReliefFor(NeedKind need)
        {
            ITraitService traits = _traits();
            if (traits == null) return TraitMath.Identity;
            IReadOnlyList<string> ids = traits.PlayerTraitIds;
            if (ids == null || ids.Count == 0) return TraitMath.Identity;

            float scale = TraitMath.Identity;
            int picks = GameServices.TraitPicksPerCharacter;
            int counted = 0;
            for (int i = 0; i < ids.Count && counted < picks; i++)
            {
                if (Repeats(ids, i)) continue;
                TraitDef def = traits.Find(ids[i]);
                if (def == null) continue;
                counted++;
                NeedRelief[] reliefs = def.NeedReliefs;
                if (reliefs == null) continue;
                for (int r = 0; r < reliefs.Length; r++)
                    if (reliefs[r].Need == need) scale *= NeedsMath.ReliefScale(reliefs[r].PenaltyScale);
            }
            return scale;
        }

        private static bool Repeats(IReadOnlyList<string> ids, int i)
        {
            for (int j = 0; j < i; j++)
                if (string.Equals(ids[i], ids[j], StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>The curve for <paramref name="need"/> as this body feels it: hunger rises by the body's
        /// <see cref="IBodyService.HungerRateScale"/> (body slice 1: a bigger body empties a meal sooner).
        /// At a scale of exactly 1 the configured curve is returned untouched.</summary>
        private NeedCurve CurveFor(NeedKind need, in NeedsSettings settings)
        {
            NeedCurve curve = NeedsMath.CurveOf(need, settings);
            if (need != NeedKind.Hunger) return curve;
            float scale = _body()?.HungerRateScale ?? TraitMath.Identity;
            if (scale != TraitMath.Identity && scale > 0f) curve.RisePerGameHour *= scale;
            return curve;
        }

        private float LevelOf(in NeedStateDto state, in NeedCurve curve)
        {
            IGameClock clock = _clock();
            double now = clock != null ? clock.TotalSeconds : state.SinceGameSeconds;
            return NeedsMath.LevelAt(state, curve, now, _secondsPerGameHour());
        }

        /// <summary>The clock now, or <paramref name="fallback"/> with no clock (the instant stays as it was).</summary>
        private double NowOr(double fallback)
        {
            IGameClock clock = _clock();
            return clock != null ? clock.TotalSeconds : fallback;
        }

        private PlayerNeedsDto Needs()
        {
            SaveData data = _save()?.Current;
            if (data == null) return null;
            return data.PlayerNeeds ??= new PlayerNeedsDto();
        }
    }
}
