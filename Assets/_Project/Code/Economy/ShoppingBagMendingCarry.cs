using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>What he has carried to the slip</b>, for the dory mending session: the goods in the paper
    /// shopping bag in his hand. Nothing put away at home counts (that is remote storage, which the
    /// <see cref="IMendingCarry"/> contract excludes), and neither does a bag he has set down.
    ///
    /// <para><b>Built for one acting player.</b> It is handed his save and his hands
    /// (<see cref="IShoppingBagHands"/>) when it is made; it holds no static and reaches for no global, so a
    /// second player's carry is simply a second adapter.</para>
    ///
    /// <para><b>A reservation debits nothing.</b> It only remembers what the job will use. While it stands,
    /// <see cref="Count"/> leaves those goods out, and the reservation stays valid only while the same save
    /// is loaded, the bag is still in his hand, and the bag still holds what was reserved. Putting the
    /// shopping away or setting the bag down mid-job makes it invalid, and the session then asks for the
    /// goods again. <see cref="IMendingReservation.TryConsume"/> takes the goods out of the bag and spends
    /// them from his supplies, all of them or none, with no save write.</para>
    /// </summary>
    public sealed class ShoppingBagMendingCarry : IMendingCarry
    {
        private readonly ISaveService _save;
        private readonly IShoppingBagHands _hands;
        private readonly Dictionary<string, int> _reserved = new Dictionary<string, int>();

        public ShoppingBagMendingCarry(ISaveService save, IShoppingBagHands hands)
        {
            _save = save;
            _hands = hands;
        }

        /// <summary>True while the bag is in his hand in the loaded game.</summary>
        public bool Carrying => Carried(_save?.Current);

        private bool Carried(SaveData data)
            => data != null && _hands != null && _hands.HoldsBag && !ShoppingBagContents.IsSetDown(data);

        private int InHand(SaveData data, string itemId)
            => Carried(data) ? ShoppingBagContents.InBag(data, itemId) : 0;

        private int Reserved(string itemId)
            => itemId != null && _reserved.TryGetValue(itemId, out int n) ? n : 0;

        /// <inheritdoc/>
        public int Count(string itemId)
        {
            int n = InHand(_save?.Current, itemId) - Reserved(itemId);
            return n > 0 ? n : 0;
        }

        /// <inheritdoc/>
        public bool TryReserve(IReadOnlyList<MendingMaterial> materials, out IMendingReservation reservation)
        {
            reservation = null;
            SaveData data = _save?.Current;
            if (materials == null || materials.Count == 0 || !Carried(data)) return false;
            for (int i = 0; i < materials.Count; i++)
            {
                MendingMaterial m = materials[i];
                if (string.IsNullOrEmpty(m.ItemId) || m.Count <= 0) return false;
                if (Count(m.ItemId) < Needed(materials, m.ItemId)) return false;
            }
            var copy = new MendingMaterial[materials.Count];
            for (int i = 0; i < materials.Count; i++)
            {
                copy[i] = materials[i];
                _reserved[copy[i].ItemId] = Reserved(copy[i].ItemId) + copy[i].Count;
            }
            reservation = new Reservation(this, data, copy);
            return true;
        }

        // The whole request's need for one id (a list could name it twice).
        private static int Needed(IReadOnlyList<MendingMaterial> materials, string itemId)
        {
            int n = 0;
            for (int i = 0; i < materials.Count; i++)
                if (materials[i].ItemId == itemId) n += materials[i].Count;
            return n;
        }

        private void Release(MendingMaterial[] materials)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                int left = Reserved(materials[i].ItemId) - materials[i].Count;
                if (left > 0) _reserved[materials[i].ItemId] = left;
                else _reserved.Remove(materials[i].ItemId);
            }
        }

        private sealed class Reservation : IMendingReservation
        {
            private readonly ShoppingBagMendingCarry _owner;
            private readonly SaveData _data;
            private readonly MendingMaterial[] _materials;
            private bool _closed;

            public Reservation(ShoppingBagMendingCarry owner, SaveData data, MendingMaterial[] materials)
            {
                _owner = owner;
                _data = data;
                _materials = materials;
            }

            public bool IsValid
            {
                get
                {
                    if (_closed || !ReferenceEquals(_data, _owner._save?.Current) || !_owner.Carried(_data))
                        return false;
                    for (int i = 0; i < _materials.Length; i++)
                    {
                        string id = _materials[i].ItemId;
                        if (ShoppingBagContents.InBag(_data, id) < _owner.Reserved(id)) return false;
                        if (SupplyLocker.Owned(_data, id) < ShoppingBagContents.InBag(_data, id)) return false;
                    }
                    return true;
                }
            }

            public bool TryConsume()
            {
                if (!IsValid) return false;
                // Every check is above, so each step below succeeds: one out of the bag makes one usable,
                // and the spend takes exactly that one.
                for (int i = 0; i < _materials.Length; i++)
                    for (int n = 0; n < _materials[i].Count; n++)
                    {
                        ShoppingBagContents.TryTakeOut(_data, _materials[i].ItemId);
                        SupplyLocker.TrySpend(_data, _materials[i].ItemId);
                    }
                Dispose();
                return true;
            }

            public void Dispose()
            {
                if (_closed) return;
                _closed = true;
                _owner.Release(_materials);
            }
        }
    }
}
