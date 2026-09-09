using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE FISH ARE SEEN WHERE HE PLAYS (owner's rulings 2026-09-05 / 2026-09-06, PR 2) — the guards
    /// for the two things that kept the schools of PR 1 out of the owner's frame, and for the
    /// per-species data that replaced the one global number.
    ///
    /// <para><b>The measured defect these pin.</b> A school's <c>RadiusMetres</c> is 22–55 m because
    /// that is how far a BOAT may be and still be on the mark; the drawer multiplied it by 0.8 and swam
    /// the shoal on a loop that wide. Against the game's own camera
    /// (<c>CameraFollow.DefaultWorldHeightMeters</c> = 14 m, 24.89 × 14 m at 16:9) only 9–26% of a
    /// school's fish were ever in the picture — sitting exactly on the mark. The spread is now a LENGTH
    /// the species states, and the whole shoal fits.</para>
    ///
    /// <para><b>What is deliberately NOT guarded here.</b> There is no "schools are inside the camera
    /// clamp" test, because <c>GameServices.CurrentRegionBounds</c> is
    /// <c>RegionAnchor.WorldBounds</c> is <c>RegionDef.WorldCenter/WorldSizeMeters</c> — the very
    /// rectangle the school lattice already scatters over. Such a test would reject nothing and pass
    /// forever; the fence that does the work is the WATER, and it is guarded below.</para>
    /// </summary>
    public class VisibleFishSeenTests
    {
        private const int Seed = 4242;
        private const string Region = "region.st_peters";
        private const double SecondsPerHour = 75.0;   // GameConfig.SecondsPerDay 1800 / 24

        // The game's own camera, from CameraFollow's constants — restated here rather than read off
        // CameraFollow so the bar cannot move when the camera does (a bar fetched from the code under
        // test is a mirror).
        private const float BoatCameraHeightM = 14f;
        private const float BoatCameraWidthM = BoatCameraHeightM * 16f / 9f;   // 24.888…

        [SetUp] public void SetUp() => GameServices.Reset();
        [TearDown] public void TearDown() => GameServices.Reset();

        // ---- fixtures ---------------------------------------------------------------------------------

        /// <summary>A world whose water column is whatever the test says it is, at whatever position —
        /// including a real seabed, so the LOCATION gate can be exercised instead of assumed.</summary>
        private sealed class FakeWorld : IFishSchoolWorld
        {
            public int Seed = VisibleFishSeenTests.Seed;
            public string Region = VisibleFishSeenTests.Region;
            public float SeaState = 0f;
            public Season Time = Season.HighSummer;

            /// <summary>Bed elevation (m above datum) at a position; the water level is 0.</summary>
            public System.Func<Vector2, float> Bed = _ => -18f;

            public int WorldSeed => Seed;
            string IFishSchoolWorld.RegionId => Region;
            public float WaterColumnAt(Vector2 worldPos, double gameSeconds)
                => Mathf.Max(0f, -Bed(worldPos));
            public float SeaState01At(double gameSeconds) => SeaState;
            public Season SeasonAt(double gameSeconds) => Time;
        }

        private static FishSchoolSettings Settings()
        {
            FishSchoolSettings s = FishSchoolSettings.Default;
            s.BaseAppearanceChance01 = 0.55f;
            return s;
        }

        private static FishSpeciesDef Species(string id, FishCategory category = FishCategory.InshoreGroundfish,
                                              float density = 0f, float spread = 0f,
                                              FishDepthBand bands = FishDepthBand.None)
        {
            var d = ScriptableObject.CreateInstance<FishSpeciesDef>();
            d.Id = id;
            d.DisplayName = id;
            d.Category = category;
            d.RegionIds = new[] { Region };
            d.Seasons = SeasonMask.AllYear;
            d.DepthBands = bands;
            d.SchoolsPerSquareKilometre = density;
            d.ShoalSpreadMetres = spread;
            return d;
        }

        private static FishSchoolModel Model(FakeWorld w, params FishSpeciesDef[] pool)
            => new FishSchoolModel(w, pool, Settings(), DepthDropSettings.Default, SecondsPerHour);

        /// <summary>Every school the region can build in one slot, over a rect wide enough to hold the
        /// whole of St Peters — the sweep the owner's "nobody sees them" complaint is really about.</summary>
        private static List<FishSchool> SweepRegion(FishSchoolModel m, double now)
        {
            var all = new List<FishSchool>();
            var buf = new List<FishSchool>();
            // 120 m cells over 760x520: walk the lattice in 60 m steps so no cell is missed, and
            // de-duplicate by centre (the view query reports a school once per overlapping read).
            var seen = new HashSet<(float, float)>();
            for (float y = -260f; y <= 260f; y += 60f)
            for (float x = -380f; x <= 380f; x += 60f)
            {
                m.SchoolsInView(new Rect(x, y, 60f, 60f), now, buf);
                foreach (FishSchool s in buf)
                    if (seen.Add((s.Centre.x, s.Centre.y))) all.Add(s);
            }
            return all;
        }

        // ---- 1. the spread is a LENGTH, and the shoal fits in his camera -------------------------------

        /// <summary>
        /// THE HEADLINE. Under the retired rule — a loop of <c>0.8 × school radius</c> — a school's fish
        /// were drawn tens of metres from the mark and mostly off screen; under the shipped per-species
        /// spread the whole shoal is inside the frame.
        ///
        /// <para><b>This is its own negative control.</b> The first two rows ARE the old rule, computed
        /// here from the shipped radius range rather than by calling anything the fix touched — so the
        /// test states the defect and the cure in the same breath, and cannot go green on a revert.</para>
        /// </summary>
        [Test]
        public void ShoalSpread_IsALength_NotAFractionOfTheBoatRadius()
        {
            // The retired rule, restated from its own two inputs — and BOTH halves are now retired:
            // the disc was 22-55 m until the owner's 2026-09-09 lattice ruling shrank it to 8-14 m
            // (a disc is how far a BOAT may be from a mark, and 22-55 m was two to four screens wide).
            // These literals are the radii the retired rule multiplied, and must stay literals: reading
            // them off today's settings would make this test measure the CURRENT disc, not the defect.
            const float retiredMinRadiusM = 22f;
            const float retiredMaxRadiusM = 55f;

            float oldTight = 0.8f * retiredMinRadiusM;      // 17.6 m
            float oldWide = 0.8f * retiredMaxRadiusM;       // 44.0 m

            float tightIn = FractionInCamera(oldTight);
            float wideIn = FractionInCamera(oldWide);
            float rigIn = FractionInCamera(FishSchoolSettings.ShoalMathReferenceSpreadMetres);
            float looseIn = FractionInCamera(4.0f);         // the loosest authored spread (striped bass)

            Assert.Less(tightIn, 0.5f,
                $"the retired rule put a TIGHT school's fish off screen ({tightIn:P0} in frame) — if this " +
                "is no longer true the old rule was not what this test claims it was");
            Assert.Less(wideIn, 0.25f,
                $"the retired rule put a WIDE school's fish off screen ({wideIn:P0} in frame)");

            Assert.AreEqual(1f, rigIn, 1e-6f,
                $"the rig's own 1.2 m shoal must draw wholly inside the boat camera; got {rigIn:P1}");
            Assert.AreEqual(1f, looseIn, 1e-6f,
                $"even the loosest authored spread (4 m) must stay in frame; got {looseIn:P1}");
        }

        /// <summary>What share of a school's drawn fish land inside the boat camera when it is parked on
        /// the school's own anchor — the most generous framing there is.</summary>
        private static float FractionInCamera(float loopRadiusMetres)
        {
            var into = new ShoalMath.Swimmer[32];
            int inside = 0, total = 0;

            for (int marks = 1; marks <= 6; marks++)
            for (int seed = 1; seed <= 12; seed++)
            for (double t = 0; t < 240; t += 3.0)
            {
                int n = ShoalMath.Fill(0.70f, marks, t, seed, loopRadiusMetres,
                                       ShoalMath.DefaultSpeedMetresPerSecond, 1f,
                                       ShoalMath.DefaultZMetres, into);
                for (int i = 0; i < n; i++)
                {
                    total++;
                    if (Mathf.Abs(into[i].X) <= BoatCameraWidthM * 0.5f &&
                        Mathf.Abs(into[i].Y) <= BoatCameraHeightM * 0.5f) inside++;
                }
            }
            return total == 0 ? 0f : (float)inside / total;
        }

        /// <summary>The Core-side reference spread is the RIG's, and Core cannot reference the Fishing
        /// module to say so — this is the pin that keeps the restated literal honest.</summary>
        [Test]
        public void ReferenceSpread_IsTheRigsOwnShoal()
            => Assert.AreEqual(ShoalMath.DefaultRadiusMetres,
                               FishSchoolSettings.ShoalMathReferenceSpreadMetres, 1e-6f,
                               "GameConfig's fallback spread must be the rig's authored shoal radius");

        // ---- 2. density is per species, and the fallback is exactly today's sea ------------------------

        /// <summary>
        /// The shipped fallback density reproduces the ONE global chance it replaces, so a species that
        /// states no density of its own keeps precisely the sea it had.
        ///
        /// <para>The bar is computed from the two shipped primitives — 0.55 and 120 m — not read back
        /// out of the code under test.</para>
        /// </summary>
        [Test]
        public void FallbackDensity_ReproducesTheOldGlobalChance()
        {
            const float shippedChance = 0.55f;      // FishSchoolSettings.Default.BaseAppearanceChance01
            const float shippedCellM = 22f;         // FishSchoolSettings.Default.CellSizeMetres

            FishSchoolSettings s = FishSchoolSettings.Default;
            Assert.AreEqual(shippedChance, s.BaseAppearanceChance01, 1e-6f, "the shipped chance moved");
            Assert.AreEqual(shippedCellM, s.CellSizeMetres, 1e-6f, "the shipped cell moved");

            float chance = FishSchoolMath.BaseChanceForDensity(
                FishSchoolSettings.ReferenceSchoolsPerSquareKilometre, shippedCellM);

            Assert.AreEqual(shippedChance, chance, 1e-3f,
                "the reference density must be the global chance restated as a density over water");
        }

        /// <summary>An unstated density is a SENTINEL, not "no fish" — the difference between "this
        /// species says nothing" (fall back) and "this species says none" (an empty sea).</summary>
        [Test]
        public void UnstatedDensity_IsASentinel_NotZero()
        {
            // A statement about the FUNCTION, at a cell size of its own: 38.19/km^2 over a 120 m cell is
            // 0.55 whatever the shipped cell happens to be today.
            Assert.Less(FishSchoolMath.BaseChanceForDensity(0f, 120f), 0f,
                "0 means unstated and must be distinguishable from a real zero chance");
            Assert.AreEqual(0.55f, FishSchoolMath.BaseChanceForDensity(38.194444f, 120f), 1e-3f);
            Assert.AreEqual(1f, FishSchoolMath.BaseChanceForDensity(100000f, 120f), 1e-6f,
                "a cell holds at most one school, so the chance is honestly capped at 1");
        }

        /// <summary>
        /// A SPECIES' OWN DENSITY IS REALISED. A pool of one species at a stated density fills about
        /// that many schools per square kilometre — and a species stated ten times denser fills about
        /// ten times as many.
        ///
        /// <para>Measured over the region sweep, not asserted from the formula, so the whole chain
        /// (density → chance → per-species roll → the school that survives) is what is being tested.</para>
        /// </summary>
        [Test]
        public void StatedDensity_IsWhatTheWaterHolds()
        {
            var w = new FakeWorld { Bed = _ => -18f };          // open water everywhere

            int sparse = CountOverSlots(Model(w, Species("fish.sparse", density: 5f)));
            int dense = CountOverSlots(Model(w, Species("fish.dense", density: 50f)));

            Assert.Greater(dense, sparse * 4,
                $"a species authored 10x denser must fill far more water: sparse={sparse} dense={dense}");
            Assert.Greater(sparse, 0, "a stated density of 5/km^2 is not an empty sea");
        }

        private static int CountOverSlots(FishSchoolModel m)
        {
            int n = 0;
            // Three slots apart, so the windows are independent draws rather than one lucky moment.
            foreach (double now in new[] { 400.0, 1200.0, 2000.0 })
                n += SweepRegion(m, now).Count;
            return n;
        }

        // ---- 3. a clam cannot swim ---------------------------------------------------------------------

        /// <summary>
        /// SHELLFISH ARE OUT OF THE SWIMMING POOL (the owner's Q1 default). Before this, a shellfish
        /// that won a cell spent that cell drawing nothing at all — the drawer has always refused to
        /// draw one — so those slots were simply blank water.
        /// </summary>
        [Test]
        public void Shellfish_NeverLeadASwimmingSchool()
        {
            var w = new FakeWorld();

            var clamsOnly = Model(w,
                Species("fish.soft_shell_clam", FishCategory.Shellfish, density: 60f),
                Species("fish.lobster", FishCategory.Shellfish, density: 60f));
            Assert.IsEmpty(SweepRegion(clamsOnly, 400.0),
                "a pool of nothing but shellfish holds no SWIMMING schools at all");

            var mixed = Model(w,
                Species("fish.soft_shell_clam", FishCategory.Shellfish, density: 200f),
                Species("fish.rock_crab", FishCategory.Shellfish, density: 200f),
                Species("fish.atlantic_cod", density: 45f));

            List<FishSchool> schools = SweepRegion(mixed, 400.0);
            Assert.IsNotEmpty(schools, "the finfish in the pool still fills the water");
            foreach (FishSchool s in schools)
            {
                Assert.IsNotNull(s.SpeciesIds);
                Assert.Greater(s.SpeciesIds.Count, 0, "a school over an authored pool states its species");
                foreach (string id in s.SpeciesIds)
                    Assert.AreNotEqual("fish.soft_shell_clam", id,
                        "a clam cannot swim — and must not be in a school the water draws");
            }
        }

        // ---- 4. a school exists only where there is WATER ------------------------------------------------

        /// <summary>
        /// THE LOCATION GATE, EXERCISED. PR 1's acceptance fixture answered a flat 18 m to every
        /// bathymetry question, so this gate — which has always been in the code — was never once
        /// measured. Here the seabed is St Peters' own shape: an island plateau at +6 m centred (70, 0)
        /// with semi-axes 120 × 70 m, and a deep harbour floor at −4 m everywhere else
        /// (<c>StPetersBuilder.IslandCenter/IslandRadius/IslandRadiusY/IslandElevation/DeepHarbourElevation</c>).
        /// Not one school may stand on the island.
        /// </summary>
        [Test]
        public void NoSchoolStandsOnTheIsland()
        {
            // St Peters' authored terrain, restated from the builder's own constants.
            var islandCentre = new Vector2(70f, 0f);
            const float rx = 120f, ry = 70f;

            var w = new FakeWorld
            {
                Bed = p =>
                {
                    float dx = (p.x - islandCentre.x) / rx, dy = (p.y - islandCentre.y) / ry;
                    return dx * dx + dy * dy <= 1f ? 6f : -4f;      // plateau, else the harbour floor
                }
            };

            var m = Model(w, Species("fish.atlantic_cod", density: 45f));
            List<FishSchool> schools = SweepRegion(m, 400.0);

            Assert.IsNotEmpty(schools, "the harbour floor still holds fish");
            foreach (FishSchool s in schools)
            {
                float dx = (s.Centre.x - islandCentre.x) / rx, dy = (s.Centre.y - islandCentre.y) / ry;
                Assert.Greater(dx * dx + dy * dy, 1f,
                    $"a school was built on dry land at {s.Centre} — the location gate is not holding");
            }
        }

        /// <summary>
        /// THE SEA IS NOT DEEPER THAN IT IS. St Peters' harbour floor is 4 m below datum and the tide
        /// swings 2.2 m, so no water anywhere is deeper than ~6 m — and the model must not claim a
        /// midwater or deep school in it. It does not, because a school's depth is a FRACTION of its
        /// own column; this pins that so nobody later "fixes" the depth into an absolute.
        /// </summary>
        [Test]
        public void NoSchoolIsDeeperThanTheWaterThatHoldsIt()
        {
            const float column = 6f;                       // the deepest water St Peters has
            var w = new FakeWorld { Bed = _ => -column };

            var m = Model(w, Species("fish.atlantic_cod", density: 45f));
            List<FishSchool> schools = SweepRegion(m, 400.0);
            Assert.IsNotEmpty(schools);

            DepthDropSettings dd = DepthDropSettings.Default;
            foreach (FishSchool s in schools)
            {
                Assert.LessOrEqual(s.DepthMetres, column,
                    $"a school at {s.DepthMetres:F1} m in {column} m of water");
                Assert.LessOrEqual(s.DepthMetres, dd.InshoreMaxMeters,
                    "6 m of water cannot hold a midwater or deep school — the bands above Inshore are " +
                    "unreachable here and the model must not pretend otherwise");
            }
        }

        // ---- 5. still deterministic, still the same school both readers see -----------------------------

        /// <summary>Rule 5: the same (seed, time, place) builds the same schools, species and all — the
        /// reorder that put the water and the species before the presence draw must not have leaked any
        /// order-dependence into the result.</summary>
        [Test]
        public void SchoolsAreDeterministic_AcrossTheReorder()
        {
            var w = new FakeWorld();
            var a = SweepRegion(Model(w, Species("fish.atlantic_cod", density: 45f),
                                         Species("fish.mackerel", density: 55f)), 900.0);
            var b = SweepRegion(Model(w, Species("fish.atlantic_cod", density: 45f),
                                         Species("fish.mackerel", density: 55f)), 900.0);

            Assert.AreEqual(a.Count, b.Count, "two identical worlds disagreed about how many schools");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Centre, b[i].Centre, "centre");
                Assert.AreEqual(a[i].DepthMetres, b[i].DepthMetres, 1e-6f, "depth");
                Assert.AreEqual(a[i].MarkCount, b[i].MarkCount, "marks");
                CollectionAssert.AreEqual(a[i].SpeciesIds, b[i].SpeciesIds, "species");
            }
        }

        /// <summary>
        /// The honesty invariant survives the reorder: the school the WATER draws is the school the ROD
        /// finds. A point query standing on a school's own centre must return that same school.
        /// </summary>
        [Test]
        public void TheSchoolDrawnIsTheSchoolFished()
        {
            var w = new FakeWorld();
            var m = Model(w, Species("fish.atlantic_cod", density: 45f));

            List<FishSchool> drawn = SweepRegion(m, 700.0);
            Assert.IsNotEmpty(drawn);

            var at = new List<FishSchool>();
            foreach (FishSchool s in drawn)
            {
                m.SchoolsAt(s.Centre, 700.0, at);
                bool found = false;
                foreach (FishSchool q in at)
                    if (q.Centre == s.Centre && q.MarkCount == s.MarkCount) { found = true; break; }
                Assert.IsTrue(found,
                    $"a school the water drew at {s.Centre} is not there when the rod asks");
            }
        }
    }
}
