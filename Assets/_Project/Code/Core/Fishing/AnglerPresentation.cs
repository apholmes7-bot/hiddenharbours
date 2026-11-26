using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A villager angler, read by pull</b> (ADR 0053 §11). An NPC never publishes
    /// <see cref="FishingStateChanged"/>: that event is the one player's (HUD, audio, animator, presenter),
    /// and a villager's bite must not ring the player's bell. Instead his stand answers the same Core
    /// snapshot, <see cref="FishingState"/>, each frame, so the shared line, splash, surfaced fish and fish in
    /// hand speak the vocabulary they already speak. A function of the clock (rule 5), never saved.
    ///
    /// FLAG lead-architect: new Core contract (the angler feed).
    /// </summary>
    public interface IAnglerStand
    {
        /// <summary>
        /// True while he is fishing this frame. <paramref name="state"/> is his fishing state;
        /// <paramref name="secondsInPhase"/> the seconds since its phase began, on the figure's clock
        /// (<see cref="IGameClock.FlowSeconds"/>). False when he is not fishing (resting, talking, away).
        /// </summary>
        bool TryGetAnglerState(out FishingState state, out float secondsInPhase);

        /// <summary>How his rod fight is drawn: authored data (his activity Def), read every frame so a value
        /// tuned live is drawn on the next frame.</summary>
        AnglerLook Look { get; }
    }

    /// <summary>
    /// <b>The fish coming to his bait</b>, read by pull beside <see cref="IAnglerStand"/>: while he waits, the
    /// slot's own catch (species, weight) and the seconds before she bites. False when no bite is coming in
    /// this wait. A function of the clock, never saved; a stand that does not implement it shows no approach.
    /// </summary>
    public interface IAnglerApproach
    {
        bool TryGetApproach(out string fishId, out float weightKg, out float secondsToBite);
    }

    /// <summary>
    /// <b>A swimming fish's cell</b>: the art the schools in the water are drawn with, for a drawer outside
    /// the module that owns that table. <paramref name="headingRadians"/> is a compass heading (0 = North,
    /// clockwise); <paramref name="seconds"/> picks the swim frame. False when the species has no swim art
    /// (a shellfish, an unmapped id): draw nothing then.
    ///
    /// FLAG lead-architect: new Core contract (the swim-art seam).
    /// </summary>
    public interface ISwimFishArt
    {
        bool TrySwimCell(string speciesId, float headingRadians, double seconds, out Sprite cell);
    }

    /// <summary>The locator for <see cref="ISwimFishArt"/>, the <see cref="AnglerPresentation"/> pattern:
    /// stateless art wiring, self-registered by its owner at runtime load. Null = no swim art.</summary>
    public static class SwimFishArt
    {
        public static ISwimFishArt Service { get; set; }
    }

    /// <summary>
    /// <b>How a villager's rod fight is drawn</b>: every number and sprite the shared pieces take that the
    /// player's presenter keeps in its own inspector. Plain serializable data with no defaults (rule 6): a
    /// World activity Def authors it. The fish's rung ladder is not here: it is the shared fish art table's
    /// (<c>FishArtLibrary.Shared</c>), the same table the player falls back to.
    /// </summary>
    [Serializable]
    public struct AnglerLook
    {
        /// <summary>The line's look, as the player's line is drawn.</summary>
        public RodLineLook Line;
        /// <summary>The line's width (m) and its sample count, taken when his line is first built.</summary>
        public float LineWidthM;
        public int LineSegments;

        /// <summary>The surfaced fish's flipbook step (s per frame), the speed she reads as darting above
        /// (m/s), and how much of each frame's speed her drawn speed takes (0..1, the player's 0.35).</summary>
        public float FishSecondsPerFrame;
        public float FishDartSpeedMps;
        public float FishSpeedSmoothing01;
        /// <summary>Seconds between a hooked jumper's tries when her species states none.</summary>
        public float FallbackJumpPeriodSeconds;
        /// <summary>The sorting order the fish on the line is drawn at (the water's elements).</summary>
        public int WaterElementOrder;

        /// <summary>The splash flipbook thrown at touchdown, at her break, at a jump's re-entry and at the
        /// landing, and its rate.</summary>
        public Sprite[] SplashFrames;
        public float SplashFps;

        /// <summary>
        /// The fish swimming in to his bait (the same swim art the schools are drawn with): she shows this many
        /// seconds before the bite (0 = no approach drawn), starting this far from the bobber and off the line
        /// by this angle (degrees, either side), easing in on a lazy swing of this width (m). She holds at the
        /// bobber through the bite and is drawn under the water at this sorting order and opacity, through the
        /// deep fight as well, until she breaks the surface.
        /// </summary>
        public float ApproachSeconds;
        public float ApproachFadeSeconds;
        public float ApproachFromM;
        public float ApproachOffLineDegrees;
        public float ApproachSwingM;
        public int ApproachSortingOrder;
        public float ApproachAlpha01;
    }

    /// <summary>A villager angler's drawing, attached to his host. Hidden, never destroyed, between
    /// sessions (rule 7).</summary>
    public interface IAnglerFigure
    {
        /// <summary>True on a frame his line is drawn.</summary>
        bool DrawsLine { get; }
    }

    /// <summary>
    /// <b>Puts a villager angler's rod fight on a host</b>: implemented in Player (the composition layer,
    /// which shares the pieces with the player's own presenter), called by World with nothing but Core types
    /// in hand. Attach lazily, the first frame a routine block names an angling activity, so a villager who
    /// never fishes gets no presenter, no line and no update.
    ///
    /// FLAG lead-architect: new Core contract (the angler presentation service).
    /// </summary>
    public interface IAnglerPresentationService
    {
        /// <summary>
        /// Attach (or return the one already attached) to <paramref name="host"/>, the GameObject carrying
        /// the villager's figure presenter, drawn every frame from <paramref name="stand"/>. Null for a
        /// missing host or stand.
        /// </summary>
        IAnglerFigure Attach(GameObject host, IAnglerStand stand);
    }

    /// <summary>
    /// The locator for <see cref="IAnglerPresentationService"/>. Deliberately NOT a <c>GameServices</c>
    /// member, for the reason <see cref="CharacterFigurePresentation"/> gives: <c>GameServices.Reset()</c>
    /// clears game-state services, and this is stateless presentation wiring. Player self-registers at
    /// runtime load; tests register explicitly. Consumers null-check: a null service means no angler drawing.
    /// </summary>
    public static class AnglerPresentation
    {
        public static IAnglerPresentationService Service { get; set; }
    }
}
