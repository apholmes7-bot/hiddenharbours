using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The SHIPYARD iso kit's import, proven against the live rig and without a single committed
    /// sheet — the same discipline as <see cref="IsoPackBakeTests"/>, which this file deliberately
    /// mirrors rather than extends (that fixture pins its own four families in a hardcoded array, and
    /// widening it would re-run four families' worth of work to check a fifth).
    ///
    /// <para><b>⭐ THE CONTRACT IS THE ORACLE AND THE RIG IS THE TRUTH.</b> The contract's generator
    /// was not committed — as with the pack — so the only thing keeping it honest is re-deriving all
    /// 25 cells from the live rig on every run. That is
    /// <see cref="EveryCellReproducesFromTheLiveRig"/>.</para>
    ///
    /// <para><b>The one measurement everything else rests on</b> is the azimuth. This rig carries a
    /// POSITIVE <c>th = dir*PI/4</c> term, and the rig-lane README rules that sign inadmissible as
    /// evidence. What settles it is calibration:
    /// <see cref="TheAzimuthIsCalibratedAgainstAnAlreadyMeasuredSibling"/> asserts that
    /// <c>ShipyardIso.project()</c> and <c>WharfIso.project()</c> agree at every dir — wharfIso being
    /// already measured and registered CounterClockwise. Registering this family clockwise off the
    /// sign would have mirrored all 25 keys at once, silently.</para>
    /// </summary>
    public class ShipyardIsoBakeTests
    {
        const string Key = "shipyardIso";
        const string G = "ShipyardIso";
        const string Scratch = "__shipyardTestScratch";

        /// <summary>The rig's own scale ladder, as its README and <c>fitScale()</c> use it.</summary>
        static readonly int[] Ladder = { 32, 24, 16, 12, 8 };

        static IRigScriptHost _host;
        static IsoPackContract _contract;
        static Dictionary<string, int> _scales;

        [OneTimeSetUp]
        public void InstallOnce()
        {
            _contract = IsoPackContract.Load(Key);
            _scales = LoadScales(_contract.SourcePath);

            _host = RigScriptHostFactory.Create();
            // No pivot global (tight cells, px/py per bake) — Install would throw, InstallModule is right.
            RigCatalog.InstallModule(_host, RigCatalog.Get(Key));
            // The calibration sibling. The two rigs declare disjoint globals, so they cohabit.
            RigCatalog.InstallModule(_host, RigCatalog.Get("wharfIso"));
        }

        [OneTimeTearDown]
        public void DisposeHost() => _host?.Dispose();

        // =====================================================================================
        // 1. the contract is internally sound
        // =====================================================================================

        [Test]
        public void TheContractIsSelfConsistent()
        {
            Assert.DoesNotThrow(() => _contract.AssertSelfConsistent(), "shipyardIso contract");
            Debug.Log($"[shipyard] {_contract}");
        }

        [Test]
        public void EveryCellCarriesAScaleAndAGridThatHoldsAllEightFacings()
        {
            foreach (var c in _contract.Cells)
            {
                Assert.IsTrue(_scales.ContainsKey(c.key), $"{c.key}: contract records no pxPerM.");
                Assert.Contains(_scales[c.key], Ladder, $"{c.key}: pxPerM {_scales[c.key]} is off the rig's ladder.");

                // A 0×0 "plan" would satisfy the pack's arithmetic and cap checks while planning
                // nothing, so the count is asserted here rather than trusted.
                Assert.GreaterOrEqual(c.sheet.cols * c.sheet.rows, _contract.Facings,
                    $"{c.key}: a {c.sheet.cols}×{c.sheet.rows} grid cannot hold {_contract.Facings} facings.");
            }
        }

        // =====================================================================================
        // 2. the traps: facings, azimuth, keys
        // =====================================================================================

        [Test]
        public void FacingsComeFromTheContractNotNativeDirs()
        {
            Assert.AreEqual(8, _contract.Facings, "contract facings");

            // This rig DOES happen to export DIRS = 8, so the two agree here — but the assertion is
            // deliberately one-directional. Three of the four pack rigs report 0 native facings and
            // report it silently (0 legitimately means "the rig does not say"), so a cross-check that
            // sourced facings from nativeDirs would pass here and rot the moment the shape changes.
            Assert.AreEqual(_contract.Facings, RenderKeyFacingCount("smallYard"),
                "the number of DISTINCT rendered facings disagrees with the contract");
        }

        [Test]
        public void TheAzimuthIsCalibratedAgainstAnAlreadyMeasuredSibling()
        {
            Assert.AreEqual(AzimuthConvention.CounterClockwise,
                RigCatalog.Get(Key).DeclaredConvention,
                "shipyardIso must be registered CounterClockwise");

            // The evidence: identical projection at every dir as a rig already measured CCW.
            for (int dir = 0; dir < 8; dir++)
            {
                double sx = Num($"{G}.project({dir},[1,0,0],40,32).x - {G}.project({dir},[0,0,0],40,32).x");
                double sy = Num($"{G}.project({dir},[1,0,0],40,32).y - {G}.project({dir},[0,0,0],40,32).y");
                double wx = Num($"WharfIso.project({dir},[1,0,0],40,32).x - WharfIso.project({dir},[0,0,0],40,32).x");
                double wy = Num($"WharfIso.project({dir},[1,0,0],40,32).y - WharfIso.project({dir},[0,0,0],40,32).y");

                Assert.AreEqual(wx, sx, 1e-9, $"dir {dir}: +X screen dx differs from wharfIso's");
                Assert.AreEqual(wy, sy, 1e-9, $"dir {dir}: +X screen dy differs from wharfIso's");
            }
        }

        [Test]
        public void EveryContractKeyResolvesOnTheRig()
        {
            // The guard that matters most on an import: an unknown key does NOT throw in this rig —
            // it resolves to something else and renders the WRONG THING, and only a cell-size
            // mismatch downstream would ever expose it.
            foreach (var c in _contract.Cells)
                Assert.IsTrue(_host.EvaluateBool($"!!{G}.resolve({Q(c.key)})"),
                    $"{c.key} does not resolve on the rig.");

            int rigKeys = (int)Num($"Object.keys({G}.SITES).length + Object.keys({G}.PARTS).length");
            Assert.AreEqual(rigKeys, _contract.Count,
                "the rig carries a different number of keys than the contract measures");
        }

        // =====================================================================================
        // 3. THE BIG ONE — all 25 cells re-derived from the live rig
        // =====================================================================================

        [Test]
        public void EveryCellReproducesFromTheLiveRig()
        {
            var failures = new List<string>();

            foreach (var cell in _contract.Cells)
            {
                var facings = RenderFacings(cell.key, _scales[cell.key]);

                // The RULE has one implementation and this test calls it: the pivot-aligned union of
                // the returned BUFFER extents. Measuring the ink bbox instead disagrees on every key.
                WharfIsoSheetBaker.MeasureCell(facings, out int w, out int h, out int px, out int py);

                if (w != cell.cellW || h != cell.cellH || px != cell.pivotX || py != cell.pivotY)
                    failures.Add($"{cell.key}: rig gives {w}×{h} pivot ({px},{py}), " +
                                 $"contract says {cell.cellW}×{cell.cellH} pivot ({cell.pivotX},{cell.pivotY})");
            }

            Assert.IsEmpty(failures,
                $"{failures.Count} of {_contract.Count} cells no longer reproduce from the rig:\n  " +
                string.Join("\n  ", failures) +
                "\n\nThe rig is the truth. Re-measure and regenerate the contract in the same commit.");

            Debug.Log($"[shipyard] all {_contract.Count} cells reproduced from the live rig.");
        }

        [Test]
        public void TheRenderIsDeterministic()
        {
            // Sites carry a deterministic scatter seed and a continuous tide; neither may leak
            // real randomness, or every re-bake would produce a different sheet.
            foreach (string key in new[] { "smallYard", "cradle" })
            {
                string a = Hash($"{G}.render({Q(key)},2,{{}}).data");
                string b = Hash($"{G}.render({Q(key)},2,{{}}).data");
                Assert.AreEqual(a, b, $"{key} rendered twice and did not match itself");
            }
        }

        // =====================================================================================
        // 4. the keyline — a MECHANICAL bake blocker, recorded so it cannot be forgotten
        // =====================================================================================

        [Test]
        public void TheKeylineConstantAgreesWithTheContract()
        {
            Assert.IsTrue(_host.EvaluateBool($"typeof {G}.KEY === 'string'"),
                "the rig does not export its keyline as `KEY`");
            Assert.AreEqual(_contract.Keyline, _host.EvaluateString($"{G}.KEY"),
                "the contract's keyline and the rig's KEY disagree");
        }

        /// <summary>
        /// ⚠️ <b>This kit CANNOT BE BAKED yet, and that is not a bug in this test.</b>
        /// <see cref="IsoPackContract.AssertKeylineGated"/> mechanically refuses to bake any rig that
        /// exposes no <c>KEYLINE_DEFAULT</c> gate, per the owner's 2026-08-06 ruling. The four pack
        /// rigs have since gained the gate; this family arrived AFTER the ruling without it and bakes
        /// the 1 px ring unconditionally — there is no <c>{outline:false}</c> anywhere in its source.
        ///
        /// <para>The fix is upstream: <c>docs/art/rigs/**</c> is the art director's lane and the rig
        /// must not be edited here to get past the gate. This test pins the CURRENT state so the
        /// blocker is visible in CI rather than discovered at bake time — and it FAILS LOUDLY when the
        /// gated rig lands, which is the prompt to regenerate this contract with
        /// <c>keylineDefault: false</c> in the same commit.</para>
        /// </summary>
        [Test]
        public void TheKeylineGateIsStillOutstandingSoTheBakeWouldRefuse()
        {
            bool gated = _host.EvaluateBool($"typeof {G}.KEYLINE_DEFAULT !== 'undefined'");

            Assert.IsFalse(gated,
                "shipyardIsoRig NOW exposes KEYLINE_DEFAULT — the upstream ADR 0031 gate has landed. " +
                "That is good news, and this test is the reminder attached to it: regenerate " +
                "Assets/_Project/Art/Sprites/Shipyard/Iso/shipyardIsoRig.contract.json against the " +
                "gated rig (keylineDefault must become false, and any cell whose geometry the ring pass " +
                "moved must be re-measured), update the keylineNote in the kit catalogue and gap G3 in " +
                "VERIFICATION.md, then invert this assertion.");

            Assert.IsTrue(_contract.KeylineDefault,
                "the contract must record keylineDefault: true while the rig bakes the ring ungated — " +
                "it describes what the rig DOES, not what canon wants.");

            Assert.Throws<InvalidOperationException>(
                () => _contract.AssertKeylineGated(_host, G, Key),
                "the shared keyline gate must refuse this rig while it carries no KEYLINE_DEFAULT");
        }

        // =====================================================================================
        // 5. the authored sidecar is still tied to the rig it was cut from
        // =====================================================================================

        [Test]
        public void TheNmcSidecarHashesTheRigAsCommitted()
        {
            string rig = Path.Combine(RigCatalog.RepoRoot,
                "docs/art/rigs/shipyard-iso-kit/shipyardIsoRig.js");
            string sidecar = Path.Combine(RigCatalog.RepoRoot,
                "docs/art/rigs/shipyard-iso-kit/gameplay/shipyardIsoRig.nmc_yard.gameplay.json");

            Assert.IsTrue(File.Exists(rig), rig);
            Assert.IsTrue(File.Exists(sidecar), sidecar);

            string actual;
            using (var sha = SHA256.Create())
                actual = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(rig)))
                                     .Replace("-", "").ToLowerInvariant();

            Assert.IsTrue(File.ReadAllText(sidecar).Contains(actual, StringComparison.Ordinal),
                "The NMC sidecar's derivedFromRigSha256 does not match the committed rig bytes " +
                $"({actual}). A mismatch means the yard was re-laid-out: every polygon, the door and " +
                "the counter point must be re-derived before anything trusts them.");
        }

        [Test]
        public void TheNmcSidecarsInteriorStillMatchesTheShedTheRigBakes()
        {
            // The sidecar's interior polygon, door and counter were cut from these five numbers. If
            // the rig re-lays the yard out, the polygons are stale — so assert the numbers, not prose.
            _host.Execute($"{Scratch} = {G}.gameplay('smallYard',{{}});");
            _host.Execute($"{Scratch}b = {Scratch}.buildings.filter(function(b){{return b.kind==='shed';}})[0];");

            Assert.AreEqual(-6.5, Num($"{Scratch}b.x"), 1e-9, "shed x");
            Assert.AreEqual(-7.8, Num($"{Scratch}b.y"), 1e-9, "shed y");
            Assert.AreEqual(13.0, Num($"{Scratch}b.L"), 1e-9, "shed L (along x)");
            Assert.AreEqual(6.6, Num($"{Scratch}b.W"), 1e-9, "shed W (along y)");
            Assert.AreEqual(5.0, Num($"{Scratch}b.doorW"), 1e-9, "shed doorW");
            Assert.AreEqual(2.7, Num($"{Scratch}.walk[0].z"), 1e-9, "yard grade z");

            // ...and that the authored interior is genuinely INSIDE that shell, which is the whole
            // claim of a "true-to-footprint" interior.
            const double wall = 0.2;
            double x0 = -6.5 - 13.0 / 2 + wall, x1 = -6.5 + 13.0 / 2 - wall;
            double y0 = -7.8 - 6.6 / 2 + wall, y1 = -7.8 + 6.6 / 2 - wall;
            Assert.AreEqual(-12.8, x0, 1e-9); Assert.AreEqual(-0.2, x1, 1e-9);
            Assert.AreEqual(-10.9, y0, 1e-9); Assert.AreEqual(-4.7, y1, 1e-9);
        }

        // =====================================================================================
        // helpers
        // =====================================================================================

        /// <summary>
        /// Render one key's 8 facings at a given scale. Mirrors <c>WharfIsoSheetBaker.RenderFacings</c>
        /// except that it passes <c>pxPerM</c> — three sites cannot hold 8 facings under the 4096 cap
        /// at their own <c>fitScale()</c> and are baked further down the rig's ladder, so the shared
        /// helper's hardcoded <c>{}</c> opts would measure a cell nobody bakes. The measurement RULE is
        /// still the shared one: this only produces its input.
        /// </summary>
        static WharfIsoFacing[] RenderFacings(string key, int pxPerM)
        {
            var convention = RigCatalog.Get(Key).DeclaredConvention;
            int facings = _contract.Facings;
            var outp = new WharfIsoFacing[facings];

            for (int cell = 0; cell < facings; cell++)
            {
                string dir = RigBaker.DirForCell(cell, facings, convention)
                                     .ToString("R", CultureInfo.InvariantCulture);

                // Rasterise ONCE into a scratch global; reading each field off its own render() call
                // would re-rasterise per field, and a whole yard is up to ~2.5 s.
                _host.Execute($"{Scratch} = {G}.render({Q(key)},{dir},{{pxPerM:{pxPerM}}});");

                int w = (int)Num($"{Scratch}.w");
                int h = (int)Num($"{Scratch}.h");
                if (w <= 0 || h <= 0)
                    throw new InvalidOperationException(
                        $"{key} facing {cell} came back {w}×{h} — it resolved but rendered nothing.");

                Assert.AreEqual(pxPerM, (int)Num($"{Scratch}.pxPerM"),
                    $"{key}: asked for {pxPerM} px/m and the rig baked at something else");

                outp[cell] = new WharfIsoFacing(
                    _host.EvaluateBytes($"{Scratch}.data"), w, h,
                    Num($"{Scratch}.px"), Num($"{Scratch}.py"));
            }

            return outp;
        }

        /// <summary>Distinct rendered facings, counted from PIXELS — 4 facings labelled as 8 would
        /// otherwise sail through every dimension check in this file.</summary>
        static int RenderKeyFacingCount(string key)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int d = 0; d < 8; d++)
                seen.Add(Hash($"{G}.render({Q(key)},{d},{{}}).data"));
            return seen.Count;
        }

        static string Hash(string expr)
        {
            _host.Execute($"{Scratch}h = {expr};");
            byte[] b = _host.EvaluateBytes($"{Scratch}h");
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(b)).Replace("-", "");
        }

        static double Num(string expr) => _host.EvaluateNumber(expr);
        static string Q(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        /// <summary>
        /// The per-cell bake scale. <see cref="IsoPackContract"/>'s cell DTO predates a family whose
        /// keys bake at different scales and does not carry it, so it is read here from the same file
        /// rather than duplicated into a second source of truth.
        /// </summary>
        static Dictionary<string, int> LoadScales(string contractRelPath)
        {
            string json = File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, contractRelPath));
            var dto = JsonUtility.FromJson<ScaleDto>(json);
            Assert.IsNotNull(dto?.cells, $"{contractRelPath}: could not read cell scales");
            return dto.cells.ToDictionary(c => c.key, c => c.pxPerM, StringComparer.Ordinal);
        }

        [Serializable] class ScaleDto { public List<ScaleCell> cells; }
        [Serializable] class ScaleCell { public string key; public int pxPerM; }
    }
}
