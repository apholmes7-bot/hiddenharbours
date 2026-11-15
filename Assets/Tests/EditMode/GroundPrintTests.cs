using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Footprints and tyre marks (owner ask 2026-11-15: "the print lasts 24/h with a gradual fade"):
    /// the fade law, the ring's capacity law, which ground takes a print, the tide wiping wet sand,
    /// and the spacing the data asks for. All engine-light: no scene, no clock service.
    /// </summary>
    public class GroundPrintTests
    {
        private const double SecondsPerDay = 1800d;   // the shipped day: 30 real minutes
        private static readonly double Lifetime = GroundPrintMath.LifetimeSeconds(24d, SecondsPerDay);

        private readonly List<GroundPrintMade> _published = new List<GroundPrintMade>();
        private void Capture(GroundPrintMade m) => _published.Add(m);

        [SetUp]
        public void SetUp()
        {
            _published.Clear();
            EventBus.Subscribe<GroundPrintMade>(Capture);
        }

        [TearDown]
        public void TearDown() => EventBus.Unsubscribe<GroundPrintMade>(Capture);

        /// <summary>A flat test ground: one surface everywhere, elevation = y, water level by time.</summary>
        private sealed class FakeGround : IGroundPrintGround
        {
            public GroundSurface Surface = GroundSurface.Sand;
            public Func<double, float> WaterLevel = _ => -10f;
            public GroundSurface SurfaceAt(Vector2 p) => Surface;
            public bool IsWashed(Vector2 p, double now) => GroundPrintMath.IsWashed(WaterLevel(now), p.y);
        }

        private static GroundPrintMade Foot(Vector2 at, double time)
            => new GroundPrintMade(at, Vector2.up, GroundPrintKind.FootLeft, 0.28f, 0.11f, 0f, time);

        // ---- the fade -------------------------------------------------------------------------------

        [Test]
        public void ShippedLifetimeIs24GameHours()
        {
            Assert.That(GroundPrintSettings.Default.LifetimeGameHours, NUnit.Framework.Is.EqualTo(24f));
            Assert.That(Lifetime, NUnit.Framework.Is.EqualTo(SecondsPerDay));
        }

        [Test]
        public void FadeIsFullWhenFreshAndZeroAtExactly24GameHours([Values(0.5f, 1f, 2f)] float exponent)
        {
            Assert.That(GroundPrintMath.Strength(0d, 0d, Lifetime, exponent), NUnit.Framework.Is.EqualTo(1f));
            Assert.That(GroundPrintMath.Strength(0d, Lifetime - 0.01d, Lifetime, exponent), NUnit.Framework.Is.GreaterThan(0f));
            Assert.That(GroundPrintMath.Strength(0d, Lifetime, Lifetime, exponent), NUnit.Framework.Is.EqualTo(0f));
            Assert.That(GroundPrintMath.Strength(0d, Lifetime * 3d, Lifetime, exponent), NUnit.Framework.Is.EqualTo(0f));
            Assert.That(GroundPrintMath.IsGone(0d, Lifetime, Lifetime), NUnit.Framework.Is.True);
            Assert.That(GroundPrintMath.IsGone(0d, Lifetime - 0.01d, Lifetime), NUnit.Framework.Is.False);
        }

        [Test]
        public void FadeIsMonotonic([Values(0.5f, 1f, 2f)] float exponent)
        {
            float previous = float.MaxValue, previousBanded = float.MaxValue;
            for (int i = 0; i <= 1000; i++)
            {
                double now = Lifetime * i / 1000d;
                float s = GroundPrintMath.Strength(0d, now, Lifetime, exponent);
                float b = GroundPrintMath.Banded(s, 6);
                Assert.That(s, NUnit.Framework.Is.LessThanOrEqualTo(previous), $"raw fade rose at step {i}");
                Assert.That(b, NUnit.Framework.Is.LessThanOrEqualTo(previousBanded), $"banded fade rose at step {i}");
                if (i < 1000) Assert.That(b, NUnit.Framework.Is.GreaterThan(0f), "a banded print vanished before 24 h");
                previous = s; previousBanded = b;
            }
        }

        [Test]
        public void StrengthDependsOnlyOnMadeAtAndNow()
        {
            // The same age gives the same strength whenever it was made...
            Assert.That(GroundPrintMath.Strength(100d, 700d, Lifetime, 1f),
                NUnit.Framework.Is.EqualTo(GroundPrintMath.Strength(5000d, 5600d, Lifetime, 1f)));
            // ...and a clock that JUMPS (sleep, fast-forward) lands on exactly what a slow walk there reads.
            float jumped = GroundPrintMath.Strength(0d, 1234.5d, Lifetime, 1.5f);
            float walked = 0f;
            for (double t = 0d; t <= 1234.5d; t += 0.5d) walked = GroundPrintMath.Strength(0d, t, Lifetime, 1.5f);
            Assert.That(walked, NUnit.Framework.Is.EqualTo(jumped));
        }

        [Test]
        public void APrintFromARewoundClockIsGone()
        {
            Assert.That(GroundPrintMath.Strength(500d, 400d, Lifetime, 1f), NUnit.Framework.Is.EqualTo(0f));
            Assert.That(GroundPrintMath.IsGone(500d, 400d, Lifetime), NUnit.Framework.Is.True);
        }

        // ---- the ring -------------------------------------------------------------------------------

        [Test]
        public void RingDropsTheOldestWhenFull()
        {
            var ring = new GroundPrintRing(4);
            for (int i = 0; i < 6; i++) ring.Add(new GroundPrint { MadeAt = i });
            Assert.That(ring.Written, NUnit.Framework.Is.EqualTo(4));
            Assert.That(ring.CountAlive(), NUnit.Framework.Is.EqualTo(4));
            var kept = new List<double>();
            for (int i = 0; i < ring.Capacity; i++) kept.Add(ring[i].MadeAt);
            kept.Sort();
            Assert.That(kept, NUnit.Framework.Is.EqualTo(new List<double> { 2, 3, 4, 5 }));
        }

        [Test]
        public void RingNeverAllocatesAfterConstruction()
        {
            var ring = new GroundPrintRing(64);
            var print = new GroundPrint { Position = Vector2.one, Heading = Vector2.up, Surface = GroundSurface.Sand };
            ring.Add(print);   // warm-up
            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++) { print.MadeAt = i; ring.Add(print); }
                ring.Kill(3);
                ring.CountAlive();
            }, Is.Not.AllocatingGCMemory());
            Assert.That(ring.Capacity, NUnit.Framework.Is.EqualTo(64));
            Assert.That(ring.Written, NUnit.Framework.Is.EqualTo(64));
        }

        // ---- which ground takes a print -------------------------------------------------------------

        [Test]
        public void OnlyGroundThatTakesPrintsGetsThem()
        {
            GroundPrintSettings s = GroundPrintSettings.Default;
            Assert.That(s.SurfaceForZone("grass"), NUnit.Framework.Is.EqualTo(GroundSurface.Grass));
            Assert.That(s.SurfaceForZone("sand"), NUnit.Framework.Is.EqualTo(GroundSurface.Sand));
            Assert.That(s.SurfaceForZone("mud"), NUnit.Framework.Is.EqualTo(GroundSurface.Mud));
            Assert.That(s.SurfaceForZone("dirt"), NUnit.Framework.Is.EqualTo(GroundSurface.Dirt));
            foreach (string hard in new[] { "shingle", "shelf", "talus", "ledge", "rockweed", "musselbed", "oysterreef", "eelgrass", "", null })
                Assert.That(s.SurfaceForZone(hard), NUnit.Framework.Is.EqualTo(GroundSurface.None), hard ?? "null");

            var layer = new GroundPrintLayer(16);
            var rock = new FakeGround { Surface = GroundSurface.None };
            Assert.That(layer.Accept(Foot(Vector2.zero, 0d), rock), NUnit.Framework.Is.EqualTo(0));
            Assert.That(layer.Ring.Written, NUnit.Framework.Is.EqualTo(0));

            var sand = new FakeGround();
            Assert.That(layer.Accept(Foot(Vector2.zero, 0d), sand), NUnit.Framework.Is.EqualTo(1));
            Assert.That(layer.Ring[0].Surface, NUnit.Framework.Is.EqualTo(GroundSurface.Sand));
        }

        [Test]
        public void EverySurfaceThatTakesPrintsHasAStyle()
        {
            GroundPrintSettings s = GroundPrintSettings.Default;
            foreach (GroundZoneSurface z in s.Zones)
                Assert.That(s.TryGetStyle(z.Surface, out _), NUnit.Framework.Is.True, z.Zone);
            Assert.That(s.TryGetStyle(GroundSurface.None, out _), NUnit.Framework.Is.False);
        }

        [Test]
        public void ATyrePairLaysTwoMarksAGaugeApart()
        {
            var layer = new GroundPrintLayer(16);
            var m = new GroundPrintMade(new Vector2(5f, 5f), Vector2.up, GroundPrintKind.TirePair, 0.5f, 0.25f, 1.4f, 0d);
            Assert.That(layer.Accept(m, new FakeGround()), NUnit.Framework.Is.EqualTo(2));
            float gap = Vector2.Distance(layer.Ring[0].Position, layer.Ring[1].Position);
            Assert.That(gap, NUnit.Framework.Is.EqualTo(1.4f).Within(1e-5f));
            Assert.That(layer.Ring[0].Position.y, NUnit.Framework.Is.EqualTo(5f).Within(1e-5f), "pair sits across the heading");
        }

        // ---- the tide -------------------------------------------------------------------------------

        [Test]
        public void ThePrintTheTideCoversIsWipedAndTheOneAboveSurvives()
        {
            var ground = new FakeGround { WaterLevel = t => t < 100d ? 0f : 1f };   // the tide comes in at t=100
            var layer = new GroundPrintLayer(16);
            layer.Accept(Foot(new Vector2(0f, 0.5f), 10d), ground);   // wet sand, dry at low tide
            layer.Accept(Foot(new Vector2(0f, 2f), 10d), ground);     // above the high water
            Assert.That(layer.Ring.CountAlive(), NUnit.Framework.Is.EqualTo(2));

            layer.Sweep(50d, Lifetime, ground, 16);
            Assert.That(layer.Ring.CountAlive(), NUnit.Framework.Is.EqualTo(2), "still low tide");

            layer.Sweep(150d, Lifetime, ground, 16);
            Assert.That(layer.Ring[0].Alive, NUnit.Framework.Is.False, "the sea covered it");
            Assert.That(layer.Ring[1].Alive, NUnit.Framework.Is.True, "the sea never reached it");
        }

        [Test]
        public void NoPrintIsMadeUnderWater()
        {
            var ground = new FakeGround { WaterLevel = _ => 1f };
            var layer = new GroundPrintLayer(16);
            Assert.That(layer.Accept(Foot(new Vector2(0f, 0.5f), 0d), ground), NUnit.Framework.Is.EqualTo(0));
        }

        [Test]
        public void TheSweepRetiresPrintsAtExactly24GameHours()
        {
            var ground = new FakeGround();
            var layer = new GroundPrintLayer(16);
            layer.Accept(Foot(Vector2.zero, 0d), ground);
            layer.Sweep(Lifetime - 1d, Lifetime, ground, 16);
            Assert.That(layer.Ring[0].Alive, NUnit.Framework.Is.True);
            layer.Sweep(Lifetime, Lifetime, ground, 16);
            Assert.That(layer.Ring[0].Alive, NUnit.Framework.Is.False);
        }

        // ---- spacing from the data ------------------------------------------------------------------

        [Test]
        public void BootPrintsAreAStrideApartAndAlternate()
        {
            GroundPrintSettings s = GroundPrintSettings.Default;
            s.StrideMetres = 0.75f;
            s.FootSideOffsetMetres = 0.09f;
            var trail = new FootPrintTrail();
            for (int i = 0; i <= 24; i++) trail.Track(new Vector2(i * 0.125f, 0f), i, s);   // 0 .. 3 m along x

            Assert.That(_published.Count, NUnit.Framework.Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                GroundPrintMade m = _published[i];
                Assert.That(m.Position.x, NUnit.Framework.Is.EqualTo(0.75f * (i + 1)).Within(1e-5f));
                Assert.That(Mathf.Abs(m.Position.y), NUnit.Framework.Is.EqualTo(0.09f).Within(1e-5f));
                Assert.That(m.LengthMetres, NUnit.Framework.Is.EqualTo(s.FootLengthMetres));
                if (i > 0) Assert.That(m.Kind, NUnit.Framework.Is.Not.EqualTo(_published[i - 1].Kind), "left and right alternate");
            }
        }

        [Test]
        public void TyreMarksAreLaidInSegmentsOfTheDataLength()
        {
            GroundPrintSettings s = GroundPrintSettings.Default;
            s.TireSegmentMetres = 0.5f;
            var trail = new TirePrintTrail();
            for (int i = 0; i <= 32; i++) trail.Track(new Vector2(i * 0.0625f, 0f), 1.2f, 0.25f, i, s);   // 0 .. 2 m

            Assert.That(_published.Count, NUnit.Framework.Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(_published[i].Kind, NUnit.Framework.Is.EqualTo(GroundPrintKind.TirePair));
                Assert.That(_published[i].Position.x, NUnit.Framework.Is.EqualTo(0.25f + 0.5f * i).Within(1e-5f));
                Assert.That(_published[i].LengthMetres, NUnit.Framework.Is.EqualTo(0.5f));
                Assert.That(_published[i].GaugeMetres, NUnit.Framework.Is.EqualTo(1.2f));
                Assert.That(_published[i].WidthMetres, NUnit.Framework.Is.EqualTo(0.25f));
            }
        }

        [Test]
        public void AFastUpdateLaysEveryPrintItEarnedUpToTheCap()
        {
            GroundPrintSettings s = GroundPrintSettings.Default;
            s.StrideMetres = 0.5f;
            s.TeleportMetres = 10f;
            s.MaxPrintsPerUpdate = 8;
            var trail = new FootPrintTrail();
            trail.Track(Vector2.zero, 0d, s);
            Assert.That(trail.Track(new Vector2(2f, 0f), 1d, s), NUnit.Framework.Is.EqualTo(4));
        }

        [Test]
        public void ATeleportRestartsTheTrailWithoutAPrint()
        {
            GroundPrintSettings s = GroundPrintSettings.Default;
            var trail = new FootPrintTrail();
            trail.Track(Vector2.zero, 0d, s);
            Assert.That(trail.Track(new Vector2(s.TeleportMetres + 5f, 0f), 1d, s), NUnit.Framework.Is.EqualTo(0));
            Assert.That(_published.Count, NUnit.Framework.Is.EqualTo(0));
        }

        [Test]
        public void TheSameWalkAndClockGiveTheSamePrints()
        {
            GroundPrintSettings s = GroundPrintSettings.Default;
            List<GroundPrintMade> Walk()
            {
                _published.Clear();
                var trail = new FootPrintTrail();
                for (int i = 0; i < 200; i++)
                    trail.Track(new Vector2(Mathf.Sin(i * 0.05f) * 4f, i * 0.07f), 60d + i, s);
                return new List<GroundPrintMade>(_published);
            }
            List<GroundPrintMade> a = Walk(), b = Walk();
            Assert.That(a.Count, NUnit.Framework.Is.GreaterThan(0));
            Assert.That(b.Count, NUnit.Framework.Is.EqualTo(a.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b[i].Position, NUnit.Framework.Is.EqualTo(a[i].Position));
                Assert.That(b[i].GameTime, NUnit.Framework.Is.EqualTo(a[i].GameTime));
                Assert.That(b[i].Kind, NUnit.Framework.Is.EqualTo(a[i].Kind));
            }
        }

        // ---- the shipped config ---------------------------------------------------------------------

        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        [Test]
        public void TheShippedAssetCarriesEveryGroundPrintKey()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "..", ConfigAssetPath);
            Assert.That(System.IO.File.Exists(path), NUnit.Framework.Is.True, path);
            var block = System.Text.RegularExpressions.Regex.Match(System.IO.File.ReadAllText(path),
                @"^  GroundPrints:\r?\n((?:    .*\r?\n)+)", System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.That(block.Success, NUnit.Framework.Is.True, "GameConfig.asset has no GroundPrints block: every print number would read 0");
            foreach (var f in typeof(GroundPrintSettings).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                StringAssert.Contains(f.Name + ":", block.Groups[1].Value, f.Name + " is missing from the GroundPrints block");
        }

        [Test]
        public void TheShippedAssetMatchesTheCodeDefaults()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.That(config, NUnit.Framework.Is.Not.Null);
            GroundPrintSettings shipped = config.GroundPrints, code = GroundPrintSettings.Default;
            Assert.That(shipped.Enabled, NUnit.Framework.Is.True);
            Assert.That(shipped.LifetimeGameHours, NUnit.Framework.Is.EqualTo(code.LifetimeGameHours));
            Assert.That(shipped.Capacity, NUnit.Framework.Is.EqualTo(code.Capacity));
            Assert.That(shipped.StrideMetres, NUnit.Framework.Is.EqualTo(code.StrideMetres));
            Assert.That(shipped.FootLengthMetres, NUnit.Framework.Is.EqualTo(code.FootLengthMetres));
            Assert.That(shipped.TireSegmentMetres, NUnit.Framework.Is.EqualTo(code.TireSegmentMetres));
            Assert.That(shipped.Surfaces.Length, NUnit.Framework.Is.EqualTo(code.Surfaces.Length));
            Assert.That(shipped.Zones.Length, NUnit.Framework.Is.EqualTo(code.Zones.Length));
            for (int i = 0; i < code.Zones.Length; i++)
            {
                Assert.That(shipped.Zones[i].Zone, NUnit.Framework.Is.EqualTo(code.Zones[i].Zone));
                Assert.That(shipped.Zones[i].Surface, NUnit.Framework.Is.EqualTo(code.Zones[i].Surface));
            }
        }

        // ---- the placeholder art --------------------------------------------------------------------

        [Test]
        public void ThePlaceholderMaskHasABootWithAnArchAndATread()
        {
            int size = GroundPrintSettings.Default.ShapeTexturePixels;
            bool[] mask = GroundPrintShapes.Mask(size);
            size = GroundPrintShapes.EvenSize(size);
            int half = size / 2;
            int boot = 0, tread = 0;
            bool emptyBootRow = false;
            for (int y = 0; y < size; y++)
            {
                int rowInk = 0;
                for (int x = 0; x < half; x++) { if (mask[y * size + x]) { boot++; rowInk++; } if (mask[y * size + half + x]) tread++; }
                if (rowInk == 0) emptyBootRow = true;
            }
            Assert.That(boot, NUnit.Framework.Is.GreaterThan(0));
            Assert.That(tread, NUnit.Framework.Is.GreaterThan(0));
            Assert.That(emptyBootRow, NUnit.Framework.Is.True, "the arch gap between heel and sole");
        }
    }
}
