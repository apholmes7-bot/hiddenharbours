using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The px twin of <see cref="CliffRigBakeTests"/>: the cliff kit's contract held against
    /// <c>pxCliffFaceRig.js</c> — the rig <b>Bake Cliff Kit — px</b> drives — every run, on CI, with no
    /// committed pixels. The bake is gitignored and CI has none, so, exactly as for v10, the rig is the
    /// committed source and this suite asserts the PROPERTY rather than counting walls.
    ///
    /// <para><see cref="PxCliffFaceKitIntakeTests"/> guards the kit as it LANDED — digests, census, the
    /// shipped samples, the sidecar's palette. This suite guards the WIRING: that
    /// <see cref="CliffCatalog"/>'s px twins, <see cref="CliffBaker"/>'s one-script face call, the LUT it
    /// writes and <see cref="CliffPxRelightMath"/> — the C# twin of the shader's px branch — still
    /// describe what the rig renders.</para>
    ///
    /// <para><b>⭐ The shadow is the claim only pixels can check.</b> The shader has no shadow map for a
    /// wall; it RECOVERS the cast shadow by dividing the bake's own <c>N·L</c> out of <c>_mask.R</c>.
    /// The rig keeps the shadow it cut (<c>face().shd</c>), so the recovery is scored against the truth,
    /// texel by texel, on three faces — through the packed-frame key the shader uses and, as the control,
    /// through the raw rig-frame key it must not use.</para>
    /// </summary>
    public class PxCliffRigBakeTests
    {
        static IRigScriptHost _host;
        const string PG = CliffCatalog.PxRigGlobalName;
        const string PL = CliffCatalog.PxLanguageGlobalName;

        [OneTimeSetUp]
        public void InstallThePxRigOnce()
        {
            _host = RigScriptHostFactory.Create();
            CliffBaker.InstallPxRig(_host);
        }

        [OneTimeTearDown]
        public void DisposeHost()
        {
            _host?.Dispose();
            _host = null;
        }

        // =====================================================================================
        //  the vocabulary
        // =====================================================================================

        [Test]
        public void TheCatalogsVocabularyIsThePxRigs()
        {
            CollectionAssert.AreEqual(CliffCatalog.Rocks, ReadStrings($"{PG}.ROCKS"),
                "CliffCatalog.Rocks is not the px rig's ROCKS, in order. The order is load-bearing twice: " +
                "the rig seeds each rock from its index, and _index puts rock r's six tiers at LUT rows " +
                "6r..6r+5 — reorder it and sandstone relights through till's ramp.");
            CollectionAssert.AreEqual(CliffCatalog.Aspects, ReadStrings($"{PG}.ASPECTS"),
                "CliffCatalog.Aspects is not the px rig's ASPECTS — the baker names every face by them.");
        }

        [Test]
        public void TheBatterAnglesAreThePxRigs()
        {
            Assert.AreEqual(CliffCatalog.Batters.Length, CliffCatalog.BatterAngles.Length);
            for (int i = 0; i < CliffCatalog.Batters.Length; i++)
                Assert.AreEqual(CliffCatalog.BatterAngles[i],
                    (float)_host.EvaluateNumber($"{PG}.SLOPES[{Js(CliffCatalog.Batters[i])}]"), 1e-4f,
                    $"The px rig's '{CliffCatalog.Batters[i]}' batter is not the catalog's. The baker bakes " +
                    "by the NAME and the shader tips the key by the ANGLE, so the two must be one table.");
        }

        // =====================================================================================
        //  the key the mask was lit with
        // =====================================================================================

        /// <summary>
        /// The key the px bake lit <c>_mask</c> with is the one the material hands the shader
        /// (<see cref="CliffCatalog.PxBakeKey"/> → <c>_PxKey</c>, set by the px menu item). The shader
        /// divides the bake's <c>N·L</c> back out of the mask; divide by another key's and the
        /// recovered shadow is noise.
        /// </summary>
        [Test]
        public void ThePxBakeKeyIsTheLanguagesKey()
        {
            AssertClose(CliffCatalog.PxBakeKey, ReadVector($"{PL}.LIGHT.key"), 1e-4f,
                "CliffCatalog.PxBakeKey is not PxLang.LIGHT.key");
        }

        /// <summary>The rig tips its key into a battered face's frame before it lights the mask
        /// (<c>keyFor()</c> after <c>setSlope()</c>); <see cref="CliffPxRelightMath.BakeKeyAt"/> is the
        /// same rotation, which the shader runs with <c>_Batter</c>.</summary>
        [Test]
        public void TheKeyTipsWithTheBatterAsTheRigTipsIt()
        {
            try
            {
                for (int i = 0; i < CliffCatalog.Batters.Length; i++)
                {
                    _host.Execute($"{PG}.setSlope({Js(CliffCatalog.Batters[i])});");
                    AssertClose(CliffPxRelightMath.BakeKeyAt(CliffCatalog.PxBakeKey, CliffCatalog.BatterAngles[i]),
                                ReadVector($"{PG}.keyFor()"), 1e-4f,
                                $"keyFor() at '{CliffCatalog.Batters[i]}' is not BakeKeyAt");
                }
            }
            finally
            {
                _host.Execute($"{PG}.setSlope(null);");
            }
        }

        // =====================================================================================
        //  the canvases and the index
        // =====================================================================================

        [Test]
        public void APxFaceIsTheCanvasAndEveryChannelTheBakerWritesComesBackFull()
        {
            BakeFace("__t", "sandstone", "SE", "wall");
            int w = (int)_host.EvaluateNumber("__t.W"), h = (int)_host.EvaluateNumber("__t.H");

            Assert.AreEqual(CliffCatalog.FaceWidth, w, "A px face is not the canvas the shader tiles.");
            Assert.AreEqual(CliffCatalog.FaceHeight, h);

            foreach (string channel in CliffCatalog.PxLiveChannels)
                Assert.IsTrue(_host.EvaluateBool(
                        $"!!__t.__ch[{Js(channel)}] && __t.__ch[{Js(channel)}].length === {w * h * 4}"),
                    $"Channel '{channel}' did not come back from channels(…, {{index: true}}) as a full " +
                    $"{w}×{h} RGBA buffer. The shader samples all three at the SAME uv.");
        }

        /// <summary>
        /// Every <c>_index</c> texel names a cell the LUT has and the relight can step: a rock texel in
        /// its OWN rock's six rows (the relight moves a tier within <c>row % 6</c>, so a rock texel
        /// outside them would step into a neighbour's ramp), an accessory texel in the rows after the
        /// rocks, a band the LUT has, full alpha.
        /// </summary>
        [TestCase("sandstone", "S", "wall")]
        [TestCase("till", "E", "ramp")]
        [TestCase("basalt", "W", "steep")]
        public void EveryIndexTexelNamesACellTheRelightCanStep(string rock, string aspect, string slope)
        {
            BakeFace("__t", rock, aspect, slope);
            byte[] index = _host.EvaluateBytes("__t.__ch['_index']");

            int tiers = CliffPxRelightMath.TiersPerRock;
            int rock0 = Array.IndexOf(CliffCatalog.Rocks, rock) * tiers;
            int accessory0 = CliffCatalog.Rocks.Length * tiers;
            int bad = 0, rockTexels = 0;
            string first = null;
            for (int o = 0; o < index.Length; o += 4)
            {
                int row = index[o], band = index[o + 1];
                bool isRock = index[o + 2] > 127;
                if (isRock) rockTexels++;
                bool ok = band < CliffCatalog.PxPaletteBands && index[o + 3] == 255 &&
                          (isRock ? row >= rock0 && row < rock0 + tiers
                                  : row >= accessory0 && row < CliffCatalog.PxPaletteRows);
                if (!ok && bad++ == 0)
                    first = $"texel {o / 4}: row {row}, band {band}, B {index[o + 2]}, A {index[o + 3]}";
            }

            Assert.Greater(rockTexels, 0, $"{rock} {aspect} {slope}: no texel is flagged rock (B = 255).");
            Assert.AreEqual(0, bad,
                $"{rock} {aspect} {slope}: {bad} _index texels name a cell the relight cannot step — first " +
                $"{first}. Rock rows for {rock} are {rock0}..{rock0 + tiers - 1}, accessories " +
                $"{accessory0}..{CliffCatalog.PxPaletteRows - 1}, bands 0..{CliffCatalog.PxPaletteBands - 1}.");
        }

        /// <summary>
        /// The LUT's layout is the one the relight's arithmetic assumes: rock r's tier k at row
        /// <c>6r + (k + T0)</c>, the accessories after the rocks, a tier DOWN darker and a band DOWN
        /// darker. The shader and <see cref="CliffPxRelightMath.Relight"/> subtract to shadow — if a
        /// re-drop reversed either ramp, a cast shadow would brighten the rock under it.
        /// </summary>
        [Test]
        public void TheLutIsSixTiersARockDarkestFirstThenTheAccessories()
        {
            int tiers = CliffPxRelightMath.TiersPerRock;
            Assert.AreEqual(tiers, (int)_host.EvaluateNumber($"{PG}.TIERS.length"));
            int t0 = (int)_host.EvaluateNumber($"{PG}.T0");

            _host.Execute($"globalThis.__lut = {PG}.paletteLUT();");
            Assert.AreEqual(CliffCatalog.PxPaletteRows, (int)_host.EvaluateNumber("__lut.length"));

            int rockRows = CliffCatalog.Rocks.Length * tiers;
            for (int r = 0; r < CliffCatalog.PxPaletteRows; r++)
            {
                string kind = _host.EvaluateString($"__lut[{r}].kind");
                if (r >= rockRows)
                {
                    Assert.AreEqual("accessory", kind, $"LUT row {r} should be an accessory.");
                    continue;
                }
                Assert.AreEqual("rock", kind, $"LUT row {r} should be a rock tier.");
                Assert.AreEqual(CliffCatalog.Rocks[r / tiers], _host.EvaluateString($"__lut[{r}].rock"),
                    $"LUT row {r} is not rock {r / tiers}'s.");
                Assert.AreEqual(r % tiers - t0, (int)_host.EvaluateNumber($"__lut[{r}].tier"),
                    $"LUT row {r} is not tier (row % {tiers}) − T0.");
            }

            byte[] lut = CliffBaker.BakePaletteLut(_host);
            for (int r = 0; r < CliffCatalog.PxPaletteRows; r++)
                for (int b = 0; b < CliffCatalog.PxPaletteBands; b++)
                {
                    if (b > 0)
                        Assert.Less(Luma(lut, r, b - 1), Luma(lut, r, b),
                            $"LUT row {r}: band {b - 1} is not darker than band {b}.");
                    if (r < rockRows && r % tiers > 0)
                        Assert.Less(Luma(lut, r - 1, b), Luma(lut, r, b),
                            $"LUT rows {r - 1}→{r}, band {b}: a tier down is not darker.");
                }
        }

        [Test]
        public void ThePxStripsAreTheCatalogsSize()
        {
            foreach (string call in new[]
                     {
                         $"{PG}.brow('S', {CliffCatalog.BaseStep})",
                         $"{PG}.toe('S', {CliffCatalog.BaseStep}, {{}})",
                         $"{PG}.toe('S', {CliffCatalog.BaseStep}, {{feature: 'slump'}})",
                     })
            {
                _host.Execute($"globalThis.__t = {call};");
                Assert.AreEqual(CliffCatalog.StripWidth, (int)_host.EvaluateNumber("__t.W"), call);
                Assert.AreEqual(CliffCatalog.StripHeight, (int)_host.EvaluateNumber("__t.H"), call);
                Assert.IsTrue(_host.EvaluateBool(
                        $"__t.data.length === {CliffCatalog.StripWidth * CliffCatalog.StripHeight * 4}"),
                    $"{call} did not hand back one full straight-alpha image — the baker writes it whole.");
            }
        }

        // =====================================================================================
        //  the shadow — recovered, not supplied
        // =====================================================================================

        /// <summary>
        /// 🔴 <b>The shadow the shader recovers is the rig's.</b> <c>_mask.R</c> is
        /// <c>N·L × (1 − shadow × 0.78)</c> at the bake key; <see cref="CliffPxRelightMath.RecoverShadow"/>
        /// divides the bake's <c>N·L</c> back out and <see cref="CliffPxRelightMath.TierStep"/> turns it into
        /// the tiers the relight drops. Scored here against <c>face().shd</c>, the shadow the rig cut:
        ///
        /// <list type="bullet">
        /// <item><description><b>Agreement ≥ 95%</b> of cells through the PACKED-frame key — and
        /// <b>&lt; 90% through the raw rig-frame key</b>, the control: the rig's key is y-DOWN the face and
        /// <c>_normal.G</c> is packed y-UP, and a test that could not tell the two apart would pass the
        /// wrong frame.</description></item>
        /// <item><description>Agreement alone is weak (a recovery that never finds a shadow scores 98% on
        /// the sandstone south wall), so: <b>≥ 90% of the rig-shadowed cells the guard can see come back
        /// at the rig's step</b>, and <b>&lt; 1% of the clear cells come back shadowed</b>.</description></item>
        /// </list>
        ///
        /// <para>Measured off the rig (V8, 2×2 blocks, one sample each): packed 99.3 / 96.2 / 99.9%, raw
        /// 69.2 / 81.7 / 73.0%; shadowed cells recovered 434 of 440 / ≈2990 of 3036 / 32 of 34; false
        /// shadow 0.0 / 0.1 / 0.0%. Behind the guard (bake <c>N·L</c> ≤ 0.02) the mask is 0 whatever the
        /// shadow, nothing is claimed, and the live band step still darkens those cells.</para>
        /// </summary>
        [TestCase("sandstone", "S", "wall")]
        [TestCase("till", "S", "wall")]
        [TestCase("sandstone", "E", "steep")]
        public void TheShadowComesBackOutOfTheMask(string rock, string aspect, string slope)
        {
            BakeFace("__t", rock, aspect, slope);
            int n = (int)_host.EvaluateNumber("__t.n"), m = (int)_host.EvaluateNumber("__t.m");
            int z = (int)_host.EvaluateNumber("__t.z"), w = (int)_host.EvaluateNumber("__t.W");
            byte[] normal = _host.EvaluateBytes("__t.__ch['_normal']");
            byte[] mask = _host.EvaluateBytes("__t.__ch['_mask']");
            float[] shadow = ReadFloats("__t.shd");
            Assert.AreEqual(n * m, shadow.Length, "face().shd is not one value per cell.");

            float angle = CliffCatalog.BatterAngles[Array.IndexOf(CliffCatalog.Batters, slope)];
            Vector3 rigKey = CliffPxRelightMath.BakeKeyAt(CliffCatalog.PxBakeKey, angle);
            Vector3 packedKey = CliffPxRelightMath.PackedFrame(rigKey);

            int cells = n * m, agree = 0, agreeRaw = 0;
            int shadowed = 0, seen = 0, found = 0, clear = 0, falseShadow = 0;
            for (int y = 0; y < m; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x, o = (y * z * w + x * z) * 4;
                    Vector3 nrm = new Vector3(normal[o] / 255f * 2f - 1f, normal[o + 1] / 255f * 2f - 1f,
                                              normal[o + 2] / 255f * 2f - 1f).normalized;
                    float maskR = mask[o] / 255f, bakeNdl = Vector3.Dot(nrm, packedKey);

                    int truth = CliffPxRelightMath.TierStep(shadow[i]);
                    int got = CliffPxRelightMath.TierStep(CliffPxRelightMath.RecoverShadow(maskR, bakeNdl));
                    int raw = CliffPxRelightMath.TierStep(
                        CliffPxRelightMath.RecoverShadow(maskR, Vector3.Dot(nrm, rigKey)));

                    if (got == truth) agree++;
                    if (raw == truth) agreeRaw++;
                    if (truth > 0)
                    {
                        shadowed++;
                        if (bakeNdl > CliffPxRelightMath.ShadowGuard) { seen++; if (got == truth) found++; }
                    }
                    else
                    {
                        clear++;
                        if (got > 0) falseShadow++;
                    }
                }

            float agreement = agree / (float)cells, rawAgreement = agreeRaw / (float)cells;
            string said =
                $"{rock} {aspect} {slope}: tier agreement {agreement:P1} through the packed key, " +
                $"{rawAgreement:P1} through the raw rig key; {found} of the {seen} rig-shadowed cells the " +
                $"guard can see come back at the rig's step ({shadowed - seen} more sit behind it); " +
                $"{falseShadow} of {clear} clear cells come back shadowed.";

            Assert.GreaterOrEqual(seen, 20, said + " Too little shadow on this face to score the recovery.");
            Assert.GreaterOrEqual(agreement, 0.95f, said);
            Assert.Less(rawAgreement, 0.90f,
                said + " The CONTROL failed: on this face the raw key scores as well as the packed one, so the " +
                "suite can no longer tell the frames apart. Score a face where it can.");
            Assert.GreaterOrEqual(found / (float)seen, 0.90f, said);
            Assert.Less(falseShadow / (float)clear, 0.01f, said);
        }

        [Test]
        public void TheRelightThresholdsAreTheSidecars()
        {
            object root = DeckSidecarJson.Parse(File.ReadAllText(Abs(CliffCatalog.PxSidecarPath)));
            object thresholds = DeckSidecarJson.Member(DeckSidecarJson.Member(root, "LIGHTING"), "thresholds");
            Assert.IsNotNull(thresholds, "The sidecar has no LIGHTING.thresholds.");

            AssertThreshold(thresholds, "band_up", CliffPxRelightMath.BandUp);
            AssertThreshold(thresholds, "band_down", CliffPxRelightMath.BandDown);
            AssertThreshold(thresholds, "shadow_one_tier", CliffPxRelightMath.ShadowOneTier);
            AssertThreshold(thresholds, "shadow_two_tiers", CliffPxRelightMath.ShadowTwoTiers);
        }

        // =====================================================================================
        //  the LUT
        // =====================================================================================

        /// <summary>
        /// What <see cref="CliffBaker.WritePaletteLut"/> writes to the tracked
        /// <see cref="CliffCatalog.PxPalettePath"/> is the kit's own <c>CliffPx_palette.png</c>, pixel for
        /// pixel — rows 25..31 transparent black and columns 5..7 repeating band 4 included. The bake
        /// never commits a different table from the one the art-director drew.
        /// </summary>
        [Test]
        public void TheLutTheBakerWritesIsTheKitsPalettePixelForPixel()
        {
            byte[] lut = CliffBaker.BakePaletteLut(_host);
            Texture2D png = LoadPng(CliffCatalog.PxKitPalettePath);
            try
            {
                Assert.AreEqual(CliffCatalog.PxPaletteWidth, png.width);
                Assert.AreEqual(CliffCatalog.PxPaletteHeight, png.height);
                Color32[] kit = TopDownRgba(png);
                Assert.AreEqual(kit.Length * 4, lut.Length);

                int bad = 0;
                string first = null;
                for (int i = 0; i < kit.Length; i++)
                {
                    Color32 k = kit[i];
                    if (k.r == lut[i * 4] && k.g == lut[i * 4 + 1] && k.b == lut[i * 4 + 2] && k.a == lut[i * 4 + 3])
                        continue;
                    if (bad++ == 0)
                        first = $"row {i / png.width} column {i % png.width}: kit {k}, baked " +
                                $"({lut[i * 4]}, {lut[i * 4 + 1]}, {lut[i * 4 + 2]}, {lut[i * 4 + 3]})";
                }
                Assert.AreEqual(0, bad, $"{bad} LUT texels differ from the kit's palette — first {first}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(png);
            }
        }

        /// <summary>
        /// ⚠ <b>Unity's v = 0 is the PNG's LAST row.</b> Every cell <see cref="CliffPxRelightMath.LutUv"/>
        /// names, resolved the way a point sampler resolves it (floor of uv × size, rows counted from the
        /// bottom), lands at that cell's centre and on the colour the rig gave that (row, band). Get the
        /// orientation backwards and sandstone samples the transparent padding rows.
        /// </summary>
        [Test]
        public void LutUvLandsOnTheCellTheRigColoured()
        {
            Texture2D png = LoadPng(CliffCatalog.PxKitPalettePath);
            try
            {
                int w = png.width, h = png.height;
                Color32[] bottomUp = png.GetPixels32();
                _host.Execute($"globalThis.__lut = {PG}.paletteLUT();");

                for (int row = 0; row < CliffCatalog.PxPaletteRows; row++)
                    for (int band = 0; band < CliffCatalog.PxPaletteBands; band++)
                    {
                        Vector2 uv = CliffPxRelightMath.LutUv(row, band);
                        float fx = uv.x * w, fy = uv.y * h;
                        Assert.AreEqual(0.5f, fx - Mathf.Floor(fx), 1e-4f, $"({row}, {band}): u is not a cell centre.");
                        Assert.AreEqual(0.5f, fy - Mathf.Floor(fy), 1e-4f, $"({row}, {band}): v is not a cell centre.");

                        Color32 sampled = bottomUp[(int)fy * w + (int)fx];
                        Vector3 rig = ReadVector($"{PL}.h2r(__lut[{row}].bands[{band}])");
                        Assert.AreEqual(new Color32((byte)rig.x, (byte)rig.y, (byte)rig.z, 255), sampled,
                            $"LutUv({row}, {band}) lands on texel ({(int)fx}, {(int)fy} from the bottom), " +
                            "which is not the colour the rig gave that cell.");
                    }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(png);
            }
        }

        // =====================================================================================
        //  the relight
        // =====================================================================================

        /// <summary>
        /// With the day/night cycle running and the sun at or below the horizon there is no direct light:
        /// <c>N·L</c> is 0 and every wall takes one band down — a wall that faces the sun's bearing does not
        /// light up at midnight. With the cycle off (edit mode) the key stands in for the sun.
        /// </summary>
        [Test]
        public void WithTheSunDownEveryWallTakesABandDown()
        {
            Vector3 facing = Vector3.forward;

            Assert.AreEqual(0f, CliffPxRelightMath.LiveNdl(facing, facing, true, -0.2f), "sun below the horizon");
            Assert.AreEqual(0f, CliffPxRelightMath.LiveNdl(facing, facing, true, 0f), "sun on the horizon");
            Assert.AreEqual(1f, CliffPxRelightMath.LiveNdl(facing, facing, true, 0.3f), 1e-6f, "sun up");
            Assert.AreEqual(1f, CliffPxRelightMath.LiveNdl(facing, facing, false, -0.2f), 1e-6f,
                "no cycle: the elevation is not read");

            CliffPxRelightMath.Relight(2, 2, true, CliffPxRelightMath.LiveNdl(facing, facing, true, -0.2f), 0f,
                                       out int row, out int band);
            Assert.AreEqual(2, row, "the sun going down is a band step, never a tier step");
            Assert.AreEqual(1, band);
        }

        // (row, band, rock?, live N·L, recovered shadow) → the LUT cell. Rows 0..5 are sandstone's tiers,
        // 6..11 till's; 18..24 the accessories.
        [TestCase(2, 2, true, 0.80f, 0.00f, 2, 3)]   // lit face: one band up
        [TestCase(2, 2, true, 0.50f, 0.00f, 2, 2)]   // between the thresholds: as baked
        [TestCase(2, 2, true, 0.30f, 0.00f, 2, 1)]   // turned from the sun: one band down
        [TestCase(3, 2, true, 0.50f, 0.50f, 2, 2)]   // shadow over 0.45: one tier down
        [TestCase(3, 2, true, 0.50f, 0.90f, 1, 2)]   // shadow over 0.85: two tiers down
        [TestCase(7, 0, true, 0.30f, 0.90f, 6, 0)]   // till's tier −2 two down clamps at till's darkest, never sandstone's
        [TestCase(11, 4, true, 0.90f, 0.00f, 11, 4)] // band 4 is the top
        [TestCase(20, 2, false, 0.30f, 0.90f, 20, 1)] // an accessory takes the band step and never a tier step
        public void TheRelightStepsTheIndex(int row, int band, bool rock, float ndl, float shadow,
                                           int wantRow, int wantBand)
        {
            CliffPxRelightMath.Relight(row, band, rock, ndl, shadow, out int litRow, out int litBand);
            Assert.AreEqual(wantRow, litRow, "row");
            Assert.AreEqual(wantBand, litBand, "band");
        }

        // =====================================================================================
        //  reproducibility
        // =====================================================================================

        /// <summary>
        /// 🔴 Same rig plus same config ⇒ same pixels, through the px call too: the bake is gitignored,
        /// so a px coast that baked differently on two machines would be two coasts. Unrelated bakes sit
        /// between the two renders because <c>channels()</c> lights with the batter the last
        /// <c>face()</c> set — the reason the baker runs them as ONE script.
        /// </summary>
        [Test]
        public void TheSamePxFaceBakesToTheSameBytesTwice()
        {
            BakeFace("__r1", "sandstone", "SE", "wall");

            BakeFace("__u", "till", "W", "bank");
            _host.Execute($"{PG}.profile('basalt', 'S', {{slope: 48}});");
            _host.Execute($"{PG}.brow('E', 0);");

            BakeFace("__r2", "sandstone", "SE", "wall");

            foreach (string channel in CliffCatalog.PxLiveChannels)
                Assert.IsTrue(_host.EvaluateBool(
                        $"(function(a, b) {{ if (a.length !== b.length) return false;" +
                        $"  for (var i = 0; i < a.length; i++) if (a[i] !== b[i]) return false;" +
                        $"  return true; }})(__r1.__ch[{Js(channel)}], __r2.__ch[{Js(channel)}])"),
                    $"Channel '{channel}' differed between two px bakes of the SAME face, separated by " +
                    "unrelated bakes. The px kit ships as the rig; that is only honest while its bake is " +
                    "a pure function of (rig, config).");
        }

        // =====================================================================================
        //  helpers
        // =====================================================================================

        /// <summary>The baker's px face call, copied rather than reached for: ONE script, face then
        /// <c>channels(…, {index: true})</c>, so the channels light with the batter this face set.</summary>
        static void BakeFace(string global, string rock, string aspect, string slope) =>
            _host.Execute($"globalThis.{global} = {PG}.face({Js(rock)}, {Js(aspect)}, " +
                          $"{CliffCatalog.BaseStep}, {{slope: {Js(slope)}}}); " +
                          $"globalThis.{global}.__ch = {PG}.channels(globalThis.{global}, {{index: true}});");

        string[] ReadStrings(string expression) =>
            ShorePlantBaker.ReadStringArray(_host, expression);

        static Vector3 ReadVector(string expression)
        {
            _host.Execute($"globalThis.__v = Array.from({expression});");
            return new Vector3((float)_host.EvaluateNumber("__v[0]"), (float)_host.EvaluateNumber("__v[1]"),
                               (float)_host.EvaluateNumber("__v[2]"));
        }

        static float[] ReadFloats(string expression)
        {
            byte[] bytes = _host.EvaluateBytes($"new Uint8Array(Float32Array.from({expression}).buffer)");
            var f = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, f, 0, bytes.Length);
            return f;
        }

        static void AssertClose(Vector3 want, Vector3 got, float tolerance, string what)
        {
            for (int k = 0; k < 3; k++)
                Assert.AreEqual(want[k], got[k], tolerance, $"{what}: want {want:F4}, got {got:F4}.");
        }

        static void AssertThreshold(object thresholds, string key, float want)
        {
            Assert.IsTrue(DeckSidecarJson.TryDouble(DeckSidecarJson.Member(thresholds, key), out double d),
                $"LIGHTING.thresholds.{key} is missing from the sidecar.");
            Assert.AreEqual(want, (float)d, 1e-6f,
                $"CliffPxRelightMath's {key} is not the sidecar's — the shader's CLIFF_PX_* and the C# twin " +
                "must both follow the kit.");
        }

        /// <summary>Rec. 709 luma of a LUT cell, from the baker's top-row-first bytes.</summary>
        static float Luma(byte[] lut, int row, int band)
        {
            int i = (row * CliffCatalog.PxPaletteWidth + band) * 4;
            return 0.2126f * lut[i] + 0.7152f * lut[i + 1] + 0.0722f * lut[i + 2];
        }

        static string Abs(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static Texture2D LoadPng(string repoRelative)
        {
            string full = Abs(repoRelative);
            Assert.IsTrue(File.Exists(full), "art present on disk: " + repoRelative);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(full)),
                "PNG decodes: " + repoRelative + " (an LFS pointer that was never smudged decodes as nothing).");
            return tex;
        }

        /// <summary>A decoded PNG is bottom-origin; the rig's buffers are top-origin. The same flip the
        /// intake suite performs, duplicated rather than borrowed.</summary>
        static Color32[] TopDownRgba(Texture2D tex)
        {
            Color32[] bottomUp = tex.GetPixels32();
            var topDown = new Color32[bottomUp.Length];
            int w = tex.width, h = tex.height;
            for (int y = 0; y < h; y++)
                Array.Copy(bottomUp, (h - 1 - y) * w, topDown, y * w, w);
            return topDown;
        }

        static string Js(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }
}
