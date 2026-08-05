using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// S4.5's flush brow mounts — geometry pinned against the immutable rig sources, re-derived from
    /// the .js (never fudged): the skiff depth cutout is consoleRig.js:397-398
    /// (<c>bw=shift?126:148, bh=86, bx=shift?170:226, by=56</c>), the skiff FISH cutout is
    /// consoleRig.js:392-394 (<c>bh=shift?150:172, by=142-bh</c> — the colour sonar rises into the
    /// headroom), and the pilothouse mounts are noviRig.js:135-136/:454 (<c>slotBox(0, fish)</c>).
    /// All card-space values include the family's TOPPAD.
    /// </summary>
    public class DashBrowMountTests
    {
        // ---- the skiff fish cutout (new in S4.5) ---------------------------------------------------

        [Test]
        public void FinderCutout_PinsToConsoleRigJs()
        {
            HelmDashGeometry.FinderCutout(false, out int x, out int y, out int w, out int h);
            Assert.That((x, y, w, h), Is.EqualTo((226, 142 - 172 + HelmDashGeometry.TOPPAD, 148, 172)),
                        "consoleRig.js:393-394 unshifted: 148×172 at (226, 142−172), TOPPAD applied");

            HelmDashGeometry.FinderCutout(true, out x, out y, out w, out h);
            Assert.That((x, y, w, h), Is.EqualTo((170, 142 - 150 + HelmDashGeometry.TOPPAD, 126, 150)),
                        "dome fitted: slides to port and narrows (126×150 at 170), the depth box's own slide");
        }

        [Test]
        public void FinderCutout_SharesTheDepthCutoutsColumn_ButRisesIntoTheHeadroom()
        {
            // Same cutout, taller glass: identical x/w to the depth box; the top is ABOVE the depth
            // box's top (the TOPPAD headroom is exactly what the tall glass rises into).
            HelmDashGeometry.SounderCutout(false, out int dx, out int dy, out int dw, out _);
            HelmDashGeometry.FinderCutout(false, out int fx, out int fy, out int fw, out _);
            Assert.That(fx, Is.EqualTo(dx));
            Assert.That(fw, Is.EqualTo(dw));
            Assert.That(fy, Is.LessThan(dy), "the sonar's glass starts higher than the depth unit's");
        }

        // ---- the one mount resolver ----------------------------------------------------------------

        [Test]
        public void SounderMountOnCard_ResolvesEveryFamilyAndKind()
        {
            // Skiff + Depth = the S2 cutout.
            Assert.That(HelmDashGeometry.SounderMountOnCard(ConsoleRigKind.Console, SounderKind.Depth,
                                                            CompassMount.None,
                                                            out int x, out int y, out int w, out int h),
                        Is.True);
            HelmDashGeometry.SounderCutout(false, out int ex, out int ey, out int ew, out int eh);
            Assert.That((x, y, w, h), Is.EqualTo((ex, ey, ew, eh)));

            // Skiff + Fish = the taller cutout; the dome slide keys on the COMPASS, per the source.
            Assert.That(HelmDashGeometry.SounderMountOnCard(ConsoleRigKind.Sport, SounderKind.Fish,
                                                            CompassMount.Dome,
                                                            out x, out y, out w, out h), Is.True);
            HelmDashGeometry.FinderCutout(true, out ex, out ey, out ew, out eh);
            Assert.That((x, y, w, h), Is.EqualTo((ex, ey, ew, eh)));

            // Pilothouse = the sounder brow slot, portrait exactly when the finder mounts.
            Assert.That(HelmDashGeometry.SounderMountOnCard(ConsoleRigKind.Novi, SounderKind.Depth,
                                                            CompassMount.None,
                                                            out x, out y, out w, out h), Is.True);
            HelmDashGeometry.SlotBoxOnCard(HelmDashGeometry.PilotSounderSlot, false,
                                           out ex, out ey, out ew, out eh);
            Assert.That((x, y, w, h), Is.EqualTo((ex, ey, ew, eh)));

            Assert.That(HelmDashGeometry.SounderMountOnCard(ConsoleRigKind.Cape, SounderKind.Fish,
                                                            CompassMount.None,
                                                            out x, out y, out w, out h), Is.True);
            HelmDashGeometry.SlotBoxOnCard(HelmDashGeometry.PilotSounderSlot, true,
                                           out ex, out ey, out ew, out eh);
            Assert.That((x, y, w, h), Is.EqualTo((ex, ey, ew, eh)), "the finder takes the tall portrait box");
        }

        [Test]
        public void SounderMountOnCard_PilothouseSounderSlot_IgnoresTheDome()
        {
            // The dome displaces the CENTRE slot (radar), never the sounder's — noviRig.js:453.
            HelmDashGeometry.SounderMountOnCard(ConsoleRigKind.Novi, SounderKind.Depth,
                                                CompassMount.None, out int x0, out _, out _, out _);
            HelmDashGeometry.SounderMountOnCard(ConsoleRigKind.Novi, SounderKind.Depth,
                                                CompassMount.Dome, out int x1, out _, out _, out _);
            Assert.That(x1, Is.EqualTo(x0), "the pilothouse sounder slot does not slide for the dome");
        }

        [Test]
        public void NegativeControl_NoConsoleOrBareBrow_MountsNothing()
        {
            Assert.That(HelmDashGeometry.SounderMountOnCard(ConsoleRigKind.None, SounderKind.Depth,
                                                            CompassMount.None,
                                                            out _, out _, out _, out _), Is.False,
                        "no console, no mount");
            Assert.That(HelmDashGeometry.SounderMountOnCard(ConsoleRigKind.Console, SounderKind.None,
                                                            CompassMount.None,
                                                            out _, out _, out _, out _), Is.False,
                        "a bare brow mounts nothing — and can never be an expansion click target");
        }

        // ---- the expansion click target ------------------------------------------------------------

        [Test]
        public void IsOnSounderMount_HitsInsideTheGlass_MissesOutside_BothDirections()
        {
            HelmDashGeometry.SounderCutout(false, out int x, out int y, out int w, out int h);
            var centre = new Vector2(x + w * 0.5f, y + h * 0.5f);
            Assert.That(HelmDashGeometry.IsOnSounderMount(ConsoleRigKind.Console, SounderKind.Depth,
                                                          CompassMount.None, centre), Is.True);
            Assert.That(HelmDashGeometry.IsOnSounderMount(ConsoleRigKind.Console, SounderKind.Depth,
                                                          CompassMount.None,
                                                          new Vector2(x - 5, y - 5)), Is.False);
            // Negative control the other way: the SAME point over a bare brow is not a target.
            Assert.That(HelmDashGeometry.IsOnSounderMount(ConsoleRigKind.Console, SounderKind.None,
                                                          CompassMount.None, centre), Is.False);
        }

        [Test]
        public void TheTallFishGlass_IsClickable_WhereTheDepthBoxWasNot()
        {
            // The finder's glass rises into the headroom: a click there expands the finder but would
            // have missed the depth unit — the hit region follows the MOUNTED kind, not one fixed box.
            HelmDashGeometry.FinderCutout(false, out int x, out int y, out int w, out _);
            var highGlass = new Vector2(x + w * 0.5f, y + 10);
            Assert.That(HelmDashGeometry.IsOnSounderMount(ConsoleRigKind.Console, SounderKind.Fish,
                                                          CompassMount.None, highGlass), Is.True);
            Assert.That(HelmDashGeometry.IsOnSounderMount(ConsoleRigKind.Console, SounderKind.Depth,
                                                          CompassMount.None, highGlass), Is.False);
        }
    }
}
