using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A caster stands down on an excluded cell.</b> SunCasterRules says splash (and spray, foam,
    /// shade...) casts no shadow, but the rule used to be asked only when a caster was ATTACHED. A gull owns
    /// its caster from birth and plays a splash row on the water, so whenever an audit happened to sample a
    /// bird mid-splash it found a forbidden caster (the six SunCastersEverywhere audits, red by timing).
    /// These drive the swap directly, no frames and no luck: the sprite lands on the cell and the caster must
    /// already be drawing nothing; it leaves and the caster is back.
    /// </summary>
    public class SpriteShadowExcludedCellTests
    {
        const string GullSheet = "Assets/_Project/Art/Sprites/Creatures/Seagull.png";
        readonly List<Object> _owned = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        Sprite Cell(string name)
        {
            var texture = new Texture2D(16, 32);
            _owned.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 32), new Vector2(0.5f, 0f), 32f);
            sprite.name = name;
            _owned.Add(sprite);
            return sprite;
        }

        SpriteShadow Caster(Sprite first)
        {
            var go = new GameObject("gull");
            _owned.Add(go);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = first;
            var shadow = go.AddComponent<SpriteShadow>();
            // Not [ExecuteAlways]: EditMode wakes it by hand, as the lamp-shadow tests do.
            MethodInfo awake = typeof(SpriteShadow).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(awake, Is.Not.Null, "SpriteShadow.Awake() not found (private API moved?)");
            awake.Invoke(shadow, null);
            return shadow;
        }

        static void AssertStandsDown(SpriteShadow shadow, string cell)
        {
            Assert.That(shadow.StandsDownForExcludedArt, Is.True, cell + ": an excluded cell must stand the caster down");
            Assert.That(shadow.IsDrawing, Is.False, cell + ": no silhouette and no ground pool on an excluded cell");
            Assert.That(shadow.TryGetLampShadowCaster(out _), Is.False, cell + ": no lamp shadow on an excluded cell");
        }

        static void AssertCasts(SpriteShadow shadow, string cell)
        {
            Assert.That(shadow.StandsDownForExcludedArt, Is.False, cell + ": an ordinary cell keeps its caster");
            Assert.That(shadow.TryGetLampShadowCaster(out _), Is.True, cell + ": an ordinary cell is a lamp caster again");
        }

        [Test]
        public void SwapOntoSplashCell_StandsDown_AndSwapOff_Returns()
        {
            Sprite fly = Cell("gull_fly_0_d5"), splash = Cell("gull_splash_1_d5"), stand = Cell("gull_stand_0_d5");
            var shadow = Caster(fly);
            var renderer = shadow.GetComponent<SpriteRenderer>();
            AssertCasts(shadow, fly.name);

            renderer.sprite = splash;          // the swap alone: no tick, no LateUpdate
            AssertStandsDown(shadow, splash.name);

            renderer.sprite = stand;
            AssertCasts(shadow, stand.name);

            renderer.sprite = splash;
            AssertStandsDown(shadow, splash.name + " (second visit)");
        }

        [Test]
        public void CasterBornOnSplashCell_StandsDownFromTheStart()
        {
            var shadow = Caster(Cell("gull_splash_2_d0"));
            AssertStandsDown(shadow, "born on gull_splash_2_d0");
        }

        [Test]
        public void ShippedGullSheet_EverySplashCellStandsDown_EveryFlyCellCasts()
        {
            var cells = AssetDatabase.LoadAllAssetsAtPath(GullSheet).OfType<Sprite>().ToArray();
            var splash = cells.Where(c => c.name.StartsWith("gull_splash_")).ToArray();
            var fly = cells.Where(c => c.name.StartsWith("gull_fly_")).ToArray();
            Assert.That(splash, Is.Not.Empty, "The gull sheet's splash row went missing; this test lost its subject.");
            Assert.That(fly, Is.Not.Empty, "The gull sheet's fly row went missing; this test lost its subject.");

            var shadow = Caster(fly[0]);
            var renderer = shadow.GetComponent<SpriteRenderer>();
            foreach (var cell in splash)
            {
                renderer.sprite = fly[0];
                AssertCasts(shadow, fly[0].name);
                renderer.sprite = cell;
                AssertStandsDown(shadow, cell.name);
            }
            foreach (var cell in fly)
            {
                renderer.sprite = cell;
                AssertCasts(shadow, cell.name);
            }
        }
    }
}
