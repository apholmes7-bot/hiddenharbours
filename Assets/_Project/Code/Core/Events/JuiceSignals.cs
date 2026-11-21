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
        /// <summary>The dig press: the clam is resolved and the dig performance starts (the body clip and
        /// the notebook's weight key off this). Strength = kg. The shovel is still in the air here; the
        /// dirt and the sound wait for <see cref="DigImpact"/>.</summary>
        DigStrike = 2,
        /// <summary>The cast line touches down. Strength = 0.</summary>
        CastEntry = 3,
        /// <summary>The blade enters the ground: the <see cref="DigStrike"/> of the same dig, re-published
        /// after the dig timing's <c>ImpactDelaySeconds</c> by the player's impact relay, and dropped if the
        /// dig is interrupted first. The sand chunks and the dig sound key off this. Strength = kg.</summary>
        DigImpact = 4,
        /// <summary>The lift and toss: the dirt leaves the shovel's blade, published by the player's impact
        /// relay <c>GameConfig.Juice.DigTossDelaySeconds</c> after <see cref="DigImpact"/> and dropped like it
        /// when the dig is interrupted. When the blade could be read (<see cref="JuiceMomentCue.FromBlade"/>)
        /// the point is the ground under the blade tip, <see cref="JuiceMomentCue.Lift"/> the tip's height
        /// above it in the picture and <see cref="JuiceMomentCue.Dir"/> the way he throws, across the ground.
        /// Silent (the dig sound is the impact's). Strength = kg.</summary>
        DigToss = 5,
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
        /// <summary>True when the point was read off the held tool's drawn blade (the dig's impact and toss),
        /// so <see cref="Lift"/> and <see cref="Dir"/> mean something; false for every other cue.</summary>
        public readonly bool FromBlade;
        /// <summary>The blade tip's height above <see cref="At"/> in the picture (world units), when
        /// <see cref="FromBlade"/>.</summary>
        public readonly float Lift;
        /// <summary>The throw across the ground (unit length, ground metres; zero = no direction), when
        /// <see cref="FromBlade"/>.</summary>
        public readonly float DirX;
        public readonly float DirY;

        public JuiceMomentCue(JuiceMoment kind, float x, float y, float strength)
        {
            Kind = kind;
            X = x;
            Y = y;
            Strength = strength;
            FromBlade = false;
            Lift = 0f;
            DirX = 0f;
            DirY = 0f;
        }

        public JuiceMomentCue(JuiceMoment kind, Vector2 at, float strength)
            : this(kind, at.x, at.y, strength) { }

        /// <summary>A cue read off the held tool's blade: <paramref name="ground"/> under the tip,
        /// <paramref name="lift"/> above it, thrown along <paramref name="dir"/> (normalised here).</summary>
        public JuiceMomentCue(JuiceMoment kind, Vector2 ground, float strength, float lift, Vector2 dir)
        {
            Kind = kind;
            X = ground.x;
            Y = ground.y;
            Strength = strength;
            FromBlade = true;
            Lift = Mathf.Max(0f, lift);
            Vector2 d = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.zero;
            DirX = d.x;
            DirY = d.y;
        }

        public Vector2 At => new Vector2(X, Y);
        public Vector2 Dir => new Vector2(DirX, DirY);
    }
}
