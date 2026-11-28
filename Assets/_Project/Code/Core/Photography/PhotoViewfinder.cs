using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Raised when a picture is KEPT in a player's album (never for a picture of nothing, nor when the
    /// album is full). A one-way Core handoff: the camera (Player) publishes it, and whoever cares (the
    /// islanders' photo requests in World) reads the album through <see cref="PhotoAlbumRows"/> without
    /// naming the camera (rule 4).
    /// </summary>
    public readonly struct PhotoTaken
    {
        /// <summary>Whose album the picture went into (<see cref="SavePlayerKeys"/>).</summary>
        public readonly string PlayerKey;

        /// <summary>The picture's number in that album.</summary>
        public readonly int Number;

        public PhotoTaken(string playerKey, int number)
        {
            PlayerKey = playerKey;
            Number = number;
        }
    }

    /// <summary>
    /// <b>What the viewfinder shows right now</b>: whether the camera is up, the patch of world it takes in
    /// and the line about the last picture. The camera (Player) writes it every frame it is up; the
    /// viewfinder (UI) draws it. Presentation only: nothing here is saved and nothing in the simulation
    /// reads it.
    /// FLAG lead-architect: new Core contract (the viewfinder's state, the camera-to-UI seam).
    /// </summary>
    public static class PhotoViewfinder
    {
        /// <summary>True while the camera is up.</summary>
        public static bool IsUp { get; private set; }

        /// <summary>The patch of world the picture takes in (world metres). Meaningful while up.</summary>
        public static Rect Frame { get; private set; }

        /// <summary>The hint under the frame (which keys take and lower).</summary>
        public static string Hint { get; private set; } = "";

        /// <summary>The line about the last picture; empty when there is none to show.</summary>
        public static string Line { get; private set; } = "";

        /// <summary>Unscaled time the line stops showing.</summary>
        public static float LineUntil { get; private set; }

        /// <summary>Unscaled time the shutter's flash ends.</summary>
        public static float FlashUntil { get; private set; }

        /// <summary>Counts every press of the shutter, so a reader can tell a new shot from the last.</summary>
        public static int Shots { get; private set; }

        /// <summary>The camera is up over <paramref name="frame"/>.</summary>
        public static void Show(Rect frame, string hint)
        {
            IsUp = true;
            Frame = frame;
            Hint = hint ?? "";
        }

        /// <summary>The camera is down. The last line keeps its time, so a picture's word can outlive the
        /// frame by a moment if the reader wants.</summary>
        public static void Hide() => IsUp = false;

        /// <summary>The shutter went: flash until <paramref name="flashUntil"/>, show <paramref name="line"/>
        /// until <paramref name="lineUntil"/> (unscaled seconds).</summary>
        public static void Shot(string line, float flashUntil, float lineUntil)
        {
            Shots++;
            Line = line ?? "";
            FlashUntil = flashUntil;
            LineUntil = lineUntil;
        }

        /// <summary>A line with no shutter (the camera would not come up, say) until
        /// <paramref name="lineUntil"/>: no flash, and <see cref="Shots"/> does not move.</summary>
        public static void Say(string line, float lineUntil)
        {
            Line = line ?? "";
            LineUntil = lineUntil;
        }

        /// <summary>Forget everything (the play-session reset, and tests).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            IsUp = false;
            Frame = default;
            Hint = "";
            Line = "";
            LineUntil = 0f;
            FlashUntil = 0f;
            Shots = 0;
        }
    }
}
