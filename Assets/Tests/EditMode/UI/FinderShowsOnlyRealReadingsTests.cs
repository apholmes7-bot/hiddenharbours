using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The owner's 2026-11-13 ruling on readings the game does not simulate: the fish finder shows no
    /// battery cell and no volts, and neither the finder nor the depth sounder prints a water
    /// temperature while that temperature is a constant (<see cref="DepthSounderSettings.ShowWaterTemp"/>).
    ///
    /// <para>Each claim is made on the pixels: with the reading hidden, changing its value changes
    /// nothing on the glass. The rig as drawn (both flags on, which the golden tests compare against)
    /// is the control that shows the same value change DOES move pixels when the reading is shown.</para>
    /// </summary>
    public class FinderShowsOnlyRealReadingsTests
    {
        private static readonly List<SonarMark> NoMarks = new List<SonarMark>();

        private static FishRigState Live(in DepthSounderSettings sounder)
        {
            SounderPrefs prefs = SounderPrefs.FromDefaults(in sounder);
            FishFinderSettings finder = FishFinderSettings.Default;
            return FishFinderOverlayHost.LiveState(12f, finder.DefaultRangeMetres, in prefs, false, true,
                                                   false, false, 0d, FishRigAdjust.Range, in sounder, in finder);
        }

        private static FishRigState Drawn(float tempC, float batt, float volts, bool showTemp, bool showPower)
            => new FishRigState(12f, tempC, 20f, 3f, false, false, true, true, false, false, 0d,
                                FishRigAdjust.Range, 0.75f, true, batt, volts, showTemp, showPower);

        private static Color32[] Finder(in FishRigState state)
        {
            var s = new DrawSurface(FishRigRender.W, FishRigRender.H);
            FishRigRender.Render(s, in state, NoMarks);
            return s.Pixels;
        }

        private static Color32[] Sounder(float tempC, bool showTemp)
        {
            var s = new DrawSurface(DepthRigRender.W, DepthRigRender.H);
            var state = new DepthRigState(12f, false, false, true, 3f, tempC, false, showTemp);
            DepthRigRender.Render(s, in state);
            return s.Pixels;
        }

        private static bool Same(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b || a[i].a != b[i].a) return false;
            return true;
        }

        // ---- the finder's battery and volts ------------------------------------------------------------

        [Test]
        public void TheLiveFinder_ShowsNoBatteryAndNoVolts()
        {
            FishRigState live = Live(DepthSounderSettings.Default);
            Assert.IsFalse(live.ShowPower, "the live glass never draws the battery cell or the volts");
        }

        [Test]
        public void WithPowerHidden_NoBatteryOrVoltsValueReachesTheGlass()
        {
            Color32[] a = Finder(Drawn(12f, 0.8f, 4f, showTemp: true, showPower: false));
            Color32[] b = Finder(Drawn(12f, 0.1f, 13.8f, showTemp: true, showPower: false));
            Assert.IsTrue(Same(a, b), "a hidden supply reading draws nothing that depends on it");

            // Control: the rig as drawn does print them, so the comparison above is not vacuous.
            Color32[] shownA = Finder(Drawn(12f, 0.8f, 4f, showTemp: true, showPower: true));
            Color32[] shownB = Finder(Drawn(12f, 0.1f, 13.8f, showTemp: true, showPower: true));
            Assert.IsFalse(Same(shownA, shownB), "control: shown, the battery and the volts are on the glass");
            Assert.IsFalse(Same(a, shownA), "control: hiding them removed something");
        }

        // ---- the water temperature, on both instruments ------------------------------------------------

        [Test]
        public void TheTemperature_IsHiddenWhileItIsAConstant_AndThatIsData()
        {
            DepthSounderSettings shipped = DepthSounderSettings.Default;
            Assert.IsFalse(shipped.ShowWaterTemp, "no water-temperature model exists, so the line ships off");
            Assert.IsFalse(Live(in shipped).ShowTemp);

            DepthSounderSettings real = shipped;
            real.ShowWaterTemp = true;               // the switch a real temperature model turns on
            Assert.IsTrue(Live(in real).ShowTemp, "the same data switch brings the line back");
        }

        [Test]
        public void WithTheTemperatureHidden_NoTemperatureReachesTheFinder()
        {
            Color32[] a = Finder(Drawn(12f, 0.8f, 4f, showTemp: false, showPower: false));
            Color32[] b = Finder(Drawn(3.5f, 0.8f, 4f, showTemp: false, showPower: false));
            Assert.IsTrue(Same(a, b));

            Color32[] shownA = Finder(Drawn(12f, 0.8f, 4f, showTemp: true, showPower: false));
            Color32[] shownB = Finder(Drawn(3.5f, 0.8f, 4f, showTemp: true, showPower: false));
            Assert.IsFalse(Same(shownA, shownB), "control: shown, the temperature is on the glass");
        }

        [Test]
        public void WithTheTemperatureHidden_NoTemperatureReachesTheSounder()
        {
            Assert.IsTrue(Same(Sounder(12f, showTemp: false), Sounder(3.5f, showTemp: false)));
            Assert.IsFalse(Same(Sounder(12f, showTemp: true), Sounder(3.5f, showTemp: true)),
                           "control: shown, the temperature is on the glass");
            Assert.IsFalse(Same(Sounder(12f, showTemp: false), Sounder(12f, showTemp: true)),
                           "control: hiding it removed something");
        }

        [Test]
        public void TheRigAsDrawn_StillShowsEverything_SoTheGoldensStand()
        {
            var drawn = new FishRigState(12f, 12f, 20f, 3f, false, false, true, true, false, false, 0d,
                                         FishRigAdjust.Range, 0.75f, true, 0.8f, 4f);
            Assert.IsTrue(drawn.ShowTemp);
            Assert.IsTrue(drawn.ShowPower);
            Assert.IsTrue(new DepthRigState(12f, false, false, true, 3f, 12f, false).ShowTemp);
        }
    }
}
