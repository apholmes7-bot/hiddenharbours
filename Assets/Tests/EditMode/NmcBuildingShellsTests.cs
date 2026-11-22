using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Nine Mile Creek's new building shells (the fish plant, the community hall with its gym, the boat gear and
    /// the fishing gear wholesalers), as the committed
    /// scene carries them: the steps over the committed scene change nothing (so the scene is what the steps write,
    /// and a second run is a no-op), each footprint stands clear of every road, every other placed thing and every
    /// plant, and each prefab is a working seamless room (ADR 0036) whose doorway is the one way in.
    /// </summary>
    public class NmcBuildingShellsTests
    {
        const string ScenePath = "_Project/Scenes/NineMileCreek.unity";

        /// <summary>The least a carriageway's edge may come to a building's wall, metres.</summary>
        const float RoadMarginMetres = 2f;

        /// <summary>The most ground between the end of a building's walk and its door, metres.</summary>
        const float MaxForecourtMetres = 3f;

        static SceneYaml _scene;
        static SceneYaml Scene => _scene ??= SceneYaml.Parse(File.ReadAllText(Path.Combine(Application.dataPath, ScenePath)).Replace("\r", ""));

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        static IEnumerable<NmcBuildingShells.Shell> Shells => NmcBuildingShells.All;

        [Test]
        public void TheStepsOverTheCommittedSceneChangeNothing()
        {
            var cache = new Dictionary<string, NmcBuildingShells.PrefabFacts>();
            List<LayerPatch> patches = NmcBuildingShells.PlanAll(Scene,
                p => cache.TryGetValue(p, out var f) ? f : cache[p] = NmcBuildingShells.PrefabFacts.Read(ProjectRoot, p), out _);
            foreach (LayerPatch p in patches)
                Assert.That(p.IsEmpty, Is.True, $"step {p.Step} on {p.Root} is not what the scene carries:\n" + p.Summary());
        }

        [Test]
        public void EachShellStandsOnceUnderTheBuildingsRoot([ValueSource(nameof(Names))] string name)
        {
            Doc root = Scene.RootNamed(NmcBuildingShells.RootName);
            List<long> children = Scene.TransformOf(root).FieldRefs("m_Children");
            int count = children.Select(c => Scene.Require(c, "child")).Where(t => t.Stripped)
                .Select(t => Scene.Require(t.FieldRef("m_PrefabInstance"), "instance"))
                .Count(pi => pi.Text.Contains("      propertyPath: m_Name\n      value: " + name + "\n"));
            Assert.That(count, Is.EqualTo(1), $"{name} under {NmcBuildingShells.RootName}");
        }

        static IEnumerable<string> Names() => NmcBuildingShells.All.Select(s => s.Name);

        [Test]
        public void EachCounterNamesASellerThatExists()
        {
            var sellers = new HashSet<string>(Resources.LoadAll<HiddenHarbours.Economy.SellerDef>(
                HiddenHarbours.Economy.SellerDirectory.ResourceFolder).Select(d => d.Id));
            foreach (var s in Shells.Where(x => !string.IsNullOrEmpty(x.SellerId)))
                Assert.That(sellers.Contains(s.SellerId), Is.True, $"{s.Name} names {s.SellerId}, which no SellerDef carries");
            Assert.That(NmcBuildingShells.BoatGearWholesaler.SellerId, Is.Not.Empty);
            Assert.That(NmcBuildingShells.FishingGearWholesaler.SellerId, Is.Not.Empty);
        }

        [Test]
        public void NoCarriagewayComesWithinTheMarginOfAWall()
        {
            NineMileCreekRoadNetwork.Network net = NineMileCreekRoadNetwork.Load();
            foreach (var s in Shells)
            {
                Rect wall = NmcBuildingShells.WorldRect(s);
                foreach (var r in net.Routes.Concat(net.Flares)
                             .Where(r => r.IsCorridor || r.On != null || System.Array.IndexOf(NineMileCreekRoadNetwork.CorridorClasses, r.Class) >= 0))
                    Assert.That(r.EdgeDistanceTo(wall), Is.GreaterThanOrEqualTo(RoadMarginMetres), $"{r} against {s.Name}");
            }
        }

        [Test]
        public void NoWalkOrPathCrossesAFootprintExceptTheOneThatEndsAtItsDoor()
        {
            NineMileCreekRoadNetwork.Network net = NineMileCreekRoadNetwork.Load();
            foreach (var s in Shells)
            {
                Rect wall = NmcBuildingShells.WorldRect(s);
                Vector2 door = NmcBuildingShells.DoorWorld(s);
                foreach (var r in net.Routes.Concat(net.Flares))
                {
                    if (r.EdgeDistanceTo(wall) >= 0f) continue;
                    // a walk may meet the wall only where it arrives at the doorway
                    float toDoor = Mathf.Min(Vector2.Distance(r.Points[0], door), Vector2.Distance(r.Points[r.Points.Length - 1], door));
                    Assert.That(toDoor, Is.LessThanOrEqualTo(r.HalfWidth + 0.5f), $"{r} runs into {s.Name} away from its door");
                }
            }
        }

        [Test]
        public void TheHallStandsInsideTheParishLotsRailWithItsDoorFacingTheGate()
        {
            Yard yard = NineMileCreekYards.Require(NineMileCreekYards.ParishHallYard);
            InteriorFootprint fp = NmcBuildingShells.FootprintOf(NmcBuildingShells.CommunityHall);
            foreach (Vector2 c in fp.Corners())
                Assert.That(yard.Contains(c), Is.True, $"the hall's corner {c} is outside the parish yard");
            // the lot's only way in is its gate: the door stands a short forecourt inside it, square on
            Assert.That(yard.Gates, Is.Not.Empty, "the parish yard has no gate");
            Vector2 gate = yard.Gates.OrderBy(g => Vector2.Distance(g, fp.DoorWorld)).First();
            Assert.That(Vector2.Distance(gate, fp.DoorWorld), Is.LessThanOrEqualTo(MaxForecourtMetres), $"the door {fp.DoorWorld} is far from the gate {gate}");
            Vector2 outward = (fp.DoorWorld - fp.Centre).normalized;
            Assert.That(Vector2.Dot((gate - fp.DoorWorld).normalized, outward), Is.GreaterThan(0.95f), "the door does not face the gate");
        }

        [Test]
        public void NothingElsePlacedStandsInsideAFootprint()
        {
            // Every scene object reachable through plain (unturned, unscaled) parents, at its world point. The
            // buildings' own instances and the meadow root (a single object at the origin) are not "placed things".
            var offenders = new List<string>();
            foreach (Doc tr in Scene.Docs.Where(d => d.ClassId == 4 && !d.Stripped))
            {
                if (!TryWorld(tr, out Vector2 at, out string top)) continue;
                if (top == NmcBuildingShells.RootName) continue;
                // A yard's boundary stands at the yard's centre and draws its rail round the edge; the rail is the
                // yard's polygon, which the hall test holds the hall inside.
                if (top == "Yards" && Lineage(tr).Contains("Boundary")) continue;
                foreach (var s in Shells)
                    if (NmcBuildingShells.WorldRect(s).Contains(at) && !BelongsInside(top, s))
                        offenders.Add($"{Scene.Require(tr.FieldRef("m_GameObject"), "go").Field("m_Name")} (under {top}) at {at} in {s.Name}");
            }
            Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
        }

        /// <summary>A wholesaler's counter and keeper are placed inside their own shell on purpose
        /// (<c>NmcWholesalerKeepers.Place</c>); inside any other shell they are still offenders.</summary>
        static bool BelongsInside(string top, NmcBuildingShells.Shell s) =>
            NmcWholesalerKeepers.All.Any(k => k.Shell.Name == s.Name && (top == k.CounterRoot || top == k.KeeperRoot));

        [Test]
        public void ThePrefabsAreWorkingRooms([ValueSource(nameof(Names))] string name)
        {
            var s = NmcBuildingShells.All.Single(x => x.Name == name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(s.PrefabPath);
            Assert.That(prefab, Is.Not.Null, s.PrefabPath);
            var room = prefab.GetComponent<BuildingInterior>();
            Assert.That(room, Is.Not.Null, "no BuildingInterior on the root");
            Assert.That(room.Footprint.WidthMetres, Is.EqualTo(s.WidthMetres));
            Assert.That(room.Footprint.LengthMetres, Is.EqualTo(s.LengthMetres));
            Assert.That(room.Footprint.DoorSign, Is.EqualTo(s.DoorOnPlusY ? 1f : -1f));
            Assert.That(room.Footprint.DoorAcrossMetres, Is.EqualTo(s.DoorAcrossMetres));
            Assert.That(prefab.transform.Find(BuildingInteriorStander.RoomChildName), Is.Not.Null);
            Assert.That(prefab.transform.Find(BuildingInteriorStander.PropsChildName), Is.Not.Null);
            Transform walls = prefab.transform.Find(BuildingInteriorStander.WallsChildName);
            Assert.That(walls, Is.Not.Null, "no walls");

            // The doorway is open and the walls are shut: a point just inside the door is free, the same point
            // shifted along the wall past the doorway is in a wall.
            Assert.That(room.Footprint.Facing, Is.EqualTo(s.Facing));
            InteriorFootprint fp = NmcBuildingShells.FootprintAt(s, Vector2.zero);
            var colliders = walls.GetComponents<PolygonCollider2D>();
            float edge = (s.DoorOnPlusY ? 1f : -1f) * (s.LengthMetres * 0.5f - BuildingInteriorStander.WallThicknessMetres * 0.5f);
            Vector2 doorway = fp.ModelToWorld(new Vector2(s.DoorAcrossMetres, edge));
            Vector2 beside = fp.ModelToWorld(new Vector2(s.DoorAcrossMetres + BuildingInteriorStander.DoorwayWidthMetres * 1.5f, edge));
            Assert.That(colliders.Any(c => Inside(c, doorway)), Is.False, "the doorway is walled");
            Assert.That(colliders.Any(c => Inside(c, beside)), Is.True, "the wall beside the doorway is open");
        }

        // ---- the wholesalers' working fixtures (kitchens and bathrooms plan, N1a) ---------------------------

        static readonly NmcBuildingShells.Shell[] Wholesalers =
            { NmcBuildingShells.BoatGearWholesaler, NmcBuildingShells.FishingGearWholesaler };

        /// <summary>The player as the game builds him (PersistentCoreBuilder): a foot circle of radius 0.35 m,
        /// 0.7 m below the transform that interaction measures reach from.</summary>
        const float FootRadiusMetres = 0.35f;
        const float FootBelowMetres = 0.7f;

        /// <summary>The walk grid's cell, metres: fine against the 1.4 m doorways and the gaps in the partitions.</summary>
        const float CellMetres = 0.05f;

        [Test]
        public void EachWholesalerStandsAToiletABasinAndAMicrowave_WithIdsOfItsOwn()
        {
            var ids = new List<string>();
            foreach (var s in Wholesalers)
            {
                Assert.That(s.Fixtures.Select(f => f.Kind), Is.EquivalentTo(new[]
                    { RoomFixturePlan.Kind.Toilet, RoomFixturePlan.Kind.BathroomSink, RoomFixturePlan.Kind.Microwave }), s.Name);
                ids.AddRange(s.Fixtures.Select(f => NmcBuildingShells.FixtureId(s, f)));
            }
            Assert.That(ids.Count, Is.EqualTo(6));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), string.Join(", ", ids));
            foreach (string id in ids)
                Assert.That(id, Does.Match(@"^fixture\.nmc\.[a-z]+(_[a-z]+)*\.(toilet|bathroom_sink|microwave)$"), id);
            Assert.That(ids, Does.Contain("fixture.nmc.boat_gear_wholesaler.toilet"));
            Assert.That(ids, Does.Contain("fixture.nmc.fishing_gear_wholesaler.bathroom_sink"));
            foreach (var s in NmcBuildingShells.All.Except(Wholesalers))
                Assert.That(s.Fixtures, Is.Empty, $"{s.Name} stands no fixtures in this slice");
        }

        [Test]
        public void ThePrefabsCarryTheFixturesBoundAndInPlace_AndNoMarkerIsLeft([ValueSource(nameof(WholesalerNames))] string name)
        {
            var s = Wholesalers.Single(x => x.Name == name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(s.PrefabPath);
            Transform props = prefab.transform.Find(BuildingInteriorStander.PropsChildName);
            InteriorFootprint fp = NmcBuildingShells.FootprintAt(s, Vector2.zero);
            foreach (string marker in NmcBuildingShells.RetiredMarkers)
                Assert.That(props.Cast<Transform>().Any(t => t.name == marker && !PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)),
                            Is.False, $"{name} still carries the greybox marker {marker}");
            foreach (NmcBuildingShells.Fixture f in s.Fixtures)
            {
                string id = NmcBuildingShells.FixtureId(s, f);
                Transform t = props.Find(f.Name);
                Assert.That(t, Is.Not.Null, $"{name} has no {f.Name}");
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject),
                            Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(f.Prefab)), $"{name}/{f.Name} is not {f.Prefab}");
                Vector2 want = fp.ModelToWorld(f.At);
                Assert.That(Vector2.Distance((Vector2)t.position - (Vector2)prefab.transform.position, want), Is.LessThan(1e-3f),
                            $"{name}/{f.Name} stands off its model point {f.At}");
                Assert.That(t.GetComponent<ApplianceArt>(), Is.Not.Null, $"{name}/{f.Name} has no art");
                switch (f.Kind)
                {
                    case RoomFixturePlan.Kind.Toilet:
                        var seat = t.GetComponent<SitPoint>();
                        Assert.That(seat, Is.Not.Null, f.Name);
                        Assert.That(seat.Id, Is.EqualTo(id));
                        Assert.That(seat.StationId, Is.EqualTo(RoomFixturePlan.DefId(f.Kind)));
                        break;
                    case RoomFixturePlan.Kind.BathroomSink:
                        var wash = t.GetComponent<WashStationPoint>();
                        Assert.That(wash, Is.Not.Null, f.Name);
                        Assert.That(wash.Id, Is.EqualTo(id));
                        Assert.That(wash.StationId, Is.EqualTo(RoomFixturePlan.DefId(f.Kind)));
                        break;
                    default:
                        var cook = t.GetComponent<HiddenHarbours.Player.CookingStationFixture>();
                        Assert.That(cook, Is.Not.Null, f.Name);
                        Assert.That(cook.Id, Is.EqualTo(id));
                        Assert.That(cook.Def, Is.Not.Null, $"{name}/{f.Name} has no station def");
                        Assert.That(cook.Def.Id, Is.EqualTo(RoomFixturePlan.DefId(f.Kind)));
                        break;
                }
            }
        }

        static IEnumerable<string> WholesalerNames() => Wholesalers.Select(s => s.Name);

        [Test]
        public void EachFixtureBacksOntoTheNorthOrEastWall_AndStandsClearOfEverySolidBlock()
        {
            foreach (var s in Wholesalers)
            {
                Assert.That(s.Facing, Is.EqualTo(0), $"{s.Name}: the wall turns below assume +y is north");
                float hw = s.WidthMetres * 0.5f - BuildingInteriorStander.WallThicknessMetres;
                float hl = s.LengthMetres * 0.5f - BuildingInteriorStander.WallThicknessMetres;
                foreach (NmcBuildingShells.Fixture f in s.Fixtures)
                {
                    var art = AssetDatabase.LoadAssetAtPath<GameObject>(f.Prefab).GetComponent<ApplianceArt>();
                    Vector2 foot = ApplianceSheets.For(art.Kind).Foot;
                    int wall = ApplianceFacing.PickWallOffset(f.At, s.WidthMetres, s.LengthMetres, foot);
                    Assert.That(wall, Is.EqualTo(0).Or.EqualTo(2), $"{s.Name}/{f.Name} backs onto wall turn {wall}; only north (0) or east (2)");
                    // its footprint, square on to that wall, inside the room and clear of every solid block
                    Vector2 size = wall == 0 ? foot : new Vector2(foot.y, foot.x);
                    Rect r = new Rect(f.At - size * 0.5f, size);
                    Assert.That(r.xMin >= -hw - 1e-3f && r.xMax <= hw + 1e-3f && r.yMin >= -hl - 1e-3f && r.yMax <= hl + 1e-3f, Is.True,
                                $"{s.Name}/{f.Name}'s footprint {r} runs into the outer wall");
                    foreach (NmcBuildingShells.Block b in NmcBuildingShells.Layout(s).Where(b => b.Solid))
                        Assert.That(b.Model.Overlaps(r), Is.False, $"{s.Name}/{f.Name}'s footprint {r} runs into {b.Name}");
                }
            }
        }

        /// <summary>The fixtures read as inside the room: no greybox quad that draws in front of a fixture (sorted
        /// at a foot south of the fixture's) covers the fixture's floor footprint on screen. A full-height inner
        /// wall did (its top rose past the whole room behind it), and the staff corner read as outdoors.</summary>
        [Test]
        public void NoQuadDrawnInFrontOfAFixtureCoversIt([ValueSource(nameof(WholesalerNames))] string name)
        {
            var s = Wholesalers.Single(x => x.Name == name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(s.PrefabPath);
            Vector2 origin = prefab.transform.position;
            Transform props = prefab.transform.Find(BuildingInteriorStander.PropsChildName);
            InteriorFootprint fp = NmcBuildingShells.FootprintAt(s, Vector2.zero);
            var quads = props.Cast<Transform>()
                .Where(t => !PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && t.GetComponent<YSortSprite>() != null)
                .Select(t =>
                {
                    var sr = t.GetComponent<SpriteRenderer>();
                    Vector2 size = Vector2.Scale(sr.sprite.bounds.size, t.lossyScale);
                    Vector2 c = (Vector2)t.position - origin;
                    return (t.name, rect: new Rect(c - size * 0.5f, size), sortY: c.y + t.GetComponent<YSortSprite>().SortPivotYOffset);
                }).ToList();
            Assert.That(quads.Any(q => q.name == "NorthWallFace"), Is.True, $"{name} draws no back-wall face");
            foreach (NmcBuildingShells.Fixture f in s.Fixtures)
            {
                var art = AssetDatabase.LoadAssetAtPath<GameObject>(f.Prefab).GetComponent<ApplianceArt>();
                Vector2 foot = ApplianceSheets.For(art.Kind).Foot;
                int wall = ApplianceFacing.PickWallOffset(f.At, s.WidthMetres, s.LengthMetres, foot);
                Vector2 size = wall == 0 ? foot : new Vector2(foot.y, foot.x);
                Vector2 a = fp.ModelToWorld(f.At - size * 0.5f), b = fp.ModelToWorld(f.At + size * 0.5f);
                Rect onScreen = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                float fixtureSortY = fp.ModelToWorld(f.At).y;
                foreach (var q in quads.Where(q => q.sortY < fixtureSortY))
                    Assert.That(q.rect.Overlaps(onScreen), Is.False,
                                $"{name}/{q.name} draws in front of {f.Name} and covers its footprint {onScreen} with {q.rect}");
            }
        }

        [Test]
        public void ThePlayerFromTheStreet_AndTheKeeperFromTheCounter_ReachEveryFixture([ValueSource(nameof(WholesalerNames))] string name)
        {
            var s = Wholesalers.Single(x => x.Name == name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(s.PrefabPath);
            Vector2 origin = prefab.transform.position;
            InteriorFootprint fp = NmcBuildingShells.FootprintAt(s, Vector2.zero);
            Vector2 outward = (fp.DoorWorld - fp.Centre).normalized;
            var keeper = NmcWholesalerKeepers.All.Single(k => k.Shell.Name == name);

            var walls = new List<Vector2[]>();
            var circles = new List<(Vector2 c, float r)>();
            foreach (Collider2D c in prefab.GetComponentsInChildren<Collider2D>(true))
            {
                if (c.isTrigger) continue;
                Vector2 at = (Vector2)c.transform.position - origin + c.offset;
                if (c is PolygonCollider2D p)
                    for (int i = 0; i < p.pathCount; i++) walls.Add(p.GetPath(i).Select(v => v + at).ToArray());
                else if (c is CircleCollider2D cc) circles.Add((at, cc.radius));
                else Assert.Fail($"{name}: an unexpected {c.GetType().Name} on {c.name}");
            }
            Assert.That(circles.Count, Is.GreaterThanOrEqualTo(2), $"{name}: the toilet and the basin each carry a collider");

            Rect floor = Rect.MinMaxRect(fp.Corners().Min(v => v.x), fp.Corners().Min(v => v.y), fp.Corners().Max(v => v.x), fp.Corners().Max(v => v.y));
            Rect area = Rect.MinMaxRect(floor.xMin - 2f, floor.yMin - 2f, floor.xMax + 2f, floor.yMax + 2f);
            bool Free(Vector2 q) =>
                !walls.Any(w => InsidePath(w, q) || DistanceToPath(w, q) < FootRadiusMetres) &&
                !circles.Any(k => Vector2.Distance(k.c, q) < k.r + FootRadiusMetres);

            var interactables = s.Fixtures.Select(f =>
            {
                MonoBehaviour point = InteriorFixturePlacement.IdComponentOf(prefab.transform.Find(BuildingInteriorStander.PropsChildName + "/" + f.Name).gameObject);
                return (f, (HiddenHarbours.Core.IInteractable)point);
            }).ToList();

            foreach ((string who, Vector2 from) in new[]
                     {
                         ("the player from the street", fp.DoorWorld + outward * 1.5f),
                         ($"the keeper ({keeper.KeeperRoot}) from the counter", fp.ModelToWorld(keeper.StandModel)),
                     })
            {
                Assert.That(Free(from), Is.True, $"{name}: {who} starts inside a collider at {from}");
                List<Vector2> reached = Flood(area, from, Free);
                foreach (var (f, point) in interactables)
                {
                    Vector2 at = (Vector2)prefab.transform.Find(BuildingInteriorStander.PropsChildName + "/" + f.Name).position - origin;
                    float best = reached.Min(q => Vector2.Distance(q + new Vector2(0f, FootBelowMetres), at));
                    Assert.That(best, Is.LessThanOrEqualTo(point.ReachMeters),
                                $"{name}: {who} gets no nearer than {best:0.00} m to {f.Name} (reach {point.ReachMeters} m)");
                }
            }
        }

        /// <summary>Every free cell 4-connected to <paramref name="from"/> on a <see cref="CellMetres"/> grid.</summary>
        static List<Vector2> Flood(Rect area, Vector2 from, System.Func<Vector2, bool> free)
        {
            int nx = Mathf.CeilToInt(area.width / CellMetres), ny = Mathf.CeilToInt(area.height / CellMetres);
            Vector2 Cell(int x, int y) => new Vector2(area.xMin + x * CellMetres, area.yMin + y * CellMetres);
            var seen = new bool[nx + 1, ny + 1];
            var open = new Queue<(int x, int y)>();
            var reached = new List<Vector2>();
            int sx = Mathf.RoundToInt((from.x - area.xMin) / CellMetres), sy = Mathf.RoundToInt((from.y - area.yMin) / CellMetres);
            seen[sx, sy] = true; open.Enqueue((sx, sy));
            while (open.Count > 0)
            {
                var (x, y) = open.Dequeue();
                reached.Add(Cell(x, y));
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int ax = x + dx, ay = y + dy;
                    if (ax < 0 || ay < 0 || ax > nx || ay > ny || seen[ax, ay]) continue;
                    seen[ax, ay] = true;
                    if (free(Cell(ax, ay))) open.Enqueue((ax, ay));
                }
            }
            return reached;
        }

        static bool InsidePath(Vector2[] path, Vector2 p)
        {
            bool inside = false;
            for (int a = 0, b = path.Length - 1; a < path.Length; b = a++)
                if ((path[a].y > p.y) != (path[b].y > p.y) &&
                    p.x < (path[b].x - path[a].x) * (p.y - path[a].y) / (path[b].y - path[a].y) + path[a].x)
                    inside = !inside;
            return inside;
        }

        static float DistanceToPath(Vector2[] path, Vector2 p)
        {
            float best = float.MaxValue;
            for (int a = 0, b = path.Length - 1; a < path.Length; b = a++)
            {
                Vector2 e = path[a] - path[b];
                float t = e.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - path[b], e) / e.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector2.Distance(p, path[b] + e * t));
            }
            return best;
        }

        /// <summary>Is a point (in the prefab root's frame) inside one of the collider's paths? Read off the paths
        /// rather than asked of the physics world, which a prefab asset is not in.</summary>
        static bool Inside(PolygonCollider2D c, Vector2 p)
        {
            Vector2 local = p - (Vector2)c.transform.localPosition - c.offset;
            for (int i = 0; i < c.pathCount; i++)
            {
                Vector2[] path = c.GetPath(i);
                bool inside = false;
                for (int a = 0, b = path.Length - 1; a < path.Length; b = a++)
                    if ((path[a].y > local.y) != (path[b].y > local.y) &&
                        local.x < (path[b].x - path[a].x) * (local.y - path[a].y) / (path[b].y - path[a].y) + path[a].x)
                        inside = !inside;
                if (inside) return true;
            }
            return false;
        }

        static bool TryWorld(Doc tr, out Vector2 at, out string top)
        {
            at = default; top = null;
            Vector3 sum = Vector3.zero;
            for (Doc t = tr; t != null; )
            {
                if (t.Stripped) return false;
                sum += ParseVec3(t.Field("m_LocalPosition"));
                long father = t.FieldRef("m_Father");
                if (father == 0) { top = Scene.Require(t.FieldRef("m_GameObject"), "go").Field("m_Name"); break; }
                t = Scene.Get(father);
                if (t == null || !Plain(t)) return false;
            }
            at = sum;
            return true;
        }

        static List<string> Lineage(Doc tr)
        {
            var names = new List<string>();
            for (Doc t = tr; t != null; t = t.FieldRef("m_Father") == 0 ? null : Scene.Get(t.FieldRef("m_Father")))
                names.Add(Scene.Require(t.FieldRef("m_GameObject"), "go").Field("m_Name"));
            return names;
        }

        static bool Plain(Doc t)
        {
            string r = t.Field("m_LocalRotation"), s = t.Field("m_LocalScale");
            return r == "{x: 0, y: 0, z: 0, w: 1}" && s == "{x: 1, y: 1, z: 1}";
        }
    }
}
