using System;
using System.Collections.Generic;
using System.Diagnostics;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Bakes ONE skinned character — a bind mesh, a skeleton and one clip per <c>ANIMS</c> row —
    /// into a committed <see cref="CharacterSkinDef"/>. ADR 0044 option (d).
    ///
    /// <para><b>What this replaces, and by how much.</b> The flipbook baker beside this one writes
    /// one <see cref="Mesh"/> per POSE: 334 meshes for the player recipe, 45,153 KB. This writes one
    /// mesh and 308 frames of 45 bone transforms: <b>611 KB, 74× smaller</b>. The saving is not a
    /// compression trick — it is the observation that every pose of a character is the same 2,992
    /// vertices in a different arrangement, so the vertices need storing once.</para>
    ///
    /// <para><b>This does not switch the game over, and a re-bake never switches it either.</b>
    /// <see cref="CharacterMeshDef"/> stays exactly as it is and stays the fallback. A FRESH bake — one
    /// with no committed asset to refresh — leaves <see cref="CharacterSkinDef.MeshStates"/> EMPTY, so
    /// nothing draws from a def this baker invented on its own.</para>
    ///
    /// <para>The per-state switch is AUTHORED ON THE COMMITTED ASSET (ADR 0041), by the presenter PR
    /// that proved those states draw. <see cref="Compose"/> refreshes the existing def IN PLACE, and the
    /// <c>??=</c> that fills the list is load-bearing: it assigns only when there is nothing there, so a
    /// re-bake refreshes the ART and leaves the owner's switch exactly where he left it. Retiring a
    /// baked sheet is a capability change.</para>
    ///
    /// <para><b>The face is missing and will be until a presenter re-adds it.</b> Rig 6 draws eyes,
    /// brows and mouth as a raster STAMP over the head quad (<c>HeadIso.stamp</c>), not as geometry.
    /// A skinned character therefore has a blank head — ≤2.82 % of the pixels, and the first thing
    /// anyone looks at. Every clip carries its per-frame <c>face</c> track for exactly this, but
    /// consuming it is the presenter's PR.</para>
    /// </summary>
    public static class CharacterSkinAssetBaker
    {
        /// <summary>
        /// <c>Assets/_Project/Data/Characters/Skin</c> — deliberately NOT the flipbook's folder.
        ///
        /// <para>⚠️ <see cref="CharacterMeshAssetBaker.AssetPathFor"/>("fisher") is
        /// <c>Data/Characters/fisher.asset</c>. Two bakers writing one path is not a merge, it is a
        /// clobber: <c>AssetDatabase.CreateAsset</c> replaces the file, so whichever baker ran second
        /// would silently delete the other's def and every sub-asset in it. A subfolder costs one
        /// path segment and makes that impossible.</para>
        /// </summary>
        public const string AssetFolder = "Assets/_Project/Data/Characters/Skin";

        public static string AssetPathFor(string preset) => $"{AssetFolder}/{preset}.asset";

        /// <summary>Def id — <c>charskin.&lt;preset&gt;</c>, distinct from the flipbook's
        /// <c>charmesh.&lt;preset&gt;</c> because both defs can exist at once, by design.</summary>
        public static string IdFor(string preset) => "charskin." + preset;

        /// <summary>What the flipbook is measured against, from ADR 0044 §3.2: the player recipe's
        /// 334 pose meshes at 45,153 KB. Named so the ratio in the log is not a floating claim.</summary>
        public const long FlipbookKilobytes = 45153;

        // -------------------------------------------------------------------------------------
        // The budget — no AssetDatabase, no graphics device. The PR body's numbers come from here.
        // -------------------------------------------------------------------------------------

        public sealed class SkinBudget
        {
            public string Preset;
            public int Bones, DeformingBones, Faces, Vertices, Triangles, MaxInfluences;
            public int Clips, Frames;
            public long BindGeometryBytes, BoneWeightBytes, BindposeBytes, ClipBytes;
            public long BindBytes => BindGeometryBytes + BoneWeightBytes + BindposeBytes;
            public long TotalBytes => BindBytes + ClipBytes;
            public long BakeMilliseconds;
            public IReadOnlyList<string> MaterialsUsed = Array.Empty<string>();

            public override string ToString() =>
                $"{Preset}: {Faces} faces → {Triangles} tris / {Vertices} verts, {Bones} bones " +
                $"({DeformingBones} deforming), {Clips} clips / {Frames} frames, max {MaxInfluences} " +
                $"influences\n" +
                $"  bind {BindBytes / 1024.0:N1} KB (geometry {BindGeometryBytes / 1024.0:N1} + " +
                $"weights {BoneWeightBytes / 1024.0:N1} + bindposes {BindposeBytes / 1024.0:N1}), " +
                $"clips {ClipBytes / 1024.0:N1} KB\n" +
                $"  TOTAL {TotalBytes / 1024.0:N1} KB ({TotalBytes / 1048576.0:F2} MB) against the " +
                $"flipbook's {FlipbookKilobytes / 1024.0:F1} MB — " +
                $"{FlipbookKilobytes * 1024.0 / Math.Max(1, TotalBytes):F1}× smaller\n" +
                $"  {MaterialsUsed.Count} materials of {CharacterSkinDef.RampSlots} ramp slots, " +
                $"bake {BakeMilliseconds:N0} ms";
        }

        /// <summary>
        /// Extract everything and cost it, without writing an asset. This is the honest place for the
        /// PR body's numbers: it walks the same code the bake walks, so a figure quoted from here is
        /// a figure the bake will reproduce.
        /// </summary>
        public static SkinBudget Measure(IRigScriptHost host, string preset)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var sw = Stopwatch.StartNew();

            CharacterSkinExtractor.AssertKitLoaded(host, preset);
            double tol = CharacterSkinExtractor.Tolerance(host);

            RigBone[] bones = CharacterSkinExtractor.ReadSkeleton(host, preset);
            CharacterSkinExtractor.AssertRestComposes(bones, tol);

            RigSkinning skin = CharacterSkinExtractor.ReadSkinning(
                host, preset, CharacterSkinDef.MaxBoneInfluences);
            CharacterSkinExtractor.MarkOwnership(bones, skin);

            RigMeshData bind = CharacterPoseMeshExtractor.ExtractPose(host, preset, "idle", 0);
            CharacterSkinExtractor.AssertBindAgrees(bind, skin, tol);

            RigMeshBuild built = RigMeshBuilder.Build(bind, $"tmp_bind_{preset}");
            var budget = new SkinBudget
            {
                Preset = preset,
                Bones = bones.Length,
                Faces = built.Faces,
                Vertices = built.Vertices,
                Triangles = built.Triangles,
                MaxInfluences = skin.MaxInfluences,
                BindGeometryBytes = built.BufferBytes,
                BoneWeightBytes = (long)built.Vertices * BoneWeightBytesPerVertex,
                BindposeBytes = (long)bones.Length * BindposeBytesPerBone,
                MaterialsUsed = NamesOf(bind.Materials),
            };
            foreach (RigBone b in bones) if (b.OwnsVertex) budget.DeformingBones++;
            UnityEngine.Object.DestroyImmediate(built.Mesh);

            foreach (string anim in CharacterSkinExtractor.Anims(host))
            {
                RigSkinClip clip = CharacterSkinExtractor.ReadClip(host, preset, anim, bones.Length);
                budget.Clips++;
                budget.Frames += clip.Frames;
                budget.ClipBytes += (long)clip.Frames * bones.Length * BoneKeyBytes;
            }

            sw.Stop();
            budget.BakeMilliseconds = sw.ElapsedMilliseconds;
            return budget;
        }

        /// <summary>A <see cref="BoneWeight"/> is 2 indices + 2 weights ×… — Unity's legacy struct is
        /// 4 of each, 32 B, regardless of how many are used. Counted as it is stored, not as it is
        /// needed, because that is what the file and the GPU pay.</summary>
        const int BoneWeightBytesPerVertex = 32;

        /// <summary>A bindpose is a full <see cref="Matrix4x4"/>: 16 floats.</summary>
        const int BindposeBytesPerBone = 64;

        /// <summary>One <see cref="CharacterSkinDef.BoneKey"/>: 3 floats of position + 4 of
        /// rotation.</summary>
        const int BoneKeyBytes = 28;

        // -------------------------------------------------------------------------------------
        // Menu + CLI
        // -------------------------------------------------------------------------------------

        [MenuItem(RigMeshGate.MenuRoot + "/Bake character SKIN (the player, ADR 0044 d)", priority = 224)]
        public static void BakePlayer()
        {
            try { Bake(CharacterRigBakeMenu.PlayerPreset); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [MenuItem(RigMeshGate.MenuRoot + "/Bake character SKIN (the player, ADR 0044 d)", validate = true)]
        public static bool BakePlayerValidate() => RigMeshGate.Enabled;

        /// <summary>
        /// Headless entry (<c>-executeMethod</c>).
        ///
        /// <para>⚠️ A FRESH WORKTREE'S FIRST UNITY RUN SKIPS <c>-executeMethod</c> ENTIRELY — it
        /// imports the project and quits, exit code 0, having baked nothing. Grep the log for the
        /// <c>[char-skin]</c> marker below; never trust the exit code alone.</para>
        /// </summary>
        public static void BakePlayerCli()
        {
            try
            {
                Bake(CharacterRigBakeMenu.PlayerPreset);
                Debug.Log("[char-skin] CLI bake OK.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[char-skin] CLI bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        // -------------------------------------------------------------------------------------
        // The bake
        // -------------------------------------------------------------------------------------

        /// <summary>
        /// One composed preset: the def, and every intermediate the bake measured on the way to it.
        /// Handed back whole because a guard that had to re-derive the skinning to check the def
        /// would be checking its own arithmetic.
        /// </summary>
        public sealed class SkinBake
        {
            /// <summary>The def, filled but NOT saved. Its <see cref="CharacterSkinDef.BindMesh"/>
            /// is a live <see cref="Mesh"/> that no asset owns yet.</summary>
            public CharacterSkinDef Def;

            /// <summary>The skeleton as the rig declared it — carries the FIGURE-frame rest whose
            /// inverse the bindposes are, which the def deliberately does not store twice.</summary>
            public RigBone[] Bones;
            public RigSkinning Skin;
            /// <summary>The bind pose's face list: rig 6's <c>idle[0]</c>, in the mesh's vertex order.</summary>
            public RigMeshData Bind;
            public RigMeshBuild Built;

            /// <summary>The rig's OWN vertex tolerance, <c>CharacterIso7.TOL</c>. A guard asserts
            /// against this rather than a number of its own, so a drop that tightens the rig
            /// tightens the guard with it.</summary>
            public double Tolerance;

            public int TotalFrames, DeformingBones;
            public float WorstStepDegrees;
            public string WorstStepAt = "";
            public string SignReport = "";

            public long BindGeometryBytes, BoneWeightBytes, BindposeBytes, ClipBytes;
            public long BindBytes => BindGeometryBytes + BoneWeightBytes + BindposeBytes;
            public long TotalBytes => BindBytes + ClipBytes;
            public long ComposeMilliseconds;
        }

        /// <summary>
        /// Everything the bake decides, with <b>no <see cref="AssetDatabase"/> anywhere in it</b> —
        /// the whole transcription from rig to def, against a caller's host.
        ///
        /// <para><b>This split exists for the guards.</b> The def is not committed to the repo in
        /// this PR (nothing can bake it until the editor slot is granted), so an EditMode guard has
        /// no <c>.asset</c> to read and must compose one. Giving it this entry point rather than a
        /// test-local copy of the same forty decisions means the guards measure THE SHIPPED
        /// TRANSCRIPTION — a second one could be wrong in exactly the way the first is and agree
        /// with it perfectly.</para>
        ///
        /// <para>The oracle stays independent, which is the half that matters: the guards replay
        /// this def through Unity's own linear-blend-skinning arithmetic and compare against rig
        /// <b>6</b>'s <c>facesOf</c> — code this baker does not touch.</para>
        /// </summary>
        /// <param name="target">An existing def to refresh in place (keeps its guid), or null to
        /// create a fresh in-memory instance.</param>
        public static SkinBake Compose(IRigScriptHost host, string preset,
                                       CharacterSkinDef target = null,
                                       Action<string, float> progress = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (string.IsNullOrEmpty(preset)) throw new ArgumentNullException(nameof(preset));
            var sw = Stopwatch.StartNew();

            // ⚠️ ORDER. The pose extractor installs eye → head → body WIDENED; the skin extractor
            // then puts rig 7 on top of that same body object. Reversed, rig 7 prototypes from an
            // unwidened body and every answer below is quietly from a rig nobody else can see.
            // Idempotent — the catalog skips a global the host already carries.
            CharacterSkinExtractor.Load(host);

            string g = CharacterPoseMeshExtractor.GlobalName;
            if (!host.EvaluateBool($"typeof {g}.BUILDS[\"{preset}\"] === 'object'"))
                throw new ArgumentException(
                    $"{g}.BUILDS has no preset '{preset}'. The rig is the authority on its own cast; " +
                    "a typo here would bake the DEFAULT man under a cast member's name.");

            CharacterSkinExtractor.AssertKitLoaded(host, preset);
            Debug.Log($"[char-skin] census — {CharacterSkinExtractor.Census(host, preset)}");

            double tol = CharacterSkinExtractor.Tolerance(host);

            // ---- the skeleton --------------------------------------------------------------
            progress?.Invoke("skeleton", 0.02f);
            RigBone[] rigBones = CharacterSkinExtractor.ReadSkeleton(host, preset);
            CharacterSkinExtractor.AssertRestComposes(rigBones, tol);

            // ---- the skinning, then the geometry it belongs to ------------------------------
            progress?.Invoke("bind mesh", 0.08f);
            RigSkinning skin = CharacterSkinExtractor.ReadSkinning(
                host, preset, CharacterSkinDef.MaxBoneInfluences);
            CharacterSkinExtractor.MarkOwnership(rigBones, skin);

            // The GEOMETRY comes from the flipbook's own extractor, at idle frame 0 — which is
            // exactly rig 7's bind pose. So the skinned bind mesh is byte-identical to the sheet's
            // first frame rather than a second transcription of it, and AssertBindAgrees proves it.
            RigMeshData bind = CharacterPoseMeshExtractor.ExtractPose(host, preset, "idle", 0);
            CharacterSkinExtractor.AssertBindAgrees(bind, skin, tol);

            // One mesh means one material table, and the bind pose's table is all of it: the rig's
            // own golden rule requires rig 6 to emit this exact face list — same count, same order,
            // same material per face — on every clip row, so no pose can introduce a 17th material.
            var shared = new List<RigMaterial>(bind.Materials);
            if (shared.Count > CharacterSkinDef.RampSlots)
                throw new InvalidOperationException(
                    $"'{preset}' references {shared.Count} materials and the facet resolve pass has " +
                    $"{CharacterSkinDef.RampSlots} ramp slots: " + string.Join(", ", NamesOf(shared)) +
                    ".\nThe bake refuses rather than dropping the tail — a silently truncated ramp " +
                    "table is a recoloured character, and this kit has shipped one before. Widen " +
                    "_RampMeta, or split the preset.");

            progress?.Invoke("mesh", 0.14f);
            RigMeshBuild built = RigMeshBuilder.Build(bind, $"CharSkin_{preset}_bind");
            if (built.Vertices != skin.CornerCount)
                throw new InvalidOperationException(
                    $"The built mesh has {built.Vertices} vertices and the rig reported " +
                    $"{skin.CornerCount} skinned corners. The weights would be attached to the " +
                    "wrong vertices — every one of them, by a different amount.");

            AttachSkin(built.Mesh, skin, rigBones);

            // ---- the clips -------------------------------------------------------------------
            string[] anims = CharacterSkinExtractor.Anims(host);
            var clips = new List<CharacterSkinDef.SkinClip>(anims.Length);
            int totalFrames = 0;
            float worstStepDeg = 0f; string worstStepAt = "";

            for (int i = 0; i < anims.Length; i++)
            {
                progress?.Invoke(anims[i], 0.2f + 0.7f * i / anims.Length);
                RigSkinClip rc = CharacterSkinExtractor.ReadClip(host, preset, anims[i], rigBones.Length);

                string[] order = CharacterSkinExtractor.ClipBoneOrder(host, preset, anims[i]);
                for (int b = 0; b < order.Length; b++)
                    if (!string.Equals(order[b], rigBones[b].Id, StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            $"Clip '{anims[i]}' packs bone {b} as '{order[b]}' and the skeleton has " +
                            $"'{rigBones[b].Id}'. The def stores INDICES, so a permuted clip would " +
                            "animate the right skeleton with the wrong limbs and never throw.");

                clips.Add(ToClip(rc, rigBones.Length, ref worstStepDeg, ref worstStepAt));
                totalFrames += rc.Frames;
            }

            // ---- the sign oracle, shared with the flipbook so both defs cannot disagree --------
            progress?.Invoke("turntable sign", 0.92f);
            bool azimuthCcw = CharacterMeshAssetBaker.MeasureFacetSign(host, preset, out string signReport);

            // ---- fill the def ------------------------------------------------------------------
            progress?.Invoke("def", 0.96f);
            CharacterSkinDef def = target != null ? target : ScriptableObject.CreateInstance<CharacterSkinDef>();

            def.Id = IdFor(preset);
            def.Preset = preset;

            // BOTH rigs are pinned. CharacterIso7 is Object.create(CharacterIso6): a body edit
            // changes this def's output without touching rig 7's bytes at all.
            def.SourceRigPath = CharacterSkinExtractor.ScriptPath;
            def.SourceRigRevision = host.EvaluateString($"String({CharacterSkinExtractor.GlobalName}.revision)");
            def.SourceRigSha256 = CharacterSkinExtractor.SourceSha256();
            def.BaseRigPath = CharacterPoseMeshExtractor.ScriptPath;
            def.BaseRigRevision = CharacterPoseMeshExtractor.Revision(host);
            def.BaseRigSha256 = CharacterPoseMeshExtractor.SourceSha256();

            def.CellW = bind.W;
            def.CellH = bind.H;
            def.PivotPx = new Vector2((float)bind.PivotX, (float)bind.PivotY);
            def.PxPerMetre = bind.PxPerMetre;
            def.ElevationDeg = (float)bind.DefaultElev;
            def.LightN = bind.LightN.ToVector3();
            def.Gain = (float)bind.Gain;
            def.Bias = (float)bind.Bias;
            def.Keyline = bind.Keyline;
            def.AzimuthCounterClockwise = azimuthCcw;

            bool anyDither = false;
            foreach (RigMaterial m in shared) anyDither |= m.OrderedDither;
            def.StepMode = anyDither ? CharacterSkinDef.ShadeStep.OrderedDither
                                     : CharacterSkinDef.ShadeStep.HardThreshold;
            def.HardStepThreshold = 0.55f;

            def.Bayer16 = new float[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    def.Bayer16[x * 4 + y] = (float)bind.Bayer[x, y];

            def.Materials = new CharacterSkinDef.Material[shared.Count];
            for (int m = 0; m < shared.Count; m++)
                def.Materials[m] = new CharacterSkinDef.Material
                {
                    Name = shared[m].Name,
                    Colors = shared[m].Ramp,
                    Offset = shared[m].Off,
                    Gain = (float)shared[m].Gain,
                    Bias = double.IsNaN(shared[m].Bias) ? float.NaN : (float)shared[m].Bias,
                    OrderedDither = shared[m].OrderedDither,
                    FixedIndex = shared[m].FixedIndex,
                };

            def.Bones = new CharacterSkinDef.Bone[rigBones.Length];
            for (int b = 0; b < rigBones.Length; b++)
                def.Bones[b] = new CharacterSkinDef.Bone
                {
                    Id = rigBones[b].Id,
                    Parent = rigBones[b].Parent < 0 ? CharacterSkinDef.NoBone : rigBones[b].Parent,
                    RestPosition = rigBones[b].RestPos.ToVector3(),
                    RestRotation = new Quaternion((float)rigBones[b].RestRx, (float)rigBones[b].RestRy,
                                                  (float)rigBones[b].RestRz, (float)rigBones[b].RestRw),
                    OwnsVertex = rigBones[b].OwnsVertex,
                };

            def.MaxInfluences = skin.MaxInfluences;
            def.Clips = clips.ToArray();

            // ⚠ LOAD-BEARING `??=`. MeshStates is the ADR 0041 per-state switch, and it is authored on
            // the COMMITTED asset, not here — refreshing the art must never move the owner's switch.
            // `??=` assigns only when Compose built this def from nothing, so a re-bake of the committed
            // fisher preserves whatever states are turned on. A plain `=` would switch the mesh back off
            // on the next art drop, and the only symptom would be the sprite quietly reappearing.
            def.MeshStates ??= Array.Empty<string>();

            def.BindMesh = built.Mesh;
            sw.Stop();

            long weightBytes = (long)built.Vertices * BoneWeightBytesPerVertex;
            long bindposeBytes = (long)rigBones.Length * BindposeBytesPerBone;
            int deforming = 0; foreach (RigBone b in rigBones) if (b.OwnsVertex) deforming++;

            return new SkinBake
            {
                Def = def,
                Bones = rigBones,
                Skin = skin,
                Bind = bind,
                Built = built,
                Tolerance = tol,
                TotalFrames = totalFrames,
                DeformingBones = deforming,
                WorstStepDegrees = worstStepDeg,
                WorstStepAt = worstStepAt,
                SignReport = signReport,
                BindGeometryBytes = built.BufferBytes,
                BoneWeightBytes = weightBytes,
                BindposeBytes = bindposeBytes,
                ClipBytes = (long)totalFrames * rigBones.Length * BoneKeyBytes,
                ComposeMilliseconds = sw.ElapsedMilliseconds,
            };
        }

        /// <summary>
        /// Bake one preset's skin into its committed def. Refreshes in place when the asset already
        /// exists (same guid, bind mesh replaced), creates it otherwise.
        ///
        /// <para>All the thinking is in <see cref="Compose"/>; this is the half that touches disk.</para>
        /// </summary>
        public static CharacterSkinDef Bake(string preset, Action<string, float> progress = null)
        {
            if (string.IsNullOrEmpty(preset)) throw new ArgumentNullException(nameof(preset));

            string path = AssetPathFor(preset);
            EnsureFolder(AssetFolder);
            var existing = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(path);
            bool created = existing == null;

            SkinBake bake;
            using (IRigScriptHost host = RigScriptHostFactory.Create())
                bake = Compose(host, preset, existing, progress);

            CharacterSkinDef def = bake.Def;
            Debug.Log($"[char-skin] {preset} turntable sign:\n{bake.SignReport}");

            progress?.Invoke("write", 0.98f);

            // Sub-assets are REPLACED, never accumulated — a re-bake that only adds leaves the
            // previous bind mesh orphaned inside the file and the asset grows without bound.
            // Safe after Compose: def.BindMesh already points at the NEW mesh, so what is destroyed
            // here is only what nothing references any more.
            if (!created)
                foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (sub is Mesh old)
                    {
                        AssetDatabase.RemoveObjectFromAsset(old);
                        UnityEngine.Object.DestroyImmediate(old, allowDestroyingAssets: true);
                    }

            if (created) AssetDatabase.CreateAsset(def, path);
            AssetDatabase.AddObjectToAsset(def.BindMesh, def);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            Debug.Log(
                $"[char-skin] {(created ? "Created" : "Refreshed")} {path}\n" +
                $"  bind mesh {bake.Built.Faces} faces → {bake.Built.Triangles:N0} tris / " +
                $"{bake.Built.Vertices:N0} verts, max {bake.Skin.MaxInfluences} influences\n" +
                $"  {def.BoneCount} bones ({bake.DeformingBones} deforming), {def.Clips.Length} clips, " +
                $"{bake.TotalFrames} frames\n" +
                $"  bind {bake.BindBytes / 1024.0:N1} KB (geometry {bake.BindGeometryBytes / 1024.0:N1} + " +
                $"weights {bake.BoneWeightBytes / 1024.0:N1} + bindposes {bake.BindposeBytes / 1024.0:N1}), " +
                $"clips {bake.ClipBytes / 1024.0:N1} KB\n" +
                $"  TOTAL {bake.TotalBytes / 1024.0:N1} KB ({bake.TotalBytes / 1048576.0:F2} MB) against " +
                $"the flipbook's {FlipbookKilobytes / 1024.0:F1} MB — " +
                $"{FlipbookKilobytes * 1024.0 / Math.Max(1, bake.TotalBytes):F1}× smaller\n" +
                $"  {def.Materials.Length} materials of {CharacterSkinDef.RampSlots} ramp slots: " +
                string.Join(", ", NamesOfDef(def.Materials)) + "\n" +
                $"  rig {def.SourceRigPath} rev {def.SourceRigRevision} sha {def.SourceRigSha256[..12]}…, " +
                $"base {def.BaseRigPath} rev {def.BaseRigRevision} sha {def.BaseRigSha256[..12]}…\n" +
                $"  azimuth {(def.AzimuthCounterClockwise ? "CCW (mapping negates)" : "CW")}, " +
                $"step {def.StepMode}, compose {bake.ComposeMilliseconds:N0} ms\n" +
                $"  worst adjacent-frame bone step {bake.WorstStepDegrees:F1}° ({bake.WorstStepAt}) — " +
                "THESE KEYS ARE DISCRETE SAMPLES, see CharacterSkinDef\n" +
                $"  usable = {def.IsUsable()}");

            if (!def.IsUsable())
                throw new InvalidOperationException($"Baked {path} is not usable — see the fields above.");
            return def;
        }

        // -------------------------------------------------------------------------------------

        /// <summary>
        /// Attach the bone weights and bindposes to a mesh the ordinary builder produced.
        ///
        /// <para><b>The bindposes come from the rig's <c>skeletonWorld</c>, and the local rest chain
        /// is proven to reproduce it first</b> (<see cref="CharacterSkinExtractor.AssertRestComposes"/>,
        /// measured at 1.3e-16 m). That matters because Unity forms each bone matrix as
        /// <c>bone.localToWorldMatrix * bindposes[i]</c>: the left half is composed from the LOCAL
        /// transforms this def stores, the right half from the world table. Two halves that disagree
        /// deform the character in the rest pose, identically in every clip, so no clip guard would
        /// ever localise it.</para>
        /// </summary>
        static void AttachSkin(Mesh mesh, RigSkinning skin, RigBone[] bones)
        {
            var weights = new BoneWeight[skin.CornerCount];
            for (int v = 0; v < skin.CornerCount; v++)
            {
                var bw = new BoneWeight();
                for (int j = 0; j < skin.Width; j++)
                {
                    int b = skin.Bone[v * skin.Width + j];
                    float w = (float)skin.Weight[v * skin.Width + j];

                    // Padding is bone −1 at weight 0. Unity has no "no bone": an out-of-range index
                    // is undefined behaviour, so padding is written as bone 0 at weight 0, which
                    // contributes nothing.
                    if (b < 0) { b = 0; w = 0f; }

                    if (j == 0) { bw.boneIndex0 = b; bw.weight0 = w; }
                    else if (j == 1) { bw.boneIndex1 = b; bw.weight1 = w; }
                    else throw new InvalidOperationException(
                        $"Corner {v} carries influence {j}; this path stores " +
                        $"{CharacterSkinDef.MaxBoneInfluences}. Refusing to drop it.");
                }
                weights[v] = bw;
            }

            var bindposes = new Matrix4x4[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                var pos = new Vector3((float)bones[b].WorldPos.X, (float)bones[b].WorldPos.Y,
                                      (float)bones[b].WorldPos.Z);
                var rot = new Quaternion((float)bones[b].WorldRx, (float)bones[b].WorldRy,
                                         (float)bones[b].WorldRz, (float)bones[b].WorldRw);
                bindposes[b] = Matrix4x4.TRS(pos, rot, Vector3.one).inverse;
            }

            mesh.boneWeights = weights;
            mesh.bindposes = bindposes;
        }

        /// <summary>
        /// Flatten one rig clip into the def's frame-major key table.
        ///
        /// <para>Reports the worst adjacent-frame rotation while it goes. On <c>walk</c> that reaches
        /// <b>178°</b> — the tube limbs carry an arbitrary axial roll whose reference flips as the
        /// limb swings through vertical, and 20 of walk's 315 adjacent bone-steps exceed 90°. The
        /// POSED MESH is right at every stored frame (the rig's own golden rule measures 4.02e-13 m
        /// across 56 rows), so this is not a defect — but it is the reason these keys must be played
        /// as DISCRETE SAMPLES. Interpolating between two keys a half-turn apart sweeps a limb
        /// through an orientation that is in no frame of the animation.</para>
        /// </summary>
        static CharacterSkinDef.SkinClip ToClip(RigSkinClip rc, int boneCount,
                                                ref float worstDeg, ref string worstAt)
        {
            var keys = new CharacterSkinDef.BoneKey[rc.Frames * boneCount];
            for (int f = 0; f < rc.Frames; f++)
                for (int b = 0; b < boneCount; b++)
                {
                    int i = f * boneCount + b;
                    keys[i] = new CharacterSkinDef.BoneKey
                    {
                        Position = rc.Pos[i].ToVector3(),
                        Rotation = new Quaternion((float)rc.Rx[i], (float)rc.Ry[i],
                                                  (float)rc.Rz[i], (float)rc.Rw[i]),
                    };
                    if (f == 0) continue;
                    int p = (f - 1) * boneCount + b;
                    double dot = Math.Abs(rc.Rx[p] * rc.Rx[i] + rc.Ry[p] * rc.Ry[i]
                                        + rc.Rz[p] * rc.Rz[i] + rc.Rw[p] * rc.Rw[i]);
                    var deg = (float)(2 * Math.Acos(Math.Min(1, dot)) * 180 / Math.PI);
                    if (deg > worstDeg) { worstDeg = deg; worstAt = $"{rc.Anim} bone {b} f{f - 1}→{f}"; }
                }

            return new CharacterSkinDef.SkinClip
            {
                Anim = rc.Anim,
                State = rc.Anim,
                FramesPerSecond = (float)(1000.0 / Math.Max(1.0, rc.Ms)),
                Settle = rc.Settle,
                Loop = rc.Loop,
                FrameCount = rc.Frames,
                Keys = keys,
                ParkedParts = rc.Parked ?? Array.Empty<string>(),
                Mount = rc.Mount,
                Carry = rc.Carry,
                Power = rc.Power,
            };
        }

        static string[] NamesOfDef(CharacterSkinDef.Material[] mats)
        {
            var names = new string[mats.Length];
            for (int i = 0; i < mats.Length; i++) names[i] = mats[i].Name;
            return names;
        }

        static string[] NamesOf(IReadOnlyList<RigMaterial> mats)
        {
            var names = new string[mats.Count];
            for (int i = 0; i < mats.Count; i++) names[i] = mats[i].Name;
            return names;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
