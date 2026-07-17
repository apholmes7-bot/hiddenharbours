using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The three shipped flower materials, held to THE ART rather than to themselves.
    ///
    /// <para>This fixture exists because of a specific, repeated failure on this project: a committed .asset/.mat
    /// deserialises any field it omits to that field's ZERO value, and a test that compares the material against a
    /// constant copied out of the same material passes vacuously while the shipped asset is dead. So the anchor
    /// here is the SHEETS ON DISK: <c>_Cols</c> and <c>_Rows</c> are checked against the real pixel dimensions of
    /// the real PNGs. Get them wrong and the shader bends the flower about the wrong line and samples the wrong
    /// pose — silently, and looking merely "a bit off" rather than broken.</para>
    /// </summary>
    public class FlowerMaterialTests
    {
        private static IEnumerable<FlowerCatalog.Tier> Tiers =>
            (FlowerCatalog.Tier[])System.Enum.GetValues(typeof(FlowerCatalog.Tier));

        [Test]
        public void EveryTier_ShipsAMaterial_OnTheFlowerShader()
        {
            foreach (var tier in Tiers)
            {
                var mat = FlowerCatalog.MaterialFor(tier);
                Assert.IsNotNull(mat, $"No material at {FlowerCatalog.MaterialPathFor(tier)} — the {tier} flowers " +
                                      "would build with no wind material at all.");
                Assert.AreEqual("HiddenHarbours/FlowerWind", mat.shader.name,
                    $"{tier}'s material is on the wrong shader; it would not read the shared wind.");
            }
        }

        /// <summary>
        /// The one that would have caught a silent no-op: _Cols/_Rows are measured off the real sheets, not
        /// copied from the material. Sabotage either number in the .mat and this fails.
        /// </summary>
        [Test]
        public void EveryTiersColsAndRows_MatchTheRealSheetsPixelDimensions()
        {
            foreach (var tier in Tiers)
            {
                var mat = FlowerCatalog.MaterialFor(tier);
                Assert.IsNotNull(mat);

                // Measure the grid straight off a real sheet of this tier: how many cells fit across and down.
                string stem = FlowerCatalog.SheetStems()
                    .FirstOrDefault(s => FlowerCatalog.TrySplit(s, out _, out var t) && t == tier);
                Assert.IsNotNull(stem, $"No {tier} sheet on disk — this assertion would prove nothing.");

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FlowerCatalog.SheetPath(stem));
                Assert.IsNotNull(tex);
                var slices = AssetDatabase.LoadAllAssetsAtPath(FlowerCatalog.SheetPath(stem))
                                          .OfType<Sprite>().ToArray();
                Assert.IsNotEmpty(slices, $"'{stem}' has no slices — cannot measure the grid.");

                int measuredCols = Mathf.RoundToInt(tex.width / slices[0].rect.width);
                int measuredRows = Mathf.RoundToInt(tex.height / slices[0].rect.height);

                Assert.AreEqual(measuredCols, Mathf.RoundToInt(mat.GetFloat("_Cols")),
                    $"{tier}'s material says _Cols={mat.GetFloat("_Cols")} but '{stem}' really has " +
                    $"{measuredCols} columns. The shader's pose select would land off-cell.");
                Assert.AreEqual(measuredRows, Mathf.RoundToInt(mat.GetFloat("_Rows")),
                    $"{tier}'s material says _Rows={mat.GetFloat("_Rows")} but '{stem}' really has " +
                    $"{measuredRows} rows. The shader's cell-local bend weight would hinge about the wrong line — " +
                    "the exact bug that makes flowers slide out of the ground.");
            }
        }

        /// <summary>
        /// Every float the shader declares must be PRESENT in the committed .mat. A missing one reads back 0 —
        /// and a silent 0 for, say, _Cols would divide the sheet into one column and freeze every flower on one
        /// pose. This is the #212 class of bug, generalised.
        /// </summary>
        [Test]
        public void EveryTiersMaterial_WritesEveryShaderFloat_NoneLeftToDeserialiseAsZero()
        {
            foreach (var tier in Tiers)
            {
                var mat = FlowerCatalog.MaterialFor(tier);
                Assert.IsNotNull(mat);

                var so = new SerializedObject(mat);
                var floats = so.FindProperty("m_SavedProperties.m_Floats");
                var written = new HashSet<string>();
                for (int i = 0; i < floats.arraySize; i++)
                    written.Add(floats.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue);

                var missing = new List<string>();
                int count = mat.shader.GetPropertyCount();
                for (int i = 0; i < count; i++)
                {
                    var type = mat.shader.GetPropertyType(i);
                    if (type != UnityEngine.Rendering.ShaderPropertyType.Float &&
                        type != UnityEngine.Rendering.ShaderPropertyType.Range) continue;
                    string name = mat.shader.GetPropertyName(i);
                    if (!written.Contains(name)) missing.Add(name);
                }

                Assert.IsEmpty(missing,
                    $"{FlowerCatalog.MaterialPathFor(tier)} does not write these shader floats, so they " +
                    $"deserialise to ZERO on a fresh checkout: {string.Join(", ", missing)}. Write them into " +
                    "the committed .mat.");
            }
        }

        /// <summary>
        /// The Patch decision, pinned. A Patch is flat ground cover on a CENTRE pivot seen near-top-down —
        /// hinging it at its bottom edge reads as a broken flap. So Patch drifts as a whole sprite
        /// (_RootedBend 0) while the stems hinge at their root (_RootedBend 1). If someone "tidies" the Patch
        /// material to match the others, this says why not.
        /// </summary>
        [Test]
        public void PatchDriftsWholeSprite_WhileStemsHingeAtTheirRoot()
        {
            Assert.AreEqual(1f, FlowerCatalog.MaterialFor(FlowerCatalog.Tier.Single).GetFloat("_RootedBend"), 1e-4f,
                "A Single is a slim stem on a bottom-centre pivot: it must hinge at its root.");
            Assert.AreEqual(1f, FlowerCatalog.MaterialFor(FlowerCatalog.Tier.Clump).GetFloat("_RootedBend"), 1e-4f,
                "A Clump is a tuft of stems on a bottom-centre pivot: it must hinge at its root.");
            Assert.AreEqual(0f, FlowerCatalog.MaterialFor(FlowerCatalog.Tier.Patch).GetFloat("_RootedBend"), 1e-4f,
                "A Patch is flat ground cover on a CENTRE pivot. Hinging it at its bottom edge would read as a " +
                "broken flap — it must drift as a whole sprite. The art agrees: the Patch's 4 drawn columns are a " +
                "whole-sprite lateral shimmer, not a stem bend.");
        }

        [Test]
        public void EveryTier_KeepsThePixelArtGrid()
        {
            foreach (var tier in Tiers)
                Assert.AreEqual(32f, FlowerCatalog.MaterialFor(tier).GetFloat("_PixelsPerUnit"), 1e-4f,
                    $"{tier}'s bend must snap to the project's PPU 32 grid or the pixel art shimmers.");
        }
    }
}
