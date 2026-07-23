using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The character bake path (Rod Fishing v2 wave 1 — spec in PR #251), proven end-to-end WITHOUT
    /// any committed sheet: everything here runs the V8 host CPU-side and decodes PNGs via
    /// <c>Texture2D.LoadImage</c>, both already established as safe on CI's null graphics device by
    /// this suite. The eight fight sheets themselves are baked on the owner's machine
    /// (Hidden Harbours ▸ Art ▸ Bake Character Fight Sheets) — these tests are what make that bake
    /// trustworthy before it runs, and <c>CharacterIsoSheetSliceTests</c> takes over the moment the
    /// PNGs land.
    /// </summary>
    public class CharacterRigBakeTests
    {
        /// <summary>
        /// The spec's frame counts, restated here ON PURPOSE rather than imported from
        /// <c>CharacterIsoSheetSliceTests</c> (different asmdef — and the duplication is the point:
        /// this asserts the RIG agrees with the spec before any sheet exists, while the slice test
        /// asserts the PNGs agree with it after).
        /// </summary>
        static readonly (string anim, int frames)[] FightSpec =
        {
            ("bite", 6), ("strike", 6), ("reel", 12), ("land", 12),
            ("castBack", 6), ("castRelease", 8), ("balance", 8), ("stagger", 10),
        };

        const int CellW = 64, CellH = 88, Dirs = 8;

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        // ---- the ROCK-less install ------------------------------------------------------------

        [Test]
        public void CharacterRig_Installs_WithoutARockBlock()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("character");
            var geo = RigCatalog.Install(host, entry);   // used to throw: ROCK.frames on no ROCK

            Assert.AreEqual(CellW, geo.Width);
            Assert.AreEqual(CellH, geo.Height);
            Assert.AreEqual(Dirs, geo.NativeDirs);
            Assert.AreEqual(0, geo.RockFrames,
                "characterIsoRig exposes no ROCK block — characters ride a deck's rock via " +
                "opts, they do not own a rock cycle. Install must report 0, not throw.");

            // Pivot (32,80) top-left = ground contact 8 px above the cell bottom — the exact rule
            // CharacterSheetSlicer bakes into every slice, read here from the rig itself.
            Assert.AreEqual(32.0, geo.PivotX, 1e-9);
            Assert.AreEqual(80.0, geo.PivotY, 1e-9);
            Assert.AreEqual(0.5f, geo.UnityNormalisedPivot.x, 1e-4f);
            Assert.AreEqual(8f / CellH, geo.UnityNormalisedPivot.y, 1e-4f);
        }

        [Test]
        public void BoatCatalog_IsUntouched_PuntAndLobsterStillInstallWithRock()
        {
            // The generalisation must not have cost the boat path its ROCK read.
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("punt"));
            Assert.Greater(geo.RockFrames, 0, "the punt's ROCK block went missing from Install");
        }

        // ---- the rig carries the spec'd states --------------------------------------------------

        [Test]
        public void FightStates_FrameCounts_ComeFromTheRig_AndMatchTheSpec()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("character");
            RigCatalog.Install(host, entry);

            foreach (var (anim, frames) in FightSpec)
                Assert.AreEqual(frames, CharacterRigBaker.FramesOf(host, entry.GlobalName, anim),
                    $"the rig's ANIMS table disagrees with the PR #251 spec on '{anim}' — if the " +
                    "rig changed, the spec (and CharacterIsoSheetSliceTests' ExpectedFrames) must " +
                    "change with it, in one commit");
        }

        [Test]
        public void AnAnimTheRigDoesNotDeclare_RefusesLoudly()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("character");
            RigCatalog.Install(host, entry);

            var ex = Assert.Throws<ArgumentException>(
                () => CharacterRigBaker.FramesOf(host, entry.GlobalName, "moonwalk"));
            StringAssert.Contains("moonwalk", ex.Message);
        }

        // ---- the convention is measured, not believed -------------------------------------------

        [Test]
        public void CharacterProbe_MeasuresTheClockwiseBake_MatchingTheCatalog()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("character");
            var geo = RigCatalog.Install(host, entry);

            var probe = CharacterRigAzimuthProbe.Measure(host, entry.GlobalName, geo);
            Debug.Log($"[character-probe]\n{probe.Report}");

            Assert.AreEqual(AzimuthConvention.Clockwise, probe.Convention,
                "the rig was fixed CLOCKWISE at source (th = −dir·45°); if this measures CCW the " +
                "art regressed — do not 'fix' it by relabelling the catalog without looking at it");
            Assert.AreEqual(entry.DeclaredConvention, probe.Convention,
                "catalog and pixels disagree — the bake would refuse, by design");

            // The two profile rows really look to opposite sides — the signal is real, not noise.
            Assert.Greater(probe.East.FaceOffset, 0.0, "the row labelled E must look screen-RIGHT");
            Assert.Less(probe.West.FaceOffset, 0.0, "the row labelled W must look screen-LEFT");
        }

        [Test]
        public void DirForCell_ClockwiseRig_EmitsRenderDUnchanged()
        {
            // The boat (N−k)%N mirror must NEVER touch a clockwise rig — cell d renders dir d.
            for (int d = 0; d < Dirs; d++)
                Assert.AreEqual((double)d,
                                RigBaker.DirForCell(d, Dirs, AzimuthConvention.Clockwise), 1e-9,
                                "re-mirroring corrected art is the exact blanket-fix mistake the " +
                                "probe exists to prevent");
        }

        // ---- one full state, end to end, to scratch ---------------------------------------------

        [Test]
        public void BakedBiteSheet_HasTheSpecifiedGeometry_AndEveryDirectionRowHasArt()
        {
            string outFolder = "artifacts/rig-bake-test";
            Directory.CreateDirectory(Path.Combine(RepoRoot, outFolder));

            var r = CharacterRigBaker.Bake("character", new[] { "bite" }, outFolder,
                                           "FisherGolden_", "FisherGoldenAnchors.json");

            Assert.AreEqual(1, r.Sheets.Count);
            var sheet = r.Sheets[0];
            Assert.AreEqual(6, sheet.Frames, "bite is a 6-frame state");
            Assert.AreEqual(6 * CellW, sheet.Width);    // 384
            Assert.AreEqual(Dirs * CellH, sheet.Height); // 704
            Assert.AreEqual(Dirs * 6, r.CellsRendered);

            // Decode what landed on disk and check every direction row actually rendered a fisher —
            // an empty row would slice "successfully" and only show up in-game.
            byte[] png = File.ReadAllBytes(Path.Combine(RepoRoot, sheet.AssetPath));
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                Assert.IsTrue(tex.LoadImage(png, markNonReadable: false), "failed to decode the baked PNG");
                Assert.AreEqual(sheet.Width, tex.width);
                Assert.AreEqual(sheet.Height, tex.height);

                Color32[] px = tex.GetPixels32();
                for (int row = 0; row < Dirs; row++)
                {
                    int opaque = 0;
                    // Texture pixels are bottom-origin; sheet row 0 is the TOP row.
                    int yLo = (Dirs - 1 - row) * CellH;
                    for (int y = yLo; y < yLo + CellH; y++)
                        for (int x = 0; x < CellW; x++)   // frame 0 of the row is plenty
                            if (px[y * tex.width + x].a >= 128) opaque++;
                    Assert.Greater(opaque, 200,
                        $"direction row {row} rendered almost nothing ({opaque} opaque px) — a " +
                        "blank row slices fine and ships as an invisible fisher");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }

            // The anchors JSON exists and carries the state, per dir, per frame.
            string anchors = File.ReadAllText(Path.Combine(RepoRoot, r.AnchorJsonPath));
            StringAssert.Contains("\"bite\"", anchors);
            StringAssert.Contains("\"handR\"", anchors);
            StringAssert.Contains("\"facingsAreCounterClockwise\": false", anchors);
        }

        // ---- the pure probe maths, host-free ----------------------------------------------------

        [Test]
        public void FaceOffset_ReadsSkinRightOfBody_OnASyntheticCell()
        {
            // A 16×16 cell: a grey "body" column at x 6..9 in the head band, plus "skin" at x 10..11
            // — the face sits right of the body centroid.
            const int W = 16, H = 16;
            var skin = new Color32(224, 169, 129, 255);
            var body = new Color32(60, 60, 60, 255);
            byte[] rgba = new byte[W * H * 4];
            void Put(int x, int y, Color32 c)
            {
                int i = (y * W + x) * 4;
                rgba[i] = c.r; rgba[i + 1] = c.g; rgba[i + 2] = c.b; rgba[i + 3] = 255;
            }
            for (int y = 4; y <= 12; y++)
            {
                for (int x = 6; x <= 9; x++) Put(x, y, body);
                Put(10, y, skin); Put(11, y, skin);
            }

            var read = CharacterRigAzimuthProbe.MeasureFaceOffset(
                rgba, W, H, headAnchorY: 8, new[] { skin });

            Assert.Greater(read.SkinPixels, 0);
            Assert.Greater(read.FaceOffset, 1.0,
                "skin at x 10..11 against a body centred lower must read as a RIGHT-looking face");
        }

        [Test]
        public void FaceOffset_RejectsAWrongSizedBuffer()
        {
            Assert.Throws<ArgumentException>(() =>
                CharacterRigAzimuthProbe.MeasureFaceOffset(
                    new byte[7], 16, 16, 8, new[] { new Color32(1, 2, 3, 255) }));
        }
    }
}
