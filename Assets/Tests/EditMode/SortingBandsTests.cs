using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards the sprite sorting-order ladder in <see cref="SortingBands"/> — and above all the one claim
    /// that failed silently for months: <b>the decor band must be WIDER THAN THE REGION IT SORTS.</b>
    ///
    /// <para>The whole game shares ONE sorting layer, so a sprite's order is its only claim to a place in
    /// the stack, and <see cref="YSortSprite"/> clamps world Y into a band inside it. Where that clamp
    /// bites, sorting simply STOPS — every sprite past the end saturates on the same order and interleaves
    /// by draw order instead of by position. Nothing throws, nothing logs, and it looks like a shuffled
    /// z-order rather than a bad number. Until 2026-08-05 the band resolved 9.5 m of a 520 m region and
    /// 98% of St Peters' ground cover — plus the player standing in it — sorted on a saturated order.
    /// It hid for so long only because the start spawn sits at Y = 0, dead centre of the window.</para>
    ///
    /// <para>So the test that matters here is <see cref="EveryRegionFitsInsideTheDecorBand"/>: it re-derives
    /// the requirement from each builder's own declared extent and fails with the number to raise
    /// <see cref="SortingBands.DecorHalfExtentMetres"/> to. Grow a region past the band and this goes red
    /// BEFORE the layering quietly stops working. (ADR 0032.)</para>
    /// </summary>
    public class SortingBandsTests
    {
        /// <summary>Every region that declares a world extent, as (name, centre Y, height) in metres.
        /// Read from the builders themselves so a resize cannot leave this table behind.</summary>
        static IEnumerable<(string Name, float CentreY, float HeightM)> Regions()
        {
            yield return ("St Peters", StPetersBuilder.RegionWorldCenter.y, StPetersBuilder.RegionWorldSize.y);
            yield return ("Nine Mile Creek", NineMileCreekBuilder.NineMileCreekSeaCenter.y,
                                             NineMileCreekBuilder.NineMileCreekSeaSize.y);
            yield return ("Greybox Cove", GreyboxBuilder.CoveSeaCenter.y, GreyboxBuilder.CoveSeaSize.y);
        }

        /// <summary>⭐ THE ONE THAT MATTERS. Every region must sit entirely inside the span the decor band
        /// resolves, or its decor stops layering past the edge — silently.</summary>
        [Test]
        public void EveryRegionFitsInsideTheDecorBand()
        {
            float worstReach = 0f;
            string worst = null;

            foreach (var (name, centreY, heightM) in Regions())
            {
                float top = centreY + heightM * 0.5f;
                float bottom = centreY - heightM * 0.5f;
                float reach = Mathf.Max(Mathf.Abs(top), Mathf.Abs(bottom));
                if (reach > worstReach) { worstReach = reach; worst = name; }

                Assert.IsTrue(SortingBands.ResolvesWorldY(top),
                    $"{name}'s north edge (Y = {top:F1} m) is outside the decor band, which resolves " +
                    $"±{SortingBands.DecorHalfExtentMetres} m. Decor up there saturates on order " +
                    $"{SortingBands.DecorFloor} and stops layering. Raise " +
                    $"SortingBands.DecorHalfExtentMetres to at least {Mathf.CeilToInt(reach)}.");

                Assert.IsTrue(SortingBands.ResolvesWorldY(bottom),
                    $"{name}'s south edge (Y = {bottom:F1} m) is outside the decor band, which resolves " +
                    $"±{SortingBands.DecorHalfExtentMetres} m. Decor down there saturates on order " +
                    $"{SortingBands.DecorCeiling} and stops layering. Raise " +
                    $"SortingBands.DecorHalfExtentMetres to at least {Mathf.CeilToInt(reach)}.");
            }

            Assert.Greater(SortingBands.DecorHalfExtentMetres, worstReach,
                $"The band has no headroom left — {worst} reaches {worstReach:F1} m of the " +
                $"±{SortingBands.DecorHalfExtentMetres} m the band resolves. Raise it before the next " +
                "region grows into the clamp.");
        }

        /// <summary>The band does not merely CONTAIN each region — it still tells two sprites apart at the
        /// far edge. A band that fits but has collapsed to one order per 10 m is the same bug, slower.</summary>
        [Test]
        public void TheBandStillResolvesAtEveryRegionsEdge()
        {
            foreach (var (name, centreY, heightM) in Regions())
            {
                float edge = centreY - heightM * 0.5f;   // the south edge: highest orders, nearest the ceiling
                int atEdge = Order(edge);
                int aStepNearer = Order(edge - 1f / SortingBands.OrdersPerMetre);

                Assert.Greater(aStepNearer, atEdge,
                    $"At {name}'s south edge (Y = {edge:F1} m) a sprite one full order-step nearer the " +
                    $"camera got the SAME order ({atEdge}) — the band has saturated there, so decor at " +
                    "that end interleaves by draw order rather than by position.");
            }
        }

        /// <summary>The ladder is strictly ordered: nothing below the band can be reached from inside it,
        /// and nothing above it can be reached from inside it either.</summary>
        [Test]
        public void TheLadderDoesNotOverlap()
        {
            Assert.Less(SortingBands.PaintedSeabedMin, SortingBands.PaintedSeabedMax, "seabed rungs inverted");
            Assert.Less(SortingBands.PaintedSeabedMax, SortingBands.SubmergedShorePlant, "seabed must be under a submerged plant");
            Assert.Less(SortingBands.SubmergedShorePlant, SortingBands.Sea, "a submerged plant must be under the sea");
            Assert.Less(SortingBands.Sea, SortingBands.WharfDeckMin, "the deck stands OVER the water");
            Assert.Less(SortingBands.WharfDeckMin, SortingBands.WharfDeckMax, "deck rows inverted");
            Assert.Less(SortingBands.WharfDeckMax, SortingBands.DecorFloor,
                "the decor band's floor must clear the wharf deck's southern row, or a character can sort " +
                "underneath the planks they are standing on.");
            Assert.AreEqual(SortingBands.WharfDeckMax, SortingBands.InteriorFloor,
                "an interior floor and the deck's near row both sit one below the decor band, by design.");
            Assert.Less(SortingBands.DecorFloor, SortingBands.DecorCeiling, "the decor band is inverted");
            Assert.Less(SortingBands.DecorCeiling, SortingBands.AboveDecor,
                "AboveDecor is what the ropes and the rain use to clear ALL decor whatever its Y — it must " +
                "sit above the highest order the band can emit.");
            Assert.Less(SortingBands.AboveDecor, SortingBands.WorldTint, "the day/night tint covers the world");
        }

        /// <summary>The band is centred: Y = ±<see cref="SortingBands.DecorHalfExtentMetres"/> lands exactly
        /// on the floor and the ceiling, so a region resolves equally far north and south of the origin.</summary>
        [Test]
        public void TheBandIsCentredOnTheOrigin()
        {
            Assert.AreEqual(SortingBands.DecorFloor, Order(SortingBands.DecorHalfExtentMetres),
                "the north end of the resolved span must land exactly on the floor");
            Assert.AreEqual(SortingBands.DecorCeiling, Order(-SortingBands.DecorHalfExtentMetres),
                "the south end of the resolved span must land exactly on the ceiling");
            Assert.AreEqual(SortingBands.DecorBase, Order(0f), "Y = 0 must sit at the band's base");
        }

        /// <summary><c>sortingOrder</c> is stored in a short. The ladder must fit with room to spare —
        /// silently clamping at 32767 would put the tint and the decor on the same order.</summary>
        [Test]
        public void TheWholeLadderFitsInAShort()
        {
            Assert.LessOrEqual(SortingBands.WorldTint, short.MaxValue,
                "sortingOrder is a short — anything above 32767 clamps and stops being a distinct tier.");
            Assert.GreaterOrEqual(SortingBands.PaintedSeabedMin, short.MinValue, "seabed under the short floor");
        }

        /// <summary>The component's shipped defaults ARE the band. If a default drifts from
        /// <see cref="SortingBands"/>, the ladder above stops describing what actually renders.</summary>
        [Test]
        public void YSortSpriteShipsTheBandsOwnNumbers()
        {
            var go = new GameObject("ysort-defaults-probe", typeof(SpriteRenderer));
            try
            {
                var sort = go.AddComponent<YSortSprite>();
                var so = new UnityEditor.SerializedObject(sort);

                Assert.AreEqual(SortingBands.DecorBase, so.FindProperty("_baseOrder").floatValue,
                    "YSortSprite._baseOrder has drifted from SortingBands.DecorBase.");
                Assert.AreEqual(SortingBands.OrdersPerMetre, so.FindProperty("_orderPerUnit").floatValue,
                    "YSortSprite._orderPerUnit has drifted from SortingBands.OrdersPerMetre.");
                Assert.AreEqual(SortingBands.DecorFloor, so.FindProperty("_minOrder").intValue,
                    "YSortSprite._minOrder has drifted from SortingBands.DecorFloor — the whole ladder " +
                    "below the band is positioned relative to this.");
                Assert.AreEqual(SortingBands.DecorCeiling, so.FindProperty("_maxOrder").intValue,
                    "YSortSprite._maxOrder has drifted from SortingBands.DecorCeiling.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The band as <see cref="YSortSprite"/> actually maps it, using the shipped numbers.</summary>
        static int Order(float worldY) =>
            YSortSprite.OrderFor(worldY, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                 SortingBands.DecorFloor, SortingBands.DecorCeiling);
    }
}
