using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The HUD's screen (<see cref="HudScreen"/>, H2): the seam every overlay over the helm lays itself
    /// out on. A plate hands it a screen, and the band, the raised cards, the HUD, the toast, the popup
    /// and the quest note all stand where they would on that screen; unset, it is the player's own.
    /// </summary>
    public class HudScreenTests
    {
        /// <summary>Every overlay that can stand over the helm and places itself by the screen.</summary>
        private static readonly string[] Readers =
        {
            "Assets/_Project/Code/UI/HelmOverlayHost.cs",
            "Assets/_Project/Code/UI/SounderOverlayHost.cs",
            "Assets/_Project/Code/UI/FishFinderOverlayHost.cs",
            "Assets/_Project/Code/UI/RadarOverlayHost.cs",
            "Assets/_Project/Code/UI/ChartplotterOverlayHost.cs",
            "Assets/_Project/Code/UI/HudBandLayout.cs",
            "Assets/_Project/Code/UI/HudController.cs",
            "Assets/_Project/Code/UI/InteractPopup.cs",
            "Assets/_Project/Code/Player/DevToast.cs",
            "Assets/_Project/Code/World/QuestPanelPresenter.cs",
        };

        [TearDown]
        public void TearDown() => HudScreen.Reset();

        [Test]
        public void AnOverride_IsTheScreenEveryReaderSees_UntilItIsReset()
        {
            HudScreen.Override(new Vector2Int(3840, 2160));
            Assert.IsTrue(HudScreen.IsOverridden);
            Assert.AreEqual(3840, HudScreen.Width);
            Assert.AreEqual(2160, HudScreen.Height);
            Assert.AreEqual(new Rect(0f, 0f, 3840f, 2160f), HudScreen.SafeArea, "a plate's screen is safe edge to edge");

            HudScreen.Reset();
            Assert.IsFalse(HudScreen.IsOverridden, "reset hands back the player's screen");
        }

        [Test]
        public void ASizeWithoutBothSides_HandsBackThePlayersScreen()
        {
            HudScreen.Override(new Vector2Int(1920, 1080));
            HudScreen.Override(new Vector2Int(0, 720));
            Assert.IsFalse(HudScreen.IsOverridden, "no width is no screen");

            HudScreen.Override(new Vector2Int(1920, 1080));
            HudScreen.Override(new Vector2Int(1280, -1));
            Assert.IsFalse(HudScreen.IsOverridden, "nor is no height");
        }

        [Test]
        public void Unset_ItIsThePlayersScreen()
        {
            Assert.IsFalse(HudScreen.IsOverridden, "premise: nothing in the game sets it");
            Assert.AreEqual(Screen.width, HudScreen.Width);
            Assert.AreEqual(Screen.height, HudScreen.Height);
            Assert.AreEqual(Screen.safeArea, HudScreen.SafeArea);
        }

        [Test]
        public void EveryOverlayOverTheHelm_ReadsTheScreenThroughTheSeam()
        {
            // One overlay reading the player's screen directly would stand where a 720p screen puts it on
            // a 4K plate, and the plate would show a HUD no player ever saw.
            foreach (string file in Readers)
            {
                Assert.IsTrue(File.Exists(file), $"premise: {file} is where it was");
                string code = CodeOnly(File.ReadAllText(file));
                StringAssert.Contains("HudScreen.", code, $"{file} reads the seam");
                foreach (string direct in new[] { "Screen.width", "Screen.height", "Screen.safeArea" })
                    StringAssert.DoesNotContain(direct, code, $"{file} reads {direct} around the seam");
            }
        }

        /// <summary>The source without its comment lines (the seam's own docs name the screen it wraps).</summary>
        private static string CodeOnly(string src)
            => string.Join("\n", src.Split('\n').Where(l => !l.TrimStart().StartsWith("//")));
    }
}
