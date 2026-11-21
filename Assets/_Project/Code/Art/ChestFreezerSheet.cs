using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>How full a freezer looks: the baked frozen-catch bed under the fish laid on top.</summary>
    public enum FreezerFillRung
    {
        Empty = 0,
        Some = 1,
        Full = 2,
    }

    /// <summary>
    /// <b>The chest freezer's sheet</b>: one row per state, one column per facing, named
    /// <c>&lt;stem&gt;_s&lt;row&gt;_d&lt;cell&gt;</c> like every appliance sheet, baked from the appliance rig's
    /// chest freezer piece. Shared by the baker (which renders exactly these rows) and the runtime art (which
    /// picks from them), so the two cannot drift.
    ///
    /// <para>Rows: closed, two lid frames on the way up, then open with the empty, some and full beds, then the
    /// open chest's NEAR layer (its front wall and rim), drawn over the fish laid inside so they sit in the
    /// chest and never over its front.</para>
    ///
    /// <para><b>Size is data.</b> <see cref="Pieces"/> lists every freezer sheet the rig bakes (a rig piece and
    /// its stem); a bigger freezer is one more rig entry, one more row here and its own Def naming the stem.</para>
    /// </summary>
    public static class ChestFreezerSheet
    {
        /// <summary>The Ginny's-size chest: its rig piece and its sheet stem.</summary>
        public const string RigName = "chestFreezer";
        public const string Stem = "Appliance_" + RigName;

        /// <summary>Every freezer sheet the baker makes: (rig piece, sheet stem).</summary>
        public static readonly (string RigName, string Stem)[] Pieces = { (RigName, Stem) };

        /// <summary>The <c>Resources</c> folder the appliance sheets live in (Art/Resources/Appliances).</summary>
        public const string ResourcesFolder = "Appliances";

        /// <summary>Facings per sheet: the rooms' own eight.</summary>
        public const int Facings = 8;

        /// <summary>The facing cell drawn front-on: the piece backed onto a wall at the top of the screen.</summary>
        public const int FrontCell = 0;

        public const int Closed = 0;
        public const int LidLow = 1;
        public const int LidHigh = 2;
        public const int OpenEmpty = 3;
        public const int OpenSome = 4;
        public const int OpenFull = 5;
        public const int NearOpen = 6;
        public const int Rows = 7;

        /// <summary>The rows the lid plays through from closed to open (the open row is the rung's).</summary>
        public static readonly int[] LidFrames = { Closed, LidLow, LidHigh };

        /// <summary>The open row that shows <paramref name="rung"/>'s bed.</summary>
        public static int OpenRow(FreezerFillRung rung) => rung switch
        {
            FreezerFillRung.Full => OpenFull,
            FreezerFillRung.Some => OpenSome,
            _ => OpenEmpty,
        };

        /// <summary>The rig options that draw one row (the baker's contract).</summary>
        public static string OptsFor(int row) => row switch
        {
            LidLow => "{lid:0.33}",
            LidHigh => "{lid:0.67}",
            OpenEmpty => "{lid:1}",
            OpenSome => "{lid:1,heap:1}",
            OpenFull => "{lid:1,heap:2}",
            NearOpen => "{lid:1,split:'near'}",
            _ => "{}",
        };

        /// <summary>The sprite name of one row at one facing cell.</summary>
        public static string SpriteName(string stem, int row, int cell) => $"{stem}_s{row}_d{cell}";

        /// <summary>Parse <c>&lt;stem&gt;_s&lt;row&gt;_d&lt;cell&gt;</c>.</summary>
        public static bool TryParse(string spriteName, string stem, out int row, out int cell)
        {
            row = cell = -1;
            if (spriteName == null || stem == null || !spriteName.StartsWith(stem + "_s")) return false;
            string rest = spriteName.Substring(stem.Length + 2);
            int d = rest.IndexOf("_d", System.StringComparison.Ordinal);
            if (d <= 0) return false;
            return int.TryParse(rest.Substring(0, d), out row) && int.TryParse(rest.Substring(d + 2), out cell)
                   && row >= 0 && cell >= 0;
        }

        private static readonly Dictionary<string, Sprite[]> Sheets = new Dictionary<string, Sprite[]>();

        /// <summary>A stem's sprites, indexed <c>row × facings + cell</c>, loaded once per stem. Missing cells
        /// are null.</summary>
        public static Sprite[] SheetFor(string stem)
        {
            if (string.IsNullOrEmpty(stem)) return null;
            if (Sheets.TryGetValue(stem, out Sprite[] cached)) return cached;
            var sprites = new Sprite[Rows * Facings];
            Sprite[] loaded = Resources.LoadAll<Sprite>(ResourcesFolder + "/" + stem);
            for (int i = 0; i < loaded.Length; i++)
                if (TryParse(loaded[i].name, stem, out int row, out int cell) && row < Rows && cell < Facings)
                    sprites[row * Facings + cell] = loaded[i];
            Sheets[stem] = sprites;
            return sprites;
        }

        /// <summary>One sprite of a stem's sheet, or null.</summary>
        public static Sprite SpriteAt(string stem, int row, int cell)
        {
            Sprite[] sheet = SheetFor(stem);
            int i = row * Facings + (((cell % Facings) + Facings) % Facings);
            return sheet != null && row >= 0 && i < sheet.Length ? sheet[i] : null;
        }
    }
}
