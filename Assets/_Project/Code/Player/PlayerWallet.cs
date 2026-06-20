using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>The player's purse (₲). Implements <see cref="IWallet"/>; raises MoneyChanged.</summary>
    public class PlayerWallet : MonoBehaviour, IWallet
    {
        [SerializeField] private int _money = 0;

        public int Money => _money;

        public void Add(int amount)
        {
            if (amount == 0) return;
            _money += amount;
            EventBus.Publish(new MoneyChanged(_money, amount));
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > _money) return false;
            _money -= amount;
            EventBus.Publish(new MoneyChanged(_money, -amount));
            return true;
        }
    }
}
