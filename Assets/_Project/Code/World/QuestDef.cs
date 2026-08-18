using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// One step of a quest: what the player is asked to do, and the FLAG that means they have done it.
    ///
    /// <para>The step owns no state of its own. Whether it is done is read from
    /// <see cref="IFlagStore"/> at the moment of asking — see <see cref="QuestDef"/> for why that
    /// matters.</para>
    /// </summary>
    [Serializable]
    public class QuestStep
    {
        [Tooltip("The line the player reads, e.g. \"twine from the store\". Write to 31 characters: " +
                 "at the closest zoom tier a step line holds exactly that many, and a longer one is " +
                 "not clipped — it wraps and eats a ruled line.")]
        public string Label = "";

        [Tooltip("The existing flag id that means this step is DONE (e.g. \"met_ginny\", " +
                 "\"deed.home.ginny_lot_camper\"). Must be a flag something in the shipped content " +
                 "actually sets — QuestContentValidationTests proves it against a registry of every " +
                 "settable flag, so a typo is a red test rather than a step that can never complete.")]
        public string Flag = "";
    }

    /// <summary>
    /// <b>A QUEST, AS DATA — AND AS A VIEW OVER FLAGS THAT ALREADY EXIST.</b> One asset per file under
    /// <c>Data/Quests</c>, keyed by a stable, append-only <see cref="Id"/> (<c>quest.snake_case</c>).
    ///
    /// <para><b>⭐ NO QUEST STATE IS SAVED, ANYWHERE — and that is the whole design.</b> A quest is not
    /// a record the game writes to; it is a READING of the flags the world already keeps (dialogue
    /// <c>CompletionFlag</c>s, the onboarding beats, a home deed). Active means any step's flag is
    /// unset; archived means all of them are set; the CURRENT step is the first unset one. Three
    /// consequences follow, and each is the reason to do it this way:</para>
    /// <list type="number">
    ///   <item><b>Save and determinism are untouched.</b> No new field, no schema bump, nothing to
    ///     migrate. Adding quests cannot break a save because quests are not IN the save.</item>
    ///   <item><b>A quest added later is retroactively correct.</b> Write a quest today about beats a
    ///     player finished last week and it opens already archived, because the flags it reads were
    ///     set at the time. A saved-progress model would show it as untouched and lie.</item>
    ///   <item><b>There is exactly one truth.</b> A saved "step 2 done" that disagreed with the flag
    ///     would be a bug with no correct resolution. Here the disagreement cannot be represented.</item>
    /// </list>
    ///
    /// <para><b>What this deliberately does NOT have.</b> No counted steps ("catch 5 cod") — the
    /// handoff flags that as a later shape (a step would name a count source rather than a flag) and
    /// building it now would be the scope creep rule 8 names. No rewards, no giver reference, no
    /// prerequisites between quests: all of those are additive to this, and none is needed for the
    /// book to be real. Fields here are append-only.</para>
    ///
    /// <para>Localization: <see cref="Title"/>, <see cref="Context"/> and each step's label are plain
    /// copy for now — the world layer's stand-in until loc tables land. The FORMAT being data is the
    /// commitment.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Quest", fileName = "Quest")]
    public class QuestDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (quest.snake_case, e.g. quest.the_letter). " +
                 "Content-validated for uniqueness and shape.")]
        public string Id = "quest.example";

        [Header("The page")]
        [Tooltip("The quest's name, in the hand at press 0.9. Holds 33 characters on a title line at " +
                 "the closest tier; longer wraps rather than clipping.")]
        public string Title = "";

        [Tooltip("One line under the title, in pencil — the asker or the deadline, NEVER a second " +
                 "title. Optional: leave empty and the block is one row shorter.")]
        public string Context = "";

        [Tooltip("The steps, IN ORDER. Order is the reading order on the page and it decides which " +
                 "step is CURRENT (the first one whose flag is still unset).")]
        public List<QuestStep> Steps = new List<QuestStep>();
    }
}
