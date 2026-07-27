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
    ///
    /// <para><b>The anchor is a POSITION, so it is measured in absolute atlas uv</b> (fixed
    /// 2026-07-26). As first shipped, the anchor assert normalised <c>uv.y</c> against each sprite's
    /// own min/max and then asked for a vertex below the anchor — but the lowest vertex maps to
    /// exactly 0 by construction, and <c>0 &lt; anchor</c> holds for every positive anchor, so that
    /// assert could not fail on any input. It was decoration. What the shader reads is the RAW atlas
    /// coordinate — <c>smoothstep(_TrunkAnchor, 1, saturate(IN.uv.y))</c>,
    /// <c>HiddenHarboursTreeWind.shader:156</c> — never a per-sprite fraction, so that is the
    /// quantity asserted here. The equivalent guard on the PR #298 tree kit
    /// (<c>TreeSheetImportTests.TheAnchorLine_ActuallyCutsEverySpritesMesh…</c>) is the reference
    /// this now matches.</para>
    ///
    /// <para><b>Measured sabotage 2026-07-26</b> (PR #299), so this assert's threshold is a number
    /// somebody checked rather than a claim. Sweeping a bottom-padding regression on Tree41
    /// (171×244) through Unity's real Tight mesher, the OLD predicate passed at ALL ten sampled
    /// pads — including 64 px, where the mesh floor sits at uv.y 0.2541 and nothing is anchored at
    /// all. The version here flips PASS→FAIL between a mesh floor of 0.1393 and 0.1557, bracketing
    /// <c>_TrunkAnchor</c> 0.14 exactly. End-to-end on a two-cell re-slice, the upper cell reports
    /// its floor 0.3600 above the anchor (88 px), swaying at 0.1436 of full amplitude instead of 0.
    /// Note the amplitude cost just past the threshold is small (~1e-6): the defect this catches is
    /// STRUCTURAL — no genuinely planted row — and grows with the gap. All 43 sheets currently clear
    /// it by the maximum possible margin, reaching uv.y 0.0000.</para>
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
            string[] paths = TreePaths();
            Assert.That(paths, Is.Not.Empty, TreeDir + " has no sprites — did the folder move?");

            float highestFloor = float.MinValue;
            string tightest = null;

            foreach (string path in paths)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(tex, Is.Not.Null, path + ": no Texture2D");

                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                // ⚠️ These import as spriteMode Multiple; LoadAssetAtPath<Sprite> returns null.
                Assert.That(sprites, Is.Not.Empty, path + ": no Sprite sub-asset");

                foreach (Sprite s in sprites)
                {
                    Vector2[] uv = s.uv;
                    Assert.That(uv, Is.Not.Empty, s.name + ": no UVs");
                    Assert.That(uv.Length, Is.LessThanOrEqualTo(MaxVerticesPerSprite),
                                s.name + ": " + uv.Length + " vertices — heavier than the budget allows");

                    // PRECONDITION for reading uv.y as a height. The shader samples the RAW atlas
                    // coordinate, so atlas uv.y only means "fraction of the tree's height" while a
                    // sheet holds ONE sprite spanning the whole texture — which is how all 43 of these
                    // hand-drawn trees are cut (one sprite, rect (0,0,w,h)). If that ever stops being
                    // true, the SHADER breaks before this test does: a cell in the upper row of a
                    // two-row sheet has uv.y ≥ 0.5 everywhere, so its own trunk foot lands far above
                    // _TrunkAnchor and sways with the canopy. One material cannot hold a per-cell
                    // anchor, so the answer is to keep one sprite per sheet (the PR #298 kit bakes ONE
                    // sway row for exactly this reason) — not to normalise this assert.
                    bool spansSheet = s.rect.x == 0f && s.rect.y == 0f
                                   && s.rect.width == tex.width && s.rect.height == tex.height;
                    Assert.That(spansSheet, Is.True,
                                s.name + ": rect " + s.rect + " does not span the whole " + tex.width +
                                "×" + tex.height + " texture, so atlas uv.y is no longer this tree's " +
                                "own height fraction. The wind shader reads raw uv.y and cannot know " +
                                "the difference — re-cut the sheet to one sprite, or give the cell its " +
                                "own material/_TrunkAnchor.");

                    float lo = uv.Min(v => v.y), hi = uv.Max(v => v.y);
                    Assert.That(hi - lo, Is.GreaterThan(1e-6f), s.name + ": degenerate UV range");

                    // A COUNT of distinct heights is invariant to where the rect sits in the atlas, so
                    // normalising is safe here — and only here. This is the assert that catches a
                    // FullRect 4-vertex quad (PR #297).
                    int distinctRows = uv.Select(v => Mathf.RoundToInt((v.y - lo) / (hi - lo) * 100f))
                                         .Distinct().Count();
                    Assert.That(distinctRows, Is.GreaterThan(2),
                                s.name + ": only " + distinctRows + " distinct vertex heights — a bend " +
                                "curve cannot be expressed with fewer than 3, it interpolates linearly");

                    // The anchor line must genuinely SPLIT the mesh: a row to hold still AND a row to
                    // move. Measured in absolute atlas uv, the same number the shader samples.
                    int below = uv.Count(v => v.y < TrunkAnchor);
                    Assert.That(below, Is.GreaterThan(0),
                                s.name + ": no vertex below the trunk anchor (" + TrunkAnchor + "), so " +
                                "there is no row the shader can hold still while the crown sways. The " +
                                "mesh spans uv.y " + lo.ToString("F4") + ".." + hi.ToString("F4") +
                                " — its lowest vertex sits " + (lo - TrunkAnchor).ToString("F4") +
                                " ABOVE the anchor (" + Mathf.RoundToInt((lo - TrunkAnchor) * tex.height) +
                                " px), where the shader already sways it at " +
                                BendWeight(lo).ToString("F4") + " of full amplitude instead of 0.");
                    Assert.That(below, Is.LessThan(uv.Length),
                                s.name + ": EVERY one of its " + uv.Length + " vertices is below the " +
                                "anchor (" + TrunkAnchor + ") — the whole tree is planted and nothing " +
                                "would sway at all. The mesh spans uv.y " + lo.ToString("F4") + ".." +
                                hi.ToString("F4") + ".");

                    if (lo > highestFloor) { highestFloor = lo; tightest = s.name; }
                }
            }

            // The margin this test is actually holding. Print it: an assert whose headroom nobody has
            // measured is how the vacuous version of this check shipped in the first place.
            Debug.Log("[tree-anchor] closest to the anchor is " + tightest + ", whose lowest vertex " +
                      "sits at atlas uv.y " + highestFloor.ToString("F4") + " — " +
                      (TrunkAnchor - highestFloor).ToString("F4") + " of headroom below _TrunkAnchor " +
                      TrunkAnchor + ". Measured 2026-07-26 across " + paths.Length + " sheets.");
        }

        /// <summary>Mirrors <c>HiddenHarboursTreeWind.shader:156-157</c> exactly:
        /// <c>bendW = smoothstep(_TrunkAnchor, 1, saturate(uv.y))</c>, then squared. Used only to say
        /// how badly a failure sways — but it has to be the shader's own curve to be worth printing.
        /// ⚠️ NOT <see cref="Mathf.SmoothStep"/>: Unity's interpolates BETWEEN from and to, whereas
        /// HLSL's returns the 0..1 position of x between the two edges.</summary>
        private static float BendWeight(float uvY)
        {
            float t = Mathf.Clamp01((Mathf.Clamp01(uvY) - TrunkAnchor) / (1f - TrunkAnchor));
            float w = t * t * (3f - 2f * t);
            return w * w;
        }

        private static SpriteMeshType GetMeshType(TextureImporter imp)
        {
            TextureImporterSettings s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            return s.spriteMeshType;
        }
    }
}
