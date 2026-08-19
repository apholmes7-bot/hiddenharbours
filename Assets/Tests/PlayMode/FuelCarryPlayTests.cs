using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Art;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>Carrying a fuel container, JOINED UP</b> — a real container registering itself, a real
    /// <see cref="ControlSwitcher"/> deciding what the press is for, and the object actually moving between
    /// the ground and the fisher's hands over real frames.
    ///
    /// <para><b>What only PlayMode can show.</b> The rules are EditMode's (<c>FuelCarryTests</c> owns the
    /// facing table, the two gates and the rung flip). What is left is the wiring, which is where a seam
    /// actually fails:
    /// <list type="bullet">
    ///   <item><b>The loop closes on the interact key</b> — the same press picks up and puts down, with no
    ///   new binding, which is the entire claim of M2-39's first consumer.</item>
    ///   <item><b>The facing is DERIVED per frame</b> from the body's drawn heading. Only a running frame
    ///   loop can show that it FOLLOWS rather than merely starting out right.</item>
    ///   <item><b>The carried sprite rides the body's sorting band</b>, re-stated every frame off whatever
    ///   the player's own Y-sort has just written.</item>
    ///   <item><b>The known cost of consulting the verb LAST</b> — a can inside board reach loses the press
    ///   to boarding — pinned rather than left as prose, so the PR that changes it must decide to.</item>
    /// </list></para>
    ///
    /// <para><b>Headless-safe by construction (⚠️ do not relax this).</b> Nothing here renders, reads
    /// pixels, or calls <c>Camera.Render</c>: CI runs with a null graphics device, where a ReadPixels
    /// PlayMode test does not fail — it KILLS the editor, exit 1, with no results XML at all. Every
    /// assertion below is on STATE (transform parentage, registry membership, a presenter's facing index, a
    /// renderer's <c>sortingOrder</c> field) and none is on a pixel.</para>
    /// </summary>
    public class FuelCarryPlayTests
    {
        private static readonly Vector3 BoatPos = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 AshorePos = new Vector3(20f, 0f, 0f);   // well outside board reach
        private static readonly Vector3 DockPos = new Vector3(500f, 0f, 0f);
        private const float DockZoneRadius = 3.5f;

        private readonly List<Object> _spawned = new();
        private readonly List<InteractPerformed> _performed = new();

        private PlayerWalkController _walk;
        private Rigidbody2D _playerBody;
        private CarryHands _hands;
        private IsoCharacterSprite _skin;
        private SpriteRenderer _bodyRenderer;
        private ControlSwitcher _switcher;
        private BoatController _boat;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();

            _performed.Clear();
            EventBus.Clear<InteractPerformed>();
            EventBus.Clear<InteractCandidateChanged>();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Subscribe<InteractPerformed>(_performed.Add);

            // The fisher, on foot and well clear of the boat — so the switcher's own branches have nothing
            // to say and the press reaches the registry.
            var playerGo = Spawn("Player");
            _bodyRenderer = playerGo.AddComponent<SpriteRenderer>();
            _walk = playerGo.AddComponent<PlayerWalkController>();
            _playerBody = playerGo.GetComponent<Rigidbody2D>();   // RequireComponent adds it
            // The 8-way skin is the DRAWN heading the carried facing is derived from. No visual def is
            // wired: it draws nothing and reports a heading, which is exactly the half under test.
            _skin = playerGo.AddComponent<IsoCharacterSprite>();
            _hands = playerGo.AddComponent<CarryHands>();
            StandAt(AshorePos);

            var boatGo = Spawn("Dory");
            boatGo.transform.position = BoatPos;
            _boat = boatGo.AddComponent<BoatController>();
            var boatInput = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory_carry_test";
            hull.DraughtMeters = 0.3f;
            hull.CameraWorldHeightMeters = 14f;
            hull.Propulsion = PropulsionType.Oars;
            _spawned.Add(hull);
            _boat.SetHull(hull);
            _boat.enabled = false;
            boatInput.enabled = false;

            var dockZone = Spawn("DockZone");
            dockZone.transform.position = DockPos;
            var disembark = Spawn("Disembark");
            disembark.transform.position = DockPos;
            _switcher = Spawn("Switcher").AddComponent<ControlSwitcher>();
            _switcher.Configure(_walk, _boat, boatInput, dockZone.transform, DockZoneRadius, disembark.transform);
            _switcher.ConfigureBoardingMove(false, 3f, 0.8f, 0.55f, 0.35f);
            _switcher.ConfigureInteractVerb(true, InteractResolver.DefaultFacingArcDegrees);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<InteractPerformed>();
            EventBus.Clear<InteractCandidateChanged>();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();
            GameServices.Reset();

            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures ---------------------------------------------------------------------------

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private void StandAt(Vector3 pos)
        {
            if (_playerBody != null) _playerBody.position = pos;
            _walk.transform.position = pos;
        }

        private FuelContainerDef Def(bool carriable = true, int facings = 8,
                                     string id = "fuelstore.gas_jerry_s20")
        {
            var def = ScriptableObject.CreateInstance<FuelContainerDef>();
            def.Id = id;
            def.DisplayName = "20 L Gas Can";
            def.Carriable = carriable;
            def.Facings = facings;
            def.FillFractions = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
            _spawned.Add(def);
            return def;
        }

        /// <summary>A real container in the scene, built the way the dev spawner builds one — its own
        /// <c>OnEnable</c> is what registers it, which is the half being proved.</summary>
        private CarriableFuelContainer RealContainer(Vector3 pos, FuelContainerDef def = null)
        {
            var go = Spawn("FuelContainer");
            go.transform.position = pos;
            var presenter = go.AddComponent<FuelLevelPresenter>();
            presenter.Container = def ?? Def();
            presenter.Fill = 0.5f;
            go.AddComponent<YSortSprite>();
            return go.AddComponent<CarriableFuelContainer>();
        }

        // ---- the loop ----------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator The_loop_closes_on_the_interact_key_alone()
        {
            CarriableFuelContainer can = RealContainer(AshorePos + new Vector3(0.6f, 0f, 0f));
            yield return null;

            Assert.AreEqual(1, Interactables.Count, "the can registered itself");

            // PRESS ONE — pick it up.
            Assert.IsTrue(_switcher.BeginInteract(), "the press was spent on the can");
            Assert.IsTrue(can.IsCarried);
            Assert.AreSame(_hands.transform, can.transform.parent, "it rides her");
            Assert.AreEqual(1, _performed.Count);

            // WALK.
            StandAt(AshorePos + new Vector3(6f, 3f, 0f));
            yield return null;
            Assert.AreEqual(AshorePos.x + 6f, can.transform.position.x, 1f, "it went with her");

            // PRESS TWO — set it down. Compared against where she ACTUALLY stands, not the constant she
            // was sent to: her body is a Rigidbody2D and a physics step may have settled it a hair.
            Vector2 feet = _hands.transform.position;
            Assert.IsTrue(_switcher.BeginInteract(), "the same key puts it down");
            Assert.IsFalse(can.IsCarried);
            Assert.IsNull(can.transform.parent, "back on the ground");
            Assert.AreEqual(feet.x, can.transform.position.x, 1e-3f, "at her feet");
            Assert.AreEqual(feet.y, can.transform.position.y, 1e-3f);
            Assert.AreEqual(2, _performed.Count, "two presses, two actions");
        }

        [UnityTest]
        public IEnumerator One_press_does_one_thing()
        {
            // The verb is a PRESS EDGE and TryPerform resolves once — so a press can never both set down
            // and pick straight back up, which is the shape the cast-latch bug (#181) took.
            CarriableFuelContainer can = RealContainer(AshorePos + new Vector3(0.6f, 0f, 0f));
            yield return null;

            _switcher.BeginInteract();
            Assert.IsTrue(can.IsCarried, "one press picked it up and stopped there");
            Assert.AreEqual(1, _performed.Count);

            // Frames pass with no new press: nothing may happen on its own.
            yield return null;
            yield return null;
            Assert.IsTrue(can.IsCarried, "it stays carried until the next press");
            Assert.AreEqual(1, _performed.Count, "no action without a press edge");
        }

        [UnityTest]
        public IEnumerator The_fill_survives_a_real_carry()
        {
            CarriableFuelContainer can = RealContainer(AshorePos + new Vector3(0.6f, 0f, 0f));
            var presenter = can.GetComponent<FuelLevelPresenter>();
            presenter.Fill = 0.75f;
            yield return null;

            _switcher.BeginInteract();
            // Assert the carry HAPPENED, or this test passes just as well when nothing happens at all —
            // an untouched can also still reads 0.75.
            Assert.IsTrue(can.IsCarried, "she picked it up");

            StandAt(AshorePos + new Vector3(12f, -5f, 0f));
            yield return null;
            _switcher.BeginInteract();
            yield return null;
            Assert.IsFalse(can.IsCarried, "and put it down again");

            Assert.AreEqual(0.75f, presenter.Fill, 1e-4f, "a carried can does not empty itself");
        }

        // ---- the derived facing -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator The_carried_facing_follows_the_body_every_frame()
        {
            CarriableFuelContainer can = RealContainer(AshorePos + new Vector3(0.6f, 0f, 0f));
            var presenter = can.GetComponent<FuelLevelPresenter>();
            yield return null;
            _switcher.BeginInteract();

            // Clockwise from north, eight cells. The skin's LateUpdate settles the held heading and
            // CarryHands (execution order 100) reads it in the SAME frame, after it.
            var expected = new (float heading, int cell)[]
            {
                (0f, 0), (90f, 2), (180f, 4), (270f, 6), (45f, 1), (135f, 3),
            };

            foreach ((float heading, int cell) in expected)
            {
                _skin.HoldHeading(heading);
                yield return null;
                Assert.AreEqual(heading, _skin.HeadingDegrees, 1e-3f, "the body is drawn at this heading");
                Assert.AreEqual(cell, presenter.Facing,
                                $"a can carried by a fisher drawn at {heading}° shows cell {cell}");
            }
        }

        [UnityTest]
        public IEnumerator A_set_down_container_stops_taking_its_facing_from_her()
        {
            CarriableFuelContainer can = RealContainer(AshorePos + new Vector3(0.6f, 0f, 0f));
            var presenter = can.GetComponent<FuelLevelPresenter>();
            yield return null;

            _switcher.BeginInteract();
            _skin.HoldHeading(90f);
            yield return null;
            Assert.AreEqual(2, presenter.Facing, "carried, it faces east with her");

            _switcher.BeginInteract();          // set it down
            _skin.HoldHeading(270f);            // she turns right round
            yield return null;
            yield return null;

            Assert.AreEqual(2, presenter.Facing, "on the ground it keeps the facing it was put down at");
        }

        // ---- the sorting band ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator The_carried_can_rides_the_bodys_sorting_band()
        {
            CarriableFuelContainer can = RealContainer(AshorePos + new Vector3(0.6f, 0f, 0f));
            var canRenderer = can.GetComponent<SpriteRenderer>();
            yield return null;
            _switcher.BeginInteract();

            // Facing the camera: the can is between her and the lens.
            _skin.HoldHeading(180f);
            yield return null;
            Assert.AreEqual(_bodyRenderer.sortingOrder + 1, canRenderer.sortingOrder,
                            "in front of a fisher facing us");
            Assert.AreEqual(_bodyRenderer.sortingLayerID, canRenderer.sortingLayerID,
                            "and in the same layer, or the order means nothing");

            // Facing away: it is behind her shoulder.
            _skin.HoldHeading(0f);
            yield return null;
            Assert.AreEqual(_bodyRenderer.sortingOrder - 1, canRenderer.sortingOrder,
                            "behind a fisher facing away");

            // The datum is the BODY's live order, not a parked constant: move the body's band and the
            // can must follow it.
            _bodyRenderer.sortingOrder = 137;
            yield return null;
            Assert.AreEqual(136, canRenderer.sortingOrder, "it rides whatever the body's Y-sort wrote");
        }

        [UnityTest]
        public IEnumerator A_container_sorts_itself_again_once_it_is_put_down()
        {
            CarriableFuelContainer can = RealContainer(AshorePos + new Vector3(0.6f, 0f, 0f));
            var ySort = can.GetComponent<YSortSprite>();
            var canRenderer = can.GetComponent<SpriteRenderer>();
            yield return null;
            int orderWhereItStood = canRenderer.sortingOrder;

            _switcher.BeginInteract();
            yield return null;
            // ⚠️ IsCarried first. A static YSortSprite parks its OWN dispatch on enable, so `enabled` is
            // false in play mode whether or not the hands ever took it — on its own that assertion passes
            // when nothing happened at all (it did, under the sabotage run).
            Assert.IsTrue(can.IsCarried, "she is carrying it");
            Assert.IsFalse(ySort.enabled, "and the hands own the renderer while she does");

            // Carry it somewhere with a genuinely different Y, or "it sorted itself again" is satisfied by
            // the order it already had.
            StandAt(AshorePos + new Vector3(0f, -8f, 0f));
            yield return null;
            _switcher.BeginInteract();
            yield return null;
            Assert.IsFalse(can.IsCarried, "and set it down there");

            int expected = YSortSprite.OrderFor(can.transform.position.y, SortingBands.DecorBase,
                                                SortingBands.OrdersPerMetre, SortingBands.DecorFloor,
                                                SortingBands.DecorCeiling);
            Assert.AreNotEqual(orderWhereItStood, expected,
                               "precondition: it must have moved far enough to re-sort at all");
            Assert.AreEqual(expected, canRenderer.sortingOrder, "back in the decor band at its NEW Y");
        }

        // ---- what must NOT happen -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator An_unliftable_container_is_never_offered_the_press()
        {
            // A bulk tank at her feet. It is not an unavailable candidate — it is not a candidate.
            RealContainer(AshorePos + new Vector3(0.4f, 0f, 0f),
                          Def(carriable: false, facings: 4, id: "fuelstore.gas_bulk_s10k"));
            yield return null;

            Assert.AreEqual(0, Interactables.Count, "nothing registered");
            Assert.IsFalse(_switcher.BeginInteract(), "the press falls through untouched");
            Assert.IsFalse(_hands.IsCarrying);
            Assert.AreEqual(0, _performed.Count);
        }

        [UnityTest]
        public IEnumerator With_nothing_registered_the_press_is_exactly_what_it_was()
        {
            // The control for the whole suite, and the thing that must stay green when the registration
            // is sabotaged: an empty registry means the verb never fires and every pre-existing interact
            // behaviour is reached unchanged.
            yield return null;

            Assert.AreEqual(0, Interactables.Count);
            Assert.IsFalse(_switcher.BeginInteract());
            Assert.IsFalse(_hands.IsCarrying);
            Assert.AreEqual(0, _performed.Count);
        }

        /// <summary>
        /// The REGISTRATION HOOK, which only PlayMode can show. Edit mode does not run <c>OnEnable</c> for
        /// a plain MonoBehaviour, so <c>FuelCarryTests</c> can only prove the registration RULE (who
        /// belongs in the registry) by driving it explicitly. That a live container puts ITSELF in and
        /// takes itself out — the scene-scoped contract every registrant on this seam signs — is this.
        /// </summary>
        [UnityTest]
        public IEnumerator A_live_container_registers_itself_and_relinquishes_on_disable()
        {
            CarriableFuelContainer can = RealContainer(AshorePos + new Vector3(0.6f, 0f, 0f));
            yield return null;

            Assert.AreEqual(1, Interactables.Count, "its own OnEnable put it there");
            Assert.AreSame(can, Interactables.Active[0]);

            can.enabled = false;
            yield return null;
            Assert.AreEqual(0, Interactables.Count, "a disabled component must not be actable");
            Assert.IsFalse(_switcher.BeginInteract(), "and the press finds nothing");

            can.enabled = true;
            yield return null;
            Assert.AreEqual(1, Interactables.Count, "re-enabling puts it back, exactly once");
        }

        [UnityTest]
        public IEnumerator A_can_out_of_reach_is_not_picked_up()
        {
            RealContainer(AshorePos + new Vector3(6f, 0f, 0f));   // well past the 1.5 m reach
            yield return null;

            Assert.IsFalse(_switcher.BeginInteract());
            Assert.IsFalse(_hands.IsCarrying);
        }

        /// <summary>
        /// <b>THE KNOWN COST OF CONSULTING THE VERB LAST</b>, pinned rather than left as prose.
        ///
        /// <para>A can standing inside board reach of a boardable boat loses the press to boarding —
        /// <c>ControlSwitcher.BeginInteract</c> answers boarding before it looks at the registry, which is
        /// what made the seam provably non-regressive when it landed. The lead-architect ruling on #503
        /// pre-approves the end state (boarding registers as a candidate and the resolver arbitrates it by
        /// facing, priority and distance). <b>When that is built this test SHOULD go red</b> — it is the
        /// tripwire, not the requirement. Flip it loudly, cite the ruling, and keep
        /// board-when-nothing-else-competes identical.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator A_can_beside_a_boardable_boat_loses_the_press_to_boarding()
        {
            StandAt(BoatPos + new Vector3(0.5f, 0f, 0f));         // inside board reach
            CarriableFuelContainer can = RealContainer(BoatPos + new Vector3(0.7f, 0f, 0f));
            yield return null;

            Assert.AreEqual(1, Interactables.Count, "the can IS registered — it simply does not get asked");
            Assert.IsTrue(_switcher.BeginInteract(), "the press boarded");
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode);
            Assert.IsFalse(can.IsCarried, "and the can is still on the wharf");
        }

        [UnityTest]
        public IEnumerator Boarding_while_carrying_keeps_hold_of_the_can()
        {
            // A person stepping aboard does not drop what they are holding. Setting it down ON a deck is
            // separate work — it would have to ride the hull, which is the deck-occupant seam's job.
            CarriableFuelContainer can = RealContainer(AshorePos + new Vector3(0.6f, 0f, 0f));
            yield return null;
            _switcher.BeginInteract();
            Assert.IsTrue(can.IsCarried);

            StandAt(BoatPos + new Vector3(0.5f, 0f, 0f));
            yield return null;
            Assert.IsTrue(_switcher.BeginInteract(), "she boards");
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode);

            yield return null;
            Assert.IsTrue(can.IsCarried, "still in her hands");

            // That the DECK offers her nowhere to put it is the context rule, and it is proved without a
            // boat in FuelCarryTests — asserting it through a second press here would really be testing
            // where the helm is on this rig, which is a different suite's business.
            InteractActor onDeck = InteractActor.For(_hands.transform.position, Vector2.zero,
                                                     ControlMode.OnDeck);
            Assert.IsFalse(InteractResolver.TryResolveNow(onDeck,
                               InteractResolver.DefaultFacingArcDegrees, out _),
                           "the deck offers her nowhere to set it down");
        }
    }
}
