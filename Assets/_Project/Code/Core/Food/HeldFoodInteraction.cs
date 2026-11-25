using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>An eating choice bound to one food identity. A stale chooser cannot eat a replacement item.</summary>
    public sealed class HeldFoodInteraction : IInteractable
    {
        private readonly string _itemId;
        private readonly Func<Vector2> _position;
        private readonly Func<FoodEater> _eater;

        public HeldFoodInteraction(FoodDef food, Func<Vector2> position, Func<FoodEater> eater = null)
        {
            _itemId = food.ItemId;
            Id = food.Id + ".eat_held";
            _position = position;
            _eater = eater ?? (() => GameServices.Food);
        }

        public string Id { get; }
        public string VerbLabel => "Eat";
        public Vector2 WorldPosition => _position();
        // It is already in his hand: only the actor at the carrier's own position can act.
        public float ReachMeters => 0f;
        public int Priority => InteractPriority.Held;
        public InteractContext Contexts => InteractContext.OnFoot | InteractContext.OnDeck;
        public bool RequiresFacing => false;
        public bool IsAvailable => _eater().CheckHand(out FoodDef food) == EatRefusal.None
                                   && string.Equals(food.ItemId, _itemId, StringComparison.Ordinal);

        public void Interact(in InteractActor actor)
        {
            if (IsAvailable) _eater().TryEatFromHand();
        }
    }
}
