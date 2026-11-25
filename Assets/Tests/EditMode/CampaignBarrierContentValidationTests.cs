using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Every flag a campaign barrier waits on is one the game can set.</b> A wall whose
    /// <see cref="CampaignBarrierDef.LiftedByFlag"/> nothing sets can never be lifted; a wall whose
    /// <see cref="CampaignBarrierDef.StandsWhileFlag"/> nothing sets can never stand. Neither is a
    /// compile error or a runtime error, so this test is what says so.
    ///
    /// <para>"Settable" is decided by <see cref="QuestContentValidationTests.SettableFlags"/>: the one
    /// registry of flag ids the shipped content can set. A flag set from a source that registry does
    /// not read yet belongs there, not here.</para>
    ///
    /// <para><see cref="NotYetSettable"/> is the ONE list of known exceptions. It must only shrink: the
    /// change that builds what sets a flag removes its entry, and a stale entry fails
    /// <see cref="NoKnownExceptionOutlivesItsReason"/>.</para>
    /// </summary>
    public class CampaignBarrierContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        /// <summary>Flags a barrier names that nothing sets yet, each with why. Empty is the goal.</summary>
        public static readonly IReadOnlyDictionary<string, string> NotYetSettable = new Dictionary<string, string>
        {
            { "st_peters.first_week", "set when a new game begins its island week, not yet built" },
        };

        private static List<CampaignBarrierDef> Walls() =>
            AssetDatabase.FindAssets("t:CampaignBarrierDef", new[] { DataRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CampaignBarrierDef>)
                .Where(w => w != null).ToList();

        /// <summary>Every (wall, field, flag) a barrier names. Empty fields name nothing.</summary>
        public static IEnumerable<(string wall, string field, string flag)> NamedFlags(IEnumerable<CampaignBarrierDef> walls)
        {
            foreach (var w in walls)
            {
                if (w == null) continue;
                if (!string.IsNullOrEmpty(w.LiftedByFlag))
                    yield return (w.Id, nameof(CampaignBarrierDef.LiftedByFlag), w.LiftedByFlag);
                if (!string.IsNullOrEmpty(w.StandsWhileFlag))
                    yield return (w.Id, nameof(CampaignBarrierDef.StandsWhileFlag), w.StandsWhileFlag);
            }
        }

        /// <summary>The flags no source sets and no exception covers, as readable lines.</summary>
        public static List<string> Problems(IEnumerable<CampaignBarrierDef> walls, ICollection<string> settable,
            ICollection<string> exceptions)
        {
            var bad = new List<string>();
            foreach (var (wall, field, flag) in NamedFlags(walls))
                if (!settable.Contains(flag) && !exceptions.Contains(flag))
                    bad.Add($"{wall}: {field} '{flag}' is set by nothing the flag registry reads");
            return bad;
        }

        [Test]
        public void EveryFlagABarrierWaitsOn_CanBeSet_OrIsANamedException()
        {
            var walls = Walls();
            Assert.IsNotEmpty(walls, "the barrier defs were found");
            var bad = Problems(walls, QuestContentValidationTests.SettableFlags(), NotYetSettable.Keys.ToList());
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void TheIslandWeatherFlag_RequiresTheShippedKeptMorningProducer()
        {
            const string weatherFlag = "st_peters.weather_turned";
            var gates = Resources.LoadAll<StoryGateDef>(StoryGates.ResourcesFolder);
            StoryGateDef[] producers = gates.Where(g => g.MorningSetsFlag == weatherFlag).ToArray();
            Assert.AreEqual(1, producers.Length, "exactly one shipped gate opens the island after sleep");
            StoryGateDef gate = producers[0];
            Assert.AreEqual("st_peters.ginny_word_completed", gate.SetsFlag);
            Assert.AreEqual(weatherFlag, gate.PendingUntilFlag);
            Assert.IsFalse(NotYetSettable.ContainsKey(weatherFlag), "the producer replaces the exception");
            var settable = QuestContentValidationTests.SettableFlags();
            CollectionAssert.Contains(settable, weatherFlag);

            var walls = Walls().Where(w => w.LiftedByFlag == weatherFlag).ToList();
            Assert.AreEqual(4, walls.Count);
            Assert.IsEmpty(Problems(walls, settable, NotYetSettable.Keys.ToList()));
            settable.Remove(weatherFlag);
            Assert.AreEqual(4, Problems(walls, settable, NotYetSettable.Keys.ToList()).Count,
                "removing the producer from the registry must fail every affected sea edge");

            ISaveService previousSave = GameServices.Save;
            bool? previousDemo = DemoBuild.Override;
            System.Func<bool> previousHost = DemoWeek.IsHost;
            var save = new StoryGateTests.FlagSave();
            save.Current.OnboardingFlags = new List<SaveFlag> { new SaveFlag(gate.SetsFlag, true) };
            try
            {
                GameServices.Save = save;
                DemoBuild.Override = false;
                DemoWeek.IsHost = () => true;
                StoryGates.Load(gate);
                StoryGates.ListenForMorning();
                EventBus.Publish(new SleepReachedMorning(GameConfig.DefaultSecondsPerDay));
                Assert.IsTrue(save.GetFlag(weatherFlag), "a declaration alone is not a producer");
                Assert.AreEqual(1, save.Writes[weatherFlag]);
            }
            finally
            {
                StoryGates.Load(null);
                GameServices.Save = previousSave;
                DemoBuild.Override = previousDemo;
                DemoWeek.IsHost = previousHost;
            }
        }

        [Test]
        public void NoKnownExceptionOutlivesItsReason()
        {
            var settable = QuestContentValidationTests.SettableFlags();
            var named = new HashSet<string>(NamedFlags(Walls()).Select(n => n.flag));
            foreach (var e in NotYetSettable)
            {
                Assert.IsNotEmpty(e.Value, e.Key + ": an exception says why");
                Assert.IsFalse(settable.Contains(e.Key),
                    $"'{e.Key}' can be set now: take it off the exceptions list");
                Assert.IsTrue(named.Contains(e.Key),
                    $"'{e.Key}' is named by no barrier any more: take it off the exceptions list");
            }
        }

        [Test]
        public void AFlagNothingSets_IsCaught_AndAnEmptyFieldIsNot()
        {
            var wall = ScriptableObject.CreateInstance<CampaignBarrierDef>();
            try
            {
                wall.Id = "campaign_barrier.fixture";
                wall.LiftedByFlag = "fixture.never_set";
                wall.StandsWhileFlag = "";
                var settable = new HashSet<string> { "fixture.other" };
                var none = new List<string>();
                var bad = Problems(new[] { wall }, settable, none);
                Assert.AreEqual(1, bad.Count, string.Join("\n", bad));
                StringAssert.Contains("fixture.never_set", bad[0]);

                wall.StandsWhileFlag = "fixture.also_never";
                Assert.AreEqual(2, Problems(new[] { wall }, settable, none).Count);
                Assert.AreEqual(1, Problems(new[] { wall }, settable, new List<string> { "fixture.also_never" }).Count);

                settable.Add("fixture.never_set");
                settable.Add("fixture.also_never");
                Assert.IsEmpty(Problems(new[] { wall }, settable, none));
            }
            finally { Object.DestroyImmediate(wall); }
        }
    }
}
