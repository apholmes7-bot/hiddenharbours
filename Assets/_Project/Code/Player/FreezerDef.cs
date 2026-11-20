using HiddenHarbours.Art;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A chest freezer for CATCH</b> (owner 2026-11-17: "more storage with the camper and small shed which
    /// hold more rods, bigger freezer"). One asset per kind, stable append-only id (rule 2). Catch put in is
    /// held Frozen, which stops rot, exactly as in Ginny's freezer; the capacity is here, not in code (rule 6).
    ///
    /// <para>Read by <see cref="CamperFreezer"/> and <see cref="GinnyFreezer"/>. Not a food store: cooked food
    /// and groceries go in the kitchen stores (<c>KitchenStorageDef</c>), a fridge and a pantry beside it.</para>
    ///
    /// <para><b>Its look</b> is here too, so a bigger freezer is a new asset and a new baked sheet, never new
    /// code: which sheet draws it (<see cref="SheetStem"/>), where in that picture the fish lie
    /// (<see cref="ContentsOpeningMetres"/>), when its bed shows full, how big the fish are drawn and how fast
    /// the lid swings.</para>
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

        [Header("Look")]
        [Tooltip("The baked appliance sheet that draws it (Art/Resources/Appliances/<stem>.png), one of the " +
                 "sheets ChestFreezerSheet.Pieces bakes. Empty = no baked art.")]
        public string SheetStem = ChestFreezerSheet.Stem;

        [Tooltip("The open chest's mouth as drawn front-on: the rectangle between its front and back rims, in " +
                 "metres from the freezer's floor point (x across, y up the screen). Fish are laid inside it; " +
                 "an EditMode test holds it to the rig's own rims.")]
        public Rect ContentsOpeningMetres = new Rect(-0.595f, 0.4499f, 1.19f, 0.4178f);

        [Range(0.01f, 1f)]
        [Tooltip("From this share of its capacity the bed under the fish is drawn full (packed to the rim).")]
        public float FullFromFraction = 0.6f;

        [Min(0.01f)]
        [Tooltip("The fish's size in the chest against their size lying on a deck (1 = the same pixels).")]
        public float FishDrawScale = 1f;

        [Min(1f)]
        [Tooltip("The room a fish is laid in against its measured (held) length. The side-on deck lay is drawn " +
                 "longer than the held pose (measured 1.12-1.27x) and up to a tenth of its length off its pivot, " +
                 "so a slot of 1.5x keeps every fish's drawing inside the walls.")]
        public float FishLaySlotOverHeldLength = 1.5f;

        [Min(0f)]
        [Tooltip("A fish's height in the chest as a share of its length, so a row never climbs over the back rim.")]
        public float FishHeightOfLength = 0.3f;

        [Min(0.01f)]
        [Tooltip("Metres between one row of fish and the next, back to front, in the opening.")]
        public float FishRowPitchMetres = 0.13f;

        [Min(1)]
        [Tooltip("How many of the catch art's deck lays (its first variants) lie side-on, along the chest; fish in " +
                 "the chest use only those, so none lies across a rim.")]
        public int FishSideOnLays = 2;

        [Tooltip("Metres between fish in a row (negative: they overlap a little, as fish heaped in a chest do).")]
        public float FishGapMetres = -0.02f;

        [Min(0.01f)]
        [Tooltip("Seconds per lid frame as it swings open or shut.")]
        public float LidFrameSeconds = 0.08f;
    }
}
