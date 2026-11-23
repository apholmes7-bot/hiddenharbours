using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The paper shopping bag in his hands</b> (buying at the shelf, slice 2): the
    /// <see cref="IShoppingBagHands"/> side of the two-hand carry. The bag is an ordinary carried container
    /// in one hand (<see cref="CarriableShoppingBag"/>), taken with the same slot rule as a pail, so a bag
    /// and a rod share the pair and a bag and a pail do not. What is in it is
    /// <see cref="ShoppingBagContents"/>, the goods set aside from his supplies until he puts them away.
    /// </summary>
    public sealed partial class CarryHands
    {
        private CarriableShoppingBag _bag;

        /// <summary>The bag in his hand, or null.</summary>
        public CarriableShoppingBag HeldBag
        {
            get
            {
                if (_bag != null && _slots.SideOf(_bag) == CarryHandSide.None) _bag = null;
                return _bag;
            }
        }

        /// <inheritdoc/>
        public bool HoldsBag => HeldBag != null;

        /// <inheritdoc/>
        public ShoppingBagRefusal CanBag(ShoppingBagDef bag)
        {
            SaveData save = GameServices.Save?.Current;
            if (save == null) return ShoppingBagRefusal.NoSave;
            CarriableShoppingBag held = HeldBag;
            if (held != null)
                return ShoppingBagContents.Total(save) >= held.Capacity ? ShoppingBagRefusal.BagFull
                                                                         : ShoppingBagRefusal.None;
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
                held = CarriableShoppingBag.Create(bag, transform);
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

        // A bag with nothing set aside in it is not held: the game was reloaded or the services reset under
        // it (the goods are then found put away, see ShoppingBagContents). Two reads a frame while a bag is up.
        private void DropEmptyBag()
        {
            if (_bag == null) return;
            if (!ShoppingBagContents.HoldsAny(GameServices.Save?.Current)) RemoveBag(_bag);
        }

        private void RemoveBag(CarriableShoppingBag held)
        {
            Forget(held);
            if (ReferenceEquals(_bag, held)) _bag = null;
            DestroyBag(held);
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
