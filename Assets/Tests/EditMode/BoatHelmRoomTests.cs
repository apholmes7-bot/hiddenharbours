using System.Collections.Generic;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The room a wheel stands in, and the cut the house shows at the wheel</b> (owner ruling
    /// 2026-11-14): walking inside an enclosed level cuts that level; at a wheel that stands inside an
    /// enclosed room that ONE room is cut and no other level; at a wheel on an open deck or a flybridge
    /// nothing is cut; and one GameConfig switch gives the closed house at the wheel back.
    ///
    /// <para>Two halves, both on hulls built here by hand with every expected value written out:
    /// <see cref="BoatHelmRoom"/> as a pure rule over a level table, and <see cref="BoatCutaway"/> asking
    /// it when the helm changes hands. The shipped fleet is measured by
    /// <see cref="EveryHullOpensTheRoomHerWheelStandsInTests"/>.</para>
    /// </summary>
    public sealed class BoatHelmRoomTests
    {
        private const string HullWalk = "hidden-harbours/hull-walk@1";

        private const int HouseTag = 3, CuddyTag = 4, CockpitTag = 1, ForedeckTag = 2, FlybridgeTag = 5;
        private const float HouseSole = 0.5f, HouseCeiling = 2.9f;
        private const float CuddySole = 0.24f, CuddyCeiling = 2.014f;
        private const float ForedeckSole = 2.124f;

        /// <summary>The house's outline in plan: x −1..1, y 0..2. The cockpit lies aft of it, y −2..0.</summary>
        private static readonly Vector2 InTheHouse = new Vector2(0f, 1f);
        private static readonly Vector2 InTheCockpit = new Vector2(0f, -1f);

        private readonly List<Object> _made = new List<Object>();
        private GameConfig _configBefore;

        [SetUp]
        public void SetUp()
        {
            ClearChannels();
            GameServices.Helm.Reset();
            _configBefore = GameServices.Config;
            GameServices.Config = null;     // every case states the switch it runs under, or runs on the ruled default
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = _configBefore;
            GameServices.Helm.Reset();
            ClearChannels();
            for (int i = _made.Count - 1; i >= 0; i--) if (_made[i] != null) Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        // =====================================================================================
        //  THE RULE — which room holds a station
        // =====================================================================================

        [Test]
        public void AStationOverARoomsSoleAndUnderItsCeiling_StandsInThatRoom()
        {
            HullMeshDef mesh = WheelhouseBoat();
            BoatInteriorDef def = WheelhouseDef();

            HullMeshDef.Cut cut = BoatHelmRoom.CutAt(def, mesh, At(InTheHouse, HouseSole + 0.3f), out string room);

            Assert.AreEqual("house_sole", room);
            Assert.AreEqual(HouseTag, cut.Level, "the house's own tag: the cut the door would open");
            Assert.AreEqual(0, cut.Lid, "and nothing else comes off with it");
        }

        [Test]
        public void TheSameHeightOutOnTheCockpit_IsTheOpenDeck()
        {
            // The cockpit runs aft at the house sole's own height: heights cannot tell them apart, the
            // outline does.
            HullMeshDef.Cut cut = BoatHelmRoom.CutAt(WheelhouseDef(), WheelhouseBoat(), At(InTheCockpit, HouseSole),
                                                     out string room);

            Assert.IsFalse(cut.Opens, "a wheel in the cockpit cuts nothing");
            Assert.IsNull(room);
        }

        [Test]
        public void ARoomThatTiesAnOpenDeckAndHasNoOutline_LosesTheTie()
        {
            // A room only the mesh knows has no outline to settle a tie with; the house stays whole.
            HullMeshDef mesh = Mesh(null, null,
                Room("house", "helm_deck", HouseTag, HouseSole, HouseCeiling),
                Open("cockpit", "cockpit", CockpitTag, HouseSole));

            Assert.IsFalse(BoatHelmRoom.CutAt(null, mesh, At(InTheHouse, HouseSole)).Opens);
        }

        [Test]
        public void AStationOnAFlybridgeOverTheHouse_IsTheOpenDeck_EvenUnderAnOverstatedCeiling()
        {
            const float flybridgeSole = 3f;
            HullMeshDef mesh = Mesh(null, null,
                Room("house", "house_sole", HouseTag, HouseSole, flybridgeSole + 1f),   // a ceiling that lies
                Open("flybridge", "flybridge", FlybridgeTag, flybridgeSole));
            BoatInteriorDef def = Def(Level("house_sole", HouseSole));

            Assert.IsFalse(BoatHelmRoom.CutAt(def, mesh, At(InTheHouse, flybridgeSole)).Opens,
                "an open deck between the room's sole and the station is the floor she stands on");
            Assert.IsTrue(BoatHelmRoom.CutAt(def, mesh, At(InTheHouse, HouseSole + 0.2f)).Opens,
                "premise: under the flybridge she is in the house");
        }

        [Test]
        public void AStationOverTheCeiling_OrUnderTheSole_IsNotInTheRoom()
        {
            HullMeshDef mesh = Mesh(null, null, Room("house", "house_sole", HouseTag, HouseSole, HouseCeiling));
            BoatInteriorDef def = Def(Level("house_sole", HouseSole));
            float tolerance = def.FloorTolerance;

            Assert.IsFalse(BoatHelmRoom.CutAt(def, mesh, At(InTheHouse, HouseCeiling)).Opens, "at the ceiling is on the roof");
            Assert.IsFalse(BoatHelmRoom.CutAt(def, mesh, At(InTheHouse, HouseSole - tolerance * 2f)).Opens, "under the sole");
            Assert.IsTrue(BoatHelmRoom.CutAt(def, mesh, At(InTheHouse, HouseSole - tolerance * 0.5f)).Opens,
                "a station a hair under the sole is still that floor");
        }

        [Test]
        public void ARoomWhoseTableGivesNoCeiling_HoldsOnlyAStationOnItsSole()
        {
            HullMeshDef mesh = Mesh(null, null, Room("house", "house_sole", HouseTag, HouseSole, 0f));
            BoatInteriorDef def = Def(Level("house_sole", HouseSole));

            Assert.IsTrue(BoatHelmRoom.CutAt(def, mesh, At(InTheHouse, HouseSole)).Opens);
            Assert.IsFalse(BoatHelmRoom.CutAt(def, mesh, At(InTheHouse, HouseSole + def.FloorTolerance * 2f)).Opens,
                "with no ceiling to stand under, over the sole is over the room");
        }

        [Test]
        public void OfTwoRoomsThatHoldHer_TheHigherSoleIsTheFloorUnderHer()
        {
            const float bridgeSole = 3f, top = 5.5f;
            const int bridgeTag = 6;
            HullMeshDef mesh = Mesh(null, null,
                Room("house", "house_sole", HouseTag, HouseSole, top),      // a ceiling that runs through the bridge
                Room("bridge", "bridge_sole", bridgeTag, bridgeSole, top));
            BoatInteriorDef def = Def(Level("house_sole", HouseSole), Level("bridge_sole", bridgeSole));

            HullMeshDef.Cut cut = BoatHelmRoom.CutAt(def, mesh, At(InTheHouse, bridgeSole + 0.2f), out string room);

            Assert.AreEqual("bridge_sole", room);
            Assert.AreEqual(bridgeTag, cut.Level);
        }

        [Test]
        public void ARoomOnlyTheMeshKnows_OpensByTheIdItsLiftSetIsKeyedBy()
        {
            // The helm deck of a cruiser: a room in the art, not a floor of the walk. Its row's deck id
            // keys no lift set; its level id does.
            const int houseLift = 128, cabinLift = 196;
            const float helmDeckSole = 1.17f, helmDeckCeiling = 3f, cabinSole = 0.22f, cabinCeiling = 1.1f;
            HullMeshDef mesh = Mesh(HullWalk,
                new[] { Lift("house", "house", houseLift), Lift("below", "cabin", cabinLift, CuddyTag) },
                Room("house", "helm_deck", HouseTag, helmDeckSole, helmDeckCeiling),
                Room("below", "cabin", CuddyTag, cabinSole, cabinCeiling));
            BoatInteriorDef def = Def(Level("cabin", cabinSole));

            HullMeshDef.Cut cut = BoatHelmRoom.CutAt(def, mesh, At(InTheHouse, helmDeckSole + 0.5f), out string room);

            Assert.AreEqual("house", room, "the id the mesh keys that room's cut by");
            Assert.AreEqual(houseLift, cut.LiftLevels);
            Assert.AreNotEqual(cabinLift, cut.LiftLevels, "the cabin under her stays shut");
        }

        [Test]
        public void ASecondFloorOfOneSpace_IsRoofedByTheRowItsLiftSetNames()
        {
            // A raised helm that shares the saloon's space: the def names it, the mesh has no row for it,
            // and its lift set's key names the row that roofs it.
            const int saloonLift = 64;
            const float saloonSole = 2.2f, helmSole = 3f, ceiling = 5f;
            HullMeshDef mesh = Mesh(HullWalk,
                new[] { Lift("saloon", "saloon_sole", saloonLift), Lift("saloon", "dining_helm", saloonLift) },
                Room("saloon", "saloon_sole", HouseTag, saloonSole, ceiling));
            BoatInteriorDef def = Def(Level("saloon_sole", saloonSole), Level("dining_helm", helmSole));

            HullMeshDef.Cut cut = BoatHelmRoom.CutAt(def, mesh, At(InTheHouse, helmSole + 0.15f), out string room);

            Assert.AreEqual("dining_helm", room, "the higher floor under the shared ceiling");
            Assert.AreEqual(saloonLift, cut.LiftLevels);
        }

        [Test]
        public void NoMesh_OrNoLevelTable_IsNoRoom()
        {
            Assert.IsFalse(BoatHelmRoom.CutAt(WheelhouseDef(), null, At(InTheHouse, HouseSole)).Opens);
            HullMeshDef bare = Mesh(null, null);
            bare.LevelTags = null;
            Assert.IsFalse(BoatHelmRoom.CutAt(WheelhouseDef(), bare, At(InTheHouse, HouseSole)).Opens);
        }

        // =====================================================================================
        //  THE CUTAWAY — what the house shows when the helm changes hands
        // =====================================================================================

        [Test]
        public void TakingAWheelThatStandsInTheHouse_OpensTheHouse_ThoughNobodyCameThroughADoor()
        {
            Rig hull = BuildHull(At(InTheHouse, HouseSole + 0.24f));
            Assert.IsFalse(hull.Renderer.Cut.Opens, "premise: on deck she is drawn whole");

            TakeTheHelm(hull);

            Assert.AreEqual(HouseTag, hull.Renderer.Cut.Level, "the room her wheel stands in is cut");
            Assert.AreEqual(0, hull.Renderer.Cut.Lid, "and no other level");
            Assert.IsFalse(hull.Cutaway.OccupantIsBelow, "nothing entered her: the cut is the wheel's, not a door's");

            LeaveTheHelm();
            Assert.IsFalse(hull.Renderer.Cut.Opens, "back on deck, never having been counted inside: whole again");
        }

        [Test]
        public void BelowInTheCuddy_ThenAtTheWheel_OnlyTheWheelsRoomIsOpen()
        {
            Rig hull = BuildHull(At(InTheHouse, HouseSole + 0.24f));
            EventBus.Publish(new CabinEntered(hull.HullId, 1));     // def index 1 = the cuddy
            Assert.AreEqual(CuddyTag, hull.Renderer.Cut.Level, "premise: below, her own level is cut");
            Assert.AreEqual(ForedeckTag, hull.Renderer.Cut.Lid);

            TakeTheHelm(hull);
            Assert.AreEqual(HouseTag, hull.Renderer.Cut.Level, "at the wheel the cut is the wheel's room");
            Assert.AreEqual(0, hull.Renderer.Cut.Lid, "one room, never two: the cuddy's lid is back on");
            Assert.IsTrue(hull.Cutaway.OccupantIsBelow, "taking the wheel moved nobody");

            LeaveTheHelm();
            Assert.AreEqual(CuddyTag, hull.Renderer.Cut.Level, "giving the wheel up gives her own level back");
        }

        [Test]
        public void WithTheOwnersSwitchOff_TheHouseIsClosedAtTheWheel_AsItWasBefore()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _made.Add(config);
            config.CutTheHelmRoomAtTheWheel = false;
            GameServices.Config = config;

            Rig hull = BuildHull(At(InTheHouse, HouseSole + 0.24f));
            EventBus.Publish(new CabinEntered(hull.HullId, 0));
            Assert.AreEqual(HouseTag, hull.Renderer.Cut.Level, "walking inside cuts the level, switch or no switch");

            TakeTheHelm(hull);
            Assert.IsFalse(hull.Renderer.Cut.Opens, "switch off: exterior only at the wheel");
            Assert.AreEqual(HouseTag, hull.Cutaway.HelmRoomCut.Level, "the room is still known; it is the switch that refuses");

            LeaveTheHelm();
            Assert.AreEqual(HouseTag, hull.Renderer.Cut.Level, "and stepping back opens her again");

            config.CutTheHelmRoomAtTheWheel = true;
            TakeTheHelm(hull);
            Assert.AreEqual(HouseTag, hull.Renderer.Cut.Level, "the switch is read each time the helm changes hands");
        }

        [Test]
        public void TheSwitchDefaultsOn_InAFreshConfigAndWithNoneWired()
        {
            Assert.IsTrue(GameConfig.DefaultCutTheHelmRoomAtTheWheel, "ruled 2026-11-14: default ON");
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _made.Add(config);
            Assert.IsTrue(config.CutTheHelmRoomAtTheWheel);
        }

        [Test]
        public void AWheelInTheCockpit_CutsNothing()
        {
            Rig hull = BuildHull(At(InTheCockpit, HouseSole));

            TakeTheHelm(hull);

            Assert.IsFalse(hull.Cutaway.HelmRoomCut.Opens);
            Assert.IsFalse(hull.Renderer.Cut.Opens, "a wheel on an open deck: exterior only");
        }

        [Test]
        public void AWheelInTheCockpit_ClosesTheLevelSheWasCountedOn()
        {
            Rig hull = BuildHull(At(InTheCockpit, HouseSole));
            EventBus.Publish(new CabinEntered(hull.HullId, 1));

            TakeTheHelm(hull);

            Assert.IsFalse(hull.Renderer.Cut.Opens, "at an open-deck wheel no level opens, hers included");
        }

        [Test]
        public void AHullWhoseArtNamesNoStation_KeepsHerHouseClosedAtTheWheel()
        {
            Rig hull = BuildHull(null);
            EventBus.Publish(new CabinEntered(hull.HullId, 0));

            TakeTheHelm(hull);

            Assert.IsFalse(hull.Cutaway.HelmRoomCut.Opens, "no measured station, no room to name");
            Assert.IsFalse(hull.Renderer.Cut.Opens, "a guess would open the wrong room; she stays whole");
        }

        [Test]
        public void SomebodyElsesWheel_OpensNothingOnThisHull()
        {
            Rig mine = BuildHull(At(InTheHouse, HouseSole + 0.24f), "Mine");
            Rig theirs = BuildHull(At(InTheHouse, HouseSole + 0.24f), "Theirs");

            TakeTheHelm(theirs);

            Assert.AreEqual(HouseTag, theirs.Renderer.Cut.Level);
            Assert.IsFalse(mine.Renderer.Cut.Opens, "only the hull being steered opens at the wheel");
        }

        [Test]
        public void AHullSwappedOnUnderHerAtTheWheel_IsToldHerCutAgain_OnceHerNewSkinIsOn()
        {
            Rig hull = BuildHull(At(InTheHouse, HouseSole + 0.24f));
            TakeTheHelm(hull);
            Assert.AreEqual(HouseTag, hull.Renderer.Cut.Level, "premise");

            // The swap: the old skin goes, a new renderer comes on reset to "whole", and the swap's last
            // word is ActiveBoatChanged.
            Object.DestroyImmediate(hull.Renderer.gameObject);
            RecordingCutaway fresh = AddRenderer(hull.Root);
            Assert.IsFalse(fresh.Cut.Opens, "premise: a new skin starts whole");

            EventBus.Publish(new ActiveBoatChanged("boat.test", 10f));

            Assert.AreEqual(HouseTag, fresh.Cut.Level, "the cut is said again to the renderer that will draw it");
        }

        // =====================================================================================
        //  THE HULLS, BUILT BY HAND
        // =====================================================================================

        /// <summary>A stand-in for the facet renderer: records the cut it was last told to show.</summary>
        private sealed class RecordingCutaway : MonoBehaviour, IHullCutaway
        {
            public HullMeshDef.Cut Cut;
            public bool CarriesLevelTags => true;
            public HullMeshDef.Cut CutawayShown => Cut;
            public void ShowCutaway(HullMeshDef.Cut cut) => Cut = cut;
        }

        private sealed class Rig
        {
            public GameObject Root;
            public BoatCutaway Cutaway;
            public RecordingCutaway Renderer;
            public object HullToken;
            public EntityId HullId;
        }

        /// <summary>A wheelhouse boat with a cuddy under her foredeck and a cockpit aft at the house
        /// sole's height, her cutaway wired to a recording renderer. <paramref name="station"/> null is
        /// a hull whose art names no helm station.</summary>
        private Rig BuildHull(Vector3? station, string name = "Hull")
        {
            HullMeshDef mesh = WheelhouseBoat();
            BoatInteriorDef def = WheelhouseDef();

            var deck = ScriptableObject.CreateInstance<BoatDeckDef>();
            _made.Add(deck);
            deck.HasHelmStation = station.HasValue;
            deck.HelmStationLocalMeters = station ?? Vector3.zero;

            var root = new GameObject(name);
            _made.Add(root);
            var cabin = root.AddComponent<BoatInterior>();
            cabin.Configure(def, exterior: null, interior: null, fittings: null,
                            interiorPivot: null, boatRoot: root.transform,
                            cells: null, facings: 8, cellsAreCounterClockwise: true,
                            zeroHeadingDegrees: 0f, deckRollDegrees: 0f, deckHeavePixels: 0f,
                            deckPitchLiftMeters: 0f);
            RecordingCutaway recorder = AddRenderer(root);

            var token = new object();
            var cutaway = root.AddComponent<BoatCutaway>();
            cutaway.Configure(cabin, mesh, root.transform, token, deck);

            return new Rig
            {
                Root = root, Cutaway = cutaway, Renderer = recorder, HullToken = token, HullId = root.GetEntityId(),
            };
        }

        private static RecordingCutaway AddRenderer(GameObject root)
        {
            var go = new GameObject("FacetMesh");
            go.transform.SetParent(root.transform, false);
            return go.AddComponent<RecordingCutaway>();
        }

        // ControlSwitcher writes the piloted hull from its Mode setter and publishes the mode after;
        // this is that order.
        private static void TakeTheHelm(Rig hull)
        {
            GameServices.Helm.SetPilotedHull(hull.HullToken);
            EventBus.Publish(new ControlModeChanged(ControlMode.Aboard));
        }

        private static void LeaveTheHelm()
        {
            GameServices.Helm.SetPilotedHull(null);
            EventBus.Publish(new ControlModeChanged(ControlMode.OnDeck));
        }

        private HullMeshDef WheelhouseBoat() => Mesh(null, null,
            Room("house", "house_sole", HouseTag, HouseSole, HouseCeiling),
            new HullMeshDef.LevelTag
            {
                LevelId = "cuddy", DeckId = "cuddy_sole", Tag = CuddyTag, Enclosed = true,
                SoleZMeters = CuddySole, CeilingZMeters = CuddyCeiling, LidLevelId = "foredeck", LidTag = ForedeckTag,
            },
            Open("cockpit", "cockpit", CockpitTag, HouseSole),
            Open("foredeck", "foredeck", ForedeckTag, ForedeckSole));

        private BoatInteriorDef WheelhouseDef() => Def(Level("house_sole", HouseSole), Level("cuddy_sole", CuddySole));

        private BoatInteriorDef Def(params BoatInteriorLevel[] levels)
        {
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();
            _made.Add(def);
            def.PixelsPerMetre = 32;
            def.Levels = levels;
            return def;
        }

        private static BoatInteriorLevel Level(string id, float sole) => new BoatInteriorLevel
        {
            Id = id,
            SoleZMeters = sole,
            Outline = new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 2f), new Vector2(-1f, 2f) },
        };

        private HullMeshDef Mesh(string layoutSchema, HullMeshDef.LiftSet[] liftSets, params HullMeshDef.LevelTag[] rows)
        {
            var mesh = ScriptableObject.CreateInstance<HullMeshDef>();
            _made.Add(mesh);
            mesh.LayoutSchema = layoutSchema;
            if (liftSets != null) mesh.LiftSets = liftSets;
            mesh.LevelTags = rows;
            return mesh;
        }

        private static HullMeshDef.LiftSet Lift(string key, string deckId, int liftLevels, int level = 0) =>
            new HullMeshDef.LiftSet { Key = key, DeckId = deckId, Level = level, LiftLevels = liftLevels };

        private static HullMeshDef.LevelTag Room(string levelId, string deckId, int tag, float sole, float ceiling) =>
            new HullMeshDef.LevelTag
            {
                LevelId = levelId, DeckId = deckId, Tag = tag, Enclosed = true, SoleZMeters = sole, CeilingZMeters = ceiling,
            };

        private static HullMeshDef.LevelTag Open(string levelId, string deckId, int tag, float sole) =>
            new HullMeshDef.LevelTag { LevelId = levelId, DeckId = deckId, Tag = tag, Enclosed = false, SoleZMeters = sole };

        private static Vector3 At(Vector2 plan, float z) => new Vector3(plan.x, plan.y, z);

        private static void ClearChannels()
        {
            EventBus.Clear<CabinEntered>();
            EventBus.Clear<CabinEntryLandingEntered>();
            EventBus.Clear<CabinLeft>();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
        }
    }
}
