namespace HiddenHarbours.Core
{
    /// <summary>The player's optional name. PlayerNameChanged redraws standing text after a change.</summary>
    public static class PlayerName
    {
        public const string Token = "{player}";

        public static string Of(SaveData save) => save?.PlayerName ?? string.Empty;

        /// <summary>Resolve authored words once when drawn, before wrapping or typewriting them.
        /// Literal replacement preserves other bindings and braces in the player's own name.</summary>
        public static string Resolve(string text) => Resolve(text, GameServices.Save?.Current);

        public static string Resolve(string text, SaveData save)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(Token, System.StringComparison.Ordinal) < 0)
                return text;

            string name = Of(save);
            if (string.IsNullOrWhiteSpace(name)) name = GameServices.PlayerNameFallback;
            return text.Replace(Token, name);
        }

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
            string previous = Of(save.Current);
            if (string.Equals(previous, next, System.StringComparison.Ordinal)) return false;
            save.Current.PlayerName = next;
            save.Save();
            EventBus.Publish(new PlayerNameChanged(previous, next));
            return true;
        }
    }

    /// <summary>Published after a changed name has been saved. Spoken lines keep their original words.</summary>
    public readonly struct PlayerNameChanged
    {
        public readonly string PreviousName;
        public readonly string Name;

        public PlayerNameChanged(string previousName, string name)
        {
            PreviousName = previousName;
            Name = name;
        }
    }
}
