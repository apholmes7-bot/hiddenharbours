using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE GAMEPLAY HALF OF THE 23-HULL FLEET PACK — the <see cref="BoatHullDef"/>s that turned #541's and
    /// #543's baked art into boats that can actually be sailed.
    ///
    /// <para><b>Why this file is asset-reading and paranoid.</b> Every def it guards was hand-written as
    /// Unity YAML with hand-minted GUIDs, in a container with no Unity in it. That failure mode is specific
    /// and silent: a wrong GUID is still well-formed YAML, still compiles, still imports — and resolves to
    /// <c>null</c> at runtime, which is a boat you cycle into and find invisible. Nothing in code review
    /// catches a transposed hex digit. <see cref="EveryPackHull_ResolvesItsVisual_AndThatVisualIsAMesh"/> is
    /// the check that does, and it walks the WHOLE chain — hull → visual → mesh def → mesh, ramps and
    /// dither table — because a reference that resolves to the wrong asset is as bad as one that resolves
    /// to nothing.</para>
    ///
    /// <para><b>The numbers are derived, not typed, and the derivations are asserted rather than described.</b>
    /// Two of them do real work. <see cref="TheGameplayDraught_IsTheMeshDraftOnTheFleetsOwnRatio"/> pins the
    /// relationship between the two DRAFT fields that this project keeps confusing for each other, and
    /// <see cref="TheLobsterAnchorRow_RegeneratesTheCommittedLobsterBoat"/> is the whole variant family's
    /// proof: the derivation that produced 18 new hulls, run at the one cell where a committed answer
    /// already exists, must reproduce that answer exactly. It does, to the unit.</para>
    /// </summary>
    public class FleetHullDefLadderTests
    {
        const string DataBoats = "Assets/_Project/Data/Boats";

        // BoatController's force assembly, restated — the same two constants PilotableFleetContentTests
        // holds. If they drift from BoatController the PlayMode speed tests fail and point here.
        const float ForceFeelScale = 0.01f;
        const float LinearDamping = 0.2f;

        /// <summary>The fleet authors draughts on a 0.05 m granularity — round the ratio's answer the
        /// same way before comparing, or every hull fails by a rounding step.</summary>
        static float RoundToStep(float v, float step) => Mathf.Round(v / step) * step;

        static float TerminalSpeed(BoatHullDef h)
            => (h.EnginePower * ForceFeelScale)
             / (h.ForwardDrag * ForceFeelScale + (h.MassKg / 100f) * LinearDamping);

        static BoatHullDef Hull(string file)
        {
            var h = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{DataBoats}/{file}.asset");
            Assert.IsNotNull(h, $"{DataBoats}/{file}.asset is missing — this hull is part of the committed " +
                                "fleet pack, not a builder output, so it should be on disk in every clone.");
            return h;
        }

        /// <summary>
        /// The five #543 families, with the terminal speed each was tuned to. EnginePower was SOLVED from
        /// these, which is the fleet's own way round — "EnginePower is a design-unit tunable calibrated to a
        /// designed speed, not a Newton count" (PilotableFleetContentTests).
        /// </summary>
        static readonly (string File, string Id, float Speed)[] PackFamilies =
        {
            ("ZodiacFrc",              "boat.zodiac_frc",               5.70f),
            ("ZodiacHurricane",        "boat.zodiac_hurricane",         5.89f),
            ("SportSkiffMk2",          "boat.sport_skiff_mk2",          5.19f),
            ("SportFisherConvertible", "boat.sport_fisher_convertible", 5.39f),
            ("SportFisherSkybridge",   "boat.sport_fisher_skybridge",   4.98f),
        };

        static readonly string[] Sizes = { "Inshore", "Standard", "Offshore" };
        static readonly string[] Styles = { "Open", "Hardtop" };
        static readonly string[] Regions = { "Northumberland", "Fundy", "Newfoundland" };

        /// <summary>The 18 lobster variants, by file name — 3 sizes x 2 styles x 3 regions.</summary>
        static IEnumerable<string> LobsterVariantFiles()
            => from size in Sizes from style in Styles from region in Regions
               select $"Lobster{size}{style}{region}";

        /// <summary>Every def this PR authored: the five families plus the eighteen lobsters.</summary>
        static IEnumerable<string> PackFiles()
            => PackFamilies.Select(f => f.File).Concat(LobsterVariantFiles());

        // ---- the references resolve, which is the thing no reviewer can see -------------------

        [Test]
        public void EveryPackHull_ResolvesItsVisual_AndThatVisualIsAMesh()
        {
            foreach (var file in PackFiles())
            {
                var h = Hull(file);

                Assert.IsNotNull(h.Visual,
                    $"{file}: its Visual reference resolved to NULL. These defs were hand-written with " +
                    "hand-minted GUIDs, so this is what a mistyped GUID looks like — it imports cleanly and " +
                    "the boat sails invisible. Check the guid on the Visual line against the Visual's .meta.");
                Assert.AreEqual(BoatHullVariant.Mesh, h.Visual.Variant,
                    $"{file}: its Visual '{h.Visual.Id}' is not a MESH visual. Every hull in the #541/#543 " +
                    "pack is mesh-drawn — a Sprite variant here means the reference landed on the wrong asset.");
                Assert.IsTrue(h.Visual.HasHullMesh(),
                    $"{file}: its Visual '{h.Visual.Id}' has no USABLE HullMesh — the mesh, its palette " +
                    "ramps or its dither table failed to resolve, so BoatHullSkinner would fall back to a " +
                    "sprite compass this hull does not have, and she would draw nothing.");

                // Mesh-only, deliberately: no sheets were ever baked for these hulls, so there is no sprite
                // half and no V-key A/B. The picker's toggle reports "this hull has only one look" — that is
                // the correct behaviour, not a defect, and this pins WHY nobody should "fix" it.
                Assert.IsFalse(h.Visual.HasFullCompass(),
                    $"{file}: this hull is mesh-only and must have no sprite compass. If facings appeared, " +
                    "someone baked sheets nobody asked for — or the Visual reference is pointing at another " +
                    "hull's asset.");
                Assert.IsNull(h.Sprite,
                    $"{file}: no fallback Sprite, on the side dragger's precedent — if the mesh path is ever " +
                    "unavailable she draws NOTHING rather than drawing wrongly.");
            }
        }

        [Test]
        public void EveryPackHull_WearsItsOwnVisual_AndCarriesItsOwnId()
        {
            // The copy-paste guard. These 23 files were generated from one template, so the plausible defect
            // is two hulls sharing a Visual GUID or an id — which would look completely correct in every
            // other assertion here, and show up in the game as two rungs of the picker being the same boat.
            var visuals = new Dictionary<string, string>();
            var ids = new Dictionary<string, string>();

            foreach (var file in PackFiles())
            {
                var h = Hull(file);

                Assert.IsFalse(visuals.TryGetValue(h.Visual.Id, out string twin),
                    $"{file} and {twin} both point at Visual '{h.Visual.Id}' — a duplicated GUID. Two rungs " +
                    "of the picker would be the same boat, and the second one's art would never be seen.");
                visuals[h.Visual.Id] = file;

                Assert.IsFalse(ids.TryGetValue(h.Id, out string clash),
                    $"{file} and {clash} share the id '{h.Id}'.");
                ids[h.Id] = file;

                Assert.IsTrue(h.Id.StartsWith("boat."),
                    $"{file}: ids are namespaced by type — '{h.Id}' should be boat.snake_case");
                Assert.IsFalse(string.IsNullOrWhiteSpace(h.DisplayName),
                    $"{file}: the picker shows DisplayName on screen — it can't be blank");
            }
        }

        [Test]
        public void EveryPackHull_IsAnEngineHull_ThatCanMoveAndSteer()
        {
            foreach (var file in PackFiles())
            {
                var h = Hull(file);
                Assert.AreEqual(PropulsionType.Engine, h.Propulsion,
                    $"{file}: every hull in the pack is engine-driven — an Oars hull here would put the " +
                    "player at a rowing helm on a 27 m battlewagon");
                Assert.Greater(h.EnginePower, 0f, $"{file}: an Engine hull with no power can't move");
                Assert.Greater(h.RudderAuthority, 0f, $"{file}: …or can't be steered");
                Assert.Greater(h.MassKg, 0f, $"{file}: a massless hull divides by zero in the force model");
                Assert.GreaterOrEqual(h.HoldUnits, 1, $"{file}: ContentValidationTests requires a real hold");
                Assert.Greater(h.CameraWorldHeightMeters, 0f,
                    $"{file}: zero framing collapses the camera onto the boat");
            }
        }

        // ---- the two drafts, and the ratio between them ---------------------------------------

        /// <summary>
        /// ⭐ THE TWO-DRAFT RULE, PINNED. <see cref="BoatHullDef.DraughtMeters"/> (gameplay: how deep she
        /// sits, and so where she grounds) and <c>HullMeshDef.RestingDraftMeters</c> (art: where the sea is
        /// DRAWN on her planking) are different fields with different owners, and they have been mistaken
        /// for each other more than once.
        ///
        /// <para>They are not independent, though. <c>water-rendering.md</c> records the side dragger's mesh
        /// draft being set by "the same ≈0.38 visual-to-gameplay-draught ratio applied to her 2.9 m
        /// DraughtMeters" — and that ratio reproduces the ENTIRE shipped fleet, from the dory's 0.11/0.30 to
        /// the tanker's 2.47/6.50. So it is the rule, not a coincidence, and it is how every draught in this
        /// PR was derived. Asserted fleet-wide at the band the shipped hulls actually occupy (the upgraded
        /// punt and the sport twin sit high, both being "same hull, heavier engine" cases where the gameplay
        /// draught was bumped and the art never re-lofted), and tight on the hulls derived by it.</para>
        /// </summary>
        [Test]
        public void TheGameplayDraught_IsTheMeshDraftOnTheFleetsOwnRatio()
        {
            var everyHull = AssetDatabase.FindAssets($"t:{nameof(BoatHullDef)}", new[] { DataBoats })
                                         .Select(AssetDatabase.GUIDToAssetPath)
                                         .Select(AssetDatabase.LoadAssetAtPath<BoatHullDef>)
                                         .Where(h => h != null)
                                         .ToList();
            Assert.IsNotEmpty(everyHull, "no BoatHullDefs found — the search path is wrong");

            int checkedHulls = 0;

            foreach (var h in everyHull)
            {
                if (h.Visual == null || !h.Visual.HasHullMesh()) continue;      // sprite-only hulls opt out
                float mesh = h.Visual.HullMesh.RestingDraftMeters;
                if (mesh <= 0f) continue;

                float ratio = h.DraughtMeters / mesh;
                checkedHulls++;
                Assert.That(ratio, Is.InRange(2.55f, 2.95f),
                    $"{h.Id}: gameplay draught {h.DraughtMeters:0.00} m against a drawn waterline of " +
                    $"{mesh:0.00} m is a ratio of {ratio:0.000}. The fleet sits at ≈2.63 (the 1/0.38 " +
                    "water-rendering.md names). A ratio near 1 means somebody has copied the ART draft into " +
                    "the GAMEPLAY field — they are different numbers and the shallow one grounds nowhere.");
            }

            Assert.GreaterOrEqual(checkedHulls, 20,
                "this rule should be reaching the whole mesh fleet — if it is only seeing a handful, the " +
                "HullMesh chain is not resolving and the assertion above is passing vacuously");
        }

        [Test]
        public void EveryPackHulls_Draught_IsExactlyTheRatioApplied()
        {
            // The tight half: every draught in this PR is mesh_draft / 0.38, rounded to the fleet's 0.05 m
            // granularity. Nothing here was eyeballed, so nothing here should sit off the rule.
            foreach (var file in PackFiles())
            {
                var h = Hull(file);
                float expected = RoundToStep(h.Visual.HullMesh.RestingDraftMeters / 0.38f, 0.05f);
                Assert.AreEqual(expected, h.DraughtMeters, 0.001f,
                    $"{file}: draught {h.DraughtMeters:0.00} m, but her drawn waterline of " +
                    $"{h.Visual.HullMesh.RestingDraftMeters:0.00} m puts her at {expected:0.00} m on the " +
                    "fleet's ratio. Either the art side re-lofted her and the gameplay draught did not " +
                    "follow, or this number was typed rather than derived.");
            }
        }

        // ---- the lobster family: 9 rows of numbers, 18 hulls ----------------------------------

        /// <summary>
        /// ⭐ THE DERIVATION'S OWN PROOF. fleet-flotation.md §3 establishes that
        /// <c>standard/*/northumberland</c> IS the committed lobster boat — her rig's offsets table is
        /// byte-identical to that cell of the variants rig. So the scaling law that produced the other eight
        /// rows, evaluated at the anchor cell, has a committed answer to be checked against: it must
        /// reproduce <c>LobsterBoat.asset</c>, and not approximately.
        ///
        /// <para>This is the single assertion that makes the other 17 hulls' numbers trustworthy. If the law
        /// is wrong anywhere it is wrong here, where the answer is known.</para>
        /// </summary>
        [Test]
        public void TheLobsterAnchorRow_RegeneratesTheCommittedLobsterBoat()
        {
            var anchor = Hull("LobsterBoat");

            foreach (var style in Styles)
            {
                var row = Hull($"LobsterStandard{style}Northumberland");

                Assert.AreEqual(anchor.LengthMeters, row.LengthMeters, 0.001f, $"{style}: the same 12.0 m");
                Assert.AreEqual(anchor.DraughtMeters, row.DraughtMeters, 0.001f,
                    $"{style}: 0.50 m of drawn waterline on the fleet ratio is 1.30 m of draught — which is " +
                    "what the committed boat has carried all along");
                Assert.AreEqual(anchor.MassKg, row.MassKg, 0.001f, $"{style}: displacement ~L^3 at L/L = 1");
                Assert.AreEqual(anchor.ForwardDrag, row.ForwardDrag, 0.001f, $"{style}: drag ~L^2 at L/L = 1");
                Assert.AreEqual(anchor.LateralDrag, row.LateralDrag, 0.001f, $"{style}");
                Assert.AreEqual(anchor.EnginePower, row.EnginePower, 0.001f,
                    $"{style}: power SOLVED from her own measured terminal speed lands back on her own " +
                    "committed EnginePower. If this drifts, the whole family's speeds are off by the same factor.");
                Assert.AreEqual(anchor.RudderAuthority, row.RudderAuthority, 0.001f, $"{style}");
                Assert.AreEqual(anchor.HoldUnits, row.HoldUnits, $"{style}");
                Assert.AreEqual(anchor.CrewSlots, row.CrewSlots, $"{style}");
                Assert.AreEqual(anchor.MaxSafeSeaState, row.MaxSafeSeaState, $"{style}");
                Assert.AreEqual(TerminalSpeed(anchor), TerminalSpeed(row), 0.005f,
                    $"{style}: …and so she settles at the same speed the committed boat does");
            }
        }

        [Test]
        public void StyleIsNotAHullAxis_SoOpenAndHardtopShareEveryNumber()
        {
            // fleet-flotation.md §3, MEASURED not assumed: "All 18 sidecars report identical
            // loa/beam/depth/freeboard/sole/sheer half-width within each (size, region) pair. The style axis
            // changes the roof cantilever and the arch, not the planking." So the roof is ART, and a style
            // pair that differed in ANY physics field would mean somebody had invented a difference the
            // hull's own geometry does not have.
            foreach (var size in Sizes)
                foreach (var region in Regions)
                {
                    var open = Hull($"Lobster{size}Open{region}");
                    var hard = Hull($"Lobster{size}Hardtop{region}");

                    Assert.AreEqual(open.LengthMeters, hard.LengthMeters, 0.001f, $"{size}/{region}");
                    Assert.AreEqual(open.DraughtMeters, hard.DraughtMeters, 0.001f, $"{size}/{region}");
                    Assert.AreEqual(open.MassKg, hard.MassKg, 0.001f, $"{size}/{region}");
                    Assert.AreEqual(open.ForwardDrag, hard.ForwardDrag, 0.001f, $"{size}/{region}");
                    Assert.AreEqual(open.LateralDrag, hard.LateralDrag, 0.001f, $"{size}/{region}");
                    Assert.AreEqual(open.EnginePower, hard.EnginePower, 0.001f, $"{size}/{region}");
                    Assert.AreEqual(open.SeakeepingLiveliness, hard.SeakeepingLiveliness, 0.001f,
                        $"{size}/{region}");
                    Assert.AreEqual(open.HoldUnits, hard.HoldUnits, $"{size}/{region}");

                    Assert.AreNotEqual(open.Id, hard.Id,
                        $"{size}/{region}: they are still two BOATS — same hull, different art");
                    Assert.AreNotSame(open.Visual, hard.Visual,
                        $"{size}/{region}: …and two different pictures, or the roof is not drawn");
                }
        }

        [Test]
        public void TheLobsterFamily_ClimbsWithSize_OnEveryAxisThatShouldClimb()
        {
            foreach (var region in Regions)
            {
                var inshore = Hull($"LobsterInshoreOpen{region}");
                var standard = Hull($"LobsterStandardOpen{region}");
                var offshore = Hull($"LobsterOffshoreOpen{region}");

                foreach (var (a, b, step) in new[]
                         {
                             (inshore, standard, "inshore -> standard"),
                             (standard, offshore, "standard -> offshore"),
                         })
                {
                    Assert.Less(a.LengthMeters, b.LengthMeters, $"{region} {step}: 8.6 -> 12.0 -> 14.6 m");
                    Assert.Less(a.DraughtMeters, b.DraughtMeters, $"{region} {step}: she draws more");
                    Assert.Less(a.MassKg, b.MassKg, $"{region} {step}: displacement ~L^3");
                    Assert.Less(a.HoldUnits, b.HoldUnits, $"{region} {step}: …and carries more");
                    Assert.Greater(a.SeakeepingLiveliness, b.SeakeepingLiveliness,
                        $"{region} {step}: the bigger boat corks about LESS");
                    Assert.Less(a.SeakeepingMassFactor, b.SeakeepingMassFactor,
                        $"{region} {step}: …because she opposes more inertia to the waves");
                }
            }
        }

        [Test]
        public void RegionMovesTheBeam_AndTheStiffness_AndNothingElse()
        {
            // fleet-flotation.md §3 ruled region OUT of the DRAFT and said why (it is one pixel, and Fundy
            // would float with her working deck under water). Region IS a real hull-form axis though — at
            // 12 m the beam runs Fundy 4.32, Northumberland 4.40, Newfoundland 4.48 — so it moves the two
            // things beam actually decides: how much boat there is, and how stiff she is. Beamier = heavier
            // and steadier. Anything else moving with region means the axis has leaked.
            foreach (var size in Sizes)
            {
                var fundy = Hull($"Lobster{size}OpenFundy");
                var north = Hull($"Lobster{size}OpenNorthumberland");
                var newf = Hull($"Lobster{size}OpenNewfoundland");

                Assert.Less(fundy.MassKg, north.MassKg, $"{size}: Fundy is the narrowest of the three");
                Assert.Less(north.MassKg, newf.MassKg, $"{size}: …and Newfoundland the beamiest");
                Assert.Greater(fundy.SeakeepingLiveliness, north.SeakeepingLiveliness,
                    $"{size}: the narrow Fundy boat is the livelier one");
                Assert.Greater(north.SeakeepingLiveliness, newf.SeakeepingLiveliness,
                    $"{size}: …and the beamy Newfoundland boat the stiffest");

                foreach (var h in new[] { fundy, newf })
                {
                    Assert.AreEqual(north.DraughtMeters, h.DraughtMeters, 0.001f,
                        $"{size}/{h.Id}: region gets NO draft delta — fleet-flotation.md §3 measured the " +
                        "spread at 37 mm (1.2 px) and ruled it out, because Fundy lands above her own sole");
                    Assert.AreEqual(north.LengthMeters, h.LengthMeters, 0.001f,
                        $"{size}/{h.Id}: region does not change her length");
                    Assert.AreEqual(north.HoldUnits, h.HoldUnits,
                        $"{size}/{h.Id}: …nor what she can carry");
                }
            }
        }

        // ---- the speed ladder --------------------------------------------------------------

        [Test]
        public void TheFiveFamilies_HitTheirDesignedTerminalSpeeds()
        {
            foreach (var (file, id, speed) in PackFamilies)
            {
                var h = Hull(file);
                Assert.AreEqual(id, h.Id, $"{file}: ids are stable and append-only (CLAUDE.md §5)");
                Assert.AreEqual(speed, TerminalSpeed(h), 0.05f,
                    $"{file}: designed for {speed:0.00} m/s. EnginePower was solved from this — if the " +
                    "speed moved, either mass/drag were retuned without re-solving it, or somebody set " +
                    "EnginePower by taste.");
            }
        }

        [Test]
        public void EveryPackHull_StaysInsideTheSpraySheetsFrame()
        {
            // BowSprayGrading frames speed over 1.7 -> 6 m/s. Over the ceiling the spray clips flat and the
            // hull outruns her own art; under the floor she never throws any. The RIBs were deliberately
            // tuned just under the top of that frame rather than "as fast as a RIB really is".
            foreach (var file in PackFiles())
            {
                float v = TerminalSpeed(Hull(file));
                Assert.Greater(v, 1.7f, $"{file}: {v:0.00} m/s is below the spray sheet's floor");
                Assert.LessOrEqual(v, 6f, $"{file}: {v:0.00} m/s is past the spray sheet's 6 m/s ceiling");
            }
        }

        [Test]
        public void TheTwoRibs_AreTheFastestHullsAfloat_AndTheTankerIsStillTheSlowest()
        {
            // The ramp's top and the ladder's bottom, as a single claim about the whole cycle — this is the
            // shape of the roster the owner walks with F, asserted rather than described in a comment.
            float hurricane = TerminalSpeed(Hull("ZodiacHurricane"));
            float frc = TerminalSpeed(Hull("ZodiacFrc"));

            foreach (var other in new[] { "ConsoleSkiff", "SportSkiff", "SportSkiffTwin", "SportSkiffMk2",
                                          "CapeIslander", "LobsterBoat", "SideDragger", "SternTrawler",
                                          "CoastalPacket", "Tanker", "SportFisherConvertible",
                                          "SportFisherSkybridge" })
                Assert.Greater(hurricane, TerminalSpeed(Hull(other)),
                    $"the Hurricane ({hurricane:0.00} m/s) is the fastest thing afloat — {other} beats her");

            Assert.Greater(hurricane, frc, "…and the bigger RIB is the faster of the two");
            Assert.Greater(frc, TerminalSpeed(Hull("SportSkiffTwin")),
                "the FRC still out-runs the twin, which is what put both RIBs after her in the cycle");
        }

        [Test]
        public void TheSportSkiffMk2_IsTheSameLengthAsHerV1Sister_AndADifferentHull()
        {
            // fleet-flotation.md §5: "she is a second hull under a new id and the committed skiff keeps her
            // own numbers untouched". Both halves matter — the reshape has to be REAL (or she is a pointless
            // rung) and it must not have leaked back into the boat she was cut from.
            var v1 = Hull("SportSkiff");
            var mk2 = Hull("SportSkiffMk2");

            Assert.AreEqual(v1.LengthMeters, mk2.LengthMeters, 0.001f,
                "the reshape kept her 7.0 m — it is the SECTION that changed, not the length");
            Assert.AreEqual("boat.sport_skiff", v1.Id, "…and the v1 keeps her own id");
            Assert.AreEqual(950f, v1.MassKg, 0.001f, "…and her own numbers, untouched by this PR");
            Assert.AreEqual(1600f, v1.EnginePower, 0.001f);
            Assert.AreEqual(0.5f, v1.DraughtMeters, 0.001f);

            Assert.Greater(mk2.MassKg, v1.MassKg, "the deep-V is more boat at the same length");
            Assert.Greater(mk2.DraughtMeters, v1.DraughtMeters,
                "…and she sits deeper: 0.19 -> 0.36 m of drawn waterline, +173 mm, which is what a deep-V " +
                "with almost no waterplane at the keel does");
            Assert.Greater(mk2.LateralDrag, v1.LateralDrag,
                "…and she TRACKS, which is the whole point of a chined deep-V");
            Assert.Greater(TerminalSpeed(mk2), TerminalSpeed(v1), "…and she is the quicker boat");
        }

        // ---- the picker cycle, read off the builder's own list ---------------------------------

        [Test]
        public void ThePickerRoster_NamesOnlyHullsThatActuallyLoad()
        {
            // The roster is a list of FILE NAMES, so a typo in one is a silently missing rung — the builder
            // filters nulls and only warns. This turns that warning into a failure, for every hull that is
            // committed. The Punt is the one legitimate absence: she is builder-generated and has never been
            // committed, so a clean clone has no such file.
            var missing = StPetersBuilder.DevPickerRosterFiles
                .Where(f => f != "Punt")
                .Where(f => AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{DataBoats}/{f}.asset") == null)
                .ToList();

            Assert.IsEmpty(missing,
                "the dev picker names hulls that are not on disk, so the owner would cycle straight past " +
                "them with no warning at all: " + string.Join(", ", missing));

            var dupes = StPetersBuilder.DevPickerRosterFiles.GroupBy(f => f)
                                       .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(dupes, "a hull listed twice is the same boat on two rungs: " +
                                  string.Join(", ", dupes));
        }

        [Test]
        public void ThePickerRoster_CarriesEveryFamilyThisPrAdded()
        {
            // The point of the PR, as an assertion: the five #543 families are all sailable, and the lobster
            // family is represented by exactly one rung per SIZE — the only axis that changes how one sails.
            var roster = StPetersBuilder.DevPickerRosterFiles;

            foreach (var (file, _, _) in PackFamilies)
                Assert.Contains(file, roster,
                    $"{file} has a BoatHullDef but no rung — she is authored and unreachable");

            var lobsterRungs = roster.Where(f => f.StartsWith("Lobster") && f != "LobsterBoat").ToList();
            Assert.AreEqual(3, lobsterRungs.Count,
                "one rung per lobster SIZE, not per hull: style moves no hull metric at all and region " +
                "moves beam by +-2%, so 18 rungs would be 15 presses of F that feel the same. If the owner " +
                "rules that he wants all 18 in the cycle, this is the number to change. Found: " +
                string.Join(", ", lobsterRungs));

            foreach (var size in Sizes)
                Assert.AreEqual(1, lobsterRungs.Count(f => f.Contains(size)),
                    $"exactly one {size} lobster rung");
        }
    }
}
