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

        // The owner (11-26): "you would think the deck around the lighthouse windows would be glowing a bit from the
        // ambient light". The spill is data next to the glass, strongest by the glass, gone within a metre or two.
        [Test]
        public void TheSpill_IsData_StrongestByTheGlass_AndGoneWithinAMetreOrTwo()
        {
            LightStationDef def = Def();
            Assert.That(def.GlassSpill, Is.Not.Empty, "the deck round the lit glass catches its light");
            Assert.That(def.GlassSpillStrength, Is.GreaterThan(0f));
            Assert.That(def.GlassSpillStrength, Is.LessThan(def.GlassIntensity * 0.5f), "a soft spill, never as bright as the glass");
            Assert.That(def.GlassSpillBands, Is.GreaterThanOrEqualTo(2), "stepped tones, not one flat block");
            float sill = def.GlassPanes.Min(p => p.yMin), head = def.GlassPanes.Max(p => p.yMax);
            float left = def.GlassPanes.Min(p => p.xMin), right = def.GlassPanes.Max(p => p.xMax);
            float ppm = LoadLightStationContract().pixelsPerMetre;
            foreach (Vector4 s in def.GlassSpill)
            {
                // The gallery deck starts at the lantern room's foot, under its lower glazing: up to a metre under the
                // lit panes' sill, never above it (the light falls on the deck, not on the sky beside the glass).
                Assert.That(s.z, Is.InRange(sill - 1f, sill), $"span {s} is strongest right under the glass");
                float below = sill - (s.z - s.w);
                Assert.That(below, Is.InRange(1f, 2.5f), $"span {s} fades out a metre or two under the glass");
                Assert.That(s.x, Is.LessThan(left)); Assert.That(s.y, Is.GreaterThan(right));
                foreach (float edge in new[] { s.x, s.y, s.z, s.w })
                    Assert.That(edge * ppm, Is.EqualTo(Mathf.Round(edge * ppm)).Within(1e-3f), $"span {s} lies on whole art pixels");
            }
        }

        [Test]
        public void TheSpill_IsDrawnInThePaneModesSpillShape_OverTheNight_AndAbsentWithoutSpans()
        {
            var go = new GameObject("spill under test");
            try
            {
                go.transform.position = new Vector3(206.5f, 58.8f, 0f);
                var glow = go.AddComponent<LanternGlassGlow>();
                glow.Configure(new[] { new Rect(-0.375f, 8.0625f, 0.75f, 0.40625f) }, new Color(0.9f, 0.8f, 0.5f, 1f), 0.9f, 0.75f, 1f, 0.6f, 0.35f, 3);
                glow.ConfigureSpill(new[] { new Vector4(-2f, 2f, 8.46875f, 2.46875f) }, 0.22f, 0.3f, true, 3);
                glow.Rebuild();

                Mesh mesh = glow.SpillMesh;
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.EqualTo(4), "one quad a span");
                Vector3[] v = mesh.vertices;
                Assert.That((Vector2)v[0], Is.EqualTo(new Vector2(206.5f - 2f, 58.8f + 8.46875f - 2.46875f)));
                Assert.That((Vector2)v[2], Is.EqualTo(new Vector2(206.5f + 2f, 58.8f + 8.46875f)));
                var sizes = new System.Collections.Generic.List<Vector2>();
                mesh.GetUVs(1, sizes);
                Assert.That(sizes[0], Is.EqualTo(new Vector2(4f, 2.46875f) * GlassLook.Current.PixelsPerMetre),
                    "the span is cut on its own art pixels");

                MeshRenderer r = glow.SpillRenderer;
                Assert.That(r, Is.Not.Null);
                Material shared = SceneLight.SharedLightMaterial();
                if (shared == null) Assert.Ignore("no light shader in this editor");
                Assert.That(r.sharedMaterial, Is.SameAs(shared), "the lamps' own material: gated on the night, off by day");
                Assert.That(r.enabled, Is.True);
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_PaneMode"), Is.EqualTo(1f));
                Assert.That(block.GetFloat("_Intensity"), Is.EqualTo(0.22f).Within(1e-6f));
                Vector4 look = block.GetVector("_PaneLook");
                Assert.That(look.z, Is.EqualTo(0f), "no hot centre: it is light on a deck, not glass");
                Assert.That(look.w, Is.EqualTo(3f));
                Assert.That(block.GetVector("_PaneLook2").w, Is.EqualTo(LanternGlassGlow.SpillShapeMode(true)),
                    "the half-ellipse with its last band dithered, not a plain pane");
                Assert.That(block.GetVector("_PaneLook2").x, Is.EqualTo(0f), "the plain pane's side falloff is not used");
                Assert.That(block.GetFloat("_InWorld"), Is.EqualTo(0f), "over the night tint, so each step adds its own level");
                Assert.That(r.sortingOrder, Is.EqualTo(SceneLight.MaxSortingOrder));

                glow.ConfigureSpill(new[] { new Vector4(-2f, 2f, 8.46875f, 2.46875f) }, 0.22f, 0.3f, false, 3);
                r.GetPropertyBlock(block);
                Assert.That(block.GetVector("_PaneLook2").w, Is.EqualTo(LanternGlassGlow.SpillShapeMode(false)));

                glow.ConfigureSpill(new Vector4[0], 0.22f, 0.3f, true, 3);
                Assert.That(r.enabled, Is.False, "no spans, no spill drawn");
                Assert.That(glow.GlassRenderer.enabled, Is.True, "the glass still burns");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheSpillsShape_IsAStepCurveUnderTheGlass_NeverABox()
        {
            // The coordinator's read of the first plate: "a hard-edged rectangle the width of the tower, with a square
            // bottom edge". The spill must fade out before its span's sides and end on curved, stepped edges.
            // Checked on the station's own span, cut on its art pixels, through the shader's C# mirror.
            LightStationDef def = Def();
            float ppm = LoadLightStationContract().pixelsPerMetre;
            int bands = def.GlassSpillBands;
            foreach (Vector4 s in def.GlassSpill)
            {
                int w = Mathf.RoundToInt((s.y - s.x) * ppm), h = Mathf.RoundToInt(s.w * ppm);
                Assert.That(w, Is.GreaterThan(4)); Assert.That(h, Is.GreaterThan(4));
                float L(int cx, int cy) => LanternGlassGlow.SpillLevel(cx, cy, w, h, def.GlassSpillPeakDepth, bands, def.GlassSpillDitherLastBand);

                // Full at its peak, just under the lantern room's foot (the upper part of the span).
                int peakRow = Mathf.Clamp(Mathf.FloorToInt((1f - def.GlassSpillPeakDepth) * h), 0, h - 1);
                Assert.That(peakRow, Is.GreaterThanOrEqualTo(h / 2), $"span {s}: it peaks in the span's upper half, by the glass");
                Assert.That(L(w / 2, peakRow), Is.EqualTo(1f), $"span {s}: full right under the glass");

                // No vertical hard side: the outermost columns are dark but for a row or two at the peak's height.
                int sideLit = 0;
                for (int cy = 0; cy < h; cy++) { if (L(0, cy) > 0f) sideLit++; if (L(w - 1, cy) > 0f) sideLit++; }
                Assert.That(sideLit, Is.LessThanOrEqualTo(2 * Mathf.CeilToInt(h / 8f)), $"span {s}: its sides are not a lit edge");

                // No square bottom and no straight top: on the foot row only the middle third is lit, on the top row
                // only the middle half.
                for (int cx = 0; cx < w; cx++)
                {
                    float off = Mathf.Abs(cx + 0.5f - w * 0.5f);
                    if (off > w / 6f) Assert.That(L(cx, 0), Is.EqualTo(0f), $"span {s}: the foot row is dark away from the middle at {cx}");
                    if (off > w / 4f) Assert.That(L(cx, h - 1), Is.EqualTo(0f), $"span {s}: the top row is dark away from the middle at {cx}");
                }

                // It only ever falls away from its peak: outward along the peak's row, and up and down the middle.
                float last = 2f;
                for (int cx = w / 2; cx < w; cx++) { float l = LanternGlassGlow.SpillLevel(cx, peakRow, w, h, def.GlassSpillPeakDepth, bands, false); Assert.That(l, Is.LessThanOrEqualTo(last)); last = l; }
                last = 2f;
                for (int cy = peakRow; cy >= 0; cy--) { float l = LanternGlassGlow.SpillLevel(w / 2, cy, w, h, def.GlassSpillPeakDepth, bands, false); Assert.That(l, Is.LessThanOrEqualTo(last)); last = l; }
                last = 2f;
                for (int cy = peakRow; cy < h; cy++) { float l = LanternGlassGlow.SpillLevel(w / 2, cy, w, h, def.GlassSpillPeakDepth, bands, false); Assert.That(l, Is.LessThanOrEqualTo(last)); last = l; }

                // Stepped tones: exactly the bands and dark, nothing between.
                var levels = new System.Collections.Generic.HashSet<float>();
                for (int cx = 0; cx < w; cx++) for (int cy = 0; cy < h; cy++) levels.Add(L(cx, cy));
                Assert.That(levels.Count, Is.EqualTo(bands + 1), $"span {s}: one tone a band, then dark");

                // The dithered last band never lights two side-by-side cells.
                if (def.GlassSpillDitherLastBand)
                    for (int cx = 0; cx + 1 < w; cx++) for (int cy = 0; cy < h; cy++)
                        Assert.That(L(cx, cy) == 1f / bands && L(cx + 1, cy) == 1f / bands, Is.False,
                            $"span {s}: the last band is thinned at ({cx},{cy})");
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
