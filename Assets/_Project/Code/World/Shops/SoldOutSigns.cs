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
    /// </summary>
    public sealed class SoldOutSigns : MonoBehaviour
    {
        private ShopShelfSlotsDef _def;
        private BuildingInterior _building;
        private SpriteRenderer[] _cards = new SpriteRenderer[0];
        private bool _wasInside;
        private bool _dirty = true;
        private bool _hadShelves;

        /// <summary>The cards, one per slot in the Def's order (a test reads which are showing).</summary>
        public SpriteRenderer[] Cards => _cards;

        /// <summary>Builds the cards for <paramref name="def"/> on <paramref name="building"/>. Idempotent.</summary>
        public void Configure(ShopShelfSlotsDef def, BuildingInterior building)
        {
            _def = def;
            _building = building;
            for (int i = 0; i < _cards.Length; i++)
                if (_cards[i] != null)
                {
                    if (Application.isPlaying) Destroy(_cards[i].gameObject);
                    else DestroyImmediate(_cards[i].gameObject);
                }
            if (def == null || def.Slots == null) { _cards = new SpriteRenderer[0]; return; }

            SpriteRenderer room = building != null ? building.RoomRenderer : null;
            _cards = new SpriteRenderer[def.Slots.Count];
            for (int i = 0; i < def.Slots.Count; i++)
            {
                ShopShelfSlot slot = def.Slots[i];
                var go = new GameObject("SoldOut_" + (slot != null ? slot.Id : i.ToString()));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = slot != null ? (Vector3)slot.Offset : Vector3.zero;
                var card = go.AddComponent<SpriteRenderer>();
                card.sprite = def.SoldOutSign;
                if (room != null)
                {
                    card.sortingLayerID = room.sortingLayerID;
                    card.sortingOrder = room.sortingOrder + def.SortingOrderAboveRoom;
                    card.sharedMaterial = room.sharedMaterial;
                }
                card.enabled = false;
                _cards[i] = card;
            }
            _dirty = true;
        }

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
                if (_cards[i] == null) continue;
                ShopShelfSlot slot = _def.Slots[i];
                _cards[i].enabled = inside && slot != null && SoldOutSignRule.IsSoldOut(shelves, slot.LineId, now);
            }
        }
    }
}
