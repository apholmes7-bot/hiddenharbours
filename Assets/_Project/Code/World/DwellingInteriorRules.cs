using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>How a mesh dwelling's levels answer <see cref="BuildingInterior"/>'s storeys</b> (ADR 0060 section 3):
    /// which level's floor is the inside test on a storey, and which cut opens it. Pure and allocation-free,
    /// so the inside test can ask every frame and the tests can ask headless.
    ///
    /// <para><b>One level speaks for each storey.</b> Several levels may share a storey (the camper's saloon
    /// and its wet bath are both storey 0). The storey's <i>primary</i> level is the one the door opens onto
    /// when the door is on that storey, and otherwise the first level the def lists on it. Its floor is the
    /// storey's inside test and its cut is the storey's cut. A smaller room inside it (the wet bath) keeps its
    /// own partition faces, which a cut of the primary level does not remove, so it still reads as a room
    /// within the room.</para>
    /// </summary>
    public static class DwellingInteriorRules
    {
        /// <summary>The index in <paramref name="def"/>'s levels of the level that speaks for
        /// <paramref name="storey"/>, or −1 when the def is null or has no level on that storey.</summary>
        public static int PrimaryLevelIndex(DwellingMeshDef def, int storey)
        {
            if (def == null || def.Levels == null) return -1;

            int door = DwellingMeshRules.IndexOfLevel(def.Levels, def.Threshold.LevelId);
            if (door >= 0 && def.Levels[door].Storey == storey) return door;

            for (int i = 0; i < def.Levels.Length; i++)
                if (def.Levels[i].Storey == storey) return i;
            return -1;
        }

        /// <summary>True, with the outline, when <paramref name="storey"/> has a primary level whose floor
        /// closes. False leaves the caller on its rectangle.</summary>
        public static bool TryFloor(DwellingMeshDef def, int storey, out Vector2[] floor)
        {
            int i = PrimaryLevelIndex(def, storey);
            floor = i >= 0 ? def.Levels[i].Floor : null;
            if (floor != null && DwellingMeshRules.FloorCloses(floor)) return true;
            floor = null;
            return false;
        }

        /// <summary>The threshold point on the ground plane, rig metres: the doorway the inside test's
        /// <see cref="InteriorFootprint.DoorWorld"/> reports.</summary>
        public static Vector2 DoorModel(DwellingMeshDef def) =>
            def != null ? new Vector2(def.Threshold.PointMeters.x, def.Threshold.PointMeters.y) : Vector2.zero;

        /// <summary>
        /// The cut for the occupant <paramref name="inside"/> on <paramref name="storey"/>, under the
        /// 2026-11-14 rule: standing inside a level cuts that level, and outside the exterior is whole.
        /// <see cref="HullMeshDef.Cut.None"/> outside, for a null def, and for a storey with no level.
        /// </summary>
        public static HullMeshDef.Cut CutFor(DwellingMeshDef def, bool inside, int storey)
        {
            if (!inside) return HullMeshDef.Cut.None;
            int i = PrimaryLevelIndex(def, storey);
            return i >= 0 ? DwellingMeshRules.CutFor(def.Levels, def.Levels[i].LevelId) : HullMeshDef.Cut.None;
        }
    }
}
