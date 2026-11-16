using System.Collections.Generic;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// "Too tired / too hungry" (owner ask 2026-11-15): the one pure check heavy work asks before it starts.
    /// Each reason, both, the classes that never refuse, work under way never interrupted, a zeroed block
    /// never locking the player out, the line said once per cooldown, and sitting resting a little. The
    /// numbers are the shipped code default (<see cref="EffortSettings.Default"/>) unless a test says so.
    /// </summary>
    public class EffortGateTests
    {
        private const double Hour = 100d;
        private static EffortSettings S => EffortSettings.Default;

        private readonly List<EffortRefused> _refused = new List<EffortRefused>();
        private readonly List<AmbientSpeechRequested> _spoken = new List<AmbientSpeechRequested>();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EffortGate.ResetSpeech();
            EventBus.Clear<EffortRefused>();
            EventBus.Clear<AmbientSpeechRequested>();
            EventBus.Subscribe<EffortRefused>(OnRefused);
            EventBus.Subscribe<AmbientSpeechRequested>(OnSpoken);
            _refused.Clear();
            _spoken.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<EffortRefused>(OnRefused);
            EventBus.Unsubscribe<AmbientSpeechRequested>(OnSpoken);
            EventBus.Clear<EffortRefused>();
            EventBus.Clear<AmbientSpeechRequested>();
            EffortGate.ResetSpeech();
            GameServices.Reset();
        }

        private void OnRefused(EffortRefused e) => _refused.Add(e);
        private void OnSpoken(AmbientSpeechRequested e) => _spoken.Add(e);

        private static EffortRefusal Heavy(float hunger, float tiredness)
            => EffortGate.Check(EffortClass.Heavy, false, hunger, tiredness, S);

        // ---- the reasons ---------------------------------------------------------------------------

        [Test]
        public void WornOut_RefusesHeavyWork_AsTired()
        {
            Assert.AreEqual(EffortRefusal.Tired, Heavy(0f, S.TiredRefuseAt));
            Assert.AreEqual(EffortRefusal.Tired, Heavy(0.5f, 1f));
            Assert.AreEqual(EffortRefusal.None, Heavy(0f, S.TiredRefuseAt - 0.01f), "just short of worn out works");
        }

        [Test]
        public void HungerAlone_RefusesOnlyAtTheExtremeFloor()
        {
            Assert.AreEqual(EffortRefusal.Hungry, Heavy(S.HungryRefuseAt, 0f));
            Assert.AreEqual(EffortRefusal.Hungry, Heavy(1f, 0f));
            Assert.AreEqual(EffortRefusal.None, Heavy(S.HungryRefuseAt - 0.01f, 0f), "hunger slows, never stops");
            Assert.AreEqual(EffortRefusal.None, Heavy(NeedsSettings.Default.Hunger.StrongAt, 0f),
                            "the 'hungry' stage alone still works");
        }

        [Test]
        public void TiredAndHungry_IsBoth()
        {
            Assert.AreEqual(EffortRefusal.Both, Heavy(1f, 1f));
            Assert.AreEqual(EffortRefusal.Both, Heavy(S.HungryAlsoSaidAt, S.TiredRefuseAt),
                            "when tiredness refuses, real hunger is named alongside");
            Assert.AreEqual(EffortRefusal.Tired, Heavy(S.HungryAlsoSaidAt - 0.01f, S.TiredRefuseAt),
                            "a little peckish is not named");
        }

        [Test]
        public void FedAndRested_IsAllowed()
        {
            Assert.AreEqual(EffortRefusal.None, Heavy(0f, 0f));
        }

        // ---- what never refuses --------------------------------------------------------------------

        [Test]
        public void LightAndModerateWork_NeverRefuse_ShippedLean()
        {
            Assert.IsFalse(S.RefuseLight);
            Assert.IsFalse(S.RefuseModerate);
            Assert.IsTrue(S.RefuseHeavy);
            Assert.AreEqual(EffortRefusal.None, EffortGate.Check(EffortClass.Light, false, 1f, 1f, S),
                            "walking, eating, sleeping, steering: anything needed to get home");
            Assert.AreEqual(EffortRefusal.None, EffortGate.Check(EffortClass.Moderate, false, 1f, 1f, S));
        }

        [Test]
        public void WorkUnderway_IsNeverInterrupted()
        {
            Assert.AreEqual(EffortRefusal.None, EffortGate.Check(EffortClass.Heavy, true, 1f, 1f, S));
        }

        [Test]
        public void AZeroedBlock_NeverRefuses()
        {
            var zeroed = new EffortSettings { RefuseHeavy = true };
            Assert.AreEqual(EffortRefusal.None, EffortGate.Check(EffortClass.Heavy, false, 1f, 1f, zeroed),
                            "a threshold at 0 means never, not always");
        }

        [Test]
        public void WhichClassesRefuse_IsData()
        {
            EffortSettings s = S;
            s.RefuseLight = true;
            s.RefuseHeavy = false;
            Assert.AreEqual(EffortRefusal.Tired, EffortGate.Check(EffortClass.Light, false, 0f, 1f, s));
            Assert.AreEqual(EffortRefusal.None, EffortGate.Check(EffortClass.Heavy, false, 0f, 1f, s));
        }

        [Test]
        public void TheCheck_IsDeterministic()
        {
            for (float t = 0f; t <= 1f; t += 0.05f)
            for (float h = 0f; h <= 1f; h += 0.05f)
                Assert.AreEqual(Heavy(h, t), Heavy(h, t));
        }

        // ---- asking, refusing and saying -----------------------------------------------------------

        private static NeedsService NeedsAt(float hunger, float tiredness)
        {
            var save = new TraitFixtures.MemorySave();
            save.Current.PlayerNeeds = new PlayerNeedsDto
            {
                Hunger = new NeedStateDto(hunger, 0d),
                Tiredness = new NeedStateDto(tiredness, 0d),
            };
            var clock = new FoodFeedsBodyTests.ZeroClock();
            return new NeedsService(() => save, () => clock, () => NeedsSettings.Default,
                                    () => new TraitService(() => save, () => new TraitDef[0]), () => Hour);
        }

        [Test]
        public void TryBegin_Rested_GoesAhead_AndSaysNothing()
        {
            Assert.IsTrue(EffortGate.TryBegin(EffortClass.Heavy, EffortGate.ClamDigId, NeedsAt(0.2f, 0.3f)));
            Assert.IsEmpty(_refused);
            Assert.IsEmpty(_spoken);
        }

        [Test]
        public void TryBegin_WornOut_Refuses_PublishesOnce_AndThePlayerSaysTheLine()
        {
            Assert.IsFalse(EffortGate.TryBegin(EffortClass.Heavy, EffortGate.TrapHaulId, NeedsAt(0.1f, 0.95f)));

            Assert.AreEqual(1, _refused.Count);
            Assert.AreEqual(EffortRefusal.Tired, _refused[0].Reason);
            Assert.AreEqual(EffortGate.TrapHaulId, _refused[0].ActionId);
            Assert.AreEqual(1, _spoken.Count, "said, through the ambient bubble");
            Assert.AreEqual("I'm too tired.", _spoken[0].Line);
            Assert.AreEqual(AmbientSpeechKind.InnerVoice, _spoken[0].Kind, "her own voice, no name chip");
        }

        [Test]
        public void TryBegin_LightWork_NeverRefuses_EvenWornOutAndStarving()
        {
            Assert.IsTrue(EffortGate.TryBegin(EffortClass.Light, null, NeedsAt(1f, 1f)));
            Assert.IsEmpty(_refused);
        }

        [Test]
        public void TheLines_MatchTheOwnersWords()
        {
            Assert.AreEqual("I'm too tired.", EffortGate.LineFor(EffortRefusal.Tired, S));
            Assert.AreEqual("I'm too hungry.", EffortGate.LineFor(EffortRefusal.Hungry, S));
            Assert.AreEqual("I'm too tired and too hungry.", EffortGate.LineFor(EffortRefusal.Both, S));
            Assert.IsNull(EffortGate.LineFor(EffortRefusal.None, S));
        }

        [Test]
        public void TheLine_IsSaidOncePerCooldown()
        {
            Assert.IsTrue(EffortGate.Say(EffortRefusal.Both, S, 10f));
            Assert.IsFalse(EffortGate.Say(EffortRefusal.Both, S, 10f + S.LineCooldownSeconds * 0.5f), "a held key");
            Assert.IsTrue(EffortGate.Say(EffortRefusal.Both, S, 10f + S.LineCooldownSeconds), "after the cooldown");
            Assert.IsFalse(EffortGate.Say(EffortRefusal.None, S, 1000f), "nothing to say");
            Assert.AreEqual(2, _spoken.Count);
            Assert.AreEqual("I'm too tired and too hungry.", _spoken[0].Line);
        }

        // ---- sitting -------------------------------------------------------------------------------

        [Test]
        public void Sitting_RestsAtAQuarterOfSleep_AndNeverBelowTheFloor()
        {
            float sleep = NeedsSettings.Default.SleepReliefPerGameHour;
            Assert.AreEqual(0.25f, S.SitReliefShareOfSleep);

            float oneHour = EffortGate.TirednessAfterSitting(0.9f, 1d, sleep, S);
            Assert.AreEqual(0.9f - sleep * 0.25f, oneHour, 1e-6f, "an hour sat is a quarter of an hour slept");

            Assert.AreEqual(S.SitRestFloor, EffortGate.TirednessAfterSitting(0.9f, 100d, sleep, S),
                            "only sleep clears the rest");
            Assert.AreEqual(0.3f, EffortGate.TirednessAfterSitting(0.3f, 5d, sleep, S),
                            "under the floor already: sitting changes nothing");
            Assert.AreEqual(0.9f, EffortGate.TirednessAfterSitting(0.9f, 5d, 0f, S), "no sleep rate, no rest");
            Assert.AreEqual(0.9f, EffortGate.TirednessAfterSitting(0.9f, 0d, sleep, S), "no time sat, no rest");
        }

        // ---- the shipped asset ---------------------------------------------------------------------

        [Test]
        public void ShippedConfig_CarriesTheEffortBlock_AsAuthored()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(config);
            EffortSettings e = config.Effort;
            Assert.IsFalse(e.RefuseLight);
            Assert.IsFalse(e.RefuseModerate);
            Assert.IsTrue(e.RefuseHeavy);
            Assert.Greater(e.TiredRefuseAt, 0f);
            Assert.GreaterOrEqual(e.HungryRefuseAt, e.TiredRefuseAt, "hunger alone refuses later than tiredness");
            Assert.AreEqual("I'm too tired.", e.TiredLine);
            Assert.AreEqual("I'm too hungry.", e.HungryLine);
            Assert.AreEqual("I'm too tired and too hungry.", e.BothLine);
            Assert.That(e.SitRestFloor, Is.InRange(0f, 1f));
        }

        [Test]
        public void Settings_FallBackToTheDefault_WithNoConfig()
        {
            GameServices.Config = null;
            Assert.AreEqual(EffortSettings.Default.TiredRefuseAt, EffortGate.Settings.TiredRefuseAt);
            Assert.AreEqual(EffortSettings.Default.TiredLine, EffortGate.Settings.TiredLine);
        }
    }
}
