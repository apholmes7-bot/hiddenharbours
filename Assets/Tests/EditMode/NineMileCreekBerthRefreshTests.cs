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

        /// <summary>The analytic plan's harbour channel, byte for byte as the scene carried it before NMC H2 (Unity
        /// spells 100 as "100"; the step's own writer spells it "1E+02").</summary>
        const string AnalyticHarbour =
            "  _channels:\n" +
            "  - Waypoints:\n" +
            "    - {x: 204, y: 50}\n" +
            "    - {x: 182, y: 62}\n" +
            "    - {x: 170, y: 58}\n" +
            "    - {x: 152, y: 67}\n" +
            "    - {x: 132, y: 71}\n" +
            "    - {x: 116, y: 67}\n" +
            "    - {x: 100, y: 70}\n" +
            "    BedElevation: -3.9\n" +
            "    HalfWidthMetres: 12\n" +
            "    CuttableCeiling: -1.6";

        /// <summary>The committed scene with its ground put back to the analytic plan's harbour, as 03-berth-trench
        /// found it. NMC H2's 04-ground-g3 bound the scene to G3, which the trench refuses
        /// (<see cref="TheCommittedGroundIsG3_SoTheTrenchStepRefusesIt"/>), so the step's own mechanics are proved on
        /// the plan they were written for. Every other document is the committed one.</summary>
        static string WithoutTrench()
        {
            string text = Text;
            var scene = SceneYaml.Parse(text);
            var ground = scene.ComponentsOf(scene.RootNamed("TidalTerrain")).Single(d => d.ClassId == 114);
            int cut = ground.Text.IndexOf("\n  m_EditorClassIdentifier:", System.StringComparison.Ordinal);
            string head = System.Text.RegularExpressions.Regex.Replace(ground.Text.Substring(0, cut),
                @"(?m)^  m_Script: .*$", "  m_Script: {fileID: 11500000, guid: " +
                UnityEditor.AssetDatabase.AssetPathToGUID("Assets/_Project/Code/World/MainlandTidalTerrain.cs") + ", type: 3}");
            return text.Replace(ground.Text, head +
                "\n  m_EditorClassIdentifier: HiddenHarbours.World::HiddenHarbours.World.MainlandTidalTerrain\n" +
                "  _deepElevation: -6\n" + AnalyticHarbour);
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
            // E2 ruling 9, A34 ruling 2: the shared formatter writes the plain decimal, never an exponent.
            Assert.AreEqual("100", F(100));
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

        /// <summary>Replaces <c>TheCommittedTerrainCarriesTheBuildersBerthTrench</c> (NMC H2): the committed ground is
        /// G3, whose painted height carries the berths' depth, so the trench has no channels to patch and refuses
        /// rather than write an analytic trench into a painted ground.</summary>
        [Test]
        public void TheCommittedGroundIsG3_SoTheTrenchStepRefusesIt()
        {
            var scene = SceneYaml.Parse(Text);
            var ground = scene.ComponentsOf(scene.RootNamed("TidalTerrain")).Single(d => d.ClassId == 114);
            Assert.IsTrue(ground.IsScript("HiddenHarbours.World.PaintedTidalTerrain"), ground.Field("m_EditorClassIdentifier"));
            Assert.Throws<Refusal>(() => NineMileCreekBerthRefresh.Trench(scene));
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
