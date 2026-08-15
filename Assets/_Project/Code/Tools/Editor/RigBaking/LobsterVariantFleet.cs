using System;
using System.Collections.Generic;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// One cell of <c>lobsterBoatVariantsIsoRig.js</c> — a size, a style and a region — plus every
    /// name derived from those three words.
    ///
    /// <para><b>Nothing here is a choice made twice.</b> The mesh id, the visual id, the deck id, the
    /// asset file names and the sidecar file name are all functions of the same three axis ids, so a
    /// hull cannot end up with a deck called one thing and a mesh called another. The ids are the
    /// ones <c>docs/design/fleet-flotation.md</c> §7 proposed and both authored tables
    /// (<c>FleetFlotationTableTests</c>, <c>FleetDeckOccupancyTableTests</c>) are keyed by — this
    /// bake accepted the proposal rather than spending the two tables' keys on a rename.</para>
    /// </summary>
    public readonly struct LobsterVariant
    {
        /// <summary>The rig's own <c>SIZES</c> id: inshore / standard / offshore.</summary>
        public readonly string Size;
        /// <summary>The rig's own <c>STYLES</c> id: open / hardtop.</summary>
        public readonly string Style;
        /// <summary>The rig's own <c>REGIONS</c> id: northumberland / fundy / newfoundland.</summary>
        public readonly string Region;

        public LobsterVariant(string size, string style, string region)
        {
            Size = size; Style = style; Region = region;
        }

        /// <summary>The unambiguous key both authored tables print, e.g. <c>standard/hardtop/fundy</c>.
        /// </summary>
        public string Variant => $"{Size}/{Style}/{Region}";

        /// <summary>snake_case body shared by every id, e.g. <c>lobster_standard_hardtop_fundy_iso</c>.
        /// </summary>
        public string Snake => $"lobster_{Size}_{Style}_{Region}_iso";

        /// <summary><see cref="HullMeshFleet"/>'s catalog key, in the camelCase the eleven use.</summary>
        public string Key => $"lobster{Pascal(Size)}{Pascal(Style)}{Pascal(Region)}";

        /// <summary>The asset FILE stem shared by the mesh, the visual and the deck def, e.g.
        /// <c>LobsterStandardHardtopFundyIso</c>. Chosen so that
        /// <c>DeckSidecarImporter.SnakeCase</c> of it is exactly <see cref="Snake"/> — the importer
        /// derives the deck id that way, and a stem that did not round-trip would silently mint a
        /// deck id that no table names.</summary>
        public string AssetName => $"Lobster{Pascal(Size)}{Pascal(Style)}{Pascal(Region)}Iso";

        /// <summary>The sidecar file stem — <see cref="AssetName"/> with a lower-case initial, which
        /// is what the importer's <c>Capitalise(StripRigSuffix(stem))</c> turns back into
        /// <see cref="AssetName"/>. See <see cref="SidecarFileName"/> for why the file is named after
        /// the HULL rather than after the rig.</summary>
        public string SidecarStem =>
            char.ToLowerInvariant(AssetName[0]) + AssetName.Substring(1);

        /// <summary>
        /// <c>lobsterStandardHardtopFundyIso.gameplay.json</c>.
        ///
        /// <para><b>Named for the hull, not for the rig — and that is the whole widening.</b> The
        /// eleven committed sidecars are named after their rig file, which worked while every rig
        /// made exactly one boat. Eighteen hulls cannot share one file name, so the resolution now
        /// reads the rig out of the sidecar's own <c>rig</c> field (which every sidecar has always
        /// carried) instead of off the file name. For the eleven the two agree, so nothing about
        /// them changes; see <see cref="DeckSidecarReader.ResolveRigFileName"/>.</para>
        /// </summary>
        public string SidecarFileName => $"{SidecarStem}.gameplay.json";

        public string MeshId => $"hullmesh.{Snake}";
        public string VisualId => $"visual.{Snake}";
        public string DeckId => $"deck.{Snake}";

        /// <summary>The variant descriptor as the rig's own <c>resolve()</c> takes it.</summary>
        public string JsDescriptor => $"{{size:'{Size}',style:'{Style}',region:'{Region}'}}";

        /// <summary>
        /// How the extractor reaches THIS cell's face list: the per-file <c>variantFaces</c> shim
        /// (<see cref="RigMeshSymbols.Reconstructions"/>) called with this cell's descriptor.
        /// </summary>
        public RigHullExtraction Extraction => new RigHullExtraction
        {
            FaceExpression = $"{LobsterVariantFleet.ShimSymbol}({JsDescriptor})",
            ExtraSymbols = new[] { LobsterVariantFleet.ShimSymbol },
            ViewOptions = JsDescriptor,
        };

        /// <summary>Human-readable, for log lines and the owner-facing report. Never parsed.</summary>
        public string Label =>
            $"lobster {Size}/{Style}/{Region} (~{LobsterVariantFleet.LoaMetresFor(Size):0.#} m)";

        static string Pascal(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        public override string ToString() => Variant;
    }

    /// <summary>
    /// <b>The eighteen boats one rig file makes</b> — the single place the variant list, its naming
    /// and its extraction live.
    ///
    /// <para><b>Why a table of its own rather than eighteen rows in <see cref="HullMeshFleet"/>.</b>
    /// Four lanes need the same eighteen: the mesh fleet, the deck-sidecar importer's wiring
    /// manifest, the sidecar generator, and the tests that sweep them. Restating three axis lists
    /// four times is how the fourth copy goes stale — and the failure would be silent, because
    /// eighteen of anything is exactly the size nobody counts. So the axes are declared once here and
    /// <c>LobsterVariantFleetTests</c> checks them against the rig's own <c>SIZES</c>/<c>STYLES</c>/
    /// <c>REGIONS</c> tables, which is the only way this list can be known to still be the whole
    /// list.</para>
    ///
    /// <para>⚠️ <b>The axes are not decorative — all three are geometry.</b> Measured 18-of-18
    /// distinct by face fingerprint (<c>RigMeshVariantExtractionTests</c>) and by rendered cell
    /// (<c>LobsterVariantRigTests</c>). Face COUNT is not an oracle: two variants are both 637 faces
    /// and are different boats. And the rig resolves an UNKNOWN axis id to its DEFAULT silently, so a
    /// typo here would bake the standard boat under another hull's id — which is why the fingerprint
    /// sweep, not the count, is what guards this table.</para>
    ///
    /// <para><b>Her twelve paints are NOT an axis.</b> They move no vertex (alphaDiff 0 on all
    /// twelve) and her paint table is byte-identical to the hero hull's, so one mesh per cell serves
    /// every scheme and the committed <c>paint.lobster_*</c> defs already cover her.</para>
    /// </summary>
    public static class LobsterVariantFleet
    {
        public const string ScriptPath = "docs/art/rigs/lobsterBoatVariantsIsoRig.js";
        public const string GlobalName = "LobsterBoatVariantsIso";

        /// <summary>The name <see cref="RigMeshSymbols.Reconstructions"/> binds the generator's
        /// private face builder to. Deliberately not a name any rig already uses.</summary>
        public const string ShimSymbol = "variantFaces";

        /// <summary>The rig's own axis ids, in the rig's own order. Checked against the rig.</summary>
        public static readonly string[] Sizes = { "inshore", "standard", "offshore" };
        public static readonly string[] Styles = { "open", "hardtop" };
        public static readonly string[] Regions = { "northumberland", "fundy", "newfoundland" };

        /// <summary>The rig's <c>SIZES[].loa</c>, for labels only — never for geometry, which is read
        /// from the rig at bake time like every other hull's.</summary>
        public static float LoaMetresFor(string size) =>
            size == "inshore" ? 8.6f : size == "offshore" ? 14.6f : 12.0f;

        /// <summary>All eighteen, in size × style × region order — the order the two authored tables
        /// are written in, so a diff of any of the three reads the same way.</summary>
        public static readonly IReadOnlyList<LobsterVariant> All = Build();

        static LobsterVariant[] Build()
        {
            var all = new List<LobsterVariant>(18);
            foreach (string size in Sizes)
            foreach (string style in Styles)
            foreach (string region in Regions)
                all.Add(new LobsterVariant(size, style, region));
            return all.ToArray();
        }

        /// <summary>The rig's axis tables as one comparable string, for the guard that this list is
        /// still the whole list.</summary>
        public static string AxisSignature(IRigScriptHost host) =>
            host.EvaluateString(
                $"{GlobalName}.SIZES.map(s=>s.id).join(',')+'|'+" +
                $"{GlobalName}.STYLES.map(s=>s.id).join(',')+'|'+" +
                $"{GlobalName}.REGIONS.map(s=>s.id).join(',')");

        /// <summary>What <see cref="AxisSignature"/> must say for <see cref="All"/> to be complete.
        /// </summary>
        public static string ExpectedAxisSignature =>
            new StringBuilder().Append(string.Join(",", Sizes)).Append('|')
                               .Append(string.Join(",", Styles)).Append('|')
                               .Append(string.Join(",", Regions)).ToString();

        public static LobsterVariant Get(string key)
        {
            foreach (LobsterVariant v in All) if (v.Key == key) return v;
            throw new ArgumentException($"No lobster variant '{key}'.", nameof(key));
        }
    }
}
