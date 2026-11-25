using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>The rule a sold-out card follows, kept pure so a test can walk it through a supply cycle.</summary>
    public static class SoldOutSignRule
    {
        /// <summary>True when the card stands on <paramref name="lineId"/>'s slot at <paramref name="now"/>:
        /// the shelves know the line and it holds no items. Unknown shelves or lines show no card (the store
        /// then looks as it always has).</summary>
        public static bool IsSoldOut(IIslandShelvesView shelves, string lineId, double now)
            => shelves != null && !string.IsNullOrEmpty(lineId) && shelves.HasLine(lineId)
               && shelves.CountAt(lineId, now) <= 0;
    }

    /// <summary>
    /// <b>The sold-out cards on one store's shelves</b> (store sold-out slice). One small card per slot of a
    /// <see cref="ShopShelfSlotsDef"/>, standing where the product normally sits; a card shows while the
    /// player is inside on the ground floor and the slot's shelf line is empty, and goes away when stock
    /// returns. The count is read through the Core <see cref="IslandShelvesDirectory"/> (never the Economy
    /// class, rule 4), and only on a change: walking in, an <see cref="IslandShelvesChanged"/> for this shop,
    /// or the shelves appearing. The per-frame cost is two bool reads; nothing allocates after setup.
    ///
    /// <para><b>Buying at the shelf (slice 1).</b> A slot may also carry an empty-shelf cover drawn over the
    /// room picture (so the painted goods are gone when the real ones are), the goods themselves (shown while
    /// the line has stock), and a <see cref="ShelfGood"/> the player buys from where it sits. All three follow
    /// the same change-only refresh as the cards.</para>
    /// </summary>
    public sealed class SoldOutSigns : MonoBehaviour
    {
        private ShopShelfSlotsDef _def;
        private BuildingInterior _building;
        private SpriteRenderer[] _cards = new SpriteRenderer[0];
        private SpriteRenderer[] _covers = new SpriteRenderer[0];
        private SpriteRenderer[] _goods = new SpriteRenderer[0];
        private ShelfGood[] _shelfGoods = new ShelfGood[0];
        private ShelfSection[] _sections = new ShelfSection[0];
        private GameObject[] _made = new GameObject[0];
        private GameObject[] _madeSections = new GameObject[0];
        private System.Func<bool> _insideNow;
        private bool _wasInside;
        private bool _dirty = true;
        private bool _hadShelves;

        /// <summary>The cards, one per slot in the Def's order (a test reads which are showing).</summary>
        public SpriteRenderer[] Cards => _cards;

        /// <summary>The empty-shelf covers, one per slot in the Def's order; null where a slot has none.</summary>
        public SpriteRenderer[] Covers => _covers;

        /// <summary>The goods drawn on each slot while it has stock; null where a slot has none.</summary>
        public SpriteRenderer[] Goods => _goods;

        /// <summary>The goods the player buys at the shelf (buying at the shelf, slice 1), one per slot in the
        /// Def's order; null where a slot sells nothing itself.</summary>
        public ShelfGood[] ShelfGoods => _shelfGoods;

        /// <summary>The shelf sections (store sections), one per <see cref="ShopShelfSlotsDef.Sections"/> entry
        /// in the Def's order; their goods are the slots' <see cref="ShelfGoods"/>, sold only through them.</summary>
        public ShelfSection[] Sections => _sections;

        /// <summary>Builds the cards for <paramref name="def"/> on <paramref name="building"/>. Idempotent.</summary>
        public void Configure(ShopShelfSlotsDef def, BuildingInterior building)
        {
            _def = def;
            _building = building;
            _insideNow ??= Inside;
            DestroyAll(_made);
            DestroyAll(_madeSections);
            int n = def != null && def.Slots != null ? def.Slots.Count : 0;
            _cards = new SpriteRenderer[n];
            _covers = new SpriteRenderer[n];
            _goods = new SpriteRenderer[n];
            _shelfGoods = new ShelfGood[n];
            _made = new GameObject[n];
            _sections = new ShelfSection[0];
            _madeSections = new GameObject[0];
            if (n == 0) return;

            SpriteRenderer room = building != null ? building.RoomRenderer : null;
            for (int i = 0; i < n; i++)
            {
                ShopShelfSlot slot = def.Slots[i];
                var go = new GameObject("SoldOut_" + (slot != null ? slot.Id : i.ToString()));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = slot != null ? (Vector3)slot.Offset : Vector3.zero;
                _made[i] = go;
                _cards[i] = Layer(go, slot != null ? slot.CardOr(def.SoldOutSign) : def.SoldOutSign, room,
                                  def.SortingOrderAboveRoom);
                if (slot == null) continue;
                if (slot.Cover != null) _covers[i] = Layer(Child(go, "Cover"), slot.Cover, room, def.CoverSortingOrderAboveRoom);
                if (slot.Goods != null) _goods[i] = Layer(Child(go, "Goods"), slot.Goods, room, def.GoodsSortingOrderAboveRoom);
                if (!string.IsNullOrEmpty(slot.ListingId))
                {
                    GameObject at = Child(go, "Good");
                    at.SetActive(false);   // register only once wired
                    var good = at.AddComponent<ShelfGood>();
                    good.Configure(def.Id + "." + slot.Id, def.ShopId, slot.ListingId, def.ReachMeters,
                                   slot.NeedsFreeHand, _insideNow, slot.IntoBag ? def.Bag : null);
                    // A good its section sells is handed over before it can register on its own.
                    if (def.SectionOf(slot.Id) != null) good.PutInSection();
                    at.SetActive(true);
                    _shelfGoods[i] = good;
                }
            }
            BuildSections(def);
            _dirty = true;
        }

        // One reachable place per section, at its own offset, selling its slots' goods through the chooser.
        private void BuildSections(ShopShelfSlotsDef def)
        {
            int m = def.Sections != null ? def.Sections.Count : 0;
            _sections = new ShelfSection[m];
            _madeSections = new GameObject[m];
            for (int s = 0; s < m; s++)
            {
                ShopShelfSection section = def.Sections[s];
                if (section == null) continue;
                int count = 0;
                ShelfGood[] goods = new ShelfGood[section.SlotIds != null ? section.SlotIds.Count : 0];
                for (int k = 0; k < goods.Length; k++)
                {
                    ShelfGood good = GoodOfSlot(def, section.SlotIds[k]);
                    if (good != null) goods[count++] = good;
                }
                if (count < goods.Length) System.Array.Resize(ref goods, count);

                var go = new GameObject("Section_" + section.Id);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = section.Offset;
                go.SetActive(false);   // register only once wired
                var shelf = go.AddComponent<ShelfSection>();
                shelf.Configure(def.Id + "." + section.Id, section.VerbLabel, def.ReachMeters, _insideNow, goods);
                go.SetActive(true);
                _sections[s] = shelf;
                _madeSections[s] = go;
            }
        }

        private ShelfGood GoodOfSlot(ShopShelfSlotsDef def, string slotId)
        {
            for (int i = 0; i < def.Slots.Count; i++)
                if (def.Slots[i] != null && string.Equals(def.Slots[i].Id, slotId, System.StringComparison.Ordinal))
                    return _shelfGoods[i];
            return null;
        }

        private static void DestroyAll(GameObject[] made)
        {
            for (int i = 0; i < made.Length; i++)
                if (made[i] != null)
                {
                    if (Application.isPlaying) Destroy(made[i]);
                    else DestroyImmediate(made[i]);
                }
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static SpriteRenderer Layer(GameObject go, Sprite sprite, SpriteRenderer room, int aboveRoom)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (room != null)
            {
                sr.sortingLayerID = room.sortingLayerID;
                sr.sortingOrder = room.sortingOrder + aboveRoom;
                sr.sharedMaterial = room.sharedMaterial;
            }
            sr.enabled = false;
            return sr;
        }

        /// <summary>Overrides the inside test the shelf goods use (EditMode has no occupant). Call before
        /// <see cref="Configure"/>.</summary>
        public void SetInsideTest(System.Func<bool> inside) => _insideNow = inside;

        private void OnEnable() => EventBus.Subscribe<IslandShelvesChanged>(OnChanged);
        private void OnDisable() => EventBus.Unsubscribe<IslandShelvesChanged>(OnChanged);

        private void OnChanged(IslandShelvesChanged change)
        {
            if (_def != null && string.Equals(change.ShopId, _def.ShopId, System.StringComparison.Ordinal))
                _dirty = true;
        }

        private void Update()
        {
            bool inside = Inside();
            bool hasShelves = IslandShelvesDirectory.TryGet(_def != null ? _def.ShopId : null, out _);
            if (inside == _wasInside && !_dirty && hasShelves == _hadShelves) return;
            _wasInside = inside;
            _hadShelves = hasShelves;
            Refresh(GameServices.Clock?.TotalSeconds ?? 0.0);
        }

        private bool Inside() => _building != null && _building.IsInside && _building.Level == 0;

        /// <summary>Shows a card on every empty slot (while inside) and hides the rest, at
        /// <paramref name="now"/>. Public so a test drives it without the frame loop.</summary>
        public void Refresh(double now) => Refresh(now, Inside());

        /// <summary><see cref="Refresh(double)"/> with the inside test given (EditMode has no occupant).</summary>
        public void Refresh(double now, bool inside)
        {
            _dirty = false;
            if (_def == null) return;
            IslandShelvesDirectory.TryGet(_def.ShopId, out IIslandShelves shelves);
            for (int i = 0; i < _cards.Length; i++)
            {
                ShopShelfSlot slot = _def.Slots[i];
                bool soldOut = slot != null && SoldOutSignRule.IsSoldOut(shelves, slot.LineId, now);
                if (_cards[i] != null) _cards[i].enabled = inside && soldOut && !slot.HideSoldOutCard;
                if (_covers[i] != null) _covers[i].enabled = inside;
                if (_goods[i] != null) _goods[i].enabled = inside && !soldOut;
                if (_shelfGoods[i] != null) _shelfGoods[i].Refresh(now);
            }
        }
    }
}
