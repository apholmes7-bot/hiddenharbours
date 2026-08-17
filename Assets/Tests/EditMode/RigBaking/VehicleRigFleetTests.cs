using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ⭐ <b>THE VEHICLE COVERAGE LAW — a road vehicle cannot arrive unnoticed.</b>
    ///
    /// <para>The direct descendant of
    /// <c>HullMeshFleetTests.EveryHullRigOnDisk_IsEitherBakedOrExplicitlyExcluded</c>, and it exists
    /// because that test <b>cannot see a truck</b>. It scans <c>docs/art/rigs/</c> for rigs containing
    /// the signal <c>rollA</c> — a hull's sea-rock amplitude — and <c>vehicleIsoRig.js</c> has zero
    /// occurrences of it, correctly. Art arrives by PR; without a law of its own, the next vehicle
    /// drop lands in the repo and is silently never baked, which is the exact failure the hull law was
    /// written to prevent.</para>
    ///
    /// <para>The population is defined by the SIDECAR, not by the rig: a road vehicle declares itself
    /// with a top-level <c>"kind": "road_vehicle"</c>. That is art's own word for the thing rather
    /// than a substring this file guesses at, and boat sidecars carry no top-level <c>kind</c>, so the
    /// two populations cannot overlap.</para>
    /// </summary>
    public class VehicleRigFleetTests
    {
        static string RepoRoot => RigCatalog.RepoRoot;

        static string SidecarFolder => Path.Combine(RepoRoot, VehicleRigFleet.SidecarFolder);

        /// <summary>Sidecar file names whose top-level <c>kind</c> is <c>road_vehicle</c>.
        ///
        /// <para>⚠ Matched on the whole <c>"kind": "road_vehicle"</c> pair rather than on the value
        /// alone: the Dually's own sidecar contains a dozen NESTED <c>kind</c> fields (<c>bucket</c>,
        /// <c>bench</c>, <c>vertical</c>…), so a looser match would be answering a different
        /// question.</para></summary>
        static IEnumerable<string> RoadVehicleSidecarsOnDisk() =>
            Directory.EnumerateFiles(SidecarFolder, "*.gameplay.json")
                     .Where(f => DeclaresPair(File.ReadAllText(f),
                                              "\"kind\"", "\"" + VehicleRigFleet.RoadVehicleKind + "\""))
                     .Select(Path.GetFileName)
                     .OrderBy(f => f, System.StringComparer.Ordinal);

        /// <summary>Is <paramref name="key"/> immediately followed by <paramref name="value"/>, allowing
        /// only a colon and whitespace between? Cheap, and enough to tell a top-level declaration from
        /// a nested field that merely reuses the word.
        ///
        /// <para>⚠ NOT called <c>Contains</c>: a static method of that name shadows NUnit's
        /// <c>Contains</c> constraint class and breaks every <c>Assert.That(x, Contains.Item(y))</c> in
        /// the fixture with a CS0119 that points at the assert rather than at the helper.</para>
        /// </summary>
        static bool DeclaresPair(string json, string key, string value)
        {
            int at = json.IndexOf(key, System.StringComparison.Ordinal);
            while (at >= 0)
            {
                int i = at + key.Length;
                while (i < json.Length && (json[i] == ':' || char.IsWhiteSpace(json[i]))) i++;
                if (i + value.Length <= json.Length &&
                    string.CompareOrdinal(json, i, value, 0, value.Length) == 0)
                    return true;
                at = json.IndexOf(key, at + key.Length, System.StringComparison.Ordinal);
            }
            return false;
        }

        // =============================================================================================

        [Test]
        public void EveryRoadVehicleOnDisk_IsEitherBakedOrExplicitlyExcluded()
        {
            var registered = new HashSet<string>(
                VehicleRigFleet.Vehicles.Select(v => Path.GetFileName(v.SidecarPath)),
                System.StringComparer.Ordinal);

            var missed = RoadVehicleSidecarsOnDisk().Where(f => !registered.Contains(f)).ToList();

            CollectionAssert.IsEmpty(missed,
                "A road-vehicle sidecar is in " + VehicleRigFleet.SidecarFolder + " but VehicleRigFleet " +
                "does not know about it: " + string.Join(", ", missed) + ".\n" +
                "Art arrives by PR from the art director, so this is the expected way a new vehicle " +
                "shows up. Add it to VehicleRigFleet.Vehicles, and then either to Baked (it gets a " +
                "mesh) or to NotBaked with the reason (it does not). Do not silence this by editing " +
                "the sidecar — docs/art/rigs/** is read-only to us.");
        }

        [Test]
        public void EveryRegisteredVehicle_IsEitherBakedOrCarriesAReason()
        {
            foreach (VehicleRigFleet.Vehicle v in VehicleRigFleet.Vehicles)
            {
                bool baked = VehicleRigFleet.Baked.Contains(v.Key);
                bool excused = VehicleRigFleet.NotBaked.ContainsKey(v.Key);

                Assert.That(baked || excused, Is.True,
                    $"'{v.Key}' is registered but is in neither Baked nor NotBaked — it would be " +
                    "quietly unbaked, which is the one thing this table exists to make impossible.");

                Assert.That(baked && excused, Is.False,
                    $"'{v.Key}' is BOTH baked and excused. One of the two is stale.");

                if (excused)
                    Assert.That(VehicleRigFleet.NotBaked[v.Key], Is.Not.Empty,
                        $"'{v.Key}' is excused with an empty reason. A refusal without a reason rots " +
                        "into folklore.");
            }
        }

        /// <summary>Both files a registered vehicle names are really there. A table that outlives its
        /// files goes on explaining a vehicle nobody can bake.</summary>
        [Test]
        public void EveryRegisteredVehiclesFilesExist()
        {
            foreach (VehicleRigFleet.Vehicle v in VehicleRigFleet.Vehicles)
            {
                FileAssert.Exists(Path.Combine(RepoRoot, v.ScriptPath),
                    $"VehicleRigFleet lists '{v.Key}' with rig {v.ScriptPath}, which does not exist.");
                FileAssert.Exists(Path.Combine(RepoRoot, v.SidecarPath),
                    $"VehicleRigFleet lists '{v.Key}' with sidecar {v.SidecarPath}, which does not " +
                    "exist.");
            }
        }

        /// <summary>
        /// ⭐ Every registered vehicle's sidecar still pins its own rig.
        ///
        /// <para>The staleness rule applied across the table rather than to one drop, so a re-shaped
        /// rig landing without a re-stamped sidecar fails here even if nobody thought to update
        /// <c>DuallyIsoKitProbeTests</c>. <see cref="RigHashMatch.LineEndingNormalized"/> passes: the
        /// kits ship LF and <c>.gitattributes</c> checks <c>.js</c> out CRLF on Windows, and a line
        /// ending cannot move a vertex.</para>
        /// </summary>
        [Test]
        public void EveryRegisteredVehiclesSidecarStillPinsItsRig()
        {
            foreach (VehicleRigFleet.Vehicle v in VehicleRigFleet.Vehicles)
            {
                byte[] rig = File.ReadAllBytes(Path.Combine(RepoRoot, v.ScriptPath));
                string sidecar = File.ReadAllText(Path.Combine(RepoRoot, v.SidecarPath));

                int at = sidecar.IndexOf("derivedFromRigSha256", System.StringComparison.Ordinal);
                Assert.That(at, Is.GreaterThanOrEqualTo(0),
                    $"'{v.Key}' carries no derivedFromRigSha256. An absent hash is a REFUSAL by " +
                    "design — do not read its geometry.");

                int open = sidecar.IndexOf('"', sidecar.IndexOf(':', at) + 1);
                int close = sidecar.IndexOf('"', open + 1);
                string expected = sidecar.Substring(open + 1, close - open - 1);

                RigHashMatch match = DeckSidecarReader.MatchRigHash(rig, expected, out string actual);

                Assert.That(match, Is.Not.EqualTo(RigHashMatch.None),
                    $"'{v.Key}': the sidecar pins {expected} but {v.ScriptPath} hashes to {actual}, " +
                    "and not through a line-ending difference. The vehicle was reshaped and its " +
                    "sidecar was not re-derived.");
            }
        }

        /// <summary>A reason may not name a vehicle that is not registered — the same anti-folklore
        /// rule <c>HullMeshFleetTests.TheExclusionList_OnlyNamesRigsThatExist</c> applies to hulls.
        /// </summary>
        [Test]
        public void TheExclusionList_OnlyNamesRegisteredVehicles()
        {
            var keys = new HashSet<string>(VehicleRigFleet.Vehicles.Select(v => v.Key),
                                           System.StringComparer.Ordinal);

            foreach (var kvp in VehicleRigFleet.NotBaked)
                Assert.That(keys, Contains.Item(kvp.Key),
                    $"VehicleRigFleet.NotBaked excuses '{kvp.Key}', which is not in Vehicles. Delete " +
                    "the entry.");
        }
    }
}
