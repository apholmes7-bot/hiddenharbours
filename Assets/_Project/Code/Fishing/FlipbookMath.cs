namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// Pure frame-mapping math for the trap arc's sprite flipbooks (the deck animals' crawl loop, the
    /// haul-break splash burst) — engine-free and EditMode-pinned, the <c>PlayerHaulAnimMath</c> /
    /// <c>TrapHaulMath</c> discipline. The CADENCE (fps) and the FRAMES are data on the Defs
    /// (<see cref="SpeciesDeckRule.CrawlFps"/>, <see cref="TrapDef.SplashBurstFps"/> — rules 2+6); this
    /// only turns (elapsed seconds, fps, frame count) into a frame index, so a re-tuned fps or a
    /// re-authored sheet never touches code.
    ///
    /// <para><b>Presentation only (rule 5).</b> These indices drive SpriteRenderers, never sim or saved
    /// state — callers feed them real (render) time, which is fine exactly because nothing here writes
    /// back.</para>
    /// </summary>
    public static class FlipbookMath
    {
        /// <summary>
        /// The frame a LOOPING flipbook shows after <paramref name="elapsedSeconds"/> at
        /// <paramref name="fps"/>: wraps forever over <paramref name="frameCount"/> frames. Degenerate
        /// inputs are safe: no frames → -1 (nothing to show); fps ≤ 0 or elapsed ≤ 0 → frame 0 (frozen).
        /// </summary>
        public static int LoopFrame(double elapsedSeconds, float fps, int frameCount)
        {
            if (frameCount <= 0) return -1;
            if (fps <= 0f || elapsedSeconds <= 0.0) return 0;
            long step = (long)(elapsedSeconds * fps);
            return (int)(step % frameCount);
        }

        /// <summary>
        /// The frame a ONE-SHOT flipbook shows after <paramref name="elapsedSeconds"/> at
        /// <paramref name="fps"/>, or -1 once the burst has finished (all frames played). Degenerate
        /// inputs are safe: no frames → -1; fps ≤ 0 → -1 (a one-shot that can't advance never shows);
        /// elapsed ≤ 0 → frame 0.
        /// </summary>
        public static int OneShotFrame(double elapsedSeconds, float fps, int frameCount)
        {
            if (frameCount <= 0 || fps <= 0f) return -1;
            if (elapsedSeconds <= 0.0) return 0;
            long step = (long)(elapsedSeconds * fps);
            return step < frameCount ? (int)step : -1;
        }

        /// <summary>Seconds a one-shot of <paramref name="frameCount"/> frames at <paramref name="fps"/>
        /// takes to play out (0 when it can't play). The pool uses this to retire entries.</summary>
        public static float OneShotSeconds(float fps, int frameCount)
            => frameCount > 0 && fps > 0f ? frameCount / fps : 0f;
    }
}
