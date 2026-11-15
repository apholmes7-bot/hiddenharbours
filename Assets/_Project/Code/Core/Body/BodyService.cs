using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b><see cref="IBodyService"/> over the save, the clock and the config</b> (body slice 1). The
    /// <see cref="NeedsService"/> shape: every seam injectable so a test drives it with its own save, clock,
    /// numbers, needs and messes; each defaults to its <see cref="GameServices"/> accessor, read at each call.
    ///
    /// <para><b>Nothing ticks the body.</b> The save holds (mass, instant) and (stomach, instant); the
    /// values now are read off the clock on demand (<see cref="BodyMath"/>), so the walk's per-tick read,
    /// a meal and a save can never disagree, and a skipped night is one straight-line catch-up.</para>
    /// </summary>
    public sealed class BodyService : IBodyService
    {
        private readonly Func<ISaveService> _save;
        private readonly Func<IGameClock> _clock;
        private readonly Func<BodySettings> _settings;
        private readonly Func<NeedsSettings> _needsSettings;
        private readonly Func<INeedsService> _needs;
        private readonly Func<IMessRegistry> _messes;
        private readonly Func<double> _secondsPerGameHour;

        public BodyService(Func<ISaveService> save = null, Func<IGameClock> clock = null,
                           Func<BodySettings> settings = null, Func<NeedsSettings> needsSettings = null,
                           Func<INeedsService> needs = null, Func<IMessRegistry> messes = null,
                           Func<double> secondsPerGameHour = null)
        {
            _save = save ?? (() => GameServices.Save);
            _clock = clock ?? (() => GameServices.Clock);
            _settings = settings ?? (() => GameServices.BodySettings);
            _needsSettings = needsSettings ?? (() => GameServices.NeedsSettings);
            _needs = needs ?? (() => GameServices.Needs);
            _messes = messes ?? (() => GameServices.Messes);
            _secondsPerGameHour = secondsPerGameHour ?? (() => GameServices.SecondsPerDay / 24d);
        }

        public float Frame { get { BodySettings s = _settings(); return BodyMath.FrameOf(Body(), s); } }

        public float HeightScale { get { BodySettings s = _settings(); return BodyMath.HeightScaleOf(Body(), s); } }

        public bool ShapeChosen => Body()?.ShapeChosen ?? false;

        public double MassKg
        {
            get
            {
                BodySettings s = _settings();
                return MassNow(Body(), s);
            }
        }

        public double LeanMassKg { get { BodySettings s = _settings(); PlayerBodyDto b = Body(); return BodyMath.LeanMassKg(BodyMath.FrameOf(b, s), BodyMath.HeightScaleOf(b, s), s); } }

        public double MaxMassKg { get { BodySettings s = _settings(); PlayerBodyDto b = Body(); return BodyMath.MaxMassKg(BodyMath.FrameOf(b, s), BodyMath.HeightScaleOf(b, s), s); } }

        public double FloorMassKg { get { BodySettings s = _settings(); PlayerBodyDto b = Body(); return BodyMath.FloorMassKg(BodyMath.FrameOf(b, s), BodyMath.HeightScaleOf(b, s), s); } }

        public double DailyKcal
        {
            get
            {
                BodySettings s = _settings();
                PlayerBodyDto b = Body();
                return BodyMath.DailyKcal(MassNow(b, s), BodyMath.HeightScaleOf(b, s), s);
            }
        }

        public float HungerRateScale
        {
            get
            {
                PlayerBodyDto b = Body();
                if (b == null) return 1f;
                BodySettings s = _settings();
                return BodyMath.HungerRateScale(b, s);
            }
        }

        public float StomachMeals
        {
            get
            {
                BodySettings s = _settings();
                return BodyMath.StomachAt(Body(), Now(0d), _secondsPerGameHour(), s);
            }
        }

        public float PlayerMultiplier(TraitStat stat)
        {
            if (stat != TraitStat.WalkSpeed) return TraitMath.Identity;
            PlayerBodyDto b = Body();
            if (b == null) return TraitMath.Identity;
            BodySettings s = _settings();
            return BodyMath.WalkFactor(MassNow(b, s), BodyMath.FrameOf(b, s), BodyMath.HeightScaleOf(b, s), s);
        }

        public bool ChooseShape(float frame, float heightScale)
        {
            PlayerBodyDto b = Body();
            if (b == null || b.ShapeChosen) return false;
            BodySettings s = _settings();
            // Hunger so far rose at the old shape's rate: anchor it (and the body) now, before the rate moves.
            IGameClock clock = _clock();
            if (clock != null)
                BodyMath.Settle(b, _save()?.Current?.PlayerNeeds, clock.TotalSeconds, _needsSettings(), s,
                                _secondsPerGameHour());
            b.Frame = frame < 0f ? 0f : frame > 1f ? 1f : frame;
            b.HeightScale = BodyMath.ClampHeightScale(heightScale, s);
            b.ShapeChosen = true;
            b.MassKg = PlayerBodyDto.Unset;   // the new shape's lean mass
            double now = Now(-1d);
            b.MassSinceGameSeconds = now >= 0d ? now : PlayerBodyDto.Unset;
            return true;
        }

        public MealOutcome Eat(float meals, Vector2 position, string regionId)
        {
            if (!(meals > 0f)) return MealOutcome.Nothing;
            SaveData data = _save()?.Current;
            if (data == null) return MealOutcome.Nothing;
            PlayerBodyDto b = data.PlayerBody ??= new PlayerBodyDto();
            Settle();

            BodySettings s = _settings();
            double now = Now(0d);
            float held = BodyMath.StomachAt(b, now, _secondsPerGameHour(), s);
            if (BodyMath.Overfills(held, meals, s))
            {
                b.StomachMeals = 0f;
                b.StomachSinceGameSeconds = now;
                string messId = _messes()?.Add(regionId, position, now);
                EventBus.Publish(new Vomited(messId, regionId ?? "", position, now, meals));
                return MealOutcome.Vomited;
            }

            INeedsService needs = _needs();
            float hungerBefore = needs != null ? needs.Level(NeedKind.Hunger) : 0f;
            needs?.Eat(meals);

            double kcalPerMeal = BodyMath.KcalPerHungerUnit(_needsSettings().Hunger.RisePerGameHour, s);
            double gain = BodyMath.MealGainKg(meals, hungerBefore, kcalPerMeal, s);
            if (gain > 0d)
            {
                float frame = BodyMath.FrameOf(b, s), height = BodyMath.HeightScaleOf(b, s);
                double anchor = BodyMath.AnchorMassKg(b, s);
                double max = BodyMath.MaxMassKg(frame, height, s);
                // Never past the frame's max; a body already over it (a retuned max) is not trimmed by a meal.
                double mass = anchor >= max ? anchor : Math.Min(anchor + gain, max);
                if (mass != anchor) b.MassKg = mass;
            }
            b.MassSinceGameSeconds = now;

            b.StomachMeals = held + meals;
            b.StomachSinceGameSeconds = now;
            return MealOutcome.Eaten;
        }

        public void Settle()
        {
            SaveData data = _save()?.Current;
            if (data == null) return;
            IGameClock clock = _clock();
            if (clock == null) return;
            data.PlayerBody ??= new PlayerBodyDto();
            BodySettings s = _settings();
            float rise = _needsSettings().Hunger.RisePerGameHour * BodyMath.HungerRateScale(data.PlayerBody, s);
            BodyMath.SettleBody(data.PlayerBody, data.PlayerNeeds, clock.TotalSeconds, rise, s, _secondsPerGameHour());
        }

        /// <summary>The mass now: the anchor less any starving since, read off the saved hunger and the
        /// rise scaled at the anchor mass.</summary>
        private double MassNow(PlayerBodyDto b, in BodySettings s)
        {
            if (b == null) return BodyMath.AnchorMassKg(null, s);
            PlayerNeedsDto needs = _save()?.Current?.PlayerNeeds;
            IGameClock clock = _clock();
            if (needs == null || clock == null) return BodyMath.AnchorMassKg(b, s);
            float rise = _needsSettings().Hunger.RisePerGameHour * BodyMath.HungerRateScale(b, s);
            return BodyMath.MassAt(b, needs.Hunger, rise, clock.TotalSeconds, _secondsPerGameHour(), s);
        }

        private double Now(double fallback)
        {
            IGameClock clock = _clock();
            return clock != null ? clock.TotalSeconds : fallback;
        }

        private PlayerBodyDto Body() => _save()?.Current?.PlayerBody;
    }
}
