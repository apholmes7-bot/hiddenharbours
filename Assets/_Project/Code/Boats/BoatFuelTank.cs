using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The fuel a hull carries</b> — her tank as an <see cref="IFuelVessel"/>, and the filler cap you
    /// pour a can into. <c>docs/design/fuel-and-refuelling.md</c> §9.2 / §9.4.
    ///
    /// <para><b>This is the far side of the seam the pump promised.</b> <c>FuelPump</c>'s own remarks
    /// have said since fuel retail shipped that when a tank arrived it would implement
    /// <see cref="IFuelVessel"/> and the pricing would be unchanged. It does, and it is: this component
    /// names no economy type, and the pump names no boat type. They meet at the Core interface (rule 4).</para>
    ///
    /// <para><b>The tank is the HULL's, so the numbers are the hull's.</b> Capacity and grade are read
    /// LIVE off <c>BoatHullDef</c> rather than copied, because <c>OwnedFleet</c> swaps the hull under a
    /// boat on a purchase — a cached capacity would leave the dory's 10 L on a Cape Islander. The only
    /// thing this component owns is the LEVEL, which is the one quantity that is genuinely hers and not
    /// derivable from anything (rule 5's exception, and the same class of state as <c>bilgeLevel</c>).</para>
    ///
    /// <para><b>⭐ A hull with <c>FuelCapacityLitres = 0</c> is INERT.</b> That is every hull until the
    /// authoring pass, and it is permanently the right answer for the rowed dory. She reports no grade,
    /// no capacity and no level; the pump refuses her the same way it refuses empty hands; and the pour
    /// verb never offers itself. The playable build must run with zero hulls authored, and this is where
    /// that promise is kept.</para>
    ///
    /// <para><b>⚠ Nothing here BURNS fuel.</b> The level only moves when someone fills or pours. Metering
    /// the engine against the fixed tick, and the running-dry states that follow from it, are F5 —
    /// deliberately separate, so the tank can be reviewed as a container before it becomes a clock.</para>
    ///
    /// <para><b>The level PERSISTS, per hull</b> (§9.3, save v14). Every mutation pushes a snapshot into
    /// the save blob and the <c>GameLoaded</c> edge restores it — the <c>ShipHold</c> pattern exactly
    /// (cheap: one row, event-time only; the DISK write stays <c>SaveService</c>'s autosave). Keyed by
    /// hull id, so selling her and buying her back finds the fuel where you left it.</para>
    ///
    /// <para><b>⭐ A hull with no row is FULL, and that one rule does two jobs.</b> It is how a v13 save
    /// loads (that player's boat had no tank; deciding she is empty would invent a consequence for them),
    /// and it is also how <c>GameConfig.Fuel.NewBoatArrivesFull</c> is honoured — a boat you have just
    /// bought has no row yet, so she arrives fuelled with no separate purchase hook to keep in step. One
    /// mechanism, two requirements, nothing to drift.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hidden Harbours/Boat Fuel Tank")]
    public sealed class BoatFuelTank : MonoBehaviour, IFuelVessel, IInteractable
    {
        [Tooltip("The hull whose tank this is. Left empty, it is read from the BoatController on this " +
                 "GameObject and re-read every time — so a hull swap changes the tank with the boat.")]
        [SerializeField] private BoatHullDef _hull;

        [Tooltip("How close (m) you must be to reach the filler with a can. Roughly a step: you are " +
                 "working the cap, not gesturing at it from the far end of the deck. The FuelPump reach " +
                 "convention; not a fuel number, a feel one.")]
        [SerializeField, Min(0f)] private float _reachMeters = 2f;

        private BoatController _controller;
        private float _litres;
        private bool _primed;
        private string _bankedHullId;

        // The memo behind HeldVessel() — the resolver reads Priority/IsAvailable on every offer
        // recompute, so an unguarded GetComponent here would run per boat per frame (rule 7).
        private ICarriable _heldCache;
        private IFuelVessel _vesselCache;
        private string _verbLabel;
        private string _resolvedId;

        // ---- what she is ------------------------------------------------------------------------

        /// <summary>The hull this tank belongs to — the serialized override, else the controller's live
        /// hull. Never cached: <c>OwnedFleet</c> re-points the controller on a purchase.</summary>
        public BoatHullDef Hull
        {
            get
            {
                if (_hull != null) return _hull;
                if (_controller == null) _controller = GetComponent<BoatController>();
                return _controller != null ? _controller.Hull : null;
            }
        }

        /// <summary>
        /// Point this tank at a hull explicitly (a builder or a test), mirroring the <c>Configure</c>
        /// pattern the rest of the module uses.
        ///
        /// <para><b>Wiring only — it does NOT re-fill her.</b> Re-priming here would make every re-point
        /// a free tank, including the ones that are not purchases at all (a builder wiring the same hull
        /// twice, a test rig). What it does do is change the capacity, and the level clamps to it on the
        /// next read, so a swap DOWN the ladder can never leave a Cape Islander's fuel sloshing in a
        /// dory's tank.</para>
        ///
        /// <para>⏳ <b>A PURCHASE is a different event and belongs to whoever grants the boat</b>
        /// (<c>OwnedFleet</c>): <c>GameConfig.Fuel.NewBoatArrivesFull</c> says a bought boat arrives
        /// fuelled, and the way to honour that is <c>SetLitres(CapacityLitres)</c> on the grant. Left to
        /// F4, with the save, because "what level does a boat you just bought have" and "what level does
        /// a boat in a loaded save have" are the same question and want one answer.</para>
        /// </summary>
        public void Configure(BoatHullDef hull)
        {
            _hull = hull;
            _resolvedId = null;
            _verbLabel = null;
        }

        // ---- IFuelVessel ------------------------------------------------------------------------

        /// <inheritdoc/>
        public string Grade
        {
            get
            {
                BoatHullDef h = Hull;
                // A hull with no tank has no grade to report even if one was authored by mistake —
                // otherwise a pump would happily match her and then deliver into nothing.
                return h != null && h.FuelCapacityLitres > 0f ? (h.FuelGrade ?? "") : "";
            }
        }

        /// <inheritdoc/>
        public float CapacityLitres
        {
            get
            {
                BoatHullDef h = Hull;
                return h != null && h.FuelCapacityLitres > 0f ? h.FuelCapacityLitres : 0f;
            }
        }

        /// <summary>How much is aboard, in litres. Clamped to the CURRENT capacity on every read, so a
        /// hull swap down the ladder cannot leave a big boat's fuel sloshing in a small boat's tank.</summary>
        public float Litres
        {
            get
            {
                Prime();
                float capacity = CapacityLitres;
                if (capacity <= 0f) return 0f;
                if (_litres > capacity) _litres = capacity;
                return _litres > 0f ? _litres : 0f;
            }
        }

        /// <summary>Fraction of the tank aboard, 0..1 — what a gauge draws. 0 for a hull with no tank,
        /// which is also what "empty" reads as, and the difference is <see cref="HasTank"/>.</summary>
        public float Fraction01
        {
            get
            {
                float capacity = CapacityLitres;
                return capacity > 0f ? Mathf.Clamp01(Litres / capacity) : 0f;
            }
        }

        /// <summary>True iff this hull carries fuel at all. The rowed dory does not, and neither does any
        /// hull the owner has not authored yet — see the class remarks.</summary>
        public bool HasTank => CapacityLitres > 0f;

        /// <inheritdoc/>
        public void Deliver(float litres)
        {
            float capacity = CapacityLitres;
            if (litres <= 0f || capacity <= 0f) return;
            Prime();
            _litres = Mathf.Min(capacity, _litres + litres);
            SyncToSave();
        }

        /// <inheritdoc/>
        public float Draw(float litres)
        {
            if (litres <= 0f || CapacityLitres <= 0f) return 0f;
            float before = Litres;
            float given = Mathf.Min(before, litres);
            _litres = before - given;
            SyncToSave();
            return given > 0f ? given : 0f;
        }

        /// <summary>
        /// Set the level outright, clamped — for the save loader and for tests. NOT a gameplay verb: fuel
        /// arrives through <see cref="Deliver"/> and leaves through <see cref="Draw"/> so that every
        /// litre in the world is accounted for. This is the one door that is allowed to skip that, and it
        /// exists because restoring a save is not a transfer from anywhere.
        /// </summary>
        public void SetLitres(float litres)
        {
            _primed = true;
            _bankedHullId = HullId;
            float capacity = CapacityLitres;
            _litres = capacity <= 0f ? 0f : Mathf.Clamp(litres, 0f, capacity);
            SyncToSave();
        }

        /// <summary>
        /// The level a tank starts at when nothing has said otherwise: brim-full, per
        /// <c>GameConfig.Fuel.NewBoatArrivesFull</c>.
        ///
        /// <para><b>Full is the forgiving direction, and it is also the true one</b> (§9.3): a boat you
        /// have just bought arrives fuelled, because that is what a sale does — a shipwright who hands
        /// you a dry boat you cannot move off his wharf is a bug shaped like realism. It is deferred to
        /// first read rather than done in <c>Awake</c> so that a save load, which happens later, does not
        /// have to undo it.</para>
        /// </summary>
        private void Prime()
        {
            if (_primed) return;
            _primed = true;
            _bankedHullId = HullId;
            _litres = LevelFromSave(_bankedHullId, CapacityLitres);
        }

        /// <summary>
        /// The level this hull should start at: her saved row, else brim-full.
        ///
        /// <para><b>⭐ The absent row IS the rule</b>, and it answers two questions at once — what a v13
        /// save's boat has, and what a boat you just bought has
        /// (<c>GameConfig.Fuel.NewBoatArrivesFull</c>). One mechanism, so the two can never disagree.</para>
        ///
        /// <para>Clamped to the CURRENT capacity, because a row is only as good as the Def it was written
        /// against: an owner who re-tunes a tank smaller must not leave a boat carrying more than she can
        /// hold. Untick <c>NewBoatArrivesFull</c> and an un-rowed hull starts dry instead — which is a
        /// harder game, and the owner's to choose.</para>
        /// </summary>
        private static float LevelFromSave(string hullId, float capacity)
        {
            if (capacity <= 0f) return 0f;

            var rows = GameServices.Save?.Current?.HullFuel;
            if (rows != null && !string.IsNullOrEmpty(hullId))
                for (int i = 0; i < rows.Count; i++)
                    if (string.Equals(rows[i].HullId, hullId, System.StringComparison.Ordinal))
                        return Mathf.Clamp(rows[i].Litres, 0f, capacity);

            return GameServices.Fuel.NewBoatArrivesFull ? capacity : 0f;
        }

        /// <summary>This hull's stable id right now, or empty. Not necessarily the id the current level
        /// belongs to — see <c>_bankedHullId</c> in <see cref="SyncToSave"/>.</summary>
        private string HullId
        {
            get { BoatHullDef h = Hull; return h != null ? (h.Id ?? "") : ""; }
        }

        // ---- persistence (§9.3, save v14) ---------------------------------------------------------

        /// <summary>
        /// Bank this hull's level into the save blob — called after every mutation, the
        /// <c>ShipHold.SyncToSave</c> pattern.
        ///
        /// <para><b>The writer rule that keeps the list sparse AND unambiguous</b> (§9.3): write a row for
        /// a hull whose level is NOT full, and REMOVE the row for one that is. So a dry boat always has an
        /// explicit <c>0</c>, a brim-full boat costs nothing, and "no row" never has to mean two things.
        /// Removing rather than leaving a stale full row matters: a hull whose capacity the owner later
        /// raises would otherwise stay pinned at the old number instead of reading as full.</para>
        /// </summary>
        private void SyncToSave()
        {
            var save = GameServices.Save;
            if (save == null || save.Current == null) return;

            // ⚠ THE HULL THE LEVEL BELONGS TO, NOT THE HULL SHE IS WEARING. These differ for exactly one
            // instant and it is the important one: OwnedFleet.ApplyHull calls BoatController.SetHull
            // FIRST, so by the time SwitchToHull banks the old level the controller is already reporting
            // the NEW hull. Writing under HullId there would file the dory's half tank under the Cape
            // Islander's id — the fuel would appear to teleport up the ladder, and the dory's own row
            // would never be written at all. _bankedHullId is set wherever _litres is, and moves with it.
            string hullId = !string.IsNullOrEmpty(_bankedHullId) ? _bankedHullId : HullId;
            if (string.IsNullOrEmpty(hullId)) return;

            var rows = save.Current.HullFuel ??= new System.Collections.Generic.List<HullFuelDto>();
            float capacity = CapacityLitres;
            // A hull with no tank has nothing to record; she is not "full", she simply has no tank, and
            // the row would be a fiction that a later authoring pass would silently inherit.
            bool full = capacity <= 0f || _litres >= capacity - FuelVolumes.MinLitres;

            for (int i = 0; i < rows.Count; i++)
            {
                if (!string.Equals(rows[i].HullId, hullId, System.StringComparison.Ordinal)) continue;
                if (full) rows.RemoveAt(i);
                else rows[i] = new HullFuelDto(hullId, _litres);
                return;
            }

            if (!full) rows.Add(new HullFuelDto(hullId, _litres));
        }

        /// <summary>
        /// Take on a different hull: bank what the OLD one had, then read what the NEW one has.
        ///
        /// <para>Called by <c>OwnedFleet.ApplyHull</c>, which is the one funnel every hull change goes
        /// through — a purchase and a save-restore both. Doing it there rather than by watching the hull
        /// change from inside a property getter keeps save I/O off the read path, and mirrors how the hold
        /// is re-capacitied on the same line.</para>
        ///
        /// <para><b>Banking first is what makes the tank per-BOAT rather than per-player.</b> Trade up to
        /// a Cape Islander with half a tank of gas in the dory and the dory still has half a tank; come
        /// back to her and it is there.</para>
        /// </summary>
        public void SwitchToHull(BoatHullDef hull)
        {
            if (_primed) SyncToSave();          // bank the old hull, under the old hull's id
            Configure(hull);
            _primed = false;
            Prime();                            // and read the new one's row, or brim-full
        }

        private void OnGameLoaded(GameLoaded _)
        {
            // The blob under us has been replaced, so whatever was primed is from the previous game.
            _primed = false;
            Prime();
        }

        // ---- the POUR verb ----------------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _resolvedId ??= ResolveId();

        /// <summary>"Pour the gas in" — the grade is the whole of what the player needs to know, and it
        /// is the warning as much as the offer. Cached: the popup asks for a label every frame it has a
        /// candidate, so an interpolated string here would be one allocation per frame (rule 7).</summary>
        public string VerbLabel
        {
            get
            {
                string grade = Grade;
                if (_verbLabel == null || !_verbLabel.Contains(FuelGrades.Display(grade)))
                    _verbLabel = "Pour the " + FuelGrades.Display(grade) + " in";
                return _verbLabel;
            }
        }

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _reachMeters;

        /// <summary>
        /// <see cref="InteractPriority.ToolTarget"/> — "use the thing you are holding on the thing in
        /// front of you", which is the rung the shovel-and-clam-hole case defined and which a fuel can
        /// over a filler cap is exactly.
        ///
        /// <para>The rung's obligation is met by <see cref="IsAvailable"/>: this is only ever a candidate
        /// while a fuel vessel is actually held, so it can never outrank "put the thing down" forever.</para>
        /// </summary>
        public int Priority => InteractPriority.ToolTarget;

        /// <summary>On the wharf beside her, or standing on her own deck — §9.4's "anywhere you can reach
        /// the filler with a can in your hands". Not <see cref="InteractContext.AtHelm"/>: a press at the
        /// helm means "step back onto the deck" and the switcher answers it before the verb is ever
        /// consulted, so offering it there would be offering a press that cannot happen.</summary>
        public InteractContext Contexts => InteractContext.OnFoot | InteractContext.OnDeck;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <summary>
        /// Only when there is a tank AND something in your hands to pour into it.
        ///
        /// <para><b>Why this one gates rather than offering-and-refusing.</b> A pump says "available"
        /// always, because a pump you cannot use is still a pump and the refusal tells you something.
        /// A filler cap is different: every boat in the world would otherwise advertise "pour the fuel in"
        /// to a player carrying a bucket of clams, on every hull, forever — including the rowed dory, who
        /// has no tank at all. The verb appears when it means something.</para>
        /// </summary>
        public bool IsAvailable => HasTank && HeldVessel() != null;

        private void OnEnable()
        {
            // Scene-scoped, self-registering — the ActiveBoatProbe pattern exactly. The pump reads this
            // slot to find "the tank the player can address", which is how FILL reaches a boat without
            // the Economy module ever naming a Boats type.
            GameServices.ActiveBoatFuel = this;
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
        }

        private void OnDisable()
        {
            // Identity-guarded, so a boat being torn down cannot clear a slot another boat now owns.
            if (ReferenceEquals(GameServices.ActiveBoatFuel, this))
                GameServices.ActiveBoatFuel = null;
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
        }

        /// <inheritdoc/>
        public void Interact(in InteractActor actor) => TryPour();

        /// <summary>
        /// Pour what is in the player's hands into her tank. Answers the litres that moved — 0 on any
        /// refusal, and every refusal says so out loud (P5).
        ///
        /// <para><b>Costs nothing, and that is the design, not an omission.</b> You already paid, at the
        /// pump or over Ginny's counter. No wallet is consulted and none is wired — a pour that could
        /// charge would be a second, quieter price for the same fuel.</para>
        /// </summary>
        public float TryPour(float maxLitres = 0f)
        {
            IFuelVessel source = HeldVessel();
            PourQuote quote = FuelTransfer.Quote(source, this, maxLitres);
            if (!quote.CanPour)
            {
                EventBus.Publish(new DevNotice(FuelTransfer.Message(quote.Refusal, source?.Grade ?? Grade)));
                return 0f;
            }

            float moved = FuelTransfer.Pour(source, this, maxLitres);
            if (moved <= 0f)
            {
                EventBus.Publish(new DevNotice(FuelTransfer.Message(PourRefusal.SourceEmpty, Grade)));
                return 0f;
            }

            EventBus.Publish(new DevNotice(PourNotice(moved)));
            return moved;
        }

        // ---- helpers ----------------------------------------------------------------------------

        /// <summary>The fuel vessel in the fisher's hands, or null — asked of Core's relay, then of the
        /// held object's own transform, memoized on WHICH object is held. The <c>FuelPump.HeldVessel</c>
        /// helper verbatim, and for the same reason: this is on the resolve path, not the press path.</summary>
        private IFuelVessel HeldVessel()
        {
            ICarriable held = GameServices.Hands?.Carried;
            if (ReferenceEquals(held, _heldCache)) return _vesselCache;

            _heldCache = held;
            Transform t = held?.Transform;
            _vesselCache = t != null ? t.GetComponent<IFuelVessel>() : null;
            return _vesselCache;
        }

        private string PourNotice(float litres)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return litres.ToString("0.0", ci) + " L of " + FuelGrades.Display(Grade) + " into her tank.";
        }

        private string ResolveId()
        {
            BoatHullDef h = Hull;
            string hull = h != null && !string.IsNullOrEmpty(h.Id) ? h.Id : "boat.unknown";
            // Instance-scoped so two boats of the same hull cannot collide. GetEntityId, not the
            // deprecated GetInstanceID — 6000.5 marks that obsolete-as-ERROR, which compiles locally
            // and reds CI.
            return $"fixture.{hull}.filler#{GetEntityId()}";
        }
    }
}
