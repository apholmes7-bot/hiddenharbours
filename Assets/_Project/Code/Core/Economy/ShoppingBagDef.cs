using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A shopping bag a store hands out</b> (buying at the shelf, slice 2; the owner's ruling, 2026-11-23:
    /// "when they grab an item a storage paper bag appears in the players hands if its an item that would
    /// appear in a storage bag, the player can fill the bag with multiple items then transfer the items
    /// manuallly to their storage"). One asset per kind of bag (<c>shoppingbag.paper</c>); a store's shelf
    /// slots name the bag its bagged goods go into. Nothing here is state: what is in the bag is
    /// <see cref="ShoppingBagContents"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Economy/Shopping Bag", fileName = "ShoppingBag")]
    public sealed class ShoppingBagDef : ScriptableObject
    {
        /// <summary>Stable id, <c>shoppingbag.snake_case</c>. Append-only.</summary>
        public string Id;

        /// <summary>What the bag is called, for people.</summary>
        public string DisplayName;

        /// <summary>How many goods the bag holds; the next one is refused before any money moves.</summary>
        [Min(1)] public int Capacity = 8;

        /// <summary>The bag as drawn in his hand.</summary>
        public Sprite HeldSprite;

        /// <summary>How near (m) to his storage he must stand for the bag to offer to put the shopping away.</summary>
        [Min(0f)] public float PutAwayReachMeters = 1.5f;

        /// <summary>How near (m) he must stand to a bag he set down to pick it up again.</summary>
        [Min(0f)] public float PickUpReachMeters = 1.2f;
    }
}
