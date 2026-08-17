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
    /// <para>Scope note: this stays the MINIMAL identity shape — name + dialogue + flag + body — and it is
    /// deliberately NOT where a routine lives. A person's DAY is its own asset
    /// (<see cref="RoutineDef"/>, one per villager under <c>Data/Routines</c>, keyed to this def) rather
    /// than a field here, so an NPC and their schedule can be authored, reviewed and vetoed separately,
    /// and an NPC with no routine is simply an NPC who waits where they were placed —
    /// <c>design/npcs-and-routines.md</c> §2.6 records what phase 1 shipped. Fields here are
    /// append-only.</para>
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

        [Tooltip("How this person's speech bubble FILLS — their cadence, and the tick that fills it is " +
                 "the sound of them talking (design/dialogue-and-knowledge.md §2). Optional and " +
                 "append-only: leave it empty and they fill at DialogueVoice.Default, which is a " +
                 "cadence, never a stall. A voice asset is shared on purpose — half the harbour can " +
                 "speak in one island voice and be re-tuned in one edit.")]
        public DialogueVoiceDef Voice;

        [Header("Appearance (optional — append-only, 2026-08-02)")]
        [Tooltip("Which baked body this person wears. LEAVE IT EMPTY and nothing changes: they keep " +
                 "the greybox standee (or the static sprite at Art/Characters/<stem>.png) they had " +
                 "before. Set it and the region builder gives them the preset's animated idle " +
                 "instead. Written by CharacterBuildsBuilder from the cast mapping; re-pointable in " +
                 "the inspector.")]
        public HiddenHarbours.Core.CharacterBuildDef Build;

        /// <summary>True when this NPC has dialogue authored to speak.</summary>
        public bool HasDialogue => Dialogue != null;

        /// <summary>
        /// True when this NPC has a build WITH baked sheets behind it — the only condition under which
        /// a region builder should reach for the animated body. A build naming a preset that has not
        /// been baked is deliberately false here: an NPC that keeps their old standee is a visible
        /// absence, where an empty SpriteRenderer is an invisible person.
        /// </summary>
        public bool HasBakedBody => Build != null && Build.HasBakedBody;
    }
}
