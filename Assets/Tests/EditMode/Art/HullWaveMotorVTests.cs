using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A boat under motor lays a V</b> (the owner, 11-21: "the dory wake looks much better, make motored
    /// boats a v wake"; second look: "smooth the v wake arms, the stern wake looks stronger then the wake off
    /// the hull, it has this intermittant repeating pattern of a half circle, then space then a half circle
    /// repeating"). Each ring a motored hull lays carries the way she was going, and the water draws it as the
    /// straight piece of her V's arm it touches; the pieces overlap into one continuous arm, the inside of the V
    /// and the water ahead of her stay calm, and her stern's waves sit under her bow's. A ROWED hull (the dory
    /// under oars, the owner's approved look) lays exactly the rings she did, bit for bit.
    /// </summary>
    public class HullWaveMotorVTests
    {
        const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const float PuntLength = 5.2f, CapeLength = 12f;

        static HullWaveSettings S => HullWaveSettings.Default;

        // ==== 1. THE ARM WINDOW =======================================================================

        [Test]
        public void TheWindow_IsOneAtTheTouchingPoint_AndFallsSmoothlyToNothing()
        {
            Vector4 frame = HullWaveMath.VArmFrame(S);
            const float front = 6f, len = 4f;
            Assert.AreEqual(1f, HullWaveMath.VArmWindow(0f, front, len, frame, out float d0), 1e-6f);
            Assert.AreEqual(0f, d0, 1e-6f, "flat at its peak");
            Assert.AreEqual(0f, HullWaveMath.VArmWindow(len, front, len, frame, out _), 1e-6f, "astern: nothing at L");
            Assert.AreEqual(0f, HullWaveMath.VArmWindow(-len, front, len, frame, out _), 1e-6f, "forward: nothing at L");
            float prev = 1f;
            for (float x = 0.05f; x <= len; x += 0.05f)
            {
                float w = HullWaveMath.VArmWindow(x, front, len, frame, out float dw);
                Assert.LessOrEqual(w, prev + 1e-6f, $"only falls astern (at {x})");
                Assert.LessOrEqual(dw, 1e-6f);
                // its derivative is the window's own
                float fd = (HullWaveMath.VArmWindow(x + 1e-3f, front, len, frame, out _) -
                            HullWaveMath.VArmWindow(x - 1e-3f, front, len, frame, out _)) / 2e-3f;
                Assert.AreEqual(fd, dw, 2e-2f, $"slope at {x}");
                prev = w;
            }
        }

        [Test]
        public void TheWindow_NeverReachesForwardPastThePointThatLaidIt()
        {
            Vector4 frame = HullWaveMath.VArmFrame(S);
            float cot = frame.y / frame.x;
            const float front = 0.5f, len = 5f;           // a young ring: its touching point is front·cot α behind
            float lead = front * cot;
            Assert.Less(lead, len);
            Assert.Greater(HullWaveMath.VArmWindow(-0.5f * lead, front, len, frame, out _), 0f);
            Assert.AreEqual(0f, HullWaveMath.VArmWindow(-lead, front, len, frame, out _), 1e-6f);
            Assert.AreEqual(0f, HullWaveMath.VArmWindow(-1.5f * lead, front, len, frame, out _), 0f);
            Assert.AreEqual(0f, HullWaveMath.VArmWindow(-1f, 0f, len, frame, out _), 0f, "a newborn ring: nothing forward");
        }

        [Test]
        public void ThePiece_GrowsWithTheRing_AndIsNeverShorterThanTheBlendOrItsOverlap()
        {
            HullWaveSettings s = S;
            Vector4 frame = HullWaveMath.VArmFrame(in s), mix = HullWaveMath.VArmMix(in s);
            float k = 2f * Mathf.PI / 1.5f;
            Assert.AreEqual(s.MotorVArmBlendWavelengths * 1.5f, HullWaveMath.VArmHalfLength(0f, k, 0.5f, frame, mix), 1e-4f);
            Assert.AreEqual(10f * Mathf.Tan(s.MotorVArmHalfWidthDegrees * Mathf.Deg2Rad) + s.MotorVArmBlendWavelengths * 1.5f,
                            HullWaveMath.VArmHalfLength(10f, k, 0.5f, frame, mix), 1e-4f);
            // rings laid far apart: the piece reaches over its neighbours
            Assert.AreEqual(s.MotorVArmOverlapSpans * 6f, HullWaveMath.VArmHalfLength(0f, k, 6f, frame, mix), 1e-4f);
        }

        // ==== 2. THE PACKED DIRECTION =================================================================

        [Test]
        public void TheTravel_PacksIntoOneFloat_AndBackWithinAThousandth()
        {
            Assert.AreEqual(0f, HullWaveMath.PackTravel(Vector2.zero), 0f, "no travel packs as 0: a whole ring");
            Assert.AreEqual(Vector2.zero, HullWaveMath.UnpackTravel(0f));
            for (int i = 0; i < 360; i += 7)
            {
                float rad = i * Mathf.Deg2Rad;
                Vector2 u = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float packed = HullWaveMath.PackTravel(u * 3.7f);
                Assert.Greater(packed, 0.5f);
                Assert.AreEqual(packed, Mathf.Round(packed), 0f, "a whole number");
                Assert.Less(packed, 16777216f, "exact in a float");
                Vector2 back = HullWaveMath.UnpackTravel(packed);
                Assert.AreEqual(u.x, back.x, 1.1e-3f, $"x at {i} deg");
                Assert.AreEqual(u.y, back.y, 1.1e-3f, $"y at {i} deg");
            }
        }

        // ==== 3. THE CHAIN ============================================================================

        static HullWaveSource[] Run(float length, Vector2 travel, in HullWaveSettings s, float speed, bool legacy = false)
        {
            var field = new HullWaveField();
            field.Limit = s.MaxSources;
            var chain = new HullWaveChain();
            for (int i = 0; i <= 240; i++)
            {
                double now = 50.0 + i / 60.0;
                field.Advance(now);
                Vector2 at = new Vector2(-(float)(now - 50.0) * speed, 0f);
                if (legacy) chain.Step(field, at, now, speed, length, 1f, 15, 9, in s);
                else chain.Step(field, at, now, speed, length, 1f, 15, 9, travel, in s);
            }
            var all = new HullWaveSource[field.Count];
            for (int i = 0; i < all.Length; i++) all[i] = field[i];
            return all;
        }

        [Test]
        public void ARowedHull_LaysExactlyTheRingsSheDid()
        {
            HullWaveSource[] old = Run(4.5f, Vector2.zero, S, 1.65f, legacy: true);
            HullWaveSource[] now = Run(4.5f, Vector2.zero, S, 1.65f);
            Assert.Greater(old.Length, 3);
            Assert.AreEqual(old.Length, now.Length);
            for (int i = 0; i < old.Length; i++)
            {
                Assert.AreEqual(old[i].Position, now[i].Position);
                Assert.AreEqual(old[i].BirthSeconds, now[i].BirthSeconds);
                Assert.AreEqual(old[i].Amplitude, now[i].Amplitude);
                Assert.AreEqual(old[i].Wavenumber, now[i].Wavenumber);
                Assert.AreEqual(old[i].PhaseOffset, now[i].PhaseOffset);
                Assert.AreEqual(old[i].Arcs, now[i].Arcs);
                Assert.AreEqual(Vector2.zero, now[i].Travel, "a rowed ring carries no direction");
                Assert.AreEqual(0f, now[i].ArmSpan, 0f, "and no arm");
            }
        }

        [Test]
        public void ARowedHull_SamplesExactlyAsBefore_WithTheVSet()
        {
            // The rowed dory's rings through the field with the V's dials published: the old whole-ring law.
            HullWaveSettings s = S;
            var field = new HullWaveField { VArmFrame = HullWaveMath.VArmFrame(in s), VArmMix = HullWaveMath.VArmMix(in s) };
            field.Limit = s.MaxSources;
            var chain = new HullWaveChain();
            for (int i = 0; i <= 240; i++)
            {
                double now = 50.0 + i / 60.0;
                field.Advance(now);
                chain.Step(field, new Vector2(-(float)(now - 50.0) * 1.65f, 0f), now, 1.65f, 4.5f, 1f, 15, 9, Vector2.zero, in s);
            }
            for (float x = -8f; x <= 2f; x += 0.7f)
                for (float y = -3f; y <= 3f; y += 0.6f)
                {
                    Vector2 p = new Vector2(x, y);
                    float h = 0f; Vector2 g = Vector2.zero;
                    for (int i = 0; i < field.Count; i++)
                    {
                        HullWaveSource r = field[i];
                        double age = field.NowSeconds - r.BirthSeconds;
                        float amp = r.AmplitudeAt(field.NowSeconds);
                        if (amp <= 0f) continue;
                        Vector2 d = p - r.Position; float rr = d.magnitude;
                        float ph = HullWaveMath.Phase(r.Wavenumber, r.PhaseSpeed, age, r.PhaseOffset);
                        h += HullWaveMath.SourceHeight(rr, amp, r.RingSpeed * (float)age, r.Wavenumber, ph, 1f / r.PacketWidth, 1f / s.SpreadMetres);
                        if (rr >= 1e-4f)
                            g += d * (HullWaveMath.SourceSlope(rr, amp, r.RingSpeed * (float)age, r.Wavenumber, ph, 1f / r.PacketWidth, 1f / s.SpreadMetres) / rr);
                    }
                    Assert.AreEqual(h, field.SampleHeight(p, s.SpreadMetres), 0f, $"height at {p}");
                    Assert.AreEqual(g, field.SampleSlope(p, s.SpreadMetres), $"slope at {p}");
                }
        }

        [Test]
        public void AMotoredShortHull_LaysCleanVRings_NotTheDorysScatteredArcs()
        {
            HullWaveSource[] v = Run(PuntLength, Vector2.left * 2f, S, 6f);
            Assert.Greater(v.Length, 3);
            HullWaveSettings plain = S;
            plain.SmallHullLengthMetres = 0f;
            HullWaveSource[] longHull = Run(PuntLength, Vector2.zero, plain, 6f);
            Assert.AreEqual(longHull.Length, v.Length, "spaced as a long hull is, not at the dory's close spacing");
            foreach (HullWaveSource r in v)
            {
                Assert.IsFalse(r.Arcs, "no broken arcs: the arm draws her");
                Assert.AreEqual(0f, r.PhaseOffset, 0f, "no scatter");
                Assert.AreEqual(Vector2.left, r.Travel, "the way she was going, as a unit vector");
                Assert.AreEqual(0f, r.Position.y, 1e-6f, "laid on her track");
                Assert.Greater(r.ArmSpan, 0f, "each stands for a stretch of arm");
            }
        }

        [Test]
        public void ShareZero_KeepsTheOldRings_ForAMotoredHullToo()
        {
            HullWaveSettings off = S;
            off.MotorVShare = 0f;
            HullWaveSource[] old = Run(PuntLength, Vector2.zero, off, 6f, legacy: true);
            HullWaveSource[] now = Run(PuntLength, Vector2.left, off, 6f);
            Assert.AreEqual(old.Length, now.Length);
            for (int i = 0; i < old.Length; i++)
            {
                Assert.AreEqual(old[i].Amplitude, now[i].Amplitude);
                Assert.AreEqual(old[i].Arcs, now[i].Arcs);
                Assert.AreEqual(Vector2.zero, now[i].Travel, "the dial off lays no direction");
            }
        }

        // ==== 4. THE ARM AND THE ENVELOPE =============================================================

        static HullWaveField Laid(float length, float speed, in HullWaveSettings s, int budget = 15, float turnDegPerSec = 0f)
        {
            var field = new HullWaveField { VArmFrame = HullWaveMath.VArmFrame(in s), VArmMix = HullWaveMath.VArmMix(in s) };
            field.Limit = s.MaxSources;
            var chain = new HullWaveChain();
            Vector2 at = Vector2.zero; float heading = 180f;
            for (int i = 0; i <= 600; i++)
            {
                double now = 50.0 + i / 60.0;
                field.Advance(now);
                if (i > 0)
                {
                    heading += turnDegPerSec / 60f;
                    at += speed / 60f * new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad));
                }
                Vector2 travel = new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad));
                chain.Step(field, at, now, speed, length, 1f, budget, 9, travel, in s);
            }
            return field;
        }

        static Vector2 ChainPoint(float speed) => new Vector2(-10f * speed, 0f);   // where Laid ends a straight run

        // Mean |height| over points in her own frame: k = |y| / (x·tan α) across the V (1 = on an arm).
        static float Band(HullWaveField field, Vector2 stern, float reach, float kLo, float kHi, float spread)
        {
            float tanA = Mathf.Tan(S.KelvinHalfAngleDegrees * Mathf.Deg2Rad);
            float sum = 0f; int n = 0;
            for (float x = 2f; x < reach; x += 0.37f)
                for (float y = -reach; y < reach; y += 0.23f)
                {
                    float k = Mathf.Abs(y) / (x * tanA);
                    if (k < kLo || k >= kHi) continue;
                    sum += Mathf.Abs(field.SampleHeight(stern + new Vector2(x, y), spread));
                    n++;
                }
            return n > 0 ? sum / n : 0f;
        }

        [Test]
        public void TheVsArms_CarryTheWaves_AndItsInsideIsCalm()
        {
            foreach (float length in new[] { PuntLength, CapeLength })
            {
                HullWaveSettings s = S;
                float speed = 5f;
                HullWaveField v = Laid(length, speed, in s);
                Vector2 stern = ChainPoint(speed);
                float reach = 3f * length;
                float arms = Band(v, stern, reach, 0.75f, 1.25f, s.SpreadMetres);
                float inside = Band(v, stern, reach, 0f, 0.45f, s.SpreadMetres);
                Assert.Greater(arms, 0f, $"{length} m: her arms draw");
                Assert.Less(inside, 0.25f * arms, $"{length} m: the inside of her V is calm (inside {inside}, arms {arms})");

                HullWaveSettings rings = s;
                rings.MotorVShare = 0f;
                HullWaveField whole = Laid(length, speed, in rings);
                float wholeInside = Band(whole, stern, reach, 0f, 0.45f, s.SpreadMetres);
                Assert.Less(inside, 0.5f * wholeInside, $"{length} m: whole rings filled the V; the V does not");
            }
        }

        // The crest along one arm from the chain point back: the highest |height| across ±half a wavelength of the
        // arm line at each step. Returns the residual's spread from a 4-wavelength moving mean over the mean
        // ("beadiness") between 20 % and 85 % of the arm, and that stretch's mean crest.
        static float Beadiness(HullWaveField field, Vector2 from, float speed, float length, in HullWaveSettings s, out float mean, out float ac)
        {
            float alpha = s.KelvinHalfAngleDegrees * Mathf.Deg2Rad;
            Vector2 u = Vector2.left, port = new Vector2(0f, -1f);       // she runs -x; port of -x is -y
            Vector2 t = -u * Mathf.Cos(alpha) + port * Mathf.Sin(alpha);
            Vector2 n = u * Mathf.Sin(alpha) + port * Mathf.Cos(alpha);
            float lambda = HullWaveMath.Wavelength(speed, length, in s);
            float q1 = 3f * length;
            const int N = 400;
            var e = new float[N];
            for (int j = 0; j < N; j++)
            {
                Vector2 c = from + t * (q1 * (j + 0.5f) / N);
                float best = 0f;
                for (int k = -4; k <= 4; k++) best = Mathf.Max(best, Mathf.Abs(field.SampleHeight(c + n * (k * lambda / 8f), s.SpreadMetres)));
                e[j] = best;
            }
            // over the arm she has laid (out to where its crest falls under a quarter of its highest), not past its end
            float top = 0f; foreach (float x in e) top = Mathf.Max(top, x);
            int extent = 0;
            for (int j = 0; j < N; j++) if (e[j] > 0.25f * top) extent = j;
            int lo = (int)(0.15f * extent), hi = Mathf.Max(lo + 10, (int)(0.85f * extent));
            int half = Mathf.Max(2, Mathf.RoundToInt(2f * lambda / (q1 / N)));
            double sum = 0, r2 = 0, rs = 0; int cnt = 0;
            var res = new float[N];
            for (int j = lo; j < hi; j++)
            {
                double a = 0; int m = 0;
                for (int q = Mathf.Max(0, j - half); q <= Mathf.Min(N - 1, j + half); q++) { a += e[q]; m++; }
                res[j] = (float)(e[j] - a / m);
                sum += e[j]; rs += res[j]; r2 += res[j] * res[j]; cnt++;
            }
            mean = (float)(sum / cnt);
            double rm = rs / cnt;
            double den = r2 - cnt * rm * rm;
            // the highest autocorrelation after it first falls below zero: a repeat, not the residual's own smoothness
            ac = 0f;
            bool crossed = false;
            for (int lag = 1; lag < cnt / 2; lag++)
            {
                double num = 0;
                for (int j = lo; j + lag < hi; j++) num += (res[j] - rm) * (res[j + lag] - rm);
                float c = den > 1e-12 ? (float)(num / den * cnt / (cnt - lag)) : 0f;
                if (c < 0f) crossed = true;
                else if (crossed) ac = Mathf.Max(ac, c);
            }
            return (float)(System.Math.Sqrt(System.Math.Max(r2 / cnt - rm * rm, 0)) / System.Math.Max(mean, 1e-9));
        }

        [Test]
        public void EachArm_IsOneContinuousLine_NotARowOfBeads()
        {
            string report = "", bad = "";
            foreach (float length in new[] { PuntLength, CapeLength, 38f })
            {
                HullWaveSettings s = S;
                float speed = 5f;
                // the stern's budget, and the bow's at the old quarter share (5 sources, widely spaced: the beat)
                foreach (int budget in new[] { 10, 5 })
                {
                    HullWaveField v = Laid(length, speed, in s, budget);
                    float bead = Beadiness(v, ChainPoint(speed), speed, length, in s, out float mean, out float ac);
                    report += $"{length} m, {budget} sources: beadiness {bead:0.0000}, repeat {ac:0.000}, mean {mean:0.00000}\n";
                    Assert.Greater(mean, 0f, $"{length} m, {budget} sources: the arm draws");
                    // a short arm's own rise and fall reads about 0.14 here (no repeat); widely spaced pieces read 0.2 and up
                    if (bead >= 0.16f) bad += $"{length} m, {budget} sources: beaded ({bead})\n";
                    // a repeat matters only where there is ripple to see
                    if (ac >= 0.5f && bead >= 0.03f) bad += $"{length} m, {budget} sources: a visible repeating beat ({ac} at beadiness {bead})\n";
                }
            }
            TestContext.WriteLine(report);
            Assert.IsEmpty(bad, bad + report);
        }

        [Test]
        public void TheArm_IsHerCrestHeight_WhateverTheSpacing()
        {
            HullWaveSettings s = S;
            const float speed = 5f;
            HullWaveField close = Laid(CapeLength, speed, in s, 10);
            HullWaveField wide = Laid(CapeLength, speed, in s, 5);
            Beadiness(close, ChainPoint(speed), speed, CapeLength, in s, out float a, out _);
            Beadiness(wide, ChainPoint(speed), speed, CapeLength, in s, out float b, out _);
            Assert.AreEqual(1f, b / a, 0.25f, $"twice the spacing, the same arm ({a} vs {b})");
        }

        [Test]
        public void NothingDrawsAheadOfTheChainPoint()
        {
            HullWaveSettings s = S;
            const float speed = 5f;
            HullWaveField v = Laid(CapeLength, speed, in s);
            Vector2 at = ChainPoint(speed);
            float arm = Band(v, at, 3f * CapeLength, 0.75f, 1.25f, s.SpreadMetres);
            float ahead = 0f;
            for (float x = 0.5f; x <= 12f; x += 0.5f)
                for (float y = -6f; y <= 6f; y += 0.5f)
                    ahead = Mathf.Max(ahead, Mathf.Abs(v.SampleHeight(at + new Vector2(-x, y), s.SpreadMetres)));
            Assert.Less(ahead, 0.05f * arm, $"ahead of her: calm ({ahead} vs arms {arm})");
        }

        [Test]
        public void ATurn_BendsTheArm_WithoutAGap()
        {
            HullWaveSettings s = S;
            HullWaveField v = Laid(PuntLength, 6f, in s, 15, -30f);
            Vector2 last = Vector2.zero; float heading = 180f;
            Vector2 at = Vector2.zero;
            for (int i = 1; i <= 600; i++)
            {
                heading += -30f / 60f;
                at += 6f / 60f * new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad));
            }
            last = at;
            // Walk back along her (bent) track; beside each point, out to where her arm should be, the crest
            // never drops to nothing between her and 2.5 s astern.
            float lambda = HullWaveMath.Wavelength(6f, PuntLength, in s);
            Vector2 p = last; float h = heading; int gaps = 0, steps = 0;
            for (int i = 0; i < 150; i++)
            {
                Vector2 dir = new Vector2(Mathf.Cos(h * Mathf.Deg2Rad), Mathf.Sin(h * Mathf.Deg2Rad));
                p -= dir * (6f / 60f); h += 30f / 60f;
                if (i < 20) continue;
                float back = (i + 1) * 6f / 60f;
                float lat = back * Mathf.Tan(s.KelvinHalfAngleDegrees * Mathf.Deg2Rad);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 nrm = new Vector2(-dir.y, dir.x) * side;
                    float best = 0f;
                    for (float o = -0.6f * lat; o <= 0.6f * lat; o += lambda / 6f)
                        best = Mathf.Max(best, Mathf.Abs(v.SampleHeight(p + nrm * (lat + o), s.SpreadMetres)));
                    steps++;
                    if (best < 1e-4f) gaps++;
                }
            }
            Assert.AreEqual(0, gaps, $"{gaps} of {steps} stations along the bend found no arm");
        }

        // ==== 5. THE ARM'S LAW AND ITS SLOPE ==========================================================

        [Test]
        public void TheArmsSlope_IsTheGradientOfItsHeight()
        {
            HullWaveSettings s = S;
            Vector4 frame = HullWaveMath.VArmFrame(in s), mix = HullWaveMath.VArmMix(in s);
            Vector2 u = new Vector2(0.6f, 0.8f);
            const float amp = 0.2f, front = 5f, k = 2f * Mathf.PI / 1.4f, span = 1.3f;
            float phase = HullWaveMath.VArmPhase(k, front), inv = 1f / s.SpreadMetres;
            int checkedPts = 0;
            for (float x = -12f; x <= 12f; x += 0.83f)
                for (float y = -12f; y <= 12f; y += 0.79f)
                {
                    Vector2 d = new Vector2(x, y);
                    float h = HullWaveMath.ArmHeight(d, u, amp, front, k, phase, span, inv, frame, mix, out Vector2 g);
                    if (Mathf.Abs(h) < 1e-5f && g.sqrMagnitude < 1e-10f) continue;
                    // stay off the side switch on her track line, where the slope has a crease
                    if (Mathf.Abs(Vector2.Dot(d, new Vector2(-u.y, u.x))) < 0.05f) continue;
                    const float e = 1e-3f;
                    float gx = (HullWaveMath.ArmHeight(d + new Vector2(e, 0f), u, amp, front, k, phase, span, inv, frame, mix, out _) -
                                HullWaveMath.ArmHeight(d - new Vector2(e, 0f), u, amp, front, k, phase, span, inv, frame, mix, out _)) / (2f * e);
                    float gy = (HullWaveMath.ArmHeight(d + new Vector2(0f, e), u, amp, front, k, phase, span, inv, frame, mix, out _) -
                                HullWaveMath.ArmHeight(d - new Vector2(0f, e), u, amp, front, k, phase, span, inv, frame, mix, out _)) / (2f * e);
                    Assert.AreEqual(gx, g.x, 0.02f + 0.02f * Mathf.Abs(gx), $"d/dx at {d}");
                    Assert.AreEqual(gy, g.y, 0.02f + 0.02f * Mathf.Abs(gy), $"d/dy at {d}");
                    checkedPts++;
                }
            Assert.Greater(checkedPts, 20);
        }

        [Test]
        public void TheArm_IsContinuousAcrossHerTrack()
        {
            HullWaveSettings s = S;
            Vector4 frame = HullWaveMath.VArmFrame(in s), mix = HullWaveMath.VArmMix(in s);
            Vector2 u = Vector2.left, port = new Vector2(0f, -1f);
            const float amp = 0.2f, front = 5f, k = 2f * Mathf.PI / 1.4f, span = 1.3f;
            float phase = HullWaveMath.VArmPhase(k, front);
            for (float x = -20f; x <= 20f; x += 0.5f)
            {
                float a = HullWaveMath.ArmHeight(new Vector2(x, 0f) + port * 1e-4f, u, amp, front, k, phase, span, 0.25f, frame, mix, out _);
                float b = HullWaveMath.ArmHeight(new Vector2(x, 0f) - port * 1e-4f, u, amp, front, k, phase, span, 0.25f, frame, mix, out _);
                Assert.AreEqual(a, b, 1e-4f, $"at {x} on her track");
            }
        }

        [Test]
        public void TheField_SamplesAVRingByTheArmLaw()
        {
            HullWaveSettings s = S;
            var field = new HullWaveField { VArmFrame = HullWaveMath.VArmFrame(in s), VArmMix = HullWaveMath.VArmMix(in s) };
            var ring = new HullWaveSource
            {
                Position = new Vector2(3f, -2f), BirthSeconds = 1.0, Amplitude = 0.1f, Wavenumber = 2f * Mathf.PI / 1.2f,
                PhaseSpeed = 1f, RingSpeed = 1.6f, PacketWidth = s.PacketWavelengths * 1.2f, Lifetime = 8f, DecaySeconds = 4f,
                Travel = Vector2.up, ArmSpan = 1.1f,
            };
            field.Advance(1.0);
            field.Add(in ring);
            field.Advance(3.5);
            float amp = ring.AmplitudeAt(3.5), front = ring.RingSpeed * 2.5f;
            for (float x = -10f; x <= 10f; x += 1.3f)
                for (float y = -10f; y <= 10f; y += 1.1f)
                {
                    Vector2 p = ring.Position + new Vector2(x, y);
                    float h = HullWaveMath.ArmHeight(p - ring.Position, ring.Travel, amp, front, ring.Wavenumber,
                        HullWaveMath.VArmPhase(ring.Wavenumber, front), ring.ArmSpan, 1f / s.SpreadMetres,
                        field.VArmFrame, field.VArmMix, out Vector2 g);
                    Assert.AreEqual(h, field.SampleHeight(p, s.SpreadMetres), 1e-6f, $"at {p}");
                    Vector2 fs = field.SampleSlope(p, s.SpreadMetres);
                    Assert.AreEqual(g.x, fs.x, 1e-6f); Assert.AreEqual(g.y, fs.y, 1e-6f);
                }
        }

        [Test]
        public void TheStern_SitsUnderTheBow_AndTheBowIsLaidAsCloselyAsTheStern()
        {
            HullWaveSettings s = S;
            Assert.LessOrEqual(s.MotorVSternShare, s.BowShare, "a motored hull's stern waves are no stronger than her bow's");
            Assert.GreaterOrEqual(s.MotorVBowSourceShare, s.BowSourceShare, "her bow arm gets at least the old share of sources");
        }

        // ==== 6. THE PLUMBING =========================================================================

        static string Squashed() => Regex.Replace(
            File.ReadAllText(Path.Combine(Application.dataPath, "..", ShaderPath)), @"\s+", "");

        static float Define(string name)
        {
            string src = File.ReadAllText(Path.Combine(Application.dataPath, "..", ShaderPath));
            Match m = Regex.Match(src, @"(?m)^\s*#define\s+" + name + @"\s+([0-9.]+)");
            Assert.IsTrue(m.Success, name + " is not defined in the water shader");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        [Test]
        public void TheShader_DrawsTheArmAsTheCSharpDoes()
        {
            Assert.AreEqual(HullWaveMath.TravelSteps, Define("HH_HULL_WAVE_TRAVEL_STEPS"), 0f);
            Assert.AreEqual(HullWaveMath.TravelHalfRange, Define("HH_HULL_WAVE_TRAVEL_HALF"), 0f);
            Assert.AreEqual(HullWaveMath.VArmWindowArea, Define("HH_HULL_WAVE_ARM_AREA"), 1e-6f);
            string src = Squashed();
            StringAssert.Contains("float4_HHHullWaveV;", src);
            StringAssert.Contains("float4_HHHullWaveV2;", src);
            foreach (string piece in new[]
                     {
                         "floatgrown=max(front,0.0)*_HHHullWaveV.z+_HHHullWaveV.w*6.2831853/k;",
                         "returnmax(max(grown,_HHHullWaveV2.z*max(span,0.0)),1e-3);",
                         "floatarmL=vRing?HullWaveArmHalfLength(a.w,b.x,b.z):0.0;",
                         "floatp=b.w-1.0;",
                         "floatqx=floor(p/HH_HULL_WAVE_TRAVEL_STEPS);",
                         "float2u=float2(qx,p-qx*HH_HULL_WAVE_TRAVEL_STEPS)/HH_HULL_WAVE_TRAVEL_HALF-1.0;",
                         "float2port=float2(-u.y,u.x);",
                         "floatside=dot(d,port)>=0.0?1.0:-1.0;",
                         "float2n=u*_HHHullWaveV.x+port*(side*_HHHullWaveV.y);",
                         "float2t=-u*_HHHullWaveV.y+port*(side*_HHHullWaveV.x);",
                         "floatlead=min(armL,max(a.w,0.0)*_HHHullWaveV.y/max(_HHHullWaveV.x,1e-4));",
                         "floatlen=max(along<0.0?lead:armL,1e-4);",
                         "floatw=q*q;",
                         "floatdw=-4.0*q*x/len;",
                         "floatnorm=b.z/(HH_HULL_WAVE_ARM_AREA*armL);",
                         "HullWaveSourceHeight(rr,a.z,a.w,b.x,b.y,invWidth,invSpread,h0,dhdr);",
                         "dhdr=across>0.0?dhdr:0.0;",
                         "slope=(n*(dhdr*w)+t*(h0*dw))*norm;",
                     })
                StringAssert.Contains(piece, src, "the shader's arm moved away from HullWaveMath.ArmHeight");
        }

        [Test]
        public void TheSourceLoop_KeepsTheWholeRingsReach_AndDrawsVRingsAsArms()
        {
            string src = Squashed();
            StringAssert.Contains("floatinvWidth=vRing?b.x*_HHHullWaveV2.y:abs(b.z);", src);
            StringAssert.Contains("floatreach=a.w+HH_HULL_WAVE_PACKET_REACH/invWidth;", src,
                "the reach is the band radius plus the packet's cut, as HullWaveField.Pack sent it");
            StringAssert.Contains("if(r2>=reach*reach+armL*armL)continue;", src, "a V ring's reach is HullWaveMath.VArmReach");
            StringAssert.Contains("HullWaveArmSource(d,a,b,invWidth,armL,invSpread,hv,gv);", src);
            StringAssert.Contains("HullWaveSourceHeight(r,a.z,a.w,b.x,b.y,abs(b.z),invSpread,h,dhdr);", src,
                "a whole ring is drawn exactly as before");
            StringAssert.Contains("floatarc=HullWaveArcGain(d,a.xy,b.z);", src);
            StringAssert.DoesNotContain("HullWaveVGain", src, "the first look's angular window is gone");
        }

        [Test]
        public void ThePack_SendsAVRingsArmAndTheWholeRingAsBefore()
        {
            HullWaveSettings s = S;
            var field = new HullWaveField { VArmFrame = HullWaveMath.VArmFrame(in s), VArmMix = HullWaveMath.VArmMix(in s) };
            var motor = new HullWaveSource
            {
                Position = Vector2.zero, BirthSeconds = 1.0, Amplitude = 0.1f, Wavenumber = Mathf.PI, PhaseSpeed = 1f,
                RingSpeed = 1f, PacketWidth = 2f * s.PacketWavelengths, Lifetime = 8f, DecaySeconds = 4f, Travel = Vector2.up,
                ArmSpan = 0.9f,
            };
            var rowed = motor;
            rowed.Travel = Vector2.zero;
            rowed.ArmSpan = 0f;
            rowed.Position = new Vector2(1f, 0f);
            field.Advance(1.0);
            field.Add(in motor);
            field.Add(in rowed);
            field.Advance(2.0);
            var a = new Vector4[4]; var b = new Vector4[4];
            Assert.AreEqual(2, field.Pack(a, b, new Rect(-50f, -50f, 100f, 100f), true));
            Assert.AreEqual(HullWaveMath.PackTravel(Vector2.up), b[0].w, 0f);
            Assert.AreEqual(0.9f, b[0].z, 0f, "a V ring sends its arm span in the 1/width slot");
            Assert.AreEqual(HullWaveMath.VArmPhase(Mathf.PI, a[0].w), b[0].y, 0f, "its crest locked to its front");
            Assert.AreEqual(1f / motor.PacketWidth, HullWaveMath.VArmInvWidth(b[0].x, field.VArmMix), 1e-5f,
                "the shader's width from k is the ring's own");
            Assert.AreEqual(0f, b[1].w, 0f, "a whole ring sends 0");
            Assert.AreEqual(1f / rowed.PacketWidth, b[1].z, 1e-6f, "and its 1/width as before");
            Assert.AreEqual(HullWaveMath.Phase(rowed.Wavenumber, rowed.PhaseSpeed, 1.0, 0f), b[1].y, 0f);

            // A V ring's reach covers its arm piece: a view that only its piece reaches still uploads it.
            float reach = HullWaveMath.VArmReach(a[0].w, motor.Wavenumber, motor.ArmSpan, field.VArmFrame, field.VArmMix);
            Assert.Greater(reach, a[0].w + HullWaveMath.PacketReach * motor.PacketWidth);
            var farView = new Rect(reach - 0.01f, -0.5f, 5f, 1f);
            Assert.AreEqual(1, field.Pack(a, b, farView, true) - (HullWaveField.CircleTouchesRect(rowed.Position,
                1f + HullWaveMath.PacketReach * rowed.PacketWidth, farView) ? 1 : 0));
        }

        [Test]
        public void UnsetV_TheFieldPacksAndSamplesWholeRings()
        {
            var field = new HullWaveField();
            Assert.AreEqual(Vector4.zero, field.VArmMix, "unset: share 0, every ring whole (as the shader's unset global)");
            var ring = new HullWaveSource
            {
                Position = Vector2.zero, BirthSeconds = 1.0, Amplitude = 0.1f, Wavenumber = Mathf.PI, PhaseSpeed = 1f,
                RingSpeed = 1f, PacketWidth = 1.6f, Lifetime = 8f, DecaySeconds = 4f, Travel = Vector2.up, ArmSpan = 0.9f,
            };
            field.Advance(1.0); field.Add(in ring); field.Advance(2.0);
            var a = new Vector4[2]; var b = new Vector4[2];
            field.Pack(a, b, default, false);
            Assert.AreEqual(0f, b[0].w, 0f); Assert.AreEqual(1f / 1.6f, b[0].z, 1e-6f);
        }

        [Test]
        public void TheBridge_PublishesTheArmsFrameAndMix()
        {
            Assert.AreEqual(Shader.PropertyToID("_HHHullWaveV"), HiddenHarbours.Art.HullWaveBridge.IdV);
            Assert.AreEqual(Shader.PropertyToID("_HHHullWaveV2"), HiddenHarbours.Art.HullWaveBridge.IdV2);
            HullWaveSettings s = S;
            Vector4 f = HullWaveMath.VArmFrame(in s), m = HullWaveMath.VArmMix(in s);
            Assert.AreEqual(Mathf.Sin(s.KelvinHalfAngleDegrees * Mathf.Deg2Rad), f.x, 1e-6f);
            Assert.AreEqual(Mathf.Cos(s.KelvinHalfAngleDegrees * Mathf.Deg2Rad), f.y, 1e-6f);
            Assert.AreEqual(Mathf.Tan(s.MotorVArmHalfWidthDegrees * Mathf.Deg2Rad), f.z, 1e-6f);
            Assert.AreEqual(s.MotorVArmBlendWavelengths, f.w, 0f);
            Assert.AreEqual(s.MotorVShare, m.x, 0f);
            Assert.AreEqual(1f / (2f * Mathf.PI * s.PacketWavelengths), m.y, 1e-7f);
            Assert.AreEqual(s.MotorVArmOverlapSpans, m.z, 0f);
        }

        [Test]
        public void TheDials_AreTheOwnersInGameConfig_AndTheCodeDefaultsMatch()
        {
            string yaml = File.ReadAllText(Path.Combine(Application.dataPath, "..", ConfigPath)).Replace("\r\n", "\n");
            Assert.AreEqual(S.MotorVShare, YamlFloat(yaml, "MotorVShare"), 0f);
            Assert.AreEqual(S.MotorVArmHalfWidthDegrees, YamlFloat(yaml, "MotorVArmHalfWidthDegrees"), 0f);
            Assert.AreEqual(S.MotorVArmBlendWavelengths, YamlFloat(yaml, "MotorVArmBlendWavelengths"), 0f);
            Assert.AreEqual(S.MotorVArmOverlapSpans, YamlFloat(yaml, "MotorVArmOverlapSpans"), 0f);
            Assert.AreEqual(S.MotorVSternShare, YamlFloat(yaml, "MotorVSternShare"), 0f);
            Assert.AreEqual(S.MotorVBowSourceShare, YamlFloat(yaml, "MotorVBowSourceShare"), 0f);
        }

        static float YamlFloat(string yaml, string key)
        {
            Match m = Regex.Match(yaml, @"(?m)^\s+" + key + @":\s*([-0-9.eE]+)\s*$");
            Assert.IsTrue(m.Success, key + " is not in GameConfig");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }
}
