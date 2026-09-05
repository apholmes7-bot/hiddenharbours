using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>ADR 0041 — the door of a converted hull opens into a ROOM.</b>
    ///
    /// <para>A boat's interior def declares her open working decks as well as her rooms, because the
    /// walker measures both. Which of them a sill walks you in onto is decided by
    /// <see cref="BoatInterior.LevelIndexAtHeight"/>, nearest-by-height — and on a sprite hull the tie
    /// is broken by the CELLS' row map: a level the sheets never baked is a level you cannot be in.</para>
    ///
    /// <para><b>A converted hull has no cells and never loads any</b>, so that map is empty forever and
    /// the tie-break would be iteration order. Her equivalent is the hull mesh's own level table, asked
    /// through <see cref="HullMeshDef.CutawayForDeck"/> — the same gate the cutaway asks, which refuses
    /// a level whose <c>Enclosed</c> is false. This fixture holds that join on every converted hull, and
    /// asserts its own premise first: that the ships really do declare an open deck at the exact height
    /// of the room behind their door, so the guard is not passing on data where nothing could go wrong.</para>
    /// </summary>
    public sealed class ConvertedHullEntryLevelTests
    {
        readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        public readonly struct Subject
        {
            public readonly string Key;
            public readonly BoatInteriorDef Def;
            public readonly HullMeshDef Mesh;
            public Subject(string key, BoatInteriorDef def, HullMeshDef mesh) { Key = key; Def = def; Mesh = mesh; }
            public override string ToString() => Key;
        }

        /// <summary>Every converted hull, with both halves of the join loaded — her interior def (the
        /// levels) and her committed hull mesh (which of them are rooms). Derived from the bake's own
        /// switch through <see cref="ConvertedInteriors"/>, so a rollout batch enrols here by baking.</summary>
        public static IEnumerable<Subject> Converted()
        {
            var found = new List<Subject>();
            foreach (ConvertedInteriors.Converted c in ConvertedInteriors.All())
            {
                FleetHull hull = HullMeshFleet.Get(c.Key);
                var def = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(c.DefAssetPath);
                var mesh = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                if (def == null || mesh == null)
                    throw new InvalidOperationException(
                        $"{c.Key}: interior def or hull mesh missing ({c.DefAssetPath} / {hull.MeshAssetPath}).");
                found.Add(new Subject(c.Key, def, mesh));
            }
            Assert.IsNotEmpty(found, "no converted hulls at all — this whole fixture would vacuously pass.");
            return found;
        }

        BoatInterior CabinFor(Subject s, HullMeshDef meshRoom)
        {
            var root = new GameObject($"Cabin_{s.Key}");
            _spawned.Add(root);
            var cabin = root.AddComponent<BoatInterior>();
            // Exactly the installer's converted-hull call: no renderer, no pivot, no cells, no row map.
            cabin.Configure(s.Def, exterior: null, interior: null, fittings: null, interiorPivot: null,
                            boatRoot: root.transform, cells: null, facings: 8,
                            cellsAreCounterClockwise: true, zeroHeadingDegrees: 0f,
                            deckRollDegrees: 0f, deckHeavePixels: 0f, deckPitchLiftMeters: 0f,
                            cellRowForLevel: null, meshRoom: meshRoom);
            return cabin;
        }

        /// <summary>
        /// <b>The guard.</b> Her sill resolves to a level her own mesh will cut open. A level the mesh
        /// calls open is an outdoor deck: walking there through a cabin door shows the player nothing,
        /// and the cutaway that should have taken the roof off answers <c>Cut.None</c>.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(Converted))]
        public void HerSillLandsInARoomHerMeshCanOpen(Subject s)
        {
            BoatInteriorDoor door = s.Def.Door;
            Assert.IsNotNull(door, $"{s.Key}: a converted hull with no door is a cabin nobody can enter.");

            BoatInterior cabin = CabinFor(s, s.Mesh);
            int level = cabin.LevelIndexAtHeight(door.ThresholdPoint.z);

            Assert.GreaterOrEqual(level, 0,
                $"{s.Key}: her sill at z {door.ThresholdPoint.z:0.###} names no level at all.");
            string id = s.Def.Levels[level].Id;
            Assert.IsTrue(s.Mesh.CutawayForDeck(id).Opens,
                $"{s.Key}: her sill at z {door.ThresholdPoint.z:0.###} walks the player onto '{id}', " +
                "which her hull mesh will not cut open — an OPEN working deck. Nearest-by-height alone " +
                "cannot tell the two apart on a ship whose main deck sits at her house sole's height; " +
                "BoatInterior.LevelIsDrawn is what asks the mesh.");
        }

        /// <summary>
        /// <b>The premise the guard above rests on — asserted, not assumed.</b> If no converted hull
        /// declared an open level at the same height as the room behind her door, the guard could not
        /// fail however the tie were broken, and its green would mean nothing. The working ships DO:
        /// all four declare <c>main_deck</c> at exactly their house sole's height, and their door
        /// thresholds sit at that height too.
        /// </summary>
        [Test]
        public void TheTieIsReal_SomeConvertedHullDeclaresAnOpenLevelAtHerSillsHeight()
        {
            var ties = new List<string>();
            foreach (Subject s in Converted())
            {
                BoatInteriorDoor door = s.Def.Door;
                if (door == null) continue;
                float z = door.ThresholdPoint.z;

                // Levels the sill is EXACTLY as near to as the nearest one — the case an ordering
                // tie-break would decide. For the tie to be a real hazard on this hull, one of them
                // must be a room and another must be an open deck.
                float best = s.Def.Levels.Where(l => l != null && l.IsUsable())
                                         .Select(l => Mathf.Abs(l.SoleZMeters - z))
                                         .DefaultIfEmpty(float.MaxValue).Min();
                var equidistant = s.Def.Levels
                    .Where(l => l != null && l.IsUsable() && Mathf.Abs(Mathf.Abs(l.SoleZMeters - z) - best) < 1e-4f)
                    .ToArray();
                if (equidistant.Length < 2) continue;

                bool anyOpen = equidistant.Any(l => !s.Mesh.CutawayForDeck(l.Id).Opens);
                bool anyRoom = equidistant.Any(l => s.Mesh.CutawayForDeck(l.Id).Opens);
                if (anyOpen && anyRoom)
                    ties.Add($"{s.Key}: {string.Join(" / ", equidistant.Select(l => $"{l.Id}@{l.SoleZMeters:0.###}"))}");
            }

            Assert.IsNotEmpty(ties,
                "not one converted hull declares an open level and a room at the same distance from her " +
                "sill, so HerSillLandsInARoomHerMeshCanOpen would pass on any tie-break at all. Either " +
                "the fleet's shipped defs changed, or this guard has stopped guarding anything.");
            UnityEngine.Debug.Log("[ADR 0041] sills with a real open-vs-room tie:\n  " + string.Join("\n  ", ties));
        }

        /// <summary>
        /// <b>WHICH FACES WENT WHERE — every room vertex is tagged to a level that is a ROOM.</b>
        ///
        /// <para>The four working ships each declare an OPEN <c>main_deck</c> at the same sole height as
        /// their <c>house_sole</c>, and the arc's charter asked this batch to say out loud which of the
        /// two the room's faces were tagged to rather than trusting that the published ceilings broke the
        /// tie. This reads the committed mesh: TexCoord1.y is the bake's room flag and TexCoord1.x is the
        /// level tag, so a room vertex carrying an OPEN level's tag is a wall the gate can never hide —
        /// it would draw through the topsides forever, since the discard inside <c>HH_LEVEL_GATE</c> only
        /// ever culls the level being cut.</para>
        ///
        /// <para>And the converse, in the same sweep: an enclosed level with NO room vertices is a room
        /// that was declared and never built, which the reveal test would only catch on a GPU.</para>
        /// </summary>
        [Test]
        [TestCaseSource(nameof(Converted))]
        public void EveryRoomFace_IsTaggedToALevelThatIsARoom(Subject s)
        {
            var tags = new List<Vector2>();
            s.Mesh.Mesh.GetUVs(1, tags);
            Assert.AreEqual(s.Mesh.Mesh.vertexCount, tags.Count,
                $"{s.Key}: the committed mesh carries no per-vertex level tags, so this reads nothing.");

            var roomVertsByTag = new SortedDictionary<int, int>();
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i].y <= 0.5f) continue;                     // hull, not room
                int tag = Mathf.RoundToInt(tags[i].x);
                roomVertsByTag[tag] = roomVertsByTag.TryGetValue(tag, out int n) ? n + 1 : 1;
            }
            Assert.IsNotEmpty(roomVertsByTag, $"{s.Key}: no room vertices at all — she is not converted.");

            var enclosedTags = new Dictionary<int, string>();
            var openTags = new Dictionary<int, string>();
            foreach (HullMeshDef.LevelTag lvl in s.Mesh.LevelTags)
                (lvl.Enclosed ? enclosedTags : openTags)[lvl.Tag] = $"{lvl.LevelId}/{lvl.DeckId}";

            var report = new List<string>();
            foreach (var kv in roomVertsByTag)
            {
                string where = enclosedTags.TryGetValue(kv.Key, out string e) ? $"ENCLOSED {e}"
                             : openTags.TryGetValue(kv.Key, out string o) ? $"OPEN {o}"
                             : "no declared level";
                report.Add($"tag {kv.Key}: {kv.Value} room verts — {where}");
            }
            UnityEngine.Debug.Log($"[ADR 0041] {s.Key} room faces by level tag:\n  " + string.Join("\n  ", report));

            var misplaced = roomVertsByTag.Keys.Where(t => !enclosedTags.ContainsKey(t)).ToArray();
            CollectionAssert.IsEmpty(misplaced,
                $"{s.Key}: room geometry is tagged to level(s) {string.Join(", ", misplaced)}, which this " +
                "hull does not declare as ENCLOSED. Only the level being cut is discarded, so a room face " +
                "on an open deck's tag draws through her topsides at every heading and no cut can hide it.");

            var empty = enclosedTags.Where(kv => !roomVertsByTag.ContainsKey(kv.Key))
                                    .Select(kv => $"{kv.Value} (tag {kv.Key})").ToArray();
            CollectionAssert.IsEmpty(empty,
                $"{s.Key}: these levels are declared ENCLOSED and carry no room geometry at all, so " +
                "cutting them open reveals the inside of an empty hull: " + string.Join(", ", empty));
        }

        /// <summary>
        /// <b>And the sprite hulls keep their own answer.</b> The row map is still what decides for a
        /// hull whose room is a sheet — asserted by handing the same cabin no mesh and the map a cells
        /// asset carries, and requiring the same room back. Two paths, one verdict.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(Converted))]
        public void WithoutHerMesh_TheRowMapStillDecides(Subject s)
        {
            BoatInteriorDoor door = s.Def.Door;
            if (door == null) Assert.Ignore($"{s.Key} has no door.");

            // The sheet path's map, built the way a cells asset carries it: -1 for a level the sheets
            // never baked (every OPEN level), a row otherwise.
            var rows = new int[s.Def.Levels.Length];
            int row = 0;
            for (int i = 0; i < rows.Length; i++)
                rows[i] = s.Mesh.CutawayForDeck(s.Def.Levels[i].Id).Opens ? row++ : -1;

            var root = new GameObject($"SpriteCabin_{s.Key}");
            _spawned.Add(root);
            var cabin = root.AddComponent<BoatInterior>();
            cabin.Configure(s.Def, null, null, null, null, root.transform, null, 8, true, 0f, 0f, 0f, 0f,
                            cellRowForLevel: rows, meshRoom: null);

            Assert.IsFalse(cabin.RoomIsGeometry, "no mesh was handed in, so this arm is the sprite path");
            int level = cabin.LevelIndexAtHeight(door.ThresholdPoint.z);
            Assert.GreaterOrEqual(level, 0, $"{s.Key}: the sprite path names no level.");
            Assert.IsTrue(s.Mesh.CutawayForDeck(s.Def.Levels[level].Id).Opens,
                $"{s.Key}: with a row map the sprite path landed on '{s.Def.Levels[level].Id}', an open " +
                "deck. The two paths must agree about which levels are rooms.");
        }
    }
}
