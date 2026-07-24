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
    /// <para><b>Three assertions, because the oracle is definite about three different things and
    /// undefined about a fourth.</b> The rig makes the zero-thickness blade double-sided by pushing
    /// every face twice, <c>q</c> then its exact reverse (<c>doryIsoRig.js:241</c>) — bit-identical
    /// vertices, identical <c>db</c>, opposite normals. Which twin you SEE is decided by the last bit
    /// of a barycentric sum and by nothing else; measured over 8 headings × 8 stroke phases, the rig's
    /// own choice agrees with six different tie-break rules at 44.6%–51.2%, i.e. with all of them at
    /// chance. So:</para>
    /// <list type="number">
    ///   <item><b>Coverage is definite</b> — the twins cover identically, so the silhouette is a fact.
    ///   Required: <b>zero</b> opaque-vs-transparent differences.</item>
    ///   <item><b>Colour outside the twins is definite.</b> Required: largest connected differing
    ///   cluster ≤ <see cref="MaxNoiseCluster"/>.</item>
    ///   <item><b>Colour inside the twins is a coin flip</b>, so it is compared against BOTH
    ///   candidates instead of one: the rig must have painted a colour one of the two twins computed
    ///   at that pixel. Required: <b>zero</b> exceptions
    ///   (<see cref="RigAmbiguousPixels.Result.ReferenceMatchedNoCandidate"/>).</item>
    /// </list>
    /// <para>See <see cref="RigAmbiguousPixels"/> for why that exclusion is a measurement rather than
    /// a tolerance, and <see cref="AFeatherErrorOfHalfADegree_IsCaught"/> for what it costs in
    /// sensitivity: nothing. The fixture resolves a HALF-DEGREE feather error, against the previous
    /// whole-cell cluster criterion which could not resolve a 4° one.</para>
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
        /// The single-pixel noise floor for colour differences OUTSIDE the double-sided twins. Same
        /// reasoning as every other fixture in this folder: the residual between V8's doubles and
        /// .NET's — plus the float32 the vertex buffer imposes — is isolated pixels at a facet or
        /// dither boundary, and a REAL defect is a connected patch, because it is geometry that moved.
        ///
        /// <para>MEASURED worst over both oars × 8 headings × 8 phases: <b>3</b>. Six is double that,
        /// and is a guard rather than the load-bearing assertion — the two zero-tolerance checks above
        /// it are what actually discriminate (they catch a half-degree feather error; this metric does
        /// not move until about four degrees).</para>
        /// </summary>
        const int MaxNoiseCluster = 6;

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
        static Mesh PoseFaithfully(HullPropMeshDef def, Quaternion r)
        {
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
                // switch is how the fixture tells that residual apart from a real mispose, and the
                // answer for the oar is BYTE-IDENTICAL: it is not the source of anything.
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

        /// <summary>What one sweep of 8 headings × 8 stroke phases found.</summary>
        readonly struct Verdict
        {
            public readonly int WorstCluster, Silhouette, ReferenceMatchedNoCandidate, Ambiguous, Inked;
            public readonly double WorstTwinDepthGap;
            public readonly string Where;

            public Verdict(int worstCluster, int silhouette, int noCandidate, int ambiguous,
                           int inked, double worstTwinDepthGap, string where)
            {
                WorstCluster = worstCluster; Silhouette = silhouette;
                ReferenceMatchedNoCandidate = noCandidate; Ambiguous = ambiguous; Inked = inked;
                WorstTwinDepthGap = worstTwinDepthGap; Where = where;
            }

            /// <summary>The one number a sabotage has to move. Coverage and unresolvable colour are
            /// both zero for a correct pose, so their SUM is the detection signal.</summary>
            public int Detections => Silhouette + ReferenceMatchedNoCandidate;

            public override string ToString() =>
                $"{Silhouette} silhouette + {ReferenceMatchedNoCandidate} unresolvable-colour " +
                $"differences, worst masked cluster {WorstCluster} at {Where}; " +
                $"{Ambiguous}/{Inked} px were double-sided twins (worst twin depth gap " +
                $"{WorstTwinDepthGap:E2})";
        }

        /// <summary>Sweeps every heading and stroke phase, posing the committed mesh with
        /// <paramref name="rotation"/> and adjudicating against the rig's own render.</summary>
        static Verdict Sweep(IRigScriptHost host, RigMeshData data, HullPropMeshDef def, int side,
                             Func<DoryOarMeshPose.Pose, Quaternion> rotation)
        {
            int[] partners = RigMeshExtractor.FindReverseDuplicatePartners(data);
            string sideOpt = side < 0 ? "port" : "star";

            int worst = 0, sil = 0, noCand = 0, ambiguous = 0, inked = 0;
            double gap = 0;
            string where = "nowhere";

            for (int dir = 0; dir < 8; dir++)
            foreach (float t in Phases)
            {
                var view = new RigViewOptions(dir, data.DefaultElev);
                byte[] truth = host.EvaluateBytes(
                    $"{RigGlobal}.renderOars({dir},{{side:'{sideOpt}',state:'row'," +
                    $"t:{t.ToString("R")},elev:{data.DefaultElev.ToString("R")}}})");

                Mesh posed = PoseFaithfully(def, rotation(DoryOarMeshPose.Row(t)));
                var trace = new RigPaintTrace { ProbeAll = true };
                byte[] mine = RigMeshReferenceRasterizer.RenderFromMesh(data, posed, view, null, trace);
                Object.DestroyImmediate(posed);

                var amb = RigAmbiguousPixels.From(trace, truth, data.W, data.H, partners);
                ambiguous += amb.Count;
                noCand += amb.ReferenceMatchedNoCandidate;
                gap = Math.Max(gap, amb.WorstTwinDepthGap);

                RigPixelDiff d = RigMeshReferenceRasterizer.Compare(
                    truth, mine, data.W, data.H, amb.Mask);
                inked += d.InkedPixels;
                sil += d.CoverageOnlyDifferences;
                if (d.LargestDifferingCluster > worst)
                {
                    worst = d.LargestDifferingCluster;
                    where = $"dir {dir}, t {t:F3} ({d})";
                }
            }

            return new Verdict(worst, sil, noCand, ambiguous, inked, gap, where);
        }

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
                Verdict v = Sweep(host, data, def, side, p => DoryOarMeshPose.Rotation(side, p));

                Assert.AreEqual(0, v.Silhouette,
                    $"The committed {name} oar covers different PIXELS from the rig's own renderOars: " +
                    $"{v}. Coverage is not ambiguous — the double-sided twins have bit-identical " +
                    "vertices — so this means the blade is in the wrong PLACE. Check the pose " +
                    "transcription, the baked canonical pose, and the rotate-about-the-pivot " +
                    "arithmetic in that order.");

                Assert.AreEqual(0, v.ReferenceMatchedNoCandidate,
                    $"The committed {name} oar shades differently from the rig's own renderOars " +
                    $"inside the double-sided blade: {v}. At those pixels the rig's choice between " +
                    "the two coplanar twins is a coin flip and is not compared — but the rig still " +
                    "has to have painted one of the two colours WE computed there, and it did not. " +
                    "That means the blade's FEATHER is wrong (its roll about its own axis), which is " +
                    "the failure a naive shortest-arc pose produces and the one that is hardest to " +
                    "see by eye.");

                Assert.LessOrEqual(v.WorstCluster, MaxNoiseCluster,
                    $"The committed {name} oar does not reproduce the rig's own renderOars outside " +
                    $"the double-sided blade: {v}, floor {MaxNoiseCluster}. A connected patch means " +
                    "geometry moved, not that arithmetic disagreed in the last bit.");
            }
        }

        /// <summary>
        /// <b>The sabotage, and the resolution it proves.</b> Pose the oar with an extra HALF-DEGREE
        /// twist about its own axis — pure feather error, the loom lands in exactly the right place
        /// and only the blade's roll is wrong, so this is the failure most likely to be waved through
        /// by eye and the one the acceptance exists to catch.
        ///
        /// <para>Half a degree is not a round number picked for comfort; it is where the measured
        /// sensitivity curve is already unambiguous. Over both oars × 8 headings × 8 phases, extra
        /// twist against (silhouette + unresolvable-colour) detections: <b>0° → 0</b>, 0.25° → 38,
        /// <b>0.5° → 76</b>, 1° → 138, 2° → 310, 4° → 814, naive shortest-arc → 334. The correct pose
        /// is the only one that scores zero, and it scores zero on both counts independently.</para>
        ///
        /// <para>⚠️ The metric this REPLACED could not do this. The old whole-cell "largest connected
        /// cluster ≤ 12" scored 11 for the naive shortest-arc sabotage — inside its own floor — so it
        /// was passing a fixture that could not tell a correctly feathered blade from a wrong one.
        /// The cluster metric still runs, as a guard; it does not move until roughly 4°.</para>
        /// </summary>
        [Test]
        public void AFeatherErrorOfHalfADegree_IsCaught_ProvingTheFixtureResolvesTheBladesRoll()
        {
            const float TwistDegrees = 0.5f;
            var def = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(StarAsset);
            Assert.IsNotNull(def);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = Extract(host, +1);

            Verdict clean = Sweep(host, data, def, +1, p => DoryOarMeshPose.Rotation(+1, p));
            Verdict twisted = Sweep(host, data, def, +1, p =>
                Quaternion.AngleAxis(TwistDegrees,
                                     DoryOarMeshPose.Direction(+1, p.SweepDegrees, p.DipDegrees))
                * DoryOarMeshPose.Rotation(+1, p));

            Assert.AreEqual(0, clean.Detections, $"The correct pose is not clean: {clean}.");
            Assert.Greater(twisted.Detections, 0,
                $"A {TwistDegrees}° feather error was NOT caught — which would mean this fixture " +
                "cannot resolve the blade's roll about its own axis, and the acceptance above proves " +
                $"nothing. Measured {twisted}.");
        }

        /// <summary>
        /// The sabotage the frame construction exists to survive: a naive shortest-arc rotation from
        /// the baked direction to the target one. The rig builds the blade's cross-section frame from
        /// a FIXED world up-vector, so the blade FEATHERS with direction rather than carrying its roll
        /// rigidly — the loom lands right and the blade sits at the wrong angle.
        /// </summary>
        [Test]
        public void NaiveShortestArcPosing_IsCaught_ProvingTheFrameConstructionIsLoadBearing()
        {
            var def = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(StarAsset);
            Assert.IsNotNull(def);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = Extract(host, +1);

            Verdict naive = Sweep(host, data, def, +1, p => Quaternion.FromToRotation(
                DoryOarMeshPose.Direction(+1, 0f, 0f),
                DoryOarMeshPose.Direction(+1, p.SweepDegrees, p.DipDegrees)));

            Assert.Greater(naive.Detections, 0,
                "A naive shortest-arc pose was NOT caught — which would mean this fixture cannot tell " +
                "a correctly feathered blade from an incorrectly feathered one, and the acceptance " +
                $"above proves nothing. Measured {naive}.");
        }
    }
}
