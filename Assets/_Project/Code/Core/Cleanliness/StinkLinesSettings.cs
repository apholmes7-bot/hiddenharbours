using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The stink lines' numbers</b> (cleanliness slice 2, owner ask 2026-11-15: "wash stations, stink
    /// lines, laundry"). A few small wavy lines rise from the player's head while he is dirty, more of them
    /// the dirtier he gets, and they are gone once he washes. A gentle joke, never a punishment: they change
    /// nothing but the picture. Read through <see cref="Current"/> so an unwired config falls back to
    /// <see cref="Default"/>.
    ///
    /// <para>Nothing here is saved: the lines are worked out from the saved dirt
    /// (<see cref="ICleanlinessService.Dirt"/>) every time they are drawn.</para>
    /// </summary>
    [Serializable]
    public struct StinkLinesSettings
    {
        [Tooltip("Draw stink lines at all.")]
        public bool Enabled;

        [Tooltip("The dirt (0 clean .. 1 filthy) at which the FIRST line appears. Shipped at the curve's " +
                 "grubby line (0.5), so the player sees it coming before villagers react at stinky (0.75).")]
        [Range(0f, 1f)] public float StartAtDirt;

        [Tooltip("The dirt at which the most lines show (MaxLines). Between StartAtDirt and this, the count " +
                 "steps up evenly from MinLines.")]
        [Range(0f, 1f)] public float FullAtDirt;

        [Tooltip("Lines shown right at StartAtDirt.")]
        [Min(1)] public int MinLines;

        [Tooltip("Lines shown at FullAtDirt and above. Also the size of the pool, made once on first need: " +
                 "raising it takes effect after a reload, lowering it at once.")]
        [Min(1)] public int MaxLines;

        [Tooltip("How fast a line rises, in metres per real second.")]
        [Min(0f)] public float RiseMetresPerSecond;

        [Tooltip("How long one line lives, in real seconds, before it starts again at the head.")]
        [Min(0.1f)] public float LifetimeSeconds;

        [Tooltip("Real seconds a line takes to fade in at the start of its life and out at the end.")]
        [Min(0f)] public float FadeSeconds;

        [Tooltip("How far a line wavers sideways as it rises, in metres either side.")]
        [Min(0f)] public float SwayMetres;

        [Tooltip("How many side-to-side wavers a line makes per real second.")]
        [Min(0f)] public float SwayCyclesPerSecond;

        [Tooltip("Where the lines start, from the figure's feet, in metres (y is about the top of his head).")]
        public Vector2 HeadOffsetMetres;

        [Tooltip("Side-by-side gap between lines, in metres: the first rises at the middle, the next to the " +
                 "right, the next to the left, and so on outwards.")]
        [Min(0f)] public float SpreadMetres;

        [Tooltip("The drawn height of one line, in metres. 11/32 m puts one texel of the 11 px line on one " +
                 "game pixel at the locked PPU 32.")]
        [Min(0f)] public float LineHeightMetres;

        [Tooltip("The lines' tint (and peak opacity).")]
        public Color Color;

        [Tooltip("How many sorting orders above the figure the lines draw, inside his own live Y-sorted " +
                 "band, so a wall in front of him hides them with him.")]
        public int OrdersAboveFigure;

        [Tooltip("How often the dirt is read again, in real seconds (it changes slowly). Washing and the " +
                 "grubby/stinky crossings read it at once.")]
        [Min(0.05f)] public float ResampleSeconds;

        [Tooltip("Show the lines while he is below decks in a cabin. Off: hidden there.")]
        public bool ShowBelowDecks;

        /// <summary>The shipped starting numbers (guesses for the owner to tune by feel).</summary>
        public static StinkLinesSettings Default => new StinkLinesSettings
        {
            Enabled = true,
            StartAtDirt = 0.5f,          // the curve's grubby line
            FullAtDirt = 1f,
            MinLines = 1,
            MaxLines = 3,
            RiseMetresPerSecond = 0.2f,
            LifetimeSeconds = 2f,        // rises 0.4 m, then starts again
            FadeSeconds = 0.5f,
            SwayMetres = 0.04f,
            SwayCyclesPerSecond = 0.75f,
            HeadOffsetMetres = new Vector2(0f, 1.6f),
            SpreadMetres = 0.18f,
            LineHeightMetres = 11f / 32f,
            Color = new Color(0.72f, 0.82f, 0.46f, 0.85f),   // a pale, faintly sickly green
            OrdersAboveFigure = 2,
            ResampleSeconds = 1f,
            ShowBelowDecks = false,
        };

        /// <summary>The live numbers: the config's, or <see cref="Default"/> with no config wired.</summary>
        public static StinkLinesSettings Current
            => GameServices.Config != null ? GameServices.Config.StinkLines : Default;
    }
}
