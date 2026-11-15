using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What left a mark. A tyre PAIR is one signal: the renderer draws both tyres from it.</summary>
    public enum GroundPrintKind
    {
        FootLeft = 0,
        FootRight = 1,
        TirePair = 2,
    }

    /// <summary>
    /// The ground classes that TAKE a print. <see cref="None"/> is every ground that does not: rock,
    /// shingle, planks, pavement, floors, water. A terrain zone maps onto one of these through
    /// <see cref="GroundPrintSettings.Zones"/> (data), never in code.
    /// </summary>
    public enum GroundSurface
    {
        None = 0,
        Grass = 1,
        Sand = 2,
        Mud = 3,
        Dirt = 4,
        Snow = 5,
    }

    /// <summary>How prints look on one surface: their ink and how dark a fresh one starts.</summary>
    [Serializable]
    public struct GroundPrintSurfaceStyle
    {
        public GroundSurface Surface;

        [Tooltip("The print's colour on this ground (a darker shade of the ground, from the palette). " +
                 "Alpha is the darkest a fresh print can be; StartStrength scales it.")]
        public Color Tint;

        [Tooltip("How strong a FRESH print is on this ground, 0..1 (mud deep, grass faint).")]
        [Range(0f, 1f)] public float StartStrength;
    }

    /// <summary>One terrain zone (a TerrainPlan ground name such as <c>sand</c>) and the print surface
    /// it is. A zone not listed takes no print.</summary>
    [Serializable]
    public struct GroundZoneSurface
    {
        public string Zone;
        public GroundSurface Surface;
    }

    /// <summary>
    /// <b>Every footprint and tyre-mark number in one block</b> on <see cref="GameConfig"/> (owner ask
    /// 2026-11-15: "the print lasts 24/h with a gradual fade"). Read through <see cref="Current"/> so an
    /// unwired config falls back to <see cref="Fallback"/>. Real-world sizes in metres.
    /// </summary>
    [Serializable]
    public struct GroundPrintSettings
    {
        [Tooltip("Master switch. Off: nobody leaves prints and the renderer draws nothing.")]
        public bool Enabled;

        [Header("Fade")]
        [Tooltip("How long a print lasts, in IN-GAME hours (24 = one game day). It fades the whole time and " +
                 "is gone exactly at this age.")]
        [Min(0.01f)] public float LifetimeGameHours;

        [Tooltip("Shape of the fade: strength = (1 - age/lifetime)^this. 1 = even fade; above 1 fades faster " +
                 "early and lingers faint; below 1 stays crisp longer then goes.")]
        [Min(0.01f)] public float FadeExponent;

        [Tooltip("The fade is drawn in this many flat steps (pixel-art banding, not a smooth ramp). A print " +
                 "stays on its first step until it has faded a full step.")]
        [Min(1)] public int FadeSteps;

        [Header("People")]
        [Tooltip("Distance between one boot print and the next (left to right), metres. A walking adult " +
                 "steps about 0.7 to 0.8 m.")]
        [Min(0.05f)] public float StrideMetres;

        [Tooltip("Boot print length, metres (an adult boot is about 0.28 m).")]
        [Min(0.01f)] public float FootLengthMetres;

        [Tooltip("Boot print width, metres.")]
        [Min(0.01f)] public float FootWidthMetres;

        [Tooltip("How far each boot sits to the side of the walking line, metres (half the stance).")]
        [Min(0f)] public float FootSideOffsetMetres;

        [Header("Vehicles")]
        [Tooltip("A tyre run is laid down in pieces this long, metres. Shorter = smoother curves, more marks.")]
        [Min(0.05f)] public float TireSegmentMetres;

        [Tooltip("Tyre width used when a vehicle's mesh def does not give its own TireWidthMeters, metres.")]
        [Min(0.01f)] public float DefaultTireWidthMetres;

        [Tooltip("Length of one repeat of the tread pattern along a tyre mark, metres.")]
        [Min(0.01f)] public float TreadRepeatMetres;

        [Header("Movement")]
        [Tooltip("A jump further than this between two updates is a teleport (a door, a region load, a " +
                 "boarding): the trail restarts there instead of drawing a line across the map, metres.")]
        [Min(0.1f)] public float TeleportMetres;

        [Tooltip("Most prints one mover may lay in a single update (fast-forward safety).")]
        [Min(1)] public int MaxPrintsPerUpdate;

        [Header("Drawing")]
        [Tooltip("How many prints the active region keeps. When full the OLDEST is dropped first. One mesh, " +
                 "one draw call; four vertices per mark.")]
        [Min(16)] public int Capacity;

        [Tooltip("Prints turn in this many headings (16 = every 22.5 degrees), so they sit on the pixel grid.")]
        [Min(4)] public int HeadingSteps;

        [Tooltip("How often (REAL seconds) the fade is redrawn and the tide is checked. The fade is a pure " +
                 "function of the clock, so a slower tick only makes the steps land a little later.")]
        [Min(0.02f)] public float SlowTickRealSeconds;

        [Tooltip("How many prints the tide check looks at per slow tick (round robin).")]
        [Min(1)] public int TideChecksPerTick;

        [Tooltip("Sorting order of the print layer: above the ground and painted seabed, below the sea plane " +
                 "(so water covering the sand hides the prints under it) and everything standing.")]
        public int SortingOrder;

        [Tooltip("Pixels per side of the generated print texture (left half boot, right half tread).")]
        [Min(4)] public int ShapeTexturePixels;

        [Header("Ground")]
        [Tooltip("How prints look on each surface that takes them.")]
        public GroundPrintSurfaceStyle[] Surfaces;

        [Tooltip("Which terrain zones take prints, and as what surface. A zone not listed takes none " +
                 "(shingle, ledge, talus, rockweed, beds, eelgrass...).")]
        public GroundZoneSurface[] Zones;

        /// <summary>The 24 game hours, in clock seconds, for a day of <paramref name="secondsPerDay"/>.</summary>
        public double LifetimeSeconds(double secondsPerDay)
            => GroundPrintMath.LifetimeSeconds(LifetimeGameHours, secondsPerDay);

        /// <summary>A fresh copy of the shipped numbers (new arrays: safe as a field initialiser).</summary>
        public static GroundPrintSettings Default => new GroundPrintSettings
        {
            Enabled = true,
            LifetimeGameHours = 24f,
            FadeExponent = 1f,
            FadeSteps = 6,
            StrideMetres = 0.75f,
            FootLengthMetres = 0.28f,
            FootWidthMetres = 0.11f,
            FootSideOffsetMetres = 0.09f,
            TireSegmentMetres = 0.5f,
            DefaultTireWidthMetres = 0.22f,
            TreadRepeatMetres = 0.25f,
            TeleportMetres = 3f,
            MaxPrintsPerUpdate = 8,
            Capacity = 2048,
            HeadingSteps = 16,
            SlowTickRealSeconds = 0.5f,
            TideChecksPerTick = 256,
            SortingOrder = -9,
            ShapeTexturePixels = 8,
            Surfaces = new[]
            {
                new GroundPrintSurfaceStyle { Surface = GroundSurface.Grass, Tint = new Color(0.16f, 0.24f, 0.12f, 0.55f), StartStrength = 0.5f },
                new GroundPrintSurfaceStyle { Surface = GroundSurface.Sand,  Tint = new Color(0.45f, 0.36f, 0.24f, 0.6f),  StartStrength = 0.8f },
                new GroundPrintSurfaceStyle { Surface = GroundSurface.Mud,   Tint = new Color(0.16f, 0.12f, 0.08f, 0.7f),  StartStrength = 1f },
                new GroundPrintSurfaceStyle { Surface = GroundSurface.Dirt,  Tint = new Color(0.24f, 0.17f, 0.11f, 0.6f),  StartStrength = 0.8f },
                new GroundPrintSurfaceStyle { Surface = GroundSurface.Snow,  Tint = new Color(0.55f, 0.62f, 0.7f, 0.6f),   StartStrength = 1f },
            },
            Zones = new[]
            {
                new GroundZoneSurface { Zone = "grass", Surface = GroundSurface.Grass },
                new GroundZoneSurface { Zone = "lawn", Surface = GroundSurface.Grass },
                new GroundZoneSurface { Zone = "sedge", Surface = GroundSurface.Grass },
                new GroundZoneSurface { Zone = "marram", Surface = GroundSurface.Sand },
                new GroundZoneSurface { Zone = "sand", Surface = GroundSurface.Sand },
                new GroundZoneSurface { Zone = "ripple", Surface = GroundSurface.Sand },
                new GroundZoneSurface { Zone = "foreshore", Surface = GroundSurface.Sand },
                new GroundZoneSurface { Zone = "silt", Surface = GroundSurface.Mud },
                new GroundZoneSurface { Zone = "marsh", Surface = GroundSurface.Mud },
                new GroundZoneSurface { Zone = "mud", Surface = GroundSurface.Mud },
                new GroundZoneSurface { Zone = "dirt", Surface = GroundSurface.Dirt },
                new GroundZoneSurface { Zone = "path", Surface = GroundSurface.Dirt },
            },
        };

        private static readonly GroundPrintSettings s_fallback = Default;

        /// <summary>The shipped numbers, built once (no allocation per read). Do not mutate its arrays.</summary>
        public static GroundPrintSettings Fallback => s_fallback;

        /// <summary>The owner's block when a config is wired, else <see cref="Fallback"/>. Same
        /// <c>Config != null</c> discipline as <see cref="GameServices"/> (never <c>?.</c> on an Object).</summary>
        public static GroundPrintSettings Current
            => GameServices.Config != null ? GameServices.Config.GroundPrints : s_fallback;

        /// <summary>The surface a terrain zone prints as, or <see cref="GroundSurface.None"/>.</summary>
        public GroundSurface SurfaceForZone(string zone)
        {
            if (string.IsNullOrEmpty(zone) || Zones == null) return GroundSurface.None;
            for (int i = 0; i < Zones.Length; i++)
                if (string.Equals(Zones[i].Zone, zone, StringComparison.Ordinal)) return Zones[i].Surface;
            return GroundSurface.None;
        }

        /// <summary>The style for <paramref name="surface"/>; false when it has none (draw nothing).</summary>
        public bool TryGetStyle(GroundSurface surface, out GroundPrintSurfaceStyle style)
        {
            if (surface != GroundSurface.None && Surfaces != null)
                for (int i = 0; i < Surfaces.Length; i++)
                    if (Surfaces[i].Surface == surface) { style = Surfaces[i]; return true; }
            style = default;
            return false;
        }
    }
}
