using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;                 // SpriteLightMath — the shared bake camera's squash
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The two houses behind Nine Mile Creek — §7.2's "a couple of buildings for flavour", built from the
    /// baked village kit rather than the loose sprites they replace.
    ///
    /// <para>Two classes of check, and they fail for different reasons. The PURE ones (which builds, and
    /// where they stand relative to the quay and to each other) hold whatever the art state is. The
    /// CONTRACT ones need the kit's committed <c>Buildings.json</c>, because a footprint is the kit's
    /// number and not this file's — those are what catch the failure that actually matters here: the
    /// houses were moved out to the empty western land precisely BECAUSE the kit's buildings are 6–10 m
    /// across their footprint where the greybox squares they replace were 5, and nothing but the contract
    /// knows that.</para>
    /// </summary>
    public class NineMileCreekFlavourTests
    {
        private static VillageBuildingCatalog.Placement Placement(string key)
            => VillageBuildingCatalog.Find(key);

        private static void RequireContract()
        {
            if (VillageBuildingCatalog.Scan().Count == 0)
                Assert.Ignore("the village building kit has not been baked in this environment — the " +
                              "footprint checks need its contract. CI checks them out of LFS.");
        }

        // ---- 1. they are builds the kit really has, and the right ones -----------------------------

        [Test]
        public void BothHousesNameARealKitBuild()
        {
            foreach (var house in NineMileCreekFlavour.Houses)
                Assert.IsNotNull(VillageBuildingKit.FindBuild(house.Key),
                    $"'{house.Key}' is not a build in VillageBuildingKit.M1Set. A stem that cannot be " +
                    "matched to a build fails SILENTLY at placement — it just does not appear");
        }

        [Test]
        public void NeitherIsTheSchoolNorTheStore_BecauseThisIsACreekAndNotAVillage()
        {
            var keys = NineMileCreekFlavour.Houses.Select(h => h.Key).ToList();

            Assert.IsFalse(keys.Contains("school"),
                "a working creek of this size has no schoolhouse — that is the island's, and putting one " +
                "here is how 'a working creek, not a town' quietly stops being true");
            Assert.IsFalse(keys.Contains("generalStore"),
                "the creek's chandlery is already a stall on the working row; a second storefront behind " +
                "it reads as a high street");

            Assert.AreEqual(keys.Count, keys.Distinct().Count(), "and they are not the same house twice");
            Assert.AreEqual(2, keys.Count, "§7.2 asks for a couple, and a couple is two");
        }

        // ---- 2. where they stand ------------------------------------------------------------------

        [Test]
        public void BothStandOnGroundThatIsDryAtEveryTide()
        {
            var go = new GameObject("TidalTerrain");
            try
            {
                var terrain = go.AddComponent<MainlandTidalTerrain>();
                NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);

                foreach (var house in NineMileCreekFlavour.Houses)
                {
                    float ground = terrain.ElevationAt(house.Position);
                    Assert.Greater(ground, NineMileCreekBuilder.SpringHighWater,
                        $"{house.Key} at {house.Position} stands on {ground:0.00} m against a spring high " +
                        $"of {NineMileCreekBuilder.SpringHighWater:0.00} m. A house does not flood");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheSpringHighUsedForSitingIsTheWidestThatCanReachTheRegion()
        {
            // Nothing re-points the tide per region yet, so the START scene's swing is what actually runs
            // here. The two are now IDENTICAL — the recreation gave the mainland St Peters' tide verbatim,
            // because the tidal bar spans the seam between them and two tides means two bars — so the fold
            // is a no-op today. It is kept, and asserted, precisely BECAUSE it is a no-op: the day
            // somebody re-tunes one of the two, siting must follow the bigger of them rather than silently
            // keeping the smaller.
            Assert.GreaterOrEqual(NineMileCreekBuilder.SpringHighWater,
                                  NineMileCreekBuilder.TideMean + NineMileCreekBuilder.TideAmplitude);
            Assert.GreaterOrEqual(NineMileCreekBuilder.SpringHighWater,
                                  StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude,
                "siting must clear the widest swing that can actually reach this region");

            Assert.AreEqual(StPetersBuilder.TideAmplitude, NineMileCreekBuilder.TideAmplitude, 1e-4f,
                "⭐ the two halves of ONE tidal bar must run one tide: a crossing that spans a region " +
                "seam is dry on one side and flooded on the other the moment they differ");
        }

        [Test]
        public void NeitherHouseIsBuiltOnTheWorkingCreek()
        {
            RequireContract();

            Rect deck = NineMileCreekWharf.DeckFootprint();

            foreach (var house in NineMileCreekFlavour.Houses)
            {
                var p = Placement(house.Key);
                if (!p.IsValid) continue;
                float r = NineMileCreekFlavour.FootprintRadiusMetres(p);

                // Nearest point of the quay to the house — a footprint that reaches it has been built on
                // the wharf, which is the one piece of ground the creek cannot spare.
                var onDeck = new Vector2(Mathf.Clamp(house.Position.x, deck.xMin, deck.xMax),
                                         Mathf.Clamp(house.Position.y, deck.yMin, deck.yMax));
                Assert.Greater(Vector2.Distance(house.Position, onDeck), r,
                    $"{house.Key}'s footprint ({r:0.0} m radius) reaches the quay");

                Assert.Greater(Vector2.Distance(house.Position, NineMileCreekDory.HaulOutPos), r,
                    $"{house.Key} is built on top of the derelict dory");

                foreach (var person in NineMileCreekPeople.People)
                    Assert.Greater(Vector2.Distance(house.Position, person.Position), r,
                        $"{person.AssetName} is standing inside {house.Key}");
            }
        }

        [Test]
        public void TheTwoHousesLeaveALaneBetweenThem()
        {
            RequireContract();

            var houses = NineMileCreekFlavour.Houses;
            for (int i = 0; i < houses.Count; i++)
            for (int j = i + 1; j < houses.Count; j++)
            {
                var a = Placement(houses[i].Key);
                var b = Placement(houses[j].Key);
                if (!a.IsValid || !b.IsValid) continue;

                float need = NineMileCreekFlavour.FootprintRadiusMetres(a) +
                             NineMileCreekFlavour.FootprintRadiusMetres(b) +
                             NineMileCreekFlavour.LaneGap;
                float got = Vector2.Distance(houses[i].Position, houses[j].Position);

                Assert.GreaterOrEqual(got, need,
                    $"{houses[i].Key} and {houses[j].Key} are {got:0.0} m apart and need {need:0.0} m — " +
                    "two footprints plus a lane to walk down");
            }
        }

        [Test]
        public void NeitherHouseCrowdsTheWorkingRow()
        {
            RequireContract();

            foreach (var house in NineMileCreekFlavour.Houses)
            {
                var p = Placement(house.Key);
                if (!p.IsValid) continue;
                float clear = NineMileCreekFlavour.FootprintRadiusMetres(p) +
                              NineMileCreekBuilder.CreeksideBuildingRadius;

                foreach (var site in NineMileCreekBuilder.CreeksideBuildingSites)
                    Assert.Greater(Vector2.Distance(house.Position, site), clear,
                        $"{house.Key} overlaps the working building at {site}. This is the reason the " +
                        "flavour houses moved west when they stopped being 5 m greybox squares — the " +
                        "kit's footprints are bigger than the sprites they replace");
            }
        }

        [Test]
        public void NeitherHouseStandsInTheArrivalSightlineToTheDory()
        {
            RequireContract();

            foreach (var house in NineMileCreekFlavour.Houses)
            {
                var p = Placement(house.Key);
                if (!p.IsValid) continue;

                Assert.IsTrue(
                    NineMileCreekDory.SightlineIsClear(NineMileCreekDory.HaulOutPos, house.Position,
                                                       NineMileCreekFlavour.FootprintRadiusMetres(p)),
                    $"{house.Key} stands between the arrival point and the dory — §7.2's exit condition " +
                    "is that you SEE her from where you land");
            }
        }

        // ---- 3. every door is turned to the water --------------------------------------------------

        [Test]
        public void EveryDoorFacesTheQuay_WithinHalfACellOfTheKitsResolution()
        {
            RequireContract();

            foreach (var house in NineMileCreekFlavour.Houses)
            {
                var p = Placement(house.Key);
                if (!p.IsValid) continue;

                int facing = NineMileCreekFlavour.FacingFor(p, house);
                Assert.GreaterOrEqual(facing, 0);
                Assert.Less(facing, p.Entry.facings, "a facing the sheet does not have is a blank sprite");

                // 🔴 MEASURED FROM THE BAKE'S OWN DOOR ANCHORS, not re-derived from the formula the
                // placer used. This test used to restate that formula —
                // `−90 + (facing − FrontFacing)·perCell` — so it and the implementation agreed with each
                // other and with nothing else, and both were wrong: cell i is baked at
                // RigBaker.DirForCell, so a door's ground bearing DECREASES as the index rises. Every
                // door in this region and in St Peters was mirrored about the north–south axis and this
                // assert reported 0°. It also took its angle in the SQUASHED world plane, which is out
                // by up to 20° on top of that.
                float perCell = 360f / p.Entry.facings;
                float error = BuildingFacing.DoorErrorDegrees(
                    p.Entry.doorY, p.Entry.pivotY, p.Entry.facings, SpriteLightMath.GroundDepthScale,
                    house.Position, NineMileCreekFlavour.FacingTarget, facing);

                Assert.LessOrEqual(error, perCell * 0.5f + 1e-3f,
                    $"{house.Key}'s door is turned {error:0.#}° away from the quay, which is more than " +
                    "the kit's own resolution — it is pointing at a wall");
            }
        }

        [Test]
        public void TheDoorsPointAtTheWharf_NotAtAPointSomebodyTypedIn()
        {
            Assert.AreEqual(NineMileCreekWharf.DeckFootprint().center, NineMileCreekFlavour.FacingTarget,
                "the houses face the quay by DERIVING it — a typed-in coordinate is a second copy of the " +
                "wharf's geometry, and this region has been bitten by one of those before");
        }
    }
}
