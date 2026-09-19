using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Tests.Support;
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
        private ISaveService _saveBefore;

        // ---- the walk from her helm (Phase B, 2026-09-19, C6) ------------------------------------------
        /// <summary>The one frame every walk is pinned to, so a run takes the same steps on any machine.</summary>
        private const float FrameSeconds = 1f / 60f;
        /// <summary>Her walking pace: the shipped value of <c>DeckWalkController._moveSpeed</c>, a private
        /// field. Only the steering's last step is scaled by it, so a drift here costs a frame, never a
        /// verdict.</summary>
        private const float HerWalkSpeed = 2.5f;
        /// <summary>A route end re-arms once she is clear of it by this many reaches: the shipped
        /// <c>DeckWalkController.RouteRearmReachMultiple</c>, a private constant.</summary>
        private const float RouteRearmMultiple = 2f;
        private const float Lookahead = 0.12f;
        private const float ArriveWithin = 0.03f;
        private const int MaxHops = 40;
        private const int HopFrameCap = 2400;
        private const int CrossingFrames = 30;
        private const int OfferFrames = 3;
        private const int StuckWindowFrames = 30;
        private const float StuckMetres = 0.02f;
        private const int StuckStrikes = 3;
        private const string OpenTheDoor = "Open the door";

        private bool _pinned;
        private float _timeScaleBefore;
        private float _captureBefore;

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

            // No save, whatever this machine holds: SaveService wires the live one before the first scene
            // loads, and a save that owns boat.dory unrepaired refuses the dory root's boarding
            // (ControlSwitcher.BoardableNow). The previous value goes back in TearDown.
            _saveBefore = GameServices.Save;
            GameServices.Save = null;

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
            GameServices.Save = _saveBefore;

            if (_pinned)
            {
                Time.timeScale = _timeScaleBefore;
                Time.captureDeltaTime = _captureBefore;
                _pinned = false;
            }

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
        //  THE REST OF THE FLEET: from her helm, through her door, on her own feet (Phase B, C6)
        // =====================================================================================

        /// <summary>
        /// ⭐ <b>Phase B, 2026-09-19, C6 — she walks from her helm through her door, on every hull the
        /// fleet report named.</b> The 2026-09-18 charter: "the cabin works on the cape and the lobster
        /// boat and on nothing else". The report's twelve hulls, F#12–F#18 and F#20–F#24, the cape and
        /// the lobster boat as the controls. Each arrives by a swap onto a dory root she stands on —
        /// never from <c>.Default</c> — and she takes her helm and leaves it. From wherever leaving it
        /// stands her she WALKS: held intents read by the shipped deck walk, one pinned frame at a time,
        /// with no teleport and no latch pre-armed. Each hop is planned from the live cabin over the floors
        /// the art measured (<see cref="CabinWalkGraph"/>) and steered over them (<see cref="CabinLegs"/>):
        /// across a deck's contacts and steps, onto a ladder or a stair where the plan takes one, E where
        /// the offer names her door, and through it, IN or OUT, with the bus hearing exactly one crossing.
        /// The owner's rulings of 2026-09-19 are what the plan finds: R2, the Convertible comes down her
        /// companionway into the house; R3, the Skybridge stands in her skylounge and takes the stairs.
        ///
        /// <para>🔴 A hull blocked on art (<see cref="CabinFleet.BlockedOnArt"/>, the list the EditMode
        /// guard reads too) runs every time, is Ignored in the fleet report's words while the block
        /// holds, and FAILS the day she walks through her door, so the entry is retired rather than
        /// left standing.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SwappedOnUnderHer_SheWalksFromTheHelmThroughHerDoor(
            [Values("CapeIslander", "LobsterBoat", "LobsterInshoreOpenNorthumberland",
                    "LobsterStandardHardtopNorthumberland", "LobsterOffshoreOpenNorthumberland",
                    "SportFisherConvertible", "SideDragger", "SportFisherSkybridge", "SternTrawler",
                    "SternTrawlerMk2", "CoastalPacket", "Tanker")] string subjectName)
        {
            Rig rig = NewDoryRig();
            BoatHullDef subject = LoadHull(subjectName);
            if (rig.Boat == null || subject == null) yield break;   // editor-only; LoadCommitted Ignored
            PinTime();
            yield return null;

            yield return Board(rig);
            var deckWalk = rig.Player.GetComponent<DeckWalkController>();
            var held = new HeldDeckIntents();
            deckWalk.ConfigureDeckInput(held);   // her feet from here on: nothing else moves her

            rig.Picker.Show(subject);
            yield return null;
            SwappedCabinDoor(rig, subject);
            Assert.AreEqual(ControlMode.OnDeck, rig.Switcher.Mode, "precondition: the swap leaves her standing on deck");

            var w = new HelmWalk
            {
                Rig = rig, Hull = subject, Cabin = rig.Installer.Interior, Deck = deckWalk, Held = held,
            };
            yield return FromTheHelm(w);
            if (!w.Failed) yield return ToHerDoor(w);
            held.Release();

            if (CabinFleet.BlockedOnArt.TryGetValue(subjectName, out string why))
            {
                if (w.Failed) Assert.Ignore($"{subjectName}: {why}\nthe walk: {w.Failure}");
                Assert.Fail($"{subjectName} WALKED from her helm through her door, and CabinFleet.BlockedOnArt " +
                            $"still names her blocked on art — retire the entry (one list, both runners):\n{w.Report}");
            }
            if (w.Failed) Assert.Fail($"{w.Failure}\nthe walk so far:\n{w.Report}");
            Assert.Greater(held.Reads, 0,
                           "the shipped deck walk read her held intents: she walked, and nothing carried her");

            Debug.Log($"[cabin walk] {subjectName}: from her helm through her door\n{w.Report}");
        }

        /// <summary>
        /// ⭐ <b>Phase B, 2026-09-19, C6 — every door her def names is built, where she can work it.</b>
        /// The Skybridge's skylounge slider was in her def and nothing built it (Phase A, C5). Swapped on
        /// under her, each hull's root carries one <see cref="BoatCabinDoor"/> per door the def names, in
        /// the def's order, each on that def entry, each under its own fixture id (the registry keys by
        /// it, and two doors of one cabin must never share one), and each reaching the whole of its band:
        /// half the DEF's clear width, read off the def here.
        /// </summary>
        [UnityTest]
        public IEnumerator SwappedOnUnderHer_EveryDoorHerDefNamesIsBuilt(
            [Values("CapeIslander", "LobsterBoat", "LobsterInshoreOpenNorthumberland",
                    "LobsterStandardHardtopNorthumberland", "LobsterOffshoreOpenNorthumberland",
                    "SportFisherConvertible", "SideDragger", "SportFisherSkybridge", "SternTrawler",
                    "SternTrawlerMk2", "CoastalPacket", "Tanker")] string subjectName)
        {
            Rig rig = NewDoryRig();
            BoatHullDef subject = LoadHull(subjectName);
            if (rig.Boat == null || subject == null) yield break;   // editor-only; LoadCommitted Ignored
            yield return null;

            yield return Board(rig);
            rig.Picker.Show(subject);
            yield return null;
            SwappedCabinDoor(rig, subject);

            BoatInteriorDef def = subject.Visual.Interior;
            var named = new List<BoatInteriorDoor>();
            if (def.Door != null) named.Add(def.Door);
            if (def.AdditionalDoors != null)
                foreach (BoatInteriorDoor extra in def.AdditionalDoors)
                    if (extra != null) named.Add(extra);

            var built = new List<BoatCabinDoor>();
            if (rig.Installer.Door != null) built.Add(rig.Installer.Door);
            built.AddRange(rig.Installer.AdditionalDoors);

            Assert.AreEqual(named.Count, built.Count,
                            $"{subjectName}: her def names {named.Count} doors and the swap built {built.Count}");
            string prefix = $"fixture.boat.{def.Id}.";
            var ids = new HashSet<string>();
            var report = new StringBuilder();
            for (int k = 0; k < named.Count; k++)
            {
                BoatInteriorDoor d = named[k];
                BoatCabinDoor b = built[k];
                string which = $"{subjectName} door {k} ('{d.Id}')";
                Assert.AreSame(d, b.Door, $"{which}: the built door works the def entry in the def's order");
                Assert.IsTrue(b.Id.StartsWith(prefix), $"{which}: its fixture id '{b.Id}' is not under '{prefix}'");
                if (!string.IsNullOrEmpty(d.Id))
                    Assert.AreEqual(prefix + d.Id, b.Id, $"{which}: a door the def names is keyed by that name");
                Assert.IsTrue(ids.Add(b.Id), $"{which}: its fixture id '{b.Id}' is already another door's");

                float band = d.ClearWidthMeters * 0.5f;
                Assert.GreaterOrEqual(b.ReachMeters, band,
                                      $"{which}: E reaches {b.ReachMeters:0.00} m, short of the {band:0.00} m band " +
                                      $"her clear width of {d.ClearWidthMeters:0.00} m opens");
                report.AppendLine($"  {b.Id}: sill {d.ThresholdPoint.ToString("F2")}, band {band:0.00} m, " +
                                  $"reach {b.ReachMeters:0.00} m");
            }
            Assert.AreEqual(named.Count, rig.Boat.GetComponentsInChildren<BoatCabinDoor>(true).Length,
                            $"{subjectName}: doors standing on the root, and no other");

            Debug.Log($"[cabin doors] {subjectName}: every door her def names is built\n{report}");
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

        // =====================================================================================
        //  THE WALKER: her own steps, planned hop by hop over the floors the art measured
        // =====================================================================================

        /// <summary>One walk from the helm: the rig, her cabin and deck walk, the planner, and what happened.
        /// The first failure is kept; everything after it is skipped, so the verdict names the first
        /// thing that went wrong.</summary>
        private sealed class HelmWalk
        {
            public Rig Rig;
            public BoatHullDef Hull;
            public BoatInterior Cabin;
            public DeckWalkController Deck;
            public HeldDeckIntents Held;
            public CabinWalkGraph Graph;
            public readonly StringBuilder Report = new StringBuilder();
            public string Failure;

            public bool Failed => Failure != null;
            public Vector2 At => Deck.DeckLocalPosition;
            public string Where => $"({At.x:0.00}, {At.y:0.00}) at {Deck.DeckHeightMeters:0.00} m";

            public void Fail(string why)
            {
                if (Failure == null) Failure = $"{Hull.Id}: {why}";
            }
        }

        /// <summary>Every frame of the walk is one 60th of a second, so a run takes the same steps on any
        /// machine; TearDown gives the clock back.</summary>
        private void PinTime()
        {
            _timeScaleBefore = Time.timeScale;
            _captureBefore = Time.captureDeltaTime;
            Time.timeScale = 1f;
            Time.captureDeltaTime = FrameSeconds;
            _pinned = true;
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        /// <summary>
        /// She takes her helm and leaves it: the stand every walk starts from. The one placement here is
        /// the setup, a skipper standing at her own wheel; from LeaveHelm on, only her steps move her.
        /// </summary>
        private static IEnumerator FromTheHelm(HelmWalk w)
        {
            ControlSwitcher sw = w.Rig.Switcher;
            // Set and pressed in ONE frame, before the deck walk's next frame writes her back.
            w.Rig.Player.transform.position = sw.HelmWorldPosition;
            if (!sw.TakesTheHelmOnThisPress())
            {
                w.Fail($"standing on her helm at {sw.HelmWorldPosition}, E would not take it");
                yield break;
            }
            if (!sw.BeginInteract() || sw.Mode != ControlMode.Aboard)
            {
                w.Fail($"E at her helm left her {sw.Mode}, not at the helm");
                yield break;
            }
            yield return null;

            if (!sw.BeginInteract() || sw.Mode != ControlMode.OnDeck)
            {
                w.Fail($"E at the helm left her {sw.Mode}, not standing on her deck");
                yield break;
            }
            yield return null;

            w.Graph = new CabinWalkGraph(w.Cabin.Def, w.Hull.Visual.Deck, ProjectRoot);
            w.Report.AppendLine($"  left her helm onto {w.Graph.Name(LiveNode(w))}, {w.Where}" +
                                (w.Cabin.IsInside ? $", inside on level {w.Cabin.Level}" : ", outside"));
        }

        /// <summary>Where the GAME stands her now, as the planner counts places: a component of the level
        /// she walks when she walks one, else the deck area she stands on, inside or out.</summary>
        private static CabinNode LiveNode(HelmWalk w)
        {
            Vector2 at = w.At;
            FrozenFloors floors = FrozenFloors.Snapshot(w.Cabin);
            if (w.Cabin.IsInside && w.Graph.Walkable(w.Cabin.Level, floors))
            {
                CabinLevelGrid grid = w.Graph.Grid(w.Cabin.Level);
                return CabinNode.OnLevel(w.Cabin.Level, grid != null ? grid.ComponentAt(at) : -1);
            }
            int area = w.Graph.SeatNearestArea(new Vector3(at.x, at.y, w.Deck.DeckHeightMeters));
            // A live snapshot is the planner's before AND after, so whether her cells have loaded is moot.
            return w.Cabin.IsInside ? CabinNode.Inside(area) : CabinNode.Deck(area, true);
        }

        /// <summary>
        /// Hop by hop to her main door and through it. Each hop is re-planned from where the game stands
        /// her, on the cabin as it is now, so a hop that lands her somewhere the plan did not expect is
        /// walked on from there rather than trusted.
        /// </summary>
        private IEnumerator ToHerDoor(HelmWalk w)
        {
            for (int hops = 0; hops < MaxHops; hops++)
            {
                CabinNode live = LiveNode(w);
                if (!live.IsSomewhere)
                {
                    w.Fail($"at {w.Where} the game stands her on no floor the art measured");
                    yield break;
                }

                FrozenFloors floors = FrozenFloors.Snapshot(w.Cabin);
                CabinWalk plan = w.Graph.Walk(live, floors, floors);
                if (!plan.TryGetReacher(0, out CabinNode reacher))
                {
                    w.Fail($"from {w.Graph.Name(live)}, {w.Where}, she can never reach {w.Graph.Doors[0].Name}: " +
                           $"she can walk to {plan.DescribeNodes()}\ngraph: {w.Graph.Describe()}");
                    yield break;
                }

                List<CabinHop> path = plan.PathTo(reacher);
                if (path.Count == 0)
                {
                    yield return TheLastCrossing(w, live);
                    yield break;
                }

                CabinHop hop = path[0];
                w.Report.AppendLine($"  {w.Graph.Name(live)}: {hop.Label} -> {w.Graph.Name(hop.To)}   " +
                                    $"(plan: {plan.Describe(reacher)})");
                switch (hop.Kind)
                {
                    case CabinHopKind.DeckEdge:
                        yield return OverTheLink(w, live, hop);
                        break;
                    case CabinHopKind.DoorIn:
                    case CabinHopKind.DoorOut:
                        yield return ThroughADoor(w, live, hop.Index);
                        break;
                    case CabinHopKind.Route:
                        yield return TakeTheRoute(w, live, hop);
                        break;
                    default:
                        w.Fail($"the plan needs '{hop.Label}' from {w.Graph.Name(live)} — a hop this walk does not play");
                        yield break;
                }
                if (w.Failed) yield break;
            }
            w.Fail($"{MaxHops} hops and she never came to her door");
        }

        /// <summary>
        /// Walk <paramref name="leg"/> on held intents until <paramref name="done"/> or she arrives,
        /// failing with where she stuck if she stops making way (three half-seconds under 2 cm) or runs
        /// out of frames. The intents are released on every way out.
        /// </summary>
        private static IEnumerator Steer(HelmWalk w, CabinLeg leg, Func<bool> done)
        {
            if (!leg.IsWalkable)
            {
                w.Fail($"no path {leg.Name} from {w.Where} to ({leg.Target.x:0.00}, {leg.Target.y:0.00}) " +
                       "over the floor she stands on");
                yield break;
            }

            Transform boat = w.Rig.Boat.transform;
            float step = HerWalkSpeed * FrameSeconds;
            Vector2 windowFrom = w.At;
            int strikes = 0;
            for (int frame = 0; !done() && !leg.Arrived(w.At, ArriveWithin); frame++)
            {
                if (frame >= HopFrameCap)
                {
                    w.Fail($"{frame} frames {leg.Name} and she never arrived: {w.Where}, aiming for " +
                           $"({leg.Target.x:0.00}, {leg.Target.y:0.00})");
                    break;
                }
                w.Held.Walk(leg.Steer(w.At, Lookahead, step, DeckWalkController.DrawnHeadingDegreesOf(boat),
                                      DeckWalkController.BakeElevationDegreesOf(boat)));
                yield return null;

                if ((frame + 1) % StuckWindowFrames != 0) continue;
                if ((w.At - windowFrom).magnitude >= StuckMetres) strikes = 0;
                else if (++strikes >= StuckStrikes)
                {
                    w.Fail($"STUCK {leg.Name}: under {StuckMetres * 100f:0} cm in each of {StuckStrikes} half-seconds, " +
                           $"{w.Where}, aiming for ({leg.Target.x:0.00}, {leg.Target.y:0.00})");
                    break;
                }
                windowFrom = w.At;
            }
            w.Held.Release();
        }

        /// <summary>A point she can stand on inside the disk about <paramref name="centre"/>, on the floor
        /// <paramref name="node"/> is (at the centre's height, on a deck).</summary>
        private static bool TryTarget(HelmWalk w, CabinNode node, Vector3 centre, float radius, out Vector2 target)
            => node.Kind == CabinNodeKind.Level
                   ? CabinLegs.TryLevelTarget(w.Graph, node.Index, node.Component, centre, radius, out target)
                   : CabinLegs.TryDeckTarget(w.Graph, node.Index, centre, radius, centre.z, out target);

        private static CabinLeg LegTo(HelmWalk w, CabinNode node, string name, Vector2 target)
            => node.Kind == CabinNodeKind.Level
                   ? CabinLegs.OnLevel(w.Graph, name, node.Index, node.Component, w.At, target)
                   : CabinLegs.OnDeck(w.Graph, name, node.Index, w.At, target);

        /// <summary>Across a flush contact or a measured step onto the next deck area.</summary>
        private static IEnumerator OverTheLink(HelmWalk w, CabinNode live, CabinHop hop)
        {
            CabinDeckLink link = w.Graph.Links[hop.Index];
            CabinLeg leg = CabinLegs.OverLink(w.Graph, link, live.Index, w.At);
            yield return Steer(w, leg, () => !LiveNode(w).SamePlace(live));
            if (!w.Failed && LiveNode(w).SamePlace(live))
                w.Fail($"arrived {leg.Name}, {w.Where}, and the game still stands her on {w.Graph.Name(live)}: " +
                       $"the {link.Why} between {w.Graph.AreaId(link.A)} and {w.Graph.AreaId(link.B)} does not carry her");
        }

        /// <summary>
        /// Into the band of door <paramref name="k"/> (the planner's index: 0 is the main door), E if it
        /// is shut, and the frames the crossing takes. A latch seeded or spent in its band arms again on
        /// her first step clear of it (R1), so where she does not cross she steps clear ONCE and walks
        /// back in, as a player would.
        /// </summary>
        private static IEnumerator ThroughADoor(HelmWalk w, CabinNode live, int k)
        {
            CabinWalkGraph g = w.Graph;
            CabinDoorSpec spec = g.Doors[k];
            BoatCabinDoor door = BuiltDoor(w.Rig.Installer, spec.Door);
            if (door == null)
            {
                w.Fail($"{spec.Name} is in her def and nothing built it");
                yield break;
            }
            Vector3 sill = spec.Threshold;
            if (!TryTarget(w, live, sill, spec.BandRadius, out Vector2 target))
            {
                w.Fail($"no point of {g.Name(live)} she can stand on lies in the band of {spec.Name} " +
                       $"({spec.BandRadius:0.00} m about ({sill.x:0.00}, {sill.y:0.00}) at {sill.z:0.00} m)");
                yield break;
            }

            bool wasInside = w.Cabin.IsInside;
            Func<bool> crossed = () => w.Cabin.IsInside != wasInside;

            yield return Steer(w, LegTo(w, live, $"into the band of {spec.Name}", target), crossed);
            if (w.Failed || crossed()) yield break;
            yield return OpenAndCross(w, door, crossed);
            if (w.Failed || crossed()) yield break;

            float beyond = BoatCabinThreshold.ReleaseRadiusMetres(door.Door) + CabinLegs.TargetMargin;
            if (CabinLegs.TryBackOff(g, live, w.At, sill, beyond, out Vector2 back))
            {
                w.Report.AppendLine($"    stepped clear of {spec.Name} to ({back.x:0.00}, {back.y:0.00}) and back, " +
                                    "to arm its latch");
                yield return Steer(w, LegTo(w, live, $"clear of {spec.Name}", back), crossed);
                if (w.Failed || crossed()) yield break;
                yield return Steer(w, LegTo(w, live, $"back into the band of {spec.Name}", target), crossed);
                if (w.Failed || crossed()) yield break;
                yield return OpenAndCross(w, door, crossed);
                if (w.Failed || crossed()) yield break;
            }

            w.Fail($"in the band of {spec.Name}, {w.Where}, she never went through: open {door.IsOpen}, " +
                   $"cueing {door.IsCueing}, latch armed {door.PassageIsArmed}, in its band " +
                   $"{BoatCabinThreshold.IsInBand(door.Door, w.At)}, on its sill " +
                   $"{BoatCabinThreshold.IsOnTheSill(door.Door, w.Deck.DeckHeightMeters, g.Tolerance)} " +
                   $"(sill at {sill.z:0.00} m)");
        }

        /// <summary>E where the offer names the door, if it is shut — what she sees is what the press
        /// does — then the frames the crossing takes.</summary>
        private static IEnumerator OpenAndCross(HelmWalk w, BoatCabinDoor door, Func<bool> crossed)
        {
            if (!door.IsOpen && !door.IsCueing)
            {
                InteractOfferChanged offer = default;
                for (int f = 0; f < OfferFrames; f++)
                {
                    yield return null;
                    if (crossed()) yield break;
                    offer = InteractOffer.Current;
                    if (offer.Has && offer.Id == door.Id) break;
                }
                if (!offer.Has || offer.Id != door.Id || offer.Label != OpenTheDoor)
                {
                    w.Fail($"in the band of {door.Id}, {w.Where}, she is offered " +
                           (offer.Has ? $"'{offer.Label}' on {offer.Id} ({offer.Source})" : "nothing") +
                           $", not '{OpenTheDoor}' on her door");
                    yield break;
                }
                if (!w.Rig.Switcher.BeginInteract() || !door.IsCueing)
                {
                    w.Fail($"E under the offer '{offer.Label}' did not move the leaf of {door.Id} " +
                           $"(she is {w.Rig.Switcher.Mode})");
                    yield break;
                }
                w.Report.AppendLine($"    E: '{offer.Label}' on {door.Id}");

                float deadline = Time.time + door.CueSeconds + 1f;
                while (door.IsCueing && !crossed() && Time.time < deadline) yield return null;
                if (door.IsCueing && !crossed())
                {
                    w.Fail($"the leaf of {door.Id} was still moving {door.CueSeconds + 1f:0.0} s after E");
                    yield break;
                }
            }
            for (int f = 0; f < CrossingFrames && !crossed(); f++) yield return null;
        }

        /// <summary>
        /// To within reach of the route's near end, where the deck walk takes it. A route end re-arms once
        /// she is clear of it by <see cref="RouteRearmMultiple"/> reaches, so where it does not take her
        /// she steps clear ONCE and walks back.
        /// </summary>
        private static IEnumerator TakeTheRoute(HelmWalk w, CabinNode live, CabinHop hop)
        {
            CabinWalkGraph g = w.Graph;
            BoatInteriorRoute route = g.Def.Routes[hop.Index];
            Vector3 near = hop.NearEnd == 0 ? route.FromPoint : route.ToPoint;
            string end = $"{route.Id}'s end ({near.x:0.00}, {near.y:0.00}) at {near.z:0.00} m";
            if (!TryTarget(w, live, near, g.Reach, out Vector2 target))
            {
                w.Fail($"no point of {g.Name(live)} she can stand on lies within {g.Reach:0.00} m of {end}");
                yield break;
            }
            Func<bool> taken = () => !LiveNode(w).SamePlace(live);

            yield return Steer(w, LegTo(w, live, $"onto {route.Id}", target), taken);
            if (w.Failed || taken()) yield break;
            for (int f = 0; f < CrossingFrames && !taken(); f++) yield return null;
            if (taken()) yield break;

            float beyond = RouteRearmMultiple * g.Reach + CabinLegs.TargetMargin;
            if (CabinLegs.TryBackOff(g, live, w.At, near, beyond, out Vector2 back))
            {
                w.Report.AppendLine($"    stepped clear of {route.Id} to ({back.x:0.00}, {back.y:0.00}) and back, " +
                                    "to arm it");
                yield return Steer(w, LegTo(w, live, $"clear of {route.Id}", back), taken);
                if (w.Failed || taken()) yield break;
                yield return Steer(w, LegTo(w, live, $"back onto {route.Id}", target), taken);
                if (w.Failed || taken()) yield break;
                for (int f = 0; f < CrossingFrames && !taken(); f++) yield return null;
                if (taken()) yield break;
            }

            w.Fail($"at {end} she stands, {w.Where}, and the route never takes her");
        }

        /// <summary>Through her main door, IN from her deck or OUT from her cabin, and the bus hears
        /// exactly that one crossing.</summary>
        private IEnumerator TheLastCrossing(HelmWalk w, CabinNode live)
        {
            CabinDoorSpec main = w.Graph.Doors[0];
            bool wasInside = w.Cabin.IsInside;
            int entered = _entered.Count, left = _left.Count;
            w.Report.AppendLine($"  {w.Graph.Name(live)}: through {main.Name} {(wasInside ? "OUT" : "IN")}");

            yield return ThroughADoor(w, live, 0);
            if (w.Failed) yield break;

            int wentIn = _entered.Count - entered, cameOut = _left.Count - left;
            bool once = wasInside ? cameOut == 1 && wentIn == 0 : wentIn == 1 && cameOut == 0;
            if (w.Cabin.IsInside == wasInside || !once)
                w.Fail($"through {main.Name} she is {(w.Cabin.IsInside ? "inside" : "outside")}, and the bus heard " +
                       $"{wentIn} CabinEntered and {cameOut} CabinLeft: one crossing is one " +
                       (wasInside ? "CabinLeft" : "CabinEntered"));
            else
                w.Report.AppendLine($"    {(wasInside ? "out" : "in")}: {w.Graph.Name(LiveNode(w))}, {w.Where}");
        }

        /// <summary>The built door that works <paramref name="door"/>, the def's own entry.</summary>
        private static BoatCabinDoor BuiltDoor(BoatInteriorInstaller installer, BoatInteriorDoor door)
        {
            if (installer.Door != null && ReferenceEquals(installer.Door.Door, door)) return installer.Door;
            foreach (BoatCabinDoor built in installer.AdditionalDoors)
                if (built != null && ReferenceEquals(built.Door, door)) return built;
            return null;
        }
    }
}
