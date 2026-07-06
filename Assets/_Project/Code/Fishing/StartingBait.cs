using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// GREYBOX dev-grant of a small starting <b>bait</b> stock (trap-fishing arc Build 4), so the manual
    /// trap loop (set → soak → haul) is playable end-to-end NOW — before the economy sells bait. Mirrors the
    /// Player lane's <c>StartingGear</c> (the shovel/bucket grant): on <c>Start</c> it seeds a few of each
    /// listed bait into the save's <see cref="SaveData.BaitStock"/>, once per game.
    ///
    /// <para><b>Explicitly a placeholder.</b> Real trap/bait acquisition is a later ECONOMY offer (a
    /// Shipwright / gear sale) — this only exists so the owner can feel the FUN of the haul this build. It is
    /// additive + guarded so re-entering the scene / loading never double-grants. Null-safe: no save wired
    /// (EditMode / pre-boot) → no-op. Writes only the Core <see cref="SaveData"/> (rule 4).</para>
    /// </summary>
    public sealed class StartingBait : MonoBehaviour
    {
        [System.Serializable]
        public struct BaitGrant
        {
            [Tooltip("Bait Def id to grant (e.g. bait.herring). Must match an authored BaitDef.")]
            public string BaitId;
            [Min(0)][Tooltip("How many of this bait to seed at start.")]
            public int Count;
        }

        [Tooltip("Bait stock the player starts this build with (greybox dev grant). Defaults to a handful of " +
                 "herring for the lobster pot. Real acquisition is a later economy offer.")]
        [SerializeField] private BaitGrant[] _startingBait = { new BaitGrant { BaitId = "bait.herring", Count = 6 } };

        [Tooltip("Save flag marking the one-time grant as done, so it isn't re-run on every scene load. " +
                 "Stable, append-only key.")]
        [SerializeField] private string _grantedFlagKey = "trap_starting_bait_granted";

        private void Start() => GrantOnce();

        /// <summary>Seed the starting bait into the live save once. Public + returns the number of records
        /// touched so EditMode tests can drive it without the scene lifecycle. Re-granting is a no-op.</summary>
        public int GrantOnce()
        {
            ISaveService saver = GameServices.Save;
            SaveData save = saver?.Current;
            if (save == null) return 0;

            if (!string.IsNullOrEmpty(_grantedFlagKey) && saver.GetFlag(_grantedFlagKey)) return 0;

            int touched = Grant(save, _startingBait);

            if (!string.IsNullOrEmpty(_grantedFlagKey)) saver.SetFlag(_grantedFlagKey, true);
            else if (touched > 0) saver.Save();
            return touched;
        }

        /// <summary>Pure grant into a save blob (testable): add/increment each bait's stock. Returns how many
        /// records were touched.</summary>
        public static int Grant(SaveData save, IReadOnlyList<BaitGrant> grants)
        {
            if (save == null || grants == null) return 0;
            save.BaitStock ??= new List<BaitStock>();
            int touched = 0;
            for (int g = 0; g < grants.Count; g++)
            {
                BaitGrant grant = grants[g];
                if (string.IsNullOrEmpty(grant.BaitId) || grant.Count <= 0) continue;

                bool merged = false;
                for (int i = 0; i < save.BaitStock.Count; i++)
                {
                    if (save.BaitStock[i].BaitId == grant.BaitId)
                    {
                        save.BaitStock[i] = new BaitStock(grant.BaitId, save.BaitStock[i].Count + grant.Count);
                        merged = true;
                        break;
                    }
                }
                if (!merged) save.BaitStock.Add(new BaitStock(grant.BaitId, grant.Count));
                touched++;
            }
            return touched;
        }

        /// <summary>Wire the starting bait in one call (tests / editor).</summary>
        public void Configure(BaitGrant[] startingBait, string grantedFlagKey)
        {
            _startingBait = startingBait;
            _grantedFlagKey = grantedFlagKey;
        }
    }
}
