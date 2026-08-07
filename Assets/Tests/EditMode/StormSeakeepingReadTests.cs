using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>ADR 0018 B2.5 through the REAL components</b> — the production chain
    /// (<see cref="BoatWaveMotion"/> → the presenter seam → <see cref="MeshHullDriver"/> /
    /// <see cref="DirectionalBoatSprite"/>) sailed against a scripted clock and sea, the
    /// <c>SharedHeaveTests</c> / <c>MeshRockSmoothnessTests</c> harness pattern. What is pinned:
    ///
    /// <list type="bullet">
    ///   <item><b>A storm outranks a chop</b> in the attitude the renderer is actually handed —
    ///   the owner's defect was precisely that it did not (the def rock amplitudes were fixed and
    ///   the wave supplied only phase).</item>
    ///   <item><b>The calm band is byte-identical</b> with the storm block on vs off — the
    ///   negative control that protects the owner's tuned cozy feel.</item>
    ///   <item><b>The weighted ride is real and honest</b> — it differs from the surface-bolted
    ///   ride in a storm (weight engaged) but never strays beyond the honesty band of it.</item>
    ///   <item><b>The sprite frame path keeps its phase honesty</b> — the storm surge rides UNDER
    ///   the frames while the frame cycle itself stays the untouched forward-phase walk.</item>
    /// </list>
    ///
    /// Headless, GPU-free, deterministic (rule 5): scripted clock, scripted sea, recording
    /// renderer; the real components run their real tick bodies.
    /// </summary>
    public class StormSeakeepingReadTests
    {
        const float Dt = 1f / 60f;
        const int Frames = 600;                      // 10 s of sail
        const int PxPerMetre = 32;
        const float RigElevationDegrees = 40f;       // every boat rig's bake elevation

        readonly object _seaOwner = new object();
        readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            DisplacedSea.Clear(_seaOwner);
            GameServices.Reset();
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
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
            public float RidePixels { get; set; }
            public bool IsConfigured => true;
            public void SetSorting(int sortingLayerId, int sortingOrder) { }
        }

        GameConfig NewConfig(in StormRockSettings storm)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.StormRock = storm;
            _spawned.Add(config);
            return config;
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

            public MeshRig(float seaState01, float rockRollDegrees, float rockPitchDegrees,
                           float draftMeters = 0f)
            {
                GameServices.Clock = Clock;
                GameServices.Environment = new ScriptedSea { SeaState01 = seaState01 };

                Root = new GameObject("Boat");
                var visual = new GameObject("Visual");
                visual.transform.SetParent(Root.transform, false);

                _def = ScriptableObject.CreateInstance<HullMeshDef>();
                _def.ElevationDeg = RigElevationDegrees;
                _def.AzimuthCounterClockwise = true;
                _def.RockRollDegrees = rockRollDegrees;
                _def.RockPitchDegrees = rockPitchDegrees;
                _def.RockHeavePixels = 0f;
                _def.PxPerMetre = PxPerMetre;
                _def.RestingDraftMeters = draftMeters;

                Driver = Root.AddComponent<MeshHullDriver>();
                Driver.Configure(visual.transform, Renderer, _def, zeroHeadingDegrees: 0f);
                Wave = Root.AddComponent<BoatWaveMotion>();
                Wave.Configure(visual.transform, new MeshHullPresenter(Driver));
            }

            /// <summary>One production frame in its production order (wave −120 → driver −110).</summary>
            public void Tick(float dt = Dt)
            {
                Clock.Advance(dt);
                Wave.Tick();
                Driver.Drive();
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(_def);
            }
        }

        /// <summary>Sail a mesh rig and record what the renderer was handed each frame.</summary>
        static void SailMesh(MeshRig rig, int frames, out float[] roll, out float[] pitch, out float[] heave)
        {
            for (int w = 0; w < 5; w++) rig.Tick();      // animator warm-up (snap frame)
            roll = new float[frames];
            pitch = new float[frames];
            heave = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                rig.Tick();
                roll[f] = rig.Renderer.RollDegrees;
                pitch[f] = rig.Renderer.PitchDegrees;
                heave[f] = rig.Renderer.HeavePixels;
            }
        }

        static float MaxAbs(float[] series)
        {
            float max = 0f;
            for (int i = 0; i < series.Length; i++) max = Mathf.Max(max, Mathf.Abs(series[i]));
            return max;
        }

        // ------------------------------------------------------------------ storm > chop

        [Test]
        public void MeshHull_AStorm_OutranksAChop_InDrawnAttitude()
        {
            // Same wind vector (same wavelengths, same period), dory-like def amplitudes — the ONE
            // axis that differs is the sea state. Before B2.5 the two sails drew identical attitude
            // (phase-only rock at fixed def amplitudes): the owner's "not responsive to the waves".
            float chopRoll, chopPitch, stormRoll, stormPitch;
            using (var chop = new MeshRig(seaState01: 0.3f, rockRollDegrees: 5f, rockPitchDegrees: 3f))
            {
                SailMesh(chop, Frames, out float[] roll, out float[] pitch, out _);
                chopRoll = MaxAbs(roll);
                chopPitch = MaxAbs(pitch);
            }
            GameServices.Reset();
            using (var storm = new MeshRig(seaState01: 1f, rockRollDegrees: 5f, rockPitchDegrees: 3f))
            {
                SailMesh(storm, Frames, out float[] roll, out float[] pitch, out _);
                stormRoll = MaxAbs(roll);
                stormPitch = MaxAbs(pitch);
            }

            Debug.Log($"[storm-read] mesh attitude: chop roll {chopRoll:F2}° pitch {chopPitch:F2}° — " +
                      $"storm roll {stormRoll:F2}° pitch {stormPitch:F2}°");
            Assert.Greater(chopRoll, 1f, "harness: the chop must actually rock (calm gate off)");
            Assert.Greater(stormRoll, chopRoll * 1.5f,
                "a full storm must visibly OUT-ROLL a light chop — fixed-amplitude rock was the defect");
            Assert.Greater(stormPitch, chopPitch * 1.5f,
                "a full storm must visibly OUT-PITCH a light chop — 'steep enough front-to-back " +
                "rocking to represent the storm waves' is the owner's ask, verbatim");
        }

        // ------------------------------------------------------------------ the negative control

        [Test]
        public void MeshHull_InTheCalmBand_IsByteIdentical_StormOnVsOff()
        {
            // Sea 0.35 sits below the default storm start (0.4): the blend is exactly 0, so the
            // storm block ON must be bit-for-bit the block OFF — the owner's tuned calm feel is
            // untouched. This is the negative control every other assertion leans on.
            float[] onRoll, onPitch, onHeave, offRoll, offPitch, offHeave;

            using (var rig = new MeshRig(seaState01: 0.35f, rockRollDegrees: 5f, rockPitchDegrees: 3f))
            {
                GameServices.Config = NewConfig(StormRockSettings.Default);   // ON (the default)
                SailMesh(rig, 240, out onRoll, out onPitch, out onHeave);
            }
            GameServices.Reset();
            using (var rig = new MeshRig(seaState01: 0.35f, rockRollDegrees: 5f, rockPitchDegrees: 3f))
            {
                StormRockSettings off = StormRockSettings.Default;
                off.Enabled = false;
                GameServices.Config = NewConfig(off);                          // OFF (the A/B)
                SailMesh(rig, 240, out offRoll, out offPitch, out offHeave);
            }

            for (int f = 0; f < onRoll.Length; f++)
            {
                Assert.AreEqual(offRoll[f], onRoll[f], $"roll frame {f}: the calm band must be BYTE-identical");
                Assert.AreEqual(offPitch[f], onPitch[f], $"pitch frame {f}: the calm band must be BYTE-identical");
                Assert.AreEqual(offHeave[f], onHeave[f], $"heave frame {f}: the calm band must be BYTE-identical");
            }
        }

        // ------------------------------------------------------------------ the weighted ride

        [Test]
        public void MeshHull_TheWeightedRide_IsReal_Honest_AndAttached()
        {
            // The same storm sailed twice — weight ON vs weight OFF (HeaveWeight01 0 = the exact
            // passthrough, so the OFF run IS the raw surface ride). The weighted ride must:
            // genuinely differ (weight is engaging); NEVER submarine below the surface by more
            // than the hard band (that side is a clamp); and stay attached on the hover side
            // within a bound DERIVED from the field itself (CI round 4 ruling: derive, never fit
            // — a copied measurement rots, a derived bound tracks the sea).
            const float draft = 0.5f;
            const float exaggeration = 2f;

            float[] weighted = SailRides(heaveWeight01: 1f, draft, exaggeration);
            GameServices.Reset();
            DisplacedSea.Clear(_seaOwner);
            float[] bolted = SailRides(heaveWeight01: 0f, draft, exaggeration);

            // THE DERIVED HOVER BOUND — the physics floor, computed against the very samples this
            // run produced (CI round 4: the 2.0 m constant was fantasy — the field's true demanded
            // unweighting on this reference sea is ~7.4 m). The IDEAL g-capped follower (infinite
            // stiffness, zero waste) rides the surface exactly, except where the surface's own
            // descent outruns the free-fall budget: there it departs with the surface's velocity,
            // falls at exactly the cap, and re-lands on contact. No follower obeying the downward
            // cap can hug this surface tighter, so its max gap is the FIELD'S OWN demanded
            // unweighting — deterministic, recomputed here every run, so a wave-field re-tune moves
            // the bound with the sea instead of rotting a constant. (The mechanism, measured: the
            // rogue combined crest departs the rider UPWARD at ~+9 m/s — the surface's own rate —
            // before plunging ~6 m, so the unweighting starts from a ballistic apex.)
            //
            // The REAL component may trail the ideal only by its finite-stiffness overhead, all of
            // it derived from the law's own constants:
            //   (1) g/ω² — the spring-vs-cap POSITION crossover: the largest gap at which the
            //       spring's command (ω²·gap) is still below the cap — the sub-cap regime where
            //       chasing gently is the DESIGN (the soft landing), not waste.
            //   (2) (g/ω) × riseTime — the LAUNCH overshoot: a finite-stiffness chaser carries a
            //       velocity lag of scale g/ω (its correction authority is g over its 1/ω response
            //       window), so at a crest departure it is flung with up to that much extra upward
            //       velocity vs the ideal (which matches the surface rate exactly); through the
            //       shared free-fall the velocity offset integrates over the event's growth time.
            //       Verified against this field: past the launch the component's gap runs PARALLEL
            //       to the ideal's — both descend at exactly the cap; the excess is launch state,
            //       not descent-budget waste.
            // Bound = idealMax × 1.1 + (g/ω)·riseTime + g/ω², every term derived, margins stated.
            // If the component exceeds it, the law is wasting budget beyond its stated overhead —
            // investigate the law, never re-margin this line.
            StormRockSettings storm = StormRockSettings.Default;
            float capAccel = WaveFieldSettings.Default.Gravity * storm.MaxDownwardAccelInGs;
            // ω_eff for this rig: no BoatController sibling ⇒ neutral hull response 1 ⇒ the base
            // stiffness exactly (the same resolution BoatWaveMotion.ResolveHullCharacter makes).
            float omega = storm.HeaveChaseStiffnessRadPerSec;
            float springCrossover = capAccel / (omega * omega);

            float idealX = bolted[0];
            float idealV = (bolted[1] - bolted[0]) / Dt;   // seeded riding the surface at its own rate
            float idealMax = 0f, idealMean = 0f;
            int eventStart = 0, riseFrames = 0;
            for (int f = 1; f < bolted.Length; f++)
            {
                float vFloor = idealV - capAccel * Dt;      // the fastest legal descent this frame
                float xFree = idealX + vFloor * Dt;
                if (xFree <= bolted[f])
                {
                    idealV = (bolted[f] - idealX) / Dt;     // ride the surface (up-moves uncapped)
                    idealX = bolted[f];
                }
                else
                {
                    idealV = vFloor;                        // unweight: free fall at exactly the cap
                    idealX = xFree;
                }
                float idealGap = idealX - bolted[f];
                if (idealGap <= springCrossover) eventStart = f;   // attached: the next event starts after here
                if (idealGap > idealMax)
                {
                    idealMax = idealGap;
                    riseFrames = f - eventStart;            // growth time of the worst unweighting event
                }
                idealMean += idealGap;
            }
            idealMean /= (bolted.Length - 1);

            float launchAllowance = (capAccel / omega) * (riseFrames * Dt);
            float hoverBound = idealMax * 1.1f + launchAllowance + springCrossover;

            float band = storm.SurfaceBandMeters;
            float maxGap = 0f, meanGap = 0f;
            int nearContact = 0;
            for (int f = 0; f < weighted.Length; f++)
            {
                float signedGap = weighted[f] - bolted[f];        // + = riding above the surface
                float gap = Mathf.Abs(signedGap);
                maxGap = Mathf.Max(maxGap, gap);
                meanGap += gap;
                if (gap < 0.1f) nearContact++;
                Assert.GreaterOrEqual(signedGap, -(band + 1e-3f),
                    $"frame {f}: the weighted ride SUBMARINED {-signedGap:F3} m under the surface " +
                    $"against a hard band of {band:F2} m");
                Assert.LessOrEqual(gap, hoverBound,
                    $"frame {f}: the weighted ride is {gap:F3} m off the surface against the " +
                    $"DERIVED bound {hoverBound:F3} m (ideal g-capped follower max {idealMax:F3} m " +
                    $"× 1.1 + launch allowance {launchAllowance:F3} m + spring crossover " +
                    $"{springCrossover:F3} m) — the law is wasting g-budget beyond its stated " +
                    "finite-stiffness overhead");
            }
            meanGap /= weighted.Length;
            Debug.Log($"[storm-read] weighted-vs-bolted ride: max gap {maxGap:F3} m, mean " +
                      $"{meanGap:F3} m, near-contact {nearContact}/{weighted.Length} frames; " +
                      $"ideal follower max {idealMax:F3} m (rise {riseFrames * Dt:F2} s), mean " +
                      $"{idealMean:F3} m; derived hover bound {hoverBound:F3} m (launch " +
                      $"{launchAllowance:F3}, crossover {springCrossover:F3})");

            Assert.Greater(idealMax, springCrossover,
                "the reference field never demanded unweighting beyond the spring crossover — the " +
                "derived bound is degenerate and this test has gone vacuous (did the storm sea get " +
                "re-tuned tame, or the exaggeration drop?)");
            Assert.Greater(maxGap, 0.02f,
                "the weighted ride never left the surface — the weight filter is not engaging in a " +
                "full storm, and 'obey gravity' shipped as a no-op");
            // The mean, on the same derivation: capped regimes match the ideal frame for frame
            // (the launch offset contributes its allowance over the event's fraction of the run);
            // sub-cap regimes (the tracking on either side of the surface) contribute at most the
            // crossover scale each on average. Every term computed above, none copied from a run.
            float meanBound = idealMean + 2f * springCrossover
                            + launchAllowance * (riseFrames * Dt) / (weighted.Length * Dt);
            Assert.Less(meanGap, meanBound,
                $"the weighted ride is off the surface on AVERAGE ({meanGap:F3} m vs the derived " +
                $"{meanBound:F3}) — sub-cap tracking should hug the surface within the crossover " +
                "scale, and capped falls should match the ideal follower");
            // A liveness floor, not a physics bound: the ideal re-lands after every event, so a
            // healthy chase touches near-contact (< 0.1 m) at least ~5% of a 10 s storm sail.
            Assert.Greater(nearContact, 30,
                "the hull almost never re-finds the surface — the chase is not landing between crests");
        }

        float[] SailRides(float heaveWeight01, float draft, float exaggeration)
        {
            StormRockSettings storm = StormRockSettings.Default;
            storm.HeaveWeight01 = heaveWeight01;

            using var rig = new MeshRig(seaState01: 1f, rockRollDegrees: 0f, rockPitchDegrees: 0f,
                                        draftMeters: draft);
            GameServices.Config = NewConfig(storm);
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(exaggeration, 0.6f));
            try
            {
                for (int w = 0; w < 5; w++) rig.Tick();
                var rides = new float[Frames];
                for (int f = 0; f < Frames; f++)
                {
                    rig.Tick();
                    // heave = (ride − sink)·px, where the sink is the design waterline through the
                    // iso projection (HullSettleMath — owner playtest 2026-08-07). It is a CONSTANT
                    // per hull, so it cancels out of every dynamic law below exactly as the raw
                    // draft used to; adding it back here recovers the ride itself.
                    rides[f] = rig.Renderer.HeavePixels / PxPerMetre
                             + HullSettleMath.AppliedSinkMeters(draft, RigElevationDegrees);
                }
                return rides;
            }
            finally
            {
                DisplacedSea.Clear(_seaOwner);
            }
        }

        // ------------------------------------------------------------------ the sprite frame path

        [Test]
        public void SpriteFrames_KeepPhaseHonesty_AndGainTheSurge_InAStorm()
        {
            // The storm surge/squash ride UNDER the frames; the frame CYCLE itself — the forward
            // phase walk, crest → 2 / trough → 6 — is untouched by B2.5 (the §5 honesty pin): every
            // frame change stays a single step in one consistent direction and the full cycle is
            // visited, exactly as SpriteRockFrameCycleTests pins at sea 0.40.
            (int[] frames, float maxOffset) = SailSprite(seaState01: 1f);

            var visited = new HashSet<int>();
            int direction = 0;
            int previous = -1;
            for (int f = 0; f < frames.Length; f++)
            {
                int frame = frames[f];
                if (frame < 0) continue;                  // pre-swell wake frames
                visited.Add(frame);
                if (previous >= 0 && frame != previous)
                {
                    int step = ((frame - previous) % 8 + 8) % 8;
                    Assert.IsTrue(step == 1 || step == 7,
                        $"frame {f}: the rock stepped {previous} → {frame} — the storm surge must " +
                        "never touch the frame walk (a skip is the stutter defect reborn)");
                    int thisDirection = step == 1 ? 1 : -1;
                    if (direction == 0) direction = thisDirection;
                    else Assert.AreEqual(direction, thisDirection,
                        $"frame {f}: the rock REVERSED — the forward-phase walk must survive the storm");
                }
                previous = frame;
            }
            Assert.AreEqual(8, visited.Count, "a 10 s storm sail must sweep the full 8-frame cycle");

            Assert.Greater(maxOffset, 0.02f,
                "the storm surge never moved the sprite — the frame path's whole B2.5 read (the " +
                "offset/squash under the frames) is not engaging");
        }

        [Test]
        public void SpriteFrames_InTheCalmBand_TransformStaysBitStill()
        {
            (_, float maxOffset) = SailSprite(seaState01: 0.35f);
            Assert.AreEqual(0f, maxOffset,
                "below the storm start the sprite's transform must stay EXACTLY at base — the " +
                "surge is a storm read, and the calm frame path is byte-identical to before B2.5");
        }

        (int[] frames, float maxAbsYOffset) SailSprite(float seaState01)
        {
            var clock = new ScriptedClock();
            GameServices.Clock = clock;
            GameServices.Environment = new ScriptedSea { SeaState01 = seaState01 };

            var root = new GameObject("Boat");
            _spawned.Add(root);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();

            var directional = root.AddComponent<DirectionalBoatSprite>();
            directional.Configure(new Sprite[8], sr);
            directional.ConfigureRock(new Sprite[8 * 8], 8);
            Assert.IsTrue(directional.HasRockGrid, "harness: the rock grid must gate ON");

            var wave = root.AddComponent<BoatWaveMotion>();
            wave.Configure(visual.transform, new SpriteHullPresenter(directional));

            float baseY = visual.transform.position.y;
            for (int w = 0; w < 5; w++) { clock.Advance(Dt); wave.Tick(); }

            var frames = new int[Frames];
            float maxOffset = 0f;
            for (int f = 0; f < Frames; f++)
            {
                clock.Advance(Dt);
                wave.Tick();
                frames[f] = directional.RockFrame;
                maxOffset = Mathf.Max(maxOffset, Mathf.Abs(visual.transform.position.y - baseY));
            }
            return (frames, maxOffset);
        }

        // ------------------------------------------------------------------ the config block

        [Test]
        public void GameConfig_CarriesTheStormBlock_AtTheReferenceDefaults()
        {
            // The owner tunes on GameConfig (rule 6); a freshly created config — and, because the
            // committed asset predates the field, the SHIPPED asset — must carry the reference
            // tuning. Spot-pin the load-bearing values (the full curve is pinned in
            // StormRockMathTests against the same Default).
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            StormRockSettings s = config.StormRock;

            Assert.IsTrue(s.Enabled, "B2.5 ships ON — the owner asked for it; calm is protected by the blend");
            Assert.AreEqual(0.4f, s.StormStartSeaState01, 1e-6f,
                "the storm start is the calm band's byte-identity boundary (the rock-smoothness " +
                "fixtures sail at exactly 0.40 and must read blend 0)");
            Assert.AreEqual(2.2f, s.MaxResponseMultiplier, 1e-6f);
            Assert.AreEqual(1f, s.MaxDownwardAccelInGs, 1e-6f, "the free-fall cap ships physical (1 g)");
            Assert.AreEqual(1f, s.HeaveWeight01, 1e-6f, "the weight ships fully engaged at full storm");
        }
    }
}
