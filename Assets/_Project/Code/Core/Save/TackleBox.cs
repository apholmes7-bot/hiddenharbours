using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// THE TACKLE BOX — the one place the player's bait stock and owned tackle are read and spent.
    ///
    /// <para><b>Why it exists.</b> Bait was counted inline inside <c>PlacedTrapService</c> when traps were
    /// the only thing that ate it. The rod eats it too now, and a third caller (a shop restocking it)
    /// is coming — three hand-rolled copies of "find the entry, decrement, write it back" is exactly how
    /// an off-by-one gets into a save file. So the wallet lives here once, in Core, where Fishing and
    /// Economy can both reach it without referencing each other (rule 4).</para>
    ///
    /// <para><b>The two shapes, deliberately different.</b> Bait is COUNTED (you have four herring and
    /// then three) — <see cref="SaveData.BaitStock"/>. Tackle is PRESENCE-OWNED (you have a cod jig or
    /// you don't) — <see cref="SaveData.OwnedGear"/>, the same wallet the rod and shovel live in. That
    /// mirrors how they really work: bait is consumed by the fish, a lure is a thing you keep.</para>
    ///
    /// <para>Static and null-safe throughout: a null save (EditMode rigs, pre-boot) reads as "nothing
    /// owned, nothing spent" rather than throwing, the established greybox posture.</para>
    /// </summary>
    public static class TackleBox
    {
        // ---- bait: counted stock ------------------------------------------------------------------

        /// <summary>How much of this bait the player has. 0 for an unknown id, a null save, or a null id.</summary>
        public static int BaitCount(SaveData save, string baitId)
        {
            if (save?.BaitStock == null || string.IsNullOrEmpty(baitId)) return 0;
            for (int i = 0; i < save.BaitStock.Count; i++)
                if (save.BaitStock[i].BaitId == baitId) return save.BaitStock[i].Count;
            return 0;
        }

        /// <summary>True when there is at least one of this bait to put on a hook.</summary>
        public static bool HasBait(SaveData save, string baitId) => BaitCount(save, baitId) > 0;

        /// <summary>
        /// Spend ONE of this bait. Returns false (and changes nothing) when there is none — callers use
        /// that as the gate, so a bait-fishing cast simply can't happen on an empty box.
        /// </summary>
        public static bool TrySpendBait(SaveData save, string baitId)
        {
            if (save?.BaitStock == null || string.IsNullOrEmpty(baitId)) return false;
            for (int i = 0; i < save.BaitStock.Count; i++)
            {
                if (save.BaitStock[i].BaitId != baitId || save.BaitStock[i].Count <= 0) continue;
                int left = save.BaitStock[i].Count - 1;
                if (left > 0) save.BaitStock[i] = new BaitStock(baitId, left);
                else save.BaitStock.RemoveAt(i);   // an empty entry is clutter, not a zero to carry
                return true;
            }
            return false;
        }

        /// <summary>Add bait to the box (a purchase, a dev grant, or a netful of capelin). Non-positive
        /// counts are ignored rather than silently subtracting.</summary>
        public static void AddBait(SaveData save, string baitId, int count)
        {
            if (save == null || string.IsNullOrEmpty(baitId) || count <= 0) return;
            save.BaitStock ??= new List<BaitStock>();
            for (int i = 0; i < save.BaitStock.Count; i++)
                if (save.BaitStock[i].BaitId == baitId)
                {
                    save.BaitStock[i] = new BaitStock(baitId, save.BaitStock[i].Count + count);
                    return;
                }
            save.BaitStock.Add(new BaitStock(baitId, count));
        }

        // ---- tackle: presence-owned ---------------------------------------------------------------

        /// <summary>True when the player owns this tackle. Tackle isn't consumed — a lure you own stays
        /// yours (until the owner's "a blow can cost you your rig" ruling is built, which would remove
        /// one here).</summary>
        public static bool OwnsTackle(SaveData save, string tackleId)
        {
            if (save?.OwnedGear == null || string.IsNullOrEmpty(tackleId)) return false;
            for (int i = 0; i < save.OwnedGear.Count; i++)
                if (save.OwnedGear[i] == tackleId) return true;
            return false;
        }

        /// <summary>Put a piece of tackle in the box (a purchase or the starting grant). Idempotent —
        /// owning it twice is meaningless, so a repeat is a no-op rather than a duplicate entry.</summary>
        public static void GrantTackle(SaveData save, string tackleId)
        {
            if (save == null || string.IsNullOrEmpty(tackleId)) return;
            save.OwnedGear ??= new List<string>();
            if (!OwnsTackle(save, tackleId)) save.OwnedGear.Add(tackleId);
        }

        /// <summary>Take a piece of tackle away — the seam a lost rig would use. Returns whether
        /// anything was actually there to lose.</summary>
        public static bool RemoveTackle(SaveData save, string tackleId)
        {
            if (save?.OwnedGear == null || string.IsNullOrEmpty(tackleId)) return false;
            return save.OwnedGear.Remove(tackleId);
        }
    }
}
