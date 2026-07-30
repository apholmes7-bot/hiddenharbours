using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE STANDABLE-STRUCTURE SEAM.</b> Before this, every on-foot read composed walkability straight
    /// out of (authored ground vs deterministic water level), so a BUILT thing standing over water was
    /// invisible to the simulation. These assert the three properties the seam has to have:
    /// <list type="bullet">
    /// <item>on a registered deck, the DECK is the standing height — walkable at high water;</item>
    /// <item>one metre off its edge, the answer is back to the seabed, unchanged;</item>
    /// <item>with nothing registered, every read is <b>bit-identical</b> to the pre-seam behaviour — because
    /// the seam substitutes ONE number into the existing <see cref="TidalExposure"/> maths rather than
    /// adding a second rule that could disagree with the first.</item>
    /// </list>
    /// Pure logic over fake terrain / environment doubles and the REAL <see cref="StandablePlatform"/> —
    /// no scene, no tide service.
    /// </summary>
    public class StandableSurfacesTests
    {
        /// <summary>A sloped beach: ground rises with X, so for a fixed water level there is one shoreline —
        /// west of it is wet, east is dry. (Same double as <c>TidalWalkabilityTests</c>, so the two files
        /// answer the same questions over the same ground.)</summary>
        private sealed class SlopeTerrain : ITidalTerrain
        {
            public float Slope = 1f;
            public float ElevationAt(Vector2 worldPos) => worldPos.x * Slope;
        }

        /// <summary>A flat seabed at one elevation — the dredged-slip shape the St Peters pier stands over.</summary>
        private sealed class FlatBed : ITidalTerrain
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

        /// <summary>
        /// A surface whose footprint is a CIRCLE that follows a moving centre — not a rectangle, and not
        /// static. It exists to prove the claim <see cref="IStandableSurface"/> makes about itself: that the
        /// M2 walkable boat deck is an implementation of this contract, not a change to it. If a moving or
        /// non-rectangular deck ever needed a new interface member, this test would stop compiling.
        /// </summary>
        private sealed class MovingRoundDeck : IStandableSurface
        {
            public Vector2 Centre;
            public float Radius = 1f;
            public float Deck;
            public string Id => "deck.moving_round";
            public bool TryGetDeckElevation(Vector2 worldPos, out float deckElevation)
            {
                deckElevation = Deck;
                return Vector2.Distance(worldPos, Centre) <= Radius;
            }
        }

        private readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp() => StandableSurfaces.Clear();

        [TearDown]
        public void TearDown()
        {
            StandableSurfaces.Clear();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        /// <summary>
        /// A real <see cref="StandablePlatform"/> on a GameObject, registered.
        ///
        /// <para>⚠ The registration is EXPLICIT here because <c>OnEnable</c> does not run in edit mode: the
        /// component carries no <c>[ExecuteAlways]</c>, on purpose — registering a deck is a runtime act,
        /// exactly as <see cref="TidalTerrain"/> only claims <c>GameServices.TidalTerrain</c> at runtime. So
        /// the SELF-registration is asserted where it actually happens, in
        /// <c>PierDisembarkPlayTests</c>; these tests are about the rule, and use the real component so the
        /// footprint/deck maths under test is the shipped one rather than a stub that could drift from it.</para>
        /// </summary>
        private StandablePlatform Platform(string id, Rect footprint, float deck)
        {
            var go = new GameObject($"Platform_{id}");
            _spawned.Add(go);
            var p = go.AddComponent<StandablePlatform>();
            p.Configure(id, footprint, deck);
            StandableSurfaces.Register(p);
            return p;
        }

        // =================================================================================
        //  THE DECK IS THE STANDING HEIGHT
        // =================================================================================

        [Test]
        public void OnARegisteredDeck_TheDeckIsTheStandingHeight_NotTheGroundUnderIt()
        {
            var terrain = new SlopeTerrain { Slope = 1f };          // ground(x) = x
            var surfaces = new IStandableSurface[] { Platform("wharf.test", new Rect(0f, -3f, 10f, 6f), 8f) };

            // At x = 2 the seabed is 2 m above datum; the deck over it is 8 m.
            Assert.AreEqual(8f, StandableSurfaces.StandingElevation(terrain.ElevationAt(new Vector2(2f, 0f)),
                                                                    surfaces, new Vector2(2f, 0f)), 1e-5f,
                "standing on a deck, your feet are on the DECK — that is the whole seam");

            // One metre east of the deck's edge (x = 10) there is no surface, so the ground answers.
            Assert.AreEqual(11f, StandableSurfaces.StandingElevation(terrain.ElevationAt(new Vector2(11f, 0f)),
                                                                     surfaces, new Vector2(11f, 0f)), 1e-5f,
                "off the deck, the authored ground is the standing height again");
        }

        [Test]
        public void OnTheDeck_WalkableAndDry_AtAWaterLevelThatDrownsTheGroundBeneath()
        {
            var terrain = new SlopeTerrain { Slope = 1f };
            var env = new FlatEnv { Level = 5f };                    // the sea is at 5 m above datum
            var deck = new Vector2(2f, 0f);                          // seabed 2 m → 3 m of water over it
            var surfaces = new IStandableSurface[] { Platform("wharf.test", new Rect(0f, -3f, 10f, 6f), 8f) };

            // The pre-seam answer, for the record: three metres of water, boat-only deep.
            Assert.AreEqual(3f, TidalWalkability.DepthAt(terrain, env, 0.0, deck), 1e-5f);
            Assert.IsFalse(TidalWalkability.IsWalkable(terrain, env, 0.0, deck),
                "without the seam this spot is open water — which is exactly the bug");

            // With the deck registered: dry, walkable, full speed.
            Assert.AreEqual(-3f, TidalWalkability.DepthAt(terrain, env, surfaces, 0.0, deck), 1e-5f,
                "the depth is water level − DECK, so an 8 m deck under a 5 m sea reads 3 m of freeboard");
            Assert.IsTrue(TidalWalkability.IsWalkable(terrain, env, surfaces, 0.0, deck),
                "a deck clear of the water is walkable — regardless of the tide, because the tide never reaches it");
            Assert.AreEqual(DepthBand.Dry,
                TidalWalkability.BandAt(terrain, env, surfaces, 0.0, deck, 0.5f, 2.0f),
                "and it reads DRY, so there is no wade slow-down and no submerged body on dry planks");
        }

        [Test]
        public void OneMetreOffTheDeckEdge_TheSeabedAnswersAgain_AndItIsBoatOnly()
        {
            // The St Peters shape in miniature: a dredged −1 m bed, a spring high water of 3.5 m, and a
            // deck measured off the dry ground the pier launches from (5.35 m).
            var bed = new FlatBed { Elevation = -1f };
            var env = new FlatEnv { Level = 3.5f };
            var surfaces = new IStandableSurface[] { Platform("wharf.test", new Rect(0f, -3f, 10f, 6f), 5.35f) };

            var onDeck = new Vector2(9.5f, 0f);
            var offEdge = new Vector2(11f, 0f);                      // one metre east of xMax = 10

            Assert.IsTrue(TidalWalkability.IsWalkable(bed, env, surfaces, 0.0, onDeck),
                "the planks are floor right up to their lip");
            Assert.IsFalse(TidalWalkability.IsWalkable(bed, env, surfaces, 0.0, offEdge),
                "one metre off the edge you are over the dredged bed again — the deck is not a bubble of dry air");
            Assert.AreEqual(DepthBand.Deep,
                TidalWalkability.BandAt(bed, env, surfaces, 0.0, offEdge, 0.5f, 2.0f),
                "4.5 m of water beside the deck is boat-only — the soft wall still stands at the edge");
        }

        [Test]
        public void ADeckTheSeaActuallyReaches_StillFloods_BecauseItIsOneNumberNotAnOverride()
        {
            // The seam is NOT "registered ⇒ dry". A deck authored below the water reads honestly through
            // the ordinary wade bands — which is the behaviour a float at low water, or an awash washboard
            // (M2), actually wants. Making registration mean "always dry" would make that unexpressible.
            var bed = new FlatBed { Elevation = -1f };
            var env = new FlatEnv { Level = 3.5f };
            var lowDeck = new IStandableSurface[] { Platform("float.test", new Rect(0f, -1f, 4f, 2f), 3.2f) };
            var p = new Vector2(2f, 0f);

            Assert.AreEqual(0.3f, TidalWalkability.DepthAt(bed, env, lowDeck, 0.0, p), 1e-5f,
                "a 3.2 m deck under a 3.5 m sea is 0.3 m awash — the same single depth rule, not a special case");
            Assert.AreEqual(DepthBand.Wade,
                TidalWalkability.BandAt(bed, env, lowDeck, 0.0, p, 0.5f, 2.0f),
                "so you WADE across it, which is what an awash deck should feel like");
        }

        [Test]
        public void HighestDeckWins_WhereSurfacesOverlap()
        {
            var surfaces = new IStandableSurface[]
            {
                Platform("wharf.low",  new Rect(0f, -2f, 10f, 4f), 4f),
                Platform("wharf.high", new Rect(4f, -2f,  3f, 4f), 6f),   // a stage built on top of the deck
            };

            Assert.IsTrue(StandableSurfaces.TryGetDeckElevation(surfaces, new Vector2(5f, 0f), out float deck));
            Assert.AreEqual(6f, deck, 1e-5f, "overlapping structures stack — you stand on the top one");

            Assert.IsTrue(StandableSurfaces.TryGetDeckElevation(surfaces, new Vector2(1f, 0f), out deck));
            Assert.AreEqual(4f, deck, 1e-5f, "…and on the plain deck where nothing is stacked on it");
        }

        // =================================================================================
        //  NOTHING REGISTERED == THE PRE-SEAM BEHAVIOUR, EXACTLY
        // =================================================================================

        [Test]
        public void WithNoSurfaces_TheTideGateIsBitIdenticalToTheTerrainAnswer()
        {
            var terrain = new SlopeTerrain { Slope = 1f };
            var env = new FlatEnv { Level = 5f };

            // Sweep the shoreline finely and compare against the rule the project had BEFORE the seam:
            // depth = waterLevel − terrainElevation, exposed = ground ≥ water. Bit equality, not tolerance:
            // the seam must not perturb the number at all where no structure stands.
            for (float x = 0f; x <= 10f; x += 0.05f)
            {
                var p = new Vector2(x, 0f);
                float legacy = TidalExposure.WaterDepth(env.Level, terrain.ElevationAt(p));

                Assert.AreEqual(legacy, TidalWalkability.DepthAt(terrain, env, 0.0, p),
                    $"depth at x={x} moved with no structures registered");
                Assert.AreEqual(legacy, TidalWalkability.DepthAt(terrain, env, null, 0.0, p),
                    $"depth at x={x} moved with an explicitly null surface list");
                Assert.AreEqual(legacy, TidalWalkability.DepthAt(terrain, env, StandableSurfaces.Active, 0.0, p),
                    $"depth at x={x} moved when read through an EMPTY live registry");
                Assert.AreEqual(TidalExposure.IsExposed(env.Level, terrain.ElevationAt(p)),
                                TidalWalkability.IsWalkable(terrain, env, 0.0, p),
                    $"walkability at x={x} moved with no structures registered");
            }
        }

        [Test]
        public void GateOffContract_IsUnchanged_NoTerrainOrNoEnvironmentStillMeansDryEverywhere()
        {
            var env = new FlatEnv { Level = 99f };
            var surfaces = new IStandableSurface[] { Platform("wharf.test", new Rect(0f, 0f, 1f, 1f), 0f) };

            Assert.AreEqual(float.NegativeInfinity,
                StandableSurfaces.OnFootDepth(null, env, surfaces, 0.0, Vector2.zero),
                "no height map → the region isn't tide-gated → 'as dry as can be' (never 0, never trapped)");
            Assert.AreEqual(float.NegativeInfinity,
                StandableSurfaces.OnFootDepth(new FlatBed(), null, surfaces, 0.0, Vector2.zero),
                "no environment service → gate off");
            Assert.IsTrue(TidalWalkability.IsWalkable(null, env, surfaces, 0.0, Vector2.zero),
                "and the boolean gate stays off too — a structure must not switch a non-tidal region ON");
        }

        // =================================================================================
        //  THE REGISTRY
        // =================================================================================

        [Test]
        public void ARegisteredPlatform_IsFoundByTheLiveLookup_AndGoneOnceUnregistered()
        {
            var p = Platform("wharf.test", new Rect(0f, 0f, 4f, 4f), 5f);
            Assert.AreEqual(1, StandableSurfaces.Count);
            Assert.IsTrue(StandableSurfaces.TryGetDeckElevationNow(new Vector2(2f, 2f), out float deck),
                "the live lookup finds a registered deck");
            Assert.AreEqual(5f, deck, 1e-5f);

            StandableSurfaces.Unregister(p);
            Assert.AreEqual(0, StandableSurfaces.Count,
                "unregistering relinquishes it — a torn-down region must leave no deck behind");
            Assert.IsFalse(StandableSurfaces.TryGetDeckElevationNow(new Vector2(2f, 2f), out _),
                "…and the live lookup goes back to finding nothing");
        }

        [Test]
        public void ClearEmptiesTheRegistry_SoALeakedDeckCannotDryUnrelatedWater()
        {
            Platform("wharf.a", new Rect(0f, 0f, 4f, 4f), 5f);
            Platform("wharf.b", new Rect(9f, 0f, 4f, 4f), 5f);
            Assert.AreEqual(2, StandableSurfaces.Count);

            StandableSurfaces.Clear();
            Assert.AreEqual(0, StandableSurfaces.Count);
            Assert.IsFalse(StandableSurfaces.TryGetDeckElevationNow(new Vector2(2f, 2f), out _));
        }

        [Test]
        public void RegisterIsIdempotent_AndNullsAreIgnored()
        {
            var p = Platform("wharf.test", new Rect(0f, 0f, 4f, 4f), 5f);
            StandableSurfaces.Register(p);
            StandableSurfaces.Register(p);
            Assert.AreEqual(1, StandableSurfaces.Count,
                "a double registration must not double-list the deck — one surface, one answer");

            StandableSurfaces.Register(null);
            Assert.AreEqual(1, StandableSurfaces.Count, "a null registration is a no-op, not an entry");

            StandableSurfaces.Unregister(null);
            StandableSurfaces.Unregister(new MovingRoundDeck());
            Assert.AreEqual(1, StandableSurfaces.Count,
                "unregistering a null / never-registered surface is a no-op — teardown never has to check first");
        }

        [Test]
        public void ANullEntryInTheList_IsSkippedRatherThanThrowing()
        {
            var surfaces = new IStandableSurface[] { null, Platform("wharf.test", new Rect(0f, 0f, 4f, 4f), 5f) };
            Assert.IsTrue(StandableSurfaces.TryGetDeckElevation(surfaces, new Vector2(1f, 1f), out float deck));
            Assert.AreEqual(5f, deck, 1e-5f, "a hole in the list must not hide the deck behind it");
        }

        // =================================================================================
        //  THE FOOTPRINT RULE
        // =================================================================================

        [Test]
        public void TheFootprintIncludesItsFarEdges_UnlikeRectContains()
        {
            // ⚠ The trap this guards: Rect.Contains is HALF-OPEN (it excludes xMax/yMax). A deck authored
            // as cells [288, 329) × [-3, 3) would then report its own north and east lips as water — a
            // one-metre strip of drowning along two sides of every wharf in the game.
            var r = new Rect(288f, -3f, 41f, 6f);

            Assert.IsTrue(StandablePlatform.Contains(r, new Vector2(329f, 0f)), "the east lip is planks");
            Assert.IsTrue(StandablePlatform.Contains(r, new Vector2(288f, 0f)), "the west lip is planks");
            Assert.IsTrue(StandablePlatform.Contains(r, new Vector2(300f, 3f)), "the north lip is planks");
            Assert.IsTrue(StandablePlatform.Contains(r, new Vector2(300f, -3f)), "the south lip is planks");
            Assert.IsFalse(r.Contains(new Vector2(329f, 0f)),
                "…and Rect.Contains genuinely disagrees, which is why this is not written with it");

            Assert.IsFalse(StandablePlatform.Contains(r, new Vector2(329.01f, 0f)), "just past the lip is water");
            Assert.IsFalse(StandablePlatform.Contains(r, new Vector2(300f, 3.01f)), "just past the lip is water");
        }

        [Test]
        public void TheContractAdmitsAMovingNonRectangularDeck_WithoutAnInterfaceChange()
        {
            // The M2 promise, kept honest by compiling: a deck that MOVES and is not a rectangle is a
            // different IStandableSurface, not a different contract.
            var bed = new FlatBed { Elevation = -4f };
            var env = new FlatEnv { Level = 0f };
            var hull = new MovingRoundDeck { Centre = new Vector2(0f, 0f), Radius = 2f, Deck = 0.6f };
            var surfaces = new IStandableSurface[] { hull };

            Assert.IsTrue(TidalWalkability.IsWalkable(bed, env, surfaces, 0.0, new Vector2(1f, 0f)),
                "standing on the hull's deck, over 4 m of water");
            Assert.IsFalse(TidalWalkability.IsWalkable(bed, env, surfaces, 0.0, new Vector2(5f, 0f)),
                "…and the sea beside her is still the sea");

            hull.Centre = new Vector2(10f, 0f);                       // she drifts
            Assert.IsFalse(TidalWalkability.IsWalkable(bed, env, surfaces, 0.0, new Vector2(1f, 0f)),
                "the deck went with her — the footprint is a QUERY, so a moving registrant needs no new member");
            Assert.IsTrue(TidalWalkability.IsWalkable(bed, env, surfaces, 0.0, new Vector2(10f, 0f)),
                "and it is standable where she now floats");
        }
    }
}
