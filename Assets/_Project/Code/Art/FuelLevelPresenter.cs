using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Shows how much is in a fuel container by picking the right baked frame — and, since the pump
    /// landed, IS that amount: this is the container's <see cref="IFuelVessel"/>.
    ///
    /// <para><b>Still visual, and the level is still one number.</b> The class holds a fraction and draws
    /// it, exactly as before; implementing <see cref="IFuelVessel"/> adds no state, it only lets something
    /// outside the art lane read the fraction in litres and pour into it. That is deliberately the same
    /// <see cref="Fill"/> the frame is picked from — a litre count stored beside a fill fraction would be
    /// two numbers for one fact, and the second one drifts. It still does not consume fuel and still does
    /// not know what a boat burns: the pump pushes, nothing pulls.</para>
    ///
    /// <para><b>Why here and not on the carriable.</b> A pump fills what is in front of it, and what will
    /// eventually be in front of it is not always something you can pick up — a bulk tank, a hull's tank.
    /// So "holds fuel" sits on the thing that models the FUEL, not on the thing that models being carried,
    /// and a pump asks for it by <c>GetComponent</c>. See <see cref="IFuelVessel"/>.</para>
    ///
    /// <para>This is the diegetic-UI direction working as intended: the level is <i>on the object</i>
    /// where you have to look at it, not read off a HUD.</para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Hidden Harbours/Fuel Level Presenter")]
    public class FuelLevelPresenter : MonoBehaviour, IFuelVessel
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

        // ---- IFuelVessel (the pump's seam — see the class remarks) --------------------------------

        /// <summary>Which fuel this vessel is FOR, from its Def. Empty with no Def, which reads as "no
        /// grade" and is refused by every pump rather than matching one by accident.</summary>
        public string Grade => _container != null ? _container.Grade : "";

        /// <summary>Brim-full capacity in litres, from the Def. 0 with no Def, and 0 for a vessel that
        /// genuinely holds nothing (a nozzle) — a pump reads both as "not something to fill".</summary>
        public float CapacityLitres => _container != null ? _container.CapacityLitres : 0f;

        /// <summary>How much is in it, in litres — <see cref="Fill"/> against the Def's capacity. Derived,
        /// never stored: the fraction is the one place the level lives.</summary>
        public float Litres => _fill * CapacityLitres;

        /// <summary>
        /// Pour fuel in, clamping at the brim. Writes through <see cref="Fill"/>, so the drawn frame
        /// follows the same way it does when a designer drags the slider.
        ///
        /// <para>Ignores a non-positive amount and a capacity-less vessel — there is nothing sensible to do
        /// with either, and dividing by the second would put a NaN into the serialized fill where it would
        /// outlive the mistake.</para>
        /// </summary>
        public void Deliver(float litres)
        {
            float capacity = CapacityLitres;
            if (litres <= 0f || capacity <= 0f) return;
            Fill = _fill + litres / capacity;
        }

        /// <summary>
        /// Take fuel out, up to <paramref name="litres"/>, and answer what was ACTUALLY given — the POUR
        /// half of the seam (<c>fuel-and-refuelling.md</c> §9.4). Writes through <see cref="Fill"/>, so
        /// the drawn frame empties the same way it fills.
        ///
        /// <para><b>⚠ The answer is measured, not assumed, and that matters here more than anywhere.</b>
        /// This vessel's level is a FRACTION clamped to 0..1 and rendered to a baked frame, so the litres
        /// that actually leave can differ from the litres asked for by a float's worth. Reporting the
        /// request instead of the difference would let a can that gave 4.99 L fill a tank by 5.00, and
        /// fuel would be created one pour at a time. Measure before, measure after, return the gap.</para>
        /// </summary>
        public float Draw(float litres)
        {
            float capacity = CapacityLitres;
            if (litres <= 0f || capacity <= 0f) return 0f;

            float before = Litres;
            Fill = _fill - litres / capacity;      // the Fill setter clamps at empty
            float given = before - Litres;
            return given > 0f ? given : 0f;
        }

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
