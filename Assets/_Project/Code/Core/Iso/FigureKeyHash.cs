namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The ONE hash of a figure's key</b> (<see cref="ICharacterFigureIdentity.FigureKey"/>, an NPC's
    /// <c>NpcDef</c> id): FNV-1a over its chars. It moves a villager's idle phase ashore, seeds every
    /// figure's blink, and places a villager angler's session grid (<see cref="AnglerTimeline"/>). Moved
    /// down from the Art figure presenter so World, Player and Art all hash a key the same way.
    ///
    /// <para>0 for a null or empty key, which is the phase with no offset of its own (a key that hashes to
    /// 0 is the same, one in four billion). Pure: the same key gives the same hash in every process and on
    /// every platform, never <c>string.GetHashCode</c>, which is salted per process (rule 5).</para>
    /// </summary>
    public static class FigureKeyHash
    {
        public static uint Of(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0u;
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < key.Length; i++) h = (h ^ key[i]) * 16777619u;
                return h;
            }
        }
    }
}
