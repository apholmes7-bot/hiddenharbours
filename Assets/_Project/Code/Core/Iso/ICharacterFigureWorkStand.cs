namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A villager at work on her own feet, with the tool in her hand</b> (ADR 0053 §10). An optional
    /// seam on an <see cref="ICharacterFigureAshoreStand"/>: a stand that is one of these can say that the
    /// person is, this frame, doing a piece of work the rig has a looping clip for (the clam dig), and
    /// which held prop the work is done with. The figure presenter plays that clip and draws the prop as
    /// its facet mesh on the figure's hand, aimed by the clip's own tool track: the same mesh, the same
    /// socket and the same aim the player's tool takes. No sprite tool is ever drawn on a mesh figure.
    ///
    /// <para><b>A function of the clock</b> (rule 5): the answer is read off the routine's pose for the
    /// hour, never latched and never saved. A looping work clip runs from absolute game time, its phase
    /// moved by the figure's own key, so two diggers side by side do not strike in step.</para>
    ///
    /// <para>Stands that do not implement this keep their ordinary stance and gait presentation, and a
    /// stand that answers a state its skin cannot draw as a mesh, or a prop its figure cannot hold, is
    /// drawn as if it had answered false: idle, empty-handed, and a mesh.</para>
    /// </summary>
    public interface ICharacterFigureWorkStand : ICharacterFigureAshoreStand
    {
        /// <summary>
        /// True while the person is at work this frame. <paramref name="stateKey"/> is the work clip's state
        /// key in the skin (<c>dig</c>), as data; <paramref name="heldProp"/> is the tool, or null for work
        /// done with bare hands.
        /// </summary>
        bool TryGetFigureWork(out string stateKey, out HeldPropMeshDef heldProp);
    }
}
