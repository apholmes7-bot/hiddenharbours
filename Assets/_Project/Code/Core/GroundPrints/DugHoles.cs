using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>One hole a shovel left in the ground: where, and the game time it was dug.</summary>
    public struct DugHole
    {
        /// <summary>World position of the hole's centre, on the ground.</summary>
        public Vector2 Position;

        /// <summary>Game time (clock seconds) the blade went in.</summary>
        public double DugAtSeconds;

        /// <summary>A stable id for this hole within its day (the same hole answers the same key on every
        /// ask), so a reader can remember what it worked out about it. 0 = no key.</summary>
        public ulong Key;
    }

    /// <summary>
    /// Anything that knows of holes dug today without being told each time, because they follow from the
    /// clock: the villagers digging on the flats (their day is a function of (world seed, day)). The
    /// reader asks on its slow tick; a source appends the holes dug so far today and nothing else.
    /// Deterministic, never saved (rule 5).
    /// </summary>
    public interface IDugHoleSource
    {
        /// <summary>Append every hole this source's diggers have dug today at or before
        /// <paramref name="nowSeconds"/>. Must not clear <paramref name="into"/>; must not allocate per call.</summary>
        void CollectDugHoles(double nowSeconds, List<DugHole> into);
    }

    /// <summary>The registry of live <see cref="IDugHoleSource"/>s (the <see cref="GroundZoneSources"/> idiom).</summary>
    public static class DugHoleSources
    {
        private static readonly List<IDugHoleSource> Sources = new List<IDugHoleSource>(2);

        public static int Count => Sources.Count;

        public static void Register(IDugHoleSource source)
        {
            if (source != null && !Sources.Contains(source)) Sources.Add(source);
        }

        public static void Unregister(IDugHoleSource source) => Sources.Remove(source);

        public static void Clear() => Sources.Clear();

        /// <summary>Every registered source's holes, appended to <paramref name="into"/>.</summary>
        public static void CollectAll(double nowSeconds, List<DugHole> into)
        {
            for (int i = 0; i < Sources.Count; i++)
                if (Sources[i] != null) Sources[i].CollectDugHoles(nowSeconds, into);
        }
    }

    /// <summary>The two ground questions a hole asks. Live: <see cref="LiveDugHoleGround"/>; tests hand in a fake.</summary>
    public interface IDugHoleGround
    {
        /// <summary>The ground's height at <paramref name="worldPos"/> (metres, the tide's datum).</summary>
        float ElevationAt(Vector2 worldPos);

        /// <summary>The sea's height at game time <paramref name="totalSeconds"/> (metres, same datum).</summary>
        float WaterLevelAt(double totalSeconds);

        /// <summary>False when there is no tide or no ground to ask (a hole then lasts the day).</summary>
        bool HasTide { get; }
    }

    /// <summary>The live answers: the active region's tidal terrain and the deterministic tide.</summary>
    public sealed class LiveDugHoleGround : IDugHoleGround
    {
        public bool HasTide => GameServices.TidalTerrain != null && GameServices.Environment != null;

        public float ElevationAt(Vector2 worldPos)
        {
            ITidalTerrain terrain = GameServices.TidalTerrain;
            return terrain != null ? terrain.ElevationAt(worldPos) : float.PositiveInfinity;
        }

        public float WaterLevelAt(double totalSeconds)
        {
            IEnvironmentService environment = GameServices.Environment;
            return environment != null ? environment.WaterLevelAt(totalSeconds) : float.NegativeInfinity;
        }
    }

    /// <summary>
    /// <b>How long a dug hole lasts, and where a villager's holes are.</b> Pure arithmetic over the clock,
    /// the tide and the ground, so a hole's end is known the moment it is dug and reads the same after a
    /// load as before (rule 5).
    ///
    /// <para><b>The rule</b> (owner, 2026-11-22): a hole stays for the rest of the game day it was dug
    /// in, or until the tide covers its spot, whichever comes first. The tide is read forward from the dig
    /// at a fixed step (<see cref="DugHoleSettings.TideCheckGameMinutes"/>) until the first reading with
    /// the sea at or over the ground (the print layer's own wash test, <see cref="GroundPrintMath.IsWashed"/>),
    /// so a flood that comes and goes again the same day still fills the hole.</para>
    /// </summary>
    public static class DugHoleMath
    {
        /// <summary>A rail on the tide walk: no hole reads the tide more often than this in one ask.</summary>
        public const int MaxTideReads = 4096;

        /// <summary>The start of the game day holding <paramref name="seconds"/>, in clock seconds.</summary>
        public static double DayStartSeconds(double seconds, double secondsPerDay)
        {
            if (secondsPerDay <= 0.0) return 0.0;
            return System.Math.Floor(seconds / secondsPerDay) * secondsPerDay;
        }

        /// <summary>The end (the next midnight) of the game day holding <paramref name="seconds"/>.</summary>
        public static double DayEndSeconds(double seconds, double secondsPerDay)
            => secondsPerDay <= 0.0 ? double.PositiveInfinity : DayStartSeconds(seconds, secondsPerDay) + secondsPerDay;

        /// <summary>Clock seconds in one game minute, for a day of <paramref name="secondsPerDay"/>.</summary>
        public static double SecondsPerGameMinute(double secondsPerDay) => secondsPerDay / (24.0 * 60.0);

        /// <summary>
        /// The game time a hole dug at <paramref name="dugAtSeconds"/> on <paramref name="position"/> is gone:
        /// the end of its day, or the first tide reading (every <paramref name="tideStepSeconds"/>) that has the
        /// sea over it, whichever is first. A hole dug under water is gone at once (returns the dig time).
        /// With no tide to ask it lasts the day.
        /// </summary>
        public static double EndsAt(IDugHoleGround ground, Vector2 position, double dugAtSeconds,
                                    double secondsPerDay, double tideStepSeconds)
        {
            double dayEnd = DayEndSeconds(dugAtSeconds, secondsPerDay);
            if (ground == null || !ground.HasTide) return dayEnd;

            float elevation = ground.ElevationAt(position);
            if (GroundPrintMath.IsWashed(ground.WaterLevelAt(dugAtSeconds), elevation)) return dugAtSeconds;
            if (tideStepSeconds <= 0.0) return dayEnd;

            double t = dugAtSeconds;
            for (int i = 0; i < MaxTideReads; i++)
            {
                t += tideStepSeconds;
                if (t >= dayEnd) return dayEnd;
                if (GroundPrintMath.IsWashed(ground.WaterLevelAt(t), elevation)) return t;
            }
            return t;
        }

        /// <summary>Is a hole dug at <paramref name="dugAtSeconds"/> and gone at <paramref name="endsAtSeconds"/>
        /// there at <paramref name="nowSeconds"/>? (Not before it was dug: a clock sought backwards hides it.)</summary>
        public static bool IsOpen(double dugAtSeconds, double endsAtSeconds, double nowSeconds)
            => nowSeconds >= dugAtSeconds && nowSeconds < endsAtSeconds;

        /// <summary>
        /// The holes a villager has dug at one place on the flats by <paramref name="nowSeconds"/>: the first
        /// <see cref="DugHoleSettings.NpcFirstHoleGameMinutes"/> after they arrive, then one every
        /// <see cref="DugHoleSettings.NpcHoleEveryGameMinutes"/> until they leave, at most
        /// <see cref="DugHoleSettings.NpcMaxHolesPerPlace"/>. Each is in front of them
        /// (<see cref="DugHoleSettings.NpcHoleReachMetres"/> along their <paramref name="headingDegrees"/>, a
        /// compass bearing across the ground) and scattered round that point by up to
        /// <see cref="DugHoleSettings.NpcHoleScatterMetres"/>, from <paramref name="seed"/> alone. Pure; appends
        /// to <paramref name="into"/> and returns how many it added.
        /// </summary>
        public static int AppendPlaceHoles(double arriveSeconds, double leaveSeconds, double nowSeconds,
                                           Vector2 standAt, float headingDegrees, uint seed,
                                           in DugHoleSettings s, double secondsPerDay, List<DugHole> into)
        {
            if (into == null || s.NpcMaxHolesPerPlace <= 0) return 0;
            double minute = SecondsPerGameMinute(secondsPerDay);
            double first = arriveSeconds + System.Math.Max(0.0, s.NpcFirstHoleGameMinutes) * minute;
            double every = System.Math.Max(1.0, s.NpcHoleEveryGameMinutes) * minute;

            float rad = headingDegrees * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));   // 0 = North, clockwise
            Vector2 reach = forward * s.NpcHoleReachMetres;

            int added = 0;
            for (int k = 0; k < s.NpcMaxHolesPerPlace; k++)
            {
                double t = first + k * every;
                if (t >= leaveSeconds || t > nowSeconds) break;

                uint h = Mix(seed, (uint)k);
                float angle = Unit(h) * Mathf.PI * 2f;
                float radius = Mathf.Sqrt(Unit(Mix(h, 0x9E3779B9u))) * s.NpcHoleScatterMetres;
                Vector2 ground = reach + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                into.Add(new DugHole
                {
                    Position = standAt + ToWorld(ground),
                    DugAtSeconds = t,
                    Key = ((ulong)seed << 32) | (uint)(k + 1),
                });
                added++;
            }
            return added;
        }

        /// <summary>A ground-metres offset as a world offset (the ¾ view squashes the ground's depth).</summary>
        public static Vector2 ToWorld(Vector2 groundOffset)
            => new Vector2(groundOffset.x, groundOffset.y * IsoGround.GroundDepthScale);

        // A small integer mixer (the murmur3 finaliser over a fold): the scatter is a function of the seed only.
        private static uint Mix(uint a, uint b)
        {
            uint h = a ^ (b * 0x85EBCA6Bu + 0x6B43A9B5u);
            h ^= h >> 16; h *= 0x85EBCA6Bu;
            h ^= h >> 13; h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }

        private static float Unit(uint h) => (h & 0x00FFFFFFu) / 16777216f;
    }
}
