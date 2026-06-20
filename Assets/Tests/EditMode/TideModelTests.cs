using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards the most important property of the sim: the tide is DETERMINISTIC from time, so it
    /// can be recomputed rather than saved (tech-architecture.md §1, §6). If these break, saves
    /// and Pillar-1 learnability are at risk.
    /// </summary>
    public class TideModelTests
    {
        private static GameConfig Config() => ScriptableObject.CreateInstance<GameConfig>();

        private static float RangeOverDay(double startSeconds, TideProfile p, GameConfig cfg)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i <= 200; i++)
            {
                double t = startSeconds + cfg.SecondsPerDay * (i / 200.0);
                float h = TideModel.Height(t, p, cfg);
                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }
            return max - min;
        }

        [Test]
        public void Height_IsReproducible()
        {
            var cfg = Config();
            var p = TideProfile.CoddleCove;
            Assert.AreEqual(TideModel.Height(123456.0, p, cfg), TideModel.Height(123456.0, p, cfg));
        }

        [Test]
        public void SpringRange_ExceedsNeapRange()
        {
            var cfg = Config();
            var p = TideProfile.CoddleCove;
            float spring = RangeOverDay(0.0, p, cfg);                                   // envelope = 1
            double neapStart = cfg.LunarMonthDays / 4.0 * cfg.SecondsPerDay;            // envelope = 0
            float neap = RangeOverDay(neapStart, p, cfg);
            Assert.Greater(spring, neap, "Spring tides should swing wider than neap tides.");
        }

        [Test]
        public void FundyRips_SwingsMoreThanCoddleCove()
        {
            var cfg = Config();
            Assert.Greater(RangeOverDay(0.0, TideProfile.FundyRips, cfg),
                           RangeOverDay(0.0, TideProfile.CoddleCove, cfg));
        }

        [Test]
        public void MeanLevel_IsAboutZeroOverASeason()
        {
            var cfg = Config();
            var p = TideProfile.CoddleCove;
            double sum = 0; int n = 4000;
            for (int i = 0; i < n; i++)
                sum += TideModel.Height(cfg.SecondsPerSeason * (i / (double)n), p, cfg);
            Assert.That(sum / n, Is.EqualTo(0.0).Within(0.15));
        }
    }
}
