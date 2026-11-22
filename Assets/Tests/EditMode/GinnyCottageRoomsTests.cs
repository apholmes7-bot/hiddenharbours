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
    /// his transform, 0.7 m up the screen from his collider, which is what every reach below measures from.
    /// "Feet" below is the collider's centre; his drawn feet stand 0.31 m below the transform.
    /// </summary>
    public class GinnyCottageRoomsTests
    {
        const float Step = 0.05f;
        const float Radius = 0.35f, FeetBelowTransform = 0.7f, DrawnFeetBelowTransform = 0.31f;
        const float WallThickness = 0.3f;
        static float K => HiddenHarbours.Core.IsoGround.GroundDepthScale;
        static float HalfW => RoomFixturePlan.GinnyW * 0.5f;
        static float HalfL => RoomFixturePlan.GinnyL * 0.5f;
        // The floor inside the outer walls: what his collider must stay within.
        static float InnerW => HalfW - WallThickness;
        static float InnerL => HalfL - WallThickness;
        /// <summary>Plan x from his collider up to his transform (where a stair lands the transform).</summary>
        static float Lift => FeetBelowTransform / K;
        /// <summary>Plan x from his collider up to his drawn feet.</summary>
        static float DrawnLift => (FeetBelowTransform - DrawnFeetBelowTransform) / K;

        static Vector2 StairFoot => new Vector2(StPetersInteriors.GinnyStairAtX, StPetersInteriors.GinnyStairAtY);
        static Vector2 StairHead => new Vector2(StPetersInteriors.GinnyStairHeadX, StPetersInteriors.GinnyStairHeadY);

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
            obs.Add(WashstandRect);
            return obs;
        }

        /// <summary>The washstand's round collider (radius in world units) laid on the plan: wider in plan x
        /// by the depth scale. It is placed by <see cref="InteriorFixturePlacement"/>, not listed with the
        /// room's furnishings, so it is added here by its site.</summary>
        const float WashstandColliderRadius = 0.3f;
        static Rect WashstandRect
        {
            get
            {
                var site = InteriorFixturePlacement.Sites.Single(s => s.InteractId == WashStationPlacement.GinnyWashstandId);
                return RectOf(site.At, new Vector2(2f * WashstandColliderRadius / K, 2f * WashstandColliderRadius));
            }
        }

        [Test]
        public void UpstairsNoFurnitureStandsOnTheWashstand()
        {
            InteriorKit.Contract contract = Contract();
            Rect wash = WashstandRect;
            foreach (var f in Plan.Furnishings.Where(f => f.Fixture != StPetersInteriors.InteriorFixture.StairDown))
                Assert.IsFalse(RectOf(f.RoomMetres, FurnitureSize(contract, f)).Overlaps(wash),
                               $"the {f.PropKey} at {f.RoomMetres} stands on the washstand at {wash.center}");
        }

        static bool Free(Vector2 feet, List<Rect> obs)
        {
            float rx = Radius / K, ry = Radius;
            if (feet.x - rx < -InnerW || feet.x + rx > InnerW || feet.y - ry < -InnerL || feet.y + ry > InnerL) return false;
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

        static Vector2 FrontDoorFeet => new Vector2(0f, InnerL - Radius - 0.05f);

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
        public void TheStairsWorkBothWays_FootToHeadAndBack()
        {
            InteriorKit.Contract contract = Contract();
            var groundObs = GroundObstacles(contract);
            var upperObs = UpperObstacles(contract);
            var ground = Flood(new[] { FrontDoorFeet }, groundObs);
            var up = ground.Select(At).Where(p => Reach(p, StairFoot) <= StPetersInteriors.StairReachMetres).ToList();
            Assert.IsNotEmpty(up, "nowhere on the ground floor reaches the foot of the stairs");

            // Going up lands his transform on the head of the stairs (the foot's landing); his collider is then
            // Lift below it, and BuildingInterior.ResolveLanding pushes him clear of anything solid.
            Vector2? arrival = NearestFree(StairHead - new Vector2(Lift, 0f), upperObs, 1.5f);
            Assert.IsTrue(arrival.HasValue, "going up lands him in a wall upstairs");
            var upstairs = Flood(new[] { arrival.Value }, upperObs);
            foreach (var f in Plan.Furnishings.Where(f => f.Fixture == StPetersInteriors.InteriorFixture.PlayerBed ||
                                                          f.Fixture == StPetersInteriors.InteriorFixture.OwnerBed ||
                                                          f.Fixture == StPetersInteriors.InteriorFixture.Wardrobe))
            {
                float r = f.Fixture == StPetersInteriors.InteriorFixture.Wardrobe ? StPetersInteriors.WardrobeReachMetres
                                                                                  : StPetersInteriors.BedReachMetres;
                Assert.IsTrue(upstairs.Select(At).Any(p => Reach(p, f.RoomMetres) <= r), $"upstairs, the {f.PropKey} at {f.RoomMetres} is out of reach");
            }

            var down = upstairs.Select(At).Where(p => Reach(p, StairHead) <= StPetersInteriors.StairReachMetres).ToList();
            Assert.IsNotEmpty(down, "nowhere upstairs reaches the head of the stairs");

            // Coming down lands his transform on the foot.
            Vector2? back = NearestFree(StairFoot - new Vector2(Lift, 0f), groundObs, 1.5f);
            Assert.IsTrue(back.HasValue && ground.Contains(Cell(back.Value)),
                          "coming down lands him off the ground floor's walkable floor");
        }

        [Test]
        public void TheStairsStandAgainstAnOutsideWall_ClearOfTheDoorways()
        {
            Assert.GreaterOrEqual(StPetersInteriors.GinnyStairX1, InnerW - 1e-3f,
                                  "the run does not stand against the north wall's inside face");
            float along = StPetersInteriors.GinnyStairY1 - StPetersInteriors.GinnyStairY0;
            float across = StPetersInteriors.GinnyStairX1 - StPetersInteriors.GinnyStairX0;
            Assert.Greater(along, 2f * across, "the run does not lie along the wall: it juts into the room");
            // He walks THROUGH a doorway across the partition, so the run must leave the doorway its
            // whole width and his body room past its north jamb: one collider radius beyond it.
            foreach (var (from, to) in BackPartitionGaps())
                Assert.Greater(StPetersInteriors.GinnyStairX0, to + Radius / K - 1e-3f,
                               $"the run stands in the walk to the doorway {from}..{to}");
            Assert.IsTrue(Plan.GroundWalls.Any(w => Mathf.Approximately(w.X0, StPetersInteriors.GinnyStairX0) &&
                                                    Mathf.Approximately(w.Y1, StPetersInteriors.GinnyStairY1)),
                          "the run has no collider on the ground floor");
        }

        /// <summary>
        /// Owner 2026-11-22: "theres no floor space in front of the stairs". The run used to reach the front
        /// wall, so its foot met plaster. Between the foot and the wall there must be a landing he can stand
        /// on and turn: as wide as the flight, at least one body (two collider radii) deep, nothing on it,
        /// walked onto from the front door, and the stair offered from it.
        /// </summary>
        [Test]
        public void TheFootOfTheStairsHasClearFloorInFrontOfIt_ALandingHeCanStandOn()
        {
            InteriorKit.Contract contract = Contract();
            float depth = InnerL - StPetersInteriors.GinnyStairY1;
            Assert.GreaterOrEqual(depth, 2f * Radius - 1e-3f,
                                  $"between the foot of the run and the front wall there is only {depth:0.00} m of floor");

            Rect landing = Rect.MinMaxRect(StPetersInteriors.GinnyStairX0, StPetersInteriors.GinnyStairY1,
                                           StPetersInteriors.GinnyStairX1, InnerL);
            var obs = GroundObstacles(contract);
            foreach (Rect r in obs)
                Assert.IsFalse(r.xMin < landing.xMax - 1e-3f && landing.xMin < r.xMax - 1e-3f &&
                               r.yMin < landing.yMax - 1e-3f && landing.yMin < r.yMax - 1e-3f,
                               $"something solid stands on the landing in front of the stair foot: {r}");

            // A stance on the landing, his body square to the foot: as far north as the wall lets his collider go.
            var stance = new Vector2(Mathf.Min(0.5f * (landing.xMin + landing.xMax), InnerW - Radius / K),
                                     StPetersInteriors.GinnyStairY1 + Radius + Step);
            Assert.IsTrue(Free(stance, obs), $"he cannot stand on the landing at {stance}");
            var walk = Flood(new[] { FrontDoorFeet }, obs);
            Assert.IsTrue(walk.Contains(Cell(stance)), "the landing cannot be walked onto from the front door");
            Assert.LessOrEqual(Reach(stance, StairFoot), StPetersInteriors.StairReachMetres,
                               "standing on the landing, the stairs are not offered");
            Assert.IsTrue(landing.Contains(StairFoot), "the stair's interact point is not on the landing");
        }

        /// <summary>
        /// Upstairs, a tall piece standing in front of the stairwell draws its top over it (the wardrobe did,
        /// pass 4). Nothing upstairs but the stair head may stand beside the stairwell's span of the room.
        /// </summary>
        [Test]
        public void UpstairsNothingTallStandsInFrontOfTheStairwell()
        {
            InteriorKit.Contract contract = Contract();
            foreach (var f in Plan.Furnishings.Where(f => f.Fixture != StPetersInteriors.InteriorFixture.StairDown))
            {
                Rect r = RectOf(f.RoomMetres, FurnitureSize(contract, f));
                bool besideTheWell = r.yMin < StPetersInteriors.GinnyStairY1 - 1e-3f && StPetersInteriors.GinnyStairY0 < r.yMax - 1e-3f
                                     && r.xMax > StPetersInteriors.GinnyCorridorX0 - 1.0f;
                Assert.IsFalse(besideTheWell && f.PropKey == "dresser",
                               $"the {f.PropKey} at {f.RoomMetres} stands against the stairwell's partition and draws over the well");
            }
        }

        [Test]
        public void HeCanStandInTheWashroom_ClearOfThePartitionPaint()
        {
            InteriorKit.Contract contract = Contract();
            var walk = Flood(new[] { FrontDoorFeet }, GroundObstacles(contract)).Select(At).ToList();
            var clear = walk.Where(p => p.y < 0f && Where(p) == RoomOf.Washroom)
                            .Where(p =>
                            {
                                Vector2 drawn = p + new Vector2(DrawnLift, 0f);
                                return Plan.GroundWalls.All(w => Mathf.Min(Mathf.Min(drawn.x - w.X0, w.X1 - drawn.x),
                                                                           Mathf.Min(drawn.y - w.Y0, w.Y1 - drawn.y)) <= 0.1f);
                            }).ToList();
            Assert.IsNotEmpty(clear, "nowhere in the washroom do his drawn feet stand clear of the walls");
            float depth = clear.Max(p => p.x) - clear.Min(p => p.x);
            Assert.GreaterOrEqual(depth, 1.0f, $"the washroom gives him only {depth:0.00} m to stand in, south to north");
        }

        [Test]
        public void TheStairHeadIsClearFloor_AndBothBedroomsOpenOffIt_EachByItsOwnDoor()
        {
            InteriorKit.Contract contract = Contract();
            var upperObs = UpperObstacles(contract);
            Vector2 arrival = StairHead - new Vector2(Lift, 0f);
            Assert.IsTrue(Free(arrival, upperObs), $"he arrives at the stair head ({arrival}) in a wall or the stairwell");

            var landing = Flood(new[] { arrival }, upperObs).Select(At).ToList();
            float corridorMid = (StPetersInteriors.GinnyCorridorX0 + InnerW) * 0.5f;
            Vector2 corridor = landing.Where(p => Mathf.Abs(p.x - corridorMid) < 0.15f)
                                      .OrderBy(p => (p - arrival).sqrMagnitude).FirstOrDefault();
            Assert.AreNotEqual(Vector2.zero, corridor, "the stair head does not open onto the corridor");

            bool InPlayerRoom(Vector2 p) => p.x < StPetersInteriors.GinnyCorridorX0 - 0.3f && p.y > StPetersInteriors.GinnyPlayerDoorY0;
            bool InOwnerRoom(Vector2 p) => p.x < StPetersInteriors.GinnyCorridorX0 - 0.3f && p.y < StPetersInteriors.GinnyOwnerDoorY1;
            var open = Flood(new[] { corridor }, upperObs).Select(At).ToList();
            Assert.IsTrue(open.Any(InPlayerRoom), "his room cannot be walked into from the stair head");
            Assert.IsTrue(open.Any(InOwnerRoom), "Ginny's room cannot be walked into from the stair head");

            float px0 = StPetersInteriors.GinnyCorridorX0 - 0.3f, px1 = StPetersInteriors.GinnyCorridorX0;
            var playerShut = new List<Rect>(upperObs)
                { Rect.MinMaxRect(px0, StPetersInteriors.GinnyPlayerDoorY0, px1, StPetersInteriors.GinnyPlayerDoorY1) };
            var a = Flood(new[] { corridor }, playerShut).Select(At).ToList();
            Assert.IsFalse(a.Any(InPlayerRoom), "with his door shut his room is still walked into: it is a way through");
            Assert.IsTrue(a.Any(InOwnerRoom), "Ginny's room is entered only through his");

            var ownerShut = new List<Rect>(upperObs)
                { Rect.MinMaxRect(px0, StPetersInteriors.GinnyOwnerDoorY0, px1, StPetersInteriors.GinnyOwnerDoorY1) };
            var b = Flood(new[] { corridor }, ownerShut).Select(At).ToList();
            Assert.IsFalse(b.Any(InOwnerRoom), "with Ginny's door shut her room is still walked into: it is a way through");
            Assert.IsTrue(b.Any(InPlayerRoom), "his room is entered only through Ginny's");
        }

        /// <summary>
        /// The real building, as the builder stands it, walked upstairs over its LIVE colliders: from the
        /// stair head both bedrooms are reached, and no cell he reaches is outside the upper floor. The
        /// storey above is drawn UpperLevelY up the screen, so the ground floor's north wall crosses the
        /// upper floor; this is what catches it walling the two south bedrooms off, and an upper storey
        /// with no outer walls of its own letting him walk out into the air.
        /// </summary>
        [Test]
        public void UpstairsTheLiveWallsLetHimIntoBothBedrooms_AndNotOutOfTheHouse()
        {
            Contract();
            var go = new GameObject("ginny cottage (test)");
            try
            {
                go.transform.position = new Vector3(84f, 28f, 0f);
                var shell = go.AddComponent<SpriteRenderer>();
                int exterior = -1;
                for (int f = 0; f < 8 && exterior < 0; f++)
                    if (InteriorCatalog.InteriorFacingFor(f) == 2) exterior = f;
                if (exterior < 0) Assert.Ignore("no exterior facing gives the scene's interior facing 2");

                var occupant = new GameObject("occupant").transform;
                occupant.SetParent(go.transform, worldPositionStays: false);
                StandResult stood = BuildingInteriorStander.Stand(go, StPetersInteriors.GinnyCottagePlanKey,
                    InteriorFamily.VillageBuilding, exterior, null, StPetersInteriors.GinnyCottagePlanKey, shell);
                if (!stood.Enterable) Assert.Ignore("Ginny's room is not baked in this checkout");

                var interior = go.GetComponent<HiddenHarbours.World.BuildingInterior>();
                interior.SetOccupant(occupant);
                occupant.position = interior.transform.position;
                typeof(HiddenHarbours.World.BuildingInterior)
                    .GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(interior, null);
                Assert.IsTrue(interior.IsInside, "precondition: he is inside");
                Assert.IsTrue(interior.TryGoToLevel(1), "precondition: he can be sent upstairs");

                var upper = interior.FootprintFor(1);
                var solids = go.GetComponentsInChildren<PolygonCollider2D>(false)
                    .Where(c => c.enabled && !c.isTrigger)
                    .SelectMany(c => Enumerable.Range(0, c.pathCount).Select(k => c.GetPath(k)
                        .Select(v => (Vector2)c.transform.TransformPoint(v + c.offset)).ToArray()))
                    .Where(poly => poly.Length >= 3)
                    .ToList();
                bool Open(Vector2 plan)
                {
                    Vector2 w = upper.ModelToWorld(-plan);
                    foreach (Vector2[] poly in solids)
                    {
                        bool inside = false;
                        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                        {
                            Vector2 a = poly[i], b = poly[j];
                            if ((a.y > w.y) != (b.y > w.y) && w.x < (b.x - a.x) * (w.y - a.y) / (b.y - a.y) + a.x)
                                inside = !inside;
                            Vector2 ab = b - a;
                            float t = Mathf.Clamp01(Vector2.Dot(w - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f));
                            if ((a + ab * t - w).sqrMagnitude < Radius * Radius - 1e-6f) return false;
                        }
                        if (inside) return false;
                    }
                    return true;
                }

                const float S = 0.1f;
                float bx = HalfW + 1.0f, by = HalfL + 1.0f;
                var start = new Vector2(Mathf.Round((StairHead.x - Lift) / S) * S, Mathf.Round(StairHead.y / S) * S);
                Assert.IsTrue(Open(start), $"the stair head ({start}) stands in a live collider upstairs");
                var seen = new HashSet<(int, int)> { (Mathf.RoundToInt(start.x / S), Mathf.RoundToInt(start.y / S)) };
                var stack = new Stack<(int, int)>(seen);
                while (stack.Count > 0)
                {
                    var (x, y) = stack.Pop();
                    foreach (var n in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    {
                        Vector2 p = new Vector2(n.Item1 * S, n.Item2 * S);
                        if (Mathf.Abs(p.x) > bx || Mathf.Abs(p.y) > by || seen.Contains(n) || !Open(p)) continue;
                        seen.Add(n); stack.Push(n);
                    }
                }
                var reached = seen.Select(c => new Vector2(c.Item1 * S, c.Item2 * S)).ToList();
                Vector2 outside = reached.FirstOrDefault(p => Mathf.Abs(p.x) > InnerW + 0.01f || Mathf.Abs(p.y) > InnerL + 0.01f);
                Assert.IsFalse(reached.Any(p => Mathf.Abs(p.x) > InnerW + 0.01f || Mathf.Abs(p.y) > InnerL + 0.01f),
                               $"upstairs he walks out of the house (reached plan {outside}): the storey has no outer wall there");
                float split = StPetersInteriors.GinnyCorridorX0 - 0.3f;
                Assert.IsTrue(reached.Any(p => p.x < split && p.y > StPetersInteriors.GinnyPlayerDoorY0),
                              "his bedroom cannot be walked into from the stair head over the live colliders");
                Assert.IsTrue(reached.Any(p => p.x < split && p.y < StPetersInteriors.GinnyOwnerDoorY1),
                              "Ginny's bedroom cannot be walked into from the stair head over the live colliders");
                Assert.IsTrue(reached.Any(p => p.x < -InnerW + 1.0f),
                              "the south half upstairs, where both beds stand, is walled off");
            }
            finally
            {
                Object.DestroyImmediate(go);
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
