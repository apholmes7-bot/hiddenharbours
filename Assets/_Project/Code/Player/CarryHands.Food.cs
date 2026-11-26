using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    public sealed partial class CarryHands
    {
        private HeldFoodInteraction _foodChoice;

        private void RegisterFoodChoices()
        {
            UnregisterFoodChoices();
            if (!TryPeekHand(out CatchItem held)
                || GameServices.Food.Check(held.SpeciesId, out FoodDef food) != EatRefusal.None) return;
            _foodChoice = new HeldFoodInteraction(food, () => (Vector2)transform.position);
            Interactables.Register(_foodChoice);
        }

        private void UnregisterFoodChoices()
        {
            Interactables.Unregister(_foodChoice);
            _foodChoice = null;
        }
    }
}
