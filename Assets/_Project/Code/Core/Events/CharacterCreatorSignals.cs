namespace HiddenHarbours.Core
{
    /// <summary>The opening asks for choices, or cancels them when their context ends.</summary>
    public readonly struct CharacterCreatorRequested
    {
        public readonly bool Open;

        public CharacterCreatorRequested(bool open) { Open = open; }
    }

    /// <summary>The choices have closed and no longer hold modal input.</summary>
    public readonly struct CharacterCreatorClosed
    {
        public readonly bool Committed;

        public CharacterCreatorClosed(bool committed) { Committed = committed; }
    }
}
