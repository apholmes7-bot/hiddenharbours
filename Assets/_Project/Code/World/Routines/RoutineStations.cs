using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// One named place a routine can send someone: <b>where it is, which way you stand when you get
    /// there, and whether it is indoors</b>. The "location anchor" of
    /// <c>design/npcs-and-routines.md</c> §2.2, and the seam that keeps a routine asset free of
    /// coordinates.
    ///
    /// <para><b>⭐ Every position here is DERIVED by the region builder, never typed in.</b> A dooryard
    /// comes out of its building's own site, a counter out of the store's, a room point out of the baked
    /// <see cref="InteriorFootprint"/>. A hand-typed station would be a second copy of the village's
    /// geometry, and the moment the owner moves a building the copy stops agreeing (#345). The routine
    /// names the station; the region owns where it is.</para>
    /// </summary>
    [System.Serializable]
    public struct RoutineStationEntry
    {
        [Tooltip("Stable id a routine entry names — station.<region>.<name>, e.g. " +
                 "station.st_peters.store_counter.")]
        public string Id;

        [Tooltip("Where it is, in world metres. DERIVED by the builder from the region's own constants.")]
        public Vector2 Position;

        [Tooltip("Which way the villager faces once they have arrived (degrees, 0 = North, clockwise). " +
                 "This is the station's STANCE: the wharf head looks out at the water, the counter looks " +
                 "at the customer, a dooryard looks out at the green. While walking, the facing follows " +
                 "the walk instead — nobody arrives sideways.")]
        public float StandHeadingDegrees;

        [Tooltip("The interior this station is INSIDE, or empty for an outdoor station. An occupant of an " +
                 "indoor station is drawn only while the player shares that interior — the reveal's own " +
                 "rule — and the walk to it goes through the building's real drawn door.")]
        public BuildingInterior Interior;

        [Tooltip("Which lane junction this station hangs off, BY NAME (a name from RoutineLanes). Named " +
                 "rather than indexed so the two tables cannot silently disagree when one is reordered, " +
                 "and stated rather than guessed: 'the nearest node' would happily pick one on the far " +
                 "side of a wall.")]
        public string LaneNodeName;

        [Tooltip("One line on why the station is here. Authoring note only.")]
        public string Why;

        /// <summary>True when standing here means being inside a building.</summary>
        public bool Indoors => Interior != null;
    }

    /// <summary>
    /// <b>The region's station table</b> — every named place its routines can send anyone, standing in
    /// the scene as one object the builder writes and the villagers read.
    ///
    /// <para><b>Builder-wired, not scene-wired.</b> Nothing here is dragged in by hand: the whole table
    /// is emitted by the region builder from the region's own constants, every run, and an EditMode test
    /// pins the derivation rather than the numbers. That is the standing rule for everything in
    /// <c>StPeters.unity</c> — the freezer, the wet bucket, the five islanders, the store counter — and a
    /// station hand-placed in the scene would be silently undone by the next build.</para>
    ///
    /// <para><b>Lookup is a linear scan and that is the right choice.</b> A region has on the order of a
    /// dozen stations, resolution happens once per villager at spawn (never per frame), and a dictionary
    /// would be a second copy of the table to keep in step with the serialized one. The scan is ordinal —
    /// ids are data, not display text.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoutineStations : MonoBehaviour
    {
        /// <summary>The conventional name of the object the builder hangs this on.</summary>
        public const string ObjectName = "RoutineStations";

        [Tooltip("Every named place this region's routines can send someone. Written by the region " +
                 "builder — see the class remarks on why this is not hand-authored.")]
        [SerializeField] private RoutineStationEntry[] _stations = System.Array.Empty<RoutineStationEntry>();

        /// <summary>How many stations this region declares.</summary>
        public int Count => _stations != null ? _stations.Length : 0;

        /// <summary>The table itself, for tests, tooling and the planner.</summary>
        public RoutineStationEntry[] Stations => _stations ?? System.Array.Empty<RoutineStationEntry>();

        /// <summary>Wire the table in one call (the region builder). Unity serializes what a component
        /// sets on itself, so this survives into the saved scene.</summary>
        public void Configure(RoutineStationEntry[] stations) =>
            _stations = stations ?? System.Array.Empty<RoutineStationEntry>();

        /// <summary>
        /// Find a station by id. Returns false — having written nothing — for an id this region does not
        /// declare, which is an authoring error the caller reports by name rather than a crash.
        /// </summary>
        public bool TryFind(string id, out RoutineStationEntry station)
        {
            station = default;
            if (string.IsNullOrEmpty(id) || _stations == null) return false;
            for (int i = 0; i < _stations.Length; i++)
            {
                if (!string.Equals(_stations[i].Id, id, System.StringComparison.Ordinal)) continue;
                station = _stations[i];
                return true;
            }
            return false;
        }

        /// <summary>Index of a station by id, or −1. Same scan as <see cref="TryFind"/>, for callers that
        /// want to talk about the table rather than a copy of one row.</summary>
        public int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id) || _stations == null) return -1;
            for (int i = 0; i < _stations.Length; i++)
                if (string.Equals(_stations[i].Id, id, System.StringComparison.Ordinal)) return i;
            return -1;
        }

#if UNITY_EDITOR
        /// <summary>Draw the stations in the Scene view. Every failure mode of a station table is silent —
        /// a villager sent to a point two metres inside a wall walks there perfectly — so the geometry is
        /// made visible where it is authored, the way <c>BuildingInterior</c> draws its own walls.</summary>
        void OnDrawGizmosSelected()
        {
            if (_stations == null) return;
            foreach (RoutineStationEntry s in _stations)
            {
                Gizmos.color = s.Indoors
                    ? new Color(0.95f, 0.70f, 0.30f, 0.9f)
                    : new Color(0.35f, 0.90f, 0.55f, 0.9f);
                Gizmos.DrawWireSphere(s.Position, 0.6f);

                // A stub in the stance direction, so "which way do they face here" is visible too.
                float rad = (90f - s.StandHeadingDegrees) * Mathf.Deg2Rad;
                Gizmos.DrawLine(s.Position,
                                s.Position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 1.4f);
            }
        }
#endif
    }
}
