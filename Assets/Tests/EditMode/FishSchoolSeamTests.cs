using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The fish-school Core seam (ADR 0025 S3 Step 0) — the contract the gameplay slice PRODUCES against
    /// and the UI slice READS against, and the empty default that lets them be built at the same time.
    ///
    /// <para><b>The load-bearing assertion is the never-null one.</b>
    /// <see cref="GameServices.FishSchools"/> is the one service on that class that is never null: with no
    /// model registered it is <see cref="EmptyFishSchools"/>, an honest empty sea. That is what lets the
    /// finder's host compile, run and draw with no fish model in the project at all, and what lets the
    /// model be swapped in later without one line of UI changing. If it ever reads null, both slices break
    /// in a way that only shows up in a scene.</para>
    /// </summary>
    public class FishSchoolSeamTests
    {
        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown() => GameServices.Reset();

        private static readonly string[] Cod = { "fish.atlantic_cod" };

        private static FishSchool School(float x = 0f, float y = 0f, float radius = 30f,
                                         float depth = 6f, int marks = 3,
                                         double start = 100.0, double end = 200.0,
                                         IReadOnlyList<string> species = null)
            => new FishSchool(new Vector2(x, y), radius, depth, marks, species ?? Cod, start, end);

        // ---- the empty default ---------------------------------------------------------------------

        [Test]
        public void FishSchools_IsNeverNull_AndIsTheEmptySeaUntilAModelRegisters()
        {
            Assert.IsNotNull(GameServices.FishSchools,
                "the UI slice reads this WITHOUT a null check — that is the whole contract");
            Assert.AreSame(EmptyFishSchools.Instance, GameServices.FishSchools);
        }

        [Test]
        public void ClearingTheRegistration_FallsBackToTheEmptySea_RatherThanNull()
        {
            GameServices.FishSchools = new StubSchools(School());
            Assert.AreNotSame(EmptyFishSchools.Instance, GameServices.FishSchools);

            GameServices.FishSchools = null;      // what a producer does on disable
            Assert.IsNotNull(GameServices.FishSchools);
            Assert.AreSame(EmptyFishSchools.Instance, GameServices.FishSchools);
        }

        [Test]
        public void Reset_LeavesTheEmptySea_NotNull()
        {
            GameServices.FishSchools = new StubSchools(School());
            GameServices.Reset();
            Assert.AreSame(EmptyFishSchools.Instance, GameServices.FishSchools,
                "scene teardown must leave the finder drawable, not crashing");
        }

        [Test]
        public void TheEmptySea_ClearsTheCallersList_AndReportsNothing()
        {
            var schools = new List<FishSchool> { School() };
            var marks = new List<FishMark> { new FishMark(1f, 1, 1f) };

            Assert.AreEqual(0, GameServices.FishSchools.SchoolsAt(Vector2.zero, 150.0, schools));
            Assert.IsEmpty(schools, "stale contents are cleared, never left to be re-drawn");
            Assert.AreEqual(0, GameServices.FishSchools.MarksAt(Vector2.zero, 150.0, marks));
            Assert.IsEmpty(marks);
        }

        [Test]
        public void TheEmptySea_ToleratesANullList()
        {
            Assert.AreEqual(0, EmptyFishSchools.Instance.SchoolsAt(Vector2.zero, 0.0, null));
            Assert.AreEqual(0, EmptyFishSchools.Instance.MarksAt(Vector2.zero, 0.0, null));
        }

        [Test]
        public void AModel_SwapsInForTheEmptyDefault_WithNoOtherChange()
        {
            // THE parallel-build proof: this single assignment is the entire handover from the empty sea
            // to a real model. Everything the UI does is unchanged either side of it.
            var marks = new List<FishMark>();
            Assert.AreEqual(0, GameServices.FishSchools.MarksAt(Vector2.zero, 150.0, marks));

            GameServices.FishSchools = new StubSchools(School(marks: 4, depth: 7.5f));

            Assert.AreEqual(1, GameServices.FishSchools.MarksAt(Vector2.zero, 150.0, marks));
            Assert.AreEqual(7.5f, marks[0].DepthMetres, 1e-5f, "and the read is in METRES, not 0..1");
            Assert.AreEqual(4, marks[0].Count);
        }

        // ---- the school's own rules ------------------------------------------------------------------

        [Test]
        public void Contains_UsesTheSchoolsOwnRadius()
        {
            FishSchool s = School(x: 100f, y: 50f, radius: 30f);
            Assert.IsTrue(s.Contains(new Vector2(100f, 50f)), "dead centre");
            Assert.IsTrue(s.Contains(new Vector2(129f, 50f)), "just inside the rim");
            Assert.IsFalse(s.Contains(new Vector2(131f, 50f)), "just outside it");
            Assert.IsFalse(School(radius: 0f).Contains(Vector2.zero), "a zero-radius school holds nothing");
        }

        [Test]
        public void TheWindow_IsHalfOpen_SoBackToBackWindowsNeverBothCount()
        {
            FishSchool s = School(start: 100.0, end: 200.0);
            Assert.IsFalse(s.IsActiveAt(99.999));
            Assert.IsTrue(s.IsActiveAt(100.0), "inclusive at the start");
            Assert.IsTrue(s.IsActiveAt(199.999));
            Assert.IsFalse(s.IsActiveAt(200.0), "EXCLUSIVE at the end — [start, end)");
        }

        [Test]
        public void SignalAt_IsOneAtTheCentre_AndFallsToZeroAtTheRim()
        {
            FishSchool s = School(x: 0f, y: 0f, radius: 40f);
            Assert.AreEqual(1f, s.SignalAt(Vector2.zero), 1e-5f);
            Assert.AreEqual(0.5f, s.SignalAt(new Vector2(20f, 0f)), 1e-5f, "linear in distance");
            Assert.AreEqual(0f, s.SignalAt(new Vector2(40f, 0f)), 1e-5f, "zero exactly at the rim");
            Assert.AreEqual(0f, s.SignalAt(new Vector2(400f, 0f)), 0f, "and clamped, never negative");
            Assert.AreEqual(0f, School(radius: 0f).SignalAt(Vector2.zero), 0f);
        }

        [Test]
        public void HasSpecies_IsAnOrdinalIdMatch_AndSafeOnAnUnstatedSet()
        {
            FishSchool s = School(species: new[] { "fish.atlantic_cod", "fish.haddock" });
            Assert.IsTrue(s.HasSpecies("fish.haddock"));
            Assert.IsFalse(s.HasSpecies("fish.mackerel"));
            Assert.IsFalse(s.HasSpecies("Fish.Haddock"), "ids are exact — stable and case-sensitive (rule 2)");
            Assert.IsFalse(s.HasSpecies(null));

            // A genuinely UNSTATED set — built directly, because School()'s `species ?? Cod` fallback
            // would quietly substitute one and this assertion would pin nothing.
            var unstated = new FishSchool(Vector2.zero, 30f, 6f, 3, null, 100.0, 200.0);
            Assert.IsNull(unstated.SpeciesIds, "the school really does carry no species set");
            Assert.IsFalse(unstated.HasSpecies("fish.atlantic_cod"),
                "an unstated species set answers no rather than throwing");
            Assert.IsFalse(new FishSchool(Vector2.zero, 30f, 6f, 3, new string[0], 100.0, 200.0)
                               .HasSpecies("fish.atlantic_cod"),
                "and so does an empty one");
        }

        [Test]
        public void ToMark_IsTheOneDerivation_SoThePictureAndTheBiteCannotDisagree()
        {
            // The honesty invariant in a test: the mark the glass draws carries the SAME count that drives
            // the bite rate, and the same depth in the same units.
            FishSchool s = School(x: 0f, y: 0f, radius: 40f, depth: 12.25f, marks: 5);
            FishMark m = s.ToMark(new Vector2(20f, 0f));

            Assert.AreEqual(s.DepthMetres, m.DepthMetres, 0f, "metres in, metres out — never normalised");
            Assert.AreEqual(s.MarkCount, m.Count, "one number: the marks drawn ARE the expected bite rate");
            Assert.AreEqual(s.SignalAt(new Vector2(20f, 0f)), m.Strength, 0f);
        }

        /// <summary>A stand-in for the gameplay slice's model: reports its one school wherever/whenever it
        /// is in range, through exactly the seam the real one will.</summary>
        private sealed class StubSchools : IFishSchools
        {
            private readonly FishSchool _school;
            public StubSchools(FishSchool school) { _school = school; }

            public int SchoolsAt(Vector2 worldPos, double gameSeconds, List<FishSchool> into)
            {
                into?.Clear();
                if (!_school.Contains(worldPos) || !_school.IsActiveAt(gameSeconds)) return 0;
                into?.Add(_school);
                return 1;
            }

            public int MarksAt(Vector2 worldPos, double gameSeconds, List<FishMark> into)
            {
                into?.Clear();
                if (!_school.Contains(worldPos) || !_school.IsActiveAt(gameSeconds)) return 0;
                into?.Add(_school.ToMark(worldPos));
                return 1;
            }
        }
    }
}
