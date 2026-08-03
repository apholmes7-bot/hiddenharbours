#if UNITY_EDITOR
using NUnit.Framework;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The pin <see cref="PersistentCoreBuilder"/>'s own comment promises (a #398 review found the
    /// comment naming a test that did not exist — this is that test, delivered late).
    ///
    /// <para><b>Why one equality deserves a file:</b> <c>IsoCellHeightPx</c> is the ONE hard-coded
    /// character cell height outside <see cref="CharacterSheetSlicer"/>, and it fails SILENTLY: at
    /// 88 against a 92-tall cell the submerge shader clips <c>uv.y</c> against the wrong body
    /// height, the wade waterline sits centimetres off, and nothing anywhere errors. The slicer's
    /// <see cref="CharacterSheetSlicer.CellH"/> is itself pinned to the live rig by
    /// <c>CharacterSlicerMatchesRigTests</c> (RigBaking suite), so this single equality chains the
    /// builder all the way back to the art source: builder == slicer == rig.</para>
    /// </summary>
    public class CharacterCellConstantTests
    {
        [Test]
        public void BuilderCellHeight_MatchesTheSlicersCell()
        {
            Assert.AreEqual(CharacterSheetSlicer.CellH, PersistentCoreBuilder.IsoCellHeightPx,
                "PersistentCoreBuilder.IsoCellHeightPx disagrees with CharacterSheetSlicer.CellH. " +
                "This is the silent wade-waterline bug: the submerge shader would clip uv.y against " +
                "the wrong body height and the waterline sits centimetres off with no error anywhere. " +
                "If the cell genuinely changed (a new rig pass), update BOTH constants and re-derive " +
                "the wade block's measured fractions in PersistentCoreBuilder — do not just silence " +
                "this test.");
        }
    }
}
#endif
