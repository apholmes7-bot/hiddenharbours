using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// Two defects from the play looks of the open book.
    ///
    /// <para><b>The open tab must be told apart from the rest.</b> The selected stub was tinted gold, but
    /// a baked chip draws white, so on the page every stub looked the same. It now takes the kit's "on"
    /// state: pulled out by <see cref="NotebookKit.TabPull"/>, an inked edge and a lit top row.</para>
    ///
    /// <para><b>The register line stays on its leaf.</b> The landing line sits in the left head, one line
    /// above the first rule, and was lettered with free overflow, so a long line ran across the gutter onto
    /// the facing leaf. It is now fitted to the head's cells beside the weight readout.</para>
    /// </summary>
    public class NotebookTabAndRegisterTests
    {
        // ---- the fit rule (pure) ----------------------------------------------------------------------

        [Test]
        public void ALineThatFitsIsLeftWhole()
        {
            Assert.AreEqual("Landed a cod.", NotebookLayout.FitLine("Landed a cod.", 20));
            Assert.AreEqual("Landed a cod.", NotebookLayout.FitLine("Landed a cod.", 13), "exactly the room");
        }

        [Test]
        public void ALongLineDropsItsTrailingClauseFirst()
        {
            string line = "Landed a 9.4 kg atlantic cod — a heavy one.";
            Assert.AreEqual("Landed a 9.4 kg atlantic cod.", NotebookLayout.FitLine(line, 29));
            Assert.AreEqual("Landed a 9.4 kg atlantic cod.", NotebookLayout.FitLine(line, 35));
        }

        [Test]
        public void WithoutRoomForTheLeadTheLineKeepsTheWordsThatNameTheCatch()
        {
            string line = "Landed a 9.4 kg atlantic cod — a heavy one.";
            Assert.AreEqual("Atlantic cod.", NotebookLayout.FitLine(line, 14), "the 1280x720 head beside a weight");
            Assert.AreEqual("9.4 kg atlantic cod.", NotebookLayout.FitLine(line, 20));
            Assert.AreEqual("Cod.", NotebookLayout.FitLine(line, 5));
        }

        [Test]
        public void WhenNotEvenTheLastWordFitsTheLineIsCutAndMarked()
        {
            Assert.AreEqual("Landed...", NotebookLayout.FitLine("Landed a flounderwithaverylongname.", 9));
            Assert.AreEqual("Lan", NotebookLayout.FitLine("Landed a 9.4 kg atlantic cod.", 3));
        }

        [Test]
        public void TheFitIsNeverWiderThanTheRoom()
        {
            string[] lines =
            {
                "Landed a 9.4 kg atlantic cod — a heavy one.",
                "Lifted out a soft-shell clam — it's in your hand.",
                "Landed an extraordinarilylongspeciesnamewithnospaces — a heavy one.",
                "",
            };
            foreach (string line in lines)
                for (int cols = 0; cols <= 60; cols++)
                    Assert.LessOrEqual(NotebookLayout.FitLine(line, cols).Length, cols,
                        $"'{line}' at {cols} cells");
        }

        [Test]
        public void TheRegisterGivesWayToTheWeightItLandsOn()
        {
            int head = 176;   // the left head at the closest tier (31 cols): 35 cells
            Assert.AreEqual(35, NotebookLayout.RegisterCols(head, 0), "no weight lettered: the whole head");
            Assert.AreEqual(35 - "9.4 kg".Length - 1, NotebookLayout.RegisterCols(head, 94));
            Assert.AreEqual(35 - "0.3 kg".Length - 1, NotebookLayout.RegisterCols(head, 3));
            Assert.AreEqual(35 - "12.3 kg".Length - 1, NotebookLayout.RegisterCols(head, 123));
            Assert.AreEqual(0, NotebookLayout.RegisterCols(4, 123), "never negative");
        }

        // ---- the built book ---------------------------------------------------------------------------

        [Test]
        public void TheOpenTabIsPulledOutAndInked()
        {
            var host = new GameObject("[NotebookTabTest]");
            NotebookPresenter p = host.AddComponent<NotebookPresenter>();
            try
            {
                Wire(p);
                p.Open();
                Assert.GreaterOrEqual(p.Tabs.Count, 2, "the book always carries more than one stub");

                AssertOnlySelectedIsOn(host, p);
                p.SelectTab(1);
                AssertOnlySelectedIsOn(host, p);
            }
            finally
            {
                p.Close();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TheLandingLineFitsItsOwnLeaf()
        {
            var host = new GameObject("[NotebookRegisterTest]");
            NotebookPresenter p = host.AddComponent<NotebookPresenter>();
            try
            {
                Wire(p);
                p.OverrideJuice(JuiceSettings.Default);
                p.Open();

                var item = new CatchItem("fish.atlantic_cod", "Atlantic Cod With A Very Long Market Name",
                                         FishCategory.InshoreGroundfish, 12.3f, 12, 0.6f);
                p.OnCatchLanded(new CatchLanded(in item));
                p.OnMoment(new JuiceMomentCue(JuiceMoment.Landing, Vector2.zero, 12.3f));

                int pageW = NotebookKit.PageWidthFor(p.Fit.Cols);
                int head = pageW - NotebookKit.PadL - NotebookKit.PadR;
                int cells = (head + NotebookKit.Slack) / NotebookKit.CellWidth;

                Assert.Greater(p.RegisterLine.Length, cells, "the test's line really is too long for the head");
                Assert.LessOrEqual(p.RegisterShown.Length * NotebookKit.CellWidth - NotebookKit.Slack,
                                   head - ("12.3" + NotebookLayout.WeightUnit).Length * NotebookKit.CellWidth,
                                   "the lettered line and the weight readout share the head without overlap");
                Assert.Greater(p.RegisterShown.Length, 1, "the head still says what was landed");
                StringAssert.Contains(p.RegisterShown.TrimEnd('.').Substring(1), p.RegisterLine,
                                      "what is lettered is words from the kept line, not new copy");
                Assert.AreNotEqual(p.RegisterLine, p.RegisterShown, "the kept line is unchanged; only its lettering is fitted");
            }
            finally
            {
                p.Close();
                Object.DestroyImmediate(host);
            }
        }

        private static void Wire(NotebookPresenter p)
        {
            p.WireContent(() => new List<QuestDef>(),
                          () => new List<KnowledgePageDef>(),
                          () => new List<NotebookNote>(),
                          new InMemoryFlagStore());
        }

        private static void AssertOnlySelectedIsOn(GameObject host, NotebookPresenter p)
        {
            RectTransform[] chips = host.GetComponentsInChildren<RectTransform>(true)
                                        .Where(r => r.name == "Tab").ToArray();
            Assert.AreEqual(Mathf.Min(p.Tabs.Count, NotebookKit.MaxTabs), chips.Length);

            float flush = chips.Select(c => c.anchoredPosition.x).Min();
            for (int i = 0; i < chips.Length; i++)
            {
                bool on = i == p.TabIndex;
                bool inked = chips[i].GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "TabOnEdge");
                Assert.AreEqual(on ? flush + NotebookKit.TabPull : flush, chips[i].anchoredPosition.x,
                    on ? "the open stub stands pulled out" : "a closed stub sits flush");
                Assert.AreEqual(on, inked, on ? "the open stub is inked" : "a closed stub is not inked");
            }
        }
    }
}
