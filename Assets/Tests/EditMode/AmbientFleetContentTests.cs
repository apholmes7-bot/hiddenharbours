using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content validation for the ambient fisher fleet's data (canon M2-33; ADR 0003 "content is
    /// data"): every <see cref="AmbientFleetDef"/> under <c>Data/</c> has a stable unique id and sane
    /// owner tunables, the Resources <see cref="AmbientFleetLibrary"/> resolves, and — because the
    /// St Peters asset is hand-authored YAML — its hull-sprite reference actually loads. Follows the
    /// same per-lane pattern as the qa-test ContentValidationTests (each role validates its own Defs).
    /// </summary>
    public class AmbientFleetContentTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        private static List<AmbientFleetDef> LoadAll()
        {
            var list = new List<AmbientFleetDef>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(AmbientFleetDef)}", new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<AmbientFleetDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) list.Add(def);
            }
            return list;
        }

        [Test]
        public void FleetDefs_Exist_AndHaveUniqueStableIds()
        {
            var defs = LoadAll();
            Assert.IsNotEmpty(defs, "the working coast ships at least one AmbientFleetDef in Data/");

            var seen = new HashSet<string>();
            foreach (var def in defs)
            {
                string path = AssetDatabase.GetAssetPath(def);
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Id), $"{path}: blank id");
                StringAssert.StartsWith("fleet.", def.Id, $"{path}: def ids are type.snake_case");
                Assert.IsTrue(seen.Add(def.Id), $"{path}: duplicate fleet id '{def.Id}'");
            }
        }

        [Test]
        public void FleetDefs_HaveSaneOwnerTunables()
        {
            foreach (var def in LoadAll())
            {
                string path = AssetDatabase.GetAssetPath(def);
                Assert.That(def.BoatCount, Is.InRange(1, 8), $"{path}: BoatCount");
                Assert.GreaterOrEqual(def.MaxSpeedMetersPerSecond, def.MinSpeedMetersPerSecond, $"{path}: inverted speed band");
                Assert.Greater(def.MinDepthMeters, 0f, $"{path}: the depth gate needs a positive margin");
                Assert.GreaterOrEqual(def.SpotsPerBoat, 1, $"{path}: a fisher needs at least one spot");
                Assert.Greater(def.SlotsPerDay, 1, $"{path}: the day must divide into slots");
                Assert.Less(def.WorkWindowStartFraction, def.WorkWindowEndFraction, $"{path}: inverted work window");
                Assert.Greater(def.GroundsSize.x * def.GroundsSize.y, 0f, $"{path}: degenerate grounds rect");
                Assert.IsNotNull(def.BuoyPalette, $"{path}: no buoy palette");
                Assert.IsNotEmpty(def.BuoyPalette, $"{path}: buoy colour = whose gear it is — the palette can't be empty");

                // Seamanship gates must keep their ordering or the hysteresis/gating inverts.
                Assert.Greater(def.HoldWakeRepulsion, def.HoldEnterRepulsion,
                               $"{path}: the wake gate must sit ABOVE the enter gate — the gap is the " +
                               "hysteresis that stops a drifting-past player waking a working boat");
                Assert.GreaterOrEqual(def.HeadOnBiasFullDegrees, def.HeadOnBiasBeginDegrees,
                                      $"{path}: the starboard-bias gate ramps up, not down");
            }
        }

        [Test]
        public void FleetDefs_TurnFloorOutrunsTheArriveEase_SoNoSpotIsOrbitable()
        {
            // The no-orbit invariant behind AmbientFleetSteering (owner feedback on #189, "spinning
            // in circles"): approaching a spot, speed sheds with distance (the arrive ease) while the
            // turn rate never drops below the steerage floor — so the turning circle always shrinks
            // faster than the distance left, and no stable orbit exists at ANY radius. That holds
            // only while the fastest cruise stays under ArriveSlowRadius × TurnRate × SteerageTurnFraction.
            foreach (var def in LoadAll())
            {
                string path = AssetDatabase.GetAssetPath(def);
                float turnFloorRadPerSec = def.TurnRateDegreesPerSecond * Mathf.Deg2Rad * def.SteerageTurnFraction;
                Assert.Less(def.MaxSpeedMetersPerSecond, def.ArriveSlowRadius * turnFloorRadPerSec,
                            $"{path}: MaxSpeed must stay under ArriveSlowRadius × TurnRate(rad/s) × " +
                            "SteerageTurnFraction, or a boat can circle a spot she can never quite reach " +
                            "(the owner's 'spinning in circles'). Slow the fleet, quicken the turn rate, " +
                            "raise SteerageTurnFraction, or widen ArriveSlowRadius.");
            }
        }

        [Test]
        public void Library_Resolves_AndListsEveryFleetDef()
        {
            var lib = Resources.Load<AmbientFleetLibrary>(AmbientFleetLibrary.ResourcesPath);
            Assert.IsNotNull(lib, "Resources/AmbientFleetLibrary.asset must exist — the presenter boots from it");
            Assert.IsNotNull(lib.Fleets);

            var listed = new HashSet<AmbientFleetDef>();
            foreach (var def in lib.Fleets)
            {
                Assert.IsNotNull(def, "the library holds a null fleet entry (a broken guid reference?)");
                listed.Add(def);
            }
            foreach (var def in LoadAll())
                Assert.IsTrue(listed.Contains(def),
                              $"{AssetDatabase.GetAssetPath(def)} is not listed in the AmbientFleetLibrary — the fleet would never spawn");
        }

        [Test]
        public void StPetersFleet_HullSpriteReference_ActuallyLoads()
        {
            // The asset is hand-authored YAML: a wrong sprite fileID/guid degrades silently to the
            // greybox wedge at runtime — catch it here instead.
            foreach (var def in LoadAll())
            {
                if (def.Id != "fleet.st_peters_ambient") continue;
                Assert.IsNotNull(def.HullSprite,
                                 "the St Peters fleet ships with the committed Punt sprite — the reference is broken");
                return;
            }
            Assert.Fail("the St Peters ambient fleet def (fleet.st_peters_ambient) is missing");
        }
    }
}
