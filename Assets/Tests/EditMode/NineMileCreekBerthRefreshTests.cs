#if UNITY_EDITOR
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using NUnit.Framework;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    public class NineMileCreekBerthRefreshTests
    {
        static string Text => File.ReadAllText(NineMileCreekLayerRefresh.ScenePath);
        static string WithoutTrench()
        {
            string text = Text;
            var scene = SceneYaml.Parse(text);
            var terrain = scene.ComponentsOf(scene.RootNamed("TidalTerrain"))
                .Single(d => d.IsScript("HiddenHarbours.World.MainlandTidalTerrain"));
            int first = terrain.Text.IndexOf("\n  - Waypoints:", System.StringComparison.Ordinal);
            int second = terrain.Text.IndexOf("\n  - Waypoints:", first + 1, System.StringComparison.Ordinal);
            return second < 0 ? text : text.Replace(terrain.Text, terrain.Text.Substring(0, second));
        }
        static string HarbourBlock()
        {
            var scene = SceneYaml.Parse(WithoutTrench());
            string ground = scene.ComponentsOf(scene.RootNamed("TidalTerrain"))
                .Single(d => d.IsScript("HiddenHarbours.World.MainlandTidalTerrain")).Text;
            return ground.Substring(ground.IndexOf("  _channels:", System.StringComparison.Ordinal));
        }
        public static void AssertMovedWaypointRejected(System.Action<string, HiddenHarbours.World.MainlandChannel[]> precondition)
        {
            string changed = HarbourBlock().Replace("{x: 204, y: 50}", "{x: 204.5, y: 50}");
            Assert.AreNotEqual(HarbourBlock(), changed, "Mutation must move one waypoint.");
            Assert.Throws<Refusal>(() => precondition(changed, new[] { NineMileCreekMainland.HarbourChannel }));
        }
        public static void AssertChangedBedRejected(System.Action<string, HiddenHarbours.World.MainlandChannel[]> precondition)
        {
            string changed = HarbourBlock().Replace("BedElevation: -3.9", "BedElevation: -3.4");
            Assert.AreNotEqual(HarbourBlock(), changed, "Mutation must change the bed.");
            Assert.Throws<Refusal>(() => precondition(changed, new[] { NineMileCreekMainland.HarbourChannel }));
        }
        [Test]
        public void TheScenesHundredAndScientificHundredAreAcceptedWithoutRewritingTheHarbour()
        {
            string source = WithoutTrench();
            string harbour = HarbourBlock();
            StringAssert.Contains("{x: 100, y: 70}", harbour);
            Assert.AreEqual("1E+02", F(100));
            NineMileCreekBerthRefresh.RequireChannels(harbour, new[] { NineMileCreekMainland.HarbourChannel });
            NineMileCreekBerthRefresh.RequireChannels(harbour.Replace("{x: 100, y: 70}", "{x: 1E+02, y: 70}"),
                new[] { NineMileCreekMainland.HarbourChannel });
            string after = NineMileCreekBerthRefresh.Trench(SceneYaml.Parse(source)).ApplyTo(source);
            StringAssert.Contains(harbour + "\n  - Waypoints:", after, "The original harbour bytes must survive intact.");
        }
        [Test]
        public void AHarbourWaypointMovedByHalfAMetreIsRefused() => AssertMovedWaypointRejected(NineMileCreekBerthRefresh.RequireChannels);
        [Test]
        public void AHarbourWithChangedBedElevationIsRefused() => AssertChangedBedRejected(NineMileCreekBerthRefresh.RequireChannels);
        [Test]
        public void ThePatchedChannelBlockParsesExactlyToTheBuildersChannels()
        {
            string source = WithoutTrench();
            var after = NineMileCreekBerthRefresh.Trench(SceneYaml.Parse(source)).Apply(SceneYaml.Parse(source));
            string ground = after.ComponentsOf(after.RootNamed("TidalTerrain"))
                .Single(d => d.IsScript("HiddenHarbours.World.MainlandTidalTerrain")).Text;
            var actual = NineMileCreekBerthRefresh.ParseChannels(ground.Substring(ground.IndexOf("  _channels:", System.StringComparison.Ordinal)));
            var expected = NineMileCreekMainland.Channels;
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Waypoints.Length, actual[i].Waypoints.Length);
                for (int j = 0; j < expected[i].Waypoints.Length; j++)
                {
                    Assert.AreEqual(expected[i].Waypoints[j].x, actual[i].Waypoints[j].x);
                    Assert.AreEqual(expected[i].Waypoints[j].y, actual[i].Waypoints[j].y);
                }
                Assert.AreEqual(expected[i].BedElevation, actual[i].BedElevation);
                Assert.AreEqual(expected[i].HalfWidthMetres, actual[i].HalfWidthMetres);
                Assert.AreEqual(expected[i].CuttableCeiling, actual[i].CuttableCeiling);
            }
        }
        [Test]
        public void TheCommittedWallFleetMatchesItsOwnerBerths() =>
            Assert.IsTrue(NineMileCreekBerthRefresh.Fleet(SceneYaml.Parse(Text)).IsEmpty);
        [Test]
        public void ASecondFleetRunChangesNothing()
        {
            string text = Text;
            string once = NineMileCreekBerthRefresh.Fleet(SceneYaml.Parse(text)).ApplyTo(text);
            Assert.AreEqual(once, NineMileCreekBerthRefresh.Fleet(SceneYaml.Parse(once)).ApplyTo(once));
        }

        // A deliberate always-pass precondition, in memory only. Exercise the SAME assertions as
        // the two permanent refusal tests, save their red output, then apply the real guarded patch.
        public static void ProveControlsAndApply()
        {
            var messages = new System.Collections.Generic.List<string>();
            System.Action<string, HiddenHarbours.World.MainlandChannel[]> alwaysPass = (block, plan) => { };
            foreach (var check in new System.Action<System.Action<string, HiddenHarbours.World.MainlandChannel[]>>[]
                { AssertMovedWaypointRejected, AssertChangedBedRejected })
            {
                check(NineMileCreekBerthRefresh.RequireChannels);
                try { check(alwaysPass); }
                catch (AssertionException e) { messages.Add(check.Method.Name + ": RED as required: " + e.Message); }
            }
            File.WriteAllLines("artifacts/nmc-n1/fix934/precondition-always-pass-control.txt", messages);
            Assert.AreEqual(2, messages.Count, "Both negative guards must fail with an always-pass precondition.");
            NineMileCreekBerthRefresh.ApplyOwnerHold();
            string once = Text;
            NineMileCreekBerthRefresh.ApplyTrench(); NineMileCreekBerthRefresh.ApplyFleet();
            string twice = Text;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                File.WriteAllLines("artifacts/nmc-n1/fix934/second-run-sha256.txt", new[] {
                    System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(once))).Replace("-", ""),
                    System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(twice))).Replace("-", "") });
            Assert.AreEqual(once, twice);
        }

        [Test]
        public void TheCommittedTerrainCarriesTheBuildersBerthTrench()
        {
            Assert.IsTrue(NineMileCreekBerthRefresh.Trench(SceneYaml.Parse(Text)).IsEmpty,
                "The committed bed is missing the builder's berth trench.");
        }
        [Test]
        public void ASecondTrenchRunChangesNothing()
        {
            string source = WithoutTrench();
            string once = NineMileCreekBerthRefresh.Trench(SceneYaml.Parse(source)).ApplyTo(source);
            var second = NineMileCreekBerthRefresh.Trench(SceneYaml.Parse(once));
            Assert.IsTrue(second.IsEmpty, second.Summary());
            Assert.AreEqual(once, second.ApplyTo(once));
        }
        [Test]
        public void TheTrenchPatchPreservesEveryOtherDocument()
        {
            var before = SceneYaml.Parse(WithoutTrench());
            var patch = NineMileCreekBerthRefresh.Trench(before);
            var after = patch.Apply(before);
            long owned = before.ComponentsOf(before.RootNamed("TidalTerrain"))
                .Single(d => d.IsScript("HiddenHarbours.World.MainlandTidalTerrain")).FileId;
            Assert.IsEmpty(patch.Deletions);
            foreach (var doc in before.Docs.Where(d => d.FileId != owned))
                Assert.AreEqual(doc.Text, after.Require(doc.FileId, "protected document").Text, "&" + doc.FileId);
        }
    }
}
#endif
