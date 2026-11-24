using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The chimney plume shows only while the building it rises from shows its own picture. A building
    /// interior cuts the shell away (disables its renderer) to show the rooms, and a plume drawn over the
    /// open rooms reads as smoke inside the house.
    /// </summary>
    public class ChimneySmokeTests
    {
        [Test]
        public void ThePlumeShowsWithTheBuilding_AndHidesWhileTheShellIsCutAway()
        {
            var go = new GameObject("cottage (test)");
            try
            {
                var shell = go.AddComponent<SpriteRenderer>();
                Assert.IsTrue(ChimneySmoke.PlumeShows(shell), "the plume is hidden over a standing house");
                shell.enabled = false;
                Assert.IsFalse(ChimneySmoke.PlumeShows(shell), "the plume still draws while the shell is cut away to show the rooms");
                shell.enabled = true;
                Assert.IsTrue(ChimneySmoke.PlumeShows(shell), "the plume does not come back when he steps outside");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void APlumeWithNoBuildingAboveItAlwaysShows()
        {
            Assert.IsTrue(ChimneySmoke.PlumeShows(null));
        }
    }
}
