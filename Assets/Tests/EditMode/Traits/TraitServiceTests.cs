using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Traits slice 1: the maths (one trait, two stacked, no traits), the service over the save (pick,
    /// refuse, persist, cache) and the save round trip. Every Def here is made by the test.
    /// </summary>
    public class TraitServiceTests
    {
        private TraitFixtures _fx;
        private TraitFixtures.MemorySave _save;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _fx = new TraitFixtures();
            _save = new TraitFixtures.MemorySave();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            _fx.DestroyAll();
        }

        // ---- maths ------------------------------------------------------------------------------

        [Test]
        public void NoTraits_IsExactlyOne_ForEveryStat()
        {
            foreach (TraitStat stat in System.Enum.GetValues(typeof(TraitStat)))
            {
                Assert.AreEqual(1f, TraitMath.Combine(stat, null));
                Assert.AreEqual(1f, TraitMath.Combine(stat, new List<TraitDef>()));
            }
        }

        [Test]
        public void Apply_WithFactorOne_ReturnsTheValueUntouched()
        {
            foreach (float v in new[] { 0f, 3f, 5.5f, 1.2345678f, -2f, float.MaxValue })
                Assert.AreEqual(System.BitConverter.GetBytes(v), System.BitConverter.GetBytes(TraitMath.Apply(v, 1f)));
        }

        [Test]
        public void OneAdditiveModifier_IsOnePlusValue()
        {
            TraitDef walker = _fx.Make("trait.a", TraitFixtures.Add(TraitStat.WalkSpeed, 0.08f));
            Assert.AreEqual(1.08f, TraitMath.FactorOf(walker, TraitStat.WalkSpeed), 1e-6f);
            Assert.AreEqual(1f, TraitMath.FactorOf(walker, TraitStat.SellPrice), "an untouched stat is exactly 1");
        }

        [Test]
        public void OneMultiplierModifier_IsTheValue()
        {
            TraitDef def = _fx.Make("trait.a", TraitFixtures.Mul(TraitStat.BiteWait, 0.9f));
            Assert.AreEqual(0.9f, TraitMath.FactorOf(def, TraitStat.BiteWait), 1e-6f);
        }

        [Test]
        public void TwoTraits_OnOneStat_AdditivesSum_ThenMultipliersApply()
        {
            TraitDef a = _fx.Make("trait.a", TraitFixtures.Add(TraitStat.SellPrice, 0.05f));
            TraitDef b = _fx.Make("trait.b", TraitFixtures.Add(TraitStat.SellPrice, 0.05f),
                                  TraitFixtures.Mul(TraitStat.SellPrice, 2f));
            // (1 + 0.05 + 0.05) x 2: additives sum, never compound.
            Assert.AreEqual(2.2f, TraitMath.Combine(TraitStat.SellPrice, new[] { a, b }), 1e-5f);
        }

        [Test]
        public void TwoTraits_OnDifferentStats_DoNotTouchEachOther()
        {
            var service = TraitFixtures.Install(_save, _fx.SliceOne(), "trait.speedwalker", "trait.patient_angler");
            Assert.AreEqual(1.08f, service.PlayerMultiplier(TraitStat.WalkSpeed), 1e-6f);
            Assert.AreEqual(0.9f, service.PlayerMultiplier(TraitStat.BiteWait), 1e-6f);
            Assert.AreEqual(1f, service.PlayerMultiplier(TraitStat.SellPrice));
        }

        [Test]
        public void Factor_IsFlooredAtZero()
        {
            TraitDef def = _fx.Make("trait.a", TraitFixtures.Add(TraitStat.BiteWait, -5f));
            Assert.AreEqual(0f, TraitMath.FactorOf(def, TraitStat.BiteWait));
        }

        [Test]
        public void PickOrder_DoesNotChangeTheFactor()
        {
            TraitDef a = _fx.Make("trait.a", TraitFixtures.Mul(TraitStat.WalkSpeed, 1.1f));
            TraitDef b = _fx.Make("trait.b", TraitFixtures.Mul(TraitStat.WalkSpeed, 1.07f));
            var service = new TraitService(() => _save, () => new[] { a, b });
            Assert.AreEqual(
                System.BitConverter.GetBytes(service.MultiplierFor(TraitStat.WalkSpeed, new[] { "trait.a", "trait.b" })),
                System.BitConverter.GetBytes(service.MultiplierFor(TraitStat.WalkSpeed, new[] { "trait.b", "trait.a" })));
        }

        [Test]
        public void BenefitDirection_FollowsTheStat()
        {
            Assert.IsTrue(TraitMath.IsBenefit(TraitStat.WalkSpeed, 1.08f));
            Assert.IsFalse(TraitMath.IsBenefit(TraitStat.WalkSpeed, 0.95f));
            Assert.IsTrue(TraitMath.IsBenefit(TraitStat.BiteWait, 0.9f));
            Assert.IsFalse(TraitMath.IsBenefit(TraitStat.BiteWait, 1.1f));
            Assert.IsFalse(TraitMath.IsBenefit(TraitStat.SellPrice, 1f), "no change is not a bonus");
        }

        // ---- the service over the save ----------------------------------------------------------

        [Test]
        public void NoSave_OrNoTraits_IsExactlyOne_AndNeverLoadsTheCatalog()
        {
            int loads = 0;
            var noSave = new TraitService(() => null, () => { loads++; return null; });
            Assert.AreEqual(1f, noSave.PlayerMultiplier(TraitStat.WalkSpeed));
            var emptySave = new TraitService(() => _save, () => { loads++; return null; });
            Assert.AreEqual(1f, emptySave.PlayerMultiplier(TraitStat.SellPrice));
            Assert.AreEqual(0, loads, "a player without traits must not pay for a Resources load");
        }

        [Test]
        public void DefaultService_WithNoSave_IsIdentity()
        {
            GameServices.Save = null;
            Assert.AreEqual(1f, GameServices.Traits.PlayerMultiplier(TraitStat.BiteWait));
        }

        [Test]
        public void UnknownAndRepeatedIds_AreIgnored_InTheMaths()
        {
            var service = TraitFixtures.Install(_save, _fx.SliceOne(), "trait.retired", "trait.speedwalker");
            Assert.AreEqual(1.08f, service.PlayerMultiplier(TraitStat.WalkSpeed), 1e-6f);
            Assert.AreEqual(1.08f, service.MultiplierFor(TraitStat.WalkSpeed,
                new[] { "trait.speedwalker", "trait.speedwalker" }), 1e-6f, "a repeat never stacks");
        }

        [Test]
        public void MoreThanThePicks_OnlyTheFirstPicksCount()
        {
            List<TraitDef> all = _fx.SliceOne();
            var service = new TraitService(() => _save, () => all);
            // Two picks (the owner's ruling): the third id in a hand-edited save is not counted.
            Assert.AreEqual(1f, service.MultiplierFor(TraitStat.BiteWait,
                new[] { "trait.speedwalker", "trait.charismatic", "trait.patient_angler" }));
        }

        [Test]
        public void SetPlayerTraits_KeepsTwo_SavesOnce_Announces_AndUpdatesTheFactor()
        {
            var service = TraitFixtures.Install(_save, _fx.SliceOne());
            int announced = 0;
            System.Action<PlayerTraitsChanged> h = _ => announced++;
            EventBus.Subscribe(h);
            try
            {
                Assert.AreEqual(1f, service.PlayerMultiplier(TraitStat.SellPrice));
                Assert.IsTrue(service.SetPlayerTraits(new[] { "trait.charismatic", "trait.speedwalker" }, out string refusal), refusal);
                CollectionAssert.AreEqual(new[] { "trait.charismatic", "trait.speedwalker" }, _save.Current.PlayerTraits);
                Assert.AreEqual(1.05f, service.PlayerMultiplier(TraitStat.SellPrice), 1e-6f, "the cache follows the pick");
                Assert.AreEqual(1, _save.Saves);
                Assert.AreEqual(1, announced);

                Assert.IsTrue(service.SetPlayerTraits(new[] { "trait.charismatic", "trait.speedwalker" }, out refusal));
                Assert.AreEqual(1, _save.Saves, "the same picks write nothing");
                Assert.AreEqual(1, announced);
            }
            finally { EventBus.Unsubscribe(h); }
        }

        [Test]
        public void SetPlayerTraits_Refuses_Unknown_Repeat_TooMany_AndNoSave()
        {
            var service = TraitFixtures.Install(_save, _fx.SliceOne());
            Assert.IsFalse(service.SetPlayerTraits(new[] { "trait.nope" }, out string r1)); Assert.IsNotEmpty(r1);
            Assert.IsFalse(service.SetPlayerTraits(new[] { "trait.charismatic", "trait.charismatic" }, out string r2)); Assert.IsNotEmpty(r2);
            Assert.IsFalse(service.SetPlayerTraits(
                new[] { "trait.speedwalker", "trait.charismatic", "trait.patient_angler" }, out string r3)); Assert.IsNotEmpty(r3);
            Assert.AreEqual(0, _save.Saves);
            Assert.IsEmpty(_save.Current.PlayerTraits);

            var noSave = new TraitService(() => null, () => _fx.SliceOne());
            Assert.IsFalse(noSave.SetPlayerTraits(new[] { "trait.speedwalker" }, out string r4)); Assert.IsNotEmpty(r4);
        }

        [Test]
        public void SetPlayerTraits_Empty_ClearsThem()
        {
            var service = TraitFixtures.Install(_save, _fx.SliceOne(), "trait.speedwalker");
            Assert.IsTrue(service.SetPlayerTraits(new string[0], out string refusal), refusal);
            Assert.IsEmpty(_save.Current.PlayerTraits);
            Assert.AreEqual(1f, service.PlayerMultiplier(TraitStat.WalkSpeed));
        }

        [Test]
        public void ASwappedSave_IsPickedUp_WithoutAnyEvent()
        {
            var service = TraitFixtures.Install(_save, _fx.SliceOne(), "trait.speedwalker");
            Assert.AreEqual(1.08f, service.PlayerMultiplier(TraitStat.WalkSpeed), 1e-6f);
            _save.Current.PlayerTraits = new List<string> { "trait.charismatic" };
            Assert.AreEqual(1f, service.PlayerMultiplier(TraitStat.WalkSpeed));
            Assert.AreEqual(1.05f, service.PlayerMultiplier(TraitStat.SellPrice), 1e-6f);
        }

        [Test]
        public void All_IsOrdinalById_AndFindResolves()
        {
            var service = new TraitService(() => _save, () => _fx.SliceOne());
            Assert.AreEqual(3, service.All.Count);
            Assert.AreEqual("trait.charismatic", service.All[0].Id);
            Assert.AreEqual("trait.patient_angler", service.All[1].Id);
            Assert.AreEqual("trait.speedwalker", service.All[2].Id);
            Assert.AreSame(service.All[2], service.Find("trait.speedwalker"));
            Assert.IsNull(service.Find("trait.nope"));
            Assert.IsNull(service.Find(null));
        }

        // ---- the save ---------------------------------------------------------------------------

        [Test]
        public void Traits_SurviveASaveRoundTrip()
        {
            SaveData save = SaveMigration.NewGame();
            save.PlayerTraits.Add("trait.speedwalker");
            save.PlayerTraits.Add("trait.patient_angler");
            SaveData back = SaveSerialization.FromJson(SaveSerialization.ToJson(save));
            CollectionAssert.AreEqual(new[] { "trait.speedwalker", "trait.patient_angler" }, back.PlayerTraits);
        }

        [Test]
        public void ANewGame_HasNoTraits()
        {
            Assert.IsNotNull(SaveMigration.NewGame().PlayerTraits);
            Assert.IsEmpty(SaveMigration.NewGame().PlayerTraits);
        }
    }
}
