using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tests.Support;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>Phase B, 2026-09-19, C6 — the sport fisher's flybridge has a way down.</b> The charter's second
    /// guard, by name: "the sport fisher's flybridge has a way down". Both fishers stand her at an upper
    /// helm, and until this lane the only way down the art drew from it — the Convertible's companionway,
    /// the Skybridge's interior stairs — reached the game without its ends: the importer read the ladder
    /// and the stair as position-less routes, so leaving the helm stranded her aloft. The flybridge trap.
    ///
    /// <para>From wherever leaving each fisher's upper helm stands her, at every heading, the walk over her
    /// measured floors (<see cref="CabinWalkGraph"/>) must reach her cockpit by a path that takes at least
    /// one placed route — the ladder or the stair, never a drop the art did not draw — and must go on to
    /// reach her main door. The owner's rulings of 2026-09-19 fix what that path is: R2, the Convertible
    /// enters from above by the companionway; R3, the Skybridge stands her in the skylounge and takes her
    /// down the interior stairs.</para>
    ///
    /// <para>The hulls are built as the game builds them, by the fleet guard's
    /// <see cref="EveryInteriorHullReachesItsDoorFromItsStandTests.Build"/>.</para>
    ///
    /// <para>⚠ RED on the Interior defs as committed on 2026-09-19, by design: their routes carry no placed
    /// ends until Phase C re-imports them through the fixed reader and builder. This is the guard that
    /// says the regen landed.</para>
    /// </summary>
    public sealed class SportFisherFlybridgeHasAWayDownTests
    {
        /// <summary>The deck area the charter means by "down": both fishers' sidecars name it.</summary>
        private const string Cockpit = "cockpit";
        private const int Headings = 360;

        private static readonly string[] Fishers = { "SportFisherConvertible", "SportFisherSkybridge" };

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown() => EveryInteriorHullReachesItsDoorFromItsStandTests.Dismantle(_spawned);

        [Test]
        public void FromHerUpperHelm_ALadderOrStairTakesHerDownToHerCockpit(
            [ValueSource(nameof(Fishers))] string hullName)
        {
            var s = EveryInteriorHullReachesItsDoorFromItsStandTests.Build(hullName, _spawned);
            CabinWalkGraph graph = s.Graph;
            int cockpit = CockpitOf(graph, hullName);

            var report = new StringBuilder();
            foreach (KeyValuePair<CabinNode, List<int>> stand in UpperHelmStands(s))
            {
                CabinNode start = stand.Key;
                string at = $"{hullName} leaving her upper helm at headings " +
                            $"{EveryInteriorHullReachesItsDoorFromItsStandTests.Ranges(stand.Value)} -> {graph.Name(start)}";
                Assert.IsTrue(start.IsSomewhere, $"{at}: the game stands her on no floor the art measured");
                Assert.IsFalse(start.Kind == CabinNodeKind.Deck && start.Index == cockpit,
                    $"{at}: she is already in her cockpit — the helm the charter means is the one aloft");

                CabinWalk walk = graph.Walk(start, s.Before, s.After);
                bool down = false;
                CabinNode inTheCockpit = CabinNode.Nowhere;
                foreach (CabinNode n in walk.Nodes)
                {
                    if (n.Kind != CabinNodeKind.Deck || n.Index != cockpit) continue;
                    inTheCockpit = n;
                    down = true;
                    break;
                }
                Assert.IsTrue(down,
                    $"{at}: THE FLYBRIDGE TRAP — she can never reach her {Cockpit}. She can walk to " +
                    $"{walk.DescribeNodes()}.\nroutes: {RoutesOf(graph)}\ngraph: {graph.Describe()}");

                List<CabinHop> path = walk.PathTo(inTheCockpit);
                Assert.IsTrue(path.Any(h => h.Kind == CabinHopKind.Route),
                    $"{at}: she reaches her {Cockpit} by {walk.Describe(inTheCockpit)} — no ladder or stair on " +
                    $"the way, so the way down is one the art never drew.\nroutes: {RoutesOf(graph)}");
                report.AppendLine($"  {at}: {walk.Describe(inTheCockpit)}");
            }

            Debug.Log($"[flybridge] {hullName}: down to her {Cockpit} from every upper-helm stand\n{report}");
        }

        [Test]
        public void FromHerUpperHelm_SheWalksOnToHerMainDoor([ValueSource(nameof(Fishers))] string hullName)
        {
            var s = EveryInteriorHullReachesItsDoorFromItsStandTests.Build(hullName, _spawned);
            CabinWalkGraph graph = s.Graph;
            CockpitOf(graph, hullName);
            Assert.IsTrue(graph.Doors[0].IsMeasured,
                $"{hullName}: her main door measures no clear width, so no band can ever be walked into");

            var report = new StringBuilder();
            foreach (KeyValuePair<CabinNode, List<int>> stand in UpperHelmStands(s))
            {
                CabinNode start = stand.Key;
                string at = $"{hullName} leaving her upper helm at headings " +
                            $"{EveryInteriorHullReachesItsDoorFromItsStandTests.Ranges(stand.Value)} -> {graph.Name(start)}";
                Assert.IsTrue(start.IsSomewhere, $"{at}: the game stands her on no floor the art measured");

                CabinWalk walk = graph.Walk(start, s.Before, s.After);
                Assert.IsTrue(walk.TryGetReacher(0, out CabinNode reacher),
                    $"{at}: she never reaches {graph.Doors[0].Name}. She can walk to {walk.DescribeNodes()}.\n" +
                    $"routes: {RoutesOf(graph)}\ngraph: {graph.Describe()}");
                report.AppendLine($"  {at}: {graph.Doors[0].Name} by {walk.Describe(reacher)}");
            }

            Debug.Log($"[flybridge] {hullName}: on to her main door from every upper-helm stand\n{report}");
        }

        // =====================================================================================
        //  HELPERS
        // =====================================================================================

        /// <summary>The premises, asserted: she has an upper helm the art measured, one cockpit, and the
        /// helm stands above that cockpit by more than her floors' own tolerance. Returns the cockpit's
        /// area index.</summary>
        private static int CockpitOf(CabinWalkGraph graph, string hullName)
        {
            Assert.IsTrue(graph.Deck.HasHelmStation,
                $"{hullName}: her deck def measures no helm station — the upper helm the charter names is unmeasured");

            var cockpits = new List<int>();
            for (int i = 0; i < graph.AreaCount; i++)
                if (graph.AreaId(i) == Cockpit && CabinWalkGraph.IsWalkable(graph.AreaAt(i))) cockpits.Add(i);
            Assert.AreEqual(1, cockpits.Count,
                $"{hullName}: her deck def should name exactly one walkable '{Cockpit}' area; it names {cockpits.Count}");

            Vector3 station = graph.Deck.HelmStationLocalMeters;
            float cockpitSole = CabinWalkGraph.HeightOn(graph.AreaAt(cockpits[0]), station);
            Assert.Greater(station.z - cockpitSole, graph.Tolerance,
                $"{hullName}: her helm station ({station.z:0.00} m) does not stand above her {Cockpit} " +
                $"({cockpitSole:0.00} m under it) — there is no flybridge to come down from");
            return cockpits[0];
        }

        /// <summary>Where leaving the upper helm stands her, every heading, grouped by stand.</summary>
        private static IEnumerable<KeyValuePair<CabinNode, List<int>>> UpperHelmStands(
            EveryInteriorHullReachesItsDoorFromItsStandTests.Subject s)
        {
            var stands = new Dictionary<CabinNode, List<int>>();
            for (int h = 0; h < Headings; h++)
            {
                CabinNode start = s.Graph.HelmStand(h, s.HelmLocal, s.Elevation, s.Before, s.After);
                if (!stands.TryGetValue(start, out List<int> headings)) stands[start] = headings = new List<int>();
                headings.Add(h);
            }
            return stands.OrderBy(g => g.Value[0]);
        }

        private static string RoutesOf(CabinWalkGraph graph)
        {
            BoatInteriorRoute[] routes = graph.Def.Routes ?? System.Array.Empty<BoatInteriorRoute>();
            if (routes.Length == 0) return "(none)";
            return string.Join("; ", routes.Select(r =>
                $"{r.Id} {r.FromLevel}->{r.ToLevel} " +
                (r.Placed ? $"placed {r.FromPoint.ToString("F2")} -> {r.ToPoint.ToString("F2")}" : "POSITION-LESS")));
        }
    }
}
