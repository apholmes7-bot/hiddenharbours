using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What the island needs from NMC, what the runs can carry, and which limit binds</b> (island supply
    /// slice S1). Pure arithmetic over the Defs: no state, no clock, no randomness. Every number it uses comes
    /// from an <see cref="IslandGoodDef"/>, <see cref="CarrierDef"/>, <see cref="SupplyRoleDef"/>,
    /// <see cref="IslandSupplyConfig"/> or <see cref="GameConfig"/>; see <c>docs/design/island-supply.md</c>.
    /// </summary>
    public static class SupplyPlanMath
    {
        // ---- demand -----------------------------------------------------------------------------------

        /// <summary>Daily mass and volume carried over the bar: every <see cref="GoodSource.MainlandOverBar"/>
        /// good, with its player allowance when <paramref name="withPlayerAllowance"/>, and its winter factor
        /// when <paramref name="winter"/>.</summary>
        public static MassVolume DailyBarDemand(IEnumerable<IslandGoodDef> goods, bool withPlayerAllowance = true,
                                                bool winter = false)
        {
            double kg = 0.0, litres = 0.0;
            if (goods == null) return new MassVolume(0.0, 0.0);
            foreach (IslandGoodDef g in goods)
            {
                if (g == null || g.Source != GoodSource.MainlandOverBar) continue;
                double k = (winter ? g.WinterMultiplier : 1.0) * (withPlayerAllowance ? 1.0 + g.PlayerAllowanceFraction : 1.0);
                kg += g.DailyMassKg * k;
                litres += g.DailyVolumeLitres * k;
            }
            return new MassVolume(kg, litres);
        }

        // ---- capacity ---------------------------------------------------------------------------------

        /// <summary>The summed limits of every carrier the role takes. An id with no Def adds nothing and is
        /// counted in <paramref name="missing"/> (the content test refuses one).</summary>
        public static MassVolume KitCapacity(SupplyRoleDef role, IReadOnlyDictionary<string, CarrierDef> carriers,
                                             out int missing)
        {
            missing = 0;
            double kg = 0.0, litres = 0.0;
            if (role == null || role.CarrierIds == null) return new MassVolume(0.0, 0.0);
            foreach (string id in role.CarrierIds)
            {
                if (id == null || carriers == null || !carriers.TryGetValue(id, out CarrierDef c) || c == null)
                {
                    missing++;
                    continue;
                }
                kg += c.CapacityKg;
                litres += c.CapacityLitres;
            }
            return new MassVolume(kg, litres);
        }

        /// <summary>True when the role's runs count as bar capacity: it crosses and runs on a cadence.</summary>
        public static bool CountsAsBarCapacity(SupplyRoleDef role) =>
            role != null && role.Mode != SupplyTripMode.MainlandOnly && role.CadenceDays > 0;

        /// <summary>Round trips per week (<c>DaysPerWeek / CadenceDays</c>); 0 for an as-needed or mainland role.</summary>
        public static double RoundTripsPerWeek(SupplyRoleDef role, GameConfig cfg) =>
            CountsAsBarCapacity(role) ? (double)Math.Max(1, cfg.DaysPerWeek) / role.CadenceDays : 0.0;

        /// <summary>Person- or vehicle-crossings of the bar per week: two per round trip per person aboard.</summary>
        public static double BarCrossingsPerWeek(SupplyRoleDef role, GameConfig cfg) =>
            RoundTripsPerWeek(role, cfg) * 2.0 * Math.Max(1, role.People);

        // ---- loads ------------------------------------------------------------------------------------

        /// <summary>Does a load of <paramref name="kg"/> and <paramref name="litres"/> fit limits of
        /// <paramref name="capacityKg"/> and <paramref name="capacityLitres"/>? The binding limit is the one with
        /// the larger fill fraction.</summary>
        public static LoadFit Fit(double capacityKg, double capacityLitres, double kg, double litres)
        {
            double mf = capacityKg > 0.0 ? kg / capacityKg : (kg > 0.0 ? double.PositiveInfinity : 0.0);
            double vf = capacityLitres > 0.0 ? litres / capacityLitres : (litres > 0.0 ? double.PositiveInfinity : 0.0);
            LoadDimension binding = mf <= 0.0 && vf <= 0.0 ? LoadDimension.None
                                  : (mf >= vf ? LoadDimension.Mass : LoadDimension.Volume);
            return new LoadFit(mf <= 1.0 && vf <= 1.0, mf, vf, binding);
        }

        /// <summary><see cref="Fit(double,double,double,double)"/> against one carrier's limits.</summary>
        public static LoadFit Fit(CarrierDef carrier, double kg, double litres) =>
            Fit(carrier.CapacityKg, carrier.CapacityLitres, kg, litres);

        /// <summary>Gross mass of a container filled to its limit: contents plus tare.</summary>
        public static double FullGrossKg(CarrierDef container) => container.CapacityKg + container.TareKg;

        /// <summary>Gross mass of a pallet carrying <paramref name="count"/> full containers.</summary>
        public static double PalletOfFullContainersKg(CarrierDef pallet, CarrierDef container, int count) =>
            pallet.TareKg + count * FullGrossKg(container);

        /// <summary>What a bucket (or any carrier) holds of a loose material: the volume filled at the
        /// material's density, cut at the mass limit. <paramref name="binding"/> says which limit stopped it.</summary>
        public static double BulkLoadKg(CarrierDef carrier, BulkMaterialDef material, out LoadDimension binding)
        {
            double byVolume = carrier.CapacityLitres * material.DensityKgPerLitre;
            if (byVolume > carrier.CapacityKg)
            {
                binding = LoadDimension.Mass;
                return carrier.CapacityKg;
            }
            binding = LoadDimension.Volume;
            return byVolume;
        }

        /// <summary>Round trips per day a kit must make to carry <paramref name="daily"/> alone (the larger of the
        /// mass and volume ratios): the "if the quad is down" figure.</summary>
        public static double RoundTripsPerDayToCarry(MassVolume daily, MassVolume kit) =>
            Math.Max(kit.MassKg > 0.0 ? daily.MassKg / kit.MassKg : double.PositiveInfinity,
                     kit.VolumeLitres > 0.0 ? daily.VolumeLitres / kit.VolumeLitres : double.PositiveInfinity);

        // ---- the whole plan ---------------------------------------------------------------------------

        /// <summary>One evaluated supply plan.</summary>
        public sealed class Report
        {
            public MassVolume DailyDemand;
            public MassVolume WeeklyDemand;
            public MassVolume WeeklyCapacity;
            /// <summary>Capacity over demand minus one, by weight (0.9 = 90% spare).</summary>
            public double MassHeadroom;
            /// <summary>Capacity over demand minus one, by volume.</summary>
            public double VolumeHeadroom;
            /// <summary>The dimension with less headroom: the one that binds.</summary>
            public LoadDimension Binding;
            public double WeeklyRoundTrips;
            public double WeeklyBarCrossings;
            public MassVolume BufferStock;
            /// <summary>Shortest cadence among the bar roles, days (0 when none runs).</summary>
            public int ShortestCadenceDays;
            /// <summary>Consecutive runs of that role the buffer can lose before the shelf runs short:
            /// <c>floor(BufferDays / cadence) - 1</c>.</summary>
            public int MissedRunsCovered;
            public bool CapacityMeetsDemand => WeeklyCapacity.MassKg >= WeeklyDemand.MassKg
                                            && WeeklyCapacity.VolumeLitres >= WeeklyDemand.VolumeLitres;
        }

        /// <summary>Evaluates the roles against the goods for one week.</summary>
        public static Report Evaluate(IEnumerable<IslandGoodDef> goods, IEnumerable<SupplyRoleDef> roles,
                                      IReadOnlyDictionary<string, CarrierDef> carriers,
                                      IslandSupplyConfig route, GameConfig cfg, bool winter = false)
        {
            var r = new Report();
            int week = Math.Max(1, cfg.DaysPerWeek);
            r.DailyDemand = DailyBarDemand(goods, true, winter);
            r.WeeklyDemand = r.DailyDemand.Times(week);

            var capacity = new MassVolume(0.0, 0.0);
            int shortest = 0;
            if (roles != null)
            {
                foreach (SupplyRoleDef role in roles)
                {
                    if (!CountsAsBarCapacity(role)) continue;
                    double trips = RoundTripsPerWeek(role, cfg);
                    capacity = capacity.Plus(KitCapacity(role, carriers, out _).Times(trips));
                    r.WeeklyRoundTrips += trips;
                    r.WeeklyBarCrossings += BarCrossingsPerWeek(role, cfg);
                    if (shortest == 0 || role.CadenceDays < shortest) shortest = role.CadenceDays;
                }
            }
            r.WeeklyCapacity = capacity;
            r.MassHeadroom = r.WeeklyDemand.MassKg > 0.0 ? capacity.MassKg / r.WeeklyDemand.MassKg - 1.0 : double.PositiveInfinity;
            r.VolumeHeadroom = r.WeeklyDemand.VolumeLitres > 0.0 ? capacity.VolumeLitres / r.WeeklyDemand.VolumeLitres - 1.0 : double.PositiveInfinity;
            r.Binding = r.VolumeHeadroom < r.MassHeadroom ? LoadDimension.Volume : LoadDimension.Mass;
            r.BufferStock = r.DailyDemand.Times(route.BufferDays);
            r.ShortestCadenceDays = shortest;
            r.MissedRunsCovered = shortest > 0 ? Math.Max(0, (int)Math.Floor(route.BufferDays / shortest) - 1) : 0;
            return r;
        }
    }
}
