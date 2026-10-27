using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class StPetersVillageYardBindingTests
    {
        [Test]
        public void OnlyFiveActiveYardsBindV1Data()
        {
            foreach (string name in new[] { "School", "GeneralStore", "SageCottage", "HarbourGable", "WhiteGable" })
                Assert.That(StPetersVillageYards.TryKey(name, out _), Is.True, name);
            foreach (string name in new[] { "WhiteFarmhouse", "RedSaltbox", "PostOffice", "CreamSaltbox", "GinnyPlot", "CamperLot" })
                Assert.That(StPetersVillageYards.TryKey(name, out _), Is.False, name);
        }

        [Test]
        public void HullMargin_UsesGroundMetresAndRejectsAnOutsideCorner()
        {
            float depth = IsoGround.GroundDepthScale;
            var polygon = new GroundPolygon(new[] { new Vector2(-5, -5 * depth),
                new Vector2(5, -5 * depth), new Vector2(5, 5 * depth), new Vector2(-5, 5 * depth) });
            Assert.That(StPetersVillageYards.HullMargin(polygon,
                new Vector4(-4, -4 * depth, 4, 4 * depth)), Is.EqualTo(1f).Within(.000001f));
            Assert.That(StPetersVillageYards.HullMargin(polygon,
                new Vector4(-4, -4 * depth, 5.1f, 4 * depth)), Is.LessThan(0));
        }
    }
}
