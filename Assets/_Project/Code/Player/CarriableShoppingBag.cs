using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The paper shopping bag, in his hand or set down</b> (buying at the shelf, slice 2 and 2b; the
    /// owner's rulings, 2026-11-23: the bag appears in his hands when he takes a bagged good, more goods go
    /// into it, he carries it to his storage to put the shopping away; and "bag gets dropped by the bed,
    /// bag is droppable with items inside"). Made by <see cref="CarryHands"/> when the first bagged good is
    /// bought, held in one of his two hands as a container, and destroyed when the shopping is put away.
    /// What is in it is <see cref="ShoppingBagContents"/>; this is the picture and the press.
    ///
    /// <para><b>The press, by where the bag is.</b></para>
    /// <list type="bullet">
    ///   <item><b>In his hand, near his storage</b> (<see cref="PlayerStorage"/>: Ginny's freezer, the
    ///   camper, his stowage rack): "Put the shopping away", ahead of the fixture he stands at. Storage
    ///   with no place in the world (the boat hold lives with the region, not at the boat) and storage he
    ///   has no key to are skipped.</item>
    ///   <item><b>In his hand, anywhere else</b>: "Set the bag down", on the last rung
    ///   (<see cref="InteractPriority.LastResort"/>): at a shelf the press still buys, at a bed it still
    ///   sleeps, and with nothing else in reach the bag goes on the ground at his feet.</item>
    ///   <item><b>On the ground</b>: "Pick up the bag", like any loose thing at his feet. A bag left in
    ///   another region, or on another storey of the house, is not drawn and not offered here.</item>
    /// </list>
    ///
    /// <para>The nearest storage is found once a frame over the handful registered; the labels are
    /// constants; nothing allocates.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarriableShoppingBag : MonoBehaviour, ICarriable, IHandLoad, IInteractable,
                                              IMultiActionInteractable
    {
        /// <summary>The label near his storage. Loc seam.</summary>
        public const string PutAwayLabel = "Put the shopping away";

        /// <summary>The label with the bag in his hand and nothing else to press. Loc seam.</summary>
        public const string SetDownLabel = "Set the bag down";

        /// <summary>The label standing over the bag he set down. Loc seam.</summary>
        public const string PickUpLabel = "Pick up the bag";

        private const string PutAwayId = "shoppingbag.put_away";
        private const string BagId = "shoppingbag.bag";

        [SerializeField] private SpriteRenderer _renderer;

        private ShoppingBagDef _def;
        private CarryHands _hands;
        private YSortSprite _ySort;
        private Component _target;
        private string _targetId = "";
        private FoodBook _foods;

        /// <summary>The bag's kind.</summary>
        public ShoppingBagDef Def => _def;

        /// <summary>How many goods the bag holds.</summary>
        public int Capacity => _def != null ? Mathf.Max(1, _def.Capacity) : 1;

        /// <summary>The storage the bag would be put away into right now, or null.</summary>
        public string TargetStorageId => _target != null ? _targetId : null;

        /// <summary>True while the bag is in one of his hands. Read from the hands, never stored.</summary>
        public bool IsCarried => _hands != null && _hands.Slots.SideOf(this) != CarryHandSide.None;

        /// <summary>True while the bag stands on the ground in the region he is in, on the storey he is on:
        /// a bag left upstairs is not seen or picked up from the floor below.</summary>
        public bool StandsHere
            => !IsCarried
               && ShoppingBagContents.LiesIn(GameServices.Save?.Current, GameServices.CurrentRegionId)
               && ShoppingBagContents.SetDownLevel == PlayerStorey.Level;

        /// <summary>The bag picture that exists right now, in a hand or on the ground, or null. There is one
        /// bag; hands that were rebuilt (a return from the title) take this one over instead of making a
        /// second.</summary>
        public static CarriableShoppingBag Standing => _standing != null ? _standing : null;

        private static CarriableShoppingBag _standing;

        /// <summary>These hands now own the bag (see <see cref="Standing"/>).</summary>
        public void Adopt(CarryHands hands) => _hands = hands;

        /// <summary>Makes a bag of <paramref name="def"/> under <paramref name="parent"/>, for
        /// <paramref name="hands"/>.</summary>
        public static CarriableShoppingBag Create(ShoppingBagDef def, Transform parent, CarryHands hands = null)
        {
            var go = new GameObject("ShoppingBag(" + (def != null ? def.Id : "none") + ")");
            go.SetActive(false);   // register as a candidate only once wired
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = def != null ? def.HeldSprite : null;
            // Sorts by where it stands once it is on the ground; in his hand the hands give the order.
            var ySort = go.AddComponent<YSortSprite>();
            ySort.enabled = false;
            var bag = go.AddComponent<CarriableShoppingBag>();
            bag._renderer = sr;
            bag._ySort = ySort;
            bag._def = def;
            bag._hands = hands;
            go.SetActive(true);
            return bag;
        }

        private void OnEnable()
        {
            Interactables.Register(this);
            _standing = this;
        }

        private void OnDisable()
        {
            Interactables.Unregister(this);
            if (ReferenceEquals(_standing, this)) _standing = null;
        }

        private void Update()
        {
            if (IsCarried)
            {
                FindStorage();
                return;
            }

            _target = null;
            _targetId = "";
            // A picture nobody owns, of a bag the loaded game does not have (another game was begun around
            // it): it goes.
            if (_hands == null && !ShoppingBagContents.HoldsAny(GameServices.Save?.Current))
            {
                Destroy(gameObject);
                return;
            }
            // A bag left in another region, or on another storey, is not drawn here.
            bool here = StandsHere;
            if (_renderer != null && _renderer.enabled != here) _renderer.enabled = here;
        }

        /// <summary>Picks the nearest storage he may put the shopping into and stands in reach of, measured
        /// from where he stands; out of reach of them all, there is none and the press sets the bag down.
        /// Public so a test steps it.</summary>
        public void FindStorage()
        {
            _target = null;
            _targetId = "";
            Vector2 at = _hands != null ? (Vector2)_hands.transform.position : (Vector2)transform.position;
            float reach = _def != null ? _def.PutAwayReachMeters : 0f;
            float best = reach * reach;
            for (int i = 0; i < PlayerStorage.Count; i++)
            {
                IStorageContainer store = PlayerStorage.GetContainer(i);
                if (!(store is Component c) || c == null || !c.gameObject.activeInHierarchy) continue;
                if (!MayPutAwayInto(store)) continue;
                float d = ((Vector2)c.transform.position - at).sqrMagnitude;
                if (d <= best) { best = d; _target = c; _targetId = store.StorageId ?? ""; }
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

        /// <summary>Into his hand: the hands give its order from here, and it is drawn wherever he is.</summary>
        public void OnLifted(ICarrier carrier)
        {
            if (carrier is CarryHands hands) _hands = hands;
            if (_ySort != null) _ySort.enabled = false;
            if (_renderer != null && !_renderer.enabled) _renderer.enabled = true;
        }

        /// <summary>On the ground at its final position: it sorts by where it stands.</summary>
        public void OnPlaced()
        {
            _target = null;
            _targetId = "";
            if (_ySort != null) _ySort.enabled = true;
        }

        // ---- IInteractable: put away, set down, pick up ---------------------------------------------

        private bool PutAwayOffered => _target != null && IsCarried;

        /// <inheritdoc/>
        public string Id => PutAwayOffered ? PutAwayId : BagId;

        /// <inheritdoc/>
        public string VerbLabel => PutAwayOffered ? PutAwayLabel : (IsCarried ? SetDownLabel : PickUpLabel);

        /// <summary>Near his storage, the storage's place (he must stand near it); otherwise the bag's own.</summary>
        public Vector2 WorldPosition => PutAwayOffered ? (Vector2)_target.transform.position : (Vector2)transform.position;

        /// <inheritdoc/>
        public float ReachMeters
        {
            get
            {
                if (_def == null) return 0f;
                return PutAwayOffered ? _def.PutAwayReachMeters : _def.PickUpReachMeters;
            }
        }

        /// <summary>Putting away outranks the fixture he stands at (it is in his hand); setting down takes
        /// the press only when nothing else wants it; a bag on the ground is a loose thing at his feet.</summary>
        public int Priority
        {
            get
            {
                if (!IsCarried) return InteractPriority.Portable;
                return _target != null ? InteractPriority.Held : InteractPriority.LastResort;
            }
        }

        /// <inheritdoc/>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <summary>Offered while the bag holds something and is in his hand or standing in this region.</summary>
        public bool IsAvailable
        {
            get
            {
                if (!ShoppingBagContents.HoldsAny(GameServices.Save?.Current)) return false;
                return IsCarried || StandsHere;
            }
        }

        /// <inheritdoc/>
        public void Interact(in InteractActor actor)
        {
            if (!IsCarried) PickUp();
            else if (_target != null) PutAway();
            else SetDown();
        }

        // Indices follow the food book, never the bag's changing lines. Missing foods have no row.
        // The existing action stays first, so a confirm on the initial row keeps its old meaning.
        private FoodBook Foods => _foods ??= GameServices.Food.Book;
        public int ActionCount => 1 + (Foods?.All.Count ?? 0);

        private FoodDef FoodAt(int index)
            => index > 0 && index < ActionCount ? Foods.All[index - 1] : null;

        public string ActionLabel(int index)
        {
            if (index == 0) return VerbLabel;
            FoodDef food = FoodAt(index);
            return food != null && ActionAvailable(index) ? food.BagEatLabel : null;
        }

        public bool ActionAvailable(int index)
        {
            if (index == 0) return IsAvailable;
            FoodDef food = FoodAt(index);
            return IsCarried && food != null && !string.IsNullOrEmpty(food.BagEatLabel)
                   && GameServices.Food.CheckBag(food.ItemId, out _) == EatRefusal.None;
        }

        public void Interact(in InteractActor actor, int index)
        {
            if (!ActionAvailable(index)) return;
            if (index == 0) { Interact(actor); return; }
            GameServices.Food.TryEatFromBag(FoodAt(index).ItemId);
            _hands.KeepBag();
        }

        /// <summary>Puts the shopping away into the storage near him, and says so. Public so a test drives
        /// it without input.</summary>
        public bool PutAway()
        {
            if (!IsCarried) return false;
            if (_target == null) FindStorage();
            if (_target == null) return false;
            IShoppingBagHands hands = GameServices.BagHands;
            if (hands == null || !hands.TryPutAway(_targetId)) return false;
            EventBus.Publish(new InnerVoiceCue(InnerVoiceCues.ShoppingPutAway));
            return true;
        }

        /// <summary>Stands the bag on the ground at his feet with the goods inside. A refusal (nowhere dry)
        /// is said in the words every carried thing uses. Public so a test drives it without input.</summary>
        public bool SetDown()
        {
            if (_hands == null) return false;
            CarryRefusal refusal = _hands.TrySetBagDown();
            if (refusal == CarryRefusal.None) return true;
            string why = CarryMath.Message(refusal);
            if (why != null) EventBus.Publish(new DevNotice(why));
            return false;
        }

        /// <summary>Takes the bag up again. With no hand free for it he says his hands are full. Public so
        /// a test drives it without input.</summary>
        public bool PickUp()
        {
            if (_hands == null) _hands = GameServices.BagHands as CarryHands;
            if (_hands == null) return false;
            CarryRefusal refusal = _hands.TryPickUpBag();
            if (refusal == CarryRefusal.None) return true;
            EventBus.Publish(new InnerVoiceCue(InnerVoiceCues.ShelfHandsFull));
            return false;
        }
    }
}
