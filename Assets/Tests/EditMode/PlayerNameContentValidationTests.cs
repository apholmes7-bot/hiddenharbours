using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class PlayerNameContentValidationTests
    {
        // One registry, including the base's existing bindings (the charter's Decision 2 amendment).
        private const string KnownTokens =
            "{player}|" +                         // Core/Save/PlayerName.cs, through PlayerName.Of.
            "{payout}|{units}|" +                 // World/DialogueOption.cs; DialoguePresenter.CounterSellAnswer.
            "{species1}|{price1}|{species2}|{price2}|" + // Art/SetPieceDef.cs, Words.Format for market.today.
            "{Submit}|{Cancel}";                  // Input/HiddenHarbours.inputactions, Input System usage bindings.

        private static readonly HashSet<string> Allowed = new HashSet<string>(KnownTokens.Split('|'), StringComparer.Ordinal);
        // A placeholder holds only an identifier. Colons, commas and whitespace belong to
        // builder objects; placeholders inside imported strings are still inspected.
        private static readonly Regex Token = new Regex(@"\{[A-Za-z0-9_.]+\}");

        public static List<string> Problems(string text, string path)
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(text)) return problems;
            foreach (Match match in Token.Matches(text))
                if (!Allowed.Contains(match.Value)) problems.Add(path + ": unknown text token " + match.Value);
            return problems;
        }

        [Test]
        public void EveryDataStringUsesOnlyKnownTokens()
        {
            var problems = new List<string>();
            int inspected = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset == null) continue;
                    inspected++;
                    using (var serialized = new SerializedObject(asset))
                    {
                        var property = serialized.GetIterator();
                        while (property.Next(true))
                            if (property.propertyType == SerializedPropertyType.String)
                                problems.AddRange(Problems(property.stringValue, path + ":" + property.propertyPath));
                    }
                }
            }
            Assert.Greater(inspected, 0, "The Data token sweep inspected no assets.");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [TestCase("{player}")]
        [TestCase("{payout} {units}")]
        [TestCase("{species1} {price1} {species2} {price2}")]
        [TestCase("{\"format\":[\"{species1} {price1}\"]}")]
        public void ExistingBindingsAreAllowed(string text)
            => Assert.That(Problems(text, "fixture.asset"), Is.Empty);

        [TestCase("{name}")]
        [TestCase("{Player}")]
        [TestCase("{player_name}")]
        [TestCase("{no_such_token}")]
        public void UnknownTokensFailWithTheirAssetAndSpelling(string token)
        {
            var problems = Problems("Hello " + token, "fixture.asset");
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("fixture.asset", problems[0]);
            StringAssert.Contains(token, problems[0]);
        }
    }
}
