using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The <b>runtime consumer the shoreline plant rig was authored for</b>: one placed plant, wearing
    /// the baked tide state that matches the water actually standing over its own ground, and drawing
    /// on the right side of the sea surface.
    ///
    /// <para><b>What the rig gave us and what this adds.</b> The kit bakes five tide states per
    /// species in which lay, wetness, submerged colour and sway are ALREADY resolved together — the
    /// rig's whole design is that they cannot disagree. Until now nothing in <c>Code/</c> read any of
    /// it; the sheets were a matrix with no consumer. This is the consumer, and it is deliberately
    /// small: read the ONE height map through Core, pick a column, set a sprite, set a sorting order.
    /// No new simulation, no second waterline.</para>
    ///
    /// <para><b>⭐ THE SUBMERGED HALF — the owner's ask, and it is a SORTING answer, not a shader
    /// one.</b> Plants below the water should be visible THROUGH the water where it is shallow. The
    /// mechanism already existed in two halves that had never been put together: the rig bakes the
    /// cold WATER ramp into the submerged states (colour only — no dapple, because the live
    /// sprite-light shader's caustics are swell-driven and two uncorrelated patterns on one frond is
    /// worse than none), and the sea already fades toward transparent in the shallows. All that was
    /// missing is drawing the plant in the gap BETWEEN the painted seabed (−21…−18) and the Sea plane
    /// (−5). A submerged bed drawn there is simply seen through shallow water and lost as it deepens.
    /// <b>No new transparency system is introduced and the water shader is not touched.</b></para>
    ///
    /// <para><b>⚠ An emergent plant goes back to the decor band</b> and lets <see cref="YSortSprite"/>
    /// own its order, because a plant sticking out through the surface is decor: leave it under the
    /// Sea plane and the water paints over the half that is in the air.</para>
    ///
    /// <para><b>⚠ THIS COMPONENT HAS NO Update, AND THAT IS A PERFORMANCE DECISION.</b> St Peters
    /// places ~700 of these. An empty <c>Update</c> still costs the engine a managed dispatch per
    /// instance per frame — ~700 × 60/s of doing nothing, which is the exact cost
    /// <see cref="YSortSprite"/> disables itself in play mode to avoid ("a clearing of ~1300
    /// tufts/trees costs literally nothing per frame instead of ~1300 empty calls"). So the whole
    /// shore is driven by ONE <see cref="ShorePlantTideDriver"/> that walks the list on the slow tick.
    /// A view left with no driver still works — it just holds the state it was built with.</para>
    ///
    /// <para><b>Rules.</b> Tide state is RECOMPUTED, never saved (rule 5) — the inputs are the
    /// deterministic water level and the authored seabed. <see cref="Refresh"/> does nothing at all
    /// when the chosen state has not changed, so a shore full of plants costs a float compare each,
    /// a few times a second (rule 7). No per-frame allocations. Everything the plant knows comes from
    /// its <see cref="ShorePlantDef"/> (rule 6).</para>
    ///
    /// <para><b>Core seam.</b> Reads <see cref="ITidalTerrain.ElevationAt"/> and
    /// <see cref="IEnvironmentService.WaterLevelAt"/> through <see cref="GameServices"/> — both
    /// already existed; <b>this component required no new Core member</b>. A null service means "no
    /// region wired yet" and the plant simply holds its authored state rather than throwing.</para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class ShorePlantTideView : MonoBehaviour
    {
        [Tooltip("The species this plant is. Carries its sprites, its zone base, its true standing " +
                 "height and the ladder its states were baked at.")]
        [SerializeField] private ShorePlantDef _def;

        [Tooltip("Sample the seabed at an offset from this transform (metres). 0 samples where the " +
                 "plant stands, which is what you want; a bed on a steep shelf may want its own " +
                 "reading nudged seaward.")]
        [SerializeField] private Vector2 _groundSampleOffset;

        [Header("Edit-mode preview (never runs in a build)")]
        [Tooltip("In the Scene view there is no clock, so the tide is taken from the slider below. " +
                 "Scrub it to see a bed submerge and drain without entering Play — the same call " +
                 "TerrainSplatSurface makes for the ground it sits on.")]
        [SerializeField] private bool _previewInEditMode = true;

        [Tooltip("Water level (m above chart datum) used for the edit-mode preview only.")]
        [SerializeField] private float _previewWaterLevel = 2f;

        private SpriteRenderer _sr;
        private YSortSprite _ySort;
        private SpriteLightBinder _light;
        private int _state = -1;            // -1 = nothing applied yet
        private bool _submerged;
        private bool _appliedOnce;

        /// <summary>The baked column currently worn, 0 (low) … 4 (high). −1 before the first tick.</summary>
        public int State => _state;

        /// <summary>Whether the plant is currently drawn beneath the sea surface.</summary>
        public bool Submerged => _submerged;

        public ShorePlantDef Def
        {
            get => _def;
            set { _def = value; _state = -1; BindLightSheets(); Refresh(); }
        }

        private void OnEnable()
        {
            _sr = GetComponent<SpriteRenderer>();
            _ySort = GetComponent<YSortSprite>();
            _light = GetComponent<SpriteLightBinder>();
            _state = -1;
            BindLightSheets();
            Refresh();
            ShorePlantTideDriver.Register(this);
        }

        /// <summary>
        /// Hand this species' baked light channels to the shared lit path, ONCE.
        ///
        /// <para><b>⭐ Why this is not part of <see cref="Refresh"/>.</b> The sheets do not change with
        /// the tide — the tide changes which SPRITE is worn, and every tide column lives in the same
        /// sheet at its own uv. So the light channel follows the state swap for free: pick column 3 and
        /// the shader samples column 3's key, rim and depth, because it samples at the ALBEDO's uv. The
        /// light and the tide state cannot disagree, which is the same guarantee the rig gives its own
        /// five states. Binding per refresh would be hundreds of property-block writes a second buying
        /// exactly nothing (rule 7).</para>
        ///
        /// <para>A plant with no binder, or a Def baked before the light pass existed, simply draws
        /// unlit — the shore is placed and tide-correct either way.</para>
        /// </summary>
        private void BindLightSheets()
        {
            if (_light == null || _def == null) return;

            // ⚠️ BLUE. The plant rig puts its no-rim flag in the tide sheet's blue channel (255 on strap
            // pixels: blades, culms, fronds). The SHRUBS use their calendar sheet's RED. Both contracts
            // say the same sentence about it — "This is the branch. Read it, do not infer it." — so the
            // channel is published here rather than guessed by the shared consumer.
            _light.SetSheets(
                _def.LightSheet,
                normalSheet: null,               // the plant rig resolves its normals AT BAKE
                rimGateSheet: _def.TideStateSheet,
                rimGateChannel: SpriteLightBinder.RimGateChannel.Blue);
        }

        private void OnDisable() => ShorePlantTideDriver.Unregister(this);

        /// <summary>Edit mode has no clock and no driver, so the preview slider is the tide — scrubbing
        /// it has to move the plant, or the preview is a field that does nothing.</summary>
        private void OnValidate()
        {
            if (Application.isPlaying || _sr == null) return;
            if (_light == null) _light = GetComponent<SpriteLightBinder>();
            _state = -1;
            BindLightSheets();
            Refresh();
        }

        /// <summary>
        /// Re-read the tide and apply the state it implies. Safe to call by hand (the builder does,
        /// once, so a freshly built scene looks right before Play).
        /// </summary>
        public void Refresh()
        {
            if (_def == null || _sr == null) return;

            var terrain = GameServices.TidalTerrain;

            // No region wired yet (EditMode, the persistent boot before the first scene loads): hold
            // the authored state rather than guessing at a tide. A null terrain means "open water"
            // per ITidalTerrain's contract, but a plant with no ground is a plant that should not
            // move — inventing a submerged look here would flicker every scene load.
            if (terrain == null || !TryResolveWaterLevel(out float waterLevel))
            {
                if (!_appliedOnce) Apply(state: 0, submerged: false);
                return;
            }

            Vector2 pos = (Vector2)transform.position + _groundSampleOffset;
            float overM = ShorePlantTideMath.OverGround(waterLevel, terrain.ElevationAt(pos));

            int state = ShorePlantTideMath.StateFor(overM, _def.TideLadderOverM);
            bool submerged = ShorePlantTideMath.IsSubmerged(overM, _def.StandingHeightM);

            if (_appliedOnce && state == _state && submerged == _submerged) return;   // the common case
            Apply(state, submerged);
        }

        /// <summary>
        /// The live water level, or the edit-mode preview. Time comes from
        /// <see cref="GameServices.Clock"/> — the environment service takes the instant, it does not
        /// own one, and reading it from anywhere else is how two consumers end up on two tides.
        /// </summary>
        private bool TryResolveWaterLevel(out float waterLevel)
        {
            if (!Application.isPlaying)
            {
                waterLevel = _previewWaterLevel;
                return _previewInEditMode;
            }

            var env = GameServices.Environment;
            if (env == null) { waterLevel = 0f; return false; }
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            waterLevel = env.WaterLevelAt(now);
            return true;
        }

        private void Apply(int state, bool submerged)
        {
            var sprite = _def.SpriteFor(state);
            if (sprite != null) _sr.sprite = sprite;

            _sr.sortingOrder = ShorePlantTideMath.SortingOrderFor(
                submerged, _def.SubmergedSortingOrder, _def.EmergentSortingOrder, out bool ySorted);

            // ⚠ YSortSprite would immediately overwrite sortingOrder back into its own 2…40 band and
            // float a submerged bed up through the sea. Standing it down while under water is what
            // makes the submerged order stick; re-arming it on emergence hands the order back so the
            // plant sorts against the player like the decor it now is.
            if (_ySort != null)
            {
                _ySort.enabled = ySorted;
                if (ySorted) _ySort.Dynamic = false;   // static decor: sorts once, then stands down
            }

            _state = state;
            _submerged = submerged;
            _appliedOnce = true;
        }
    }
}
