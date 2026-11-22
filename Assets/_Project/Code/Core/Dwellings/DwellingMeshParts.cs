using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One walkable level of a parked dwelling (ADR 0060 section 2)</b>: the row that joins the faces
    /// the bake tagged in <c>TexCoord1.x</c> to the floor the occupant walks on and the storey
    /// <c>BuildingInterior</c> says he is on.
    ///
    /// <para>All positions are in the dwelling's own rig metres: origin at the ground centre of the body
    /// footprint, +x the curb (door) side, +y toward the nose or hitch, +z up. That is the frame the shell
    /// and room sidecars already publish, so nothing is re-derived between the rig and the def.</para>
    /// </summary>
    [Serializable]
    public struct DwellingLevel
    {
        [Tooltip("The rig's own level name ('saloon', 'head'). Fixture anchors and the threshold name " +
                 "their level by this id.")]
        public string LevelId;

        [Tooltip("The int this level's faces carry in TexCoord1.x. Never 0: tag 0 is the exterior, which " +
                 "is never cut, so a room tagged 0 could never be opened (ADR 0041).")]
        public int Tag;

        [Tooltip("The tag of the faces that come off with this level when it is cut: the roof over it. " +
                 "0 = no lid.")]
        public int LidTag;

        [Tooltip("True when the rig declared a ceiling over this level. An open level cannot be cut " +
                 "into, the same rule as a hull's open deck.")]
        public bool Enclosed;

        [Tooltip("The BuildingInterior storey this level belongs to: 0 the ground floor, 1 the storey " +
                 "above. Several levels may share a storey (the camper's saloon and its wet bath).")]
        [Min(0)] public int Storey;

        [Tooltip("Floor height above rig z = 0, metres.")]
        public float SoleZMeters;

        [Tooltip("The ceiling's underside above rig z = 0, metres. Only meaningful when Enclosed.")]
        public float CeilingZMeters;

        [Tooltip("The walkable floor outline, rig metres, in order around the edge. The closing edge " +
                 "from the last point back to the first is implied. Read from the rig, never retyped.")]
        public Vector2[] Floor;
    }

    /// <summary>
    /// <b>The dwelling's door, as the threshold <c>BuildingInterior</c> crosses (ADR 0060 section 3).</b>
    /// The door point is on the wall line; the landing is the clear spot just inside it where a body
    /// stands after stepping in.
    /// </summary>
    [Serializable]
    public struct DwellingThreshold
    {
        [Tooltip("The sidecar's own THRESHOLD id ('entry').")]
        public string Id;

        [Tooltip("The level the door opens onto.")]
        public string LevelId;

        [Tooltip("The threshold point on the wall line, rig metres (x, y, z).")]
        public Vector3 PointMeters;

        [Tooltip("The door's clear width, metres. 0 = no door was published.")]
        [Min(0f)] public float ClearWidthMeters;

        [Tooltip("The centre of the clear landing just inside the door, rig metres.")]
        public Vector2 InsideLandingMeters;

        [Tooltip("The landing's radius, metres.")]
        [Min(0f)] public float LandingRadiusMeters;

        /// <summary>True when the art published a door. A def with no door has no way in.</summary>
        public bool IsPublished => ClearWidthMeters > 0f && !string.IsNullOrEmpty(LevelId);
    }

    /// <summary>
    /// <b>One fixture in a dwelling's room, as the rig declares it (ADR 0060 section 4).</b> The rig says
    /// WHAT the fixture is (its verb and, where it has one, the Def that holds its numbers). It never says
    /// WHICH instance it is: the save key of a placed store comes from the site's plan, so two dwellings
    /// placed from one rig get two keys.
    ///
    /// <para>The verb is the sidecar's own word (<c>sleep_save</c>, <c>freeze_catch</c>, <c>storage</c>,
    /// <c>cook</c>, <c>sit</c>, <c>step_out</c>). Which component a verb becomes is the editor builder's
    /// verb table, not Core's. Capacities, rates and prices stay on the Defs (rule 6).</para>
    /// </summary>
    [Serializable]
    public struct DwellingFixtureAnchor
    {
        [Tooltip("The sidecar's INTERACT id ('freezer', 'bunk'). Unique within one dwelling.")]
        public string AnchorId;

        [Tooltip("The sidecar's action word ('freeze_catch', 'sleep_save').")]
        public string Verb;

        [Tooltip("The Def that holds this fixture's numbers ('freezer.camper_freezer'). Empty for a " +
                 "fixture with no Def of its own: a seat, the door, a bunk.")]
        public string DefId;

        [Tooltip("The level this fixture stands on.")]
        public string LevelId;

        [Tooltip("Where the fixture is, rig metres (x, y, z): the sidecar's 'at'.")]
        public Vector3 PositionMeters;

        [Tooltip("Where a body stands to use it, rig metres (x, y, z): the sidecar's 'reach_point'.")]
        public Vector3 ReachPointMeters;

        [Tooltip("The fixture's floor outline, rig metres. Empty when the rig published none.")]
        public Vector2[] Footprint;

        /// <summary>True when the fixture's numbers live on a Def.</summary>
        public bool HasDefId => !string.IsNullOrEmpty(DefId);

        /// <summary>True when the rig published a floor outline for the fixture.</summary>
        public bool HasFootprint => Footprint != null && Footprint.Length >= DwellingMeshRules.MinPolygonPoints;
    }

    /// <summary>
    /// The pure rules a <see cref="DwellingMeshDef"/> is held to: lookups, the cut for a level, and the
    /// checks that decide whether a def can be used. Static and allocation-free on the success path, so
    /// the bake (its refusals), the def (<see cref="DwellingMeshDef.IsUsable"/>) and the tests all ask the
    /// same question.
    /// </summary>
    public static class DwellingMeshRules
    {
        /// <summary>The fewest points that can enclose a floor.</summary>
        public const int MinPolygonPoints = 3;

        /// <summary>The smallest floor area, square metres, that counts as a floor rather than a line.
        /// A numeric tolerance, not a tunable: any real room is many orders of magnitude larger.</summary>
        public const float MinFloorAreaSquareMeters = 1e-4f;

        /// <summary>The signed area of a closed outline (shoelace), square metres. Positive when the
        /// points run counter-clockwise. 0 for null or fewer than three points.</summary>
        public static float SignedArea(Vector2[] outline)
        {
            if (outline == null || outline.Length < MinPolygonPoints) return 0f;
            double twice = 0d;
            for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
                twice += (double)outline[j].x * outline[i].y - (double)outline[i].x * outline[j].y;
            return (float)(twice * 0.5d);
        }

        /// <summary>True when the outline encloses a floor: at least three points, all finite, and a
        /// non-zero area. The closing edge is implied, so a repeated first point at the end is allowed.
        /// Self-intersection is the bake's check, not this one.</summary>
        public static bool FloorCloses(Vector2[] outline)
        {
            if (outline == null || outline.Length < MinPolygonPoints) return false;
            for (int i = 0; i < outline.Length; i++)
                if (!IsFinite(outline[i].x) || !IsFinite(outline[i].y)) return false;
            return Mathf.Abs(SignedArea(outline)) >= MinFloorAreaSquareMeters;
        }

        /// <summary>The index of the level named <paramref name="levelId"/>, or −1.</summary>
        public static int IndexOfLevel(DwellingLevel[] levels, string levelId)
        {
            if (levels == null || string.IsNullOrEmpty(levelId)) return -1;
            for (int i = 0; i < levels.Length; i++)
                if (string.Equals(levels[i].LevelId, levelId, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>The index of the anchor named <paramref name="anchorId"/>, or −1.</summary>
        public static int IndexOfAnchor(DwellingFixtureAnchor[] anchors, string anchorId)
        {
            if (anchors == null || string.IsNullOrEmpty(anchorId)) return -1;
            for (int i = 0; i < anchors.Length; i++)
                if (string.Equals(anchors[i].AnchorId, anchorId, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>
        /// The cut that opens the level named <paramref name="levelId"/>: its own tag and its lid, as the
        /// renderer's existing <see cref="HullMeshDef.Cut"/>. <see cref="HullMeshDef.Cut.None"/> when the
        /// level is unknown, open, or tagged 0, so every refusal draws the whole exterior.
        /// </summary>
        public static HullMeshDef.Cut CutFor(DwellingLevel[] levels, string levelId)
        {
            int i = IndexOfLevel(levels, levelId);
            if (i < 0) return HullMeshDef.Cut.None;
            var level = levels[i];
            if (!level.Enclosed || level.Tag <= 0) return HullMeshDef.Cut.None;
            return new HullMeshDef.Cut(level.Tag, level.LidTag > 0 ? level.LidTag : 0);
        }

        /// <summary>
        /// The first reason the room data cannot be used, or null when it can: levels, threshold and
        /// anchors. Names the field, so a bake refusal can say what is missing.
        /// </summary>
        public static string FirstRoomProblem(DwellingLevel[] levels, DwellingThreshold threshold,
                                              DwellingFixtureAnchor[] anchors)
        {
            if (levels == null || levels.Length == 0) return "Levels: no walkable level";
            for (int i = 0; i < levels.Length; i++)
            {
                var level = levels[i];
                if (string.IsNullOrEmpty(level.LevelId)) return $"Levels[{i}].LevelId is empty";
                if (level.Tag <= 0) return $"Levels[{i}] '{level.LevelId}': Tag must be above 0";
                if (level.LidTag < 0 || level.LidTag == level.Tag)
                    return $"Levels[{i}] '{level.LevelId}': LidTag must be 0 or another tag";
                if (level.Storey < 0) return $"Levels[{i}] '{level.LevelId}': Storey is negative";
                if (!FloorCloses(level.Floor)) return $"Levels[{i}] '{level.LevelId}': Floor does not close";
                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(levels[j].LevelId, level.LevelId, StringComparison.Ordinal))
                        return $"Levels[{i}]: LevelId '{level.LevelId}' repeats";
                    if (levels[j].Tag == level.Tag) return $"Levels[{i}]: Tag {level.Tag} repeats";
                }
            }

            if (!threshold.IsPublished) return "Threshold: no door published";
            if (IndexOfLevel(levels, threshold.LevelId) < 0)
                return $"Threshold.LevelId '{threshold.LevelId}' names no level";

            if (anchors == null) return null;
            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (string.IsNullOrEmpty(anchor.AnchorId)) return $"Anchors[{i}].AnchorId is empty";
                if (string.IsNullOrEmpty(anchor.Verb)) return $"Anchors[{i}] '{anchor.AnchorId}': Verb is empty";
                if (IndexOfLevel(levels, anchor.LevelId) < 0)
                    return $"Anchors[{i}] '{anchor.AnchorId}': LevelId '{anchor.LevelId}' names no level";
                if (IndexOfAnchor(anchors, anchor.AnchorId) != i)
                    return $"Anchors[{i}]: AnchorId '{anchor.AnchorId}' repeats";
            }
            return null;
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
