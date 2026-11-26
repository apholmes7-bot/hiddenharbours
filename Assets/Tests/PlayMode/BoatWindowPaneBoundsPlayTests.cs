using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The moored fleet's lit cabins have a pane mesh the camera can see.</b> The cabins at Nine
    /// Mile Creek drew no panes at night: every flag said the windows were lit and the renderer was on,
    /// but the pane mesh kept a zero-size box at the world origin, so the camera culled it. This walks
    /// the real fleet, switches every windowed cabin on, and fails on any lit boat whose pane renderer
    /// has a zero-size box or a box that misses her own facing panes.
    ///
    /// <para>No pixels are read, so no graphics device is needed: the box is CPU state, and this runs
    /// on CI as well as on a desk.</para>
    /// </summary>
    public class BoatWindowPaneBoundsPlayTests
    {
        WharfNightStage _stage;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
        }

        [UnityTest]
        public IEnumerator EveryLitCabinAtTheBerthsHasAPaneBoxAroundItsOwnWindows()
        {
            _stage = new WharfNightStage("NineMileCreek", "PaneBounds");
            yield return _stage.Load();
            yield return _stage.SetNight(2f);

            var lamps = Object.FindObjectsByType<BoatLamps>(FindObjectsSortMode.None)
                .Where(l => l.HasWindows).ToArray();
            Assume.That(lamps.Length, Is.GreaterThan(0), "the region has no windowed boats to check");
            foreach (var l in lamps) l.SetCabinLights(CabinLightsSwitch.On);
            for (int i = 0; i < 4; i++) yield return null;

            int checkedBoats = 0;
            foreach (var l in lamps)
            {
                var glow = l.GetComponent<BoatWindowGlow>();
                var hull = l.GetComponent<IsoFacetHullRenderer>();
                if (glow == null || hull == null || hull.PosedMesh == null) continue;
                string name = BoatLamps.BoatRootOf(l.transform).name;

                Assert.IsTrue(glow.Lit, $"{name}: the cabin was switched on but the windows say unlit");
                Assert.IsNotNull(glow.PaneRenderer, $"{name}: a windowed hull built no pane renderer");
                Assert.IsTrue(glow.PaneRenderer.enabled, $"{name}: a lit cabin's pane renderer is off");

                Bounds b = glow.PaneRenderer.bounds;
                Assert.Greater(b.size.x * b.size.y, 0f,
                    $"{name}: the lit pane mesh has a zero-size box {b}, so the camera culls her windows");

                // Sorted as part of HER, so a hull nearer the camera can never tie with the panes alone
                // (one rung over a root group tied with a runner a quarter metre nearer and showed through).
                if (GlassLook.Current.SortsInWorld)
                {
                    var paneGroup = glow.PaneRenderer.GetComponent<UnityEngine.Rendering.SortingGroup>();
                    Assert.IsTrue(glow.PanesNestInCarrier, $"{name}: the lit panes do not sort inside her own group");
                    Assert.IsNotNull(paneGroup, $"{name}: the panes lost their sorting group");
                    Assert.IsFalse(paneGroup.sortAtRoot, $"{name}: the panes still sort at the root, over her group");
                    var carrier = SceneLight.OuterSortingGroup(glow.transform);
                    Assert.AreEqual(carrier.sortingOrder, glow.PaneWorldOrder,
                        $"{name}: among the world her panes compete as she does");
                }

                foreach (HullPane p in hull.Panes)
                {
                    if (!p.IsUsable) continue;
                    if (!BoatWindowGlow.FacesCamera(hull.PosedMesh.TransformVector(p.Outward))) continue;
                    Vector3 c = hull.PosedMesh.TransformPoint(p.CentreMetres);
                    var at = new Vector3(c.x, c.y, b.center.z);
                    Assert.IsTrue(b.Contains(at),
                        $"{name}: a facing pane at ({c.x:0.00}, {c.y:0.00}) is outside the pane box {b}");
                }
                checkedBoats++;
            }
            Assert.Greater(checkedBoats, 0, "no lit windowed hull was found to check");
        }
    }
}
