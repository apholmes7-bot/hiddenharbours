using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The north-east light's lantern glass, lit. The owner (11-26): "make the lighthouse glasses lenses glow as
    /// well". The glass is drawn the way a lit cabin's windows are (the shared light material in its pane mode,
    /// gated on the night in the shader), its panes are where the tower picture draws its glass, and it is not
    /// another lamp: the station's lamps are unchanged.
    /// </summary>
    public class LanternGlassGlowTests
    {
        const string DefPath = LightStationFolder + "/LightStation_stp_ne_light.asset";
        const byte OpaqueAlpha = 200;

        static LightStationDef Def()
        {
            var def = AssetDatabase.LoadAssetAtPath<LightStationDef>(DefPath);
            Assert.That(def, Is.Not.Null, "no light station at " + DefPath);
            return def;
        }

        [Test]
        public void TheGlass_IsData_AndLiesOnTheLanternTheTowerPictureDraws()
        {
            LightStationDef def = Def();
            Assert.That(def.GlassPanes, Is.Not.Empty, "the lantern has glass to light");
            Assert.That(def.GlassIntensity, Is.GreaterThan(0f));
            // The hot core: somewhere in a pane the shader's level passes the 0.6 it starts whitening at.
            Assert.That(def.GlassIntensity * (Mathf.Max(def.GlassHeadLevel, def.GlassSillLevel) + def.GlassCentreLift),
                Is.GreaterThan(1f), "the middle of a pane burns toward white");

            LightStationContract contract = LoadLightStationContract();
            var tower = contract.parts.Single(p => p.name == "tower");
            var tex = new Texture2D(2, 2);
            try
            {
                Assert.That(tex.LoadImage(File.ReadAllBytes(LightStationSprites + "/" + tower.sprite)), Is.True);
                Color32[] px = tex.GetPixels32();
                float ppm = contract.pixelsPerMetre;
                float pivotX = tower.pivot.x * tex.width, pivotY = tower.pivot.y * tex.height;
                float lantern = contract.lantern.riseMetres;
                foreach (Rect r in def.GlassPanes)
                {
                    Assert.That(r.width, Is.GreaterThan(0f)); Assert.That(r.height, Is.GreaterThan(0f));
                    Assert.That(r.center.y, Is.EqualTo(lantern).Within(1f), $"pane {r} is up at the lantern, not on the tower's shaft");
                    // Every art pixel the pane covers is drawn (opaque) in the tower picture: the glass is on the glass.
                    int x0 = Mathf.RoundToInt(pivotX + r.xMin * ppm), x1 = Mathf.RoundToInt(pivotX + r.xMax * ppm);
                    int y0 = Mathf.RoundToInt(pivotY + r.yMin * ppm), y1 = Mathf.RoundToInt(pivotY + r.yMax * ppm);
                    Assert.That(x1 - x0, Is.EqualTo(Mathf.RoundToInt(r.width * ppm)), "pane edges land on whole art pixels");
                    Assert.That(y1 - y0, Is.EqualTo(Mathf.RoundToInt(r.height * ppm)));
                    int bare = 0;
                    for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                        if (px[y * tex.width + x].a < OpaqueAlpha) bare++;
                    Assert.That(bare, Is.Zero, $"pane {r} reaches off the drawn lantern ({bare} bare pixels)");
                }
                for (int i = 0; i < def.GlassPanes.Length; i++)
                for (int j = i + 1; j < def.GlassPanes.Length; j++)
                    Assert.That(def.GlassPanes[i].Overlaps(def.GlassPanes[j]), Is.False, "two panes never draw over each other");
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void TheGlass_IsDrawnAsLitPanes_GatedOnTheNight_AndSortedOverItsTower()
        {
            var go = new GameObject("glass under test");
            try
            {
                go.transform.position = new Vector3(206.5f, 58.8f, 0f);
                var glow = go.AddComponent<LanternGlassGlow>();
                var panes = new[] { new Rect(-0.375f, 8.0625f, 0.75f, 0.40625f), new Rect(0.4375f, 8.0625f, 0.5f, 0.40625f) };
                glow.Configure(panes, new Color(0.9f, 0.8f, 0.5f, 1f), 0.9f, 0.75f, 1f, 0.6f, 0.35f, 3);
                glow.Rebuild();

                Mesh mesh = glow.GlassMesh;
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.EqualTo(8), "four corners a pane");
                Vector3[] v = mesh.vertices;
                Assert.That((Vector2)v[0], Is.EqualTo(new Vector2(206.5f - 0.375f, 58.8f + 8.0625f)));
                Assert.That((Vector2)v[2], Is.EqualTo(new Vector2(206.5f + 0.375f, 58.8f + 8.0625f + 0.40625f)));
                var sizes = new System.Collections.Generic.List<Vector2>();
                mesh.GetUVs(1, sizes);
                float ppm = GlassLook.Current.PixelsPerMetre;
                Assert.That(sizes[0], Is.EqualTo(panes[0].size * ppm), "each pane knows its drawn size in art pixels");
                Assert.That(mesh.bounds.Contains(new Vector3(206.5f, 58.8f + 8.2f, 0f)), Is.True, "bounds hold the glass (never culled)");

                MeshRenderer r = glow.GlassRenderer;
                Assert.That(r, Is.Not.Null);
                Material shared = SceneLight.SharedLightMaterial();
                if (shared == null) Assert.Ignore("no light shader in this editor");
                Assert.That(r.sharedMaterial, Is.SameAs(shared), "drawn by the lamps' own material: gated on the night in its shader");
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_PaneMode"), Is.EqualTo(1f), "drawn as lit panes, not a round glow");
                Assert.That(block.GetFloat("_Intensity"), Is.EqualTo(0.9f));
                Assert.That(block.GetVector("_PaneLook").z, Is.EqualTo(0.6f), "the lens's core is the pane's centre lift");
                if (GlassLook.Current.SortsInWorld)
                    Assert.That(r.sortingOrder, Is.EqualTo(LanternGlassGlow.OrderInWorld(58.8f)), "one rung over the tower it is set in");
                Assert.That(go.GetComponentsInChildren<SceneLight>(true), Is.Empty, "the glass is not another lamp");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheShaderGate_IsOffByDay_AndOnAtNight()
        {
            // The pane mode multiplies by the shader's NightGate, the C# copy of which is LightMath's; its ramp runs
            // from no light at a bright frame to full at a dark one.
            float day = LightMath.NightGate(1f, 0.12f, 0.35f), night = LightMath.NightGate(0.05f, 0.12f, 0.35f);
            Assert.That(day, Is.EqualTo(0f).Within(1e-4f), "by day the glass is dark");
            Assert.That(night, Is.GreaterThan(0.9f), "at night it burns");
        }
    }
}
