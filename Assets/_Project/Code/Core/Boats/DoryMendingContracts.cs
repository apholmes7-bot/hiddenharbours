using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    [Serializable]
    public struct MendingMaterial
    {
        public string ItemId;
        public int Count;
        public MendingMaterial(string itemId, int count) { ItemId = itemId; Count = count; }
    }

    /// <summary>The goods the player has to mend with: what he carries, plus what he has put away
    /// (a bag set down elsewhere is the adapter's call). Count excludes reservations. Reserving
    /// locks the whole request against spending, transfer and use elsewhere without debiting saved
    /// contents. A failed reservation changes nothing.</summary>
    public interface IMendingCarry
    {
        int Count(string itemId);
        bool TryReserve(IReadOnlyList<MendingMaterial> materials, out IMendingReservation reservation);
    }

    /// <summary>A session-owned claim on physically carried goods, bound to the loaded save and
    /// carrier. Dispose releases unconsumed goods without changing their saved count. TryConsume
    /// debits the whole claim exactly once, or returns false with no changes. Consumption must be
    /// synchronous, without callbacks or save writes; the caller records completion before saving.
    /// Losing the carrier or loaded save invalidates the claim. Never serialize a reservation.</summary>
    public interface IMendingReservation : IDisposable
    {
        bool IsValid { get; }
        bool TryConsume();
    }

    public enum DoryMendReason
    {
        None, Busy, SessionChanged, WrongPlace, BoatNotOwned, BoatAlreadyRepaired,
        UnknownJob, EarlierJobsIncomplete, JobAlreadyDone, JobInProgress, MissingMaterials,
        LowWaterRequired, ClockUnavailable, NotStarted, InvalidWorkDuration, InvalidProgress,
        NotRepaired, HighWaterRequired, SupportedLaunchRequired
    }

    public static class DoryMendingReasons
    {
        public static string TextKey(this DoryMendReason reason)
        {
            switch (reason)
            {
                case DoryMendReason.None: return "dory_mend.ready";
                case DoryMendReason.Busy: return "dory_mend.busy";
                case DoryMendReason.SessionChanged: return "dory_mend.session_changed";
                case DoryMendReason.WrongPlace: return "dory_mend.wrong_place";
                case DoryMendReason.BoatNotOwned: return "dory_mend.boat_not_owned";
                case DoryMendReason.BoatAlreadyRepaired: return "dory_mend.already_repaired";
                case DoryMendReason.UnknownJob: return "dory_mend.unknown_job";
                case DoryMendReason.EarlierJobsIncomplete: return "dory_mend.earlier_jobs";
                case DoryMendReason.JobAlreadyDone: return "dory_mend.job_done";
                case DoryMendReason.JobInProgress: return "dory_mend.job_in_progress";
                case DoryMendReason.MissingMaterials: return "dory_mend.missing_materials";
                case DoryMendReason.LowWaterRequired: return "dory_mend.wait_for_low_water";
                case DoryMendReason.ClockUnavailable: return "dory_mend.clock_unavailable";
                case DoryMendReason.NotStarted: return "dory_mend.start_or_resume";
                case DoryMendReason.InvalidWorkDuration: return "dory_mend.invalid_duration";
                case DoryMendReason.InvalidProgress: return "dory_mend.invalid_progress";
                case DoryMendReason.NotRepaired: return "dory_mend.finish_repairs";
                case DoryMendReason.HighWaterRequired: return "dory_mend.wait_for_high_water";
                case DoryMendReason.SupportedLaunchRequired: return "dory_mend.bring_hull_to_water";
                default: throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }
    }

    /// <summary>Session state for an unfinished job. This is not a save record and cannot be imported
    /// as authority to receive materials or complete work. Finished jobs use existing saved flags.</summary>
    public readonly struct DoryMendingSnapshot
    {
        public readonly string BoatId;
        public readonly string JobId;
        public readonly double WorkedSeconds;
        public readonly bool MaterialsReserved;
        public readonly bool Working;
        public DoryMendingSnapshot(string boatId, string jobId, double workedSeconds,
                                  bool materialsReserved, bool working)
        {
            BoatId = boatId; JobId = jobId; WorkedSeconds = workedSeconds;
            MaterialsReserved = materialsReserved; Working = working;
        }
    }

    public readonly struct DoryMendingJobStarted
    {
        public readonly string BoatId, JobId;
        public readonly double GameSeconds;
        public DoryMendingJobStarted(string boatId, string jobId, double seconds)
        { BoatId = boatId; JobId = jobId; GameSeconds = seconds; }
    }

    public readonly struct DoryMendingJobDone
    {
        public readonly string BoatId, JobId;
        public readonly double GameSeconds;
        public DoryMendingJobDone(string boatId, string jobId, double seconds)
        { BoatId = boatId; JobId = jobId; GameSeconds = seconds; }
    }

    public readonly struct DoryMended
    {
        public readonly string BoatId;
        public readonly double GameSeconds;
        public DoryMended(string boatId, double seconds) { BoatId = boatId; GameSeconds = seconds; }
    }

    public readonly struct DoryReadyToLaunch
    {
        public readonly string BoatId;
        public readonly double GameSeconds;
        public DoryReadyToLaunch(string boatId, double seconds) { BoatId = boatId; GameSeconds = seconds; }
    }
}
