using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The authored flotation numbers for the fleet-rig-pack hulls, and the guard that they reach
    /// the assets.</b> Derivation, sources and the measurements behind every ruling:
    /// <c>docs/design/fleet-flotation.md</c>.
    ///
    /// <para><b>Why a table in a test rather than values on assets.</b> These three fields are
    /// GAME-SIDE — <c>RigMeshAssetBaker</c> never writes them, which is exactly what lets them survive
    /// a re-bake — and there is no asset to write them to yet: <c>lobsterBoatVariantsIsoRig.js</c> is
    /// still in <see cref="HullMeshFleet.NotHulls"/>, and the zodiac and sport-skiff-v2 rigs are not in
    /// <c>docs/art/rigs/</c> at all. So the numbers are pinned HERE, once, and
    /// <see cref="EveryFleetHull_HasFlotation_FromItsAssetOrFromThisTable"/> is what stops a hull
    /// joining the fleet without them. CLAUDE.md rule 6 asks that a test pin the TABLE rather than
    /// literals scattered per file; this is that table.</para>
    ///
    /// <para><b>The one claim that is falsifiable today</b> is
    /// <see cref="TheAnchor_MatchesTheCommittedLobsterBoat"/>. Everything else in this fixture is a
    /// property of the table until the bake lands, but the anchor is a shipped asset: the variants
    /// rig's <c>northumberland</c> offsets table is byte-identical to
    /// <c>lobsterBoatIsoRig.js</c>'s, with the same <c>L</c>, <c>DECK</c> and <c>RAKE</c>, so
    /// <c>standard/*/northumberland</c> IS <c>hullmesh.lobster_boat_iso</c> and the derivation has to
    /// reproduce her three committed fields exactly. If it does not, the method is wrong — not the
    /// asset.</para>
    /// </summary>
    public class FleetFlotationTableTests
    {
        /// <summary>One hull's hand-authored flotation, plus the geometry it was derived from.</summary>
        public readonly struct Flotation
        {
            public readonly string Variant;      // the unambiguous key: size/style/region, or the build
            public readonly float Draft;         // RestingDraftMeters
            public readonly float Deck;          // WatertightDeckHeightMeters
            public readonly float HalfBeam;      // WatertightHalfBeamMeters
            public readonly float TrueHalfBeam;  // the hull's real max sheer half-width, for provenance
            public readonly bool RigIsCommitted; // false => the rig is pack-only, so this row is provisional

            public Flotation(string variant, float draft, float deck, float halfBeam,
                             float trueHalfBeam, bool rigIsCommitted = true)
            {
                Variant = variant; Draft = draft; Deck = deck; HalfBeam = halfBeam;
                TrueHalfBeam = trueHalfBeam; RigIsCommitted = rigIsCommitted;
            }
        }

        /// <summary>
        /// The authored table. Ids are the bake's to choose and are a PROPOSAL (see the doc §6); the
        /// numbers are not. Keyed so that a different id choice moves only the key.
        ///
        /// <para>Style is not a hull axis — measured across all 18 sidecars, <c>open</c> and
        /// <c>hardtop</c> of the same size+region report identical hull metrics, so the pairs
        /// deliberately carry identical numbers and
        /// <see cref="StylePairs_ShareOneHullRow"/> pins that.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Flotation> Authored =
            new Dictionary<string, Flotation>
            {
                // ---- 18 lobster variants: draft = deck = 0.50 * the rig's own depth scalar ---------
                // Region gets NO draft delta on purpose: the hydrostatic delta is <= 37 mm (~1 px at
                // 32 px/m) and would float Fundy above her own cockpit sole. Doc §3.
                ["hullmesh.lobster_inshore_open_northumberland_iso"] = new Flotation("inshore/open/northumberland", 0.44f, 0.44f, 2.00f, 1.776f),
                ["hullmesh.lobster_inshore_hardtop_northumberland_iso"] = new Flotation("inshore/hardtop/northumberland", 0.44f, 0.44f, 2.00f, 1.776f),
                ["hullmesh.lobster_inshore_open_fundy_iso"] = new Flotation("inshore/open/fundy", 0.44f, 0.44f, 1.95f, 1.728f),
                ["hullmesh.lobster_inshore_hardtop_fundy_iso"] = new Flotation("inshore/hardtop/fundy", 0.44f, 0.44f, 1.95f, 1.728f),
                ["hullmesh.lobster_inshore_open_newfoundland_iso"] = new Flotation("inshore/open/newfoundland", 0.44f, 0.44f, 2.04f, 1.808f),
                ["hullmesh.lobster_inshore_hardtop_newfoundland_iso"] = new Flotation("inshore/hardtop/newfoundland", 0.44f, 0.44f, 2.04f, 1.808f),

                // ⚠️ THE ANCHOR. standard/*/northumberland IS the committed hullmesh.lobster_boat_iso.
                ["hullmesh.lobster_standard_open_northumberland_iso"] = new Flotation("standard/open/northumberland", 0.50f, 0.50f, 2.50f, 2.220f),
                ["hullmesh.lobster_standard_hardtop_northumberland_iso"] = new Flotation("standard/hardtop/northumberland", 0.50f, 0.50f, 2.50f, 2.220f),
                ["hullmesh.lobster_standard_open_fundy_iso"] = new Flotation("standard/open/fundy", 0.50f, 0.50f, 2.43f, 2.160f),
                ["hullmesh.lobster_standard_hardtop_fundy_iso"] = new Flotation("standard/hardtop/fundy", 0.50f, 0.50f, 2.43f, 2.160f),
                ["hullmesh.lobster_standard_open_newfoundland_iso"] = new Flotation("standard/open/newfoundland", 0.50f, 0.50f, 2.55f, 2.260f),
                ["hullmesh.lobster_standard_hardtop_newfoundland_iso"] = new Flotation("standard/hardtop/newfoundland", 0.50f, 0.50f, 2.55f, 2.260f),

                ["hullmesh.lobster_offshore_open_northumberland_iso"] = new Flotation("offshore/open/northumberland", 0.55f, 0.55f, 2.85f, 2.531f),
                ["hullmesh.lobster_offshore_hardtop_northumberland_iso"] = new Flotation("offshore/hardtop/northumberland", 0.55f, 0.55f, 2.85f, 2.531f),
                ["hullmesh.lobster_offshore_open_fundy_iso"] = new Flotation("offshore/open/fundy", 0.55f, 0.55f, 2.77f, 2.462f),
                ["hullmesh.lobster_offshore_hardtop_fundy_iso"] = new Flotation("offshore/hardtop/fundy", 0.55f, 0.55f, 2.77f, 2.462f),
                ["hullmesh.lobster_offshore_open_newfoundland_iso"] = new Flotation("offshore/open/newfoundland", 0.55f, 0.55f, 2.90f, 2.576f),
                ["hullmesh.lobster_offshore_hardtop_newfoundland_iso"] = new Flotation("offshore/hardtop/newfoundland", 0.55f, 0.55f, 2.90f, 2.576f),

                // ---- coast-guard RHIB, 2 builds. Half-beam is over the TUBES. Doc §4. --------------
                // PROVISIONAL: zodiacIsoRig.js is pack-only, so nothing here has a committed source.
                ["hullmesh.zodiac_hurricane_iso"] = new Flotation("hurricane", 0.32f, 0.42f, 1.61f, 1.480f, rigIsCommitted: false),
                ["hullmesh.zodiac_frc_iso"] = new Flotation("frc", 0.30f, 0.40f, 1.52f, 1.400f, rigIsCommitted: false),

                // ---- sport skiff v2. She does NOT inherit the committed skiff — doc §5. -----------
                // PROVISIONAL: the v2 rig is pack-only, and its shipped sidecar is STALE (it pins the
                // hash of the OLD rig), so the numbers come off the v2 rig source directly.
                ["hullmesh.sport_skiff_mk2_iso"] = new Flotation("sport skiff v2", 0.36f, 0.46f, 1.38f, 1.270f, rigIsCommitted: false),
            };

        /// <summary>The variants rig's own depth scalars, for the size-ladder claim.</summary>
        static readonly (string Size, float DepthScalar)[] SizeLadder =
            { ("inshore", 0.88f), ("standard", 1.00f), ("offshore", 1.10f) };

        // ---- 1. the anchor: the only claim a shipped asset can falsify today --------------------

        /// <summary>
        /// <b>The derivation must reproduce the committed lobster boat exactly.</b>
        ///
        /// <para><c>lobsterBoatIsoRig.js</c> carries <c>L = 12.0</c>, <c>DECK = 0.50</c>,
        /// <c>RAKE = 0.50</c> and an offsets table byte-identical to the variants rig's
        /// <c>northumberland</c> table, so the variant <c>standard/*/northumberland</c> is the same
        /// boat as <c>hullmesh.lobster_boat_iso</c> — and a method that lands anywhere else on her is
        /// a method that is wrong about the other seventeen too. Sabotage: move the anchor row's draft
        /// and this goes red.</para>
        /// </summary>
        [Test]
        public void TheAnchor_MatchesTheCommittedLobsterBoat()
        {
            var committed = AssetDatabase.LoadAssetAtPath<HullMeshDef>(
                HullMeshFleet.Get("lobsterBoat").MeshAssetPath);
            Assert.IsNotNull(committed, "hullmesh.lobster_boat_iso is missing — she is the anchor for " +
                                        "the whole lobster family.");

            foreach (string id in new[] { "hullmesh.lobster_standard_open_northumberland_iso",
                                          "hullmesh.lobster_standard_hardtop_northumberland_iso" })
            {
                Flotation f = Authored[id];
                Assert.AreEqual(committed.RestingDraftMeters, f.Draft, 1e-4f,
                    $"{f.Variant} is the committed lobster boat (identical offsets table, L, DECK and " +
                    "RAKE), so her authored draft must be the shipped one. Either the derivation " +
                    "drifted or somebody re-tuned the shipped asset — see docs/design/fleet-flotation.md §3.");
                Assert.AreEqual(committed.WatertightDeckHeightMeters, f.Deck, 1e-4f,
                    $"{f.Variant}: deck height must be the shipped hull's (both are the rig's DECK = 0.50).");
                Assert.AreEqual(committed.WatertightHalfBeamMeters, f.HalfBeam, 1e-4f,
                    $"{f.Variant}: half-beam must be the shipped hull's — the family's margin is " +
                    "calibrated as hers (2.50 / 2.220).");
            }
        }

        /// <summary>The size ladder is the rig's own depth scalar, not a hand-picked curve: every
        /// lobster row must equal 0.50 · dep for its size. Keeps a future edit from quietly turning
        /// three derived numbers into three preferences.</summary>
        [Test]
        public void TheLobsterSizeLadder_IsTheRigsOwnDepthScalar()
        {
            foreach ((string size, float dep) in SizeLadder)
            {
                float expected = 0.50f * dep;
                foreach (var kvp in Authored.Where(k => k.Key.StartsWith($"hullmesh.lobster_{size}_")))
                {
                    Assert.AreEqual(expected, kvp.Value.Draft, 5e-3f,
                        $"{kvp.Value.Variant}: draft should be 0.50 x the rig's depth scalar ({dep}).");
                    Assert.AreEqual(expected, kvp.Value.Deck, 5e-3f,
                        $"{kvp.Value.Variant}: deck should be the rig's own DECK = 0.50 x {dep}.");
                }
            }
        }

        /// <summary>Style changes the roof and the arch, never the planking — measured across all 18
        /// sidecars. So the two styles of one size+region must carry one hull row.</summary>
        [Test]
        public void StylePairs_ShareOneHullRow()
        {
            foreach ((string size, _) in SizeLadder)
                foreach (string region in new[] { "northumberland", "fundy", "newfoundland" })
                {
                    Flotation open = Authored[$"hullmesh.lobster_{size}_open_{region}_iso"];
                    Flotation hard = Authored[$"hullmesh.lobster_{size}_hardtop_{region}_iso"];
                    Assert.AreEqual(open.Draft, hard.Draft, 1e-6f, $"{size}/{region} draft");
                    Assert.AreEqual(open.Deck, hard.Deck, 1e-6f, $"{size}/{region} deck");
                    Assert.AreEqual(open.HalfBeam, hard.HalfBeam, 1e-6f, $"{size}/{region} half-beam");
                }
        }

        // ---- 2. properties every authored row must hold ----------------------------------------

        /// <summary>
        /// <b>A hull may not be authored to float with her cockpit sole under water.</b> The clamp
        /// bounds the drawn waterline at the lowest open interior surface, so a draft above that line
        /// means the clamp spends the life of the boat fighting her own datum. This is the invariant
        /// that decided the region ruling (doc §2/§3) — the hydrostatic Fundy delta breaks it.
        /// </summary>
        [Test]
        public void EveryAuthoredHull_FloatsAtOrBelowHerOwnDeck()
        {
            var over = Authored.Where(k => k.Value.Draft > k.Value.Deck + 1e-4f)
                               .Select(k => $"{k.Value.Variant} (draft {k.Value.Draft} > deck {k.Value.Deck})")
                               .ToList();
            CollectionAssert.IsEmpty(over,
                "These hulls are authored to float above their own lowest open interior surface, so " +
                "the watertight clamp fights the design waterline for ever: " + string.Join(", ", over));
        }

        [Test]
        public void EveryAuthoredHull_CarriesAPositiveDraftAndBothClampFields()
        {
            foreach (var kvp in Authored)
            {
                Assert.Greater(kvp.Value.Draft, 0f,
                    $"{kvp.Value.Variant}: a keel on the surface opens air under the whole boat in a trough.");
                Assert.Greater(kvp.Value.Deck, 0f,
                    $"{kvp.Value.Variant}: deck 0 turns the watertight clamp OFF entirely.");
                Assert.Greater(kvp.Value.HalfBeam, 0f,
                    $"{kvp.Value.Variant}: half-beam 0 degrades the clamp to root-line-only and the " +
                    "far rail boards first (water-rendering.md §24).");
                Assert.GreaterOrEqual(kvp.Value.HalfBeam, kvp.Value.TrueHalfBeam,
                    $"{kvp.Value.Variant}: the clamp must reach at least as far abeam as the hull does.");
            }
        }

        [Test]
        public void EveryAuthoredId_FollowsTheProjectConvention()
        {
            foreach (string id in Authored.Keys)
                Assert.That(id, Does.Match(@"^hullmesh\.[a-z0-9]+(_[a-z0-9]+)*$"),
                    "CLAUDE.md §5: ids are type.snake_case, append-only and stable.");
        }

        // ---- 3. the coverage guard: no hull joins the fleet without numbers ---------------------

        /// <summary>
        /// <b>The reason this fixture exists.</b> Phase 6 landed nine hulls with all three flotation
        /// fields at 0 and nothing went red, because a rig oracle cannot police a field with no rig
        /// counterpart. <c>HullMeshFleetTests</c> now catches that for hulls that are BAKED; this
        /// catches the other half — a hull that joins <see cref="HullMeshFleet.Hulls"/> having never
        /// had a number derived for her at all.
        ///
        /// <para>A hull passes if her committed def already carries all three (the eleven shipped
        /// hulls) or if this table authored them (the twenty-one incoming ones). Anything else is a
        /// hull nobody has floated.</para>
        /// </summary>
        [Test]
        public void EveryFleetHull_HasFlotation_FromItsAssetOrFromThisTable()
        {
            var unfloated = new List<string>();

            foreach (var hull in HullMeshFleet.Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                bool assetCarriesIt = def != null &&
                                      def.RestingDraftMeters > 0f &&
                                      def.WatertightDeckHeightMeters > 0f &&
                                      def.WatertightHalfBeamMeters > 0f;

                if (!assetCarriesIt && !Authored.ContainsKey(hull.MeshId))
                    unfloated.Add($"{hull.Key} ({hull.MeshId})");
            }

            CollectionAssert.IsEmpty(unfloated,
                "These hulls are in the fleet but nobody has derived a resting draft or a watertight " +
                "clamp for them, so the sea will draw through them: " + string.Join(", ", unfloated) +
                ".\nDerive the numbers (docs/design/fleet-flotation.md has the method and the " +
                "calibration set), add a row to FleetFlotationTableTests.Authored, and write them onto " +
                "the def — the baker never will.");
        }

        /// <summary>
        /// Once a hull in this table HAS been baked, her asset must carry exactly what was authored.
        /// Inert while the defs do not exist, and binding the moment they do — which is the handover
        /// this table is for.
        /// </summary>
        [Test]
        public void EveryAuthoredHull_ThatHasBeenBaked_CarriesExactlyTheseNumbers()
        {
            var wrong = new List<string>();
            int baked = 0;

            foreach (var hull in HullMeshFleet.Hulls)
            {
                if (!Authored.TryGetValue(hull.MeshId, out Flotation f)) continue;
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                if (def == null) continue;
                baked++;

                if (Mathf.Abs(def.RestingDraftMeters - f.Draft) > 1e-4f)
                    wrong.Add($"{f.Variant} draft: asset {def.RestingDraftMeters}, table {f.Draft}");
                if (Mathf.Abs(def.WatertightDeckHeightMeters - f.Deck) > 1e-4f)
                    wrong.Add($"{f.Variant} deck: asset {def.WatertightDeckHeightMeters}, table {f.Deck}");
                if (Mathf.Abs(def.WatertightHalfBeamMeters - f.HalfBeam) > 1e-4f)
                    wrong.Add($"{f.Variant} half-beam: asset {def.WatertightHalfBeamMeters}, table {f.HalfBeam}");

                // The clamp scans x over half the rig cell, so it cannot reach past it.
                float cellHalfMeters = 0.5f * def.CellW / Mathf.Max(1, def.PxPerMetre);
                Assert.Less(f.HalfBeam, cellHalfMeters,
                    $"{f.Variant}: half-beam {f.HalfBeam} m exceeds half her own cell " +
                    $"({cellHalfMeters:0.##} m) — she cannot be that wide.");
            }

            CollectionAssert.IsEmpty(wrong,
                "A baked hull disagrees with the authored flotation table. The asset is the thing that " +
                "ships, so either the bake reset the field (it must not — the baker never writes these) " +
                "or the table needs re-deriving: " + string.Join("; ", wrong));

            Assert.AreEqual(baked, Authored.Count(a => HullMeshFleet.Hulls.Any(h => h.MeshId == a.Key)),
                "Every table row whose hull is in the fleet should have been checked against a def.");
        }

        /// <summary>
        /// The three pack-only rigs are flagged, and the flag has to stay honest: once a rig lands in
        /// <c>docs/art/rigs/</c> its row stops being provisional and this says so out loud rather than
        /// letting a stale caveat harden into folklore.
        /// </summary>
        [Test]
        public void TheProvisionalRows_AreExactlyTheHullsWithNoCommittedRig()
        {
            var provisional = Authored.Where(k => !k.Value.RigIsCommitted)
                                      .Select(k => k.Value.Variant).OrderBy(v => v).ToList();
            CollectionAssert.AreEqual(new[] { "frc", "hurricane", "sport skiff v2" }, provisional,
                "The provisional set changed. If art-pipeline has imported zodiacIsoRig.js or the " +
                "sport-skiff v2 rig, clear the flag on that row and verify its numbers against the " +
                "committed source (docs/design/fleet-flotation.md §6).");
        }
    }
}
