using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>What became of a wall: kept as the scene had it, re-lined on the ground, banked by the main beach, the
    /// beach's return, its toe lifted, retired, or held for a wall that is not built yet.</summary>
    public enum CliffWallStatus
    {
        Kept = 0,
        Relined = 1,
        Banked = 2,
        Return = 3,
        ToeLifted = 4,
        Retired = 5,
        Held = 6,
    }

    /// <summary>Where a station's height came from: the scene's own (kept), or read off the ground file's import (measured).</summary>
    public enum CliffStationSource
    {
        Kept = 0,
        Measured = 1,
    }

    /// <summary>
    /// <b>ONE CLIFF WALL, BY ITS REAL ID.</b> One asset per wall (<c>wall.stp_NNN</c>): one chunk of standing wall in the
    /// region scene, the <c>CliffWall_&lt;class&gt;_&lt;aspect&gt;_&lt;batter&gt;_NNN</c> object whose last three digits are
    /// its id. Its lines are the chunk's stations, a station every 0.25 m of brow, exactly as the scene's
    /// <c>CliffWallSurface</c> holds them, so the builder draws from the Defs and the scene and the Defs agree to the bit
    /// (terrain PR 5w, amendment 1 §4.5).
    ///
    /// <para><b>Runs.</b> A wall that <see cref="Follows"/> another starts on that wall's last station; its offsets along
    /// the run and the run's row basis come from the chunk math over the whole run (<c>StPetersCliffWalls.ChunksOfDefs</c>).
    /// A wall that follows none starts a run.</para>
    ///
    /// <para><b>Ids are append-only.</b> A wall cut from another records <see cref="SplitFrom"/>; a retired id keeps its Def
    /// and is never used again; a held id (the Head's ring, 079 to 146) stands in no scene and carries the lines it was
    /// measured on and the face it needs, as a forecast.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Cliff Wall", fileName = "CliffWall")]
    public class CliffWallDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (wall.stp_NNN): NNN is the last three digits of the scene object's name.")]
        public string Id = "wall.example_000";
        public CliffWallStatus Status = CliffWallStatus.Kept;
        [Tooltip("The wall it was cut from (its id), or empty.")]
        public string SplitFrom = "";
        [Tooltip("The live wall whose last station is this one's first (its id): the run it carries on. Empty where a run starts.")]
        public string Follows = "";

        [Header("Its face")]
        [Tooltip("The rock its face is baked in.")]
        public string Rock = "sandstone";
        public CoastClass Class = CoastClass.Cliff;
        [Tooltip("The face's aspect: W, SW, S, SE or E (CliffCatalog.Aspects).")]
        public string Aspect = "S";
        [Tooltip("The face's batter: wall, steep, ramp or bank (CliffCatalog.Batters).")]
        public string Batter = "steep";

        [Header("Its lines, a station every 0.25 m of brow")]
        [Tooltip("The brow at each station (world x, y).")]
        public Vector2[] Brow = new Vector2[0];
        [Tooltip("The toe at each station, in plan (world x, y).")]
        public Vector2[] Toe = new Vector2[0];
        [Tooltip("The brow's height over the toe at each station (m).")]
        public float[] DropMetres = new float[0];
        [Tooltip("The toe's height at each station (m above chart datum).")]
        public float[] ToeElevations = new float[0];

        [Header("Where each station came from")]
        [Tooltip("Each station's index on the wall it came from (its k).")]
        public int[] Stations = new int[0];
        public CliffStationSource[] BrowFrom = new CliffStationSource[0];
        public CliffStationSource[] ToeFrom = new CliffStationSource[0];
        [Tooltip("Pass 9's height at each station's brow (m), as the measure read it: a kept station's ground must stay within " +
                 "the list's 10 cm of it.")]
        public float[] BrowPass9 = new float[0];
        [Tooltip("Pass 9's height at each station's toe (m).")]
        public float[] ToePass9 = new float[0];

        [Header("Its source")]
        [Tooltip("The list that gave the wall its status (the Art desk's walls by real id).")]
        public string SourceList = "";
        public string SourceListSha256 = "";
        [Tooltip("The list's row for the wall, or for the wall it was cut from; -1 where the list has none (a held wall).")]
        public int SourceRow = -1;
        [Tooltip("The ground its measured stations were read off.")]
        public string MeasuredOn = "";

        [Header("A held wall: a forecast")]
        [Tooltip("The face it needs, where the kit has none for it yet.")]
        public string FaceNeeded = "";

        [TextArea] public string Why = "";

        /// <summary>True when the wall stands in the scene: neither retired nor held.</summary>
        public bool IsLive => Status != CliffWallStatus.Retired && Status != CliffWallStatus.Held;

        /// <summary>The wall's real id: the last three digits of its id and of its scene object's name.</summary>
        public string RealId => Id != null && Id.Length >= 3 ? Id.Substring(Id.Length - 3) : "";
    }
}
