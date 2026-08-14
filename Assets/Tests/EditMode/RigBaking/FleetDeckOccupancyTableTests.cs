using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The deck-occupant slot budget of the twenty-three fleet-rig-pack hulls.</b> Derivation,
    /// sources and every measurement behind the verdicts: <c>docs/design/fleet-deck-occupancy.md</c>.
    ///
    /// <para><b>Why a table in a test and no asset.</b> Unlike flotation there is not even a field to
    /// write to: <c>IsoFacetHullRenderer.DeckOccupantSlots</c> is <b>one compile-time constant for
    /// the whole fleet</b> (the facet shader holds the same literal, and <c>DeckOccupantSlotTests</c>
    /// fails if the two drift apart). So "hull X's budget" is not a number stored on X — it is the
    /// question <i>does X's demand fit the shared capacity?</i> This fixture holds the measured
    /// demand and answers it, once, where CI can adjudicate it.</para>
    ///
    /// <para><b>What overflow actually costs</b>, so the severity is not guessed at: the surplus
    /// claim is REFUSED and logged (<c>IsoFacetHullRenderer.ClaimSlot</c>), the earlier claims keep
    /// their slots, and the refused occupant draws un-occluded — the picture that shipped before
    /// #461. A visual regression, never a crash and never a corrupt save. That is exactly why it
    /// needs a test: nothing else would ever go red.</para>
    ///
    /// <para><b>The claims here are falsifiable two ways.</b> The anchor row is checked against a
    /// SHIPPED asset (<c>LobsterBoatDeckGear.asset</c>), and every per-hull anchor count is
    /// re-derived from the rig's own geometry in the repo's V8 host rather than remembered — so a
    /// rig reshape that adds a place to stand cannot leave this table quietly stale.</para>
    /// </summary>
    public class FleetDeckOccupancyTableTests
    {
        /// <summary>One hull's measured deck-occupant demand. See the doc §2 for what each term is
        /// read from; §1 for what counts as an occupant at all.</summary>
        public readonly struct Occupancy
        {
            public readonly string Variant;      // the unambiguous key: size/style/region, or the build
            public readonly int Crew;            // hands aboard at the worst beat
            public readonly int Furniture;       // deck-furniture sprites (kit stations)
            public readonly int StackSurfaces;   // ONE slot per surface, never one per pot (#484)
            public readonly int ItemsInPlay;     // the pot being worked, the fish tray
            public readonly int Dressable;       // further rig anchors nothing places YET (doc §5)
            public readonly string RigFile;

            public Occupancy(string variant, int crew, int furniture, int stackSurfaces,
                             int itemsInPlay, int dressable, string rigFile)
            {
                Variant = variant; Crew = crew; Furniture = furniture; StackSurfaces = stackSurfaces;
                ItemsInPlay = itemsInPlay; Dressable = dressable; RigFile = rigFile;
            }

            /// <summary>What the shipped pattern actually puts on her deck at once.</summary>
            public int Committed => Crew + Furniture + StackSurfaces + ItemsInPlay;

            /// <summary>Committed plus every anchor her rig defines but nothing dresses yet.</summary>
            public int Ceiling => Committed + Dressable;
        }

        const string LobsterRig = "lobsterBoatVariantsIsoRig.js";
        const string ZodiacRig = "zodiacIsoRig.js";
        const string SkiffMk2Rig = "sportSkiffMk2IsoRig.js";
        const string FisherRig = "sportFisherIsoRig2.js";

        const string ShippedLobsterGear =
            "Assets/_Project/Data/Boats/DeckGear/LobsterBoatDeckGear.asset";

        static string RigFolder => Path.Combine(RigCatalog.RepoRoot, "docs", "art", "rigs");

        /// <summary>
        /// A fresh V8 host with exactly ONE rig in it.
        ///
        /// <para>⚠️ <b>One rig per host is load-bearing, not tidiness.</b>
        /// <c>sportSkiffMk2IsoRig.js</c> installs the SAME global (<c>SportSkiffIso</c>) as the
        /// committed sport-skiff rig — the collision PR #533 flagged upstream and did not patch,
        /// because <c>docs/art/rigs/**</c> is art-director's lane. Two rigs in one host gets the
        /// wrong boat with no error at all.</para>
        /// </summary>
        static IRigScriptHost HostFor(string rigFile)
        {
            var host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Path.Combine(RigFolder, rigFile)));
            return host;
        }

        // ---- the authored table ---------------------------------------------------------------

        /// <summary>
        /// The measured demand, keyed by the ids <c>fleet-flotation.md</c> §7 proposes (the bake PR
        /// owns the ids; a different choice moves only the key).
        ///
        /// <para>All eighteen lobster variants carry ONE row's worth of numbers because their anchor
        /// sets are byte-identical across every cell — measured, and pinned by
        /// <see cref="TheEighteenLobsterVariants_ShareOneOccupancyRow"/>. Size scales the metres and
        /// style changes the roof; neither adds a place to stand.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Occupancy> Authored = BuildTable();

        static Dictionary<string, Occupancy> BuildTable()
        {
            var t = new Dictionary<string, Occupancy>();

            // ---- 18 lobster variants: the stern-deck loop, on a hull that has both stack surfaces.
            // crew 2 (deck-loop-kit README:108-109) + furniture 4 (the kit's four stations, as both
            // shipped working hulls carry them) + stacks 2 (cockpit sole + washboard cap) + items 2
            // (the pot in play, the fish tray) = 10, which is what #481 measured on the shipped hull.
            // Dressable 5 = tubSlots(), her rig's cockpit crate anchors (line 790) — see doc §5.
            foreach (string size in new[] { "inshore", "standard", "offshore" })
            foreach (string style in new[] { "open", "hardtop" })
            foreach (string region in new[] { "northumberland", "fundy", "newfoundland" })
                t[$"hullmesh.lobster_{size}_{style}_{region}_iso"] =
                    new Occupancy($"{size}/{style}/{region}", 2, 4, 2, 2, 5, LobsterRig);

            // ---- coast-guard RHIB, 2 builds. No deck furniture, no stack surface, no crate anchor:
            // she is a boat you sit in, not a deck you work. Crew is seatsOf() (rig line 626), and on
            // the hurricane HELM (621) IS jockey seat 0 — verified numerically, so it is not a second
            // station. Doc §3.
            t["hullmesh.zodiac_hurricane_iso"] = new Occupancy("hurricane", 4, 0, 0, 0, 0, ZodiacRig);
            t["hullmesh.zodiac_frc_iso"] = new Occupancy("frc", 1, 0, 0, 0, 0, ZodiacRig);

            // ---- sport skiff v2. HELM (999, the leaning-post cushion) and PILOT (1013, the sole in
            // front of it) are one station and one person. Dressable 3 = TUBS (1007).
            t["hullmesh.sport_skiff_mk2_iso"] = new Occupancy("sport skiff v2", 1, 0, 0, 0, 3, SkiffMk2Rig);

            // ---- sport fisher, 2 builds. FOUR crew stations at four heights — HELM, TOWER_STATION,
            // CHAIR, MEZZ — which is the sportfisher scene, not an inflated count. She runs no lobster
            // loop, so furniture/stacks/items are 0 by hull type, NOT unmeasured (doc §6).
            // Dressable = tubs + rod mounts: 4+9 and 5+13. Both ceilings overflow — doc §5.
            t["hullmesh.sport_fisher_convertible_iso"] = new Occupancy("53' convertible", 4, 0, 0, 0, 13, FisherRig);
            t["hullmesh.sport_fisher_skybridge_iso"] = new Occupancy("90' skybridge", 4, 0, 0, 0, 18, FisherRig);

            return t;
        }

        /// <summary>The hulls whose CEILING exceeds the budget — the doc §5 finding, pinned so it
        /// cannot change silently in either direction.</summary>
        static bool IsKnownCeilingOverflow(string meshId) =>
            meshId.StartsWith("hullmesh.lobster_") || meshId.StartsWith("hullmesh.sport_fisher_");

        // ---- 1. the capacity this whole table is judged against ---------------------------------

        /// <summary>
        /// <b>The budget is the renderer's own constant, never a number copied into this file.</b>
        /// If somebody raises <c>DeckOccupantSlots</c> to make room for a dressing pass, every
        /// verdict below has to be re-read against the new number — so the table reads it live
        /// rather than remembering 12.
        ///
        /// <para>This test exists to say that out loud: it pins the value the doc's prose was
        /// written against. Change the constant and this names the doc that needs updating.</para>
        /// </summary>
        [Test]
        public void TheTablesCapacity_IsStillTheTwelveTheDocumentWasWrittenAgainst()
        {
            Assert.AreEqual(12, IsoFacetHullRenderer.DeckOccupantSlots,
                "IsoFacetHullRenderer.DeckOccupantSlots has moved off 12. Every verdict in " +
                "docs/design/fleet-deck-occupancy.md — including the three ceiling overflows in §5 " +
                "and the 19-simultaneous-hull id budget in §4 — was measured against 12. Re-read " +
                "the table against the new capacity and update the doc in the same PR.");
        }

        // ---- 2. the anchor: the only claim a shipped asset can falsify today --------------------

        /// <summary>
        /// <b>The method must reproduce the demand #481 measured on the shipped lobster boat.</b>
        ///
        /// <para><c>fleet-flotation.md</c> §3 established that the variants rig's
        /// <c>standard/*/northumberland</c> IS the committed <c>hullmesh.lobster_boat_iso</c> —
        /// identical offsets table, <c>L</c>, <c>DECK</c> and <c>RAKE</c>. So her furniture and
        /// stack-surface terms are not opinions: they are countable on a shipped asset, and the
        /// total has to land on the 10 that PR measured. A method that misses her is a method that
        /// is wrong about the other seventeen.</para>
        ///
        /// <para>Sabotage: change any term of the anchor row, or add a fifth mount to the shipped
        /// gear def, and this goes red naming which half moved.</para>
        /// </summary>
        [Test]
        public void TheAnchor_ReproducesTheShippedLobsterBoatsMeasuredDemand()
        {
            var gear = AssetDatabase.LoadAssetAtPath<BoatDeckGearDef>(ShippedLobsterGear);
            Assert.IsNotNull(gear, $"{ShippedLobsterGear} is missing — she is the anchor for the " +
                                   "whole occupancy method.");

            foreach (string id in new[] { "hullmesh.lobster_standard_open_northumberland_iso",
                                          "hullmesh.lobster_standard_hardtop_northumberland_iso" })
            {
                Occupancy o = Authored[id];

                Assert.AreEqual(gear.Mounts.Length, o.Furniture,
                    $"{o.Variant} IS the committed lobster boat, so her deck-furniture term must be " +
                    "the mount count on her own shipped gear def. Either the def gained/lost a " +
                    "station or the table drifted — docs/design/fleet-deck-occupancy.md §2.");

                Assert.AreEqual(gear.StackBases.Length, o.StackSurfaces,
                    $"{o.Variant}: stack SURFACES, not stack capacity. A stack claims exactly one " +
                    "slot however many pots are on it (#484 finding 2), so this must equal the " +
                    "number of StackBases on the shipped def, not their capacities " +
                    $"({string.Join("+", gear.StackBases.Select(s => s.Capacity))}).");

                Assert.AreEqual(10, o.Committed,
                    $"{o.Variant}: PR #481 measured TEN things on this hull's deck at the worst " +
                    "beat (2 hands + 4 furniture + 2 stacks + pot + tray). The method has to " +
                    "reproduce that exactly — see the table in docs/design/fleet-deck-occupancy.md §2.");
            }
        }

        // ---- 3. the verdicts ---------------------------------------------------------------------

        /// <summary>
        /// <b>The headline claim: every one of the 23 fits.</b> No incoming hull demands more than
        /// the shared capacity as the shipped pattern actually dresses a deck, so the bake arc can
        /// land all of them without touching the seam.
        ///
        /// <para>This is the test that would catch a bake PR wiring a hull with more gear than the
        /// budget holds — the failure mode is a silently un-occluded sprite, which reads as a shader
        /// bug and is otherwise invisible until somebody notices a pot through a wheelhouse.</para>
        /// </summary>
        [Test]
        public void EveryHullsCommittedDemand_FitsTheSharedSlotBudget()
        {
            var over = Authored
                .Where(k => k.Value.Committed > IsoFacetHullRenderer.DeckOccupantSlots)
                .Select(k => $"{k.Value.Variant} (needs {k.Value.Committed})")
                .ToList();

            CollectionAssert.IsEmpty(over,
                $"These hulls demand more than the {IsoFacetHullRenderer.DeckOccupantSlots} deck-" +
                "occupant slots every hull shares, so the surplus occupants will be REFUSED and draw " +
                "over the cabins they should be hidden behind: " + string.Join(", ", over) +
                ".\nThe levers, cheapest first, are in docs/design/fleet-deck-occupancy.md §5 — " +
                "prefer mesh fittings over sprites before raising the constant, which costs " +
                "simultaneous-hull headroom.");
        }

        /// <summary>
        /// <b>The §5 finding, pinned in both directions.</b> Twenty of the twenty-three overflow if
        /// every anchor their own rig defines gets dressed — the 18 lobster variants by 3 (five
        /// cockpit crate anchors), and the two sport fishers by 5 and 10 (tubs plus rod mounts).
        ///
        /// <para>Pinned <i>in both directions</i> on purpose: a new overflow appearing is a warning,
        /// and an existing one disappearing means somebody fixed it and the doc's §5 recommendation
        /// is now stale. Either way the doc needs re-reading, so either way this goes red.</para>
        /// </summary>
        [Test]
        public void TheCeilingOverflows_AreExactlyTheHullsTheDocumentNames()
        {
            int cap = IsoFacetHullRenderer.DeckOccupantSlots;

            var unexpected = Authored.Where(k => k.Value.Ceiling > cap && !IsKnownCeilingOverflow(k.Key))
                                     .Select(k => $"{k.Value.Variant} (ceiling {k.Value.Ceiling})")
                                     .ToList();
            CollectionAssert.IsEmpty(unexpected,
                "A hull the document does not name would overflow the budget once her rig's anchors " +
                "are dressed: " + string.Join(", ", unexpected) +
                ". Re-measure and add her to docs/design/fleet-deck-occupancy.md §5.");

            var healed = Authored.Where(k => k.Value.Ceiling <= cap && IsKnownCeilingOverflow(k.Key))
                                 .Select(k => $"{k.Value.Variant} (ceiling {k.Value.Ceiling})")
                                 .ToList();
            CollectionAssert.IsEmpty(healed,
                "A hull §5 names as a ceiling overflow now fits: " + string.Join(", ", healed) +
                ". Good news, but the document's recommendation is now stale — update §5.");

            // and the sizes of the two that matter most, so a drift of one anchor is named
            Assert.AreEqual(15, Authored["hullmesh.lobster_standard_open_northumberland_iso"].Ceiling,
                "The lobster ceiling is 10 committed + 5 crate anchors. It matching #481's own " +
                "prediction for a DIFFERENT decision (per-pot stack depths also land on 15) is the " +
                "signal in §5 that 12 has no room for a second dressing pass.");
            Assert.AreEqual(22, Authored["hullmesh.sport_fisher_skybridge_iso"].Ceiling,
                "The skybridge is the worst case in the fleet: 4 crew + 5 tubs + 13 rod mounts.");
        }

        // ---- 4. properties every row must hold ---------------------------------------------------

        /// <summary>Style and size change the roof and the metres, never the number of places to
        /// stand — so all eighteen lobster cells must carry one row's numbers.</summary>
        [Test]
        public void TheEighteenLobsterVariants_ShareOneOccupancyRow()
        {
            var lobster = Authored.Where(k => k.Key.StartsWith("hullmesh.lobster_")).ToList();
            Assert.AreEqual(18, lobster.Count, "The variants rig makes eighteen hulls.");

            foreach (var kvp in lobster)
            {
                Occupancy o = kvp.Value, a = Authored["hullmesh.lobster_standard_open_northumberland_iso"];
                Assert.AreEqual(a.Committed, o.Committed, $"{o.Variant}: committed demand");
                Assert.AreEqual(a.Dressable, o.Dressable, $"{o.Variant}: dressable anchors");
            }
        }

        [Test]
        public void EveryAuthoredRow_IsInternallyCoherent()
        {
            foreach (var kvp in Authored)
            {
                Occupancy o = kvp.Value;
                Assert.GreaterOrEqual(o.Crew, 1,
                    $"{o.Variant}: a pilotable hull carries at least the person steering her.");
                Assert.GreaterOrEqual(o.Dressable, 0, $"{o.Variant}: dressable cannot be negative.");
                Assert.GreaterOrEqual(o.Ceiling, o.Committed,
                    $"{o.Variant}: the ceiling includes the committed demand by construction.");
                Assert.That(o.StackSurfaces, Is.LessThanOrEqualTo(2),
                    $"{o.Variant}: the fleet's working hulls carry two pot surfaces (sole + " +
                    "washboard cap). A third needs a rig that has one.");
            }
        }

        [Test]
        public void EveryAuthoredId_FollowsTheProjectConvention()
        {
            Assert.AreEqual(23, Authored.Count,
                "The pack is 23 hulls (18 lobster + 2 zodiac + sport skiff v2 + 2 sport fisher) — " +
                "the same set docs/design/fleet-flotation.md floats.");

            foreach (string id in Authored.Keys)
                Assert.That(id, Does.Match(@"^hullmesh\.[a-z0-9]+(_[a-z0-9]+)*$"),
                    "CLAUDE.md §5: ids are type.snake_case, append-only and stable.");
        }

        /// <summary>Every row must name a rig that is actually on disk — the same derived-from-disk
        /// discipline <c>FleetFlotationTableTests.TheProvisionalFlag_IsExactlyWhetherHerRigIsOnDisk</c>
        /// uses, so a table row can never outlive the geometry it was cut from.</summary>
        [Test]
        public void EveryRigThisTableMeasures_IsOnDisk()
        {
            foreach (string rig in Authored.Values.Select(o => o.RigFile).Distinct())
                Assert.IsTrue(File.Exists(Path.Combine(RigFolder, rig)),
                    $"docs/art/rigs/{rig} is absent, but this table measured hulls from it. The " +
                    "numbers cannot be re-derived or trusted until the rig is back.");
        }

        // ---- 5. re-derive the anchor counts from the rigs themselves -----------------------------

        /// <summary>
        /// <b>The lobster variants' five crate anchors, read from the rig rather than remembered</b>
        /// — swept over all eighteen cells, and the axis lists checked against the rig's own tables
        /// first so a drop that adds a fourth region cannot leave this sweeping the old eighteen.
        ///
        /// <para>Those five anchors ARE the §5 finding: nothing places anything on them today, and
        /// the moment a lane does, this hull and the shipped lobster boat alike go to 15.</para>
        /// </summary>
        [Test]
        public void TheLobsterVariants_StillAnchorExactlyTheCrateSlotsTheTableCounts()
        {
            using IRigScriptHost host = HostFor(LobsterRig);

            Assert.AreEqual("inshore,standard,offshore|open,hardtop|northumberland,fundy,newfoundland",
                host.EvaluateString(
                    "LobsterBoatVariantsIso.SIZES.map(s=>s.id).join(',')+'|'+" +
                    "LobsterBoatVariantsIso.STYLES.map(s=>s.id).join(',')+'|'+" +
                    "LobsterBoatVariantsIso.REGIONS.map(s=>s.id).join(',')"),
                "The variants rig's own axes are no longer the eighteen cells this table measured.");

            foreach (var kvp in Authored.Where(k => k.Key.StartsWith("hullmesh.lobster_")))
            {
                string[] p = kvp.Value.Variant.Split('/');
                string v = $"{{size:'{p[0]}',style:'{p[1]}',region:'{p[2]}'}}";

                Assert.AreEqual(kvp.Value.Dressable,
                    (int)host.EvaluateNumber($"LobsterBoatVariantsIso.anchors({v}).tubs.length"),
                    $"{kvp.Value.Variant}: tubSlots() no longer anchors the number of cockpit crates " +
                    "this table's ceiling was measured from (rig line 790). Re-measure §5.");

                Assert.AreEqual(2,
                    (int)host.EvaluateNumber($"LobsterBoatVariantsIso.gameplayGeometry({v}).WASHBOARD.length"),
                    $"{kvp.Value.Variant}: she is counted as having a washboard cap to stack pots on. " +
                    "If the rig lost her washboards, her stack-surface term is no longer 2.");
            }
        }

        /// <summary>
        /// The zodiac's crew term is her rig's own seat table, and the hurricane's helm is jockey
        /// seat 0 rather than a fifth station — the coincidence that keeps her at 4 and not 5. Both
        /// re-derived here, because "the helm is one of the seats" is exactly the kind of claim that
        /// is true when written and false after a reshape.
        /// </summary>
        [Test]
        public void TheZodiacsCrewTerm_IsHerRigsOwnSeatTable()
        {
            using IRigScriptHost host = HostFor(ZodiacRig);

            Assert.AreEqual("hurricane,frc", host.EvaluateString("ZodiacIso.buildIds.join(',')"),
                "The zodiac's builds are no longer the two this table measured.");

            Assert.AreEqual(Authored["hullmesh.zodiac_hurricane_iso"].Crew,
                (int)host.EvaluateNumber("ZodiacIso.seatsOf(ZodiacIso.BUILDS['hurricane']).length"),
                "hurricane: seatsOf() (rig line 626) no longer returns the crew count in the table.");
            Assert.AreEqual(Authored["hullmesh.zodiac_frc_iso"].Crew,
                (int)host.EvaluateNumber("ZodiacIso.seatsOf(ZodiacIso.BUILDS['frc']).length"),
                "frc: seatsOf() no longer returns the crew count in the table.");

            Assert.IsTrue(host.EvaluateBool(
                    "(function(){var B=ZodiacIso.BUILDS['hurricane'];" +
                    "var h={x:0.34,y:0.115*B.L-0.62};" +
                    "return ZodiacIso.seatsOf(B).some(function(s){" +
                    "return Math.abs(s.x-h.x)<1e-9 && Math.abs(s.y-h.y)<1e-9;});})()"),
                "The hurricane's HELM (rig line 621) no longer coincides with one of her jockey " +
                "seats, so the helm is now a SEPARATE station and her crew term should be 5, not 4.");
        }

        /// <summary>The sport skiff v2's three tub anchors, read from the rig she is actually cut
        /// from — under the Mk2 filename, because the v2 rig installs the shipped skiff's global and
        /// this fixture must not be the thing that gets the wrong boat.</summary>
        [Test]
        public void TheSportSkiffMk2_StillAnchorsTheTubsTheTableCounts()
        {
            using IRigScriptHost host = HostFor(SkiffMk2Rig);

            Assert.AreEqual(Authored["hullmesh.sport_skiff_mk2_iso"].Dressable,
                (int)host.EvaluateNumber("SportSkiffIso.TUBS.length"),
                "sport skiff v2: TUBS (rig line 1007) no longer matches her dressable term.");
            Assert.AreEqual(7.0d, host.EvaluateNumber("SportSkiffIso.L"), 1e-6,
                "This host loaded a 7.0 m rig that is not the Mk2 — check the global collision " +
                "PR #533 flagged (she installs SportSkiffIso, the shipped skiff's own global).");
            Assert.AreEqual(0.46d, host.EvaluateNumber("SportSkiffIso.DECK"), 1e-6,
                "DECK 0.46 is the v2's sole; the committed skiff's is 0.28. If this reads 0.28 the " +
                "host has the WRONG BOAT and every number derived from it is the wrong hull's.");
        }

        /// <summary>
        /// <b>The sport fisher's census — the worst case in the fleet.</b> Four crew stations, and
        /// tubs plus rod mounts that take her ceiling to 17 and 22.
        ///
        /// <para>Also pins the geometric fact §5 records as measured-and-not-claimed: her
        /// rocket-launcher rods sit BELOW her tower station, so tower structure can occlude them and
        /// they cannot be dismissed as "too high to need a slot". That was the obvious way to shrink
        /// her ceiling; the geometry refuses it, and the refusal is worth keeping true.</para>
        /// </summary>
        [Test]
        public void TheSportFishersCensus_IsStillHerRigsOwn()
        {
            using IRigScriptHost host = HostFor(FisherRig);

            Assert.AreEqual("convertible,skybridge",
                host.EvaluateString("SportFisherIso2.HULLS.map(h=>h.id).join(',')"),
                "The sport fisher's builds are no longer the two this table measured.");

            foreach ((string id, string key) in new[]
                     { ("convertible", "hullmesh.sport_fisher_convertible_iso"),
                       ("skybridge", "hullmesh.sport_fisher_skybridge_iso") })
            {
                Occupancy o = Authored[key];

                int tubs = (int)host.EvaluateNumber($"SportFisherIso2.byId('{id}').TUBS.length");
                int rods = (int)host.EvaluateNumber($"SportFisherIso2.byId('{id}').RODS.length");
                Assert.AreEqual(o.Dressable, tubs + rods,
                    $"{o.Variant}: her rig now anchors {tubs} tubs + {rods} rod mounts, and the " +
                    $"table's ceiling was measured from {o.Dressable}. Re-measure §5.");

                Assert.AreEqual(o.Crew, (int)host.EvaluateNumber(
                        $"['HELM','TOWER_STATION','CHAIR','MEZZ'].filter(function(k){{" +
                        $"return !!SportFisherIso2.byId('{id}')[k];}}).length"),
                    $"{o.Variant}: her rig no longer defines the four crew stations (helm, tower, " +
                    "fighting chair, mezzanine) her crew term counts.");

                Assert.IsTrue(host.EvaluateBool(
                        $"(function(){{var h=SportFisherIso2.byId('{id}');" +
                        "var rocket=h.RODS.filter(function(r){return r.z>h.DECK+3;});" +
                        "return rocket.length>0 && rocket.every(function(r){" +
                        "return r.z < h.TOWER_STATION.z;});})()"),
                    $"{o.Variant}: her rocket-launcher rods no longer sit BELOW the tower station. " +
                    "docs/design/fleet-deck-occupancy.md §5 records that they do, as the reason they " +
                    "cannot be written off as too high to need a slot. If they now stand clear of " +
                    "every piece of tower structure, that argument is back on and her ceiling drops.");
            }
        }

        // ---- 6. the handover guard ----------------------------------------------------------------

        /// <summary>
        /// <b>Inert today, binding the moment a bake lands.</b> Every rig in this table is currently
        /// in <see cref="HullMeshFleet.NotHulls"/>, so nothing here is wired into a scene yet. When
        /// one of them joins the fleet, her demand must still fit the budget the seam ships with.
        ///
        /// <para>This is the half the flotation fixture's coverage guard does for drafts: it is what
        /// stops a hull arriving in a harbour with more on her deck than her hull can hide.</para>
        /// </summary>
        [Test]
        public void EveryTableHull_ThatHasJoinedTheFleet_StillFitsTheBudget()
        {
            int cap = IsoFacetHullRenderer.DeckOccupantSlots;
            var wrong = new List<string>();

            foreach (var hull in HullMeshFleet.Hulls)
            {
                if (!Authored.TryGetValue(hull.MeshId, out Occupancy o)) continue;
                if (o.Committed > cap)
                    wrong.Add($"{hull.Key} ({o.Variant}) needs {o.Committed} of {cap}");
            }

            CollectionAssert.IsEmpty(wrong,
                "A hull has joined HullMeshFleet.Hulls demanding more deck-occupant slots than the " +
                "renderer shares out, so her surplus gear will draw through her own cabins: " +
                string.Join("; ", wrong) + ".\nSee docs/design/fleet-deck-occupancy.md §5.");
        }
    }
}
