using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>The dwelling bake's pure rules (ADR 0060 section 2)</b>: how a face is tagged, which verbs a
    /// fixture may carry, and the refusals. No JS host and no asset database, so every refusal has a
    /// sabotage test that runs in milliseconds (<c>DwellingMeshBakeTests</c>), and
    /// <see cref="DwellingMeshAssetBaker"/> asks the same questions through the same methods.
    /// </summary>
    public static class DwellingMeshBakeRules
    {
        // ------------------------------------------------------------------------------- level tags

        /// <summary>Tag 0: the exterior below the floor (chassis, wheels, step, hitch). Never cut.</summary>
        public const int ExteriorTag = 0;

        /// <summary>
        /// The tag of the shell above the floor and the roof: the LID of every walkable level, so cutting
        /// any level takes the whole upper shell off.
        ///
        /// <para><b>Why the walls above the floor share the roof's tag instead of the room level's.</b>
        /// The level gate lifts exactly one lid per cut (<c>HullMeshDef.Cut</c>), and the camper has two
        /// walkable levels under one roof: the saloon and the wet bath. If the walls carried the saloon's
        /// tag and the roof its own, standing in the wet bath could lift the roof or the walls but not
        /// both, and the camper's skin would stand between the camera and the room. One lid tag shared
        /// by both levels opens the same shell for either, with no shader change. The effect inside the
        /// saloon is exactly ADR 0060's: the body above the floor and the roof come off.</para>
        /// </summary>
        public const int ShellLidTag = 3;

        /// <summary>The shell lid's name in the mesh's level vocabulary.</summary>
        public const string ShellLidLevelId = "shell";

        /// <summary>
        /// The tag of a shell face: <see cref="ShellLidTag"/> when its centroid is above the published
        /// floor height, <see cref="ExteriorTag"/> at or below it.
        /// </summary>
        public static int ShellTagFor(double centroidZ, double soleZ)
            => centroidZ > soleZ ? ShellLidTag : ExteriorTag;

        /// <summary>
        /// True when a room face belongs to a secondary level whose floor is
        /// <paramref name="levelFloor"/>: its plan bounds overlap the floor's bounds grown by
        /// <paramref name="wallThickness"/>, so the partitions that enclose the level and the floor and
        /// walls it shares with the main level are drawn there too. Touching counts as overlapping.
        /// </summary>
        public static bool OverlapsLevel(Vector2 faceMin, Vector2 faceMax, Vector2[] levelFloor,
                                         float wallThickness)
        {
            if (levelFloor == null || levelFloor.Length == 0) return false;
            Vector2 min = levelFloor[0], max = levelFloor[0];
            for (int i = 1; i < levelFloor.Length; i++)
            {
                min = Vector2.Min(min, levelFloor[i]);
                max = Vector2.Max(max, levelFloor[i]);
            }
            min -= new Vector2(wallThickness, wallThickness);
            max += new Vector2(wallThickness, wallThickness);
            return faceMin.x <= max.x && faceMax.x >= min.x && faceMin.y <= max.y && faceMax.y >= min.y;
        }

        // ------------------------------------------------------------------------------- the verbs

        /// <summary>
        /// ⚠️ <b>A STUB of ADR 0060 section 4's verb table: the vocabulary only.</b> The table proper maps a
        /// verb to the component the prefab builder adds, and it lives beside that builder (ADR 0060 PR
        /// 5). PR 2 needs only the refusal ("an unknown verb is a bake refusal, not a silent prop"), so
        /// this is the set of verbs section 4 names and nothing else. When the builder lands, its table
        /// replaces this set and this becomes a read of its keys.
        /// </summary>
        public static readonly HashSet<string> KnownVerbs = new HashSet<string>(StringComparer.Ordinal)
        {
            "sleep_save",                              // InteriorBed
            "freeze_catch",                            // the catch-hold freezer
            "storage",                                 // StowageRack
            "food_storage",                            // the kitchen store fixture
            "cook", "prep",                            // CookingStationFixture
            "wash", "wash_dishes", "toilet", "bathe",  // WashStationPoint
            "sit",                                     // SitPoint
            "step_out",                                // the door
        };

        // ---------------------------------------------------------------------------- the refusals

        /// <summary>A fixture anchor whose verb is not in <see cref="KnownVerbs"/>, or null.</summary>
        public static string VerbProblem(DwellingFixtureAnchor[] anchors)
        {
            if (anchors == null) return null;
            foreach (DwellingFixtureAnchor a in anchors)
                if (string.IsNullOrEmpty(a.Verb) || !KnownVerbs.Contains(a.Verb))
                    return $"fixture '{a.AnchorId}' carries the verb '{a.Verb}', which is not in the verb " +
                           "table. An unknown verb is a refusal, not a silent prop: add it to the table " +
                           "with the component it becomes, or take the rig's word back upstream.";
            return null;
        }

        /// <summary>A walkable level whose floor polygon does not close, or null.</summary>
        public static string FloorProblem(DwellingLevel[] levels)
        {
            if (levels == null) return "no walkable level was read";
            foreach (DwellingLevel l in levels)
                if (!DwellingMeshRules.FloorCloses(l.Floor))
                    return $"level '{l.LevelId}': its floor polygon does not close ({l.Floor?.Length ?? 0} " +
                           "points, or no area). A floor that does not close has no inside to stand in.";
            return null;
        }

        /// <summary>
        /// A room sidecar whose shell stamp does not pin the shell rig, or null. A line-ending-only
        /// difference is accepted, as everywhere sidecars are stamped (<see cref="RigHashMatch"/>).
        /// </summary>
        public static string ShellStampProblem(string sidecarShellSha, byte[] shellRigBytes, string shellRigPath)
        {
            RigHashMatch match = DeckSidecarReader.MatchRigHash(shellRigBytes, sidecarShellSha, out string actual);
            if (match != RigHashMatch.None) return null;
            return $"the room sidecar is stamped against shell rig {(string.IsNullOrEmpty(sidecarShellSha) ? "(none)" : sidecarShellSha)} " +
                   $"but {shellRigPath} hashes to {actual}. The room was measured against another shell, so " +
                   "its walls may stand outside this one. The fix is upstream: regenerate the room " +
                   "sidecar. Do NOT re-stamp it here.";
        }

        /// <summary>The same check for the room sidecar against its own rig.</summary>
        public static string RoomStampProblem(string sidecarRoomSha, byte[] roomRigBytes, string roomRigPath)
        {
            RigHashMatch match = DeckSidecarReader.MatchRigHash(roomRigBytes, sidecarRoomSha, out string actual);
            if (match != RigHashMatch.None) return null;
            return $"the room sidecar is stamped against room rig {(string.IsNullOrEmpty(sidecarRoomSha) ? "(none)" : sidecarRoomSha)} " +
                   $"but {roomRigPath} hashes to {actual}. Its fixtures and floors may describe another room.";
        }

        /// <summary>More interior ramps than the shader's table holds, or null (the shared rule).</summary>
        public static string InteriorRampProblem(string owner, int rampCount)
            => RigRoomExtraction.InteriorRampProblem(owner, rampCount);

        /// <summary>
        /// Every refusal the room data can raise, in the order the bake checks them; the first one
        /// found, or null. The render half is checked again by <see cref="DwellingMeshDef.FirstProblem"/>
        /// on the written asset.
        /// </summary>
        public static string FirstRoomRefusal(DwellingRoomSidecar room)
        {
            if (room == null) return "no room sidecar was read";
            if (room.Errors.Count > 0) return room.Errors[0];
            return FloorProblem(room.Levels)
                   ?? VerbProblem(room.Anchors)
                   ?? DwellingMeshRules.FirstRoomProblem(room.Levels, room.Threshold, room.Anchors);
        }
    }

    /// <summary>
    /// <b>A room sidecar (<c>camper-interior@1</c>), read into the def's own types.</b> Walkable levels
    /// become <see cref="DwellingLevel"/> rows, <c>THRESHOLD</c> plus the <c>step_out</c> fixture becomes
    /// the <see cref="DwellingThreshold"/>, and <c>INTERACT</c> becomes the
    /// <see cref="DwellingFixtureAnchor"/>s. Nothing is retyped: every number is the sidecar's.
    ///
    /// <para>A field the bake needs and the sidecar does not publish is an ERROR naming the field (ADR
    /// 0060 section 2), never a default. Tags are assigned here because the sidecar carries level NAMES
    /// and the gate needs ints: the main level (the threshold's) is 1, the next 2, in the sidecar's
    /// order, and every walkable level's lid is <see cref="DwellingMeshBakeRules.ShellLidTag"/>.</para>
    /// </summary>
    public sealed class DwellingRoomSidecar
    {
        /// <summary>The fields that name a fixture's Def, in the sidecar's own spelling. The ADR's
        /// single <c>defId</c> field is an upstream ask; until it lands, the first of these that holds a
        /// string is the Def id.</summary>
        public static readonly string[] DefIdFields = { "freezer", "stowage", "kitchen_storage", "washstation" };

        public string Schema = "";
        public string ShellRigSha = "";
        public string RoomRigSha = "";
        public string Convention = "";
        public float SoleZ;
        public float CeilingZ;
        public DwellingLevel[] Levels = Array.Empty<DwellingLevel>();
        public DwellingThreshold Threshold;
        public DwellingFixtureAnchor[] Anchors = Array.Empty<DwellingFixtureAnchor>();
        public readonly List<string> Errors = new List<string>();

        /// <summary>Read a room sidecar's text. Never throws on content; problems land in
        /// <see cref="Errors"/>. Malformed JSON throws, as everywhere sidecars are read.</summary>
        public static DwellingRoomSidecar Read(string json, string sourceName)
        {
            var r = new DwellingRoomSidecar();
            object doc = DeckSidecarJson.Parse(json);
            string Where(string field) => $"{sourceName}: {field}";

            r.Schema = DeckSidecarJson.String(DeckSidecarJson.Member(doc, "schema")) ?? "";
            r.ShellRigSha = DeckSidecarJson.String(DeckSidecarJson.Member(doc, "shellRigSha256")) ?? "";
            r.RoomRigSha = DeckSidecarJson.String(DeckSidecarJson.Member(doc, "derivedFromRigSha256")) ?? "";
            r.Convention = DeckSidecarJson.String(
                DeckSidecarJson.Member(DeckSidecarJson.Member(doc, "cell"), "convention")) ?? "";

            object room = DeckSidecarJson.Member(doc, "ROOM");
            if (!DeckSidecarJson.TryDouble(DeckSidecarJson.Member(room, "sole_z_m"), out double sole))
                r.Errors.Add(Where("ROOM.sole_z_m is not published; the floor height tags every shell face"));
            bool enclosed = DeckSidecarJson.TryDouble(DeckSidecarJson.Member(room, "ceiling_z_max_m"), out double ceiling);
            if (!enclosed)
                r.Errors.Add(Where("ROOM.ceiling_z_max_m is not published; a level with no ceiling cannot be cut into"));
            r.SoleZ = (float)sole;
            r.CeilingZ = (float)ceiling;

            object thresholdNode = DeckSidecarJson.Member(doc, "THRESHOLD");
            object interact = DeckSidecarJson.Member(doc, "INTERACT");
            r.Anchors = ReadAnchors(interact, r.Errors, sourceName);

            // The main level is the one the door opens onto; it is listed first and tagged 1.
            string doorLevel = null;
            Vector3 doorPoint = default;
            foreach (DwellingFixtureAnchor a in r.Anchors)
                if (a.Verb == "step_out") { doorLevel = a.LevelId; doorPoint = a.PositionMeters; break; }
            if (doorLevel == null)
                r.Errors.Add(Where("INTERACT has no step_out fixture, so there is no door point on the wall line"));

            r.Levels = ReadLevels(DeckSidecarJson.Member(doc, "WALKABLE"), doorLevel, enclosed, r.CeilingZ,
                                  r.Errors, sourceName);

            var threshold = new DwellingThreshold
            {
                Id = DeckSidecarJson.String(DeckSidecarJson.Member(thresholdNode, "id")) ?? "",
                LevelId = doorLevel ?? "",
                PointMeters = doorPoint,
            };
            if (!DeckSidecarJson.TryDouble(DeckSidecarJson.Member(thresholdNode, "door_clear_width_m"), out double width))
                r.Errors.Add(Where("THRESHOLD.door_clear_width_m is not published"));
            if (!TryVec2(DeckSidecarJson.Member(thresholdNode, "landing_centre"), out Vector2 landing))
                r.Errors.Add(Where("THRESHOLD.landing_centre is not published"));
            if (!DeckSidecarJson.TryDouble(DeckSidecarJson.Member(thresholdNode, "landing_radius_m"), out double radius))
                r.Errors.Add(Where("THRESHOLD.landing_radius_m is not published"));
            threshold.ClearWidthMeters = (float)width;
            threshold.InsideLandingMeters = landing;
            threshold.LandingRadiusMeters = (float)radius;
            r.Threshold = threshold;
            return r;
        }

        static DwellingLevel[] ReadLevels(object walkable, string mainLevel, bool enclosed, float ceilingZ,
                                          List<string> errors, string sourceName)
        {
            var obj = DeckSidecarJson.AsObject(walkable);
            if (obj == null || obj.Count == 0)
            {
                errors.Add($"{sourceName}: WALKABLE is not published");
                return Array.Empty<DwellingLevel>();
            }

            // Order: the main level first, then the rest in the sidecar's own order.
            var names = new List<string>();
            if (mainLevel != null && obj.ContainsKey(mainLevel)) names.Add(mainLevel);
            foreach (string k in obj.Keys) if (!names.Contains(k)) names.Add(k);

            var levels = new DwellingLevel[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                object node = obj[names[i]];
                if (!DeckSidecarJson.TryDouble(DeckSidecarJson.Member(node, "z"), out double z))
                    errors.Add($"{sourceName}: WALKABLE.{names[i]}.z is not published");
                levels[i] = new DwellingLevel
                {
                    LevelId = names[i],
                    Tag = i + 1,
                    LidTag = DwellingMeshBakeRules.ShellLidTag,
                    Enclosed = enclosed,
                    Storey = 0,
                    SoleZMeters = (float)z,
                    CeilingZMeters = ceilingZ,
                    Floor = ReadPolygon(DeckSidecarJson.Member(node, "polygon")),
                };
            }
            if (levels.Length >= DwellingMeshBakeRules.ShellLidTag)
                errors.Add($"{sourceName}: {levels.Length} walkable levels would collide with the shell's lid " +
                           $"tag {DwellingMeshBakeRules.ShellLidTag}; move the lid tag before baking a third level");
            return levels;
        }

        static DwellingFixtureAnchor[] ReadAnchors(object interact, List<string> errors, string sourceName)
        {
            var list = DeckSidecarJson.AsArray(interact);
            if (list == null)
            {
                errors.Add($"{sourceName}: INTERACT is not published");
                return Array.Empty<DwellingFixtureAnchor>();
            }
            var anchors = new DwellingFixtureAnchor[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                object a = list[i];
                string id = DeckSidecarJson.String(DeckSidecarJson.Member(a, "id")) ?? "";
                if (!TryVec3(DeckSidecarJson.Member(a, "at"), out Vector3 at))
                    errors.Add($"{sourceName}: INTERACT '{id}'.at is not published");
                if (!TryVec3(DeckSidecarJson.Member(a, "reach_point"), out Vector3 reach))
                    errors.Add($"{sourceName}: INTERACT '{id}'.reach_point is not published");
                string defId = "";
                foreach (string field in DefIdFields)
                {
                    string s = DeckSidecarJson.String(DeckSidecarJson.Member(a, field));
                    if (!string.IsNullOrEmpty(s)) { defId = s; break; }
                }
                anchors[i] = new DwellingFixtureAnchor
                {
                    AnchorId = id,
                    Verb = DeckSidecarJson.String(DeckSidecarJson.Member(a, "action")) ?? "",
                    DefId = defId,
                    LevelId = DeckSidecarJson.String(DeckSidecarJson.Member(a, "level")) ?? "",
                    PositionMeters = at,
                    ReachPointMeters = reach,
                    Footprint = Array.Empty<Vector2>(),
                };
            }
            return anchors;
        }

        static Vector2[] ReadPolygon(object node)
        {
            var list = DeckSidecarJson.AsArray(node);
            if (list == null) return Array.Empty<Vector2>();
            var pts = new List<Vector2>(list.Count);
            foreach (object p in list)
                if (TryVec2(p, out Vector2 v)) pts.Add(v);
                else return Array.Empty<Vector2>();
            return pts.ToArray();
        }

        static bool TryVec2(object node, out Vector2 v)
        {
            var a = DeckSidecarJson.AsArray(node);
            v = default;
            if (a == null || a.Count < 2) return false;
            if (!DeckSidecarJson.TryDouble(a[0], out double x) || !DeckSidecarJson.TryDouble(a[1], out double y))
                return false;
            v = new Vector2((float)x, (float)y);
            return true;
        }

        static bool TryVec3(object node, out Vector3 v)
        {
            var a = DeckSidecarJson.AsArray(node);
            v = default;
            if (a == null || a.Count < 3) return false;
            if (!DeckSidecarJson.TryDouble(a[0], out double x) || !DeckSidecarJson.TryDouble(a[1], out double y) ||
                !DeckSidecarJson.TryDouble(a[2], out double z))
                return false;
            v = new Vector3((float)x, (float)y, (float)z);
            return true;
        }
    }
}
