using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The deck-loop kit's BAKE PATH, proven end-to-end without a single committed sheet.
    ///
    /// <para><see cref="DeckLoopKitContractTests"/> (#480) proves the kit's 24 cells reproduce from the
    /// live rigs and that the keyline gate is real. It does that with its own private reader, because
    /// at the time nothing in the bake path could load this contract at all. This file is the other
    /// half: that the registries — contract, slicer, catalog — now agree the kit exists, that the
    /// baker they select measures the SAME 24 cells through its own <c>MeasureCell</c>, and that the
    /// three traps this kit carries are each closed by something mechanical.</para>
    ///
    /// <para><b>The three traps, and what closes each.</b></para>
    /// <list type="number">
    ///   <item><b>It turns the other way.</b> First measured-clockwise family in the repo, sharing a
    ///         cell rule AND a −46.75°/step screen figure with two counter-clockwise siblings. Closed
    ///         by <see cref="IsoPackContract.AssertConventionAgrees"/>, pinned in
    ///         <see cref="TheKitTakesNoFacingCorrection_AndTheCatalogAndContractSayTheSameThing"/> —
    ///         together with what the wrong answer would have DONE, which is the part a reader needs.</item>
    ///   <item><b>Three different render() call shapes across five families</b>, and the wrong one does
    ///         not throw: a key passed to a key-less <c>render()</c> is read as its DIR. Closed by
    ///         <see cref="IsoPropSheetBaker.Shapes"/>, pinned against the rigs' own arity in
    ///         <see cref="EveryFamilysRegisteredCallShapeMatchesItsRig"/>.</item>
    ///   <item><b>One contract file, five families.</b> A registration without a section hands all
    ///         five deckGear's cells — real numbers, wrong ones. Closed by
    ///         <see cref="IsoPackContract.Registration.Section"/>, pinned in
    ///         <see cref="EachFamilyLoadsItsOwnSectionAndNotItsNeighboursCells"/>.</item>
    /// </list>
    ///
    /// <para>No sheet is written here, and no PNG is committed by the branch this file lands on — the
    /// coordinator bakes after merge. What can be wrong is the geometry and the wiring, and both are
    /// measurable without touching the disk.</para>
    /// </summary>
    public class DeckLoopKitBakeTests
    {
        /// <summary>Catalog key → the section it occupies in the kit contract. Order is the kit
        /// README's. The two spellings differ for three of the five, which is the whole reason the
        /// section is registered rather than assumed equal to the key.</summary>
        static readonly (string Key, string Section)[] Families =
        {
            ("deckGear", "deckGear"), ("trap", "trap"), ("fishTray2", "tray"),
            ("buoyIso", "buoy"), ("trapFauna", "trapFauna"),
        };

        /// <summary>What the kit bakes: 18 directional pieces at 8 facings, 6 catch kinds at one.</summary>
        const int TotalCells = 24;

        static IRigScriptHost _host;
        static readonly Dictionary<string, IsoPackContract> Contracts =
            new Dictionary<string, IsoPackContract>(StringComparer.Ordinal);
        static readonly Dictionary<string, RigGeometry> Geometry =
            new Dictionary<string, RigGeometry>(StringComparer.Ordinal);

        [OneTimeSetUp]
        public void InstallTheKitOnce()
        {
            // One V8 host for the fixture: the five rigs declare disjoint globals over one shared
            // turntable, and installing them is the expensive part.
            //
            // ⚠️ Installed through IsoPropSheetBaker.InstallAndGuard — the bake's OWN entry point,
            // not a copy of it. That means every standing guard runs here for real: the contract's
            // self-consistency, the handedness cross-check, the KEYLINE_DEFAULT gate, and the native
            // buffer. A test that installed the rigs itself would prove the rigs load and nothing
            // about whether a bake can.
            _host = RigScriptHostFactory.Create();

            foreach (var (key, _) in Families)
            {
                var contract = IsoPackContract.Load(key);
                Contracts[key] = contract;
                Geometry[key] = IsoPropSheetBaker.InstallAndGuard(_host, RigCatalog.Get(key), contract, key);
            }
        }

        [OneTimeTearDown]
        public void DisposeHost() => _host?.Dispose();

        static IsoPackContract C(string key) => Contracts[key];
        static string G(string key) => RigCatalog.Get(key).GlobalName;
        static string Q(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";

        // =====================================================================================
        // 1. registration — the guard that caught #472's omission, one registry wider
        // =====================================================================================

        /// <summary>
        /// All five families are known to all THREE registries. A family missing from any one of them
        /// fails differently and none of the three failures is loud: absent from the contract registry
        /// there is nothing to bake against, absent from the slicer the sheets bake and import as
        /// spriteMode Multiple with EMPTY rects (and load as nothing), absent from the catalog there is
        /// no rig to install.
        /// </summary>
        [Test]
        public void AllFiveFamiliesAreRegisteredInTheContractRegistryTheSlicerAndTheCatalog()
        {
            foreach (var (key, section) in Families)
            {
                Assert.IsTrue(IsoPackContract.Registry.ContainsKey(key),
                    $"{key}: absent from IsoPackContract.Registry — there is no contract to bake against.");
                Assert.AreEqual(section, IsoPackContract.Registry[key].Section,
                    $"{key}: registered against the wrong section of the kit contract.");

                Assert.IsTrue(RigCatalog.Entries.ContainsKey(key),
                    $"{key}: absent from RigCatalog — there is no rig source to install.");

                Assert.AreEqual(1, IsoPackSheetSlicer.Families.Count(f => f.Key == key),
                    $"{key}: absent from (or duplicated in) IsoPackSheetSlicer.Families — an unsliced " +
                    "sheet imports with EMPTY sprite rects, which looks fine on disk and loads as nothing.");

                var fam = IsoPackSheetSlicer.Families.Single(f => f.Key == key);
                Assert.AreEqual(IsoPackContract.Paths[key], fam.ContractPath,
                    $"{key}: the slicer and the baker must read the SAME contract file.");
                Assert.AreEqual(section, fam.Section,
                    $"{key}: the slicer and the baker must read the same SECTION of it.");
            }
        }

        /// <summary>
        /// The five share a contract and must NOT share a sprite folder — one folder of 24 sheets makes
        /// every family's slice pass walk the other four's PNGs and warn about each, and any future stem
        /// collision would slice one family's sheet against another's grid.
        /// </summary>
        [Test]
        public void EachFamilyBakesIntoItsOwnFolder_AndTheSlicerLooksInTheSameOne()
        {
            var folders = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (key, _) in Families)
            {
                string folder = IsoPackContract.SheetFolderFor(key);
                Assert.IsTrue(folders.Add(folder), $"{key}: shares its sheet folder '{folder}'.");

                string contractPath = IsoPackContract.Paths[key];
                string contractDir = contractPath.Substring(0, contractPath.LastIndexOf('/'));
                Assert.AreNotEqual(contractDir, folder,
                    $"{key}: its sheet folder is the contract's own folder, which is what DERIVING it " +
                    "would produce. One contract governs five folders here, so a derived folder hands " +
                    "all five the same one and every read-side lookup misses.");
                StringAssert.StartsWith(contractDir + "/", folder,
                    $"{key}: the kit's sheets belong under {contractDir}/.");

                // The slicer's folder carries a trailing slash and the registry's does not; they are
                // the same place and a mismatch means a bake and its slice pass look in different ones.
                var fam = IsoPackSheetSlicer.Families.Single(f => f.Key == key);
                Assert.AreEqual(folder + "/", fam.Folder,
                    $"{key}: the baker writes to '{folder}' and the slicer reads '{fam.Folder}'.");

                Assert.AreEqual(folder, IsoPackSprites.FolderFor(key),
                    $"{key}: the READ side would look somewhere the bake never wrote.");
            }
        }

        /// <summary>
        /// Every family the contract registry knows must resolve to a rig and to a baker. This is the
        /// deck-loop-shaped version of the omission that #472 shipped: a registry entry with no baker
        /// behind it reads as "registered" everywhere a human looks.
        /// </summary>
        [Test]
        public void EveryRegisteredFamilyHasABakerThatWillTakeIt()
        {
            foreach (string key in IsoPackContract.Families)
            {
                var contract = IsoPackContract.Load(key);

                switch (contract.Rule)
                {
                    case IsoPackContract.CellRule.InkUnionFixed:
                        Assert.Contains(key, IsoPropSheetBaker.Families.ToArray(),
                            $"{key} bakes by the fixed-buffer rule but IsoPropSheetBaker does not serve " +
                            "it — and a family with no registered render shape cannot be called at all.");
                        break;
                    case IsoPackContract.CellRule.BufferUnion:
                        Assert.IsTrue(contract.NeedsInstallModule,
                            $"{key} measures a returned BUFFER, so it must be the InstallModule kind.");
                        break;
                    case IsoPackContract.CellRule.Analytic:
                        Assert.AreEqual(ShoreFindsSheetBaker.RigKey, key,
                            "shoreFinds is the only analytic family; a second one needs its own baker.");
                        break;
                    default:
                        Assert.Fail($"{key}: unhandled cell rule {contract.Rule}");
                        break;
                }
            }
        }

        // =====================================================================================
        // 2. one file, five families
        // =====================================================================================

        /// <summary>
        /// ⚠️ <b>The failure this closes is silent.</b> Five registrations against one file with no
        /// section resolve to whichever family the reader finds first — real cells, a real grid, a real
        /// pivot, and the wrong ones for four families out of five. So the sections are asserted to be
        /// genuinely different data rather than merely present.
        /// </summary>
        [Test]
        public void EachFamilyLoadsItsOwnSectionAndNotItsNeighboursCells()
        {
            var byKey = Families.ToDictionary(f => f.Key, f => C(f.Key), StringComparer.Ordinal);

            Assert.AreEqual(TotalCells, byKey.Values.Sum(c => c.Count), "the kit's cell count moved");

            // Every family names a different rig and a different set of keys.
            CollectionAssert.AllItemsAreUnique(byKey.Values.Select(c => c.RigName).ToArray(),
                "two families resolved to the same rig — a section is being ignored");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (key, _) in Families)
                foreach (var cell in C(key).Cells)
                    Assert.IsTrue(seen.Add(cell.key),
                        $"'{cell.key}' appears in more than one family. Either the kit genuinely reuses " +
                        "a key across families — in which case the sheet stems collide — or a section " +
                        "resolved to the wrong family's cells.");

            // The native buffer is per family, and it is what IsoPropSheetBaker checks the rig against.
            // If sections leaked, five families would report one buffer.
            CollectionAssert.AllItemsAreUnique(
                byKey.Values.Select(c => $"{c.Proj.nativeSheetW}x{c.Proj.nativeSheetH}").ToArray(),
                "two families claim the same native buffer — the per-family natives did not resolve");

            foreach (var (key, _) in Families)
            {
                var geo = Geometry[key];
                Assert.AreEqual(geo.Width, C(key).Proj.nativeSheetW, $"{key}: native buffer width");
                Assert.AreEqual(geo.Height, C(key).Proj.nativeSheetH, $"{key}: native buffer height");
            }
        }

        [Test]
        public void EveryFamilysContractIsSelfConsistent()
        {
            foreach (var (key, _) in Families)
            {
                Assert.DoesNotThrow(() => C(key).AssertSelfConsistent(), $"{key} contract");
                Assert.DoesNotThrow(() => C(key).ToString(), $"{key}.ToString()");
                Debug.Log($"[deck-loop] {C(key)}");
            }
        }

        // =====================================================================================
        // 3. handedness — the trap that mirrors every cell of every piece
        // =====================================================================================

        /// <summary>
        /// The contract MEASURED clockwise, the catalog DECLARES clockwise, and the bake therefore
        /// applies NO facing correction — cell <c>i</c> is rendered at the rig's own <c>dir i</c>.
        ///
        /// <para>The second half of this test is the part worth reading: it shows what the fleet's
        /// correction WOULD have done to this kit, so the claim "do not apply it" is a number rather
        /// than a warning comment. Cells 1–7 come out reversed and cell 0 does not move, which is why
        /// a spot-check of the first facing would have found nothing.</para>
        /// </summary>
        [Test]
        public void TheKitTakesNoFacingCorrection_AndTheCatalogAndContractSayTheSameThing()
        {
            foreach (var (key, _) in Families)
            {
                Assert.AreEqual(AzimuthConvention.Clockwise, RigCatalog.Get(key).DeclaredConvention,
                    $"{key}: the catalog must declare this kit Clockwise — it was MEASURED so (#473).");
                Assert.AreEqual("clockwise", C(key).Azi.convention,
                    $"{key}: the contract must record the same handedness the catalog declares.");
                Assert.DoesNotThrow(
                    () => C(key).AssertConventionAgrees(RigCatalog.Get(key).DeclaredConvention, key),
                    $"{key}: the bake's own cross-check must pass");
            }

            for (int cell = 0; cell < 8; cell++)
            {
                Assert.AreEqual(cell, RigBaker.DirForCell(cell, 8, AzimuthConvention.Clockwise), 1e-9,
                    $"cell {cell} must bake the rig's own dir {cell} — this kit takes no correction.");

                double corrected = RigBaker.DirForCell(cell, 8, AzimuthConvention.CounterClockwise);
                Assert.AreEqual((8 - cell) % 8, corrected, 1e-9);
                if (cell != 0)
                    Assert.AreNotEqual(cell, corrected,
                        $"cell {cell} would bake dir {corrected} under the fleet's correction. That is " +
                        "the mirror this kit must not receive — and cell 0 is unmoved by it, so " +
                        "checking one facing would not have caught it.");
            }
        }

        /// <summary>
        /// A wrong handedness must FAIL rather than mirror. Driven with the opposite convention through
        /// the contract's own guard, because that guard is the only thing standing between a re-declared
        /// catalog entry and eight silently-reversed cells.
        /// </summary>
        [Test]
        public void DeclaringTheKitCounterClockwiseWouldRefuseTheBake()
        {
            foreach (var (key, _) in Families)
                Assert.Throws<InvalidOperationException>(
                    () => C(key).AssertConventionAgrees(AzimuthConvention.CounterClockwise, key),
                    $"{key}: a counter-clockwise declaration must refuse. If this ever passes, the " +
                    "correction is applied on a guess and every piece in the family faces backwards.");
        }

        // =====================================================================================
        // 4. facings — from the contract, never from nativeDirs
        // =====================================================================================

        [Test]
        public void FacingsComeFromTheContract_BecauseNativeDirsReadsZeroForAllFive()
        {
            foreach (var (key, _) in Families)
            {
                Assert.AreEqual(0, Geometry[key].NativeDirs,
                    $"{key} started declaring a numeric DIRS. 0 means 'the rig does not say' and is " +
                    "reported SILENTLY — re-check that the contract is still the authority before " +
                    "anything starts believing this field.");

                int expected = key == "trapFauna" ? 1 : 8;
                Assert.AreEqual(expected, C(key).Facings, $"{key}: facings");
                Assert.AreEqual(expected, C(key).CellsPerSheet, $"{key}: cells per sheet");
            }
        }

        /// <summary>
        /// <c>trapFauna</c> records <c>facings: 1</c> rather than omitting the axis, and that is load
        /// bearing: an absent field reads back as JsonUtility's 0, and a family with 0 cells per sheet
        /// bakes and slices nothing at all while reporting success.
        /// </summary>
        [Test]
        public void TheCatchIsNotDirectionalAndItsSingleCellIsStatedRatherThanMissing()
        {
            Assert.AreEqual(1, C("trapFauna").Facings);
            Assert.AreEqual(1, _host.EvaluateNumber("TrapFauna.render.length - 1"),
                "TrapFauna.render should be (kind, opts) — a dir parameter would change the contract");

            foreach (var cell in C("trapFauna").Cells)
            {
                C("trapFauna").GridFor(cell.key, out int cols, out int rows);
                Assert.AreEqual(1, cols * rows, $"trapFauna.{cell.key}: one cell, one slot");
            }
        }

        // =====================================================================================
        // 5. the three call shapes
        // =====================================================================================

        /// <summary>
        /// Each family's registered call shape matches the arity its rig actually exposes, and its key
        /// table lists exactly the keys the contract measures.
        ///
        /// <para>⚠️ The wrong shape does not throw. <c>FishTray2.render(dir, opts)</c> handed the string
        /// <c>'tray'</c> takes it as the DIR, and the rig coerces it — so the sheet bakes, at the right
        /// size, showing one facing eight times. That is why the arity is asserted from the rig rather
        /// than trusted from a table.</para>
        /// </summary>
        [Test]
        public void EveryFamilysRegisteredCallShapeMatchesItsRig()
        {
            foreach (var (key, _) in Families)
            {
                var shape = IsoPropSheetBaker.Shapes[key];
                string g = G(key);

                // render()'s declared arity, minus the trailing opts object.
                int args = (int)_host.EvaluateNumber($"{g}.render.length") - 1;
                int registered = (shape.TakesKey ? 1 : 0) + (shape.TakesDir ? 1 : 0);
                Assert.AreEqual(args, registered,
                    $"{key}: {g}.render takes {args} argument(s) before opts, but the registered shape " +
                    $"passes {registered}. A mismatch does not throw — it renders a plausible, wrong sheet.");

                if (shape.KeyTable == null)
                {
                    Assert.AreEqual(1, C(key).Count,
                        $"{key} names no key table, so it must draw exactly one thing.");
                    continue;
                }

                Assert.IsTrue(_host.EvaluateBool($"Array.isArray({g}.{shape.KeyTable})"),
                    $"{key}: {g}.{shape.KeyTable} is not an array — the key probe would throw on it.");

                var rigKeys = _host.EvaluateString($"{g}.{shape.KeyTable}.join(',')")
                                   .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                CollectionAssert.AreEquivalent(rigKeys, C(key).Cells.Select(c => c.key).ToArray(),
                    $"{key}: the rig's {shape.KeyTable} and the contract's keys have drifted. " +
                    "Regenerate the contract rather than editing one side to match.");
            }
        }

        /// <summary>
        /// A key the rig does not know is REFUSED. It would otherwise draw nothing and land as an empty
        /// sheet with a perfectly correct sprite count — and for <c>trapFauna</c> #480 made the rig
        /// itself throw, so the two refusals must not be mistaken for one another.
        /// </summary>
        [Test]
        public void AnUnknownKeyIsRefusedRatherThanBakedEmpty()
        {
            foreach (var (key, _) in Families)
                Assert.Throws<InvalidOperationException>(
                    () => IsoPropSheetBaker.RenderFacings(_host, key, "noSuchPiece", C(key),
                                                          Geometry[key],
                                                          RigCatalog.Get(key).DeclaredConvention),
                    $"{key}: baking an unknown key must refuse.");
        }

        // =====================================================================================
        // 6. THE BIG ONE — all 24 cells re-derived through the baker's own rule
        // =====================================================================================

        /// <summary>
        /// Every committed cell, re-measured from the live rigs through
        /// <see cref="IsoPropSheetBaker.MeasureCell"/> — the bake's own implementation of the rule, not
        /// a copy of it. #480 re-derives the same 24 with its own reader; this one proves the BAKER
        /// arrives at them, which is a different claim and the one a coordinator is relying on.
        ///
        /// <para>Renders through <see cref="IsoPropSheetBaker.RenderFacings"/> for the same reason: the
        /// call shape is per family, and a test that spelled the three shapes itself would be exactly
        /// the second source of truth this file exists to remove.</para>
        /// </summary>
        [Test]
        public void EveryCellReproducesFromTheLiveRigThroughTheBakersOwnRule()
        {
            var failures = new List<string>();
            int checkedCells = 0;

            foreach (var (key, _) in Families)
            {
                var contract = C(key);
                var geo = Geometry[key];
                int px = Mathf.RoundToInt((float)geo.PivotX), py = Mathf.RoundToInt((float)geo.PivotY);

                foreach (var cell in contract.Cells)
                {
                    var facings = IsoPropSheetBaker.RenderFacings(
                        _host, key, cell.key, contract, geo, RigCatalog.Get(key).DeclaredConvention);

                    Assert.AreEqual(contract.Facings, facings.Length, $"{key}.{cell.key}: facing count");

                    Assert.IsTrue(
                        IsoPropSheetBaker.MeasureCell(facings, geo.Width, geo.Height, px, py,
                                                      out int cw, out int ch, out int pvx, out int pvy,
                                                      out _, out _, out bool pivotInsideInk),
                        $"{key}.{cell.key}: every facing rendered fully transparent.");

                    if (cw != cell.cellW || ch != cell.cellH || pvx != cell.pivotX || pvy != cell.pivotY)
                        failures.Add($"{key}.{cell.key}: rig gives {cw}×{ch} pivot ({pvx},{pvy}), " +
                                     $"contract says {cell.cellW}×{cell.cellH} pivot " +
                                     $"({cell.pivotX},{cell.pivotY})");

                    if (pivotInsideInk != cell.pivotInsideInk)
                        failures.Add($"{key}.{cell.key}: pivotInsideInk measured {pivotInsideInk}, " +
                                     $"contract says {cell.pivotInsideInk}");

                    checkedCells++;
                }
            }

            Assert.IsEmpty(failures,
                $"{failures.Count} of {checkedCells} committed cells no longer reproduce through the " +
                $"baker:\n  {string.Join("\n  ", failures)}\n\n" +
                "The rig is the truth. Re-emit the contract with " +
                "`node docs/art/rigs/deck-loop-kit/_verify.js --emit` in the same commit — do not " +
                "relax this.");
            Assert.AreEqual(TotalCells, checkedCells, "the kit's cell count moved — re-emit the contract");
        }

        /// <summary>
        /// The bake asserts the rendered cell against the contract before it writes a pixel, and that
        /// assert is the oracle — so it must be reached, and it must pass, for all 24.
        /// </summary>
        [Test]
        public void TheContractAssertAcceptsWhatTheBakerMeasures()
        {
            foreach (var (key, _) in Families)
                foreach (var cell in C(key).Cells)
                {
                    Assert.DoesNotThrow(() => C(key).AssertMatchesContract(
                        cell.key, cell.cellW, cell.cellH, cell.pivotX, cell.pivotY));
                    Assert.Throws<InvalidOperationException>(() => C(key).AssertMatchesContract(
                        cell.key, cell.cellW + 1, cell.cellH, cell.pivotX, cell.pivotY),
                        $"{key}.{cell.key}: a one-pixel-wider cell must be refused, not absorbed.");
                }
        }

        // =====================================================================================
        // 7. packing, the cap, and the slice grid
        // =====================================================================================

        /// <summary>
        /// Every plan holds its family's cells exactly, its arithmetic is exact, and it is under the
        /// cap. Over the cap a sheet does NOT fail to import — it imports silently downscaled with the
        /// sprite count still correct, and every slice rect then lands wrong.
        /// </summary>
        [Test]
        public void EveryCommittedSheetPlanIsExactAndUnderTheCap()
        {
            foreach (var (key, _) in Families)
            {
                var contract = C(key);
                Assert.AreEqual(2048, contract.ImportSizeCap, $"{key}: the kit's import cap");

                foreach (var cell in contract.Cells)
                {
                    contract.GridFor(cell.key, out int cols, out int rows);
                    Assert.AreEqual(contract.CellsPerSheet, cols * rows,
                        $"{key}.{cell.key}: a {cols}×{rows} grid is not {contract.CellsPerSheet} slots. " +
                        "A LARGER grid is legal elsewhere (shipyardIso packs 3×3 for 8 facings); this " +
                        "kit's cells are small enough that every plan is an exact divisor, so a ragged " +
                        "one here means the packing rule moved.");

                    Assert.DoesNotThrow(() => contract.AssertSheetFits(
                        cell.key, cols, rows, cols * cell.cellW, rows * cell.cellH),
                        $"{key}.{cell.key}: sheet plan");
                }
            }
        }

        /// <summary>
        /// <b>The cap-lift question, answered with a number.</b> The slicer raises
        /// <c>maxTextureSize</c> when a sheet needs more than Unity's DEFAULT 2048 — the iso-rig-pack's
        /// 3784 px sheets do. This kit's widest sheet is <c>haulerstation</c> at 416 px, so the lift
        /// never fires and no sheet here can hit the silent-downscale trap. Asserted rather than
        /// assumed, because the day a cell grows past 256 px this becomes false quietly.
        /// </summary>
        [Test]
        public void NoSheetInTheKitNeedsTheSlicersMaxTextureSizeLift()
        {
            const int unityDefaultCap = 2048;
            string widest = null;
            int worst = 0;

            foreach (var (key, _) in Families)
                foreach (var cell in C(key).Cells)
                {
                    C(key).GridFor(cell.key, out int cols, out int rows);
                    int maxDim = Mathf.Max(cols * cell.cellW, rows * cell.cellH);
                    int needed = Mathf.NextPowerOfTwo(maxDim);

                    Assert.LessOrEqual(needed, unityDefaultCap,
                        $"{key}.{cell.key} is {cols * cell.cellW}×{rows * cell.cellH} and needs a " +
                        $"{needed} px import — past Unity's default {unityDefaultCap}. The slicer's " +
                        "lift would now have to fire for this kit; that is fine, but it is a new fact " +
                        "and this assertion is where it gets recorded.");

                    if (maxDim <= worst) continue;
                    worst = maxDim;
                    widest = $"{key}.{cell.key} {cols * cell.cellW}×{rows * cell.cellH}";
                }

            Debug.Log($"[deck-loop] widest sheet: {widest} ({worst} px on the longest side, " +
                      $"{unityDefaultCap - worst} px under Unity's default importer cap).");
        }

        /// <summary>
        /// The contracts record pivots from the TOP-LEFT and Unity normalises from the BOTTOM-LEFT.
        /// Inverted, a piece plants itself through the deck by twice its own height and nothing reports
        /// an error — so the ADR 0026 formula is asserted per cell, in pixels.
        /// </summary>
        [Test]
        public void TheSlicerNormalisesEveryKitPivotTheAdr0026Way()
        {
            foreach (var (key, _) in Families)
                foreach (var cell in C(key).Cells)
                {
                    Vector2 p = IsoPackSheetSlicer.NormalisedPivot(cell.cellW, cell.cellH,
                                                                   cell.pivotX, cell.pivotY);

                    Assert.AreEqual(cell.pivotX, p.x * cell.cellW, 0.001,
                        $"{key}.{cell.key}: x must round-trip to the contract's pixel column.");
                    Assert.AreEqual(cell.cellH - cell.pivotY, p.y * cell.cellH, 0.001,
                        $"{key}.{cell.key}: y must be (cellH − pivotY), measured UP from the bottom.");

                    Assert.That(p.x, Is.InRange(0f, 1f), $"{key}.{cell.key} pivot x out of the cell");
                    Assert.That(p.y, Is.InRange(0f, 1f), $"{key}.{cell.key} pivot y out of the cell");
                }
        }

        /// <summary>Every slice rect lands inside its sheet and no two overlap, on the kit's own
        /// grids — one row of eight, and the catch's single slot.</summary>
        [Test]
        public void EverySliceRectLandsInsideItsSheet()
        {
            foreach (var (key, _) in Families)
                foreach (var cell in C(key).Cells)
                {
                    C(key).GridFor(cell.key, out int cols, out int rows);
                    int sheetW = cols * cell.cellW, sheetH = rows * cell.cellH;
                    var seen = new HashSet<Vector2>();

                    for (int i = 0; i < C(key).CellsPerSheet; i++)
                    {
                        Rect r = IsoPackSheetSlicer.CellRect(i, cols, rows, cell.cellW, cell.cellH);
                        Assert.That(r.xMin, Is.GreaterThanOrEqualTo(0));
                        Assert.That(r.yMin, Is.GreaterThanOrEqualTo(0));
                        Assert.That(r.xMax, Is.LessThanOrEqualTo(sheetW), $"{key}.{cell.key} slice {i}");
                        Assert.That(r.yMax, Is.LessThanOrEqualTo(sheetH), $"{key}.{cell.key} slice {i}");
                        Assert.IsTrue(seen.Add(r.position), $"{key}.{cell.key} slice {i} overlaps");
                    }
                }
        }

        // =====================================================================================
        // 8. the fauna prerequisite — #480 made the refusal loud; the bake must not provoke it
        // =====================================================================================

        /// <summary>
        /// <b>The bake path carries <c>catchKit</c> and <c>crustacean</c>, not just the turntable.</b>
        ///
        /// <para>The two globals in <c>trapFaunaRig.js</c> split the kit's catch: <c>TrapFauna</c> owns
        /// six kinds and DELEGATES lobster / crab / jonah / short to <c>CatchKit</c> and
        /// <c>Crustacean</c>. Without them <c>TrapCatch</c> returns null for a delegated kind — the
        /// kit's own documented silent-empty failure — and since #480 <c>TrapFauna.render</c> THROWS
        /// for the same kinds rather than drawing a byte-identical urchin in their place.</para>
        ///
        /// <para>Asserted on the host the BAKE builds, not on a host this test assembles: the whole
        /// point is that the prerequisite travels with the catalog entry, the way
        /// <c>deckIsoSolid</c> does, rather than being remembered at each call site.</para>
        /// </summary>
        [Test]
        public void TheBakePathInstallsTheFaunasDelegationPrerequisites()
        {
            var prereqs = RigCatalog.Get("trapFauna").Prerequisites;
            CollectionAssert.Contains(prereqs, "deckIsoSolid", "the shared turntable");
            CollectionAssert.Contains(prereqs, "catchKit",
                "TrapCatch delegates lobster/crab to CatchKit and returns null without it");
            CollectionAssert.Contains(prereqs, "crustacean",
                "jonah and short are the Crustacean plotter re-tinted — TrapCatch returns null without it");

            // _host is the one InstallAndGuard built in OneTimeSetUp — i.e. the bake's own host.
            foreach (string global in new[] { "IsoSolid", "CatchKit", "Crustacean", "TrapFauna" })
                Assert.IsTrue(_host.EvaluateBool($"typeof {global} === 'object' && {global} !== null"),
                    $"{global} is not in the bake's host. A missing prerequisite here does not throw " +
                    "at install time — it changes what the file's other half returns.");
        }

        /// <summary>
        /// #480's refusal must never fire during a correct bake: every key the contract measures is a
        /// kind this rig OWNS. The negative arm is the point — the refusal is a live guard, not a
        /// renderer that has stopped.
        /// </summary>
        [Test]
        public void TheFaunaRefusalNeverFiresForAKeyTheBakeActuallyAsks()
        {
            string owned = _host.EvaluateString("TrapFauna.KINDS.join(',')");
            var ownedKinds = owned.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            CollectionAssert.AreEquivalent(ownedKinds, C("trapFauna").Cells.Select(c => c.key).ToArray(),
                "the contract must measure exactly the kinds this rig owns. A delegated kind in the " +
                "contract would take the whole bake down on #480's refusal.");

            foreach (string kind in ownedKinds)
                Assert.DoesNotThrow(() => _host.EvaluateBytes($"TrapFauna.render({Q(kind)},{{}})"),
                    $"TrapFauna.render('{kind}') is a kind the bake asks for and must draw.");

            foreach (string kind in new[] { "lobster", "crab", "jonah", "short" })
                Assert.IsTrue(
                    _host.EvaluateBool(
                        $"(function(){{ try {{ TrapFauna.render({Q(kind)},{{}}); return false; }} " +
                        "catch(e){ return /not one of this rig's kinds/.test(String(e && e.message)); } })()"),
                    $"TrapFauna.render('{kind}') stopped refusing. It is drawn by CatchKit or " +
                    "Crustacean; drawing it here would come up as a plausible urchin (#480).");
        }
    }
}
