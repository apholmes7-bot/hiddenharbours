using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>No painted fill states, no deck tray.</b> The deck container used to fall back to a code-built
    /// placeholder tray when its Def had no art. That flat, screen-upright sprite does not sit in a mesh
    /// hull, so aboard the dory and the punt it floated as a grey rectangle over the water, over the
    /// rower's legs or above his head as the boat turned. The rule pinned here: the presenter draws only
    /// the Def's authored fill states, and a container without them puts no renderer on the boat at all.
    ///
    /// <para>EditMode runs no <c>Awake</c>/<c>OnEnable</c> on AddComponent, so the lifecycle is called by
    /// reflection (the <c>GiftRefreshesContainerPicturesTests</c> pattern).</para>
    /// </summary>
    public class DeckTrayDrawsOnlyPaintedArtTests
    {
        private const string BoatsRoot = "Assets/_Project/Data/Boats";
        private const string DoryPath = BoatsRoot + "/Dory.asset";

        private readonly List<Object> _spawned = new List<Object>();
        private readonly List<MonoBehaviour> _enabled = new List<MonoBehaviour>();

        [SetUp]
        public void SetUp() => EventBus.Clear<FishCaught>();

        [TearDown]
        public void TearDown()
        {
            foreach (MonoBehaviour b in _enabled) if (b != null) Call(b, "OnDisable");
            _enabled.Clear();
            EventBus.Clear<FishCaught>();
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private static void Call(MonoBehaviour target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic |
                                                              BindingFlags.Public);
            Assert.That(m, Is.Not.Null, $"{target.GetType().Name}.{method} exists");
            m.Invoke(target, null);
        }

        private Sprite NamedSprite(string name)
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
            s.name = name;
            _spawned.Add(s);
            return s;
        }

        private static CatchItem Cod() => new CatchItem("fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish, 2f, 12, 0.2f);

        private GameObject BoatWith(DeckContainerDef container, int holdUnits, int catches)
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(hull);
            hull.HoldUnits = holdUnits;
            hull.DeckContainer = container;
            hull.DeckContainerOffset = new Vector2(0.35f, -0.9f);

            var go = new GameObject("Boat");
            _spawned.Add(go);
            var hold = go.AddComponent<ShipHold>();
            hold.SetHull(hull);
            for (int i = 0; i < catches; i++) Assert.That(hold.TryAdd(Cod()), Is.True);

            var presenter = go.AddComponent<DeckContainerPresenter>();
            Call(presenter, "Awake");      // finds its hold
            Call(presenter, "OnEnable");   // subscribes and reads the hold once
            _enabled.Add(presenter);
            return go;
        }

        // ---- the pure rule ---------------------------------------------------------------------------

        [Test]
        public void AContainerWithNoPaintedStates_HasNothingToDraw_AtAnyFill()
        {
            var unpainted = ScriptableObject.CreateInstance<DeckContainerDef>();
            _spawned.Add(unpainted);

            unpainted.FillSprites = null;
            for (int used = 0; used <= 6; used++)
                Assert.That(DeckContainerPresenter.SpriteForFill(unpainted, used, 6), Is.Null, $"null states, {used}/6");

            unpainted.FillSprites = new Sprite[0];
            for (int used = 0; used <= 6; used++)
                Assert.That(DeckContainerPresenter.SpriteForFill(unpainted, used, 6), Is.Null, $"empty states, {used}/6");

            Assert.That(DeckContainerPresenter.SpriteForFill(null, 0, 6), Is.Null, "no container, nothing to draw");
        }

        [Test]
        public void AContainerWithPaintedStates_DrawsThem()
        {
            var painted = ScriptableObject.CreateInstance<DeckContainerDef>();
            _spawned.Add(painted);
            painted.FillSprites = new[] { NamedSprite("empty"), NamedSprite("low"), NamedSprite("half"), NamedSprite("brim") };

            Assert.That(DeckContainerPresenter.SpriteForFill(painted, 0, 6).name, Is.EqualTo("empty"));
            Assert.That(DeckContainerPresenter.SpriteForFill(painted, 1, 6).name, Is.EqualTo("low"));
            Assert.That(DeckContainerPresenter.SpriteForFill(painted, 6, 6).name, Is.EqualTo("brim"));
        }

        // ---- on a boat ---------------------------------------------------------------------------------

        [Test]
        public void AnUnpaintedContainer_PutsNoRendererOnTheBoat_EmptyOrLoaded()
        {
            var unpainted = ScriptableObject.CreateInstance<DeckContainerDef>();
            _spawned.Add(unpainted);
            unpainted.FillSprites = new Sprite[0];

            GameObject empty = BoatWith(unpainted, holdUnits: 6, catches: 0);
            Assert.That(empty.transform.Find("DeckContainer"), Is.Null, "an empty hold with no painted tray draws nothing");
            Assert.That(empty.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty, "no sprite of any kind rides the boat");

            GameObject loaded = BoatWith(unpainted, holdUnits: 6, catches: 3);
            EventBus.Publish(new FishCaught(Cod()));
            Assert.That(loaded.transform.Find("DeckContainer"), Is.Null, "a loaded hold with no painted tray draws nothing either");
        }

        [Test]
        public void APaintedContainer_StillPutsItsTrayOnTheBoat()
        {
            var painted = ScriptableObject.CreateInstance<DeckContainerDef>();
            _spawned.Add(painted);
            painted.FillSprites = new[] { NamedSprite("empty"), NamedSprite("brim") };

            GameObject boat = BoatWith(painted, holdUnits: 6, catches: 0);
            Transform tray = boat.transform.Find("DeckContainer");
            Assert.That(tray, Is.Not.Null, "painted art: the tray is drawn");
            var sr = tray.GetComponent<SpriteRenderer>();
            Assert.That(sr.enabled, Is.True);
            Assert.That(sr.sprite.name, Is.EqualTo("empty"));
        }

        // ---- the shipped content ---------------------------------------------------------------------

        [Test]
        public void ShippedHulls_WhoseContainerIsUnpainted_DrawNoTray_TheDoryIncluded()
        {
            var dory = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DoryPath);
            Assert.That(dory, Is.Not.Null, DoryPath);
            Assert.That(dory.DeckContainer, Is.Not.Null, "the dory carries a deck container (the hold read's data)");

            int checkedHulls = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:BoatHullDef", new[] { BoatsRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(path);
                if (hull == null || hull.DeckContainer == null) continue;
                Sprite[] states = hull.DeckContainer.FillSprites;
                if (states != null && states.Length > 0) continue;   // painted: the content validator owns it
                checkedHulls++;
                for (int used = 0; used <= hull.HoldUnits; used++)
                    Assert.That(DeckContainerPresenter.SpriteForFill(hull.DeckContainer, used, hull.HoldUnits), Is.Null,
                        $"{path}: an unpainted '{hull.DeckContainer.Id}' must draw nothing ({used}/{hull.HoldUnits})");
            }

            bool doryPainted = dory.DeckContainer.FillSprites != null && dory.DeckContainer.FillSprites.Length > 0;
            if (!doryPainted) Assert.That(checkedHulls, Is.GreaterThan(0), "the dory's unpainted tray was checked");
        }
    }
}
