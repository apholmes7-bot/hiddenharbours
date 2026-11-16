using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The keep rule for the old grass tufts</b> (owner, 2026-11-15: "remove most of the old grass
    /// sprites … areas it could stay might be in some trees or mixed amongst other vegetation"). Pure: no
    /// scene, no assets beyond throwaway instances. The region-wide content check lives in
    /// <c>NineMileCreekGrassThinningContentTests</c>.
    /// </summary>
    public class GrassThinningTests
    {
        // Test-local numbers, standing in for a region's GrassThinningDef. Not the shipped values: the
        // rule must hold for any values, and the content check pins the shipped ones.
        const float Fraction = 0.35f;
        const float Floor = 1.6f;
        const float Radius = 2f;

        sealed class FlatGround : ITidalTerrain
        {
            readonly float _elevation;
            public FlatGround(float elevation) => _elevation = elevation;
            public float ElevationAt(Vector2 worldPos) => _elevation;
        }

        /// <summary>Ground that is water west of x = 0 and high meadow east of it.</summary>
        sealed class ShoreGround : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => p.x < 0f ? -2f : 5f;
        }

        static VegetationAnchorIndex OneShrubAt(Vector2 p, float radius = Radius) =>
            new VegetationAnchorIndex(new List<VegetationAnchor> { new VegetationAnchor(p, radius) });

        [Test]
        public void ATuftOnBareSandIsRejectedEvenBesideAShrub()
        {
            var anchors = OneShrubAt(Vector2.zero);
            Assert.IsFalse(GrassThinning.Keeps(new Vector2(0.5f, 0f), 0f, Fraction, anchors,
                                               new FlatGround(0.8f), Floor),
                "sand below the floor keeps nothing, sheltered or not");
        }

        [Test]
        public void ATuftOverWaterOrMudIsRejected()
        {
            var anchors = OneShrubAt(Vector2.zero);
            Assert.IsFalse(GrassThinning.Keeps(new Vector2(0.5f, 0f), 0f, Fraction, anchors,
                                               new FlatGround(-2f), Floor), "water");
            Assert.IsFalse(GrassThinning.Keeps(new Vector2(0.5f, 0f), 0f, Fraction, anchors,
                                               new FlatGround(float.NegativeInfinity), Floor),
                "no height map at all reads as open water");
            Assert.IsFalse(GrassThinning.Keeps(new Vector2(0.5f, 0f), 0f, Fraction, anchors,
                                               new FlatGround(float.NaN), Floor),
                "a NaN elevation must not slip through a >= comparison");
        }

        [Test]
        public void ATuftInTheOpenAwayFromAnyVegetationIsRejected()
        {
            var anchors = OneShrubAt(Vector2.zero);
            Assert.IsFalse(GrassThinning.Keeps(new Vector2(10f, 10f), 0f, Fraction, anchors,
                                               new FlatGround(5f), Floor),
                "dry open ground with no tree or shrub near is exactly where the owner wants it gone");
            Assert.IsFalse(GrassThinning.Keeps(Vector2.zero, 0f, Fraction,
                                               new VegetationAnchorIndex(new List<VegetationAnchor>()),
                                               new FlatGround(5f), Floor), "no anchors keeps nothing");
            Assert.IsFalse(GrassThinning.Keeps(Vector2.zero, 0f, Fraction, null, new FlatGround(5f), Floor),
                "a null index keeps nothing");
        }

        [Test]
        public void ATuftInsideVegetationOnDryGroundIsKept()
        {
            var anchors = OneShrubAt(new Vector2(3f, 4f));
            Assert.IsTrue(GrassThinning.Keeps(new Vector2(4f, 4.5f), 0.1f, Fraction, anchors,
                                              new FlatGround(5f), Floor));
            Assert.IsTrue(GrassThinning.Keeps(new Vector2(4f, 4.5f), 0.1f, Fraction, anchors, null, Floor),
                "with no ground source the dry-ground test is skipped, not failed");
        }

        [Test]
        public void TheShelterRadiusIsPerAnchorAndEdgeInclusive()
        {
            var anchors = new VegetationAnchorIndex(new List<VegetationAnchor>
            {
                new VegetationAnchor(new Vector2(0f, 0f), 3f),     // a tree
                new VegetationAnchor(new Vector2(20f, 0f), 1f),    // a shrub
            });
            Assert.IsTrue(anchors.IsNear(new Vector2(3f, 0f)), "on the tree's edge");
            Assert.IsFalse(anchors.IsNear(new Vector2(3.01f, 0f)), "just past it");
            Assert.IsTrue(anchors.IsNear(new Vector2(20f, 1f)), "on the shrub's edge");
            Assert.IsFalse(anchors.IsNear(new Vector2(22f, 0f)),
                "2 m from the shrub: inside the TREE's radius but not the shrub's");
            Assert.AreEqual(2, anchors.Count);
        }

        [Test]
        public void AShorelineShrubKeepsItsLandSideOnly()
        {
            var anchors = OneShrubAt(Vector2.zero, 3f);
            var ground = new ShoreGround();
            Assert.IsFalse(GrassThinning.Keeps(new Vector2(-1f, 0f), 0f, 1f, anchors, ground, Floor), "sea side");
            Assert.IsTrue(GrassThinning.Keeps(new Vector2(1f, 0f), 0f, 1f, anchors, ground, Floor), "land side");
        }

        [Test]
        public void TheDensityCapKeepsItsShareOfShelteredTufts()
        {
            // Every site of a 120x120 grid stands inside one huge anchor on dry ground, so only the keep
            // roll decides. The share kept must sit on the fraction — and every shape roll must still be
            // represented, or thinning would be culling one blade class rather than thinning evenly.
            var layout = new GrassFieldLayout
            {
                OriginX = 0f, OriginY = 0f, CellSize = 1f, JitterMetres = 0.3f, SpreadMetres = 0.2f,
                CellsX = 120, CellsY = 120, Slots = 1, Seed = 0,
            };
            var anchors = OneShrubAt(new Vector2(60f, 60f), 1000f);
            var ground = new FlatGround(5f);

            int kept = 0, total = 0, keptTall = 0, keptShort = 0;
            for (int ix = 0; ix < layout.CellsX; ix++)
            for (int iy = 0; iy < layout.CellsY; iy++)
            {
                total++;
                var p = GrassFieldScatter.SlotPosition(layout, ix, iy, 0);
                if (!GrassThinning.Keeps(p, GrassFieldScatter.KeepRoll(layout, ix, iy, 0), Fraction,
                                         anchors, ground, Floor)) continue;
                kept++;
                if (GrassFieldScatter.ShapeRoll(layout, ix, iy, 0) > 0.5f) keptTall++; else keptShort++;
            }

            float share = kept / (float)total;
            Assert.That(share, Is.EqualTo(Fraction).Within(0.02f), $"kept {kept} of {total}");
            Assert.That(keptTall / (float)kept, Is.EqualTo(0.5f).Within(0.05f),
                "the keep roll must be independent of the shape roll");

            Assert.IsFalse(GrassThinning.Keeps(new Vector2(60f, 60f), 0f, 0f, anchors, ground, Floor),
                "a fraction of zero keeps nothing");
            Assert.IsTrue(GrassThinning.Keeps(new Vector2(60f, 60f), 0.9999f, 1f, anchors, ground, Floor),
                "a fraction of one keeps every sheltered tuft");
        }

        [Test]
        public void TheKeepRollIsStable()
        {
            var layout = new GrassFieldLayout { CellSize = 2f, CellsX = 4, CellsY = 4, Slots = 1, Seed = 0 };
            Assert.AreEqual(GrassFieldScatter.KeepRoll(layout, 3, 2, 0), GrassFieldScatter.KeepRoll(layout, 3, 2, 0));
            Assert.AreEqual(GrassFieldScatter.Hash01(3 * 11, 2, GrassFieldScatter.Salt(223, 0)),
                            GrassFieldScatter.KeepRoll(layout, 3, 2, 0),
                "the roll the offline census modelled: Hash01(ix*11+slot, iy, Salt(223, seed))");
        }

        [Test]
        public void TheDefMatchesItsMaterialsAndTheirInstances()
        {
            var shader = Shader.Find("Sprites/Default");
            var tree = new Material(shader) { name = "Tree" };
            var instanced = new Material(shader) { name = "Tree (Instance)" };
            var grass = new Material(shader) { name = "Grass" };
            var def = ScriptableObject.CreateInstance<GrassThinningDef>();
            try
            {
                def.Anchors = new[] { new GrassThinningDef.AnchorClass { Name = "tree", Material = tree, RadiusMetres = 3f } };
                Assert.IsTrue(def.TryRadiusFor(tree, out float r)); Assert.AreEqual(3f, r);
                Assert.IsTrue(def.TryRadiusFor(instanced, out r), "a runtime instance of the anchor material");
                Assert.IsFalse(def.TryRadiusFor(grass, out _));
                Assert.IsFalse(def.TryRadiusFor(null, out _));
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(tree); Object.DestroyImmediate(instanced); Object.DestroyImmediate(grass);
            }
        }

        [Test]
        public void AFieldWithoutARuleDrawsItsWholeMeadowAndARuleOnlyRemoves()
        {
            var go = new GameObject("GrassThinningTests_Field");
            var def = ScriptableObject.CreateInstance<GrassThinningDef>();
            try
            {
                var field = go.AddComponent<GrassField>();
                const int cx = 40, cy = 40;
                var sites = new byte[cx * cy];
                for (int i = 0; i < sites.Length; i++) sites[i] = GrassFieldScatter.PackSlot(1, false);
                // No material: SetField's rebuild returns before drawing, which is all this test needs.
                field.SetField(Vector2.zero, 1f, 0.3f, 0.2f, cx, cy, 1, 0, sites,
                               4f, 0, 0, null, Color.white,
                               new Sprite[1], new[] { new GrassField.HabitatPool { Name = "meadow", Pool = new[] { 0 } } },
                               null);

                var whole = field.DeriveBlades();
                Assert.AreEqual(cx * cy, whole.Count);
                CollectionAssert.AreEqual(Positions(whole), Positions(field.DeriveBlades(null, null)),
                    "no rule is the shipped meadow, tuft for tuft");

                def.KeepFraction = 1f;
                var anchors = OneShrubAt(new Vector2(10f, 10f), 4f);
                var thinned = field.DeriveBlades(def, anchors);
                Assert.That(thinned.Count, Is.GreaterThan(0).And.LessThan(whole.Count));
                var all = new HashSet<Vector2>(Positions(whole));
                foreach (var b in thinned)
                {
                    Assert.IsTrue(all.Contains(b.Position), "thinning only removes; it never moves a tuft");
                    Assert.That(Vector2.Distance(b.Position, new Vector2(10f, 10f)), Is.LessThanOrEqualTo(4f));
                }

                Assert.AreEqual(0, field.DeriveBlades(def, null).Count, "a rule with no vegetation keeps nothing");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(def);
            }
        }

        static List<Vector2> Positions(List<GrassField.Blade> blades)
        {
            var list = new List<Vector2>(blades.Count);
            foreach (var b in blades) list.Add(b.Position);
            return list;
        }
    }
}
