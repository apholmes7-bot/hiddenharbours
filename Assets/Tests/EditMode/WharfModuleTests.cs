using System.Linq;
using HiddenHarbours.App.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WharfModuleTests
    {
        [Test]
        public void ReversingARunSwapsOnlyItsOuterCaps()
        {
            Assert.That(Enumerable.Range(0, 4).Select(i => WharfModules.Key("logCrib", i, 4)),
                Is.EqualTo(new[] { "logCribStart", "logCribMiddle", "logCribMiddle", "logCribEnd" }));
            Assert.That(Enumerable.Range(0, 4).Select(i => WharfModules.Key("logCrib", i, 4, false)),
                Is.EqualTo(new[] { "logCribEnd", "logCribMiddle", "logCribMiddle", "logCribStart" }));
            Assert.That(WharfModules.Key("logCrib", 0, 1), Is.EqualTo("logCrib"));
        }

        [Test]
        public void NorthSouthSocketsUseTheDrawnLength()
        {
            // Independent camera value: the original gap was 9.6 - 9.6*sin(40 degrees).
            Assert.That(WharfModules.Step(9.6f, 2).x, Is.EqualTo(0f).Within(1e-5));
            Assert.That(WharfModules.Step(9.6f, 2).y, Is.EqualTo(-6.170761f).Within(1e-5));
            Assert.That(WharfModules.Step(9.6f, 6).y, Is.EqualTo(6.170761f).Within(1e-5));
            Assert.That(WharfModules.Step(9.6f, 0).x, Is.EqualTo(9.6f).Within(1e-5));
        }

        [Test]
        public void FloatBuilderUsesLevelModulesAndRetainsSixMetrePitch()
        {
            var run = NineMileCreekWharf.FloatCourses();
            Assert.That(run.First().Key, Is.EqualTo("timberFloatStart"));
            Assert.That(run.Last().Key, Is.EqualTo("timberFloatEnd"));
            Assert.That(run.Skip(1).Take(run.Count - 2).All(c => c.Key == "timberFloatMiddle"), Is.True);
            for (int i = 1; i < run.Count; i++)
                Assert.That(Vector2.Distance(run[i].Position, run[i - 1].Position), Is.EqualTo(6f).Within(1e-5));
        }

        [Test]
        public void EveryQuayRunHasExactlyTwoOuterCaps()
        {
            foreach (var run in NineMileCreekDressing.FacePieces().GroupBy(p => p.Wall))
            {
                Assert.That(run.Count(p => p.Key.EndsWith("Start")), Is.EqualTo(1), run.Key);
                Assert.That(run.Count(p => p.Key.EndsWith("End")), Is.EqualTo(1), run.Key);
                Assert.That(run.Skip(1).Take(run.Count() - 2).All(p => p.Key.EndsWith("Middle")), Is.True, run.Key);
            }
        }
    }
}
