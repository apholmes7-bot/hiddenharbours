using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// Two pins from the owner's 2026-08-20 eyeball pass on the rendered proofs.
    ///
    /// <para><b>The chip is TabChipWidth, not TabCol.</b> The kit's chip is "the tab column plus the
    /// 9 px it stands proud of the fore-edge" — and those 9 px are exactly the difference between a
    /// five-character label fitting its lane (ChipChars × CellWidth = 25 px) and TASKS overflowing
    /// its own chip art (TabCol − 2 × TabPad = 22 px), which is what the proofs showed. The test
    /// stands on the BUILT hierarchy rather than on the constant the presenter happens to name, so a
    /// regression to any wrong width goes red, not only a regression to the old one.</para>
    ///
    /// <para><b>N is the book's key.</b> The owner's ruling; the tide table moved to Tab for it. The
    /// pin here holds the constant; the tide table's side is pinned beside its own class (UI tests),
    /// because these assemblies deliberately cannot see each other.</para>
    /// </summary>
    public class NotebookChipAndKeyTests
    {
        [Test]
        public void TheBooksKeyIsN_TheOwnersRuling()
        {
            Assert.AreEqual(Key.N, NotebookPresenter.OpenKey,
                "NotebookPresenter.OpenKey is the owner's 2026-08-20 ruling (notebook = N; tide " +
                "table = Tab). Changing it is his call, not a refactor.");
        }

        [Test]
        public void ChipLaneHoldsTheKitsFullLabelBudget()
        {
            Assert.GreaterOrEqual(
                NotebookKit.TabChipWidth - 2 * NotebookKit.TabPad,
                NotebookKit.ChipChars * NotebookKit.CellWidth,
                "A ChipChars-long label must fit the chip's text lane — the kit publishes both " +
                "numbers, and validation holds authored labels to ChipChars on the promise they fit.");
        }

        [Test]
        public void EveryDrawnChipIsTheKitsChipWidth()
        {
            var host = new GameObject("[NotebookChipTest]");
            NotebookPresenter p = host.AddComponent<NotebookPresenter>();
            try
            {
                p.WireContent(() => new List<QuestDef>(),
                              () => new List<KnowledgePageDef>(),
                              () => new List<NotebookNote>(),
                              new InMemoryFlagStore());
                p.Open();

                RectTransform[] chips = host.GetComponentsInChildren<RectTransform>(true)
                                            .Where(r => r.name == "Tab").ToArray();
                Assert.IsNotEmpty(chips, "an open book draws its fore-edge stubs");

                foreach (RectTransform chip in chips)
                {
                    Assert.AreEqual(NotebookKit.TabChipWidth, (int)chip.sizeDelta.x,
                        "a chip drawn at any other width either clips its label (TabCol did, " +
                        "measured on the 2026-08-20 proofs) or stops matching the kit's fore-edge");
                }
            }
            finally
            {
                p.Close();
                Object.DestroyImmediate(host);
            }
        }
    }
}
