using System;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class StPetersLandingCleanDeckTests
    {
        static readonly Rect Floor = new Rect(183f, 2.533452f, 30.5f, 3.71875f);

        [Test]
        public void NoAuthoredPlantRootStandsInsideTheLandingFloor()
        {
            var definition = AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>(
                "Assets/_Project/Data/Plants5/StPetersPlantField.asset");
            Assert.That(definition.HeightProjection, Is.Zero, "authored roots use their data coordinates");
            foreach (var site in definition.Authored)
                Assert.That(StandablePlatform.Contains(Floor, site.At), Is.False, site.Id);
        }

        [Test]
        public void The488ScatterCellsUnderTheLandingRemainExcluded()
        {
            var ground = AssetDatabase.LoadAssetAtPath<PlantFieldGroundData>(
                "Assets/_Project/Data/Plants5/StPetersPlantGround.asset");
            byte[] cells = ground.Decode();
            int checkedCells = 0;
            for (int y = 0; y < ground.Height; y++)
            for (int x = 0; x < ground.Width; x++)
            {
                var at = ground.Origin + new Vector2(x + .5f, y + .5f) * ground.CellSize;
                if (!StandablePlatform.Contains(Floor, at)) continue;
                Assert.That(cells[y * ground.Width + x], Is.EqualTo(255), at.ToString());
                checkedCells++;
            }
            Assert.That(checkedCells, Is.EqualTo(488));
        }

        [Test]
        public void TheLandingDeckGetsNoSpriteShadowFromTheInstaller()
        {
            CheckCaster(false);
        }

        [Test]
        public void ThePreviousRulesWouldInstallTheWholeDeckAsACaster()
        {
            CheckCaster(true);
        }

        static void CheckCaster(bool previousRules)
        {
            var rules = Object.Instantiate(Resources.Load<SunCasterRules>(nameof(SunCasterRules)));
            // The fixture tests art eligibility independently of the test scene's name.
            rules.ExcludedScenes = Array.Empty<string>();
            if (previousRules)
                rules.ExcludedNames = rules.ExcludedNames.Where(n => n != "WharfPass2Deck").ToArray();
            var deck = new GameObject("WharfPass2Deck");
            var hostObject = new GameObject("LandingCasterFixture");
            try
            {
                var renderer = deck.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/_Project/Art/Sprites/StPeters/SetPieces/stpLandingDeck.png");
                renderer.sortingOrder = SortingBands.WharfDeckMin;
                var host = hostObject.AddComponent<SunCasterInstaller>();
                host.Configure(rules);
                var shadow = host.Consider(renderer);
                if (previousRules)
                    Assert.That(shadow, Is.Not.Null, "the unchanged installer casts the old rule set's whole floor");
                else
                {
                    Assert.That(shadow, Is.Null);
                    Assert.That(renderer.GetComponent<SpriteShadow>(), Is.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
                Object.DestroyImmediate(deck);
                Object.DestroyImmediate(rules);
            }
        }
    }
}
