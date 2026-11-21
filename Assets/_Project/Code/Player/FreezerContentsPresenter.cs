using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>What is in Ginny's freezer, lying in it</b> (fish art pass 2, plan 4a): while the player stands at
    /// the freezer (<see cref="GameConfig.FreezerFishShowRangeMeters"/>, as if the lid were open), each
    /// frozen catch is drawn on a slot of the freezer's slot grid, as its own deck-lay sprite at its own
    /// size rung (<see cref="CatchItemLibrary.SizedKindFor(string, float)"/>: a big cod is big here too),
    /// with the frost tint (<see cref="GameConfig.FrozenFishTint"/>). Walk away and the draw calls go.
    ///
    /// <para><b>The slot points are data</b> (<see cref="GameConfig.FreezerFishColumns"/>, rows, origin and
    /// pitch, in metres from the freezer's pivot): the freezer is a greybox chest today, so a grid on its
    /// lid stands in for a rig's published slots, and a baked freezer rig replaces the numbers, not this.
    /// More fish than slots: the newest are shown.</para>
    ///
    /// <para>Presentation only: reads the freezer's items, writes nothing (no save change). The slots are
    /// re-laid only when what is in the freezer changes (a signature over the items); a frame at the
    /// freezer costs a loop over at most the freezer's capacity and allocates nothing once its slots are made.</para>
    /// </summary>
    [RequireComponent(typeof(GinnyFreezer))]
    public sealed class FreezerContentsPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("The catch art table the fish are drawn from. Empty = the shared one.")]
        private CatchItemLibrary _art;

        [SerializeField, Tooltip("Sorting orders above the freezer's own sprite the first row draws at; each row " +
                                 "nearer the front draws one higher.")]
        private int _sortingAbove = 1;

        private GinnyFreezer _freezer;
        private SpriteRenderer _chest;
        private Transform _lay;
        private readonly List<SpriteRenderer> _slots = new List<SpriteRenderer>();
        private int _laidSignature;
        private bool _laid;

        /// <summary>How many fish are drawn now.</summary>
        public int ShownCount { get; private set; }

        /// <summary>The slot renderers made so far. For tests and plates.</summary>
        public IReadOnlyList<SpriteRenderer> Slots => _slots;

        private void Awake()
        {
            _freezer = GetComponent<GinnyFreezer>();
            _chest = GetComponent<SpriteRenderer>();
        }

        private void OnDisable() => HideAll();

        private void LateUpdate()
        {
            GameConfig config = GameServices.Config;
            Transform player = GameServices.PlayerTransform;
            if (config == null || _freezer == null || player == null || !Near(player.position, transform.position,
                                                                                 config.FreezerFishShowRangeMeters))
            {
                HideAll();
                return;
            }
            IReadOnlyList<CatchItem> items = _freezer.Items;
            int signature = Signature(items);
            if (!_laid || signature != _laidSignature) Lay(config, items, signature);
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

        private void Lay(GameConfig config, IReadOnlyList<CatchItem> items, int signature)
        {
            _laid = true;
            _laidSignature = signature;
            CatchItemLibrary art = FishArtLibrary.ResolveCatchArt(_art, FishArtLibrary.Shared);
            int slots = Mathf.Max(1, config.FreezerFishColumns) * Mathf.Max(1, config.FreezerFishRows);
            int count = items != null ? items.Count : 0;
            int first = FirstShown(count, slots);
            EnsureLay();
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
                sr.sprite = sprite;
                sr.color = config.FrozenFishTint;
                sr.enabled = true;
                shown++;
            }
            for (int i = shown; i < _slots.Count; i++) _slots[i].enabled = false;
            ShownCount = shown;
        }

        // The freezer object is scaled to its greybox footprint; the fish ride a child that undoes that scale,
        // so a slot point and a fish are both true metres.
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
            if (_chest == null) return;
            int columns = GameServices.Config != null ? Mathf.Max(1, GameServices.Config.FreezerFishColumns) : 1;
            for (int i = 0; i < ShownCount; i++)
            {
                SpriteRenderer sr = _slots[i];
                int want = _chest.sortingOrder + _sortingAbove + i / columns;
                if (sr.sortingLayerID != _chest.sortingLayerID) sr.sortingLayerID = _chest.sortingLayerID;
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
