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
    /// <para><b>Decor tier, never saved (rule 5).</b> No collision, no chart, no light. A mark that
    /// floats correctly and nothing more; the meaning it carries is data on the def, waiting on its
    /// own slice.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuoyWaveVisual))]
    public sealed class NavBuoyVisual : MonoBehaviour
    {
        [Header("Which mark, and how big")]
        [Tooltip("The mark. Content is data (ADR 0003) — a new mark is a new NavBuoyDef, not a code change.")]
        [SerializeField] private NavBuoyDef _def;

        [Tooltip("Which rung of the def's size ladder to wear. Empty = the def's own default size.")]
        [SerializeField] private string _sizeId = "";

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

        /// <summary>The def this mark wears. Null until wired.</summary>
        public NavBuoyDef Def => _def;

        /// <summary>The facing cell currently shown (0..7).</summary>
        public int Facing => _facing;

        /// <summary>The size rung in force — the named one, or the def's default.</summary>
        public NavBuoyDef.SizeEntry ActiveSize =>
            _def == null ? null
                         : (string.IsNullOrEmpty(_sizeId) ? _def.DefaultSize() : _def.Size(_sizeId));

        private void Reset()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_renderer != null) _visual = _renderer.transform;
        }

        private void Awake() => Apply();

        /// <summary>Place a mark from code: which def, which size rung, which facing.</summary>
        public void Configure(NavBuoyDef def, string sizeId, int facing)
        {
            _def = def;
            _sizeId = sizeId;
            _facing = Mathf.Clamp(facing, 0, 7);
            Apply();
        }

        /// <summary>
        /// Resolve the art and re-seat the float geometry. Idempotent — safe to call from the editor
        /// after retargeting the def.
        /// </summary>
        public void Apply()
        {
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
