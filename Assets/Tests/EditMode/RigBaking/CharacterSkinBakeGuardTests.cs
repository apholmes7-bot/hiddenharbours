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
    /// skeleton, one clip per <c>ANIMS</c> row and one per <c>CARRY_CLIPS</c> row (option (d)).
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
    /// <para><b>Why it composes instead of loading an asset.</b> The fixture calls the shipped
    /// <see cref="CharacterSkinAssetBaker.Compose"/> — the disk-free half of the bake — rather than
    /// a test-local transcription that could be wrong in exactly the way the baker is and agree
    /// with it perfectly. This was written before the asset existed, and promised a second question
    /// for the day it landed: does the committed asset still equal what Compose produces today?
    /// Section 3 asks it now, twice — by the rigs' hashes, and by the bind mesh's content for the
    /// face chain no hash covers — because a composed def is current by construction, and the def
    /// the game LOADS is the one that can go stale.</para>
    /// </summary>
    public partial class CharacterSkinBakeGuardTests
    {
        const string Player = CharacterRigBakeMenu.PlayerPreset;

        /// <summary>ADR 0044 §3.2's flipbook, in KB — what option (d) is measured against.</summary>
        const double FlipbookKb = CharacterSkinAssetBaker.FlipbookKilobytes;

        /// <summary>
        /// The four piloting stances rig 7's <c>CARRY_CLIPS</c> table adds, by the STATE KEY the game
        /// asks for (<c>CharacterRigBaker.CharacterState.Key</c>: the animation, then the carry).
        /// Spelled here rather than read back from the baker, so a bake that keyed them differently
        /// fails by name instead of agreeing with itself.
        /// </summary>
        static readonly string[] PilotingStates = { "idle_helm", "walk_helm", "idle_oars", "walk_oars" };

        /// <summary>
        /// The composed preset — the def the game ships, with the face the owner accepted — built
        /// once. Compose runs the rig, reads 45 bones, 3,108 weighted corners and 39 clips (the 35
        /// <c>ANIMS</c> rows and the four <c>CARRY_CLIPS</c> piloting stances), and renders two
        /// turntable probes; at roughly two seconds a call there is no reason for every test here to
        /// pay for it again.
        /// </summary>
        IRigScriptHost _host;
        CharacterSkinAssetBaker.SkinBake _bake;
        CharacterSkinDef _def;

        /// <summary>
        /// The PROVEN bake: the same preset with <c>composedFace: false</c>, which is rig 6's own head.
        /// The two guards that replay a def against rig 6's <c>facesOf</c> on every clip and frame can
        /// only be pointed at a mesh rig 6 draws itself. The composed face is a BIND-pose layer, so
        /// there is no posed reference for it, and using the layer as one would make the def's own bind
        /// faces its oracle. Those two guards measure this bake, and
        /// <see cref="TheShippedDefSharesTheProvenDefsSkeletonAndClips"/> proves the shipped def plays
        /// back through the same skeleton, bindposes and clip keys.
        ///
        /// <para>In a host of its OWN, so the oracle's host never has the face modules installed.
        /// Built on first use, once, because only three tests ask for it.</para>
        /// </summary>
        IRigScriptHost _provenHost;
        CharacterSkinAssetBaker.SkinBake _proven;

        CharacterSkinAssetBaker.SkinBake Proven
        {
            get
            {
                if (_proven == null)
                {
                    _provenHost ??= RigScriptHostFactory.Create();
                    _proven = CharacterSkinAssetBaker.Compose(_provenHost, Player, composedFace: false);
                }
                return _proven;
            }
        }

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
            // The defs and their bind meshes are loose objects no asset owns — leaving them behind
            // leaks a Mesh into the editor for the rest of the run.
            DestroyDef(_def);
            DestroyDef(_proven?.Def);
            _host?.Dispose();
            _provenHost?.Dispose();
            _host = null; _bake = null; _def = null;
            _provenHost = null; _proven = null;
        }

        static void DestroyDef(CharacterSkinDef def)
        {
            if (def == null) return;
            if (def.BindMesh != null) UnityEngine.Object.DestroyImmediate(def.BindMesh);
            UnityEngine.Object.DestroyImmediate(def);
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
        ///
        /// <para><b>It measures the PROVEN bake</b> (<c>composedFace: false</c>, see
        /// <see cref="Proven"/>), because rig 6 has no posed answer for the composed face. The def the
        /// game ships is tied to it by <see cref="TheShippedDefSharesTheProvenDefsSkeletonAndClips"/>.</para>
        ///
        /// <para><b>The carry rows are swept like every other row, and the sweep says it met them.</b>
        /// The four <c>CARRY_CLIPS</c> stances ride an existing animation plus a carry modifier, so each
        /// frame is posed here as <c>facesOf(pose(anim, u, build, {carry}))</c>: the clip's own
        /// <c>Carry</c> is what goes to <c>ExtractPose</c>. A def that dropped them would still pass a
        /// sweep that only counts what it was handed, so the sweep also asserts it swept each of
        /// <see cref="PilotingStates"/>, and as many carry rows as the rig declares.</para>
        /// </summary>
        [Test]
        public void TheDefPosesTheRigsOwnGeometry_OnEveryClipRowAndEveryFrame()
        {
            CharacterSkinAssetBaker.SkinBake bake = Proven;
            CharacterSkinDef def = bake.Def;
            double tol = bake.Tolerance;
            var bindFaces = bake.Bind.Faces;
            Vector3[] bindVerts = def.BindMesh.vertices;
            BoneWeight[] weights = def.BindMesh.boneWeights;

            var sw = Stopwatch.StartNew();
            double worst = 0; string worstAt = "none";
            int rows = 0, frames = 0, compared = 0, parkedTotal = 0;
            var carried = new SortedSet<string>(StringComparer.Ordinal);

            foreach (CharacterSkinDef.SkinClip clip in def.Clips)
            {
                var parked = new HashSet<string>(clip.ParkedParts ?? Array.Empty<string>(),
                                                 StringComparer.Ordinal);
                rows++;

                // A CARRY_CLIPS row is keyed by the state it answers (idle_helm), an ANIMS row by its
                // animation. That difference is the row kind; the Carry is what the rig poses it with.
                if (!string.Equals(clip.StateKey, clip.Anim, StringComparison.Ordinal))
                {
                    Assert.IsNotEmpty(clip.Carry,
                        $"'{clip.StateKey}' is keyed as a carry stance riding '{clip.Anim}' and names no " +
                        "carry, so the sweep would pose the free body and prove the wrong clip.");
                    carried.Add(clip.StateKey);
                }

                for (int frame = 0; frame < clip.FrameCount; frame++)
                {
                    RigMeshData reference = CharacterPoseMeshExtractor.ExtractPose(
                        _provenHost, Player, clip.Anim, frame, Nz(clip.Carry), Nz(clip.Power));
                    Matrix4x4[] skin = SkinMatrices(def, clip, frame);
                    frames++;

                    int corner = 0, j = 0;
                    for (int f = 0; f < bindFaces.Count; f++)
                    {
                        RigFace bf = bindFaces[f];
                        if (parked.Contains(bake.Skin.FacePart[f]))
                        {
                            corner += bf.V.Length; parkedTotal++; continue;
                        }

                        Assert.Less(j, reference.Faces.Count,
                            $"{clip.StateKey}[{frame}]: the bind mesh carries drawn face {f} " +
                            $"('{bake.Skin.FacePart[f]}') that rig 6 does not emit at all.");
                        RigFace rf = reference.Faces[j++];

                        Assert.AreEqual(bf.V.Length, rf.V.Length,
                            $"{clip.StateKey}[{frame}] face {f} ('{bake.Skin.FacePart[f]}'): the bind " +
                            "mesh and rig 6 disagree about how many corners this face has, so the " +
                            "two face lists have gone out of step and every distance after this " +
                            "one is meaningless.");
                        Assert.AreEqual(bake.Bind.Materials[bf.Mat].Name, reference.Materials[rf.Mat].Name,
                            $"{clip.StateKey}[{frame}] face {f}: the bind mesh paints this face " +
                            $"'{bake.Bind.Materials[bf.Mat].Name}' and rig 6 paints it " +
                            $"'{reference.Materials[rf.Mat].Name}' — the lists are misaligned.");

                        for (int k = 0; k < bf.V.Length; k++)
                        {
                            Vector3 posed = SkinVertex(bindVerts[corner + k], weights[corner + k],
                                                       skin, def.MaxInfluences);
                            double d = Vector3.Distance(posed, rf.V[k].ToVector3());
                            if (d > worst)
                            {
                                worst = d;
                                worstAt = $"{clip.StateKey}[{frame}] face {f} " +
                                          $"('{bake.Skin.FacePart[f]}') corner {k}";
                            }
                            compared++;
                        }
                        corner += bf.V.Length;
                    }

                    Assert.AreEqual(reference.Faces.Count, j,
                        $"{clip.StateKey}[{frame}]: rig 6 emitted {reference.Faces.Count} faces and the " +
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

            Assert.AreEqual(def.Clips.Length, rows, "a clip was skipped");
            CollectionAssert.IsSubsetOf(PilotingStates, carried,
                $"the sweep met the carry stances [{string.Join(", ", carried)}] and the game's piloting " +
                $"stances are [{string.Join(", ", PilotingStates)}]. A stance the def does not carry " +
                "cannot be drawn, and no sweep catches a clip that is not there. The extractor lists them " +
                "with CharacterSkinExtractor.CarryClipNames; if the rig declares them, the def is stale — " +
                "re-bake the player (\"Bake character SKIN (the player, ADR 0044 d)\").");
            Assert.AreEqual(CharacterSkinExtractor.CarryClipNames(_provenHost).Length, carried.Count,
                "the rig's CARRY_CLIPS table and the carry rows the def carries differ in number — a " +
                "stance was dropped or doubled on its way into the def.");
            Assert.AreEqual(bake.TotalFrames, frames, "a frame was skipped");
            Assert.Greater(compared, 0, "nothing was compared");
            Assert.LessOrEqual(worst, tol,
                $"the def does not reproduce the rig: worst {worst:E3} m at {worstAt}, against a " +
                $"tolerance of {tol:E1} m.\nThe def's numbers are what moved — rig 7's own golden " +
                "is guarded separately in CharacterSkinnedExportTests, so if THAT is green and " +
                "this is red the loss is in the transcription: the bindpose inversion, the " +
                "parent-index remap, the frame-major flattening, or the float32 quantisation.");
        }

        /// <summary>
        /// <b>The shipped def is the proven def with a different face on it — and nothing else that
        /// moves her.</b> The sweep above measures the PROVEN bake (<c>composedFace: false</c>),
        /// because rig 6 can only answer for its own head. The game ships the COMPOSED bake. This
        /// asserts that everything the sweep proved about playback is the SAME in the def the game
        /// gets: the bones in order, with their parents and rests; the bindposes; every clip's
        /// identity, timing, parking and carry; every key of every bone on every frame; and the
        /// influence width. Exact equality throughout — both bakes read one rig through one code path,
        /// and the face layer is never handed to the skeleton or clip readers, so any difference at
        /// all is a different skeleton or a different clip, not rounding.
        ///
        /// <para><b>Deliberately NOT compared, and why that is legitimate:</b> the bind mesh's
        /// vertices and weights, and <see cref="CharacterSkinDef.Bone.OwnsVertex"/>. Those are facts
        /// about the MESH, and the two meshes differ on purpose: the composed one drops rig 6's head
        /// and the inseam panel, adds the pass-05/06 head, and rebuilds the body through the finish
        /// pass. OwnsVertex follows the weights, so a bone that only moved dropped faces stops owning a
        /// vertex. The bones whose flag differs are logged by name.</para>
        ///
        /// <para><b>The gap this leaves, named:</b> no guard replays the COMPOSED mesh's own vertices
        /// and weights against a posed oracle, because none exists — the face layer is a bind-pose list
        /// and rig 6 draws a different head. What the shipped mesh has instead is its bind-pose checks
        /// (<c>AssertBindAgrees</c> and <c>AssertComposedFaceAgrees</c> inside the bake, and
        /// <see cref="TheBindMeshVertexOrderIsTheRigsCornerOrder"/>) and this proof that the proven
        /// skeleton and clips drive it.</para>
        /// </summary>
        [Test]
        public void TheShippedDefSharesTheProvenDefsSkeletonAndClips()
        {
            CharacterSkinDef shipped = _def, proven = Proven.Def;
            Assert.AreNotSame(proven, shipped, "the two bakes must be two defs, or this compares one with itself");
            Assert.AreEqual(proven.Preset, shipped.Preset, "the two bakes are of different presets");
            Assert.AreEqual(proven.MaxInfluences, shipped.MaxInfluences,
                "the shipped def carries a different influence width from the def the collapse guard " +
                "measured, so its Bone2 requirement no longer transfers to the def the game draws.");

            // ---- the skeleton, in order ----------------------------------------------------------
            Assert.AreEqual(proven.Bones.Length, shipped.Bones.Length,
                "the shipped def has a different bone count; clips store bone INDICES.");
            var ownershipMoved = new List<string>();
            for (int b = 0; b < proven.Bones.Length; b++)
            {
                CharacterSkinDef.Bone p = proven.Bones[b], s = shipped.Bones[b];
                string at = $"bone {b} ('{p.Id}')";
                Assert.AreEqual(p.Id, s.Id,
                    $"{at}: the shipped def has '{s.Id}' here. The bone ORDER differs, and clips store indices.");
                Assert.AreEqual(p.Parent, s.Parent, $"{at}: the shipped def parents it to a different bone.");
                Assert.IsTrue(Same(p.RestPosition, s.RestPosition),
                    $"{at}: rest position {s.RestPosition:F6} in the shipped def, {p.RestPosition:F6} in the proven one.");
                Assert.IsTrue(Same(p.RestRotation, s.RestRotation),
                    $"{at}: rest rotation {s.RestRotation:F6} in the shipped def, {p.RestRotation:F6} in the proven one.");
                if (p.OwnsVertex != s.OwnsVertex)
                    ownershipMoved.Add($"{p.Id} {(p.OwnsVertex ? "owns" : "moves none")} → {(s.OwnsVertex ? "owns" : "moves none")}");
            }

            // ---- the bindposes -------------------------------------------------------------------
            Matrix4x4[] pb = proven.BindMesh.bindposes, sb = shipped.BindMesh.bindposes;
            Assert.AreEqual(pb.Length, sb.Length, "the shipped bind mesh carries a different bindpose count.");
            for (int b = 0; b < pb.Length; b++)
                for (int e = 0; e < 16; e++)
                    if (pb[b][e] != sb[b][e])
                        Assert.Fail($"bindpose {b} ('{proven.Bones[b].Id}') element {e}: {sb[b][e]:R} in the " +
                                    $"shipped def, {pb[b][e]:R} in the proven one. The sweep's skinning proof " +
                                    "does not carry to a mesh with different bindposes.");

            // ---- the clips, every key on every frame ---------------------------------------------
            int boneCount = proven.Bones.Length;
            long keys = 0;
            Assert.AreEqual(proven.Clips.Length, shipped.Clips.Length, "the shipped def carries a different clip count.");
            for (int c = 0; c < proven.Clips.Length; c++)
            {
                CharacterSkinDef.SkinClip p = proven.Clips[c], s = shipped.Clips[c];
                string at = $"clip {c} ('{p.Anim}')";
                Assert.AreEqual(p.Anim, s.Anim, $"{at}: the shipped def has '{s.Anim}' here; clip ORDER differs.");
                Assert.AreEqual(p.State, s.State, $"{at}: state");
                Assert.AreEqual(p.FramesPerSecond, s.FramesPerSecond, $"{at}: frames per second");
                Assert.AreEqual(p.Settle, s.Settle, $"{at}: settle");
                Assert.AreEqual(p.Loop, s.Loop, $"{at}: loop");
                Assert.AreEqual(p.FrameCount, s.FrameCount, $"{at}: frame count");
                Assert.AreEqual(p.Mount, s.Mount, $"{at}: mount");
                Assert.AreEqual(p.Carry, s.Carry, $"{at}: carry");
                Assert.AreEqual(p.Power, s.Power, $"{at}: power");
                CollectionAssert.AreEqual(p.ParkedParts ?? Array.Empty<string>(), s.ParkedParts ?? Array.Empty<string>(),
                    $"{at}: parked parts");
                Assert.IsTrue(p.KeysWellFormed(boneCount) && s.KeysWellFormed(boneCount),
                    $"{at}: a key array is not FrameCount × BoneCount.");

                for (int frame = 0; frame < p.FrameCount; frame++)
                    for (int b = 0; b < boneCount; b++)
                    {
                        CharacterSkinDef.BoneKey pk = p.KeyOf(frame, b, boneCount), sk = s.KeyOf(frame, b, boneCount);
                        if (!Same(pk.Position, sk.Position) || !Same(pk.Rotation, sk.Rotation))
                            Assert.Fail($"{at} frame {frame} bone {b} ('{proven.Bones[b].Id}'): the shipped key " +
                                        $"is {sk.Position:F6} {sk.Rotation:F6} and the proven key is " +
                                        $"{pk.Position:F6} {pk.Rotation:F6}. The sweep proved the proven def's " +
                                        "playback, and this frame of the shipped def is not that playback.");
                        keys++;
                    }
            }

            Debug.Log(
                $"[char-skin guard] the shipped (composed) def against the proven (rig 6 head) def:\n" +
                $"  identical: {boneCount} bones, {pb.Length} bindposes, {proven.Clips.Length} clips, " +
                $"{keys:N0} bone keys, {shipped.MaxInfluences} influences\n" +
                $"  meshes (differ on purpose): shipped {shipped.BindMesh.vertexCount:N0} vertices, " +
                $"proven {proven.BindMesh.vertexCount:N0}\n" +
                $"  OwnsVertex differs on {ownershipMoved.Count} bone(s): " +
                (ownershipMoved.Count == 0 ? "none" : string.Join(", ", ownershipMoved)));
        }

        /// <summary>Bit-exact: both sides are float32 casts of one double read from one rig.</summary>
        static bool Same(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;

        static bool Same(Quaternion a, Quaternion b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;

        // =======================================================================================
        // 2. the mesh Unity will hand the GPU is the mesh the weights were measured on
        // =======================================================================================

        /// <summary>
        /// The weights are attached by INDEX to a mesh a different class built. If
        /// <see cref="RigMeshBuilder"/> ever welds, reorders or drops a corner, every weight lands
        /// on the wrong vertex — all 3,108 of them, each by a different amount, and nothing throws.
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

        /// <summary>
        /// <b>The COMMITTED defs pin the rigs as they are TODAY.</b> The test above is self-consistent by
        /// construction: it asks a def Compose built a moment ago whether it recorded the files Compose
        /// just read. The def the game loads is the one on disk, and nothing re-bakes it when a rig
        /// moves, so a rig edit landed without a re-bake left every other guard in this file green over a
        /// stale skin. <c>CharacterSkinExtractor.SourceSha256</c> calls the pin "the def's stale-bake
        /// guard"; this is the test that reads it.
        ///
        /// <para><b>Every def, found by type</b>, not the player by path, so a preset's def joins the
        /// guard the moment it is committed. Red means RE-BAKE, never re-pin: for the player the menu is
        /// "Bake character SKIN (the player, ADR 0044 d)". A hash edited by hand says the skin matches
        /// rigs it was never baked from, which is the one thing this pin exists to rule out.</para>
        /// </summary>
        [Test]
        public void EveryCommittedSkinDef_PinsTheRigsAsTheyAreToday()
        {
            string liveRig7 = CharacterSkinExtractor.SourceSha256();
            string liveRig6 = CharacterPoseMeshExtractor.SourceSha256();
            string[] guids = UnityEditor.AssetDatabase.FindAssets(
                "t:" + nameof(CharacterSkinDef), new[] { "Assets" });
            Assert.IsNotEmpty(guids,
                "the search found no CharacterSkinDef, and the player's is committed — the search is " +
                "broken, and every check below would pass on nothing.");

            var stale = new List<string>();
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var committed = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(path);
                Assert.IsNotNull(committed, $"{path} is indexed as a CharacterSkinDef and does not load as one.");
                Assert.AreEqual(CharacterSkinExtractor.ScriptPath, committed.SourceRigPath,
                    $"{path} was baked from a different skinned export");
                Assert.AreEqual(CharacterPoseMeshExtractor.ScriptPath, committed.BaseRigPath,
                    $"{path} was baked from a different body rig");

                if (!string.Equals(liveRig7, committed.SourceRigSha256, StringComparison.Ordinal))
                    stale.Add($"{path}: rig 7 {Head(committed.SourceRigSha256)} baked, {Head(liveRig7)} live");
                if (!string.Equals(liveRig6, committed.BaseRigSha256, StringComparison.Ordinal))
                    stale.Add($"{path}: rig 6 {Head(committed.BaseRigSha256)} baked, {Head(liveRig6)} live");
            }

            Assert.IsEmpty(stale,
                "a committed skin def was baked from rigs that are no longer the ones in the repo:\n  " +
                string.Join("\n  ", stale) + "\nThe skin the game draws is not the rig's. Re-bake it " +
                "(the player: \"Bake character SKIN (the player, ADR 0044 d)\") and commit the asset with " +
                "the rig change. Never edit the hash.");
            Debug.Log($"[char-skin guard] {guids.Length} committed def(s) pin the live rigs: " +
                      $"rig 7 {Head(liveRig7)} / rig 6 {Head(liveRig6)}");
        }

        /// <summary>
        /// <b>The committed bind mesh is the face the chain composes TODAY</b>, compared by content
        /// because no hash covers it. The def pins rigs 6 and 7. The face it wears is also read from
        /// <c>eyeIsoRig</c>, <c>headIsoRig3</c>, <c>characterFaceStudy</c>,
        /// <c>characterFinishConfig</c>, <c>characterFinish</c>, <c>characterArtStudy</c> and
        /// <c>characterFaceComposition</c>, and none of those bytes is recorded in the def: an edit to
        /// any of them changes her face without moving a pinned hash, and the test above stays green.
        /// So this takes the player's bind mesh as the chain composes it now (the fixture's
        /// <see cref="CharacterSkinAssetBaker.Compose"/>) and requires the committed
        /// <c>CharSkin_fisher_bind</c> sub-asset to be that mesh. It reds when the chain moves HER mesh,
        /// and not on a comment or on another preset's face.
        ///
        /// <para><b>What "that mesh" means.</b> Triangles, bone weights and every UV channel (the ramp,
        /// level and texture attributes) exactly: they are indices and authored numbers carried straight
        /// through. Positions to the rig's own tolerance (<c>CharacterIso7.TOL</c>, read off the rig):
        /// the committed mesh was baked on one machine and CI composes on another, a face edit moves a
        /// corner by millimetres, and a last-bit difference in a float path moves it by nothing a pixel
        /// can show. The normals follow from those positions and triangles, and the bindposes are the
        /// skeleton's, pinned above and replayed by
        /// <see cref="TheShippedDefSharesTheProvenDefsSkeletonAndClips"/>, so neither is compared
        /// twice.</para>
        ///
        /// <para>Hashing the face chain into the def (a <c>FaceChainSha256</c> field) would say the same
        /// more cheaply. It is a Core schema change and the lead-architect's call, so it is not here.</para>
        /// </summary>
        [Test]
        public void TheCommittedBindMeshIsTheFaceTheChainComposesToday()
        {
            string path = CharacterSkinAssetBaker.AssetPathFor(Player);
            var committed = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(path);
            Assert.IsNotNull(committed, $"no CharacterSkinDef at {path}: the player's skin is committed content.");
            Mesh disk = committed.BindMesh, live = _def.BindMesh;
            Assert.IsNotNull(disk, $"{path} carries no bind mesh sub-asset.");
            const string rebake = " The face chain moved her face and the committed skin still wears the " +
                                  "old one: re-bake the player (\"Bake character SKIN (the player, ADR 0044 " +
                                  "d)\") in the PR that changed the chain, and commit the asset with it.";

            Assert.AreEqual(live.vertexCount, disk.vertexCount,
                $"the chain composes {live.vertexCount:N0} corners and the committed mesh has " +
                $"{disk.vertexCount:N0}." + rebake);
            Assert.AreEqual(live.subMeshCount, disk.subMeshCount, "the sub-mesh count differs." + rebake);
            int indices = 0;
            for (int s = 0; s < live.subMeshCount; s++)
            {
                int[] a = live.GetTriangles(s), b = disk.GetTriangles(s);
                Assert.AreEqual(a.Length, b.Length, $"sub-mesh {s}: the index count differs." + rebake);
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i])
                        Assert.Fail($"sub-mesh {s} index {i}: {a[i]} composed, {b[i]} committed; the faces " +
                                    "are ordered or wound differently." + rebake);
                indices += a.Length;
            }

            Vector3[] lv = live.vertices, dv = disk.vertices;
            double worst = 0; int worstAt = -1;
            for (int i = 0; i < lv.Length; i++)
            {
                double d = Vector3.Distance(lv[i], dv[i]);
                if (d > worst) { worst = d; worstAt = i; }
            }
            Assert.LessOrEqual(worst, _bake.Tolerance,
                $"corner {worstAt} is {worst:E3} m from where the committed mesh has it, against the rig's " +
                $"tolerance of {_bake.Tolerance:E1} m." + rebake);

            BoneWeight[] lw = live.boneWeights, dw = disk.boneWeights;
            Assert.AreEqual(lw.Length, dw.Length, "the bone weight count differs." + rebake);
            for (int i = 0; i < lw.Length; i++)
                if (!lw[i].Equals(dw[i]))
                    Assert.Fail($"corner {i}: composed {lw[i].boneIndex0}×{lw[i].weight0:R} + " +
                                $"{lw[i].boneIndex1}×{lw[i].weight1:R}, committed {dw[i].boneIndex0}×" +
                                $"{dw[i].weight0:R} + {dw[i].boneIndex1}×{dw[i].weight1:R}." + rebake);

            int channels = 0;
            var lu = new List<Vector4>();
            var du = new List<Vector4>();
            for (var attr = UnityEngine.Rendering.VertexAttribute.TexCoord0;
                 attr <= UnityEngine.Rendering.VertexAttribute.TexCoord7; attr++)
            {
                int ch = attr - UnityEngine.Rendering.VertexAttribute.TexCoord0;
                Assert.AreEqual(live.HasVertexAttribute(attr), disk.HasVertexAttribute(attr),
                    $"UV channel {ch} exists on one mesh and not the other." + rebake);
                if (!live.HasVertexAttribute(attr)) continue;
                live.GetUVs(ch, lu);
                disk.GetUVs(ch, du);
                Assert.AreEqual(lu.Count, du.Count, $"UV channel {ch}: the corner count differs." + rebake);
                for (int i = 0; i < lu.Count; i++)
                    if (lu[i].x != du[i].x || lu[i].y != du[i].y || lu[i].z != du[i].z || lu[i].w != du[i].w)
                        Assert.Fail($"UV channel {ch} corner {i}: {lu[i]:R} composed, {du[i]:R} committed; " +
                                    "the face is painted or shaded differently." + rebake);
                channels++;
            }

            Debug.Log($"[char-skin guard] the committed {path} bind mesh is the chain's face today: " +
                      $"{lv.Length:N0} corners (worst {worst:E3} m), {indices:N0} indices, " +
                      $"{lw.Length:N0} weights, {channels} UV channel(s)");
        }

        /// <summary>The first twelve hex digits, which is how the PR bodies and the logs name a pin.</summary>
        static string Head(string sha) =>
            string.IsNullOrEmpty(sha) ? "<none>" : sha.Length > 12 ? sha.Substring(0, 12) + "…" : sha;

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
        ///
        /// <para><b>It measures the PROVEN bake</b> (<c>composedFace: false</c>, see
        /// <see cref="Proven"/>) for the same reason the sweep above does: rig 6 keeps answering, and
        /// it cannot answer for the composed face. The shipped def carries the same influence width,
        /// asserted in <see cref="TheShippedDefSharesTheProvenDefsSkeletonAndClips"/>.</para>
        /// </summary>
        [Test]
        public void CollapsingTheBlendedRingsToOneBone_MovesTheHemsByOrdersOfMagnitude()
        {
            CharacterSkinAssetBaker.SkinBake bake = Proven;
            CharacterSkinDef def = bake.Def;
            Assert.AreEqual(CharacterSkinDef.MaxBoneInfluences, def.MaxInfluences,
                "the def must carry the measured influence width, not a convenient one");

            BoneWeight[] weights = def.BindMesh.boneWeights;
            Vector3[] verts = def.BindMesh.vertices;

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

            // 'walk' ALONE COSTS 3.620e-3 m — an order short of the bar, because the 4.52e-2 m the
            // rig quotes is the max over its WHOLE row set and the hems swing hardest elsewhere.
            // Sweep every clip the def carries. Measured 2026-09-10 in the standalone V8 harness:
            // worst 4.516e-2 m on 'sleep', frame 1, face 396 ('upper_R'); then toss 3.389e-2,
            // reach 3.295e-2, ladderDown 3.227e-2. Do not narrow this back to one clip.
            var bindFaces = bake.Bind.Faces;
            double worst = 0; string at = "none";
            var perClip = new List<KeyValuePair<string, double>>();

            foreach (CharacterSkinDef.SkinClip clip in def.Clips)
            {
                var parked = new HashSet<string>(clip.ParkedParts ?? Array.Empty<string>(), StringComparer.Ordinal);
                double clipWorst = 0;

                for (int frame = 0; frame < clip.FrameCount; frame++)
                {
                    RigMeshData reference = CharacterPoseMeshExtractor.ExtractPose(
                        _provenHost, Player, clip.Anim, frame, Nz(clip.Carry), Nz(clip.Power));
                    Matrix4x4[] skin = SkinMatrices(def, clip, frame);

                    int corner = 0, j = 0;
                    for (int f = 0; f < bindFaces.Count; f++)
                    {
                        RigFace bf = bindFaces[f];
                        if (parked.Contains(bake.Skin.FacePart[f])) { corner += bf.V.Length; continue; }
                        RigFace rf = reference.Faces[j++];
                        for (int k = 0; k < bf.V.Length; k++)
                        {
                            Vector3 posed = SkinVertex(verts[corner + k], collapsed[corner + k], skin, 1);
                            double d = Vector3.Distance(posed, rf.V[k].ToVector3());
                            if (d > clipWorst) clipWorst = d;
                            if (d > worst)
                            {
                                worst = d;
                                at = $"'{clip.Anim}' frame {frame} face {f} " +
                                     $"('{bake.Skin.FacePart[f]}') corner {k}";
                            }
                        }
                        corner += bf.V.Length;
                    }
                }

                perClip.Add(new KeyValuePair<string, double>(clip.Anim, clipWorst));
            }

            perClip.Sort((x, y) => y.Value.CompareTo(x.Value));
            double tol = bake.Tolerance;
            var loudest = new StringBuilder();
            for (int i = 0; i < 4 && i < perClip.Count; i++)
                loudest.Append(i == 0 ? "" : ", ").Append(perClip[i].Key).Append(' ')
                       .Append(perClip[i].Value.ToString("E3"));

            Debug.Log($"[char-skin guard] one-influence sabotage over all {perClip.Count} clips: " +
                      $"{blended} blended corners of {weights.Length:N0} collapsed onto their " +
                      $"heavier bone → worst {worst:E3} m at {at}, {worst / tol:N0}× the " +
                      $"{tol:E1} m tolerance. Loudest clips: {loudest}");

            Assert.Greater(worst, tol * 100,
                $"collapsing the blended rings cost only {worst:E3} m ({worst / tol:N1}× tol). " +
                "Either the sabotage no longer reaches the weights the hems actually use, or the " +
                "rig stopped blending — and if the rig stopped blending, the def should be one " +
                "influence wide and this whole test should go. Do not relax the bar to make it " +
                "pass: rig 7's own measurement of this collapse is 4.52e-2 m, and this sweep " +
                "reproduced it at 4.516e-2 m on 'sleep' on 2026-09-10.");
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

                // IsUsable also gates the SHADING half — ramps, the dither matrix, the cell. A
                // probe that only carries geometry is refused for a reason that has nothing to do
                // with what the next few lines sabotage, so satisfy every clause first and take
                // them away one at a time. (This is what made the first draft of this test red:
                // it asserted a def with no Materials was usable.)
                probe.Materials = new[]
                {
                    new CharacterSkinDef.Material
                    {
                        Name = "skin",
                        Colors = new[] { new Color32(0, 0, 0, 255), new Color32(255, 255, 255, 255) },
                    },
                };
                probe.Bayer16 = new float[16];
                probe.CellW = _def.CellW;
                probe.CellH = _def.CellH;
                probe.PxPerMetre = _def.PxPerMetre;
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

                // No ramp at all. The presenter reads Materials[i].Colors to index the palette;
                // an empty table draws every facet at index 0, which is a silhouette, not a figure.
                CharacterSkinDef.Material[] mats = probe.Materials;
                probe.Materials = null;
                Assert.IsFalse(probe.IsUsable(), "a def with no material table must be refused");
                probe.Materials = Array.Empty<CharacterSkinDef.Material>();
                Assert.IsFalse(probe.IsUsable(), "an empty material table must be refused");

                // More ramps than the shader has slots. RampSlots is the width of the array the
                // material block uploads; a 17th ramp is not clamped, it is dropped off the end.
                var tooMany = new CharacterSkinDef.Material[CharacterSkinDef.RampSlots + 1];
                for (int i = 0; i < tooMany.Length; i++) tooMany[i] = mats[0];
                probe.Materials = tooMany;
                Assert.IsFalse(probe.IsUsable(),
                    $"more than {CharacterSkinDef.RampSlots} ramps must be refused, not truncated");

                // A named material carrying no colours — the shape the extractor produces when a
                // rig part names a palette that the build did not resolve.
                probe.Materials = new[] { new CharacterSkinDef.Material { Name = "skin", Colors = null } };
                Assert.IsFalse(probe.IsUsable(), "a material with no ramp must be refused");
                probe.Materials = new[]
                {
                    new CharacterSkinDef.Material { Name = "skin", Colors = Array.Empty<Color32>() },
                };
                Assert.IsFalse(probe.IsUsable(), "a material with an empty ramp must be refused");
                probe.Materials = mats;

                // The ordered-dither matrix is indexed (y & 3) * 4 + (x & 3) with no bounds check.
                probe.Bayer16 = null;
                Assert.IsFalse(probe.IsUsable(), "a def with no dither matrix must be refused");
                probe.Bayer16 = new float[15];
                Assert.IsFalse(probe.IsUsable(), "a dither matrix that is not 4×4 must be refused");
                probe.Bayer16 = new float[16];

                // The cell and the scale. PxPerMetre 0 divides by zero when the presenter converts
                // rig metres to pixels; a zero cell gives every sprite an empty rect.
                probe.CellW = 0;
                Assert.IsFalse(probe.IsUsable(), "a zero-width cell must be refused");
                probe.CellW = _def.CellW;
                probe.CellH = 0;
                Assert.IsFalse(probe.IsUsable(), "a zero-height cell must be refused");
                probe.CellH = _def.CellH;
                probe.PxPerMetre = 0;
                Assert.IsFalse(probe.IsUsable(), "a def with no metres-to-pixels scale must be refused");
                probe.PxPerMetre = _def.PxPerMetre;

                Assert.IsTrue(probe.IsUsable(),
                    "every sabotage above must have been put back — if this fails the ones after " +
                    "it are measuring the leftovers of an earlier one, not their own clause");

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

            // One clip per ANIMS row AND one per CARRY_CLIPS row. Until the rig 7 follow-up this read
            // Anims(_host).Length alone. The piloting stances are a separate table on purpose (ANIMS
            // stays 35, which CharacterFaceCompositionTests.NoTrackOfAnyAnimationChanges pins), so the
            // def's row count is the SUM, and a bake that enumerated only one table is short.
            string[] carryRows = CharacterSkinExtractor.CarryClipNames(_host);
            Assert.AreEqual(CharacterSkinExtractor.Anims(_host).Length + carryRows.Length, _def.Clips.Length,
                "one clip per ANIMS row and one per CARRY_CLIPS row, or a state the game can reach draws " +
                "nothing");
            var stateKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterSkinDef.SkinClip c in _def.Clips)
            {
                Assert.IsTrue(c.KeysWellFormed(_def.BoneCount), $"clip '{c.StateKey}' is malformed");
                Assert.Greater(c.FramesPerSecond, 0f, $"clip '{c.StateKey}' plays at no rate");
                Assert.IsTrue(_def.TryGetClip(c.StateKey, out _), $"'{c.StateKey}' is not findable");
                Assert.IsTrue(stateKeys.Add(c.StateKey),
                    $"two clips answer to '{c.StateKey}'. TryGetClip returns the first, so the second can " +
                    "never be drawn; a carry stance keyed by the animation it rides is exactly this.");
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

            // MeshStates is authored ON the asset and survives a re-bake (the baker keeps it with ??=),
            // so a composed def only has to hold an array. What the committed one names is held to the
            // clips it carries by CharacterSkinDefContentTests.TheDefDeclaresOnlyStatesItActuallyCarries.
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
