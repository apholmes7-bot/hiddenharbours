using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
    /// <b>ADR 0022 phase 7 acceptance: the outboards.</b> The last sprite art bolted to a hull, and
    /// the fittings that hold four boats (five visuals) on the sprite compass.
    ///
    /// <para><b>The oracle is each rig's own <c>renderMotor</c>, at matched poses</b> — the same
    /// stronger-than-phase-6 oracle the dory's oars got. The mesh is posed exactly the way the runtime
    /// poses it (rotate about the clamp swivel, then offset by the lateral mount) and rasterised
    /// through the phase-2 reference rasteriser; if the transcribed kinematics, the baked canonical
    /// pose, or the rotate-about-the-pivot arithmetic is wrong, the engine lands somewhere else and
    /// the pixels say so.</para>
    ///
    /// <para><b>Two rigs, four builds, five boats.</b> The punt owns her tiller engine outright
    /// (<c>puntIsoRig.renderMotor</c>, <c>basic</c>/<c>upgraded</c>, ±32°, cell 212×168); the skiffs
    /// share <c>skiffMotorRig</c>'s remote-steer four-stroke (<c>work</c>/<c>sport</c>, ±30°, cell
    /// 272×216) — one fitting, three visuals, which is exactly why fittings are their own assets.
    /// Both builds of both rigs are swept here: the punt's upgrade is a real gameplay distinction, so
    /// the variant is DATA and each build is baked and adjudicated on its own.</para>
    ///
    /// <para><b>The steer is read CONTINUOUSLY, and the sweep proves it.</b> Every angle below comes
    /// from <see cref="OutboardMotorMath.SteerDegreesForPosition"/> at deliberately FRACTIONAL
    /// positions — places between the nine baked sprite columns, which the sprite path could not draw
    /// and therefore never checked. Hard-over (position 0 and 8) is included at both ends.</para>
    ///
    /// <para><b>Everything here is CPU — so CI adjudicates it.</b> V8 for the truth, the phase-2
    /// reference rasteriser for the mesh; no graphics device is touched.</para>
    /// </summary>
    public class OutboardPropMeshAcceptanceTests
    {
        /// <summary>
        /// The nine-column sheet the sprite path drew from — kept as the coordinate the steer
        /// accumulator lives in, so the mesh and the sprite agree about where hard-over is.
        /// </summary>
        const int SteerColumns = OutboardMotorMath.SteerColumns;

        /// <summary>
        /// Steer positions swept, in accumulator units. <b>Five of the seven are fractional</b> —
        /// poses no baked column exists for, which is the entire point of the mesh path — and the two
        /// integers are the extremes, so hard-over to port and to starboard are both covered.
        /// </summary>
        static readonly float[] SteerPositions = { 0f, 1.37f, 2.5f, 4f, 5.62f, 7.15f, 8f };

        /// <summary>
        /// The (steer position, tilt) pairs swept at every heading. The FULL steer sweep runs at the
        /// running trim, which is the pose the game actually drives; tilt is then sampled at three
        /// steers, including both hard-overs.
        ///
        /// <para><b>Tilt is in here at all because it is the axis that catches a COMPOSITION error:</b>
        /// <c>Rz(steer)·Rx(−tilt)</c> and <c>Rx(−tilt)·Rz(steer)</c> are the same rotation whenever
        /// either angle is zero, and differ everywhere else. A sweep that only ever ran at trim 0
        /// could not tell them apart — see
        /// <see cref="SwappingTheSteerAndTiltComposition_IsCaught_AtAPoseWhereTheyDiffer"/>.</para>
        ///
        /// <para>A cross product of every steer with every tilt would say nothing more and would put
        /// minutes on the CI suite (the cells here are 212×168 and 272×216, and the oracle is a
        /// software rasteriser in V8).</para>
        /// </summary>
        static IEnumerable<(float position, float tilt)> Poses()
        {
            foreach (float p in SteerPositions) yield return (p, 0f);
            foreach (float t in new[] { 17f, 40f })
                foreach (float p in new[] { 0f, 2.5f, 8f }) yield return (p, t);
        }

        /// <summary>
        /// Single-pixel noise floor for colour differences, over the whole sweep. Same reasoning as
        /// every fixture in this folder: the residual between V8's doubles and .NET's — plus the
        /// float32 the vertex buffer imposes — is isolated pixels at a facet or dither boundary, while
        /// a real defect is a connected patch because it is geometry that moved.
        ///
        /// <para>MEASURED clean, worst over all four fittings × 8 headings × 13 poses (624 renders,
        /// plus the twin's second clamp position): <b>1</b> — a single pixel, once, on the twin's port
        /// engine. Three of the five cases come back at <b>0/0/0</b>: not "close", identical. Four is
        /// a guard, not the load-bearing assertion; the two zero-tolerance checks above it are, and
        /// the sensitivity curve below shows this one moving to 17 at a quarter of a degree.</para>
        /// </summary>
        const int MaxNoiseCluster = 4;

        // ---- the fittings under test ------------------------------------------------------------

        /// <summary>One baked fitting, and the rig call that tells the truth about it.</summary>
        readonly struct MotorCase
        {
            /// <summary><see cref="HullPropFleet"/> key — the fixture reads the extraction from the
            /// BAKER's own catalog rather than restating it, so the two cannot drift.</summary>
            public readonly string FleetKey;
            /// <summary>The rig's paint build (<c>basic</c>/<c>upgraded</c>/<c>work</c>/<c>sport</c>).</summary>
            public readonly string Variant;
            /// <summary>Lateral clamp positions to check this fitting at, in metres. <c>{0}</c> for a
            /// centreline engine; the twin's <c>±0.34</c> for the skiff's, because a mount the RIG can
            /// render is a mount the fixture can adjudicate.</summary>
            public readonly float[] Mounts;
            /// <summary>True when the rig's <c>renderMotor</c> takes an <c>mx</c> at all (the punt's
            /// does not — she is single-engine by the art).</summary>
            public readonly bool TakesMount;
            public readonly string Label;

            public MotorCase(string fleetKey, string variant, float[] mounts, bool takesMount, string label)
            {
                FleetKey = fleetKey; Variant = variant; Mounts = mounts;
                TakesMount = takesMount; Label = label;
            }
        }

        static readonly float[] Centreline = { 0f };
        static readonly float[] TwinMounts = { -0.34f, +0.34f };

        static IEnumerable<MotorCase> Cases()
        {
            yield return new MotorCase("puntMotorBasic", "basic", Centreline, false,
                                       "punt outboard, basic");
            yield return new MotorCase("puntMotorUpgraded", "upgraded", Centreline, false,
                                       "punt outboard, upgraded");
            yield return new MotorCase("skiffMotorWork", "work", Centreline, true,
                                       "skiff outboard, work (console skiff)");
            // The sport build is checked at BOTH of the twin's clamp positions as well as on the
            // centreline: the twin is this one mesh instantiated twice, so "the twin is right" is
            // exactly "this mesh is right at each mount".
            yield return new MotorCase("skiffMotorSport", "sport", TwinMounts, true,
                                       "skiff outboard, sport (twin, ±0.34 m)");
            yield return new MotorCase("skiffMotorSport", "sport", Centreline, true,
                                       "skiff outboard, sport (sport skiff, single)");
        }

        static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        // ---- the harness --------------------------------------------------------------------------

        /// <summary>
        /// The committed mesh, posed exactly the way <c>IsoFacetPropRenderer</c> poses it: rotate about
        /// the fitting's pivot, then offset laterally by the clamp mount. Returns a throwaway mesh the
        /// caller destroys.
        ///
        /// ⚠️ Normals are the RIG's flat per-face normals rotated, never Unity's smoothed
        /// recalculation — the facet shader takes one normal per face and that IS the shading.
        /// </summary>
        static Mesh Pose(HullPropMeshDef def, Quaternion r, float lateralMeters)
        {
            // ⚠️ The BOLTED-DOWN half first, then the half that swivels — which is the order the rig
            // emits them in (the bracket is built before the cowl), so the combined buffer is the
            // rig's own face order and a depth tie would fall the same way. The extractor refuses to
            // split a rig whose fixed faces are interleaved, for exactly this reason.
            Mesh bolted = def.FixedMesh == null
                ? null
                : Transform(def.FixedMesh, Quaternion.identity, Vector3.zero,
                            new Vector3(lateralMeters, 0f, 0f));
            Mesh swivelled = Transform(def.Mesh, r, def.PivotLocalMeters,
                                       new Vector3(lateralMeters, 0f, 0f));
            if (bolted == null) return swivelled;

            Mesh both = Combine(bolted, swivelled);
            Object.DestroyImmediate(bolted);
            Object.DestroyImmediate(swivelled);
            return both;
        }

        /// <summary>One mesh, rotated about <paramref name="pivot"/> and then offset — the exact
        /// arithmetic <c>IsoFacetPropRenderer.Apply</c> puts on the child's Transform.</summary>
        static Mesh Transform(Mesh source, Quaternion r, Vector3 pivot, Vector3 offset)
        {
            var src = source.vertices;
            var srcN = source.normals;
            var uv = new List<Vector4>();
            source.GetUVs(0, uv);

            var dst = new Vector3[src.Length];
            var dstN = new Vector3[srcN.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = offset + pivot + r * (src[i] - pivot);
            for (int i = 0; i < srcN.Length; i++) dstN[i] = r * srcN[i];

            var m = new Mesh { name = "PosedMotor", hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(dst);
            m.SetNormals(dstN);
            m.SetUVs(0, uv);
            m.SetTriangles(source.triangles, 0);
            return m;
        }

        /// <summary>What one sweep found.</summary>
        readonly struct Verdict
        {
            public readonly int WorstCluster, Silhouette, ReferenceMatchedNoCandidate, Ambiguous;
            public readonly int Inked, Differing, Poses;
            public readonly string Where;

            public Verdict(int worstCluster, int silhouette, int noCandidate, int ambiguous,
                           int inked, int differing, int poses, string where)
            {
                WorstCluster = worstCluster; Silhouette = silhouette;
                ReferenceMatchedNoCandidate = noCandidate; Ambiguous = ambiguous;
                Inked = inked; Differing = differing; Poses = poses; Where = where;
            }

            /// <summary>
            /// <b>Would this sweep FAIL the acceptance?</b> The sabotage curve is measured against
            /// exactly the assertions the acceptance makes — not against a proxy — so a sabotage that
            /// "scores" without breaking them would be proving nothing.
            /// </summary>
            public bool FailsAcceptance(int floor) =>
                Silhouette > 0 || ReferenceMatchedNoCandidate > 0 || WorstCluster > floor;

            public override string ToString() =>
                $"{Silhouette} silhouette + {ReferenceMatchedNoCandidate} unresolvable-colour " +
                $"differences over {Poses} poses, worst cluster {WorstCluster} at {Where}; " +
                $"{Differing}/{Inked} inked px differ; {Ambiguous} px were double-sided twins";
        }

        /// <summary>
        /// Sweeps every heading × steer × tilt × mount, posing the committed mesh with
        /// <paramref name="steerError"/> degrees of deliberate error added, and adjudicating against
        /// the rig's own <c>renderMotor</c> at the UNCORRUPTED pose.
        /// </summary>
        static Verdict Sweep(IRigScriptHost host, RigMeshData data, HullPropMeshDef def,
                             in MotorCase c, string rigGlobal, float steerError,
                             IEnumerable<(float position, float tilt)> poseSet)
        {
            var poseList = new List<(float position, float tilt)>(poseSet);
            int[] partners = RigMeshExtractor.FindReverseDuplicatePartners(data);
            bool anyTwins = false;
            for (int i = 0; i < partners.Length; i++) if (partners[i] >= 0) { anyTwins = true; break; }

            float maxSteer = def.MaxSteerDegrees;
            double elev = data.DefaultElev;

            int worst = 0, sil = 0, noCand = 0, ambiguous = 0, inked = 0, differing = 0, poses = 0;
            string where = "nowhere";

            for (int dir = 0; dir < 8; dir++)
            foreach ((float position, float tilt) in poseList)
            foreach (float mount in c.Mounts)
            {
                float steer = OutboardMotorMath.SteerDegreesForPosition(position, SteerColumns, maxSteer);

                var opts = new StringBuilder("{variant:'").Append(c.Variant).Append('\'');
                opts.Append(",steerDeg:").Append(Num(steer));
                opts.Append(",tilt:").Append(Num(tilt));
                if (c.TakesMount) opts.Append(",mx:").Append(Num(mount));
                opts.Append(",elev:").Append(Num(elev)).Append('}');

                byte[] truth = host.EvaluateBytes($"{rigGlobal}.renderMotor({dir},{opts})");

                var view = new RigViewOptions(dir, elev);
                // The basis comes from the ENGINE THE RIG RUNS IN: V8's cos/sin and .NET's differ in
                // the last ULP, which on a rasteriser with hard fill and dither thresholds flips a
                // handful of boundary pixels. That is a confound, not a finding.
                RigTrigBasis basis = RigTrigBasis.FromScriptHost(host, view);

                Mesh posed = Pose(def, OutboardMotorMeshPose.Rotation(steer + steerError, tilt), mount);
                RigPaintTrace trace = anyTwins ? new RigPaintTrace { ProbeAll = true } : null;
                byte[] mine = RigMeshReferenceRasterizer.RenderFromMesh(data, posed, view, basis, trace);
                Object.DestroyImmediate(posed);

                bool[] mask = null;
                if (trace != null)
                {
                    // Only paid for when the rig actually builds double-sided twins — see the class
                    // note on RigAmbiguousPixels. Neither motor rig does; the branch stays so a rig
                    // edit that introduces one is handled rather than silently mis-adjudicated.
                    RigAmbiguousPixels.Result amb =
                        RigAmbiguousPixels.From(trace, truth, data.W, data.H, partners);
                    ambiguous += amb.Count;
                    noCand += amb.ReferenceMatchedNoCandidate;
                    mask = amb.Mask;
                }

                RigPixelDiff d = RigMeshReferenceRasterizer.Compare(truth, mine, data.W, data.H, mask);
                poses++;
                inked += d.InkedPixels;
                differing += d.DifferingPixels;
                sil += d.CoverageOnlyDifferences;
                if (d.LargestDifferingCluster > worst)
                {
                    worst = d.LargestDifferingCluster;
                    where = $"dir {dir}, steer {steer:F2}° (pos {position:F2}), tilt {tilt:F0}°, " +
                            $"mx {mount:F2} ({d})";
                }
            }

            return new Verdict(worst, sil, noCand, ambiguous, inked, differing, poses, where);
        }

        static RigMeshData Extract(IRigScriptHost host, in FleetProp p) =>
            RigMeshExtractor.ExtractFrom(host, p.ScriptPath, p.GlobalName, p.Extraction);

        static HullPropMeshDef Load(in FleetProp p)
        {
            var def = AssetDatabase.LoadAssetAtPath<HullPropMeshDef>(p.AssetPath);
            Assert.IsNotNull(def, $"{p.AssetPath} did not load — {p.Label} is not committed. " +
                                  "Run Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake ALL hull fittings.");
            Assert.IsTrue(def.IsUsable(), $"{p.AssetPath} is not usable.");
            return def;
        }

        // ---- the acceptance -----------------------------------------------------------------------

        [Test]
        public void TheCommittedOutboards_ReproduceTheirRigsOwnRender_AcrossHeadingsSteerAndTilt()
        {
            var report = new StringBuilder();
            foreach (MotorCase c in Cases())
            {
                FleetProp p = HullPropFleet.Get(c.FleetKey);
                // The variant under test must be the variant that was BAKED. A fixture sweeping
                // 'basic' against an asset built from 'upgraded' would compare two different engines
                // and blame the difference on the pose.
                StringAssert.Contains($"variant:'{c.Variant}'", p.Extraction.FaceBuilderCall,
                    $"{c.Label}: the fixture and HullPropFleet disagree about which paint build " +
                    $"{p.AssetPath} was baked from.");

                HullPropMeshDef def = Load(p);
                using IRigScriptHost host = RigScriptHostFactory.Create();
                RigMeshData data = Extract(host, p);

                Verdict v = Sweep(host, data, def, c, p.GlobalName, steerError: 0f, poseSet: Poses());
                report.AppendLine($"  {c.Label}: {v}");

                Assert.AreEqual(0, v.Silhouette,
                    $"The committed {c.Label} covers different PIXELS from its rig's own renderMotor: " +
                    $"{v}. Coverage is a fact — nothing about it is ambiguous — so this means the " +
                    "engine is in the wrong PLACE. Check the swivel point (HullPropMeshDef." +
                    "PivotLocalMeters, read from the rig's YA/ZT), the steer/tilt composition order " +
                    "in OutboardMotorMeshPose, and the lateral mount, in that order.");

                Assert.AreEqual(0, v.ReferenceMatchedNoCandidate,
                    $"The committed {c.Label} shades differently from its rig inside a double-sided " +
                    $"pair: {v}. (Neither motor rig builds one, so this firing at all means the rig " +
                    "changed shape.)");

                Assert.LessOrEqual(v.WorstCluster, MaxNoiseCluster,
                    $"The committed {c.Label} does not reproduce its rig's own renderMotor: {v}, " +
                    $"floor {MaxNoiseCluster}. A connected patch means geometry moved or shaded from a " +
                    "different normal, not that arithmetic disagreed in the last bit.");
            }
            Debug.Log($"[phase 7] outboard acceptance, clean:\n{report}");
        }

        /// <summary>
        /// <b>The sabotage, measured rather than asserted.</b> Pose the engine with a steer error and
        /// count how much of the acceptance breaks — the same failure a wrong pivot, a transposed
        /// rotation or a mis-scaled authority produces, and the one a reader would most want quantified.
        ///
        /// <para>A fixture that cannot discriminate proves nothing, and "the sabotage was caught" is
        /// only meaningful with a curve behind it: the numbers this run measures are logged, and the
        /// assertions below are that <b>zero error is the only clean point</b> and that the first
        /// non-zero rung already fails the acceptance outright.</para>
        ///
        /// <para><b>MEASURED</b>, sport outboard, 24 poses per rung (8 headings × 3 steers),
        /// steer error → (silhouette differences, worst colour cluster):</para>
        /// <code>
        ///   0.00° → (  0,  0)   ← the only clean point
        ///   0.25° → ( 34, 17)
        ///   0.50° → ( 54, 17)
        ///   1.00° → ( 94, 17)
        ///   2.00° → (233, 29)
        ///   4.00° → (468, 34)
        /// </code>
        /// <para><b>The fixture resolves a QUARTER of a degree of swivel</b>, on both channels at once
        /// — which was not the expected answer. An outboard is a compact part (its longest lever from
        /// the swivel is well under a metre, against the dory oar's 2.3 m), so the silhouette was the
        /// channel in doubt; at 0.25° it already moves 34 pixels, because a sub-pixel shift still
        /// flips the pixels the outline lands on.</para>
        /// </summary>
        [Test]
        public void ASteerError_IsCaught_ProvingTheFixtureResolvesTheSwivel()
        {
            // One fitting, a reduced pose set: the curve is about SENSITIVITY, and six sweeps of the
            // full set would cost minutes to say the same thing.
            FleetProp p = HullPropFleet.Get("skiffMotorSport");
            HullPropMeshDef def = Load(p);
            var posesForCurve = new[] { (0f, 0f), (2.5f, 0f), (8f, 0f) };

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = Extract(host, p);
            var c = new MotorCase("skiffMotorSport", "sport", Centreline, true, "skiff outboard, sport");

            float[] errors = { 0f, 0.25f, 0.5f, 1f, 2f, 4f };
            var curve = new StringBuilder();
            var verdicts = new Verdict[errors.Length];

            for (int i = 0; i < errors.Length; i++)
            {
                verdicts[i] = Sweep(host, data, def, c, p.GlobalName, errors[i], posesForCurve);
                curve.AppendLine($"  {errors[i],5:F2}° → {verdicts[i].Silhouette,6} silhouette, " +
                                 $"worst cluster {verdicts[i].WorstCluster,6}, " +
                                 $"{verdicts[i].Differing,7}/{verdicts[i].Inked} px " +
                                 $"[{(verdicts[i].FailsAcceptance(MaxNoiseCluster) ? "CAUGHT" : "missed")}]");
            }
            Debug.Log($"[phase 7] outboard steer-error sensitivity ({verdicts[0].Poses} poses each):\n{curve}");

            Assert.IsFalse(verdicts[0].FailsAcceptance(MaxNoiseCluster),
                $"The CORRECT pose is not clean: {verdicts[0]}. Everything below is meaningless until " +
                "it is.");

            for (int i = 1; i < errors.Length; i++)
                Assert.IsTrue(verdicts[i].FailsAcceptance(MaxNoiseCluster),
                    $"A {errors[i]}° steer error was NOT caught — the acceptance above would pass a " +
                    $"visibly wrong engine. Measured {verdicts[i]}, floor {MaxNoiseCluster}.\n{curve}");

            // …and it must be MONOTONE in the obvious direction, or the metric is measuring noise
            // rather than the pose. Checked on the cluster channel, which is the sensitive one.
            Assert.Greater(verdicts[errors.Length - 1].WorstCluster, verdicts[1].WorstCluster,
                $"4° of steer error should disturb more than 0.25° does. It does not, which means the " +
                $"metric saturated and the curve says nothing:\n{curve}");
        }

        /// <summary>
        /// <b>The composition ORDER is load-bearing, and this is what says so.</b>
        /// <c>Rz(steer)·Rx(−tilt)</c> against <c>Rx(−tilt)·Rz(steer)</c> is the difference between
        /// tilting about the engine's own clamp bar and tilting about the hull's beam. They are
        /// IDENTICAL whenever either angle is zero — so a fixture that only ever swept steer, or only
        /// ever ran at the running trim, could not tell them apart and would wave the wrong one through.
        /// </summary>
        [Test]
        public void SwappingTheSteerAndTiltComposition_IsCaught_AtAPoseWhereTheyDiffer()
        {
            FleetProp p = HullPropFleet.Get("skiffMotorWork");
            HullPropMeshDef def = Load(p);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = Extract(host, p);
            double elev = data.DefaultElev;
            const float Steer = 22f, Tilt = 30f;

            int worstSwapped = 0, worstCorrect = 0;
            for (int dir = 0; dir < 8; dir++)
            {
                byte[] truth = host.EvaluateBytes(
                    $"{p.GlobalName}.renderMotor({dir},{{variant:'work',steerDeg:{Num(Steer)}," +
                    $"tilt:{Num(Tilt)},mx:0,elev:{Num(elev)}}})");
                var view = new RigViewOptions(dir, elev);
                RigTrigBasis basis = RigTrigBasis.FromScriptHost(host, view);

                Mesh correct = Pose(def, OutboardMotorMeshPose.Rotation(Steer, Tilt), 0f);
                Mesh swapped = Pose(def, SwappedComposition(Steer, Tilt), 0f);

                worstCorrect = Math.Max(worstCorrect, RigMeshReferenceRasterizer
                    .Compare(truth, RigMeshReferenceRasterizer.RenderFromMesh(data, correct, view, basis),
                             data.W, data.H).LargestDifferingCluster);
                worstSwapped = Math.Max(worstSwapped, RigMeshReferenceRasterizer
                    .Compare(truth, RigMeshReferenceRasterizer.RenderFromMesh(data, swapped, view, basis),
                             data.W, data.H).LargestDifferingCluster);

                Object.DestroyImmediate(correct);
                Object.DestroyImmediate(swapped);
            }

            Debug.Log($"[phase 7] tilt/steer composition at {Steer}°/{Tilt}°: " +
                      $"correct worst cluster {worstCorrect}, swapped {worstSwapped}.");
            Assert.LessOrEqual(worstCorrect, MaxNoiseCluster,
                $"The correct composition does not reproduce the rig at a tilted, hard-over pose " +
                $"(worst cluster {worstCorrect}).");
            Assert.Greater(worstSwapped, MaxNoiseCluster,
                "Swapping the steer and tilt composition was NOT caught, so this fixture cannot tell " +
                "which axis the engine tilts about. Sweep a pose where BOTH angles are non-zero.");
        }

        /// <summary>
        /// <b>The twin, as the mesh path actually builds it.</b> The sprite era needed a
        /// draw-the-far-engine-first rule because two sprites cannot interleave in depth; two meshes
        /// sharing a depth buffer simply resolve. This renders BOTH instances into one z-buffer and
        /// compares against the rig painting both engines' faces in one pass — which is the claim
        /// "the twin is two instances of one mesh, and no ordering rule is needed" stated in pixels.
        /// </summary>
        [Test]
        public void TheTwin_IsTwoInstancesOfOneMesh_WithNoDrawOrderRule()
        {
            FleetProp p = HullPropFleet.Get("skiffMotorSport");
            HullPropMeshDef def = Load(p);
            Assert.AreEqual(2, def.MountsForFit(multiEngine: true).Length,
                "The sport outboard must carry the rig's own dual clamp spacing.");
            float[] mounts = def.MountsForFit(multiEngine: true);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = Extract(host, p);
            double elev = data.DefaultElev;

            // The rig renders ONE engine per renderMotor call, so the two-engine truth is its own
            // _paint over the concatenated face lists — reached by the same in-memory widening the
            // baker uses for motorFaces. The file on disk is not touched.
            string source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(RigCatalog.RepoRoot, p.ScriptPath));
            host.Execute(RigMeshExtractor.WidenExportedLiteral(
                source, p.GlobalName, new[] { "_paint", "_toRGBA", "motorFaces" }, p.ScriptPath));

            int worst = 0, sil = 0;
            string where = "nowhere";
            foreach (int dir in new[] { 0, 1, 2, 3, 4, 5, 6, 7 })
            foreach (float position in new[] { 0f, 4f, 8f })
            {
                float steer = OutboardMotorMath.SteerDegreesForPosition(
                    position, SteerColumns, def.MaxSteerDegrees);
                string o(float mx) =>
                    $"{{variant:'sport',steerDeg:{Num(steer)},tilt:0,mx:{Num(mx)}}}";

                byte[] truth = host.EvaluateBytes(
                    $"{p.GlobalName}._toRGBA({p.GlobalName}._paint(" +
                    $"{p.GlobalName}.motorFaces({o(mounts[0])})." +
                    $"concat({p.GlobalName}.motorFaces({o(mounts[1])}))," +
                    $"{{dir:{dir},elev:{Num(elev)}}}))");

                Quaternion r = OutboardMotorMeshPose.Rotation(steer, 0f);
                Mesh a = Pose(def, r, mounts[0]);
                Mesh b = Pose(def, r, mounts[1]);
                Mesh both = Combine(a, b);

                var view = new RigViewOptions(dir, elev);
                byte[] mine = RigMeshReferenceRasterizer.RenderFromMesh(
                    data, both, view, RigTrigBasis.FromScriptHost(host, view));

                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(both);

                RigPixelDiff d = RigMeshReferenceRasterizer.Compare(truth, mine, data.W, data.H);
                sil += d.CoverageOnlyDifferences;
                if (d.LargestDifferingCluster > worst)
                {
                    worst = d.LargestDifferingCluster;
                    where = $"dir {dir}, steer {steer:F2}° ({d})";
                }
            }

            Debug.Log($"[phase 7] twin outboard, both engines in one z-buffer: {sil} silhouette " +
                      $"differences, worst cluster {worst} at {where}.");
            Assert.AreEqual(0, sil,
                $"The twin's two instances do not cover what the rig's two engines cover ({where}).");
            Assert.LessOrEqual(worst, MaxNoiseCluster,
                $"Two instances of the one fitting at ±{mounts[1]:F2} m do not reproduce the rig's own " +
                $"two-engine render: worst cluster {worst} at {where}, floor {MaxNoiseCluster}. Where " +
                "they overlap, the depth buffer must resolve them exactly as the rig's z-buffer does — " +
                "if this fails, an ordering rule really was load-bearing after all.");
        }

        /// <summary><c>Rx(−tilt)·Rz(steer)</c> — the SAME two rotations the correct pose uses, applied
        /// in the other order. Built explicitly rather than as an <c>AngleAxis</c> chain so the
        /// sabotage is precisely the mistake it names, and not also a handedness mistake.</summary>
        static Quaternion SwappedComposition(float steerDegrees, float tiltDegrees)
        {
            float sa = steerDegrees * Mathf.Deg2Rad, ta = tiltDegrees * Mathf.Deg2Rad;
            float cs = Mathf.Cos(sa), ss = Mathf.Sin(sa);
            float ct = Mathf.Cos(ta), st = Mathf.Sin(ta);

            var m = Matrix4x4.identity;
            m[0, 0] = cs; m[0, 1] = -ss; m[0, 2] = 0f;
            m[1, 0] = ss * ct; m[1, 1] = cs * ct; m[1, 2] = st;
            m[2, 0] = -ss * st; m[2, 1] = -cs * st; m[2, 2] = ct;
            return RigRotationMath.FromMatrix(m);
        }

        /// <summary>Two posed meshes as one buffer, in face order — the same order the rig concatenates
        /// its two engines' face lists, so a depth tie (there are none at 0.68 m of separation) would
        /// fall the same way.</summary>
        static Mesh Combine(Mesh a, Mesh b)
        {
            var verts = new List<Vector3>(a.vertices); verts.AddRange(b.vertices);
            var norms = new List<Vector3>(a.normals); norms.AddRange(b.normals);
            var uvA = new List<Vector4>(); a.GetUVs(0, uvA);
            var uvB = new List<Vector4>(); b.GetUVs(0, uvB);
            uvA.AddRange(uvB);

            int[] ta = a.triangles, tb = b.triangles;
            var tris = new int[ta.Length + tb.Length];
            Array.Copy(ta, tris, ta.Length);
            int offset = a.vertexCount;
            for (int i = 0; i < tb.Length; i++) tris[ta.Length + i] = tb[i] + offset;

            var m = new Mesh { name = "PosedTwin", hideFlags = HideFlags.HideAndDontSave };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvA);
            m.SetTriangles(tris, 0);
            return m;
        }

        /// <summary>
        /// The measured pose facts each fitting carries, against the rigs' own values. The pivot is the
        /// one number the whole pose turns on, and it is a RECONSTRUCTION (our reading of two private
        /// consts), so it is checked directly as well as in pixels.
        /// </summary>
        [Test]
        public void TheBakedPoseFacts_MatchTheirRigs()
        {
            foreach (var (key, global, script, expectSteer, expectTilt, expectMounts) in new[]
            {
                ("puntMotorBasic", "PuntIso", "docs/art/rigs/puntIsoRig.js", 32f, 40f, 0),
                ("puntMotorUpgraded", "PuntIso", "docs/art/rigs/puntIsoRig.js", 32f, 40f, 0),
                ("skiffMotorWork", "SkiffMotor", "docs/art/rigs/skiffMotorRig.js", 30f, 40f, 2),
                ("skiffMotorSport", "SkiffMotor", "docs/art/rigs/skiffMotorRig.js", 30f, 40f, 2),
            })
            {
                FleetProp p = HullPropFleet.Get(key);
                HullPropMeshDef def = Load(p);

                Assert.AreEqual(expectSteer, def.MaxSteerDegrees, 1e-3f,
                    $"{p.Label}: steer authority is an ART FACT of the rig (MOTOR.maxSteer).");
                Assert.AreEqual(expectTilt, def.MaxTiltDegrees, 1e-3f, $"{p.Label}: MOTOR.tiltMax.");
                Assert.AreEqual(expectMounts, def.LateralMountsMeters.Length,
                    $"{p.Label}: the rig's declared multi-fit clamp positions.");
                Assert.AreEqual(script, def.SourceRigPath, $"{p.Label}: provenance.");

                using IRigScriptHost host = RigScriptHostFactory.Create();
                RigMeshData data = Extract(host, p);

                // The swivel, straight out of the rig's own closure — the reconstruction's claim,
                // checked against the constants it claims to read.
                double ya = host.EvaluateNumber($"{global}.swivelPt()[1]");
                double zt = host.EvaluateNumber($"{global}.swivelPt()[2]");
                Assert.AreEqual(0f, def.PivotLocalMeters.x, 1e-4f,
                    $"{p.Label}: the swivel is on the centreline; the lateral mount is applied " +
                    "separately, per instance.");
                Assert.AreEqual(ya, def.PivotLocalMeters.y, 1e-4,
                    $"{p.Label}: the swivel's fore-aft position drifted from the rig's YA.");
                Assert.AreEqual(zt, def.PivotLocalMeters.z, 1e-4,
                    $"{p.Label}: the clamp height drifted from the rig's ZT.");

                // ⚠️ The engine's cell is WIDER than its hull's because it swings outboard of the
                // transom. Reading the hull's would crop the cowl on hard-over headings.
                Assert.AreEqual(host.EvaluateNumber($"{global}.MOTOR.W"), def.CellW,
                    $"{p.Label}: cell width must be the MOTOR's, not the hull's.");
                Assert.AreEqual(host.EvaluateNumber($"{global}.MOTOR.H"), def.CellH, $"{p.Label}: cell height.");
                Assert.AreEqual((float)host.EvaluateNumber($"{global}.MOTOR.pivot.x"), def.PivotPx.x, 1e-3f,
                    $"{p.Label}: cell pivot x.");
                Assert.AreEqual(data.PxPerMetre, def.PxPerMetre, $"{p.Label}: px/metre.");

                // ⚠️ An outboard is NOT one rigid body: the clamp bracket is bolted to the transom
                // and the engine swivels on it. Both rigs build it through the identity placement,
                // and the bake finds that out by building the face list at two poses and keeping the
                // faces that did not move. If this ever comes back null the split silently stopped
                // happening, and the bracket would ride round with the cowl — invisible dead ahead
                // and worst at hard-over, which is the failure most likely to be waved through.
                Assert.IsNotNull(def.FixedMesh,
                    $"{p.Label}: the clamp bracket is fixed to the transom and must be baked as the " +
                    "fitting's non-articulating half.");
                Assert.Greater(def.FixedMesh.vertexCount, 0, $"{p.Label}: the bracket has no geometry.");

                int fixedVerts = 0, movingVerts = 0;
                foreach (RigFace f in data.Faces)
                    if (f.FixedInPose) fixedVerts += f.V.Length; else movingVerts += f.V.Length;

                Assert.AreEqual(fixedVerts, def.FixedMesh.vertexCount,
                    $"{p.Label}: the committed bracket does not carry the faces a fresh extraction " +
                    $"measures as pose-fixed ({data.FixedFaceCount} of {data.Faces.Count}).");
                Assert.AreEqual(movingVerts, def.Mesh.vertexCount,
                    $"{p.Label}: the swivelling half does not carry the rest.");
            }
        }
    }
}
