using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A cabin-light switch stands where nothing else takes the press.</b> The switch is on the last
    /// rung of the press, so beside the helm or a door it never answers. The rule moves each switch to a
    /// spot on her cabin wall out of the helm's clearance and every door's reach, at every heading
    /// (<see cref="CabinLightSwitchRule.PlaceClear"/>). Pins the finder on a made-up cabin, that it
    /// invents nothing when no wall has a spot, and that every shipped cabin hull gets at least one
    /// switch on such a spot.
    /// </summary>
    public class CabinLightSwitchClearSpotTests
    {
        const string GameConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const float Elevation = 40f;
        const float HelmClear = GameConfig.DefaultCabinLightSwitchHelmClearanceMetres;

        readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        BoatInteriorDef Cabin(float halfWidth, float halfLength, float soleZ, Vector3 doorAt, float doorWidth)
        {
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();
            _spawned.Add(def);
            def.Levels = new[]
            {
                new BoatInteriorLevel
                {
                    Id = "house_sole",
                    SoleZMeters = soleZ,
                    Outline = new[]
                    {
                        new Vector2(-halfWidth, -halfLength), new Vector2(halfWidth, -halfLength),
                        new Vector2(halfWidth, halfLength), new Vector2(-halfWidth, halfLength),
                    },
                },
            };
            def.Door = new BoatInteriorDoor { Id = "entry", ThresholdPoint = doorAt, ClearWidthMeters = doorWidth };
            return def;
        }

        [Test]
        public void OnARoomyCabin_TheHelmSwitchMovesToAWallSpot_OutOfTheHelmAndTheDoor()
        {
            var helm = new Vector3(0f, 2.5f, 0.5f);
            var door = new Vector3(0f, -3f, 0.5f);
            BoatInteriorDef cabin = Cabin(2f, 3f, 0.5f, door, 0.7f);

            Assert.IsFalse(CabinLightSwitchRule.IsClear(cabin, helm, true, helm, HelmClear, Elevation),
                "premise: at the helm itself the helm takes the press");

            Assert.IsTrue(CabinLightSwitchRule.TryClearSpot(cabin, helm, true, helm, HelmClear, Elevation,
                                                            out Vector3 spot), "a 4 x 6 m cabin has a clear wall");
            Assert.IsTrue(CabinLightSwitchRule.IsClear(cabin, spot, true, helm, HelmClear, Elevation));
            bool onWall = Mathf.Approximately(Mathf.Abs(spot.x), 2f) || Mathf.Approximately(Mathf.Abs(spot.y), 3f);
            Assert.IsTrue(onWall, $"on her wall, not in the middle of the sole: {spot}");
            Assert.AreEqual(0.5f, spot.z, 1e-5f, "at her sole's height, where he stands");

            float reach = BoatInteriorInstaller.ReachMetres(cabin.Door);
            for (int k = 0; k < 8; k++)
            {
                float heading = k * 45f;
                Vector2 at = DeckAreaMath.DeckToWorld(new Vector2(spot.x, spot.y), spot.z, heading, Elevation);
                Vector2 h = DeckAreaMath.DeckToWorld(new Vector2(helm.x, helm.y), helm.z, heading, Elevation);
                Vector2 d = DeckAreaMath.DeckToWorld(new Vector2(door.x, door.y), door.z, heading, Elevation);
                Assert.Greater(Vector2.Distance(at, h), HelmClear, $"out of the helm at heading {heading}");
                Assert.Greater(Vector2.Distance(at, d), reach, $"out of the door's reach at heading {heading}");
            }
        }

        [Test]
        public void ASpotInsideFurniture_IsNeverChosen()
        {
            var helm = new Vector3(0f, 2.5f, 0.5f);
            BoatInteriorDef cabin = Cabin(2f, 3f, 0.5f, new Vector3(0f, -3f, 0.5f), 0.7f);
            Assert.IsTrue(CabinLightSwitchRule.TryClearSpot(cabin, helm, true, helm, HelmClear, Elevation,
                                                            out Vector3 first), "premise");

            // Put a bunk over that spot: the finder must look elsewhere.
            cabin.Levels[0].Obstructions = new[]
            {
                new BoatInteriorObstruction
                {
                    Id = "bunk", Treatment = "waist_block", HeightAboveSoleMeters = 0.5f,
                    Footprint = new[]
                    {
                        new Vector2(first.x - 0.6f, first.y - 0.6f), new Vector2(first.x + 0.6f, first.y - 0.6f),
                        new Vector2(first.x + 0.6f, first.y + 0.6f), new Vector2(first.x - 0.6f, first.y + 0.6f),
                    },
                },
            };
            Assert.IsTrue(CabinLightSwitchRule.TryClearSpot(cabin, helm, true, helm, HelmClear, Elevation,
                                                            out Vector3 second));
            Assert.Greater(Vector2.Distance(new Vector2(first.x, first.y), new Vector2(second.x, second.y)), 0.5f,
                "moved off the bunk");
        }

        [Test]
        public void OnACabinWithNoClearWall_NothingIsInvented()
        {
            var helm = new Vector3(0f, 0f, 0.5f);
            BoatInteriorDef cabin = Cabin(0.4f, 0.4f, 0.5f, new Vector3(0f, -0.4f, 0.5f), 0.6f);

            Assert.IsFalse(CabinLightSwitchRule.TryClearSpot(cabin, helm, true, helm, HelmClear, Elevation,
                                                             out Vector3 spot), "an 0.8 m box is all helm and door");
            Assert.AreEqual(helm, spot, "the switch keeps its rule point");
        }

        [Test]
        public void EveryShippedCabinHull_HasAtLeastOneSwitchWhereItIsAloneInReach()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            Assert.IsNotNull(config, "premise: the shipped config");
            Assert.Greater(config.CabinLightSwitchHelmClearanceMetres, 0.5f,
                "premise: the clearance is above the helm radius, or the helm still takes the press");

            BoatVisualDef[] cabins = AssetDatabase.FindAssets("t:BoatVisualDef", new[] { "Assets/_Project/Data/Boats" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BoatVisualDef>)
                .Where(v => v != null && v.HasInterior())
                .OrderBy(v => v.name, System.StringComparer.Ordinal)
                .ToArray();
            Assert.GreaterOrEqual(cabins.Length, 22, "premise: the kit hulls and the big boats all have cabins");

            var spots = new CabinLightSwitchSpot[CabinLightSwitchRule.MaxSwitchesPerBoat];
            var none = new List<string>();
            var lines = new List<string>();
            var knownSeen = new List<string>();
            foreach (BoatVisualDef v in cabins)
            {
                int n = CabinLightSwitchRule.PlanFor(v, config.CabinLightSwitchDoorDistanceMetres, spots);
                if (n == 0) { none.Add($"{v.name}: no switch at all"); continue; }
                int clear = CabinLightSwitchRule.PlaceClear(v, config.CabinLightSwitchHelmClearanceMetres, spots, n);
                lines.Add($"{v.name}: {clear}/{n} clear" + (n > 0 ? $" ({spots[0].Id} at {spots[0].LocalMetres})" : ""));
                if (clear == 0)
                {
                    if (HullsWithNoClearSpot.Contains(v.name)) knownSeen.Add(v.name);
                    else none.Add($"{v.name}: no wall spot out of the helm and every door");
                }
            }

            TestContext.WriteLine(string.Join("\n", lines));
            Assert.IsEmpty(none, "hulls with no clear switch spot:\n" + string.Join("\n", none));
            CollectionAssert.AreEquivalent(HullsWithNoClearSpot, knownSeen,
                "a hull on the no-clear-spot list now has one (or is gone): take it off the list");
        }

        /// <summary>Hulls whose cabin, as committed, has no wall spot out of the helm and every door: the
        /// switch keeps its rule point and answers only where the helm does not. Named, not fixed with
        /// made-up geometry; each is the data owner's to resolve (a dash switch, or new cabin data).
        /// ExpressCruiserIso: her helm sits 1.4 m from her door in a small cabin.</summary>
        static readonly string[] HullsWithNoClearSpot = { "ExpressCruiserIso" };
    }
}
