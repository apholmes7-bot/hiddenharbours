using System.Text;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The last line of defence between an id and the player.</b> A thing the player reads should be
    /// named in its own data (a <c>DisplayName</c> on its Def). When that name is missing, a surface
    /// writes the id through here rather than showing it: "fish.atlantic_cod" reads "Atlantic cod",
    /// "bootCol" reads "Boot". Never the dot, never an underscore, never camelCase.
    /// </summary>
    public static class PlainName
    {
        /// <summary>A readable stand-in for an id or code name, in sentence case. Empty in, empty out.</summary>
        public static string FromId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "";
            string s = id.Trim();
            int dot = s.LastIndexOf('.');
            if (dot >= 0 && dot < s.Length - 1) s = s.Substring(dot + 1);
            if (s.EndsWith("Col") && s.Length > 3) s = s.Substring(0, s.Length - 3);

            var sb = new StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '_' || c == '-') { if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' '); continue; }
                if (char.IsUpper(c) && i > 0 && sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                sb.Append(sb.Length == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            }
            return sb.ToString().Trim();
        }
    }
}
