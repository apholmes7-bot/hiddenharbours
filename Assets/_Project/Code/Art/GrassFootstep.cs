using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The WALKER-TRAIL bridge for the grass and flower shaders. Drop it on anything that moves through the
    /// meadow (the player, a villager on a routine, a cart) and it records that transform's recent PATH and
    /// publishes it into the SHARED global shader arrays <c>_GrassTrail</c> and <c>_GrassWalkers</c> with
    /// <see cref="Shader.SetGlobalVectorArray(int, Vector4[])"/>. The shaders part each tuft away from the
    /// NEAREST recent trail point within a footprint-sized radius, fading by the point's recency — so the grass
    /// bends along the path the walker actually trod and springs back behind them (a trodden trail), instead of
    /// a wide halo that circles them.
    ///
    /// <para><b>A POOL of walkers, not one (2026-08-13).</b> This used to publish a single 24-point ring buffer
    /// into one global array from every instance's <c>LateUpdate</c> — so a second <see cref="GrassFootstep"/>
    /// did not add a second trail, it OVERWROTE the first every frame (last writer wins). That is why the
    /// village routines deliberately left this component OFF the villagers: six of them would have erased the
    /// player's own trodden path. The array is now <see cref="MaxWalkers"/> SEGMENTS of
    /// <see cref="PointsPerWalker"/> points; each enabled instance claims one segment (see
    /// <see cref="ClaimSlot"/>) and only ever writes its own, so N walkers tread N trails.</para>
    ///
    /// <para><b>How one walker's trail works.</b> A ring buffer of the last <see cref="PointsPerWalker"/> world
    /// positions: the HEAD point tracks the live position (the footprint under the walker), and a new point is
    /// laid down every <see cref="_pointSpacing"/> metres of travel. Each point fades from full to nothing over
    /// <see cref="_trailLifetime"/> seconds (the spring-back). The shader takes the strongest nearby point (not
    /// a sum) so overlapping footprints never stack into a bulge.</para>
    ///
    /// <para><b>Behind-only (direction of travel).</b> Each point also carries the HEADING the walker was moving
    /// when it was laid (packed as an angle in the trail vector's w), and each walker publishes its own live
    /// speed factor (0..1) in its <c>_GrassWalkers</c> record's <c>w</c>. The shaders use these to bend grass
    /// only BEHIND each footprint while that walker is moving — grass AHEAD of the foot stays upright until it
    /// is actually trodden, so the parting trails the walk rather than bulging ahead of it; the gate relaxes to
    /// symmetric when the walker is still. (This was the scalar global <c>_PlayerMoving</c>, which could only
    /// ever describe one walker; it is retired.)</para>
    ///
    /// <para><b>Performance (rule 7).</b> Each walker also publishes a BOUNDING CIRCLE of its live trail
    /// (<c>_GrassWalkers[i].xy</c> = centre, <c>.z</c> = radius, or &lt; 0 for an unclaimed slot), so a blade
    /// tests one circle per walker and skips that walker's whole segment when it is out of reach. The cull is
    /// pure optimisation — a skipped point could not have bent that blade anyway — and it means a lone player
    /// in a big region costs LESS per tuft than the old un-culled 24-point loop, while a crowd only costs where
    /// the crowd actually stands. The buffers are static and reused (no per-frame allocation) and each instance
    /// uploads the SHARED arrays in <c>LateUpdate</c>, so the last writer in a frame carries every walker's
    /// writes.</para>
    ///
    /// <para><b>No per-blade state, nothing saved (rule 5).</b> The recovery is the recency fade — there is no
    /// per-tuft state and nothing is persisted; <see cref="Time.time"/> only drives the cosmetic fade
    /// (visual-only, like the water shader's <c>_Time</c>). <b>Seam discipline (rule 4):</b> it only writes
    /// shader globals — it references no other feature module.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrassFootstep : MonoBehaviour
    {
        /// <summary>
        /// How many walkers the shared trail pool holds — MUST match <c>WALKERS_N</c> in
        /// HiddenHarboursGrass.shader AND HiddenHarboursFlower.shader (pinned by GrassTrailPoolTests).
        /// St Peters wants seven: the player plus the six villagers on routines.
        /// </summary>
        public const int MaxWalkers = 8;

        /// <summary>
        /// Trail points ONE walker owns — MUST match <c>POINTS_N</c> in both shaders. Sized so the ring buffer
        /// is never the thing that truncates the trail: at the default 0.25 m spacing it spans 6 m, and the
        /// 1.2 s lifetime only reaches that far at a full 5 m/s sprint.
        /// </summary>
        public const int PointsPerWalker = 24;

        /// <summary>
        /// Total points in the global <c>_GrassTrail</c> array — MUST match <c>TRAIL_N</c> in both shaders.
        /// </summary>
        public const int TrailPointCount = MaxWalkers * PointsPerWalker;

        /// <summary>
        /// The <see cref="Priority"/> the PLAYER claims with (set by PersistentCoreBuilder). Above the default
        /// 0 that ambient walkers use, so a village full of villagers can never cost the player their trail —
        /// the player's component re-claims its slot on every region hop (the persistent core root toggles),
        /// which would otherwise mean claiming LAST, after the new region's villagers.
        /// </summary>
        public const int PlayerPriority = 100;

        private static readonly int IdGrassTrail = Shader.PropertyToID("_GrassTrail");
        private static readonly int IdGrassWalkers = Shader.PropertyToID("_GrassWalkers");

        /// <summary>An unclaimed pool slot: a NEGATIVE cull radius, which the shaders skip outright.</summary>
        private static readonly Vector4 EmptyWalker = new Vector4(0f, 0f, -1f, 0f);

        // ---- the SHARED pool (one set of buffers for every walker; see the class doc) ----------------------
        private static readonly Vector4[] Trail = new Vector4[TrailPointCount];
        private static readonly Vector4[] Walkers = new Vector4[MaxWalkers];
        private static readonly GrassFootstep[] SlotOwner = new GrassFootstep[MaxWalkers];
        private static bool _poolReady;
        private static bool _warnedPoolFull;

        [Tooltip("Optional explicit source to track. Leave empty to track THIS object's transform (the usual " +
                 "case: put this component on the player or an NPC).")]
        [SerializeField] private Transform _source;

        [Tooltip("Who wins a trail slot when all of them are taken. A claimant evicts the lowest-priority " +
                 "holder ranked STRICTLY below it; equal priorities never evict each other, so a full pool " +
                 "simply leaves the newest walker without a trail. The player is built at " +
                 "GrassFootstep.PlayerPriority; leave ambient walkers at 0.")]
        [SerializeField] private int _priority;

        [Tooltip("Speed (m/s) at which the behind-only gate is FULLY directional. Below it the footprint parts " +
                 "the grass more symmetrically (so standing/shuffling still flattens what's underfoot); at or " +
                 "above it, only grass behind the direction of travel bends.")]
        [Min(0.05f)] [SerializeField] private float _moveSpeedForFull = 1.2f;

        [Tooltip("How quickly the moving/standing blend responds (seconds). Smoothing avoids the gate flickering " +
                 "between symmetric and directional as the walker starts and stops.")]
        [Min(0f)] [SerializeField] private float _moveSmoothing = 0.12f;

        [Tooltip("Seconds a trodden footprint takes to spring back (fade to nothing). Shorter = the grass " +
                 "recovers right behind you; longer = a path lingers.")]
        [Min(0.05f)] [SerializeField] private float _trailLifetime = 1.2f;

        [Tooltip("Lay down a new footprint every this many metres of travel. Roughly a stride; smaller = a " +
                 "denser, smoother path (uses up the trail length faster).")]
        [Min(0.02f)] [SerializeField] private float _pointSpacing = 0.25f;

        [Tooltip("Release the pool slot in OnDisable so the grass relaxes when the walker leaves (e.g. " +
                 "boarding a boat) and another walker can use it. Off HOLDS both the slot and the last path, " +
                 "so the grass keeps its parting — at the cost of one of the MaxWalkers slots.")]
        [SerializeField] private bool _resetOnDisable = true;

        private readonly Vector2[] _pos = new Vector2[PointsPerWalker];
        private readonly float[] _born = new float[PointsPerWalker];
        private readonly float[] _heading = new float[PointsPerWalker];   // radians the walker moved when laid
        private int _head;
        private int _slot = -1;
        private bool _seeded;
        private Vector2 _prevPos;
        private float _headingRad;     // last known heading (kept while ~still so the gate doesn't snap)
        private float _moving;         // smoothed 0..1 speed factor published in this walker's record

        private Transform Source => _source != null ? _source : transform;

        /// <summary>
        /// Who wins a pool slot when all <see cref="MaxWalkers"/> are taken (see <see cref="PlayerPriority"/>).
        /// Read when the slot is CLAIMED, i.e. in <c>OnEnable</c> — set it before the object is activated for
        /// it to matter.
        /// </summary>
        public int Priority
        {
            get => _priority;
            set => _priority = value;
        }

        /// <summary>
        /// The pool slot this walker owns, or -1 when it has none (the pool was full at claim time, or the
        /// component is disabled). Its points occupy <c>_GrassTrail</c> indices
        /// <see cref="SlotBase"/>(slot) .. +<see cref="PointsPerWalker"/>. Exposed for tests and diagnostics.
        /// </summary>
        public int Slot => _slot;

        private void OnEnable()
        {
            EnsurePool();
            for (int i = 0; i < PointsPerWalker; i++) { _born[i] = -1e9f; _pos[i] = Vector2.zero; _heading[i] = 0f; }
            _head = 0;
            _seeded = false;
            _moving = 0f;
            _prevPos = Source.position;
            if (_slot >= 0 && SlotOwner[_slot] == this)
            {
                // We still hold a slot from before: _resetOnDisable is off, so OnDisable kept the parting. Do
                // NOT claim a second one (that would leak the first, frozen, forever) — reuse it, cleared, since
                // the ring buffer above has just been reset and this is a fresh visit.
                ClearSegment(_slot);
                Walkers[_slot] = EmptyWalker;
            }
            else
            {
                _slot = ClaimSlot(this);
            }
            Step();
        }

        private void LateUpdate() => Step();

        private void OnDisable()
        {
            if (!_resetOnDisable)
            {
                // HOLD the parting: keep the slot and the points, but relax the directional gate so a removed
                // walker never leaves it engaged on a stale heading.
                if (_slot >= 0) Walkers[_slot].w = 0f;
                Publish();
                return;
            }
            ReleaseSlot();
            Publish();
        }

        // ==== the shared pool ==============================================================================

        /// <summary>
        /// Publish the EMPTY pool before the first scene loads. An unset global array reads as all-zero, and a
        /// zero record means "claimed, standing still" (radius 0), not "unclaimed" — so without this a scene
        /// with no walkers in it would have every blade sweep all <see cref="TrailPointCount"/> zero-recency
        /// points instead of culling <see cref="MaxWalkers"/> empty segments outright. Same picture either way;
        /// this is the cost.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PublishEmptyPool()
        {
            EnsurePool();
            Publish();
        }

        private static void EnsurePool()
        {
            if (_poolReady) return;
            _poolReady = true;
            for (int i = 0; i < TrailPointCount; i++) Trail[i] = Vector4.zero;
            for (int i = 0; i < MaxWalkers; i++) { Walkers[i] = EmptyWalker; SlotOwner[i] = null; }
        }

        /// <summary>
        /// Take a free pool slot, or — when every slot is taken — evict the lowest-priority holder ranked
        /// STRICTLY below the claimant (so an ambient villager yields to the player, but two equals never
        /// churn each other's trails). Returns -1 when nothing can be freed; that walker simply leaves no
        /// trail, which is cosmetic. Destroyed owners read as null (Unity's fake-null) and their slot is
        /// reclaimed here, so a pool slot cannot leak past a scene unload.
        /// </summary>
        private static int ClaimSlot(GrassFootstep claimant)
        {
            for (int i = 0; i < MaxWalkers; i++)
            {
                if (SlotOwner[i] != null) continue;
                SlotOwner[i] = claimant;
                Walkers[i] = EmptyWalker;
                ClearSegment(i);
                return i;
            }

            int victim = -1;
            int worst = claimant._priority;   // strictly-below: an equal-ranked holder is never evicted
            for (int i = 0; i < MaxWalkers; i++)
            {
                GrassFootstep owner = SlotOwner[i];
                if (owner == null) continue;   // unreachable via the loop above, but a static registry outlives reloads
                if (owner._priority >= worst) continue;
                worst = owner._priority;
                victim = i;
            }

            if (victim < 0)
            {
                if (!_warnedPoolFull)
                {
                    _warnedPoolFull = true;
                    Debug.LogWarning(
                        $"[GrassFootstep] All {MaxWalkers} grass-trail slots are taken, so '{claimant.name}' " +
                        "treads no path. Raise GrassFootstep.MaxWalkers (and WALKERS_N in HiddenHarboursGrass" +
                        ".shader / HiddenHarboursFlower.shader together — GrassTrailPoolTests pins them), or " +
                        "give the walkers that matter a higher Priority. Cosmetic only.");
                }
                return -1;
            }

            SlotOwner[victim]._slot = -1;
            SlotOwner[victim] = claimant;
            Walkers[victim] = EmptyWalker;
            ClearSegment(victim);
            return victim;
        }

        private void ReleaseSlot()
        {
            int s = _slot;
            _slot = -1;
            if (s < 0) return;
            if (SlotOwner[s] == this) SlotOwner[s] = null;
            // Zero every point's recency so no tuft thinks the walker is still standing in it.
            ClearSegment(s);
            Walkers[s] = EmptyWalker;
        }

        private static void ClearSegment(int slot)
        {
            int b = SlotBase(slot);
            for (int i = 0; i < PointsPerWalker; i++) Trail[b + i] = Vector4.zero;
        }

        /// <summary>
        /// Upload BOTH shared arrays. Every enabled walker does this in its own <c>LateUpdate</c>, so whichever
        /// runs last in a frame uploads a buffer that already holds every walker's writes for that frame — no
        /// ordering assumption and no separate publisher host to install.
        /// </summary>
        private static void Publish()
        {
            Shader.SetGlobalVectorArray(IdGrassTrail, Trail);
            Shader.SetGlobalVectorArray(IdGrassWalkers, Walkers);
        }

        // ==== per-frame step ===============================================================================

        private void Step()
        {
            float now = Time.time;
            float dt = Time.deltaTime;
            Vector2 cur = Source.position;

            // --- live speed + heading (drives the behind-only gate) ---
            Vector2 delta = cur - _prevPos;
            _prevPos = cur;
            float speed = dt > 1e-5f ? delta.magnitude / dt : 0f;
            if (delta.sqrMagnitude > 1e-8f) _headingRad = Mathf.Atan2(delta.y, delta.x);   // keep last when ~still
            float movingTarget = MovingFactor(speed, _moveSpeedForFull);
            _moving = SmoothToward(_moving, movingTarget, dt, _moveSmoothing);

            if (_slot < 0) return;   // no pool slot — this walker leaves no trail (cosmetic; see ClaimSlot)

            if (!_seeded)
            {
                _head = 0;
                _pos[0] = cur;
                _born[0] = now;
                _heading[0] = _headingRad;
                _seeded = true;
            }
            else
            {
                // Lay a new footprint once we've travelled a stride; otherwise keep dragging the head along.
                if ((cur - _pos[_head]).sqrMagnitude >= _pointSpacing * _pointSpacing)
                    _head = (_head + 1) % PointsPerWalker;
                _pos[_head] = cur;
                _born[_head] = now;             // the head footprint stays fresh while it's the active one
                _heading[_head] = _headingRad;  // and records the live heading for the behind-only gate
            }

            int b = SlotBase(_slot);
            float radius = 0f;
            for (int i = 0; i < PointsPerWalker; i++)
            {
                float str = TrailStrength(now - _born[i], _trailLifetime);
                Trail[b + i] = new Vector4(_pos[i].x, _pos[i].y, str, _heading[i]);
                radius = GrowRadius(radius, cur, _pos[i], str);
            }
            Walkers[_slot] = new Vector4(cur.x, cur.y, radius, _moving);
            Publish();
        }

        // ==== PURE helpers (testable headless; mirror the shader math) ====================================

        /// <summary>
        /// First <c>_GrassTrail</c> index owned by a pool slot. Segments are fixed-stride and disjoint, which
        /// is what lets each walker write only its own and lets the shader derive the walker from the index.
        /// </summary>
        public static int SlotBase(int slot) => slot * PointsPerWalker;

        /// <summary>
        /// Grow a walker's bounding-circle radius to include one trail point, IGNORING points that have already
        /// sprung back (<paramref name="strength"/> ≤ 0) — those exert no bend, so leaving them out only makes
        /// the circle tighter without ever cutting a point that could still move a blade.
        /// </summary>
        public static float GrowRadius(float radius, Vector2 centre, Vector2 point, float strength)
        {
            if (strength <= 0f) return radius;
            return Mathf.Max(radius, Vector2.Distance(centre, point));
        }

        /// <summary>
        /// The shaders' per-walker CULL test (pure mirror): can ANY point inside this walker's bounding circle
        /// still reach <paramref name="blade"/>? A negative <paramref name="radius"/> marks an unclaimed slot.
        /// False means every point of that walker is at least <paramref name="footRadius"/> away, where
        /// <see cref="FootstepFalloff"/> is already 0 — so skipping the segment can never change the picture.
        /// </summary>
        public static bool WalkerCanReach(Vector2 blade, Vector2 centre, float radius, float footRadius)
        {
            if (radius < 0f) return false;
            float cull = radius + Mathf.Max(footRadius, 1e-3f);
            return (blade - centre).sqrMagnitude <= cull * cull;
        }

        /// <summary>
        /// A trail point's recency strength (0..1) the shader multiplies its bend by: 1 the instant it is trodden,
        /// fading LINEARLY to 0 after <paramref name="lifetime"/> seconds (the spring-back). Clamped, so a stale
        /// or never-set point (huge/negative age) reads 0 — no bend. Monotonic non-increasing in age.
        /// </summary>
        public static float TrailStrength(float age, float lifetime)
        {
            if (age < 0f) return 1f;   // freshly stamped (head refreshed this frame)
            return Mathf.Clamp01(1f - age / Mathf.Max(lifetime, 1e-3f));
        }

        /// <summary>
        /// The per-point footprint falloff the shader applies: <c>1 - smoothstep(0, radius, dist)</c> — full at
        /// the footprint centre, easing to 0 at and beyond <paramref name="radius"/>. Mirrors the HLSL so the
        /// footprint shape is unit-tested headless. Monotonic non-increasing in distance; 0 at/past the radius.
        /// </summary>
        public static float FootstepFalloff(float distance, float radius)
        {
            float r = Mathf.Max(radius, 1e-3f);
            float t = Mathf.Clamp01(distance / r);
            float s = t * t * (3f - 2f * t);   // smoothstep(0,1,t)
            return 1f - s;
        }

        /// <summary>
        /// Live speed → the 0..1 moving factor each walker publishes in its record: speed normalized against
        /// <paramref name="speedForFull"/> and saturated. 0 when still (gate symmetric), 1 at/above the speed
        /// (gate fully behind-only). Deterministic, monotonic in speed.
        /// </summary>
        public static float MovingFactor(float speed, float speedForFull)
        {
            return Mathf.Clamp01(speed / Mathf.Max(speedForFull, 1e-3f));
        }

        /// <summary>
        /// Frame-rate-independent exponential smoothing toward <paramref name="target"/> over time-constant
        /// <paramref name="smoothing"/> seconds. <paramref name="smoothing"/> ≤ 0 snaps. Used to ease the
        /// moving/standing blend so the gate doesn't flicker as the walker starts and stops.
        /// </summary>
        public static float SmoothToward(float current, float target, float dt, float smoothing)
        {
            if (smoothing <= 1e-5f || dt <= 0f) return target;
            float a = 1f - Mathf.Exp(-dt / smoothing);
            return current + (target - current) * a;
        }

        /// <summary>
        /// The behind-only directional gate the shader applies per footprint (pure mirror). <paramref name="ahead"/>
        /// is <c>dot(footprint→blade, heading)</c>: ≤ 0 means the blade is BEHIND the direction of travel (bends),
        /// &gt; 0 means AHEAD (cut out over <paramref name="softness"/> metres). The result is blended toward 1
        /// (symmetric) by (1 − <paramref name="moving"/>) so a standing walker still flattens the grass underfoot.
        /// Returns 1 fully behind, 0 fully ahead while moving; 1 everywhere when still.
        /// </summary>
        public static float DirectionalGate(float ahead, float softness, float moving)
        {
            float w = Mathf.Max(softness, 1e-4f);
            float t = Mathf.Clamp01(ahead / w);
            float behind = 1f - (t * t * (3f - 2f * t));   // 1 - smoothstep(0, softness, ahead)
            return Mathf.Lerp(1f, behind, Mathf.Clamp01(moving));
        }
    }
}
