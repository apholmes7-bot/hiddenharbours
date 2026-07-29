using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Encodes a hold's live catch into <see cref="HeldCatchDto"/> records and decodes them back
    /// (M1 §7.3 — freshness survives a save EXACTLY). One codec shared by every persisting container
    /// (boat hold, clam pail, freezer) so the shape can't drift between them. Pure list-in/list-out —
    /// EditMode-testable with no scene, no services.
    /// </summary>
    public static class HoldCatchCodec
    {
        /// <summary>
        /// Snapshot <paramref name="items"/> into <paramref name="into"/> (cleared first). Consecutive
        /// units with the same species AND the exact same freshness triple compress into one record's
        /// <see cref="HeldCatchDto.Quantity"/> (the same-haul case); distinct clocks stay distinct —
        /// grouping must never merge different freshness.
        /// </summary>
        public static void Capture(IReadOnlyList<CatchItem> items, List<HeldCatchDto> into)
        {
            if (into == null) return;
            into.Clear();
            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                CatchItem it = items[i];
                int last = into.Count - 1;
                if (last >= 0 && SameRun(into[last], it))
                {
                    HeldCatchDto run = into[last];
                    run.Quantity++;
                    into[last] = run;
                }
                else
                {
                    into.Add(new HeldCatchDto(it.SpeciesId, 1, it.Freshness.SpoilAccrued,
                                              it.Freshness.LastSettleGameSeconds, (int)it.Freshness.Mode));
                }
            }
        }

        /// <summary>
        /// Rebuild live items from <paramref name="records"/> into <paramref name="into"/> (cleared
        /// first): each record's species is resolved through <paramref name="factory"/> and the SAVED
        /// freshness stamped on unchanged — spoil is a pure function of (state, now), so the restored
        /// read equals the pre-save read to the bit. A record whose species no longer resolves (or a
        /// null factory) is SKIPPED, never a crash — the count of restored units is returned so the
        /// caller can log honest losses.
        /// </summary>
        public static int Restore(IReadOnlyList<HeldCatchDto> records, ICatchItemFactory factory,
                                  List<CatchItem> into)
        {
            if (into == null) return 0;
            into.Clear();
            if (records == null || factory == null) return 0;

            int restored = 0;
            for (int i = 0; i < records.Count; i++)
            {
                HeldCatchDto r = records[i];
                if (r.Quantity <= 0 || !factory.TryCreate(r.SpeciesId, out CatchItem template)) continue;
                CatchItem item = template.WithFreshness(r.Freshness);
                for (int q = 0; q < r.Quantity; q++)
                {
                    into.Add(item);
                    restored++;
                }
            }
            return restored;
        }

        private static bool SameRun(in HeldCatchDto run, in CatchItem it)
            => run.SpeciesId == it.SpeciesId
               && run.SpoilAccrued == it.Freshness.SpoilAccrued
               && run.LastSettleGameSeconds == it.Freshness.LastSettleGameSeconds
               && run.Mode == (int)it.Freshness.Mode;
    }
}
