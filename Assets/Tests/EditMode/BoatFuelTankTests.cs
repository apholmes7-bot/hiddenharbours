using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE TANK — <c>docs/design/fuel-and-refuelling.md</c> §9.2 / §9.4, acceptance sheet §9.12.
    ///
    /// <para>Three things this suite is actually protecting:</para>
    /// <list type="number">
    /// <item><description><b>An un-authored hull is INERT.</b> Every hull in the game has
    /// <c>FuelCapacityLitres = 0</c> until the numbers pass, and the rowed dory keeps it forever. She must
    /// report no grade, no capacity and no level, refuse every pour, and never offer the verb — the
    /// standing rule that the playable build runs with zero hulls authored.</description></item>
    /// <item><description><b>Capacity is the HULL's and is read live.</b> <c>OwnedFleet</c> swaps the hull
    /// under a boat on a purchase, so a cached capacity leaves a dory's 10 L on a Cape Islander — and,
    /// worse the other way, a Cape Islander's 90 L sloshing in a dory.</description></item>
    /// <item><description><b>The seam is honoured.</b> The tank is an <c>IFuelVessel</c> and nothing
    /// more, so the pump's existing pricing fills it without a single economy-side special case.</description></item>
    /// </list>
    /// </summary>
    public class BoatFuelTankTests
    {
        private readonly System.Collections.Generic.List<Object> _spawned = new System.Collections.Generic.List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameServices.ActiveBoatFuel = null;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        /// <summary>A hull Def with a tank of the given size. Capacity 0 = "she has no tank".</summary>
        private BoatHullDef Hull(string id, float capacity, string grade, float burn = 20f)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.FuelCapacityLitres = capacity;
            h.FuelGrade = grade;
            h.FullThrottleLitresPerHour = capacity > 0f ? burn : 0f;
            _spawned.Add(h);
            return h;
        }

        /// <summary>A bare tank on a bare GameObject — no BoatController, because the tank must work
        /// from an explicit hull alone (that is what <c>Configure</c> is for, and what a test rig uses).</summary>
        private BoatFuelTank Tank(BoatHullDef hull)
        {
            var go = new GameObject("boat");
            _spawned.Add(go);
            var tank = go.AddComponent<BoatFuelTank>();
            tank.Configure(hull);
            return tank;
        }

        // ---- a hull with no tank is inert -------------------------------------------------------

        [Test]
        public void TheRowedDory_ReportsNoTankAtAll()
        {
            var tank = Tank(Hull("boat.dory", 0f, ""));

            Assert.IsFalse(tank.HasTank);
            Assert.AreEqual(0f, tank.CapacityLitres);
            Assert.AreEqual(0f, tank.Litres);
            Assert.IsEmpty(tank.Grade);
            Assert.AreEqual(0f, tank.Fraction01, "no tank is not the same as an empty one, but it draws as 0");
        }

        [Test]
        public void AHullWithNoTank_CannotBeFilled_OrDrawnFrom()
        {
            var tank = Tank(Hull("boat.dory", 0f, ""));

            tank.Deliver(50f);
            Assert.AreEqual(0f, tank.Litres, "fuel poured at a hull with no tank must go nowhere");
            Assert.AreEqual(0f, tank.Draw(5f), "and nothing can come back out of it");
        }

        [Test]
        public void AHullWithNoTank_NeverOffersThePourVerb()
        {
            // ⭐ Otherwise every boat in the world advertises "pour the fuel in" forever, including the
            // one boat canon guarantees has no engine.
            var tank = Tank(Hull("boat.dory", 0f, ""));
            Assert.IsFalse(tank.IsAvailable);
        }

        [Test]
        public void AGradeAuthoredOnAHullWithNoTank_IsNotReported()
        {
            // A half-authored hull (grade set, capacity still 0) must not match a pump: it would take the
            // fill and deliver it into nothing. Content validation refuses this shape too; this is the
            // runtime belt to that braces.
            var tank = Tank(Hull("boat.half_authored", 0f, FuelGrades.Gas));
            Assert.IsEmpty(tank.Grade);
            Assert.IsFalse(tank.HasTank);
        }

        // ---- the tank as a vessel ---------------------------------------------------------------

        [Test]
        public void ANewBoat_ArrivesFull()
        {
            // §9.3: that is what a sale does. A shipwright who hands you a dry boat you cannot move off
            // his wharf is a bug shaped like realism.
            var tank = Tank(Hull("boat.dory_outboard", 10f, FuelGrades.Gas));

            Assert.AreEqual(10f, tank.Litres, 1e-4f);
            Assert.AreEqual(1f, tank.Fraction01, 1e-4f);
        }

        [Test]
        public void TheTank_ReportsTheHullsGradeAndCapacity()
        {
            var tank = Tank(Hull("boat.lobster_boat", 85f, FuelGrades.Diesel));

            Assert.AreEqual(FuelGrades.Diesel, tank.Grade);
            Assert.AreEqual(85f, tank.CapacityLitres, 1e-4f);
        }

        [Test]
        public void DeliverAndDraw_ClampAtTheBrimAndAtEmpty()
        {
            var tank = Tank(Hull("boat.punt", 25f, FuelGrades.Gas));
            tank.SetLitres(10f);

            tank.Deliver(100f);
            Assert.AreEqual(25f, tank.Litres, 1e-4f, "brim-full, never over");

            Assert.AreEqual(25f, tank.Draw(1000f), 1e-4f, "she gives what she HAS and says so");
            Assert.AreEqual(0f, tank.Litres, 1e-4f, "and is empty, never negative");
            Assert.AreEqual(0f, tank.Draw(1f), "an empty tank gives nothing");
        }

        [Test]
        public void SwappingUpTheLadder_ReadsTheNewHullsCapacity()
        {
            // P2 made literal: buy the bigger boat and the tank is the bigger boat's. A cached capacity
            // would leave the dory's 10 L on a Cape Islander.
            var tank = Tank(Hull("boat.dory_outboard", 10f, FuelGrades.Gas));
            Assert.AreEqual(10f, tank.CapacityLitres, 1e-4f);

            tank.Configure(Hull("boat.cape_islander", 90f, FuelGrades.Diesel));

            Assert.AreEqual(90f, tank.CapacityLitres, 1e-4f);
            Assert.AreEqual(FuelGrades.Diesel, tank.Grade);
        }

        [Test]
        public void SwappingDOWN_CannotLeaveABigBoatsFuelInASmallBoatsTank()
        {
            // The direction that actually creates fuel if it is got wrong: 90 L of level against a 10 L
            // capacity reads as 900% of a tank, and a gauge would draw it as nine tanks of range.
            var tank = Tank(Hull("boat.cape_islander", 90f, FuelGrades.Diesel));
            Assert.AreEqual(90f, tank.Litres, 1e-4f);

            tank.Configure(Hull("boat.dory_outboard", 10f, FuelGrades.Gas));

            Assert.AreEqual(10f, tank.Litres, 1e-4f, "the level clamps to the tank she now has");
            Assert.AreEqual(1f, tank.Fraction01, 1e-4f);
        }

        [Test]
        public void SetLitres_ClampsRatherThanTrusting()
        {
            // The save loader's door. A hand-edited or stale save must not be able to state a level the
            // hull cannot hold, in either direction.
            var tank = Tank(Hull("boat.punt", 25f, FuelGrades.Gas));

            tank.SetLitres(999f);
            Assert.AreEqual(25f, tank.Litres, 1e-4f);

            tank.SetLitres(-5f);
            Assert.AreEqual(0f, tank.Litres, 1e-4f);
        }

        // ---- the POUR verb ----------------------------------------------------------------------

        [Test]
        public void AGasCan_PouredIntoAGasBoat_MovesTheFuelAndEmptiesTheCan()
        {
            var tank = Tank(Hull("boat.dory_outboard", 10f, FuelGrades.Gas));
            tank.SetLitres(2f);
            var can = new StubCan(FuelGrades.Gas, 20f, 20f);

            float moved = FuelTransfer.Pour(can, tank);

            Assert.AreEqual(8f, moved, 1e-4f, "she takes what fits");
            Assert.AreEqual(10f, tank.Litres, 1e-4f);
            Assert.AreEqual(12f, can.Litres, 1e-4f, "and the rest stays in the can");
        }

        [Test]
        public void AGasCan_WillNotPourIntoADieselBoat()
        {
            var tank = Tank(Hull("boat.lobster_boat", 85f, FuelGrades.Diesel));
            tank.SetLitres(0f);
            var can = new StubCan(FuelGrades.Gas, 20f, 20f);

            Assert.AreEqual(PourRefusal.GradeMismatch, FuelTransfer.Quote(can, tank).Refusal);
            Assert.AreEqual(0f, FuelTransfer.Pour(can, tank));
            Assert.AreEqual(0f, tank.Litres, 1e-4f);
            Assert.AreEqual(20f, can.Litres, 1e-4f);
        }

        [Test]
        public void ThePourVerb_SitsOnTheToolTargetRung_AndOnFootOrOnDeck()
        {
            // ToolTarget is "use the thing you are holding on the thing in front of you" — the rung the
            // shovel-and-clam-hole case defined. A carried can reports Held (20); at the plain Fixture
            // rung (0) the filler would lose every press to "set the can down" and pouring would be
            // IMPOSSIBLE.
            var tank = Tank(Hull("boat.dory_outboard", 10f, FuelGrades.Gas));

            Assert.AreEqual(InteractPriority.ToolTarget, tank.Priority);
            Assert.IsTrue((tank.Contexts & InteractContext.OnDeck) != 0, "standing on her own deck");
            Assert.IsTrue((tank.Contexts & InteractContext.OnFoot) != 0, "or on the wharf beside her");
            Assert.IsTrue((tank.Contexts & InteractContext.AtHelm) == 0,
                "NOT at the helm: a press there means 'step back onto the deck' and the switcher " +
                "answers it before any verb is consulted, so the offer could never resolve");
        }

        [Test]
        public void ThePourVerb_NamesTheFuel()
        {
            var tank = Tank(Hull("boat.lobster_boat", 85f, FuelGrades.Diesel));
            StringAssert.Contains("diesel", tank.VerbLabel,
                "the grade is the offer and the warning both — see the pump's 'Fill up with gas'");
        }

        [Test]
        public void TheTankHasAStableId_ThatNamesItsHull()
        {
            var tank = Tank(Hull("boat.dory_outboard", 10f, FuelGrades.Gas));
            StringAssert.Contains("boat.dory_outboard", tank.Id);
            Assert.AreEqual(tank.Id, tank.Id, "resolved once and cached — the resolver reads it per press");
        }

        // ---- a stand-in can, so this suite does not drag the art module in ----------------------

        private sealed class StubCan : IFuelVessel
        {
            public StubCan(string grade, float capacity, float litres)
            {
                Grade = grade; CapacityLitres = capacity; Litres = litres;
            }

            public string Grade { get; }
            public float CapacityLitres { get; }
            public float Litres { get; private set; }

            public void Deliver(float litres)
            {
                if (litres > 0f) Litres = Mathf.Min(CapacityLitres, Litres + litres);
            }

            public float Draw(float litres)
            {
                if (litres <= 0f) return 0f;
                float given = Mathf.Min(Litres, litres);
                Litres -= given;
                return given;
            }
        }
    }
}
