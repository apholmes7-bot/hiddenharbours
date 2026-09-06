using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE FISH YOU CAN SEE (owner's ruling 2026-09-05) — the presenter's pure half: the C# port of the
    /// rig's <c>shoal()</c>, the depth-to-visibility table, the event schedule, and the honesty invariant
    /// carried across to the WATER.
    ///
    /// <para><b>The load-bearing claim is that the presenter invents nothing.</b> A fish drawn on the
    /// water must be a fish the resolver would bite, of a species it would roll — so the tests below do
    /// not merely exercise <see cref="IFishSchoolView.SchoolsInView"/>, they compare it against
    /// <see cref="IFishSchools.SchoolsAt"/> at the same instant and demand the SAME school, field for
    /// field. That is the check that would fail if anyone ever gave the view query its own density, its
    /// own species pick or its own filter.</para>
    ///
    /// <para><b>The shoal port is pinned to the rig, not to itself.</b> The golden rows in
    /// <see cref="ShoalPortMatchesTheRigOracle"/> were produced by running the rig's own
    /// <c>FishIso2.shoal()</c> arithmetic (<c>docs/art/rigs/catch-pass-2-kit/Art/fishIsoRig2.js</c>)
    /// under JS semantics — float64 throughout, <c>Math.imul</c>, and JS's round-half-toward-+∞. A port
    /// that agreed only with itself would pass a self-consistency test and still draw the wrong
    /// fish.</para>
    /// </summary>
    public class VisibleFishTests
    {
        private const int Seed = 4242;
        private const string Region = "region.st_peters";
        private const double SecondsPerHour = 75.0;   // GameConfig.SecondsPerDay 1800 / 24

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown() => GameServices.Reset();

        // ---- fixtures ---------------------------------------------------------------------------------

        private sealed class FakeWorld : IFishSchoolWorld
        {
            public int Seed = VisibleFishTests.Seed;
            public string Region = VisibleFishTests.Region;
            public float Column = 18f;
            public float SeaState = 0f;
            public Season Time = Season.HighSummer;

            public int WorldSeed => Seed;
            string IFishSchoolWorld.RegionId => Region;
            public float WaterColumnAt(Vector2 worldPos, double gameSeconds) => Column;
            public float SeaState01At(double gameSeconds) => SeaState;
            public Season SeasonAt(double gameSeconds) => Time;
        }

        private static FishSchoolSettings Settings(float chance = 1f)
        {
            FishSchoolSettings s = FishSchoolSettings.Default;
            s.BaseAppearanceChance01 = chance;
            return s;
        }

        private static FishSpeciesDef Species(string id, FishDepthBand bands, FishFlags flags = FishFlags.None)
        {
            var d = ScriptableObject.CreateInstance<FishSpeciesDef>();
            d.Id = id;
            d.RegionIds = new[] { Region };
            d.Seasons = SeasonMask.AllYear;
            d.DepthBands = bands;
            d.BehaviorFlags = flags;
            return d;
        }

        private static FishSchoolModel Model(FakeWorld w, params FishSpeciesDef[] pool)
            => new FishSchoolModel(w, pool, Settings(), DepthDropSettings.Default, SecondsPerHour);

        // ---- the port is the rig's, arithmetic for arithmetic -----------------------------------------

        /// <summary>
        /// The ported <c>shoal()</c> reproduces the rig's own output. Golden rows are the JS oracle's;
        /// the tolerance is a float ULP band, not bit equality, because the two transcriptions round a
        /// double to a float at different points — bit equality between transcriptions is unattainable
        /// and demanding it would make this guard rot on the first harmless refactor.
        /// </summary>
        [Test]
        public void ShoalPortMatchesTheRigOracle()
        {
            // cod, 5 fish, seed 11, t = 12.5 s — from FishIso2.shoal() under JS semantics.
            var expected = new[]
            {
                new[] { 0.476749503f, -0.232445309f, -0.133370605f, -0.914354963f, 7f, 1f, 0.945370661f },
                new[] { 0.412580928f, -0.956498433f, -0.141634533f, -0.804331453f, 7f, 1f, 0.924973548f },
                new[] { 0.417591474f, -0.278198315f, -0.135659538f, -1.148636863f, 7f, 0f, 1.109708885f },
                new[] { 1.268600954f, -0.536611900f, -0.164346038f, -0.910900301f, 7f, 2f, 1.107842928f },
                new[] { 0.780864246f,  0.771457619f, -0.118745323f, -0.824852192f, 7f, 5f, 0.955873938f },
            };

            var into = new ShoalMath.Swimmer[8];
            int n = ShoalMath.Fill(0.70f, 5, 12.5, 11, ShoalMath.DefaultRadiusMetres,
                                   ShoalMath.DefaultSpeedMetresPerSecond, 1f, ShoalMath.DefaultZMetres, into);

            Assert.AreEqual(5, n, "the fill wrote a different number of fish than it was asked for");
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(expected[i][0], into[i].X, 1e-5f, $"fish {i} x");
                Assert.AreEqual(expected[i][1], into[i].Y, 1e-5f, $"fish {i} y");
                Assert.AreEqual(expected[i][2], into[i].Z, 1e-5f, $"fish {i} z");
                Assert.AreEqual(expected[i][3], into[i].HeadingRad, 1e-5f, $"fish {i} heading");
                Assert.AreEqual((int)expected[i][4], into[i].Dir, $"fish {i} dir (the baked sheet row)");
                Assert.AreEqual((int)expected[i][5], into[i].Frame, $"fish {i} swim frame");
                Assert.AreEqual(expected[i][6], into[i].Scale, 1e-5f, $"fish {i} scale");
            }
        }

        /// <summary>
        /// ⚠ The one line where the obvious .NET call is the wrong one: JS's <c>Math.round</c> breaks ties
        /// toward +∞, so <c>round(-1.5) === -1</c>, while every .NET rounding mode breaks them
        /// symmetrically and would hand back the NEIGHBOURING sheet row. Half of all headings out of
        /// <c>Atan2</c> are negative, so this is a live boundary, not a curiosity.
        /// </summary>
        [Test]
        public void DirOfBreaksTiesTheRigsWay()
        {
            // ⚠ The quantum must be the DOUBLE one the function divides by. Writing it as
            // Mathf.PI / 4f (a float) puts the probe a hair off the boundary, which lands on the other
            // side of the tie and tests nothing about the tie-break at all.
            const double q = System.Math.PI / 4.0;
            Assert.AreEqual(0, ShoalMath.DirOf(-0.5 * q), "round(-0.5) is 0 in the rig, not -1");
            Assert.AreEqual(7, ShoalMath.DirOf(-1.5 * q), "round(-1.5) is -1 in the rig, not -2");
            Assert.AreEqual(6, ShoalMath.DirOf(-2.5 * q), "round(-2.5) is -2 in the rig, not -3");
            Assert.AreEqual(1, ShoalMath.DirOf(0.5 * q));
            Assert.AreEqual(2, ShoalMath.DirOf(1.5 * q));

            // The tie is knife-edge by nature, so the guard also pins the rule either side of it —
            // a boundary test that only ever probes the exact midpoint rots the moment anything
            // upstream nudges the heading by an ulp.
            Assert.AreEqual(7, ShoalMath.DirOf(-0.5 * q - 1e-9), "just below the tie rounds down");
            Assert.AreEqual(0, ShoalMath.DirOf(-0.5 * q + 1e-9), "just above the tie rounds up");
            // and it stays inside the turntable for any heading at all
            for (int i = -20; i <= 20; i++)
            {
                int d = ShoalMath.DirOf(i * 0.37);
                Assert.IsTrue(d >= 0 && d < 8, $"dir {d} is off the 8-direction turntable");
            }
        }

        /// <summary>The same clock and seed give the same fish, exactly — the property a save/load and a
        /// region stream-in both rest on (rule 5).</summary>
        [Test]
        public void TheSameSeedAndClockGiveTheSameFish()
        {
            var a = new ShoalMath.Swimmer[6];
            var b = new ShoalMath.Swimmer[6];

            ShoalMath.Fill(0.55f, 6, 987.625, 31, 12f, 0.35f, 1f, -0.15f, a);
            ShoalMath.Fill(0.55f, 6, 987.625, 31, 12f, 0.35f, 1f, -0.15f, b);

            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X, "x drifted between two identical reads");
                Assert.AreEqual(a[i].Y, b[i].Y, "y drifted between two identical reads");
                Assert.AreEqual(a[i].HeadingRad, b[i].HeadingRad, "heading drifted");
                Assert.AreEqual(a[i].Dir, b[i].Dir, "dir drifted");
                Assert.AreEqual(a[i].Frame, b[i].Frame, "frame drifted");
            }
        }

        /// <summary>A negative control for the test above: a different CLOCK really does move the fish, so
        /// the equality check is not passing because the fill writes nothing.</summary>
        [Test]
        public void ADifferentClockMovesTheFish()
        {
            var a = new ShoalMath.Swimmer[4];
            var b = new ShoalMath.Swimmer[4];

            int na = ShoalMath.Fill(0.55f, 4, 100.0, 31, 12f, 0.35f, 1f, -0.15f, a);
            int nb = ShoalMath.Fill(0.55f, 4, 130.0, 31, 12f, 0.35f, 1f, -0.15f, b);

            Assert.AreEqual(4, na, "the fixture wrote no fish — the equality guard would be vacuous");
            Assert.AreEqual(4, nb);

            bool moved = false;
            for (int i = 0; i < 4; i++)
                if (!Mathf.Approximately(a[i].X, b[i].X) || !Mathf.Approximately(a[i].Y, b[i].Y))
                    moved = true;
            Assert.IsTrue(moved, "30 seconds passed and not one fish moved");
        }

        /// <summary>The fill never writes past the caller's array — the pooled-renderer budget depends on
        /// it (rule 7).</summary>
        [Test]
        public void TheFillNeverOverrunsTheCallersArray()
        {
            var small = new ShoalMath.Swimmer[3];
            Assert.AreEqual(3, ShoalMath.Fill(0.7f, 9, 5.0, 11, 10f, 0.35f, 1f, -0.15f, small));
            Assert.AreEqual(0, ShoalMath.Fill(0.7f, 9, 5.0, 11, 10f, 0.35f, 1f, -0.15f, null));
            Assert.AreEqual(0, ShoalMath.Fill(0.7f, 0, 5.0, 11, 10f, 0.35f, 1f, -0.15f, small));
        }

        // ---- depth is visibility (the owner's ruling) --------------------------------------------------

        /// <summary>The owner's table: shallow water shows the fish, the open column shows a shape, and
        /// over the drop-off shows nothing at all.</summary>
        [Test]
        public void DepthDecidesWhatTheWaterShows()
        {
            Assert.AreEqual(SwimmerDraw.Full, SwimmerVisibility.For(FishDepthBand.Tidepool));
            Assert.AreEqual(SwimmerDraw.Full, SwimmerVisibility.For(FishDepthBand.Shallows));
            Assert.AreEqual(SwimmerDraw.Dim, SwimmerVisibility.For(FishDepthBand.Inshore));
            Assert.AreEqual(SwimmerDraw.Shadow, SwimmerVisibility.For(FishDepthBand.Midwater));
            Assert.AreEqual(SwimmerDraw.None, SwimmerVisibility.For(FishDepthBand.Deep));
            Assert.AreEqual(SwimmerDraw.None, SwimmerVisibility.For(FishDepthBand.Abyssal));

            Assert.IsFalse(SwimmerVisibility.Draws(FishDepthBand.Deep),
                           "a deep school must cost the water nothing");
            Assert.IsTrue(SwimmerVisibility.Draws(FishDepthBand.Shallows));
        }

        /// <summary>The bands come from the ONE classifier the depth drop and the species pick already
        /// use — not a second set of thresholds this feature owns. Moving the owner's Shallows bar moves
        /// what the water shows, with no second number to remember.</summary>
        [Test]
        public void VisibilityReadsTheOwnersOwnDepthBands()
        {
            DepthDropSettings d = DepthDropSettings.Default;

            Assert.AreEqual(SwimmerVisibility.For(
                                DepthDropMath.ZoneForDepth(d.ShallowsMaxMeters - 0.01f,
                                                           d.TidepoolMaxMeters, d.ShallowsMaxMeters,
                                                           d.InshoreMaxMeters, d.MidwaterMaxMeters,
                                                           d.DeepMaxMeters)),
                            SwimmerVisibility.For(d.ShallowsMaxMeters - 0.01f, in d),
                            "the depth overload disagreed with the band overload");

            Assert.AreEqual(SwimmerDraw.None, SwimmerVisibility.For(d.DeepMaxMeters + 50f, in d),
                            "water past the deep bar must show nothing");
        }

        // ---- events are slotted, not rolled per fish ---------------------------------------------------

        /// <summary>A flounder never leaves the water. The flag is the owner's ruling and the schedule is
        /// the only thing that reads it.</summary>
        [Test]
        public void ANonJumperNeverJumps()
        {
            for (int i = 0; i < 4000; i++)
            {
                double t = i * 0.25;
                ShoalEventKind k = ShoalEventMath.At(Seed, 12345u, t, 5, speciesJumps: false,
                                                     periodSeconds: 20.0, out _, out _);
                Assert.AreNotEqual(ShoalEventKind.Jump, k, $"a non-jumper jumped at t={t}");
            }
        }

        /// <summary>A negative control for the test above: a jumper, on the same schedule, does jump — so
        /// the guard is not passing because nothing ever fires.</summary>
        [Test]
        public void AJumperDoesJump()
        {
            bool jumped = false;
            for (int i = 0; i < 4000 && !jumped; i++)
                if (ShoalEventMath.At(Seed, 12345u, i * 0.25, 5, speciesJumps: true, 20.0, out _, out _)
                    == ShoalEventKind.Jump) jumped = true;

            Assert.IsTrue(jumped, "no jump in 1000 s — the non-jumper guard would be vacuous");
        }

        /// <summary>
        /// One actor per period, never two. This is the anti-clump property: a hash rolled per fish gives
        /// a uniform distribution, which is NOT a spread — it leaves dead stretches and puts three fish in
        /// the air at once. Slotting the schedule is what buys an even sea.
        /// </summary>
        [Test]
        public void OnlyOneFishActsAtATime()
        {
            for (int i = 0; i < 8000; i++)
            {
                double t = i * 0.05;
                ShoalEventKind k = ShoalEventMath.At(Seed, 999u, t, 5, true, 20.0,
                                                     out int actor, out double start);
                if (k == ShoalEventKind.None) { Assert.AreEqual(-1, actor); continue; }

                Assert.IsTrue(actor >= 0 && actor < 5, $"actor {actor} is not a fish in this school");
                Assert.IsTrue(t >= start && t < start + ShoalEventMath.DurationSeconds(k),
                              "an event was reported outside its own anim's run");
            }
        }

        /// <summary>An event plays ONCE and holds its last frame — a wrapped jump would loop a fish
        /// through the air forever.</summary>
        [Test]
        public void AnEventPlaysOnceAndDoesNotWrap()
        {
            double d = ShoalEventMath.DurationSeconds(ShoalEventKind.Jump);
            Assert.AreEqual(0, ShoalEventMath.FrameAt(ShoalEventKind.Jump, 100.0, 100.0));
            Assert.AreEqual(ShoalEventMath.JumpFrames - 1,
                            ShoalEventMath.FrameAt(ShoalEventKind.Jump, 100.0, 100.0 + d * 2.0),
                            "the jump wrapped instead of holding its last frame");

            // the arc leaves and re-enters the water
            Assert.AreEqual(0f, ShoalEventMath.JumpArc01(100.0, 100.0), 1e-4f);
            Assert.AreEqual(0f, ShoalEventMath.JumpArc01(100.0, 100.0 + d), 1e-4f);
            Assert.Greater(ShoalEventMath.JumpArc01(100.0, 100.0 + d * 0.5f), 0.9f,
                           "the fish never actually left the water");
        }

        // ---- the honesty invariant, carried to the water -----------------------------------------------

        /// <summary>
        /// THE LOAD-BEARING TEST. A school the presenter draws is the very school the rod finds when you
        /// cast onto it — same centre, same depth, same density, same species. If anyone ever gives the
        /// view query its own density, its own species pick or its own filter, this is what reddens.
        /// </summary>
        [Test]
        public void AFishYouCanSeeIsAFishYouCanCatch()
        {
            var w = new FakeWorld();
            FishSchoolModel model = Model(w, Species("fish.atlantic_cod", FishDepthBand.None));

            var view = new List<FishSchool>();
            var at = new List<FishSchool>();
            const double now = 5000.0;

            // A wide view, so there are schools to compare in the first place.
            int seen = model.SchoolsInView(new Rect(-400f, -400f, 800f, 800f), now, view);
            Assert.Greater(seen, 0, "no schools in view — the comparison below would be vacuous");

            int compared = 0;
            foreach (FishSchool s in view)
            {
                // Stand on the school's own centre and ask the GAMEPLAY question.
                Assert.AreEqual(1, model.SchoolsAt(s.Centre, now, at),
                                "a school the water drew is not the school the rod finds at its centre");

                FishSchool r = at[0];
                Assert.AreEqual(s.Centre, r.Centre, "centre");
                Assert.AreEqual(s.DepthMetres, r.DepthMetres, "depth");
                Assert.AreEqual(s.RadiusMetres, r.RadiusMetres, "radius");
                Assert.AreEqual(s.MarkCount, r.MarkCount, "density — the bite rate and the drawn count");
                Assert.AreEqual(s.StartSeconds, r.StartSeconds, "window start");

                CollectionAssert.AreEqual(s.SpeciesIds, r.SpeciesIds,
                                          "the water would draw a species the resolver would not roll");
                compared++;
            }

            Assert.Greater(compared, 0);
        }

        /// <summary>
        /// The same load-bearing identity as <see cref="AFishYouCanSeeIsAFishYouCanCatch"/>, stated over
        /// the STRUCTURAL fields alone so it needs no <c>ScriptableObject</c> and therefore runs in every
        /// harness, not only inside the editor. The species half is the other test's to prove.
        /// </summary>
        [Test]
        public void EverySchoolInViewIsTheSchoolTheRodFindsAtItsCentre()
        {
            var w = new FakeWorld();
            FishSchoolModel model = Model(w);

            var view = new List<FishSchool>();
            var at = new List<FishSchool>();
            const double now = 5000.0;

            Assert.Greater(model.SchoolsInView(new Rect(-400f, -400f, 800f, 800f), now, view), 0,
                           "no schools in view — the comparison below would be vacuous");

            // The premise the comparison rests on: these schools are not all one identical thing, so
            // an equality check over them can actually fail.
            var densities = new HashSet<int>();
            foreach (FishSchool s in view) densities.Add(s.MarkCount);
            Assert.Greater(view.Count, 1, "one school is not enough to call this a comparison");

            foreach (FishSchool s in view)
            {
                Assert.AreEqual(1, model.SchoolsAt(s.Centre, now, at),
                                "a school the water drew is not the school the rod finds at its centre");
                FishSchool r = at[0];
                Assert.AreEqual(s.Centre, r.Centre, "centre");
                Assert.AreEqual(s.DepthMetres, r.DepthMetres, "depth");
                Assert.AreEqual(s.RadiusMetres, r.RadiusMetres, "radius");
                Assert.AreEqual(s.MarkCount, r.MarkCount, "density — the bite rate and the drawn count");
                Assert.AreEqual(s.StartSeconds, r.StartSeconds, "window start");
                Assert.AreEqual(s.EndSeconds, r.EndSeconds, "window end");
            }
        }

        /// <summary>A school is drawn while any of its water is on screen, not only when its centre is —
        /// the pop this seam exists to prevent.</summary>
        [Test]
        public void ASchoolAtTheScreenEdgeIsStillDrawn()
        {
            var w = new FakeWorld();
            FishSchoolModel model = Model(w);

            var wide = new List<FishSchool>();
            const double now = 5000.0;
            Assert.Greater(model.SchoolsInView(new Rect(-400f, -400f, 800f, 800f), now, wide), 0);

            FishSchool s = wide[0];

            // A tiny view placed just OUTSIDE the school's centre but inside its rim.
            float justInside = s.RadiusMetres * 0.9f;
            var edge = new List<FishSchool>();
            var r = new Rect(s.Centre.x + justInside, s.Centre.y - 0.5f, 1f, 1f);

            int n = model.SchoolsInView(r, now, edge);
            Assert.Greater(n, 0, "a school reaching into the view was clipped away at its centre");
            CollectionAssert.Contains(new List<Vector2> { edge[0].Centre }, s.Centre);
        }

        /// <summary>The view read never returns more than its own documented budget, whatever the rect —
        /// the presenter's pool is sized against this (rule 7).</summary>
        [Test]
        public void TheViewReadIsBounded()
        {
            var w = new FakeWorld();
            FishSchoolModel model = Model(w);

            var into = new List<FishSchool>();
            int n = model.SchoolsInView(new Rect(-100000f, -100000f, 200000f, 200000f), 5000.0, into);

            Assert.LessOrEqual(n, FishSchoolModel.ViewCells,
                               "the view query grew past the budget the seam documents");
            Assert.AreEqual(n, into.Count, "the count and the filled list disagree");
        }

        /// <summary>The owner's off switch empties the water as well as the glass.</summary>
        [Test]
        public void TheOwnersOffSwitchEmptiesTheWater()
        {
            var w = new FakeWorld();
            FishSchoolSettings off = Settings(chance: 1f);
            off.BaseAppearanceChance01 = 0f;

            var model = new FishSchoolModel(w, System.Array.Empty<FishSpeciesDef>(),
                                            off, DepthDropSettings.Default, SecondsPerHour);

            var into = new List<FishSchool>();
            Assert.AreEqual(0, model.SchoolsInView(new Rect(-400f, -400f, 800f, 800f), 5000.0, into));
            Assert.AreEqual(0, into.Count);
        }
    }
}
