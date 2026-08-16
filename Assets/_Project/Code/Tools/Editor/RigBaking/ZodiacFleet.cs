using System;
using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// One build of <c>zodiacIsoRig.js</c> — the coast-guard RHIB's hurricane cabin or her open FRC —
    /// plus every name derived from that one word.
    ///
    /// <para>Same shape and the same reasoning as <see cref="LobsterVariant"/>: the mesh id, the
    /// visual id, the deck id, the asset file names and the sidecar file name are all functions of
    /// the build id, so a hull cannot end up with a deck called one thing and a mesh called another.
    /// The ids are the ones <c>FleetFlotationTableTests.Authored</c> is already keyed by
    /// (<c>hullmesh.zodiac_hurricane_iso</c>, <c>hullmesh.zodiac_frc_iso</c>), so this bake accepts
    /// that table's proposal rather than spending its keys on a rename.</para>
    ///
    /// <para><b>Two builds, ONE cell.</b> Unlike the sport fisher's two hulls, both zodiac builds are
    /// drawn in the same 272×248 cell at the same pivot (136,138) — measured in the repo's own V8,
    /// 2026-08-15 — because they are one loft with different tops, not two boats. So this family
    /// needs no <see cref="RigHullExtraction.HullScope"/>: her global publishes the cell, the
    /// elevation, the render and the anchors for both.</para>
    /// </summary>
    public readonly struct ZodiacBuild
    {
        /// <summary>The rig's own <c>BUILDS</c> key: hurricane / frc.</summary>
        public readonly string Build;

        /// <summary>Length over the tubes, from the drop's own sidecar. Labels only — never geometry,
        /// which is read from the rig at bake time like every other hull's.</summary>
        public readonly float LoaMetres;

        public ZodiacBuild(string build, float loaMetres)
        {
            Build = build; LoaMetres = loaMetres;
        }

        /// <summary>snake_case body shared by every id, e.g. <c>zodiac_hurricane_iso</c>.</summary>
        public string Snake => $"zodiac_{Build}_iso";

        /// <summary><see cref="HullMeshFleet"/>'s catalog key, in the camelCase the eleven use.</summary>
        public string Key => $"zodiac{Pascal(Build)}";

        /// <summary>The asset FILE stem shared by the mesh, the visual and the deck def. Chosen so
        /// that <c>DeckSidecarImporter.SnakeCase</c> of it is exactly <see cref="Snake"/> — the
        /// importer derives the deck id that way, and a stem that did not round-trip would silently
        /// mint a deck id that no table names.</summary>
        public string AssetName => $"Zodiac{Pascal(Build)}Iso";

        /// <summary>The sidecar file stem — <see cref="AssetName"/> with a lower-case initial, which
        /// is what the importer's <c>Capitalise(StripRigSuffix(stem))</c> turns back into
        /// <see cref="AssetName"/>. Named for the HULL, not the rig (#541).</summary>
        public string SidecarStem => char.ToLowerInvariant(AssetName[0]) + AssetName.Substring(1);

        public string SidecarFileName => $"{SidecarStem}.gameplay.json";

        public string MeshId => $"hullmesh.{Snake}";
        public string VisualId => $"visual.{Snake}";
        public string DeckId => $"deck.{Snake}";

        /// <summary>
        /// How the extractor reaches THIS build's face list: the per-file <c>variantFaces</c> shim
        /// (<see cref="RigMeshSymbols.Reconstructions"/>) called with the build id.
        ///
        /// <para><b><see cref="RigHullExtraction.ViewOptions"/> is the descriptor her PUBLIC entry
        /// points take</b> — <c>render(dir, opts)</c>, <c>painter(dir, opts)</c>,
        /// <c>sternEye(dir, opts)</c>. It is not a duplicate of the face expression: without it the
        /// azimuth probe photographs the rig's DEFAULT build and one hull's picture decides the
        /// other's convention, which is exactly the defect measured on all eighteen lobster cells.
        /// </para>
        /// </summary>
        public RigHullExtraction Extraction => new RigHullExtraction
        {
            FaceExpression = $"{ZodiacFleet.ShimSymbol}('{Build}')",
            ExtraSymbols = new[] { ZodiacFleet.ShimSymbol },
            ViewOptions = $"{{build:'{Build}'}}",
        };

        /// <summary>Human-readable, for log lines and the owner-facing report. Never parsed.</summary>
        public string Label => $"coast-guard zodiac {Build} (~{LoaMetres:0.##} m over the tubes)";

        static string Pascal(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        public override string ToString() => Build;
    }

    /// <summary>
    /// <b>The two boats the zodiac rig file makes</b> — the single place her build list, her naming
    /// and her extraction live.
    ///
    /// <para><b>Her contact-shadow list has no mesh equivalent, and that is deliberate.</b> The rig's
    /// private <c>facesOf(B)</c> returns <c>{F, SH, HALF}</c>, and <c>SH</c> is a ONE-FACE contact
    /// shadow (measured, both builds) that her renderer lays under the hull. <c>RigMeshData</c> has
    /// no shadow channel — no hull in the repo has ever had one — and a mesh hull's contact shadow is
    /// a rendering-time question that ADR 0022 answers elsewhere. So the bake takes <c>F</c> and
    /// leaves <c>SH</c> where it is: the mesh is HULL ONLY, exactly as it is for the other twenty-two
    /// hulls, and nothing about her shadow regresses because she never had one in Unity to begin
    /// with. Recorded here rather than in a commit message because the next reader of
    /// <c>variantFaces</c> will wonder where the other list went.</para>
    /// </summary>
    public static class ZodiacFleet
    {
        public const string ScriptPath = "docs/art/rigs/zodiacIsoRig.js";
        public const string GlobalName = "ZodiacIso";

        /// <summary>The name <see cref="RigMeshSymbols.Reconstructions"/> binds her private
        /// <c>facesOf</c> to. The same name the lobster generator's shim uses, deliberately: it is
        /// one shim CONCEPT — "reach this generator's face builder" — and the expression behind it is
        /// per-file.</summary>
        public const string ShimSymbol = "variantFaces";

        /// <summary>The rig's own <c>BUILDS</c> keys, in the rig's own order, with the LOA each
        /// build's sidecar records. Checked against the rig by <c>ZodiacFleetTests</c> — the only way
        /// this list can be known to still be the whole list.</summary>
        public static readonly IReadOnlyList<ZodiacBuild> All = new[]
        {
            new ZodiacBuild("hurricane", 7.28f),
            new ZodiacBuild("frc", 6.66f),
        };

        /// <summary>Her <c>buildIds</c> as one comparable string, for the guard that
        /// <see cref="All"/> is still the whole list.</summary>
        public static string BuildSignature(IRigScriptHost host) =>
            host.EvaluateString($"{GlobalName}.buildIds.join(',')");

        public static string ExpectedBuildSignature
        {
            get
            {
                var ids = new List<string>(All.Count);
                foreach (ZodiacBuild b in All) ids.Add(b.Build);
                return string.Join(",", ids);
            }
        }

        public static ZodiacBuild Get(string key)
        {
            foreach (ZodiacBuild b in All) if (b.Key == key) return b;
            throw new ArgumentException($"No zodiac build '{key}'.", nameof(key));
        }
    }
}
