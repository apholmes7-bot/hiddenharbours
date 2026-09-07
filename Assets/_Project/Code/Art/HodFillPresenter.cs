using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>A clam hod that visibly fills</b> — the presenter for the wire roller basket the clam dig
    /// drags along the flat.
    ///
    /// <para><b>Three sprites on one transform, because the basket is HOLLOW.</b> The hod rig bakes its
    /// far half (<c>Hod2_back</c>) and its near half (<c>Hod2_front</c>) separately so the catch can go
    /// BETWEEN them — a full hod shows shells through the galvanised mesh rather than wearing a lid of
    /// them. The middle layer is <c>Hod2_heap_&lt;band&gt;</c>, the rig's own <c>CatchKit2.heap</c>
    /// surface baked per band on the same 40×40 cell and the same (20,30) pivot. So this presenter
    /// swaps three sprite references and owns no layout at all: where the clams sit inside the rim was
    /// decided by the rig at bake time.</para>
    ///
    /// <para><b>The bands are the shared rule, not a second one.</b> Which band a fraction reads as is
    /// <see cref="CatchFillMath.BandFor"/> — the same pinning the pail and the deck tray use, and itself
    /// the rig's own <c>FRAC</c> table. EMPTY only when truly empty, BRIM only when truly full. There is
    /// no threshold table here and there must not be one: a second opinion about what "half" means is
    /// how the drawn catch and the counted catch drift apart.</para>
    ///
    /// <para><b>What it does NOT draw.</b> Only the four bands with a non-zero fraction are baked, so an
    /// empty hod is simply back+front with nothing between them. And the hod bakes ONE catch — the clam;
    /// it is the clam dig's basket, and <c>CatchKit2.heap</c> heaps shellfish only. A hod handed
    /// something else still fills (a heap of clams is what the art has), which is honest for the flat
    /// and would be wrong for a general container; the day the hod carries mussels it gains a kind
    /// segment in its stem, not a guess here.</para>
    ///
    /// <para><b>Budget (rule 7).</b> Event-driven through <see cref="HoldCatchFillSource"/>: a refresh
    /// assigns at most three sprite references and one colour. No per-frame work, no allocation, no
    /// <c>Find</c> — the sprite tables are serialized arrays indexed by arithmetic.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HodFillPresenter : MonoBehaviour, ICatchFillTarget
    {
        /// <summary>How many facing rows the hod rig bakes (ADR-0006's eight).</summary>
        public const int Facings = 8;

        [SerializeField, Tooltip("The catch art table — the speciesId → kind map the bridge uses. The " +
                                 "SAME asset the pail and tote read, so a clam is a clam everywhere.")]
        private CatchItemLibrary _library;

        [SerializeField, Tooltip("Hod2_back_d0..d7 — the far half of the wire, drawn under the catch.")]
        private Sprite[] _backSprites = new Sprite[Facings];

        [SerializeField, Tooltip("Hod2_front_d0..d7 — the near half, drawn over the catch so the shells " +
                                 "show THROUGH the mesh.")]
        private Sprite[] _frontSprites = new Sprite[Facings];

        [SerializeField, Tooltip("Hod2_heap_<band>_d<dir> flattened as [band × 8 + dir], band in " +
                                 "few/half/full/brim order — 32 entries when fully baked. A missing " +
                                 "entry draws no heap rather than the wrong one, so a half-imported " +
                                 "kit still draws an honest empty basket.")]
        private Sprite[] _heapSprites = new Sprite[4 * Facings];

        [SerializeField, Range(0, Facings - 1),
         Tooltip("Facing row the hod is drawn at (row d = heading 45°·d, the baked-sheet convention).")]
        private int _direction;

        [SerializeField, Tooltip("The far half's renderer. Auto-resolved from a child named 'Back'.")]
        private SpriteRenderer _backRenderer;

        [SerializeField, Tooltip("The catch layer's renderer, between the two halves. Auto-resolved " +
                                 "from a child named 'Heap'.")]
        private SpriteRenderer _heapRenderer;

        [SerializeField, Tooltip("The near half's renderer. Auto-resolved from a child named 'Front'.")]
        private SpriteRenderer _frontRenderer;

        private readonly List<string> _kinds = new List<string>();
        private float _fill01;
        private float _spoil;
        private bool _dirty = true;

        /// <inheritdoc/>
        public CatchItemLibrary Library => _library;

        /// <summary>The band the hod is drawing right now — what a test asserts instead of reading
        /// pixels, and what the owner sees in the inspector while tuning the hod's capacity.</summary>
        public CatchFillBand Band { get; private set; } = CatchFillBand.Empty;

        /// <summary>How many catch units the hod was last told it holds — the count behind the band.</summary>
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
            if (_dirty) RebuildNow();
        }

        /// <summary>
        /// Choose and apply the three sprites NOW rather than at the next <c>LateUpdate</c>. Public for
        /// the same reason <see cref="BucketFillPresenter.RebuildNow"/> is: EditMode never runs a frame,
        /// so a test that has just stored a clam has no other way to settle the picture.
        /// </summary>
        public void RebuildNow()
        {
            _dirty = false;
            Resolve();

            Band = CatchFillMath.BandFor(_fill01, _kinds.Count);

            if (_backRenderer != null) _backRenderer.sprite = LayerSprite(_backSprites, _direction);
            if (_frontRenderer != null) _frontRenderer.sprite = LayerSprite(_frontSprites, _direction);

            if (_heapRenderer == null) return;
            Sprite heap = HeapSpriteFor(Band, _direction);
            _heapRenderer.sprite = heap;
            _heapRenderer.enabled = heap != null;
            // Only the CATCH rots. The galvanised wire it sits in does not, so the spoil tint lands on
            // the heap layer alone — the pail tints its whole sprite only because its catch and its
            // container are one baked picture.
            _heapRenderer.color = CatchSpoilMath.RendererTint(_spoil);
        }

        /// <summary>
        /// The baked heap sprite for one (band, facing), or null when this band draws no heap — which is
        /// every band at <see cref="CatchFillBand.Empty"/>, and any band whose sheet has not been wired.
        /// Public so the index arithmetic is testable on its own: a table indexed wrong draws a
        /// plausible hod at the wrong fullness, exactly the kind of wrong nobody notices in play.
        /// </summary>
        public Sprite HeapSpriteFor(CatchFillBand band, int direction)
        {
            if (band == CatchFillBand.Empty) return null;

            int bandIndex = System.Array.IndexOf(CatchFillMath.FilledBands, band);
            if (bandIndex < 0) return null;

            int d = ((direction % Facings) + Facings) % Facings;
            int i = bandIndex * Facings + d;
            return _heapSprites != null && i < _heapSprites.Length ? _heapSprites[i] : null;
        }

        static Sprite LayerSprite(Sprite[] byDir, int direction)
        {
            if (byDir == null) return null;
            int d = ((direction % Facings) + Facings) % Facings;
            return d < byDir.Length ? byDir[d] : null;
        }

        /// <summary>Wire the art in one call (the region builders / tests) — the Configure seam used
        /// across the lane, because a builder sets serialized fields it cannot see and EditMode never
        /// runs a builder.</summary>
        public void Configure(CatchItemLibrary library, Sprite[] backByDir, Sprite[] frontByDir,
                              Sprite[] heapsFlattened)
        {
            if (library != null) _library = library;
            if (backByDir != null) _backSprites = backByDir;
            if (frontByDir != null) _frontSprites = frontByDir;
            if (heapsFlattened != null) _heapSprites = heapsFlattened;
            _dirty = true;
        }

        /// <summary>Wire the three renderers in one call, for a builder assembling the hod object.</summary>
        public void ConfigureRenderers(SpriteRenderer back, SpriteRenderer heap, SpriteRenderer front)
        {
            if (back != null) _backRenderer = back;
            if (heap != null) _heapRenderer = heap;
            if (front != null) _frontRenderer = front;
            _dirty = true;
        }

        private void Resolve()
        {
            if (_backRenderer == null) _backRenderer = FindRenderer("Back");
            if (_heapRenderer == null) _heapRenderer = FindRenderer("Heap");
            if (_frontRenderer == null) _frontRenderer = FindRenderer("Front");
        }

        private SpriteRenderer FindRenderer(string childName)
        {
            Transform t = transform.Find(childName);
            return t != null ? t.GetComponent<SpriteRenderer>() : null;
        }
    }
}
