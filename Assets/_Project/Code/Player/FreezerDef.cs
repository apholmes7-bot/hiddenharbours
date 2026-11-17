using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A chest freezer for CATCH</b> (owner 2026-11-17: "more storage with the camper and small shed which
    /// hold more rods, bigger freezer"). One asset per kind, stable append-only id (rule 2). Catch put in is
    /// held Frozen, which stops rot, exactly as in Ginny's freezer; the capacity is here, not in code (rule 6).
    ///
    /// <para>Read by <see cref="CamperFreezer"/>. Not a food store: cooked food and groceries go in the
    /// kitchen stores (<c>KitchenStorageDef</c>), a fridge and a pantry beside it.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Freezer", fileName = "Freezer")]
    public class FreezerDef : ScriptableObject
    {
        [Tooltip("Stable id, type.snake_case, APPEND-ONLY (freezer.camper_freezer).")]
        public string Id;

        [Tooltip("What the game calls it in a line ('camper's freezer').")]
        public string DisplayName;

        [Min(0)]
        [Tooltip("How many catch units it holds at once. Owner-tunable. Ginny's freezer holds 20.")]
        public int CapacityUnits;

        [Tooltip("The home whose deed opens it (home.ginny_lot_camper). Empty = open with no deed. Read " +
                 "through Core.StorageAccess: a closed freezer takes nothing and gives nothing back.")]
        public string DeedHomeId = "";
    }
}
