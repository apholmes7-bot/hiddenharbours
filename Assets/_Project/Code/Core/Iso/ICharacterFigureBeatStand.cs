namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Work with a place in time</b> (ADR 0053 §11): a beat of a one-shot sequence, such as a villager's
    /// cast, strike, reel and landing. A <see cref="ICharacterFigureWorkStand"/> that can also say where in
    /// the beat the person is. The figure presenter asks it before the plain work and after a route-driven
    /// clip (priority: driven clip, then beat, then work), applies the work gates (a well-formed meshed
    /// clip; for a tool, the hand-tool switch and a prop the figure can hold on the right hand's tool
    /// socket), and takes the frame through the one frame rule, <see cref="CharacterSkinPose.FrameFor"/>.
    /// No stand computes a frame.
    ///
    /// <para><b>A function of the clock</b> (rule 5): the answer is read off a pure timeline every frame,
    /// never latched and never saved.</para>
    ///
    /// FLAG lead-architect: new Core contract (the villager beat seam).
    /// </summary>
    public interface ICharacterFigureBeatStand : ICharacterFigureWorkStand
    {
        /// <summary>
        /// True while the person is in a beat this frame. <paramref name="stateKey"/> is the clip's state key
        /// in the skin, as data. <paramref name="beatStartSeconds"/> is when the beat began, on the figure's
        /// own clock (<see cref="IGameClock.FlowSeconds"/>): a one-shot clip plays from it and holds its last
        /// frame; a looping clip ignores it and runs from absolute time, phased by the figure's key.
        /// <paramref name="scrub01"/> is NaN to play at the clip's own rate, or 0..1 to scrub the clip by
        /// progress (the wind-back, as the player's charge scrubs it). <paramref name="heldProp"/> is the
        /// tool, or null for bare hands.
        /// </summary>
        bool TryGetFigureBeat(out string stateKey, out double beatStartSeconds, out float scrub01,
                              out HeldPropMeshDef heldProp);
    }
}
