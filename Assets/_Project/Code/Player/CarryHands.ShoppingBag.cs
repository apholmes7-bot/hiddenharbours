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
    /// <para><b>Never lost.</b> The goods are his from the moment he pays. If the bag's picture is gone
    /// while its goods are still set aside (he left for the title and came back, the scene it stood in was
    /// unloaded), the bag is put back in his hand. See <see cref="ShoppingBagContents"/> for what a full
    /// quit does until the save holds the bag.</para>
    /// </summary>
    public sealed partial class CarryHands
    {
        private CarriableShoppingBag _bag;
        private bool _bagBackToHand;

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
            SetBagDown(byTheBed: true);
        }

        private CarryRefusal SetBagDown(bool byTheBed)
        {
            CarriableShoppingBag held = HeldBag;
            SaveData save = GameServices.Save?.Current;
            if (held == null || !ShoppingBagContents.HoldsAny(save)) return CarryRefusal.NothingCarried;

            CarryRefusal refusal = TryPlace(held);
            if (refusal != CarryRefusal.None) return refusal;

            Vector2 at = held.transform.position;
            string region = GameServices.CurrentRegionId;
            ShoppingBagContents.MarkSetDown(save, region, at, byTheBed);
            EventBus.Publish(new ShoppingBagSetDown(region, at, ShoppingBagContents.Total(save), byTheBed));
            return CarryRefusal.None;
        }

        // A game was loaded or resumed: his bag goes back in his hand (KeepBag, next frame).
        private void OnGameLoadedForBag(GameLoaded _) => _bagBackToHand = true;

        private void SubscribeBagSignals()
        {
            EventBus.Subscribe<RestSaveRequested>(OnRestRequested);
            EventBus.Subscribe<GameLoaded>(OnGameLoadedForBag);
        }

        private void UnsubscribeBagSignals()
        {
            EventBus.Unsubscribe<RestSaveRequested>(OnRestRequested);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoadedForBag);
        }

        /// <summary>
        /// Keeps the bag and its goods in step, once a frame; one static read with no bag anywhere.
        /// <list type="bullet">
        ///   <item>No goods of the loaded game are set aside (put away, used up, or the bag belonged to a
        ///   game that was left for a new one): there is no bag.</item>
        ///   <item>Goods are set aside and the bag's picture is gone: it is put back in his hand, or at his
        ///   feet when no hand is free.</item>
        ///   <item>A game was just loaded or resumed and the bag stands somewhere: it is back in his hand
        ///   when a hand is free.</item>
        /// </list>
        /// Public so a test settles it without waiting a frame.
        /// </summary>
        public void KeepBag()
        {
            if (ReferenceEquals(_bag, null) && !ShoppingBagContents.Any)
            {
                _bagBackToHand = false;
                return;
            }

            SaveData save = GameServices.Save?.Current;
            CarriableShoppingBag bag = Bag;
            if (!ShoppingBagContents.HoldsAny(save))
            {
                if (bag != null) RemoveBag(bag);
                _bag = null;
                _bagBackToHand = false;
                // Goods set aside against a game that is no longer the loaded one: that bag is gone.
                if (save != null && ShoppingBagContents.Any) ShoppingBagContents.Clear();
                return;
            }

            if (bag == null) RestoreBag(save);
            else if (_bagBackToHand && _slots.SideOf(bag) == CarryHandSide.None) TryPickUpBag();
            _bagBackToHand = false;
        }

        private void RestoreBag(SaveData save)
        {
            ShoppingBagDef def = ShoppingBagContents.Bag;
            if (def == null)
            {
                // No kind of bag to draw: the goods are his, so they are simply in his supplies.
                ShoppingBagContents.Release(save);
                _bag = null;
                return;
            }

            CarriableShoppingBag bag = CarriableShoppingBag.Create(def, transform, this);
            _bag = bag;
            if (_slots.TryTake(bag, false, PreferredHand(bag), out CarryHandSide side) == HandSlotRefusal.None)
            {
                RememberParent(side, null);
                _lastTaken = bag;
                ShoppingBagContents.MarkHeld();
                ApplyCarriedPose();
                return;
            }

            // Both hands taken: it stands at his feet.
            Vector2 feet = transform.position;
            bag.transform.SetParent(null, worldPositionStays: false);
            bag.transform.position = feet;
            bag.OnPlaced();
            ShoppingBagContents.MarkSetDown(save, GameServices.CurrentRegionId, feet, byTheBed: false);
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
