#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>WHERE THE KITCHENS AND BATHROOMS GO</b> in the St Peters house-kit interiors, as data the
    /// placement lanes will consume (docs/world/kitchens-bathrooms-placement-plan.md).
    ///
    /// <para>This file places nothing. It is the table a later named patch step reads (one row = one
    /// station instance), and it is held to the room by <c>RoomFixturePlanTests</c>: every station inside
    /// its room's floor, out of the doorway lane, clear of the shipped furniture and of every other
    /// station, and every bathroom station inside its own closet.</para>
    ///
    /// <para><b>Frame.</b> The ROOM's model metres, the frame every row of
    /// <see cref="StPetersInteriors.FurnishingsFor"/> uses: origin the floor centre, <c>+y</c> toward the
    /// front door, <c>+x</c> to the LEFT as you come in. Level 0 is the ground floor, 1 the storey above.</para>
    ///
    /// <para><b>Footprints here are planning sizes</b>, not art: the kitchen prefabs have no sprite yet
    /// and no bathroom prefab exists. When the prefabs carry real colliders, the placement step's test
    /// should read those instead and these sizes retire.</para>
    /// </summary>
    public static class RoomFixturePlan
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
        }

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
            _ => "",
        };

        /// <summary>Planning footprint in metres, as placed: <c>x</c> across the room, <c>y</c> along it.
        /// A counter run hugs the left side wall, so its depth runs across; the closet pieces are given
        /// as they stand in the template below.</summary>
        public static Vector2 Footprint(Kind kind) => kind switch
        {
            Kind.PrepCounter => new Vector2(0.60f, 0.90f),
            Kind.Stove => new Vector2(0.60f, 0.75f),
            Kind.Microwave => new Vector2(0.45f, 0.50f),
            Kind.BathroomSink => new Vector2(0.55f, 0.45f),
            Kind.Shower => new Vector2(0.85f, 0.85f),
            Kind.Bath => new Vector2(1.60f, 0.75f),
            Kind.Toilet => new Vector2(0.45f, 0.70f),
            _ => Vector2.zero,
        };

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

            public Row(string buildingKey, string buildingRoot, string building, string roomKey, string planKey,
                       int level, Kind kind, Vector2 at)
            {
                BuildingKey = buildingKey; BuildingRoot = buildingRoot; Building = building;
                RoomKey = roomKey; PlanKey = planKey; Level = level; Kind = kind; At = at;
            }

            /// <summary><c>fixture.st_peters.&lt;building&gt;.&lt;slot&gt;</c>, the washstand's pattern.</summary>
            public string Id => $"fixture.{Region}.{BuildingKey}.{Slot(Kind)}";
        }

        /// <summary>A bathroom closet: the rectangle its two new partitions enclose (model metres), and
        /// the 0.8 m door in the partition that faces the room.</summary>
        public readonly struct Closet
        {
            public readonly string BuildingKey;
            public readonly int Level;
            public readonly Rect Area;
            public readonly float DoorFrom, DoorTo;   // along y, in the x = inner-edge partition

            public Closet(string buildingKey, int level, Rect area, float doorFrom, float doorTo)
            {
                BuildingKey = buildingKey; Level = level; Area = area; DoorFrom = doorFrom; DoorTo = doorTo;
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
                                        float w, float l, bool stove = true, bool prep = true)
        {
            float hw = w * 0.5f, hl = l * 0.5f, x = hw - CounterInset;
            if (prep) yield return new Row(key, root, building, room, plan, 0, Kind.PrepCounter, new Vector2(x, hl - 2.60f));
            if (stove)
            {
                yield return new Row(key, root, building, room, plan, 0, Kind.Stove, new Vector2(x, hl - 1.60f));
                yield return new Row(key, root, building, room, plan, 0, Kind.Microwave, new Vector2(x, hl - 0.75f));
            }
            else
                yield return new Row(key, root, building, room, plan, 0, Kind.Microwave, new Vector2(x, hl - 1.60f));
        }

        const float ClosetWidth = 1.90f, ClosetDepth = 2.20f;

        /// <summary>The standard closet: toilet against the back wall in the corner, basin beside it,
        /// shower in front of the toilet. <paramref name="front"/> mirrors it to the front-right corner.</summary>
        static IEnumerable<Row> Bathroom(string key, string root, string building, string room, string plan,
                                         int level, float w, float l, bool front = false)
        {
            float hw = w * 0.5f, hl = l * 0.5f, s = front ? -1f : 1f;
            yield return new Row(key, root, building, room, plan, level, Kind.Toilet, new Vector2(-hw + 0.65f, -s * (hl - 0.65f)));
            yield return new Row(key, root, building, room, plan, level, Kind.BathroomSink, new Vector2(-hw + 1.45f, -s * (hl - 0.55f)));
            yield return new Row(key, root, building, room, plan, level, Kind.Shower, new Vector2(-hw + 0.75f, -s * (hl - 1.60f)));
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

        // The farmhouse is a family house: a bath along the back wall instead of a shower, so its closet is
        // wider and deeper (2.3 x 2.6 m) and the toilet and basin stand along the right wall.
        const float FarmClosetWidth = 2.30f, FarmClosetDepth = 2.60f;

        static IEnumerable<Row> FarmBathroom(float w, float l)
        {
            float hw = w * 0.5f, hl = l * 0.5f;
            yield return Farm(Kind.Bath, new Vector2(-hw + 1.20f, -hl + 0.70f));
            yield return Farm(Kind.Toilet, new Vector2(-hw + 0.65f, -hl + 1.55f));
            yield return Farm(Kind.BathroomSink, new Vector2(-hw + 0.60f, -hl + 2.15f));
        }

        // Ginny's cottage, the R1 pilot (re-planned after its first editor look): a longer, shallower
        // closet in the back-right corner, 2.05 m across by 2.9 m along, its front end stopping 0.1 m short
        // of the dresser. Re-planned a second time from the scene's colliders (no editor): the toilet at the
        // front end of the right wall, the shower behind it, the basin on the back wall. Standing just inside
        // the closet door the player reaches each piece with that piece the nearest (the toilet's seat reach
        // is 1.4 m on its prefab for this). Each piece is 0.9 m or more from the next.
        const float GinnyClosetFront = -1.10f, GinnyClosetInner = -1.25f;

        static IEnumerable<Row> GinnyBathroom(string root)
        {
            yield return Ginny(root, Kind.Shower, new Vector2(-2.575f, -2.85f));
            yield return Ginny(root, Kind.Toilet, new Vector2(-2.775f, -1.475f));
            yield return Ginny(root, Kind.BathroomSink, new Vector2(-1.925f, -3.50f));
        }

        static Row Ginny(string root, Kind k, Vector2 at) =>
            new Row("ginny_cottage", root, StPetersGinnyPlot.CottageKey, "sageCottage",
                    StPetersInteriors.GinnyCottagePlanKey, 0, k, at);

        static Row Farm(Kind k, Vector2 at) =>
            new Row("white_farmhouse", StPetersVillage.RootName, "whiteFarmhouse", "whiteFarmhouse", null, 0, k, at);

        // ---- the rows -----------------------------------------------------------------------------
        // Room sizes are the baked rooms' own (Interiors.json footprint metres, = the BuildingInterior's
        // serialized _widthMetres/_lengthMetres). The test re-derives them from InteriorKit and fails if
        // they drift.

        public const float SageW = 6.60f, SageL = 8.05f;
        public const float RedW = 6.96f, RedL = 8.68f;
        public const float FarmW = 7.68f, FarmL = 9.94f;
        public const float HarbourW = 6.72f, HarbourL = 8.26f;
        public const float CreamW = 6.84f, CreamL = 8.47f;
        public const float WhiteGableW = 6.84f, WhiteGableL = 8.47f;
        public const float SchoolW = 6.36f, SchoolL = 7.63f;

        static readonly Row[] _rows = Build();

        /// <summary>Every planned station in the St Peters house-kit interiors.</summary>
        public static IReadOnlyList<Row> Rows => _rows;

        static Row[] Build()
        {
            var r = new List<Row>();
            string ginny = StPetersGinnyPlot.RootName, village = StPetersVillage.RootName;

            r.AddRange(Kitchen("ginny_cottage", ginny, StPetersGinnyPlot.CottageKey, "sageCottage",
                               StPetersInteriors.GinnyCottagePlanKey, SageW, SageL));
            r.AddRange(GinnyBathroom(ginny));

            r.AddRange(Kitchen("sage_cottage", village, "sageCottage", "sageCottage", null, SageW, SageL));
            r.AddRange(Bathroom("sage_cottage", village, "sageCottage", "sageCottage", null, 0, SageW, SageL));

            r.AddRange(Kitchen("red_saltbox", village, "redSaltbox", "redSaltbox", null, RedW, RedL));
            r.AddRange(Bathroom("red_saltbox", village, "redSaltbox", "redSaltbox", null, 0, RedW, RedL, front: true));

            r.AddRange(Kitchen("white_farmhouse", village, "whiteFarmhouse", "whiteFarmhouse", null, FarmW, FarmL));
            r.AddRange(FarmBathroom(FarmW, FarmL));

            r.AddRange(Kitchen("harbour_gable", village, "harbourGable", "harbourGable", "harbourGable", HarbourW, HarbourL));
            r.AddRange(Bathroom("harbour_gable", village, "harbourGable", "harbourGableUpper", "harbourGable", 1, HarbourW, HarbourL));

            r.AddRange(Kitchen("cream_saltbox", village, "creamSaltbox", "creamSaltbox", "creamSaltbox", CreamW, CreamL));
            r.AddRange(Bathroom("cream_saltbox", village, "creamSaltbox", "creamSaltboxUpper", "creamSaltbox", 1, CreamW, CreamL));

            r.AddRange(Kitchen("white_gable", village, "whiteGable", "whiteGable", "whiteGable", WhiteGableW, WhiteGableL));
            r.AddRange(Bathroom("white_gable", village, "whiteGable", "whiteGableUpper", "whiteGable", 1, WhiteGableW, WhiteGableL));

            // The school: a staff kitchenette (prep and a microwave, no stove: the hearth heats the room,
            // it is not a cooker) and no indoor bathroom. The one-room school gets an outhouse outside,
            // placed by the outhouse lane, not by this table.
            r.AddRange(Kitchen("school", village, "school", "school", null, SchoolW, SchoolL, stove: false));
            return r.ToArray();
        }

        /// <summary>Every planned bathroom closet (the partitions a layout change must stand).</summary>
        public static readonly Closet[] Closets =
        {
            new Closet("ginny_cottage", 0,
                       Rect.MinMaxRect(-SageW * 0.5f, -SageL * 0.5f, GinnyClosetInner, GinnyClosetFront),
                       -2.40f, -1.60f),
            StandardCloset("sage_cottage", 0, SageW, SageL),
            StandardCloset("red_saltbox", 0, RedW, RedL, front: true),
            new Closet("white_farmhouse", 0,
                       new Rect(-FarmW * 0.5f, -FarmL * 0.5f, FarmClosetWidth, FarmClosetDepth),
                       -FarmL * 0.5f + 1.40f, -FarmL * 0.5f + 2.20f),
            StandardCloset("harbour_gable", 1, HarbourW, HarbourL),
            StandardCloset("cream_saltbox", 1, CreamW, CreamL),
            StandardCloset("white_gable", 1, WhiteGableW, WhiteGableL),
        };

        /// <summary>Whether a kind belongs in a bathroom closet.</summary>
        public static bool IsBathroom(Kind kind) =>
            kind == Kind.BathroomSink || kind == Kind.Shower || kind == Kind.Bath || kind == Kind.Toilet;
    }
}
#endif
