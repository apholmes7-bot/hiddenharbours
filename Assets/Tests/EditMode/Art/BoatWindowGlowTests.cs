using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art
{
    /// <summary>
    /// <b>Which windows the viewer can see into</b> — the rule that replaced the cabin disc's fade
    /// (owner's ruling, 2026-09-03), pinned against the runtime's own projection with no scene.
    ///
    /// <para><b>Why there is a rule at all.</b> A window is drawn as its four PROJECTED corners, so a
    /// wall turning away from the camera foreshortens to a sliver, reaches exactly zero area edge-on,
    /// and then shows the viewer its inside. Drawing that far side would put a wedge of amber across
    /// her own roof. <see cref="BoatWindowGlow.FacesCamera"/> drops it instead — and because it drops
    /// it at the instant the projected area is nought, there is nothing to fade and nothing to pop.</para>
    ///
    /// <para><b>The sign is the whole thing, and it is not obvious.</b> Get it backwards and the boat
    /// still looks lit — from inside out, on the walls you cannot see — which is the kind of wrong
    /// that ships. So it is pinned twice here, from two independent facts about the same projection:
    /// the DEPTH row (<c>world.z = rY·cos e − rZ·sin e</c>, nearer being smaller) and the SCREEN-UP row
    /// (<c>world.y = rY·sin e + rZ·cos e</c>, a wall facing the viewer throwing its light DOWN-screen).
    /// Both come out of <c>IsoFacetMath.RigToWorld</c>, which is the map the game actually draws with.
    /// </para>
    /// </summary>
    public class BoatWindowGlowTests
    {
        const float Elev = 40f;    // the fleet's shipped camera elevation (HullMeshDef.ElevationDeg)
        const int Facings = 8;     // ADR: eight, ruled

        /// <summary>The Cape Islander's own windows, as her rig publishes them — the three-pane raked
        /// screen, two lights a side, one small light aft. Written out rather than probed so this test
        /// needs no V8 host and no asset database: it is about the RULE, not about her data.</summary>
        static List<HullPane> CapeWindows()
        {
            var up = new Vector3(0f, 0.56f, 2.26f).normalized * 0.309f;   // her rake: (dy, dz) from HOUSE
            var panes = new List<HullPane>
            {
                new HullPane(HullWall.Front, new Vector3(-0.77f, 2.927f, 2.28f), new Vector3(0.27f, 0, 0), up),
                new HullPane(HullWall.Front, new Vector3(0.00f, 2.927f, 2.28f), new Vector3(0.34f, 0, 0), up),
                new HullPane(HullWall.Front, new Vector3(0.77f, 2.927f, 2.28f), new Vector3(0.27f, 0, 0), up),
                new HullPane(HullWall.Aft, new Vector3(0.90f, 0.500f, 2.52f), new Vector3(-0.28f, 0, 0),
                             new Vector3(0, 0, 0.16f)),
            };
            for (int i = 0; i < 2; i++)
            {
                float y = i == 0 ? 1.09f : 1.99f, halfLen = i == 0 ? 0.33f : 0.37f;
                panes.Add(new HullPane(HullWall.Starboard, new Vector3(1.32f, y, 2.21f),
                                       new Vector3(0f, -halfLen, 0f), new Vector3(0, 0, 0.23f)));
                panes.Add(new HullPane(HullWall.Port, new Vector3(-1.32f, y, 2.21f),
                                       new Vector3(0f, halfLen, 0f), new Vector3(0, 0, 0.23f)));
            }
            return panes;
        }

        static bool Faces(HullPane p, int dir) =>
            BoatWindowGlow.FacesCamera(IsoFacetMath.RigToWorld(dir, Elev).MultiplyVector(p.Outward));

        static bool AnyFacing(List<HullPane> panes, HullWall wall, int dir)
        {
            foreach (HullPane p in panes) if (p.Wall == wall && Faces(p, dir)) return true;
            return false;
        }

        // -------------------------------------------------------------------------------------------

        [Test]
        public void SternOnYouSeeHerAftWindowAndNotHerWindscreen()
        {
            // Facing 0 is bow-away: the rig's heading rotation is identity there, so her aft wall's
            // outward (0,−1,0) lands at depth −cos(e) — toward the camera — and her forward-and-down
            // raked screen lands at +(0.971·cos e + 0.241·sin e), away from it.
            foreach (HullPane p in CapeWindows())
            {
                if (p.Wall == HullWall.Aft)
                    Assert.IsTrue(Faces(p, 0), "stern-on, the light in her aft wall is the one you see");
                if (p.Wall == HullWall.Front)
                    Assert.IsFalse(Faces(p, 0),
                                   "and her windscreen is on the far side of the house — drawing it " +
                                   "would throw amber across her own roof");
            }
        }

        [Test]
        public void BowOnItIsExactlyTheOtherWayRound()
        {
            // The control on the test above: a sign error that satisfied one facing would have to
            // satisfy its opposite too, and it cannot.
            foreach (HullPane p in CapeWindows())
            {
                if (p.Wall == HullWall.Front)
                    Assert.IsTrue(Faces(p, 4), "bow-on, her lit windscreen is what you see");
                if (p.Wall == HullWall.Aft)
                    Assert.IsFalse(Faces(p, 4), "and the aft light is behind the house");
            }
        }

        [Test]
        public void OpposedWallsAreNeverBothVisible()
        {
            // Front/aft and port/starboard are opposite by construction, so at most one of each pair
            // can face the viewer at any heading — and at the crossing NEITHER does, which is correct
            // and is precisely where each has zero projected area anyway.
            List<HullPane> panes = CapeWindows();
            for (int d = 0; d < Facings; d++)
            {
                Assert.IsFalse(AnyFacing(panes, HullWall.Front, d) && AnyFacing(panes, HullWall.Aft, d),
                               $"facing {d}: she cannot show her windscreen and her aft light at once");
                Assert.IsFalse(AnyFacing(panes, HullWall.Port, d) && AnyFacing(panes, HullWall.Starboard, d),
                               $"facing {d}: she cannot show both her sides at once");
            }
        }

        [Test]
        public void NoWallIsPermanentlyDarkAndNoneIsPermanentlyLit()
        {
            // The rule must be a function of the HEADING. A sign or an axis fixed the wrong way could
            // still satisfy the pairs above while leaving one wall on (or off) at every facing, which
            // would read as a boat lit from one side for ever.
            List<HullPane> panes = CapeWindows();
            foreach (HullWall wall in new[] { HullWall.Front, HullWall.Aft, HullWall.Port, HullWall.Starboard })
            {
                int seen = 0;
                for (int d = 0; d < Facings; d++) if (AnyFacing(panes, wall, d)) seen++;
                Assert.Greater(seen, 0, $"her {wall} windows never show at any heading");
                Assert.Less(seen, Facings, $"her {wall} windows show at EVERY heading, including astern of them");
            }
        }

        [Test]
        public void SheIsNeverCompletelyDark()
        {
            List<HullPane> panes = CapeWindows();
            for (int d = 0; d < Facings; d++)
            {
                int lit = 0;
                foreach (HullPane p in panes) if (Faces(p, d)) lit++;
                Assert.Greater(lit, 0, $"facing {d}: every window on a lit boat was culled");
            }
        }

        [Test]
        public void AVisibleWindowThrowsItsLightDOWNTheScreen()
        {
            // ⭐ THE SECOND, INDEPENDENT PIN ON THE SIGN. FacesCamera reads the projection's DEPTH row;
            // this reads its SCREEN-UP row, which is different arithmetic over the same matrix. In a
            // three-quarter top-down view the ground's +y and height's +z both map UP the screen, so a
            // wall the viewer can see into must throw its wash DOWN the screen, toward them. If the
            // depth sign were inverted, every "visible" wall here would be washing away from the
            // camera and this test — not the one above — is what would catch it.
            foreach (HullPane p in CapeWindows())
            {
                if (p.Wall == HullWall.Port || p.Wall == HullWall.Starboard) continue;   // edge-on cases
                for (int d = 0; d < Facings; d++)
                {
                    Vector3 outward = IsoFacetMath.RigToWorld(d, Elev).MultiplyVector(p.Outward);
                    if (!BoatWindowGlow.FacesCamera(outward)) continue;
                    Assert.Less(outward.y, 0f,
                                $"{p.Wall} at facing {d} is called visible but its wash goes UP the " +
                                "screen, away from the viewer — the depth and screen rows disagree");
                }
            }
        }

        // -------------------------------------------------------------------------------------------

        [Test]
        public void APanesOutwardIsDerivedAndCannotBeArguedWith()
        {
            // The reason HullPane carries two vectors and no normal: a declared normal is a second
            // source of truth for something the geometry already fixes, and the failure it invites is
            // a window that lights the inside of its own cabin.
            var starboard = new HullPane(HullWall.Starboard, new Vector3(1.32f, 1.09f, 2.21f),
                                         new Vector3(0f, -0.33f, 0f), new Vector3(0f, 0f, 0.23f));
            Assert.AreEqual(Vector3.right, starboard.Outward);

            var port = new HullPane(HullWall.Port, new Vector3(-1.32f, 1.09f, 2.21f),
                                    new Vector3(0f, 0.33f, 0f), new Vector3(0f, 0f, 0.23f));
            Assert.AreEqual(Vector3.left, port.Outward);

            var aft = new HullPane(HullWall.Aft, new Vector3(0.9f, 0.5f, 2.52f),
                                   new Vector3(-0.28f, 0f, 0f), new Vector3(0f, 0f, 0.16f));
            Assert.AreEqual(new Vector3(0f, -1f, 0f), aft.Outward);

            // Sizes come off the same two vectors, so a pane cannot be one size and another shape.
            Assert.AreEqual(0.66f, starboard.WidthMetres, 1e-5f);
            Assert.AreEqual(0.46f, starboard.HeightMetres, 1e-5f);
        }

        [Test]
        public void ADegeneratePaneIsSkippedRatherThanDrawnAtRandom()
        {
            // A def field left at its default deserialises to all-zero. It must read as "no window",
            // not as a zero-area quad facing an arbitrary direction.
            var empty = default(HullPane);
            Assert.IsFalse(empty.IsUsable);
            Assert.AreEqual(Vector3.zero, empty.Outward, "and it names no direction at all");

            var flat = new HullPane(HullWall.Front, Vector3.zero, new Vector3(0.3f, 0, 0), Vector3.zero);
            Assert.IsFalse(flat.IsUsable, "a pane with no height is not a window");
        }
    }
}
