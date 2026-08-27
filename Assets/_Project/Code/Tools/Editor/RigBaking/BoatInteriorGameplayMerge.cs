using System;
using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>What a merge produced, and everything it did on the way.</summary>
    public sealed class InteriorMergeResult
    {
        /// <summary>The merged document. Null when <see cref="Errors"/> is non-empty.</summary>
        public Dictionary<string, object> Merged;

        /// <summary>Every section-level operation, in the order performed — the dry-run report. A merge
        /// that changed nothing says so; a merge that replaced a section names it.</summary>
        public readonly List<string> Changes = new List<string>();

        /// <summary>Fatal problems. A result with any of these must not be written anywhere.</summary>
        public readonly List<string> Errors = new List<string>();

        public bool Ok => Errors.Count == 0 && Merged != null;
    }

    /// <summary>
    /// <b>The gameplay-sidecar merge, as pure code.</b> The kit's manifest states the contract in one
    /// sentence — "sections are additive against the hull's existing gameplay file: DECK entries
    /// replace by <c>id</c>, THRESHOLD/STAIRS/LADDER/INTERACT replace wholesale, <c>_excluded</c>
    /// merges" — and this is that sentence, executable and testable.
    ///
    /// <para><b>It writes nothing.</b> Deliberately: the committed boat gameplay sidecars under
    /// <c>docs/art/rigs/gameplay/</c> are the art director's lane and are re-read by
    /// <c>DeckSidecarImportParityTests</c> on every run. This class exists so the merge SEMANTICS are
    /// pinned by tests and so the builder can report what a merge would do, not so an intake lane can
    /// overwrite eleven files it does not own.</para>
    ///
    /// <para><b>Where the DECK contribution comes from.</b> An interior sidecar has no <c>DECK</c>
    /// section — it has <c>WALKABLE</c>, an object keyed by level. Each level becomes one DECK entry
    /// carrying the same polygon and z, the deck contract's <c>winding</c>, and the level's
    /// obstructions moved to <c>_notes</c>. That last part is the owner's 2026-07-22 ruling kept
    /// literally: deck polygons never carry holes for furniture, obstructions are listed beside them
    /// and become game-side colliders.</para>
    ///
    /// <para><b>One thing this merge cannot reproduce, and says so.</b> The kit's own pre-merged files
    /// carry a prose <c>note</c> on each interior DECK entry. That prose is not in the interior sidecar
    /// — it is written by the drop's generator — so a merge computed from the committed inputs will not
    /// contain it. This is a difference in ANNOTATION only; every coordinate is identical.</para>
    /// </summary>
    public static class BoatInteriorGameplayMerge
    {
        /// <summary>The deck contract's winding, stamped on every DECK entry this merge contributes.</summary>
        public const string Winding = "ccw_from_above";

        /// <summary>Sections taken from the interior sidecar WHOLESALE — present in the interior means
        /// the interior's version wins entirely; absent means the base keeps whatever it had.</summary>
        public static readonly string[] WholesaleSections = { "THRESHOLD", "STAIRS", "LADDER", "INTERACT" };

        /// <summary>
        /// Merge one interior sidecar into one hull gameplay sidecar.
        /// </summary>
        /// <param name="baseGameplayJson">the committed hull sidecar, unmodified.</param>
        /// <param name="interiorSidecarJson">the interior sidecar contributing sections.</param>
        public static InteriorMergeResult Merge(string baseGameplayJson, string interiorSidecarJson)
        {
            var result = new InteriorMergeResult();

            Dictionary<string, object> baseDoc, interior;
            try { baseDoc = DeckSidecarJson.AsObject(DeckSidecarJson.Parse(baseGameplayJson)); }
            catch (Exception e) { result.Errors.Add($"base gameplay sidecar unreadable: {e.Message}"); return result; }
            try { interior = DeckSidecarJson.AsObject(DeckSidecarJson.Parse(interiorSidecarJson)); }
            catch (Exception e) { result.Errors.Add($"interior sidecar unreadable: {e.Message}"); return result; }

            if (baseDoc == null) { result.Errors.Add("base gameplay sidecar is not a JSON object."); return result; }
            if (interior == null) { result.Errors.Add("interior sidecar is not a JSON object."); return result; }

            var merged = new Dictionary<string, object>(baseDoc, StringComparer.Ordinal);

            MergeDeck(merged, baseDoc, interior, result);
            MergeWholesale(merged, interior, result);
            MergeExcluded(merged, baseDoc, interior, result);
            Restamp(merged, baseDoc, interior, result);

            if (result.Errors.Count == 0) result.Merged = merged;
            return result;
        }

        // ---- DECK: replace by id, else append -------------------------------------------------------

        static void MergeDeck(Dictionary<string, object> merged, Dictionary<string, object> baseDoc,
                              Dictionary<string, object> interior, InteriorMergeResult result)
        {
            var walkable = DeckSidecarJson.AsObject(DeckSidecarJson.Member(interior, "WALKABLE"));
            if (walkable == null || walkable.Count == 0)
            {
                result.Changes.Add("DECK: interior contributes no WALKABLE levels — base DECK untouched.");
                return;
            }

            var baseDeck = DeckSidecarJson.AsArray(DeckSidecarJson.Member(baseDoc, "DECK"));
            var deck = baseDeck != null ? new List<object>(baseDeck) : new List<object>();
            if (baseDeck == null)
                result.Changes.Add("DECK: base had no DECK section — the interior's levels create it.");

            foreach (var kv in walkable)
            {
                string levelId = kv.Key;
                var entry = BuildDeckEntry(levelId, kv.Value, result);
                if (entry == null) continue;

                int at = IndexOfId(deck, levelId);
                if (at >= 0)
                {
                    deck[at] = entry;
                    result.Changes.Add($"DECK '{levelId}': REPLACED the base entry (same id).");
                }
                else
                {
                    deck.Add(entry);
                    result.Changes.Add($"DECK '{levelId}': appended (no base entry with that id).");
                }
            }
            merged["DECK"] = deck;
        }

        static Dictionary<string, object> BuildDeckEntry(string levelId, object level, InteriorMergeResult result)
        {
            var poly = DeckSidecarJson.AsArray(DeckSidecarJson.Member(level, "polygon"));
            if (poly == null || poly.Count < 3)
            {
                result.Errors.Add($"WALKABLE '{levelId}': polygon absent or fewer than 3 points — " +
                                  "cannot become a DECK entry.");
                return null;
            }
            if (!DeckSidecarJson.TryDouble(DeckSidecarJson.Member(level, "z"), out double z))
            {
                result.Errors.Add($"WALKABLE '{levelId}': no 'z' — cannot become a flat DECK entry.");
                return null;
            }

            var entry = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["id"] = levelId,
                ["z"] = z,
                ["winding"] = Winding,
                ["polygon"] = poly,
            };

            // Obstructions travel as _notes, never as holes in the polygon (owner, 2026-07-22).
            var obstructions = DeckSidecarJson.AsArray(DeckSidecarJson.Member(level, "obstructions"));
            if (obstructions != null && obstructions.Count > 0) entry["_notes"] = obstructions;
            return entry;
        }

        static int IndexOfId(List<object> entries, string id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                string got = DeckSidecarJson.String(DeckSidecarJson.Member(entries[i], "id"));
                if (string.Equals(got, id, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        // ---- THRESHOLD / STAIRS / LADDER / INTERACT: wholesale ---------------------------------------

        static void MergeWholesale(Dictionary<string, object> merged, Dictionary<string, object> interior,
                                   InteriorMergeResult result)
        {
            for (int i = 0; i < WholesaleSections.Length; i++)
            {
                string section = WholesaleSections[i];
                if (!interior.TryGetValue(section, out object value) || value == null)
                {
                    result.Changes.Add($"{section}: absent from the interior — base kept " +
                                       "(absence is data, never an empty section).");
                    continue;
                }
                bool had = merged.ContainsKey(section);
                merged[section] = value;
                result.Changes.Add(had
                    ? $"{section}: REPLACED wholesale by the interior's."
                    : $"{section}: added from the interior.");
            }
        }

        // ---- _excluded: union ------------------------------------------------------------------------

        static void MergeExcluded(Dictionary<string, object> merged, Dictionary<string, object> baseDoc,
                                  Dictionary<string, object> interior, InteriorMergeResult result)
        {
            var fromInterior = DeckSidecarJson.AsObject(DeckSidecarJson.Member(interior, "_excluded"));
            if (fromInterior == null || fromInterior.Count == 0)
            {
                result.Changes.Add("_excluded: interior declares none — base kept.");
                return;
            }

            var fromBase = DeckSidecarJson.AsObject(DeckSidecarJson.Member(baseDoc, "_excluded"));
            var union = fromBase != null
                ? new Dictionary<string, object>(fromBase, StringComparer.Ordinal)
                : new Dictionary<string, object>(StringComparer.Ordinal);

            int added = 0, overwritten = 0;
            foreach (var kv in fromInterior)
            {
                if (union.ContainsKey(kv.Key))
                {
                    // Two statements about the same omission. The interior's is the newer one and wins,
                    // but silently taking it is how a decision gets un-recorded — so it is named.
                    result.Changes.Add($"_excluded '{kv.Key}': the interior's wording REPLACES the " +
                                       "base's — both describe the same omission.");
                    overwritten++;
                }
                else added++;
                union[kv.Key] = kv.Value;
            }
            merged["_excluded"] = union;
            result.Changes.Add($"_excluded: merged — {added} added, {overwritten} overwritten, " +
                               $"{union.Count} total.");
        }

        // ---- provenance re-stamp ---------------------------------------------------------------------

        /// <summary>
        /// The merged file describes the REVISED hull, so it carries that rig's SHA — and
        /// <c>_supersedes</c> carries the one it replaced, which is the base's own
        /// <c>derivedFromRigSha256</c>. That is the stale-merge detector the manifest describes, and
        /// deriving it rather than accepting it is what makes it trustworthy.
        /// </summary>
        static void Restamp(Dictionary<string, object> merged, Dictionary<string, object> baseDoc,
                            Dictionary<string, object> interior, InteriorMergeResult result)
        {
            string interiorSha = DeckSidecarJson.String(
                DeckSidecarJson.Member(interior, "derivedFromRigSha256")) ?? "";
            if (!string.IsNullOrEmpty(interiorSha))
            {
                merged["interiorDerivedFromRigSha256"] = interiorSha;
                result.Changes.Add("interiorDerivedFromRigSha256: stamped from the interior sidecar.");
            }

            string previousHullSha = DeckSidecarJson.String(
                DeckSidecarJson.Member(baseDoc, "derivedFromRigSha256")) ?? "";
            string newHullSha = SingleHullRigSha(interior);

            if (string.IsNullOrEmpty(newHullSha))
            {
                result.Errors.Add("interior sidecar has no single hullRigSha256 entry — the merged file " +
                                  "would not say which loft it describes.");
                return;
            }
            if (string.Equals(previousHullSha, newHullSha, StringComparison.OrdinalIgnoreCase))
            {
                result.Changes.Add("hull rig SHA unchanged — the interior was measured against the " +
                                   "base's own loft, so no _supersedes is written.");
                return;
            }

            merged["derivedFromRigSha256"] = newHullSha;
            if (!string.IsNullOrEmpty(previousHullSha)) merged["_supersedes"] = previousHullSha;
            result.Changes.Add("derivedFromRigSha256: re-stamped to the revised hull rig; _supersedes " +
                               "carries the one it replaces.");
        }

        /// <summary>The one SHA the interior's <c>hullRigSha256</c> object unanimously pins
        /// (<see cref="BoatInteriorHullResolver.TryUnanimousHullRigPin"/> — the fourth of the four
        /// same-shape gates; the sport fishers' two identically-stamped entries are one loft), or
        /// empty when the pin is absent or its entries disagree.</summary>
        static string SingleHullRigSha(Dictionary<string, object> interior)
        {
            var pin = DeckSidecarJson.AsObject(DeckSidecarJson.Member(interior, "hullRigSha256"));
            return BoatInteriorHullResolver.TryUnanimousHullRigPin(pin, out _, out string sha, out _)
                ? sha : "";
        }
    }
}
