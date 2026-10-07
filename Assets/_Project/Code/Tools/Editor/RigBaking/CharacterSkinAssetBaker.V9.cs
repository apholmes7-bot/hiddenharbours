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
    // from a rig 7 def only in what the rig said. Rig 10 (the rig 10 intake, 2026-10-02) bakes through
    // the same ComposeV9 from a host loaded with CharacterRigKit.Rig10: its face marks, smooth normals
    // and no-keyline default ride the same def. Since kit 10.3 (2026-10-03) the gate tolerance, the
    // face cull floor, the marks' pixel edge and the dither are read from the rig's exports (TOL,
    // DITHER) on that host, never copied here.
    public static partial class CharacterSkinAssetBaker
    {
        /// <summary>The rig the cast is baked from: rig 10 since the rig 10 intake's Phase B
        /// (2026-10-02), which baked all ten figures from kit 10.2 and shot their plates against rig 9.2;
        /// kit 10.3 landed over it on 2026-10-03, and its Phase B re-bakes the ten from it.
        /// Rig 9 was live from the character intake's Phase B (2026-09-26) until then. This is the one
        /// line that switches it: <see cref="CharacterSkinExtractor.V9CatalogKey"/> goes back to rig 9.2
        /// and <see cref="CharacterSkinExtractor.CatalogKey"/> to rig 7, each with a re-bake. Read-only
        /// static rather than a const, so the branches that are not taken still compile and still warn
        /// nobody.</summary>
        public static readonly string LiveRig = CharacterRigKit.Rig10.CatalogKey;

        /// <summary>The kit <see cref="LiveRig"/> names, which <see cref="Bake"/> loads into its host
        /// before <see cref="ComposeV9"/> reads it: rig 9 or rig 10, or null while it names rig 7.</summary>
        public static CharacterRigKit LiveKit =>
            LiveRig == CharacterRigKit.Rig10.CatalogKey ? CharacterRigKit.Rig10
            : LiveRig == CharacterRigKit.Rig9.CatalogKey ? CharacterRigKit.Rig9
            : null;

        /// <summary>True while the live rig bakes through <see cref="ComposeV9"/>: rig 9 or rig 10, and
        /// <see cref="LiveKit"/> says which.</summary>
        public static bool LiveRigIsV9 => LiveKit != null;

        /// <summary>
        /// Compose one preset's skin def from rig 9, without touching the AssetDatabase. The bind
        /// mesh is EVERY face with every face group (<see cref="CharacterSkinExtractor.FaceMeshJs9"/>,
        /// since character PR 2a), each face carrying its group, its cull role and its head flag in
        /// UV1; every clip rig 9 ships is baked under its (anim, carry) key with its face track and
        /// its tool track; the materials are the ones that mesh paints, each with its own gain, bias
        /// and tone window, and the bake refuses a figure over <see cref="CharacterSkinDef.MaxMaterials"/>
        /// for <see cref="ToneRule.V9"/> rather than dropping the tail. The blink, the look, the face
        /// thresholds, the head snap, the edge and the keyline mix are read off the rig (the blink and
        /// the look checked against the committed sidecar), and the finished def is painted against
        /// rig 9's own render (<see cref="CharacterSkinInk9"/>) and the match recorded.
        ///
        /// <para>The host says which rig (<see cref="CharacterSkinExtractor.KitOf"/>): rig 9 unless it was
        /// loaded with <see cref="CharacterRigKit.Rig10"/>. A rig 10 def also carries the face as point
        /// marks (each mark's turn band in UV1.w, its flags and the faces' smooth normals in UV2, the two
        /// mark floors on the def), no role thresholds (rig 10 culls its face by the marks' own
        /// <c>az</c>), and <see cref="CharacterSkinDef.KeylineDefault"/> off: rig 10 draws no keyline
        /// unless asked (K4).</para>
        /// </summary>
        public static SkinBake ComposeV9(IRigScriptHost host, string preset,
                                         CharacterSkinDef target = null,
                                         Action<string, float> progress = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (string.IsNullOrEmpty(preset)) throw new ArgumentNullException(nameof(preset));
            var sw = Stopwatch.StartNew();

            CharacterSkinExtractor.Load9(host);
            CharacterRigKit kit = CharacterSkinExtractor.KitOf(host);
            CharacterSkinExtractor.AssertPreset9(host, preset);
            Debug.Log($"[char-skin] census — {CharacterSkinExtractor.Census9(host, preset)}");
            double tol = CharacterSkinExtractor.GateTolerance9(host);
            string g = kit.GlobalName;

            progress?.Invoke("skeleton", 0.02f);
            RigBone[] rigBones = CharacterSkinExtractor.ReadSkeleton9(host, preset);
            CharacterSkinExtractor.AssertRestComposes(rigBones, tol);

            progress?.Invoke("bind mesh", 0.08f);
            string[] faceGroups = CharacterSkinExtractor.FaceGroupOrder9(host);
            string meshJs = CharacterSkinExtractor.FaceMeshJs9(host, preset);
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
            CharacterSkinExtractor.FaceThresholds9 thresholds = CharacterSkinExtractor.ReadFaceThresholds9(host, preset);
            CharacterSkinExtractor.ResolveFaceRoles9(bind.Faces, faceGroups, thresholds, $"{g}:{preset}:bind");
            bind.CarriesFaceAttributes = true;
            bind.CarriesMarkAttributes = kit.FaceMarks;
            CharacterSkinExtractor.AssertBindAgrees(bind, skin, tol);

            progress?.Invoke("mesh", 0.14f);
            RigMeshBuild built = RigMeshBuilder.Build(bind, $"{kit.MeshPrefix}_{preset}_bind");
            if (built.Vertices != skin.CornerCount)
                throw new InvalidOperationException(
                    $"The built mesh has {built.Vertices} vertices and {kit.Name} reported " +
                    $"{skin.CornerCount} skinned corners. The weights would be attached to the " +
                    "wrong vertices — every one of them, by a different amount.");
            AttachSkin(built.Mesh, skin, rigBones);

            string[] clipNames = CharacterSkinExtractor.ClipNames9(host);
            var clips = new List<CharacterSkinDef.SkinClip>(clipNames.Length);
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            int totalFrames = 0, faceClips = 0, toolClips = 0;
            float worstStepDeg = 0f; string worstStepAt = "";
            for (int i = 0; i < clipNames.Length; i++)
            {
                progress?.Invoke(clipNames[i], 0.2f + 0.7f * i / clipNames.Length);
                RigSkinClip rc = CharacterSkinExtractor.ReadClip9(host, preset, clipNames[i], rigBones,
                                                                  out string state);
                if (keys.TryGetValue(state, out string first))
                    throw new InvalidOperationException(
                        $"{kit.Title} clips '{first}' and '{clipNames[i]}' both key as '{state}'. The def " +
                        "finds a clip by its key, so one of them could never be played.");
                keys.Add(state, clipNames[i]);
                CharacterSkinDef.SkinClip sc = ToClip(rc, state, rigBones.Length, ref worstStepDeg, ref worstStepAt);
                sc.Face = rc.Face ?? Array.Empty<byte>();
                sc.Tool = rc.Tool ?? Array.Empty<CharacterSkinDef.ToolKey>();
                if (sc.HasFaceTrack) faceClips++;
                if (sc.Tool.Length > 0) toolClips++;
                clips.Add(sc);
                totalFrames += rc.Frames;
            }

            progress?.Invoke("face, blink and look", 0.905f);
            CharacterSkinExtractor.AssertOverlaysMatchSidecar9(host, preset);
            CharacterSkinExtractor.Blink9 blink = CharacterSkinExtractor.ReadBlink9(host);
            CharacterSkinExtractor.Look9 look = CharacterSkinExtractor.ReadLook9(host, preset, rigBones);
            int[] restFace = CharacterSkinExtractor.RestFace9(host, preset, faceGroups);
            double cullFloor = CharacterSkinExtractor.CullFloor9(host);
            CharacterSkinExtractor.MarkCull9 markCull = CharacterSkinExtractor.ReadMarkCull9(host);
            bool keylineDefault = CharacterSkinExtractor.KeylineDefault9(host);

            progress?.Invoke("turntable sign", 0.92f);
            bool azimuthCcw = MeasureFacetSignV9(host, preset, out string signReport);

            progress?.Invoke("def", 0.96f);
            CharacterSkinDef def = target != null ? target : ScriptableObject.CreateInstance<CharacterSkinDef>();
            def.Id = IdFor(preset);
            def.Preset = preset;
            def.SourceRigPath = kit.ScriptPath;
            def.SourceRigRevision = host.EvaluateString($"String({g}.revision)");
            def.SourceRigSha256 = CharacterSkinExtractor.SourceSha256V9(kit);
            // Rigs 9 and 10 have no base rig; the second file is the poses, and a re-bake must notice
            // when either moves.
            def.BaseRigPath = kit.PosesPath;
            def.BaseRigRevision = def.SourceRigRevision;
            def.BaseRigSha256 = CharacterSkinExtractor.PosesSha256V9(kit);
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
            // The figure ink, the face, the blink and the look: all the rig's, none of them
            // re-derived (character PR 2a).
            def.Edge = (float)CharacterSkinExtractor.V9ShadingNumber(host, "edge");
            def.KeylineMix = (float)CharacterSkinExtractor.V9ShadingNumber(host, "keylineMix");
            def.HeadSnap = CharacterSkinExtractor.HeadSnap9(host);
            def.HeadMid = CharacterSkinExtractor.HeadMid9(host, preset).ToVector3();
            def.KeylineDefault = keylineDefault;
            def.FaceGroups = faceGroups;
            def.RestFace = restFace;
            def.FaceMinToward = thresholds.ToVector4();
            def.FaceCullFloor = (float)cullFloor;
            def.FaceMarkAzFloor = (float)markCull.AzFloor;
            def.FaceMarkEdge = (float)markCull.Edge;
            def.BlinkSteps = blink.Steps;
            def.BlinkIntervalSeconds = blink.IntervalSeconds;
            def.BlinkDoubleChance = blink.DoubleChance;
            def.BlinkDoubleGapSeconds = blink.DoubleGapSeconds;
            def.BlinkSkipGroups = blink.SkipGroups;
            def.LookNeckBone = look.Neck;
            def.LookHeadBone = look.Head;
            def.LookChestBone = look.Chest;
            def.LookSplitNeck = look.SplitNeck;
            def.LookSplitHead = look.SplitHead;
            def.LookYawLimits = look.Yaw;
            def.LookPitchLimits = look.Pitch;
            def.LookHeadShare = look.HeadShare;
            def.LookEyesBeyondDeg = look.EyesBeyondDeg;
            def.LookEyes = look.Eyes;
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

            progress?.Invoke("ink", 0.97f);
            CharacterSkinInk9.Reading[] ink = CharacterSkinInk9.MeasureAll(host, def, keys);
            string inkReport = CharacterSkinInk9.Report(def, ink, out int inkWorst);
            sw.Stop();

            var c = System.Globalization.CultureInfo.InvariantCulture;
            int marks = 0;
            foreach (RigFace f in bind.Faces) if (f.Mark) marks++;
            string cullReport = kit.FaceMarks
                ? $"{marks} point marks culled by their own az (floor {markCull.AzFloor.ToString("R", c)}, " +
                  $"pixel edge {markCull.Edge.ToString("R", c)}; UV1.w, UV2), no roles"
                : $"cull by role {thresholds}";
            string faceReport =
                $"{faceGroups.Length} face groups bound, {built.GroupedFaces} of {built.Faces} faces in them (UV1); " +
                $"{cullReport}, floor {cullFloor.ToString("R", c)}; keyline {(keylineDefault ? "on" : "off")} " +
                "unless asked; rest face " +
                $"[{string.Join(", ", RestNames(faceGroups, restFace))}]; {faceClips}/{clips.Count} clips carry a " +
                $"face track, {toolClips} a tool track (data only); edge {def.Edge.ToString("R", c)}, keyline mix " +
                $"{def.KeylineMix.ToString("R", c)}, head snap {(def.HeadSnap ? "on" : "off")} at " +
                $"({def.HeadMid.x.ToString("R", c)}, {def.HeadMid.y.ToString("R", c)}, {def.HeadMid.z.ToString("R", c)}); " +
                $"blink {blink}; look {look}";

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
                FaceReport = faceReport,
                InkReport = inkReport,
                InkWorstCluster = inkWorst,
                InkReadings = ink,
                BindGeometryBytes = built.BufferBytes,
                BoneWeightBytes = weightBytes,
                BindposeBytes = bindposeBytes,
                ClipBytes = (long)totalFrames * rigBones.Length * BoneKeyBytes,
                ComposeMilliseconds = sw.ElapsedMilliseconds,
            };
        }

        /// <summary>The rest face's group names, for the face report.</summary>
        static string[] RestNames(string[] groups, int[] ids)
        {
            var names = new string[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                names[i] = ids[i] >= 1 && ids[i] <= groups.Length ? groups[ids[i] - 1] : "?";
            return names;
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
