using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>A pail that visibly fills</b> — the presenter for a container whose fills are BAKED STATES
    /// rather than stacked item sprites.
    ///
    /// <para><b>Why the tote's renderer cannot do this job.</b> <see cref="CatchFillRenderer"/> places
    /// individual catch sprites on the tote rig's measured slot points and clips them to its rim; it needs
    /// <c>CatchStorageAnchors.json</c>, which publishes geometry for the TOTE and for nothing else. The
    /// bucket rig takes the other approach entirely — <c>bucketRig.js</c> bakes the whole picture per
    /// (tier × fill band × catch group × facing), <c>Bucket_pail_&lt;band&gt;_&lt;group&gt;_d&lt;dir&gt;</c>,
    /// which is why a pail can be drawn at 48×52 with a hollow interior and a wire bail and still read at
    /// 32 px/m. So the pail swaps a sprite; it never stacks one.</para>
    ///
    /// <para><b>The bands are the shared rule, not a second one.</b> Which band a fraction reads as comes
    /// from <see cref="CatchFillMath.BandFor"/> — the same pinning the deck tray uses: EMPTY only when
    /// truly empty, BRIM only when truly full, everything between spread across few/half/full. One fish in
    /// a twenty-clam pail therefore shows FEW and not empty, which is the whole diegetic point: you can
    /// see that you have something.</para>
    ///
    /// <para><b>Budget (rule 7).</b> Event-driven: the bridge pushes contents on the Core edges and this
    /// swaps at most one sprite reference. No per-frame work, no allocation, no <c>Find</c> — the sprite
    /// table is a serialized array indexed by arithmetic.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BucketFillPresenter : MonoBehaviour, ICatchFillTarget
    {
        /// <summary>The four non-empty bands, in the rig's own order — the index base for
        /// <see cref="_fillSprites"/>.</summary>
        public static readonly CatchFillBand[] Bands =
            { CatchFillBand.Few, CatchFillBand.Half, CatchFillBand.Full, CatchFillBand.Brim };

        /// <summary>How many facing rows the bucket rig bakes.</summary>
        public const int Facings = 8;

        [SerializeField, Tooltip("The catch art table — the speciesId → kind map the bridge uses. The " +
                                 "SAME asset the tote renderer reads, so a cod is a cod in both.")]
        private CatchItemLibrary _library;

        [SerializeField, Tooltip("Bucket_<tier>_empty_d0..d7 — the eight facings of the empty pail. " +
                                 "This is what an empty container draws, and the fallback whenever a " +
                                 "filled state is missing.")]
        private Sprite[] _emptySprites = new Sprite[Facings];

        [SerializeField, Tooltip("Bucket_<tier>_<band>_<group>_d<dir> flattened as " +
                                 "[(band × 3 + group) × 8 + dir] — band in few/half/full/brim order, " +
                                 "group in the bucket rig's fish/shell/crust order. 96 entries when " +
                                 "fully baked; a missing entry falls back to the empty sprite for that " +
                                 "facing rather than to nothing, so a half-imported kit still draws a pail.")]
        private Sprite[] _fillSprites = new Sprite[Bands.Length * 3 * Facings];

        [SerializeField, Range(0, Facings - 1),
         Tooltip("Facing row of the pail this fill sits in (row d = heading 45°·d, the baked-sheet " +
                 "convention).")]
        private int _direction;

        [SerializeField, Tooltip("The pail's own renderer. Auto-resolved off this object when empty.")]
        private SpriteRenderer _renderer;

        private readonly List<string> _kinds = new List<string>();
        private float _fill01;
        private float _spoil;
        private bool _dirty = true;

        /// <inheritdoc/>
        public CatchItemLibrary Library => _library;

        /// <summary>The band the pail is drawing right now — what a test asserts instead of reading
        /// pixels, and what the owner sees in the inspector while tuning capacities.</summary>
        public CatchFillBand Band { get; private set; } = CatchFillBand.Empty;

        /// <summary>The catch group the pail is drawing (fish / shell / crust), or null while empty.</summary>
        public string Group { get; private set; }

        /// <summary>How many catch units the pail was last told it holds — the count behind the band, so
        /// a test can say "she stored three fish and the pail knows it".</summary>
        public int ItemCount => _kinds.Count;

        /// <inheritdoc/>
        public void SetContents(IReadOnlyList<string> kinds, float fill01)
        {
            _kinds.Clear();
            if (kinds != null)
                for (int i = 0; i < kinds.Count; i++)
                    _kinds.Add(kinds[i]);
            _fill01 = fill01;
            _dirty = true;
        }

        /// <inheritdoc/>
        public void SetDirection(int direction)
        {
            int d = ((direction % Facings) + Facings) % Facings;
            if (d == _direction) return;
            _direction = d;
            _dirty = true;
        }

        /// <inheritdoc/>
        public void SetSpoil(float spoil01)
        {
            float s = Mathf.Clamp01(spoil01);
            if (Mathf.Approximately(s, _spoil)) return;
            _spoil = s;
            _dirty = true;
        }

        private void Awake() => Resolve();
        private void OnEnable() => _dirty = true;
        private void OnValidate() => _dirty = true;      // owner scrubs the inspector dials

        private void LateUpdate()
        {
            if (!_dirty) return;
            RebuildNow();
        }

        /// <summary>
        /// Choose and apply the sprite NOW rather than at the next <c>LateUpdate</c>. Public for the same
        /// reason <see cref="CatchFillRenderer.RebuildNow"/> is: EditMode never runs a frame, so a test
        /// that has just stored a fish has no other way to settle the picture.
        /// </summary>
        public void RebuildNow()
        {
            _dirty = false;
            Resolve();

            Band = CatchFillMath.BandFor(_fill01, _kinds.Count);
            Group = Band == CatchFillBand.Empty ? null : CatchFillMath.BucketGroupOf(_kinds);

            if (_renderer == null) return;
            _renderer.sprite = SpriteFor(Band, Group, _direction);
            _renderer.color = CatchSpoilMath.RendererTint(_spoil);
        }

        /// <summary>
        /// The baked sprite for one (band, group, facing), or the empty pail when that state was not
        /// baked. Public + static-shaped so the index arithmetic is testable on its own: a table indexed
        /// wrong draws a plausible pail at the wrong fullness, which is exactly the kind of wrong nobody
        /// notices in play.
        /// </summary>
        public Sprite SpriteFor(CatchFillBand band, string group, int direction)
        {
            int d = ((direction % Facings) + Facings) % Facings;
            Sprite empty = _emptySprites != null && d < _emptySprites.Length ? _emptySprites[d] : null;
            if (band == CatchFillBand.Empty || group == null) return empty;

            int bandIndex = System.Array.IndexOf(Bands, band);
            int groupIndex = System.Array.IndexOf(CatchFillMath.BucketGroups, group);
            if (bandIndex < 0 || groupIndex < 0) return empty;

            int i = (bandIndex * CatchFillMath.BucketGroups.Length + groupIndex) * Facings + d;
            Sprite filled = _fillSprites != null && i < _fillSprites.Length ? _fillSprites[i] : null;
            return filled != null ? filled : empty;
        }

        /// <summary>Wire the art in one call (the region builders / tests) — the Configure seam used
        /// across the lane, because a builder sets serialized fields it cannot see and EditMode never
        /// runs a builder.</summary>
        public void Configure(CatchItemLibrary library, Sprite[] emptyByDir, Sprite[] fillsFlattened)
        {
            if (library != null) _library = library;
            if (emptyByDir != null) _emptySprites = emptyByDir;
            if (fillsFlattened != null) _fillSprites = fillsFlattened;
            _dirty = true;
        }

        private void Resolve()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        }
    }
}
