using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Owner ruling: the word the player reads is <b>bucket</b>, never <b>pail</b>. Every string field of
    /// every ScriptableObject under <c>Data/</c> and <c>Resources/</c> is scanned for the word "pail"
    /// (whole word, any case), except the fields that are never shown: ids and other keys, art tier and
    /// variant keys, bake-source references and the authoring notes (Basis, Provenance).
    ///
    /// <para><b>Why an exclusion list rather than a list of visible fields.</b> A new visible field
    /// (a flavor, a reply, a prompt) is covered the day it is added; a new internal field that happens to
    /// say "pail" fails here with its asset and property path, and is added to the exclusions on
    /// purpose. Ids and asset names keep "pail" where they already have it: ids are append-only and
    /// stable, and nobody reads them.</para>
    /// </summary>
    public class BucketNotPailContentValidationTests
    {
        static readonly string[] Roots = { "Assets/_Project/Data", "Assets/_Project/Resources" };

        static readonly Regex Pail = new Regex(@"\bpails?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Field names that are never shown to the player: keys, ids, art tiers, bake sources
        /// and authoring notes. Matched against the field's own name (the last segment of its path).</summary>
        static readonly Regex NeverShown = new Regex(
            @"^(m_.*|.*Id|.*Ids|Variant|.*Tier|.*TierId|Source.*|Basis|Provenance|.*Note|.*Notes|.*Path|.*Key)$",
            RegexOptions.CultureInvariant);

        [Test]
        public void NoPlayerVisibleDefStringSaysPail()
        {
            var hits = new List<string>();
            int scanned = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", Roots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null) continue;
                scanned++;

                using (var so = new SerializedObject(asset))
                {
                    SerializedProperty p = so.GetIterator();
                    while (p.Next(true))
                    {
                        if (p.propertyType != SerializedPropertyType.String) continue;
                        string value = p.stringValue;
                        if (string.IsNullOrEmpty(value) || !Pail.IsMatch(value)) continue;
                        if (NeverShown.IsMatch(FieldNameOf(p.propertyPath))) continue;
                        hits.Add($"{path} :: {p.propertyPath} = \"{value}\"");
                    }
                }
            }

            Assert.Greater(scanned, 0, "no Def assets were found, so nothing was checked");
            Assert.IsEmpty(hits,
                "the player reads 'bucket', never 'pail' (owner ruling). Change the display text, not " +
                "the id. If a field below is never shown, add its name to NeverShown:\n" +
                string.Join("\n", hits));
        }

        /// <summary>The field a property path ends in: <c>Options.Array.data[0].ReplyLines.Array.data[1]</c>
        /// is <c>ReplyLines</c>.</summary>
        static string FieldNameOf(string propertyPath)
        {
            string[] parts = propertyPath.Split('.');
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                string s = parts[i];
                if (s == "Array" || s.StartsWith("data[")) continue;
                return s;
            }
            return propertyPath;
        }
    }
}
