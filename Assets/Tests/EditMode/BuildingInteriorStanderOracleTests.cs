using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE ORACLE</b> for the extraction of the interior recipe out of the two builders and into
    /// <see cref="BuildingInteriorStander"/>.
    ///
    /// <para>There were two copies of the recipe — <c>StPetersInteriors.Stand</c> for village buildings
    /// and <c>ShopPlacement.StandInterior</c> for shops — and they are now one. A refactor like that has
    /// exactly one interesting question: <b>did anything about what gets built change?</b> So the
    /// pre-refactor ORCHESTRATION is transcribed below, verbatim from the commit before the move, and
    /// each test stands the same building twice — once the old way, once the new — and compares the two
    /// object trees against each other.</para>
    ///
    /// <para><b>What the oracle is, and deliberately is not.</b> It is the ~40 lines that were moved:
    /// the order the pieces go up in, the numbers handed to <see cref="BuildingInterior.Configure"/>,
    /// and the wall-building loop. It calls the same production helpers the stander calls
    /// (<see cref="StPetersInteriors.FurnishingsFor"/>, <see cref="StPetersInteriors.Furnish"/>,
    /// <see cref="StPetersInteriors.StandUpperLevel"/>, the two catalogs), because those did not move.
    /// The comparison is therefore precisely "did the orchestration change?" and not a second
    /// implementation of the furniture tables, which would only ever test the transcription.</para>
    ///
    /// <para><b>⚠️ It asserts the oracle actually stood a room BEFORE it compares.</b> Every assertion
    /// here is an equality, and two buildings that both failed to find their art are equal. The room
    /// sheets, their sliced <c>.meta</c>s and both contracts are tracked in the repo and CI pulls them
    /// through LFS, so a missing sprite here means the checkout is broken — and this test says so
    /// rather than reading green having compared nothing.</para>
    ///
    /// <para><b>One deliberate non-identity.</b> The stander falls back to the building's own
    /// <see cref="SpriteRenderer"/> when a caller passes none, which neither old path did (both took the
    /// renderer as a required argument, and every caller passes it). These tests pass the shell
    /// explicitly, so the comparison stays exact.</para>
    /// </summary>
    public class BuildingInteriorStanderOracleTests
    {
        // The sage cottage is the pilot interior: a baked room, six pieces of furniture, and the site
        // that carries the only upper-storey plan in the game. The general store is the shop the
        // fixtures kit was built against — and it is also the key that exists in BOTH kits, which is
        // why the family is an argument and not something the stander infers.
        const string CottageKey = "sageCottage";
        const string ShopUnderTest = "generalStore";

        // A facing that is neither 0 nor the offset itself, so a lost offset shows up as a difference
        // rather than as a coincidence, and an off-origin site so a world/local mix-up in the wall
        // paths cannot cancel out.
        const int ExteriorFacing = 3;
        static readonly Vector3 Site = new Vector3(11.5f, -7.25f, 0f);

        readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>A placed shell at the site, with the renderer every placement pass puts on it.</summary>
        GameObject SpawnShell(string name)
        {
            var go = new GameObject(name);
            go.transform.position = Site;
            go.AddComponent<SpriteRenderer>();
            _spawned.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        // =============================================================================
        //  THE TESTS
        // =============================================================================

        [Test]
        public void TheStanderStandsTheCottageTheOldVillagePathStood()
        {
            GameObject oracle = SpawnShell("oracle-cottage");
            GameObject subject = SpawnShell("subject-cottage");

            bool oracleStood = Oracle.StandVillageBuilding(
                oracle, oracle.GetComponent<SpriteRenderer>(), CottageKey, ExteriorFacing,
                occupant: null, upperPlanKey: null);

            StandResult result = BuildingInteriorStander.Stand(
                subject, CottageKey, InteriorFamily.VillageBuilding, ExteriorFacing,
                occupant: null, upperPlanKey: null, shell: subject.GetComponent<SpriteRenderer>());

            Assert.IsTrue(oracleStood, "the OLD path must stand the sage cottage — if it did not, the " +
                                       "art or the contract is missing and every comparison below is " +
                                       "vacuous");
            AssertTheOracleActuallyStoodARoom(oracle, "the sage cottage", minFurniture: 1);

            Assert.IsTrue(result.Enterable, "the stander must reach the same verdict as the old path");
            AssertSameBuild(oracle, subject, "the sage cottage");
            AssertTheResultDescribesWhatItBuilt(subject, result, expectUpperLevel: false);
        }

        [Test]
        public void TheStanderStandsTheStoreyAboveTheOldVillagePathStood()
        {
            // The one site in the game with an upper-level plan. It is keyed by SITE, not by build, so
            // it only ever arrives as this argument — which makes it exactly the sort of thing a move
            // drops silently.
            GameObject oracle = SpawnShell("oracle-ginny");
            GameObject subject = SpawnShell("subject-ginny");

            bool oracleStood = Oracle.StandVillageBuilding(
                oracle, oracle.GetComponent<SpriteRenderer>(), CottageKey, ExteriorFacing,
                occupant: null, upperPlanKey: StPetersInteriors.GinnyCottagePlanKey);

            StandResult result = BuildingInteriorStander.Stand(
                subject, CottageKey, InteriorFamily.VillageBuilding, ExteriorFacing,
                occupant: null, upperPlanKey: StPetersInteriors.GinnyCottagePlanKey,
                shell: subject.GetComponent<SpriteRenderer>());

            Assert.IsTrue(oracleStood, "the OLD path must stand Ginny's cottage");
            AssertTheOracleActuallyStoodARoom(oracle, "Ginny's cottage", minFurniture: 1);
            Assert.IsNotNull(oracle.transform.Find(BuildingInteriorStander.UpperRoomChildName),
                             "the OLD path must put a storey above Ginny's cottage — without one this " +
                             "test compares two single-storey buildings and proves nothing about the " +
                             "upper level");

            Assert.IsTrue(result.Enterable);
            AssertSameBuild(oracle, subject, "Ginny's cottage");
            AssertTheResultDescribesWhatItBuilt(subject, result, expectUpperLevel: true);
        }

        [Test]
        public void TheStanderStandsTheShopFloorTheOldShopPathStood()
        {
            GameObject oracle = SpawnShell("oracle-shop");
            GameObject subject = SpawnShell("subject-shop");

            bool oracleStood = Oracle.StandShopInterior(
                oracle, oracle.GetComponent<SpriteRenderer>(), ShopUnderTest, ExteriorFacing,
                occupant: null, logPrefix: "[oracle]");

            StandResult result = BuildingInteriorStander.Stand(
                subject, ShopUnderTest, InteriorFamily.Shop, ExteriorFacing,
                occupant: null, upperPlanKey: null, shell: subject.GetComponent<SpriteRenderer>());

            Assert.IsTrue(oracleStood, "the OLD shop path must stand the general store's trading floor");
            AssertTheOracleActuallyStoodARoom(oracle, "the general store", minFurniture: 0);

            Assert.IsTrue(result.Enterable);
            AssertSameBuild(oracle, subject, "the general store");
            AssertTheResultDescribesWhatItBuilt(subject, result, expectUpperLevel: false);

            // A shop's fixtures are their own kit and their own pass. The trading floor stands empty,
            // and the result has to say so rather than reporting the village's furniture count.
            Assert.AreEqual(0, result.FurnitureCount, "a shop's floor is furnished by the fixtures kit");
            Assert.IsNull(subject.transform.Find(BuildingInteriorStander.PropsChildName),
                          "a shop must not get a Furniture root");
        }

        [Test]
        public void StandingTwiceLeavesOneOfEverything()
        {
            // The property that made ClearExisting the first thing the recipe does, and the one most
            // easily lost in a move: the second call has to find and destroy the first call's work
            // BEFORE it looks the room up, or a world creator who turns a placed building gets two
            // rooms, two sets of walls and doubled furniture.
            GameObject once = SpawnShell("stood-once");
            GameObject twice = SpawnShell("stood-twice");

            BuildingInteriorStander.Stand(once, CottageKey, InteriorFamily.VillageBuilding,
                                          ExteriorFacing, shell: once.GetComponent<SpriteRenderer>());

            BuildingInteriorStander.Stand(twice, CottageKey, InteriorFamily.VillageBuilding,
                                          ExteriorFacing, shell: twice.GetComponent<SpriteRenderer>());
            BuildingInteriorStander.Stand(twice, CottageKey, InteriorFamily.VillageBuilding,
                                          ExteriorFacing, shell: twice.GetComponent<SpriteRenderer>());

            AssertTheOracleActuallyStoodARoom(once, "the sage cottage stood once", minFurniture: 1);
            AssertSameBuild(once, twice, "the sage cottage stood twice");
            Assert.AreEqual(1, twice.GetComponents<BuildingInterior>().Length,
                            "a second stand must replace the BuildingInterior, not add another");
        }

        // =============================================================================
        //  THE COMPARISON
        // =============================================================================

        /// <summary>
        /// The oracle built a real room out of real pixels. Every other assertion in this fixture is an
        /// equality, and two buildings that both found nothing are equal — so this runs first.
        /// </summary>
        static void AssertTheOracleActuallyStoodARoom(GameObject oracle, string what, int minFurniture)
        {
            Transform room = oracle.transform.Find(BuildingInteriorStander.RoomChildName);
            Assert.IsNotNull(room, $"{what}: the OLD path stood no room child. The sheets and both " +
                                   "contracts are tracked in the repo — a missing room here means the " +
                                   "checkout has no LFS content, not that the refactor is correct.");

            var renderer = room.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, $"{what}: the room child carries no renderer");
            Assert.IsNotNull(renderer.sprite, $"{what}: the room's sheet is missing or unsliced — the " +
                                              "comparison below would be between two empty rooms");

            Transform walls = oracle.transform.Find(BuildingInteriorStander.WallsChildName);
            Assert.IsNotNull(walls, $"{what}: no walls went up");
            Assert.GreaterOrEqual(walls.GetComponents<PolygonCollider2D>().Length, 4,
                                  $"{what}: a walled room is at least four quads");

            var interior = oracle.GetComponent<BuildingInterior>();
            Assert.IsNotNull(interior, $"{what}: no BuildingInterior was configured");
            Assert.Greater(interior.Footprint.WidthMetres, 0f, $"{what}: the floor has no width");
            Assert.Greater(interior.Footprint.LengthMetres, 0f, $"{what}: the floor has no length");

            if (minFurniture <= 0) return;

            Transform props = oracle.transform.Find(BuildingInteriorStander.PropsChildName);
            Assert.IsNotNull(props, $"{what}: no furniture root");
            Assert.GreaterOrEqual(props.childCount, minFurniture,
                                  $"{what}: the furnishing table asks for furniture and none was " +
                                  "placed — the prop sheets are missing, so the furniture comparison " +
                                  "would be 0 against 0");
        }

        /// <summary>Same children, same components, same places, same colliders, same behaviour.</summary>
        static void AssertSameBuild(GameObject oracle, GameObject subject, string what)
        {
            // --- the root's own components: the BuildingInterior, and nothing else new.
            AssertSameComponents(oracle.transform, subject.transform, $"{what}: the building root");

            // --- the children, in order. Order matters: it is the order the recipe builds in, and a
            //     reordered tree is a reordered recipe.
            CollectionAssert.AreEqual(
                ChildNames(oracle.transform), ChildNames(subject.transform),
                $"{what}: the child objects the two paths stood, in order");

            for (int i = 0; i < oracle.transform.childCount; i++)
                AssertSameSubtree(oracle.transform.GetChild(i), subject.transform.GetChild(i),
                                  $"{what}: {oracle.transform.GetChild(i).name}");

            AssertSameWalls(oracle, subject, BuildingInteriorStander.WallsChildName, what);
            AssertSameWalls(oracle, subject, BuildingInteriorStander.UpperWallsChildName, what);
            AssertSameFurniture(oracle, subject, BuildingInteriorStander.PropsChildName, what);
            AssertSameFurniture(oracle, subject, BuildingInteriorStander.UpperPropsChildName, what);
            AssertSameInterior(oracle.GetComponent<BuildingInterior>(),
                               subject.GetComponent<BuildingInterior>(), what);
        }

        static void AssertSameSubtree(Transform oracle, Transform subject, string path)
        {
            Assert.AreEqual(oracle.name, subject.name, $"{path}: the object's name");
            AssertSameComponents(oracle, subject, path);
            AssertSameVector(oracle.localPosition, subject.localPosition, $"{path}: local position");
            AssertSameVector(oracle.localScale, subject.localScale, $"{path}: local scale");

            CollectionAssert.AreEqual(ChildNames(oracle), ChildNames(subject),
                                      $"{path}: its own children, in order");
            for (int i = 0; i < oracle.childCount; i++)
                AssertSameSubtree(oracle.GetChild(i), subject.GetChild(i),
                                  $"{path}/{oracle.GetChild(i).name}");
        }

        static void AssertSameComponents(Transform oracle, Transform subject, string path)
        {
            CollectionAssert.AreEqual(ComponentNames(oracle), ComponentNames(subject),
                                      $"{path}: the components on it");
        }

        /// <summary>
        /// The walls, path by path and point by point. The component-set comparison already counts the
        /// colliders; this is what they enclose. ⚠️ <see cref="PolygonCollider2D.SetPath"/> takes
        /// COLLIDER-LOCAL points, and handing it world ones puts the walls a village away — silently,
        /// because nothing about a collider is drawn. Both buildings stand at the same off-origin site,
        /// so a local/world slip in either path shows up here as a difference of exactly the site.
        /// </summary>
        static void AssertSameWalls(GameObject oracle, GameObject subject, string childName, string what)
        {
            Transform o = oracle.transform.Find(childName);
            Transform s = subject.transform.Find(childName);
            Assert.AreEqual(o == null, s == null, $"{what}: one path stood '{childName}' and the other " +
                                                  "did not");
            if (o == null) return;

            PolygonCollider2D[] oc = o.GetComponents<PolygonCollider2D>();
            PolygonCollider2D[] sc = s.GetComponents<PolygonCollider2D>();
            Assert.AreEqual(oc.Length, sc.Length, $"{what}: the number of '{childName}' colliders");

            for (int i = 0; i < oc.Length; i++)
            {
                Assert.AreEqual(oc[i].pathCount, sc[i].pathCount,
                                $"{what}: '{childName}' collider {i} path count — several paths on one " +
                                "collider turn their overlaps into HOLES");
                for (int p = 0; p < oc[i].pathCount; p++)
                {
                    Vector2[] op = oc[i].GetPath(p);
                    Vector2[] sp = sc[i].GetPath(p);
                    Assert.AreEqual(op.Length, sp.Length,
                                    $"{what}: '{childName}' collider {i} path {p} point count");
                    for (int v = 0; v < op.Length; v++)
                        AssertSameVector(op[v], sp[v],
                                         $"{what}: '{childName}' collider {i} path {p} point {v}");
                }
            }
        }

        /// <summary>The furniture: how many pieces, which ones, and where. The prop objects are named
        /// for their label and their room coordinate, so a name comparison catches a moved chair.</summary>
        static void AssertSameFurniture(GameObject oracle, GameObject subject, string childName,
                                        string what)
        {
            Transform o = oracle.transform.Find(childName);
            Transform s = subject.transform.Find(childName);
            Assert.AreEqual(o == null, s == null, $"{what}: one path stood '{childName}' and the other " +
                                                  "did not");
            if (o == null) return;

            Assert.AreEqual(o.childCount, s.childCount, $"{what}: the number of pieces in '{childName}'");
            CollectionAssert.AreEqual(ChildNames(o), ChildNames(s),
                                      $"{what}: which pieces went into '{childName}', in order");

            for (int i = 0; i < o.childCount; i++)
                AssertSameVector(o.GetChild(i).position, s.GetChild(i).position,
                                 $"{what}: '{childName}' piece '{o.GetChild(i).name}' stands in the " +
                                 "same place");
        }

        /// <summary>Every value the recipe hands <see cref="BuildingInterior.Configure"/>, read back
        /// through the component's own surface — which is what the runtime uses.</summary>
        static void AssertSameInterior(BuildingInterior oracle, BuildingInterior subject, string what)
        {
            Assert.IsNotNull(oracle, $"{what}: the OLD path configured no BuildingInterior");
            Assert.IsNotNull(subject, $"{what}: the stander configured no BuildingInterior");

            AssertSameFootprint(oracle.Footprint, subject.Footprint, $"{what}: the ground floor");

            Assert.AreEqual(oracle.WallThicknessMetres, subject.WallThicknessMetres, 1e-5f,
                            $"{what}: the wall thickness the inside test uses");
            Assert.AreEqual(oracle.DoorwayWidthMetres, subject.DoorwayWidthMetres, 1e-5f,
                            $"{what}: the doorway width");
            AssertSameVector(oracle.DoorWorld, subject.DoorWorld, $"{what}: the threshold");

            Assert.AreEqual(oracle.HasUpperLevel, subject.HasUpperLevel,
                            $"{what}: whether there is a storey above");
            Assert.AreEqual(oracle.TopLevel, subject.TopLevel, $"{what}: the top level");

            if (!oracle.HasUpperLevel) return;

            Assert.AreEqual(oracle.UpperLevelY, subject.UpperLevelY, 1e-5f,
                            $"{what}: how high the storey above sits — read from the rig's declared " +
                            "storey, never typed");
            AssertSameFootprint(oracle.FootprintFor(oracle.TopLevel),
                                subject.FootprintFor(subject.TopLevel), $"{what}: the storey above");
        }

        static void AssertSameFootprint(InteriorFootprint oracle, InteriorFootprint subject, string what)
        {
            AssertSameVector(oracle.Centre, subject.Centre, $"{what}: centre");
            Assert.AreEqual(oracle.WidthMetres, subject.WidthMetres, 1e-5f, $"{what}: width");
            Assert.AreEqual(oracle.LengthMetres, subject.LengthMetres, 1e-5f, $"{what}: length");
            Assert.AreEqual(oracle.Facing, subject.Facing,
                            $"{what}: the facing the ROOM is drawn at — the shell's facing plus the " +
                            "contract's MEASURED offset, which is 4 for the house family and 0 for shops");
            Assert.AreEqual(oracle.Facings, subject.Facings, $"{what}: how many facings the kit baked");
            Assert.AreEqual(oracle.DepthScale, subject.DepthScale, 1e-6f,
                            $"{what}: the ¾-view squash");
            Assert.AreEqual(oracle.DoorSign, subject.DoorSign, 1e-6f,
                            $"{what}: which wall the doorway is cut in");
            Assert.AreEqual(oracle.DoorAcrossMetres, subject.DoorAcrossMetres, 1e-5f,
                            $"{what}: how far the doorway sits off that wall's centre");
        }

        /// <summary>The stander returns a description of what it built; it has to be true of the object
        /// it left behind, because PR 2's authoring component reports from it.</summary>
        static void AssertTheResultDescribesWhatItBuilt(GameObject built, in StandResult result,
                                                        bool expectUpperLevel)
        {
            var interior = built.GetComponent<BuildingInterior>();
            Assert.IsNotNull(interior, "an enterable result must leave a BuildingInterior behind");

            Assert.AreEqual(interior.Footprint.Facing, result.InteriorFacing,
                            "the result's interior facing must be the one the footprint was built at");
            Assert.AreEqual(interior.Footprint.WidthMetres, result.WidthMetres, 1e-5f);
            Assert.AreEqual(interior.Footprint.LengthMetres, result.LengthMetres, 1e-5f);
            Assert.AreEqual(expectUpperLevel, result.HasUpperLevel,
                            "the result must report the storey above");
            Assert.AreEqual(interior.HasUpperLevel, result.HasUpperLevel);
            Assert.IsNotNull(result.RoomKey, "an enterable result names the room it stood");
            Assert.IsNull(result.Reason, "an enterable result has nothing to explain");

            int placed = CountChildren(built, BuildingInteriorStander.PropsChildName) +
                         CountChildren(built, BuildingInteriorStander.UpperPropsChildName);
            Assert.AreEqual(placed, result.FurnitureCount,
                            "the result's furniture count must be the furniture actually standing in " +
                            "the building, both storeys");
        }

        // -----------------------------------------------------------------------------

        static int CountChildren(GameObject go, string childName)
        {
            Transform t = go.transform.Find(childName);
            return t == null ? 0 : t.childCount;
        }

        static string[] ChildNames(Transform t) =>
            Enumerable.Range(0, t.childCount).Select(i => t.GetChild(i).name).ToArray();

        static string[] ComponentNames(Transform t) =>
            t.GetComponents<Component>().Select(c => c.GetType().Name).OrderBy(n => n).ToArray();

        static void AssertSameVector(Vector2 oracle, Vector2 subject, string what)
        {
            Assert.AreEqual(oracle.x, subject.x, 1e-4f, $"{what} (x)");
            Assert.AreEqual(oracle.y, subject.y, 1e-4f, $"{what} (y)");
        }

        static void AssertSameVector(Vector3 oracle, Vector3 subject, string what)
        {
            Assert.AreEqual(oracle.x, subject.x, 1e-4f, $"{what} (x)");
            Assert.AreEqual(oracle.y, subject.y, 1e-4f, $"{what} (y)");
            Assert.AreEqual(oracle.z, subject.z, 1e-4f, $"{what} (z)");
        }

        // =============================================================================
        //  THE ORACLE — the pre-refactor orchestration, transcribed
        // =============================================================================

        /// <summary>
        /// What the two builders did before <see cref="BuildingInteriorStander"/> existed, copied from
        /// the commit before the move. Do not tidy it, do not share code between its two halves and do
        /// not make it call the stander: the duplication below IS the thing under test, and an oracle
        /// that has been refactored towards its subject measures nothing.
        ///
        /// <para>The child-name and dimension constants are read from the stander rather than retyped —
        /// they are the values the old code compiled against (the builders' own constants forward to
        /// them now), and retyping them here would test the transcription rather than the recipe.</para>
        /// </summary>
        static class Oracle
        {
            // ---- StPetersInteriors.Stand, as it stood ------------------------------------------
            public static bool StandVillageBuilding(GameObject buildingGo, SpriteRenderer shell,
                                                    string buildingKey, int exteriorFacing,
                                                    Transform occupant, string upperPlanKey)
            {
                if (buildingGo == null) return false;

                ClearExisting(buildingGo.transform);

                InteriorCatalog.Placement room = InteriorCatalog.FindRoom(buildingKey);
                if (!room.IsValid) return false;

                int interiorFacing = InteriorCatalog.InteriorFacingFor(exteriorFacing);
                Sprite roomSprite = InteriorCatalog.LoadFacing(room, interiorFacing);
                if (roomSprite == null) return false;

                var roomGo = new GameObject(BuildingInteriorStander.RoomChildName);
                roomGo.transform.SetParent(buildingGo.transform, worldPositionStays: false);
                roomGo.transform.localPosition = Vector3.zero;
                SpriteRenderer roomRenderer = InteriorCatalog.ConfigureRoom(roomGo, room, roomSprite);

                Vector2 door = InteriorCatalog.DoorModelMetres(room);
                float doorSign = door.y >= 0f ? 1f : -1f;

                var footprint = new InteriorFootprint(
                    buildingGo.transform.position,
                    room.Entry.footprintWidthMetres, room.Entry.footprintLengthMetres,
                    interiorFacing, room.Entry.facings, SpriteLightMath.GroundDepthScale,
                    doorSign, door.x);

                BuildWalls(buildingGo.transform, footprint);

                var propsGo = new GameObject(BuildingInteriorStander.PropsChildName);
                propsGo.transform.SetParent(buildingGo.transform, worldPositionStays: false);
                propsGo.transform.localPosition = Vector3.zero;
                StPetersInteriors.Furnish(propsGo.transform,
                                          StPetersInteriors.FurnishingsFor(room.Entry.key),
                                          room.Entry.key, footprint, interiorFacing, room.Entry.facings);

                var interior = buildingGo.AddComponent<BuildingInterior>();
                interior.Configure(shell, roomRenderer, propsGo.transform,
                                   room.Entry.footprintWidthMetres, room.Entry.footprintLengthMetres,
                                   interiorFacing, room.Entry.facings,
                                   SpriteLightMath.GroundDepthScale,
                                   BuildingInteriorStander.WallThicknessMetres,
                                   BuildingInteriorStander.DoorwayWidthMetres,
                                   doorOnPlusY: doorSign > 0f, doorAcrossMetres: door.x);
                interior.SetOccupant(occupant);

                StPetersInteriors.StandUpperLevel(buildingGo, interior, room, roomSprite, footprint,
                                                  interiorFacing, propsGo.transform, upperPlanKey);
                return true;
            }

            // ---- ShopPlacement.StandInterior, as it stood ---------------------------------------
            public static bool StandShopInterior(GameObject shopGo, SpriteRenderer shellRenderer,
                                                 string key, int exteriorFacing, Transform occupant,
                                                 string logPrefix)
            {
                if (shopGo == null) return false;

                ClearExisting(shopGo.transform);

                var level = ShopCatalog.FindLevel(key, ShopKit.GroundLevel);
                if (!level.IsValid) return false;

                int interiorFacing = ShopCatalog.InteriorFacingFor(exteriorFacing);
                Sprite roomSprite = ShopCatalog.LoadFacing(level, interiorFacing);
                if (roomSprite == null) return false;

                var roomGo = new GameObject(BuildingInteriorStander.RoomChildName);
                roomGo.transform.SetParent(shopGo.transform, worldPositionStays: false);
                roomGo.transform.localPosition = Vector3.zero;
                SpriteRenderer roomRenderer = ShopCatalog.Configure(
                    roomGo, level, roomSprite, ShopCatalog.RoomSortingOrder, ySort: false);

                Vector2 door = ShopCatalog.DoorModelMetres(level);
                float doorSign = door.y >= 0f ? 1f : -1f;

                var footprint = new InteriorFootprint(
                    shopGo.transform.position,
                    level.FootprintMetres.x, level.FootprintMetres.y,
                    interiorFacing, level.Entry.facings, SpriteLightMath.GroundDepthScale,
                    doorSign, door.x);

                BuildWalls(shopGo.transform, footprint);

                var interior = shopGo.AddComponent<BuildingInterior>();
                interior.Configure(shellRenderer, roomRenderer, props: null,
                                   level.FootprintMetres.x, level.FootprintMetres.y,
                                   interiorFacing, level.Entry.facings,
                                   SpriteLightMath.GroundDepthScale,
                                   BuildingInteriorStander.WallThicknessMetres,
                                   BuildingInteriorStander.DoorwayWidthMetres,
                                   doorOnPlusY: doorSign > 0f, doorAcrossMetres: door.x);
                interior.SetOccupant(occupant);
                return true;
            }

            // ---- the two helpers both copies carried -------------------------------------------
            static void ClearExisting(Transform buildingRoot)
            {
                var existing = buildingRoot.GetComponent<BuildingInterior>();
                if (existing != null) Object.DestroyImmediate(existing);

                for (int i = buildingRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = buildingRoot.GetChild(i);
                    if (child.name == BuildingInteriorStander.RoomChildName ||
                        child.name == BuildingInteriorStander.PropsChildName ||
                        child.name == BuildingInteriorStander.WallsChildName ||
                        child.name == BuildingInteriorStander.UpperRoomChildName ||
                        child.name == BuildingInteriorStander.UpperPropsChildName ||
                        child.name == BuildingInteriorStander.UpperWallsChildName)
                        Object.DestroyImmediate(child.gameObject);
                }
            }

            static void BuildWalls(Transform buildingRoot, in InteriorFootprint footprint)
            {
                var wallsGo = new GameObject(BuildingInteriorStander.WallsChildName);
                wallsGo.transform.SetParent(buildingRoot, worldPositionStays: false);
                wallsGo.transform.localPosition = Vector3.zero;

                Vector2 origin = buildingRoot.position;
                foreach (Vector2[] quad in footprint.WallQuads(
                             BuildingInteriorStander.WallThicknessMetres,
                             BuildingInteriorStander.DoorwayWidthMetres))
                {
                    var collider = wallsGo.AddComponent<PolygonCollider2D>();
                    collider.pathCount = 1;

                    var local = new Vector2[quad.Length];
                    for (int i = 0; i < quad.Length; i++) local[i] = quad[i] - origin;
                    collider.SetPath(0, local);
                }
            }
        }
    }
}
