using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One need as saved</b> (needs slice 1): the level it had at one instant and that instant on the
    /// game clock. The level NOW is worked out from the clock (<see cref="NeedsMath.LevelAt"/>), never
    /// ticked up and stored, so a save holds two numbers per need and a reload lands on exactly the level
    /// the player left (rule 5). Flat and JsonUtility-friendly.
    /// </summary>
    [Serializable]
    public struct NeedStateDto
    {
        /// <summary>The marker for "not yet anchored to the clock": the need holds <see cref="Level"/>
        /// until the first slow tick stamps the instant it starts rising from. A brand-new game starts
        /// here, so the hours before the opening never count against the player.</summary>
        public const double Unstamped = -1d;

        /// <summary>0..1 at <see cref="SinceGameSeconds"/>.</summary>
        public float Level;

        /// <summary>The game clock (<c>IGameClock.TotalSeconds</c>) the level was true at, or
        /// <see cref="Unstamped"/>.</summary>
        public double SinceGameSeconds;

        public NeedStateDto(float level, double sinceGameSeconds)
        {
            Level = level;
            SinceGameSeconds = sinceGameSeconds;
        }

        /// <summary>Fed or rested, not yet anchored to the clock.</summary>
        public static NeedStateDto Fresh => new NeedStateDto(0f, Unstamped);

        public bool IsStamped => SinceGameSeconds >= 0d;
    }

    /// <summary>
    /// <b>The local player's needs, as saved</b> (needs slice 1, save schema v19). One
    /// <see cref="NeedStateDto"/> per <see cref="NeedKind"/>. Written only through
    /// <see cref="INeedsService"/>.
    /// </summary>
    [Serializable]
    public sealed class PlayerNeedsDto
    {
        public NeedStateDto Hunger = NeedStateDto.Fresh;
        public NeedStateDto Tiredness = NeedStateDto.Fresh;

        /// <summary>Fed and rested as of <paramref name="gameSeconds"/>: what an older save loads as.</summary>
        public static PlayerNeedsDto FedAndRestedAt(double gameSeconds)
        {
            double at = gameSeconds >= 0d ? gameSeconds : NeedStateDto.Unstamped;
            return new PlayerNeedsDto
            {
                Hunger = new NeedStateDto(0f, at),
                Tiredness = new NeedStateDto(0f, at),
            };
        }

        public NeedStateDto Get(NeedKind need) => need == NeedKind.Tiredness ? Tiredness : Hunger;

        public void Set(NeedKind need, NeedStateDto state)
        {
            if (need == NeedKind.Tiredness) Tiredness = state;
            else Hunger = state;
        }
    }
}
