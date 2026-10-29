using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>Fishing tubs remain owned while set. Availability is derived from placement records.</summary>
    public static class TubLocker
    {
        public static int OwnedCount(SaveData save, string longlineDefId)
        {
            if (save?.TubStock == null || string.IsNullOrEmpty(longlineDefId)) return 0;
            for (int i = 0; i < save.TubStock.Count; i++)
                if (save.TubStock[i].LonglineDefId == longlineDefId) return save.TubStock[i].Count;
            return 0;
        }

        public static int AddOwned(SaveData save, string longlineDefId, int count)
        {
            if (save == null || string.IsNullOrEmpty(longlineDefId) || count <= 0) return 0;
            save.TubStock ??= new List<TubStock>();
            for (int i = 0; i < save.TubStock.Count; i++)
            {
                if (save.TubStock[i].LonglineDefId != longlineDefId) continue;
                int total = checked(save.TubStock[i].Count + count);
                save.TubStock[i] = new TubStock(longlineDefId, total);
                return total;
            }
            save.TubStock.Add(new TubStock(longlineDefId, count));
            return count;
        }

        public static int DeployedCount(SaveData save, string longlineDefId)
        {
            if (save?.PlacedLonglines == null || string.IsNullOrEmpty(longlineDefId)) return 0;
            int count = 0;
            for (int i = 0; i < save.PlacedLonglines.Count; i++)
                if (save.PlacedLonglines[i].LonglineDefId == longlineDefId)
                    count = checked(count + System.Math.Max(0, save.PlacedLonglines[i].Tubs));
            return count;
        }

        public static int AvailableCount(SaveData save, string longlineDefId)
            => OwnedCount(save, longlineDefId) - DeployedCount(save, longlineDefId);
    }
}
