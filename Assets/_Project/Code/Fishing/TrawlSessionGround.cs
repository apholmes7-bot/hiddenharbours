using System;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>Immutable copy of FishingGroundDef's existing shape, never a population generator.</summary>
    public sealed class TrawlSessionGround
    {
        private readonly string[] _fishIds;
        public string Id { get; }
        public string RegionId { get; }
        public Vector2 Center { get; }
        public Vector2 HalfSize { get; }
        public double MinDepthMetres { get; }
        public double MaxDepthMetres { get; }
        public EcologyHabitat Bottom { get; }
        public double Hardness01 { get; }
        public int SpeciesCount => _fishIds.Length;
        public string SpeciesAt(int index) => _fishIds[index];

        public TrawlSessionGround(string id, string regionId, Vector2 center, Vector2 halfSize,
            double minDepthMetres, double maxDepthMetres, EcologyHabitat bottom, double hardness01, string[] fishIds)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(regionId)
                || !TrawlNumbers.Finite(center.x) || !TrawlNumbers.Finite(center.y)
                || !TrawlNumbers.Positive(halfSize.x) || !TrawlNumbers.Positive(halfSize.y)
                || !TrawlNumbers.Positive(minDepthMetres) || !TrawlNumbers.Positive(maxDepthMetres)
                || maxDepthMetres < minDepthMetres || bottom == EcologyHabitat.None
                || (bottom & ~(EcologyHabitat.OpenWater | EcologyHabitat.Sand | EcologyHabitat.Mud
                    | EcologyHabitat.Gravel | EcologyHabitat.Rock | EcologyHabitat.Estuary)) != 0
                || !TrawlNumbers.Unit(hardness01) || fishIds == null)
                throw new ArgumentException("Invalid fishing ground snapshot.");
            _fishIds = (string[])fishIds.Clone();
            for (int i = 0; i < _fishIds.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(_fishIds[i])) throw new ArgumentException("Missing species id.", nameof(fishIds));
                for (int j = 0; j < i; j++)
                    if (_fishIds[j] == _fishIds[i]) throw new ArgumentException("Duplicate species id.", nameof(fishIds));
            }
            Id = id; RegionId = regionId; Center = center; HalfSize = halfSize;
            MinDepthMetres = minDepthMetres; MaxDepthMetres = maxDepthMetres; Bottom = bottom; Hardness01 = hardness01;
        }

        public static TrawlSessionGround FromDefinition(FishingGroundDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            return new TrawlSessionGround(def.Id, def.RegionId, def.Center, def.HalfSize,
                def.MinDepthMetres, def.MaxDepthMetres, def.Bottom, def.Hardness01, def.FishIds);
        }

        public bool Holds(string speciesId)
        {
            for (int i = 0; i < _fishIds.Length; i++)
                if (string.Equals(_fishIds[i], speciesId, StringComparison.Ordinal)) return true;
            return false;
        }

        // A rectangle is convex: both endpoints with this margin certify the swept segment.
        public bool Contains(Vector2 netCentre, double halfWidthMetres)
            => TrawlNumbers.NonNegative(halfWidthMetres) && TrawlNumbers.Finite(netCentre.x)
                && TrawlNumbers.Finite(netCentre.y)
                && Math.Abs((double)netCentre.x - Center.x) + halfWidthMetres <= HalfSize.x
                && Math.Abs((double)netCentre.y - Center.y) + halfWidthMetres <= HalfSize.y;
    }
}
