using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>What is in a chest freezer, lying in it</b> (fish art pass 2, plan 4a): each frozen catch is drawn as
    /// its own deck-lay sprite at its own size rung (<see cref="CatchItemLibrary.SizedKindFor(string, float)"/>:
    /// a big cod is big here too), with the frost tint (<see cref="GameConfig.FrozenFishTint"/>).
    ///
    /// <para><b>With the baked chest</b> (a <see cref="ChestFreezerArt"/> beside it whose Def names a sheet) the
    /// fish show only once the lid is up, laid by <see cref="FreezerLayout.Pack"/> inside the Def's contents
    /// opening (the mouth between the chest's rims), newest first, and drawn between the chest and its near
    /// layer so its front rim covers the front row: they lie IN the chest, never over its outline. More fish
    /// than fit lie under the ones shown, which the chest's full bed stands for.</para>
    ///
    /// <para><b>Without it</b> (a bare marker) they lie on the plain slot grid in <see cref="GameConfig"/>
    /// (<see cref="GameConfig.FreezerFishColumns"/>, rows, origin and pitch) while he stands within
    /// <see cref="GameConfig.FreezerFishShowRangeMeters"/>.</para>
    ///
    /// <para>Presentation only: reads the freezer's items, writes nothing (no save change). The fish are re-laid
    /// only when what is in the freezer changes (a signature over the items); a frame allocates nothing once
    /// its renderers are made.</para>
    /// </summary>
    public sealed class FreezerContentsPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("The catch art table the fish are drawn from. Empty = the shared one.")]
        private CatchItemLibrary _art;

        [SerializeField, Tooltip("Plain marker only: sorting orders above the marker the first row draws at; each " +
                                 "row nearer the front draws one higher.")]
        private int _sortingAbove = 1;

        [SerializeField, Tooltip("In the baked chest: how much nearer the camera each row toward the front sits, so " +
                                 "a front fish draws over the one behind it at the same order.")]
        private float _rowDepthStep = 0.002f;

        private IHold _freezer;
        private ChestFreezerArt _chestArt;
        private SpriteRenderer _chest;
        private Transform _lay;
        private readonly List<SpriteRenderer> _slots = new List<SpriteRenderer>();
        private readonly List<float> _lengths = new List<float>();
        private readonly List<int> _rows = new List<int>();
        private readonly List<float> _laidLengths = new List<float>();
        private FreezerLayout.Placement[] _placements = new FreezerLayout.Placement[0];
        private int _laidSignature;
        private bool _laid;
        private bool _laidInChest;

        /// <summary>How many fish are drawn now.</summary>
        public int ShownCount { get; private set; }

        /// <summary>The slot renderers made so far. For tests and plates.</summary>
        public IReadOnlyList<SpriteRenderer> Slots => _slots;

        /// <summary>The length (metres) shown fish <paramref name="slot"/> was laid at in the chest (0 on the
        /// plain marker). For tests and plates.</summary>
        public float LaidLengthMetres(int slot) => slot >= 0 && slot < _laidLengths.Count ? _laidLengths[slot] : 0f;

        private void Awake()
        {
            _freezer = GetComponent<IHold>();
            _chestArt = GetComponent<ChestFreezerArt>();
            _chest = GetComponent<SpriteRenderer>();
        }

        private void OnDisable() => HideAll();

        private bool _artLooked;

        private bool InChest => ArtNow() != null && _chestArt.Def != null && !string.IsNullOrEmpty(_chestArt.Def.SheetStem);

        // The chest art may be added after this Awake (a freezer dressing itself): look once more on first use.
        private ChestFreezerArt ArtNow()
        {
            if (_chestArt == null && !_artLooked) { _artLooked = true; _chestArt = GetComponent<ChestFreezerArt>(); }
            return _chestArt;
        }

        private void LateUpdate()
        {
            GameConfig config = GameServices.Config;
            bool inChest = InChest;
            bool show;
            if (inChest) show = _chestArt.IsOpen;
            else
            {
                Transform player = GameServices.PlayerTransform;
                show = config != null && player != null &&
                       Near(player.position, transform.position, config.FreezerFishShowRangeMeters);
            }
            if (!show || _freezer == null)
            {
                HideAll();
                return;
            }
            IReadOnlyList<CatchItem> items = _freezer.Items;
            int signature = Signature(items);
            if (!_laid || signature != _laidSignature || inChest != _laidInChest)
            {
                if (inChest) LayInChest(config, items, signature);
                else if (config != null) Lay(config, items, signature);
            }
            Sort();
        }

        /// <summary>True when <paramref name="a"/> is within <paramref name="range"/> of <paramref name="b"/>
        /// on the ground plane.</summary>
        public static bool Near(Vector3 a, Vector3 b, float range)
            => range > 0f && ((Vector2)(a - b)).sqrMagnitude <= range * range;

        /// <summary>
        /// The slot <paramref name="index"/> sits on, metres from the freezer's pivot: row by row from the
        /// back-left, <paramref name="columns"/> to a row.
        /// </summary>
        public static Vector2 SlotPoint(int index, int columns, Vector2 origin, Vector2 pitch)
        {
            int c = Mathf.Max(1, columns);
            return origin + new Vector2((index % c) * pitch.x, -(index / c) * pitch.y);
        }

        /// <summary>Which items are shown, as the index of the first one: the newest that fit the slots.</summary>
        public static int FirstShown(int count, int slots) => Mathf.Max(0, count - Mathf.Max(0, slots));

        /// <summary>A fish's drawn length in metres at scale 1: its rung's measured length, else 0.6 of its cell
        /// (the cell pads the fish).</summary>
        public static float DrawnLengthMetres(Sprite sprite, int measuredPx)
        {
            if (sprite == null) return 0f;
            float ppu = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 32f;
            float px = measuredPx > 0 ? Mathf.Min(measuredPx, sprite.rect.width) : sprite.rect.width * 0.6f;
            return px / ppu;
        }

        /// <summary>The deck lay a fish in the chest uses: one of the Def's side-on lays (head left, head right),
        /// alternating, so none lies across a rim.</summary>
        public static int SideOnLay(int index, FreezerDef def)
        {
            int n = def != null ? Mathf.Max(1, def.FishSideOnLays) : 1;
            return ((index % n) + n) % n;
        }

        private static int Signature(IReadOnlyList<CatchItem> items)
        {
            if (items == null) return 0;
            unchecked
            {
                int h = 17 + items.Count;
                for (int i = 0; i < items.Count; i++)
                {
                    h = h * 31 + (items[i].SpeciesId != null ? items[i].SpeciesId.GetHashCode() : 0);
                    h = h * 31 + items[i].WeightKg.GetHashCode();
                }
                return h;
            }
        }

        // ---- in the baked chest -----------------------------------------------------------------------

        private void LayInChest(GameConfig config, IReadOnlyList<CatchItem> items, int signature)
        {
            _laid = true;
            _laidInChest = true;
            _laidSignature = signature;
            FreezerDef def = _chestArt.Def;
            CatchItemLibrary art = FishArtLibrary.ResolveCatchArt(_art, FishArtLibrary.Shared);
            int count = items != null ? items.Count : 0;
            EnsureLay();
            _lengths.Clear();
            for (int i = 0; i < count; i++)
            {
                Sprite s = art != null ? art.SpriteFor(art.SizedKindFor(items[i].SpeciesId, items[i].WeightKg), SideOnLay(i, def)) : null;
                string kind = art != null ? art.SizedKindFor(items[i].SpeciesId, items[i].WeightKg) : null;
                _lengths.Add(art != null ? DrawnLengthMetres(s, art.HeldLengthPxFor(kind)) * Mathf.Max(1f, def.FishLaySlotOverHeldLength) : 0f);
            }
            if (_placements.Length < count) _placements = new FreezerLayout.Placement[count];
            int laid = FreezerLayout.Pack(_lengths, count, def.ContentsOpeningMetres, def.FishRowPitchMetres,
                                          def.FishDrawScale, def.FishHeightOfLength, def.FishGapMetres, _placements);
            Color tint = config != null ? config.FrozenFishTint : Color.white;
            _rows.Clear();
            _laidLengths.Clear();
            int shown = 0;
            for (int k = 0; k < laid; k++)
            {
                FreezerLayout.Placement p = _placements[k];
                CatchItem item = items[p.Item];
                Sprite sprite = art.SpriteFor(art.SizedKindFor(item.SpeciesId, item.WeightKg), SideOnLay(p.Item, def));
                if (sprite == null) continue;
                SpriteRenderer sr = Slot(shown);
                sr.transform.localPosition = new Vector3(p.Centre.x, p.Centre.y, -_rowDepthStep * (p.Row + 1));
                sr.transform.localScale = new Vector3(p.Scale, p.Scale, 1f);
                sr.sprite = sprite;
                sr.color = tint;
                sr.enabled = true;
                _rows.Add(p.Row);
                _laidLengths.Add(p.Size.x);
                shown++;
            }
            for (int i = shown; i < _slots.Count; i++) _slots[i].enabled = false;
            ShownCount = shown;
        }

        // ---- on a plain marker ------------------------------------------------------------------------

        private void Lay(GameConfig config, IReadOnlyList<CatchItem> items, int signature)
        {
            _laid = true;
            _laidInChest = false;
            _laidSignature = signature;
            CatchItemLibrary art = FishArtLibrary.ResolveCatchArt(_art, FishArtLibrary.Shared);
            int slots = Mathf.Max(1, config.FreezerFishColumns) * Mathf.Max(1, config.FreezerFishRows);
            int count = items != null ? items.Count : 0;
            int first = FirstShown(count, slots);
            EnsureLay();
            _rows.Clear();
            _laidLengths.Clear();
            int shown = 0;
            for (int i = first; i < count && art != null; i++)
            {
                CatchItem item = items[i];
                string kind = art.SizedKindFor(item.SpeciesId, item.WeightKg);
                Sprite sprite = art.SpriteFor(kind, i);
                if (sprite == null) continue;
                SpriteRenderer sr = Slot(shown);
                Vector2 p = SlotPoint(shown, config.FreezerFishColumns, config.FreezerFishOriginMeters,
                                      config.FreezerFishPitchMeters);
                sr.transform.localPosition = new Vector3(p.x, p.y, 0f);
                sr.transform.localScale = Vector3.one;
                sr.sprite = sprite;
                sr.color = config.FrozenFishTint;
                sr.enabled = true;
                _rows.Add(shown / Mathf.Max(1, config.FreezerFishColumns));
                shown++;
            }
            for (int i = shown; i < _slots.Count; i++) _slots[i].enabled = false;
            ShownCount = shown;
        }

        // The freezer object may be scaled (the greybox marker); the fish ride a child that undoes that scale,
        // so a laid point and a fish are both true metres.
        private void EnsureLay()
        {
            if (_lay == null)
            {
                var go = new GameObject("FreezerContents") { hideFlags = HideFlags.DontSave };
                go.layer = gameObject.layer;
                _lay = go.transform;
                _lay.SetParent(transform, false);
            }
            Vector3 s = transform.lossyScale;
            _lay.localScale = new Vector3(s.x != 0f ? 1f / s.x : 1f, s.y != 0f ? 1f / s.y : 1f, 1f);
        }

        private SpriteRenderer Slot(int i)
        {
            while (_slots.Count <= i)
            {
                var go = new GameObject("FrozenFish") { hideFlags = HideFlags.DontSave };
                go.layer = gameObject.layer;
                go.transform.SetParent(_lay, false);
                _slots.Add(go.AddComponent<SpriteRenderer>());
            }
            return _slots[i];
        }

        private void Sort()
        {
            bool chest = _laidInChest && _chestArt != null;
            if (!chest && _chest == null) return;
            int layer = chest ? _chestArt.SortingLayerId : _chest.sortingLayerID;
            for (int i = 0; i < ShownCount; i++)
            {
                SpriteRenderer sr = _slots[i];
                int want = chest ? _chestArt.ContentsOrder : _chest.sortingOrder + _sortingAbove + _rows[i];
                if (sr.sortingLayerID != layer) sr.sortingLayerID = layer;
                if (sr.sortingOrder != want) sr.sortingOrder = want;
            }
        }

        private void HideAll()
        {
            if (!_laid && ShownCount == 0) return;
            _laid = false;
            ShownCount = 0;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != null) _slots[i].enabled = false;
        }
    }
}
