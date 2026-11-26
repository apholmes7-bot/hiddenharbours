using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Economy;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Field = HiddenHarbours.Tests.EditMode.FirstWeekTextGuard.TextField;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Read-only inventory of the island's authored words, including dormant branches of speech.</summary>
    public sealed class FirstWeekTextCorpus
    {
        public const string BoundsPath = "Assets/Tests/EditMode/Fixtures/first-week-text-bounds.json";
        public const string ExceptionsPath = "Assets/Tests/EditMode/Fixtures/first-week-text-exceptions.json";
        const string DataRoot = "Assets/_Project/Data";

        [Serializable]
        public sealed class Bounds
        {
            public string Comment;
            public string BootScenePath;
            public string[] ProspectiveAssets;
        }

        public readonly List<Field> Fields = new List<Field>();
        public readonly List<FirstWeekTextGuard.Place> Outside = new List<FirstWeekTextGuard.Place>();
        public readonly HashSet<string> InsideIds = new HashSet<string>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly HashSet<string> _fieldKeys = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _visitedFiles = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _npcIds = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _sellerIds = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _itemIds = new HashSet<string>(StringComparer.Ordinal);

        static readonly HashSet<string> SpeechKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "NpcDef", "DialogueDef", "ConversationDef", "InnerVoiceLineDef", "StoryGateDef",
            "QuestDef", "KnowledgePageDef", "NotebookGearPageDef", "GiftConfig"
        };
        static readonly Regex Reference = new Regex(@"guid: ([a-f0-9]{32})", RegexOptions.CultureInvariant);
        static readonly Regex Property = new Regex(@"^( +)([A-Za-z_][A-Za-z0-9_]*):(?: (.*))?$", RegexOptions.CultureInvariant);

        // These are prose-bearing fields, not ids, object names, flags, paths or authoring descriptions.
        public static bool IsTextField(string path)
        {
            string field = Regex.Replace(path, @"\.Array\.data\[\d+\]|\[\d+\]", "");
            if (field.StartsWith("Gifts.Lines.", StringComparison.Ordinal) || field.StartsWith("Lines.", StringComparison.Ordinal)) return true;
            string leaf = field.Substring(field.LastIndexOf('.') + 1).TrimStart('_');
            if (leaf == "m_Text" || leaf == "m_text") return true;
            if (leaf.StartsWith("m_", StringComparison.Ordinal)) return false;
            return Regex.IsMatch(leaf, @"^(DisplayName|Name|Title|Context|Head|Heading|OtherHeading|Body|TabLabel|Flavor|Flavour|Text|Caption|Label|Speaker|Line|Lines|FirstLines|RepeatLines|ConditionalLines|ReplyLines|NothingToSellLines|TaskNote|SpokenHint|OutsideHoursLine|AllHoldNote|DemoAfterLine|.*Text|.*Label|.*Prompt|.*Message|.*Hint|.*Caption|.*Line)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        public static FirstWeekTextCorpus Read()
        {
            var corpus = new FirstWeekTextCorpus();
            var bounds = JsonUtility.FromJson<Bounds>(File.ReadAllText(BoundsPath));
            Assert.That(bounds.BootScenePath, Is.Not.Null.And.Not.Empty, BoundsPath);
            Assert.That(EditorBuildSettings.scenes.First(s => s.enabled).path, Is.EqualTo(bounds.BootScenePath),
                "The demo draft uses the first build scene. Review the boundary fixture when that changes.");
            foreach (var region in All<RegionDef>())
            {
                if (region.SceneName == Path.GetFileNameWithoutExtension(bounds.BootScenePath)) corpus.InsideIds.Add(region.Id);
                else corpus.Outside.Add(new FirstWeekTextGuard.Place(region.Id, region.DisplayName));
            }
            Assert.That(corpus.InsideIds.Count, Is.EqualTo(1), "The boundary must resolve to one RegionDef.");
            Assert.That(corpus.Outside, Is.Not.Empty, "An empty outside set would disable the guard.");

            corpus.ReadSceneOrPrefab(bounds.BootScenePath);
            // Include weekly visitors too, even if their home region is elsewhere.
            foreach (var routine in All<RoutineDef>())
            {
                using (var serialized = new SerializedObject(routine))
                {
                    var property = serialized.GetIterator();
                    bool visits = false;
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.String && property.name == "RegionId"
                            && corpus.InsideIds.Contains(property.stringValue)) visits = true;
                    if (visits) corpus.AddAsset(routine.Npc, "Dialogue");
                }
            }
            foreach (var seller in All<SellerDef>())
                if (corpus._npcIds.Contains(seller.KeeperNpcId)) corpus._sellerIds.Add(seller.Id);

            foreach (var page in All<QuestDef>()) corpus.AddAsset(page, "Notebook");
            foreach (var page in All<KnowledgePageDef>()) corpus.AddAsset(page, "Notebook");
            foreach (var page in All<NotebookGearPageDef>()) corpus.AddAsset(page, "Notebook");
            foreach (var exchange in All<ConversationDef>())
                if (corpus.InsideIds.Contains(exchange.RegionId)) corpus.AddAsset(exchange, "Dialogue");
            foreach (var line in All<InnerVoiceLineDef>())
                if (string.IsNullOrEmpty(line.RegionId) || corpus.InsideIds.Contains(line.RegionId)) corpus.AddAsset(line, "Inner voice");

            // The gate type is in a separate draft. Serialized fields keep this check compiling before it lands.
            foreach (var gate in AssetsOfKind("StoryGateDef"))
            {
                using (var serialized = new SerializedObject(gate))
                {
                    var npc = serialized.FindProperty("NpcId");
                    if (npc == null || !corpus._npcIds.Contains(npc.stringValue)) continue;
                    corpus.AddAsset(gate, "Story gate");
                    var demoWord = serialized.FindProperty("DemoWord");
                    var word = demoWord != null && demoWord.objectReferenceValue != null ? demoWord : serialized.FindProperty("Word");
                    if (word != null) corpus.AddAsset(word.objectReferenceValue, "Story gate");
                }
            }

            foreach (var fish in All<HiddenHarbours.Fishing.FishSpeciesDef>())
                if (corpus.InsideIds.Any(fish.RegionAllowed)) corpus.AddAsset(fish, "Item flavour");

            var catalog = Resources.LoadAll<ScriptableObject>(CatalogSource.CatalogFolder);
            foreach (var asset in catalog)
                if (asset is ICatalogListing listing && corpus._sellerIds.Any(listing.Catalog.IsStockedBy))
                {
                    corpus._itemIds.Add(listing.ListingId);
                    corpus.AddAsset(asset, "Item flavour");
                }
            foreach (string path in bounds.ProspectiveAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.That(asset, Is.Not.Null, "Missing prospective island content: " + path);
                corpus.AddAsset(asset, "Prospective item flavour");
            }
            foreach (var asset in AssetsOfKind("ToolDef"))
            {
                using (var serialized = new SerializedObject(asset))
                    if (corpus._itemIds.Contains(serialized.FindProperty("GearId")?.stringValue ?? ""))
                        corpus.AddAsset(asset, "Item flavour");
            }
            foreach (var asset in AssetsOfKind("GiftConfig")) corpus.AddAsset(asset, "Dialogue");
            corpus.AddStaticWords();
            corpus.Fields.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return corpus;
        }

        static IEnumerable<T> All<T>() where T : Object => AssetsOfKind(typeof(T).Name).Cast<T>();

        static IEnumerable<Object> AssetsOfKind(string kind)
        {
            if (!TypeCache.GetTypesDerivedFrom<ScriptableObject>().Any(t => t.Name == kind)) yield break;
            foreach (string guid in AssetDatabase.FindAssets("t:" + kind, new[] { DataRoot }).OrderBy(g => g, StringComparer.Ordinal))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                Assert.That(asset, Is.Not.Null, "Could not read text source: " + path);
                yield return asset;
            }
        }

        void Add(string file, string field, string value, string category)
        {
            var row = new Field(file, field, value);
            if (!_fieldKeys.Add(row.Key)) throw new InvalidDataException("Duplicate text address: " + row.Key);
            Fields.Add(row);
            Counts[category] = Counts.TryGetValue(category, out int count) ? count + 1 : 1;
        }

        void AddAsset(Object asset, string category)
        {
            if (asset == null) return;
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path) || !_visitedFiles.Add(path)) return;
            foreach (var field in AssetText(asset, path)) Add(field.File, field.Field, field.Text, category);
            if (asset is NpcDef npc)
            {
                _npcIds.Add(npc.Id);
                AddAsset(npc.Dialogue, "Dialogue");
            }
            if (asset is DialogueDef dialogue && dialogue.Options != null)
                foreach (var option in dialogue.Options)
                    if (!string.IsNullOrEmpty(option.CatalogSellerId)) _sellerIds.Add(option.CatalogSellerId);
        }

        public static IEnumerable<Field> AssetText(Object asset, string path)
        {
            using (var serialized = new SerializedObject(asset))
            {
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    string field = Regex.Replace(property.propertyPath, @"\.Array\.data\[(\d+)\]", "[$1]");
                    if (property.propertyType == SerializedPropertyType.String && IsTextField(field))
                        yield return new Field(path, field, property.stringValue);
                }
            }
        }

        void ReadSceneOrPrefab(string path)
        {
            if (!_visitedFiles.Add(path)) return;
            Assert.That(File.Exists(path), Is.True, "Missing island scene or prefab: " + path);
            var references = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in File.ReadLines(path))
                foreach (Match match in Reference.Matches(line)) references.Add(match.Groups[1].Value);
            foreach (var field in SceneText(path, File.ReadLines(path))) Add(field.File, field.Field, field.Text, "Scene/sign");
            foreach (string guid in references.OrderBy(g => g, StringComparer.Ordinal))
            {
                string target = AssetDatabase.GUIDToAssetPath(guid);
                if (target.EndsWith(".prefab", StringComparison.Ordinal)) ReadSceneOrPrefab(target);
                else if (target.StartsWith(DataRoot + "/", StringComparison.Ordinal) && target.EndsWith(".asset", StringComparison.Ordinal))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(target);
                    if (asset != null && (SpeechKinds.Contains(asset.GetType().Name) || asset is ICatalogListing
                        || asset.GetType().Name == "ToolDef")) AddAsset(asset, "Scene content");
                }
            }
        }

        /// <summary>Read prose scalars and prefab text overrides without opening a scene or running its components.</summary>
        public static IEnumerable<Field> SceneText(string path, IEnumerable<string> lines)
        {
            string objectId = "", pendingField = null, overrideField = null;
            int indent = 0;
            var scalar = new StringBuilder();
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string line in lines.Concat(new[] { "--- end" }))
            {
                int spaces = line.TakeWhile(c => c == ' ').Count();
                if (pendingField != null && line.TrimStart().Length > 0
                    && (spaces > indent || (spaces == indent && line.TrimStart().StartsWith("- ", StringComparison.Ordinal))))
                {
                    scalar.Append(' ', 2 + spaces - indent).AppendLine(line.TrimStart());
                    continue;
                }
                if (pendingField != null)
                {
                    var value = KeySceneImport.ReadAsset("MonoBehaviour:\n  Value:" + scalar, path)[0].Value;
                    string key = "&" + objectId + "." + pendingField;
                    int occurrence = occurrences.TryGetValue(key, out int count) ? count : 0;
                    occurrences[key] = occurrence + 1;
                    foreach (var field in SceneValues(path, key + "#" + occurrence, value)) yield return field;
                    pendingField = null;
                }
                if (line.StartsWith("---", StringComparison.Ordinal))
                {
                    objectId = line.Contains("&") ? line.Substring(line.IndexOf('&') + 1).Split(' ')[0] : "";
                    overrideField = null;
                    continue;
                }
                var property = Property.Match(line);
                if (!property.Success) continue;
                string name = property.Groups[2].Value;
                string text = property.Groups[3].Value;
                if (name == "propertyPath") { overrideField = text; continue; }
                string field = name == "value" ? overrideField : name;
                if (field == null || !IsTextField(field)) continue;
                if (text == "|" || text == ">" || text.StartsWith("|-", StringComparison.Ordinal) || text.StartsWith(">-", StringComparison.Ordinal))
                    throw new InvalidDataException(path + ": block-style text needs a reader before it can be checked: " + field);
                pendingField = name == "value" ? "override." + field : field;
                indent = property.Groups[1].Value.Length;
                scalar.Clear();
                if (text.Length > 0) scalar.Append(' ').Append(text);
                scalar.AppendLine();
            }
        }

        static IEnumerable<Field> SceneValues(string path, string field, KeySceneImport.YamlNode value)
        {
            if (value.Scalar != null) yield return new Field(path, field, value.Scalar);
            if (value.Items != null)
                for (int i = 0; i < value.Items.Count; i++)
                    foreach (var row in SceneValues(path, field + "[" + i + "]", value.Items[i])) yield return row;
            if (value.Map != null)
                foreach (var pair in value.Map)
                    if (IsTextField(pair.Key))
                        foreach (var row in SceneValues(path, field + "." + pair.Key, pair.Value)) yield return row;
        }

        void AddStaticWords()
        {
            var types = new[] { typeof(WorldStrings).Assembly, typeof(HiddenHarbours.UI.HudStrings).Assembly,
                typeof(HiddenHarbours.Player.ToolDef).Assembly, typeof(HiddenHarbours.Fishing.FishSpeciesDef).Assembly,
                typeof(HiddenHarbours.Core.GameConfig).Assembly }.Distinct().SelectMany(a => a.GetTypes())
                .Where(t => t.Name.EndsWith("Strings", StringComparison.Ordinal) || t == typeof(NotebookContent)
                    || t == typeof(HiddenHarbours.Core.NotebookLayout)).ToArray();
            var names = new HashSet<string>(types.Select(t => t.Name), StringComparer.Ordinal);
            var sources = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string source in Directory.GetFiles("Assets/_Project/Code", "*.cs", SearchOption.AllDirectories))
            {
                string path = source.Replace('\\', '/');
                if (path.Contains("/Editor/")) continue;
                foreach (Match declaration in Regex.Matches(File.ReadAllText(source), @"\bclass\s+(\w+)"))
                    if (names.Contains(declaration.Groups[1].Value)) sources.Add(declaration.Groups[1].Value, path);
            }
            foreach (var type in types)
            {
                string path = sources[type.Name];
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.FieldType == typeof(string) && field.IsLiteral)
                        Add(path, field.Name, (string)field.GetRawConstantValue(), "Code words");
                    else if (field.FieldType == typeof(string[]) && field.IsInitOnly && type == typeof(WorldStrings))
                    {
                        var values = (string[])field.GetValue(null);
                        for (int i = 0; i < values.Length; i++) Add(path, field.Name + "[" + i + "]", values[i], "Code words");
                    }
                }
            }
        }
    }
}
