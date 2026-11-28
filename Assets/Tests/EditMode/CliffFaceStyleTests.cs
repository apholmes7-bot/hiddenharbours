using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards for the CLIFF FACE STYLE (pass 2 of the cliff mix: the boards' coursed rock as real faces, not only a
    /// palette). Not the look (the owner's eye owns that; CI has no GPU): the data it stands on. The style Def exists
    /// with a stable id, holds one face for every kit sandstone face St Peters draws (five aspects, three batters),
    /// every face is committed with the kit's face import contract, and the board skin draws with it.
    /// </summary>
    public class CliffFaceStyleTests
    {
        const string StylesFolder = "Assets/_Project/Data/Terrain/CliffStyles";
        const string BoardStyleId = "cliffstyle.board_coursed";
        const string BoardSkinPath = "Assets/_Project/Data/Terrain/CliffSkins/CliffSkin_BoardBedded.asset";
        const string KitFaces = "Assets/_Project/Art/Terrain/Cliffs/Faces/";

        /// <summary>Five aspects times three batters: the kit faces a St Peters band can name.</summary>
        const int FacesPerRock = 15;

        static readonly Regex IdRx = new Regex("^cliffstyle\\.[a-z0-9_]+$");

        static List<CliffFaceStyleDef> Styles() =>
            AssetDatabase.FindAssets("t:" + nameof(CliffFaceStyleDef), new[] { StylesFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<CliffFaceStyleDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null).ToList();

        static CliffFaceStyleDef Board() => Styles().FirstOrDefault(s => s.Id == BoardStyleId);

        [Test]
        public void EveryStyle_HasAStableId_OneAFile()
        {
            List<CliffFaceStyleDef> styles = Styles();
            Assert.That(styles.Select(s => s.Id), Does.Contain(BoardStyleId));
            Assert.AreEqual(styles.Count, styles.Select(s => s.Id).Distinct().Count(), "two styles share an id");
            foreach (CliffFaceStyleDef s in styles)
                Assert.IsTrue(IdRx.IsMatch(s.Id), $"'{s.Id}' is not cliffstyle.snake_case");
        }

        [Test]
        public void TheBoardStyle_HasAFace_ForEveryKitSandstoneFace_WithAllThreeChannels()
        {
            CliffFaceStyleDef style = Board();
            Assert.IsNotNull(style, "no cliffstyle.board_coursed asset");
            Assert.AreEqual(FacesPerRock, style.Faces.Count, "the style does not cover every aspect and batter");
            var bases = new HashSet<Texture2D>();
            foreach (CliffStyleFace f in style.Faces)
            {
                Assert.IsTrue(f.HasChannels, "a style face is missing a channel (re-run Bake Cliff Face Styles)");
                string basePath = AssetDatabase.GetAssetPath(f.Base);
                StringAssert.StartsWith(KitFaces + "Sandstone_", basePath, "a style face stands in for something not a kit sandstone face");
                StringAssert.EndsWith("_index.png", basePath, "a style face must key on the kit face's colour slot (_index)");
                Assert.IsTrue(bases.Add(f.Base), $"two style faces stand in for {f.Base.name}");

                // The style face is the SAME place in the kit: Sandstone_S_S76_index -> SandstoneCoursed_S_S76_index.
                string tail = f.Base.name.Substring(f.Base.name.IndexOf('_'));
                Assert.AreEqual(style.FilePrefix + tail, f.Index.name);
                Assert.AreEqual(style.FilePrefix + tail.Replace("_index", "_normal"), f.Normal.name);
                Assert.AreEqual(style.FilePrefix + tail.Replace("_index", "_mask"), f.Mask.name);
                foreach (Texture2D t in new[] { f.Index, f.Normal, f.Mask })
                {
                    StringAssert.StartsWith(style.OutputFolder + "/", AssetDatabase.GetAssetPath(t), $"{t.name} is not where the baker writes");
                    Assert.AreEqual(f.Base.width, t.width, $"{t.name} is not the kit face's size");
                    Assert.AreEqual(f.Base.height, t.height, $"{t.name} is not the kit face's size");
                    Assert.AreEqual(FilterMode.Point, t.filterMode, $"{t.name} is filtered: rows and bands would bleed");
                    Assert.AreEqual(TextureWrapMode.Repeat, t.wrapModeU, $"{t.name} does not wrap along the shore");
                    Assert.AreEqual(1, t.mipmapCount, $"{t.name} has mips: the index would be resampled");
                }
            }
        }

        [Test]
        public void TheBoardSkin_DrawsWithTheBoardStyle_AndALookupFindsEveryFace()
        {
            var skin = AssetDatabase.LoadAssetAtPath<CliffFaceSkinDef>(BoardSkinPath);
            Assert.IsNotNull(skin, "no board skin");
            Assert.IsNotNull(skin.FaceStyle, "the board skin draws no face style: pass 2 is a palette only");
            Assert.AreEqual(BoardStyleId, skin.FaceStyle.Id);
            foreach (CliffStyleFace f in skin.FaceStyle.Faces)
            {
                Assert.IsTrue(skin.FaceStyle.TryGetFace(f.Base, out CliffStyleFace got));
                Assert.AreSame(f.Index, got.Index);
            }
            Assert.IsFalse(skin.FaceStyle.TryGetFace(null, out _), "a band with no face found a style face");
        }

        [Test]
        public void TheOptions_AreAllData_AndReachThePainterAsOneObject()
        {
            CliffFaceStyleDef style = Board();
            Assert.IsNotNull(style);
            string json = style.OptionsJson();
            foreach (string key in new[] { "course", "pale", "paleTh", "jsp", "jgate", "step", "seg", "proud", "relief", "lip", "speck", "sunk", "stain", "tick", "part", "chip", "paleTone", "silt", "siltTh", "siltTone", "recess", "spall", "spallCells", "fissure", "fissureLen", "jag", "mottle", "mottleCells" })
                StringAssert.Contains($"\"{key}\":", json, $"the painter's '{key}' is not passed from the Def");
            StringAssert.StartsWith("{", json);
            StringAssert.EndsWith("}", json);
            StringAssert.DoesNotContain(",}", json);
            Assert.LessOrEqual(style.CourseTexels.x, style.CourseTexels.y);
            Assert.LessOrEqual(style.JointSpacingTexels.x, style.JointSpacingTexels.y);
            Assert.LessOrEqual(style.PaleTexels.x, style.PaleTexels.y);
            Assert.LessOrEqual(style.SiltBeds.x, style.SiltBeds.y);
            Assert.LessOrEqual(style.SiltTexels.x, style.SiltTexels.y);
        }

        [Test]
        public void TheBoardSkin_DrawsACoolSiltBed_OnEveryFace()
        {
            var skin = AssetDatabase.LoadAssetAtPath<CliffFaceSkinDef>(BoardSkinPath);
            Assert.IsNotNull(skin);
            Assert.GreaterOrEqual(skin.FaceStyle.SiltBeds.x, 1, "the board style must draw at least one silt bed a face");
            int si = System.Array.FindIndex(skin.Accessories, a => a.Name == "silt");
            Assert.GreaterOrEqual(si, 0, "the board skin does not move the silt row, so its silt beds would read in the kit's grey-green");
            CliffSkinRock silt = skin.Accessories[si];
            Assert.IsTrue(ColorUtility.TryParseHtmlString(silt.BaseHex, out Color c), silt.BaseHex);
            Assert.Greater(c.b, c.r, "the board's silt bed is cool blue-grey: its blue must beat its red");
        }
    }
}
