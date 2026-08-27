using System.Collections.Generic;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The cutaway gate's behaviour</b> — the owner's 2026-08-26 ruling as four states and their
    /// boundaries: below decks she is cut open; at the helm and on deck she is whole; and neither
    /// answer may leak onto another boat.
    ///
    /// <para><b>PlayMode because the inputs are signals and a component lifecycle.</b>
    /// <see cref="BoatCutaway"/> subscribes in <c>OnEnable</c>, and <c>OnEnable</c> never fires in
    /// EditMode — a fixture there would drive a component that had not started listening and would
    /// pass by driving nothing.</para>
    ///
    /// <para><b>The renderer is a double, and that is the honest boundary.</b> What is under test is
    /// which LEVEL gets asked for; whether the GPU then draws it is the shader's claim, measured in
    /// pixels by the interior-mesh spike's render fixture (0 px across the keyword boundary, 0 px
    /// for the level's own faces with the gate on). A PlayMode journey cannot adjudicate pixels
    /// headless anyway — this repo's known caveat is that these journeys are run without a graphics
    /// device in CI, so anything that needs a real frame is measured on the 4060 instead. Keeping
    /// the double here means the test says exactly what it can prove.</para>
    ///
    /// <para>The hull defs are built in memory rather than loaded: the committed batch-1 assets are
    /// the subject of <c>HullCutawayAssetTests</c>, and a behaviour fixture that loaded them would
    /// go red for a stale bake while claiming the gate was broken.</para>
    /// </summary>
    public sealed class BoatCutawayPlayTests
    {
        // The lobster's own vocabulary, but nothing here depends on the numbers being HERS — they
        // are the def's, built below, and the point is that the tag asked for is the tag the table
        // holds. Real ids are used only so a failure message reads like a boat.
        private const int HouseTag = 3;
        private const int CuddyTag = 4;
        private const int CockpitTag = 1;   // an OPEN level — declared open, never cut

        private readonly List<Object> _spawned = new();

        /// <summary>A stand-in for the facet renderer: records what the gate asked for. Implements
        /// only <see cref="IHullCutaway"/>, which is the whole reason that seam is a second
        /// interface rather than a widening of <c>IHullMeshRenderer</c> — a double for one
        /// capability does not have to fake the other twenty.</summary>
        private sealed class RecordingCutaway : MonoBehaviour, IHullCutaway
        {
            public bool Tagged = true;
            public int Level;
            public int Writes;

            public bool CarriesLevelTags => Tagged;
            public int CutawayLevel => Level;

            public void ShowCutawayLevel(int levelTag)
            {
                if (!Tagged) levelTag = 0;
                if (levelTag == Level) return;   // mirrors the real renderer's idempotence
                Level = levelTag;
                Writes++;
            }
        }

        [SetUp]
        public void SetUp()
        {
            // ⚠️ Clear() UNSUBSCRIBES every listener on the channel, so it runs BEFORE any cutaway in
            // this fixture exists. A component built after this point subscribes for itself, exactly
            // as it does in the game.
            EventBus.Clear<CabinEntered>();
            EventBus.Clear<CabinLeft>();
            EventBus.Clear<ControlModeChanged>();
            GameServices.Helm.Reset();

            Spawn("Listener").AddComponent<AudioListener>();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Helm.Reset();
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        // ---- the fixture --------------------------------------------------------------------

        private sealed class Rig
        {
            public GameObject Root;
            public BoatCutaway Cutaway;
            public RecordingCutaway Renderer;
            public BoatInterior Cabin;
            public object HullToken;
            public EntityId HullId;
        }

        /// <summary>
        /// A hull with a two-level cabin and a cutaway wired to a recording renderer. The interior
        /// def's LEVEL ORDER is deliberately NOT the mesh table's tag order — house is def index 0
        /// and tag 3, cuddy is def index 1 and tag 4 — so a fixture that resolved by index instead of
        /// by the def's id would still be internally consistent and would still be wrong. That
        /// substitution is the shipped defect this join exists to prevent.
        /// </summary>
        private Rig BuildHull(string name = "Hull")
        {
            var mesh = ScriptableObject.CreateInstance<HullMeshDef>();
            _spawned.Add(mesh);
            mesh.Id = "hullmesh.test_" + name.ToLowerInvariant();
            mesh.LevelTags = new[]
            {
                new HullMeshDef.LevelTag { LevelId = "house", DeckId = "house_sole", Tag = HouseTag,
                                           Enclosed = true, SoleZMeters = 0.5f, CeilingZMeters = 2.9f },
                new HullMeshDef.LevelTag { LevelId = "cuddy", DeckId = "cuddy_sole", Tag = CuddyTag,
                                           Enclosed = true, SoleZMeters = 0.24f, CeilingZMeters = 2.014f },
                new HullMeshDef.LevelTag { LevelId = "cockpit", DeckId = "cockpit", Tag = CockpitTag,
                                           Enclosed = false, SoleZMeters = 0.5f },
            };

            var interior = ScriptableObject.CreateInstance<BoatInteriorDef>();
            _spawned.Add(interior);
            interior.Id = "interior.test_" + name.ToLowerInvariant();
            interior.PixelsPerMetre = 32;
            interior.Levels = new[]
            {
                Level("house_sole", 0.5f),
                Level("cuddy_sole", 0.24f),
                Level("cockpit", 0.5f),
            };

            GameObject root = Spawn(name);
            var cabin = root.AddComponent<BoatInterior>();
            cabin.Configure(interior, exterior: null, interior: null, fittings: null,
                            interiorPivot: null, boatRoot: root.transform,
                            cells: null, facings: 8, cellsAreCounterClockwise: true,
                            zeroHeadingDegrees: 0f, deckRollDegrees: 0f, deckHeavePixels: 0f,
                            deckPitchLiftMeters: 0f);

            var rendererGo = new GameObject("FacetMesh");
            rendererGo.transform.SetParent(root.transform, false);
            var recorder = rendererGo.AddComponent<RecordingCutaway>();

            // The helm identity token: any object will do here, because Core never dereferences it —
            // HelmSlot compares by reference and nothing else. In the game it is the BoatController.
            var token = new object();

            var cutaway = root.AddComponent<BoatCutaway>();
            cutaway.Configure(cabin, mesh, root.transform, token);

            return new Rig
            {
                Root = root, Cutaway = cutaway, Renderer = recorder, Cabin = cabin,
                HullToken = token, HullId = root.GetEntityId(),
            };
        }

        private static BoatInteriorLevel Level(string id, float soleZ) => new BoatInteriorLevel
        {
            Id = id,
            SoleZMeters = soleZ,
            // Four corners: IsUsable() wants three, and a square is the cheapest honest outline.
            Outline = new[] { new Vector2(-1, -2), new Vector2(1, -2), new Vector2(1, 2), new Vector2(-1, 2) },
        };

        // ---- the four states ----------------------------------------------------------------

        [Test]
        public void SheStartsWhole_BeforeAnybodyHasBoardedHer()
        {
            Rig hull = BuildHull();
            Assert.AreEqual(0, hull.Cutaway.RequestedLevelTag);
            Assert.AreEqual(0, hull.Renderer.Level, "A boat nobody is inside is drawn whole.");
            Assert.IsFalse(hull.Cutaway.OccupantIsBelow);
        }

        [Test]
        public void GoingBelow_CutsAwayThatLevelsFaces_ByTheDefsIdAndNotItsIndex()
        {
            Rig hull = BuildHull();

            EventBus.Publish(new CabinEntered(hull.HullId, 0));      // def index 0 = house_sole
            Assert.AreEqual(HouseTag, hull.Renderer.Level,
                "Entering the wheelhouse must cut the HOUSE. Tag 3, at def index 0 — a gate that " +
                "resolved by index would ask for 0 and open nothing.");

            EventBus.Publish(new CabinEntered(hull.HullId, 1));      // a companionway taken while below
            Assert.AreEqual(CuddyTag, hull.Renderer.Level,
                "Moving to another level re-publishes CabinEntered; the cut must follow her.");
        }

        [Test]
        public void ComingBackOnDeck_ClosesHerUp()
        {
            Rig hull = BuildHull();
            EventBus.Publish(new CabinEntered(hull.HullId, 0));
            EventBus.Publish(new CabinLeft(hull.HullId));

            Assert.AreEqual(0, hull.Renderer.Level, "Player out on deck: exterior only (the ruling).");
            Assert.IsFalse(hull.Cutaway.OccupantIsBelow);
        }

        /// <summary>
        /// <b>At the helm, exterior only</b> — the ruling's second sentence, and the one that needs a
        /// second input. Taking the wheel moves nobody: the cabin publishes nothing, so a gate driven
        /// by <c>CabinSignals</c> alone would leave the house standing open around a player at the
        /// helm.
        /// </summary>
        [Test]
        public void TakingThisHullsHelmWhileBelow_ClosesHerUp_AndGivingItUpOpensHerAgain()
        {
            Rig hull = BuildHull();
            EventBus.Publish(new CabinEntered(hull.HullId, 0));
            Assert.AreEqual(HouseTag, hull.Renderer.Level);

            // ControlSwitcher writes the piloted hull from its Mode setter and publishes the mode
            // AFTER, so a listener reading the slot on the signal is never a frame stale. This is
            // that order.
            GameServices.Helm.SetPilotedHull(hull.HullToken);
            EventBus.Publish(new ControlModeChanged(ControlMode.Aboard));
            Assert.AreEqual(0, hull.Renderer.Level, "At the helm: exterior only (the ruling).");
            Assert.IsTrue(hull.Cutaway.OccupantIsBelow,
                "She has not left the cabin — the house is shut because she is steering, and those " +
                "are two different facts.");

            GameServices.Helm.SetPilotedHull(null);
            EventBus.Publish(new ControlModeChanged(ControlMode.OnDeck));
            Assert.AreEqual(HouseTag, hull.Renderer.Level,
                "Stepping back from the wheel while still below opens the house again.");
        }

        [Test]
        public void SomebodyElsesWheel_DoesNotCloseThisHull()
        {
            Rig hull = BuildHull("Mine");
            EventBus.Publish(new CabinEntered(hull.HullId, 0));

            GameServices.Helm.SetPilotedHull(new object());          // a different boat's helm
            EventBus.Publish(new ControlModeChanged(ControlMode.Aboard));

            Assert.AreEqual(HouseTag, hull.Renderer.Level,
                "The player is steering some OTHER hull. This one's house stays open — she is still " +
                "standing in it.");
        }

        /// <summary>
        /// <b>A cabin on another boat is not this boat's business.</b> Eighteen lobster boats can be
        /// afloat in one creek and most of them share a def, so a hull that took every
        /// <c>CabinEntered</c> as its own would open her house because somebody boarded a sister ship
        /// two berths down — and the tag would MATCH, because sisters share a mesh table.
        /// </summary>
        [Test]
        public void ACabinEnteredOnAnotherBoat_LeavesThisHullExactlyAsSheWas()
        {
            Rig mine = BuildHull("Mine");
            Rig hers = BuildHull("Hers");

            EventBus.Publish(new CabinEntered(hers.HullId, 0));

            Assert.AreEqual(HouseTag, hers.Renderer.Level);
            Assert.AreEqual(0, mine.Renderer.Level);
            Assert.AreEqual(0, mine.Renderer.Writes,
                "Not merely the same answer — this hull was never written to at all.");
        }

        /// <summary>
        /// <b>An OPEN level is refused.</b> The lobster's cockpit is a walkable level with a declared
        /// open sky; cutting one would be cutting the sky. The refusal answers 0, which is the same
        /// value as "gate off" — a cutaway that does not happen is a missing feature, one that
        /// happens to the wrong room is a broken boat.
        /// </summary>
        [Test]
        public void AnOpenLevel_IsNeverCut()
        {
            Rig hull = BuildHull();
            EventBus.Publish(new CabinEntered(hull.HullId, 2));      // def index 2 = cockpit, OPEN

            Assert.AreEqual(0, hull.Renderer.Level);
            Assert.IsTrue(hull.Cutaway.OccupantIsBelow,
                "She is on that level; it simply has no roof to take off.");
        }

        [Test]
        public void AHullWhoseMeshCarriesNoTags_StaysWhole()
        {
            Rig hull = BuildHull();
            hull.Renderer.Tagged = false;                            // baked before the cutaway kit

            EventBus.Publish(new CabinEntered(hull.HullId, 0));

            Assert.AreEqual(0, hull.Renderer.Level,
                "Most of the fleet has no cutaway geometry and never will. That is data, not a fault, " +
                "and it must degrade to the shipped picture rather than to a half-cut boat.");
            Assert.AreEqual(HouseTag, hull.Cutaway.RequestedLevelTag,
                "And the refusal belongs to the RENDERER, which is the only thing that has met the " +
                "mesh. The def still says which level she is on; splitting those two facts is what " +
                "keeps a half-re-baked project from being wrong in a NEW way.");
        }

        /// <summary>
        /// <b>The cut survives a region hop.</b> Root-toggling IS how a region hop works, and the
        /// player can cross a boundary while below. A component that forgot here would put her back
        /// inside a closed house at every boundary she crossed — the same defect ADR 0038 proposal 4
        /// rules out for the layer swap itself.
        /// </summary>
        [Test]
        public void TheCutSurvivesTheRootBeingToggled()
        {
            Rig hull = BuildHull();
            EventBus.Publish(new CabinEntered(hull.HullId, 0));
            Assert.AreEqual(HouseTag, hull.Renderer.Level);

            hull.Root.SetActive(false);
            hull.Renderer.Level = 0;              // the renderer is rebuilt/reset by the hop
            hull.Root.SetActive(true);

            Assert.AreEqual(HouseTag, hull.Renderer.Level,
                "OnEnable must RE-ASSERT the cut, not merely stop forgetting it.");
        }

        [Test]
        public void ADisabledCutaway_StopsListening()
        {
            Rig hull = BuildHull();
            hull.Root.SetActive(false);

            EventBus.Publish(new CabinEntered(hull.HullId, 0));

            Assert.AreEqual(0, hull.Renderer.Level,
                "A torn-down hull must not be writing to a renderer on the strength of a signal " +
                "aimed at the boat she used to be.");
        }
    }
}
