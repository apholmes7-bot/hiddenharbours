using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Every dug-hole number in one block</b> on <see cref="GameConfig"/> (owner ask 2026-11-22: "there
    /// should be a dark hole left for the remainder of that day, even by working npcs, or until tide washes
    /// over it"). Read through <see cref="Current"/> so an unwired config falls back to <see cref="Fallback"/>.
    /// Real-world sizes in metres; times in IN-GAME minutes.
    /// </summary>
    [Serializable]
    public struct DugHoleSettings
    {
        [Tooltip("Master switch. Off: no holes are drawn (digging itself is unchanged).")]
        public bool Enabled;

        [Header("Look")]
        [Tooltip("Width of a hole across the ground, metres (a clam shovel's bite is about 0.3 to 0.5 m).")]
        [Min(0.05f)] public float WidthMetres;

        [Tooltip("Depth of a hole across the ground as a share of its width, before the 3/4 view squashes it.")]
        [Range(0.3f, 1.5f)] public float DepthOverWidth;

        [Tooltip("The bottom of the hole (sRGB): near black, a shade of the wet mud's hue.")]
        public Color InnerColor;

        [Tooltip("The hole's wall and the wet sand churned round its lip (sRGB): darker than the flat, " +
                 "lighter than the bottom. Its alpha is the halo's strength.")]
        public Color RimColor;

        [Tooltip("Pixels per side of the generated hole sprite (point filtered, pixel art).")]
        [Min(6)] public int TexturePixels;

        [Tooltip("Sorting order of the holes: above the ground and the footprints (-9), below the sea plane " +
                 "(-5, so water over the flat hides a hole before the tide check ends it) and everyone standing.")]
        public int SortingOrder;

        [Header("Life")]
        [Tooltip("How often the tide is read forward from a dig to find when it first covers the hole, in " +
                 "game minutes. Smaller = the hole goes closer to the moment the water reaches it.")]
        [Min(0.25f)] public float TideCheckGameMinutes;

        [Tooltip("How often (REAL seconds) the holes on show are worked out again. A hole's end is known when it " +
                 "is dug, so this only decides how promptly it appears and goes.")]
        [Min(0.05f)] public float RefreshRealSeconds;

        [Tooltip("A new player dig this close (metres) to a hole that is still open digs the same hole again " +
                 "instead of drawing a second one on top.")]
        [Min(0f)] public float SameHoleMetres;

        [Tooltip("Most player holes remembered (session only). When full the oldest is forgotten.")]
        [Min(1)] public int MaxPlayerHoles;

        [Tooltip("Most holes drawn at once (the renderer pool, built once).")]
        [Min(1)] public int PoolSize;

        [Header("Villagers digging on the flats (derived from their day, never saved)")]
        [Tooltip("Game minutes after a villager reaches their place on the flats that the first hole shows.")]
        [Min(0f)] public float NpcFirstHoleGameMinutes;

        [Tooltip("Game minutes between one hole and the next while a villager keeps digging at one place.")]
        [Min(1f)] public float NpcHoleEveryGameMinutes;

        [Tooltip("Most holes one villager leaves at one place.")]
        [Min(0)] public int NpcMaxHolesPerPlace;

        [Tooltip("How far in front of a digging villager their holes are, metres (where the blade goes in).")]
        [Min(0f)] public float NpcHoleReachMetres;

        [Tooltip("How far round that point a villager's holes scatter, metres.")]
        [Min(0f)] public float NpcHoleScatterMetres;

        /// <summary>Clock seconds between tide readings, for a day of <paramref name="secondsPerDay"/>.</summary>
        public double TideStepSeconds(double secondsPerDay)
            => TideCheckGameMinutes * DugHoleMath.SecondsPerGameMinute(secondsPerDay);

        /// <summary>A fresh copy of the shipped numbers.</summary>
        public static DugHoleSettings Default => new DugHoleSettings
        {
            Enabled = true,
            WidthMetres = 0.42f,
            DepthOverWidth = 0.8f,
            InnerColor = new Color(0.07f, 0.05f, 0.035f, 0.95f),
            RimColor = new Color(0.22f, 0.17f, 0.11f, 0.8f),
            TexturePixels = 16,
            SortingOrder = -8,
            TideCheckGameMinutes = 5f,
            RefreshRealSeconds = 0.5f,
            SameHoleMetres = 0.25f,
            MaxPlayerHoles = 32,
            PoolSize = 64,
            NpcFirstHoleGameMinutes = 3f,
            NpcHoleEveryGameMinutes = 20f,
            NpcMaxHolesPerPlace = 6,
            NpcHoleReachMetres = 0.55f,
            NpcHoleScatterMetres = 0.6f,
        };

        private static readonly DugHoleSettings s_fallback = Default;

        /// <summary>The shipped numbers, built once.</summary>
        public static DugHoleSettings Fallback => s_fallback;

        /// <summary>The owner's block when a config is wired, else <see cref="Fallback"/>.</summary>
        public static DugHoleSettings Current
            => GameServices.Config != null ? GameServices.Config.DugHoles : s_fallback;
    }
}
