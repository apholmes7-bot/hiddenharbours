namespace HiddenHarbours.Core
{
    /// <summary>
    /// Rebuilds a <see cref="CatchItem"/> from a stable species id — the load-time inverse of the
    /// save carrying only the REFERENCE (rule 2: the Def's stats are never persisted, so someone who
    /// can see the Defs must re-cache them). Implemented in the Fishing module over its species
    /// registry and registered into <see cref="GameServices.CatchFactory"/> at boot (the
    /// FishSpeciesRegistrar bootstrap) — so Core's save restore never references Fishing (rule 4).
    ///
    /// <para>The produced item carries the Def's cached fields and a DEFAULT freshness clock — the
    /// caller stamps the saved <see cref="FreshnessState"/> on via <see cref="CatchItem.WithFreshness"/>.
    /// Weight is representative (the species' mid-range): per-unit weights are not persisted, and
    /// nothing downstream prices by weight (one unit = one HU).</para>
    /// </summary>
    public interface ICatchItemFactory
    {
        /// <summary>True and a rebuilt item when <paramref name="speciesId"/> resolves; false (and
        /// default) for an unknown id — the caller skips the record rather than crashing the load.</summary>
        bool TryCreate(string speciesId, out CatchItem item);
    }
}
