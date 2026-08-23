using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE CLIMB</b> — the short walk from the foot of a stairwell to the head of it, as a plain
    /// object with a playhead. One storey's worth of travel, played out over a duration, so going
    /// upstairs is something the player watches happen to them rather than a cut.
    ///
    /// <para><b>Why it is a path and not a teleport.</b> ADR 0036 put both halves of the stairwell on one
    /// footprint coordinate and moved nobody: the storeys were drawn at the same place, so the foot of
    /// the stairs and the head of them WERE the same point. Once the upper level draws at its true height
    /// (<see cref="InteriorLevelLayout.UpperLevelY"/>) they are a real distance apart, and a fisher who
    /// arrives up there by snapping is a fisher the camera jumps with. The path is the whole difference
    /// between a storey you climb and a storey you blink to.</para>
    ///
    /// <para><b>⚠️ THE GAIT HOLD IS PART OF THIS CONTRACT, NOT AN EXTRA.</b> Whoever plays a traversal
    /// writes the walker's position every frame, and <c>IsoCharacterSprite</c> picks its gait by
    /// MEASURING position deltas in <c>LateUpdate</c>. A storey's rise is ~2.4 world units in about half
    /// a second, so a measured walker reads ~5 m/s and draws a dead sprint — on the spot, because the
    /// rise is height and not ground covered. The driver must therefore STATE the speed with
    /// <c>IsoCharacterSprite.HoldSpeed(<see cref="GaitSpeedMetresPerSecond"/>)</c> every frame the climb
    /// runs, and it must do so in <c>Update</c>: the hold has to be in force BEFORE the measurement in
    /// <c>LateUpdate</c>, and a hold applied in <c>LateUpdate</c> is a frame late for ever.
    /// <c>ReleaseSpeed</c> on the way out, exactly as the deck rider does when the player steps ashore.</para>
    ///
    /// <para>Pure C#: no scene, no <c>Time</c>, no engine state, no allocation per frame. It is handed a
    /// delta and told to advance, so the whole of it is unit-tested headless at any timescale — including
    /// the two properties that matter, that it ENDS exactly one storey up and that it never goes
    /// backwards on the way.</para>
    /// </summary>
    public sealed class StairTraversal
    {
        /// <summary>
        /// <b>The speed a climbing walker's gait must be held at</b> — see the ⚠️ note in the class
        /// remarks for why any hold at all, and why it is stated in <c>Update</c>.
        ///
        /// <para><b>Zero, and zero is the honest number.</b> The rise up a stairwell is HEIGHT: the
        /// fisher covers no ground, so there is no stride to draw and stating their screen travel would
        /// draw a run they are not taking. Nothing in the character kit is baked climbing, so a held 0
        /// puts the standing cell on the stairs for half a second, which is the truthful greybox until
        /// there is climb art to play instead. The day there is, this becomes that clip's speed and every
        /// caller follows it — which is the reason it is a named term here rather than a 0 typed into the
        /// one place that happens to hold the gait today.</para>
        /// </summary>
        public const float GaitSpeedMetresPerSecond = 0f;

        /// <summary>Where the climb starts — the foot of the stairs, on the storey being left.</summary>
        public Vector2 Foot { get; }

        /// <summary>Where it ends — the head of the stairs: the SAME footprint coordinate, one storey's
        /// projected height away. That the two differ only in Y is the layer model holding: you arrive
        /// standing on the other half of the stairwell, which is what makes the way back a press and not
        /// a search.</summary>
        public Vector2 Head { get; }

        /// <summary>How long the whole climb takes, in seconds. Never negative. Zero is legal and means
        /// an instant arrival — the behaviour the storey swap had before the climb existed, and what the
        /// owner gets by setting <c>GameConfig.StairClimbSeconds</c> to 0.</summary>
        public float Seconds { get; }

        /// <summary>How far into the climb the playhead is, in seconds. Never past
        /// <see cref="Seconds"/>.</summary>
        public float Elapsed { get; private set; }

        /// <summary>The climb's rise in world units — positive going up, negative coming down.</summary>
        public float RiseWorldUnits => Head.y - Foot.y;

        /// <summary>Where the walker is right now.</summary>
        public Vector2 Position => PositionAt(Progress01);

        /// <summary>The playhead as a 0…1 fraction. A zero-length climb reads 1 from the outset — it is
        /// over before it began, not divided by zero.</summary>
        public float Progress01 => Seconds <= 0f ? 1f : Mathf.Clamp01(Elapsed / Seconds);

        /// <summary>True once the walker has arrived. The driver hands the body back at this point:
        /// release the gait, drop the move-axis claim, let the threshold test run again.</summary>
        public bool IsDone => Progress01 >= 1f;

        public StairTraversal(Vector2 foot, Vector2 head, float seconds)
        {
            Foot = foot;
            Head = head;
            Seconds = float.IsNaN(seconds) ? 0f : Mathf.Max(0f, seconds);
        }

        /// <summary>
        /// Move the playhead on by <paramref name="deltaSeconds"/> and answer where the walker now is.
        /// Ignores a negative or NaN delta rather than rewinding: a climb is not scrubbed, and a bad
        /// delta (a paused editor, a frame spike, a hand-driven test) must never put the fisher back
        /// downstairs.
        /// </summary>
        public Vector2 Advance(float deltaSeconds)
        {
            if (!float.IsNaN(deltaSeconds) && deltaSeconds > 0f)
                Elapsed = Mathf.Min(Seconds, Elapsed + deltaSeconds);
            return Position;
        }

        /// <summary>Where the walker is at a given fraction of THIS climb.</summary>
        public Vector2 PositionAt(float t01) => PositionAt(Foot, Head, t01);

        /// <summary>
        /// <b>The path itself</b>, as a pure function: the point <paramref name="t01"/> of the way from
        /// <paramref name="foot"/> to <paramref name="head"/>.
        ///
        /// <para>Eased with <see cref="Mathf.SmoothStep"/> — a step up starts and settles rather than
        /// beginning and stopping at full speed, and half a second of linear travel reads mechanical. Any
        /// ease chosen here must be MONOTONIC: an overshooting one (a back- or elastic-ease is the usual
        /// temptation) would take the fisher above the upper floor and drop them back onto it, and on a
        /// stairwell that is a body clipping through a bedroom ceiling. A test pins that.</para>
        ///
        /// <para>Clamped at both ends, so a caller that oversteps its own playhead lands ON the storey
        /// rather than past it.</para>
        /// </summary>
        public static Vector2 PositionAt(Vector2 foot, Vector2 head, float t01)
        {
            if (float.IsNaN(t01)) return foot;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t01));
            return new Vector2(Mathf.LerpUnclamped(foot.x, head.x, t),
                               Mathf.LerpUnclamped(foot.y, head.y, t));
        }
    }
}
