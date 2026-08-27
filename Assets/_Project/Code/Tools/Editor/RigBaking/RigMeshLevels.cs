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
    }

    /// <summary>
    /// One record out of a pass-3 rig's <c>geometry()</c>: a WALKABLE level, its sole, and its ceiling
    /// — declared from the same constants the mesh is built from, never measured back off it.
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
    /// so the gate refuses one; that refusal is only honest if the openness was declared.</para>
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
            (Enclosed ? $"ceiling {CeilingZ:0.###} m ({CeilingKind})" : $"OPEN ({CeilingKind})");
    }

    /// <summary>An empty table, shared, so a rig that publishes no levels allocates nothing.</summary>
    internal static class RigLevelTables
    {
        public static readonly IReadOnlyDictionary<string, int> NoIds =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public static readonly IReadOnlyList<RigLevelRecord> NoLevels = Array.Empty<RigLevelRecord>();
    }
}
