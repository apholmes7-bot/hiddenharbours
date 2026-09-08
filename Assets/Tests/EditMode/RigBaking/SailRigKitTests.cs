using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE SAIL RIG KIT (owner drop 2026-09-06) — the intake guards.</b>
    ///
    /// <para>Two sloops, four sidecars, a new schema and the repo's first rigs that do not sit at the
    /// top of <c>docs/art/rigs/</c>. Every claim below was MEASURED first in a standalone ClearScript
    /// V8 harness against the committed bytes; what is pinned here is the subset that must keep being
    /// true, not the transcript. The measurements themselves live in
    /// <c>docs/art/spikes/sail-rig-kit/probe-record.md</c>.</para>
    ///
    /// <para>The failure this file exists to prevent is the one the fleet has shipped repeatedly: a
    /// sidecar whose numbers were cut from a hull the rig no longer draws, discovered in play.</para>
    /// </summary>
    public class SailRigKitTests
    {
        const string KitFolder = "docs/art/rigs/sail-rig-kit";
        const string GameplayFolder = "docs/art/rigs/gameplay/sail";

        const string Rig30 = KitFolder + "/sloop-30/sloopIsoRig.js";
        const string Rig88 = KitFolder + "/sloop-88/sloop88IsoRig.js";

        /// <summary>
        /// The kit's own <c>SHA256SUMS.txt</c> is KIT-RELATIVE: it names each hull's sidecars under
        /// <c>sloop-30/</c> and <c>sloop-88/</c>, where this repo lands them in
        /// <c>docs/art/rigs/gameplay/sail/</c> (a subfolder while the mesh bake is blocked — see that
        /// folder's README). The manifest is kept exactly as the art director shipped it — rewriting a
        /// supplier's checksum file to suit our folders is how a checksum stops being evidence — so
        /// the mapping lives here instead, and this is the ONE place the path is written down.
        /// </summary>
        static readonly IReadOnlyDictionary<string, string> ManifestToRepoPath =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sloop-30/sloopIsoRig.js"] = Rig30,
                ["sloop-30/sloopIsoRig.gameplay.json"] = GameplayFolder + "/sloopIsoRig.gameplay.json",
                ["sloop-30/sloopIsoRig.sailing.json"] = GameplayFolder + "/sloopIsoRig.sailing.json",
                ["sloop-88/sloop88IsoRig.js"] = Rig88,
                ["sloop-88/sloop88IsoRig.gameplay.json"] = GameplayFolder + "/sloop88IsoRig.gameplay.json",
                ["sloop-88/sloop88IsoRig.sailing.json"] = GameplayFolder + "/sloop88IsoRig.sailing.json",
            };

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static string Sha256(byte[] b)
        {
            using var sha = SHA256.Create();
            var sb = new StringBuilder(64);
            foreach (byte x in sha.ComputeHash(b)) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        // =========================================================================================
        //  1. THE KIT LANDED, AND ITS OWN MANIFEST STILL DESCRIBES IT
        // =========================================================================================

        [Test]
        public void EveryFileTheKitManifestNamesIsOnDisk()
        {
            FileAssert.Exists(Full(KitFolder + "/SHA256SUMS.txt"));
            foreach (string path in ManifestToRepoPath.Values) FileAssert.Exists(Full(path));
        }

        /// <summary>
        /// ⭐ <b>All six hashes, over the FULL 64 characters.</b> A prefix check is not a hash check:
        /// this repo has already met a corrupt stamp that shared SIXTEEN leading hex digits with the
        /// truth and diverged over the remaining forty-eight, and the drop that carried it reached a
        /// lane as "all six verified" because the staging pass compared eight characters.
        ///
        /// <para><b>⚠️ EXACT, not line-ending-normalised.</b> The stamps are over the rig bytes as
        /// written, which are LF, and <c>.gitattributes</c> pins this kit to LF for exactly this
        /// reason. Accepting the normalised form here would make the pin decorative and let a CRLF
        /// checkout drift in unnoticed — which is what the pin is preventing.</para>
        /// </summary>
        [Test]
        public void EveryHashInTheKitManifestMatchesTheFileItNames_Exactly()
        {
            var wrong = new List<string>();
            foreach (string line in File.ReadAllLines(Full(KitFolder + "/SHA256SUMS.txt")))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;

                int split = trimmed.IndexOf(' ');
                Assert.Greater(split, 0, $"unparseable manifest line: '{trimmed}'");
                string expected = trimmed.Substring(0, split).Trim();
                string name = trimmed.Substring(split).Trim();

                Assert.That(ManifestToRepoPath.ContainsKey(name), Is.True,
                    $"SHA256SUMS.txt names '{name}', which this map does not place in the repo. A new " +
                    "file in the kit needs a row in ManifestToRepoPath — an unmapped entry is an " +
                    "unchecked file.");

                string actual = Sha256(File.ReadAllBytes(Full(ManifestToRepoPath[name])));
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    wrong.Add($"{name} → {ManifestToRepoPath[name]}: expected {expected}, got {actual}");
            }

            Assert.IsEmpty(wrong,
                "the kit's own checksum file no longer describes the committed bytes:\n" +
                string.Join("\n", wrong) +
                "\nIf a rig was reshaped, regenerate the whole kit with SAIL_KIT.write() — never patch " +
                "a number (the kit README's own rule). If only the line endings moved, the " +
                "docs/art/rigs/sail-rig-kit/** LF pin in .gitattributes has been lost.");
        }

        /// <summary>
        /// ⭐ <b>BOTH sidecars of each hull stamp that hull's rig.</b> The kit's contract is that the
        /// geometry file and the sailing file are generated from ONE read of the rig, so they cannot
        /// come apart — this is the assertion that keeps that true after any later hand-edit.
        /// </summary>
        [Test]
        public void BothSidecarsOfEachSloopPinTheirOwnRig()
        {
            foreach ((string rig, string[] sidecars) in new[]
            {
                (Rig30, new[] { GameplayFolder + "/sloopIsoRig.gameplay.json",
                                GameplayFolder + "/sloopIsoRig.sailing.json" }),
                (Rig88, new[] { GameplayFolder + "/sloop88IsoRig.gameplay.json",
                                GameplayFolder + "/sloop88IsoRig.sailing.json" }),
            })
            {
                string rigSha = Sha256(File.ReadAllBytes(Full(rig)));
                foreach (string sidecar in sidecars)
                {
                    string stamped = ReadJsonString(File.ReadAllText(Full(sidecar)), "derivedFromRigSha256");
                    Assert.That(stamped, Is.Not.Empty,
                        $"{sidecar} carries no derivedFromRigSha256. An unstamped sidecar is the defect " +
                        "— it is refused exactly like a stale one.");
                    Assert.That(stamped, Is.EqualTo(rigSha).IgnoreCase,
                        $"{sidecar} pins {stamped} but {rig} hashes to {rigSha}. Compare the whole " +
                        "digest, never a prefix.");
                }
            }
        }

        // =========================================================================================
        //  2. THE NEW SCHEMA, AND THE TWO THINGS ITS READER MUST REFUSE
        // =========================================================================================

        [Test]
        public void TheSailingSidecarReaderReadsBothSloops()
        {
            foreach ((string rig, string sailing) in new[]
            {
                (Rig30, GameplayFolder + "/sloopIsoRig.sailing.json"),
                (Rig88, GameplayFolder + "/sloop88IsoRig.sailing.json"),
            })
            {
                SailingSidecarRead read = SailingSidecarReader.Read(
                    File.ReadAllText(Full(sailing)), sailing, File.ReadAllBytes(Full(rig)));

                Assert.IsEmpty(read.Errors, $"{sailing}: {string.Join(" | ", read.Errors)}");
                Assert.That(read.HashMatch, Is.EqualTo(RigHashMatch.Exact),
                    $"{sailing} should pin its rig EXACTLY — the kit is LF-pinned in .gitattributes.");
                Assert.That(read.Schema, Is.EqualTo(SailingSidecarReader.Schema));

                Assert.Greater(read.LwlMeters, 0f, "LWL is the model's own input; zero means unread.");
                Assert.Greater(read.HullSpeedKn, 0f);
                Assert.Greater(read.UpwindSailAreaM2, 0f);
                Assert.That(read.WaterlineHalfBreadths.Count, Is.GreaterThan(4),
                    "the half-breadths are the honest waterplane input — displacement_kg is CANOE " +
                    "BODY ONLY and must not be used in its place.");
                Assert.That(read.PolarRows.Count,
                            Is.EqualTo(read.PolarTrueWindKn.Count * read.PolarTrueWindAngleDeg.Count),
                    "a partial polar grid indexed row-major reads the wrong cell.");
                Assert.That(read.States.Select(s => s.Id), Contains.Item("stored"),
                    "the STORED state is what a moored sloop is drawn in.");
                Assert.That(read.FromTrue.ContainsKey("aws_kn"), Is.True,
                    "WIND.from_true is the glue a drive must reproduce; it is carried verbatim.");
            }
        }

        /// <summary>
        /// ⭐ <b>A geometry sidecar read as a sailing one must ERROR, not come back empty.</b> The two
        /// schemas share a folder and a naming convention, and every section this reader wants is
        /// absent from the geometry file — so "best effort" would return a fully-populated-looking
        /// object with a zero hull, a zero polar and no complaint at all.
        /// </summary>
        [Test]
        public void TheSailingSidecarReaderRefusesAGeometrySidecar()
        {
            SailingSidecarRead read = SailingSidecarReader.Read(
                File.ReadAllText(Full(GameplayFolder + "/sloopIsoRig.gameplay.json")),
                GameplayFolder + "/sloopIsoRig.gameplay.json",
                File.ReadAllBytes(Full(Rig30)));

            Assert.IsNotEmpty(read.Errors);
            Assert.That(string.Join(" ", read.Errors), Does.Contain("WRONG SCHEMA"));
            Assert.That(read.PolarRows, Is.Empty, "a refused read must not half-populate.");
        }

        /// <summary>
        /// ⭐ <b>A reshaped hull is refused.</b> One byte of the rig moved is enough — which is the
        /// point: the digest cannot tell a moved vertex from a moved comment, so it refuses both and
        /// the art side re-derives. A near-miss is not a pass.
        /// </summary>
        [Test]
        public void TheSailingSidecarReaderRefusesAStaleStamp()
        {
            byte[] rig = File.ReadAllBytes(Full(Rig30));
            var reshaped = new byte[rig.Length + 1];
            Array.Copy(rig, reshaped, rig.Length);
            reshaped[rig.Length] = (byte)'\n';

            SailingSidecarRead read = SailingSidecarReader.Read(
                File.ReadAllText(Full(GameplayFolder + "/sloopIsoRig.sailing.json")),
                GameplayFolder + "/sloopIsoRig.sailing.json", reshaped);

            Assert.That(read.HashMatch, Is.EqualTo(RigHashMatch.None));
            Assert.That(string.Join(" ", read.Errors), Does.Contain("STALE"));
            Assert.That(read.PolarRows, Is.Empty);
        }

        // =========================================================================================
        //  3. THE TWO NO-GO ANGLES DISAGREE — MEASURED, AND SIZED
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ <b>THE OWNER'S RULING HAS A SIZE.</b> The polar's model returns zero below twa 35
        /// TRUE; the rig draws both sails flogging below awa 25 APPARENT. They are in different
        /// frames, so no single number reconciles them, and the shipped grid contains cells where the
        /// polar hands out a speed and the sprite shows a boat in irons at it.
        ///
        /// <para><b>⚠️ THE DROP'S OWN READMEs DESCRIBE THIS BACKWARDS.</b> Both say the 88 reads "in
        /// irons" at twa 40 <i>above</i> ~10 kn true. Measured off the shipped rows, the affected band
        /// is 4–14 kn and she CLEARS at 16 kn and above — the apparent wind draws forward when the
        /// boat is fast relative to the true wind, so the defect bites in LIGHT air, which is exactly
        /// when a player is most likely to be pinching. The numbers in the file are right; the prose
        /// about them is not. This test pins the numbers.</para>
        /// </summary>
        [Test]
        public void ThePolarAndTheSpriteDisagreeAboutNoGo_AndTheDisagreementIsInLightAir()
        {
            foreach ((string rig, string sailing, int expectedIrons) in new[]
            {
                (Rig30, GameplayFolder + "/sloopIsoRig.sailing.json", 4),
                (Rig88, GameplayFolder + "/sloop88IsoRig.sailing.json", 14),
            })
            {
                SailingSidecarRead read = SailingSidecarReader.Read(
                    File.ReadAllText(Full(sailing)), sailing, File.ReadAllBytes(Full(rig)));
                Assert.IsEmpty(read.Errors);

                Assert.That(read.NoGoTrueWindDeg, Is.EqualTo(35f).Within(1e-3),
                    "the polar's no-go, parsed out of its own note.");
                Assert.That(read.IronsApparentDeg, Is.EqualTo(25f).Within(1e-3),
                    "the rig's in-irons band, as WIND.thresholds declares it.");

                var irons = read.PolarRows
                                .Where(r => string.Equals(r.Mode, "in irons", StringComparison.Ordinal))
                                .ToList();
                Assert.That(irons.Count, Is.EqualTo(expectedIrons),
                    $"{sailing}: the number of grid cells the polar sails and the sprite flogs. If " +
                    "this moved, the coupling changed and the owner's ruling needs re-taking.");
                Assert.IsNotEmpty(irons);

                // THE DIRECTION, which is the half the drop's prose gets wrong. Every affected cell is
                // at or below the middle of the wind range, and the tightest angle at the TOP of the
                // range is sailable.
                float worstWind = irons.Max(r => r.TrueWindKn);
                float topWind = read.PolarTrueWindKn.Max();
                Assert.Less(worstWind, topWind,
                    $"{sailing}: the in-irons cells run to {worstWind} kn of a {topWind} kn range. The " +
                    "coupling defect is a LIGHT-AIR one — apparent wind draws forward as boat speed " +
                    "grows relative to true wind — not the heavy-air one both READMEs describe.");

                foreach (SailingPolarRow r in irons)
                    Assert.Less(r.ApparentWindAngleDeg, read.IronsApparentDeg,
                        "a cell the rig calls 'in irons' must be inside the rig's own apparent band; " +
                        "if it is not, the sidecar's mode column and its awa column disagree.");
            }
        }

        // =========================================================================================
        //  4. THE RIG TREE IS NO LONGER FLAT
        // =========================================================================================

        /// <summary>
        /// ⭐ <b>The flat path still wins.</b> This is what keeps the widened resolver from moving any
        /// committed hull's staleness check: every rig that was findable flat is still found flat,
        /// and only a name that is NOT flat reaches the recursive search.
        /// </summary>
        [Test]
        public void ResolveRigPath_PrefersTheFlatFile_ThenSearchesTheKitFolders()
        {
            string root = RigCatalog.RepoRoot;

            string flat = DeckSidecarReader.ResolveRigPath(root, "puntIsoRig.js");
            Assert.IsNotNull(flat);
            Assert.That(Path.GetDirectoryName(flat)!.Replace('\\', '/'),
                        Does.EndWith("docs/art/rigs"),
                        "a rig that sits flat must still resolve flat.");

            foreach ((string name, string expectedTail) in new[]
            {
                ("sloopIsoRig.js", "sail-rig-kit/sloop-30"),
                ("sloop88IsoRig.js", "sail-rig-kit/sloop-88"),
            })
            {
                string found = DeckSidecarReader.ResolveRigPath(root, name);
                Assert.IsNotNull(found, $"{name} should be found under the rig tree.");
                Assert.That(found!.Replace('\\', '/'), Does.Contain(expectedTail));
            }

            Assert.IsNull(DeckSidecarReader.ResolveRigPath(root, "noSuchRigAnywhere.js"),
                "a name with no match returns null so the caller can report a refusal — it must not " +
                "throw, and it must not fall back to a near match.");
        }

        // =========================================================================================
        //  5. THE CATALOG AND THE FLEET AGREE WITH THE PIXELS
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ <b>Counter-clockwise, re-measured HERE off the rig's own projected anchors</b> rather
        /// than inherited from the catalog line it is checking.
        ///
        /// <para>The method is the camper's, with one correction the sloops force: the bearing is
        /// taken from a PORT/STARBOARD pair that shares a y and a z, so the offset is pure +X and the
        /// anchor's height cannot enter it. Reading a single lifted anchor instead (the bow roller,
        /// 2.044 m up) gives steps of −29.8° / −32.4° / −42.1° … summing to 330° over seven — a
        /// plausible-looking incoherence that has nothing to do with heading.</para>
        /// </summary>
        [Test]
        public void BothSloopsMeasureCounterClockwise_FromAPairAtEqualHeight()
        {
            foreach ((string rig, string global, string port, string stbd) in new[]
            {
                (Rig30, "SloopIso", "winchPort", "winchStbd"),
                (Rig88, "Sloop88Iso", "helmPort", "helmStbd"),
            })
            {
                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(File.ReadAllText(Full(rig)));

                double sinElev = Math.Sin(host.EvaluateNumber($"{global}.defaultElev") * Math.PI / 180.0);
                Assert.Greater(sinElev, 0.1, "elevation must be readable off the rig, not assumed.");

                // level, still, stowed — heel would roll the model and is not part of the heading
                const string Flat = "{awa:60,aws:0,hoist:0,furl:1,cover:true,frame:0,rock:false,heel:false,sfurl:1}";
                var headings = new double[8];
                for (int d = 0; d < 8; d++)
                {
                    double vx = host.EvaluateNumber($"{global}.anchors({d},{Flat}).{stbd}.x - {global}.anchors({d},{Flat}).{port}.x");
                    double vy = host.EvaluateNumber($"{global}.anchors({d},{Flat}).{stbd}.y - {global}.anchors({d},{Flat}).{port}.y");
                    double starboardBearing = Math.Atan2(vx, -vy / sinElev) * 180.0 / Math.PI;
                    headings[d] = ((starboardBearing - 90.0) % 360.0 + 360.0) % 360.0;
                }

                for (int d = 1; d < 8; d++)
                {
                    double step = ((headings[d] - headings[d - 1] + 540.0) % 360.0) - 180.0;
                    Assert.That(step, Is.EqualTo(-45.0).Within(1e-3),
                        $"{global}: dir {d - 1}→{d} steps {step:0.000}°. Every boat in this fleet is " +
                        "counter-clockwise; a positive step means the rig changed handedness and " +
                        "RigCatalog.SailKit's declaration is now a lie.");
                }

                Assert.That(RigCatalog.Entries.Values.Single(e => e.GlobalName == global).DeclaredConvention,
                            Is.EqualTo(AzimuthConvention.CounterClockwise),
                    $"{global}: the catalog must declare what the pixels measure.");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>The ramp table does not fit unfiltered, and that is a quiet failure.</b>
        /// <c>palette({}).mats</c> is 18 entries on the 30 and 19 on the 88;
        /// <c>HullMeshDef.HullRampSlots</c> is 16 and <c>IsUsable()</c> returns false above it — so the
        /// bare form writes a def the game refuses to present rather than blowing up at extraction.
        ///
        /// <para>Filtering to the materials the static face list actually names gives 14 on both. The
        /// entries dropped are the sail's, and they are unreferenced because the sails are not in
        /// <c>F</c> at all.</para>
        ///
        /// <para><b>⚠️ 14 + 3 sail ramps = 17.</b> That arithmetic is the seam: an articulated sail
        /// part cannot share the body's ramp table.</para>
        /// </summary>
        [Test]
        public void TheFullRampTableOverflowsTheSixteenSlots_AndTheBodysUsedSetDoesNot()
        {
            foreach ((string rig, string global, int full) in new[]
            {
                (Rig30, "SloopIso", 18),
                (Rig88, "Sloop88Iso", 19),
            })
            {
                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(File.ReadAllText(Full(rig)));

                double fullCount = host.EvaluateNumber($"Object.keys({global}.palette({{}}).mats).length");
                Assert.That(fullCount, Is.EqualTo((double)full),
                    $"{global}: the full ramp table moved. Re-measure the filter before baking.");
                Assert.Greater(fullCount, 16d,
                    $"{global}: if the full table now fits, the reconstruction entry in " +
                    "RigMeshExtractor is no longer load-bearing and should be deleted rather than " +
                    "left to explain a problem that is gone.");

                host.Execute(
                    $"globalThis.__used = (function(){{var F={global}.faces(),M={global}.palette({{}}).mats," +
                    $"u={{}},o={{}};for(var i=0;i<F.length;i++)u[F[i].mat]=1;" +
                    $"for(var k in M)if(u[k])o[k]=M[k];return Object.keys(o);}})();");

                double used = host.EvaluateNumber("__used.length");
                Assert.That(used, Is.EqualTo(14d),
                    $"{global}: the body names {used} materials; 14 is what the bake was sized on.");
                Assert.LessOrEqual(used, 16d, $"{global}: the filtered table must fit the 16 slots.");
                Assert.That(host.EvaluateBool("__used[0] === 'paint'"), Is.True,
                    $"{global}: MATS order IS the baked material index and the face packer resolves " +
                    "an unknown material to index 0, so the default ramp must stay first.");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>The sails are not in the mesh, and the number says how much that is.</b>
        /// <c>faces()</c> is the rig's static <c>F</c> — the body — and it is byte-identical after
        /// renders at opposite poses. Everything that answers the wind is built per pose by a private
        /// <c>dynamicFaces()</c>, so this bake does not freeze one sail pose: it has no sails at all.
        /// </summary>
        [Test]
        public void TheStaticBodyIsPoseFree_AndTheSailsAreNotInIt()
        {
            foreach ((string rig, string global, int faces) in new[]
            {
                (Rig30, "SloopIso", 1852),
                (Rig88, "Sloop88Iso", 3088),
            })
            {
                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(File.ReadAllText(Full(rig)));

                Assert.That(host.EvaluateNumber($"{global}.faces().length"), Is.EqualTo((double)faces),
                    $"{global}: the static body's face count moved — re-measure the bake.");

                // render at two opposite poses, then ask again: same array, same length.
                host.Execute($"{global}.render(0,{{awa:45,aws:12,hoist:1,furl:0,cover:false,frame:0,sfurl:1}});");
                host.Execute($"var __a = {global}.faces();");
                host.Execute($"{global}.render(3,{{awa:170,aws:22,hoist:0,furl:1,cover:true,frame:5,view:'cabin',sfurl:1}});");
                host.Execute($"var __b = {global}.faces();");
                Assert.That(host.EvaluateBool("__a === __b && __a.length === __b.length"), Is.True,
                    $"{global}: faces() changed with the pose. If the body ever becomes pose-dependent " +
                    "the mesh bake really would be freezing one pose, and the seam question changes.");

                Assert.That(host.EvaluateBool(
                    $"(function(){{var F={global}.faces();for(var i=0;i<F.length;i++)" +
                    $"if(F[i].mat==='sail'||F[i].mat==='canvas'||F[i].mat==='batten')return false;" +
                    $"return true;}})()"), Is.True,
                    $"{global}: a face in the static body wears a SAIL material. The body is supposed " +
                    "to carry no cloth — if it does, the filtered ramp table above is wrong too.");
            }
        }

        /// <summary>
        /// ⭐ <b>The compass string renders NOTHING, silently.</b> Both READMEs talk in
        /// <c>N NE E SE …</c>; <c>render</c> takes an integer. <c>render('N', …)</c> makes the camera
        /// angle NaN and returns a fully transparent cell with no error — the camper's trap, at a new
        /// scale. Any baker must assert an opaque-pixel floor per cell rather than trust its caller.
        /// </summary>
        [Test]
        public void TheCompassStringRendersAnEmptyCell()
        {
            foreach ((string rig, string global) in new[] { (Rig30, "SloopIso"), (Rig88, "Sloop88Iso") })
            {
                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(File.ReadAllText(Full(rig)));
                host.Execute(
                    "globalThis.__op = function(p){var n=0;for(var i=3;i<p.length;i+=4)if(p[i]>=128)n++;return n;};");

                const string Opts = "{awa:45,aws:12,main:0.7,jib:0.7,hoist:1,furl:0,cover:false,frame:0,sfurl:1,scheme:'gelcoat-white'}";
                double good = host.EvaluateNumber($"__op({global}.render(0,{Opts}))");
                double byName = host.EvaluateNumber($"__op({global}.render('N',{Opts}))");

                Assert.Greater(good, 1000d, $"{global}: dir 0 should draw a boat.");
                Assert.That(byName, Is.EqualTo(0d),
                    $"{global}: render('N') drew {byName} px. If the rig learns to accept compass " +
                    "strings this guard should be deleted, not relaxed.");
            }
        }

        // =========================================================================================
        //  6. THE FLEET TABLE
        // =========================================================================================

        [Test]
        public void BothSloopsAreOnTheBakeBlockedLedger_AndTheirRigsExist()
        {
            Assert.That(HullMeshFleet.BakeBlocked.Keys, Is.EquivalentTo(new[] { Rig30, Rig88 }),
                "the ledger must name exactly the rigs whose bake is blocked, by the same " +
                "repo-relative path the fleet table uses, so an entry travels with its file.");

            foreach (var kv in HullMeshFleet.BakeBlocked)
            {
                FileAssert.Exists(Full(kv.Key),
                    $"BakeBlocked names '{kv.Key}' but no such rig exists any more. Delete the entry " +
                    "— a ledger that outlives its files rots into folklore.");
                Assert.That(kv.Value, Is.Not.Empty, $"{kv.Key}: a block without a reason is a shrug.");
            }

            foreach (string key in new[] { "sloop30", "sloop88" })
                Assert.That(HullMeshFleet.TryGet(key, out _), Is.False,
                    $"'{key}' is in HullMeshFleet.Hulls while still on the BakeBlocked ledger. A hull " +
                    "is on one list or the other, never both — every fixture that sweeps Hulls " +
                    "expects a committed HullMeshDef behind each row.");
        }

        /// <summary>
        /// ⭐⭐ <b>THE LEVEL CONTRACT, RE-MEASURED — and since S0 it is SATISFIED.</b>
        ///
        /// <para>Both rigs publish <c>geometry().ids</c>, which arms the extractor's level-tag
        /// contract: every face handed over must declare a level that <c>ids</c> names. Until S0
        /// neither did — the faces carried a CUTAWAY vocabulary (<c>cabin · lid · rig · under</c>)
        /// while <c>ids</c> published a LEVEL one, and on the 88 the two shared exactly one member,
        /// <c>rig</c>: 449 of her faces were stamped <c>cabin</c>, which she does not declare at all.
        /// This test used to assert that break and was written to FAIL on the fix. It has done that
        /// job, so it is turned around and pins the fix instead.</para>
        ///
        /// <para><b>What S0 changed, and what it deliberately did not.</b> Each rig now carries the
        /// pass-3 authoring cursor, so every face declares its level at the point it is emitted; the
        /// three switches the rasteriser used to read off <c>lv</c> became face PROPERTIES
        /// (<c>inside</c> / <c>lid</c> / <c>under</c>), so a level tag can no longer move a pixel —
        /// every render is byte-identical across the change and the exported geometry did not move.
        /// <b>The bake itself is still owed to S1.</b> These two stay on
        /// <see cref="HullMeshFleet.BakeBlocked"/> until an editor lane runs
        /// <c>RigMeshAssetBaker.BakeSloopsCli</c> and commits the <c>HullMeshDef</c>s, because
        /// delisting without a committed def reddens every fixture that sweeps <c>Hulls</c>. The
        /// ledger's reasons say exactly that, and
        /// <see cref="BothSloopsAreOnTheBakeBlockedLedger_AndTheirRigsExist"/> keeps them honest.</para>
        /// </summary>
        [Test]
        public void TheLevelContractIsSatisfied_EveryFaceDeclaresALevelItsOwnIdsNames()
        {
            foreach ((string rig, string global) in new[] { (Rig30, "SloopIso"), (Rig88, "Sloop88Iso") })
            {
                using IRigScriptHost host = RigScriptHostFactory.Create();
                host.Execute(File.ReadAllText(Full(rig)));

                Assert.That(host.EvaluateBool($"!!({global}.geometry && {global}.geometry().ids)"), Is.True,
                    $"{global}: the rig no longer publishes geometry().ids, so the level-tag contract " +
                    "is no longer armed and the bake may simply work. Delist her from BakeBlocked.");

                // ⚠️ EVERY fragment is interpolated, deliberately. A `$"…"` head concatenated with a
                // plain `"…}}"` tail leaves the tail's braces DOUBLED in the JS, and the engine
                // answers "SyntaxError: Unexpected token '}'" from inside a string this file built —
                // which reads like the rig being malformed. Keep the `$` on all of them.
                host.Execute(
                    $"globalThis.__bad = (function(){{var F={global}.faces(),ids={global}.geometry().ids," +
                    $"n=0;for(var i=0;i<F.length;i++){{var lv=F[i].lv;" +
                    $"if(lv==null||!Object.prototype.hasOwnProperty.call(ids,lv))n++;}}return n;}})();");

                double bad = host.EvaluateNumber("__bad");
                double total = host.EvaluateNumber($"{global}.faces().length");

                Assert.Greater(total, 0d,
                    $"{global}.faces() came back empty, so `bad == 0` is vacuous. The probe is wrong, " +
                    "not the rig — a fixture that measures a buffer can publish a lie.");

                Assert.That(bad, Is.EqualTo(0d),
                    $"{global}: {bad:0} of {total:0} faces carry no level her own geometry().ids " +
                    "names, so RigMeshExtractor REFUSES her. It does not default, because the only " +
                    "defensible default is 'hull', which means NEVER CULL. S0 " +
                    "(art/sloop-face-levels) gave each rig the pass-3 authoring cursor and every " +
                    "emission path rides it, so a face that escaped came in through a path the " +
                    "cursor does not ride, or through a widening that built faces outside it. Stamp " +
                    "it where it is EMITTED — never by re-deriving a tag from geometry.");

                TestContext.WriteLine(
                    $"{global}: all {total:0} faces declare a level its geometry().ids names.");
            }
        }

        // ---- helpers ------------------------------------------------------------------------------

        /// <summary>Pulls one top-level string out of a JSON document without a parser dependency —
        /// the same shape the other kit probes use.</summary>
        static string ReadJsonString(string json, string key)
        {
            int k = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (k < 0) return "";
            int colon = json.IndexOf(':', k);
            int open = json.IndexOf('"', colon + 1);
            int close = json.IndexOf('"', open + 1);
            return open < 0 || close < 0 ? "" : json.Substring(open + 1, close - open - 1);
        }
    }
}
