using System.IO;
using NUnit.Framework;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// 🔴 <b>THE TRIPWIRE.</b> The anchored bubble ships GREYBOX — tinted rects, no sprites — because the
    /// bubble kit is the art lane's sibling PR and this one refused to wait for it. That is the right
    /// call and it is also exactly the arrangement that quietly becomes permanent, which is the #555
    /// lesson: a fallback with nobody watching it is a fallback that ships.
    ///
    /// <para>So this test FAILS THE DAY THE ART LANDS, and its failure message is the checklist. Nothing
    /// here asserts that greybox is good; it asserts that greybox is still what is happening, so that
    /// stopping being true is an event somebody sees.</para>
    /// </summary>
    public class DialogueBubbleArtTests
    {
        /// <summary>The flip, spelled out once so both asserts can say it.</summary>
        const string TheFlip =
            "🔴 THE BUBBLE KIT HAS LANDED — this test has done its job and must now be FLIPPED.\n" +
            "  1. Wire the kit's 9-slice body, tail and option-panel sprites onto DialoguePresenter's " +
            "_bubbleSprite / _tailSprite / _optionSprite from the region builders (StPetersBuilder, " +
            "NineMileCreekBuilder), the way the camper's sheets are wired.\n" +
            "  2. Take the kit's declared pixel grid (24 vs 32) from its own contract and check the " +
            "bubble's constants against it — the layout numbers here are canvas units chosen for the " +
            "greybox, not measurements of the kit.\n" +
            "  3. Rewrite this file into its landed form: assert the sprites RESOLVE (the #555 " +
            "`WithTheSheetsBaked_…` shape), so the tripwire then guards against the art LEAVING.";

        [Test]
        public void TheBubbleKitHasNotLandedYet_AndWhenItDoesThisTestSaysWhatToDo()
        {
            Assert.That(Directory.Exists(DialoguePresenter.ArtFolder), Is.False,
                        $"{DialoguePresenter.ArtFolder} exists.\n{TheFlip}");
        }

        [Test]
        public void TheArtFolderIsDeclaredOnThePresenter_SoTheImportPrHasOnePlaceToLook()
        {
            // The contract half: the path is not a guess living in a test, it is a constant on the
            // component the kit gets wired into. If somebody moves it, they move it once.
            Assert.That(DialoguePresenter.ArtFolder, Does.StartWith("Assets/"),
                        "the kit lands inside the project, not in docs/");
            Assert.That(DialoguePresenter.ArtFolder, Does.Contain("Art"),
                        "and under Art, where every other kit lives");
        }
    }
}
