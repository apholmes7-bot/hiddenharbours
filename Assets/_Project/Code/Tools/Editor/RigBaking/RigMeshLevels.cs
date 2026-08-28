using System;
using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// The per-face LEVEL vocabulary the cutaway kit's pass-3 rigs publish, as the extractor sees it.
    ///
    /// <para><b>The rule that governs every line in this file: the cursor stamped it, read it.</b> A
    /// pass-3 rig carries an authoring cursor (<c>let LV='hull'; F.push = …arguments[i].lv = LV</c>)
    /// so every face DECLARES the level it belongs to at the point it is emitted. Nothing here — and
    /// nothing downstream of here — may re-derive a tag from geometry. The interior-mesh spike
    /// (<c>docs/design/spikes/interior-mesh-verdict.md</c> §B) measured what derivation costs: a rule
    /// built from soles and z-spans left 0.79% of the fleet's faces ambiguous, gave a deckhouse WALL
    /// to the deck it stands on rather than to the room it encloses, and could not separate two levels
    /// that share one sole at all — which is two of the three hulls in this batch.</para>
    ///
    /// <para><b>The vocabulary is the RIG's, per rig</b> (<c>geometry().ids</c>). The two ships agree
    /// with each other and the lobster family does not, and that is fine because the table travels
    /// with the hull: ships are <c>hull 0 · main_deck 1 · house 2 · bridge 3 · below 4 · rigging 5</c>,
    /// the lobster family <c>hull 0 · cockpit 1 · foredeck 2 · house 3 · cuddy 4 · rigging 5</c>. Never
    /// transcribe either into C#; read <c>geometry().ids</c> and carry it.</para>
    /// </summary>
    public static class RigLevelTags
    {
        /// <summary>What <see cref="RigFace.Level"/> holds on a rig that publishes no level table at
        /// all — every hull baked before the cutaway kit, and every fitting. It is deliberately NOT 0:
        /// 0 is <c>hull</c>, a real level with a real meaning ("the exterior silhouette, never
        /// culled"), and an untagged face claiming it would be a rig's silence dressed up as a
        /// declaration. The builder writes no TexCoord1 channel at all in this case, so such a mesh
        /// stays byte-for-byte what it was.</summary>
        public const int Untagged = -1;

        /// <summary>The exterior silhouette — shell, bulwarks, washboards, rail caps, the trawler's
        /// stern-ramp cut. <b>Never culled</b>: the room shows inside the hull's own outline, which is
        /// the whole of what "cutaway" means. Its id is 0 on every rig in the kit, which is also what
        /// makes "gate off" and "show level 0" the same picture.</summary>
        public const string HullLevelId = "hull";

        /// <summary>Arch, dome, aerials, gantry, warps, radar mast, stays, derricks, deck cranes. A
        /// <b>dedicated class</b>, not a room, so a cut can never take a spar away with the space it
        /// happens to stand over. Its id is 5 on every rig in the kit — outside the 1..4 band a cutaway
        /// ever shows, which is the mechanism rather than a convention.</summary>
        public const string RiggingLevelId = "rigging";

        /// <summary>The name of the ceiling record's LID field — <b>which level is this level's
        /// lid</b>, per the 2026-08-27 ruling: <i>a cut takes its declared ceiling</i>. When level L
        /// engages the gate, L's own faces come off AND the faces of the level L's ceiling record
        /// names here. <b>One hop only, declared, never inferred.</b>
        ///
        /// <para>Three states, all distinct on purpose:</para>
        /// <list type="bullet">
        ///   <item><description>a level id — this level's cut takes that level with it;</description></item>
        ///   <item><description><c>null</c> — the ruling's <b>per-level veto</b>: takes nothing. Every
        ///     level that folds its own lid into its own tag says this (both ships' <c>house</c>
        ///     carries its boat deck, both <c>bridge</c>es their deckhead);</description></item>
        ///   <item><description><b>absent — a REFUSAL.</b> <see cref="RigMeshExtractor"/> stops the
        ///     bake. Absent and null must never look the same, which is the same law the kit already
        ///     applies to an open sky, and the day a rig needs a lid the rig itself must say so.
        ///     This repo stood in for the field once, in a table keyed by rig file, and that table is
        ///     retired — it is not coming back as a silent default.</description></item>
        /// </list>
        ///
        /// <para><b>Why it is declared and not measured.</b> Matching a ceiling z against another
        /// level's sole z is the inference the ruling forbids, and the TANKER proves it could not
        /// merely be risky but undecidable: her <c>below</c> is lidded by <c>poop_deck</c>, not the
        /// obvious <c>main_deck</c>, and the two levels whose sole sits 0.25 m above that ceiling —
        /// <c>poop_deck</c> and <c>house</c>, both at 11.60 m — share a sole. A z rule would have had
        /// nothing to choose on, and a wrong lid does not look wrong: it opens a plausible hole in the
        /// wrong deck.</para></summary>
        public const string LidProperty = "lid";
    }

    /// <summary>
    /// One record out of a pass-3 rig's <c>geometry()</c>: a WALKABLE level, its sole, its ceiling —
    /// and, since the 2026-08-27 ruling, the level that ceiling is the underside OF. All declared from
    /// the same constants the mesh is built from, never measured back off it.
    ///
    /// <para><b><see cref="DeckId"/> is the join, and the rig publishes it.</b> The rig names its levels
    /// <c>house</c>/<c>cuddy</c>/<c>below</c>; <c>BoatInteriorDef</c> names the same rooms
    /// <c>house_sole</c>/<c>cuddy_sole</c>/<c>below_sole</c>. Two vocabularies for one thing is exactly
    /// how a hull ends up drawing the wrong room and looking fine doing it (the tanker's
    /// <c>house_sole</c> → sheet row <c>below</c>, caught only by a test). The rig's <c>deck</c> field
    /// IS the def's level id, so the map is DATA carried from upstream rather than a suffix rule
    /// re-derived at runtime.</para>
    ///
    /// <para><b><see cref="Enclosed"/> is a declaration too.</b> The kit's own contract: "an absent
    /// field and an open sky must never look the same" — an open level publishes <c>ceilingZ: null</c>
    /// plus <c>ceiling:{kind:'open'}</c>. A cutaway of a level with no ceiling is a cutaway of the sky,
    /// so the gate refuses one; that refusal is only honest if the openness was declared. ⚠️ Note the
    /// asymmetry the ruling creates: an open level may not be ENTERED into a cut, but it may perfectly
    /// well BE a lid — the lobster's foredeck and both ships' main_deck are open, and all three are
    /// lids.</para>
    /// </summary>
    public sealed class RigLevelRecord
    {
        /// <summary>The rig's own level name — <c>house</c>, <c>cuddy</c>, <c>below</c>, <c>bridge</c>,
        /// <c>main_deck</c>, <c>cockpit</c>, <c>foredeck</c>.</summary>
        public string Id = "";

        /// <summary>The <c>BoatInteriorDef</c> level id this record is the same room as — the rig's
        /// <c>deck</c> field. Empty when the rig published none.</summary>
        public string DeckId = "";

        /// <summary>This level's id in <c>geometry().ids</c>: the int that goes in TexCoord1.x.</summary>
        public int Tag = RigLevelTags.Untagged;

        /// <summary>
        /// <b>The level this level's ceiling is the underside OF</b>, and therefore the one hop a cut
        /// of this level also takes. Empty for "takes nothing with it" — which is the ordinary answer,
        /// and the right one for every level that already folds its own lid into its own tag (both
        /// ships' <c>house</c> carries its boat deck; both <c>bridge</c>es carry their deckhead).
        /// </summary>
        public string LidLevelId = "";

        /// <summary>The lid's own tag, resolved through the rig's <c>ids</c>. 0 — <c>hull</c>, the
        /// level that is never cut — when there is no lid, so "no lid" and "gate off" are the same
        /// value in the shader as well as in the data.</summary>
        public int LidTag;

        /// <summary>The rig published <c>ceiling.lid</c> itself. The end state, and the only source
        /// that needs no policing.</summary>
        public const string LidFromRig = "rig";

        /// <summary>The rig published <c>lid: null</c> — the ruling's per-level veto: this level
        /// takes nothing with it. The ordinary answer, and the right one for every level that folds
        /// its own lid into its own tag (both ships' <c>house</c> carries its boat deck, both
        /// <c>bridge</c>es their deckhead).</summary>
        public const string LidFromVeto = "veto";

        /// <summary>Nothing has resolved this record's lid — the field's initial value, and not a
        /// state any EXTRACTED record is left in. <see cref="RigMeshExtractor"/> refuses a rig that
        /// publishes levels without a lid on every one of them, so every record it returns carries
        /// <see cref="LidFromRig"/> or <see cref="LidFromVeto"/>.
        ///
        /// <para>There was a third source once: a table in this repo, keyed by rig file, standing in
        /// for a <c>ceiling.lid</c> the cutaway rigs did not publish. They publish it now, the table
        /// is retired, and a fixture holds it retired.</para></summary>
        public const string LidNone = "none";

        /// <summary>Where <see cref="LidLevelId"/> came from, carried for the bake log and for the
        /// fixture that holds the retired stand-in table retired.</summary>
        public string LidSource = LidNone;

        /// <summary>Sole height above the keel bottom, rig metres. A raked sole publishes its honest
        /// minimum here, exactly as the rig does.</summary>
        public double SoleZ;

        /// <summary>True when the rig declared a real ceiling (<c>kind: 'hard'</c> or
        /// <c>'raked'</c>). False for a declared open sky.</summary>
        public bool Enclosed;

        /// <summary>The overhead's underside, rig metres. Only meaningful when <see cref="Enclosed"/>;
        /// a raked ceiling publishes its honest minimum, at the companionway.</summary>
        public double CeilingZ;

        /// <summary>The rig's own <c>ceiling.kind</c> — <c>hard</c>, <c>raked</c>, <c>open</c>. Carried
        /// verbatim for provenance and for the bake log; nothing branches on the string.</summary>
        public string CeilingKind = "";

        public override string ToString() =>
            $"{Id} (tag {Tag}, deck '{DeckId}') sole {SoleZ:0.###} m, " +
            (Enclosed ? $"ceiling {CeilingZ:0.###} m ({CeilingKind})" : $"OPEN ({CeilingKind})") +
            (string.IsNullOrEmpty(LidLevelId) ? "" : $", lid {LidLevelId} [{LidSource}]");
    }

    /// <summary>An empty table, shared, so a rig that publishes no levels allocates nothing.</summary>
    internal static class RigLevelTables
    {
        public static readonly IReadOnlyDictionary<string, int> NoIds =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public static readonly IReadOnlyList<RigLevelRecord> NoLevels = Array.Empty<RigLevelRecord>();
    }
}
