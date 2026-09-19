using UnityEngine;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// A test-only body-sort writer. It runs at execution order 50: after the figure renderer's own
    /// LateUpdate (0) and before the rider's (100). It moves the body's sorting order every frame and
    /// records two numbers: what the overlay held before the write (the control) and what it wrote
    /// (the bar). An overlay that matches <see cref="Written"/> on the same frame copied this frame's
    /// order, not the order from the frame before.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class LateSortWriter : MonoBehaviour
    {
        /// <summary>The first order written; far from the rig's own orders, so the first write moves it.</summary>
        private const int FirstOrder = 10;
        /// <summary>How far each frame's write moves the order; never zero, so no two frames match.</summary>
        private const int Step = 7;

        public SpriteRenderer Target;
        public Renderer Overlay;
        public int NextOrder = FirstOrder;

        /// <summary>The order this frame's write gave the body.</summary>
        public int Written { get; private set; } = int.MinValue;

        /// <summary>The overlay's order just before this frame's write.</summary>
        public int OverlayBeforeWrite { get; private set; } = int.MinValue;

        /// <summary>How many frames the writer has run, so a test can see that it ran this frame.</summary>
        public int Frames { get; private set; }

        private void LateUpdate()
        {
            if (Target == null) return;
            OverlayBeforeWrite = Overlay != null ? Overlay.sortingOrder : int.MinValue;
            Written = NextOrder;
            Target.sortingOrder = NextOrder;
            NextOrder += Step;
            Frames++;
        }
    }
}
