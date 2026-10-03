using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>ST PETERS' TERRAIN PLAN HOLDS WHAT IT MUST</b> (terrain PR 5, key scenes first; the charter's test list). One
    /// case per guard in <see cref="TerrainPlanGuards.Names"/>, every one judged against ONE fresh derivation from the
    /// frozen sources (<c>StPetersPlan.sources.json</c>, today's ground sampled from <see cref="HiddenHarbours.World.TidalTerrain"/>,
    /// and since PR 5 B the plan's ground file imported, <see cref="StPetersGroundFileImportTests"/>) and the scene as committed:
    /// <list type="bullet">
    /// <item><b>Part 1's:</b> the frozen mask within one R16 step of today; the berths, wharf, buildings, roads,
    /// arrival route, cliffs, woods and placed things unchanged; the nav marks at least the mark floor deep at a spring
    /// low; the clams inside their band; ponds that do not leak and streams that never climb.</item>
    /// <item><b>The crossing's:</b> its sill is the gut's bed (−0.6 at x −234.1), so the walk is cut above −0.10; the
    /// crest band holds outside layout B's pools; the ground west of x −356 holds.</item>
    /// <item><b>The key scenes':</b> the Landing, and the cannery's 12 m, at 0.0 m; the Head clear of the arrival
    /// route's 30 m capsule and 21.06 m clear of the cannery's corner (170, 16), its stack and bar where
    /// <c>terrain.json</c> puts them; the cut inside x 99..114, y 55..69.4, the Fen Pool holding +5.25 and the plunge
    /// pool +2.05 (spilling only down its outflow); Ginny's plot and track at part 1's; the shore path never cut; the
    /// east cardinal deep; no shore rock on the Head's brow or the stack's plinth; no rock or clam hole on a key scene's
    /// path or lot; and the sea's range the map's.</item>
    /// <item><b>The ground file's</b> (PR 5 B, amendments 1 and 2): the cannery's circle pass 9's ground; the kept walls'
    /// channels wet at a spring low where pass 9's were; fix 2's west end only raising, no steeper than the beach and dry
    /// of still water; the beach's dry sand still over the spring high; the dipping pool at its surface; fix 4's hollow
    /// filled to its level, out of the cannery's circle and found by no still-water pass; the Heath Brook never climbing
    /// and ending at the shore; the crossing's walk off still water; and the main beach's sand by its recipe.</item>
    /// <item><b>The village's</b> (V1): every footprint and yard 3 m clear of the trunks, on the plateau (5.9 m under a
    /// footprint, 5.5 m under a yard), and clear of pass 9's barren and shore paths by half their width and 0.5 m.</item>
    /// </list>
    ///
    /// <para><b>The guards are pure</b> (<see cref="TerrainPlanGuards"/>): they read the derivation, the plan and the
    /// scene's text, never the engine, so the headless runner gives the same verdicts. Each case writes what it measured
    /// and where to the test's output. A red names the cell; the fix is in the Defs or the derivation and then the maps
    /// re-written (Hidden Harbours ▸ World ▸ Terrain Plan…), never a loosened guard.</para>
    /// </summary>
    public class StPetersTerrainPlanGuardTests
    {
        private List<TerrainPlanGuardCase> _cases;
        private string _derivation;

        [OneTimeSetUp]
        public void DeriveOnceAndJudge()
        {
            var sw = Stopwatch.StartNew();
            var plan = StPetersTerrainPlan.LoadPlan();
            var src = StPetersTerrainPlan.LoadFrozen();
            var r = TerrainPlanDerivation.Derive(plan, src);
            _derivation = r.Sha256();
            _cases = TerrainPlanGuards.All(StPetersTerrainPlan.GuardInput(plan, src, r));
            TestContext.WriteLine($"derivation {_derivation}; {_cases.Count(k => k.Pass)}/{_cases.Count} guards hold " +
                                  $"({sw.Elapsed.TotalSeconds:0.0} s)");
        }

        [TestCaseSource(typeof(TerrainPlanGuards), nameof(TerrainPlanGuards.Names))]
        public void TheGuard_Holds(string name)
        {
            var matches = _cases.Where(k => k.Name == name).ToList();
            Assert.AreEqual(1, matches.Count, $"the guards judged '{name}' {matches.Count} times");
            var k = matches[0];
            TestContext.WriteLine(k.ToString());
            foreach (var n in k.Numbers) TestContext.WriteLine($"  {n.Key} = {n.Value}");
            Assert.IsTrue(k.Pass, $"{k} (derivation {_derivation})");
        }
    }
}
