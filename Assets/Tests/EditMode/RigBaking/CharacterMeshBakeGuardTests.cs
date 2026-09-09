using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The guards ADR 0044 owes for PR 1 — the mesh side of the character bake.
    ///
    /// <para>Five questions, each asked the way this repo has learned to ask it:</para>
    /// <list type="number">
    /// <item>the rig SOURCE is untouched (the widening is in memory, and only in memory);</item>
    /// <item>the turntable sign is ADJUDICATED FROM PIXELS with a sabotage margin, and the two
    ///       independent probes of one rig agree;</item>
    /// <item>a pose is heading-INDEPENDENT — the flipbook's whole premise;</item>
    /// <item>the facet oracle reproduces the rig across all 8 directions for one frame of EVERY
    ///       state, within the delta ADR 0044 §3.3 measured;</item>
    /// <item>the recipe and the Def can carry what the bake will put in them.</item>
    /// </list>
    ///
    /// <para><b>Read the fidelity numbers against ADR 0044 §3.3, never against the hull band, and
    /// read the OUTLINE before the colour.</b> Hulls sit at 2.47–4.81% because the facet shader
    /// carries the one gain/bias their rigs use. Rig 6 gives 27 of its 36 materials their own gain,
    /// and until the shader carries per-material gain (an open question for the seat, ADR 0044 §5.2)
    /// the character's honest SHADING residual is 59.38–79.34%, measured. A guard that asserted the
    /// hull band here would be asserting a shader change this PR does not make. What this PR CAN be
    /// held to is where the figure is: the outline residual is 0.00–4.98% across the same 352
    /// probes, and that is the bar the golden fixture leans on.</para>
    /// </summary>
    public class CharacterMeshBakeGuardTests
    {
        // ⚠️ THE BARS BELOW ARE MEASURED, AND THEY REPLACE A PROXY. Their first version came from
        // ADR 0044 §3.3's 44.85–56.63% band, which was measured rig-JS against rig-JS with one
        // expression patched out per term, worst of 20 probes — a MODEL of what the facet oracle
        // would lose, never the oracle itself. CI run 34356893050 is the first time the C# oracle
        // was ever compared against the rig, over the whole recipe: 44 states × 8 dirs = 352
        // probes. What that measured, and what the bars below are derived from:
        //
        //     shading delta   59.38% .. 79.34%   median 65.68%, sd 4.30   — EVERY probe above 59%
        //     outline delta    0.00% ..  4.98%   median  2.31%
        //
        // There is no outlier in that. The worst state ('balance', 79.34%) sits 2.2 sd above the
        // mean of the 44 per-state worsts, at the top of one tight unimodal band. The old 60%
        // ceiling was not exceeded by one state — it sat BELOW the entire distribution.

        /// <summary>
        /// <b>The load-bearing bar.</b> The OUTLINE — opaque against transparent — is what a bake
        /// can actually get wrong: a pose resolved against the wrong build, a heading applied
        /// twice, a mirrored turntable all MOVE the figure. Shading cannot move a silhouette.
        /// Measured max over 352 probes: <b>4.98%</b>. Measured cost of a real geometry defect,
        /// from the mirrored pose <c>MeasureFacetSign</c> rejects: <b>78/357 px = 21.8%</b>. This
        /// bar sits between them with better than 1.6× of margin on each side — above every probe
        /// measured, below the smallest geometry error this lane has evidence of.
        /// </summary>
        const double OutlineCeilingPercent = 8.0;

        /// <summary>
        /// The SHADING delta, kept as a band and not as an acceptance criterion, because what it
        /// measures is a shader this PR does not change: one global gain against rig 6's 27
        /// per-material ones (§3.3, and the seat's PR-2 widening question). Measured max 79.34%,
        /// sd 4.30 — this is two sd of headroom. Above it a NEW loss has joined the four known
        /// ones (head stamp, gridHead, dither, flattened gain): find which.
        /// </summary>
        const double ShadeDeltaCeilingPercent = 88.0;

        /// <summary>And a FLOOR, well under the measured 59.38% minimum: if the shading delta
        /// collapses, either somebody landed the per-material-gain widening (good — re-baseline
        /// this file and ADR 0044 §3.3 in the same PR) or the two rasters stopped being
        /// compared.</summary>
        const double ShadeDeltaFloorPercent = 40.0;

        const string Player = CharacterRigBakeMenu.PlayerPreset;

        // ---- 1. the rig source is untouched ---------------------------------------------------

        /// <summary>
        /// `docs/art/rigs/**` is the ART-DIRECTOR's. This lane needs one module-private symbol
        /// (<c>resolveOpts</c>) and takes it the sanctioned way: an in-memory widening applied to a
        /// COPY of the source on its way into V8. The bytes on disk must be exactly what the
        /// art-director committed — before the widening and after it.
        /// </summary>
        [Test]
        public void TheWideningIsInMemoryOnly_TheRigBytesOnDiskAreUntouched()
        {
            RigEntry entry = CharacterPoseMeshExtractor.Entry;
            string full = Path.Combine(RigCatalog.RepoRoot, entry.ScriptPath);
            Assert.IsTrue(File.Exists(full), $"the character rig is missing at {entry.ScriptPath}");

            byte[] before = File.ReadAllBytes(full);
            string sha = CharacterPoseMeshExtractor.SourceSha256();

            string source = RigCatalog.ReadSource(entry);
            string widened = RigMeshExtractor.ApplyInnerWidenings(source, entry.ScriptPath);

            byte[] after = File.ReadAllBytes(full);
            CollectionAssert.AreEqual(before, after,
                "applying the widening changed the rig ON DISK. The widening exists precisely so " +
                "that never happens — a rig edit is the art-director's, not this lane's.");
            Assert.AreEqual(sha, CharacterPoseMeshExtractor.SourceSha256(),
                "the rig's content hash moved during the test");

            Assert.AreNotEqual(source, widened,
                "the widening did nothing. The anchor is `const API = {` and it must match exactly " +
                "once; if the rig's export shape changed, the extractor's assert would fire at load " +
                "instead — but a silent no-op here means the bake would run against the UNWIDENED " +
                "module and fall back to whatever `resolveOpts` it could see.");
            Assert.AreEqual(source.Length + " resolveOpts,".Length, widened.Length,
                "the widening inserted something other than the one documented literal");
        }

        /// <summary>
        /// The stale-bake guard's raw material. A Def stores this hash; a bake against a moved rig
        /// must be visible as a mismatch rather than as art that is quietly one drop old.
        /// </summary>
        [Test]
        public void TheSourceHash_IsStable_AndIndependentOfLineEndings()
        {
            string sha = CharacterPoseMeshExtractor.SourceSha256();
            Assert.AreEqual(64, sha.Length, "a SHA-256 is 64 hex characters");
            StringAssert.IsMatch("^[0-9a-f]{64}$", sha, "the hash must be lowercase hex");
            Assert.AreEqual(sha, CharacterPoseMeshExtractor.SourceSha256(), "the hash is not stable");

            // The hash is taken over LF-normalised bytes on purpose: a checkout with different
            // autocrlf settings must not read as a moved rig.
            string text = RigCatalog.ReadSource(CharacterPoseMeshExtractor.Entry);
            byte[] lf = Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n"));
            using var h = SHA256.Create();
            var sb = new StringBuilder(64);
            foreach (byte b in h.ComputeHash(lf)) sb.Append(b.ToString("x2"));
            Assert.AreEqual(sb.ToString(), sha,
                "the extractor's hash is not the LF-normalised hash of the source it reads — a " +
                "Windows checkout and a CI checkout would then disagree about whether the rig moved");
        }

        // ---- 2. the turntable sign, from pixels ------------------------------------------------

        /// <summary>
        /// The trap this lane exists to not fall into a third time. The character rig turns
        /// <c>th = −dir·π/4</c>; boats and <c>IsoFacetMath</c> use <c>+dir·π/4</c>. Nobody DECLARES
        /// which way it goes: the bake renders the rig's East view and asks which signed oracle dir
        /// reproduces it, and refuses unless the loser is at least 4× worse.
        ///
        /// <para><b>"Worse" is measured on the SILHOUETTE</b> — opaque-vs-transparent — because
        /// handedness is a question about where the figure is, and the facet model's own 45–57%
        /// shading delta (per-material gain, forced dither, the head stamp, the gridHead nudge)
        /// drowns that question out of any inked-colour statistic: the same pair of renders reads
        /// 77.95% against 90.76% by colour (1.16×, no signal) and 7 px against 78 px by coverage
        /// (11.1×). Shading cannot move an outline; a mirrored pose moves it everywhere.</para>
        ///
        /// <para>Two independent probes read one rig here. <c>CharacterRigAzimuthProbe</c> reads
        /// face-offset asymmetry out of the rig's own SHEET renders (what the sprite bake uses);
        /// <c>MeasureFacetSign</c> diffs the FACET oracle's raster against the rig's. They measure
        /// the same handedness in two vocabularies, so they must agree — and if they ever stop
        /// agreeing, one of the two broke, which is a thing worth being told loudly.</para>
        /// </summary>
        [Test]
        public void TheFacetSign_IsAdjudicatedFromPixels_AndTheSheetProbeAgrees()
        {
            using var host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.Load(host);

            bool facetNegates = CharacterMeshAssetBaker.MeasureFacetSign(host, Player, out string report);
            Debug.Log("[character-mesh] facet sign adjudication:\n" + report);

            // The adjudication itself throws below a 4× silhouette margin, so reaching here IS the
            // margin assertion — but say so out loud, because a future refactor could soften it.
            StringAssert.Contains("facet sign:", report, "the adjudication did not report its reading");

            // A SECOND host on purpose: RigCatalog.Install re-executes the UNWIDENED source, and
            // the sheet probe must read the rig the sprite bake reads, not this lane's copy.
            RigEntry entry = CharacterPoseMeshExtractor.Entry;
            using var sheetHost = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(sheetHost, entry);
            var sheetProbe = CharacterRigAzimuthProbe.Measure(sheetHost, entry.GlobalName, geo);
            Debug.Log($"[character-mesh] sheet-side probe says {sheetProbe.Convention}\n{sheetProbe.Report}");

            Assert.AreEqual(entry.DeclaredConvention, sheetProbe.Convention,
                "the catalog and the sheet probe disagree — the SPRITE bake would already be " +
                "refusing. Fix that before reading anything the mesh side says.");

            Assert.AreEqual(sheetProbe.Convention == AzimuthConvention.Clockwise, facetNegates,
                "the two probes of this ONE rig disagree about which way it turns.\n" +
                "The relation they must satisfy: the rig poses with th = −dir·π/4 while the facet " +
                "oracle poses with +dir·π/4, so a rig that reads CLOCKWISE to the sheet probe is " +
                "exactly the rig whose faces the oracle must pose at a NEGATED dir. If this reds, " +
                "do NOT flip the expectation to match — find out which probe moved, and put the " +
                "answer in ADR 0044 §6.");
        }

        // ---- 3. a pose is heading-independent ---------------------------------------------------

        /// <summary>
        /// The flipbook's whole premise: one mesh per FRAME, and heading applied as a live
        /// transform. If <c>pose()</c> leaked the direction into the geometry, every clip would need
        /// eight meshes instead of one and the 44 MB of §3.2 would be 350 MB — and, worse, the
        /// rotation would double-count and she would face the wrong way at every heading but one.
        /// </summary>
        [Test]
        public void APoseIsTheSameGeometryAtEveryHeading()
        {
            using var host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.Load(host);

            foreach (string anim in new[] { "idle", "walk", "cast" })
            {
                RigMeshData a = CharacterPoseMeshExtractor.ExtractPose(host, Player, anim, 0, dir: 0);
                RigMeshData b = CharacterPoseMeshExtractor.ExtractPose(host, Player, anim, 0, dir: 4);

                Assert.AreEqual(a.Faces.Count, b.Faces.Count,
                    $"'{anim}' produced a different number of faces at dir 0 and dir 4");
                for (int i = 0; i < a.Faces.Count; i++)
                {
                    RigFace fa = a.Faces[i], fb = b.Faces[i];
                    Assert.AreEqual(fa.Mat, fb.Mat, $"'{anim}' face {i}: material differs by heading");
                    Assert.AreEqual(fa.V.Length, fb.V.Length, $"'{anim}' face {i}: vertex count differs");
                    for (int v = 0; v < fa.V.Length; v++)
                    {
                        Assert.AreEqual(fa.V[v].X, fb.V[v].X, 1e-12, $"'{anim}' face {i} vert {v}.x");
                        Assert.AreEqual(fa.V[v].Y, fb.V[v].Y, 1e-12, $"'{anim}' face {i} vert {v}.y");
                        Assert.AreEqual(fa.V[v].Z, fb.V[v].Z, 1e-12, $"'{anim}' face {i} vert {v}.z");
                    }
                }
            }
        }

        // ---- 4. golden fidelity, 8 dirs, one frame of every state -------------------------------

        /// <summary>
        /// One frame of EVERY state in the player recipe, rendered by the rig and by the facet
        /// oracle at all eight cardinal directions. This is the fixture that would catch a pose
        /// resolved against the wrong build, a material index handed to the wrong ramp, or a
        /// heading applied twice — none of which the budget numbers can see.
        /// </summary>
        [Test]
        public void EveryStateReproducesTheRig_AcrossAllEightDirections()
        {
            using var host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.Load(host);

            bool ccw = CharacterMeshAssetBaker.MeasureFacetSign(host, Player, out _);
            IReadOnlyList<CharacterState> recipe = CharacterMeshAssetBaker.Recipe(host);
            Assert.Greater(recipe.Count, 30, "the player recipe collapsed — the states are the test");

            var report = new StringBuilder();
            double worst = 0, worstOutline = 0;
            string worstState = null, worstOutlineState = null;

            foreach (CharacterState state in recipe)
            {
                double d = CharacterMeshAssetBaker.GoldenReport(
                    host, Player, state, 0, ccw, report, out double outline);
                if (d > worst) { worst = d; worstState = state.Key; }
                if (outline > worstOutline) { worstOutline = outline; worstOutlineState = state.Key; }
            }

            Debug.Log($"[character-mesh] golden across {recipe.Count} states × 8 dirs, " +
                      $"worst shading {worst:F2}% at '{worstState}', " +
                      $"worst outline {worstOutline:F2}% at '{worstOutlineState}'\n{report}");

            // The OUTLINE first: of the two it is the one that says the mesh is right.
            Assert.Less(worstOutline, OutlineCeilingPercent,
                $"'{worstOutlineState}' misses the rig's OUTLINE by {worstOutline:F2}%, above the " +
                $"{OutlineCeilingPercent}% bar. Shading cannot move a silhouette, so this is " +
                "GEOMETRY: a pose resolved against the wrong build, a heading applied twice, or a " +
                "turntable sign that flipped. The measured band over 352 probes is 0.00–4.98%; a " +
                "mirrored pose costs 21.8%. Do not raise this bar — find what moved the figure.");

            Assert.Less(worst, ShadeDeltaCeilingPercent,
                $"'{worstState}' differs by {worst:F2}%, above the {ShadeDeltaCeilingPercent}% " +
                "ceiling — two sd above the 59.38–79.34% band measured over 352 probes. That is a " +
                "NEW loss on top of the four known ones (head stamp, gridHead, dither, flattened " +
                "gain) — find which grew.");
            Assert.Greater(worst, ShadeDeltaFloorPercent,
                $"the worst state differs by only {worst:F2}%, well under the measured 59.38% " +
                "floor. Either the per-material-gain widening landed (good — re-baseline this file " +
                "AND ADR 0044 §3.3 in that PR) or the two rasters stopped being compared.");
        }

        // ---- 5. the recipe, the ramp table, the Def ---------------------------------------------

        /// <summary>
        /// ADR 0041's retirement law only means something if both paths enumerate ONE state list.
        /// The mesh recipe is the sheet baker's states, grown by the sheet baker's own
        /// <c>ExpandCarryStances</c>, plus the four off-deck anims the sheet baker declines purely
        /// because they do not fit its 64×88 cell. A second expansion here would drift the day the
        /// rig adds a stance, and the drift would read as "the mesh path is missing art".
        /// </summary>
        [Test]
        public void TheMeshRecipeIsTheSheetRecipe_PlusTheAnimsOnlyAMeshCanCarry()
        {
            using var host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.Load(host);

            IReadOnlyList<CharacterState> recipe = CharacterMeshAssetBaker.Recipe(host);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterState s in recipe)
                Assert.IsTrue(keys.Add(s.Key), $"the recipe lists '{s.Key}' twice");

            foreach (CharacterState s in CharacterRigBakeMenu.PlayerStates)
                Assert.IsTrue(keys.Contains(s.Key),
                    $"the sheet baker bakes '{s.Key}' and the mesh recipe does not. Parity is not a " +
                    "question two different recipes can answer — ADR 0041 retires per STATE.");

            foreach (string anim in CharacterMeshAssetBaker.OffDeckAnims)
                Assert.IsTrue(keys.Contains(anim),
                    $"'{anim}' is declined by the sheet baker only because it does not fit the " +
                    "64×88 off-deck cell. A mesh has no cell, so the mesh path must carry it.");

            Assert.Greater(recipe.Count, CharacterRigBakeMenu.PlayerStates.Length,
                "the carry stances did not expand — the recipe is not growing from the rig's CARRIES");
        }

        /// <summary>
        /// <c>CharacterMeshDef.RampSlots</c> is 16 and the facet shader's <c>_RampMeta</c> array is
        /// the reason. The PLAYER fits in 12. Two of the cast do NOT (deckboss 17, packer 17) — the
        /// baker refuses them rather than truncating, and ADR 0044 §5.3 puts that decision on the
        /// seat. This guard proves the player is safe and that the refusal is real.
        /// </summary>
        [Test]
        public void ThePlayersMaterialsFitTheRampTable()
        {
            using var host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.Load(host);

            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterState state in CharacterMeshAssetBaker.Recipe(host))
            {
                RigMeshData pose = CharacterPoseMeshExtractor.ExtractPose(host, Player, state, 0);
                foreach (RigMaterial m in pose.Materials) used.Add(m.Name);
            }

            var names = new List<string>(used);
            names.Sort(StringComparer.Ordinal);
            Debug.Log($"[character-mesh] {Player} uses {used.Count} materials: {string.Join(" ", names)}");

            Assert.LessOrEqual(used.Count, CharacterMeshDef.RampSlots,
                $"the player's flipbook needs {used.Count} ramps and the shader carries " +
                $"{CharacterMeshDef.RampSlots}. Widening the table is a WATER-lane change (ADR 0044 §5.2/§5.3).");
        }

        /// <summary>
        /// <c>IsUsable()</c> is what stands between a half-filled Def and a character drawn as
        /// nothing. Pin what it actually refuses, with no host and no rig — a Def's shape is a Core
        /// question.
        /// </summary>
        [Test]
        public void AnIncompleteDefIsNotUsable()
        {
            var def = ScriptableObject.CreateInstance<CharacterMeshDef>();
            try
            {
                Assert.IsFalse(def.IsUsable(), "an empty Def must not read as usable");
                Assert.AreEqual(0, def.TotalMeshes);
                Assert.IsFalse(def.DrawsAsMesh("walk"),
                    "MeshStates is empty in PR 1 — every state still draws from its sheet, and the " +
                    "flip is PR 3's, one state at a time, each at the owner's eye.");
                Assert.IsFalse(def.TryGetClip("walk", out _));

                def.Clips = new[]
                {
                    new CharacterMeshDef.PoseClip { Anim = "walk", State = "", Frames = new Mesh[0] },
                };
                Assert.IsFalse(def.IsUsable(),
                    "a clip with no frames and no state key must not read as usable — that is what " +
                    "a bake looks like when the rig answered but the mesh build did not.");
            }
            finally { ScriptableObject.DestroyImmediate(def); }
        }

        /// <summary>
        /// The id convention (`type.snake_case`, append-only and stable — CLAUDE.md §5) and the one
        /// asset path the bake writes. Cheap, and it catches a rename that would orphan every
        /// committed Def.
        /// </summary>
        [Test]
        public void TheDefIdAndPathFollowTheConventions()
        {
            Assert.AreEqual("charmesh.fisher", CharacterMeshAssetBaker.IdFor("fisher"));
            Assert.AreEqual("Assets/_Project/Data/Characters/fisher.asset",
                            CharacterMeshAssetBaker.AssetPathFor("fisher"));
            Assert.AreEqual("fisher", Player,
                "the player preset moved; every plate, number and Def in ADR 0044 names the fisher");
        }
    }
}
