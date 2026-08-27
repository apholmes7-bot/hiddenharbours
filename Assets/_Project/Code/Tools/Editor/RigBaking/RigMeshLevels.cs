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
    /// <b>Which level is this level's LID</b> — the coordinator's ruling of 2026-08-27: <i>a cut takes
    /// its declared ceiling.</i> When level L engages the gate, L's own faces come off AND the faces
    /// of the level L's ceiling record names as its lid. <b>One hop only, declaration-driven, never
    /// inferred from geometry.</b>
    ///
    /// <para><b>⚠️ WHY THIS TABLE EXISTS, AND WHEN TO DELETE IT.</b> The ruling says the lid is what
    /// the ceiling record NAMES. Neither batch's ceiling records name it in a form a machine can
    /// read: they carry <c>of:</c>, which is PROSE — <c>'main-deck underside (DECK-0.12)'</c>,
    /// <c>'foredeck underside = sheerZ(y)-0.16, rising toward the bow'</c>. Those spellings are not
    /// level ids (<c>main-deck</c> is hyphenated where the id is <c>main_deck</c>; <c>boat-deck</c>
    /// and <c>wheelhouse deckhead</c> are not levels at all), and substring-matching a human sentence
    /// is a worse kind of inference than the geometric one the ruling forbids — it would silently
    /// re-aim the moment upstream reworded a comment.
    ///
    /// <para>The other candidate, matching a ceiling z against another level's sole z, is exactly the
    /// forbidden geometric inference AND needs a per-hull tolerance nobody can justify: the gaps are
    /// 0.110 m (lobster), 0.120 m (trawler) and 0.200 m (packet), each a deck-plate thickness the rig
    /// states in prose and publishes nowhere. Batch 2 adds three more of them — 0.10 (dragger), 0.12
    /// (Mk II) and 0.25 (tanker) — and one hull that settles the argument outright: the TANKER's
    /// <c>below</c> is lidded by <c>poop_deck</c>, not <c>main_deck</c>. Both prose-matching and
    /// z-matching would have to get her right by accident, and a wrong lid does not look wrong; it
    /// opens a plausible hole in the wrong deck.</para>
    ///
    /// <para>So the lid is DECLARED here, once, in the same place and for the same reason the
    /// extractor already declares the facts a rig does not export
    /// (<see cref="RigMeshSymbols.Widenings"/>, <see cref="RigPropExtraction.BackfaceRescueNeedsOptIn"/>).
    /// <b>Every entry quotes the rig's own words for it</b> — the kit's authoring cursor states each
    /// relationship in a comment beside the emission, which is a declaration in the source even though
    /// it is not one in the data. <b>The moment a rig publishes <c>ceiling.lid</c>, the rig wins and
    /// its entry here must be deleted</b>: <see cref="RigMeshExtractor"/> refuses a bake where the two
    /// disagree, so the table cannot outlive the field it stands in for.</para>
    /// </summary>
    public static class RigLevelLids
    {
        /// <summary>The rig's <c>ceiling.lid</c> when it publishes one. A rig that sets it to
        /// <c>null</c> — as opposed to omitting it — is the ruling's <b>per-level veto</b>: this level
        /// takes nothing with it. Absent and null must never look the same, which is the same law the
        /// kit already applies to an open sky.</summary>
        public const string RigLidProperty = "lid";

        /// <summary>Declared here because the cutaway kit's rigs — batch 1 and batch 2 alike —
        /// predate <see cref="RigLidProperty"/>. Keyed by rig FILE NAME (as
        /// <see cref="RigMeshSymbols.Widenings"/> is), then by the rig's own level id. Keying by FILE
        /// is also why the eighteen lobster variants need one entry between them rather than
        /// eighteen: one generator rig makes them all.</summary>
        static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Declared =
            new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
            {
                // The rig says it in as many words at the emission itself:
                //   lobsterBoatIsoRig.js:354  lv('foredeck');  // the cuddy's lid — a walkable level of its own
                // and again in the ceiling record's prose: 'foredeck underside = sheerZ(y)-0.16'.
                // Her cuddy is a crawl-in berth under the foredeck, and the foredeck is a walkable
                // level in its own right — which is exactly why the lid needs a hop instead of being
                // folded into the cuddy's own tag the way the ships fold theirs.
                ["lobsterBoatIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["cuddy"] = "foredeck",
                },

                // 'main-deck underside (DECK-0.12)' — the trawl deck is the engine space's lid, and
                // it is a level of its own (lv('main_deck'): "the trawl deck and everything standing
                // on it"). Her HOUSE needs no entry: its ceiling is the boat deck, whose faces the rig
                // already tags `house` (lv('house'): "walls, vestibule, boat deck, its rails, ladder,
                // liferafts"), so the house takes its own lid with it and one tag does the work. Same
                // for her BRIDGE — "the wheelhouse cuts with its own room".
                ["sternTrawlerIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["below"] = "main_deck",
                },

                // 'main-deck underside (DECK-0.20)'; lv('main_deck'): "the weather deck and everything
                // standing on it". Her house and bridge fold their own lids in exactly as the
                // trawler's do — lv('house') carries "L1 + the dressed L2 block, boat deck, rails,
                // ladder, scoop", which is also what geometry().dressed is telling you.
                ["coastalPacketIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["below"] = "main_deck",
                },

                // ---- BATCH 2 (2026-08-27) — same mechanism, same debt --------------------------
                // These four rigs do not publish ceiling.lid either. They were swept for it at
                // intake: the string 'lid' occurs in them only in prose and in the cursor comments
                // quoted below, never as a geometry() field. So they JOIN this table rather than
                // retiring it, and the ask above grows from three levels to seven.

                // 'main-deck underside (DECK-0.10)'; lv('main_deck'): "the working deck and
                // everything standing on it". Her house and bridge fold their own lids in as both
                // batch-1 ships do — lv('house'): "walls, vestibule, boat deck, its rails, ladder"
                // and, at the funnel, "the funnel stands on the boat deck — the house's lid";
                // lv('bridge'): "the wheelhouse cuts with its own room".
                ["sideDraggerIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["below"] = "main_deck",
                },

                // 'main-deck underside (DECK-0.12)' — the same 0.12 m deck plate her Mk I sister
                // carries, and the same lid. lv('main_deck'): "the trawl deck and everything
                // standing on it". Her BRIDGE needs no entry for a reason the rig states outright
                // in the ceiling record itself: "the flared sides (hxAt) are the walls, not the
                // lid", so the deckhead it does name is already inside lv('bridge').
                ["sternTrawlerMk2IsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["below"] = "main_deck",
                },

                // ⚠️ THE ONE THAT IS NOT main_deck, AND THE ONE THAT CLOSES THE ARGUMENT ABOVE.
                // 'poop-deck underside (POOP-0.25)' — her accommodation sits under the RAISED POOP,
                // not under the weather deck, and poop_deck is a level of its own (lv('poop_deck'):
                // "the raised poop mooring deck strip"; id 6, the fleet table's newest). Reading
                // "the underside of a deck" as main_deck because both batch-1 ships said so would
                // put her lid on the wrong deck entirely, and the cut would look plausible doing it.
                //
                // The z-matching alternative does not merely risk her — it CANNOT DECIDE her.
                // Measured off her own geometry(): below's ceiling is 11.35 m, and the two levels
                // whose sole sits 0.25 m above it are poop_deck AND house, both at 11.60. They
                // share a sole; that is the tie her own tieBreak field exists to break. A rule that
                // matched ceiling z to sole z would have to choose between the deck overhead and
                // the room beside it with nothing to choose on.
                ["tankerIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["below"] = "poop_deck",
                },

                // The lobster family's own law, unchanged from the boat it was generalised from:
                //   lobsterBoatVariantsIsoRig.js:544  lv('foredeck');  // the cuddy's lid — a
                //                                                        walkable level of its own
                // and the ceiling record's prose is her sister's word for word: 'foredeck underside
                // = sheerZ(y)-0.16, rising toward the bow'. ONE entry covers all EIGHTEEN hulls —
                // this table is keyed by rig FILE and one file makes them all, so no variant can
                // drift away from its family here. Her house folds its own lid in: lv('house') is
                // "walls, glazing, vestibule, roof — cuts with the room".
                ["lobsterBoatVariantsIsoRig.js"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["cuddy"] = "foredeck",
                },
            };

        /// <summary>The declared lid for <paramref name="levelId"/> on the rig at
        /// <paramref name="scriptPath"/>, or null for "this level takes nothing with it".</summary>
        public static string For(string scriptPath, string levelId)
        {
            string file = FileNameOf(scriptPath);
            return Declared.TryGetValue(file, out var byLevel)
                   && byLevel.TryGetValue(levelId, out string lid)
                ? lid
                : null;
        }

        /// <summary>Every level this table declares a lid for, on one rig. Used by the tests that
        /// police the table against the rig it stands in for.</summary>
        public static IReadOnlyDictionary<string, string> AllFor(string scriptPath) =>
            Declared.TryGetValue(FileNameOf(scriptPath), out var byLevel)
                ? byLevel
                : RigLevelTables.NoLids;

        static string FileNameOf(string scriptPath)
        {
            if (string.IsNullOrEmpty(scriptPath)) return "";
            int slash = scriptPath.LastIndexOfAny(new[] { '/', '\\' });
            return slash >= 0 ? scriptPath.Substring(slash + 1) : scriptPath;
        }
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

        /// <summary>This repo's <see cref="RigLevelLids"/> stood in for a field the rig does not yet
        /// publish. Every one of these is a debt, and a test names them.</summary>
        public const string LidFromTable = "declared";

        /// <summary>The rig published <c>lid: null</c> — the ruling's per-level veto. Beats the
        /// table.</summary>
        public const string LidFromVeto = "veto";

        /// <summary>Nobody declared one: this level takes nothing with it, which is the ordinary
        /// answer.</summary>
        public const string LidNone = "none";

        /// <summary>Where <see cref="LidLevelId"/> came from, carried for the bake log and for the
        /// test that retires <see cref="RigLevelLids"/>.</summary>
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

        public static readonly IReadOnlyDictionary<string, string> NoLids =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
