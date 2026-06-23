using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Pure, save-bound bookkeeping for the damaged-boat → repair flow (St Peters opening). A boat the
    /// player owns is <b>usable</b> only once it has been repaired; this is the single place that reads
    /// and writes that state on a <see cref="SaveData"/>, so the Shipwright and any test agree on the
    /// rule. Static + <see cref="SaveData"/>-in → fully EditMode-testable, no scene.
    ///
    /// <para>The owning lane (gameplay-systems' boarding/active-boat) asks <see cref="IsRepaired"/>
    /// before letting the player board a hull — a boat bought damaged sits owned-but-unusable until the
    /// repair is paid. Economy owns this economy-state; gameplay reads it through the save seam (the
    /// same <c>SaveData.Current</c> seam OwnedFleet's VS-08 follow-up reads).</para>
    /// </summary>
    public static class RepairLedger
    {
        /// <summary>True iff this hull has been repaired (and is therefore usable). A null save or
        /// null/empty id reads as not-repaired (safe default: don't claim an unknown boat is usable).</summary>
        public static bool IsRepaired(SaveData data, string boatId)
        {
            if (data?.RepairedBoats == null || string.IsNullOrEmpty(boatId)) return false;
            return data.RepairedBoats.Contains(boatId);
        }

        /// <summary>Mark a hull repaired (idempotent). Returns true iff it was newly added (so the caller
        /// can decide whether to persist / raise an event). No-op on a null save or null/empty id.</summary>
        public static bool MarkRepaired(SaveData data, string boatId)
        {
            if (data == null || string.IsNullOrEmpty(boatId)) return false;
            data.RepairedBoats ??= new List<string>();
            if (data.RepairedBoats.Contains(boatId)) return false;
            data.RepairedBoats.Add(boatId);
            return true;
        }
    }
}
