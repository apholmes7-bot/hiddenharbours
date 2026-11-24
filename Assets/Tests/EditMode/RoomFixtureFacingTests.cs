using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using Row = HiddenHarbours.App.Editor.RoomFixturePlan.Row;
using Wall = HiddenHarbours.App.Editor.RoomFixturePlan.Wall;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Every room's fixtures and chairs face the way the room says (owner 2026-11-23: "kitchen appliances
    /// appear to be oriented incorrectly based on where the walls are and where the bordering appliances are
    /// and where the player would interact from. Fix this everywhere."). Each piece has its back flush to a
    /// wall or partition, its art turned out of that wall on the storey it stands on, open floor in front of
    /// it that he can walk to from the door, and every chair faces its table.
    /// </summary>
    public class RoomFixtureFacingTests
    {
        const float Eps = 1e-3f;

        /// <summary>The most a piece's back may stand off the face of the wall it backs onto.</summary>
        const float MaxBackGap = 0.05f;

        /// <summary>His round footing, and the step the floor is walked at.</summary>
        const float Radius = 0.35f, Step = 0.05f;

        /// <summary>How far in front of a piece's face he stands to use it: just clear of it.</summary>
        const float StanceClearance = 0.05f;

        static readonly Dictionary<string, (float w, float l)> Declared = new Dictionary<string, (float, float)>
        {
            { "sageCottage", (RoomFixturePlan.SageW, RoomFixturePlan.SageL) },
            { "ginnyCottage", (RoomFixturePlan.GinnyW, RoomFixturePlan.GinnyL) },
            { "redSaltbox", (RoomFixturePlan.RedW, RoomFixturePlan.RedL) },
            { "whiteFarmhouse", (RoomFixturePlan.FarmW, RoomFixturePlan.FarmL) },
            { "harbourGable", (RoomFixturePlan.HarbourFrameW, RoomFixturePlan.HarbourFrameL) },
            { "harbourGableUpper", (RoomFixturePlan.HarbourFrameW, RoomFixturePlan.HarbourFrameL) },
            { "creamSaltbox", (RoomFixturePlan.CreamW, RoomFixturePlan.CreamL) },
            { "creamSaltboxUpper", (RoomFixturePlan.CreamW, RoomFixturePlan.CreamL) },
            { "whiteGable", (RoomFixturePlan.WhiteGableW, RoomFixturePlan.WhiteGableL) },
            { "whiteGableUpper", (RoomFixturePlan.WhiteGableW, RoomFixturePlan.WhiteGableL) },
            { "school", (RoomFixturePlan.SchoolW, RoomFixturePlan.SchoolL) },
        };

        static Rect RectOf(Vector2 centre, Vector2 size) =>
            new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, size.x, size.y);

        static Rect RectOf(StPetersInteriors.WallRect w) => Rect.MinMaxRect(w.X0, w.Y0, w.X1, w.Y1);

        static StPetersInteriors.UpperLevelPlan PlanOf(Row row) =>
            string.IsNullOrEmpty(row.PlanKey) ? default : StPetersInteriors.UpperLevelFor(row.PlanKey);

        /// <summary>The inner walls on the row's storey, in the plan frame.</summary>
        static IEnumerable<StPetersInteriors.WallRect> PartitionsOf(Row row) =>
            (row.Level == 0 ? PlanOf(row).GroundWalls : PlanOf(row).Partitions) ?? Enumerable.Empty<StPetersInteriors.WallRect>();

        static IEnumerable<StPetersInteriors.Furnishing> FurnitureFor(Row row)
        {
            var plan = PlanOf(row);
            if (row.Level == 0)
            {
                foreach (var f in StPetersInteriors.GroundFurnishingsFor(row.RoomKey, row.PlanKey)) yield return f;
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

        static InteriorKit.Contract Contract()
        {
            InteriorKit.Contract contract = InteriorKit.Load();
            if (contract?.props == null || contract.props.Length == 0)
                Assert.Ignore("no baked interior contract: nothing to read furniture footprints from");
            return contract;
        }

        /// <summary>The unit vector out of the named wall into the room, in the plan frame.</summary>
        static Vector2 IntoRoom(Wall wall) => wall switch
        {
            Wall.Left => Vector2.left,
            Wall.Right => Vector2.right,
            Wall.Front => Vector2.down,
            _ => Vector2.up,
        };

        /// <summary>How far the piece's back stands off the nearest wall face behind it: the outer wall's inner
        /// face, or a partition's face that spans the piece.</summary>
        static float BackGap(Row row)
        {
            (float w, float l) = Declared[row.RoomKey];
            float hw = w * 0.5f - StPetersInteriors.WallThicknessMetres, hl = l * 0.5f - StPetersInteriors.WallThicknessMetres;
            Rect r = RectOf(row.At, RoomFixturePlan.FootprintOf(row));
            float gap = row.Wall switch
            {
                Wall.Left => hw - r.xMax,
                Wall.Right => r.xMin + hw,
                Wall.Front => hl - r.yMax,
                _ => r.yMin + hl,
            };
            foreach (var p in PartitionsOf(row))
            {
                bool acrossY = p.Y0 < r.yMax - Eps && r.yMin < p.Y1 - Eps;
                bool acrossX = p.X0 < r.xMax - Eps && r.xMin < p.X1 - Eps;
                if (row.Wall == Wall.Left && acrossY && p.X0 >= r.xMax - Eps) gap = Mathf.Min(gap, p.X0 - r.xMax);
                if (row.Wall == Wall.Right && acrossY && p.X1 <= r.xMin + Eps) gap = Mathf.Min(gap, r.xMin - p.X1);
                if (row.Wall == Wall.Front && acrossX && p.Y0 >= r.yMax - Eps) gap = Mathf.Min(gap, p.Y0 - r.yMax);
                if (row.Wall == Wall.Back && acrossX && p.Y1 <= r.yMin + Eps) gap = Mathf.Min(gap, r.yMin - p.Y1);
            }
            return gap;
        }

        // ---- backs to the wall --------------------------------------------------------------------

        [Test]
        public void EveryPieceStandsWithItsBackFlushToItsWallOrAPartition()
        {
            var off = new List<string>();
            foreach (Row row in RoomFixturePlan.Rows)
            {
                float gap = BackGap(row);
                if (gap < -Eps || gap > MaxBackGap) off.Add($"{row.Id} ({row.Wall}): {gap:0.000} m off its wall");
            }
            CollectionAssert.IsEmpty(off, "a piece stands out from (or into) the wall it backs onto");
        }

        // ---- the art turns out of the wall on its own storey -------------------------------------

        /// <summary>
        /// The plan's wall, as the placed piece records it, is the wall its ground-floor geometry backs onto.
        /// Before 2026-11-23 the art picked its wall itself, and a ground-floor piece by the back wall stood
        /// inside the upper storey's on-screen rectangle (drawn 2.5 up the screen): it was read in that frame
        /// and turned to the wrong wall. A piece backed onto a partition names it; the geometry pick sees only
        /// the outer walls, so those are held to the plan's turn instead.
        /// </summary>
        [Test]
        public void EachPlacedPieceRecordsTheWallItsOwnStoreyBacksItOnto()
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText(RoomFixturePlan.Scene).Replace("\r\n", "\n"));
            var rows = RoomFixturePlan.Rows.Where(r => r.Placed).ToDictionary(r => r.Id);
            int checkedPieces = 0;
            foreach (InteriorFixturePlacement.Site site in InteriorFixturePlacement.Sites.Where(s => s.IsInterior && s.Wall >= 0))
            {
                InteriorFootprint fp = InteriorFixturePlacement.FootprintOf(scene, site, out _, out _);
                int recorded = InteriorFixturePlacement.ModelWall(fp, site);
                int turn = site.PlanFrame && fp.DoorSign < 0f ? 4 : 0;
                Assert.AreEqual((site.Wall + turn) % 8, recorded, $"{site.Name}: the plan's wall, half turned for a door at -y");

                bool onPartition = site.InteractId != null && rows.TryGetValue(site.InteractId, out Row row) &&
                                   PartitionsOf(row).Any() && BackGapToOuterWall(row) > MaxBackGap;
                if (onPartition) continue;
                var art = AssetDatabase.LoadAssetAtPath<GameObject>(site.Prefab)?.GetComponentInChildren<ApplianceArt>(true);
                Assert.IsNotNull(art, $"{site.Prefab} carries no ApplianceArt");
                int picked = ApplianceFacing.PickWallOffset(InteriorFixturePlacement.ModelPoint(fp, site), fp.WidthMetres,
                                                            fp.LengthMetres, ApplianceSheets.For(art.Kind).Foot);
                Assert.AreEqual(picked, recorded, $"{site.Name}: the wall its own storey backs it onto");
                checkedPieces++;
            }
            Assert.Greater(checkedPieces, 0, "no placed piece backs onto an outer wall");
        }

        static float BackGapToOuterWall(Row row)
        {
            (float w, float l) = Declared[row.RoomKey];
            float hw = w * 0.5f - StPetersInteriors.WallThicknessMetres, hl = l * 0.5f - StPetersInteriors.WallThicknessMetres;
            Rect r = RectOf(row.At, RoomFixturePlan.FootprintOf(row));
            return row.Wall switch
            {
                Wall.Left => hw - r.xMax,
                Wall.Right => r.xMin + hw,
                Wall.Front => hl - r.yMax,
                _ => r.yMin + hl,
            };
        }

        [Test]
        public void AGroundPieceReadsTheGroundFloor_EvenWhereTheStoreyAboveIsDrawnOverIt()
        {
            var spawned = new List<GameObject>();
            try
            {
                var buildingGo = new GameObject("cottage"); spawned.Add(buildingGo);
                var shell = new GameObject("Shell").AddComponent<SpriteRenderer>();
                var ground = new GameObject("Interior").AddComponent<SpriteRenderer>();
                var upper = new GameObject("InteriorUpper").AddComponent<SpriteRenderer>();
                var groundProps = new GameObject("Furniture");
                var upperProps = new GameObject("FurnitureUpper");
                var upperWalls = new GameObject("WallsUpper");
                foreach (var t in new[] { shell.transform, ground.transform, upper.transform,
                                          groundProps.transform, upperProps.transform, upperWalls.transform })
                    t.SetParent(buildingGo.transform, worldPositionStays: false);
                var interior = buildingGo.AddComponent<BuildingInterior>();
                interior.Configure(shell, ground, groundProps.transform, RoomFixturePlan.GinnyW, RoomFixturePlan.GinnyL,
                                   facing: 6, facings: 8, groundDepthScale: HiddenHarbours.Core.IsoGround.GroundDepthScale,
                                   wallThicknessMetres: StPetersInteriors.WallThicknessMetres,
                                   doorwayWidthMetres: StPetersInteriors.DoorwayWidthMetres);
                interior.ConfigureUpperLevel(upper, upperProps.transform, upperWalls.transform,
                                             InteriorLevelLayout.UpperLevelY(3.1025f));

                InteriorFootprint groundFp = interior.FootprintFor(0), upperFp = interior.FootprintFor(1);
                // A point by the ground floor's back half, which the storey above is drawn over on screen.
                // Found by walking the ground floor on a 0.25 m grid.
                Vector2 world = default;
                bool found = false;
                float ghw = groundFp.WidthMetres * 0.5f - 0.3f, ghl = groundFp.LengthMetres * 0.5f - 0.3f;
                for (float y = -ghl; y <= ghl && !found; y += 0.25f)
                for (float x = -ghw; x <= ghw && !found; x += 0.25f)
                {
                    Vector2 w = groundFp.ModelToWorld(new Vector2(x, y));
                    if (groundFp.Contains(w, 0f) && upperFp.Contains(w, 0f)) { world = w; found = true; }
                }
                Assume.That(found, "the storey above must be drawn over part of the ground floor");

                var piece = new GameObject("Stove").transform;
                piece.SetParent(groundProps.transform, false);
                piece.position = world;
                Assert.AreEqual(groundFp.ModelToWorld(Vector2.zero), ApplianceArt.FootprintHolding(interior, piece).ModelToWorld(Vector2.zero),
                                "a piece among the ground floor's furniture is read in the ground floor's frame");

                piece.SetParent(upperProps.transform, true);
                Assert.AreEqual(upperFp.ModelToWorld(Vector2.zero), ApplianceArt.FootprintHolding(interior, piece).ModelToWorld(Vector2.zero),
                                "a piece among the upper storey's furniture is read in the upper storey's frame");

                piece.SetParent(buildingGo.transform, true);
                Assert.AreEqual(upperFp.ModelToWorld(Vector2.zero), ApplianceArt.FootprintHolding(interior, piece).ModelToWorld(Vector2.zero),
                                "a piece under neither falls back to the storey it is drawn in");
            }
            finally { foreach (var o in spawned) if (o != null) Object.DestroyImmediate(o); }
        }

        // ---- open floor in front, walked to from the door ----------------------------------------

        static bool FreeCircle(Vector2 c, float hw, float hl, List<Rect> obs)
        {
            // he may stand with his footing against a wall's face (a toilet in a corner leaves exactly that)
            const float touch = 0.01f;
            if (c.x - Radius < -hw - touch || c.x + Radius > hw + touch || c.y - Radius < -hl - touch || c.y + Radius > hl + touch)
                return false;
            foreach (Rect r in obs)
            {
                float cx = Mathf.Clamp(c.x, r.xMin, r.xMax), cy = Mathf.Clamp(c.y, r.yMin, r.yMax);
                float dx = c.x - cx, dy = c.y - cy;
                if (dx * dx + dy * dy < Radius * Radius - 1e-6f) return false;
            }
            return true;
        }

        /// <summary>
        /// In front of every piece with a verb there is a clear spot he can stand on, and he can walk to it from
        /// the room's door (or, upstairs, from the head of the stairs). Walked with a round footing of his
        /// radius in the plan frame and the room's planned furniture, pieces and inner walls; a planned bathroom
        /// closet's own walls are not built yet and are not walls here.
        /// </summary>
        [Test]
        public void InFrontOfEveryPieceIsOpenFloor_WalkedToFromTheDoor()
        {
            InteriorKit.Contract contract = Contract();
            var fails = new List<string>();
            foreach (var group in RoomFixturePlan.Rows.GroupBy(r => (r.BuildingKey, r.Level)))
            {
                Row first = group.First();
                (float w, float l) = Declared[first.RoomKey];
                float hw = w * 0.5f - StPetersInteriors.WallThicknessMetres, hl = l * 0.5f - StPetersInteriors.WallThicknessMetres;
                var furniture = FurnitureFor(first).ToList();
                var obs = PartitionsOf(first).Select(RectOf).ToList();
                obs.AddRange(furniture.Select(f => RectOf(f.RoomMetres, FurnitureSize(contract, f))));
                obs.AddRange(group.Select(r => RectOf(r.At, RoomFixturePlan.FootprintOf(r))));

                Vector2 start;
                if (first.Level == 0)
                {
                    InteriorCatalog.Placement baked = InteriorCatalog.FindRoom(first.RoomKey);
                    float doorEnd = baked.IsValid && InteriorCatalog.DoorModelMetres(baked).y < 0f ? -1f : 1f;
                    start = new Vector2(0f, doorEnd * (hl - Radius - Step));
                }
                else
                {
                    var stair = furniture.FirstOrDefault(f => f.Fixture == StPetersInteriors.InteriorFixture.StairDown);
                    start = stair.PropKey != null ? stair.RoomMetres : Vector2.zero;
                }
                var walk = Flood(start, hw, hl, obs);
                if (walk.Count == 0) { fails.Add($"{first.BuildingKey} level {first.Level}: no free floor at the way in {start}"); continue; }

                foreach (Row row in group.Where(r => RoomFixturePlan.HasInteractId(r.Kind)))
                {
                    Vector2 size = RoomFixturePlan.FootprintOf(row);
                    Vector2 n = IntoRoom(row.Wall);
                    float half = Mathf.Abs(n.x) > 0f ? size.x * 0.5f : size.y * 0.5f;
                    Vector2 stance = row.At + n * (half + Radius + StanceClearance);
                    var cell = (Mathf.RoundToInt(stance.x / Step), Mathf.RoundToInt(stance.y / Step));
                    if (!FreeCircle(stance, hw, hl, obs)) fails.Add($"{row.Id}: the floor in front of it at {stance} is not clear");
                    // the walk is on a 0.05 m grid: the stance is reached if a walked cell is next to it
                    else if (!Enumerable.Range(-1, 3).Any(dx => Enumerable.Range(-1, 3).Any(dy => walk.Contains((cell.Item1 + dx, cell.Item2 + dy)))))
                        fails.Add($"{row.Id}: the floor in front of it at {stance} is not walked to from the way in");
                }
            }
            CollectionAssert.IsEmpty(fails);
        }

        static HashSet<(int, int)> Flood(Vector2 start, float hw, float hl, List<Rect> obs)
        {
            var seen = new HashSet<(int, int)>();
            var c0 = (Mathf.RoundToInt(start.x / Step), Mathf.RoundToInt(start.y / Step));
            // the way in may sit on a stair's own footprint: start from the nearest free cell to it
            if (!FreeCircle(new Vector2(c0.Item1 * Step, c0.Item2 * Step), hw, hl, obs))
            {
                bool found = false;
                for (int ring = 1; ring <= 40 && !found; ring++)
                for (int dx = -ring; dx <= ring && !found; dx++)
                for (int dy = -ring; dy <= ring && !found; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;
                    var c = (c0.Item1 + dx, c0.Item2 + dy);
                    if (FreeCircle(new Vector2(c.Item1 * Step, c.Item2 * Step), hw, hl, obs)) { c0 = c; found = true; }
                }
                if (!found) return seen;
            }
            var stack = new Stack<(int, int)>();
            seen.Add(c0); stack.Push(c0);
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                foreach (var nb in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    if (!seen.Contains(nb) && FreeCircle(new Vector2(nb.Item1 * Step, nb.Item2 * Step), hw, hl, obs))
                    { seen.Add(nb); stack.Push(nb); }
            }
            return seen;
        }

        // ---- chairs face their table ---------------------------------------------------------------

        /// <summary>Every furnishing list a room stands from: each room's own, Ginny's ground floor, and every
        /// declared storey above with its ground additions.</summary>
        static IEnumerable<(string where, IReadOnlyList<StPetersInteriors.Furnishing> list)> FurnishingLists()
        {
            foreach (var room in InteriorKit.RoomSet)
                yield return (room.Key, StPetersInteriors.FurnishingsFor(room.Key));
            foreach (string plan in RoomFixturePlan.Rows.Select(r => r.PlanKey).Append(StPetersInteriors.GinnyCottagePlanKey)
                                                   .Where(p => !string.IsNullOrEmpty(p)).Distinct())
            {
                var upper = StPetersInteriors.UpperLevelFor(plan);
                if (upper.Furnishings != null) yield return (plan + " upstairs", upper.Furnishings);
                string room = RoomFixturePlan.Rows.FirstOrDefault(r => r.PlanKey == plan).RoomKey ?? StPetersGinnyPlot.CottageKey;
                yield return (plan + " ground", StPetersInteriors.GroundFurnishingsFor(room, plan));
            }
        }

        [Test]
        public void EveryChairFacesItsTable_InEveryRoomsPlan()
        {
            int chairs = 0;
            foreach (var (where, list) in FurnishingLists())
            {
                var tables = list.Where(f => f.PropKey == "table").ToList();
                if (tables.Count == 0) continue;
                foreach (var chair in list.Where(f => f.PropKey == "chair"))
                {
                    Vector2 table = tables.OrderBy(t => (t.RoomMetres - chair.RoomMetres).sqrMagnitude).First().RoomMetres;
                    Assert.AreEqual(StPetersInteriors.ChairFacingOffset(chair.RoomMetres, table), chair.FacingOffset,
                                    $"{where}: the chair at {chair.RoomMetres} does not face the table at {table}");
                    chairs++;
                }
            }
            Assert.Greater(chairs, 0, "no chair stands at a table anywhere");
        }

        /// <summary>
        /// The same held to the committed scene, from where each chair and table actually stand (a chair's
        /// name records the point it was stood at in an older frame; its transform is the truth): each chair's
        /// sprite is the chair drawn at the room's facing turned by the side of the nearest table it stands on, read in the plan's frame.
        /// </summary>
        [Test]
        public void EveryChairInTheSceneIsDrawnFacingItsTable()
        {
            string text = File.ReadAllText(RoomFixturePlan.Scene).Replace("\r\n", "\n");
            SceneYaml scene = SceneYaml.Parse(text);
            InteriorCatalog.Placement chairProp = InteriorCatalog.FindProp("chair");
            Assume.That(chairProp.IsValid, "no chair prop in the interior contract");
            int chairs = 0;
            foreach (Doc bi in scene.Docs.Where(d => d.IsScript("HiddenHarbours.World.BuildingInterior")))
            {
                long propsId = bi.FieldRef("_props"), roomId = bi.FieldRef("_room");
                if (propsId == 0 || roomId == 0) continue;
                Doc roomSr = scene.Get(roomId);
                if (roomSr == null) continue;
                Sprite roomSprite = SpriteOf(ObjRef.Parse(roomSr.Field("m_Sprite")));
                Match m = roomSprite != null ? Regex.Match(roomSprite.name, @"_d(\d+)$") : Match.Empty;
                if (!m.Success) continue;
                int facing = int.Parse(m.Groups[1].Value), facings = int.Parse(bi.Field("_facings"));

                Doc buildingTr = scene.TransformOf(scene.Require(bi.FieldRef("m_GameObject"), "building"));
                var fp = new InteriorFootprint(InteriorFixturePlacement.WorldOf(scene, buildingTr),
                    float.Parse(bi.Field("_widthMetres"), System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(bi.Field("_lengthMetres"), System.Globalization.CultureInfo.InvariantCulture),
                    int.Parse(bi.Field("_facing")), facings,
                    float.Parse(bi.Field("_groundDepthScale"), System.Globalization.CultureInfo.InvariantCulture),
                    bi.Field("_doorOnPlusY") == "1" ? 1f : -1f,
                    float.Parse(bi.Field("_doorAcrossMetres"), System.Globalization.CultureInfo.InvariantCulture));

                var items = new List<(string label, Vector2 model, Doc go)>();
                foreach (long c in scene.Require(propsId, "furniture root").FieldRefs("m_Children"))
                {
                    Doc tr = scene.Require(c, "child");
                    if (tr.Stripped) continue;
                    Doc go = scene.Require(tr.FieldRef("m_GameObject"), "child go");
                    string name = go.Field("m_Name");
                    string label = name.Contains(" (") ? name.Substring(0, name.IndexOf(" (")) : name;
                    items.Add((label, fp.WorldToModel(InteriorFixturePlacement.WorldOf(scene, tr)), go));
                }
                var tables = items.Where(i => i.label == "Table").ToList();
                if (tables.Count == 0) continue;
                foreach (var chair in items.Where(i => i.label == "Chair"))
                {
                    Vector2 table = tables.OrderBy(t => (t.model - chair.model).sqrMagnitude).First().model;
                    // The room's sheet is drawn in the plan's frame (door at +y): a room whose door is in its model -y wall
                    // is that frame half turned, so the side of the table is read with the point turned back.
                    int cell = ((facing + StPetersInteriors.ChairFacingOffset(chair.model * fp.DoorSign, table * fp.DoorSign)) % facings + facings) % facings;
                    Sprite expected = InteriorCatalog.LoadFacing(chairProp, cell);
                    Assert.IsNotNull(expected, $"no chair sprite at facing {cell}");
                    Doc sr = scene.ComponentsOf(chair.go).Single(d => d.ClassId == 212);
                    Sprite actual = SpriteOf(ObjRef.Parse(sr.Field("m_Sprite")));
                    Assert.AreEqual(expected, actual,
                        $"{scene.Require(scene.TransformOf(scene.Require(bi.FieldRef("m_GameObject"), "b")).FieldRef("m_GameObject"), "b").Field("m_Name")}" +
                        $" '{chair.go.Field("m_Name")}' at model {chair.model} is not drawn facing the table at {table}");
                    chairs++;
                }
            }
            Assert.Greater(chairs, 0, "no chair stands at a table in the scene");
        }

        static Sprite SpriteOf(ObjRef r)
        {
            if (string.IsNullOrEmpty(r.Guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(r.Guid);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s =>
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string _, out long id) && id == r.FileId);
        }

        // ---- Ginny's table on the kitchen's south wall ---------------------------------------------

        [Test]
        public void GinnysTableStandsOnTheKitchensSouthWall_ItsChairsOnTheRoomSide()
        {
            InteriorKit.Contract contract = Contract();
            var ground = StPetersInteriors.GroundFurnishingsFor(StPetersGinnyPlot.CottageKey, StPetersInteriors.GinnyCottagePlanKey);
            var table = ground.Single(f => f.PropKey == "table");
            var chairs = ground.Where(f => f.PropKey == "chair").ToList();
            var closet = RoomFixturePlan.Closets.Single(c => c.BuildingKey == "ginny_cottage");
            // The kitchen is the back half's north part: plan x above the partition (+x is north), y below the
            // living room's back partition. Its south wall is the kitchen/washroom partition's north face.
            float partitionNorthFace = StPetersInteriors.UpperLevelFor(StPetersInteriors.GinnyCottagePlanKey).GroundWalls
                .Where(w => w.Y0 < closet.Area.yMax - Eps && w.X0 >= closet.Area.xMax - Eps)
                .Select(w => w.X1).DefaultIfEmpty(float.NaN).Min();
            Assert.IsFalse(float.IsNaN(partitionNorthFace), "no kitchen/washroom partition");
            Rect t = RectOf(table.RoomMetres, FurnitureSize(contract, table));
            Assert.AreEqual(partitionNorthFace, t.xMin, MaxBackGap, "the table stands against the kitchen's south wall");
            Assert.Less(t.yMax, closet.Area.yMax, "the table stands in the kitchen, behind the living room's partition");
            Assert.AreEqual(2, chairs.Count);
            foreach (var c in chairs)
            {
                Assert.Greater(c.RoomMetres.x, t.xMax, "a chair stands on the room side of the table, not in the wall");
                Assert.AreEqual(StPetersInteriors.ChairSidePlusX, c.FacingOffset, "the chair faces the table across it");
            }
        }
    }
}
