using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>Every existing house is unchanged by the interior seam</b> (ADR 0060 PR 4). Two halves:
    /// <list type="bullet">
    /// <item>behaviour: a sprite house configured the way the builders configure one swaps shell, room,
    /// furniture and storeys exactly as before, keeps its rectangle, and has no mesh reveal;</item>
    /// <item>census: in every committed scene and prefab, each <see cref="BuildingInterior"/> is a sprite
    /// house (no dwelling def, no <see cref="DwellingCutaway"/>) unless it is on
    /// <see cref="MeshDwellingSites"/>, and a <see cref="DwellingCutaway"/> never stands anywhere but beside
    /// a <see cref="BuildingInterior"/> that has a dwelling def.</item>
    /// </list>
    /// </summary>
    public class SpriteHousesStaySpriteHousesTests
    {
        /// <summary>The GameObject names of committed mesh dwellings. Empty until the camper is placed (ADR
        /// 0060 PR 6), which adds its site here in the same PR.</summary>
        static readonly string[] MeshDwellingSites = { };

        const string BuildingInteriorMeta = "Assets/_Project/Code/World/BuildingInterior.cs.meta";
        const string DwellingCutawayMeta = "Assets/_Project/Code/World/DwellingCutaway.cs.meta";

        readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            HiddenHarbours.Core.MoveActionClaim.Reset();   // the stair walk claims the move axis while it runs
        }

        // ---- behaviour -----------------------------------------------------------------------------

        [Test]
        public void ASpriteHouse_SwapsExactlyAsBefore_AndKeepsItsRectangle()
        {
            var go = new GameObject("cottage"); _spawned.Add(go);
            var shell = new GameObject("Shell").AddComponent<SpriteRenderer>(); _spawned.Add(shell.gameObject);
            var room = new GameObject("Interior").AddComponent<SpriteRenderer>(); _spawned.Add(room.gameObject);
            var props = new GameObject("Furniture"); _spawned.Add(props);
            var upper = new GameObject("InteriorUpper").AddComponent<SpriteRenderer>(); _spawned.Add(upper.gameObject);
            var upperProps = new GameObject("FurnitureUpper"); _spawned.Add(upperProps);
            var upperWalls = new GameObject("WallsUpper"); _spawned.Add(upperWalls);
            var occupant = new GameObject("occupant").transform; _spawned.Add(occupant.gameObject);

            var interior = go.AddComponent<BuildingInterior>();
            interior.Configure(shell, room, props.transform, 6.6f, 8.05f, 3, 8, 0.6427876f, 0.3f, 1.4f, true, -1.68f);
            interior.ConfigureUpperLevel(upper, upperProps.transform, upperWalls.transform, 2.4f);
            interior.SetOccupant(occupant);

            Assert.That(interior.DwellingMesh, Is.Null);
            Assert.That(go.GetComponent<IInteriorReveal>(), Is.Null, "a sprite house has no mesh reveal");

            InteriorFootprint f = interior.Footprint;
            var expected = new InteriorFootprint(go.transform.position, 6.6f, 8.05f, 3, 8, 0.6427876f, 1f, -1.68f);
            Assert.That(f.HasFloorOutline, Is.False, "the rectangle, not an outline");
            Assert.That(f.WidthMetres, Is.EqualTo(expected.WidthMetres));
            Assert.That(f.LengthMetres, Is.EqualTo(expected.LengthMetres));
            Assert.That(f.DoorSign, Is.EqualTo(expected.DoorSign));
            Assert.That(f.DoorAcrossMetres, Is.EqualTo(expected.DoorAcrossMetres));
            Assert.That(f.DoorWorld, Is.EqualTo(expected.DoorWorld));

            AssertShown(shell, room, props, upper, upperProps, upperWalls, shellOn: true, ground: false, up: false, "outside");

            occupant.position = f.ModelToWorld(Vector2.zero);
            Tick(interior);
            Assert.That(interior.IsInside, Is.True);
            AssertShown(shell, room, props, upper, upperProps, upperWalls, shellOn: false, ground: true, up: false, "inside, ground");

            Assert.That(interior.TryGoToLevel(1), Is.True);
            AssertShown(shell, room, props, upper, upperProps, upperWalls, shellOn: false, ground: false, up: true, "inside, upstairs");
        }

        static void AssertShown(SpriteRenderer shell, SpriteRenderer room, GameObject props, SpriteRenderer upper,
                                GameObject upperProps, GameObject upperWalls, bool shellOn, bool ground, bool up,
                                string when)
        {
            Assert.That(shell.enabled, Is.EqualTo(shellOn), $"{when}: shell");
            Assert.That(room.enabled, Is.EqualTo(ground), $"{when}: ground room");
            Assert.That(props.activeSelf, Is.EqualTo(ground), $"{when}: ground furniture");
            Assert.That(upper.enabled, Is.EqualTo(up), $"{when}: upper room");
            Assert.That(upperProps.activeSelf, Is.EqualTo(up), $"{when}: upper furniture");
            Assert.That(upperWalls.activeSelf, Is.EqualTo(up), $"{when}: upper walls");
        }

        static void Tick(BuildingInterior interior) =>
            typeof(BuildingInterior)
                .GetMethod("Update", System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.NonPublic |
                                     System.Reflection.BindingFlags.Public)
                .Invoke(interior, null);

        // ---- census --------------------------------------------------------------------------------

        static string GuidOf(string metaPath)
        {
            Match m = Regex.Match(File.ReadAllText(metaPath), @"guid:\s*([0-9a-f]{32})");
            Assert.That(m.Success, Is.True, $"no guid in {metaPath}");
            return m.Groups[1].Value;
        }

        static IEnumerable<string> CommittedScenesAndPrefabs() =>
            Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets", "*.prefab", SearchOption.AllDirectories))
                .Select(p => p.Replace('\\', '/'));

        [Test]
        public void EveryCommittedBuildingInterior_IsASpriteHouse_UnlessItIsANamedMeshDwelling()
        {
            string interiorGuid = GuidOf(BuildingInteriorMeta);
            string cutawayGuid = GuidOf(DwellingCutawayMeta);
            int houses = 0;
            var problems = new List<string>();

            foreach (string path in CommittedScenesAndPrefabs())
            {
                string text = File.ReadAllText(path);
                if (!text.Contains(interiorGuid) && !text.Contains(cutawayGuid)) continue;

                // fileID of a GameObject -> its name; and the behaviours that sit on each GameObject.
                var names = new Dictionary<string, string>();
                var meshInteriors = new HashSet<string>();
                var spriteInteriors = new HashSet<string>();
                var cutaways = new HashSet<string>();

                foreach (string doc in text.Split(new[] { "\n--- !u!" }, System.StringSplitOptions.None))
                {
                    Match header = Regex.Match(doc, @"^(\d+) &(-?\d+)");
                    if (!header.Success) continue;

                    if (header.Groups[1].Value == "1")
                    {
                        Match name = Regex.Match(doc, @"\n  m_Name: ([^\n]*)");
                        if (name.Success) names[header.Groups[2].Value] = name.Groups[1].Value.Trim();
                        continue;
                    }
                    if (header.Groups[1].Value != "114") continue;

                    Match owner = Regex.Match(doc, @"\n  m_GameObject: \{fileID: (-?\d+)\}");
                    if (!owner.Success) continue;
                    string go = owner.Groups[1].Value;

                    if (doc.Contains(interiorGuid))
                    {
                        houses++;
                        Match mesh = Regex.Match(doc, @"\n  _dwellingMesh: \{fileID: (-?\d+)");
                        if (mesh.Success && mesh.Groups[1].Value != "0") meshInteriors.Add(go);
                        else spriteInteriors.Add(go);
                    }
                    else if (doc.Contains(cutawayGuid))
                    {
                        cutaways.Add(go);
                    }
                }

                string Name(string go) => names.TryGetValue(go, out string n) ? n : $"fileID {go}";

                foreach (string go in meshInteriors)
                {
                    if (!MeshDwellingSites.Contains(Name(go)))
                        problems.Add($"{path}: '{Name(go)}' is a mesh dwelling but not on MeshDwellingSites");
                    if (!cutaways.Contains(go))
                        problems.Add($"{path}: mesh dwelling '{Name(go)}' has no DwellingCutaway, so it never opens");
                }
                foreach (string go in cutaways)
                    if (!meshInteriors.Contains(go))
                        problems.Add($"{path}: a DwellingCutaway on '{Name(go)}' stands beside no mesh dwelling" +
                                     (spriteInteriors.Contains(go) ? " (a sprite house)" : ""));
            }

            Assert.That(houses, Is.GreaterThan(0), "the census found no BuildingInterior at all; it is reading nothing");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
    }
}
