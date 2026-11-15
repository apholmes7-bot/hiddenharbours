using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The local player's cleanliness, as saved</b> (cleanliness slice 1, save schema v21). Like a need
    /// (<see cref="NeedStateDto"/>): the DIRT at one instant and that instant on the game clock, plus what
    /// the player was doing from then on (a stretch of dirty work, or washing at a station) and when that
    /// stretch began. The dirt NOW is worked out from the clock (<see cref="CleanlinessMath.DirtAt"/>),
    /// never ticked and stored, so a reload lands on exactly the dirt the player left (rule 5).
    /// Flat and JsonUtility-friendly. Written only through <see cref="ICleanlinessService"/>.
    /// </summary>
    [Serializable]
    public sealed class PlayerCleanlinessDto
    {
        /// <summary>0 clean .. 1 filthy, at <see cref="SinceGameSeconds"/>.</summary>
        public float Dirt;

        /// <summary>The game clock the dirt was true at, or <see cref="NeedStateDto.Unstamped"/>: a new
        /// game holds its dirt until the first slow tick anchors it.</summary>
        public double SinceGameSeconds = NeedStateDto.Unstamped;

        /// <summary>The <see cref="DirtActivity"/> going on since <see cref="StretchSinceGameSeconds"/>
        /// (stored as its integer; 0 = none).</summary>
        public int Activity;

        /// <summary>The <see cref="WashStationDef.Id"/> being washed at, or empty. Washing and an activity
        /// never run together: whichever began last wins.</summary>
        public string WashStationId = "";

        /// <summary>When the current activity or wash began (its guard window,
        /// <see cref="CleanlinessSettings.MaxStretchGameMinutes"/>, counts from here, so re-anchoring at a
        /// save write never restarts it), or <see cref="NeedStateDto.Unstamped"/> with none.</summary>
        public double StretchSinceGameSeconds = NeedStateDto.Unstamped;

        public bool IsStamped => SinceGameSeconds >= 0d;

        public bool IsWashing => !string.IsNullOrEmpty(WashStationId);

        public DirtActivity ActivityKind => (DirtActivity)Activity;

        /// <summary>Clean, idle and anchored at <paramref name="gameSeconds"/>: what an older save loads as.
        /// A negative time leaves it unanchored (the first slow tick anchors it).</summary>
        public static PlayerCleanlinessDto CleanAt(double gameSeconds) => new PlayerCleanlinessDto
        {
            Dirt = 0f,
            SinceGameSeconds = gameSeconds >= 0d ? gameSeconds : NeedStateDto.Unstamped,
        };
    }
}
