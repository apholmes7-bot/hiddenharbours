using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The heading dial of the figure pixel look (<see cref="FigureHeadingDial"/>,
    /// <see cref="GameConfig.FigureHeadingSteps"/>; the owner's D2 of 10-15: built at 0, smooth). 0 and
    /// below are the identity bit for bit; N steps give exactly N on-screen facings through either sign
    /// of a def's measured azimuth; aboard, the hull's own turn stays smooth under her; and the dial is
    /// held at 0 whenever the look is OFF.
    /// </summary>
    public class FigureHeadingDialTests
    {
        private static object _sink;
        private static float _acc;

        [Test]
        public void ZeroOrFewerSteps_IsTheIdentity_BitForBit()
        {
            float[] inputs = { 0f, -0f, float.Epsilon, -float.Epsilon, 1e-7f, 22.5f, 33.3f, -33.3f, 179.99f, 180f,
                               -180f, 359.999f, 720.25f, -1234.5678f, float.NaN, float.PositiveInfinity };
            int[] steps = { 0, -1, -8, int.MinValue };
            foreach (int n in steps)
            {
                foreach (float a in inputs)
                {
                    Assert.AreEqual(Bits(a), Bits(FigureHeadingDial.Quantize(a, n)), $"Quantize({a}, {n})");
                    Assert.AreEqual(Bits(a), Bits(FigureHeadingDial.Heading(a, n)), $"Heading({a}, {n})");
                    Assert.AreEqual(Bits(a), Bits(FigureHeadingDial.DeckYaw(a, 137.5f, n)), $"DeckYaw({a}, 137.5, {n})");
                }
            }
        }

        [TestCase(8)]
        [TestCase(16)]
        [TestCase(32)]
        public void NSteps_GiveNFacings_ThroughEitherAzimuthSign(int n)
        {
            double step = 360d / n;
            foreach (bool counterClockwise in new[] { false, true })
            {
                var seen = new bool[n];
                for (int i = 0; i < 3600; i++)
                {
                    float bearing = i * 0.1f;
                    float compass = FigureHeadingDial.Heading(bearing, n);
                    // The presenters' order: the dial first, then the def's measured azimuth sign.
                    float yaw = counterClockwise ? -compass : compass;
                    double k = yaw / step;
                    string what = $"{n} steps, {(counterClockwise ? "CCW" : "CW")} def, bearing {bearing}";
                    Assert.AreEqual(Math.Round(k), k, 1e-4d, $"{what}: yaw {yaw} is not on a step");
                    Assert.LessOrEqual(Math.Abs(bearing - compass), step / 2d + 1e-4d,
                                       $"{what}: the nearest step is {compass}, more than half a step away");
                    seen[(int)(((long)Math.Round(k) % n + n) % n)] = true;
                }
                int facings = 0;
                foreach (bool s in seen) if (s) facings++;
                Assert.AreEqual(n, facings, $"{n} steps through a {(counterClockwise ? "CCW" : "CW")} def: distinct facings");
            }
        }

        [Test]
        public void TheDial_IsOdd_SoEitherSignGivesTheSameFacings()
        {
            foreach (int n in new[] { 8, 16, 32 })
            {
                double step = 360d / n;
                for (int i = 0; i <= 7200; i++)
                {
                    float a = i * 0.05f;
                    Assert.AreEqual(-FigureHeadingDial.Quantize(a, n), FigureHeadingDial.Quantize(-a, n), 0f,
                                    $"{n} steps at {a}");
                }
                // The ties, where only an odd rounding rule keeps the two signs mirror images.
                for (int k = -n; k <= n; k++)
                {
                    float tie = (float)((k + 0.5d) * step);
                    Assert.AreEqual(-FigureHeadingDial.Quantize(tie, n), FigureHeadingDial.Quantize(-tie, n), 0f,
                                    $"{n} steps at the tie {tie}");
                    Assert.AreEqual((float)((k + (k >= 0 ? 1 : 0)) * step), FigureHeadingDial.Quantize(tie, n), 0f,
                                    $"{n} steps: the tie {tie} goes away from zero");
                }
            }
        }

        [Test]
        public void Aboard_TheSumLandsOnAStep_AndTheHullKeepsItsOwnTurn()
        {
            float[] hullTurns = { 0f, 13.7f, -101.25f, 45f, 359.9f, -720.3f };
            foreach (int n in new[] { 8, 16, 32 })
            {
                double step = 360d / n;
                foreach (float hull in hullTurns)
                {
                    for (int i = -1800; i <= 1800; i += 7)
                    {
                        float yaw = i * 0.1f;
                        float deck = FigureHeadingDial.DeckYaw(yaw, hull, n);
                        double k = (hull + (double)deck) / step;
                        string what = $"{n} steps, hull {hull}, yaw {yaw}";
                        Assert.AreEqual(Math.Round(k), k, 1e-3d, $"{what}: her on-screen facing {hull + deck} is not on a step");
                        Assert.LessOrEqual(Math.Abs(deck - yaw), step / 2d + 1e-3d, $"{what}: she turned more than half a step");
                    }
                }
            }
        }

        [Test]
        public void TheLiveSteps_AreTheConfigs_OnlyWhileTheLookIsOn()
        {
            GameConfig previous = GameServices.Config;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                GameServices.Config = null;
                Assert.AreEqual(0, FigureHeadingDial.LiveSteps, "no config: the code defaults, OFF and 0");

                GameServices.Config = config;
                config.FigurePixelTarget = false;
                config.FigureHeadingSteps = 16;
                Assert.AreEqual(0, FigureHeadingDial.LiveSteps, "OFF holds the dial at 0 whatever the steps say");

                config.FigurePixelTarget = true;
                Assert.AreEqual(16, FigureHeadingDial.LiveSteps, "ON: the config's steps");

                config.FigureHeadingSteps = -4;
                Assert.AreEqual(0, FigureHeadingDial.LiveSteps, "a negative count validates to 0, the identity");
            }
            finally
            {
                GameServices.Config = previous;
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void TurningTheDial_AllocatesNothing()
        {
            _acc = Turn(8);   // warm-up

            // The recorder must see an allocation here, or the negative below would pass on anything.
            Assert.That(() => { _sink = new object[64]; }, Is.AllocatingGCMemory(),
                        "harness: the GC.Alloc recorder saw no allocation at all");
            Assert.That(() => { _acc = Turn(256); }, Is.Not.AllocatingGCMemory(),
                        "turning the dial allocated (rule 7: nothing per frame)");
        }

        private static float Turn(int frames)
        {
            float acc = 0f;
            for (int i = 0; i < frames; i++)
            {
                acc += FigureHeadingDial.Quantize(i * 1.7f, 16) + FigureHeadingDial.Heading(i * 2.3f, 8);
                acc += FigureHeadingDial.DeckYaw(i * 0.9f, i * 0.37f, 32) + FigureHeadingDial.LiveSteps;
            }
            return acc;
        }

        private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    }
}
