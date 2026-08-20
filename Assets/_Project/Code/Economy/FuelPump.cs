using UnityEngine;
using UnityEngine.Events;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>One hose, at one site, dispensing one grade</b> — the verb half of fuel retail. Walk up holding a
    /// can, press interact, and it fills and charges you at the price its <see cref="FuelStationDef"/>
    /// posts.
    ///
    /// <para><b>Why an interact candidate and not a buy-screen vendor.</b> Every other thing this module
    /// sells is a ROW: a rod, a licence, a bag of ice — you open a counter and pick from a list, which is
    /// what <c>BuyCatalog</c>/<c>BuyScreen</c> are for. Fuel is not like that. It is a thing you do to an
    /// object you are already holding, at a place, and the diegetic-UI direction is explicit that the level
    /// reads off the can rather than off a screen. So this registers on the one interact seam
    /// (<see cref="IInteractable"/>) beside the pail and the clam hole, and no screen opens.</para>
    ///
    /// <para><b>It never touches the can's lane.</b> What is in the player's hands is asked of Core
    /// (<see cref="GameServices.Hands"/>), and what that thing holds is asked through
    /// <see cref="IFuelVessel"/> — so the economy prices fuel without ever naming <c>CarriableFuelContainer</c>
    /// or <c>FuelContainerDef</c>, which live in lanes this module does not reference (rule 4). Anything
    /// that implements the interface can be filled here, which is how a boat's tank will arrive: it
    /// implements <see cref="IFuelVessel"/> and this class does not change.</para>
    ///
    /// <para><b>⚠️ What is NOT wired.</b> <b>Boat tanks.</b> No hull carries fuel today — there is no tank
    /// field on <c>BoatHullDef</c>, none in <c>SaveData</c>, and no burn model — so "refuel the boat you are
    /// aboard" has nothing to write to and is deliberately absent rather than faked. When the tank lands it
    /// implements <see cref="IFuelVessel"/>, this component adds <see cref="InteractContext.Aboard"/>, and
    /// the pricing below is unchanged. <b>Persistence:</b> a can's level is session-local, exactly as its
    /// position is (see <c>CarryHands</c>) — buying fuel does not survive a save, because there is nowhere
    /// to put it yet. The money you spent DOES persist, so a pump in a live scene is a leak until the fuel
    /// state does; that is why nothing places one yet.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hidden Harbours/Fuel Pump")]
    public sealed class FuelPump : MonoBehaviour, IInteractable
    {
        [Header("What this hose sells")]
        [Tooltip("The SITE whose prices this hose posts. Every pump at one place points at one asset, so " +
                 "two hoses ten feet apart can never post different numbers for the same fuel.")]
        [SerializeField] private FuelStationDef _station;

        [Tooltip("Which grade THIS hose dispenses — one of FuelGrades (gas · diesel · mixed · oil · " +
                 "stove_oil). A twin pump is two of these components, one per grade.")]
        [SerializeField] private string _grade = FuelGrades.Gas;

        [Tooltip("A GameObject carrying an IWallet (the player's PlayerWallet / persistent proxy).")]
        [SerializeField] private GameObject _walletProvider;

        [Header("The press")]
        [Tooltip("Stable interact id, e.g. \"fixture.nmc.wharf_pump_gas\". ⚠️ Must be unique among live " +
                 "registrants — two pumps sharing an id make the resolver's order non-total. Left empty, " +
                 "one is derived from the station, the grade and the instance.")]
        [SerializeField] private string _id;

        [Tooltip("How close (m) the fisher must be. A pump is something you stand AT, so this is a " +
                 "step-up range rather than a reach.")]
        [SerializeField, Min(0f)] private float _reachMeters = 2f;

        [Tooltip("Require the fisher to be FACING it. Off, matching every other fixture on this seam.")]
        [SerializeField] private bool _requiresFacing;

        [Tooltip("Inspector hook fired with the price paid on a successful fill (UI can bind this later).")]
        [SerializeField] private UnityEvent<int> _onFilled;

        private IWallet _wallet;
        private string _resolvedId;
        private string _verbLabel;
        private string _labelledGrade;

        /// <summary>The site whose prices this hose posts. Null until wired.</summary>
        public FuelStationDef Station => _station;

        /// <summary>Which grade this hose dispenses.</summary>
        public string Grade => _grade;

        /// <summary>This site's offer for this hose's grade — availability and price in one value. A pump
        /// with no station answers "not available", which <see cref="Quote"/> turns into
        /// <see cref="FuelRefusal.NoStation"/> so the authoring error reads differently from a grade the
        /// site genuinely does not stock.</summary>
        public FuelGradeOffer Offer =>
            _station != null ? _station.Offer(_grade)
                             : new FuelGradeOffer { Grade = _grade, Available = false, PricePerLitre = 0f };

        /// <summary>True iff the most recent <see cref="TryFill()"/> went through.</summary>
        public bool LastFillSucceeded { get; private set; }

        /// <summary>Litres delivered by the most recent successful <see cref="TryFill()"/>. 0 after a
        /// refusal.</summary>
        public float LastLitresDelivered { get; private set; }

        private void Awake()
        {
            if (_walletProvider != null) _wallet = _walletProvider.GetComponent<IWallet>();
            if (_wallet == null)
                Debug.LogWarning("[FuelPump] No IWallet found on the wallet provider.", this);
        }

        // ---- wiring (for builders and tests) -------------------------------------------------------

        /// <summary>
        /// Point this hose at a site and a grade, and give it its interact id — for a builder or a test,
        /// which are the only things that know what the pump beside it is called.
        ///
        /// <para>Safe to call after registration: <see cref="Id"/> is re-derived rather than left stale.
        /// A null or empty <paramref name="id"/> falls back to the derived form.</para>
        /// </summary>
        public void Configure(FuelStationDef station, string grade, string id = null)
        {
            _station = station;
            _grade = grade;
            _id = id;
            _resolvedId = null;
            _labelledGrade = null;
        }

        /// <summary>Hand this pump the wallet it charges — the seam a builder or a test wires when there is
        /// no <c>_walletProvider</c> GameObject to find one on.</summary>
        public void SetWallet(IWallet wallet) => _wallet = wallet;

        // ---- the interact seam ----------------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _resolvedId ??= ResolveId();

        /// <summary>"Fill up with gas" — the grade is the whole of what the player needs to know, and it is
        /// the warning as well as the offer (standing at the wrong hose is a mistake worth catching before
        /// the press, not after).
        ///
        /// <para><b>⚠ Cached, because this is read on the OFFER path, not on the press.</b> The popup asks
        /// for a label every frame it has a candidate, so an interpolated string built here would be one
        /// allocation per frame for as long as the player stands at the pump — the per-frame garbage rule 7
        /// rules out. Built once, and rebuilt only if the grade changes under it (a
        /// <see cref="Configure"/> after Awake).</para></summary>
        public string VerbLabel
        {
            get
            {
                if (!string.Equals(_grade, _labelledGrade, System.StringComparison.Ordinal))
                {
                    _labelledGrade = _grade;
                    _verbLabel = "Fill up with " + FuelGrades.Display(_grade);
                }
                return _verbLabel;
            }
        }

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _reachMeters;

        /// <summary>
        /// A fixture normally — and a <see cref="InteractPriority.ToolTarget"/> while you are holding
        /// something that holds fuel.
        ///
        /// <para><b>Why it must move, and this is not a special case.</b> A carried can reports
        /// <see cref="InteractPriority.Held"/> (20). A pump at the plain <see cref="InteractPriority.Fixture"/>
        /// rung (0) therefore loses every press to "set the can down" — so standing at the pump with the can
        /// in your hands, filling it would be IMPOSSIBLE. That is precisely the inversion
        /// <see cref="InteractPriority.ToolTarget"/> was added to fix for the shovel and the clam hole, and
        /// a pump with a can is the same shape: "use the thing you are holding on the thing in front of you"
        /// is more specific than "put the thing you are holding down". Setting the can down near a pump is
        /// still available the honest way — step back a pace, out of reach.</para>
        ///
        /// <para><b>The rung's obligation is met.</b> <see cref="InteractPriority.ToolTarget"/> requires that
        /// a candidate claim it only while the tool is actually held, or it would outrank held things
        /// forever and nothing could ever be put down nearby. This claims it only when
        /// <see cref="HeldVessel"/> finds one.</para>
        ///
        /// <para>Deliberately claimed for ANY fuel vessel, not only one this hose can fill: holding a diesel
        /// can at the gas pump, the useful outcome is being TOLD that, which requires winning the press.</para>
        /// </summary>
        public int Priority => HeldVessel() != null ? InteractPriority.ToolTarget : InteractPriority.Fixture;

        /// <summary>
        /// On your own two feet, and now <b>on a deck</b> too — you lie alongside and fill her without
        /// getting off (<c>fuel-and-refuelling.md</c> §9.4).
        ///
        /// <para><b>⚠ The bit is <see cref="InteractContext.OnDeck"/>, not the "Aboard" this file and
        /// §9.12 both name.</b> There is no <c>InteractContext.Aboard</c>: the flags are OnFoot / OnDeck /
        /// AtHelm / Driving, and <c>ControlMode.Aboard</c> maps to <see cref="InteractContext.AtHelm"/> —
        /// which is unreachable by construction, because a press at the helm means "step back onto the
        /// deck" and the switcher answers it before any verb is consulted. Offering AtHelm would be
        /// offering a press that can never happen. OnDeck is both reachable and the honest place to be
        /// standing while a hose goes into your filler.</para>
        /// </summary>
        public InteractContext Contexts => InteractContext.OnFoot | InteractContext.OnDeck;

        /// <inheritdoc/>
        public bool RequiresFacing => _requiresFacing;

        /// <summary>Always a thing to act on. A pump whose grade is out of stock is still AVAILABLE — press
        /// it and it says so, which is the cozy refusal (P5) and the reading <see cref="IInteractable"/>
        /// asks for.</summary>
        public bool IsAvailable => true;

        private void OnEnable() => Interactables.Register(this);

        private void OnDisable() => Interactables.Unregister(this);

        /// <inheritdoc/>
        public void Interact(in InteractActor actor) => TryFill();

        // ---- the purchase ---------------------------------------------------------------------------

        /// <summary>
        /// What a fill would cost right now, given what is in the player's hands and what is in their
        /// purse. Pure read — nothing is charged and nothing is poured. This is what a future UI shows
        /// before the player commits, and what <see cref="TryFill()"/> acts on, so the two can never
        /// disagree.
        /// </summary>
        public FuelQuote Quote(IFuelVessel vessel, int money, float maxLitres = 0f)
        {
            if (_station == null) return FuelQuote.No(FuelRefusal.NoStation);
            return FuelPricing.Quote(Offer, vessel, money, maxLitres);
        }

        /// <summary>The no-arg interaction entrypoint (the press / tests): fill whatever the fisher is
        /// carrying, from the wired wallet, to the brim.</summary>
        public bool TryFill() => TryFill(TargetVessel(), _wallet);

        /// <summary>
        /// Core fill seam (testable): quote it, spend, pour, announce.
        ///
        /// <para>Money moves before fuel does and only on success — <see cref="IWallet.TrySpend"/> is
        /// atomic, and the quote already proved the price is affordable, so the spend cannot fail here.
        /// It is still checked: a wallet that refuses anyway must not hand out free fuel.</para>
        ///
        /// <para>Every refusal publishes a <see cref="DevNotice"/> and charges nothing — the cozy refusal
        /// (P5), and the same shape <c>CarriableFuelContainer</c> uses for its own.</para>
        /// </summary>
        public bool TryFill(IFuelVessel vessel, IWallet wallet, float maxLitres = 0f)
        {
            LastFillSucceeded = false;
            LastLitresDelivered = 0f;

            if (wallet == null)
            {
                Debug.LogWarning("[FuelPump] No wallet to charge — refusing rather than pour for free.", this);
                return false;
            }

            FuelQuote quote = Quote(vessel, wallet.Money, maxLitres);
            if (!quote.CanBuy)
            {
                EventBus.Publish(new DevNotice(FuelPricing.Message(quote.Refusal, _grade)));
                return false;
            }

            if (quote.Price > 0 && !wallet.TrySpend(quote.Price))
            {
                EventBus.Publish(new DevNotice(FuelPricing.Message(FuelRefusal.CannotAfford, _grade)));
                return false;
            }

            vessel.Deliver(quote.Litres);

            LastFillSucceeded = true;
            LastLitresDelivered = quote.Litres;

            string stationId = _station != null ? _station.Id : "";
            EventBus.Publish(new FuelPurchased(stationId, _grade, quote.Litres, quote.Price));
            EventBus.Publish(new DevNotice(FillNotice(quote)));

            Debug.Log($"[FuelPump] {quote.Litres:0.0} L of {_grade} at {stationId} for {quote.Price}.");
            _onFilled?.Invoke(quote.Price);
            return true;
        }

        // ---- helpers ---------------------------------------------------------------------------------

        /// <summary>
        /// The fuel vessel in the fisher's hands, or null.
        ///
        /// <para>Asked of Core's <see cref="GameServices.Hands"/> relay, then of the held object's own
        /// transform — the carried thing is an <see cref="ICarriable"/>, and whether it also holds fuel is
        /// a separate question its GameObject answers.</para>
        ///
        /// <para><b>⚠ Memoized on WHICH object is held, because <see cref="Priority"/> calls this on the
        /// resolve path.</b> The resolver reads every candidate's rung whenever the offer is recomputed, so
        /// an unguarded <c>GetComponent</c> here would run per pump per frame for as long as the player
        /// stands on the wharf — the kind of per-frame work rule 7 rules out. The answer can only change
        /// when the held OBJECT changes, so that is what the cache is keyed on;
        /// <c>ReferenceEquals</c> because identity is the question, and both a null hand and a destroyed
        /// can (which the relay launders to null) invalidate it correctly.</para>
        /// </summary>
        private IFuelVessel HeldVessel()
        {
            ICarriable held = GameServices.Hands?.Carried;
            if (ReferenceEquals(held, _heldCache)) return _vesselCache;

            _heldCache = held;
            Transform t = held?.Transform;
            _vesselCache = t != null ? t.GetComponent<IFuelVessel>() : null;
            return _vesselCache;
        }

        /// <summary>
        /// <b>What this press is FOR</b> — the can in your hands if there is one, otherwise the tank of
        /// the boat you are standing on.
        ///
        /// <para><b>Hands win, and that ordering is the whole of it.</b> Alongside in your own boat with
        /// a can in your hands, the thing you meant was the can — you are standing at a hose holding an
        /// empty container. Empty-handed on the same deck, the only thing you can have meant is her tank.
        /// Neither reading needs a mode switch or a second button.</para>
        ///
        /// <para><b>⚠ §9.12 says this component should be "unchanged except for adding the deck
        /// context". It needed one line more than that, and the reason is worth recording:</b> a pump can
        /// only fill what it can NAME, and "the boat you are on" is not something the Economy module is
        /// allowed to look up — <c>BoatFuelTank</c> lives in Boats, which this module does not reference
        /// and must not (rule 4). So the tank publishes itself into Core
        /// (<see cref="GameServices.ActiveBoatFuel"/>) exactly as the heading probe and the helm relay
        /// already do, and this reads the slot. The pump still names no boat type and the pricing below
        /// is untouched, which is what the criterion was actually protecting.</para>
        /// </summary>
        private IFuelVessel TargetVessel()
        {
            IFuelVessel held = HeldVessel();
            if (held != null) return held;

            // ⚠ NOT `HeldVessel() ?? GameServices.ActiveBoatFuel`. The slot holds a MonoBehaviour behind
            // an interface, and `??` is a REFERENCE null check — it does not run Unity's overloaded
            // operator, so a destroyed tank would come back "not null" and then throw on first touch.
            // The slot is identity-cleared on disable, so this only bites on a torn-down boat that never
            // got its OnDisable; launder it explicitly rather than rely on that.
            IFuelVessel tank = GameServices.ActiveBoatFuel;
            return tank is Object o && o == null ? null : tank;
        }

        // The memo behind HeldVessel(). Never read anywhere else.
        private ICarriable _heldCache;
        private IFuelVessel _vesselCache;

        // What just happened, in words. Loc-seam literals (HudStrings convention). The money-limited case
        // gets its own sentence: a can that came away half full because the purse ran out must SAY so, or
        // the player reads it as a bug.
        private string FillNotice(in FuelQuote quote)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string litres = quote.Litres.ToString("0.0", ci);
            string fuel = FuelGrades.Display(_grade);

            if (quote.Price <= 0)
                return litres + " L of " + fuel + ", on the house.";
            if (quote.LimitedByMoney)
                return litres + " L of " + fuel + " - all your money would buy.";
            return litres + " L of " + fuel + " for " + quote.Price.ToString(ci) + ".";
        }

        private string ResolveId()
        {
            if (!string.IsNullOrEmpty(_id)) return _id;
            string site = _station != null && !string.IsNullOrEmpty(_station.Id) ? _station.Id : "station.unknown";
            // Instance-scoped, so two hoses of the same grade at one site cannot collide. Readable ids are
            // the builder's job (Configure); this is the safety net, not the intent. GetEntityId, not the
            // deprecated GetInstanceID — 6000.5 marks that obsolete-as-ERROR, which compiles locally and
            // reds CI.
            return $"fixture.{site}.{_grade}#{GetEntityId()}";
        }
    }
}
