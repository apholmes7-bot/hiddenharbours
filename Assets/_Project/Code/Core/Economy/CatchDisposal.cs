using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The DISPOSE verb's logic (M1 §7.3): empty the rubbish out of a hold. Spoiled catch is refused
    /// by every buyer yet still occupies hold space — without this verb a fully-spoiled hold would be
    /// a soft-lock (nothing sells, nothing fits). Dumping only ever removes what is ALREADY worthless
    /// (<see cref="SpoilContext.IsSpoiled"/>), so the verb is always safe to press: the loss happened
    /// on the clock, not at the rail (P5 — teeth, telegraphed, survivable).
    ///
    /// <para>Static with Core-contract inputs (<see cref="IHold"/> + <see cref="SpoilContext"/>) →
    /// EditMode-testable, same shape as <c>SellService</c>. Publishes <see cref="CatchDumped"/> so the
    /// hold-reading presenters refresh; deliberately NOT <see cref="CatchSold"/> — nothing was paid.</para>
    /// </summary>
    public static class CatchDisposal
    {
        /// <summary>
        /// Remove every spoiled unit from <paramref name="hold"/>. Returns how many were dumped
        /// (0 leaves the hold untouched and publishes nothing). Fresh and still-sellable catch is
        /// never touched — this is taking out the rubbish, not emptying the hold.
        /// </summary>
        public static int DumpSpoiled(IHold hold, in SpoilContext spoil)
        {
            if (hold == null || hold.UsedUnits == 0) return 0;

            var items = hold.Items;
            var keep = new List<CatchItem>(items.Count);
            int dumped = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (spoil.IsSpoiled(items[i])) dumped++;
                else keep.Add(items[i]);
            }
            if (dumped == 0) return 0;

            // IHold exposes only Clear()/TryAdd() (no partial remove) — the SellService rebuild pattern.
            hold.Clear();
            for (int i = 0; i < keep.Count; i++) hold.TryAdd(keep[i]);

            EventBus.Publish(new CatchDumped(dumped));
            return dumped;
        }
    }
}
