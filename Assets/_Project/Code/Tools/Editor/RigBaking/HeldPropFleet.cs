using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// One held-prop RIG: a family of held states that the rig itself enumerates (ADR 0053). The
    /// fleet names the rig and nothing else about it — which states exist, their faces, their
    /// palette, their pose and their grip are all read off the rig at bake time.
    /// </summary>
    public readonly struct HeldPropFamily
    {
        /// <summary>The family as the def records it — <see cref="HeldPropMeshDef.RodFamily"/> etc.
        /// It is also the first word of every id in the family.</summary>
        public readonly string Family;

        /// <summary>The rig's key in <see cref="RigCatalog"/>, which owns its script path, its global
        /// and the turn convention it DECLARES. Nothing about the rig is restated here.</summary>
        public readonly string CatalogKey;

        /// <summary>Where the rig's cell lives, relative to its global (see
        /// <see cref="RigPropExtraction.CellPath"/>). Empty = the global's own W/H/pivot, which the rod,
        /// the spade and the fish export. The bucket exports two pivots and no <c>pivot</c>, so its
        /// cell comes through the reconstructed <c>held.cell</c>, which names the CARRY one.</summary>
        public readonly string CellPath;

        public readonly string Label;

        public HeldPropFamily(string family, string catalogKey, string cellPath, string label)
        {
            Family = family;
            CatalogKey = catalogKey;
            CellPath = cellPath;
            Label = label;
        }

        public string ScriptPath => RigCatalog.Get(CatalogKey).ScriptPath;
        public string GlobalName => RigCatalog.Get(CatalogKey).GlobalName;

        /// <summary>As the catalog DECLARES it — not measured (<see cref="RigAzimuthProbe"/> reads a
        /// boat's bow taper, which a pail or a rod does not have).</summary>
        public bool AzimuthCounterClockwise =>
            RigCatalog.Get(CatalogKey).DeclaredConvention == AzimuthConvention.CounterClockwise;
    }

    /// <summary>One held STATE — a rod tier, a species, a bucket at one fill — and so one asset.</summary>
    public readonly struct HeldProp
    {
        public readonly HeldPropFamily Owner;

        /// <summary>The state in the rig's own words (<c>cane</c>, <c>cod</c>, <c>pail_half_shell</c>);
        /// empty for a family with one state (the spade).</summary>
        public readonly string Variant;

        public readonly string PropId;
        public readonly string AssetPath;

        /// <summary>The sub-asset name of the baked mesh — the hull props' casing
        /// (<c>doryOarPortPropMesh</c> inside <c>DoryOarPortPropMesh.asset</c>).</summary>
        public readonly string MeshName;

        /// <summary>The extractor's road to this state: the reconstructed <c>held</c>'s builder and
        /// material table for this variant, the cell, and the paint rules.</summary>
        public readonly RigPropExtraction Extraction;

        public HeldProp(in HeldPropFamily owner, string variant, string propId, string assetPath,
                        string meshName, RigPropExtraction extraction)
        {
            Owner = owner;
            Variant = variant;
            PropId = propId;
            AssetPath = assetPath;
            MeshName = meshName;
            Extraction = extraction;
        }

        public string Family => Owner.Family;
        public string ScriptPath => Owner.ScriptPath;
        public string GlobalName => Owner.GlobalName;
        public string Label => Variant.Length == 0 ? Owner.Family : $"{Owner.Family} {Variant}";

        /// <summary>A JS expression on the rig's global — <c>held.&lt;member&gt;('&lt;variant&gt;')</c>.</summary>
        public string HeldCall(string member) =>
            $"{GlobalName}.{HeldPropFleet.HeldSymbol}.{member}('{Variant}')";
    }

    /// <summary>
    /// <b>Every prop a figure HOLDS, as a facet mesh (ADR 0053, PR 2)</b> — the held-prop twin of
    /// <see cref="HullPropFleet"/>: three rods at the rod rig's held stance, the clam spade, the
    /// seven fish by the tail, and every bucket state, one mesh per state, mirroring the sprite cells.
    ///
    /// <para><b>The fleet names RIGS, not props.</b> Four entries, one per rig, and that is the whole
    /// of the C# here: no tier, species, fill, face count or pose is typed. Each rig gets one
    /// reconstructed symbol, <c>held</c> (<see cref="RigMeshSymbols.Reconstructions"/>), whose
    /// <c>variants</c> list is the rig's own — the rod's <c>ORDER</c>, the fish's <c>ORDER</c>, the
    /// bucket's <c>TIERS × FILLS × CATCHES</c> — so a tier the art director adds is a state this
    /// bake makes, and an id the content validation then asks to be appended to its ledger.</para>
    ///
    /// <para><b>What stays a sprite</b> (D4, D7, D8): the clam, the fuel can, the knife and the gaff
    /// are not here, on purpose.</para>
    ///
    /// <para><b>Nothing reads the meshes yet.</b> PR 4 presents them on the figure's hand sockets and
    /// PR 3 draws their plotted lines; this PR only bakes them and points the held items at them.</para>
    /// </summary>
    public static class HeldPropFleet
    {
        /// <summary>The ONE symbol each held rig has reconstructed — see the "held props" block of
        /// <see cref="RigMeshSymbols.Reconstructions"/> for its members.</summary>
        public const string HeldSymbol = "held";

        /// <summary>
        /// The four rigs. Cells, palettes and poses come from the rigs; the only facts here are which
        /// rig, which family name its ids carry, and where its cell is.
        /// </summary>
        public static readonly HeldPropFamily[] Families =
        {
            new HeldPropFamily(HeldPropMeshDef.RodFamily, "rod", cellPath: "",
                               label: "rods, every tier, at the rod rig's held stance"),
            new HeldPropFamily(HeldPropMeshDef.ShovelFamily, "shovel", cellPath: "",
                               label: "the clam spade at its held pose"),
            new HeldPropFamily(HeldPropMeshDef.FishFamily, "fish2", cellPath: "",
                               label: "every species by the tail (the tail rest, frame 0)"),
            new HeldPropFamily(HeldPropMeshDef.BucketFamily, "bucket", cellPath: HeldSymbol + ".cell",
                               label: "every bucket tier at every fill, carried by its grip"),
        };

        /// <summary>
        /// The held states a rig enumerates, in its own order — read by running the rig with
        /// <c>held</c> widened in, exactly the string the extractor's pass 2 runs. The rig file is
        /// only ever READ.
        /// </summary>
        public static IReadOnlyList<string> VariantsOf(IRigScriptHost host, in HeldPropFamily family)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            string path = family.ScriptPath, g = family.GlobalName;
            string full = Path.Combine(RigCatalog.RepoRoot, path);
            if (!File.Exists(full))
                throw new FileNotFoundException($"Held-prop rig missing at {full}.", full);

            string source = RigMeshExtractor.ApplyInnerWidenings(File.ReadAllText(full), path);
            host.Execute(RigMeshExtractor.WidenExportedLiteral(source, g, new[] { HeldSymbol }, path));

            string blob = host.EvaluateString($"{g}.{HeldSymbol}.variants.join('|')");
            string[] variants = blob.Split('|');
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string v in variants)
            {
                if (!VariantShape.IsMatch(v))
                    throw new InvalidOperationException(
                        $"{path}: held state '{v}' is not lower_snake_case, so it cannot be an id " +
                        "(CLAUDE.md §5) or a JS literal. The rig's naming changed; re-aim the held " +
                        "reconstruction rather than escaping it here.");
                if (!seen.Add(v))
                    throw new InvalidOperationException($"{path}: held state '{v}' is listed twice.");
            }
            if (variants.Length > 1 && seen.Contains(""))
                throw new InvalidOperationException(
                    $"{path}: an empty held state among {variants.Length} — only a one-state family " +
                    "may leave its state unnamed.");
            return variants;
        }

        /// <summary>Every held state of every family — the whole bake, in family then rig order. Each
        /// rig is read in a fresh host of its own, as the extractor reads it, so no rig's globals can
        /// leak into another's.</summary>
        public static IReadOnlyList<HeldProp> Expand()
        {
            var all = new List<HeldProp>();
            foreach (HeldPropFamily f in Families)
            {
                using IRigScriptHost host = RigScriptHostFactory.Create();
                foreach (string v in VariantsOf(host, f))
                    all.Add(Make(f, v));
            }
            return all;
        }

        /// <summary>One state's id, asset path, mesh name and extraction.</summary>
        public static HeldProp Make(in HeldPropFamily family, string variant)
        {
            if (variant == null || !VariantShape.IsMatch(variant))
                throw new ArgumentException($"'{variant}' is not a held state name.", nameof(variant));

            string stem = Pascal(family.Family) + Pascal(variant) + "HeldMesh";
            var extraction = new RigPropExtraction
            {
                FaceBuilderCall = $"{HeldSymbol}.faces('{variant}')",
                MaterialsCall = $"{HeldSymbol}.mats('{variant}')",
                CellPath = family.CellPath,
                // None of the four exports its pixel scale either; `held` and `PX` are both
                // reconstructions (PX = the rig's own S, as for the skiff motor).
                ExtraSymbols = new[] { HeldSymbol, "PX" },
                // The hull rule: a back-lit face is re-lit only when b <= -1. It IS the rule of the
                // rod, spade and bucket (rodIsoRig.js:143, shovelIsoRig.js:109, bucketRig.js:229).
                // The fish rescues by a per-face `ds` flag (fishIsoRig2.js:222) the mesh cannot carry;
                // of the two rules the shader has, this one leaves its body right and only a back-lit
                // fin darker than the sprite — recorded in ADR 0053, judged on the proof sheet.
                BackfaceRescueNeedsOptIn = true,
                // All four rigs' _paint run the 1 px depth-edge darkening (EDGE read per rig).
                DepthEdgeDarkening = true,
            };
            return new HeldProp(family, variant, IdFor(family.Family, variant),
                                $"{RigPropMeshBaker.HeldPropFolder}/{stem}.asset",
                                char.ToLowerInvariant(stem[0]) + stem.Substring(1), extraction);
        }

        /// <summary><c>heldprop.&lt;family&gt;_&lt;state&gt;</c>, or <c>heldprop.&lt;family&gt;</c> for a
        /// one-state family. Ids are append-only (CLAUDE.md §5).</summary>
        public static string IdFor(string family, string variant) =>
            HeldPropMeshDef.IdPrefix + family + (string.IsNullOrEmpty(variant) ? "" : "_" + variant);

        public static HeldPropFamily FamilyNamed(string family)
        {
            foreach (var f in Families) if (f.Family == family) return f;
            throw new ArgumentException(
                $"No held-prop family '{family}'. Known: {string.Join(", ", Families.Select(f => f.Family))}.");
        }

        static readonly Regex VariantShape = new Regex("^([a-z0-9]+(_[a-z0-9]+)*)?$", RegexOptions.CultureInvariant);

        static string Pascal(string snake)
        {
            var sb = new StringBuilder(snake.Length);
            foreach (string part in snake.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries))
                sb.Append(char.ToUpperInvariant(part[0])).Append(part, 1, part.Length - 1);
            return sb.ToString();
        }
    }
}
