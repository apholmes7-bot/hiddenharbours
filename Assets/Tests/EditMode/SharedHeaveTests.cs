using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>ADR 0023 phase 3, step 2 — the SHARED HEAVE:</b> while the displaced sea is active,
    /// boats visually ride the SAME exaggerated, shore-faded displaced height the surface lifts
    /// with (<see cref="ShoreFadeMath.DisplacedHeight"/> on the one wave sample the rock already
    /// reads, with the surface's own published exaggeration + band — the Core seam
    /// <see cref="DisplacedSea"/>), and a mesh hull sinks to its data-driven resting draft
    /// (<see cref="HullMeshDef.RestingDraftMeters"/> — the rigs' origin is the keel bottom, so
    /// zero draft means keel-on-the-surface).
    ///
    /// <para><b>What is pinned, and how, without re-deriving the field:</b> the wave height under
    /// the hull is the animator's own business, so the assertions use LAWS that hold whatever the
    /// height is — the OFF byte-identity (no seam ⇒ heave untouched), the resting draft at glass
    /// calm (ride 0 ⇒ heave = −draft, exactly), the LINEARITY of the shared rule in the published
    /// exaggeration (double the exaggeration at the same instant ⇒ double the ride — which is
    /// precisely how an owner's live config edit reaches the boat: the surface re-publishes and
    /// the boat's next read moves), and the SHORE FADE (a shallow-depth ride is the open-water
    /// ride × the exact <see cref="ShoreFadeMath.Fade01"/> factor — a boat nosing into the
    /// shallows settles as the water does).</para>
    ///
    /// <para>Headless, GPU-free, deterministic (rule 5): scripted clock, scripted sea, recording
    /// renderer — the MeshRockSmoothnessTests harness pattern. The real components run their real
    /// tick bodies (<see cref="BoatWaveMotion.Tick"/> / <see cref="MeshHullDriver.Drive"/>).</para>
    /// </summary>
    public class SharedHeaveTests
    {
        const float Dt = 1f / 60f;
        const int PxPerMetre = 32;
        const float Draft = 0.5f;
        const float Band = 0.6f;
        const float Exag = 1.5f;

        readonly object _seaOwner = new object();

        [TearDown]
        public void TearDown()
        {
            DisplacedSea.Clear(_seaOwner);      // never leak an active sea into another fixture
            GameServices.Reset();
        }

        // ------------------------------------------------------------------ doubles

        sealed class ScriptedClock : IGameClock
        {
            public double TotalSeconds { get; private set; }
            public void Advance(double dt) => TotalSeconds += dt;
            public GameTime Now => new GameTime(TotalSeconds);
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public int DayIndex => 0;
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => 0f;
            public float HourOfDay => 12f;
            public void SeekTo(double totalSeconds) => TotalSeconds = totalSeconds;
        }

        sealed class ScriptedSea : IEnvironmentService
        {
            public Vector2 Wind = new Vector2(6f, 3f);
            public float SeaState01 = 0.75f;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Wind, Vector2.zero, tideHeight: 0f, HiddenHarbours.Core.SeaState.Moderate,
                visibility: 1f, seaState01: SeaState01);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        sealed class RecordingRenderer : IHullMeshRenderer
        {
            public float HeadingDirUnits { get; set; }
            public float RollDegrees { get; set; }
            public float PitchDegrees { get; set; }
            public float HeavePixels { get; set; }
            public bool IsConfigured => true;
            public void SetSorting(int sortingLayerId, int sortingOrder) { }
        }

        /// <summary>Flat authored seabed at a constant elevation (metres above datum) — with the
        /// scripted sea's water level 0, depth = −elevation everywhere.</summary>
        sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        // ------------------------------------------------------------------ the mesh chain

        sealed class MeshRig : System.IDisposable
        {
            public readonly GameObject Root;
            public readonly RecordingRenderer Renderer = new RecordingRenderer();
            public readonly MeshHullDriver Driver;
            public readonly BoatWaveMotion Wave;
            public readonly ScriptedClock Clock = new ScriptedClock();
            readonly HullMeshDef _def;

            /// <summary>Rock amplitudes ZERO by default so <see cref="RecordingRenderer.HeavePixels"/>
            /// is EXACTLY the displaced term — the law under test, isolated.</summary>
            public MeshRig(float draftMeters, float seaState01, float rockHeavePixels = 0f)
            {
                GameServices.Clock = Clock;
                GameServices.Environment = new ScriptedSea { SeaState01 = seaState01 };

                Root = new GameObject("Boat");
                var visual = new GameObject("Visual");
                visual.transform.SetParent(Root.transform, false);

                _def = ScriptableObject.CreateInstance<HullMeshDef>();
                _def.ElevationDeg = 40f;
                _def.AzimuthCounterClockwise = true;
                _def.RockRollDegrees = 0f;
                _def.RockPitchDegrees = 0f;
                _def.RockHeavePixels = rockHeavePixels;
                _def.PxPerMetre = PxPerMetre;
                _def.RestingDraftMeters = draftMeters;

                Driver = Root.AddComponent<MeshHullDriver>();
                Driver.Configure(visual.transform, Renderer, _def, zeroHeadingDegrees: 0f);
                Wave = Root.AddComponent<BoatWaveMotion>();
                Wave.Configure(visual.transform, new MeshHullPresenter(Driver));
            }

            /// <summary>One production frame in its production order (wave −120 → driver −110).</summary>
            public float Tick(float dt = Dt)
            {
                Clock.Advance(dt);
                Wave.Tick();
                Driver.Drive();
                return Renderer.HeavePixels;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(_def);
            }
        }

        // ------------------------------------------------------------------ the Core seam

        [Test]
        public void Seam_PublishesReadsAndClears_WithOwnerGuard()
        {
            Assert.IsFalse(DisplacedSea.IsActive, "no publisher yet — the seam must start silent");
            Assert.IsFalse(DisplacedSea.TryGet(out _));

            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(1.5f, 0.6f));
            Assert.IsTrue(DisplacedSea.TryGet(out DisplacedSeaState s));
            Assert.AreEqual(1.5f, s.Exaggeration, 1e-6f);
            Assert.AreEqual(0.6f, s.ShoreFadeBandMeters, 1e-6f);

            // Re-publish updates in place — the live-tuning path (the surface re-reads the
            // config and re-publishes every throttled tick; the boat's next read must move).
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(3f, 1.2f));
            DisplacedSea.TryGet(out s);
            Assert.AreEqual(3f, s.Exaggeration, 1e-6f, "a re-publish is how a config edit lands");

            // A STALE owner going away must not kill a newer sea's state.
            var older = new object();
            DisplacedSea.Clear(older);
            Assert.IsTrue(DisplacedSea.IsActive, "clear by a non-owner is a no-op");

            DisplacedSea.Clear(_seaOwner);
            Assert.IsFalse(DisplacedSea.IsActive, "the owner's clear is the OFF contract");
            Assert.IsFalse(DisplacedSea.TryGet(out DisplacedSeaState cleared));
            Assert.AreEqual(0f, cleared.Exaggeration, "cleared state reads default, never stale");
        }

        // ------------------------------------------------------------------ mesh: off / draft / ride

        [Test]
        public void MeshHull_DisplacedOff_HeaveIsUntouched()
        {
            using var rig = new MeshRig(Draft, seaState01: 0.75f);
            for (int f = 0; f < 60; f++)
                Assert.AreEqual(0f, rig.Tick(), 1e-6f,
                    "with the displaced sea OFF the heave channel must be byte-identical to " +
                    "before phase 3 step 2 — no ride, and NO resting draft (the A/B contract " +
                    "extends to boats).");
        }

        [Test]
        public void MeshHull_AtGlassCalm_SitsExactlyAtItsRestingDraft()
        {
            using var rig = new MeshRig(Draft, seaState01: 0f);   // glass — the field is exactly 0
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(Exag, Band));

            for (int f = 0; f < 30; f++)
                Assert.AreEqual(-Draft * PxPerMetre, rig.Tick(), 1e-3f,
                    "glass calm + displaced sea ON: ride is 0, so the whole heave is the resting " +
                    "draft — the keel-origin rig sunk to its design waterline, exactly " +
                    "−draft × PxPerMetre. (A hull with no wave motion still sits at its " +
                    "waterline: the draft gate is the driver's, not the ride's.)");
        }

        [Test]
        public void MeshHull_RidesTheDisplacedHeight_LinearInThePublishedExaggeration()
        {
            // The linearity law pins the shared rule without re-deriving the field: at the SAME
            // deterministic instant, ride(2E) = 2 × ride(E) for every frame — which is also
            // exactly how an owner's live exaggeration edit reaches the boat (a re-publish).
            const int frames = 120;
            float[] rideE = SailMeshRides(Exag, frames);
            float[] rideE2 = SailMeshRides(Exag * 2f, frames);

            int meaningful = 0;
            for (int f = 0; f < frames; f++)
            {
                Assert.AreEqual(rideE[f] * 2f, rideE2[f], 2e-3f,
                    $"frame {f}: ride must be LINEAR in the published exaggeration — the one " +
                    "shared constant, never per-consumer rescaled (the overlay-pose lesson).");
                if (Mathf.Abs(rideE[f]) > 0.05f) meaningful++;
            }
            Assert.Greater(meaningful, frames / 4,
                "the reference sail barely moved — the ride never exceeded 5 cm, so the " +
                "linearity law above was vacuous. Did the boat stop riding the sea?");
        }

        float[] SailMeshRides(float exaggeration, int frames)
        {
            using var rig = new MeshRig(Draft, seaState01: 0.75f);
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(exaggeration, Band));
            for (int w = 0; w < 5; w++) rig.Tick();               // animator warm-up (snap frame)
            var rides = new float[frames];
            for (int f = 0; f < frames; f++)
                rides[f] = rig.Tick() / PxPerMetre + Draft;       // heave = (ride − draft)·px
            DisplacedSea.Clear(_seaOwner);
            GameServices.Reset();
            return rides;
        }

        [Test]
        public void MeshHull_InTheShallows_SettlesByTheSurfacesOwnShoreFade()
        {
            // Depth band/4 ⇒ the ride must be the open-water ride × Fade01(band/4, band) — the
            // EXACT factor the displaced surface's vertex stage fades with, so a boat nosing
            // into the shallows settles as the water under it does (no second seam).
            const int frames = 120;
            const float depth = Band / 4f;
            float fade = ShoreFadeMath.Fade01(depth, Band);
            Assert.Greater(fade, 0.01f);
            Assert.Less(fade, 0.99f, "pick a depth strictly inside the band or the test is vacuous");

            float[] open = SailMeshRides(Exag, frames);           // null terrain ⇒ depth +∞ ⇒ fade 1
            float[] shallow = SailMeshRidesOverTerrain(Exag, frames, elevation: -depth);

            int meaningful = 0;
            for (int f = 0; f < frames; f++)
            {
                Assert.AreEqual(open[f] * fade, shallow[f], 2e-3f,
                    $"frame {f}: the shallow-water ride must be the open-water ride × the Core " +
                    "shore fade — the same Fade01 the surface's vertex stage runs.");
                if (Mathf.Abs(open[f]) > 0.05f) meaningful++;
            }
            Assert.Greater(meaningful, frames / 4, "the reference sail barely moved — vacuous");
        }

        float[] SailMeshRidesOverTerrain(float exaggeration, int frames, float elevation)
        {
            using var rig = new MeshRig(Draft, seaState01: 0.75f);
            GameServices.TidalTerrain = new FlatTerrain { Elevation = elevation };
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(exaggeration, Band));
            for (int w = 0; w < 5; w++) rig.Tick();
            var rides = new float[frames];
            for (int f = 0; f < frames; f++)
                rides[f] = rig.Tick() / PxPerMetre + Draft;
            DisplacedSea.Clear(_seaOwner);
            GameServices.Reset();
            return rides;
        }

        [Test]
        public void MeshHull_RideComposesWithTheRigsOwnRockHeave()
        {
            // The rig's canned rock heave (~1 px) and the metre-scale displaced term share ONE
            // channel; the displaced term must ADD, not replace. Frozen clock ⇒ identical rock
            // phase, so the E=0 run isolates rock+draft and the difference isolates the ride.
            using var rig = new MeshRig(Draft, seaState01: 0.75f, rockHeavePixels: 1.2f);
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(Exag, Band));
            for (int w = 0; w < 30; w++) rig.Tick();              // let the swell build

            float withRide = rig.Tick(0f);                        // dt 0: the sea is frozen
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(0f, Band));
            float rockOnly = rig.Tick(0f);                        // exaggeration 0 ⇒ ride exactly 0

            Assert.LessOrEqual(Mathf.Abs(rockOnly + Draft * PxPerMetre), 1.2f + 1e-3f,
                "with exaggeration 0 the heave must be draft + the rig's own ±1.2 px rock alone");

            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(Exag * 2f, Band));
            float withDoubleRide = rig.Tick(0f);
            Assert.AreEqual((withRide - rockOnly) * 2f, withDoubleRide - rockOnly, 5e-2f,
                "the displaced term must compose ADDITIVELY with the rock heave (the frozen-sea " +
                "difference doubles with the exaggeration; the rock term cancels).");
        }

        // ------------------------------------------------------------------ sprite fleet parity

        [Test]
        public void SpriteHull_LegacyPath_RidesTheSameDisplacedHeight()
        {
            // The legacy transform path (a hull with no rock grid): with the displaced sea ON the
            // bob term IS the shared displaced height — uncapped (metre-scale, far beyond the
            // legacy 0.2 m bob cap) and linear in the published exaggeration. Exaggeration 0
            // (active, ride 0) isolates the pitch term so the ride difference is exact.
            const int frames = 120;
            float[] y0 = SailLegacyOffsets(0f, frames);           // active, ride exactly 0
            float[] yE = SailLegacyOffsets(Exag, frames);
            float[] yE2 = SailLegacyOffsets(Exag * 2f, frames);

            float maxRide = 0f;
            for (int f = 0; f < frames; f++)
            {
                float rideE = yE[f] - y0[f];
                float rideE2 = yE2[f] - y0[f];
                Assert.AreEqual(rideE * 2f, rideE2, 2e-3f,
                    $"frame {f}: the sprite fleet's ride must be linear in the SAME published " +
                    "exaggeration as the mesh's — one sea, never two.");
                maxRide = Mathf.Max(maxRide, Mathf.Abs(rideE));
            }
            Assert.Greater(maxRide, 0.25f,
                "the legacy path's ride never exceeded the old 0.2 m bob cap — the metre-scale " +
                "displaced heave is NOT reaching the sprite fleet (the cap must not apply to it).");
        }

        float[] SailLegacyOffsets(float exaggeration, int frames)
        {
            GameServices.Clock = new ScriptedClock();
            GameServices.Environment = new ScriptedSea { SeaState01 = 0.75f };
            var clock = (ScriptedClock)GameServices.Clock;

            var root = new GameObject("Boat");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var wave = root.AddComponent<BoatWaveMotion>();
            wave.Configure(visual.transform, (IBoatHullPresenter)null);   // the legacy transform path

            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(exaggeration, Band));
            try
            {
                for (int w = 0; w < 5; w++) { clock.Advance(Dt); wave.Tick(); }
                var offsets = new float[frames];
                for (int f = 0; f < frames; f++)
                {
                    clock.Advance(Dt);
                    wave.Tick();
                    offsets[f] = visual.transform.position.y;
                }
                return offsets;
            }
            finally
            {
                DisplacedSea.Clear(_seaOwner);
                Object.DestroyImmediate(root);
                GameServices.Reset();
            }
        }
    }
}
