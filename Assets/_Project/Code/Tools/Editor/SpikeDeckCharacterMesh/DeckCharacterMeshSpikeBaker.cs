using System;
using System.Collections.Generic;
using System.Text;
using HiddenHarbours.Tools.RigBaking;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.SpikeDeckCharacterMesh.Editor
{
    /// <summary>
    /// ⚠️ SPIKE (deck-character-mesh, draft ADR 0024). Bakes the Fisher's on-deck pose-mesh
    /// flipbook (idle + hold) into ONE committed <see cref="DeckCharacterMeshSpikeDef"/> asset,
    /// through the ADR 0022 tail end unchanged: pose faces → <see cref="RigMeshBuilder"/> meshes,
    /// shading facts straight off the rig, refresh-in-place (same guid).
    ///
    /// <para><b>Two measurements happen at bake time, both logged:</b></para>
    /// <list type="number">
    ///   <item><b>The turntable sign, adjudicated by PIXELS.</b> The label probe
    ///   (<see cref="CharacterRigAzimuthProbe"/>) answers which way the rig's LABELS run — the
    ///   sprite-sheet fact. The facet pipeline needs a different fact: which sign of dir-units
    ///   reproduces the rig's East view through the shared projection. The character rig's
    ///   turntable is <c>th = −dir·π/4</c> (the ADR-0006 label fix) where the boats' is
    ///   <c>+dir·π/4</c> — so label-probe conventions and facet signs DISAGREE across the two
    ///   lineages, and declaring either would ship a fifth mirrored artwork. The bake renders the
    ///   rig's East view and compares it against the reference rasteriser at dir −2 AND +2; the
    ///   sign that matches wins, the other must be catastrophically wrong (the sabotage margin),
    ///   and the def stores the winner as <see cref="DeckCharacterMeshSpikeDef.AzimuthCounterClockwise"/>
    ///   (true ⇒ <c>HullMeshMath.HeadingToDirUnits</c> negates).</item>
    ///   <item><b>The golden fidelity number</b> — rig's own render vs the reference rasteriser
    ///   (the facet pipeline's CPU oracle) across all 8 cardinal dirs for one frame of each clip.
    ///   This is spike question 1, measured: the residual quantifies exactly the known deltas (the
    ///   resolve pass's 0.30 m depth-edge threshold vs the character rig's tighter 0.13, and
    ///   single-step dither noise).</item>
    /// </list>
    /// </summary>
    public static class DeckCharacterMeshSpikeBaker
    {
        public const string MenuRoot = "Hidden Harbours/Spike/Deck Character Mesh (SPIKE)";
        public const string AssetPath = "Assets/_Project/Data/Spike/DeckCharacterMeshSpike.asset";

        const string Build = "fisher";
        static readonly string[] Anims = { "idle", "hold" };

        [MenuItem(MenuRoot + "/Bake Fisher pose meshes (idle + hold)", priority = 10)]
        public static void Bake()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.LoadWidened(host);

            // ---- shared facts + the flipbook ---------------------------------------------
            var clips = new List<DeckCharacterMeshSpikeDef.PoseClip>();
            var meshes = new List<Mesh>();
            RigMeshData first = null;
            long totalTris = 0, totalBytes = 0;

            foreach (string anim in Anims)
            {
                int frames = CharacterPoseMeshExtractor.FrameCount(host, anim);
                double ms = CharacterPoseMeshExtractor.FrameMs(host, anim);
                var clip = new DeckCharacterMeshSpikeDef.PoseClip
                {
                    Anim = anim,
                    FramesPerSecond = (float)(1000.0 / Math.Max(1.0, ms)),
                    Frames = new Mesh[frames],
                };
                for (int f = 0; f < frames; f++)
                {
                    RigMeshData data = CharacterPoseMeshExtractor.ExtractPose(host, Build, anim, f);
                    first ??= data;
                    RigMeshBuild built = RigMeshBuilder.Build(data, $"CharPose_{anim}_{f}");
                    clip.Frames[f] = built.Mesh;
                    meshes.Add(built.Mesh);
                    totalTris += built.Triangles;
                    totalBytes += built.BufferBytes;
                }
                clips.Add(clip);
            }

            // ---- the turntable sign, adjudicated by pixels -------------------------------
            bool azimuthCcw = MeasureFacetSign(host, out string signReport);
            Debug.Log($"[deck-char SPIKE] Turntable sign:\n{signReport}");

            // The label probe runs too — cross-lineage documentation, not the stored fact.
            var geo = new RigGeometry(
                width: first.W, height: first.H, pivotX: first.PivotX, pivotY: first.PivotY,
                nativeDirs: (int)8, rockFrames: 0, defaultElevation: first.DefaultElev);
            try
            {
                var label = CharacterRigAzimuthProbe.Measure(host, CharacterPoseMeshExtractor.GlobalName, geo);
                Debug.Log($"[deck-char SPIKE] Label probe (sheet fact, NOT the facet sign):\n{label.Report}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[deck-char SPIKE] Label probe inconclusive (non-fatal for the mesh " +
                                 $"path — the facet sign above is pixel-adjudicated): {e.Message}");
            }

            // ---- the golden fidelity numbers (spike question 1) --------------------------
            var report = new StringBuilder("[deck-char SPIKE] Golden: rig render vs facet oracle\n");
            foreach (string anim in Anims)
                GoldenReport(host, Build, anim, 0, azimuthCcw, report);
            Debug.Log(report.ToString());

            // ---- write / refresh the asset (same guid, meshes replaced) ------------------
            EnsureFolder("Assets/_Project/Data/Spike");
            var def = AssetDatabase.LoadAssetAtPath<DeckCharacterMeshSpikeDef>(AssetPath);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<DeckCharacterMeshSpikeDef>();

            def.Id = "spike.deck_character_mesh";
            def.SourceRigPath = CharacterPoseMeshExtractor.ScriptPath;
            def.Build = Build;
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

            def.Bayer16 = new float[16];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    def.Bayer16[x * 4 + y] = (float)first.Bayer[x, y];

            def.Ramps = new DeckCharacterMeshSpikeDef.Ramp[first.Materials.Count];
            for (int m = 0; m < first.Materials.Count; m++)
                def.Ramps[m] = new DeckCharacterMeshSpikeDef.Ramp
                {
                    Colors = first.Materials[m].Ramp,
                    Offset = first.Materials[m].Off,
                };

            // Replace sub-assets, never accumulate (the RigMeshAssetBaker discipline).
            if (!created)
            {
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(AssetPath))
                    if (sub is Mesh old)
                    {
                        AssetDatabase.RemoveObjectFromAsset(old);
                        UnityEngine.Object.DestroyImmediate(old, allowDestroyingAssets: true);
                    }
            }
            def.Clips = clips.ToArray();
            if (created) AssetDatabase.CreateAsset(def, AssetPath);
            foreach (var mesh in meshes)
                AssetDatabase.AddObjectToAsset(mesh, def);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath);

            Debug.Log($"[deck-char SPIKE] {(created ? "Created" : "Refreshed")} {AssetPath}: " +
                      $"{meshes.Count} pose meshes, {totalTris} tris total, " +
                      $"{totalBytes / 1024.0:F1} KB, azimuth {(azimuthCcw ? "CCW (mapping negates)" : "CW")}, " +
                      $"usable = {def.IsUsable()}.");
            if (!def.IsUsable())
                throw new InvalidOperationException("Baked spike def is not usable — see fields.");
        }

        /// <summary>Headless entry (-executeMethod).</summary>
        public static void BakeCli()
        {
            try
            {
                Bake();
                Debug.Log("[deck-char SPIKE] CLI bake OK.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[deck-char SPIKE] CLI bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Decide the facet turntable sign by rendering the rig's East view (dir 2) and asking
        /// which signed dir the reference rasteriser must pose to reproduce it. Returns TRUE when
        /// the mapping must negate (<c>HullMeshMath.HeadingToDirUnits</c>'s azimuthCounterClockwise).
        /// Public-ish shape so the spike EditMode test pins the same adjudication.
        /// </summary>
        public static bool MeasureFacetSign(IRigScriptHost host, out string report)
        {
            RigMeshData pose = CharacterPoseMeshExtractor.ExtractPose(host, Build, "idle", 0);
            byte[] truthEast = CharacterPoseMeshExtractor.RenderTruth(host, dir: 2, "idle", 0);

            var negView = new RigViewOptions(-2, pose.DefaultElev);
            var posView = new RigViewOptions(+2, pose.DefaultElev);
            byte[] neg = RigMeshReferenceRasterizer.RenderFromFaces(pose, negView,
                             RigTrigBasis.FromScriptHost(host, negView));
            byte[] pos = RigMeshReferenceRasterizer.RenderFromFaces(pose, posView,
                             RigTrigBasis.FromScriptHost(host, posView));

            RigPixelDiff dNeg = RigMeshReferenceRasterizer.Compare(truthEast, neg, pose.W, pose.H);
            RigPixelDiff dPos = RigMeshReferenceRasterizer.Compare(truthEast, pos, pose.W, pose.H);

            bool negWins = dNeg.DifferingPixels < dPos.DifferingPixels;
            RigPixelDiff winner = negWins ? dNeg : dPos;
            RigPixelDiff loser = negWins ? dPos : dNeg;

            report =
                $"rig East (dir 2) vs oracle dir −2: {dNeg}\n" +
                $"rig East (dir 2) vs oracle dir +2: {dPos}\n" +
                $"=> facet sign: {(negWins ? "NEGATED (azimuthCounterClockwise = true)" : "direct (false)")}";

            // The loser must be unambiguously wrong — a mirrored character differs across most of
            // the silhouette. A mushy margin means the adjudication is reading noise: stop.
            if (loser.DifferingPixels < winner.DifferingPixels * 4)
                throw new InvalidOperationException(
                    "FACET SIGN ADJUDICATION INCONCLUSIVE — the wrong sign is not wrong enough:\n" +
                    report + "\nDo not bake until this is understood.");
            return negWins;
        }

        static void GoldenReport(IRigScriptHost host, string build, string anim, int frame,
                                 bool azimuthCcw, StringBuilder report)
        {
            RigMeshData pose = CharacterPoseMeshExtractor.ExtractPose(host, build, anim, frame);
            double worst = 0;
            int worstCluster = 0;
            for (int dir = 0; dir < 8; dir++)
            {
                byte[] truth = CharacterPoseMeshExtractor.RenderTruth(host, dir, anim, frame);
                double oracleDir = azimuthCcw ? -dir : dir;
                var view = new RigViewOptions(oracleDir, pose.DefaultElev);
                byte[] oracle = RigMeshReferenceRasterizer.RenderFromFaces(
                    pose, view, RigTrigBasis.FromScriptHost(host, view));
                RigPixelDiff diff = RigMeshReferenceRasterizer.Compare(truth, oracle, pose.W, pose.H);
                report.AppendLine($"  {anim}[{frame}] dir {dir}: {diff}");
                worst = Math.Max(worst, diff.PercentDiffering);
                worstCluster = Math.Max(worstCluster, diff.LargestDifferingCluster);
            }
            report.AppendLine($"  {anim}[{frame}] worst: {worst:F2}% differing, cluster {worstCluster} " +
                              "(expected residual: the resolve's 0.30 m depth-edge floor vs the rig's " +
                              "0.13, plus single-step dither noise)");
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
