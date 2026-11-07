using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The cliff faces' GUIDs, held where a clone breaks (terrain-fidelity P1, 11-07).
    ///
    /// <para><b>⭐ WHY THIS SUITE EXISTS.</b> Every St Peters and Nine Mile Creek wall names its face by a
    /// <c>.meta</c> GUID. Until 11-07 the faces were gitignored and each box baked its own, and a bake from
    /// nothing let Unity mint a fresh GUID per file, so a clone opened walls whose faces resolved to nothing:
    /// the coast stood untextured on every machine but the one that had last baked into the scenes. The
    /// faces are now committed, and <see cref="CliffBaker"/> writes each meta's GUID from
    /// <see cref="CliffBaker.StableGuid"/> (a hash of the file's place in the kit) before the file itself.
    /// These three tests are the two halves of that contract plus the bake that ties them.</para>
    ///
    /// <list type="number">
    /// <item>The committed metas carry the GUID their place hashes to (no Unity rendering; reads files).</item>
    /// <item>A bake into a temp folder outside Assets writes exactly the committed set, at the committed
    /// GUIDs. It also LOGS whether the PNG bytes match: that is the 1.2 measure, not a pin, because the
    /// bytes belong to the encoder and the rig, and a rig edit is a PR that re-commits them.</item>
    /// <item>Every face GUID a committed wall names resolves to a file under
    /// <see cref="CliffCatalog.BakeRoot"/>: v60's <c>cliff_guid_hunt</c> as a test, run on text.</item>
    /// </list>
    /// </summary>
    public class CliffFaceGuidTests
    {
        /// <summary>The wall component every face reference hangs off.</summary>
        const string WallScriptMeta = "Assets/_Project/Code/Art/CliffWallSurface.cs.meta";

        /// <summary>The serialized fields of <c>CliffWallSurface</c> and its <c>CliffFaceBand</c> that hold a
        /// kit texture: a band's three channels, the plan profile, and the two decals.</summary>
        static readonly string[] FaceFields = { "Unlit", "Normal", "Mask", "_profile", "_browDecal", "_toeDecal" };

        /// <summary>Text copies of committed walls that are not scenes. The fixture is NMC's walls before G3,
        /// which NineMileCreekWallsAgreeWithGroundTests replays; its GUIDs were rewritten with the scenes'.</summary>
        static readonly string[] ExtraWallSources = { "Assets/Tests/EditMode/Fixtures/NineMileCreekWallsBeforeG3.txt" };

        static readonly Regex FaceRef = new Regex(
            @"^\s*-?\s*(" + string.Join("|", FaceFields) + @"): \{fileID: -?\d+, guid: ([0-9a-f]{32}), type: \d+\}",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        static readonly Regex NullFaceRef = new Regex(
            @"^\s*-?\s*(" + string.Join("|", FaceFields) + @"): \{fileID: 0\}",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static string Repo(string full) =>
            Path.GetFullPath(full).Substring(Path.GetFullPath(RigCatalog.RepoRoot).TrimEnd('\\', '/').Length + 1)
                .Replace('\\', '/');

        /// <summary>Every meta of a kit at <paramref name="kitFolder"/> (repo-relative), the folder's own
        /// included, keyed by its path in the kit; value = (asset path, GUID the meta carries).</summary>
        static Dictionary<string, (string asset, string guid)> KitMetas(string kitFolder)
        {
            var metas = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            var all = new List<string> { Full(kitFolder + ".meta") };
            if (Directory.Exists(Full(kitFolder)))
                all.AddRange(Directory.GetFiles(Full(kitFolder), "*.meta", SearchOption.AllDirectories));
            foreach (string meta in all.Where(File.Exists))
            {
                string metaRepo = Repo(meta);
                string asset = metaRepo.Substring(0, metaRepo.Length - ".meta".Length);
                metas[CliffBaker.PathInKit(kitFolder, asset)] = (asset, CliffBaker.MetaGuid(meta));
            }
            return metas;
        }

        // =====================================================================================
        //  1. the committed metas
        // =====================================================================================

        [Test]
        public void TheCommittedMetasCarryTheGuidTheirPlaceHashesTo()
        {
            Dictionary<string, (string asset, string guid)> metas = KitMetas(CliffCatalog.BakeRoot);
            Assert.That(metas, Has.Count.GreaterThan(1),
                $"No kit under {CliffCatalog.BakeRoot}. The faces are COMMITTED (under LFS) since terrain-fidelity " +
                "P1: a checkout without them is a checkout whose LFS objects never landed (git lfs pull).");

            var wrong = new List<string>();
            var orphans = new List<string>();
            foreach (KeyValuePair<string, (string asset, string guid)> m in metas)
            {
                string want = CliffBaker.StableGuid(m.Key);
                if (m.Value.guid != want) wrong.Add($"{m.Value.asset}.meta holds {m.Value.guid}; '{m.Key}' hashes to {want}");
                if (!File.Exists(Full(m.Value.asset)) && !Directory.Exists(Full(m.Value.asset)))
                    orphans.Add(m.Value.asset + ".meta");
            }

            Assert.That(wrong, Is.Empty,
                "A committed kit meta carries a GUID its place does not hash to. A clone's re-bake would write the " +
                "hashed one and every wall naming this one would lose its face. Re-run the stable-GUID rewrite " +
                "(every kit meta to CliffBaker.StableGuid AND every scene GUID with it, in one commit), never the " +
                "metas alone:\n  " +
                string.Join("\n  ", wrong.Take(20)));
            Assert.That(orphans, Is.Empty, "A kit meta with no file or folder beside it:\n  " + string.Join("\n  ", orphans));

            var shared = metas.GroupBy(m => m.Value.guid).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.That(shared, Is.Empty, "Two kit files share a GUID (the colour-slot pair must never both exist): " +
                                          string.Join(", ", shared));
        }

        // =====================================================================================
        //  2. a bake reproduces them
        // =====================================================================================

        [Test]
        public void ABakeIntoATempFolderReproducesEveryCommittedGuid()
        {
            string temp = $"Temp/CliffFaceGuidTests-{Guid.NewGuid():N}";
            try
            {
                // The committed kit is the px skin of every rock the rig knows (the px colour slot is _index).
                foreach (string rock in CliffCatalog.Rocks)
                    CliffBaker.BakeAll(outputFolder: temp, rock: rock, px: true);

                Dictionary<string, (string asset, string guid)> committed = KitMetas(CliffCatalog.BakeRoot);
                Dictionary<string, (string asset, string guid)> baked = KitMetas(temp);

                List<string> notBaked = committed.Keys.Except(baked.Keys).OrderBy(k => k, StringComparer.Ordinal).ToList();
                List<string> notCommitted = baked.Keys.Except(committed.Keys).OrderBy(k => k, StringComparer.Ordinal).ToList();
                List<string> differ = committed.Keys.Intersect(baked.Keys)
                    .Where(k => committed[k].guid != baked[k].guid)
                    .Select(k => $"'{k}': committed {committed[k].guid}, baked {baked[k].guid}").ToList();

                // The measure: same place, same bytes? Logged, not asserted (see the class summary).
                int same = 0, changed = 0;
                var changedNames = new List<string>();
                foreach (string k in committed.Keys.Intersect(baked.Keys).Where(k => k.EndsWith(".png", StringComparison.Ordinal))
                                              .OrderBy(k => k, StringComparer.Ordinal))
                {
                    if (File.ReadAllBytes(Full(committed[k].asset)).SequenceEqual(File.ReadAllBytes(Full(baked[k].asset)))) same++;
                    else { changed++; changedNames.Add(k); }
                }
                string measure = $"CliffFaceGuidTests MEASURE: {baked.Count} metas baked, {committed.Count} committed; " +
                                 $"PNG bytes identical {same}, different {changed}" +
                                 (changed > 0 ? " (" + string.Join(", ", changedNames.Take(12)) + (changed > 12 ? ", ..." : "") + ")" : "");
                Debug.Log(measure);
                TestContext.WriteLine(measure);

                Assert.That(notBaked, Is.Empty, "Committed kit files the bake does not write (a stale file, or a bake that " +
                                                "lost a slot):\n  " + string.Join("\n  ", notBaked));
                Assert.That(notCommitted, Is.Empty, "Kit files the bake writes that are not committed (a clone would " +
                                                    "lack them):\n  " + string.Join("\n  ", notCommitted));
                Assert.That(differ, Is.Empty, "A bake wrote a GUID other than the committed one at the same place:\n  " +
                                              string.Join("\n  ", differ.Take(20)));
            }
            finally
            {
                if (Directory.Exists(Full(temp))) Directory.Delete(Full(temp), recursive: true);
                if (File.Exists(Full(temp + ".meta"))) File.Delete(Full(temp + ".meta"));
            }
        }

        // =====================================================================================
        //  3. every face a wall names is in the tree
        // =====================================================================================

        [Test]
        public void EveryFaceGuidACommittedWallNamesResolvesToAFileInTheTree()
        {
            string wallScript = CliffBaker.MetaGuid(Full(WallScriptMeta));
            Assert.That(wallScript, Is.Not.Null.And.Length.EqualTo(32), $"{WallScriptMeta} has no GUID.");

            var kit = new Dictionary<string, string>(StringComparer.Ordinal);   // guid -> asset path
            foreach (var (asset, guid) in KitMetas(CliffCatalog.BakeRoot).Values)
                if (guid != null && File.Exists(Full(asset))) kit[guid] = asset;

            IEnumerable<string> sources = Directory.GetFiles(Full("Assets"), "*.unity", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(Full("Assets"), "*.prefab", SearchOption.AllDirectories))
                .Select(Repo)
                .Concat(ExtraWallSources)
                .OrderBy(p => p, StringComparer.Ordinal);

            string marker = $"m_Script: {{fileID: 11500000, guid: {wallScript}, type: 3}}";
            var unresolved = new List<string>();
            var wallsPerSource = new Dictionary<string, int>(StringComparer.Ordinal);
            int faceRefs = 0, nullRefs = 0;

            foreach (string source in sources)
            {
                string text = File.ReadAllText(Full(source)).Replace("\r\n", "\n");
                if (!text.Contains(wallScript)) continue;
                foreach (string doc in text.Split(new[] { "\n--- !u!" }, StringSplitOptions.None))
                {
                    if (!doc.Contains(marker)) continue;
                    wallsPerSource[source] = wallsPerSource.TryGetValue(source, out int n) ? n + 1 : 1;
                    string docId = doc.Substring(0, Math.Min(doc.IndexOf('\n') < 0 ? doc.Length : doc.IndexOf('\n'), 40));
                    nullRefs += NullFaceRef.Matches(doc).Count;
                    foreach (Match m in FaceRef.Matches(doc))
                    {
                        faceRefs++;
                        if (!kit.ContainsKey(m.Groups[2].Value))
                            unresolved.Add($"{source} !u!{docId} {m.Groups[1].Value} -> {m.Groups[2].Value}");
                    }
                }
            }

            string summary = "CliffFaceGuidTests WALLS: " +
                             string.Join(", ", wallsPerSource.Select(w => $"{Path.GetFileName(w.Key)} {w.Value}")) +
                             $"; face refs {faceRefs}, resolved {faceRefs - unresolved.Count}, null (no face set) {nullRefs}";
            Debug.Log(summary);
            TestContext.WriteLine(summary);

            foreach (string scene in new[] { "Assets/_Project/Scenes/StPeters.unity", "Assets/_Project/Scenes/NineMileCreek.unity" })
                Assert.That(wallsPerSource.ContainsKey(scene), Is.True,
                    $"No CliffWallSurface found in {scene}: the scan is reading the wrong script GUID or the wrong file, " +
                    "and a scan that finds no walls passes for nothing.");
            Assert.That(faceRefs, Is.GreaterThan(0), "The walls name no faces at all; the field regex has drifted from CliffWallSurface.");

            Assert.That(unresolved, Is.Empty,
                $"A committed wall names a face GUID no file under {CliffCatalog.BakeRoot} carries. On a clone that wall " +
                "draws with no face. Bake (Hidden Harbours ▸ Dev ▸ Bake Cliff Kit — px) and commit what it writes, or re-point " +
                "the wall:\n  " + string.Join("\n  ", unresolved.Take(30)) + (unresolved.Count > 30 ? $"\n  ... {unresolved.Count} in all" : ""));
        }
    }
}
