using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>A pure decision over all participants, with stable refusal precedence regardless of roster order.</summary>
    public sealed class RestQuorumRule : IRestQuorum
    {
        public const string NoPlayers = "sleep.no_players";
        public const string RepeatedPlayer = "sleep.repeated_player";
        public const string PlayerNotInBunk = "sleep.player_not_in_bunk";
        public const string SharedBunk = "sleep.shared_bunk";

        public RestDecision Evaluate(IReadOnlyList<RestParticipant> players)
        {
            if (players == null || players.Count == 0) return new RestDecision(false, NoPlayers);
            var identities = new HashSet<int>();
            var bunks = new HashSet<string>(StringComparer.Ordinal);
            bool repeated = false, standing = false, shared = false;
            for (int i = 0; i < players.Count; i++)
            {
                RestParticipant player = players[i];
                if (!identities.Add(player.PlayerId)) repeated = true;
                if (string.IsNullOrWhiteSpace(player.BunkId)) standing = true;
                else if (!bunks.Add(player.BunkId)) shared = true;
            }
            if (repeated) return new RestDecision(false, RepeatedPlayer);
            if (standing) return new RestDecision(false, PlayerNotInBunk);
            if (shared) return new RestDecision(false, SharedBunk);
            return new RestDecision(true, string.Empty);
        }
    }

    /// <summary>
    /// <b>Who has to be asleep before the night moves, and one sleeper per bed</b> (owner ruling
    /// 2026-11-15, "player/s able to cancel"). Multiplayer is NOT built; this is the seam in front of the
    /// rest flow so that, when it is, the question "may time run fast now?" already has one place to be
    /// asked. Today there is one player, so the quorum is one of one and every rule here reduces to the
    /// single-player behaviour exactly.
    ///
    /// <para><b>One bed, one occupant.</b> A bed or bunk is claimed by its id when a sleeper lies down
    /// and released on waking or getting up. A second sleeper asking for a claimed bed is refused. A rest
    /// that names no bed (an old request, a test rig) claims nothing and is never refused.</para>
    /// </summary>
    public static class RestQuorum
    {
        /// <summary>The local player's sleeper number. The only sleeper there is today.</summary>
        public const int LocalSleeper = 0;

        /// <summary>The legacy local default. Supplied rosters are evaluated by IRestQuorum instead.</summary>
        public static int Players => 1;

        /// <summary>Legacy count check. Counts cannot prove distinct bunks; the sleep flow uses EvaluateForRest.</summary>
        public static bool IsMet(int sleepers, int players) => players > 0 && sleepers >= players;

        /// <summary><see cref="IsMet(int, int)"/> against today's <see cref="Players"/>.</summary>
        public static bool IsMet(int sleepers) => IsMet(sleepers, Players);

        private static readonly Dictionary<string, int> Occupants = new Dictionary<string, int>(StringComparer.Ordinal);

        private static readonly IRestQuorum Rule = new RestQuorumRule();
        private const string LegacyLocalBunk = "rest.legacy_local";

        /// <summary>
        /// Optional session input: return every participating player, including anyone standing.
        /// Called afresh at confirmation and before each clock advance. Returning null refuses sleep;
        /// leaving this unset preserves the one-player rest adapter. Clear it when the session ends.
        /// This supplies occupancy only; it does not create actors or write their state to the save.
        /// </summary>
        public static Func<IReadOnlyList<RestParticipant>> ParticipantSource { get; set; }

        /// <summary>
        /// A detached session snapshot. The unnamed single-player rest request remains a valid rest;
        /// the compatibility id never enters a save or a supplied multi-player roster.
        /// </summary>
        public static RestParticipant[] SnapshotFor(string localBedId, bool requireLocalClaim = false)
        {
            if (ParticipantSource != null)
            {
                IReadOnlyList<RestParticipant> players = ParticipantSource();
                if (players == null) return Array.Empty<RestParticipant>();
                var snapshot = new RestParticipant[players.Count];
                for (int i = 0; i < snapshot.Length; i++) snapshot[i] = players[i];
                return snapshot;
            }

            string bunk = string.IsNullOrEmpty(localBedId) ? LegacyLocalBunk : localBedId;
            if (requireLocalClaim && !string.IsNullOrEmpty(localBedId) &&
                (!Occupants.TryGetValue(localBedId, out int occupant) || occupant != LocalSleeper))
                bunk = null;
            return new[] { new RestParticipant(LocalSleeper, bunk) };
        }

        public static RestDecision EvaluateForRest(string localBedId, bool requireLocalClaim = false) =>
            Rule.Evaluate(SnapshotFor(localBedId, requireLocalClaim));

        /// <summary>
        /// Lie <paramref name="sleeper"/> down in <paramref name="bedId"/>. True if the bed was free or is
        /// already theirs; false if someone else is in it. An empty id claims nothing and answers true.
        /// </summary>
        public static bool TryClaim(string bedId, int sleeper)
        {
            if (string.IsNullOrEmpty(bedId)) return true;
            if (Occupants.TryGetValue(bedId, out int occupant)) return occupant == sleeper;
            Occupants[bedId] = sleeper;
            return true;
        }

        /// <summary>Get <paramref name="sleeper"/> up out of <paramref name="bedId"/>. Releasing a bed that
        /// is someone else's, or nobody's, changes nothing.</summary>
        public static void Release(string bedId, int sleeper)
        {
            if (string.IsNullOrEmpty(bedId)) return;
            if (Occupants.TryGetValue(bedId, out int occupant) && occupant == sleeper) Occupants.Remove(bedId);
        }

        /// <summary>Is <paramref name="bedId"/> taken?</summary>
        public static bool IsOccupied(string bedId) => !string.IsNullOrEmpty(bedId) && Occupants.ContainsKey(bedId);

        /// <summary>Everyone up (a teardown, a quit to the title, a test).</summary>
        public static void Reset()
        {
            Occupants.Clear();
            ParticipantSource = null;
        }
    }
}
