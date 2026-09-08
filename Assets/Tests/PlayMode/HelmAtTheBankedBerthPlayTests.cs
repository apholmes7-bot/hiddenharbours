using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>The owner's press, on the boat he was pressing it on.</b> (Playtest, St Peters,
    /// 2026-09-07: <i>"still when i mount it i cant operate them — i immediately jump ashore with a
    /// rope … its when i mount the dory and try to push e at the helm, im also close to the dock so i
    /// understand why it happens but this will be an everyday occurance when docking so we need a smooth
    /// solution"</i>.)
    ///
    /// <para><b>The berth is the point.</b> The starter dory lies alongside the pier's NORTH face on the
    /// pier's own axis — <see cref="StPetersBuilder.DoryMooredPos"/> at
    /// <see cref="StPetersBuilder.DoryMooredHeadingDegrees"/>, which is bow-WEST. Every defect here needed
    /// that heading: a helm spot measured in world axes lands abeam of a boat lying athwart the screen,
    /// and the press then falls through the deck ladder to a step-ashore that is perfectly available
    /// because she really is tied up to a wharf.</para>
    ///
    /// <para><b>Three presses, one boat</b>: at the tiller E takes the helm; on the deck facing the pier
    /// E steps her ashore with the line; on the deck facing away E does nothing and the line stays
    /// stowed. ⚠ The facing is injected, never acted out — a virtual keypress is undeliverable headless,
    /// and it is the FACING that is injectable, never the decision.</para>
    ///
    /// <para>⚠ This fixture builds its own world and loads no scene, so it never touches the player's
    /// savegame.</para>
    /// </summary>
    public class HelmAtTheBankedBerthPlayTests
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
        private BoatMooring _mooring;
        private GameObject _playerGo;
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            StandableSurfaces.Clear();
            InteractionGate.Reset();

            GameServices.Environment = new FixedTide { Level = StPetersBuilder.TideMean };

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
            _playerGo.AddComponent<DeckRiderVisual>();      // the shipping facing source, overridden below
            GameServices.PlayerTransform = _playerGo.transform;

            var boatGo = Spawn("Boat");
            boatGo.transform.position = StPetersBuilder.DoryMooredPos;
            boatGo.transform.rotation =
                Quaternion.Euler(0f, 0f, -StPetersBuilder.DoryMooredHeadingDegrees);
            _boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            _mooring = boatGo.AddComponent<BoatMooring>();

            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory";
            hull.LengthMeters = StPetersBuilder.DoryLengthMetres;
            hull.DraughtMeters = 0.3f;
            hull.CameraWorldHeightMeters = 14f;
            hull.Propulsion = PropulsionType.Oars;
            _spawned.Add(hull);
            _boat.SetHull(hull);
            _boat.enabled = false; input.enabled = false;

            // Her authored deck — the floor the fisher walks and the box the tiller sits abaft of.
            string path = "Assets/_Project/Data/Boats/Decks/DoryIso.asset";
            var deck = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(path);
            Assert.IsNotNull(deck, $"the authored deck {path} must exist");
            boatGo.AddComponent<BoatDeckAreas>().Configure(deck);

            // The pier she is tied to, as a standable surface — this is what makes stepping ashore
            // available from her deck at all, and it is the whole reason the old press ended on the planks.
            var pier = Spawn("Wharf").AddComponent<StandablePlatform>();
            pier.Configure("wharf.st_peters", StPetersWharf.DeckFootprint(),
                           StPetersWharf.DeckElevationFrom(GameServices.TidalTerrain));

            // ⚠ The dock zone is the ARRIVAL's berth on the SOUTH face, far off — exactly as the region
            // authors it. Her step ashore is therefore the PLANKS probe, not the authored landing.
            var dockZone = Spawn("DockZone");
            dockZone.transform.position = StPetersBuilder.DockZonePos;
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

        // ---- the cases ---------------------------------------------------------------------------

        /// <summary>
        /// ⭐ <b>THE DEFECT, as he met it.</b> Aboard the banked dory, walk aft to the tiller, press E
        /// once: she takes the HELM. Before the fix the helm spot lay 2.4 m away abeam of her, so this
        /// same press stepped the fisher onto the pier with the painter in his hand.
        /// </summary>
        [UnityTest]
        public IEnumerator AtHerTiller_OnTheBankedBerth_EPressTakesTheHelm()
        {
            yield return BoardHer();
            Assert.IsTrue(_switcher.CanStepAshore(),
                "premise: she IS alongside a wharf — that is what made the old press so easy to lose");

            yield return WalkAft();

            Assert.IsTrue(_switcher.WithinHelmReach(),
                "standing at her stern, the fisher is at the tiller — the whole of defect A is that this " +
                "was false on every heading but north");

            Assert.IsTrue(_switcher.BeginInteract(), "E must do something");
            yield return SettleAnyMove();

            Assert.AreEqual(ControlMode.Aboard, _switcher.Mode, "he took the helm");
            Assert.AreEqual(MooringState.Stowed, _mooring.State,
                "…and no line went into his hand: the rope is stowed while anyone is aboard");
            Assert.IsTrue(_walk.transform == _playerGo.transform, "harness: it is the same fisher");
        }

        /// <summary>Facing the pier, the everyday docking press still puts him on the planks — with the
        /// line, which is correct and stays (<i>"tie up your boat so the sea doesn't take it"</i>).</summary>
        [UnityTest]
        public IEnumerator OnHerDeckFacingThePier_EPressStepsAshoreWithTheLine()
        {
            yield return BoardHer();
            FaceTheWharf();
            yield return null;

            Assert.IsTrue(_switcher.BeginInteract(), "E must step him off");
            yield return SettleAnyMove();

            Assert.AreEqual(ControlMode.OnFoot, _switcher.Mode, "he is ashore");
            Assert.IsTrue(StPetersWharf.DeckFootprint().Contains((Vector2)_playerGo.transform.position),
                $"…on the planks (he is at {_playerGo.transform.position})");
            Assert.IsTrue(_mooring.IsHeld, "…holding her painter, which is the ruled behaviour");
        }

        /// <summary>
        /// ⭐ <b>Defect B.</b> The same fisher, on the same deck, turned AWAY from the planks: the press
        /// does not put him ashore and hands him no line. That is the whole of the owner's complaint —
        /// <i>"i immediately jump ashore with a rope"</i> — and it is what must stop happening.
        ///
        /// <para><b>What the press does instead is the RAIL</b>, and that is the ladder working rather
        /// than a gap in it (corrected after CI, 2026-09-07). A rung that stands down passes the press
        /// down the ladder; turned away from the planks he has declined them, and E then means what it
        /// has meant since the owner's 2026-09-02 ruling — one press onto the washboard, and the next
        /// one over the side if he is still looking at the sea. He can step straight back inboard. The
        /// one thing that cannot happen is the thing he complained about.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator OnHerDeckFacingAwayFromThePlanks_EPressDoesNotPutHimAshore()
        {
            yield return BoardHer();
            FaceAwayFromTheWharf();
            yield return null;

            Assert.IsTrue(_switcher.CanStepAshore(), "the planks are still there");
            Assert.IsFalse(_switcher.FacesTheStepAshore(), "…but he is not looking at them");

            _switcher.BeginInteract();
            yield return SettleAnyMove();

            Assert.AreNotEqual(ControlMode.OnFoot, _switcher.Mode,
                "he was put ashore by a press he made looking away from the wharf — the defect");
            Assert.IsFalse(StPetersWharf.DeckFootprint().Contains((Vector2)_playerGo.transform.position),
                $"…he is standing on the planks at {_playerGo.transform.position}");
            Assert.AreEqual(MooringState.Stowed, _mooring.State,
                "and no line was handed over: the painter goes into his hand on a step ASHORE, and he " +
                "did not make one");
        }

        // ---- the rig -----------------------------------------------------------------------------

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        /// <summary>Put him aboard the way the shipping verb does, from alongside her.</summary>
        private IEnumerator BoardHer()
        {
            _playerGo.transform.position = _boat.transform.position;
            yield return null;
            Assert.IsTrue(_switcher.BeginInteract(), "he must be able to board from alongside");
            yield return SettleAnyMove();
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode, "premise: he is on deck");
        }

        /// <summary>Walk him to the after end of her floor — the tiller end. Placed in the DECK frame,
        /// which is the frame a walk actually happens in; the projection to screen is the hull's.</summary>
        private IEnumerator WalkAft()
        {
            Assert.IsTrue(_walk.TryDeckBox(_boat.transform, out Vector2 centre, out Vector2 half),
                          "harness: her walkable box");
            _walk.SnapToDeckLocal(new Vector2(centre.x, centre.y - half.y));
            yield return null;
        }

        private IEnumerator SettleAnyMove()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_switcher.IsBoardingMove && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
        }

        /// <summary>Point his drawn figure at the pier. The bearing is read off the geometry rather
        /// than written down — a literal here would stop being true the day the berth moved.</summary>
        private void FaceTheWharf() => FaceCompass(CompassToTheWharf());

        private void FaceAwayFromTheWharf() => FaceCompass(CompassToTheWharf() + 180f);

        /// <summary>
        /// The bearing to the NEAREST PLANK — the point on the wharf's deck closest to where he stands.
        ///
        /// <para>⚠ This read <c>deck.center</c> until CI reddened it, and the failure is worth keeping
        /// written down: this pier is 31 m long and she lies at its HEAD, so its centre is 15 m west of
        /// her. Facing the middle of a wharf you are moored at the end of is a bearing <b>73° off</b> the
        /// planks beside you — outside a 120° cone, and the fixture then reported the production rule as
        /// broken when what was broken was where the fixture was looking. A pier is not a point.</para>
        /// </summary>
        private float CompassToTheWharf()
        {
            Rect deck = StPetersWharf.DeckFootprint();
            Vector2 here = _playerGo.transform.position;
            var nearest = new Vector2(Mathf.Clamp(here.x, deck.xMin, deck.xMax),
                                      Mathf.Clamp(here.y, deck.yMin, deck.yMax));
            Vector2 toPlanks = nearest - here;
            return Mathf.Atan2(toPlanks.x, toPlanks.y) * Mathf.Rad2Deg;
        }

        /// <summary>Inject the deck bearing that DRAWS along a compass heading on this hull — the
        /// composition <see cref="DeckRiderFacingMath"/> owns, inverted, so the fixture can speak in the
        /// bearings a player would recognise on screen.</summary>
        private void FaceCompass(float compassDegrees)
        {
            float hull = DeckWalkController.DrawnHeadingDegreesOf(_boat.transform);
            float deckBearing = DeckRiderFacingMath.DeckBearingFor(compassDegrees, hull);
            _switcher.ConfigureDeckFacing(() => deckBearing);
        }
    }
}
