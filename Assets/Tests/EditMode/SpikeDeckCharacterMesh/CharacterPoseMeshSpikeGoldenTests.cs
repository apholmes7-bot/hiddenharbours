using System.IO;
using HiddenHarbours.SpikeDeckCharacterMesh.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.SpikeDeckCharacterMesh
{
    /// <summary>
    /// ⚠️ SPIKE (deck-character-mesh, draft ADR 0024) — the spike's measured answers, CI-safe (CPU
    /// only, no graphics device needed — the RigBaking oracle pattern):
    ///
    /// <list type="number">
    /// <item>The character rig's PER-POSE face list extracts into the SAME RigMeshData the ADR 0022
    /// pipeline consumes, without touching the rig source (byte-identity pinned).</item>
    /// <item>The facet turntable sign for the character is NEGATED (azimuthCounterClockwise = true
    /// for <c>HullMeshMath.HeadingToDirUnits</c>) — measured from pixels, with the wrong sign
    /// proven catastrophically wrong. ⚠️ This is the OPPOSITE of what the label probe suggests
    /// (labels are CW-correct for characters) — the two facts genuinely differ, which is exactly
    /// how the mirrored-boat class of defect ships. Pinned so a rig change screams.</item>
    /// <item>The golden fidelity band: the rig's own render vs the facet pipeline's CPU oracle,
    /// per dir. The residual is the known deltas (the fullscreen resolve's 0.30 m depth-edge
    /// floor vs the character rig's tighter 0.13 m, plus single-step dither/keyline noise) — the
    /// numbers quoted by the draft ADR.</item>
    /// </list>
    /// </summary>
    public class CharacterPoseMeshSpikeGoldenTests
    {
        const string Build = "fisher";

        [Test]
        public void RigSource_IsByteIdentical_AfterExtraction()
        {
            string full = Path.Combine(RigCatalog.RepoRoot, CharacterPoseMeshExtractor.ScriptPath);
            byte[] before = File.ReadAllBytes(full);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.LoadWidened(host);
            CharacterPoseMeshExtractor.ExtractPose(host, Build, "idle", 0);

            byte[] after = File.ReadAllBytes(full);
            CollectionAssert.AreEqual(before, after,
                "docs/art/rigs/** is the art director's source and must never be written by the spike.");
        }

        [Test]
        public void PoseExtraction_ReadsTheRigsOwnFacts()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.LoadWidened(host);
            RigMeshData data = CharacterPoseMeshExtractor.ExtractPose(host, Build, "idle", 0);

            Assert.AreEqual(64, data.W);
            Assert.AreEqual(88, data.H);
            Assert.AreEqual(32.0, data.PivotX, 1e-9);
            Assert.AreEqual(80.0, data.PivotY, 1e-9);
            Assert.AreEqual(32, data.PxPerMetre);
            Assert.AreEqual(40.0, data.DefaultElev, 1e-9);
            Assert.Greater(data.Faces.Count, 50, "a posed mannequin is dozens of faces");
            Assert.GreaterOrEqual(data.Materials.Count, 8, "skin/hair/over/shirt/boot/eye/lash/lip/sole");
            Assert.LessOrEqual(data.Materials.Count, 16, "the facet shader's _RampMeta holds 16");
            TestContext.WriteLine($"idle[0]: {data}");
        }

        [Test]
        public void PoseMeshes_AreARealFlipbook_AndCheapEnough()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.LoadWidened(host);

            RigMeshData f0 = CharacterPoseMeshExtractor.ExtractPose(host, Build, "idle", 0);
            RigMeshData f3 = CharacterPoseMeshExtractor.ExtractPose(host, Build, "idle", 3);
            RigMeshBuild b0 = RigMeshBuilder.Build(f0, "idle0");
            RigMeshBuild b3 = RigMeshBuilder.Build(f3, "idle3");

            // The pose is baked into the vertices — different frames must be different geometry.
            Assert.AreNotEqual(b0.Mesh.vertices[0], b3.Mesh.vertices[b3.Mesh.vertices.Length - 1]);
            bool anyDiffer = false;
            var v0 = b0.Mesh.vertices;
            var v3 = b3.Mesh.vertices;
            for (int i = 0; i < Mathf.Min(v0.Length, v3.Length) && !anyDiffer; i++)
                anyDiffer = (v0[i] - v3[i]).sqrMagnitude > 1e-10f;
            Assert.IsTrue(anyDiffer || v0.Length != v3.Length,
                "idle frame 0 and frame 3 baked identical geometry — the pose surface is not wired.");

            // The cost question (spike Q4): a whole 12-mesh flipbook must stay trivial next to the
            // hulls (lobster 1,384 tris). Per-frame budget generous on purpose — this is a spike
            // band, not a tuned number.
            TestContext.WriteLine($"idle[0]: {b0} · idle[3]: {b3}");
            Assert.Less(b0.Triangles, 4000);
            Assert.Less(b0.BufferBytes, 512 * 1024);

            Object.DestroyImmediate(b0.Mesh);
            Object.DestroyImmediate(b3.Mesh);
        }

        [Test]
        public void FacetTurntableSign_IsNegated_MeasuredFromPixels()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.LoadWidened(host);

            bool negated = DeckCharacterMeshSpikeBaker.MeasureFacetSign(host, out string report);
            TestContext.WriteLine(report);

            // th = −dir·π/4 in the character rig (the ADR-0006 label fix) vs +dir·π/4 in the boats
            // and the shared IsoFacetMath projection ⇒ the mapping must negate. Pinned: if this
            // flips, the rig changed its turntable and every consumer needs re-measuring.
            Assert.IsTrue(negated,
                "The character rig's facet turntable sign measured DIRECT — it has always measured " +
                "NEGATED (its th = −dir·π/4). The rig's projection changed; re-measure everything.");
        }

        [Test]
        public void Golden_RigRenderVsFacetOracle_StaysInTheSpikeBand()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            CharacterPoseMeshExtractor.LoadWidened(host);

            foreach (string anim in new[] { "idle", "hold" })
            {
                RigMeshData pose = CharacterPoseMeshExtractor.ExtractPose(host, Build, anim, 0);
                for (int dir = 0; dir < 8; dir++)
                {
                    byte[] truth = CharacterPoseMeshExtractor.RenderTruth(host, dir, anim, 0);
                    var view = new RigViewOptions(-dir, pose.DefaultElev);   // measured sign: negated
                    byte[] oracle = RigMeshReferenceRasterizer.RenderFromFaces(
                        pose, view, RigTrigBasis.FromScriptHost(host, view));
                    RigPixelDiff diff = RigMeshReferenceRasterizer.Compare(truth, oracle, pose.W, pose.H);
                    TestContext.WriteLine($"{anim}[0] dir {dir}: {diff}");

                    // The spike band, not a shipping tolerance: the whole KNOWN residual is the
                    // resolve's 0.30 m depth-edge floor (vs the rig's 0.13 m limb separation) plus
                    // single-step dither noise — edge-shaped, scattered. A mirrored/broken
                    // convention differs across the body in one giant cluster instead.
                    Assert.Less(diff.PercentDiffering, 20.0,
                        $"{anim}[0] dir {dir} left the spike band — not edge noise any more.");
                    Assert.Less(diff.LargestDifferingCluster, 250,
                        $"{anim}[0] dir {dir}: a single large cluster means a convention broke, " +
                        "not a threshold.");
                }
            }
        }
    }
}
