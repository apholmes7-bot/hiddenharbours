using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Draws a container's fill as REAL catch — baked item sprites placed on the container rig's
    /// projected slot points, back-to-front, clipped to the projected rim opening — so the tote IS
    /// the inventory readout (the fishing kit's diegetic rule: no icons; a tote of cod shows cod).
    ///
    /// <para><b>Plain data in.</b> This component knows nothing about holds or fishing: callers
    /// hand it a kind list + fill fraction (<see cref="SetContents"/>), a facing row
    /// (<see cref="SetDirection"/>) and a spoil level (<see cref="SetSpoil"/>). The Core-facing
    /// bridge is its sibling <see cref="HoldCatchFillSource"/>. Geometry comes from the baked
    /// <c>CatchStorageAnchors.json</c> (rig-derived data, never hand-typed), art from
    /// <see cref="CatchItemLibrary"/>, and the heap layout from <see cref="CatchFillMath"/> —
    /// seeded and MONOTONIC, so a growing fill never rearranges the catch already showing and the
    /// same seed reproduces the same heap every session (rule 5, applied to the look).</para>
    ///
    /// <para><b>Budget (rule 7).</b> A brim tote is ~32 tiny sprites: renderers are pooled children
    /// created once up to slot capacity and toggled, never destroyed; rebuilds happen only when an
    /// input actually changed (event-driven — LateUpdate does nothing while clean); steady-state
    /// refreshes allocate nothing (reused item/kind buffers). All items share the default sprite
    /// material with the rest of the pixel art, so they batch with their container.</para>
    ///
    /// <para><b>Clipping.</b> A <see cref="SpriteMask"/> child carries the baked per-direction
    /// opening mask (<c>ToteMask_d&lt;dir&gt;_f0</c>) with a custom sorting range bracketing only
    /// the item orders, so the front wall correctly occludes the low layers without touching any
    /// other sprite in the scene. No mask wired (pre-bake) = items draw unclipped rather than not
    /// at all.</para>
    ///
    /// <para><b>Spoil.</b> Applied as the recipe's uniform green multiply
    /// (<see cref="CatchSpoilMath.RendererTint"/>) — the per-pixel mottle and the rot motes are
    /// deferred (see <see cref="CatchSpoilMath"/>). Spoil is a published visual input; the
    /// gameplay freshness clock wires in later.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CatchFillRenderer : MonoBehaviour
    {
        [SerializeField, Tooltip("The catch art table: kind → baked lay-variant sprites, speciesId → kind.")]
        private CatchItemLibrary _library;

        [SerializeField, Tooltip("CatchStorageAnchors.json from the storage bake (Hidden Harbours ▸ " +
                                 "Art ▸ Bake Catch Storage Kit). Slot points + opening per facing row.")]
        private TextAsset _anchorsJson;

        [SerializeField, Tooltip("The 8 opening-mask slices (ToteMask_d0_f0 … d7), one per facing " +
                                 "row. Optional — unclipped without them.")]
        private Sprite[] _openingMaskByDir = new Sprite[8];

        [SerializeField, Range(0, 7), Tooltip("Facing row of the container sprite this fill sits in " +
                                              "(row d = heading 45°·d, the baked-sheet convention).")]
        private int _direction;

        [SerializeField, Tooltip("Heap seed. Same seed = the same heap every session — change it " +
                                 "only to re-scatter this one container's look.")]
        private int _seed = 7;

        [SerializeField, Range(0f, 1f), Tooltip("Rot 0..1 — green-shifts the drawn catch. Visual " +
                                                "input only; the freshness clock wires in later.")]
        private float _spoil;

        [SerializeField, Tooltip("Pixels per metre of the baked art — the project's locked scale.")]
        private float _pixelsPerUnit = 32f;

        [SerializeField, Tooltip("Sorting order of the lowest item, relative to this object's own " +
                                 "SpriteRenderer order (items stack upward from here).")]
        private int _sortingOrderOffset = 1;

        // ---- state ---------------------------------------------------------------------------

        private readonly List<string> _kinds = new List<string>();          // actual catch, landed order
        private readonly List<CatchFillItem> _items = new List<CatchFillItem>();
        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private float _fill01;
        private bool _dirty = true;
        private CatchStorageAnchors _anchors;
        private bool _anchorsParsed;
        private SpriteMask _mask;

        /// <summary>The art table, exposed so the hold bridge maps species with the same data.</summary>
        public CatchItemLibrary Library => _library;

        // ---- inputs (all plain data; each marks dirty, work happens once in LateUpdate) --------

        /// <summary>Hand over the container's contents: visual kinds in landed order plus the
        /// 0..1 fullness. The list is COPIED — callers may reuse theirs.</summary>
        public void SetContents(IReadOnlyList<string> kinds, float fill01)
        {
            _kinds.Clear();
            if (kinds != null)
                for (int i = 0; i < kinds.Count; i++)
                    _kinds.Add(kinds[i]);
            _fill01 = fill01;
            _dirty = true;
        }

        /// <summary>The container sprite's facing row (0..7). Boats feed their drawn facing here
        /// when the container rides a deck; static props leave the serialized value.</summary>
        public void SetDirection(int direction)
        {
            int d = ((direction % 8) + 8) % 8;
            if (d == _direction) return;
            _direction = d;
            _dirty = true;
        }

        /// <summary>Rot 0..1 — the published spoil input (see class remarks).</summary>
        public void SetSpoil(float spoil01)
        {
            float s = Mathf.Clamp01(spoil01);
            if (Mathf.Approximately(s, _spoil)) return;
            _spoil = s;
            _dirty = true;
        }

        private void OnEnable() => _dirty = true;

        private void OnValidate() => _dirty = true;   // owner scrubs the inspector dials

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            Rebuild();
        }

        // ---- the rebuild (event-time only) -----------------------------------------------------

        private void Rebuild()
        {
            CatchContainerAnchors tote = ResolveToteAnchors();
            if (tote == null || tote.byDir == null || tote.byDir.Length == 0 || _library == null)
            {
                HideAll();
                return;
            }

            CatchDirAnchors dir = tote.byDir[_direction % tote.byDir.Length];
            int capacity = dir.slots != null ? dir.slots.Length : 0;
            int visible = Mathf.Min(CatchFillMath.VisibleCount(_fill01, capacity), _kinds.Count);

            CatchFillMath.AppearancesFor(_kinds, _seed, _items);
            EnsurePool(visible);
            Color tint = CatchSpoilMath.RendererTint(_spoil);
            int baseOrder = BaseSortingOrder();

            int drawn = 0;
            for (int i = 0; i < visible; i++)
            {
                CatchFillItem item = _items[i];
                Sprite sprite = _library.SpriteFor(item.Kind, item.Variant);
                if (sprite == null) continue;   // kind not wired yet — skip, never draw a blank

                SpriteRenderer r = _pool[drawn];
                r.sprite = sprite;
                r.color = tint;
                // Slots are pre-sorted floor-layer-first, back-to-front: index order IS draw order.
                r.sortingOrder = baseOrder + drawn;
                CatchAnchorPoint slot = dir.slots[i];
                // Rig offsets are screen px from the pivot, screen-down-positive; Unity y is up.
                r.transform.localPosition = new Vector3(slot.dx / _pixelsPerUnit,
                                                        -slot.dy / _pixelsPerUnit, 0f);
                float s = item.Scale;
                r.transform.localScale = new Vector3(s, s, 1f);
                r.enabled = true;
                drawn++;
            }
            for (int i = drawn; i < _pool.Count; i++) _pool[i].enabled = false;

            UpdateMask(baseOrder, capacity);
        }

        private CatchContainerAnchors ResolveToteAnchors()
        {
            if (!_anchorsParsed)
            {
                _anchorsParsed = true;
                _anchors = _anchorsJson != null ? CatchStorageAnchors.Parse(_anchorsJson.text) : null;
                if (_anchors == null && _anchorsJson != null)
                    Debug.LogWarning($"[{nameof(CatchFillRenderer)}] '{_anchorsJson.name}' did not " +
                                     "parse as CatchStorageAnchors — fill disabled.", this);
            }
            return _anchors?.tote;
        }

        private int BaseSortingOrder()
        {
            // Sit just above the container's own sprite so the catch reads inside it; the opening
            // mask (not the order) is what lets the front wall occlude the low layers.
            var container = GetComponent<SpriteRenderer>();
            return (container != null ? container.sortingOrder : 0) + _sortingOrderOffset;
        }

        private void EnsurePool(int count)
        {
            var container = GetComponent<SpriteRenderer>();
            while (_pool.Count < count)
            {
                var go = new GameObject($"CatchItem{_pool.Count}");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                if (container != null) r.sortingLayerID = container.sortingLayerID;
                r.maskInteraction = SpriteMaskInteraction.None;   // set for real in UpdateMask
                _pool.Add(r);
            }
        }

        private void UpdateMask(int baseOrder, int capacity)
        {
            Sprite maskSprite = _openingMaskByDir != null && _direction < _openingMaskByDir.Length
                ? _openingMaskByDir[_direction]
                : null;

            if (maskSprite == null)
            {
                if (_mask != null) _mask.enabled = false;
                SetItemMasking(SpriteMaskInteraction.None);
                return;
            }

            if (_mask == null)
            {
                var go = new GameObject("OpeningMask");
                go.transform.SetParent(transform, false);
                _mask = go.AddComponent<SpriteMask>();
                _mask.isCustomRangeActive = true;
            }
            _mask.sprite = maskSprite;
            // Bracket exactly the item orders so no other sprite in the scene is clipped.
            var container = GetComponent<SpriteRenderer>();
            int layer = container != null ? container.sortingLayerID : 0;
            _mask.backSortingLayerID = layer;
            _mask.frontSortingLayerID = layer;
            _mask.backSortingOrder = baseOrder - 1;
            _mask.frontSortingOrder = baseOrder + capacity;
            _mask.enabled = true;
            SetItemMasking(SpriteMaskInteraction.VisibleInsideMask);
        }

        private void SetItemMasking(SpriteMaskInteraction interaction)
        {
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i].maskInteraction != interaction)
                    _pool[i].maskInteraction = interaction;
        }

        private void HideAll()
        {
            for (int i = 0; i < _pool.Count; i++) _pool[i].enabled = false;
            if (_mask != null) _mask.enabled = false;
        }
    }
}
