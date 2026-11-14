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
using Subject = HiddenHarbours.Tests.EditMode.EveryInteriorHullReachesItsDoorFromItsStandTests.Subject;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>The cabin census (2026-11-14): on every hull that links an interior, does she build a door
    /// and a cutaway, does every room she has open, and which room, if any, does her wheel stand in?</b>
    ///
    /// <para><b>Why.</b> The owner's ruling of 2026-11-14: walking inside an enclosed level cuts that
    /// level; at a wheel that stands inside an enclosed room, that ONE room is cut so the skipper and
    /// the instruments show; at a wheel on an open deck or a flybridge nothing is cut. The door-and-cut
    /// proof before this covered a handful of hulls by name; this asks the whole fleet, off the shipped
    /// assets, so a hull added tomorrow is asked too.</para>
    ///
    /// <para><b>What it measures, per hull.</b>
    /// <list type="bullet">
    ///   <item>Her installer builds a cabin, a main door and a cutaway.</item>
    ///   <item>Every level of her def that is a room opens when she is entered into it, through the
    ///   real cutaway, with the cut the hull mesh keys by that level's id; every level her mesh's table
    ///   declares OPEN opens nothing; every enclosed row of that table can be opened by an id.</item>
    ///   <item>The room her wheel stands in (<see cref="BoatHelmRoom"/>), which her built cutaway must
    ///   hold, and which must open whenever the game itself stands her in a room on leaving the wheel
    ///   (<see cref="DeckWalkController.ChooseTheFloorNearest"/>, <see cref="DeckWalkController.RoomStoodIn"/>).</item>
    /// </list></para>
    ///
    /// <para><b>And the findings are pinned</b>, by the ids the art gives: which hulls steer from a
    /// wheelhouse, a bridge, a helm deck; which from the open; and which name no helm station at all
    /// and so keep a closed house at the wheel until the art measures one.</para>
    ///
    /// <para>⚠ Reads the SHIPPED assets and mutates none of them.</para>
    /// </summary>
    public sealed class EveryHullOpensTheRoomHerWheelStandsInTests
    {
        private const string BoatsFolder = "Assets/_Project/Data/Boats";

        /// <summary>Hulls that carried an Interior link on 2026-11-13 (the helm census's own count).</summary>
        private const int InteriorFleetOn20261113 = 31;

        /// <summary>Of those, the hulls whose interior def has no walkable level and so build no cabin:
        /// the go-fast (the fleet audit's slice 7).</summary>
        private const int HullsWithNoWalkableLevelOn20261114 = 1;

        private static readonly string[] LobsterVariantStems = { "LobsterInshore", "LobsterStandard", "LobsterOffshore" };

        /// <summary>
        /// The finding, by hull: the id of the room her wheel stands in, as her own data names it (a
        /// def level's id, or the id her mesh keys a mesh-only room's cut by). Measured 2026-11-14 off
        /// the shipped assets. The cape and the eighteen lobster variants (the wheelhouse) are pinned by
        /// <see cref="WheelhouseHulls"/> instead of being listed here one by one.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> RoomAtTheWheel = new Dictionary<string, string>
        {
            ["SternTrawler"] = "bridge",
            ["SternTrawlerMk2"] = "bridge",
            ["SideDragger"] = "bridge",
            ["SportFisherSkybridge"] = "bridge_sole",
            ["ExpeditionYacht120"] = "bridge_sole",
            ["MotorYacht84"] = "dining_helm",
            ["ExpressCruiser"] = "house",
        };

        private const string WheelhouseLevelId = "house_sole";
        private const string CapeIslander = "CapeIslander";

        /// <summary>Hulls with a cabin whose wheel stands in the open: a flybridge over the house.</summary>
        private static readonly string[] WheelInTheOpen = { "SportFisherConvertible" };

        /// <summary>
        /// 🔴 Hulls with a cabin whose art names NO helm station, so nothing can say which room the wheel
        /// is in and the house stays closed at the wheel. A debt with an owner (the rig's gameplay
        /// sidecar owes a helm anchor), not an exemption: the case below FAILS the day one gains a
        /// station, so the entry is retired and she is pinned in <see cref="RoomAtTheWheel"/> instead.
        /// </summary>
        private static readonly string[] NoMeasuredStation = { "LobsterBoat", "CoastalPacket", "Tanker" };

        private readonly List<Object> _spawned = new();
        private GameConfig _configBefore;

        [SetUp]
        public void SetUp()
        {
            GameServices.Helm.Reset();
            _configBefore = GameServices.Config;
            GameServices.Config = null;     // the ruled default: the wheel's room opens
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = _configBefore;
            GameServices.Helm.Reset();
            EventBus.Clear<CabinEntryLandingEntered>();
            EventBus.Clear<ActiveBoatChanged>();
            EveryInteriorHullReachesItsDoorFromItsStandTests.Dismantle(_spawned);
        }

        // =====================================================================================
        //  THE FLEET THE CENSUS IS HANDED
        // =====================================================================================

        [Test]
        public void TheCensusSeesEveryHullWithAnInteriorLink_AndAllButOneBuildACabin()
        {
            List<string> hulls = InteriorHulls().ToList();
            Assert.GreaterOrEqual(hulls.Count, InteriorFleetOn20261113,
                $"{InteriorFleetOn20261113} hulls carried an Interior link on 2026-11-13; the census sees " +
                $"{hulls.Count}: {string.Join(", ", hulls)}");

            List<string> noCabin = hulls.Where(h => !Load(h).Visual.Interior.HasInterior()).ToList();
            Assert.LessOrEqual(noCabin.Count, HullsWithNoWalkableLevelOn20261114,
                "hulls that link an interior with no walkable level build no cabin, no door and no cutaway. " +
                $"There was {HullsWithNoWalkableLevelOn20261114} on 2026-11-14; now: {string.Join(", ", noCabin)}");
        }

        // =====================================================================================
        //  A DOOR, A CUTAWAY, AND EVERY ROOM OPENS — one case per hull
        // =====================================================================================

        [TestCaseSource(nameof(InteriorHulls))]
        public void SheBuildsADoorAndACutaway_AndEveryRoomSheHasOpens(string hullName)
        {
            BoatHullDef hull = Load(hullName);
            BoatVisualDef visual = hull.Visual;
            BoatInteriorDef def = visual.Interior;
            if (!def.HasInterior())
                Assert.Pass($"{hullName}: her interior def has no walkable level; she builds no cabin (counted above)");

            Assert.IsTrue(visual.HasHullMesh(),
                $"{hullName}: she links an interior and carries no usable hull mesh, so nothing can cut her open");
            HullMeshDef mesh = visual.HullMesh;

            Subject s = EveryInteriorHullReachesItsDoorFromItsStandTests.Build(hullName, _spawned);
            var installer = s.Cabin.GetComponent<BoatInteriorInstaller>();
            Assert.IsNotNull(installer, $"{hullName}: premise, her cabin sits on the root her installer is on");
            Assert.IsNotNull(installer.Door, $"{hullName}: her installer built no main door");
            Assert.IsNotNull(installer.Cutaway, $"{hullName}: her installer built no cutaway");
            BoatCutaway cutaway = installer.Cutaway;
            EntityId hullId = installer.gameObject.GetEntityId();

            var wrong = new StringBuilder();
            var report = new StringBuilder();
            int rooms = 0;
            for (int i = 0; i < def.Levels.Length; i++)
            {
                BoatInteriorLevel level = def.Levels[i];
                if (level == null || !level.IsUsable()) continue;
                HullMeshDef.Cut keyed = mesh.CutawayForDeck(level.Id);

                // She is entered into the level the way the cabin says it: the real cutaway answers.
                EventBus.Publish(new CabinEntered(hullId, i));
                HullMeshDef.Cut asked = cutaway.RequestedCut;
                EventBus.Publish(new CabinLeft(hullId));

                if (DeclaredOpen(mesh, level.Id))
                {
                    report.AppendLine($"  '{level.Id}': an open deck, nothing to cut");
                    if (asked.Opens)
                        wrong.AppendLine($"  '{level.Id}' is declared OPEN by her level table and standing on it cuts {asked}");
                    continue;
                }

                rooms++;
                report.AppendLine($"  '{level.Id}': a room, opens with {Describe(asked)}");
                if (!asked.Opens)
                    wrong.AppendLine($"  room '{level.Id}' (sole {level.SoleZMeters:0.00} m) opens nothing when she is inside it");
                else if (!Same(asked, keyed))
                    wrong.AppendLine($"  room '{level.Id}': the cutaway asks for {Describe(asked)}, her mesh keys {Describe(keyed)}");
                if (cutaway.RequestedCut.Opens)
                    wrong.AppendLine($"  leaving '{level.Id}' left her open: {Describe(cutaway.RequestedCut)}");
            }

            foreach (HullMeshDef.LevelTag row in mesh.LevelTags)
            {
                if (!row.Enclosed) continue;
                if (!mesh.CutawayForDeck(row.DeckId).Opens && !mesh.CutawayForDeck(row.LevelId).Opens)
                    wrong.AppendLine($"  her level table encloses '{row.LevelId}' (deck '{row.DeckId}') and no id opens it");
            }

            Assert.Greater(rooms, 0, $"{hullName}: she has walkable levels and none of them is a room\n{report}");
            Assert.AreEqual(0, wrong.Length, $"{hullName}: a room that does not open, or a deck that does.\n{wrong}her levels:\n{report}");
            Debug.Log($"[cabin-census] {hullName}: door, cutaway, {rooms} room(s)\n{report}");
        }

        // =====================================================================================
        //  THE ROOM AT THE WHEEL — one case per hull
        // =====================================================================================

        [TestCaseSource(nameof(InteriorHulls))]
        public void AtHerWheel_HerCutawayHoldsTheRoomHerStationStandsIn_AndItOpensWhereTheGameStandsHerInARoom(string hullName)
        {
            BoatHullDef hull = Load(hullName);
            BoatVisualDef visual = hull.Visual;
            BoatInteriorDef def = visual.Interior;
            BoatDeckDef deck = visual.Deck;
            if (!def.HasInterior())
                Assert.Pass($"{hullName}: no walkable level, no cabin, no cutaway to open at any wheel");

            HullMeshDef mesh = visual.HasHullMesh() ? visual.HullMesh : null;
            Subject s = EveryInteriorHullReachesItsDoorFromItsStandTests.Build(hullName, _spawned);
            var installer = s.Cabin.GetComponent<BoatInteriorInstaller>();
            Assert.IsNotNull(installer.Cutaway, $"{hullName}: her installer built no cutaway");
            BoatCutaway cutaway = installer.Cutaway;

            if (!deck.HasHelmStation)
            {
                Assert.IsFalse(cutaway.HelmRoomCut.Opens,
                    $"{hullName}: her art names no helm station and her cutaway opens {Describe(cutaway.HelmRoomCut)} at the wheel");
                Debug.Log($"[helm-room] {hullName}: no measured helm station; her house stays closed at the wheel");
                return;
            }

            Vector3 station = deck.HelmStationLocalMeters;
            HullMeshDef.Cut byTheRule = BoatHelmRoom.CutAt(def, mesh, station, out string room);
            Assert.IsTrue(Same(byTheRule, cutaway.HelmRoomCut),
                $"{hullName}: the cutaway her installer built holds {Describe(cutaway.HelmRoomCut)} for the wheel; " +
                $"her data says {Describe(byTheRule)} (room '{room}')");

            // The game's own answer to "where does leaving the wheel stand her", asked independently.
            int stoodIn = DeckWalkController.ChooseTheFloorNearest(
                deck, s.Before, station, false, out _, out bool measured,
                out Vector2 deckSeat, out int deckArea, out float deckHeight);
            if (stoodIn < 0 && measured && deckArea >= 0)
                stoodIn = DeckWalkController.RoomStoodIn(s.Before, deckSeat, deckHeight);
            string stoodInId = stoodIn >= 0 ? def.Levels[stoodIn].Id : null;

            if (stoodIn >= 0)
                Assert.IsTrue(byTheRule.Opens,
                    $"{hullName}: leaving the wheel stands her inside room '{stoodInId}', and at the wheel nothing is cut. " +
                    $"Station ({station.x:0.00}, {station.y:0.00}, {station.z:0.00}).");

            // At the wheel, through the real cutaway: the wheel's room and nothing else.
            GameServices.Helm.SetPilotedHull(installer.GetComponent<BoatController>());
            EventBus.Publish(new ControlModeChanged(ControlMode.Aboard));
            HullMeshDef.Cut atTheWheel = cutaway.RequestedCut;
            GameServices.Helm.SetPilotedHull(null);
            EventBus.Publish(new ControlModeChanged(ControlMode.OnDeck));

            Assert.IsTrue(Same(byTheRule, atTheWheel),
                $"{hullName}: at the wheel her cutaway asks for {Describe(atTheWheel)}; the wheel's room is {Describe(byTheRule)}");
            Assert.IsFalse(cutaway.RequestedCut.Opens, $"{hullName}: giving the wheel up, on deck, left her open");

            Debug.Log($"[helm-room] {hullName}: station ({station.x:0.00}, {station.y:0.00}, {station.z:0.00}) -> " +
                      (byTheRule.Opens ? $"room '{room}', {Describe(byTheRule)}" : "the open deck, nothing cut") +
                      $"; leaving the wheel stands her in {(stoodInId != null ? $"room '{stoodInId}'" : "no room")}");
        }

        // =====================================================================================
        //  THE FINDINGS, PINNED
        // =====================================================================================

        [TestCaseSource(nameof(WheelhouseHulls))]
        public void TheCapeAndEveryLobsterVariant_SteerFromTheWheelhouse_AndItIsTheCutTheDoorOpens(string hullName)
        {
            BoatHullDef hull = Load(hullName);
            HullMeshDef.Cut cut = HelmRoomOf(hull, out string room);

            Assert.AreEqual(WheelhouseLevelId, room, $"{hullName}: her wheel stands in the wheelhouse");
            Assert.IsTrue(cut.Opens);
            Assert.IsTrue(Same(hull.Visual.HullMesh.CutawayForDeck(WheelhouseLevelId), cut),
                $"{hullName}: the cut at the wheel is the one walking into the wheelhouse opens");
        }

        [TestCaseSource(nameof(PinnedRooms))]
        public void HerWheelStandsInTheRoomTheCensusFound(string hullName)
        {
            BoatHullDef hull = Load(hullName);
            Assert.IsNotNull(hull, $"no hull asset named {hullName} under {BoatsFolder}: retire or rename her pin");
            HullMeshDef.Cut cut = HelmRoomOf(hull, out string room);

            Assert.AreEqual(RoomAtTheWheel[hullName], room,
                $"{hullName}: measured 2026-11-14, her wheel stood in '{RoomAtTheWheel[hullName]}'. If her art moved " +
                "the wheel, rewrite this pin, do not delete it.");
            Assert.IsTrue(cut.Opens, $"{hullName}: and that room opens");
        }

        [TestCaseSource(nameof(WheelInTheOpen))]
        public void AWheelOnAFlybridge_CutsNothing(string hullName)
        {
            BoatHullDef hull = Load(hullName);
            Assert.IsNotNull(hull, $"no hull asset named {hullName} under {BoatsFolder}: retire or rename her pin");
            Assert.IsTrue(hull.Visual.Deck.HasHelmStation, $"{hullName}: premise, her rig measures a helm station");

            HullMeshDef.Cut cut = HelmRoomOf(hull, out string room);

            Assert.IsFalse(cut.Opens, $"{hullName}: her wheel is in the open and the rule opens room '{room}'");
        }

        [TestCaseSource(nameof(NoMeasuredStation))]
        public void AHullWhoseArtNamesNoHelmStation_IsADebt_NotAGuess(string hullName)
        {
            BoatHullDef hull = Load(hullName);
            Assert.IsNotNull(hull, $"no hull asset named {hullName} under {BoatsFolder}: retire or rename her entry");
            Assert.IsFalse(hull.Visual.Deck.HasHelmStation,
                $"{hullName} now measures a helm station: retire her from {nameof(NoMeasuredStation)} and pin the room " +
                $"her wheel stands in ({nameof(RoomAtTheWheel)}), so her house opens at the wheel and stays proven.");
        }

        // =====================================================================================
        //  HELPERS
        // =====================================================================================

        private static HullMeshDef.Cut HelmRoomOf(BoatHullDef hull, out string room)
        {
            BoatVisualDef visual = hull.Visual;
            room = null;
            if (!visual.Deck.HasHelmStation) return HullMeshDef.Cut.None;
            return BoatHelmRoom.CutAt(visual.Interior, visual.HasHullMesh() ? visual.HullMesh : null,
                                      visual.Deck.HelmStationLocalMeters, out room);
        }

        /// <summary>Does her level table carry a row for this deck id and declare it open?</summary>
        private static bool DeclaredOpen(HullMeshDef mesh, string deckId)
        {
            foreach (HullMeshDef.LevelTag row in mesh.LevelTags)
                if (row.DeckId == deckId) return !row.Enclosed;
            return false;
        }

        private static bool Same(HullMeshDef.Cut a, HullMeshDef.Cut b)
        {
            if (a.Level != b.Level || a.Lid != b.Lid || a.LiftLevels != b.LiftLevels || a.ShownRoles != b.ShownRoles)
                return false;
            int[] am = a.RoleMasks ?? System.Array.Empty<int>(), bm = b.RoleMasks ?? System.Array.Empty<int>();
            return am.SequenceEqual(bm);
        }

        private static string Describe(HullMeshDef.Cut cut)
            => cut.Opens
                ? $"[level {cut.Level}, lid {cut.Lid}, lift {cut.LiftLevels}, roles {cut.ShownRoles}]"
                : "[nothing]";

        private static BoatHullDef Load(string hullName)
            => EveryInteriorHullReachesItsDoorFromItsStandTests.LoadInteriorHull(hullName);

        private static bool IsLobsterVariant(string hullName)
            => LobsterVariantStems.Any(stem => hullName.StartsWith(stem, System.StringComparison.Ordinal));

        private static IEnumerable<string> WheelhouseHulls()
            => InteriorHulls().Where(h => h == CapeIslander || IsLobsterVariant(h));

        private static IEnumerable<string> PinnedRooms() => RoomAtTheWheel.Keys;

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
