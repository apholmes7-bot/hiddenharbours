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

    /// <summary>Only the goods physically carried to the slip. Implementations must consume the whole
    /// request atomically, or return false without changing anything. Remote storage is excluded.</summary>
    public interface IMendingCarry
    {
        int Count(string itemId);
        bool TryConsume(IReadOnlyList<MendingMaterial> materials);
    }

    public enum DoryMendReason
    {
        None, Busy, SessionChanged, WrongPlace, BoatNotOwned, BoatAlreadyRepaired,
        UnknownJob, EarlierJobsIncomplete, JobAlreadyDone, JobInProgress, MissingMaterials,
        LowWaterRequired, ClockUnavailable, NotStarted, InvalidWorkDuration, InvalidProgress,
        NotRepaired, HighWaterRequired
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
        public readonly bool MaterialsCommitted;
        public readonly bool Working;
        public DoryMendingSnapshot(string boatId, string jobId, double workedSeconds,
                                  bool materialsCommitted, bool working)
        {
            BoatId = boatId; JobId = jobId; WorkedSeconds = workedSeconds;
            MaterialsCommitted = materialsCommitted; Working = working;
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
