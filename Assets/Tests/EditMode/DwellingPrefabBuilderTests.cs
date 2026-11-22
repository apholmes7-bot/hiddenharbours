using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ADR 0060 PR 5: the dwelling prefab builder and fixture binding. The verb table agrees with the bake's
    /// vocabulary; the prefab holds one bound fixture per anchor, a wall line open at the door, and no save
    /// row of its own; a site's store keys come from <see cref="HomeStoragePlan"/>; the Def is the deed gate
    /// and the room sidecar agrees with it; kitchen and bath pieces back onto a north, east or west wall as
    /// the lot parks the camper; and the committed prefab is what the builder makes.
    /// </summary>
    public class DwellingPrefabBuilderTests
    {
        const string CamperSite = "ginny_lot_camper";

        /// <summary>Kitchen or bath anchors allowed on a south wall, each with its reason. Empty today.
        /// (Owner 2026-11-22: the north wall is no longer strict; a piece that reads best elsewhere is added
        /// here with why, rather than the test being dropped.)</summary>
        static readonly Dictionary<string, string> SouthWallExceptions = new Dictionary<string, string>();

        static readonly Dictionary<DwellingFixtureVerbs.Binding, Type> ComponentOf = new Dictionary<DwellingFixtureVerbs.Binding, Type>
        {
            { DwellingFixtureVerbs.Binding.Bed, typeof(InteriorBed) },
            { DwellingFixtureVerbs.Binding.CatchFreezer, typeof(CamperFreezer) },
            { DwellingFixtureVerbs.Binding.Stowage, typeof(StowageRack) },
            { DwellingFixtureVerbs.Binding.FoodStore, typeof(KitchenStoreFixture) },
            { DwellingFixtureVerbs.Binding.CookingStation, typeof(CookingStationFixture) },
            { DwellingFixtureVerbs.Binding.WashStation, typeof(WashStationPoint) },
            { DwellingFixtureVerbs.Binding.Seat, typeof(SitPoint) },
        };

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        static IEnumerable<DwellingRigFleet.MeshDwelling> Baked() => DwellingRigFleet.MeshBaked;

        static DwellingMeshDef DefOf(DwellingRigFleet.MeshDwelling d)
        {
            var def = AssetDatabase.LoadAssetAtPath<DwellingMeshDef>(d.MeshAssetPath);
            Assert.That(def, Is.Not.Null, d.MeshAssetPath);
            return def;
        }

        static DwellingMeshDef Clipper()
        {
            Assert.That(DwellingRigFleet.TryGetMeshBaked("clipper", out DwellingRigFleet.MeshDwelling d), Is.True);
            return DefOf(d);
        }

        GameObject Build(DwellingMeshDef def)
        {
            GameObject root = DwellingPrefabBuilder.Build(def, out List<string> problems);
            Assert.That(problems, Is.Empty, string.Join("; ", problems));
            Assert.That(root, Is.Not.Null);
            _made.Add(root);
            return root;
        }

        // ------------------------------------------------------------------------ the verb table

        [Test]
        public void TheVerbTable_IsTheBakesVocabulary_OneRowPerVerb()
        {
            var verbs = DwellingFixtureVerbs.Rows.Select(r => r.Verb).ToList();
            Assert.That(verbs, Is.Unique);
            CollectionAssert.AreEquivalent(DwellingMeshBakeRules.KnownVerbs, verbs,
                "the bake refuses what the builder cannot bind, and the builder binds nothing the bake refuses");
        }

        [Test]
        public void EveryFallbackDef_IsOnDisk_AsTheKindItsBindingNeeds()
        {
            foreach (DwellingFixtureVerbs.Row r in DwellingFixtureVerbs.Rows.Where(r => r.FallbackDefId.Length > 0))
            {
                ScriptableObject found = r.Binding switch
                {
                    DwellingFixtureVerbs.Binding.CookingStation => DwellingPrefabBuilder.Find<CookingStationDef>(r.FallbackDefId, d => d.Id),
                    DwellingFixtureVerbs.Binding.WashStation => DwellingPrefabBuilder.Find<WashStationDef>(r.FallbackDefId, d => d.Id),
                    _ => null,
                };
                Assert.That(found, Is.Not.Null, $"'{r.Verb}' falls back to '{r.FallbackDefId}', which is not shipped as its kind");
            }
        }

        // ------------------------------------------------------------------------ the prefab

        [Test]
        public void TheClipper_BuildsOneBoundFixturePerAnchor()
        {
            DwellingMeshDef def = Clipper();
            GameObject root = Build(def);
            Transform fixtures = root.transform.Find(DwellingPrefabBuilder.FixturesName);
            Assert.That(fixtures, Is.Not.Null);
            Assert.That(fixtures.childCount, Is.EqualTo(def.Anchors.Length));

            var site = root.GetComponent<DwellingSite>();
            Assert.That(site.Anchors.Select(a => a.AnchorId), Is.EquivalentTo(def.Anchors.Select(a => a.AnchorId)));

            foreach (DwellingFixtureAnchor a in def.Anchors)
            {
                Transform child = fixtures.Find(a.AnchorId);
                Assert.That(child, Is.Not.Null, a.AnchorId);
                Assert.That(DwellingFixtureVerbs.TryGet(a.Verb, out DwellingFixtureVerbs.Row row), Is.True);
                if (row.Binding == DwellingFixtureVerbs.Binding.Door)
                    Assert.That(child.GetComponents<MonoBehaviour>(), Is.Empty, "the door is a marker");
                else
                    Assert.That(child.GetComponent(ComponentOf[row.Binding]), Is.Not.Null, $"'{a.AnchorId}' ({a.Verb})");
            }
        }

        [Test]
        public void TheClipperPrefab_DrawsNothingOfItsOwn_AndClaimsNoSaveRow()
        {
            GameObject root = Build(Clipper());
            Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.Empty,
                "the shell, the room and every fixture's shape are the one dwelling mesh, installed in play");
            foreach (StowageRack rack in root.GetComponentsInChildren<StowageRack>(true))
                Assert.That(rack.IsRemembered, Is.False, $"{rack.name}: a prefab never claims a save row");
            Assert.That(root.GetComponent<DwellingSite>().SiteKey, Is.Empty);
        }

        [Test]
        public void TheClipperPrefab_HasAWallLine_OpenAtTheDoor()
        {
            DwellingMeshDef def = Clipper();
            GameObject root = Build(def);
            var walls = root.transform.Find(DwellingPrefabBuilder.WallsName).GetComponent<EdgeCollider2D>();
            Assert.That(walls.enabled, Is.True);
            Assert.That(walls.edgeRadius, Is.EqualTo(DwellingPrefabBuilder.WallRadiusMetres).Within(1e-5f));
            Vector2 first = walls.points[0], last = walls.points[walls.pointCount - 1];
            float gap = Vector2.Distance(first, last);
            // The gap between the jambs is the published door plus the collider's rounded ends (unsquashed
            // at bearing 0: the curb side's run is along the world's y, which the picture squashes).
            float expected = def.Threshold.ClearWidthMeters + 2f * DwellingPrefabBuilder.WallRadiusMetres;
            Assert.That(gap, Is.GreaterThan(expected * Mathf.Sin(def.ElevationDeg * Mathf.Deg2Rad) - 1e-3f));
            Assert.That(gap, Is.LessThanOrEqualTo(expected + 1e-3f));
        }

        [Test]
        public void TheBuilder_RefusesAVerbOutsideTheTable()
        {
            DwellingMeshDef def = Object.Instantiate(Clipper());
            try
            {
                def.Anchors = (DwellingFixtureAnchor[])def.Anchors.Clone();
                def.Anchors[1].Verb = "juggle";
                GameObject root = DwellingPrefabBuilder.Build(def, out List<string> problems);
                Assert.That(root, Is.Null);
                Assert.That(problems.Any(p => p.Contains("juggle")), Is.True, string.Join("; ", problems));
            }
            finally { Object.DestroyImmediate(def); }
        }

        [Test]
        public void EveryFixture_IsWithinReach_OfWhereTheRigStandsABodyToUseIt()
        {
            DwellingMeshDef def = Clipper();
            GameObject root = Build(def);
            Transform fixtures = root.transform.Find(DwellingPrefabBuilder.FixturesName);
            foreach (DwellingFixtureAnchor a in def.Anchors)
            {
                var interactable = fixtures.Find(a.AnchorId).GetComponent<IInteractable>();
                if (interactable == null) continue;   // the door marker and the food stores offer no verb yet
                // Ground metres bound the picture distance from above at every bearing (the squash only shortens).
                float ground = Vector2.Distance(new Vector2(a.PositionMeters.x, a.PositionMeters.y),
                                                new Vector2(a.ReachPointMeters.x, a.ReachPointMeters.y));
                Assert.That(ground, Is.LessThanOrEqualTo(interactable.ReachMeters), $"'{a.AnchorId}'");
            }
        }

        // ------------------------------------------------------------------------ the site and its keys

        [Test]
        public void EveryStore_HasOneSaveKey_AtTheCamperSite_AndNoneElsewhere()
        {
            DwellingMeshDef def = Clipper();
            var keys = new List<string>();
            foreach (DwellingFixtureAnchor a in def.Anchors)
            {
                DwellingFixtureVerbs.TryGet(a.Verb, out DwellingFixtureVerbs.Row row);
                if (!DwellingPrefabBuilder.IsStore(row.Binding)) continue;
                Assert.That(DwellingPrefabBuilder.TrySiteStorageKey(CamperSite, a, out string key, out string problem), Is.True, problem);
                Assert.That(StorageIds.IsStorageId(key), Is.True, key);
                keys.Add(key);
                Assert.That(DwellingPrefabBuilder.TrySiteStorageKey("some_other_lot", a, out _, out _), Is.False,
                            "the key is the site's, never the rig's");
            }
            Assert.That(keys, Is.Unique);
            Assert.That(keys, Has.Count.EqualTo(5), "rod rack, gear locker, freezer, fridge, pantry");
        }

        [Test]
        public void ApplySite_WritesTheSitesKeysAndIds_IntoOneInstance()
        {
            DwellingMeshDef def = Clipper();
            GameObject root = Build(def);
            List<string> problems = DwellingPrefabBuilder.ApplySite(root, CamperSite, 90);
            Assert.That(problems, Is.Empty, string.Join("; ", problems));

            var site = root.GetComponent<DwellingSite>();
            Assert.That(site.SiteKey, Is.EqualTo(CamperSite));
            Assert.That(site.GroundBearingDegrees, Is.EqualTo(90));
            Assert.That(root.GetComponent<BuildingInterior>().Facing, Is.EqualTo(90));

            Transform fixtures = root.transform.Find(DwellingPrefabBuilder.FixturesName);
            foreach (DwellingFixtureAnchor a in def.Anchors.Where(x => x.Verb == "storage"))
            {
                var rack = fixtures.Find(a.AnchorId).GetComponent<StowageRack>();
                DwellingPrefabBuilder.TrySiteStorageKey(CamperSite, a, out string key, out _);
                Assert.That(rack.IsRemembered, Is.True);
                Assert.That(new SerializedObject(rack).FindProperty("_id").stringValue, Is.EqualTo(key));
            }
            Assert.That(fixtures.Find("bunk").GetComponent<InteriorBed>().Id,
                        Is.EqualTo(DwellingPrefabBuilder.FixtureId(CamperSite, "bunk")));
            Assert.That(fixtures.Find("freezer").GetComponent<CamperFreezer>().StorageId, Is.EqualTo(StorageIds.CamperFreezer));
        }

        // ------------------------------------------------------------------------ the deed gate

        [Test]
        public void TheDefIsTheDeedGate_AndTheRoomSidecarAgreesWithIt()
        {
            foreach (DwellingRigFleet.MeshDwelling d in Baked())
            {
                if (string.IsNullOrEmpty(d.RoomSidecarPath)) continue;
                object doc = DeckSidecarJson.Parse(File.ReadAllText(d.RoomSidecarPath));
                List<object> interact = DeckSidecarJson.AsArray(DeckSidecarJson.Member(doc, "INTERACT"));
                Assert.That(interact, Is.Not.Null, d.RoomSidecarPath);
                DwellingMeshDef def = DefOf(d);
                int checkedCount = 0;
                foreach (object entry in interact)
                {
                    string id = DeckSidecarJson.String(DeckSidecarJson.Member(entry, "id"));
                    string sidecarDeed = DeckSidecarJson.String(DeckSidecarJson.Member(entry, "deed_gated")) ?? "";
                    Assert.That(def.TryGetAnchor(id, out DwellingFixtureAnchor a), Is.True, $"{d.Key}: '{id}' is not in the bake");
                    string defDeed = DeedOf(a);
                    if (defDeed == null) { Assert.That(sidecarDeed, Is.Empty, $"{d.Key}: '{id}' is deed gated but has no store Def"); continue; }
                    Assert.That(defDeed, Is.EqualTo(sidecarDeed), $"{d.Key}: '{id}': the Def's DeedHomeId and the sidecar's deed_gated differ");
                    checkedCount++;
                }
                Assert.That(checkedCount, Is.GreaterThan(0), $"{d.Key}: no store was checked");
            }
        }

        /// <summary>The DeedHomeId of the store Def an anchor names, or null when it names no store Def.</summary>
        static string DeedOf(in DwellingFixtureAnchor a)
        {
            DwellingFixtureVerbs.TryGet(a.Verb, out DwellingFixtureVerbs.Row row);
            switch (row.Binding)
            {
                case DwellingFixtureVerbs.Binding.Stowage:
                    return Require(DwellingPrefabBuilder.Find<StowageDef>(a.DefId, x => x.Id), a).DeedHomeId ?? "";
                case DwellingFixtureVerbs.Binding.CatchFreezer:
                    return Require(DwellingPrefabBuilder.Find<FreezerDef>(a.DefId, x => x.Id), a).DeedHomeId ?? "";
                case DwellingFixtureVerbs.Binding.FoodStore:
                    return Require(DwellingPrefabBuilder.Find<KitchenStorageDef>(a.DefId, x => x.Id), a).DeedHomeId ?? "";
                default:
                    return null;
            }
        }

        static T Require<T>(T def, in DwellingFixtureAnchor a) where T : Object
        {
            Assert.That(def, Is.Not.Null, $"'{a.AnchorId}' names '{a.DefId}', which is not on disk");
            return def;
        }

        // ------------------------------------------------------------------------ the wall rule

        [Test]
        public void KitchenAndBath_BackOntoANorthEastOrWestWall_AsTheLotParksTheCamper()
        {
            DwellingMeshDef def = Clipper();
            Vector2 nose = StPetersCamperLot.NoseDirection;
            int bearing = DwellingSiteLayout.NormalizeBearing(Mathf.RoundToInt(Mathf.Atan2(nose.x, nose.y) * Mathf.Rad2Deg));
            Assert.That(DwellingInteriorRules.TryFloor(def, 0, out Vector2[] floor), Is.True);

            int seen = 0;
            foreach (DwellingFixtureAnchor a in def.Anchors)
            {
                DwellingFixtureVerbs.TryGet(a.Verb, out DwellingFixtureVerbs.Row row);
                if (row.Room == DwellingFixtureVerbs.Room.None) continue;
                seen++;
                DwellingSiteLayout.Side side = DwellingSiteLayout.WallSide(floor, new Vector2(a.PositionMeters.x, a.PositionMeters.y), bearing);
                if (side != DwellingSiteLayout.Side.South) continue;
                Assert.That(SouthWallExceptions.ContainsKey(a.AnchorId), Is.True,
                    $"'{a.AnchorId}' ({row.Room}) backs onto the SOUTH wall at bearing {bearing}: move it in the rig, " +
                    "or list it with a reason");
            }
            Assert.That(seen, Is.GreaterThanOrEqualTo(7), "the galley and the wet bath were not found");
        }

        // ------------------------------------------------------------------------ the committed prefab

        [Test]
        public void EveryMeshBakedDwelling_HasItsCommittedPrefab_AsTheBuilderMakesIt()
        {
            foreach (DwellingRigFleet.MeshDwelling d in Baked())
            {
                DwellingMeshDef def = DefOf(d);
                string path = DwellingPrefabBuilder.PrefabPathFor(def);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, $"{path}: run Build the Dwelling Prefabs");

                var interior = prefab.GetComponent<BuildingInterior>();
                Assert.That(interior.DwellingMesh, Is.SameAs(def), "the prefab's interior is this def");
                Assert.That(prefab.GetComponent<DwellingCutaway>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<DwellingSite>(), Is.Not.Null);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Empty, "no renderer is ever serialised");

                GameObject built = Build(def);
                Transform want = built.transform.Find(DwellingPrefabBuilder.FixturesName);
                Transform have = prefab.transform.Find(DwellingPrefabBuilder.FixturesName);
                Assert.That(have.childCount, Is.EqualTo(want.childCount), $"{path} is stale: rebuild it");
                foreach (Transform w in want)
                {
                    Transform h = have.Find(w.name);
                    Assert.That(h, Is.Not.Null, $"{path} lacks '{w.name}': rebuild it");
                    CollectionAssert.AreEqual(w.GetComponents<Component>().Select(c => c.GetType()),
                                              h.GetComponents<Component>().Select(c => c.GetType()), $"{path} '{w.name}'");
                    Assert.That(Vector2.Distance(w.localPosition, h.localPosition), Is.LessThan(1e-3f), $"{path} '{w.name}' moved");
                }
                var wantWalls = built.transform.Find(DwellingPrefabBuilder.WallsName).GetComponent<EdgeCollider2D>();
                var haveWalls = prefab.transform.Find(DwellingPrefabBuilder.WallsName).GetComponent<EdgeCollider2D>();
                Assert.That(haveWalls.pointCount, Is.EqualTo(wantWalls.pointCount), $"{path}: the wall line is stale");
            }
        }
    }
}
