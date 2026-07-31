using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The 2026-07-31 RAIN ACTIVATION, held to both halves of its contract at once — because the two failure
    /// modes pull in opposite directions and a test for either one alone lets the other through.
    ///
    /// <para><b>Half one: it is ON.</b> The rain layer shipped in Arc C (#156/#160, tuned #161/#162) and then
    /// sat dark for months behind placeholder "dial it in later" values — <c>_RainRingStrength</c> 0 on the
    /// material and a drop pool so small a real squall drew ~17 faint streaks. Those are exactly the kind of
    /// values a later merge restores without anyone noticing, since nothing goes red when rain quietly stops
    /// existing. So this fixture asserts the rings are on in the shipped material and that a REPRESENTATIVE
    /// squall clears a visibility floor in both streak count and opacity.</para>
    ///
    /// <para><b>Half two: it is still DRY when it should be.</b> The activation touched only LOOK knobs. The
    /// intensity mapping — <see cref="RainConfig.BaselineIntensity"/> 0 plus the two onsets — is the
    /// 2026-07-05 owner-playtest fix ("rain was constant because a rough-ish sea alone leaked through the old
    /// gate even in perfectly clear air"). Turning the look up is the obvious way to un-fix that by accident,
    /// so the complaint case is re-pinned HERE, against <see cref="RainConfig.Default"/> itself rather than
    /// against copied constants (<c>RainIntensityTests</c> pins the maths; this pins the shipped config).</para>
    ///
    /// <para><b>The preset trap.</b> Ring strength is deliberately NOT in <c>WaterSurface.MoodFloatNames</c>
    /// (<c>_RainIntensity</c> already scales the rings by {sea-state, visibility} and the mood blend runs on
    /// those same two axes — blending the strength too would multiply one signal by itself). That leaves
    /// <c>Water.mat</c> as the single live knob, which is fine at runtime but exposes an authoring trap: the
    /// editor's "Hidden Harbours ▸ Art ▸ Water Presets ▸ Apply" does a wholesale
    /// <c>CopyPropertiesFromMaterial</c>, so a preset that omits the key stamps the SHADER DEFAULT (0) onto
    /// Water.mat and silently switches the rain back off. Every preset therefore has to carry the key set —
    /// asserted below on the serialized YAML, because a material that omits a property still answers
    /// <c>HasProperty</c>/<c>GetFloat</c> from the shader and would pass vacuously.</para>
    ///
    /// <para>NOTE (CI has no GPU): none of this can see a pixel. It pins the DATA that decides whether rain
    /// draws at all. Whether the streaks sort in front of the water mesh, and whether the rings read at night,
    /// are owner in-editor checks.</para>
    /// </summary>
    public class RainActivationTests
    {
        private const string LiveWaterMatPath = "Assets/_Project/Art/Materials/Water.mat";
        private const string PresetsFolder = "Assets/_Project/Art/Materials/WaterPresets";

        /// <summary>The tracked preset library — the anchors the weather blend reads AND the moods the owner
        /// can stamp onto Water.mat from the menu. Every one of them must carry the ring keys.</summary>
        private static readonly string[] PresetNames =
        {
            "Water_DeepBlue", "Water_FoggySmother", "Water_GlassyCalm", "Water_NorthAtlantic",
            "Water_StirredBrown", "Water_StormGrey", "Water_Tropical", "Water_WarmShelter",
        };

        /// <summary>The ring SHAPE keys, which are owner-tuned in ONE place. They are not mood-blended, so a
        /// preset carrying a different value can only surprise: it silently re-shapes the rings when applied.</summary>
        private static readonly string[] SharedShapeFloats =
        {
            "_RainRingScale", "_RainRingDensity", "_RainRingSpeed",
        };

        /// <summary>A representative squall the deterministic weather actually produces: real murk, real chop.
        /// The same point <c>RainIntensityTests</c> calls "blow + thick murk" (~0.27 intensity).</summary>
        private const float SquallVisibility = 0.35f;
        private const float SquallSeaState = 0.54f;

        /// <summary>Rain intensity AT THE SHIPPED CONFIG — read off <see cref="RainConfig.Default"/> rather
        /// than re-declared, so retuning the config moves these tests with it instead of past them.</summary>
        private static float RainAt(float visibility, float seaState01)
        {
            RainConfig c = RainConfig.Default;
            return AmbientParticleMath.RainIntensity(
                visibility, seaState01, c.BaselineIntensity, c.SeaStateWeight, c.VisOnset, c.VisFull, c.SeaOnset);
        }

        private static Material LoadMaterial(string path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(mat, $"No material at '{path}'. Rain draws off these assets — a missing one is a " +
                                  "failure, not a skip.");
            return mat;
        }

        // ==== half two: the dryness contract the activation must not have touched ==========================

        [Test]
        public void ShippedRainConfig_KeepsTheTwoOnsetGate_Untouched()
        {
            RainConfig c = RainConfig.Default;
            Assert.AreEqual(0f, c.BaselineIntensity, 1e-6f,
                "BaselineIntensity must stay 0. Any nonzero baseline rains on a clear, glassy day — the exact " +
                "2026-07-05 owner complaint. Dial the LOOK (MaxDrops / MaxAlpha), never the baseline.");
            Assert.Less(c.VisFull, c.VisOnset,
                "The murk gate needs VisFull < VisOnset; inverted edges collapse the gate to a step.");
            Assert.Greater(c.SeaOnset, 0f,
                "A sea onset of 0 lets a dead-calm foggy morning rain — that is fog, not a squall.");
        }

        [Test]
        public void ShippedRainConfig_StillRainsNothing_OnAClearOrCalmDay()
        {
            // The complaint case itself: an ordinary Moderate sea on a clear night.
            Assert.AreEqual(0f, RainAt(0.85f, 0.43f), 1e-4f,
                "A Moderate sea on a clear night must be DRY. If this went nonzero, the activation regressed " +
                "the 2026-07-05 fix — check whether BaselineIntensity or an onset moved.");
            // No amount of chop rains in clear air, and no amount of murk rains on glass.
            Assert.AreEqual(0f, RainAt(1f, 1f), 1e-4f, "A full gale in crystal-clear air is a bright blustery day, not a downpour.");
            Assert.AreEqual(0f, RainAt(0f, 0f), 1e-4f, "A dead-glass sea in thick murk is fog, not a squall.");
        }

        // ==== half one: it is actually ON =================================================================

        [Test]
        public void ARealSquall_DrawsEnoughStreaksToRead()
        {
            RainConfig c = RainConfig.Default;
            float intensity = RainAt(SquallVisibility, SquallSeaState);
            Assert.Greater(intensity, 0.1f, "The representative squall must clear the two onsets at all.");

            // Spawn rate is MaxDrops/Lifetime × intensity and each drop lives Lifetime, so the steady-state
            // count on screen is simply MaxDrops × intensity — the pool size IS the full-downpour streak count.
            float concurrent = c.MaxDrops * intensity;
            Assert.Greater(concurrent, 30f,
                $"Only ~{concurrent:F0} streaks over the {c.AreaHalfSize.x * 2f:F0}x{c.AreaHalfSize.y * 2f:F0}m " +
                "field in a genuine squall — that is the invisible placeholder look, not rain. Raise MaxDrops " +
                "(it is a pooled, batched sprite; the budget is not the constraint here).");
        }

        [Test]
        public void ARealSquall_DrawsStreaksYouCanSeeAgainstGreyWater()
        {
            RainConfig c = RainConfig.Default;
            float peakAlpha = c.MaxAlpha * RainAt(SquallVisibility, SquallSeaState);
            Assert.Greater(peakAlpha, 0.15f,
                $"Peak streak alpha in a real squall is only {peakAlpha:F3} — the life envelope and the night " +
                "fade scale DOWN from here, so the rain would be a rumour. MaxAlpha is the dial.");
        }

        [Test]
        public void TheRainTint_StaysRestrained_NotTheReferenceDemosSaturatedLook()
        {
            // weather-rendering.md §4: we take the reference's technique, never its palette. Our identity is the
            // salt-stained North Atlantic — so the streak is a desaturated cool grey, cousin to _RainRingColor.
            Color c = RainConfig.Default.Color;
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            float saturation = max > 1e-4f ? (max - min) / max : 0f;
            Assert.Less(saturation, 0.25f,
                $"The rain tint {c} is {saturation:P0} saturated. The look-target's palette caveat is explicit: " +
                "restrained and salt-stained, not the reference's neon.");
        }

        [Test]
        public void LiveWaterMaterial_HasTheSurfaceRingsTurnedOn()
        {
            Material water = LoadMaterial(LiveWaterMatPath);
            float strength = water.GetFloat("_RainRingStrength");
            Assert.Greater(strength, 0f,
                "_RainRingStrength is 0 on the live Water.mat, so the shader's rain-ring block is an exact " +
                "passthrough and a squall dimples nothing. This is the single knob that turns the rings on, and " +
                "every scene builder assigns Water.mat to the Sea renderer, so 0 here means 0 everywhere.");
            Assert.LessOrEqual(strength, 1.5f,
                $"_RainRingStrength {strength} is very high. The ring add is already multiplied by the derived " +
                "_RainIntensity; past ~1 a heavy squall reads as white confetti rather than dimpled water.");
        }

        // ==== the preset trap: "Apply water preset" must not be able to switch the rain off ================

        [Test]
        public void EveryWaterPreset_SerializesTheFullRingKeySet()
        {
            foreach (string name in PresetNames)
            {
                string path = $"{PresetsFolder}/{name}.mat";
                Assert.IsTrue(File.Exists(path), $"Tracked preset '{name}' is missing from {PresetsFolder}.");
                string yaml = File.ReadAllText(path);

                // Asserted on the SERIALIZED text, not via HasProperty/GetFloat: a material that omits a
                // property still answers from the shader's defaults, so the value-based check passes vacuously
                // for exactly the presets that carry the trap.
                foreach (string key in SharedShapeFloats)
                    Assert.IsTrue(yaml.Contains($"- {key}:"),
                        $"'{name}.mat' does not serialize {key}. Applying it would stamp the shader default onto " +
                        "Water.mat and silently re-shape the owner's tuned rings.");

                Assert.IsTrue(yaml.Contains("- _RainRingStrength:"),
                    $"'{name}.mat' does not serialize _RainRingStrength. 'Water Presets ▸ Apply' copies the whole " +
                    "property set onto Water.mat, so applying this preset would stamp the shader default (0) and " +
                    "switch the rain OFF with no warning.");
                Assert.IsTrue(yaml.Contains("- _RainRingColor:"),
                    $"'{name}.mat' does not serialize _RainRingColor — applying it would wash out the ring tint.");
            }
        }

        [Test]
        public void EveryWaterPreset_KeepsTheRingsOn_WhenAppliedToTheLiveMaterial()
        {
            foreach (string name in PresetNames)
            {
                Material preset = LoadMaterial($"{PresetsFolder}/{name}.mat");
                float strength = preset.GetFloat("_RainRingStrength");
                Assert.Greater(strength, 0f,
                    $"'{name}' carries _RainRingStrength 0. Applying this mood would turn the rain rings off for " +
                    "the whole game. Per-mood strength is fine — zero is not, because nothing tells the owner.");
                Assert.LessOrEqual(strength, 1.5f,
                    $"'{name}' carries _RainRingStrength {strength} — high enough to read as white confetti once " +
                    "the derived _RainIntensity multiplies in.");
            }
        }

        [Test]
        public void EveryWaterPreset_SharesTheOwnerTunedRingShape()
        {
            Material water = LoadMaterial(LiveWaterMatPath);
            foreach (string name in PresetNames)
            {
                Material preset = LoadMaterial($"{PresetsFolder}/{name}.mat");
                foreach (string key in SharedShapeFloats)
                {
                    Assert.AreEqual(water.GetFloat(key), preset.GetFloat(key), 1e-4f,
                        $"'{name}' disagrees with Water.mat on {key}. Ring SHAPE is not mood-blended, so a preset " +
                        "that differs can only surprise — applying it silently re-shapes the rings (this is the " +
                        "bug the FoggySmother preset carried: scale 0.4 / density 1 against the tuned 6 / 0.35). " +
                        "If a per-mood shape is genuinely wanted, it has to go through MoodFloatNames, not here.");
                }

                Color presetTint = preset.GetColor("_RainRingColor");
                Color waterTint = water.GetColor("_RainRingColor");
                string tintMsg = $"'{name}' disagrees with Water.mat on _RainRingColor ({presetTint} vs " +
                                 $"{waterTint}) — same reasoning as the shape keys.";
                Assert.AreEqual(waterTint.r, presetTint.r, 1e-3f, tintMsg);
                Assert.AreEqual(waterTint.g, presetTint.g, 1e-3f, tintMsg);
                Assert.AreEqual(waterTint.b, presetTint.b, 1e-3f, tintMsg);
            }
        }
    }
}
