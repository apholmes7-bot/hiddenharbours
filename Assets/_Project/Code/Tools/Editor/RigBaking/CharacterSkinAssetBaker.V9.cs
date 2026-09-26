using System;
using System.Collections.Generic;
using System.Diagnostics;
using HiddenHarbours.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tools.RigBaking
{
    // Rig 9's half of the skin bake. Compose reads rig 7 over its rig 6 base; ComposeV9 reads rig 9
    // alone and fills the same CharacterSkinDef under the v9 tone rule. The mesh builder, the skin
    // attach, the clip packer and the turntable-sign adjudication are shared, so a v9 def differs
    // from a rig 7 def only in what the rig said.
    public static partial class CharacterSkinAssetBaker
    {
        /// <summary>The rig the cast is baked from: rig 9 since the intake's Phase B (2026-09-26), which
        /// baked all ten figures from it and shot their plates against rig 7. This is the one line
        /// that switches it. Read-only static rather than a const, so the branch that is not taken
        /// still compiles and still warns nobody.</summary>
        public static readonly string LiveRig = CharacterSkinExtractor.V9CatalogKey;

        public static bool LiveRigIsV9 => LiveRig == CharacterSkinExtractor.V9CatalogKey;

        /// <summary>
        /// Compose one preset's skin def from rig 9, without touching the AssetDatabase. The bind
        /// mesh is the preset's rest face (<see cref="CharacterSkinExtractor.DefaultFaceMeshJs9"/>);
        /// every clip rig 9 ships is baked under its (anim, carry) key; the materials are the ones
        /// that mesh paints, each with its own gain, bias and tone window, and the bake refuses a
        /// figure over <see cref="CharacterSkinDef.MaxMaterials"/> for <see cref="ToneRule.V9"/>
        /// rather than dropping the tail.
        /// </summary>
        public static SkinBake ComposeV9(IRigScriptHost host, string preset,
                                         CharacterSkinDef target = null,
                                         Action<string, float> progress = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (string.IsNullOrEmpty(preset)) throw new ArgumentNullException(nameof(preset));
            var sw = Stopwatch.StartNew();

            CharacterSkinExtractor.Load9(host);
            CharacterSkinExtractor.AssertPreset9(host, preset);
            Debug.Log($"[char-skin] census — {CharacterSkinExtractor.Census9(host, preset)}");
            double tol = CharacterSkinExtractor.V9Tolerance;
            string g = CharacterSkinExtractor.V9GlobalName;

            progress?.Invoke("skeleton", 0.02f);
            RigBone[] rigBones = CharacterSkinExtractor.ReadSkeleton9(host, preset);
            CharacterSkinExtractor.AssertRestComposes(rigBones, tol);

            progress?.Invoke("bind mesh", 0.08f);
            string meshJs = CharacterSkinExtractor.DefaultFaceMeshJs9(host, preset);
            RigSkinning skin = CharacterSkinExtractor.ReadSkinning(
                host, preset, CharacterSkinDef.MaxBoneInfluences, meshJs);
            CharacterSkinExtractor.MarkOwnership(rigBones, skin);

            List<RigMaterial9> mats = CharacterSkinExtractor.ReadMaterials9(host, preset, meshJs);
            int limit = CharacterSkinDef.MaxMaterials(ToneRule.V9);
            if (mats.Count > limit)
            {
                var names = new List<string>(mats.Count);
                foreach (RigMaterial9 m in mats) names.Add(m.Name);
                throw new InvalidOperationException(
                    $"'{preset}' paints {mats.Count} materials and the v9 tone rule has {limit} slots: " +
                    string.Join(", ", names) + ".\nThe bake refuses rather than dropping the tail — a " +
                    "silently truncated material table is a recoloured character.");
            }

            RigMeshData bind = CharacterSkinExtractor.NewData9(host, preset, $"{g}:{preset}:bind", meshJs, mats);
            CharacterSkinExtractor.AssertBindAgrees(bind, skin, tol);

            progress?.Invoke("mesh", 0.14f);
            RigMeshBuild built = RigMeshBuilder.Build(bind, $"CharSkin9_{preset}_bind");
            if (built.Vertices != skin.CornerCount)
                throw new InvalidOperationException(
                    $"The built mesh has {built.Vertices} vertices and rig 9 reported " +
                    $"{skin.CornerCount} skinned corners. The weights would be attached to the " +
                    "wrong vertices — every one of them, by a different amount.");
            AttachSkin(built.Mesh, skin, rigBones);

            string[] clipNames = CharacterSkinExtractor.ClipNames9(host);
            var clips = new List<CharacterSkinDef.SkinClip>(clipNames.Length);
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            int totalFrames = 0;
            float worstStepDeg = 0f; string worstStepAt = "";
            for (int i = 0; i < clipNames.Length; i++)
            {
                progress?.Invoke(clipNames[i], 0.2f + 0.7f * i / clipNames.Length);
                RigSkinClip rc = CharacterSkinExtractor.ReadClip9(host, preset, clipNames[i], rigBones,
                                                                  out string state);
                if (keys.TryGetValue(state, out string first))
                    throw new InvalidOperationException(
                        $"Rig 9 clips '{first}' and '{clipNames[i]}' both key as '{state}'. The def " +
                        "finds a clip by its key, so one of them could never be played.");
                keys.Add(state, clipNames[i]);
                clips.Add(ToClip(rc, state, rigBones.Length, ref worstStepDeg, ref worstStepAt));
                totalFrames += rc.Frames;
            }

            progress?.Invoke("turntable sign", 0.92f);
            bool azimuthCcw = MeasureFacetSignV9(host, preset, out string signReport);

            progress?.Invoke("def", 0.96f);
            CharacterSkinDef def = target != null ? target : ScriptableObject.CreateInstance<CharacterSkinDef>();
            def.Id = IdFor(preset);
            def.Preset = preset;
            def.SourceRigPath = CharacterSkinExtractor.V9ScriptPath;
            def.SourceRigRevision = host.EvaluateString($"String({g}.revision)");
            def.SourceRigSha256 = CharacterSkinExtractor.SourceSha256V9();
            // Rig 9 has no base rig; its second file is its poses, and a re-bake must notice when
            // either moves.
            def.BaseRigPath = CharacterSkinExtractor.V9PosesPath;
            def.BaseRigRevision = def.SourceRigRevision;
            def.BaseRigSha256 = CharacterSkinExtractor.PosesSha256V9();
            def.CellW = bind.W;
            def.CellH = bind.H;
            def.PivotPx = new Vector2((float)bind.PivotX, (float)bind.PivotY);
            def.PxPerMetre = bind.PxPerMetre;
            def.ElevationDeg = (float)bind.DefaultElev;
            // The v9 tone rule reads the WORLD key (fleetKey) for the facet normal and the SCREEN key
            // for the form term; the reference data above carries the screen key for the turntable
            // oracle, which is rig 9's own render.
            def.ToneRule = ToneRule.V9;
            def.LightN = CharacterSkinExtractor.V9Shading3(host, "fleetKey").ToVector3();
            def.KeyScreen = CharacterSkinExtractor.V9Shading3(host, "key").ToVector3();
            def.Form = (float)CharacterSkinExtractor.V9ShadingNumber(host, "form");
            def.FormMid = (float)CharacterSkinExtractor.V9ShadingNumber(host, "formMid");
            def.Gain = 1f;
            def.Bias = 0f;
            def.Keyline = bind.Keyline;
            def.AzimuthCounterClockwise = azimuthCcw;
            def.StepMode = CharacterSkinDef.ShadeStep.HardThreshold;
            def.HardStepThreshold = 0.55f;
            def.Bayer16 = new float[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    def.Bayer16[x * 4 + y] = (float)bind.Bayer[x, y];
            def.Materials = new CharacterSkinDef.Material[mats.Count];
            for (int m = 0; m < mats.Count; m++)
            {
                RigMaterial9 src = mats[m];
                def.Materials[m] = src.Fixed
                    ? new CharacterSkinDef.Material
                    {
                        Name = src.Name, Colors = src.Ramp, Offset = 0, Gain = 1f, Bias = 0f,
                        OrderedDither = false, FixedIndex = -1, ToneLo = 0, ToneHi = 0,
                    }
                    : new CharacterSkinDef.Material
                    {
                        Name = src.Name, Colors = src.Ramp, Offset = src.Off,
                        Gain = (float)src.Gain, Bias = (float)src.Bias,
                        OrderedDither = false, FixedIndex = -1, ToneLo = src.Lo, ToneHi = src.Hi,
                    };
            }
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
        /// Rig 9's turntable sign, measured the way rig 7's is: at each frame of the shared plan,
        /// rig 9's own render facing east is compared with the C# rasterizer's render of the SAME
        /// posed faces at dir -2 and +2, and the shared adjudication decides (or refuses).
        /// </summary>
        public static bool MeasureFacetSignV9(IRigScriptHost host, string preset, out string report)
        {
            CharacterSkinExtractor.Load9(host);
            CharacterSkinExtractor.AssertPreset9(host, preset);
            var readings = new List<CharacterMeshAssetBaker.FacetSignReading>();
            foreach (var (state, frame) in CharacterMeshAssetBaker.FacetSignPlan(
                         CharacterSkinExtractor.Anims9(host),
                         anim => CharacterSkinExtractor.FrameCount9(host, anim)))
                readings.Add(ReadFacetSignV9(host, preset, state, frame));
            return CharacterMeshAssetBaker.AdjudicateFacetSign(readings, out report);
        }

        static CharacterMeshAssetBaker.FacetSignReading ReadFacetSignV9(IRigScriptHost host, string preset,
                                                                         string clip, int frame)
        {
            byte[] truthEast = CharacterSkinExtractor.RenderTruth9(host, preset, clip, frame, 2,
                                                                   out RigMeshData pose);
            var negView = new RigViewOptions(-2, pose.DefaultElev);
            var posView = new RigViewOptions(+2, pose.DefaultElev);
            byte[] neg = RigMeshReferenceRasterizer.RenderFromFaces(
                pose, negView, RigTrigBasis.FromScriptHost(host, negView));
            byte[] pos = RigMeshReferenceRasterizer.RenderFromFaces(
                pose, posView, RigTrigBasis.FromScriptHost(host, posView));
            return new CharacterMeshAssetBaker.FacetSignReading(clip, frame,
                RigMeshReferenceRasterizer.Compare(truthEast, neg, pose.W, pose.H),
                RigMeshReferenceRasterizer.Compare(truthEast, pos, pose.W, pose.H));
        }
    }
}
