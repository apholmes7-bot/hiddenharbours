using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Encodes a hold's live catch into <see cref="HeldCatchDto"/> records and decodes them back
    /// (M1 §7.3 — freshness survives a save EXACTLY). One codec shared by every persisting container
    /// (boat hold, clam pail, freezer) so the shape can't drift between them. <see cref="Capture"/> and
    /// the facts-taking <see cref="Restore(IReadOnlyList{HeldCatchDto}, ICatchItemFactory, IFishConservationFacts, List{CatchItem}, List{CatchItem})"/>
    /// are pure list-in/list-out, EditMode-testable with no scene and no services. The short
    /// <see cref="Restore(IReadOnlyList{HeldCatchDto}, ICatchItemFactory, List{CatchItem})"/> reads the
    /// registered <see cref="GameServices.FishConservation"/> and publishes <see cref="CatchReleased"/>
    /// for each protected unit it drops.
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
        /// caller can log honest losses. Protected units are dropped too and reported as releases
        /// (see the facts-taking overload); this wrapper reads the registered facts and publishes one
        /// <see cref="CatchReleased"/> per dropped unit.
        /// </summary>
        public static int Restore(IReadOnlyList<HeldCatchDto> records, ICatchItemFactory factory,
                                  List<CatchItem> into)
        {
            var released = new List<CatchItem>();   // load-time only, never per frame
            int restored = Restore(records, factory, GameServices.FishConservation, into, released);
            PublishReleased(released);
            return restored;
        }

        /// <summary>
        /// The pure restore. As the short overload, but the protected-species facts are handed in: a
        /// species flagged protected after the save was written goes back in the water (owner ruling
        /// 2026-11-15). Each such unit is left out of <paramref name="into"/> and, when
        /// <paramref name="released"/> is given, appended there (cleared first) so the caller reports it
        /// as a release, never as an unresolved id. Null facts drop nothing. Returns the units restored.
        /// </summary>
        public static int Restore(IReadOnlyList<HeldCatchDto> records, ICatchItemFactory factory,
                                  IFishConservationFacts facts, List<CatchItem> into,
                                  List<CatchItem> released = null)
        {
            released?.Clear();
            if (into == null) return 0;
            into.Clear();
            if (records == null || factory == null) return 0;

            int restored = 0;
            for (int i = 0; i < records.Count; i++)
            {
                HeldCatchDto r = records[i];
                if (r.Quantity <= 0 || !factory.TryCreate(r.SpeciesId, out CatchItem template)) continue;
                CatchItem item = template.WithFreshness(r.Freshness);
                bool keep = CatchKeep.MayKeep(r.SpeciesId, facts);
                for (int q = 0; q < r.Quantity; q++)
                {
                    if (keep) { into.Add(item); restored++; }
                    else released?.Add(item);
                }
            }
            return restored;
        }

        /// <summary>Publish one <see cref="CatchReleased"/> per unit a restore let go. Null-safe.</summary>
        public static void PublishReleased(IReadOnlyList<CatchItem> released)
        {
            if (released == null) return;
            for (int i = 0; i < released.Count; i++)
                EventBus.Publish(new CatchReleased(released[i]));
        }

        private static bool SameRun(in HeldCatchDto run, in CatchItem it)
            => run.SpeciesId == it.SpeciesId
               && run.SpoilAccrued == it.Freshness.SpoilAccrued
               && run.LastSettleGameSeconds == it.Freshness.LastSettleGameSeconds
               && run.Mode == (int)it.Freshness.Mode;
    }
}
