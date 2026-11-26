using System.IO;
using System.Linq;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>The two keepers who live away from the village: each is data with a stable id, each has a day that
    /// resolves on their own post, and the scene holds exactly what their step would write.</summary>
    public class StPetersKeepersTests
    {
        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "nothing at " + path);
            return asset;
        }

        [Test]
        public void TheKeepers_AreData_WithStableIds()
        {
            var alban = Load<NpcDef>(KeepersNpcFolder + "/AlbanDoucet.asset");
            var odile = Load<NpcDef>(KeepersNpcFolder + "/OdileMarchand.asset");
            Assert.That(alban.Id, Is.EqualTo("npc.alban_doucet"));
            Assert.That(odile.Id, Is.EqualTo("npc.odile_marchand"));
            foreach (NpcDef npc in new[] { alban, odile })
            {
                Assert.That(npc.DisplayName, Is.Not.Empty);
                Assert.That(npc.HasDialogue, Is.True, npc.Id + " has nothing to say");
                Assert.That(npc.Build, Is.Not.Null, npc.Id + " has no build to draw");
            }
            Assert.That(LightKeeperAnchorId, Is.EqualTo("anchor.stp_ne_light.tower_door"));
            Assert.That(WhelpsCottageAnchorId, Is.EqualTo("anchor.stp_whelps.cottage_door"));
        }

        [Test]
        public void EachKeepersDay_NamesOnlyTheirOwnPost_AndRunsInOrderRoundTheClock()
        {
            foreach (KeeperPost post in LoadKeeperPosts())
            {
                var routine = Load<RoutineDef>(KeepersFolder + "/" + post.RoutineAsset + ".asset");
                Assert.That(routine.IsWalkable, Is.True, routine.Id);
                Assert.That(routine.RegionId, Is.EqualTo("region.st_peters"));
                Assert.That(routine.Npc, Is.SameAs(Load<NpcDef>(KeepersNpcFolder + "/" + post.NpcAsset + ".asset")));
                Assert.That(routine.SpansRegions, Is.False, routine.Id + " never leaves the island");
                string[] stations = post.Stops.Select(s => s.StationId).ToArray();
                Assert.That(stations, Is.Unique);
                float last = -1f;
                foreach (RoutineEntry entry in routine.Entries)
                {
                    Assert.That(stations, Does.Contain(entry.StationId), routine.Id);
                    Assert.That(entry.StartHour, Is.GreaterThan(last).And.LessThan(24f), routine.Id + " runs out of order");
                    last = entry.StartHour;
                }
                Assert.That(post.Stops.Count(s => !s.OutOfSight), Is.GreaterThanOrEqualTo(2), "somewhere to be met, and somewhere else");
                Assert.That(post.Stops.Single(s => s.At == post.AnchorAt).OutOfSight, Is.False, "the meeting place is in the open");
                Assert.That(routine.Entries.Any(e => post.Stops.Single(s => s.StationId == e.StationId).At == post.AnchorAt), Is.True,
                    routine.Id + " never stands at " + post.AnchorId);
            }
        }

        static RoutineEntry Block(float hour, string station) => new RoutineEntry { StartHour = hour, StationId = station };
        static RoutineStationEntry Place(string id, float x, float y, float heading, bool indoors = false) =>
            new RoutineStationEntry { Id = id, Position = new Vector2(x, y), StandHeadingDegrees = heading, OutOfSight = indoors };

        [Test]
        public void AKeepersPlace_IsAFunctionOfTheHourAlone()
        {
            var day = new[] { Block(6f, "door"), Block(12f, "yard"), Block(21f, "in") };
            var post = new[] { Place("door", 0f, 0f, 180f), Place("yard", 3f, 4f, 90f), Place("in", 3f, 9f, 0f, indoors: true) };
            const float secondsPerHour = 60f, speed = 1f;   // five metres takes five seconds: a twelfth of an hour

            Assert.That(KeeperDay.TryPose(day, post, 9f, secondsPerHour, speed, out var morning), Is.True);
            Assert.That(morning.At, Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(morning.HeadingDegrees, Is.EqualTo(180f));
            Assert.That(morning.Hidden, Is.False); Assert.That(morning.Walking, Is.False);

            Assert.That(KeeperDay.TryPose(day, post, 12f + 2.5f / 60f, secondsPerHour, speed, out var onTheWay), Is.True);
            Assert.That(onTheWay.Walking, Is.True);
            Assert.That(Vector2.Distance(onTheWay.At, new Vector2(1.5f, 2f)), Is.LessThan(1e-3f), "half way, in a straight line");
            Assert.That(onTheWay.HeadingDegrees, Is.EqualTo(KeeperDay.HeadingOf(new Vector2(3f, 4f))).Within(1e-3f), "facing the way they walk");

            Assert.That(KeeperDay.TryPose(day, post, 15f, secondsPerHour, speed, out var noon), Is.True);
            Assert.That(noon.At, Is.EqualTo(new Vector2(3f, 4f))); Assert.That(noon.Hidden, Is.False);

            Assert.That(KeeperDay.TryPose(day, post, 23f, secondsPerHour, speed, out var night), Is.True);
            Assert.That(night.Hidden, Is.True, "indoors once arrived");
            Assert.That(KeeperDay.TryPose(day, post, 2f, secondsPerHour, speed, out var small), Is.True);
            Assert.That(small.Hidden, Is.True, "before the first block it is still last night's");
            Assert.That(small.At, Is.EqualTo(night.At));

            Assert.That(KeeperDay.TryPose(day, post, 9f, secondsPerHour, speed, out var again), Is.True);
            Assert.That(again.At, Is.EqualTo(morning.At), "the same hour, the same place");
            Assert.That(KeeperDay.HeadingOf(Vector2.up), Is.EqualTo(0f)); Assert.That(KeeperDay.HeadingOf(Vector2.right), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(KeeperDay.TryPose(new[] { Block(6f, "door") }, post, 9f, secondsPerHour, speed, out _), Is.False, "one block is not a day");
            Assert.That(KeeperDay.TryPose(new[] { Block(6f, "door"), Block(9f, "nowhere") }, post, 10f, secondsPerHour, speed, out _), Is.False);
        }

        [Test]
        public void TheCottage_StandsBesideTheLookout_WithItsDoorTowardTheSea()
        {
            KeeperPost post = LoadKeeperPosts().Single(p => p.HasCottage);
            Vector2 bench = post.Stops.Single(s => s.StationId == WhelpsBenchStation).At;
            Assert.That(Vector2.Distance(post.CottageAt, bench), Is.InRange(6f, 14f), "beside the lookout, not on it");
            Assert.That(post.AnchorAt.x, Is.GreaterThan(post.CottageAt.x), "the door is on the seaward gable");
            Assert.That(post.AnchorAt.y, Is.LessThan(post.CottageAt.y));
        }

        [Test]
        public void TheScene_HoldsExactlyTheKeepersTheirStepWouldWrite_AndTheInteractorTalksToThem()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            KeeperPost[] posts = LoadKeeperPosts();
            LayerPatch stand = Keepers(scene, posts, new EditorKeeperAssets());
            Assert.That(stand.IsEmpty, Is.True, "StPeters.unity is not what the keepers step writes:\n" + stand.Summary());
            LayerPatch register = RegisterKeepers(scene, posts);
            Assert.That(register.IsEmpty, Is.True, "the interactor does not list the keepers:\n" + register.Summary());

            var owned = scene.SubtreeOf(scene.RootNamed(KeepersRootName).FileId);
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.IsScript(typeof(KeeperRound).FullName)), Is.EqualTo(2));
            Assert.That(scene.Docs.Count(d => d.IsScript(typeof(RoutineStations).FullName)), Is.EqualTo(1), "the village's table stays the only one");
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.IsScript(typeof(VillagerRoutine).FullName)), Is.EqualTo(0));
            foreach (string anchor in new[] { LightKeeperAnchorId, WhelpsCottageAnchorId })
                Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.ClassId == 1 && d.Field("m_Name") == anchor), Is.EqualTo(1), anchor);
        }
    }
}
