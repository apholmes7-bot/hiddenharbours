using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ⭐⭐ <b>THE HELM STATION IN THE SHIPPED ASSET IS THE ONE IN THE RIG'S SIDECAR.</b> The import half
    /// of the guard for the owner's 2026-09-09 playtest (<i>"when piloting boats the sprite does not stay
    /// in the accurate helm position"</i>); the runtime half is
    /// <c>Tests/EditMode/HelmStationTests</c>.
    ///
    /// <para><b>Why parity and not a value.</b> <see cref="BoatDeckDef"/>'s own remarks carry the law this
    /// executes — <i>"Everything here is imported, never typed… Editing these by hand re-opens exactly the
    /// failure the baked-anchors JSON died of (a hand-copied constant drifting from the rig)"</i>. A test
    /// that asserted "the cape's station is (0, 1.35, 0.74)" would be a second hand-copy of the same
    /// constant and would go green on a hull whose rig had moved. This one re-reads every sidecar through
    /// the SHIPPED reader and holds the asset against it.</para>
    ///
    /// <para><b>⚠ And it reads the .asset FILE as well as the object.</b> A serialized field absent from
    /// the YAML reads ZERO at runtime — for the flag that is "no station" and for the vector it is "the
    /// hull's pivot", and both look exactly like a clean pass on an object that was never re-imported.</para>
    ///
    /// <para>⭐ <see cref="EveryHullIsAccountedFor"/> takes a ROLL CALL. Ten shipped hulls publish no
    /// station and still fall back to the dory's tiller. That is not a failure — absence is data — but a
    /// silent fallback is exactly how one number came to steer eleven boats, so the list is printed in the
    /// run every time. It should shrink, never grow.</para>
    /// </summary>
    public class HelmStationImportTests
    {
        private const string DeckFolder = "Assets/_Project/Data/Boats/Decks";
        private const string SidecarFolder = "docs/art/rigs/gameplay";
        private const float Tol = 1e-4f;

        /// <summary>⭐ The shipped asset's station IS its sidecar's, hull by hull, through the reader
        /// production imports with.</summary>
        [Test]
        public void EveryImportedStation_MatchesItsSidecar()
        {
            var listed = new StringBuilder();
            int withStation = 0;

            foreach ((string stem, string path, BoatDeckDef def) in ShippedDecks())
            {
                string sidecar = SidecarPathOf(def, stem);
                Assert.IsTrue(File.Exists(sidecar),
                              $"{stem}: the asset names sidecar '{def.SourceSidecar}' and it is not there");

                SidecarRead read = DeckSidecarReader.Read(File.ReadAllText(sidecar), def.SourceSidecar, null);

                Assert.AreEqual(read.HasHelmStation, def.HasHelmStation,
                    $"{stem}: the sidecar says HasHelmStation={read.HasHelmStation} and the shipped asset " +
                    "says the opposite — re-run Hidden Harbours ▸ Dev ▸ Boats ▸ Import deck sidecars and " +
                    "commit the assets");

                if (!def.HasHelmStation)
                {
                    Assert.AreEqual(Vector3.zero, def.HelmStationLocalMeters,
                        $"{stem}: a hull with no station must carry no station, not a stale one — a leftover " +
                        "vector behind a false flag is the next person's afternoon");
                    Assert.IsEmpty(def.HelmStationSource ?? "",
                        $"{stem}: no station, but the asset still names where one came from");
                    continue;
                }

                withStation++;
                Assert.AreEqual(read.HelmStation.x, def.HelmStationLocalMeters.x, Tol, $"{stem}: station x");
                Assert.AreEqual(read.HelmStation.y, def.HelmStationLocalMeters.y, Tol, $"{stem}: station y");
                Assert.AreEqual(read.HelmStation.z, def.HelmStationLocalMeters.z, Tol, $"{stem}: station z");
                Assert.AreEqual(read.HelmStationSource, def.HelmStationSource,
                    $"{stem}: the asset and the sidecar disagree about WHICH key the station came from — " +
                    "provenance that has drifted is provenance that is lying");
                listed.AppendLine($"  {stem}: {def.HelmStationLocalMeters} from {def.HelmStationSource}");
            }

            Debug.Log($"[HelmStation] {withStation} hull(s) steer from their own station:\n{listed}");
            Assert.Greater(withStation, 0,
                "not one shipped deck carries a station, so every hull in the game is still steered from " +
                "the dory's tiller — the import has not been run and committed");
        }

        /// <summary>⭐ The fields are in the <b>.asset FILE</b>, not merely on the class.</summary>
        [Test]
        public void TheStationFieldsAreInTheAssetFile_NotJustOnTheClass()
        {
            var missing = new List<string>();
            foreach ((string stem, string path, BoatDeckDef _) in ShippedDecks())
            {
                string yaml = File.ReadAllText(path);
                if (!yaml.Contains("HasHelmStation") || !yaml.Contains("HelmStationLocalMeters"))
                    missing.Add(stem);
            }

            Assert.IsEmpty(missing,
                "these deck assets carry no station fields in their YAML at all, so they read ZERO however " +
                "green the class looks — re-run the deck sidecar import and commit them: " +
                string.Join(", ", missing));
        }

        /// <summary>⭐ THE ROLL CALL — every hull, named, on both sides of the line.</summary>
        [Test]
        public void EveryHullIsAccountedFor()
        {
            var stationed = new List<string>();
            var fallingBack = new List<string>();

            foreach ((string stem, string _, BoatDeckDef def) in ShippedDecks())
                (def.HasHelmStation ? stationed : fallingBack).Add(stem);

            Debug.Log($"[HelmStation] roll call — {stationed.Count} with their own station: " +
                      $"{string.Join(", ", stationed)}\n" +
                      $"[HelmStation] {fallingBack.Count} still steering from the dory's tiller " +
                      $"(art-director ask, and this list should shrink): {string.Join(", ", fallingBack)}");

            Assert.Greater(stationed.Count + fallingBack.Count, 10,
                "too few deck assets found for this roll call to mean anything");
        }

        /// <summary>⭐ The reader accepts BOTH shapes the sidecars actually publish — an object under
        /// <c>ANCHORS.helm</c> and an array under <c>STATIONS</c> — and this asserts the fleet exercises
        /// both. A ladder with a rung nothing walks on is a rung that quietly rots.</summary>
        [Test]
        public void BothSidecarShapes_AreExercisedByTheShippedFleet()
        {
            var sources = new HashSet<string>();
            foreach ((string _, string __, BoatDeckDef def) in ShippedDecks())
                if (def.HasHelmStation) sources.Add(def.HelmStationSource);

            Assert.IsTrue(sources.Any(s => s.StartsWith("ANCHORS.")),
                          $"no shipped hull uses the ANCHORS object shape (found: {string.Join(", ", sources)})");
            Assert.IsTrue(sources.Any(s => s.StartsWith("STATIONS.")),
                          $"no shipped hull uses the STATIONS array shape (found: {string.Join(", ", sources)})");
        }

        // ---- harness ---------------------------------------------------------------------------------

        private static string RepoRoot() => Directory.GetParent(Application.dataPath).FullName;

        private static string SidecarPathOf(BoatDeckDef def, string stem)
            => Path.Combine(RepoRoot(), SidecarFolder,
                            string.IsNullOrEmpty(def.SourceSidecar)
                                ? stem + ".gameplay.json"
                                : Path.GetFileName(def.SourceSidecar));

        /// <summary>Every shipped <see cref="BoatDeckDef"/>. Found by type rather than named, so a new
        /// hull joins these guards by existing rather than by somebody remembering.</summary>
        private static List<(string stem, string path, BoatDeckDef def)> ShippedDecks()
        {
            var found = new List<(string, string, BoatDeckDef)>();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:BoatDeckDef", new[] { DeckFolder }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var def = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(path);
                if (def != null) found.Add((Path.GetFileNameWithoutExtension(path), path, def));
            }
            found.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
            Assert.IsNotEmpty(found, $"no BoatDeckDef assets under {DeckFolder}");
            return found;
        }
    }
}
