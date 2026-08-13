#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The COMMERCIAL BLOCK: which shops St Peters builds out of the 2026-08-06 shop kit, where their
    /// sheets land, and the contract every consumer reads back. Sibling of <see cref="VillageBuildingKit"/>
    /// (the dwellings) and <see cref="InteriorKit"/> (their insides), and deliberately shaped the same way.
    ///
    /// <para><b>What is different here, and it is the whole reason this is its own kit.</b> A house and its
    /// interior are two rigs that had to be REGISTERED to each other after the fact — measured, corrected,
    /// and carried as <c>InteriorKit.ExteriorFacingOffset</c>. The shop kit's three rigs were authored
    /// against one footprint formula, so shell, room and whole-building plan already agree. That is worth
    /// stating because it is also the trap: the agreement holds only when all three rigs are LOADED, and
    /// every failure of that is silent. See <c>RigCatalog</c>'s shop entries for the three measured cases.</para>
    ///
    /// <para><b>One level per build, and the shell is a build too.</b> The exterior comes from
    /// <c>shopfrontRig</c>, the inside of the same building from <c>shopBuildingRig</c> (the whole plan
    /// merged into one bake, not per-room sheets). They share a trade and a <c>size</c>, which is what makes
    /// the room land under its own shell — <see cref="Build.Size"/> is carried on the build rather than left
    /// to each call site precisely so the two cannot drift.</para>
    /// </summary>
    public static class ShopKit
    {
        public const string ShopsRoot = "Assets/_Project/Art/Sprites/Buildings/Shops/";
        public const string ContractFileName = "shops.contract.json";
        public static string ContractPath => ShopsRoot + ContractFileName;

        public const string ShellStemPrefix = "Shopfront_";
        public const string LevelStemPrefix = "ShopLevel_";

        /// <summary>
        /// The importer texture cap, and the cap the PACK is solved against —
        /// <c>ChooseGrid</c> adds a row rather than emitting a sheet the import would silently shrink.
        /// Over the cap Unity downscales and the sprite COUNT still comes out right, so only a cell-size
        /// or pivot assert catches it (<c>ShopKitTests</c> is that assert).
        ///
        /// <para><b>⚠️ 4096, and this kit is the first to need it — so here is the measurement, FROM A
        /// REAL BAKE.</b> <see cref="InteriorKit.ImportSizeCap"/> declines to lift on principle (a 4096
        /// texture is the first thing a later mobile port has to undo, CLAUDE.md rule 7) and says the
        /// union crop is what makes 2048 hold. Every cell below is what <c>Bake Shops</c> actually wrote,
        /// not what a harness predicted — and <b>the bake moved the argument</b>. The pre-bake estimate
        /// had the restaurant's SHELL as the binding build at a 716×605 crop; the shell in fact crops to
        /// 650×584 and packs 3×3 at 1950×1752, <i>inside</i> 2048. The sheet that actually needs the cap
        /// is its GROUND FLOOR PLAN, which nothing had measured:</para>
        /// <list type="bullet">
        ///   <item>general store shell 554×517 → 3×3 = 1662×1551 ✔ · ground plan 412×347 → 4×2 = 1648×694 ✔</item>
        ///   <item>post office shell 498×477 → 4×2 = 1992×954 ✔ · ground plan 354×307 → 4×2 = 1416×614 ✔</item>
        ///   <item>restaurant shell 650×584 → 3×3 = 1950×1752 ✔ ·
        ///         <b>ground plan 696×559 — no grid fits 2048</b>: 3×3 is 2088 wide (over by <b>40 px</b>),
        ///         4×2 is 2784 wide, 2×4 is 2236 tall.</item>
        /// </list>
        /// <para>The restaurant is simply a bigger building than anything the village kit holds — a dining
        /// room, a bar and a kitchen wing under one roof, against a cottage — and its floor plan carries a
        /// kitchen WING that projects past the shell, which is why the plan out-measures the elevation.
        /// Forty pixels is a margin somebody will be tempted to close by dialling the size to 0.48; the
        /// owner ruled on 2026-08-11 that the restaurant ships at 0.50, so the cap moves and the art keeps
        /// its size. Spending it the other way is one line here and one on
        /// <see cref="ShopSet"/>'s restaurant.</para>
        ///
        /// <para><b>⚠️ Lifting the cap is not the same as honouring it, and the difference is silent.</b>
        /// <c>maxTextureSize</c> is a power-of-two ceiling that Unity defaults to 2048, and writing the
        /// field does nothing until an asset is REIMPORTED. Measured on this kit's first slice:
        /// <c>Shopfront_generalStore</c> read back <b>2048×546</b> against a 3878×1034 sheet.
        /// <c>ShopSheetSlicer</c> is where that is handled, and why it reimports before it reads.</para>
        ///
        /// <para>ONE number: the pack, the import lock and the verify assert all read it, because two
        /// numbers that have to agree is the bug #352 recorded.</para>
        /// </summary>
        public const int ImportSizeCap = 4096;

        /// <summary>Facings every build bakes — the eight the owner ruled on 2026-08-05. The shop rigs
        /// carry <c>DIRS: 8</c> themselves; this is asserted against them, never assumed.</summary>
        public const int Facings = 8;

        public static string ShellStemFor(string key) => ShellStemPrefix + key;
        public static string LevelStemFor(string key, string level) => LevelStemPrefix + key + "_" + level;

        public static string ShellSheetPath(string key) => ShopsRoot + ShellStemFor(key) + ".png";
        public static string LevelSheetPath(string key, string level) =>
            ShopsRoot + LevelStemFor(key, level) + ".png";

        public static string ShellSpriteNameFor(string key, int facing) => $"{ShellStemFor(key)}_d{facing}";
        public static string LevelSpriteNameFor(string key, string level, int facing) =>
            $"{LevelStemFor(key, level)}_d{facing}";

        /// <summary>
        /// A contract entry's sheet stem. <b>The entry's own <c>level</c> is what tells the two families
        /// apart</b> — a shell carries the empty string, a level carries <c>ground</c>/<c>upper</c> — so
        /// nothing downstream has to remember which list it pulled an entry out of. That mattering is not
        /// hypothetical: shells and levels share the folder, and a stem built with the wrong prefix names
        /// a sheet that exists, so the slicer would happily cut a shell against an interior's cell.
        /// </summary>
        public static string StemFor(Entry e) =>
            e == null ? null
                      : string.IsNullOrEmpty(e.level) ? ShellStemFor(e.key)
                                                      : LevelStemFor(e.key, e.level);

        public static string SheetPathFor(Entry e) => e == null ? null : ShopsRoot + StemFor(e) + ".png";

        public static string SpriteNameFor(Entry e, int facing) =>
            e == null ? null : $"{StemFor(e)}_d{facing}";

        /// <summary>Every entry the contract carries, shells then levels — one sequence, because every
        /// consumer that walks the kit has to walk both and forgetting one is silent.</summary>
        public static IEnumerable<Entry> AllEntries(Contract c)
        {
            if (c?.shells != null) foreach (var e in c.shells) yield return e;
            if (c?.levels != null) foreach (var e in c.levels) yield return e;
        }

        /// <summary>The entry a sheet stem belongs to, or null. Matches on the stem the contract itself
        /// would produce, never on a parse of the file name — <c>ShopLevel_generalStore_ground</c> splits
        /// three ways and only one of them is right.</summary>
        public static Entry EntryForStem(Contract c, string stem)
        {
            foreach (var e in AllEntries(c))
                if (string.Equals(stem, StemFor(e), StringComparison.Ordinal)) return e;
            return null;
        }

        /// <summary>The pivot in cell px from the rect's own BOTTOM-left, which is what
        /// <c>Sprite.pivot</c> reports back. The contract stores it top-left (see
        /// <see cref="Entry.pivotX"/>), so this is the one flip and it lives here.</summary>
        public static Vector2 PivotPixels(Entry e) =>
            e == null ? Vector2.zero : new Vector2(e.pivotX, e.cellH - e.pivotY);

        /// <summary>
        /// How many rows of art sit BELOW the ground centre — exactly how far a bottom-centre pivot would
        /// sink this building. The <c>/buildings/</c> import default IS bottom-centre
        /// (<c>ArtImportPipeline.PivotFor</c>), so this is the size of the mistake the slicer overrides.
        /// </summary>
        public static int BelowGroundPad(Entry e) =>
            e == null ? 0 : Mathf.Max(0, e.cellH - Mathf.RoundToInt(e.pivotY));

        /// <summary>sRGB lives on <see cref="TextureImporterSettings"/> rather than on the importer —
        /// the same indirection <c>VillageBuildingKit.SRgbOf</c> records, restated because Art.Editor
        /// kits do not reference each other.</summary>
        public static bool SRgbOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.sRGBTexture;
        }

        // =================================================================================
        //  the builds
        // =================================================================================

        /// <summary>
        /// One shop St Peters builds: a trade, the shopfront PRESET that dresses it, and the
        /// <see cref="Size"/> both its shell and its interior are baked at.
        ///
        /// <para><b>Presets rather than dialled options, and that is a defence.</b> The rigs resolve an
        /// option as <c>opts[k] ?? fallback</c> — a misspelled key changes nothing and says nothing, which
        /// is the trap <see cref="InteriorKit.RoomOptionKeys"/> exists to catch. A build that spreads the
        /// rig's own <c>PRESETS</c> entry has no key surface to misspell, and the art director's taste for
        /// that trade comes along with it. Anything the owner wants dialled off a preset should be added
        /// as an explicit, tested key the way the village kit does it — not sprinkled here.</para>
        /// </summary>
        public readonly struct Build
        {
            /// <summary>Stable key. Shared with the site that places it and with the sheet stem, so a
            /// stem that cannot be matched to a build fails loudly rather than silently (the shrub kit's
            /// lesson).</summary>
            public readonly string Key;
            public readonly string Label;

            /// <summary>The rig's trade id — <c>generalStore</c>, <c>fishMarket</c>, … Not derived from
            /// <see cref="Key"/>: they coincide today and a camel-cased guess is how the shrub kit's keys
            /// went wrong.</summary>
            public readonly string Trade;

            /// <summary>The <c>Shopfront.PRESETS</c> entry the shell is dressed from.</summary>
            public readonly string Preset;

            /// <summary>The size BOTH the shell and every level bake at. The registration contract in one
            /// number: the shell and its interior must resolve the same Wd/Ln or the room does not stand
            /// under its own door. Seeded from the preset and asserted equal to it.</summary>
            public readonly double Size;

            /// <summary>Levels to bake, in order. <c>ground</c> always; <c>upper</c> only where the trade
            /// HAS one — see <see cref="HasUpper"/>, which is not a matter of taste.</summary>
            public readonly string[] Levels;

            public readonly string Why;

            public Build(string key, string label, string trade, string preset, double size,
                         string[] levels, string why)
            {
                Key = key; Label = label; Trade = trade; Preset = preset;
                Size = size; Levels = levels; Why = why;
            }
        }

        /// <summary>The level ids the building rig knows.</summary>
        public const string GroundLevel = "ground";
        public const string UpperLevel = "upper";

        /// <summary>
        /// Trades with no upper storey. <b>MEASURED, and it matters because the rig does not say so.</b>
        /// <c>ShopBuilding.dims({type:'fishMarket',level:'upper'})</c> does not throw and does not return
        /// an empty plan — it returns the GROUND rooms (<c>counter · cutting · ice</c>) under the name
        /// <c>upper</c>. Baked, that is a second copy of the ground floor stacked half a metre above the
        /// first, and nothing anywhere reports it. The kit's own plan table is the authority
        /// (README-buildings: fish market and takeout stand have no upper), and this is that table.
        /// </summary>
        public static bool HasUpper(string trade) =>
            !string.Equals(trade, "fishMarket", StringComparison.Ordinal) &&
            !string.Equals(trade, "takeoutStand", StringComparison.Ordinal);

        /// <summary>
        /// The shops St Peters opens, in the order they read walking up the lane.
        ///
        /// <para><b>Four of the kit's nine, and the other five are deliberately not built.</b> The owner
        /// ruled the first three on 2026-08-11 (general store, post office, restaurant); the fish market
        /// joined them when B2 placed one at Nine Mile Creek. The kit ships nine trades because it is a
        /// kit. Importing source is not a licence to wire content (CLAUDE.md rule 8) — a chandlery and a
        /// tavern nobody has sited would be scope, not scenery. All nine remain one <see cref="Build"/>
        /// away, and the bake path, contract and tests are written against the set rather than against
        /// whichever trades happen to be in it.</para>
        ///
        /// <para><b>⚠️ THE SET IS DRIVEN BY PLACEMENTS, AND THE FISH MARKET IS THE WORKED EXAMPLE.</b> It
        /// was in part 1's set as an INFERENCE from P3 — the owner had asked for "shop/store/restaurant"
        /// — and he replaced it with the post office rather than confirm it. Its measurements survived in
        /// <see cref="ImportSizeCap"/>'s note and in
        /// <c>ShopKitBakeTests.TradesWithoutAnUpperStoreyReturnTheGroundPlanForOne</c> (a fact about the
        /// KIT, independent of the set). It is back now because a REGION asked: Nine Mile Creek is the
        /// working wharf, and the ask arrived with ground under it. That is the order this table wants —
        /// a placement, then a build — and not the other way round.</para>
        ///
        /// <para><b>GROUND LEVELS ONLY in this slice.</b> Every one of these trades has a <c>flat</c>
        /// above the shop and the rig bakes it — but the stair between them is a level change, which is
        /// interior gameplay and belongs to another lane. The bake path takes <c>upper</c> today (the
        /// <see cref="Build.Levels"/> array is the only edit); nothing is baked that nothing can reach.</para>
        /// </summary>
        public static readonly Build[] ShopSet =
        {
            // The general store already had a site, a keeper and a counter with five vendors on it
            // (#353/#354/#356) — what it did not have was a shop. It stood as houseIsoRig with a bay
            // window doing duty as a storefront. This is the real thing, at the same authored site.
            new Build("generalStore", "General store", "generalStore", "harbourStore", 0.35,
                      new[] { GroundLevel },
                      "world-and-regions §6: basic gear, the clam licence, gas in a can — and the first " +
                      "door the player has a reason to open"),

            // The owner's second call of 2026-08-11, replacing this slice's fish market. The market was
            // part 1's INFERENCE from P3 and he did not ask for it; the post office he did. It is also
            // the cheaper building by a distance — 6 × 9 units at size 0.30 against the market's 6 × 8 at
            // 0.45 — and the one whose two rooms (a public lobby, a sorting room behind the wicket) read
            // as a service the player has a reason to walk into on day one.
            new Build("postOffice", "Post office", "postOffice", "villagePost", 0.30,
                      new[] { GroundLevel },
                      "the owner's 2026-08-11 ruling: a post office, not the market — P3's working " +
                      "coast is a place with services, and the market is one Build away when a " +
                      "placement asks for it"),

            // The owner named it. Also the kit's largest plan, which is why it is the build the import
            // cap was solved against (see ImportSizeCap).
            new Build("restaurant", "Restaurant", "restaurant", "wharfDiner", 0.50,
                      new[] { GroundLevel },
                      "the owner's 2026-08-06 ask, and P2's other half: somewhere the money goes"),

            // ⭐ THE FISH MARKET IS BACK, and it is Nine Mile Creek's rather than St Peters'. Part 1
            // inferred a market for the island, the owner replaced it with the post office (see the note
            // on the post office above) — and then the wharf asked for one, which is a different
            // building in a different place for a reason nobody had to infer: a fish wharf sells fish.
            // ⚠ NO UPPER STOREY, and that is the kit's plan table rather than this slice's taste —
            // HasUpper('fishMarket') is false, and asking the rig for one bakes a second copy of the
            // ground floor half a metre in the air with nothing reporting it.
            new Build("fishMarket", "Fish market", "fishMarket", "coopFishHouse", 0.45,
                      new[] { GroundLevel },
                      "B2: Nine Mile Creek is the working wharf, and P3's living coast is a place where " +
                      "what the fleet lands is sold — the trade the kit has always carried, asked for at " +
                      "last by a placement"),
        };

        public static Build? FindBuild(string key)
        {
            foreach (var b in ShopSet)
                if (string.Equals(b.Key, key, StringComparison.Ordinal)) return b;
            return null;
        }

        // =================================================================================
        //  the contract schema
        // =================================================================================

        [Serializable]
        public sealed class Contract
        {
            public string note;
            public string writtenBy;

            /// <summary>Pixels per metre the sheets were baked at — the rigs' own <c>PX</c>.</summary>
            public int ppu;
            public int facings;

            /// <summary>
            /// ⭐ Add this to a SHELL facing to get the interior facing that stands under it. MEASURED at
            /// bake time by <c>ShopRegistrationProbe</c>, exactly as the house/room pair measures its own.
            ///
            /// <para><b>Expected to be 0 for this kit, and that is the point of measuring it.</b> The
            /// house family's answer is 4, because <c>interiorIsoRig</c> puts its door on −Y while its
            /// shell puts one on +Y. The shop kit's room is the shopfront seen from inside and both doors
            /// are on +Y, so nothing needs turning. Writing 0 here because it was measured is a different
            /// claim from writing 0 because someone assumed the kits are alike — and carrying the house's
            /// 4 across by analogy would put every shop's doorway against its back wall.</para>
            /// </summary>
            public int shellFacingOffset;

            public string conventionNote;
            public string pivotNote;
            public string registrationReport;

            /// <summary>The cap the pack was solved against — recorded so the importer lock and the
            /// verify assert can be checked against the bake instead of against each other.</summary>
            public int importSizeCap;

            public Entry[] shells;
            public Entry[] levels;
        }

        [Serializable]
        public sealed class Entry
        {
            public string key;
            public string label;

            /// <summary>Shell entries: empty. Level entries: <c>ground</c> or <c>upper</c>.</summary>
            public string level;

            public string trade;
            public string preset;
            public float size;

            /// <summary>Catalog rig key, script path and global — so a re-bake against a changed rig
            /// shows up in the diff.</summary>
            public string rig;
            public string rigScript;
            public string rigGlobal;

            /// <summary>Sheet file name (no folder).</summary>
            public string sheet;

            /// <summary>The EXACT options expression <c>render()</c> was handed — the audit trail.</summary>
            public string optionsJs;

            public int facings;
            public int cols;
            public int rows;

            /// <summary>The CROPPED cell every facing is packed at.</summary>
            public int cellW;
            public int cellH;

            /// <summary>The rig's native cell, before the union crop. For a LEVEL this is per trade and
            /// per level (<c>ShopBuilding.sheet()</c>), not a rig constant — the rig's own README says
            /// "ask sheet(opts), don't assume", and it is right.</summary>
            public int nativeCellW;
            public int nativeCellH;

            public int cropX;
            public int cropY;

            /// <summary>The GROUND CENTRE of the footprint, in cropped-cell px from the cell's TOP-LEFT.
            /// Identical across all 8 facings of a bake — the kit's own first assert, and one this
            /// baker checks rather than trusts.</summary>
            public float pivotX;
            public float pivotY;

            /// <summary>The Unity sprite pivot: normalised, BOTTOM-origin.
            /// <c>(pivotX/cellW, (cellH − pivotY)/cellH)</c> — ADR 0026.</summary>
            public float unityPivotX;
            public float unityPivotY;

            public int sheetW;
            public int sheetH;

            /// <summary>Footprint in metres as the rig reports it: the honest-scale number, what the
            /// interior extent is checked against, and what the wall colliders are built from.</summary>
            public float footprintWidthMetres;
            public float footprintLengthMetres;

            /// <summary>LEVEL: where this floor sits in world height (<c>dims().levelZ</c>), so ground
            /// and upper stack on one ground point.</summary>
            public float levelZ;

            public string convention;

            /// <summary>Per facing, in cropped-cell px, top-left origin: the street door — the threshold
            /// the player crosses. Present on shells and levels both, which is what lets a test assert
            /// the two land on the same point instead of trusting the offset.</summary>
            public float[] doorX;
            public float[] doorY;

            /// <summary>LEVEL, per facing: the back door, where the plan has one. NaN where it has none —
            /// zero is a legal coordinate, so a missing door written as zero puts a doorway in the corner.</summary>
            public float[] backDoorX;
            public float[] backDoorY;

            public long pngBytes;
            public long runtimeBytesRgba32;
            public float cropSaving;
        }

        /// <summary>
        /// The Unity sprite pivot for an entry — the SINGLE definition, so the value written into the
        /// contract and the value the slicer applies cannot disagree. Bottom-origin and normalised.
        ///
        /// <para>⚠️ Not bottom-centre. Under the ¾ camera the near half of the footprint projects BELOW
        /// the ground centre, so a bottom-centre pivot sinks the building by that much, silently — the
        /// same trap <see cref="InteriorKit"/> records for rooms.</para>
        /// </summary>
        public static Vector2 NormalizedPivot(Entry e)
        {
            if (e == null || e.cellW <= 0 || e.cellH <= 0) return new Vector2(0.5f, 0.5f);
            return new Vector2(e.pivotX / e.cellW, (e.cellH - e.pivotY) / e.cellH);
        }

        // =================================================================================
        //  reading it back
        // =================================================================================

        /// <summary>The committed contract, or null with a logged error if it is missing/unparseable.</summary>
        public static Contract Load()
        {
            if (!File.Exists(ContractPath))
            {
                Debug.LogError(
                    $"[ShopKit] No contract at '{ContractPath}'. Run Hidden Harbours ▸ Art ▸ Bake Shops " +
                    "(shells + interiors) — the sheets and this file are written together, and a consumer " +
                    "reading one without the other slices a stale sheet against a fresh cell.");
                return null;
            }

            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath));
            if (contract?.shells == null || contract.shells.Length == 0)
            {
                Debug.LogError($"[ShopKit] '{ContractPath}' parsed but carries no shells.");
                return null;
            }
            return contract;
        }

        public static Entry FindShell(Contract c, string key) => FindIn(c?.shells, key, null);

        public static Entry FindLevel(Contract c, string key, string level) => FindIn(c?.levels, key, level);

        static Entry FindIn(Entry[] entries, string key, string level)
        {
            if (entries == null) return null;
            foreach (var e in entries)
                if (string.Equals(e.key, key, StringComparison.Ordinal) &&
                    (level == null || string.Equals(e.level, level, StringComparison.Ordinal)))
                    return e;
            return null;
        }
    }
}
#endif
