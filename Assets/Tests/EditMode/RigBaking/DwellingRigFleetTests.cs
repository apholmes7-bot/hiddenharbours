using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ⭐ <b>THE DWELLING COVERAGE LAW — a parked dwelling cannot arrive unnoticed either.</b>
    ///
    /// <para>The third of these, after the hull law and
    /// <see cref="VehicleRigFleetTests"/>, and it exists because <b>neither of the other two can see a
    /// camper</b>. The hull law scans for <c>rollA</c>, which a parked trailer correctly does not have;
    /// the vehicle law keys on a top-level <c>"kind": "road_vehicle"</c>, which the camper's sidecar
    /// does not declare. Both of those are asserted below rather than asserted-in-prose, because they
    /// are the entire justification for a third table and the day either stops being true this file
    /// should either change its rule or be deleted.</para>
    /// </summary>
    public class DwellingRigFleetTests
    {
        static string RepoRoot => RigCatalog.RepoRoot;

        static string SidecarFolder => Path.Combine(RepoRoot, DwellingRigFleet.SidecarFolder);

        /// <summary>
        /// Every sidecar in the dwellings folder.
        ///
        /// <para>⚠ Unlike <see cref="VehicleRigFleetTests"/>, membership is the FOLDER and not a
        /// declared kind — the camper drop publishes no top-level <c>kind</c> to key on. That is a
        /// weaker rule and it is called out in <see cref="DwellingRigFleet"/>: the upstream ask is for
        /// art to stamp one so this can key on the same kind of signal its sibling does.</para>
        /// </summary>
        static IEnumerable<string> DwellingSidecarsOnDisk() =>
            Directory.EnumerateFiles(SidecarFolder, "*.gameplay.json")
                     .Select(Path.GetFileName)
                     .OrderBy(f => f, System.StringComparer.Ordinal);

        // =============================================================================================
        //  THE COVERAGE LAW
        // =============================================================================================

        [Test]
        public void EveryDwellingOnDisk_IsEitherBakedOrExplicitlyExcluded()
        {
            var registered = new HashSet<string>(
                DwellingRigFleet.Dwellings.Select(d => Path.GetFileName(d.SidecarPath)),
                System.StringComparer.Ordinal);

            var missed = DwellingSidecarsOnDisk().Where(f => !registered.Contains(f)).ToList();

            CollectionAssert.IsEmpty(missed,
                "A dwelling sidecar is in " + DwellingRigFleet.SidecarFolder + " but DwellingRigFleet " +
                "does not know about it: " + string.Join(", ", missed) + ".\n" +
                "Art arrives by PR, so this is the expected way a new dwelling shows up. Add it to " +
                "DwellingRigFleet.Dwellings, and then either to Baked (it gets sheets) or to NotBaked " +
                "with the reason (it does not). Do not silence this by editing the sidecar — " +
                "docs/art/rigs/** is read-only to us.");
        }

        [Test]
        public void EveryRegisteredDwelling_IsEitherBakedOrCarriesAReason()
        {
            foreach (DwellingRigFleet.Dwelling d in DwellingRigFleet.Dwellings)
            {
                bool baked = DwellingRigFleet.Baked.Contains(d.Key);
                bool excused = DwellingRigFleet.NotBaked.ContainsKey(d.Key);

                Assert.That(baked || excused, Is.True,
                    $"'{d.Key}' is registered but is in neither Baked nor NotBaked — it would be " +
                    "quietly unbaked, which is the one thing this table exists to make impossible.");

                Assert.That(baked && excused, Is.False,
                    $"'{d.Key}' is BOTH baked and excused. One of the two is stale.");

                if (excused)
                    Assert.That(DwellingRigFleet.NotBaked[d.Key], Is.Not.Empty,
                        $"'{d.Key}' is excused with an empty reason. A refusal without a reason rots " +
                        "into folklore.");
            }
        }

        /// <summary>Both files a registered dwelling names are really there. A table that outlives its
        /// files goes on explaining a camper nobody can bake.</summary>
        [Test]
        public void EveryRegisteredDwellingsFilesExist()
        {
            foreach (DwellingRigFleet.Dwelling d in DwellingRigFleet.Dwellings)
            {
                FileAssert.Exists(Path.Combine(RepoRoot, d.ScriptPath),
                    $"DwellingRigFleet lists '{d.Key}' with rig {d.ScriptPath}, which does not exist.");
                FileAssert.Exists(Path.Combine(RepoRoot, d.SidecarPath),
                    $"DwellingRigFleet lists '{d.Key}' with sidecar {d.SidecarPath}, which does not " +
                    "exist.");
            }
        }

        // =============================================================================================
        //  WHY THIS TABLE EXISTS — pinned, so it can be deleted when the reason goes away
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>The hull law is BLIND to a camper, and this is the measurement that says so.</b>
        /// <c>HullMeshFleetTests</c> finds hull rigs by scanning for <c>rollA</c> — a hull's sea-rock
        /// amplitude. A parked trailer has none, correctly. If a future camper rig ever grows one, this
        /// fails and the two tables' populations have started to overlap, which is a real problem
        /// worth being told about.
        /// </summary>
        [Test]
        public void TheCamperRigCarriesNoRollA_SoTheHullLawCannotSeeIt()
        {
            string rig = File.ReadAllText(Path.Combine(RepoRoot, DwellingRigFleet.CamperRigPath));

            Assert.That(rig.Contains("rollA"), Is.False,
                "camperIsoRig.js now contains 'rollA', the signal HullMeshFleetTests scans for. Either " +
                "the camper grew a sea-rock (it should not — it is parked) or the hull law will start " +
                "claiming it. Settle which table owns it before the next bake.");
        }

        /// <summary>
        /// 🔴 <b>The vehicle law is blind to it too</b> — its population is a top-level
        /// <c>"kind": "road_vehicle"</c>, and these sidecars declare no top-level <c>kind</c> at all.
        ///
        /// <para>⚠ The nested <c>"kind"</c> fields in these files (<c>berth</c>, <c>seat</c>,
        /// <c>table</c>, <c>counter</c>, <c>tall</c>) are fit-out classifications, so this asks the
        /// narrow question — is the FIRST <c>kind</c> the top-level one, and is it
        /// <c>road_vehicle</c>? — rather than a loose substring search that would answer a different
        /// one.</para>
        ///
        /// <para>The day art stamps a kind on these files, this test fails. <b>That is the intended
        /// outcome</b>: it is the prompt to make <see cref="DwellingRigFleet"/> key on art's own word
        /// instead of on which folder a file was put in.</para>
        /// </summary>
        [Test]
        public void TheCamperSidecarsDeclareNoRoadVehicleKind_WhichIsWhyThisTableExists()
        {
            foreach (DwellingRigFleet.Dwelling d in DwellingRigFleet.Dwellings)
            {
                string json = File.ReadAllText(Path.Combine(RepoRoot, d.SidecarPath));

                Assert.That(json.Contains("\"" + VehicleRigFleet.RoadVehicleKind + "\""), Is.False,
                    $"'{d.Key}' now declares kind '{VehicleRigFleet.RoadVehicleKind}'. If art has " +
                    "reclassified the camper as a road vehicle it belongs in VehicleRigFleet and this " +
                    "table should lose the entry; if it has stamped some OTHER kind, key " +
                    "DwellingRigFleet on it and stop using the folder as the signal.");
            }
        }

        // =============================================================================================
        //  THE STALENESS RULE
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Every sidecar still pins the rig ON DISK.</b> An absent or wrong
        /// <c>derivedFromRigSha256</c> is a REFUSAL by design — polygons cut from a different shape are
        /// worse than no polygons. Both failure modes have shipped in this repo's boat drops.
        ///
        /// <para>⚠ <see cref="RigHashMatch.LineEndingNormalized"/> is an ACCEPTED pass, not a
        /// near-miss: the kit ships the rig LF-only and <c>.gitattributes</c> checks <c>.js</c> out
        /// CRLF on Windows, and a line ending cannot move a vertex. The Dually's import measured
        /// exactly this.</para>
        ///
        /// <para>Both camper sidecars are generated from ONE rig, so both must pin the SAME hash — a
        /// disagreement between them means one was regenerated and the other was not.</para>
        /// </summary>
        [Test]
        public void EverySidecarStillPinsTheRigOnDisk_AndTheyAllAgree()
        {
            var stamps = new List<string>();

            foreach (DwellingRigFleet.Dwelling d in DwellingRigFleet.Dwellings)
            {
                byte[] rig = File.ReadAllBytes(Path.Combine(RepoRoot, d.ScriptPath));
                string sidecar = File.ReadAllText(Path.Combine(RepoRoot, d.SidecarPath));

                string expected = ReadJsonString(sidecar, "derivedFromRigSha256");
                Assert.That(expected, Is.Not.Empty,
                    $"'{d.Key}' carries no derivedFromRigSha256. An absent hash is a refusal by " +
                    "design — ask the art side to re-stamp it rather than reading the polygons anyway.");
                stamps.Add(expected);

                RigHashMatch match = DeckSidecarReader.MatchRigHash(rig, expected, out string actual);

                Assert.That(match, Is.Not.EqualTo(RigHashMatch.None),
                    $"'{d.Key}' pins {expected} but the rig on disk hashes to {actual}, and not " +
                    "through a line-ending difference either. The camper was reshaped and the sidecar " +
                    "was not re-derived — do not trust its geometry.");
            }

            Assert.That(stamps.Distinct().Count(), Is.EqualTo(1),
                "the camper's sidecars pin DIFFERENT rig hashes: " + string.Join(" / ", stamps) +
                ". They are generated from one rig by one generator, so a disagreement means one of " +
                "them was regenerated against a reshaped camper and the other was left behind.");
        }

        /// <summary>The sidecar names its rig and its variant inside the file, so a reader never has to
        /// infer either from the filename.</summary>
        [Test]
        public void EverySidecarNamesItsRigAndItsVariantInsideTheFile()
        {
            foreach (DwellingRigFleet.Dwelling d in DwellingRigFleet.Dwellings)
            {
                string json = File.ReadAllText(Path.Combine(RepoRoot, d.SidecarPath));

                Assert.That(ReadJsonString(json, "exportSymbol"), Is.EqualTo(d.GlobalName));
                Assert.That(ReadJsonString(json, "variant"), Is.EqualTo(d.Key),
                    $"the sidecar at {d.SidecarPath} calls itself something other than '{d.Key}', so " +
                    "the table's key and the file have drifted apart.");
            }
        }

        /// <summary>Minimal top-level string read — the same shape the Dually's probe uses, and
        /// deliberately not a JSON dependency for four fields.</summary>
        static string ReadJsonString(string json, string key)
        {
            int at = json.IndexOf("\"" + key + "\"", System.StringComparison.Ordinal);
            if (at < 0) return string.Empty;

            int colon = json.IndexOf(':', at);
            if (colon < 0) return string.Empty;

            int open = json.IndexOf('"', colon);
            if (open < 0) return string.Empty;

            int close = json.IndexOf('"', open + 1);
            return close < 0 ? string.Empty : json.Substring(open + 1, close - open - 1);
        }
    }
}
