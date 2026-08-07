using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The ladder-boarding tunables on the shipped GameConfig</b>, pinned two ways.
    ///
    /// <para><b>Why the raw-YAML test exists.</b> Unity serializes a struct field by writing the keys it
    /// finds; a key ABSENT from the asset deserializes to <b>0</b> silently, and nothing fails. A
    /// <c>ClimbLoopSeconds</c> of 0 would make every climb instantaneous and a <c>RungMetres</c> of 0
    /// would make it infinite — both with a green suite and a shipped asset that merely looks short. So
    /// the asset's TEXT is asserted to carry every key by name, not just its loaded values.</para>
    ///
    /// <para><b>And the guard-rail test</b> follows the RodFight/DisplacedWater precedent: exact pins on
    /// the CODE defaults, bounds only on the owner's tuned asset. His tuning is his; what this defends is
    /// that the tuning is still a boarding rule and not a division by zero.</para>
    /// </summary>
    public class LadderBoardingConfigTests
    {
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        /// <summary>Every field of <see cref="LadderBoardingSettings"/>, by the name Unity writes into the
        /// asset. Derived by reflection so ADDING a field to the struct and forgetting to add it to the
        /// asset fails here rather than shipping as a silent zero.</summary>
        private static string[] FieldNames()
        {
            var fields = typeof(LadderBoardingSettings).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var names = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++) names[i] = fields[i].Name;
            return names;
        }

        private static string ConfigText()
        {
            string path = Path.Combine(Application.dataPath, "..", ConfigAssetPath);
            Assert.IsTrue(File.Exists(path), "the shared GameConfig asset is missing at " + path);
            return File.ReadAllText(path);
        }

        /// <summary>The block the settings serialize into, so a key belonging to a NEIGHBOURING block
        /// cannot satisfy the search by accident.</summary>
        private static string LadderBlock()
        {
            string text = ConfigText();
            Match block = Regex.Match(text, @"^  LadderBoarding:\r?\n((?:    .*\r?\n)+)",
                                      RegexOptions.Multiline);
            Assert.IsTrue(block.Success,
                "the shipped GameConfig has NO LadderBoarding block at all — every tunable is reading 0, " +
                "which means no gap ever needs a ladder and any climb that started would never end");
            return block.Groups[1].Value;
        }

        [Test]
        public void TheShippedAsset_CarriesEveryLadderKeyByName()
        {
            string block = LadderBlock();
            foreach (string name in FieldNames())
                StringAssert.Contains(name + ":", block,
                    $"'{name}' is missing from the GameConfig asset's LadderBoarding block. Unity " +
                    "deserializes an absent key to 0 WITHOUT failing — write the key with its default.");
        }

        /// <summary>And the values must be the code defaults: this feature has not been owner-tuned yet,
        /// so any difference is a hand-edit or a half-written block rather than a decision.</summary>
        [Test]
        public void TheShippedAsset_MatchesTheCodeDefaults()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(config, "the shared GameConfig asset must exist at " + ConfigAssetPath);

            var shipped = config.LadderBoarding;
            var code = LadderBoardingSettings.Default;

            Assert.AreEqual(code.BoardClampMetres, shipped.BoardClampMetres, 1e-4f);
            Assert.AreEqual(code.LadderReachMetres, shipped.LadderReachMetres, 1e-4f);
            Assert.AreEqual(code.RungMetres, shipped.RungMetres, 1e-4f);
            Assert.AreEqual(code.StandoffMetres, shipped.StandoffMetres, 1e-4f);
            Assert.AreEqual(code.ClimbLoopSeconds, shipped.ClimbLoopSeconds, 1e-4f);
            Assert.AreEqual(code.TransitionSeconds, shipped.TransitionSeconds, 1e-4f);
        }

        /// <summary>The guard-rail: whatever the owner tunes it to, it must still describe a climb that
        /// happens and ends.</summary>
        [Test]
        public void TheShippedAsset_StaysInsideWhatAClimbCanMean()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(config);
            var s = config.LadderBoarding;

            Assert.Greater(s.RungMetres, 0f,
                "a ladder with no rung spacing gives an infinitely long climb (gap ÷ 0)");
            Assert.Greater(s.ClimbLoopSeconds, 0f,
                "a zero loop length makes every climb instantaneous — the exact silent-zero this suite " +
                "exists to catch");
            Assert.GreaterOrEqual(s.BoardClampMetres, 0f);
            Assert.GreaterOrEqual(s.LadderReachMetres, 0f);
            Assert.GreaterOrEqual(s.TransitionSeconds, 0f);
        }

        /// <summary>
        /// <b>The kit disagrees with itself about this number, and that must not be forgotten quietly.</b>
        ///
        /// <para><c>characterIsoRig6.js</c> cites <b>1.2 m</b> as where its <c>board</c> clip gives way to
        /// a ladder, measured on the deck-to-WATER drop. <c>wharfIsoRig.js</c> IMPLEMENTS a stricter
        /// <b>0.55 m</b>, on <c>drop − freeboard</c> — the deck-to-GUNWALE gap, which is the quantity
        /// <see cref="LadderBoardingSettings.BoardClampMetres"/> actually compares. We ship 1.2 (see that
        /// field's tooltip for the argument). This test scrapes BOTH rigs so the day either number moves,
        /// the choice is re-made deliberately instead of drifting.</para>
        /// </summary>
        [Test]
        public void BothRigsStillStateTheThresholdsThisChoiceWasMadeBetween()
        {
            string character = File.ReadAllText(
                Path.Combine(Application.dataPath, "..", "docs/art/rigs/characterIsoRig6.js"));
            StringAssert.Contains("drop > 1.2 m", character,
                "characterIsoRig6.js no longer cites 1.2 m as its board/ladder boundary — the shipped " +
                "BoardClampMetres default was chosen from that line, so re-make the choice");

            string wharf = File.ReadAllText(Path.Combine(
                Application.dataPath, "..", "docs/art/rigs/iso-rig-pack/wharf-kit-iso/wharfIsoRig.js"));
            StringAssert.Contains("drop - bt.freeboard > 0.55", wharf,
                "wharfIsoRig.js no longer implements needsLadder as a 0.55 m gunwale gap — that is the " +
                "stricter rule the owner can dial BoardClampMetres to, and it must stay documented");

            Assert.AreEqual(1.2f, LadderBoardingSettings.Default.BoardClampMetres, 1e-4f,
                "we ship the character rig's generous 1.2 m so a step aboard survives the top of the " +
                "tide; 0.55 is the one-number swap to the wharf rig's stricter rule");
        }
    }
}
