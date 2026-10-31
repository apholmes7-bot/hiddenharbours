using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class SunCasterRulesTests
    {
        readonly List<Object> _owned = new();
        SunCasterRules _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = Object.Instantiate(Resources.Load<SunCasterRules>(nameof(SunCasterRules)));
            _owned.Add(_rules);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        SpriteRenderer Sprite(string name, int order = SortingBands.DecorBase)
        {
            var texture = new Texture2D(16, 64);
            _owned.Add(texture);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 16, 64), Vector2.zero, 32f);
            sprite.name = "StandingFixture";
            _owned.Add(sprite);
            var go = new GameObject(name);
            _owned.Add(go);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        [Test] public void DecorAboveMinimumHeight_Qualifies()
            => Assert.That(_rules.Qualifies(Sprite("Crate"), out _), Is.True);

        [Test] public void BoatAttachedDeckGear_IsExcludedByShippedRules()
        {
            var renderer = Sprite("DeckGear_0");
            Assert.That(_rules.IsExcludedArt(renderer), Is.True);
            Assert.That(_rules.Qualifies(renderer, out _), Is.False);
        }

        [TestCase("GroundDecal")]
        [TestCase("MudSplat")]
        [TestCase("ClamHole")]
        [TestCase("Sea")]
        [TestCase("Seabed")]
        [TestCase("SpriteShadow")]
        [TestCase("LampShadow_0")]
        public void ExcludedArt_DoesNotCast(string name)
            => Assert.That(_rules.Qualifies(Sprite(name), out _), Is.False);

        [Test] public void SpriteAlreadyCasting_DoesNotQualifyAgain()
        {
            var renderer = Sprite("Crate");
            renderer.gameObject.AddComponent<SpriteShadow>();
            Assert.That(_rules.Qualifies(renderer, out _), Is.False);
        }

        [Test] public void ShortSprite_DoesNotQualify()
        {
            var renderer = Sprite("Crate");
            renderer.transform.localScale = Vector3.one * (_rules.MinimumHeightMetres / 4f);
            Assert.That(_rules.Qualifies(renderer, out _), Is.False);
        }

        [Test] public void Bands_UseCoreDecorAndWharfBoundaries()
        {
            Assert.That(_rules.IncludesOrder(SortingBands.DecorFloor), Is.True);
            Assert.That(_rules.IncludesOrder(SortingBands.DecorCeiling), Is.True);
            Assert.That(_rules.IncludesOrder(SortingBands.WharfDeckMin), Is.True);
            Assert.That(_rules.IncludesOrder(SortingBands.WharfDeckMax), Is.True);
            Assert.That(_rules.IncludesOrder(SortingBands.Sea), Is.False);
            Assert.That(_rules.IncludesOrder(SortingBands.PaintedSeabedMin), Is.False);
            Assert.That(_rules.IncludesOrder(SortingBands.SunShade), Is.False);
            Assert.That(_rules.IncludesOrder(SortingBands.WorldTint), Is.False);
        }

        [Test] public void LegacyTree_CastsOutsideModernBand_WhileWaterDoesNot()
        {
            Assert.That(_rules.Qualifies(Sprite("Tree05", SortingBands.PaintedSeabedMin), out var tree), Is.True);
            Assert.That(tree.Id, Is.EqualTo("legacy_tree"));
            Assert.That(_rules.Qualifies(Sprite("Sea", SortingBands.WharfDeckMin), out _), Is.False);
        }

        [Test] public void HiddenFigure_CastsWhileEnabled()
        {
            var renderer = Sprite("Villager");
            renderer.forceRenderingOff = true;
            Assert.That(_rules.Qualifies(renderer, out _), Is.True);
            renderer.enabled = false;
            Assert.That(_rules.Qualifies(renderer, out _), Is.False);
        }

        [Test] public void SceneOptOutAndCanvas_ExcludeSprites()
        {
            // A real region has a name; the editor's initial unsaved scene has an empty one.
            // Keep both exclusion assertions on an explicit, unsaved fixture, never on editor state.
            var scene = EditorSceneManager.NewPreviewScene();
            scene.name = "SunCasterRulesFixture";
            try
            {
                var renderer = Sprite("Crate");
                SceneManager.MoveGameObjectToScene(renderer.gameObject, scene);
                _rules.ExcludedScenes = new[] { "SunCasterRulesFixture" };
                Assert.That(_rules.Qualifies(renderer, out _), Is.False);
                _rules.ExcludedScenes = System.Array.Empty<string>();
                renderer.gameObject.AddComponent<Canvas>();
                Assert.That(_rules.Qualifies(renderer, out _), Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test] public void ShippedDef_HasStableIdentityAndValidFields()
        {
            Assert.That(_rules.Id, Is.EqualTo("art.sun_caster_rules"));
            Assert.That(_rules.IsValid(out var error), Is.True, error);
            Assert.That(_rules.Classes, Is.Not.Empty);
        }

        [TestCase("id")]
        [TestCase("height")]
        [TestCase("interval")]
        [TestCase("distance")]
        [TestCase("offset")]
        [TestCase("bands")]
        [TestCase("class")]
        public void InvalidDefFields_AreRejected(string field)
        {
            switch (field)
            {
                case "id": _rules.Id = ""; break;
                case "height": _rules.MinimumHeightMetres = 0f; break;
                case "interval": _rules.ArrivalScanSeconds = 0f; break;
                case "distance": _rules.CameraDistanceMetres = -1f; break;
                case "offset": _rules.DefaultFootOffset = float.NaN; break;
                case "bands": _rules.Bands = System.Array.Empty<SunCasterBand>(); break;
                case "class": _rules.Classes[0].FootOffset = float.PositiveInfinity; break;
            }
            Assert.That(_rules.IsValid(out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }
    }
}
