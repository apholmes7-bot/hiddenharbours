using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The on-foot <b>clam dig</b> (St Peters opening, P4 "every job by hand first") — the hand-gather
    /// catch method, NOT the rod mini-game. At a clam-hole spot, when the flats are bared by the falling
    /// tide, you Interact to dig: one press adds a single clam to the bucket. It's the opening's first
    /// by-hand income, the thing you do while you wait out the tide to walk the sandbar to Nine Mile Creek.
    ///
    /// <para><b>The gates (all cozy).</b> A dig only lands a clam when: (0) you are <em>at this hole</em> —
    /// the on-foot player is within a small reach (<see cref="ReachRadius"/>, ~1.25 m) of THIS spot, the
    /// same pure-distance test the dock zone uses (you can't dig a hole across the flat); (1) the spot is
    /// <em>exposed</em> — the deterministic tide has bared this ground (<see cref="TidalExposure"/> over
    /// the authored <see cref="ITidalTerrain"/>); (2) the player owns the <b>shovel</b> (starting gear,
    /// read off the save); and (3) the <b>bucket</b> has room (its <see cref="IHold.CapacityUnits"/> —
    /// 20 clams). Fail any gate and the dig just doesn't yield (a log, no penalty). When the tide floods
    /// the spot back over, it stops being diggable — pure tide-gating, the inverse of needing deep water
    /// to float a boat.</para>
    ///
    /// <para><b>One press, one clam, the nearest hole.</b> Each <see cref="ClamDig"/> is just a hole's
    /// gate-and-yield; it does NOT listen for input itself (that bug dug every exposed hole on the bar at
    /// once, filling the 20-clam bucket in a press or two). A single scene-side <see cref="ClamDigger"/> on
    /// the player owns the Interact key: on a press it picks the <em>nearest in-range, exposed</em> hole and
    /// digs only THAT one, so a press is always exactly one clam from the hole you're standing on (and
    /// nothing when you're not at a hole). The digger gathers the candidate holes on each press, not per
    /// frame, so there's no per-frame cost here.</para>
    ///
    /// <para><b>Reuse, don't reinvent.</b> The clam is the existing <c>fish.soft_shell_clam</c>
    /// <see cref="FishSpeciesDef"/> (a Shellfish — the hand-gathered category); the weight roll
    /// (<see cref="CatchResolver.RollWeight"/>) and the <see cref="CatchItem"/> + <see cref="FishCaught"/>
    /// land path are the same ones the rod uses, so the Nine Mile Creek stall sells a dug clam exactly like a
    /// landed fish. Because a dig is a single press (not the tension fight), it doesn't run the
    /// <c>FishFight</c> FSM — it's the lightest hand-gather: bend down, lift one out.</para>
    ///
    /// <para><b>Greybox reveal.</b> The "two little squirt holes give the clam away" tell is a simple
    /// placeholder here: while exposed, <see cref="ShowingSquirt"/> flips on a 10–20 s cadence for art/UI
    /// to render a hint. It's cosmetic only (real-time, not sim state) and never gates the dig — digging
    /// works whenever the spot is exposed. Determinism is unaffected (the reveal isn't world-sim state).</para>
    ///
    /// <para><b>Yields once, then it's spent.</b> A hole gives up its clam <em>once</em>: a successful
    /// <see cref="TryDig"/> flips <see cref="Consumed"/> on, after which the hole no longer yields — the
    /// clam's been lifted out, there's nothing left to dig. The "skittish clam" escape (a player who loiters
    /// too close) also spends the hole via <see cref="MarkConsumed"/>. <see cref="ClamHoleVisual"/> reads
    /// <see cref="Consumed"/> to hide/animate the spent hole. This is a <b>real-time, play-session</b> state,
    /// NOT world-sim — it isn't saved, so a reload reconstructs the deterministic hole field afresh (same as
    /// the cosmetic squirt cue); determinism (the dig YIELD + tidal EXPOSURE) is untouched (rule 5).</para>
    ///
    /// <para><b>Seam discipline.</b> Reads the world terrain + tide through the Core
    /// <see cref="GameServices"/> accessors and the bucket through the Core <see cref="IHold"/> contract;
    /// the shovel-ownership check is the owned-gear list on the save. No World/Player/Environment concrete
    /// classes referenced. The dev Interact key (E) lives on the sibling <see cref="ClamDigger"/>, not here;
    /// an InputService/interaction prompt replaces it later (ui-ux).</para>
    /// </summary>
    public class ClamDig : MonoBehaviour, IInteractable
    {
        [Header("What & where")]
        [Tooltip("The clam species this hole yields (fish.soft_shell_clam). Data-driven — the value, " +
                 "weight range and category all come from the Def, never hard-coded here.")]
        [SerializeField] private FishSpeciesDef _clamSpecies;
        [Tooltip("A GameObject carrying an IHold (the player's ClamBucket). The dug clam is stowed here.")]
        [SerializeField] private GameObject _bucketProvider;
        [Tooltip("The clam-hole world position to test for tidal exposure. Defaults to this object's " +
                 "transform when unset (world-content places the spot).")]
        [SerializeField] private Transform _spot;

        [Header("Gating")]
        [Tooltip("How close (m) the on-foot player must stand to THIS hole's spot to dig it — the reach " +
                 "of a shovel, the same pure-distance gate the dock zone uses. A tunable, not a magic " +
                 "number: forgiving enough to feel cozy, tight enough that you must be at the hole.")]
        [SerializeField] private float _reachRadius = 1.25f;
        [Tooltip("Owned-gear id that enables digging (the shovel). Matches the GearOffer id.")]
        [SerializeField] private string _shovelGearId = "gear.shovel";
        [Tooltip("The TOOL id that must be IN THE FISHER'S HANDS to dig (tool.shovel) — the owner's " +
                 "ruling, 2026-08-13: 'they should need a shovel to dig clams'. Distinct from the gear id " +
                 "above, which is the OWNERSHIP record: owning a shovel and holding one are different " +
                 "facts, and digging now needs both. Empty disables the in-hand gate (ownership only), " +
                 "which is the pre-ruling behaviour and exists so a test can pin the difference.")]
        [SerializeField] private string _shovelToolId = "tool.shovel";
        [Tooltip("Stable INSTANCE id for the interact registry — the resolver's LAST tie-break, so it " +
                 "must be unique among live holes. The builder gives each hole a readable one derived " +
                 "from its cell; left empty an instance-scoped fallback keeps it unique but unreadable.")]
        [SerializeField] private string _id;
        [Tooltip("0 = time-seeded weight roll; non-zero for reproducible clam weights in testing.")]
        [SerializeField] private int _rngSeed = 0;
        [Tooltip("Land the dug clam IN THE FISHER'S HAND rather than straight into the pail (the owner's " +
                 "ruling, 2026-08-13: 'a clam can be in hand but needs to be placed in a container to " +
                 "stack'). Falls back to the pail wherever there are no hands to land into — EditMode, a " +
                 "bare art scene, a region with no persistent core — so nothing ever drops a clam. Turn " +
                 "OFF for the pre-ruling behaviour; that path is what a test uses to prove the difference.")]
        [SerializeField] private bool _landInHand = true;

        [Header("Greybox reveal (cosmetic placeholder)")]
        [Tooltip("Shortest gap between squirt-hole reveals while exposed (real seconds).")]
        [SerializeField] private float _revealMinSeconds = 10f;
        [Tooltip("Longest gap between squirt-hole reveals while exposed (real seconds).")]
        [SerializeField] private float _revealMaxSeconds = 20f;
        [Tooltip("How long a squirt reveal stays shown (real seconds).")]
        [SerializeField] private float _revealShowSeconds = 1.5f;

        private IHold _bucket;
        private System.Random _rng;
        private float _revealTimer;
        private bool _showingSquirt;
        private bool _consumed;
        private string _resolvedId;

        /// <summary>True while the greybox squirt-hole cue is showing (art/UI hint; cosmetic, never gates).</summary>
        public bool ShowingSquirt => _showingSquirt;

        /// <summary>True once this hole has been spent — either dug (it yielded its clam) or escaped (the
        /// skittish clam burrowed away). A consumed hole no longer yields; the visual hides/animates it.
        /// Real-time play-session state, never saved (determinism unaffected — rule 5).</summary>
        public bool Consumed => _consumed;

        /// <summary>How close the player must stand to dig this hole (m) — the shovel's reach.</summary>
        public float ReachRadius => _reachRadius;

        /// <summary>This hole's world spot (the spot transform, or this object's position when unset).</summary>
        public Vector2 SpotPos => _spot != null ? (Vector2)_spot.position : (Vector2)transform.position;

        private void Awake()
        {
            _rng = _rngSeed == 0 ? new System.Random() : new System.Random(_rngSeed);
            if (_bucketProvider != null) _bucket = _bucketProvider.GetComponent<IHold>();
            _revealTimer = RandomRevealGap();
        }

        private void Update()
        {
            // The only per-hole work is the cosmetic squirt-reveal cadence. INPUT lives on ClamDigger so one
            // press digs one clam from the nearest in-range hole — not every exposed hole on the bar at once.
            UpdateReveal(Time.deltaTime, IsExposedNow());
        }

        /// <summary>Is the on-foot player (at <paramref name="playerPos"/>) within shovel reach of this hole's
        /// spot? Pure distance test, mirroring the dock zone — the proximity gate the digger applies before
        /// it digs a hole.</summary>
        public bool WithinReach(Vector2 playerPos) => Vector2.Distance(playerPos, SpotPos) <= _reachRadius;

        /// <summary>
        /// Attempt one dig. Lands a single clam into the bucket and raises <see cref="FishCaught"/> iff all
        /// three gates pass (exposed, shovel owned, bucket has room). Returns true iff a clam was dug.
        /// Public so EditMode tests can drive it without the scene lifecycle / input.
        /// </summary>
        public bool TryDig()
        {
            if (_clamSpecies == null) { Debug.LogWarning("[ClamDig] No clam species wired.", this); return false; }

            if (_consumed)
            {
                Say("This hole's already given up its clam.");
                return false;
            }
            if (!IsExposedNow())
            {
                Say("The flat's still under water here — wait for the tide to fall.");
                return false;
            }
            if (!OwnsShovel())
            {
                Say("You need a clam shovel to dig.");
                return false;
            }
            // THE OWNER'S RULING (2026-08-13): owning the shovel is no longer enough — it has to be in
            // your hands. Kept as a SECOND gate rather than replacing the first because the two are
            // different facts and both are cheap: ownership is what the shop sold you, holding is what
            // you brought to the flats. Asked through Core (CarriedItem), so this file still has no idea
            // that CarryHands exists — Fishing cannot reference Player and must not start (rule 4).
            if (!ShovelInHand())
            {
                Say("Your hands are empty — you need the shovel to dig.");
                return false;
            }

            // ⚠️ HANDS FIRST, and the order of these two checks is the ruling. A dug clam goes IN YOUR
            // HAND (owner, 2026-08-13), so "can it be landed?" is a question about your hands before it is
            // a question about the pail — and a clam already in hand refuses HERE, before the bucket is
            // ever consulted, because a full pail and a full hand are different problems with different
            // sentences. The bucket-room check below still runs for the hold-direct fallback path.
            if (_landInHand && CatchHandsAvailable())
            {
                if (HandsAlreadyFull())
                {
                    Say("You're already holding a clam — put it in the bucket first.");
                    return false;
                }
            }
            else
            {
                EnsureBucket();
                if (_bucket == null) { Say("Nowhere to put a clam — you need a bucket."); return false; }
                if (_bucket.UsedUnits >= _bucket.CapacityUnits)
                {
                    Say("The bucket's full — head to Nine Mile Creek and sell.");
                    return false;
                }
            }

            float weight = CatchResolver.RollWeight(_clamSpecies, _rng);
            // Stamp the freshness clock at the dig (M1 §7.3) — a clam in the pail is on the clock too.
            double dugAt = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            var clam = new CatchItem(_clamSpecies.Id, _clamSpecies.DisplayName, _clamSpecies.Category,
                                     weight, _clamSpecies.BaseValue, _clamSpecies.SupplyElasticity,
                                     _clamSpecies.SpoilPerDay, Freshness.Landed(dugAt));
            // INTO HER HAND if the hands are there to take it; into the pail if they are not.
            //
            // The fallback is not a nicety — it is what keeps every context that has no CarryHands working
            // exactly as it did: EditMode, a bare art scene, a region played directly with no persistent
            // core. "Nobody published a pair of hands" must mean "as before", never a dropped clam.
            //
            // ⚠️ FishCaught is NOT published here on the in-hand path, and that is deliberate. It has
            // always meant "a catch entered a hold" — the fill renderers re-read the container on it, the
            // onboarding director counts it, the deck presenters re-stack. Firing it over a clam that is
            // merely in your fist would lie to all of them. The hand moment publishes CatchLanded instead
            // (from CarryHands), and FishCaught fires when the clam actually goes in the pail.
            if (_landInHand && TryLandInHand(clam))
            {
                _showingSquirt = false;
                _consumed = true;
                Debug.Log($"[ClamDig] Lifted out a {clam} — it's in your hand.");
                return true;
            }

            EnsureBucket();
            if (_bucket == null || !_bucket.TryAdd(clam)) return false;   // race with capacity; cozy no-op

            EventBus.Publish(new FishCaught(clam));     // same land path the rod uses
            _showingSquirt = false;                     // dug it — the tell's gone
            _consumed = true;                           // a hole yields ONCE, then it's spent (the clam's gone)
            Debug.Log($"[ClamDig] Dug a {clam}. ({_bucket.UsedUnits}/{_bucket.CapacityUnits} in the bucket.)");
            return true;
        }

        /// <summary>Spend this hole without yielding a clam — the "skittish clam" escape: a player who
        /// loitered too close let the clam burrow away. After this the hole no longer yields (the visual
        /// plays the sink-away animation then hides). Idempotent. Real-time cosmetic state — not saved
        /// (determinism unaffected, rule 5).</summary>
        public void MarkConsumed()
        {
            _consumed = true;
            _showingSquirt = false;
        }

        /// <summary>Is the dig spot bared by the tide right now? A null terrain (open water) or null
        /// environment reads as submerged (not diggable) — the safe default.</summary>
        public bool IsExposedNow()
        {
            ITidalTerrain terrain = GameServices.TidalTerrain;
            IEnvironmentService env = GameServices.Environment;
            if (terrain == null || env == null) return false;

            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            float ground = terrain.ElevationAt(SpotPos);
            return TidalExposure.IsExposed(env, now, ground);
        }

        private bool OwnsShovel()
        {
            var save = GameServices.Save?.Current;
            return save?.OwnedGear != null && !string.IsNullOrEmpty(_shovelGearId)
                   && save.OwnedGear.Contains(_shovelGearId);
        }

        /// <summary>
        /// Is the shovel in the fisher's hands right now? Asked through Core's
        /// <see cref="CarriedItem.InHand"/>, which is the only thing that crosses the module boundary —
        /// this file cannot see <c>CarryHands</c> and never should.
        ///
        /// <para>An EMPTY <see cref="_shovelToolId"/> means "no in-hand gate", which reads as true. That
        /// is the pre-ruling behaviour, kept reachable on purpose: it is what a test sets to prove the new
        /// gate is the thing doing the work rather than some other condition.</para>
        /// </summary>
        public bool ShovelInHand()
            => string.IsNullOrEmpty(_shovelToolId) || CarriedItem.InHand(_shovelToolId);

        /// <summary>Are there hands to land a clam into? False in EditMode, a bare art scene, or a region
        /// played with no persistent core — and then the dig falls back to the pail exactly as it always
        /// did.</summary>
        private static bool CatchHandsAvailable() => GameServices.CatchHands != null;

        /// <summary>Is she already holding a landed catch? Asked of the READ side (<see cref="ICarrier"/>)
        /// so this file needs nothing from Player but the Core contracts — a clam in hand reports its
        /// SPECIES id as its <c>DefId</c>, which is what makes this answerable from here.</summary>
        private bool HandsAlreadyFull()
        {
            ICarrier hands = GameServices.Hands;
            ICarriable held = hands?.Carried;
            return held != null && !string.IsNullOrEmpty(_clamSpecies != null ? _clamSpecies.Id : null)
                   && string.Equals(held.DefId, _clamSpecies.Id, System.StringComparison.Ordinal);
        }

        /// <summary>Offer the clam to her hands. False means the hands refused (or were not there), and
        /// the caller lands it in the pail instead.</summary>
        private static bool TryLandInHand(in CatchItem clam)
        {
            ICatchHands hands = GameServices.CatchHands;
            return hands != null && hands.TryPutInHand(clam);
        }

        /// <summary>
        /// Say it to the player, not just to the console. A dig is now a deliberate press through the one
        /// interact verb, and a press that refuses EARNS a sentence (P5) — the register the carry
        /// refusals and the wet bucket already use. Still logged, because a headless run has no toast.
        /// </summary>
        private void Say(string message)
        {
            EventBus.Publish(new DevNotice(message));
            Debug.Log($"[ClamDig] {message}");
        }

        // ---- the interact seam (M2-39): the hole is the CANDIDATE, the shovel is the tool -------------

        /// <inheritdoc/>
        public string Id => _resolvedId ??= ResolveId();

        /// <inheritdoc/>
        public Vector2 WorldPosition => SpotPos;

        /// <summary>The shovel's reach — this hole's own tunable, unchanged from the radius the digger
        /// used to apply itself.</summary>
        public float ReachMeters => _reachRadius;

        /// <summary>
        /// <see cref="InteractPriority.ToolTarget"/> — a work-site the tool in your hands operates.
        ///
        /// <para><b>⚠️ This rung is what makes digging possible at all, and the reason is worth keeping
        /// in front of whoever changes it.</b> A held shovel reports <c>Held</c> (20). If a hole were a
        /// plain <c>Fixture</c> (0), then standing on a bared hole WITH THE SHOVEL IN HAND, E would
        /// resolve to "put the shovel down" — and the one thing the shovel exists for would be
        /// unreachable. Using what you hold on what is in front of you is the more specific act; putting
        /// it down is the fallback you get by stepping a pace away.</para>
        /// </summary>
        public int Priority => InteractPriority.ToolTarget;

        /// <summary>On your own two feet — the flats are walked, not sailed.</summary>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <summary>False: a hole at your feet should not need you to look down at it, matching every
        /// other interaction on this seam.</summary>
        public bool RequiresFacing => false;

        /// <summary>
        /// Is this hole a thing to act on right now? Exposed, unspent, AND the shovel in hand.
        ///
        /// <para><b>The shovel condition is not optional decoration — it is the obligation
        /// <see cref="InteractPriority.ToolTarget"/> imposes.</b> A work-site claiming that rung
        /// unconditionally would outrank the held thing forever, and you could never put anything down
        /// while standing near a clam hole. Gating availability on the tool is what keeps the ladder
        /// honest in both directions.</para>
        ///
        /// <para>Note what is deliberately NOT here: bucket room, and shovel OWNERSHIP. Those are
        /// refusals — press and the game tells you (P5) — not reasons to vanish from the resolver and
        /// leave the press silent. Exposure and the tool decide whether this is a work-site at all.</para>
        /// </summary>
        public bool IsAvailable => !_consumed && ShovelInHand() && IsExposedNow();

        /// <summary>One word, because it is one action and the shovel in your hands has already said the
        /// rest. Only reachable with the shovel held and the flat bared — see <see cref="IsAvailable"/> —
        /// so the popup never offers a dig the tide has taken away.</summary>
        public string VerbLabel => "Dig";

        /// <summary>The press: one clam, this hole. The resolver has already picked the nearest qualifying
        /// hole, which is what <c>ClamDigger.NearestDiggable</c> used to do by hand.</summary>
        public void Interact(in InteractActor actor) => TryDig();

        private void OnEnable() => Interactables.Register(this);

        private void OnDisable() => Interactables.Unregister(this);

        private string ResolveId()
        {
            if (!string.IsNullOrEmpty(_id)) return _id;
            // Instance-scoped so two holes can never collide. Readable ids are the builder's job.
            // GetEntityId, not the deprecated GetInstanceID (6000.5 marks that obsolete-as-ERROR).
            return $"fixture.clam_hole#{GetEntityId()}";
        }

        private void EnsureBucket()
        {
            if (_bucket == null && _bucketProvider != null) _bucket = _bucketProvider.GetComponent<IHold>();
        }

        // Cosmetic greybox reveal cadence: flash the "squirt" cue every 10–20 s while exposed.
        private void UpdateReveal(float dt, bool exposed)
        {
            if (_consumed) { _showingSquirt = false; return; }   // a spent hole gives no more tells
            if (!exposed) { _showingSquirt = false; return; }

            _revealTimer -= dt;
            if (_showingSquirt)
            {
                if (_revealTimer <= 0f) { _showingSquirt = false; _revealTimer = RandomRevealGap(); }
            }
            else if (_revealTimer <= 0f)
            {
                _showingSquirt = true;
                _revealTimer = Mathf.Max(0.1f, _revealShowSeconds);
            }
        }

        private float RandomRevealGap()
        {
            float lo = Mathf.Min(_revealMinSeconds, _revealMaxSeconds);
            float hi = Mathf.Max(_revealMinSeconds, _revealMaxSeconds);
            return lo + (float)(_rng?.NextDouble() ?? 0.0) * (hi - lo);
        }

        /// <summary>Wire the dig in one call (tests / editor). <paramref name="reachRadius"/> is the shovel
        /// reach; pass a negative value to leave the serialized/default radius untouched.</summary>
        public void Configure(FishSpeciesDef clamSpecies, IHold bucket, Transform spot, string shovelGearId, int seed,
                              float reachRadius = -1f)
        {
            _clamSpecies = clamSpecies;
            _bucket = bucket;
            _spot = spot;
            _shovelGearId = shovelGearId;
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
            if (reachRadius >= 0f) _reachRadius = reachRadius;
        }

        /// <summary>
        /// Give this hole its interact identity and its in-hand tool gate (builder / tests).
        ///
        /// <para>Split from <see cref="Configure"/> rather than added to it so every existing caller —
        /// the builder and a dozen tests — keeps compiling and keeps meaning what it meant. Pass a null
        /// or empty <paramref name="shovelToolId"/> to run WITHOUT the in-hand gate, which is the
        /// pre-ruling behaviour and is exactly what a test needs to prove the gate is load-bearing.</para>
        /// </summary>
        public void ConfigureInteract(string id, string shovelToolId, bool landInHand = true)
        {
            _id = id;
            _resolvedId = null;
            _shovelToolId = shovelToolId;
            _landInHand = landInHand;
        }
    }
}
