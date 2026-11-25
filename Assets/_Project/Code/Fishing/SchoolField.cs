using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// Caller-certified, static homogeneous habitat disc wholly inside its cell. HighestBedMetres
    /// bounds the highest ground ANYWHERE in the disc. Unknown vegetation never earns a preference.
    /// No main-world ecology adapter is implied: absent certification means no new-field fish.
    /// </summary>
    public readonly struct SchoolHabitatPatch
    {
        public readonly Vector2 Centre;
        public readonly float RadiusMetres, HighestBedMetres;
        public readonly FishHabitat Habitats;
        public readonly FishVegetation Vegetation;
        public readonly bool VegetationKnown;

        public SchoolHabitatPatch(Vector2 centre, float radiusMetres, float highestBedMetres,
                                  FishHabitat habitats, FishVegetation vegetation, bool vegetationKnown)
        {
            Centre = centre;
            RadiusMetres = radiusMetres;
            HighestBedMetres = highestBedMetres;
            Habitats = habitats;
            Vegetation = vegetation;
            VegetationKnown = vegetationKnown;
        }
    }

    /// <summary>Deterministic environment read for the query's exact game instant, in datum metres.</summary>
    public readonly struct SchoolFieldEnvironment
    {
        public readonly float WaterLevelMetres, Tide01;
        public SchoolFieldEnvironment(float waterLevelMetres, float tide01)
        { WaterLevelMetres = waterLevelMetres; Tide01 = tide01; }
    }

    /// <summary>
    /// One explicitly supplied local event; queries never mutate it. Keep this value fixed while
    /// sampling its path. Replacing a live event needs a continuous blend at the integration layer.
    /// Default is no reaction. This field does not save player-caused events or hooked individuals.
    /// </summary>
    public readonly struct SchoolReaction
    {
        public readonly double StartGameSeconds;
        public readonly Vector2 Direction;
        public readonly float Strength01;
        public SchoolReaction(double startGameSeconds, Vector2 direction, float strength01)
        { StartGameSeconds = startGameSeconds; Direction = direction; Strength01 = strength01; }
    }

    public readonly struct SchoolFieldSample
    {
        public readonly string SpeciesId;
        public readonly uint Key;
        public readonly Vector2 Centre;
        public readonly float DepthMetres, FootprintMetres, Tightness01;
        public readonly int Individuals;
        public readonly double MaxPlanarMetresPerGameSecond;

        public SchoolFieldSample(string speciesId, uint key, Vector2 centre, float depthMetres,
                                 float footprintMetres, float tightness01, int individuals, double maxSpeed)
        {
            SpeciesId = speciesId; Key = key; Centre = centre; DepthMetres = depthMetres;
            FootprintMetres = footprintMetres; Tightness01 = tightness01; Individuals = individuals;
            MaxPlanarMetresPerGameSecond = maxSpeed;
        }
    }

    /// <summary>
    /// Continuous species/cell field, deliberately not installed into the existing FishSchoolModel.
    /// O(species per cell), no per-query allocation, RNG, scene, MonoBehaviour or hidden state.
    /// Positions are recomputed from seed/time plus immutable content/terrain and explicit reaction.
    /// Simulation speeds are metres per Core game-clock second; pass the real SecondsPerDay config.
    /// </summary>
    public sealed class SchoolField
    {
        private readonly int _seed;
        private readonly string _regionId;
        private readonly double _secondsPerDay, _cellSize;
        // Permanent independent hash streams. Append; never renumber existing decisions.
        private const int PresenceStream = 0, PhaseStream = 1, DepthPhaseStream = 2, CountStream = 3;
        private const double Tau = 2d * Math.PI;

        public SchoolField(int worldSeed, string regionId, double secondsPerDay, double cellSizeMetres)
        {
            if (string.IsNullOrEmpty(regionId)) throw new ArgumentException("A stable region id is required.", nameof(regionId));
            if (!LivingFishBehaviour.Finite(secondsPerDay) || secondsPerDay <= 0d)
                throw new ArgumentOutOfRangeException(nameof(secondsPerDay));
            if (!LivingFishBehaviour.Finite(cellSizeMetres) || cellSizeMetres <= 0d)
                throw new ArgumentOutOfRangeException(nameof(cellSizeMetres));
            _seed = worldSeed; _regionId = regionId; _secondsPerDay = secondsPerDay; _cellSize = cellSizeMetres;
        }

        /// <summary>
        /// Fills a caller-owned bounded buffer, in catalogue order, returning the number written.
        /// A null buffer writes zero. A truncated buffer is a query budget, not population depletion.
        /// Each species result is independent of catalogue order and all prior queries.
        /// </summary>
        public int FillCell(IReadOnlyList<FishSpeciesDef> species, int cellX, int cellY, double gameSeconds,
                            in SchoolHabitatPatch patch, in SchoolFieldEnvironment environment,
                            in SchoolReaction reaction, SchoolFieldSample[] into)
        {
            if (species == null || into == null) return 0;
            int written = 0;
            for (int i = 0; i < species.Count && written < into.Length; i++)
                if (TrySample(species[i], cellX, cellY, gameSeconds, patch, environment, reaction, out var sample))
                    into[written++] = sample;
            return written;
        }

        public bool TrySample(FishSpeciesDef species, int cellX, int cellY, double gameSeconds,
                              in SchoolHabitatPatch patch, in SchoolFieldEnvironment environment,
                              in SchoolReaction reaction, out SchoolFieldSample sample)
        {
            sample = default;
            if (species == null || string.IsNullOrEmpty(species.Id) || !LivingFishBehaviour.Finite(gameSeconds)) return false;
            LivingFishBehaviour b = species.LivingBehaviour;
            if (!b.Enabled || !b.IsValid || !InRegion(species) || !ValidPatch(patch, cellX, cellY)) return false;
            if ((patch.Habitats & b.AllowedHabitats) == 0) return false;
            if (!LivingFishBehaviour.Finite(environment.WaterLevelMetres) ||
                !LivingFishBehaviour.Finite(environment.Tide01) ||
                !LivingFishBehaviour.Finite(species.SchoolsPerSquareKilometre) || species.SchoolsPerSquareKilometre <= 0f ||
                species.MinSchoolMarks <= 0 || species.MaxSchoolMarks < species.MinSchoolMarks) return false;
            if (!LivingFishBehaviour.Finite(reaction.StartGameSeconds) || !LivingFishBehaviour.Finite(reaction.Direction.x) ||
                !LivingFishBehaviour.Finite(reaction.Direction.y) || !LivingFishBehaviour.Finite(reaction.Strength01)) return false;

            uint key = KeyFor(species.Id, cellX, cellY);
            double preference = patch.VegetationKnown && (patch.Vegetation & b.PreferredVegetation) != 0
                ? b.VegetationPreferenceMultiplier : 1d;
            double chance = Clamp01(species.SchoolsPerSquareKilometre * _cellSize * _cellSize / 1000000d * preference);
            if (Draw(key, PresenceStream) >= chance) return false;

            bool sessile = b.Kind == LivingFishKind.Sessile;
            double scatterDistance = sessile ? 0d : b.ScatterDistanceMetres;
            double reserve = b.ScatteredFootprintMetres + scatterDistance;
            if (reserve > patch.RadiusMetres) return false;
            double radius = sessile ? 0d : Math.Min(b.PatrolRadiusMetres, patch.RadiusMetres - reserve);
            double speed = radius > 0d ? b.PatrolMetresPerGameSecond : 0d;
            double angle = Tau * Draw(key, PhaseStream);
            if (speed > 0d) angle += Tau * Cycle01(gameSeconds, Tau * radius / speed);
            double x = patch.Centre.x + radius * Math.Cos(angle);
            double y = patch.Centre.y + radius * Math.Sin(angle);

            double envelope = ReactionEnvelope(gameSeconds, reaction, b.ReactionDurationGameSeconds);
            double directionLength = Math.Sqrt((double)reaction.Direction.x * reaction.Direction.x +
                                               (double)reaction.Direction.y * reaction.Direction.y);
            if (directionLength > 0d && !sessile)
            {
                x += reaction.Direction.x / directionLength * scatterDistance * envelope;
                y += reaction.Direction.y / directionLength * scatterDistance * envelope;
            }
            else envelope = 0d;

            double lo = b.MinDepthMetres;
            double hi = Math.Min(b.MaxDepthMetres,
                (double)environment.WaterLevelMetres - patch.HighestBedMetres - b.BedClearanceMetres);
            if (hi < lo) return false; // Never clamp a species onto dry ground or outside its band.
            bool bottom = b.Kind == LivingFishKind.BottomDweller || b.Kind == LivingFishKind.Crawler || sessile;
            double depth01 = Clamp01(0.5d + 0.5d * b.DailyDepthStrength01 *
                Math.Cos(Tau * Cycle01(gameSeconds, _secondsPerDay) + Tau * Draw(key, DepthPhaseStream)) +
                b.TidalDepthStrength01 * (Clamp01(environment.Tide01) - 0.5d));
            // A conservative patch depth, NOT a rendered bed attachment. Crawlers need a later
            // actual-position elevation adapter before presentation can be connected.
            float depth = (float)(bottom ? hi : lo + (hi - lo) * depth01);
            float footprint = (float)(b.NormalFootprintMetres +
                (b.ScatteredFootprintMetres - b.NormalFootprintMetres) * envelope);
            int count = b.Kind == LivingFishKind.Ambusher || sessile ? 1 :
                (int)Math.Min(species.MaxSchoolMarks, species.MinSchoolMarks + Math.Floor(
                    Draw(key, CountStream) * ((long)species.MaxSchoolMarks - species.MinSchoolMarks + 1L)));
            double maxSpeed = speed + scatterDistance * Math.PI / b.ReactionDurationGameSeconds;
            sample = new SchoolFieldSample(species.Id, key, new Vector2((float)x, (float)y), depth,
                                            footprint, (float)(1d - envelope), count, maxSpeed);
            return true;
        }

        private bool InRegion(FishSpeciesDef species)
        {
            if (species.RegionIds == null) return false;
            for (int i = 0; i < species.RegionIds.Length; i++)
                if (string.Equals(species.RegionIds[i], _regionId, StringComparison.Ordinal)) return true;
            return false;
        }

        private bool ValidPatch(in SchoolHabitatPatch p, int cellX, int cellY)
        {
            if (!LivingFishBehaviour.Finite(p.Centre.x) || !LivingFishBehaviour.Finite(p.Centre.y) ||
                !LivingFishBehaviour.Finite(p.RadiusMetres) || p.RadiusMetres < 0f ||
                !LivingFishBehaviour.Finite(p.HighestBedMetres) || p.Habitats == FishHabitat.Unknown ||
                (p.Habitats & ~FishHabitat.All) != 0 || (p.Vegetation & ~FishVegetation.All) != 0) return false;
            double minX = cellX * _cellSize, minY = cellY * _cellSize;
            return p.Centre.x - (double)p.RadiusMetres >= minX && p.Centre.x + (double)p.RadiusMetres <= minX + _cellSize &&
                   p.Centre.y - (double)p.RadiusMetres >= minY && p.Centre.y + (double)p.RadiusMetres <= minY + _cellSize;
        }

        private uint KeyFor(string speciesId, int cellX, int cellY)
        {
            uint h = StableHash.Fold(2166136261u, _seed);
            h = StableHash.Fold(h, _regionId);
            h = StableHash.Fold(h, cellX); h = StableHash.Fold(h, cellY);
            return StableHash.Finalize(StableHash.Fold(h, speciesId));
        }

        private static double Draw(uint key, int stream)
            => StableHash.Finalize(StableHash.Fold(key, stream)) / 4294967296d;
        private static double Clamp01(double value) => Math.Max(0d, Math.Min(1d, value));
        private static double Cycle01(double time, double period)
        {
            double phase = time % period;
            return (phase < 0d ? phase + period : phase) / period;
        }

        private static double ReactionEnvelope(double time, in SchoolReaction reaction, double duration)
        {
            double age = time - reaction.StartGameSeconds;
            if (age <= 0d || age >= duration) return 0d;
            double pulse = Math.Sin(Math.PI * age / duration);
            return Clamp01(reaction.Strength01) * pulse * pulse;
        }
    }
}
