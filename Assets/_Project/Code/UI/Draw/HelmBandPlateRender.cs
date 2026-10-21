using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The helm band's GREYBOX painters (H2): the panel and cap tints, the instrument housings (a
    /// named plate when fitted, a blank one when not), the pilot bands' depth digits, and the drive's
    /// gear letter. Every housing is a plate because no instrument's face fits 144 px at ×1 and the
    /// brief forbids scaling a face; a press on a named plate raises the instrument's own card.
    ///
    /// <para><b>Interim palette.</b> These colours are ported constants until H3's <c>PANEL</c> tile
    /// and plate art replace them (Phase A §8: a greybox tint is not a tunable). Day ink is light on
    /// a slate face; at night the ink turns warm amber, switched once at dusk by the band's
    /// <see cref="HelmPanelLight"/> boolean, never by the hour.</para>
    ///
    /// <para>Pure painters into a <see cref="DrawSurface"/>: no texture, no allocation beyond the
    /// font's own upper-casing of the label, and the band calls them only when a plate's key changes
    /// (rule 7).</para>
    /// </summary>
    public static class HelmBandPlateRender
    {
        public static readonly Color32 PanelDay = new Color32(40, 46, 52, 255);
        public static readonly Color32 PanelNight = new Color32(20, 24, 28, 255);
        public static readonly Color32 CapDay = new Color32(26, 30, 35, 255);
        public static readonly Color32 CapNight = new Color32(11, 13, 16, 255);

        private static readonly Color32 RimDay = new Color32(22, 26, 30, 255);
        private static readonly Color32 RimNight = new Color32(8, 10, 12, 255);
        private static readonly Color32 FaceDay = new Color32(62, 70, 78, 255);
        private static readonly Color32 FaceNight = new Color32(30, 30, 30, 255);
        private static readonly Color32 InkDay = new Color32(216, 222, 226, 255);
        private static readonly Color32 InkNight = new Color32(255, 178, 82, 255);
        private static readonly Color32 AlarmInk = new Color32(255, 74, 58, 255);

        /// <summary>The housing's inset from its piece's edges.</summary>
        public const int InsetX = 4, InsetY = 8, Radius = 6, RimPx = 2;

        /// <summary>The label's glyph scale (s2: 10 px tall) and the depth digits' (s3: 15 px).</summary>
        public const int LabelScale = 2, DigitScale = 3;

        public static Color32 Panel(bool night) => night ? PanelNight : PanelDay;

        public static Color32 Cap(bool night) => night ? CapNight : CapDay;

        public static Color32 Ink(bool night) => night ? InkNight : InkDay;

        public static Color32 Alarm => AlarmInk;

        /// <summary>An empty housing: a recessed plate with nothing on it (the unfitted instrument,
        /// or a compass mount with no compass).</summary>
        public static void Blank(DrawSurface s, bool night)
        {
            s.Clear();
            int w = s.Width - 2 * InsetX, h = s.Height - 2 * InsetY;
            RigDrawUtil.RRect(s, InsetX, InsetY, w, h, Radius, night ? RimNight : RimDay);
            RigDrawUtil.RRect(s, InsetX + RimPx, InsetY + RimPx, w - 2 * RimPx, h - 2 * RimPx,
                              Radius - 1, night ? FaceNight : FaceDay);
        }

        /// <summary>A fitted instrument's housing: its name, centred, at s2.</summary>
        public static void Named(DrawSurface s, string label, bool night)
        {
            Blank(s, night);
            RigDrawUtil.TextC(s, label, s.Width / 2.0, (s.Height - 5 * LabelScale) / 2, LabelScale, Ink(night));
        }

        /// <summary>The pilot bands' sounder housing: its name, the live depth in digits at s3 (red
        /// while the shallow alarm's blink is lit), and the hull's units.</summary>
        public static void NamedWithDepth(DrawSurface s, string label, string digits, string unit,
                                          bool alarmLit, bool night)
        {
            Blank(s, night);
            double cx = s.Width / 2.0;
            RigDrawUtil.TextC(s, label, cx, 26, LabelScale, Ink(night));
            if (!string.IsNullOrEmpty(digits))
                RigDrawUtil.TextC(s, digits, cx, 62, DigitScale, alarmLit ? AlarmInk : Ink(night));
            if (!string.IsNullOrEmpty(unit))
                RigDrawUtil.TextC(s, unit, cx, 98, LabelScale, Ink(night));
        }

        /// <summary>The drive's gear letter (F, N or R) at s3 in its own column, on a small window
        /// plate.</summary>
        public static void GearLetter(DrawSurface s, int x, int w, string letter, bool night)
        {
            int py = s.Height / 2 - 14;
            RigDrawUtil.RRect(s, x + 1, py, w - 2, 28, 4, night ? RimNight : RimDay);
            RigDrawUtil.RRect(s, x + 2, py + 1, w - 4, 26, 3, night ? FaceNight : FaceDay);
            RigDrawUtil.TextC(s, letter, x + w / 2.0, py + 7, DigitScale, Ink(night));
        }
    }
}
