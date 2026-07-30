using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>Who wears which fitting</b> — the half of the prop catalog that is not geometry.
    ///
    /// <para>The baker re-points every wearer of a fitting each time it bakes
    /// (<c>RigPropMeshBaker.WireVisuals</c>), which is exactly why "this boat wears that engine"
    /// cannot live only in the committed asset: wiring an asset by hand is wiring it once, and the
    /// next bake takes it off again. It lives here, and this is the guard.</para>
    ///
    /// <para>Headless — the catalog is plain C# and needs neither a rig host nor a GPU.</para>
    /// </summary>
    public class HullPropFleetWearerTests
    {
        const string Visuals = "Assets/_Project/Data/Boats/Visuals";

        static string[] WearersOf(string key) =>
            HullPropFleet.Get(key).WornBy.Select(w => w.visualAssetPath).ToArray();

        /// <summary>
        /// <b>The dory borrows the punt's starter engine (§7.7).</b> Her own kicker rig is written
        /// (<c>docs/art/rigs/doryMotorRig.js</c>) and has never been baked, and the M1 slice closes
        /// on her wearing an outboard — so until that bake she hangs the punt's, shifted onto her own
        /// transom by <c>BoatVisualDef.MotorMeshFitmentOffsetMeters</c>.
        ///
        /// <para>WHEN THE KICKER IS BAKED: give it its own entry here, take <c>DoryIso</c> off this
        /// one, and zero her fitment offset.</para>
        /// </summary>
        [Test]
        public void ThePuntsBasicOutboard_IsWornByThePunt_AndOnLoanByTheDory()
        {
            string[] wearers = WearersOf("puntMotorBasic");

            CollectionAssert.Contains(wearers, $"{Visuals}/PuntIsoBasic.asset");
            CollectionAssert.Contains(wearers, $"{Visuals}/DoryIso.asset",
                "the dory must be listed as a wearer, or 'Bake ALL hull fittings' un-wires her " +
                "engine the next time it runs and the outboard rung silently disappears.");
            Assert.IsTrue(HullPropFleet.Get("puntMotorBasic").WornBy.All(w => w.slot == "Motor"),
                "an outboard goes in the Motor slot, whatever the boat.");
        }

        /// <summary>The loan is the BASIC engine only. The upgraded punt build is her own upgrade and
        /// has nothing to do with the dory — a second-hand kicker is not a new one.</summary>
        [Test]
        public void TheUpgradedPuntEngine_IsNotLentToAnybody()
        {
            CollectionAssert.AreEqual(new[] { $"{Visuals}/PuntIsoUpgraded.asset" },
                                      WearersOf("puntMotorUpgraded"));
        }

        /// <summary>Nothing else changed hands: one skiff bake still fits both skiff hulls, and the
        /// dory's oars are still hers alone.</summary>
        [Test]
        public void TheRestOfTheCatalogIsUnchanged()
        {
            CollectionAssert.AreEqual(new[] { $"{Visuals}/ConsoleSkiff.asset" }, WearersOf("skiffMotorWork"));
            CollectionAssert.AreEqual(
                new[] { $"{Visuals}/SportSkiffSingle.asset", $"{Visuals}/SportSkiffTwin.asset" },
                WearersOf("skiffMotorSport"));
            CollectionAssert.AreEqual(new[] { $"{Visuals}/DoryIso.asset" }, WearersOf("doryOarPort"));
        }
    }
}
