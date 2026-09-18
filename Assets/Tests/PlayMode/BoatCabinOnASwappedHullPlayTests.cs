using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The cabin the owner plays is a hull swapped onto a dory root.</b> Owner playtest 2026-09-17:
    /// "after i board it and press e at an interactable door, i dont see the doors open and i dont see
    /// the interior cutaway when im inside an interior".
    ///
    /// <para>No scene places a cape islander or a lobster boat. Every boat root in St Peters wears
    /// <c>boat.dory</c>, and the playtest hull arrives later, through <see cref="DevBoatPicker"/> or
    /// OwnedFleet, onto a root whose <see cref="BoatInteriorInstaller"/> built the dory's answer at
    /// Start: no cabin, no door, no cutaway. <see cref="BoatCabinJourneyPlayTests"/> skins her lobster
    /// before the installer ever looks, the one order that always worked. So these fixtures build the
    /// dory root, let her Start run, and swap through <see cref="DevBoatPicker.Show"/> itself. The
    /// picker's key listens only at the helm, but <c>Show</c> is the whole swap and never reads where
    /// she stands, so she is swapped under while standing on deck, where the deck walk has already
    /// looked for a doorway on the dory and found none.</para>
    ///
    /// <para>No <c>ConfigureHelm</c> anywhere: the switcher keeps its shipped 0.5 m helm radius, so the
    /// owner's second ruling ("only open the door, you should need to be within a small radius of the
    /// helm to push e and take control") is played here, not configured away.</para>
    /// </summary>
    public sealed class BoatCabinOnASwappedHullPlayTests
    {
        private const string BoatsFolder = "Assets/_Project/Data/Boats";
        private const string DoryName = "Dory";
        private const string LeafChildName = "DoorLeaf";

        private readonly List<UnityEngine.Object> _spawned = new();
        private readonly List<CabinEntered> _entered = new();
        private readonly List<CabinLeft> _left = new();

        [SetUp]
        public void SetUp()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            InteractOffer.Reset();
            InteractionGate.Reset();
            BoatInteriorCells.Reset();

            // Clear() unsubscribes every listener on the channel, so it runs before anything here exists.
            _entered.Clear();
            _left.Clear();
            EventBus.Clear<CabinEntered>();
            EventBus.Clear<CabinLeft>();
            EventBus.Subscribe<CabinEntered>(_entered.Add);
            EventBus.Subscribe<CabinLeft>(_left.Add);

            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.InteriorRockScale = GameConfig.DefaultInteriorRockScale;
            _spawned.Add(config);
            GameServices.Config = config;

            // A listener-less play scene logs a warning every frame.
            Spawn("Listener").AddComponent<AudioListener>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<CabinEntered>(_entered.Add);
            EventBus.Unsubscribe<CabinLeft>(_left.Add);
            Interactables.Clear();
            InteractVerb.Reset();
            InteractOffer.Reset();
            InteractionGate.Reset();
            GameServices.Config = null;

            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) UnityEngine.Object.Destroy(_spawned[i]);
            _spawned.Clear();

            BoatInteriorCells.Reset();
        }

        // =====================================================================================
        //  THE PRESS, THE BAND AND THE CUT, on a hull that arrived after Start
        // =====================================================================================

        [UnityTest]
        public IEnumerator SwappedOnUnderHer_EOpensHerDoor_AndHerHouseIsCutOpenOnlyWhileSheIsBelow(
            [Values("CapeIslander", "LobsterBoat")] string subjectName)
        {
            Rig rig = NewDoryRig();
            BoatHullDef subject = LoadHull(subjectName);
            if (rig.Boat == null || subject == null) yield break;   // editor-only; LoadCommitted Ignored
            yield return null;

            Assert.IsFalse(rig.Installer.Built,
                           "precondition: the dory root's installer has looked, and built nothing");

            yield return Board(rig);
            rig.Picker.Show(subject);
            yield return null;   // the outgoing skin's leftovers go at the end of the swap's frame

            BoatCabinDoor door = SwappedCabinDoor(rig, subject);
            BoatInterior cabin = rig.Installer.Interior;
            BoatCutaway cutaway = rig.Installer.Cutaway;
            Assert.IsNotNull(cutaway, $"{subject.Id} is a mesh hull, so her cutaway comes with her cabin");
            Assert.IsFalse(cutaway.RequestedCut.Opens, "nobody is below yet, so nothing is cut");
            Assert.AreEqual(ControlMode.OnDeck, rig.Switcher.Mode, "premise: the swap left her standing on deck");

            // ---- E at her door: the door, and only the door ------------------------------------
            yield return WalkToTheDoor(rig, door);

            Assert.IsTrue(InteractOffer.Current.Has,
                          $"standing at {subject.Id}'s threshold ({Vector2.Distance(rig.Player.transform.position, door.transform.position):0.00} m " +
                          $"from a door with {door.ReachMeters:0.00} m of reach) must offer the way in");
            Assert.AreEqual("Open the door", InteractOffer.Current.Label);

            Assert.IsTrue(rig.Switcher.BeginInteract(), "E at her door is spent on her door");
            Assert.AreEqual(ControlMode.OnDeck, rig.Switcher.Mode,
                            "…and never on her helm: at a cabin door E only opens the door (owner, 2026-09-17)");
            Assert.IsTrue(door.IsCueing, "the press starts the leaf moving");
            yield return WaitForCue(door);

            Assert.IsTrue(door.IsOpen, "the cue ends on a leaf standing open");
            Assert.IsFalse(cabin.IsInside, "opening a door is not walking through it (the 2026-08-28 ruling)");
            Assert.AreEqual(0, _entered.Count, "…so nothing has been entered yet");
            Assert.IsTrue(HullOf(rig).DoorLeafShownOpen,
                          "the door told the renderer the swap skinned on that her leaf stands open");

            // ---- through the open band, on the shipped deck walk --------------------------------
            yield return SheWalksThroughTheDoorway(rig, door);

            Assert.IsTrue(cabin.IsInside, "walking the open band takes her below, with no second press");
            Assert.AreEqual(1, _entered.Count, "CabinEntered is published, once");
            Assert.IsTrue(cutaway.RequestedCut.Opens,
                          $"below decks on {subject.Id}, her cutaway must ask for a cut (it asked for none)");
            Assert.IsTrue(HullOf(rig).CutawayShown.Opens, "…and the hull the swap skinned on draws it");

            // ---- and back out, the same way ----------------------------------------------------
            yield return SheWalksThroughTheDoorway(rig, door);

            Assert.IsFalse(cabin.IsInside, "walking the band again brings her out");
            Assert.AreEqual(1, _left.Count, "CabinLeft is published, once");
            Assert.IsFalse(cutaway.RequestedCut.Opens, "…and her cutaway stops asking for a cut");
            yield return null;
            Assert.IsFalse(HullOf(rig).CutawayShown.Opens, "her house closes behind her");
        }

        // =====================================================================================
        //  THE LEAF, drawn in the pose her door is in
        // =====================================================================================

        [UnityTest]
        public IEnumerator SwappedOnUnderHer_HerDoorLeafIsDrawnInThePoseHerDoorIsIn(
            [Values("CapeIslander", "LobsterBoat")] string subjectName)
        {
            Rig rig = NewDoryRig();
            BoatHullDef subject = LoadHull(subjectName);
            if (rig.Boat == null || subject == null) yield break;
            yield return null;

            yield return Board(rig);
            rig.Picker.Show(subject);
            yield return null;

            BoatCabinDoor door = SwappedCabinDoor(rig, subject);
            HullMeshDef mesh = subject.Visual.HullMesh;
            Assert.IsTrue(mesh.HasDoorLeaf(),
                          $"{mesh.name} carries no split door leaf, so it was baked before the split: her door " +
                          "opens with nothing drawn moving. Re-bake it with RigMeshAssetBaker.");

            Assert.AreSame(mesh.DoorLeafClosed, LeafMeshOf(HullOf(rig)),
                           "a door nobody has pressed is drawn shut: the doorOpen 0 pose");

            yield return WalkToTheDoor(rig, door);
            Assert.IsTrue(rig.Switcher.BeginInteract(), "E at her door is spent on her door");
            Assert.AreSame(mesh.DoorLeafClosed, LeafMeshOf(HullOf(rig)),
                           "the leaf lands at the END of the cue, not at the press");
            yield return WaitForCue(door);

            Assert.IsTrue(door.IsOpen, "precondition: the cue ended on a door standing open");
            Assert.AreSame(mesh.DoorLeafOpen, LeafMeshOf(HullOf(rig)),
                           "…and her leaf is drawn open: the doorOpen 1 pose");

            Assert.AreEqual("Close the door", door.VerbLabel, "the press now names the leaf");
            Assert.IsTrue(rig.Switcher.BeginInteract(), "a second E at her door closes it");
            yield return WaitForCue(door);

            Assert.IsFalse(door.IsOpen, "precondition: the cue ended on a door shut again");
            Assert.AreSame(mesh.DoorLeafClosed, LeafMeshOf(HullOf(rig)), "…and her leaf is drawn shut again");
        }

        // =====================================================================================
        //  THE NEXT SWAP: nothing of the old cabin stays behind
        // =====================================================================================

        [UnityTest]
        public IEnumerator ASwapWhileSheIsBelow_LetsHerOut_AndLeavesOnlyTheNewHullsCabinStanding()
        {
            Rig rig = NewDoryRig();
            BoatHullDef cape = LoadHull("CapeIslander");
            BoatHullDef lobster = LoadHull("LobsterBoat");
            BoatHullDef dory = LoadHull(DoryName);
            if (rig.Boat == null || cape == null || lobster == null || dory == null) yield break;
            yield return null;

            yield return Board(rig);
            rig.Picker.Show(cape);
            yield return null;

            BoatCabinDoor capeDoor = SwappedCabinDoor(rig, cape);
            yield return WalkToTheDoor(rig, capeDoor);
            Assert.IsTrue(rig.Switcher.BeginInteract(), "E at her door is spent on her door");
            yield return WaitForCue(capeDoor);
            yield return SheWalksThroughTheDoorway(rig, capeDoor);
            Assert.IsTrue(rig.Installer.Interior.IsInside, "precondition: she is below on the cape islander");
            Assert.AreEqual(0, _left.Count, "precondition: she has not left it");

            // ---- a new hull under her while she is below ---------------------------------------
            rig.Picker.Show(lobster);
            Assert.AreEqual(1, _left.Count,
                            "the cape islander's cabin lets her out as it goes, so nothing below decks is " +
                            "left believing she is still inside a room that no longer exists");
            yield return null;

            SwappedCabinDoor(rig, lobster);
            Assert.IsFalse(rig.Installer.Interior.IsInside, "the lobster boat's cabin is new, and nobody is in it");
            Assert.IsFalse(rig.Installer.Cutaway.RequestedCut.Opens, "…so nothing is cut");
            Assert.IsFalse(HullOf(rig).CutawayShown.Opens, "…and the hull the swap skinned on draws her house whole");
            AssertCabinSetsStanding(rig, 1, lobster.Id);

            // ---- and back to the dory, who has no cabin -----------------------------------------
            rig.Picker.Show(dory);
            yield return null;

            Assert.IsFalse(rig.Installer.Built, "the dory has no cabin, so her root carries none");
            Assert.IsNull(rig.Installer.Door, "…and no door");
            Assert.IsNull(rig.Installer.Cutaway, "…and no cutaway");
            AssertCabinSetsStanding(rig, 0, dory.Id);
            Assert.AreEqual(1, _entered.Count, "only the cape islander's cabin was ever entered");
            Assert.AreEqual(1, _left.Count, "nobody was below for the last swap, so it publishes nothing");
        }

        // =====================================================================================
        //  RIG + HELPERS
        // =====================================================================================

        private struct Rig
        {
            public GameObject Player;
            public ControlSwitcher Switcher;
            public GameObject Boat;
            public BoatController BoatController;
            public DevBoatPicker Picker;
            public BoatInteriorInstaller Installer;
        }

        private GameObject Spawn(string name, Vector3 position = default)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawned.Add(go);
            return go;
        }

        /// <summary>A St Peters boat root: a dory, skinned the way the scene skins her, with the picker on.</summary>
        private Rig NewDoryRig()
        {
            BoatHullDef dory = LoadHull(DoryName);
            if (dory == null) return default;

            var boatGo = Spawn(DoryName);
            var boat = boatGo.AddComponent<BoatController>();     // Awake mounts the interior installer
            var input = boatGo.AddComponent<DevBoatInput>();
            var baseRenderer = boatGo.AddComponent<SpriteRenderer>();
            boat.SetHull(dory);

            var picker = boatGo.AddComponent<DevBoatPicker>();
            picker.Configure(new[] { dory }, boat, null, baseRenderer);
            BoatHullSkinner.ApplyHull(boatGo, baseRenderer, dory, boat);

            var installer = boatGo.GetComponent<BoatInteriorInstaller>();
            Assert.IsNotNull(installer, "BoatController.Awake should have mounted the installer itself");

            var playerGo = Spawn("Player", new Vector3(1.2f, 0f, 0f));   // the journey's reach from the boat
            var walk = playerGo.AddComponent<PlayerWalkController>();   // adds Rigidbody2D + SpriteRenderer
            var body = playerGo.GetComponent<SpriteRenderer>();
            var deckWalk = playerGo.AddComponent<DeckWalkController>();
            deckWalk.enabled = false;
            var character = playerGo.AddComponent<IsoCharacterSprite>();

            var riderGo = new GameObject("DeckRider");
            riderGo.transform.SetParent(playerGo.transform, false);
            var riderSr = riderGo.AddComponent<SpriteRenderer>();
            riderSr.enabled = false;
            var rider = playerGo.AddComponent<DeckRiderVisual>();
            rider.Configure(riderSr, body, character);

            walk.enabled = true; boat.enabled = false; input.enabled = false;   // on-foot start

            var sw = Spawn("Switcher").AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, null, zoneRadius: 6f, disembarkPoint: null);

            return new Rig
            {
                Player = playerGo, Switcher = sw, Boat = boatGo, BoatController = boat,
                Picker = picker, Installer = installer,
            };
        }

        /// <summary>The swap landed, and it brought <paramref name="hull"/>'s own cabin and door with it.</summary>
        private static BoatCabinDoor SwappedCabinDoor(Rig rig, BoatHullDef hull)
        {
            Assert.AreSame(hull, rig.BoatController.Hull, $"the picker put {hull.Id} under her");
            Assert.IsTrue(rig.Installer.Built,
                          $"{hull.Id} swapped onto a root whose installer had already built must grow her cabin");
            Assert.AreSame(hull.Visual.Interior, rig.Installer.Interior.Def, $"…and it is {hull.Id}'s own cabin");
            Assert.IsNotNull(rig.Installer.Door, $"…with {hull.Id}'s door at her measured threshold");
            return rig.Installer.Door;
        }

        private static void AssertCabinSetsStanding(Rig rig, int expected, string hullId)
        {
            Assert.AreEqual(expected, rig.Boat.GetComponentsInChildren<BoatCabinDoor>(true).Length,
                            $"doors standing on the root under {hullId}: a swap tears the old door down");
            Assert.AreEqual(expected, rig.Boat.GetComponentsInChildren<BoatInterior>(true).Length,
                            $"cabins standing on the root under {hullId}");
            Assert.AreEqual(expected, rig.Boat.GetComponentsInChildren<BoatCutaway>(true).Length,
                            $"cutaways standing on the root under {hullId}: a stale one still answers CabinEntered");
        }

        private IEnumerator Board(Rig rig)
        {
            Assert.IsTrue(rig.Switcher.BeginInteract(), "E within reach must start the boarding");
            yield return WaitUntil(() => rig.Switcher.Mode == ControlMode.OnDeck, 5f,
                                   "the boarding move to land her on the deck");
            yield return null;
        }

        private static IEnumerator WalkToTheDoor(Rig rig, BoatCabinDoor door)
        {
            rig.Player.transform.position = door.transform.position;
            yield return null;
        }

        private static IEnumerator SheWalksThroughTheDoorway(Rig rig, BoatCabinDoor door)
        {
            var walk = rig.Player.GetComponent<DeckWalkController>();
            Assert.IsNotNull(walk, "the shipped deck walk is what carries her through a doorway");

            Vector2 doorway = BoatCabinThreshold.PointOf(door.Door);
            Vector2 clear = doorway + Vector2.right *
                            (BoatCabinThreshold.ReleaseRadiusMetres(door.Door) + 1f);

            walk.SnapToDeckLocal(clear);
            yield return null;
            walk.SnapToDeckLocal(doorway);
            yield return null;
        }

        private static IEnumerator WaitForCue(BoatCabinDoor door)
        {
            float deadline = Time.time + door.CueSeconds + 1f;
            while (door.IsCueing && Time.time < deadline) yield return null;
            Assert.IsFalse(door.IsCueing, "the leaf finished moving within its own declared duration");
            yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> done, float seconds, string what)
        {
            float deadline = Time.time + seconds;
            while (!done() && Time.time < deadline) yield return null;
            Assert.IsTrue(done(), $"timed out after {seconds:0.0}s waiting for {what}");
        }

        /// <summary>The facet renderer on her skin's visual child, which a swap re-configures in place.</summary>
        private static IsoFacetHullRenderer HullOf(Rig rig)
        {
            Transform visual = rig.Boat.transform.Find(BoatHullSkinner.VisualChildName);
            Assert.IsNotNull(visual, "a mesh hull wears her skin on the visual child");
            var hull = visual.GetComponent<IsoFacetHullRenderer>();
            Assert.IsNotNull(hull, "…and the facet renderer draws her there");
            return hull;
        }

        private static Mesh LeafMeshOf(IsoFacetHullRenderer hull)
        {
            Transform posed = hull.PosedMesh;
            Transform leaf = posed != null ? posed.Find(LeafChildName) : null;
            Assert.IsNotNull(leaf, "a hull with a split door leaf draws it on a 'DoorLeaf' child of her posed mesh");
            return leaf.GetComponent<MeshFilter>().sharedMesh;
        }

        private static BoatHullDef LoadHull(string name) => LoadCommitted<BoatHullDef>($"{BoatsFolder}/{name}.asset");

        private static T LoadCommitted<T>(string path) where T : ScriptableObject
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"missing {path}");
            return asset;
#else
            Assert.Ignore("Needs the AssetDatabase: these journeys swap onto the REAL committed hulls.");
            return null;
#endif
        }
    }
}
