namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A fight's own seed</b>: FNV-1a over the fish id, then the weight's bit pattern, so the same fish at
    /// the same weight fights the same way twice running and two fish a gram apart fight differently. Never
    /// <c>Random</c> (rule 5). Moved down from Player's surfaced-fish drawer so a villager angler's timeline
    /// (<see cref="AnglerTimeline"/>) folds the fish into its fight exactly as the player's drawer does.
    /// Non-negative.
    /// </summary>
    public static class FishFightSeed
    {
        public static int For(string fishId, float weightKg)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (!string.IsNullOrEmpty(fishId))
                    for (int i = 0; i < fishId.Length; i++) h = (h ^ fishId[i]) * 16777619u;
                // The weight enters as its bit pattern so two fish a gram apart seed differently.
                h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(weightKg)) * 16777619u;
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }
}
