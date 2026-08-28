using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The arithmetic of a mark on a chain</b> — a pure function of (where she is, how she is moving,
    /// how much scope she has), so the whole mooring is EditMode-assertable without a scene and comes
    /// out the same on every machine (rule 5). No time, no RNG, no <c>Random</c>.
    ///
    /// <para><b>Two things, and they are different things.</b> The SPRING is how a moored buoy behaves
    /// inside her watch circle: shove her and she comes back, overshoots a little, settles. The CHAIN is
    /// the hard edge of that circle — a mooring is not a rubber band that stretches forever, and a mark
    /// dragged fifty metres by a passing hull is a mark that no longer marks anything.</para>
    /// </summary>
    public static class NavBuoyMooringMath
    {
        /// <summary>
        /// The damping coefficient a ratio asks for. 1 is critical (she returns without overshooting at
        /// all), below 1 she rebounds and settles, above 1 she oozes home.
        ///
        /// <para>Expressed as a RATIO rather than as a coefficient on purpose: a raw damping number is
        /// meaningless without the spring beside it, so tuning one without the other silently changes
        /// the character of every mark. The ratio is the thing an owner actually has an opinion about.</para>
        /// </summary>
        public static float DampingFor(float springPerSecondSquared, float dampingRatio) =>
            Mathf.Max(0f, dampingRatio) * 2f * Mathf.Sqrt(Mathf.Max(0f, springPerSecondSquared));

        /// <summary>
        /// The restoring acceleration on a buoy displaced from her anchor — a damped harmonic spring,
        /// in m/s². An ACCELERATION and not a force, so the mark's own mass has no say in how fast she
        /// comes home: her mass belongs to the collision, not to the chain.
        /// </summary>
        public static Vector2 RestoringAcceleration(Vector2 offsetFromAnchor, Vector2 velocity,
                                                    float springPerSecondSquared, float damping) =>
            -offsetFromAnchor * Mathf.Max(0f, springPerSecondSquared)
            - velocity * Mathf.Max(0f, damping);

        /// <summary>What the chain did this step.</summary>
        public struct Held
        {
            /// <summary>Where she is allowed to be, relative to her anchor.</summary>
            public Vector2 Offset;
            /// <summary>How she is allowed to be moving.</summary>
            public Vector2 Velocity;
            /// <summary>True if the chain came taut — i.e. this step was a correction, not a no-op.</summary>
            public bool Taut;
        }

        /// <summary>
        /// The chain coming taut. Outside the watch circle she is pulled back onto its rim and her
        /// OUTWARD speed is taken off her; her speed along the rim is untouched, which is what makes
        /// a struck buoy swing round her anchor rather than stop dead against an invisible wall.
        ///
        /// <para>⚠ Only the outward component goes. Killing the whole velocity would make the rim
        /// absorb a glancing blow entirely, and a mark that swallows momentum reads as a collision
        /// with the terrain rather than with a floating object.</para>
        /// </summary>
        public static Held HoldTheWatchCircle(Vector2 offsetFromAnchor, Vector2 velocity,
                                              float watchRadiusMetres)
        {
            var held = new Held { Offset = offsetFromAnchor, Velocity = velocity, Taut = false };

            float radius = Mathf.Max(0f, watchRadiusMetres);
            float distance = offsetFromAnchor.magnitude;
            if (radius <= 0f || distance <= radius || distance <= 1e-6f) return held;

            Vector2 outward = offsetFromAnchor / distance;
            held.Offset = outward * radius;
            held.Taut = true;

            float outwardSpeed = Vector2.Dot(velocity, outward);
            if (outwardSpeed > 0f) held.Velocity = velocity - outward * outwardSpeed;
            return held;
        }
    }

    /// <summary>
    /// <b>A nav mark that pushes back.</b> The kit shipped these as decor — <see cref="NavBuoyVisual"/>
    /// says so in as many words: "no collision, no chart, no light" — and this is the promotion it
    /// anticipated, asked for by the owner on 2026-08-27 after watching a skipper drive straight
    /// through the buoyed entrance: <i>"buoys should also have collision with some type of rubberbanding
    /// effect depended on the mass of the vessel."</i>
    ///
    /// <para><b>⭐ THE MASS RESPONSE IS ONE LAW, AND THE LAW IS MOMENTUM.</b> There is no per-hull case
    /// and no response curve. The mark carries a stated displacement mass
    /// (<c>NavBuoyDef.SizeEntry.MooredMassKg</c>, on the fleet's own <c>MassKg / 100</c> scale so the
    /// two are in the same units), and the solver does the rest: a struck vessel's change of velocity
    /// is <c>m_buoy / (m_buoy + m_vessel)</c> of the closing speed, which is large for a punt, small
    /// for a cape islander and nothing at all for a tanker — <i>exactly the ladder the owner described,
    /// out of a single number he can tune.</i> A scripted shove on top of this would be a SECOND motion
    /// path on a hull that already has one, and those drift out of agreement.</para>
    ///
    /// <para><b>⭐ AND THE BUOY ALWAYS GIVES.</b> She is far lighter than anything afloat, so the
    /// contact moves HER first; her chain then brings her home. She cannot stop a boat, she cannot be
    /// taken away, and she never ends up somewhere she does not mark.</para>
    ///
    /// <para><b>Nothing here is saved (rule 5).</b> Where a mark is at this instant is transient runtime
    /// state recomputed from her anchor; the anchor is authored. Nothing about a knock survives a
    /// reload, and nothing about it is random.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class NavBuoyMooring : MonoBehaviour
    {
        [Header("Where she is moored (the placement writes this)")]
        [Tooltip("The anchor's world position. Her watch circle is centred here and she is always " +
                 "pulled back to it. Set by the region build; her scene position is the fallback.")]
        [SerializeField] private Vector2 _anchor;

        [Tooltip("Has the anchor been stated? False lets Awake take her placed position, so a mark " +
                 "dragged into place by hand in the scene view moors where she was dropped.")]
        [SerializeField] private bool _anchorSet;

        [Header("The chain (all of it from her Def - rule 6)")]
        [Tooltip("How far from her anchor she may go before the chain comes taut, in metres.")]
        [Min(0f)] [SerializeField] private float _watchRadiusMetres = 3f;

        [Tooltip("Spring stiffness, 1/s^2. The undamped period is 2*pi/sqrt(k) - 4 gives about 3 s.")]
        [Min(0f)] [SerializeField] private float _springPerSecondSquared = 4f;

        [Tooltip("Damping as a fraction of critical. Below 1 she rebounds and settles; 1 returns her " +
                 "without any overshoot at all.")]
        [Min(0f)] [SerializeField] private float _dampingRatio = 0.5f;

        [Header("What she is, physically")]
        [Tooltip("Her displacement in kg. THIS is the mass-response knob: a struck hull's deflection " +
                 "is m_buoy/(m_buoy + m_hull) of the closing speed, so raising this shoulders every " +
                 "boat harder and lowering it lets the fleet through.")]
        [Min(1f)] [SerializeField] private float _massKg = 300f;

        [Tooltip("Her girth in the water, in metres - the radius a hull actually meets. Her own " +
                 "diameter halved, from the size rung she wears.")]
        [Min(0.05f)] [SerializeField] private float _radiusMetres = 0.875f;

        private Rigidbody2D _rb;
        private CircleCollider2D _collider;

        /// <summary>Where she is moored, in world space.</summary>
        public Vector2 Anchor => _anchor;

        /// <summary>How far she may swing, in metres.</summary>
        public float WatchRadiusMetres => _watchRadiusMetres;

        /// <summary>How far she is from her anchor right now, in metres.</summary>
        public float OffsetFromAnchorMetres =>
            Vector2.Distance(_rb != null ? _rb.position : (Vector2)transform.position, _anchor);

        /// <summary>Her displacement in kg — the one number the whole mass ladder comes out of.</summary>
        public float MassKg => _massKg;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();

            if (!_anchorSet)
            {
                _anchor = transform.position;
                _anchorSet = true;
            }
            Apply();
        }

        /// <summary>
        /// Moor a mark from code: her displacement, her girth, her scope, and the chain's own law.
        /// Called by <see cref="NavBuoyVisual.Apply"/> off the size rung she wears, so a mark's
        /// physics and a mark's art can never describe two different buoys.
        /// </summary>
        public void Configure(float massKg, float radiusMetres, float watchRadiusMetres,
                              float springPerSecondSquared, float dampingRatio)
        {
            _massKg = Mathf.Max(1f, massKg);
            _radiusMetres = Mathf.Max(0.05f, radiusMetres);
            _watchRadiusMetres = Mathf.Max(0f, watchRadiusMetres);
            _springPerSecondSquared = Mathf.Max(0f, springPerSecondSquared);
            _dampingRatio = Mathf.Max(0f, dampingRatio);
            Apply();
        }

        /// <summary>Re-moor her at a stated anchor. The region build calls this with her planned
        /// position, so the anchor is AUTHORED rather than "wherever she happened to be on Awake".</summary>
        public void MoorAt(Vector2 anchor)
        {
            _anchor = anchor;
            _anchorSet = true;
        }

        /// <summary>Push the def's numbers onto the body and the collider. Idempotent.</summary>
        public void Apply()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_collider == null) _collider = GetComponent<CircleCollider2D>();
            if (_rb == null || _collider == null) return;

            _rb.gravityScale = 0f;

            // ⚠ Her facing is AUTHORED (the kit is clockwise; NavBuoyVisual picks the cell). A mark
            // free to spin would show a different face every time a boat brushed her, which reads as
            // a bad bake rather than as physics.
            _rb.freezeRotation = true;

            // ⚠ NO body drag. The chain's damping IS the law here; leaving Unity's linear damping on
            // as well would apply a second, untunable one and the def's ratio would stop meaning what
            // it says.
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;

            // The fleet's own scale — BoatController sets rb.mass = MassKg / 100. Two hulls in
            // different units cannot exchange momentum correctly, and the whole mass response is that
            // exchange.
            _rb.mass = Mathf.Max(0.05f, _massKg / 100f);

            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // A planing RIB does 5.9 m/s; at a 50 Hz step that is 0.12 m a frame against a 0.6 m
            // radius, so discrete detection is enough in principle — and continuous costs nothing on
            // a dozen marks, which is cheaper than one report of a boat passing through a buoy.
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _collider.isTrigger = false;
            _collider.radius = _radiusMetres;
            _collider.offset = Vector2.zero;
        }

        private void FixedUpdate()
        {
            if (_rb == null) return;

            Vector2 offset = _rb.position - _anchor;
            Vector2 velocity = _rb.linearVelocity;

            float damping = NavBuoyMooringMath.DampingFor(_springPerSecondSquared, _dampingRatio);
            Vector2 acceleration = NavBuoyMooringMath.RestoringAcceleration(
                offset, velocity, _springPerSecondSquared, damping);

            // Force = m·a, so the acceleration is what the def states regardless of her displacement.
            _rb.AddForce(acceleration * _rb.mass, ForceMode2D.Force);

            NavBuoyMooringMath.Held held =
                NavBuoyMooringMath.HoldTheWatchCircle(offset, velocity, _watchRadiusMetres);
            if (!held.Taut) return;

            _rb.position = _anchor + held.Offset;
            _rb.linearVelocity = held.Velocity;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // The watch circle, so the owner can see the scope he is tuning.
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Vector3 centre = _anchorSet ? new Vector3(_anchor.x, _anchor.y, 0f) : transform.position;
            const int segments = 48;
            Vector3 previous = centre + new Vector3(_watchRadiusMetres, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * _watchRadiusMetres;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
