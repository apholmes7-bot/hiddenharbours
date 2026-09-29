namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A figure's stable identity</b> — the one key its per-figure presentation state (the blink's
    /// seed, the look's target) is keyed by. Implemented by whatever a figure stands for: a moored
    /// boat's skipper answers with her owner's id, a villager with her NPC's id.
    ///
    /// <para><b>One key scheme.</b> A figure that can be asked this is asked this, and nothing else
    /// invents a second key for the same figure. Stable across runs and saves (an id, never an
    /// instance id or a scene path), so the same person blinks the same way every time.</para>
    /// </summary>
    public interface ICharacterFigureIdentity
    {
        /// <summary>The figure's stable key, or empty for none.</summary>
        string FigureKey { get; }
    }
}
