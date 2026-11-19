using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A gift leaves the pail or the deck tray, and the picture shows it at once.</b> The gift take
    /// moves one item through Core (hands, then what he carries, then storage) and publishes only
    /// <see cref="GiftGiven"/>: no <c>FishCaught</c>, no <c>CatchSold</c>. Before this fix neither picture
    /// listened to it, so the pail and the tray kept showing the fish he had just given until some other
    /// catch event came along.
    ///
    /// <para>EditMode runs no <c>OnEnable</c>/<c>OnDisable</c>, so the tests call them by reflection (the
    /// pattern <c>LampShadowRenderTests</c> uses): that is what wires and unwires the real bus
    /// subscription, which is the thing under test. The gift take itself is simulated the way
    /// <c>GiftInventory</c> does it for each container: a hold with no surgical remove is emptied and
    /// refilled less one (the pail), a hold with one removes the one item (the boat's hold).</para>
    /// </summary>
    public class GiftRefreshesContainerPicturesTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private readonly List<MonoBehaviour> _enabled = new List<MonoBehaviour>();

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<GiftGiven>();
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (MonoBehaviour b in _enabled) if (b != null) Call(b, "OnDisable");
            _enabled.Clear();
            EventBus.Clear<GiftGiven>();
            GameServices.Reset();
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures ------------------------------------------------------------------------------------

        private static void Call(MonoBehaviour target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic |
                                                              BindingFlags.Public);
            Assert.That(m, Is.Not.Null, $"{target.GetType().Name}.{method} exists");
            m.Invoke(target, null);
        }

        private void Enable(MonoBehaviour b)
        {
            Call(b, "OnEnable");
            _enabled.Add(b);
        }

        private void Disable(MonoBehaviour b)
        {
            Call(b, "OnDisable");
            _enabled.Remove(b);
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

        private CatchItemLibrary Library(string speciesId, string kind)
        {
            var library = ScriptableObject.CreateInstance<CatchItemLibrary>();
            _spawned.Add(library);
            var variants = new Sprite[CatchFillMath.Variants];
            Sprite one = NamedSprite("variant");
            for (int v = 0; v < variants.Length; v++) variants[v] = one;
            library.Configure(
                new[] { new CatchItemLibrary.KindEntry { Kind = kind, Variants = variants } },
                new[] { new CatchItemLibrary.SpeciesEntry { SpeciesId = speciesId, Kind = kind } },
                fallbackKind: kind);
            return library;
        }

        private Sprite[] EightSprites()
        {
            var sprites = new Sprite[BucketFillPresenter.Facings];
            for (int d = 0; d < sprites.Length; d++) sprites[d] = NamedSprite($"d{d}");
            return sprites;
        }

        private Sprite[] FillTable()
        {
            int facings = BucketFillPresenter.Facings;
            string[] groups = CatchFillMath.BucketGroups;
            var flat = new Sprite[BucketFillPresenter.Bands.Length * groups.Length * facings];
            for (int b = 0; b < BucketFillPresenter.Bands.Length; b++)
                for (int g = 0; g < groups.Length; g++)
                    for (int d = 0; d < facings; d++)
                        flat[(b * groups.Length + g) * facings + d] =
                            NamedSprite($"{BucketFillPresenter.Bands[b]}_{groups[g]}_d{d}");
            return flat;
        }

        private static CatchItem Cod()
            => new CatchItem("fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish, 2f, 12, 0.2f);

        private static GiftGiven AGift() => new GiftGiven("npc.test", "fish.atlantic_cod", GiftReaction.Liked, 0);

        // ---- the pail ------------------------------------------------------------------------------------

        [Test]
        public void AGiftFromThePail_RefreshesThePailPictureAtOnce_AndNotAfterItIsSwitchedOff()
        {
            var go = new GameObject("Pail");
            _spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            var presenter = go.AddComponent<BucketFillPresenter>();
            presenter.Configure(Library("fish.atlantic_cod", "cod"), EightSprites(), FillTable());
            var bucket = go.AddComponent<CarriableBucket>();
            bucket.Configure(20, sr, "container.test.gift_pail");
            Assert.That(bucket.TryAdd(Cod()), Is.True);
            Assert.That(bucket.TryAdd(Cod()), Is.True);
            Assert.That(bucket.TryAdd(Cod()), Is.True);

            var bridge = go.AddComponent<HoldCatchFillSource>();
            Enable(bridge);   // subscribes and reads the pail once
            Assert.That(presenter.ItemCount, Is.EqualTo(3), "the pail shows the three he has");

            // The gift take for a hold with no surgical remove: emptied and refilled less one.
            bucket.Clear();
            Assert.That(bucket.TryAdd(Cod()), Is.True);
            Assert.That(bucket.TryAdd(Cod()), Is.True);
            EventBus.Publish(AGift());

            Assert.That(presenter.ItemCount, Is.EqualTo(2),
                        "the gift left the pail, and the picture must say so the moment it is given");

            Disable(bridge);
            bucket.Clear();
            Assert.That(bucket.TryAdd(Cod()), Is.True);
            EventBus.Publish(AGift());
            Assert.That(presenter.ItemCount, Is.EqualTo(2), "switched off, it no longer listens (unsubscribed)");
        }

        // ---- the deck tray -------------------------------------------------------------------------------

        [Test]
        public void AGiftOffTheBoat_RefreshesTheDeckTrayAtOnce_AndNotAfterItIsSwitchedOff()
        {
            var tray = ScriptableObject.CreateInstance<DeckContainerDef>();
            _spawned.Add(tray);
            tray.FillSprites = new[] { NamedSprite("empty"), NamedSprite("low"), NamedSprite("half"), NamedSprite("brim") };
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(hull);
            hull.HoldUnits = 4;
            hull.DeckContainer = tray;

            var go = new GameObject("Boat");
            _spawned.Add(go);
            var hold = go.AddComponent<ShipHold>();
            hold.SetHull(hull);
            Assert.That(hold.TryAdd(Cod()), Is.True);
            Assert.That(hold.TryAdd(Cod()), Is.True);

            var presenter = go.AddComponent<DeckContainerPresenter>();
            Call(presenter, "Awake");   // finds its hold
            Enable(presenter);          // subscribes and reads the hold once

            Transform child = go.transform.Find("DeckContainer");
            Assert.That(child, Is.Not.Null, "the tray is drawn");
            var renderer = child.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sprite.name, Is.EqualTo("half"), "two of four reads half");

            Assert.That(hold.TryRemoveAt(0), Is.True, "the gift take off the boat: one item, nothing else");
            EventBus.Publish(AGift());

            Assert.That(renderer.sprite.name, Is.EqualTo("low"),
                        "the gift came off the boat, and the tray must say so the moment it is given");

            Disable(presenter);
            Assert.That(hold.TryRemoveAt(0), Is.True);
            EventBus.Publish(AGift());
            Assert.That(renderer.sprite.name, Is.EqualTo("low"), "switched off, it no longer listens (unsubscribed)");
        }
    }
}
