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
using Subject = HiddenHarbours.Tests.EditMode.EveryInteriorHullReachesItsDoorFromItsStandTests.Subject;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>The helm census (2026-11-13, the fleet audit's R2): on every hull with a cabin, where does
    /// leaving the wheel stand her, and does the game know it when that is inside a room?</b>
    ///
    /// <para><b>What R2 said.</b> "Inside" was only ever set by a door or a stair. A hull swapped on
    /// under her while she steers, or a wheel taken from the cockpit, puts her at the wheel without
    /// either. If the wheel stands in the house, she steps back from it into a house that stays shut.</para>
    ///
    /// <para><b>What this measures, per hull, off the shipped assets.</b> The stand is the game's own:
    /// a measured station's nearest floor (<see cref="DeckWalkController.ChooseTheFloorNearest"/>), or
    /// the switcher's helm offset seeded onto the deck at every drawn heading. Where the walker itself
    /// chooses a ROOM she is entered by the walker and there is nothing to prove. Where it stands her on
    /// a DECK AREA, this asks the art a question the game's own rule does not: is that spot inside the
    /// outline of a level the hull mesh encloses, between its sole and its CEILING
    /// (<see cref="EnclosedLevelHolding"/>)? The game's rule (<see cref="DeckWalkController.RoomStoodIn"/>,
    /// what <c>ControlSwitcher.LeaveHelm</c> enters her by) asks by the sole and the def's floor
    /// tolerance. The two must name the same room, or neither: a stand inside a house that nothing
    /// counts as inside fails here, naming the hull.</para>
    ///
    /// <para><b>And the finding is pinned</b> (<see cref="TheCapeAndEveryLobsterVariant_StandAtTheirWheelInTheWheelhouse_OnADeckArea"/>):
    /// on the cape islander and all eighteen lobster variants the walker's choice is the deck (the
    /// cockpit's area runs in under the house at the house sole's height, and the deck keeps a tie), so
    /// the walker never enters her; and that deck spot is in the wheelhouse. That is R2, true on
    /// nineteen hulls.</para>
    ///
    /// <para>⚠ Reads the SHIPPED assets and mutates none of them.</para>
    /// </summary>
    public sealed class EveryHelmStandKnowsTheRoomItIsInTests
    {
        private const string BoatsFolder = "Assets/_Project/Data/Boats";
        private const int Headings = 360;

        /// <summary>Hulls that carried an Interior link on 2026-11-13: the audit's 33 cabin hulls less
        /// the two sloops, whose mesh names enclosed levels but which have no interior def at all.</summary>
        private const int InteriorFleetOn20261113 = 31;

        /// <summary>The wheelhouse, by the def's own id, on the cape and every lobster variant.</summary>
        private const string WheelhouseLevelId = "house_sole";

        private const string CapeIslander = "CapeIslander";

        /// <summary>The eighteen variants are named Lobster{Inshore|Standard|Offshore}{Open|Hardtop}{coast}.</summary>
        private static readonly string[] LobsterVariantStems = { "LobsterInshore", "LobsterStandard", "LobsterOffshore" };
        private const int LobsterVariants = 18;

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown() => EveryInteriorHullReachesItsDoorFromItsStandTests.Dismantle(_spawned);

        // =====================================================================================
        //  THE FLEET THE CENSUS IS HANDED
        // =====================================================================================

        [Test]
        public void TheCensusSeesEveryHullWithAnInteriorLink()
        {
            List<string> hulls = InteriorHulls().ToList();
            Assert.GreaterOrEqual(hulls.Count, InteriorFleetOn20261113,
                $"{InteriorFleetOn20261113} hulls carried an Interior link on 2026-11-13; the census sees " +
                $"{hulls.Count}: {string.Join(", ", hulls)}");
            CollectionAssert.Contains(hulls, CapeIslander);
            Assert.AreEqual(LobsterVariants, hulls.Count(IsLobsterVariant),
                "the eighteen lobster variants are the bulk of the R2 finding; one has gone missing or been added");
        }

        // =====================================================================================
        //  THE CENSUS — one case per hull
        // =====================================================================================

        [TestCaseSource(nameof(InteriorHulls))]
        public void WhereLeavingTheWheelStandsHer_SheIsCountedInsideExactlyWhenSheStandsInARoom(string hullName)
        {
            BoatHullDef hull = EveryInteriorHullReachesItsDoorFromItsStandTests.LoadInteriorHull(hullName);
            Assert.IsNotNull(hull, $"no hull asset named {hullName} under {BoatsFolder}");
            if (!hull.Visual.Interior.HasInterior())
                Assert.Pass($"{hullName}: her interior def has no walkable level, so there is no room to stand in");

            Subject s = EveryInteriorHullReachesItsDoorFromItsStandTests.Build(hullName, _spawned);
            List<Stand> stands = HelmStands(hull, s);

            var wrong = new StringBuilder();
            var report = new StringBuilder();
            foreach (Stand stand in stands)
            {
                report.AppendLine("  " + Describe(hull, stand));
                if (stand.Nowhere)
                    wrong.AppendLine($"  headings {stand.HeadingRange}: the helm stands her on no floor the art measured");
                else if (!stand.ByTheWalker && stand.CountedIn != stand.EnclosedBy)
                    wrong.AppendLine(
                        $"  headings {stand.HeadingRange}: she stands on deck area '{stand.AreaId}' at " +
                        $"({stand.Feet.x:0.00}, {stand.Feet.y:0.00}), floor {stand.FloorZ:0.00} m. The hull mesh " +
                        $"encloses that spot in {LevelName(hull, stand.EnclosedBy)}; leaving the wheel counts her " +
                        $"inside {LevelName(hull, stand.CountedIn)}.");
                else if (!stand.ByTheWalker && stand.CountedIn != stand.CountedInOnceHerCellsLoad)
                    wrong.AppendLine(
                        $"  headings {stand.HeadingRange}: counted inside {LevelName(hull, stand.CountedIn)} before her " +
                        $"cells load and {LevelName(hull, stand.CountedInOnceHerCellsLoad)} after: the answer must " +
                        "not depend on whether she has been below before");
            }

            Assert.AreEqual(0, wrong.Length,
                $"{hullName}: where leaving the wheel stands her and whether she is counted inside disagree " +
                $"(the fleet audit's R2).\n{wrong}the whole census for her:\n{report}");
            Debug.Log($"[helm-census] {hullName}\n{report}");
        }

        // =====================================================================================
        //  THE FINDING, PINNED — R2 is true on these nineteen
        // =====================================================================================

        [TestCaseSource(nameof(WheelhouseHulls))]
        public void TheCapeAndEveryLobsterVariant_StandAtTheirWheelInTheWheelhouse_OnADeckArea(string hullName)
        {
            BoatHullDef hull = EveryInteriorHullReachesItsDoorFromItsStandTests.LoadInteriorHull(hullName);
            Assert.IsNotNull(hull, $"no hull asset named {hullName} under {BoatsFolder}");
            Subject s = EveryInteriorHullReachesItsDoorFromItsStandTests.Build(hullName, _spawned);
            Assert.IsTrue(hull.Visual.Deck.HasHelmStation, $"{hullName}: premise, her rig measures a helm station");

            List<Stand> stands = HelmStands(hull, s);
            Assert.AreEqual(1, stands.Count, $"{hullName}: a measured station stands her in one place at every heading");
            Stand stand = stands[0];

            Assert.IsFalse(stand.Nowhere, $"{hullName}: the helm stands her on no floor");
            Assert.IsFalse(stand.ByTheWalker,
                $"{hullName}: the walker's own choice was expected to be the DECK (the cockpit's area runs in under " +
                "the house at the house sole's height, and the deck keeps a tie), which is why nothing entered her " +
                $"before 2026-11-13. It now chooses a room: {Describe(hull, stand)}. If that is deliberate the " +
                "LeaveHelm call is no longer what stands her inside; rewrite this pin, do not delete it.");

            int wheelhouse = LevelIndex(hull.Visual.Interior, WheelhouseLevelId);
            Assert.GreaterOrEqual(wheelhouse, 0, $"{hullName}: her def names no level '{WheelhouseLevelId}'");
            Assert.AreEqual(wheelhouse, stand.EnclosedBy,
                $"{hullName}: the art puts her wheel in the wheelhouse. {Describe(hull, stand)}");
            Assert.AreEqual(wheelhouse, stand.CountedIn,
                $"{hullName}: leaving the wheel must count her inside her wheelhouse. {Describe(hull, stand)}");
        }

        // =====================================================================================
        //  THE RULE ITSELF, on a room drawn by hand
        // =====================================================================================

        [Test]
        public void RoomStoodIn_IsTheRoomWhoseOutlineHoldsHerAndWhoseSoleIsHerFloor()
        {
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();
            _spawned.Add(def);
            const float lowerSole = 0.5f, upperSole = 3f;
            def.Levels = new[]
            {
                Room("lower", lowerSole, -1f, 1f, 0f, 2f),
                Room("upper", upperSole, -1f, 1f, 0f, 2f),
                Room("undrawn", lowerSole, -1f, 1f, 4f, 6f),
            };
            var floors = new FrozenFloors(def, new[] { true, true, false });
            float tolerance = def.FloorTolerance;
            var inPlan = new Vector2(0f, 1f);

            Assert.AreEqual(0, DeckWalkController.RoomStoodIn(floors, inPlan, lowerSole), "on the lower sole, in its outline");
            Assert.AreEqual(1, DeckWalkController.RoomStoodIn(floors, inPlan, upperSole), "the same plan, a deck up");
            Assert.AreEqual(0, DeckWalkController.RoomStoodIn(floors, inPlan, lowerSole + tolerance * 0.5f),
                            "a deck area a hair off the sole is still that floor");
            Assert.AreEqual(-1, DeckWalkController.RoomStoodIn(floors, inPlan, lowerSole + tolerance * 2f),
                            "standing over a room is not standing in it");
            Assert.AreEqual(-1, DeckWalkController.RoomStoodIn(floors, new Vector2(0f, -1f), lowerSole),
                            "outside the outline is the open deck");
            Assert.AreEqual(-1, DeckWalkController.RoomStoodIn(floors, new Vector2(0f, 5f), lowerSole),
                            "a level the hull does not draw as a room is never stood in");
            Assert.AreEqual(-1, DeckWalkController.RoomStoodIn(null, inPlan, lowerSole), "no cabin, no room");
        }

        private static BoatInteriorLevel Room(string id, float sole, float x0, float x1, float y0, float y1)
            => new BoatInteriorLevel
            {
                Id = id,
                SoleZMeters = sole,
                Outline = new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) },
            };

        // =====================================================================================
        //  THE STAND, as the game makes it
        // =====================================================================================

        private sealed class Stand
        {
            public readonly List<int> AtHeadings = new();
            public bool Nowhere;
            /// <summary>The walker's own nearest-floor choice was a room: it enters her itself.</summary>
            public bool ByTheWalker;
            public int WalkerRoom = -1;
            public string AreaId = "";
            public Vector2 Feet;
            public float FloorZ;
            /// <summary>The art's answer: the enclosed level holding her feet, sole to ceiling. −1 = open deck.</summary>
            public int EnclosedBy = -1;
            /// <summary>The game's answer, before and after her cells load. −1 = not counted inside.</summary>
            public int CountedIn = -1, CountedInOnceHerCellsLoad = -1;

            public string HeadingRange => EveryInteriorHullReachesItsDoorFromItsStandTests.Ranges(AtHeadings);
            public string Key => Nowhere ? "nowhere" : $"{ByTheWalker}|{WalkerRoom}|{AreaId}|{EnclosedBy}|{CountedIn}|{CountedInOnceHerCellsLoad}";
        }

        /// <summary>Every distinct place leaving the wheel stands her, over the drawn headings. A measured
        /// station is a hull-frame point and stands her in one place; the switcher's fallback offset is a
        /// screen offset and may not.</summary>
        private static List<Stand> HelmStands(BoatHullDef hull, Subject s)
        {
            BoatVisualDef visual = hull.Visual;
            BoatDeckDef deck = visual.Deck;
            BoatInteriorDef def = visual.Interior;
            HullMeshDef mesh = visual.HasHullMesh() ? visual.HullMesh : null;

            var byKey = new Dictionary<string, Stand>();
            var order = new List<Stand>();
            int headings = deck.HasHelmStation ? 1 : Headings;
            for (int h = 0; h < headings; h++)
            {
                var stand = new Stand();
                if (deck.HasHelmStation)
                {
                    int room = DeckWalkController.ChooseTheFloorNearest(
                        deck, s.Before, deck.HelmStationLocalMeters, false, out Vector2 roomSeat, out bool measured,
                        out Vector2 deckSeat, out int deckArea, out float deckHeight);
                    if (room >= 0)
                    {
                        stand.ByTheWalker = true;
                        stand.WalkerRoom = room;
                        stand.Feet = roomSeat;
                        stand.FloorZ = def.Levels[room].SoleZMeters;
                    }
                    else if (measured && deckArea >= 0) OnDeck(stand, deck, deckArea, deckSeat, deckHeight);
                    else stand.Nowhere = true;
                }
                else if (deck.HasWalkableDeck())
                {
                    // The switcher's fallback, as CabinWalkGraph.HelmStand seeds it: twice, the second
                    // from the first's area.
                    Vector2 named = DeckAreaMath.WorldToDeck(s.HelmLocal, 0f, 0f, s.Elevation);
                    Vector2 relative = DeckAreaMath.DeckToWorld(named, 0f, h, s.Elevation);
                    int area = -1;
                    DeckWalkController.SeedDeckLocalPure(relative, h, s.Elevation, deck, false, ref area, out _);
                    Vector2 seat = DeckWalkController.SeedDeckLocalPure(relative, h, s.Elevation, deck, false,
                                                                        ref area, out float height);
                    if (area >= 0) OnDeck(stand, deck, area, seat, height);
                    else stand.Nowhere = true;
                }
                else stand.Nowhere = true;

                if (!stand.Nowhere && !stand.ByTheWalker)
                {
                    stand.EnclosedBy = EnclosedLevelHolding(def, mesh, stand.Feet, stand.FloorZ);
                    stand.CountedIn = DeckWalkController.RoomStoodIn(s.Before, stand.Feet, stand.FloorZ);
                    stand.CountedInOnceHerCellsLoad = DeckWalkController.RoomStoodIn(s.After, stand.Feet, stand.FloorZ);
                }

                if (!byKey.TryGetValue(stand.Key, out Stand seen))
                {
                    byKey[stand.Key] = seen = stand;
                    order.Add(stand);
                }
                seen.AtHeadings.Add(h);
            }
            return order;
        }

        private static void OnDeck(Stand stand, BoatDeckDef deck, int area, Vector2 seat, float height)
        {
            stand.AreaId = deck.Areas[area] != null ? deck.Areas[area].Id : "?";
            stand.Feet = seat;
            stand.FloorZ = height;
        }

        /// <summary>
        /// The art's own answer, asked a different way from the game's rule: the level whose outline
        /// holds the point and which the hull mesh's level table ENCLOSES, with her floor between that
        /// level's sole (less the def's tolerance) and the table's CEILING for it. Where the table has no
        /// row for a level the cutaway can still open (a hull-walk@1 lift set), the ceiling is unknown and
        /// her floor must be the sole. The highest such sole wins: the room whose floor is nearest under
        /// her. −1 when she stands in the open.
        /// </summary>
        private static int EnclosedLevelHolding(BoatInteriorDef def, HullMeshDef mesh, Vector2 feet, float floorZ)
        {
            if (def == null || def.Levels == null || mesh == null) return -1;
            int best = -1;
            for (int i = 0; i < def.Levels.Length; i++)
            {
                BoatInteriorLevel level = def.Levels[i];
                if (level == null || !level.IsUsable()) continue;
                if (!mesh.CutawayForDeck(level.Id).Opens) continue;   // the cutaway's own gate: no cut, no room
                if (floorZ < level.SoleZMeters - def.FloorTolerance) continue;

                bool hasRow = TryEnclosedRow(mesh, level.Id, out HullMeshDef.LevelTag row);
                bool underTheCeiling = hasRow && row.CeilingZMeters > level.SoleZMeters
                    ? floorZ < row.CeilingZMeters
                    : floorZ <= level.SoleZMeters + def.FloorTolerance;
                if (!underTheCeiling) continue;
                if (!DeckAreaMath.Contains(level.Outline, feet)) continue;
                if (best < 0 || level.SoleZMeters > def.Levels[best].SoleZMeters) best = i;
            }
            return best;
        }

        private static bool TryEnclosedRow(HullMeshDef mesh, string levelId, out HullMeshDef.LevelTag row)
        {
            row = default;
            if (mesh.LevelTags == null) return false;
            foreach (HullMeshDef.LevelTag tag in mesh.LevelTags)
                if (tag.Enclosed && (tag.DeckId == levelId || tag.LevelId == levelId))
                {
                    row = tag;
                    return true;
                }
            return false;
        }

        private static string Describe(BoatHullDef hull, Stand stand)
        {
            BoatDeckDef deck = hull.Visual.Deck;
            string station = deck.HasHelmStation
                ? $"station ({deck.HelmStationLocalMeters.x:0.00}, {deck.HelmStationLocalMeters.y:0.00}, {deck.HelmStationLocalMeters.z:0.00})"
                : "no station (the switcher's fallback offset)";
            if (stand.Nowhere) return $"{station}, headings {stand.HeadingRange}: on no measured floor";
            string at = $"({stand.Feet.x:0.00}, {stand.Feet.y:0.00}) at {stand.FloorZ:0.00} m";
            if (stand.ByTheWalker)
                return $"{station}, headings {stand.HeadingRange}: the walker stands her in room " +
                       $"{LevelName(hull, stand.WalkerRoom)} at {at} and enters it itself";
            return $"{station}, headings {stand.HeadingRange}: deck area '{stand.AreaId}' at {at}; the mesh encloses " +
                   $"that in {LevelName(hull, stand.EnclosedBy)}; leaving the wheel counts her inside " +
                   $"{LevelName(hull, stand.CountedIn)}";
        }

        private static string LevelName(BoatHullDef hull, int level)
        {
            BoatInteriorDef def = hull.Visual.Interior;
            if (level < 0) return "nothing (the open deck)";
            return def != null && def.Levels != null && level < def.Levels.Length && def.Levels[level] != null
                ? $"'{def.Levels[level].Id}'"
                : $"level {level}";
        }

        private static int LevelIndex(BoatInteriorDef def, string id)
        {
            if (def == null || def.Levels == null) return -1;
            for (int i = 0; i < def.Levels.Length; i++)
                if (def.Levels[i] != null && def.Levels[i].Id == id) return i;
            return -1;
        }

        // =====================================================================================
        //  THE FLEET
        // =====================================================================================

        private static bool IsLobsterVariant(string hullName)
            => LobsterVariantStems.Any(stem => hullName.StartsWith(stem, System.StringComparison.Ordinal));

        private static IEnumerable<string> WheelhouseHulls()
            => InteriorHulls().Where(h => h == CapeIslander || IsLobsterVariant(h));

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
    }
}
