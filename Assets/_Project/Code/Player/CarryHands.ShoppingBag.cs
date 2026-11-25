using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The paper shopping bag in his hands, and set down</b> (buying at the shelf, slice 2 and 2b): the
    /// <see cref="IShoppingBagHands"/> side of the two-hand carry. The bag is an ordinary carried container
    /// in one hand (<see cref="CarriableShoppingBag"/>), taken with the same slot rule as a pail, so a bag
    /// and a rod share the pair and a bag and a pail do not. What is in it is
    /// <see cref="ShoppingBagContents"/>, the goods set aside from his supplies until he puts them away.
    ///
    /// <para><b>Set down and picked up</b> (the owner's ruling, 2026-11-23: "bag gets dropped by the bed,
    /// bag is droppable with items inside"). He can stand the bag on the ground with the goods still in it
    /// and take it up again; lying down to sleep ashore with it in his hand leaves it by the bed. There is
    /// one bag: while it stands somewhere with goods in it, a shelf refuses to start a second.</para>
    ///
    /// <para><b>Saved, and never lost.</b> The goods are his from the moment he pays, and the bag is in the
    /// save (<see cref="ShoppingBagContents"/>): after a load it is in his hand if it was in his hand, and
    /// otherwise stands where he left it, in its region, at its place, on its storey. The picture is made
    /// again from the save whenever it is missing (a restart, the scene it stood in was unloaded and
    /// loaded again); a bag left in another region has no picture here until he is back there.</para>
    /// </summary>
    public sealed partial class CarryHands
    {
        private CarriableShoppingBag _bag;

        /// <summary>His bag, in a hand or set down, or null.</summary>
        public CarriableShoppingBag Bag => _bag != null ? _bag : null;

        /// <summary>The bag in his hand, or null (none, or it is set down).</summary>
        public CarriableShoppingBag HeldBag
        {
            get
            {
                CarriableShoppingBag bag = Bag;
                return bag != null && _slots.SideOf(bag) != CarryHandSide.None ? bag : null;
            }
        }

        /// <inheritdoc/>
        public bool HoldsBag => HeldBag != null;

        /// <inheritdoc/>
        public bool BagIsSetDown => ShoppingBagContents.IsSetDown(GameServices.Save?.Current);

        /// <inheritdoc/>
        public ShoppingBagRefusal CanBag(ShoppingBagDef bag)
        {
            SaveData save = GameServices.Save?.Current;
            if (save == null) return ShoppingBagRefusal.NoSave;
            CarriableShoppingBag held = HeldBag;
            if (held != null)
                return ShoppingBagContents.Total(save) >= held.Capacity ? ShoppingBagRefusal.BagFull
                                                                         : ShoppingBagRefusal.None;
            // Goods set aside and no bag in hand: his one bag stands somewhere.
            if (ShoppingBagContents.HoldsAny(save)) return ShoppingBagRefusal.BagElsewhere;
            if (bag == null) return ShoppingBagRefusal.HandsFull;
            return _slots.CanTake(HandLoad.Container, needsBothHands: false) == HandSlotRefusal.None
                ? ShoppingBagRefusal.None
                : ShoppingBagRefusal.HandsFull;
        }

        /// <inheritdoc/>
        public ShoppingBagRefusal TryBag(ShoppingBagDef bag, string supplyId)
        {
            ShoppingBagRefusal refusal = CanBag(bag);
            if (refusal != ShoppingBagRefusal.None) return refusal;
            SaveData save = GameServices.Save.Current;

            CarriableShoppingBag held = HeldBag;
            bool made = false;
            if (held == null)
            {
                held = CarriableShoppingBag.Create(bag, transform, this);
                if (_slots.TryTake(held, false, PreferredHand(held), out CarryHandSide side) != HandSlotRefusal.None)
                {
                    DestroyBag(held);   // unreachable: CanBag agreed on the same state
                    return ShoppingBagRefusal.HandsFull;
                }
                RememberParent(side, null);
                _lastTaken = held;
                _bag = held;
                made = true;
            }

            if (!ShoppingBagContents.TryPut(save, supplyId, held.Capacity))
            {
                if (made) RemoveBag(held);
                return ShoppingBagRefusal.BagFull;
            }
            ShoppingBagContents.NoteBag(save, held.Def);
            ApplyCarriedPose();
            return ShoppingBagRefusal.None;
        }

        /// <inheritdoc/>
        public bool TryPutAway(string storageId)
        {
            CarriableShoppingBag held = HeldBag;
            if (held == null) return false;
            int count = ShoppingBagContents.Release(GameServices.Save?.Current);
            RemoveBag(held);
            EventBus.Publish(new ShoppingPutAway(storageId, count));
            return true;
        }

        // ---- set down, pick up ---------------------------------------------------------------------

        /// <summary>
        /// Stands the bag in his hand on the ground at his feet with its goods inside. The ground rule is
        /// the one every carried thing uses (<see cref="TryPlace(ICarriable)"/>); on a refusal nothing
        /// changes and the bag stays in his hand.
        /// </summary>
        public CarryRefusal TrySetBagDown() => SetBagDown(byTheBed: false);

        /// <summary>
        /// Takes the set-down bag up again, goods and all. Refused, and nothing changes, when there is no
        /// bag standing, or no hand free for it.
        /// </summary>
        public CarryRefusal TryPickUpBag()
        {
            CarriableShoppingBag bag = Bag;
            if (bag == null || HeldBag != null) return CarryRefusal.NotCarriable;
            CarryRefusal refusal = TryPickUp(bag);
            if (refusal != CarryRefusal.None) return refusal;
            ShoppingBagContents.MarkHeld();
            EventBus.Publish(new ShoppingBagPickedUp(ShoppingBagContents.Total(GameServices.Save?.Current)));
            return CarryRefusal.None;
        }

        /// <summary>
        /// He lies down to sleep: a bag in his hand is left by the bed, where he stood to turn in, with
        /// its goods inside. Aboard, the bag stays in his hand: nothing set down on a deck rides the hull
        /// yet. Public so a test drives it without a bed.
        /// </summary>
        public void OnRestRequested(RestSaveRequested request)
        {
            if (request.Aboard || HeldBag == null) return;
            // The bed names its own storey; it is the floor he is standing on to turn in.
            SetBagDown(byTheBed: true, level: Mathf.Max(request.WakeLevel, PlayerStorey.Level));
        }

        private CarryRefusal SetBagDown(bool byTheBed) => SetBagDown(byTheBed, PlayerStorey.Level);

        private CarryRefusal SetBagDown(bool byTheBed, int level)
        {
            CarriableShoppingBag held = HeldBag;
            SaveData save = GameServices.Save?.Current;
            if (held == null || !ShoppingBagContents.HoldsAny(save)) return CarryRefusal.NothingCarried;

            CarryRefusal refusal = TryPlace(held);
            if (refusal != CarryRefusal.None) return refusal;

            Vector2 at = held.transform.position;
            string region = GameServices.CurrentRegionId;
            ShoppingBagContents.MarkSetDown(save, region, at, level, byTheBed);
            EventBus.Publish(new ShoppingBagSetDown(region, at, ShoppingBagContents.Total(save), byTheBed));
            return CarryRefusal.None;
        }

        private void SubscribeBagSignals() => EventBus.Subscribe<RestSaveRequested>(OnRestRequested);

        private void UnsubscribeBagSignals() => EventBus.Unsubscribe<RestSaveRequested>(OnRestRequested);

        /// <summary>
        /// Keeps the bag's picture in step with the save, once a frame; a couple of reads with no bag
        /// anywhere.
        /// <list type="bullet">
        ///   <item>The loaded game has nothing in a bag (put away, used up, a new game, another game
        ///   loaded): there is no bag picture.</item>
        ///   <item>The save says the bag is in his hand and there is no picture: it is made in his hand,
        ///   or stood at his feet when no hand is free.</item>
        ///   <item>The save says the bag stands in the region he is in and there is no picture: it is made
        ///   where it stands. A bag left in another region gets no picture until he is back there.</item>
        /// </list>
        /// Public so a test settles it without waiting a frame.
        /// </summary>
        public void KeepBag()
        {
            SaveData save = GameServices.Save?.Current;
            bool holds = ShoppingBagContents.HoldsAny(save);
            if (ReferenceEquals(_bag, null) && !holds) return;

            CarriableShoppingBag bag = Bag;
            if (!holds)
            {
                if (bag != null) RemoveBag(bag);
                _bag = null;
                return;
            }

            if (bag == null) RestoreBag(save);
        }

        private void RestoreBag(SaveData save)
        {
            _bag = null;
            bool setDown = ShoppingBagContents.IsSetDown(save);
            // It stands in another region: nothing to draw here, and nothing to look up.
            if (setDown && !ShoppingBagContents.LiesIn(save, GameServices.CurrentRegionId)) return;

            ShoppingBagDef def = ShoppingBagContents.Bag;
            if (def == null)
            {
                // A kind of bag that no longer ships: the goods are his, so they are simply in his supplies.
                ShoppingBagContents.Release(save);
                return;
            }

            // A picture of it may already stand in the world (the player was rebuilt around it).
            CarriableShoppingBag bag = CarriableShoppingBag.Standing;
            if (bag != null && bag.Def == def && _slots.SideOf(bag) == CarryHandSide.None) bag.Adopt(this);
            else bag = CarriableShoppingBag.Create(def, transform, this);
            _bag = bag;

            if (setDown)
            {
                Stand(bag, ShoppingBagContents.SetDownPosition);
                return;
            }

            if (_slots.TryTake(bag, false, PreferredHand(bag), out CarryHandSide side) == HandSlotRefusal.None)
            {
                RememberParent(side, null);
                _lastTaken = bag;
                bag.OnLifted(this);
                bag.transform.SetParent(transform, worldPositionStays: false);
                ApplyCarriedPose();
                return;
            }

            // Both hands taken: it stands at his feet.
            Vector2 feet = transform.position;
            Stand(bag, feet);
            ShoppingBagContents.MarkSetDown(save, GameServices.CurrentRegionId, feet, PlayerStorey.Level, byTheBed: false);
        }

        private static void Stand(CarriableShoppingBag bag, Vector2 at)
        {
            bag.transform.SetParent(null, worldPositionStays: false);
            bag.transform.position = at;
            bag.OnPlaced();
        }
        private void RemoveBag(CarriableShoppingBag bag)
        {
            // Forget() clears both remembered parents for a thing in no hand, so only a held bag is forgotten.
            if (_slots.SideOf(bag) != CarryHandSide.None) Forget(bag);
            if (ReferenceEquals(_bag, bag)) _bag = null;
            DestroyBag(bag);
            TakeBackSlung();
        }

        private static void DestroyBag(CarriableShoppingBag bag)
        {
            if (bag == null) return;
            bag.gameObject.SetActive(false);   // off the interact list now, not at the end of the frame
            if (Application.isPlaying) Destroy(bag.gameObject);
            else DestroyImmediate(bag.gameObject);
        }
    }
}
