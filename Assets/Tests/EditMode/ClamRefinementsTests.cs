using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters CLAM refinements (owner playtest feedback):
    /// <list type="number">
    /// <item>A hole yields ONCE — after a successful dig it's spent (<see cref="ClamDig.Consumed"/>) and
    /// won't dig again (the clam's gone).</item>
    /// <item>SKITTISH clams — if the on-foot player lingers within the escape radius for the escape time the
    /// clam burrows away (the visual sinks the hole, marks the dig consumed); the linger timer RESETS if the
    /// player leaves the radius before it fires.</item>
    /// <item>The squirt is a SEPARATE overlay renderer ON TOP of the holes, not a sprite-swap — the two-holes
    /// base renderer stays visible while the squirt overlay plays.</item>
    /// </list>
    /// Drives the components directly with fake terrain/env/save/hold and a primed player beacon — no scene,
    /// no game loop (the visual exposes a public Tick + observers; the digger publishes the beacon).
    /// </summary>
    public class ClamRefinementsTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int Capacity = 20;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= Capacity) return false; _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
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

        private readonly List<Object> _spawned = new();
        private int _caught;
        private void OnCaught(FishCaught e) => _caught++;

        private FlatTerrain _terrain;
        private FlatEnv _env;
        private SaveData _save;

        [SetUp]
        public void SetUp()
        {
            _caught = 0;
            EventBus.Clear<FishCaught>();
            EventBus.Subscribe<FishCaught>(OnCaught);
            GameServices.Reset();
            ClamDigger.ClearPlayerPosition();

            _terrain = new FlatTerrain { Elevation = 1.0f };   // the flat sits 1.0 m above datum
            _env = new FlatEnv { Level = 0.5f };               // low water 0.5 m → the flat is bared (exposed)
            _save = SaveMigration.NewGame();
            _save.OwnedGear.Add("gear.shovel");

            GameServices.TidalTerrain = _terrain;
            GameServices.Environment = _env;
            GameServices.Save = new FakeSaveService(_save);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<FishCaught>(OnCaught);
            EventBus.Clear<FishCaught>();
            GameServices.Reset();
            ClamDigger.ClearPlayerPosition();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishSpeciesDef MakeClam()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = "fish.soft_shell_clam"; f.DisplayName = "Soft-shell Clam";
            f.Category = FishCategory.Shellfish;
            f.MinWeightKg = 0.05f; f.MaxWeightKg = 0.2f; f.BaseValue = 2; f.SupplyElasticity = 0.45f;
            _spawned.Add(f);
            return f;
        }

        private ClamDig MakeDigAt(IHold bucket, FishSpeciesDef clam, Vector2 spotPos, float reach = 1.25f, int seed = 7)
        {
            var go = new GameObject("ClamHole");
            _spawned.Add(go);
            go.transform.position = spotPos;
            var d = go.AddComponent<ClamDig>();
            d.Configure(clam, bucket, go.transform, "gear.shovel", seed, reach);
            return d;
        }

        // A visual on its own GameObject (so Awake's child-overlay creation has a clean transform), wired to a
        // dig at the same position. A tiny solid sprite stands in for the imported holes/squirt art.
        private ClamHoleVisual MakeVisualFor(ClamDig dig, Sprite holeSprite, Sprite[] squirtFrames)
        {
            var visual = dig.gameObject.AddComponent<ClamHoleVisual>();
            visual.Configure(dig, holeSprite, squirtFrames);
            return visual;
        }

        private Sprite MakeSprite()
        {
            var tex = new Texture2D(2, 2);
            _spawned.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 16f);
            _spawned.Add(s);
            return s;
        }

        // Prime the static player-position beacon (as a live ClamDigger would each frame).
        private void PrimePlayerAt(Vector2 pos)
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            go.transform.position = pos;
            var digger = go.AddComponent<ClamDigger>();
            digger.Configure(go.transform);
            digger.PublishPlayerPosition();
        }

        // ---- 1. VANISH AFTER A DIG: a hole yields once, then it's spent ------------------------------------

        [Test]
        public void Dig_YieldsOnce_ThenHoleIsSpent()
        {
            var hold = new FakeHold();
            var dig = MakeDigAt(hold, MakeClam(), Vector2.zero);

            Assert.IsFalse(dig.Consumed, "a fresh hole isn't spent");
            Assert.IsTrue(dig.TryDig(), "the first dig on the bared hole yields a clam");
            Assert.IsTrue(dig.Consumed, "after a successful dig the hole is spent");
            Assert.AreEqual(1, hold.UsedUnits);

            Assert.IsFalse(dig.TryDig(), "the spent hole yields nothing on a second dig (the clam's gone)");
            Assert.AreEqual(1, hold.UsedUnits, "still just the one clam — a hole yields ONCE");
            Assert.AreEqual(1, _caught, "FishCaught fired exactly once");
        }

        [Test]
        public void Visual_Vanishes_WhenItsDigIsDug()
        {
            var hold = new FakeHold();
            var dig = MakeDigAt(hold, MakeClam(), Vector2.zero);
            var visual = MakeVisualFor(dig, MakeSprite(), null);

            visual.Tick(0.1f);
            Assert.IsTrue(visual.HoleRenderer.enabled, "the holes show while exposed");

            Assert.IsTrue(dig.TryDig(), "dig the hole");
            visual.Tick(0.1f);

            Assert.IsTrue(visual.IsHidden, "a dug hole vanishes");
            Assert.IsFalse(visual.HoleRenderer.enabled, "the holes are gone once dug");
        }

        // ---- 2. SKITTISH CLAMS: proximity-escape timer fires, and resets when the player leaves ------------

        [Test]
        public void Escape_FiresAfterLingeringInsideRadius_SinksThenHidesAndConsumes()
        {
            var hold = new FakeHold();
            var dig = MakeDigAt(hold, MakeClam(), Vector2.zero);
            var visual = MakeVisualFor(dig, MakeSprite(), null);
            visual.ConfigureEscape(escapeRadius: 2.0f, escapeSeconds: 4.0f, sinkSeconds: 0.5f);

            PrimePlayerAt(new Vector2(1.0f, 0f));   // 1.0 m away → inside the 2.0 m escape radius

            // Linger for 3 s — under the 4 s threshold, so the clam hasn't bolted yet.
            for (int i = 0; i < 30; i++) visual.Tick(0.1f);
            Assert.IsFalse(visual.IsSinking, "3 s of lingering is under the 4 s threshold — not yet spooked");
            Assert.IsFalse(dig.Consumed, "the hole is still diggable while the clam holds");

            // Cross the 4 s threshold → the clam burrows away (sink starts, dig spent).
            for (int i = 0; i < 15; i++) visual.Tick(0.1f);   // +1.5 s → past 4 s
            Assert.IsTrue(dig.Consumed, "lingering past the escape time spends the hole (the clam escaped)");
            Assert.IsFalse(dig.TryDig(), "nothing left to dig — the clam got away");

            // Let the sink animation finish → the hole is hidden for good.
            for (int i = 0; i < 10; i++) visual.Tick(0.1f);   // +1.0 s > 0.5 s sink
            Assert.IsTrue(visual.IsHidden, "after the sink-into-the-sand animation the hole is gone");
            Assert.IsFalse(visual.HoleRenderer.enabled, "the holes have sunk away");
        }

        [Test]
        public void Escape_TimerResets_WhenPlayerLeavesRadiusBeforeItFires()
        {
            var hold = new FakeHold();
            var dig = MakeDigAt(hold, MakeClam(), Vector2.zero);
            var visual = MakeVisualFor(dig, MakeSprite(), null);
            visual.ConfigureEscape(escapeRadius: 2.0f, escapeSeconds: 4.0f, sinkSeconds: 0.5f);

            // Linger 3 s inside the radius (under the 4 s threshold)...
            PrimePlayerAt(new Vector2(1.0f, 0f));
            for (int i = 0; i < 30; i++) visual.Tick(0.1f);
            Assert.Greater(visual.LingerTimer, 2.5f, "the linger timer was counting up while inside the radius");

            // ...then step well outside the radius: the timer must RESET.
            PrimePlayerAt(new Vector2(10.0f, 0f));   // 10 m away → outside the 2 m radius
            visual.Tick(0.1f);
            Assert.AreEqual(0f, visual.LingerTimer, 1e-4f, "leaving the radius resets the linger timer");

            // Even after a further 3 s away + back-and-forth shorter than the threshold, it never fires.
            for (int i = 0; i < 30; i++) visual.Tick(0.1f);
            Assert.IsFalse(visual.IsSinking, "a player who doesn't loiter the full time never spooks the clam");
            Assert.IsFalse(dig.Consumed, "the hole stays diggable — approach promptly, don't loiter");
            Assert.IsTrue(dig.TryDig(), "and you can still dig it (it never escaped)");
        }

        [Test]
        public void Escape_DoesNotFire_WhenNoPlayerBeacon()
        {
            var hold = new FakeHold();
            var dig = MakeDigAt(hold, MakeClam(), Vector2.zero);
            var visual = MakeVisualFor(dig, MakeSprite(), null);
            visual.ConfigureEscape(escapeRadius: 2.0f, escapeSeconds: 1.0f, sinkSeconds: 0.5f);

            // No PrimePlayerAt → the beacon is empty; the timer must never accumulate.
            for (int i = 0; i < 30; i++) visual.Tick(0.1f);
            Assert.AreEqual(0f, visual.LingerTimer, 1e-4f, "no player beacon → no linger");
            Assert.IsFalse(dig.Consumed, "no player nearby → the clam never bolts");
        }

        // ---- 3. SQUIRT OVERLAY: a separate renderer ON TOP, holes stay visible ----------------------------

        [Test]
        public void Squirt_IsASeparateOverlayRenderer_NotASpriteSwap()
        {
            var hold = new FakeHold();
            var dig = MakeDigAt(hold, MakeClam(), Vector2.zero);
            var holeSprite = MakeSprite();
            var squirtFrames = new[] { MakeSprite(), MakeSprite() };
            var visual = MakeVisualFor(dig, holeSprite, squirtFrames);

            // The overlay is a DIFFERENT renderer from the base holes renderer, drawn one order on top.
            Assert.IsNotNull(visual.SquirtRenderer, "the squirt has its own overlay renderer");
            Assert.AreNotSame(visual.HoleRenderer, visual.SquirtRenderer, "overlay is a separate renderer, not the base");
            Assert.AreEqual(visual.HoleRenderer.sortingOrder + 1, visual.SquirtRenderer.sortingOrder,
                "the squirt overlay draws one order ON TOP of the holes");

            // Quiet (no squirt): holes visible, overlay off.
            visual.Tick(0.1f);
            Assert.IsTrue(visual.HoleRenderer.enabled, "the holes show while exposed");
            Assert.AreEqual(holeSprite, visual.HoleRenderer.sprite, "the base renderer shows the two-holes sprite");
            Assert.IsFalse(visual.SquirtRenderer.enabled, "no overlay while the tell is quiet");
        }

        [Test]
        public void Squirt_LeavesTheHolesVisibleUnderneath_WhenItPlays()
        {
            var hold = new FakeHold();
            var dig = MakeDigAt(hold, MakeClam(), Vector2.zero);
            var holeSprite = MakeSprite();
            var squirtFrames = new[] { MakeSprite(), MakeSprite() };
            var visual = MakeVisualFor(dig, holeSprite, squirtFrames);

            // Force the dig's squirt tell on (drive the cosmetic cadence to "showing").
            DriveSquirtOn(dig);
            Assert.IsTrue(dig.ShowingSquirt, "the squirt tell is showing");

            visual.Tick(0.1f);

            // The HOLES stay visible (this is the fix: the squirt no longer replaces the holes)...
            Assert.IsTrue(visual.HoleRenderer.enabled, "the two-holes base renderer REMAINS visible while squirting");
            Assert.AreEqual(holeSprite, visual.HoleRenderer.sprite, "the base renderer still shows the holes (not a squirt frame)");

            // ...and the squirt plays as an ADDED overlay on top.
            Assert.IsTrue(visual.SquirtRenderer.enabled, "the squirt overlay is on top while the tell shows");
            Assert.Contains(visual.SquirtRenderer.sprite, squirtFrames, "the overlay shows a squirt frame");
        }

        // Tick the dig's cosmetic reveal cadence until its squirt cue is showing (it flips on a 10–20 s gap).
        // We drive plenty of time so the gap elapses regardless of the seeded initial value.
        private static void DriveSquirtOn(ClamDig dig)
        {
            var update = typeof(ClamDig).GetMethod("UpdateReveal",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int i = 0; i < 3000 && !dig.ShowingSquirt; i++)
                update.Invoke(dig, new object[] { 0.1f, true });
        }
    }
}
