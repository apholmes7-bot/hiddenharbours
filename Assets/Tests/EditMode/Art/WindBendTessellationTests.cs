using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Pins the geometry the wind shaders' bend curve DEPENDS ON, which nothing pinned before.
    ///
    /// <para><b>The defect this exists to stop coming back</b> (measured 2026-07-25).
    /// <c>HiddenHarboursTreeWind</c> shapes its sway as
    /// <c>bendW = smoothstep(_TrunkAnchor, 1, uv.y)^2</c> and <c>HiddenHarboursGrass</c> as
    /// <c>bendW = uv.y^2</c> — both in the VERTEX stage. A sprite imported as
    /// <see cref="SpriteMeshType.FullRect"/> is a <b>four-vertex quad</b>, so such an expression is
    /// only ever evaluated at <c>uv.y = 0</c> and <c>uv.y = 1</c> and the rasteriser interpolates
    /// LINEARLY between them. Every shaping term then does nothing at all: the squaring collapses
    /// (0^2 = 0, 1^2 = 1) and <c>_TrunkAnchor</c> cannot change <c>smoothstep(a,1,0) = 0</c> or
    /// <c>smoothstep(a,1,1) = 1</c> for any <c>a</c>. All 43 trees shipped that way, so the
    /// "trunk stays planted" promise in that shader's own header was not being kept — the whole
    /// sprite sheared from its bottom row, worst case 0.362 of full sway (~1.5 px) at mid-canopy.</para>
    ///
    /// <para>The grass tufts were always <see cref="SpriteMeshType.Tight"/> and so were always fine;
    /// that is the in-repo precedent this fix follows rather than invents.</para>
    ///
    /// <para><b>Why assert vertex HEIGHTS and not just the import flag.</b> Tight is the mechanism,
    /// not the requirement. What the shader actually needs is vertices at intermediate heights — and
    /// specifically at least one below the trunk anchor, or there is no row that can be held still
    /// while the crown moves. Asserting the capability keeps the test honest if Unity ever changes
    /// how it tessellates.</para>
    /// </summary>
    public class WindBendTessellationTests
    {
        private const string TreeDir = "Assets/_Project/Art/Sprites/Environment/Trees";

        /// <summary>Matches <c>_TrunkAnchor</c> in <c>Assets/_Project/Art/Materials/Tree.mat</c>.</summary>
        private const float TrunkAnchor = 0.14f;

        /// <summary>Sanity ceiling: a tessellated foliage sprite must not become a mesh-heavy object
        /// (rule 7). Measured headroom, not a tight fit — trip it and profile before raising it.</summary>
        private const int MaxVerticesPerSprite = 2000;

        private static string[] TreePaths() =>
            AssetDatabase.FindAssets("t:Texture2D", new[] { TreeDir })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(p => p.EndsWith(".png"))
                         .OrderBy(p => p)
                         .ToArray();

        [Test]
        public void TreeSprites_AreTessellated_SoTheBendCurveCanBeEvaluated()
        {
            string[] paths = TreePaths();
            Assert.That(paths, Is.Not.Empty, TreeDir + " has no sprites — did the folder move?");

            var flat = paths.Where(p =>
            {
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                return imp != null && imp.spriteImportMode != SpriteImportMode.None
                                   && GetMeshType(imp) == SpriteMeshType.FullRect;
            }).ToArray();

            Assert.That(flat, Is.Empty,
                "These tree sprites are FullRect, i.e. a 4-vertex quad. The wind shader's bend curve " +
                "is evaluated per VERTEX, so on a quad it collapses to a linear shear and _TrunkAnchor " +
                "does nothing — the trunk will not stay planted. Set Mesh Type to Tight:\n  " +
                string.Join("\n  ", flat));
        }

        [Test]
        public void TreeSprites_HaveAVertexBelowTheTrunkAnchor_SoTheBaseCanBeHeldStill()
        {
            foreach (string path in TreePaths())
            {
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                // ⚠️ These import as spriteMode Multiple; LoadAssetAtPath<Sprite> returns null.
                Assert.That(sprites, Is.Not.Empty, path + ": no Sprite sub-asset");

                foreach (Sprite s in sprites)
                {
                    Vector2[] uv = s.uv;
                    Assert.That(uv.Length, Is.LessThanOrEqualTo(MaxVerticesPerSprite),
                                s.name + ": " + uv.Length + " vertices — heavier than the budget allows");

                    // uv.y spans the sprite's rect within the sheet, so normalise against its own range.
                    float lo = uv.Min(v => v.y), hi = uv.Max(v => v.y);
                    Assert.That(hi - lo, Is.GreaterThan(1e-6f), s.name + ": degenerate UV range");

                    int distinctRows = uv.Select(v => Mathf.RoundToInt((v.y - lo) / (hi - lo) * 100f))
                                         .Distinct().Count();
                    Assert.That(distinctRows, Is.GreaterThan(2),
                                s.name + ": only " + distinctRows + " distinct vertex heights — a bend " +
                                "curve cannot be expressed with fewer than 3, it interpolates linearly");

                    bool anchored = uv.Any(v => (v.y - lo) / (hi - lo) < TrunkAnchor);
                    Assert.That(anchored, Is.True,
                                s.name + ": no vertex below the trunk anchor (" + TrunkAnchor + "), so " +
                                "there is no row the shader can hold still while the crown sways");
                }
            }
        }

        private static SpriteMeshType GetMeshType(TextureImporter imp)
        {
            TextureImporterSettings s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            return s.spriteMeshType;
        }
    }
}
