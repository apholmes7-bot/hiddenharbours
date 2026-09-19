using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Tests.Support;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>Phase B, 2026-09-19, C6 — every hull with an Interior link walks to her door from where the
    /// game stands her.</b> The 2026-09-18 charter's guard, by name: "every hull with an Interior link has
    /// a reachable door from its boarding stand (a guard never asks the code for its own bar)". The cabin
    /// worked on the cape and the lobster boat and on nothing else, and no test knew: every cabin test
    /// in the tree stood her in a doorway by hand.
    ///
    /// <para>Two stands, both where the GAME puts her, at every drawn heading 0..359: the board spot
    /// (the switcher's <see cref="ControlSwitcher.BoardLocalOffset"/>, seeded onto the floor at
    /// <see cref="ControlSwitcher.BoardSpotHeightMeters"/>), and wherever leaving the helm stands her
    /// (a measured station's nearest floor, room or deck — <see cref="DeckWalkController.ChooseTheFloorNearest"/>
    /// — else the switcher's <see cref="ControlSwitcher.HelmLocalOffset"/> on the deck). From each stand
    /// <see cref="CabinWalkGraph"/> walks what the art measured — the deck def's areas, their flush
    /// contacts and the sidecar's steps, every placed route, every level's standable sole — and every
    /// door the def names must be reached: its band, HALF THE DEF'S CLEAR WIDTH read off the def here,
    /// must hold a point she can stand on at its sill.</para>
    ///
    /// <para>The hull is built the way the game builds it: her own <see cref="BoatHullDef"/> asset on a
    /// <see cref="BoatController"/>, her cabin by <see cref="BoatInteriorInstaller.Build"/>. Which of her
    /// levels are rooms is asked of THAT cabin, twice — before her cells load and after — because the
    /// game asks at the helm and at the board spot before they have (see <see cref="FrozenFloors"/>).</para>
    ///
    /// <para>🔴 A hull blocked on art is NAMED here with its reason (<see cref="BlockedOnArt"/>), never
    /// dropped from the fleet: it runs every time, is Ignored in the fleet report's words while the block
    /// holds, and FAILS the day she walks to her door, so the entry is retired rather than left to hide
    /// the next regression. A stand that puts her on no floor at all is never excused.</para>
    ///
    /// <para>⚠ Reads the SHIPPED assets and mutates none of them.</para>
    /// </summary>
    public sealed class EveryInteriorHullReachesItsDoorFromItsStandTests
    {
        private const string BoatsFolder = "Assets/_Project/Data/Boats";
        private const int Headings = 360;

        /// <summary>The fleet report's hulls, F#12–F#18 and F#20–F#24 (2026-09-18). The census insists
        /// the guard sees every one of them, so a lost Interior link cannot quietly shrink the fleet.</summary>
        private static readonly string[] ReportedFleet =
        {
            "CapeIslander", "LobsterBoat", "LobsterInshoreOpenNorthumberland",
            "LobsterStandardHardtopNorthumberland", "LobsterOffshoreOpenNorthumberland",
            "SportFisherConvertible", "SideDragger", "SportFisherSkybridge", "SternTrawler",
            "SternTrawlerMk2", "CoastalPacket", "Tanker",
        };

        /// <summary>Every Interior-linked hull on 2026-09-19: the eighteen lobster variants, the lobster
        /// boat, the cape, the packet, the dragger, both fishers, both trawlers and the tanker.</summary>
        private const int InteriorFleetOn20260919 = 27;

        /// <summary>
        /// 🔴 Hulls whose door the art has not yet made walkable, each with its reason — a debt with an
        /// owner, not an exemption (see the class remarks). Named once in
        /// <see cref="CabinFleet.BlockedOnArt"/>, shared with the PlayMode walk, so both runners retire an
        /// entry together.
        /// </summary>
        private static IReadOnlyDictionary<string, string> BlockedOnArt => CabinFleet.BlockedOnArt;

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown() => Dismantle(_spawned);

        /// <summary>Everything <see cref="Build"/> made, gone, and the channels its cutaway joined, cleared.</summary>
        internal static void Dismantle(List<Object> spawned)
        {
            // The cutaway subscribes to three signals in Configure, and its OnDestroy never runs in
            // EditMode: clear the channels or the next fixture inherits a dead hull's listeners.
            EventBus.Clear<CabinEntered>();
            EventBus.Clear<CabinLeft>();
            EventBus.Clear<ControlModeChanged>();
            Interactables.Clear();
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();
            BoatInteriorCells.Reset();
        }

        // =====================================================================================
        //  THE CENSUS — the guard below is only as good as the fleet it is handed
        // =====================================================================================

        [Test]
        public void TheGuardSeesEveryHullWithAnInteriorLink()
        {
            List<string> hulls = InteriorHulls().ToList();

            foreach (string reported in ReportedFleet)
                CollectionAssert.Contains(hulls, reported,
                    $"{reported} is in the 2026-09-18 fleet report but carries no Interior link any more — a " +
                    "hull that drops its cabin must be a decision somebody wrote down, not a silent shrink");
            Assert.GreaterOrEqual(hulls.Count, InteriorFleetOn20260919,
                $"{InteriorFleetOn20260919} hulls carried an Interior link on 2026-09-19; the guard sees " +
                $"{hulls.Count}: {string.Join(", ", hulls)}");
            Assert.AreEqual(hulls.Count, hulls.Distinct().Count(), "two hull assets share a name");
            foreach (string blocked in BlockedOnArt.Keys)
                CollectionAssert.Contains(hulls, blocked,
                    $"{blocked} is named as blocked on art but has no Interior link: retire the entry");
        }

        // =====================================================================================
        //  THE GUARD, BY NAME — both stands, every heading, every door
        // =====================================================================================

        [TestCaseSource(nameof(InteriorHulls))]
        public void FromHerBoardSpot_SheWalksToEveryDoorHerDefNames(string hullName)
            => Guard(hullName, Stand.BoardSpot);

        [TestCaseSource(nameof(InteriorHulls))]
        public void FromWhereLeavingTheHelmStandsHer_SheWalksToEveryDoorHerDefNames(string hullName)
            => Guard(hullName, Stand.LeaveHelm);

        private enum Stand { BoardSpot, LeaveHelm }

        private void Guard(string hullName, Stand stand)
        {
            Subject s = Build(hullName, _spawned);
            CabinWalkGraph graph = s.Graph;

            // ---- premises: what the graph borrows from the game must agree with the game ------
            int measured = 0;
            foreach (CabinDoorSpec door in graph.Doors)
            {
                if (!door.IsMeasured) continue;
                measured++;
                float sill = door.Threshold.z;
                Assert.AreEqual(s.Cabin.LevelIndexAtHeight(sill), graph.LevelAtHeight(sill, s.After),
                    $"{hullName} {door.Name}: the walk graph and her cabin disagree about which level a sill at " +
                    $"{sill:0.00} m opens onto — the graph's port of LevelIndexAtHeight has drifted");
            }
            Assert.Greater(measured, 0,
                $"{hullName} links an interior whose def measures no door (no ClearWidthMeters on any of " +
                $"{graph.Doors.Count}): nothing can ever be walked through. {graph.Describe()}");

            // ---- every heading, grouped by where the game stands her ----------------------------
            var stands = new Dictionary<CabinNode, List<int>>();
            for (int h = 0; h < Headings; h++)
            {
                CabinNode start = stand == Stand.BoardSpot
                    ? graph.BoardStand(h, s.BoardLocal, ControlSwitcher.BoardSpotHeightMeters, s.Elevation)
                    : graph.HelmStand(h, s.HelmLocal, s.Elevation, s.Before, s.After);
                if (!stands.TryGetValue(start, out List<int> headings)) stands[start] = headings = new List<int>();
                headings.Add(h);
            }

            var nowhere = new StringBuilder();
            var unreached = new StringBuilder();
            var reached = new StringBuilder();
            int nowhereHeadings = 0, unreachedHeadings = 0;
            foreach (KeyValuePair<CabinNode, List<int>> group in stands.OrderBy(g => g.Value[0]))
            {
                CabinNode start = group.Key;
                string at = $"{stand} at headings {Ranges(group.Value)} ({group.Value.Count}) -> {graph.Name(start)}";
                if (!start.IsSomewhere)
                {
                    nowhereHeadings += group.Value.Count;
                    nowhere.AppendLine($"  {at}: the game stands her on no floor the art measured");
                    continue;
                }

                CabinWalk walk = graph.Walk(start, s.Before, s.After);
                List<string> missed = graph.Doors.Where(d => d.IsMeasured && !walk.Reaches(d.Index))
                                           .Select(d => d.Name).ToList();
                if (missed.Count > 0)
                {
                    unreachedHeadings += group.Value.Count;
                    unreached.AppendLine($"  {at}: never reaches {string.Join(", ", missed)}; " +
                                         $"she can walk to {walk.DescribeNodes()}");
                    continue;
                }
                walk.TryGetReacher(0, out CabinNode reacher);
                reached.AppendLine($"  {at}: {graph.Doors[0].Name} by {walk.Describe(reacher)}");
            }

            // A stand on no floor is never the art's debt: the game put her somewhere it cannot walk.
            Assert.AreEqual(0, nowhereHeadings,
                $"{hullName}: at {nowhereHeadings} of {Headings} headings the {stand} stand puts her on no " +
                $"measured floor.\n{nowhere}{unreached}graph: {graph.Describe()}");

            if (BlockedOnArt.TryGetValue(hullName, out string why))
            {
                if (unreachedHeadings == 0)
                    Assert.Fail($"{hullName} is named as blocked on art, but from the {stand} stand she now walks " +
                                $"to every door at every heading — retire the entry.\n{reached}");
                Assert.Ignore($"{hullName} ({stand}, {unreachedHeadings}/{Headings} headings): {why}");
            }

            Assert.AreEqual(0, unreachedHeadings,
                $"{hullName}: at {unreachedHeadings} of {Headings} headings, from the {stand} stand, she cannot " +
                $"walk to every door her def names.\n{unreached}graph: {graph.Describe()}");

            Debug.Log($"[cabin-reach] {hullName} {stand}: every door at every heading, from " +
                      $"{stands.Count} stand(s)\n{reached}");
        }

        // =====================================================================================
        //  THE HULL, BUILT AS THE GAME BUILDS IT
        // =====================================================================================

        internal sealed class Subject
        {
            public BoatInterior Cabin;
            public CabinWalkGraph Graph;
            public FrozenFloors Before;
            public FrozenFloors After;
            public Vector2 BoardLocal;
            public Vector2 HelmLocal;
            public float Elevation;
        }

        /// <summary>Her hull asset on a boat, her cabin by her installer, the switcher's offsets beside
        /// her, and her walk graph over the defs that cabin was built from. Everything made goes into
        /// <paramref name="spawned"/> for <see cref="Dismantle"/>.</summary>
        internal static Subject Build(string hullName, List<Object> spawned)
        {
            BoatHullDef hull = LoadInteriorHull(hullName);
            Assert.IsNotNull(hull, $"no hull asset named {hullName} under {BoatsFolder}");
            BoatVisualDef visual = hull.Visual;
            Assert.IsNotNull(visual.Deck, $"{hullName} links an interior but carries no deck def: nowhere to stand");

            var boatGo = new GameObject(hullName);
            spawned.Add(boatGo);
            var boat = boatGo.AddComponent<BoatController>();
            boat.SetHull(hull);
            boat.enabled = false;
            var installer = boatGo.AddComponent<BoatInteriorInstaller>();
            installer.Build();
            Assert.IsTrue(installer.Built, $"{hullName}: her installer built no cabin from {visual.Interior.Id}");

            // The switcher's own offsets, at the values it ships with (the scenes leave them there).
            var switcherGo = new GameObject("Switcher");
            spawned.Add(switcherGo);
            var switcher = switcherGo.AddComponent<ControlSwitcher>();

            BoatInterior cabin = installer.Interior;
            FrozenFloors before = FrozenFloors.Snapshot(cabin);
            cabin.EnsureCells();
            FrozenFloors after = FrozenFloors.Snapshot(cabin);

            return new Subject
            {
                Cabin = cabin,
                Graph = new CabinWalkGraph(cabin.Def, visual.Deck, ProjectRoot),
                Before = before,
                After = after,
                BoardLocal = switcher.BoardLocalOffset,
                HelmLocal = switcher.HelmLocalOffset,
                Elevation = BoatInteriorInstaller.BakeElevationDegrees(visual),
            };
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private static IEnumerable<string> InteriorHulls()
        {
            var names = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:BoatHullDef", new[] { BoatsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(path);
                if (hull != null && hull.Visual != null && hull.Visual.Interior != null)
                    names.Add(Path.GetFileNameWithoutExtension(path));
            }
            names.Sort(System.StringComparer.Ordinal);
            return names;
        }

        internal static BoatHullDef LoadInteriorHull(string hullName)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:BoatHullDef", new[] { BoatsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == hullName)
                    return AssetDatabase.LoadAssetAtPath<BoatHullDef>(path);
            }
            return null;
        }

        /// <summary>"0-29, 300-359" for a sorted list of headings.</summary>
        internal static string Ranges(List<int> headings)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < headings.Count; i++)
            {
                int first = headings[i];
                while (i + 1 < headings.Count && headings[i + 1] == headings[i] + 1) i++;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(first);
                if (headings[i] != first) sb.Append('-').Append(headings[i]);
            }
            return sb.ToString();
        }
    }
}
