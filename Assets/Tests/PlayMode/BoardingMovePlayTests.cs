using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The boarding MOVE, live</b> — through the real component lifecycle, the real
    /// <see cref="ControlSwitcher"/> transition and the real drawn-facing read (owner ask 2026-08-06:
    /// the fisher climbs aboard instead of being teleported).
    ///
    /// <para>The claims the EditMode machine cannot make on its own all come down to one thing: <b>the
    /// hull does not hold still</b>. She rocks, she turns and she drifts for the whole half-second the
    /// fisher is in the air, so every waypoint on her is a boat-frame fact re-projected each tick rather
    /// than a world point captured at the key-press. A test that boarded a parked boat would pass just as
    /// happily while the landing missed a moving one by a metre — which is the whole failure this arc
    /// could plausibly introduce.</para>
    ///
    /// <para><b>Frames are not seconds.</b> The move is measured in seconds, so these advance it by
    /// elapsed <c>Time.deltaTime</c> and never by a frame count; a slow runner takes fewer frames to the
    /// same landing, and that must not be a failure.</para>
    /// </summary>
    public class BoardingMovePlayTests
    {
        const float Iso = 40f;                       // every iso rig bakes here

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            InteractionGate.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        GameObject NewGo(string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            _spawned.Add(g);
            return g;
        }

        /// <summary>The lobster boat's shape in her own metres — a cockpit sole aft and a raised foredeck
        /// forward, plus the WASHBOARD strips the boarding move steps over on the way in. Built through
        /// the very <see cref="DeckArea.From"/> bake the sidecar importer uses.</summary>
        BoatDeckDef LobsterLikeDeck()
        {
            var def = ScriptableObject.CreateInstance<BoatDeckDef>();
            def.Id = "deck.boarding_move_test";
            def.Areas = new[]
            {
                DeckArea.From("cockpit_sole", DeckAreaKind.Deck, new[]
                {
                    new Vector3(-1.7f, -6f, 0.5f), new Vector3(1.7f, -6f, 0.5f),
                    new Vector3(1.7f, 0.55f, 0.5f), new Vector3(-1.7f, 0.55f, 0.5f),
                }),
                DeckArea.From("foredeck", DeckAreaKind.Deck, new[]
                {
                    new Vector3(-1.2f, 3.85f, 2.1f), new Vector3(1.2f, 3.85f, 2.1f),
                    new Vector3(1.2f, 6.3f, 2.7f), new Vector3(-1.2f, 6.3f, 2.7f),
                }),
                DeckArea.From("washboard_star", DeckAreaKind.Washboard, new[]
                {
                    new Vector3(1.7f, -6f, 1.1f), new Vector3(2.1f, -6f, 1.1f),
                    new Vector3(2.1f, 0.55f, 1.1f), new Vector3(1.7f, 0.55f, 1.1f),
                }),
                DeckArea.From("washboard_port", DeckAreaKind.Washboard, new[]
                {
                    new Vector3(-2.1f, -6f, 1.1f), new Vector3(-1.7f, -6f, 1.1f),
                    new Vector3(-1.7f, 0.55f, 1.1f), new Vector3(-2.1f, 0.55f, 1.1f),
                }),
            };
            def.WalkCenter = new Vector2(0f, 0.15f);
            def.WalkHalfExtents = new Vector2(1.7f, 6.15f);
            _spawned.Add(def);
            return def;
        }

        /// <summary>A minimal snap-directional skin: eight 1×1 facings and a 40° bake elevation, which is
        /// all the deck maths reads off a hull picture (the drawn heading and the artwork's camera).</summary>
        void GiveHerASkin(GameObject boatGo)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _spawned.Add(tex);

            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 32f);
            _spawned.Add(sprite);
            var facings = new Sprite[8];
            for (int i = 0; i < facings.Length; i++) facings[i] = sprite;

            var child = new GameObject("Visual");
            child.transform.SetParent(boatGo.transform, false);
            var sr = child.AddComponent<SpriteRenderer>();

            var directional = boatGo.AddComponent<DirectionalBoatSprite>();
            directional.Configure(facings, sr, zeroHeadingDegrees: 0f, smoothModeSprite: sprite,
                                  mode: DirectionalBoatSprite.RotationMode.SnapDirectional,
                                  facingsAreCounterClockwise: true,      // the iso kits' bake order
                                  bakeElevationDegrees: Iso);
        }

        sealed class Rig
        {
            public ControlSwitcher Switcher;
            public DeckWalkController DeckWalk;
            public PlayerWalkController Walk;
            public GameObject PlayerGo;
            public GameObject BoatGo;
            public BoatDeckDef Deck;
            public Transform DisembarkPoint;
        }

        /// <summary>A measured hull with a skin, a dock zone and a disembark point on the planks, and a
        /// fisher standing off her starboard side within board reach. The switcher's own Update is left
        /// running — it is the pump the move rides on.</summary>
        Rig Build(Vector3 playerPos, Vector3 boatPos)
        {
            var playerGo = NewGo("Player", playerPos);
            var walk = playerGo.AddComponent<PlayerWalkController>();
            var deckWalk = playerGo.AddComponent<DeckWalkController>();
            deckWalk.enabled = false;

            var boatGo = NewGo("Boat", boatPos);
            var boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            GiveHerASkin(boatGo);

            BoatDeckDef deck = LobsterLikeDeck();
            BoatDeckAreas.Write(boatGo, deck);

            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory";                       // the id the repair gate keys off
            hull.DisplayName = "Boarding Move Test";
            hull.CameraWorldHeightMeters = 14f;
            _spawned.Add(hull);
            boat.SetHull(hull);

            walk.enabled = true; boat.enabled = false; input.enabled = false;

            var dock = NewGo("DockZone", boatPos + new Vector3(0f, 2f, 0f));
            var disembark = NewGo("Disembark", boatPos + new Vector3(0f, 4f, 0f));
            var swGo = NewGo("Switcher", Vector3.zero);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, 3f, disembark.transform);
            sw.ConfigureBoardingMove(enabled: true, approachSpeed: 3f, approachMaxSeconds: 0.8f,
                                     vaultSeconds: 0.5f, vaultHopMeters: 0.35f);

            return new Rig { Switcher = sw, DeckWalk = deckWalk, Walk = walk, PlayerGo = playerGo,
                             BoatGo = boatGo, Deck = deck, DisembarkPoint = disembark.transform };
        }

        /// <summary>Pump real frames until the move lands, in SECONDS — a frame count would be pinning the
        /// runner rather than the move. Optionally works the hull while the fisher is in the air.
        ///
        /// <para>Deliberately assertion-free: it is yielded as a NESTED coroutine, and Unity's coroutine
        /// scheduler can turn an exception thrown inside one into a logged error rather than a test
        /// failure. The time limit only stops it hanging; every caller states its own post-conditions.</para>
        /// </summary>
        IEnumerator RunTheMoveOut(Rig r, System.Action<float> workTheHull = null, float limitSeconds = 5f)
        {
            float spent = 0f;
            while (r.Switcher.IsBoardingMove && spent < limitSeconds)
            {
                yield return null;
                spent += Time.deltaTime;
                workTheHull?.Invoke(spent);
            }
            yield return null;              // one more Update: the deck walk steps, clamps and publishes
        }

        /// <summary>Rock, turn and drift her the way a real hull behaves under a boarding fisher.</summary>
        static void WorkTheHull(GameObject boatGo, Vector3 origin, float t)
        {
            boatGo.transform.position = origin + new Vector3(Mathf.Sin(t * 3f) * 0.25f, t * 0.6f, 0f);
            boatGo.transform.rotation = Quaternion.Euler(0f, 0f, -t * 60f);
        }

        /// <summary>Assert the fisher is standing on the deck in the two halves that matter: the hull-frame
        /// position lies on one of her <c>DECK</c> polygons, and the transform is the honest FORWARD
        /// projection of exactly that point at that area's height in that facing. (Forward-projecting, not
        /// inverting: the inverse is a converging iteration, so a legitimately-clamped point on a SLOPED
        /// area reads as off the deck — a false negative in the check itself.)</summary>
        static void AssertStandingOnDeck(DeckWalkController walk, Transform player, Transform boat,
                                         float headingDeg, string who)
        {
            BoatDeckDef deck = walk.Deck;
            Assert.IsNotNull(deck, $"{who}: the walk resolved no deck at all");

            Vector2 local = walk.DeckLocalPosition;
            float height = 0f;
            bool onDeck = false;
            foreach (DeckArea a in deck.Areas)
            {
                if (a.Kind != DeckAreaKind.Deck) continue;
                DeckAreaMath.ClosestPointOnOutline(a.Outline, local, out float sqr);
                if (!DeckAreaMath.Contains(a.Outline, a.Bounds, local) && sqr > 1e-6f) continue;
                height = DeckAreaMath.HeightAt(a.HeightPlane, local);
                onDeck = true;
                break;
            }
            Assert.IsTrue(onDeck, $"{who}: hull-frame {local} is not on any DECK area of {deck.Id}");

            Vector2 expected = DeckAreaMath.DeckToWorld(local, height, headingDeg, Iso);
            Vector2 actual = (Vector2)player.position - (Vector2)boat.position;
            Assert.Less(Vector2.Distance(expected, actual), 1e-3f,
                $"{who}: standing at hull-frame {local} (height {height:0.00} m) should draw at {expected}, " +
                $"but the fisher is at {actual}");
        }

        // ---------------------------------------------------------------------------------------------

        /// <summary>The headline claim: E plays a MOVE, and a hull that rocks, turns and drifts for the
        /// whole of it still gets landed on. The landing spot is a boat-frame fact re-projected every
        /// tick — capture it as a world point at the key-press and this test puts the fisher in the water
        /// beside her.</summary>
        [UnityTest]
        public IEnumerator BoardingLandsInsideHerAuthoredDeck_OnAHullThatNeverHoldsStill()
        {
            var r = Build(new Vector3(2.6f, 0f, 0f), Vector3.zero);
            Vector3 origin = r.BoatGo.transform.position;
            Vector3 stoodAt = r.PlayerGo.transform.position;
            yield return null;

            Assert.IsTrue(r.Switcher.BeginInteract(), "E within reach starts the boarding move");
            Assert.IsTrue(r.Switcher.IsBoardingMove);
            Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode, "nothing has flipped — they are on their way");

            yield return RunTheMoveOut(r, t => WorkTheHull(r.BoatGo, origin, t));

            Assert.IsFalse(r.Switcher.IsBoardingMove, "the move landed rather than running out its guard");
            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode, "landing aboard is what ends the move");
            Assert.AreSame(r.BoatGo.transform, r.PlayerGo.transform.parent, "and now they ride her");
            Assert.Greater(Vector3.Distance(r.PlayerGo.transform.position, stoodAt), 0.5f,
                "the fisher travelled — they are not where they pressed E");

            float heading = r.BoatGo.GetComponent<DirectionalBoatSprite>().DrawnHeadingDegrees();
            AssertStandingOnDeck(r.DeckWalk, r.PlayerGo.transform, r.BoatGo.transform, heading,
                                 "after the boarding move");
        }

        /// <summary>The fisher is genuinely between places for the length of the arc: visible, driven by
        /// nothing but the move, riding no deck, and — because they have not landed — not yet anybody's
        /// idea of a deckhand.</summary>
        [UnityTest]
        public IEnumerator MidArc_TheFisherIsVisible_Unparented_AndNotYetAboard()
        {
            var modes = new List<ControlModeChanged>();
            void OnMode(ControlModeChanged e) => modes.Add(e);
            EventBus.Subscribe<ControlModeChanged>(OnMode);
            try
            {
                var r = Build(new Vector3(2.6f, 0f, 0f), Vector3.zero);
                yield return null;
                Vector3 stoodAt = r.PlayerGo.transform.position;

                Assert.IsTrue(r.Switcher.BeginInteract());

                // Every one of these is a per-frame invariant of being mid-arc, so they are asserted on
                // every in-flight frame rather than once after a loop that may already have landed.
                float spent = 0f;
                int inFlightFrames = 0;
                bool sawTravel = false;
                while (r.Switcher.IsBoardingMove && spent < 0.35f)
                {
                    yield return null;
                    spent += Time.deltaTime;
                    if (!r.Switcher.IsBoardingMove) break;
                    inFlightFrames++;

                    Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode, "not aboard until they land");
                    Assert.IsNull(r.PlayerGo.transform.parent, "airborne, they ride nobody's deck");
                    Assert.IsFalse(r.Walk.enabled, "the walk keys are dead — the move owns the body");
                    Assert.IsFalse(r.DeckWalk.enabled, "and the deck walk is not driving them either");
                    Assert.IsTrue(r.PlayerGo.GetComponent<SpriteRenderer>().enabled,
                        "the fisher is drawn for every frame of the move — that is the whole point");
                    Assert.IsTrue(InteractionGate.IsBlocked, "the move owns the interact key while it runs");
                    Assert.IsFalse(DeckStance.IsActive,
                        "nobody in mid-air is standing on a deck, so no deck stance is published");
                    Assert.AreEqual(0, modes.Count, "and not one ControlModeChanged has gone out yet");
                    if (Vector3.Distance(r.PlayerGo.transform.position, stoodAt) > 0.1f) sawTravel = true;
                }

                Assert.Greater(inFlightFrames, 0, "the move should still have been running to observe");
                Assert.IsTrue(sawTravel, "the fisher visibly moved during the arc");

                yield return RunTheMoveOut(r);
                Assert.IsFalse(r.Switcher.IsBoardingMove, "the move landed");
                Assert.AreEqual(1, modes.Count, "exactly ONE flip, at the far end");
                Assert.AreEqual(ControlMode.OnDeck, modes[0].Mode);
                Assert.IsFalse(InteractionGate.IsBlocked, "and the interact key comes straight back");
            }
            finally
            {
                EventBus.Unsubscribe<ControlModeChanged>(OnMode);
            }
        }

        /// <summary>Stepping ashore is the same move mirrored, and it must finish exactly where
        /// <c>Disembark()</c> places the fisher — the dock planks — with no pop at the join.</summary>
        [UnityTest]
        public IEnumerator SteppingAshoreArcsDownAndReachesTheDisembarkPoint()
        {
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };   // bared flats → standable
            GameServices.Environment = new FlatEnv { Level = 0f };

            var r = Build(new Vector3(2.6f, 0f, 0f), Vector3.zero);
            yield return null;
            r.Switcher.TryInteract();                                   // aboard instantly; this is about leaving
            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode);
            yield return null;

            Assert.IsFalse(r.Switcher.WithinHelmReach(), "not at the tiller, so E means 'step ashore'");
            Assert.IsTrue(r.Switcher.BeginInteract(), "…and starts the move ashore");
            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode, "still hers until their feet land");
            Assert.IsNull(r.PlayerGo.transform.parent, "but they have let go of her");

            yield return RunTheMoveOut(r);

            Assert.IsFalse(r.Switcher.IsBoardingMove, "the move landed rather than running out its guard");
            Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode);
            Assert.Less(Vector3.Distance(r.PlayerGo.transform.position, r.DisembarkPoint.position), 1e-3f,
                "the arc lands them exactly on the planks Disembark() places them at");
            Assert.IsTrue(r.Walk.enabled, "with their legs back");
            Assert.IsTrue(r.PlayerGo.GetComponent<Rigidbody2D>().simulated, "and their physics restored");
            Assert.IsFalse(DeckStance.IsActive, "ashore there is no deck stance (dock parity)");
        }

        /// <summary>The gates are law mid-arc: a hull that stops being boardable under a fisher in mid-air
        /// is not boarded, and the fisher is put back down where they set off from.</summary>
        [UnityTest]
        public IEnumerator TheGatesHoldMidArc_AnUnboardableHullIsNeverBoarded()
        {
            var save = SaveMigration.NewGame();
            save.OwnedBoats.Add("boat.dory");
            RepairLedger.MarkRepaired(save, "boat.dory");                // sound when they set off…
            GameServices.Save = new FakeSaveService(save);

            var r = Build(new Vector3(2.6f, 0f, 0f), Vector3.zero);
            yield return null;
            Vector3 stoodAt = r.PlayerGo.transform.position;

            Assert.IsTrue(r.Switcher.BeginInteract());
            float spent = 0f;
            while (r.Switcher.IsBoardingMove && spent < 0.2f) { yield return null; spent += Time.deltaTime; }
            Assert.IsTrue(r.Switcher.IsBoardingMove, "mid-arc");

            save.RepairedBoats.Remove("boat.dory");                      // …and not, while they are in the air
            Assert.IsFalse(r.Switcher.BoardableNow(), "the gate has closed under them");

            yield return null;                                           // the switcher's own Update notices

            Assert.IsFalse(r.Switcher.IsBoardingMove, "the move is called off");
            Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode, "she is not boarded");
            Assert.IsNull(r.PlayerGo.transform.parent);
            Assert.Less(Vector3.Distance(r.PlayerGo.transform.position, stoodAt), 1e-3f,
                "the fisher is put back down where they set off from");
            Assert.IsTrue(r.Walk.enabled, "with their own legs back");
            Assert.IsFalse(InteractionGate.IsBlocked, "and the interact key handed back");
        }

        /// <summary>A held interact gate that outlived its holder would wedge interaction off for the whole
        /// game — so a switcher torn down mid-vault must hand the key back on the way out.</summary>
        [UnityTest]
        public IEnumerator TornDownMidVault_TheInteractKeyIsStillHandedBack()
        {
            var r = Build(new Vector3(2.6f, 0f, 0f), Vector3.zero);
            yield return null;

            r.Switcher.BeginInteract();
            float spent = 0f;
            while (r.Switcher.IsBoardingMove && spent < 0.2f) { yield return null; spent += Time.deltaTime; }
            Assert.IsTrue(r.Switcher.IsBoardingMove);
            Assert.IsTrue(InteractionGate.IsBlocked);

            r.Switcher.enabled = false;                                  // the rig goes away under the move
            yield return null;

            Assert.IsFalse(InteractionGate.IsBlocked, "the key is handed back on teardown");
            Assert.IsFalse(r.Switcher.IsBoardingMove);
        }
    }
}
