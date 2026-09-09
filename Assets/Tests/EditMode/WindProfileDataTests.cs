using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>A region's wind is DATA (rule 2), the weather SYSTEM is not a regional fact, and an
    /// unauthored profile is a dead calm rather than a gentle one.</b> The three traps that come with
    /// moving wind onto assets, guarded rather than hoped about.
    /// </summary>
    public class WindProfileDataTests
    {
        const double SecondsPerHour = 3600.0 / 72.0;

        static readonly string[] WindAssets =
        {
            "Assets/_Project/Data/Wind/Wind_CoddleCove.asset",
            "Assets/_Project/Data/Wind/Wind_NineMileCreek.asset",
            "Assets/_Project/Data/Wind/Wind_StPeters.asset",
        };

        static readonly (string region, string wind)[] RegionWiring =
        {
            ("Assets/_Project/Data/Regions/CoddleCove.asset",     "Wind_CoddleCove"),
            ("Assets/_Project/Data/Regions/NineMileCreek.asset",  "Wind_NineMileCreek"),
            ("Assets/_Project/Data/Regions/StPeters.asset",       "Wind_StPeters"),
        };

        // =============================================================================================
        //  1. The weather system is a WORLD fact
        // =============================================================================================

        /// <summary>
        /// ⚠️ <b>Two regions are never in different weather on the same day.</b> The system channel's
        /// timing and shape are world-level; a region scales only its STRENGTH. So at the same
        /// <c>(seed, t)</c> two profiles that differ ONLY in <c>SystemStrength</c> must differ in wind
        /// speed by exactly <c>factor × Δstrength</c> — the same factor for both, never a different
        /// point of a different storm.
        ///
        /// <para>Without this, Nine Mile Creek could blow a gale while St Peters, ten kilometres away
        /// on the same island, lay glass — and a player crossing between them would watch the weather
        /// change because the scene did.</para>
        /// </summary>
        [Test]
        public void TheWeatherSystem_IsAWorldFact_SoTwoRegionsDifferOnlyByTheirExposure()
        {
            WindProfile a = WindProfile.CoddleCove;
            WindProfile b = WindProfile.CoddleCove;
            a.SystemStrength = 8f;
            b.SystemStrength = 16f;

            var report = new StringBuilder("  hours   factor   windA   windB   (B-A)   factor*dS\n");
            int checkedPoints = 0;
            for (double h = 0; h < 24 * 14; h += 0.37)
            {
                float factor = WeatherModel.WeatherSystemFactor(h, 12345);
                float ua = WeatherModel.SampleWind(h * SecondsPerHour, 12345, SecondsPerHour, a).magnitude;
                float ub = WeatherModel.SampleWind(h * SecondsPerHour, 12345, SecondsPerHour, b).magnitude;
                float expected = factor * (b.SystemStrength - a.SystemStrength);

                // Only where neither is clipped by the zero floor or the rail, which are not the
                // subject here (both are far away at these strengths, but say so rather than assume).
                if (ua <= 0.001f || ub >= b.CalmMaxStrength - 0.001f) continue;
                checkedPoints++;
                if (checkedPoints <= 6)
                    report.AppendLine($"  {h,6:0.0} {factor,8:0.0000} {ua,7:0.00} {ub,7:0.00} {ub - ua,7:0.00} {expected,11:0.00}");

                Assert.AreEqual(expected, ub - ua, 1e-3f,
                    $"⭐ at {h:0.00} h two regions differing only in exposure must differ by exactly " +
                    "their exposure times the WORLD's system factor. If this fires, the system " +
                    "channel has picked up something per-region — a profile-dependent timescale or " +
                    "shape — and the island now has two weathers at once.");
            }
            TestContext.WriteLine(report.ToString());
            Assert.Greater(checkedPoints, 500, "the sweep must actually have compared something");

            // ...and the factor must genuinely move, or the assertion above is satisfied by zero.
            float lo = 1f, hi = 0f;
            for (double h = 0; h < 24 * 14; h += 0.37)
            {
                float f = WeatherModel.WeatherSystemFactor(h, 12345);
                lo = Mathf.Min(lo, f); hi = Mathf.Max(hi, f);
            }
            TestContext.WriteLine($"  the world's system factor ranged {lo:0.000}..{hi:0.000} over a fortnight");
            Assert.Less(lo, 0.05f, "DEAD CONTROL: the system channel must REST near zero most of the time");
            Assert.Greater(hi, 0.5f, "…and must actually build, or no region ever sees a blow");
        }

        /// <summary>The channel's signature: skewed, so it is at rest far more often than not. A
        /// symmetric channel would have made the average day rough, which is the failure mode the
        /// third channel exists to avoid.</summary>
        [Test]
        public void TheSystemChannel_RestsAtZeroMostOfTheTime()
        {
            int atRest = 0, total = 0;
            foreach (int seed in new[] { 12345, 7, 4242 })
                for (double h = 0; h < 24 * 7; h += 0.05)
                {
                    if (WeatherModel.WeatherSystemFactor(h, seed) < 0.05f) atRest++;
                    total++;
                }
            double share = atRest / (double)total * 100.0;
            TestContext.WriteLine($"  the system channel is at rest (<0.05) for {share:0.0}% of the time");
            Assert.Greater(share, 50.0,
                $"the system channel is only at rest {share:0.0}% of the time. It is meant to be a " +
                "passing depression, not a fourth wind: if it is always partly on it has become a " +
                "mean-strength increase in disguise, and the ordinary day will have gone rough.");
        }

        // =============================================================================================
        //  2. The assets carry every key, and the regions point at them
        // =============================================================================================

        /// <summary>
        /// ⚠️ <b>Every wind asset states every field the struct declares.</b> An absent key
        /// deserializes to ZERO, and a zeroed <c>WindProfile</c> is not a gentle default — it is a
        /// dead calm forever, because <c>CalmMaxStrength = 0</c> clamps every wind to nothing. Walked
        /// by reflection over the struct, so a field added next month fails here rather than shipping
        /// silently as zero. (Same shape as <c>GameConfigAssetCoverageTests</c>, and the same reason.)
        /// </summary>
        [Test]
        public void EveryWindAsset_StatesEveryFieldTheStructDeclares()
        {
            if (!File.Exists(WindAssets[0]))
                Assert.Ignore("the wind assets are not reachable from here — this guard reads them " +
                              "and runs where they are (EditMode / CI).");

            FieldInfo[] fields = typeof(WindProfile).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.Greater(fields.Length, 5, "reflection found no WindProfile fields — has it moved?");

            var report = new StringBuilder();
            foreach (string path in WindAssets)
            {
                Assert.IsTrue(File.Exists(path), $"missing wind asset: {path}");
                string text = File.ReadAllText(path);
                Assert.IsTrue(text.EndsWith("\n"),
                    $"{path} does not end in a newline — the next appended key would land outside the " +
                    "mapping and read as zero");

                var missing = new List<string>();
                foreach (FieldInfo f in fields)
                    if (!text.Contains("    " + f.Name + ":")) missing.Add(f.Name);

                report.AppendLine($"  {Path.GetFileName(path),-26} {fields.Length - missing.Count}/{fields.Length} fields");
                Assert.IsEmpty(missing,
                    $"{Path.GetFileName(path)} does not state: {string.Join(", ", missing)}. An absent " +
                    "key reads ZERO, and a zero WindProfile is a dead calm forever (CalmMaxStrength 0 " +
                    "clamps every wind to nothing) — not a gentle breeze.");

                // The blank-line trap: a blank line ENDS a Unity YAML block mapping and everything
                // after it is silently dropped. This repo has shipped that bug (#741, found by #747).
                // ⚠️ Checked BY INDEX, and excluding the final empty element the trailing newline
                // creates — an earlier revision used text.IndexOf(line), which returns 0 for the
                // empty string and therefore failed on every well-formed asset.
                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                int lastContent = lines.Length - 1;
                while (lastContent > 0 && lines[lastContent].Trim().Length == 0) lastContent--;
                for (int i = 0; i < lastContent; i++)
                    Assert.Greater(lines[i].Trim().Length, 0,
                        $"{Path.GetFileName(path)} has a BLANK LINE at line {i + 1}, inside the " +
                        "mapping. A blank line ENDS a Unity YAML block mapping: every key after it " +
                        "is silently dropped to zero, and a zero WindProfile is a dead calm forever.");
            }
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>Each region points at its own wind. A region with no reference is not broken —
        /// it falls back by name — but silence is how per-region weather quietly never happens.</summary>
        [Test]
        public void EveryRegionAsset_PointsAtItsOwnWind()
        {
            if (!File.Exists(RegionWiring[0].region))
                Assert.Ignore("the region assets are not reachable from here.");

            foreach (var (regionPath, windName) in RegionWiring)
            {
                Assert.IsTrue(File.Exists(regionPath), $"missing region asset: {regionPath}");
                string metaPath = $"Assets/_Project/Data/Wind/{windName}.asset.meta";
                Assert.IsTrue(File.Exists(metaPath), $"missing wind meta: {metaPath}");

                string guid = null;
                foreach (string line in File.ReadAllLines(metaPath))
                    if (line.StartsWith("guid: ")) guid = line.Substring(6).Trim();
                Assert.IsNotNull(guid, $"{metaPath} has no guid");

                string region = File.ReadAllText(regionPath);
                StringAssert.Contains("WindProfile:", region,
                    $"{Path.GetFileName(regionPath)} has no WindProfile reference at all");
                StringAssert.Contains(guid, region,
                    $"{Path.GetFileName(regionPath)} does not reference {windName} (guid {guid}). " +
                    "Without it the region falls back to WindProfile.CoddleCove and its own wind " +
                    "authoring is dead data.");
            }
        }

        // =============================================================================================
        //  3. The fallback is by NAME, never to a zero profile
        // =============================================================================================

        /// <summary>
        /// ⚠️ <b>An unassigned or unauthored profile must land on <c>CoddleCove</c>, never on zero.</b>
        /// A zeroed struct passes a null check and then clamps every wind to nothing: the region is
        /// becalmed forever and nothing logs. This is the shape of
        /// <c>a-serialized-field-absent-from-the-asset-reads-zero</c>, applied to a whole struct.
        /// </summary>
        [Test]
        public void AnUnassignedOrZeroedProfile_FallsBackByName_NeverToADeadCalm()
        {
            WindProfile fromNull = WindProfileDef.Resolve(null, "a region with no wind wired");
            Assert.AreEqual(WindProfile.CoddleCove.MeanStrength, fromNull.MeanStrength, 1e-6f,
                "a null reference must resolve to CoddleCove BY NAME");
            Assert.Greater(fromNull.CalmMaxStrength, 0f, "…and therefore to a profile that can blow");

            var zeroed = ScriptableObject.CreateInstance<WindProfileDef>();
            try
            {
                zeroed.Profile = default;                       // exactly what an unauthored asset gives
                Assert.IsFalse(WindProfileDef.IsAuthored(zeroed.Profile),
                    "a zeroed profile must be recognised as unauthored — CalmMaxStrength 0 is the tell");

                WindProfile healed = WindProfileDef.Resolve(zeroed, "an unauthored asset");
                Assert.Greater(healed.CalmMaxStrength, 0f,
                    "⭐ a zeroed profile must NOT be sampled. CalmMaxStrength 0 clamps every wind to " +
                    "nothing, so the region would be becalmed forever — a bug that looks exactly like " +
                    "weather and reports nothing.");
                Assert.AreEqual(WindProfile.CoddleCove.SystemStrength, healed.SystemStrength, 1e-6f,
                    "…and it heals to CoddleCove, the named fallback");
            }
            finally { UnityEngine.Object.DestroyImmediate(zeroed); }
        }

        /// <summary>And the shipped assets are all authored — the guard above only matters if the
        /// real data passes it.</summary>
        [Test]
        public void TheShippedWindAssets_AreAllAuthored()
        {
            if (!File.Exists(WindAssets[0])) Assert.Ignore("wind assets not reachable from here.");
            foreach (string path in WindAssets)
            {
                string text = File.ReadAllText(path);
                int i = text.IndexOf("CalmMaxStrength:", StringComparison.Ordinal);
                Assert.Greater(i, 0, $"{Path.GetFileName(path)} states no CalmMaxStrength");
                string value = text.Substring(i + 16).Split('\n')[0].Trim();
                Assert.IsTrue(float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                                             out float cap), $"unparsable CalmMaxStrength: '{value}'");
                Assert.Greater(cap, 0f,
                    $"{Path.GetFileName(path)} has CalmMaxStrength {cap} — WindProfileDef.Resolve " +
                    "would treat it as unauthored and quietly substitute CoddleCove, so this region's " +
                    "wind authoring would never be used.");
            }
        }
    }
}
