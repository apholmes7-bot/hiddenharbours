using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// Grants the player's <b>starting equipment</b> on the St Peters opening — the clam shovel and the
    /// clam bucket — as <em>starting gear, not a purchase</em> (the rod is bought later at Greywick). You
    /// wake on St Peters already able to dig: the shovel enables digging, the bucket is your on-foot clam
    /// hold (<see cref="ClamBucket"/>). It writes the gear ids into the save's owned-gear list so the
    /// capability (<see cref="PlayerGear"/>) and the persisted record agree, then it's done.
    ///
    /// <para><b>Idempotent &amp; persisted.</b> Granting is additive and de-duplicated, so re-entering the
    /// scene or loading a save never double-grants and never strips gear the player later bought. A guard
    /// flag in the save makes the grant a once-per-game event (so it won't re-add starting gear the player
    /// deliberately... well, gear can't be dropped yet — the flag is future-proofing + avoids redundant
    /// writes). Null-safe: no save wired (EditMode/pre-boot) → no-op.</para>
    ///
    /// <para><b>Data, not code.</b> The starting ids are a serialized list (default shovel + bucket), so the
    /// opening's kit is editable without code and matches the authored GearOffer ids
    /// (<see cref="PlayerGear"/>). Cross-module-clean: writes only the Core <see cref="SaveData"/> — Economy
    /// isn't involved (this is a grant, not a sale).</para>
    /// </summary>
    public class StartingGear : MonoBehaviour
    {
        [Tooltip("Gear ids the player starts the St Peters opening already owning (NOT purchased). Defaults " +
                 "to the clam shovel + bucket. Match the authored GearOffer ids (gear.shovel / gear.bucket).")]
        [SerializeField] private string[] _startingGearIds = { PlayerGear.ShovelId, PlayerGear.BucketId };

        [Tooltip("Save flag that marks the one-time starting-gear grant as done, so it isn't re-run on " +
                 "every scene load. Stable, append-only key.")]
        [SerializeField] private string _grantedFlagKey = "st_peters_starting_gear_granted";

        private void Start() => GrantOnce();

        /// <summary>
        /// Grant the starting gear into the live save once. Public + returns the count newly granted so
        /// EditMode tests can drive it without the scene lifecycle. Re-granting is a no-op.
        /// </summary>
        public int GrantOnce()
        {
            ISaveService saver = GameServices.Save;
            SaveData save = saver?.Current;
            if (save == null) return 0;                                  // no save → nothing to grant into

            if (!string.IsNullOrEmpty(_grantedFlagKey) && saver.GetFlag(_grantedFlagKey))
                return 0;                                               // already granted this game

            int granted = Grant(save, _startingGearIds);

            if (!string.IsNullOrEmpty(_grantedFlagKey)) saver.SetFlag(_grantedFlagKey, true); // persists
            else if (granted > 0) saver.Save();
            return granted;
        }

        /// <summary>Pure grant into a save blob (testable): add each id to OwnedGear if absent. Returns how
        /// many were newly added.</summary>
        public static int Grant(SaveData save, IReadOnlyList<string> gearIds)
        {
            if (save == null || gearIds == null) return 0;
            save.OwnedGear ??= new List<string>();
            int added = 0;
            for (int i = 0; i < gearIds.Count; i++)
            {
                string id = gearIds[i];
                if (string.IsNullOrEmpty(id) || save.OwnedGear.Contains(id)) continue;
                save.OwnedGear.Add(id);
                added++;
            }
            return added;
        }

        /// <summary>Wire the starting ids in one call (tests / editor).</summary>
        public void Configure(string[] startingGearIds, string grantedFlagKey)
        {
            _startingGearIds = startingGearIds;
            _grantedFlagKey = grantedFlagKey;
        }
    }
}
