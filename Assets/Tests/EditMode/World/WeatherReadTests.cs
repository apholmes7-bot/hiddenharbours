using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>Basil reads the weather</b>: the day's spoken read is worked out from the deterministic wind,
    /// so it must name the direction and strength the sim gave, say building or easing as the sim does,
    /// come out the same for the same (seed, day), and have words for every band and outlook.
    /// </summary>
    public class WeatherReadTests
    {
        private const string BasilReadPath = "Assets/_Project/Data/NPCs/Dialogue/BasilWeatherRead.asset";
        private const string BasilDialoguePath = "Assets/_Project/Data/NPCs/Dialogue/BasilFirst.asset";
        private const string StPetersWindPath = "Assets/_Project/Data/Wind/Wind_StPeters.asset";
        private const float SecondsPerDay = GameConfig.DefaultSecondsPerDay;

        private static readonly string[] Compass =
            { "north", "nor'east", "east", "sou'east", "south", "sou'west", "west", "nor'west" };

        // ---- fakes ------------------------------------------------------------------------------

        /// <summary>One wind before 11:00 and another after, every day: a hand-set morning and afternoon.</summary>
        private sealed class MorningAfternoonWind : IWaveWindSource
        {
            private readonly Vector2 _morning, _afternoon;
            public MorningAfternoonWind(Vector2 morning, Vector2 afternoon) { _morning = morning; _afternoon = afternoon; }
            public WaveWindSample WaveWindAt(double t)
            {
                double hour = (t % SecondsPerDay + SecondsPerDay) % SecondsPerDay / SecondsPerDay * 24.0;
                Vector2 w = hour < 11.0 ? _morning : _afternoon;
                return new WaveWindSample(w, 0f);
            }
        }

        /// <summary>The real wind law (the one EnvironmentService evaluates) for a seed and profile.</summary>
        private sealed class SimWind : IWaveWindSource
        {
            private readonly int _seed;
            private readonly WindProfile _profile;
            public SimWind(int seed, WindProfile profile) { _seed = seed; _profile = profile; }
            public WaveWindSample WaveWindAt(double t)
            {
                Vector2 w = WeatherModel.SampleWind(t, _seed, SecondsPerDay / 24.0, _profile);
                return new WaveWindSample(w, WeatherModel.SeaState01(w.magnitude));
            }
        }

        private static WeatherReadDef Basil()
        {
            var def = AssetDatabase.LoadAssetAtPath<WeatherReadDef>(BasilReadPath);
            Assert.IsNotNull(def, "Basil's weather read asset is missing at " + BasilReadPath);
            return def;
        }

        /// <summary>A def whose lines name their own pool, so a composed line says which pools it came from.</summary>
        private static WeatherReadDef Tagged()
        {
            var def = ScriptableObject.CreateInstance<WeatherReadDef>();
            def.LightLines = new[] { "LIGHT {from}." };
            def.ModerateLines = new[] { "MODERATE {from}." };
            def.FreshLines = new[] { "FRESH {from}." };
            def.TooMuchLines = new[] { "TOOMUCH {from}." };
            def.GaleLines = new[] { "GALE {from}." };
            def.HoldingLines = new[] { "HOLDING" };
            def.BuildingLines = new[] { "BUILDING" };
            def.EasingLines = new[] { "EASING" };
            def.HomeEarlyLines = new[] { "HOMEEARLY" };
            def.StayInLines = new[] { "STAYIN" };
            def.StayInEasingLines = new[] { "STAYINEASING" };
            return def;
        }

        private static Vector2 Toward(float compassDeg, float strength)
        {
            float rad = compassDeg * Mathf.Deg2Rad;   // compass: clockwise from north (+y)
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * strength;
        }

        private static bool NamesWord(string line, string word) =>
            Regex.IsMatch(line, "(?<![\\w'])" + Regex.Escape(word) + "(?![\\w'])", RegexOptions.IgnoreCase);

        // ---- the table: hand-set winds ------------------------------------------------------------

        // Toward-bearing (compass deg), morning m/s, afternoon m/s -> the from-word, band, outlook.
        [TestCase(45f, 4f, 4f, "sou'west", "MODERATE", "HOLDING")]
        [TestCase(180f, 1f, 1f, "north", "LIGHT", "HOLDING")]
        [TestCase(270f, 7f, 3f, "east", "FRESH", "EASING")]
        [TestCase(0f, 3f, 9f, "south", "MODERATE", "HOMEEARLY")]
        [TestCase(135f, 9f, 9f, "nor'west", "TOOMUCH", "STAYIN")]
        [TestCase(90f, 12f, 5f, "west", "GALE", "STAYINEASING")]
        [TestCase(225f, 3f, 5f, "nor'east", "MODERATE", "BUILDING")]
        [TestCase(315f, 2.5f, 3f, "sou'east", "MODERATE", "HOLDING")]
        [TestCase(315f, 7f, 9f, "sou'east", "FRESH", "HOMEEARLY")]
        [TestCase(0f, 8.5f, 10f, "south", "TOOMUCH", "STAYIN")]
        public void Line_NamesTheSimulatedDirectionStrengthAndTrend(float towardDeg, float morning, float afternoon,
                                                                    string from, string band, string outlook)
        {
            var def = Tagged();
            var wind = new MorningAfternoonWind(Toward(towardDeg, morning), Toward(towardDeg, afternoon));
            for (int day = 0; day < 3; day++)
            {
                string line = WeatherRead.LineFor(def, wind, 12345, day, SecondsPerDay);
                Assert.AreEqual(band + " " + from + ". " + outlook, line, "day " + day);
            }
            UnityEngine.Object.DestroyImmediate(def);
        }

        [Test]
        public void FromPoint_IsTheBearingTheWindComesFrom()
        {
            // The vector points where the wind BLOWS; a wind blowing toward the north-east is a sou'wester.
            Assert.AreEqual(5, WeatherRead.FromPointOf(new Vector2(1f, 1f)));
            Assert.AreEqual(0, WeatherRead.FromPointOf(new Vector2(0f, -1f)));
            Assert.AreEqual(2, WeatherRead.FromPointOf(new Vector2(-1f, 0f)));
            Assert.AreEqual(4, WeatherRead.FromPointOf(new Vector2(0f, 1f)));
            Assert.AreEqual(6, WeatherRead.FromPointOf(new Vector2(1f, 0f)));
            for (int k = 0; k < 8; k++)
                for (float off = -20f; off <= 20f; off += 10f)
                    Assert.AreEqual(k, WeatherRead.FromPointOf(-Toward(k * 45f + off, 5f)), "point " + k + " off " + off);
        }

        [TestCase(WindBand.Light, WindBand.Light, 0f, WeatherOutlook.Holding)]
        [TestCase(WindBand.Moderate, WindBand.Fresh, 2f, WeatherOutlook.Building)]
        [TestCase(WindBand.Fresh, WindBand.Moderate, -2f, WeatherOutlook.Easing)]
        [TestCase(WindBand.Fresh, WindBand.TooMuch, 1f, WeatherOutlook.HomeEarly)]
        [TestCase(WindBand.TooMuch, WindBand.Gale, 4f, WeatherOutlook.StayIn)]
        [TestCase(WindBand.Gale, WindBand.Fresh, -6f, WeatherOutlook.StayInEasing)]
        [TestCase(WindBand.TooMuch, WindBand.TooMuch, -1f, WeatherOutlook.StayIn)]
        public void Outlook_JudgesTheDayForADory(WindBand now, WindBand later, float change, WeatherOutlook expected)
        {
            Assert.AreEqual(expected, WeatherRead.OutlookOf(now, later, change, 1.5f));
        }

        // ---- the real sim and Basil's real words ----------------------------------------------

        [Test]
        public void RealSim_BasilsLineMatchesTheWindForATableOfSeedsAndDays()
        {
            var def = Basil();
            var windDef = AssetDatabase.LoadAssetAtPath<WindProfileDef>(StPetersWindPath);
            Assert.IsNotNull(windDef, "St Peters wind profile missing");
            int[] seeds = { 1, 7, 42, 1234, 99991, -5 };
            double sph = SecondsPerDay / 24.0;
            var seenLines = new HashSet<string>();
            var seenBands = new HashSet<WindBand>();

            foreach (int seed in seeds)
            {
                var wind = new SimWind(seed, windDef.Profile);
                for (int day = 0; day < 14; day++)
                {
                    // The wind as the sim gives it, averaged here independently over the same looks.
                    Vector2 now = Mean(wind, day * (double)SecondsPerDay + def.ReadHour * sph, def, sph);
                    Vector2 later = Mean(wind, day * (double)SecondsPerDay + def.OutlookHour * sph, def, sph);
                    string expectedFrom = Compass[ExpectedPoint(now)];
                    WindBand expectedBand = def.BandOf(now.magnitude);
                    float change = later.magnitude - now.magnitude;
                    WeatherOutlook expectedOutlook =
                        WeatherRead.OutlookOf(expectedBand, def.BandOf(later.magnitude), change, def.ChangeAtLeast);

                    string line = WeatherRead.LineFor(def, wind, seed, day, SecondsPerDay);
                    string ctx = "seed " + seed + " day " + day + ": " + line;
                    Assert.IsNotNull(line, ctx);

                    // Direction: the sim's, and no other.
                    Assert.IsTrue(NamesWord(line, expectedFrom), ctx + " should name " + expectedFrom);
                    foreach (string other in Compass)
                        if (other != expectedFrom)
                            Assert.IsFalse(NamesWord(line, other), ctx + " names " + other);

                    // Strength and outlook: the line is one now line of the sim's band, then one outlook
                    // line of the sim's outlook, and nothing else.
                    var nows = def.NowLines(expectedBand).Select(l => l.Replace("{from}", expectedFrom));
                    var outs = def.OutlookLines(expectedOutlook);
                    Assert.IsTrue(nows.Any(n => outs.Any(o => line == n + " " + o)),
                        ctx + " is not a " + expectedBand + " / " + expectedOutlook + " line");

                    // Trend sanity against the raw numbers.
                    if (expectedOutlook == WeatherOutlook.Building) Assert.GreaterOrEqual(change, def.ChangeAtLeast, ctx);
                    if (expectedOutlook == WeatherOutlook.Easing || expectedOutlook == WeatherOutlook.StayInEasing)
                        Assert.LessOrEqual(change, -def.ChangeAtLeast, ctx);

                    seenLines.Add(line);
                    seenBands.Add(expectedBand);
                }
            }

            Assert.GreaterOrEqual(seenLines.Count, 20, "he repeats himself too much across " + seeds.Length * 14 + " days");
            Assert.GreaterOrEqual(seenBands.Count, 2, "the sweep only ever saw one wind band");
        }

        [Test]
        public void SameSeedAndDay_AlwaysGiveTheSameLine()
        {
            var def = Basil();
            var windDef = AssetDatabase.LoadAssetAtPath<WindProfileDef>(StPetersWindPath);
            for (int seed = 0; seed < 5; seed++)
                for (int day = 0; day < 10; day++)
                {
                    string a = WeatherRead.LineFor(def, new SimWind(seed, windDef.Profile), seed, day, SecondsPerDay);
                    string b = WeatherRead.LineFor(def, new SimWind(seed, windDef.Profile), seed, day, SecondsPerDay);
                    Assert.AreEqual(a, b, "seed " + seed + " day " + day);
                }
        }

        [Test]
        public void Pick_IsStableInRangeAndVariesByDay()
        {
            for (int count = 1; count <= 5; count++)
            {
                var hit = new HashSet<int>();
                for (int day = 0; day < 60; day++)
                {
                    int p = WeatherRead.Pick(77, day, 0x1234u, count);
                    Assert.AreEqual(p, WeatherRead.Pick(77, day, 0x1234u, count));
                    Assert.That(p, Is.InRange(0, count - 1));
                    hit.Add(p);
                }
                Assert.AreEqual(count, hit.Count, "count " + count + ": not every line gets a day");
            }
            Assert.That(WeatherRead.Pick(int.MinValue, int.MaxValue, 0u, 3), Is.InRange(0, 2));
        }

        [Test]
        public void NoServices_NoLine()
        {
            Assert.IsNull(WeatherRead.LineFor(null, new MorningAfternoonWind(Vector2.one, Vector2.one), 1, 1, SecondsPerDay));
            Assert.IsNull(WeatherRead.LineFor(Basil(), null, 1, 1, SecondsPerDay));
        }

        // ---- content: Basil's words ---------------------------------------------------------------

        [Test]
        public void BasilsRead_IsWiredAndWellFormed()
        {
            var def = Basil();
            StringAssert.IsMatch(@"^weather_read\.[a-z][a-z0-9_]*$", def.Id);
            Assert.IsTrue(def.BandsAscend, "band edges must climb: moderate < fresh < too much < gale");
            Assert.Less(def.ReadHour, def.OutlookHour, "the outlook is later in the day than the read");
            Assert.GreaterOrEqual(def.LookSamples, 1);

            Assert.AreEqual(8, def.CompassWords.Length, "eight compass points");
            Assert.AreEqual(8, def.CompassWords.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct().Count());

            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueDef>(BasilDialoguePath);
            Assert.IsNotNull(dialogue);
            Assert.AreSame(def, dialogue.WeatherRead, "Basil's dialogue does not carry his weather read");
        }

        [Test]
        public void BasilsRead_EveryBandAndOutlookHasLines()
        {
            var def = Basil();
            foreach (WindBand band in Enum.GetValues(typeof(WindBand)))
            {
                string[] pool = def.NowLines(band);
                Assert.IsNotEmpty(pool, band + " has no lines");
                foreach (string line in pool)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(line), band + " has a blank line");
                    Assert.AreEqual(1, Regex.Matches(line, Regex.Escape("{from}")).Count,
                        band + ": a now line names the direction exactly once, as {from}: " + line);
                    foreach (string word in def.CompassWords)
                        Assert.IsFalse(NamesWord(line, word), band + " line hard-codes a direction: " + line);
                }
            }
            foreach (WeatherOutlook outlook in Enum.GetValues(typeof(WeatherOutlook)))
            {
                string[] pool = def.OutlookLines(outlook);
                Assert.IsNotEmpty(pool, outlook + " has no lines");
                foreach (string line in pool)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(line), outlook + " has a blank line");
                    Assert.IsFalse(line.Contains("{from}"), outlook + ": the outlook does not know a direction: " + line);
                    foreach (string word in def.CompassWords)
                        Assert.IsFalse(NamesWord(line, word), outlook + " line names a direction: " + line);
                }
            }
        }

        /// <summary>A band's lines never use another band's strength words, so he cannot call a gale a
        /// light air.</summary>
        [Test]
        public void BasilsRead_NoBandNamesAnotherBandsStrength()
        {
            var def = Basil();
            var markers = new Dictionary<WindBand, string[]>
            {
                { WindBand.Light, new[] { "light", "breath", "calm", "flat" } },
                { WindBand.Moderate, new[] { "breeze" } },
                { WindBand.Fresh, new[] { "fresh", "chop" } },
                { WindBand.TooMuch, new[] { "hard", "too much" } },
                { WindBand.Gale, new[] { "gale" } },
            };
            foreach (WindBand band in Enum.GetValues(typeof(WindBand)))
                foreach (string line in def.NowLines(band))
                    foreach (var other in markers)
                    {
                        if (other.Key == band) continue;
                        foreach (string word in other.Value)
                            Assert.IsFalse(NamesWord(line, word),
                                band + " line uses the " + other.Key + " word '" + word + "': " + line);
                    }
        }

        // ---- independent arithmetic ---------------------------------------------------------------

        private static Vector2 Mean(IWaveWindSource wind, double centre, WeatherReadDef def, double sph)
        {
            int n = def.LookSamples;
            double window = def.LookWindowHours * sph;
            if (n <= 1 || window <= 0) return wind.WaveWindAt(centre).Wind;
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < n; i++)
                sum += wind.WaveWindAt(centre - window / 2 + window * i / (n - 1)).Wind;
            return sum / n;
        }

        private static int ExpectedPoint(Vector2 toward)
        {
            double bearing = Math.Atan2(-toward.x, -toward.y) * 180.0 / Math.PI;
            if (bearing < 0) bearing += 360.0;
            return (int)Math.Floor((bearing + 22.5) / 45.0) % 8;
        }
    }
}
