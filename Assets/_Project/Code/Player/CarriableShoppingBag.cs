using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The paper shopping bag in his hand</b> (buying at the shelf, slice 2; the owner's ruling,
    /// 2026-11-23: the bag appears in his hands when he takes a bagged good, more goods go into it, and he
    /// carries it to his storage to put the shopping away). Made by <see cref="CarryHands"/> when the first
    /// bagged good is bought, held in one of his two hands as a container, and destroyed when the shopping is
    /// put away. What is in it is <see cref="ShoppingBagContents"/>; this is the picture and the press.
    ///
    /// <para><b>Putting it away.</b> While held, the bag is also an interact candidate: near anything that
    /// is his storage (<see cref="PlayerStorage"/>: Ginny's freezer, the camper, his stowage rack) it offers
    /// "Put the shopping away". Storage with no place in the world (the boat hold lives with the region, not
    /// at the boat) and storage he has no key to are skipped. The nearest storage is found once a frame over
    /// the handful registered; nothing allocates.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarriableShoppingBag : MonoBehaviour, ICarriable, IHandLoad, IInteractable
    {
        /// <summary>The label near his storage. Loc seam.</summary>
        public const string PutAwayLabel = "Put the shopping away";

        [SerializeField] private SpriteRenderer _renderer;

        private ShoppingBagDef _def;
        private Component _target;
        private string _targetId = "";

        /// <summary>The bag's kind.</summary>
        public ShoppingBagDef Def => _def;

        /// <summary>How many goods the bag holds.</summary>
        public int Capacity => _def != null ? Mathf.Max(1, _def.Capacity) : 1;

        /// <summary>The storage the bag would be put away into right now, or null.</summary>
        public string TargetStorageId => _target != null ? _targetId : null;

        /// <summary>Makes a bag of <paramref name="def"/> under <paramref name="parent"/>.</summary>
        public static CarriableShoppingBag Create(ShoppingBagDef def, Transform parent)
        {
            var go = new GameObject("ShoppingBag(" + (def != null ? def.Id : "none") + ")");
            go.SetActive(false);   // register as a candidate only once wired
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = def != null ? def.HeldSprite : null;
            var bag = go.AddComponent<CarriableShoppingBag>();
            bag._renderer = sr;
            bag._def = def;
            go.SetActive(true);
            return bag;
        }

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        private void Update() => FindStorage();

        /// <summary>Picks the nearest storage he may put the shopping into. Public so a test steps it.</summary>
        public void FindStorage()
        {
            _target = null;
            _targetId = "";
            Vector2 at = transform.position;
            float best = float.MaxValue;
            for (int i = 0; i < PlayerStorage.Count; i++)
            {
                IStorageContainer store = PlayerStorage.GetContainer(i);
                if (!(store is Component c) || c == null || !c.gameObject.activeInHierarchy) continue;
                if (!MayPutAwayInto(store)) continue;
                float d = ((Vector2)c.transform.position - at).sqrMagnitude;
                if (d < best) { best = d; _target = c; _targetId = store.StorageId ?? ""; }
            }
        }

        /// <summary>Whether the shopping may be put away into <paramref name="store"/>: a place in the world
        /// that is his.</summary>
        public static bool MayPutAwayInto(IStorageContainer store)
        {
            if (store == null || string.IsNullOrEmpty(store.StorageId)) return false;
            if (string.Equals(store.StorageId, StorageIds.BoatHold, System.StringComparison.Ordinal)) return false;
            if (store is StowageRack rack && !rack.IsOpen) return false;
            if (store is CamperFreezer camper && !camper.IsOpen) return false;
            return true;
        }

        // ---- ICarriable ---------------------------------------------------------------------------

        /// <inheritdoc/>
        public string DefId => _def != null ? _def.Id : "";

        /// <summary>A bag is a container: it shares the pair with a tool, not with a pail.</summary>
        public HandLoad HandLoad => HiddenHarbours.Core.HandLoad.Container;

        /// <inheritdoc/>
        public bool IsCarriable => true;

        /// <inheritdoc/>
        public Transform Transform => transform;

        /// <summary>One drawing at every heading.</summary>
        public int BakedFacings => 1;

        /// <inheritdoc/>
        public void ShowFacing(int facingIndex) { }

        /// <inheritdoc/>
        public void RideSortingBand(int sortingLayerId, int sortingOrder)
        {
            if (_renderer == null) return;
            _renderer.sortingLayerID = sortingLayerId;
            _renderer.sortingOrder = sortingOrder;
        }

        /// <inheritdoc/>
        public void OnLifted(ICarrier carrier) { }

        /// <inheritdoc/>
        public void OnPlaced() { }

        // ---- IInteractable: put the shopping away --------------------------------------------------

        /// <inheritdoc/>
        public string Id => "shoppingbag.put_away";

        /// <inheritdoc/>
        public string VerbLabel => PutAwayLabel;

        /// <summary>The storage's place: he must stand near it.</summary>
        public Vector2 WorldPosition => _target != null ? (Vector2)_target.transform.position : (Vector2)transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _def != null ? _def.PutAwayReachMeters : 0f;

        /// <summary>Held: it is in his hand, so it outranks the fixture he is standing at.</summary>
        public int Priority => InteractPriority.Held;

        /// <inheritdoc/>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <summary>Offered only with his storage near and the bag holding something.</summary>
        public bool IsAvailable => _target != null && ShoppingBagContents.HoldsAny(GameServices.Save?.Current);

        /// <inheritdoc/>
        public void Interact(in InteractActor actor) => PutAway();

        /// <summary>Puts the shopping away into the storage near him, and says so. Public so a test drives
        /// it without input.</summary>
        public bool PutAway()
        {
            if (_target == null) FindStorage();
            if (_target == null) return false;
            IShoppingBagHands hands = GameServices.BagHands;
            if (hands == null || !hands.TryPutAway(_targetId)) return false;
            EventBus.Publish(new InnerVoiceCue(InnerVoiceCues.ShoppingPutAway));
            return true;
        }
    }
}
