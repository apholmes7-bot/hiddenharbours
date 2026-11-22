using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>An oar blade met the water</b>: published by whoever draws a rowed hull's oars (the dory's oar
    /// splash today) on the frame a ROWING blade goes in (<see cref="OarStrokePart.Catch"/>) or comes out
    /// (<see cref="OarStrokePart.Release"/>), and heard by whoever voices it (the audio director's oar
    /// strokes). A Core seam so that the sound of a stroke keys off the same frame as the splash and the
    /// drawn blade, without Audio reaching into the Boats module (rule 4).
    ///
    /// <para>One message per oar: a straight pull with both oars sends two catches a stroke, a frame or
    /// two apart; a listener that wants one sound a stroke merges them by <see cref="HullId"/>. Nothing
    /// is sent for an oar that is not being rowed (trailing, shipped, stowed under power, nobody at the
    /// helm, aground). Presentation only: nothing in the simulation reads it (rule 5).</para>
    /// </summary>
    public readonly struct OarBladeWater
    {
        /// <summary><see cref="OarStrokePart.Catch"/> (the blade went in) or
        /// <see cref="OarStrokePart.Release"/> (it came out).</summary>
        public readonly OarStrokePart Moment;

        /// <summary>Where the blade met the water, world metres.</summary>
        public readonly Vector2 Position;

        /// <summary>How hard the stroke was, 0..1 (the splash's own strength: the pull and the boat's speed).</summary>
        public readonly float Strength01;

        /// <summary>Which hull's oar this is, for this session only (never saved, never compared across
        /// runs): the same number for both oars of one boat.</summary>
        public readonly int HullId;

        /// <summary>0 = the port oar, 1 = the starboard oar.</summary>
        public readonly int Oar;

        /// <summary>How many of this <see cref="Moment"/> this oar has made since it was wired, this one
        /// included (1 for the first). Counts from the oar's own stroke, so it is the same on every run of
        /// the same rowing.</summary>
        public readonly int StrokeIndex;

        public OarBladeWater(OarStrokePart moment, Vector2 position, float strength01, int hullId, int oar,
                             int strokeIndex)
        {
            Moment = moment;
            Position = position;
            Strength01 = Mathf.Clamp01(strength01);
            HullId = hullId;
            Oar = oar;
            StrokeIndex = strokeIndex;
        }

        /// <summary>True for a blade going in.</summary>
        public bool IsCatch => Moment == OarStrokePart.Catch;
    }
}
