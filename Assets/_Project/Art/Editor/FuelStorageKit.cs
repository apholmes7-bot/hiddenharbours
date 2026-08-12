using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The fuel storage &amp; dispensing kit's identity: what gets baked, at what facings and fills,
    /// and the contract schema the slicer and the runtime presenter read back.
    ///
    /// <para>Everything here is DATA. The scope of a bake is <see cref="Builds"/> — one row per
    /// (type × size × grade) sheet — so re-scoping the kit is a table edit, never a code change.
    /// Cell geometry, pivots and the crop rect are read FROM THE RIG at bake time (ADR 0021 §4);
    /// this file never restates them.</para>
    /// </summary>
    public static class FuelStorageKit
    {
        /// <summary><see cref="HiddenHarbours.Tools.RigBaking.RigCatalog"/> key.</summary>
        public const string RigKey = "fuel";

        /// <summary>The global the rig installs.</summary>
        public const string GlobalName = "FuelIso";

        public const string OutputFolder = "Assets/_Project/Art/Sprites/FuelStorage/Iso";
        public const string ContractPath = OutputFolder + "/FuelStorage.json";

        /// <summary>
        /// Unity's DEFAULT import ceiling, and the one this kit solves its packing for.
        ///
        /// <para>⚠️ The bake cap and the IMPORT cap must be the SAME number. They fail in opposite
        /// directions: above Unity's hard 4096 a bake refuses, but BETWEEN this value and 4096 the
        /// bake succeeds and the import silently DOWNSCALES with the sprite count still correct —
        /// which poisons every slice rect without a single error. Measured: the worst sheet this kit
        /// produces is the 50 kL bulk tank at 4 facings × 5 fills = 744 × 1140, so unlike the shop
        /// and interior kits this one has no reason to lift the cap.</para>
        /// </summary>
        public const int ImportSizeCap = 2048;

        /// <summary>Pixels per metre. The fleet's scale, and the rig's own <c>S</c>.</summary>
        public const int PixelsPerUnit = 32;

        /// <summary>
        /// The wear state every sheet bakes at, and it is passed EXPLICITLY on every single render.
        ///
        /// <para>⚠️ <b>Omitting <c>wear</c> does not mean "the default" — it selects a phantom fourth
        /// state, and that state COLLIDES IN THE RIG'S GEOMETRY CACHE with this one.</b> The streak
        /// passes receive <c>o.wear</c> raw and bail on a falsy value (<c>if (!wear || wear ===
        /// 'fresh') return;</c>) so an omitted wear draws no rust; but <c>wearPass</c> and the cache
        /// key both normalise the same call to <c>'working'</c>. Two renders that differ in pixels
        /// therefore share one cache key and WHICHEVER RUNS FIRST WINS for the rest of the session.
        /// Measured on drum s205 dir 3: omitted, 'fresh' and 'working' are three distinct images, and
        /// calling omitted-then-'working' returns the omitted one. See
        /// <c>FuelRegistrationProbe.MeasureWearCacheDefect</c>, which asserts the defect still exists
        /// and goes RED the day a drop fixes it.</para>
        /// </summary>
        public const string BakedWear = "working";

        // ---- the fill axis ---------------------------------------------------------------------

        /// <summary>
        /// The fill fractions baked, low to high. <c>fill</c> is a VOLUME fraction and the rig solves
        /// the surface height itself, so a horizontal-cylinder skid tank at 0.25 by volume stands
        /// 29.8% of the way up its diameter — these are not evenly-spaced pictures.
        ///
        /// <para>The rig is CONTINUOUS in fill, not stepped: <c>geo()</c> quantises to
        /// <c>Math.round(fill*96)/96</c>, and all 97 quanta render distinctly on a jerry can. So
        /// there is no rig-declared set of levels to inherit and this is a chosen granularity —
        /// the handoff's default of five (empty · ¼ · ½ · ¾ · full).</para>
        /// </summary>
        public static readonly IReadOnlyList<float> Fills = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };

        /// <summary>
        /// Types whose <c>read</c> is <c>'none'</c> — they hold no volume, so fill is a NO-OP.
        ///
        /// <para>This is a MEASUREMENT, not a scope cut: sweeping all 97 fill quanta through a
        /// nozzle and a dispenser yields exactly ONE distinct image each. Baking five identical
        /// rows would ship four duplicate copies of every dispenser.</para>
        /// </summary>
        public static readonly IReadOnlyList<string> HoldsNothing = new[] { "nozzle", "pump" };

        // ---- the facing axis -------------------------------------------------------------------

        /// <summary>Facings for a vessel the player picks up, turns and sets down at any angle.</summary>
        public const int CarryFacings = 8;

        /// <summary>
        /// Facings for a vessel that stands where it is put.
        ///
        /// <para>⚠️ This is the OWNER'S CALL, taken at the handoff's stated default and costed in the
        /// PR body. All eight facings are genuinely distinct at every wear state (measured — unlike
        /// the nav-buoy kit, nothing here collapses), so this is a budget decision and not a
        /// free one: eight facings for the storage half would take the kit from 8.9 to 17.7 Mpx.
        /// Changing this constant and re-baking updates the sheets, the contract and the slice with
        /// no other edit.</para>
        /// </summary>
        public const int StoreFacings = 4;

        // ---- the build table -------------------------------------------------------------------

        /// <summary>One baked sheet: a vessel of one size carrying one grade, all facings × fills.</summary>
        public readonly struct Build
        {
            public readonly string Type, Size, Grade;

            public Build(string type, string size, string grade)
            {
                Type = type; Size = size; Grade = grade;
            }

            /// <summary>Sheet stem, e.g. <c>jerry_s20_gas</c>. Also the contract key.</summary>
            public string Key => $"{Type}_{Size}_{Grade}";

            public override string ToString() => Key;
        }

        /// <summary>
        /// Grade codes, in the rig's own <c>FUEL_ORDER</c>. Kept here so the build table can be
        /// generated without a host; <c>FuelRegistrationProbe</c> checks this list against the live
        /// rig at bake time, because a grade added upstream and missed here bakes a short kit.
        /// </summary>
        public static readonly IReadOnlyList<string> Grades = new[] { "gas", "diesel", "mixed", "oil" };

        /// <summary>
        /// Every vessel the rig draws, in <c>TYPE_ORDER</c>, with its sizes in <c>sizesOf()</c> order.
        /// Cross-checked against the live rig at bake time for exactly the same reason as
        /// <see cref="Grades"/>.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string[]> Sizes =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["jug"]    = new[] { "s1", "s4", "s10" },
                ["jerry"]  = new[] { "s5", "s10", "s20", "s25" },
                ["nozzle"] = new[] { "sAuto", "sHigh" },
                ["drum"]   = new[] { "s60", "s205" },
                ["tote"]   = new[] { "s600", "s1000" },
                ["skid"]   = new[] { "s500", "s1200", "s2500" },
                ["bulk"]   = new[] { "s10k", "s25k", "s50k" },
                ["pump"]   = new[] { "sSingle", "sTwin" },
            };

        /// <summary>Types the player carries — <c>kind: 'carry'</c> in the rig's table.</summary>
        public static readonly IReadOnlyList<string> CarryTypes = new[] { "jug", "jerry", "nozzle" };

        public static bool IsCarry(string type) => CarryTypes.Contains(type);

        /// <summary>Facings for a type. Carriables turn in the hand; storage stands still.</summary>
        public static int FacingsFor(string type) => IsCarry(type) ? CarryFacings : StoreFacings;

        /// <summary>
        /// Fill fractions for a type — the full ladder for anything that holds a volume, and a single
        /// frame for the nozzle and the dispenser, which hold none.
        /// </summary>
        public static IReadOnlyList<float> FillsFor(string type) =>
            HoldsNothing.Contains(type) ? new[] { 0f } : Fills;

        /// <summary>
        /// The whole kit: every type × size × grade. 21 sizes × 4 grades = 84 sheets.
        ///
        /// <para>The rig will render any vessel in any grade, and this table asks for all of them
        /// rather than pruning by plausibility. A 25 kL bulk tank of two-stroke premix is not a real
        /// object — but which combinations to keep is a CONTENT call for the owner, not a silent
        /// decision for the baker to take on the way past.</para>
        /// </summary>
        public static IReadOnlyList<Build> Builds =>
            Sizes.SelectMany(kv => kv.Value.SelectMany(size =>
                     Grades.Select(grade => new Build(kv.Key, size, grade))))
                 .ToArray();

        // ---- carry mode ------------------------------------------------------------------------

        /// <summary>
        /// Whether a build bakes in the vessel's REST pose.
        ///
        /// <para>Carry types default to the CARRY pivot (the grip) and a cell that carries a 15°
        /// tilt allowance for the swing of a walking character. Nothing in this PR swings anything —
        /// carrying needs an interact verb that does not exist yet — so every carriable bakes at
        /// <c>{rest:true}</c>: the placed frame, on its own base, in the tighter cell. Measured, that
        /// is 34–79% less area than the carry cell. The carry pose is a re-bake, not a re-author.</para>
        /// </summary>
        public const bool BakeCarryTypesAtRest = true;

        // ---- the contract ----------------------------------------------------------------------

        /// <summary>
        /// One sheet, as written to <see cref="ContractPath"/>. Plain fields and
        /// <see cref="JsonUtility"/> both ways, so the serialiser and the parser are one code path.
        /// </summary>
        [Serializable]
        public class SheetEntry
        {
            public string key;             // jerry_s20_gas
            public string type, size, grade;
            public string kind;            // carry | store
            public string read;            // body | tube | board | none
            public string shape;           // box | vcyl | hcyl
            public string wear;
            public bool rest;
            public int facings;
            public int cellW, cellH;       // the CROPPED cell
            public int pivotX, pivotY;     // top-left origin, inside the cropped cell
            public int columns, rows;      // columns = facings, rows = fills
            public int sheetW, sheetH;
            public float[] fills;
            public float litres, tare, fullKg;
            public bool pivotInsideInk;

            /// <summary>Unity's normalised, BOTTOM-origin sprite pivot. The y term is
            /// <c>(H − pivotY)/H</c> per ADR 0026 — the hull convention, because a vessel's pivot is
            /// a projected POINT (its base centre on the ground plane) and not a chosen row.</summary>
            public Vector2 NormalisedPivot =>
                new Vector2(pivotX / (float)cellW, (cellH - pivotY) / (float)cellH);

            /// <summary>Sprite name for one cell. Rows are fills, columns are facings.</summary>
            public string SpriteName(int fillIndex, int facing) => $"{key}_f{fillIndex}_d{facing}";
        }

        [Serializable]
        public class Contract
        {
            public string generated;
            public string rigKey, globalName, convention;
            public int pixelsPerUnit;
            public int importSizeCap;
            public string bakedWear;
            public int carryFacings, storeFacings;
            public SheetEntry[] sheets;

            public SheetEntry Find(string key) =>
                sheets?.FirstOrDefault(s => string.Equals(s.key, key, StringComparison.Ordinal));
        }
    }
}
