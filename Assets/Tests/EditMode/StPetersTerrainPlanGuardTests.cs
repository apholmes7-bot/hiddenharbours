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
    /// <c>terrain.json</c> puts them; the cut inside its imported ask's box, the Fen Pool holding +5.25 and the plunge
    /// pool +2.062 at base flow (spilling only down its outflow); Ginny's plot and track at part 1's; the shore path never cut; the
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
        private TerrainPlanGuardInput _input;

        [OneTimeSetUp]
        public void DeriveOnceAndJudge()
        {
            var sw = Stopwatch.StartNew();
            var plan = StPetersTerrainPlan.LoadPlan();
            var src = StPetersTerrainPlan.LoadFrozen();
            var r = TerrainPlanDerivation.Derive(plan, src);
            _derivation = r.Sha256();
            _input = StPetersTerrainPlan.GuardInput(plan, src, r);
            _cases = TerrainPlanGuards.All(_input);
            TestContext.WriteLine($"derivation {_derivation}; {_cases.Count(k => k.Pass)}/{_cases.Count} guards hold " +
                                  $"({sw.Elapsed.TotalSeconds:0.0} s)");
        }

        // A source rule alone does not prove the committed map carries that level: a bilinear footprint
        // can include code 0 (none). Exercise the runtime sampler on the committed texture, not r.Still.
        [Test]
        public void TheCommittedStillWater_HoldsAllFiveSourceHeads()
        {
            var plan = StPetersTerrainPlan.LoadPlan();
            var map = StPetersTerrainPlan.LoadSeabed();
            Assert.NotNull(map.StillLevelTexture, "the committed still map must be assigned");
            var still = new PaintedStillWater(map.StillLevelTexture, map.WorldCenter, map.WorldSize,
                                             map.MinElevation, map.MaxElevation);
            Assert.IsTrue(still.IsBound, "the committed still map must be readable");
            CheckSourceHeads(plan, still, (map.MaxElevation - map.MinElevation) / TerrainPlanMaps.CodeCount);
        }

        // Kept separate so the same assertions and runtime sampling can also run headless after a
        // managed PNG decode; only AssetDatabase/Texture2D acquisition requires the editor.
        static void CheckSourceHeads(RegionTerrainPlanDef plan, HiddenHarbours.Core.IStillWater still, float step)
        {
            var expected = new Dictionary<string, float>
            {
                { "stream.stp_barren_brook", 5.3f },
                { "stream.stp_alder_run", 5.25f },
                { "stream.stp_gap_brook", 5.3f },
                { "stream.stp_storm_brook", 5.3f },
                { "stream.stp_heath_brook", 5.3f }
            };
            var heads = plan.Streams.Where(s => s.Source != null).ToArray();
            CollectionAssert.AreEquivalent(expected.Keys, heads.Select(s => s.Id).ToArray(),
                "all five sourced heads must be checked; Cove Brook has no source pond");
            var failures = new List<string>();
            foreach (var stream in heads)
            {
                Assert.IsNotEmpty(stream.Points, stream.Id + " has no head");
                Assert.AreEqual(expected[stream.Id], stream.Source.Surface, stream.Id + " source rule moved");
                float want = (float)TerrainPlanDerivation.BrookHeadLevel(stream, stream.BedZ[0], 0);
                Assert.AreEqual(stream.Source.Surface, want, stream.Id + " does not use Source.Surface");
                var head = stream.Points[0];
                float actual = still.StillLevelAt(head);
                float error = UnityEngine.Mathf.Abs(actual - want);
                string measured = $"{stream.Id} at ({head.x:R}, {head.y:R}): committed {actual:R} m, " +
                                  $"source {want:R} m, error {error:R} m, one R16 step {step:R} m";
                TestContext.WriteLine(measured);
                if (!(error <= step)) failures.Add(measured); // also rejects NaN and None
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
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

        [Test]
        public void EveryNewlyHeldVillageCell_RejectsEvenOneChangedCode()
        {
            var result = _input.Result;
            foreach (int index in _input.VillageHold.Cells)
            {
                int pixel = TerrainPlanMaps.Pixel(result.Grid, index);
                ushort before = result.GroundCodes[pixel];
                try
                {
                    result.GroundCodes[pixel] = (ushort)(before == 65535 ? before - 1 : before + 1);
                    Assert.IsFalse(_input.VillageHold.Matches(result, index), $"cell {index} accepted a changed code");
                }
                finally { result.GroundCodes[pixel] = before; }
            }
        }

        // One cell in the core and one in the feather that the former bare-disc premise missed.
        [TestCase(657457)]
        [TestCase(660544)]
        public void BothMovedPremiseGuards_RejectChangedVillageGround(int index)
        {
            var result = _input.Result;
            Assert.IsTrue(_input.VillageHold.Contains(index));
            int pixel = TerrainPlanMaps.Pixel(result.Grid, index);
            ushort before = result.GroundCodes[pixel];
            double elevation = result.E[index];
            try
            {
                result.GroundCodes[pixel] = (ushort)(before + 1);
                result.E[index] = TerrainPlanMaps.Decode((ushort)(before + 1), _input.MapMin, _input.MapMax);
                var cases = TerrainPlanGuards.All(_input).Where(k => k.Name == "FrozenMaskHoldsToday" || k.Name == "HeldGroundUnchanged").ToArray();
                Assert.AreEqual(2, cases.Length);
                foreach (var guard in cases) Assert.IsFalse(guard.Pass, guard.ToString());
            }
            finally { result.GroundCodes[pixel] = before; result.E[index] = elevation; }
        }
    }
}
