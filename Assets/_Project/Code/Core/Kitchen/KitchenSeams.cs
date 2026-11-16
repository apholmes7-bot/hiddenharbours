namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>⚠ SEAM(power): does the place a fridge stands in have power?</b> (kitchens slice 1). Main has no
    /// power model: the helm's battery gauge reads a hull probe that answers "no battery"
    /// (<c>WorkingHelmHullProbe</c>), and no engine charges anything. So this is a named Core seam with an
    /// honest default, <see cref="AlwaysOnKitchenPower"/>: every fridge is powered. A boat's battery, a
    /// home's mains or a shop's generator answers here later, keyed by the store's place.
    ///
    /// <para><b>Determinism.</b> Power is read at the slow tick (<see cref="KitchenStores.Tick"/>) and a
    /// change is banked at that instant, so a fridge's contents settle piecewise exactly like a cooler's
    /// ice. With the default nothing ever changes, and catch-up over a sleep is exact.</para>
    /// </summary>
    public interface IKitchenPower
    {
        /// <summary>True when the store with this key (<see cref="KitchenStorageStateDto.Key"/>) has power
        /// now.</summary>
        bool IsPowered(string storageKey);
    }

    /// <summary>The default: every fridge has power. See <see cref="IKitchenPower"/>.</summary>
    public sealed class AlwaysOnKitchenPower : IKitchenPower
    {
        public static readonly AlwaysOnKitchenPower Instance = new AlwaysOnKitchenPower();
        public bool IsPowered(string storageKey) => true;
    }

    /// <summary>
    /// <b>⚠ SEAM(food): how fast an item spoils in the open</b>, by item id (kitchens slice 1). The food
    /// Defs are not on main yet (draft PR 1227 adds <c>FoodDef</c>/<c>FoodEater</c>), so this answers for
    /// them: <see cref="NothingSpoilsPerishability"/> says 0 for every id, which is the dry-goods truth and
    /// never invents a rate. When the food slice lands, a resolver over its Defs (a FoodDef's own spoil
    /// rate, or a FishDef's <c>SpoilPerDay</c> for a raw catch) is assigned to
    /// <see cref="GameServices.FoodPerishability"/> and every store starts spoiling for real, with no change
    /// here.
    /// </summary>
    public interface IFoodPerishability
    {
        /// <summary>The item's ambient spoil rate: 1 = ruined in one in-game day, 0 = never spoils.</summary>
        float SpoilPerDay(string itemId);
    }

    /// <summary>The default: nothing spoils. See <see cref="IFoodPerishability"/>.</summary>
    public sealed class NothingSpoilsPerishability : IFoodPerishability
    {
        public static readonly NothingSpoilsPerishability Instance = new NothingSpoilsPerishability();
        public float SpoilPerDay(string itemId) => 0f;
    }
}
