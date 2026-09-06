using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE HOD FILLS AS SHE DIGS</b> — the clam basket's picture, pinned to the clams actually in the
    /// hold, through the real chain.
    ///
    /// <para><b>What was missing before this.</b> Catch pass 2 baked <c>Hod2_back</c> and
    /// <c>Hod2_front</c> — the two halves of the wire — and nothing that draws what goes between them.
    /// The rig's own recipe for a filling hod is a RUNTIME composition over <c>CatchKit2.heap</c>, which
    /// is JavaScript and cannot run in the player, so the hod could be drawn and carried but never
    /// filled: <see cref="ClamDig"/> incremented a number nobody could see. The heap is now baked per
    /// band and this presenter swaps the middle sprite.</para>
    ///
    /// <para>The claims, in the order they would break:
    /// <list type="number">
    ///   <item><b>THE DUG CLAM SHOWS.</b> Driven through <see cref="ClamDig.TryDig"/> →
    ///   <see cref="IHold.TryAdd"/> → <c>FishCaught</c> → <see cref="HoldCatchFillSource"/> → the
    ///   presenter, because a test that poked the presenter directly would pass over a disconnected
    ///   game.</item>
    ///   <item><b>THE LADDER IS THE SHARED ONE.</b> Which band a fill reads as is
    ///   <see cref="CatchFillMath.BandFor"/> and nothing local — one clam is never EMPTY, a full hod is
    ///   BRIM, and the steps in between are the rig's own fractions.</item>
    ///   <item><b>THREE LAYERS, ONE TRANSFORM.</b> Back and front never change with the fill; only the
    ///   middle does, and it goes away entirely when the hod is empty.</item>
    ///   <item><b>THE INDEX.</b> The flattened heap table is read the way the bake fills it — a table
    ///   read in a different order draws a plausible hod at the wrong fullness.</item>
    /// </list></para>
    /// </summary>
    public class HodFillTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation = 1.0f;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level = 0.5f;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchLanded>();
            GameServices.Reset();
            Interactables.Clear();
            InteractVerb.Reset();

            var save = SaveMigration.NewGame();
            save.OwnedGear.Add("gear.shovel");
            GameServices.TidalTerrain = new FlatTerrain();
            GameServices.Environment = new FlatEnv();
            GameServices.Save = new FakeSaveService(save);
            ToolInHand.Holding(ToolDef.ShovelId, _spawned);
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

        private Texture2D BlankTexture()
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            return tex;
        }

        private Sprite Named(string name)
        {
            var s = Sprite.Create(BlankTexture(), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
            s.name = name;
            _spawned.Add(s);
            return s;
        }

        /// <summary>Eight distinct, NAMED sprites — so an index mistake shows up as the wrong sprite
        /// rather than as the same one twice.</summary>
        private Sprite[] ByDir(string stem)
        {
            var sprites = new Sprite[HodFillPresenter.Facings];
            for (int d = 0; d < sprites.Length; d++) sprites[d] = Named($"{stem}_d{d}");
            return sprites;
        }

        /// <summary>The heap table as the bake lays it out: [band × 8 + dir], band in the shared
        /// few/half/full/brim order.</summary>
        private Sprite[] HeapTable()
        {
            var bands = CatchFillMath.FilledBands;
            var flat = new Sprite[bands.Length * HodFillPresenter.Facings];
            for (int b = 0; b < bands.Length; b++)
                for (int d = 0; d < HodFillPresenter.Facings; d++)
                    flat[b * HodFillPresenter.Facings + d] = Named($"Hod2_heap_{bands[b]}_d{d}");
            return flat;
        }

        private CatchItemLibrary Library()
        {
            var library = ScriptableObject.CreateInstance<CatchItemLibrary>();
            _spawned.Add(library);

            var variants = new Sprite[CatchFillMath.Variants];
            for (int v = 0; v < variants.Length; v++) variants[v] = Named($"clam_v{v}");

            library.Configure(
                new[] { new CatchItemLibrary.KindEntry { Kind = "clam", Variants = variants } },
                new[] { new CatchItemLibrary.SpeciesEntry { SpeciesId = "fish.soft_shell_clam", Kind = "clam" } },
                fallbackKind: "clam");
            return library;
        }

        private FishSpeciesDef Clam()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.soft_shell_clam"; f.DisplayName = "Soft-shell Clam";
            f.Category = FishCategory.Shellfish;
            f.MinWeightKg = 0.05f; f.MaxWeightKg = 0.2f; f.BaseValue = 2; f.SupplyElasticity = 0.45f;
            _spawned.Add(f);
            return f;
        }

        private SpriteRenderer Layer(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            _spawned.Add(go);
            return go.AddComponent<SpriteRenderer>();
        }

        /// <summary>The hod as a builder would stand it up: a hold, three stacked renderers, the
        /// presenter and the Core bridge between them, on one object.</summary>
        private (CarriableBucket hold, HodFillPresenter hod, HoldCatchFillSource bridge) Hod(int capacity = 20)
        {
            var go = new GameObject("ClamHod");
            _spawned.Add(go);

            SpriteRenderer back = Layer(go, "Back"), heap = Layer(go, "Heap"), front = Layer(go, "Front");

            var hod = go.AddComponent<HodFillPresenter>();
            hod.ConfigureRenderers(back, heap, front);
            hod.Configure(Library(), ByDir("Hod2_back"), ByDir("Hod2_front"), HeapTable());

            var hold = go.AddComponent<CarriableBucket>();
            hold.Configure(capacity, go.AddComponent<SpriteRenderer>(), "container.test.hod");

            var bridge = go.AddComponent<HoldCatchFillSource>();
            return (hold, hod, bridge);
        }

        private ClamDig DigAt(IHold hold, FishSpeciesDef clam)
        {
            var go = new GameObject("ClamHole");
            _spawned.Add(go);
            go.transform.position = Vector2.zero;
            var d = go.AddComponent<ClamDig>();
            d.Configure(clam, hold, go.transform, "gear.shovel", seed: 7);
            d.ConfigureInteract($"fixture.clam_hole#{_spawned.Count}", ToolDef.ShovelId, landInHand: false);
            return d;
        }

        /// <summary>⚠️ Both calls are load-bearing in EditMode and neither is a shortcut past the real
        /// path: EditMode never fires <c>OnEnable</c>, so the bridge never subscribed, and no frame runs
        /// to reach <c>LateUpdate</c>. These force exactly what the runtime does on the event edge and on
        /// the next frame.</summary>
        private static void Settle(HoldCatchFillSource bridge, HodFillPresenter hod)
        {
            bridge.Refresh();
            hod.RebuildNow();
        }

        /// <summary>Dig <paramref name="n"/> clams into the hold — a fresh hole each time, because a hole
        /// yields once.</summary>
        private void Dig(IHold hold, FishSpeciesDef clam, int n)
        {
            for (int i = 0; i < n; i++)
                Assert.IsTrue(DigAt(hold, clam).TryDig(), $"dig {i + 1} of {n} lands a clam");
        }

        // ---- 1. THE DUG CLAM SHOWS -----------------------------------------------------------------------

        [Test]
        public void DiggingAClam_FillsTheHod_ThroughTheWholeRealChain()
        {
            (CarriableBucket hold, HodFillPresenter hod, HoldCatchFillSource bridge) = Hod();
            FishSpeciesDef clam = Clam();

            Settle(bridge, hod);
            Assert.That(hod.Band, Is.EqualTo(CatchFillBand.Empty), "an empty hod reads empty");
            Assert.That(hod.HeapSpriteFor(hod.Band, 0), Is.Null, "…and draws no heap at all");

            Dig(hold, clam, 1);
            Assert.That(hold.UsedUnits, Is.EqualTo(1), "the hold took the clam");

            Settle(bridge, hod);
            Assert.That(hod.ItemCount, Is.EqualTo(1), "the hod's picture knows the hold gained one");
            Assert.That(hod.Band, Is.Not.EqualTo(CatchFillBand.Empty),
                        "⭐ one clam in a hod must never read EMPTY — being able to see what you have " +
                        "picked up is the whole diegetic point");
            Assert.That(hod.HeapSpriteFor(hod.Band, 0), Is.Not.Null, "there is a heap to draw");
        }

        // ---- 2. THE LADDER IS THE SHARED ONE -------------------------------------------------------------

        [Test]
        public void TheBandLadder_IsCatchFillMaths_NotASecondOpinion()
        {
            (CarriableBucket hold, HodFillPresenter hod, HoldCatchFillSource bridge) = Hod(capacity: 20);
            FishSpeciesDef clam = Clam();

            // Every step of the ladder, and each asserted against the SHARED rule rather than against a
            // literal here — a threshold table of this lane's own is exactly what must not exist.
            foreach (int dug in new[] { 1, 5, 8, 11, 17, 20 })
            {
                hold.Clear();
                Dig(hold, clam, dug);
                Settle(bridge, hod);

                CatchFillBand want = CatchFillMath.BandFor(dug / 20f, dug);
                Assert.That(hod.Band, Is.EqualTo(want), $"{dug}/20 clams");
            }
        }

        [Test]
        public void AFullHodIsBrim_AndAnEmptyOneIsEmpty()
        {
            (CarriableBucket hold, HodFillPresenter hod, HoldCatchFillSource bridge) = Hod(capacity: 20);
            FishSpeciesDef clam = Clam();

            Dig(hold, clam, 20);
            Settle(bridge, hod);
            Assert.That(hod.Band, Is.EqualTo(CatchFillBand.Brim), "a hod dug to capacity is heaped");

            hold.Clear();
            Settle(bridge, hod);
            Assert.That(hod.Band, Is.EqualTo(CatchFillBand.Empty), "tipped out, it is a basket again");
        }

        // ---- 3. THREE LAYERS, ONE TRANSFORM --------------------------------------------------------------

        [Test]
        public void OnlyTheMiddleLayerChangesWithTheFill()
        {
            (CarriableBucket hold, HodFillPresenter hod, HoldCatchFillSource bridge) = Hod();
            FishSpeciesDef clam = Clam();
            hod.SetDirection(3);

            Settle(bridge, hod);
            Sprite back = hod.transform.Find("Back").GetComponent<SpriteRenderer>().sprite;
            Sprite front = hod.transform.Find("Front").GetComponent<SpriteRenderer>().sprite;
            SpriteRenderer heapRenderer = hod.transform.Find("Heap").GetComponent<SpriteRenderer>();

            Assert.That(back.name, Is.EqualTo("Hod2_back_d3"), "the far half is drawn at the hod's facing");
            Assert.That(front.name, Is.EqualTo("Hod2_front_d3"), "and so is the near half");
            Assert.That(heapRenderer.enabled, Is.False, "an empty hod draws no catch layer");

            Dig(hold, clam, 20);
            Settle(bridge, hod);

            Assert.That(hod.transform.Find("Back").GetComponent<SpriteRenderer>().sprite, Is.EqualTo(back),
                        "the wire does not change because the basket filled");
            Assert.That(hod.transform.Find("Front").GetComponent<SpriteRenderer>().sprite, Is.EqualTo(front),
                        "…on either side of the catch");
            Assert.That(heapRenderer.enabled, Is.True, "the catch layer comes on");
            Assert.That(heapRenderer.sprite.name, Is.EqualTo("Hod2_heap_Brim_d3"),
                        "and it is this band at this facing");
        }

        // ---- 4. THE INDEX --------------------------------------------------------------------------------

        [Test]
        public void TheHeapTable_IsReadTheWayTheBakeFillsIt()
        {
            (CarriableBucket _, HodFillPresenter hod, HoldCatchFillSource __) = Hod();

            var bands = CatchFillMath.FilledBands;
            for (int b = 0; b < bands.Length; b++)
                for (int d = 0; d < HodFillPresenter.Facings; d++)
                    Assert.That(hod.HeapSpriteFor(bands[b], d).name,
                                Is.EqualTo($"Hod2_heap_{bands[b]}_d{d}"),
                                $"band {bands[b]} at dir {d}");

            Assert.That(hod.HeapSpriteFor(CatchFillBand.Empty, 0), Is.Null,
                        "'empty' heaps nothing and has no sheet — it must not fall through to a band");

            // Facings wrap rather than throw: a caller handing a raw heading must not tear the picture.
            Assert.That(hod.HeapSpriteFor(CatchFillBand.Half, 8).name, Is.EqualTo("Hod2_heap_Half_d0"));
            Assert.That(hod.HeapSpriteFor(CatchFillBand.Half, -1).name, Is.EqualTo("Hod2_heap_Half_d7"));
        }

        [Test]
        public void TheFilledBandOrder_IsOneTableSharedWithThePail()
        {
            // The hod and the pail index their baked-state tables with the same array. Two copies of this
            // order is how one container ends up drawing 'full' where the other draws 'half'.
            Assert.That(BucketFillPresenter.Bands, Is.SameAs(CatchFillMath.FilledBands));
            CollectionAssert.AreEqual(
                new[] { CatchFillBand.Few, CatchFillBand.Half, CatchFillBand.Full, CatchFillBand.Brim },
                CatchFillMath.FilledBands);
        }
    }
}
