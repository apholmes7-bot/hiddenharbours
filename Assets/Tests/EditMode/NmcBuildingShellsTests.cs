using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
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
