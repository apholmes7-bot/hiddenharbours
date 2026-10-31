namespace HiddenHarbours.Core
{
    /// <summary>The player's optional name. No signal is needed until something displays it.</summary>
    public static class PlayerName
    {
        public static string Of(SaveData save) => save?.PlayerName ?? string.Empty;

        public static string Check(string candidate) =>
            (candidate ?? string.Empty).Trim().Length > GameServices.PlayerNameMaxLength
                ? $"Please keep your name to {GameServices.PlayerNameMaxLength} characters or fewer."
                : null;

        /// <summary>Keep a trimmed name. True only when it changed; a refusal never changes it.</summary>
        public static bool Set(string candidate, out string refusal)
        {
            refusal = Check(candidate);
            if (refusal != null) return false;
            ISaveService save = GameServices.Save;
            if (save?.Current == null)
            {
                refusal = "No game is loaded, so there is nowhere to keep your name.";
                return false;
            }
            string next = (candidate ?? string.Empty).Trim();
            if (string.Equals(Of(save.Current), next, System.StringComparison.Ordinal)) return false;
            save.Current.PlayerName = next;
            save.Save();
            return true;
        }
    }
}
