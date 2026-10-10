using System;
using UnityEngine;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// A test-only reader. It runs after every drawer's LateUpdate: the wave motion (−120), the hull driver
    /// (−110), the interior (−100), the default band (the arrival, the facet hull) and the figure presenter
    /// (100), the latest. So what <see cref="Tick"/> reads is the frame as it will be drawn, not the frame
    /// before; a coroutine resumed by <c>yield return null</c> would read between Update and LateUpdate.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class LateFrameProbe : MonoBehaviour
    {
        /// <summary>Called once per frame, last. Null stops the reading.</summary>
        public Action Tick;

        private void LateUpdate() => Tick?.Invoke();
    }
}
