using System;
using System.IO;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The CLAM SPADE bake path, proven end-to-end WITHOUT any committed sheet — everything here
    /// runs the V8 host CPU-side, which <c>RigBakerTests</c> / <c>CharacterRigBakeTests</c> already
    /// establish as CI-safe on a null graphics device. The sheets themselves are baked on the
    /// owner's machine (Hidden Harbours ▸ Art ▸ Bake Shovel Kit); these tests are what make that
    /// bake trustworthy before it runs, and <c>FishingKitSheetSliceTests</c> takes over the moment
    /// the PNGs land.
    ///
    /// <para><b>The owner's report this kit answers (playtest 2026-09-09):</b> <i>"the shovel seems
    /// to be the old sprite which does not look rooted in the players hand."</i> Two defects, and
    /// the tests below split them because the fixes live in different lanes — the missing ART is
    /// this kit's, the missing ANCHOR is upstream. See
    /// <see cref="TheCarriedSpadeHasNoRigAnswer_WhichIsWhyThereIsNoCarrySheet"/>.</para>
    /// </summary>
    public class ShovelKitBakeTests
    {
        /// <summary>
        /// The spade's stated shape, restated here ON PURPOSE rather than read from the rig — the
        /// same policy the fishing kit's tests state for themselves. These assert the RIG agrees
        /// with what the kit was built against; reading them from the rig would assert the rig
        /// against itself.
        /// </summary>
        const int CellW = 112, CellH = 112;
        const double PivotX = 56, PivotY = 72;
        const int DigFrames = 10;
        const int Dirs = 8;

        static RigGeometry Shovel(IRigScriptHost host) =>
            RigCatalog.Install(host, RigCatalog.Get("shovel"));

        // ---- the rig is registered, and what it is ------------------------------------------

        [Test]
        public void TheSpadeRigIsRegistered_AndCarriesTheCellAndGripTheKitWasBuiltAgainst()
        {
            Assert.IsTrue(RigCatalog.Has("shovel"),
                "shovelIsoRig.js was committed and unregistered for months — CharacterRigBaker's " +
                "hand-prop layer skipped it in so many words. Registration is what makes it bakeable.");

            var entry = RigCatalog.Get("shovel");
            Assert.AreEqual("docs/art/rigs/shovelIsoRig.js", entry.ScriptPath);
            Assert.AreEqual("ShovelIso", entry.GlobalName);
            CollectionAssert.IsEmpty(entry.Prerequisites,
                "the spade renders standalone; the CHARACTER rig is its pose DRIVER, not a " +
                "prerequisite — ShovelKitBaker installs and probes both.");

            using var host = RigScriptHostFactory.Create();
            RigGeometry geo = Shovel(host);
            Assert.AreEqual(CellW, geo.Width, "cell width");
            Assert.AreEqual(CellH, geo.Height, "cell height");
            Assert.AreEqual(PivotX, geo.PivotX, 1e-9, "pivot x = the grip centre");
            Assert.AreEqual(PivotY, geo.PivotY, 1e-9, "pivot y = the grip centre");
        }

        // ---- the azimuth: the finding this whole kit turns on --------------------------------

        /// <summary>
        /// <b>The spade rig is COUNTER-CLOCKWISE and the rig that poses it is CLOCKWISE.</b> The
        /// asserted fact is the DISAGREEMENT, and both halves are asserted, because the disagreement
        /// is what makes <c>ShovelKitBaker</c> apply the correction per rig at every cell instead of
        /// once for the sheet.
        ///
        /// <para><b>⚠️ Why the premise is asserted before the claim.</b> "The catalog's declaration
        /// matches the measurement" is true of a catalog that declares Clockwise and a rig that
        /// became clockwise — so this first pins that the CHARACTER and the ROD are clockwise and
        /// the spade is not. The day the art director folds the missing sign fix into
        /// <c>shovelIsoRig.js</c>, this reddens on the spade's line and the fix is consumed
        /// deliberately, rather than the kit quietly mirroring itself.</para>
        ///
        /// <para>The error class is why this is worth a test at all: <c>drawn − true = −2·heading</c>
        /// is exactly ZERO at north and south and 180° out at east and west, so a spot-check that
        /// reaches for a cardinal cannot see it. That trap has shipped mirrored art in this repo
        /// before.</para>
        /// </summary>
        [Test]
        public void TheSpadeIsCounterClockwise_WhereTheRigThatPosesItIsClockwise()
        {
            Assert.AreEqual(AzimuthConvention.Clockwise,
                            RigCatalog.Get("character").DeclaredConvention,
                            "premise: the pose driver is clockwise");
            Assert.AreEqual(AzimuthConvention.Clockwise, RigCatalog.Get("rod").DeclaredConvention,
                            "premise: the spade's structural twin is clockwise");

            Assert.AreEqual(AzimuthConvention.CounterClockwise,
                            RigCatalog.Get("shovel").DeclaredConvention,
                            "the spade's camBasis is th = +dir·45° where the character's and the " +
                            "rod's are th = −dir·45° — and the rod's carries the comment 'ADR-0006 " +
                            "fix: CW azimuth, kept in sync with characterIsoRig so the mount still " +
                            "aligns'. The spade never got that fix.");
        }

        /// <summary>
        /// The declaration above, measured from PIXELS rather than trusted — the same
        /// refuse-on-mismatch loop every other kit's bake runs, executed here so a mismatch is a
        /// red test rather than a failed bake on the owner's machine.
        /// </summary>
        [Test]
        public void TheMeasuredConvention_MatchesTheCatalogDeclaration()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("shovel");
            RigGeometry geo = Shovel(host);

            var probe = ShovelRigAzimuthProbe.Measure(host, entry.GlobalName, geo, Dirs);
            Assert.AreEqual(entry.DeclaredConvention, probe.Convention, probe.Report);

            // Mirrored, and past the floor on BOTH sides — the property the centroid read failed.
            Assert.Less(probe.EastBladeOffsetPx, -5.0, probe.Report);
            Assert.Greater(probe.WestBladeOffsetPx, 5.0, probe.Report);
            Assert.AreEqual(-probe.EastBladeOffsetPx, probe.WestBladeOffsetPx, 2.0,
                            "the east and west reads must mirror to within a couple of px, or the " +
                            "pose being probed is no longer flat along the heading\n" + probe.Report);
        }

        /// <summary>
        /// <b>Why this probe reads the blade's EXTREME and not the opaque centroid.</b> Transcribing
        /// <c>FishingRigAzimuthProbe.MeasureRod</c>'s centroid read onto the spade measured −5.76 px
        /// at east and +4.76 px at west — correctly mirrored, but one side under the rod's own
        /// 5.0 px floor, so <c>MeasureRod</c> would have thrown INCONCLUSIVE on art that is
        /// perfectly legible. The cause is structural, not incidental: a spade is TWO-ENDED, with
        /// the D-grip 0.375 m behind the pivot dragging mass back across it, where a rod's whole
        /// blank lies on the heading side of its grip.
        ///
        /// <para>Asserted on synthetic bytes rather than on the rig, so it states the rule instead
        /// of the artwork: a silhouette with a long arm one way and a short arm the other reads the
        /// LONG one, even when the short arm carries more mass and drags the centroid to the wrong
        /// side of the pivot. That is precisely the shape a spade makes, and precisely the case a
        /// centroid gets backwards.</para>
        /// </summary>
        [Test]
        public void TheBladeRead_TakesTheFarExtreme_WhereACentroidWouldReadTheHeavyShortEnd()
        {
            const int w = 40, h = 8;
            var rgba = new byte[w * h * 4];
            void Ink(int x, int y) => rgba[(y * w + x) * 4 + 3] = 255;

            // pivot column 20. A thin blade arm reaching far RIGHT (to x=34), and a short, FAT
            // grip block just left of the pivot (x 14..19, all 8 rows) that outweighs it.
            for (int x = 21; x <= 34; x++) Ink(x, 4);
            for (int x = 14; x <= 19; x++)
                for (int y = 0; y < h; y++) Ink(x, y);

            var read = ShovelRigAzimuthProbe.ReadBlade(rgba, w, h);

            Assert.Less(read.CentroidX, 20.0,
                        "premise: the fat short end really does pull the centroid to the WRONG side " +
                        "of the pivot — without this the test would not discriminate the two reads");
            Assert.Greater(read.BladeOffset(20.0), 0.0,
                           "the far extreme is the blade, and it is to the right of the grip");
            Assert.AreEqual(14.0, read.BladeOffset(20.0), 1e-9, "x=34 is 14 px right of the pivot");
        }

        [Test]
        public void ABlankCell_ReadsAsNoBladeRatherThanAsAnOffset()
        {
            var read = ShovelRigAzimuthProbe.ReadBlade(new byte[16 * 16 * 4], 16, 16);
            Assert.AreEqual(0, read.OpaquePixels);
            Assert.AreEqual(0.0, read.BladeOffset(8.0), 1e-9,
                            "an empty silhouette must report NO side, not side zero-by-accident");
        }

        // ---- the dig contract ---------------------------------------------------------------

        /// <summary>
        /// The two rigs must agree about how long a dig is. <c>ShovelIso.DIG.frames</c> is the
        /// spade's own claim; <c>CharacterIso.ANIMS.dig.frames</c> is what the engine animates from.
        /// A sheet baked to the wrong one has a column nothing plays, or a frame with no art.
        /// </summary>
        [Test]
        public void TheSpadeAndTheClipThatSwingsIt_AgreeAboutTheLengthOfADig()
        {
            using var host = RigScriptHostFactory.Create();
            var shovel = RigCatalog.Get("shovel");
            var character = RigCatalog.Get("character");
            RigCatalog.Install(host, shovel);
            RigCatalog.Install(host, character);

            int rig = (int)host.EvaluateNumber($"{shovel.GlobalName}.DIG.frames");
            int clip = CharacterRigBaker.FramesOf(host, character.GlobalName, ShovelKitBaker.DigAnim);

            Assert.AreEqual(DigFrames, rig, "ShovelIso.DIG.frames");
            Assert.AreEqual(DigFrames, clip, "CharacterIso.ANIMS.dig.frames");
            Assert.AreEqual(rig, clip, "the spade rig and the dig clip disagree about a dig's length");

            Assert.AreEqual("shovel",
                            CharacterRigBaker.MountOf(host, character.GlobalName, ShovelKitBaker.DigAnim),
                            "ANIM_MOUNT['dig'] is what says the spade is the tool this clip drives");
        }

        /// <summary>
        /// The rig's rests are STILL props, one cell per facing — <c>ShovelIso</c> declares no
        /// <c>REST_FRAMES</c>, unlike the rod, whose rests became animated hand-overs when a
        /// one-cell rest was found to make the rod jump out of her hand. Pinned so the day the spade
        /// grows the same thing, the baker's own refusal is reached deliberately instead of a
        /// one-column sheet shipping quietly.
        /// </summary>
        [Test]
        public void TheSpadesRests_AreStillPropsAndNotYetHandOvers()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("shovel");
            RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            CollectionAssert.AreEqual(new[] { "ground", "stored" },
                FishingKitBaker.ReadStringArray(host, $"{g}.REST"),
                "the rig's own REST list — the kit reads this rather than restating it");

            Assert.IsTrue(host.EvaluateBool($"typeof {g}.REST_FRAMES === 'undefined'"),
                "ShovelIso has grown a REST_FRAMES — its rests are now animated hand-overs like the " +
                "rod's, and ShovelKitBaker still writes one still cell per facing. It refuses rather " +
                "than bakes; bake the frames instead.");
        }

        // ---- the finding: the CARRY is upstream, the DIG is not ------------------------------

        /// <summary>
        /// <b>The owner's "does not look rooted in her hand", located.</b> While she WALKS, the spade
        /// is posed by <c>CarryHands.FallbackOffset</c> — one serialized hip <c>Vector2</c>,
        /// reflected for the left hand, identical at every heading and every frame of the gait. That
        /// is not a bug in <c>CarryHands</c>: it is the documented answer for a prop with no
        /// hand-prop row, and the spade has none.
        ///
        /// <para><b>And it cannot be fixed by baking harder.</b> The character rig marks idle, walk
        /// and run <c>'free'</c> in <c>ANIM_MOUNT</c>, and <c>tool()</c> returns <b>null</b> on all
        /// three — a free arm's prop is the hands table's job. So a carried spade needs a
        /// <c>shovelTrail</c> row in <c>characterIsoRig6.hands.js</c>, which is the art director's
        /// file, and this test is the record of WHY this kit ships a dig sheet and no carry sheet.</para>
        ///
        /// <para>It goes RED the day the rig grows a tool pose on a gait, which is the day the carry
        /// becomes bakeable here and the omission should be revisited. The companion guards for the
        /// hands table itself already exist and are deliberately not duplicated:
        /// <c>CharacterHandPropAnchorTests.NoShovelRow_Exists_TheCarriedShovelIsAnOpenArtAsk</c> and
        /// <c>CarryAnchorImportTests.NoShovelRow_SoTheShovelDefNamesNoHandProp_AndTheRodNamesTheCarriedOne</c>.</para>
        /// </summary>
        [Test]
        public void TheCarriedSpadeHasNoRigAnswer_WhichIsWhyThereIsNoCarrySheet()
        {
            using var host = RigScriptHostFactory.Create();
            var character = RigCatalog.Get("character");
            RigCatalog.Install(host, character);
            string ch = character.GlobalName;

            foreach (string gait in new[] { "idle", "walk", "run" })
            {
                Assert.AreEqual("free", CharacterRigBaker.MountOf(host, ch, gait),
                                $"ANIM_MOUNT['{gait}']");
                Assert.IsTrue(host.EvaluateBool($"{ch}.tool(0,{{anim:'{gait}',frame:0}}) === null"),
                    $"tool() answered on '{gait}'. That is GOOD NEWS and this test is the reminder " +
                    "to bake the carry: ShovelKitBaker can pose a spade on a gait the moment the " +
                    "rig can, and the sheet + FisherShovelMount.json should grow a block for it.");
            }

            // The contrast, asserted in the same test so the finding cannot be misread as "the
            // spade has no anchors at all". The dig's pin is authored, present, and per-frame.
            Assert.IsTrue(
                host.EvaluateBool($"{ch}.tool(0,{{anim:'{ShovelKitBaker.DigAnim}',frame:0}}) !== null"),
                "the dig's own mount is a different, authored path — that is the half this kit bakes");
        }

        // ---- the mount sidecar, once the bake has written it ---------------------------------

        /// <summary>Where the bake writes the dig sheet — the file whose presence means "the bake
        /// has run". Named once, because two tests below branch on it.</summary>
        const string DigSheet = "Assets/_Project/Art/Fishing/Iso/Shovel_dig.png";

        static bool BakeHasRun(out string missing)
        {
            bool sheet = File.Exists(Path.Combine(RigCatalog.RepoRoot, DigSheet));
            bool sidecar = File.Exists(Path.Combine(RigCatalog.RepoRoot, ShovelKitBaker.MountSidecar));
            missing = sheet == sidecar ? null : (sheet ? ShovelKitBaker.MountSidecar : DigSheet);
            return sheet && sidecar;
        }

        /// <summary>
        /// <b>The sheet and its mount sidecar land TOGETHER, or neither does.</b> This runs
        /// unconditionally, and it is what makes the skip below a BOUNDED silence rather than an
        /// open one.
        ///
        /// <para><see cref="TheMountSidecar_DescribesTheRigsItNames"/> can only be skipped while
        /// there is genuinely nothing to guard. The failure mode it cannot cover on its own is the
        /// bake half-landing — <c>Shovel_dig.png</c> committed and <c>FisherShovelMount.json</c>
        /// forgotten (or the reverse). That would leave the sheet shipping with its mount contract
        /// permanently unasserted and the skip looking exactly as innocent as it does today, which
        /// is the shape of unguarded art nobody notices. Both are written by ONE menu click, so
        /// anything other than both-or-neither is a commit that dropped a file.</para>
        /// </summary>
        [Test]
        public void TheSheetAndItsMountSidecar_LandTogetherOrNotAtAll()
        {
            Assert.IsTrue(BakeHasRun(out string missing) || missing == null,
                $"the shovel bake half-landed: '{missing}' is missing while its other half is " +
                "committed. Hidden Harbours ▸ Art ▸ Bake Shovel Kit writes the sheets AND " +
                "FisherShovelMount.json in one operation — commit both, or neither.");
        }

        /// <summary>
        /// <c>FisherShovelMount.json</c> — the rod's sibling — checked against the live rigs rather
        /// than against itself.
        ///
        /// <para><b>⚠️ This test SKIPS before the bake, and a skip is a silence with two causes.</b>
        /// The innocent one is "the derived file does not exist yet", which is this one: the sidecar
        /// is written by the bake, the bake is one batchmode launch on an editor slot, and #805
        /// deliberately lands the kit's code and guards first. The guilty one would be "the sidecar
        /// exists and this quietly stopped reading it" — impossible here, because the branch is on
        /// <c>File.Exists</c> and <see cref="TheSheetAndItsMountSidecar_LandTogetherOrNotAtAll"/>
        /// runs unconditionally and reddens if the sheet ever ships without it.</para>
        ///
        /// <para><b>Where it RUNS with the bake in place:</b> on the machine that bakes. The PR that
        /// commits <c>Shovel_dig.png</c> commits the sidecar in the same operation and empties
        /// <c>FishingKitSheetSliceTests.AwaitingOwnerBake</c>, and from that commit on this test is
        /// live on every run including CI — nothing here is graphics-dependent, so CI's null device
        /// is not a reason it would keep skipping.</para>
        /// </summary>
        [Test]
        public void TheMountSidecar_DescribesTheRigsItNames()
        {
            string abs = Path.Combine(RigCatalog.RepoRoot, ShovelKitBaker.MountSidecar);
            if (!File.Exists(abs))
                Assert.Ignore(
                    $"SKIPPED because the DERIVED file does not exist yet: {ShovelKitBaker.MountSidecar} " +
                    "is written by Hidden Harbours ▸ Art ▸ Bake Shovel Kit, which needs an editor " +
                    "slot, and #805 lands the kit's code before its bake. This is NOT 'the mount " +
                    "contract is unguarded': TheSheetAndItsMountSidecar_LandTogetherOrNotAtAll runs " +
                    "unconditionally and fails if a Shovel_ sheet ever ships without this sidecar, so " +
                    "this test cannot stay silent once there is anything to say. Every rig, probe and " +
                    "contract guard in this class is live today and needs no bake.");

            string json = File.ReadAllText(abs);

            // Frame count = the character clip's, for the one consumed anim. The charter's acceptance
            // criterion, asserted against the RIG rather than against the number in the file.
            using var host = RigScriptHostFactory.Create();
            var shovel = RigCatalog.Get("shovel");
            var character = RigCatalog.Get("character");
            RigGeometry geo = RigCatalog.Install(host, shovel);
            RigCatalog.Install(host, character);
            int clipFrames = CharacterRigBaker.FramesOf(host, character.GlobalName,
                                                        ShovelKitBaker.DigAnim);

            StringAssert.Contains($"\"frames\": {clipFrames}", json,
                "the mount's dig block must carry the clip's own frame count");
            StringAssert.Contains($"\"w\": {geo.Width}", json, "shovelCell width from the rig");
            StringAssert.Contains($"\"h\": {geo.Height}", json, "shovelCell height from the rig");
            StringAssert.Contains("\"measuredShovelConvention\": \"CounterClockwise\"", json);
            StringAssert.Contains("\"measuredCharacterConvention\": \"Clockwise\"", json);

            // The provenance pins must describe the rigs in the working tree, LF-normalised —
            // otherwise the document is a record of a bake nobody can reproduce.
            foreach (var (relPath, label) in new[]
                     { (shovel.ScriptPath, "shovelIsoRig.js"),
                       (character.ScriptPath, "characterIsoRig6.js") })
            {
                string sha = DeckSidecarReader.Sha256HexLineEndingNormalised(
                    File.ReadAllBytes(Path.Combine(RigCatalog.RepoRoot, relPath)));
                StringAssert.Contains(sha, json,
                    $"{label}'s LF-normalised sha is not pinned — the sidecar names a rig it does " +
                    "not describe. Re-run the bake; do NOT re-stamp the hash by hand. " +
                    "⚠️ Compared as the FULL 64-char digest: a prefix check is not a hash check.");
            }

            // The one thing the document must NOT have grown quietly.
            StringAssert.Contains("\"_absent\"", json,
                "the sidecar must keep stating that it carries no carried/walk block, and why — a " +
                "reader who cannot see the absence will assume the walk was measured and is fine");
        }

        // ---- the slicer knows the grid ------------------------------------------------------

        /// <summary>
        /// The spade's slice spec exists and reads its numbers off the SPADE rig. (The rig-vs-slicer
        /// drift alarm for all four kits lives in
        /// <c>FishingKitBakeTests.SlicerKitSpecs_MatchTheLiveRigs</c>; this asserts the entry is
        /// there at all, because a <c>Shovel_*.png</c> under a root whose slicer has no prefix for
        /// it FAILS the slice rather than importing unsliced.)
        /// </summary>
        [Test]
        public void TheSlicerHasASpadeGrid_SoABakedSheetDoesNotLandUnsliced()
        {
            Assert.IsTrue(FishingSheetSlicer.Kits.ContainsKey("Shovel_"),
                "FishingSheetSlicer refuses a stem matching no kit prefix — a baked Shovel_ sheet " +
                "with no entry here fails the slice, loudly, which is the right failure but not " +
                "the one we want to ship.");

            var kit = FishingSheetSlicer.Kits["Shovel_"];
            Assert.AreEqual(Dirs, kit.Rows, "eight facing rows");
            Assert.AreEqual(CellW, kit.Cell.x);
            Assert.AreEqual(CellH, kit.Cell.y);
        }
    }
}
