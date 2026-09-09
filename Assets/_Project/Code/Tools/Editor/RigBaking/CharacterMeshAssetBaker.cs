using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Bakes one preset's whole pose flipbook into a committed <see cref="CharacterMeshDef"/> —
    /// <c>Assets/_Project/Data/Characters/&lt;preset&gt;.asset</c>, id <c>charmesh.&lt;preset&gt;</c>
    /// — through the ADR 0022 tail end unchanged: rig faces → <see cref="RigMeshBuilder"/> meshes,
    /// shading facts straight off the rig, sub-assets replaced in place so the guid survives.
    ///
    /// <para><b>Two things are MEASURED at bake time and stored, never declared:</b></para>
    /// <list type="number">
    ///   <item><b>The turntable sign, adjudicated by pixels.</b> The label probe answers which way
    ///   the rig's LABELS run — a sprite-sheet fact. The facet path needs a different one: which
    ///   sign of dir-units reproduces the rig's East view through the shared projection. This rig's
    ///   turntable is <c>th = −dir·π/4</c> where every hull's is <c>+dir·π/4</c>, so the two
    ///   lineages DISAGREE and declaring either ships a mirrored character — which this kit has
    ///   done twice. The bake renders East, poses the oracle at −2 and +2, and takes the winner;
    ///   the loser must be catastrophically wrong (a 4× sabotage margin) or the bake refuses.</item>
    ///   <item><b>The rig's source hash</b> (<see cref="CharacterMeshDef.SourceRigSha256"/>), so a
    ///   def baked from a superseded rig is a red test rather than a wrong character on screen.
    ///   <see cref="HullMeshDef"/> has no such field and the hull path pays for it in the same
    ///   coin the sheet path does: nothing notices.</item>
    /// </list>
    ///
    /// <para><b>The recipe is the SHEET recipe, plus four.</b> States come from
    /// <c>CharacterRigBakeMenu.PlayerStates</c> grown by the rig's own
    /// <see cref="CharacterRigBaker.ExpandCarryStances"/> — the identical set the sprite bake
    /// enumerates, because ADR 0041 retires a sheet per state AT PARITY and parity is not a
    /// question two different recipes can answer. The four exceptions are
    /// <c>swim, tread, sleep, drive</c>: the sheet path declines them purely because they ship at a
    /// re-windowed 64×88 cell it cannot emit (<c>PlayerAnimsBakedElsewhere</c>). A mesh has no
    /// cell, so the mesh path simply carries them.</para>
    ///
    /// <para>⚠️ <b>This baker writes assets and therefore needs the editor.</b>
    /// <see cref="Measure"/> is the same walk with nothing written and every mesh destroyed — it
    /// runs headless, in CI, on the V8 host alone, and is what produces the numbers in the PR body
    /// without an editor slot.</para>
    /// </summary>
    public static class CharacterMeshAssetBaker
    {
        public const string AssetFolder = "Assets/_Project/Data/Characters";

        public static string AssetPathFor(string preset) => $"{AssetFolder}/{preset}.asset";

        /// <summary>Def id — append-only and stable, per CLAUDE.md §5.</summary>
        public static string IdFor(string preset) => "charmesh." + preset;

        /// <summary>
        /// The anims the SHEET baker declines for the cell and the mesh path carries for free.
        /// Not a widening of scope: these four are already authored, already drawn in game from
        /// imported 64×88 PNGs, and the only reason the recipe could not remake them was a raster
        /// window. Named here rather than inlined so the day the sheet baker learns a per-state
        /// cell, the two lists can be compared instead of re-derived.
        /// </summary>
        public static readonly string[] OffDeckAnims = { "swim", "tread", "sleep", "drive" };

        // -------------------------------------------------------------------------------------
        // The recipe
        // -------------------------------------------------------------------------------------

        /// <summary>
        /// Every state one preset's flipbook holds: the sheet recipe, plus the off-deck four, grown
        /// by the rig's own carry table under the sheet path's own exclusions.
        /// </summary>
        public static IReadOnlyList<CharacterState> Recipe(IRigScriptHost host)
        {
            var seed = new List<CharacterState>(CharacterRigBakeMenu.PlayerStates);
            foreach (string anim in OffDeckAnims)
                seed.Add(new CharacterState(anim));

            return CharacterRigBaker.ExpandCarryStances(
                host, CharacterPoseMeshExtractor.GlobalName, seed,
                CharacterRigBakeMenu.PlayerCarryStanceExclusions);
        }

        // -------------------------------------------------------------------------------------
        // Measurement (no editor, no assets — this is what CI runs)
        // -------------------------------------------------------------------------------------

        /// <summary>What one flipbook costs, and what it is made of.</summary>
        public sealed class MeshBudget
        {
            public string Preset;
            public int States, Meshes;
            public long Faces, Vertices, Triangles;
            public long BufferBytes;
            public IReadOnlyList<string> MaterialsUsed = Array.Empty<string>();

            public override string ToString() =>
                $"{Preset}: {States} states, {Meshes} pose meshes, {Faces:N0} faces, " +
                $"{Triangles:N0} tris, {Vertices:N0} verts, {BufferBytes / 1024.0:N0} KB " +
                $"({BufferBytes / 1048576.0:F1} MB) of GPU buffer, " +
                $"{MaterialsUsed.Count} materials of {CharacterMeshDef.RampSlots} ramp slots";
        }

        /// <summary>
        /// Walk the whole recipe, build every mesh, sum the cost, then destroy the meshes. No
        /// AssetDatabase, no graphics device — the numbers in the PR body come from here.
        /// </summary>
        public static MeshBudget Measure(IRigScriptHost host, string preset,
                                         IReadOnlyList<CharacterState> states = null)
        {
            states ??= Recipe(host);
            var shared = new List<RigMaterial>();
            var budget = new MeshBudget { Preset = preset, States = states.Count };

            foreach (CharacterState state in states)
            {
                int frames = CharacterRigBaker.FramesOf(host, CharacterPoseMeshExtractor.GlobalName,
                                                        state.Anim);
                for (int f = 0; f < frames; f++)
                {
                    RigMeshData data = CharacterPoseMeshExtractor.ExtractPose(host, preset, state, f);
                    MergeMaterials(shared, data, state, f);
                    CharacterPoseMeshExtractor.RemapMaterials(data, shared);

                    RigMeshBuild built = RigMeshBuilder.Build(data, $"tmp_{state.Key}_{f}");
                    budget.Meshes++;
                    budget.Faces += built.Faces;
                    budget.Vertices += built.Vertices;
                    budget.Triangles += built.Triangles;
                    budget.BufferBytes += built.BufferBytes;
                    UnityEngine.Object.DestroyImmediate(built.Mesh);
                }
            }

            budget.MaterialsUsed = OrderNames(host, preset, shared);
            return budget;
        }

        // -------------------------------------------------------------------------------------
        // The bake
        // -------------------------------------------------------------------------------------

        [MenuItem(RigMeshGate.MenuRoot + "/Bake character poses (the player)", priority = 220)]
        public static void BakePlayer()
        {
            try
            {
                Bake(CharacterRigBakeMenu.PlayerPreset);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem(RigMeshGate.MenuRoot + "/Bake character poses (the player)", validate = true)]
        public static bool BakePlayerValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (<c>-executeMethod</c>).</summary>
        public static void BakePlayerCli()
        {
            try
            {
                Bake(CharacterRigBakeMenu.PlayerPreset);
                Debug.Log("[char-mesh] CLI bake OK.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[char-mesh] CLI bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Bake one preset's flipbook into its committed def. Refreshes in place when the asset
        /// already exists (same guid, meshes replaced), creates it otherwise.
        /// </summary>
        public static CharacterMeshDef Bake(string preset,
                                            IReadOnlyList<CharacterState> states = null,
                                            Action<string, float> progress = null)
        {
            if (string.IsNullOrEmpty(preset)) throw new ArgumentNullException(nameof(preset));

            using IRigScriptHost host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.Load(host);
            states ??= Recipe(host);

            string g = CharacterPoseMeshExtractor.GlobalName;
            if (!host.EvaluateBool($"typeof {g}.BUILDS[\"{preset}\"] === 'object'"))
                throw new ArgumentException(
                    $"{g}.BUILDS has no preset '{preset}'. The rig is the authority on its own cast; " +
                    "a typo here would bake the DEFAULT man under a cast member's name.");

            var shared = new List<RigMaterial>();
            var clips = new List<CharacterMeshDef.PoseClip>();
            var meshes = new List<Mesh>();
            RigMeshData first = null;
            long faces = 0, verts = 0, tris = 0, bytes = 0;

            for (int s = 0; s < states.Count; s++)
            {
                CharacterState state = states[s];
                progress?.Invoke(state.Key, (float)s / states.Count);

                int frames = CharacterRigBaker.FramesOf(host, g, state.Anim);
                var clip = new CharacterMeshDef.PoseClip
                {
                    Anim = state.Anim,
                    State = state.Key,
                    FramesPerSecond = (float)(1000.0 /
                        Math.Max(1.0, CharacterPoseMeshExtractor.FrameMs(host, state.Anim))),
                    Settle = CharacterPoseMeshExtractor.IsSettle(host, state.Anim),
                    Frames = new Mesh[frames],
                };

                for (int f = 0; f < frames; f++)
                {
                    RigMeshData data = CharacterPoseMeshExtractor.ExtractPose(host, preset, state, f);
                    first ??= data;
                    MergeMaterials(shared, data, state, f);
                    CharacterPoseMeshExtractor.RemapMaterials(data, shared);

                    RigMeshBuild built = RigMeshBuilder.Build(data, $"CharPose_{preset}_{state.Key}_{f}");
                    clip.Frames[f] = built.Mesh;
                    meshes.Add(built.Mesh);
                    faces += built.Faces;
                    verts += built.Vertices;
                    tris += built.Triangles;
                    bytes += built.BufferBytes;
                }
                clips.Add(clip);
            }

            // ⚠️ The ramp table is a SHADER ARRAY, not a list. Sixteen slots is the resolve pass's
            // _RampMeta[16]; two of the ten presets (deckboss, packer) declare seventeen materials,
            // which is a WIDENING to argue for, not a truncation to perform quietly.
            if (shared.Count > CharacterMeshDef.RampSlots)
                throw new InvalidOperationException(
                    $"'{preset}' references {shared.Count} materials and the facet resolve pass has " +
                    $"{CharacterMeshDef.RampSlots} ramp slots: " +
                    string.Join(", ", NamesOf(shared)) + ".\nThe bake refuses rather than dropping " +
                    "the tail — a silently truncated ramp table is a recoloured character, and this " +
                    "kit has shipped one before. Widen _RampMeta, or split the preset.");

            bool azimuthCcw = MeasureFacetSign(host, preset, out string signReport);
            Debug.Log($"[char-mesh] {preset} turntable sign:\n{signReport}");

            var golden = new StringBuilder($"[char-mesh] {preset} golden: rig render vs facet oracle\n");
            foreach (CharacterState state in states)
                GoldenReport(host, preset, state, 0, azimuthCcw, golden);
            Debug.Log(golden.ToString());

            // ---- write / refresh ------------------------------------------------------------
            string path = AssetPathFor(preset);
            EnsureFolder(AssetFolder);
            var def = AssetDatabase.LoadAssetAtPath<CharacterMeshDef>(path);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<CharacterMeshDef>();

            def.Id = IdFor(preset);
            def.Preset = preset;
            def.SourceRigPath = CharacterPoseMeshExtractor.ScriptPath;
            def.SourceRigRevision = CharacterPoseMeshExtractor.Revision(host);
            def.SourceRigSha256 = CharacterPoseMeshExtractor.SourceSha256();
            def.CellW = first.W;
            def.CellH = first.H;
            def.PivotPx = new Vector2((float)first.PivotX, (float)first.PivotY);
            def.PxPerMetre = first.PxPerMetre;
            def.ElevationDeg = (float)first.DefaultElev;
            def.LightN = first.LightN.ToVector3();
            def.Gain = (float)first.Gain;
            def.Bias = (float)first.Bias;
            def.Keyline = first.Keyline;
            def.AzimuthCounterClockwise = azimuthCcw;

            // The rig hard-steps at 0.55 and ordered-dithers nothing — 0 of its 36 materials carry
            // `dith`. Forcing Bayer on this kit measured 19.74–30.21% differing pixels, so the mode
            // is DATA, taken from what the materials actually asked for.
            bool anyDither = false;
            foreach (RigMaterial m in shared) anyDither |= m.OrderedDither;
            def.StepMode = anyDither ? CharacterMeshDef.ShadeStep.OrderedDither
                                     : CharacterMeshDef.ShadeStep.HardThreshold;
            def.HardStepThreshold = 0.55f;

            def.Bayer16 = new float[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    def.Bayer16[x * 4 + y] = (float)first.Bayer[x, y];

            def.Materials = new CharacterMeshDef.Material[shared.Count];
            for (int m = 0; m < shared.Count; m++)
                def.Materials[m] = new CharacterMeshDef.Material
                {
                    Name = shared[m].Name,
                    Colors = shared[m].Ramp,
                    Offset = shared[m].Off,
                    Gain = (float)shared[m].Gain,
                    Bias = double.IsNaN(shared[m].Bias) ? float.NaN : (float)shared[m].Bias,
                    OrderedDither = shared[m].OrderedDither,
                    FixedIndex = shared[m].FixedIndex,
                };

            // Sub-assets are REPLACED, never accumulated — the RigMeshAssetBaker discipline. A
            // re-bake that only adds leaves the previous flipbook orphaned inside the file and the
            // asset grows without bound.
            if (!created)
                foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (sub is Mesh old)
                    {
                        AssetDatabase.RemoveObjectFromAsset(old);
                        UnityEngine.Object.DestroyImmediate(old, allowDestroyingAssets: true);
                    }

            def.Clips = clips.ToArray();
            // MeshStates stays EMPTY here: PR 1 bakes the art, it does not switch the game over.
            // Flipping a state to the mesh renderer retires a baked sheet, and retiring a sheet is
            // a CAPABILITY change that goes past the owner's eye — that is PR 3.
            def.MeshStates ??= Array.Empty<string>();

            if (created) AssetDatabase.CreateAsset(def, path);
            foreach (Mesh mesh in meshes) AssetDatabase.AddObjectToAsset(mesh, def);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            Debug.Log(
                $"[char-mesh] {(created ? "Created" : "Refreshed")} {path}\n" +
                $"  {states.Count} states, {meshes.Count} pose meshes, {faces:N0} faces, " +
                $"{tris:N0} tris, {verts:N0} verts\n" +
                $"  {bytes / 1024.0:N0} KB ({bytes / 1048576.0:F1} MB) of GPU buffer, " +
                $"{shared.Count} materials of {CharacterMeshDef.RampSlots} ramp slots: " +
                string.Join(", ", NamesOf(shared)) + "\n" +
                $"  rig {def.SourceRigPath} rev {def.SourceRigRevision} sha {def.SourceRigSha256[..12]}…, " +
                $"azimuth {(azimuthCcw ? "CCW (mapping negates)" : "CW")}, step {def.StepMode}\n" +
                $"  usable = {def.IsUsable()}");

            if (!def.IsUsable())
                throw new InvalidOperationException(
                    $"Baked {path} is not usable — see the fields above.");
            return def;
        }

        // -------------------------------------------------------------------------------------
        // The two guards, promoted from the spike
        // -------------------------------------------------------------------------------------

        /// <summary>
        /// Decide the facet turntable sign by rendering the rig's East view (dir 2) and asking which
        /// signed dir the reference rasteriser must pose to reproduce it. TRUE when the mapping must
        /// negate (<c>HullMeshMath.HeadingToDirUnits</c>'s <c>azimuthCounterClockwise</c>).
        ///
        /// <para><b>The statistic is the SILHOUETTE, not the inked-colour diff.</b> Handedness is a
        /// question about WHERE the figure is; the facet model's own 45–57% shading delta drowns
        /// that question out of any colour statistic. The measurement is at the comparison below.</para>
        ///
        /// <para>Public so the EditMode guard pins the SAME adjudication the bake stored, rather
        /// than a second implementation that could agree by luck.</para>
        /// </summary>
        public static bool MeasureFacetSign(IRigScriptHost host, string preset, out string report)
        {
            RigMeshData pose = CharacterPoseMeshExtractor.ExtractPose(host, preset, "idle", 0);
            byte[] truthEast = CharacterPoseMeshExtractor.RenderTruth(host, dir: 2, preset, "idle", 0);

            var negView = new RigViewOptions(-2, pose.DefaultElev);
            var posView = new RigViewOptions(+2, pose.DefaultElev);
            byte[] neg = RigMeshReferenceRasterizer.RenderFromFaces(
                pose, negView, RigTrigBasis.FromScriptHost(host, negView));
            byte[] pos = RigMeshReferenceRasterizer.RenderFromFaces(
                pose, posView, RigTrigBasis.FromScriptHost(host, posView));

            RigPixelDiff dNeg = RigMeshReferenceRasterizer.Compare(truthEast, neg, pose.W, pose.H);
            RigPixelDiff dPos = RigMeshReferenceRasterizer.Compare(truthEast, pos, pose.W, pose.H);

            // ⚠️ Adjudicate on the SILHOUETTE (opaque-vs-transparent), never on inked COLOUR.
            // The sign question is "is she facing the other way", and that is a question about
            // WHERE the figure is — not about what shade each pixel of it came out. Every loss this
            // lane measured in the facet model is a SHADING loss that cannot move an outline:
            // per-material gain flattened to one global (35.50–53.61%), ordered dither forced on
            // where rig 6 uses none (19.74–30.21%), the head raster STAMP (0.00–2.82%), the
            // per-direction gridHead nudge (2.28–15.00%). Together they swamp the colour statistic
            // — measured here, 77.95% wrong against 90.76% wrong, a 1.16x margin carrying no signal
            // at all. The same two renders read as coverage: 7 against 78, an 11.1x margin. A
            // mirrored pose moves the outline everywhere; nothing else in this pipeline can.
            int negOut = dNeg.CoverageOnlyDifferences;
            int posOut = dPos.CoverageOnlyDifferences;

            bool negWins = negOut < posOut;
            int winner = negWins ? negOut : posOut;
            int loser = negWins ? posOut : negOut;

            report =
                $"rig East (dir 2) vs oracle dir -2: {dNeg}\n" +
                $"rig East (dir 2) vs oracle dir +2: {dPos}\n" +
                $"=> adjudicated on SILHOUETTE (opaque-vs-transparent): {negOut} vs {posOut} px\n" +
                $"=> facet sign: {(negWins ? "NEGATED (azimuthCounterClockwise = true)" : "direct (false)")}";

            // The loser must be unambiguously wrong — a mirrored character differs across most of
            // the silhouette. A mushy margin means the adjudication is reading noise: stop. Both at
            // zero is that same failure wearing a different face: two silhouettes that agree
            // perfectly have not told us which way she turns.
            if (loser == 0 || loser < winner * 4)
                throw new InvalidOperationException(
                    "FACET SIGN ADJUDICATION INCONCLUSIVE — the wrong sign is not wrong enough:\n" +
                    report + "\nDo not bake until this is understood.");
            return negWins;
        }

        /// <summary>
        /// One frame of one state, rig render vs the facet oracle across all 8 cardinal dirs.
        /// Returns the worst percentage so a caller can assert on it.
        ///
        /// <para>The residual is NOT noise and is not expected to be small: it is the pipeline delta
        /// this lane measured and ADR 0044 records — the head raster STAMP the mesh does not carry
        /// (0.00–2.82%), rev 6.8's per-direction <c>gridHead</c> sub-pixel nudge a rotated flipbook
        /// cannot carry (2.28–15.00%), and, until the shader carries per-material gain, the flattened
        /// shading (35.50–53.61%). Assert against the number the ADR states, never against the hull
        /// band (2.47–4.81%) — these are different pipelines.</para>
        /// </summary>
        public static double GoldenReport(IRigScriptHost host, string preset, in CharacterState state,
                                          int frame, bool azimuthCcw, StringBuilder report)
        {
            RigMeshData pose = CharacterPoseMeshExtractor.ExtractPose(host, preset, state, frame);
            double worst = 0;
            int worstCluster = 0;

            for (int dir = 0; dir < 8; dir++)
            {
                byte[] truth = CharacterPoseMeshExtractor.RenderTruth(host, dir, preset, state, frame);
                double oracleDir = azimuthCcw ? -dir : dir;
                var view = new RigViewOptions(oracleDir, pose.DefaultElev);
                byte[] oracle = RigMeshReferenceRasterizer.RenderFromFaces(
                    pose, view, RigTrigBasis.FromScriptHost(host, view));
                RigPixelDiff diff = RigMeshReferenceRasterizer.Compare(truth, oracle, pose.W, pose.H);
                report?.AppendLine($"  {state.Key}[{frame}] dir {dir}: {diff}");
                worst = Math.Max(worst, diff.PercentDiffering);
                worstCluster = Math.Max(worstCluster, diff.LargestDifferingCluster);
            }

            report?.AppendLine($"  {state.Key}[{frame}] worst: {worst:F2}% differing, " +
                               $"cluster {worstCluster}");
            return worst;
        }

        // -------------------------------------------------------------------------------------

        /// <summary>
        /// Fold one pose's material table into the flipbook's shared one, appending names not seen
        /// before. Append-only, so an index handed out to an earlier frame stays valid.
        ///
        /// <para>It also REFUSES a name whose shading changed between poses. Nothing in the rig
        /// should do that for a fixed build — <c>makeMats</c> is a pure function of the build — so a
        /// mismatch means the bake resolved two different builds under one preset name, which is
        /// exactly the failure mode of handing <c>build</c> a bare string instead of
        /// <c>{preset: …}</c>, and it produces a fully self-consistent flipbook of the wrong
        /// person.</para>
        /// </summary>
        static void MergeMaterials(List<RigMaterial> shared, RigMeshData data,
                                   in CharacterState state, int frame)
        {
            foreach (RigMaterial m in data.Materials)
            {
                RigMaterial seen = null;
                foreach (RigMaterial s in shared)
                    if (string.Equals(s.Name, m.Name, StringComparison.Ordinal)) { seen = s; break; }

                if (seen == null) { shared.Add(m); continue; }
                if (!SameShading(seen, m))
                    throw new InvalidOperationException(
                        $"Material '{m.Name}' shades differently at {state.Key}[{frame}] than it did " +
                        "earlier in the same bake. makeMats is a pure function of the build, so this " +
                        "means two different builds were resolved under one preset name — check that " +
                        "`build` reached the rig as {preset: \"…\"} and not as a bare string.");
            }
        }

        static bool SameShading(RigMaterial a, RigMaterial b)
        {
            if (a.Off != b.Off || a.FixedIndex != b.FixedIndex ||
                a.OrderedDither != b.OrderedDither) return false;
            if (a.Gain != b.Gain) return false;
            if (double.IsNaN(a.Bias) != double.IsNaN(b.Bias)) return false;
            if (!double.IsNaN(a.Bias) && a.Bias != b.Bias) return false;
            if (a.RampHex == null || b.RampHex == null || a.RampHex.Length != b.RampHex.Length)
                return false;
            for (int i = 0; i < a.RampHex.Length; i++)
                if (!string.Equals(a.RampHex[i], b.RampHex[i], StringComparison.Ordinal)) return false;
            return true;
        }

        static string[] NamesOf(IReadOnlyList<RigMaterial> mats)
        {
            var names = new string[mats.Count];
            for (int i = 0; i < mats.Count; i++) names[i] = mats[i].Name;
            return names;
        }

        /// <summary>The used names, sorted into the rig's own MATS declaration order — a stable,
        /// diffable list for a report, independent of which pose happened to be extracted first.</summary>
        static string[] OrderNames(IRigScriptHost host, string preset, IReadOnlyList<RigMaterial> used)
        {
            string[] order = CharacterPoseMeshExtractor.MaterialOrder(host, preset);
            var rank = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < order.Length; i++) rank[order[i]] = i;

            var names = new List<string>(NamesOf(used));
            names.Sort((x, y) =>
            {
                int rx = rank.TryGetValue(x, out int a) ? a : int.MaxValue;
                int ry = rank.TryGetValue(y, out int b) ? b : int.MaxValue;
                return rx != ry ? rx.CompareTo(ry)
                                : string.CompareOrdinal(x, y);
            });
            return names.ToArray();
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
