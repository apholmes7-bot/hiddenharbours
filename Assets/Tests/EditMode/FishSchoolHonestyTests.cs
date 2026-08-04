using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE HONESTY INVARIANT (ADR 0025 S3, the owner's headline requirement) — <b>whatever the glass
    /// shows must be the same object that changes the fishing.</b> If the marks and the bite can ever
    /// disagree the instrument is a liar and the feature is worse than not having it.
    ///
    /// <para>So this class tests neither the fish finder nor the catch resolver. It tests that the two are
    /// reading ONE record: it takes the marks the glass would draw
    /// (<see cref="IFishSchools.MarksAt"/>), takes the bite the fishing path would get
    /// (<see cref="SchoolInfluence.At"/> over <see cref="IFishSchools.SchoolsAt"/>), and asserts the
    /// arithmetic that ties them together — <c>Σ (marks drawn × how solid the return is)</c> IS the
    /// bite-rate elevation, with nothing in between that could be tuned apart.</para>
    ///
    /// <para><b>The two sabotage arms are the point of the class.</b> An honesty assertion that cannot
    /// fail proves nothing — this repo has shipped exactly that mistake, and the tell was a guard whose
    /// own self-check reported "SABOTAGE NOT DETECTED". So <see cref="ALyingSonar_IsCaught"/> and
    /// <see cref="ASonarThatInflatesItsMarks_IsCaught"/> decouple the glass from the fish deliberately and
    /// require the check to THROW. If a future refactor lets the picture and the bite drift apart again,
    /// those two go red first and name the reason.</para>
    ///
    /// <para><b>The fixture hunts an ISOLATED school</b> (<see cref="FindIsolatedSchool"/>) using the same
    /// public maths the model does, rather than asking the model where its fish are. That is what lets
    /// "outside the radius" and "after the window closed" be real negative cases: the test knows a school
    /// is there, knows exactly where its rim and its last second are, and knows no neighbouring school is
    /// quietly covering for it.</para>
    /// </summary>
    public class FishSchoolHonestyTests
    {
        private const int Seed = 90210;
        private const string Region = "region.st_peters";
        private const double SecondsPerHour = 75.0;    // GameConfig.SecondsPerDay 1800 / 24
        private const float Column = 18f;              // the fake world's uniform water column

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown() => GameServices.Reset();

        // ---- the fixture ------------------------------------------------------------------------------

        private static FishSchoolSettings Settings()
        {
            FishSchoolSettings s = FishSchoolSettings.Default;
            s.BaseAppearanceChance01 = 1f;      // every cell holds a school: the GEOMETRY is what's under test
            s.MinSpecies = 1;
            s.MaxSpecies = 1;                   // one stated species makes the roll assertion exact
            return FishSchoolMath.Sanitized(in s);
        }

        private static FishSpeciesDef Fish(string id)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id;
            f.RegionIds = new[] { Region };
            f.AllowedGear = Gear.Handline | Gear.Jig;
            f.Seasons = SeasonMask.AllYear;
            f.DepthBands = FishDepthBand.None;   // depth-neutral: the band filter never excludes here
            f.SpawnWeight = 1f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 2f;
            return f;
        }

        private static FishSchoolModel Model(out FishSchoolSettings s, out List<FishSpeciesDef> pool)
        {
            s = Settings();
            pool = new List<FishSpeciesDef> { Fish("fish.atlantic_cod"), Fish("fish.mackerel") };
            return new FishSchoolModel(new FakeWorld(), pool, in s, DepthDropSettings.Default, SecondsPerHour);
        }

        /// <summary>The school one cell/slot holds, rebuilt from the PUBLIC maths — the test's own
        /// independent answer, never asked of the model it is judging.</summary>
        private static bool SchoolIn(int cellX, int cellY, long slot, in FishSchoolSettings s,
                                     out FishSchool school)
        {
            school = default;
            uint key = FishSchoolMath.CellSlotKey(Seed, Region, cellX, cellY, slot);
            float chance = FishSchoolMath.AppearanceChance01(Column, 0f, Season.HighSummer, in s);
            if (!FishSchoolMath.IsPresent(key, chance)) return false;

            FishSchoolMath.WindowFor(key, slot, SecondsPerHour, in s, out double a, out double b);
            school = new FishSchool(FishSchoolMath.CentreFor(key, cellX, cellY, s.CellSizeMetres),
                                    FishSchoolMath.RadiusFor(key, in s),
                                    FishSchoolMath.DepthFor(key, Column, 0f, in s),
                                    FishSchoolMath.MarkCountFor(key, in s),
                                    null, a, b);
            return true;
        }

        /// <summary>Is any school anywhere near <paramref name="pos"/> covering it at
        /// <paramref name="t"/>? Scans a 5×5 cell block, comfortably wider than the one-cell reach a
        /// radius is capped to.</summary>
        private static bool AnySchoolCovers(Vector2 pos, double t, long slot, in FishSchoolSettings s,
                                            int skipCellX, int skipCellY)
        {
            for (int cy = -2; cy <= 2; cy++)
            for (int cx = -2; cx <= 2; cx++)
            {
                if (cx == skipCellX && cy == skipCellY) continue;
                if (!SchoolIn(cx, cy, slot, in s, out FishSchool other)) continue;
                if (other.Contains(pos) && other.IsActiveAt(t)) return true;
            }
            return false;
        }

        /// <summary>
        /// Find a school in cell (0,0) that stands ALONE for the assertions: nothing else covers its
        /// centre while it is up, nothing covers its centre a second after it closes (and that second is
        /// still inside the slot, so only this slot has to be reasoned about), and nothing covers a point
        /// well outside its rim. Without this, "no marks here" could be true for the wrong reason — or
        /// false because a neighbour's shoal happened to overlap.
        /// </summary>
        private static void FindIsolatedSchool(in FishSchoolSettings s, out FishSchool school,
                                               out double midWindow, out double justAfter, out Vector2 offRim)
        {
            double period = FishSchoolMath.SlotSeconds(in s, SecondsPerHour);
            for (long slot = 0; slot < 600; slot++)
            {
                if (!SchoolIn(0, 0, slot, in s, out FishSchool candidate)) continue;

                double mid = (candidate.StartSeconds + candidate.EndSeconds) * 0.5;
                double after = candidate.EndSeconds + 1.0;
                if (after >= FishSchoolMath.SlotStartSeconds(slot, period) + period) continue;

                Vector2 off = candidate.Centre + new Vector2(candidate.RadiusMetres * 1.5f, 0f);
                if (AnySchoolCovers(candidate.Centre, mid, slot, in s, 0, 0)) continue;
                if (AnySchoolCovers(candidate.Centre, after, slot, in s, 0, 0)) continue;
                if (AnySchoolCovers(off, mid, slot, in s, 0, 0)) continue;
                if (candidate.Contains(off)) continue;      // paranoia: the offset really is outside

                school = candidate;
                midWindow = mid;
                justAfter = after;
                offRim = off;
                return;
            }

            Assert.Fail("no isolated school in 600 slots — the fixture, not the sim, needs re-picking");
            school = default; midWindow = 0; justAfter = 0; offRim = default;
        }

        // ---- the check itself -------------------------------------------------------------------------

        /// <summary>
        /// THE PREDICATE. The glass and the bite are read from <paramref name="model"/> at the same place
        /// and the same instant, and the numbers must reconcile exactly. Both the positive tests and the
        /// two sabotage arms run through this one method, so its correctness is what both directions rest
        /// on.
        /// </summary>
        private static void AssertGlassAndBiteAgree(IFishSchools model, Vector2 pos, double t,
                                                    in FishSchoolSettings s)
        {
            var marks = new List<FishMark>();
            int drawnCount = model.MarksAt(pos, t, marks);

            var schools = new List<FishSchool>();
            var species = new List<string>();
            SchoolInfluence bite = SchoolInfluence.At(model, pos, t, CatchContext.NoDepth,
                                                      DepthDropSettings.Default, in s, schools, species);

            // What the PICTURE is worth, computed only from what the glass was handed. A cast with no
            // depth game scales every school by the same DepthlessMatch01, so it factors straight out.
            float onScreen = 0f;
            for (int i = 0; i < drawnCount; i++) onScreen += marks[i].Count * marks[i].Strength;
            onScreen *= Mathf.Clamp01(s.DepthlessMatch01);

            Assert.AreEqual(onScreen, bite.EffectiveMarks, 1e-3f,
                "the marks on the glass ARE the bite rate — a difference here is the instrument lying");
            Assert.AreEqual(FishSchoolMath.BiteRateMultiplier(onScreen, in s), bite.BiteRateMultiplier, 1e-4f,
                "and the density→bite-rate map is the one the settings state, not a second table");
            Assert.AreEqual(onScreen > 0f, bite.OnFish,
                "fish on the glass ⇔ fish in the water (a mark exactly on the rim is worth nothing to both)");
        }

        // ---- the positive arm --------------------------------------------------------------------------

        [Test]
        public void OnTheSchool_TheGlassAndTheBiteAgree_AndTheBiteIsQuicker()
        {
            FishSchoolModel model = Model(out FishSchoolSettings s, out _);
            FindIsolatedSchool(in s, out FishSchool school, out double t, out _, out _);

            AssertGlassAndBiteAgree(model, school.Centre, t, in s);

            var marks = new List<FishMark>();
            Assert.AreEqual(1, model.MarksAt(school.Centre, t, marks), "the one isolated school is showing");
            Assert.AreEqual(school.MarkCount, marks[0].Count);
            Assert.AreEqual(school.DepthMetres, marks[0].DepthMetres, 1e-4f, "in METRES, never normalised");

            var schools = new List<FishSchool>();
            var species = new List<string>();
            SchoolInfluence bite = SchoolInfluence.At(model, school.Centre, t, CatchContext.NoDepth,
                                                     DepthDropSettings.Default, in s, schools, species);
            Assert.Greater(bite.BiteRateMultiplier, 1f, "sitting on fish must fish faster");
            Assert.Less(bite.BiteDelayScale, 1f, "a school can only ever shorten the wait");
        }

        [Test]
        public void MarksAreSchoolsPutThroughToMark_NeverASecondQuery()
        {
            FishSchoolModel model = Model(out FishSchoolSettings s, out _);
            FindIsolatedSchool(in s, out FishSchool school, out double t, out _, out _);

            var schools = new List<FishSchool>();
            var marks = new List<FishMark>();
            int a = model.SchoolsAt(school.Centre, t, schools);
            int b = model.MarksAt(school.Centre, t, marks);

            Assert.AreEqual(a, b, "one query, two views");
            for (int i = 0; i < a; i++)
            {
                FishMark expected = schools[i].ToMark(school.Centre);
                Assert.AreEqual(expected.DepthMetres, marks[i].DepthMetres, 0f);
                Assert.AreEqual(expected.Count, marks[i].Count);
                Assert.AreEqual(expected.Strength, marks[i].Strength, 0f);
            }
        }

        [Test]
        public void TheSpeciesOnTheGlassAreTheSpeciesThatBite()
        {
            FishSchoolModel model = Model(out FishSchoolSettings s, out List<FishSpeciesDef> pool);
            FindIsolatedSchool(in s, out FishSchool school, out double t, out _, out _);

            var schools = new List<FishSchool>();
            var species = new List<string>();
            SchoolInfluence bite = SchoolInfluence.At(model, school.Centre, t, CatchContext.NoDepth,
                                                     DepthDropSettings.Default, in s, schools, species);

            Assert.AreEqual(1, schools.Count);
            Assert.IsNotNull(bite.SpeciesIds);
            Assert.IsNotEmpty(bite.SpeciesIds, "a school over an authored pool always states what it holds");
            for (int i = 0; i < bite.SpeciesIds.Count; i++)
            {
                Assert.IsTrue(schools[0].HasSpecies(bite.SpeciesIds[i]),
                    "the roll's species come from the school on the glass, not from anywhere else");

                bool inPool = false;
                for (int k = 0; k < pool.Count; k++) inPool |= pool[k].Id == bite.SpeciesIds[i];
                Assert.IsTrue(inPool,
                    $"{bite.SpeciesIds[i]} is not in the region's pool — a school can only hold fish that " +
                    "could actually bite here");
            }
        }

        [Test]
        public void TheSchoolWeightsTheRoll_ButNeverBarsTheOtherFish()
        {
            FishSchoolModel model = Model(out FishSchoolSettings s, out List<FishSpeciesDef> pool);
            FindIsolatedSchool(in s, out FishSchool school, out double t, out _, out _);

            var schools = new List<FishSchool>();
            var species = new List<string>();
            SchoolInfluence bite = SchoolInfluence.At(model, school.Centre, t, CatchContext.NoDepth,
                                                     DepthDropSettings.Default, in s, schools, species);
            Assert.AreEqual(1, bite.SpeciesIds.Count, "the fixture states exactly one species");
            string held = bite.SpeciesIds[0];

            var onFish = new CatchContext(Region, 0f, 12f, Season.HighSummer, Gear.Handline,
                                          CatchContext.NoDepth, 0f, LureTag.None, null, bite);

            int inSchool = 0, other = 0;
            var rng = new System.Random(7);
            for (int i = 0; i < 3000; i++)
            {
                FishSpeciesDef f = CatchResolver.Resolve(pool, in onFish, DepthDropSettings.Default, rng);
                if (f == null) continue;
                if (f.Id == held) inSchool++; else other++;
            }

            Assert.Greater(inSchool, other * 2,
                "a school of one species must dominate the roll — that IS 'the marks mean something'");
            Assert.Greater(other, 0,
                "but never bar the other fish: a weight, not a wall (the module's standing promise)");

            // And with no school under the rod the roll is EXACTLY the pre-school one — not statistically
            // close, identically un-weighted, which is what makes the off switch and every legacy path safe.
            var plain = new CatchContext(Region, 0f, 12f, Season.HighSummer, Gear.Handline);
            for (int k = 0; k < pool.Count; k++)
                Assert.AreEqual(1f, plain.School.AffinityFor(pool[k].Id), 0f,
                    "a context built the old way carries SchoolInfluence.None and weights nothing");
        }

        // ---- the two cases that let the check FAIL ------------------------------------------------------

        [Test]
        public void OutsideTheRadius_TheGlassIsEmptyAndSoIsTheBite()
        {
            FishSchoolModel model = Model(out FishSchoolSettings s, out _);
            FindIsolatedSchool(in s, out FishSchool school, out double t, out _, out Vector2 offRim);

            // The school EXISTS and its window is open — we are simply not on it. If the marks were drawn
            // from anything but the school's own area test, this is where it would light up.
            AssertGlassAndBiteAgree(model, offRim, t, in s);

            var marks = new List<FishMark>();
            Assert.AreEqual(0, model.MarksAt(offRim, t, marks), "no marks off the school");

            var schools = new List<FishSchool>();
            var species = new List<string>();
            SchoolInfluence bite = SchoolInfluence.At(model, offRim, t, CatchContext.NoDepth,
                                                     DepthDropSettings.Default, in s, schools, species);
            Assert.IsFalse(bite.OnFish);
            Assert.AreEqual(1f, bite.BiteRateMultiplier, 1e-6f, "and the wait is exactly the un-schooled one");
            Assert.AreEqual(1f, bite.AffinityFor("fish.atlantic_cod"), 1e-6f, "and the roll is un-weighted");

            // Sanity: the school really is up at this instant, so the emptiness is about WHERE we are.
            Assert.IsTrue(school.IsActiveAt(t));
            Assert.GreaterOrEqual(model.MarksAt(school.Centre, t, marks), 1);
        }

        [Test]
        public void AfterTheWindowCloses_TheGlassIsEmptyAndSoIsTheBite()
        {
            FishSchoolModel model = Model(out FishSchoolSettings s, out _);
            FindIsolatedSchool(in s, out FishSchool school, out double t, out double after, out _);

            // Standing exactly where the fish were, a second after they moved on.
            AssertGlassAndBiteAgree(model, school.Centre, after, in s);

            var marks = new List<FishMark>();
            Assert.AreEqual(0, model.MarksAt(school.Centre, after, marks),
                "the window is half-open [start, end) — the fish are gone");

            var schools = new List<FishSchool>();
            var species = new List<string>();
            SchoolInfluence bite = SchoolInfluence.At(model, school.Centre, after, CatchContext.NoDepth,
                                                     DepthDropSettings.Default, in s, schools, species);
            Assert.IsFalse(bite.OnFish);
            Assert.AreEqual(1f, bite.BiteRateMultiplier, 1e-6f);

            // Sanity: the same spot WAS fishing a moment ago, so the emptiness is about WHEN we are.
            Assert.GreaterOrEqual(model.MarksAt(school.Centre, t, marks), 1);
        }

        // ---- the sabotage arms: the check must be ABLE to catch a liar -----------------------------------

        [Test]
        public void ALyingSonar_IsCaught()
        {
            FishSchoolModel real = Model(out FishSchoolSettings s, out _);
            FindIsolatedSchool(in s, out FishSchool school, out double t, out _, out _);

            // The exact defect the owner's ruling forbids: a glass that paints fish nobody can catch.
            IFishSchools liar = new DecoupledSonar(real, showMarks: true, reportSchools: false);

            Assert.Throws<AssertionException>(
                () => AssertGlassAndBiteAgree(liar, school.Centre, t, in s),
                "SABOTAGE NOT DETECTED — the honesty check no longer notices marks that do not fish");
        }

        [Test]
        public void ASonarThatInflatesItsMarks_IsCaught()
        {
            FishSchoolModel real = Model(out FishSchoolSettings s, out _);
            FindIsolatedSchool(in s, out FishSchool school, out double t, out _, out _);

            // The subtler defect: the same schools, but the picture flatters them. A second "how many fish
            // to draw" number is precisely what FishSchool.MarkCount exists to prevent.
            IFishSchools flatterer = new DecoupledSonar(real, showMarks: true, reportSchools: true,
                                                        markMultiplier: 2);

            Assert.Throws<AssertionException>(
                () => AssertGlassAndBiteAgree(flatterer, school.Centre, t, in s),
                "SABOTAGE NOT DETECTED — the honesty check no longer notices an inflated picture");
        }

        // ---- fixtures -------------------------------------------------------------------------------------

        /// <summary>A world with no services: fixed weather, fixed season, a uniform column. Exactly the
        /// inputs the sim is allowed to have (<see cref="IFishSchoolWorld"/>), so a test can hold every one
        /// of them still.</summary>
        private sealed class FakeWorld : IFishSchoolWorld
        {
            public int WorldSeed => Seed;
            public string RegionId => Region;
            public float WaterColumnAt(Vector2 worldPos, double gameSeconds) => Column;
            public float SeaState01At(double gameSeconds) => 0f;
            public Season SeasonAt(double gameSeconds) => Season.HighSummer;
        }

        /// <summary>
        /// A deliberately DISHONEST <see cref="IFishSchools"/>: it can paint marks the fishing path is
        /// never told about, or inflate the count it paints. Nothing in the shipping code can be built this
        /// way — <see cref="FishSchoolModel.MarksAt"/> is <c>SchoolsAt</c> put through
        /// <see cref="FishSchool.ToMark"/> — which is the point: this exists only so the honesty check has
        /// something it is required to catch.
        /// </summary>
        private sealed class DecoupledSonar : IFishSchools
        {
            private readonly IFishSchools _inner;
            private readonly bool _showMarks;
            private readonly bool _reportSchools;
            private readonly int _markMultiplier;

            public DecoupledSonar(IFishSchools inner, bool showMarks, bool reportSchools,
                                  int markMultiplier = 1)
            {
                _inner = inner;
                _showMarks = showMarks;
                _reportSchools = reportSchools;
                _markMultiplier = markMultiplier;
            }

            public int SchoolsAt(Vector2 worldPos, double gameSeconds, List<FishSchool> into)
            {
                into?.Clear();
                return _reportSchools ? _inner.SchoolsAt(worldPos, gameSeconds, into) : 0;
            }

            public int MarksAt(Vector2 worldPos, double gameSeconds, List<FishMark> into)
            {
                into?.Clear();
                if (!_showMarks) return 0;
                int n = _inner.MarksAt(worldPos, gameSeconds, into);
                if (into != null && _markMultiplier != 1)
                    for (int i = 0; i < into.Count; i++)
                        into[i] = new FishMark(into[i].DepthMetres, into[i].Count * _markMultiplier,
                                               into[i].Strength);
                return n;
            }
        }
    }
}
