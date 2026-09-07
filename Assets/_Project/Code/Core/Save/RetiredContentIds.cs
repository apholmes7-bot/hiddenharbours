using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>THE RETIRED-IDS LEDGER</b> — content ids that were shipped, then withdrawn. An id on this list
    /// may never be authored again, by any Def, ever.
    ///
    /// <para><b>Why a list and not a deletion.</b> Def ids are append-only and stable (CLAUDE.md §5)
    /// because the SAVEGAME names them: <see cref="SaveData.ActiveHullId"/>, the owned-boat list, the
    /// per-hull fuel rows, the instrument locker. Deleting the asset removes the content; it does not
    /// remove the id from the saves already on disk. Re-using the id for something ELSE is the failure
    /// this ledger exists to stop — a save that said "she keeps the small skiff" would silently come back
    /// as whatever new boat had inherited the name, which is worse than an id that resolves to nothing.</para>
    ///
    /// <para><b>What happens to a save that names one.</b> Nothing dramatic, by existing design:
    /// <c>OwnedFleet.ApplyHull</c> treats an unresolved hull id as a graceful no-op ("no exception, no
    /// null-swap"), so the player keeps the hull the persistent core starts her in — the dory. The row
    /// stays in the save, inert, and the ledger keeps it inert forever.</para>
    ///
    /// <para><b>Adding to this list is a retirement, not a cleanup.</b> An id belongs here only once the
    /// owner has ruled the content out of the game and its assets are gone from Data/. The content
    /// validator reads this list and fails on any shipped Def that authors one, so the ledger is enforced
    /// rather than advisory.</para>
    /// </summary>
    public static class RetiredContentIds
    {
        /// <summary>
        /// Every withdrawn id, with the ruling that withdrew it. ORDERED BY RETIREMENT, append-only —
        /// entries are never removed, because the saves they protect are never re-written.
        /// </summary>
        public static readonly string[] All =
        {
            // ── 2026-09-06, owner ruling: "please retire old boat model that has no mesh, it was
            //    handdrawn by me". The Fishing Skiff was the last hull in the fleet drawn as eight
            //    hand-painted plan-view files (FishingBoat_N..NW) rather than baked from a rig, and the
            //    only one whose visual carried no HullMesh at all. Her art, her visual and her hull def
            //    are deleted; Celeste Bernard moved to boat.dory_outboard and the St Peters ambient
            //    fleet to the punt's compass.
            //
            //    ⚠ This id has been orphaned ONCE BEFORE (#97's engine-helm experiment) and was
            //    un-orphaned rather than replaced, precisely because ids are append-only. It is retired
            //    now, which is the end of that road: it does not come back a second time.
            "boat.fishing_skiff",
        };

        /// <summary>True when this id has been withdrawn and may not be authored by any Def.</summary>
        public static bool IsRetired(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            for (int i = 0; i < All.Length; i++)
                if (string.Equals(All[i], id, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
