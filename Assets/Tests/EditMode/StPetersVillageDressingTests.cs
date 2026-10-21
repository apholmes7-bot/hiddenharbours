using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class StPetersVillageDressingTests
    {
        [Test]
        public void HeldTrees_AreNotPlacedOrSentToASpriteLoader()
        {
            foreach (string id in new[] { "tree.stp_vl_square_maple_01", "tree.stp_vl_square_maple_02" })
            {
                var piece = new VillagePiece { Id = id, Kit = "treeIsoRig4", Piece = "RedMaple",
                    OptionsJson = "{\"stage\":\"young\",\"variant\":0}", Pending = true };
                var row = StPetersVillageDressing.Resolve("commons.st_peters.square", piece, false);
                Assert.That(row.Status, Is.EqualTo("held"));
                Assert.That(StPetersVillageDressing.Places(row), Is.False);
                Assert.That(piece.OptionsJson, Is.EqualTo("{\"stage\":\"young\",\"variant\":0}"));
                Assert.Throws<System.InvalidOperationException>(() => StPetersVillageDressing.SpriteFor(row));
            }
            Assert.That(StPetersVillageDressing.HoldsTree("tree.stp_vl_square_maple_03"), Is.False);
        }

        [Test]
        public void HeldYardsAndFlakeMembership_AreNamedExactly()
        {
            foreach (string id in new[] { "white_farmhouse", "red_saltbox", "post_office", "cream_saltbox" })
                Assert.That(StPetersVillageDressing.HoldsYard("yard.st_peters." + id), Is.True);
            foreach (string id in new[] { "school", "general_store", "sage_cottage", "harbour_gable", "white_gable" })
                Assert.That(StPetersVillageDressing.HoldsYard("yard.st_peters." + id), Is.False);
            Assert.That(StPetersVillageDressing.HoldsCommonsStation("station.st_peters.flake_yard"), Is.True);
            Assert.That(StPetersVillageDressing.HoldsCommonsStation("station.st_peters.hearth_bench_a"), Is.False);
        }

        [Test]
        public void DressingChoices_KeepTheRuledOmissionsAndBenchStandIn()
        {
            var bench = new VillagePiece { Id = "bench", Kit = "yardIso", Piece = "bench" };
            var row = StPetersVillageDressing.Resolve("square", bench, false);
            Assert.That(row.Kit, Is.EqualTo("wharfDecor"));
            Assert.That(row.Key, Is.EqualTo("bench"));
            Assert.That(row.Status, Is.EqualTo("stand-in"));
            var barrel = new VillagePiece { Id = "barrel", Kit = "yardIso", Piece = "rainBarrel" };
            Assert.That(StPetersVillageDressing.Resolve("square", barrel, false).Status, Is.EqualTo("omit"));
            Assert.That(StPetersVillageDressing.Resolve("held", barrel, true).Status, Is.EqualTo("held"));
            var unknown = new VillagePiece { Id = "unknown", Kit = "yardIso", Piece = "unlisted" };
            Assert.Throws<System.InvalidOperationException>(() => StPetersVillageDressing.Resolve("square", unknown, false));
        }
    }
}
