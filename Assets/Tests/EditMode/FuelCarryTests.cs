using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Carrying a fuel container</b> — everything about the loop that can be proved without a frame.
    ///
    ///   • THE FACING DERIVATION — which baked cell a carried container shows for the heading its carrier
    ///     is drawn at. Pinned as a table over all eight facings, because the fuel kit is one of only
    ///     three rigs that genuinely bake CLOCKWISE and the other nineteen do not: a later "fix" toward
    ///     the majority convention would rotate every carried can by its reflection and nothing else
    ///     would notice.
    ///   • THE TWO GATES — what may be lifted and what may be set down, including which refusal wins when
    ///     two apply at once.
    ///   • THE SORT SIDE — that the carried sprite draws in front of a fisher facing the camera and
    ///     behind one facing away, and never ties with her.
    ///   • REGISTRATION — that a carriable container becomes an interact candidate and an unliftable one
    ///     does NOT, which is the thing that stops a bulk tank stealing the press meant for the can
    ///     beside it.
    ///   • THE RUNG FLIP — Portable on the ground, Held in the hands. That flip is the whole state
    ///     machine; there is no mode flag to test because there is no mode flag.
    ///
    /// <para>The JOIN — a real press through <c>ControlSwitcher</c> moving a real object between the
    /// ground and the fisher's hands over real frames — is PlayMode's (<c>FuelCarryPlayTests</c>).</para>
    /// </summary>
    public class FuelCarryTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // ---- fixtures ---------------------------------------------------------------------------

        private FuelContainerDef Def(bool carriable = true, int facings = 8, string id = "fuelstore.gas_jerry_s20")
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

        /// <summary>
        /// A container object built the way the dev spawner builds one — Def FIRST, then the component,
        /// which is the order registration depends on.
        ///
        /// <para>⚠️ <b>The <c>RefreshRegistration()</c> call is load-bearing and must not be tidied away.</b>
        /// Edit mode does not run <c>OnEnable</c> for a plain MonoBehaviour — only for an
        /// <c>[ExecuteAlways]</c> one, which is why <see cref="FuelLevelPresenter"/> wakes here and
        /// <see cref="CarriableFuelContainer"/> does not. So the REGISTRATION HOOK is a PlayMode claim
        /// (<c>FuelCarryPlayTests</c> proves a live container registers itself and relinquishes on
        /// disable); what this suite proves is the registration RULE — who belongs in the registry —
        /// driven explicitly. Without this line the registry stays empty and every "is it a candidate?"
        /// assertion below passes for the wrong reason.</para>
        /// </summary>
        private CarriableFuelContainer Container(FuelContainerDef def, Vector2 at = default)
        {
            var go = new GameObject("FuelContainer");
            go.transform.position = at;
            _spawned.Add(go);

            var presenter = go.AddComponent<FuelLevelPresenter>();
            presenter.Container = def;
            var carriable = go.AddComponent<CarriableFuelContainer>();
            carriable.RefreshRegistration();
            return carriable;
        }

        private CarryHands Hands(Vector2 at = default)
        {
            var go = new GameObject("Player");
            go.transform.position = at;
            _spawned.Add(go);
            go.AddComponent<SpriteRenderer>();
            return go.AddComponent<CarryHands>();
        }

        // ---- the facing derivation ----------------------------------------------------------------

        // Clockwise from north, eight cells: N NE E SE S SW W NW. If this table ever needs "fixing",
        // check RigCatalog.FuelKit's measured AzimuthConvention.Clockwise before changing a number here —
        // FuelRegistrationProbe re-measures it at every bake and the declaration is what it enforces.
        [TestCase(0f, 0)]     // N
        [TestCase(45f, 1)]    // NE
        [TestCase(90f, 2)]    // E
        [TestCase(135f, 3)]   // SE
        [TestCase(180f, 4)]   // S
        [TestCase(225f, 5)]   // SW
        [TestCase(270f, 6)]   // W
        [TestCase(315f, 7)]   // NW
        public void BakedFacingIndex_is_clockwise_from_north(float heading, int expected)
            => Assert.AreEqual(expected, CarryMath.BakedFacingIndex(heading, 8));

        [Test]
        public void BakedFacingIndex_wraps_and_survives_negative_headings()
        {
            Assert.AreEqual(0, CarryMath.BakedFacingIndex(360f, 8), "360 is north again");
            Assert.AreEqual(0, CarryMath.BakedFacingIndex(720f, 8), "two turns is still north");
            Assert.AreEqual(6, CarryMath.BakedFacingIndex(-90f, 8), "-90 is west");
            Assert.AreEqual(7, CarryMath.BakedFacingIndex(-45f, 8), "-45 is north-west");
        }

        [Test]
        public void BakedFacingIndex_snaps_to_the_nearest_cell()
        {
            Assert.AreEqual(2, CarryMath.BakedFacingIndex(80f, 8), "80 is nearer east than north-east");
            Assert.AreEqual(1, CarryMath.BakedFacingIndex(40f, 8), "40 is nearer north-east than north");
        }

        [Test]
        public void BakedFacingIndex_is_null_safe_on_an_unbuilt_def()
        {
            // A Def whose bake never ran has Facings 0. Drawing cell 0 is what FuelContainerDef.Frame
            // already answers with; throwing here would take the whole press down over missing art.
            Assert.AreEqual(0, CarryMath.BakedFacingIndex(123f, 0));
            Assert.AreEqual(0, CarryMath.BakedFacingIndex(123f, -4));
        }

        [Test]
        public void BakedFacingIndex_handles_a_four_facing_bake()
        {
            // Not every vessel bakes eight — the standing kit bakes four. The rule is general in the
            // count, so a four-cell Def resolves N/E/S/W without a second code path.
            Assert.AreEqual(0, CarryMath.BakedFacingIndex(0f, 4));
            Assert.AreEqual(1, CarryMath.BakedFacingIndex(90f, 4));
            Assert.AreEqual(2, CarryMath.BakedFacingIndex(180f, 4));
            Assert.AreEqual(3, CarryMath.BakedFacingIndex(270f, 4));
        }

        [Test]
        public void HeadingFor_reads_screen_axes_as_compass_bearings()
        {
            Assert.AreEqual(0f, CarryMath.HeadingFor(Vector2.up), 1e-3f, "+Y is north");
            Assert.AreEqual(90f, CarryMath.HeadingFor(Vector2.right), 1e-3f, "+X is east");
            Assert.AreEqual(180f, CarryMath.HeadingFor(Vector2.down), 1e-3f, "−Y is south");
            Assert.AreEqual(270f, CarryMath.HeadingFor(Vector2.left), 1e-3f, "−X is west");
        }

        [Test]
        public void The_four_way_walk_facings_all_resolve_to_a_baked_cell()
        {
            // The fallback path, end to end: the four-way enum → a unit vector → a bearing → a cell.
            // These are the four cells a fisher drawn by the old FisherSheet can ever show a can at.
            Assert.AreEqual(0, Cell(Facing.Up), "walking north shows the north cell");
            Assert.AreEqual(2, Cell(Facing.Right), "east");
            Assert.AreEqual(4, Cell(Facing.Down), "south");
            Assert.AreEqual(6, Cell(Facing.Left), "west");

            static int Cell(Facing f)
                => CarryMath.BakedFacingIndex(
                       CarryMath.HeadingFor(PlayerWalkController.FacingUnitVector(f)), 8);
        }

        // ---- the two gates ------------------------------------------------------------------------

        [Test]
        public void CanPickUp_allows_a_carriable_vessel_with_free_hands()
            => Assert.AreEqual(CarryRefusal.None, CarryMath.CanPickUp(carriable: true, handsFull: false));

        [Test]
        public void CanPickUp_refuses_a_vessel_that_is_not_carriable()
            => Assert.AreEqual(CarryRefusal.NotCarriable,
                               CarryMath.CanPickUp(carriable: false, handsFull: false));

        [Test]
        public void CanPickUp_says_hands_full_before_it_says_too_heavy()
        {
            // Order matters: standing over a bulk tank with a can already in hand, the useful sentence is
            // the one about the thing you can do something about.
            Assert.AreEqual(CarryRefusal.HandsFull, CarryMath.CanPickUp(carriable: false, handsFull: true));
            Assert.AreEqual(CarryRefusal.HandsFull, CarryMath.CanPickUp(carriable: true, handsFull: true));
        }

        [Test]
        public void CanPlace_allows_a_carried_thing_on_valid_ground()
            => Assert.AreEqual(CarryRefusal.None, CarryMath.CanPlace(carrying: true, groundIsValid: true));

        [Test]
        public void CanPlace_refuses_with_empty_hands_and_refuses_bad_ground()
        {
            Assert.AreEqual(CarryRefusal.NothingCarried,
                            CarryMath.CanPlace(carrying: false, groundIsValid: true));
            Assert.AreEqual(CarryRefusal.NoGround,
                            CarryMath.CanPlace(carrying: true, groundIsValid: false));
        }

        [Test]
        public void Every_refusal_has_something_to_say_and_success_says_nothing()
        {
            Assert.IsNull(CarryMath.Message(CarryRefusal.None), "nothing was refused");
            foreach (CarryRefusal r in System.Enum.GetValues(typeof(CarryRefusal)))
            {
                if (r == CarryRefusal.None) continue;
                Assert.IsFalse(string.IsNullOrWhiteSpace(CarryMath.Message(r)),
                               $"{r} would show an empty toast");
            }
        }

        // ---- which side of the body it draws on -----------------------------------------------------

        [Test]
        public void The_can_draws_in_front_when_she_faces_the_camera_and_behind_when_she_faces_away()
        {
            Assert.AreEqual(-1, CarryMath.AheadOrdersFor(0f, 1), "facing north — away — so behind her");
            Assert.AreEqual(1, CarryMath.AheadOrdersFor(180f, 1), "facing south — at the camera — in front");
            Assert.AreEqual(-1, CarryMath.AheadOrdersFor(45f, 1), "north-east still has her back to us");
            Assert.AreEqual(1, CarryMath.AheadOrdersFor(135f, 1), "south-east faces us");
        }

        [Test]
        public void Dead_abeam_draws_in_front_rather_than_tying_or_coin_flipping()
        {
            // Beside her, either answer reads the same — but a TIE sorts by draw order, and draw order is
            // not stable, which is a flicker with no owner. ⚠️ And the sign here is NOT free: cos(±90°) is
            // a few times 1e-8 of arbitrary sign in float, so without CarryMath's epsilon these two land
            // on whichever side the rounding fell. Asserting the SIDE, not just non-zero, is what keeps
            // that hole closed.
            Assert.AreEqual(1, CarryMath.AheadOrdersFor(90f, 1), "due east draws in front");
            Assert.AreEqual(1, CarryMath.AheadOrdersFor(270f, 1), "due west draws in front");
        }

        [Test]
        public void One_degree_off_abeam_still_reads_the_body()
        {
            // The epsilon must not be so wide it swallows real headings: a degree either side of abeam
            // still answers from which way she is actually turned.
            Assert.AreEqual(-1, CarryMath.AheadOrdersFor(89f, 1), "89° still has her back to us");
            Assert.AreEqual(1, CarryMath.AheadOrdersFor(91f, 1), "91° has turned toward us");
        }

        // ---- registration: the thing that makes it a candidate at all ---------------------------------

        [Test]
        public void A_carriable_container_belongs_in_the_registry()
        {
            CarriableFuelContainer can = Container(Def(carriable: true));

            Assert.AreEqual(1, Interactables.Count, "a jerry can is something the verb can act on");
            Assert.AreSame(can, Interactables.Active[0]);
        }

        [Test]
        public void An_unliftable_container_is_kept_out_of_the_registry()
        {
            // Not an unavailable candidate — absent. A 10,000 L tank registered at the same rung as the
            // can beside it could win on distance and eat the press meant for the can.
            Container(Def(carriable: false, facings: 4, id: "fuelstore.gas_bulk_s10k"));

            Assert.AreEqual(0, Interactables.Count);
        }

        [Test]
        public void The_registration_rule_relinquishes_as_well_as_registers()
        {
            // Both directions, or "not carriable" would only ever mean "never joined" and a container
            // whose Def changed under it would linger in the registry as a candidate that cannot be lifted.
            FuelContainerDef def = Def(carriable: true);
            CarriableFuelContainer can = Container(def);
            Assert.AreEqual(1, Interactables.Count);

            def.Carriable = false;
            can.RefreshRegistration();
            Assert.AreEqual(0, Interactables.Count, "it stopped being something you can pick up");

            def.Carriable = true;
            can.RefreshRegistration();
            Assert.AreEqual(1, Interactables.Count, "and back again, without duplicating");
        }

        [Test]
        public void Two_spawned_cans_of_one_def_do_not_share_an_id()
        {
            // Duplicate ids make the resolver's order non-total, and registration order — the one thing
            // the whole seam refuses to depend on — would decide between them.
            FuelContainerDef def = Def();
            CarriableFuelContainer a = Container(def, new Vector2(1f, 0f));
            CarriableFuelContainer b = Container(def, new Vector2(2f, 0f));

            Assert.AreNotEqual(a.Id, b.Id);
            Assert.IsNotEmpty(a.Id);
        }

        [Test]
        public void Configure_gives_a_readable_id_and_takes_effect_after_registration()
        {
            CarriableFuelContainer can = Container(Def());
            can.Configure("fuelstore.gas_jerry_s20.spawn_1");

            Assert.AreEqual("fuelstore.gas_jerry_s20.spawn_1", can.Id, "the cached id must be re-derived");
        }

        // ---- the rung flip: the whole state machine ---------------------------------------------------

        [Test]
        public void A_container_is_Portable_on_the_ground_and_Held_once_lifted()
        {
            CarryHands hands = Hands();
            CarriableFuelContainer can = Container(Def(), new Vector2(0.5f, 0f));

            Assert.AreEqual(InteractPriority.Portable, can.Priority, "lying about");
            Assert.IsFalse(can.IsCarried);

            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(can));

            Assert.AreEqual(InteractPriority.Held, can.Priority, "in her hands");
            Assert.IsTrue(can.IsCarried);
            Assert.AreSame(can, hands.Carried);
        }

        [Test]
        public void The_held_rung_beats_the_loose_one_so_E_means_put_it_down()
        {
            // The loop closes on the ladder alone: with a can in hand and another at her feet, the press
            // belongs to the one she is holding.
            CarryHands hands = Hands();
            CarriableFuelContainer held = Container(Def(), Vector2.zero);
            CarriableFuelContainer loose = Container(Def(id: "fuelstore.gas_jerry_s10"), new Vector2(0.2f, 0f));
            hands.TryPickUp(held);

            InteractActor actor = InteractActor.For(Vector2.zero, Vector2.up, ControlMode.OnFoot);
            Assert.IsTrue(InteractResolver.TryResolveNow(actor, InteractResolver.DefaultFacingArcDegrees,
                                                         out IInteractable best));
            Assert.AreSame(held, best, "what you are carrying is the most specific answer to 'act'");
            Assert.AreNotSame(loose, best);
        }

        [Test]
        public void Hands_hold_exactly_one_thing()
        {
            CarryHands hands = Hands();
            CarriableFuelContainer first = Container(Def(), new Vector2(0.3f, 0f));
            CarriableFuelContainer second = Container(Def(id: "fuelstore.gas_jerry_s10"), new Vector2(0.4f, 0f));

            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(first));
            Assert.AreEqual(CarryRefusal.HandsFull, hands.TryPickUp(second), "one at a time");
            Assert.AreSame(first, hands.Carried, "the refusal must not have swapped what she holds");
            Assert.IsFalse(second.IsCarried);
        }

        [Test]
        public void Setting_down_with_empty_hands_is_refused_not_crashed()
            => Assert.AreEqual(CarryRefusal.NothingCarried, Hands().TryPlace());

        [Test]
        public void A_container_rides_the_carrier_and_is_left_where_she_stood()
        {
            var hands = Hands(new Vector2(10f, 4f));
            CarriableFuelContainer can = Container(Def(), new Vector2(10.4f, 4f));

            hands.TryPickUp(can);
            Assert.AreSame(hands.transform, can.transform.parent, "it rides her");

            hands.transform.position = new Vector2(25f, -3f);
            hands.ApplyCarriedPose();
            Assert.AreEqual(25f, can.transform.position.x, 1f, "it went with her");

            Assert.AreEqual(CarryRefusal.None, hands.TryPlace());
            Assert.IsNull(can.transform.parent, "back on the ground, not on her");
            Assert.AreEqual(new Vector2(25f, -3f), (Vector2)can.transform.position, "at her feet");
            Assert.IsNull(hands.Carried);
            Assert.IsFalse(can.IsCarried);
        }

        [Test]
        public void The_fill_survives_the_carry()
        {
            // Nothing carries the fill across — the GameObject is re-parented, never rebuilt, so the
            // presenter holding the level is the same component throughout. This pins that it stays so.
            CarryHands hands = Hands();
            CarriableFuelContainer can = Container(Def(), new Vector2(0.5f, 0f));
            var presenter = can.GetComponent<FuelLevelPresenter>();
            presenter.Fill = 0.75f;
            int fillIndexBefore = presenter.FillIndex;

            hands.TryPickUp(can);
            hands.transform.position = new Vector2(40f, 12f);
            hands.ApplyCarriedPose();
            hands.TryPlace();

            Assert.AreEqual(0.75f, presenter.Fill, 1e-4f, "a carried can does not empty itself");
            Assert.AreEqual(fillIndexBefore, presenter.FillIndex);
        }

        [Test]
        public void A_carried_container_stays_registered_so_the_press_can_set_it_down()
        {
            CarryHands hands = Hands();
            CarriableFuelContainer can = Container(Def(), new Vector2(0.5f, 0f));
            hands.TryPickUp(can);

            Assert.AreEqual(1, Interactables.Count, "still a candidate — that is how E puts it down");
            Assert.AreSame(can, Interactables.Active[0]);
        }

        [Test]
        public void A_carried_container_is_never_out_of_its_own_reach()
        {
            // The hip offset must never be able to push the held can outside its pick-up radius and
            // strand it there, unable to be set down.
            CarryHands hands = Hands();
            CarriableFuelContainer can = Container(Def(), new Vector2(0.5f, 0f));
            hands.TryPickUp(can);
            hands.ApplyCarriedPose();

            InteractActor actor = InteractActor.For(hands.transform.position, Vector2.up, ControlMode.OnFoot);
            float distance = Vector2.Distance(actor.Position, can.WorldPosition);
            Assert.Less(distance, can.ReachMeters, "held is in reach by definition");
        }

        [Test]
        public void A_carried_container_is_not_a_candidate_from_the_deck()
        {
            // Setting a can down on a deck means it has to RIDE that hull, which is the deck-occupant
            // seam's job and not a re-parent. So board while carrying and you simply keep hold of it.
            CarryHands hands = Hands();
            CarriableFuelContainer can = Container(Def(), new Vector2(0.5f, 0f));
            hands.TryPickUp(can);

            InteractActor onDeck = InteractActor.For(Vector2.zero, Vector2.zero, ControlMode.OnDeck);
            Assert.IsFalse(InteractResolver.TryResolveNow(onDeck, InteractResolver.DefaultFacingArcDegrees,
                                                          out _),
                           "on deck the press is not offered the can");
        }

        // ---- the Core seam: the can is the FIRST implementor, not a special case ---------------------
        //
        // These pin the generalisation itself. The carry loop above used to be typed end-to-end to
        // CarriableFuelContainer; it is now typed to Core's ICarriable/ICarrier so that a lane which
        // cannot reference Player (Fishing) can still ask what is in the fisher's hands. Everything
        // above this line is the BEHAVIOUR that had to survive that move unchanged — these are the
        // claims the move itself makes.

        [Test]
        public void The_can_is_carriable_through_the_core_contract()
        {
            CarriableFuelContainer can = Container(Def());

            Assert.IsInstanceOf<ICarriable>(can, "the can implements the Core carry contract");
            Assert.IsInstanceOf<IInteractable>(can, "and still answers the press — the two are separate");

            var carriable = (ICarriable)can;
            Assert.IsTrue(carriable.IsCarriable);
            Assert.AreSame(can.transform, carriable.Transform, "the hands re-parent THIS");
            Assert.AreEqual(8, carriable.BakedFacings);
        }

        [Test]
        public void The_hands_are_a_carrier_and_report_the_held_thing_through_the_contract()
        {
            CarryHands hands = Hands();
            CarriableFuelContainer can = Container(Def(), new Vector2(0.5f, 0f));

            var carrier = (ICarrier)hands;
            Assert.IsFalse(carrier.IsCarrying, "empty hands");
            Assert.IsNull(carrier.Carried);

            hands.TryPickUp(can);

            Assert.IsTrue(carrier.IsCarrying);
            Assert.AreSame(can, carrier.Carried, "what the contract reports IS the object lifted");
        }

        [Test]
        public void DefId_is_the_KIND_and_Id_is_the_INSTANCE_and_they_are_not_the_same_string()
        {
            // ⚠️ The whole point of the two ids, and the one thing a later "tidy-up" would get wrong by
            // collapsing them. IInteractable.Id must be UNIQUE among live registrants (it is the
            // resolver's last tie-break, so duplicates make its order non-total). ICarriable.DefId must
            // be SHARED by every can of the same kind, because it is what a gate matches on. Two cans off
            // the same Def therefore agree on one and differ on the other — in both directions.
            FuelContainerDef def = Def(id: "fuelstore.gas_jerry_s20");
            CarriableFuelContainer a = Container(def, new Vector2(0f, 0f));
            CarriableFuelContainer b = Container(def, new Vector2(2f, 0f));

            Assert.AreEqual("fuelstore.gas_jerry_s20", ((ICarriable)a).DefId, "the KIND is the Def id");
            Assert.AreEqual(((ICarriable)a).DefId, ((ICarriable)b).DefId, "two gas cans are the same kind");
            Assert.AreNotEqual(a.Id, b.Id, "but they are not the same THING");
        }

        [Test]
        public void CarriedItem_answers_the_cross_module_question_without_a_Player_reference()
        {
            // This is the query ClamDig makes from the Fishing assembly, which cannot see CarryHands at
            // all. Driven through the explicit-hands overload because EditMode never fires OnEnable on a
            // plain MonoBehaviour, so nothing publishes GameServices.Hands here (see its remarks).
            CarryHands hands = Hands();
            CarriableFuelContainer can = Container(Def(id: "fuelstore.gas_jerry_s20"), new Vector2(0.5f, 0f));

            Assert.IsFalse(CarriedItem.InHand(hands, "fuelstore.gas_jerry_s20"), "nothing in hand yet");

            hands.TryPickUp(can);

            Assert.IsTrue(CarriedItem.InHand(hands, "fuelstore.gas_jerry_s20"), "a gas can is in hand");
            Assert.IsFalse(CarriedItem.InHand(hands, "tool.shovel"), "a gas can is not a shovel");
            Assert.IsFalse(CarriedItem.InHand(hands, null), "a null id asks nothing and gets false");
            Assert.IsFalse(CarriedItem.InHand(null, "fuelstore.gas_jerry_s20"),
                           "no published hands is 'carry on', never a throw");

            hands.TryPlace();
            Assert.IsFalse(CarriedItem.InHand(hands, "fuelstore.gas_jerry_s20"), "set down again");
        }

        [Test]
        public void The_hands_report_empty_when_what_they_held_was_destroyed_under_them()
        {
            // ⚠️ THE FAKE-NULL TRAP, pinned. The backing field is INTERFACE-typed, and an interface
            // reference does NOT carry UnityEngine.Object's == overload — so without the laundering in
            // CarryHands.Carried a destroyed can reads as a live ICarriable and every consumer's
            // `!= null` passes on a corpse. Remove the cast in that getter and this goes red; nothing
            // else in the suite would notice.
            CarryHands hands = Hands();
            CarriableFuelContainer can = Container(Def(), new Vector2(0.5f, 0f));
            hands.TryPickUp(can);
            Assert.IsTrue(hands.IsCarrying);

            Object.DestroyImmediate(can.gameObject);

            Assert.IsNull(hands.Carried, "a destroyed can is not something you are carrying");
            Assert.IsFalse(hands.IsCarrying);
            Assert.IsFalse(CarriedItem.InHand(hands, "fuelstore.gas_jerry_s20"));
        }
    }
}
