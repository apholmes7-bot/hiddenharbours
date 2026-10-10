#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using HiddenHarbours.App.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    public class NineMileCreekDrawnBerthPlayTests : NineMileCreekBerthProof
    {
        [UnityTest]
        public IEnumerator TheDrawnWallHullsLeaveAFenderBetweenEveryNeighbour()
        {
            TideOnly();
            foreach (double time in Tides)
            {
                yield return Seek(time);
                var spans = Wall.Select(b => new {Boat=b, Span=DrawnSpan(b)}).OrderBy(x => x.Span.x).ToArray();
                for (int i = 1; i < spans.Length; i++)
                {
                    float gap = spans[i].Span.x - spans[i-1].Span.y;
                    Assert.That(gap, Is.GreaterThanOrEqualTo(NineMileCreekMainland.BerthFenderMetres),
                        $"Drawn hulls {spans[i-1].Boat.Owner.Id} and {spans[i].Boat.Owner.Id}: gap {gap} m at t={time}; a fender needs {NineMileCreekMainland.BerthFenderMetres} m.");
                }
            }
        }
    }
}

#endif
