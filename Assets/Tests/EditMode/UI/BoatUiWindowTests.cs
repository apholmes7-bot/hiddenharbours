using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// What is left of the boat-UI windowing model (the owner's 2026-08-07 ruling) after H2, pinned
    /// where it is pure: the hide-all toggle, the watch's individual hide, and the chrome publication
    /// that keeps a press on the watch's × from reading as a helm host's click-away. The move, size and
    /// collapse half retired with the dash card and the expanded cards' windows. The canvas-and-pointer
    /// half is <c>BoatUiWindowPlayTests</c>.
    /// </summary>
    public class BoatUiWindowTests
    {
        [SetUp]
        public void SetUp() => BoatUiWindows.Reset();

        [TearDown]
        public void TearDown() => BoatUiWindows.Reset();

        [Test]
        public void HideAll_HidesEveryWindow_AndTheRestoreClearsIndividualHides()
        {
            BoatUiWindows.SetHidden(BoatUiWindowId.Watch, true);
            Assert.That(BoatUiWindows.IsShown(BoatUiWindowId.Watch), Is.False,
                        "an individual hide hides that window");
            Assert.That(BoatUiWindows.AllHidden, Is.False,
                        "…and is not the hide-all: the helm hosts still stand");

            BoatUiWindows.ToggleHideAll();
            Assert.That(BoatUiWindows.AllHidden, Is.True);
            Assert.That(BoatUiWindows.IsShown(BoatUiWindowId.Watch), Is.False);

            BoatUiWindows.ToggleHideAll();
            Assert.That(BoatUiWindows.AllHidden, Is.False);
            Assert.That(BoatUiWindows.IsShown(BoatUiWindowId.Watch), Is.True,
                        "the RESTORE half clears individual hides too — it is the one guaranteed " +
                        "recovery path back to a window whose own chrome is off screen");

            BoatUiWindows.ToggleHideAll();
            Assert.That(BoatUiWindows.IsShown(BoatUiWindowId.Watch), Is.False,
                        "hide-all hides a window that was never individually hidden");
        }

        [Test]
        public void TheChromePublication_RoutesPresses_AndClears()
        {
            Assert.That(BoatUiWindows.OverAnyChrome(new Vector2(50f, 50f)), Is.False);

            BoatUiWindows.PublishChrome(BoatUiWindowId.Watch, new Rect(40f, 40f, 18f, 18f));
            Assert.That(BoatUiWindows.OverAnyChrome(new Vector2(50f, 50f)), Is.True,
                        "a press on the watch's × is the watch's");
            Assert.That(BoatUiWindows.OverAnyChrome(new Vector2(200f, 50f)), Is.False);

            BoatUiWindows.ClearChrome(BoatUiWindowId.Watch);
            Assert.That(BoatUiWindows.OverAnyChrome(new Vector2(50f, 50f)), Is.False,
                        "a withdrawn publication must stop swallowing clicks");

            BoatUiWindows.PublishChrome(BoatUiWindowId.Watch, new Rect(40f, 40f, 18f, 0f));
            Assert.That(BoatUiWindows.OverAnyChrome(new Vector2(50f, 40f)), Is.False,
                        "an empty strip is no publication");
        }
    }
}
