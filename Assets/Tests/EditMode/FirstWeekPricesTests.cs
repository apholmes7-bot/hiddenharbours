using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The first week's prices, paced like a farming game</b> (owner, 2026-11-25: "the grind should feel
    /// stardew valley paced"; "cod license cheaper but dory still more because fishing cod should earn more
    /// money"). Every number here is read off the SHIPPED assets and priced through the same
    /// <see cref="SellPricing"/> the till uses, so a re-tune of any Def or <see cref="GameConfig"/> value is
    /// checked against the week it has to pay for. The page that explains the numbers is
    /// <c>docs/design/first-week-purse-choices.md</c>; its day tables are the model below.
    ///
    /// <para><b>What is a model assumption, not data</b> (each is marked on the page as a guess): a bucket is
    /// 20 clams per low tide; a "cod day" is three dory loads (the dory's hold, three times), every cod landed
    /// on one capelin; a short rod session from the shore is five mackerel; food is 9 coins on the arrival
    /// day and 14 a day after; Basil's dory is 80 (it lives in an open draft, not on this base).</para>
    /// </summary>
    public class FirstWeekPricesTests
    {
        private const string Data = "Assets/_Project/Data/";

        // ---- model assumptions (guesses, see the class summary) ---------------------------------------
        private const int ClamsPerBucket = 20;
        private const int DoryLoadsPerCodDay = 3;
        private const int ShoreFishPerSession = 5;
        private const int BasilsDoryPrice = 80;     // slipsale.basils_dory, open draft 1539
        private const int ArrivalDayFood = 9;       // three tins of beans (purse page section 4)
        private const int FullDayFood = 14;         // four tins and an oatcake
        private const int Days = 7;

        private static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(Data + path);
            Assert.IsNotNull(a, $"shipped asset {path}");
            return a;
        }

        private static GameConfig Config => Load<GameConfig>("Config/GameConfig.asset");
        private static FishSpeciesDef Clam => Load<FishSpeciesDef>("Fish/SoftShellClam.asset");
        private static FishSpeciesDef Cod => Load<FishSpeciesDef>("Fish/AtlanticCod.asset");
        private static FishSpeciesDef Mackerel => Load<FishSpeciesDef>("Fish/Mackerel.asset");
        private static FishSpeciesDef Flounder => Load<FishSpeciesDef>("Fish/WinterFlounder.asset");
        private static BaitDef Capelin => Load<BaitDef>("Resources/Catalog/Bait/Capelin.asset");
        private static int DoryHold => Load<BoatHullDef>("Boats/Dory.asset").HoldUnits;

        private static float IslandLevel => Config.MarketPriceLevelStPetersStore;
        private static float IslandDemand => Config.MarketDemandStPetersStore;

        private static int AtIsland(FishSpeciesDef f, float supply, int n, float needs = 1f)
            => SellPricing.RunningTotal(f.BaseValue, f.SupplyElasticity, supply, n, IslandDemand, IslandLevel * needs);

        private static int AtCreek(FishSpeciesDef f, float supply, int n)
            => SellPricing.RunningTotal(f.BaseValue, f.SupplyElasticity, supply, n,
                                        Config.MarketDemandNineMileCreek, Config.MarketPriceLevelNineMileCreek);

        private static int TwoSettledBuckets()
            => AtIsland(Clam, ClamsPerBucket, ClamsPerBucket) + AtIsland(Clam, 2 * ClamsPerBucket, ClamsPerBucket);

        // ---- 1. clams pay two coins each ---------------------------------------------------------------

        [Test]
        public void AClamAtLeBlancs_NeverFetchesLessThanTwo_FedOrAtTheHungerFloor()
        {
            float hungry = Config.Needs.SellFloor;
            for (int s = 0; s <= 200; s++)
            {
                Assert.GreaterOrEqual(SellPricing.UnitPrice(Clam.BaseValue, Clam.SupplyElasticity, s, IslandDemand, 1f, IslandLevel), 2,
                    $"a fed digger is paid under 2 a clam at supply {s}");
                Assert.GreaterOrEqual(SellPricing.UnitPrice(Clam.BaseValue, Clam.SupplyElasticity, s, IslandDemand, 1f, IslandLevel * hungry), 2,
                    $"a hungry digger (sell floor {hungry}) is paid under 2 a clam at supply {s}");
            }
            Assert.AreEqual(2, SellPricing.UnitPrice(Clam.BaseValue, Clam.SupplyElasticity, 1000f, IslandDemand, 1f, IslandLevel),
                "a glutted counter settles at exactly 2 a clam: the honest 'two coins a clam' the week is built on");
        }

        // ---- 2. where you sell still matters -----------------------------------------------------------

        [Test]
        public void TheCreekStillPaysMore_ForAFreshBucket_AndForAFreshCodDay()
        {
            int codDay = DoryHold * DoryLoadsPerCodDay;
            Assert.Less(AtIsland(Clam, 0f, ClamsPerBucket), AtCreek(Clam, 0f, ClamsPerBucket), "a bucket pays more across the bar");
            Assert.Less(AtIsland(Cod, 0f, codDay), AtCreek(Cod, 0f, codDay), "a day of cod pays more across the bar");
        }

        // ---- 3. cod from the dory earns clearly more than clams ----------------------------------------

        [Test]
        public void AGoodCodDay_BeatsTwoBucketsWidely_AndThePlusPaysForTheDoryWithinThreeDays()
        {
            int codDay = DoryHold * DoryLoadsPerCodDay;
            int bait = codDay * Capelin.Price;                     // one capelin per landed fish
            int clams = TwoSettledBuckets();
            float supply = 0f, recover = Config.MarketDailyRecovery;
            int cumulativeMargin = 0, paidBackOnDay = 0;
            var log = new StringBuilder($"[first-week] two buckets on a settled counter: {clams}\n");
            for (int day = 1; day <= 4; day++)
            {
                int net = AtIsland(Cod, supply, codDay) - bait;
                cumulativeMargin += net - clams;
                if (paidBackOnDay == 0 && cumulativeMargin >= BasilsDoryPrice) paidBackOnDay = day;
                log.AppendLine($"[first-week] cod day {day} (supply {supply:0.##}): net {net}, over clams {net - clams}, running {cumulativeMargin}");
                if (day == 1)
                    Assert.GreaterOrEqual(net, 1.4f * clams, "one good cod day must beat two buckets by a wide margin");
                if (day == 2)
                    Assert.Greater(net, clams, "a second straight cod day at the same counter still beats the clams");
                supply = MarketMath.SettleSupplyDaily(supply + codDay, recover);
            }
            Debug.Log(log.ToString());
            Assert.That(paidBackOnDay, Is.InRange(1, 3),
                "what cod earns over clams pays for Basil's dory in about two or three days of fishing");
        }

        [Test]
        public void RodFishFromTheShore_BeatTheirBait()
        {
            foreach (var f in new[] { Mackerel, Flounder })
                Assert.Greater(AtIsland(f, 0f, ShoreFishPerSession), ShoreFishPerSession * Capelin.Price,
                    $"{f.Id}: a short session from the shore earns more than its bait");
        }

        // ---- 4. the week -------------------------------------------------------------------------------

        private enum Act { Bucket, DoryLoad, ShoreSession }

        private sealed class Week
        {
            public int Money = Config.FrontedLicenceFee;           // wallet 0 + Ginny's fronted fee
            public readonly List<string> Owned = new();
            public int Bait;
            public bool Has(string n) => Owned.Contains(n);
            public bool Ready => Has("dory") && Has("rod") && Has("cod licence");
        }

        private static (string name, int price)[] Purchases()
        {
            int clamLicence = Load<LicenseDef>("Resources/Catalog/Licenses/ClamLicense.asset").Price;
            int codLicence = Load<LicenseDef>("Resources/Catalog/Licenses/CodLicense.asset").Price;
            int rod = Load<GearOffer>("Resources/Catalog/Gear/Rod.asset").Price;
            return new[] { ("clam licence", clamLicence), ("rod", rod), ("bait", Capelin.Price * Capelin.LotSize),
                           ("dory", BasilsDoryPrice), ("cod licence", codLicence) };
        }

        /// <summary>Runs the week: buy in order as soon as the purse covers it and still keeps the day's food
        /// (the clam licence is bought from Ginny's fronted fee before any clam is kept), sell each load as it
        /// comes in, settle the counter at each dawn. Returns the purse on day 7's night and what is unbought.</summary>
        private static (int purse, int unbought, int allBoughtOnDay) RunWeek(System.Func<int, Week, Act[]> plan, StringBuilder log)
        {
            var w = new Week();
            var order = Purchases();
            float shell = 0f, ground = 0f, pelagic = 0f;
            int allBoughtOnDay = 0;
            for (int day = 1; day <= Days; day++)
            {
                int food = day == 1 ? ArrivalDayFood : FullDayFood;
                if (day > 1)
                {
                    shell = MarketMath.SettleSupplyDaily(shell, Config.MarketDailyRecovery);
                    ground = MarketMath.SettleSupplyDaily(ground, Config.MarketDailyRecovery);
                    pelagic = MarketMath.SettleSupplyDaily(pelagic, Config.MarketDailyRecovery);
                }
                void Buy()
                {
                    while (w.Owned.Count < order.Length)
                    {
                        var (name, price) = order[w.Owned.Count];
                        int reserve = name == "clam licence" ? 0 : food;
                        if (w.Money - price < reserve) break;
                        w.Money -= price; w.Owned.Add(name);
                        if (name == "bait") w.Bait += Capelin.LotSize;
                    }
                }
                void TakeBait(int n)
                {
                    while (w.Bait < n) { w.Money -= Capelin.Price * Capelin.LotSize; w.Bait += Capelin.LotSize; }
                    w.Bait -= n;
                }
                Buy();
                foreach (Act a in plan(day, w))
                {
                    switch (a)
                    {
                        case Act.Bucket when w.Has("clam licence"):
                            w.Money += AtIsland(Clam, shell, ClamsPerBucket); shell += ClamsPerBucket; break;
                        case Act.DoryLoad when w.Ready:
                            TakeBait(DoryHold); w.Money += AtIsland(Cod, ground, DoryHold); ground += DoryHold; break;
                        case Act.ShoreSession when w.Has("rod") && w.Has("bait"):
                            TakeBait(ShoreFishPerSession); w.Money += AtIsland(Mackerel, pelagic, ShoreFishPerSession);
                            pelagic += ShoreFishPerSession; break;
                    }
                    Buy();
                }
                w.Money -= food;
                if (allBoughtOnDay == 0 && w.Owned.Count == order.Length) allBoughtOnDay = day;
                log.AppendLine($"[first-week] day {day}: purse {w.Money}, holds {string.Join(", ", w.Owned)}");
            }
            int unbought = 0;
            for (int i = w.Owned.Count; i < order.Length; i++) unbought += order[i].price;
            return (w.Money, unbought, allBoughtOnDay);
        }

        [Test]
        public void TheOrdinaryWeekCloses_AndTheCarefulWeekEndsWithMoneyToSpare()
        {
            var log = new StringBuilder();

            // Ordinary: one bucket a day; once he has the rod and bait, a short session from the shore;
            // once he holds the dory and the cod licence, one dory load instead.
            log.AppendLine("[first-week] ordinary");
            var ordinary = RunWeek((day, w) =>
                w.Ready ? new[] { Act.Bucket, Act.DoryLoad }
                : w.Has("rod") && w.Has("bait") ? new[] { Act.Bucket, Act.ShoreSession }
                : new[] { Act.Bucket }, log);

            // Careful: both daylight low tides (one on day 7) until he holds the dory and the cod licence,
            // then the morning bucket and a cod day.
            log.AppendLine("[first-week] careful");
            var careful = RunWeek((day, w) =>
                w.Ready ? new[] { Act.Bucket, Act.DoryLoad, Act.DoryLoad, Act.DoryLoad }
                : day < Days ? new[] { Act.Bucket, Act.Bucket } : new[] { Act.Bucket }, log);
            Debug.Log(log.ToString());

            Assert.AreEqual(0, ordinary.unbought, "the ordinary player buys every forced purchase by the word");
            Assert.GreaterOrEqual(ordinary.purse, 0, "and is never in the red at the end");
            Assert.AreEqual(0, careful.unbought, "the careful player buys everything");
            Assert.Greater(careful.purse, BasilsDoryPrice, "and ends the week with real money to spare");
            Assert.Less(careful.allBoughtOnDay, ordinary.allBoughtOnDay, "care is rewarded: he finishes sooner");
        }
    }
}
