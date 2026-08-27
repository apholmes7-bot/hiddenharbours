using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The acceptance tests for the boat-interior sheets: the right hulls, and every one of them
    /// registered on its boat.</b>
    ///
    /// <para>Two properties, and the whole kit is worthless without either.</para>
    ///
    /// <para><b>1. Coverage is exactly the cleared set.</b> All twenty-seven hulls now have a
    /// <c>BoatInteriorDef</c> and sheets: the two sport fishers were cleared 2026-08-26 when the
    /// cutaway kit stamped their renderer pin, and the Cape Islander 2026-08-27 when her forked rig
    /// was MERGED (third sha <c>60d127c3…</c>). A kit that quietly covered 24 of 27 would look
    /// complete — that is exactly what makes the count worth asserting rather than eyeballing, and
    /// the assertion is on the LEDGER's cleared set, so it tracks a future refusal too.</para>
    ///
    /// <para><b>2. Every sprite is the FULL hull cell at the HULL's pivot.</b> This is the property
    /// the runtime is built on ("composites under the exterior 1:1"), and it is the one that fails
    /// invisibly: a sheet sliced on the wrong grid, or reverted to the importer's default pivot, still
    /// imports, still counts right, still shows a perfectly good picture of a cabin — three pixels off
    /// its own boat, in every facing, for ever. So the cell and the pivot are compared against the
    /// EXTERIOR RIG ITSELF, run here, rather than against a number this file restates.</para>
    /// </summary>
    public class BoatInteriorSheetTests
    {
        const string InteriorDefFolder = "Assets/_Project/Data/Boats/Interiors";
        const string RigFolder = "docs/art/rigs";

        static string RepoRoot => Directory.GetParent(Application.dataPath).FullName;

        // ---- fixture ----------------------------------------------------------------------------

        static BoatInteriorKit.Contract _contract;
        static Dictionary<string, BoatInteriorS0Entry> _ledger;
        static Dictionary<string, RigGeometry> _exteriorGeometryByHullStem;
        static Dictionary<string, string[]> _sidecarLevelsByHullStem;
        static Dictionary<string, double> _bearingByHullStem;
        static string[] _cleanHullStems;

        [OneTimeSetUp]
        public void LoadEverythingOnce()
        {
            string contractAbs = Path.Combine(RepoRoot, BoatInteriorKit.ContractPath);
            if (File.Exists(contractAbs))
                _contract = JsonUtility.FromJson<BoatInteriorKit.Contract>(File.ReadAllText(contractAbs));

            _ledger = BoatInteriorS0Ledger.Parse(
                File.ReadAllText(Path.Combine(RepoRoot, BoatInteriorS0Ledger.LedgerPath)));

            // Which hulls the intake CLEARED, derived the same way the baker derives it: one row per
            // interior sidecar, adjudicated through the ledger (the 18 variants inherit their family's).
            var clean = new List<string>();
            _sidecarLevelsByHullStem = new Dictionary<string, string[]>(StringComparer.Ordinal);

            string kit = Path.Combine(RepoRoot, BoatInteriorKit.KitFolder);
            foreach (string path in Directory.GetFiles(kit, "*.interior.json", SearchOption.AllDirectories)
                                             .OrderBy(p => p, StringComparer.Ordinal))
            {
                object root = DeckSidecarJson.Parse(File.ReadAllText(path));
                string stem = DeckSidecarJson.String(DeckSidecarJson.Member(root, "hull_stem")) ?? "";
                if (!BoatInteriorS0Ledger.For(_ledger, stem).IsClean) continue;

                clean.Add(stem);

                var levels = DeckSidecarJson.AsArray(
                    DeckSidecarJson.Member(DeckSidecarJson.Member(root, "cell"), "levels"));
                _sidecarLevelsByHullStem[stem] =
                    levels == null
                        ? Array.Empty<string>()
                        : levels.Select(DeckSidecarJson.String).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            }
            clean.Sort(StringComparer.Ordinal);
            _cleanHullStems = clean.ToArray();

            LoadExteriorGeometry();
        }

        /// <summary>
        /// The SHIPPED exterior rig's cell for every cleared hull, read by running the rig.
        ///
        /// <para>Deliberately <c>docs/art/rigs/</c> and not the kit's <c>hull-rigs/</c>: the question
        /// this suite asks is whether the interior art registers on the boat THE GAME DRAWS. That the
        /// kit's revision agrees is a separate fact, and the bake already refuses without it.</para>
        ///
        /// <para>Which rig belongs to which hull comes from the interior rig's own <c>HULLS</c> table,
        /// so nothing here is a table that could drift. One host per distinct rig file — both copies
        /// install the same global, so a host can only hold one of them.</para>
        /// </summary>
        static void LoadExteriorGeometry()
        {
            _exteriorGeometryByHullStem = new Dictionary<string, RigGeometry>(StringComparer.Ordinal);
            _bearingByHullStem = new Dictionary<string, double>(StringComparer.Ordinal);

            IReadOnlyList<BoatInteriorRigHost.HullBinding> table;
            using (var host = RigScriptHostFactory.Create())
                table = BoatInteriorRigHost.ReadHullTable(host);

            var bindingByStem = new Dictionary<string, BoatInteriorRigHost.HullBinding>(StringComparer.Ordinal);
            foreach (string stem in _cleanHullStems)
                if (BoatInteriorRigHost.TryBind(table, stem, out var b, out _))
                    bindingByStem[stem] = b;

            var bearingByStem = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (string rigFile in bindingByStem.Values.Select(b => b.ExteriorRigFileName).Distinct()
                                                    .OrderBy(s => s, StringComparer.Ordinal))
            {
                string abs = Path.Combine(RepoRoot, RigFolder, rigFile);
                if (!File.Exists(abs)) continue;

                using var host = RigScriptHostFactory.Create();
                host.Execute(File.ReadAllText(abs));

                // Geometry AND the handedness oracle are taken PER HULL, addressed through the
                // binding's own hull object (ExteriorHullJs): a multi-hull global (the sport
                // fishers' SportFisherIso2) publishes W/H/pivot per hull, and a per-file read
                // would replay the first hull's cell onto the second — the same wrong boat the
                // baker's own (file, pick) grouping exists to refuse. One host still serves
                // every hull the file makes.
                foreach (var kv in bindingByStem)
                {
                    if (kv.Value.ExteriorRigFileName != rigFile) continue;
                    string global = kv.Value.ExteriorHullJs;
                    if (!host.EvaluateBool($"!!(typeof ({global}) === 'object' && ({global}) && " +
                                           $"typeof ({global}).W === 'number' && ({global}).pivot)"))
                        continue;

                    _exteriorGeometryByHullStem[kv.Key] = new RigGeometry(
                        (int)host.EvaluateNumber($"({global}).W"),
                        (int)host.EvaluateNumber($"({global}).H"),
                        host.EvaluateNumber($"({global}).pivot.x"),
                        host.EvaluateNumber($"({global}).pivot.y"),
                        0, 0, 0);

                    var sheet = _contract?.sheets?.FirstOrDefault(x => x.hullStem == kv.Key);
                    string extOpts = sheet == null
                        ? "{}"
                        : BoatInteriorRigHost.ExteriorOptsLiteral(sheet.variantSize, sheet.variantStyle,
                                                                  sheet.variantRegion);
                    if (TryMeanBearingStep(host, global, extOpts, out double step))
                        bearingByStem[kv.Key] = step;
                }
            }

            _bearingByHullStem = bearingByStem;
        }

        /// <summary>
        /// The mean signed bearing step of the exterior's +X axis, per 45 degrees of <c>dir</c>.
        ///
        /// <para>Deliberately a SECOND, independent transcription of the same oracle
        /// <see cref="BoatInteriorRegistrationProbe.MeasureConvention"/> uses. A test that called the
        /// baker's own method would only prove the baker agrees with itself.</para>
        /// </summary>
        static bool TryMeanBearingStep(IRigScriptHost host, string global, string extOpts, out double mean)
        {
            mean = 0;
            if (!host.EvaluateBool($"typeof {global}.navMounts === 'function' && " +
                                   $"typeof {global}.defaultElev === 'number'")) return false;

            double se = Math.Sin(host.EvaluateNumber($"{global}.defaultElev") * Math.PI / 180.0);
            if (se < 1e-6) return false;

            var bearings = new double[8];
            for (int k = 0; k < 8; k++)
            {
                string nav = $"{global}.navMounts({k},{extOpts})";
                double dx = host.EvaluateNumber($"{nav}.star.x") - host.EvaluateNumber($"{nav}.port.x");
                double dy = -(host.EvaluateNumber($"{nav}.star.y") - host.EvaluateNumber($"{nav}.port.y")) / se;
                bearings[k] = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            }

            double sum = 0;
            for (int k = 0; k < 8; k++)
            {
                double step = bearings[(k + 1) % 8] - bearings[k];
                while (step > 180) step -= 360;
                while (step <= -180) step += 360;
                sum += step;
            }
            mean = sum / 8.0;
            return true;
        }

        static BoatInteriorKit.Contract Contract()
        {
            if (_contract == null)
                Assert.Fail($"No sheet contract at {BoatInteriorKit.ContractPath}. The sheets and their " +
                            "contract are committed art — if this fired, either the bake has not been " +
                            "run on this branch or the contract was not committed with the PNGs.");
            return _contract;
        }

        /// <summary>One case per hull that HAS a sheet, so a per-hull failure names its own boat.</summary>
        static IEnumerable<TestCaseData> ShippedHulls()
        {
            string contractAbs = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                              BoatInteriorKit.ContractPath);
            if (!File.Exists(contractAbs))
            {
                yield return new TestCaseData("(no contract)").Ignore(
                    $"No sheet contract at {BoatInteriorKit.ContractPath} — nothing to enumerate.");
                yield break;
            }

            var c = JsonUtility.FromJson<BoatInteriorKit.Contract>(File.ReadAllText(contractAbs));
            foreach (var s in c.sheets.OrderBy(s => s.hullFileStem, StringComparer.Ordinal))
                yield return new TestCaseData(s.hullStem).SetName($"Hull_{s.hullFileStem}");
        }

        static BoatInteriorKit.SheetEntry SheetFor(string hullStem)
        {
            var sheet = Contract().sheets.FirstOrDefault(s => s.hullStem == hullStem);
            Assert.NotNull(sheet, $"no contract entry for '{hullStem}'.");
            return sheet;
        }

        // ---- coverage ---------------------------------------------------------------------------

        [Test]
        public void Sheets_CoverExactlyTheClearedHulls()
        {
            var shipped = Contract().sheets.Select(s => s.hullStem).OrderBy(s => s, StringComparer.Ordinal).ToArray();

            CollectionAssert.AreEqual(_cleanHullStems, shipped,
                "the sheets and the S0 ledger's cleared set are not the same hulls.\n" +
                $"  cleared but unbaked: {string.Join(", ", _cleanHullStems.Except(shipped))}\n" +
                $"  baked but not cleared: {string.Join(", ", shipped.Except(_cleanHullStems))}\n" +
                "A cleared hull without art is a boat nobody can go below on; a sheet for an " +
                "uncleared hull is art cut from a boat whose shape was never verified.");

            // 27 = ALL of the drop. The last refusal is discharged.
            // Moved 24 → 26 → 27, each step WITH the ledger change it tracks:
            //   24 → 26  the cutaway kit's sport-fisher stamp (ebc77bac…) cleared both sport fishers,
            //            flipped on the coordinator-ruled unanimity rule (PR #660).
            //   26 → 27  the cape's RIG MERGE landed as the third sha 60d127c3… — repo main as the
            //            base, the kit's aft door and published loft re-applied, with #508's OKLCH
            //            paint and #247's washboards byte-identical through it. Her FORKED-RIG
            //            refusal is flipped to CLEAN in the ledger's dated _corrections entry.
            Assert.AreEqual(27, shipped.Length,
                $"the kit ships {shipped.Length} interior sheet sets where the intake cleared 27 hulls " +
                "of the 27 in the drop — every one. If the ledger has genuinely changed — a hull " +
                "regressed, or a new drop arrived — this number moves WITH a ledger change and an ADR " +
                "note, never on its own.");
        }

        [Test]
        public void Sheets_MatchTheInteriorDefsOneForOne()
        {
            string[] defs = AssetDatabase.FindAssets("t:BoatInteriorDef", new[] { InteriorDefFolder })
                                         .Select(AssetDatabase.GUIDToAssetPath)
                                         .Select(AssetDatabase.LoadAssetAtPath<BoatInteriorDef>)
                                         .Where(d => d != null)
                                         .Select(d => d.Id)
                                         .OrderBy(s => s, StringComparer.Ordinal)
                                         .ToArray();

            string[] fromSheets = Contract().sheets.Select(s => s.defId)
                                            .OrderBy(s => s, StringComparer.Ordinal).ToArray();

            CollectionAssert.AreEqual(defs, fromSheets,
                "the interior DEFS and the interior SHEETS name different hulls. They are built from " +
                "one kit through one ledger, so a difference means one of the two is stale — and a def " +
                "whose art is missing loads a cabin that draws nothing.");
        }

        [Test]
        public void NoRefusedHull_IsNamedAnywhereInTheKit()
        {
            var refusedStems = _ledger.Values.Where(e => !e.IsClean)
                                             .Select(e => e.HullStem)
                                             .ToArray();
            Assert.IsNotEmpty(refusedStems,
                "the S0 ledger refuses nothing at all. That is possible — upstream may have fixed both " +
                "outstanding items — but it must be a deliberate ledger change, so this fires to make " +
                "the change visible rather than letting the guard quietly become vacuous.");

            foreach (string stem in refusedStems)
            {
                Assert.IsFalse(Contract().sheets.Any(s => s.hullStem == stem),
                    $"'{stem}' is refused by the S0 ledger and has a sheet anyway. LAW: never bake for " +
                    "a refused hull.");

                Assert.IsTrue(Contract().refused.Any(r => r.hullStem == stem),
                    $"'{stem}' is refused by the ledger but the contract does not record it. A kit that " +
                    "says nothing about what it does NOT cover reads as complete.");
            }

            // And on disk: no page is FILED under a refused hull's name either.
            string folder = Path.Combine(RepoRoot, BoatInteriorKit.OutputFolder);
            if (!Directory.Exists(folder)) return;

            string[] expected = Contract().sheets.SelectMany(s => s.pages).Select(p => p.file).ToArray();
            string[] onDisk = Directory.GetFiles(folder, "*.png").Select(Path.GetFileName).ToArray();

            CollectionAssert.AreEquivalent(expected, onDisk,
                "the PNGs on disk and the pages the contract describes are not the same set. A stray " +
                "sheet is either art for a hull nobody cleared or the residue of an older bake — both " +
                "of which ship.");
        }

        // ---- registration: the property the whole kit exists for ---------------------------------

        [Test, TestCaseSource(nameof(ShippedHulls))]
        public void SheetCellAndPivot_AreTheExteriorRigsOwn(string hullStem)
        {
            var sheet = SheetFor(hullStem);

            Assert.IsTrue(_exteriorGeometryByHullStem.TryGetValue(hullStem, out RigGeometry exterior),
                $"could not run the shipped exterior rig for '{hullStem}', so there is nothing to " +
                "register against. That is a failure, not a skip: the whole claim of this art is that " +
                "it lands in the exterior's cell.");

            Assert.AreEqual(exterior.Width, sheet.cellW,
                $"'{hullStem}': the sheet's cell is {sheet.cellW} px wide but the hull's rig renders " +
                $"{exterior.Width}. The interior would composite offset from its own boat.");
            Assert.AreEqual(exterior.Height, sheet.cellH,
                $"'{hullStem}': the sheet's cell is {sheet.cellH} px tall but the hull's rig renders " +
                $"{exterior.Height}.");

            Assert.AreEqual(exterior.PivotX, sheet.pivotX, 1e-9,
                $"'{hullStem}': the sheet pivots at x={sheet.pivotX} and the hull at " +
                $"x={exterior.PivotX}. Same cell, different point — the cabin slides across the deck " +
                "as the boat turns.");
            Assert.AreEqual(exterior.PivotY, sheet.pivotY, 1e-9,
                $"'{hullStem}': the sheet pivots at y={sheet.pivotY} and the hull at y={exterior.PivotY}.");
        }

        /// <summary>
        /// The recorded handedness must still be the one the SHIPPED exterior rig measures — from the
        /// un-squashed ground-plane bearing of its own +X axis, not from a silhouette.
        ///
        /// <para><b>This is the assert that would have caught the bug this kit shipped with for one
        /// bake.</b> <c>RigAzimuthProbe</c>'s taper heuristic calls every lobster VARIANT "Clockwise",
        /// confidently, at elongations past its own warning threshold. Baked on that answer all
        /// eighteen had every facing reversed — and nothing else in the suite could see it: the cells
        /// were the right size, at the right pivot, in the right count, each one a perfectly plausible
        /// cabin. Only the sign of the bearing tells them apart, so only the sign is asserted.</para>
        /// </summary>
        [Test, TestCaseSource(nameof(ShippedHulls))]
        public void SheetConvention_IsTheExteriorsMeasuredHandedness(string hullStem)
        {
            var sheet = SheetFor(hullStem);

            Assert.IsTrue(_bearingByHullStem.TryGetValue(hullStem, out double meanStep),
                $"could not take a ground-plane bearing for '{hullStem}' — its shipped exterior rig " +
                "either would not run or exposes no navMounts() ±X pair.");

            string measured = meanStep > 0 ? "CounterClockwise" : "Clockwise";
            Assert.AreEqual(measured, sheet.convention,
                $"'{hullStem}': the sheets were baked {sheet.convention} but the shipped exterior rig " +
                $"measures {measured} ({meanStep:+0.000;-0.000}° per 45° step). Getting this wrong does " +
                "not fail — it writes every facing in reverse order, and each cell still looks right.");

            Assert.AreEqual(45.0, Math.Abs(meanStep), 0.5,
                $"'{hullStem}': the bearing steps {Math.Abs(meanStep):F3}° per 45° of dir, not 45°. " +
                "The turntable is not what this kit assumes, so the SIGN cannot be trusted either.");
        }

        [Test, TestCaseSource(nameof(ShippedHulls))]
        public void EverySprite_IsTheFullCellAtTheHullsOnePivot(string hullStem)
        {
            var sheet = SheetFor(hullStem);
            Vector2 want = sheet.NormalisedPivot;
            int seen = 0;

            foreach (var page in sheet.pages)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(page.AssetPath);
                Assert.NotNull(tex, $"'{page.AssetPath}' is not imported.");

                Assert.AreEqual(page.sheetW, tex.width,
                    $"{page.file} imported {tex.width} px wide where the bake wrote {page.sheetW}. A " +
                    "SILENT downscale — the sprite count still matches and every rect is wrong.");
                Assert.AreEqual(page.sheetH, tex.height,
                    $"{page.file} imported {tex.height} px tall where the bake wrote {page.sheetH}.");

                var sprites = AssetDatabase.LoadAllAssetsAtPath(page.AssetPath).OfType<Sprite>().ToArray();
                Assert.AreEqual(page.cellCount, sprites.Length,
                    $"{page.file} holds {sprites.Length} sprites, expected {page.cellCount}. " +
                    "spriteMode Multiple with ZERO rects reads as 'imported' everywhere else.");

                foreach (var s in sprites)
                {
                    seen++;
                    Assert.AreEqual(sheet.cellW, Mathf.RoundToInt(s.rect.width),
                        $"{page.file}/{s.name} is {s.rect.width} px wide, not the hull's full " +
                        $"{sheet.cellW} px cell. A cropped interior does not composite 1:1.");
                    Assert.AreEqual(sheet.cellH, Mathf.RoundToInt(s.rect.height),
                        $"{page.file}/{s.name} is {s.rect.height} px tall, not the hull's full " +
                        $"{sheet.cellH} px cell.");

                    var got = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
                    Assert.AreEqual(want.x, got.x, 1e-3f,
                        $"{page.file}/{s.name} pivots at {got} where the hull pivots at {want}.");
                    Assert.AreEqual(want.y, got.y, 1e-3f,
                        $"{page.file}/{s.name} pivots at {got} where the hull pivots at {want}.");
                }

                var importer = AssetImporter.GetAtPath(page.AssetPath) as TextureImporter;
                Assert.NotNull(importer, $"{page.file} has no TextureImporter.");
                Assert.AreEqual(sheet.pixelsPerMetre, importer.spritePixelsPerUnit, 1e-4f,
                    $"{page.file} imports at {importer.spritePixelsPerUnit} PPU but this hull renders " +
                    $"at {sheet.pixelsPerMetre} px/m. TWO pixel grids live in this kit — the tanker at " +
                    "16, the fleet at 32 — and PPU is how a sheet says which one it is on. At the " +
                    "wrong one a 110 m ship's cabins land at half scale, in focus, with nothing to " +
                    "catch it but the eye.");
                Assert.AreEqual(FilterMode.Point, importer.filterMode, $"{page.file} is not Point-filtered.");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression,
                    $"{page.file} is compressed — pixel art must be exact.");
                Assert.IsFalse(importer.mipmapEnabled, $"{page.file} has mipmaps.");
            }

            Assert.AreEqual(sheet.levels.Length * sheet.facings, seen,
                $"'{hullStem}' shows {seen} sprites across its pages, expected " +
                $"{sheet.levels.Length} level(s) × {sheet.facings} facings.");
        }

        // ---- the packing and the pixel grid ------------------------------------------------------

        [Test, TestCaseSource(nameof(ShippedHulls))]
        public void SheetLevels_AreTheSidecarsOwnCellLevels(string hullStem)
        {
            var sheet = SheetFor(hullStem);

            Assert.IsTrue(_sidecarLevelsByHullStem.TryGetValue(hullStem, out string[] declared),
                $"no interior sidecar found for '{hullStem}'.");

            CollectionAssert.AreEqual(declared, sheet.levels,
                $"'{hullStem}' baked levels [{string.Join(", ", sheet.levels)}] where its sidecar " +
                $"declares cell.levels [{string.Join(", ", declared)}]. WALKABLE is NOT this list — it " +
                "is the game's standable planes and carries weather decks the interior rig never draws.");
        }

        /// <summary>
        /// <b>The pixels on disk are the rig's own, in the cell the contract says they are.</b>
        ///
        /// <para>Everything else in this file checks the sheet's SHAPE — its size, its count, its
        /// pivot, its PPU. All of that stays green when the cells are correct pictures in the wrong
        /// places, which is the failure paging introduces and the failure a reversed turntable
        /// introduces. This is the only assert that opens the PNG and compares it to a fresh render.</para>
        ///
        /// <para>Two hulls, chosen for where the two failures hide: the side dragger PAGES (and her
        /// pages split a level, so page 1 begins mid-hull), and a lobster VARIANT is drawn through the
        /// variant-object path that silently renders a different boat when it is handed a string.</para>
        ///
        /// <para>Decoded with <c>LoadImage</c> from the file rather than read off the imported
        /// texture, so nothing here depends on <c>isReadable</c> — the same route
        /// <c>PuntGoldenMasterTests</c> takes.</para>
        /// </summary>
        [Test]
        public void SheetCells_AreTheRigsOwnPixels()
        {
            AssertCellsMatchTheRig("sideDraggerIsoRig",
                                   new[] { ("bridge", 2), ("house", 1), ("below", 5) });
            AssertCellsMatchTheRig("lobsterBoatVariantsIsoRig.offshore_open_fundy",
                                   new[] { ("house", 3), ("cuddy", 6) });
        }

        static void AssertCellsMatchTheRig(string hullStem, (string level, int facing)[] cells)
        {
            var sheet = SheetFor(hullStem);

            IReadOnlyList<BoatInteriorRigHost.HullBinding> table;
            using (var scratch = RigScriptHostFactory.Create())
                table = BoatInteriorRigHost.ReadHullTable(scratch);

            Assert.IsTrue(BoatInteriorRigHost.TryBind(table, hullStem, out var binding, out string bindError),
                          bindError);

            using var host = RigScriptHostFactory.Create();
            BoatInteriorRigHost.Install(host, binding);

            string variantJs = BoatInteriorRigHost.VariantLiteral(
                sheet.variantSize, sheet.variantStyle, sheet.variantRegion);
            var convention = (AzimuthConvention)Enum.Parse(typeof(AzimuthConvention), sheet.convention);

            var decoded = new Dictionary<string, Color32[]>(StringComparer.Ordinal);

            foreach (var (level, facing) in cells)
            {
                int levelIndex = Array.IndexOf(sheet.levels, level);
                Assert.GreaterOrEqual(levelIndex, 0, $"'{hullStem}' has no level '{level}'.");

                int flat = sheet.CellIndex(levelIndex, facing);
                var page = sheet.pages.FirstOrDefault(
                    p => p.firstCell <= flat && flat < p.firstCell + p.cellCount);
                Assert.NotNull(page, $"no page of '{hullStem}' carries cell {flat}.");

                if (!decoded.TryGetValue(page.file, out Color32[] pixels))
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false);
                    Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(Path.Combine(RepoRoot, page.AssetPath)),
                                                markNonReadable: false),
                                  $"could not decode {page.file}.");
                    Assert.AreEqual(page.sheetW, tex.width, $"{page.file} is not the size the contract records.");
                    Assert.AreEqual(page.sheetH, tex.height, $"{page.file} is not the size the contract records.");
                    pixels = tex.GetPixels32();
                    decoded[page.file] = pixels;
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                double dir = RigBaker.DirForCell(facing, sheet.facings, convention);
                byte[] rgba = host.EvaluateBytes(BoatInteriorRigHost.RenderExpression(
                    sheet.interiorHullKey, level, variantJs,
                    dir.ToString("R", CultureInfo.InvariantCulture)));

                Assert.AreEqual(sheet.cellW * sheet.cellH * 4, rgba.Length,
                    $"'{hullStem}' {level}/d{facing} rendered {rgba.Length} bytes. The interior rig " +
                    "returns FOUR bytes when it cannot resolve the hull.");

                BoatInteriorKit.Slot(flat - page.firstCell, page.columns, out int col, out int rowFromTop);

                int differing = 0, firstX = -1, firstY = -1;
                for (int y = 0; y < sheet.cellH; y++)
                {
                    // The rig buffer is TOP-origin row-major; Unity's Color32[] is BOTTOM-origin. The
                    // flip happens exactly once, here, as it does in RigBaker.Blit.
                    int unityY = page.sheetH - 1 - (rowFromTop * sheet.cellH + y);
                    int dstRow = unityY * page.sheetW + col * sheet.cellW;
                    int srcRow = y * sheet.cellW * 4;

                    for (int x = 0; x < sheet.cellW; x++)
                    {
                        Color32 got = pixels[dstRow + x];
                        int s = srcRow + x * 4;
                        if (got.r == rgba[s] && got.g == rgba[s + 1] &&
                            got.b == rgba[s + 2] && got.a == rgba[s + 3]) continue;
                        if (differing == 0) { firstX = x; firstY = y; }
                        differing++;
                    }
                }

                Assert.AreEqual(0, differing,
                    $"'{hullStem}' {level}/d{facing} — the cell at {page.file} (col {col}, row " +
                    $"{rowFromTop}) differs from a fresh render in {differing} px, first at " +
                    $"({firstX},{firstY}). Either the sheet is stale, or the cell is in the wrong " +
                    "slot, or it was baked under a different handedness. Every one of those still " +
                    "leaves a sheet that looks entirely correct.");
            }
        }

        [Test]
        public void EveryPage_FitsTheImportCap()
        {
            foreach (var sheet in Contract().sheets)
                foreach (var page in sheet.pages)
                {
                    int needed = Mathf.NextPowerOfTwo(Mathf.Max(page.sheetW, page.sheetH));
                    Assert.LessOrEqual(needed, BoatInteriorKit.ImportSizeCap,
                        $"{page.file} is {page.sheetW}×{page.sheetH} and needs a {needed} px import, " +
                        $"past the kit's {BoatInteriorKit.ImportSizeCap} cap. Between the cap and " +
                        "Unity's hard 4096 a sheet bakes fine and imports SILENTLY downscaled.");
                }
        }

        [Test]
        public void SheetPixelsPerMetre_MatchesTheDef()
        {
            foreach (var sheet in Contract().sheets)
            {
                string assetPath = $"{InteriorDefFolder}/" +
                                   $"{BoatInteriorHullResolver.AssetName(sheet.hullFileStem)}.asset";
                var def = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(assetPath);
                Assert.NotNull(def, $"no BoatInteriorDef at {assetPath} for '{sheet.hullStem}'.");

                Assert.AreEqual(def.PixelsPerMetre, sheet.pixelsPerMetre,
                    $"'{sheet.hullStem}': the def says {def.PixelsPerMetre} px/m and the sheet says " +
                    $"{sheet.pixelsPerMetre}. The def is what the runtime scales its geometry by and " +
                    "the sheet is what it draws — they cannot disagree.");

                Assert.AreEqual(def.CellPixels.x, sheet.cellW,
                    $"'{sheet.hullStem}': the def's cell is {def.CellPixels.x} px wide, the sheet's " +
                    $"{sheet.cellW}.");
                Assert.AreEqual(def.CellPixels.y, sheet.cellH,
                    $"'{sheet.hullStem}': the def's cell is {def.CellPixels.y} px tall, the sheet's " +
                    $"{sheet.cellH}.");
                Assert.AreEqual(def.PivotPixels.x, sheet.pivotX,
                    $"'{sheet.hullStem}': the def pivots at x={def.PivotPixels.x}, the sheet at " +
                    $"x={sheet.pivotX}.");
                Assert.AreEqual(def.PivotPixels.y, sheet.pivotY,
                    $"'{sheet.hullStem}': the def pivots at y={def.PivotPixels.y}, the sheet at " +
                    $"y={sheet.pivotY}.");
            }
        }

        [Test]
        public void Contract_RecordsTheRendererThatShipped()
        {
            string abs = Path.Combine(RepoRoot, BoatInteriorKit.KitFolder, BoatInteriorKit.InteriorRigFileName);
            Assert.IsTrue(File.Exists(abs), $"{BoatInteriorKit.InteriorRigFileName} is missing from the kit.");

            // MatchRigHash, not a raw byte compare: the contract was stamped from one machine's
            // checkout and this assert runs on another's, and CRLF↔LF is the one difference between
            // those that provably cannot move a vertex. Anything else is still a refusal.
            var match = DeckSidecarReader.MatchRigHash(
                File.ReadAllBytes(abs), Contract().interiorRigSha256, out string actual);
            Assert.AreNotEqual(RigHashMatch.None, match,
                $"the contract pins a renderer ({Contract().interiorRigSha256}) that is not the one " +
                $"in the kit ({actual}, and no line-ending form of it matches either). Either the kit " +
                "was revised without a re-bake, or the sheets were baked from a file that is no longer " +
                "there — and every sheet's provenance claim is wrong either way.");
        }

        [Test]
        public void EverySheet_RecordsTheOptionsItWasBakedAt()
        {
            foreach (var sheet in Contract().sheets)
            {
                Assert.AreEqual(BoatInteriorKit.BakedDoorOpen, sheet.doorOpen, 1e-6f,
                    $"'{sheet.hullStem}' records doorOpen {sheet.doorOpen}. The door is an 8-frame cue " +
                    "and this kit bakes only its resting fraction — a sheet at another fraction is one " +
                    "frame of a cue nobody is playing yet.");
                Assert.AreEqual(BoatInteriorKit.BakedWeather, sheet.weather, 1e-6f,
                    $"'{sheet.hullStem}' records weather {sheet.weather}, not the kit's " +
                    $"{BoatInteriorKit.BakedWeather}.");
                Assert.AreEqual(BoatInteriorKit.Facings, sheet.facings,
                    $"'{sheet.hullStem}' bakes {sheet.facings} facings, not the kit's " +
                    $"{BoatInteriorKit.Facings}.");
                Assert.IsNotEmpty(sheet.convention,
                    $"'{sheet.hullStem}' records no azimuth convention. It is the correction every " +
                    "cell was written under; unrecorded, a re-bake cannot be checked against this one.");
            }
        }

        [Test]
        public void VariantAwareHulls_CarryTheirWholeTriple()
        {
            foreach (var sheet in Contract().sheets)
            {
                bool any = sheet.variantSize.Length > 0 || sheet.variantStyle.Length > 0 ||
                           sheet.variantRegion.Length > 0;
                if (!any) continue;

                Assert.IsTrue(sheet.variantSize.Length > 0 && sheet.variantStyle.Length > 0 &&
                              sheet.variantRegion.Length > 0,
                    $"'{sheet.hullStem}' carries a partial variant " +
                    $"(size='{sheet.variantSize}' style='{sheet.variantStyle}' region='{sheet.variantRegion}'). " +
                    "The rig resolves each field independently against its own table with a fall-through " +
                    "default, so ONE missing field bakes a different boat under this name with no error.");
            }
        }
    }
}
