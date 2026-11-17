using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Plants against a hull (the owner, 2026-11-16: "it seem that sprites appear above the boat when they
    /// should be below/behind"). Run aground on the St Peters shoal, rockweed and grass drew over her stern,
    /// because every plant Y-sorts in the decor band and every hull sorts below it. Ground cover and
    /// intertidal weed now lie under every hull; a standing plant sorts against her waterline pivot.
    /// </summary>
    public sealed class PlantHullSortingTests
    {
        private sealed class Hull : IHullPresence
        {
            public HullFootprint Footprint { get; set; }
            public float PictureForeshortenY { get; set; } = 1f;
        }

        const float GroundCover = 0.5f, Rise = 6f;
        static readonly string[] Lying = { "fringe", "mid" };
        readonly List<PlantSpeciesDef> _made = new List<PlantSpeciesDef>();

        PlantSpeciesDef Species(float height, string habitat)
        {
            var s = ScriptableObject.CreateInstance<PlantSpeciesDef>();
            s.StandingHeightM = height; s.Habitat = habitat;
            _made.Add(s);
            return s;
        }

        [TearDown] public void TearDown()
        {
            foreach (var s in _made) Object.DestroyImmediate(s);
            _made.Clear();
        }

        // The Cape Islander as she lay aground in the look: 12.9 m, half-beam 2.4, bow 20 degrees east of
        // north, drawn at the rigs' 40 degree bake.
        static IReadOnlyList<IHullPresence> Aground(Vector2 at) => new IHullPresence[]
        {
            new Hull
            {
                Footprint = HullFootprint.FromBowDirection(at, new Vector2(Mathf.Sin(20f * Mathf.Deg2Rad),
                                                           Mathf.Cos(20f * Mathf.Deg2Rad)), 12.9f, 2.4f),
                PictureForeshortenY = WakeRootMath.ForeshortenY(40f),
            },
        };

        [Test] public void GroundCoverAndIntertidalWeed_LieFlat_TallStandingGrowthDoesNot()
        {
            Assert.IsTrue(PlantHullSorting.LiesFlat(Species(0.375f, "barren"), GroundCover, Lying), "lowbush blueberry");
            Assert.IsTrue(PlantHullSorting.LiesFlat(Species(1.625f, "mid"), GroundCover, Lying), "knotted wrack");
            Assert.IsTrue(PlantHullSorting.LiesFlat(Species(2.75f, "fringe"), GroundCover, Lying), "sugar kelp, left by the tide");
            Assert.IsFalse(PlantHullSorting.LiesFlat(Species(1.8125f, "lowmarsh"), GroundCover, Lying), "cordgrass stands");
            Assert.IsFalse(PlantHullSorting.LiesFlat(Species(3.4f, "edge"), GroundCover, Lying), "sumac stands");
            Assert.IsFalse(PlantHullSorting.LiesFlat(Species(0f, ""), GroundCover, Lying), "no measured height: it stands, as before");
            Assert.IsFalse(PlantHullSorting.LiesFlat(null, GroundCover, Lying));
        }

        [Test] public void WhatLiesFlat_IsUnderEveryHull_WhereverItIs()
        {
            foreach (float y in new[] { -250f, -55f, 0f, 120f, 250f })
                foreach (var visual in AllBoatVisuals())
                    Assert.Less(PlantHullSorting.OrderFor(y, false, SortingBands.SubmergedShorePlant, true, false),
                                visual.Order, $"{visual.Name} at y {y}");
        }

        [Test] public void AStandingPlant_SortsAgainstHerWaterlinePivot()
        {
            var at = new Vector2(189.67f, -55.88f);
            var hulls = Aground(at);
            Assert.IsFalse(PlantHullSorting.BehindAHull(at + new Vector2(0.5f, -3f), hulls, Rise), "south of her pivot: in front");
            Assert.IsTrue(PlantHullSorting.BehindAHull(at + new Vector2(0.5f, 3f), hulls, Rise), "north of it, on her picture: behind");
            Assert.IsTrue(PlantHullSorting.BehindAHull(at + new Vector2(0f, 9f), hulls, Rise), "behind her wheelhouse");
            Assert.IsFalse(PlantHullSorting.BehindAHull(at + new Vector2(0f, 20f), hulls, Rise), "far up the beach: no overlap");
            Assert.IsFalse(PlantHullSorting.BehindAHull(at + new Vector2(9f, 2f), hulls, Rise), "well clear abeam");
            Assert.IsFalse(PlantHullSorting.BehindAHull(at, new IHullPresence[0], Rise), "no hull, nothing to be behind");

            int front = PlantHullSorting.OrderFor(at.y - 3f, false, SortingBands.SubmergedShorePlant, false, false);
            int behind = PlantHullSorting.OrderFor(at.y + 3f, false, SortingBands.SubmergedShorePlant, false, true);
            foreach (var visual in AllBoatVisuals())
            {
                Assert.Greater(front, visual.Order, $"in front of {visual.Name}");
                Assert.Less(behind, visual.Order, $"behind {visual.Name}");
            }
        }

        [Test] public void Submerged_StillWins_TheSeaReadsOverIt()
        {
            Assert.AreEqual(SortingBands.SubmergedShorePlant,
                PlantHullSorting.OrderFor(0f, true, SortingBands.SubmergedShorePlant, true, true));
            Assert.Less(SortingBands.SubmergedShorePlant, SortingBands.UnderHulls);
            Assert.Less(SortingBands.UnderHulls, SortingBands.Sea);
        }

        // Every hull's picture order, read off her visual def asset (Boats owns the type; this assembly
        // reads the serialized field, which is the number the hull skinner hands her renderer).
        static IEnumerable<(string Name, int Order)> AllBoatVisuals()
        {
            int found = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:BoatVisualDef"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                var order = asset != null ? new SerializedObject(asset).FindProperty("SortingOrder") : null;
                if (order == null) continue;
                found++;
                yield return (asset.name, order.intValue);
            }
            Assert.Greater(found, 0, "no BoatVisualDef assets found");
        }
    }
}