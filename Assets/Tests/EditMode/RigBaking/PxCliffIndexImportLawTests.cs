using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// 🔴 <b>THE px <c>_index</c> IMPORT LAW</b>, held on the importer settings the baker itself writes
    /// (<see cref="CliffBaker.ApplyImportSettings"/>) and on the slot move that hands a wall's GUID from
    /// v10's <c>_unlit</c> to the px <c>_index</c> (<see cref="CliffBaker.ClaimSlot"/>).
    ///
    /// <para><b>Why it is a law.</b> <c>_index</c> is not a colour. R is a LUT row, G a band, B the rock
    /// flag — integers the shader reads back as <c>floor(x × 255 + 0.5)</c>. sRGB bends them (a row
    /// comes back as another rock's), a bilinear filter or a mip averages two rows into a third,
    /// compression quantises them, and a premultiply zeroes them wherever alpha is short of 255. Each
    /// reads on screen as "the cliff is the wrong colour in patches", never as an error. The bake is
    /// gitignored and CI never runs it, so the law is asserted HERE, on scratch faces, through the same
    /// two calls the bake makes.</para>
    ///
    /// <para><b>Two ways in, both covered.</b> A fresh px bake writes <c>_index.png</c> new, and its first
    /// import is stamped by <see cref="ArtImportPipeline"/> as sprite colour art — sRGB on,
    /// alpha-is-transparency on, clamped. A px bake over a v10 kit MOVES <c>_unlit.png</c> to
    /// <c>_index.png</c>, and the importer settings travel with the <c>.meta</c>: sRGB on, as colour.
    /// Either way only the settings pass makes it data, and each test reads what the importer holds
    /// once that pass has run.</para>
    /// </summary>
    public class PxCliffIndexImportLawTests
    {
        // Under the art root on purpose: that is where the bake writes, so the art pipeline's
        // first-import stamp is the starting state here too.
        const string Parent = "Assets/_Project/Art";
        const string Leaf = "_CliffPxImportLawScratch";
        const string Scratch = Parent + "/" + Leaf;
        const string Rock = "sandstone", Aspect = "S";
        const int Batter = 0;

        static string Unlit => CliffBaker.FaceAssetPath(Scratch, Rock, Aspect, Batter, CliffCatalog.UnlitChannel);
        static string Index => CliffBaker.FaceAssetPath(Scratch, Rock, Aspect, Batter, CliffCatalog.IndexChannel);

        [SetUp]
        public void MakeScratch()
        {
            if (AssetDatabase.IsValidFolder(Scratch)) AssetDatabase.DeleteAsset(Scratch);
            AssetDatabase.CreateFolder(Parent, Leaf);
            AssetDatabase.CreateFolder(Scratch, "Faces");
        }

        [TearDown]
        public void DeleteScratch()
        {
            if (AssetDatabase.IsValidFolder(Scratch)) AssetDatabase.DeleteAsset(Scratch);
        }

        // =====================================================================================
        //  the law
        // =====================================================================================

        [Test]
        public void AFreshIndexFileImportsAsDataOnceTheBakerHasTunedIt()
        {
            WritePng(Index);
            Assert.IsTrue(Importer(Index).sRGBTexture,
                "Premise: the art pipeline's first-import stamp treats a new _index.png as colour. If it " +
                "now imports it as data, the settings pass is no longer the only thing between the file " +
                "and the screen — re-read this suite, do not just delete this line.");

            Assert.IsTrue(CliffBaker.ApplyImportSettings(Index, CliffAssetKind.Face, CliffCatalog.IndexChannel),
                "The settings pass left a freshly imported _index untouched.");
            AssertTheIndexLaw(Index);
            Assert.IsFalse(CliffBaker.ApplyImportSettings(Index, CliffAssetKind.Face, CliffCatalog.IndexChannel),
                "A second settings pass changed something: the pass must settle in one go, or every px " +
                "bake reimports the whole kit twice.");
        }

        /// <summary>
        /// ⭐ Route (b): the px bake MOVES the v10 colour file into the <c>_index</c> slot so the walls keep
        /// the GUID they were built with — no rebuild, no scene change. The move carries v10's colour
        /// settings; the pass must turn the file into data.
        /// </summary>
        [Test]
        public void AV10FaceMovedIntoTheIndexSlotKeepsItsGuidAndImportsAsData()
        {
            WritePng(Unlit);
            CliffBaker.ApplyImportSettings(Unlit, CliffAssetKind.Face, CliffCatalog.UnlitChannel);
            string guid = AssetDatabase.AssetPathToGUID(Unlit);
            Assert.IsNotEmpty(guid);

            CliffBaker.ClaimSlot(Scratch, Rock, Aspect, Batter, px: true);

            Assert.IsFalse(CliffBaker.OnDisk(Unlit), "The v10 file is still on disk after the claim.");
            Assert.IsTrue(CliffBaker.OnDisk(Index), "Nothing arrived in the _index slot.");
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(Index),
                "The claim minted a NEW GUID. Every wall built on the v10 look references the face by its " +
                "GUID, so they would all point at nothing — the move exists to keep it.");
            Assert.IsTrue(Importer(Index).sRGBTexture,
                "Premise: a moved asset keeps its .meta, so the file arrives imported as colour.");

            Assert.IsTrue(CliffBaker.ApplyImportSettings(Index, CliffAssetKind.Face, CliffCatalog.IndexChannel),
                "The settings pass left a moved face imported as colour.");
            AssertTheIndexLaw(Index);
            Assert.IsFalse(CliffBaker.ApplyImportSettings(Index, CliffAssetKind.Face, CliffCatalog.IndexChannel));
        }

        /// <summary>The v10 menu item moves it back, GUID and all, and the pass makes it colour again.</summary>
        [Test]
        public void AFaceMovedBackToTheV10SlotImportsAsColourAgain()
        {
            WritePng(Unlit);
            CliffBaker.ApplyImportSettings(Unlit, CliffAssetKind.Face, CliffCatalog.UnlitChannel);
            string guid = AssetDatabase.AssetPathToGUID(Unlit);
            CliffBaker.ClaimSlot(Scratch, Rock, Aspect, Batter, px: true);
            CliffBaker.ApplyImportSettings(Index, CliffAssetKind.Face, CliffCatalog.IndexChannel);

            CliffBaker.ClaimSlot(Scratch, Rock, Aspect, Batter, px: false);

            Assert.IsFalse(CliffBaker.OnDisk(Index));
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(Unlit), "The move back minted a new GUID.");
            Assert.IsTrue(CliffBaker.ApplyImportSettings(Unlit, CliffAssetKind.Face, CliffCatalog.UnlitChannel));

            TextureImporter ti = Importer(Unlit);
            Assert.IsTrue(ti.sRGBTexture, "v10's _unlit is colour: sRGB must be back ON.");
            Assert.AreEqual(FilterMode.Point, ti.filterMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, ti.textureCompression);
            Assert.IsFalse(ti.mipmapEnabled);
        }

        [Test]
        public void AClaimWithNothingToMoveMovesNothing()
        {
            Assert.DoesNotThrow(() => CliffBaker.ClaimSlot(Scratch, Rock, Aspect, Batter, px: true),
                "A fresh clone has neither file; the bake writes the slot new.");
            Assert.IsFalse(CliffBaker.OnDisk(Index));

            WritePng(Index);
            string guid = AssetDatabase.AssetPathToGUID(Index);
            CliffBaker.ClaimSlot(Scratch, Rock, Aspect, Batter, px: true);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(Index), "A slot already held was disturbed.");
            Assert.IsFalse(CliffBaker.OnDisk(Unlit));
        }

        /// <summary>
        /// 🔴 A face set holding BOTH files is refused, whole, before anything moves: only one of the two
        /// carries the GUID the walls mean, and nothing on disk says which.
        /// </summary>
        [Test]
        public void ASetHoldingBothSlotsIsRefusedAndNothingMoves()
        {
            WritePng(Unlit);
            WritePng(Index);
            string unlitGuid = AssetDatabase.AssetPathToGUID(Unlit), indexGuid = AssetDatabase.AssetPathToGUID(Index);

            var e = Assert.Throws<InvalidOperationException>(
                () => CliffBaker.AssertSlotsUnambiguous(Scratch, new[] { Rock }));
            StringAssert.Contains(Unlit, e.Message, "The refusal must name both files.");
            StringAssert.Contains(Index, e.Message, "The refusal must name both files.");

            Assert.Throws<InvalidOperationException>(
                () => CliffBaker.ClaimSlot(Scratch, Rock, Aspect, Batter, px: true));
            Assert.Throws<InvalidOperationException>(
                () => CliffBaker.ClaimSlot(Scratch, Rock, Aspect, Batter, px: false));

            Assert.AreEqual(unlitGuid, AssetDatabase.AssetPathToGUID(Unlit));
            Assert.AreEqual(indexGuid, AssetDatabase.AssetPathToGUID(Index));
        }

        // =====================================================================================
        //  the palette
        // =====================================================================================

        /// <summary>
        /// The LUT is COLOUR (the pixel artist's ramps; sRGB on, like every sprite) but it is addressed
        /// like data: point-sampled at cell centres, and clamped — a wrapping LUT bleeds band 4 into
        /// band 0 at the column edge, and a repeating V would sample the padding rows.
        /// </summary>
        [Test]
        public void ThePaletteImportsAsColourPointSampledAndClamped()
        {
            string lut = $"{Scratch}/CliffPx_palette.png";
            WritePng(lut);

            Assert.IsTrue(CliffBaker.ApplyImportSettings(lut, CliffAssetKind.Palette, ""));

            TextureImporter ti = Importer(lut);
            Assert.AreEqual(TextureImporterType.Default, ti.textureType, "The LUT is sampled by a shader, not drawn.");
            Assert.IsTrue(ti.sRGBTexture, "The LUT holds the art-director's colours: sRGB ON.");
            Assert.AreEqual(FilterMode.Point, ti.filterMode, "Bilinear would blend two bands, or two rocks.");
            Assert.AreEqual(TextureWrapMode.Clamp, ti.wrapModeU, "wrap U");
            Assert.AreEqual(TextureWrapMode.Clamp, ti.wrapModeV, "wrap V");
            Assert.IsFalse(ti.mipmapEnabled, "A mip of a LUT is a different LUT.");
            Assert.AreEqual(TextureImporterCompression.Uncompressed, ti.textureCompression);
            Assert.AreEqual(TextureImporterNPOTScale.None, ti.npotScale);
            Assert.IsFalse(ti.alphaIsTransparency);
            Assert.AreEqual(TextureImporterAlphaSource.FromInput, ti.alphaSource);
            Assert.IsFalse(CliffBaker.ApplyImportSettings(lut, CliffAssetKind.Palette, ""));
        }

        // =====================================================================================
        //  helpers
        // =====================================================================================

        /// <summary>The law, as literals — never read back from the catalog it guards.</summary>
        static void AssertTheIndexLaw(string path)
        {
            TextureImporter ti = Importer(path);
            Assert.IsFalse(ti.sRGBTexture, "_index holds integers: sRGB must be OFF or every row and band bends.");
            Assert.AreEqual(FilterMode.Point, ti.filterMode, "_index must be Point: a filter averages rows.");
            Assert.IsFalse(ti.mipmapEnabled, "_index must have no mips: a mip averages cells.");
            Assert.AreEqual(TextureImporterCompression.Uncompressed, ti.textureCompression,
                "_index must be uncompressed: compression quantises the rows.");
            Assert.IsFalse(ti.alphaIsTransparency,
                "_index must not be premultiplied: alpha-is-transparency rewrites the RGB it bleeds into.");
            Assert.AreEqual(TextureImporterAlphaSource.FromInput, ti.alphaSource, "_index alpha comes from the file.");
            Assert.AreEqual(TextureImporterNPOTScale.None, ti.npotScale,
                "_index must keep its 384 × 288: a rescale resamples the integers.");
            Assert.AreEqual(TextureImporterType.Default, ti.textureType, "A face is a texture, not a sprite.");
            Assert.AreEqual(TextureWrapMode.Repeat, ti.wrapModeU, "A face tiles along the shore.");
            Assert.AreEqual(TextureWrapMode.Repeat, ti.wrapModeV, "A face tiles up the wall.");
            Assert.IsFalse(ti.isReadable, "Only the profile is read by the CPU.");
        }

        static TextureImporter Importer(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(ti, $"No texture importer at {path}.");
            return ti;
        }

        /// <summary>A small RGBA file with an _index's shape of content: small integers in R and G,
        /// the rock flag in B, full alpha.</summary>
        static void WritePng(string assetPath)
        {
            var tex = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[8 * 4];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32((byte)(i % 25), (byte)(i % 5), (byte)(i % 2 == 0 ? 255 : 0), 255);
                tex.SetPixels32(pixels);
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, assetPath), tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
