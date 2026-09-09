namespace HiddenHarbours.Core
{
    /// <summary>
    /// The one sentence a landed catch says (juice charter §4.4, the silent dig #803): "Lifted out a
    /// soft-shell clam — it's in your hand." A shellfish is LIFTED OUT (of the flat); everything else is
    /// LANDED. The line is built once per catch, never per frame; the notebook's register keeps it
    /// and the toast shows it — <b>the one on-screen consequence of <c>CatchLanded</c></b>.
    /// </summary>
    public static class LiftLine
    {
        public static string For(in CatchItem item)
        {
            string name = string.IsNullOrEmpty(item.DisplayName) ? "catch" : item.DisplayName.ToLowerInvariant();
            string verb = item.Category == FishCategory.Shellfish ? "Lifted out" : "Landed";
            return $"{verb} {Article(name)} {name} — it's in your hand.";
        }

        private static string Article(string lowerName)
        {
            if (string.IsNullOrEmpty(lowerName)) return "a";
            char c = lowerName[0];
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' ? "an" : "a";
        }
    }
}
