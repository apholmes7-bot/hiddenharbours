using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The deck-loop kit's contract, re-derived from the live rigs on every run.
    ///
    /// <para><b>⭐ THE CONTRACT IS THE ORACLE AND THE RIG IS THE TRUTH.</b> Same standing rule as
    /// <see cref="IsoPackBakeTests"/> (#452): the committed
    /// <c>deckLoopKit.contract.json</c> is what a baker will assert against, and nothing keeps it
    /// honest except re-measuring every cell from the rig source each run. Everything here runs the
    /// V8 host CPU-side, which this suite has already established as safe on CI's null graphics
    /// device. No sheet is written — the geometry is what can be wrong, and geometry is measurable
    /// without touching the disk.</para>
    ///
    /// <para><b>The handedness test is the reason this file exists at all.</b> This kit turns the
    /// OPPOSITE way to every boat in the catalog, and the two are indistinguishable by the screen
    /// measure the iso-rig-pack contracts record (both read −46.75°/step). Only the ground-plane
    /// bearing, with the depth un-squashed by <c>1/sin(elev)</c>, separates them — so that is what
    /// <see cref="TheKitTurnsOppositeToTheFleet"/> measures, against this repo's own registered
    /// counter-clockwise rig rather than against a constant.</para>
    /// </summary>
    public class DeckLoopKitContractTests
    {
        const string ContractPath = "Assets/_Project/Art/Sprites/DeckGear/deckLoopKit.contract.json";

        /// <summary>Catalog key → the contract's family key. Order is the kit README's.</summary>
        static readonly (string catalog, string family)[] Families =
        {
            ("deckGear", "deckGear"), ("trap", "trap"), ("fishTray2", "tray"),
            ("buoyIso", "buoy"), ("trapFauna", "trapFauna"),
        };

        static IRigScriptHost _host;
        static Contract _contract;

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        [OneTimeSetUp]
        public void InstallTheKitOnce()
        {
            _host = RigScriptHostFactory.Create();
            // isoSolid is every rig's declared prerequisite, so installing the five families
            // installs it exactly once. InstallModule for the turntable: it exposes no W/H/pivot
            // triple and Install would throw on the missing pivot.
            RigCatalog.InstallModule(_host, RigCatalog.Get("deckIsoSolid"));
            foreach (var (catalog, _) in Families)
                RigCatalog.InstallModule(_host, RigCatalog.Get(catalog));

            _contract = Contract.Load(Path.Combine(RepoRoot, ContractPath));
        }

        [OneTimeTearDown]
        public void Dispose() => _host?.Dispose();

        // ------------------------------------------------------------------ handedness

        /// <summary>
        /// Measured against <c>utility-iso</c>, which this repo's own probe registered as
        /// CounterClockwise — not against a hard-coded ±45. If the reference is ever re-measured the
        /// comparison moves with it, which is the point: this asserts the two families are
        /// OPPOSITE, and that claim survives either one being re-registered.
        /// </summary>
        [Test]
        public void TheKitTurnsOppositeToTheFleet()
        {
            double elev = _host.EvaluateNumber("IsoSolid.DEFAULT_ELEV");
            double se = Math.Sin(elev * Math.PI / 180.0);
            double kit = Norm180(KitBearing(1, se) - KitBearing(0, se));

            using (var refHost = RigScriptHostFactory.Create())
            {
                RigCatalog.InstallModule(refHost, RigCatalog.Get("utilityIso"));
                double reference = UtilityGroundStepDegrees(refHost);

                Assert.Greater(reference, 0,
                    "the registered-CounterClockwise reference should step +45°/dir in the ground plane");
                Assert.Less(kit, 0,
                    "the deck-loop kit should step −45°/dir — it is Clockwise, as its drop README says");
                Assert.AreNotEqual(Math.Sign(reference), Math.Sign(kit),
                    "the kit and the fleet must measure OPPOSITE handedness. If this ever agrees, a " +
                    "baker is about to mirror all eight cells of every piece in the kit.");
            }

            Assert.AreEqual("clockwise", _contract.AzimuthConvention,
                "the committed contract must record what was just measured");
        }

        /// <summary>The facings are exactly 45° apart in the ground plane — the step magnitude is a
        /// separate claim from its sign, and a baker that used 46.75 as spacing would be wrong.</summary>
        [Test]
        public void TheFacingsAreExactly45DegreesApart()
        {
            double elev = _host.EvaluateNumber("IsoSolid.DEFAULT_ELEV");
            double se = Math.Sin(elev * Math.PI / 180.0);
            var bearings = new double[8];
            for (int d = 0; d < 8; d++) bearings[d] = KitBearing(d, se);

            for (int i = 1; i < 8; i++)
            {
                double step = Norm180(bearings[i] - bearings[i - 1]);
                Assert.AreEqual(-45.0, step, 1e-6,
                    $"dir {i - 1}→{i} stepped {step:F6}° in the ground plane, not −45");
            }
        }

        /// <summary>The depth squash is per family and measured — carrying one across families is the
        /// un-squash mistake this repo keeps making (shoreFinds uses 0.72, not sin 40°).</summary>
        [Test]
        public void TheDepthSquashIsSin40AndTheContractSaysSo()
        {
            double elev = _host.EvaluateNumber("IsoSolid.DEFAULT_ELEV");
            Assert.AreEqual(40.0, elev, 1e-9, "the kit's camera elevation");
            Assert.AreEqual(Math.Sin(40.0 * Math.PI / 180.0), _contract.DepthScale, 1e-12,
                "contract depthScale must be sin(elev), measured from the rig");
        }

        // ------------------------------------------------------------------ the cells

        /// <summary>
        /// Every committed cell re-derived from the live rig, by the recovered rule: pivot-INCLUSIVE
        /// union of the INK bbox across all facings on the fixed native buffer, seeded at the pivot.
        ///
        /// <para><b>Facings come from the CONTRACT, never from <c>nativeDirs</c>.</b> #452's finding,
        /// and it reproduces here: none of these rigs exposes a numeric <c>DIRS</c>, so
        /// <c>Install</c> reports 0 facings for all five. A baker that believed it would bake
        /// nothing at all.</para>
        /// </summary>
        [Test]
        public void EveryCellReproducesFromTheLiveRig()
        {
            int checkedCells = 0;
            var failures = new List<string>();

            foreach (var (catalogKey, familyKey) in Families)
            {
                var fam = _contract.Family(familyKey);
                string g = RigCatalog.Get(catalogKey).GlobalName;
                int W = (int)_host.EvaluateNumber($"{g}.W"), H = (int)_host.EvaluateNumber($"{g}.H");
                int px = (int)_host.EvaluateNumber($"{g}.pivot.x"), py = (int)_host.EvaluateNumber($"{g}.pivot.y");

                Assert.AreEqual(fam.NativeW, W, $"{familyKey}: native buffer width");
                Assert.AreEqual(fam.NativeH, H, $"{familyKey}: native buffer height");
                Assert.AreEqual(fam.NativePivotX, px, $"{familyKey}: native pivot x");
                Assert.AreEqual(fam.NativePivotY, py, $"{familyKey}: native pivot y");

                foreach (var cell in fam.Cells)
                {
                    // Seed the union AT the pivot so a piece whose ink does not span its own pivot
                    // still contains it — the wharfDecor/utilityIso rule, recovered by measurement.
                    int x0 = px, y0 = py, x1 = px, y1 = py;
                    for (int d = 0; d < fam.Facings; d++)
                    {
                        byte[] rgba = _host.EvaluateBytes(RenderExpr(familyKey, g, cell.Key, d));
                        Assert.AreEqual(W * H * 4, rgba.Length,
                            $"{familyKey}.{cell.Key} dir {d}: buffer is not the declared {W}×{H}");
                        if (!InkBox(rgba, W, H, out int bx0, out int by0, out int bx1, out int by1)) continue;
                        x0 = Math.Min(x0, bx0); y0 = Math.Min(y0, by0);
                        x1 = Math.Max(x1, bx1); y1 = Math.Max(y1, by1);
                    }

                    Check(failures, $"{familyKey}.{cell.Key}.cellW", cell.CellW, x1 - x0 + 1);
                    Check(failures, $"{familyKey}.{cell.Key}.cellH", cell.CellH, y1 - y0 + 1);
                    Check(failures, $"{familyKey}.{cell.Key}.pivotX", cell.PivotX, px - x0);
                    Check(failures, $"{familyKey}.{cell.Key}.pivotY", cell.PivotY, py - y0);
                    checkedCells++;
                }
            }

            Assert.IsEmpty(failures,
                $"{failures.Count} of {checkedCells} committed cells no longer reproduce from the " +
                $"live rigs:\n  {string.Join("\n  ", failures)}");
            Assert.AreEqual(24, checkedCells, "the kit's cell count moved — re-emit the contract");
        }

        /// <summary>
        /// <c>Install</c> reports 0 facings for every family here, so the contract is the only source
        /// of the facing count. Asserted rather than commented, because the failure mode is a baker
        /// that silently produces an empty sheet.
        /// </summary>
        [Test]
        public void NativeDirsIsUselessHereAndTheContractIsNot()
        {
            foreach (var (catalogKey, familyKey) in Families)
            {
                string g = RigCatalog.Get(catalogKey).GlobalName;
                Assert.IsFalse(_host.EvaluateBool($"typeof {g}.DIRS === 'number'"),
                    $"{familyKey} started declaring a numeric DIRS — re-check whether the contract's " +
                    "facing count is still the authority before relying on this.");
                Assert.Greater(_contract.Family(familyKey).Facings, 0,
                    $"{familyKey}: the contract must carry the facing count");
            }
        }

        /// <summary>
        /// <c>TrapFauna.render(kind, opts)</c> takes no <c>dir</c> — what a pot comes up holding is
        /// not directional, and the contract records that as <c>facings: 1</c> rather than omitting
        /// the axis. An absent section is data (the sidecar contract's rule), not a gap to fill.
        /// </summary>
        [Test]
        public void TheCatchIsNotDirectionalAndTheContractSaysSoExplicitly()
        {
            Assert.AreEqual(1, _contract.Family("trapFauna").Facings);
            Assert.AreEqual(1, _host.EvaluateNumber("TrapFauna.render.length - 1"),
                "TrapFauna.render should be (kind, opts) — a dir parameter would change the contract");
            foreach (var (_, familyKey) in Families.Where(f => f.family != "trapFauna"))
                Assert.AreEqual(8, _contract.Family(familyKey).Facings, $"{familyKey} is 8-way");
        }

        /// <summary>
        /// Per-hull trap capacity lives in the RIG, not in C#. CLAUDE.md rule 2 — content is data —
        /// and the deck loop reads these rather than carrying its own constants.
        /// </summary>
        [Test]
        public void PerHullStackLimitsComeFromTheRig()
        {
            foreach (var (hull, deck, washboard) in new[]
                     { ("lobsterBoat", 5, 2), ("capeIslander", 3, 2), ("wharf", 6, 0) })
            {
                Assert.IsTrue(_host.EvaluateBool($"typeof TrapIso.CAPS['{hull}'] === 'object'"),
                    $"TrapIso.CAPS is missing '{hull}'");
                Assert.AreEqual(deck, (int)_host.EvaluateNumber($"TrapIso.CAPS['{hull}'].deck"),
                    $"{hull} deck capacity");
                Assert.AreEqual(washboard, (int)_host.EvaluateNumber($"TrapIso.CAPS['{hull}'].washboard"),
                    $"{hull} washboard capacity");
            }
        }

        /// <summary>
        /// <c>DeckGear.station()</c> is the crew contract, and its work height is a WORLD METRE, never
        /// a body fraction — a bench belongs to the deck, not to whoever is standing at it. The hauler
        /// is the one station whose operator faces outboard.
        /// </summary>
        [Test]
        public void EveryStationSuppliesAWorldMetreWorkHeight()
        {
            foreach (string kind in new[] { "baitbin", "baitbox", "chopboard", "bandingtable", "haulerstation" })
            {
                Assert.IsTrue(_host.EvaluateBool($"DeckGear.station('{kind}',0,{{}}) !== null"),
                    $"{kind} has no station");
                double workZ = _host.EvaluateNumber($"DeckGear.station('{kind}',0,{{}}).workZ");
                Assert.Greater(workZ, 0.0, $"{kind}: workZ must be a positive world metre");
                Assert.Less(workZ, 2.0, $"{kind}: workZ of {workZ} m is not a deck surface height");
            }

            Assert.AreEqual(4, (int)_host.EvaluateNumber("DeckGear.station('haulerstation',0,{}).turn"),
                "the hauler operator faces OUTBOARD — turn 4 against the gear's own dir");
            Assert.AreEqual("hauler", _host.EvaluateString("DeckGear.station('haulerstation',0,{}).anim"),
                "the hauler station drives the `hauler` clip");
        }

        // ------------------------------------------------------------------ helpers

        static string RenderExpr(string family, string g, string key, int dir) => family switch
        {
            "tray"      => $"{g}.render({dir},{{}})",
            "trapFauna" => $"{g}.render('{key}',{{}})",
            _           => $"{g}.render('{key}',{dir},{{}})",
        };

        static void Check(ICollection<string> into, string what, int want, int got)
        {
            if (want != got) into.Add($"{what}: contract {want}, measured {got}");
        }

        static bool InkBox(byte[] rgba, int W, int H, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = int.MaxValue; y0 = int.MaxValue; x1 = int.MinValue; y1 = int.MinValue;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (rgba[(y * W + x) * 4 + 3] > 0)
                    {
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
            return x1 >= x0;
        }

        static double Norm180(double d)
        {
            while (d > 180.0) d -= 360.0;
            while (d <= -180.0) d += 360.0;
            return d;
        }

        /// <summary>Ground-plane bearing of the kit's +X axis at <paramref name="dir"/>, depth
        /// un-squashed. Screen-space would answer the wrong question — see the class remarks.</summary>
        double KitBearing(int dir, double sinElev)
        {
            string basis = $"IsoSolid.camBasis({{dir:{dir},elev:IsoSolid.DEFAULT_ELEV}})";
            double ox = _host.EvaluateNumber($"IsoSolid.proj(0,0,0,{basis},0,0).sx");
            double oy = _host.EvaluateNumber($"IsoSolid.proj(0,0,0,{basis},0,0).sy");
            double px = _host.EvaluateNumber($"IsoSolid.proj(1,0,0,{basis},0,0).sx");
            double py = _host.EvaluateNumber($"IsoSolid.proj(1,0,0,{basis},0,0).sy");
            return Math.Atan2(-(py - oy) / sinElev, px - ox) * 180.0 / Math.PI;
        }

        static double UtilityGroundStepDegrees(IRigScriptHost host)
        {
            double elev = host.EvaluateNumber("UtilityIso.defaultElev");
            double se = Math.Sin(elev * Math.PI / 180.0);
            string E = elev.ToString("R", CultureInfo.InvariantCulture);
            double B(int dir)
            {
                double ox = host.EvaluateNumber($"UtilityIso.project({dir},[0,0,0],{E}).x");
                double oy = host.EvaluateNumber($"UtilityIso.project({dir},[0,0,0],{E}).y");
                double px = host.EvaluateNumber($"UtilityIso.project({dir},[1,0,0],{E}).x");
                double py = host.EvaluateNumber($"UtilityIso.project({dir},[1,0,0],{E}).y");
                return Math.Atan2(-(py - oy) / se, px - ox) * 180.0 / Math.PI;
            }
            return Norm180(B(1) - B(0));
        }

        // ------------------------------------------------------------------ contract model

        sealed class Contract
        {
            public string AzimuthConvention;
            public double DepthScale;
            readonly Dictionary<string, Fam> _families = new Dictionary<string, Fam>(StringComparer.Ordinal);

            public Fam Family(string key) =>
                _families.TryGetValue(key, out var f)
                    ? f
                    : throw new AssertionException(
                        $"contract has no family '{key}'. Known: {string.Join(", ", _families.Keys)}");

            public static Contract Load(string path)
            {
                Assert.IsTrue(File.Exists(path),
                    $"the deck-loop contract is missing at {path} — re-emit it with " +
                    "`node docs/art/rigs/deck-loop-kit/_verify.js --emit`");
                var dto = JsonUtility.FromJson<Dto>(File.ReadAllText(path));
                var c = new Contract
                {
                    AzimuthConvention = dto.azimuth.convention,
                    DepthScale = dto.projection.depthScale,
                };
                foreach (var f in dto.families) c._families[f.key] = f;
                return c;
            }

            [Serializable] sealed class Dto
            {
                public Projection projection;
                public Azimuth azimuth;
                public Fam[] families;
            }
            [Serializable] sealed class Projection { public double depthScale; }
            [Serializable] sealed class Azimuth { public string convention; }
        }

        [Serializable]
        internal sealed class Fam
        {
            public string key;
            public int facings, nativeW, nativeH, nativePivotX, nativePivotY;
            public CellDto[] cells;

            public int Facings => facings;
            public int NativeW => nativeW;
            public int NativeH => nativeH;
            public int NativePivotX => nativePivotX;
            public int NativePivotY => nativePivotY;
            public IEnumerable<CellDto> Cells => cells;
        }

        [Serializable]
        internal sealed class CellDto
        {
            public string key;
            public int cellW, cellH, pivotX, pivotY;

            public string Key => key;
            public int CellW => cellW;
            public int CellH => cellH;
            public int PivotX => pivotX;
            public int PivotY => pivotY;
        }
    }
}
