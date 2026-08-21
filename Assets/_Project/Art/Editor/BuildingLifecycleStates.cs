using System;
using System.Collections.Generic;
using System.Linq;

// Lives in HiddenHarbours.Art.Editor, NOT beside the baker, and the assembly graph is why: the kit
// table that names these ids (VillageBuildingKit) is an Art.Editor type, and RigBaking.Editor already
// references Art.Editor one way. Putting the ids next to the baker would need the arrow back and make
// a cycle.
namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The BUILDING LIFECYCLE state ids — the seven construction phases and the five decay steps the
    /// pass understands — plus the one guard that keeps a typo out of a sheet name.
    ///
    /// <para><b>⚠️ THIS IS A SECOND COPY OF A TABLE THAT LIVES IN THE PASS, AND IT IS DELIBERATE.</b>
    /// A bake has to be able to refuse a misspelled state <i>before</i> it starts a JS engine, opens
    /// a rig and renders eight facings — and a kit table that names a state is C#, checked at compile
    /// time by nothing at all. <c>BuildingLifecyclePassTests.TheCSharpStateTable_IsThePassesOwnTable</c>
    /// is what stops the two copies drifting: it reads the pass's own <c>PHASES</c>/<c>DECAY</c> arrays
    /// out of V8 and compares them to these.</para>
    ///
    /// <para><b>🔴 WHY THE GUARD EXISTS, MEASURED.</b> The pass's <c>active(opts)</c> answers "is there
    /// anything to do?" by asking whether the keys are set and not the default — but
    /// <c>normPhase</c>/<c>normDecay</c> then fall back to <c>finished</c>/<c>sound</c> for a key they
    /// do not recognise. So <c>decay:'collapsed'</c> — the natural misspelling of <c>collapsing</c> —
    /// makes <c>active()</c> return true, does nothing, and returns the faces untouched. The bake
    /// succeeds, the sheet has the right number of sprites and the right cell size, and it holds a
    /// pristine building under the file name <c>…_collapsed.png</c>. Nothing throws and nothing logs.
    /// Refusing an unknown id is the only place that failure can be caught.</para>
    ///
    /// <para>The ids are APPEND-ONLY (CLAUDE.md §5). They are already sheet names and contract keys,
    /// and they will be save-file values the day building repair and construction become gameplay
    /// (<c>backlog/</c> — M3). Renaming one silently remaps every building that carries it.</para>
    /// </summary>
    public static class BuildingLifecycleStates
    {
        /// <summary>The seven construction phases, in build order. <c>finished</c> is the rig's own
        /// untouched output and is the default.</summary>
        public static readonly string[] Phases =
            { "site", "foundation", "frame", "rafters", "sheathed", "cladding", "finished" };

        /// <summary>The decay ladder, kept-up first. <c>sound</c> is the default.</summary>
        public static readonly string[] Decays =
            { "sound", "neglected", "abandoned", "collapsing", "ruin" };

        /// <summary>The phase a building is in when nobody has touched the axis.</summary>
        public const string FinishedPhase = "finished";

        /// <summary>The decay state of a building that is being kept up.</summary>
        public const string SoundDecay = "sound";

        /// <summary>
        /// Does this pair ask the pass to do anything? The C#-side twin of the pass's <c>active()</c>,
        /// used by a kit to tell a lifecycle build from an ordinary one without starting an engine.
        /// </summary>
        public static bool IsActive(string phase, string decay, bool burnt) =>
            burnt
            || (!string.IsNullOrEmpty(phase) && phase != FinishedPhase)
            || (!string.IsNullOrEmpty(decay) && decay != SoundDecay);

        /// <summary>
        /// Refuse an id the pass would silently ignore. Null or empty is fine on either axis — that is
        /// "leave this axis alone", which the pass handles correctly; a NON-EMPTY id that is not in the
        /// table is the failure this exists for.
        /// </summary>
        /// <exception cref="ArgumentException">the phase or the decay is a non-empty unknown.</exception>
        public static void AssertKnown(string phase, string decay)
        {
            Check(phase, Phases, "phase", nameof(phase));
            Check(decay, Decays, "decay", nameof(decay));
        }

        static void Check(string value, string[] table, string axis, string paramName)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (table.Contains(value, StringComparer.Ordinal)) return;

            throw new ArgumentException(
                $"'{value}' is not a lifecycle {axis}. Known: {string.Join(", ", table)}.\n\n" +
                "This is refused rather than passed through because the pass would SILENTLY IGNORE it: " +
                $"BuildingLifecycle.active() returns true for any non-default {axis}, and then " +
                $"norm{char.ToUpperInvariant(axis[0])}{axis.Substring(1)}() falls back to the default and " +
                "returns the faces untouched. The bake would succeed and write a pristine building to a " +
                "sheet named after a state it is not in.", paramName);
        }

        /// <summary>
        /// A short human tag for a state pair — <c>"collapsing"</c>, <c>"frame"</c>,
        /// <c>"frame+abandoned"</c>, <c>"ruin+burnt"</c> — for a sheet stem, a log line and a contract
        /// key. Returns <see cref="FinishedPhase"/> for a building in no particular state.
        /// </summary>
        public static string Tag(string phase, string decay, bool burnt)
        {
            var parts = new List<string>(3);
            if (!string.IsNullOrEmpty(phase) && phase != FinishedPhase) parts.Add(phase);
            if (!string.IsNullOrEmpty(decay) && decay != SoundDecay) parts.Add(decay);
            if (burnt) parts.Add("burnt");
            return parts.Count == 0 ? FinishedPhase : string.Join("+", parts);
        }
    }
}
