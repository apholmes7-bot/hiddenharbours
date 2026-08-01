using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guards for the 2026-08-01 band de-regularity work — the owner's *"the large white bands
    /// are too regular like a pattern"*.
    ///
    /// <para><b>Why the bands ARE a pattern.</b> The wave field is ≤4 sine trains sharing one downwind axis
    /// at FIXED angular offsets and FIXED wavelength ratios: a deterministic quasi-periodic interference
    /// whose envelope repeats at evenly spaced diagonals. Four separate features — envelope value bands,
    /// the swell read, rolling-swell brightness and face shading — all read that same envelope and
    /// reinforce into ONE band set, and the only randomization anywhere in the chain is edge Bayer. #382
    /// raised the band strength to 0.35, which is what made the ruler legible rather than what created it.
    ///
    /// <para><b>The field is deliberately not touched</b> — the owner locked that spectrum and the hulls
    /// ride it, so SEE==FEEL forbids editing the sea to fix a look. De-regularizing happens entirely at the
    /// cosmetic READ: bands surface in PATCHES instead of wall-to-wall, and their boundaries WANDER instead
    /// of running as parallel rulers.</para>
    ///
    /// <para>Plus the second, quieter source of regularity: the capillary ripple band was being SAMPLED
    /// below the shader's own stated moiré floor, because its wavelength had been derived against the
    /// wrong pixel grid.</para>
    /// </summary>
    public class WaterBandRegularityTests
    {
        private const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const string LiveWaterMatPath = "Assets/_Project/Art/Materials/Water.mat";
        private const string PresetsFolder = "Assets/_Project/Art/Materials/WaterPresets";

        private static readonly string[] PresetNames =
        {
            "Water_DeepBlue", "Water_FoggySmother", "Water_GlassyCalm", "Water_NorthAtlantic",
            "Water_StirredBrown", "Water_StormGrey", "Water_Tropical", "Water_WarmShelter",
        };

        private static readonly string[] BandKeys =
        {
            "_EnvelopeBandPatchScale", "_EnvelopeBandPatchMin", "_EnvelopeBandWarp", "_RippleWavelength",
            "_PixelsPerUnit",
        };

        private static float MatFloat(string matPath, string key)
        {
            Assert.IsTrue(File.Exists(matPath), $"No material at '{matPath}'.");
            var m = Regex.Match(File.ReadAllText(matPath), $@"-\s{Regex.Escape(key)}:\s*(-?[\d.eE+]+)");
            Assert.IsTrue(m.Success, $"'{matPath}' does not serialize {key}.");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        // ==== the ripple was sampled under the shader's own moiré floor ============================

        [Test]
        public void TheRippleBand_IsSampledAboveTheMoireFloor_AtTheShippedPixelGrid()
        {
            // ⚠️ THE BUG THIS PINS, and it is a units bug rather than a taste one. The shader quantizes
            // world coordinates to _PixelsPerUnit cells before sampling any band. The ripple wavelength had
            // been chosen against the CAMERA's assetsPPU (32) instead — a different grid entirely — giving
            // "0.12 m = 3.84 cells, comfortably past the ~4 floor". At the real _PixelsPerUnit 24 it was
            // 2.88 cells: UNDER the shader's own stated floor. An under-sampled sine beating against a
            // regular grid is moiré, and moiré reads as exactly the regular striping the owner reported.
            const float moireFloorCells = 4f;
            float ppu = MatFloat(LiveWaterMatPath, "_PixelsPerUnit");
            float lambda = MatFloat(LiveWaterMatPath, "_RippleWavelength");
            float cellMetres = 1f / ppu;
            float cellsPerCycle = lambda / cellMetres;

            Assert.GreaterOrEqual(cellsPerCycle, moireFloorCells,
                $"the ripple resolves at {cellsPerCycle:0.00} cells per cycle on the shipped _PixelsPerUnit " +
                $"{ppu} grid ({cellMetres * 100f:0.0} cm cells) — under the shader's own ≥{moireFloorCells} " +
                "floor, which is moiré, not texture");

            // …and it must still be a RIPPLE. Clearing the floor by making the band a metre wide would be a
            // second chop octave, not the finest band on the sea.
            Assert.Less(lambda, 0.25f,
                $"a {lambda * 100f:0} cm 'ripple' is no longer the finest band — it is chop. Clear the " +
                "moiré floor by raising the sample grid, not by coarsening the band out of existence.");
        }

        [Test]
        public void TheOldRippleWavelength_WouldStillFailTheFloor_TheSabotageCheck()
        {
            // Prove the assertion above can actually SEE the defect it exists to pin — the discipline this
            // shader's acceptance suites hold, and one of them is currently failing precisely because its
            // own sabotage check no longer trips.
            const float oldLambda = 0.12f;
            float ppu = MatFloat(LiveWaterMatPath, "_PixelsPerUnit");
            float oldCells = oldLambda / (1f / ppu);
            Assert.Less(oldCells, 4f,
                $"SABOTAGE NOT DETECTED — the pre-fix 0.12 m wavelength resolves at {oldCells:0.00} cells " +
                $"on the shipped PPU {ppu} grid, which the floor check above would have PASSED. The test " +
                "cannot see the moiré it exists to pin.");
        }

        [Test]
        public void EveryPreset_AgreesWithTheLiveMaterialOnThePixelGrid()
        {
            // ⚠️ Found while fixing the moiré, and it is the more dangerous half. _PixelsPerUnit is not a
            // mood: it is THE quantization grid every band in the shader samples against. Six presets
            // carried 12 while Water.mat carried 24, so "Water Presets ▸ Apply" — a wholesale
            // CopyPropertiesFromMaterial — would silently HALVE the sea's pixel grid, and at PPU 12 no
            // wavelength inside the ADR's ripple band can clear the moiré floor at all. A preset may carry
            // any mood it likes; it may not redefine the grid underneath the whole look.
            float live = MatFloat(LiveWaterMatPath, "_PixelsPerUnit");
            foreach (string name in PresetNames)
            {
                float p = MatFloat($"{PresetsFolder}/{name}.mat", "_PixelsPerUnit");
                Assert.AreEqual(live, p, 1e-4f,
                    $"'{name}.mat' carries _PixelsPerUnit {p} but the live Water.mat is {live}. Applying it " +
                    "would re-quantize the entire sea — every band's sampling, not just this preset's mood.");
            }
        }

        [Test]
        public void EveryPreset_SerializesTheBandKeys_OrApplyingItStampsTheDefault()
        {
            // The standing preset trap (the _RainRingStrength precedent), on the new keys. Asserted on the
            // serialized TEXT: a material that omits a key still answers from the shader defaults, so a
            // value-based check passes vacuously for exactly the presets that carry the trap.
            //
            // _RippleWavelength is the cautionary case — it was serialized by NO material at all, so every
            // one of them silently rode the shader default, and the default was the wrong number.
            foreach (string name in PresetNames)
            {
                string yaml = File.ReadAllText($"{PresetsFolder}/{name}.mat");
                foreach (string key in BandKeys)
                    Assert.IsTrue(yaml.Contains($"- {key}:"),
                        $"'{name}.mat' does not serialize {key} — applying it would stamp the shader default " +
                        "over the owner's tuned value.");
            }
            string liveYaml = File.ReadAllText(LiveWaterMatPath);
            foreach (string key in BandKeys)
                Assert.IsTrue(liveYaml.Contains($"- {key}:"),
                    $"Water.mat does not serialize {key} — the live material is the one that draws.");
        }

        // ==== the de-regularizing knobs are real, bounded, and revertible ==========================

        [Test]
        public void TheBandDeRegularizers_ShipOn_OrTheOwnerSeesNoChange()
        {
            // Shipped-on with modest values: the owner just locked this baseline, so this PR removes the
            // PATTERN, not the banding feature. Values that ship at their off position would make the whole
            // change a no-op he cannot see.
            Assert.Less(MatFloat(LiveWaterMatPath, "_EnvelopeBandPatchMin"), 1f,
                "_EnvelopeBandPatchMin = 1 is wall-to-wall banding — exactly today's look, no patchiness");
            Assert.Greater(MatFloat(LiveWaterMatPath, "_EnvelopeBandWarp"), 0f,
                "_EnvelopeBandWarp = 0 leaves the band boundaries ruler-straight — the reported defect");
        }

        [Test]
        public void TheDeRegularizers_StayModest_SoTheLockedBaselineIsNotRetuned()
        {
            // The other side of the same argument. #372/#382 locked the sea's look by owner ruling; this
            // change is allowed to break the RULER, not to restyle the water. Bands must still read as
            // bands, and the value-axis wander must stay well inside one band of the 7-band posterize
            // (~0.143 of the axis) or it re-shuffles which band a crest lands in rather than wandering
            // where the boundary falls.
            float warp = MatFloat(LiveWaterMatPath, "_EnvelopeBandWarp");
            float bandWidth = 1f / 7f;
            Assert.Less(warp, bandWidth * 2f,
                $"_EnvelopeBandWarp {warp:0.00} is more than two band widths ({bandWidth:0.000} each) — at " +
                "that size it stops wandering the boundary and starts re-assigning crests to other shades");
            // ⚠️ The lower bound here is not a guess — it is where a GPU acceptance guard actually bit.
            // ShoreSwirl_EnvelopeBands_FadeWithTheSeam measures that open water still carries its envelope
            // bands, and a patch floor of 0.35 took that imprint under its bar: de-regularizing had started
            // deleting the feature rather than breaking up its spacing. Keep a real floor so the bands are
            // everywhere and only their STRENGTH is uneven.
            Assert.GreaterOrEqual(MatFloat(LiveWaterMatPath, "_EnvelopeBandPatchMin"), 0.45f,
                "the patch floor is low enough to thin the bands out of open water — the §23 marked-wave " +
                "read is owner-approved and must survive; this PR breaks the RULER, not the feature");
        }

        [Test]
        public void ThePatchScale_MakesPatchesTensOfMetresAcross_NotASecondGrid()
        {
            // Patches have to be much larger than the band spacing they break up, or the mask is just
            // another regular texture laid over the first one — trading one pattern for two.
            float cyclesPerMetre = MatFloat(LiveWaterMatPath, "_EnvelopeBandPatchScale");
            float patchMetres = 1f / cyclesPerMetre;
            Assert.That(patchMetres, Is.InRange(20f, 120f),
                $"patches are {patchMetres:0} m across — the intent is tens of metres, comfortably larger " +
                "than the band spacing being broken up and smaller than the region");
        }

        // ==== the shader actually wires them, and off means off ====================================

        [Test]
        public void TheShader_AppliesThePatchMaskToTheBandContribution_AndWarpsBeforePosterizing()
        {
            // Order matters and is easy to get subtly wrong: the warp must be applied to the value axis
            // BEFORE BandValue01 posterizes (warping after would move a finished, quantized value and
            // produce off-palette shades), and the patch mask must scale the band's CONTRIBUTION (the lerp
            // weight), not the shade itself — scaling the shade would tint the sea rather than reveal less
            // of the band.
            string src = File.ReadAllText(ShaderPath);
            int warpAt = src.IndexOf("_EnvelopeBandWarp);", System.StringComparison.Ordinal);
            int bandAt = src.IndexOf("BandValue01(vN,", System.StringComparison.Ordinal);
            Assert.Greater(warpAt, 0, "the band value warp is not applied in the shader");
            Assert.Greater(bandAt, warpAt,
                "the warp must be applied to the value axis BEFORE the posterize, or it moves an already " +
                "quantized value and lands between the palette's band shades");
            StringAssert.Contains("* bandSeam * patchMask", src,
                "the patch mask must scale the band's CONTRIBUTION alongside the other gates, not its shade");
        }

        [Test]
        public void TheShader_KeepsTheFieldItself_Untouched()
        {
            // The load-bearing constraint of this whole PR: SEE==FEEL. The owner locked the spectrum and the
            // hulls ride it, so a look complaint may never be answered by editing the sea. The band read may
            // be warped; WaveFieldSample's inputs may not.
            string src = File.ReadAllText(ShaderPath);
            // The fragment's own read is at Pixelize(worldXY) — the true world position on the shared pixel
            // grid, with no warp term. (The neighbour taps offset by ±de/±ce are gradient probes, which is
            // a different thing.) If a warp ever appears inside this call, the drawn sea has stopped being
            // the sea the hull rides and the whole SEE==FEEL argument goes with it.
            Assert.IsTrue(Regex.IsMatch(src, @"WaveFieldSample\(\s*Pixelize\(worldXY\)\s*,"),
                "the fragment must still sample the shared wave field at the TRUE world position — " +
                "de-regularizing is a read-side change, never a change to the sea the hulls ride");
            Assert.IsFalse(Regex.IsMatch(src, @"WaveFieldSample\([^)]*_EnvelopeBandWarp"),
                "the band warp must never be fed into the field sample itself — it warps the VALUE AXIS " +
                "the bands posterize, which costs one noise read; warping the sample position would both " +
                "double the field cost and draw crests where the hull cannot feel them");
        }
    }
}
