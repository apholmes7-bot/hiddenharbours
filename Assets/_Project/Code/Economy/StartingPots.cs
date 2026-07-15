using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Grants the COZY POT STARTER KIT once per game — the flip side of pots becoming bought, finite
    /// stock (ADR 0020 addendum): a NEW game starts with a couple of pots so the trap loop is playable
    /// before the first shipwright visit, and an EXISTING save (which could set pots freely before the
    /// update) gets the same kit on its first load after — so nobody is ever stranded potless mid-loop.
    /// Every further pot is bought (<see cref="PotShop"/>) — that's the P2 money wheel.
    ///
    /// <para>Mirrors the established <c>StartingGear</c>/<c>StartingBait</c> grant pattern exactly:
    /// flag-guarded (once per game, persisted), additive (never strips or caps what the player already
    /// owns), null-safe (no save → no-op, retried next Start). The COUNTS live on
    /// <see cref="GameConfig.StarterPotKit"/> — owner-tunable data on the config asset, no code and no
    /// scene rebuild to retune (rule 6). Writes only the Core <see cref="SaveData"/> (rule 4).</para>
    /// </summary>
    public sealed class StartingPots : MonoBehaviour
    {
        [Tooltip("The shared GameConfig whose StarterPotKit defines what this grant gives (owner-tunable " +
                 "data). No config wired → nothing granted (and the flag stays unset, so a later fix " +
                 "still grants).")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Save flag marking the one-time grant as done, so it isn't re-run on every scene load. " +
                 "Stable, append-only key.")]
        [SerializeField] private string _grantedFlagKey = "pot_starter_kit_granted";

        private void Start() => GrantOnce();

        /// <summary>Grant the starter pot kit into the live save once. Public + returns the number of
        /// kit entries applied so EditMode tests can drive it without the scene lifecycle. Re-granting
        /// is a no-op (the persisted flag). Nothing to grant (no config / empty kit) leaves the flag
        /// UNSET so a later wiring fix still delivers the kit.</summary>
        public int GrantOnce()
        {
            ISaveService saver = GameServices.Save;
            SaveData save = saver?.Current;
            if (save == null) return 0;                                   // no save → nothing to grant into

            if (!string.IsNullOrEmpty(_grantedFlagKey) && saver.GetFlag(_grantedFlagKey))
                return 0;                                                 // already granted this game

            int granted = Grant(save, _config != null ? _config.StarterPotKit : null);
            if (granted <= 0) return 0;                                   // nothing granted → stay armed

            if (!string.IsNullOrEmpty(_grantedFlagKey)) saver.SetFlag(_grantedFlagKey, true);   // persists
            else saver.Save();
            return granted;
        }

        /// <summary>Pure grant into a save blob (testable): add each kit entry's count to the owned pot
        /// stock (<see cref="PotLocker.AddOwned"/> — additive, merged per kind). Returns how many
        /// entries actually granted.</summary>
        public static int Grant(SaveData save, PotStarterEntry[] kit)
        {
            if (save == null || kit == null) return 0;
            int granted = 0;
            for (int i = 0; i < kit.Length; i++)
            {
                if (string.IsNullOrEmpty(kit[i].TrapDefId) || kit[i].Count <= 0) continue;
                PotLocker.AddOwned(save, kit[i].TrapDefId, kit[i].Count);
                granted++;
            }
            return granted;
        }

        /// <summary>Wire the grant in one call (tests / editor).</summary>
        public void Configure(GameConfig config, string grantedFlagKey)
        {
            _config = config;
            _grantedFlagKey = grantedFlagKey;
        }
    }
}
