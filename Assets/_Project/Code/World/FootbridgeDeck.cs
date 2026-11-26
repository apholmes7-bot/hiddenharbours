using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A plank footbridge as ground</b>: a deck that runs along its own axis, square to the brook under it,
    /// rather than along the world's x. It is stood on (<see cref="IStandableSurface"/>), it says where its planks
    /// are drawn (<see cref="IStandableOutline"/>), and while the player's feet are on it it guides his walk along
    /// the axis (<see cref="IWalkMovementConstraint"/>), so he runs straight across the way the planks go.
    ///
    /// <para><b>Why not <see cref="StandablePlatform"/>.</b> That is an upright world rectangle by design (its own
    /// note says a turned deck is another implementation). A bridge forced onto the world's x made the path bend
    /// into each end and the rails stand across a diagonal run, so a walker coming along the path met the end of a
    /// rail and was slid round it: his heading turned to the screen's horizontal at each end. This deck is turned
    /// instead, and its rails with it.</para>
    ///
    /// <para><b>The guide.</b> A keyboard has eight headings and a bridge set square to a brook rarely runs along one
    /// of them. Without help, a walker pushing the nearest heading catches a rail end or slides along a rail at an
    /// angle. So while his feet are on the planks, or on the walk's last stretch off either end
    /// (<see cref="FootbridgeDef.GuideApproachMetres"/>), a heading within <see cref="FootbridgeDef.GuideConeDegrees"/>
    /// of the axis is turned onto it at his full pace; a wider heading is left as it is (on the planks the rails stop
    /// it, off them he walks where he likes). The guide is installed only into an empty slot (a mooring's hold is never
    /// displaced) and released the moment his feet leave that ground.</para>
    ///
    /// <para>Authored geometry, data only (rule 6): the builder step writes every field from the bridge's Def.
    /// Deterministic, never saved.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]   // installs (or releases) the guide before the walker takes this tick's step
    public sealed class FootbridgeDeck : MonoBehaviour, IStandableSurface, IStandableOutline, IWalkMovementConstraint
    {
        [SerializeField] private string _id = "bridge.unnamed";

        [Tooltip("The middle of the planks, world metres.")]
        [SerializeField] private Vector2 _at;

        [Tooltip("Which way the deck runs: degrees anticlockwise from the world's +x.")]
        [SerializeField] private float _axisDegrees;

        [Tooltip("Bank to bank along the axis, metres.")]
        [SerializeField] private float _spanMetres = 3f;

        [Tooltip("Across the walk, metres.")]
        [SerializeField] private float _widthMetres = 1.15f;

        [Tooltip("How far up the world (+y) of the planks the deck still counts as stood on, metres: the walker's ground " +
                 "is read at his body, drawn above his feet.")]
        [SerializeField] private float _standingReachMetres = 0.8f;

        [Tooltip("The standing height of the planks, metres above chart datum.")]
        [SerializeField] private float _deckElevation;

        [Tooltip("Headings within this many degrees of the axis are turned onto it while the feet are on the planks.")]
        [SerializeField] private float _guideConeDegrees = 50f;

        [Tooltip("How far off each end of the planks, along the axis, the guide already holds, metres.")]
        [SerializeField] private float _guideApproachMetres = 0.8f;

        private Transform _player;
        private Rigidbody2D _body;
        private Collider2D _feet;
        private IConstrainedWalker _walker;
        private bool _guiding;

        /// <inheritdoc/>
        public string Id => _id;

        /// <summary>The middle of the planks, world metres.</summary>
        public Vector2 At => _at;

        /// <summary>The deck's axis, degrees anticlockwise from the world's +x.</summary>
        public float AxisDegrees => _axisDegrees;

        /// <summary>The deck's axis as a unit vector.</summary>
        public Vector2 Axis => FootbridgeDef.AxisOf(_axisDegrees);

        public float SpanMetres => _spanMetres;
        public float WidthMetres => _widthMetres;
        public float StandingReachMetres => _standingReachMetres;
        public float DeckElevation => _deckElevation;
        public float GuideConeDegrees => _guideConeDegrees;
        public float GuideApproachMetres => _guideApproachMetres;

        /// <summary>Is this deck guiding the player's walk right now? Tests.</summary>
        public bool Guiding => _guiding;

        /// <summary>Author the deck in one call (tests; the builder step writes the same fields as YAML).</summary>
        public void Configure(string id, Vector2 at, float axisDegrees, float span, float width, float reach,
                              float deckElevation, float guideConeDegrees, float guideApproachMetres)
        {
            _id = id; _at = at; _axisDegrees = axisDegrees; _spanMetres = span; _widthMetres = width;
            _standingReachMetres = reach; _deckElevation = deckElevation; _guideConeDegrees = guideConeDegrees;
            _guideApproachMetres = guideApproachMetres;
        }

        private void OnEnable() => StandableSurfaces.Register(this);

        private void OnDisable()
        {
            StandableSurfaces.Unregister(this);
            Release();
        }

        /// <inheritdoc/>
        public bool TryGetDeckElevation(Vector2 worldPos, out float deckElevation)
        {
            deckElevation = _deckElevation;
            return StandsOn(worldPos, _at, _axisDegrees, _spanMetres, _widthMetres, _standingReachMetres);
        }

        /// <inheritdoc/>
        public int GetDrawnOutline(Vector2[] drawn, float[] deckElevation)
        {
            if (drawn == null || deckElevation == null || drawn.Length < 4 || deckElevation.Length < 4) return 0;
            Vector2[] c = FootbridgeDef.CornersOf(_at, _axisDegrees, _spanMetres, _widthMetres);
            for (int i = 0; i < 4; i++) { drawn[i] = c[i]; deckElevation[i] = _deckElevation; }
            return 4;
        }

        // ---- the guide ------------------------------------------------------------------------------------------

        private void FixedUpdate()
        {
            Transform player = GameServices.PlayerTransform;
            if (player != _player)
            {
                Release();
                _player = player;
                _body = player != null ? player.GetComponent<Rigidbody2D>() : null;
                _feet = player != null ? player.GetComponent<Collider2D>() : null;
                _walker = player != null ? player.GetComponent<IConstrainedWalker>() : null;
            }
            if (_player == null || _walker == null) { Release(); return; }

            bool held = OnPlanks(FeetOf(), _at, _axisDegrees, _spanMetres + 2f * Mathf.Max(0f, _guideApproachMetres), _widthMetres);
            if (held && !_guiding && _walker.MovementConstraint == null)
            {
                _walker.MovementConstraint = this;
                _guiding = true;
            }
            else if (!held) Release();
        }

        /// <summary>Where his feet are: the body plus its foot collider's offset (the walker's body is drawn, and
        /// read, above his feet).</summary>
        private Vector2 FeetOf()
        {
            Vector2 body = _body != null ? _body.position : (Vector2)_player.position;
            return _feet != null ? body + _feet.offset : body;
        }

        private void Release()
        {
            if (_guiding && _walker != null && ReferenceEquals(_walker.MovementConstraint, this))
                _walker.MovementConstraint = null;
            _guiding = false;
        }

        /// <inheritdoc/>
        public Vector2 ConstrainWalk(Vector2 destination)
        {
            if (_player == null) return destination;
            // The walker asks about his body's next place (his feet ride with it), so the step is taken from there.
            Vector2 body = _body != null ? _body.position : (Vector2)_player.position;
            return Guide(body, destination, Axis, _guideConeDegrees);
        }

        // ---- the rules, pure for EditMode tests ---------------------------------------------------------------

        /// <summary>A world point in the deck's own frame: x along the axis, y across it (landward positive), from
        /// the middle of the planks.</summary>
        public static Vector2 ToDeck(Vector2 worldPos, Vector2 at, float axisDegrees)
        {
            Vector2 a = FootbridgeDef.AxisOf(axisDegrees), d = worldPos - at;
            return new Vector2(Vector2.Dot(d, a), Vector2.Dot(d, new Vector2(-a.y, a.x)));
        }

        /// <summary>Is a world point on the planks? Edges inclusive, as <see cref="StandablePlatform.Contains"/>.</summary>
        public static bool OnPlanks(Vector2 worldPos, Vector2 at, float axisDegrees, float span, float width)
        {
            Vector2 p = ToDeck(worldPos, at, axisDegrees);
            return Mathf.Abs(p.x) <= span * 0.5f && Mathf.Abs(p.y) <= width * 0.5f;
        }

        /// <summary>
        /// Is a world point stood on: on the planks, or up to <paramref name="reach"/> metres above (world +y) a
        /// point that is? The planks swept up the world by the reach, a convex six-sided shape for a turned deck
        /// (and the upright rectangle extended north for an unturned one, as before).
        /// </summary>
        public static bool StandsOn(Vector2 worldPos, Vector2 at, float axisDegrees, float span, float width, float reach)
        {
            // worldPos - (0, h) is on the planks for some h in [0, reach]. In the deck's frame the step down the
            // world is (-sin, -cos) * h, so each half-extent gives an interval of h; they must meet inside [0, reach].
            Vector2 p = ToDeck(worldPos, at, axisDegrees);
            Vector2 a = FootbridgeDef.AxisOf(axisDegrees);
            float lo = 0f, hi = Mathf.Max(0f, reach);
            return Narrow(p.x, a.y, span * 0.5f, ref lo, ref hi) && Narrow(p.y, a.x, width * 0.5f, ref lo, ref hi) && lo <= hi;
        }

        // |v - h * c| <= half: the h for which it holds, intersected into [lo, hi].
        private static bool Narrow(float v, float c, float half, ref float lo, ref float hi)
        {
            const float Flat = 1e-6f;
            if (Mathf.Abs(c) < Flat) return Mathf.Abs(v) <= half;
            float h0 = (v - half) / c, h1 = (v + half) / c;
            if (h0 > h1) { float t = h0; h0 = h1; h1 = t; }
            lo = Mathf.Max(lo, h0); hi = Mathf.Min(hi, h1);
            return lo <= hi;
        }

        /// <summary>
        /// The guided step: a step heading within <paramref name="coneDegrees"/> of the axis (either way along it)
        /// goes along the axis at its own length; a wider one is left as it is.
        /// </summary>
        public static Vector2 Guide(Vector2 origin, Vector2 destination, Vector2 axis, float coneDegrees)
        {
            Vector2 d = destination - origin;
            float m = d.magnitude;
            if (m < 1e-6f || axis.sqrMagnitude < 1e-12f) return destination;
            axis = axis.normalized;
            float along = Vector2.Dot(d, axis);
            if (Mathf.Abs(along) >= m * Mathf.Cos(Mathf.Clamp(coneDegrees, 0f, 90f) * Mathf.Deg2Rad))
                return origin + axis * (Mathf.Sign(along) * m);
            return destination;
        }
    }
}
