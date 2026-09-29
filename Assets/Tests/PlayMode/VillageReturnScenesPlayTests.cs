using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>Drop 14's hand edits to the committed scenes LOAD, and say what the bakes say</b> (#898).
    ///
    /// <para>Neither region can be rebuilt — a rebuild wipes the hand-authored layer — so what the village
    /// return changed reaches the scenes by hand in the YAML, and this is the proof of it:</para>
    /// <list type="bullet">
    /// <item><b>The houses light</b> ("L2, houses first"): every placed house draws with
    /// <c>LitVillageBuilding</c> and hands the shader its own mask, normal and emitter sheets, on the
    /// albedo's pixels, with the response and the glow on — and every other village building (the wharf
    /// buildings, whose sheets did not move) draws exactly as it did.</item>
    /// <item><b>The rooms open where their houses do.</b> The returned room rig draws its doorway on the
    /// gable its shell's door is on, so its rooms register at offset 0 where today's registered at 4, and
    /// each placed room's sprite was re-pointed to the cell the contract now names. Its walls, doorway,
    /// furniture and beds did not move, and did not need to: the room is the same rectangle turned half a
    /// turn. What this checks is the one thing the re-point could get wrong — that the doorway the room
    /// is DRAWN with lands in the gap its walls leave.</item>
    /// </list>
    ///
    /// <para>Everything is read off the bakes' own contracts (<c>Buildings.json</c>,
    /// <c>Interiors.json</c>) rather than restated, so each test holds before the re-bake and after it,
    /// and fails on the one state in between: sheets re-baked, scene not re-pointed.</para>
    /// </summary>
    public class VillageReturnScenesPlayTests
    {
        const string BuildingsContractPath = "_Project/Art/Sprites/Buildings/Village/Buildings.json";
        const string InteriorsContractPath = "_Project/Art/Sprites/Interiors/Interiors.json";
        const string VillageSheetPrefix = "Village_";
        const string RoomSheetPrefix = "Interior_";
        const string HouseRig = "house";
        const string LitMaterialName = "LitVillageBuilding";

        /// <summary>The two fields of the village contract these tests read.</summary>
        [Serializable] public sealed class Buildings { public Building[] buildings; }

        [Serializable] public sealed class Building { public string key; public string rig; }

        /// <summary>The fields of the interiors contract these tests read: the registration, and each
        /// room's floor centre and door anchor per facing, in cropped-cell px from the cell's top-left.</summary>
        [Serializable] public sealed class Interiors
        {
            public float ppu;
            public int facings;
            public int exteriorFacingOffset;
            public Room[] rooms;
        }

        [Serializable] public sealed class Room
        {
            public string key;
            public float pivotX, pivotY;
            public float[] doorX, doorY;
        }

        static readonly Regex CellSuffix = new Regex(@"_d(\d+)$");

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        string _loaded;

        static Scene PersistentScene()
        {
            var probe = new GameObject("__ddolProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s;
        }

        static List<GameObject> PersistentRoots()
        {
            Scene s = PersistentScene();
            return s.IsValid() ? s.GetRootGameObjects().ToList() : new List<GameObject>();
        }

        [UnitySetUp]
        public IEnumerator SetUpRegion()
        {
            _residentBefore.Clear();
            foreach (GameObject go in PersistentRoots()) _residentBefore.Add(go);
            _loaded = null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownRegion()
        {
            LogAssert.ignoreFailingMessages = false;

            // Put the world back: the region, and everything its load promoted out of it (the same
            // cleanup StPetersEastDoorPlayTests does, for the same reasons).
            var clean = SceneManager.CreateScene("VillageReturnScenesCleanup");
            SceneManager.SetActiveScene(clean);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s != clean && s.name == _loaded)
                    yield return SceneManager.UnloadSceneAsync(s);
            }

            foreach (GameObject go in PersistentRoots())
                if (go != null && !_residentBefore.Contains(go))
                    Object.DestroyImmediate(go);

            GameServices.Reset();
            yield return null;
        }

        IEnumerator Load(string sceneName)
        {
            // A region logs unrelated decor complaints; a stray one must not fail a claim about its houses.
            LogAssert.ignoreFailingMessages = true;
            _loaded = sceneName;
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            for (int i = 0; i < 4; i++) yield return null;
            LogAssert.ignoreFailingMessages = false;
        }

        // ---- the houses light ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryHouseInStPeters_Lights_OnItsOwnSheets() =>
            EveryHouseLights("StPeters", expectedHouses: 5, expectedOthers: 4);

        [UnityTest]
        public IEnumerator EveryHouseInNineMileCreek_Lights_OnItsOwnSheets() =>
            EveryHouseLights("NineMileCreek", expectedHouses: 2, expectedOthers: 0);

        IEnumerator EveryHouseLights(string sceneName, int expectedHouses, int expectedOthers)
        {
            Dictionary<string, string> rigOf = ReadContract<Buildings>(BuildingsContractPath).buildings
                .ToDictionary(e => e.key, e => e.rig);
            yield return Load(sceneName);

            var failures = new List<string>();
            int houses = 0, others = 0;
            var block = new MaterialPropertyBlock();
            foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(
                         FindObjectsInactive.Include))
            {
                Texture2D albedo = sr.sprite != null ? sr.sprite.texture : null;
                if (albedo == null || !albedo.name.StartsWith(VillageSheetPrefix, StringComparison.Ordinal))
                    continue;
                string key = albedo.name.Substring(VillageSheetPrefix.Length);
                if (!rigOf.TryGetValue(key, out string rig)) continue;   // a channel sheet or another kit's

                string who = $"{sr.gameObject.name} ({albedo.name})";
                var binder = sr.GetComponent<SpriteLightBinder>();
                string material = sr.sharedMaterial != null ? sr.sharedMaterial.name : "none";

                if (rig != HouseRig)
                {
                    others++;
                    if (binder != null) failures.Add($"{who}: a {rig} has no channels, yet carries a binder");
                    if (material == LitMaterialName) failures.Add($"{who}: a {rig} draws with the lit house material");
                    continue;
                }

                houses++;
                if (material != LitMaterialName) failures.Add($"{who}: draws with '{material}', not {LitMaterialName}");
                if (binder == null)
                {
                    failures.Add($"{who}: no SpriteLightBinder");
                    continue;
                }

                sr.GetPropertyBlock(block);
                CheckSheet(block, SpriteLightBinding.MaskProperty, albedo, "_mask", who, failures);
                CheckSheet(block, SpriteLightBinding.NormalProperty, albedo, "_normal", who, failures);
                CheckSheet(block, SpriteLightBinding.EmitProperty, albedo, "_emit", who, failures);
                if (block.GetFloat(SpriteLightBinding.ChannelsProperty) != 1f)
                    failures.Add($"{who}: the light response is off");
                if (block.GetFloat(SpriteLightBinding.EmitChannelsProperty) != 1f)
                    failures.Add($"{who}: the window glow is off");
            }

            Assert.IsEmpty(failures, $"{sceneName}: a placed village building does not light as its bake says:\n  " +
                                     string.Join("\n  ", failures));
            Assert.AreEqual(expectedHouses, houses,
                            $"{sceneName} places {houses} house(s); this test was written against {expectedHouses}");
            Assert.AreEqual(expectedOthers, others,
                            $"{sceneName} places {others} other village building(s); written against {expectedOthers}");
        }

        static void CheckSheet(MaterialPropertyBlock block, string property, Texture2D albedo, string suffix,
                               string who, List<string> failures)
        {
            Texture bound = block.GetTexture(property);
            if (bound == null)
            {
                failures.Add($"{who}: {property} is unbound");
                return;
            }
            if (bound.name != albedo.name + suffix)
                failures.Add($"{who}: {property} holds '{bound.name}', not '{albedo.name + suffix}'");
            if (bound.width != albedo.width || bound.height != albedo.height)
                failures.Add($"{who}: {bound.name} is {bound.width}×{bound.height}, the albedo " +
                             $"{albedo.width}×{albedo.height} — not the albedo's grid");
        }

        // ---- the rooms open where their houses do -----------------------------------------------

        [UnityTest]
        public IEnumerator EveryRoomInStPeters_IsDrawnWithItsDoorwayInTheGapItsWallsLeave()
        {
            const int expectedRooms = 5;
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo shellField = typeof(BuildingInterior).GetField("_shell", Private);
            FieldInfo roomField = typeof(BuildingInterior).GetField("_room", Private);
            FieldInfo upperField = typeof(BuildingInterior).GetField("_upperRoom", Private);
            Assert.IsNotNull(shellField, "BuildingInterior no longer serializes _shell; re-point this test");
            Assert.IsNotNull(roomField, "BuildingInterior no longer serializes _room; re-point this test");
            Assert.IsNotNull(upperField, "BuildingInterior no longer serializes _upperRoom; re-point this test");

            Interiors contract = ReadContract<Interiors>(InteriorsContractPath);
            Assert.Greater(contract.facings, 0, "the interiors contract declares no facings");
            Assert.Greater(contract.ppu, 0f, "the interiors contract declares no ppu");
            yield return Load("StPeters");

            var failures = new List<string>();
            int rooms = 0;
            foreach (BuildingInterior interior in Object.FindObjectsByType<BuildingInterior>(
                         FindObjectsInactive.Include))
            {
                var room = roomField.GetValue(interior) as SpriteRenderer;
                if (room == null || room.sprite == null ||
                    !room.sprite.texture.name.StartsWith(RoomSheetPrefix, StringComparison.Ordinal))
                    continue;   // a shop: its own kit, whose registration did not move
                rooms++;
                string who = interior.gameObject.name;

                // ⭐ The cell: the shell's facing plus the offset the bake MEASURED — one number, carried
                // in the contract, the same one the stander turns every room by.
                var shell = shellField.GetValue(interior) as SpriteRenderer;
                Sprite shellSprite = shell != null ? shell.sprite : null;
                int shellCell = CellOf(shellSprite);
                int roomCell = CellOf(room.sprite);
                int expectedCell = (shellCell + contract.exteriorFacingOffset) % contract.facings;
                if (shellCell < 0 || roomCell < 0)
                    failures.Add($"{who}: cannot read a facing off shell " +
                                 $"'{(shellSprite != null ? shellSprite.name : "none")}' or room " +
                                 $"'{room.sprite.name}'");
                else if (roomCell != expectedCell)
                    failures.Add($"{who}: the room is cell {roomCell}, but its shell is cell {shellCell} and " +
                                 $"the contract registers rooms at +{contract.exteriorFacingOffset}, so " +
                                 $"cell {expectedCell}");

                float halfDoorway = interior.DoorwayWidthMetres * 0.5f;
                CheckDoorway(contract, room, interior.Footprint, halfDoorway, who, failures);

                // The storey above, where there is one, is drawn from a room sheet too, and has to turn
                // with the storey below it.
                var upper = upperField.GetValue(interior) as SpriteRenderer;
                if (upper != null && upper.sprite != null)
                {
                    if (CellOf(upper.sprite) != roomCell)
                        failures.Add($"{who}: the storey above is cell {CellOf(upper.sprite)}, the room " +
                                     $"below it cell {roomCell}");
                    CheckDoorway(contract, upper, interior.FootprintFor(1), halfDoorway, who + " (upstairs)",
                                 failures);
                }
            }

            Assert.IsEmpty(failures, "a placed room is not drawn where its walls stand:\n  " +
                                     string.Join("\n  ", failures));
            Assert.AreEqual(expectedRooms, rooms,
                            $"St Peters places {rooms} village room(s); this test was written against {expectedRooms}");
        }

        /// <summary>The doorway the room is DRAWN with — its bake's own door anchor for the cell it shows,
        /// from the floor centre it pivots on — must land inside the gap its walls leave, which is
        /// <see cref="InteriorFootprint.DoorWorld"/> give or take half a doorway. A room shown half a turn
        /// out draws its doorway a whole room's length from the gap, on the back wall.</summary>
        static void CheckDoorway(Interiors contract, SpriteRenderer room, InteriorFootprint walls,
                                 float halfDoorway, string who, List<string> failures)
        {
            string key = room.sprite.texture.name.Substring(RoomSheetPrefix.Length);
            Room entry = contract.rooms?.FirstOrDefault(r => r.key == key);
            int cell = CellOf(room.sprite);
            if (entry == null || entry.doorX == null || entry.doorY == null ||
                cell < 0 || cell >= entry.doorX.Length || cell >= entry.doorY.Length)
            {
                failures.Add($"{who}: the interiors contract has no door anchor for '{key}' cell {cell}");
                return;
            }

            var drawnOffset = new Vector2(entry.doorX[cell] - entry.pivotX,
                                          entry.pivotY - entry.doorY[cell]) / contract.ppu;
            Vector2 drawn = (Vector2)room.transform.position + drawnOffset;
            Vector2 gap = walls.DoorWorld;
            float apart = Vector2.Distance(drawn, gap);
            if (apart > halfDoorway)
                failures.Add($"{who}: '{room.sprite.name}' draws its doorway at {drawn:F2}, but the walls " +
                             $"leave their gap at {gap:F2} — {apart:F2} units apart");
        }

        static int CellOf(Sprite sprite)
        {
            if (sprite == null) return -1;
            Match m = CellSuffix.Match(sprite.name);
            return m.Success ? int.Parse(m.Groups[1].Value) : -1;
        }

        static T ReadContract<T>(string relativePath) where T : class
        {
            string path = Path.Combine(Application.dataPath, relativePath);
            Assert.IsTrue(File.Exists(path), $"the contract is not at {path}");
            var contract = JsonUtility.FromJson<T>(File.ReadAllText(path));
            Assert.IsNotNull(contract, $"{path} does not parse");
            return contract;
        }
    }
}
