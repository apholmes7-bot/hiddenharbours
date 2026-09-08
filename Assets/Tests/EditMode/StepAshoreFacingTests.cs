using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>Stepping ashore is a FACING, not a fallback.</b> (Owner playtest, St Peters, 2026-09-07:
    /// <i>"im also close to the dock so i understand why it happens but this will be an everyday
    /// occurance when docking so we need a smooth solution"</i>.)
    ///
    /// <para>Alongside a wharf, E anywhere on a small boat's deck used to end on the planks with the
    /// painter in the player's hand, whichever way she was looking. The rule that already governs the
    /// washboard — <i>"one press onto the washboard, the next press goes inboard or outboard by
    /// FACING"</i> (owner, 2026-08-23 / 09-02) — now governs the rung above it too: look at the wharf and
    /// E puts you on it; look inboard, or along the deck, and E leaves you aboard.</para>
    ///
    /// <para><b>What is asserted here, and what is not.</b> The arc itself is
    /// <see cref="InteractArc"/>'s, tested in its own fixture; these are the switcher's questions — which
    /// bearing is the wharf on, which frame the fisher's facing is read in, and the two cases where the
    /// rule stands aside rather than stranding somebody. The cone's edge is asserted from both sides at
    /// ±1°: an exact boundary passes in the frame that built it, so the honest bar is a degree either
    /// way.</para>
    ///
    /// <para>⚠ The facing is INJECTED (<see cref="ControlSwitcher.ConfigureDeckFacing"/>), which is the
    /// seam's whole purpose: a virtual keypress is undeliverable headless, so the only way to ask "and
    /// what if she were looking at the wharf?" is to be able to say so. It is the facing that is
    /// injectable, never the decision.</para>
    /// </summary>
    public class StepAshoreFacingTests
    {
        /// <summary>The shipped arc: 120° full width, so ±60° either side of dead ahead.</summary>
        private const float ConeDegrees = 120f;
        private const float HalfCone = ConeDegrees * 0.5f;

        private static readonly Vector3 BoatPos = new Vector3(0f, -12f, 0f);      // in the dock zone
        private static readonly Vector3 DockZonePos = new Vector3(0f, -12f, 0f);
        private static readonly Vector3 DisembarkPos = new Vector3(0f, -10.5f, 0f);  // the planks, due NORTH

        private readonly List<Object> _spawned = new List<Object>();
        private GameConfig _previousConfig;

        /// <summary>The hull the current rig was built around — the switcher keeps hers private, and a
        /// test that re-derived it would be guessing at the very transform the rule reads.</summary>
        private Transform _boat;

        [SetUp]
        public void SetUp()
        {
            _previousConfig = GameServices.Config;
            GameServices.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();
            GameServices.Reset();
            GameServices.Config = _previousConfig;
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- the rule ---------------------------------------------------------------------------------

        /// <summary>Looking at the planks, the everyday docking press does exactly what it always did.</summary>
        [Test]
        public void FacingTheWharf_ThePressStepsHerAshore()
        {
            var sw = BoardedInTheDockZone();
            FaceCompass(sw, 0f);                       // the planks are due north of her deck

            Assert.IsTrue(sw.CanStepAshore(), "harness: there are planks to step onto");
            Assert.IsTrue(sw.FacesTheStepAshore(), "…and she is looking at them");
            Assert.IsTrue(sw.CanInteract(), "so the prompt offers the step off");
            Assert.IsTrue(sw.TryInteract(), "and the press takes it");
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode, "she is ashore");
        }

        /// <summary>The defect: the same press, looking INBOARD, must leave her aboard.</summary>
        [Test]
        public void FacingInboard_ThePressLeavesHerAboard()
        {
            var sw = BoardedInTheDockZone();
            FaceCompass(sw, 180f);                     // her back to the wharf

            Assert.IsTrue(sw.CanStepAshore(),
                "the GEOMETRY is unchanged — there are still planks there, which is what the boarding " +
                "move re-reads at the far end of its arc");
            Assert.IsFalse(sw.FacesTheStepAshore(), "…but she is not looking at them");
            Assert.IsFalse(sw.CanInteract(), "so nothing is offered");
            Assert.IsFalse(sw.TryInteract(), "and the press does nothing at all");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "she is still on her own deck");
        }

        /// <summary>Along the deck is not facing the wharf — the case the owner will hit every time he
        /// walks forward to the bow while lying alongside. It is also why the cone must stay under 180°.</summary>
        [Test]
        public void FacingAlongTheDeck_ThePressLeavesHerAboard()
        {
            var sw = BoardedInTheDockZone();
            FaceCompass(sw, 90f);
            Assert.IsFalse(sw.FacesTheStepAshore(), "90° off the wharf is walking the deck, not leaving her");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode);
            Assert.IsFalse(sw.TryInteract());
        }

        /// <summary>The cone's edge, from both sides. ±1° of the half-arc, because an exact boundary
        /// passes in the frame that built it and proves nothing about either neighbour.</summary>
        [Test]
        public void TheConeEdge_HoldsFromBothSides()
        {
            foreach (float sign in new[] { 1f, -1f })
            {
                var sw = BoardedInTheDockZone();

                FaceCompass(sw, sign * (HalfCone - 1f));
                Assert.IsTrue(sw.FacesTheStepAshore(),
                    $"{sign * (HalfCone - 1f):0.#}° off the wharf is inside a {ConeDegrees}° cone");

                FaceCompass(sw, sign * (HalfCone + 1f));
                Assert.IsFalse(sw.FacesTheStepAshore(),
                    $"{sign * (HalfCone + 1f):0.#}° off the wharf is outside it");

                TearDownRig();
            }
        }

        /// <summary>
        /// The facing is COMPOSED, not measured: the fisher's bearing is relative to the DECK, and the
        /// deck turns. The same deck bearing that faced the wharf on a boat lying north must face away
        /// from it when the same boat lies east — otherwise the rule would break the moment a hull swung
        /// on her lines, which is the exact failure the rider's facing seam exists to prevent.
        /// </summary>
        [Test]
        public void TheFacingIsReadInTheDecksFrame_SoTurningTheHullTurnsTheRule()
        {
            var sw = BoardedInTheDockZone();
            TurnTheHullTo(sw, 90f);                    // her bow now points east; the wharf is still north

            SetDeckBearing(sw, 0f);                    // "looking at the bow" — which is now east
            Assert.IsFalse(sw.FacesTheStepAshore(),
                "a deck bearing of 0 looks along her keel, and her keel no longer points at the wharf");

            SetDeckBearing(sw, 270f);                  // over her port bow — which is now north
            Assert.IsTrue(sw.FacesTheStepAshore(),
                "the same fisher, turned on the deck to look north, is looking at the planks again");
        }

        // ---- where the rule stands aside --------------------------------------------------------------

        /// <summary>
        /// ⚠ The compatibility guarantee. A rig that draws no figure publishes no bearing, and an unknown
        /// facing must never STRAND a player on a boat — <see cref="InteractArc"/> passes it. The opposite
        /// default to the washboard's, and deliberately so: each verb's default is the safe answer for
        /// its own consequence.
        /// </summary>
        [Test]
        public void WithNoDrawnFacingToRead_TheRuleDoesNotApply()
        {
            var sw = BoardedInTheDockZone();           // no rider, no injected facing

            Assert.IsTrue(sw.FacesTheStepAshore(), "an unknown facing faces everything");
            Assert.IsTrue(sw.TryInteract(), "so every fixture that predates the rule still steps ashore");
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
        }

        /// <summary>
        /// ⚠ The stand-aside asserted from BOTH sides, in one case, because "an unknown facing allows"
        /// is only safe if "a KNOWN facing that is 61° off refuses" — a gate that allowed everything
        /// would pass the first arm and mean nothing. The unreadable bearing is injected rather than
        /// arranged by deleting the rider, so the two arms differ in exactly one thing.
        /// </summary>
        [Test]
        public void AnUnreadableFacingAllows_AndAReadableOneOffTheConeStillRefuses()
        {
            var sw = BoardedInTheDockZone();

            SetDeckBearing(sw, float.NaN);
            Assert.IsTrue(sw.FacesTheStepAshore(),
                "a bearing that is not a number is not a facing — an unknown facing faces everything, " +
                "and must never STRAND a player aboard");
            Assert.IsTrue(sw.CanInteract(), "…so the step off is offered");

            FaceCompass(sw, HalfCone + 1f);
            Assert.IsFalse(sw.FacesTheStepAshore(),
                "…while a bearing that IS readable and is a degree outside the cone still refuses — " +
                "without this arm the case above would pass on a gate that allowed everything");
        }

        /// <summary>
        /// Aground on a bared flat there is no wharf to look at: the whole hull is over land and she may
        /// step off any side of it. ⚠ The landing in that case is the BOAT'S OWN ORIGIN, so a rule applied
        /// blindly would have demanded she face INBOARD to step out onto the beach.
        /// </summary>
        [Test]
        public void OverBaredLand_ThereIsNoWharfToFace()
        {
            var sw = BoardedOverBaredLand();
            FaceCompass(sw, 180f);                     // the worst facing there is

            Assert.IsTrue(sw.CanStepAshore(), "the ground under her is bared");
            Assert.IsTrue(sw.FacesTheStepAshore(), "and there is no bearing to be wrong about");
            Assert.IsTrue(sw.TryInteract());
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
        }

        /// <summary>The A/B and the escape hatch: a full-circle arc cannot exclude anything, so the press
        /// behaves exactly as it did before the rule existed.</summary>
        [Test]
        public void AFullCircleArc_RestoresTheOldBehaviourExactly()
        {
            WireConfig(360f);
            var sw = BoardedInTheDockZone();
            FaceCompass(sw, 180f);

            Assert.IsTrue(sw.FacesTheStepAshore(), "360° is 'no arc', not 'a very wide one'");
            Assert.IsTrue(sw.TryInteract());
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
        }

        /// <summary>And the tunable is really read: narrowing the arc narrows what passes. Pinned because
        /// a number nobody reads is a number that silently stops mattering (rule 6).</summary>
        [Test]
        public void TheArcComesFromGameConfig()
        {
            WireConfig(40f);                           // ±20°
            var sw = BoardedInTheDockZone();

            Assert.AreEqual(40f, sw.StepAshoreFacingArcDegrees, 1e-4f, "the switcher reads the config");
            FaceCompass(sw, 19f);
            Assert.IsTrue(sw.FacesTheStepAshore(), "19° is inside ±20°");
            FaceCompass(sw, 21f);
            Assert.IsFalse(sw.FacesTheStepAshore(), "21° is not — and it WOULD be inside the shipped 120°");
        }

        // ---- the rig ----------------------------------------------------------------------------------

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

        private void WireConfig(float arcDegrees)
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(cfg);
            cfg.StepAshoreFacingArcDegrees = arcDegrees;
            GameServices.Config = cfg;
        }

        /// <summary>Point her drawn figure along a COMPASS bearing (0 = north), whatever the hull is
        /// doing — the switcher composes the deck bearing back out of the hull's heading, so this is the
        /// bearing a player would recognise on screen.</summary>
        private void FaceCompass(ControlSwitcher sw, float compassDegrees)
        {
            float hull = DeckWalkController.DrawnHeadingDegreesOf(_boat);
            SetDeckBearing(sw, DeckRiderFacingMath.DeckBearingFor(compassDegrees, hull));
        }

        private static void SetDeckBearing(ControlSwitcher sw, float deckBearingDegrees)
            => sw.ConfigureDeckFacing(() => deckBearingDegrees);

        private void TurnTheHullTo(ControlSwitcher sw, float compassHeadingDegrees)
            => _boat.rotation = Quaternion.Euler(0f, 0f, -compassHeadingDegrees);

        /// <summary>A fisher standing on the deck of a boat lying in the dock zone, with the planks due
        /// north of her.</summary>
        private ControlSwitcher BoardedInTheDockZone()
        {
            var sw = BuildAndBoard(BoatPos, DockZonePos, DisembarkPos);
            Assert.IsTrue(sw.InDockZone(), "harness: she is at the berth");
            return sw;
        }

        /// <summary>The same fisher on a boat sitting over bared ground, with no dock zone anywhere near
        /// her — the OnLand arm of the step-off.</summary>
        private ControlSwitcher BoardedOverBaredLand()
        {
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };
            GameServices.Environment = new FlatEnv { Level = 0f };      // depth −0.2 m ⇒ land
            var sw = BuildAndBoard(BoatPos, new Vector3(0f, 200f, 0f), new Vector3(0f, 201f, 0f));
            Assert.IsFalse(sw.InDockZone(), "harness: nowhere near the authored berth");
            Assert.IsTrue(sw.OnLand(), "harness: the ground under her is bared");
            return sw;
        }

        private ControlSwitcher BuildAndBoard(Vector3 boatPos, Vector3 dockPos, Vector3 disembarkPos)
        {
            var playerGo = NewGo("Player", boatPos + new Vector3(0f, 1f, 0f));
            var walk = playerGo.AddComponent<PlayerWalkController>();     // auto-adds Rigidbody2D + renderer
            playerGo.AddComponent<DeckWalkController>().enabled = false;

            var boatGo = NewGo("Boat", boatPos);
            var boat = boatGo.AddComponent<BoatController>();             // auto-adds Rigidbody2D
            _boat = boatGo.transform;
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(hull);
            hull.Id = "boat.dory"; hull.LengthMeters = 4.5f; hull.CameraWorldHeightMeters = 14f;
            boat.SetHull(hull);
            boat.enabled = false;

            var dock = NewGo("DockZone", dockPos);
            var disembark = NewGo("Disembark", disembarkPos);
            var swGo = NewGo("Switcher", Vector3.zero);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, null, dock.transform, 3f, disembark.transform);

            Assert.IsTrue(sw.TryInteract(), "harness: she boards");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "harness: boarding lands on the DECK");
            Assert.IsFalse(sw.WithinHelmReach(),
                "harness: the board spot is away from the tiller, so E here is the step-ashore rung " +
                "rather than the helm");
            return sw;
        }

        private GameObject NewGo(string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            _spawned.Add(g);
            return g;
        }

        /// <summary>⚠ Between the two halves of the edge case: two live switchers both publish who holds
        /// the helm, and a fixture that builds two stages poisons whatever runs after it.</summary>
        private void TearDownRig()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            _boat = null;
        }
    }
}
