using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>What somebody on the beach is doing, as far as WHERE THEY ARE goes. It picks the lines
    /// of the <see cref="BeachDef"/> they use and whether they stay put or move along.</summary>
    public enum BeachActivityKind
    {
        /// <summary>Stand (later: sit) on a spot line and look out.</summary>
        Watch = 0,
        /// <summary>Walk the water's edge from one promenade line to the next, pausing at each.</summary>
        Stroll = 1,
        /// <summary>Work one place out on the flats while they are bare.</summary>
        Dig = 2,
        /// <summary>Leave the dry sand, go in to the shallows of a <see cref="BeachDef.Shallows"/> line
        /// and follow the water's edge as the tide moves it, then come back out.</summary>
        Wade = 3,
        /// <summary>Several people round one place on a <see cref="BeachDef.Gatherings"/> line: a
        /// supper on the sand, a fire. The day is decided ONCE for everybody, and so is the place.</summary>
        Gather = 4,
    }

    /// <summary>One line of talk on the beach: who says it and what they say.</summary>
    [Serializable]
    public struct BeachTalkLine
    {
        [Tooltip("Who says it. They must be at the gathering or the exchange is not started.")]
        public NpcDef Speaker;

        [TextArea(2, 3)]
        [Tooltip("What they say. One bubble of it.")]
        public string Text;
    }

    /// <summary>A short exchange between people at one gathering. One of them is had per gathering,
    /// chosen by the day.</summary>
    [Serializable]
    public struct BeachTalk
    {
        public BeachTalkLine[] Lines;
    }

    /// <summary>
    /// <b>ONE THING PEOPLE DO ON A BEACH</b> — and when the sea, the sky and the calendar let them.
    ///
    /// <para>A villager with a block of free time (<see cref="Replaces"/>) takes the first activity in
    /// the library whose gates pass that day, and spends that block on the beach instead of where their
    /// authored day put them. Work, meals and sleep are never replaced: only the tag named here is.</para>
    ///
    /// <para><b>Every gate is a function of game time and the seed</b> (rule 5). The tide, the wind and
    /// the fog are asked at the hours of the stay, not sampled "now", so the same day always gives the
    /// same beach, and a stay ends at the hour a gate first fails: people go home when the weather turns
    /// or the tide comes over the flats.</para>
    ///
    /// <para><b>What the sim does not have</b> (and the stand-ins used): no rain, so fog and a rough sea
    /// stand for a poor day; no temperature, so season and hour stand for warmth.</para>
    ///
    /// <para>One activity per file, stable id <c>beachactivity.snake_case</c> (rule 2). Every number is
    /// here, not in code (rule 6).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Beach Activity", fileName = "BeachActivity")]
    public sealed class BeachActivityDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (beachactivity.snake_case).")]
        public string Id = "beachactivity.example";

        [Tooltip("Shown in tools and test output. Not player-facing yet.")]
        public string DisplayName = "";

        [Tooltip("The beach it happens on.")]
        public BeachDef Beach;

        [Tooltip("Where people are while they do it.")]
        public BeachActivityKind Kind = BeachActivityKind.Watch;

        [Header("Who and which part of their day")]
        [Tooltip("The kind of block this may take over. Free time only: leave it Recreation.")]
        public RoutineActivity Replaces = RoutineActivity.Recreation;

        [Tooltip("The tag the block carries while they are on the beach.")]
        public RoutineActivity Tag = RoutineActivity.Recreation;

        [Tooltip("Whose days it may vary. Empty = anybody who lives in the beach's region.")]
        public RoutineDef[] Who = Array.Empty<RoutineDef>();

        [Tooltip("Of the days every gate passes, the share they actually go (0..1). Decided per person " +
                 "per day from the world seed, so it is the same every time that day is played.")]
        [Range(0f, 1f)] public float ChancePerDay = 0.5f;

        [Header("When: clock and calendar")]
        [Tooltip("Nobody is on the beach for this before this hour.")]
        [Range(0f, 24f)] public float EarliestHour = 6f;

        [Tooltip("Everybody has left by this hour.")]
        [Range(0f, 24f)] public float LatestHour = 21.5f;

        public bool InEarlySpring = true;
        public bool InHighSummer = true;
        public bool InTheTurn = true;
        public bool InHardWinter = false;

        [Header("When: the sea and the sky, asked across the whole stay")]
        [Tooltip("Lowest water level (metres, the tide's own datum) it happens at.")]
        public float MinWaterLevelMetres = -100f;

        [Tooltip("Highest water level it happens at. Digging wants the flats bare: set this low.")]
        public float MaxWaterLevelMetres = 100f;

        [Tooltip("Least wind it happens in (the wind law's own strength units). A kite wants some.")]
        [Min(0f)] public float MinWindStrength = 0f;

        [Tooltip("Most wind it happens in.")]
        [Min(0f)] public float MaxWindStrength = 1000f;

        [Tooltip("Roughest sea it happens in (0 calm .. 1 storm). The stand-in for a poor day.")]
        [Range(0f, 1f)] public float MaxSeaState01 = 0.5f;

        [Tooltip("Thickest fog it happens in, as visibility (1 clear .. 0 nothing). Nobody watches " +
                 "water they cannot see.")]
        [Range(0f, 1f)] public float MinVisibility01 = 0.5f;

        [Tooltip("How often the gates are asked across the stay, in game minutes.")]
        [Min(1f)] public float GateSampleMinutes = 10f;

        [Header("Where on the sand")]
        [Tooltip("How far above the highest water of the stay people keep, in metres of ground height. " +
                 "This is what keeps everybody above the water on a flood.")]
        [Min(0f)] public float ClearanceMetres = 0.3f;

        [Tooltip("Of the dry part of their line (0 = the top, 1 = as near the water as the clearance " +
                 "allows), the nearest the top they settle.")]
        [Range(0f, 1f)] public float MinDepth01 = 1f;

        [Tooltip("...and the nearest the water. Each person's own place between the two is seeded.")]
        [Range(0f, 1f)] public float MaxDepth01 = 1f;

        [Tooltip("The line must have at least this much dry ground or the activity is off (metres). " +
                 "Digging wants the flats properly bare, not a strip.")]
        [Min(0f)] public float MinDryMetres = 0f;

        [Tooltip("How far apart people doing this keep when they would otherwise share a place, in " +
                 "metres up the sand from one another.")]
        [Min(0f)] public float PersonSpacingMetres = 1.2f;

        [Tooltip("Stroll only: how many walkers have a lane of their own, side by side along the " +
                 "shore, each PersonSpacingMetres from the next. More walkers than this follow behind.")]
        [Min(1)] public int WalkersAbreast = 4;

        [Header("How long")]
        [Tooltip("Not worth the walk for less than this, in game minutes on the beach.")]
        [Min(0f)] public float MinStayMinutes = 30f;

        [Tooltip("Stroll only: how long they pause at each point of the walk, in game minutes.")]
        [Min(0f)] public float LingerMinutes = 8f;

        [Header("In the water (Wade only)")]
        [Tooltip("The shallowest water they stand in, in metres over the ground.")]
        [Min(0f)] public float MinWadeDepthMetres = 0.08f;

        [Tooltip("The deepest water they choose to stand in. Each standing place is seeded between the two.")]
        [Min(0f)] public float MaxWadeDepthMetres = 0.25f;

        [Tooltip("Never deeper than this at any moment, walking or standing, whatever the tide does " +
                 "while they are in. A place that would break it is not taken.")]
        [Min(0f)] public float DeepestWadeMetres = 0.4f;

        [Tooltip("How long they are in the water, in game minutes, before coming back up the sand.")]
        [Min(0f)] public float WadeMinutes = 25f;

        [Tooltip("How far to either side of their line they wander along the shore, in metres. Keep it " +
                 "well under half the distance between the beach's shallows lines.")]
        [Min(0f)] public float WadeWanderMetres = 0.7f;

        [Header("Together (Gather only)")]
        [Tooltip("How far from the middle of the gathering each person is, in metres.")]
        [Min(0.5f)] public float GatherRadiusMetres = 1.5f;

        [Tooltip("How many places there are round the middle. More people than this make a second, " +
                 "wider ring, PersonSpacingMetres further out.")]
        [Min(2)] public int GatherPlaces = 6;

        [Tooltip("The part of the circle left open, as a compass bearing from the middle (0 = North, " +
                 "clockwise). Places are shared out evenly from here, so with fewer people than places " +
                 "the gap is on this side.")]
        [Range(0f, 360f)] public float GatherOpenBearingDegrees = 270f;

        [Tooltip("What sits in the middle while anybody is there (a fire pit, later a blanket). None = " +
                 "nothing is put down.")]
        public Sprite CentreSprite;

        [Tooltip("The thing in the middle gives light: a fire. The glow fades in with the dark by itself.")]
        public bool CentreIsLit;

        [Tooltip("Colour of the glow.")]
        [ColorUsage(false, true)] public Color GlowColor = new Color(1f, 0.62f, 0.28f, 1f);

        [Tooltip("How far the glow lights the ground round it, in metres (its pool, not the size of the flame).")]
        [Min(0.1f)] public float GlowRangeMetres = 5f;

        [Tooltip("How strong the glow is, as a multiple of the lighting library's own flame (1 = as the library has it).")]
        [Min(0f)] public float GlowIntensity = 1f;

        [Tooltip("How much it flickers (0 steady .. 1 a strong wobble).")]
        [Range(0f, 1f)] public float GlowFlicker01 = 0.35f;

        [Tooltip("How high above the sand the light sits, in metres.")]
        [Min(0f)] public float GlowHeightMetres = 0.4f;

        [Tooltip("How long the thing in the middle stays after the last person has left, in game " +
                 "minutes: a fire burns down, it is not carried home.")]
        [Min(0f)] public float CentreLingersMinutes = 45f;

        [Tooltip("Things people say to each other here. One exchange per gathering, chosen by the day " +
                 "from those whose speakers are all there.")]
        public BeachTalk[] Talk = Array.Empty<BeachTalk>();

        [Header("Not ready")]
        [Tooltip("Why this activity is authored but does not run yet (a missing animation or prop). " +
                 "Empty = it runs. The data stays so the day it can run is a one-field change.")]
        public string WaitsOn = "";

        /// <summary>Enough authored to plan with, and nothing it waits on.</summary>
        public bool IsRunnable => !string.IsNullOrEmpty(Id) && Beach != null && Beach.IsUsable &&
                                  string.IsNullOrEmpty(WaitsOn);

        /// <summary>Does it happen in <paramref name="season"/> at all.</summary>
        public bool InSeason(Season season)
        {
            switch (season)
            {
                case Season.EarlySpring: return InEarlySpring;
                case Season.HighSummer: return InHighSummer;
                case Season.TheTurn: return InTheTurn;
                case Season.HardWinter: return InHardWinter;
                default: return false;
            }
        }

        /// <summary>Is this routine one of the days it may vary.</summary>
        public bool Admits(string routineId)
        {
            if (Who == null || Who.Length == 0) return true;
            for (int i = 0; i < Who.Length; i++)
                if (Who[i] != null && string.Equals(Who[i].Id, routineId, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
