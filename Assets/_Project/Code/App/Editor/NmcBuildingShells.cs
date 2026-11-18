#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>NINE MILE CREEK'S MISSING BUILDINGS, AS GREYBOX SHELLS</b> (owner, 2026-11-16: "Add the new proposed
    /// buildings in NMC the gym, fish plant etc."). Each building is one prefab, built by
    /// <see cref="BuildPrefabs"/> from the greybox kit (<c>Square.png</c>) and the region's existing set-piece art,
    /// and one named text patch that stands an instance of it under <see cref="RootName"/>.
    ///
    /// <para><b>Seamless interiors (ADR 0036).</b> Every shell carries a <see cref="BuildingInterior"/>: the solid
    /// block yields to its floor and working layout as the player walks through the door, with no scene load and
    /// no camera cut, and the walls (<see cref="BuildingInteriorStander.BuildWalls"/>, the recipe every room uses)
    /// block from both sides with the doorway the one gap. The footprint follows the room convention: one metre
    /// north draws <see cref="IsoGround.GroundDepthScale"/> up the screen.</para>
    ///
    /// <para><b>Greybox, said plainly.</b> No exterior or interior art exists for these buildings yet (the fish
    /// plant's rig, #1203, is unbaked). The shell is one flat block, the floor and its working zones are tinted
    /// quads, and the machines are plain blocks sized from the plans. Outside, the plant's yard lights, ice box,
    /// totes, skip and wheel stops are the fuel stop's and landing's existing sliced set pieces. No new binary art.
    /// The art lane replaces the block; the step keeps the footprint.</para>
    ///
    /// <para><b>Why a text patch.</b> Opening and re-saving the 13 MB scene rewrites most of it, so it is changed
    /// only by named patches (N1 decision 1(b), <see cref="StPetersLayerRefresh"/>). Each step plans from the
    /// committed text, adds one prefab instance, clears the meadow and any planting off the footprint, and
    /// re-plans to empty.</para>
    ///
    /// <para><b>Shells only.</b> No plant jobs, no gym workouts, no fees, no NPC defs and no save field: each is
    /// a follow-up for its own lane.</para>
    /// </summary>
    public static class NmcBuildingShells
    {
        public const string RootName = "NmcBuildings";
        public const string PrefabFolder = "Assets/_Project/Prefabs/Buildings/NineMileCreek";
        public const string PatchFolder = "artifacts/nmc-buildings";
        public const string ScenePath = "Assets/_Project/Scenes/NineMileCreek.unity";
        const string SquareSprite = "Assets/_Project/Art/Sprites/Square.png";
        const string SetPieceFolder = "Assets/_Project/Data/SetPieces";

        /// <summary>Sorting for the flat greybox layers inside: the room floor is the room order every interior
        /// uses (behind any occupant); the zone tints sit one above it, still below the Y-sort band.</summary>
        const int FloorOrder = InteriorCatalog.RoomSortingOrder;
        const int ZoneOrder = InteriorCatalog.RoomSortingOrder + 1;

        /// <summary>The ground mark at a doorway, outside the wall: below every Y-sorted thing.</summary>
        const int DoorStepOrder = InteriorCatalog.RoomSortingOrder;

        /// <summary>Margin round a footprint that the meadow and any planting are cleared from, metres.</summary>
        public const float ClearMarginMetres = 1.5f;

        /// <summary>How far the doorstep mark runs out from the doorway, metres.</summary>
        const float DoorStepMetres = 1f;

        // ---- the data a shell is built from --------------------------------------------------------

        /// <summary>A tinted rectangle of floor, in the room's model frame (metres; +y is the plan's north at facing 0).</summary>
        public readonly struct Zone
        {
            public readonly string Name; public readonly Rect Model; public readonly Color Tint; public readonly bool Public;
            public Zone(string name, float x0, float x1, float y0, float y1, Color tint, bool isPublic = false)
            { Name = name; Model = Rect.MinMaxRect(x0, y0, x1, y1); Tint = tint; Public = isPublic; }
        }

        /// <summary>A greybox block standing on the floor (a machine, a counter, a rail): its footprint in the
        /// model frame, its height, and whether it blocks walking.</summary>
        public readonly struct Block
        {
            public readonly string Name; public readonly Rect Model; public readonly float HeightMetres; public readonly Color Tint;
            public readonly bool Solid;
            public Block(string name, float x0, float x1, float y0, float y1, float height, Color tint, bool solid)
            { Name = name; Model = Rect.MinMaxRect(x0, y0, x1, y1); HeightMetres = height; Tint = tint; Solid = solid; }
        }

        /// <summary>An existing set piece stood outside the shell: which Def, where (world metres from the
        /// building's centre) and at which rig facing.</summary>
        public readonly struct Dressing
        {
            public readonly string Name, SetPiece; public readonly Vector2 Offset; public readonly int Dir;
            public Dressing(string name, string setPiece, float dx, float dy, int dir = 4)
            { Name = name; SetPiece = setPiece; Offset = new Vector2(dx, dy); Dir = dir; }
        }

        /// <summary>A piece of equipment that has a Def (a <see cref="GymEquipmentDef"/>): its id, the model-frame
        /// corner it stands from, and its height. Its footprint is the Def's own <see cref="GymEquipmentDef.FootprintTiles"/>,
        /// so the layout and the data cannot disagree about its size.</summary>
        public readonly struct Fitting
        {
            public readonly string DefId; public readonly Vector2 From; public readonly float HeightMetres; public readonly bool Solid;
            public Fitting(string defId, float x0, float y0, float height, bool solid)
            { DefId = defId; From = new Vector2(x0, y0); HeightMetres = height; Solid = solid; }
        }

        /// <summary>One building: what it is, where it stands, its footprint and door, and its greybox layout.</summary>
        public sealed class Shell
        {
            public string Name, Step, Why;
            public Vector2 Centre;
            public float WidthMetres, LengthMetres, EavesMetres;
            /// <summary>The room's facing of <see cref="Facings"/>: 0 puts the model's +y north; 2 turns it east.</summary>
            public int Facing;
            public bool DoorOnPlusY;
            public float DoorAcrossMetres;
            public Color ShellTint, FloorTint;
            public Zone[] Zones = Array.Empty<Zone>();
            public Block[] Blocks = Array.Empty<Block>();
            public Fitting[] Fittings = Array.Empty<Fitting>();
            public Dressing[] Outside = Array.Empty<Dressing>();
            /// <summary>The trade this building houses, as its <c>SellerDef</c> id; empty for a building with no
            /// counter. The shell only names it: offers and stock stay where they are until the owner rules.</summary>
            public string SellerId = "";
            public string PrefabPath => PrefabFolder + "/" + Name + ".prefab";
        }

        // ---- the palette (greybox: legible, not final) ----------------------------------------------

        static readonly Color ShellGrey = new Color(0.72f, 0.74f, 0.76f);
        static readonly Color Concrete = new Color(0.62f, 0.63f, 0.63f);
        static readonly Color PublicFloor = new Color(0.80f, 0.77f, 0.70f);
        static readonly Color WetFloor = new Color(0.55f, 0.62f, 0.60f);
        static readonly Color ColdFloor = new Color(0.60f, 0.68f, 0.78f);
        static readonly Color StaffFloor = new Color(0.70f, 0.66f, 0.60f);
        static readonly Color DryFloor = new Color(0.68f, 0.64f, 0.55f);
        static readonly Color PlantFloor = new Color(0.50f, 0.50f, 0.52f);
        static readonly Color Steel = new Color(0.78f, 0.80f, 0.82f);
        static readonly Color DarkSteel = new Color(0.40f, 0.42f, 0.45f);
        static readonly Color Rail = new Color(0.85f, 0.70f, 0.20f);
        static readonly Color Partition = new Color(0.58f, 0.58f, 0.60f);
        static readonly Color Wood = new Color(0.55f, 0.40f, 0.26f);
        static readonly Color ToteBlue = new Color(0.25f, 0.45f, 0.70f);
        static readonly Color DoorMark = new Color(0.30f, 0.30f, 0.32f);
        static readonly Color StorefrontBlue = new Color(0.60f, 0.67f, 0.74f);
        static readonly Color WarehouseTan = new Color(0.70f, 0.66f, 0.56f);
        static readonly Color GlassCase = new Color(0.62f, 0.78f, 0.82f);
        static readonly Color Porcelain = new Color(0.92f, 0.92f, 0.90f);
        static readonly Color PotGreen = new Color(0.36f, 0.52f, 0.38f);
        static readonly Color RopeYellow = new Color(0.82f, 0.72f, 0.40f);

        // ---- the fish plant --------------------------------------------------------------------------

        /// <summary>
        /// <b>The fish plant</b> (owner's ruling of 2026-11-12: a key scene, site A). Centre (-265, -40), the brief's
        /// 48 x 28 m single storey, public door on the south wall at u = -4 (model x +20, world x -245), so the
        /// visitor's route is reception, receiving aisle, the delivery edge of weigh and grade, intake I1
        /// (<c>docs/design/nmc-fish-plant-key-scene-brief.md</c> section 2). The brief's zones are laid out in its
        /// own local frame: model x = u + 24, model y = v. The production floor is fenced off at the delivery edge
        /// (the brief: players read the working floor from there and do not walk through running equipment); the
        /// staff rooms, cold stores and dock are real allocated floor behind closed thresholds until the job lane
        /// opens them. The 26 m truck court east of the east facade is left as ground: its paving, gate, spur and
        /// trucks are the road and vehicle lanes'.
        /// </summary>
        public static readonly Shell FishPlant = new Shell
        {
            Name = "NmcFishPlant", Step = "20-fish-plant",
            Why = "the fish plant at site A (ruling 11-12, key scene): inland, truck-fed, door to Route 91's court",
            Centre = new Vector2(-265f, -40f),
            WidthMetres = 48f, LengthMetres = 28f, EavesMetres = 6f,
            DoorOnPlusY = false, DoorAcrossMetres = 20f,
            ShellTint = ShellGrey, FloorTint = Concrete,
            Zones = new[]
            {
                // the south row: services, staff, office, the public reception
                new Zone("PlantServices", -24, -14, -14, -8, PlantFloor),
                new Zone("StaffEntranceWash", -14, -4, -14, -8, StaffFloor),
                new Zone("StaffRoom", -4, 6, -14, -8, StaffFloor),
                new Zone("Office", 6, 16, -14, -8, StaffFloor),
                new Zone("Reception", 16, 24, -14, -8, PublicFloor, true),
                // the cold side
                new Zone("RawChilledBuffer", -24, -10, -8, -2, ColdFloor),
                new Zone("FinishedColdStore", -24, -10, -2, 6, ColdFloor),
                new Zone("LobsterCell", -24, -10, 6, 10, ColdFloor),
                new Zone("Refrigeration", -24, -10, 10, 14, PlantFloor),
                // the lines
                new Zone("FilletLine", -10, 6, -8, 0, WetFloor),
                new Zone("CanningLine", -10, 6, 0, 10, WetFloor),
                new Zone("DryStore", -10, 6, 10, 14, DryFloor),
                // weigh, pack, dispatch
                new Zone("WeighAndGrade", 6, 16, -8, 2, WetFloor),
                new Zone("Packing", 6, 18, 2, 10, WetFloor),
                new Zone("DispatchStaging", 6, 18, 10, 14, DryFloor),
                // the east strip: the public aisle to intake I1, then the working dock
                new Zone("DeliveryEdge", 16, 18, -8, 2, PublicFloor, true),
                new Zone("ReceivingAisle", 18, 24, -8, -2, PublicFloor, true),
                new Zone("IntakeI1", 18, 24, -2, 2, PublicFloor, true),
                new Zone("ReturnsWash", 18, 24, 2, 6, WetFloor),
                new Zone("IntakeI2", 18, 24, 6, 10, Concrete),
                new Zone("OutboundDock", 18, 24, 10, 14, Concrete),
            },
            Blocks = new[]
            {
                // the public area's edges: the office wall (its counter faces the reception), the delivery-edge
                // rail over the grading floor, and the rail that closes the returns wash.
                new Block("OfficeWall", 15.85f, 16.15f, -14f, -8f, 3f, Partition, true),
                new Block("DeliveryEdgeRail", 15.85f, 16.15f, -8f, 2f, 1.1f, Rail, true),
                new Block("ReturnsRail", 16.15f, 24f, 1.85f, 2.15f, 1.1f, Rail, true),
                new Block("OfficeCounter", 16.15f, 16.85f, -13f, -10f, 1.05f, Wood, true),
                new Block("ReceptionBench", 21.5f, 23.5f, -13.7f, -13.2f, 0.45f, Wood, true),
                new Block("IntakeTotes", 21.5f, 23f, -1.2f, 0.2f, 0.9f, ToteBlue, true),
                // the production floor, seen over the rail
                new Block("IntakeScale", 12.5f, 14.5f, -7f, -5f, 0.4f, DarkSteel, false),
                new Block("GradingTable", 8f, 14f, -3f, -1.8f, 0.9f, Steel, false),
                new Block("FilletTableA", -8f, 4f, -6.6f, -5.4f, 0.9f, Steel, false),
                new Block("FilletTableB", -8f, 4f, -3.1f, -1.9f, 0.9f, Steel, false),
                new Block("CanningConveyor", -8f, 4f, 2.5f, 3.5f, 1.0f, Steel, false),
                new Block("Filler", -9f, -6f, 6f, 8.5f, 2.2f, DarkSteel, false),
                new Block("Retort", 1f, 4f, 6f, 8.5f, 2.6f, DarkSteel, false),
                new Block("PackingTableA", 7.5f, 11.5f, 4.5f, 6f, 0.9f, Steel, false),
                new Block("PackingTableB", 12.5f, 16.5f, 4.5f, 6f, 0.9f, Steel, false),
                new Block("ColdStoreDoorA", -10.15f, -9.85f, 1f, 3.4f, 2.4f, DoorMark, false),
                new Block("ColdStoreDoorB", -10.15f, -9.85f, -6f, -3.6f, 2.4f, DoorMark, false),
                new Block("ColdStoreWall", -10.15f, -9.85f, -8f, 10f, 3f, Partition, false),
                new Block("DryStoreRacks", -8f, 4f, 11.5f, 13f, 2.4f, Wood, false),
                new Block("DispatchPallets", 8f, 16f, 11f, 13f, 1.2f, Wood, false),
            },
            Outside = new[]
            {
                // the court's two yard lights and the intake's ice box and totes (key-scene pieces, existing art)
                new Dressing("YardLightSouth", "lampPole", 27f, -6f),
                new Dressing("YardLightNorth", "lampPole", 27f, 6f),
                new Dressing("IntakeIceBox", "iceBox", 25.6f, -3.2f),
                new Dressing("IntakeTotesOutside", "landingTotes", 25.2f, 2.6f),
                // the byproduct skip on the west service side, away from the estates' boundary
                new Dressing("ServiceSkip", "dumpster", -27f, 4f, 6),
                // staff parking north of the building (brief: u [-44,-24]): four wheel stops
                new Dressing("StaffParking1", "wheelStop", -18.75f, 12f),
                new Dressing("StaffParking2", "wheelStop", -16.25f, 12f),
                new Dressing("StaffParking3", "wheelStop", -13.75f, 12f),
                new Dressing("StaffParking4", "wheelStop", -11.25f, 12f),
            },
        };

        // ---- the community hall and its gym -------------------------------------------------------------

        /// <summary>
        /// <b>The community hall, with the gym in its back room</b> (owner's ruling of 2026-11-15; <c>docs/world/nmc-gym.md</c>:
        /// "the back room of the community hall ... a seamless door"). The hall stands on the parish hall's town lot
        /// (<c>NineMileCreekMainland.ParishHallPos</c>), inside its post-and-rail fence. The lot's one gate is in its
        /// WEST rail (<c>NineMileCreekYards</c>, (-161.5, 62.8)); its north rail has none, so a door on the north
        /// wall could only be reached round the side. The hall is therefore turned a quarter (facing 2 of 8) so its
        /// door, on the model's -y wall, is the west wall, 2.5 m in from the gate and square on to it.
        ///
        /// <para>Model frame at this facing: model +y runs east (away from the door), model +x runs south. 18 m
        /// across (north to south) by 12 m deep (west to east), one storey: the front hall by the door (model y
        /// [-6, 0]), and behind it the gym (the doc's 8 x 6 floor, the north-east corner), a kitchen and a washroom
        /// (closed for now: the owner's 11-16 ask that halls carry them). The gym's equipment follows the doc (rack
        /// and bench on the back wall, treadmill under the north windows, pull-up bar and grip trainer by its door,
        /// skipping in the middle, a bench by the door), each marker sized from its Def. No workouts: a room for now.</para>
        /// </summary>
        public static readonly Shell CommunityHall = new Shell
        {
            Name = "NmcCommunityHall", Step = "21-community-hall-gym",
            Why = "the community hall on the parish lot, door to the lot's gate, the gym in its back room (ruling 11-15, nmc-gym.md)",
            // Measured against the lot's rail (warm-a census, 11-16): the yard runs x -161.5 to -144.5 at the gate's
            // height and y 53.5 to 70.3; the west wall at x -159 leaves 2.5 m to the gate, the east wall 2.6 m to the
            // rail, the north wall 2.1 m and the south wall 3.2 m. The door's -0.5 m offset puts it at the gate's y.
            Centre = new Vector2(-153f, 62.5f),
            Facing = 2,
            WidthMetres = 18f, LengthMetres = 12f, EavesMetres = 5f,
            DoorOnPlusY = false, DoorAcrossMetres = -0.5f,
            ShellTint = new Color(0.78f, 0.72f, 0.62f), FloorTint = Wood,
            Zones = new[]
            {
                new Zone("FrontHall", -9, 9, -6, 0, PublicFloor, true),
                new Zone("Gym", -9, -1, 0, 6, StaffFloor, true),
                new Zone("Kitchen", -1, 4, 0, 6, WetFloor),
                new Zone("Washroom", 4, 9, 0, 6, ColdFloor),
            },
            Blocks = new[]
            {
                // the back-room wall, with the gym's doorway (the room convention's width) at model x -5
                new Block("BackWallNorth", -9f, -5.7f, -0.15f, 0.15f, 3f, Partition, true),
                new Block("BackWallSouth", -4.3f, 9f, -0.15f, 0.15f, 3f, Partition, true),
                new Block("GymKitchenWall", -1.15f, -0.85f, 0.15f, 6f, 3f, Partition, true),
                new Block("KitchenWashroomWall", 3.85f, 4.15f, 0.15f, 6f, 3f, Partition, true),
                new Block("GymBench", -8.8f, -6.8f, 0.2f, 0.6f, 0.45f, Wood, true),
                new Block("HallStage", 6.5f, 8.7f, -5f, -1f, 0.6f, Wood, false),
            },
            Fittings = new[]
            {
                new Fitting("gym_equipment.squat_rack", -8.8f, 3.8f, 2.2f, true),
                new Fitting("gym_equipment.bench_press", -6.2f, 3.8f, 0.5f, true),
                new Fitting("gym_equipment.treadmill", -8.8f, 1.0f, 1.3f, true),
                new Fitting("gym_equipment.rowing_machine", -2.2f, 2.6f, 0.6f, true),
                new Fitting("gym_equipment.pull_up_bar", -4.0f, 0.3f, 2.3f, false),
                new Fitting("gym_equipment.grip_trainer", -2.6f, 0.3f, 0.9f, false),
                new Fitting("gym_equipment.skipping_rope", -5.5f, 2.4f, 0.05f, false),
            },
        };

        // ---- the two wholesalers (owner's ruling of 2026-11-16) ----------------------------------------

        /// <summary>
        /// <b>The marine electronics and boat gear wholesaler.</b> Owner, 11-16: "The loran and all current boat
        /// electronics will be sold at a boat/gear wholesaler in NMC this is a new building. Its not a boatyard
        /// which does hull sales and engine upgrades." It takes the Water Street lot the main street key scene
        /// calls the east block (<c>structure.nmc_main_street_east_block</c>, no trade assigned), just east of the
        /// chandlery's lot, and stands in for that block's ground floor until the key scene is placed (the
        /// placement then takes this building rather than doubling it).
        ///
        /// <para>Census (11-17, the committed scene and the road network): the south wall stands 0.3 m behind the
        /// frontage walk's edge, so the door opens onto the walk; Water Street's carriageway is 4 m south, the
        /// east court's turning end and the east lane further off to the north. Nothing placed stands on the lot.
        /// 12 m along the street by 9 m deep, one storey of shop: the shop floor with the trade counter, a
        /// glass case for the electronics and shelf markers, and behind it the stock room, with the staff
        /// microwave and the staff washroom (toilet and sink) that the kitchens and bathrooms plan gives every
        /// shop (<c>docs/world/kitchens-bathrooms-placement-plan.md</c>, "Shops get a staff Toilet and Sink in
        /// the back room, plus a staff Micro").</para>
        ///
        /// <para>No offer moves here yet: the stock and its prices are a proposal the owner has not ruled on.</para>
        /// </summary>
        public static readonly Shell BoatGearWholesaler = new Shell
        {
            Name = "NmcBoatGearWholesaler", Step = "22-boat-gear-wholesaler",
            Why = "the marine electronics and boat gear wholesaler on Water Street (ruling 11-16), the east block's lot",
            SellerId = "seller.nmc_boat_gear_wholesaler",
            Centre = new Vector2(-148.4f, 235.9f),
            WidthMetres = 12f, LengthMetres = 9f, EavesMetres = 7f,
            DoorOnPlusY = false, DoorAcrossMetres = 0f,
            ShellTint = StorefrontBlue, FloorTint = PublicFloor,
            Zones = new[]
            {
                new Zone("ShopFloor", -6, 6, -4.5f, 1.2f, PublicFloor, true),
                new Zone("StockRoom", -6, 3.6f, 1.2f, 4.5f, DryFloor),
                new Zone("StaffWashroom", 3.6f, 6, 1.2f, 4.5f, ColdFloor),
            },
            Blocks = new[]
            {
                // the stock room's wall, with its doorway (the room convention's width) behind the counter
                new Block("BackWallWest", -6f, -0.1f, 1.05f, 1.35f, 3f, Partition, true),
                new Block("BackWallEast", 1.3f, 6f, 1.05f, 1.35f, 3f, Partition, true),
                // the washroom opens off the stock room
                new Block("WashroomWall", 3.45f, 3.75f, 1.35f, 2.8f, 3f, Partition, true),
                // the shop: the trade counter, the electronics case on the back wall, shelf markers
                new Block("TradeCounter", 1.8f, 4.4f, -0.9f, -0.4f, 1.05f, Wood, true),
                new Block("ElectronicsCase", -4.2f, -1.0f, 0.45f, 0.95f, 1.2f, GlassCase, true),
                new Block("WallShelfWest", -5.7f, -5.2f, -3.6f, 0.9f, 2.0f, Wood, true),
                new Block("ShelfGondola", -3.4f, -2.6f, -3.0f, -1.0f, 1.5f, Wood, true),
                // the stock room: shelving, a pallet, the staff microwave
                new Block("StockShelving", -5.7f, -1.0f, 3.6f, 4.2f, 2.2f, Wood, true),
                new Block("StockPallet", -5.0f, -3.6f, 1.8f, 3.0f, 1.0f, Wood, true),
                new Block("StaffMicrowave", 1.6f, 3.2f, 3.7f, 4.2f, 0.95f, Steel, true),
                // the washroom's fixtures (markers)
                new Block("Toilet", 4.8f, 5.5f, 3.3f, 4.1f, 0.45f, Porcelain, false),
                new Block("Sink", 4.0f, 4.6f, 3.7f, 4.2f, 0.85f, Porcelain, false),
            },
        };

        /// <summary>
        /// <b>The fishing gear wholesaler.</b> Owner, 11-16: "There is also a fishing gear wholesaler in NMC which
        /// is another building". A warehouse west of the lumber yard's fence (the plaza key scene's lumber shed
        /// stands at (-279, 111), its fence at x -288), on open meadow that the census (11-17) found clear of
        /// every road, path and placed thing; the yard's out-drive is 10 m east. The customer door is in the
        /// south wall, the face the view reads (a north door stands behind the shell and hides the player at
        /// it); the ground south of the wall is open meadow down to the hedge line, and the 4 m strip between
        /// the east wall and the yard fence leads round to it from the out-drive (no walk is laid yet: a
        /// follow-up for the road lane).
        ///
        /// <para>18 m by 14 m, one storey. At the front, by the door: the trade floor with the counter and shelf
        /// markers, and a bulk display of pots, rope and net bales. Behind, through a doorway behind the counter:
        /// the warehouse with pallet racking, and off it the staff room with the staff microwave and the staff
        /// washroom (the kitchens and bathrooms plan's shop rule).</para>
        ///
        /// <para>No offer moves here yet, as for the boat gear wholesaler.</para>
        /// </summary>
        public static readonly Shell FishingGearWholesaler = new Shell
        {
            Name = "NmcFishingGearWholesaler", Step = "23-fishing-gear-wholesaler",
            Why = "the fishing gear wholesaler's warehouse west of the lumber yard (ruling 11-16)",
            SellerId = "seller.nmc_fishing_gear_wholesaler",
            Centre = new Vector2(-301f, 125f),
            WidthMetres = 18f, LengthMetres = 14f, EavesMetres = 6f,
            DoorOnPlusY = false, DoorAcrossMetres = 2f,
            ShellTint = WarehouseTan, FloorTint = Concrete,
            Zones = new[]
            {
                new Zone("TradeFloor", -1, 9, -7, -0.5f, PublicFloor, true),
                new Zone("BulkDisplay", -9, -1, -7, -0.5f, PublicFloor, true),
                new Zone("Warehouse", -9, 4.5f, -0.5f, 7, DryFloor),
                new Zone("StaffRoom", 4.5f, 9, -0.5f, 3.5f, StaffFloor),
                new Zone("StaffWashroom", 4.5f, 9, 3.5f, 7, ColdFloor),
            },
            Blocks = new[]
            {
                // the back wall, with the warehouse doorway behind the counter, in line with the front door
                new Block("BackWallWest", -9f, 1.3f, -0.65f, -0.35f, 3.5f, Partition, true),
                new Block("BackWallEast", 2.7f, 9f, -0.65f, -0.35f, 3.5f, Partition, true),
                // the staff room opens off the warehouse, the washroom off the staff room
                new Block("StaffWallSouth", 4.35f, 4.65f, -0.35f, 0.6f, 3f, Partition, true),
                new Block("StaffWallNorth", 4.35f, 4.65f, 2.0f, 7f, 3f, Partition, true),
                new Block("WashroomWallWest", 4.65f, 6.5f, 3.35f, 3.65f, 3f, Partition, true),
                new Block("WashroomWallEast", 7.9f, 9f, 3.35f, 3.65f, 3f, Partition, true),
                // the trade floor: the counter east of the way in (the seller stands behind it), shelf markers
                new Block("TradeCounter", 3.0f, 7.5f, -3.0f, -2.4f, 1.05f, Wood, true),
                new Block("WallShelfEast", 8.2f, 8.7f, -6.4f, -1.0f, 2.2f, Wood, true),
                new Block("TackleGondola", 0.2f, 1.0f, -6.0f, -3.5f, 1.6f, Wood, true),
                // the bulk display
                new Block("PotStack", -5.0f, -3.0f, -6.5f, -4.5f, 1.8f, PotGreen, true),
                new Block("NetBales", -5.0f, -3.4f, -2.6f, -1.2f, 1.2f, DarkSteel, true),
                new Block("RopeCoils", -2.6f, -1.4f, -2.4f, -1.2f, 0.8f, RopeYellow, true),
                // the warehouse: pallet racking and a floor stack, clear of the aisle from the doorway
                new Block("PalletRackWest", -8.6f, -7.4f, 0.4f, 6.6f, 3.2f, Steel, true),
                new Block("PalletRackNorth", -6.5f, 3.8f, 5.6f, 6.6f, 3.2f, Steel, true),
                new Block("FloorPallets", -4f, 0f, 1.5f, 3.5f, 1.4f, Wood, true),
                // the staff fixtures (markers)
                new Block("StaffMicrowave", 8.3f, 8.8f, 0.4f, 1.8f, 0.95f, Steel, true),
                new Block("Toilet", 7.9f, 8.6f, 5.8f, 6.6f, 0.45f, Porcelain, false),
                new Block("Sink", 5.0f, 5.8f, 6.2f, 6.7f, 0.85f, Porcelain, false),
            },
        };

        /// <summary>Every shell this file places, in step order.</summary>
        public static readonly Shell[] All = { FishPlant, CommunityHall, BoatGearWholesaler, FishingGearWholesaler };

        /// <summary>The metres a Def's footprint tile covers (<c>nmc-gym.md</c>: one tile, one metre).</summary>
        public const float TileMetres = 1f;

        // ---- footprints ------------------------------------------------------------------------------

        /// <summary>The room convention's facing count: eight, a turn of 45 degrees each.</summary>
        public const int Facings = 8;

        /// <summary>The shell's room footprint at its placed centre, at its facing of <see cref="Facings"/>.</summary>
        public static InteriorFootprint FootprintOf(Shell s) => FootprintAt(s, s.Centre);

        public static InteriorFootprint FootprintAt(Shell s, Vector2 centre)
        {
            if (s.Facing % 2 != 0) throw new Refusal($"{s.Name}: facing {s.Facing} is a diagonal; the greybox draws square blocks only.");
            return new InteriorFootprint(centre, s.WidthMetres, s.LengthMetres, s.Facing, Facings, IsoGround.GroundDepthScale,
                                         s.DoorOnPlusY ? 1f : -1f, s.DoorAcrossMetres);
        }

        /// <summary>The ground the building stands on, in world units (axis-aligned at the four square facings).</summary>
        public static Rect WorldRect(Shell s)
        {
            Vector2[] c = FootprintOf(s).Corners();
            return Rect.MinMaxRect(c.Min(p => p.x), c.Min(p => p.y), c.Max(p => p.x), c.Max(p => p.y));
        }

        /// <summary>The footprint grown by <see cref="ClearMarginMetres"/>: what the meadow and planting step off.</summary>
        public static Rect ClearRect(Shell s)
        {
            Rect r = WorldRect(s);
            return Rect.MinMaxRect(r.xMin - ClearMarginMetres, r.yMin - ClearMarginMetres,
                                   r.xMax + ClearMarginMetres, r.yMax + ClearMarginMetres);
        }

        /// <summary>The world point of the shell's doorway: the threshold the player crosses.</summary>
        public static Vector2 DoorWorld(Shell s) => FootprintOf(s).DoorWorld;

        // ---- the prefabs -----------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/World/Nine Mile Creek Buildings/Build the Shell Prefabs")]
        public static void BuildPrefabs()
        {
            Directory.CreateDirectory(PrefabFolder);
            Sprite square = AssetDatabase.LoadAllAssetsAtPath(SquareSprite).OfType<Sprite>().FirstOrDefault()
                            ?? throw new Refusal($"no sprite in {SquareSprite}; the greybox kit is missing.");
            // Only the missing ones: a rebuilt prefab gets fresh random file ids, which orphans every override
            // the region scene holds against the old ones. Delete a prefab first to rebuild it on purpose.
            foreach (Shell s in All)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(s.PrefabPath) == null) BuildPrefab(s, square);
            AssetDatabase.SaveAssets();
        }

        static void BuildPrefab(Shell s, Sprite square)
        {
            var root = new GameObject(s.Name);
            try
            {
                // Built at the origin so every collider path is local = world; the instance moves it.
                InteriorFootprint fp = FootprintAt(s, Vector2.zero);
                float h = IsoGround.HeightScale;
                float hl = s.LengthMetres * 0.5f;
                Rect floor = Bounds(fp.Corners());

                // the shell: one block from the front wall's foot to the roof's ridge line, sorted at its foot.
                var shellGo = Child(root, "Shell");
                SpriteRenderer shell = Quad(shellGo, square, s.ShellTint,
                    Rect.MinMaxRect(floor.xMin, floor.yMin, floor.xMax, floor.yMax + s.EavesMetres * h), sortAtFoot: true);

                // the room: the floor, behind any occupant.
                var roomGo = Child(root, BuildingInteriorStander.RoomChildName);
                SpriteRenderer room = Quad(roomGo, square, s.FloorTint, floor, sortAtFoot: false);
                room.sortingOrder = FloorOrder;

                // the working layout, switched with the room.
                var props = Child(root, BuildingInteriorStander.PropsChildName);
                foreach (Zone z in s.Zones)
                {
                    var zg = Child(props, "Zone_" + z.Name);
                    SpriteRenderer zr = Quad(zg, square, z.Tint, ModelRect(fp, z.Model, 0f), sortAtFoot: false);
                    zr.sortingOrder = ZoneOrder;
                }
                Block[] layout = Layout(s);
                foreach (Block b in layout)
                    Quad(Child(props, b.Name), square, b.Tint, ModelRect(fp, b.Model, b.HeightMetres * h), sortAtFoot: true);

                // the walls: the shared recipe, then the partitions and solid fittings (always on: a wall is a wall
                // from both sides, and from outside they are unreachable behind the shell's own walls).
                BuildingInteriorStander.BuildWalls(root.transform, fp);
                var inner = Child(root, "InnerWalls");
                foreach (Block b in layout.Where(b => b.Solid))
                {
                    var c = inner.AddComponent<PolygonCollider2D>();
                    c.pathCount = 1;
                    c.SetPath(0, fp.Quad(b.Model.xMin, b.Model.xMax, b.Model.yMin, b.Model.yMax));
                }

                // the doorstep: a ground mark outside the doorway, so the way in reads from the street.
                Vector2 door = fp.DoorWorld;
                float sign = s.DoorOnPlusY ? 1f : -1f, half = BuildingInteriorStander.DoorwayWidthMetres * 0.5f;
                var step = Child(root, "DoorStep");
                SpriteRenderer stepR = Quad(step, square, DoorMark,
                    Bounds(fp.Quad(s.DoorAcrossMetres - half, s.DoorAcrossMetres + half, sign * hl, sign * (hl + DoorStepMetres))),
                    sortAtFoot: false);
                stepR.sortingOrder = DoorStepOrder;

                // outside: existing set pieces.
                if (s.Outside.Length > 0)
                {
                    var outside = Child(root, "Outside");
                    foreach (Dressing d in s.Outside) StandSetPiece(outside, d);
                }

                var interior = root.AddComponent<BuildingInterior>();
                interior.Configure(shell, room, props.transform, s.WidthMetres, s.LengthMetres, s.Facing, Facings,
                                   IsoGround.GroundDepthScale, BuildingInteriorStander.WallThicknessMetres,
                                   BuildingInteriorStander.DoorwayWidthMetres, s.DoorOnPlusY, s.DoorAcrossMetres);
                PrefabUtility.SaveAsPrefabAsset(root, s.PrefabPath);
                Debug.Log($"[NmcBuildingShells] Built {s.PrefabPath}: {s.WidthMetres} x {s.LengthMetres} m, door at " +
                          $"{s.Centre + door}, {s.Zones.Length} zones, {s.Blocks.Length} blocks, {s.Outside.Length} set pieces outside.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>The shell's blocks and its Def-backed fittings as one list: each fitting sized from its Def's
        /// footprint (x across, y deep, in tiles) and named by its id. Refuses a fitting whose Def is missing.</summary>
        public static Block[] Layout(Shell s)
        {
            if (s.Fittings.Length == 0) return s.Blocks;
            var defs = AssetDatabase.FindAssets("t:" + nameof(GymEquipmentDef))
                .Select(g => AssetDatabase.LoadAssetAtPath<GymEquipmentDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null && !string.IsNullOrEmpty(d.Id))
                .ToDictionary(d => d.Id);
            var all = new List<Block>(s.Blocks);
            foreach (Fitting f in s.Fittings)
            {
                if (!defs.TryGetValue(f.DefId, out GymEquipmentDef def)) throw new Refusal($"{s.Name}: no Def '{f.DefId}'.");
                Vector2 size = new Vector2(def.FootprintTiles.x, def.FootprintTiles.y) * TileMetres;
                all.Add(new Block(f.DefId, f.From.x, f.From.x + size.x, f.From.y, f.From.y + size.y, f.HeightMetres, Steel, f.Solid));
            }
            return all.ToArray();
        }

        static Rect Bounds(Vector2[] pts) =>
            Rect.MinMaxRect(pts.Min(p => p.x), pts.Min(p => p.y), pts.Max(p => p.x), pts.Max(p => p.y));

        static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        /// <summary>A model-frame rectangle on the floor as a world rectangle (facing 0: x straight, y squashed),
        /// raised by <paramref name="heightUp"/> screen units for something that stands up off the floor.</summary>
        static Rect ModelRect(in InteriorFootprint fp, Rect model, float heightUp)
        {
            Vector2 a = fp.ModelToWorld(new Vector2(model.xMin, model.yMin)), b = fp.ModelToWorld(new Vector2(model.xMax, model.yMax));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y) + heightUp);
        }

        /// <summary>The greybox sprite stretched over <paramref name="world"/> (local to the building). With
        /// <paramref name="sortAtFoot"/> it Y-sorts by its bottom edge, where it meets the ground.</summary>
        static SpriteRenderer Quad(GameObject go, Sprite square, Color tint, Rect world, bool sortAtFoot)
        {
            Vector2 size = square.bounds.size;
            go.transform.localPosition = new Vector3(world.center.x, world.center.y, 0f);
            go.transform.localScale = new Vector3(world.width / size.x, world.height / size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = tint;
            if (sortAtFoot)
            {
                go.AddComponent<YSortSprite>().SortPivotYOffset = -world.height * 0.5f;
            }
            return sr;
        }

        /// <summary>An existing sliced set piece, stood at its offset: its frame for the facing, Y-sorted, and a
        /// box over its ground bounds when it does not let you walk through.</summary>
        static void StandSetPiece(GameObject parent, Dressing d)
        {
            string path = SetPieceFolder + "/SetPiece_" + d.SetPiece + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<SetPieceDef>(path) ?? throw new Refusal($"no set piece at {path}.");
            Sprite sprite = def.SpriteFor(SetPieceDef.FrameForRigDir(d.Dir))
                            ?? throw new Refusal($"{path} has no sprite for rig facing {d.Dir}.");
            var go = Child(parent, d.Name);
            go.transform.localPosition = new Vector3(d.Offset.x, d.Offset.y, 0f);
            go.AddComponent<SetPiecePlacement>().Configure(def, "nmc_buildings." + d.Name, d.Dir);
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            go.AddComponent<YSortSprite>();
            // the piece's own walk-blocking footprints, turned for the facing, as every key-scene step writes them
            List<Vector2[]> walls = WallsOf(def.Colliders, SetPieceDef.FrameForRigDir(d.Dir), d.Name);
            if (walls.Count == 0) return;
            var poly = go.AddComponent<PolygonCollider2D>();
            poly.pathCount = walls.Count;
            for (int i = 0; i < walls.Count; i++) poly.SetPath(i, walls[i]);
        }

        // ---- the text patch --------------------------------------------------------------------------

        /// <summary>One prefab as its file holds it: the GUID and the root's documents' ids.</summary>
        public sealed class PrefabFacts
        {
            public string Path, Guid;
            public long GameObject, Transform, Interior;

            public static PrefabFacts Read(string projectRoot, string path)
            {
                string file = System.IO.Path.Combine(projectRoot, path);
                if (!File.Exists(file)) throw new Refusal($"{path} is not on disk; build the shell prefabs first.");
                Match guid = Regex.Match(File.ReadAllText(file + ".meta"), @"(?m)^guid: ([0-9a-f]{32})\s*$");
                if (!guid.Success) throw new Refusal($"{path}.meta has no guid.");
                SceneYaml prefab = SceneYaml.Parse(File.ReadAllText(file).Replace("\r\n", "\n"));
                Doc rootTr = prefab.Docs.Single(d => d.ClassId == 4 && d.FieldRef("m_Father") == 0);
                Doc go = prefab.Require(rootTr.FieldRef("m_GameObject"), path + "'s root");
                Doc interior = prefab.ComponentsOf(go).SingleOrDefault(d => d.IsScript("HiddenHarbours.World.BuildingInterior"))
                               ?? throw new Refusal($"{path}'s root carries no BuildingInterior.");
                return new PrefabFacts
                {
                    Path = path, Guid = guid.Groups[1].Value,
                    GameObject = go.FileId, Transform = rootTr.FileId, Interior = interior.FileId,
                };
            }
        }

        /// <summary>One shell's patches on <paramref name="scene"/>: the instance under <see cref="RootName"/>, then
        /// the meadow and the four planting roots cleared off its footprint. Each is planned on the text the one
        /// before it leaves (the caller applies them in order).</summary>
        public static IEnumerable<Func<SceneYaml, LayerPatch>> Planners(Shell s, PrefabFacts prefab)
        {
            Rect clear = ClearRect(s);
            string why = "under " + s.Name;
            yield return scene => PlanInstance(scene, s, prefab);
            yield return scene => NineMileCreekGroundSteps.PadMeadow(scene, clear, s.Step);
            foreach (string root in new[] { "CreekHedgerows", "CreekTrees", "CreekUnderstorey", "CreekMarsh" })
                yield return scene => NineMileCreekGroundSteps.PadPlanting(scene, root, clear, s.Step, why);
        }

        /// <summary>Every shell's patches over <paramref name="text"/>, in order; <paramref name="after"/> is the
        /// scene they write.</summary>
        public static List<LayerPatch> PlanAll(SceneYaml scene, Func<string, PrefabFacts> prefabs, out SceneYaml after)
        {
            var patches = new List<LayerPatch>();
            foreach (Shell s in All)
                foreach (var plan in Planners(s, prefabs(s.PrefabPath)))
                {
                    LayerPatch patch = plan(scene);
                    patches.Add(patch);
                    scene = patch.Apply(scene);
                }
            after = scene;
            return patches;
        }

        static LayerPatch PlanInstance(SceneYaml scene, Shell s, PrefabFacts prefab)
        {
            Doc root = scene.Docs.SingleOrDefault(d => d.ClassId == 1 && !d.Stripped && d.Field("m_Name") == RootName &&
                                                       scene.TransformOf(d).FieldRef("m_Father") == 0);
            Doc rootTr = root != null ? scene.TransformOf(root) : null;
            if (rootTr != null) RequireIdentity(rootTr, RootName);
            var ids = new IdAllocator(scene);
            long go = root?.FileId ?? ids.Next($"{RootName}/GameObject");
            long tr = rootTr?.FileId ?? ids.Next($"{RootName}/Transform");
            var patch = new LayerPatch(s.Step, RootName, go);
            List<long> standing = rootTr?.FieldRefs("m_Children") ?? new List<long>();
            var children = new List<long>(standing);

            FindInstance(scene, standing, prefab, s.Name, out Doc pi, out Doc piTr);
            long piId = pi?.FileId ?? ids.Next($"{s.Step}/{s.Name}/PrefabInstance");
            long trId = piTr?.FileId ?? ids.Next($"{s.Step}/{s.Name}/Transform");
            long occupant = NineMileCreekWharfLandSide.ReviewOccupant(scene);
            Put(patch, pi, Instance(piId, tr, prefab, s.Name, s.Centre, occupant), s.Name + " (PrefabInstance)");
            Put(patch, piTr, Stripped(trId, piId, prefab), s.Name + "/Transform (stripped)");
            if (!children.Contains(trId)) children.Add(trId);
            if (pi == null) patch.Note(s.Name, "a building shell", $"centre {At(s.Centre)}, door {At(DoorWorld(s))}: {s.Why}");

            Put(patch, root, RootGameObject(go, tr), RootName);
            Put(patch, rootTr, RootTransform(tr, go, children), RootName + "/Transform");
            if (root == null)
            {
                Doc list = SceneRootsOf(scene);
                patch.Edit(list, WithRootListed(list.Text, tr), $"the scene's SceneRoots list ('{RootName}' joins it)");
                patch.Note(RootName, "a new root", "the region's new buildings' parent, at the origin");
            }
            return patch.Seal(scene);
        }

        static void FindInstance(SceneYaml scene, List<long> children, PrefabFacts prefab, string name, out Doc pi, out Doc piTr)
        {
            pi = null; piTr = null;
            foreach (long c in children)
            {
                Doc t = scene.Require(c, "a child transform");
                if (!t.Stripped || ObjRef.Parse(t.Field("m_CorrespondingSourceObject")).Guid != prefab.Guid) continue;
                Doc instance = scene.Require(t.FieldRef("m_PrefabInstance"), "a building's prefab instance");
                if (!instance.Text.Contains("      propertyPath: m_Name\n      value: " + name + "\n")) continue;
                if (pi != null) throw new Refusal($"two '{name}' instances of {prefab.Path}.");
                pi = instance; piTr = t;
            }
        }

        /// <summary>An instance as the editor writes one dropped under a parent: its name, the root transform's
        /// records and, where the scene has its own review player, the room's occupant (the convention every
        /// serialized <see cref="BuildingInterior"/> in the region follows; see
        /// <see cref="NineMileCreekWharfLandSide.ReviewOccupant"/>).</summary>
        static string Instance(long id, long parent, PrefabFacts prefab, string name, Vector2 at, long occupant)
        {
            var records = new List<(long target, string path, string value, string reference)>
            {
                (prefab.GameObject, "m_Name", Plain(name), "{fileID: 0}"),
                (prefab.Transform, "m_LocalPosition.x", F(at.x), "{fileID: 0}"),
                (prefab.Transform, "m_LocalPosition.y", F(at.y), "{fileID: 0}"),
                (prefab.Transform, "m_LocalPosition.z", "0", "{fileID: 0}"),
                (prefab.Transform, "m_LocalRotation.w", "1", "{fileID: 0}"),
                (prefab.Transform, "m_LocalRotation.x", "0", "{fileID: 0}"),
                (prefab.Transform, "m_LocalRotation.y", "0", "{fileID: 0}"),
                (prefab.Transform, "m_LocalRotation.z", "0", "{fileID: 0}"),
                (prefab.Transform, "m_LocalEulerAnglesHint.x", "0", "{fileID: 0}"),
                (prefab.Transform, "m_LocalEulerAnglesHint.y", "0", "{fileID: 0}"),
                (prefab.Transform, "m_LocalEulerAnglesHint.z", "0", "{fileID: 0}"),
            };
            if (occupant != 0) records.Add((prefab.Interior, "_occupant", "", Ref(occupant)));
            var lines = new List<string>
            {
                $"--- !u!1001 &{Id(id)}", "PrefabInstance:", "  m_ObjectHideFlags: 0", "  serializedVersion: 2",
                "  m_Modification:", "    serializedVersion: 3", $"    m_TransformParent: {Ref(parent)}", "    m_Modifications:",
            };
            foreach (var r in records.OrderBy(r => r.target))
            {
                lines.Add($"    - target: {{fileID: {Id(r.target)}, guid: {prefab.Guid}, type: 3}}");
                lines.Add("      propertyPath: " + r.path);
                lines.Add(r.value.Length == 0 ? "      value: " : "      value: " + r.value);
                lines.Add("      objectReference: " + r.reference);
            }
            lines.Add("    m_RemovedComponents: []");
            lines.Add("    m_RemovedGameObjects: []");
            lines.Add("    m_AddedGameObjects: []");
            lines.Add("    m_AddedComponents: []");
            lines.Add($"  m_SourcePrefab: {{fileID: 100100000, guid: {prefab.Guid}, type: 3}}");
            return string.Join("\n", lines);
        }

        static string Stripped(long id, long instance, PrefabFacts prefab) => string.Join("\n",
            $"--- !u!4 &{Id(id)} stripped",
            "Transform:",
            $"  m_CorrespondingSourceObject: {{fileID: {Id(prefab.Transform)}, guid: {prefab.Guid}, type: 3}}",
            $"  m_PrefabInstance: {Ref(instance)}",
            "  m_PrefabAsset: {fileID: 0}");

        const string Unlinked = "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                                "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}";

        static string RootGameObject(long id, long tr) => string.Join("\n",
            $"--- !u!1 &{Id(id)}", "GameObject:", Unlinked, "  serializedVersion: 6", "  m_Component:",
            $"  - component: {Ref(tr)}", "  m_Layer: 0", "  m_Name: " + RootName, "  m_TagString: Untagged",
            "  m_Icon: {fileID: 0}", "  m_NavMeshLayer: 0", "  m_StaticEditorFlags: 0", "  m_IsActive: 1");

        static string RootTransform(long id, long go, IList<long> children)
        {
            var lines = new List<string>
            {
                $"--- !u!4 &{Id(id)}", "Transform:", Unlinked, $"  m_GameObject: {Ref(go)}", "  serializedVersion: 2",
                "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}", "  m_LocalPosition: {x: 0, y: 0, z: 0}",
                "  m_LocalScale: {x: 1, y: 1, z: 1}", "  m_ConstrainProportionsScale: 0",
                children.Count == 0 ? "  m_Children: []" : "  m_Children:",
            };
            foreach (long c in children) lines.Add($"  - {Ref(c)}");
            lines.Add("  m_Father: {fileID: 0}");
            lines.Add("  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}");
            return string.Join("\n", lines);
        }

        static void Put(LayerPatch patch, Doc before, string after, string name)
        {
            if (before == null) patch.Add(after, name); else patch.Edit(before, after, name);
        }

        static string Id(long v) => v.ToString(CultureInfo.InvariantCulture);
        static string Ref(long v) => "{fileID: " + Id(v) + "}";
        static string At(Vector2 p) => $"({F(p.x)}, {F(p.y)})";

        static string Plain(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Trim() != s || s.Contains(": ") || s.Contains(" #") || "!&*|>'\"%@`#,[]{}".IndexOf(s[0]) >= 0)
                throw new Refusal($"'{s}' would need quoting in the scene.");
            return s;
        }

        // ---- running it ------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/World/Nine Mile Creek Buildings/Write the Patches (dry run)")]
        public static void DryRun() => Run(false);

        [MenuItem("Hidden Harbours/World/Nine Mile Creek Buildings/Apply the Patches")]
        public static void Apply() => Run(true);

        /// <summary>Plan (and with <paramref name="apply"/> write) every shell's patches on the committed scene.
        /// Refuses an open scene: it could later overwrite the file.</summary>
        public static List<LayerPatch> Run(bool apply)
        {
            if (apply)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).path == ScenePath)
                        throw new Refusal($"{ScenePath} is open. Close it before applying text patches.");
            string project = Directory.GetCurrentDirectory();
            var cache = new Dictionary<string, PrefabFacts>();
            PrefabFacts Facts(string p) => cache.TryGetValue(p, out PrefabFacts f) ? f : cache[p] = PrefabFacts.Read(project, p);
            string before = File.ReadAllText(ScenePath);
            List<LayerPatch> patches = PlanAll(SceneYaml.Parse(before), Facts, out SceneYaml planned);
            string after = planned.Render();
            Directory.CreateDirectory(PatchFolder);
            var utf8 = new UTF8Encoding(false);
            for (int i = 0; i < patches.Count; i++)
                File.WriteAllText(Path.Combine(PatchFolder, $"{i:00}.{patches[i].Step}.{patches[i].Root}.yaml"), patches[i].ToYaml(), utf8);
            if (apply && after != before) File.WriteAllText(ScenePath, after, utf8);
            if (apply) AssetDatabase.Refresh();
            Debug.Log($"[NmcBuildingShells] {(apply ? "Applied" : "Dry run")}:\n" + string.Join("\n", patches.Select(p => p.Summary())));
            return patches;
        }
    }
}
#endif
