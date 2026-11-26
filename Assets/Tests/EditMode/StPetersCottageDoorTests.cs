using System.IO;
using System.Linq;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>The Whelps cottage door: it can be knocked on, nobody answers while its keeper is indoors, the words
    /// are inner-voice lines a writer can change, and the scene holds the door its step writes.</summary>
    public class StPetersCottageDoorTests
    {
        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "nothing at " + path);
            return asset;
        }

        static InnerVoiceLineDef[] Lines() => AssetDatabase.FindAssets("t:" + nameof(InnerVoiceLineDef))
            .Select(g => AssetDatabase.LoadAssetAtPath<InnerVoiceLineDef>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(l => l != null).ToArray();

        [Test]
        public void TheDoor_IsData_AndEachOfItsAnswersIsALine()
        {
            var door = Load<KnockDoorDef>(WhelpsCottageDoorPath);
            Assert.That(door.Id, Is.EqualTo("door.stp_whelps_cottage"));
            Assert.That(door.VerbLabel, Is.Not.Empty);
            Assert.That(door.IndoorsCueId, Is.Not.EqualTo(door.OutCueId), "indoors and out say different things");
            InnerVoiceLineDef[] lines = Lines();
            foreach (string cue in new[] { door.IndoorsCueId, door.OutCueId })
            {
                InnerVoiceLineDef line = lines.SingleOrDefault(l => l.Trigger == InnerVoiceTrigger.Cue && l.SubjectId == cue);
                Assert.That(line, Is.Not.Null, "no line answers " + cue);
                Assert.That(line.Line, Is.Not.Empty, cue);
                Assert.That(line.OnceOnly, Is.False, "a door can be knocked on more than once: " + cue);
            }
            Assert.That(lines.Single(l => l.SubjectId == door.IndoorsCueId).Line, Does.Contain("Nobody answers"));
        }

        [Test]
        public void AKnock_AtNightWhenSheIsIndoors_IsAnsweredByNobody()
        {
            var def = ScriptableObject.CreateInstance<KnockDoorDef>();
            try
            {
                def.IndoorsCueId = "cue.test.nobody"; def.OutCueId = "cue.test.out";
                Assert.That(def.CueFor(householdIndoors: true), Is.EqualTo("cue.test.nobody"));
                Assert.That(def.CueFor(householdIndoors: false), Is.EqualTo("cue.test.out"));

                var go = new GameObject("door");
                try
                {
                    var door = go.AddComponent<KnockDoor>();
                    Assert.That(door.IsAvailable, Is.False, "a door with no Def offers nothing");
                    door.Configure(def, null);
                    Assert.That(door.IsAvailable, Is.True);
                    Assert.That(door.CueNow, Is.EqualTo("cue.test.out"), "with nobody living there the house is empty, never 'indoors'");
                }
                finally { Object.DestroyImmediate(go); }
            }
            finally { Object.DestroyImmediate(def); }
        }

        [Test]
        public void TheScene_HoldsTheDoor_AtTheCottage_AndItKnowsWhoLivesThere()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            var owned = scene.SubtreeOf(scene.RootNamed(KeepersRootName).FileId);
            var doors = scene.Docs.Where(d => owned.Contains(d.FileId) && d.IsScript(typeof(KnockDoor).FullName)).ToList();
            Assert.That(doors.Count, Is.EqualTo(1), "one door to knock on, the Whelps cottage's");
            long household = doors[0].FieldRef("_household");
            var round = scene.Get(household);
            Assert.That(round, Is.Not.Null, "the door names a keeper that is in the scene");
            Assert.That(round.IsScript(typeof(KeeperRound).FullName), Is.True);
        }
    }
}
