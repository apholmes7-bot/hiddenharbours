using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The Fishing-side <see cref="ICatchItemFactory"/>: resolves a stable species id through
    /// <see cref="FishSpeciesRegistry"/> and re-caches the Def's fields onto a fresh
    /// <see cref="CatchItem"/> — the load-time inverse of the save carrying only the reference
    /// (rule 2). Registered into <see cref="GameServices.CatchFactory"/> by
    /// <see cref="FishSpeciesRegistrar"/> at boot, so Core's restore never references this module.
    ///
    /// <para>Weight is the species' mid-range: per-unit weights are not persisted and nothing prices
    /// by weight (one unit = one HU) — a representative value keeps the restored item honest in logs
    /// and tooltips. Freshness is left default; the restore stamps the SAVED state on.</para>
    /// </summary>
    public sealed class SpeciesCatchItemFactory : ICatchItemFactory
    {
        public bool TryCreate(string speciesId, out CatchItem item)
        {
            FishSpeciesDef def = FishSpeciesRegistry.Get(speciesId);
            if (def == null)
            {
                item = default;
                return false;
            }
            float midWeight = (def.MinWeightKg + def.MaxWeightKg) * 0.5f;
            item = new CatchItem(def.Id, def.DisplayName, def.Category, midWeight,
                                 def.BaseValue, def.SupplyElasticity, def.SpoilPerDay, default);
            return true;
        }
    }
}
