using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards for the CLIFF WATERLINE (owner ask 2026-08-06: *"water should RISE against the cliff
    /// faces"*). #439 stood the coast up and the sea knew nothing about it — a cliff plunging into deep
    /// water was drawn dry rock all the way down, with the water plane simply passing behind it.
    ///
    /// <para><b>What these tests actually protect.</b> Not the look — the owner's eyeball owns that, and
    /// CI has no graphics device to see it with. They protect the ONE rule that makes a second surface
    /// safe to draw a waterline at all: <b>the cliff does not get its own sea</b>. The tide it uses is the
    /// one <c>WaterSurface</c> published, the surge is the shared wave field through the DRAWN sea's own
    /// frequency scale, and an unpublished global reads as "there is no sea here" instead of as chart
    /// datum. That last one is the loud failure: a silent global taken at face value would drown every
    /// cliff in the game to its brow in any scene without water.</para>
    /// </summary>
    public class CliffWaterlineTests
    {
        private const string CliffShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursCliffFace.shader";
        private const string WaterSurfacePath = "Assets/_Project/Code/Art/WaterSurface.cs";
        private const string WallSurfacePath = "Assets/_Project/Code/Art/CliffWallSurface.cs";

        private static string ReadRepoText(string repoRelative)
        {
            string path = Path.Combine(Application.dataPath, "..", repoRelative);
            Assert.IsTrue(File.Exists(path), $"missing: {repoRelative}");
            return File.ReadAllText(path);
        }

        // ==== the face's own geometry, in elevation ==================================================

        [Test]
        public void Elevation_WalksLinearlyFromTheBrowToTheToe()
        {
            const float toe = -3.5f, drop = 8f;
            float brow = toe + drop;
            Assert.AreEqual(brow, CliffWaterlineMath.ElevationAt(brow, drop, 0f), 1e-5f, "t=0 is the brow.");
            Assert.AreEqual(toe, CliffWaterlineMath.ElevationAt(brow, drop, 1f), 1e-5f, "t=1 is the toe.");
            Assert.AreEqual(brow - drop * 0.5f, CliffWaterlineMath.ElevationAt(brow, drop, 0.5f), 1e-5f);
            // Out of range clamps rather than extrapolating a wall that is not there.
            Assert.AreEqual(brow, CliffWaterlineMath.ElevationAt(brow, drop, -1f), 1e-5f);
            Assert.AreEqual(toe, CliffWaterlineMath.ElevationAt(brow, drop, 2f), 1e-5f);
        }

        // ==== the waterline rides the tide ===========================================================

        [Test]
        public void TheWaterline_RidesTheTide()
        {
            // The owner's first half: the line has to MOVE with the tide, not sit at a painted height.
            float low = CliffWaterlineMath.SeaElevation(-1.2f, 0f, 1f, 1f);
            float high = CliffWaterlineMath.SeaElevation(1.8f, 0f, 1f, 1f);
            Assert.Greater(high, low, "the waterline must climb with the flood.");
            Assert.AreEqual(3.0f, high - low, 1e-5f, "and by exactly the tide's own range, unscaled.");

            // A face whose toe is at −3.5 m and brow at +4.5 m: the crossing must travel down the rock as
            // the tide ebbs, which is the whole visible effect.
            const float toe = -3.5f, drop = 8f;
            float brow = toe + drop;
            float wetAtHigh = CliffWaterlineMath.SubmergenceMetres(
                CliffWaterlineMath.ElevationAt(brow, drop, 0.9f), high);
            float wetAtLow = CliffWaterlineMath.SubmergenceMetres(
                CliffWaterlineMath.ElevationAt(brow, drop, 0.9f), low);
            Assert.Greater(wetAtHigh, wetAtLow, "the same patch of rock is deeper at high water.");
        }

        [Test]
        public void TheWaterline_RidesTheWaveField_AndItIsTheSHAREDField()
        {
            // The owner's second half: the line surges with the sea. And it must surge with the SAME sea
            // — this asserts the surge IS the shared field's height, not a lookalike, by evaluating the
            // published transcription the hull clamp also uses.
            var a = new WaveTrain(new Vector2(1f, 0f), 9f, 0.55f, 0.3f, 9.81f);
            var b = new WaveTrain(new Vector2(0f, 1f), 14f, 0.30f, 2.1f, 9.81f);
            var trains = new WaveTrains(a, b, default, default, 2, 1.6f);
            PackedWaveField field = WaveFieldBridge.Pack(in trains);

            bool sawPositive = false, sawNegative = false;
            for (int i = 0; i < 60; i++)
            {
                var p = new Vector2(i * 0.37f - 11f, i * 0.21f - 6f);
                float surge = CliffWaterlineMath.SurgeMetres(p, in field, 2.8f);
                Assert.AreEqual(WaveFieldBridge.ShaderTwinSample(p, in field, 2.8f).Height, surge, 1e-6f,
                    "the cliff's surge must BE the shared field's height, not a second model of it.");
                if (surge > 0.01f) sawPositive = true;
                if (surge < -0.01f) sawNegative = true;
            }
            Assert.IsTrue(sawPositive && sawNegative,
                "the surge must run both ways along the shore — a waterline that only ever rises is " +
                "not riding a wave.");
        }

        [Test]
        public void TheFrequencyScale_IsBorrowed_NotAssumedToBeOne()
        {
            // ⚠️ The _OceanSwellScale defect class, restated as a test: the drawn sea runs the field at
            // ~2.8x on the owner's materials, and a consumer that evaluated at 1 would surge at
            // wavelengths that are not on screen. Different scale MUST mean a different surge.
            var train = new WaveTrain(new Vector2(1f, 0f), 9f, 0.55f, 0.3f, 9.81f);
            var trains = new WaveTrains(train, default, default, default, 1, 1.6f);
            PackedWaveField field = WaveFieldBridge.Pack(in trains);

            var p = new Vector2(3.5f, 0f);
            float atOne = CliffWaterlineMath.SurgeMetres(p, in field, 1f);
            float atDrawn = CliffWaterlineMath.SurgeMetres(p, in field, 2.8f);
            Assert.Greater(Mathf.Abs(atOne - atDrawn), 1e-3f,
                "if the scale did not matter here, nothing would stop the cliff surging at the sim's " +
                "wavelengths while the sea drew at the DRAWN ones.");
        }

        [Test]
        public void TheExaggeration_IsBorrowedToo_SoTheRockMeetsTheDrawnCrest()
        {
            float plain = CliffWaterlineMath.SeaElevation(0f, 0.4f, 1f, 1f);
            float displaced = CliffWaterlineMath.SeaElevation(0f, 0.4f, 1.5f, 1f);
            Assert.AreEqual(0.4f, plain, 1e-5f);
            Assert.AreEqual(0.6f, displaced, 1e-5f,
                "the displaced sea lifts its own crests by x1.5; the rock must meet THAT crest.");

            // …and the gain is a separate dial that can silence the surge without touching the tide.
            Assert.AreEqual(0f, CliffWaterlineMath.SeaElevation(0f, 0.4f, 1.5f, 0f), 1e-5f);
            Assert.AreEqual(2.2f, CliffWaterlineMath.SeaElevation(2.2f, 0.4f, 1.5f, 0f), 1e-5f,
                "gain 0 stills the surge and leaves the tide exactly where it was.");
        }

        // ==== the bands ==============================================================================

        [Test]
        public void Submerged_IsZeroAboveTheLine_AndSaturatesWithDepth()
        {
            Assert.AreEqual(0f, CliffWaterlineMath.Submerged01(-0.5f, 2f), 1e-6f, "dry rock stays dry.");
            Assert.AreEqual(0f, CliffWaterlineMath.Submerged01(0f, 2f), 1e-6f, "and so does the line itself.");
            Assert.AreEqual(0.5f, CliffWaterlineMath.Submerged01(1f, 2f), 1e-5f);
            Assert.AreEqual(1f, CliffWaterlineMath.Submerged01(9f, 2f), 1e-6f, "deep is fully drowned.");

            float previous = -1f;
            for (int i = 0; i <= 40; i++)
            {
                float v = CliffWaterlineMath.Submerged01(i * 0.2f, 2f);
                Assert.GreaterOrEqual(v, previous - 1e-6f, "deeper rock can never read drier.");
                previous = v;
            }
        }

        [Test]
        public void DampBand_IsCentredOnTheLine_Symmetric_AndDiesAtItsEdges()
        {
            const float band = 0.6f;
            Assert.AreEqual(1f, CliffWaterlineMath.DampBand01(0f, band), 1e-5f, "peak AT the waterline.");
            Assert.AreEqual(CliffWaterlineMath.DampBand01(0.25f, band),
                            CliffWaterlineMath.DampBand01(-0.25f, band), 1e-6f,
                "rock just above the line is wet from the last wave; rock just below is still worked.");
            Assert.AreEqual(0f, CliffWaterlineMath.DampBand01(band, band), 1e-6f);
            Assert.AreEqual(0f, CliffWaterlineMath.DampBand01(-band * 2f, band), 1e-6f);
            Assert.AreEqual(0f, CliffWaterlineMath.DampBand01(0f, 0f), 1e-6f, "a zero band is an off switch.");
        }

        [Test]
        public void FoamCollar_SitsBelowTheLine_WhereTheWaterIs()
        {
            const float collar = 0.2f;
            float below = CliffWaterlineMath.FoamCollar01(collar * 0.45f, collar, 0.45f);
            float above = CliffWaterlineMath.FoamCollar01(-collar * 0.45f, collar, 0.45f);
            Assert.Greater(below, above,
                "foam gathers on the WATER side of the line, not up the dry face.");
            Assert.AreEqual(0f, CliffWaterlineMath.FoamCollar01(0f, 0f, 0.45f), 1e-6f, "off switch.");
            // …and it is tighter than the damp band, so the two read as a strip with a lit edge.
            Assert.Less(collar, 0.6f);
        }

        // ==== the unset global: the loudest possible failure =========================================

        [Test]
        public void AnUnpublishedSea_ReadsAsNoSea_NotAsChartDatum()
        {
            Assert.IsFalse(CliffWaterlineMath.HasPublishedSea(Vector4.zero),
                "an all-zero global is UNSET. Taken at face value it would put the sea at chart datum " +
                "and drown every cliff in every scene that has no water surface.");
            Assert.IsTrue(CliffWaterlineMath.HasPublishedSea(new Vector4(-1.2f, 2.8f, 1.5f, 1f)),
                "a published sea is flagged in w, even when the tide itself happens to be 0.");
            Assert.IsTrue(CliffWaterlineMath.HasPublishedSea(new Vector4(0f, 1f, 1f, 1f)));
        }

        // ==== source tripwires: the seam is only closed if all three ends carry it ====================

        [Test]
        public void WaterSurface_IsTheOnePublisher_AndPublishesTheUnsetStateToo()
        {
            string src = ReadRepoText(WaterSurfacePath);
            StringAssert.Contains("_HHSeaLevelWorld", src, "the waterline global must be published here.");
            StringAssert.Contains("PublishSeaLevelUnset", src,
                "a stopped play session must leave the waterline SILENT, not frozen on the last tide.");
            StringAssert.Contains("DisplacedSea.TryGet", src,
                "the frequency scale and the exaggeration must be BORROWED from the one seam that " +
                "carries them, never re-declared here.");
        }

        [Test]
        public void CliffShader_ReadsThePublishedSea_AndGatesOnTheUnsetFlag()
        {
            string src = ReadRepoText(CliffShaderPath);
            StringAssert.Contains("float4 _HHSeaLevelWorld;", src);
            StringAssert.Contains("_HHSeaLevelWorld.w > 0.5", src,
                "the shader MUST refuse to draw a waterline off an unset global.");
            StringAssert.Contains("_WaterlineStrength > 0.001", src,
                "and strength 0 must be an exact passthrough.");
            StringAssert.Contains("IN.waterline.z > 0.5", src,
                "and a wall with no authored elevation must be skipped, not treated as chart datum.");
            StringAssert.Contains("CliffWaveHeight(IN.uv2, _HHSeaLevelWorld.y)", src,
                "the surge is sampled at the TOE PLAN (uv2) through the DRAWN sea's frequency scale — " +
                "the drawn toe has already been pushed down-screen and is the wrong place to ask.");

            // The magenta trap this shader's own header names: a fixed loop bound with the live count
            // masking inside it, never [unroll] over a runtime bound.
            StringAssert.Contains("for (int i = 0; i < CLIFF_WAVE_MAX_TRAINS; i++)", src);
            StringAssert.DoesNotContain("i < count; i++", src,
                "[unroll] over a runtime bound is a known magenta trap in this project.");
        }

        [Test]
        public void CliffWall_WritesTheElevationAndSeaPlanChannels()
        {
            string src = ReadRepoText(WallSurfacePath);
            StringAssert.Contains("mesh.SetUVs(1, elevationUv)", src);
            StringAssert.Contains("mesh.SetUVs(2, seaPlanUv)", src);
            StringAssert.Contains("_toeElevations", src,
                "the ABSOLUTE toe elevation is what the waterline is measured against; a drop is only " +
                "a difference and cannot say where the crossing falls.");
            // BOTH the rock bands and the decals carry the channels: the TOE strip is the piece the sea
            // actually reaches, and a decal drawn dry across a wet foot is the seam this item closes.
            Assert.AreEqual(2, System.Text.RegularExpressions.Regex.Matches(src, @"seaPlanUv\[idx\] = ").Count,
                "the face bands AND the decals must both carry the waterline channels.");
        }

        [Test]
        public void AWallBuiltBeforeTheWaterline_DrawsExactlyAsItShipped()
        {
            // The scenes are in the repo (#432) and carry the arrays the builder wrote. Until the owner
            // re-runs the region builder those have no toe elevations, and a wall that INVENTED a datum
            // would not merely be inert — at any flood tide it would read as metres under water and draw
            // the whole cliff drowned. So the mesh carries a FLAG beside the elevation and the shader
            // gates on it; asserted at source, because CI has no graphics device to see a drowned wall.
            string src = ReadRepoText(WallSurfacePath);
            StringAssert.Contains("bool haveElevation = toeElevation != null && toeElevation.Length >= n",
                src, "an un-rebuilt scene must fall back, not guess.");
            StringAssert.Contains("float elevationValid = HasToeElevations ? 1f : 0f", src,
                "the fallback must be SAID in the mesh, not implied by a zero that looks like datum.");
            StringAssert.Contains("elevationValid);", src);
        }

        // ==== the px look (owner, 09-18: "day only for now") =========================================

        /// <summary>
        /// The px look replaces the LIGHT and nothing else, so it meets the sea exactly as v10 does: the
        /// waterline tints whatever <c>lit</c> the look branch left, and it has to stay outside that
        /// branch. Moved inside the <c>#else</c> it would be v10's alone, and a px wall would draw dry
        /// rock into a flood tide with nothing red anywhere — CI has no graphics device to see it.
        /// </summary>
        [Test]
        public void ThePxLook_MeetsTheSameWaterline()
        {
            string src = ReadRepoText(CliffShaderPath);
            StringAssert.Contains($"#pragma shader_feature_local _ {CliffCatalog.PxKeyword}", src,
                "the px look is the material keyword the bake menu sets; with no pragma declaring it, a " +
                "px bake would leave every wall drawing v10's lighting over palette indices.");

            string pxIf = $"#if defined({CliffCatalog.PxKeyword})";
            int branch = Find(src, pxIf);
            Assert.GreaterOrEqual(branch, 0, "the fragment has no px branch.");
            Assert.AreEqual(branch, src.LastIndexOf(pxIf, System.StringComparison.Ordinal),
                "the px look must be ONE branch of the fragment.");
            int orV10 = Find(src, "#else", branch);
            int end = Find(src, "#endif", branch);
            Assert.Greater(orV10, branch, "the px branch has no v10 side.");
            Assert.Greater(end, orV10, "the px branch never closes after its v10 side.");

            int relight = Find(src, "lit = CliffPxRelight(", branch);
            Assert.IsTrue(relight > branch && relight < orV10,
                "the palette relight must be the px side of the branch.");

            const string gate = "_WaterlineStrength > 0.001";
            int waterline = Find(src, gate);
            Assert.AreEqual(waterline, src.LastIndexOf(gate, System.StringComparison.Ordinal),
                "ONE waterline, shared by both looks — a second copy is a second sea.");
            Assert.Greater(waterline, end,
                "the waterline must come AFTER the look branch closes, so it tints whichever look ran.");
        }

        /// <summary>
        /// ⭐ The px branch IS the arithmetic <see cref="CliffPxRelightMath"/> runs, and that twin is what
        /// <c>PxCliffRigBakeTests</c> drives over the rig's own faces, because CI cannot run a fragment
        /// shader. So every number and every step the two share is pinned equal HERE. A threshold edited
        /// on one side only would leave every test green and the coast stepping at a light nobody priced.
        /// </summary>
        [Test]
        public void ThePxBranch_IsTheTwinTheTestsDrive()
        {
            string src = ReadRepoText(CliffShaderPath);

            AssertDefine(src, "CLIFF_PX_BAND_UP", CliffPxRelightMath.BandUp);
            AssertDefine(src, "CLIFF_PX_BAND_DOWN", CliffPxRelightMath.BandDown);
            AssertDefine(src, "CLIFF_PX_SHADOW_ONE", CliffPxRelightMath.ShadowOneTier);
            AssertDefine(src, "CLIFF_PX_SHADOW_TWO", CliffPxRelightMath.ShadowTwoTiers);
            AssertDefine(src, "CLIFF_PX_SHADOW_DEPTH", CliffPxRelightMath.ShadowDepth);
            AssertDefine(src, "CLIFF_PX_SHADOW_GUARD", CliffPxRelightMath.ShadowGuard);
            AssertDefine(src, "CLIFF_PX_TIERS", CliffPxRelightMath.TiersPerRock);
            AssertDefine(src, "CLIFF_PX_BANDS", CliffCatalog.PxPaletteBands);
            AssertDefine(src, "CLIFF_PX_LUT_W", CliffCatalog.PxPaletteWidth);
            AssertDefine(src, "CLIFF_PX_LUT_H", CliffCatalog.PxPaletteHeight);

            // The key the mask was baked at. The px bake writes the catalog's onto the shipped material;
            // a fresh material reads this default, so the two must be the same light.
            Match key = Regex.Match(src,
                @"_PxKey\s*\(""[^""]*"",\s*Vector\)\s*=\s*\(([^,]+),([^,]+),([^,]+),[^)]+\)");
            Assert.IsTrue(key.Success, "the shader declares no _PxKey default.");
            var declared = new Vector3(Num(key.Groups[1].Value), Num(key.Groups[2].Value),
                                       Num(key.Groups[3].Value));
            Assert.Less((declared - CliffCatalog.PxBakeKey).magnitude, 1e-3f,
                $"_PxKey defaults to {declared} and the px bake wrote its mask at {CliffCatalog.PxBakeKey}: " +
                "a fresh material would divide the wrong N dot L out of the mask and shadow the wrong texels.");
            StringAssert.Contains("normalize(_PxKey.xyz)", src);

            // …tipped by the batter as the rig tips it, and flipped into the frame _Normal is packed in.
            Match batter = Regex.Match(src, @"radians\(90\.0 - clamp\(_Batter,\s*([-0-9.]+),\s*([-0-9.]+)\)\)");
            Assert.IsTrue(batter.Success, "the px bake key is not tipped by the wall's batter.");
            Assert.AreEqual(CliffPxRelightMath.MinBatter, Num(batter.Groups[1].Value), "the batter clamp's floor");
            Assert.AreEqual(CliffPxRelightMath.MaxBatter, Num(batter.Groups[2].Value), "the batter clamp's ceiling");
            StringAssert.Contains("return float3(k.x, -(k.y * ct + k.z * st), k.z * ct - k.y * st);", src,
                "PackedFrame(BakeKeyAt(key, batter)): y negated, because the rig's key runs DOWN the face " +
                "and the normal it packs runs UP it. Unflipped, the recovered shadow agrees with the rig's " +
                "on only 69–82% of cells.");

            // The shadow, recovered by dividing the bake's own N dot L out of mask.R, guarded near zero;
            // and the LUT's v counted DOWN from the top, as LutUv counts it.
            StringAssert.Contains("bakeNdl > CLIFF_PX_SHADOW_GUARD", src);
            StringAssert.Contains("saturate((1.0 - saturate(maskR / bakeNdl)) / CLIFF_PX_SHADOW_DEPTH)", src);
            StringAssert.Contains("1.0 - (row + 0.5) / CLIFF_PX_LUT_H", src);

            // The fragment's px side: the live light, the sun-down gate, the relight at the px key.
            int branch = Find(src, $"#if defined({CliffCatalog.PxKeyword})");
            int orV10 = branch < 0 ? -1 : Find(src, "#else", branch);
            Assert.IsTrue(branch >= 0 && orV10 > branch, "the fragment has no px branch.");
            string px = src.Substring(branch, orV10 - branch);
            Assert.IsTrue(Regex.IsMatch(px,
                    @"ndl\s*=\s*\(cycleOn\s*&&\s*e\s*<=\s*0\.0\)\s*\?\s*0\.0\s*:\s*dot\(N,\s*L\)"),
                "the sun-down gate (LiveNdl): with the cycle on and the sun at or below the horizon, the " +
                "live N dot L is 0 — one band down everywhere, and no wall lit from under the planet.");
            StringAssert.Contains("CliffPxRelight(unlit, ndl, dot(N, Lpx), msk.r)", px,
                "the shadow comes out of the mask at the PX key, the light the mask was baked at.");
            StringAssert.DoesNotContain("_BakeL", px,
                "_BakeL is the v10 kit's per-aspect key; the px mask was never baked at it.");
            StringAssert.Contains("if (_PxDecal < 0.5)", px,
                "a px decal is the rig's own pre-lit strip and is drawn as baked, not relit.");

            // …and the wall is what tells the shader which pieces are decals.
            string wall = ReadRepoText(WallSurfacePath);
            StringAssert.Contains("Shader.PropertyToID(\"_PxDecal\")", wall);
            StringAssert.Contains("_mpb.SetFloat(IdPxDecal, decal ? 1f : 0f)", wall,
                "every renderer's block SAYS whether it is a decal; a px decal relit as a band would read " +
                "its pre-lit colours as palette indices.");
            StringAssert.Contains("decal: true", wall, "the brow and toe strips are the pieces flagged.");
        }

        private static int Find(string src, string what, int from = 0) =>
            src.IndexOf(what, from, System.StringComparison.Ordinal);

        private static float Num(string s) => float.Parse(s.Trim(), CultureInfo.InvariantCulture);

        private static void AssertDefine(string src, string name, float twin)
        {
            Match m = Regex.Match(src, @"#define\s+" + name + @"\s+([-0-9.]+)");
            Assert.IsTrue(m.Success, $"the shader has no #define {name}.");
            Assert.AreEqual(twin, Num(m.Groups[1].Value), 1e-6f,
                $"{name} is {m.Groups[1].Value} in the shader and {twin} in its C# twin — the tests " +
                "would be pricing a relight the wall does not run.");
        }
    }
}
