using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE SILENT-ZERO TRAP, killed for every field at once. A serialized field that
    /// <see cref="GameConfig"/> declares but the shipped <c>GameConfig.asset</c>'s YAML does not
    /// carry deserializes to the C# TYPE default (0 / false / a zeroed struct), NOT to the field
    /// initializer or the struct's <c>.Default</c> — so the feature the block drives reads as
    /// simply not working, with no error anywhere. #420 hit it on <c>Anchor</c>; the owner's
    /// 2026-08-19 walk hit it again when the wheel zoom shipped dead because the asset lagged the
    /// code by one block.
    ///
    /// <para><b>Nothing upstream ever closes the gap by itself.</b> The scene builders reach the
    /// asset through <c>LoadOrCreate</c>, which runs its initializer ONLY when the asset is absent
    /// — a shipped asset never gains a new block from a rebuild (the LoadOrCreate trap). And Unity
    /// only rewrites the full field list when a person edits the asset in an inspector and saves,
    /// which no pipeline guarantees. So a new GameConfig field must be HAND-ADDED to the asset's
    /// YAML in the same PR that declares it, and this test is what notices when it wasn't.</para>
    ///
    /// <para>Per-block tripwires already existed (<c>AnchorMathTests</c>,
    /// <c>DisplacedDefaultTests</c>); this is the general one: reflection walks every serialized
    /// field the type declares — recursing into HiddenHarbours settings structs — and demands a
    /// matching key in the shipped YAML. Rule 6 depends on it: a tunable the owner cannot see in
    /// the asset is not a tunable, it is a zero.</para>
    /// </summary>
    public class GameConfigAssetCoverageTests
    {
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        [Test]
        public void TheShippedAsset_CarriesEverySerializedFieldTheCodeDeclares()
        {
            string[] lines = ReadAssetLines();
            int root = Array.FindIndex(lines, l => l.StartsWith("MonoBehaviour:", StringComparison.Ordinal));
            Assert.GreaterOrEqual(root, 0, $"{ConfigAssetPath} has no MonoBehaviour document");

            var missing = new List<string>();
            CheckBlock(typeof(GameConfig), "GameConfig", lines, root, indent: 2, missing);

            Assert.IsEmpty(missing,
                "GameConfig.asset's YAML omits these serialized fields, so in play they read as " +
                "ZERO/false — not as the code's initializer or the struct's .Default. Hand-add each " +
                "key to the asset with the code's intended value (do NOT let Unity re-serialize the " +
                "whole file), in the same PR that declared the field:\n  " +
                string.Join("\n  ", missing));
        }

        [Test]
        public void TheShippedAsset_CarriesNoKeyTheCodeNoLongerDeclares()
        {
            // The same drift, mirrored: a root key with no matching field is usually a rename that
            // forgot FormerlySerializedAs (the owner's tuned value silently stops being read) or a
            // retired field whose YAML lingers as dead weight.
            string[] lines = ReadAssetLines();
            int root = Array.FindIndex(lines, l => l.StartsWith("MonoBehaviour:", StringComparison.Ordinal));
            Dictionary<string, int> keys = KeysInBlock(lines, root + 1, indent: 2);

            // Parser sanity: an empty key set would make this test pass vacuously. The sibling
            // test cannot false-green that way (an empty parse fails it loudly), but this one can.
            Assert.IsTrue(keys.ContainsKey(nameof(GameConfig.SecondsPerDay)),
                "the YAML parser found no root keys at all — the asset's layout changed under it");

            var declared = new HashSet<string>(SerializedFields(typeof(GameConfig)).Select(f => f.Name));
            string[] stale = keys.Keys
                .Where(k => !k.StartsWith("m_", StringComparison.Ordinal) && !declared.Contains(k))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();

            Assert.IsEmpty(stale,
                "GameConfig.asset carries root keys that GameConfig no longer declares — a rename " +
                "missing [FormerlySerializedAs], or a retired field whose YAML was never removed: " +
                string.Join(", ", stale));
        }

        // ---- the walk -------------------------------------------------------------------------

        private static void CheckBlock(Type type, string label, string[] lines, int blockLine,
                                       int indent, List<string> missing)
        {
            Dictionary<string, int> keys = KeysInBlock(lines, blockLine + 1, indent);
            foreach (FieldInfo f in SerializedFields(type))
            {
                if (!keys.TryGetValue(f.Name, out int keyLine))
                {
                    missing.Add($"{label}.{f.Name}");
                    continue;
                }
                // HiddenHarbours settings structs serialize as nested block mappings, and a sub-key
                // the block omits zeroes just as silently as a missing block (#420's exact shape) —
                // so recurse. Everything else (scalars, enums, Colors, asset refs, lists) is one
                // line per key and the presence check above is the whole guarantee.
                if (SerializesAsNestedBlock(f.FieldType))
                    CheckBlock(f.FieldType, $"{label}.{f.Name}", lines, keyLine, indent + 2, missing);
            }
        }

        /// <summary>The serialized fields reflection sees the way Unity does: public instance
        /// fields not marked [NonSerialized], plus [SerializeField] privates.</summary>
        private static IEnumerable<FieldInfo> SerializedFields(Type t)
        {
            return t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(f => !f.IsStatic && !f.IsInitOnly && !f.IsLiteral)
                    .Where(f => !f.IsDefined(typeof(NonSerializedAttribute), false))
                    .Where(f => f.IsPublic || f.IsDefined(typeof(SerializeField), false))
                    .Where(f => !typeof(Delegate).IsAssignableFrom(f.FieldType));
        }

        private static bool SerializesAsNestedBlock(Type t)
        {
            if (t.IsEnum || t.IsPrimitive || t == typeof(string)) return false;
            if (t.IsArray || t.IsGenericType) return false;                    // lists: presence only
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return false;  // asset ref: {fileID:}
            return t.Namespace != null && t.Namespace.StartsWith("HiddenHarbours", StringComparison.Ordinal);
        }

        // ---- the YAML side --------------------------------------------------------------------

        private static string[] ReadAssetLines()
        {
            string path = Path.Combine(Application.dataPath, "..", ConfigAssetPath);
            Assert.IsTrue(File.Exists(path), $"missing: {ConfigAssetPath}");
            return File.ReadAllLines(path);
        }

        /// <summary>Mapping keys at exactly <paramref name="indent"/> spaces, scanning from
        /// <paramref name="firstLine"/> until indentation falls below the block. List entries
        /// (<c>- </c> lines) are entries, not keys, and deeper lines belong to nested blocks.</summary>
        private static Dictionary<string, int> KeysInBlock(string[] lines, int firstLine, int indent)
        {
            var keys = new Dictionary<string, int>();
            var keyRx = new Regex(@"^ {" + indent + @"}([A-Za-z_][A-Za-z0-9_]*):");
            for (int i = firstLine; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0) continue;
                string trimmed = line.TrimStart(' ');
                int lineIndent = line.Length - trimmed.Length;
                if (lineIndent < indent) break;
                if (lineIndent > indent || trimmed[0] == '-') continue;
                Match m = keyRx.Match(line);
                if (m.Success) keys[m.Groups[1].Value] = i;
            }
            return keys;
        }
    }
}
