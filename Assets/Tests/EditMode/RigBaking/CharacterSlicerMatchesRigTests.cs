using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The seam nobody owns.</b> The baker reads the cell and pivot FROM the rig; the slicer
    /// declares them as constants, because it never runs JavaScript. Two numbers, two files, no
    /// compiler relationship between them — and when the rig's cell moved 64 × 88 → 64 × 92 the
    /// slicer's <c>GroundInsetPx</c> quietly stopped being <c>H − pivotY</c>.
    ///
    /// <para>That failure is invisible everywhere else. A sheet baked at the new cell and sliced at
    /// the old inset still slices into 8 × N cells, still imports at native resolution, still tiles
    /// with no gaps, still names every slice correctly — and plants the character two pixels into the
    /// ground in every frame of every animation. The only thing that can catch it is comparing the
    /// two sources of truth directly, which is what this file is.</para>
    ///
    /// <para>This is the only test in the repo that reaches across both assemblies on purpose. It
    /// lives in the RigBaking suite because that is the one that can run the rig.</para>
    /// </summary>
    public class CharacterSlicerMatchesRigTests
    {
        [Test]
        public void SlicerCell_IsTheRigsOwnCell()
        {
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("character"));

            Assert.AreEqual(geo.Width, CharacterSheetSlicer.CellW,
                "CharacterSheetSlicer.CellW disagrees with the rig's own W. Sheets would slice on a " +
                "grid the art was never baked on.");
            Assert.AreEqual(geo.Height, CharacterSheetSlicer.CellH,
                "CharacterSheetSlicer.CellH disagrees with the rig's own H. The rig moved and the " +
                "slicer did not follow — sheets will fail the row-count check, or worse, pass it.");
        }

        [Test]
        public void SlicerGroundInset_IsTheRigsOwnPivot()
        {
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("character"));

            // ADR 0026: the rig's pivot is a CONTINUOUS coordinate from the cell's top-left, so the
            // inset from the bottom is H − pivotY exactly — not H − 1 − pivotY.
            double insetFromRig = geo.Height - geo.PivotY;

            Assert.AreEqual(insetFromRig, CharacterSheetSlicer.GroundInsetPx, 1e-9,
                $"the rig plants its figure {insetFromRig} px above the cell bottom but the slicer " +
                $"pivots at {CharacterSheetSlicer.GroundInsetPx}. Every character stands " +
                $"{insetFromRig - CharacterSheetSlicer.GroundInsetPx} px off the ground, on art that " +
                "passes every other slice assert.");

            Assert.AreEqual(geo.PivotX, CharacterSheetSlicer.CellW / 2.0, 1e-9,
                "the rig's pivot is off the cell centreline — the slicer's (cellW/2) rule no longer " +
                "describes it");
        }

        [Test]
        public void SlicerPivot_MatchesTheRigsUnityNormalisedPivot()
        {
            // The same equality one level up, in the number that actually lands in the .meta — so a
            // future refactor that keeps the constants but changes the normalisation still trips.
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("character"));

            Vector2 fromSlicer = CharacterSheetSlicer.GroundPivotFor(
                new Vector2Int(CharacterSheetSlicer.CellW, CharacterSheetSlicer.CellH));

            Assert.AreEqual(geo.UnityNormalisedPivot.x, fromSlicer.x, 1e-5f);
            Assert.AreEqual(geo.UnityNormalisedPivot.y, fromSlicer.y, 1e-5f,
                "the slicer's normalised ground pivot is not the rig's. ADR 0026's formula is " +
                "(H − pivotY)/H and both sides must compute it the same way.");
        }
    }
}
