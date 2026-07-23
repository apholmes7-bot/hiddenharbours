using UnityEngine;

namespace HiddenHarbours.SpikeDeckCharacterMesh
{
    /// <summary>
    /// ⚠️ SPIKE (deck-character-mesh, draft ADR 0024). The pure math the deck-mesh rig reuses —
    /// EditMode-testable, engine-light, deterministic (CLAUDE.md rule 5: pure functions of their
    /// inputs, no hidden state).
    /// </summary>
    public static class DeckCharacterSpikeMath
    {
        /// <summary>
        /// Which frame of an N-frame cycle a clock lands on. Pure: the same clock always answers
        /// the same frame (no Time.* read here — the caller owns the clock).
        /// </summary>
        public static int PoseFrame(double clockSeconds, int frameCount, float framesPerSecond)
        {
            if (frameCount <= 1 || framesPerSecond <= 0f || !double.IsFinite(clockSeconds))
                return 0;
            double f = clockSeconds * framesPerSecond;
            int frame = (int)(f % frameCount);
            return frame < 0 ? frame + frameCount : frame;
        }

        /// <summary>
        /// Re-express the DECK's tilt in the CHARACTER's own frame.
        ///
        /// <para>The hull states its rock as roll (about ITS fore-aft axis) + pitch (about ITS
        /// beam). The character rig applies opts.roll/opts.pitch about the CHARACTER's own
        /// fore-aft/beam axes (<c>camBasis</c> — "the hull recipe", per the rig's DECK ROCK note).
        /// When the character faces somewhere other than the bow, the two frames differ by the
        /// local heading, so the tilt VECTOR (pitch about x, roll about y — small-angle rotation
        /// vectors add and rotate like vectors) is rotated into the character's frame:</para>
        ///
        /// <code>
        ///   pitchC =  pitchHull·cos δ + rollHull·sin δ
        ///   rollC  = −pitchHull·sin δ + rollHull·cos δ
        /// </code>
        ///
        /// <para>δ = the character's deck-local heading in DEGREES (0 = facing the bow). At δ = 0
        /// this is the identity — the rig's own documented "feed a hull rig's rock(i) straight in"
        /// contract; at δ = ±90° roll and pitch trade places; the tilt magnitude is preserved at
        /// every δ. Small-angle: exact composition would need a shared 3D rotation, which the rig's
        /// own roll-then-pitch channel cannot express anyway — at the fleet's ≤3° rock amplitudes
        /// the error is second-order (&lt;0.01°).</para>
        /// </summary>
        public static void DeckTiltToCharacter(float hullRollDegrees, float hullPitchDegrees,
                                               float localHeadingDegrees,
                                               out float rollDegrees, out float pitchDegrees)
        {
            float d = localHeadingDegrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(d), s = Mathf.Sin(d);
            pitchDegrees = hullPitchDegrees * c + hullRollDegrees * s;
            rollDegrees = -hullPitchDegrees * s + hullRollDegrees * c;
        }

        /// <summary>
        /// Split a rig-camera anchor projection (screen METRES from the hull pivot, +x right,
        /// +y up — <c>MountedRockPoseMath.Project</c>'s result frame) into what the facet renderer
        /// wants: a world-x offset for the character ROOT, and the screen-y lift expressed as HEAVE
        /// PIXELS.
        ///
        /// <para><b>Why y goes into heave and not into the root's position:</b> the renderer
        /// calibrates its iso z off its OWN root y while the displaced sea is live
        /// (<c>IsoFacetHullRenderer.ApplyPose</c> → <c>DisplacedWaterMath.HullDepthBias</c>).
        /// A character root lifted by deck height would sit ~deckHeight·cos(elev) FARTHER in the
        /// shared z-buffer than its own hull and lose the per-pixel z-test to the deck it stands
        /// on. Keeping the root at the HULL's y (the shared ground anchor of the water convention)
        /// and lifting the picture through the heave channel is also rig-honest: heave IS the
        /// rig's screen-y lift, subtracted after projection.</para>
        /// </summary>
        public static void SplitAnchor(Vector2 projectedMetres, int pxPerMetre,
                                       out float rootOffsetX, out float heavePixels)
        {
            rootOffsetX = projectedMetres.x;
            heavePixels = projectedMetres.y * pxPerMetre;
        }
    }
}
