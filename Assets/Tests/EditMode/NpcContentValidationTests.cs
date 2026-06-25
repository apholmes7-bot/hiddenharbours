using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters onboarding — content validation for the NEW world data (the NPC + dialogue defs that
    /// drive the buy-and-repair opening). Mirrors <see cref="ContentValidationTests"/>'s rules over the
    /// real assets in Data/: every <see cref="NpcDef"/> and <see cref="DialogueDef"/> has a non-empty,
    /// unique, namespaced id; every NPC's dialogue ref resolves and has lines; the opening cast (Aunt
    /// Ginny + Ned's letter) exists with the flags the onboarding director keys off. Catches a
    /// copy-pasted id, an NPC pointing at no dialogue, or an empty conversation — as the cast grows.
    ///
    /// Lives in the TOP-LEVEL EditMode folder (not a per-module one) because content validation loads
    /// real Data/ assets across the World module — the same placement as <see cref="ContentValidationTests"/>.
    /// </summary>
    public class NpcContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        private static List<T> LoadAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { DataRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        // ---- dialogue defs --------------------------------------------------------------------

        [Test]
        public void Dialogues_Exist_AndHaveNonEmptyUniqueNamespacedIds()
        {
            var dialogues = LoadAll<DialogueDef>();
            Assert.IsNotEmpty(dialogues, "the St Peters opening must ship dialogue defs (Ginny + Ned's letter)");

            var seen = new Dictionary<string, string>();
            foreach (var d in dialogues)
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.Id), $"{path}: DialogueDef has an empty id");
                Assert.IsTrue(d.Id.StartsWith("dialogue."),
                    $"{path}: DialogueDef id '{d.Id}' must be namespaced 'dialogue.snake_case'");
                Assert.IsFalse(seen.ContainsKey(d.Id),
                    $"duplicate DialogueDef id '{d.Id}' in '{path}' and '{(seen.TryGetValue(d.Id, out var o) ? o : "?")}'");
                seen[d.Id] = path;
            }
        }

        [Test]
        public void Dialogues_HaveAtLeastOneFirstLine()
        {
            foreach (var d in LoadAll<DialogueDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.IsNotNull(d.FirstLines, $"{path}: FirstLines is null");
                Assert.IsNotEmpty(d.FirstLines, $"{path}: a conversation with no first line can never play");
                foreach (var line in d.FirstLines)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(line), $"{path}: a blank dialogue line");
                // Repeat lines are optional, but if present must be non-blank (they fall back to FirstLines).
                if (d.RepeatLines != null)
                    foreach (var line in d.RepeatLines)
                        Assert.IsFalse(string.IsNullOrWhiteSpace(line), $"{path}: a blank repeat line");
            }
        }

        // ---- npc defs -------------------------------------------------------------------------

        [Test]
        public void Npcs_Exist_AndHaveNonEmptyUniqueNamespacedIds()
        {
            var npcs = LoadAll<NpcDef>();
            Assert.IsNotEmpty(npcs, "the St Peters opening must ship NPC defs (Aunt Ginny + Ned's letter)");

            var seen = new Dictionary<string, string>();
            foreach (var n in npcs)
            {
                string path = AssetDatabase.GetAssetPath(n);
                Assert.IsFalse(string.IsNullOrWhiteSpace(n.Id), $"{path}: NpcDef has an empty id");
                Assert.IsTrue(n.Id.StartsWith("npc."),
                    $"{path}: NpcDef id '{n.Id}' must be namespaced 'npc.snake_case'");
                Assert.IsFalse(seen.ContainsKey(n.Id),
                    $"duplicate NpcDef id '{n.Id}' in '{path}' and '{(seen.TryGetValue(n.Id, out var o) ? o : "?")}'");
                seen[n.Id] = path;
            }
        }

        [Test]
        public void Npcs_HaveNameAndResolvableDialogueWithLines()
        {
            foreach (var n in LoadAll<NpcDef>())
            {
                string path = AssetDatabase.GetAssetPath(n);
                Assert.IsFalse(string.IsNullOrWhiteSpace(n.DisplayName), $"{path}: NpcDef has an empty DisplayName");
                Assert.IsTrue(n.HasDialogue, $"{path}: NpcDef '{n.Id}' has no DialogueDef — it would speak nothing");
                Assert.IsNotEmpty(n.Dialogue.FirstLines, $"{path}: NpcDef '{n.Id}' points at an empty conversation");
            }
        }

        [Test]
        public void OpeningCast_GinnyAndNedLetter_ExistWithTheirFlags()
        {
            NpcDef ginny = null, ned = null;
            foreach (var n in LoadAll<NpcDef>())
            {
                if (n.Id == "npc.aunt_ginny") ginny = n;
                if (n.Id == "npc.ned_letter") ned = n;
            }

            Assert.IsNotNull(ginny, "Aunt Ginny (npc.aunt_ginny) must exist — she teaches the opening loop");
            Assert.AreEqual(InteractKind.Talk, ginny.Kind, "Ginny is talked to");
            Assert.AreEqual(OnboardingFlags.MetGinnyKey, ginny.CompletionFlag,
                "Ginny's completion flag must be met_ginny (the first onboarding nudge keys off it)");

            Assert.IsNotNull(ned, "Ned's letter (npc.ned_letter) must exist — the remembered presence (no inherited dory)");
            Assert.AreEqual(InteractKind.Read, ned.Kind, "the letter is READ, not talked to");
            Assert.AreEqual(OnboardingFlags.ReadLogbookKey, ned.CompletionFlag,
                "the letter's completion flag must be read_logbook");
        }
    }
}
