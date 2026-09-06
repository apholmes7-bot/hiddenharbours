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
                Assert.That(def.HullTintStrength, Is.InRange(0f, 1f),
                            $"{path}: HullTintStrength is a 0..1 colour mix (hand-authored YAML can dodge the Range clamp)");

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

        // ---- the hull compass the fleet wears ----------------------------------------------------

        // The CW-from-North compass, by filename suffix — the shape a fleet that authors its OWN facings
        // must spell (art order ↔ heading math). The shipped fleet no longer does: since 2026-09-06 it
        // points at a whole BoatVisualDef instead, for the reason the last test in this file measures.
        private static readonly string[] CompassSuffixes = { "_N", "_NE", "_E", "_SE", "_S", "_SW", "_W", "_NW" };

        [Test]
        public void FleetDefs_HullFacings_AreAllOrNothing_TheFullCompassOfEight()
        {
            foreach (var def in LoadAll())
            {
                string path = AssetDatabase.GetAssetPath(def);
                if (def.HullFacings == null || def.HullFacings.Length == 0) continue;   // fallback rig — fine

                Assert.AreEqual(CompassSuffixes.Length, def.HullFacings.Length,
                    $"{path}: HullFacings is authored, so it must be the FULL 8-way compass — a partial " +
                    "compass would snap into a stale picture mid-turn (the all-or-nothing rule)");
                for (int i = 0; i < def.HullFacings.Length; i++)
                    Assert.IsNotNull(def.HullFacings[i],
                        $"{path}: HullFacings[{i}] is unassigned (broken guid in the hand-authored YAML?) — " +
                        "the presenter would silently fall back to the pre-compass hull");
                Assert.IsTrue(def.HasFullHullCompass(),
                    $"{path}: the presenter's all-or-nothing gate must accept this authored set");
            }
        }

        [Test]
        public void HullCompassGate_IsAllOrNothing_NeverAPartialCompass()
        {
            var def = ScriptableObject.CreateInstance<AmbientFleetDef>();
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 32f);
            try
            {
                def.HullFacings = null;
                Assert.IsFalse(def.HasFullHullCompass(), "null = no compass (fallback rig)");

                def.HullFacings = new Sprite[0];
                Assert.IsFalse(def.HasFullHullCompass(), "empty = no compass (fallback rig)");

                def.HullFacings = new Sprite[CompassSuffixes.Length];
                for (int i = 0; i < def.HullFacings.Length; i++) def.HullFacings[i] = sprite;
                def.HullFacings[5] = null;
                Assert.IsFalse(def.HasFullHullCompass(),
                    "one missing facing = NO compass — never a partial compass that snaps into a stale picture");

                def.HullFacings[5] = sprite;
                Assert.IsTrue(def.HasFullHullCompass(), "the complete set renders directionally");
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void HullCompassGate_PrefersTheSharedVisual_ButOnlyWhenItIsComplete()
        {
            var def = ScriptableObject.CreateInstance<AmbientFleetDef>();
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 32f);
            var visual = ScriptableObject.CreateInstance<BoatVisualDef>();
            try
            {
                // A visual wired but EMPTY must not count as a compass, and must not mask the local
                // facings either: the all-or-nothing rule applied one level up.
                visual.Facings = new Sprite[0];
                def.HullVisual = visual;
                def.HullFacings = new Sprite[0];
                Assert.IsFalse(def.WearsSharedVisual(), "an empty visual is not a compass");
                Assert.IsFalse(def.HasFullHullCompass(), "…and nothing else is authored, so: fallback rig");

                visual.Facings = new Sprite[] { sprite, null, sprite, sprite };
                Assert.IsFalse(def.WearsSharedVisual(), "one unassigned slot = no compass, same as locally");

                // …and a HOLE in the shared visual falls THROUGH to a complete local set rather than
                // dropping the fleet to the fallback rig.
                def.HullFacings = new Sprite[] { sprite, sprite, sprite, sprite };
                Assert.IsTrue(def.HasFullHullCompass(), "the local compass still stands behind it");

                visual.Facings = new Sprite[] { sprite, sprite, sprite, sprite };
                Assert.IsTrue(def.WearsSharedVisual(), "complete = the shared visual wins");
            }
            finally
            {
                Object.DestroyImmediate(visual);
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// <b>⭐ THE ART FACTS MUST TRAVEL WITH THE SPRITES.</b> The fleet used to author its own eight
        /// facings, copied from the owner's hand-drawn plan-view compass. That compass was retired on
        /// 2026-09-06 (Core RetiredContentIds) and the fleet moved onto a shipped hull's — and a facing
        /// compass is not just its pictures. It is the pictures PLUS the sheet's handedness and the
        /// elevation it was baked at, and every hand-exported iso kit in this repo is baked
        /// COUNTER-CLOCKWISE while labelling its cells clockwise.
        ///
        /// <para><b>Why this is a test and not a comment.</b> Hand-copying the sprite refs into
        /// <c>HullFacings</c> would have compiled, loaded, and rendered — and shipped the whole fleet
        /// MIRRORED, because the bare-array path reads its facings as clockwise plan-view art. The error
        /// is drawn-minus-true = −2·heading: exactly ZERO at north and south, 90° at the diagonals and a
        /// full 180° at east and west. A fisher heading north looks perfect. That is why the fleet points
        /// at the whole visual and the runtime copy is built by <c>CreateRuntimeFrom</c>.</para>
        ///
        /// <para>The premise is asserted before the conclusion: if the source hull ever became CW
        /// plan-view art, "the facts were carried" would be true of a copy that carried nothing, and this
        /// test would pass while measuring absolutely nothing.</para>
        /// </summary>
        [Test]
        public void StPetersFleet_WearsAShippedHull_AndTheRuntimeSkinCarriesHerArtFacts()
        {
            foreach (var def in LoadAll())
            {
                if (def.Id != "fleet.st_peters_ambient") continue;

                Assert.IsTrue(def.WearsSharedVisual(),
                    "the St Peters fleet wears a SHIPPED hull's compass (HullVisual), not a hand-copied " +
                    "pile of sprites — see this test's summary for what a hand-copy silently costs");
                var source = def.HullVisual;

                // The fleet is drawn from the same lineage the player's boat is: rig-baked art off a hull
                // that has a mesh (ADR 0022, "all boats will need to be a mesh"). Sprite-only art is what
                // the retirement removed; it must not come back through the fleet's back door.
                Assert.IsTrue(source.HasHullMesh(),
                    $"'{source.Id}' has no hull mesh — the fleet would be back on sprite-only art");

                // ⚠ THE PREMISE. These are the two facts the copy has to carry, and they are only worth
                // carrying because they are NOT the defaults.
                Assert.IsTrue(source.FacingsAreCounterClockwise,
                    $"fixture premise: '{source.Id}' is a hand-exported iso sheet, so her cells run CCW " +
                    "while the bare-array default assumes CW. If she is genuinely CW now (a RigBaker " +
                    "re-bake), this test is no longer measuring anything — point it at a CCW hull.");
                Assert.That(source.ArtBakeElevationDegrees, Is.Not.EqualTo(90f).Within(0.001f),
                    $"fixture premise: '{source.Id}' is a rig bake, so her elevation is not the plan-view " +
                    "default. If it were, a copy that dropped it would look identical.");

                // …and now the conclusion, through the exact call the presenter makes.
                var skin = BoatVisualDef.CreateRuntimeFrom(source, def.HullSortingOrder);

                Assert.AreEqual(source.FacingsAreCounterClockwise, skin.FacingsAreCounterClockwise,
                    "the runtime skin lost the sheet's HANDEDNESS — every fisher is mirrored, and it looks " +
                    "correct heading north and south");
                Assert.AreEqual(source.ArtBakeElevationDegrees, skin.ArtBakeElevationDegrees, 0.001f,
                    "the runtime skin lost the BAKE ELEVATION — everything anchored on the drawn hull " +
                    "(the wake at her transom) drifts as she turns");
                Assert.AreEqual(source.ZeroHeadingDegrees, skin.ZeroHeadingDegrees, 0.001f);
                CollectionAssert.AreEqual(source.Facings, skin.Facings, "the compass itself must be the same art");

                // DECOR TIER: the picture comes across, the machinery does not. Four ambient fishers are
                // not four more player boats.
                Assert.AreEqual(BoatHullVariant.Sprite, skin.Variant,
                    "the fleet's copy is drawn as a SPRITE — a decor caller has no paint scheme to hand a " +
                    "mesh, and its per-boat identity colour is a SpriteRenderer tint");
                Assert.IsNull(skin.HullMesh, "…so the mesh must not come across either");
                Assert.IsFalse(skin.HasRockGrid(), "no rock grid on the decor tier");
                Assert.IsFalse(skin.HasOarSheets(), "no oars on the decor tier");
                Assert.IsFalse(skin.HasMotor(), "no outboard on the decor tier");
                Assert.AreEqual(def.HullSortingOrder, skin.SortingOrder, "the fleet's own sorting order stands");
                return;
            }
            Assert.Fail("the St Peters ambient fleet def (fleet.st_peters_ambient) is missing");
        }
    }
}
