using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>Everything a store's maths needs at one instant: the clock, the day length, the spoil
    /// policy and the item spoil rates. <see cref="Capture"/> reads them from the live services.</summary>
    public readonly struct KitchenContext
    {
        public readonly double NowGameSeconds;
        public readonly float SecondsPerDay;
        public readonly SpoilPolicy Policy;
        public readonly IFoodPerishability Perishability;

        public KitchenContext(double nowGameSeconds, float secondsPerDay, in SpoilPolicy policy,
                              IFoodPerishability perishability)
        {
            NowGameSeconds = nowGameSeconds;
            SecondsPerDay = secondsPerDay;
            Policy = policy;
            Perishability = perishability ?? NothingSpoilsPerishability.Instance;
        }

        /// <summary>In-game seconds per in-game hour (0 with no sensible day length).</summary>
        public double SecondsPerGameHour => SecondsPerDay > 0f ? SecondsPerDay / 24d : 0d;

        /// <summary>The same context at another instant.</summary>
        public KitchenContext At(double nowGameSeconds)
            => new KitchenContext(nowGameSeconds, SecondsPerDay, Policy, Perishability);

        public float SpoilPerDay(string itemId) => Perishability.SpoilPerDay(itemId);

        /// <summary>The live clock, day length, freshness policy and perishability resolver.</summary>
        public static KitchenContext Capture()
        {
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0d;
            GameConfig config = GameServices.Config;
            SpoilPolicy policy = (config != null ? config.Freshness : FreshnessSettings.Default).Policy;
            return new KitchenContext(now, GameServices.SecondsPerDay, policy, GameServices.FoodPerishability);
        }
    }

    /// <summary>
    /// <b>The pure maths of one food store</b> (kitchens slice 1): what goes in, what comes out, what the
    /// ice and the power do to it. No Unity calls, no clock reads, no RNG; every method takes the instant
    /// in a <see cref="KitchenContext"/>. Everything that changes a store SETTLES it first, so time already
    /// spent warm is banked and can never be undone by cold arriving late (the <see cref="Freshness"/>
    /// rule).
    ///
    /// <para><b>Catch-up equals stepping.</b> A store is piecewise constant: a cooler is cold until one
    /// computable instant (its ice running out), a fridge changes only when its power does (banked at the
    /// slow tick), a pantry never changes. Settling once across a sleep therefore lands on the same
    /// numbers as settling every tick (to float rounding of the spoil sum; the melt instant is exact).</para>
    /// </summary>
    public static class KitchenStorageModel
    {
        /// <summary>How many items are in it, in all.</summary>
        public static int Total(KitchenStorageStateDto store)
        {
            if (store?.Stacks == null) return 0;
            int n = 0;
            for (int i = 0; i < store.Stacks.Count; i++) n += store.Stacks[i].Count;
            return n;
        }

        /// <summary>How many of <paramref name="itemId"/> are in it.</summary>
        public static int Count(KitchenStorageStateDto store, string itemId)
        {
            if (store?.Stacks == null || string.IsNullOrEmpty(itemId)) return 0;
            int n = 0;
            for (int i = 0; i < store.Stacks.Count; i++)
                if (store.Stacks[i].ItemId == itemId) n += store.Stacks[i].Count;
            return n;
        }

        /// <summary>Room left, in items.</summary>
        public static int Free(KitchenStorageStateDto store, KitchenStorageDef def)
        {
            if (def == null) return 0;
            int free = def.CapacityItems - Total(store);
            return free > 0 ? free : 0;
        }

        /// <summary>Game seconds one bag of ice lasts in this store (0 with no sensible day length).</summary>
        public static double IceUnitSeconds(KitchenStorageDef def, in KitchenContext ctx)
            => def != null && def.IceHoursPerUnit > 0f ? def.IceHoursPerUnit * ctx.SecondsPerGameHour : 0d;

        /// <summary>Game seconds of ice left at the context's instant (0 when none).</summary>
        public static double IceSecondsLeft(KitchenStorageStateDto store, in KitchenContext ctx)
        {
            if (store == null || store.IceRunsOutAtGameSeconds < 0d) return 0d;
            double left = store.IceRunsOutAtGameSeconds - ctx.NowGameSeconds;
            return left > 0d ? left : 0d;
        }

        /// <summary>Bags of ice still (partly) in it: a bag half gone still counts as one.</summary>
        public static int IceUnitsLeft(KitchenStorageStateDto store, KitchenStorageDef def, in KitchenContext ctx)
        {
            double unit = IceUnitSeconds(def, ctx);
            double left = IceSecondsLeft(store, ctx);
            if (!(unit > 0d) || !(left > 0d)) return 0;
            return (int)System.Math.Ceiling(left / unit - 1e-9);
        }

        /// <summary>True while it keeps its contents cold: a cooler with ice left, a fridge with power.</summary>
        public static bool IsCold(KitchenStorageStateDto store, KitchenStorageDef def, in KitchenContext ctx)
        {
            if (store == null || def == null || def.ColdMode == StorageMode.Ambient) return false;
            switch (def.Kind)
            {
                case KitchenStorageKind.Cooler: return IceSecondsLeft(store, ctx) > 0d;
                case KitchenStorageKind.Fridge: return store.Powered;
                default: return false;
            }
        }

        /// <summary>The mode its contents are held in now.</summary>
        public static StorageMode HeldMode(KitchenStorageStateDto store, KitchenStorageDef def, in KitchenContext ctx)
            => IsCold(store, def, ctx) ? def.ColdMode : StorageMode.Ambient;

        /// <summary>One lot's freshness clock at the context's instant, without banking it.</summary>
        public static FreshnessState FreshnessAt(KitchenStorageStateDto store, KitchenStorageDef def,
                                                 in KitchenStackDto stack, in KitchenContext ctx)
        {
            FreshnessState fs = stack.Freshness;
            float spd = ctx.SpoilPerDay(stack.ItemId);
            bool iceBound = def != null && def.Kind == KitchenStorageKind.Cooler &&
                            fs.Mode != StorageMode.Ambient && fs.Mode == def.ColdMode;
            if (iceBound)
            {
                double ends = store != null && store.IceRunsOutAtGameSeconds >= 0d
                    ? store.IceRunsOutAtGameSeconds : fs.LastSettleGameSeconds;
                return Freshness.SettleThroughProtection(fs, ctx.NowGameSeconds, ends, spd, ctx.SecondsPerDay, ctx.Policy);
            }
            return Freshness.Settle(fs, ctx.NowGameSeconds, spd, ctx.SecondsPerDay, ctx.Policy);
        }

        /// <summary>One lot's spoil 0..1 at the context's instant.</summary>
        public static float SpoilAt(KitchenStorageStateDto store, KitchenStorageDef def,
                                    in KitchenStackDto stack, in KitchenContext ctx)
            => FreshnessAt(store, def, stack, ctx).SpoilAccrued;

        /// <summary>Bank every lot's spoil up to the context's instant. Idempotent at a fixed instant.</summary>
        public static void Settle(KitchenStorageStateDto store, KitchenStorageDef def, in KitchenContext ctx)
        {
            if (store?.Stacks == null) return;
            for (int i = 0; i < store.Stacks.Count; i++)
                store.Stacks[i] = store.Stacks[i].With(FreshnessAt(store, def, store.Stacks[i], ctx));
        }

        /// <summary>
        /// Put up to <paramref name="count"/> of <paramref name="itemId"/> in, arriving with its own
        /// freshness clock (a catch from the hold keeps the hours it already spent). Returns how many went
        /// in: never more than the room left. The lot is settled to now in its own mode, then held in the
        /// store's.
        /// </summary>
        public static int Store(KitchenStorageStateDto store, KitchenStorageDef def, in KitchenContext ctx,
                                string itemId, int count, in FreshnessState arriving)
        {
            if (store == null || def == null || string.IsNullOrEmpty(itemId) || count <= 0) return 0;
            store.Stacks ??= new List<KitchenStackDto>();
            Settle(store, def, ctx);
            int n = System.Math.Min(count, Free(store, def));
            if (n <= 0) return 0;

            FreshnessState settled = Freshness.Settle(arriving, ctx.NowGameSeconds, ctx.SpoilPerDay(itemId),
                                                      ctx.SecondsPerDay, ctx.Policy);
            var lot = new KitchenStackDto(itemId, n,
                new FreshnessState(settled.SpoilAccrued, settled.LastSettleGameSeconds, HeldMode(store, def, ctx)));

            for (int i = 0; i < store.Stacks.Count; i++)
            {
                KitchenStackDto s = store.Stacks[i];
                if (s.ItemId == lot.ItemId && s.Mode == lot.Mode && s.SpoilAccrued == lot.SpoilAccrued &&
                    s.LastSettleGameSeconds == lot.LastSettleGameSeconds)
                {
                    store.Stacks[i] = s.WithCount(s.Count + n);
                    return n;
                }
            }
            store.Stacks.Add(lot);
            return n;
        }

        /// <summary>
        /// Take up to <paramref name="count"/> of <paramref name="itemId"/> out, oldest lot first, settled to
        /// now. The taken lots (with their clocks) are appended to <paramref name="taken"/> when given.
        /// Returns how many came out.
        /// </summary>
        public static int Take(KitchenStorageStateDto store, KitchenStorageDef def, in KitchenContext ctx,
                               string itemId, int count, List<KitchenStackDto> taken = null)
        {
            if (store?.Stacks == null || string.IsNullOrEmpty(itemId) || count <= 0) return 0;
            Settle(store, def, ctx);
            int got = 0;
            for (int i = 0; i < store.Stacks.Count && got < count;)
            {
                KitchenStackDto s = store.Stacks[i];
                if (s.ItemId != itemId) { i++; continue; }
                int n = System.Math.Min(s.Count, count - got);
                got += n;
                taken?.Add(s.WithCount(n));
                if (n >= s.Count) store.Stacks.RemoveAt(i);
                else { store.Stacks[i] = s.WithCount(s.Count - n); i++; }
            }
            return got;
        }

        /// <summary>How many bags of ice it could take right now.</summary>
        public static int IceRoom(KitchenStorageStateDto store, KitchenStorageDef def, in KitchenContext ctx)
        {
            if (store == null || def == null || def.Kind != KitchenStorageKind.Cooler) return 0;
            double unit = IceUnitSeconds(def, ctx);
            if (!(unit > 0d)) return 0;
            double room = def.IceCapacityUnits * unit - IceSecondsLeft(store, ctx);
            int bags = (int)System.Math.Floor(room / unit + 1e-9);
            return bags > 0 ? bags : 0;
        }

        /// <summary>
        /// Pack up to <paramref name="units"/> bags of ice in a cooler. Settles first (so the warm stretch
        /// before the ice is banked), extends the melt instant by one bag's life per bag from whichever is
        /// later of now and the old melt instant, and holds every lot cold from now. Returns bags packed
        /// (never more than <see cref="IceRoom"/>).
        /// </summary>
        public static int PackIce(KitchenStorageStateDto store, KitchenStorageDef def, in KitchenContext ctx, int units)
        {
            int n = System.Math.Min(units, IceRoom(store, def, ctx));
            if (n <= 0) return 0;
            Settle(store, def, ctx);
            double from = store.IceRunsOutAtGameSeconds > ctx.NowGameSeconds ? store.IceRunsOutAtGameSeconds : ctx.NowGameSeconds;
            store.IceRunsOutAtGameSeconds = from + n * IceUnitSeconds(def, ctx);
            store.IceOutAnnounced = false;
            Rehold(store, def, ctx);
            return n;
        }

        /// <summary>A fridge's power changed: bank the stretch so far, then hold its lots in the new mode.
        /// A no-op for anything but a fridge or when nothing changed.</summary>
        public static void SetPower(KitchenStorageStateDto store, KitchenStorageDef def, in KitchenContext ctx, bool powered)
        {
            if (store == null || def == null || def.Kind != KitchenStorageKind.Fridge || store.Powered == powered) return;
            Settle(store, def, ctx);
            store.Powered = powered;
            Rehold(store, def, ctx);
        }

        /// <summary>Every lot (already settled to now) is held in the store's mode from now.</summary>
        static void Rehold(KitchenStorageStateDto store, KitchenStorageDef def, in KitchenContext ctx)
        {
            if (store.Stacks == null) return;
            StorageMode mode = HeldMode(store, def, ctx);
            for (int i = 0; i < store.Stacks.Count; i++)
            {
                FreshnessState fs = store.Stacks[i].Freshness;
                store.Stacks[i] = store.Stacks[i].With(new FreshnessState(fs.SpoilAccrued, fs.LastSettleGameSeconds, mode));
            }
        }
    }
}
