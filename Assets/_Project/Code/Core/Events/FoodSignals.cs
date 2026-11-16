namespace HiddenHarbours.Core
{
    /// <summary>
    /// Raised once when the player has eaten one unit of a food: the unit is already gone and hunger already
    /// lowered through <see cref="INeedsService.Eat"/>. The diegetic hook for the moment (owner ruling
    /// 2026-11-14: no bar, no meter, no number): audio plays a bite, the rig a hand-to-mouth, dialogue drops
    /// the hungry bubble. Nothing is required to consume it.
    /// </summary>
    public readonly struct AteFood
    {
        /// <summary>The <see cref="FoodDef.Id"/> eaten (<c>food.bread</c>).</summary>
        public readonly string FoodId;

        /// <summary>The item one unit of which was used up (<c>supply.bread</c>, <c>produce.carrot</c>).</summary>
        public readonly string ItemId;

        /// <summary>The share of one meal this food is worth (<see cref="FoodDef.HungerRelief"/>). For a
        /// reaction's size (a nibble or a meal), never for display.</summary>
        public readonly float Relief;

        public AteFood(string foodId, string itemId, float relief)
        {
            FoodId = foodId; ItemId = itemId; Relief = relief;
        }
    }

    /// <summary>
    /// Raised once when an eat was refused; nothing was used up. Carries a reason code, not a sentence, so
    /// the character's bubble or a villager's line owns the words (a raw cod: "not like this").
    /// </summary>
    public readonly struct EatRefused
    {
        /// <summary>The item the player tried to eat, or null when there was nothing to try.</summary>
        public readonly string ItemId;

        /// <summary>Why it did not happen. Never <see cref="EatRefusal.None"/>.</summary>
        public readonly EatRefusal Reason;

        public EatRefused(string itemId, EatRefusal reason)
        {
            ItemId = itemId; Reason = reason;
        }
    }
}
