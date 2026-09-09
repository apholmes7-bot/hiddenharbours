using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE SEA THE OWNER ACTUALLY PLAYS — one definition, and a guard that keeps it true.</b>
    ///
    /// <para>⚠️ <b>Why this exists.</b> Since #600 the shipped <c>GameConfig.asset</c> is authoritative:
    /// it carries every serialized field, and changing a <c>.Default</c> in code no longer changes the
    /// live game. <c>GameConfigAssetCoverageTests</c> guards that every key is PRESENT — but it does
    /// not, and cannot, guard the VALUES. So a measurement fixture that builds
    /// <c>WaveFieldSettings.Default</c> is measuring a sea nobody sails, and nothing goes red.</para>
    ///
    /// <para><b>That had already happened, twice, in the water register's own evidence</b> (found
    /// 2026-09-09 while building row 34). Both <c>WaveAmplitudeMeasurementTests</c> and
    /// <c>WaveStateChangeVibrationTests</c> opened with <c>Default</c> and patched exactly one of the
    /// four fields the asset overrides:</para>
    /// <code>
    ///   field                      | .Default | GameConfig.asset
    ///   SeaFetchKilometres         |        0 |  25   (patched)
    ///   SeaStateAmplitudeExponent  |     1.35 |  1.5  (missed)
    ///   SpectrumBlend              |        0 |  0.65 (missed)   &lt;- 0 is the LEGACY 4-train field
    ///   CrestSharpening            |      2.2 |  2.6  (missed)
    /// </code>
    /// <para><c>SpectrumBlend</c> is the one that mattered: every number those fixtures published
    /// described a <b>four-train</b> sea, while the owner sails an eight-bin spectral one. It cost
    /// register row 33 its headline — the "steepness 0.182, past the ~0.14 breaking limit" claim is an
    /// artefact of the 1.35 exponent and the missing spectrum; on the shipped config the steepness
    /// never exceeds 0.105. (Row 33's other half survives and is the stronger claim: the drawn sea is
    /// 1.83× the fetch-limited reference at the top of the reachable wind.)</para>
    ///
    /// <para><b>The fix is this file.</b> One mirror, used by every fixture that claims to measure the
    /// shipped sea, and <see cref="TheMirror_IsTheShippedAsset_FieldByField"/> to stop it drifting —
    /// in BOTH directions, because it walks the asset's own keys rather than a list kept here.</para>
    ///
    /// <para>⚠️ The mirror is a MIRROR, deliberately, not a loader: it is plain C# so the headless
    /// harness and the no-Unity paths can use it. The guard is what makes a mirror safe.</para>
    /// </summary>
    public static class ShippedWaveField
    {
        public const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        /// <summary>
        /// <c>GameConfig.asset</c>'s <c>WaveField</c> block, as C#. Pinned field-by-field against the
        /// asset by <see cref="ShippedWaveFieldTests"/> — if you change one here and not there, or
        /// there and not here, CI names the field.
        /// </summary>
        public static WaveFieldSettings Settings()
        {
            WaveFieldSettings s = WaveFieldSettings.Default;
            s.Gravity = 9.81f;
            s.SecondaryTrainCount = 3;
            s.DominantWavelengthBase = 6f;
            s.DominantWavelengthPerWindSpeed = 1.5f;
            s.DominantWavelengthMax = 40f;
            s.SeaFetchKilometres = 25f;
            s.DominantWavelengthScale = 1f;
            s.PrimaryAmplitude = 0.8f;
            s.SeaStateAmplitudeExponent = 1.5f;
            s.HeightFromFetch = true;
            s.FetchHeightCoefficient = 0.0016f;
            s.FullyDevelopedHeightCoefficient = 0.0246f;
            s.HeightStyleScale = 1f;
            s.GlassGateSeaState = 0.05f;
            s.CrestSharpening = 2.6f;
            s.PhaseSeed = 0;
            s.Secondary1AngleDegrees = 32f;
            s.Secondary1WavelengthRatio = 0.55f;
            s.Secondary1AmplitudeRatio = 0.45f;
            s.Secondary2AngleDegrees = -47f;
            s.Secondary2WavelengthRatio = 0.38f;
            s.Secondary2AmplitudeRatio = 0.3f;
            s.Secondary3AngleDegrees = 11f;
            s.Secondary3WavelengthRatio = 0.22f;
            s.Secondary3AmplitudeRatio = 0.18f;
            s.SpectrumBlend = 0.65f;
            s.SpectrumPeakEnhancement = 3.3f;
            s.SpectrumPeakWidth = 0.08f;
            s.SpectrumFrequencySpacing = 0.08f;
            s.SpectrumSpreadDegrees = 55f;
            s.SpectrumSpreadExponent = 2f;
            s.SpectrumBinCount = 8;
            s.SpectrumLadderMinWavelengthMeters = 5f;
            s.SpectrumLadderMaxWavelengthMeters = 30f;
            return s;
        }

        /// <summary>The asset's <c>WaveField:</c> block as key → value, straight out of the YAML text.
        /// Text, not <c>AssetDatabase</c>, so this runs anywhere a file can be read.</summary>
        public static Dictionary<string, float> ReadWaveFieldBlock(string configPath)
        {
            var map = new Dictionary<string, float>();
            string[] lines = File.ReadAllLines(configPath);
            bool inBlock = false;
            foreach (string raw in lines)
            {
                string line = raw.TrimEnd();
                if (line.Length == 0) continue;

                // The block header sits at two spaces; its members at four. Any other key at two
                // spaces ends it — including the sibling "WaveFieldAnimator:", which is why the
                // header is matched exactly rather than by prefix.
                if (line.StartsWith("  ") && !line.StartsWith("   "))
                {
                    inBlock = line.Trim() == "WaveField:";
                    continue;
                }
                if (!inBlock || !line.StartsWith("    ")) continue;

                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    map[key] = v;
            }
            return map;
        }
    }

    /// <summary>The guard that makes <see cref="ShippedWaveField.Settings"/> safe to trust.</summary>
    public class ShippedWaveFieldTests
    {
        /// <summary>
        /// ⭐ <b>THE MIRROR IS THE ASSET, KEY BY KEY — and the asset is walked, not a list kept here.</b>
        /// Written this way on purpose: a guard that iterates a hand-kept list of fields cannot notice
        /// the field somebody adds to the asset next month, which is the exact shape of the failure
        /// this whole file exists to stop.
        /// </summary>
        [Test]
        public void TheMirror_IsTheShippedAsset_FieldByField()
        {
            if (!File.Exists(ShippedWaveField.ConfigPath))
                Assert.Ignore($"{ShippedWaveField.ConfigPath} is not reachable from here — this guard " +
                              "reads the shipped asset and runs where it is (EditMode / CI).");

            var asset = ShippedWaveField.ReadWaveFieldBlock(ShippedWaveField.ConfigPath);
            Assert.IsNotEmpty(asset, "the WaveField block in GameConfig.asset parsed as empty — has " +
                                     "the asset's indentation or block name changed?");

            WaveFieldSettings mirror = ShippedWaveField.Settings();
            var report = new StringBuilder();
            report.AppendLine("  key                                 asset      mirror");
            var missing = new List<string>();

            foreach (var kv in asset)
            {
                FieldInfo f = typeof(WaveFieldSettings).GetField(kv.Key,
                    BindingFlags.Public | BindingFlags.Instance);
                if (f == null)
                {
                    missing.Add(kv.Key);
                    continue;
                }
                float mine = Convert.ToSingle(f.GetValue(mirror), CultureInfo.InvariantCulture);
                report.AppendLine($"  {kv.Key,-34} {kv.Value,9:0.####} {mine,11:0.####}");
                Assert.AreEqual(kv.Value, mine, Mathf.Max(1e-4f, Mathf.Abs(kv.Value) * 1e-5f),
                    $"⭐ ShippedWaveField.Settings() has {kv.Key} = {mine}, but the sea the owner " +
                    $"plays has {kv.Value} (GameConfig.asset). One of the two is wrong, and the ASSET " +
                    "is authoritative (#600). Every fixture that claims to measure the shipped sea " +
                    "reads this mirror, so a drift here silently republishes measurements of a sea " +
                    "nobody sails — which is how register row 33 lost its headline on 2026-09-09.");
            }
            TestContext.WriteLine(report.ToString());

            Assert.IsEmpty(missing,
                "GameConfig.asset carries WaveField keys that WaveFieldSettings does not declare: " +
                string.Join(", ", missing) + ". Either the asset has a stale key or the struct lost a " +
                "field; GameConfigAssetCoverageTests owns the presence half of this.");
        }

        /// <summary>
        /// ⚠️ <b>AND THE MIRROR MUST NOT BE `.Default`.</b> The whole point is that the two differ; a
        /// mirror that had quietly become the code defaults would pass every assertion above only if
        /// the asset had ALSO become the defaults, but it would pass this file's intent by accident.
        /// This names the four fields that differ today, so the difference is documented rather than
        /// merely present.
        /// </summary>
        [Test]
        public void TheShippedSea_IsNotTheCodeDefaults_AndTheseAreTheFieldsThatDiffer()
        {
            WaveFieldSettings d = WaveFieldSettings.Default;
            WaveFieldSettings s = ShippedWaveField.Settings();

            Assert.AreNotEqual(d.SeaFetchKilometres, s.SeaFetchKilometres,
                "the asset ships the fetch law on (25 km); .Default ships it OFF (0), which is the " +
                "legacy linear peak — 3.8x longer at the light airs the owner sails");
            Assert.AreNotEqual(d.SpectrumBlend, s.SpectrumBlend,
                "⭐ THE ONE THAT COST ROW 33 ITS HEADLINE: .Default's SpectrumBlend is 0, which is the " +
                "hand-authored FOUR-train field. The asset ships 0.65 — an eight-bin spectral sea. A " +
                "fixture on .Default is not measuring the same sea in any sense that matters.");
            Assert.AreNotEqual(d.SeaStateAmplitudeExponent, s.SeaStateAmplitudeExponent,
                "the asset's amplitude response is 1.5, .Default's 1.35 — the fixtures' sea was ~30 % " +
                "taller than the owner's at light airs");
            Assert.AreNotEqual(d.CrestSharpening, s.CrestSharpening,
                "the asset sharpens crests at 2.6, .Default at 2.2");
        }
    }
}
