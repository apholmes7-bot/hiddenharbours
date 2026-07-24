using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>ADR 0022 phase 7 acceptance: the dory's oars.</b> The fitting that gates the whole
    /// small-boat fleet — she cannot go mesh without oars, and four PlayMode tests say so.
    ///
    /// <para><b>The oracle is the rig's own <c>renderOars</c>, at matched poses.</b> That is a
    /// stronger oracle than the hulls got in phase 6: a hull was compared against a CPU transcription
    /// of the rig's pipeline, but an oar can be compared against the rig ACTUALLY RENDERING THE SAME
    /// POSE. If the transcribed kinematics, the baked mesh, or the rotate-about-the-pivot arithmetic
    /// is wrong by so much as a degree, the blade lands somewhere else and the pixels say so.</para>
    ///
    /// <para><b>Everything here is CPU — so CI adjudicates it.</b> V8 for the truth, the phase-2
    /// reference rasteriser for the mesh; no graphics device is touched. The rendering fixtures that
    /// CI must skip are the GPU ones, and deliberately none of this is.</para>
    /// </summary>
    public class DoryOarPropMeshAcceptanceTests
    {
        const string RigPath = "docs/art/rigs/doryIsoRig.js";
        const string RigGlobal = "DoryIso";
        const string PortAsset = "Assets/_Project/Data/Boats/HullProps/DoryOarPortPropMesh.asset";
        const string StarAsset = "Assets/_Project/Data/Boats/HullProps/DoryOarStarPropMesh.asset";

        /// <summary>
        /// The stroke phases sampled. Deliberately includes the quarter points, where the blade is
        /// at its extremes of sweep and dip — the poses a feather error is largest at, and the ones a
        /// naive shortest-arc rotation gets most wrong.
        /// </summary>
        static readonly float[] Phases = { 0f, 0.125f, 0.25f, 0.375f, 0.5f, 0.625f, 0.75f, 0.875f };

        /// <summary>
        /// The single-pixel noise floor. Same reasoning as every other fixture in this folder: the
        /// residual between V8's doubles and .NET's is isolated pixels at a facet or dither boundary,
        /// and a REAL defect is a connected patch, because it is geometry that moved. An oar is a
        /// thin object — its whole silhouette is a few hundred pixels — so the floor is far tighter
        /// than a hull's 300–650, and a mispose would blow through it by orders of magnitude.
        /// </summary>
        const int MaxNoiseCluster = 12;

        static RigMeshData Extract(IRigScriptHost host, int side) =>
            RigMeshExtractor.ExtractFrom(host, RigPath, RigGlobal, new RigPropExtraction
            {
                FaceBuilderCall = $"buildOar({side},{{sweep:0,dip:0}})",
                PivotCall = $"oarlockPt({side})",
                ExtraSymbols = new[] { "buildOar", "oarlockPt" },
            });

        /// <summary>
        /// The committed mesh, posed exactly the way the runtime poses it: rotate about the
        /// fitting's pivot. Returns a throwaway mesh the caller destroys.
        ///
        /// ⚠️ Normals must be the RIG's flat per-face normals, not Unity's smoothed recalculation —
        /// the facet shader takes one normal per face and that IS the shading. Re-reading them off
        /// the baked mesh keeps the posed copy faithful.
        /// </summary>
        static Mesh PoseFaithfully(HullPropMeshDef def, int side, in DoryOarMeshPose.Pose pose)
        {
            Quaternion r = DoryOarMeshPose.Rotation(side, pose);
            Vector3 p = def.PivotLocalMeters;

            var src = def.Mesh.vertices;
            var srcN = def.Mesh.normals;
            var uv = new List<Vector4>();
            def.Mesh.GetUVs(0, uv);

            var dst = new Vector3[src.Length];
            var dstN = new Vector3[srcN.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = p + r * (src[i] - p);
            for (int i = 0; i < srcN.Length; i++) dstN[i] = r * srcN[i];

            int[] tris = def.Mesh.triangles;
            if (RederiveNormals)
            {
                // The rig takes each face's normal from its ROTATED vertices; rotating the baked
                // object-space normal instead is (R·u)×(R·v) against R·(u×v) — equal only in exact
                // arithmetic. Phase 6 measured that difference as isolated pixels on a hull. This
                // switch is how the fixture tells that residual apart from a real mispose.
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 n = Vector3.Normalize(Vector3.Cross(dst[tris[t + 1]] - dst[tris[t]],
                                                                dst[tris[t + 2]] - dst[tris[t]]));
                    dstN[tris[t]] = dstN[tris[t + 1]] = dstN[tris[t + 2]] = n;
                }
            }

            var m = new Mesh { name = "PosedOar", hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(dst);
            m.SetNormals(dstN);
            m.SetUVs(0, uv);
            m.SetTriangles(tris, 0);
            return m;
        }

        /// <summary>Diagnostic switch — see <see cref="PoseFaithfully"/>.</summary>
        static bool RederiveNormals;

        [Test]
        public void TheTranscribedStroke_MatchesTheRigsOwnOarPose_AcrossTheWholeCycle()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            // oarPose is closure-private like every other rig internal, so it is reached the same
            // way the baker reaches buildOar: an IN-MEMORY widening. The file on disk is not touched.
            string source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(RigCatalog.RepoRoot, RigPath));
            host.Execute(RigMeshExtractor.WidenExportedLiteral(
                source, RigGlobal, new[] { "oarPose" }, RigPath));

            // 64 samples, not 8: the C# is a CONTINUOUS function and the point of the mesh path is
            // that it is read between the sprite path's frames, so the places it could disagree are
            // exactly the places the sprite path never sampled.
            const int Samples = 64;
            double worstSweep = 0, worstDip = 0;
            for (int i = 0; i < Samples; i++)
            {
                float t = i / (float)Samples;
                double rigSweep = host.EvaluateNumber($"{RigGlobal}.oarPose('row',{t.ToString("R")}).sweep");
                double rigDip = host.EvaluateNumber($"{RigGlobal}.oarPose('row',{t.ToString("R")}).dip");
                var mine = DoryOarMeshPose.Row(t);
                worstSweep = Math.Max(worstSweep, Math.Abs(rigSweep - mine.SweepDegrees));
                worstDip = Math.Max(worstDip, Math.Abs(rigDip - mine.DipDegrees));
            }

            Assert.Less(worstSweep, 1e-4,
                $"The transcribed rowing stroke has drifted from the rig's own oarPose (worst sweep " +
                $"error {worstSweep:F6}°). DoryOarMeshPose is a TRANSCRIPTION and this test is the " +
                "only thing standing between it and a silently different stroke.");
            Assert.Less(worstDip, 1e-4, $"Dip drifted by {worstDip:F6}°.");
        }

        [Test]
        public void TheCommittedOars_ReproduceTheRigsOwnRender_AcrossHeadingsAndTheStroke(
            [Values(false, true)] bool rederiveNormals)
        {
            RederiveNormals = rederiveNormals;
            foreach (var (side, assetPath, name) in new[]
                     { (-1, PortAsset, "port"), (+1, StarAsset, "starboard") })
            {
                var def = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(assetPath);
                Assert.IsNotNull(def, $"{assetPath} did not load — the {name} oar is not committed.");
                Assert.IsTrue(def.IsUsable(), $"{assetPath} is not usable.");

                using IRigScriptHost host = RigScriptHostFactory.Create();
                RigMeshData data = Extract(host, side);

                string sideOpt = side < 0 ? "port" : "star";
                int worst = 0;
                string worstWhere = "";

                for (int dir = 0; dir < 8; dir++)
                {
                    foreach (float t in Phases)
                    {
                        var view = new RigViewOptions(dir, data.DefaultElev);
                        byte[] truth = host.EvaluateBytes(
                            $"{RigGlobal}.renderOars({dir},{{side:'{sideOpt}',state:'row'," +
                            $"t:{t.ToString("R")},elev:{data.DefaultElev.ToString("R")}}})");

                        Mesh posed = PoseFaithfully(def, side, DoryOarMeshPose.Row(t));
                        byte[] mine = RigMeshReferenceRasterizer.RenderFromMesh(data, posed, view);
                        Object.DestroyImmediate(posed);

                        RigPixelDiff d = RigMeshReferenceRasterizer.Compare(truth, mine, data.W, data.H);
                        if (d.LargestDifferingCluster > worst)
                        {
                            worst = d.LargestDifferingCluster;
                            worstWhere = $"dir {dir}, t {t:F3} ({d})";
                        }
                    }
                }

                Assert.LessOrEqual(worst, MaxNoiseCluster,
                    $"The committed {name} oar does not reproduce the rig's own renderOars: largest " +
                    $"connected differing cluster {worst} at {worstWhere}, floor {MaxNoiseCluster}. " +
                    "A connected patch means the blade is in the WRONG PLACE, not that arithmetic " +
                    "disagreed in the last bit — check the pose transcription, the baked canonical " +
                    "pose, and the rotate-about-the-pivot arithmetic in that order.");
            }
        }

        /// <summary>
        /// The sabotage: pose the oar with the frame construction REMOVED — a naive shortest-arc
        /// rotation from the baked direction to the target one. The loom lands in the right place and
        /// only the blade's feather is wrong, so this is the failure most likely to be waved through
        /// by eye, and the one the acceptance test exists to catch.
        /// </summary>
        [Test]
        public void NaiveShortestArcPosing_IsCaught_ProvingTheFrameConstructionIsLoadBearing()
        {
            var def = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(StarAsset);
            Assert.IsNotNull(def);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = Extract(host, +1);

            // The pose with the most feather error: a quarter through the stroke, blade fully dipped.
            const float T = 0.25f;
            var pose = DoryOarMeshPose.Row(T);
            var view = new RigViewOptions(2, data.DefaultElev);

            byte[] truth = host.EvaluateBytes(
                $"{RigGlobal}.renderOars(2,{{side:'star',state:'row',t:{T.ToString("R")}," +
                $"elev:{data.DefaultElev.ToString("R")}}})");

            Quaternion naive = Quaternion.FromToRotation(
                DoryOarMeshPose.Direction(+1, 0f, 0f),
                DoryOarMeshPose.Direction(+1, pose.SweepDegrees, pose.DipDegrees));

            Vector3 p = def.PivotLocalMeters;
            var src = def.Mesh.vertices;
            var srcN = def.Mesh.normals;
            var uv = new List<Vector4>(); def.Mesh.GetUVs(0, uv);
            var dst = new Vector3[src.Length];
            var dstN = new Vector3[srcN.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = p + naive * (src[i] - p);
            for (int i = 0; i < srcN.Length; i++) dstN[i] = naive * srcN[i];

            var sabotaged = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            sabotaged.SetVertices(dst); sabotaged.SetNormals(dstN);
            sabotaged.SetUVs(0, uv); sabotaged.SetTriangles(def.Mesh.triangles, 0);

            byte[] wrong = RigMeshReferenceRasterizer.RenderFromMesh(data, sabotaged, view);
            Object.DestroyImmediate(sabotaged);

            RigPixelDiff d = RigMeshReferenceRasterizer.Compare(truth, wrong, data.W, data.H);
            Assert.Greater(d.LargestDifferingCluster, MaxNoiseCluster,
                "A naive shortest-arc pose was NOT caught — which would mean this fixture cannot tell " +
                "a correctly feathered blade from an incorrectly feathered one, and the acceptance " +
                $"above proves nothing. Measured {d}.");
        }
    }
}
