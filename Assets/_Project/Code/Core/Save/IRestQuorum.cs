using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>Decides whether every participant is lying in a distinct bunk, from one current snapshot.</summary>
    public interface IRestQuorum
    {
        RestDecision Evaluate(IReadOnlyList<RestParticipant> players);
    }

    /// <summary>
    /// Session-only occupancy. BunkId is a globally unique physical berth id, including its boat or
    /// building instance. Null or empty means standing. An authored wheelhouse bench is a bunk too.
    /// RegionId is descriptive: players do not have to sleep in the same region.
    /// </summary>
    public readonly struct RestParticipant
    {
        public readonly int PlayerId;
        public readonly string BunkId;
        public readonly string RegionId;

        public RestParticipant(int playerId, string bunkId, string regionId = null)
        {
            PlayerId = playerId;
            BunkId = bunkId;
            RegionId = regionId;
        }
    }

    /// <summary>A decision and a text key for its caller; evaluating it publishes no presentation event.</summary>
    public readonly struct RestDecision
    {
        public readonly bool MayPassNight;
        public readonly string ReasonKey;

        internal RestDecision(bool mayPassNight, string reasonKey)
        {
            MayPassNight = mayPassNight;
            ReasonKey = reasonKey;
        }
    }
}
