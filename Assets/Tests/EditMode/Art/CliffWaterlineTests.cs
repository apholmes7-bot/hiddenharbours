using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
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
    }
}
