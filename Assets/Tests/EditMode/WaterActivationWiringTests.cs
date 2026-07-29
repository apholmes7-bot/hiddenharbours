#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Art;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The ADR 0027 ACTIVATION wiring, pinned where it is actually made — in the BUILDERS.
    ///
    /// <para><b>Why these tests exist at all.</b> The lesson #323 taught this repo the hard way:
    /// <b>scene-wired is not builder-wired</b>. A committed scene here is builder OUTPUT, so a feature
    /// activated by hand-editing a scene survives exactly until the next builder run erases it — and
    /// nothing fails, the effect just quietly stops being there. Every assertion below therefore reads
    /// what a BUILDER produced, never what a scene happens to contain.</para>
    ///
    /// <para>Pure editor-object construction: no scene is loaded and none is written. That matters
    /// twice over here, because the committed <c>Greybox.unity</c> carries no logic tree at all — the
    /// cove's terrain does not exist until a builder run makes it, which is exactly why the cove's
    /// seabed is baked from the builder's own constants rather than from a scene.</para>
    /// </summary>
    public class WaterActivationWiringTests
    {
        // ===== the cove's baked seabed (ADR 0027 #7) ======================================

        [Test]
        public void CoveSeabedBake_IsCommitted_AndImportedAsAPointClampedColourTexture()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GreyboxBuilder.CoveSeabedTex);
            Assert.IsNotNull(tex,
                $"the cove's seabed bake is missing at {GreyboxBuilder.CoveSeabedTex}. Re-bake it with " +
                "Hidden Harbours ▸ Art ▸ Bake Cove Seabed (it needs no scene open).");
            Assert.AreEqual(GreyboxBuilder.CoveSeabedResolution, tex.width,
                "the committed bake must match the resolution the builder documents its budget against.");
            Assert.AreEqual(GreyboxBuilder.CoveSeabedResolution, tex.height);

            var importer = AssetImporter.GetAtPath(GreyboxBuilder.CoveSeabedTex) as TextureImporter;
            Assert.IsNotNull(importer);
            // POINT + CLAMP is the pixel-art contract: bilinear would smear one bottom cell into the
            // next, and Repeat would tile a whole second coastline in from off-rect.
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            // sRGB ON — unlike the height map beside it, these bytes really are colour.
            Assert.IsTrue(importer.sRGBTexture, "a seabed albedo is colour, not linear data.");
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression,
                "block compression would mottle a bottom the absorption then bands.");
        }

        [Test]
        public void SeabedPalette_DarkensWithDepth_AndIsNotTheHypsometricOverlayRamp()
        {
            // The bottom must get DARKER as it deepens, or the absorbed result reads as noise rather
            // than as depth.
            float prev = 2f;
            foreach (float e in new[] { 2f, 0.6f, -0.2f, -0.8f, -2f, -4f })
            {
                Color c = SeabedPalette.Albedo(e);
                float luma = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                Assert.Less(luma, prev, $"the bottom must darken with depth (elevation {e}).");
                prev = luma;
            }

            // ⚠️ And it must NOT be the designer's hypsometric ramp. That one paints the SHALLOWS
            // CYAN-BLUE because it is showing you how deep something is; used as a seabed albedo it
            // paints the water's own colour onto the bottom, the blue doubles up, and absorption —
            // whose entire job is taking red out of a warm bottom — has nothing left to take. A
            // seabed is sand, silt and rock; the blue is the water's job.
            foreach (float e in new[] { -2f, -0.8f, -0.2f, 0.6f })
            {
                Color bed = SeabedPalette.Albedo(e);
                Assert.GreaterOrEqual(bed.r, bed.b,
                    $"a seabed is never bluer than it is red (elevation {e}) — that is the water's job.");
            }
        }

        // ===== the reflective set (ADR 0027 #8) ==========================================

        [Test]
        public void TreePrefabs_CarryAReflectiveObject_SoAPaintedTreelineReflects()
        {
            // The hand-drawn set. DecorPrefabBuilder gives these the TreeWind material, which is the
            // one that carries the HHReflect pass — so they can actually draw a reflection, unlike the
            // props and buildings on the default sprite material.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Decor/Trees/Tree01.prefab");
            if (prefab == null)
                Assert.Ignore("Tree01.prefab is not built in this checkout — run Build Decor Prefabs.");
            Assert.IsNotNull(prefab.GetComponent<ReflectiveObject>(),
                "a tree prefab must carry a ReflectiveObject, or a painted treeline reflects nothing. " +
                "Wire it in DecorPrefabBuilder — NOT by hand on the prefab, which the next build erases.");
        }

        [Test]
        public void ReflectiveObject_OnAFreshRenderer_PublishesItsPivotAndJoinsTheLayer()
        {
            // The contract every builder-added reflector relies on, asserted against a renderer rather
            // than against the builder call — because "the component reached the prefab" and "the
            // pivot reached the shader" are different claims, and only the second one matters.
            var go = new GameObject("Reflector");
            try
            {
                go.transform.position = new Vector3(3f, 7f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                go.AddComponent<ReflectiveObject>().Refresh();

                Vector4 origin = ReflectiveObject.OriginOn(sr);
                Assert.AreEqual(1f, origin.w, 1e-5f, "w = 1 is the shader's 'a pivot was published' flag.");
                Assert.AreEqual(3f, origin.x, 1e-4f);
                Assert.AreEqual(7f, origin.y, 1e-4f);
                Assert.IsTrue(ReflectiveObject.IsInReflectionLayer(sr));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void CoveTerrain_ShoalsFromTheDeepFloorUpToTheLand_SoTheBakeHasSomethingToShow()
        {
            // The bake is only worth committing if the terrain under it actually varies. This is the
            // same terrain BakeCoveSeabed builds, from the same builder call — so a change that
            // flattened the cove would redden here rather than silently produce a one-colour bottom.
            var go = new GameObject("~probe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var terrain = go.AddComponent<RectTidalTerrain>();
                GreyboxBuilder.ConfigureCoveTerrain(terrain);

                float land = terrain.ElevationAt(GreyboxBuilder.CoveLandCenter);
                float deep = terrain.ElevationAt(new Vector2(0f, -25f));
                Assert.Greater(land, deep + 2f,
                    "the cove must still shoal from its deep floor to its land, or the seabed bake is " +
                    "one flat colour and absorption has nothing to reveal.");

                Color landBed = SeabedPalette.Albedo(land);
                Color deepBed = SeabedPalette.Albedo(deep);
                Assert.Greater(landBed.r, deepBed.r + 0.2f,
                    "and the two must bake to visibly different bottoms.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ===== the shared-material trap (the one that would black out St Peters) ==========

        [Test]
        public void ASurfaceWithNoBake_PublishesACLEARSeabed_NotTheMaterialsOpaqueBlackDefault()
        {
            // ⚠️ THE TRAP. _USE_SEABEDTEX is a MATERIAL keyword and Water.mat is SHARED by every
            // region, so turning it on for the cove turns the shader block on for St Peters too. A
            // surface with no bake that pushed nothing would sample the material's default "black"
            // texture — opaque black, alpha 1 — and composite a BLACK BOTTOM across its shallows.
            // Pushing an explicit transparent 1x1 makes "no bake" mean coverage 0, which composites
            // nothing.
            var go = new GameObject("~sea") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var surface = go.AddComponent<WaterSurface>();
                surface.ConfigureSeabedTexture(null);

                Assert.IsNull(surface.SeabedTexture, "no bake wired is the state under test.");
                var block = new MaterialPropertyBlock();
                sr.GetPropertyBlock(block);
                Texture pushed = block.GetTexture("_SeabedTex");
                Assert.IsNotNull(pushed,
                    "a surface with no bake must still PUBLISH something — leaving the slot unbound is " +
                    "what falls through to the material's opaque black.");
                Assert.AreEqual(1, pushed.width, "the fallback is a 1x1.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ConfigureSeabedTexture_ReachesTheRenderer_NotJustTheField()
        {
            var go = new GameObject("~sea") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var surface = go.AddComponent<WaterSurface>();
                var bake = AssetDatabase.LoadAssetAtPath<Texture2D>(GreyboxBuilder.CoveSeabedTex);
                if (bake == null) Assert.Ignore("the cove bake is not present in this checkout.");

                surface.ConfigureSeabedTexture(bake);
                Assert.AreSame(bake, surface.SeabedTexture);

                var block = new MaterialPropertyBlock();
                sr.GetPropertyBlock(block);
                Assert.AreSame(bake, block.GetTexture("_SeabedTex"),
                    "the bake must reach the RENDERER — the builder setting a field the shader never " +
                    "sees is exactly the failure this asserts against.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ===== the judgeable defaults =====================================================

        [Test]
        public void WaterMaterial_ShipsWithBothFeaturesVisiblyOn()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Materials/Water.mat");
            Assert.IsNotNull(mat);
            Assert.AreEqual(1f, mat.GetFloat("_UseSeabedTex"), 1e-5f,
                "absorption is dormant without the keyword, and this PR's whole purpose is a look the " +
                "owner can judge.");
            Assert.IsTrue(mat.IsKeywordEnabled("_USE_SEABEDTEX"),
                "the float and the KEYWORD must agree — the float alone changes nothing, because the " +
                "shader block is compiled out by the keyword.");
            Assert.Greater(mat.GetFloat("_ObjectReflectStrength"), 0f,
                "object reflections are dormant at 0.");
            Assert.Greater(mat.GetFloat("_Turbidity"), 0f,
                "sigma 0 skips the absorption block entirely, so the bake would never be seen.");
        }
    }
}
#endif
