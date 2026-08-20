using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The gameplay-sidecar merge semantics, pinned against the one sentence that defines them: "DECK
    /// entries replace by <c>id</c>, THRESHOLD/STAIRS/LADDER/INTERACT replace wholesale,
    /// <c>_excluded</c> merges."
    ///
    /// <para><b>Pure, and writing nothing.</b> These run on strings; no committed sidecar is opened,
    /// let alone modified. That is deliberate — the point of having the merge as code is that its
    /// behaviour can be argued about in a test rather than discovered after eleven files have been
    /// overwritten.</para>
    /// </summary>
    public class BoatInteriorGameplayMergeTests
    {
        const string BaseHullSha = "1111111111111111111111111111111111111111111111111111111111111111";
        const string NewHullSha = "2222222222222222222222222222222222222222222222222222222222222222";
        const string InteriorSha = "3333333333333333333333333333333333333333333333333333333333333333";

        static string Base(string deck = null, string excluded = null) =>
            "{\n" +
            "  \"schema\": \"hidden-harbours/boat-gameplay@1\",\n" +
            "  \"rig\": \"testBoatIsoRig.js\",\n" +
            $"  \"derivedFromRigSha256\": \"{BaseHullSha}\",\n" +
            "  \"DECK\": " + (deck ?? "[ { \"id\": \"cockpit\", \"z\": 0.8, \"winding\": \"ccw_from_above\",\n" +
                              "    \"polygon\": [[-1,-2],[1,-2],[1,0],[-1,0]] } ]") + ",\n" +
            "  \"CLEATS\": [ { \"id\": \"bow_1\" } ],\n" +
            "  \"_excluded\": " + (excluded ?? "{ \"WASHBOARD\": \"open boat\" }") + "\n" +
            "}";

        static string Interior(string walkable = null, string extra = "") =>
            "{\n" +
            "  \"schema\": \"hidden-harbours/boat-interior@1\",\n" +
            $"  \"derivedFromRigSha256\": \"{InteriorSha}\",\n" +
            $"  \"hullRigSha256\": {{ \"testBoatIsoRig\": \"{NewHullSha}\" }},\n" +
            "  \"WALKABLE\": " + (walkable ?? "{\n" +
                "    \"house_sole\": { \"z\": 1.1, \"polygon\": [[-1.2,0.6],[1.2,0.6],[1.2,2.4],[-1.2,2.4]],\n" +
                "      \"obstructions\": [ { \"id\": \"stove\", \"footprint\": [[-1.1,2.0],[-0.6,2.0],[-0.6,2.3],[-1.1,2.3]] } ] }\n" +
                "  }") + ",\n" +
            "  \"THRESHOLD\": { \"id\": \"entry\", \"mechanism\": \"sliding\" },\n" +
            "  \"INTERACT\": [ { \"id\": \"helm\" } ],\n" +
            "  \"_excluded\": { \"engine_room\": \"abstracted\" }" +
            extra + "\n}";

        static List<object> Deck(InteriorMergeResult r) =>
            DeckSidecarJson.AsArray(DeckSidecarJson.Member(r.Merged, "DECK"));

        static string IdAt(List<object> deck, int i) =>
            DeckSidecarJson.String(DeckSidecarJson.Member(deck[i], "id"));

        // ---- DECK: replace by id ------------------------------------------------------------------

        [Test]
        public void AWalkableLevelWithNoMatchingIdIsAppended()
        {
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(Base(), Interior());

            Assert.IsTrue(r.Ok, string.Join(" | ", r.Errors));
            List<object> deck = Deck(r);
            Assert.AreEqual(2, deck.Count);
            Assert.AreEqual("cockpit", IdAt(deck, 0), "the base's own decks survive, in place");
            Assert.AreEqual("house_sole", IdAt(deck, 1));
            Assert.IsTrue(r.Changes.Any(c => c.Contains("house_sole") && c.Contains("appended")));
        }

        [Test]
        public void AWalkableLevelSharingABaseDeckIdReplacesItInPlace()
        {
            // The rule is replace-by-id, not append-always: a hull whose extractor already published
            // `house_sole` must end with ONE of them, at the same index, carrying the interior's numbers.
            string baseDeck =
                "[ { \"id\": \"cockpit\", \"z\": 0.8, \"polygon\": [[-1,-2],[1,-2],[1,0],[-1,0]] },\n" +
                "  { \"id\": \"house_sole\", \"z\": 9.99, \"polygon\": [[0,0],[1,0],[1,1],[0,1]] },\n" +
                "  { \"id\": \"foredeck\", \"z\": 1.2, \"polygon\": [[-1,2],[1,2],[1,3],[-1,3]] } ]";
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(Base(baseDeck), Interior());

            Assert.IsTrue(r.Ok, string.Join(" | ", r.Errors));
            List<object> deck = Deck(r);
            Assert.AreEqual(3, deck.Count, "replaced, not appended");
            Assert.AreEqual("house_sole", IdAt(deck, 1), "and replaced IN PLACE, not moved to the end");
            Assert.AreEqual(1.1, DeckSidecarJson.Float(DeckSidecarJson.Member(deck[1], "z")), 1e-4,
                            "the interior's z wins over the base's");
            Assert.IsTrue(r.Changes.Any(c => c.Contains("house_sole") && c.Contains("REPLACED")));
        }

        [Test]
        public void ObstructionsTravelAsNotesAndNeverAsHolesInThePolygon()
        {
            // The owner's 2026-07-22 ruling, kept literally: obstructions become game-side colliders
            // sourced from notes; deck polygons are never cut.
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(Base(), Interior());
            object house = Deck(r).Single(e => DeckSidecarJson.String(DeckSidecarJson.Member(e, "id")) == "house_sole");

            Assert.AreEqual(4, DeckSidecarJson.AsArray(DeckSidecarJson.Member(house, "polygon")).Count,
                            "the sole is still a plain quad — no hole was cut for the stove");
            Assert.AreEqual(1, DeckSidecarJson.AsArray(DeckSidecarJson.Member(house, "_notes")).Count);
            Assert.AreEqual(BoatInteriorGameplayMerge.Winding,
                            DeckSidecarJson.String(DeckSidecarJson.Member(house, "winding")));
        }

        [Test]
        public void ALevelWithNoObstructionsGrowsNoEmptyNotesKey()
        {
            string walkable = "{ \"cuddy\": { \"z\": 0.4, \"polygon\": [[0,0],[1,0],[1,1],[0,1]] } }";
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(Base(), Interior(walkable));
            object cuddy = Deck(r).Single(e => DeckSidecarJson.String(DeckSidecarJson.Member(e, "id")) == "cuddy");

            Assert.IsNull(DeckSidecarJson.Member(cuddy, "_notes"));
        }

        // ---- wholesale sections ----------------------------------------------------------------------

        [Test]
        public void WholesaleSectionsReplaceRatherThanMerge()
        {
            string baseWithSections = Base().Replace(
                "  \"CLEATS\": [ { \"id\": \"bow_1\" } ],",
                "  \"CLEATS\": [ { \"id\": \"bow_1\" } ],\n" +
                "  \"INTERACT\": [ { \"id\": \"old_a\" }, { \"id\": \"old_b\" } ],");
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(baseWithSections, Interior());

            Assert.IsTrue(r.Ok, string.Join(" | ", r.Errors));
            List<object> interact = DeckSidecarJson.AsArray(DeckSidecarJson.Member(r.Merged, "INTERACT"));
            Assert.AreEqual(1, interact.Count, "wholesale means the base's entries are GONE, not unioned");
            Assert.AreEqual("helm", DeckSidecarJson.String(DeckSidecarJson.Member(interact[0], "id")));
            Assert.IsTrue(r.Changes.Any(c => c.Contains("INTERACT") && c.Contains("REPLACED wholesale")));
        }

        [Test]
        public void ASectionAbsentFromTheInteriorLeavesTheBasesAlone()
        {
            // Absence is data — the contract's own rule. An interior with no LADDER does not blank a
            // hull that has one.
            string baseWithLadder = Base().Replace(
                "  \"CLEATS\": [ { \"id\": \"bow_1\" } ],",
                "  \"CLEATS\": [ { \"id\": \"bow_1\" } ],\n" +
                "  \"LADDER\": [ { \"id\": \"boat_deck_ladder\" } ],");
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(baseWithLadder, Interior());

            Assert.IsTrue(r.Ok, string.Join(" | ", r.Errors));
            List<object> ladder = DeckSidecarJson.AsArray(DeckSidecarJson.Member(r.Merged, "LADDER"));
            Assert.AreEqual(1, ladder.Count);
            Assert.AreEqual("boat_deck_ladder", DeckSidecarJson.String(DeckSidecarJson.Member(ladder[0], "id")));
        }

        [Test]
        public void EveryWholesaleSectionIsAccountedForInTheReport()
        {
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(Base(), Interior());
            foreach (string section in BoatInteriorGameplayMerge.WholesaleSections)
                Assert.IsTrue(r.Changes.Any(c => c.StartsWith(section + ":")),
                              $"the dry run must say what happened to {section}");
        }

        // ---- _excluded: union ---------------------------------------------------------------------------

        [Test]
        public void ExcludedIsUnionedAndTheBasesEntriesSurvive()
        {
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(Base(), Interior());
            var excluded = DeckSidecarJson.AsObject(DeckSidecarJson.Member(r.Merged, "_excluded"));

            Assert.AreEqual(2, excluded.Count);
            Assert.IsTrue(excluded.ContainsKey("WASHBOARD"), "a base decision is never dropped by a merge");
            Assert.IsTrue(excluded.ContainsKey("engine_room"));
        }

        [Test]
        public void AnExcludedKeyClaimedByBothIsTakenFromTheInteriorAndSaidOutLoud()
        {
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(
                Base(excluded: "{ \"engine_room\": \"the base's wording\" }"), Interior());
            var excluded = DeckSidecarJson.AsObject(DeckSidecarJson.Member(r.Merged, "_excluded"));

            Assert.AreEqual("abstracted", excluded["engine_room"]);
            Assert.IsTrue(r.Changes.Any(c => c.Contains("engine_room") && c.Contains("REPLACES")),
                          "overwriting a recorded decision silently is how it stops being a record");
        }

        // ---- provenance re-stamp -------------------------------------------------------------------------

        [Test]
        public void TheMergedFileCarriesTheNewLoftAndSupersedesTheOld()
        {
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(Base(), Interior());

            Assert.AreEqual(NewHullSha, DeckSidecarJson.String(
                DeckSidecarJson.Member(r.Merged, "derivedFromRigSha256")));
            Assert.AreEqual(BaseHullSha, DeckSidecarJson.String(
                DeckSidecarJson.Member(r.Merged, "_supersedes")),
                "_supersedes is the stale-merge detector: it must carry the loft this one replaced");
            Assert.AreEqual(InteriorSha, DeckSidecarJson.String(
                DeckSidecarJson.Member(r.Merged, "interiorDerivedFromRigSha256")));
        }

        [Test]
        public void AnInteriorMeasuredAgainstTheBasesOwnLoftWritesNoSupersedes()
        {
            // Nothing was superseded, so claiming something was would be a lie that a later stale-merge
            // check would act on.
            string interior = Interior().Replace(NewHullSha, BaseHullSha);
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(Base(), interior);

            Assert.IsTrue(r.Ok, string.Join(" | ", r.Errors));
            Assert.IsNull(DeckSidecarJson.Member(r.Merged, "_supersedes"));
        }

        [Test]
        public void AnInteriorNamingNoLoftIsRefused()
        {
            string interior = Interior().Replace(
                $"  \"hullRigSha256\": {{ \"testBoatIsoRig\": \"{NewHullSha}\" }},\n", "");
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(Base(), interior);

            Assert.IsFalse(r.Ok);
            Assert.IsNull(r.Merged, "a refused merge hands back nothing to write");
        }

        // ---- failure modes ----------------------------------------------------------------------------------

        [Test]
        public void UnreadableInputIsRefusedRatherThanPartiallyMerged()
        {
            Assert.IsFalse(BoatInteriorGameplayMerge.Merge("{ oops", Interior()).Ok);
            Assert.IsFalse(BoatInteriorGameplayMerge.Merge(Base(), "{ oops").Ok);
        }

        [Test]
        public void ALevelThatCannotBecomeADeckEntryRefusesTheWholeMerge()
        {
            string walkable = "{ \"broken\": { \"polygon\": [[0,0],[1,0],[1,1],[0,1]] } }";   // no z
            InteriorMergeResult r = BoatInteriorGameplayMerge.Merge(Base(), Interior(walkable));

            Assert.IsFalse(r.Ok);
            Assert.IsTrue(r.Errors.Any(e => e.Contains("broken")), string.Join(" | ", r.Errors));
        }
    }
}
