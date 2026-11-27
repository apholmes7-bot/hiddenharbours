using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    public enum WharfFishStrength { None, Light, Steady, Strong }

    /// <summary>Explicit numbers for this angler's fitted rod; never reads a player singleton.</summary>
    public readonly struct WharfVisitRod
    {
        public readonly string Id;
        public readonly float ReelSpeedMultiplier;
        public readonly float FightHelp01;
        public WharfVisitRod(string id, float reelSpeedMultiplier, float fightHelp01)
        {
            if (string.IsNullOrWhiteSpace(id) || float.IsNaN(reelSpeedMultiplier)
                || float.IsInfinity(reelSpeedMultiplier) || reelSpeedMultiplier <= 0f
                || float.IsNaN(fightHelp01) || fightHelp01 < 0f || fightHelp01 > 1f)
                throw new ArgumentException("A rod needs an id, positive reel speed and bounded fight help.");
            Id = id; ReelSpeedMultiplier = reelSpeedMultiplier; FightHelp01 = fightHelp01;
        }
    }

    /// <summary>A recomputable cast snapshot. A blank has no species, weight, fight or landing.</summary>
    public readonly struct WharfVisitCast
    {
        public readonly double CastAt;
        public readonly float WaitSeconds;
        public readonly string SpeciesId;
        public readonly float WeightKg;
        public readonly float FightSeconds;
        public readonly WharfFishStrength Strength;
        public readonly bool Landed;
        public bool HasBite => SpeciesId != null;
        public double CheckedAt => CastAt + WaitSeconds;
        public double FinishedAt => CheckedAt + FightSeconds;

        public WharfVisitCast(double castAt, float waitSeconds, string speciesId = null,
            float weightKg = 0f, float fightSeconds = 0f,
            WharfFishStrength strength = WharfFishStrength.None, bool landed = false)
        {
            CastAt = castAt; WaitSeconds = waitSeconds; SpeciesId = speciesId; WeightKg = weightKg;
            FightSeconds = fightSeconds; Strength = strength; Landed = landed;
        }
    }

    /// <summary>
    /// Pure visit calculation over fixed content and time-addressed world reads. The pool must be the
    /// player's serialized region pool, in its authored order. World and tide delegates must evaluate
    /// the supplied time and seed, never current services. School buffers are scratch only; create one
    /// calculator per concurrent caller. No outcomes, random stream or visit counters are retained.
    /// </summary>
    public sealed class WharfVisitCatches
    {
        private readonly WharfVisitDef _visit;
        private readonly IReadOnlyList<FishSpeciesDef> _pool;
        private readonly IFishSchoolWorld _world;
        private readonly Func<double, float> _tideAt;
        private readonly string _spotId;
        private readonly Vector2 _hookPosition;
        private readonly DepthDropSettings _depth;
        private readonly BaitTackleSettings _bait;
        private readonly SeaFishingSettings _sea;
        private readonly FishSchoolSettings _schoolSettings;
        private readonly FishSchoolModel _schools;
        private readonly List<FishSchool> _schoolScratch;
        private readonly List<string> _speciesScratch;

        public WharfVisitCatches(WharfVisitDef visit, IReadOnlyList<FishSpeciesDef> playerPool,
            IFishSchoolWorld world, Func<double, float> tideAt, string spotId, Vector2 hookPosition,
            in DepthDropSettings depth, in BaitTackleSettings bait, in SeaFishingSettings sea,
            in FishSchoolSettings schools, double secondsPerHour)
        {
            if (visit == null || playerPool == null || world == null || tideAt == null)
                throw new ArgumentNullException("Visit content and time-addressed water are required.");
            visit.Validate();
            if (string.IsNullOrWhiteSpace(spotId) || string.IsNullOrWhiteSpace(world.RegionId)
                || double.IsNaN(secondsPerHour) || double.IsInfinity(secondsPerHour) || secondsPerHour <= 0)
                throw new ArgumentException("A spot, region and clock pace are required.");
            // FishSchoolModel reserves four species per cell. Refuse tuning that could grow those
            // private buffers during a visit; the shipped maximum is three.
            const int retainedSpeciesPerCell = 4;
            if (Math.Max(schools.MinSpecies, schools.MaxSpecies) > retainedSpeciesPerCell
                && playerPool.Count > retainedSpeciesPerCell)
                throw new ArgumentException("School species tuning exceeds the retained buffer capacity.");
            _visit = visit; _pool = playerPool; _world = world; _tideAt = tideAt;
            _spotId = spotId; _hookPosition = hookPosition;
            _depth = depth; _bait = bait; _sea = sea; _schoolSettings = schools;
            _schools = new FishSchoolModel(world, playerPool, schools, depth, secondsPerHour);
            _schoolScratch = new List<FishSchool>(FishSchoolMath.SearchCells);
            _speciesScratch = new List<string>(playerPool.Count);
        }

        /// <summary>
        /// Returns exactly CastsPerVisit records, including honest blanks. Waits and fights use clock
        /// seconds. Landed means brought to hand, not licensed, kept, sold or added to an inventory.
        /// Bait preferences are the BaitDef's own FavorsSpeciesIds list; null bait means a bare hook.
        /// </summary>
        public List<WharfVisitCast> Build(int worldSeed, double gameTime, string spotId,
            in WharfVisitRod rod, string baitId, IReadOnlyList<string> baitFavours)
        {
            if (worldSeed != _world.WorldSeed || !string.Equals(spotId, _spotId, StringComparison.Ordinal))
                throw new ArgumentException("Visit seed and spot must match their water source.");
            if (double.IsNaN(gameTime) || double.IsInfinity(gameTime) || gameTime < 0
                || string.IsNullOrWhiteSpace(rod.Id) || rod.ReelSpeedMultiplier <= 0f)
                throw new ArgumentException("A visit needs a finite time and an initialized rod.");
            _visit.Validate();
            uint key = unchecked((uint)StableHash.CatchSeed(worldSeed, spotId, gameTime));
            key = StableHash.Fold(StableHash.Fold(key, rod.Id), baitId);
            var casts = new List<WharfVisitCast>(_visit.CastsPerVisit);
            double at = gameTime;
            for (int cast = 0; cast < _visit.CastsPerVisit; cast++)
            {
                SchoolInfluence school = SchoolAt(at);
                float wait = Mathf.Lerp(_visit.MinBiteWaitSeconds, _visit.MaxBiteWaitSeconds, Draw(key, cast, 0))
                    * SeaFightMath.BiteDelayScale(_world.SeaState01At(at), _sea.SeaBoldness01)
                    * school.BiteDelayScale;
                double checkedAt = at + wait;
                CatchContext context = ContextAt(checkedAt, baitFavours);
                FishSpeciesDef fish = string.IsNullOrEmpty(baitId) || _world.WaterColumnAt(_hookPosition, checkedAt) <= 0f
                    ? null : Pick(context, Draw(key, cast, 1));
                var result = new WharfVisitCast(at, wait);
                if (fish != null)
                {
                    float size = SeaFightMath.BiasedWeight01(Draw(key, cast, 2),
                        SeaFightMath.WeightBias01(_world.SeaState01At(checkedAt), _sea.SeaBigFishBias01));
                    float weight = Mathf.Lerp(fish.MinWeightKg, fish.MaxWeightKg, size);
                    float strength = fish.RodFight != null ? fish.RodFight.Strength : RodFightStrength.NeutralStrength01;
                    WharfFishStrength band = strength <= _visit.LightStrengthCeiling ? WharfFishStrength.Light
                        : strength >= _visit.StrongStrengthFloor ? WharfFishStrength.Strong : WharfFishStrength.Steady;
                    float loss = band == WharfFishStrength.Light ? _visit.LightLossChance
                        : band == WharfFishStrength.Strong ? _visit.StrongLossChance : _visit.SteadyLossChance;
                    float fight = Mathf.Lerp(_visit.MinFightSeconds, _visit.MaxFightSeconds, strength)
                        / rod.ReelSpeedMultiplier;
                    result = new WharfVisitCast(at, wait, fish.Id, weight, fight, band,
                        Draw(key, cast, 3) >= loss * (1f - rod.FightHelp01));
                }
                casts.Add(result);
                at = result.FinishedAt + _visit.BetweenCastsSeconds;
            }
            return casts;
        }

        private SchoolInfluence SchoolAt(double at) => SchoolInfluence.At(_schools, _hookPosition, at,
            CatchContext.NoDepth, _depth, _schoolSettings, _schoolScratch, _speciesScratch);

        private CatchContext ContextAt(double at, IReadOnlyList<string> baitFavours)
        {
            float hour = _world.HourOfDayAt(at);
            float tide = _tideAt(at);
            float rate = _world.TideRateMetresPerHourAt(at);
            if (float.IsNaN(hour) || hour < 0f || hour >= 24f || float.IsNaN(tide) || float.IsInfinity(tide)
                || float.IsNaN(rate) || float.IsInfinity(rate))
                throw new ArgumentException("Visit water must supply an hour, tide and tide rate.");
            return new CatchContext(_world.RegionId, tide, hour, _world.SeasonAt(at), Gear.Handline,
                CatchContext.NoDepth, 0f, LureTag.None, baitFavours, SchoolAt(at), rate,
                _schoolSettings.MovingWaterMetresPerHour);
        }

        private FishSpeciesDef Pick(in CatchContext context, float unitRoll)
        {
            // Share every gate and preference with the player. Only the allocation-free draw differs.
            float total = 0f;
            for (int i = 0; i < _pool.Count; i++)
                if (CatchResolver.Matches(_pool[i], context))
                    total += CatchResolver.EffectiveWeight(_pool[i], context, _depth, _bait);
            double target = unitRoll * (double)total;
            double accumulated = 0;
            for (int i = 0; i < _pool.Count; i++)
            {
                FishSpeciesDef fish = _pool[i];
                if (!CatchResolver.Matches(fish, context)) continue;
                float weight = CatchResolver.EffectiveWeight(fish, context, _depth, _bait);
                if (weight <= 0f) continue;
                accumulated += weight;
                if (target < accumulated) return fish;
            }
            return null;
        }

        private float Draw(uint key, int cast, int channel)
        {
            // Fixed hash channels and 24-bit fractions specify the stream across runtimes and peers.
            uint bits = StableHash.Finalize(StableHash.Fold(StableHash.Fold(key, cast), channel));
            return (bits >> 8) * (1f / 16777216f);
        }
    }
}
