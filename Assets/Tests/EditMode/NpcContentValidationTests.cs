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

        // ---- dialogue options (2026-08-17) ----------------------------------------------------

        /// <summary>
        /// Option ids are the payload of the Core <c>DialogueOptionPicked</c> signal — whichever system
        /// acts on a choice matches on the id and never on the label. So they follow the same
        /// append-only, namespaced discipline as every other id here, and they are unique WITHIN a
        /// conversation (two rows sharing an id would make the pick ambiguous at the listener).
        ///
        /// <para>Passes vacuously until the first choice is authored, which is the point: it is here so
        /// that the day one is, the rule is already being enforced.</para>
        /// </summary>
        [Test]
        public void DialogueOptions_HaveNamespacedIdsUniqueWithinTheirConversation()
        {
            foreach (var d in LoadAll<DialogueDef>())
            {
                string path = AssetDatabase.GetAssetPath(d);
                if (d.Options == null) continue;

                var seen = new Dictionary<string, int>();
                for (int i = 0; i < d.Options.Length; i++)
                {
                    DialogueOption o = d.Options[i];
                    if (!o.IsAuthored) continue;   // a half-typed row is skipped at runtime too

                    Assert.IsTrue(o.Id.StartsWith("option."),
                        $"{path}: option {i} id '{o.Id}' must be namespaced 'option.snake_case' — it is " +
                        "what the DialogueOptionPicked signal carries");
                    Assert.IsFalse(seen.ContainsKey(o.Id),
                        $"{path}: duplicate option id '{o.Id}' (rows " +
                        $"{(seen.TryGetValue(o.Id, out int first) ? first : -1)} and {i}) — a listener " +
                        "could not tell which row the player chose");
                    seen[o.Id] = i;
                }
            }
        }

        /// <summary>
        /// ⛔ <b>No conversation authors its own way out.</b> <c>DialogueOptionPicker</c> APPENDS the
        /// close row so it is always present and always last; a hand-authored row claiming the reserved
        /// id would be silently dropped at runtime, so catch it at the asset instead of leaving somebody
        /// wondering why their goodbye never shows.
        /// </summary>
        [Test]
        public void NoDialogueAuthorsTheReservedCloseOption()
        {
            foreach (var d in LoadAll<DialogueDef>())
            {
                if (d.Options == null) continue;
                foreach (DialogueOption o in d.Options)
                    Assert.AreNotEqual(DialogueOption.CloseId, o.Id,
                        $"{AssetDatabase.GetAssetPath(d)}: '{DialogueOption.CloseId}' is reserved — the " +
                        "picker appends the close row itself, always last, so that every conversation " +
                        "has exactly one safe way out and it is always in the same place");
            }
        }

        // ---- dialogue voices (2026-08-17) -----------------------------------------------------

        /// <summary>
        /// A voice asset is a shared cadence, so it needs the same stable, unique, namespaced id as
        /// anything else an NpcDef points at. Its NUMBERS are the owner's taste surface and are
        /// deliberately not asserted — only that a voice which exists can actually finish a line, which
        /// <c>DialogueVoice.Sanitised</c> guarantees and this checks has not been designed around.
        /// </summary>
        [Test]
        public void Voices_HaveNonEmptyUniqueNamespacedIds_AndCanFinishALine()
        {
            var seen = new Dictionary<string, string>();
            foreach (var v in LoadAll<DialogueVoiceDef>())
            {
                string path = AssetDatabase.GetAssetPath(v);
                Assert.IsFalse(string.IsNullOrWhiteSpace(v.Id), $"{path}: DialogueVoiceDef has an empty id");
                Assert.IsTrue(v.Id.StartsWith("voice."),
                    $"{path}: DialogueVoiceDef id '{v.Id}' must be namespaced 'voice.snake_case'");
                Assert.IsFalse(seen.ContainsKey(v.Id),
                    $"duplicate DialogueVoiceDef id '{v.Id}' in '{path}' and " +
                    $"'{(seen.TryGetValue(v.Id, out var o) ? o : "?")}'");
                seen[v.Id] = path;

                Assert.Greater(v.Resolved.CharactersPerSecond, 0f,
                    $"{path}: a voice that fills at zero characters a second would leave a bubble that " +
                    "never finishes and no way past it");
                Assert.GreaterOrEqual(v.Resolved.CharactersPerTick, 1,
                    $"{path}: the audio stride must be at least one character");
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
