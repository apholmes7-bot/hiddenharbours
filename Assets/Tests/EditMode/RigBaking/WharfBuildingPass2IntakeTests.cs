using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The wharf buildings pass 2 intake (drop 13, in <c>docs/art/rigs/wharf-building-kit-v2/</c>: all of it
    /// but the one byte-identical library that already has a home on main, see <see cref="LandedElsewhere"/>).
    /// Pass 2 is a NEW catalog key beside pass 1, and nothing bakes from it yet: pass 1 still bakes the
    /// four placed sheets until the owner rules a re-bake. So this fixture pins two things at once —
    /// that pass 2 arrives as delivered and loads the way its README says, and that its arrival moves
    /// none of the pixels the game draws today.
    /// </summary>
    public class WharfBuildingPass2IntakeTests
    {
        const string Kit = "docs/art/rigs/wharf-building-kit-v2/";
        const string Global = "WharfBuilding2";

        static string Root => RigCatalog.RepoRoot;
        static string Full(string relative) => Path.Combine(Root, relative);
        // Checkout-agnostic text: the kit is pinned LF, main's libraries may not be on every machine.
        static string Lf(string relative) => File.ReadAllText(Full(relative)).Replace("\r\n", "\n");
        static string Quote(string value) => "\"" + value.Replace("\\", "\\\\")
            .Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")
            .Replace("\t", "\\t") + "\"";
        static string Sha(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        static string Preset(string name) => $"Object.assign({{}},{Global}.PRESETS[{Quote(name)}])";
        static string InState(string name, string key, string valueJs) =>
            $"Object.assign({Preset(name)},{{{key}:{valueJs}}})";
        static byte[] Render(IRigScriptHost host, int dir, string optsJs) =>
            host.EvaluateBytes($"{Global}.render({dir},{optsJs})");

        /// <summary>The catalog's chain with the lifecycle pass left out: the hook OFF.</summary>
        static IRigScriptHost HookOffHost()
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            try
            {
                RigCatalog.InstallModule(host, RigCatalog.Get("coastalPass"));
                RigCatalog.InstallModule(host, RigCatalog.Get("wharfBuilding2Geometry"));
                host.Execute(RigCatalog.ReadSource(RigCatalog.Get("wharfBuilding2")));
                Assert.That(host.EvaluateBool($"typeof BuildingLifecycle === 'undefined' && typeof {Global} === 'object'"),
                            Is.True, "the hook-off host must carry the rig and NOT the pass");
                return host;
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        // ---- the delivery ----------------------------------------------------------------------

        /// <summary>The kit's one file that lands at main's copy instead of in the kit: the prop rig has one
        /// home (MultiunitKitIntakeTests.OneInteriorPropRig), as drop 14's houses kit found, and drop 13's
        /// copy is byte-identical to it. Its manifest line is checked against that home.</summary>
        static readonly Dictionary<string, string> LandedElsewhere = new Dictionary<string, string>
        {
            ["lib/interiorPropRig.js"] = "docs/art/rigs/interiorPropRig.js",
        };

        [Test]
        public void DeliveredKitMatchesPinnedManifestAndLfBytes()
        {
            Assert.That(Sha(File.ReadAllBytes(Full(Kit + "SHA256SUMS.txt"))),
                Is.EqualTo("e2b555946ae3a985a3d756ce530ab14479480cb9530219f3d14148645034490d"));
            var lines = Lf(Kit + "SHA256SUMS.txt").Split('\n').Where(l => l.Length > 0).ToArray();
            Assert.That(lines.Length, Is.EqualTo(28));
            foreach (string line in lines)
            {
                string file = line.Substring(66);
                if (LandedElsewhere.TryGetValue(file, out string home))
                {
                    Assert.That(File.Exists(Full(Kit + file)), Is.False, $"{file}: a second copy of {home}");
                    // main's copy is not pinned LF on every checkout, so fold it as the manifest's LF bytes
                    Assert.That(Sha(Encoding.UTF8.GetBytes(Lf(home))), Is.EqualTo(line.Substring(0, 64)),
                                $"{file} is not {home}");
                    continue;
                }
                byte[] bytes = File.ReadAllBytes(Full(Kit + file));
                Assert.That(Sha(bytes), Is.EqualTo(line.Substring(0, 64)), file);
                if (!file.EndsWith(".png", StringComparison.Ordinal))
                    Assert.That(Encoding.UTF8.GetString(bytes), Does.Not.Contain("\r"), file);
            }
            Assert.That(Directory.GetFiles(Full(Kit), "*", SearchOption.AllDirectories).Length,
                Is.EqualTo(28), "Do not insert generated data into the immutable delivery.");
        }

        // ---- the catalog -----------------------------------------------------------------------

        [Test]
        public void TheCatalogLoadsTheReadmeChain_FromMainsLibraries()
        {
            var expected = new[]
            {
                "docs/art/rigs/interiorPropRig.js",
                "docs/art/rigs/village-return/houses-kit/Art/coastalPass.js",
                "docs/art/rigs/building-lifecycle-kit/buildingLifecycleRig.js",
                Kit + "wharfBuildingRig2.geo.js",
                Kit + "wharfBuildingRig2.js",
            };
            var sources = new List<string>();
            var visited = new HashSet<string>();
            void Walk(string key)
            {
                if (!visited.Add(key)) return;
                var entry = RigCatalog.Get(key);
                foreach (string prerequisite in entry.Prerequisites) Walk(prerequisite);
                sources.Add(entry.ScriptPath);
            }
            Walk("wharfBuilding2");
            CollectionAssert.AreEqual(expected, sources, "the README's load order, weatherSky left out");

            // The kit's lib/ copies are the viewer's. They must be main's files, so the viewer the
            // art director checks and the bake the game ships run one library.
            var copies = new (string Lib, string Main)[]
            {
                ("lib/coastalPass.js", "docs/art/rigs/village-return/houses-kit/Art/coastalPass.js"),
                ("lib/buildingLifecycleRig.js", "docs/art/rigs/building-lifecycle-kit/buildingLifecycleRig.js"),
                ("lib/weatherSky.js", "docs/art/rigs/terrain/pass9/lib/weatherSky.js"),
                ("lib/characterIsoRig9.js", "docs/art/rigs/character/rig9/Art/characterIsoRig9.js"),
                ("lib/characterIsoRig9.poses.js", "docs/art/rigs/character/rig9/Art/characterIsoRig9.poses.js"),
                ("support.js", "docs/art/rigs/character/rig9/support.js"),
            };
            foreach ((string lib, string main) in copies)
                Assert.That(Lf(Kit + lib), Is.EqualTo(Lf(main)), $"{Kit}{lib} is not {main}");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigGeometry geo = RigCatalog.Install(host, RigCatalog.Get("wharfBuilding2"));
            Assert.That((geo.Width, geo.Height), Is.EqualTo((1200, 1160)), "pass 1's cell");
            Assert.That((geo.PivotX, geo.PivotY), Is.EqualTo((600.0, 780.0)), "pass 1's pivot");
            Assert.That(geo.NativeDirs, Is.EqualTo(8));
            Assert.That(geo.DefaultElevation, Is.EqualTo(40.0));
            Assert.That(host.EvaluateString($"Object.keys({Global}.PRESETS).join(',')"),
                Is.EqualTo("netShed,redShed,tealShack,gambrelBarn,iceHouse,fishPlant,cannery"));
            Assert.That(host.EvaluateBool("typeof WeatherSky === 'undefined' && typeof CharacterIso9 === 'undefined'"),
                Is.True, "the viewer's libraries stay out of the bake");
            Assert.That(host.EvaluateBool("typeof WharfBuilding === 'undefined'"), Is.True,
                "pass 1 is not loaded, so a pass-2 bake can never hand its render to pass 1");
        }

        [TestCase("netShed")]
        [TestCase("gambrelBarn")]
        [TestCase("cannery")]
        public void TheRigTurnsCounterClockwise_AsTheBuildingProbeReadsIt(string preset)
        {
            RigEntry entry = RigCatalog.Get("wharfBuilding2");
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigGeometry geo = RigCatalog.Install(host, entry);

            BuildingRigAzimuthProbe.Result probe = BuildingRigAzimuthProbe.Measure(
                host, Global, Preset(preset), geo.Width, geo.Height, geo.PivotX);
            Assert.AreEqual(AzimuthConvention.CounterClockwise, probe.Convention, probe.Report);
            Assert.AreEqual(entry.DeclaredConvention, probe.Convention, probe.Report);
        }

        // ---- the lifecycle ---------------------------------------------------------------------

        [Test]
        public void TheLifecycleReachesPassTwo_AndEveryStateDrawsItsOwnShell()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get("wharfBuilding2"));
            Assert.That(host.EvaluateBool("typeof BuildingLifecycle === 'object'"), Is.True,
                "the catalog's own prerequisite brings the pass");

            // The states come from the pass itself, never typed here.
            string[] phases = host.EvaluateString("BuildingLifecycle.PHASES.filter(p => p !== 'finished').join(',')").Split(',');
            string[] decays = host.EvaluateString("BuildingLifecycle.DECAY.filter(d => d !== 'sound').join(',')").Split(',');
            Assert.That(phases.Length, Is.EqualTo(6));
            Assert.That(decays.Length, Is.EqualTo(4));

            var states = phases.Select(p => ($"phase {p}", InState("netShed", "phase", Quote(p))))
                .Concat(decays.Select(d => ($"decay {d}", InState("netShed", "decay", Quote(d)))))
                .Append(("burnt", InState("netShed", "burnt", "true")));
            var seen = new Dictionary<string, string> { [Sha(Render(host, 2, Preset("netShed")))] = "sound" };
            var failures = new List<string>();
            foreach ((string label, string opts) in states)
            {
                string hash = Sha(Render(host, 2, opts));
                if (seen.TryGetValue(hash, out string same)) failures.Add($"{label} draws the same bytes as {same}");
                else seen[hash] = label;
            }
            Assert.That(failures, Is.Empty, "every lifecycle state must reach the pass-2 rig through its hook");
        }

        [TestCase("netShed")]
        [TestCase("gambrelBarn")]
        [TestCase("fishPlant")]
        public void WithTheHookOff_ASoundBuildDrawsTheSameBytes(string preset)
        {
            using IRigScriptHost withPass = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(withPass, RigCatalog.Get("wharfBuilding2"));
            using IRigScriptHost hookOff = HookOffHost();

            string sound = Sha(Render(hookOff, 2, Preset(preset)));
            Assert.That(Sha(Render(withPass, 2, Preset(preset))), Is.EqualTo(sound),
                $"{preset}: loading the pass changed a sound build");
            // And the pass is what draws a shell: with it absent, a state is ignored, not half-drawn.
            Assert.That(Sha(Render(hookOff, 2, InState(preset, "decay", "'ruin'"))), Is.EqualTo(sound),
                $"{preset}: the hook-off rig drew a ruin by itself");
        }

        // ---- the sidecars ----------------------------------------------------------------------

        [Test]
        public void EverySidecarParses_NamesADefinedPreset_AndIsTheLiveModelsBytes()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get("wharfBuilding2"));
            string[] presets = host.EvaluateString($"Object.keys({Global}.PRESETS).join(',')").Split(',');
            string[] files = Directory.GetFiles(Full(Kit + "gameplay"), "*.json").Select(Path.GetFileName)
                .OrderBy(f => f, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(
                presets.Select(p => $"wharfBuilding2.{p}.gameplay.json").OrderBy(f => f, StringComparer.Ordinal).ToArray(),
                files, "one sidecar per preset, and none for a preset the rig does not define");

            foreach (string p in presets)
            {
                string file = $"wharfBuilding2.{p}.gameplay.json";
                string text = Lf(Kit + "gameplay/" + file);
                host.Execute("var gp = JSON.parse(" + Quote(text) + ");");
                Assert.That(host.EvaluateString("gp.schema"), Is.EqualTo("hidden-harbours/wharf-building-gameplay@1"), file);
                Assert.That(host.EvaluateString("gp.exportSymbol"), Is.EqualTo(Global), file);
                Assert.That(host.EvaluateString("gp.build.preset"), Is.EqualTo(p), file);
                Assert.That(host.EvaluateString("gp.CHAR.rig"), Is.EqualTo("characterIsoRig9.js"), file);
                Assert.That(
                    host.EvaluateString($"JSON.stringify({Global}.gameplay(Object.assign({Preset(p)},{{preset:{Quote(p)}}})),null,1)"),
                    Is.EqualTo(text), $"{file} is not what the live model serialises");
            }
        }

        // ---- pass 1: the game's pixels ---------------------------------------------------------

        [Test]
        public void PassOnesFourPlacedSheets_DrawTheirCommittedPixels_WithPassTwoLoaded()
        {
            VillageBuildingKit.Contract contract = VillageBuildingKit.Load();
            Assert.IsNotNull(contract, VillageBuildingKit.ContractPath);
            VillageBuildingKit.Entry[] placed = contract.buildings.Where(e => e.rigGlobal == "WharfBuilding")
                .OrderBy(e => e.key, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(new[] { "ginnyLeanTo", "ginnyNetStore", "ginnyWoodshed", "stPetersCannery" },
                placed.Select(e => e.key).ToArray(), "the sheets pass 1 bakes today");

            // Pass 2's chain FIRST, then pass 1 over it: pass 1 then runs in a host that already holds
            // every library pass 2 brought, which is the harder of the two orders.
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get("wharfBuilding2"));
            RigGeometry geo = RigCatalog.Install(host, RigCatalog.Get("wharfBuilding"));
            Assert.That(host.EvaluateBool($"typeof CoastalPass === 'object' && typeof {Global} === 'object'"), Is.True);

            var failures = new List<string>();
            foreach (VillageBuildingKit.Entry e in placed)
            {
                byte[] png = File.ReadAllBytes(Full(VillageBuildingKit.BuildingsRoot + e.sheet));
                Assert.That(Encoding.ASCII.GetString(png, 0, Math.Min(png.Length, 24)),
                    Does.Not.StartWith("version https://git-lfs"), $"{e.sheet} is an LFS pointer, not a sheet");
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                try
                {
                    Assert.That(texture.LoadImage(png), Is.True, e.sheet);
                    Assert.That((texture.width, texture.height), Is.EqualTo((e.sheetW, e.sheetH)), e.sheet);
                    Color32[] sheet = texture.GetPixels32();
                    var convention = (AzimuthConvention)Enum.Parse(typeof(AzimuthConvention), e.convention);
                    int mismatches = 0, covered = 0, outside = 0;
                    for (int cell = 0; cell < e.facings; cell++)
                    {
                        string dir = RigBaker.DirForCell(cell, e.facings, convention).ToString("R", CultureInfo.InvariantCulture);
                        byte[] rgba = host.EvaluateBytes($"WharfBuilding.render({dir},{e.optionsJs})");
                        Assert.That(rgba.Length, Is.EqualTo(geo.Width * geo.Height * 4), $"{e.key} cell {cell}");
                        int col = cell % e.cols, row = cell / e.cols;
                        for (int y = 0; y < geo.Height; y++)
                            for (int x = 0; x < geo.Width; x++)
                            {
                                int i = (y * geo.Width + x) * 4, cx = x - e.cropX, cy = y - e.cropY;
                                if (cx < 0 || cy < 0 || cx >= e.cellW || cy >= e.cellH)
                                {
                                    if (rgba[i + 3] != 0) outside++;
                                    continue;
                                }
                                if (rgba[i + 3] != 0) covered++;
                                Color32 s = sheet[(e.sheetH - 1 - (row * e.cellH + cy)) * e.sheetW + col * e.cellW + cx];
                                if (rgba[i] != s.r || rgba[i + 1] != s.g || rgba[i + 2] != s.b || rgba[i + 3] != s.a) mismatches++;
                            }
                    }
                    if (covered < 1000 || mismatches != 0 || outside != 0)
                        failures.Add($"{e.key}: {mismatches} px differ, {outside} drawn px outside its crop, {covered} covered");
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            Assert.That(failures, Is.Empty, "pass 1 no longer draws the sheets the game places");
        }
    }
}
