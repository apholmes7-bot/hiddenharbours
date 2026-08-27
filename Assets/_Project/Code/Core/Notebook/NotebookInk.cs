using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The book's colours, in one place</b> — cover, stock, rule and the three weights of ink.
    ///
    /// <para>They lived as private fields on <see cref="NotebookPresenter"/> while the book was the only
    /// thing drawn in this hand. The 2026-08-22 diegetic ruling put the current quest in a corner note
    /// drawn in the same hand (<see cref="QuestPanelPresenter"/>), and a second copy of seven colours is
    /// a guarantee that one day the note is a shade off the page it is supposed to have come from.</para>
    ///
    /// <para>These ARE the dialogue bubble kit's palette — the notebook took them from there so the
    /// book, the bubble and now the note all read as one surface. Fallback tints only: a baked piece
    /// draws its own pixels and is tinted white, so these are what an UNBAKED kit shows, plus the ink
    /// every glyph is actually set in.</para>
    /// </summary>
    public static class NotebookInk
    {
        /// <summary>The board the leaves are bound into — the dark green-black of the cover.</summary>
        public static readonly Color Cover = new Color(0.055f, 0.180f, 0.180f, 1f);

        /// <summary>Warm paper stock.</summary>
        public static readonly Color Paper = new Color(0.921f, 0.886f, 0.800f, 1f);

        /// <summary>The printed ruled line — present whether or not anything sits on it.</summary>
        public static readonly Color Rule = new Color(0.451f, 0.478f, 0.478f, 0.55f);

        /// <summary>Her hand: the weight nearly everything is written in.</summary>
        public static readonly Color Ink = new Color(0.145f, 0.157f, 0.172f, 1f);

        /// <summary>Gone over twice — a title, a ticked box.</summary>
        public static readonly Color InkStrong = new Color(0.086f, 0.094f, 0.105f, 1f);

        /// <summary>Faded: a step already done, a page mark.</summary>
        public static readonly Color InkFaint = new Color(0.145f, 0.157f, 0.172f, 0.45f);

        /// <summary>The one warm accent — the current-step arrow, the selected stub, the done stamp.</summary>
        public static readonly Color Gold = new Color(0.839f, 0.667f, 0.325f, 1f);

        /// <summary>Tab card stock, a shade off the leaves so a stub reads as card and not as paper.</summary>
        public static readonly Color Tab = new Color(0.788f, 0.741f, 0.639f, 1f);
    }
}
