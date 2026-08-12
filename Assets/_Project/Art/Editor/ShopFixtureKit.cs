#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The shop kit's FIXTURE family: which of <c>ShopInterior</c>'s 32 fixtures bake as standalone
    /// props, at what dressing, and the contract the slicer and every placer read back.
    ///
    /// <para>Sibling of <see cref="ShopKit"/> (the shells and floor plans) and deliberately the same
    /// shape. What is different is the entry point: shells and levels come from <c>render()</c>, a
    /// fixture from <c>renderItem(name, dir, opts)</c>, which draws ONE fixture on its own ground
    /// point with no room around it.</para>
    ///
    /// <para><b>The scope is <see cref="Builds"/>, and it is a table.</b> One row per baked sheet. The
    /// counter is row one because the general store has been trading over a tinted rectangle since
    /// #356; fixture two is a line here, not a code change. Everything else — the cell, the pivot, the
    /// footprint, the front anchors — is read FROM THE RIG at bake time (ADR 0021 §4) and never
    /// restated in this file.</para>
    /// </summary>
    public static class ShopFixtureKit
    {
        /// <summary><see cref="HiddenHarbours.Tools.RigBaking.RigCatalog"/> key. Already registered
        /// (<c>RigCatalog.ShopKit.cs</c>) — the fixture family needs no new registration, only the
        /// globals the shop entries already install.</summary>
        public const string RigKey = "shopInterior";

        /// <summary>The global the rig installs.</summary>
        public const string GlobalName = "ShopInterior";

        public const string OutputFolder = ShopKit.ShopsRoot + "Fixtures";
        public const string ContractFileName = "shopFixtures.contract.json";
        public static string ContractPath => OutputFolder + "/" + ContractFileName;

        public const string StemPrefix = "ShopFixture_";

        /// <summary>Pixels per metre — the rig's own <c>PX</c>, asserted against it at bake time.</summary>
        public const int PixelsPerUnit = 32;

        /// <summary>Facings every fixture bakes at. The rig carries <c>DIRS: 8</c> itself; this is
        /// checked against it, never assumed.</summary>
        public const int Facings = 8;

        /// <summary>
        /// Unity's DEFAULT import ceiling, and the one this family solves its packing for.
        ///
        /// <para><b>⚠️ The bake cap and the IMPORT cap must be the SAME number</b>, and they fail in
        /// opposite directions: above Unity's hard 4096 a bake refuses, but BETWEEN this value and 4096
        /// the bake succeeds and the import silently DOWNSCALES with the sprite count still correct,
        /// which poisons every slice rect without one error.</para>
        ///
        /// <para><b>2048, and unlike <see cref="ShopKit.ImportSizeCap"/> this family has no reason to
        /// lift it.</b> MEASURED, all 32 fixtures at the general store's dressing, 8 facings packed one
        /// row wide: the largest sheet in the whole catalog is the <c>bar</c> at <b>864 × 123</b> and
        /// the counter is <b>760 × 93</b>. The binding dimension is width, and it would take a fixture
        /// two and a half times the bar's 3.2 m frontage to reach the cap. A shell is a building and a
        /// fixture is furniture — that is the whole difference, and it is why the shop kit's documented
        /// exception does not extend here.</para>
        /// </summary>
        public const int ImportSizeCap = 2048;

        // ---- the dressing every sheet bakes at ---------------------------------------------------

        /// <summary>
        /// Camera elevation, in degrees. The rig defaults to this when <c>elev</c> is omitted; it is
        /// passed EXPLICITLY on every render all the same, so that a drop which re-seats the default
        /// changes the sheets through this constant rather than behind it.
        /// </summary>
        public const double Elevation = 40;

        /// <summary>
        /// The fixture rotation every sheet bakes at, and it is <b>0 because <c>rot</c> is REDUNDANT
        /// with <c>dir</c></b> — measured, not assumed.
        ///
        /// <para><c>rot</c> turns the fixture in the world in 90° steps; <c>dir</c> turns the camera in
        /// 45° steps. Measured on the counter, <c>rot = r</c> at <c>dir 0</c> is byte-identical to
        /// <c>rot = 0</c> at <c>dir = 2r</c>, for every r. So a <c>rot</c> axis on the sheet would ship
        /// four exact duplicates of four of the eight facings. One axis, and it is the facing.</para>
        /// </summary>
        public const int BakedRot = 0;

        /// <summary>Shelf/case stock level, 0 (bare) → 1 (packed). Moves pixels (measured), so it is a
        /// baked-in decision and it is recorded in the contract.</summary>
        public const float BakedStock = 0.95f;

        /// <summary>Grime and wear, 0 → 1. Moves pixels (measured). The rig's own default, stated.</summary>
        public const float BakedWeather = 0.3f;

        /// <summary>Lamp-lit dressing. Night is a RUNTIME overlay in this kit (the room rig's own
        /// README), so the sheets bake by day.</summary>
        public const bool BakedNight = false;

        /// <summary>Dressing seed. Moves pixels (measured) — it picks which wares sit on the counter's
        /// back shelf. Fixed so a re-bake is reproducible.</summary>
        public const int BakedSeed = 7;

        // ---- the build table ---------------------------------------------------------------------

        /// <summary>
        /// One baked fixture sheet: a fixture from the rig's catalog, dressed as one trade's.
        ///
        /// <para><b>Every option that moves a pixel is on this row.</b> MEASURED against the live rig,
        /// the knobs that change a counter are: <c>type</c>, <c>size</c>, <c>stock</c>, <c>weather</c>,
        /// <c>night</c>, <c>seed</c>, <c>rot</c> and <c>elev</c>. The knobs that do NOT are everything
        /// about the room the fixture would stand in — <c>room</c>, <c>floor</c>, <c>wall</c> paper,
        /// <c>trimTone</c>, <c>storey</c>, <c>shell</c>, <c>dividers</c>, <c>beams</c>, the windows, the
        /// storefront and the item list — because <c>renderItem</c> never builds the shell.</para>
        /// </summary>
        public readonly struct Build
        {
            /// <summary>The rig's fixture id — <c>counter</c>, <c>displayCase</c>, … Checked against
            /// <c>ShopInterior.list()</c> before rendering: an unknown name draws NOTHING and does not
            /// throw (see <c>ShopFixtureSheetBaker.AssertKnown</c>).</summary>
            public readonly string Fixture;

            /// <summary>The rig's trade id the fixture is dressed as — <c>generalStore</c>, … It seeds
            /// the counter's paint, its worktop and the goods on its shelf. An unknown trade falls
            /// through to <c>generalStore</c> silently, which is the other guard.</summary>
            public readonly string Trade;

            /// <summary>
            /// The trade's build size. It has to be here and it has to match: <c>size</c> is not just a
            /// footprint dial, it also seeds the weather speckle in the rig's post pass, so a fixture
            /// baked at one size and its building at another are dressed by two different RNG streams.
            /// Seeded from <see cref="ShopKit.ShopSet"/> and asserted equal to it.
            /// </summary>
            public readonly double Size;

            public readonly float Stock, Weather;
            public readonly bool Night;
            public readonly int Seed;
            public readonly string Why;

            public Build(string fixture, string trade, double size, string why)
                : this(fixture, trade, size, BakedStock, BakedWeather, BakedNight, BakedSeed, why) { }

            public Build(string fixture, string trade, double size,
                         float stock, float weather, bool night, int seed, string why)
            {
                Fixture = fixture; Trade = trade; Size = size;
                Stock = stock; Weather = weather; Night = night; Seed = seed;
                Why = why;
            }

            /// <summary>Contract key and sheet stem suffix, e.g. <c>counter_generalStore</c>.</summary>
            public string Key => $"{Fixture}_{Trade}";

            public override string ToString() => Key;
        }

        /// <summary>
        /// The fixtures this repo bakes. <b>One row. On purpose.</b>
        ///
        /// <para>The rig draws 32 and all 32 render cleanly at 8 facings inside the cap (measured — the
        /// whole catalog is 4.25 MB at RGBA32). Baking the other 31 would be shipping art nothing
        /// places, which is CLAUDE.md rule 8, so the table is the scope and the scope is what has a
        /// consumer: the general store's counter, which five vendors have been standing behind a tinted
        /// rectangle on since #356.</para>
        /// </summary>
        public static readonly Build[] Builds =
        {
            new Build("counter", "generalStore", 0.35,
                      "#495 §6: the general store's counter is a tinted placeholder with five vendors " +
                      "on it. This is the fixture the kit already draws, at the trade and size its own " +
                      "shell bakes at."),
        };

        public static Build? FindBuild(string key)
        {
            foreach (var b in Builds)
                if (string.Equals(b.Key, key, StringComparison.Ordinal)) return b;
            return null;
        }

        // ---- names --------------------------------------------------------------------------------

        public static string StemFor(string key) => StemPrefix + key;
        public static string SheetPath(string key) => $"{OutputFolder}/{StemFor(key)}.png";

        /// <summary>Sprite name for one baked facing. Columns are facings; there is one row.</summary>
        public static string SpriteNameFor(string key, int facing) => $"{StemFor(key)}_d{facing}";

        // ---- the contract ---------------------------------------------------------------------------

        [Serializable]
        public sealed class Contract
        {
            public string note;
            public string generated;
            public string rigKey, globalName;
            public string convention;
            public string conventionNote;
            public string pivotNote;

            public int ppu;
            public int facings;
            public int importSizeCap;

            /// <summary>The rig's native buffer, before the union crop — a rig constant here (unlike a
            /// shop LEVEL, whose cell is per trade), recorded so a drop that resizes it shows in the diff.</summary>
            public int nativeCellW, nativeCellH;

            public Entry[] fixtures;

            public Entry Find(string key) =>
                fixtures?.FirstOrDefault(e => string.Equals(e.key, key, StringComparison.Ordinal));
        }

        [Serializable]
        public sealed class Entry
        {
            public string key;              // counter_generalStore
            public string fixture, trade, label, cat;
            public float size;

            /// <summary>The EXACT options expression <c>renderItem()</c> was handed — the audit trail,
            /// and the only complete statement of what these pixels are.</summary>
            public string optionsJs;

            public float stock, weather;
            public bool night;
            public int seed, rot;
            public float elev;

            /// <summary>Sheet file name (no folder).</summary>
            public string sheet;

            public int facings, columns, rows;

            /// <summary>The CROPPED cell every facing is packed at.</summary>
            public int cellW, cellH;

            /// <summary>Where the crop was taken from in the rig's native buffer.</summary>
            public int cropX, cropY;

            /// <summary>The fixture's GROUND CENTRE in cropped-cell px from the cell's TOP-LEFT.
            /// Identical at every facing by construction — <c>renderItem</c> places the fixture at world
            /// (0,0), and the projection of the origin does not move with the camera. Measured all the
            /// same (<c>project(d,[0,0,0])</c> deviates 0 px over 8 facings).</summary>
            public int pivotX, pivotY;

            public int sheetW, sheetH;

            /// <summary>Whether the pivot lands on drawn ink. Informational: a counter's ground centre
            /// sits under its own carcase, so it does — a rug's would too, a wall board's would not.</summary>
            public bool pivotInsideInk;

            /// <summary>The rig's footprint for this fixture, in metres (<c>fixtureFoot</c> at rot 0):
            /// <c>x</c> across the service face, <c>y</c> front-to-back. The honest metric size — the
            /// drawn sprite is bigger, because the ¾ camera draws its height as depth.</summary>
            public float footprintWidthMetres, footprintDepthMetres;

            /// <summary>
            /// Per FACING (not per rig dir), in cropped-cell px from the top-left: the centre of the
            /// fixture's SERVICE FACE — the side a customer stands at.
            ///
            /// <para>Deliberately the same shape and semantics as <see cref="ShopKit.Entry.doorX"/>, so
            /// <see cref="BuildingFacing"/> aims a counter with the code that already aims a door. The
            /// service face is <c>+y</c> in the model at rot 0 (the rig's <c>anchors()</c> puts the queue
            /// at <c>+dv</c> and the keeper's station at <c>−dv</c>), and this records it as MEASURED
            /// pixels rather than as that sentence.</para>
            /// </summary>
            public float[] frontX, frontY;

            public long pngBytes;
            public long runtimeBytesRgba32;

            /// <summary>Sheet stem — the file name without the extension.</summary>
            public string Stem => StemFor(key);

            public string SpriteName(int facing) => SpriteNameFor(key, facing);

            /// <summary>Unity's normalised, BOTTOM-origin sprite pivot. The y term is
            /// <c>(H − pivotY)/H</c> per ADR 0026 — the hull convention, because a fixture's pivot is a
            /// projected POINT (its ground centre) and not a chosen row.</summary>
            public Vector2 NormalisedPivot =>
                cellW <= 0 || cellH <= 0
                    ? new Vector2(0.5f, 0.5f)
                    : new Vector2(pivotX / (float)cellW, (cellH - pivotY) / (float)cellH);
        }

        /// <summary>The committed contract, or null with a logged error if it is missing/unparseable.</summary>
        public static Contract Load()
        {
            string abs = AbsoluteContractPath;
            if (!File.Exists(abs))
            {
                Debug.LogError(
                    $"[ShopFixtureKit] No contract at '{ContractPath}'. Run Hidden Harbours ▸ Art ▸ " +
                    "Bake Shop Fixtures — the sheets and this file are written together, and a consumer " +
                    "reading one without the other places a stale sheet on a fresh pivot.");
                return null;
            }

            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(abs));
            if (contract?.fixtures == null || contract.fixtures.Length == 0)
            {
                Debug.LogError($"[ShopFixtureKit] '{ContractPath}' parsed but carries no fixtures.");
                return null;
            }
            return contract;
        }

        public static string AbsoluteContractPath =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, ContractPath);

        /// <summary>sRGB lives on <see cref="TextureImporterSettings"/> rather than on the importer —
        /// the same indirection <see cref="ShopKit.SRgbOf"/> records, restated because Art.Editor kits
        /// do not reference each other's helpers.</summary>
        public static bool SRgbOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.sRGBTexture;
        }
    }
}
#endif
