using System;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>The step that names each deck's underside is a one-line, re-runnable scene edit.</summary>
    public class UndersideAssignmentTests
    {
        const string Deck = "44422230a0a985e44ac4eb7c9dd04712";
        const string Other = "0123456789abcdef0123456789abcdef";
        const string Guid = "215d3cd7c8194e598e5901f2df2d8581";

        static string Scene(string extra = "") =>
            "%YAML 1.1\n--- !u!1 &10\nGameObject:\n  m_Name: Pier\n" +
            "--- !u!114 &11\nMonoBehaviour:\n  m_GameObject: {fileID: 10}\n" +
            $"  m_Script: {{fileID: 11500000, guid: {Deck}, type: 3}}\n  _deckElevation: 3.4\n{extra}" +
            "--- !u!1 &20\nGameObject:\n  m_Name: Shed\n" +
            "--- !u!114 &21\nMonoBehaviour:\n  m_GameObject: {fileID: 20}\n" +
            $"  m_Script: {{fileID: 11500000, guid: {Other}, type: 3}}\n  _deckElevation: 3.4\n";

        static readonly UndersideAssignment.Entry[] One = { new UndersideAssignment.Entry("s", "Pier", "a.asset") };
        static string G(string _) => Guid;

        [Test]
        public void ItAddsOneLine_ToTheNamedDeckOnly()
        {
            string before = Scene();
            string after = UndersideAssignment.Patch(before, One, G, out int changed);
            Assert.AreEqual(1, changed);
            string line = $"  _underside: {{fileID: 11400000, guid: {Guid}, type: 2}}\n";
            Assert.AreEqual(before.Replace("  _deckElevation: 3.4\n--- !u!1 &20", "  _deckElevation: 3.4\n" + line + "--- !u!1 &20"), after);
        }

        [Test]
        public void ASecondRun_ChangesNothing_AndAStaleReferenceIsReplaced()
        {
            string once = UndersideAssignment.Patch(Scene(), One, G, out _);
            Assert.AreEqual(once, UndersideAssignment.Patch(once, One, G, out int again));
            Assert.AreEqual(0, again);
            string stale = Scene("  _underside: {fileID: 0}\n");
            Assert.AreEqual(once, UndersideAssignment.Patch(stale, One, G, out int fixedCount));
            Assert.AreEqual(1, fixedCount);
        }

        [Test]
        public void ADeckTheTableNamesButTheSceneLacks_IsARefusal()
        {
            var missing = new[] { new UndersideAssignment.Entry("s", "Shed", "a.asset") };
            Assert.Throws<InvalidOperationException>(() => UndersideAssignment.Patch(Scene(), missing, G, out _),
                "Shed is not a deck component: refuse, never skip");
        }

        [Test]
        public void TheTable_NamesEachDeckOnce()
        {
            var table = UndersideAssignment.Table().ToArray();
            var keys = table.Select(e => e.Scene + "/" + e.Deck).ToArray();
            Assert.AreEqual(keys.Length, keys.Distinct().Count());
            Assert.AreEqual(18, keys.Length, "1 St Peters pier + 9 NMC pier walks named; 4 floats + 4 gangways exempt");
            Assert.AreEqual(10, table.Count(e => e.Asset != null), "ten decks reflect an underside");
            Assert.AreEqual(8, table.Count(e => e.Asset == null), "the floats and gangways reflect none");
            CollectionAssert.AreEquivalent(new[] { "pierW/walk0", "pierE/walk0" },
                table.Where(e => e.DrawnLifted).Select(e => e.Deck).ToArray(), "only the yard's kit piers are drawn lifted");
        }

        [Test]
        public void AnExemptDeck_LosesItsReference_AndALiftedDeckSaysSo_OnceOnly()
        {
            string named = UndersideAssignment.Patch(Scene(), One, G, out _);
            var clear = new[] { new UndersideAssignment.Entry("s", "Pier", null) };
            Assert.AreEqual(Scene(), UndersideAssignment.Patch(named, clear, G, out int removed), "the reference line goes, nothing else");
            Assert.AreEqual(1, removed);
            UndersideAssignment.Patch(Scene(), clear, G, out int none);
            Assert.AreEqual(0, none, "an exempt deck without a reference is left alone");

            var lifted = new[] { new UndersideAssignment.Entry("s", "Pier", "a.asset", drawnLifted: true) };
            string once = UndersideAssignment.Patch(Scene(), lifted, G, out _);
            StringAssert.Contains("  _deckElevation: 3.4\n  _drawnLiftedByDeck: 1\n  _underside: ", once, "written after the deck height");
            Assert.AreEqual(once, UndersideAssignment.Patch(once, lifted, G, out int again));
            Assert.AreEqual(0, again);
            Assert.AreEqual(once, UndersideAssignment.Patch(once.Replace("_drawnLiftedByDeck: 1", "_drawnLiftedByDeck: 0"), lifted, G, out _),
                "a cleared flag is set again");
        }
    }
}
