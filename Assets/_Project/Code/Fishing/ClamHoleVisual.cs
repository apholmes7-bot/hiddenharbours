using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The on-flats <b>look</b> of a clam hole (St Peters opening): the sprite a player actually sees and
    /// walks up to. It is the visible half of a dig spot — <see cref="ClamDig"/> owns the gate logic
    /// (exposed? shovel? bucket room?) and the cosmetic reveal cadence; this drives the picture off it so the
    /// hole is <b>only drawn while its ground is bared by the falling tide</b> and shows the "two squirting
    /// holes" tell while <see cref="ClamDig.ShowingSquirt"/> flips on. When the flood covers the ground the
    /// hole vanishes — the same single tide number that gates the dig also decides whether you can see it, so
    /// the picture can never disagree with the gate (the discipline #69's <c>TidalFlatVisual</c> follows).
    ///
    /// <para><b>Why a sibling of <see cref="ClamDig"/>, not <see cref="HiddenHarbours.World.ClamSpot"/>.</b>
    /// The visual must read exposure (tide) and the reveal cadence, both of which live on the dig in this
    /// lane; reusing the dig's <see cref="ClamDig.IsExposedNow"/> keeps ONE exposure read per hole and keeps
    /// the World marker a pure position. It references no other module — only its own <see cref="ClamDig"/>
    /// and two sprites the builder hands it (CLAUDE.md rule 4).</para>
    ///
    /// <para><b>Two states, two sprites.</b> Exposed &amp; quiet → the still <c>ClamHole</c> sprite (a dimple
    /// in the sand). Exposed &amp; <see cref="ClamDig.ShowingSquirt"/> → the <c>ClamSquirt</c> flip-book (the
    /// give-away spurt) cycled at <see cref="_squirtFps"/>. Submerged → hidden (renderer off). The frames are
    /// data the builder loads from the imported art (sliced, Sprite Mode Multiple) — none hard-coded.</para>
    ///
    /// <para><b>Cheap &amp; allocation-free.</b> One <see cref="SpriteRenderer"/>, a throttled tick (the tide
    /// is slow; a few Hz is plenty), and an index into a cached frame array — no per-frame GC (rule 7). The
    /// reveal itself is cosmetic real-time state on the dig, so determinism is unaffected (rule 5).</para>
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ClamHoleVisual : MonoBehaviour
    {
        [Tooltip("The dig this hole belongs to — read for tide exposure (IsExposedNow) and the squirt tell " +
                 "(ShowingSquirt). Defaults to a ClamDig on this same GameObject.")]
        [SerializeField] private ClamDig _dig;

        [Tooltip("The still clam-hole sprite, shown while the ground is bared and quiet (a dimple in the sand).")]
        [SerializeField] private Sprite _holeSprite;

        [Tooltip("The 'two squirting holes' flip-book frames, cycled while the dig's squirt tell is showing. " +
                 "Empty falls back to the still hole sprite (the tell just doesn't animate).")]
        [SerializeField] private Sprite[] _squirtFrames;

        [Tooltip("Flip-book speed of the squirt tell (frames per second).")]
        [Min(1f)] [SerializeField] private float _squirtFps = 6f;

        [Tooltip("Refresh cadence (Hz) of the show/hide + frame tick. The tide is slow; a few Hz is plenty.")]
        [Min(1f)] [SerializeField] private float _refreshHz = 10f;

        private SpriteRenderer _sr;
        private float _refreshTimer;
        private float _squirtClock;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_dig == null) _dig = GetComponent<ClamDig>();
            if (_holeSprite != null) _sr.sprite = _holeSprite;
            _sr.enabled = false;   // hidden until the first tick proves the ground is bared
        }

        private void Update()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 1f / _refreshHz;

            bool exposed = _dig != null && _dig.IsExposedNow();
            if (!exposed) { if (_sr.enabled) _sr.enabled = false; return; }

            if (!_sr.enabled) _sr.enabled = true;

            bool squirting = _dig.ShowingSquirt && _squirtFrames != null && _squirtFrames.Length > 0;
            if (squirting)
            {
                _squirtClock += 1f / _refreshHz;
                int frame = Mathf.FloorToInt(_squirtClock * _squirtFps) % _squirtFrames.Length;
                if (_squirtFrames[frame] != null) _sr.sprite = _squirtFrames[frame];
            }
            else
            {
                _squirtClock = 0f;
                if (_holeSprite != null) _sr.sprite = _holeSprite;
            }
        }

        /// <summary>Wire the visual in one call (editor / builder).</summary>
        public void Configure(ClamDig dig, Sprite holeSprite, Sprite[] squirtFrames)
        {
            _dig = dig;
            _holeSprite = holeSprite;
            _squirtFrames = squirtFrames;
        }
    }
}
