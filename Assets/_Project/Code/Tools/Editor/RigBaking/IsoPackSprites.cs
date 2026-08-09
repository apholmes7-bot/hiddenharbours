#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>THE READ SIDE OF THE ISO RIG PACK.</b> <see cref="IsoPackContract"/> is what a BAKER asserts
    /// against before it writes a pixel; this is what a REGION BUILDER asks once the pixels exist —
    /// "give me <c>trapStack</c> facing south", "give me <c>Starfish</c> wet, lie NE, variant 1".
    ///
    /// <para>It lives beside the contract rather than in <c>Art/Editor</c> for one hard reason: the
    /// slice INDEX is a function of the family's sheet axes, the contract is the only committed record
    /// of those axes, and <c>HiddenHarbours.Art.Editor</c> cannot see this assembly (the reference runs
    /// the other way). A copy of the axis rule over there would be a second source of truth for the one
    /// number that silently returns the wrong picture when it is wrong.</para>
    ///
    /// <para><b>⚠️ THE THREE TRAPS THIS CLASS EXISTS TO ABSORB.</b></para>
    /// <list type="number">
    /// <item><description><b>The sheets are <c>spriteMode: Multiple</c>.</b>
    /// <c>AssetDatabase.LoadAssetAtPath&lt;Sprite&gt;</c> returns <c>null</c> on a multiple-sliced
    /// sheet — the sprites are SUB-ASSETS. Only <see cref="AssetDatabase.LoadAllAssetsAtPath"/> sees
    /// them, and it also hands back the <see cref="Texture2D"/>, which must be filtered
    /// out.</description></item>
    /// <item><description><b>The index rule is per family.</b> The three DIRECTIONAL families
    /// (<c>wharfIso</c>, <c>wharfDecor</c>, <c>utilityIso</c>) pack 8 facings on one axis, so the index
    /// IS the facing. <c>shoreFinds</c> packs <b>8 lie angles across × 3 variants down</b> — its own
    /// contract says so in <c>sheetAxes</c> — so the index is <c>variant × 8 + lie</c> and reading it
    /// as a facing silently returns the wrong variant.</description></item>
    /// <item><description><b>A find's sheet is per STATE.</b> One PNG per find per wet/dry/bleached,
    /// named <c>&lt;Key&gt;_&lt;state&gt;.png</c> — the state is in the FILE name, the lie and variant
    /// are in the slice name. Asking for a state that has not baked yields no sheet, not a wrong
    /// one.</description></item>
    /// </list>
    ///
    /// <para><b>Null-tolerant on purpose.</b> "Declared in the contract" and "has pixels on disk" are
    /// different questions — the pack bakes to order and a region should dress with what has landed
    /// rather than throw halfway through a build. Every miss returns null/empty; the CALLER decides
    /// whether that is a warning or a failure.</para>
    /// </summary>
    public static class IsoPackSprites
    {
        /// <summary>Facings on a directional sheet — the packs' own, and the modulus a facing index is
        /// wrapped into so a caller can hand in a compass turn without pre-wrapping it.</summary>
        public const int Facings = 8;

        /// <summary>Lie angles across a shore-finds sheet (its x axis).</summary>
        public const int FindLieAngles = 8;

        /// <summary>Variants down a shore-finds sheet (its y axis).</summary>
        public const int FindVariants = 3;

        /// <summary>The three states a find bakes in, in the contract's own order.</summary>
        public static readonly string[] FindStates = { "wet", "dry", "bleached" };

        // -----------------------------------------------------------------------------------------
        //  where a sheet is
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// The folder a family's sheets bake into.
        ///
        /// <para>⚠️ <b>Read from the registry, NOT re-derived from the contract path.</b> For every
        /// family whose contract sits beside its own sheets the two are the same string — and for the
        /// deck-loop kit's five they are not, because one contract governs five folders. Deriving here
        /// would hand all five the kit's parent folder and every lookup would miss.</para>
        /// </summary>
        public static string FolderFor(string rigKey) => IsoPackContract.SheetFolderFor(rigKey);

        /// <summary>The sheet path for a directional piece — one PNG per key.</summary>
        public static string SheetPath(string rigKey, string key) => $"{FolderFor(rigKey)}/{key}.png";

        /// <summary>The sheet path for a shore find — one PNG per find PER STATE.</summary>
        public static string FindSheetPath(string find, string state) =>
            $"{FolderFor("shoreFinds")}/{find}_{state}.png";

        // -----------------------------------------------------------------------------------------
        //  the slice index
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// The slice index of a shore find's (lie angle, variant) — <c>variant × 8 + lie</c>, from the
        /// contract's <c>sheetAxes</c>: "8 lie angles across x 3 variants down", read row-major the way
        /// every slicer in this repo writes a grid.
        /// </summary>
        public static int FindSliceIndex(int lieAngle, int variant) =>
            Wrap(variant, FindVariants) * FindLieAngles + Wrap(lieAngle, FindLieAngles);

        /// <summary>Wrap an index into <c>[0, modulus)</c> for negative values too — a facing derived
        /// from a compass turn is routinely negative, and C#'s <c>%</c> keeps the sign.</summary>
        public static int Wrap(int value, int modulus)
        {
            if (modulus <= 0) return 0;
            int m = value % modulus;
            return m < 0 ? m + modulus : m;
        }

        // -----------------------------------------------------------------------------------------
        //  the facing
        // -----------------------------------------------------------------------------------------

        static readonly Dictionary<string, IsoPackContract> Contracts =
            new Dictionary<string, IsoPackContract>(StringComparer.Ordinal);

        /// <summary>A family's contract, loaded once. Null rather than throwing if it is missing — a
        /// caller that only wants a facing should not blow up a region build over an absent oracle.</summary>
        public static IsoPackContract ContractOf(string rigKey)
        {
            if (Contracts.TryGetValue(rigKey, out var cached)) return cached;
            IsoPackContract c;
            try { c = IsoPackContract.Load(rigKey); }
            catch (Exception) { c = null; }
            Contracts[rigKey] = c;
            return c;
        }

        /// <summary>
        /// Which facing cell turns a piece's FRONT toward a compass heading (N = 0, clockwise) — the
        /// frame <c>CoastPlan</c>, <c>CliffWallGeometry.AzimuthOf</c> and every bearing in this repo are
        /// written in.
        ///
        /// <para><b>⚠️ The convention is READ FROM THE CONTRACT, never assumed.</b> A sheet's
        /// <c>order</c> array reads <c>N NE E SE S SW W NW</c> — clockwise — while all three directional
        /// families in this pack are registered <b>counter-clockwise</b>, meaning cell <c>i</c> actually
        /// depicts heading <c>−45°·i</c>. That exact mislabel has shipped defects in five separate kits
        /// (<c>RigAzimuthProbe</c>'s own note), and it is invisible: every prop simply faces the wrong
        /// way and nothing fails. Reading <c>azimuth.convention</c> here means a family that is ever
        /// re-registered turns its props with it.</para>
        /// </summary>
        public static int FacingForHeading(string rigKey, float headingDegrees)
        {
            var contract = ContractOf(rigKey);
            string convention = contract?.Azi?.convention;
            bool counterClockwise = string.IsNullOrEmpty(convention) ||
                convention.Equals("counterClockwise", StringComparison.OrdinalIgnoreCase);

            int step = Mathf.RoundToInt(headingDegrees * Facings / 360f);
            return Wrap(counterClockwise ? -step : step, Facings);
        }

        /// <summary>Compass azimuth (N = 0, clockwise) of a plan direction — <c>atan2(east, north)</c>,
        /// matching <c>CliffWallGeometry.AzimuthOf</c>. Restated rather than referenced because
        /// <c>HiddenHarbours.Art</c> is a runtime assembly and this is the pack's own read side; the two
        /// are held equal by <c>NineMileCreekDressingTests</c>.</summary>
        public static float HeadingOf(Vector2 planDirection)
        {
            if (planDirection.sqrMagnitude < 1e-12f) return 0f;
            float a = Mathf.Atan2(planDirection.x, planDirection.y) * Mathf.Rad2Deg;
            return a < 0f ? a + 360f : a;
        }

        /// <summary>The facing that turns a piece standing at <paramref name="from"/> to look at
        /// <paramref name="target"/>.</summary>
        public static int FacingToward(string rigKey, Vector2 from, Vector2 target) =>
            FacingForHeading(rigKey, HeadingOf(target - from));

        // -----------------------------------------------------------------------------------------
        //  loading
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Every sub-sprite of a sliced sheet, keyed by the trailing slice INDEX. The slicers name a
        /// slice <c>&lt;stem&gt;_&lt;index&gt;</c>, so the index is parsed off the name rather than
        /// inferred from the order <see cref="AssetDatabase.LoadAllAssetsAtPath"/> happens to return.
        /// </summary>
        public static IReadOnlyDictionary<int, Sprite> SlicesOf(string sheetPath)
        {
            var byIndex = new Dictionary<int, Sprite>();
            if (string.IsNullOrEmpty(sheetPath)) return byIndex;

            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                // ⚠️ The Texture2D comes back too, and it is not a slice.
                if (!(obj is Sprite s)) continue;
                int u = s.name.LastIndexOf('_');
                if (u < 0) continue;
                if (int.TryParse(s.name.Substring(u + 1), out int n)) byIndex[n] = s;
            }
            return byIndex;
        }

        /// <summary>
        /// One facing of a directional piece, or null if the sheet or the slice is missing.
        ///
        /// <para>⚠️ <b>Directional families only.</b> <c>trapFauna</c> is registered like its four
        /// siblings but has no facing axis at all — one cell per sheet — so every facing but 0 returns
        /// null here rather than the one sprite there is. Read a catch kind by its sheet, not through
        /// this. (Nothing consumes the fauna yet; Phase B is where that read side gets designed.)</para>
        /// </summary>
        public static Sprite Facing(string rigKey, string key, int facing)
        {
            var slices = SlicesOf(SheetPath(rigKey, key));
            return slices.TryGetValue(Wrap(facing, Facings), out Sprite s) ? s : null;
        }

        /// <summary>One (state, lie angle, variant) of a shore find, or null if it has not baked.</summary>
        public static Sprite Find(string find, string state, int lieAngle, int variant)
        {
            var slices = SlicesOf(FindSheetPath(find, state));
            return slices.TryGetValue(FindSliceIndex(lieAngle, variant), out Sprite s) ? s : null;
        }

        /// <summary>
        /// True when a family has ANY sliced pixels on disk — the cheap "has this pack been baked?"
        /// a builder asks once before it warns, instead of warning per piece.
        /// </summary>
        public static bool FamilyHasSheets(string rigKey)
        {
            string folder = FolderFor(rigKey);
            if (!AssetDatabase.IsValidFolder(folder)) return false;
            return AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Length > 0;
        }
    }
}
#endif
