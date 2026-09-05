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
    /// <b>THE JOURNEY — a fisher walks onto a real lobster boat, opens her cabin and goes below.</b>
    /// The whole chain, driven rather than reasoned about: the committed <c>LobsterBoatIso</c> visual
    /// def, the mesh skinner, the shipped boarding verb, the interact seam's own press, the door's
    /// measured cue, the sheets arriving from <c>Resources</c> under it, and the hull letting go of the
    /// fisher it had been hiding.
    ///
    /// <para><b>Why this file exists rather than another unit test.</b> Every piece of ADR 0038 already
    /// had one, and the pieces still did not add up to a door you could walk through: the gate was shut
    /// fleet-wide, the entry was only ever driven on a hand-built rig, and the third consequence — the
    /// player's own occluder ids — was written down and not built. What is asserted here is the
    /// <i>journey</i>: board, walk to the threshold, press, be inside, cross a region line while below,
    /// press again, be back on deck with everything given back.</para>
    ///
    /// <para><b>Real assets, on purpose.</b> A mirror def would pass this suite and tell us nothing about
    /// the boat in the game. The lobster is the cheapest hull that carries a cabin (11.7 MB of cells) and
    /// the only interior-bearing hull whose level map is the identity — which is why the tanker gets her
    /// own journey at the bottom of this file, where it is not.</para>
    ///
    /// <para><b>State, never pixels.</b> Nothing here renders: no camera is created and nothing calls
    /// <c>Camera.Render</c>/<c>ReadPixels</c>, which on a CI editor with no graphics device kills the
    /// process outright. The occlusion is read back off the renderer's own property block — what was
    /// actually written, not what a field remembers.</para>
    ///
    /// <para><b>No frame-count-as-time.</b> The cue is advanced by real seconds and waited on by a real
    /// deadline; headless frames are nowhere near wall-clock ones and a test that counted them would be
    /// asserting against the machine it happens to run on.</para>
    ///
    /// <para><b>THE JOURNEY RUNS WITH HER LYING OFF, NOT TIED UP</b> — and that is now fixture economy
    /// rather than a finding. It used to be the latter: while a standable step-off was available,
    /// <c>ControlSwitcher</c> answered E with "step ashore" before the interact registry was consulted at
    /// all, so at a wharf her cabin door was neither offered nor pressable. The 2026-08-25 ruling put the
    /// registry ahead of the step ashore on deck, and
    /// <see cref="AtAWharf_TheCabinDoorTakesThePress_AndTheStepAshoreOfferStandsDown"/> — the test that
    /// used to pin the defect — now walks her through that same door while she is tied up.</para>
    /// </summary>
    public class BoatCabinJourneyPlayTests
    {
        private const string LobsterVisualPath = "Assets/_Project/Data/Boats/Visuals/LobsterBoatIso.asset";
        private const string LobsterHullPath = "Assets/_Project/Data/Boats/LobsterBoat.asset";
        private const string TankerInteriorPath = "Assets/_Project/Data/Boats/Interiors/TankerIso.asset";
        /// <summary>One of the eighteen lobster variants (ADR 0041 rollout PR 1) — a hardtop; she has a
        /// committed visual and interior def but no BoatHullDef, so the journey wraps her visual in one.</summary>
        private const string VariantVisualPath = "Assets/_Project/Data/Boats/Visuals/LobsterStandardHardtopFundyIso.asset";

        /// <summary>One of the four working ships (ADR 0041 rollout PR 2). She is the batch's journey
        /// because every one of the four declares an OPEN main deck at her house sole's height and her
        /// door's threshold sits there too — the tie a converted hull has no cells to break.</summary>
        private const string ShipVisualPath = "Assets/_Project/Data/Boats/Visuals/SternTrawlerIso.asset";

        /// <summary>The shader property the hull's occluding id block is written into. Named here because
        /// the test reads back what reached the RENDERER, which is the only thing the GPU would see.</summary>
        private const string OccluderIdProperty = "_HHDeckOccluderId";
        private const string OccluderIdTopProperty = "_HHDeckOccluderIdTop";

        private readonly List<UnityEngine.Object> _spawned = new();
        private readonly List<CabinEntered> _entered = new();
        private readonly List<CabinLeft> _left = new();
        private GameConfig _config;
        private MaterialPropertyBlock _readBlock;

        [SetUp]
        public void SetUp()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            InteractOffer.Reset();
            InteractionGate.Reset();
            BoatInteriorCells.Reset();

            // ⚠️ Clear() UNSUBSCRIBES every listener on the channel, so it runs BEFORE anything in the
            // fixture exists. A rider built after this point subscribes for itself, exactly as it does in
            // the game; one left over from a previous case is silenced here rather than double-counting.
            _entered.Clear();
            _left.Clear();
            EventBus.Clear<CabinEntered>();
            EventBus.Clear<CabinLeft>();
            EventBus.Subscribe<CabinEntered>(_entered.Add);
            EventBus.Subscribe<CabinLeft>(_left.Add);

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.InteriorRockScale = GameConfig.DefaultInteriorRockScale;
            _spawned.Add(_config);
            GameServices.Config = _config;

            _readBlock = new MaterialPropertyBlock();

            // ⚠ An AudioListener, because a listener-less play scene logs a warning EVERY frame — and a
            // full suite writes it about a million times into the log.
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

            // The cabins are gone; forget what anybody held. Not a substitute for the release the cabin
            // does in OnDestroy — that is asserted on its own below — just fixture isolation.
            BoatInteriorCells.Reset();
        }

        // =====================================================================================
        //  THE JOURNEY
        // =====================================================================================

        [UnityTest]
        public IEnumerator TheJourney_AboardHerOwnBoat_ThroughTheCabinDoorAndBackOutAgain()
        {
            Rig rig = NewLobsterRig();
            if (rig.Boat == null) yield break;   // editor-only fixture; LoadCommitted already Ignored
            yield return null;

            Assert.IsTrue(rig.Installer.Built, "the committed lobster carries a cabin and grows one on load");
            BoatInterior cabin = rig.Installer.Interior;
            BoatCabinDoor door = rig.Installer.Door;
            Assert.IsNotNull(door, "…and a door, at her measured threshold");

            // ---- nothing of her cabin is in memory before anybody touches the handle ------------
            Assert.AreEqual(0, BoatInteriorCells.ResidentSets,
                            "a hull nobody has boarded must hold none of her cabin's sheets");

            // ---- aboard, through the shipped verb ----------------------------------------------
            yield return Board(rig);
            Assert.AreEqual(ControlMode.OnDeck, rig.Switcher.Mode);
            Assert.IsTrue(rig.Rider.IsDrawing, "the rider is presenting her, which is what makes the " +
                                               "occluder assertions below about the real channel");

            // ---- to the threshold, and the popup names the way in ------------------------------
            // The offer is the switcher's own, republished every frame — not a candidate this test
            // pushed. What the player would SEE, in other words.
            yield return WalkToTheDoor(rig, door);

            Assert.IsTrue(InteractOffer.Current.Has,
                          $"standing at her threshold ({Vector2.Distance(rig.Player.transform.position, door.transform.position):0.00} m " +
                          $"from a door with {door.ReachMeters:0.00} m of reach) must offer the way in");
            Assert.AreEqual("Go below", InteractOffer.Current.Label);

            // ---- press, through the KEY's own entry point ------------------------------------
            Assert.IsTrue(rig.Switcher.BeginInteract(), "E at her threshold is spent on her door");

            // The cue is DECLARED: 8 frames at 70 ms off the sidecar, and the swap lands at the END of
            // it. Asserted as a duration and never as pixels — the sheets bake doorOpen: 0 only, so
            // there is no leaf artwork yet and the cue is honestly just the time the door takes.
            Assert.IsTrue(door.IsCueing, "a press starts the leaf moving");
            Assert.AreEqual(0.56f, door.CueSeconds, 1e-4f, "8 frames x 70 ms, straight off her sidecar");
            Assert.IsFalse(cabin.IsInside, "…and she is NOT inside while it is still moving");
            Assert.GreaterOrEqual(door.CueFrame, 0, "the cue publishes a frame for whoever draws it");

            // ⭐ THE LOAD USED TO HAPPEN HERE, under the cue. Since ADR 0041 her room is geometry
            // (#688): there is no sheet, so the cue start loads NOTHING and Resources is never touched.
            Assert.IsTrue(cabin.RoomIsGeometry, "the committed lobster's room is geometry (ADR 0041)");
            Assert.AreEqual(0, BoatInteriorCells.ResidentSets,
                            "a converted hull has no sheet to bring in at the cue — her cabin is in her mesh");

            yield return WaitForCue(door);

            // ---- inside --------------------------------------------------------------------
            Assert.IsTrue(cabin.IsInside, "the swap lands at the END of the cue");
            Assert.AreEqual(0, cabin.Level, "her sill is at house-sole height, so that is where she walks in");

            // ADR 0041: her cabin is her own hull mesh, revealed by the cutaway — ONE picture. No
            // sprite room exists on her at all (a second one would draw the cabin twice; measured on
            // main 2026-09-01 before the retirement: two sources).
            Assert.IsNull(rig.Boat.transform.Find(BoatInteriorInstaller.RoomChildName),
                          "a converted hull must carry no sprite room child");
            Assert.IsTrue(HullOf(rig).CutawayShown.Opens, "her house is cut open: the mesh room is what is drawn");
            Assert.AreEqual(1, BelowDecksDrawSources.Measure(rig.Boat.transform).Total,
                            "exactly one source draws below decks");

            // ---- and back out --------------------------------------------------------------
            Assert.AreEqual("Come out", door.VerbLabel, "the same door, said the other way round");
            Assert.IsTrue(door.TryUse());
            yield return WaitForCue(door);

            Assert.IsFalse(cabin.IsInside);
            yield return null;   // the cutaway re-asserts on CabinLeft
            Assert.IsFalse(HullOf(rig).CutawayShown.Opens, "her house closes behind her");
            Assert.AreEqual(0, cabin.Level, "and the level resets on the way out");

            Assert.AreEqual(1, _entered.Count, "one entry, published once");
            Assert.AreEqual(1, _left.Count, "one exit, published once");
        }

        /// <summary>
        /// THE RETIREMENT MEASUREMENT (ADR 0041, fleet rollout PR 0). The lobster's room has been
        /// geometry since #688. Below decks on her, how many things are drawing a cabin? The sprite
        /// room the ADR 0038 installer builds for every measured hull, the mesh room her open cut reveals,
        /// or exactly one of them. Measured on the shipped wiring, not reasoned about: the count is logged
        /// whichever way it comes out, so the number is in the run's log even when the assertion is red.
        /// </summary>
        [UnityTest]
        public IEnumerator BelowDecks_HerCabinDrawsFromExactlyOneSource_TheGeometry()
        {
            Rig rig = NewLobsterRig();
            if (rig.Boat == null) yield break;
            yield return null;

            yield return Board(rig);
            yield return EnterTheCabin(rig);
            yield return null;   // the cabin's LateUpdate has shown its cell; the cutaway has re-asserted
            yield return null;

            BelowDecksDrawSources.Count drawn = BelowDecksDrawSources.Measure(rig.Boat.transform);
            Debug.Log($"[mesh-interiors-retirement] lobster, below decks: {drawn}");

            Assert.IsTrue(drawn.MeshRoom,
                "her house is not cut open while she is below — the mesh room, which IS her cabin now, " +
                "is not being shown. " + drawn);
            Assert.AreEqual(1, drawn.Total,
                "a converted hull must draw her cabin from ONE source. Two means the sprite room is still " +
                "wired under a mesh room — she draws her cabin twice (ADR 0041's own warning). " + drawn);
        }

        /// <summary>
        /// <b>ADR 0041 rollout PR 1 — one of the eighteen lobster variants goes below into her own mesh
        /// room.</b> The variants have no BoatHullDef of their own (they are the ambient fleet), so the
        /// committed visual — with her re-baked hull mesh and her interior def — is wrapped in one here,
        /// skinned by the shipped skinner, and her installer built exactly as BoatController does it.
        /// Below decks she must draw from ONE source, the geometry, and load no sheet: hers is retired.
        /// </summary>
        [UnityTest]
        public IEnumerator AVariantOfTheBatch_GoesBelowIntoHerOwnMeshRoom_AndLoadsNoSheet()
        {
            var visual = LoadCommitted<BoatVisualDef>(VariantVisualPath);
            if (visual == null) yield break;
            Assert.IsNotNull(visual.Interior, "the variant's visual must link her interior def");
            Assert.IsTrue(visual.HasHullMesh() && visual.HullMesh.HasMeshInterior(),
                          "the variant's committed hull mesh must carry her room (re-bake her)");

            var hullDef = ScriptableObject.CreateInstance<BoatHullDef>();
            hullDef.Visual = visual;
            _spawned.Add(hullDef);

            var boatGo = Spawn("LobsterVariant");
            var boat = boatGo.AddComponent<BoatController>();
            boatGo.AddComponent<DevBoatInput>().enabled = false;
            boat.SetHull(hullDef);
            BoatHullSkinner.Rig skin = BoatHullSkinner.Apply(
                boatGo, visual, boat, new BoatHullSkinner.Options { SkipWaveMotion = true });
            Assert.IsTrue(skin.Skinned, "the variant must skin — she is a MESH hull");
            var installer = boatGo.GetComponent<BoatInteriorInstaller>();
            installer.Build();
            Assert.IsTrue(installer.Built, "a measured variant grows her cabin on load");
            Assert.IsTrue(installer.Interior.RoomIsGeometry, "…and it is geometry, off the bake's own output");
            Assert.IsNull(boatGo.transform.Find(BoatInteriorInstaller.RoomChildName), "no sprite room child");
            Assert.IsNull(Resources.Load<BoatInteriorCellsDef>(BoatInteriorCellsDef.PathFor(visual.Interior.Id)),
                          "her cells asset is RETIRED");
            yield return null;

            BoatCabinDoor door = installer.Door;
            Assert.IsNotNull(door, "…and a door at her measured threshold");
            Assert.IsTrue(door.TryUse(), "her door offers, so the press is taken");
            yield return WaitForCue(door);
            Assert.IsTrue(installer.Interior.IsInside);
            yield return null;

            BelowDecksDrawSources.Count drawn = BelowDecksDrawSources.Measure(boatGo.transform);
            Debug.Log($"[mesh-interiors-rollout] {visual.name}, below decks: {drawn}");
            Assert.IsTrue(drawn.MeshRoom, "her house is cut open: the mesh room is what is drawn. " + drawn);
            Assert.AreEqual(1, drawn.Total, "exactly one source draws below decks. " + drawn);
            Assert.AreEqual(0, BoatInteriorCells.ResidentSets, "no sheet was loaded for her");
        }

        /// <summary>
        /// <b>ADR 0041 rollout PR 2 — a WORKING SHIP goes below, and lands in her house rather than on
        /// her deck.</b> The stern trawler has no <c>BoatHullDef</c> either, so her committed visual is
        /// wrapped in one exactly as the variant journey does.
        ///
        /// <para><b>Why a ship is the journey this batch owes.</b> All four working ships declare an
        /// OPEN <c>main_deck</c> at the exact height of their <c>house_sole</c>, and their cabin door's
        /// threshold sits at that height too — so "the nearest sole to the sill" names two levels and one
        /// of them is outdoors. On a sprite hull the cells' row map breaks the tie; a converted hull has
        /// no cells, so <see cref="BoatInterior.LevelIndexAtHeight"/> asks her mesh instead
        /// (<see cref="HullMeshDef.CutawayForDeck"/> refuses an open level). Get that wrong and the
        /// press still works, the cue still runs and she is still "inside" — she is just standing on an
        /// outdoor deck whose roof cannot be taken off, and the room never appears. So this asserts the
        /// LEVEL and the CUT, not only that one source draws.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AWorkingShipOfTheBatch_GoesBelowIntoHerHouse_NotOntoHerMainDeck()
        {
            var visual = LoadCommitted<BoatVisualDef>(ShipVisualPath);
            if (visual == null) yield break;
            BoatInteriorDef def = visual.Interior;
            Assert.IsNotNull(def, "the trawler's visual must link her interior def");
            Assert.IsTrue(visual.HasHullMesh() && visual.HullMesh.HasMeshInterior(),
                          "her committed hull mesh must carry her room (re-bake her)");

            // The premise, on her shipped data: two levels equally near the sill, one of them open.
            float sill = def.Door.ThresholdPoint.z;
            var equidistant = new List<BoatInteriorLevel>();
            foreach (BoatInteriorLevel l in def.Levels)
                if (l != null && l.IsUsable() && Mathf.Abs(l.SoleZMeters - sill) < 1e-4f) equidistant.Add(l);
            Assert.GreaterOrEqual(equidistant.Count, 2,
                $"the trawler's sill at z {sill:0.###} is meant to be equidistant from her main deck and " +
                "her house sole — if it is not any more, this journey has stopped testing the tie.");

            var hullDef = ScriptableObject.CreateInstance<BoatHullDef>();
            hullDef.Visual = visual;
            _spawned.Add(hullDef);

            var boatGo = Spawn("SternTrawler");
            var boat = boatGo.AddComponent<BoatController>();
            boatGo.AddComponent<DevBoatInput>().enabled = false;
            boat.SetHull(hullDef);
            BoatHullSkinner.Rig skin = BoatHullSkinner.Apply(
                boatGo, visual, boat, new BoatHullSkinner.Options { SkipWaveMotion = true });
            Assert.IsTrue(skin.Skinned, "the trawler must skin — she is a MESH hull");

            var installer = boatGo.GetComponent<BoatInteriorInstaller>();
            installer.Build();
            Assert.IsTrue(installer.Built, "a measured ship grows her cabin on load");
            Assert.IsTrue(installer.Interior.RoomIsGeometry, "…and it is geometry, off the bake's own output");
            Assert.IsNull(boatGo.transform.Find(BoatInteriorInstaller.RoomChildName), "no sprite room child");
            Assert.IsNull(Resources.Load<BoatInteriorCellsDef>(BoatInteriorCellsDef.PathFor(def.Id)),
                          "her cells asset is RETIRED");
            yield return null;

            BoatCabinDoor door = installer.Door;
            Assert.IsNotNull(door, "…and a door at her measured threshold");
            Assert.IsTrue(door.TryUse(), "her door offers, so the press is taken");
            yield return WaitForCue(door);
            Assert.IsTrue(installer.Interior.IsInside, "the swap lands at the end of the cue");
            yield return null;

            // WHERE she landed. The def is the authority on the id; the mesh is the authority on whether
            // it is a room.
            string landed = def.Levels[installer.Interior.Level].Id;
            Assert.IsTrue(visual.HullMesh.CutawayForDeck(landed).Opens,
                $"she walked in onto '{landed}', which her hull mesh will not cut open — an OPEN working " +
                "deck at her house sole's height. LevelIndexAtHeight must ask the mesh on a converted hull.");
            Assert.AreEqual("house_sole", landed,
                "and on this ship the room behind that door is her house");

            Assert.IsNotNull(installer.Cutaway, "a mesh hull carries a cutaway");
            Assert.IsTrue(installer.Cutaway.RequestedCut.Opens,
                "…and with her occupant below it is actually open — a cut that answers None is a roof " +
                "that stayed on");

            BelowDecksDrawSources.Count drawn = BelowDecksDrawSources.Measure(boatGo.transform);
            Debug.Log($"[mesh-interiors-rollout] {visual.name}, below decks on '{landed}': {drawn}");
            Assert.IsTrue(drawn.MeshRoom, "her house is cut open: the mesh room is what is drawn. " + drawn);
            Assert.AreEqual(1, drawn.Total, "exactly one source draws below decks. " + drawn);
            Assert.AreEqual(0, BoatInteriorCells.ResidentSets, "no sheet was loaded for her");
        }

        /// <summary>
        /// <b>ADR 0041 changed what this test could measure.</b> It used to read the comfort fraction
        /// (<c>InteriorRockScale</c>, 0.45) off a SPRITE room leaning under her hull. A converted hull has
        /// no second picture: her room is her own mesh and rides at her own rock, and the comfort clamp
        /// has nothing to scale. So the claim is now the structural one — there is no room transform to
        /// lean — and the hull is still posed exactly as before. The clamp itself stays in
        /// <c>GameConfig</c> for the sprite hulls the fleet still draws.
        /// </summary>
        [UnityTest]
        public IEnumerator HerRoomIsHerHull_SoThereIsNoSecondPictureToRideAtTheComfortFraction()
        {
            const float tilt = 8f;

            Rig rig = NewLobsterRig();
            if (rig.Boat == null) yield break;
            yield return null;

            yield return EnterTheCabin(rig);

            rig.Skin.Presenter.VisualTiltDegrees = tilt;
            yield return null;

            Assert.IsTrue(rig.Installer.Interior.RoomIsGeometry);
            Assert.IsNull(rig.Boat.transform.Find(BoatInteriorInstaller.RoomChildName),
                          "a converted hull has no sprite room to lean — her cabin is in her mesh");
            Assert.AreEqual(tilt, rig.Skin.Presenter.VisualTiltDegrees, 1e-5f,
                            "the hull is posed identically: nothing about the room reaches her pose");
            Assert.AreEqual(GameConfig.DefaultInteriorRockScale, GameServices.InteriorRockScale, 1e-6f,
                            "the comfort clamp still exists for the sprite hulls; it is simply inert here");
        }

        /// <summary>
        /// ⭐ <b>TIED UP AT A WHARF, HER CABIN DOOR TAKES THE PRESS — the flipped pin.</b>
        ///
        /// <para><b>What this used to assert.</b> <see cref="ControlSwitcher.BeginInteract"/> answered E
        /// with its own transitions <i>before</i> the interact registry was looked at: on deck, out of
        /// helm reach, with a standable step-off available, E stepped ashore. So wherever
        /// <c>CanStepAshore()</c> was true — moored at a wharf, or lying over bared ground at low water —
        /// the press never reached the registry, the popup named the dock rather than the cabin, and this
        /// test said so out loud, remarking that it was the one that should fail when the ordering was
        /// ruled on.</para>
        ///
        /// <para><b>It was.</b> Lead-architect, 2026-08-25: on deck, away from the helm, the registry is
        /// consulted BEFORE stepping ashore. The amended invariant is that <i>boarding and the helm win
        /// over the registry; step-ashore yields to a resolving fixture</i> — so a fixture on a docked
        /// deck is reachable, and the transit offer stands itself down rather than advertising a press
        /// the game will not perform. On-foot boarding is untouched, and the fuller end state (boarding
        /// registering as a candidate too) stays deferred.</para>
        ///
        /// <para>So this now asserts the new truth on the same harness: she is tied up, her door is
        /// willing, the popup names the way in, and E takes her below.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AtAWharf_TheCabinDoorTakesThePress_AndTheStepAshoreOfferStandsDown()
        {
            Rig rig = NewLobsterRig(atAWharf: true);
            if (rig.Boat == null) yield break;
            yield return null;

            yield return Board(rig);
            yield return WalkToTheDoor(rig, rig.Installer.Door);

            Assert.IsTrue(rig.Switcher.CanStepAshore(), "harness: she must actually be tied up for this");
            Assert.IsTrue(rig.Installer.Door.IsAvailable,
                          "the door itself is willing — the gate is open and she is in reach");

            Assert.AreEqual("Go below", InteractOffer.Current.Label,
                            "the popup names the way in, not the dock: with a fixture resolving, the " +
                            "switcher makes no transit offer at all");
            Assert.AreEqual(InteractOfferSource.Fixture, InteractOffer.Current.Source,
                            "…and it is the VERB's slot that is standing, not a re-ranked transit");

            Assert.IsTrue(rig.Switcher.BeginInteract(), "E at her threshold is spent on her door");
            Assert.AreEqual(ControlMode.OnDeck, rig.Switcher.Mode, "…and it did not step her ashore");
            yield return WaitForCue(rig.Installer.Door);

            Assert.IsTrue(rig.Installer.Interior.IsInside,
                          "she went below — from a boat that is tied up, which is the whole ruling");
        }

        // =====================================================================================
        //  ADR 0038's THIRD CONSEQUENCE — the hull stops hiding a fisher who is INSIDE her
        // =====================================================================================

        [UnityTest]
        public IEnumerator TheHullStopsHidingHerWhileSheIsBelow_AndTakesItBackWhenSheComesOut()
        {
            Rig rig = NewLobsterRig();
            if (rig.Boat == null) yield break;
            yield return null;

            yield return Board(rig);
            yield return null;

            float onDeckId = ReadOccluderId(rig);
            float onDeckTop = ReadOccluderIdTop(rig);
            Assert.Greater(onDeckId, 0f,
                           "harness: a fisher standing on a MESH hull must be carrying a live occluding " +
                           "id, or the rest of this test is asserting that 0 stays 0");
            Assert.IsFalse(rig.Rider.BelowDecks);

            yield return EnterTheCabin(rig);
            yield return null;

            Assert.IsTrue(rig.Rider.BelowDecks, "the rider answered the cabin's signal");
            Assert.AreEqual(0f, ReadOccluderId(rig), 1e-6f,
                            "she is INSIDE the house that was hiding her, so the hull hides nobody");
            Assert.AreEqual(0f, ReadOccluderIdTop(rig), 1e-6f, "…and the range goes with it");

            yield return LeaveTheCabin(rig);
            yield return null;

            Assert.IsFalse(rig.Rider.BelowDecks);
            Assert.AreEqual(onDeckId, ReadOccluderId(rig), 1e-6f,
                            "back on deck, the hull hides her again — by the same id, because the slot " +
                            "was held across the doorway rather than released and re-claimed");
            Assert.AreEqual(onDeckTop, ReadOccluderIdTop(rig), 1e-6f);
        }

        [UnityTest]
        public IEnumerator ACabinEnteredOnANOTHERBoat_LeavesThisFisherExactlyAsSheWas()
        {
            // The guard on the signal, and the reason it carries a hull at all. Nothing in the game
            // publishes this for a boat the player is not on today — but the ambient fleet all carry
            // cabins, and a rider that took every CabinEntered as its own would go transparent to the
            // hull it is standing on because somebody else's door opened.
            Rig rig = NewLobsterRig();
            if (rig.Boat == null) yield break;
            yield return null;

            yield return Board(rig);
            yield return null;
            float onDeckId = ReadOccluderId(rig);

            GameObject someoneElse = Spawn("AnotherBoatEntirely");
            EventBus.Publish(new CabinEntered(someoneElse.GetEntityId(), 0));
            yield return null;

            Assert.IsFalse(rig.Rider.BelowDecks, "a different hull's doorway is not this fisher's");
            Assert.AreEqual(onDeckId, ReadOccluderId(rig), 1e-6f);
        }

        // =====================================================================================
        //  THE REGION HOP — she is still below on the other side of the line
        // =====================================================================================

        [UnityTest]
        public IEnumerator CrossingARegionBoundaryWhileBelow_LeavesHerBelow_AndPublishesNothingNew()
        {
            Rig rig = NewLobsterRig();
            if (rig.Boat == null) yield break;
            yield return null;

            yield return Board(rig);
            yield return EnterTheCabin(rig);
            yield return null;

            int enteredBefore = _entered.Count;
            Assert.AreEqual(1, enteredBefore);

            // ROOT-TOGGLING IS HOW A REGION HOP WORKS. The travel coordinator deactivates the roots of
            // the scene you leave and activates the ones you arrive in; the boat and her cabin go with
            // you (ADR 0038 proposal 4), so "disabled" happens constantly and does not mean "gone".
            rig.Boat.SetActive(false);
            yield return null;
            rig.Boat.SetActive(true);
            yield return null;
            yield return null;   // one more, so the re-asserted swap and the rider have both ticked

            Assert.IsTrue(rig.Installer.Interior.IsInside, "she is still below on the far side of the line");
            Assert.IsTrue(rig.Rider.BelowDecks, "…and the rider still knows it");
            Assert.AreEqual(0f, ReadOccluderId(rig), 1e-6f,
                            "…so the hull has not gone back to hiding a fisher who is inside her");

            // ⚠️ THE SHARP EDGE. The cabin re-asserts its swap in OnEnable, which a hop fires on every
            // boundary. A publish from THERE would announce a fresh entry every crossing, and any
            // listener that counted, logged, cued a sound or started an animation on the edge would do
            // it again for a player who has not moved.
            Assert.AreEqual(enteredBefore, _entered.Count,
                            "the re-assert is not a transition, and must not be published as one");
            Assert.AreEqual(0, _left.Count, "…nor is a root toggle an exit");
        }

        // =====================================================================================
        //  RESIDENCY — the sheets arrive at the cue and are given back when the boat is gone
        // =====================================================================================

        /// <summary>
        /// <b>A converted hull never touches Resources</b> — not at spawn, not at the cue, not inside,
        /// not on destroy — and her cells asset does not exist to be found. (The sheets used to arrive
        /// at the cue start and go back in OnDestroy; that mechanism still serves the sprite hulls and
        /// is exercised on the tanker below.)
        /// </summary>
        [UnityTest]
        public IEnumerator AConvertedHullNeverLoadsASheet_BecauseThereIsNone()
        {
            Rig rig = NewLobsterRig();
            if (rig.Boat == null) yield break;
            yield return null;

            BoatInteriorDef def = LoadCommitted<BoatVisualDef>(LobsterVisualPath).Interior;
            Assert.IsNull(Resources.Load<BoatInteriorCellsDef>(BoatInteriorCellsDef.PathFor(def.Id)),
                          "her cells asset is RETIRED — nothing should be under Resources for a mesh room");

            Assert.AreEqual(0, BoatInteriorCells.ResidentSets, "nothing before the handle is touched");
            yield return Board(rig);
            Assert.AreEqual(0, BoatInteriorCells.ResidentSets, "…and nothing for standing on her deck");

            yield return EnterTheCabin(rig);
            Assert.AreEqual(0, BoatInteriorCells.ResidentSets,
                            "…and nothing INSIDE: the cue start asked for no sheet, because RoomIsGeometry");
            Assert.AreEqual(0f, BoatInteriorCells.ResidentMegabytes, 1e-4f);

            UnityEngine.Object.Destroy(rig.Boat);
            yield return null;
            Assert.AreEqual(0, BoatInteriorCells.ResidentSets, "nothing was held, so nothing is released");
        }

        // =====================================================================================
        //  D · THE LEVEL MAP — a def's LEVELS are not a sheet's ROWS, and the tanker proves it
        // =====================================================================================

        [UnityTest]
        public IEnumerator TheTankerDoorOpensOntoHerHOUSE_NotHerEngineSpace()
        {
            // ⭐ THE WORST TRAP IN THIS ARC, driven rather than reasoned about. A BoatInteriorDef
            // declares every level a ROUTE reaches — including exterior working decks the sheets never
            // bake — and the sheets run bridge/house/below where the tanker's def runs
            // main_deck/poop_deck/house_sole/bridge_sole/below_sole. Index the cells by the DEF's level
            // index and walking into her wheelhouse draws row 2, which is `below`: the engine space.
            // It looks like a perfectly good cabin, and nothing else in the project would notice.
            BoatInteriorDef def = LoadCommitted<BoatInteriorDef>(TankerInteriorPath);
            if (def == null) yield break;

            BoatInteriorCellsDef cells = BoatInteriorCells.Acquire(def.Id);
            Assert.IsNotNull(cells, $"the tanker's committed cells must be under Resources as " +
                                    $"{BoatInteriorCellsDef.PathFor(def.Id)}");
            Assert.IsTrue(cells.IsUsableFor(def));

            int house = Array.IndexOf(cells.CellLevels, "house");
            int below = Array.IndexOf(cells.CellLevels, "below");
            Assert.GreaterOrEqual(house, 0, "her sheets must bake a `house` row at all");
            Assert.AreNotEqual(house, below);

            // ⭐ THE HARNESS GUARD THAT MAKES THIS TEST WORTH RUNNING: her def's index for the house sole
            // must NOT equal the sheet's house row. If a re-bake ever made them agree, this file would go
            // on passing while testing nothing — so say so out loud rather than discover it later.
            int defIndexOfHouseSole = Array.FindIndex(def.Levels, l => l != null && l.Id == "house_sole");
            Assert.AreNotEqual(house, defIndexOfHouseSole,
                               "the tanker is in this suite BECAUSE her def index and her sheet row " +
                               "differ; if they ever agree, pick a hull where they do not");

            // The journey, on the smallest rig that can make it: her real def, her real cells, her real
            // sill. (No mesh hull — the tanker's 281 MB of sheets is quite enough for one test, and what
            // is under examination is which ROW gets drawn, not how she is skinned.)
            CabinRig rig = NewCabinRig(def, cells);
            yield return null;

            int sillLevel = rig.Cabin.LevelIndexAtHeight(def.Door.ThresholdPoint.z);
            Assert.AreEqual("house_sole", def.Levels[sillLevel].Id,
                            "her sill is at house-sole height — and poop_deck, which is at the SAME " +
                            "height, must be skipped because the sheets bake no working deck");

            Assert.IsTrue(rig.Door.TryUse());
            yield return WaitForCue(rig.Door);
            Assert.IsTrue(rig.Cabin.IsInside);
            Assert.AreEqual(sillLevel, rig.Cabin.Level);

            Assert.AreEqual(house, rig.Cabin.CellRowFor(sillLevel),
                            "the level map — builder-computed, matched by id — sends her wheelhouse to " +
                            "the sheet's HOUSE row");
            Assert.AreNotEqual(below, rig.Cabin.CellRowFor(sillLevel),
                               "…and never to `below`, which is what indexing by the def's level would give");

            // What is actually DRAWN, which is the only version of this claim that cannot be argued with.
            int facing = IsoFacing.HeadingToFacingIndex(0f, cells.Facings, 0f, cells.CellsAreCounterClockwise);
            Assert.AreSame(cells.Cells[house * cells.Facings + facing], rig.Room.sprite,
                           "the cell on screen is the one baked for her house");

            // An outdoor deck is a real answer of -1, and it is what stops a cabin door walking somebody
            // onto a working deck to be shown nothing.
            for (int i = 0; i < def.Levels.Length; i++)
                if (def.Levels[i] != null && def.Levels[i].Id.EndsWith("_deck", StringComparison.Ordinal))
                    Assert.AreEqual(-1, rig.Cabin.CellRowFor(i),
                                    $"'{def.Levels[i].Id}' is weather deck: the sheets draw nothing for it");

            BoatInteriorCells.Release(def.Id);
        }

        // =====================================================================================
        //  the fixture
        // =====================================================================================

        private struct Rig
        {
            public GameObject Player;
            public SpriteRenderer RiderSr;
            public DeckRiderVisual Rider;
            public ControlSwitcher Switcher;
            public GameObject Boat;
            public BoatInteriorInstaller Installer;
            public BoatHullSkinner.Rig Skin;
        }

        /// <summary>A cabin and her door, with no boat around them — everything the level map needs and
        /// nothing it does not.</summary>
        private struct CabinRig
        {
            public BoatInterior Cabin;
            public BoatCabinDoor Door;
            public SpriteRenderer Room;
        }

        private GameObject Spawn(string name, Vector3 position = default)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawned.Add(go);
            return go;
        }

        /// <summary>
        /// The shipped rig, as <c>PersistentCoreBuilder</c> assembles it, wearing the REAL committed
        /// lobster: a rider child under the player, a boat skinned by the mesh path, and the interior
        /// installer <see cref="BoatController"/> mounts for itself.
        ///
        /// <para>Returns an empty rig (and <c>Assert.Ignore</c>s) in a player build, where the committed
        /// defs cannot be reached — the <c>MeshHullPlayTests</c> precedent.</para>
        /// </summary>
        /// <param name="atAWharf">Give her a dock zone. Off by default, and the default matters — see the
        /// note where it is read.</param>
        private Rig NewLobsterRig(bool atAWharf = false)
        {
            var visual = LoadCommitted<BoatVisualDef>(LobsterVisualPath);
            if (visual == null) return default;
            var hullDef = LoadCommitted<BoatHullDef>(LobsterHullPath);
            if (hullDef == null) return default;

            Assert.AreSame(visual, hullDef.Visual, "her hull def must point at her own visual def");
            Assert.IsNotNull(visual.Interior,
                             "the committed lobster must be wired to her own cabin — re-run Hidden " +
                             "Harbours ▸ Dev ▸ 3D Hulls ▸ Wire Boat Interiors To Visuals");

            // ---- the boat --------------------------------------------------------------------
            var boatGo = Spawn("LobsterBoat");
            var boat = boatGo.AddComponent<BoatController>();     // Awake mounts the interior installer
            var input = boatGo.AddComponent<DevBoatInput>();
            boat.SetHull(hullDef);

            // ⚠ SkipWaveMotion: this fixture wants a hull whose lean it can STATE, not one riding a sea
            // that is glass-calm in a test scene anyway. The rock is pinned by BoatInteriorPlayTests.
            BoatHullSkinner.Rig skin = BoatHullSkinner.Apply(
                boatGo, visual, boat, new BoatHullSkinner.Options { SkipWaveMotion = true });
            Assert.IsTrue(skin.Skinned, "the committed lobster must skin — she is a MESH hull (ADR 0022)");
            Assert.AreEqual(BoatHullVariant.Mesh, skin.Presenter.Variant);

            var installer = boatGo.GetComponent<BoatInteriorInstaller>();
            Assert.IsNotNull(installer, "BoatController.Awake should have mounted the installer itself");
            // Awake ran BEFORE SetHull, so the installer's own Start found no hull to read. Idempotent.
            installer.Build();

            // ---- the player ------------------------------------------------------------------
            var playerGo = Spawn("Player", new Vector3(1.2f, 0f, 0f));
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

            // NO DOCK ZONE unless a test asks for one — she is lying off, not tied up. This used to be
            // load-bearing: while "step ashore" was available the switcher answered E with it and the
            // press never reached the interact registry, so her cabin door could not be pressed at all.
            // The 2026-08-25 ruling put the registry ahead of the step ashore on deck (see
            // TryInteractCandidate's remarks), so a wharf no longer shuts the door — it is now simply the
            // narrower fixture, and AtAWharf_TheCabinDoorTakesThePress below is the one that asks for the
            // wider one.
            Transform dockZone = null;
            if (atAWharf) dockZone = Spawn("Wharf").transform;

            var swGo = Spawn("Switcher");
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dockZone, zoneRadius: 6f, disembarkPoint: null);
            // The helm is pulled in tight ON PURPOSE: this journey is about a door, and a generous helm
            // reach would have the interact key taking the wheel from wherever boarding lands her.
            sw.ConfigureHelm(new Vector2(0f, -1.3f), 0.05f);

            return new Rig
            {
                Player = playerGo, RiderSr = riderSr, Rider = rider, Switcher = sw,
                Boat = boatGo, Installer = installer, Skin = skin,
            };
        }

        /// <summary>A bare cabin: her real def, her real cells, her door — no hull, no skin, no boat.</summary>
        private CabinRig NewCabinRig(BoatInteriorDef def, BoatInteriorCellsDef cells)
        {
            var root = Spawn($"CabinRig_{def.Id}");

            var roomGo = new GameObject("Room");
            roomGo.transform.SetParent(root.transform, false);
            var room = roomGo.AddComponent<SpriteRenderer>();

            var cabin = root.AddComponent<BoatInterior>();
            cabin.Configure(def, null, room, null, roomGo.transform, root.transform,
                            cells.Cells, cells.Facings, cells.CellsAreCounterClockwise, 0f,
                            0f, 0f, 0f, cells.CellRowForLevel);

            var doorGo = new GameObject("CabinDoor");
            doorGo.transform.SetParent(root.transform, false);
            var door = doorGo.AddComponent<BoatCabinDoor>();
            door.Configure(cabin, $"fixture.boat.{def.Id}.entry", -1, 1.2f, "Go below", "Come out");

            return new CabinRig { Cabin = cabin, Door = door, Room = room };
        }

        // ---- the moves the journey is made of ---------------------------------------------------

        /// <summary>Board through the key's own entry point, so whichever route the switcher picks —
        /// the step across, or down a wharf ladder when the tide has opened a gap a step cannot honestly
        /// cross — is the one the player would get. Which of the two it is belongs to
        /// <c>ControlSwitcher</c>'s own suite; what this journey needs is a fisher genuinely on the
        /// deck, with her rider bound to that hull.</summary>
        private IEnumerator Board(Rig rig)
        {
            Assert.IsTrue(rig.Switcher.BeginInteract(), "E within reach must start the boarding");
            yield return WaitUntil(() => rig.Switcher.Mode == ControlMode.OnDeck, 5f,
                                   "the boarding move to land her on the deck");
            yield return null;
        }

        /// <summary>Put her at the threshold and let a tick clamp her onto the deck the way her own walk
        /// would. The door follows the drawn hull round, so this is wherever the art puts it.</summary>
        private IEnumerator WalkToTheDoor(Rig rig, BoatCabinDoor door)
        {
            rig.Player.transform.position = door.transform.position;
            yield return null;
        }

        private IEnumerator EnterTheCabin(Rig rig)
        {
            BoatCabinDoor door = rig.Installer.Door;
            Assert.IsTrue(door.TryUse(), "her door offers, so the press is taken");
            yield return WaitForCue(door);
            Assert.IsTrue(rig.Installer.Interior.IsInside);
        }

        private IEnumerator LeaveTheCabin(Rig rig)
        {
            BoatCabinDoor door = rig.Installer.Door;
            Assert.IsTrue(door.TryUse());
            yield return WaitForCue(door);
            Assert.IsFalse(rig.Installer.Interior.IsInside);
        }

        /// <summary>Wait out the leaf, by SECONDS and a real deadline. Frames are not time.</summary>
        private IEnumerator WaitForCue(BoatCabinDoor door)
        {
            float deadline = Time.time + door.CueSeconds + 1f;
            while (door.IsCueing && Time.time < deadline) yield return null;
            Assert.IsFalse(door.IsCueing, "the leaf finished moving within its own declared duration");
            yield return null;   // one more, so the swap's LateUpdate has run
        }

        private IEnumerator WaitUntil(Func<bool> done, float seconds, string what)
        {
            float deadline = Time.time + seconds;
            while (!done() && Time.time < deadline) yield return null;
            Assert.IsTrue(done(), $"timed out after {seconds:0.0}s waiting for {what}");
        }

        // ---- reading what actually reached the renderer -----------------------------------------

        private float ReadOccluderId(Rig rig) => ReadRenderer(rig, OccluderIdProperty);

        private float ReadOccluderIdTop(Rig rig) => ReadRenderer(rig, OccluderIdTopProperty);

        /// <summary>Read a float back off the RIDER's own property block — what was written to the
        /// renderer, which is the only version of it the GPU would ever see.</summary>
        private float ReadRenderer(Rig rig, string property)
        {
            rig.RiderSr.GetPropertyBlock(_readBlock);
            return _readBlock.GetFloat(property);
        }

        /// <summary>The hull's facet renderer — the mesh that IS her cabin on a converted hull.</summary>
        private static IsoFacetHullRenderer HullOf(Rig rig)
        {
            var r = rig.Skin.Visual.GetComponent<IsoFacetHullRenderer>();
            Assert.IsNotNull(r, "the committed lobster is a mesh hull; her visual child carries the facet renderer");
            return r;
        }

        /// <summary>Load a committed def, or <c>Assert.Ignore</c> in a player build where the
        /// AssetDatabase is not there to reach it. These tests assert the REAL boat, not a mirror of
        /// her — the <c>MeshHullPlayTests</c> precedent, for the same reason.</summary>
        private static T LoadCommitted<T>(string path) where T : ScriptableObject
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"missing {path}");
            return asset;
#else
            Assert.Ignore("Needs the AssetDatabase: this journey asserts the REAL committed hull.");
            return null;
#endif
        }
    }
}
