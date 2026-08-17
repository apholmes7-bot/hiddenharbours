using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>THE DOOR OF A HOME YOU MIGHT OWN</b> — walk up, press the key, and one of four honest things
    /// happens: you buy it, you are told what it costs, you are told the inside is not built yet, or you
    /// go in. Today the fourth never happens and the third always does, and that is a stated fact about
    /// the art, not a bug (see <see cref="InteriorStood"/>).
    ///
    /// <para><b>The buy seam is <see cref="LicenseVendor"/>'s, deliberately.</b> Check the price, spend
    /// through <see cref="IWallet"/> (atomic — money only leaves on success), and on success record the
    /// deed through Core's <see cref="HomeDeeds"/> and publish. Nothing here reaches into the world or
    /// the player; the world reads ownership back through Core (rule 4).</para>
    ///
    /// <para><b>No HUD, no screen, no prompt chrome.</b> The diegetic-UI principles say information is
    /// earned and the interface is physical: you find out what a home costs by walking to its door and
    /// trying it, the way you find out what the freezer holds by opening it. Feedback rides
    /// <see cref="DevNotice"/> like every other proximity interactable in the greybox; when ui-ux owns
    /// the real Interact mapping (§7.6) this component keeps its seam and loses its key.</para>
    ///
    /// <para>Proximity + on-foot gating is the stall pattern (<see cref="StallReach"/> — no builder
    /// wiring for the player ref), the same one <c>GinnyFreezer</c> uses two dozen metres away. New
    /// Input System only.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeDoor : MonoBehaviour
    {
        [Tooltip("Which home this door belongs to — its id, its price, and whether it is bought at all.")]
        [SerializeField] private HomeDef _home;

        [Tooltip("Interact key (shared with the other proximity interactables — their ranges don't " +
                 "overlap, the WorldInteractor precedent). ui-ux owns the real Interact mapping later.")]
        [SerializeField] private Key _interactKey = Key.F;

        [Tooltip("On-foot + in-range gate (the DevSellInput stall pattern).")]
        [SerializeField] private StallReach _reach = new StallReach();

        [Tooltip("Has a room actually been stood inside this shell? FALSE until the interior rig lands " +
                 "and is baked — an owned home with no inside refuses entry rather than pretending.")]
        [SerializeField] private bool _interiorStood;

        private IWallet _wallet;

        /// <summary>The home this door belongs to. Null until configured.</summary>
        public HomeDef Home => _home;

        /// <summary>
        /// Whether there is anything to walk into.
        ///
        /// <para><b>⚠️ FALSE for the camper, and it is the drop that says so, not a decision here.</b> The
        /// camper kit's own README reads <i>"No interior. This is the shell."</i> — <c>camperInteriorRig.js</c>
        /// is named in it but is not in the zip. So the camper can be placed, priced, bought and walked up
        /// to, and cannot be walked INTO. That is a first-class state in this component rather than a
        /// missing feature: the door is present and it answers honestly. The seamless-interiors ruling
        /// (2026-07-30) still holds for the day the rig arrives — wiring it is that PR's job, and this
        /// flag is where it lands.</para>
        /// </summary>
        public bool InteriorStood => _interiorStood;

        /// <summary>True iff the player already holds this home's deed.</summary>
        public bool IsOwned => _home != null && HomeDeeds.IsOwned(_home.Id);

        /// <summary>
        /// Is the on-foot player standing close enough to try this door? The gate <see cref="Update"/>
        /// applies to a key press, exposed because <see cref="Interact"/> deliberately does NOT gate —
        /// the DECISION is unconditional so a test (or a future UI) can drive it, and the REACH is what
        /// the frame loop puts in front of it.
        ///
        /// <para>Public because it is otherwise untestable: a virtual key press is not deliverable to an
        /// unfocused headless editor (three PlayMode classes in this repo self-ignore for it), so a test
        /// that drives the keyboard proves nothing about the gate on this machine. This is the seam that
        /// lets the proximity rule be checked without one.</para>
        /// </summary>
        public bool PlayerIsAtTheDoor => _reach.CanInteract(transform.position);

        /// <summary>What happened on the last <see cref="Interact"/> — for tests and for a future
        /// UI to bind without re-deriving the decision.</summary>
        public Outcome LastOutcome { get; private set; }

        /// <summary>The four honest answers a home's door can give.</summary>
        public enum Outcome
        {
            /// <summary>Nothing was asked yet, or the door has no home configured.</summary>
            None = 0,
            /// <summary>The deed changed hands just now.</summary>
            Bought,
            /// <summary>Not enough money — nothing was charged.</summary>
            CannotAfford,
            /// <summary>Yours, but there is no inside to go into yet.</summary>
            OwnedButNotEnterable,
            /// <summary>Yours, and you went in.</summary>
            Entered,
        }

        /// <summary>
        /// Wire this door from a builder: which home, and whether a room was stood inside it. Public
        /// because EditMode does not run <c>OnEnable</c> for an <c>AddComponent</c>, so a fixture drives
        /// the same path the builder does.
        /// </summary>
        public void Configure(HomeDef home, bool interiorStood)
        {
            _home = home;
            _interiorStood = interiorStood;
        }

        private void OnEnable() => _reach.Enable();

        private void OnDisable() => _reach.Disable();

        private void Update()
        {
            var kb = Keyboard.current;   // New Input System only
            if (kb == null || !kb[_interactKey].wasPressedThisFrame) return;
            if (!_reach.CanInteract(transform.position)) return;
            Interact();
        }

        /// <summary>
        /// Try the door. Public so tests drive it without input.
        ///
        /// <para>Order matters and is the vendor's: ownership is checked BEFORE money, so a door you
        /// already own can never charge you twice — the <see cref="LicenseVendor"/> double-charge guard,
        /// which matters more here because a home costs a season's fishing.</para>
        /// </summary>
        public Outcome Interact()
        {
            LastOutcome = Outcome.None;
            if (_home == null)
            {
                Debug.LogWarning("[HomeDoor] No home configured — this door belongs to nothing.", this);
                return LastOutcome;
            }
            if (string.IsNullOrEmpty(_home.Id))
            {
                Debug.LogWarning($"[HomeDoor] '{_home.name}' has no id, so no deed can be recorded for it.",
                                 this);
                return LastOutcome;
            }

            if (!IsOwned)
            {
                // A starter home should already have been granted when the region was placed; if we are
                // here it was flipped mid-session, so honour it for free rather than charging for
                // something the owner declared free.
                if (_home.Acquisition == HomeDef.Mode.StarterHome)
                {
                    HomeDeeds.Grant(_home.Id);
                    EventBus.Publish(new HomePurchased(_home.Id, 0));
                    EventBus.Publish(new DevNotice($"{_home.DisplayName} is yours."));
                    return LastOutcome = Outcome.Bought;
                }

                _wallet ??= GameServices.Wallet;
                if (_wallet == null)
                {
                    Debug.LogWarning("[HomeDoor] No wallet to pay from.", this);
                    return LastOutcome;
                }

                if (!_wallet.TrySpend(_home.Price))
                {
                    EventBus.Publish(new DevNotice(
                        $"{_home.DisplayName}: ₲{_home.Price}. You have ₲{_wallet.Money}."));
                    return LastOutcome = Outcome.CannotAfford;
                }

                HomeDeeds.Grant(_home.Id);
                EventBus.Publish(new HomePurchased(_home.Id, _home.Price));
                EventBus.Publish(new DevNotice(
                    $"{_home.DisplayName} is yours — ₲{_home.Price}. Somewhere of your own at last."));
                return LastOutcome = Outcome.Bought;
            }

            // ---- owned. Can you get in? -------------------------------------------------------------
            if (!_interiorStood)
            {
                // The honest line. NOT "the door is locked" — it is your door and it is not locked; there
                // is simply nothing built behind it yet, and saying so is better than a lie the player
                // will spend ten minutes trying to solve.
                EventBus.Publish(new DevNotice(
                    "Your key turns, but the inside isn't finished — bare shell, no floor down yet."));
                return LastOutcome = Outcome.OwnedButNotEnterable;
            }

            return LastOutcome = Outcome.Entered;
        }
    }
}
