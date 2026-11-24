#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>WHERE THE KITCHENS AND BATHROOMS GO</b> in the St Peters house-kit interiors, as data the
    /// placement slices will consume (docs/world/kitchens-bathrooms-placement-plan.md).
    ///
    /// <para>This file places nothing. It is the table a later named patch step reads (one row = one
    /// station instance), and it is held to the room by <c>RoomFixturePlanTests</c>: every station inside
    /// its room's floor, out of the doorway lane, clear of the shipped furniture and of every other
    /// station, and every bathroom station inside its own closet.</para>
    ///
    /// <para><b>One file per building.</b> This file holds the shared vocabulary (kinds, the row and
    /// closet shapes, the kitchen and closet templates, the room sizes) and the fixed building order.
    /// Each building's rows, closet and door compass live in its own <c>RoomFixturePlan.&lt;Building&gt;.cs</c>
    /// partial, so a change to one building's rows touches only that building's file.</para>
    ///
    /// <para><b>Frame.</b> The ROOM's model metres, the frame every row of
    /// <see cref="StPetersInteriors.FurnishingsFor"/> uses: origin the floor centre, <c>+y</c> toward the
    /// front door, <c>+x</c> to the LEFT as you come in. Level 0 is the ground floor, 1 the storey above.</para>
    ///
    /// <para><b>Compass.</b> Each building states which compass way its front door wall faces in the
    /// scene (<see cref="Compass"/>), and each row states which wall of the plan frame it backs onto
    /// (<see cref="Wall"/>); the row's <see cref="Row.Backing"/> is the compass wall it stands against.
    /// The tests hold the door compass to the scene's serialized BuildingInterior and the wall to the
    /// row's geometry, and fail any south-backed row not on <see cref="SouthWallExceptions"/>.</para>
    ///
    /// <para><b>Footprints here are planning sizes</b>, not art: the kitchen prefabs have no sprite yet
    /// and no bathroom prefab exists. When the prefabs carry real colliders, the placement step's test
    /// should read those instead and these sizes retire.</para>
    /// </summary>
    public static partial class RoomFixturePlan
    {
        /// <summary>What a station is. Append-only.</summary>
        public enum Kind
        {
            PrepCounter = 0,
            Stove = 1,
            Microwave = 2,
            BathroomSink = 3,
            Shower = 4,
            Bath = 5,
            Toilet = 6,
            Fridge = 7,
            KitchenSink = 8,
        }

        /// <summary>A ground compass way, in 45 degree steps clockwise from north (world <c>+y</c>).</summary>
        public enum Compass
        {
            North = 0,
            NorthEast = 1,
            East = 2,
            SouthEast = 3,
            South = 4,
            SouthWest = 5,
            West = 6,
            NorthWest = 7,
        }

        /// <summary>The number of <see cref="Compass"/> ways.</summary>
        public const int CompassPoints = 8;

        /// <summary>A wall of the plan frame, as compass steps clockwise from the front (door) wall:
        /// the left wall (<c>+x</c>) is a quarter turn clockwise of the door's way, the back wall half a
        /// turn, the right wall (<c>-x</c>) three quarters.</summary>
        public enum Wall
        {
            Front = 0,
            Left = 2,
            Back = 4,
            Right = 6,
        }

        /// <summary>The compass way of a plan wall in a building whose front door wall faces <paramref name="door"/>.</summary>
        public static Compass CompassOf(Compass door, Wall wall) =>
            (Compass)(((int)door + (int)wall) % CompassPoints);

        /// <summary>Whether a wall faces into the southern half (S, SE or SW): the near side of the
        /// cutaway, where a fixture stands with its back to the camera.</summary>
        public static bool IsSouthWall(Compass c) =>
            c == Compass.SouthEast || c == Compass.South || c == Compass.SouthWest;

        /// <summary>The id slot (the last part of the fixture id) for a kind.</summary>
        public static string Slot(Kind kind) => kind switch
        {
            Kind.PrepCounter => "prep_counter",
            Kind.Stove => "stove",
            Kind.Microwave => "microwave",
            Kind.BathroomSink => "bathroom_sink",
            Kind.Shower => "shower",
            Kind.Bath => "bath",
            Kind.Toilet => "toilet",
            Kind.Fridge => "fridge",
            Kind.KitchenSink => "kitchen_sink",
            _ => "unknown",
        };

        /// <summary>The station Def a kind is wired to. The three cooking ids arrive with the kitchen
        /// stations slice; the four wash ids are on main (<c>Data/Resources/WashStations</c>).</summary>
        public static string DefId(Kind kind) => kind switch
        {
            Kind.PrepCounter => "cooking_station.prep_counter",
            Kind.Stove => "cooking_station.stove",
            Kind.Microwave => "cooking_station.microwave",
            Kind.BathroomSink => "washstation.sink",
            Kind.Shower => "washstation.shower",
            Kind.Bath => "washstation.bath",
            Kind.Toilet => "washstation.toilet",
            Kind.Fridge => "kitchen_storage.fridge",
            Kind.KitchenSink => "washstation.sink",
            _ => "",
        };

        /// <summary>Planning footprint in metres, as placed: <c>x</c> across the room, <c>y</c> along it.
        /// A counter run hugs the left side wall, so its depth runs across; the closet pieces are given
        /// as they stand in the template below. The fridge and the kitchen sink are the appliance rig's own feet,
        /// given as they stand against a FRONT (door) wall: x across their face, y their depth out from it.</summary>
        public static Vector2 Footprint(Kind kind) => kind switch
        {
            Kind.PrepCounter => new Vector2(0.60f, 0.90f),
            Kind.Stove => new Vector2(0.60f, 0.75f),
            Kind.Microwave => new Vector2(0.45f, 0.50f),
            Kind.BathroomSink => new Vector2(0.55f, 0.45f),
            Kind.Shower => new Vector2(0.85f, 0.85f),
            Kind.Bath => new Vector2(1.60f, 0.75f),
            Kind.Toilet => new Vector2(0.45f, 0.70f),
            Kind.Fridge => new Vector2(0.70f, 0.68f),
            Kind.KitchenSink => new Vector2(1.00f, 0.60f),
            _ => Vector2.zero,
        };

        /// <summary>A row's planning footprint turned to the wall it backs: <see cref="Footprint"/> gives the
        /// counter pieces as they stand on a side (left or right) wall and the fridge and kitchen sink as they
        /// stand on an end (front or back) wall, so a piece on the other kind of wall swaps its two sides. The
        /// closet pieces are given as they stand on an end wall too (across the wall, then out from it), so a
        /// toilet or basin on a side wall is turned as well: it reaches out from its wall by its depth, as its
        /// picture does, not by its width.</summary>
        public static Vector2 FootprintOf(Row row)
        {
            Vector2 f = Footprint(row.Kind);
            bool side = row.Wall == Wall.Left || row.Wall == Wall.Right;
            bool counter = row.Kind == Kind.PrepCounter || row.Kind == Kind.Stove || row.Kind == Kind.Microwave;
            bool endPiece = row.Kind == Kind.Fridge || row.Kind == Kind.KitchenSink || IsBathroom(row.Kind);
            return (counter && !side) || (endPiece && side) ? new Vector2(f.y, f.x) : f;
        }

        /// <summary>One planned station.</summary>
        public readonly struct Row
        {
            public readonly string BuildingKey;   // snake_case, the id's building part
            public readonly string BuildingRoot;  // the scene root the building hangs under
            public readonly string Building;      // the building's GameObject name under that root
            public readonly string RoomKey;       // the FurnishingsFor / InteriorKit room key on this level
            public readonly string PlanKey;       // the UpperLevelFor plan key, or null
            public readonly int Level;
            public readonly Kind Kind;
            public readonly Vector2 At;

            /// <summary>The compass way the building's front door wall faces.</summary>
            public readonly Compass Door;

            /// <summary>The plan-frame wall this station backs onto.</summary>
            public readonly Wall Wall;

            /// <summary>True once the placement step stands this row in the scene
            /// (<see cref="InteriorFixturePlacement.PlacedRows"/>).</summary>
            public readonly bool Placed;

            public Row(string buildingKey, string buildingRoot, string building, string roomKey, string planKey,
                       int level, Kind kind, Vector2 at, Compass door, Wall wall, bool placed = false)
            {
                BuildingKey = buildingKey; BuildingRoot = buildingRoot; Building = building;
                RoomKey = roomKey; PlanKey = planKey; Level = level; Kind = kind; At = at;
                Door = door; Wall = wall; Placed = placed;
            }

            /// <summary><c>fixture.st_peters.&lt;building&gt;.&lt;slot&gt;</c>, the washstand's pattern.</summary>
            public string Id => $"fixture.{Region}.{BuildingKey}.{Slot(Kind)}";

            /// <summary>The compass wall this station stands against.</summary>
            public Compass Backing => CompassOf(Door, Wall);
        }

        /// <summary>A bathroom closet: the rectangle its two new partitions enclose (model metres), and
        /// the 0.8 m door in the partition that faces the room.</summary>
        public readonly struct Closet
        {
            public readonly string BuildingKey;
            public readonly int Level;
            public readonly Rect Area;
            public readonly float DoorFrom, DoorTo;   // along y, in the x = inner-edge partition (or along x, below)

            /// <summary>The door is in the closet's FRONT partition (y = <see cref="Area"/>.yMax), and
            /// <see cref="DoorFrom"/>/<see cref="DoorTo"/> run along x: a washroom entered from the room in
            /// front of it rather than from beside it (Ginny's).</summary>
            public readonly bool DoorInFront;

            public Closet(string buildingKey, int level, Rect area, float doorFrom, float doorTo,
                          bool doorInFront = false)
            {
                BuildingKey = buildingKey; Level = level; Area = area; DoorFrom = doorFrom; DoorTo = doorTo;
                DoorInFront = doorInFront;
            }
        }

        public const string Region = "st_peters";
        public const string Scene = "Assets/_Project/Scenes/StPeters.unity";

        // ---- the templates ------------------------------------------------------------------------
        // Kitchen: a run along the LEFT side wall (+x), in the front half, ending 0.5 m short of the front
        // wall. Prep, then stove, then the microwave on its own short cabinet nearest the door.
        // Closet: the BACK-right corner (-x, -y), clear of the hearth that sits mid back wall, unless the
        // building's furniture is already there (the red saltbox), in which case the FRONT-right corner.

        const float CounterInset = 0.60f;   // counter centre from the side wall's outer face

        static IEnumerable<Row> Kitchen(string key, string root, string building, string room, string plan,
                                        Compass door, float w, float l, bool stove = true, bool prep = true)
        {
            float hw = w * 0.5f, hl = l * 0.5f, x = hw - CounterInset;
            // The microwave's cabinet is shallower than a counter: it stands with its back to the same wall,
            // so its centre sits nearer that wall by half the difference.
            float xm = x + (Footprint(Kind.PrepCounter).x - Footprint(Kind.Microwave).x) * 0.5f;
            if (prep) yield return new Row(key, root, building, room, plan, 0, Kind.PrepCounter, new Vector2(x, hl - 2.60f), door, Wall.Left);
            if (stove)
            {
                yield return new Row(key, root, building, room, plan, 0, Kind.Stove, new Vector2(x, hl - 1.60f), door, Wall.Left);
                yield return new Row(key, root, building, room, plan, 0, Kind.Microwave, new Vector2(xm, hl - 0.75f), door, Wall.Left);
            }
            else
                yield return new Row(key, root, building, room, plan, 0, Kind.Microwave, new Vector2(xm, hl - 1.60f), door, Wall.Left);
        }

        const float ClosetWidth = 1.90f, ClosetDepth = 2.70f;

        /// <summary>The standard closet: toilet against the back wall in the corner, basin beside it, and
        /// the shower against the right wall beyond the floor he stands on to use either of them (it used to
        /// stand on that floor, 1.6 m out, in front of the toilet). <paramref name="front"/> mirrors it to the
        /// front-right corner, where the toilet and basin back onto the front wall.</summary>
        static IEnumerable<Row> Bathroom(string key, string root, string building, string room, string plan,
                                         int level, Compass door, float w, float l, bool front = false)
        {
            float hw = w * 0.5f, hl = l * 0.5f, s = front ? -1f : 1f;
            Wall end = front ? Wall.Front : Wall.Back;
            yield return new Row(key, root, building, room, plan, level, Kind.Toilet, new Vector2(-hw + 0.65f, -s * (hl - 0.65f)), door, end);
            yield return new Row(key, root, building, room, plan, level, Kind.BathroomSink, new Vector2(-hw + 1.45f, -s * (hl - 0.55f)), door, end);
            yield return new Row(key, root, building, room, plan, level, Kind.Shower, new Vector2(-hw + 0.75f, -s * (hl - 2.225f)), door, Wall.Right);
        }

        static Closet StandardCloset(string key, int level, float w, float l, bool front = false)
        {
            float hw = w * 0.5f, hl = l * 0.5f;
            Rect area = front
                ? new Rect(-hw, hl - ClosetDepth, ClosetWidth, ClosetDepth)
                : new Rect(-hw, -hl, ClosetWidth, ClosetDepth);
            float d0 = front ? hl - 2.00f : -hl + 1.20f;
            return new Closet(key, level, area, d0, d0 + 0.80f);
        }

        // ---- the room sizes -----------------------------------------------------------------------
        // Room sizes are the baked rooms' own (Interiors.json footprint metres, = the BuildingInterior's
        // serialized _widthMetres/_lengthMetres). The test re-derives them from InteriorKit and fails if
        // they drift. The sage room is shared by Ginny's cottage and the village sage cottage.

        public const float SageW = 6.60f, SageL = 8.05f;
        public const float GinnyW = 7.20f, GinnyL = 9.10f;
        public const float RedW = 6.96f, RedL = 8.68f;
        public const float FarmW = 7.68f, FarmL = 9.94f;
        public const float HarbourW = 6.72f, HarbourL = 8.26f;
        public const float CreamW = 6.84f, CreamL = 8.47f;
        public const float WhiteGableW = 6.84f, WhiteGableL = 8.47f;
        public const float SchoolW = 6.36f, SchoolL = 7.63f;

        // ---- the rows -----------------------------------------------------------------------------
        // The building order is fixed: it is the row order, and so the order the placement step writes.
        // A building's rows themselves are in its own file.

        static readonly Row[] _rows = Build();

        /// <summary>Every planned station in the St Peters house-kit interiors.</summary>
        public static IReadOnlyList<Row> Rows => _rows;

        static Row[] Build()
        {
            var r = new List<Row>();
            r.AddRange(GinnyCottageRows());
            r.AddRange(SageCottageRows());
            r.AddRange(RedSaltboxRows());
            r.AddRange(WhiteFarmhouseRows());
            r.AddRange(HarbourGableRows());
            r.AddRange(CreamSaltboxRows());
            r.AddRange(WhiteGableRows());
            r.AddRange(SchoolRows());
            return r.ToArray();
        }

        /// <summary>Every planned bathroom closet (the partitions a layout change must stand).</summary>
        public static readonly Closet[] Closets =
        {
            GinnyCottageCloset(),
            SageCottageCloset(),
            RedSaltboxCloset(),
            WhiteFarmhouseCloset(),
            HarbourGableCloset(),
            CreamSaltboxCloset(),
            WhiteGableCloset(),
        };

        /// <summary>
        /// Rows allowed to back onto a south wall (<see cref="IsSouthWall"/>) for now, by id. The rule is
        /// north first (owner 2026-11-17): kitchens and bathrooms go on north walls. Every planned-only id
        /// here is a row that still has to be re-planned; remove it when it is. Ginny's placed pieces stay
        /// (owner 2026-11-22: the north wall is no longer strict there; a piece goes where it reads best).
        /// </summary>
        public static readonly IReadOnlyCollection<string> SouthWallExceptions = new HashSet<string>
        {
            "fixture.st_peters.red_saltbox.shower",            // planned, not placed; re-plan north before placing
            "fixture.st_peters.white_farmhouse.toilet",        // planned, not placed (SE wall); re-plan before placing
            "fixture.st_peters.white_farmhouse.bathroom_sink", // planned, not placed (SE wall); re-plan before placing
        };

        /// <summary>Whether a kind belongs in a bathroom closet.</summary>
        public static bool IsBathroom(Kind kind) =>
            kind == Kind.BathroomSink || kind == Kind.Shower || kind == Kind.Bath || kind == Kind.Toilet;

        /// <summary>Whether a kind's prefab carries an interact id. The fridge does not yet: its
        /// <c>KitchenStoreFixture</c> names its Def and has no verb, so it stands as a picture with a footprint.</summary>
        public static bool HasInteractId(Kind kind) => kind != Kind.Fridge;
    }
}
#endif
