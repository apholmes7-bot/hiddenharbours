using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Words the player reads never carry a note meant for the team.</b> The clam bucket's catalog
    /// blurb once ended in "HOLD NOTE: the 20-unit capacity is ..." and a dozen species and baits ended in
    /// "BALANCE NOTE: placeholder value ..." — all of it drawn on the page, in the seller's hand, for the
    /// player to read. Nothing failed: the text was non-empty, which is all the other validators ask.
    ///
    /// <para>This sweeps every Def type whose text reaches the player (catalog listings, species,
    /// inner-voice lines, dialogue, conversations, traits) over the real assets in <c>Data/</c>, walks
    /// the player-facing string fields (and the nested option / line records that hold them), and fails
    /// on authoring markers: an all-caps <c>... NOTE</c>, HOLD / TODO / FIXME / TBD / OWED, and the
    /// internal vocabulary (placeholder, greybox, working name, a role name, seat, owner). It also holds
    /// the 2026-11-25 word ruling: the clam carrier is a <i>bucket</i>, never a pail.</para>
    ///
    /// <para>Tuning and wiring notes belong in a code comment, a tooltip or a doc, not in Flavor. If a
    /// genuine in-world line ever needs one of the flagged words, reword it or add a narrow allowance
    /// here with the reason; do not widen the regex.</para>
    ///
    /// <para>Types are named as strings and found through <c>AssetDatabase</c>, so this file needs no
    /// reference to the feature modules that own them (rule 4).</para>
    /// </summary>
    public class PlayerTextContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        /// <summary>The Def types whose text is drawn for the player.</summary>
        private static readonly string[] PlayerTextTypes =
        {
            // catalog listings: name + blurb on the catalog page
            "GearOffer", "PotOffer", "BaitDef", "SupplyDef", "InstrumentOffer", "LicenseDef", "VehicleOffer",
            "HomeDef", "FuelStationDef",
            // things in hand / on the line
            "FishSpeciesDef", "TackleDef", "ToolDef", "StowageDef",
            // spoken words
            "InnerVoiceLineDef", "DialogueDef", "ConversationDef",
            // the creator
            "TraitDef",
        };

        /// <summary>Field names that hold player-facing words on those types and their nested records.</summary>
        private static readonly HashSet<string> PlayerTextFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "DisplayName", "Flavor", "Line", "Label", "Text",
            "FirstLines", "RepeatLines", "ConditionalLines", "ReplyLines", "NothingToSellLines",
        };

        /// <summary>Extra player-facing fields that are only player-facing on one type.</summary>
        private static readonly Dictionary<string, string[]> ExtraFieldsByType = new Dictionary<string, string[]>
        {
            { "TraitDef", new[] { "Description" } },   // shown beside the trait in the creator
        };

        private static readonly Regex InternalMarker = new Regex(
            @"\b(HOLD|TODO|FIXME|TBD|OWED|XXX)\b|\b[A-Z]{3,} NOTE\b",
            RegexOptions.CultureInvariant);

        private static readonly Regex InternalWord = new Regex(
            @"\b(placeholder|greybox|working name|stopgap|economy-sim|gameplay-systems|world-content|ui-ux|art-pipeline|seat|owner)\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>A content id or a code word spelled out in prose ("Capacity is on the stowage.rod_rack Def").</summary>
        private static readonly Regex CodeTalk = new Regex(
            @"\b(gear|fish|stowage|tool|bait|supply|seller|region|npc|item|boat)\.[a-z_]+\b|\bDef\b|\bGearTag\b",
            RegexOptions.CultureInvariant);

        private static readonly Regex Pail = new Regex(@"\bpails?\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>Why <paramref name="text"/> would be wrong in front of the player, or null.</summary>
        internal static string Problem(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            Match m = InternalMarker.Match(text);
            if (m.Success) return $"internal marker '{m.Value}'";
            m = InternalWord.Match(text);
            if (m.Success) return $"internal word '{m.Value}'";
            m = CodeTalk.Match(text);
            if (m.Success) return $"code talk '{m.Value}'";
            m = Pail.Match(text);
            if (m.Success) return $"'{m.Value}' (the word is bucket)";
            return null;
        }

        // ---- the rule itself ----------------------------------------------------------------------

        [Test]
        public void Problem_FlagsTheOldClamBucketNote_AndPassesTheShippedLine()
        {
            Assert.IsNotNull(Problem("Holds what you dig - a 20-clam pail. HOLD NOTE: the 20-unit capacity is " +
                                     "gameplay-systems' IHold mechanic."));
            Assert.IsNotNull(Problem("Oily and cheap. PRICE NOTE: greybox placeholder - flagged for economy-sim to tune."));
            Assert.IsNotNull(Problem("Take the pail."));
            Assert.IsNotNull(Problem("NAME OWED: the owner names the water."));
            Assert.IsNotNull(Problem("Capacity is on the stowage.rod_rack Def."));
            Assert.IsNull(Problem("A galvanised bucket for the flats. Twenty clams to a load, carried home by hand " +
                                  "until you have a boat to carry them."));
            Assert.IsNull(Problem("A heading-hold unit for a hull with an offered helm."),
                "lower-case 'hold' is a real word; only the all-caps marker is a note");
        }

        // ---- the real content ---------------------------------------------------------------------

        [Test]
        public void PlayerFacingText_CarriesNoInternalNotes()
        {
            var problems = new List<string>();
            int assets = 0, strings = 0;
            foreach (string typeName in PlayerTextTypes)
            {
                ExtraFieldsByType.TryGetValue(typeName, out string[] extra);
                foreach (string guid in AssetDatabase.FindAssets("t:" + typeName, new[] { DataRoot }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset == null || asset.GetType().Name != typeName) continue;
                    assets++;
                    Walk(asset, asset.GetType().Name, extra, 0, path, problems, ref strings);
                }
            }

            Assert.Greater(assets, 0, "found no player-text Defs under " + DataRoot + " - the type list is stale");
            Assert.Greater(strings, 0, "read no player-facing strings - the field list is stale");
            Assert.IsEmpty(problems,
                $"{problems.Count} player-facing string(s) carry a note meant for the team (move it to a code " +
                "comment, tooltip or doc):\n  " + string.Join("\n  ", problems));
        }

        private static void Walk(object obj, string where, string[] extra, int depth, string path,
                                 List<string> problems, ref int strings)
        {
            if (obj == null || depth > 4) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type t = obj.GetType(); t != null && t != typeof(ScriptableObject) && t != typeof(object); t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(flags | BindingFlags.DeclaredOnly))
                {
                    if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                    if (f.IsNotSerialized) continue;
                    bool named = PlayerTextFields.Contains(f.Name) || (extra != null && Array.IndexOf(extra, f.Name) >= 0);
                    object value = f.GetValue(obj);
                    if (value == null) continue;
                    string at = where + "." + f.Name;

                    if (value is string s)
                    {
                        if (named) Check(s, at, path, problems, ref strings);
                        continue;
                    }
                    if (value is Object) continue;   // references are other assets; they are swept on their own
                    if (value is IList list)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            object item = list[i];
                            if (item is string si) { if (named) Check(si, at + "[" + i + "]", path, problems, ref strings); }
                            else if (IsRecord(item)) Walk(item, at + "[" + i + "]", null, depth + 1, path, problems, ref strings);
                        }
                        continue;
                    }
                    if (IsRecord(value)) Walk(value, at, null, depth + 1, path, problems, ref strings);
                }
            }
        }

        private static bool IsRecord(object o)
        {
            if (o == null || o is Object) return false;
            Type t = o.GetType();
            return !t.IsPrimitive && !t.IsEnum && t.Namespace != null
                   && t.Namespace.StartsWith("HiddenHarbours", StringComparison.Ordinal)
                   && t.IsDefined(typeof(SerializableAttribute), false);
        }

        private static void Check(string text, string at, string path, List<string> problems, ref int strings)
        {
            strings++;
            string why = Problem(text);
            if (why != null) problems.Add($"{path} {at}: {why}");
        }
    }
}
