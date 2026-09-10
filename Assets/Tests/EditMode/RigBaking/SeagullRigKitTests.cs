using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE SEAGULL RIG KIT (owner drop 2026-09-10) — the intake guards.</b>
    ///
    /// <para>One rig, one sidecar, and the repo's first <c>creature-gameplay@1</c>. Every number
    /// pinned below was measured against the committed bytes before it was written down; what is here
    /// is the subset that must keep being true, not the transcript.</para>
    ///
    /// <para>The failure this file exists to prevent is the fleet's recurring one: a sidecar cut from
    /// a bird the rig no longer draws, discovered in play. The second failure it guards is newer —
    /// the kit README and the intake charter both say <b>27</b> transitions and the rig ships
    /// <b>26</b>. A prose file is not the authority; the rig is. See
    /// <see cref="TheGraphIsExactlyTheTwentySixEdgesTheRigDeclares"/>.</para>
    /// </summary>
    public class SeagullRigKitTests
    {
        const string RigPath = "docs/art/rigs/seagullIsoRig.js";
        const string SidecarPath = "docs/art/rigs/gameplay/seagullIsoRig.gameplay.json";

        /// <summary>A boat sidecar, used to prove the reader refuses a schema it does not read.</summary>
        const string BoatSidecarPath = "docs/art/rigs/gameplay/doryIsoRig.gameplay.json";

        /// <summary>
        /// From the drop's own <c>SHA256SUMS.txt</c> (<c>C:/hh-drops/2026-09-10-seagull-rig-kit</c>),
        /// which is NOT committed — the charter landed two files, not three, and the manifest's third
        /// line names a sheet this PR holds. Transcribed here so the drop's claim about the bytes and
        /// this repo's copy of the bytes can still be compared without the supplier's file.
        /// </summary>
        const string DropRigSha = "3cbfa37bf5424e144068cc1d841b562774f135dff81ffca289d20a29ee48da9e";

        const string DropSidecarSha = "6cc9ed53bfd9186c00bf44d57dcd298245515abcdbedb76ac18e0dcddb04cd46";

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static byte[] RigBytes() => File.ReadAllBytes(Full(RigPath));
        static string SidecarText() => File.ReadAllText(Full(SidecarPath));

        static SeagullSidecarRead ReadKit() =>
            SeagullSidecarReader.Read(SidecarText(), SidecarPath, RigBytes());

        static string Sha256(byte[] b)
        {
            using var sha = SHA256.Create();
            var sb = new StringBuilder(64);
            foreach (byte x in sha.ComputeHash(b)) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        static void AssertClean(SeagullSidecarRead read)
            => Assert.That(read.Errors, Is.Empty, "the committed kit must read without errors.");

        // =========================================================================================
        //  1. THE DROP LANDED, BYTE FOR BYTE
        // =========================================================================================

        [Test]
        public void BothFilesLandedWithTheBytesTheDropShipped()
        {
            FileAssert.Exists(Full(RigPath));
            FileAssert.Exists(Full(SidecarPath));

            Assert.That(Sha256(RigBytes()), Is.EqualTo(DropRigSha),
                $"{RigPath} is not the file the art director shipped. The rig is the authority for " +
                "every number in this suite; a rig that has moved invalidates all of them.");
            Assert.That(Sha256(File.ReadAllBytes(Full(SidecarPath))), Is.EqualTo(DropSidecarSha),
                $"{SidecarPath} is not the file the art director shipped. It is GENERATED — never " +
                "hand-edit it; re-run SeagullIso.gameplayGeometry() upstream and land a new drop.");
        }

        /// <summary>
        /// The drop measured 0 CR in either file and <c>.gitattributes</c> pins both to
        /// <c>eol=lf</c>, because <c>derivedFromRigSha256</c> hashes the WORKING-TREE bytes: a
        /// checkout that normalised the rig to CRLF would break the pin on line endings alone,
        /// with the bird unchanged.
        /// </summary>
        [Test]
        public void NeitherFileCarriesACarriageReturn()
        {
            Assert.That(RigBytes().Count(b => b == (byte)'\r'), Is.Zero,
                $"{RigPath} has CRLF endings — check the .gitattributes eol=lf pin for this kit.");
            Assert.That(File.ReadAllBytes(Full(SidecarPath)).Count(b => b == (byte)'\r'), Is.Zero,
                $"{SidecarPath} has CRLF endings — check the .gitattributes eol=lf pin for this kit.");
        }

        // =========================================================================================
        //  2. THE PIN
        // =========================================================================================

        [Test]
        public void TheSidecarPinsTheRigThatIsOnDisk()
        {
            var read = ReadKit();
            AssertClean(read);
            Assert.That(read.HashMatch, Is.EqualTo(RigHashMatch.Exact),
                $"the sidecar was derived from {read.Gameplay.DerivedFromRigSha256} but the committed " +
                $"rig hashes to {read.ActualRigSha}.");
            Assert.That(read.Gameplay.DerivedFromRigSha256, Is.EqualTo(DropRigSha));
            Assert.That(read.Gameplay.Schema,
                Is.EqualTo(SeagullSidecarReader.Schema));
            Assert.That(read.Gameplay.ExportSymbol, Is.EqualTo("SeagullIso"));
        }

        [Test]
        public void ARefusedReadDoesNotHalfPopulate()
        {
            // One character of the pin moved: the same file, claiming a rig that is not on disk.
            string tampered = SidecarText().Replace(DropRigSha,
                "0000000000000000000000000000000000000000000000000000000000000000");
            Assert.That(tampered, Is.Not.EqualTo(SidecarText()), "the tamper must actually apply.");

            var read = SeagullSidecarReader.Read(tampered, SidecarPath, RigBytes());

            Assert.That(read.Ok, Is.False);
            Assert.That(read.HashMatch, Is.EqualTo(RigHashMatch.None));
            Assert.That(string.Join(" | ", read.Errors), Does.Contain("STALE"));
            Assert.That(read.Gameplay.States, Is.Empty, "a refused read must not half-populate.");
            Assert.That(read.Gameplay.Transitions, Is.Empty, "a refused read must not half-populate.");
            Assert.That(read.Gameplay.HasFlock, Is.False, "a refused read must not half-populate.");
        }

        [Test]
        public void ABoatSidecarIsRefusedRatherThanReadEmpty()
        {
            string boat = File.ReadAllText(Full(BoatSidecarPath));
            var read = SeagullSidecarReader.Read(boat, BoatSidecarPath, RigBytes(), enforceHash: false);

            Assert.That(read.Ok, Is.False);
            Assert.That(string.Join(" | ", read.Errors), Does.Contain("WRONG SCHEMA"));
            Assert.That(read.Gameplay.States, Is.Empty);
            Assert.That(read.Gameplay.Transitions, Is.Empty);
        }

        [Test]
        public void UnreadableJsonIsRefusedLoudlyRatherThanThrowing()
        {
            var read = SeagullSidecarReader.Read("{ \"schema\": ", SidecarPath, RigBytes());
            Assert.That(read.Ok, Is.False);
            Assert.That(string.Join(" | ", read.Errors), Does.Contain("unreadable JSON"));
        }

        // =========================================================================================
        //  3. THE FRAME — what the slicer will cut, and the owner's strict world scale
        // =========================================================================================

        [Test]
        public void TheFrameIsTheCellTheSlicerWillCut()
        {
            var g = ReadKit().Gameplay;
            Assert.That(g.CellWidth, Is.EqualTo(64));
            Assert.That(g.CellHeight, Is.EqualTo(64));
            Assert.That(g.PivotX, Is.EqualTo(32));
            Assert.That(g.PivotY, Is.EqualTo(46));
            Assert.That(g.Directions, Is.EqualTo(8));
            Assert.That(g.PixelsPerMetre, Is.EqualTo(32f));
        }

        /// <summary>
        /// The owner's ruling of 2026-09-09: 32 px = 1 m, a 1.40 m span is 45 px, AT EVERY ALTITUDE.
        /// Altitude is a screen OFFSET, never a sprite scale — there are no giant gulls crossing the
        /// camera. This pins the two numbers that offset arithmetic is built from.
        /// </summary>
        [Test]
        public void TheBirdIsAtStrictWorldScale()
        {
            var g = ReadKit().Gameplay;
            Assert.That(g.WingspanMetres, Is.EqualTo(1.4f).Within(1e-4f));
            Assert.That(g.LengthMetres, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That((int)Math.Round(g.WingspanMetres * g.PixelsPerMetre), Is.EqualTo(45),
                "1.40 m at 32 px/m is a 45 px span; the kit README states the same. If this moves, " +
                "every altitude offset moves with it.");
            Assert.That(g.BodyCentreZStand, Is.EqualTo(0.19f).Within(1e-4f));
            Assert.That(g.BodyCentreZPerch, Is.EqualTo(0.165f).Within(1e-4f));
            Assert.That(g.BodyCentreZFloat, Is.EqualTo(0.035f).Within(1e-4f));
        }

        // =========================================================================================
        //  4. THE STATES — the kit README's table, pinned frame for frame
        // =========================================================================================

        /// <summary>state, frames, ms, v, vz, loop, next — the README's table verbatim.</summary>
        static readonly object[] ReadmeStateTable =
        {
            new object[] { "fly",     6,  90,  9.0f,  0.0f, true,  "" },
            new object[] { "glide",   2, 320,  8.0f, -0.4f, true,  "" },
            new object[] { "swoop",   4, 110, 12.0f, -3.0f, true,  "" },
            new object[] { "dive",    4,  85,  6.0f, -9.0f, false, "splash" },
            new object[] { "splash",  5, 110,  1.2f,  0.0f, false, "float" },
            new object[] { "float",   2, 420,  0.15f, 0.0f, true,  "" },
            new object[] { "preen",   4, 230,  0.0f,  0.0f, true,  "" },
            new object[] { "peck",    3, 140,  0.0f,  0.0f, true,  "" },
            new object[] { "land",    4, 110,  2.0f, -1.5f, false, "stand" },
            new object[] { "stand",   2, 520,  0.0f,  0.0f, true,  "" },
            new object[] { "walk",    6, 130,  0.45f, 0.0f, true,  "" },
            new object[] { "perch",   2, 560,  0.0f,  0.0f, true,  "" },
            new object[] { "takeoff", 4, 100,  2.5f,  2.2f, false, "fly" },
        };

        [TestCaseSource(nameof(ReadmeStateTable))]
        public void EveryStateInTheKitTableIsPresentWithItsFramesAndTiming(
            string id, int frames, int ms, float v, float vz, bool loop, string next)
        {
            var s = ReadKit().Gameplay.State(id);
            Assert.That(s, Is.Not.Null, $"STATES.{id} is missing.");
            Assert.That(s.Frames, Is.EqualTo(frames), $"{id}.frames");
            Assert.That(s.Milliseconds, Is.EqualTo(ms), $"{id}.ms");
            Assert.That(s.SpeedMetresPerSecond, Is.EqualTo(v).Within(1e-4f), $"{id}.v");
            Assert.That(s.ClimbMetresPerSecond, Is.EqualTo(vz).Within(1e-4f), $"{id}.vz");
            Assert.That(s.Loop, Is.EqualTo(loop), $"{id}.loop");
            Assert.That(s.Next, Is.EqualTo(next), $"{id}.next");
        }

        [Test]
        public void TheKitHasThirteenStatesAndNoMore()
        {
            var g = ReadKit().Gameplay;
            Assert.That(g.States.Select(s => s.Id).ToArray(),
                Is.EquivalentTo(ReadmeStateTable.Cast<object[]>().Select(r => (string)r[0]).ToArray()),
                "a state added or removed upstream changes the sheet's 48 columns and every consumer " +
                "of sheetOrder(). Re-measure the bake before adopting it.");
        }

        /// <summary>
        /// ⚠️ <b>The <c>alt</c> pair does not mean the same thing in every state.</b> For the loopers
        /// it is a [min, max] BAND; for the one-shots it is START → END — except <c>dive</c>, which
        /// ships [0.3, 8] ASCENDING while the README writes the same state "8→0.3". A consumer that
        /// reads [start, end] uniformly flies the dive UPWARDS out of the sea. <c>vz</c> is the
        /// unambiguous direction: dive is −9 m/s.
        /// </summary>
        [Test]
        public void TheAltitudePairIsNotUniformlyStartThenEnd()
        {
            var g = ReadKit().Gameplay;

            var dive = g.State("dive");
            Assert.That(dive.AltitudeA, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(dive.AltitudeB, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(dive.AltitudeA, Is.LessThan(dive.AltitudeB),
                "dive's pair is ASCENDING in the file.");
            Assert.That(dive.ClimbMetresPerSecond, Is.LessThan(0f),
                "…and its vz is negative. Direction comes from vz, never from the pair's order.");

            var land = g.State("land");
            Assert.That(land.AltitudeA, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(land.AltitudeB, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(land.AltitudeA, Is.GreaterThan(land.AltitudeB),
                "land's pair is DESCENDING — the opposite order to dive's, for the same downward move.");

            var takeoff = g.State("takeoff");
            Assert.That(takeoff.AltitudeA, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(takeoff.AltitudeB, Is.EqualTo(1.2f).Within(1e-4f));

            // The band states: min/max helpers must not care which way round the pair came.
            var fly = g.State("fly");
            Assert.That(fly.AltitudeMinMetres, Is.EqualTo(6f).Within(1e-4f));
            Assert.That(fly.AltitudeMaxMetres, Is.EqualTo(25f).Within(1e-4f));
        }

        // =========================================================================================
        //  5. THE GRAPH — 26 edges, not the 27 the prose claims
        // =========================================================================================

        /// <summary>The rig's own <c>TRANSITIONS</c> literal, transcribed in its source order.</summary>
        static readonly string[] Edges26 =
        {
            "stand>walk", "walk>stand", "stand>takeoff", "walk>takeoff", "perch>takeoff",
            "takeoff>fly", "fly>glide", "glide>fly", "fly>swoop", "glide>swoop", "swoop>fly",
            "swoop>dive", "swoop>land", "swoop>splash", "glide>land", "glide>splash", "glide>dive",
            "dive>splash", "splash>float", "float>preen", "preen>float", "float>peck", "peck>float",
            "float>takeoff", "land>stand", "land>perch",
        };

        /// <summary>
        /// <b>The kit README says "TRANSITIONS — 27 edges" and the intake charter repeated it. The rig
        /// ships 26.</b> Counted in the rig's own literal (<c>seagullIsoRig.js:406</c>: 5 + 9 + 5 + 5
        /// + 2 pairs across five source lines, the trailing comma after <c>['land','perch'],</c>
        /// notwithstanding — JS does not create an element from it) and in the generated sidecar.
        ///
        /// <para>Same shape as the shovel rig's lying azimuth header: prose in a drop is an
        /// EXPECTATION, the rig is the authority. This is pinned at 26 and reported upstream, not
        /// "fixed" — nobody here edits the rig.</para>
        ///
        /// <para>If this goes red at 27, upstream has ADDED the missing edge (see
        /// <see cref="ThereIsNoDryPreenInTheDeclaredGraph"/> for the most likely identity). That is a
        /// behaviour change for PR 2's state machine, not a test to relax.</para>
        /// </summary>
        [Test]
        public void TheGraphIsExactlyTheTwentySixEdgesTheRigDeclares()
        {
            var g = ReadKit().Gameplay;
            string[] actual = g.Transitions.Select(t => $"{t.From}>{t.To}").ToArray();

            Assert.That(actual.Length, Is.EqualTo(26),
                "the kit README and the intake charter both say 27 edges; the rig's TRANSITIONS " +
                $"literal and the generated sidecar both hold 26. Got {actual.Length}.");
            Assert.That(actual, Is.EquivalentTo(Edges26));
            Assert.That(actual.Distinct().Count(), Is.EqualTo(actual.Length),
                "a duplicated edge would inflate the count without adding a move.");
        }

        [Test]
        public void EveryOneshotEndsSomewhereTheGraphAllows()
        {
            var g = ReadKit().Gameplay;
            foreach (var s in g.States.Where(x => !string.IsNullOrEmpty(x.Next)))
                Assert.That(g.HasTransition(s.Id, s.Next), Is.True,
                    $"{s.Id}.next = '{s.Next}' with no {s.Id}->{s.Next} edge: the clip would end " +
                    "somewhere the graph forbids.");

            // The four chains the README names, spelled out so a silent re-wiring is loud.
            Assert.That(g.HasTransition("dive", "splash"));
            Assert.That(g.HasTransition("splash", "float"));
            Assert.That(g.HasTransition("land", "stand"));
            Assert.That(g.HasTransition("takeoff", "fly"));
        }

        [Test]
        public void EveryStateIsReachableAndNoStateIsADeadEnd()
        {
            var g = ReadKit().Gameplay;

            var seen = new HashSet<string>(StringComparer.Ordinal) { "fly" };
            var queue = new Queue<string>(seen);
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                foreach (var t in g.Transitions.Where(t => t.From == at))
                    if (seen.Add(t.To)) queue.Enqueue(t.To);
            }

            Assert.That(seen.Count, Is.EqualTo(g.States.Count),
                "unreachable from fly: " +
                string.Join(", ", g.States.Select(s => s.Id).Where(id => !seen.Contains(id))));

            foreach (var s in g.States)
                Assert.That(g.Transitions.Any(t => t.From == s.Id), Is.True,
                    $"'{s.Id}' has no out-edge — a bird that enters it can never leave.");
        }

        /// <summary>
        /// <b>A finding for the seat, pinned so it cannot be forgotten.</b> The owner asked for a gull
        /// that "can clean itself", and the kit README says explicitly to pass <c>waterZ:null</c> to
        /// render a water state dry. But in the DECLARED graph <c>preen</c> and <c>peck</c> have
        /// <c>float</c> as their only in-edge: the bird can only preen while swimming. A dry preen on
        /// a wharf has no edge to reach it.
        ///
        /// <para>That is the most plausible identity of the prose's missing 27th edge (a
        /// <c>stand-&gt;preen</c>), but it is NOT invented here — it goes upstream as an observation.
        /// When this test goes red, the edge has landed and PR 2's state machine should adopt it.</para>
        /// </summary>
        [Test]
        public void ThereIsNoDryPreenInTheDeclaredGraph()
        {
            var g = ReadKit().Gameplay;
            foreach (string grooming in new[] { "preen", "peck" })
                Assert.That(g.Transitions.Where(t => t.To == grooming).Select(t => t.From).ToArray(),
                    Is.EquivalentTo(new[] { "float" }),
                    $"'{grooming}' has gained a dry in-edge. Good — that is the gap reported upstream " +
                    "at intake; adopt it in the state machine and update this test.");
        }

        // =========================================================================================
        //  6. THE BEHAVIOUR SECTIONS — and the contract that absence is data
        // =========================================================================================

        [Test]
        public void TheLandingBoxIsTheOwnersClearance()
        {
            var g = ReadKit().Gameplay;
            Assert.That(g.HasLand, Is.True);
            Assert.That(g.LandNeedsClearX, Is.EqualTo(1.4f).Within(1e-4f));
            Assert.That(g.LandNeedsClearY, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(g.LandMinFlatMetres, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(g.LandApproachIntoWind, Is.True);
        }

        [Test]
        public void ThePerchContractCarriesItsSeventeenCandidates()
        {
            var g = ReadKit().Gameplay;
            Assert.That(g.HasPerch, Is.True);
            Assert.That(g.PerchCandidates.Count, Is.EqualTo(17));
            Assert.That(g.PerchCandidates, Does.Contain("rail").And.Contain("trap_stack")
                                               .And.Contain("dinghy_gunwale"));
            Assert.That(g.PerchMinWidthMetres, Is.EqualTo(0.03f).Within(1e-4f));
            Assert.That(g.PerchMaxWidthForGripMetres, Is.EqualTo(0.12f).Within(1e-4f));
            Assert.That(g.PerchFlatOkMinMetres, Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(g.PerchMaxSlopeDegrees, Is.EqualTo(35f).Within(1e-4f));
            Assert.That(g.PerchClearanceAboveMetres, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(g.PerchExit, Is.EqualTo("takeoff"));

            // The feet straddle the pivot by one pixel each way at dir 0 — the grip on an edge.
            Assert.That(g.PerchFootLeft, Is.Not.Null);
            Assert.That(g.PerchFootRight, Is.Not.Null);
            Assert.That(g.PerchFootLeft.Dx, Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(g.PerchFootRight.Dx, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void TheWaterSectionNamesTheFrameTheSplashFiresOn()
        {
            var g = ReadKit().Gameplay;
            Assert.That(g.HasWater, Is.True);
            Assert.That(g.FloatBodyZMetres, Is.EqualTo(0.035f).Within(1e-4f));
            Assert.That(g.DriftWithCurrent, Is.True);
            Assert.That(g.SplashRig, Is.EqualTo("splashRig.js"));

            Assert.That(g.SplashBurstFrame, Is.EqualTo(2),
                "PR 3's GullSplashed signal hangs off this frame.");
            Assert.That(g.SplashBurstFrame, Is.LessThan(g.State("splash").Frames),
                "a burst frame past the clip's last frame never fires.");

            Assert.That(g.HasDiveYield, Is.True);
            Assert.That(g.DiveCatchProbability, Is.EqualTo(0.35f).Within(1e-4f));
            Assert.That(g.DiveCatchSpecies, Is.EquivalentTo(new[] { "herring", "mackerel" }));
        }

        [Test]
        public void TheFlockCarriesItsSixAttractorsWithTheirWeights()
        {
            var g = ReadKit().Gameplay;
            Assert.That(g.HasFlock, Is.True);

            var byId = g.Attractors.ToDictionary(a => a.Id, a => a.Weight, StringComparer.Ordinal);
            Assert.That(byId.Count, Is.EqualTo(6));
            Assert.That(byId["gutting"], Is.EqualTo(1.0f).Within(1e-4f));
            Assert.That(byId["chum"], Is.EqualTo(1.0f).Within(1e-4f));
            Assert.That(byId["open_tub"], Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(byId["trawler_wake"], Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(byId["bait_bucket"], Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(byId["shoal_surface"], Is.EqualTo(0.5f).Within(1e-4f));

            Assert.That(g.FlockSizeMin, Is.EqualTo(3));
            Assert.That(g.FlockSizeMax, Is.EqualTo(9));
            Assert.That(g.FlockAttractRadiusMetres, Is.EqualTo(40f).Within(1e-4f));
            Assert.That(g.FlockFleeRadiusMetres, Is.EqualTo(2.5f).Within(1e-4f));
            Assert.That(g.FlockFleeReaction, Is.EqualTo("takeoff"));
            Assert.That(g.FlockSettleAfterSeconds, Is.EqualTo(20f).Within(1e-4f));

            // Read, recorded, NOT built: stealing from a tub is not in any PR of this arc.
            Assert.That(g.HasSteal, Is.True);
            Assert.That(g.StealFrom, Is.EqualTo("open_tub"));
            Assert.That(g.StealPerBirdSeconds, Is.EqualTo(12f).Within(1e-4f));
        }

        [Test]
        public void TheDirZeroAnchorsAreOnTheBirdThePivotSitsUnder()
        {
            var g = ReadKit().Gameplay;
            Assert.That(g.HasAnchors, Is.True);
            Assert.That(g.AnchorsPx.Keys, Is.EquivalentTo(new[] { "stand", "float", "perch" }),
                "ANCHORS_PX records the three CONTACT states only; anchors(dir,{anim,frame}) covers " +
                "the rest. The 'note' member is prose and must not read as a fourth state.");

            Assert.That(g.Anchor("stand", "footL"), Is.Not.Null);
            Assert.That(g.Anchor("stand", "footR"), Is.Not.Null);

            // A floating bird has no feet above the surface — the section says so by OMITTING them,
            // which is only distinguishable from (0,0,0) because Anchor() returns null.
            Assert.That(g.Anchor("float", "footL"), Is.Null,
                "a floating gull's feet are under the water and are not recorded; absent is not zero.");
            Assert.That(g.Anchor("float", "bill"), Is.Not.Null);
        }

        /// <summary>
        /// The sidecar's own <c>extractor_contract</c>: "absent section = the creature does not
        /// support the feature (not an error)". A creature with no FLOCK must read cleanly with
        /// <c>HasFlock</c> false and a note — never as a flock of size zero, which is the same shape
        /// as a bug.
        /// </summary>
        [Test]
        public void AnAbsentSectionIsDistinguishableFromAZeroOne()
        {
            string stripped = SidecarText().Replace("\"FLOCK\"", "\"FLOCK_REMOVED_BY_TEST\"");
            Assert.That(stripped, Is.Not.EqualTo(SidecarText()), "the strip must actually apply.");

            var read = SeagullSidecarReader.Read(stripped, SidecarPath, RigBytes());

            Assert.That(read.Ok, Is.True, "an absent optional section is not an error.");
            Assert.That(read.Gameplay.HasFlock, Is.False);
            Assert.That(read.Gameplay.Attractors, Is.Empty);
            Assert.That(string.Join(" | ", read.Notes), Does.Contain("FLOCK absent"));

            // …and the states are still all there: one missing behaviour does not cost the bird its
            // animation table.
            Assert.That(read.Gameplay.States.Count, Is.EqualTo(13));
        }
    }
}
