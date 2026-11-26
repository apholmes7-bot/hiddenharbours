using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Environment;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    // Complements BaitEconomyPacingTests and TidePacingInvariantTests with a seven-day cash ledger.
    // Uses their production pricing/tide rules, the existing SceneYaml reader and the terrain sampler.
    // ClamRefinementsTests already pins one clam per hole per tide; no new harvesting rule lives here.
    public class FirstWeekPurseTests
    {
        const string Document = "docs/design/first-week-purse.md";
        const string DataRoot = "Assets/_Project/Data/";
        const int Days = 7;
        const int MinutesPerHour = 60;
        const int HoursPerDay = 24;
        const double RationTolerance = 0.0000001;

        // Proposed prices only: PR 1539 (hull); PR 1508 (one of each material).
        const int ProposedDoryPrice = 80;
        const int ProposedPlankPrice = 24;
        const int ProposedNailsPrice = 8;
        const int ProposedOakumPrice = 12;
        const int ProposedPaintPrice = 16;

        // Observed shortfalls, not relaxed targets. A change requires reviewing the generated report.
        const int KnownOrdinaryRodShortfall = 0;
        const int KnownCarefulWeekShortfall = 0;
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public enum Strategy { Careful, Ordinary, Careless }

        sealed class Food
        {
            public SupplyDef Supply;
            public FoodDef Nutrition;
            public IslandShelfLine Shelf;
        }

        sealed class Purchase
        {
            public readonly string Name;
            public readonly int Price;
            public readonly string Availability;
            public Purchase(string name, int price, string availability)
            { Name = name; Price = price; Availability = availability; }
        }

        sealed class Inputs
        {
            public GameConfig Config;
            public RegionDef Region;
            public TideProfile Tide;
            public FishSpeciesDef Clam;
            public IslandStoreShelvesDef Shelves;
            public Food[] Foods;
            public Purchase[] Purchases;
            public float[] HoleElevations;
            public int Seed, StartingMoney, Capacity, BaitLotSize;
            public float SupplyPerSale, Demand, PriceLevel;
            public double HungerRate;
            public double BedHour => HoursPerDay + Config.WakeHour - Config.SleepMaxHours;
            public double Seconds(double hour) => hour * Config.SecondsPerDay / HoursPerDay;
            public float Water(double hour) => TideModel.Height(Seconds(hour), Tide, Config);
            public long Cycle(double hour) => TideModel.CycleIndex(Seconds(hour), Tide, Config);
        }

        sealed class Window
        {
            public long Cycle;
            public int Exposed, Clams, AtMinute;
        }

        sealed class Ration
        {
            public int[] Counts;
            public int Cost;
            public double Need, Relief;
        }

        sealed class Day
        {
            public int Number, Windows, Refused, Clams, Income, FoodPaid, FoodMissing, Purse, PurchaseSpend;
            public double ReliefMissing;
            public readonly List<string> Purchases = new List<string>();
            public readonly List<int> Balances = new List<int>();
        }

        sealed class Week
        {
            public Strategy Strategy;
            public readonly List<Day> Days = new List<Day>();
            public int[] BoughtOn;
            public int MissingFood => Days.Sum(d => d.FoodMissing);
            public int Remaining(Inputs data) => Math.Max(0,
                data.Purchases.Where((p, i) => BoughtOn[i] == 0).Sum(p => p.Price)
                + MissingFood - Days.Last().Purse);
        }

        static T Asset<T>(string relative) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(DataRoot + relative);
            Assert.That(asset, Is.Not.Null, relative);
            return asset;
        }

        static T Reference<T>(string value) where T : UnityEngine.Object
        {
            var match = Regex.Match(value ?? "", @"guid: ([a-f0-9]{32})");
            Assert.That(match.Success, Is.True, "Expected an authored asset reference: " + value);
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value));
            Assert.That(asset, Is.Not.Null, value);
            return asset;
        }

        static int Integer(Doc doc, string field) => int.Parse(doc.Field(field), Invariant);
        static float Scalar(Doc doc, string field) => float.Parse(doc.Field(field), Invariant);
        static float[] Components(string value) => Regex.Matches(value, @"[xyzw]: ([-+\d.eE]+)")
            .Cast<Match>().Select(m => float.Parse(m.Groups[1].Value, Invariant)).ToArray();

        static Vector3 Position(SceneYaml scene, Doc transform)
        {
            Vector3 point = Vector3.zero;
            var seen = new HashSet<long>();
            while (transform != null)
            {
                Assert.That(seen.Add(transform.FileId), Is.True, "Transform parent cycle");
                var p = Components(transform.Field("m_LocalPosition"));
                var s = Components(transform.Field("m_LocalScale"));
                var q = Components(transform.Field("m_LocalRotation"));
                point = new Quaternion(q[0], q[1], q[2], q[3]) *
                    Vector3.Scale(point, new Vector3(s[0], s[1], s[2])) + new Vector3(p[0], p[1], p[2]);
                long parent = transform.FieldRef("m_Father");
                transform = parent == 0 ? null : scene.Require(parent, "parent");
            }
            return point;
        }

        static Inputs ReadInputs()
        {
            var scene = SceneYaml.Parse(File.ReadAllText("Assets/_Project/Scenes/StPeters.unity").Replace("\r", ""));
            Doc One(string name) => scene.Docs.Single(d => d.IsScript(name));
            var env = One("HiddenHarbours.Environment.EnvironmentService");
            var cfg = Reference<GameConfig>(env.Field("_config"));
            var region = Asset<RegionDef>("Regions/StPeters.asset");
            var shelf = Asset<IslandStoreShelvesDef>("Resources/IslandSupply/Shelves/StPetersStoreShelves.asset");
            var seller = Asset<SellerDef>("Resources/Sellers/leblancs.asset");
            Assert.That(shelf.SellerId, Is.EqualTo(seller.Id));
            var clamLicence = Asset<LicenseDef>("Resources/Catalog/Licenses/ClamLicense.asset");
            var codLicence = Asset<LicenseDef>("Resources/Catalog/Licenses/CodLicense.asset");
            var rod = Asset<GearOffer>("Resources/Catalog/Gear/Rod.asset");
            var bait = Asset<BaitDef>("Resources/Catalog/Bait/Capelin.asset");
            var clam = Asset<FishSpeciesDef>("Fish/SoftShellClam.asset");
            Assert.That(clamLicence.PermittedSpeciesIds, Does.Contain(clam.Id));
            Assert.That(clamLicence.Catalog.IsStockedBy(seller.Id), Is.True);
            Assert.That(rod.Catalog.IsStockedBy(seller.Id), Is.True);
            Assert.That(bait.Catalog.IsStockedBy(seller.Id), Is.True);
            Assert.That(shelf.Lines.Single(l => l.CounterListingIds.Contains(bait.Id)).SoldAtShelf, Is.True);
            var foods = new[] { "Bread", "Oatcakes", "TinnedBeans" }.Select(name =>
            {
                var supply = Asset<SupplyDef>("Resources/Catalog/Supplies/" + name + ".asset");
                var food = Asset<FoodDef>("Resources/Food/" + name + ".asset");
                var line = shelf.Lines.Single(l => l.CounterListingIds.Contains(supply.Id));
                Assert.That(supply.Catalog.IsStockedBy(seller.Id), Is.True);
                Assert.That(line.SoldAtShelf, Is.True);
                Assert.That(food.ItemId, Is.EqualTo(supply.Id));
                Assert.That(food.EdibleRaw && food.HungerRelief > 0 && supply.Price > 0, Is.True);
                return new Food { Supply = supply, Nutrition = food, Shelf = line };
            }).ToArray();
            var terrain = One("HiddenHarbours.World.PaintedTidalTerrain");
            var map = Reference<PaintedHeightMap>(terrain.Field("_map"));
            Assert.That(map.Field, Is.Not.Null, "Readable terrain is required; do not skip this case.");
            var holes = scene.Docs.Where(d => d.IsScript("HiddenHarbours.Fishing.ClamDig")).ToArray();
            Assert.That(holes.Select(h => h.Field("_id")).Distinct().Count(), Is.EqualTo(holes.Length));
            var elevations = holes.Select(h =>
            {
                Assert.That(Reference<FishSpeciesDef>(h.Field("_clamSpecies")), Is.EqualTo(clam));
                return map.Field.ElevationAt(Position(scene, scene.Require(h.FieldRef("_spot"), "clam spot")));
            }).ToArray();
            var pail = scene.Docs.Single(d => d.IsScript("HiddenHarbours.Player.CarriableBucket") &&
                d.Field("_id") == "container.st_peters.bucket");
            var market = One("HiddenHarbours.Economy.Market");
            Assert.That(Integer(market, "_marketId"), Is.EqualTo((int)MarketId.StPetersStore));
            Assert.That(market.Field("_categoryDemand"), Is.EqualTo("[]"), "Read any new category overrides.");
            Assert.That(Reference<GameConfig>(market.Field("_config")), Is.EqualTo(cfg));
            var grant = One("HiddenHarbours.Economy.FrontedFeeGrant");
            Assert.That(Reference<GameConfig>(grant.Field("_config")), Is.EqualTo(cfg));
            float frame = cfg.Body.DefaultFrame;
            float hungerScale = BodyMath.HungerRateScale(BodyMath.LeanMassKg(frame, 1f, cfg.Body), frame, 1f, cfg.Body);
            string Availability(CatalogListing listing) => listing.IsStockedBy(seller.Id) ? "Island counter" : "No island listing";
            return new Inputs
            {
                Config = cfg, Region = region, Shelves = shelf, Foods = foods, Clam = clam,
                Seed = Integer(env, "_worldSeed"),
                StartingMoney = Integer(One("HiddenHarbours.Player.PlayerWallet"), "_money") + cfg.FrontedLicenceFee,
                Capacity = Integer(pail, "_capacity"), HoleElevations = elevations,
                Tide = new TideProfile { MeanLevel = region.TideMeanLevel, Amplitude = region.TideAmplitude, PhaseHours = region.TidePhaseHours },
                SupplyPerSale = Scalar(market, "_supplyPerSale"), Demand = cfg.MarketDemandStPetersStore,
                PriceLevel = cfg.MarketPriceLevelStPetersStore, BaitLotSize = BaitShop.LotSizeOf(bait),
                HungerRate = cfg.Needs.Hunger.RisePerGameHour * hungerScale,
                Purchases = new[]
                {
                    new Purchase("Shellfish licence", clamLicence.Price, Availability(clamLicence.Catalog)),
                    new Purchase("Rod", rod.Price, Availability(rod.Catalog)),
                    new Purchase("Bait lot", BaitShop.LotPriceOf(bait), "Island shelf"),
                    new Purchase("Dory (proposal)", ProposedDoryPrice, "PR 1539"),
                    new Purchase("Patch plank (proposal)", ProposedPlankPrice, "PR 1508"),
                    new Purchase("Copper nails (proposal)", ProposedNailsPrice, "PR 1508"),
                    new Purchase("Oakum (proposal)", ProposedOakumPrice, "PR 1508"),
                    new Purchase("Paint (proposal)", ProposedPaintPrice, "PR 1508"),
                    new Purchase("Cod licence", codLicence.Price, Availability(codLicence.Catalog))
                }
            };
        }

        static List<Window> Windows(Inputs data, int day)
        {
            double start = Math.Max(data.Config.WakeHour, day == 0 ? data.Config.StartHour : 0);
            var windows = new SortedDictionary<long, Window>();
            int first = (int)Math.Ceiling((day * HoursPerDay + start) * MinutesPerHour);
            int last = (int)((day * HoursPerDay + data.BedHour) * MinutesPerHour);
            for (int minute = first; minute < last; minute++)
            {
                double hour = minute / (double)MinutesPerHour;
                float water = data.Water(hour);
                if (water > data.Tide.MeanLevel) continue;
                long cycle = data.Cycle(hour);
                int exposed = data.HoleElevations.Count(e => TidalExposure.IsExposed(water, e));
                if (!windows.TryGetValue(cycle, out var window))
                    windows.Add(cycle, window = new Window { Cycle = cycle });
                if (exposed > window.Exposed) window.AtMinute = minute;
                window.Exposed = Math.Max(window.Exposed, exposed);
                window.Clams = Math.Min(data.Capacity, window.Exposed);
            }
            return windows.Values.Where(w => w.Clams > 0).ToList();
        }

        static Ration CheapestRation(Inputs data, int day)
        {
            double need = (day == 0 ? HoursPerDay - data.Config.StartHour : HoursPerDay) * data.HungerRate;
            int limit = (int)Math.Ceiling(need / data.Foods.Min(f => f.Nutrition.HungerRelief));
            Ration best = null;
            var counts = new int[data.Foods.Length];
            void Search(int index, int cost, double relief)
            {
                if (index == counts.Length)
                {
                    if (relief + RationTolerance < need) return;
                    if (best != null && (cost > best.Cost || cost == best.Cost && relief >= best.Relief)) return;
                    best = new Ration { Counts = (int[])counts.Clone(), Cost = cost, Need = need, Relief = relief };
                    return;
                }
                for (int count = 0; count <= limit; count++)
                {
                    counts[index] = count;
                    Search(index + 1, cost + count * data.Foods[index].Supply.Price,
                        relief + count * (double)data.Foods[index].Nutrition.HungerRelief);
                }
            }
            Search(0, 0, 0);
            Assert.That(best, Is.Not.Null);
            return best;
        }

        static Week Play(Inputs data, Strategy strategy)
        {
            var week = new Week { Strategy = strategy, BoughtOn = new int[data.Purchases.Length] };
            int purse = data.StartingMoney;
            float supply = 0;
            bool wastedBait = false;
            double hunger = 0, tiredness = 0;
            int atMinute = (int)Math.Round(data.Config.StartHour * MinutesPerHour);
            var meals = new int[data.Foods.Length];
            var foodOrder = Enumerable.Range(0, data.Foods.Length)
                .OrderBy(i => data.Foods[i].Supply.Price / (double)data.Foods[i].Nutrition.HungerRelief)
                .ThenBy(i => data.Foods[i].Supply.Id, StringComparer.Ordinal).ToArray();
            bool Awake(int minute)
            {
                double hour = (minute % (HoursPerDay * MinutesPerHour)) / (double)MinutesPerHour;
                return hour >= data.Config.WakeHour && hour < data.BedHour;
            }
            void Eat()
            {
                if (!Awake(atMinute)) return;
                foreach (int index in foodOrder)
                    while (meals[index] > 0 && hunger + RationTolerance >= data.Foods[index].Nutrition.HungerRelief)
                    {
                        hunger = Math.Max(0, hunger - data.Foods[index].Nutrition.HungerRelief);
                        meals[index]--;
                    }
            }
            void Advance(int until)
            {
                while (atMinute < until)
                {
                    Eat();
                    hunger = Math.Min(1, hunger + data.HungerRate / MinutesPerHour);
                    tiredness = Awake(atMinute)
                        ? Math.Min(1, tiredness + data.Config.Needs.Tiredness.RisePerGameHour / (double)MinutesPerHour)
                        : Math.Max(0, tiredness - data.Config.Needs.SleepReliefPerGameHour / (double)MinutesPerHour);
                    atMinute++;
                }
                Eat();
            }
            for (int day = 0; day < Days; day++)
            {
                var row = new Day { Number = day + 1 };
                week.Days.Add(row);
                row.Balances.Add(purse);
                bool Spend(int amount)
                {
                    if (amount > purse) return false;
                    purse -= amount;
                    row.Balances.Add(purse);
                    return true;
                }
                void Buy(int index)
                {
                    var purchase = data.Purchases[index];
                    Assert.That(Spend(purchase.Price), Is.True, purchase.Name);
                    row.PurchaseSpend += purchase.Price;
                    row.Purchases.Add(purchase.Name);
                    week.BoughtOn[index] = day + 1;
                }
                if (day == 0 && purse >= data.Purchases[0].Price) Buy(0);
                var ration = CheapestRation(data, day);
                var boughtFood = new int[data.Foods.Length];
                void BuyFood()
                {
                    foreach (int index in foodOrder)
                        while (boughtFood[index] < ration.Counts[index] && Spend(data.Foods[index].Supply.Price))
                        {
                            boughtFood[index]++;
                            meals[index]++;
                            row.FoodPaid += data.Foods[index].Supply.Price;
                        }
                    Eat();
                }
                int wakeMinute = (int)Math.Ceiling((day * HoursPerDay + data.Config.WakeHour) * MinutesPerHour);
                Advance(Math.Max(atMinute, wakeMinute));
                BuyFood();
                var visits = Windows(data, day);
                if (strategy != Strategy.Careful) visits = visits.Take(1).ToList();
                // The missed days and extra lot are the declared careless choices, not balance tunables.
                if (strategy == Strategy.Careless && (day == 1 || day == 4)) visits.Clear();
                if (week.BoughtOn[0] == 0) visits.Clear();
                row.Windows = visits.Count;
                if (day > 0) supply = MarketMath.SettleSupplyDaily(supply, data.Config.MarketDailyRecovery);
                foreach (var visit in visits)
                {
                    Advance(visit.AtMinute);
                    if (EffortGate.Check(EffortClass.Heavy, false, (float)hunger, (float)tiredness, data.Config.Effort) != EffortRefusal.None)
                    { row.Refused++; continue; }
                    row.Clams += visit.Clams;
                    for (int clam = 0; clam < visit.Clams; clam++)
                    {
                        int price = SellPricing.UnitPrice(data.Clam.BaseValue, data.Clam.SupplyElasticity,
                            supply, data.Demand, 1f, data.PriceLevel);
                        row.Income += price;
                        purse += price;
                        supply += data.SupplyPerSale;
                    }
                    row.Balances.Add(purse);
                    if (strategy == Strategy.Careless && !wastedBait && Spend(data.Purchases[2].Price))
                    {
                        wastedBait = true;
                        row.PurchaseSpend += data.Purchases[2].Price;
                        row.Purchases.Add("Unneeded bait");
                    }
                    BuyFood();
                }
                for (int index = 0; index < data.Foods.Length; index++)
                {
                    int missing = ration.Counts[index] - boughtFood[index];
                    row.FoodMissing += missing * data.Foods[index].Supply.Price;
                    row.ReliefMissing += missing * (double)data.Foods[index].Nutrition.HungerRelief;
                }
                Advance((day + 1) * HoursPerDay * MinutesPerHour);
                for (int index = 0; index < data.Purchases.Length; index++)
                {
                    if (week.BoughtOn[index] != 0) continue;
                    if (purse < data.Purchases[index].Price) break;
                    Buy(index);
                }
                row.Purse = purse;
            }
            return week;
        }

        static int RodShortfall(Inputs data, Week ordinary)
            => ordinary.BoughtOn[1] > 0 && ordinary.BoughtOn[1] <= 3 ? 0
                : Math.Max(0, data.Purchases[1].Price - ordinary.Days[2].Purse);

        static string Number(double value) => value.ToString("0.###", Invariant);
        static string Clock(double hour)
        {
            int minute = (int)Math.Round(hour * MinutesPerHour, MidpointRounding.AwayFromZero);
            return (minute / MinutesPerHour).ToString("00", Invariant) + ":" + (minute % MinutesPerHour).ToString("00", Invariant);
        }

        static string LowWaterTimes(Inputs data, int day)
        {
            // Mean-level crossings of the carrier are exact half-periods. Refine against the real model
            // instead of using an old tide table or the disabled analytic bar crest.
            var spans = new List<string>();
            double begin = day * HoursPerDay, end = begin + HoursPerDay;
            double? opened = data.Water(begin) <= data.Tide.MeanLevel ? begin : (double?)null;
            double previous = begin;
            bool wasLow = opened.HasValue;
            for (int minute = 1; minute <= HoursPerDay * MinutesPerHour; minute++)
            {
                double now = begin + minute / (double)MinutesPerHour;
                bool low = data.Water(now) <= data.Tide.MeanLevel;
                if (low != wasLow)
                {
                    double left = previous, right = now;
                    // Sub-second root accuracy; the report rounds to minutes.
                    while (right - left > 1d / (MinutesPerHour * MinutesPerHour))
                    {
                        double middle = (left + right) / 2;
                        if ((data.Water(middle) <= data.Tide.MeanLevel) == wasLow) left = middle;
                        else right = middle;
                    }
                    double crossing = (left + right) / 2;
                    if (low) opened = crossing;
                    else { spans.Add(Clock(opened.Value - begin) + "-" + Clock(crossing - begin)); opened = null; }
                }
                wasLow = low; previous = now;
            }
            if (opened.HasValue) spans.Add(Clock(opened.Value - begin) + "-24:00");
            return string.Join("; ", spans);
        }

        static string Render(Inputs data)
        {
            var weeks = Enum.GetValues(typeof(Strategy)).Cast<Strategy>().Select(s => Play(data, s)).ToArray();
            var careful = weeks[0]; var ordinary = weeks[1]; var careless = weeks[2];
            var b = new StringBuilder();
            void Line(string line = "") => b.Append(line).Append('\n');
            Line("# The first week's purse"); Line();
            Line($"**Known shortfalls:** the ordinary player's rod is {RodShortfall(data, ordinary)} coins short on day 3. " +
                $"The careful player's complete week is {careful.Remaining(data)} coins short on day 7. " +
                $"The shellfish licence is funded on day {ordinary.BoughtOn[0]}. All three cash ledgers stay nonnegative; " +
                $"the careless player leaves {careless.MissingFood} coins of planned food unfunded.");
            Line();
            Line("These are conditional cash budgets for one carried pail per visited tide, not a maximum harvest or a successful playthrough. " +
                "Finite shelf stock, travel and eating access can make the week harder. Proposed hull and repair prices are included; " +
                "a cash milestone does not create an island seller for an unavailable item.");
            Line();
            foreach (var week in weeks)
            {
                Line("## " + week.Strategy); Line();
                Line("| Day | Visits | Refused | Clams sold | Income | Food paid | Food unfunded | Purchases / earmarks | End purse |");
                Line("|---|---:|---:|---:|---:|---:|---:|---|---:|");
                foreach (var d in week.Days)
                    Line($"| {d.Number} | {d.Windows} | {d.Refused} | {d.Clams} | {d.Income} | {d.FoodPaid} | {d.FoodMissing} | " +
                        $"{(d.Purchases.Count == 0 ? "-" : string.Join("; ", d.Purchases))} | {d.Purse} |");
                Line();
                Line($"Seven-day income: {week.Days.Sum(d => d.Income)}. Food paid: {week.Days.Sum(d => d.FoodPaid)}. " +
                    $"Unfunded planned hunger relief: {Number(week.Days.Sum(d => d.ReliefMissing))}. " +
                    $"Remaining cash shortfall, including missed food: {week.Remaining(data)}."); Line();
            }
            Line("## First affordable purchase in the spending order"); Line();
            Line("Food is budgeted each day before progression purchases. Days below include all earlier purchases; '-' means not reached by day 7. " +
                "The careless player's extra bait is wasted and does not replace the planned fishing lot."); Line();
            Line("| Purchase | Coins | Source / availability | Careful | Ordinary | Careless |");
            Line("|---|---:|---|---:|---:|---:|");
            for (int i = 0; i < data.Purchases.Length; i++)
            {
                var p = data.Purchases[i];
                string When(Week week) => week.BoughtOn[i] == 0 ? "-" : week.BoughtOn[i].ToString(Invariant);
                Line($"| {p.Name} | {p.Price} | {p.Availability} | {When(careful)} | {When(ordinary)} | {When(careless)} |");
            }
            Line();
            Line($"Purchase list: {data.Purchases.Sum(p => p.Price)} coins. Starting purse plus the one-time fronted fee: {data.StartingMoney}. " +
                $"Seven-day full ration budget: {Enumerable.Range(0, Days).Sum(day => CheapestRation(data, day).Cost)}. " +
                $"The careful route needs {careful.Remaining(data)} more coins at day 7; at the current one-coin clam price, " +
                "that is the same number of additional saleable clams. Repeated trips within a tide could change this result; this model does not claim they are impossible.");
            Line();
            Line("## The three written strategies"); Line();
            Line($"All start at {Clock(data.Config.StartHour)}, then wake at {Clock(data.Config.WakeHour)} and turn in at {Clock(data.BedHour)} " +
                $"(the configured maximum {Number(data.Config.SleepMaxHours)}-hour sleep). The licence is paid before retaining any clams. " +
                "The placed shovel and pail are collected for free. Setup, walking, hand transfers and selling time are not simulated.");
            Line();
            Line($"- Careful: visit every low-water cycle overlapping waking time, with one load of at most {data.Capacity} clams per visit.");
            Line("- Ordinary: visit the first such window each day, with one load.");
            Line("- Careless: follow the ordinary route, miss days 2 and 5, and spend on one unnecessary bait lot before food as soon as he can. Buy the affordable units of the planned ration; show every unpaid unit as a food shortfall.");
            Line();
            Line("Each visit uses the greatest exposed-hole count sampled during that awake window, capped by one pail. " +
                "A hole yields one clam per tide. Tide cycle ids prevent counting a midnight tail as a fresh tide. " +
                "The player carries and sells each load before the next visit; income is booked on that day. " +
                "This assumes fresh, saleable catch and ordinary price factors. No fish income, gifts beyond the first fee, off-island trade, free working boat or resale windfall is credited.");
            Line();
            Line("Hunger and tiredness advance minute by minute. Food is bought from available cash at waking and after a sale; " +
                "a held food unit is eaten while awake once hunger can use its full relief. Sleep reduces tiredness but hunger keeps rising. " +
                "Each visit occurs at the first minute attaining that window's greatest awake exposure. EffortGate checks the actual needs before awarding any clams. " +
                "Refused visits produce no income. The careless route runs out of food money, and hunger refuses his later visits once the food money is gone.");
            Line();
            Line("## Tide and carry data"); Line();
            Line($"World seed {data.Seed}; start season {(int)data.Config.StartSeason}, day {data.Config.StartDayOfSeason}; " +
                $"{Number(data.Config.SecondsPerDay)} seconds per game day. The tide is seed-independent; the seed is read and recorded, never randomly replaced. " +
                $"Mean {Number(data.Tide.MeanLevel)} m, amplitude {Number(data.Tide.Amplitude)} m, phase {Number(data.Tide.PhaseHours)} h, " +
                $"period {data.Config.TidalPeriodHours.ToString("0.0000", Invariant)} h, lunar month {Number(data.Config.LunarMonthDays)} days, " +
                $"neap fraction {Number(data.Config.NeapAmplitudeFraction)}. Read {data.HoleElevations.Length} placed holes and the carried pail's {data.Capacity}-unit capacity.");
            Line();
            Line("Low water here means at or below the region's mean level, not a safe bar crossing. Times are rounded to minutes. " +
                "Hole exposure uses the shipped painted terrain and TidalExposure; awake counts use one-minute samples. " +
                "The last column lists exposed holes / carried clams for each awake cycle, including evening windows whose minimum falls after bedtime.");
            Line();
            Line("| Day | All low-water spans | Awake exposure / one-load yield |"); Line("|---|---|---|");
            for (int day = 0; day < Days; day++)
                Line($"| {day + 1} | {LowWaterTimes(data, day)} | {string.Join("; ", Windows(data, day).Select(w => w.Exposed + " / " + w.Clams))} |");
            Line();
            Line("## Food and sale arithmetic"); Line();
            Line($"Hunger rises at {data.HungerRate.ToString("0.0000", Invariant)} per hour for the reference body, including sleep: " +
                $"{Number(CheapestRation(data, 0).Need)} on the arrival day and {Number(CheapestRation(data, 1).Need)} per full day. " +
                "The ration is the cheapest whole-item combination covering that day's rise; ties choose less excess relief. " +
                "Unused paid food can be eaten later, but each day's allowance is still purchased in full; hunger relief is never banked above fed. " +
                "Body changes, traits, exercise, calories, stomach capacity and spoilage are outside this base hunger allowance.");
            Line();
            Line("| Food | Price | Hunger relief | Shelf line | Day 1 units | Full-day units |");
            Line("|---|---:|---:|---|---:|---:|");
            for (int i = 0; i < data.Foods.Length; i++)
            {
                var f = data.Foods[i];
                Line($"| {f.Supply.Id} | {f.Supply.Price} | {Number(f.Nutrition.HungerRelief)} | {f.Shelf.Id} | " +
                    $"{CheapestRation(data, 0).Counts[i]} | {CheapestRation(data, 1).Counts[i]} |");
            }
            Line();
            Line($"The island buyer has price level {Number(data.PriceLevel)} and demand {Number(data.Demand)}. " +
                $"A clam has base value {data.Clam.BaseValue}, elasticity {Number(data.Clam.SupplyElasticity)}; " +
                $"each sale adds {Number(data.SupplyPerSale)} supply and daily recovery is {Number(data.Config.MarketDailyRecovery)}. " +
                "SellPricing rounds and floors each sale, with MarketMath carrying the glut across days. " +
                $"One bait lot contains {data.BaitLotSize} units. Digging's EffortGate refuses at hunger " +
                $"{Number(data.Config.Effort.HungryRefuseAt)} or tiredness {Number(data.Config.Effort.TiredRefuseAt)}. " +
                "HeavyWorkNeeds is a separate stage-based requirement, not this numeric gate. Missing meals and refused visits are both reported.");
            Line();
            Line("## Scope and regeneration"); Line();
            Line("This is a price-and-carry budget. It assumes the listed food and bait can be supplied; it does not promise shelf replenishment or subtract islanders' purchases. " +
                "The cod licence has no island listing on this baseline. The dory and material offers are proposals, and physical pail sales and eating still have separate work in progress. " +
                "Travel reach, safe routes, late sales, tide-window throughput, repairs, launch and the complete week need execution proof. " +
                "The deficits above already exist under these optimistic access assumptions. No gameplay, asset, tuning or save change is made or required by this report.");
            Line();
            Line("The inputs are GameConfig, StPeters RegionDef, the scene's wallet/fee/market/pail/holes, the referenced PaintedHeightMap, " +
                "the island seller and shelf definitions, catalogue licence/rod/bait/supply definitions, and FoodDefs. " +
                "Only proposed prices are constants: PR 1539 for the hull and PR 1508 for one plank, one nail packet, one oakum and one paint tin.");
            Line();
            Line("[FirstWeekPurseTests](../../Assets/Tests/EditMode/FirstWeekPurseTests.cs) renders this entire page and compares it byte-for-byte after newline normalization. " +
                "Run its `WriteDocument` editor entry point to regenerate after reviewing changed data. The fixture reuses TideModel, TidalExposure, PaintedHeightMap, " +
                "BodyMath, EffortGate, BaitShop, SellPricing, MarketMath and the existing SceneYaml reader. It complements BaitEconomyPacingTests, TidePacingInvariantTests, " +
                "ClamRefinementsTests and the food content tests. Known target deficits have exact assertions; they are not skipped tests.");
            return b.ToString();
        }

        [MenuItem("Tests/First Week/Write purse document")]
        public static void WriteDocument() => File.WriteAllText(Document, Render(ReadInputs()), new UTF8Encoding(false));

        [Test]
        public void Document_matches_the_complete_data_generated_report()
        {
            string report = Render(ReadInputs());
            TestContext.WriteLine(report);
            Assert.That(File.ReadAllText(Document).Replace("\r\n", "\n"), Is.EqualTo(report),
                "Review the changed figures, then regenerate with FirstWeekPurseTests.WriteDocument.");
        }

        [Test]
        public void Same_seed_and_start_data_produce_the_same_report()
        {
            var first = ReadInputs(); var second = ReadInputs();
            Assert.That(second.Seed, Is.EqualTo(first.Seed));
            Assert.That(Render(second), Is.EqualTo(Render(first)));
        }

        [Test]
        public void Ordinary_can_pay_the_shellfish_licence_before_his_first_catch()
        {
            var data = ReadInputs(); var week = Play(data, Strategy.Ordinary);
            Assert.That(week.BoughtOn[0], Is.EqualTo(1));
            Assert.That(week.Days[0].Balances[1], Is.EqualTo(data.StartingMoney - data.Purchases[0].Price));
        }

        [Test]
        public void Ordinary_rod_by_day_three_has_a_known_exact_shortfall()
        {
            var data = ReadInputs(); int missing = RodShortfall(data, Play(data, Strategy.Ordinary));
            TestContext.WriteLine($"TARGET MISSED: ordinary rod by day 3; missing {missing} coins (target 0).");
            Assert.That(missing, Is.EqualTo(KnownOrdinaryRodShortfall), "Target is zero; review any changed shortfall.");
        }

        [Test]
        public void Careful_complete_week_by_day_seven_has_a_known_exact_shortfall()
        {
            var data = ReadInputs(); int missing = Play(data, Strategy.Careful).Remaining(data);
            TestContext.WriteLine($"TARGET MISSED: careful complete week by day 7; missing {missing} coins (target 0).");
            Assert.That(missing, Is.EqualTo(KnownCarefulWeekShortfall), "Target is zero; review any changed shortfall.");
        }

        [TestCase(Strategy.Careful)]
        [TestCase(Strategy.Ordinary)]
        [TestCase(Strategy.Careless)]
        public void Every_transaction_is_funded_and_cash_is_conserved(Strategy strategy)
        {
            var data = ReadInputs(); var week = Play(data, strategy);
            int previous = data.StartingMoney;
            foreach (var day in week.Days)
            {
                Assert.That(day.Balances, Has.All.GreaterThanOrEqualTo(0));
                Assert.That(day.Purse, Is.EqualTo(previous + day.Income - day.FoodPaid - day.PurchaseSpend));
                Assert.That(day.FoodPaid + day.FoodMissing, Is.EqualTo(CheapestRation(data, day.Number - 1).Cost));
                if (strategy != Strategy.Careless)
                {
                    Assert.That(day.FoodMissing, Is.Zero);
                    Assert.That(day.Refused, Is.Zero);
                }
                previous = day.Purse;
            }
        }

        [Test]
        public void Careless_does_miss_two_days_waste_bait_and_leave_meals_unfunded()
        {
            var data = ReadInputs(); var week = Play(data, Strategy.Careless);
            CollectionAssert.AreEqual(new[] { 2, 5 }, week.Days.Where(d => d.Windows == 0).Select(d => d.Number).ToArray());
            Assert.That(week.Days.Sum(d => d.Purchases.Count(p => p == "Unneeded bait")), Is.EqualTo(1));
            Assert.That(week.MissingFood, Is.EqualTo(36));
            Assert.That(week.Days.Sum(d => d.ReliefMissing), Is.EqualTo(3.95).Within(0.000001));
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0, 1, 1 }, week.Days.Select(d => d.Refused).ToArray());
            Assert.That(week.Days.Sum(d => d.Clams), Is.EqualTo(3 * data.Capacity));
        }

        [Test]
        public void Awake_windows_use_distinct_tides_and_do_not_exceed_one_pail()
        {
            var data = ReadInputs(); var windows = Enumerable.Range(0, Days).SelectMany(d => Windows(data, d)).ToArray();
            Assert.That(windows.Select(w => w.Cycle).Distinct().Count(), Is.EqualTo(windows.Length));
            CollectionAssert.AreEqual(new[] { 2, 2, 2, 2, 2, 2, 1 }, Enumerable.Range(0, Days).Select(d => Windows(data, d).Count).ToArray());
            foreach (var window in windows)
                Assert.That(window.Clams, Is.EqualTo(Math.Min(window.Exposed, data.Capacity)));
        }

        [Test]
        public void Whole_food_rations_cover_hunger_and_pin_the_economical_menu()
        {
            var data = ReadInputs();
            foreach (int day in Enumerable.Range(0, Days))
            {
                var ration = CheapestRation(data, day);
                Assert.That(ration.Relief + RationTolerance, Is.GreaterThanOrEqualTo(ration.Need));
                Assert.That(ration.Cost, Is.EqualTo(day == 0 ? 9 : 14));
                CollectionAssert.AreEqual(day == 0 ? new[] { 0, 0, 3 } : new[] { 0, 1, 4 }, ration.Counts);
            }
        }

        [Test]
        public void No_licence_budget_means_no_retained_clams_or_sale_income()
        {
            var data = ReadInputs(); data.StartingMoney = 0;
            var week = Play(data, Strategy.Ordinary);
            Assert.That(week.Days.Sum(d => d.Clams), Is.Zero);
            Assert.That(week.Days.Sum(d => d.Income), Is.Zero);
            Assert.That(week.BoughtOn, Has.All.EqualTo(0));
        }
    }
}
