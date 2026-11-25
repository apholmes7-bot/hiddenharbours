using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The outhouse's sheet</b>: one row per door state, one column per facing, named
    /// <c>&lt;stem&gt;_s&lt;row&gt;_d&lt;cell&gt;</c> like every appliance sheet, baked from the appliance rig's
    /// outhouse piece. Shared by the baker (which renders exactly these rows) and the runtime art (which picks
    /// from them), so the two cannot drift.
    ///
    /// <para>Rows: the door shut, ajar, wide open (the bench showing inside). He is never drawn inside the booth
    /// (see <see cref="OuthouseArt"/>), so there is no near layer.</para>
    ///
    /// <para>The footprint and the seat are in the rig's PROP frame (metres; -Y is the door side) and must equal
    /// <c>ApplianceIso.PROPS.outhouse</c>'s <c>foot</c> and <c>seat</c>: an EditMode test holds them equal.</para>
    /// </summary>
    public static class OuthouseSheet
    {
        public const string RigName = "outhouse";
        public const string Stem = ApplianceSheets.StemPrefix + RigName;

        /// <summary>The <c>Resources</c> folder the appliance sheets live in (Art/Resources/Appliances).</summary>
        public const string ResourcesFolder = ApplianceSheets.ResourcesFolder;

        /// <summary>Facings per sheet: the same eight as every other standing piece.</summary>
        public const int Facings = ApplianceSheets.Facings;

        /// <summary>The cell drawn door-on: the door facing down the screen.</summary>
        public const int FrontCell = 0;

        public const int Closed = 0;
        public const int Ajar = 1;
        public const int Open = 2;
        public const int Rows = 3;

        /// <summary>The rows the door plays through from shut to open.</summary>
        public static readonly int[] DoorFrames = { Closed, Ajar, Open };

        /// <summary>The booth's ground footprint (x across the door face, y front to back). Equals the rig's foot.</summary>
        public static readonly Vector2 Foot = new Vector2(1.1f, 1.2f);

        /// <summary>The front edge of the bench, where he sits (z is the bench height). Equals the rig's seat.</summary>
        public static readonly Vector3 Seat = new Vector3(0f, 0.04f, 0.45f);

        /// <summary>Which way he faces sat on the bench: out of the door (the rig's heading 270).</summary>
        public static readonly Vector2 SeatFacing = new Vector2(0f, -1f);

        /// <summary>The rig options that draw one row (the baker's contract).</summary>
        public static string OptsFor(int row) => row switch
        {
            Ajar => "{leaf:0.35}",
            Open => "{leaf:1}",
            _ => "{}",
        };

        /// <summary>The sprite name of one row at one facing cell.</summary>
        public static string SpriteName(int row, int cell) => $"{Stem}_s{row}_d{cell}";

        private static Sprite[] _sheet;

        /// <summary>The sheet's sprites, indexed <c>row × facings + cell</c>, loaded once. Missing cells are null.</summary>
        public static Sprite[] Sheet()
        {
            // Cached in play only: in the editor a re-bake must be picked up at once.
            if (_sheet != null && Application.isPlaying) return _sheet;
            var sprites = new Sprite[Rows * Facings];
            Sprite[] loaded = Resources.LoadAll<Sprite>(ResourcesFolder + "/" + Stem);
            for (int i = 0; i < loaded.Length; i++)
                if (ApplianceArt.TryParse(loaded[i].name, Stem, out int row, out int cell) && row < Rows && cell < Facings)
                    sprites[row * Facings + cell] = loaded[i];
            _sheet = sprites;
            return sprites;
        }

        /// <summary>One sprite of the sheet, or null.</summary>
        public static Sprite SpriteAt(int row, int cell)
        {
            Sprite[] sheet = Sheet();
            if (row < 0 || row >= Rows) return null;
            return sheet[row * Facings + (((cell % Facings) + Facings) % Facings)];
        }

        /// <summary>The booth's walls as a world-space outline around its pivot at a facing cell (the collider).</summary>
        public static Vector2[] FootprintOutline(int cell)
        {
            float hx = Foot.x * 0.5f, hy = Foot.y * 0.5f;
            return new[]
            {
                ApplianceFacing.ModelToWorldOffset(cell, new Vector3(-hx, -hy, 0f)),
                ApplianceFacing.ModelToWorldOffset(cell, new Vector3(hx, -hy, 0f)),
                ApplianceFacing.ModelToWorldOffset(cell, new Vector3(hx, hy, 0f)),
                ApplianceFacing.ModelToWorldOffset(cell, new Vector3(-hx, hy, 0f)),
            };
        }
    }
}
