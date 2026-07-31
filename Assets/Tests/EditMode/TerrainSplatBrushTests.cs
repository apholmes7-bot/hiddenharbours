using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Material brush's PURE stroke math (ADR 0028 PR 2 addendum) — channel packing, falloff,
    /// flow, the dab, and the exclusive-painting contract — all headless (no scene, no assets), so
    /// the rules the owner paints by are pinned where CI can see them.
    /// </summary>
    public class TerrainSplatBrushTests
    {
        // ============================ CHANNEL PACKING ============================

        [Test]
        public void MaterialOrder_IsTheCanonicalSplatOrder()
        {
            // The one order everything shares: the shader's channel unpack (A.rgba B.rgba C.rg),
            // the pin tests, and every committed splat PNG. Append-only, never reorder.
            CollectionAssert.AreEqual(
                new[] { "Grass", "Marram", "Sand", "Shingle", "Ripple", "Shelf", "Silt", "Dirt", "Marsh", "Sedge" },
                TerrainSplatBrush.MaterialNames,
                "The brush's material order drifted from the canonical splat channel order.");
        }

        [Test]
        public void EveryBrushMaterial_ExistsInTheKitPackOrders()
        {
            foreach (string name in TerrainSplatBrush.MaterialNames)
                Assert.IsTrue(TerrainTexArrayBuilder.Order256.Contains(name) ||
                              TerrainTexArrayBuilder.Order512.Contains(name),
                    $"'{name}' is paintable but in neither TerrainTexArrayBuilder pack order — " +
                    "the shader would have no detail slices for it.");
        }

        [Test]
        public void ChannelPacking_RoundTripsForAllTenMaterials()
        {
            for (int m = 0; m < TerrainSplatBrush.MaterialCount; m++)
            {
                int tex = TerrainSplatBrush.TextureOf(m);
                int ch = TerrainSplatBrush.ChannelOf(m);
                Assert.That(tex, Is.InRange(0, 2), $"material {m} maps to texture {tex}.");
                Assert.That(ch, Is.InRange(0, 3), $"material {m} maps to channel {ch}.");
                Assert.AreEqual(m, TerrainSplatBrush.MaterialOf(tex, ch),
                    $"material {m} does not round-trip through (texture, channel).");
            }
            // Texture C carries only two channels (marsh, sedge) — the last valid material is 9.
            Assert.AreEqual(2, TerrainSplatBrush.TextureOf(TerrainSplatBrush.MaterialCount - 1));
            Assert.AreEqual(1, TerrainSplatBrush.ChannelOf(TerrainSplatBrush.MaterialCount - 1));
        }

        [Test]
        public void ChannelLabels_NameTheTextureAndChannel()
        {
            Assert.AreEqual("SplatA.r", TerrainSplatBrush.ChannelLabel(0));   // grass
            Assert.AreEqual("SplatA.a", TerrainSplatBrush.ChannelLabel(3));   // shingle
            Assert.AreEqual("SplatB.r", TerrainSplatBrush.ChannelLabel(4));   // ripple
            Assert.AreEqual("SplatC.g", TerrainSplatBrush.ChannelLabel(9));   // sedge
        }

        [Test]
        public void SplatAssetPaths_MatchTheBuilderWiring()
        {
            // StPetersBuilder loads these EXACT paths when wiring ConfigureSplat — the two
            // spellings must never drift.
            Assert.AreEqual("Assets/_Project/Data/Terrain/StPetersSplatA.png", TerrainSplatAssets.PathOf(0));
            Assert.AreEqual("Assets/_Project/Data/Terrain/StPetersSplatB.png", TerrainSplatAssets.PathOf(1));
            Assert.AreEqual("Assets/_Project/Data/Terrain/StPetersSplatC.png", TerrainSplatAssets.PathOf(2));
        }

        // ============================ FALLOFF + FLOW ============================

        [Test]
        public void Weight_IsOneAtCentre_ZeroAtRadius_AndMonotone()
        {
            Assert.AreEqual(1f, TerrainSplatBrush.Weight(0f, 4f, 0.5f), 1e-5f);
            Assert.AreEqual(0f, TerrainSplatBrush.Weight(4f, 4f, 0.5f), 1e-5f);
            Assert.AreEqual(0f, TerrainSplatBrush.Weight(9f, 4f, 0.5f), 1e-5f);

            float prev = 1f;
            for (float d = 0f; d <= 4f; d += 0.1f)
            {
                float w = TerrainSplatBrush.Weight(d, 4f, 0.5f);
                Assert.LessOrEqual(w, prev + 1e-5f, $"weight rose with distance at d={d}.");
                prev = w;
            }
        }

        [Test]
        public void Weight_FalloffZero_IsAHardStamp()
        {
            Assert.AreEqual(1f, TerrainSplatBrush.Weight(3.99f, 4f, 0f), 1e-4f,
                "with no falloff the whole footprint should be full weight");
            Assert.AreEqual(0f, TerrainSplatBrush.Weight(4.01f, 4f, 0f), 1e-4f);
        }

        [Test]
        public void Step_LerpsTowardTheTarget_AndClamps()
        {
            Assert.AreEqual(0.5f, TerrainSplatBrush.Step(0f, 1f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, TerrainSplatBrush.Step(0f, 1f, 5f), 1e-5f, "k must clamp at 1");
            Assert.AreEqual(0.2f, TerrainSplatBrush.Step(0.2f, 1f, 0f), 1e-5f, "k 0 must not move");
            // Repeated low-flow steps converge on the target.
            float v = 0f;
            for (int i = 0; i < 60; i++) v = TerrainSplatBrush.Step(v, 0.35f, 0.25f);
            Assert.AreEqual(0.35f, v, 1e-3f);
        }

        // ============================ THE DAB ============================

        private const int W = 16, H = 16;
        private static readonly Vector2 Min = new Vector2(0f, 0f);
        private static readonly Vector2 Size = new Vector2(16f, 16f);   // 1 m per texel

        private static (Color[] a, Color[] b, Color[] c) Blank()
            => (new Color[W * H], new Color[W * H], new Color[W * H]);

        private static int CentreIdx(Vector2 world)
        {
            int x = Mathf.RoundToInt((world.x - Min.x) / Size.x * W - 0.5f);
            int y = Mathf.RoundToInt((world.y - Min.y) / Size.y * H - 0.5f);
            return y * W + x;
        }

        [Test]
        public void Dab_PaintsTheChannelTowardTheTarget_AtFullFlow()
        {
            var (a, b, c) = Blank();
            var centre = new Vector2(8f, 8f);
            TerrainSplatBrush.Dab(a, b, c, W, H, Min, Size, centre, 3f, 0.5f,
                material: 7 /* dirt → B.a */, target: 0.35f, flow: 1f, exclusive: true);

            Assert.AreEqual(0.35f, b[CentreIdx(centre)].a, 1e-3f, "centre must reach the target");
            Assert.AreEqual(0f, a[CentreIdx(centre)].r, 1e-5f, "no other channel painted");
            Assert.AreEqual(0f, b[CentreIdx(new Vector2(1f, 1f))].a, 1e-5f, "outside the radius untouched");
        }

        [Test]
        public void Dab_Exclusive_FadesTheOtherChannels_NonExclusiveStacks()
        {
            var centre = new Vector2(8f, 8f);

            var (a, b, c) = Blank();
            for (int i = 0; i < b.Length; i++) b[i].b = 0.8f;   // pre-painted silt everywhere
            TerrainSplatBrush.Dab(a, b, c, W, H, Min, Size, centre, 3f, 0.5f,
                material: 7, target: 0.4f, flow: 1f, exclusive: true);
            Assert.AreEqual(0f, b[CentreIdx(centre)].b, 1e-4f,
                "exclusive painting at full flow must fully replace the other material at the centre");
            Assert.AreEqual(0.8f, b[CentreIdx(new Vector2(1f, 1f))].b, 1e-5f,
                "silt outside the footprint untouched");

            (a, b, c) = Blank();
            for (int i = 0; i < b.Length; i++) b[i].b = 0.8f;
            TerrainSplatBrush.Dab(a, b, c, W, H, Min, Size, centre, 3f, 0.5f,
                material: 7, target: 0.4f, flow: 1f, exclusive: false);
            Assert.AreEqual(0.8f, b[CentreIdx(centre)].b, 1e-5f,
                "non-exclusive painting must leave the other material alone (the shader renormalises)");
        }

        [Test]
        public void Dab_EraseAll_FadesEveryChannel()
        {
            var (a, b, c) = Blank();
            var centre = new Vector2(8f, 8f);
            for (int i = 0; i < a.Length; i++) { a[i] = new Color(0.5f, 0.4f, 0.3f, 0.2f); c[i].g = 0.6f; }

            TerrainSplatBrush.Dab(a, b, c, W, H, Min, Size, centre, 3f, 0.5f,
                TerrainSplatBrush.EraseAllMaterials, target: 0f, flow: 1f, exclusive: false);

            int idx = CentreIdx(centre);
            Assert.AreEqual(0f, a[idx].r, 1e-4f);
            Assert.AreEqual(0f, a[idx].a, 1e-4f);
            Assert.AreEqual(0f, c[idx].g, 1e-4f, "erase-all must clear texture C too");
            Assert.AreEqual(0.6f, c[CentreIdx(new Vector2(1f, 1f))].g, 1e-5f, "outside untouched");
        }

        [Test]
        public void Dab_Erase_LowersOnlyTheSelectedChannel()
        {
            var (a, b, c) = Blank();
            var centre = new Vector2(8f, 8f);
            for (int i = 0; i < a.Length; i++) { a[i].r = 0.7f; a[i].g = 0.5f; }

            // Erase = target 0 on the selected channel, exclusive off (the tool's Erase mode).
            TerrainSplatBrush.Dab(a, b, c, W, H, Min, Size, centre, 3f, 0.5f,
                material: 0 /* grass */, target: 0f, flow: 1f, exclusive: false);

            int idx = CentreIdx(centre);
            Assert.AreEqual(0f, a[idx].r, 1e-4f, "grass erased at the centre");
            Assert.AreEqual(0.5f, a[idx].g, 1e-5f, "marram untouched by a grass erase");
        }

        [Test]
        public void Dab_IsDeterministic()
        {
            var (a1, b1, c1) = Blank();
            var (a2, b2, c2) = Blank();
            var centre = new Vector2(7.3f, 9.1f);
            TerrainSplatBrush.Dab(a1, b1, c1, W, H, Min, Size, centre, 4f, 0.7f, 8, 0.5f, 0.6f, true);
            TerrainSplatBrush.Dab(a2, b2, c2, W, H, Min, Size, centre, 4f, 0.7f, 8, 0.5f, 0.6f, true);
            CollectionAssert.AreEqual(a1, a2);
            CollectionAssert.AreEqual(b1, b2);
            CollectionAssert.AreEqual(c1, c2);
        }

        [Test]
        public void PaintPolyline_CoversTheWholeLine_AndIsDeterministic()
        {
            var (a1, b1, c1) = Blank();
            // Vertices on texel CENTRES (x.5) so the coverage asserts sample the line itself.
            var pts = new[] { new Vector2(2.5f, 3.5f), new Vector2(9.5f, 4.5f), new Vector2(13.5f, 11.5f) };
            TerrainSplatBrush.PaintPolyline(a1, b1, c1, W, H, Min, Size, pts,
                dabSpacingMetres: 0.75f, radiusMetres: 1.25f, falloff01: 0.5f,
                material: 7, target: 0.35f, exclusive: true);

            // Every vertex (and the midpoints of each segment) got paint in the dirt channel.
            foreach (var p in pts)
                Assert.Greater(b1[CentreIdx(p)].a, 0.2f, $"no paint at vertex {p}");
            Assert.Greater(b1[CentreIdx((pts[0] + pts[1]) * 0.5f)].a, 0.2f, "gap mid-segment 1");
            Assert.Greater(b1[CentreIdx((pts[1] + pts[2]) * 0.5f)].a, 0.2f, "gap mid-segment 2");

            var (a2, b2, c2) = Blank();
            TerrainSplatBrush.PaintPolyline(a2, b2, c2, W, H, Min, Size, pts,
                0.75f, 1.25f, 0.5f, 7, 0.35f, true);
            CollectionAssert.AreEqual(b1, b2, "the polyline stroke is not deterministic");
        }
    }
}
