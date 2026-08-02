using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The character bake path, proven end-to-end WITHOUT any committed sheet: everything here runs
    /// the V8 host CPU-side and decodes PNGs via <c>Texture2D.LoadImage</c>, both already established
    /// as safe on CI's null graphics device by this suite. The shipped sheets are baked on the owner's
    /// machine (Hidden Harbours ▸ Art ▸ Bake Character Sheets …) — these tests are what make that bake
    /// trustworthy before it runs, and <c>CharacterIsoSheetSliceTests</c> takes over once the PNGs land.
    ///
    /// <para><b>Pass 6 (2026-08-02).</b> The rig became three files with a mandatory load order, the
    /// cell grew 64 × 88 → 64 × 92, and the sheet axis stopped being the anim axis (<c>cast</c> bakes
    /// twice, once per power). Each of those is a way to ship wrong art in silence, so each has an
    /// assert below.</para>
    /// </summary>
    public class CharacterRigBakeTests
    {
        /// <summary>
        /// The rig's frame counts, restated here ON PURPOSE rather than imported from
        /// <c>CharacterIsoSheetSliceTests</c> (different asmdef — and the duplication is the point:
        /// this asserts the RIG agrees with the recipe before any sheet exists, while the slice test
        /// asserts the PNGs agree with it after).
        /// </summary>
        static readonly (string anim, int frames)[] AnimSpec =
        {
            ("idle", 6), ("walk", 8), ("run", 6),
            ("balance", 8), ("stagger", 10),
            ("hold", 6), ("cast", 10), ("castBack", 6), ("castRelease", 8),
            ("bite", 6), ("strike", 6), ("reel", 12), ("land", 12),
            ("dig", 10),
        };

        /// <summary>Which layer each anim drives, from the pass-6 mount contract.</summary>
        static readonly (string anim, string mount)[] MountSpec =
        {
            ("idle", "free"), ("walk", "free"), ("run", "free"),
            ("balance", "free"), ("stagger", "free"),
            ("hold", "rod"), ("cast", "rod"), ("reel", "rod"), ("land", "rod"),
            ("dig", "shovel"),
        };

        const int CellW = 64, CellH = 92, Dirs = 8;
        const double PivotX = 32.0, PivotY = 82.0;
        const int GroundInsetPx = 10;   // CellH − PivotY, the number the slicer must agree with

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        // ---- the three-file install --------------------------------------------------------------

        [Test]
        public void CharacterRig_Installs_WithItsHeadAndEyeRigs_AndWithoutARockBlock()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("character");
            var geo = RigCatalog.Install(host, entry);   // used to throw: ROCK.frames on no ROCK

            Assert.AreEqual(CellW, geo.Width);
            Assert.AreEqual(CellH, geo.Height,
                "the pass-6 cell is 64 × 92. If this reads 88 the catalog is still pointed at the " +
                "pass-1 body, and every sheet baked from it slices at the wrong grid.");
            Assert.AreEqual(Dirs, geo.NativeDirs);
            Assert.AreEqual(0, geo.RockFrames,
                "the character rig exposes no ROCK block — characters ride a deck's rock via " +
                "opts, they do not own a rock cycle. Install must report 0, not throw.");

            // Pivot (32,82) top-left = ground contact 10 px above the cell bottom.
            Assert.AreEqual(PivotX, geo.PivotX, 1e-9);
            Assert.AreEqual(PivotY, geo.PivotY, 1e-9);
            Assert.AreEqual(0.5f, geo.UnityNormalisedPivot.x, 1e-4f);
            Assert.AreEqual(GroundInsetPx / (float)CellH, geo.UnityNormalisedPivot.y, 1e-4f);
        }

        [Test]
        public void InstallingTheBody_AlsoInstalledTheHeadAndEyeRigs_InThatOrder()
        {
            // ⚠️ THE SILENT FAILURE THIS EXISTS FOR: without the head rig the body still renders. It
            // falls back to its own local HATS_LOCAL table and never stamps a face — wrong art, no
            // exception, and a bake would happily write 33 sheets of it.
            using var host = RigScriptHostFactory.Create();
            RigCatalog.Install(host, RigCatalog.Get("character"));

            Assert.IsTrue(host.EvaluateBool("typeof EyeIso === 'object' && EyeIso !== null"),
                "eyeIsoRig did not install — declare it as a prerequisite, do not load it by hand");
            Assert.IsTrue(host.EvaluateBool("typeof HeadIso === 'object' && HeadIso !== null"),
                "headIsoRig3 did not install — the body would silently use HATS_LOCAL and no face");

            // The body really is delegating, not falling back: its hat list is the HEAD rig's.
            Assert.IsTrue(host.EvaluateBool("CharacterIso6.head === HeadIso"),
                "the body's head accessor is not the installed head rig — load order is wrong");
            Assert.IsTrue(host.EvaluateBool(
                "JSON.stringify(CharacterIso6.HATS) === JSON.stringify(HeadIso.HATS)"),
                "the body's HATS came from HATS_LOCAL, not the head rig — it fell back");
        }

        [Test]
        public void TheCatalogPointsAtPassSix_NotThePassOneBody()
        {
            var entry = RigCatalog.Get("character");
            StringAssert.Contains("characterIsoRig6.js", entry.ScriptPath);
            Assert.AreEqual("CharacterIso6", entry.GlobalName);

            using var host = RigScriptHostFactory.Create();
            RigCatalog.Install(host, entry);
            Assert.AreEqual(6.0, host.EvaluateNumber("CharacterIso6.pass"), 1e-9,
                            "the rig itself reports a different pass than the catalog names");
        }

        [Test]
        public void BoatCatalog_IsUntouched_PuntStillInstallsWithRock()
        {
            // The prerequisite generalisation must not have cost the boat path its ROCK read.
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("punt"));
            Assert.Greater(geo.RockFrames, 0, "the punt's ROCK block went missing from Install");
        }

        // ---- the rig carries the spec'd states ----------------------------------------------------

        [Test]
        public void AnimFrameCounts_ComeFromTheRig_AndMatchTheRecipe()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("character");
            RigCatalog.Install(host, entry);

            foreach (var (anim, frames) in AnimSpec)
                Assert.AreEqual(frames, CharacterRigBaker.FramesOf(host, entry.GlobalName, anim),
                    $"the rig's ANIMS table disagrees with the recipe on '{anim}' — if the rig " +
                    "changed, the recipe (and CharacterIsoSheetSliceTests' Frames) must change with " +
                    "it, in one commit");
        }

        [Test]
        public void MountContract_ComesFromTheRig_AndMatchesTheRecipe()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("character");
            RigCatalog.Install(host, entry);

            foreach (var (anim, mount) in MountSpec)
                Assert.AreEqual(mount, CharacterRigBaker.MountOf(host, entry.GlobalName, anim),
                    $"'{anim}' drives a different layer than the recipe expects — the sidecar would " +
                    "carry the wrong pin block (tool vs carry) for it");
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

        // ---- the NEGATIVE control: an unknown build must not resolve silently ----------------------

        [Test]
        public void ABuildPresetTheRigDoesNotHave_RefusesBeforeAnyFileLands()
        {
            // THE failure this guards: the rig resolves an unknown preset to DEFAULT_BUILD without a
            // word, so a typo'd cast key would bake nine identical fishers under nine names and the
            // slice tests would all pass. The bake must refuse, and refuse with NOTHING on disk.
            string outFolder = "artifacts/rig-bake-test-refusal";
            string abs = Path.Combine(RepoRoot, outFolder);
            if (Directory.Exists(abs)) Directory.Delete(abs, recursive: true);

            var ex = Assert.Throws<ArgumentException>(() => CharacterRigBaker.Bake(
                "character", new[] { new CharacterState("idle") }, outFolder, "Nobody_",
                "NobodyAnchors.json", buildPreset: "shipwright"));

            StringAssert.Contains("shipwright", ex.Message);
            StringAssert.Contains("ginny", ex.Message, "the message must list the keys that DO exist");
            Assert.IsFalse(Directory.Exists(abs) && Directory.GetFiles(abs).Length > 0,
                           "the refusal wrote files — validation must happen before the first blit");
        }

        [Test]
        public void ARealBuildPreset_ChangesThePixels()
        {
            // The positive half of the control: if a preset key were ignored, the refusal test above
            // could pass while every build still rendered the same person.
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("character");
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            byte[] fisher = host.EvaluateBytes($"{g}.render(4,{{anim:'idle',frame:0,build:{{preset:'fisher'}}}})");
            byte[] nan = host.EvaluateBytes($"{g}.render(4,{{anim:'idle',frame:0,build:{{preset:'nan'}}}})");

            Assert.AreEqual(geo.Width * geo.Height * 4, fisher.Length);
            int differing = 0;
            for (int i = 0; i < fisher.Length; i++) if (fisher[i] != nan[i]) differing++;
            Assert.Greater(differing, 500,
                "'fisher' and 'nan' rendered near-identically — an adult man in bib overalls and an " +
                "elder woman in a skirt and kerchief must not be the same pixels. The build opt is " +
                "not reaching the rig.");
        }

        // ---- the sheet axis is not the anim axis --------------------------------------------------

        [Test]
        public void CastPowerVariants_BakeToTheirOwnSheetKeys()
        {
            Assert.AreEqual("cast_short", new CharacterState("cast", "short").Key);
            Assert.AreEqual("cast_long", new CharacterState("cast", "long").Key);
            Assert.AreEqual("reel", new CharacterState("reel").Key,
                            "a state with no power must not grow a suffix");
        }

        [Test]
        public void ACarryStanceTheRigWouldIgnore_RefusesRatherThanBakingADuplicate()
        {
            // Both refusals guard the SAME silent defect: the rig resolves a carry it will not use
            // and renders the free (or tool) pose anyway, so the sheet is a byte-identical copy under
            // a name that promises a stance. Nothing downstream could tell.
            string outFolder = "artifacts/rig-bake-test-carry-refusal";

            // 1. a TOOL anim — the tool always wins and pose() drops the carry.
            var onTool = Assert.Throws<ArgumentException>(() => CharacterRigBaker.Bake(
                "character", new[] { new CharacterState("reel", null, "buckets") },
                outFolder, "Nope_"));
            StringAssert.Contains("rod", onTool.Message);

            // 2. a free anim the rig's own CARRIES table does not ride: nobody runs a tiller.
            var onGait = Assert.Throws<ArgumentException>(() => CharacterRigBaker.Bake(
                "character", new[] { new CharacterState("run", null, "helm") },
                outFolder, "Nope_"));
            StringAssert.Contains("helm", onGait.Message);
            StringAssert.Contains("buckets", onGait.Message,
                                  "the message must name the stances that DO ride a run");
        }

        [Test]
        public void ThePlayerRecipe_CoversEveryAnimTheRigDeclares()
        {
            // A state the rig grows and the recipe never learns about is art that silently never
            // ships. Checked against the rig, not against a list.
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("character");
            RigCatalog.Install(host, entry);

            string[] declared = host
                .EvaluateString($"Object.keys({entry.GlobalName}.ANIMS).join(',')")
                .Split(',');

            foreach (string anim in declared)
                Assert.IsTrue(Array.Exists(CharacterRigBakeMenu.PlayerStates, s => s.Anim == anim),
                    $"the rig declares '{anim}' but the player recipe never bakes it");
        }

        // ---- the convention is measured, not believed ---------------------------------------------

        [Test]
        public void CharacterProbe_MeasuresTheConvention_AndTheCatalogAgrees()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("character");
            var geo = RigCatalog.Install(host, entry);

            var probe = CharacterRigAzimuthProbe.Measure(host, entry.GlobalName, geo);
            Debug.Log($"[character-probe]\n{probe.Report}");

            Assert.AreEqual(entry.DeclaredConvention, probe.Convention,
                "catalog and pixels disagree — the bake would refuse, by design. This lane has been " +
                "CCW-mislabelled twice; look at the art before touching the catalog.");

            // The two profile rows really look to opposite sides — the signal is real, not noise.
            Assert.AreNotEqual(Math.Sign(probe.East.FaceOffset), Math.Sign(probe.West.FaceOffset),
                "the E and W rows must look to OPPOSITE sides for the read to mean anything");
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

        // ---- one full state, end to end, to scratch -----------------------------------------------

        [Test]
        public void BakedBiteSheet_HasTheSpecifiedGeometry_AndEveryDirectionRowHasArt()
        {
            string outFolder = "artifacts/rig-bake-test";
            Directory.CreateDirectory(Path.Combine(RepoRoot, outFolder));

            var r = CharacterRigBaker.Bake("character", new[] { "bite" }, outFolder,
                                           "FisherGolden_", "FisherGoldenAnchors.json");

            Assert.AreEqual(1, r.Sheets.Count);
            var sheet = r.Sheets[0];
            Assert.AreEqual("bite", sheet.State);
            Assert.AreEqual(6, sheet.Frames, "bite is a 6-frame state");
            Assert.AreEqual(6 * CellW, sheet.Width);      // 384
            Assert.AreEqual(Dirs * CellH, sheet.Height);  // 736
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
            StringAssert.Contains("\"pivotTopLeft\": { \"x\": 32, \"y\": 82 }", anchors);
        }

        [Test]
        public void TheSidecar_CarriesToolPinsForARodState_AndCarryPinsForAFreeOne()
        {
            // The pass-6 addition: a prop is never placed by hand, so every state's sidecar must
            // carry the pin the rig says that state's mount needs. A sidecar with anchors but no
            // tool block looks complete and leaves the rod floating.
            string outFolder = "artifacts/rig-bake-test-sidecar";
            Directory.CreateDirectory(Path.Combine(RepoRoot, outFolder));

            var r = CharacterRigBaker.Bake(
                "character",
                new[]
                {
                    new CharacterState("hold"),                          // rod → tool pins
                    new CharacterState("walk"),                          // free, empty-handed
                    new CharacterState("walk", null, "buckets"),         // free, a stance → carry pins
                },
                outFolder, "Sidecar_", "SidecarAnchors.json", buildPreset: "ginny");

            Assert.AreEqual(3, r.Sheets.Count);
            Assert.AreEqual("walk_buckets", r.Sheets[2].State,
                            "a stance is its own sheet, keyed anim_stance");

            string json = File.ReadAllText(Path.Combine(RepoRoot, r.AnchorJsonPath));

            StringAssert.Contains("\"build\": \"ginny\"", json);
            StringAssert.Contains("\"mount\": \"rod\"", json, "hold drives the rod layer");
            StringAssert.Contains("\"tool\":", json, "a rod state must carry its grip pins");
            StringAssert.Contains("\"pitch\"", json, "the tool pin carries pitch/yaw/bend");

            StringAssert.Contains("\"mount\": \"free\"", json, "walk drives no tool");
            StringAssert.Contains("\"carry\": \"buckets\"", json, "the stance names itself");
            StringAssert.Contains("\"carryPins\":", json, "a stance state must carry its hand pins");
            StringAssert.Contains("\"swingL\"", json, "the pail pendulum rides in the carry pins");
            StringAssert.Contains("\"behindL\"", json, "and so does the draw-under flag");

            // The rounding replacer really ran: no full-precision doubles in the file.
            StringAssert.DoesNotContain("0000000000", json,
                "anchor numbers are not rounded — the sidecar is carrying float noise");
        }

        [Test]
        public void ThePadIsZero_AndTheSheetCellIsTheRigCell()
        {
            // Pinning the deviation from the kit README's PAD = 24 so it stays a decision rather than
            // drifting back. The kit pads because its harness composites the prop INTO the cell; this
            // baker pins props at runtime from the sidecar, so a pad would be 2.66× the pixels for
            // transparent margin. If a bake ever does composite a prop, this test is the tripwire.
            Assert.AreEqual(0, CharacterRigBaker.PadPx);

            string outFolder = "artifacts/rig-bake-test";
            Directory.CreateDirectory(Path.Combine(RepoRoot, outFolder));
            var r = CharacterRigBaker.Bake("character", new[] { "idle" }, outFolder, "FisherPad_");

            Assert.AreEqual(CellW * 6, r.Sheets[0].Width);
            Assert.AreEqual(CellH * Dirs, r.Sheets[0].Height);
        }

        // ---- the pure probe maths, host-free ------------------------------------------------------

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
