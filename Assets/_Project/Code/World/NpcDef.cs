using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A named world character, as DATA (ADR 0003 / CLAUDE.md rule 2): one asset per file under
    /// <c>Data/NPCs</c>, keyed by a stable, append-only <see cref="Id"/> (<c>npc.snake_case</c>, e.g.
    /// <c>npc.aunt_ginny</c>). This is the lightweight authoring handle the region builder reads to
    /// place an <see cref="Interactable"/> — the speaker's name, their dialogue (a <see cref="DialogueDef"/>),
    /// and the onboarding/flag bookkeeping — so introducing an NPC is a new asset, not new code.
    ///
    /// <para>Scope note: this is the MINIMAL opening-cast shape (name + dialogue + flag), deliberately
    /// short of the full routine/anchor/schedule system in <c>design/npcs-and-routines.md</c> §2 — those
    /// land in M2 when the cast goes on daily routines. The St Peters opening NPCs are <b>anchored</b>
    /// (placed, no routine), exactly as the charter's M1 focus describes. Fields here are append-only.</para>
    ///
    /// <para>Localization: <see cref="DisplayName"/> is plain copy for now (the world layer's stand-in
    /// until loc tables land — see <see cref="WorldStrings"/>); the FORMAT being data is the commitment.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/NPC", fileName = "Npc")]
    public class NpcDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (npc.snake_case, e.g. npc.aunt_ginny). Content-validated for uniqueness.")]
        public string Id = "npc.example";

        [Tooltip("Name shown on the dialogue nameplate (e.g. \"Aunt Ginny\"). The localization stand-in.")]
        public string DisplayName = "Someone";

        [Header("Interaction")]
        [Tooltip("Talk to a person, or Read a thing (a letter, a logbook) — selects the floating prompt's verb.")]
        public InteractKind Kind = InteractKind.Talk;

        [Tooltip("The conversation this NPC speaks (a DialogueDef asset). Data, not a hard-coded WorldStrings id.")]
        public DialogueDef Dialogue;

        [Tooltip("Onboarding flag set true when this conversation completes (e.g. \"met_ginny\"). " +
                 "Empty = no flag. Drives the warmer 'met before' variant and the onboarding nudges.")]
        public string CompletionFlag = "";

        /// <summary>True when this NPC has dialogue authored to speak.</summary>
        public bool HasDialogue => Dialogue != null;
    }
}
