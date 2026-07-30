using UnityEngine;
using UnityEngine.Events;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Fronts the player a licence fee ONCE per game — the mechanism behind the Aunt Ginny beat that makes
    /// the clam licence buyable on day one (plan-to-m1 §7.5).
    ///
    /// <para><b>The deadlock this exists to prevent.</b> <see cref="CatchLicensePolicy.MayLand"/> fails
    /// CLOSED: a species that some authored licence gates cannot be landed without holding it. The clam
    /// licence gates clams, and clams are the player's only income on day one — so "buy the clam licence with
    /// clam money" is a hard soft-lock the moment the dig starts consulting the gate. §7.5 rules the fix as a
    /// character beat rather than a mechanic: Ginny fronts the fee, and the player still walks to the store
    /// and buys the licence <i>themselves</i> — they meet the vendor, they learn that licences gate species,
    /// and it plants a small warm debt to pay back. This class is only the mechanism half.</para>
    ///
    /// <para><b>Her words are NOT here.</b> world-content owns Ginny's dialogue and where she stands. This
    /// exposes two clean hooks and writes no prose: call <see cref="GrantOnce"/> from her dialogue's
    /// completion (the moment the beat actually lands), and/or bind <see cref="_onFronted"/> in the inspector
    /// for a toast. A <c>FeeFronted</c> signal goes out on the EventBus for anyone who would rather subscribe
    /// than be wired (rule 4 — no cross-module references either way).</para>
    ///
    /// <para><b>The trigger, and why this one.</b> Mirrors <see cref="StartingPots"/> exactly — the
    /// established one-time-grant pattern in this codebase (itself following StartingGear/StartingBait):
    /// flag-guarded via <see cref="ISaveService"/>'s flag store (so it needs NO save-schema field of its
    /// own), additive (never caps or strips what the player already has), null-safe (no save/wallet → no-op,
    /// retried next Start). <c>Start()</c> auto-grants as the SAFETY NET, so a build always clears the
    /// deadlock even before the dialogue lane has wired anything; once Ginny's scene calls
    /// <see cref="GrantOnce"/> first, the flag makes the Start call a no-op and the beat belongs to her.</para>
    /// </summary>
    public sealed class FrontedFeeGrant : MonoBehaviour
    {
        [Tooltip("The shared GameConfig whose FrontedLicenceFee sets how much is fronted (owner-tunable " +
                 "data). No config wired → nothing granted (and the flag stays unset, so a later fix " +
                 "still grants).")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Save flag marking the one-time grant as done, so it isn't re-run on every scene load. " +
                 "Stable, append-only key.")]
        [SerializeField] private string _grantedFlagKey = DefaultGrantKey;

        [Tooltip("Inspector hook fired with the ₲ fronted, the moment it lands. world-content binds this " +
                 "(or calls GrantOnce from Ginny's dialogue) — no dialogue is written here.")]
        [SerializeField] private UnityEvent<int> _onFronted;

        /// <summary>The stable, append-only default flag key for the fronted clam-licence fee.</summary>
        public const string DefaultGrantKey = "ginny_fronted_clam_fee";

        // Deferred to the moment the loaded save is authoritative (M1 §7.8's shell) — at the title the
        // blob is still the outgoing game's, so its grant flag says nothing about the new one. It also
        // means the money lands in a wallet the restore has already brought to its saved balance.
        private void Start() => SaveReady.Run(this, () => GrantOnce());

        /// <summary>
        /// Front the fee into the live wallet once. Returns the ₲ actually granted (0 if it had already been
        /// fronted, or there was nothing to grant into). Public + returning so EditMode tests can drive it
        /// without the scene lifecycle, and so Ginny's dialogue can call it at the exact beat. Re-granting is
        /// a no-op (the persisted flag). Nothing granted (no config, or an amount of 0) leaves the flag UNSET
        /// so a later wiring fix still delivers it — the <see cref="StartingPots"/> rule.
        /// </summary>
        public int GrantOnce()
        {
            ISaveService saver = GameServices.Save;
            if (saver?.Current == null) return 0;                          // no save → nothing to grant into

            if (!string.IsNullOrEmpty(_grantedFlagKey) && saver.GetFlag(_grantedFlagKey))
                return 0;                                                  // already fronted this game

            int amount = _config != null ? _config.FrontedLicenceFee : 0;
            if (!Grant(GameServices.Wallet, amount)) return 0;             // nothing granted → stay armed

            if (!string.IsNullOrEmpty(_grantedFlagKey)) saver.SetFlag(_grantedFlagKey, true);   // persists
            else saver.Save();

            EventBus.Publish(new FeeFronted(_grantedFlagKey, amount));
            Debug.Log($"[FrontedFeeGrant] Fronted ₲{amount} ({_grantedFlagKey}).");
            _onFronted?.Invoke(amount);
            return amount;
        }

        /// <summary>Pure grant into a wallet (testable): credit <paramref name="amount"/> if there is a
        /// wallet and the amount is positive. Returns whether anything was actually granted.</summary>
        public static bool Grant(IWallet wallet, int amount)
        {
            if (wallet == null || amount <= 0) return false;
            wallet.Add(amount);
            return true;
        }

        /// <summary>Wire the grant in one call (tests / editor).</summary>
        public void Configure(GameConfig config, string grantedFlagKey)
        {
            _config = config;
            _grantedFlagKey = grantedFlagKey;
        }
    }
}
