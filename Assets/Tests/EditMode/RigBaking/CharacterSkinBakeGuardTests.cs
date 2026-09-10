using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The guards ADR 0044 owes for the SKINNED half of the character bake — one bind mesh, one
    /// skeleton, one clip per <c>ANIMS</c> row (option (d)).
    ///
    /// <para><b>What this file is NOT.</b> <c>CharacterSkinnedExportTests</c> already guards the
    /// RIG's side of the claim: rig 7's own <c>goldenReport</c>, the byte-identical sprite render,
    /// and the 452× cost of collapsing the two-weight rings — all measured in JavaScript, on
    /// numbers that never left the rig. Every one of those can be green while the DEF is wrong,
    /// because between them and the def sit a quantisation to float32, a parent-index remap, a
    /// frame-major flattening and a matrix inversion. This file guards THAT: what
    /// <see cref="CharacterSkinAssetBaker.Compose"/> actually wrote down.</para>
    ///
    /// <para><b>The oracle is rig 6, and it is untouched by this lane.</b> The sweep below poses
    /// the def's bind mesh with the def's own bones, weights, bindposes and clip keys — through
    /// Unity's <see cref="Matrix4x4"/> and <see cref="Quaternion"/>, the same
    /// <c>Σ wᵢ · (worldᵢ · bindposeᵢ) · v</c> a <see cref="SkinnedMeshRenderer"/> evaluates — and
    /// compares against the face list <c>CharacterPoseMeshExtractor</c> reads out of
    /// <c>CharacterIso6.facesOf</c>. Nothing in the comparison is a second copy of the baker's
    /// arithmetic; the baker never skins anything.</para>
    ///
    /// <para><b>Why it composes instead of loading an asset.</b> Nothing can bake the def until the
    /// editor slot is granted, so there is no <c>.asset</c> in the repo yet to read. The fixture
    /// calls the shipped <see cref="CharacterSkinAssetBaker.Compose"/> — the disk-free half of the
    /// bake — rather than a test-local transcription that could be wrong in exactly the way the
    /// baker is and agree with it perfectly. When the asset does land, this file keeps working
    /// unchanged and gains a second question worth asking: does the committed asset still equal
    /// what Compose produces today?</para>
    /// </summary>
    public class CharacterSkinBakeGuardTests
    {
        const string Player = CharacterRigBakeMenu.PlayerPreset;

        /// <summary>ADR 0044 §3.2's flipbook, in KB — what option (d) is measured against.</summary>
        const double FlipbookKb = CharacterSkinAssetBaker.FlipbookKilobytes;

        /// <summary>
        /// The composed preset, built once. Compose runs the rig, reads 45 bones, 2,992 weighted
        /// corners and 35 clips, and renders two turntable probes; at roughly two seconds a call
        /// there is no reason for nine tests to pay for it nine times.
        /// </summary>
        IRigScriptHost _host;
        CharacterSkinAssetBaker.SkinBake _bake;
        CharacterSkinDef _def;

        [OneTimeSetUp]
        public void ComposeOnce()
        {
            _host = RigScriptHostFactory.Create();
            _bake = CharacterSkinAssetBaker.Compose(_host, Player);
            _def = _bake.Def;
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            // The def and its bind mesh are loose objects no asset owns — leaving them behind
            // leaks a Mesh into the editor for the rest of the run.
            if (_def != null)
            {
                if (_def.BindMesh != null) UnityEngine.Object.DestroyImmediate(_def.BindMesh);
                UnityEngine.Object.DestroyImmediate(_def);
            }
            _host?.Dispose();
            _host = null; _bake = null; _def = null;
        }

        // =======================================================================================
        // 1. the load-bearing one: the def poses the rig's own geometry
        // =======================================================================================

        /// <summary>
        /// <b>Every clip, every frame, every drawn vertex, against rig 6.</b> The bar is the rig's
        /// OWN tolerance (<c>CharacterIso7.TOL</c>, 1e-4 m) read off the rig rather than spelled
        /// here, so a drop that tightens it tightens this guard with it.
        ///
        /// <para><b>Parked faces are skipped exactly as the rig skips them.</b> Two topologies wear
        /// one name: 711 faces in the 308 posed frames, 705 in swim/tread/sleep/drive, the
        /// difference being the six-face <c>overD</c> inseam panel. The bind mesh is the UNION, so
        /// on a parked clip rig 6 emits FEWER faces than the mesh carries and the two lists stop
        /// being index-aligned after the panel. The walk below carries a separate cursor into the
        /// reference and advances it only on a drawn face — the same rule
        /// <c>goldenDiffDetail</c> uses (rig 7 :497). The cursor landing exactly on the end of the
        /// reference list is itself an assertion, and it is the one that catches a def whose
        /// <c>ParkedParts</c> claims a panel is parked on a frame where the rig draws it.</para>
        ///
        /// <para><b>Materials are compared by NAME, never by index.</b> <c>ExtractPose</c> emits
        /// only the materials THAT pose references, in the rig's MATS order, so face 400's
        /// <c>Mat</c> is an index into a table that differs between two poses of one figure.
        /// Comparing the indices would pass on frames where the tables happen to align and fail on
        /// the rest, for a reason having nothing to do with skinning.</para>
        /// </summary>
        [Test]
        public void TheDefPosesTheRigsOwnGeometry_OnEveryClipRowAndEveryFrame()
        {
            double tol = _bake.Tolerance;
            var bindFaces = _bake.Bind.Faces;
            Vector3[] bindVerts = _def.BindMesh.vertices;
            BoneWeight[] weights = _def.BindMesh.boneWeights;

            var sw = Stopwatch.StartNew();
            double worst = 0; string worstAt = "none";
            int rows = 0, frames = 0, compared = 0, parkedTotal = 0;

            foreach (CharacterSkinDef.SkinClip clip in _def.Clips)
            {
                var parked = new HashSet<string>(clip.ParkedParts ?? Array.Empty<string>(),
                                                 StringComparer.Ordinal);
                rows++;

                for (int frame = 0; frame < clip.FrameCount; frame++)
                {
                    RigMeshData reference = CharacterPoseMeshExtractor.ExtractPose(
                        _host, Player, clip.Anim, frame, Nz(clip.Carry), Nz(clip.Power));
                    Matrix4x4[] skin = SkinMatrices(_def, clip, frame);
                    frames++;

                    int corner = 0, j = 0;
                    for (int f = 0; f < bindFaces.Count; f++)
                    {
                        RigFace bf = bindFaces[f];
                        if (parked.Contains(_bake.Skin.FacePart[f]))
                        {
                            corner += bf.V.Length; parkedTotal++; continue;
                        }

                        Assert.Less(j, reference.Faces.Count,
                            $"{clip.Anim}[{frame}]: the bind mesh carries drawn face {f} " +
                            $"('{_bake.Skin.FacePart[f]}') that rig 6 does not emit at all.");
                        RigFace rf = reference.Faces[j++];

                        Assert.AreEqual(bf.V.Length, rf.V.Length,
                            $"{clip.Anim}[{frame}] face {f} ('{_bake.Skin.FacePart[f]}'): the bind " +
                            "mesh and rig 6 disagree about how many corners this face has, so the " +
                            "two face lists have gone out of step and every distance after this " +
                            "one is meaningless.");
                        Assert.AreEqual(_bake.Bind.Materials[bf.Mat].Name, reference.Materials[rf.Mat].Name,
                            $"{clip.Anim}[{frame}] face {f}: the bind mesh paints this face " +
                            $"'{_bake.Bind.Materials[bf.Mat].Name}' and rig 6 paints it " +
                            $"'{reference.Materials[rf.Mat].Name}' — the lists are misaligned.");

                        for (int k = 0; k < bf.V.Length; k++)
                        {
                            Vector3 posed = SkinVertex(bindVerts[corner + k], weights[corner + k],
                                                       skin, _def.MaxInfluences);
                            double d = Vector3.Distance(posed, rf.V[k].ToVector3());
                            if (d > worst)
                            {
                                worst = d;
                                worstAt = $"{clip.Anim}[{frame}] face {f} " +
                                          $"('{_bake.Skin.FacePart[f]}') corner {k}";
                            }
                            compared++;
                        }
                        corner += bf.V.Length;
                    }

                    Assert.AreEqual(reference.Faces.Count, j,
                        $"{clip.Anim}[{frame}]: rig 6 emitted {reference.Faces.Count} faces and the " +
                        $"def accounted for {j}. The clip's ParkedParts " +
                        $"([{string.Join(", ", clip.ParkedParts ?? Array.Empty<string>())}]) does not " +
                        "describe what the rig actually draws on this FRAME — parking is per frame " +
                        "in the rig (S.inseamDrawn) and per clip in the def, and this is the " +
                        "assertion that catches the two disagreeing.");
                }
            }
            sw.Stop();

            Debug.Log(
                $"[char-skin guard] the def replayed through Unity's skinning, against rig 6:\n" +
                $"  {rows} clips, {frames} frames, {compared:N0} vertex comparisons, " +
                $"{parkedTotal:N0} parked-face skips\n" +
                $"  worst {worst:E3} m at {worstAt}\n" +
                $"  tolerance {tol:E1} m (CharacterIso7.TOL) — margin {tol / Math.Max(worst, 1e-18):N0}×\n" +
                $"  sweep {sw.ElapsedMilliseconds:N0} ms");

            Assert.AreEqual(_def.Clips.Length, rows, "a clip was skipped");
            Assert.AreEqual(_bake.TotalFrames, frames, "a frame was skipped");
            Assert.Greater(compared, 0, "nothing was compared");
            Assert.LessOrEqual(worst, tol,
                $"the def does not reproduce the rig: worst {worst:E3} m at {worstAt}, against a " +
                $"tolerance of {tol:E1} m.\nThe def's numbers are what moved — rig 7's own golden " +
                "is guarded separately in CharacterSkinnedExportTests, so if THAT is green and " +
                "this is red the loss is in the transcription: the bindpose inversion, the " +
                "parent-index remap, the frame-major flattening, or the float32 quantisation.");
        }

        // =======================================================================================
        // 2. the mesh Unity will hand the GPU is the mesh the weights were measured on
        // =======================================================================================

        /// <summary>
        /// The weights are attached by INDEX to a mesh a different class built. If
        /// <see cref="RigMeshBuilder"/> ever welds, reorders or drops a corner, every weight lands
        /// on the wrong vertex — all 2,992 of them, each by a different amount, and nothing throws.
        /// So the identity is asserted rather than assumed: vertex <c>c</c> of the asset is corner
        /// <c>c</c> of the rig's bind mesh, bit for bit, because both sides are the same float.
        ///
        /// <para>Note this is deliberately an EXACT comparison, not a tolerance. Both sides are
        /// <c>(float)</c> casts of the same double, so any difference at all means a different
        /// vertex — not a rounding difference.</para>
        /// </summary>
        [Test]
        public void TheBindMeshVertexOrderIsTheRigsCornerOrder()
        {
            Vector3[] verts = _def.BindMesh.vertices;
            BoneWeight[] weights = _def.BindMesh.boneWeights;
            Matrix4x4[] bindposes = _def.BindMesh.bindposes;
            RigSkinning skin = _bake.Skin;

            Assert.AreEqual(skin.CornerCount, verts.Length,
                "the asset has a different vertex count from the rig's skinned corner list");
            Assert.AreEqual(verts.Length, weights.Length,
                "every vertex must carry a weight — Unity pads silently and the padding is bone 0");
            Assert.AreEqual(_def.Bones.Length, bindposes.Length,
                "one bindpose per bone, or Unity indexes past the end of the array");

            int mismatches = 0; double worst = 0; int worstAt = -1;
            for (int c = 0; c < verts.Length; c++)
            {
                Vector3 rig = skin.Position[c].ToVector3();
                if (verts[c] != rig)
                {
                    mismatches++;
                    double d = Vector3.Distance(verts[c], rig);
                    if (d > worst) { worst = d; worstAt = c; }
                }
            }

            Debug.Log($"[char-skin guard] vertex order: {verts.Length:N0} corners, " +
                      $"{mismatches} mismatched, worst {worst:E3} m at corner {worstAt}");

            Assert.AreEqual(0, mismatches,
                $"{mismatches} of {verts.Length} asset vertices are not the rig's corner at the " +
                $"same index (worst {worst:E3} m at {worstAt}). The bone weights are attached BY " +
                "INDEX, so this is not a geometry difference — it is every weight on the wrong " +
                "vertex.");

            // A face's corners are emitted contiguously and in order, which is what lets the sweep
            // above walk one cursor through both lists.
            int expected = 0;
            foreach (RigFace f in _bake.Bind.Faces) expected += f.V.Length;
            Assert.AreEqual(expected, verts.Length,
                "the builder did not emit exactly one vertex per face corner");
        }

        // =======================================================================================
        // 3. both rigs are pinned
        // =======================================================================================

        /// <summary>
        /// The def is baked from TWO files. <c>CharacterIso7</c> is
        /// <c>Object.create(CharacterIso6)</c>: an edit to the body moves this def's geometry, its
        /// clips and its materials without changing one byte of rig 7. A def that pinned only the
        /// file it names would go stale invisibly on every body drop.
        ///
        /// <para>The two hashes must also be taken by the SAME rule. They are: rig 7's
        /// normalisation skips a <c>\r</c> only when a <c>\n</c> follows it, byte for byte what
        /// <c>CharacterPoseMeshExtractor</c> does — because a rule that stripped every <c>\r</c>
        /// would let a lone CR move one hash and not the other, and the pin exists to catch exactly
        /// that.</para>
        /// </summary>
        [Test]
        public void TheDefPinsBothRigs_ByHashesTakenTheSameWay()
        {
            Assert.AreEqual(CharacterSkinExtractor.ScriptPath, _def.SourceRigPath);
            Assert.AreEqual(CharacterPoseMeshExtractor.ScriptPath, _def.BaseRigPath);
            Assert.AreNotEqual(_def.SourceRigPath, _def.BaseRigPath,
                "the skinned export and the body it re-expresses are two different files");

            Assert.AreEqual(CharacterSkinExtractor.SourceSha256(), _def.SourceRigSha256,
                "the def does not pin the rig 7 bytes it was baked from");
            Assert.AreEqual(CharacterPoseMeshExtractor.SourceSha256(), _def.BaseRigSha256,
                "the def does not pin the rig 6 bytes its geometry came from");
            Assert.AreNotEqual(_def.SourceRigSha256, _def.BaseRigSha256,
                "two different files hashed to the same value — the hash is reading one of them");

            foreach (string sha in new[] { _def.SourceRigSha256, _def.BaseRigSha256 })
                StringAssert.IsMatch("^[0-9a-f]{64}$", sha, "a SHA-256 is 64 lowercase hex chars");

            // The normalisation, restated independently: LF-normalise the text the catalog reads
            // and hash that. If the extractor's byte-level loop and this string-level one ever
            // disagree, a Windows checkout and a CI checkout disagree about whether the rig moved.
            Assert.AreEqual(LfSha256(RigCatalog.ReadSource(CharacterSkinExtractor.Entry)),
                            _def.SourceRigSha256,
                            "rig 7's hash is not the LF-normalised hash of the source the catalog runs");
            Assert.AreEqual(LfSha256(RigCatalog.ReadSource(CharacterPoseMeshExtractor.Entry)),
                            _def.BaseRigSha256,
                            "rig 6's hash is not the LF-normalised hash of the source the catalog runs");

            Assert.IsNotEmpty(_def.SourceRigRevision, "rig 7 must record which revision it was");
            Assert.IsNotEmpty(_def.BaseRigRevision, "rig 6 must record which revision it was");
            Debug.Log($"[char-skin guard] pinned: {_def.SourceRigPath} rev {_def.SourceRigRevision} " +
                      $"{_def.SourceRigSha256[..12]}… / {_def.BaseRigPath} rev {_def.BaseRigRevision} " +
                      $"{_def.BaseRigSha256[..12]}…");
        }

        // =======================================================================================
        // 4. the two-weight floor, sabotaged at DEF level
        // =======================================================================================

        /// <summary>
        /// <b>The one number that decides the whole shape of this def.</b> Rig 7 :533 gives the
        /// BLENDED rings — apron rows 1-4, the skirt, the <c>upper_*</c> hem, the <c>fore_*</c>
        /// bands — two influences each. One influence per vertex would halve the weight buffer and
        /// let the presenter use <c>SkinQuality.Bone1</c>, and it is the first economy anybody
        /// reaches for.
        ///
        /// <para>It costs 452× the tolerance. This test measures that at the DEF, by collapsing
        /// each blended vertex onto its heavier bone and re-running the pose — the sabotage feeds
        /// ONLY the def's copy of the weights, while rig 6 keeps answering from numbers the
        /// sabotage cannot reach. A knob that fed both sides would measure nothing.</para>
        ///
        /// <para>Consequence for the presenter, and it is not optional:
        /// <c>SkinnedMeshRenderer.quality</c> must be set to at least
        /// <see cref="SkinQuality.Bone2"/>. Unity's project default can be Bone1, and a renderer
        /// left on it would silently reproduce this failure on the shipped character.</para>
        /// </summary>
        [Test]
        public void CollapsingTheBlendedRingsToOneBone_MovesTheHemsByOrdersOfMagnitude()
        {
            Assert.AreEqual(CharacterSkinDef.MaxBoneInfluences, _def.MaxInfluences,
                "the def must carry the measured influence width, not a convenient one");

            BoneWeight[] weights = _def.BindMesh.boneWeights;
            Vector3[] verts = _def.BindMesh.vertices;

            int blended = 0;
            var collapsed = new BoneWeight[weights.Length];
            for (int c = 0; c < weights.Length; c++)
            {
                BoneWeight w = weights[c];
                if (w.weight1 > 0f)
                {
                    blended++;
                    // Onto the heavier bone, renormalised — the economy as anyone would write it.
                    if (w.weight1 > w.weight0) { w.boneIndex0 = w.boneIndex1; }
                    w.weight0 = 1f; w.boneIndex1 = 0; w.weight1 = 0f;
                }
                collapsed[c] = w;
            }

            Assert.Greater(blended, 0,
                "no vertex carries a second influence, so this def cannot be the two-weight one " +
                "rig 7 exports — either the extractor dropped the blend or the rig changed.");

            // One clip is enough and 'walk' is where the hems swing: find the worst drawn vertex.
            CharacterSkinDef.SkinClip clip = ClipNamed("walk");
            var parked = new HashSet<string>(clip.ParkedParts ?? Array.Empty<string>(), StringComparer.Ordinal);
            var bindFaces = _bake.Bind.Faces;
            double worst = 0; string at = "none";

            for (int frame = 0; frame < clip.FrameCount; frame++)
            {
                RigMeshData reference = CharacterPoseMeshExtractor.ExtractPose(
                    _host, Player, clip.Anim, frame, Nz(clip.Carry), Nz(clip.Power));
                Matrix4x4[] skin = SkinMatrices(_def, clip, frame);

                int corner = 0, j = 0;
                for (int f = 0; f < bindFaces.Count; f++)
                {
                    RigFace bf = bindFaces[f];
                    if (parked.Contains(_bake.Skin.FacePart[f])) { corner += bf.V.Length; continue; }
                    RigFace rf = reference.Faces[j++];
                    for (int k = 0; k < bf.V.Length; k++)
                    {
                        Vector3 posed = SkinVertex(verts[corner + k], collapsed[corner + k], skin, 1);
                        double d = Vector3.Distance(posed, rf.V[k].ToVector3());
                        if (d > worst) { worst = d; at = $"frame {frame} face {f} ('{_bake.Skin.FacePart[f]}') corner {k}"; }
                    }
                    corner += bf.V.Length;
                }
            }

            double tol = _bake.Tolerance;
            Debug.Log($"[char-skin guard] one-influence sabotage on 'walk': {blended} blended " +
                      $"corners of {weights.Length:N0} collapsed onto their heavier bone → " +
                      $"worst {worst:E3} m at {at}, {worst / tol:N0}× the {tol:E1} m tolerance");

            Assert.Greater(worst, tol * 100,
                $"collapsing the blended rings cost only {worst:E3} m ({worst / tol:N1}× tol). " +
                "Either the sabotage no longer reaches the weights the hems actually use, or the " +
                "rig stopped blending — and if the rig stopped blending, the def should be one " +
                "influence wide and this whole test should go. Do not relax the bar to make it " +
                "pass: rig 7's own measurement of this collapse is 4.52e-2 m.");
        }

        // =======================================================================================
        // 5. the clips are discrete samples
        // =======================================================================================

        /// <summary>
        /// <b>These keys must never be lerped or slerped.</b> The clips are not a spline sampled
        /// finely — they are the rig's own frames, and the rig moves limbs by half-turns between
        /// them: measured on 'walk', 20 of 315 adjacent bone-steps exceed 90°, the worst at 178.3°.
        /// Playback is therefore exact at every stored frame (the sweep above proves it) and
        /// arbitrarily wrong anywhere in between: an interpolator would sweep a forearm through a
        /// half-circle that the animation never contains, once per cycle, per limb.
        ///
        /// <para>This is why <see cref="CharacterSkinDef.SkinClip.Keys"/> is a flat frame-major
        /// array read through <see cref="CharacterSkinDef.SkinClip.KeyOf"/> and not an
        /// <c>AnimationClip</c>: Unity's clip playback interpolates, and there is no import setting
        /// that stops it. A presenter samples <c>KeyOf(round(t · fps), bone)</c>.</para>
        ///
        /// <para>The assertion is deliberately that a big step EXISTS. A future drop that made
        /// every step small would make interpolation harmless — and would also mean this warning
        /// has stopped being true and should be re-measured rather than inherited.</para>
        /// </summary>
        [Test]
        public void TheClipKeysAreDiscreteSamples_AndCannotBeInterpolated()
        {
            int n = _def.Bones.Length;
            double worst = 0; string at = "none"; int over90 = 0, steps = 0;

            foreach (CharacterSkinDef.SkinClip clip in _def.Clips)
                for (int frame = 1; frame < clip.FrameCount; frame++)
                    for (int b = 0; b < n; b++)
                    {
                        Quaternion a = clip.KeyOf(frame - 1, b, n).Rotation;
                        Quaternion c = clip.KeyOf(frame, b, n).Rotation;
                        double dot = Math.Abs(Quaternion.Dot(a, c));
                        double deg = 2.0 * Math.Acos(Math.Min(1.0, dot)) * Mathf.Rad2Deg;
                        steps++;
                        if (deg > 90.0) over90++;
                        if (deg > worst) { worst = deg; at = $"{clip.Anim}[{frame}] bone '{_def.Bones[b].Id}'"; }
                    }

            Debug.Log($"[char-skin guard] adjacent-frame bone steps: {steps:N0} measured, " +
                      $"{over90} over 90°, worst {worst:F1}° at {at}");

            Assert.Greater(steps, 0, "no clip carries a second frame to step to");
            Assert.Greater(worst, 90.0,
                $"the worst adjacent-frame bone step is only {worst:F1}°. These keys were measured " +
                "as DISCRETE samples with half-turn steps in them; if that has stopped being true, " +
                "re-measure it and update CharacterSkinDef's remarks and ADR 0044 — do not simply " +
                "delete this guard, because the interpolation ban rests on it.");
            Assert.AreEqual(_bake.WorstStepDegrees, (float)worst, 0.01f,
                "the bake reported a different worst step than the def actually carries");
        }

        // =======================================================================================
        // 6. the turntable sign, with its margin said out loud
        // =======================================================================================

        /// <summary>
        /// The def stores which way the figure turns, and gets it from
        /// <c>CharacterMeshAssetBaker.MeasureFacetSign</c> — the SAME adjudication the flipbook def
        /// stores, on purpose: two defs of one rig that disagreed about handedness would draw the
        /// same character facing two different ways depending on which path the presenter took.
        ///
        /// <para>The margin is the point. Handedness is adjudicated on the SILHOUETTE, and the
        /// adjudication refuses below 4×. This test reads the two pixel counts back out of the
        /// report and asserts the ratio explicitly, because "it did not throw" is a claim that a
        /// future refactor of MeasureFacetSign could quietly stop being true.</para>
        /// </summary>
        [Test]
        public void TheTurntableSignCarriesItsSabotageMargin_AndTheDefStoredIt()
        {
            bool measured = CharacterMeshAssetBaker.MeasureFacetSign(_host, Player, out string report);
            Debug.Log("[char-skin guard] turntable sign:\n" + report);

            Assert.AreEqual(measured, _def.AzimuthCounterClockwise,
                "the def stored a different sign than the adjudication returned");

            // "=> adjudicated on SILHOUETTE (opaque-vs-transparent): 7 vs 78 px"
            const string marker = "SILHOUETTE (opaque-vs-transparent):";
            int i = report.IndexOf(marker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(i, 0,
                "the adjudication no longer reports its silhouette counts — the margin below " +
                "cannot be checked, and 'it did not throw' is not a measurement.\n" + report);

            string tail = report[(i + marker.Length)..].Trim();
            string[] bits = tail.Split(new[] { " vs ", " px" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.GreaterOrEqual(bits.Length, 2, "could not read two counts from: " + tail);
            int negOut = int.Parse(bits[0].Trim());
            int posOut = int.Parse(bits[1].Trim());

            int winner = Math.Min(negOut, posOut), loser = Math.Max(negOut, posOut);
            Debug.Log($"[char-skin guard] silhouette margin: winner {winner} px, loser {loser} px, " +
                      $"{(winner == 0 ? double.PositiveInfinity : (double)loser / winner):N1}×");

            Assert.Greater(loser, 0,
                "the WRONG sign reproduced the rig perfectly. Two silhouettes that agree have not " +
                "told us which way she turns — this is an inconclusive reading wearing the face of " +
                "a clean one.");
            Assert.GreaterOrEqual(loser, winner * 4,
                $"the wrong sign is not wrong enough: {winner} px against {loser} px. Below 4× the " +
                "adjudication is reading noise, and the character's handedness is being decided by " +
                "a coin flip that happens to land the same way twice.");
        }

        // =======================================================================================
        // 7-9. completeness, conventions, and the budget the ADR claims
        // =======================================================================================

        /// <summary>
        /// <see cref="CharacterSkinDef.IsUsable"/> is what a presenter will branch on, so each
        /// clause it makes is sabotaged here one at a time. A completeness check that only ever
        /// sees complete data is a check nobody has tested.
        /// </summary>
        [Test]
        public void AnIncompleteDefIsNotUsable()
        {
            Assert.IsTrue(_def.IsUsable(), "the freshly composed def must be usable to start with");

            var probe = ScriptableObject.CreateInstance<CharacterSkinDef>();
            try
            {
                Assert.IsFalse(probe.IsUsable(), "an empty def must not claim to be usable");

                probe.Id = _def.Id;
                probe.BindMesh = _def.BindMesh;
                probe.MaxInfluences = _def.MaxInfluences;
                probe.Bones = new[]
                {
                    new CharacterSkinDef.Bone { Id = "root", Parent = CharacterSkinDef.NoBone },
                    new CharacterSkinDef.Bone { Id = "spine", Parent = 0 },
                };
                probe.Clips = new[] { OneFrameClip(probe.Bones.Length) };
                Assert.IsTrue(probe.IsUsable(), "a minimal well-formed def must be usable");

                // A parent LATER in the array. The composition loop walks the array once and reads
                // world[parent] before it is written, so this is silently a pose built on an
                // identity matrix — a limb hanging at the origin, not an exception.
                probe.Bones[1].Parent = 2;
                Assert.IsFalse(probe.IsUsable(), "a forward parent reference must be refused");
                probe.Bones[1].Parent = 0;

                // The root must BE the root.
                probe.Bones[0].Parent = 1;
                Assert.IsFalse(probe.IsUsable(), "bone 0 must be the root");
                probe.Bones[0].Parent = CharacterSkinDef.NoBone;

                probe.MaxInfluences = 0;
                Assert.IsFalse(probe.IsUsable(), "zero influences skins nothing");
                probe.MaxInfluences = CharacterSkinDef.MaxBoneInfluences + 1;
                Assert.IsFalse(probe.IsUsable(),
                    "a width wider than the def can store must be refused, not truncated");
                probe.MaxInfluences = CharacterSkinDef.MaxBoneInfluences;

                // A key array that is not FrameCount × boneCount indexes past its end on the last
                // frame of the last bone, which is the frame a looping clip reaches every cycle.
                CharacterSkinDef.SkinClip bad = probe.Clips[0];
                bad.Keys = new CharacterSkinDef.BoneKey[probe.Bones.Length - 1];
                probe.Clips = new[] { bad };
                Assert.IsFalse(probe.IsUsable(), "a short key array must be refused");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }

        /// <summary>
        /// The skin def and the flipbook def describe the same character and must not describe it
        /// at the same address. <c>AssetDatabase.CreateAsset</c> REPLACES the file at its path, so
        /// a collision here is not a merge conflict — it is one bake silently destroying the other.
        /// </summary>
        [Test]
        public void TheDefIdAndPathDoNotCollideWithTheFlipbooks()
        {
            Assert.AreEqual("charskin." + Player, CharacterSkinAssetBaker.IdFor(Player));
            Assert.AreEqual(CharacterSkinAssetBaker.IdFor(Player), _def.Id);
            Assert.AreEqual(Player, _def.Preset);

            Assert.AreNotEqual(CharacterMeshAssetBaker.IdFor(Player), CharacterSkinAssetBaker.IdFor(Player),
                "the two defs of one character must not share an id");
            Assert.AreNotEqual(CharacterMeshAssetBaker.AssetPathFor(Player),
                               CharacterSkinAssetBaker.AssetPathFor(Player),
                               "CreateAsset replaces the file at its path — one bake would delete " +
                               "the other's asset, guid and all");

            StringAssert.StartsWith("Assets/_Project/Data/Characters/",
                CharacterSkinAssetBaker.AssetPathFor(Player), "characters live under Data/Characters");
            StringAssert.EndsWith("/" + Player + ".asset", CharacterSkinAssetBaker.AssetPathFor(Player),
                "one entity per file, named for its stable id (CLAUDE.md §3.2)");
        }

        /// <summary>
        /// The budget option (d) was chosen on, restated as an assertion so the PR body's numbers
        /// and the repo cannot drift apart. Nothing here is a bar the bake must hit — they are the
        /// facts ADR 0044 §3.6 now records, with enough slack that ordinary drift does not red the
        /// build but a change of ORDER does.
        /// </summary>
        [Test]
        public void TheDefIsWhatTheADRSaysItIs()
        {
            double totalKb = _bake.TotalBytes / 1024.0;
            double ratio = FlipbookKb / totalKb;

            Debug.Log(
                $"[char-skin guard] {Player}: {_def.BoneCount} bones ({_def.DeformingBoneCount} " +
                $"deforming), {_bake.Built.Faces} faces → {_bake.Built.Vertices:N0} verts / " +
                $"{_bake.Built.Triangles:N0} tris, {_def.Clips.Length} clips, " +
                $"{_bake.TotalFrames} frames\n" +
                $"  bind {_bake.BindBytes / 1024.0:N1} KB + clips {_bake.ClipBytes / 1024.0:N1} KB " +
                $"= {totalKb:N1} KB ({totalKb / 1024.0:F2} MB)\n" +
                $"  against the flipbook's {FlipbookKb / 1024.0:F1} MB — {ratio:N1}× smaller\n" +
                $"  {_def.Materials.Length} materials, compose {_bake.ComposeMilliseconds:N0} ms");

            Assert.Greater(_def.BoneCount, 0, "a skin needs a skeleton");
            Assert.AreEqual(_bake.Bones.Length, _def.BoneCount);
            Assert.Greater(_def.DeformingBoneCount, 0, "no bone owns a vertex");
            Assert.LessOrEqual(_def.DeformingBoneCount, _def.BoneCount,
                "more deforming bones than bones");

            Assert.AreEqual(CharacterSkinExtractor.Anims(_host).Length, _def.Clips.Length,
                "one clip per ANIMS row, or a state the game can reach draws nothing");
            foreach (CharacterSkinDef.SkinClip c in _def.Clips)
            {
                Assert.IsTrue(c.KeysWellFormed(_def.BoneCount), $"clip '{c.Anim}' is malformed");
                Assert.Greater(c.FramesPerSecond, 0f, $"clip '{c.Anim}' plays at no rate");
                Assert.IsTrue(_def.TryGetClip(c.StateKey, out _), $"'{c.StateKey}' is not findable");
            }

            // A presenter reaches bones BY NAME once, at bind time, and by index every frame after.
            for (int b = 0; b < _def.BoneCount; b++)
                Assert.AreEqual(b, _def.IndexOfBone(_def.Bones[b].Id),
                    $"bone '{_def.Bones[b].Id}' is not findable at its own index — two bones share " +
                    "a name, and every lookup after the first resolves to the wrong limb");
            Assert.AreEqual(CharacterSkinDef.NoBone, _def.IndexOfBone("no_such_bone"),
                "an unknown bone name must resolve to NoBone, never to bone 0");

            Assert.LessOrEqual(_def.Materials.Length, CharacterSkinDef.RampSlots,
                $"{_def.Materials.Length} materials against {CharacterSkinDef.RampSlots} ramp " +
                "slots — the resolve pass would drop the tail and recolour her");

            // The claim option (d) rests on. A regression to within 10× of the flipbook means the
            // clips have exploded and the choice needs re-arguing, not a wider bar.
            Assert.Greater(ratio, 10.0,
                $"the skinned preset is {totalKb:N1} KB against the flipbook's {FlipbookKb:N0} KB, " +
                $"only {ratio:N1}× smaller. ADR 0044 §3.6 measured 74×.");

            // MeshStates stays empty in this PR: the art is baked, the game is not switched over.
            Assert.IsNotNull(_def.MeshStates, "MeshStates must be an array, never null");
        }

        // =======================================================================================
        // helpers
        // =======================================================================================

        /// <summary>
        /// <c>Σ wᵢ · (worldᵢ · bindposeᵢ) · v</c>, composed down the parent chain — Unity's own
        /// linear-blend skinning, written out because a guard that asked a
        /// <see cref="SkinnedMeshRenderer"/> for the answer would need a GPU, and CI has none.
        ///
        /// <para>The single-pass loop is only correct because a parent always sits EARLIER in the
        /// array; <see cref="CharacterSkinDef.IsUsable"/> is what makes that true, and
        /// <see cref="AnIncompleteDefIsNotUsable"/> is what makes IsUsable trustworthy.</para>
        /// </summary>
        static Matrix4x4[] SkinMatrices(CharacterSkinDef def, in CharacterSkinDef.SkinClip clip, int frame)
        {
            int n = def.Bones.Length;
            var world = new Matrix4x4[n];
            var skin = new Matrix4x4[n];
            Matrix4x4[] bindposes = def.BindMesh.bindposes;

            for (int b = 0; b < n; b++)
            {
                CharacterSkinDef.BoneKey k = clip.KeyOf(frame, b, n);
                var local = Matrix4x4.TRS(k.Position, k.Rotation, Vector3.one);
                int p = def.Bones[b].Parent;
                world[b] = p == CharacterSkinDef.NoBone ? local : world[p] * local;
                skin[b] = world[b] * bindposes[b];
            }
            return skin;
        }

        static Vector3 SkinVertex(Vector3 v, BoneWeight w, Matrix4x4[] skin, int influences)
        {
            Vector3 p = skin[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
            if (influences > 1 && w.weight1 != 0f)
                p += skin[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
            return p;
        }

        CharacterSkinDef.SkinClip ClipNamed(string anim)
        {
            foreach (CharacterSkinDef.SkinClip c in _def.Clips)
                if (string.Equals(c.Anim, anim, StringComparison.Ordinal)) return c;
            Assert.Fail($"the def carries no '{anim}' clip");
            return default;
        }

        static CharacterSkinDef.SkinClip OneFrameClip(int boneCount) => new CharacterSkinDef.SkinClip
        {
            Anim = "idle",
            State = "idle",
            FramesPerSecond = 10f,
            FrameCount = 1,
            Keys = NeutralKeys(boneCount),
            ParkedParts = Array.Empty<string>(),
        };

        static CharacterSkinDef.BoneKey[] NeutralKeys(int boneCount)
        {
            var keys = new CharacterSkinDef.BoneKey[boneCount];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new CharacterSkinDef.BoneKey { Rotation = Quaternion.identity };
            return keys;
        }

        /// <summary>An empty string is the extractor's "no option"; the rig wants a null.</summary>
        static string Nz(string s) => string.IsNullOrEmpty(s) ? null : s;

        static string LfSha256(string text)
        {
            byte[] lf = Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n"));
            using var h = SHA256.Create();
            var sb = new StringBuilder(64);
            foreach (byte b in h.ComputeHash(lf)) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
