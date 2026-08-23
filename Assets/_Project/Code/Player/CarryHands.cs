using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The fisher's hands</b> — the one place that knows what is being carried, and the component that
    /// draws it there.
    ///
    /// <para><b>Why the state lives HERE and not on the thing carried.</b> "Which hand is free" is a
    /// single-occupancy invariant, and an invariant enforced by each of N objects individually is not
    /// enforced at all — two containers both believing they were picked up is a state no test would catch
    /// until the second one drew inside the first. The hands are the resource, so the hands own the slots,
    /// and <see cref="CarriableFuelContainer"/> asks them. It is also why this is a component and not a
    /// field on <see cref="FuelLevelPresenter"/>: a presenter draws what it is handed and must never be the
    /// thing that remembers (the lesson of the six private ride smoothers — state parked in a presenter is
    /// state nothing else can read or test).</para>
    ///
    /// <para><b>⭐ TWO HANDS, and what that changed.</b> There used to be one slot, so a landed fish
    /// DISPLACED the rod: the rod slung under her arm and the fish took the hand. The owner's ruling is
    /// that she has two hands and should use them — a fish in one and the rod in the other, a pail in one
    /// and the rod in the other. The rule itself is <see cref="HandSlots"/>, a plain object in Core with
    /// no scene and no components, so the whole truth table is provable headless; this class is what
    /// re-parents, poses and speaks for it.</para>
    ///
    /// <para><b>THE CONTINUITY LAW, as it lands here.</b> <i>While a hand is free, nothing already held
    /// moves.</i> Taking something up fills a FREE slot — it never swaps hands, never slings, never
    /// re-poses the other hand — and a refusal changes nothing at all. Two consequences worth stating,
    /// because both would otherwise look like bugs: (1) the sling now fires ONLY when both hands are
    /// genuinely committed, which is the one case where the alternative is dropping the catch on the
    /// floor; (2) a thing held ALONE still follows the rig's own per-facing preferred hand as she turns
    /// (the shipped look, where a small prop moves to the near hand on the away headings), while a thing
    /// sharing the pair keeps the hand it was given — because with two things held, a per-facing swap
    /// would slide one of them across the body and through the other.</para>
    ///
    /// <para><b>What is NOT here.</b> No key is read: the press arrives through <see cref="InteractVerb"/>
    /// (M2-39), which is the entire point of that seam — the dev key ledger is spent A–Z and a new feature
    /// registers a candidate rather than binding a letter. No fuel is consumed, nothing is refuelled and
    /// nothing is priced; that is phase-gated. No save is written: see the class remarks on persistence
    /// below.</para>
    ///
    /// <para><b>Persistence is deliberately absent.</b> A set-down container is SESSION-LOCAL — it exists
    /// until the scene does. The world-placed wiring (the <c>PlacedTrap</c> / ADR 0020 precedent) belongs
    /// to the PR that actually stands containers on a wharf. Parenting is restored on set-down so a
    /// container put back down lands in the region it came from rather than leaking into the persistent
    /// core, which is the honest half-measure until that PR exists.</para>
    ///
    /// <para><b>Rules.</b> Visual + state only; the sim is untouched (rule 5). Every offset and magnitude is
    /// a serialized tunable (rule 6). The per-frame path does no allocation and no <c>Find</c> — the
    /// renderer and the character skin are resolved once (rule 7).</para>
    /// </summary>
    [DisallowMultipleComponent]
    // Runs AFTER YSortSprite (default 0) has written the body's order for this frame, so the carried
    // sprite mirrors the order the player ACTUALLY drew at rather than last frame's. Same reason, and the
    // same number, as DeckRiderVisual.
    [DefaultExecutionOrder(100)]
    public sealed class CarryHands : MonoBehaviour, IHandsCarrier, ICatchHands
    {
        [Header("Wiring (auto-resolved off this object if empty)")]
        [Tooltip("The player's own renderer — the SORTING DATUM the carried sprite rides. Null = the " +
                 "carried sprite keeps whatever order it had, which is wrong but never invisible.")]
        [SerializeField] private SpriteRenderer _bodyRenderer;

        [Tooltip("The 8-direction iso skin — the source of the DRAWN heading the carried object's facing " +
                 "is derived from. Absent (no iso art built) falls back to the four-way walk facing below.")]
        [SerializeField] private IsoCharacterSprite _character;

        [Tooltip("The four-way walk controller — the FALLBACK heading source when the iso skin is absent, " +
                 "matching the fallback the player's own picture uses.")]
        [SerializeField] private PlayerWalkController _walk;

        [Header("Where it hangs")]
        [Tooltip("The character rig's HAND-PROP table (imported from the baked sidecar): per prop, per " +
                 "facing, per hand, which wrist holds it, how far off that wrist it sits and whether it " +
                 "draws over or under. Anything whose ICarryAnchored.HandPropKey names a row in here is " +
                 "posed from it. Null, or a carried thing with no row, falls back to the single offset " +
                 "below.")]
        [SerializeField] private CarryAnchorTableDef _carryAnchors;

        [Header("How it is held (greybox tunables, rule 6)")]
        [Tooltip("FALLBACK only — where a carried object with no hand-prop row sits relative to the " +
                 "fisher's feet, in metres, on her RIGHT side. ⚠️ ONE offset for every facing, which is " +
                 "why it is only a fallback now: the fuel kit bakes no hand anchors and the rig bakes no " +
                 "spade or pail row, so a jerry can, a shovel and the dev bucket still hang here. " +
                 "Everything the rig DOES bake a row for (the rod, a fish, a handful of clams) is posed " +
                 "from the table above and ignores this.")]
        [SerializeField] private Vector2 _hipOffsetMeters = new Vector2(0.28f, 0.34f);

        [Tooltip("How many sorting orders the carried sprite draws ahead of the body when the fisher " +
                 "faces the camera (and behind it when she faces away) — see CarryMath.AheadOrdersFor. " +
                 "1 is one step of the decor band; 0 makes it tie with the body and sort by draw order, " +
                 "which flickers.")]
        [SerializeField, Min(0)] private int _aheadOrders = 1;

        private readonly HandSlots _slots = new HandSlots();

        // Where each hand's occupant hung before it was lifted, indexed by hand (0 = left, 1 = right).
        // Two fields rather than a dictionary because there are exactly two hands and the set-down path
        // must not allocate to answer "where did this come from".
        private readonly Transform[] _placedParents = new Transform[2];

        private ICarriable _lastTaken;
        private bool _resolved;

        /// <summary>
        /// <b>The two hands</b> — which hand holds what, and the whole sharing rule. Public so the interact
        /// candidates and the tests can ask; nothing outside this class may mutate it, which is why the
        /// verbs below are the only writers.
        /// </summary>
        public HandSlots Slots => _slots;

        /// <summary>
        /// The thing she took up MOST RECENTLY, or null — the single-slot read
        /// <see cref="ICarrier.Carried"/> has always been, generalised in the only way that keeps its old
        /// meaning: with one slot, the last thing put there IS the thing in her hands.
        ///
        /// <para><b>⚠️ Do not ask this "is X in her hands".</b> With two slots it answers only for the
        /// hand she filled last, so the off hand reads as empty. Every such question goes through
        /// <see cref="CarriedItem.IsHeld"/> / <see cref="CarriedItem.InHand"/> /
        /// <see cref="CarriedItem.Held"/>, which consult both and handle a one-slot carrier too.</para>
        ///
        /// <para>The getter launders Unity's fake-null: an interface reference does not carry
        /// <see cref="Object"/>'s <c>==</c> overload, so a carried object destroyed out from under the
        /// hands would read here as a live <see cref="ICarriable"/> and hand every consumer a corpse whose
        /// <c>!= null</c> passes.</para>
        /// </summary>
        public ICarriable Carried
        {
            get
            {
                ICarriable last = _lastTaken is Object o && o == null ? null : _lastTaken;
                if (last != null && _slots.SideOf(last) != CarryHandSide.None) return last;
                return _slots.Right ?? _slots.Left;
            }
        }

        /// <summary>True when either hand holds something.</summary>
        public bool IsCarrying => !_slots.IsEmpty;

        // ---- the Core relay ------------------------------------------------------------------------

        /// <summary>
        /// Publish these hands so lanes that cannot reference Player can still ask what is held
        /// (<see cref="GameServices.Hands"/> — the seam <c>ClamDig</c> reads across the Fishing boundary).
        /// </summary>
        private void OnEnable()
        {
            GameServices.Hands = this;
            GameServices.CatchHands = this;
        }

        /// <summary>
        /// Relinquish the relay — <b>in <c>OnDestroy</c>, and deliberately NOT in <c>OnDisable</c>.</b>
        ///
        /// <para>⚠️ The house law, learned the expensive way (fix/interior-reveal-travel): root-toggling
        /// IS how a region hop works, so "disabled" happens constantly and does not mean "gone". A service
        /// cleared on disable is a service wiped mid-crossing, and the symptom is silent and total — every
        /// consumer reads the null as "the thing does not exist". Whoever registers, unregisters, on
        /// destroy, guarded on still owning the slot (<c>GameRoot.OnDestroy</c> is the reference
        /// implementation).</para>
        /// </summary>
        private void OnDestroy()
        {
            if (ReferenceEquals(GameServices.Hands, this)) GameServices.Hands = null;
            if (ReferenceEquals(GameServices.CatchHands, this)) GameServices.CatchHands = null;
        }

        // ---- the catch seam: a landed catch takes the free hand ---------------------------------------

        /// <summary>
        /// What is SLUNG — the tool tucked away to free a hand for a landed catch, or null.
        ///
        /// <para><b>It is not a third carry slot and it cannot be used as one:</b> nothing may put anything
        /// here deliberately. It exists for the one case two hands still cannot cover — <b>both</b> hands
        /// already committed when a catch lands, where the alternative is dropping the fish on the floor.
        /// That is rarer than it used to be: with one slot the shovel slung on EVERY dug clam, and now it
        /// only slings if the other hand is holding something too.</para>
        ///
        /// <para>The sling fills only in <see cref="TryPutInHand"/> and empties the moment a hand is
        /// free.</para>
        /// </summary>
        public ICarriable Slung => _slung is Object o && o == null ? null : _slung;

        private ICarriable _slung;

        /// <summary>
        /// Put a landed catch in her hands (<see cref="ICatchHands.TryPutInHand"/>).
        ///
        /// <para>Refuses — returning false, so the caller lands it in its own hold instead — when a hand
        /// already holds a CATCH (the over-encumbrance rule in its cheapest honest form: full is full, and
        /// you deal with the fish you have before you land another), and when the fish is too long for one
        /// hand and she is not empty-handed enough to cradle it.</para>
        ///
        /// <para><b>The size rule.</b> A fish past <see cref="GameConfig.MaxOneHandFishLengthMeters"/> is
        /// cradled across both arms, so it needs BOTH hands free. It will not displace the rod to get
        /// them — that would move something already held — so with anything at all in her hands a big fish
        /// simply does not come aboard by hand and goes into the hold the caller offers instead. Which is
        /// the honest picture: you do not one-hand a twelve-kilo cod while holding a rod.</para>
        ///
        /// <para><b>Nothing changes on a refusal.</b> Including the sling: if the tool is tucked away and
        /// the catch STILL cannot be taken, the tool comes straight back out. A half-applied refusal is
        /// the failure mode the continuity law exists to prevent.</para>
        /// </summary>
        public bool TryPutInHand(in CatchItem item)
        {
            if (Slung != null) return false;                  // a sling outstanding means a hand is busy

            float lengthMeters = CatchSize.LengthMeters(item);
            bool cradle = HandSlots.NeedsBothHands(lengthMeters, GameServices.MaxOneHandFishLengthMeters);

            // Asked BEFORE anything is built: a catch is spawned, so a refusal discovered afterwards would
            // mean creating a GameObject only to destroy it in the same frame.
            HandSlotRefusal refusal = _slots.CanTake(HandLoad.Catch, cradle);

            ICarriable tucked = null;
            if (refusal == HandSlotRefusal.HandsFull)
            {
                // Both hands committed. Tuck a TOOL under her arm — it stays parented to the fisher and
                // stays HERS, it simply stops being posed at a wrist. Deliberately not TryPlace(): setting
                // the shovel on the sand every time a clam comes up would be a different, worse game.
                tucked = _slots.FirstOf(HandLoad.Tool);
                if (tucked == null) return false;
                Sling(tucked);
                refusal = _slots.CanTake(HandLoad.Catch, cradle);
            }

            if (refusal != HandSlotRefusal.None)
            {
                if (tucked != null) TakeBackSlung();          // put it back exactly as it was
                return false;
            }

            CarriableCatch catchObject = CarriableCatch.Create(item, transform);
            CarryHandSide preferred = PreferredHand(catchObject);
            if (_slots.TryTake(catchObject, cradle, preferred, out CarryHandSide side) != HandSlotRefusal.None)
            {
                // Unreachable: CanTake said yes on the same state a line ago. Belt-and-braces, because the
                // alternative to a cheap guard is a catch object orphaned on the fisher forever.
                DestroyCatch(catchObject);
                if (tucked != null) TakeBackSlung();
                return false;
            }

            RememberParent(side, null);
            _lastTaken = catchObject;
            ApplyCarriedPose();
            EventBus.Publish(new CatchLanded(item));
            return true;
        }

        /// <summary>
        /// Hand the held catch to a container. Returns false when there is no catch to give or the
        /// container would not take it, and NOTHING changes in that case — the catch stays in her hands
        /// rather than evaporating between the two.
        ///
        /// <para>The item is moved, never copied and never re-stamped: the same <see cref="CatchItem"/>
        /// that landed is the one that stacks, freshness clock and all.</para>
        /// </summary>
        public bool TryGiveCatchTo(IHold hold)
        {
            if (hold == null) return false;
            if (_slots.FirstOf(HandLoad.Catch) is not CarriableCatch held || !held.HasItem) return false;
            if (hold.UsedUnits >= hold.CapacityUnits) return false;

            CatchItem item = held.Item;
            if (!hold.TryAdd(item)) return false;

            // ⚠️ THIS is where FishCaught belongs on the in-hand path, and it is easy to leave out — the
            // catch source no longer publishes it (it did not put anything in a hold), so if this line is
            // missing, nothing does. The event means "a catch entered a hold": the hold-fill renderers
            // re-read the container on it, the onboarding director counts clams with it, the deck
            // presenters re-stack. Without it a clam dug the new way would be in the pail and invisible to
            // every one of them.
            EventBus.Publish(new FishCaught(item));

            held.Take();
            Forget(held);
            DestroyCatch(held);

            TakeBackSlung();
            return true;
        }

        /// <summary>
        /// Put the slung tool back in her hands, if a hand is free. Idempotent and silent when there is
        /// nothing slung — this is called on every path that empties a hand, so "nothing to do" is the
        /// ordinary case.
        /// </summary>
        public void TakeBackSlung()
        {
            ICarriable slung = Slung;
            if (slung == null) return;
            if (_slots.CanTake(HandSlots.LoadOf(slung), needsBothHands: false) != HandSlotRefusal.None) return;

            _slung = null;
            if (slung.Transform != null) slung.Transform.gameObject.SetActive(true);
            if (_slots.TryTake(slung, false, PreferredHand(slung), out CarryHandSide side)
                == HandSlotRefusal.None)
            {
                RememberParent(side, _slungParent);
                _slungParent = null;
                _lastTaken = slung;
                ApplyCarriedPose();
            }
            else
            {
                Sling(slung);   // unreachable (CanTake agreed a line ago); never leave it in limbo
            }
        }

        /// <summary>The heading the BODY is drawn at right now (compass degrees, 0 = N, CW) — the one
        /// quantity a carried object's facing is derived from. Prefers the iso skin's drawn heading (the
        /// picture on screen); falls back to the four-way walk facing, which is what draws the fisher when
        /// the iso art is missing. Neither present = 0 (north), the null-safe default.</summary>
        public float DrawnHeadingDegrees
        {
            get
            {
                Resolve();
                if (_character != null) return _character.HeadingDegrees;
                if (_walk != null)
                    return CarryMath.HeadingFor(PlayerWalkController.FacingUnitVector(_walk.CurrentFacing));
                return 0f;
            }
        }

        /// <summary>
        /// Lift a container into a hand. Returns <see cref="CarryRefusal.None"/> on success; any other
        /// value means nothing changed and the caller should say so.
        ///
        /// <para>The GameObject is re-parented, never rebuilt — which is exactly why the fill survives a
        /// carry with no code to carry it: the <see cref="FuelLevelPresenter"/> that holds the level is the
        /// same component on the same object throughout. Its previous parent is remembered so
        /// <see cref="TryPlace(ICarriable)"/> can put it back in the region it came from.</para>
        ///
        /// <para>Which hand: the one the RIG prefers for this prop at the heading she is drawn at, when it
        /// is free; the other hand otherwise. Nothing already held is moved to make room.</para>
        /// </summary>
        public CarryRefusal TryPickUp(ICarriable container)
        {
            // The Unity-aware leg of the check is the one that matters: `container == null` on an
            // interface reference misses a destroyed component entirely (see the Carried remarks), and a
            // destroyed one has no transform to re-parent.
            if (container == null || container.Transform == null) return CarryRefusal.NotCarriable;

            // ⚠️ The SLOT verdict is asked first, and the order is the one CarryMath.CanPickUp has always
            // kept: standing over a bulk tank with your hands full, "Your hands are full." is the useful
            // sentence — it names the thing you can do something about. Telling you the ten-thousand-litre
            // tank is too heavy is true, unhelpful, and you knew.
            HandSlotRefusal verdict = _slots.CanTake(HandSlots.LoadOf(container), needsBothHands: false);
            if (verdict != HandSlotRefusal.None) return CarryMath.From(verdict);
            if (!container.IsCarriable) return CarryRefusal.NotCarriable;

            HandSlotRefusal refusal =
                _slots.TryTake(container, needsBothHands: false, PreferredHand(container),
                               out CarryHandSide side);
            if (refusal != HandSlotRefusal.None) return CarryMath.From(refusal);

            RememberParent(side, container.Transform.parent);
            _lastTaken = container;
            container.OnLifted(this);

            container.Transform.SetParent(transform, worldPositionStays: false);
            ApplyCarriedPose();
            return CarryRefusal.None;
        }

        /// <summary>Set down the thing she took up most recently. The no-argument form every press path
        /// used while there was one slot; a candidate that knows WHICH object it is should call
        /// <see cref="TryPlace(ICarriable)"/> so the off hand can be emptied too.</summary>
        public CarryRefusal TryPlace() => TryPlace(Carried);

        /// <summary>
        /// Set one carried thing down at the fisher's feet. Returns <see cref="CarryRefusal.None"/> on
        /// success; the other hand is untouched either way.
        ///
        /// <para>Valid ground is <see cref="TidalWalkability.IsWalkableNow"/> at the fisher's own position —
        /// the same read the walker gates on, so "somewhere I can stand" and "somewhere I can put this
        /// down" can never disagree. It fails open in a region with no tide gate, which is the right way
        /// round: a can that cannot be put down because a service is not installed is a soft-lock.</para>
        /// </summary>
        public CarryRefusal TryPlace(ICarriable thing)
        {
            Vector2 feet = transform.position;
            CarryHandSide side = _slots.SideOf(thing);
            CarryRefusal refusal = CarryMath.CanPlace(side != CarryHandSide.None,
                                                      TidalWalkability.IsWalkableNow(feet));
            if (refusal != CarryRefusal.None) return refusal;

            Transform remembered = ParentFor(side);
            Forget(thing);

            // Back to the region it came from when that parent is still alive AND still on screen; the
            // scene root otherwise (the region was unloaded or toggled out under it, which is not a
            // reason to refuse the press).
            //
            // ⚠️ The activeInHierarchy leg is not belt-and-braces — it is a real defect the moment
            // anything carriable is standing in a REGION rather than spawned by a dev menu. A region hop
            // does not unload the region you left; it SetActive(false)s its roots. So carrying a tool
            // from St Peters to Nine Mile Creek and setting it down would re-parent it under St Peters'
            // sleeping root: the object survives, inactive — invisible, unregistered from the interact
            // registry, and unrecoverable without going back and re-activating a scene. Dropping to the
            // scene root instead leaves it where the fisher actually stood, which is what she just did.
            Transform parent = remembered != null && remembered.gameObject.activeInHierarchy
                               ? remembered
                               : null;
            thing.Transform.SetParent(parent, worldPositionStays: false);
            thing.Transform.position = feet;

            thing.OnPlaced();
            TakeBackSlung();
            return CarryRefusal.None;
        }

        private void LateUpdate()
        {
            if (IsCarrying) ApplyCarriedPose();
        }

        /// <summary>
        /// State every carried object's pose from the body, every frame: where it hangs, which baked facing
        /// it shows, and which order it draws at. <b>Stated, never accumulated</b> — the deck rider's rule,
        /// for the deck rider's reason. Public so a test can settle the pose without waiting a frame.
        ///
        /// <para><b>The hand a thing is posed from depends on how many she is holding, and that is
        /// deliberate.</b> Alone, a prop is posed from the rig's own RESOLVED row for the facing she is
        /// drawn at — so a fish still moves to the near hand on the away headings, which is the shipped
        /// look and is measured art rather than a rule. Sharing the pair, each prop is posed from the row
        /// for the hand it was actually GIVEN: two things both following a per-facing preference would
        /// meet in the same hand at the headings where they agree.</para>
        /// </summary>
        public void ApplyCarriedPose()
        {
            _slots.ForgetDestroyed();
            if (_slots.IsEmpty) return;
            Resolve();

            if (_slots.SpansBoth)
            {
                // A cradle is one object across both wrists, and the row that describes it is the rig's
                // own (whose Hand is Both, or — on the shipped table, which bakes no cradle row — one
                // hand). Asking for a named hand here would be asking the table a question about a pose
                // it does not describe.
                Pose(_slots.Right, CarryHandSide.None);
                return;
            }

            bool sharing = _slots.UsedHands >= 2;
            ICarriable right = _slots.Right, left = _slots.Left;
            if (right != null) Pose(right, sharing ? CarryHandSide.Right : CarryHandSide.None);
            if (left != null) Pose(left, sharing ? CarryHandSide.Left : CarryHandSide.None);
        }

        /// <summary>
        /// One carried thing, posed. <paramref name="hand"/> is <see cref="CarryHandSide.None"/> for "no
        /// particular hand — use the rig's resolved row", which is what a thing carried alone gets.
        ///
        /// <para><b>⚠️ The two paths must not be merged into one "compute an offset then apply it".</b>
        /// They disagree about the DRAW ORDER at north, and the table is the one that is right: deriving
        /// the order from the heading puts a small held item behind the body at N, where the wrist is
        /// 0.215 m out and the torso only ~0.115 m half-wide, so the item is really clear of the
        /// silhouette. That is the bug that made a held clam vanish, and it is fixed by taking the rig's
        /// answer rather than by tuning the epsilon in <see cref="CarryMath.AheadOrdersFor"/>.</para>
        /// </summary>
        private void Pose(ICarriable carried, CarryHandSide hand)
        {
            if (carried == null || carried.Transform == null) return;

            if (TryAnchorRow(carried, hand, out CarryAnchorRow row, out int facingRow))
            {
                carried.Transform.localPosition =
                    _carryAnchors.PinMeters(row, _character.Gait, facingRow, _character.Frame);
                carried.ShowFacing(row.ItemFacing);
                RideBodyBand(carried, row.Behind ? -OrdersFor(hand) : OrdersFor(hand));
                return;
            }

            carried.Transform.localPosition = FallbackOffset(hand);

            float heading = DrawnHeadingDegrees;
            carried.ShowFacing(CarryMath.BakedFacingIndex(heading, carried.BakedFacings));
            RideBodyBand(carried, CarryMath.AheadOrdersFor(heading, OrdersFor(hand)));
        }

        /// <summary>
        /// The fallback pose for a thing with no hand-prop row. The serialized offset is her RIGHT side;
        /// the left slot takes its mirror.
        ///
        /// <para><b>Mirroring is legal HERE and nowhere else in this feature.</b> This number is a greybox
        /// constant someone typed, not a measurement — so reflecting it costs nothing true. A measured
        /// anchor is the opposite case and must never be mirrored: see
        /// <see cref="CarryAnchorTableDef.TryRow(string,int,CarryHandSide,out CarryAnchorRow,out bool)"/>
        /// for why one hand's projected grip is not the other's reflection.</para>
        /// </summary>
        private Vector2 FallbackOffset(CarryHandSide hand)
            => hand == CarryHandSide.Left
                ? new Vector2(-_hipOffsetMeters.x, _hipOffsetMeters.y)
                : _hipOffsetMeters;

        /// <summary>How far out of the body's sorting band this hand's item draws. The left hand takes one
        /// extra step so two things held at once cannot land on the SAME order, where they would sort by
        /// draw order — a flicker with no owner. Identical to the old behaviour whenever one thing is
        /// held (<see cref="CarryHandSide.None"/>).</summary>
        private int OrdersFor(CarryHandSide hand)
            => hand == CarryHandSide.Left ? _aheadOrders + 1 : _aheadOrders;

        /// <summary>
        /// The hand-prop row for one carried thing, and the facing row it applies at. False means "pose it
        /// the old way", and every false here is an ordinary state rather than a fault.
        ///
        /// <para><b>Why the iso skin is required and not merely preferred.</b> The rows are indexed by the
        /// CHARACTER kit's facing rows and describe the wrists in that kit's pictures. A fisher drawn by
        /// the four-way fallback sheets is not those pictures, and deriving a row index from the heading
        /// instead would mean restating the character kit's bake convention inside a helper that carries
        /// the FUEL kit's (<see cref="CarryMath.FuelKitFacingsAreCounterClockwise"/>) — two art lineages
        /// that agree today by coincidence and are documented as free to disagree. So the facing row is
        /// read off the picture that is actually up (<see cref="IsoCharacterSprite.FacingRow"/>), which is
        /// the same rule the deck rider was rebuilt around, and no skin means no row.</para>
        ///
        /// <para><b>⚠️ And why a SUSPENDED skin is refused too, which is not a corner case.</b> While
        /// another driver has claimed the renderer (<see cref="IsoCharacterSprite.Suspend"/>) the body is
        /// being drawn from a different sheet entirely — <c>CharacterClipPlayer</c>'s board / boardDown /
        /// haul / ladderDown, or the rod-fight animator's cast and land — and this skin's facing row and
        /// frame are frozen at whatever they last were. The table only carries the FREE body's idle, walk
        /// and run wrists, so posing from it through a clip would hang the prop off a wrist the fisher is
        /// not drawing. That is reachable and visible: boarding while carrying is a supported move, and
        /// the <c>board</c> clip's arms travel 19 px over its ten frames.</para>
        ///
        /// <para>The facing-count check is the other half of the same rule: a skin baked at four rows
        /// indexing an eight-row table would pose from a heading the body is not facing, silently and only
        /// at some headings — the worst shape of wrong. Nothing ships at four today; the check costs an
        /// int compare and closes it before it can happen.</para>
        /// </summary>
        private bool TryAnchorRow(ICarriable carried, CarryHandSide hand, out CarryAnchorRow row,
                                  out int facingRow)
        {
            row = default;
            facingRow = 0;

            if (!AnchorsUsable()) return false;
            if (carried is not ICarryAnchored anchored) return false;

            facingRow = _character.FacingRow;
            return _carryAnchors.TryRow(anchored.HandPropKey, facingRow, hand, out row, out _);
        }

        /// <summary>Is the hand-prop table readable for the body as it is drawn RIGHT NOW? Shared by the
        /// pose path and by <see cref="PreferredHand"/> so the two can never disagree about whether the
        /// rig has an answer.</summary>
        private bool AnchorsUsable()
        {
            Resolve();
            if (_carryAnchors == null || _character == null) return false;
            if (!_character.HasArt || _character.IsSuspended) return false;

            CharacterVisualDef visual = _character.Visual;
            return visual != null && visual.FacingCount == _carryAnchors.FacingCount;
        }

        /// <summary>
        /// Which hand the RIG prefers for this thing at the heading she is drawn at — the input
        /// <see cref="HandSlots.TryTake"/> honours when that hand is free.
        ///
        /// <para><see cref="CarryHandSide.Right"/> when the rig has nothing to say (no table, no iso skin,
        /// no row for this prop): it is the rig's own default for every one-handed prop but the rope, and
        /// a preference is only ever a preference — an occupied right hand sends the thing left
        /// anyway.</para>
        /// </summary>
        private CarryHandSide PreferredHand(ICarriable thing)
        {
            if (thing is not ICarryAnchored anchored || !AnchorsUsable()) return CarryHandSide.Right;
            return _carryAnchors.TryRow(anchored.HandPropKey, _character.FacingRow, out CarryAnchorRow row)
                   ? row.Hand
                   : CarryHandSide.Right;
        }

        /// <summary>Put the carried sprite in the body's own live sorting band, this many orders ahead of
        /// it (negative = behind). No order is authored anywhere — the body's Y-sort output this frame is
        /// the datum, ADR 0032.</summary>
        private void RideBodyBand(ICarriable carried, int aheadOrders)
        {
            if (_bodyRenderer == null) return;
            carried.RideSortingBand(_bodyRenderer.sortingLayerID,
                                    _bodyRenderer.sortingOrder + aheadOrders);
        }

        /// <summary>Wire the hand-prop table (the start builder / tests) — the same <c>Configure</c> seam
        /// <see cref="IsoCharacterSprite.Configure"/> offers, and needed for the same reason: EditMode
        /// never runs a builder, so a test states it.</summary>
        public void ConfigureCarryAnchors(CarryAnchorTableDef table) => _carryAnchors = table;

        /// <summary>The hand-prop table currently wired, or null. For tests / tooling.</summary>
        public CarryAnchorTableDef CarryAnchors => _carryAnchors;

        // ---- bookkeeping ------------------------------------------------------------------------------

        /// <summary>
        /// Tuck a tool under her arm: it stays parented to the fisher and stays HERS, it simply stops
        /// being posed at a wrist and stops occupying a hand.
        ///
        /// <para>⚠️ Its remembered PARENT travels with it. The hand it vacates is about to be filled by
        /// the catch, whose own <see cref="RememberParent"/> would overwrite the slot — and then the
        /// shovel, once taken back and set down, would land at the scene root instead of in the region it
        /// came from. Invisible until someone carries a tool across a region hop and puts it down.</para>
        /// </summary>
        private void Sling(ICarriable tool)
        {
            _slungParent = ParentFor(_slots.SideOf(tool));
            _slots.Release(tool);
            _slung = tool;
            if (tool.Transform != null) tool.Transform.gameObject.SetActive(false);
        }

        // Where the slung tool hung before it was ever lifted — see Sling.
        private Transform _slungParent;

        /// <summary>Give up a hand and the parent it was remembering, and keep <see cref="Carried"/>
        /// pointing at something that is still held.</summary>
        private void Forget(ICarriable thing)
        {
            CarryHandSide side = _slots.SideOf(thing);
            if (side != CarryHandSide.Right) _placedParents[0] = null;
            if (side != CarryHandSide.Left) _placedParents[1] = null;
            _slots.Release(thing);
            if (ReferenceEquals(_lastTaken, thing)) _lastTaken = null;
        }

        private void RememberParent(CarryHandSide side, Transform parent)
        {
            if (side != CarryHandSide.Right) _placedParents[0] = parent;
            if (side != CarryHandSide.Left) _placedParents[1] = parent;
        }

        private Transform ParentFor(CarryHandSide side)
            => side == CarryHandSide.Left ? _placedParents[0] : _placedParents[1];

        /// <summary>
        /// ⚠️ <c>Destroy()</c> THROWS in edit mode ("Destroy may not be called from edit mode"), and the
        /// give-to-a-hold path is reached by every EditMode test of the stack. <c>Object.Destroy</c> is
        /// also DEFERRED to end of frame in play mode, which would leave a taken-from catch parented to
        /// the fisher for the rest of the frame — harmless, but the immediate form is the honest one here
        /// since the object has already given up its item.
        /// </summary>
        private static void DestroyCatch(CarriableCatch catchObject)
        {
            if (catchObject == null) return;
            if (Application.isPlaying) Destroy(catchObject.gameObject);
            else DestroyImmediate(catchObject.gameObject);
        }

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            if (_bodyRenderer == null) _bodyRenderer = GetComponent<SpriteRenderer>();
            if (_character == null) _character = GetComponent<IsoCharacterSprite>();
            if (_walk == null) _walk = GetComponent<PlayerWalkController>();
        }
    }
}
