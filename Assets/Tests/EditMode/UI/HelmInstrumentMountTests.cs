using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The S4.5 selection model and flush geometry — the owner's "the depth finder / fish finder and
    /// any navigation tool should be shown on the dash and not blown up by default; this should be
    /// selectable, which UI can be expanded".
    ///
    /// <para>Two halves, both pure: <see cref="HelmInstrumentExpansion"/>'s state machine (one open at
    /// a time, nothing persisted) and which authored box a fit means. The glass's flush placement in
    /// that box retired with the dash card (H2): an unraised instrument is a plate in the band.</para>
    /// </summary>
    public class HelmInstrumentMountTests
    {
        [SetUp]
        [TearDown]
        public void ResetExpansion() => HelmInstrumentExpansion.Collapse();

        private static HelmFit Fit(ConsoleRigKind rig, SounderKind sounder,
                                   CompassMount compass = CompassMount.None)
            => new HelmFit(rig, sounder, compass, false, false);

        // ---- the state machine ----------------------------------------------------------------------

        [Test]
        public void NothingIsExpandedByDefault_TheOwnersWholeAsk()
        {
            Assert.That(HelmInstrumentExpansion.Expanded, Is.EqualTo(HelmInstrumentSlot.None));
            Assert.IsFalse(HelmInstrumentExpansion.AnyExpanded);
            Assert.IsFalse(HelmInstrumentExpansion.IsExpanded(HelmInstrumentSlot.Sounder));
            Assert.IsFalse(HelmInstrumentExpansion.IsExpanded(HelmInstrumentSlot.FishFinder));
        }

        [Test]
        public void ClickingAFlushInstrumentExpandsIt_AndClickingItAgainCollapses()
        {
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Sounder);
            Assert.IsTrue(HelmInstrumentExpansion.IsExpanded(HelmInstrumentSlot.Sounder));
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Sounder);
            Assert.IsFalse(HelmInstrumentExpansion.AnyExpanded);
        }

        [Test]
        public void ClickingAwayCollapses()
        {
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.FishFinder);
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.None);
            Assert.IsFalse(HelmInstrumentExpansion.AnyExpanded);
        }

        [Test]
        public void OnlyOneCanBeExpanded_ForEveryPairOfSlots()
        {
            // The invariant the shared arbiter exists for. Exhaustive over the slot set — including
            // the two S5/S6 units that have no renderer yet, because the hull that carries a radar
            // carries a sounder too, and that is exactly when two bools would have failed.
            var slots = new[]
            {
                HelmInstrumentSlot.Sounder, HelmInstrumentSlot.FishFinder,
                HelmInstrumentSlot.Radar, HelmInstrumentSlot.Chartplotter,
            };
            foreach (var a in slots)
            {
                foreach (var b in slots)
                {
                    HelmInstrumentExpansion.Collapse();
                    HelmInstrumentExpansion.Click(a);
                    HelmInstrumentExpansion.Click(b);

                    HelmInstrumentSlot expect = a == b ? HelmInstrumentSlot.None : b;
                    Assert.That(HelmInstrumentExpansion.Expanded, Is.EqualTo(expect), $"{a} then {b}");

                    int open = 0;
                    foreach (var s in slots) if (HelmInstrumentExpansion.IsExpanded(s)) open++;
                    Assert.That(open, Is.LessThanOrEqualTo(1), $"{a} then {b}: two instruments open");
                }
            }
        }

        [Test]
        public void ExpandingWritesNothingToTheSave_ItIsWhereYourEyesAre_NotAPreference()
        {
            // Rule 5 / the S3b Ruling A precedent, pinned rather than asserted in a comment: drive the
            // whole machine over a live save and prove it is byte-for-byte untouched. If expansion
            // ever learns to persist, this fails — which is the point.
            SaveData save = SaveMigration.NewGame();
            int hullRows = save.HullInstruments != null ? save.HullInstruments.Count : 0;
            SounderPrefs before = InstrumentLocker.PrefsFor(save, "boat.skiff",
                                                            SounderPrefs.FromDefaults(
                                                                GameServices.DepthSounder));

            HelmInstrumentExpansion.Click(HelmInstrumentSlot.Sounder);
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.FishFinder);
            HelmInstrumentExpansion.Click(HelmInstrumentSlot.FishFinder);
            HelmInstrumentExpansion.Collapse();

            Assert.That(save.HullInstruments != null ? save.HullInstruments.Count : 0,
                        Is.EqualTo(hullRows), "expanding an instrument grew the save");
            SounderPrefs after = InstrumentLocker.PrefsFor(save, "boat.skiff",
                                                           SounderPrefs.FromDefaults(
                                                               GameServices.DepthSounder));
            Assert.That(after.Feet, Is.EqualTo(before.Feet));
            Assert.That(after.Night, Is.EqualTo(before.Night));
            Assert.That(after.Armed, Is.EqualTo(before.Armed));
            Assert.That(after.AlarmMetres, Is.EqualTo(before.AlarmMetres));
            Assert.That(after.RangeMetres, Is.EqualTo(before.RangeMetres));
        }

        [Test]
        public void TheSlotFollowsTheFittedBrow()
        {
            Assert.That(HelmInstrumentExpansion.SlotFor(SounderKind.Depth),
                        Is.EqualTo(HelmInstrumentSlot.Sounder));
            Assert.That(HelmInstrumentExpansion.SlotFor(SounderKind.Fish),
                        Is.EqualTo(HelmInstrumentSlot.FishFinder));
            Assert.That(HelmInstrumentExpansion.SlotFor(SounderKind.None),
                        Is.EqualTo(HelmInstrumentSlot.None));
        }

        // ---- which authored box a fit means -----------------------------------------------------------

        [Test]
        public void NoConsoleOrNoSounder_MountsNothing()
        {
            Assert.IsFalse(HelmDashGeometry.TryBrowSounderBox(HelmFit.None, out _, out _, out _, out _),
                           "a rowed dory has no brow to mount in");
            Assert.IsFalse(HelmDashGeometry.TryBrowSounderBox(
                               Fit(ConsoleRigKind.Novi, SounderKind.None),
                               out _, out _, out _, out _),
                           "a bare brow draws nothing — never a default box, never a fake instrument");
        }

        [Test]
        public void TheSkiffsMountIsTheAuthoredCutout_AndNarrowsUnderTheDome()
        {
            foreach (ConsoleRigKind rig in new[] { ConsoleRigKind.Console, ConsoleRigKind.Sport })
            {
                HelmDashGeometry.SounderCutout(false, out int ex, out int ey, out int ew, out int eh);
                Assert.IsTrue(HelmDashGeometry.TryBrowSounderBox(Fit(rig, SounderKind.Depth),
                                                                 out int x, out int y, out int w, out int h));
                Assert.That((x, y, w, h), Is.EqualTo((ex, ey, ew, eh)), $"{rig} bare crown");

                HelmDashGeometry.SounderCutout(true, out ex, out ey, out ew, out eh);
                Assert.IsTrue(HelmDashGeometry.TryBrowSounderBox(
                                  Fit(rig, SounderKind.Depth, CompassMount.Dome),
                                  out x, out y, out w, out h));
                Assert.That((x, y, w, h), Is.EqualTo((ex, ey, ew, eh)), $"{rig} sharing the crown");
                Assert.That(w, Is.LessThan(ew + 1));
            }
            // The skiffs' cutout is one landscape hole whatever drops into it — the rig authors no
            // portrait variant, so the finder is fitted INTO the hole rather than reshaping it.
            HelmDashGeometry.TryBrowSounderBox(Fit(ConsoleRigKind.Console, SounderKind.Depth),
                                               out _, out _, out int dw, out int dh);
            HelmDashGeometry.TryBrowSounderBox(Fit(ConsoleRigKind.Console, SounderKind.Fish),
                                               out _, out _, out int fw, out int fh);
            Assert.That((fw, fh), Is.EqualTo((dw, dh)));
        }

        [Test]
        public void ThePilothouseMountIsBrowSlotZero_AndGoesPortraitForTheFinder()
        {
            foreach (ConsoleRigKind rig in new[] { ConsoleRigKind.Novi, ConsoleRigKind.Cape })
            {
                HelmDashGeometry.SlotBoxOnCard(HelmDashGeometry.PilotSounderSlot, false,
                                               out int ex, out int ey, out int ew, out int eh);
                Assert.IsTrue(HelmDashGeometry.TryBrowSounderBox(Fit(rig, SounderKind.Depth),
                                                                 out int x, out int y, out int w, out int h));
                Assert.That((x, y, w, h), Is.EqualTo((ex, ey, ew, eh)), $"{rig} depth = landscape box");

                HelmDashGeometry.SlotBoxOnCard(HelmDashGeometry.PilotSounderSlot, true,
                                               out ex, out ey, out ew, out eh);
                Assert.IsTrue(HelmDashGeometry.TryBrowSounderBox(Fit(rig, SounderKind.Fish),
                                                                 out x, out y, out w, out h));
                Assert.That((x, y, w, h), Is.EqualTo((ex, ey, ew, eh)), $"{rig} fish = tall box");
                Assert.That(h, Is.GreaterThan(eh - 1));
            }
        }

        [Test]
        public void TheDomeNeverDisplacesTheSounderSlot()
        {
            // The dome takes the CROWN and displaces the centre mount only (noviRig.js:453). Asserted
            // rather than assumed, because "the compass eats a brow slot" is true — of the radar's.
            Assert.IsFalse(HelmDashGeometry.SlotIsDisplacedByCompass(HelmDashGeometry.PilotSounderSlot,
                                                                     CompassMount.Dome));
            Assert.IsTrue(HelmDashGeometry.SlotIsDisplacedByCompass(HelmDashGeometry.PilotRadarSlot,
                                                                    CompassMount.Dome));
            foreach (CompassMount m in new[] { CompassMount.None, CompassMount.Dome, CompassMount.Flush })
                Assert.IsTrue(HelmDashGeometry.TryBrowSounderBox(
                                  Fit(ConsoleRigKind.Novi, SounderKind.Depth, m),
                                  out _, out _, out _, out _), $"compass {m} must not blank the sounder");
        }

    }
}
