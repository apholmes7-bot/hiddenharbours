#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>WHERE THE PLAYER'S STORAGE GOES</b> (owner 2026-11-17): "the player will need to store their first
    /// rod and equipment, give them storage with memory at ginnys, but give them more storage with the camper
    /// and small shed which hold more rods, bigger freezer".
    ///
    /// <para>This file places nothing. It is the table the placement step reads (one row = one remembered
    /// container), held to the room by <c>HomeStoragePlanTests</c>: every positioned row inside its
    /// bedroom, clear of the shipped furniture, the partitions and the doorway lanes, every id a unique
    /// <c>storage.*</c> id and every Def on disk.</para>
    ///
    /// <para><b>The ids are save keys</b> (<see cref="HiddenHarbours.Core.StoredContainers"/>,
    /// <see cref="HiddenHarbours.Core.KitchenStores"/>): the placement step writes <see cref="Row.StorageId"/>
    /// into the placed rack's <c>_id</c> (or uses it as the kitchen store's key). Append-only: renaming one
    /// orphans what the player left in it.</para>
    ///
    /// <para><b>Frame.</b> The ROOM's model metres, the frame of <see cref="StPetersInteriors.FurnishingsFor"/>
    /// and <see cref="StPetersInteriors.UpperLevelFor"/>: origin the floor centre, <c>+y</c> toward the front
    /// door, <c>+x</c> to the LEFT (north) as you come in. Ginny's upstairs: the player's room is the front
    /// room, x from the south wall (-3.3) to the corridor partition (1.7), y from the bedroom partition (-0.70)
    /// to the front wall (4.25). Both pieces stand against the bedroom partition, clear of the bed on the
    /// south wall, of the dresser and of his door off the corridor.</para>
    ///
    /// <para><b>The camper and the shed have no coordinates yet</b>: the camper lot is the camper lane's
    /// (docs/world/camper-home-plan.md, on its own branch). Their rows carry the id and the Def, so the save
    /// keys and capacities are fixed now and the camper lane only adds a position.</para>
    ///
    /// <para><b>Footprints are planning sizes</b>, not art. The placement step is <see cref="HomeStoragePlacement"/>
    /// (rows of <see cref="InteriorFixturePlacement"/>); its greybox prefabs are drawn and collide at these sizes.</para>
    /// </summary>
    public static class HomeStoragePlan
    {
        /// <summary>What a row is. Append-only.</summary>
        public enum Kind
        {
            RodRack = 0,
            GearLocker = 1,
            Freezer = 2,
            Fridge = 3,
            Pantry = 4,
        }

        /// <summary>A rack or locker (<c>StowageDef</c>, Data/Stowage).</summary>
        public static bool IsStowage(Kind kind) => kind == Kind.RodRack || kind == Kind.GearLocker;

        /// <summary>A catch freezer (<c>FreezerDef</c>, Data/Stowage; the <c>CamperFreezer</c> component, whose
        /// storage id is the fixed <c>StorageIds.CamperFreezer</c>). Everything else is a food store
        /// (<c>KitchenStorageDef</c>, Data/Resources/Kitchen/Storage).</summary>
        public static bool IsCatchFreezer(Kind kind) => kind == Kind.Freezer;

        /// <summary>Planning footprint in metres, as placed: <c>x</c> across the room, <c>y</c> along it. A rod
        /// rack is a shallow board of uprights against a wall; a locker is a tall cabinet.</summary>
        public static Vector2 Footprint(Kind kind) => kind switch
        {
            Kind.RodRack => new Vector2(0.90f, 0.30f),
            Kind.GearLocker => new Vector2(0.60f, 0.60f),
            Kind.Freezer => new Vector2(1.10f, 0.70f),
            Kind.Fridge => new Vector2(0.70f, 0.70f),
            Kind.Pantry => new Vector2(0.80f, 0.40f),
            _ => Vector2.zero,
        };

        /// <summary>One planned container.</summary>
        public readonly struct Row
        {
            public readonly string StorageId;   // the save key, storage.*
            public readonly string DefId;       // stowage.*, freezer.* or kitchen_storage.*
            public readonly Kind Kind;
            public readonly string BuildingKey; // snake_case
            public readonly string RoomKey;     // the room sheet the level draws, or null
            public readonly string PlanKey;     // the UpperLevelFor plan key, or null
            public readonly int Level;
            public readonly bool HasPosition;   // false = the camper lane places it
            public readonly Vector2 At;

            public Row(string storageId, string defId, Kind kind, string buildingKey, string roomKey,
                       string planKey, int level, bool hasPosition, Vector2 at)
            {
                StorageId = storageId; DefId = defId; Kind = kind; BuildingKey = buildingKey; RoomKey = roomKey;
                PlanKey = planKey; Level = level; HasPosition = hasPosition; At = at;
            }
        }

        public const string Scene = "Assets/_Project/Scenes/StPeters.unity";

        /// <summary>The camper's deed opens the camper's and the shed's stores (their Defs say so).</summary>
        public const string CamperDeedHomeId = "home.ginny_lot_camper";

        static readonly Row[] _rows =
        {
            // ---- Ginny's: his room upstairs, open with no deed (lodging) -------------------------------
            new Row("storage.stpeters.ginny_rod_rack", "stowage.ginny_rod_rack", Kind.RodRack,
                    "ginny_cottage", StPetersGinnyPlot.CottageKey, StPetersInteriors.GinnyCottagePlanKey, 1,
                    true, new Vector2(-2.10f, -0.40f)),   // against the bedroom partition, south of the locker
            new Row("storage.stpeters.ginny_gear_locker", "stowage.ginny_gear_locker", Kind.GearLocker,
                    "ginny_cottage", StPetersGinnyPlot.CottageKey, StPetersInteriors.GinnyCottagePlanKey, 1,
                    true, new Vector2(-0.60f, -0.40f)),   // the same partition, 1.5 m north, its top clear of his doorway lane

            // ---- the camper: opens with its deed --------------------------------------------------------
            new Row("storage.stpeters.camper_rod_rack", "stowage.camper_rod_rack", Kind.RodRack,
                    "ginny_lot_camper", null, null, 0, false, Vector2.zero),
            new Row("storage.stpeters.camper_gear_locker", "stowage.camper_gear_locker", Kind.GearLocker,
                    "ginny_lot_camper", null, null, 0, false, Vector2.zero),
            new Row(HiddenHarbours.Core.StorageIds.CamperFreezer, "freezer.camper_freezer", Kind.Freezer,
                    "ginny_lot_camper", null, null, 0, false, Vector2.zero),
            new Row("storage.stpeters.camper_fridge", "kitchen_storage.camper_fridge", Kind.Fridge,
                    "ginny_lot_camper", null, null, 0, false, Vector2.zero),
            new Row("storage.stpeters.camper_pantry", "kitchen_storage.camper_pantry", Kind.Pantry,
                    "ginny_lot_camper", null, null, 0, false, Vector2.zero),

            // ---- the small shed on the camper lot: opens with the camper's deed -------------------------
            new Row("storage.stpeters.shed_rod_rack", "stowage.shed_rod_rack", Kind.RodRack,
                    "ginny_lot_shed", null, null, 0, false, Vector2.zero),
            new Row("storage.stpeters.shed_gear_locker", "stowage.shed_gear_locker", Kind.GearLocker,
                    "ginny_lot_shed", null, null, 0, false, Vector2.zero),
        };

        /// <summary>Every planned container.</summary>
        public static IReadOnlyList<Row> Rows => _rows;
    }
}
#endif
