using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>SWIM UP TO A HULL AND CLIMB ABOARD</b> — the owner's 2026-09-02 sentence, driven end to end
    /// on the real St Peters seabed with the real components: <i>"for now a player should be able to
    /// swim up to a hull and climb aboard anywhere"</i>.
    ///
    /// <para><b>What only a PlayMode run can hold.</b> The pure rule is pinned in EditMode
    /// (<c>HullPresencesTests</c>, <c>SwimToHullTests</c>). What those cannot see is the WIRING: that a
    /// live <see cref="BoatController"/> actually puts her outline into <see cref="HullPresences"/>, that
    /// the walk controller's cached probe actually asks it, and that the boarding verb PR 2 shipped
    /// answers from the water. Three seams, none of which a pure test touches — and the boarding path was
    /// already swim-clean, so the ONLY thing that ever stopped her was the wall this closes.</para>
    /// </summary>
    public class SwimToHullPlayTests
    {
        private sealed class FixedTide : IEnvironmentService
        {
            public float Level = StPetersBuilder.TideMean;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private readonly List<Object> _spawned = new List<Object>();
        private FixedTide _tide;
        private PlayerWalkController _walk;
        private Rigidbody2D _body;
        private HeldWalkIntents _held;
        private ControlSwitcher _switcher;
        private BoatController _boat;

        // 40 cm in from the pier's north lip, so the walk model's own look-ahead probe lands in the
        // berth rather than on the boundary (the EditMode fixture stands her in the same place).
        private static float OnThePlanksNorth => StPetersWharf.NorthFaceY - 0.4f;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            StandableSurfaces.Clear();
            HullPresences.Clear();
            InteractionGate.Reset();
            MoveActionClaim.Reset();
            ShellPause.Reset();

            _tide = new FixedTide();
            GameServices.Environment = _tide;

            var terrainGo = Spawn("StPetersTerrain");
            var terrain = terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);
            GameServices.TidalTerrain = terrain;

            // The pier, registered the way StPetersWharf.Place() registers it — so the planks are floor.
            var pierGo = Spawn("StPetersWharf");
            var pier = pierGo.AddComponent<StandablePlatform>();
            pier.Configure(StPetersWharf.SurfaceId, StPetersWharf.DeckFootprint(),
                           StPetersWharf.DeckElevationFrom(terrain));

            // The player, on foot, with the region's tide gate on.
            var playerGo = Spawn("Player");
            playerGo.AddComponent<SpriteRenderer>();
            _walk = playerGo.AddComponent<PlayerWalkController>();
            _body = playerGo.GetComponent<Rigidbody2D>();
            SetTideGatedWalk(_walk, true);
            _held = new HeldWalkIntents();
            _walk.ConfigureWalkInput(_held);
            GameServices.PlayerTransform = playerGo.transform;

            // The starter dory at her berth, lying on the pier's axis — a REAL BoatController, which is
            // the thing under test: nothing here installs HullPresence by hand.
            var boatGo = Spawn("Dory");
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
            dockZone.transform.position = StPetersBuilder.DockZonePos;   // the ARRIVAL's berth, not hers
            var disembark = Spawn("Disembark");
            disembark.transform.position = StPetersBuilder.DisembarkPos;
            _switcher = Spawn("Switcher").AddComponent<ControlSwitcher>();
            _switcher.Configure(_walk, _boat, input, dockZone.transform,
                                StPetersBuilder.DockZoneRadius, disembark.transform);
        }

        [TearDown]
        public void TearDown()
        {
            StandableSurfaces.Clear();
            HullPresences.Clear();
            InteractionGate.Reset();
            MoveActionClaim.Reset();
            ShellPause.Reset();
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

        private static void SetTideGatedWalk(PlayerWalkController walk, bool on)
        {
#if UNITY_EDITOR
            var so = new SerializedObject(walk);
            var prop = so.FindProperty("_tideGatedWalk");
            Assert.IsNotNull(prop, "PlayerWalkController._tideGatedWalk was renamed — re-point this write");
            prop.boolValue = on;
            so.ApplyModifiedPropertiesWithoutUndo();
#else
            Assert.Ignore("needs the editor to set the serialized tide-gate tunable");
#endif
        }

        /// <summary>
        /// Put her at <paramref name="from"/> and push in <paramref name="push"/> for a stretch of real
        /// time, then hand back how far she actually travelled along it. Frames are not time and a
        /// keypress is undeliverable — so the intent is HELD on the controller's own source and the
        /// distance is measured off the rigidbody.
        /// </summary>
        private IEnumerator Push(Vector2 from, Vector2 push, System.Action<float> metresAlong)
        {
            _body.position = from;
            _body.linearVelocity = Vector2.zero;
            _held.Walk(push.normalized, sprint: false);
            yield return null;                    // the read is in Update — land the intent first
            yield return new WaitForFixedUpdate(); // …and let one physics tick apply it

            Vector2 start = _body.position;
            float deadline = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < deadline) yield return null;

            metresAlong(Vector2.Dot(_body.position - start, push.normalized));
            _held.Walk(Vector2.zero, sprint: false);
            yield return null;
        }

        // =============================================================================================
        //  1. the wiring: a live hull is a registered hull
        // =============================================================================================

        [UnityTest]
        public IEnumerator ALiveBoatControllerPutsHerOutlineInTheRegistry()
        {
            yield return null;

            Assert.AreEqual(1, HullPresences.Count,
                "the dory must register herself — nothing in this fixture installs HullPresence by hand, " +
                "which is the point: a hull the game spawns is a hull you can swim up to");

            HullFootprint f = HullPresences.Active[0].Footprint;
            Assert.AreEqual(StPetersBuilder.DoryMooredPos.x, f.Center.x, 1e-3f, "her centre, x");
            Assert.AreEqual(StPetersBuilder.DoryMooredPos.y, f.Center.y, 1e-3f, "her centre, y");
            Assert.AreEqual(StPetersBuilder.DoryLengthMetres * 0.5f, f.HalfLength, 1e-3f,
                "her half-length off the def — a hull registered as a POINT would open a hole in the sea " +
                "around nothing");
            Assert.Greater(f.HalfBeam, 0f, "and a beam, so her outline is a boat rather than a line");

            // She lies ALONGSIDE: her keel runs east–west, so her ends reach along x, not across y.
            Assert.Less(Mathf.Abs(f.BowDirection.y), 0.2f,
                $"her bow points {f.BowDirection} — she is moored on the pier's axis, and a hull registered " +
                "athwart would put her outline across the fairway (the #707 defect, in the registry this time)");
        }

        [UnityTest]
        public IEnumerator AHullThatIsDestroyedTakesHerHoleInTheWallWithHer()
        {
            yield return null;
            Assert.AreEqual(1, HullPresences.Count, "premise");

            _boat.gameObject.SetActive(false);
            yield return null;

            Assert.AreEqual(0, HullPresences.Count,
                "a boat that is no longer in the scene must not leave swimmable water behind her");
        }

        // =============================================================================================
        //  2. ⭐ the journey: off the planks, into the water beside her own boat, and aboard
        // =============================================================================================

        [UnityTest]
        public IEnumerator OffThePlanksBesideHerOwnDory_SheGoesIn_AndClimbsAboard()
        {
            yield return null;

            var onThePlanks = new Vector2(StPetersBuilder.DoryMooredX, OnThePlanksNorth);
            float travelled = 0f;
            yield return Push(onThePlanks, Vector2.up, m => travelled = m);

            Assert.Greater(travelled, 0.2f,
                $"she covered {travelled:0.00} m north off the planks — with the dory alongside, the " +
                "boat-only wall steps aside and she goes into the water");
            Assert.AreEqual(OnFootWaterState.Swim, _walk.WaterState,
                "…and she is swimming, not standing on something");

            // ⭐ …and the verb PR 2 shipped answers from the water. Nothing here re-implements boarding:
            // the whole of PR 4 is that she can now BE here.
            Assert.IsTrue(_switcher.WithinBoardReach(),
                "in the water alongside her, the boarding gate must see her");
            Assert.IsTrue(_switcher.BeginInteract(), "…and E must put her aboard");

            float deadline = Time.realtimeSinceStartup + 10f;
            while (_switcher.IsBoardingMove && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode,
                "she swam up to a hull and climbed aboard — the owner's sentence, end to end");
        }

        /// <summary>
        /// ⭐⭐ <b>THE CONTROL, and the half of the ruling a relaxation loses first.</b> The same push, the
        /// same tide, the same pier — out of reach of every hull. Water travel is boats only.
        ///
        /// <para><b>⚠ The spot is SWEPT, not chosen.</b> Standing her a flat 10 m west along the north lip
        /// measures <b>−0.66 m</b> — that far inshore the pier runs over ground the tide bares, so there is
        /// no wall there to test and the control would pass for the wrong reason. This finds a plank point
        /// that actually poses the question: dry underfoot, boat-only water off the lip, no hull in reach.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator OutOfReachOfHer_TheWallStillRefusesHer()
        {
            yield return null;

            float reach = _walk.SwimBoardReach;
            float swimLimit = _walk.SwimLimit;

            Vector2 onThePlanks = default;
            float outward = 0f, toHull = 0f;
            bool found = false;
            foreach (float lip in new[] { StPetersWharf.NorthFaceY - 0.4f, StPetersWharf.MooringFaceY + 0.4f })
            {
                float o = lip > 0f ? 1f : -1f;
                for (float x = StPetersWharf.RootCellX; x <= StPetersWharf.HeadCellX + 1f && !found; x += 0.5f)
                {
                    var p = new Vector2(x, lip);
                    if (TidalWalkability.DepthNow(p) > 0f) continue;                    // she must stand dry
                    if (TidalWalkability.DepthNow(p + new Vector2(0f, o * 0.5f)) <= swimLimit) continue;
                    float d = HullPresences.DistanceToNearestOutlineNow(p);
                    if (d <= reach) continue;                                           // alongside IS the feature
                    onThePlanks = p; outward = o; toHull = d; found = true;
                }
                if (found) break;
            }
            Assert.IsTrue(found,
                "this pier must have SOMEWHERE that poses the question — dry underfoot, boat-only water off " +
                "the lip, no hull within reach");

            float travelled = 0f;
            yield return Push(onThePlanks, new Vector2(0f, outward), m => travelled = m);

            Assert.Less(travelled, 0.05f,
                $"she covered {travelled:0.00} m at {toHull:0.0} m from the nearest hull — out of reach of a " +
                "boat the sea is still boat-only water. If this ever goes green the relaxation has stopped " +
                "being narrow and the owner's boats-only rule is gone");
            Assert.AreEqual(OnFootWaterState.Dry, _walk.WaterState, "she is still on the planks");
        }
    }
}
