using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>What he has to mend her with</b>, for the dory mending session: the supplies he has put away,
    /// plus the goods in the paper shopping bag in his hand. A bag he has set down somewhere does not
    /// count until he picks it up again, because what is in it is lying wherever he left it.
    ///
    /// <para><b>Why put-away supplies count.</b> The supplies are given by people, and once the paper bag
    /// is put away at home the goods in it are ordinary owned supplies. If only the bag in his hand
    /// counted, a player who tidied the shopping away before going to the slip would own a plank and be
    /// told by the station that he had none. Counting both closes that.</para>
    ///
    /// <para><b>Built for one acting player.</b> It is handed his save and his hands
    /// (<see cref="IShoppingBagHands"/>) when it is made; it holds no static and reaches for no global, so a
    /// second player's carry is simply a second adapter.</para>
    ///
    /// <para><b>A reservation debits nothing.</b> It only remembers what the job will use. While it stands,
    /// <see cref="Count"/> leaves those goods out, and the reservation stays valid only while the same save
    /// is loaded and there is still enough put away or in his hand to cover it. Setting the bag down
    /// mid-job with the only plank in it makes it invalid, and the session then asks for the goods again.
    /// <see cref="IMendingReservation.TryConsume"/> takes each unit from the bag in his hand first and
    /// otherwise from what is put away, spending it from his supplies, all of them or none, with no save
    /// write.</para>
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

        // Owned and not in the bag: put away. SupplyLocker.Count is exactly that.
        private static int PutAway(SaveData data, string itemId)
            => data != null ? SupplyLocker.Count(data, itemId) : 0;

        private int Available(SaveData data, string itemId) => PutAway(data, itemId) + InHand(data, itemId);

        private int Reserved(string itemId)
            => itemId != null && _reserved.TryGetValue(itemId, out int n) ? n : 0;

        /// <inheritdoc/>
        public int Count(string itemId)
        {
            int n = Available(_save?.Current, itemId) - Reserved(itemId);
            return n > 0 ? n : 0;
        }

        /// <inheritdoc/>
        public bool TryReserve(IReadOnlyList<MendingMaterial> materials, out IMendingReservation reservation)
        {
            reservation = null;
            SaveData data = _save?.Current;
            if (materials == null || materials.Count == 0 || data == null) return false;
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
                    if (_closed || !ReferenceEquals(_data, _owner._save?.Current)) return false;
                    for (int i = 0; i < _materials.Length; i++)
                    {
                        string id = _materials[i].ItemId;
                        if (_owner.Available(_data, id) < _owner.Reserved(id)) return false;
                    }
                    return true;
                }
            }

            public bool TryConsume()
            {
                if (!IsValid) return false;
                // Every check is above, so each step below succeeds. A unit in the bag in his hand comes
                // out of the bag and is spent; otherwise the spend takes one that is put away (TrySpend
                // only ever spends goods that are not in the bag).
                for (int i = 0; i < _materials.Length; i++)
                    for (int n = 0; n < _materials[i].Count; n++)
                    {
                        string id = _materials[i].ItemId;
                        if (_owner.InHand(_data, id) > 0) ShoppingBagContents.TryTakeOut(_data, id);
                        SupplyLocker.TrySpend(_data, id);
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
