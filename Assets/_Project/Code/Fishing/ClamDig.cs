using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The on-foot <b>clam dig</b> (St Peters opening, P4 "every job by hand first") — the hand-gather
    /// catch method, NOT the rod mini-game. At a clam-hole spot, when the flats are bared by the falling
    /// tide, you Interact to dig: one press adds a single clam to the bucket. It's the opening's first
    /// by-hand income, the thing you do while you wait out the tide to walk the sandbar to Greywick.
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
    /// land path are the same ones the rod uses, so the Greywick stall sells a dug clam exactly like a
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
    public class ClamDig : MonoBehaviour
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
        [Tooltip("0 = time-seeded weight roll; non-zero for reproducible clam weights in testing.")]
        [SerializeField] private int _rngSeed = 0;

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
                Debug.Log("[ClamDig] This hole's already given up its clam — there's nothing left to dig.");
                return false;
            }
            if (!IsExposedNow())
            {
                Debug.Log("[ClamDig] The flat's still under water here — wait for the tide to fall.");
                return false;
            }
            if (!OwnsShovel())
            {
                Debug.Log("[ClamDig] You need a clam shovel to dig.");
                return false;
            }

            EnsureBucket();
            if (_bucket == null) { Debug.Log("[ClamDig] Nowhere to put a clam — you need a bucket."); return false; }
            if (_bucket.UsedUnits >= _bucket.CapacityUnits)
            {
                Debug.Log("[ClamDig] The bucket's full — head to Greywick and sell.");
                return false;
            }

            float weight = CatchResolver.RollWeight(_clamSpecies, _rng);
            var clam = new CatchItem(_clamSpecies.Id, _clamSpecies.DisplayName, _clamSpecies.Category,
                                     weight, _clamSpecies.BaseValue, _clamSpecies.SupplyElasticity);
            if (!_bucket.TryAdd(clam)) return false;   // race with capacity; cozy no-op

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
    }
}
