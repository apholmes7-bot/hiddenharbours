using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The fishing kit bake path (Rod Fishing v2 wave 3 — fish / bobber / rod), proven end-to-end
    /// WITHOUT any committed sheet: everything here runs the V8 host CPU-side and decodes PNGs via
    /// <c>Texture2D.LoadImage</c>, both established as CI-safe (null graphics device) by
    /// <c>RigBakerTests</c>/<c>CharacterRigBakeTests</c>. The 63 kit sheets themselves are baked on
    /// the owner's machine (Hidden Harbours ▸ Art ▸ Bake Fishing Kit) — these tests are what make
    /// that bake trustworthy before it runs, and <c>FishingKitSheetSliceTests</c> takes over the
    /// moment the PNGs land.
    /// </summary>
    public class FishingKitBakeTests
    {
        /// <summary>
        /// The kit's stated shapes, restated here ON PURPOSE rather than read from the rigs:
        /// these assert the RIGS agree with the drop's contract (README, PR #258) before any
        /// sheet exists, while the slice tests assert the PNGs agree with it after.
        /// </summary>
        static readonly string[] SpeciesSpec = { "cod", "haddock", "pollock", "mackerel" };

        static readonly (string state, int frames)[] FishStateSpec =
        {
            ("swim", 4), ("dart", 2), ("thrash", 4), ("shadow", 2),   // water anims (AORDER)
            ("deck", 4), ("gill", 2), ("tail", 2), ("cradle", 2),     // dry rests (RESTS)
        };

        static readonly (string state, int frames)[] BobberStateSpec =
        {
            ("float", 4), ("nibble", 4), ("strike", 4), ("fly", 2),
        };

        static readonly string[] RodTierSpec = { "cane", "coast", "deep" };

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        // ---- geometry installs (no DIRS, no ROCK — legitimate rig shapes) -----------------------

        [Test]
        public void FishRig_Installs_WithFishPivot_AndNoDirsDeclared()
        {
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("fish"));

            Assert.AreEqual(64, geo.Width);
            Assert.AreEqual(64, geo.Height);
            Assert.AreEqual(32.0, geo.PivotX, 1e-9);
            Assert.AreEqual(38.0, geo.PivotY, 1e-9, "pivot = THE WATER-SURFACE POINT, (32,38) top-left");
            Assert.AreEqual(0, geo.NativeDirs,
                "FishIso declares no DIRS global — the 8 headings are the ADR-0006 recipe, " +
                "supplied by FishingKitBaker.Dirs, and Install must report 0, not throw");
            Assert.AreEqual(0, geo.RockFrames);
            Assert.AreEqual(40.0, geo.DefaultElevation, 1e-9);
        }

        [Test]
        public void RodRig_Installs_WithGripPivot()
        {
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("rod"));

            Assert.AreEqual(112, geo.Width);
            Assert.AreEqual(112, geo.Height);
            Assert.AreEqual(56.0, geo.PivotX, 1e-9);
            Assert.AreEqual(72.0, geo.PivotY, 1e-9, "pivot = THE GRIP CENTRE, (56,72) top-left");
            Assert.AreEqual(0, geo.NativeDirs);
        }

        [Test]
        public void BobberRig_Installs_WithWaterlinePivot_AndNoCameraAtAll()
        {
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("bobber"));

            Assert.AreEqual(16, geo.Width);
            Assert.AreEqual(22, geo.Height);
            Assert.AreEqual(8.0, geo.PivotX, 1e-9);
            Assert.AreEqual(12.0, geo.PivotY, 1e-9, "pivot = THE WATERLINE POINT, (8,12) top-left");
            Assert.AreEqual(0, geo.NativeDirs, "a state sprite — no direction axis");
            Assert.AreEqual(0.0, geo.DefaultElevation, 1e-9,
                "hand-plotted, no camera: Install must report 0, not throw on a missing defaultElev");
        }

        [Test]
        public void BoatAndCharacterCatalog_AreUntouched_ByTheDirsGeneralisation()
        {
            // The guarded DIRS/defaultElev reads must not have cost the existing paths anything.
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("character"));
            Assert.AreEqual(8, geo.NativeDirs, "characterIsoRig declares DIRS:8 — still read from the rig");
        }

        // ---- the slicer's kit manifest is the rigs' own geometry --------------------------------

        [Test]
        public void SlicerKitSpecs_MatchTheLiveRigs()
        {
            // FishingSheetSlicer runs without a script host, so its cells/pivots are constants.
            // This is the drift alarm: the constants must equal what the rigs themselves declare.
            using var host = RigScriptHostFactory.Create();
            foreach (var (rigKey, prefix) in new[] { ("fish", "Fish_"), ("bobber", "Bobber_"), ("rod", "Rod_") })
            {
                var geo = RigCatalog.Install(host, RigCatalog.Get(rigKey));
                var kit = FishingSheetSlicer.Kits[prefix];
                Assert.AreEqual(geo.Width, kit.Cell.x, $"{prefix}: slicer cell width drifted from the rig");
                Assert.AreEqual(geo.Height, kit.Cell.y, $"{prefix}: slicer cell height drifted from the rig");
                Assert.AreEqual(geo.PivotX, kit.PivotTopLeftPx.x, 1e-9, $"{prefix}: slicer pivot.x drifted");
                Assert.AreEqual(geo.PivotY, kit.PivotTopLeftPx.y, 1e-9, $"{prefix}: slicer pivot.y drifted");

                // The bottom-left normalization, spelled out once more because inverting it is
                // silent: (x/W, (H−y)/H).
                Assert.AreEqual((float)(geo.PivotX / geo.Width), kit.NormalizedPivot.x, 1e-4f);
                Assert.AreEqual((float)((geo.Height - geo.PivotY) / geo.Height), kit.NormalizedPivot.y, 1e-4f);
            }
        }

        // ---- the rigs carry the spec'd species / states / tiers ---------------------------------

        [Test]
        public void FishSpecies_AndStates_ComeFromTheRig_AndMatchTheSpec()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("fish");
            RigCatalog.Install(host, entry);

            var order = FishingKitBaker.ReadFishStateOrder(host, entry.GlobalName);
            CollectionAssert.AreEqual(FishStateSpec.Select(s => s.state).ToArray(), order.ToArray(),
                "AORDER+RESTS disagree with the drop's stated state order — if the rig changed, " +
                "the spec (and FishingKitSheetSliceTests' expectations) must change with it, in one commit");

            foreach (var (state, frames) in FishStateSpec)
                Assert.AreEqual(frames,
                    FishingKitBaker.FishStateFrames(host, entry.GlobalName, state),
                    $"the rig's tables disagree with the spec on '{state}'");

            string speciesJson = host.EvaluateString($"JSON.stringify({entry.GlobalName}.ORDER)");
            foreach (var sp in SpeciesSpec)
                StringAssert.Contains($"\"{sp}\"", speciesJson, "a spec'd species went missing from ORDER");
        }

        [Test]
        public void AFishStateTheRigDoesNotDeclare_RefusesLoudly()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("fish");
            RigCatalog.Install(host, entry);

            var ex = Assert.Throws<ArgumentException>(
                () => FishingKitBaker.FishStateFrames(host, entry.GlobalName, "moonwalk"));
            StringAssert.Contains("moonwalk", ex.Message);
        }

        [Test]
        public void BobberStates_ComeFromTheRig_AndMatchTheSpec()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("bobber");
            RigCatalog.Install(host, entry);

            foreach (var (state, frames) in BobberStateSpec)
                Assert.AreEqual(frames,
                    FishingKitBaker.BobberStateFrames(host, entry.GlobalName, state),
                    $"the rig's STATES table disagrees with the spec on '{state}'");

            Assert.Throws<ArgumentException>(
                () => FishingKitBaker.BobberStateFrames(host, entry.GlobalName, "submerge"));
        }

        [Test]
        public void RodTiers_AndToolAnims_AreAllServable()
        {
            using var host = RigScriptHostFactory.Create();
            var rod = RigCatalog.Get("rod");
            var ch = RigCatalog.Get("character");
            RigCatalog.Install(host, rod);
            RigCatalog.Install(host, ch);

            string tiersJson = host.EvaluateString($"JSON.stringify({rod.GlobalName}.order)");
            foreach (var tier in RodTierSpec)
                StringAssert.Contains($"\"{tier}\"", tiersJson, "a spec'd tier went missing from RodIso.order");

            StringAssert.Contains("[7,0,1]",
                host.EvaluateString($"JSON.stringify({rod.GlobalName}.behind)"),
                "RodIso.behind (rod-under-body facings NW/N/NE) changed shape");

            // Every anim the rod bake poses against must carry a character tool pose…
            foreach (var anim in FishingKitBaker.RodToolAnims)
                Assert.IsTrue(host.EvaluateBool(
                        $"{ch.GlobalName}.tool(0,{{anim:'{anim}',frame:0}}) !== null"),
                    $"CharacterIso.tool() returns null for '{anim}' — the rod overlay bake has " +
                    "nothing to pose with; if the rig dropped the state this recipe must change");

            // …and a non-tool anim must be null, or the guard in BakeRod guards nothing.
            Assert.IsFalse(host.EvaluateBool(
                $"{ch.GlobalName}.tool(0,{{anim:'walk',frame:0}}) !== null"));
        }

        // ---- the conventions are measured, not believed -----------------------------------------

        [Test]
        public void FishProbe_MeasuresTheClockwiseClaim_MatchingTheCatalog()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("fish");
            var geo = RigCatalog.Install(host, entry);

            var probe = FishingRigAzimuthProbe.MeasureFish(host, entry.GlobalName, geo,
                                                           FishingKitBaker.Dirs);
            Debug.Log($"[fish-probe]\n{probe.Report}");

            Assert.AreEqual(AzimuthConvention.Clockwise, probe.Convention,
                "the kit claims clockwise (th = −dir·45°) and the pixels are the arbiter — if " +
                "this measures CCW the art regressed; do not relabel the catalog without looking");
            Assert.AreEqual(entry.DeclaredConvention, probe.Convention,
                "catalog and pixels disagree — the bake would refuse, by design");

            // The mirrored-profile signal is real, not noise: head-mass right at E, left at W.
            Assert.Greater(probe.East.MassOffset, 0.0, "the row labelled E must carry its head screen-RIGHT");
            Assert.Less(probe.West.MassOffset, 0.0, "the row labelled W must carry its head screen-LEFT");
        }

        [Test]
        public void RodProbe_MeasuresTheClockwiseFix_MatchingTheCatalog()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("rod");
            var geo = RigCatalog.Install(host, entry);

            var probe = FishingRigAzimuthProbe.MeasureRod(host, entry.GlobalName, geo,
                                                          FishingKitBaker.Dirs);
            Debug.Log($"[rod-probe]\n{probe.Report}");

            Assert.AreEqual(AzimuthConvention.Clockwise, probe.Convention,
                "rodIsoRig is in the README's pixel-verified clockwise group; a CCW read here " +
                "means the art regressed");
            Assert.AreEqual(entry.DeclaredConvention, probe.Convention);
        }

        // ---- the pure probe maths, host-free ----------------------------------------------------

        [Test]
        public void Silhouette_ReadsMassTowardTheHeavySide_OnASyntheticCell()
        {
            // 16×16: a wide block at x 2..9 plus a thin 1-px tail at x 10..14 — the bbox midpoint
            // sits at 8, the mass well left of it.
            const int W = 16, H = 16;
            byte[] rgba = new byte[W * H * 4];
            void Put(int x, int y) { rgba[(y * W + x) * 4 + 3] = 255; }
            for (int y = 4; y <= 11; y++)
                for (int x = 2; x <= 9; x++) Put(x, y);
            for (int x = 10; x <= 14; x++) Put(x, 8);   // thin tail, few pixels

            var read = FishingRigAzimuthProbe.ReadSilhouette(rgba, W, H);
            Assert.Greater(read.OpaquePixels, 60);
            Assert.Less(read.MassOffset, -1.0,
                "mass at x 2..9 against a bbox stretched to x 14 must read heavy-side LEFT");
        }

        [Test]
        public void Silhouette_RejectsAWrongSizedBuffer()
        {
            Assert.Throws<ArgumentException>(
                () => FishingRigAzimuthProbe.ReadSilhouette(new byte[7], 16, 16));
        }

        // ---- one small bake per rig, end to end, to scratch -------------------------------------

        static string ScratchFolder()
        {
            const string outFolder = "artifacts/rig-bake-test";
            Directory.CreateDirectory(Path.Combine(RepoRoot, outFolder));
            return outFolder;
        }

        [Test]
        public void BakedFishSheets_HaveTheSpecifiedGeometry_AndEveryDirectionRowHasArt()
        {
            string outFolder = ScratchFolder();
            // cod, not mackerel: the LONGEST species keeps the bow-on rows (N/S — a fish seen
            // along its own axis is a few px of cross-section) comfortably above the art floor.
            var r = FishingKitBaker.BakeFish(new[] { "cod" }, new[] { "dart", "gill" }, outFolder);

            Assert.AreEqual(2, r.Sheets.Count);
            var dart = r.Sheets.Single(s => s.Name == "Fish_cod_dart");
            Assert.AreEqual(2, dart.Frames, "dart is a 2-frame state");
            Assert.AreEqual(2 * 64, dart.Width);
            Assert.AreEqual(8 * 64, dart.Height);
            var gill = r.Sheets.Single(s => s.Name == "Fish_cod_gill");
            Assert.AreEqual(2, gill.Frames, "gill is a 2-frame held rest");
            Assert.AreEqual(2 * 2 * 8, r.CellsRendered, "2 states × 2 frames × 8 dirs");

            // dart bakes UNDERWATER (tinted, alpha 160) and gill bakes DRY (opaque) — both must
            // land art in every direction row, so the row check runs at a low alpha floor.
            foreach (var sheet in r.Sheets)
                AssertEveryRowHasArt(sheet.AssetPath, 64, 64, rows: 8, alphaFloor: 64, minOpaque: 30);

            string anchors = File.ReadAllText(Path.Combine(RepoRoot, r.AnchorJsonPath));
            AssertBalancedJson(anchors, r.AnchorJsonPath);
            StringAssert.Contains("\"cod\"", anchors);
            StringAssert.Contains("\"mouth\"", anchors);
            StringAssert.Contains("\"hold\"", anchors);
            StringAssert.Contains("\"facingsAreCounterClockwise\": false", anchors);
            StringAssert.DoesNotContain("\"gill\"", anchors,
                "mouth() is body-space and the held rests re-pivot to the grip — a rest-pose " +
                "mouth is wrong data and must not be exported");
        }

        [Test]
        public void BakedBobberSheets_AreSingleRowStrips_WithTheRigsLineAttachData()
        {
            string outFolder = ScratchFolder();
            var r = FishingKitBaker.BakeBobber(outFolder);

            Assert.AreEqual(4, r.Sheets.Count);
            Assert.IsNull(r.MeasuredConvention, "the bobber has no azimuth and must never be probed");

            var fl = r.Sheets.Single(s => s.Name == "Bobber_float");
            Assert.AreEqual(4 * 16, fl.Width);
            Assert.AreEqual(22, fl.Height, "ONE row — the bobber is a state sprite, not a turntable");
            var fly = r.Sheets.Single(s => s.Name == "Bobber_fly");
            Assert.AreEqual(2 * 16, fly.Width);

            foreach (var sheet in r.Sheets)
                AssertEveryRowHasArt(sheet.AssetPath, 16, 22, rows: 1, alphaFloor: 64,
                                     minOpaque: 15);   // a 16×22 float is ~25 body px + keyline

            string anchors = File.ReadAllText(Path.Combine(RepoRoot, r.AnchorJsonPath));
            AssertBalancedJson(anchors, r.AnchorJsonPath);
            StringAssert.Contains("\"directional\": false", anchors);
            // float f0 sits ON the waterline: stem top at dy −6. strike f3 is pulled 6 px under:
            // −6+6 = 0. fly is airborne: dip ignored, −6. All straight from the rig's POSE table.
            StringAssert.Contains("\"float\": { \"frames\": 4, \"ms\": 240, \"lineAttach\": [{ \"dx\": 0, \"dy\": -6 }", anchors);
            StringAssert.Contains("{ \"dx\": 0, \"dy\": 0 }] },", anchors);
            StringAssert.Contains("\"fly\": { \"frames\": 2, \"ms\": 120, \"lineAttach\": [{ \"dx\": 0, \"dy\": -6 }, { \"dx\": 0, \"dy\": -6 }] }", anchors);
        }

        [Test]
        public void BakedRodSheets_PoseFromTheCharacterRig_AndCarryTipAnchors()
        {
            string outFolder = ScratchFolder();
            var r = FishingKitBaker.BakeRod(new[] { "cane" }, new[] { "bite" },
                                            includeRests: true, outFolder);

            Assert.AreEqual(3, r.Sheets.Count, "bite + ground + stored");
            var bite = r.Sheets.Single(s => s.Name == "Rod_cane_bite");
            Assert.AreEqual(6, bite.Frames, "bite is a 6-frame state (read from CharacterIso.ANIMS)");
            Assert.AreEqual(6 * 112, bite.Width);
            Assert.AreEqual(8 * 112, bite.Height);
            var ground = r.Sheets.Single(s => s.Name == "Rod_cane_ground");
            Assert.AreEqual(112, ground.Width);

            foreach (var sheet in r.Sheets)
                AssertEveryRowHasArt(sheet.AssetPath, 112, 112, rows: 8, alphaFloor: 64,
                                     minOpaque: 15);   // a cane rod is thin — tens of px, not hundreds

            string anchors = File.ReadAllText(Path.Combine(RepoRoot, r.AnchorJsonPath));
            AssertBalancedJson(anchors, r.AnchorJsonPath);
            StringAssert.Contains("\"behindDirs\": [7,0,1]", anchors);
            StringAssert.Contains("\"grips\"", anchors);
            StringAssert.Contains("\"bite\"", anchors);
            StringAssert.Contains("\"cane\"", anchors);
            StringAssert.Contains("\"tip\"", anchors);
            StringAssert.Contains("\"tipLocal\"", anchors);
            StringAssert.Contains("\"ground\"", anchors);
            StringAssert.Contains("\"facingsAreCounterClockwise\": false", anchors);
        }

        [Test]
        public void RodBake_RefusesAnAnimWithoutAToolPose()
        {
            string outFolder = ScratchFolder();
            var ex = Assert.Throws<ArgumentException>(
                () => FishingKitBaker.BakeRod(new[] { "cane" }, new[] { "walk" }, false, outFolder));
            StringAssert.Contains("walk", ex.Message);
            Assert.IsFalse(File.Exists(Path.Combine(RepoRoot, outFolder, "Rod_cane_walk.png")),
                "a refused recipe must leave zero files on disk");
        }

        // ---- helpers ----------------------------------------------------------------------------

        static void AssertEveryRowHasArt(string assetPath, int cellW, int cellH, int rows,
                                         byte alphaFloor, int minOpaque = 50)
        {
            byte[] png = File.ReadAllBytes(Path.Combine(RepoRoot, assetPath));
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                Assert.IsTrue(tex.LoadImage(png, markNonReadable: false),
                              $"failed to decode {assetPath}");
                Color32[] px = tex.GetPixels32();
                for (int row = 0; row < rows; row++)
                {
                    int visible = 0;
                    // Texture pixels are bottom-origin; sheet row 0 is the TOP row.
                    int yLo = (rows - 1 - row) * cellH;
                    for (int y = yLo; y < yLo + cellH; y++)
                        for (int x = 0; x < cellW; x++)   // frame 0 of the row is plenty
                            if (px[y * tex.width + x].a >= alphaFloor) visible++;
                    Assert.Greater(visible, minOpaque,
                        $"{assetPath}: row {row} rendered almost nothing ({visible} px over " +
                        $"alpha {alphaFloor}) — a blank row slices fine and ships invisible");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>Brace/bracket balance outside string literals — a cheap structural check that
        /// catches the classic hand-built-JSON failure (a missing close on the last list item)
        /// without needing a JSON parser in the test assembly.</summary>
        static void AssertBalancedJson(string json, string path)
        {
            int brace = 0, bracket = 0;
            bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inString = true; break;
                    case '{': brace++; break;
                    case '}': brace--; break;
                    case '[': bracket++; break;
                    case ']': bracket--; break;
                }
                Assert.GreaterOrEqual(brace, 0, $"{path}: closes an object it never opened (at {i})");
                Assert.GreaterOrEqual(bracket, 0, $"{path}: closes an array it never opened (at {i})");
            }
            Assert.AreEqual(0, brace, $"{path}: unbalanced braces");
            Assert.AreEqual(0, bracket, $"{path}: unbalanced brackets");
            Assert.IsFalse(inString, $"{path}: unterminated string literal");
        }
    }
}
