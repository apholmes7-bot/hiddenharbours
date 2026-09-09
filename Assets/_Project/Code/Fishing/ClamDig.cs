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

        private IHold _bucket;
        private System.Random _rng;

        // The spurt is a pure function of (worldSeed, hole position, t) — ClamSpurtMath, transcribed
        // from shellfishRig2's own holes()/spurt(). The only state kept here is the clock, because a
        // MonoBehaviour is handed a dt rather than a time. Nothing here is saved (rule 5).
        private double _spurtClockMs;
        private float _phaseMs, _periodMs;
        private int _seedUsed;
        private bool _showingSquirt;
        // Said ONCE per hole, the first time this hole has to fall back from its own (missing or dead)
        // provider to the pail on her belt. A fallback that engages silently hides the next wiring rot:
        // the game would keep working and nothing would ever say which hole stopped resolving. Runtime
        // only — never serialized, never saved (rule 5).
        private bool _saidNoProvider;
        private float _spurtRise, _spurtU;
        private bool _consumed;
        private string _resolvedId;

        /// <summary>True while this hole is squirting — the dig's TELL (cosmetic, never gates).</summary>
        public bool ShowingSquirt => _showingSquirt;

        /// <summary>How high the jet stands, 0..1 (<c>sin(π·u)</c> across the 420 ms window). Zero
        /// whenever <see cref="ShowingSquirt"/> is false, so a reader that forgets to check draws
        /// nothing rather than a stuck jet.</summary>
        public float SpurtRise => _spurtRise;

        /// <summary>Progress through the current spurt, 0..1 — the droplet detaches past
        /// <see cref="ClamSpurtMath.DropletAfterU"/>.</summary>
        public float SpurtU => _spurtU;

        /// <summary>This hole's rolled gap between spurts, ms (2600..7800). Exposed for tests and the
        /// presenter; it is derived, never serialized.</summary>
        public float SpurtPeriodMs => _periodMs;

        /// <summary>This hole's rolled phase, ms. ⚠️ NOT "when it starts" — the window opens when
        /// <c>(t + phase) % period ≤ dur</c>. See <see cref="ClamSpurtMath"/>.</summary>
        public float SpurtPhaseMs => _phaseMs;

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
            ResolveSpurtTiming();
        }

        /// <summary>
        /// The seed the SPURT is rolled from: the WORLD's, exactly as the charter says
        /// (<c>deterministic from (worldSeed, hole)</c>) and exactly where every other deterministic
        /// system in the module reads it — <c>LiveFishSchoolWorld.WorldSeed</c> takes the same route.
        ///
        /// <para>⚠️ This is NOT <see cref="_rngSeed"/>. That field seeds the component's own
        /// <see cref="System.Random"/> for the yield roll, it is an inspector convenience, and it is
        /// <b>0 on every hole a builder places</b> — so seeding the spurt from it made every world's
        /// flat identical. It stays the fallback for a scene with no environment service (a bare art
        /// scene, a test), because there a caller-supplied seed is the only entropy there is.</para>
        /// </summary>
        private int SpurtWorldSeed()
        {
            IEnvironmentService env = GameServices.Environment;
            return env != null ? env.WorldSeed : _rngSeed;
        }

        /// <summary>
        /// Roll this hole's phase and period from <c>(worldSeed, its own position)</c>. The position IS
        /// the hole's identity, so a hole that moves is a different hole and must re-roll.
        /// </summary>
        private void ResolveSpurtTiming()
        {
            _seedUsed = SpurtWorldSeed();
            Vector2 p = SpotPos;
            ClamSpurtMath.TimingFor(_seedUsed, p.x, p.y, out _phaseMs, out _periodMs);
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
                // ⭐ TRUE NOW, AND IT WAS NOT BEFORE. EnsureBucket falls back to the pail she carries, so
                // a null here means there is no hold on this flat AND none on her person — she really has
                // nowhere to put it. Before that fallback this sentence could fire at a fisher with a full
                // pail on her belt, purely because the hole's own serialized reference had not survived.
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
                // THE STRIKE is one published beat (juice charter §4.3, §4.5): the sand chunks, the audio
                // slot and the notebook all key off THIS cue, never off the hole (rule 4). Strength = kg.
                EventBus.Publish(new JuiceMomentCue(JuiceMoment.DigStrike, SpotPos, weight));
                _showingSquirt = false;
                _consumed = true;
                Debug.Log($"[ClamDig] Lifted out a {clam} — it's in your hand.");
                return true;
            }

            EnsureBucket();
            if (_bucket == null || !_bucket.TryAdd(clam)) return false;   // race with capacity; cozy no-op

            EventBus.Publish(new FishCaught(clam));     // same land path the rod uses
            EventBus.Publish(new JuiceMomentCue(JuiceMoment.DigStrike, SpotPos, weight));   // the same beat, pail path
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

        /// <summary>Is she already holding a landed catch of THIS species — in either hand? Asked of the
        /// READ side (<see cref="ICarrier"/>) so this file needs nothing from Player but the Core
        /// contracts — a clam in hand reports its SPECIES id as its <c>DefId</c>, which is what makes this
        /// answerable from here.</summary>
        private bool HandsAlreadyFull()
        {
            string id = _clamSpecies != null ? _clamSpecies.Id : null;
            // ⚠️ CarriedItem.InHand, not hands.Carried: she has TWO hands now, and the single-slot read
            // answers only for the one she filled last — so a clam in the off hand would read as "hands
            // empty" and the next press would dig a second one she has nowhere to put.
            return CarriedItem.InHand(GameServices.Hands, id);
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

        /// <summary>
        /// Resolve somewhere to put a clam: the hold this hole was WIRED to first, and failing that the
        /// pail she is carrying (<see cref="GameServices.PlayerHold"/>).
        ///
        /// <para><b>⭐ The fallback is the fix for the owner's 2026-09-09 report</b> ("pressing e the first
        /// time … i needed a bucket"). <see cref="_bucketProvider"/> is a serialized reference to ONE
        /// GameObject, and it is only as good as the scene that wrote it — a hole authored in a region she
        /// is not standing in, a hole spawned by a tool, a hole whose scene was rebuilt round a different
        /// core all leave it dead. When it was dead this method left <c>_bucket</c> null and the dig said
        /// <i>"you need a bucket"</i> to a fisher with a twenty-clam pail on her belt. The pail is a fact
        /// about HER, so it is now asked of her. <b>Nothing is taken away:</b> a hole with a live provider
        /// still uses it, and the sentence below stays exactly as it was — it is simply no longer
        /// reachable while she is carrying a pail, which is the only state in which it was a lie.</para>
        ///
        /// <para><b>The fallback is never silent.</b> A fallback that engages quietly hides the NEXT
        /// hole to lose its wiring: the game keeps working and nothing ever says which one stopped
        /// resolving. So the first time a hole has to reach past its own provider it logs a warning
        /// naming itself, once — the same rule the helm-seat fallback follows. Only when the pail
        /// actually answers: a context with no hold at all (EditMode, a bare art scene) is not a fault,
        /// and it already earns the sentence.</para>
        ///
        /// <para><b>⚠ The laundering line is not decoration.</b> <c>_bucket</c> is INTERFACE-typed, so a
        /// destroyed <c>ClamBucket</c> cached here would compare non-null forever (an interface reference
        /// does not carry <c>UnityEngine.Object</c>'s overloaded <c>==</c>) and every read after would
        /// throw on a corpse instead of falling back. <see cref="GameServices.PlayerHold"/> launders its
        /// own; this launders what the provider handed us.</para>
        /// </summary>
        private void EnsureBucket()
        {
            if (_bucket is UnityEngine.Object dead && dead == null) _bucket = null;   // a corpse is not a hold
            if (_bucket == null && _bucketProvider != null) _bucket = _bucketProvider.GetComponent<IHold>();
            if (_bucket != null) return;

            // …then the pail on her belt — and SAY SO, once, naming this hole. The fallback keeps the
            // game playable; the warning is what stops it from quietly papering over the next hole that
            // loses its wiring. Only when the pail actually answers: a context with no hold at all
            // (EditMode, a bare art scene) is not a fault and earns no warning — it gets the sentence.
            _bucket = GameServices.PlayerHold;
            if (_bucket == null || _saidNoProvider) return;

            _saidNoProvider = true;
            Debug.LogWarning($"[ClamDig] {Id} has no resolvable hold provider; landing in the player's " +
                             "pail. The hole's serialized _bucketProvider is missing or dead — re-run " +
                             "the region builder if this hole should have one.", this);
        }

        /// <summary>
        /// The squirt tell, on the art director's own clock: <see cref="ClamSpurtMath"/> says whether
        /// this hole is spurting at the current moment and how high the jet stands.
        ///
        /// <para>The method still takes a <c>dt</c> because that is what <c>Update</c> is handed, but
        /// the only thing dt does now is advance a clock — the answer is a pure function of that
        /// clock, so two holes at the same instant cannot disagree and nothing drifts. It replaced a
        /// stateful 10–20 s / 1.5 s greybox cadence that was invented rather than measured; the rig
        /// says 2.6–7.8 s apart and 420 ms long.</para>
        /// </summary>
        private void UpdateReveal(float dt, bool exposed)
        {
            if (_consumed) { ClearSpurt(); return; }   // a spent hole gives no more tells
            if (!exposed) { ClearSpurt(); return; }

            // A hole that has never resolved its timing (a test that skipped Awake, an editor-time
            // instance) rolls it now rather than sitting silently at period 0 and never spurting.
            //
            // It also re-rolls if the world seed has CHANGED under it. That is not paranoia: the
            // environment service is frequently absent at Awake and arrives with the persistent core
            // a moment later, so a hole woken early would otherwise keep the fallback seed for the
            // rest of the session and quietly disagree with every hole woken after it.
            if (!(_periodMs > 0f) || SpurtWorldSeed() != _seedUsed) ResolveSpurtTiming();

            _spurtClockMs += Mathf.Max(0f, dt) * 1000.0;
            _showingSquirt = ClamSpurtMath.TrySpurt(_spurtClockMs, _phaseMs, _periodMs,
                                                    out _spurtRise, out _spurtU);
        }

        private void ClearSpurt()
        {
            _showingSquirt = false;
            _spurtRise = 0f;
            _spurtU = 0f;
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

            // The hole's position may have just moved, and the spot IS its identity — so re-roll
            // rather than keep a timing that belonged to a different hole. The seed recorded here
            // only reaches the spurt in a scene with no environment service (see SpurtWorldSeed);
            // in play the world's seed wins, which is what the charter asks for.
            _rngSeed = seed;
            ResolveSpurtTiming();
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
