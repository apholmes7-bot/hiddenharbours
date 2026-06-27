using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The PURE-logic guard for the boat wake (the owner's brief: follow the boat / travel with the current /
    /// the waves distort it / once it loses force it dissipates). Every claim is exercised on the
    /// side-effect-free <see cref="WakeParticleSystem"/> math headless — no Unity scene, no physics step:
    /// <list type="bullet">
    /// <item><description><b>Follow</b> — emission rate scales with speed; NONE below the threshold or when
    /// aground; foam is shed at the stern; the V wings diverge.</description></item>
    /// <item><description><b>Travel with the current</b> — a live puff's position moves by (own velocity +
    /// current)·dt, and its own velocity decays toward only-the-current drift.</description></item>
    /// <item><description><b>Waves distort it</b> — zero distortion on glassy water, growing with sea-state;
    /// deterministic for identical inputs.</description></item>
    /// <item><description><b>Dissipate</b> — opacity fades monotonically to ~0 and size spreads monotonically
    /// up over a particle's life; the particle dies at its lifetime.</description></item>
    /// </list>
    /// Plus determinism: same inputs → identical emission, advection and wobble across runs (CLAUDE.md rule 5).
    /// </summary>
    public class WakeParticleSystemTests
    {
        private static WakeConfig Cfg() => WakeConfig.Default;

        // ==== brief 1: FOLLOW THE BOAT — emission gating & geometry =====================================

        [Test]
        public void Emission_BelowSpeedThreshold_ShedsNothing()
        {
            var cfg = Cfg();
            float carry = 0f;
            int n = WakeParticleSystem.EmissionCount(cfg.SpeedThreshold * 0.5f, aground: false, cfg, 1f, ref carry);
            Assert.AreEqual(0, n, "a boat slower than the threshold leaves no wake");
        }

        [Test]
        public void Emission_WhenAground_ShedsNothing_EvenAtSpeed()
        {
            var cfg = Cfg();
            float carry = 0f;
            int n = WakeParticleSystem.EmissionCount(speed: 5f, aground: true, cfg, 1f, ref carry);
            Assert.AreEqual(0, n, "an aground boat throws no wake (it isn't pushing water)");
        }

        [Test]
        public void Emission_RisesWithSpeed_AboveThreshold()
        {
            var cfg = Cfg();
            float slowCarry = 0f, fastCarry = 0f;
            int slow = WakeParticleSystem.EmissionCount(cfg.SpeedThreshold + 1f, false, cfg, 1f, ref slowCarry);
            int fast = WakeParticleSystem.EmissionCount(cfg.SpeedThreshold + 4f, false, cfg, 1f, ref fastCarry);
            Assert.Greater(fast, slow, "a faster boat sheds more foam per second");
            Assert.Greater(slow, 0, "above the threshold a wake forms");
        }

        [Test]
        public void Emission_CarriesFraction_SoSlowBoatsStillEmitEventually()
        {
            var cfg = Cfg();
            // A tiny dt so rate*dt < 1 — the count should be 0 some ticks then 1, never stuck at 0 forever.
            float carry = 0f;
            int total = 0;
            for (int i = 0; i < 60; i++)
                total += WakeParticleSystem.EmissionCount(cfg.SpeedThreshold + 1f, false, cfg, 0.05f, ref carry);
            Assert.Greater(total, 0, "the fractional carry must accumulate into whole puffs over time");
        }

        [Test]
        public void Emission_Stopping_ResetsCarry_NoBurpOnRestart()
        {
            var cfg = Cfg();
            float carry = 0.9f;   // pretend we had nearly accumulated a puff
            WakeParticleSystem.EmissionCount(0f, aground: false, cfg, 1f, ref carry);
            Assert.AreEqual(0f, carry, "stopping clears the carried fraction so the wake doesn't burp on restart");
        }

        [Test]
        public void SternEmitPoint_IsBehindTheBow()
        {
            // Bow pointing +Y, boat at origin → stern is at −Y by the offset.
            Vector2 stern = WakeParticleSystem.SternEmitPoint(Vector2.zero, Vector2.up, 0.5f);
            Assert.Less(stern.y, 0f, "foam sheds astern of the hull");
            Assert.AreEqual(-0.5f, stern.y, 1e-4f);
        }

        [Test]
        public void EmitVelocity_TwoWings_DivergeAstern()
        {
            var cfg = Cfg();
            Vector2 port = WakeParticleSystem.EmitVelocity(Vector2.up, speed: 3f, side: -1, cfg);
            Vector2 stbd = WakeParticleSystem.EmitVelocity(Vector2.up, speed: 3f, side: +1, cfg);
            // Both go astern (−Y component) ...
            Assert.Less(port.y, 0f, "the port wing washes astern");
            Assert.Less(stbd.y, 0f, "the starboard wing washes astern");
            // ... and they spread to opposite sides (mirrored X).
            Assert.AreEqual(-stbd.x, port.x, 1e-4f, "the two wings are mirror images → a diverging V");
            Assert.That(Mathf.Abs(port.x), Is.GreaterThan(0f), "the wings actually diverge, not straight astern");
        }

        [Test]
        public void EmitVelocity_ScalesWithSpeed()
        {
            var cfg = Cfg();
            Vector2 slow = WakeParticleSystem.EmitVelocity(Vector2.up, 2f, -1, cfg);
            Vector2 fast = WakeParticleSystem.EmitVelocity(Vector2.up, 6f, -1, cfg);
            Assert.Greater(fast.magnitude, slow.magnitude, "a faster boat throws a livelier wash");
        }

        [Test]
        public void Emit_ShedsParticlesAtOrAsternOfTheSternApex()
        {
            // With the V-arm placement, fresh puffs are spread ALONG the arms (apex at t=0, astern as t→1),
            // so the right invariant is: every fresh puff is at the stern apex or BEHIND it — never ahead of
            // the hull. (The apex itself equals the old single stern emit point.)
            var cfg = Cfg();
            var sys = new WakeParticleSystem(64);
            Assert.AreEqual(0, sys.AliveCount);
            sys.Emit(32, Vector2.zero, Vector2.up, speed: 3f, cfg);
            Assert.AreEqual(32, sys.AliveCount, "all puffs went live");
            // Bow points +Y → astern is −Y. The apex is at −SternOffset; nothing may sit ahead of it (y > apex.y).
            float apexY = -cfg.SternOffset;
            foreach (var p in sys.Pool)
                if (p.Alive)
                    Assert.LessOrEqual(p.Pos.y, apexY + 1e-4f,
                        "a fresh puff is shed at or astern of the stern apex, never ahead of the hull");
        }

        [Test]
        public void ArmEmitPoint_ApexAtSternThenWidensWithDistance()
        {
            var cfg = Cfg();
            Vector2 bow = Vector2.up;
            Vector2 apex = WakeParticleSystem.SternEmitPoint(Vector2.zero, bow, cfg.SternOffset);

            // At t=0 both wings start at the apex (the stern).
            Vector2 p0 = WakeParticleSystem.ArmEmitPoint(Vector2.zero, bow, 0f, -1, cfg);
            Assert.AreEqual(apex, p0, "the V apex sits exactly at the stern");

            // Farther along the arm = farther astern AND farther out to the side (the V WIDENS with distance).
            Vector2 near = WakeParticleSystem.ArmEmitPoint(Vector2.zero, bow, 0.3f, +1, cfg);
            Vector2 far  = WakeParticleSystem.ArmEmitPoint(Vector2.zero, bow, 0.9f, +1, cfg);
            Assert.Less(far.y, near.y, "farther along the arm is farther astern");
            Assert.Greater(Mathf.Abs(far.x), Mathf.Abs(near.x), "the arm spreads wider the farther astern it runs");
            // Lateral spread grows in proportion to astern distance (a straight diverging line at the half-angle).
            float tan = Mathf.Tan(Mathf.Deg2Rad * cfg.VHalfAngleDeg);
            float asternFar = apex.y - far.y;     // distance astern of the apex
            Assert.AreEqual(asternFar * tan, Mathf.Abs(far.x), 1e-3f,
                "the arm holds the Kelvin half-angle (|lateral| = tan(halfAngle)·asternDistance)");
        }

        [Test]
        public void ArmEmitPoint_WingsMirrorAcrossTheCentreline()
        {
            var cfg = Cfg();
            Vector2 bow = Vector2.up;
            Vector2 port = WakeParticleSystem.ArmEmitPoint(Vector2.zero, bow, 0.7f, -1, cfg);
            Vector2 stbd = WakeParticleSystem.ArmEmitPoint(Vector2.zero, bow, 0.7f, +1, cfg);
            Assert.AreEqual(-stbd.x, port.x, 1e-4f, "the two arms are mirror images → a symmetric V");
            Assert.AreEqual(stbd.y, port.y, 1e-4f, "and sit at the same astern distance");
            Assert.Greater(Mathf.Abs(port.x), 0f, "the arms actually diverge, not straight astern");
        }

        [Test]
        public void SternFillPoint_StaysInsideTheArms()
        {
            // The turbulent centre fill must never punch past the crisp arm edges, or it would blur the V.
            var cfg = Cfg();
            Vector2 bow = Vector2.up;
            for (float t = 0f; t <= 1f; t += 0.1f)
            for (float lat = -1f; lat <= 1f; lat += 0.25f)
            {
                Vector2 fill = WakeParticleSystem.SternFillPoint(Vector2.zero, bow, t, lat, cfg);
                Vector2 arm  = WakeParticleSystem.ArmEmitPoint(Vector2.zero, bow, t, +1, cfg);
                // At the same along-distance the arm edge is the widest allowed |x|; the fill (scaled by
                // SternFillWidth ≤ 1) must be no wider.
                Assert.LessOrEqual(Mathf.Abs(fill.x), Mathf.Abs(arm.x) + 1e-4f,
                    "the stern fill stays within the V arms so it never blurs the crisp edges");
            }
        }

        [Test]
        public void Emit_RecyclesPool_NeverExceedsCapacity()
        {
            var cfg = Cfg();
            var sys = new WakeParticleSystem(8);
            sys.Emit(50, Vector2.zero, Vector2.up, 3f, cfg);   // far more than the pool
            Assert.LessOrEqual(sys.AliveCount, 8, "the fixed pool never grows — old puffs recycle");
        }

        // ==== brief 2: TRAVEL WITH THE CURRENT (+ own momentum, with decay) =============================

        [Test]
        public void Advect_MovesByOwnVelocityPlusCurrent()
        {
            var p = new WakeParticleSystem.Particle
            {
                Alive = true, Pos = Vector2.zero, Vel = new Vector2(1f, 0f),
                Age = 0f, Lifetime = 10f, Seed = 0f, BaseSize = 0.3f,
            };
            Vector2 current = new Vector2(0f, 2f);
            var stepped = WakeParticleSystem.Advect(p, current, velocityDecay: 1f, dt: 1f);
            // pos += (vel + current) * dt = (1,0)+(0,2) = (1,2)
            Assert.AreEqual(1f, stepped.Pos.x, 1e-4f, "drifts by its own forward momentum");
            Assert.AreEqual(2f, stepped.Pos.y, 1e-4f, "AND by the live current — it travels with the tide");
        }

        [Test]
        public void Advect_PureCurrent_NoOwnVelocity_StillDrifts()
        {
            // A puff that has lost all its own push (vel ~0) still moves with the current — the brief's
            // "once it loses force a distance from the boat … the current's drift remains".
            var p = new WakeParticleSystem.Particle
            {
                Alive = true, Pos = Vector2.zero, Vel = Vector2.zero,
                Age = 0f, Lifetime = 10f, Seed = 0f, BaseSize = 0.3f,
            };
            var stepped = WakeParticleSystem.Advect(p, new Vector2(0.5f, -0.5f), 0.3f, 1f);
            Assert.AreEqual(new Vector2(0.5f, -0.5f), stepped.Pos, "a spent puff still sets with the current");
        }

        [Test]
        public void Advect_VelocityDecays_TowardCurrentOnlyDrift()
        {
            var p = new WakeParticleSystem.Particle
            {
                Alive = true, Pos = Vector2.zero, Vel = new Vector2(4f, 0f),
                Age = 0f, Lifetime = 100f, Seed = 0f, BaseSize = 0.3f,
            };
            float v0 = p.Vel.magnitude;
            // Step several times; the OWN velocity must shrink (the wake "loses force").
            for (int i = 0; i < 5; i++)
                p = WakeParticleSystem.Advect(p, Vector2.zero, velocityDecay: 0.5f, dt: 0.2f);
            Assert.Less(p.Vel.magnitude, v0, "the puff's own push fades over time");
            Assert.Greater(p.Vel.magnitude, 0f, "but doesn't snap to zero — it eases off");
        }

        [Test]
        public void Advect_DecayIsFrameRateIndependent()
        {
            // One step of dt=1 must reach the same speed as five steps of dt=0.2 (exponential composes).
            var a = new WakeParticleSystem.Particle { Alive = true, Vel = new Vector2(3f, 0f), Lifetime = 100f };
            var b = a;
            a = WakeParticleSystem.Advect(a, Vector2.zero, 0.4f, 1f);
            for (int i = 0; i < 5; i++) b = WakeParticleSystem.Advect(b, Vector2.zero, 0.4f, 0.2f);
            Assert.AreEqual(a.Vel.magnitude, b.Vel.magnitude, 1e-3f,
                "exponential decay is frame-rate independent (the time-constant form composes)");
        }

        // ==== brief 4: DISSIPATE — fade + spread + lifetime =============================================

        [Test]
        public void LifeFade_IsMonotonicNonIncreasing_AndReachesZero()
        {
            var cfg = Cfg();
            float prev = float.MaxValue;
            for (float t = 0f; t <= 1f + 1e-4f; t += 0.05f)
            {
                float a = WakeParticleSystem.LifeFade(t, cfg);
                Assert.LessOrEqual(a, prev + 1e-5f, "opacity never rises over a puff's life");
                prev = a;
            }
            Assert.AreEqual(0f, WakeParticleSystem.LifeFade(1f, cfg), 1e-4f, "fully dissolved at end of life");
            Assert.Greater(WakeParticleSystem.LifeFade(0f, cfg), 0f, "a fresh puff is visible");
        }

        [Test]
        public void LifeSpread_IsMonotonicNonDecreasing_AndGrows()
        {
            var cfg = Cfg();
            float baseSize = 0.3f;
            float prev = -1f;
            for (float t = 0f; t <= 1f + 1e-4f; t += 0.05f)
            {
                float s = WakeParticleSystem.LifeSpread(baseSize, t, cfg);
                Assert.GreaterOrEqual(s, prev - 1e-5f, "size never shrinks over a puff's life");
                prev = s;
            }
            Assert.Greater(WakeParticleSystem.LifeSpread(baseSize, 1f, cfg),
                           WakeParticleSystem.LifeSpread(baseSize, 0f, cfg),
                           "the foam spreads as it dissolves");
        }

        [Test]
        public void Advect_DiesAtLifetime()
        {
            var p = new WakeParticleSystem.Particle { Alive = true, Lifetime = 1f, Age = 0f };
            p = WakeParticleSystem.Advect(p, Vector2.zero, 1f, 0.6f);
            Assert.IsTrue(p.Alive, "still alive mid-life");
            p = WakeParticleSystem.Advect(p, Vector2.zero, 1f, 0.6f);   // age 1.2 > lifetime 1
            Assert.IsFalse(p.Alive, "the puff dies once it outlives its lifetime");
        }

        [Test]
        public void Step_AgesPoolAndCullsDeadPuffs()
        {
            var cfg = Cfg();
            var sys = new WakeParticleSystem(16);
            sys.Emit(8, Vector2.zero, Vector2.up, 3f, cfg);
            Assert.AreEqual(8, sys.AliveCount);
            // Step well past the max possible lifetime → all dead.
            float bigDt = cfg.Lifetime * (1f + cfg.LifetimeJitter) + 1f;
            sys.Step(Vector2.zero, cfg.VelocityDecay, bigDt);
            Assert.AreEqual(0, sys.AliveCount, "every puff has dissolved past its lifetime");
        }

        // ==== brief 3: WAVES DISTORT IT — sea-state-scaled, deterministic ===============================

        [Test]
        public void WaveDistort_GlassyWater_NoDistortion()
        {
            var cfg = Cfg();
            Vector2 d = WakeParticleSystem.WaveDistort(new Vector2(3f, 4f), time: 2f, seed: 0.5f,
                                                       roughness: 0f, cfg);
            Assert.AreEqual(Vector2.zero, d, "glassy water leaves the wake undistorted");
        }

        [Test]
        public void WaveDistort_RougherSea_DistortsMore()
        {
            var cfg = Cfg();
            var pos = new Vector2(3f, 4f);
            float calm = WakeParticleSystem.WaveDistort(pos, 2f, 0.5f, 0.2f, cfg).magnitude;
            float rough = WakeParticleSystem.WaveDistort(pos, 2f, 0.5f, 1f, cfg).magnitude;
            Assert.Greater(rough, calm, "a rougher sea wobbles/breaks up the wake more");
        }

        [Test]
        public void WaveDistort_BoundedByAmplitudeTimesRoughness()
        {
            var cfg = Cfg();
            // Value-noise mapped to −1..1 per axis → |component| ≤ amp; vector magnitude ≤ amp·√2.
            float amp = cfg.WaveDistortAmount;
            for (int i = 0; i < 50; i++)
            {
                var pos = new Vector2(i * 1.3f, i * -0.7f);
                Vector2 d = WakeParticleSystem.WaveDistort(pos, i * 0.5f, i * 0.13f, 1f, cfg);
                Assert.LessOrEqual(Mathf.Abs(d.x), amp + 1e-4f, "x wobble within the amplitude bound");
                Assert.LessOrEqual(Mathf.Abs(d.y), amp + 1e-4f, "y wobble within the amplitude bound");
            }
        }

        [Test]
        public void WaveDistort_IsDeterministic()
        {
            var cfg = Cfg();
            var pos = new Vector2(7.3f, -2.1f);
            Vector2 a = WakeParticleSystem.WaveDistort(pos, 5f, 0.42f, 0.8f, cfg);
            Vector2 b = WakeParticleSystem.WaveDistort(pos, 5f, 0.42f, 0.8f, cfg);
            Assert.AreEqual(a, b, "identical inputs reproduce the identical wobble (rule 5)");
        }

        [Test]
        public void RenderPosition_IsBasePosPlusWobble_NotAccumulated()
        {
            var cfg = Cfg();
            var p = new WakeParticleSystem.Particle
            {
                Alive = true, Pos = new Vector2(5f, 6f), Seed = 0.3f, Lifetime = 10f,
            };
            Vector2 rp = WakeParticleSystem.RenderPosition(in p, 1f, 1f, cfg);
            Vector2 expected = p.Pos + WakeParticleSystem.WaveDistort(p.Pos, 1f, p.Seed, 1f, cfg);
            Assert.AreEqual(expected, rp, "render = integrated pos + a display-only wobble (never accumulates)");
            // The integrated position is untouched by rendering.
            Assert.AreEqual(new Vector2(5f, 6f), p.Pos, "the wobble must not drift the actual particle position");
        }

        // ==== sea-state → roughness mapping (shared with the water's choppiness scale) ===================

        [Test]
        public void SeaStateRoughness_GlassIsZero_StormIsOne()
        {
            Assert.AreEqual(0f, BoatWakeEmitter.SeaStateRoughness(SeaState.Glass), 1e-4f);
            Assert.AreEqual(1f, BoatWakeEmitter.SeaStateRoughness(SeaState.Storm), 1e-4f);
            Assert.Greater(BoatWakeEmitter.SeaStateRoughness(SeaState.Rough),
                           BoatWakeEmitter.SeaStateRoughness(SeaState.Calm),
                           "roughness rises with the sea-state tier");
        }

        // ==== determinism across whole-system runs (rule 5) =============================================

        [Test]
        public void Emit_IsDeterministic_AcrossRuns()
        {
            var cfg = Cfg();
            var a = new WakeParticleSystem(32);
            var b = new WakeParticleSystem(32);
            for (int i = 0; i < 10; i++)
            {
                a.Emit(3, new Vector2(i, 0), Vector2.up, 3f, cfg);
                b.Emit(3, new Vector2(i, 0), Vector2.up, 3f, cfg);
                a.Step(new Vector2(0.1f, 0.2f), cfg.VelocityDecay, 0.1f);
                b.Step(new Vector2(0.1f, 0.2f), cfg.VelocityDecay, 0.1f);
            }
            Assert.AreEqual(a.AliveCount, b.AliveCount, "two identical runs keep the same live count");
            var pa = a.Pool; var pb = b.Pool;
            for (int i = 0; i < pa.Length; i++)
            {
                Assert.AreEqual(pa[i].Alive, pb[i].Alive, $"slot {i} alive-state matches");
                if (pa[i].Alive)
                {
                    Assert.AreEqual(pa[i].Pos, pb[i].Pos, $"slot {i} position is bit-stable across runs");
                    Assert.AreEqual(pa[i].Seed, pb[i].Seed, $"slot {i} seed is deterministic (no RNG)");
                }
            }
        }

        [Test]
        public void Hash01_IsStable_AndInUnitRange()
        {
            for (uint i = 0; i < 1000; i++)
            {
                float h = WakeParticleSystem.Hash01(i);
                Assert.GreaterOrEqual(h, 0f);
                Assert.Less(h, 1f);
                Assert.AreEqual(h, WakeParticleSystem.Hash01(i), "the hash is pure/stable");
            }
        }
    }
}
