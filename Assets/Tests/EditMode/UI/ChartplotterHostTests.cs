using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The chartplotter HOST's pure parts (ADR 0025 S6 PR 3): the hit map every control resolves
    /// through, the chart-tap → world conversion a waypoint removal needs, the QUANTIZATION that keeps
    /// steady sailing off the raster path (rule 7), the range ladder's stepping, and the persistence
    /// contract for the new preferences.
    ///
    /// <para><b>Why the preference no-op test sweeps EVERY field.</b> The three-edit prefs trap (DTO +
    /// runtime struct + the locker's comparison) has bitten this project before: v9's
    /// <c>RangeMetres</c> shipped able to move the glass and never the save, because the field was added
    /// to two of the three places. A test that only checks "a changed pref persists" passes with a
    /// field missing from the comparison; one that checks "each field INDIVIDUALLY makes the locker
    /// report a change" cannot.</para>
    ///
    /// <para>Pure maths and a bare <see cref="SaveData"/> — no scene, no canvas, no services, so this
    /// runs headless. The live host is covered in PlayMode, where its differential guards live.</para>
    /// </summary>
    public class ChartplotterHostTests
    {
        private static ChartplotterSettings Cfg => ChartplotterSettings.Default;

        private static NavRigState State(float rangeNM = 0.2f,
                                         NavRigGeometry.Orient orient = NavRigGeometry.Orient.North,
                                         float heading = 0f)
            => new NavRigState(new Vector2(400f, 300f), true, heading, 6f, rangeNM, orient, false,
                               true, 0f, false);

        /// <summary>The centre of a box, in rig pixels.</summary>
        private static Vector2 Centre(RectInt r)
            => new Vector2(r.x + r.width / 2f, r.y + r.height / 2f);

        // ---- the hit map -------------------------------------------------------------------------------

        [Test]
        public void EachControl_ResolvesToItsOwnCode_AndAMissResolvesToNothing()
        {
            NavRigGeometry.Layout L = ChartplotterOverlayLayout.NativeLayout();

            Assert.AreEqual(ChartplotterOverlayLayout.KeyMax,
                            ChartplotterOverlayLayout.HitTest(Centre(L.Key(0))), "keys[0] is MAX/CNSL");
            Assert.AreEqual(ChartplotterOverlayLayout.KeyMark,
                            ChartplotterOverlayLayout.HitTest(Centre(L.Key(1))), "keys[1] is MARK");
            Assert.AreEqual(ChartplotterOverlayLayout.KeyIn,
                            ChartplotterOverlayLayout.HitTest(Centre(L.Key(2))), "keys[2] is IN");
            Assert.AreEqual(ChartplotterOverlayLayout.KeyOut,
                            ChartplotterOverlayLayout.HitTest(Centre(L.Key(3))), "keys[3] is OUT");

            Assert.AreEqual(ChartplotterOverlayLayout.ChartHit,
                            ChartplotterOverlayLayout.HitTest(Centre(L.Chart)));
            Assert.AreEqual(ChartplotterOverlayLayout.RouteHit,
                            ChartplotterOverlayLayout.HitTest(Centre(L.BotBar)));

            // The orientation legend sits INSIDE the data strip, so the strip's left end must still be
            // the night tap and only its right end the orient toggle.
            RectInt orient = ChartplotterOverlayLayout.OrientBox(in L);
            Assert.AreEqual(ChartplotterOverlayLayout.OrientHit,
                            ChartplotterOverlayLayout.HitTest(Centre(orient)));
            Assert.AreEqual(ChartplotterOverlayLayout.NightHit,
                            ChartplotterOverlayLayout.HitTest(new Vector2(L.TopBar.x + 4f,
                                                                          Centre(L.TopBar).y)),
                            "the rest of the data strip is the backlight tap, the finder's gesture");

            Assert.AreEqual(ChartplotterOverlayLayout.NoHit,
                            ChartplotterOverlayLayout.HitTest(new Vector2(-20f, -20f)),
                            "off the face entirely");
        }

        [Test]
        public void TheOrientBox_SitsAtTheRightEndOfTheStrip_WhereTheLegendIsActuallyPrinted()
        {
            NavRigGeometry.Layout L = ChartplotterOverlayLayout.NativeLayout();
            RectInt o = ChartplotterOverlayLayout.OrientBox(in L);

            Assert.AreEqual(L.TopBar.x + L.TopBar.width, o.x + o.width,
                            "right-aligned, because NavRigRender right-aligns the legend there");
            Assert.That(o.width, Is.LessThan(L.TopBar.width),
                        "and it must not swallow the whole strip, or the night tap becomes unreachable");
            Assert.That(o.width, Is.GreaterThanOrEqualTo(RigDrawUtil.TextW("NORTH-UP", 1)),
                        "…while still covering the widest legend it can print");
        }

        // ---- the chart tap -----------------------------------------------------------------------------

        [Test]
        public void AChartTapAtTheCentre_IsOwnShip_AndTheRadiusScalesWithTheRange()
        {
            NavRigGeometry.Layout L = ChartplotterOverlayLayout.NativeLayout();
            NavRigState st = State(rangeNM: 0.2f);

            Assert.IsTrue(ChartplotterOverlayLayout.TryChartTapToWorld(Centre(L.Chart), in st,
                                                                       out Vector2 world,
                                                                       out float radiusWide));
            Assert.That(world.x, Is.EqualTo(st.Boat.x).Within(0.5f));
            Assert.That(world.y, Is.EqualTo(st.Boat.y).Within(0.5f),
                        "the chart is centred on own ship, so its centre pixel IS the boat");

            // The same fingertip covers less water at a closer range — that is the whole reason the
            // radius is specified in chart pixels rather than metres.
            NavRigState close = State(rangeNM: 0.05f);
            Assert.IsTrue(ChartplotterOverlayLayout.TryChartTapToWorld(Centre(L.Chart), in close,
                                                                       out _, out float radiusClose));
            Assert.That(radiusClose, Is.LessThan(radiusWide));
            Assert.That(radiusClose, Is.GreaterThan(0f));
        }

        [Test]
        public void ATapOutsideTheChart_IsNotAChartTap()
        {
            NavRigGeometry.Layout L = ChartplotterOverlayLayout.NativeLayout();
            NavRigState st = State();
            Assert.IsFalse(ChartplotterOverlayLayout.TryChartTapToWorld(Centre(L.BotBar), in st,
                                                                        out _, out _),
                           "the route strip is not water");
        }

        // ---- the repaint quantum (rule 7) --------------------------------------------------------------

        [Test]
        public void OwnShip_QuantizesToWholeChartPixels_SoDriftDoesNotCostARaster()
        {
            const float range = 0.2f;
            float metresPerPx = ChartplotterOverlayLayout.MetresPerChartPixel(range);
            Assert.That(metresPerPx, Is.GreaterThan(0f));

            var at = new Vector2(400f, 300f);
            long key = ChartplotterOverlayLayout.QuantizePosition(at, range);

            // A tenth of a chart pixel cannot move the picture, so it must not move the key.
            long nudged = ChartplotterOverlayLayout.QuantizePosition(
                at + new Vector2(metresPerPx * 0.1f, 0f), range);
            Assert.AreEqual(key, nudged, "a sub-pixel drift must not repaint (rule 7)");

            // THE NEGATIVE CONTROL: the same probe must still register a real move, or the assertion
            // above would pass on a key that is simply constant.
            long moved = ChartplotterOverlayLayout.QuantizePosition(
                at + new Vector2(metresPerPx * 3f, 0f), range);
            Assert.AreNotEqual(key, moved,
                               "…but three whole chart pixels IS a different picture — if this fails, " +
                               "the sub-pixel assertion above proves nothing");
        }

        [Test]
        public void AWiderRange_MakesTheQuantumCoarser()
        {
            Assert.That(ChartplotterOverlayLayout.MetresPerChartPixel(1.6f),
                        Is.GreaterThan(ChartplotterOverlayLayout.MetresPerChartPixel(0.05f)),
                        "one screen pixel covers more water at a wider range, so the boat may move " +
                        "further before the glass owes a repaint");
        }

        // ---- the expanded card -------------------------------------------------------------------------

        [Test]
        public void TheExpandedCard_IsAnIntegerScaleOfTheConsoleFace_AndCentred()
        {
            Rect card = ChartplotterOverlayLayout.ExpandedCardRect(1920f, 1080f);
            float scale = card.width / NavRigGeometry.ConsoleW;

            Assert.That(scale, Is.EqualTo(Mathf.Round(scale)).Within(1e-4f),
                        "a fractional scale resamples the 3x5 font into mush");
            Assert.That(scale, Is.GreaterThanOrEqualTo(1f));
            Assert.That(card.height / NavRigGeometry.ConsoleH, Is.EqualTo(scale).Within(1e-4f),
                        "aspect preserved — the same face, larger");
            Assert.That(card.center.x, Is.EqualTo(960f).Within(0.5f));
            Assert.That(card.center.y, Is.EqualTo(540f).Within(0.5f));
        }

        [Test]
        public void OnATinyScreen_TheCardFallsBackToNativeSize_RatherThanBelowIt()
        {
            Rect card = ChartplotterOverlayLayout.ExpandedCardRect(640f, 400f);
            Assert.AreEqual(NavRigGeometry.ConsoleW, card.width, 1e-3f,
                            "never smaller than 1x — a downscaled rig is illegible, and the rig is the " +
                            "read in this state");
        }

        // ---- the range ladder --------------------------------------------------------------------------

        [Test]
        public void TheRangeSteps_ClampAtBothEnds_AndNeverWrap()
        {
            ChartplotterSettings cfg = Cfg;
            var prefs = new ChartplotterPrefs(false, false, 0);

            Assert.AreEqual(0, prefs.SteppedRange(+1, in cfg).RangeStep,
                            "IN at the closest range stays put — wrapping to the widest at a wharf " +
                            "would be a very unwelcome surprise");

            int top = cfg.SafeRangeStepCount - 1;
            var wide = new ChartplotterPrefs(false, false, top);
            Assert.AreEqual(top, wide.SteppedRange(-1, in cfg).RangeStep, "and OUT at the widest");

            // …and in between it actually moves, in the direction the pusher's label promises.
            var mid = new ChartplotterPrefs(false, false, 2);
            Assert.AreEqual(1, mid.SteppedRange(+1, in cfg).RangeStep, "IN is a CLOSER range");
            Assert.AreEqual(3, mid.SteppedRange(-1, in cfg).RangeStep, "OUT is a wider one");
            Assert.That(mid.SteppedRange(+1, in cfg).RangeNM(in cfg),
                        Is.LessThan(mid.RangeNM(in cfg)));
        }

        [Test]
        public void AFreshPlotter_WakesOnTheOwnersNominatedRung()
        {
            ChartplotterSettings cfg = Cfg;
            ChartplotterPrefs fresh = ChartplotterPrefs.FromDefaults(in cfg);
            Assert.AreEqual(cfg.SafeDefaultRangeStep, fresh.RangeStep);
            Assert.IsFalse(fresh.HeadUp, "north-up is the chart convention");
            Assert.IsFalse(fresh.Night);
        }

        // ---- persistence: the THREE-EDIT TRAP ----------------------------------------------------------

        [Test]
        public void EveryPreferenceField_IsInTheLockersComparison_OrItWouldSilentlyStopPersisting()
        {
            const string hull = "boat.novi";
            var baseline = new ChartplotterPrefs(false, false, 2);

            // Each arm changes exactly ONE field from the same baseline and requires the locker to
            // report a real change. A field missing from SetChartplotterPrefs's comparison fails here
            // and nowhere else — which is precisely how v9's RangeMetres shipped half-wired.
            var arms = new (string name, ChartplotterPrefs prefs)[]
            {
                ("Night", baseline.WithNight(true)),
                ("HeadUp", baseline.WithHeadUp(true)),
                ("RangeStep", new ChartplotterPrefs(baseline.Night, baseline.HeadUp, 4)),
            };

            foreach ((string name, ChartplotterPrefs moved) in arms)
            {
                var save = new SaveData();
                Assert.IsTrue(InstrumentLocker.SetChartplotterPrefs(save, hull, in baseline),
                              "the first write always lands");
                Assert.IsTrue(InstrumentLocker.SetChartplotterPrefs(save, hull, in moved),
                              $"changing {name} must reach the save — if this fails, that field is " +
                              "missing from the locker's field-by-field comparison (the three-edit trap)");
                Assert.AreEqual(moved.Night,
                                InstrumentLocker.ChartplotterPrefsFor(save, hull, in baseline).Night);
                Assert.AreEqual(moved.HeadUp,
                                InstrumentLocker.ChartplotterPrefsFor(save, hull, in baseline).HeadUp);
                Assert.AreEqual(moved.RangeStep,
                                InstrumentLocker.ChartplotterPrefsFor(save, hull, in baseline).RangeStep);
            }
        }

        [Test]
        public void AnUnchangedPress_DoesNotDirtyTheSave()
        {
            var save = new SaveData();
            var prefs = new ChartplotterPrefs(true, true, 3);
            Assert.IsTrue(InstrumentLocker.SetChartplotterPrefs(save, "boat.cape", in prefs));
            Assert.IsFalse(InstrumentLocker.SetChartplotterPrefs(save, "boat.cape", in prefs),
                           "re-writing identical preferences must report no change, so a no-op button " +
                           "press never costs an I/O");
            Assert.AreEqual(1, save.HullChartplotterPrefs.Count, "and never appends a duplicate row");
        }

        [Test]
        public void PreferencesAreOwnedPerHull_NotSharedAcrossTheFleet()
        {
            var save = new SaveData();
            var novi = new ChartplotterPrefs(false, true, 1);
            var cape = new ChartplotterPrefs(true, false, 4);
            InstrumentLocker.SetChartplotterPrefs(save, "boat.novi", in novi);
            InstrumentLocker.SetChartplotterPrefs(save, "boat.cape", in cape);

            ChartplotterPrefs fallback = ChartplotterPrefs.FromDefaults(Cfg);
            Assert.AreEqual(1, InstrumentLocker.ChartplotterPrefsFor(save, "boat.novi", in fallback)
                                               .RangeStep);
            Assert.AreEqual(4, InstrumentLocker.ChartplotterPrefsFor(save, "boat.cape", in fallback)
                                               .RangeStep);
            Assert.AreEqual(fallback.RangeStep,
                            InstrumentLocker.ChartplotterPrefsFor(save, "boat.dory", in fallback)
                                            .RangeStep,
                            "a hull that has never been touched reads the owner's defaults");
        }

        [Test]
        public void ASaveWrittenBeforeThisMemberExisted_ReadsAsNeverTouched_NotAsGarbage()
        {
            // The additive-v10 contract: no rows is not a broken save, it is a save from before the
            // preference existed. It must yield the defaults rather than a zeroed struct — a zero
            // RangeStep would silently pin every existing plotter to the closest range.
            var save = new SaveData { HullChartplotterPrefs = null };
            ChartplotterPrefs fallback = ChartplotterPrefs.FromDefaults(Cfg);
            ChartplotterPrefs got = InstrumentLocker.ChartplotterPrefsFor(save, "boat.novi", in fallback);
            Assert.AreEqual(fallback.RangeStep, got.RangeStep);
            Assert.AreNotEqual(0, fallback.RangeStep,
                               "…and the default is deliberately NOT rung 0, which is what makes the " +
                               "assertion above able to tell the two apart");
        }
    }
}
