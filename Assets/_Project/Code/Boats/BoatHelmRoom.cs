using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Which room a hull's wheel stands in</b>, read off her own data: the helm station the rig
    /// measured, against the level table her hull mesh carries. The answer is the cut that opens that
    /// one room, or <see cref="HullMeshDef.Cut.None"/> when the wheel stands in the open (a cockpit, a
    /// flybridge) or the data cannot say.
    ///
    /// <para><b>The rule.</b> A room is a level the hull mesh can cut open: a level of the interior def
    /// whose id the mesh has a cut for, or an enclosed row of the mesh's level table that the def does
    /// not name (a helm deck that is a room in the art and not a floor of the walk). The wheel stands
    /// in a room when the station's height is on or over that room's sole and under its ceiling, and no
    /// open deck lies between the sole and the station. Rooms stack, so of two that pass, the higher
    /// sole is the floor under her.</para>
    ///
    /// <para><b>Where heights cannot tell a room from the deck beside it</b> (a cockpit that runs aft
    /// at the house sole's own height), the def's outline of the room does: the room keeps the wheel
    /// only when its outline holds the station in plan. A room with no outline loses that tie, and the
    /// house stays whole: a cut that does not happen is a missing feature, one that opens the wrong
    /// room is a broken boat.</para>
    ///
    /// <para>Pure, static, and asks nothing of the scene. Called when the helm changes hands, never
    /// per frame.</para>
    /// </summary>
    public static class BoatHelmRoom
    {
        /// <summary>The cut that opens the room the helm station stands in. See <see cref="CutAt(BoatInteriorDef, HullMeshDef, Vector3, out string)"/>.</summary>
        public static HullMeshDef.Cut CutAt(BoatInteriorDef def, HullMeshDef mesh, Vector3 stationLocalMeters)
            => CutAt(def, mesh, stationLocalMeters, out _);

        /// <summary>
        /// The cut that opens the room a helm station stands in, and the id the hull mesh keys that
        /// cut by (<paramref name="roomId"/>: a def level's id, or a mesh row's deck or level id).
        /// <see cref="HullMeshDef.Cut.None"/> and a null id when the station stands in the open.
        /// </summary>
        /// <param name="def">Her interior def, or null: a mesh-only room needs none.</param>
        /// <param name="mesh">Her hull mesh, whose level table says which levels are enclosed.</param>
        /// <param name="stationLocalMeters">The helm station, hull-local metres, z above the keel.</param>
        public static HullMeshDef.Cut CutAt(BoatInteriorDef def, HullMeshDef mesh, Vector3 stationLocalMeters,
                                            out string roomId)
        {
            roomId = null;
            if (mesh == null || mesh.LevelTags == null) return HullMeshDef.Cut.None;

            float tolerance = def != null ? def.FloorTolerance : BoatInteriorDef.DefaultFloorToleranceMetres;
            Vector2 plan = new Vector2(stationLocalMeters.x, stationLocalMeters.y);
            float z = stationLocalMeters.z;

            HullMeshDef.Cut best = HullMeshDef.Cut.None;
            float bestSole = float.NegativeInfinity;

            // The def's own levels first: the def is the level authority, and its order breaks a tie.
            BoatInteriorLevel[] levels = def != null ? def.Levels : null;
            if (levels != null)
            {
                for (int i = 0; i < levels.Length; i++)
                {
                    BoatInteriorLevel level = levels[i];
                    if (level == null || !level.IsUsable()) continue;
                    HullMeshDef.Cut cut = mesh.CutawayForDeck(level.Id);
                    if (!cut.Opens) continue;

                    float ceiling = TryEnclosedRowFor(mesh, level.Id, out HullMeshDef.LevelTag row)
                        ? row.CeilingZMeters
                        : 0f;
                    if (!Holds(mesh, level.SoleZMeters, ceiling, level.Outline, plan, z, tolerance)) continue;
                    if (level.SoleZMeters <= bestSole) continue;
                    bestSole = level.SoleZMeters;
                    best = cut;
                    roomId = level.Id;
                }
            }

            // Then the rooms only the mesh knows.
            for (int i = 0; i < mesh.LevelTags.Length; i++)
            {
                HullMeshDef.LevelTag row = mesh.LevelTags[i];
                if (!row.Enclosed || NamesALevel(levels, row.DeckId) || NamesALevel(levels, row.LevelId)) continue;
                HullMeshDef.Cut cut = BoatInterior.LandingCut(mesh, row, out string key);
                if (!cut.Opens) continue;
                if (!Holds(mesh, row.SoleZMeters, row.CeilingZMeters, null, plan, z, tolerance)) continue;
                if (row.SoleZMeters <= bestSole) continue;
                bestSole = row.SoleZMeters;
                best = cut;
                roomId = key;
            }
            return best;
        }

        /// <summary>
        /// Does a room with this sole, ceiling and outline hold the station? On or over its sole (less
        /// the floor tolerance), under its ceiling (or, where the table gives none, on the sole), with
        /// no open deck between its sole and the station, and holding her in plan where an open deck
        /// ties its sole.
        /// </summary>
        private static bool Holds(HullMeshDef mesh, float sole, float ceiling, Vector2[] outline, Vector2 plan,
                                  float z, float tolerance)
        {
            if (z < sole - tolerance) return false;
            if (ceiling > sole) { if (z >= ceiling) return false; }
            else if (z > sole + tolerance) return false;

            for (int i = 0; i < mesh.LevelTags.Length; i++)
            {
                HullMeshDef.LevelTag open = mesh.LevelTags[i];
                if (open.Enclosed || open.SoleZMeters > z + tolerance) continue;   // a room, or a deck over her head
                if (open.SoleZMeters > sole + tolerance) return false;             // she stands on that deck, over this room
                if (Mathf.Abs(open.SoleZMeters - sole) > tolerance) continue;      // a deck below this room's floor
                // A deck at this room's own height: only the outline can say which she is on.
                if (outline == null || outline.Length < 3 || !DeckAreaMath.Contains(outline, plan)) return false;
            }
            return true;
        }

        /// <summary>The enclosed row of the level table that roofs def level <paramref name="levelId"/>:
        /// the row that names it, or, on a mesh whose lift sets key a second floor of one space by its
        /// own id, the row that lift set's key names.</summary>
        private static bool TryEnclosedRowFor(HullMeshDef mesh, string levelId, out HullMeshDef.LevelTag row)
        {
            for (int i = 0; i < mesh.LevelTags.Length; i++)
            {
                row = mesh.LevelTags[i];
                if (row.Enclosed && (Same(row.DeckId, levelId) || Same(row.LevelId, levelId))) return true;
            }
            if (!string.IsNullOrEmpty(mesh.LayoutSchema) && mesh.LiftSets != null)
            {
                for (int s = 0; s < mesh.LiftSets.Length; s++)
                {
                    if (!Same(mesh.LiftSets[s].DeckId, levelId)) continue;
                    for (int i = 0; i < mesh.LevelTags.Length; i++)
                    {
                        row = mesh.LevelTags[i];
                        if (row.Enclosed && Same(row.LevelId, mesh.LiftSets[s].Key)) return true;
                    }
                    break;
                }
            }
            row = default;
            return false;
        }

        private static bool NamesALevel(BoatInteriorLevel[] levels, string id)
        {
            if (levels == null || string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < levels.Length; i++)
                if (levels[i] != null && Same(levels[i].Id, id)) return true;
            return false;
        }

        private static bool Same(string a, string b)
            => !string.IsNullOrEmpty(a) && string.Equals(a, b, System.StringComparison.Ordinal);
    }
}
