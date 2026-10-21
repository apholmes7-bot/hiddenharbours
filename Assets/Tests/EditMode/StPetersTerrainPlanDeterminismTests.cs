using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>ST PETERS' GROUND IS A FUNCTION OF ITS PLAN</b> (terrain PR 5; rule 5, the simulation deterministic). The
    /// committed maps are not hand-painted: they are what <see cref="TerrainPlanDerivation"/> makes of the plan's Defs
    /// and the frozen sources, so this fixture pins three things.
    /// <list type="number">
    /// <item><b>Two derivations are identical.</b> Each from a fresh read of the frozen sources and a fresh sample of
    /// today's ground: the same hash, the same order log, the same numbers.</item>
    /// <item><b>Each stands on its ground file's import</b> (PR 5 B): the two imports' codes are identical, the
    /// derivation's ground is their decode and its height map their codes, and the plan's own ground is kept as the
    /// check.</item>
    /// <item><b>The committed maps equal a fresh one.</b> The R16 height, the still water and the six splat maps read
    /// back code for code (<see cref="TerrainPlanMapWriter.Differences"/>), the seabed asset's range and still binding
    /// are the plan's, and the manifest records this derivation, this ground and each map's values.</item>
    /// <item><b>The frozen sources are canonical.</b> Read and written again, the file is byte for byte itself.</item>
    /// </list>
    ///
    /// <para><b>When it reds.</b> A Def or the derivation changed and the maps were not re-written: write them again
    /// (Hidden Harbours ▸ World ▸ Terrain Plan… ▸ Write maps) and commit them with the change. If only the ground's
    /// hash differs from the manifest's, this machine samples <see cref="TidalTerrain"/> differently from the one that
    /// wrote the maps (a float difference between platforms), and the maps' writer is the arbiter.</para>
    /// </summary>
    public class StPetersTerrainPlanDeterminismTests
    {
        private RegionTerrainPlanDef _plan;
        private TerrainPlanResult _a, _b;
        private GroundFileImport.Result _importA, _importB;
        private string _groundSha;

        [OneTimeSetUp]
        public void DeriveTwiceFromScratch()
        {
            var sw = Stopwatch.StartNew();
            _plan = StPetersTerrainPlan.LoadPlan();
            var first = StPetersTerrainPlan.LoadFrozen();
            _a = TerrainPlanDerivation.Derive(_plan, first);
            _groundSha = TerrainPlanMaps.Sha256(first.Base);
            var second = StPetersTerrainPlan.LoadFrozen();
            _b = TerrainPlanDerivation.Derive(_plan, second);
            _importA = first.Import;
            _importB = second.Import;
            TestContext.WriteLine($"derivations {_a.Sha256()} and {_b.Sha256()}; today's ground {_groundSha}; the ground file's codes " +
                                  $"{_importA?.CodesSha256 ?? "(none)"} and {_importB?.CodesSha256 ?? "(none)"} ({sw.Elapsed.TotalSeconds:0.0} s for both)");
        }

        [Test]
        public void TwoDerivations_FromScratch_AreIdentical()
        {
            Assert.AreEqual(_a.Sha256(), _b.Sha256(), "two derivations from the same Defs and sources hash differently");
            CollectionAssert.AreEqual(_a.Order, _b.Order, "the two derivations logged a different order");
            CollectionAssert.AreEqual(_a.Numbers.ToList(), _b.Numbers.ToList(), "the two derivations measured different numbers");
        }

        [Test]
        public void EachDerivation_StandsOnItsGroundFilesImport_AndTheTwoImportsAreIdentical()
        {
            Assert.IsNotNull(_plan.Ground, "the plan names no ground file");
            Assert.IsNotNull(_importA, "the frozen sources were loaded without the ground file's import");
            Assert.IsNotNull(_importB, "the frozen sources were loaded without the ground file's import");
            Assert.AreEqual(_importA.CodesSha256, _importB.CodesSha256, "two imports of the same ground file hash differently");
            Assert.AreEqual(_importA.CodesSha256, TerrainPlanMaps.Sha256(_importA.Codes), "the import's hash is not its codes'");

            foreach (var (r, imp, which) in new[] { (_a, _importA, "the first"), (_b, _importB, "the second") })
            {
                Assert.IsNotNull(r.GroundCodes, which + " derivation's height map is not its import's codes");
                Assert.IsTrue(r.GroundCodes.SequenceEqual(imp.Codes), which + " derivation's height map is not its import's codes");
                Assert.IsNotNull(r.EDerived, which + " derivation dropped the plan's own ground, the check against the import");
                var ground = TerrainPlanMaps.GroundOf(imp, _plan.HeightRange.x, _plan.HeightRange.y);
                Assert.AreEqual(ground.Length, r.E.Length, which + " derivation's ground");
                int at = -1;
                for (int i = 0; i < ground.Length && at < 0; i++) if (!ground[i].Equals(r.E[i])) at = i;
                Assert.AreEqual(-1, at, at < 0 ? "" :
                    $"{which} derivation's ground is not its import's codes decoded: cell {at} reads {r.E[at]}, the decode {ground[at]}");
            }
        }

        [Test]
        public void TheCommittedMaps_EqualAFreshDerivation()
        {
            var diffs = TerrainPlanMapWriter.Differences(TerrainPlanMapWriter.ExpectedOf(_plan, _a));
            string manifest = File.Exists(StPetersTerrainPlan.ManifestPath) ? File.ReadAllText(StPetersTerrainPlan.ManifestPath) : null;
            string writtenOn = manifest == null ? "(no manifest)" : Sha256Of(manifest, "base");
            Assert.IsEmpty(diffs,
                $"the committed maps are not this derivation ({_a.Sha256()}); re-write them. Today's ground here is {_groundSha}, " +
                $"the maps were written on {writtenOn}.\n  " + string.Join("\n  ", diffs));
        }

        [Test]
        public void TheManifest_RecordsThisDerivation_ItsGround_AndEachMap()
        {
            Assert.IsTrue(File.Exists(StPetersTerrainPlan.ManifestPath), $"no manifest at {StPetersTerrainPlan.ManifestPath}");
            string text = File.ReadAllText(StPetersTerrainPlan.ManifestPath);
            Assert.AreEqual(_groundSha, Sha256Of(text, "base"),
                "today's ground, sampled here, is not the ground the maps were derived from: this platform's TidalTerrain " +
                "differs from the writer's, or the analytic terrain changed under the plan");
            Assert.AreEqual(StPetersTerrainPlan.FileSha256(StPetersTerrainPlan.SourcesPath), Sha256Of(text, "sources"),
                "the frozen sources changed after the maps were written");
            StringAssert.Contains("\"derivation\": \"" + _a.Sha256() + "\"", text, "the manifest records another derivation");

            var x = TerrainPlanMapWriter.ExpectedOf(_plan, _a);
            Assert.AreEqual(TerrainPlanMaps.Sha256(x.Height), TerrainPlanManifest.ValuesOf(text, TerrainPlanMapWriter.HeightRole), "height");
            Assert.AreEqual(TerrainPlanMaps.Sha256(x.Still), TerrainPlanManifest.ValuesOf(text, TerrainPlanMapWriter.StillRole), "still water");
            for (int m = 0; m < TerrainPlanMaps.SplatMaps; m++)
                Assert.AreEqual(TerrainPlanMaps.Sha256(x.Splat[m]), TerrainPlanManifest.ValuesOf(text, TerrainPlanMapWriter.SplatRole(m)),
                                TerrainPlanMapWriter.SplatRole(m));
        }

        [Test]
        public void TheFrozenSources_WriteBackToThemselves()
        {
            string text = File.ReadAllText(StPetersTerrainPlan.SourcesPath);
            var s = TerrainPlanSourcesJson.Read(text, out string planId);
            Assert.AreEqual(StPetersTerrainPlan.PlanId, planId, "the frozen sources are another plan's");
            string again = TerrainPlanSourcesJson.Write(s, planId);
            int at = 0;
            while (at < text.Length && at < again.Length && text[at] == again[at]) at++;
            Assert.AreEqual(text.Length, again.Length, $"the frozen sources are not canonical: they part at character {at}");
            Assert.AreEqual(text, again, $"the frozen sources are not canonical: they part at character {at}");
        }

        /// <summary>The <c>sha256</c> of the manifest's object <paramref name="key"/> (<c>"base": {"from": …, "sha256": …}</c>).</summary>
        private static string Sha256Of(string manifest, string key)
        {
            int at = manifest.IndexOf("\"" + key + "\": {", StringComparison.Ordinal);
            if (at < 0) return null;
            const string field = "\"sha256\": \"";
            int v = manifest.IndexOf(field, at, StringComparison.Ordinal);
            int close = manifest.IndexOf('}', at);
            if (v < 0 || v > close) return null;
            v += field.Length;
            return manifest.Substring(v, manifest.IndexOf('"', v) - v);
        }
    }
}
