using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// WORKING THE LURE (owner's ask 2026-07-25) — jigging as the second way to fish. Pins the owner's
    /// two rules: each tackle wants its OWN action (so the five pieces feel different in the hand, not
    /// just on the catch roll), and a lure you aren't working is nearly dead while BAIT still fishes
    /// itself. Also pins the measurement, because a hand-motion read that can be fooled by holding
    /// still would quietly hand the player a free catch.
    /// </summary>
    public class JiggingTests
    {
        private static readonly JiggingSettings J = JiggingSettings.Default;

        /// <summary>Work the rod for <paramref name="seconds"/> at a given tempo and stroke size, as a
        /// clean back-and-forth — what a player's hand actually does. dt is injected, so this is exact
        /// and repeatable with no clock involved.</summary>
        private static JigWork WorkedAt(float strokesPerSec, float strokeMetres, float seconds = 6f,
                                        float dt = 1f / 60f)
        {
            var jig = new JigWork();
            float halfPeriod = 1f / Mathf.Max(0.01f, strokesPerSec);
            float t = 0f;
            while (t < seconds)
            {
                // A triangle wave: out one way for a half-period, back the other. Each turn is a stroke.
                float phase = (t % (halfPeriod * 2f)) / (halfPeriod * 2f);
                float x = phase < 0.5f ? Mathf.Lerp(0f, strokeMetres, phase * 2f)
                                       : Mathf.Lerp(strokeMetres, 0f, (phase - 0.5f) * 2f);
                jig.Tick(dt, new Vector2(x, 0f), true);
                t += dt;
            }
            return jig;
        }

        // ---- the measurement itself ---------------------------------------------------------------

        [Test]
        public void AStillRod_ReadsAsNoWorkAtAll()
        {
            var jig = new JigWork();
            for (int i = 0; i < 240; i++) jig.Tick(1f / 60f, new Vector2(3f, 2f), true);   // parked
            Assert.AreEqual(0f, jig.StrokesPerSec, 1e-3f, "a motionless hand is not jigging");
            Assert.AreEqual(0f, jig.StrokeMetres, 1e-3f);
        }

        [Test]
        public void StoppingMidStroke_FallsAwayRatherThanCoasting()
        {
            // The trap: park the mouse halfway through a lift and the naive read never sees another
            // reversal, so it would report the old tempo forever and fish the lure for free.
            var jig = WorkedAt(2f, 0.8f);
            Assert.Greater(jig.StrokesPerSec, 0.1f, "the setup must actually be working");

            // Ten seconds of stillness: past the stall grace (which must be long enough for a slow
            // crawl's 2 s stroke), then decaying.
            for (int i = 0; i < 600; i++) jig.Tick(1f / 60f, new Vector2(0.4f, 0f), true);   // hand rests
            Assert.Less(jig.StrokesPerSec, 0.05f,
                "a rod left still must go quiet on its own — otherwise you jig once and coast");
        }

        [Test]
        public void LosingThePointer_ReadsAsStopped_NotAsFrozen()
        {
            var jig = WorkedAt(2f, 0.8f);
            for (int i = 0; i < 300; i++) jig.Tick(1f / 60f, default, false);
            Assert.Less(jig.StrokesPerSec, 0.05f, "no pointer means no hands on the rod");
        }

        [Test]
        public void TinyJitter_IsNotJigging()
        {
            // Hand tremor and pixel noise must not read as furious work.
            var jig = new JigWork();
            for (int i = 0; i < 600; i++)
                jig.Tick(1f / 60f, new Vector2(i % 2 == 0 ? 0f : 0.01f, 0f), true);
            Assert.Less(jig.StrokesPerSec, 0.05f, "a wobble under the minimum stroke isn't a stroke");
        }

        // ---- each tackle wants its OWN action (the owner's first rule) ----------------------------

        [Test]
        public void EachTackle_IsBestServedByItsOwnAction()
        {
            // THE payoff test: work the rod the way each style wants, and that style must score higher
            // than every other style would for the same hand motion. If this ever fails, the five
            // pieces of tackle have collapsed into one and the tackle box is decoration.
            var styles = new[]
            {
                JigStyle.LiftAndDrop, JigStyle.QuickJerks, JigStyle.SteadySweep,
                JigStyle.SlowCrawl,  JigStyle.FastSteady,
            };

            foreach (JigStyle style in styles)
            {
                JigMath.GetTarget(style, in J, out float tempo, out float stroke);
                var worked = WorkedAt(tempo, stroke);
                float own = JigMath.Quality01(style, worked.StrokesPerSec, worked.StrokeMetres, in J);

                Assert.Greater(own, 0.6f,
                    $"{style}: working it exactly as asked must score well — if the target can't be hit, " +
                    "the action is unlearnable");

                foreach (JigStyle other in styles)
                {
                    if (other == style) continue;
                    float wrong = JigMath.Quality01(other, worked.StrokesPerSec, worked.StrokeMetres, in J);
                    Assert.Greater(own, wrong,
                        $"working like a {style} must suit a {style} better than a {other} — otherwise " +
                        "the two actions aren't actually different in the hand");
                }
            }
        }

        [Test]
        public void BothAxesMustBeRight_NotJustOne()
        {
            // Right tempo, wrong stroke size is NOT half-right — fast little twitches are not a slow
            // heave. The score multiplies the axes for exactly this reason.
            JigMath.GetTarget(JigStyle.LiftAndDrop, in J, out float tempo, out float stroke);
            var rightTempoTinyStroke = WorkedAt(tempo, stroke * 0.15f);

            float score = JigMath.Quality01(JigStyle.LiftAndDrop,
                rightTempoTinyStroke.StrokesPerSec, rightTempoTinyStroke.StrokeMetres, in J);
            Assert.Less(score, 0.5f, "matching only one axis must not read as competent jigging");
        }

        // ---- "nearly dead — but bait still fishes" (the owner's second rule) ----------------------

        [Test]
        public void ADeadLure_WaitsFarLonger_ButIsNeverCompletelyDead()
        {
            float dead = JigMath.BiteDelayScale(JigStyle.LiftAndDrop, 0f, in J);
            float worked = JigMath.BiteDelayScale(JigStyle.LiftAndDrop, 1f, in J);

            Assert.AreEqual(1f, worked, 1e-4f, "a well-worked lure fishes at full speed");
            Assert.Greater(dead, 4f,
                "a motionless lure must wait MUCH longer — 'nearly dead' has to be felt");
            Assert.IsFalse(float.IsInfinity(dead),
                "…but never infinite: nothing else in this fishing is a hard wall, and a distracted " +
                "player should still get the occasional fish rather than a system that silently stopped");
        }

        [Test]
        public void Bait_FishesItself_WhateverYourHandsAreDoing()
        {
            // The owner's rule, and the reason bait and lure are two ways to SPEND ATTENTION rather
            // than two stat blocks: with bait on, sitting perfectly still costs nothing.
            Assert.AreEqual(1f, JigMath.Quality01(JigStyle.None, 0f, 0f, in J), 1e-6f);
            Assert.AreEqual(1f, JigMath.BiteDelayScale(JigStyle.None, 0f, in J), 1e-6f,
                "a baited hook waits exactly as long whether you work it or not");
        }

        // ---- guards --------------------------------------------------------------------------------

        [Test]
        public void TheMathsIsNaNSafe()
        {
            float nan = float.NaN;
            Assert.IsFalse(float.IsNaN(JigMath.Quality01(JigStyle.QuickJerks, nan, nan, in J)));
            Assert.IsFalse(float.IsNaN(JigMath.BiteDelayScale(JigStyle.QuickJerks, nan, in J)));
            Assert.IsFalse(float.IsNaN(JigMath.RatioMatch01(nan, nan, nan)));
            Assert.DoesNotThrow(() => new JigWork().Tick(nan, new Vector2(nan, nan), true));
        }

        [Test]
        public void TheDefaultActions_AreGenuinelyDistinct()
        {
            // A content guard: if two styles ever get tuned onto the same numbers, the tackle box
            // quietly loses a dimension and nobody notices.
            var styles = new[]
            {
                JigStyle.LiftAndDrop, JigStyle.QuickJerks, JigStyle.SteadySweep,
                JigStyle.SlowCrawl,  JigStyle.FastSteady,
            };
            for (int i = 0; i < styles.Length; i++)
            for (int k = i + 1; k < styles.Length; k++)
            {
                JigMath.GetTarget(styles[i], in J, out float t1, out float s1);
                JigMath.GetTarget(styles[k], in J, out float t2, out float s2);
                Assert.IsTrue(!Mathf.Approximately(t1, t2) || !Mathf.Approximately(s1, s2),
                    $"{styles[i]} and {styles[k]} ask for the same thing — one of them is redundant");
            }
        }
    }
}
