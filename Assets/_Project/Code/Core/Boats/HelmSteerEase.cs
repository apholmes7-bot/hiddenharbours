namespace HiddenHarbours.Core
{
    /// <summary>
    /// The key layer's STEER EASE (ADR 0025 S4.5, the owner's "the steering wheel needs to follow the
    /// turning from the arrow keys — gradual and smooth"). A keyboard can only say −1 / 0 / +1, so the
    /// commanded steer used to jump the full lock in one frame and the dash wheel — which MIRRORS
    /// <see cref="IHelmControl.Steer"/> — teleported with it. This walks the COMMAND toward the key's
    /// target at a fixed rate instead.
    ///
    /// <para><b>Why the command and not the wheel graphic.</b> Easing only the picture would make the
    /// wheel show less lock than the rudder actually has — a lying instrument, and the one thing an
    /// instrument may never be. Easing the command keeps the existing mirror exact: the wheel is
    /// gradual because the RUDDER is gradual, which is also the progressive key-steer feel the ask is
    /// really about.</para>
    ///
    /// <para><b>It holds no state.</b> The caller passes the live steer in and puts the result back
    /// through <c>BoatController.SetControl</c>, so there is never a second copy of steer to drift
    /// from the one owner (the same read-back discipline the notched throttle uses). Taking the
    /// channel over from a held wheel session therefore starts from the wheel's own angle and cannot
    /// snap: <paramref name="current"/> IS that angle.</para>
    ///
    /// <para><b>It settles EXACTLY.</b> Once the remaining distance is inside one step the result is
    /// the target itself, not an asymptote — an exponential approach would leave the wheel's
    /// half-degree repaint key flickering forever (rule 7). Pure + static, injected dt, so the whole
    /// curve is EditMode-testable without a frame loop (PlayMode frame count is not time).</para>
    /// </summary>
    public static class HelmSteerEase
    {
        /// <summary>
        /// Walk <paramref name="current"/> toward <paramref name="target"/> over
        /// <paramref name="dt"/> seconds at a rate of one full lock per
        /// <paramref name="secondsToFullLock"/>.
        ///
        /// <para>The rate is expressed in steer units per second, so CENTRE→FULL LOCK takes
        /// <paramref name="secondsToFullLock"/> and a full reversal (lock to opposite lock) takes
        /// twice that — a wheel does not spin faster because you asked for more of it. A reversal
        /// therefore passes through centre smoothly rather than snapping across it.</para>
        ///
        /// <para><paramref name="secondsToFullLock"/> ≤ 0 means NO ease (the pre-S4.5 instant step),
        /// so the owner can dial the feature off from <c>GameConfig</c> without a code path of its
        /// own. A non-positive <paramref name="dt"/> makes no progress and returns
        /// <paramref name="current"/> unchanged — a paused frame must not teleport the helm.</para>
        /// </summary>
        public static float Step(float current, float target, float dt, float secondsToFullLock)
        {
            if (!(secondsToFullLock > 0f)) return target;   // ease disabled → the old instant step
            if (!(dt > 0f)) return current;                 // paused / first frame → hold

            float maxStep = dt / secondsToFullLock;
            float delta = target - current;
            if (delta <= maxStep && delta >= -maxStep) return target;   // settle EXACTLY, no epsilon
            return current + (delta > 0f ? maxStep : -maxStep);
        }
    }
}
