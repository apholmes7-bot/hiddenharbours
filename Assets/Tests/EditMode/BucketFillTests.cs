using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE FISH YOU JUST PUT IN THE BUCKET IS IN THE BUCKET AND YOU CAN SEE IT</b> — the defect this
    /// path was reported for, pinned end to end.
    ///
    /// <para><b>What was actually broken.</b> <see cref="HoldCatchFillSource"/> — the one Core→art bridge
    /// for container contents — was welded to <see cref="CatchFillRenderer"/> by a <c>RequireComponent</c>.
    /// That renderer draws individual catch sprites on the TOTE rig's measured slot points, and the bucket
    /// rig publishes no such points: its fills are baked whole states. So a pail wired to the bridge
    /// resolved a geometry it does not have and drew nothing, while the hold cheerfully reported 1/20. The
    /// fix is <see cref="ICatchFillTarget"/>: the bridge feeds whatever pictures are on the object.</para>
    ///
    /// <para>The claims, in the order they would break:
    /// <list type="number">
    ///   <item><b>THE PRESSED-E PATH.</b> Fish in hand → press → the presenter shows N. Driven through
    ///   the REAL chain (<see cref="CarryHands.TryGiveCatchTo"/> → <see cref="IHold.TryAdd"/> →
    ///   <c>FishCaught</c> → the bridge → the presenter), because a test that poked the presenter directly
    ///   would pass over a completely disconnected game.</item>
    ///   <item><b>THE SECOND PICTURE.</b> The bridge feeds a <see cref="BucketFillPresenter"/> as
    ///   willingly as a <see cref="CatchFillRenderer"/>, and both at once.</item>
    ///   <item><b>NO CAPACITY IS NOT NO CATCH.</b> A hold with items and a zero capacity must not read
    ///   empty.</item>
    ///   <item><b>THE INDEX.</b> The presenter's flattened sprite table is indexed the way the builder
    ///   fills it — a table read in a different order draws a plausible pail at the wrong fullness.</item>
    /// </list></para>
    /// </summary>
    public class BucketFillTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchLanded>();
            GameServices.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchLanded>();
            Interactables.Clear();
            InteractVerb.Reset();
            GameServices.Reset();
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- fixtures ------------------------------------------------------------------------------------

        /// <summary>A minimal art table: one species, one kind, one sprite per variant. Enough for the
        /// bridge to map and for the tote renderer to have something non-null to draw.</summary>
        private CatchItemLibrary Library(string speciesId, string kind)
        {
            var library = ScriptableObject.CreateInstance<CatchItemLibrary>();
            _spawned.Add(library);

            var sprite = Sprite.Create(BlankTexture(), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
            _spawned.Add(sprite);

            var variants = new Sprite[CatchFillMath.Variants];
            for (int v = 0; v < variants.Length; v++) variants[v] = sprite;

            library.Configure(
                new[] { new CatchItemLibrary.KindEntry { Kind = kind, Variants = variants } },
                new[] { new CatchItemLibrary.SpeciesEntry { SpeciesId = speciesId, Kind = kind } },
                fallbackKind: kind);
            return library;
        }

        private Texture2D BlankTexture()
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            return tex;
        }

        /// <summary>Eight distinct sprites — one per facing — so an index mistake in the presenter's
        /// table shows up as the WRONG sprite rather than as the same one twice.</summary>
        private Sprite[] EightSprites()
        {
            var sprites = new Sprite[BucketFillPresenter.Facings];
            for (int d = 0; d < sprites.Length; d++)
            {
                sprites[d] = Sprite.Create(BlankTexture(), new Rect(0, 0, 4, 4),
                                           new Vector2(0.5f, 0.5f), 32f);
                sprites[d].name = $"d{d}";
                _spawned.Add(sprites[d]);
            }
            return sprites;
        }

        private static CatchItem Cod(float weightKg = 2f)
            => new CatchItem("fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish,
                             weightKg, 12, 0.2f);

        /// <summary>The dev bucket as the builder stands it up: a hold, a picture and the bridge between
        /// them, on one object.</summary>
        private (CarriableBucket bucket, BucketFillPresenter presenter, HoldCatchFillSource bridge)
            Pail(int capacity = 20)
        {
            var go = new GameObject("Bucket");
            _spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();

            var presenter = go.AddComponent<BucketFillPresenter>();
            presenter.Configure(Library("fish.atlantic_cod", "cod"), EightSprites(), FillTable());

            var bucket = go.AddComponent<CarriableBucket>();
            bucket.Configure(capacity, sr, "container.test.bucket");

            var bridge = go.AddComponent<HoldCatchFillSource>();
            return (bucket, presenter, bridge);
        }

        /// <summary>A full fill table, every entry distinct and NAMED for its own (band, group, facing) —
        /// so a mis-indexed read is caught by name rather than by a null.</summary>
        private Sprite[] FillTable()
        {
            int facings = BucketFillPresenter.Facings;
            string[] groups = CatchFillMath.BucketGroups;
            var flat = new Sprite[BucketFillPresenter.Bands.Length * groups.Length * facings];
            for (int b = 0; b < BucketFillPresenter.Bands.Length; b++)
                for (int g = 0; g < groups.Length; g++)
                    for (int d = 0; d < facings; d++)
                    {
                        var s = Sprite.Create(BlankTexture(), new Rect(0, 0, 4, 4),
                                              new Vector2(0.5f, 0.5f), 32f);
                        s.name = $"{BucketFillPresenter.Bands[b]}_{groups[g]}_d{d}";
                        _spawned.Add(s);
                        flat[(b * groups.Length + g) * facings + d] = s;
                    }
            return flat;
        }

        /// <summary>
        /// Push the hold through the bridge and settle the picture.
        ///
        /// <para>⚠️ <b>Both calls are load-bearing in EditMode and neither is a shortcut past the real
        /// path.</b> EditMode never fires <c>OnEnable</c> on a plain MonoBehaviour, so the bridge never
        /// subscribed to <c>FishCaught</c> and no frame ever runs to reach the presenter's
        /// <c>LateUpdate</c>. What these two force is exactly what the runtime does on the event edge and
        /// on the next frame — the same methods, called early. The SUBSCRIPTION itself (that landing a
        /// catch really does reach the bridge) is what the PlayMode carry smoke proves, because it is the
        /// one link EditMode structurally cannot.</para>
        /// </summary>
        private static void Settle(HoldCatchFillSource bridge, BucketFillPresenter presenter)
        {
            bridge.Refresh();
            presenter.RebuildNow();
        }

        /// <summary>The shipped tote slot geometry — the tote renderer draws nothing without it, and a
        /// test that let it draw nothing would pass over a broken renderer.</summary>
        private static TextAsset ToteAnchors()
        {
            var json = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Project/Art/Fishing/Storage/CatchStorageAnchors.json");
            Assert.That(json, Is.Not.Null,
                        "the committed storage anchors are missing — run Hidden Harbours ▸ Art ▸ Bake " +
                        "Catch Storage Kit");
            return json;
        }

        // ---- 1. THE PRESSED-E PATH -----------------------------------------------------------------------

        [Test]
        public void StoringAFish_ShowsItInThePail_ThroughTheWholeRealChain()
        {
            (CarriableBucket bucket, BucketFillPresenter presenter, HoldCatchFillSource bridge) = Pail();
            CarryHands hands = ToolInHand.Empty(_spawned);

            Settle(bridge, presenter);
            Assert.That(presenter.Band, Is.EqualTo(CatchFillBand.Empty), "an empty pail reads empty");
            Assert.That(presenter.ItemCount, Is.EqualTo(0));

            Assert.That(hands.TryPutInHand(Cod()), Is.True, "the fish is in her hand");
            Assert.That(bucket.UsedUnits, Is.EqualTo(0), "…and NOT yet in the pail");

            Assert.That(hands.TryGiveCatchTo(bucket), Is.True, "the press");

            Assert.That(bucket.UsedUnits, Is.EqualTo(1), "the hold took it");
            Settle(bridge, presenter);
            Assert.That(presenter.ItemCount, Is.EqualTo(1),
                        "⭐ THE DEFECT: the pail held a fish and the picture did not know");
            Assert.That(presenter.Band, Is.Not.EqualTo(CatchFillBand.Empty),
                        "one fish in a pail must never read EMPTY — that is the whole diegetic point");
            Assert.That(presenter.Group, Is.EqualTo("fish"), "a cod is a finfish");
        }

        [Test]
        public void StoringThreeFish_ShowsThree()
        {
            (CarriableBucket bucket, BucketFillPresenter presenter, HoldCatchFillSource bridge) = Pail();
            CarryHands hands = ToolInHand.Empty(_spawned);

            for (int i = 1; i <= 3; i++)
            {
                Assert.That(hands.TryPutInHand(Cod()), Is.True, $"fish {i} into her hand");
                Assert.That(hands.TryGiveCatchTo(bucket), Is.True, $"fish {i} into the pail");

                Settle(bridge, presenter);
                Assert.That(presenter.ItemCount, Is.EqualTo(i), $"after storing {i}");
                Assert.That(bucket.UsedUnits, Is.EqualTo(i));
            }
        }

        [Test]
        public void TheBandRises_AsThePailFills_AndOnlyABrimPailReadsBrim()
        {
            (CarriableBucket bucket, BucketFillPresenter presenter, HoldCatchFillSource bridge) =
                Pail(capacity: 4);
            CarryHands hands = ToolInHand.Empty(_spawned);

            var seen = new List<CatchFillBand>();
            for (int i = 0; i < 4; i++)
            {
                hands.TryPutInHand(Cod());
                hands.TryGiveCatchTo(bucket);
                Settle(bridge, presenter);
                seen.Add(presenter.Band);
            }

            Assert.That(seen[0], Is.Not.EqualTo(CatchFillBand.Empty), "one aboard is not empty");
            Assert.That(seen[3], Is.EqualTo(CatchFillBand.Brim), "four of four is brim");
            for (int i = 1; i < seen.Count; i++)
                Assert.That((int)seen[i], Is.GreaterThanOrEqualTo((int)seen[i - 1]),
                            "the read is MONOTONIC — a pail never looks emptier for gaining a fish");
        }

        // ---- 2. THE SECOND PICTURE ------------------------------------------------------------------------

        [Test]
        public void TheBridge_FeedsATotesRendererAndAPailsPresenter_BothAtOnce()
        {
            // The RequireComponent that used to be here made "a container has exactly one kind of picture,
            // and it is the tote's" a compile-time fact. It is not a fact.
            var go = new GameObject("Container");
            _spawned.Add(go);
            go.AddComponent<SpriteRenderer>();

            CatchItemLibrary library = Library("fish.atlantic_cod", "cod");
            var presenter = go.AddComponent<BucketFillPresenter>();
            presenter.Configure(library, EightSprites(), FillTable());
            var renderer = go.AddComponent<CatchFillRenderer>();
            renderer.Configure(library, ToteAnchors());

            var bucket = go.AddComponent<CarriableBucket>();
            bucket.Configure(8, go.GetComponent<SpriteRenderer>(), "container.test.both");
            var bridge = go.AddComponent<HoldCatchFillSource>();

            bucket.TryAdd(Cod());
            bucket.TryAdd(Cod());
            Settle(bridge, presenter);
            Assert.That(presenter.ItemCount, Is.EqualTo(2), "the pail's presenter was fed");
            Assert.That(presenter.Band, Is.Not.EqualTo(CatchFillBand.Empty));

            renderer.RebuildNow();
            Assert.That(renderer.VisibleItemCount, Is.EqualTo(2),
                        "…and so was the tote's renderer, on the same object, from the same bridge");
        }

        // ---- 3. NO CAPACITY IS NOT NO CATCH -----------------------------------------------------------------

        [Test]
        public void AHoldWithCatchAndNoDeclaredCapacity_StillReadsAsHoldingSomething()
        {
            // ⚠️ 0/0 is the ownership-gated pail and the not-yet-resolved proxy, and dividing it gives a
            // fill of ZERO — which draws an empty container over a hold with fish in it. Exactly the
            // reported symptom, from a different direction.
            var go = new GameObject("GatedHold");
            _spawned.Add(go);
            go.AddComponent<SpriteRenderer>();

            var presenter = go.AddComponent<BucketFillPresenter>();
            presenter.Configure(Library("fish.atlantic_cod", "cod"), EightSprites(), FillTable());
            var hold = go.AddComponent<ZeroCapacityHold>();
            hold.Items.Add(Cod());
            var bridge = go.AddComponent<HoldCatchFillSource>();

            Settle(bridge, presenter);

            Assert.That(presenter.ItemCount, Is.EqualTo(1));
            Assert.That(presenter.Band, Is.Not.EqualTo(CatchFillBand.Empty),
                        "a hold that holds something must never read EMPTY");
        }

        private sealed class ZeroCapacityHold : MonoBehaviour, IHold
        {
            public readonly List<CatchItem> Items = new List<CatchItem>();
            public int CapacityUnits => 0;
            public int UsedUnits => Items.Count;
            IReadOnlyList<CatchItem> IHold.Items => Items;
            public bool TryAdd(CatchItem item) { Items.Add(item); return true; }
            public void Clear() => Items.Clear();
        }

        // ---- 4. THE INDEX -----------------------------------------------------------------------------------

        [Test]
        public void TheFillTable_IsIndexedTheWayTheBuilderFillsIt()
        {
            var go = new GameObject("Pail");
            _spawned.Add(go);
            go.AddComponent<SpriteRenderer>();
            var presenter = go.AddComponent<BucketFillPresenter>();
            presenter.Configure(null, EightSprites(), FillTable());

            foreach (CatchFillBand band in BucketFillPresenter.Bands)
                foreach (string group in CatchFillMath.BucketGroups)
                    for (int d = 0; d < BucketFillPresenter.Facings; d++)
                        Assert.That(presenter.SpriteFor(band, group, d).name,
                                    Is.EqualTo($"{band}_{group}_d{d}"),
                                    "band × group × facing must land on its own sprite");
        }

        [Test]
        public void AnEmptyPail_AndAMissingState_BothDrawTheEmptySprite()
        {
            var go = new GameObject("Pail");
            _spawned.Add(go);
            go.AddComponent<SpriteRenderer>();
            var presenter = go.AddComponent<BucketFillPresenter>();
            Sprite[] empty = EightSprites();
            // A half-baked kit: no filled states at all.
            presenter.Configure(null, empty, new Sprite[BucketFillPresenter.Bands.Length * 3 *
                                                        BucketFillPresenter.Facings]);

            for (int d = 0; d < BucketFillPresenter.Facings; d++)
            {
                Assert.That(presenter.SpriteFor(CatchFillBand.Empty, null, d), Is.SameAs(empty[d]),
                            $"empty d{d}");
                Assert.That(presenter.SpriteFor(CatchFillBand.Brim, "fish", d), Is.SameAs(empty[d]),
                            $"a state with no art falls back to the empty pail at d{d}, never to nothing");
            }
        }

        [Test]
        public void TheCatchGroups_AreTheBucketRigsOwnThree()
        {
            Assert.That(CatchFillMath.BucketGroups, Is.EqualTo(new[] { "fish", "shell", "crust" }));

            foreach (string finfish in new[] { "cod", "haddock", "pollock", "mackerel" })
                Assert.That(CatchFillMath.BucketGroupFor(finfish), Is.EqualTo("fish"), finfish);
            foreach (string shell in new[] { "mussel", "clam" })
                Assert.That(CatchFillMath.BucketGroupFor(shell), Is.EqualTo("shell"), shell);
            foreach (string crust in new[] { "lobster", "crab" })
                Assert.That(CatchFillMath.BucketGroupFor(crust), Is.EqualTo("crust"), crust);

            Assert.That(CatchFillMath.BucketGroupFor("something_new"), Is.EqualTo("fish"),
                        "an unknown kind reads as the rig's own default rather than as nothing");
        }

        [Test]
        public void APailReadsAsWhateverMostOfItIs()
        {
            Assert.That(CatchFillMath.BucketGroupOf(new[] { "cod", "cod", "clam" }), Is.EqualTo("fish"));
            Assert.That(CatchFillMath.BucketGroupOf(new[] { "clam", "mussel", "cod" }), Is.EqualTo("shell"));
            Assert.That(CatchFillMath.BucketGroupOf(new[] { "lobster", "crab" }), Is.EqualTo("crust"));
            Assert.That(CatchFillMath.BucketGroupOf(new string[0]), Is.Null, "an empty pail has no group");
            Assert.That(CatchFillMath.BucketGroupOf(null), Is.Null);

            // A tie must be stable — a pail that flickered between two looks as it filled would read as
            // a bug even though both answers are defensible.
            Assert.That(CatchFillMath.BucketGroupOf(new[] { "cod", "clam" }), Is.EqualTo("fish"));
            Assert.That(CatchFillMath.BucketGroupOf(new[] { "clam", "cod" }), Is.EqualTo("fish"),
                        "the same tie, the other way round, still answers the same");
        }
    }
}
