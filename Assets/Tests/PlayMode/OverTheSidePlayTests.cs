using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>OVER THE SIDE</b> — the owner's 2026-09-02 ask, driven end to end: <i>"players should be
    /// able to exit the boat into the water off the wash boards, maybe one button to get on the
    /// washboard and then it depends which way you face when you place the next button, either in the
    /// boat or in the water if facing each."</i>
    ///
    /// <para><b>⚠ The charter named this journey "on the dory" — and the dory has no washboards.</b>
    /// Only the cape islander and the lobster family author <c>DeckAreaKind.Washboard</c> areas; the
    /// starter dory, the punt and the skiffs author none, which is DATA, not an omission — an open boat
    /// has no side deck to climb onto. So the verb falls back to a derived gunwale band on those hulls,
    /// and this file runs the journey on BOTH: the dory (derived band) and the cape (authored
    /// washboards), because a verb that only works on the boats the rig happened to draw side decks for
    /// is not the verb the owner asked for.</para>
    /// </summary>
    public class OverTheSidePlayTests
    {
        private sealed class FixedTide : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private readonly List<Object> _spawned = new List<Object>();
        private ControlSwitcher _switcher;
        private DeckWalkController _walk;
        private BoatController _boat;
        private GameObject _playerGo;
        private FixedTide _tide;
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            StandableSurfaces.Clear();
            InteractionGate.Reset();

            _tide = new FixedTide { Level = StPetersBuilder.TideMean };
            GameServices.Environment = _tide;

            var terrainGo = Spawn("TidalTerrain");
            var terrain = terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);
            GameServices.TidalTerrain = terrain;

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(_config);
            GameServices.Config = _config;

            _playerGo = Spawn("Player");
            _playerGo.AddComponent<SpriteRenderer>();
            var playerWalk = _playerGo.AddComponent<PlayerWalkController>();
            _walk = _playerGo.AddComponent<DeckWalkController>();
            _playerGo.AddComponent<DeckRiderVisual>();   // the shipping facing source this test overrides
            GameServices.PlayerTransform = _playerGo.transform;

            var boatGo = Spawn("Boat");
            boatGo.transform.position = StPetersBuilder.DoryMooredPos;
            boatGo.transform.rotation =
                Quaternion.Euler(0f, 0f, -StPetersBuilder.DoryMooredHeadingDegrees);
            _boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory";
            hull.LengthMeters = StPetersBuilder.DoryLengthMetres;
            hull.DraughtMeters = 0.3f;
            hull.CameraWorldHeightMeters = 14f;
            hull.Propulsion = PropulsionType.Oars;
            _spawned.Add(hull);
            _boat.SetHull(hull);
            _boat.enabled = false; input.enabled = false;

            var dockZone = Spawn("DockZone");
            dockZone.transform.position = StPetersBuilder.DockZonePos;   // the ARRIVAL's berth, far off
            var disembark = Spawn("Disembark");
            disembark.transform.position = StPetersBuilder.DisembarkPos;

            _switcher = Spawn("Switcher").AddComponent<ControlSwitcher>();
            _switcher.Configure(playerWalk, _boat, input, dockZone.transform,
                                StPetersBuilder.DockZoneRadius, disembark.transform);
        }

        [TearDown]
        public void TearDown()
        {
            StandableSurfaces.Clear();
            InteractionGate.Reset();
            GameServices.PlayerTransform = null;
            GameServices.Config = null;
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private void AttachDeck(string deckAssetName)
        {
            string path = $"Assets/_Project/Data/Boats/Decks/{deckAssetName}.asset";
            var deck = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(path);
            Assert.IsNotNull(deck, $"the authored deck {path} must exist");
            var host = _boat.GetComponent<BoatDeckAreas>() ?? _boat.gameObject.AddComponent<BoatDeckAreas>();
            host.Configure(deck);
        }

        /// <summary>Put her aboard and standing on the deck, the way the shipping verb would.</summary>
        private IEnumerator BoardHer()
        {
            _playerGo.transform.position = _boat.transform.position;
            yield return null;
            Assert.IsTrue(_switcher.BeginInteract(), "she must be able to board from alongside");
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_switcher.IsBoardingMove && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode, "premise: she is on deck");
        }

        /// <summary>Point her at a deck bearing — the drawn facing the second press reads. Injected
        /// rather than acted out, because a virtual keypress is undeliverable to headless input; the
        /// DECISION is still OverTheSideMath's.</summary>
        private void FaceDeckBearing(float bearing) => _switcher.ConfigureDeckFacing(() => bearing);

        // =============================================================================================
        //  1. ⭐ the journey, on BOTH kinds of hull
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Deck → washboard → over the side.</b> Two presses, and the second one is the facing.
        /// Run on the open dory (a derived gunwale band) and on the cape islander (real authored
        /// washboards), because the owner's boat is the first kind.
        /// </summary>
        [UnityTest]
        public IEnumerator DeckToWashboardToTheWater([Values("DoryIso", "CapeIslanderIso")] string deck)
        {
            AttachDeck(deck);
            yield return BoardHer();

            // PRESS ONE — out onto the rail.
            Assert.IsTrue(_switcher.BeginInteract(), $"[{deck}] E on deck must put her on the rail");
            yield return null;
            Assert.IsTrue(_switcher.OnWashboard, $"[{deck}] …and she must be standing on it");
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode,
                $"[{deck}] the rail is a PLACE on the deck, not a mode of its own");

            // ⚠ …and she must STAY there. The walk clamps to her deck polygons, which exclude the
            // washboard: without the box-clamp rule the next tick drags her back inboard and the verb
            // reads as having done nothing at all.
            Vector2 onRail = _walk.DeckLocalPosition;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(onRail.x, _walk.DeckLocalPosition.x, 0.25f,
                $"[{deck}] she slid off the rail on her own — the deck clamp is pulling her back inboard");

            // PRESS TWO, facing the sea.
            FaceDeckBearing(OutboardBearing());
            yield return null;
            Assert.IsTrue(_switcher.BeginInteract(), $"[{deck}] facing outboard, E must put her over");
            yield return null;

            Assert.AreEqual(ControlMode.OnFoot, _switcher.Mode, $"[{deck}] over the side means ON FOOT");
            Assert.IsFalse(_switcher.OnWashboard, $"[{deck}] …and no longer on the rail");
            Assert.IsFalse(_walk.OnWashboard, $"[{deck}] …the walk must be told too, or she walks a rail " +
                                              "on a boat she is no longer standing on");
        }

        /// <summary>
        /// ⭐⭐ <b>Facing INBOARD steps her back into the boat</b> — the other half of the same press, and
        /// the half that makes the verb safe to have.
        /// </summary>
        [UnityTest]
        public IEnumerator OnTheRailFacingInboard_ShePutsHerselfBackOnDeck()
        {
            AttachDeck("DoryIso");
            yield return BoardHer();

            Assert.IsTrue(_switcher.BeginInteract(), "onto the rail");
            yield return null;
            Assert.IsTrue(_switcher.OnWashboard);

            FaceDeckBearing(OutboardBearing() + 180f);            // …turn round and look inboard
            yield return null;
            Assert.IsTrue(_switcher.BeginInteract(), "facing inboard, E must take her back on deck");
            yield return null;

            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode, "she is still aboard");
            Assert.IsFalse(_switcher.OnWashboard, "…and off the rail");
            Assert.IsFalse(_walk.OnWashboard);
        }

        /// <summary>
        /// ⭐⭐ <b>The tie: facing ALONG the rail keeps her aboard.</b> The safety property the whole verb
        /// rests on — the water under this berth is 4 m deep, and walking the gunwale is not a decision
        /// to swim.
        /// </summary>
        [UnityTest]
        public IEnumerator FacingAlongTheRail_NeverPutsHerInTheWater()
        {
            AttachDeck("DoryIso");
            yield return BoardHer();

            Assert.IsTrue(_switcher.BeginInteract(), "onto the rail");
            yield return null;

            foreach (float alongTheRail in new[] { OutboardBearing() + 90f, OutboardBearing() - 90f })
            {
                FaceDeckBearing(alongTheRail);
                yield return null;
                _switcher.BeginInteract();
                yield return null;

                Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode,
                    $"facing {alongTheRail:F0}° — straight along the rail — put her in the sea. A tie " +
                    "resolves INBOARD: nobody swims by accident.");

                // Back out onto the rail for the second half of the loop.
                if (!_switcher.OnWashboard) { _switcher.BeginInteract(); yield return null; }
            }
        }

        // =============================================================================================
        //  2. the ladder's order, and the rungs above it
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>At a wharf, E still steps her ASHORE.</b> The 08-25 deck ladder grows one rung on the
        /// END — helm → registry → step ashore → washboard — so adding a way into the water cannot take
        /// the press away from the planks.
        /// </summary>
        [UnityTest]
        public IEnumerator AtAWharf_ThePressStillStepsAshore_NotOverTheSide()
        {
            var pier = Spawn("Wharf").AddComponent<StandablePlatform>();
            pier.Configure("wharf.st_peters", StPetersWharf.DeckFootprint(),
                           StPetersWharf.DeckElevationFrom(GameServices.TidalTerrain));
            AttachDeck("DoryIso");
            yield return BoardHer();

            Assert.IsTrue(_switcher.CanStepAshore(), "premise: she is lying against the planks");

            FaceDeckBearing(OutboardBearing());                   // …even facing straight at the sea
            yield return null;
            Assert.IsTrue(_switcher.BeginInteract(), "E must do something");
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_switcher.IsBoardingMove && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;

            Assert.IsFalse(_switcher.OnWashboard,
                "the press went to the RAIL while she was lying against a wharf — step-ashore outranks " +
                "the washboard, or a boat at a dock becomes a boat you fall out of");
            Assert.AreEqual(ControlMode.OnFoot, _switcher.Mode, "she stepped ashore");
            Assert.IsTrue(StPetersWharf.DeckFootprint().Contains((Vector2)_playerGo.transform.position),
                $"…onto the planks, not into the water (she is at {_playerGo.transform.position})");
        }

        /// <summary>Whichever bearing points off her port side — the side the pier is on at this berth,
        /// and the one the derived band and the authored washboards both reach.</summary>
        private float OutboardBearing()
        {
            _walk.TryDeckBox(_boat.transform, out Vector2 c, out Vector2 h);
            Vector2 n = OverTheSideMath.OutwardNormalOnBox(c, h, _walk.DeckLocalPosition);
            if (n.sqrMagnitude < 1e-6f) n = new Vector2(-1f, 0f);
            return Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg;
        }
    }
}
