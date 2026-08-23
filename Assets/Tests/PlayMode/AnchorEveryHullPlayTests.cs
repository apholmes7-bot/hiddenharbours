using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>AN ANCHOR ON EVERY HULL</b> — the owner's ruling held on the live loop, across the THREE
    /// hull classes the fleet actually comes in, because each reaches the same tackle by a different
    /// road and the interesting failures are at the joins:
    ///
    /// <list type="bullet">
    ///   <item><b>The rowed dory</b> (<c>Propulsion = Oars</c>, no console). She has no helm at all, so
    ///   there is no switch and no dash — and, critically, <b>no working helm relay to borrow</b>: her
    ///   relay exists (every hull grows one) but is never GRANTED the Core helm slot, because nobody is
    ///   piloting an engine on her. Her tackle answers to the anchor key alone, gated on
    ///   <c>IsPlayersBoat</c>. Anything that reached for <c>IsPlayerHelm</c>, or for
    ///   <c>GameServices.HelmControl</c>, would be dead on the very boat the game opens with.</item>
    ///   <item><b>The console skiff</b> (engine, skiff dash). The switch road: the granted relay answers
    ///   the Core ground-tackle seam and a flick moves the real <see cref="BoatAnchor"/>.</item>
    ///   <item><b>The lobster boat</b> (engine, pilothouse dash). The same road through the OTHER helm
    ///   family, whose switch is the rigs' authored ANCH breaker rather than the skiffs' added bat.</item>
    /// </list>
    ///
    /// <para><b>One tackle, two controls.</b> The last test presses both on the same hull and asserts
    /// they land in the same place — the whole reason the switch calls <c>BoatAnchor.Toggle()</c>
    /// rather than keeping a state of its own.</para>
    ///
    /// <para><b>Time, not frames</b> (the <c>AnchorPlayTests</c> convention): headless frames run as
    /// fast as the machine allows, so any wait here is on elapsed <see cref="Time.time"/>.</para>
    /// </summary>
    public class AnchorEveryHullPlayTests
    {
        /// <summary>A flat painted seabed — 4 m of water at mean level, which every hull below can
        /// anchor in (their rodes run 6 m up).</summary>
        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation = -4f;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class StillEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 11;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Vector2.zero, Vector2.zero, 0f, SeaState.Calm, 1f, 0f);
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private sealed class StoppedClock : IGameClock
        {
            public double TotalSeconds => 0.0;
            public GameTime Now => new GameTime(0.0);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 12f;      // broad daylight: the panel lights stay out of this
            public float DayFraction => 0.5f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            HelmKeyCapture.Reset();
            EventBus.Clear<ControlModeChanged>();
            GameServices.TidalTerrain = new FlatTerrain();
            GameServices.Environment = new StillEnv();
            GameServices.Clock = new StoppedClock();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            InteractionGate.Reset();
            HelmKeyCapture.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- the fixture -----------------------------------------------------------------------

        private GameObject NewBoat(BoatHullDef hull, out BoatAnchor anchor, out AnchorInput input,
                                   out HelmControlRelay relay)
        {
            var go = new GameObject($"Boat:{hull.Id}");
            _spawned.Add(go);
            var boat = go.AddComponent<BoatController>();      // self-installs the tackle, key and relay
            boat.SetHull(hull);

            anchor = go.GetComponent<BoatAnchor>();
            input = go.GetComponent<AnchorInput>();
            relay = go.GetComponent<HelmControlRelay>();
            Assert.IsNotNull(anchor, $"{hull.Id}: BoatController must self-install the ground tackle");
            Assert.IsNotNull(input, $"{hull.Id}: …and the key that works it, on EVERY hull");
            return go;
        }

        /// <summary>Put the player aboard this hull — the wider Core declaration
        /// (<c>ControlSwitcher.PublishBoatOwnership</c>'s own second line), which is the one the tackle
        /// answers to.</summary>
        private static void PlayerAboard(GameObject boat, AnchorInput input, bool atTheHelm)
        {
            var controller = boat.GetComponent<BoatController>();
            GameServices.Helm.SetPlayersBoat(controller);
            GameServices.Helm.SetPilotedHull(atTheHelm ? controller : null);
            input.OnControlModeChanged(new ControlModeChanged(
                atTheHelm ? ControlMode.Aboard : ControlMode.OnDeck));
        }

        /// <summary>The shipped hull classes, loaded from the REAL committed assets: this is a test
        /// about the hulls the game actually ships, so a hull invented in code would prove nothing about
        /// them (the <c>BoatLoadTimePresentationPlayTests</c> convention, guard and all).</summary>
        private static BoatHullDef Hull(string assetName)
        {
#if UNITY_EDITOR
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(
                $"Assets/_Project/Data/Boats/{assetName}.asset");
            Assert.IsNotNull(hull, $"missing hull asset: {assetName}");
            return hull;
#else
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed hulls, not a mirror.");
            return null;
#endif
        }

        // ========= 1. the ROWED dory: no helm, no dash, no relay to borrow ========================

        [UnityTest]
        public IEnumerator TheRowedDory_WorksHerHook_WithNoHelmRelayGrantedAtAll()
        {
            BoatHullDef dory = Hull("Dory");
            Assert.AreEqual(PropulsionType.Oars, dory.Propulsion, "this test is only meaningful on a rowed hull");

            GameObject go = NewBoat(dory, out BoatAnchor anchor, out AnchorInput input, out HelmControlRelay relay);
            yield return null;                                  // Awake / OnEnable across the rig

            // The player is ABOARD her — and nobody is piloting anything, because there is no engine
            // helm on a dory to be at.
            PlayerAboard(go, input, atTheHelm: false);

            // ⭐ THE POINT OF THIS TEST. No helm is granted, so every helm-shaped road to the tackle is
            // a dead end — and the anchor must work anyway.
            Assert.IsNull(GameServices.HelmControl,
                "nobody is piloting an engine, so the Core helm slot is empty — this is the state a " +
                "helm-gated anchor would silently die in");
            Assert.IsFalse(relay.HasHelm, "a dory has no engine helm…");
            Assert.IsFalse(relay.IsPlayerHelm, "…so she is nobody's helm either");
            Assert.IsTrue(GameServices.Helm.IsPlayersBoat(go.GetComponent<BoatController>()),
                "but she IS the player's boat — the wider fact the tackle answers to (#642)");

            Assert.IsTrue(input.AnchorKeyLive, "the key lives on the boat the game opens with");
            Assert.IsTrue(anchor.HasAnchor, "…and she carries a hook to work");

            Assert.AreEqual(AnchorDrop.Set, anchor.TryDrop(), "the dory lets go in 4 m on a 6 m rode");
            Assert.AreEqual(AnchorState.Set, anchor.State);
            anchor.Weigh();
            Assert.AreEqual(AnchorState.Stowed, anchor.State);

            // A small anchor, on a short rode: the bottom of the ladder, in her own data.
            Assert.Greater(dory.AnchorMassKg, 0f);
            Assert.Less(dory.AnchorMassKg, Hull("LobsterBoat").AnchorMassKg,
                "the dory heaves a grapnel; the lobster boat swings a real hook");
        }

        [UnityTest]
        public IEnumerator TheAnchorKey_IsNotEatenByASecondHullInTheScene()
        {
            // The ambient fleet / a rotation rig / a boat for sale: every one of them grows a tackle
            // and a key, and exactly one of them is the player's.
            GameObject mine = NewBoat(Hull("Dory"), out BoatAnchor myAnchor, out AnchorInput myKey, out _);
            NewBoat(Hull("Punt"), out BoatAnchor theirAnchor, out AnchorInput theirKey, out _);
            yield return null;

            PlayerAboard(mine, myKey, atTheHelm: false);
            theirKey.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));

            Assert.IsTrue(myKey.AnchorKeyLive, "my boat's key works my tackle");
            Assert.IsFalse(theirKey.AnchorKeyLive,
                "the other hull in the scene must not eat the press — the declaration names ONE boat");

            Assert.AreEqual(AnchorDrop.Set, myAnchor.TryDrop());
            Assert.AreEqual(AnchorState.Stowed, theirAnchor.State, "and her hook never moved");
        }

        // ========= 2 & 3. the CONSOLE SKIFF and the LOBSTER BOAT: the switch road =================

        [UnityTest]
        public IEnumerator TheDashSwitch_MovesTheRealTackle_OnBothHelmFamilies()
        {
            foreach (string assetName in new[] { "ConsoleSkiff", "LobsterBoat" })
            {
                BoatHullDef hull = Hull(assetName);
                Assert.AreEqual(PropulsionType.Engine, hull.Propulsion, $"{assetName} should be an engine hull");
                Assert.IsTrue(hull.Helm != null, $"{assetName} should carry a helm console");

                GameObject go = NewBoat(hull, out BoatAnchor anchor, out AnchorInput input, out _);
                yield return null;

                PlayerAboard(go, input, atTheHelm: true);

                // The dash reads the GRANTED relay through Core — the same object the overlay holds.
                IHelmControl helm = GameServices.HelmControl;
                Assert.IsNotNull(helm, $"{assetName}: she is being piloted, so a helm is granted");
                Assert.IsTrue(helm.HasAnchor, $"{assetName}: her data says she carries a hook");
                Assert.AreEqual(AnchorState.Stowed, helm.AnchorState, $"{assetName}: catted to start");

                // ⭐ THE FLICK. One call through the Core seam; the tackle itself moves.
                helm.ToggleAnchor();
                Assert.AreEqual(AnchorState.Set, anchor.State,
                    $"{assetName}: the switch let go the real anchor, not a UI-side copy of one");
                Assert.AreEqual(AnchorState.Set, helm.AnchorState,
                    $"{assetName}: …and the switch reads what the tackle says");
                Assert.IsTrue(anchor.IsDown);

                helm.ToggleAnchor();
                Assert.AreEqual(AnchorState.Stowed, anchor.State, $"{assetName}: and weighs her again");
                Assert.AreEqual(AnchorState.Stowed, helm.AnchorState);
            }
        }

        [UnityTest]
        public IEnumerator TheSwitch_RefusesAHullThatIsNotThePlayers_EvenWhenSheIsRightThere()
        {
            // A skipper's boat under way beside you: her relay is live and her tackle is real, and a
            // press on a dash that is not yours must not reach it. The gate is IsPlayersBoat, so this
            // also pins that the relay uses the WIDER fact rather than an accident of the helm slot.
            GameObject theirs = NewBoat(Hull("ConsoleSkiff"), out BoatAnchor theirAnchor,
                                        out AnchorInput theirKey, out HelmControlRelay theirRelay);
            yield return null;

            GameServices.Helm.SetPlayersBoat(null);
            GameServices.Helm.SetPilotedHull(null);

            Assert.IsTrue(theirRelay.HasAnchor, "her data says she carries a hook…");
            theirRelay.ToggleAnchor();
            Assert.AreEqual(AnchorState.Stowed, theirAnchor.State,
                "…and it is not yours to let go");
            Assert.IsFalse(theirKey.AnchorKeyLive);

            // Step aboard her and the same call lands.
            PlayerAboard(theirs, theirKey, atTheHelm: true);
            theirRelay.ToggleAnchor();
            Assert.AreEqual(AnchorState.Set, theirAnchor.State, "aboard, she answers to you");
        }

        // ========= 4. one tackle, two controls ====================================================

        [UnityTest]
        public IEnumerator TheSwitchAndTheKey_AreTheSameVerb_ReachingTheSameHook()
        {
            // A consoled hull carries BOTH controls, and the whole design rests on their never being
            // able to put the hook in two different places: the switch calls the same Toggle() the key
            // does, and neither keeps a state of its own.
            GameObject go = NewBoat(Hull("ConsoleSkiff"), out BoatAnchor anchor, out AnchorInput input, out _);
            yield return null;
            PlayerAboard(go, input, atTheHelm: true);

            IHelmControl helm = GameServices.HelmControl;
            Assert.IsNotNull(helm);

            helm.ToggleAnchor();                       // switch: let go
            Assert.AreEqual(AnchorState.Set, anchor.State);

            anchor.Toggle();                           // key: weigh (the path AnchorInput presses)
            Assert.AreEqual(AnchorState.Stowed, anchor.State);
            Assert.AreEqual(AnchorState.Stowed, helm.AnchorState,
                "the switch reads the tackle, so the key moving it moves the switch too");

            anchor.Toggle();                           // key: let go
            Assert.AreEqual(AnchorState.Set, helm.AnchorState, "…in both directions");

            helm.ToggleAnchor();                       // switch: weigh
            Assert.AreEqual(AnchorState.Stowed, anchor.State);
        }
    }
}
