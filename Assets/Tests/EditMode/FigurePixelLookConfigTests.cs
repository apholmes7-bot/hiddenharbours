using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The figure pixel look's two tunables (<see cref="GameConfig.FigurePixelTarget"/>,
    /// <see cref="GameConfig.FigureHeadingSteps"/>): the shipped asset carries both at their ship values
    /// (ON, the owner's D5 of 10-18; 0, smooth), each code default is the value a key the asset LACKS
    /// loads as, and <see cref="GameServices"/> reads the defaults with no config and validates a
    /// negative dial to 0.
    ///
    /// <para>The absent-key case loads the REAL serialized asset with the keys taken out, not a fresh
    /// instance: a copy with a moved sentinel proves the file was the thing read, and a control copy with
    /// both keys set away from their defaults proves the loader reads them when they are there. With the
    /// switch's default ON it is no longer the type's zero, so that case now decides what a root key the
    /// file lacks loads as: the field initialiser, which <c>BoatHullPresenterSeamTests</c> measured
    /// standing on a root key, and not the type's zero of <c>GameConfigAssetCoverageTests</c>' trap.</para>
    /// </summary>
    public class FigurePixelLookConfigTests
    {
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const string SwitchKey = nameof(GameConfig.FigurePixelTarget);
        private const string StepsKey = nameof(GameConfig.FigureHeadingSteps);
        private const string SentinelKey = nameof(GameConfig.SecondsPerDay);
        private const float Sentinel = 1234f;

        [Test]
        public void TheShippedAsset_CarriesBothKeys_AtTheirShipValues()
        {
            string yaml = File.ReadAllText(ConfigAssetPath);
            Assert.AreEqual("1", RootValue(yaml, SwitchKey), "the switch ships ON (the owner's D5 of 10-18)");
            Assert.AreEqual("0", RootValue(yaml, StepsKey), "the dial ships at 0, smooth (the owner's D2 of 10-15)");
        }

        [Test]
        public void TheCodeDefaults_AreOnAndSmooth_AndOnlyTheDialIsTheTypesZero()
        {
            Assert.IsTrue(GameConfig.DefaultFigurePixelTarget, "the switch's code default is ON (the owner's D5 of 10-18)");
            Assert.AreEqual(0, GameConfig.DefaultFigureHeadingSteps, "the dial's code default is 0, smooth");
            // The dial's default is the type's zero, so an asset without its key reads 0 whichever value a
            // missing key loads as. The switch's is not: an asset without its key reads ON only while the
            // loader leaves the field initialiser standing, which the absent-key case below pins.
            Assert.AreNotEqual(default(bool), GameConfig.DefaultFigurePixelTarget, "the switch's default is not the type's zero");
            Assert.AreEqual(default(int), GameConfig.DefaultFigureHeadingSteps, "the dial's default is the type's zero");

            var fresh = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.AreEqual(GameConfig.DefaultFigurePixelTarget, fresh.FigurePixelTarget, "a fresh config: the switch");
                Assert.AreEqual(GameConfig.DefaultFigureHeadingSteps, fresh.FigureHeadingSteps, "a fresh config: the dial");
            }
            finally
            {
                Object.DestroyImmediate(fresh);
            }
        }

        [Test]
        public void TheRealAsset_WithBothKeysAbsent_LoadsTheCodeDefaults()
        {
            string yaml = File.ReadAllText(ConfigAssetPath);
            string marked = SetRoot(yaml, SentinelKey, Sentinel.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string absent = Strip(Strip(marked, SwitchKey), StepsKey);
            string present = SetRoot(SetRoot(marked, SwitchKey, "0"), StepsKey, "16");

            var loaded = new List<Object>();
            try
            {
                GameConfig control = LoadCopy(present, "the control copy (both keys set away from their defaults)", loaded);
                Assert.AreEqual(Sentinel, control.SecondsPerDay, 0f, "the control copy: its sentinel, so the file was read");
                Assert.IsFalse(control.FigurePixelTarget,
                               "the control copy: the loader reads the switch when it is there (an explicit 0 over the ON initialiser)");
                Assert.AreEqual(16, control.FigureHeadingSteps, "the control copy: the loader reads the dial when it is there");

                GameConfig shipped = LoadCopy(marked, "the shipped copy (switch 1, dial 0)", loaded);
                Assert.AreEqual(Sentinel, shipped.SecondsPerDay, 0f, "the shipped copy: its sentinel");
                Assert.IsTrue(shipped.FigurePixelTarget, "the shipped copy: an explicit 1 reads ON");
                Assert.AreEqual(0, shipped.FigureHeadingSteps, "the shipped copy: an explicit 0 reads 0");

                GameConfig missing = LoadCopy(absent, "the copy with both keys absent", loaded);
                Assert.AreEqual(Sentinel, missing.SecondsPerDay, 0f,
                                "the absent copy: its sentinel, so this is the file and not a fresh instance");
                Assert.AreEqual(GameConfig.DefaultFigurePixelTarget, missing.FigurePixelTarget,
                                "the absent copy: a missing switch loads as the code default, ON (the field " +
                                "initialiser stands), not as the type's zero");
                Assert.AreEqual(GameConfig.DefaultFigureHeadingSteps, missing.FigureHeadingSteps,
                                "the absent copy: a missing dial loads as the code default");
            }
            finally
            {
                foreach (Object o in loaded)
                    if (o != null && !EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
            }
        }

        [Test]
        public void GameServices_ReadsTheDefaults_WithNoConfig_AndValidatesANegativeDial()
        {
            GameConfig previous = GameServices.Config;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                GameServices.Config = null;
                Assert.AreEqual(GameConfig.DefaultFigurePixelTarget, GameServices.FigurePixelTarget, "no config: the switch's default");
                Assert.AreEqual(GameConfig.DefaultFigureHeadingSteps, GameServices.FigureHeadingSteps, "no config: the dial's default");

                GameServices.Config = config;
                config.FigurePixelTarget = true;
                config.FigureHeadingSteps = 32;
                Assert.IsTrue(GameServices.FigurePixelTarget, "the config's switch");
                Assert.AreEqual(32, GameServices.FigureHeadingSteps, "the config's dial");

                config.FigureHeadingSteps = -3;
                Assert.AreEqual(0, GameServices.FigureHeadingSteps, "a negative dial validates to 0, the identity");
                config.FigureHeadingSteps = int.MinValue;
                Assert.AreEqual(0, GameServices.FigureHeadingSteps, "the most negative dial validates to 0");
            }
            finally
            {
                GameServices.Config = previous;
                Object.DestroyImmediate(config);
            }
        }

        // ==================================================================== helpers

        /// <summary>The value of a root-level key (two-space indent), which must appear exactly once.</summary>
        private static string RootValue(string yaml, string key)
        {
            MatchCollection matches = Regex.Matches(yaml, "^  " + key + @":[ \t]*(\S+)[ \t]*\r?$", RegexOptions.Multiline);
            Assert.AreEqual(1, matches.Count, $"'{key}' must appear exactly once at the root of {ConfigAssetPath}");
            return matches[0].Groups[1].Value;
        }

        /// <summary>The YAML with a root-level key's whole line taken out.</summary>
        private static string Strip(string yaml, string key)
        {
            var line = new Regex("^  " + key + @":[^\r\n]*\r?\n", RegexOptions.Multiline);
            Assert.AreEqual(1, line.Matches(yaml).Count, $"'{key}' must be taken out exactly once");
            string stripped = line.Replace(yaml, string.Empty);
            Assert.IsFalse(Regex.IsMatch(stripped, "^ *" + key + ":", RegexOptions.Multiline), $"'{key}' is still in the copy");
            return stripped;
        }

        /// <summary>The YAML with a root-level key's value replaced.</summary>
        private static string SetRoot(string yaml, string key, string value)
        {
            var line = new Regex("^(  " + key + @":)[^\r\n]*", RegexOptions.Multiline);
            Assert.AreEqual(1, line.Matches(yaml).Count, $"'{key}' must be set exactly once");
            return line.Replace(yaml, "${1} " + value);
        }

        /// <summary>Write <paramref name="yaml"/> to a temporary file and load it as a serialized file, as
        /// the editor loads any text asset; the file is gone again before this returns.</summary>
        private static GameConfig LoadCopy(string yaml, string what, List<Object> loaded)
        {
            string path = FileUtil.GetUniqueTempPathInProject() + ".asset";
            try
            {
                File.WriteAllText(path, yaml);
                Object[] objects = InternalEditorUtility.LoadSerializedFileAndForget(path);
                if (objects != null) loaded.AddRange(objects);
                GameConfig config = objects?.OfType<GameConfig>().FirstOrDefault();
                Assert.IsNotNull(config, $"{what}: the copy loaded no GameConfig ({objects?.Length ?? 0} object(s))");
                return config;
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
