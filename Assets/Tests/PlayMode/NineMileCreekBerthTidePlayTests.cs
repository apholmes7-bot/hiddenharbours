#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    public class NineMileCreekBerthTidePlayTests : NineMileCreekBerthProof
    {
        [UnityTest]
        public IEnumerator EveryWallHullFloatsAndRisesWithTheRealFloatAtFourTides()
        {
            TideOnly();
            yield return Seek(Tides[0]);
            float meanFloat = Float.DeckElevationNow() * IsoGround.HeightScale;
            float[] mean = Wall.Select(b => Visual(b).position.y).ToArray();
            foreach (double time in Tides)
            {
                yield return Seek(time);
                float sea = GameServices.Environment.WaterLevelAt(Clock.TotalSeconds);
                for (int i = 0; i < Wall.Length; i++)
                {
                    var ride = Wall[i].GetComponentInChildren<HullTideRide>();
                    Assert.IsNotNull(ride, Wall[i].Owner.Id + " has no tide rider");
                    Assert.IsFalse(ride.IsAgroundNow(), $"{Wall[i].Owner.Id} is grounded at t={time}: sea {sea}, bed {ride.BedElevation}, draught {ride.DraughtMetres}");
                    Assert.That(Visual(Wall[i]).position.y - mean[i],
                        Is.EqualTo(Float.DeckElevationNow() * IsoGround.HeightScale - meanFloat).Within(0.005f),
                        Wall[i].Owner.Id + " does not rise with the real NMC float at " + time);
                }
            }
        }
    }
}

#endif
