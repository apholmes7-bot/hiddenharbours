using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The wash fixtures' look</b> (owner ask 2026-11-17: sinks, toilets and baths should read as what they
    /// are, fill with water, let the player climb in, and froth while he washes). How long a bath or basin
    /// takes to fill and drain, how fast the suds and the shower stream cycle, the climb into the tub, and the
    /// soap bubbles that rise off him while a soaped wash runs. None of it changes the game: the wash itself
    /// (rates, ceilings, soap) is the cleanliness service's. Read through <see cref="Current"/> so an unwired
    /// config falls back to <see cref="Default"/>.
    ///
    /// <para>Nothing here is saved. A fixture that is mid-fill when the game is saved simply comes back dry.</para>
    /// </summary>
    [Serializable]
    public struct WashVisualsSettings
    {
        [Tooltip("Animate the fixtures at all. Off: each stands still in its idle art.")]
        public bool Enabled;

        [Tooltip("Real seconds a bath or basin takes to fill when a wash begins (the tap runs meanwhile).")]
        [Min(0f)] public float FillSeconds;

        [Tooltip("Real seconds it takes to drain after the wash ends.")]
        [Min(0f)] public float DrainSeconds;

        [Tooltip("How many suds frames a second while a soaped wash runs (the bubbles shift; 3 frames loop).")]
        [Min(0f)] public float FrothFramesPerSecond;

        [Tooltip("How many stream frames a second while a shower runs (3 frames loop).")]
        [Min(0f)] public float RunFramesPerSecond;

        [Tooltip("Real seconds a water closet's flush plays when the player gets up.")]
        [Min(0f)] public float FlushSeconds;

        [Tooltip("Froth even when the wash is not soaped. Off (shipped): the suds are the soap's, so a wash " +
                 "without soap is plain water.")]
        public bool FrothWithoutSoap;

        [Tooltip("Real seconds the climb into (or out of) the bath takes.")]
        [Min(0.05f)] public float ClimbSeconds;

        [Tooltip("How high the climb arcs at its middle, in world metres (up the screen): he steps over the rim.")]
        [Min(0f)] public float ClimbArcMetres;

        [Tooltip("Where he stands to climb in, in metres out from the fixture's front, along its facing.")]
        [Min(0f)] public float EntryStandOffMetres;

        [Tooltip("The seated pose's baked seat height (m) used only when the off-deck mount contract is not " +
                 "wired. The contract's DriveSeatZ wins whenever it is there.")]
        [Min(0f)] public float FallbackBakedSeatMetres;

        [Tooltip("Soap bubbles shown around the player while a soaped wash runs (the pool size, made once).")]
        [Min(0)] public int BubbleCount;

        [Tooltip("How fast a bubble rises, in metres per real second.")]
        [Min(0f)] public float BubbleRiseMetresPerSecond;

        [Tooltip("How long one bubble lives, in real seconds, before it starts again.")]
        [Min(0.1f)] public float BubbleLifetimeSeconds;

        [Tooltip("How far either side of him a bubble starts, in metres.")]
        [Min(0f)] public float BubbleSpreadMetres;

        [Tooltip("Where the bubbles start, from his feet, in metres (y is about his shoulders).")]
        public Vector2 BubbleOffsetMetres;

        [Tooltip("A bubble's drawn size, in metres (3/32 is three game pixels at the locked PPU 32).")]
        [Min(0f)] public float BubbleSizeMetres;

        [Tooltip("The bubbles' tint and peak opacity.")]
        public Color BubbleColor;

        [Tooltip("How many sorting orders above the figure the bubbles draw, inside his own Y-sorted band.")]
        public int BubbleOrdersAboveFigure;

        [Tooltip("Real seconds a kitchen fixture's door (the fridge) takes to swing wide open, or shut again.")]
        [Min(0f)] public float DoorSeconds;

        [Tooltip("How far (m) south of a fixture's art its solid footprint stop is drawn: how far below his drawn " +
                 "feet the player's solid foot circle sits (0.7). He is sorted by his drawn feet, so with the stop " +
                 "dropped by this much his feet meet the piece's edge instead of walking into it and vanishing " +
                 "behind it. Less than his foot drop and he can stand inside the art.")]
        [Min(0f)] public float FixtureStopDropMetres;

        /// <summary>The shipped starting numbers (guesses for the owner to tune by feel).</summary>
        public static WashVisualsSettings Default => new WashVisualsSettings
        {
            Enabled = true,
            FillSeconds = 1.6f,
            DrainSeconds = 1.2f,
            FrothFramesPerSecond = 4f,
            RunFramesPerSecond = 10f,
            FlushSeconds = 0.9f,
            FrothWithoutSoap = false,
            ClimbSeconds = 0.55f,
            ClimbArcMetres = 0.22f,
            EntryStandOffMetres = 0.35f,
            FallbackBakedSeatMetres = 0.40f,
            BubbleCount = 6,
            BubbleRiseMetresPerSecond = 0.35f,
            BubbleLifetimeSeconds = 1.4f,
            BubbleSpreadMetres = 0.22f,
            BubbleOffsetMetres = new Vector2(0f, 0.75f),
            BubbleSizeMetres = 3f / 32f,
            BubbleColor = new Color(0.93f, 0.97f, 1f, 0.9f),
            BubbleOrdersAboveFigure = 2,
            DoorSeconds = 0.36f,
            FixtureStopDropMetres = 0.7f,
        };

        /// <summary>The live numbers: the config's, or <see cref="Default"/> with no config wired.</summary>
        public static WashVisualsSettings Current
            => GameServices.Config != null ? GameServices.Config.WashVisuals : Default;

        /// <summary>Whether a wash shows suds: always when <see cref="FrothWithoutSoap"/>, else only when the
        /// wash is soaped.</summary>
        public static bool Froths(bool soaped, in WashVisualsSettings s) => s.Enabled && (soaped || s.FrothWithoutSoap);
    }
}
