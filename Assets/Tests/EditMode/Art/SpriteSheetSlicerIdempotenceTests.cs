using System.IO;
using System.Linq;
using HiddenHarbours.Art.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>Re-slicing unchanged art must not touch the <c>.meta</c>.</b>
    ///
    /// <para><see cref="SpriteSheetSlicer"/> used to call <c>GUID.Generate()</c> for every cell on every
    /// run, so simply invoking the slicer rewrote the <c>spriteID</c> of every sprite on every manifest
    /// sheet. The last time the owner ran it that churned <b>43 files</b> in his working copy — 25 of
    /// them under <c>Art/Boats</c> — burying his ~40 intentional uncommitted changes in noise. It is not
    /// a rare event either: <c>RigBaker</c> invokes the slicer automatically, so every bake did it again.</para>
    ///
    /// <para>⚠️ <b>This is a DIFF-NOISE guard, not a broken-reference guard.</b> A sprite reference
    /// resolves through the <c>internalID</c> in the <c>.meta</c>'s <c>fileIDToRecycleName</c> table, not
    /// through <c>spriteID</c> — that was investigated and disproven by negative control. What genuinely
    /// breaks references is a changed rect SET (a different slice count or different names), which this
    /// fixture also happens to pin. Do not "upgrade" the failure message to claim broken refs.</para>
    ///
    /// <para><b>How it asserts.</b> Slice once to normalize, capture the <c>.meta</c> bytes, slice again,
    /// and require the bytes to be <b>byte-identical</b>. That is the acceptance criterion stated
    /// directly rather than approximated by comparing ids we computed ourselves — it catches any future
    /// per-run nondeterminism the slicer might grow, not only the GUID one. The assets are the real
    /// imported sheets in the repo, not fixtures.</para>
    ///
    /// <para>Proven by sabotage: restoring the unconditional <c>GUID.Generate()</c> in
    /// <c>SpriteSheetSlicer.BuildRects</c> turns <see cref="ReSlicing_UnchangedArt_LeavesTheMetaByteIdentical"/>
    /// red on every case.</para>
    /// </summary>
    public class SpriteSheetSlicerIdempotenceTests
    {
        private const string Boats = "Assets/_Project/Art/Boats/";
        private const string Root = "Assets/_Project/Art/";

        /// <summary>
        /// Deliberately spans the three shapes the slicer has to keep stable, and no more (each case
        /// costs two full reimports of a large texture):
        ///   • a SheetSpec with a custom, hand-measured origin and one row (CapeIslanderIso);
        ///   • one page of a MULTI-PAGE baker sheet — the pages RigBaker emits because 32 facings × 4
        ///     rock frames would stand past the 4096 cap on a single sheet (LobsterBoatIsoRock0);
        ///   • a small, ordinary manifest sheet on a non-boat pivot (FishTray, bottom-centre).
        /// </summary>
        private static readonly string[] Sheets =
        {
            Boats + "CapeIslanderIso.png",
            Boats + "LobsterBoatIsoRock0.png",
            Root  + "Sprites/Gear/FishTray.png",
        };

        private static string MetaPath(string assetPath) => assetPath + ".meta";

        [Test]
        [TestCaseSource(nameof(Sheets))]
        public void ReSlicing_UnchangedArt_LeavesTheMetaByteIdentical(string assetPath)
        {
            Assert.IsTrue(File.Exists(assetPath), $"{assetPath}: not on disk — the fixture needs the real art");

            // First pass normalizes whatever state the working copy happens to be in; the SECOND and THIRD
            // are the ones under test. (Comparing pass 1 against the committed bytes would be a different,
            // weaker assertion — it would pass on any repo whose metas were merely already stale.)
            Assert.IsTrue(SpriteSheetSlicer.SliceSheet(assetPath), $"{assetPath}: first slice failed");
            byte[] after1 = File.ReadAllBytes(MetaPath(assetPath));

            Assert.IsTrue(SpriteSheetSlicer.SliceSheet(assetPath), $"{assetPath}: second slice failed");
            byte[] after2 = File.ReadAllBytes(MetaPath(assetPath));

            Assert.AreEqual(after1, after2,
                $"{Path.GetFileName(assetPath)}: re-slicing UNCHANGED art rewrote the .meta. The slicer must " +
                "re-use the spriteID each existing slice name already carries (see " +
                "SpriteSheetSlicer.BuildRects / existingIds) and only GUID.Generate() for genuinely new " +
                "names. Without that, every run churns every manifest sheet as pure diff noise — and " +
                "RigBaker runs the slicer on every bake.");
        }

        [Test]
        [TestCaseSource(nameof(Sheets))]
        public void ReSlicing_KeepsEverySpriteId_AndTheRectSet(string assetPath)
        {
            // The .meta byte-compare above is the acceptance criterion; this one names WHICH field moved
            // when it goes red, so the failure is diagnosable without diffing YAML by hand. It also pins
            // the rect set (name → rect), which — unlike spriteID — really would break references.
            Assert.IsTrue(SpriteSheetSlicer.SliceSheet(assetPath), $"{assetPath}: first slice failed");
            var before = Snapshot(assetPath);
            Assert.IsNotEmpty(before, $"{assetPath}: the importer holds no sprite rects after a slice");

            Assert.IsTrue(SpriteSheetSlicer.SliceSheet(assetPath), $"{assetPath}: second slice failed");
            var after = Snapshot(assetPath);

            CollectionAssert.AreEqual(before.Keys, after.Keys, $"{assetPath}: the slice NAME set changed");
            foreach (var kv in before)
            {
                Assert.AreEqual(kv.Value.rect, after[kv.Key].rect, $"{kv.Key}: rect moved on a re-slice");
                Assert.AreEqual(kv.Value.id, after[kv.Key].id,
                    $"{kv.Key}: spriteID was regenerated on a re-slice — this is the churn the fix removes");
            }

            // Belt and braces: the imported Sprite objects must still be there and still match the rects
            // the importer holds, so this fixture cannot pass on an importer state that never imported.
            var imported = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
            Assert.AreEqual(after.Count, imported.Length,
                $"{assetPath}: importer holds {after.Count} rects but {imported.Length} sprites imported " +
                "(LoadAssetAtPath<Sprite> returns null for Multiple-mode sheets — this uses LoadAllAssetsAtPath)");
        }

        /// <summary>
        /// name → (rect, spriteID) straight from the importer's persisted sprite metadata — which IS what
        /// the <c>.meta</c> serializes. Read through <c>ISpriteEditorDataProvider</c>, never
        /// <c>TextureImporter.spritesheet</c> (obsolete-as-error on Unity 6000.5) and never via the
        /// imported Sprite's local file id, which is the <c>internalID</c> and is stable even when the
        /// <c>spriteID</c> churns — using it here would make this test vacuously green.
        /// </summary>
        private static System.Collections.Generic.SortedDictionary<string, (Rect rect, string id)> Snapshot(
            string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.IsNotNull(importer, $"{assetPath}: no TextureImporter");
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode,
                            $"{assetPath}: must be grid-sliced (Multiple) after a slice");

            var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            var map = new System.Collections.Generic.SortedDictionary<string, (Rect, string)>();
            foreach (var r in dp.GetSpriteRects())
                map[r.name] = (r.rect, r.spriteID.ToString());
            return map;
        }
    }
}
