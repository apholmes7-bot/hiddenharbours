using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using Kind = HiddenHarbours.App.Editor.RoomFixturePlan.Kind;
using Row = HiddenHarbours.App.Editor.RoomFixturePlan.Row;
using Wall = HiddenHarbours.App.Editor.RoomFixturePlan.Wall;
using Compass = HiddenHarbours.App.Editor.RoomFixturePlan.Compass;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The kitchens-and-bathrooms plan's St Peters table (<see cref="RoomFixturePlan"/>) held to the
    /// rooms it names: the same bounds <c>StPetersInteriorsTests</c> holds the furniture to, plus clearance
    /// from that furniture, from each other and (for bathroom pieces) containment in their closet.
    /// </summary>
    public class RoomFixturePlanTests
    {
        const float Eps = 1e-3f;

        static readonly Dictionary<string, (float w, float l)> Declared = new Dictionary<string, (float, float)>
        {
            { "sageCottage", (RoomFixturePlan.SageW, RoomFixturePlan.SageL) },
            { "redSaltbox", (RoomFixturePlan.RedW, RoomFixturePlan.RedL) },
            { "whiteFarmhouse", (RoomFixturePlan.FarmW, RoomFixturePlan.FarmL) },
            { "harbourGable", (RoomFixturePlan.HarbourW, RoomFixturePlan.HarbourL) },
            { "harbourGableUpper", (RoomFixturePlan.HarbourW, RoomFixturePlan.HarbourL) },
            { "creamSaltbox", (RoomFixturePlan.CreamW, RoomFixturePlan.CreamL) },
            { "creamSaltboxUpper", (RoomFixturePlan.CreamW, RoomFixturePlan.CreamL) },
            { "whiteGable", (RoomFixturePlan.WhiteGableW, RoomFixturePlan.WhiteGableL) },
            { "whiteGableUpper", (RoomFixturePlan.WhiteGableW, RoomFixturePlan.WhiteGableL) },
            { "school", (RoomFixturePlan.SchoolW, RoomFixturePlan.SchoolL) },
        };

        /// <summary>The room's size from its own dialled <c>size</c>, as StPetersInteriorsTests derives it.</summary>
        static (float w, float l) Baked(string roomKey)
        {
            var room = InteriorKit.RoomSet.Single(b => b.Key == roomKey);
            Assert.IsNotNull(room.Dialled, $"room '{roomKey}' must be dialled to have a size");
            Assert.IsTrue(room.Dialled.TryGetValue("size", out object v), $"room '{roomKey}' must dial a size");
            float size = System.Convert.ToSingle(v);
            return (6f + size * 2.4f, 7f + size * 4.2f);
        }

        static Rect RectOf(Vector2 centre, Vector2 size) =>
            new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, size.x, size.y);

        static bool Overlap(Rect a, Rect b) =>
            a.xMin < b.xMax - Eps && b.xMin < a.xMax - Eps && a.yMin < b.yMax - Eps && b.yMin < a.yMax - Eps;

        /// <summary>The shipped furniture a row's level already holds.</summary>
        static IEnumerable<StPetersInteriors.Furnishing> FurnitureFor(Row row)
        {
            StPetersInteriors.UpperLevelPlan plan = string.IsNullOrEmpty(row.PlanKey)
                ? default
                : StPetersInteriors.UpperLevelFor(row.PlanKey);
            if (row.Level == 0)
            {
                foreach (var f in StPetersInteriors.FurnishingsFor(row.RoomKey)) yield return f;
                if (plan.GroundAdditions != null) foreach (var f in plan.GroundAdditions) yield return f;
            }
            else if (plan.Furnishings != null)
                foreach (var f in plan.Furnishings) yield return f;
        }

        static Vector2 FurnitureSize(InteriorKit.Contract contract, StPetersInteriors.Furnishing f)
        {
            InteriorKit.Entry prop = InteriorKit.FindProp(contract, f.PropKey);
            if (prop == null) return Vector2.zero;
            float w = prop.propFootprintWidth, d = prop.propFootprintDepth;
            int q = ((f.FacingOffset % 8) + 8) % 8;
            if (q % 2 != 0) { float diag = Mathf.Sqrt(w * w + d * d); return new Vector2(diag, diag); }
            return q == 2 || q == 6 ? new Vector2(d, w) : new Vector2(w, d);
        }

        [Test]
        public void TheDeclaredRoomSizesAreTheBakedRooms()
        {
            foreach (var kv in Declared)
            {
                (float w, float l) = Baked(kv.Key);
                Assert.AreEqual(w, kv.Value.w, 0.011f, $"'{kv.Key}' width");
                Assert.AreEqual(l, kv.Value.l, 0.011f, $"'{kv.Key}' length");
            }
        }

        [Test]
        public void EveryRowNamesADeclaredRoomAndAKnownPlan()
        {
            foreach (Row row in RoomFixturePlan.Rows)
            {
                Assert.IsTrue(Declared.ContainsKey(row.RoomKey), $"{row.Id}: room '{row.RoomKey}' is not declared");
                if (row.Level > 0)
                    Assert.IsTrue(StPetersInteriors.HasUpperLevel(row.PlanKey),
                                  $"{row.Id}: level {row.Level} needs a declared upper-level plan, not '{row.PlanKey}'");
            }
        }

        [Test]
        public void IdsAreUniqueAndFollowTheFixturePattern()
        {
            var seen = new HashSet<string>();
            var pattern = new Regex(@"^fixture\.st_peters\.[a-z0-9_]+\.[a-z0-9_]+$");
            foreach (Row row in RoomFixturePlan.Rows)
            {
                Assert.IsTrue(pattern.IsMatch(row.Id), $"'{row.Id}' is not fixture.st_peters.<building>.<slot>");
                Assert.IsTrue(seen.Add(row.Id), $"'{row.Id}' is planned twice");
                Assert.IsNotEmpty(RoomFixturePlan.DefId(row.Kind), $"{row.Id} has no station Def");
            }
        }

        [Test]
        public void EveryStationStandsInsideItsRoom_AndOutOfTheDoorway()
        {
            float wall = StPetersInteriors.WallThicknessMetres;
            float halfDoor = StPetersInteriors.DoorwayWidthMetres * 0.5f;
            foreach (Row row in RoomFixturePlan.Rows)
            {
                (float w, float l) = Declared[row.RoomKey];
                float hw = w * 0.5f, hl = l * 0.5f;
                Rect r = RectOf(row.At, RoomFixturePlan.Footprint(row.Kind));
                Assert.Less(Mathf.Abs(row.At.x), hw - wall, $"{row.Id} at {row.At}: centre inside a side wall");
                Assert.Less(Mathf.Abs(row.At.y), hl - wall, $"{row.Id} at {row.At}: centre inside an end wall");
                Assert.LessOrEqual(Mathf.Max(-r.xMin, r.xMax), hw + Eps, $"{row.Id}: sticks out through a side wall");
                Assert.LessOrEqual(Mathf.Max(-r.yMin, r.yMax), hl + Eps, $"{row.Id}: sticks out through an end wall");

                // The same doorway lane the furniture test keeps clear (+y is the door end in this frame;
                // on an upper storey the drawn doorway is plugged, but the lane is kept anyway so the
                // landing under a future stair stays open).
                InteriorCatalog.Placement baked = InteriorCatalog.FindRoom(row.RoomKey);
                float doorEnd = baked.IsValid && InteriorCatalog.DoorModelMetres(baked).y < 0f ? -1f : 1f;
                bool inLane = Mathf.Abs(row.At.x) < halfDoor + 0.5f && row.At.y * doorEnd > hl - 2.0f;
                Assert.IsFalse(inLane, $"{row.Id} at {row.At} is parked in the doorway");
            }
        }

        [Test]
        public void NoStationSitsOnTheShippedFurniture()
        {
            InteriorKit.Contract contract = InteriorKit.Load();
            if (contract?.props == null || contract.props.Length == 0)
                Assert.Ignore("no baked interior contract: nothing to read prop footprints from");
            foreach (Row row in RoomFixturePlan.Rows)
            {
                Rect r = RectOf(row.At, RoomFixturePlan.Footprint(row.Kind));
                foreach (var f in FurnitureFor(row))
                {
                    Rect fr = RectOf(f.RoomMetres, FurnitureSize(contract, f));
                    Assert.IsFalse(Overlap(r, fr), $"{row.Id} at {row.At} overlaps the {f.PropKey} at {f.RoomMetres}");
                }
            }
        }

        [Test]
        public void NoTwoStationsOverlap()
        {
            var rows = RoomFixturePlan.Rows;
            for (int i = 0; i < rows.Count; i++)
            for (int j = i + 1; j < rows.Count; j++)
            {
                if (rows[i].BuildingKey != rows[j].BuildingKey || rows[i].Level != rows[j].Level) continue;
                Assert.IsFalse(Overlap(RectOf(rows[i].At, RoomFixturePlan.Footprint(rows[i].Kind)),
                                       RectOf(rows[j].At, RoomFixturePlan.Footprint(rows[j].Kind))),
                               $"{rows[i].Id} and {rows[j].Id} overlap");
            }
        }

        [Test]
        public void EveryHomeHasAKitchenAndABathroom_AndBathroomPiecesAreInTheirCloset()
        {
            var homes = new[] { "ginny_cottage", "sage_cottage", "red_saltbox", "white_farmhouse",
                                "harbour_gable", "cream_saltbox", "white_gable" };
            foreach (string home in homes)
            {
                var mine = RoomFixturePlan.Rows.Where(r => r.BuildingKey == home).ToList();
                foreach (Kind k in new[] { Kind.PrepCounter, Kind.Stove, Kind.Microwave, Kind.Toilet, Kind.BathroomSink })
                    Assert.IsTrue(mine.Any(r => r.Kind == k), $"{home} has no {k}");
                Assert.IsTrue(mine.Any(r => r.Kind == Kind.Shower || r.Kind == Kind.Bath), $"{home} has no shower or bath");
                Assert.AreEqual(1, RoomFixturePlan.Closets.Count(c => c.BuildingKey == home), $"{home} needs one closet");
            }

            foreach (Row row in RoomFixturePlan.Rows.Where(r => RoomFixturePlan.IsBathroom(r.Kind)))
            {
                var closet = RoomFixturePlan.Closets.Single(c => c.BuildingKey == row.BuildingKey);
                Assert.AreEqual(closet.Level, row.Level, $"{row.Id} is on another storey than its closet");
                Rect r = RectOf(row.At, RoomFixturePlan.Footprint(row.Kind));
                Rect a = closet.Area;
                Assert.IsTrue(r.xMin >= a.xMin - Eps && r.xMax <= a.xMax + Eps && r.yMin >= a.yMin - Eps && r.yMax <= a.yMax + Eps,
                              $"{row.Id} at {row.At} is outside its closet {a}");
            }
            foreach (Row row in RoomFixturePlan.Rows.Where(r => !RoomFixturePlan.IsBathroom(r.Kind)))
                foreach (var closet in RoomFixturePlan.Closets.Where(c => c.BuildingKey == row.BuildingKey && c.Level == row.Level))
                    Assert.IsFalse(Overlap(RectOf(row.At, RoomFixturePlan.Footprint(row.Kind)), closet.Area),
                                   $"{row.Id} stands inside the bathroom closet");
        }

        [Test]
        public void EveryClosetClearsTheFurniture_TheDoorway_AndHasItsDoorInItsInnerWall()
        {
            InteriorKit.Contract contract = InteriorKit.Load();
            if (contract?.props == null || contract.props.Length == 0)
                Assert.Ignore("no baked interior contract: nothing to read prop footprints from");
            float halfDoor = StPetersInteriors.DoorwayWidthMetres * 0.5f;
            foreach (var closet in RoomFixturePlan.Closets)
            {
                Row any = RoomFixturePlan.Rows.First(r => r.BuildingKey == closet.BuildingKey && r.Level == closet.Level);
                foreach (var f in FurnitureFor(any))
                    Assert.IsFalse(Overlap(closet.Area, RectOf(f.RoomMetres, FurnitureSize(contract, f))),
                                   $"{closet.BuildingKey}'s closet {closet.Area} overlaps the {f.PropKey} at {f.RoomMetres}");
                Assert.LessOrEqual(closet.Area.xMax, -(halfDoor + 0.5f), $"{closet.BuildingKey}'s closet reaches the doorway lane");
                Assert.GreaterOrEqual(closet.DoorTo - closet.DoorFrom, 0.8f - Eps, $"{closet.BuildingKey}'s closet door is too narrow");
                Assert.IsTrue(closet.DoorFrom >= closet.Area.yMin && closet.DoorTo <= closet.Area.yMax,
                              $"{closet.BuildingKey}'s closet door is not in its inner wall");
            }
        }

        [Test]
        public void TheCountsMatchThePlanDoc()
        {
            // docs/world/kitchens-bathrooms-placement-plan.md section 1: 8 house-kit buildings, 54 stations
            // in St Peters, of which this table holds the 46 with exact coordinates (the two shops' 7 are zones
            // awaiting an editor look; the school's outhouse is the outhouse lane's).
            Assert.AreEqual(46, RoomFixturePlan.Rows.Count);
            Assert.AreEqual(8, RoomFixturePlan.Rows.Select(r => r.BuildingKey).Distinct().Count());
            Assert.AreEqual(7, RoomFixturePlan.Closets.Length);
        }

        // ---- the pinned row set (the per-building split is behaviour-neutral) -----------------------

        /// <summary>Every row in order: id, room, plan key, level, planned point and the plan wall it backs.
        /// A change to any building's rows must change this. Ginny's bathroom rows are its re-plan off the
        /// south wall; every other row is as the single-file table produced it.</summary>
        static readonly (string id, string room, string plan, int level, float x, float y, Wall wall)[] Pinned =
        {
            ("fixture.st_peters.ginny_cottage.prep_counter", "sageCottage", "ginnyCottage", 0, 2.7f, 1.425f, Wall.Left),
            ("fixture.st_peters.ginny_cottage.stove", "sageCottage", "ginnyCottage", 0, 2.7f, 2.425f, Wall.Left),
            ("fixture.st_peters.ginny_cottage.microwave", "sageCottage", "ginnyCottage", 0, 2.7f, 3.275f, Wall.Left),
            ("fixture.st_peters.ginny_cottage.kitchen_sink", "sageCottage", "ginnyCottage", 0, 1.75f, 3.425f, Wall.Front),
            ("fixture.st_peters.ginny_cottage.fridge", "sageCottage", "ginnyCottage", 0, -1.6f, 3.385f, Wall.Front),
            ("fixture.st_peters.ginny_cottage.shower", "sageCottage", "ginnyCottage", 0, -2.525f, -3.3f, Wall.Back),
            ("fixture.st_peters.ginny_cottage.toilet", "sageCottage", "ginnyCottage", 0, -1.6f, -3.375f, Wall.Back),
            ("fixture.st_peters.ginny_cottage.bathroom_sink", "sageCottage", "ginnyCottage", 0, -2.725f, -1.55f, Wall.Right),
            ("fixture.st_peters.sage_cottage.prep_counter", "sageCottage", null, 0, 2.7f, 1.425f, Wall.Left),
            ("fixture.st_peters.sage_cottage.stove", "sageCottage", null, 0, 2.7f, 2.425f, Wall.Left),
            ("fixture.st_peters.sage_cottage.microwave", "sageCottage", null, 0, 2.7f, 3.275f, Wall.Left),
            ("fixture.st_peters.sage_cottage.toilet", "sageCottage", null, 0, -2.65f, -3.375f, Wall.Back),
            ("fixture.st_peters.sage_cottage.bathroom_sink", "sageCottage", null, 0, -1.85f, -3.475f, Wall.Back),
            ("fixture.st_peters.sage_cottage.shower", "sageCottage", null, 0, -2.55f, -2.425f, Wall.Right),
            ("fixture.st_peters.red_saltbox.prep_counter", "redSaltbox", null, 0, 2.88f, 1.74f, Wall.Left),
            ("fixture.st_peters.red_saltbox.stove", "redSaltbox", null, 0, 2.88f, 2.74f, Wall.Left),
            ("fixture.st_peters.red_saltbox.microwave", "redSaltbox", null, 0, 2.88f, 3.59f, Wall.Left),
            ("fixture.st_peters.red_saltbox.toilet", "redSaltbox", null, 0, -2.83f, 3.69f, Wall.Front),
            ("fixture.st_peters.red_saltbox.bathroom_sink", "redSaltbox", null, 0, -2.03f, 3.79f, Wall.Front),
            ("fixture.st_peters.red_saltbox.shower", "redSaltbox", null, 0, -2.73f, 2.74f, Wall.Right),
            ("fixture.st_peters.white_farmhouse.prep_counter", "whiteFarmhouse", null, 0, 3.24f, 2.37f, Wall.Left),
            ("fixture.st_peters.white_farmhouse.stove", "whiteFarmhouse", null, 0, 3.24f, 3.37f, Wall.Left),
            ("fixture.st_peters.white_farmhouse.microwave", "whiteFarmhouse", null, 0, 3.24f, 4.22f, Wall.Left),
            ("fixture.st_peters.white_farmhouse.bath", "whiteFarmhouse", null, 0, -2.64f, -4.27f, Wall.Back),
            ("fixture.st_peters.white_farmhouse.toilet", "whiteFarmhouse", null, 0, -3.19f, -3.42f, Wall.Right),
            ("fixture.st_peters.white_farmhouse.bathroom_sink", "whiteFarmhouse", null, 0, -3.24f, -2.82f, Wall.Right),
            ("fixture.st_peters.harbour_gable.prep_counter", "harbourGable", "harbourGable", 0, 2.76f, 1.53f, Wall.Left),
            ("fixture.st_peters.harbour_gable.stove", "harbourGable", "harbourGable", 0, 2.76f, 2.53f, Wall.Left),
            ("fixture.st_peters.harbour_gable.microwave", "harbourGable", "harbourGable", 0, 2.76f, 3.38f, Wall.Left),
            ("fixture.st_peters.harbour_gable.toilet", "harbourGableUpper", "harbourGable", 1, -2.71f, -3.48f, Wall.Back),
            ("fixture.st_peters.harbour_gable.bathroom_sink", "harbourGableUpper", "harbourGable", 1, -1.91f, -3.58f, Wall.Back),
            ("fixture.st_peters.harbour_gable.shower", "harbourGableUpper", "harbourGable", 1, -2.61f, -2.53f, Wall.Right),
            ("fixture.st_peters.cream_saltbox.prep_counter", "creamSaltbox", "creamSaltbox", 0, 2.82f, 1.635f, Wall.Left),
            ("fixture.st_peters.cream_saltbox.stove", "creamSaltbox", "creamSaltbox", 0, 2.82f, 2.635f, Wall.Left),
            ("fixture.st_peters.cream_saltbox.microwave", "creamSaltbox", "creamSaltbox", 0, 2.82f, 3.485f, Wall.Left),
            ("fixture.st_peters.cream_saltbox.toilet", "creamSaltboxUpper", "creamSaltbox", 1, -2.77f, -3.585f, Wall.Back),
            ("fixture.st_peters.cream_saltbox.bathroom_sink", "creamSaltboxUpper", "creamSaltbox", 1, -1.97f, -3.685f, Wall.Back),
            ("fixture.st_peters.cream_saltbox.shower", "creamSaltboxUpper", "creamSaltbox", 1, -2.67f, -2.635f, Wall.Right),
            ("fixture.st_peters.white_gable.prep_counter", "whiteGable", "whiteGable", 0, 2.82f, 1.635f, Wall.Left),
            ("fixture.st_peters.white_gable.stove", "whiteGable", "whiteGable", 0, 2.82f, 2.635f, Wall.Left),
            ("fixture.st_peters.white_gable.microwave", "whiteGable", "whiteGable", 0, 2.82f, 3.485f, Wall.Left),
            ("fixture.st_peters.white_gable.toilet", "whiteGableUpper", "whiteGable", 1, -2.77f, -3.585f, Wall.Back),
            ("fixture.st_peters.white_gable.bathroom_sink", "whiteGableUpper", "whiteGable", 1, -1.97f, -3.685f, Wall.Back),
            ("fixture.st_peters.white_gable.shower", "whiteGableUpper", "whiteGable", 1, -2.67f, -2.635f, Wall.Right),
            ("fixture.st_peters.school.prep_counter", "school", null, 0, 2.58f, 1.215f, Wall.Left),
            ("fixture.st_peters.school.microwave", "school", null, 0, 2.58f, 2.215f, Wall.Left),
        };

        /// <summary>Each building's scene root and GameObject name, as the single-file table had them.</summary>
        static readonly Dictionary<string, (string root, string building)> PinnedBuildings = new Dictionary<string, (string, string)>
        {
            { "ginny_cottage", (StPetersGinnyPlot.RootName, StPetersGinnyPlot.CottageKey) },
            { "sage_cottage", (StPetersVillage.RootName, "sageCottage") },
            { "red_saltbox", (StPetersVillage.RootName, "redSaltbox") },
            { "white_farmhouse", (StPetersVillage.RootName, "whiteFarmhouse") },
            { "harbour_gable", (StPetersVillage.RootName, "harbourGable") },
            { "cream_saltbox", (StPetersVillage.RootName, "creamSaltbox") },
            { "white_gable", (StPetersVillage.RootName, "whiteGable") },
            { "school", (StPetersVillage.RootName, "school") },
        };

        [Test]
        public void TheRowSetIsPinned_IdsOrderRoomsLevelsAndPoints()
        {
            var rows = RoomFixturePlan.Rows;
            Assert.AreEqual(Pinned.Length, rows.Count, "row count");
            CollectionAssert.AreEqual(Pinned.Select(p => p.id).ToArray(), rows.Select(r => r.Id).ToArray(), "ids in order");
            for (int i = 0; i < Pinned.Length; i++)
            {
                var p = Pinned[i];
                Row r = rows[i];
                Assert.AreEqual(p.room, r.RoomKey, $"{p.id} room");
                Assert.AreEqual(p.plan, r.PlanKey, $"{p.id} plan key");
                Assert.AreEqual(p.level, r.Level, $"{p.id} level");
                Assert.AreEqual(p.x, r.At.x, 1e-4f, $"{p.id} x");
                Assert.AreEqual(p.y, r.At.y, 1e-4f, $"{p.id} y");
                Assert.AreEqual(p.wall, r.Wall, $"{p.id} wall");
                Assert.IsTrue(PinnedBuildings.TryGetValue(r.BuildingKey, out var b), $"{p.id}: unknown building");
                Assert.AreEqual(b.root, r.BuildingRoot, $"{p.id} building root");
                Assert.AreEqual(b.building, r.Building, $"{p.id} building");
            }
        }

        [Test]
        public void OnlyGinnysKitchenAndBathroomAreMarkedPlaced()
        {
            CollectionAssert.AreEquivalent(new[]
            {
                "fixture.st_peters.ginny_cottage.prep_counter",
                "fixture.st_peters.ginny_cottage.stove",
                "fixture.st_peters.ginny_cottage.microwave",
                "fixture.st_peters.ginny_cottage.kitchen_sink",
                "fixture.st_peters.ginny_cottage.fridge",
                "fixture.st_peters.ginny_cottage.shower",
                "fixture.st_peters.ginny_cottage.toilet",
                "fixture.st_peters.ginny_cottage.bathroom_sink",
            }, RoomFixturePlan.Rows.Where(r => r.Placed).Select(r => r.Id));
        }

        // ---- the backing wall and the north-first rule -------------------------------------------

        [Test]
        public void EachRowsWallIsTheNearestWallOfItsRoom()
        {
            foreach (Row row in RoomFixturePlan.Rows)
            {
                (float w, float l) = Declared[row.RoomKey];
                float hw = w * 0.5f, hl = l * 0.5f;
                Rect r = RectOf(row.At, RoomFixturePlan.Footprint(row.Kind));
                var gaps = new Dictionary<Wall, float>
                {
                    { Wall.Left, hw - r.xMax }, { Wall.Right, r.xMin + hw },
                    { Wall.Front, hl - r.yMax }, { Wall.Back, r.yMin + hl },
                };
                Wall nearest = gaps.OrderBy(g => g.Value).First().Key;
                Assert.AreEqual(nearest, row.Wall, $"{row.Id} at {row.At} is nearest its {nearest} wall");
            }
        }

        [Test]
        public void ThePlanWallsTurnToCompassFromTheDoor()
        {
            Assert.AreEqual(Compass.West, RoomFixturePlan.CompassOf(Compass.West, Wall.Front));
            Assert.AreEqual(Compass.North, RoomFixturePlan.CompassOf(Compass.West, Wall.Left));
            Assert.AreEqual(Compass.East, RoomFixturePlan.CompassOf(Compass.West, Wall.Back));
            Assert.AreEqual(Compass.South, RoomFixturePlan.CompassOf(Compass.West, Wall.Right));
            Assert.AreEqual(Compass.North, RoomFixturePlan.CompassOf(Compass.South, Wall.Back));
            Assert.AreEqual(Compass.NorthEast, RoomFixturePlan.CompassOf(Compass.SouthWest, Wall.Back));
            Assert.AreEqual(Compass.SouthEast, RoomFixturePlan.CompassOf(Compass.SouthWest, Wall.Right));
        }

        [Test]
        public void GinnysKitchenIsNorth_ToiletAndShowerEast_BasinTheOneRecordedSouthPiece()
        {
            Row Of(string slot) => RoomFixturePlan.Rows.Single(r => r.Id == "fixture.st_peters.ginny_cottage." + slot);
            Assert.AreEqual(Compass.North, Of("prep_counter").Backing);
            Assert.AreEqual(Compass.North, Of("stove").Backing);
            Assert.AreEqual(Compass.North, Of("microwave").Backing);
            Assert.AreEqual(Compass.West, Of("kitchen_sink").Backing);
            Assert.AreEqual(Compass.West, Of("fridge").Backing);
            Assert.AreEqual(Compass.East, Of("toilet").Backing);
            Assert.AreEqual(Compass.East, Of("shower").Backing);
            Assert.AreEqual(Compass.South, Of("bathroom_sink").Backing);

            var south = RoomFixturePlan.Rows
                .Where(r => r.BuildingKey == "ginny_cottage" && RoomFixturePlan.IsSouthWall(r.Backing))
                .Select(r => r.Id).ToList();
            CollectionAssert.AreEqual(new[] { "fixture.st_peters.ginny_cottage.bathroom_sink" }, south,
                                      "Ginny's only south-backed piece is the basin");
            foreach (string id in south)
                CollectionAssert.Contains(RoomFixturePlan.SouthWallExceptions, id, $"{id} is south-backed and unrecorded");
            CollectionAssert.DoesNotContain(RoomFixturePlan.SouthWallExceptions, "fixture.st_peters.ginny_cottage.toilet");
            CollectionAssert.DoesNotContain(RoomFixturePlan.SouthWallExceptions, "fixture.st_peters.ginny_cottage.shower");
        }

        [Test]
        public void GinnysShowerStandsClearOfTheEastGableWindow_AndNoPieceBlocksTheClosetDoor()
        {
            // The east gable's window pair is at +-0.42 x half-width, 0.82 m wide (appliance-wall plan 1.4):
            // a tall shower stands south of it; the low toilet may stand under it.
            const float windowCentre = 0.42f, windowWidth = 0.82f;
            float windowSouthEdge = -(RoomFixturePlan.SageW * 0.5f * windowCentre + windowWidth * 0.5f);
            Row shower = RoomFixturePlan.Rows.Single(r => r.Id == "fixture.st_peters.ginny_cottage.shower");
            Rect s = RectOf(shower.At, RoomFixturePlan.Footprint(Kind.Shower));
            Assert.LessOrEqual(s.xMax, windowSouthEdge + Eps, "the shower stands clear of the gable window");

            // Inside the closet door, an aisle the player's collider fits (0.7 m) is left clear.
            const float aisle = 0.7f;
            var closet = RoomFixturePlan.Closets.Single(c => c.BuildingKey == "ginny_cottage");
            foreach (Row row in RoomFixturePlan.Rows.Where(r => r.BuildingKey == "ginny_cottage" && RoomFixturePlan.IsBathroom(r.Kind)))
            {
                Rect r = RectOf(row.At, RoomFixturePlan.Footprint(row.Kind));
                bool blocksDoor = r.xMax > closet.Area.xMax - aisle && r.yMax > closet.DoorFrom && r.yMin < closet.DoorTo;
                Assert.IsFalse(blocksDoor, $"{row.Id} stands in front of the closet door");
            }
        }

        [Test]
        public void GinnysSinkAndFridgeFlankTheDoor_ClearOfTheLaneTheMicrowaveAndTheirOwnDoorSwing()
        {
            Row Of(string slot) => RoomFixturePlan.Rows.Single(r => r.Id == "fixture.st_peters.ginny_cottage." + slot);
            float halfDoor = StPetersInteriors.DoorwayWidthMetres * 0.5f;
            Row sink = Of("kitchen_sink"), fridge = Of("fridge"), micro = Of("microwave");
            Rect s = RectOf(sink.At, RoomFixturePlan.Footprint(Kind.KitchenSink));
            Rect f = RectOf(fridge.At, RoomFixturePlan.Footprint(Kind.Fridge));
            Rect m = RectOf(micro.At, RoomFixturePlan.Footprint(Kind.Microwave));

            // One each side of the doorway (+x is the north side here), neither reaching into the doorway itself.
            Assert.Greater(s.xMin, halfDoor, "the sink stands north of the doorway, clear of it");
            Assert.Less(f.xMax, -halfDoor, "the fridge stands south of the doorway, clear of it");

            // The sink joins the run: beside the microwave with a gap, not on it.
            Assert.Greater(m.xMin - s.xMax, 0.2f - Eps, "the sink leaves 0.2 m beside the microwave");
            Assert.Greater(s.yMax, m.yMin, "the sink and the microwave are one run along the north-west corner");

            // The fridge door hangs on its right as you face it: facing the west wall that is north, the doorway
            // side. The rig asks 0.2 m beside the hinge and 0.75 m clear in front (appliance layout note).
            Assert.GreaterOrEqual(-halfDoor - f.xMax, 0.2f - Eps, "room for the fridge door beside its hinge");
            var front = Rect.MinMaxRect(f.xMin, f.yMin - 0.75f, f.xMax, f.yMin);
            InteriorKit.Contract contract = InteriorKit.Load();
            if (contract?.props != null && contract.props.Length > 0)
                foreach (var furn in FurnitureFor(fridge))
                    Assert.IsFalse(Overlap(front, RectOf(furn.RoomMetres, FurnitureSize(contract, furn))),
                                   $"the fridge's door swing meets the {furn.PropKey} at {furn.RoomMetres}");
        }

        [Test]
        public void NoRowBacksOntoASouthWall_UnlessItIsAListedException()
        {
            var offenders = RoomFixturePlan.Rows
                .Where(r => RoomFixturePlan.IsSouthWall(r.Backing) && !RoomFixturePlan.SouthWallExceptions.Contains(r.Id))
                .Select(r => $"{r.Id} backs onto the {r.Backing} wall")
                .ToList();
            Assert.IsEmpty(offenders, "kitchens and bathrooms go on north walls:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void EverySouthWallExceptionIsAPlannedSouthBackedRow()
        {
            foreach (string id in RoomFixturePlan.SouthWallExceptions)
            {
                Row[] hits = RoomFixturePlan.Rows.Where(r => r.Id == id).ToArray();
                Assert.AreEqual(1, hits.Length, $"exception '{id}' names no row");
                Assert.IsTrue(RoomFixturePlan.IsSouthWall(hits[0].Backing),
                              $"exception '{id}' backs onto {hits[0].Backing} now; take it off the list");
            }
        }
    }
}
