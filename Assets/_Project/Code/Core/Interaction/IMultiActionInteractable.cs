namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One thing that offers more than one action</b> (owner ask 2026-11-15: a choice appears when the
    /// player stands somewhere with several options). Optional, beside <see cref="IInteractable"/>: a
    /// registrant implementing it with two or more actions is expanded by
    /// <see cref="InteractChoiceList"/> into one row per action, and a press on it opens the chooser.
    ///
    /// <para>With one action (or none) it behaves exactly like a plain <see cref="IInteractable"/>: the
    /// press calls <see cref="IInteractable.Interact"/>. No registrant implements this on main yet; the
    /// seam exists so the first one (a shop counter: Buy / Sell / Talk) needs no chooser change.</para>
    /// </summary>
    public interface IMultiActionInteractable
    {
        /// <summary>How many actions are offered right now. Read live.</summary>
        int ActionCount { get; }

        /// <summary>What action <paramref name="index"/> says, in the <see cref="IInteractable.VerbLabel"/>
        /// style: present tense, a few words, never the key.</summary>
        string ActionLabel(int index);

        /// <summary>False greys the row and makes it unpickable (the chooser skips it).</summary>
        bool ActionAvailable(int index);

        /// <summary>Do action <paramref name="index"/>.</summary>
        void Interact(in InteractActor actor, int index);
    }
}
