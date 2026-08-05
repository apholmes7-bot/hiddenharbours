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
    /// a time, nothing persisted) and <see cref="HelmInstrumentMountLayout"/>'s placement of the glass
    /// in the console's own authored cutout.</para>
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

        [Test]
        public void TheMountFollowsTheDevBrowCycle()
        {
            // #409's K-cycle steps the brow bare → depth → fish on the boat under you. Each step has
            // to land somewhere real (or nowhere at all, for bare) with no second table to keep in
            // step — the mount is a pure function of the same fit the chrome draws its bezel from.
            Rect card = new Rect(0f, 0f, 600f, 548f);
            var fit0 = Fit(ConsoleRigKind.Novi, SounderKind.None);
            var fit1 = Fit(ConsoleRigKind.Novi, SounderKind.Depth);
            var fit2 = Fit(ConsoleRigKind.Novi, SounderKind.Fish);

            Assert.IsFalse(HelmInstrumentMountLayout.TryBrowSounderRect(fit0, card, out _));
            Assert.IsTrue(HelmInstrumentMountLayout.TryBrowSounderRect(fit1, card, out Rect depth));
            Assert.IsTrue(HelmInstrumentMountLayout.TryBrowSounderRect(fit2, card, out Rect fish));
            Assert.That(fish, Is.Not.EqualTo(depth), "the portrait finder does not sit where the sounder did");
            Assert.That(fish.height, Is.GreaterThan(depth.height));
        }

        // ---- the letterbox ---------------------------------------------------------------------------

        [Test]
        public void TheGlassFitsInsideItsBox_CentredAndUndistorted()
        {
            // 1:1 card so the numbers are readable; the flip from card space (y down) to screen space
            // (y up) is the part that is easy to get backwards.
            var card = new Rect(100f, 50f, HelmDashGeometry.W, HelmDashGeometry.H);
            var fit = Fit(ConsoleRigKind.Console, SounderKind.Depth);
            HelmDashGeometry.TryBrowSounderBox(fit, out int bx, out int by, out int bw, out int bh);
            Assert.IsTrue(HelmInstrumentMountLayout.TryBrowSounderRect(fit, card, out Rect m));

            float boxLeft = card.xMin + bx, boxBottom = card.yMax - (by + bh);
            Assert.That(m.xMin, Is.GreaterThanOrEqualTo(boxLeft - 0.01f));
            Assert.That(m.xMax, Is.LessThanOrEqualTo(boxLeft + bw + 0.01f));
            Assert.That(m.yMin, Is.GreaterThanOrEqualTo(boxBottom - 0.01f));
            Assert.That(m.yMax, Is.LessThanOrEqualTo(boxBottom + bh + 0.01f));

            Assert.That(m.center.x, Is.EqualTo(boxLeft + bw * 0.5f).Within(0.01f), "centred in x");
            Assert.That(m.center.y, Is.EqualTo(boxBottom + bh * 0.5f).Within(0.01f), "centred in y");
            Assert.That(m.width / m.height,
                        Is.EqualTo(DepthRigRender.W / (float)DepthRigRender.H).Within(1e-4f),
                        "the rig's aspect is never stretched to fill a hole");
        }

        [Test]
        public void TheDepthSounderNearlyFillsBothAuthoredCutouts()
        {
            // Not decoration: the depth rig (480×330, 1.4545) and the two authored mounts were drawn
            // for each other, so a big letterbox gap here means the wrong box has been picked. The
            // portrait finder in a landscape skiff hole legitimately does NOT fill it, which is why
            // this only claims it of the sounder.
            var card = new Rect(0f, 0f, HelmDashGeometry.W, HelmDashGeometry.H);
            var skiff = Fit(ConsoleRigKind.Console, SounderKind.Depth, CompassMount.Dome);
            HelmDashGeometry.TryBrowSounderBox(skiff, out _, out _, out int sw, out int sh);
            HelmInstrumentMountLayout.TryBrowSounderRect(skiff, card, out Rect sm);
            Assert.That(sm.width * sm.height / (sw * (float)sh), Is.GreaterThan(0.95f),
                        "the skiff cutout");

            var pilot = new Rect(0f, 0f, HelmDashGeometry.PilotW, HelmDashGeometry.PilotH);
            var novi = Fit(ConsoleRigKind.Novi, SounderKind.Depth);
            HelmDashGeometry.TryBrowSounderBox(novi, out _, out _, out int pw, out int ph);
            HelmInstrumentMountLayout.TryBrowSounderRect(novi, pilot, out Rect pm);
            Assert.That(pm.width * pm.height / (pw * (float)ph), Is.GreaterThan(0.95f),
                        "the pilothouse brow slot");
        }

        [Test]
        public void TheMountRidesTheDashCardsScale()
        {
            // The flush face is the dash's own texture rect scaled — so focusing the dash, or the
            // owner re-dialling DashSmallScale, moves and grows the instrument with it for free.
            var fit = Fit(ConsoleRigKind.Cape, SounderKind.Fish);
            var half = new Rect(0f, 0f, HelmDashGeometry.PilotW * 0.5f, HelmDashGeometry.PilotH * 0.5f);
            var full = new Rect(0f, 0f, HelmDashGeometry.PilotW, HelmDashGeometry.PilotH);
            Assert.IsTrue(HelmInstrumentMountLayout.TryBrowSounderRect(fit, half, out Rect a));
            Assert.IsTrue(HelmInstrumentMountLayout.TryBrowSounderRect(fit, full, out Rect b));
            Assert.That(b.width, Is.EqualTo(a.width * 2f).Within(0.01f));
            Assert.That(b.height, Is.EqualTo(a.height * 2f).Within(0.01f));
        }

        [Test]
        public void ACollapsedCardMountsNothing()
        {
            var fit = Fit(ConsoleRigKind.Novi, SounderKind.Depth);
            Assert.IsFalse(HelmInstrumentMountLayout.TryBrowSounderRect(fit, new Rect(0, 0, 0, 0), out _));
            Assert.IsFalse(HelmInstrumentMountLayout.TryBrowSounderRect(fit, new Rect(0, 0, 600, 0), out _));
        }
    }
}
