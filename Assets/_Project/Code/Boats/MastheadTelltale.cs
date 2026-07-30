using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// THE WIND, READ OFF THE BOAT (VS-19) — a pennant at the masthead, which is the oldest wind
    /// instrument there is and the only one that needs no HUD at all. It streams downwind, it goes
    /// board-stiff in a blow, and it hangs dead in a calm. Look at the boat, know the wind.
    ///
    /// <para><b>Apparent wind, because that is the skill.</b> The burgee shows what the BOAT feels —
    /// true wind less the boat's own motion (<see cref="TelltaleMath"/>). Motoring hard into a flat calm
    /// still streams it astern; bearing away in a blow softens it. That is the read a skipper actually
    /// steers on, and it is strictly more informative than the absolute compass direction a HUD line
    /// gives you.</para>
    ///
    /// <para><b>Rides the pictured deck.</b> Same convention as <see cref="DeckContainerPresenter"/>:
    /// this lives on the PHYSICS ROOT, and the masthead anchor is authored in the deck frame (x abeam to
    /// starboard, y along the keel toward the bow) then rotated by the drawn heading each LateUpdate —
    /// so when the hull picture snaps N→NE the pennant jumps with it and stays at the masthead. The
    /// pennant sprite itself is NOT held upright: it rotates to the wind, which is the whole point.</para>
    ///
    /// <para><b>Deterministic underneath, smoothed on top.</b> The state is a pure function of
    /// <c>(trueWind, boatVelocity)</c>; only the visible angle is eased toward it, so the look never
    /// feeds back into the sim (rule 5). Wind is sampled on the HUD's ~4 Hz cadence rather than per
    /// frame, and the sprite is built once — no per-frame allocation (rule 7).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MastheadTelltale : MonoBehaviour
    {
        [Tooltip("Where the pennant flies, in the DECK frame (x abeam toward starboard, y along the " +
                 "keel toward the bow), in metres. Default is just forward of amidships — a dory's " +
                 "painter staff rather than a mast she does not have.")]
        [SerializeField] private Vector2 _mastheadDeckOffset = new Vector2(0f, 1.1f);

        [Tooltip("Height of the pennant above the deck anchor, in metres — how far up the staff it flies.")]
        [SerializeField] private float _staffHeightMeters = 0.9f;

        [Tooltip("Length of the pennant at full stiffness, in metres.")]
        [Min(0.01f)] [SerializeField] private float _lengthMeters = 0.42f;

        [Tooltip("How short the pennant hangs when there is no wind, as a fraction of its full length. " +
                 "Not zero — a dead burgee still hangs down the staff.")]
        [Range(0.05f, 1f)] [SerializeField] private float _limpLengthFraction = 0.35f;

        [Tooltip("Environment sample cadence (Hz). 4 Hz matches the sim's sampling and the HUD's.")]
        [Min(0.1f)] [SerializeField] private float _sampleHz = 4f;

        // Above the hull facing (1) and the deck tray (50) — a masthead is the highest thing aboard.
        private const int TelltaleSortingOrder = 60;
        private const int PennantPixelsPerUnit = 32;

        private SpriteRenderer _renderer;
        private IBoatHullPresenter _hull;
        private BoatController _boat;
        private float _sampleTimer;

        // The resolved target and the eased angle actually drawn.
        private TelltaleState _target;
        private float _shownBearing;
        private bool _hasShown;

        private void Awake()
        {
            _boat = GetComponent<BoatController>();
            // The drawn-facing read goes through the presenter seam (ADR 0022 phase 4), resolved exactly
            // as the deck tray does; a boat without one rides its true heading (smooth hull).
            _hull = BoatHullPresenterHost.Resolve(gameObject);
            BuildPennant();
        }

        private void LateUpdate()
        {
            if (_renderer == null) return;

            // Re-resolve the wind on the sim's cadence; ease the drawn angle every frame so a veering
            // gust reads as a swing rather than a jump.
            _sampleTimer -= Time.deltaTime;
            if (_sampleTimer <= 0f)
            {
                _sampleTimer = _sampleHz > 0f ? 1f / _sampleHz : 0.25f;
                Resolve();
            }

            Place();
        }

        private void Resolve()
        {
            IEnvironmentService env = GameServices.Environment;
            if (env == null) { _target = default; return; }

            Vector2 trueWind = env.Sample().WindVector;
            Vector2 velocity = _boat != null ? _boat.Velocity : Vector2.zero;
            _target = TelltaleMath.Resolve(trueWind, velocity, GameServices.Telltale);
        }

        private void Place()
        {
            // The masthead anchor, on the PICTURED deck (the tray's convention, read through the live host).
            var host = GetComponent<BoatHullPresenterHost>();
            var presenter = (host != null && host.Presenter != null) ? host.Presenter : _hull;
            float heading = presenter != null
                ? presenter.DrawnHeadingDegrees()
                : DirectionalBoatSprite.HeadingDegreesFromBow(transform.up);

            Vector2 anchor = (Vector2)transform.position
                           + DeckContainerPresenter.DeckOffsetToWorld(_mastheadDeckOffset, heading);
            _renderer.transform.position = new Vector3(anchor.x, anchor.y + _staffHeightMeters,
                                                       _renderer.transform.position.z);

            // A limp pennant has no direction — hold the last one rather than snapping to North, so a
            // dying breeze droops in place instead of swinging to an arbitrary bearing.
            if (!_target.IsLimp)
            {
                float target = _target.StreamBearingDegrees;
                _shownBearing = _hasShown
                    ? Mathf.LerpAngle(_shownBearing, target,
                                      1f - Mathf.Exp(-GameServices.Telltale.ResponsePerSecond * Time.deltaTime))
                    : target;
                _hasShown = true;
            }

            // Bearing is clockwise-from-North; a sprite drawn pointing +X needs the complement.
            _renderer.transform.rotation = Quaternion.Euler(0f, 0f, 90f - _shownBearing);

            // Stiffness reads as LENGTH — the shape channel, legible with no colour at all (§8).
            float t = Mathf.Clamp01(_target.Stiffness01);
            float scale = Mathf.Lerp(_limpLengthFraction, 1f, t);
            _renderer.transform.localScale = new Vector3(scale, Mathf.Lerp(0.7f, 1f, t), 1f);
        }

        // ---- the greybox pennant (code-built, same approach as the deck tray's silhouettes) --------

        private void BuildPennant()
        {
            var go = new GameObject("MastheadTelltale");
            go.transform.SetParent(transform, false);

            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sprite = BuildPennantSprite();
            _renderer.sortingOrder = TelltaleSortingOrder;
        }

        /// <summary>
        /// A swallow-tailed burgee pointing +X, hoist at the left edge so it pivots on the staff.
        /// Greybox art: the owner's painted pennant replaces this the moment there is one.
        /// </summary>
        private Sprite BuildPennantSprite()
        {
            int w = Mathf.Max(4, Mathf.RoundToInt(_lengthMeters * PennantPixelsPerUnit));
            int h = Mathf.Max(3, w / 3);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var clear = new Color32(0, 0, 0, 0);
            var cloth = new Color32(216, 74, 58, 255);   // a red burgee — reads against sea and sky alike
            var edge = new Color32(74, 30, 26, 255);     // keyline, so it holds its shape over bright water

            var pixels = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                // Taper toward the fly, then bite a swallow-tail out of the last fifth.
                float alongFraction = x / (float)(w - 1);
                int half = Mathf.RoundToInt(Mathf.Lerp(h * 0.5f, h * 0.22f, alongFraction));
                int notch = alongFraction > 0.8f
                    ? Mathf.RoundToInt(Mathf.InverseLerp(0.8f, 1f, alongFraction) * half)
                    : 0;

                for (int y = 0; y < h; y++)
                {
                    int fromCentre = Mathf.Abs(y - (h - 1) / 2);
                    bool inCloth = fromCentre <= half && fromCentre >= notch;
                    bool onEdge = inCloth && (fromCentre == half || (notch > 0 && fromCentre == notch));
                    pixels[y * w + x] = inCloth ? (onEdge ? edge : cloth) : clear;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            // Pivot at the hoist (left edge, mid-height) so the pennant swings about the staff.
            return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0f, 0.5f), PennantPixelsPerUnit);
        }
    }
}
