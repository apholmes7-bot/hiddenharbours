using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>Pure delivery arithmetic on the canon seven-day calendar (absolute day zero is Monday).</summary>
    public static class ShopRestock
    {
        private const int DaysPerWeek = (int)Weekday.Sunday + 1;

        /// <summary>Count deliveries in (settledThroughDay, today], in at most seven steps, even years away.</summary>
        public static long DeliveriesBetween(ShopDeliveryDays weekdays, int settledThroughDay, int today)
        {
            if (settledThroughDay < 0 || today < settledThroughDay)
                throw new ArgumentOutOfRangeException(nameof(today));
            long elapsed = (long)today - settledThroughDay;
            long weeks = elapsed / DaysPerWeek;
            int remainder = (int)(elapsed % DaysPerWeek);
            long deliveries = 0;
            for (int day = 0; day < DaysPerWeek; day++)
            {
                if (((int)weekdays & (1 << day)) == 0) continue;
                deliveries += weeks;
                int offset = (day - settledThroughDay % DaysPerWeek + DaysPerWeek) % DaysPerWeek;
                if (offset == 0) offset = DaysPerWeek;
                if (offset <= remainder) deliveries++;
            }
            return deliveries;
        }

        /// <summary>No jitter is authored: all seeds share the timetable. No random state is consumed.</summary>
        public static int Settle(ShopStockLine line, int count, int settledThroughDay, int today, uint worldSeed)
        {
            long deliveries = DeliveriesBetween(line.DeliveryWeekdays, settledThroughDay, today);
            // An int day span times an int delivery count fits in long; clamp before narrowing.
            long available = Math.Max(line.Floor, Math.Min(line.MaxOnShelf, count));
            return (int)Math.Min(line.MaxOnShelf, available + deliveries * line.ItemsPerDelivery);
        }

        /// <summary>The essentials guarantee replenishes the minimum immediately; it remains takeable.</summary>
        public static bool TryTakeOne(ShopStockLine line, int count, out int remaining)
        {
            remaining = count;
            if (count <= 0) return false;
            remaining = Math.Max(line.Floor, count - 1);
            return true;
        }

        public static bool TryPutBackOne(ShopStockLine line, int count, out int remaining)
        {
            remaining = count;
            if (count >= line.MaxOnShelf) return false;
            remaining = count + 1;
            return true;
        }
    }
}
