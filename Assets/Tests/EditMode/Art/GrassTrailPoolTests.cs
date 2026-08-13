using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guard for the MULTI-WALKER grass-trail contract. <see cref="GrassFootstep"/> used to publish one
    /// 24-point ring buffer into a single global array from every instance, so a second walker did not add a
    /// second trail — it overwrote the first every frame (last writer wins). The array is now a POOL:
    /// <see cref="GrassFootstep.MaxWalkers"/> segments of <see cref="GrassFootstep.PointsPerWalker"/> points, with
    /// a companion <c>_GrassWalkers</c> record per segment carrying that walker's bounding circle and its own
    /// moving factor (retiring the scalar <c>_PlayerMoving</c>, which could only describe one walker).
    ///
    /// <para>Two things are pinned here. (1) THE SIZES, across C# and BOTH shaders that read the pool — a shader
    /// declaring a different <c>WALKERS_N</c>/<c>POINTS_N</c> than the component writes does not fail to compile,
    /// it silently reads the wrong walker's footprints, which is exactly the class of bug this pool replaces.
    /// (2) THE CULL IS SAFE — the per-walker bounding-circle early-out may only ever skip points that could not
    /// have bent the blade anyway, so it changes cost and never picture.</para>
    ///
    /// <para>The runtime half (two walkers really do keep two trails; a released slot is reusable) needs
    /// <c>OnEnable</c>, which EditMode does not run — that lives in
    /// <c>Assets/Tests/PlayMode/GrassTrailMultiWalkerPlayTests.cs</c>.</para>
    /// </summary>
    public class GrassTrailPoolTests
    {
        private const float Eps = 1e-4f;

        private const string GrassShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursGrass.shader";
        private const string FlowerShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursFlower.shader";

        /// <summary>The shaders that read the shared pool — both must agree with the component and each other.</summary>
        private static readonly string[] PoolShaders = { GrassShaderPath, FlowerShaderPath };

        // ---- the SIZE contract: C# constants vs both shaders ---------------------------------------------

        [Test]
        public void EveryShaderThatReadsThePool_DeclaresTheSameWalkerAndPointCountsAsTheComponent(
            [ValueSource(nameof(PoolShaders))] string shaderPath)
        {
            string src = ReadShader(shaderPath);

            Assert.AreEqual(
                GrassFootstep.MaxWalkers, ReadDefine(src, "WALKERS_N", shaderPath),
                $"'{shaderPath}' declares a different WALKERS_N than GrassFootstep.MaxWalkers. A mismatch does " +
                "NOT fail to compile — the shader just reads segments the component never writes (or misses ones " +
                "it does), so walkers silently lose or swap trails. Change both together.");

            Assert.AreEqual(
                GrassFootstep.PointsPerWalker, ReadDefine(src, "POINTS_N", shaderPath),
                $"'{shaderPath}' declares a different POINTS_N than GrassFootstep.PointsPerWalker. The segment " +
                "STRIDE would disagree, so walker N would read a slice of walkers N-1 and N. Change both together.");
        }

        [Test]
        public void EveryShaderThatReadsThePool_DeclaresBothGlobalArraysAtTheContractSize(
            [ValueSource(nameof(PoolShaders))] string shaderPath)
        {
            string src = CodeOnly(ReadShader(shaderPath));

            StringAssert.Contains(
                "float4 _GrassTrail[TRAIL_N];", src,
                $"'{shaderPath}' must declare the trail as float4 _GrassTrail[TRAIL_N] — Unity fixes a global " +
                "array's size at the first SetGlobalVectorArray, so a hard-coded smaller size drops walkers.");
            StringAssert.Contains(
                "float4 _GrassWalkers[WALKERS_N];", src,
                $"'{shaderPath}' must declare the per-walker records as float4 _GrassWalkers[WALKERS_N] " +
                "(xy = trail bounding-circle centre, z = radius or negative for an unclaimed slot, w = that " +
                "walker's 0..1 moving factor).");
        }

        [Test]
        public void NoShaderStillReadsThePlayerMovingScalar_ItCouldOnlyEverDescribeOneWalker(
            [ValueSource(nameof(PoolShaders))] string shaderPath)
        {
            // CODE only — both shaders still NAME the retired global in their header comments, explaining why
            // it went away. It is the declaration and the read that must be gone.
            string src = CodeOnly(ReadShader(shaderPath));
            StringAssert.DoesNotContain(
                "_PlayerMoving", src,
                $"'{shaderPath}' still reads the retired global _PlayerMoving. It was ONE scalar for the whole " +
                "scene, so with several walkers whoever wrote last gated everybody's footprints. The moving " +
                "factor now rides in each walker's own _GrassWalkers record (w).");
        }

        [Test]
        public void ThePool_HoldsThePlayerPlusAVillageOfWalkers()
        {
            // St Peters wants seven: the player plus the six villagers on routines. The constant exists for that
            // reason, so state the reason — shrinking it below seven silently drops a villager's trail.
            Assert.GreaterOrEqual(
                GrassFootstep.MaxWalkers, 7,
                "The pool must hold the player plus St Peters' six villagers on routines.");
            Assert.AreEqual(
                GrassFootstep.MaxWalkers * GrassFootstep.PointsPerWalker, GrassFootstep.TrailPointCount,
                "TrailPointCount is the array the shaders declare — it must be exactly walkers times points.");
            Assert.Greater(
                GrassFootstep.PlayerPriority, 0,
                "The player must outrank the ambient default (0) or a village could evict its trail.");
        }

        // ---- segments: fixed stride, disjoint, and covering ----------------------------------------------

        [Test]
        public void SlotSegments_AreDisjointAndCoverTheWholeArray()
        {
            var owner = new int[GrassFootstep.TrailPointCount];
            for (int i = 0; i < owner.Length; i++) owner[i] = -1;

            for (int slot = 0; slot < GrassFootstep.MaxWalkers; slot++)
            {
                int b = GrassFootstep.SlotBase(slot);
                Assert.GreaterOrEqual(b, 0);
                Assert.LessOrEqual(
                    b + GrassFootstep.PointsPerWalker, GrassFootstep.TrailPointCount,
                    $"Slot {slot} runs past the end of _GrassTrail.");
                for (int i = 0; i < GrassFootstep.PointsPerWalker; i++)
                {
                    Assert.AreEqual(-1, owner[b + i], $"Point {b + i} is claimed by two slots — segments overlap.");
                    owner[b + i] = slot;
                }
            }

            for (int i = 0; i < owner.Length; i++)
                Assert.AreNotEqual(-1, owner[i], $"Point {i} belongs to no slot — the stride leaves a hole.");
        }

        [Test]
        public void SlotBase_MatchesTheStrideTheShaderIndexesWith()
        {
            // The shader computes pBase = wi * POINTS_N. Any other mapping here would read another walker's path.
            for (int slot = 0; slot < GrassFootstep.MaxWalkers; slot++)
                Assert.AreEqual(slot * GrassFootstep.PointsPerWalker, GrassFootstep.SlotBase(slot));
        }

        // ---- the bounding circle: tight, and never cuts a point that could still bend ---------------------

        [Test]
        public void GrowRadius_IgnoresPointsThatHaveAlreadySprungBack()
        {
            var centre = new Vector2(10f, 10f);
            float r = GrassFootstep.GrowRadius(0f, centre, new Vector2(40f, 10f), 0f);
            Assert.AreEqual(0f, r, Eps, "A faded point (strength 0) exerts no bend, so it must not inflate the circle.");
            r = GrassFootstep.GrowRadius(0f, centre, new Vector2(40f, 10f), -1f);
            Assert.AreEqual(0f, r, Eps, "A never-set point must not inflate the circle either.");
        }

        [Test]
        public void GrowRadius_CoversEveryLivePointAndOnlyGrows()
        {
            var centre = new Vector2(3f, -2f);
            float r = 0f;
            var live = new[] { new Vector2(3.5f, -2f), new Vector2(3f, 0.5f), new Vector2(1f, -2.25f) };
            foreach (Vector2 p in live) r = GrassFootstep.GrowRadius(r, centre, p, 0.4f);

            foreach (Vector2 p in live)
                Assert.LessOrEqual(
                    Vector2.Distance(centre, p), r + Eps,
                    "Every live point must sit inside the published circle or the shader would cull a real bend.");

            float shrunk = GrassFootstep.GrowRadius(r, centre, new Vector2(3.05f, -2f), 1f);
            Assert.AreEqual(r, shrunk, Eps, "A nearer point must not shrink the circle.");
        }

        [Test]
        public void WalkerCanReach_SkipsUnclaimedSlots()
        {
            Assert.IsFalse(
                GrassFootstep.WalkerCanReach(Vector2.zero, Vector2.zero, -1f, 0.5f),
                "A negative radius marks an unclaimed slot — the shader must skip it outright.");
        }

        [Test]
        public void WalkerCanReach_IsTheCircleGrownByTheFootRadius()
        {
            var centre = new Vector2(5f, 5f);
            const float radius = 2f;
            const float foot = 0.5f;

            Assert.IsTrue(GrassFootstep.WalkerCanReach(centre, centre, radius, foot), "A blade at the centre is in reach.");
            Assert.IsTrue(
                GrassFootstep.WalkerCanReach(centre + new Vector2(radius + foot - 1e-3f, 0f), centre, radius, foot),
                "Just inside radius plus foot radius is still in reach.");
            Assert.IsFalse(
                GrassFootstep.WalkerCanReach(centre + new Vector2(radius + foot + 1e-2f, 0f), centre, radius, foot),
                "Past radius plus foot radius nothing in the segment can bend this blade.");
            // A standing walker publishes radius 0 (only the head point is live) — that must NOT read as empty.
            Assert.IsTrue(
                GrassFootstep.WalkerCanReach(centre + new Vector2(0.2f, 0f), centre, 0f, foot),
                "A standing walker (radius 0) must still part the grass underfoot.");
        }

        [Test]
        public void TheCull_OnlyEverSkipsPointsWhoseBendIsAlreadyZero()
        {
            // The safety property the whole optimisation rests on: if WalkerCanReach says no, then EVERY live
            // point of that walker is already at or past _FootRadius, where FootstepFalloff is 0. Swept over a
            // deterministic trail and a grid of blades — no randomness, so a failure reproduces exactly.
            const float foot = 0.5f;
            var centre = new Vector2(0f, 0f);

            var pts = new Vector2[GrassFootstep.PointsPerWalker];
            var str = new float[GrassFootstep.PointsPerWalker];
            float radius = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                // a curved trail trailing away from the centre, fading with age like the ring buffer does
                float t = i * 0.25f;
                pts[i] = new Vector2(-t, Mathf.Sin(t) * 0.6f);
                str[i] = GrassFootstep.TrailStrength(t * 0.1f, 1.2f);
                radius = GrassFootstep.GrowRadius(radius, centre, pts[i], str[i]);
            }

            int culled = 0;
            for (int gx = -60; gx <= 60; gx++)
            for (int gy = -60; gy <= 60; gy++)
            {
                var blade = new Vector2(gx * 0.15f, gy * 0.15f);
                if (GrassFootstep.WalkerCanReach(blade, centre, radius, foot)) continue;
                culled++;
                for (int i = 0; i < pts.Length; i++)
                {
                    if (str[i] <= 0f) continue;   // a faded point bends nothing whether it is culled or not
                    float bend = GrassFootstep.FootstepFalloff(Vector2.Distance(blade, pts[i]), foot) * str[i];
                    Assert.AreEqual(
                        0f, bend, Eps,
                        $"The cull dropped a blade at {blade} that live trail point {i} at {pts[i]} would have " +
                        "bent. The bounding circle is too tight — the cull must be invisible, cost only.");
                }
            }

            Assert.Greater(culled, 0, "The sweep must actually exercise culled blades or it proves nothing.");
        }

        // ---- helpers -------------------------------------------------------------------------------------

        private static string ReadShader(string relativePath)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            Assert.IsTrue(
                File.Exists(full),
                $"Could not find '{relativePath}'. A missing/renamed shader leaves the trail-pool size contract " +
                "UNGUARDED — treat it as a failure, not a pass.");
            return File.ReadAllText(full);
        }

        /// <summary>
        /// The shader source with its comments removed, so an assertion about what the shader DOES is not
        /// satisfied (or defeated) by prose in a header block that merely talks about it.
        /// </summary>
        private static string CodeOnly(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(source, @"//[^\n]*", "", RegexOptions.None);
        }

        private static int ReadDefine(string source, string name, string where)
        {
            Match m = Regex.Match(source, @"^\s*#define\s+" + name + @"\s+(\d+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(
                m.Success,
                $"'{where}' has no literal '#define {name} <n>'. The size contract is pinned by parsing that " +
                "line, so keep it a plain integer literal.");
            return int.Parse(m.Groups[1].Value);
        }
    }
}
