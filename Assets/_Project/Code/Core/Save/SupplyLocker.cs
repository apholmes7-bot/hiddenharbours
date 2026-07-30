using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// THE SUPPLY LOCKER — the one place the player's consumable supplies (ice first, §7.3's lids next) are
    /// counted, added and spent.
    ///
    /// <para><b>Why it exists, and why here.</b> Exactly the reason <see cref="TackleBox"/> exists: a
    /// consumable read and written by three lanes (Economy sells it, the Fishing/Player cold chain spends
    /// it, a test grants it) turns into three hand-rolled "find the entry, decrement, write it back" loops,
    /// which is how an off-by-one gets into a save file. So the wallet lives here once, in Core, where
    /// Economy and the container code can both reach it without referencing each other (rule 4).</para>
    ///
    /// <para><b>Counted, not presence-owned.</b> A bag of ice is spent when you pack a bucket with it;
    /// <see cref="SaveData.OwnedGear"/> is for things you keep. That is the same deliberate split
    /// <see cref="TackleBox"/> documents between bait and tackle — supplies sit on the bait side of it.
    /// Deliberately keyed by a GENERIC supply id rather than one field per consumable, so the rest of the
    /// cold chain is new Def assets and no further schema bumps.</para>
    ///
    /// <para>Static and null-safe throughout: a null save (EditMode rigs, pre-boot) reads as "nothing owned,
    /// nothing spent" rather than throwing — the established greybox posture.</para>
    /// </summary>
    public static class SupplyLocker
    {
        /// <summary>How much of this supply the player has. 0 for an unknown id, a null save, or a null id.</summary>
        public static int Count(SaveData save, string supplyId)
        {
            if (save?.SupplyStock == null || string.IsNullOrEmpty(supplyId)) return 0;
            for (int i = 0; i < save.SupplyStock.Count; i++)
                if (save.SupplyStock[i].SupplyId == supplyId) return save.SupplyStock[i].Count;
            return 0;
        }

        /// <summary>True when there is at least one of this supply to use.</summary>
        public static bool Has(SaveData save, string supplyId) => Count(save, supplyId) > 0;

        /// <summary>
        /// Spend ONE of this supply. Returns false (and changes nothing) when there is none — callers use
        /// that as the gate, so packing a bucket with ice simply can't happen on an empty locker.
        /// </summary>
        public static bool TrySpend(SaveData save, string supplyId)
        {
            if (save?.SupplyStock == null || string.IsNullOrEmpty(supplyId)) return false;
            for (int i = 0; i < save.SupplyStock.Count; i++)
            {
                if (save.SupplyStock[i].SupplyId != supplyId || save.SupplyStock[i].Count <= 0) continue;
                int left = save.SupplyStock[i].Count - 1;
                if (left > 0) save.SupplyStock[i] = new SupplyStock(supplyId, left);
                else save.SupplyStock.RemoveAt(i);   // an empty entry is clutter, not a zero to carry
                return true;
            }
            return false;
        }

        /// <summary>Add supplies to the locker (a purchase or a grant). Returns the new owned total.
        /// Non-positive counts are ignored rather than silently subtracting.</summary>
        public static int Add(SaveData save, string supplyId, int count)
        {
            if (save == null || string.IsNullOrEmpty(supplyId) || count <= 0) return Count(save, supplyId);
            save.SupplyStock ??= new List<SupplyStock>();
            for (int i = 0; i < save.SupplyStock.Count; i++)
                if (save.SupplyStock[i].SupplyId == supplyId)
                {
                    int total = save.SupplyStock[i].Count + count;
                    save.SupplyStock[i] = new SupplyStock(supplyId, total);
                    return total;
                }
            save.SupplyStock.Add(new SupplyStock(supplyId, count));
            return count;
        }
    }
}
