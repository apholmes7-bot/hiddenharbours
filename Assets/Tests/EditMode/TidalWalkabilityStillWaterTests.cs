using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The on-foot fisher in still water above the tide (ADR 0046; the owner's ruling of 2026-09-25 that
    /// streams and ponds above the tide are real water). A pond or a brook's fresh reach is WATER to the
    /// walker — waded where it is shallow, swum where it is deeper than the wade limit — whatever the tide
    /// is doing, because its surface stands above every tide there is. The walker's water is
    /// max(tide, still) at the point: the tide still governs wherever it is the higher (a brook's lowest
    /// reach at spring high), a deck over a pond is stood on dry, and wherever there is no still water the
    /// answer is the tide-only answer, bit for bit.
    ///
    /// <para><b>Independent bars.</b> The levels are the spec's own (part 1's pond table): the Bog Pond's
    /// surface +5.30 m over a bed of +4.65 m, the Fen Pool's +5.25 over +4.70, a brook's fresh reach its bed
    /// plus 0.12 m, the spring range ±2.2 m and the bar crest +0.88 m. The wade and swim limits are the
    /// game's defaults (<c>GameConfig.WadeDepth</c> 0.5, <c>SwimLimit</c> 2.0), written here as literals.
    /// Every expected depth is hand arithmetic on those numbers; none is read from the code under test.</para>
    ///
    /// <para><b>On the base</b> the walker's water is the tide alone, and the highest tide is +2.2 m (spring
    /// high). Over the Bog Pond's bed of +4.65 the base reads at most 2.2 − 4.65 = −2.45 m — Dry, walkable —
    /// where each test here asserts 0.65 m and Swim; over a brook reach at +3.10 it reads at most −0.90 m,
    /// Dry, where the test asserts 0.12 m and Wade. (The still overloads and
    /// <see cref="GameServices.StillWater"/> do not exist there to compile against either.)</para>
    /// </summary>
    public sealed class TidalWalkabilityStillWaterTests
    {
        private const float WadeDepth = 0.5f;
        private const float SwimLimit = 2.0f;
        private const float Tol = 1e-5f;

        // Spring low, a neap-ish low, datum, the bar crest, a neap-ish high, spring high.
        private static readonly float[] Tides = { -2.2f, -1.1f, 0f, 0.88f, 1.1f, 2.2f };

        private const float MoorGround = 5.40f;   // the bank round the ponds, above every tide and every pond

        private static readonly Vector2 BogPond = new Vector2(40f, 12f);
        private const float BogSurface = 5.30f, BogBed = 4.65f;
        private static readonly Vector2 FenPool = new Vector2(70f, 12f);
        private const float FenSurface = 5.25f, FenBed = 4.70f;
        private static readonly Vector2 UpperReach = new Vector2(100f, 30f);
        private const float UpperBed = 3.10f, UpperFresh = 3.22f;   // bed + 0.12
        private static readonly Vector2 LowerReach = new Vector2(100f, 20f);
        private const float LowerBed = 1.00f, LowerFresh = 1.12f;   // bed + 0.12: the reach the tide reaches

        private static readonly Vector2 Bank = new Vector2(48f, 12f);        // 8 m off the Bog Pond's centre
        private static readonly Vector2 OnTheStage = new Vector2(40f, 10.5f);
        private static readonly Vector2 BesideTheStage = new Vector2(40f, 14f);
        private const float StageDeck = 5.80f;

        private PatchTerrain _bed;
        private PatchStillWater _still;

        [SetUp]
        public void SetUp()
        {
            StandableSurfaces.Clear();
            GameServices.Reset();

            _bed = new PatchTerrain(MoorGround,
                new Patch(BogPond, 6f, BogBed), new Patch(FenPool, 4f, FenBed),
                new Patch(UpperReach, 2f, UpperBed), new Patch(LowerReach, 2f, LowerBed));
            _still = new PatchStillWater(
                new Patch(BogPond, 6f, BogSurface), new Patch(FenPool, 4f, FenSurface),
                new Patch(UpperReach, 2f, UpperFresh), new Patch(LowerReach, 2f, LowerFresh));
        }

        [TearDown]
        public void TearDown()
        {
            StandableSurfaces.Clear();
            GameServices.Reset();
        }

        [Test]
        public void BothPonds_AreSwum_AtEveryTide_AndTheirBankIsDry()
        {
            foreach (float tide in Tides)
            {
                var env = new FlatEnv { Level = tide };

                // Bog Pond: 5.30 − 4.65 = 0.65 m, over the wade limit (0.5) and within the swim limit (2.0).
                Assert.AreEqual(0.65f, TidalWalkability.DepthAt(_bed, env, _still, null, 0.0, BogPond), Tol,
                    $"tide {tide:+0.00;-0.00}: the Bog Pond's middle is its own 0.65 m deep, not the tide's");
                Assert.AreEqual(DepthBand.Swim,
                    TidalWalkability.BandAt(_bed, env, _still, null, 0.0, BogPond, WadeDepth, SwimLimit),
                    $"tide {tide:+0.00;-0.00}: the Bog Pond is swum");
                Assert.IsFalse(TidalWalkability.IsWalkable(_bed, env, _still, null, 0.0, BogPond),
                    $"tide {tide:+0.00;-0.00}: the Bog Pond's bed is under water, not bared ground");

                // Fen Pool: 5.25 − 4.70 = 0.55 m — only 5 cm over the wade limit, and swum.
                Assert.AreEqual(0.55f, TidalWalkability.DepthAt(_bed, env, _still, null, 0.0, FenPool), Tol,
                    $"tide {tide:+0.00;-0.00}: the Fen Pool is 0.55 m deep");
                Assert.AreEqual(DepthBand.Swim,
                    TidalWalkability.BandAt(_bed, env, _still, null, 0.0, FenPool, WadeDepth, SwimLimit),
                    $"tide {tide:+0.00;-0.00}: the Fen Pool is swum");

                // The bank: no still water there, so the water is the tide, 3.2 m or more below the moor.
                Assert.AreEqual(tide - MoorGround, TidalWalkability.DepthAt(_bed, env, _still, null, 0.0, Bank), Tol,
                    $"tide {tide:+0.00;-0.00}: off the pond the water is the tide");
                Assert.AreEqual(DepthBand.Dry,
                    TidalWalkability.BandAt(_bed, env, _still, null, 0.0, Bank, WadeDepth, SwimLimit),
                    $"tide {tide:+0.00;-0.00}: the bank is dry");
                Assert.IsTrue(TidalWalkability.IsWalkable(_bed, env, _still, null, 0.0, Bank),
                    $"tide {tide:+0.00;-0.00}: the bank is walked");
            }
        }

        [Test]
        public void ABrooksFreshReach_IsWaded_UntilTheTideRisesAboveIt()
        {
            foreach (float tide in Tides)
            {
                var env = new FlatEnv { Level = tide };

                // The upper reach, +3.10 bed with 0.12 m of fresh water, is above every tide: always waded.
                Assert.AreEqual(0.12f, TidalWalkability.DepthAt(_bed, env, _still, null, 0.0, UpperReach), Tol,
                    $"tide {tide:+0.00;-0.00}: the upper reach runs 0.12 m deep");
                Assert.AreEqual(DepthBand.Wade,
                    TidalWalkability.BandAt(_bed, env, _still, null, 0.0, UpperReach, WadeDepth, SwimLimit),
                    $"tide {tide:+0.00;-0.00}: the upper reach is waded");

                // The lowest reach, +1.00 bed with its fresh surface at +1.12, is where the tide meets the
                // brook: the deeper of the two governs. Hand arithmetic, per tide:
                //   −2.2, −1.1, 0, +0.88, +1.1 → the fresh water (1.12) is higher → 1.12 − 1.00 = 0.12 m, Wade;
                //   +2.2 (spring high)         → the tide is higher        → 2.20 − 1.00 = 1.20 m, Swim.
                float expected = tide > LowerFresh ? tide - LowerBed : 0.12f;
                DepthBand band = tide > LowerFresh ? DepthBand.Swim : DepthBand.Wade;
                Assert.AreEqual(expected, TidalWalkability.DepthAt(_bed, env, _still, null, 0.0, LowerReach), Tol,
                    $"tide {tide:+0.00;-0.00}: the lowest reach is {expected:0.00} m deep");
                Assert.AreEqual(band,
                    TidalWalkability.BandAt(_bed, env, _still, null, 0.0, LowerReach, WadeDepth, SwimLimit),
                    $"tide {tide:+0.00;-0.00}: the lowest reach is {band}");
            }
        }

        [Test]
        public void AStageOverThePond_IsStoodOnDry_AndThePondBesideItIsSwum()
        {
            var surfaces = new List<IStandableSurface> { new Stage(new Rect(38f, 10f, 4f, 1f), StageDeck) };
            foreach (float tide in Tides)
            {
                var env = new FlatEnv { Level = tide };

                // On the stage: 5.30 − 5.80 = −0.50 m. The deck is what the walker stands on, as over the sea.
                Assert.AreEqual(-0.5f, TidalWalkability.DepthAt(_bed, env, _still, surfaces, 0.0, OnTheStage), Tol,
                    $"tide {tide:+0.00;-0.00}: the stage's deck stands half a metre over the pond");
                Assert.IsTrue(TidalWalkability.IsWalkable(_bed, env, _still, surfaces, 0.0, OnTheStage),
                    $"tide {tide:+0.00;-0.00}: the stage is walked");

                // Step off it into the pond: 0.65 m, swum.
                Assert.AreEqual(DepthBand.Swim,
                    TidalWalkability.BandAt(_bed, env, _still, surfaces, 0.0, BesideTheStage, WadeDepth, SwimLimit),
                    $"tide {tide:+0.00;-0.00}: off the stage is the pond");
            }
        }

        [Test]
        public void OffTheStillWater_EveryAnswerIsTheTideOnlyAnswer_BitForBit()
        {
            // Points with no still water over them: the bank, the moor between the features, and a flat
            // outside every patch. Tide-only overloads are the pre-seam answer the region played to.
            var off = new[] { Bank, new Vector2(55f, 12f), new Vector2(85f, 25f), new Vector2(-300f, -40f) };
            IStillWater[] unbound = { null, EmptyStillWater.Instance };

            foreach (float tide in Tides)
            {
                var env = new FlatEnv { Level = tide };
                foreach (Vector2 p in off)
                {
                    float tideOnly = TidalWalkability.DepthAt(_bed, env, 0.0, p);
                    Assert.AreEqual(tideOnly, TidalWalkability.DepthAt(_bed, env, _still, null, 0.0, p),
                        $"{p} at tide {tide}: off the ponds the depth is the tide's, exactly");
                    Assert.AreEqual(TidalWalkability.IsWalkable(_bed, env, 0.0, p),
                        TidalWalkability.IsWalkable(_bed, env, _still, null, 0.0, p), $"{p} at tide {tide}");
                    Assert.AreEqual(TidalWalkability.BandAt(_bed, env, 0.0, p, WadeDepth, SwimLimit),
                        TidalWalkability.BandAt(_bed, env, _still, null, 0.0, p, WadeDepth, SwimLimit),
                        $"{p} at tide {tide}");
                }

                // A region with no still map (every region today): even over a pond's bed the answer is the
                // tide's — here 4.65 m of dry pond bed, since no map says there is a pond.
                foreach (IStillWater none in unbound)
                {
                    Assert.AreEqual(TidalWalkability.DepthAt(_bed, env, 0.0, BogPond),
                        TidalWalkability.DepthAt(_bed, env, none, null, 0.0, BogPond),
                        $"{(none == null ? "null" : "the empty still water")} at tide {tide}: no map, no pond");
                    Assert.AreEqual(tide - BogBed, TidalWalkability.DepthAt(_bed, env, none, null, 0.0, BogPond), Tol);
                }

                // And the pond, where the map says one is: this is the answer the tide alone never gives.
                Assert.AreEqual(0.65f, TidalWalkability.DepthAt(_bed, env, _still, null, 0.0, BogPond), Tol,
                    $"tide {tide}: over the pond the still water governs");
            }
        }

        [Test]
        public void TheLiveReads_WadeTheRegisteredStillWater_AndOnlyWhileItIsRegistered()
        {
            var env = new FlatEnv { Level = 0.88f };   // the tide at the bar crest; the clock is unset, t = 0
            GameServices.TidalTerrain = _bed;
            GameServices.Environment = env;
            GameServices.StillWater = _still;
            StandableSurfaces.Register(new Stage(new Rect(38f, 10f, 4f, 1f), StageDeck));

            // What the walk controller, the control switcher, the carried catch and the submerge visual read.
            Assert.AreEqual(0.65f, TidalWalkability.DepthNow(BogPond), Tol, "the live depth is the pond's");
            Assert.AreEqual(DepthBand.Swim,
                TidalExposure.BandForDepth(TidalWalkability.DepthNow(BogPond), WadeDepth, SwimLimit),
                "the live band over the pond is Swim");
            Assert.IsFalse(TidalWalkability.IsWalkableNow(BogPond), "the pond's bed is not walked dry");
            Assert.AreEqual(0.65f, StandableSurfaces.OnFootDepthNow(BogPond), Tol, "the Core read agrees");
            Assert.AreEqual(0.12f, TidalWalkability.DepthNow(UpperReach), Tol, "the brook is waded");
            Assert.AreEqual(-0.5f, TidalWalkability.DepthNow(OnTheStage), Tol, "the registered stage is stood on");
            Assert.IsTrue(TidalWalkability.IsWalkableNow(OnTheStage));
            Assert.IsTrue(TidalWalkability.IsWalkableNow(Bank), "the bank is walked");

            foreach (float tide in Tides)
            {
                env.Level = tide;
                Assert.AreEqual(0.65f, TidalWalkability.DepthNow(BogPond), Tol,
                    $"tide {tide:+0.00;-0.00}: the pond does not rise and fall with the sea");
            }

            // The region's terrain goes (or never had a still map): the walker is back on the tide alone.
            env.Level = 0.88f;
            GameServices.StillWater = null;
            Assert.AreEqual(0.88f - BogBed, TidalWalkability.DepthNow(BogPond), Tol, "no still water: the tide");
            Assert.IsTrue(TidalWalkability.IsWalkableNow(BogPond), "and the pond's bed is dry ground");
            GameServices.StillWater = EmptyStillWater.Instance;
            Assert.AreEqual(0.88f - BogBed, TidalWalkability.DepthNow(BogPond), Tol, "the empty still water: the tide");
        }

        // ---- doubles ------------------------------------------------------------------------------------

        /// <summary>A round patch of the world with one level over it.</summary>
        private readonly struct Patch
        {
            public readonly Vector2 Centre;
            public readonly float Radius;
            public readonly float Level;

            public Patch(Vector2 centre, float radius, float level)
            {
                Centre = centre;
                Radius = radius;
                Level = level;
            }

            public bool Covers(Vector2 p) => (p - Centre).sqrMagnitude <= Radius * Radius;
        }

        /// <summary>The moor at one height, with a bed patch for each pond and brook reach.</summary>
        private sealed class PatchTerrain : ITidalTerrain
        {
            private readonly float _ground;
            private readonly Patch[] _beds;

            public PatchTerrain(float ground, params Patch[] beds)
            {
                _ground = ground;
                _beds = beds;
            }

            public float ElevationAt(Vector2 worldPos)
            {
                foreach (Patch bed in _beds) if (bed.Covers(worldPos)) return bed.Level;
                return _ground;
            }
        }

        /// <summary>A registered still water as the walker reads it: a level over each patch, none elsewhere.
        /// The walker never reads the map (the render does), so it is unbound.</summary>
        private sealed class PatchStillWater : IStillWater
        {
            private readonly Patch[] _surfaces;

            public PatchStillWater(params Patch[] surfaces) { _surfaces = surfaces; }

            public float StillLevelAt(Vector2 worldPos)
            {
                foreach (Patch surface in _surfaces) if (surface.Covers(worldPos)) return surface.Level;
                return StillWaterLevels.None;
            }

            public StillWaterMap Map => default;
        }

        /// <summary>A fishing stage: a deck over part of the pond.</summary>
        private sealed class Stage : IStandableSurface
        {
            private readonly Rect _footprint;
            private readonly float _deck;

            public Stage(Rect footprint, float deck)
            {
                _footprint = footprint;
                _deck = deck;
            }

            public string Id => "stage.test_pond";

            public bool TryGetDeckElevation(Vector2 worldPos, out float deckElevation)
            {
                deckElevation = _deck;
                return _footprint.Contains(worldPos);
            }
        }

        /// <summary>An environment whose tide is one level at every time.</summary>
        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }
    }
}
