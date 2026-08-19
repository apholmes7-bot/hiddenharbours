using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A <b>floating standable structure</b> — a pontoon whose deck height is the TIDE's. The variant of
    /// <see cref="StandablePlatform"/> for the one piece of harbour furniture that piece cannot express:
    /// a fixed platform is nailed to one elevation above chart datum, and a float at Nine Mile Creek
    /// swings through 4.4 m of it. Registering a float as a fixed platform would put the fisher standing
    /// two metres over the water at low tide, or two metres under it at high — a wrong number that
    /// nothing would fail on (<c>NineMileCreekWharf.FloatCleatPositions</c> refused to build the walk for
    /// exactly that reason, and this type is what it was waiting for).
    ///
    /// <para><b>⭐ It needed no Core change.</b> <see cref="IStandableSurface"/> is a QUERY — "am I over
    /// you, and if so how high is your deck?" — and its own doc says a deck that moves is an
    /// implementation of that contract rather than a change to it. This is the first registrant to prove
    /// it: nothing in Core, in <see cref="StandableSurfaces"/> or in any on-foot consumer moves, and the
    /// float composes through the same one number (<c>StandableSurfaces.StandingElevation</c>) as
    /// the quay it hangs off.</para>
    ///
    /// <para><b>THE WHOLE MECHANIC IS ONE LINE</b> (<see cref="DeckElevation"/>):</para>
    /// <code>deck = max(waterLevel − draught, bed) + (draught + freeboard)</code>
    /// <para>Afloat, the underside rides one draught below the water and the deck one freeboard above it.
    /// When the ebb takes the water down to where the underside meets the ground, the <c>max</c> pins her
    /// to the bed and she <b>takes the ground</b> like any other hull — the deck stops falling and the
    /// water keeps going. Nothing animates, nothing is saved, and grounding is not a second rule that
    /// could disagree with the first: it is the same expression, from the other side.</para>
    ///
    /// <para><b>Rigid, and grounded on the SHALLOWEST bed under her.</b> A raft is one plane — the water
    /// surface is flat, so every bay of a coupled run rides at the same height — and a rigid thing rests
    /// on the highest ground beneath it. So the bed here is a single MEASURED number: the shallowest
    /// ground anywhere inside the footprint, sampled off the terrain by the builder exactly as
    /// <see cref="StandablePlatform"/>'s deck height is measured off the fill it stands on. Author it and
    /// a terrain edit that shoals the berth grounds the float instead of leaving it hovering.</para>
    ///
    /// <para><b>Deterministic, never saved</b> (CLAUDE.md rule 5). The deck is a pure function of
    /// <c>(waterLevel, bed, draught, freeboard)</c>, and the water level is itself recomputed from
    /// <c>(worldSeed, gameTime)</c>. With no environment service wired (EditMode, pre-bootstrap) the water
    /// reads 0 and the deck sits at its own freeboard above datum — the established gate-off shape
    /// (<c>BoatCleats.ElevationOf</c>, <c>ControlSwitcher.BoatDeckElevationNow</c>), not a special
    /// case.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatingPlatform : MonoBehaviour, IStandableSurface
    {
        [Header("Identity")]
        [Tooltip("Stable id of the float (e.g. 'wharf.nine_mile_creek.float') — diagnostics only; never " +
                 "a lookup key.")]
        [SerializeField] private string _id = "float.unnamed";

        [Header("Footprint (WORLD space, axis-aligned — metres)")]
        [Tooltip("The rectangle of deck you can stand on, in WORLD metres — the float's plan outline. " +
                 "One metre outside it the sim is back to reading the seabed.")]
        [SerializeField] private Rect _footprint = new Rect(0f, 0f, 1f, 1f);

        [Header("How she floats (metres)")]
        [Tooltip("FREEBOARD: how high the deck stands above her own waterline while afloat. This is the " +
                 "number the drawn float was baked at — take it from the art rather than picking one, or " +
                 "the planks the player stands on and the planks they SEE part company as the tide runs.")]
        [SerializeField] private float _freeboardMetres = 0.4f;

        [Tooltip("DRAUGHT: how deep her underside sits below her own waterline while afloat. Decides the " +
                 "one thing freeboard cannot — WHEN SHE TAKES THE GROUND. A float authored with no " +
                 "draught can never ground, which is a prettier harbour than the real one.")]
        [SerializeField] private float _draughtMetres = 0.31f;

        [Header("What she grounds on")]
        [Tooltip("The controlling seabed under her: the SHALLOWEST ground anywhere inside the footprint, " +
                 "in metres above chart datum. MEASURE it off the terrain — a raft is rigid and rests on " +
                 "the highest thing beneath it, so the shallowest point is the one that decides. Left " +
                 "unauthored (−∞) she cannot take the ground at all, which is the gate-off reading: no " +
                 "bed known, so no grounding claimed.")]
        [SerializeField] private float _bedElevation = float.NegativeInfinity;

        /// <inheritdoc/>
        public string Id => _id;

        /// <summary>The world-space rectangle of standable deck (for tests / tooling / the validator).</summary>
        public Rect Footprint => _footprint;

        /// <summary>How high her deck rides above her own waterline, metres (for tests / tooling).</summary>
        public float FreeboardMetres => _freeboardMetres;

        /// <summary>How deep her underside rides below her own waterline, metres (for tests / tooling).</summary>
        public float DraughtMetres => _draughtMetres;

        /// <summary>The controlling seabed under her, metres above chart datum (for tests / tooling).</summary>
        public float BedElevation => _bedElevation;

        /// <summary>Her structural depth, deck to underside — <c>draught + freeboard</c>.</summary>
        public float HullDepthMetres => HullDepth(_draughtMetres, _freeboardMetres);

        /// <summary>
        /// Author the float in one call (region builders; the direct-configure convention). The footprint
        /// is WORLD space, and <paramref name="bedElevation"/> is MEASURED off the terrain — see the field
        /// tooltip for why it is the shallowest point under her rather than the deepest or the middle.
        /// </summary>
        public void Configure(string id, Rect worldFootprint, float bedElevation,
                              float draughtMetres, float freeboardMetres)
        {
            _id = id;
            _footprint = worldFootprint;
            _bedElevation = bedElevation;
            _draughtMetres = draughtMetres;
            _freeboardMetres = freeboardMetres;
        }

        private void OnEnable() => StandableSurfaces.Register(this);

        private void OnDisable() => StandableSurfaces.Unregister(this);

        /// <inheritdoc/>
        public bool TryGetDeckElevation(Vector2 worldPos, out float deckElevation)
        {
            deckElevation = DeckElevationNow();
            // The SAME footprint rule as the fixed platform, deliberately shared rather than re-written:
            // a float's edge and a quay's edge must be inclusive in the same way or the seam between them
            // (the gangway lands on one and hinges off the other) reports water for half a texel.
            return StandablePlatform.Contains(_footprint, worldPos);
        }

        /// <summary>Her deck height (m above chart datum) at an arbitrary water level — the pure form, for
        /// tests and for anything reasoning about a tide it is not standing in.</summary>
        public float DeckElevationAt(float waterLevel)
            => DeckElevation(waterLevel, _bedElevation, _draughtMetres, _freeboardMetres);

        /// <summary>Her deck height right now, off the live deterministic water level.</summary>
        public float DeckElevationNow() => DeckElevationAt(WaterLevelNow());

        /// <summary>True when the ebb has put her underside on the bottom at the given water level.</summary>
        public bool IsAgroundAt(float waterLevel)
            => IsAground(waterLevel, _bedElevation, _draughtMetres);

        /// <summary>…and right now.</summary>
        public bool IsAgroundNow() => IsAgroundAt(WaterLevelNow());

        // ---- the maths (pure + static, so the whole mechanic is EditMode-testable without a scene) -----

        /// <summary>Her structural depth, deck to underside: <c>draught + freeboard</c>. Negative inputs
        /// are floored at zero — a float that draws less than nothing is data we cannot honour.</summary>
        public static float HullDepth(float draughtMetres, float freeboardMetres)
            => Mathf.Max(0f, draughtMetres) + Mathf.Max(0f, freeboardMetres);

        /// <summary>
        /// ⭐ <b>THE DECK HEIGHT</b> (m above chart datum): the underside rides one draught below the
        /// water until the bed stops it, and the deck is one hull-depth above the underside.
        ///
        /// <para>Written as an explicit comparison rather than <c>Mathf.Max</c> so a
        /// NaN bed (an unmeasured float, a torn-down terrain) degrades to "afloat" instead of poisoning
        /// the deck height — <c>Mathf.Max(x, NaN)</c> returns NaN, and a NaN standing elevation would make
        /// every on-foot depth read NaN with nothing to point at.</para>
        /// </summary>
        public static float DeckElevation(float waterLevel, float bedElevation,
                                          float draughtMetres, float freeboardMetres)
        {
            float underside = waterLevel - Mathf.Max(0f, draughtMetres);
            if (bedElevation > underside) underside = bedElevation;
            return underside + HullDepth(draughtMetres, freeboardMetres);
        }

        /// <summary>True when a float of <paramref name="draughtMetres"/> over a bed at
        /// <paramref name="bedElevation"/> is on the bottom at <paramref name="waterLevel"/> — the same
        /// <c>depth &lt; draught</c> rule <c>BoatCrossing.CanFloat</c> holds every hull to, so a float and
        /// a boat cannot disagree about what "aground" means.</summary>
        public static bool IsAground(float waterLevel, float bedElevation, float draughtMetres)
            => waterLevel - Mathf.Max(0f, draughtMetres) < bedElevation;

        /// <summary>The live deterministic water level (m above datum), or <b>0</b> with no environment
        /// service wired — the established gate-off shape, under which a float sits at its own freeboard
        /// above datum rather than throwing or vanishing.</summary>
        public static float WaterLevelNow()
        {
            IEnvironmentService environment = GameServices.Environment;
            if (environment == null) return 0f;
            IGameClock clock = GameServices.Clock;
            return environment.WaterLevelAt(clock != null ? clock.TotalSeconds : 0.0);
        }
    }
}
