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
    /// 🔴 <b>A BOAT YOU CAN BOARD AND NEVER GET OFF.</b> #707 moored the starter dory alongside the
    /// pier's north face — 0.40 m of water between her rail and the planks. And she became unleaveable,
    /// because step-ashore knew exactly two things: the region's ONE authored dock-zone transform (which
    /// at St Peters is the ARRIVAL's berth, ten metres away on the other face) and bared ground under the
    /// hull (this is a dredged −4 m pocket). Neither is true of a boat lying against a wharf.
    ///
    /// <para>So this is the charter's named acceptance test, over the region's REAL geometry: the
    /// wharf's own footprint and deck elevation, the dory at her own derived berth. She must be
    /// boardable from the planks and she must be leaveable onto them.</para>
    ///
    /// <para><b>The other half is REACH.</b> Boarding measured the player to the boat's ROOT, which is
    /// wrong in both directions at once: a 12.9 m hull is unboardable from her own stern while you are
    /// touching her, and a 4.5 m dory is boardable from open water off her bow. It now measures to the
    /// hull's OUTLINE — the same rail the boarding move already arcs to.</para>
    /// </summary>
    public class StepOffAnywherePlayTests
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
        private BoatController _dory;
        private GameObject _playerGo;
        private StandablePlatform _pier;
        private FixedTide _tide;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            StandableSurfaces.Clear();
            InteractionGate.Reset();

            _tide = new FixedTide { Level = StPetersBuilder.TideMean };
            GameServices.Environment = _tide;

            // ⚠ THE REGION'S REAL SEABED, not a flat stand-in. The first cut of this fixture used a flat
            // −4 m bed and every plank probe came back false — because StPetersWharf.DeckElevationFrom
            // MEASURES the deck off the terrain at the pier root, so on a flat −4 m world the pier's own
            // planks read as 4 m UNDER water and the dry-deck check correctly refused them. The pier root
            // stands on +5.35 m of real ground; nothing about this test works without it.
            var terrainGo = Spawn("TidalTerrain");
            var terrain = terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);
            GameServices.TidalTerrain = terrain;

            // THE PIER, exactly as the region registers it: its own footprint, its own measured deck.
            var pierGo = Spawn("StPetersWharf");
            _pier = pierGo.AddComponent<StandablePlatform>();
            _pier.Configure("wharf.st_peters", StPetersWharf.DeckFootprint(),
                            StPetersWharf.DeckElevationFrom(GameServices.TidalTerrain));

            _playerGo = Spawn("Player");
            _playerGo.AddComponent<SpriteRenderer>();
            var walk = _playerGo.AddComponent<PlayerWalkController>();
            _playerGo.AddComponent<DeckWalkController>();
            GameServices.PlayerTransform = _playerGo.transform;

            // HER OWN BERTH, derived — alongside the pier's north face at the head pilehead.
            var doryGo = Spawn("Dory");
            doryGo.transform.position = StPetersBuilder.DoryMooredPos;
            doryGo.transform.rotation =
                Quaternion.Euler(0f, 0f, -StPetersBuilder.DoryMooredHeadingDegrees);
            _dory = doryGo.AddComponent<BoatController>();
            var input = doryGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory";
            hull.LengthMeters = StPetersBuilder.DoryLengthMetres;
            hull.DraughtMeters = 0.3f;
            hull.CameraWorldHeightMeters = 14f;
            hull.Propulsion = PropulsionType.Oars;
            _spawned.Add(hull);
            _dory.SetHull(hull);
            _dory.enabled = false; input.enabled = false;

            // ⭐ HER REAL AUTHORED DECK, not the walker's fallback rectangle. It matters: the dory's
            // walkable strip is 0.45 m wide (WalkHalfExtents.x 0.225) against a 0.85 m half-beam, so the
            // deck edge sits ~0.63 m INSIDE her rail and ~1.03 m off the planks she is fendered against.
            // A test on the generic 1.4 × 3.2 m fallback would be measuring a boat this game does not have.
            AttachDeck(doryGo, "DoryIso");

            // ⚠ The region's ONE dock zone is the ARRIVAL's berth on the far (south) face — which is the
            // whole point: it is nowhere near her, so InDockZone() is false and only the new route can
            // answer. Wired at the real coordinate rather than left null, so the test cannot pass by
            // accident of a missing reference.
            var dockZone = Spawn("DockZone");
            dockZone.transform.position = StPetersBuilder.DockZonePos;
            var disembark = Spawn("Disembark");
            disembark.transform.position = StPetersBuilder.DisembarkPos;

            _switcher = Spawn("Switcher").AddComponent<ControlSwitcher>();
            _switcher.Configure(walk, _dory, input, dockZone.transform,
                                StPetersBuilder.DockZoneRadius, disembark.transform);
        }

        [TearDown]
        public void TearDown()
        {
            StandableSurfaces.Clear();
            InteractionGate.Reset();
            GameServices.PlayerTransform = null;
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

        /// <summary>
        /// Let a boarding/step-off MOVE land. ⚠ Boarding is an ARC, not a teleport (the owner's own ask:
        /// "climb aboard, don't teleport"), so the mode does not change on the frame the key is pressed —
        /// a fixture that yields once and asserts the mode is testing the press, not the transition.
        /// Bounded by wall clock rather than a frame count, because frames are not time.
        /// </summary>
        private IEnumerator SettleTheMove()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_switcher.IsBoardingMove && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(_switcher.IsBoardingMove, "the boarding move never landed inside 10 s");
            yield return null;
        }

        /// <summary>Hang a hull's REAL authored deck areas on her, the way the region's own hull skinner
        /// does — so every outline question in this file is asked of shipped data.</summary>
        private void AttachDeck(GameObject boatRoot, string deckAssetName)
        {
            string path = $"Assets/_Project/Data/Boats/Decks/{deckAssetName}.asset";
            var deck = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(path);
            Assert.IsNotNull(deck, $"the authored deck {path} must exist — this test measures her real " +
                                   "outline, not a stand-in rectangle");
            var host = boatRoot.GetComponent<BoatDeckAreas>();
            if (host == null) host = boatRoot.AddComponent<BoatDeckAreas>();
            host.Configure(deck);
        }

        /// <summary>The plank cell nearest her berth — where a player standing on the pier would be.</summary>
        private static Vector3 PlanksBesideHer()
        {
            Rect deck = StPetersWharf.DeckFootprint();
            return new Vector3(StPetersBuilder.DoryMooredPos.x, deck.yMax - 0.5f, 0f);
        }

        /// <summary>📏 What the probe actually sees. Logged, not asserted — when a geometric predicate
        /// says no, the number it said no about is the finding.</summary>
        [UnityTest]
        public IEnumerator TheProbeGeometry_IsReported()
        {
            _playerGo.transform.position = PlanksBesideHer();
            yield return null;

            Rect deck = StPetersWharf.DeckFootprint();
            float deckY = StPetersWharf.DeckElevationFrom(GameServices.TidalTerrain);
            var said = new System.Text.StringBuilder(
                $"[step-off/probe] surfaces={StandableSurfaces.Count}; pier deck rect {deck} at " +
                $"{deckY:F2} m; water {_tide.Level:F2} m; dory at {_dory.transform.position} heading " +
                $"{StPetersBuilder.DoryMooredHeadingDegrees:F0}°; reach {_switcher.StepAshoreReachMetres:F2} m\n");

            var host = _dory.GetComponent<BoatDeckAreas>();
            said.Append($"  deck def = {(host != null && host.Deck != null ? host.Deck.Id : "NONE")}, " +
                        $"walkable={(host != null && host.HasWalkableDeck())}\n");

            var walk = _playerGo.GetComponent<DeckWalkController>();
            walk.TryDeckBox(_dory.transform, out Vector2 c, out Vector2 h);
            said.Append($"  deck box centre {c} half {h}\n");

            for (int side = 0; side < 4; side++)
            {
                bool alongKeel = side < 2;
                float sign = (side % 2 == 0) ? -1f : 1f;
                Vector2 outward = alongKeel ? new Vector2(sign, 0f) : new Vector2(0f, sign);
                float edge = alongKeel ? h.x : h.y;
                float span = alongKeel ? h.y : h.x;
                said.Append($"  side {(alongKeel ? (sign < 0 ? "port  " : "stbd  ") : (sign < 0 ? "stern " : "bow   "))}:");
                for (int i = 0; i <= 6; i++)
                {
                    float t = Mathf.Lerp(-span, span, i / 6f);
                    Vector2 station = alongKeel ? new Vector2(c.x + sign * edge, c.y + t)
                                                : new Vector2(c.x + t, c.y + sign * edge);
                    Vector2 dp = station + outward * _switcher.StepAshoreReachMetres;
                    walk.TryDeckFramePointWorld(_dory.transform, dp, out Vector3 at);
                    bool hit = StandableSurfaces.TryGetDeckElevationNow(at, out _);
                    said.Append($" {at.x:F1},{at.y:F1}{(hit ? "✓" : "·")}");
                }
                said.Append('\n');
            }
            said.Append($"  → PlanksWithinReach = {_switcher.PlanksWithinReach(out Vector3 land)} at {land}\n");
            Debug.Log(said.ToString());
            Assert.Pass("a survey, not a claim");
        }

        // =============================================================================================
        //  1. 🔴 the acceptance test the charter names
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>Board her from the planks, and get back off onto them.</b> Before this the second half
        /// was impossible: `CanStepAshore()` was `InDockZone() || OnLand()`, and at her berth both are
        /// false — the dock zone is the arrival's, ten metres away, and the ground under her is the
        /// dredged −4 m pocket.
        /// </summary>
        [UnityTest]
        public IEnumerator TheStarterDoryAtHerBerth_IsBoardableFromThePlanks_AndLeaveableOntoThem()
        {
            _playerGo.transform.position = PlanksBesideHer();
            yield return null;

            // Premise, stated so a green run cannot be hiding a berth that moved under the test.
            Assert.IsFalse(_switcher.InDockZone(),
                "premise: her berth is NOT the region's authored dock zone (that is the arrival's, on " +
                "the south face) — if it were, this test would be exercising the old route");
            Assert.IsFalse(_switcher.OnLand(),
                "premise: she floats over the dredged pocket, so there is no bared ground to step onto");

            Assert.IsTrue(_switcher.WithinBoardReach(),
                "she lies 0.40 m off the planks and the player is standing on them — that is within " +
                $"{_switcher.BoardReachMetres:F2} m of her outline by any honest measure");
            Assert.IsTrue(_switcher.BeginInteract(), "E on the planks beside her must board her");
            yield return SettleTheMove();

            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode, "…and boarding means being on her deck");

            // 🔴 THE HALF THAT WAS IMPOSSIBLE.
            Assert.IsTrue(_switcher.PlanksWithinReach(out Vector3 landing),
                "there are planks 0.40 m off her rail and the wharf has registered them as a standing " +
                "surface — this is the read that did not exist, and without it she is a boat you can " +
                "board and never get off");
            Assert.IsTrue(_switcher.CanStepAshore(),
                "she is lying against a wharf; that is what stepping ashore IS");

            Assert.IsTrue(StPetersWharf.DeckFootprint().Contains(landing),
                $"the landing {landing} is not on the pier's planks {StPetersWharf.DeckFootprint()} — " +
                "a step ashore that puts you in the water is not a step ashore");

            Debug.Log($"[step-off] she lies at {StPetersBuilder.DoryMooredPos} on " +
                      $"{StPetersBuilder.DoryMooredHeadingDegrees:F0}°; the planks answer at {landing}, " +
                      $"inside the deck {StPetersWharf.DeckFootprint()}.");
        }

        /// <summary>…and the whole round trip, through the shipping verb rather than the predicates.</summary>
        [UnityTest]
        public IEnumerator SheCanBeBoardedAndLeft_ThroughTheInteractVerb()
        {
            _playerGo.transform.position = PlanksBesideHer();
            yield return null;

            Assert.IsTrue(_switcher.BeginInteract(), "board");
            yield return SettleTheMove();
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode);

            // Away from the helm, E steps her ashore. (The helm sits at the switcher's helm offset; the
            // walker is put down at the board offset, which is not it.)
            Assert.IsFalse(_switcher.WithinHelmReach(),
                "premise: she is not standing at the tiller, or E would take the helm instead");

            Assert.IsTrue(_switcher.BeginInteract(), "E on her deck, away from the helm, must step ashore");
            yield return SettleTheMove();

            Assert.AreEqual(ControlMode.OnFoot, _switcher.Mode,
                "she never got off — the boarding move either refused or never landed");
            Assert.IsTrue(StPetersWharf.DeckFootprint().Contains((Vector2)_playerGo.transform.position),
                $"she stepped off to {_playerGo.transform.position}, which is not on the planks");
        }

        // =============================================================================================
        //  2. …and the refusal still refuses
        // =============================================================================================

        /// <summary>
        /// ⚠ The other side of a new permission. With NO built surface registered — she is moored out in
        /// open water — stepping ashore must still be refused. The owner's 2026 playtest ruling stands:
        /// you cannot step off onto water.
        /// </summary>
        [UnityTest]
        public IEnumerator WithNoPlanksAnywhere_SteppingAshoreIsStillRefused()
        {
            StandableSurfaces.Clear();                       // the pier is gone; she lies in the pocket
            _playerGo.transform.position = StPetersBuilder.DoryMooredPos;
            yield return null;

            Assert.IsFalse(_switcher.PlanksWithinReach(out _), "no surface is registered");
            Assert.IsFalse(_switcher.CanStepAshore(),
                "over a dredged −4 m pocket with no wharf, there is nowhere to step — the 'no stepping " +
                "off onto water' ruling must survive this change");
        }

        /// <summary>⚠ And a wharf the tide has covered is not somewhere to stand either.</summary>
        [UnityTest]
        public IEnumerator PlanksUnderWater_AreNotSomewhereToStepAshore()
        {
            _playerGo.transform.position = PlanksBesideHer();
            yield return null;
            Assert.IsTrue(_switcher.PlanksWithinReach(out _), "premise: dry at mean tide");

            _tide.Level = StPetersWharf.DeckElevationFrom(GameServices.TidalTerrain) + 1f;
            yield return null;

            Assert.IsFalse(_switcher.PlanksWithinReach(out _),
                "the deck is a metre under water and still being offered as a place to stand");
        }

        // =============================================================================================
        //  3. ⭐ reach is to the OUTLINE, not the root
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>A long hull is boardable from her own stern.</b> Measured to the root, a 12.9 m boat
        /// refuses a player standing against her quarter — 6 m from her origin, 0 m from her hull. This
        /// is the case the old gate got exactly backwards.
        /// </summary>
        [UnityTest]
        public IEnumerator ALongHull_IsBoardableFromHerQuarter_WhereTheRootTestRefused()
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.cape_islander";
            hull.LengthMeters = StPetersBuilder.ArrivalHullLengthMetres;   // 12.9 m
            hull.DraughtMeters = 1.4f;
            hull.CameraWorldHeightMeters = 24f;
            hull.Propulsion = PropulsionType.Engine;
            _spawned.Add(hull);
            _dory.SetHull(hull);
            AttachDeck(_dory.gameObject, "CapeIslanderIso");   // her REAL deck: 6.30 m of walkable half-length
            _dory.transform.rotation = Quaternion.identity;    // bow north
            yield return null;

            // Astern of her, just off her transom: far from her ROOT, touching her HULL.
            float halfLength = hull.LengthMeters * 0.5f;
            _playerGo.transform.position = _dory.transform.position + new Vector3(0f, -(halfLength - 0.5f), 0f);
            yield return null;

            float toRoot = Vector2.Distance(_playerGo.transform.position, _dory.transform.position);
            Assert.Greater(toRoot, _switcher.BoardReachMetres,
                $"premise: she stands {toRoot:F2} m from the hull's ROOT, outside the " +
                $"{_switcher.BoardReachMetres:F2} m reach — this is the position the old gate refused");

            Assert.IsTrue(_switcher.WithinBoardReach(),
                $"she is standing against a 12.9 m hull's quarter, {toRoot:F2} m from her origin, and " +
                "the gate still measures to the origin. Reach is to the RAIL — the same rail the " +
                "boarding move arcs to.");
        }

        /// <summary>⭐ …and the mirror: open water off a small boat's bow is still not boarding range,
        /// so the outline did not simply make everything reachable.</summary>
        [UnityTest]
        public IEnumerator OpenWaterWellOffASmallHull_IsStillOutOfReach()
        {
            _playerGo.transform.position =
                StPetersBuilder.DoryMooredPos + new Vector3(0f, 12f, 0f);
            yield return null;

            Assert.IsFalse(_switcher.WithinBoardReach(),
                "twelve metres off a 4.5 m dory is not somewhere you can climb aboard from");
        }

        // =============================================================================================
        //  4. the reach is the OWNER'S number
        // =============================================================================================

        /// <summary>
        /// ⚠ <c>_boardReach</c> was a serialized private field, so the owner could not tune it and no
        /// test could state it (rule 6). It is <c>GameConfig.BoardReachMetres</c> now — and this asserts
        /// the switcher actually READS the config rather than its own fallback, which is the failure the
        /// GameConfig-is-behind-the-code trap is made of.
        /// </summary>
        [UnityTest]
        public IEnumerator TheBoardReach_ComesFromTheOwnersConfig()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.BoardReachMetres = 0.25f;                  // absurd on purpose: nothing is boardable
            _spawned.Add(config);
            GameServices.Config = config;
            yield return null;

            Assert.AreEqual(0.25f, _switcher.BoardReachMetres, 1e-4f,
                "the switcher is still reading its serialized fallback — the owner's tuning of the " +
                "ASSET would never reach the game");

            _playerGo.transform.position = PlanksBesideHer();
            yield return null;
            Assert.IsFalse(_switcher.WithinBoardReach(),
                "…and the tuned number has to actually gate the verb, not merely be readable");
        }
    }
}
