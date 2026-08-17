using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Where a speech bubble sits and where its tail points, for one frame. A value, so the whole
    /// screen-edge rule is a POCO test rather than a screenshot.
    /// </summary>
    public readonly struct DialogueBubblePlacement
    {
        /// <summary>Centre of the bubble body, in SCREEN PIXELS (origin bottom-left, Unity's convention).</summary>
        public readonly Vector2 BubbleCentre;

        /// <summary>Where the tail meets the bubble, in screen pixels — x follows the speaker even after
        /// the bubble has stopped following (that is the whole trick), y is the bubble edge the tail
        /// leaves from.</summary>
        public readonly Vector2 TailRoot;

        /// <summary>True in the normal case (bubble above the speaker, tail hanging down off its bottom
        /// edge); false when the speaker is close enough to the top of the screen that the bubble had to
        /// flip UNDER them and the tail points up.</summary>
        public readonly bool TailPointsDown;

        public DialogueBubblePlacement(Vector2 bubbleCentre, Vector2 tailRoot, bool tailPointsDown)
        {
            BubbleCentre = bubbleCentre;
            TailRoot = tailRoot;
            TailPointsDown = tailPointsDown;
        }
    }

    /// <summary>
    /// <b>The Eastward read: the bubble slides to stay on screen, the tail keeps pointing at whoever is
    /// talking.</b> Pure static maths — no camera, no canvas, no <c>Time</c> — so every edge case that
    /// matters (a speaker at the screen edge, a speaker taller than the gap above them, a bubble wider
    /// than the screen) is an EditMode assertion instead of something you have to walk to a corner to
    /// see.
    ///
    /// <para><b>Screen pixels in, screen pixels out.</b> The presenter converts the speaker's world
    /// position with the camera and converts the answer into canvas units on the way back; keeping this
    /// function in one honest unit is what lets it be tested with plain numbers. It also keeps the
    /// pixel-crispness discipline honest (camera-zoom ruling #327): the caller rounds ONE number — the
    /// anchor — and everything downstream is integer-shaped.</para>
    ///
    /// <para><b>Why the tail is a separate answer from the bubble.</b> If the tail were welded to the
    /// bubble's centre, a bubble that slid away from a screen edge would point at empty air and the
    /// speaker would appear to be somebody else. So the bubble is clamped and the tail is clamped
    /// SEPARATELY — to the bubble's own width, inset by <paramref name="tailInset"/> so it never falls
    /// off the rounded corner. Past that inset the tail stops tracking, which is the honest limit: the
    /// speaker is now further out than the bubble can lean.</para>
    /// </summary>
    public static class DialogueBubbleLayout
    {
        /// <summary>
        /// Place a bubble of <paramref name="bubbleSize"/> for a speaker whose head is at
        /// <paramref name="anchor"/> (screen pixels), inside a <paramref name="screenSize"/> screen.
        ///
        /// <list type="bullet">
        ///   <item>preferred: centred over the speaker, its bottom edge <paramref name="tailHeight"/>
        ///   above their head;</item>
        ///   <item>clamped horizontally and vertically to keep <paramref name="margin"/> of screen around
        ///   it — and centred instead if it is simply wider (or taller) than the room available, because
        ///   a clamp with no room is a jitter;</item>
        ///   <item>flipped BELOW the speaker when there is not enough headroom above but there is below —
        ///   which is what a conversation at the top of the screen needs, and the one case where the tail
        ///   points up.</item>
        /// </list>
        /// </summary>
        public static DialogueBubblePlacement Solve(Vector2 anchor, Vector2 bubbleSize, Vector2 screenSize,
                                                    float margin, float tailHeight, float tailInset)
        {
            float halfW = bubbleSize.x * 0.5f;
            float halfH = bubbleSize.y * 0.5f;

            // ---- vertical: above the head by default, under it when the head is near the top.
            float above = anchor.y + tailHeight + halfH;
            float below = anchor.y - tailHeight - halfH;

            bool fitsAbove = above + halfH <= screenSize.y - margin;
            bool fitsBelow = below - halfH >= margin;

            bool tailPointsDown = fitsAbove || !fitsBelow;   // prefer above; flip only when below is the
                                                             // only one that fits
            float centreY = tailPointsDown ? above : below;

            // Clamp within the margins — unless the bubble is taller than the room, in which case centre
            // it rather than letting two opposing clamps fight.
            float minY = margin + halfH, maxY = screenSize.y - margin - halfH;
            centreY = minY <= maxY ? Mathf.Clamp(centreY, minY, maxY) : screenSize.y * 0.5f;

            // ---- horizontal: centred on the speaker, then slid inside the margins.
            float minX = margin + halfW, maxX = screenSize.x - margin - halfW;
            float centreX = minX <= maxX ? Mathf.Clamp(anchor.x, minX, maxX) : screenSize.x * 0.5f;

            // ---- the tail keeps pointing at the speaker, within the bubble's own width.
            float inset = Mathf.Min(tailInset, halfW);
            float tailX = Mathf.Clamp(anchor.x, centreX - halfW + inset, centreX + halfW - inset);
            float tailY = tailPointsDown ? centreY - halfH : centreY + halfH;

            return new DialogueBubblePlacement(new Vector2(centreX, centreY), new Vector2(tailX, tailY),
                                               tailPointsDown);
        }

        /// <summary>
        /// Is the speaker's anchor actually on screen (within <paramref name="margin"/> of it)? The presenter
        /// hides the bubble when they are not — a tail pointing off the edge at somebody you cannot see
        /// is worse than no bubble, and it is the only case where the Eastward read has nothing to say.
        /// </summary>
        public static bool AnchorIsOnScreen(Vector2 anchor, Vector2 screenSize, float margin)
            => anchor.x >= -margin && anchor.x <= screenSize.x + margin
            && anchor.y >= -margin && anchor.y <= screenSize.y + margin;
    }
}
