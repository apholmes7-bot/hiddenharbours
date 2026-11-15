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
        private readonly Func<ICleanlinessService> _cleanliness;
        private readonly Func<CleanlinessSettings> _cleanlinessSettings;

        private readonly NeedStage[] _lastStage = new NeedStage[Kinds.Length];
        private readonly bool[] _stageKnown = new bool[Kinds.Length];

        public NeedsService(Func<ISaveService> save = null, Func<IGameClock> clock = null,
                            Func<NeedsSettings> settings = null, Func<ITraitService> traits = null,
                            Func<double> secondsPerGameHour = null,
                            Func<ICleanlinessService> cleanliness = null,
                            Func<CleanlinessSettings> cleanlinessSettings = null)
        {
            _cleanliness = cleanliness ?? (() => GameServices.Cleanliness);
            _cleanlinessSettings = cleanlinessSettings ?? (() => GameServices.CleanlinessSettings);
            _save = save ?? (() => GameServices.Save);
            _clock = clock ?? (() => GameServices.Clock);
            _settings = settings ?? (() => GameServices.NeedsSettings);
            _traits = traits ?? (() => GameServices.Traits);
            _secondsPerGameHour = secondsPerGameHour ?? (() => GameServices.SecondsPerDay / 24d);
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
        /// their saved line; dirtiness is the cleanliness service's (cleanliness slice 1).</summary>
        private float LevelNow(NeedKind need, PlayerNeedsDto needs, in NeedsSettings settings, out NeedCurve curve)
        {
            if (need == NeedKind.Dirtiness)
            {
                curve = _cleanlinessSettings().Curve;
                ICleanlinessService cleanliness = _cleanliness();
                return cleanliness != null ? cleanliness.Dirt : 0f;
            }
            curve = NeedsMath.CurveOf(need, settings);
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
            NeedStage before = Stage(NeedKind.Hunger);
            NeedStateDto state = needs.Hunger;
            float now = Level(NeedKind.Hunger) - amount;
            needs.Hunger = new NeedStateDto(now > 0f ? now : 0f, NowOr(state.SinceGameSeconds));
            Announce(NeedKind.Hunger, before);
        }

        public void Sleep()
        {
            PlayerNeedsDto needs = Needs();
            if (needs == null) return;
            NeedStage before = Stage(NeedKind.Tiredness);
            needs.Tiredness = new NeedStateDto(0f, NowOr(needs.Tiredness.SinceGameSeconds));
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
