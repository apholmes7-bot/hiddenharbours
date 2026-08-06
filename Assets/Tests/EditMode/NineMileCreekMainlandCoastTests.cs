using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ THE NINE MILE CREEK MAINLAND'S COAST PLAN — its own plan, its own tests, as the handoff
    /// requires, because St Peters' plan sits 0.02° inside the aspect law's ceiling and must not be
    /// nudged to make room for a mainland.
    ///
    /// <para><b>Every number here is DERIVED from the plan the builder pushes</b>
    /// (<see cref="NineMileCreekMainland.CoastPoints"/> / <see cref="NineMileCreekMainland.CoastSectors"/>
    /// through <see cref="MainlandCoast"/>'s own arithmetic), never from a literal copied out of it —
    /// the house's derived-bounds law. A classifier that silently died would move the measured cliff
    /// share by a FACTOR here, and a bound written down by hand would go on passing.</para>
    ///
    /// <para><see cref="NineMileCreekMainlandTerrainTests"/> is the sibling that asserts what the tide
    /// does to this coast, where the crossing hands over to St Peters, and whether the roads stay dry.
    /// This file asserts the SHAPE.</para>
    /// </summary>
    public class NineMileCreekMainlandCoastTests
    {
        private static Vector2[] Points => NineMileCreekMainland.CoastPoints;
        private static CoastRunSector[] Sectors => NineMileCreekMainland.CoastSectors;
        private static float Run => NineMileCreekMainland.CoastRunLength;

        // =============================================================================================
        //  the run itself
        // =============================================================================================

        [Test]
        public void TheCoastIsAnOpenRunFromOneRegionEdgeToTheOther()
        {
            Assert.That(MainlandCoast.IsRun(Points), Is.True, "the coastline must have at least one segment");

            float halfHeight = NineMileCreekMainland.RegionWorldSize.y * 0.5f;
            Vector2 south = Points[0];
            Vector2 north = Points[Points.Length - 1];

            Assert.That(south.y, Is.EqualTo(-halfHeight).Within(0.01f),
                "the coast must START on the region's south edge — a mainland's shore does not stop " +
                "inside the map, it runs off it");
            Assert.That(north.y, Is.EqualTo(halfHeight).Within(0.01f),
                "the coast must END on the region's north edge, for the same reason");
        }

        [Test]
        public void TheSeaIsEastAndTheFieldsAreWest()
        {
            // The whole plan hangs on the run's handedness: MainlandCoast puts the sea on the RIGHT of
            // the authored walk direction, and this coast is authored south → north. If a future edit
            // reverses the point order, every profile in the region turns inside out — so measure it.
            MainlandCoast.Project(Points, new Vector2(300f, 0f), out _, out float seawardEast);
            MainlandCoast.Project(Points, new Vector2(-300f, 0f), out _, out float seawardWest);

            Assert.That(seawardEast, Is.GreaterThan(0f), "a point out in the bay must read as SEAWARD");
            Assert.That(seawardWest, Is.LessThan(0f), "a point out in the fields must read as INLAND");
        }

        [Test]
        public void TheSectorsAreAClosedOrderedPartitionOfTheRun()
        {
            Assert.That(Sectors.Length, Is.GreaterThan(0), "the region authors a coast plan");
            Assert.That(Sectors[0].FromMetres, Is.EqualTo(0f).Within(0.001f),
                "the first sector must start at the run's own start, or the coast has an unclassified head");

            for (int i = 1; i < Sectors.Length; i++)
                Assert.That(Sectors[i].FromMetres, Is.GreaterThan(Sectors[i - 1].FromMetres),
                    $"sector {i} must start after sector {i - 1} — the table is read in order");

            Assert.That(Sectors[Sectors.Length - 1].FromMetres, Is.LessThan(Run),
                "the last sector must start inside the run");

            // The partition is total: every metre of coast answers with some class.
            float total = 0f;
            for (int i = 0; i < Sectors.Length; i++) total += MainlandCoast.SectorLength(Sectors, i, Run);
            Assert.That(total, Is.EqualTo(Run).Within(0.01f),
                "the sectors must tile the whole run with no gap and no overlap");
        }

        [Test]
        public void NoSectorIsShorterThanTheBlendItHasToSurvive()
        {
            // A sector narrower than twice the feather never reaches its own profile: it is blended into
            // its neighbours from both ends at once and the class it names is a class the ground never
            // actually takes.
            float minimum = 2f * NineMileCreekMainland.CoastFeatherMetres;
            for (int i = 0; i < Sectors.Length; i++)
            {
                float length = MainlandCoast.SectorLength(Sectors, i, Run);
                Assert.That(length, Is.GreaterThanOrEqualTo(minimum),
                    $"sector {i} ({Sectors[i].Class}) is {length:0.0} m; it must be at least " +
                    $"{minimum:0.0} m (2 x the {NineMileCreekMainland.CoastFeatherMetres:0.0} m feather) " +
                    "or it never reaches its own profile");
            }
        }

        // =============================================================================================
        //  the aspect law — measured on the real normal, per SEGMENT
        // =============================================================================================

        [Test]
        public void EveryCliffSectorStandsWhereTheKitCanHonestlyDrawIt()
        {
            var report = new StringBuilder();
            bool ok = true;

            for (int i = 0; i < Sectors.Length; i++)
            {
                if (!CoastPlan.IsCliff(Sectors[i].Class)) continue;
                float from = Sectors[i].FromMetres;
                float to = from + MainlandCoast.SectorLength(Sectors, i, Run);
                float worst = MainlandCoast.WorstAspectError(Points, from, to);

                report.AppendLine($"  {Sectors[i].Class,-16} s=[{from,7:0.0},{to,7:0.0})  " +
                                  $"worst aspect error {worst:0.00}°");
                if (worst > CoastPlan.AspectSnapToleranceDegrees + 1e-3f) ok = false;
            }

            Assert.That(ok, Is.True,
                "a cliff whose seaward normal is further than " +
                $"{CoastPlan.AspectSnapToleranceDegrees}° from EVERY facing the kit authors cannot be " +
                "drawn honestly — the kit bakes no north face, because a north-facing wall shows its " +
                "shadowed back to a 3/4 top-down camera.\n" + report);
        }

        [Test]
        public void NoCliffRunIsTooShortToReadAsRock()
        {
            for (int i = 0; i < Sectors.Length; i++)
            {
                if (!CoastPlan.IsCliff(Sectors[i].Class)) continue;
                float length = MainlandCoast.SectorLength(Sectors, i, Run);
                Assert.That(length, Is.GreaterThanOrEqualTo(NineMileCreekMainland.MinimumCliffRunMetres),
                    $"the {Sectors[i].Class} run at s={Sectors[i].FromMetres:0.0} is {length:0.0} m; a " +
                    $"run under {NineMileCreekMainland.MinimumCliffRunMetres:0} m cannot show the kit's " +
                    "periodic wall twice, so it reads as a texture accident rather than as rock");
            }
        }

        [Test]
        public void TheCliffShareSitsWellUnderTheAspectLawsOwnCeiling_AndTheMainlandHasRoomStPetersHasNot()
        {
            float authored = MainlandCoast.CliffLength(Sectors, Run);
            float ceiling = MainlandCoast.CliffLegalLength(Points);

            Assert.That(authored, Is.LessThanOrEqualTo(ceiling),
                "the plan cannot author more cliff than the aspect law permits");

            // The headline difference between this coast and St Peters', and the reason the mainland
            // could be given its own plan instead of squeezing another sector out of the island's.
            float ceilingShare = ceiling / Run;
            Assert.That(ceilingShare, Is.GreaterThan(0.75f),
                $"an east-facing mainland should be almost entirely cliff-LEGAL; measured " +
                $"{ceilingShare:P2} ({ceiling:0.0} m of {Run:0.0} m). If this has fallen the coastline " +
                "has been bent away from the kit's facings and the plan needs re-deriving, not nudging.");

            float authoredShare = authored / Run;
            Assert.That(authoredShare, Is.GreaterThan(0.15f),
                "a coast the docs describe as red sandstone bluffs should stand up somewhere");
            Assert.That(authoredShare, Is.LessThan(0.45f),
                "...and the owner's photographs are of a LOW coast; the drama here belongs to the " +
                "crossing and the working wharf, not to a wall of rock");
        }

        [Test]
        public void TheCreekMouthIsTheOnlyStretchACliffMayNotStandOn()
        {
            // Not a decoration: it is the one place this coastline turns its back on the camera, and it
            // is exactly where the plan puts marsh and sand. If a future edit makes some OTHER stretch
            // illegal, the plan has to be re-read rather than patched.
            float illegal = Run - MainlandCoast.CliffLegalLength(Points);
            Assert.That(illegal, Is.GreaterThan(0f),
                "the creek mouth's two inner banks face NE and NNE — they must be illegal for cliff, or " +
                "the notch has been flattened out of the coastline and the barachois has lost its mouth");

            for (int i = 0; i < MainlandCoast.SegmentCount(Points); i++)
            {
                if (MainlandCoast.CliffIsLegalOn(Points, i)) continue;
                float from = MainlandCoast.SegmentStart(Points, i);
                float to = from + MainlandCoast.SegmentLength(Points, i);
                // Sample the middle of the illegal segment — the class there must be soft.
                CoastClass c = MainlandCoast.ClassAt(Sectors, 0.5f * (from + to));
                Assert.That(CoastPlan.IsCliff(c), Is.False,
                    $"segment {i} (s {from:0.0}..{to:0.0}, seaward normal " +
                    $"{MainlandCoast.OutwardNormalAzimuth(Points, i):0.0}°) is illegal for cliff but the " +
                    $"plan authors {c} there");
            }
        }

        // =============================================================================================
        //  the crossing's landing — the owner's words, measured
        // =============================================================================================

        [Test]
        public void TheCrossingComesAshoreOnBeach_WithRoomEitherSide()
        {
            float landing = NineMileCreekMainland.BarLandingAlong;
            int index = MainlandCoast.SectorIndexAt(Sectors, landing);
            Assert.That(Sectors[index].Class, Is.EqualTo(CoastClass.Beach),
                "the crossing has to land on walkable shore — it is the prologue's first landfall");

            // The bar bares out to its half-width either side of the centre-line, so the landing is a
            // FOOTPRINT, not a point. A landing with no margin is a landing a later nudge silently
            // walls off (St Peters learned this at 252°; the margin is cheap and the failure is silent).
            float from = Sectors[index].FromMetres;
            float to = from + MainlandCoast.SectorLength(Sectors, index, Run);
            float half = NineMileCreekMainland.BarHalfWidth;

            Assert.That(landing - half, Is.GreaterThan(from + 10f),
                "the bar's south edge must land inside the beach with room to spare");
            Assert.That(landing + half, Is.LessThan(to - 10f),
                "the bar's north edge must land inside the beach with room to spare");
        }

        [Test]
        public void MostOfTheLandingsBeachLiesSouthOfTheCrossing()
        {
            // The owner's brief, verbatim: "at the mainland end of the crossing, beach to the SOUTH of
            // the crossing point". The run is authored south → north, so "south of" is "earlier along".
            float landing = NineMileCreekMainland.BarLandingAlong;
            int index = MainlandCoast.SectorIndexAt(Sectors, landing);
            float from = Sectors[index].FromMetres;
            float to = from + MainlandCoast.SectorLength(Sectors, index, Run);

            float south = landing - from;
            float north = to - landing;
            Assert.That(south, Is.GreaterThan(north),
                $"beach south of the crossing {south:0.0} m vs north {north:0.0} m — the owner asked for " +
                "the beach to be SOUTH of the landing");
        }

        [Test]
        public void ThereIsAWayDownOntoTheForeshoreFromTheCliffCoast()
        {
            // Without an Access gully the whole cliff foreshore — the ledges the tide lends you twice a
            // month — is scenery you can see and never reach.
            bool hasAccess = false;
            bool accessTouchesCliff = false;
            for (int i = 0; i < Sectors.Length; i++)
            {
                if (Sectors[i].Class != CoastClass.Access) continue;
                hasAccess = true;
                bool before = i > 0 && CoastPlan.IsCliff(Sectors[i - 1].Class);
                bool after = i + 1 < Sectors.Length && CoastPlan.IsCliff(Sectors[i + 1].Class);
                if (before || after) accessTouchesCliff = true;
            }

            Assert.That(hasAccess, Is.True, "the cliff coast needs at least one Access gully");
            Assert.That(accessTouchesCliff, Is.True,
                "an Access gully in the middle of a beach is a path to nowhere — it has to cut a cliff run");
        }

        // =============================================================================================
        //  the blend
        // =============================================================================================

        [Test]
        public void TheProfilesMeetHalfwayOnAJoin_AndAreUndilutedInASectorsInterior()
        {
            for (int i = 1; i < Sectors.Length; i++)
            {
                float join = Sectors[i].FromMetres;
                MainlandCoast.BlendAt(Sectors, join, Run, NineMileCreekMainland.CoastFeatherMetres,
                                      out CoastClass primary, out CoastClass secondary, out float weight);
                Assert.That(weight, Is.EqualTo(0.5f).Within(1e-4f),
                    $"exactly ON the join at s={join:0.0} the two sides must agree, or the height field " +
                    "is discontinuous there and the seabed bake aliases across it");
                Assert.That(primary, Is.EqualTo(Sectors[i].Class));
                Assert.That(secondary, Is.EqualTo(Sectors[i - 1].Class));
            }

            // And in the middle of a sector the class is its own, undiluted.
            for (int i = 0; i < Sectors.Length; i++)
            {
                float mid = Sectors[i].FromMetres + 0.5f * MainlandCoast.SectorLength(Sectors, i, Run);
                MainlandCoast.BlendAt(Sectors, mid, Run, NineMileCreekMainland.CoastFeatherMetres,
                                      out CoastClass primary, out CoastClass secondary, out float weight);
                Assert.That(weight, Is.EqualTo(1f).Within(1e-4f), $"sector {i} interior");
                Assert.That(primary, Is.EqualTo(secondary));
            }
        }

        [Test]
        public void TheRunsTwoENDSDoNotBlendIntoEachOther()
        {
            // A ring's first and last sector are neighbours. A run's are not, and feathering them would
            // smear the region's north edge into its south one.
            MainlandCoast.BlendAt(Sectors, 0f, Run, NineMileCreekMainland.CoastFeatherMetres,
                                  out CoastClass headPrimary, out CoastClass headSecondary, out float headWeight);
            Assert.That(headWeight, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(headPrimary, Is.EqualTo(headSecondary));

            MainlandCoast.BlendAt(Sectors, Run, Run, NineMileCreekMainland.CoastFeatherMetres,
                                  out CoastClass tailPrimary, out CoastClass tailSecondary, out float tailWeight);
            Assert.That(tailWeight, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(tailPrimary, Is.EqualTo(tailSecondary));
        }
    }
}
