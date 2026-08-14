using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The on-foot <b>clam bucket</b> — the player's hand-carried hold before they have a boat (St Peters
    /// opening). Implements the Core <see cref="IHold"/> contract exactly like the boat's <c>ShipHold</c>,
    /// so the clam-dig interaction fills it and the Nine Mile Creek stall empties it through the same seam — no
    /// special-casing an "on-foot hold". It holds up to <see cref="Capacity"/> clams (the design's 20-clam
    /// pail); when full, you head to Nine Mile Creek to sell.
    ///
    /// <para><b>Owned-gear gated.</b> The bucket is a capability granted by owning <c>gear.bucket</c>
    /// (<see cref="PlayerGear.HasBucket()"/>, starting gear on St Peters). Until owned its capacity reads as
    /// 0, so nothing can be stowed — you can't dig into a bucket you don't have. The dig interaction also
    /// checks ownership, so this is belt-and-braces.</para>
    ///
    /// <para><b>Tunable, not a magic number.</b> The 20-unit cap is a serialized field (the bucket's hold
    /// size, the gameplay-systems mechanic the GearOffer flavour defers to us), editable in the inspector,
    /// not a literal buried in logic.</para>
    /// </summary>
    public class ClamBucket : MonoBehaviour, IHold, IInteractable
    {
        [Tooltip("How many clams the pail holds (the design's 20-clam bucket). The gameplay-systems hold " +
                 "mechanic the gear.bucket GearOffer defers to — a tunable, not a hard-coded number.")]
        [SerializeField] private int _capacity = 20;

        [Tooltip("If true, the bucket only has capacity once the player owns gear.bucket (PlayerGear). " +
                 "Leave on for the real opening; tests/tools can turn it off to use the bucket unconditionally.")]
        [SerializeField] private bool _requireOwnedBucket = true;

        private readonly List<CatchItem> _items = new();

        private void Awake()
        {
            // The dump verb rides in with the player's hand-carried hold (M1 §7.3): one global
            // CatchDumpInput per scene, runtime-spawned so no builder re-run is needed — the same
            // self-install pattern ShipHold uses for its DeckContainerPresenter. Play mode only
            // (EditMode tests add ClamBuckets freely and must stay input-free).
            if (Application.isPlaying && FindAnyObjectByType<CatchDumpInput>() == null)
                gameObject.AddComponent<CatchDumpInput>();
        }

        /// <summary>The pail's clam capacity (0 when the player doesn't own a bucket and ownership is required).</summary>
        public int Capacity => _capacity;

        public int CapacityUnits => (_requireOwnedBucket && !PlayerGear.HasBucket()) ? 0 : _capacity;
        public int UsedUnits => _items.Count;
        public IReadOnlyList<CatchItem> Items => _items;

        public bool TryAdd(CatchItem item)
        {
            if (_items.Count >= CapacityUnits) return false;
            _items.Add(item);
            SyncToSave();
            return true;
        }

        public void Clear()
        {
            _items.Clear();
            SyncToSave();
        }

        // The pail's catch persists (M1 §7.3, SaveData v5) — the ShipHold pattern: mutations push a
        // snapshot into the save blob (disk stays SaveService's autosave); GameLoaded restores it, so
        // a bucket of clams reloads into your hand with its freshness exact. Restore writes _items
        // directly (the saved pail was legal; the ownership gate must not eat a reloaded catch).
        private void OnEnable()
        {
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
            Interactables.Register(this);      // the pail is now something E can act on (see below)
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
            Interactables.Unregister(this);
        }

        private void OnGameLoaded(GameLoaded _)
        {
            ISaveService save = GameServices.Save;
            if (save == null || save.Current == null) return;
            HoldCatchCodec.Restore(save.Current.BucketCatch, GameServices.CatchFactory, _items);
        }

        private void SyncToSave()
        {
            ISaveService save = GameServices.Save;
            if (save == null || save.Current == null) return;
            HoldCatchCodec.Capture(_items, save.Current.BucketCatch ??= new List<HeldCatchDto>());
        }

        /// <summary>Wire capacity/ownership in one call (tests / editor).</summary>
        public void Configure(int capacity, bool requireOwnedBucket)
        {
            _capacity = capacity;
            _requireOwnedBucket = requireOwnedBucket;
        }

        // ---- THE CONTAINER YOU STACK INTO (the owner's ruling, 2026-08-13) ---------------------------
        //
        // "A clam can be in hand but needs to be placed in a container to stack." The pail is that
        // container, and it answers the same one interact verb everything else does: hold a clam, press E,
        // it goes in the pail and your hands are free (and the shovel comes back off the sling).
        //
        // ⚠️ THE BUCKET RIDES THE PLAYER, so it is always in reach — see ReachMeters. That is honest for
        // now (the pail is on her belt, not standing on the wharf) and it is exactly the thing that
        // changes when the bucket becomes a placeable world object: this component gets a transform of its
        // own and NOTHING else here has to move.

        [Tooltip("Stable INSTANCE id for the interact registry — the resolver's LAST tie-break. One " +
                 "bucket per player, so a constant is safe here in a way it would not be for a can.")]
        [SerializeField] private string _interactId = "container.player.clam_bucket";

        /// <inheritdoc/>
        public string Id => _interactId;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <summary>On her belt: always within arm's length, because it IS on the arm. Stated as a real
        /// number rather than infinity so the resolver's distance comparison stays ordinary maths.</summary>
        public float ReachMeters => 4f;

        /// <summary>
        /// <see cref="InteractPriority.ToolTarget"/> — the container is a work-site for the thing in your
        /// hands, exactly as a bared clam hole is a work-site for the shovel.
        ///
        /// <para>It has to outrank <see cref="InteractPriority.Held"/> for the same reason the hole does:
        /// otherwise, with a clam in hand, E would resolve to whatever else you are near and the one act
        /// available to you would be unreachable. And it carries the same obligation that rung imposes —
        /// <see cref="IsAvailable"/> is false unless a CATCH is actually in hand, so the pail never
        /// outranks anything while you are holding a shovel or a fuel can.</para>
        /// </summary>
        public int Priority => InteractPriority.ToolTarget;

        /// <inheritdoc/>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <summary>False — it is on your own belt; requiring you to face it would be absurd.</summary>
        public bool RequiresFacing => false;

        /// <summary>
        /// Only a thing to act on when there is a catch in her hands to put in it.
        ///
        /// <para>Note what is NOT here: a FULL pail is still available. Press it and the game says the
        /// pail is full (P5, a cozy refusal) — vanishing from the resolver would leave the press silent
        /// and the player holding a clam with no idea why nothing happens.</para>
        /// </summary>
        public bool IsAvailable => HeldCatch() != null;

        /// <summary>The catch in the fisher's hands right now, or null. Written out rather than as
        /// <c>GameServices.Hands?.Carried as CarriableCatch</c> so the fake-null question is answered
        /// where a reader can see it: both <c>Hands</c> and <c>Carried</c> launder a destroyed object into
        /// a REAL null on the way out, so a plain <c>is</c> pattern here is right — the <c>?.</c> that
        /// would be wrong on a raw UnityEngine.Object is safe only because of that laundering, and saying
        /// so beats leaving the next reader to work it out.</summary>
        private static CarriableCatch HeldCatch()
        {
            ICarrier hands = GameServices.Hands;
            if (hands == null) return null;
            return hands.Carried as CarriableCatch;
        }

        /// <summary>The press: stack what is in her hands, or say why it did not go in.</summary>
        public void Interact(in InteractActor actor)
        {
            if (GameServices.CatchHands is not CarryHands hands)
            {
                EventBus.Publish(new DevNotice("No one here to fill it."));
                return;
            }
            if (hands.Carried is not CarriableCatch held)
            {
                EventBus.Publish(new DevNotice("Nothing in your hands to put in."));
                return;
            }

            string what = held.Item.DisplayName;
            if (!hands.TryGiveCatchTo(this))
            {
                EventBus.Publish(new DevNotice("The bucket's full — head to Nine Mile Creek and sell."));
                return;
            }

            EventBus.Publish(new DevNotice($"You drop the {what} in the bucket. ({UsedUnits}/{CapacityUnits})"));
        }
    }
}
