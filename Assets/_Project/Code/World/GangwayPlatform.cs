using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A <b>gangway</b> — the hinged brow between fixed ground and a <see cref="FloatingPlatform"/>. The
    /// third <see cref="IStandableSurface"/>, and the one that makes the second one worth having: a float
    /// you can stand on but cannot walk onto is a float that goes nowhere, which is precisely the defect
    /// the harbour channel shipped a draft of (a lane wet at every station of its own line, with seven
    /// metres of drying ground between it and the sea).
    ///
    /// <para><b>Its deck is a RAMP, not a height.</b> One end is bolted to the abutment at a fixed
    /// elevation; the other rides the float. So the standing height along it is the straight line between
    /// them, and the whole thing re-solves its own slope every time the tide moves — steep at low water,
    /// almost flat at high. The tide is not consulted here at all: the landing end is read straight off
    /// the float (<see cref="FloatingPlatform.DeckElevationNow"/>), so the brow and the thing it lands on
    /// <b>cannot disagree about where the landing is</b>. That is the same reason the ladder and the
    /// mooring lines are composed from one <c>MooringLineMath</c> rather than each deriving the deck.</para>
    ///
    /// <para><b>The footprint is a SEGMENT with a half-width</b>, not a <see cref="Rect"/> — a brow is a
    /// narrow thing at an arbitrary angle, and an axis-aligned rectangle around a diagonal one would
    /// claim water on both sides of it. Both ends are inclusive, exactly as
    /// <see cref="StandablePlatform.Contains"/> is, so where the brow meets the quay and where it meets
    /// the float there is no half-texel seam reporting sea between two decks.</para>
    ///
    /// <para><b>⚠ A GANGWAY DOES NOT GATE ITS OWN SLOPE, because nothing in the project does yet.</b>
    /// At a 4.4 m tide a short brow stands at better than 1:3 for the bottom of the ebb, which a person
    /// hauls themselves up rather than strolls. The on-foot sim has no slope gate — the same open flag
    /// the region's cliffs carry — so today this walks at any angle. When the gate lands it reads
    /// <see cref="SlopeAt"/> and needs nothing here to change.</para>
    ///
    /// <para><b>No float, no brow.</b> With nothing to land on the surface reports <c>false</c> — "not a
    /// structure" — and the sim falls back to the seabed underneath, which is the honest answer and the
    /// same degradation an empty registry gives. It deliberately does NOT fall back to a flat deck at the
    /// abutment height: that would draw a walkable bridge over open water out of a missing reference.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GangwayPlatform : MonoBehaviour, IStandableSurface
    {
        [Header("Identity")]
        [Tooltip("Stable id of the brow (e.g. 'wharf.nine_mile_creek.gangway') — diagnostics only.")]
        [SerializeField] private string _id = "gangway.unnamed";

        [Header("Where it runs (WORLD metres)")]
        [Tooltip("The HINGE end, on fixed ground — the abutment face it is bolted to.")]
        [SerializeField] private Vector2 _shoreEnd;

        [Tooltip("The LANDING end, on the float.")]
        [SerializeField] private Vector2 _floatEnd;

        [Tooltip("Half the walking width (m). The drawn brow and the walkable brow are the same width " +
                 "on purpose (the render==sim discipline of ADR 0010): a walkway wider in the sim than " +
                 "in the picture is a player standing on air.")]
        [SerializeField] private float _halfWidthMetres = 0.5f;

        [Header("Heights")]
        [Tooltip("The abutment's standing height (m above chart datum) — MEASURE it off the deck the " +
                 "hinge is bolted to, so a ramp meets that deck rather than stepping above it.")]
        [SerializeField] private float _abutmentElevation;

        [Tooltip("The float this brow lands on. Its deck is read LIVE, so the landing end rides the tide " +
                 "with it and the two can never disagree about where the step down is.")]
        [SerializeField] private FloatingPlatform _landing;

        /// <inheritdoc/>
        public string Id => _id;

        /// <summary>The hinge end, world metres (for tests / tooling).</summary>
        public Vector2 ShoreEnd => _shoreEnd;

        /// <summary>The landing end, world metres (for tests / tooling).</summary>
        public Vector2 FloatEnd => _floatEnd;

        /// <summary>Half the walking width, metres (for tests / tooling).</summary>
        public float HalfWidthMetres => _halfWidthMetres;

        /// <summary>The abutment's standing height, m above chart datum (for tests / tooling).</summary>
        public float AbutmentElevation => _abutmentElevation;

        /// <summary>The float it lands on (for tests / tooling). Null = no brow; see the type note.</summary>
        public FloatingPlatform Landing => _landing;

        /// <summary>How long the brow is on the ground plan, metres. The RAMP is longer by its own rise,
        /// which at this project's tides is a few percent — this is the plan run, which is what the
        /// geography constrains.</summary>
        public float RunMetres => Vector2.Distance(_shoreEnd, _floatEnd);

        /// <summary>Author the brow in one call (region builders; the direct-configure convention).</summary>
        public void Configure(string id, Vector2 shoreEnd, Vector2 floatEnd, float halfWidthMetres,
                              float abutmentElevation, FloatingPlatform landing)
        {
            _id = id;
            _shoreEnd = shoreEnd;
            _floatEnd = floatEnd;
            _halfWidthMetres = halfWidthMetres;
            _abutmentElevation = abutmentElevation;
            _landing = landing;
        }

        private void OnEnable() => StandableSurfaces.Register(this);

        private void OnDisable() => StandableSurfaces.Unregister(this);

        /// <inheritdoc/>
        public bool TryGetDeckElevation(Vector2 worldPos, out float deckElevation)
        {
            deckElevation = 0f;
            if (_landing == null) return false;
            if (!TryProject(_shoreEnd, _floatEnd, worldPos, _halfWidthMetres, out float along)) return false;
            deckElevation = Mathf.Lerp(_abutmentElevation, _landing.DeckElevationNow(), along);
            return true;
        }

        /// <summary>How far the brow falls from hinge to landing (m) at a water level: positive going
        /// DOWN to the float, which is the ordinary case, and negative at the top of a spring tide if a
        /// float ever rode above its own abutment.</summary>
        public float DropAt(float waterLevel)
            => _landing == null ? 0f : _abutmentElevation - _landing.DeckElevationAt(waterLevel);

        /// <summary>The brow's slope at a water level as rise over plan run (0.4 = 1:2.5). Zero for a
        /// degenerate brow with no run, which is the reading that cannot divide by nothing.</summary>
        public float SlopeAt(float waterLevel)
        {
            float run = RunMetres;
            return run <= 1e-4f ? 0f : Mathf.Abs(DropAt(waterLevel)) / run;
        }

        // ---- the footprint rule (pure + static, so it is EditMode-testable without a component) --------

        /// <summary>
        /// Is <paramref name="worldPos"/> on the brow — within <paramref name="halfWidth"/> of the
        /// segment <paramref name="from"/> → <paramref name="to"/> and between its ends? Reports how far
        /// ALONG it (0 at <paramref name="from"/>, 1 at <paramref name="to"/>), which is the fraction the
        /// deck height is interpolated by.
        ///
        /// <para>Both the ends and the edges are INCLUSIVE, for the reason
        /// <see cref="StandablePlatform.Contains"/> states: a point exactly on the lip is on the planks,
        /// and a visible seam where two decks meet is a bug.</para>
        /// </summary>
        public static bool TryProject(Vector2 from, Vector2 to, Vector2 worldPos, float halfWidth,
                                      out float along)
        {
            along = 0f;
            Vector2 span = to - from;
            float lengthSquared = span.sqrMagnitude;
            if (lengthSquared <= 1e-6f) return false;          // a brow with no run is not a surface

            float t = Vector2.Dot(worldPos - from, span) / lengthSquared;
            if (t < 0f || t > 1f) return false;

            Vector2 foot = from + span * t;
            if (Vector2.Distance(worldPos, foot) > Mathf.Max(0f, halfWidth)) return false;

            along = t;
            return true;
        }
    }
}
