using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The interior sidecar reader, on synthetic input: the parse, both halves of the sidecar-hash law,
    /// the unknown-section guard, and the schema checks. No disk, no assets, no scene — two strings and
    /// two byte arrays in, rooms out.
    ///
    /// <para><b>The refusal arms are the point of this fixture.</b> Twenty-five of the drop's
    /// twenty-seven sidecars pin an interior renderer that shipped in neither the kit nor the
    /// repository, and one hull's loft moved by owner ruling after her rooms were measured. Every one
    /// of those has to stop at the reader rather than becoming a plausible-looking cabin in the wrong
    /// place — so each arm gets a test that also asserts the read contributed NO geometry.</para>
    /// </summary>
    public class BoatInteriorSidecarReaderTests
    {
        const string InteriorRig = "// the interior renderer\nconst CEIL = 1.95;\n";
        const string HullRig = "// the exterior loft\nconst HX = 1.32;\n";

        static byte[] Lf(string s) => Encoding.UTF8.GetBytes(s);
        static byte[] Crlf(string s) => Encoding.UTF8.GetBytes(s.Replace("\n", "\r\n"));

        static string InteriorSha => DeckSidecarReader.Sha256Hex(Lf(InteriorRig));
        static string HullSha => DeckSidecarReader.Sha256Hex(Lf(HullRig));

        /// <summary>A complete, legal sidecar. Every test that wants a defect starts here and breaks
        /// exactly one thing, so a failure names the arm that fired.</summary>
        static string Sidecar(string interiorSha = null, string hullSha = null, string extra = "",
                              int pxPerM = 32)
        {
            if (interiorSha == null) interiorSha = InteriorSha;
            if (hullSha == null) hullSha = HullSha;
            return "{\n" +
                "  \"schema\": \"hidden-harbours/boat-interior@1\",\n" +
                "  \"hull_stem\": \"testBoatIsoRig\",\n" +
                "  \"fits_hulls\": [\"testBoatIsoRig\"],\n" +
                "  \"interior_rig\": \"boatInteriorRig.js\",\n" +
                $"  \"derivedFromRigSha256\": \"{interiorSha}\",\n" +
                $"  \"hullRigSha256\": {{ \"testBoatIsoRig\": \"{hullSha}\" }},\n" +
                $"  \"frame\": {{ \"units\": \"m\", \"scale_px_per_m\": {pxPerM} }},\n" +
                "  \"cell\": { \"w\": 480, \"h\": 420, \"pivot\": { \"x\": 240, \"y\": 232 } },\n" +
                "  \"motion\": { \"rides_hull_rock\": true },\n" +
                "  \"FOOTPRINT\": { \"house\": [[-1.3, 0.5], [1.3, 0.5], [1.3, 2.5], [-1.3, 2.5]] },\n" +
                "  \"WALKABLE\": {\n" +
                "    \"house_sole\": { \"z\": 1.1, \"polygon\": [[-1.2, 0.6], [1.2, 0.6], [1.2, 2.4], [-1.2, 2.4]],\n" +
                "      \"obstructions\": [ { \"id\": \"stove\", \"footprint\": [[-1.1, 2.0], [-0.6, 2.0], [-0.6, 2.3], [-1.1, 2.3]],\n" +
                "        \"height_above_sole_m\": 0.9, \"treatment\": \"waist_block\" } ] },\n" +
                "    \"cuddy_sole\": { \"z\": 0.4, \"polygon\": [[-0.9, 2.5], [0.9, 2.5], [0.9, 3.6], [-0.9, 3.6]] }\n" +
                "  },\n" +
                "  \"THRESHOLD\": { \"id\": \"entry\", \"side\": \"aft\", \"door_clear_width_m\": 0.74,\n" +
                "    \"door_clear_height_m\": 1.6, \"threshold_point\": [0.03, 0.47, 1.1],\n" +
                "    \"mechanism\": \"sliding\",\n" +
                "    \"slide\": { \"travel_m\": 0.8, \"keep_clear\": [[-0.4, 0.47], [1.26, 0.47], [1.26, 0.32], [-0.4, 0.32]] },\n" +
                "    \"door_cue\": { \"frames\": 8, \"played_reversed_on_exit\": true, \"suggested_ms_per_frame\": 70 } },\n" +
                "  \"STAIRS\": { \"companionways\": [ { \"id\": \"house_to_cuddy\", \"from\": \"house_sole\",\n" +
                "    \"to\": \"cuddy_sole\", \"mechanism\": \"InteriorStair\", \"total_rise_m\": 0.7 } ] },\n" +
                "  \"INTERACT\": [ { \"id\": \"helm\", \"action\": \"enter_helm\", \"reach_point\": [0, 2.1, 1.1],\n" +
                "    \"visible_facings\": [\"N\", \"NE\"] } ],\n" +
                "  \"_stem_note\": \"annotations are never unknown sections\"" +
                extra + "\n}";
        }

        static BoatInteriorRead Read(string json) =>
            BoatInteriorSidecarReader.Read(json, "test.interior.json", Lf(InteriorRig), Lf(HullRig));

        // ---- the happy path --------------------------------------------------------------------------

        [Test]
        public void ACompleteSidecarReadsEverySection()
        {
            BoatInteriorRead read = Read(Sidecar());

            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));
            Assert.AreEqual("testBoatIsoRig", read.HullStem);
            CollectionAssert.AreEqual(new[] { "testBoatIsoRig" }, read.FitsHulls);
            Assert.AreEqual(32, read.PixelsPerMetre);
            Assert.AreEqual(new Vector2Int(480, 420), read.CellPixels);
            Assert.AreEqual(new Vector2Int(240, 232), read.PivotPixels);
            Assert.IsTrue(read.RidesHullRock);
            Assert.AreEqual(4, read.Footprint.Length);
            Assert.AreEqual(2, read.Levels.Count);
            Assert.AreEqual(1, read.Anchors.Count);
            Assert.AreEqual(1, read.Routes.Count);
        }

        [Test]
        public void LevelsCarryTheirSoleHeightAndObstructions()
        {
            BoatInteriorRead read = Read(Sidecar());
            BoatInteriorLevel house = read.Levels.Single(l => l.Id == "house_sole");

            Assert.AreEqual(1.1f, house.SoleZMeters, 1e-4f);
            Assert.AreEqual(4, house.Outline.Length);
            Assert.AreEqual(1, house.Obstructions.Length);
            Assert.AreEqual("stove", house.Obstructions[0].Id);
            Assert.AreEqual("waist_block", house.Obstructions[0].Treatment);
            Assert.AreEqual(0.9f, house.Obstructions[0].HeightAboveSoleMeters, 1e-4f);

            // A level with no obstructions gets an empty array, never a null the caller must guard.
            Assert.IsNotNull(read.Levels.Single(l => l.Id == "cuddy_sole").Obstructions);
            Assert.IsEmpty(read.Levels.Single(l => l.Id == "cuddy_sole").Obstructions);
        }

        [Test]
        public void TheDoorIsReadAsADeclarationIncludingItsCue()
        {
            BoatInteriorDoor door = Read(Sidecar()).Door;

            Assert.AreEqual("entry", door.Id);
            Assert.AreEqual(BoatInteriorDoorMechanism.Sliding, door.Mechanism);
            Assert.AreEqual(new Vector3(0.03f, 0.47f, 1.1f), door.ThresholdPoint);
            Assert.AreEqual(0.74f, door.ClearWidthMeters, 1e-4f);
            Assert.AreEqual(8, door.CueFrames, "the kit bakes an 8-frame cue");
            Assert.AreEqual(70f, door.CueMillisecondsPerFrame, 1e-4f);
            Assert.IsTrue(door.CueReversedOnExit);
            Assert.AreEqual(4, door.KeepClear.Length, "a slider's keep_clear lives in its `slide` block");
        }

        // ---- refusal 1: the interior renderer's SHA ----------------------------------------------------

        [Test]
        public void ASidecarPinningARendererThatDidNotShipIsRefused()
        {
            // This is the drop's actual defect, in miniature: 25 of 27 sidecars name a renderer whose
            // sha is in neither the kit nor the repository's history.
            BoatInteriorRead read = Read(Sidecar(interiorSha: new string('5', 64)));

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("STALE INTERIOR RIG")),
                          string.Join(" | ", read.Errors));
            Assert.IsEmpty(read.Levels, "a refused sidecar must contribute no geometry at all");
        }

        [Test]
        public void AnAbsentInteriorShaIsRefusedNotDefaulted()
        {
            BoatInteriorRead read = Read(Sidecar(interiorSha: ""));

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("no derivedFromRigSha256")),
                          string.Join(" | ", read.Errors));
            Assert.IsEmpty(read.Levels);
        }

        [Test]
        public void ACrlfDerivedInteriorShaIsAcceptedAndReported()
        {
            string sha = DeckSidecarReader.Sha256Hex(Crlf(InteriorRig));
            BoatInteriorRead read = Read(Sidecar(interiorSha: sha));

            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));
            Assert.AreEqual(RigHashMatch.LineEndingNormalized, read.InteriorHashMatch);
            Assert.IsNotEmpty(read.Notes, "accepted, but never silently — a line ending cannot move a vertex");
        }

        // ---- refusal 2: the exterior loft's SHA --------------------------------------------------------

        [Test]
        public void RoomsMeasuredAgainstALoftThatMovedAreRefused()
        {
            // The cape's case: her washboards moved by owner ruling after these rooms were cut.
            BoatInteriorRead read = Read(Sidecar(hullSha: new string('b', 64)));

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("STALE HULL RIG")), string.Join(" | ", read.Errors));
            Assert.IsEmpty(read.Levels);
        }

        [Test]
        public void AnAbsentHullRigPinIsRefused()
        {
            string json = Sidecar().Replace(
                $"  \"hullRigSha256\": {{ \"testBoatIsoRig\": \"{HullSha}\" }},\n", "");
            BoatInteriorRead read = Read(json);

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("no hullRigSha256")), string.Join(" | ", read.Errors));
        }

        [Test]
        public void AnInteriorClaimingTwoLoftsIsRefused()
        {
            string json = Sidecar().Replace(
                $"{{ \"testBoatIsoRig\": \"{HullSha}\" }}",
                $"{{ \"testBoatIsoRig\": \"{HullSha}\", \"otherIsoRig\": \"{HullSha}\" }}");
            BoatInteriorRead read = Read(json);

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("names 2 hull rigs")), string.Join(" | ", read.Errors));
        }

        [Test]
        public void AnUnsubstitutedExportTemplateIsRefusedByName()
        {
            // The re-export's real defect: hullRigSha256 carrying the literal generator template. An
            // unstamped stamp is worse than an absent one — it sits in a provenance field looking like
            // a value — so it is named exactly, because the fix is one substitution upstream.
            string json = Sidecar().Replace(
                $"\"testBoatIsoRig\": \"{HullSha}\"",
                "\"testBoatIsoRig\": \"STAMP_AT_EXPORT_LF_SHA256_OF_testBoatIsoRig.js\"");
            BoatInteriorRead read = Read(json);

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("UNSTAMPED hull-rig pin")),
                          string.Join(" | ", read.Errors));
            Assert.IsTrue(read.Errors.Any(e => e.Contains("STAMP_AT_EXPORT")),
                          "the refusal must quote the placeholder, not paraphrase it");
            Assert.IsEmpty(read.Levels);
        }

        [Test]
        public void AnUnstampedRendererPinIsRefusedToo()
        {
            BoatInteriorRead read = Read(Sidecar(interiorSha: "STAMP_AT_EXPORT_LF_SHA256_OF_rig.js"));

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("UNSTAMPED renderer pin")),
                          string.Join(" | ", read.Errors));
        }

        [Test]
        public void OnlySixtyFourHexCharactersCountAsAStamp()
        {
            Assert.IsTrue(BoatInteriorSidecarReader.IsSha256Hex(new string('a', 64)));
            Assert.IsTrue(BoatInteriorSidecarReader.IsSha256Hex(new string('F', 64)), "case-insensitive");
            Assert.IsFalse(BoatInteriorSidecarReader.IsSha256Hex(new string('a', 63)), "too short");
            Assert.IsFalse(BoatInteriorSidecarReader.IsSha256Hex(new string('a', 65)), "too long");
            Assert.IsFalse(BoatInteriorSidecarReader.IsSha256Hex(new string('g', 64)), "not hex");
            Assert.IsFalse(BoatInteriorSidecarReader.IsSha256Hex(null));
            Assert.IsFalse(BoatInteriorSidecarReader.IsSha256Hex(""));
        }

        // ---- refusal 3: unknown sections ---------------------------------------------------------------

        [Test]
        public void ASectionThisReaderWouldDropStopsTheHull()
        {
            // The failure this guards: a drop ships a section nobody taught the importer about, the
            // importer skips it silently, and the feature is simply missing with nothing to grep for.
            BoatInteriorRead read = Read(Sidecar(extra: ",\n  \"PORTLIGHTS\": [ { \"id\": \"p1\" } ]"));

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("PORTLIGHTS")), string.Join(" | ", read.Errors));
        }

        [Test]
        public void UnderscoreAnnotationsAreNeverUnknownSections()
        {
            // `_stem_note` is already in the baseline sidecar; add two more of the contract's own
            // annotation shapes and the read must still be clean.
            BoatInteriorRead read = Read(Sidecar(extra: ",\n  \"_excluded\": { \"engine_room\": \"abstracted\" }," +
                                                       "\n  \"_confirm\": \"a judgment not yet ruled\""));
            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));
        }

        // ---- refusal 4: schema and the two pixel grids --------------------------------------------------

        [Test]
        public void AnUnknownSchemaIsRefusedRatherThanParsedHopefully()
        {
            string json = Sidecar().Replace("boat-interior@1", "boat-interior@2");
            BoatInteriorRead read = Read(json);

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("schema")), string.Join(" | ", read.Errors));
        }

        [Test]
        public void TheTankersSixteenPixelGridIsReadNotAssumed()
        {
            // The kit holds TWO pixel grids. Reading 16 as 16 is the whole test.
            BoatInteriorRead read = BoatInteriorSidecarReader.Read(
                Sidecar(pxPerM: 16), "tanker.interior.json", Lf(InteriorRig), Lf(HullRig));

            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));
            Assert.AreEqual(16, read.PixelsPerMetre);
        }

        [Test]
        public void AnAbsentPixelsPerMetreIsRefusedAndNeverDefaultedToThirtyTwo()
        {
            string json = Sidecar().Replace("\"scale_px_per_m\": 32", "\"note\": \"oops\"");
            BoatInteriorRead read = Read(json);

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("scale_px_per_m")), string.Join(" | ", read.Errors));
        }

        // ---- structural refusals -------------------------------------------------------------------------

        [Test]
        public void AnInteriorWithNowhereToStandIsRefused()
        {
            string json = Sidecar().Replace("\"WALKABLE\"", "\"_WALKABLE_disabled\"");
            BoatInteriorRead read = Read(json);

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("no WALKABLE")), string.Join(" | ", read.Errors));
        }

        [Test]
        public void AnInteriorWithNoWayInIsRefused()
        {
            string json = Sidecar().Replace("\"THRESHOLD\"", "\"_THRESHOLD_disabled\"");
            BoatInteriorRead read = Read(json);

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("no THRESHOLD")), string.Join(" | ", read.Errors));
        }

        [Test]
        public void ADoorWhoseMechanismIsNeitherSlidingNorHingedIsRefused()
        {
            string json = Sidecar().Replace("\"mechanism\": \"sliding\"", "\"mechanism\": \"revolving\"");
            BoatInteriorRead read = Read(json);

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("revolving")), string.Join(" | ", read.Errors));
        }

        [Test]
        public void DuplicateAnchorIdsAreRefusedBecauseIdsAreAppendOnly()
        {
            string json = Sidecar().Replace(
                "\"INTERACT\": [ { \"id\": \"helm\"",
                "\"INTERACT\": [ { \"id\": \"helm\", \"action\": \"a\", \"reach_point\": [0,0,0] },\n" +
                "    { \"id\": \"helm\"");
            BoatInteriorRead read = Read(json);

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("appears twice")), string.Join(" | ", read.Errors));
        }

        [Test]
        public void UnreadableJsonIsALoudFailureNotAPartialRead()
        {
            BoatInteriorRead read = Read("{ \"schema\": ");

            Assert.IsFalse(read.Ok);
            Assert.IsTrue(read.Errors.Any(e => e.Contains("unreadable JSON")), string.Join(" | ", read.Errors));
            Assert.IsEmpty(read.Levels);
        }

        // ---- routes may leave the interior ---------------------------------------------------------------

        [Test]
        public void ARouteEndingOnAnExteriorDeckIsANoteNotAnError()
        {
            // The 53's companionway lands on `bridge_sole`, which belongs to the HULL's gameplay
            // sidecar, not to the interior. Refusing that would refuse the very thing the kit built to
            // close the extractor's "no route up" finding.
            string json = Sidecar().Replace("\"to\": \"cuddy_sole\"", "\"to\": \"bridge_sole\"");
            BoatInteriorRead read = Read(json);

            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));
            Assert.IsTrue(read.Notes.Any(n => n.Contains("bridge_sole")), string.Join(" | ", read.Notes));
        }

        [Test]
        public void LadderLegsBecomeRoutesWithTheirRiseMeasured()
        {
            string json = Sidecar(extra:
                ",\n  \"LADDER\": [ { \"id\": \"flybridge_ladder\", \"kind\": \"vertical_ladder\",\n" +
                "    \"exterior\": true, \"z0\": 1.78, \"z1\": 4.86 } ]");
            BoatInteriorRead read = Read(json);

            Assert.IsTrue(read.Ok, string.Join(" | ", read.Errors));
            BoatInteriorRoute ladder = read.Routes.Single(r => r.Id == "flybridge_ladder");
            Assert.IsTrue(ladder.Exterior);
            Assert.AreEqual(3.08f, ladder.TotalRiseMeters, 1e-3f);
        }
    }
}
