using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A placed food store's binding</b>: which <see cref="KitchenStorageDef"/> a kitchen fixture (the fridge)
    /// is, the way a chest freezer prefab names its <c>FreezerDef</c>. Its capacity, its cold and its deed are the
    /// Def's answers, never this component's (rule 2).
    ///
    /// <para><b>What it does not do yet.</b> Nothing in the world puts food in or takes it out of a kitchen store
    /// (the store model and its save list, <see cref="KitchenStores"/>, have no fixture that calls them), so this
    /// carries no verb, no store key and no state. A later slice that lets the player use the fridge reads the
    /// Def from here, gives each placed instance its own store key, and opens the door through
    /// <see cref="ApplianceArt.SetDoorOpen"/>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KitchenStoreFixture : MonoBehaviour
    {
        [Tooltip("The food store this fixture is (kitchen_storage.fridge for a fridge): its capacity, its cold, " +
                 "its deed.")]
        [SerializeField] private KitchenStorageDef _def;

        /// <summary>The store's Def, or null.</summary>
        public KitchenStorageDef Def => _def;

        /// <summary>Wire the fixture (a builder, and tests).</summary>
        public void Configure(KitchenStorageDef def) => _def = def;
    }
}
