using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using Kind = HiddenHarbours.App.Editor.RoomFixturePlan.Kind;
using Row = HiddenHarbours.App.Editor.RoomFixturePlan.Row;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Ginny's cottage as the player walks it (owner 2026-11-22: three rooms on the ground floor, the bedrooms
    /// upstairs, the stairs against a wall). The ground floor's walls, furniture and placed kitchen and
    /// washroom pieces are laid on a grid in the plan frame (+x north, +y toward the front door) and the
    /// player's collider is walked over it from the front door: an ellipse, because his round collider sits
    /// on the foreshortened ground (0.35 m across, 0.35 / depth scale along the room). The press reaches from
    /// his transform, 0.7 m up the screen from his feet, which is what every reach below measures from.
    /// </summary>
    public class GinnyCottageRoomsTests
    {
        const float Step = 0.05f;
        const float Radius = 0.35f, FeetBelowTransform = 0.7f;
        static float K => HiddenHarbours.Core.IsoGround.GroundDepthScale;
        static float HalfW => RoomFixturePlan.SageW * 0.5f;
        static float HalfL => RoomFixturePlan.SageL * 0.5f;

        static StPetersInteriors.UpperLevelPlan Plan =>
            StPetersInteriors.UpperLevelFor(StPetersInteriors.GinnyCottagePlanKey);

        static IEnumerable<Row> Placed =>
            RoomFixturePlan.Rows.Where(r => r.BuildingKey == "ginny_cottage" && r.Level == 0);

        static Rect RectOf(Vector2 centre, Vector2 size) =>
            new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, size.x, size.y);

        static Rect RectOf(StPetersInteriors.WallRect w) => Rect.MinMaxRect(w.X0, w.Y0, w.X1, w.Y1);

        static Vector2 FurnitureSize(InteriorKit.Contract contract, StPetersInteriors.Furnishing f)
        {
            InteriorKit.Entry prop = InteriorKit.FindProp(contract, f.PropKey);
            if (prop == null) return Vector2.zero;
            float w = prop.propFootprintWidth, d = prop.propFootprintDepth;
            int q = ((f.FacingOffset % 8) + 8) % 8;
            if (q % 2 != 0) { float diag = Mathf.Sqrt(w * w + d * d); return new Vector2(diag, diag); }
            return q == 2 || q == 6 ? new Vector2(d, w) : new Vector2(w, d);
        }

        static InteriorKit.Contract Contract()
        {
            InteriorKit.Contract contract = InteriorKit.Load();
            if (contract?.props == null || contract.props.Length == 0)
                Assert.Ignore("no baked interior contract: nothing to read furniture footprints from");
            return contract;
        }

        /// <summary>Everything solid on the ground floor: its walls, its furniture, the placed pieces.</summary>
        static List<Rect> GroundObstacles(InteriorKit.Contract contract)
        {
            var obs = Plan.GroundWalls.Select(RectOf).ToList();
            foreach (var f in StPetersInteriors.GroundFurnishingsFor(RoomFixturePlan.Rows.First(r => r.BuildingKey == "ginny_cottage").RoomKey,
                                                                     StPetersInteriors.GinnyCottagePlanKey))
                obs.Add(RectOf(f.RoomMetres, FurnitureSize(contract, f)));
            foreach (Row row in Placed) obs.Add(RectOf(row.At, RoomFixturePlan.FootprintOf(row)));
            return obs;
        }

        static List<Rect> UpperObstacles(InteriorKit.Contract contract)
        {
            var obs = Plan.Partitions.Select(RectOf).ToList();
            foreach (var f in Plan.Furnishings)
                if (f.Fixture != StPetersInteriors.InteriorFixture.StairDown)
                    obs.Add(RectOf(f.RoomMetres, FurnitureSize(contract, f)));
            return obs;
        }

        static bool Free(Vector2 feet, List<Rect> obs)
        {
            float rx = Radius / K, ry = Radius;
            if (feet.x - rx < -HalfW || feet.x + rx > HalfW || feet.y - ry < -HalfL || feet.y + ry > HalfL) return false;
            foreach (Rect r in obs)
            {
                float cx = Mathf.Clamp(feet.x, r.xMin, r.xMax), cy = Mathf.Clamp(feet.y, r.yMin, r.yMax);
                float dx = (feet.x - cx) * K, dy = feet.y - cy;
                if (dx * dx + dy * dy < Radius * Radius - 1e-6f) return false;
            }
            return true;
        }

        static (int, int) Cell(Vector2 p) => (Mathf.RoundToInt(p.x / Step), Mathf.RoundToInt(p.y / Step));
        static Vector2 At((int x, int y) c) => new Vector2(c.x * Step, c.y * Step);

        static HashSet<(int, int)> Flood(IEnumerable<Vector2> starts, List<Rect> obs)
        {
            var seen = new HashSet<(int, int)>();
            var stack = new Stack<(int, int)>();
            foreach (Vector2 s in starts)
            {
                var c = Cell(s);
                if (Free(At(c), obs) && seen.Add(c)) stack.Push(c);
            }
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                foreach (var n in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    if (!seen.Contains(n) && Free(At(n), obs)) { seen.Add(n); stack.Push(n); }
            }
            return seen;
        }

        static Vector2 FrontDoorFeet => new Vector2(0f, HalfL - 0.45f);

        /// <summary>The press distance from a stance (feet) to a point: from his transform, over the ground.</summary>
        static float Reach(Vector2 feet, Vector2 target)
        {
            Vector2 transform = new Vector2(-feet.y, feet.x * K + FeetBelowTransform);
            return Vector2.Distance(transform, new Vector2(-target.y, target.x * K));
        }

        static Dictionary<Row, float> ReachOf()
        {
            var reach = new Dictionary<Row, float>();
            foreach (Row row in Placed)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteriorFixturePlacement.PrefabFor(row.Kind));
                var verb = prefab != null ? prefab.GetComponent<HiddenHarbours.Core.IInteractable>() : null;
                if (verb != null) reach[row] = verb.ReachMeters;
            }
            return reach;
        }

        enum RoomOf { Living, Kitchen, Washroom }

        static RoomOf Where(Vector2 feet)
        {
            var closet = RoomFixturePlan.Closets.Single(c => c.BuildingKey == "ginny_cottage");
            if (feet.y > closet.Area.yMax) return RoomOf.Living;
            return feet.x < closet.Area.xMax ? RoomOf.Washroom : RoomOf.Kitchen;
        }

        static RoomOf Where(Row row) => RoomFixturePlan.IsBathroom(row.Kind) ? RoomOf.Washroom : RoomOf.Kitchen;

        /// <summary>The gaps (doorways) along the line the living room's back partition runs.</summary>
        static List<(float from, float to)> BackPartitionGaps()
        {
            var closet = RoomFixturePlan.Closets.Single(c => c.BuildingKey == "ginny_cottage");
            float line = closet.Area.yMax + 0.15f;
            var spans = Plan.GroundWalls.Where(w => w.Y0 <= line && w.Y1 >= line)
                            .Select(w => (Mathf.Max(w.X0, -HalfW), Mathf.Min(w.X1, HalfW))).OrderBy(s => s.Item1).ToList();
            var gaps = new List<(float, float)>();
            float at = -HalfW;
            foreach (var (a, b) in spans)
            {
                if (a > at + 1e-3f) gaps.Add((at, a));
                at = Mathf.Max(at, b);
            }
            if (at < HalfW - 1e-3f) gaps.Add((at, HalfW));
            return gaps;
        }

        // ---- the three rooms -------------------------------------------------------------------

        [Test]
        public void TheGroundFloorIsThreeRooms_EachEnteredByItsOwnDoorway()
        {
            InteriorKit.Contract contract = Contract();
            var obs = GroundObstacles(contract);
            var gaps = BackPartitionGaps();
            Assert.AreEqual(2, gaps.Count, "the back partition has exactly two doorways: the kitchen's and the washroom's");
            foreach (var (from, to) in gaps)
                Assert.GreaterOrEqual(to - from, 2f * Radius / K - 1e-3f, $"the doorway {from}..{to} is narrower than he is");

            var open = Flood(new[] { FrontDoorFeet }, obs).Select(At).ToList();
            Assert.IsTrue(open.Any(p => Where(p) == RoomOf.Kitchen), "the kitchen cannot be walked into from the front door");
            Assert.IsTrue(open.Any(p => Where(p) == RoomOf.Washroom), "the washroom cannot be walked into from the front door");

            var closet = RoomFixturePlan.Closets.Single(c => c.BuildingKey == "ginny_cottage");
            foreach (var shut in gaps)
            {
                var plugged = new List<Rect>(obs) { Rect.MinMaxRect(shut.from, closet.Area.yMax, shut.to, closet.Area.yMax + 0.3f) };
                var reached = Flood(new[] { FrontDoorFeet }, plugged).Select(At).ToList();
                RoomOf behind = (shut.from + shut.to) * 0.5f < closet.Area.xMax ? RoomOf.Washroom : RoomOf.Kitchen;
                Assert.IsFalse(reached.Any(p => Where(p) == behind),
                               $"with the doorway {shut.from}..{shut.to} shut the {behind} is still walked into: its walls leak");
            }
        }

        [Test]
        public void TheKitchenHoldsTheFivePieces_TheWashroomTheThreeBehindItsDoor()
        {
            var closet = RoomFixturePlan.Closets.Single(c => c.BuildingKey == "ginny_cottage");
            Assert.IsTrue(closet.DoorInFront, "the washroom is entered from the living room in front of it");
            CollectionAssert.AreEquivalent(new[] { Kind.Fridge, Kind.KitchenSink, Kind.Stove, Kind.PrepCounter, Kind.Microwave },
                                           Placed.Where(r => !RoomFixturePlan.IsBathroom(r.Kind)).Select(r => r.Kind));
            CollectionAssert.AreEquivalent(new[] { Kind.BathroomSink, Kind.Toilet, Kind.Shower },
                                           Placed.Where(r => RoomFixturePlan.IsBathroom(r.Kind)).Select(r => r.Kind));
            foreach (Row row in Placed)
            {
                Rect r = RectOf(row.At, RoomFixturePlan.FootprintOf(row));
                foreach (var w in Plan.GroundWalls)
                    Assert.IsFalse(r.Overlaps(RectOf(w)) && r.xMin < w.X1 - 1e-3f && w.X0 < r.xMax - 1e-3f && r.yMin < w.Y1 - 1e-3f && w.Y0 < r.yMax - 1e-3f,
                                   $"{row.Id} stands in a wall: {w.Why}");
                Vector2 c = new Vector2(Mathf.Clamp(row.At.x, -HalfW + 0.01f, HalfW - 0.01f), row.At.y);
                Assert.AreEqual(Where(row), Where(c), $"{row.Id} is not in the {Where(row)}");
            }
        }

        // ---- reach ------------------------------------------------------------------------------

        [Test]
        public void EveryPieceAndTheStairIsReachableFromTheFrontDoor_AndOfferedThere()
        {
            InteriorKit.Contract contract = Contract();
            var walk = Flood(new[] { FrontDoorFeet }, GroundObstacles(contract)).Select(At).ToList();
            var reach = ReachOf();
            Assert.AreEqual(7, reach.Count, "every ground-floor piece but the fridge has a verb");
            foreach (var kv in reach)
            {
                bool offered = walk.Any(p => Reach(p, kv.Key.At) <= kv.Value &&
                                             reach.All(o => o.Key.Id == kv.Key.Id || Reach(p, o.Key.At) > o.Value ||
                                                            Reach(p, o.Key.At) > Reach(p, kv.Key.At)));
                Assert.IsTrue(offered, $"{kv.Key.Id} is never the piece offered anywhere he can walk");
            }
            Vector2 stair = new Vector2(StPetersInteriors.GinnyStairAtX, StPetersInteriors.GinnyStairAtY);
            Assert.IsTrue(walk.Any(p => Reach(p, stair) <= StPetersInteriors.StairReachMetres),
                          "the foot of the stairs cannot be reached from the front door");
        }

        [Test]
        public void NothingIsOfferedThroughAPartition()
        {
            InteriorKit.Contract contract = Contract();
            var walk = Flood(new[] { FrontDoorFeet }, GroundObstacles(contract)).Select(At).ToList();
            var reach = ReachOf();
            foreach (Vector2 p in walk)
            {
                Row best = default; float bd = float.MaxValue; bool any = false;
                foreach (var kv in reach)
                {
                    float d = Reach(p, kv.Key.At);
                    if (d <= kv.Value && d < bd) { bd = d; best = kv.Key; any = true; }
                }
                if (any) Assert.AreEqual(Where(best), Where(p), $"standing at {p} in the {Where(p)}, the {best.Id} is offered through the wall");
            }
        }

        // ---- one storey at a time, and the stairs both ways ---------------------------------------

        [Test]
        public void NoBedStandsOnTheGroundFloor_BothAreUpstairs()
        {
            string room = RoomFixturePlan.Rows.First(r => r.BuildingKey == "ginny_cottage").RoomKey;
            var ground = StPetersInteriors.GroundFurnishingsFor(room, StPetersInteriors.GinnyCottagePlanKey)
                                          .Concat(Plan.GroundAdditions).ToList();
            Assert.IsFalse(ground.Any(f => f.PropKey == "bed"), "a bed stands on Ginny's ground floor");
            Assert.AreEqual(2, Plan.Furnishings.Count(f => f.PropKey == "bed"), "both beds are upstairs");
            // The village's pilot cottage is the same build and keeps its own one-room list.
            CollectionAssert.AreEqual(StPetersInteriors.FurnishingsFor(room).Select(f => f.PropKey),
                                      StPetersInteriors.GroundFurnishingsFor(room, null).Select(f => f.PropKey));
        }

        [Test]
        public void TheStoreyAboveDrawsItsOwnSheet_NeverTheGroundFloors()
        {
            Assert.IsTrue(Plan.HasOwnUpperPicture, "Ginny's upstairs has no picture of its own");
            var upper = AssetDatabase.LoadAssetAtPath<Sprite>(Plan.UpperSheetPath);
            Assert.IsNotNull(upper, $"no sprite at {Plan.UpperSheetPath}");
            InteriorCatalog.Placement room = InteriorCatalog.FindRoom(RoomFixturePlan.Rows.First(r => r.BuildingKey == "ginny_cottage").RoomKey);
            Assert.IsTrue(room.IsValid);
            for (int facing = 0; facing < room.Entry.facings; facing++)
                Assert.AreNotSame(InteriorCatalog.LoadFacing(room, facing), upper, "the storey above draws the ground floor's sheet");
            Assert.AreEqual(StPetersInteriors.GroundWallsChildName, "WallsGround");
            Assert.Greater(Plan.GroundWalls.Count, 0, "the ground floor's partitions must ride the ground storey, switched off upstairs");
        }

        [Test]
        public void TheStairsWorkBothWays_AndStandAgainstTheBackPartition()
        {
            InteriorKit.Contract contract = Contract();
            Vector2 stair = new Vector2(StPetersInteriors.GinnyStairAtX, StPetersInteriors.GinnyStairAtY);
            var closet = RoomFixturePlan.Closets.Single(c => c.BuildingKey == "ginny_cottage");
            Assert.LessOrEqual(StPetersInteriors.GinnyStairY0, closet.Area.yMax + 0.3f + 1e-3f,
                               "the run stands against the living room's back partition");
            Assert.GreaterOrEqual(StPetersInteriors.GinnyStairX1, HalfW, "the run rises to the north wall");

            var groundObs = GroundObstacles(contract);
            var upperObs = UpperObstacles(contract);
            var ground = Flood(new[] { FrontDoorFeet }, groundObs);
            var up = ground.Select(At).Where(p => Reach(p, stair) <= StPetersInteriors.StairReachMetres).ToList();
            Assert.IsNotEmpty(up, "nowhere on the ground floor reaches the foot of the stairs");

            // He arrives where he pressed and the landing pushes him to the nearest open floor
            // (BuildingInterior.ResolveLanding, minimum separation); from there the whole storey above (both
            // beds, the wardrobe) is his. Within 1.5 m: never thrown across the room.
            var arrivals = new List<Vector2>();
            foreach (Vector2 p in up)
            {
                Vector2? landing = NearestFree(p, upperObs, 1.5f);
                Assert.IsTrue(landing.HasValue, $"pressing the stairs from {p} lands in a wall upstairs");
                arrivals.Add(landing.Value);
            }
            var upstairs = Flood(arrivals, upperObs);
            foreach (var f in Plan.Furnishings.Where(f => f.Fixture == StPetersInteriors.InteriorFixture.PlayerBed ||
                                                          f.Fixture == StPetersInteriors.InteriorFixture.OwnerBed ||
                                                          f.Fixture == StPetersInteriors.InteriorFixture.Wardrobe))
            {
                float r = f.Fixture == StPetersInteriors.InteriorFixture.Wardrobe ? StPetersInteriors.WardrobeReachMetres
                                                                                  : StPetersInteriors.BedReachMetres;
                Assert.IsTrue(upstairs.Select(At).Any(p => Reach(p, f.RoomMetres) <= r), $"upstairs, the {f.PropKey} at {f.RoomMetres} is out of reach");
            }

            var down = upstairs.Select(At).Where(p => Reach(p, stair) <= StPetersInteriors.StairReachMetres).ToList();
            Assert.IsNotEmpty(down, "nowhere upstairs reaches the head of the stairs");
            foreach (Vector2 p in down)
            {
                Vector2? landing = NearestFree(p, groundObs, 1.5f);
                Assert.IsTrue(landing.HasValue && ground.Contains(Cell(landing.Value)),
                              $"coming down from {p} lands off the ground floor's walkable floor");
            }
        }

        static Vector2? NearestFree(Vector2 p, List<Rect> obs, float within)
        {
            if (Free(p, obs)) return p;
            int n = Mathf.CeilToInt(within / Step);
            Vector2? best = null; float bd = float.MaxValue;
            for (int i = -n; i <= n; i++)
            for (int j = -n; j <= n; j++)
            {
                Vector2 q = At(Cell(p)) + new Vector2(i * Step, j * Step);
                float d = Vector2.Distance(new Vector2(q.x * K, q.y), new Vector2(p.x * K, p.y));
                if (d <= within && d < bd && Free(q, obs)) { bd = d; best = q; }
            }
            return best;
        }
    }
}
