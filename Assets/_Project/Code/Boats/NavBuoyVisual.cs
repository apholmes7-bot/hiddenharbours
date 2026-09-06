using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// A placed <b>aid to navigation</b>: picks the mark's art for a facing and hands the hull's real
    /// float geometry to <see cref="BuoyWaveVisual"/>, so the mark rides the ONE shared wave field
    /// exactly as the trap buoys do (P1).
    ///
    /// <para><b>This component does NOT bob.</b> It has no Update, no wave sampling and no maths of
    /// its own — it is wiring. The bob, the climbing waterline and the duck-under-a-crest all belong
    /// to <see cref="BuoyWaveVisual"/>, unchanged. Writing a second bob for the nav marks would put
    /// two buoys 30 m apart on two different seas, which is the one thing the shared-field rule
    /// exists to prevent.</para>
    ///
    /// <para><b>Facing is authored, not derived.</b> A moored mark does not steer, so it has no
    /// heading to read — the facing is a placement choice (which side of the mark the player mostly
    /// approaches from). ⚠️ The kit is CLOCKWISE by measurement: cell <c>i</c> depicts heading
    /// <c>+45°·i</c>, the OPPOSITE of every boat in the fleet. Do not "correct" it.</para>
    ///
    /// <para><b>⭐ SHE COLLIDES NOW — this class used to say she did not.</b> The decor tier
    /// anticipated its own promotion and 2026-08-27 is when it came: the owner watched the arrival
    /// run through the entrance marks and asked for buoys that push back. The physics belongs to
    /// <see cref="NavBuoyMooring"/>, wired from the same size rung the art comes from — so a mark's
    /// girth, her displacement and her scope can never describe a different buoy from the one on
    /// screen.</para>
    ///
    /// <para><b>⭐ AND SHE FLASHES NOW.</b> The third promise this class kept deferring — "the data
    /// for a light is already carried here so wiring it needs no re-bake" — is paid: she answers
    /// <see cref="INavLightSource"/>, so an Art component can hang a lantern on her and drive it
    /// off the master clock without the Art assembly ever learning what a <see cref="NavBuoyDef"/>
    /// is (rule 4). She still owns no lamp and draws nothing: what she publishes is her CHARACTER,
    /// her PHASE and where her lantern sits. Only the chart is still its own slice.</para>
    ///
    /// <para><b>Never saved (rule 5).</b> Where a knocked mark is at this instant is transient and
    /// recomputed from her anchor.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuoyWaveVisual))]
    [RequireComponent(typeof(NavBuoyMooring))]
    public sealed class NavBuoyVisual : MonoBehaviour, INavLightSource
    {
        [Header("Which mark, and how big")]
        [Tooltip("The mark. Content is data (ADR 0003) — a new mark is a new NavBuoyDef, not a code change.")]
        [SerializeField] private NavBuoyDef _def;

        [Tooltip("Which rung of the def's size ladder to wear. Empty = the def's own default size.")]
        [SerializeField] private string _sizeId = "";

        [Tooltip("This placement's own chart id, from the region's nav-mark plan (e.g. " +
                 "channel.nmc_entrance.p2). It is what PHASES her light: two port-hand cans a " +
                 "hundred metres apart must not wink together, and the thing that tells them apart " +
                 "has to be a fact about the CHART, never the order they happened to be spawned in. " +
                 "Empty falls back to the GameObject name, which the placer also derives from the id.")]
        [SerializeField] private string _markId = "";

        [Tooltip("Where in her light's period she sits, as a fraction 0..1. Assigned at placement by " +
                 "NavLightPhasePlan, which shares the period out among every mark of her character " +
                 "in this region so no two can wink together. NEGATIVE means 'nobody assigned me one' " +
                 "— she then phases off a hash of her own id, which is right on its own but carries " +
                 "no guarantee about her neighbours.")]
        [SerializeField] private float _phaseFraction = -1f;

        [Tooltip("Which of the 8 baked facings to show, cell order N NE E SE S SW W NW. A moored mark " +
                 "has no heading to derive this from — it is a placement choice.")]
        [Range(0, 7)] [SerializeField] private int _facing;

        [Header("Wiring (the prefab sets these)")]
        [Tooltip("The SpriteRenderer on the CHILD visual that BuoyWaveVisual bobs. Null → found in children.")]
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("The CHILD transform the bob offsets. NEVER this root — the wave field samples at the " +
                 "root, and a sample point that moved with the bob would chase its own tail.")]
        [SerializeField] private Transform _visual;

        private BuoyWaveVisual _bob;
        private NavBuoyMooring _mooring;

        /// <summary>The def this mark wears. Null until wired.</summary>
        public NavBuoyDef Def => _def;

        /// <summary>The facing cell currently shown (0..7).</summary>
        public int Facing => _facing;

        /// <summary>The size rung in force — the named one, or the def's default.</summary>
        public NavBuoyDef.SizeEntry ActiveSize =>
            _def == null ? null
                         : (string.IsNullOrEmpty(_sizeId) ? _def.DefaultSize() : _def.Size(_sizeId));

        // ---- INavLightSource: what she shows, and where from (the Core seam — rule 4) -------------

        /// <summary>
        /// Her light character, parsed once out of the def's chart abbreviation and cached.
        ///
        /// <para><b>⚠️ Parsed from <c>LightText</c>, not <c>LightCharacter</c>.</b> The id is a NAME
        /// and it is lossy — <c>Q3</c> is the east cardinal but her ten-second period appears nowhere
        /// in it, and <c>Q9</c>'s fifteen is likewise missing. <c>LightText</c> is the international
        /// chart form, it is complete, and it is already authored on all ten defs. See
        /// <see cref="NavLightCharacter"/> for the whole argument; a content test holds the id and
        /// the text together so the two can never drift.</para>
        ///
        /// <para>Cached because the parse allocates and this is read whenever a lamp installs. It is
        /// invalidated by <see cref="Apply"/>, so retargeting the def in the inspector re-reads it.</para>
        /// </summary>
        public NavLightCharacter Character
        {
            get
            {
                if (_characterParsed) return _character;
                _characterParsed = true;
                _character = _def == null ? default : NavLightCharacter.Parse(_def.LightText);
                return _character;
            }
        }

        /// <summary>
        /// Where in her period she sits, seconds.
        ///
        /// <para>A mark the region placer stood up carries a FRACTION assigned by
        /// <see cref="NavLightPhasePlan"/>, which shares the period out among every mark of her
        /// character in that harbour — the only way to guarantee two green cans are not winking
        /// together. A mark dropped into a scene by hand has no assignment and falls back to a hash
        /// of her own id, which is right on its own and merely not guaranteed against a neighbour.</para>
        /// </summary>
        public float PhaseSeconds
        {
            get
            {
                NavLightCharacter c = Character;
                if (!c.IsLit) return 0f;
                if (_phaseFraction < 0f) return c.PhaseFromSeed(NavLightCharacter.SeedFromId(PhaseId));
                return c.PeriodSeconds * Mathf.Repeat(_phaseFraction, 1f);
            }
        }

        /// <summary>The id her phase is derived from: her planned chart id, or failing that the name
        /// the placer derived from the same id.</summary>
        public string PhaseId => string.IsNullOrEmpty(_markId) ? name : _markId;

        /// <summary>Her assigned slot in the period as a fraction, or negative if nobody assigned one
        /// and she is phasing off a hash of her own id. Read by the editor's phasing command.</summary>
        public float PhaseFraction => _phaseFraction;

        /// <summary>
        /// How high her lantern burns above her own waterline, metres.
        ///
        /// <para><b>Derived from the bake, not chosen.</b> The nav-buoy sheets pivot ON the waterline
        /// (that is what <see cref="NavBuoyDef.SizeEntry.FloatLineFraction"/> records), so the top of
        /// her painted structure is the rest of her height above it. A 3 m landfall buoy therefore
        /// carries her light higher than a 1.2 m harbour can, without anybody typing either number.</para>
        ///
        /// <para>It is the top of the SPRITE, which on a cardinal is the topmark rather than the
        /// lantern under it — a few tens of centimetres high, stated rather than hidden, and the
        /// same order of approximation the fleet's lamps make by measuring from the keel.</para>
        /// </summary>
        public float LanternHeightMetres
        {
            get
            {
                NavBuoyDef.SizeEntry size = ActiveSize;
                if (size == null) return 0f;
                return Mathf.Max(0f, size.SpriteHeightMeters * (1f - size.FloatLineFraction));
            }
        }

        /// <summary>
        /// The transform her lantern rides — the BOBBED visual, so her light heaves with her in a
        /// seaway instead of hanging in the air above her.
        /// </summary>
        public Transform LanternMount => _visual != null ? _visual : transform;

        private NavLightCharacter _character;
        private bool _characterParsed;

        private void Reset()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_renderer != null) _visual = _renderer.transform;
        }

        private void Awake() => Apply();

        /// <summary>
        /// Place a mark from code: which def, which size rung, which facing, and — when the caller
        /// has one — her chart id, which is what phases her light.
        ///
        /// <para><paramref name="markId"/> is optional so every existing caller compiles unchanged;
        /// a mark placed without one falls back to her GameObject name. The region placers DO pass
        /// it, because a mark whose phase came from her name would re-phase the day somebody renamed
        /// an object in the hierarchy.</para>
        /// </summary>
        public void Configure(NavBuoyDef def, string sizeId, int facing, string markId = null,
                              float phaseFraction = -1f)
        {
            _def = def;
            _sizeId = sizeId;
            _facing = Mathf.Clamp(facing, 0, 7);
            if (markId != null) _markId = markId;
            _phaseFraction = phaseFraction;
            Apply();
        }

        /// <summary>
        /// Give this mark her chart id and her slot in her light's period, without disturbing her
        /// art, her size rung or her facing.
        ///
        /// <para><b>Why this exists separately from <see cref="Configure"/>.</b> A mark already
        /// standing in a scene was placed before her light existed: she has the right buoy on the
        /// right spot and only wants telling where in the period to sit. Re-running the whole
        /// placement to hand her that one number would rewrite the region. Used by the editor
        /// command <c>Hidden Harbours ▸ Art ▸ Phase Nav Lights in Open Scene</c>.</para>
        /// </summary>
        public void AssignPhase(string markId, float phaseFraction)
        {
            if (!string.IsNullOrEmpty(markId)) _markId = markId;
            _phaseFraction = phaseFraction;
        }

        /// <summary>
        /// Resolve the art and re-seat the float geometry. Idempotent — safe to call from the editor
        /// after retargeting the def.
        /// </summary>
        public void Apply()
        {
            // Retargeting the def changes the character, so the cached parse has to go. Cheap: the
            // re-parse happens on the next read, which for a placed mark is once, when a lamp installs.
            _characterParsed = false;

            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_visual == null && _renderer != null) _visual = _renderer.transform;
            if (_bob == null) _bob = GetComponent<BuoyWaveVisual>();
            if (_renderer == null || _bob == null) return;

            NavBuoyDef.SizeEntry size = ActiveSize;
            if (size == null) return;

            if (size.Facings != null && size.Facings.Length > 0)
            {
                int i = Mathf.Clamp(_facing, 0, size.Facings.Length - 1);
                if (size.Facings[i] != null) _renderer.sprite = size.Facings[i];
            }

            _bob.Configure(_renderer, _visual);
            _bob.ConfigureFloatGeometry(size.SpriteHeightMeters, size.FloatLineFraction, size.SlopeFollow);

            // ⭐ The physics comes off the SAME rung as the art. A mark whose collider was sized
            // from one size and whose sprite from another is a buoy you can miss by looking at it.
            if (_mooring == null) _mooring = GetComponent<NavBuoyMooring>();
            if (_mooring != null)
                _mooring.Configure(size.MooredMassKg, size.CollisionRadiusMeters,
                                   size.WatchRadiusMeters,
                                   _def.MooringSpringPerSecondSquared, _def.MooringDampingRatio);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Retarget in the inspector and the mark re-dresses immediately — the owner's placement
            // pass is a scrub through facings and sizes, and a stale sprite would read as a bad bake.
            if (!Application.isPlaying) Apply();
        }
#endif
    }
}
