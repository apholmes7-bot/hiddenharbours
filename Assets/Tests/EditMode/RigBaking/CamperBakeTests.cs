using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ⭐ <b>THE CAMPER BAKE — is what shipped what the kit describes, and do the two measured traps
    /// still get caught?</b>
    ///
    /// <para><see cref="CamperIsoKitProbeTests"/> settled that the RIG is faithful to its README. This
    /// settles the next question: that the SHEETS are faithful to the rig, that the guards which stand
    /// between the two still fire, and that the one thing the bake deliberately gives up — the
    /// Clipper's awning on its enter sheet — is exactly the thing it gave up and nothing more.</para>
    ///
    /// <para>The kit ships <b>no reference sheets</b>, so there is no committed image to diff a bake
    /// against. The README's own measured numbers remain the only external control there is, which is
    /// why the crop arithmetic below is stated against published figures rather than against a
    /// golden master.</para>
    /// </summary>
    public class CamperBakeTests
    {
        const string G = CamperKit.GlobalName;

        static IRigScriptHost NewHost(out RigGeometry geo)
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            geo = RigCatalog.Install(host, RigCatalog.Get(CamperKit.RigKey));
            return host;
        }

        static CamperKit.Contract Contract()
        {
            string abs = Path.Combine(RigCatalog.RepoRoot, CamperKit.ContractPath);
            FileAssert.Exists(abs,
                "No camper contract on disk. It is written by the bake and read by the slicer, so a " +
                "missing one means the sheets in this branch were never baked by this code.");
            return JsonUtility.FromJson<CamperKit.Contract>(File.ReadAllText(abs));
        }

        // =============================================================================================
        //  1. REGISTRATION AND THE TURNTABLE
        // =============================================================================================

        [Test]
        public void Catalog_registers_the_camper_counter_clockwise_and_standalone()
        {
            var e = RigCatalog.Get(CamperKit.RigKey);

            Assert.That(e.GlobalName, Is.EqualTo("CamperIso"));
            Assert.That(e.ScriptPath, Is.EqualTo(DwellingRigFleet.CamperRigPath),
                "the catalog and DwellingRigFleet must name the same file, or the coverage law is " +
                "guarding a rig the baker does not use");
            Assert.That(e.DeclaredConvention, Is.EqualTo(AzimuthConvention.CounterClockwise));
            Assert.That(e.Prerequisites, Is.Empty,
                "the kit's README is explicit that the rig is self-contained — no deps, no isoSolid.js");
        }

        /// <summary>
        /// 🔴 <b>The measurement the bake refuses on.</b> Getting handedness wrong does not fail — it
        /// writes every facing in reverse order, which is a plausible picture and has shipped defects
        /// in five kits here. Re-measured from the live rig on every run.
        /// </summary>
        [Test]
        public void TheMeasuredConventionIsCounterClockwise_AndTheCatalogAgrees()
        {
            using IRigScriptHost host = NewHost(out RigGeometry geo);

            var probe = CamperRigAzimuthProbe.Measure(
                host, G, CamperSheetBaker.OptsJs("clipper", awning: false, swing: 0),
                geo.Width, geo.Height, geo.PivotX);

            Assert.That(probe.Convention,
                        Is.EqualTo(RigCatalog.Get(CamperKit.RigKey).DeclaredConvention),
                        probe.Report);
            Assert.That(probe.HitchOffsetPx, Is.LessThan(-CamperRigAzimuthProbe.MinHitchOffsetPx),
                "cell 2 is labelled 'E' and the coupler should project WEST of the pivot there.\n" +
                probe.Report);
        }

        /// <summary>
        /// ⚠ Why this kit needed a third azimuth probe rather than reusing the building one: a house's
        /// door is on its <c>+Y</c> face, so the door IS the heading — but a camper's door is on the
        /// CURB side. Measured here, so the day a drop moves it this reasoning is re-checked instead of
        /// merely believed.
        /// </summary>
        [Test]
        public void TheDoorCannotAnswerTheHandednessQuestion_WhichIsWhyTheHitchDoes()
        {
            using IRigScriptHost host = NewHost(out RigGeometry geo);

            foreach (string variant in CamperKit.Variants)
            {
                double door = host.EvaluateNumber($"{G}.anchors(2,{{variant:'{variant}'}}).door.x")
                              - geo.PivotX;
                double hitch = host.EvaluateNumber($"{G}.anchors(2,{{variant:'{variant}'}}).hitch.x")
                               - geo.PivotX;

                Assert.That(Mathf.Abs((float)door),
                            Is.LessThan((float)BuildingRigAzimuthProbe.MinDoorOffsetPx),
                    $"{variant}'s door is now {door:F1} px off the pivot at a quarter turn, over " +
                    $"BuildingRigAzimuthProbe's own {BuildingRigAzimuthProbe.MinDoorOffsetPx} px floor. " +
                    "If the door has moved onto the heading face, the building probe could read this " +
                    "rig after all and CamperRigAzimuthProbe may be redundant — check before deleting.");

                Assert.That(Mathf.Abs((float)hitch),
                            Is.GreaterThan((float)CamperRigAzimuthProbe.MinHitchOffsetPx),
                    $"{variant}'s hitch is only {hitch:F1} px off the pivot — the probe's own guard " +
                    "would refuse, and it would be right to.");
            }
        }

        // =============================================================================================
        //  2. THE SABOTAGE ARMS — both traps, driven through the baker's real code path
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>Sabotage: hand the baker a compass string.</b> <c>render(dir, opts)</c> takes an
        /// INTEGER INDEX; <c>camBasis</c> computes <c>dir·π/4</c>, so <c>'N'</c> makes the angle NaN
        /// and the call returns a fully transparent cell <b>without throwing</b>. The kit's README
        /// talks in compass names from end to end, so this is the mistake a baker actually makes, and
        /// its whole cost is that nothing says so.
        ///
        /// <para>Driven through <see cref="CamperSheetBaker.RenderCell"/> — the same method every real
        /// cell goes through — so this cannot pass while the production path has lost the guard.</para>
        /// </summary>
        [Test]
        public void Sabotage_ACompassStringForDir_TripsTheInkGuardInsteadOfBakingAnEmptyCell()
        {
            using IRigScriptHost host = NewHost(out RigGeometry geo);
            string opts = CamperSheetBaker.OptsJs("bantam", awning: false, swing: 0);

            // The control: the integer form draws a real camper through the very same call.
            byte[] honest = CamperSheetBaker.RenderCell(host, G, geo, "0", opts, "control");
            Assert.That(honest.Length, Is.EqualTo(geo.Width * geo.Height * 4));

            var e = Assert.Throws<System.InvalidOperationException>(
                () => CamperSheetBaker.RenderCell(host, G, geo, "'N'", opts, "sabotage"),
                "render('N') was accepted. Either the rig now takes compass names — in which case " +
                "simplify the baker and delete this test — or the ink guard has been removed and the " +
                "bake will happily write eight transparent sheets.");

            Assert.That(e.Message, Does.Contain("0 opaque px"),
                "the guard fired, but not on an empty cell — so this is no longer measuring the trap.");
        }

        /// <summary>
        /// 🔴 <b>Sabotage: bake the door cue with the awning on.</b> The Clipper fits one by default,
        /// on the curb side, directly over its own door. Measured worst-facing swing 0→1 repaint:
        /// 1641 px unawninged, 546 px with it. A cue that small is not a subtle cue — it is a bug that
        /// looks deliberate, and it would ship without an error.
        /// </summary>
        [Test]
        public void Sabotage_BakingTheEnterCueWithTheAwningOn_TripsTheCueGuard()
        {
            using IRigScriptHost host = NewHost(out RigGeometry geo);
            var build = new CamperKit.Build("clipper", CamperKit.Role.Enter);
            var clock = new Stopwatch();

            // The control: as the kit actually bakes it, the cue reads and nothing throws.
            var honest = CamperSheetBaker.RenderBuild(host, G, geo, build,
                                                      AzimuthConvention.CounterClockwise, clock);
            Assert.That(honest.Awning, Is.False, "an Enter build must resolve unawninged");
            Assert.That(honest.MaxCueDeltaPx,
                        Is.GreaterThan(CamperSheetBaker.MinCueDeltaPx),
                        "the unoccluded Clipper cue measured 1641 px when this was written");

            var e = Assert.Throws<System.InvalidOperationException>(
                () => CamperSheetBaker.RenderBuild(host, G, geo, build,
                                                   AzimuthConvention.CounterClockwise, clock,
                                                   awningOverride: true),
                "an awninged enter sheet was accepted. Either the awning stopped occluding the door — " +
                "in which case the split in CamperKit.Build.AwningOverride is no longer needed — or " +
                "the cue guard has been lost.");

            Assert.That(e.Message, Does.Contain("door cue barely moves"));
        }

        // =============================================================================================
        //  3. THE CROP, AGAINST THE README'S PUBLISHED ARITHMETIC
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The only external control this kit offers.</b> The README publishes a "painted union,
        /// at rest" for each length, and a per-facing crop box for each of the sixteen facings. The
        /// union of those boxes and the published union are two separate claims in the same document,
        /// and the baked cell has to satisfy both:
        ///
        /// <code>
        ///   bantam  x: 82 (E) … 301 (W) → 301 − 82 + 1 = 220     published union 220 × 159 ✓
        ///           y: 117 (S) … 275 (S) → 275 − 117 + 1 = 159
        ///   clipper x: 31 (E) … 352 (W) → 352 − 31 + 1 = 322     published union 322 × 231 ✓
        ///           y: 78 (S) … 308 (S) → 308 − 78 + 1 = 231
        /// </code>
        ///
        /// <para>⚠ The cell is unioned over BOTH roles of a variant, so a cue frame that reached
        /// outside the at-rest silhouette would legitimately make it bigger than this — and that is
        /// worth being told about rather than tolerated, because it would mean the door or its step
        /// swings past the parked outline.</para>
        /// </summary>
        [TestCase("bantam", 82, 117, 301, 275, 220, 159)]
        [TestCase("clipper", 31, 78, 352, 308, 322, 231)]
        public void TheBakedCellIsTheReadmesPublishedUnion(string variant, int minX0, int minY0,
                                                           int maxX1, int maxY1,
                                                           int publishedW, int publishedH)
        {
            Assert.That(maxX1 - minX0 + 1, Is.EqualTo(publishedW),
                "the README's own two claims disagree before the bake is even consulted");
            Assert.That(maxY1 - minY0 + 1, Is.EqualTo(publishedH));

            var contract = Contract();

            foreach (var sheet in contract.sheets.Where(s => s.variant == variant))
            {
                Assert.That(sheet.cellW, Is.EqualTo(publishedW),
                    $"{sheet.key} baked a {sheet.cellW} px cell; the README publishes a {publishedW} px " +
                    "painted union at rest, and the per-facing boxes agree with it.");
                Assert.That(sheet.cellH, Is.EqualTo(publishedH), sheet.key);

                Assert.That(sheet.cropX, Is.EqualTo(minX0),
                    $"{sheet.key} crops from x={sheet.cropX}; the leftmost ink over the eight " +
                    $"published boxes is x={minX0} (at E).");
                Assert.That(sheet.cropY, Is.EqualTo(minY0), sheet.key);

                // The pivot moves with the crop, and nothing else moves it. 192,214 is the rig's own.
                Assert.That(sheet.pivotX, Is.EqualTo(192 - minX0), sheet.key);
                Assert.That(sheet.pivotY, Is.EqualTo(214 - minY0), sheet.key);
            }
        }

        /// <summary>
        /// The drawbar rides BELOW the pivot row — 61 px on the Bantam, 94 on the Clipper — and after
        /// cropping that has to still be true, or the coupler has been trimmed off the sheet.
        /// </summary>
        [TestCase("bantam", 61)]
        [TestCase("clipper", 94)]
        public void TheCroppedCellStillHoldsTheWholeDrawbar(string variant, int overhang)
        {
            var sheet = Contract().sheets.First(s => s.variant == variant && s.role == "rest");

            Assert.That(sheet.cellH - sheet.pivotY - 1, Is.GreaterThanOrEqualTo(overhang),
                $"{variant} keeps only {sheet.cellH - sheet.pivotY - 1} px below the pivot row but the " +
                $"A-frame reaches {overhang} px at S. The pivot is the ground-centre of the BODY, " +
                "tongue excluded, so those rows are the drawbar resting on the dirt — cropping them " +
                "away would saw the coupler off.");
        }

        // =============================================================================================
        //  4. THE TWO SHEETS OF A LENGTH ARE INTERCHANGEABLE, AND THE AWNING IS THE ONLY DIFFERENCE
        // =============================================================================================

        /// <summary>
        /// ⭐ A camper must not shift when its DOOR opens, which is the same rule that says a building
        /// must not shift when it turns. The bake unions its crop over both roles of a length so this
        /// holds by construction; pinned here because a future baker that measured each sheet
        /// separately would pass every other test in this file.
        /// </summary>
        [Test]
        public void RestAndEnterOfALengthShareACellAndAPivot()
        {
            var contract = Contract();

            foreach (string variant in CamperKit.Variants)
            {
                var sheets = contract.sheets.Where(s => s.variant == variant).ToArray();
                Assert.That(sheets.Length, Is.EqualTo(2), $"{variant} should have a rest and an enter");

                Assert.That(sheets.Select(s => (s.cellW, s.cellH, s.pivotX, s.pivotY)).Distinct().Count(),
                            Is.EqualTo(1),
                    $"{variant}'s sheets disagree about the cell: " +
                    string.Join(" / ", sheets.Select(s => $"{s.key} {s.cellW}×{s.cellH} @{s.pivotX},{s.pivotY}")) +
                    ". A presenter cutting from the parked frame to the cue would jump by the " +
                    "difference, and every individual sheet would still look correct.");
            }
        }

        /// <summary>
        /// ⭐ <b>The split's whole cost, measured on both sides.</b> The Bantam fits no awning either
        /// way, so its rest sheet and its enter sheet's first frame must be <b>pixel-identical</b> —
        /// which is what proves the two roles differ by the awning ALONE and not by some other
        /// resolved option drifting apart. The Clipper's must differ, because that is the deliberate
        /// choice, and if it ever stops differing the enter sheet has quietly grown an awning.
        /// </summary>
        [Test]
        public void OnlyTheAwningSeparatesARestSheetFromItsEnterSheetsFirstFrame()
        {
            using IRigScriptHost host = NewHost(out RigGeometry geo);
            var clock = new Stopwatch();

            foreach (string variant in CamperKit.Variants)
            {
                var rest = CamperSheetBaker.RenderBuild(
                    host, G, geo, new CamperKit.Build(variant, CamperKit.Role.Rest),
                    AzimuthConvention.CounterClockwise, clock);
                var enter = CamperSheetBaker.RenderBuild(
                    host, G, geo, new CamperKit.Build(variant, CamperKit.Role.Enter),
                    AzimuthConvention.CounterClockwise, clock);

                bool awninged = rest.Awning;
                Assert.That(enter.Awning, Is.False);

                for (int facing = 0; facing < CamperKit.Facings; facing++)
                {
                    int differing = Differing(rest.Cells[facing],
                                              enter.Cells[facing * CamperKit.CueFrames]);

                    if (awninged)
                        Assert.That(differing, Is.GreaterThan(0),
                            $"{variant} facing {facing}: the rest sheet resolves awninged but is " +
                            "pixel-identical to the unawninged cue. The awning has stopped drawing, " +
                            "and the rest sheet is no longer the variant's default build.");
                    else
                        Assert.That(differing, Is.Zero,
                            $"{variant} facing {facing}: {differing} px differ between the parked " +
                            "frame and the cue's first frame, on a length that fits no awning either " +
                            "way. Something other than the awning now separates the two roles — a " +
                            "presenter cutting between them would flicker.");
                }
            }
        }

        static int Differing(byte[] a, byte[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i += 4)
                if (a[i] != b[i] || a[i + 1] != b[i + 1] ||
                    a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]) n++;
            return n;
        }

        // =============================================================================================
        //  5. WHAT ACTUALLY LANDED ON DISK
        // =============================================================================================

        [Test]
        public void EveryPlannedBuildHasAContractEntryAndNothingElseDoes()
        {
            var planned = CamperKit.Builds.Select(b => b.Key).OrderBy(k => k).ToArray();
            var onFile = Contract().sheets.Select(s => s.key).OrderBy(k => k).ToArray();

            CollectionAssert.AreEqual(planned, onFile,
                "CamperKit.Builds and the contract have drifted apart. The contract is written BY the " +
                "bake, so this means the sheets on disk are from an older build table — re-bake " +
                "rather than editing the JSON.");
        }

        /// <summary>
        /// The sheets imported at NATIVE resolution with the full rect count and the contract's pivot.
        ///
        /// <para><b>spriteMode Multiple is not the same as sliced</b> — a fresh import is Multiple with
        /// ZERO rects, and every downstream <c>LoadAllAssetsAtPath</c> returns nothing while the
        /// importer reports exactly what was asked for. This is the check that tells them apart.</para>
        /// </summary>
        [Test]
        public void EverySheetIsImportedAtNativeSize_Sliced_AndCarriesTheContractPivot()
        {
            var contract = Contract();
            var problems = new List<string>();

            foreach (var sheet in contract.sheets)
            {
                string path = $"{CamperKit.OutputFolder}/{sheet.key}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                if (tex == null)
                {
                    Assert.Ignore($"{path} did not load. These sheets are LFS-backed, so a " +
                                  "pointer-only checkout cannot run this — `git lfs pull`. Ignored " +
                                  "rather than passed, so an unfetched checkout cannot look green.");
                    return;
                }

                if (tex.width != sheet.sheetW || tex.height != sheet.sheetH)
                    problems.Add($"{sheet.key}: imported {tex.width}×{tex.height}, contract baked " +
                                 $"{sheet.sheetW}×{sheet.sheetH} — a SILENT downscale invalidates " +
                                 "every rect on the sheet.");

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                int expected = sheet.columns * sheet.rows;
                if (sprites.Length != expected)
                    problems.Add($"{sheet.key}: {sprites.Length} sprites, expected {expected}.");

                var want = sheet.NormalisedPivot;
                foreach (var s in sprites)
                {
                    var got = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
                    if (Mathf.Abs(got.x - want.x) > 1e-3f || Mathf.Abs(got.y - want.y) > 1e-3f)
                    {
                        problems.Add($"{sheet.key}/{s.name}: pivot {got}, contract says {want}.");
                        break;
                    }
                }
            }

            CollectionAssert.IsEmpty(problems, string.Join("\n  ", problems));
        }

        /// <summary>
        /// ⭐ <b>Asking for "the camper at facing N" the way this repo's kits are addressed — by the
        /// <c>_d&lt;facing&gt;</c> suffix — finds exactly ONE sprite per length.</b>
        ///
        /// <para>Every kit here bakes to <c>stem…_d0 … _d7</c> (the fuel kit's
        /// <c>jerry_s20_gas_f2_d3</c>), so a suffix match is the idiom a consumer reaches for. This
        /// kit has two sheets per length, and if both ended in the facing there would be seven
        /// candidates for each one — the parked frame and five mid-swing cue frames, resolved by
        /// whatever order the asset database happened to return. A camper drawn with its door half
        /// open looks finished and is wrong, which is worse to find than one that is obviously not
        /// drawn yet.</para>
        ///
        /// <para>So the <c>rest</c> sheet drops the frame index and the <c>enter</c> sheet keeps it,
        /// and the parked frame is the unique answer. Pinned because it is a property of the NAMES,
        /// which nothing else in this file would notice changing.</para>
        /// </summary>
        [Test]
        public void AFacingSuffixMatchesExactlyOneSpritePerLength_AndItIsTheParkedFrame()
        {
            var contract = Contract();

            foreach (string variant in CamperKit.Variants)
                for (int facing = 0; facing < CamperKit.Facings; facing++)
                {
                    string suffix = "_d" + facing;

                    var hits = contract.sheets
                        .Where(s => s.variant == variant)
                        .SelectMany(s => Enumerable.Range(0, s.rows).SelectMany(
                            f => Enumerable.Range(0, s.columns).Select(n => (s, name: s.SpriteName(f, n)))))
                        .Where(h => h.name.EndsWith(suffix, System.StringComparison.Ordinal))
                        .ToArray();

                    Assert.That(hits.Length, Is.EqualTo(1),
                        $"'{variant}' facing {facing} is matched by {hits.Length} sprite names " +
                        $"({string.Join(", ", hits.Select(h => h.name))}). Exactly one of this " +
                        "length's cells may end in a facing, and it has to be the parked one.");

                    Assert.That(hits[0].s.role, Is.EqualTo("rest"),
                        $"the unique '{suffix}' match for {variant} is on the " +
                        $"'{hits[0].s.role}' sheet ({hits[0].name}), not the parked one.");
                }
        }

        /// <summary>
        /// Every sheet fits under the kit's own import cap. ⚠ A sheet between this cap and Unity's hard
        /// 4096 BAKES FINE and then imports silently downscaled with the sprite count still correct,
        /// which is why the number is checked rather than assumed to be Unity's.
        /// </summary>
        [Test]
        public void NoSheetNeedsAnImportOverTheKitsCap()
        {
            foreach (var s in Contract().sheets)
            {
                int needed = Mathf.NextPowerOfTwo(Mathf.Max(s.sheetW, s.sheetH));
                Assert.That(needed, Is.LessThanOrEqualTo(CamperKit.ImportSizeCap),
                    $"{s.key} is {s.sheetW}×{s.sheetH}, needing a {needed} px import. Reduce " +
                    "CamperKit.CueFrames rather than lifting the cap.");
            }
        }

        // =============================================================================================
        //  6. THE COVERAGE LAW NOW SAYS BAKED — AND THERE ARE SHEETS BEHIND THAT
        // =============================================================================================

        /// <summary>
        /// ⭐ <c>DwellingRigFleetTests</c> adjudicates baked-XOR-excused. This adjudicates the other
        /// half: that <see cref="DwellingRigFleet.Baked"/> is a claim with files behind it, rather than
        /// a word someone moved between two lists.
        /// </summary>
        [Test]
        public void EveryDwellingCalledBaked_HasBothOfItsSheetsOnDisk()
        {
            CollectionAssert.AreEquivalent(CamperKit.Variants.ToArray(), DwellingRigFleet.Baked.ToArray(),
                "the camper's lengths and the dwellings called baked have drifted apart");

            Assert.That(DwellingRigFleet.NotBaked, Is.Empty,
                "a dwelling is excused from baking, but the camper is the only dwelling in the repo " +
                "and both of its lengths now have sheets. A leftover excuse goes on explaining a " +
                "blocker that was cleared.");

            foreach (string key in DwellingRigFleet.Baked)
                foreach (CamperKit.Role role in new[] { CamperKit.Role.Rest, CamperKit.Role.Enter })
                {
                    string rel = $"{CamperKit.OutputFolder}/{new CamperKit.Build(key, role).Key}.png";
                    FileAssert.Exists(Path.Combine(RigCatalog.RepoRoot, rel),
                        $"DwellingRigFleet calls '{key}' baked but {rel} is not there.");
                }
        }
    }
}
