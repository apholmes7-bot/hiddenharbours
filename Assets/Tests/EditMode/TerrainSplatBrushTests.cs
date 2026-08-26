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
            // The one order everything shares: the shader's channel unpack (A.rgba B.rgba C.rgba
            // D.rgba E.rg), the pin tests, and every committed splat PNG. Append-only, never reorder.
            CollectionAssert.AreEqual(
                new[]
                {
                    "Grass", "Marram", "Sand", "Shingle", "Ripple", "Shelf", "Silt",
                    "Dirt", "Marsh", "Sedge", "Foreshore", "Talus", "Ledge", "Rockweed",
                    "Musselbed", "Oysterreef", "Eelgrass", "Irishmoss",
                    "Lawn",
                },
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
        public void ChannelPacking_RoundTripsForEveryMaterial()
        {
            for (int m = 0; m < TerrainSplatBrush.MaterialCount; m++)
            {
                int tex = TerrainSplatBrush.TextureOf(m);
                int ch = TerrainSplatBrush.ChannelOf(m);
                Assert.That(tex, Is.InRange(0, TerrainSplatBrush.TextureCount - 1),
                    $"material {m} maps to texture {tex}, outside the {TerrainSplatBrush.TextureCount} maps.");
                Assert.That(ch, Is.InRange(0, 3), $"material {m} maps to channel {ch}.");
                Assert.AreEqual(m, TerrainSplatBrush.MaterialOf(tex, ch),
                    $"material {m} does not round-trip through (texture, channel).");
            }
            // Texture E carries only two channels (eelgrass, irishmoss) — the last valid material
            // is 18 (Lawn, at E.b), so E.a is the ONE slot left free.
            Assert.AreEqual(4, TerrainSplatBrush.TextureOf(TerrainSplatBrush.MaterialCount - 1));
            Assert.AreEqual(2, TerrainSplatBrush.ChannelOf(TerrainSplatBrush.MaterialCount - 1));
        }

        [Test]
        public void KitV2Materials_LandOnTheChannelsTheHandoffPromised()
        {
            // The four new families' channels, stated as the PR body states them. If the append
            // order ever shifts, the owner's painted ground changes meaning — fail here first.
            Assert.AreEqual("SplatC.b", TerrainSplatBrush.ChannelLabel(10), "Foreshore");
            Assert.AreEqual("SplatC.a", TerrainSplatBrush.ChannelLabel(11), "Talus");
            Assert.AreEqual("SplatD.r", TerrainSplatBrush.ChannelLabel(12), "Ledge");
            Assert.AreEqual("SplatD.g", TerrainSplatBrush.ChannelLabel(13), "Rockweed");

            Assert.AreEqual("Foreshore", TerrainSplatBrush.MaterialNames[10]);
            Assert.AreEqual("Talus", TerrainSplatBrush.MaterialNames[11]);
            Assert.AreEqual("Ledge", TerrainSplatBrush.MaterialNames[12]);
            Assert.AreEqual("Rockweed", TerrainSplatBrush.MaterialNames[13]);
        }

        [Test]
        public void KitV3Beds_LandOnTheChannelsTheHandoffPromised()
        {
            // The reef beds took D's two free slots and opened E. Stated as the PR body states it:
            // if this append order ever shifts, every committed splat PNG changes meaning per
            // channel and St Peters repaints itself silently.
            Assert.AreEqual("SplatD.b", TerrainSplatBrush.ChannelLabel(14), "Musselbed");
            Assert.AreEqual("SplatD.a", TerrainSplatBrush.ChannelLabel(15), "Oysterreef");
            Assert.AreEqual("SplatE.r", TerrainSplatBrush.ChannelLabel(16), "Eelgrass");
            Assert.AreEqual("SplatE.g", TerrainSplatBrush.ChannelLabel(17), "Irishmoss");
            Assert.AreEqual("SplatE.b", TerrainSplatBrush.ChannelLabel(18), "Lawn");

            Assert.AreEqual("Musselbed", TerrainSplatBrush.MaterialNames[14]);
            Assert.AreEqual("Oysterreef", TerrainSplatBrush.MaterialNames[15]);
            Assert.AreEqual("Eelgrass", TerrainSplatBrush.MaterialNames[16]);
            Assert.AreEqual("Irishmoss", TerrainSplatBrush.MaterialNames[17]);
            Assert.AreEqual("Lawn", TerrainSplatBrush.MaterialNames[18]);
        }

        [Test]
        public void TheTwoFreeSlots_AreAtTheEnd_SoTheNextAppendCannotCollide()
        {
            // 19 materials in 20 channels. MaterialOf() is only meaningful below MaterialCount, so
            // spell out WHICH two are spare — a future kit appending at 18 must land on E.b.
            Assert.AreEqual(18, TerrainSplatBrush.MaterialOf(4, 2), "E.b should be the next slot (18).");
            Assert.AreEqual(19, TerrainSplatBrush.MaterialOf(4, 3), "E.a should be the last slot (19).");
            // ⭐ SAID DELIBERATELY, 2026-08-26: Lawn took E.b, so ONE channel is free, not two. The
            // next material after it needs a SIXTH splat map — every region's committed PNGs, the
            // surface's binding and the byte-zero gate all move — so this number going to 0 is a
            // decision somebody has to make on purpose, which is what this assertion is for.
            Assert.AreEqual(TerrainSplatBrush.TextureCount * 4 - 1, TerrainSplatBrush.MaterialCount,
                "Exactly ONE channel should be free — if that changed, say so deliberately.");
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
            // StPetersBuilder loads these EXACT paths when wiring ConfigureSplat (through PathOf,
            // so the builder cannot spell them a second way) — pin the spelling itself here.
            Assert.AreEqual("Assets/_Project/Data/Terrain/StPetersSplatA.png", TerrainSplatAssets.PathOf(0));
            Assert.AreEqual("Assets/_Project/Data/Terrain/StPetersSplatB.png", TerrainSplatAssets.PathOf(1));
            Assert.AreEqual("Assets/_Project/Data/Terrain/StPetersSplatC.png", TerrainSplatAssets.PathOf(2));
            Assert.AreEqual("Assets/_Project/Data/Terrain/StPetersSplatD.png", TerrainSplatAssets.PathOf(3));
            Assert.AreEqual("Assets/_Project/Data/Terrain/StPetersSplatE.png", TerrainSplatAssets.PathOf(4));
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

        /// <summary>One blank buffer per splat map — sized from TextureCount, so adding a fifth
        /// map does not quietly leave these tests exercising only the first four.</summary>
        private static Color[][] Blank()
        {
            var layers = new Color[TerrainSplatBrush.TextureCount][];
            for (int t = 0; t < layers.Length; t++) layers[t] = new Color[W * H];
            return layers;
        }

        private static int CentreIdx(Vector2 world)
        {
            int x = Mathf.RoundToInt((world.x - Min.x) / Size.x * W - 0.5f);
            int y = Mathf.RoundToInt((world.y - Min.y) / Size.y * H - 0.5f);
            return y * W + x;
        }

        [Test]
        public void Dab_PaintsTheChannelTowardTheTarget_AtFullFlow()
        {
            var L = Blank();
            Color[] a = L[0], b = L[1], c = L[2];
            var centre = new Vector2(8f, 8f);
            TerrainSplatBrush.Dab(L, W, H, Min, Size, centre, 3f, 0.5f,
                material: 7 /* dirt → B.a */, target: 0.35f, flow: 1f, exclusive: true);

            Assert.AreEqual(0.35f, b[CentreIdx(centre)].a, 1e-3f, "centre must reach the target");
            Assert.AreEqual(0f, a[CentreIdx(centre)].r, 1e-5f, "no other channel painted");
            Assert.AreEqual(0f, b[CentreIdx(new Vector2(1f, 1f))].a, 1e-5f, "outside the radius untouched");
        }

        [Test]
        public void Dab_Exclusive_FadesTheOtherChannels_NonExclusiveStacks()
        {
            var centre = new Vector2(8f, 8f);

            var L = Blank();
            Color[] a = L[0], b = L[1], c = L[2];
            for (int i = 0; i < b.Length; i++) b[i].b = 0.8f;   // pre-painted silt everywhere
            TerrainSplatBrush.Dab(L, W, H, Min, Size, centre, 3f, 0.5f,
                material: 7, target: 0.4f, flow: 1f, exclusive: true);
            Assert.AreEqual(0f, b[CentreIdx(centre)].b, 1e-4f,
                "exclusive painting at full flow must fully replace the other material at the centre");
            Assert.AreEqual(0.8f, b[CentreIdx(new Vector2(1f, 1f))].b, 1e-5f,
                "silt outside the footprint untouched");

            L = Blank();
            a = L[0]; b = L[1]; c = L[2];
            for (int i = 0; i < b.Length; i++) b[i].b = 0.8f;
            TerrainSplatBrush.Dab(L, W, H, Min, Size, centre, 3f, 0.5f,
                material: 7, target: 0.4f, flow: 1f, exclusive: false);
            Assert.AreEqual(0.8f, b[CentreIdx(centre)].b, 1e-5f,
                "non-exclusive painting must leave the other material alone (the shader renormalises)");
        }

        [Test]
        public void Dab_EraseAll_FadesEveryChannel_OnEverySplatMap()
        {
            var L = Blank();
            var centre = new Vector2(8f, 8f);
            // Prime EVERY map, not just the ones that existed before kit v2: an erase that misses
            // one map leaves paint the owner believes they removed.
            for (int t = 0; t < L.Length; t++)
                for (int i = 0; i < L[t].Length; i++) L[t][i] = new Color(0.5f, 0.4f, 0.3f, 0.2f);

            TerrainSplatBrush.Dab(L, W, H, Min, Size, centre, 3f, 0.5f,
                TerrainSplatBrush.EraseAllMaterials, target: 0f, flow: 1f, exclusive: false);

            int idx = CentreIdx(centre);
            int outside = CentreIdx(new Vector2(1f, 1f));
            for (int t = 0; t < L.Length; t++)
            {
                string map = TerrainSplatBrush.TextureSuffixes[t];
                Assert.AreEqual(0f, L[t][idx].r, 1e-4f, $"erase-all left paint in Splat{map}.r");
                Assert.AreEqual(0f, L[t][idx].g, 1e-4f, $"erase-all left paint in Splat{map}.g");
                Assert.AreEqual(0f, L[t][idx].b, 1e-4f, $"erase-all left paint in Splat{map}.b");
                Assert.AreEqual(0f, L[t][idx].a, 1e-4f, $"erase-all left paint in Splat{map}.a");
                Assert.AreEqual(0.4f, L[t][outside].g, 1e-5f, $"Splat{map} outside the radius touched");
            }
        }

        [Test]
        public void Dab_PaintsAndReplacesOnTheKitV2Maps()
        {
            var L = Blank();
            var centre = new Vector2(8f, 8f);

            // Rockweed lives on SplatD.g — the map that did not exist before kit v2. Lay grass
            // (SplatA.r) under it first so the exclusive contract is exercised ACROSS maps.
            for (int i = 0; i < L[0].Length; i++) L[0][i].r = 0.9f;
            TerrainSplatBrush.Dab(L, W, H, Min, Size, centre, 3f, 0.5f,
                material: 13 /* rockweed → D.g */, target: 0.6f, flow: 1f, exclusive: true);

            int idx = CentreIdx(centre);
            Assert.AreEqual(0.6f, L[3][idx].g, 1e-3f, "rockweed did not reach its target on SplatD.g");
            Assert.AreEqual(0f, L[0][idx].r, 1e-4f,
                "exclusive rockweed must replace the grass beneath it — across splat maps, not just within one");

            // Foreshore is the other new class: SplatC.b, an alpha-adjacent channel on an
            // already-existing map, where an off-by-one in ChannelOf would land on sedge.
            var L2 = Blank();
            TerrainSplatBrush.Dab(L2, W, H, Min, Size, centre, 3f, 0.5f,
                material: 10 /* foreshore → C.b */, target: 0.5f, flow: 1f, exclusive: true);
            Assert.AreEqual(0.5f, L2[2][idx].b, 1e-3f, "foreshore did not land on SplatC.b");
            Assert.AreEqual(0f, L2[2][idx].g, 1e-5f, "foreshore leaked into sedge (SplatC.g)");
            Assert.AreEqual(0f, L2[2][idx].a, 1e-5f, "foreshore leaked into talus (SplatC.a)");
        }

        [Test]
        public void Dab_Erase_LowersOnlyTheSelectedChannel()
        {
            var L = Blank();
            Color[] a = L[0], b = L[1], c = L[2];
            var centre = new Vector2(8f, 8f);
            for (int i = 0; i < a.Length; i++) { a[i].r = 0.7f; a[i].g = 0.5f; }

            // Erase = target 0 on the selected channel, exclusive off (the tool's Erase mode).
            TerrainSplatBrush.Dab(L, W, H, Min, Size, centre, 3f, 0.5f,
                material: 0 /* grass */, target: 0f, flow: 1f, exclusive: false);

            int idx = CentreIdx(centre);
            Assert.AreEqual(0f, a[idx].r, 1e-4f, "grass erased at the centre");
            Assert.AreEqual(0.5f, a[idx].g, 1e-5f, "marram untouched by a grass erase");
        }

        [Test]
        public void Dab_IsDeterministic()
        {
            var L1 = Blank();
            Color[] a1 = L1[0], b1 = L1[1], c1 = L1[2];
            var L2 = Blank();
            Color[] a2 = L2[0], b2 = L2[1], c2 = L2[2];
            var centre = new Vector2(7.3f, 9.1f);
            TerrainSplatBrush.Dab(L1, W, H, Min, Size, centre, 4f, 0.7f, 8, 0.5f, 0.6f, true);
            TerrainSplatBrush.Dab(L2, W, H, Min, Size, centre, 4f, 0.7f, 8, 0.5f, 0.6f, true);
            CollectionAssert.AreEqual(a1, a2);
            CollectionAssert.AreEqual(b1, b2);
            CollectionAssert.AreEqual(c1, c2);
        }

        [Test]
        public void PaintPolyline_CoversTheWholeLine_AndIsDeterministic()
        {
            var L1 = Blank();
            Color[] a1 = L1[0], b1 = L1[1], c1 = L1[2];
            // Vertices on texel CENTRES (x.5) so the coverage asserts sample the line itself.
            var pts = new[] { new Vector2(2.5f, 3.5f), new Vector2(9.5f, 4.5f), new Vector2(13.5f, 11.5f) };
            TerrainSplatBrush.PaintPolyline(L1, W, H, Min, Size, pts,
                dabSpacingMetres: 0.75f, radiusMetres: 1.25f, falloff01: 0.5f,
                material: 7, target: 0.35f, exclusive: true);

            // Every vertex (and the midpoints of each segment) got paint in the dirt channel.
            foreach (var p in pts)
                Assert.Greater(b1[CentreIdx(p)].a, 0.2f, $"no paint at vertex {p}");
            Assert.Greater(b1[CentreIdx((pts[0] + pts[1]) * 0.5f)].a, 0.2f, "gap mid-segment 1");
            Assert.Greater(b1[CentreIdx((pts[1] + pts[2]) * 0.5f)].a, 0.2f, "gap mid-segment 2");

            var L2 = Blank();
            Color[] a2 = L2[0], b2 = L2[1], c2 = L2[2];
            TerrainSplatBrush.PaintPolyline(L2, W, H, Min, Size, pts,
                0.75f, 1.25f, 0.5f, 7, 0.35f, true);
            CollectionAssert.AreEqual(b1, b2, "the polyline stroke is not deterministic");
        }
    }
}
