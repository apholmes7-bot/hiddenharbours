namespace HiddenHarbours.Core
{
    /// <summary>
    /// The pot LOCKER — pure, engine-free helpers over <see cref="SaveData.PotStock"/> (owned pots,
    /// counted per TrapDef id) and the honest availability derivation the trap loop gates on:
    ///
    /// <para><b>available = owned − deployed − aboard.</b> OWNED is the only stored number (a purchase
    /// or a starter grant increments it; nothing today decrements it — pots can't yet be lost or sold).
    /// DEPLOYED is derived by counting <see cref="SaveData.PlacedTraps"/> (a pot in the water is one of
    /// your pots, working). ABOARD is the transient hauled deck pot (the #193 work-the-deck state, held
    /// by the Fishing lane and passed in as a plain count — Core stays lane-blind). Deriving, never
    /// storing, means the count can't desync: a haul moves a pot from deployed to aboard (net zero), a
    /// re-set moves it back (net zero), the legacy instant-land haul frees it (deployed −1 → one spare
    /// in the locker) — all without a single write. Rule 5's recompute-don't-store discipline applied
    /// to gear. (ADR 0020 addendum.)</para>
    ///
    /// <para>Both lanes meet here: Economy increments OWNED at the shipwright sale
    /// (<c>PotShop</c>), Fishing reads AVAILABLE before a fresh set (<c>PlacedTrapService</c>) — neither
    /// references the other (rule 4). Null-safe throughout: a null save reads as "owns nothing".</para>
    /// </summary>
    public static class PotLocker
    {
        /// <summary>How many pots of <paramref name="trapDefId"/> the player OWNS (deployed + aboard +
        /// spare). 0 for a null save / unknown id.</summary>
        public static int OwnedCount(SaveData save, string trapDefId)
        {
            if (save?.PotStock == null || string.IsNullOrEmpty(trapDefId)) return 0;
            for (int i = 0; i < save.PotStock.Count; i++)
                if (save.PotStock[i].TrapDefId == trapDefId) return save.PotStock[i].Count;
            return 0;
        }

        /// <summary>
        /// Add <paramref name="count"/> pots of <paramref name="trapDefId"/> to the owned stock (a
        /// purchase / starter grant). Merges into the existing record (one record per kind). Returns the
        /// new owned total, or 0 if nothing could be added (null save / empty id / non-positive count —
        /// owned stock only ever grows through here, so a negative "add" is refused, not applied).
        /// </summary>
        public static int AddOwned(SaveData save, string trapDefId, int count)
        {
            if (save == null || string.IsNullOrEmpty(trapDefId) || count <= 0) return 0;
            save.PotStock ??= new System.Collections.Generic.List<PotStock>();
            for (int i = 0; i < save.PotStock.Count; i++)
            {
                if (save.PotStock[i].TrapDefId == trapDefId)
                {
                    int total = save.PotStock[i].Count + count;
                    save.PotStock[i] = new PotStock(trapDefId, total);
                    return total;
                }
            }
            save.PotStock.Add(new PotStock(trapDefId, count));
            return count;
        }

        /// <summary>How many pots of <paramref name="trapDefId"/> are DEPLOYED — sitting in the water as
        /// <see cref="SaveData.PlacedTraps"/> records. Derived by counting, never stored.</summary>
        public static int DeployedCount(SaveData save, string trapDefId)
        {
            if (save?.PlacedTraps == null || string.IsNullOrEmpty(trapDefId)) return 0;
            int deployed = 0;
            for (int i = 0; i < save.PlacedTraps.Count; i++)
                if (save.PlacedTraps[i].TrapDefId == trapDefId) deployed++;
            return deployed;
        }

        /// <summary>
        /// How many pots of <paramref name="trapDefId"/> are FREE TO SET right now: owned minus deployed
        /// minus <paramref name="aboardCount"/> (the transient hauled deck pot, supplied by the Fishing
        /// lane; pass 0 when nothing is aboard). Can legitimately go NEGATIVE on a pre-v4 save mid-loop
        /// or a hand-edited blob — callers gate on <c>&gt; 0</c>, so negative simply reads "none spare".
        /// </summary>
        public static int AvailableCount(SaveData save, string trapDefId, int aboardCount)
            => OwnedCount(save, trapDefId) - DeployedCount(save, trapDefId)
               - (aboardCount > 0 ? aboardCount : 0);
    }
}
