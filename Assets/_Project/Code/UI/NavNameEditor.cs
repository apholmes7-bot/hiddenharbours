namespace HiddenHarbours.UI
{
    /// <summary>
    /// The waypoint NAME editor's pure model (ADR 0025 S6 PR 4): a buffer, the charset rule that keeps
    /// what you type drawable, and the commit/cancel outcomes. No input devices and no drawing, so the
    /// whole text policy is EditMode-testable — the discipline every other piece of this instrument's
    /// logic already follows.
    ///
    /// <para><b>Why sanitising is a policy here and not a backstop.</b> The shared 3×5 font
    /// (<see cref="RigDrawUtil"/>) carries A–Z, 0–9, space and a short punctuation set, and rasterises
    /// anything else as a loud tofu block plus a console error. That guard exists to catch a
    /// PROGRAMMER writing a label the font cannot draw. A PLAYER pressing a key is not a bug, so their
    /// input is folded into the charset on the way in — an accented letter or an emoji becomes nothing
    /// rather than a black box on the chart, and the tofu guard never fires for a name.</para>
    ///
    /// <para><b>Kept to the characters a name wants</b>, not to everything the font can draw. The
    /// table also holds ':' and '°' (bearings and clocks) and '·' — which is an authored BLANK, pinned
    /// as a gap on purpose. A '·' in a name would type as an invisible character, which is exactly the
    /// sort of thing that makes two waypoints look identical and behave differently, so it is dropped
    /// with the rest.</para>
    ///
    /// <para><b>Uppercase, because the glass is.</b> <see cref="RigDrawUtil.Text"/> upper-cases before
    /// it rasterises, so a lower-case buffer would draw as upper-case and then read back differently
    /// from the save. Folding at the door keeps what is stored and what is shown the same string.</para>
    /// </summary>
    public sealed class NavNameEditor
    {
        /// <summary>
        /// Longest name the editor will take.
        ///
        /// <para>Twelve, from the two places a name is drawn. The manager column prints the first seven
        /// characters (navRig.js:429 <c>w.name.slice(0,7)</c>), so anything past that is only ever seen
        /// on the CHART, where the label sits on a plate sized to the string
        /// (<c>NavRigRender.WaypointGlyph</c>). A long name there is not clipped — it smears across the
        /// water it is supposed to mark. Twelve keeps the plate under a fifth of the chart at the
        /// closest range while still fitting the names a skipper actually writes.</para>
        /// </summary>
        public const int MaxLength = 12;

        private string _text = "";

        /// <summary>Which waypoint (index into the region's list) is being renamed, or −1 when the
        /// editor is closed. Held here rather than beside it so a selection that moves under the editor
        /// cannot rename the wrong row.</summary>
        public int Target { get; private set; } = -1;

        /// <summary>True while the editor owns the keyboard.</summary>
        public bool IsOpen => Target >= 0;

        /// <summary>The buffer, always already sanitised. Never null.</summary>
        public string Text => _text;

        /// <summary>
        /// Open on a waypoint, seeded with its current name (so a rename is an edit, not a retype).
        /// The seed goes through the same charset rule as typing: a name written by an older build, or
        /// by a future importer, cannot put an undrawable character into the buffer.
        /// </summary>
        public void Open(int target, string seed)
        {
            Target = target < 0 ? -1 : target;
            _text = Target < 0 ? "" : Sanitize(seed);
        }

        /// <summary>Take one typed character. Returns true iff the buffer changed — the host's cue that
        /// the glass owes a repaint, and the reason a rejected keystroke costs nothing.</summary>
        public bool Type(char ch)
        {
            if (!IsOpen || _text.Length >= MaxLength) return false;
            if (!TryFold(ch, out char folded)) return false;
            // A leading space would be invisible and would make two names sort and read alike.
            if (folded == ' ' && _text.Length == 0) return false;
            _text += folded;
            return true;
        }

        /// <summary>Rub out the last character. Returns true iff there was one.</summary>
        public bool Backspace()
        {
            if (!IsOpen || _text.Length == 0) return false;
            _text = _text.Substring(0, _text.Length - 1);
            return true;
        }

        /// <summary>
        /// Finish the edit and hand back the name to store. Returns false — and stores nothing — for a
        /// buffer that is empty or all spaces: a waypoint with no label is a dot you cannot tell from
        /// the next one, and blanking a name by pressing Enter on an empty field would be far too easy
        /// a way to lose one.
        /// </summary>
        public bool TryCommit(out string name)
        {
            name = IsOpen ? _text.Trim() : "";
            bool ok = name.Length > 0;
            Close();
            return ok;
        }

        /// <summary>Abandon the edit. Always safe, including when already closed.</summary>
        public void Close()
        {
            Target = -1;
            _text = "";
        }

        // ---- the charset rule -------------------------------------------------------------------------

        /// <summary>
        /// Fold one typed character into the font's charset. False means "drop it" — the caller must
        /// not substitute anything, because a silent replacement character is how you end up with two
        /// waypoints called the same thing.
        /// </summary>
        public static bool TryFold(char ch, out char folded)
        {
            folded = char.ToUpperInvariant(ch);
            if (folded >= 'A' && folded <= 'Z') return true;
            if (folded >= '0' && folded <= '9') return true;
            return folded == ' ' || folded == '-' || folded == '.' || folded == '/';
        }

        /// <summary>Fold a whole string, dropping what the font cannot draw and clamping to
        /// <see cref="MaxLength"/>. Null-safe; the identity on a string that is already clean.</summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char ch in raw)
            {
                if (sb.Length >= MaxLength) break;
                if (!TryFold(ch, out char folded)) continue;
                if (folded == ' ' && sb.Length == 0) continue;
                sb.Append(folded);
            }
            return sb.ToString().TrimEnd();
        }
    }
}
