using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The four moments the juice charter names (owner ruling 2026-09-09, §4): the frame a fish
    /// leaves the water, the sale, the shovel's strike, the line's entry. Each is ONE published
    /// <see cref="JuiceMomentCue"/> — the audio slot in <c>AUDIO-MANIFEST.md</c> keys off the same
    /// event the picture does, so when the owner's files land they play with no code change.
    /// </summary>
    public enum JuiceMoment
    {
        /// <summary>The fish leaves the water (the fight drawer's last frame). Strength = kg.</summary>
        Landing = 0,
        /// <summary>The catch is sold (<c>CatchSold</c> carries the coins; this is the beat). Strength = coins.</summary>
        Sale = 1,
        /// <summary>The shovel bites the flat and a clam comes up. Strength = kg.</summary>
        DigStrike = 2,
        /// <summary>The cast line touches down. Strength = 0.</summary>
        CastEntry = 3,
    }

    /// <summary>
    /// One moment, published by the system that owns it (the fishing controller, the dig, the sell
    /// service's listener) and heard by every presenter that dresses it — the pooled burst emitter,
    /// the hit-stop, the audio director, the notebook. World position + one strength number; the
    /// presenters scale from the strength (kg, coins), never from a concrete class (rule 4).
    /// </summary>
    public readonly struct JuiceMomentCue
    {
        public readonly JuiceMoment Kind;
        public readonly float X;
        public readonly float Y;
        /// <summary>The moment's size in its own unit: kg for a landing or a dig, coins for a sale, 0 for a cast entry.</summary>
        public readonly float Strength;

        public JuiceMomentCue(JuiceMoment kind, float x, float y, float strength)
        {
            Kind = kind;
            X = x;
            Y = y;
            Strength = strength;
        }

        public JuiceMomentCue(JuiceMoment kind, Vector2 at, float strength)
            : this(kind, at.x, at.y, strength) { }

        public Vector2 At => new Vector2(X, Y);
    }
}
