using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE POUR VERB — <c>docs/design/fuel-and-refuelling.md</c> §9.4, and its acceptance sheet §9.12.
    ///
    /// <para>POUR is the verb that makes the geography work: St Peters has no pump, so at home a can over
    /// Ginny's counter is the ONLY way fuel reaches a boat. It moves fuel, it charges nothing, and it
    /// refuses in words.</para>
    ///
    /// <para><b>The load-bearing property is CONSERVATION</b>, and it is tested against a source that
    /// deliberately gives less than it is asked for — because the shipped cans genuinely can. Their level
    /// is a fill FRACTION rendered to a baked frame, not a litre count, so "I asked for 5 L" and "5 L left
    /// the can" are different questions. Answer the first and fuel gets created one pour at a time.</para>
    /// </summary>
    public class FuelTransferTests
    {
        // ---- the refusals, in the order they must be checked ------------------------------------

        [Test]
        public void EmptyHands_RefuseWithNoSource()
        {
            Assert.AreEqual(PourRefusal.NoSource, FuelTransfer.Quote(null, new FakeVessel("gas", 10f, 0f)).Refusal);
        }

        [Test]
        public void AHullWithNoTank_RefusesWithNoDestination()
        {
            // FuelCapacityLitres 0 is "no tank at all", not "an empty tank" — the rowed dory, and every
            // hull not yet authored. Pouring into her must refuse, not silently succeed at nothing.
            Assert.AreEqual(PourRefusal.NoDestination,
                FuelTransfer.Quote(new FakeVessel("gas", 20f, 20f), new FakeVessel("gas", 0f, 0f)).Refusal);
            Assert.AreEqual(PourRefusal.NoDestination,
                FuelTransfer.Quote(new FakeVessel("gas", 20f, 20f), null).Refusal);
        }

        [Test]
        public void AGasCan_WillNotPourIntoADieselBoat()
        {
            // The grade check is a fact about the OBJECT: a can's grade is its identity, because there is
            // nowhere to record that a gas can currently has diesel in it.
            var quote = FuelTransfer.Quote(new FakeVessel("gas", 20f, 20f), new FakeVessel("diesel", 85f, 0f));
            Assert.AreEqual(PourRefusal.GradeMismatch, quote.Refusal);
            Assert.AreEqual(0f, quote.Litres);
        }

        [Test]
        public void TheGradeCheck_ComesBeforeEmptyAndBeforeFull()
        {
            // ⭐ Standing over the filler with the wrong can, the useful sentence is "she doesn't take
            // gas", not "the can's empty" — you would go and fill the wrong can and come back. Same
            // precedence FuelPricing uses at the pump, for the same reason.
            Assert.AreEqual(PourRefusal.GradeMismatch,
                FuelTransfer.Quote(new FakeVessel("gas", 20f, 0f), new FakeVessel("diesel", 85f, 0f)).Refusal,
                "an EMPTY wrong-grade can must still say 'wrong grade'");

            Assert.AreEqual(PourRefusal.GradeMismatch,
                FuelTransfer.Quote(new FakeVessel("gas", 20f, 20f), new FakeVessel("diesel", 85f, 85f)).Refusal,
                "a FULL destination must still say 'wrong grade' to the wrong can");
        }

        [Test]
        public void AnEmptyCan_AndABrimFullTank_EachRefuseInTheirOwnWords()
        {
            Assert.AreEqual(PourRefusal.SourceEmpty,
                FuelTransfer.Quote(new FakeVessel("gas", 20f, 0f), new FakeVessel("gas", 10f, 0f)).Refusal);
            Assert.AreEqual(PourRefusal.DestinationFull,
                FuelTransfer.Quote(new FakeVessel("gas", 20f, 20f), new FakeVessel("gas", 10f, 10f)).Refusal);
        }

        [Test]
        public void EveryRefusal_HasWords_AndSuccessHasNone()
        {
            foreach (PourRefusal r in System.Enum.GetValues(typeof(PourRefusal)))
            {
                string words = FuelTransfer.Message(r, FuelGrades.Diesel);
                if (r == PourRefusal.None) Assert.IsEmpty(words, "success says nothing");
                else Assert.IsNotEmpty(words, $"{r} must earn a sentence (P5: a refusal is never silent)");
            }
            StringAssert.Contains("diesel", FuelTransfer.Message(PourRefusal.GradeMismatch, FuelGrades.Diesel),
                "the mismatch sentence must NAME the fuel — that is the entire warning");
        }

        // ---- what a pour actually moves ---------------------------------------------------------

        [Test]
        public void APour_EmptiesTheCanIntoHerTank()
        {
            var can = new FakeVessel("gas", 20f, 20f);
            var tank = new FakeVessel("gas", 45f, 5f);

            float moved = FuelTransfer.Pour(can, tank);

            Assert.AreEqual(20f, moved, 1e-4f, "the whole can goes in — there is room for it");
            Assert.AreEqual(0f, can.Litres, 1e-4f, "and the can is empty afterwards");
            Assert.AreEqual(25f, tank.Litres, 1e-4f);
        }

        [Test]
        public void APour_StopsAtTheBrim_AndLeavesTheRestInTheCan()
        {
            var can = new FakeVessel("gas", 20f, 20f);
            var tank = new FakeVessel("gas", 10f, 4f);       // the dory: 10 L, 6 L of room

            float moved = FuelTransfer.Pour(can, tank);

            Assert.AreEqual(6f, moved, 1e-4f);
            Assert.AreEqual(10f, tank.Litres, 1e-4f, "brim-full, never over");
            Assert.AreEqual(14f, can.Litres, 1e-4f, "and what would not fit is still in the can");
        }

        [Test]
        public void AskingForLess_MovesLess()
        {
            var can = new FakeVessel("gas", 20f, 20f);
            var tank = new FakeVessel("gas", 45f, 0f);

            Assert.AreEqual(5f, FuelTransfer.Pour(can, tank, 5f), 1e-4f);
            Assert.AreEqual(15f, can.Litres, 1e-4f);
            Assert.AreEqual(5f, tank.Litres, 1e-4f);
        }

        [Test]
        public void ARefusedPour_MovesNothingAtAll()
        {
            var can = new FakeVessel("gas", 20f, 20f);
            var tank = new FakeVessel("diesel", 85f, 10f);

            Assert.AreEqual(0f, FuelTransfer.Pour(can, tank));
            Assert.AreEqual(20f, can.Litres, 1e-4f, "the can is untouched");
            Assert.AreEqual(10f, tank.Litres, 1e-4f, "and so is the tank");
            Assert.AreEqual(0, can.DrawCalls, "a refused pour must not even ASK the can for fuel");
        }

        // ---- conservation, which is the whole point of Draw returning ---------------------------

        [Test]
        public void WhatArrives_IsWhatTheCanActuallyGave_NotWhatWasQuoted()
        {
            // ⭐⭐ THE REASON IFuelVessel.Draw RETURNS A VALUE. This source hands over 10% less than it is
            // asked for — which is a caricature of what the shipped cans really do (their level is a fill
            // FRACTION rounded to a drawn frame, so a few thousandths go astray). Deliver the QUOTE
            // instead of the DRAW and every pour in the game mints fuel out of nothing.
            var lossy = new FakeVessel("gas", 20f, 20f) { GiveFraction = 0.9f };
            var tank = new FakeVessel("gas", 45f, 0f);

            float moved = FuelTransfer.Pour(lossy, tank, 10f);

            Assert.AreEqual(9f, moved, 1e-4f, "the pour reports what really moved");
            Assert.AreEqual(9f, tank.Litres, 1e-4f, "the tank got what the can gave, not what was asked");
            Assert.AreEqual(11f, lossy.Litres, 1e-4f);
            Assert.AreEqual(lossy.TotalGiven, tank.Litres, 1e-4f, "⭐ conserved: out of one, into the other");
        }

        [Test]
        public void ACanThatGivesNothing_DeliversNothing()
        {
            // The degenerate end of the same rule: a source that answers 0 must not top the tank up by
            // the quoted amount. Guards the "if (drawn <= MinLitres) return 0" branch.
            var stuck = new FakeVessel("gas", 20f, 20f) { GiveFraction = 0f };
            var tank = new FakeVessel("gas", 45f, 3f);

            Assert.AreEqual(0f, FuelTransfer.Pour(stuck, tank));
            Assert.AreEqual(3f, tank.Litres, 1e-4f, "nothing was created");
        }

        [Test]
        public void PouringBackAndForth_NeverCreatesOrDestroysFuel()
        {
            var a = new FakeVessel("gas", 20f, 17f);
            var b = new FakeVessel("gas", 20f, 2f);
            float total = a.Litres + b.Litres;

            for (int i = 0; i < 12; i++)
            {
                FuelTransfer.Pour(a, b, 3f);
                FuelTransfer.Pour(b, a, 5f);
            }

            Assert.AreEqual(total, a.Litres + b.Litres, 1e-3f,
                "24 transfers later there must be exactly as much fuel in the world as there was");
        }

        // ---- the real shipped can, not a fake ---------------------------------------------------

        [Test]
        public void TheShippedCan_ImplementsDraw_AndRoundTrips()
        {
            // The join between the art lane's baked container Defs and this seam. FuelLevelPresenter
            // stores a FRACTION, so this is where a litres-vs-fraction slip would actually show up.
            var go = new GameObject("can");
            try
            {
                go.AddComponent<SpriteRenderer>();
                var can = go.AddComponent<FuelLevelPresenter>();
                var def = ScriptableObject.CreateInstance<FuelContainerDef>();
                def.Grade = FuelGrades.Gas;
                def.CapacityLitres = 20f;
                can.Container = def;
                can.Fill = 1f;

                try
                {
                    Assert.AreEqual(20f, can.Litres, 1e-3f, "brim-full is its capacity in litres");

                    Assert.AreEqual(5f, can.Draw(5f), 1e-3f, "it gives what it has");
                    Assert.AreEqual(15f, can.Litres, 1e-3f);

                    Assert.AreEqual(15f, can.Draw(50f), 1e-3f,
                        "asked for more than it holds, it gives what it HAS and says so");
                    Assert.AreEqual(0f, can.Litres, 1e-3f, "and is empty, never negative");

                    Assert.AreEqual(0f, can.Draw(5f), "an empty can gives nothing");
                    Assert.AreEqual(0f, can.Draw(-1f), "and a nonsense request moves nothing either way");
                    Assert.AreEqual(0f, can.Litres, 1e-3f);
                }
                finally { Object.DestroyImmediate(def); }
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- a stand-in vessel ------------------------------------------------------------------

        /// <summary>
        /// A vessel with a plain litre count, so a test can state a level exactly. <see cref="GiveFraction"/>
        /// makes it hand over less than asked, which is how the conservation rule above is proved.
        /// </summary>
        private sealed class FakeVessel : IFuelVessel
        {
            public FakeVessel(string grade, float capacity, float litres)
            {
                Grade = grade; CapacityLitres = capacity; Litres = litres;
            }

            public string Grade { get; }
            public float CapacityLitres { get; }
            public float Litres { get; private set; }

            /// <summary>How much of a request this vessel actually honours. 1 = all of it.</summary>
            public float GiveFraction { get; set; } = 1f;

            public int DrawCalls { get; private set; }
            public float TotalGiven { get; private set; }

            public void Deliver(float litres)
            {
                if (litres <= 0f) return;
                Litres = Mathf.Min(CapacityLitres, Litres + litres);
            }

            public float Draw(float litres)
            {
                DrawCalls++;
                if (litres <= 0f) return 0f;
                float given = Mathf.Min(Litres, litres * GiveFraction);
                Litres -= given;
                TotalGiven += given;
                return given;
            }
        }
    }
}
