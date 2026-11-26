using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Owner ruling: the word the player reads is <b>bucket</b>, never <b>pail</b>. The Def assets are
    /// covered by <see cref="BucketNotPailContentValidationTests"/>; this covers the text that lives in a
    /// scene or a prefab instead (a sign board, a label, a prompt serialized on a component). Every
    /// <c>.unity</c> and <c>.prefab</c> under <c>Assets/_Project</c> is streamed as text and every
    /// serialized string field is checked for the word "pail" (whole word, any case), except the fields
    /// that are never shown: object names, ids and keys, art tiers and variants, bake sources and
    /// authoring notes. The same exclusion pattern as the Def test, so the two agree on what is shown.
    ///
    /// <para>A line that says "pail" but carries no field name of its own (the second line of a wrapped
    /// string) is flagged too: its field cannot be told from the line, so it is treated as shown.</para>
    /// </summary>
    public class BucketNotPailSceneTextTests
    {
        const string Root = "Assets/_Project";

        static readonly Regex Pail = new Regex(@"\bpails?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>A serialized field line: indentation, an optional list dash, the field name, a colon.</summary>
        static readonly Regex FieldLine = new Regex(@"^\s*(?:-\s+)?([A-Za-z_][A-Za-z0-9_]*):(?:\s|$)",
            RegexOptions.CultureInvariant);

        /// <summary>Field names never shown to the player. Kept in step with the Def test's list.</summary>
        static readonly Regex NeverShown = new Regex(
            @"^(m_Name|m_EditorClassIdentifier|.*Id|.*Ids|Variant|.*Tier|.*TierId|Source.*|Basis|Provenance|.*Note|.*Notes|.*Path|.*Key|propertyPath|objectReference)$",
            RegexOptions.CultureInvariant);

        [Test]
        public void NoPlayerVisibleSceneOrPrefabStringSaysPail()
        {
            var hits = new List<string>();
            int scanned = 0;

            foreach (string pattern in new[] { "*.unity", "*.prefab" })
            {
                foreach (string file in Directory.EnumerateFiles(Root, pattern, SearchOption.AllDirectories))
                {
                    scanned++;
                    int lineNo = 0;
                    foreach (string line in File.ReadLines(file))
                    {
                        lineNo++;
                        if (line.IndexOf("ail", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (!Pail.IsMatch(line)) continue;
                        Match field = FieldLine.Match(line);
                        if (field.Success && NeverShown.IsMatch(field.Groups[1].Value)) continue;
                        hits.Add($"{file.Replace('\\', '/')}:{lineNo}: {line.Trim()}");
                    }
                }
            }

            Assert.Greater(scanned, 0, "no scenes or prefabs were found, so nothing was checked");
            Assert.IsEmpty(hits,
                "the player reads 'bucket', never 'pail' (owner ruling). Change the shown text, not the " +
                "id. If a field below is never shown, add its name to NeverShown:\n" +
                string.Join("\n", hits));
        }

        [TestCase("  _signText: Pail of clams", true)]
        [TestCase("    - Fresh pails here", true)]
        [TestCase("  Label: A pail.", true)]
        [TestCase("  m_Name: HandPail", false)]
        [TestCase("  Id: carrier.hand_pail", false)]
        [TestCase("  BucketTierId: pail", false)]
        [TestCase("  _label: Bucket", false)]
        [TestCase("  _label: Pailful", false)]
        public void TheLineRuleFlagsOnlyShownPail(string line, bool flagged)
        {
            bool hit = Pail.IsMatch(line);
            if (hit)
            {
                Match field = FieldLine.Match(line);
                if (field.Success && NeverShown.IsMatch(field.Groups[1].Value)) hit = false;
            }
            Assert.AreEqual(flagged, hit, line);
        }
    }
}
