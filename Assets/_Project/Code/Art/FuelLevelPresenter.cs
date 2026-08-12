using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Shows how much is in a fuel container by picking the right baked frame.
    ///
    /// <para><b>Visual only, and deliberately so.</b> This holds a fraction and draws it. It does not
    /// consume fuel, does not know what a boat burns, does not talk to the economy, and nothing in
    /// the game writes to it yet — the fuel gameplay is phase-gated. <see cref="Fill"/> is settable
    /// from the inspector and from any future system; that is the whole seam.</para>
    ///
    /// <para>This is the diegetic-UI direction working as intended: the level is <i>on the object</i>
    /// where you have to look at it, not read off a HUD.</para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Hidden Harbours/Fuel Level Presenter")]
    public class FuelLevelPresenter : MonoBehaviour
    {
        [SerializeField] FuelContainerDef _container;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How full, as a fraction of capacity BY VOLUME. Not a height — the rig solved the " +
                 "surface height when it baked, which is why a horizontal skid tank at 0.25 shows " +
                 "just under a third of the way up its glass rather than a quarter.")]
        float _fill = 0.6f;

        [SerializeField]
        [Tooltip("Which baked facing to show, clockwise from north.")]
        int _facing;

        SpriteRenderer _renderer;

        public FuelContainerDef Container
        {
            get => _container;
            set { _container = value; Apply(); }
        }

        /// <summary>Fraction of capacity by volume, clamped to 0..1. Setting it redraws.</summary>
        public float Fill
        {
            get => _fill;
            set { _fill = Mathf.Clamp01(value); Apply(); }
        }

        /// <summary>Baked facing index, clockwise from north. Setting it redraws.</summary>
        public int Facing
        {
            get => _facing;
            set { _facing = value; Apply(); }
        }

        /// <summary>The frame index <see cref="Fill"/> currently resolves to.</summary>
        public int FillIndex => NearestFillIndex(_container != null ? _container.FillFractions : null, _fill);

        void OnEnable() => Apply();

        void OnValidate()
        {
            _fill = Mathf.Clamp01(_fill);
            if (_container != null && _container.Facings > 0)
                _facing = Mathf.Clamp(_facing, 0, _container.Facings - 1);
            Apply();
        }

        /// <summary>Draws the frame for the current fill and facing. Safe to call at any time.</summary>
        public void Apply()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null || _container == null) return;

            var sprite = _container.Frame(FillIndex, _facing);
            if (sprite != null) _renderer.sprite = sprite;
        }

        /// <summary>
        /// The baked fill state NEAREST a continuous fraction — a quantised readout, the way a real
        /// gauge needle points at the nearest mark.
        ///
        /// <para>Pure and static so it can be pinned by a test without a scene. Returns 0 for a null
        /// or empty ladder, which is what a vessel that holds nothing (a nozzle, a dispenser) has.</para>
        ///
        /// <para>⚠️ Nearest means a five-rung ladder shows "empty" up to 12.5% — honest for a gauge,
        /// but if running dry ever becomes a real failure the rounding rule is the thing to revisit,
        /// not the ladder.</para>
        /// </summary>
        public static int NearestFillIndex(float[] fractions, float fill)
        {
            if (fractions == null || fractions.Length == 0) return 0;

            fill = Mathf.Clamp01(fill);
            int best = 0;
            float bestDistance = Mathf.Abs(fractions[0] - fill);

            for (int i = 1; i < fractions.Length; i++)
            {
                float d = Mathf.Abs(fractions[i] - fill);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }
    }
}
